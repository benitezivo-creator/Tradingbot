#region Using declarations
using System;
using System.Collections.Generic;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Data;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class ExcesoRotacionBot : Strategy
    {
        // =========================================================================
        // MOTOR DE ORDER FLOW (AGRESIÓN, DELTA, VELOCIDAD DE FLUJO Y BRACKET)
        // =========================================================================

        private struct PrintCinta
        {
            public DateTime Tiempo;
            public double VolumenCompra;
            public double VolumenVenta;
        }

        private struct EntradaBracketDelta
        {
            public long Volumen;
            public double DeltaFirmado;
        }

        // --- Estado de la cinta y agresor ---
        private double precioAnteriorTape = double.NaN;
        private int direccionAnteriorTape = 0;
        private double bidVivo = double.NaN;
        private double askVivo = double.NaN;

        // --- Delta acumulado ---
        private double deltaSesionAcumulado = 0.0;
        private long volumenSesionCompra = 0;
        private long volumenSesionVenta = 0;
        private double deltaPrevioBarra = 0.0;
        private double precioPrevioBarra = double.NaN;

        // --- Velocidad de flujo (Ventana rodante en tiempo) ---
        private readonly Queue<PrintCinta> ventanaVelocidad = new Queue<PrintCinta>(2048);
        private double volumenCompraVentana = 0.0;
        private double volumenVentaVentana = 0.0;
        private double velocidadCompraActual = 0.0;
        private double velocidadVentaActual = 0.0;
        private double velocidadFlujoTotal = 0.0;
        private int ladoDominanteVelocidad = 0; // +1 Compra, -1 Venta

        // --- Bracket Delta VND (Ventana constante en actividad de contratos) ---
        private readonly Queue<EntradaBracketDelta> colaBracket = new Queue<EntradaBracketDelta>(8192);
        private long volumenBracketActual = 0;
        private double deltaBracketActual = 0.0;
        private double deltaBracketMaximo = 0.0;
        private double deltaBracketMinimo = 0.0;
        private bool bracketCruzoCero = false;
        private bool bracketRetesteoCero = false;

        // Clasifica si un print es compra o venta agresiva
        private int ClasificarAgresor(double precio, double bid, double ask, bool enVivo)
        {
            int tickDir = 0;
            if (!double.IsNaN(precioAnteriorTape) && precioAnteriorTape > 0)
            {
                if (precio > precioAnteriorTape)
                    tickDir = 1;
                else if (precio < precioAnteriorTape)
                    tickDir = -1;
            }

            int quoteDir = 0;
            if (enVivo && UsarClasificacionBidAsk && !double.IsNaN(bid) && !double.IsNaN(ask) && ask >= bid)
            {
                double tol = TickSize * 0.5;
                if (precio >= ask - tol)
                    quoteDir = 1;
                else if (precio <= bid + tol)
                    quoteDir = -1;
            }

            int decision = 0;
            if (quoteDir != 0)
            {
                decision = quoteDir;
            }
            else if (tickDir != 0)
            {
                decision = tickDir;
            }
            else
            {
                // Regla de persistencia (Zero-tick): mantiene la dirección del agresor anterior
                // Crucial para detectar absorción institucional en un solo nivel
                decision = direccionAnteriorTape != 0 ? direccionAnteriorTape : 1;
            }

            precioAnteriorTape = precio;
            if (decision != 0)
                direccionAnteriorTape = decision;

            return decision;
        }

        // Procesa un trade individual
        private void ProcesarOrderFlow(DateTime tiempo, double precio, long volumen, double bid, double ask, bool enVivo)
        {
            if (volumen <= 0 || precio <= 0.0 || double.IsNaN(precio))
                return;

            if (VolumenMinimoTrade > 0 && volumen < VolumenMinimoTrade)
            {
                precioAnteriorTape = precio;
                return;
            }

            int lado = ClasificarAgresor(precio, bid, ask, enVivo);
            double deltaTrade = lado * (double)volumen;

            // 1. Actualizar delta acumulado de la sesión
            deltaSesionAcumulado += deltaTrade;
            if (lado > 0)
                volumenSesionCompra += volumen;
            else
                volumenSesionVenta += volumen;

            // 2. Actualizar ventana rodante de velocidad de flujo
            ActualizarVentanaVelocidad(tiempo, volumen, lado);

            // 3. Actualizar Bracket rodante de volumen
            ActualizarBracketDelta(volumen, deltaTrade);

            // 4. Evaluar migración y retesteo de cero en el bracket
            MonitorearMigracionBracket();
        }

        private void ActualizarVentanaVelocidad(DateTime tiempo, long volumen, int lado)
        {
            double volCompra = lado > 0 ? (double)volumen : 0.0;
            double volVenta = lado < 0 ? (double)volumen : 0.0;

            ventanaVelocidad.Enqueue(new PrintCinta
            {
                Tiempo = tiempo,
                VolumenCompra = volCompra,
                VolumenVenta = volVenta
            });

            volumenCompraVentana += volCompra;
            volumenVentaVentana += volVenta;

            double ventanaSegundos = Math.Max(0.1, VentanaVelocidadMs / 1000.0);

            while (ventanaVelocidad.Count > 0 && (tiempo - ventanaVelocidad.Peek().Tiempo).TotalSeconds > ventanaSegundos)
            {
                PrintCinta viejo = ventanaVelocidad.Dequeue();
                volumenCompraVentana = Math.Max(0.0, volumenCompraVentana - viejo.VolumenCompra);
                volumenVentaVentana = Math.Max(0.0, volumenVentaVentana - viejo.VolumenVenta);
            }

            velocidadCompraActual = volumenCompraVentana / ventanaSegundos;
            velocidadVentaActual = volumenVentaVentana / ventanaSegundos;
            velocidadFlujoTotal = velocidadCompraActual + velocidadVentaActual;

            if (velocidadCompraActual > velocidadVentaActual)
                ladoDominanteVelocidad = 1;
            else if (velocidadVentaActual > velocidadCompraActual)
                ladoDominanteVelocidad = -1;
            else
                ladoDominanteVelocidad = 0;
        }

        private void ActualizarBracketDelta(long volumen, double deltaTrade)
        {
            colaBracket.Enqueue(new EntradaBracketDelta
            {
                Volumen = volumen,
                DeltaFirmado = deltaTrade
            });

            volumenBracketActual += volumen;
            deltaBracketActual += deltaTrade;

            long umbral = Math.Max(100L, UmbralBracketContratos);
            while (volumenBracketActual > umbral && colaBracket.Count > 1)
            {
                EntradaBracketDelta expulsado = colaBracket.Dequeue();
                volumenBracketActual -= expulsado.Volumen;
                deltaBracketActual -= expulsado.DeltaFirmado;
            }

            if (deltaBracketActual > deltaBracketMaximo) deltaBracketMaximo = deltaBracketActual;
            if (deltaBracketActual < deltaBracketMinimo) deltaBracketMinimo = deltaBracketActual;
        }

        private void MonitorearMigracionBracket()
        {
            // Migración: delta viaja de un extremo al opuesto cruzando cero
            if (!bracketCruzoCero)
            {
                if ((deltaBracketMinimo < -500 && deltaBracketActual > 100) ||
                    (deltaBracketMaximo > 500 && deltaBracketActual < -100))
                {
                    bracketCruzoCero = true;
                }
            }
            else
            {
                // Retest de cero sin perderlo
                if (Math.Abs(deltaBracketActual) < 150)
                {
                    bracketRetesteoCero = true;
                }
            }
        }

        private void ReiniciarDeltaSesion()
        {
            deltaSesionAcumulado = 0.0;
            volumenSesionCompra = 0;
            volumenSesionVenta = 0;
            deltaPrevioBarra = 0.0;
            precioPrevioBarra = double.NaN;

            ventanaVelocidad.Clear();
            volumenCompraVentana = 0.0;
            volumenVentaVentana = 0.0;
            velocidadFlujoTotal = 0.0;

            colaBracket.Clear();
            volumenBracketActual = 0;
            deltaBracketActual = 0.0;
            deltaBracketMaximo = 0.0;
            deltaBracketMinimo = 0.0;
            bracketCruzoCero = false;
            bracketRetesteoCero = false;
        }

        // Evalúa si el delta es convergente o divergente con el precio
        private bool EsDeltaConvergente(double cambioPrecio, double cambioDelta)
        {
            if (Math.Abs(cambioPrecio) < TickSize * 0.5)
                return false;

            // Precio sube y delta sube -> Convergente
            if (cambioPrecio > 0 && cambioDelta > 0)
                return true;

            // Precio baja y delta baja -> Convergente
            if (cambioPrecio < 0 && cambioDelta < 0)
                return true;

            // Divergente (movimiento hueco)
            return false;
        }

        // Evalúa si el mercado está en estrés de delta acumulado
        private bool EstaDeltaEstresado()
        {
            return Math.Abs(deltaSesionAcumulado) >= UmbralEstresDelta;
        }
    }
}

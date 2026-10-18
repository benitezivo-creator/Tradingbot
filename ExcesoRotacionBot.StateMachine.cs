#region Using declarations
using System;
using System.Collections.Generic;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Data;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class ExcesoRotacionBot : Strategy
    {
        // =========================================================================
        // MÁQUINA DE ESTADOS: EXCESO, ROTACIÓN, GATILLO DE APERTURA Y PONDERACIÓN
        // =========================================================================

        public enum EstadoBot
        {
            IDLE,
            EXCESO,
            ROTACION,
            SENAL,
            EN_POSICION,
            PROTEGIDO,
            RUNNER,
            CERRADO
        }

        public enum TipoOperacion
        {
            Ninguna,
            Scalp,
            Estructural
        }

        public enum DireccionSenal
        {
            Ninguna,
            Compra,
            Venta
        }

        // --- Estado de la máquina ---
        private EstadoBot estadoActual = EstadoBot.IDLE;
        private DireccionSenal direccionPendiente = DireccionSenal.Ninguna;
        private TipoOperacion tipoOperacionActual = TipoOperacion.Ninguna;

        // --- Datos del Exceso activo ---
        private bool eventoExcesoActivo = false;
        private int ladoExceso = 0;              // +1 Compra (extremo = maximo), -1 Venta (extremo = minimo)
        private double precioExceso = double.NaN;
        private double extremoExceso = double.NaN;
        private int barraInicioExceso = -1;
        private int barrasTranscurridasExceso = 0;
        private double velocidadMaximaRegistrada = 0.0;

        // --- Datos de la Rotación confirmada ---
        private double precioConfirmacionRotacion = double.NaN;
        private bool esRetestContinuacion = false;
        private bool esGiroCompleto = false;

        // --- Gatillo de Apertura (09:30) ---
        private bool gatilloAperturaProcesado = false;
        private bool primeraBarra15mAperturaProcesada = false;
        private bool esEventoApertura = false;

        // --- Scoring y Ponderación calculada ---
        private double scorePonderacionActual = 0.0;
        private string detallePonderacionActual = string.Empty;

        // --- Filtro de Cooldown Post-Trade ---
        private int barraUltimoCierre = -100;

        // Evalúa tick a tick la detección y mantenimiento del Exceso
        private void EvaluarExcesoTick(DateTime tiempo, double precio)
        {
            if (estadoActual == EstadoBot.EN_POSICION || estadoActual == EstadoBot.PROTEGIDO || estadoActual == EstadoBot.RUNNER)
                return;

            bool superaUmbral = velocidadFlujoTotal >= UmbralVelocidadExceso;

            // 1. SI supera umbral y NO había exceso activo: inicia nuevo evento
            if (superaUmbral && !eventoExcesoActivo)
            {
                if (ladoDominanteVelocidad != 0)
                {
                    eventoExcesoActivo = true;
                    ladoExceso = ladoDominanteVelocidad;
                    precioExceso = precio;
                    extremoExceso = precio;
                    velocidadMaximaRegistrada = velocidadFlujoTotal;
                    barraInicioExceso = CurrentBars[0];
                    barrasTranscurridasExceso = 0;
                    estadoActual = EstadoBot.EXCESO;

                    // Log / Telemetría
                    RegistrarLog(string.Format("EXCESO DETECTADO [{0}] en {1:F2} | Vel: {2:F1} | Hora NY: {3:HH:mm:ss}",
                        ladoExceso > 0 ? "COMPRA" : "VENTA", precio, velocidadFlujoTotal, AHoraNuevaYork(tiempo)));
                }
            }
            // 2. MIENTRAS siga superando el umbral: actualizar extremo alcanzado
            else if (superaUmbral && eventoExcesoActivo)
            {
                if (velocidadFlujoTotal > velocidadMaximaRegistrada)
                    velocidadMaximaRegistrada = velocidadFlujoTotal;

                if (ladoExceso > 0) // Exceso de compra: extremo es el máximo
                {
                    if (precio > extremoExceso) extremoExceso = precio;
                }
                else if (ladoExceso < 0) // Exceso de venta: extremo es el mínimo
                {
                    if (precio < extremoExceso) extremoExceso = precio;
                }
            }
            // 3. Velocidad cayó por debajo del umbral: el evento de exceso quedó fijado
            else if (!superaUmbral && eventoExcesoActivo && estadoActual == EstadoBot.EXCESO)
            {
                // El exceso pasa a esperar confirmación de rotación
            }
        }

        // Evalúa en cada tick o cierre de barra si se confirma la Rotación
        private void EvaluarRotacionTick(double precio)
        {
            if (estadoActual != EstadoBot.EXCESO || !eventoExcesoActivo)
                return;

            double tick = Math.Max(1E-10, TickSize);
            double distanciaTicks = 0.0;
            bool rotacionConfirmada = false;

            // SI exceso fue de VENTA (extremo = mínimo): rotación = precio SUBE desde extremo
            if (ladoExceso < 0)
            {
                distanciaTicks = (precio - extremoExceso) / tick;
                if (distanciaTicks >= DistanciaMinimaRotacionTicks)
                {
                    rotacionConfirmada = true;
                    direccionPendiente = DireccionSenal.Compra;
                }
            }
            // SI exceso fue de COMPRA (extremo = máximo): rotación = precio BAJA desde extremo
            else if (ladoExceso > 0)
            {
                distanciaTicks = (extremoExceso - precio) / tick;
                if (distanciaTicks >= DistanciaMinimaRotacionTicks)
                {
                    rotacionConfirmada = true;
                    direccionPendiente = DireccionSenal.Venta;
                }
            }

            if (rotacionConfirmada)
            {
                estadoActual = EstadoBot.ROTACION;
                precioConfirmacionRotacion = precio;

                RegistrarLog(string.Format("ROTACIÓN CONFIRMADA [{0}] en {1:F2} | Giro: {2:F1} ticks desde extremo {3:F2}",
                    direccionPendiente, precio, distanciaTicks, extremoExceso));

                // Evaluar dos vías: Retest o Giro Completo
                EvaluarDosVias(precio);

                // Calcular ponderación y emitir señal si pasa el filtro
                ProcesarPonderacionYGenerarSenal(precio);
            }
        }

        // Descuenta el plazo de confirmación del exceso en cada barra
        private void AvanzarBarraExceso()
        {
            if (estadoActual == EstadoBot.EXCESO && eventoExcesoActivo)
            {
                barrasTranscurridasExceso++;
                if (barrasTranscurridasExceso > PlazoMaximoRotacionBarras)
                {
                    // Plazo vencido sin confirmación de giro -> descartar evento
                    RegistrarLog(string.Format("EXCESO DESCARTADO: Plazo vencido ({0} barras) sin rotación mínima",
                        PlazoMaximoRotacionBarras));

                    ReiniciarMaquinaEstados();
                }
            }
        }

        // LÓGICA DE LAS DOS VÍAS (Desenlace tras rotación)
        private void EvaluarDosVias(double precio)
        {
            double tick = Math.Max(1E-10, TickSize);
            double distanciaAlExtremo = Math.Abs(precio - extremoExceso) / tick;

            // Si el precio regresa a la zona del extremo (< 4 ticks) y sigue en sentido del exceso -> RETEST
            if (distanciaAlExtremo <= 4.0)
            {
                esRetestContinuacion = true;
                esGiroCompleto = false;
            }
            else
            {
                esGiroCompleto = true;
                esRetestContinuacion = false;
            }
        }

        // GATILLO DE APERTURA (09:30 NY)
        // 1ª barra de 15m tocando/rompiendo Europa H/L + Exceso + Rotación
        private bool EvaluarGatilloApertura(DateTime tiempoNy, double highBarra, double lowBarra)
        {
            if (gatilloAperturaProcesado)
                return false;

            TimeSpan horaNy = tiempoNy.TimeOfDay;

            // Ventana de la primera barra de 15 minutos: 09:30 -> 09:45
            if (horaNy >= new TimeSpan(9, 30, 0) && horaNy <= new TimeSpan(9, 45, 0))
            {
                if (europaCompleta && !double.IsNaN(ultimoEuropaHigh) && !double.IsNaN(ultimoEuropaLow))
                {
                    bool tocaRompeEuropaHigh = highBarra >= ultimoEuropaHigh - TickSize * 2;
                    bool tocaRompeEuropaLow = lowBarra <= ultimoEuropaLow + TickSize * 2;

                    if (tocaRompeEuropaHigh || tocaRompeEuropaLow)
                    {
                        if (estadoActual == EstadoBot.ROTACION)
                        {
                            esEventoApertura = true;
                            gatilloAperturaProcesado = true;
                            RegistrarLog("GATILLO DE APERTURA (09:30) DISPARADO sobre niveles de Europa!");
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        // LÓGICA DEL DELTA (Scalp o Estructural)
        private TipoOperacion CalificarTipoOperacion(double cambioPrecio, double cambioDelta)
        {
            bool convergente = EsDeltaConvergente(cambioPrecio, cambioDelta);
            bool migracionExitosa = bracketCruzoCero && bracketRetesteoCero;

            // Convergente o migración exitosa -> ESTRUCTURAL
            if (convergente || migracionExitosa)
            {
                return TipoOperacion.Estructural;
            }

            // Divergente o migración fallida (rebote en cero) -> SCALP
            return TipoOperacion.Scalp;
        }

        // LÓGICA DE PONDERACIÓN (Scoring de Niveles + Delta)
        private void ProcesarPonderacionYGenerarSenal(double precio)
        {
            double score = 0.0;
            List<string> confluencias = new List<string>();
            double tolTicks = ToleranciaNivelTicks;
            double tolPrecio = tolTicks * TickSize;

            // 1. Pesos por nivel donde ocurrió el exceso
            bool conEuropa = false;
            bool conAsia = false;
            bool conVwap = false;
            bool conValVah = false;
            bool conPoc = false;
            bool conNivelFijo = false;

            // High/Low Europa (Peso 10)
            if (europaCompleta)
            {
                if (Math.Abs(extremoExceso - ultimoEuropaHigh) <= tolPrecio || Math.Abs(extremoExceso - ultimoEuropaLow) <= tolPrecio)
                {
                    score += 10.0;
                    conEuropa = true;
                    confluencias.Add("H/L Europa (+10)");
                }
            }

            // High/Low Asia (Peso 8)
            if (asiaCompleta)
            {
                if (Math.Abs(extremoExceso - ultimoAsiaHigh) <= tolPrecio || Math.Abs(extremoExceso - ultimoAsiaLow) <= tolPrecio)
                {
                    score += 8.0;
                    conAsia = true;
                    confluencias.Add("H/L Asia (+8)");
                }
            }

            // VWAP (Peso 6)
            if (!double.IsNaN(vwapActual) && Math.Abs(extremoExceso - vwapActual) <= tolPrecio)
            {
                score += 6.0;
                conVwap = true;
                confluencias.Add("VWAP (+6)");
            }

            // VAH / VAL (Peso 5)
            if (!double.IsNaN(vahPrecio) && Math.Abs(extremoExceso - vahPrecio) <= tolPrecio)
            {
                score += 5.0;
                conValVah = true;
                confluencias.Add("VAH (+5)");
            }
            else if (!double.IsNaN(valPrecio) && Math.Abs(extremoExceso - valPrecio) <= tolPrecio)
            {
                score += 5.0;
                conValVah = true;
                confluencias.Add("VAL (+5)");
            }

            // POC (Peso 4)
            if (!double.IsNaN(pocPrecio) && Math.Abs(extremoExceso - pocPrecio) <= tolPrecio)
            {
                score += 4.0;
                conPoc = true;
                confluencias.Add("POC (+4)");
            }

            // Niveles Fijos Institucionales (Peso 5)
            double nivelFijoCercano;
            if (EstaCercaDeNivelFijo(extremoExceso, tolTicks, out nivelFijoCercano))
            {
                score += 5.0;
                conNivelFijo = true;
                confluencias.Add(string.Format("Nivel Fijo {0:F2} (+5)", nivelFijoCercano));
            }

            // Si no tocó ningún nivel clave
            if (confluencias.Count == 0)
            {
                score += 1.0;
                confluencias.Add("Sin Nivel (+1)");
            }

            // BONUS DE CONFLUENCIA: varios niveles coincidentes pesan más que su suma
            if (confluencias.Count >= 2)
            {
                double bonusConfluencia = confluencias.Count * 2.5;
                score += bonusConfluencia;
                confluencias.Add(string.Format("Bonus Confluencia (+{0:F1})", bonusConfluencia));
            }

            // 2. MODIFICADOR DE DELTA
            double cambioPrecio = Closes[0][0] - Opens[0][0];
            double cambioDelta = deltaSesionAcumulado - deltaPrevioBarra;
            bool deltaConvergente = EsDeltaConvergente(cambioPrecio, cambioDelta);
            bool migracionExitosa = bracketCruzoCero && bracketRetesteoCero;

            if (deltaConvergente || migracionExitosa)
            {
                score += 5.0;
                confluencias.Add("Delta Convergente / Migración (+5)");
                tipoOperacionActual = TipoOperacion.Estructural;
            }
            else
            {
                score -= 4.0;
                confluencias.Add("Delta Divergente / Muerto (-4)");
                tipoOperacionActual = TipoOperacion.Scalp;
            }

            // Gatillo de Apertura agrega puntaje decisivo (+8)
            DateTime tiempoNy = AHoraNuevaYork(Times[0][0]);
            if (EvaluarGatilloApertura(tiempoNy, Highs[0][0], Lows[0][0]))
            {
                score += 8.0;
                confluencias.Add("Evento Apertura (+8)");
            }

            scorePonderacionActual = score;
            detallePonderacionActual = string.Join(" | ", confluencias);

            RegistrarLog(string.Format("PONDERACIÓN: Score {0:F1} (Mín: {1:F1}) | Tipo: {2} | Detalle: {3}",
                score, ScoreMinimoSenal, tipoOperacionActual, detallePonderacionActual));

            // Si el score supera el umbral mínimo -> EMITIR SEÑAL
            if (score >= ScoreMinimoSenal && direccionPendiente != DireccionSenal.Ninguna)
            {
                // Validación de Cooldown post-trade
                if (!ValidarCooldown())
                {
                    ReiniciarMaquinaEstados();
                    return;
                }

                estadoActual = EstadoBot.SENAL;

                // Feedback visual en el gráfico
                if (direccionPendiente == DireccionSenal.Compra)
                {
                    Draw.ArrowUp(this, "Senal_Compra_" + CurrentBars[0], false, 0, Lows[0][0] - TickSize * 4, Brushes.LimeGreen);
                    Draw.Text(this, "Txt_Compra_" + CurrentBars[0], false, string.Format("COMPRA ({0:F0})", score), 0, Lows[0][0] - TickSize * 8, 0, Brushes.LimeGreen, new SimpleFont("Arial", 11) { Bold = true }, System.Windows.TextAlignment.Center, Brushes.Transparent, Brushes.Transparent, 0);
                }
                else if (direccionPendiente == DireccionSenal.Venta)
                {
                    Draw.ArrowDown(this, "Senal_Venta_" + CurrentBars[0], false, 0, Highs[0][0] + TickSize * 4, Brushes.Red);
                    Draw.Text(this, "Txt_Venta_" + CurrentBars[0], false, string.Format("VENTA ({0:F0})", score), 0, Highs[0][0] + TickSize * 8, 0, Brushes.Red, new SimpleFont("Arial", 11) { Bold = true }, System.Windows.TextAlignment.Center, Brushes.Transparent, Brushes.Transparent, 0);
                }

                // AI Vision Dataset: Exportar muestra de señal y snapshot
                ExportarMuestraVision(direccionPendiente.ToString(), score, detallePonderacionActual);

                // Ejecución: en Modo Solo Dataset (Shadow) no se lanzan órdenes reales
                if (!ModoSoloDataset)
                {
                    EjecutarEntradaEstrategia(direccionPendiente, tipoOperacionActual);
                }
                else
                {
                    RegistrarLog("MODO SHADOW (SOLO DATASET): Señal y frame capturados. Ejecución omitida.");
                    ReiniciarMaquinaEstados();
                }
            }
            else
            {
                RegistrarLog(string.Format("SEÑAL NO EMITIDA: Score insuficiente ({0:F1} < {1:F1})", score, ScoreMinimoSenal));
                ReiniciarMaquinaEstados();
            }
        }

        private void ReiniciarMaquinaEstados()
        {
            estadoActual = EstadoBot.IDLE;
            eventoExcesoActivo = false;
            ladoExceso = 0;
            precioExceso = double.NaN;
            extremoExceso = double.NaN;
            barraInicioExceso = -1;
            barrasTranscurridasExceso = 0;
            velocidadMaximaRegistrada = 0.0;
            direccionPendiente = DireccionSenal.Ninguna;
            tipoOperacionActual = TipoOperacion.Ninguna;
            precioConfirmacionRotacion = double.NaN;
            esRetestContinuacion = false;
            esGiroCompleto = false;
            esEventoApertura = false;
        }

        private void ProcesarFiltroYCooldown()
        {
            // Registrar la barra del último cierre cuando la posición vuelve a Flat
            if (Position.MarketPosition == MarketPosition.Flat && PositionAccount != null && PositionAccount.MarketPosition != MarketPosition.Flat)
            {
                barraUltimoCierre = CurrentBars[0];
            }
        }

        private bool ValidarCooldown()
        {
            if (CurrentBars[0] - barraUltimoCierre < CooldownBarrasPostTrade)
            {
                RegistrarLog(string.Format("[COOLDOWN] Señal omitida. Barras transcurridas: {0} / {1}",
                    CurrentBars[0] - barraUltimoCierre, CooldownBarrasPostTrade));
                return false;
            }
            return true;
        }
    }
}

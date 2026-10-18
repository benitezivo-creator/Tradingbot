#region Using declarations
using System;
using System.Collections.Generic;
using NinjaTrader.Cbi;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Data;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class ExcesoRotacionBot : Strategy
    {
        // =========================================================================
        // GESTIÓN DE EJECUCIÓN Y RIESGO EN TRES TERCIOS
        // =========================================================================
        // Posición en 3 tercios iguales:
        // - Stop Loss: 20 puntos (toda la posición)
        // - TP1 (1/3): +15 puntos -> mover Stop a Break-Even
        // - TP2 (1/3): +30 puntos
        // - Runner (1/3): se deja correr en LARGOS (gestión activa > 100 puntos)
        //                 en CORTOS el runner NO se deja correr (se cierra en TP2 o target fijo)
        // =========================================================================

        private const string ORDEN_L1 = "Entrada_L1";
        private const string ORDEN_L2 = "Entrada_L2";
        private const string ORDEN_L3 = "Entrada_L3";

        private const string ORDEN_S1 = "Entrada_S1";
        private const string ORDEN_S2 = "Entrada_S2";
        private const string ORDEN_S3 = "Entrada_S3";

        private double precioEntradaPromedio = 0.0;
        private double maximoRecorridoFavorablePuntos = 0.0;
        private bool tp1Alcanzado = false;
        private bool tp2Alcanzado = false;
        private bool posicionProtegidaBE = false;
        private bool enGestionRunner = false;

        // Configuración de Brackets y ejecución
        private void ConfigurarBracketsEntrada()
        {
            // Convertir puntos a ticks (para NQ 1 punto = 4 ticks, en ES 1 punto = 4 ticks)
            double puntosPorTick = TickSize > 0 ? 1.0 / (TickSize * 4.0) : 1.0;
            int ticksPorPunto = (int)Math.Round(1.0 / Math.Max(1E-10, TickSize));

            int stopLossTicks = (int)Math.Round(StopLossPuntos * ticksPorPunto);
            int tp1Ticks = (int)Math.Round(TP1Puntos * ticksPorPunto);
            int tp2Ticks = (int)Math.Round(TP2Puntos * ticksPorPunto);
            int tpCortosRunnerTicks = (int)Math.Round(TPRunnerCortosPuntos * ticksPorPunto);

            // Tercios Largos
            SetStopLoss(ORDEN_L1, CalculationMode.Ticks, stopLossTicks, false);
            SetProfitTarget(ORDEN_L1, CalculationMode.Ticks, tp1Ticks);

            SetStopLoss(ORDEN_L2, CalculationMode.Ticks, stopLossTicks, false);
            SetProfitTarget(ORDEN_L2, CalculationMode.Ticks, tp2Ticks);

            SetStopLoss(ORDEN_L3, CalculationMode.Ticks, stopLossTicks, false);
            // El runner en largos no lleva profit target fijo (se deja correr con gestión > 100 pts)

            // Tercios Cortos
            SetStopLoss(ORDEN_S1, CalculationMode.Ticks, stopLossTicks, false);
            SetProfitTarget(ORDEN_S1, CalculationMode.Ticks, tp1Ticks);

            SetStopLoss(ORDEN_S2, CalculationMode.Ticks, stopLossTicks, false);
            SetProfitTarget(ORDEN_S2, CalculationMode.Ticks, tp2Ticks);

            SetStopLoss(ORDEN_S3, CalculationMode.Ticks, stopLossTicks, false);
            // EN CORTOS: El runner no se deja correr; se le asigna target acotado
            if (!PermitirRunnerEnCortos)
            {
                SetProfitTarget(ORDEN_S3, CalculationMode.Ticks, tpCortosRunnerTicks);
            }
        }

        // Ejecuta la entrada al mercado dividida en tres tercios iguales
        private void EjecutarEntradaEstrategia(DireccionSenal direccion, TipoOperacion tipoOp)
        {
            if (Position.MarketPosition != MarketPosition.Flat)
            {
                RegistrarLog("ENTRADA RECHAZADA: Ya existe una posición abierta.");
                return;
            }

            int contratosPorTercio = Math.Max(1, CantidadPorTercio);

            // Reiniciar métricas de trade
            precioEntradaPromedio = 0.0;
            maximoRecorridoFavorablePuntos = 0.0;
            tp1Alcanzado = false;
            tp2Alcanzado = false;
            posicionProtegidaBE = false;
            enGestionRunner = false;

            if (direccion == DireccionSenal.Compra)
            {
                RegistrarLog(string.Format("EJECUTANDO COMPRA (3x{0} contratos) [{1}] | SL: {2} pts | TP1: {3} pts | TP2: {4} pts",
                    contratosPorTercio, tipoOp, StopLossPuntos, TP1Puntos, TP2Puntos));

                EnterLong(contratosPorTercio, ORDEN_L1);
                EnterLong(contratosPorTercio, ORDEN_L2);
                EnterLong(contratosPorTercio, ORDEN_L3);

                estadoActual = EstadoBot.EN_POSICION;
            }
            else if (direccion == DireccionSenal.Venta)
            {
                RegistrarLog(string.Format("EJECUTANDO VENTA (3x{0} contratos) [{1}] | SL: {2} pts | TP1: {3} pts | TP2: {4} pts (Runner corto acotado)",
                    contratosPorTercio, tipoOp, StopLossPuntos, TP1Puntos, TP2Puntos));

                EnterShort(contratosPorTercio, ORDEN_S1);
                EnterShort(contratosPorTercio, ORDEN_S2);
                EnterShort(contratosPorTercio, ORDEN_S3);

                estadoActual = EstadoBot.EN_POSICION;
            }
        }

        // Monitoreo continuo de la posición abierta (Trailing, Break Even y Runner)
        private void MonitorearPosicionAbierta(double precioActual)
        {
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                if (estadoActual == EstadoBot.EN_POSICION || estadoActual == EstadoBot.PROTEGIDO || estadoActual == EstadoBot.RUNNER)
                {
                    estadoActual = EstadoBot.CERRADO;
                    ReiniciarMaquinaEstados();
                }
                return;
            }

            if (precioEntradaPromedio <= 0.0)
                precioEntradaPromedio = Position.AveragePrice;

            // Calcular recorrido favorable en puntos
            double recorridoPuntos = 0.0;
            if (Position.MarketPosition == MarketPosition.Long)
                recorridoPuntos = precioActual - precioEntradaPromedio;
            else if (Position.MarketPosition == MarketPosition.Short)
                recorridoPuntos = precioEntradaPromedio - precioActual;

            if (recorridoPuntos > maximoRecorridoFavorablePuntos)
                maximoRecorridoFavorablePuntos = recorridoPuntos;

            // 1. REGLA: Al alcanzar TP1 (+15 puntos) -> Mover Stop a Break-Even
            if (!posicionProtegidaBE && recorridoPuntos >= TP1Puntos)
            {
                posicionProtegidaBE = true;
                tp1Alcanzado = true;
                estadoActual = EstadoBot.PROTEGIDO;

                // Mover stop loss a precio de entrada para los tercios restantes
                if (Position.MarketPosition == MarketPosition.Long)
                {
                    SetStopLoss(ORDEN_L2, CalculationMode.Price, precioEntradaPromedio, false);
                    SetStopLoss(ORDEN_L3, CalculationMode.Price, precioEntradaPromedio, false);
                }
                else if (Position.MarketPosition == MarketPosition.Short)
                {
                    SetStopLoss(ORDEN_S2, CalculationMode.Price, precioEntradaPromedio, false);
                    SetStopLoss(ORDEN_S3, CalculationMode.Price, precioEntradaPromedio, false);
                }

                RegistrarLog(string.Format("TP1 ALCANZADO (+{0:F1} pts) -> STOP LOSS MOVIDO A BREAK EVEN ({1:F2})",
                    recorridoPuntos, precioEntradaPromedio));
            }

            // 2. REGLA: TP2 (+30 puntos) alcanzado -> Estado RUNNER
            if (!tp2Alcanzado && recorridoPuntos >= TP2Puntos)
            {
                tp2Alcanzado = true;

                if (Position.MarketPosition == MarketPosition.Long)
                {
                    estadoActual = EstadoBot.RUNNER;
                    RegistrarLog(string.Format("TP2 ALCANZADO (+{0:F1} pts) -> TERCIO RUNNER EN JUEGO", recorridoPuntos));
                }
                else
                {
                    // En cortos, si no se permite runner, cerramos aquí o se espera su target acotado
                    RegistrarLog(string.Format("TP2 ALCANZADO (+{0:F1} pts) en CORTOS", recorridoPuntos));
                }
            }

            // 3. REGLA: Por encima de ~100 puntos de recorrido -> Gestionar el runner
            if (estadoActual == EstadoBot.RUNNER && Position.MarketPosition == MarketPosition.Long)
            {
                if (recorridoPuntos >= UmbralActivacionRunnerPuntos)
                {
                    enGestionRunner = true;

                    // Trailing stop activo: asegurar beneficio a 20 puntos por debajo del máximo alcanzado
                    double precioTrailingStop = precioActual - DistanciaTrailingRunnerPuntos;

                    // Solo subir el stop, nunca bajarlo
                    SetStopLoss(ORDEN_L3, CalculationMode.Price, precioTrailingStop, false);

                    RegistrarLog(string.Format("GESTIÓN RUNNER > {0} pts (Actual: +{1:F1} pts) -> Trailing SL ajustado a {2:F2}",
                        UmbralActivacionRunnerPuntos, recorridoPuntos, precioTrailingStop));
                }
            }
        }

        // Cierre preventivo de posiciones al finalizar la sesión de trading
        private void ComprobarCierreFinDeSesion(DateTime tiempoNy)
        {
            if (Position.MarketPosition == MarketPosition.Flat)
                return;

            TimeSpan horaNy = tiempoNy.TimeOfDay;

            // Si llegamos a las 16:55 NY (5 minutos antes del corte) -> CERRAR TODO
            if (horaNy >= new TimeSpan(16, 55, 0))
            {
                RegistrarLog("FIN DE SESIÓN (16:55 NY) -> Cerrando todas las posiciones abiertas por horario.");
                if (Position.MarketPosition == MarketPosition.Long)
                    ExitLong();
                else if (Position.MarketPosition == MarketPosition.Short)
                    ExitShort();

                ReiniciarMaquinaEstados();
                estadoActual = EstadoBot.CERRADO;
            }
        }
    }
}

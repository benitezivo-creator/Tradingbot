#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using NinjaTrader.Cbi;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class ExcesoRotacionBot : Strategy
    {
        // =========================================================================
        // ESTRATEGIA PRINCIPAL: EXCESO Y ROTACIÓN — ILT TRADINGBOT (NINJATRADER 8)
        // =========================================================================

        [NinjaScriptProperty]
        [Display(Name = "Exportar Dataset Imágenes", Description = "Guarda captura y metadatos en cada señal para entrenamiento de visión.", Order = 1, GroupName = "06. AI & Dataset")]
        public bool ExportarDatasetVision { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Ruta Exportación Dataset", Description = "Ruta local donde se almacenarán los eventos e imágenes.", Order = 2, GroupName = "06. AI & Dataset")]
        public string RutaDatasetVision { get; set; }

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description                                 = "Bot de Trading institucional basado en Exceso, Rotación, Order Flow y Confluencia Multinivel.";
                Name                                        = "ExcesoRotacionBot";
                Calculate                                   = Calculate.OnEachTick;
                EntriesPerDirection                         = 3; // 3 tercios
                EntryHandling                               = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy                = true;
                ExitOnSessionCloseSeconds                   = 300; // Salir 5 min antes del cierre (16:55 NY)
                IsFillLimitOnTouch                          = false;
                MaximumBarsLookBack                         = MaximumBarsLookBack.Infinite;
                OrderFillResolution                         = OrderFillResolution.Standard;
                Slippage                                    = 0;
                StartBehavior                               = StartBehavior.WaitUntilFlat;
                TimeInForce                                 = TimeInForce.Gtc;
                TraceOrders                                 = false;
                RealtimeErrorHandling                       = RealtimeErrorHandling.StopCancelClose;
                StopTargetHandling                          = StopTargetHandling.PerEntryExecution;
                BarsRequiredToTrade                         = 20;

                // 01. Gestión de Posición y Riesgo
                CantidadPorTercio                           = 1;
                StopLossPuntos                              = 20.0;
                TP1Puntos                                   = 15.0;
                TP2Puntos                                   = 30.0;
                UmbralActivacionRunnerPuntos                = 100.0;
                DistanciaTrailingRunnerPuntos               = 20.0;
                PermitirRunnerEnCortos                      = false;
                TPRunnerCortosPuntos                        = 30.0;
                CooldownBarrasPostTrade                     = 8;
                ModoSoloDataset                             = false;

                // 02. Detección de Exceso y Rotación
                UmbralVelocidadExceso                       = 80.0; // contratos/segundo en ventana rodante
                VentanaVelocidadMs                          = 1000; // 1 segundo
                DistanciaMinimaRotacionTicks                = 6;    // ticks mínimos de giro desde extremo
                PlazoMaximoRotacionBarras                   = 8;    // barras de espera máximas

                // 03. Order Flow y Delta
                UsarClasificacionBidAsk                     = true;
                VolumenMinimoTrade                          = 0;
                UmbralBracketContratos                      = 10000;
                UmbralEstresDelta                           = 1500.0;

                // 04. Confluencia y Ponderación
                ToleranciaNivelTicks                        = 4;
                ScoreMinimoSenal                            = 12.0;
                ActivarGatilloApertura                      = true;

                // 05. Diagnóstico y Logs
                ActivarLogs                                 = true;

                // 06. AI & Dataset Config
                ExportarDatasetVision                       = true;
                RutaDatasetVision                           = @"C:\Users\SIBS\Documents\Tradingbot\vision_dataset";
            }
            else if (State == State.Configure)
            {
                // Serie secundaria interna de 1 tick para reconstruir el order flow, la cinta y el delta con exactitud
                AddDataSeries(BarsPeriodType.Tick, 1);
            }
            else if (State == State.DataLoaded)
            {
                InicializarZonaHoraria();
                ReiniciarDeltaSesion();
                ReiniciarPerfilSesion();
                ReiniciarMaquinaEstados();
                ConfigurarBracketsEntrada();

                if (ExportarDatasetVision && !Directory.Exists(RutaDatasetVision))
                {
                    try
                    {
                        Directory.CreateDirectory(RutaDatasetVision);
                        Directory.CreateDirectory(Path.Combine(RutaDatasetVision, "images"));
                    }
                    catch (Exception ex)
                    {
                        RegistrarLog("ERROR AL CREAR DIRECTORIO DATASET: " + ex.Message);
                    }
                }

                RegistrarLog("=== INICIALIZACIÓN COMPLETADA: ExcesoRotacionBot listo ===");
            }
            else if (State == State.Terminated)
            {
                RegistrarLog("=== ESTRATEGIA DETENIDA ===");
            }
        }

        protected override void OnBarUpdate()
        {
            // BIP 1: Serie interna de 1 Tick para cálculo de Order Flow en histórico y replay
            if (BarsInProgress == 1)
            {
                if (CurrentBars[1] < 0 || State == State.Realtime)
                    return;

                DateTime tiempo = Times[1][0];
                double precio = Closes[1][0];
                long volumen = Math.Max(1L, Convert.ToInt64(Volumes[1][0]));

                // Procesar Order Flow histórico con tick rule
                ProcesarOrderFlow(tiempo, precio, volumen, double.NaN, double.NaN, false);
                ActualizarSesionesYPerfil(tiempo, precio, volumen);

                // Evaluar exceso y rotación
                EvaluarExcesoTick(tiempo, precio);
                EvaluarRotacionTick(precio);
                return;
            }

            // BIP 0: Serie primaria del gráfico (temporal, range o renko)
            if (BarsInProgress != 0 || CurrentBars[0] < BarsRequiredToTrade)
                return;

            DateTime tiempoBarraNy = AHoraNuevaYork(Times[0][0]);

            // Avanzar reloj del exceso (timeout en barras)
            AvanzarBarraExceso();

            // Guardar fotos de delta y precio al cierre de barra para calcular convergencias
            deltaPrevioBarra = deltaSesionAcumulado;
            precioPrevioBarra = Closes[0][0];

            // Recalcular Value Area de la sesión
            RecalcularValueArea(0.70);

            // Monitorear posición abierta al cierre de cada barra primaria
            MonitorearPosicionAbierta(Closes[0][0]);

            // Monitorear filtro de cooldown post-trade
            ProcesarFiltroYCooldown();

            // Comprobar cierre preventivo al final de sesión
            ComprobarCierreFinDeSesion(tiempoBarraNy);
        }

        protected override void OnMarketData(MarketDataEventArgs e)
        {
            if (e == null || State != State.Realtime)
                return;

            if (e.MarketDataType == MarketDataType.Bid)
            {
                if (e.Price > 0) bidVivo = e.Price;
                return;
            }

            if (e.MarketDataType == MarketDataType.Ask)
            {
                if (e.Price > 0) askVivo = e.Price;
                return;
            }

            if (e.MarketDataType == MarketDataType.Last)
            {
                double precio = e.Price;
                long volumen = Math.Max(1L, e.Volume);
                DateTime tiempo = e.Time;
                double bid = (!double.IsNaN(e.Bid) && e.Bid > 0) ? e.Bid : bidVivo;
                double ask = (!double.IsNaN(e.Ask) && e.Ask > 0) ? e.Ask : askVivo;

                // 1. Alimentar Order Flow y Cinta en vivo
                ProcesarOrderFlow(tiempo, precio, volumen, bid, ask, true);

                // 2. Alimentar Sesiones, VWAP y Perfil de Volumen
                ActualizarSesionesYPerfil(tiempo, precio, volumen);

                // 3. Evaluar máquina de estados de Exceso y Rotación tick a tick
                EvaluarExcesoTick(tiempo, precio);
                EvaluarRotacionTick(precio);

                // 4. Gestión activa de la posición (BE, TP2, Trailing Runner)
                MonitorearPosicionAbierta(precio);
            }
        }

        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity,
            MarketPosition marketPosition, string orderId, DateTime time)
        {
            if (execution == null || execution.Order == null)
                return;

            // Registro de llenados de órdenes
            RegistrarLog(string.Format("EJECUCIÓN: {0} {1}x @ {2:F2} | Orden: {3} | Posición Actual: {4}",
                execution.Order.Name, quantity, price, orderId, marketPosition));

            if (Position.MarketPosition != MarketPosition.Flat)
            {
                if (precioEntradaPromedio <= 0.0)
                    precioEntradaPromedio = Position.AveragePrice;
            }
            else
            {
                // Posición totalmente cerrada
                if (estadoActual == EstadoBot.EN_POSICION || estadoActual == EstadoBot.PROTEGIDO || estadoActual == EstadoBot.RUNNER)
                {
                    RegistrarLog("POSICIÓN TOTALMENTE CERRADA -> Volviendo a IDLE.");
                    barraUltimoCierre = CurrentBars[0];
                    ReiniciarMaquinaEstados();
                }
            }
        }

        private void RegistrarLog(string mensaje)
        {
            if (ActivarLogs)
            {
                Print(string.Format("[{0:HH:mm:ss.fff}] {1}", DateTime.Now, mensaje));
            }
        }

        // =========================================================================
        // PIPELINE DATASET VISION: EXPORTACIÓN DE SEÑALES Y CAPTURA DEL GRÁFICO
        // =========================================================================
        private void ExportarMuestraVision(string tipoSenal, double score, string confluencias)
        {
            if (!ExportarDatasetVision) return;

            try
            {
                if (!Directory.Exists(RutaDatasetVision))
                {
                    Directory.CreateDirectory(RutaDatasetVision);
                    Directory.CreateDirectory(Path.Combine(RutaDatasetVision, "images"));
                }

                string idMuestra = string.Format("sample_{0:yyyyMMdd_HHmmss}_{1}", DateTime.Now, tipoSenal);
                string rutaJson = Path.Combine(RutaDatasetVision, "labels.json");
                string rutaImagen = Path.Combine(RutaDatasetVision, "images", idMuestra + ".png");

                string entradaJson = string.Format(
                    "{{\"id\":\"{0}\",\"timestamp\":\"{1:o}\",\"signal\":\"{2}\",\"score\":{3:F2},\"confluences\":\"{4}\",\"price\":{5:F2},\"delta\":{6:F1},\"image_path\":\"images/{0}.png\"}},",
                    idMuestra, DateTime.Now, tipoSenal, score, (confluencias ?? string.Empty).Replace("\"", "'"), Closes[0][0], deltaSesionAcumulado
                );

                File.AppendAllText(rutaJson, entradaJson + Environment.NewLine);

                // Si NT8 tiene habilitado el Chart Control, captura el frame
                if (ChartControl != null)
                {
                    ChartControl.Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            int width = (int)Math.Max(1, ChartControl.ActualWidth);
                            int height = (int)Math.Max(1, ChartControl.ActualHeight);
                            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
                                width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                            rtb.Render(ChartControl);

                            using (var stream = new FileStream(rutaImagen, FileMode.Create))
                            {
                                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                                encoder.Save(stream);
                            }
                        }
                        catch (Exception imgEx)
                        {
                            RegistrarLog("AVISO AL GUARDAR CAPTURA DE GRÁFICO: " + imgEx.Message);
                        }
                    });
                }

                RegistrarLog(string.Format("AI DATASET: Muestra registrada -> {0}", idMuestra));
            }
            catch (Exception ex)
            {
                RegistrarLog("ERROR AL EXPORTAR DATASET VISION: " + ex.Message);
            }
        }
    }
}

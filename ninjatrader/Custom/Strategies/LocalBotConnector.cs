#region Using declarations
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public class LocalBotConnector : Strategy
    {
        public enum RolConector
        {
            Ejecutor_MNQ,        // Se activa en MNQ: Recibe órdenes de 3 tercios desde Python
            Analizador_NQ,       // Se activa en NQ: Envía telemetría de ticks y niveles a Python
            Hibrido_Auto         // Autodetecta según el instrumento del gráfico
        }

        private const string ORDEN_L1 = "Entrada_L1";
        private const string ORDEN_L2 = "Entrada_L2";
        private const string ORDEN_L3 = "Entrada_L3";

        private const string ORDEN_S1 = "Entrada_S1";
        private const string ORDEN_S2 = "Entrada_S2";
        private const string ORDEN_S3 = "Entrada_S3";

        private TcpClient socketClient;
        private NetworkStream stream;
        private string serverIp = "127.0.0.1";
        private int serverPort = 5555;
        private bool isConnected = false;
        private DateTime lastConnectAttempt = DateTime.MinValue;

        private double precioEntrada = 0.0;
        private bool tp1Alcanzado = false;
        private bool protegidoBE = false;

        #region Properties
        [NinjaScriptProperty]
        [Display(Name = "Rol del Conector", Description = "Define si este gráfico analiza (NQ) o ejecuta órdenes (MNQ).", Order = 1, GroupName = "1. Configuración Cross-Instrument")]
        public RolConector Rol { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Solo Ticks en Tiempo Real", Description = "Ignora ticks históricos al cargar el gráfico.", Order = 2, GroupName = "1. Configuración Cross-Instrument")]
        public bool OnlyRealtime { get; set; }

        [NinjaScriptProperty]
        [Range(1, 10)]
        [Display(Name = "Contratos por Tercio", Description = "Número de contratos asignados a cada uno de los 3 tercios.", Order = 3, GroupName = "2. Gestión en 3 Tercios")]
        public int ContratosPorTercio { get; set; }

        [NinjaScriptProperty]
        [Range(1, 200)]
        [Display(Name = "Stop Loss (Puntos)", Description = "Stop Loss rígido para toda la posición en puntos (20 pts = 80 ticks en NQ/MNQ).", Order = 4, GroupName = "2. Gestión en 3 Tercios")]
        public double StopLossPuntos { get; set; }

        [NinjaScriptProperty]
        [Range(1, 300)]
        [Display(Name = "Take Profit 1 (Puntos)", Description = "Target del primer tercio (15 pts = 60 ticks) -> Mueve Stop a BE.", Order = 5, GroupName = "2. Gestión en 3 Tercios")]
        public double TP1Puntos { get; set; }

        [NinjaScriptProperty]
        [Range(1, 500)]
        [Display(Name = "Take Profit 2 (Puntos)", Description = "Target del segundo tercio (30 pts = 120 ticks).", Order = 6, GroupName = "2. Gestión en 3 Tercios")]
        public double TP2Puntos { get; set; }

        [NinjaScriptProperty]
        [Range(1, 500)]
        [Display(Name = "TP Cortos Runner (Puntos)", Description = "Target del tercer tercio solo en cortos (45 pts = 180 ticks). En largos corre libre.", Order = 7, GroupName = "2. Gestión en 3 Tercios")]
        public double TPRunnerCortosPuntos { get; set; }
        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description                             = "Conector Institucional Cross-Instrument E&R (Análisis en NQ, Ejecución en MNQ) en 3 Tercios";
                Name                                    = "LocalBotConnector";
                Calculate                               = Calculate.OnEachTick;
                IsInstantiatedOnEachOptimizationIteration = false;
                OnlyRealtime                            = true;
                Rol                                     = RolConector.Hibrido_Auto;
                
                ContratosPorTercio                      = 1;
                StopLossPuntos                          = 20.0; // 80 ticks
                TP1Puntos                               = 15.0; // 60 ticks
                TP2Puntos                               = 30.0; // 120 ticks
                TPRunnerCortosPuntos                    = 45.0; // 180 ticks
                
                EntriesPerDirection                     = 3; // Permite los 3 tercios
                EntryHandling                           = EntryHandling.AllEntries;
            }
            else if (State == State.Configure)
            {
                // Configurar Brackets de 3 tercios nativos
                int ticksPorPunto = (int)Math.Round(1.0 / Math.Max(1E-10, TickSize));
                int slTicks = (int)Math.Round(StopLossPuntos * ticksPorPunto);
                int tp1Ticks = (int)Math.Round(TP1Puntos * ticksPorPunto);
                int tp2Ticks = (int)Math.Round(TP2Puntos * ticksPorPunto);
                int tpCortosTicks = (int)Math.Round(TPRunnerCortosPuntos * ticksPorPunto);

                // Tercios Largos
                SetStopLoss(ORDEN_L1, CalculationMode.Ticks, slTicks, false);
                SetProfitTarget(ORDEN_L1, CalculationMode.Ticks, tp1Ticks);

                SetStopLoss(ORDEN_L2, CalculationMode.Ticks, slTicks, false);
                SetProfitTarget(ORDEN_L2, CalculationMode.Ticks, tp2Ticks);

                SetStopLoss(ORDEN_L3, CalculationMode.Ticks, slTicks, false);

                // Tercios Cortos
                SetStopLoss(ORDEN_S1, CalculationMode.Ticks, slTicks, false);
                SetProfitTarget(ORDEN_S1, CalculationMode.Ticks, tp1Ticks);

                SetStopLoss(ORDEN_S2, CalculationMode.Ticks, slTicks, false);
                SetProfitTarget(ORDEN_S2, CalculationMode.Ticks, tp2Ticks);

                SetStopLoss(ORDEN_S3, CalculationMode.Ticks, slTicks, false);
                SetProfitTarget(ORDEN_S3, CalculationMode.Ticks, tpCortosTicks);
            }
            else if (State == State.DataLoaded)
            {
                ConnectToLocalBot();
            }
            else if (State == State.Terminated)
            {
                DisconnectBot();
            }
        }

        private void ConnectToLocalBot()
        {
            if (isConnected) return;
            lastConnectAttempt = DateTime.Now;

            try
            {
                socketClient = new TcpClient();
                var result = socketClient.BeginConnect(serverIp, serverPort, null, null);
                bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                if (!success || !socketClient.Connected)
                {
                    socketClient.Close();
                    isConnected = false;
                    Print(string.Format("LocalBotConnector: Esperando conexión con Bot Python en {0}:{1}...", serverIp, serverPort));
                    return;
                }
                socketClient.EndConnect(result);

                stream = socketClient.GetStream();
                isConnected = true;
                Print(string.Format("LocalBotConnector: ¡Conectado al Bot Python! [Instrumento: {0} | Rol: {1}]", Instrument.FullName, Rol));
                Task.Run(() => ListenToBotResponses());
            }
            catch (Exception ex)
            {
                Print("LocalBotConnector Estado de Conexión: " + ex.Message);
                isConnected = false;
            }
        }

        protected override void OnBarUpdate()
        {
            if (OnlyRealtime && State != State.Realtime) return;
            if (CurrentBar < 1) return;

            if (!isConnected)
            {
                if ((DateTime.Now - lastConnectAttempt).TotalSeconds > 5)
                {
                    ConnectToLocalBot();
                }
                return;
            }

            try
            {
                string rolStr = Rol == RolConector.Hibrido_Auto ? (Instrument.FullName.Contains("MNQ") ? "EXECUTOR" : "ANALYZER") : (Rol == RolConector.Ejecutor_MNQ ? "EXECUTOR" : "ANALYZER");

                string jsonPayload = string.Format(
                    CultureInfo.InvariantCulture,
                    "{{\"instrument\":\"{0}\",\"role\":\"{1}\",\"close\":{2:F2},\"volume\":{3:F0},\"time\":\"{4}\"}}\n",
                    Instrument.FullName,
                    rolStr,
                    Close[0],
                    Volume[0],
                    Time[0].ToString("o")
                );

                byte[] data = Encoding.UTF8.GetBytes(jsonPayload);
                stream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                Print("Error al enviar tick al Bot Python: " + ex.Message);
                isConnected = false;
            }
        }

        private async Task ListenToBotResponses()
        {
            byte[] buffer = new byte[1024];
            while (isConnected && socketClient != null && socketClient.Connected)
            {
                try
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;
                    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                    string[] signals = response.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var sig in signals)
                    {
                        TriggerCustomEvent(state => ProcessSignal(state as string), sig.Trim());
                    }
                }
                catch
                {
                    break;
                }
            }
            isConnected = false;
        }

        private void ProcessSignal(string signal)
        {
            if (string.IsNullOrEmpty(signal)) return;

            Print(string.Format("LocalBotConnector: Señal recibida -> [{0}] en {1} | Posición actual: {2} ({3})", 
                signal, Instrument.FullName, Position.MarketPosition, Position.Quantity));

            if (signal.Contains("BUY"))
            {
                if (Position.MarketPosition == MarketPosition.Long && Position.Quantity >= 3)
                {
                    Print("LocalBotConnector: BUY ignorada (los 3 tercios Long ya están activos).");
                    return;
                }

                if (Position.MarketPosition == MarketPosition.Short)
                    CerrarTodaLaPosicion();

                tp1Alcanzado = false;
                protegidoBE = false;
                precioEntrada = Close[0];

                // Ejecución escalonada en 3 tercios
                EnterLong(ContratosPorTercio, ORDEN_L1);
                EnterLong(ContratosPorTercio, ORDEN_L2);
                EnterLong(ContratosPorTercio, ORDEN_L3);

                Draw.ArrowUp(this, "BuyArrow_" + CurrentBar, true, 0, Low[0] - (TickSize * 4), Brushes.LimeGreen);
            }
            else if (signal.Contains("SELL"))
            {
                if (Position.MarketPosition == MarketPosition.Short && Position.Quantity >= 3)
                {
                    Print("LocalBotConnector: SELL ignorada (los 3 tercios Short ya están activos).");
                    return;
                }

                if (Position.MarketPosition == MarketPosition.Long)
                    CerrarTodaLaPosicion();

                tp1Alcanzado = false;
                protegidoBE = false;
                precioEntrada = Close[0];

                // Ejecución escalonada en 3 tercios
                EnterShort(ContratosPorTercio, ORDEN_S1);
                EnterShort(ContratosPorTercio, ORDEN_S2);
                EnterShort(ContratosPorTercio, ORDEN_S3);

                Draw.ArrowDown(this, "SellArrow_" + CurrentBar, true, 0, High[0] + (TickSize * 4), Brushes.Crimson);
            }
            else if (signal.Contains("FLATTEN"))
            {
                CerrarTodaLaPosicion();
            }
        }

        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            // Gestión dinámica: Al tocar TP1 -> mover stop de los tercios restantes a Break-Even
            if (execution.Order != null && execution.Order.OrderState == OrderState.Filled)
            {
                string orderName = execution.Order.Name;

                if (orderName.Contains(ORDEN_L1) || orderName.Contains(ORDEN_S1))
                {
                    if (execution.Order.OrderAction == OrderAction.Sell || execution.Order.OrderAction == OrderAction.BuyToCover)
                    {
                        // Se llenó el Profit Target del Tercio 1 (+15 pts)
                        tp1Alcanzado = true;
                        if (!protegidoBE && precioEntrada > 0)
                        {
                            protegidoBE = true;
                            if (Position.MarketPosition == MarketPosition.Long)
                            {
                                SetStopLoss(ORDEN_L2, CalculationMode.Price, precioEntrada, false);
                                SetStopLoss(ORDEN_L3, CalculationMode.Price, precioEntrada, false);
                                Print(string.Format("LocalBotConnector: TP1 alcanzado (+15 pts). Stop Loss de L2 y L3 movido a Break-Even ({0})", precioEntrada));
                            }
                            else if (Position.MarketPosition == MarketPosition.Short)
                            {
                                SetStopLoss(ORDEN_S2, CalculationMode.Price, precioEntrada, false);
                                SetStopLoss(ORDEN_S3, CalculationMode.Price, precioEntrada, false);
                                Print(string.Format("LocalBotConnector: TP1 alcanzado (+15 pts). Stop Loss de S2 y S3 movido a Break-Even ({0})", precioEntrada));
                            }
                        }
                    }
                }
            }

            // Notificar a Python cuando la posición vuelve a quedar FLAT para actualizar el dataset
            if (Position.MarketPosition == MarketPosition.Flat && precioEntrada > 0)
            {
                try
                {
                    double pnlPuntos = (execution.Order != null && execution.Order.OrderAction == OrderAction.Sell) ? (price - precioEntrada) : (precioEntrada - price);
                    string eventJson = string.Format(CultureInfo.InvariantCulture,
                        "{{\"event\":\"TRADE_CLOSED\",\"instrument\":\"{0}\",\"pnl_points\":{1:F2}}}\n",
                        Instrument.FullName, pnlPuntos);
                    byte[] data = Encoding.UTF8.GetBytes(eventJson);
                    stream.Write(data, 0, data.Length);
                }
                catch { }

                precioEntrada = 0.0;
                tp1Alcanzado = false;
                protegidoBE = false;
            }
        }

        private void CerrarTodaLaPosicion()
        {
            if (Position.MarketPosition == MarketPosition.Long)
            {
                ExitLong(ORDEN_L1);
                ExitLong(ORDEN_L2);
                ExitLong(ORDEN_L3);
            }
            else if (Position.MarketPosition == MarketPosition.Short)
            {
                ExitShort(ORDEN_S1);
                ExitShort(ORDEN_S2);
                ExitShort(ORDEN_S3);
            }
        }

        private void DisconnectBot()
        {
            isConnected = false;
            if (stream != null) { stream.Close(); stream = null; }
            if (socketClient != null) { socketClient.Close(); socketClient = null; }
            Print("LocalBotConnector: Desconectado de forma limpia.");
        }
    }
}

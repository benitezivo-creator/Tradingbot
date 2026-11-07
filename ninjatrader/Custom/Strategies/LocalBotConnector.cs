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
        private TcpClient socketClient;
        private NetworkStream stream;
        private string serverIp = "127.0.0.1";
        private int serverPort = 5555;
        private bool isConnected = false;
        private DateTime lastConnectAttempt = DateTime.MinValue;

        #region Properties
        [NinjaScriptProperty]
        [Display(Name = "Solo Ticks en Tiempo Real", Description = "Ignora ticks históricos al cargar el gráfico.", Order = 1, GroupName = "1. Conexión")]
        public bool OnlyRealtime { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "Cantidad de Contratos", Description = "Número de contratos por orden.", Order = 2, GroupName = "2. Gestión de Órdenes")]
        public int OrderQuantity { get; set; }

        [NinjaScriptProperty]
        [Range(0, 1000)]
        [Display(Name = "Stop Loss (Ticks)", Description = "Stop Loss automático en ticks (0 para desactivar).", Order = 3, GroupName = "2. Gestión de Órdenes")]
        public int StopLossTicks { get; set; }

        [NinjaScriptProperty]
        [Range(0, 2000)]
        [Display(Name = "Profit Target (Ticks)", Description = "Take Profit automático en ticks (0 para desactivar).", Order = 4, GroupName = "2. Gestión de Órdenes")]
        public int ProfitTargetTicks { get; set; }
        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description                             = "Conector local ultra-rápido por Socket TCP para Bot Python (NT 8.1.8.5 64-bit)";
                Name                                    = "LocalBotConnector";
                Calculate                               = Calculate.OnEachTick;
                IsInstantiatedOnEachOptimizationIteration = false;
                OnlyRealtime                            = true;
                OrderQuantity                           = 1;
                StopLossTicks                           = 40; // 10 puntos en NQ/MNQ
                ProfitTargetTicks                       = 80; // 20 puntos en NQ/MNQ
                EntriesPerDirection                     = 1;
                EntryHandling                           = EntryHandling.AllEntries;
            }
            else if (State == State.Configure)
            {
                if (StopLossTicks > 0)
                    SetStopLoss(CalculationMode.Ticks, StopLossTicks);

                if (ProfitTargetTicks > 0)
                    SetProfitTarget(CalculationMode.Ticks, ProfitTargetTicks);
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
                Print("LocalBotConnector: ¡Conectado exitosamente al Bot Python local en " + serverIp + ":" + serverPort + "!");
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
                string jsonPayload = string.Format(
                    CultureInfo.InvariantCulture,
                    "{{\"instrument\":\"{0}\",\"close\":{1:F2},\"volume\":{2:F0},\"time\":\"{3}\"}}\n",
                    Instrument.FullName,
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

            Print(string.Format("LocalBotConnector: Señal recibida -> [{0}] | Posición: {1} ({2})", 
                signal, Position.MarketPosition, Position.Quantity));

            if (signal.Contains("BUY"))
            {
                if (Position.MarketPosition == MarketPosition.Long && Position.Quantity >= EntriesPerDirection)
                {
                    Print(string.Format("LocalBotConnector: Señal BUY ignorada (ya existe posición Long de {0} contrato(s)).", Position.Quantity));
                    return;
                }

                if (Position.MarketPosition == MarketPosition.Short)
                    ExitShort("Bot_SELL");

                EnterLong(OrderQuantity, "Bot_BUY");
                Draw.ArrowUp(this, "BuyArrow_" + CurrentBar, true, 0, Low[0] - (TickSize * 4), Brushes.LimeGreen);
            }
            else if (signal.Contains("SELL"))
            {
                if (Position.MarketPosition == MarketPosition.Short && Position.Quantity >= EntriesPerDirection)
                {
                    Print(string.Format("LocalBotConnector: Señal SELL ignorada (ya existe posición Short de {0} contrato(s)).", Position.Quantity));
                    return;
                }

                if (Position.MarketPosition == MarketPosition.Long)
                    ExitLong("Bot_BUY");

                EnterShort(OrderQuantity, "Bot_SELL");
                Draw.ArrowDown(this, "SellArrow_" + CurrentBar, true, 0, High[0] + (TickSize * 4), Brushes.Crimson);
            }
            else if (signal.Contains("FLATTEN"))
            {
                if (Position.MarketPosition == MarketPosition.Long)
                    ExitLong("Bot_BUY");
                if (Position.MarketPosition == MarketPosition.Short)
                    ExitShort("Bot_SELL");
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

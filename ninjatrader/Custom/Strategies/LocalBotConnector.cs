#region Using declarations
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
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

        [NinjaScriptProperty]
        public bool OnlyRealtime
        { get; set; }

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description                             = "Conector local ultra-rápido por Socket TCP para Bot Python (NT 8.1.8.5 64-bit)";
                Name                                    = "LocalBotConnector";
                Calculate                               = Calculate.OnEachTick;
                IsInstantiatedOnEachOptimizationIteration = false;
                OnlyRealtime                            = true;
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
            // Si está cargando datos históricos y OnlyRealtime está activo, no enviar ticks pasados
            if (OnlyRealtime && State != State.Realtime) return;
            if (CurrentBar < 1) return;

            // Reintento automático de conexión si el servidor Python no estaba iniciado al arrancar NinjaTrader
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
                // InvariantCulture asegura que el punto (.) sea el separador decimal en cualquier configuración regional
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
                        // TriggerCustomEvent traslada la ejecución al hilo de la estrategia de NinjaTrader de forma segura
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

            Print(string.Format("LocalBotConnector: Señal ejecutada -> [{0}] a las {1}", signal, Time[0]));

            if (signal.Contains("BUY"))
            {
                if (Position.MarketPosition == MarketPosition.Short)
                    ExitShort("Bot_SELL");
                EnterLong(1, "Bot_BUY");
            }
            else if (signal.Contains("SELL"))
            {
                if (Position.MarketPosition == MarketPosition.Long)
                    ExitLong("Bot_BUY");
                EnterShort(1, "Bot_SELL");
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

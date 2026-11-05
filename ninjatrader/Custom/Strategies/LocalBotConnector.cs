#region Using declarations
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
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

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description                             = "Conector local ultra-rápido por Socket TCP para Bot Python (NT 8.1.8.5 64-bit)";
                Name                                    = "LocalBotConnector";
                Calculate                               = Calculate.OnEachTick;
                IsInstantiatedOnEachOptimizationIteration = false;
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
            try
            {
                socketClient = new TcpClient(serverIp, serverPort);
                stream = socketClient.GetStream();
                isConnected = true;
                Print("LocalBotConnector: Conectado exitosamente al Bot Python local en " + serverIp + ":" + serverPort);
                Task.Run(() => ListenToBotResponses());
            }
            catch (Exception ex)
            {
                Print("LocalBotConnector Error de Conexión: " + ex.Message);
                isConnected = false;
            }
        }

        protected override void OnBarUpdate()
        {
            if (!isConnected || CurrentBar < 1) return;

            try
            {
                string jsonPayload = string.Format(
                    "{{\"instrument\":\"{0}\",\"close\":{1},\"volume\":{2},\"time\":\"{3}\"}}\n",
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
                    ProcessSignal(response);
                }
                catch
                {
                    break;
                }
            }
        }

        private void ProcessSignal(string signal)
        {
            if (signal.Contains("BUY"))
            {
                EnterLong(1, "Bot_BUY");
            }
            else if (signal.Contains("SELL"))
            {
                EnterShort(1, "Bot_SELL");
            }
            else if (signal.Contains("FLATTEN"))
            {
                ExitLong("Bot_BUY");
                ExitShort("Bot_SELL");
            }
        }

        private void DisconnectBot()
        {
            isConnected = false;
            if (stream != null) stream.Close();
            if (socketClient != null) socketClient.Close();
            Print("LocalBotConnector: Desconectado de forma limpia.");
        }
    }
}


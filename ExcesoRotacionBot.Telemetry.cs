using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using NinjaTrader.Cbi;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class ExcesoRotacionBot
    {
        private string DirectorioDataset
        {
            get { return !string.IsNullOrEmpty(RutaDatasetVision) ? RutaDatasetVision : @"C:\Users\SIBS\Documents\Tradingbot\vision_dataset"; }
        }

        private string ultimaSenalEjecutada = string.Empty;
        private string ultimaRutaImagenEjecutada = string.Empty;
        private double cumProfitAlEntrar = 0.0;
        private string ultimoSnapshotImagen = string.Empty;
        private int ultimoTradeNumberRegistrado = -1;

        #region Win32 GDI Screen Capture P/Invoke (Sin dependencias de System.Drawing)
        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hObjectSource, int nXSrc, int nYSrc, int dwRop);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        private const int SRCCOPY = 0x00CC0020;
        #endregion

        /// <summary>
        /// Captura la imagen del gráfico de NinjaTrader de forma directa (GDI BitBlt nativo) para evitar marcos negros por aceleración GPU DirectX.
        /// No requiere ninguna referencia externa a System.Drawing.dll.
        /// </summary>
        private void CapturarSnapshotGrafico(string rutaDestino, bool sincrono = true)
        {
            if (ChartControl == null) return;

            Action accionCaptura = () =>
            {
                IntPtr hdcScreen = IntPtr.Zero;
                IntPtr hdcMem = IntPtr.Zero;
                IntPtr hBitmap = IntPtr.Zero;
                IntPtr hOld = IntPtr.Zero;

                try
                {
                    // Obtener las coordenadas físicas exactas del control del gráfico en la pantalla (DPI-Aware)
                    System.Windows.Point upperLeft = ChartControl.PointToScreen(new System.Windows.Point(0, 0));
                    System.Windows.Point bottomRight = ChartControl.PointToScreen(new System.Windows.Point(ChartControl.ActualWidth, ChartControl.ActualHeight));

                    int width = (int)Math.Max(1, bottomRight.X - upperLeft.X);
                    int height = (int)Math.Max(1, bottomRight.Y - upperLeft.Y);

                    if (width <= 10 || height <= 10) return;

                    // Crear directorio si no existe
                    string directorio = Path.GetDirectoryName(rutaDestino);
                    if (!string.IsNullOrEmpty(directorio) && !Directory.Exists(directorio))
                    {
                        Directory.CreateDirectory(directorio);
                    }

                    // Captura GDI directa del contexto de pantalla (lee la GPU/DirectX sin marcos negros)
                    hdcScreen = GetDC(IntPtr.Zero);
                    hdcMem = CreateCompatibleDC(hdcScreen);
                    hBitmap = CreateCompatibleBitmap(hdcScreen, width, height);
                    hOld = SelectObject(hdcMem, hBitmap);

                    BitBlt(hdcMem, 0, 0, width, height, hdcScreen, (int)upperLeft.X, (int)upperLeft.Y, SRCCOPY);

                    // Convertir hBitmap a BitmapSource nativo de WPF
                    BitmapSource bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions()
                    );

                    // Guardar como PNG usando el codificador nativo WPF
                    PngBitmapEncoder pngEncoder = new PngBitmapEncoder();
                    pngEncoder.Frames.Add(BitmapFrame.Create(bitmapSource));

                    using (FileStream stream = new FileStream(rutaDestino, FileMode.Create, FileAccess.Write))
                    {
                        pngEncoder.Save(stream);
                    }

                    Print(string.Format("[TELEMETRIA] Captura HD generada en: {0}", rutaDestino));
                }
                catch (Exception ex)
                {
                    Print(string.Format("[TELEMETRIA ERROR] Fallo al capturar gráfico: {0}", ex.Message));
                }
                finally
                {
                    if (hdcMem != IntPtr.Zero && hOld != IntPtr.Zero)
                        SelectObject(hdcMem, hOld);
                    if (hBitmap != IntPtr.Zero)
                        DeleteObject(hBitmap);
                    if (hdcMem != IntPtr.Zero)
                        DeleteDC(hdcMem);
                    if (hdcScreen != IntPtr.Zero)
                        ReleaseDC(IntPtr.Zero, hdcScreen);
                }
            };

            if (sincrono)
            {
                if (ChartControl.Dispatcher.CheckAccess())
                    accionCaptura();
                else
                    ChartControl.Dispatcher.Invoke(accionCaptura);
            }
            else
            {
                ChartControl.Dispatcher.InvokeAsync(accionCaptura);
            }
        }

        /// <summary>
        /// Etiqueta y guarda la muestra asociando el tipo de señal y su resultado final.
        /// </summary>
        private void RegistrarMuestraConResultado(string signalType, double pnlRealizado, string rutaImagen)
        {
            try
            {
                string estadoResultado = pnlRealizado > 0 ? "WIN" : "LOSS";
                string etiquetaFinal = string.Format("{0}_{1}", estadoResultado, signalType);

                string jsonEntry = string.Format(
                    "{{\"timestamp\":\"{0}\",\"signal\":\"{1}\",\"pnl\":{2},\"image_path\":\"{3}\"}},",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    etiquetaFinal,
                    pnlRealizado.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    rutaImagen.Replace("\\", "\\\\")
                );

                string rutaJson = Path.Combine(DirectorioDataset, "labels.json");
                File.AppendAllText(rutaJson, jsonEntry + Environment.NewLine);
                
                Print(string.Format("[DATASET] Muestra registrada: {0} | PnL: {1}", etiquetaFinal, pnlRealizado));
            }
            catch (Exception ex)
            {
                Print(string.Format("[DATASET ERROR] No se pudo guardar etiqueta: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Evento que detecta tanto trades automáticos como ejecuciones manuales (Chart Trader / DOM).
        /// </summary>
        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            base.OnExecutionUpdate(execution, executionId, price, quantity, marketPosition, orderId, time);

            if (execution == null || execution.Order == null)
                return;

            try
            {
                // Registro de llenados de órdenes
                RegistrarLog(string.Format("EJECUCIÓN: {0} {1}x @ {2:F2} | Orden: {3} | Posición Actual: {4}",
                    execution.Order.Name, quantity, price, orderId, marketPosition));

                // 1. Capturar el instante exacto en que se ejecuta una entrada (Manual o Automática)
                if (Position.MarketPosition != MarketPosition.Flat)
                {
                    if (precioEntradaPromedio <= 0.0)
                        precioEntradaPromedio = Position.AveragePrice;

                    // Detectar si la orden es de entrada
                    bool esEntrada = execution.Order.OrderAction == OrderAction.Buy
                                  || execution.Order.OrderAction == OrderAction.SellShort
                                  || (execution.Order.Name != null && (execution.Order.Name.Contains("Entrada") || execution.Order.Name.Contains("Entry")));

                    if (esEntrada)
                    {
                        bool esBot = (execution.Order.Name != null && (execution.Order.Name.Contains("Entrada") || execution.Order.Name.Contains("Entry") || execution.Order.Name.StartsWith("ORDEN_")))
                                  || (!string.IsNullOrEmpty(execution.Order.FromEntrySignal) && !execution.Order.FromEntrySignal.Equals("Order", StringComparison.OrdinalIgnoreCase));
                        string origen = esBot ? "BOT" : "MANUAL";
                        string tipoOrden = Position.MarketPosition == MarketPosition.Long ? "Compra" : "Venta";

                        string nombreImagen = string.Format("{0}_{1}_{2}.png", origen, tipoOrden, time.ToString("yyyyMMdd_HHmmss"));
                        string rutaImagen = Path.Combine(DirectorioDataset, "images", nombreImagen);

                        if (nombreImagen != ultimoSnapshotImagen)
                        {
                            ultimoSnapshotImagen = nombreImagen;
                            CapturarSnapshotGrafico(rutaImagen);
                        }
                    }
                }
                else
                {
                    // 2. Al cerrarse el trade (Flat), calcular el PnL y registrar la muestra final en labels.json
                    if (estadoActual == EstadoBot.EN_POSICION || estadoActual == EstadoBot.PROTEGIDO || estadoActual == EstadoBot.RUNNER)
                    {
                        RegistrarLog("POSICIÓN TOTALMENTE CERRADA -> Volviendo a IDLE.");
                        barraUltimoCierre = CurrentBars[0];
                        ReiniciarMaquinaEstados();
                    }

                    if (SystemPerformance != null && SystemPerformance.AllTrades != null && SystemPerformance.AllTrades.Count > 0)
                    {
                        Trade ultimoTrade = SystemPerformance.AllTrades[SystemPerformance.AllTrades.Count - 1];

                        if (ultimoTrade != null && ultimoTrade.TradeNumber != ultimoTradeNumberRegistrado && execution.Order.OrderState == OrderState.Filled)
                        {
                            ultimoTradeNumberRegistrado = ultimoTrade.TradeNumber;

                            double pnlTrade = ultimoTrade.ProfitCurrency;
                            string tipoOrden = (ultimoTrade.Entry != null && ultimoTrade.Entry.MarketPosition == MarketPosition.Long) ? "Compra" : "Venta";

                            bool esBotTrade = (ultimoTrade.Entry != null && ultimoTrade.Entry.Name != null
                                && (ultimoTrade.Entry.Name.Contains("Entrada") || ultimoTrade.Entry.Name.Contains("Entry") || ultimoTrade.Entry.Name.StartsWith("ORDEN_")));
                            string origen = esBotTrade ? "BOT" : "MANUAL";

                            string identificadorSeñal = string.Format("{0}_{1}", origen, tipoOrden);
                            DateTime tiempoEntrada = (ultimoTrade.Entry != null) ? ultimoTrade.Entry.Time : time;
                            string nombreImagen = string.Format("{0}_{1}_{2}.png", origen, tipoOrden, tiempoEntrada.ToString("yyyyMMdd_HHmmss"));
                            string rutaImagen = Path.Combine(DirectorioDataset, "images", nombreImagen);

                            RegistrarMuestraConResultado(identificadorSeñal, pnlTrade, rutaImagen);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Print(string.Format("[TELEMETRIA DISCRECIONAL ERROR] {0}", ex.Message));
            }
        }
    }
}

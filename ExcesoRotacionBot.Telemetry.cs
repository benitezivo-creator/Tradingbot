using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

        /// <summary>
        /// Captura el estado visual exacto del gráfico desde el UI Thread utilizando RenderTargetBitmap.
        /// </summary>
        private void CapturarSnapshotGrafico(string rutaDestino)
        {
            if (ChartControl == null) return;

            ChartControl.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    int width = (int)ChartControl.ActualWidth;
                    int height = (int)ChartControl.ActualHeight;

                    if (width <= 0 || height <= 0) return;

                    RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(
                        width, height, 96, 96, PixelFormats.Pbgra32);

                    renderTargetBitmap.Render(ChartControl);

                    PngBitmapEncoder pngEncoder = new PngBitmapEncoder();
                    pngEncoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));

                    string directorio = Path.GetDirectoryName(rutaDestino);
                    if (!string.IsNullOrEmpty(directorio) && !Directory.Exists(directorio))
                    {
                        Directory.CreateDirectory(directorio);
                    }

                    using (FileStream stream = new FileStream(rutaDestino, FileMode.Create, FileAccess.Write))
                    {
                        pngEncoder.Save(stream);
                    }

                    Print(string.Format("[TELEMETRIA] Captura guardada en: {0}", rutaDestino));
                }
                catch (Exception ex)
                {
                    Print(string.Format("[TELEMETRIA ERROR] Fallo al generar captura: {0}", ex.Message));
                }
            });
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
                string rutaRelativa = "images/" + Path.GetFileName(rutaImagen);

                string jsonEntry = string.Format(
                    "{{\"timestamp\":\"{0}\",\"signal\":\"{1}\",\"pnl\":{2},\"image_path\":\"{3}\"}},",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    etiquetaFinal,
                    pnlRealizado.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    rutaRelativa.Replace("\\", "/")
                );

                string rutaJson = Path.Combine(DirectorioDataset, "labels.json");
                File.AppendAllText(rutaJson, jsonEntry + Environment.NewLine);
                
                Print(string.Format("[DATASET] Muestra registrada: {0} | PnL: {1:F2}", etiquetaFinal, pnlRealizado));
            }
            catch (Exception ex)
            {
                Print(string.Format("[DATASET ERROR] No se pudo guardar etiqueta: {0}", ex.Message));
            }
        }
    }
}

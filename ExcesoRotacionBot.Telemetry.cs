using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NinjaTrader.Gui.Chart;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class ExcesoRotacionBot
    {
        /// <summary>
        /// Captura el estado visual exacto del gráfico desde el UI Thread utilizando RenderTargetBitmap (WPF/NT8 nativo).
        /// Resuelve el problema de imágenes en negro o archivos corruptos de 5KB.
        /// </summary>
        private void CapturarSnapshotGrafico(string rutaDestino)
        {
            if (ChartControl == null) return;

            // Invocar de forma asíncrona en el hilo de interfaz de usuario de WPF
            ChartControl.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    int width = (int)ChartControl.ActualWidth;
                    int height = (int)ChartControl.ActualHeight;

                    if (width <= 0 || height <= 0)
                    {
                        Print("[TELEMETRIA] Error: Dimensiones del gráfico no válidas para la captura.");
                        return;
                    }

                    // Renderizar la superficie visual actual del ChartControl
                    RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(
                        width, 
                        height, 
                        96, 
                        96, 
                        PixelFormats.Pbgra32);

                    renderTargetBitmap.Render(ChartControl);

                    // Codificar a formato PNG
                    PngBitmapEncoder pngEncoder = new PngBitmapEncoder();
                    pngEncoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));

                    // Asegurar la creación del directorio destino si no existe
                    string directorio = Path.GetDirectoryName(rutaDestino);
                    if (!string.IsNullOrEmpty(directorio) && !Directory.Exists(directorio))
                    {
                        Directory.CreateDirectory(directorio);
                    }

                    // Guardar en disco en modo sobrescritura/creación limpia
                    using (FileStream stream = new FileStream(rutaDestino, FileMode.Create, FileAccess.Write))
                    {
                        pngEncoder.Save(stream);
                    }

                    Print(string.Format("[TELEMETRIA] Captura HD guardada correctamente en: {0}", rutaDestino));
                }
                catch (Exception ex)
                {
                    Print(string.Format("[TELEMETRIA ERROR] Fallo al generar captura visual: {0}", ex.Message));
                }
            });
        }
    }
}

#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class ExcesoRotacionBot
    {
        // =========================================================================
        // VISION AI INFERENCE GATE (RESNET-18 EN TIEMPO REAL)
        // =========================================================================

        private static readonly HttpClient httpClientVision = new HttpClient { Timeout = TimeSpan.FromMilliseconds(800) };

        /// <summary>
        /// Consulta al microservicio Python si la imagen capturada cumple con el filtro de IA (P_WIN >= Umbral).
        /// </summary>
        private bool ValidarEntradaConVisionAI(string rutaImagen)
        {
            if (!ActivarFiltroVisionAI)
                return true;

            if (string.IsNullOrEmpty(rutaImagen) || !File.Exists(rutaImagen))
            {
                RegistrarLog(string.Format("[VISION AI WARN] Archivo de imagen no encontrado para inferencia: {0}. Permitiendo entrada por defecto.", rutaImagen));
                return true;
            }

            try
            {
                string rutaEscapada = rutaImagen.Replace("\\", "\\\\");
                string url = !string.IsNullOrEmpty(UrlServidorInferencia) ? UrlServidorInferencia : "http://127.0.0.1:5005/predict";
                double umbral = UmbralProbabilidadVisionAI > 0.0 ? UmbralProbabilidadVisionAI : 0.70;

                string jsonPayload = string.Format(CultureInfo.InvariantCulture,
                    "{{\"image_path\":\"{0}\",\"umbral\":{1:F2}}}",
                    rutaEscapada,
                    umbral
                );

                StringContent content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Enviar petición POST sincrónica rápida al servidor local de inferencia
                HttpResponseMessage response = httpClientVision.PostAsync(url, content).Result;

                if (response.IsSuccessStatusCode)
                {
                    string jsonResult = response.Content.ReadAsStringAsync().Result;

                    // Parsing básico y robusto de respuesta JSON
                    bool aprobado = jsonResult.Contains("\"aprobado\":true");

                    // Extraer probabilidad para logging detallado
                    string probWinStr = "N/A";
                    int idxProb = jsonResult.IndexOf("\"prob_win\":");
                    if (idxProb >= 0)
                    {
                        int idxComa = jsonResult.IndexOfAny(new char[] { ',', '}' }, idxProb + 11);
                        if (idxComa > idxProb)
                        {
                            probWinStr = jsonResult.Substring(idxProb + 11, idxComa - (idxProb + 11)).Trim();
                        }
                    }

                    RegistrarLog(string.Format("[VISION AI GATE] {0} | P(WIN): {1} (Mín: {2:P0}) | Snapshot: {3}",
                        aprobado ? "APROBADO" : "RECHAZADO",
                        probWinStr,
                        umbral,
                        Path.GetFileName(rutaImagen)));

                    return aprobado;
                }
                else
                {
                    RegistrarLog(string.Format("[VISION AI WARN] Servidor HTTP status {0}. Permitiendo entrada por bypass.", response.StatusCode));
                    return true;
                }
            }
            catch (Exception ex)
            {
                RegistrarLog(string.Format("[VISION AI ERROR] Servidor de inferencia no disponible ({0}). Permitiendo entrada por bypass de seguridad.", ex.Message));
                return true; // Bypass de seguridad si el servidor Python no está activo
            }
        }
    }
}

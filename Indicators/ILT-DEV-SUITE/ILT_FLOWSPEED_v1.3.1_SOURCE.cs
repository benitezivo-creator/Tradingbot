#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

// ═══════════════════════════════════════════════════════════════════════════
//  ILT FLOWSPEED · ILT SUITE — Iron Lion Traders
//  Motor de anomalías de velocidad y aceleración de order flow (NinjaTrader 8)
//
//  VERSIÓN 1.3.3 — AUTOESCALA VISIBLE + PANEL FLOTANTE · 2026-09-08
// ═══════════════════════════════════════════════════════════════════════════
//
//  ARQUITECTURA
//  · Serie interna de 1 tick (BIP 1) alimenta el motor; BIP 0 consolida plots,
//    señales y dibujos → resultado idéntico en cualquier equipo, feed u orden
//    interno de despacho.
//  · Ventana rodante de velocidad (ask/bid por segundo) + aceleración por vela.
//  · Baseline causal ROBUSTO por mediana + MAD (Z estable en tape rápido o lento).
//  · Score compuesto 0..100 con signo (M magnitud · D dirección · A aceleración).
//  · Umbral adaptativo intradía por percentil de la sesión ILT (18:00 → 17:00),
//    con zona muerta 17:00-18:00 excluida del baseline.
//  · Modelo M + E: la eficiencia E (el precio sigue o rechaza al flujo) es el
//    único juez del lado-precio. Los excesos quedan PENDIENTES (EXB/EXS) y el
//    clasificador impulso→respuesta los resuelve como CONTINUACIÓN (ACB/ACS)
//    o ROTACIÓN (RTB/RTS).
//  · Histograma al ancho de vela con efecto glass 3D, LOD automático anti-lag,
//    fallback nativo garantizado y ciclo RenderTarget seguro (SharpDX).
//  · Histórico determinista: todos los ticks alimentan el motor; los visuales
//    se reconstruyen tras F5, cambio de temporalidad o paso a tiempo real.
//
//  NOTAS DE ESTA VERSIÓN (auditoría de lógica + limpieza para el release)
//  · El Flow Speed conserva las barras de sesiones anteriores para que el
//    histograma histórico completo permanezca visible.
//  · Menú "ILT FLOWSPEED ▼" en la barra superior: modo operativo, visibilidad
//    de panel/flechas/etiquetas y pintado por familia sin detener los cálculos.
//  1. Histéresis del gate de anomalía: el re-arme ahora se evalúa en CADA barra
//     cerrada. Antes solo se comprobaba en barras candidatas a señal, por lo que
//     tras un disparo el gate podía quedar desarmado (sin señales) por tramos
//     largos de la sesión.
//  2. Las confirmaciones diferidas (rotación/continuación de un exceso
//     pendiente) ya no pueden ser bloqueadas por el debounce ni por la
//     supresión de repetidos: su validez proviene del modelo impulso→respuesta
//     y su pendiente se consume al resolverse. Bloquearlas dejaba marcadores
//     EXB/EXS sin resolución visible.
//  3. Los presets del modo operativo ya no sobreescriben propiedades visibles
//     del usuario (filtro de anomalía y sensibilidad base); solo ajustan la
//     capa oculta del motor. Antes cada recarga pisaba esos dos controles y
//     los dejaba sin efecto real.
//  4. ProcessTrade: la detección de gaps/sesión y el cierre determinista de
//     barra se registran ANTES del filtro de volumen mínimo, y el tick-rule
//     sigue el tape aunque el print quede filtrado.
//  5. Eliminado código sin consumidores: contadores de diagnóstico, subsistema
//     legacy de retención de DrawObjects (todas las señales confirmadas son
//     persistentes) y la propiedad legada RotationReversalPoints.
//  6. Región de parámetros reorganizada: numeración limpia por grupo, nombres
//     en español y propiedades internas del motor agrupadas y ocultas.
//  7. (1.0.1) Arbitraje impulso→respuesta: una agresión contraria a un exceso
//     pendiente ya no imprime ACB/ACS ni pisa el pendiente; el clasificador la
//     reconoce como respuesta agresiva y confirma ROTACIÓN (RTB/RTS) con media
//     distancia adversa. Corrige giros en V etiquetados como continuación.
//  8. (1.0.2) BASELINE DE SESIÓN COMPLETA: la mediana+MAD de los Z-scores y el
//     percentil adaptativo se calculan sobre TODAS las barras válidas de la
//     sesión ILT en curso (18:00→17:00), no sobre una ventana rodante de N
//     barras. Dos equipos que abren el gráfico a distinta hora convergen al
//     mismo baseline y, por tanto, a las mismas señales — siempre que ambos
//     carguen la sesión completa (Tick Replay + días suficientes).
//  9. (1.0.2) Veredicto anclado a la GEOMETRÍA DEL EVENTO: continuación =
//     cierre más allá del extremo del exceso; rotación estándar = cierre de
//     vuelta más allá del punto medio del rango del evento; rotación fuerte =
//     cierre más allá del extremo opuesto (confirma sola). La escala adaptativa
//     queda como piso anti-ruido, no como frontera rectora. Elimina el sesgo
//     3:1 que ahogaba las rotaciones en gráficos Range.
// 10. (1.0.2) Pendiente INVIOLABLE con veredicto obligatorio: un impulso del
//     mismo lado fusiona extremos sin re-anclar el evento; el lado contrario no
//     puede pisarlo; y si la ventana expira sin veredicto, el marcador EXB/EXS
//     se atenúa. Cada exceso del gráfico queda auditado.
// 11. (1.0.2) Modo diagnóstico (DebugMode): sello de versión, estado del
//     baseline de sesión y traza completa del ciclo impulso→respuesta en la
//     ventana Output, para calibrar umbrales con datos reales.
// 12. (1.0.3) Un evento = una etiqueta. Mientras un EXB/EXS espera veredicto,
//     ninguna señal fresca de aceleración se imprime (tampoco del mismo lado):
//     la respuesta la nombra siempre el resolvedor. Era la causa de marcas
//     duplicadas en velas contiguas (EXS seguido de ACS por la vía fresca).
// 13. (1.0.3) Escalonado anti-colisión de etiquetas: dos marcadores legítimos
//     en velas contiguas del mismo lado ya no superponen su texto.
// 14. (1.0.4) Política de clasificación del agresor documentada y explícita:
//     los prints al mismo precio (zero tick) heredan el lado del agresor
//     anterior. En ES/NQ, con un tick de horquilla, prints consecutivos al
//     mismo precio golpean la misma cola pasiva y son del mismo lado; es el
//     caso de absorción en un extremo. Se eliminó SplitUnclassifiedTrades,
//     que prometía un reparto 50/50 inalcanzable en la práctica.
// 15. (1.0.4) Diagnóstico de clasificación por sesión: porcentaje de prints
//     zero tick y grado de acuerdo entre la regla de cotización y el tick rule
//     sobre los prints donde ambas se pronuncian. Con esos dos números se
//     decide si conviene igualar histórico y tiempo real.
// 16. (1.0.4) "Regla de cotización en tiempo real" pasa a ser visible: es la
//     palanca de simetría. OFF fuerza tick rule en ambos estados y hace el
//     resultado reproducible entre equipos y entre vivo e histórico.
// 17. (1.0.5) La expiración atenuada solo afecta a excesos reales. El "watch de
//     reversa" que nace de una ACEL apunta a la barra que ya muestra ACB/ACS;
//     atenuarla pisaba esa etiqueta y dibujaba un EXB fantasma encima.
// 18. (1.0.5) La fusión extiende los extremos pero NO la geometría de decisión:
//     los umbrales se miden contra el rango del evento original (techo +25%).
//     Antes, en tendencia el evento crecía de 40 a 300 ticks y exigía retrocesos
//     imposibles, de modo que el pendiente sólo podía expirar.
// 19. (1.0.5) Continuación exige 0.75 del rango del evento (antes 0.25). En
//     Range 40 la vela siguiente recorre 40 ticks por construcción, así que el
//     umbral anterior lo cumplía cualquier vela que no girara.
// 20. (1.0.5) El percentil de sesión se siembra con la cola de la sesión previa
//     y se diluye una a una. Evita que el nivel efectivo caiga al piso durante
//     las primeras barras, que son las más activas del día.
// 21. (1.0.5) Un pendiente vivo se cierra en el corte de sesión en vez de
//     quedar colgado a través de la pausa diaria.
// 22. (1.0.6) MODELO UNIFICADO DE ANTICIPACIÓN → VEREDICTO. Dos eventos abren
//     un pendiente y ninguno dicta nada por sí solo:
//        EXB/EXS  exceso   — agresión anómala EN un extremo (ambigua por
//                            naturaleza; no se exige que el precio siga).
//        MB /MS   momentum — agresión anómala FUERA del extremo, con el precio
//                            siguiendo al flujo (eficiencia).
//     Ambos resuelven en CB/CS (continuación), RB/RS (rotación) o marcador
//     atenuado (expiró). Son mutuamente excluyentes: el exceso tiene prioridad.
// 23. (1.0.6) El momentum ya puede confirmar CONTINUACIÓN. Antes su watch sólo
//     admitía rotación y descartaba en silencio recorridos favorables de 100+
//     ticks: la información más valiosa del evento se perdía.
// 24. (1.0.6) Etiquetas renombradas: ACB/ACS → CB/CS y RTB/RTS → RB/RS, para
//     que anticipación y veredicto se distingan de un vistazo.
// 25. (1.0.6) Diagnóstico [EVENTOS]: tasas de continuación/rotación/expiración
//     separadas para EXCESO y MOMENTUM. Si resultan indistinguibles, la
//     distinción no aporta y procede fusionar ambos eventos.
// 26. (1.0.6) La siembra del percentil muestrea la sesión anterior de forma
//     uniforme en vez de tomar su cola (el cierre, el tramo más tranquilo).
// 27. (1.0.6) MB/MS se dibujan en la dirección del flujo: compra con flecha
//     arriba y etiqueta debajo de la vela, venta al revés. Convención opuesta
//     a EXB/EXS, cuya flecha señala el agotamiento en el extremo.
// ── Calibración 1.0.7, medida sobre 834 veredictos reales (3 sesiones NQ) ──
// 28. Continuación 0.75 → 1.20 (1.50 en la 1.0.7) y rotación 0.50/1.00 → 1.00/2.00 del rango del
//     evento. Con los valores anteriores el umbral no filtraba: el 10% más
//     marginal aún lo superaba por un 8%, y el veredicto se había degradado a
//     "¿la vela siguiente cerró para el mismo lado?". Ambos escalan igual para
//     que continuación y rotación exijan una prueba comparable.
// 29. Persistencia de la aceptación: el 75-82% de los veredictos se dictaban en
//     la vela inmediata. Ahora la primera vela de respuesta sólo resuelve si el
//     desenlace es decisivo; lo marginal espera a que el mercado lo sostenga.
//     Conmutable desde "Exigir aceptación sostenida".
// 30. Ventana de confirmación por defecto 12 → 6 velas (la expiración medida
//     era del 2-3%: sobraba margen para exigir persistencia).
// 31. Corregido: la telemetría [PENDIENTE] imprimía siempre "EXCESO" porque el
//     tipo se asignaba después de la llamada. Ahora es parámetro de creación.
//     Los contadores [EVENTOS] no estaban afectados.
// 33. (1.0.8) Nomenclatura del panel alineada al modelo: ACEL → CONTINUACIÓN,
//     EXC → EXCESO, ROT → ROTACIÓN, con el acrónimo entre paréntesis y el orden
//     siguiendo el ciclo (anticipación EXB/EXS/MB/MS → veredicto CB/CS/RB/RS).
//     Sólo cambian los nombres visibles; las propiedades conservan su nombre
//     interno, así que ninguna plantilla guardada se rompe.
// 32. (1.0.8) EMPUJE DE VELA ÚNICA. En un gráfico Range la vela de respuesta
//     abarca un rango fijo, así que su cierre no puede alejarse del extremo del
//     evento más de ~1.0 rango: cualquier umbral ≥ 1.0 impedía la resolución
//     inmediata por geometría, no por criterio. Ahora la vela siguiente puede
//     confirmar continuación si demuestra lo que sí está a su alcance: recorrer
//     ~0.95 rangos más allá del extremo con cuerpo limpio, cierre pegado a su
//     extremo y flujo acompañando. Lo marginal sigue esperando.
// 34. (1.0.9) Empuje de vela única endurecido (0.85→0.95, alineación de precio
//     0.65→0.80, de flujo 0.15→0.25, score 0.70→0.78). Medido sobre 1052
//     veredictos: la vía absorbía el 55-62% de las continuaciones con margen
//     mediano 0.90, por debajo del umbral de distancia. Volvió a ser excepción.
// ═══════════════════════════════════════════════════════════════════════════

namespace NinjaTrader.NinjaScript.Indicators.ILT_SUITE
{
    public partial class ILT_FLOWSPEED : Indicator
    {

        // ================================================================
        //  === LICENCIA ILT_SUITE (integrada) — NO TOCAR ===
        //  Clave pública RSA compartida por todos los indicadores ILT SUITE.
        //  Valida Documentos\ILT_SUITE\licencia.lic (fingerprint + caducidad).
        // ================================================================
        private const string ILTPUB_ILTFLOWSPEED =
            "<RSAKeyValue><Modulus>6Xi/RSxNtqILBIrligimoZIBittn99yzL/b2FjKmEtnWUL/mCOObtBbvdskEhXx6ep44BthxahLWtwaFZQoSBjo3OrJO/pA267uTUAFZ0kfXEvggyBrwKfw8uMFs9i87yNUL5QiOTujfy7Z/WvYhGdIg+G9JSLZV9iOzXVwFXCz1786JMzrm4JXkgldP9zqQ4dVX0VO4XvQA8VhRh/t2lQ6bx89Oy5amS6NdNGGdV5+oLLDScwJ0xaVwBLvnqNk0KAbripykuwt2BfEQYB5YJv8PfPO641IXRsPJnrh/7wWLAZ9oL/A7VzB32H7KHNyEWoiW4JSit8KI0WoInC++qQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        private bool?  iltOk_ILTFLOWSPEED = null;
        private string iltFp_ILTFLOWSPEED = null;

        private string IltFingerprint_ILTFLOWSPEED()
        {
            if (iltFp_ILTFLOWSPEED == null) iltFp_ILTFLOWSPEED = IltCalcFp_ILTFLOWSPEED();
            return iltFp_ILTFLOWSPEED;
        }

        private bool IltEsValida_ILTFLOWSPEED()
        {
            if (iltOk_ILTFLOWSPEED.HasValue) return iltOk_ILTFLOWSPEED.Value;
            iltOk_ILTFLOWSPEED = IltValidar_ILTFLOWSPEED();
            return iltOk_ILTFLOWSPEED.Value;
        }


        private bool IltValidar_ILTFLOWSPEED()
        {
            try
            {
                string ruta = IltRuta_ILTFLOWSPEED();
                if (!System.IO.File.Exists(ruta))
                    return false;

                string contenido = System.IO.File.ReadAllText(ruta, System.Text.Encoding.UTF8);
                string cuerpo, firmaB64;
                if (!IltParse_ILTFLOWSPEED(contenido, out cuerpo, out firmaB64))
                    return false;
                if (!IltVerificar_ILTFLOWSPEED(cuerpo, firmaB64))
                    return false;

                string fpLic = IltCampo_ILTFLOWSPEED(cuerpo, "FINGERPRINT");
                string cadStr = IltCampo_ILTFLOWSPEED(cuerpo, "CADUCIDAD");
                if (!string.Equals(fpLic, IltFingerprint_ILTFLOWSPEED(), StringComparison.OrdinalIgnoreCase))
                    return false;

                DateTime cad;
                if (!DateTime.TryParse(cadStr, out cad))
                    return false;

                return DateTime.UtcNow.Date <= cad.Date;
            }
            catch
            {
                return false;
            }
        }

        private string IltRuta_ILTFLOWSPEED()
        {
            string[] rutas = IltRutas_ILTFLOWSPEED();
            foreach (string ruta in rutas)
                if (System.IO.File.Exists(ruta))
                    return ruta;

            return rutas.Length > 0 ? rutas[0] : string.Empty;
        }

        private string[] IltRutas_ILTFLOWSPEED()
        {
            var rutas = new System.Collections.Generic.List<string>();
            string docs = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrEmpty(docs))
                rutas.Add(System.IO.Path.Combine(docs, "ILT_SUITE", "licencia.lic"));
            string perfil = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(perfil))
            {
                rutas.Add(System.IO.Path.Combine(perfil, "OneDrive", "Documentos", "ILT_SUITE", "licencia.lic"));
                rutas.Add(System.IO.Path.Combine(perfil, "OneDrive", "Documents", "ILT_SUITE", "licencia.lic"));
                rutas.Add(System.IO.Path.Combine(perfil, "Documents", "ILT_SUITE", "licencia.lic"));
                rutas.Add(System.IO.Path.Combine(perfil, "Documentos", "ILT_SUITE", "licencia.lic"));
            }
            string od = System.Environment.GetEnvironmentVariable("OneDrive");
            if (!string.IsNullOrEmpty(od))
            {
                rutas.Add(System.IO.Path.Combine(od, "Documentos", "ILT_SUITE", "licencia.lic"));
                rutas.Add(System.IO.Path.Combine(od, "Documents", "ILT_SUITE", "licencia.lic"));
            }
            return rutas.ToArray();
        }

        private bool IltVerificar_ILTFLOWSPEED(string cuerpo, string firmaB64)
        {
            using (var rsa = new System.Security.Cryptography.RSACryptoServiceProvider())
            using (var sha = new System.Security.Cryptography.SHA256CryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(ILTPUB_ILTFLOWSPEED);
                byte[] datos = System.Text.Encoding.UTF8.GetBytes(cuerpo);
                byte[] firma = System.Convert.FromBase64String(firmaB64);
                return rsa.VerifyData(datos, sha, firma);
            }
        }

        private string IltCalcFp_ILTFLOWSPEED()
        {
            string maquina = System.Environment.MachineName;
            string usuario = System.Environment.UserName;
            string serieC  = IltSerieC_ILTFLOWSPEED();
            string crudo = maquina + "|" + usuario + "|" + serieC;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(crudo));
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < 6; i++) sb.Append(hash[i].ToString("X2"));
                return sb.ToString();
            }
        }

        private string IltSerieC_ILTFLOWSPEED()
        {
            try
            {
                uint serial = 0;
                var vn = new System.Text.StringBuilder(261);
                var fsn = new System.Text.StringBuilder(261);
                uint maxComp, flags;
                if (IltGetVol_ILTFLOWSPEED("C:\\", vn, vn.Capacity, ref serial, out maxComp, out flags, fsn, fsn.Capacity))
                    return serial.ToString("X8");
            }
            catch { }
            return "NOSERIE";
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", EntryPoint = "GetVolumeInformation", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern bool IltGetVol_ILTFLOWSPEED(
            string rootPathName,
            System.Text.StringBuilder volumeNameBuffer, int volumeNameSize,
            ref uint volumeSerialNumber,
            out uint maximumComponentLength,
            out uint fileSystemFlags,
            System.Text.StringBuilder fileSystemNameBuffer, int fileSystemNameSize);

        private bool IltParse_ILTFLOWSPEED(string contenido, out string cuerpo, out string firma)
        {
            cuerpo = ""; firma = "";
            const string mf = "===FIRMA===";
            const string mi = "===ILT_SUITE_LICENCIA===";
            int idx = contenido.IndexOf(mf, System.StringComparison.Ordinal);
            if (idx < 0) return false;
            int ini = contenido.IndexOf(mi, System.StringComparison.Ordinal);
            if (ini < 0) return false;
            ini += mi.Length;
            while (ini < contenido.Length && (contenido[ini] == '\r' || contenido[ini] == '\n')) ini++;
            cuerpo = contenido.Substring(ini, idx - ini);
            firma = contenido.Substring(idx + mf.Length).Trim();
            return firma.Length > 0;
        }

        private string IltCampo_ILTFLOWSPEED(string cuerpo, string campo)
        {
            foreach (string linea in cuerpo.Split('\n'))
            {
                string l = linea.Trim();
                if (l.StartsWith(campo + "=", System.StringComparison.Ordinal))
                    return l.Substring(campo.Length + 1).Trim();
            }
            return "";
        }
        // ================ FIN LICENCIA ILT_SUITE ================


        // struct (no clase): evita una asignacion al heap por CADA tick. En ES/NQ
        // son miles por segundo y esa basura provocaba pausas de GC. El calculo es
        // identico: el valor se escribe una vez antes de Enqueue y solo se lee al
        // Dequeue/Peek (nunca se muta de forma compartida).
        private struct FlowPrint
        {
            public DateTime Time;
            public double AskVolume;
            public double BidVolume;
        }

        private class BarFlowData
        {
            public double AskVolume;
            public double BidVolume;

            public double SumNetSpeed;
            public double SumTotalSpeed;
            public int Samples;

            public double NetSpeed;
            public double TotalSpeed;
            public double AbsNetSpeed;
            public double Imbalance;
            public double Acceleration;

            public double SpeedZ;
            public double NetSpeedZ;
            public double AbsNetSpeedZ;
            public double AccelerationZ;

            // Score compuesto 0..100 y su dirección firmada.
            public double AnomalyMagnitude;
            public double AnomalySigned;

            // Umbral causal fijado antes de incorporar esta barra al percentil.
            public double EffectiveAnomalyLevel;

            // Eficiencia suavizada: +1 sigue al flujo; -1 lo rechaza.
            public double Efficiency;

            // Valor cacheado para el render personalizado del histograma.
            public double VisualHistogramValue;

            public int SignalCode;

            // Código visual persistente de la barra. A diferencia de SignalCode, también
            // conserva el marcador anticipado EXB/EXS para poder reconstruir colores,
            // flechas y etiquetas después de F5, cambio de temporalidad o carga histórica.
            public int VisualSignalCode;
        }

        private const int NONE      = 0;
        private const int ACEL_BUY  = 1;
        private const int ACEL_SELL = -1;
        private const int EXC_BUY   = 2;    // Buy excess at highs: aggressive buyers fail to continue.
        private const int EXC_SELL  = -2;   // Sell excess at lows: aggressive sellers fail to continue.

        // Exceso que agoto su ventana de confirmacion SIN veredicto. Conserva el marcador
        // (el evento existio) pero atenuado, para que ningun EXB/EXS quede sin auditar.
        private const int EXC_BUY_EXPIRED  = 4;
        private const int EXC_SELL_EXPIRED = -4;

        // (1.0.6) MOMENTUM: agresion anomala FUERA de un extremo, con el precio siguiendo al
        // flujo. Es el segundo evento de anticipacion del modelo y, como el exceso, no dicta
        // nada por si solo: abre un pendiente y espera el veredicto del mercado.
        private const int MOM_BUY  = 5;
        private const int MOM_SELL = -5;
        private const int MOM_BUY_EXPIRED  = 6;
        private const int MOM_SELL_EXPIRED = -6;
        private const int ROT_BUY   = 3;
        private const int ROT_SELL  = -3;

        // Menú desplegable de acceso rápido en la barra superior del chart.
        private const string FLOW_SPEED_TOOLBAR_TAG = "ILT_FLOWSPEED_TOOLBAR_MENU";
        private const string FLOW_SPEED_PANEL_TAG = "ILT_FLOWSPEED_FLOATING_PANEL";
        private Chart flowSpeedToolbarChart;
        private NTMenuItem flowSpeedToolbarMenu;
        // Se conserva como contenedor interno de los comandos para mantener una sola
        // fuente de estado; el usuario interactúa mediante el panel flotante WPF.
        private ContextMenu flowSpeedToolbarPopup;
        private NTMenuItem toolbarShowFlowSpeedItem;
        private NTMenuItem toolbarShowArrowsItem;
        private NTMenuItem toolbarShowLabelsItem;
        private NTMenuItem toolbarPaintExcessItem;
        private NTMenuItem toolbarPaintMomentumItem;
        private NTMenuItem toolbarPaintContinuationItem;
        private NTMenuItem toolbarPaintRotationItem;
        private NTMenuItem toolbarModeActiveItem;
        private NTMenuItem toolbarModeBalancedItem;
        private NTMenuItem toolbarModeStrictItem;
        private ChartPanel flowSpeedPanelHost;
        private PropertyInfo flowSpeedPanelControlsProperty;
        private Grid flowSpeedPanelRoot;
        private StackPanel flowSpeedPanelBody;
        private ComboBox flowSpeedModeCombo;
        private Button flowSpeedPanelCollapseButton;
        private Button toolbarShowFlowSpeedButton;
        private Button toolbarShowArrowsButton;
        private Button toolbarShowLabelsButton;
        private Button toolbarPaintExcessButton;
        private Button toolbarPaintMomentumButton;
        private Button toolbarPaintContinuationButton;
        private Button toolbarPaintRotationButton;
        private bool flowSpeedPanelExpanded;
        private bool flowSpeedPanelBuildQueued;
        private bool flowSpeedPanelSyncing;
        private bool flowSpeedPanelDragging;
        private Point flowSpeedPanelDragStart;
        private Thickness flowSpeedPanelDragMargin;


        private Queue<FlowPrint> flowWindow;
        private Dictionary<int, BarFlowData> barData;

        // barData se escribe desde el hilo de datos y se consulta desde OnRender (UI).
        // Dictionary no admite lectura/escritura concurrente: el lock protege únicamente
        // su estructura; el render usa TryEnter + snapshot para no bloquear el hot path.
        private readonly object barDataSync = new object();
        private readonly Dictionary<int, double> histogramRenderSnapshot = new Dictionary<int, double>();
        private readonly Dictionary<int, double> histogramRenderScratch = new Dictionary<int, double>();

        private struct SignalVisualSnapshot
        {
            public int BarIndex;
            public int Code;
        }
        private readonly List<SignalVisualSnapshot> signalVisualRestoreScratch = new List<SignalVisualSnapshot>();
        private bool restoreHistoricalVisualsPending;
        private bool restoreHistoricalPlotsPending;

        // Orden determinista entre la serie primaria y la serie interna de 1 tick.
        // Algunas instalaciones procesan primero BIP 0 y otras BIP 1 cuando comparten
        // timestamp. Finalizamos la barra anterior al detectar el cambio desde BIP 1,
        // evitando que las señales dependan del orden interno del equipo/feed.
        private int lastTradePrimaryBarIndex = -1;
        private DateTime lastProcessedTradeTime = DateTime.MinValue;

        // BIP 1 procesa exclusivamente datos. Las escrituras de Plot, Draw y BarBrushes se
        // consolidan después desde BIP 0, donde NinjaTrader garantiza el contexto de la
        // serie primaria. Esto evita diferencias por velocidad de CPU/orden de despacho.
        private Queue<int> pendingCompletedBars;
        private HashSet<int> pendingCompletedBarSet;
        private readonly List<int> historicalPlotRestoreScratch = new List<int>();

        // Estado de compatibilidad gráfica. Un fallo de SharpDX deshabilita únicamente el
        // render ancho/3D de esta instancia; el plot nativo y todas las señales continúan.
        private bool customHistogramRenderDisabledForInstance;
        private int customHistogramRenderFailureCount;
        private const int CUSTOM_RENDER_FAILURE_LIMIT = 3;

        private double windowAskVolume;
        private double windowBidVolume;

        private double lastBid;
        private double lastAsk;
        private double lastTradePrice;
        private int lastAggressorSide;

        private int lastEvaluatedBar;
        private int lastSignalBar;
        private int lastSignalCode;

        // ── Estado del modelo de exceso en dos fases ──────────────────────────
        // Un exceso (agresion en un extremo) queda PENDIENTE hasta que el precio
        // confirma rotacion o continuacion dentro de la ventana. Un solo slot:
        // un exceso nuevo refresca el contexto vigente.
        private bool pendingExcessActive;
        private bool pendingExcessIsBuy;
        private double pendingExcessHigh;
        private double pendingExcessLow;
        private double pendingExcessEventRange;
        private int pendingExcessBarIndex;
        private int pendingExcessLastBar;
        private int pendingExcessBarsLeft;
        private bool pendingAllowContinuation;   // true=exceso (continuacion o rotacion); false=aceleracion (solo rotacion)

        // (1.0.5) ¿Este pendiente tiene un marcador EXB/EXS dibujado en su barra?
        // Solo lo tienen los excesos reales. El "watch de reversa" que se arma tras una
        // ACEL apunta a una barra que YA muestra ACB/ACS: atenuarla al expirar pisaba esa
        // etiqueta y dibujaba encima un EXB fantasma. Este flag separa ambos casos.
        private bool pendingExcessHasMarker;

        // (1.0.6) Tipo del evento que abrio el pendiente: exceso (en un extremo) o momentum
        // (fuera del extremo). Ambos resuelven igual, pero se etiquetan y se miden por separado.
        // (1.0.7) Se declara como parametro de StartPendingExcess: asignarlo DESPUES de la
        // llamada hacia que la telemetria imprimiera siempre "EXCESO".
        private bool pendingExcessIsMomentum;

        // (1.0.5) Rango del evento ORIGINAL, congelado al crearse el pendiente. La fusion
        // extiende los extremos para MEDIR desplazamiento, pero la geometria que define los
        // umbrales sigue anclada aqui: sin esto, en tendencia el evento crecia de 40 a 300
        // ticks y exigia retrocesos imposibles, de modo que el veredicto nunca llegaba.
        private double pendingExcessBaseRange;

        // Estado del clasificador adaptativo impulso-respuesta. La distancia necesaria
        // deja de ser un numero fijo de ticks: se normaliza por el rango del evento,
        // la mediana robusta del rango reciente y la fuerza de la anomalia causal.
        private double pendingExcessEventMagnitude;
        private double pendingExcessEventLevel;
        private double pendingResponseScaleTicks;
        private double pendingBestFavorableTicks;
        private int pendingContinuationEvidenceBars;
        private int pendingRotationEvidenceBars;
        private readonly List<double> responseRangeScratch = new List<double>();

        private int lastExcessMarkerBar;

        // Toda señal confirmada es estructural: sus DrawObjects usan tags deterministas
        // por (barra, código) y permanecen visibles durante toda la data cargada.
        // NinjaTrader libera los objetos no-globales al destruir la instancia.
        private SimpleFont signalFont;

        // Lightweight render/plot guards. These reduce redundant chart work without
        // changing the flow-speed calculation engine.
        private double lastSyncedGuideLevel = double.NaN;
        private double cumulationIntervalSeconds = 1.0;

        // ── Suite performance guards ──────────────────────────────────────
        // These are intentionally internal constants, not user-facing signal filters.
        // They do not alter the order-flow calculation engine; they only reduce
        // redundant plot invalidations and repeated baseline scans while the suite
        // is running with Heatmap, Footprint, Profiles, VWAP and Levels.
        private const int REALTIME_PLOT_REFRESH_MS = 80;     // ~12.5 FPS for panel plots during tick bursts
        private int lastRealtimePlotRefreshMs = -100000000;

        // Cached baseline statistics for Z-score calculations. For a given primary
        // bar, the SpeedLookback baseline uses only prior bars, so the baseline is
        // stable while the current bar receives many 1-tick updates.
        // Cache caliente del bar actual/previo: ProcessTrade y UpdateFlowMetrics
        // hacian 2 busquedas de diccionario POR TICK; con el cache son ~0 (solo se
        // consulta el diccionario cuando cambia la barra). Mismos datos, mismo calculo.
        private int hotCurIdx = -1;
        private int hotPrevIdx = int.MinValue;
        private BarFlowData hotCurData;
        private BarFlowData hotPrevData;

        private int zCacheBarIndex = -1;
        private int zCachePeriod = -1;
        private int zCacheScope = -1;        // 0 = rodante (N barras) ; 1 = sesion completa
        private int zCacheCount = 0;
        // Baseline ROBUSTO: mediana + MAD (escala) en vez de media + desviacion.
        // La media/desviacion sobre una variable positiva y con rafagas se contamina a
        // si misma (la rafaga infla la base y suprime la siguiente). La mediana no se
        // mueve con 2-3 outliers, asi que el Z significa lo mismo en tape rapido y lento.
        private double zCacheMedTotal, zCacheMedNet, zCacheMedAbsNet, zCacheMedAccel;
        private double zCacheScaleTotal, zCacheScaleNet, zCacheScaleAbsNet, zCacheScaleAccel;

        // Buffers reutilizables para mediana/MAD (sin asignar memoria por barra).
        private readonly List<double> zwTotal = new List<double>();
        private readonly List<double> zwNet = new List<double>();
        private readonly List<double> zwAbsNet = new List<double>();
        private readonly List<double> zwAccel = new List<double>();
        private readonly List<double> madScratch = new List<double>();

        // ── Baseline acumulado de la SESION ILT en curso ─────────────────────
        // Motivo: una ventana rodante de N barras hace que el Z de una misma barra
        // dependa de CUANTAS barras alcanzo a procesar cada instancia. Dos equipos que
        // abren el grafico a distinta hora ven baselines distintos y, por tanto, senales
        // distintas. Anclando el baseline al inicio de sesion (18:00) el conjunto de
        // muestras queda definido por el reloj del mercado, no por el arranque del PC.
        // Se alimentan SOLO con barras cerradas ya evaluadas (causalidad intacta).
        private readonly List<double> sessTotal  = new List<double>();
        private readonly List<double> sessNet    = new List<double>();
        private readonly List<double> sessAbsNet = new List<double>();
        private readonly List<double> sessAccel  = new List<double>();
        private int sessionBaselineLastBar = -1;
        private int sessionFirstBarIndex = -1;
        private bool sessionAnchoredFromOpen = false;
        private const int SESSION_BASELINE_MAX = 6000;   // guarda de memoria; una sesion real no lo alcanza

        // Recalculo por tramos: con sesiones muy largas, rehacer mediana+MAD en CADA barra
        // es O(n^2 log n) durante la carga historica. El paso depende solo del numero de
        // muestras de la sesion, que es identico en todos los equipos con datos completos,
        // asi que la optimizacion NO introduce divergencia entre maquinas.
        private int sessStatsCount = -1;
        private const int SESSION_STATS_BIG   = 800;   // a partir de aqui se recalcula por tramos
        private const int SESSION_STATS_STRIDE = 5;    // muestras entre recalculos en sesiones grandes

        // Brushes atenuados para excesos expirados (se derivan de los colores del usuario).
        private Brush expiredExcessBuyBrush;
        private Brush expiredExcessSellBrush;
        private Brush expiredMomentumBuyBrush;
        private Brush expiredMomentumSellBrush;

        // Diagnóstico de clasificación del agresor (por sesión).
        private long diagPrintCount;
        private long diagZeroTickCount;
        private long diagComparableCount;
        private long diagAgreeCount;

        // (1.0.6) Desenlace de los eventos de anticipacion, separado por tipo. Con estos seis
        // numeros se responde empiricamente si EXCESO y MOMENTUM se comportan distinto: si sus
        // tasas resultan indistinguibles, la distincion no aporta y conviene fusionarlos.
        private int statExcCont, statExcRot, statExcExp;
        private int statMomCont, statMomRot, statMomExp;

        // Escalonado anti-colision de etiquetas en velas contiguas.
        private int lastMarkerBarAbove = -1;
        private int lastMarkerBarBelow = -1;
        private int lastMarkerStackAbove = 0;
        private int lastMarkerStackBelow = 0;
        private const int MARKER_STACK_SPAN   = 2;   // distancia en velas que se considera "contigua"
        private const int MARKER_STACK_LEVELS = 3;   // alturas disponibles antes de reciclar

        // Constantes del baseline robusto.
        private const double ROBUST_MAD_TO_SIGMA = 1.4826;   // hace el MAD comparable a sigma gaussiana
        private const double ROBUST_REL_FLOOR    = 0.15;     // piso de escala relativo al nivel tipico
        private const int    ROBUST_MIN_SAMPLES  = 12;       // warmup minimo para confiar en el baseline

        // Sello de build: se imprime con el modo diagnostico para verificar sin ambiguedad
        // que binario esta ejecutando cada equipo (NT8 cachea ensamblados con frecuencia).
        private const string INDICATOR_BUILD = "v1.3.3 (autoescala visible + panel flotante 2026-09-08)";

        // Histeresis del filtro de anomalia: tras un disparo hay que caer por debajo del
        // piso de re-arme para volver a habilitar senales, evitando el parpadeo en el borde.
        private const double ANOMALY_REARM_FRACTION = 0.60;
        private bool anomalyGateArmed = true;
        private double anomalyReArmFloor = 0.0;

        // Nivel de anomalia ADAPTATIVO a la sesion: en vez de un umbral fijo, el nivel efectivo
        // es un percentil robusto de las magnitudes recientes de la sesion (buffer rodante).
        // Asi "que tan fuerte cuenta como anomalia real" se calibra solo segun lo tranquila o
        // salvaje que este la sesion. Se registra UNA magnitud por barra cerrada; se resetea por
        // sesion; con pocas muestras usa el piso (AnomalyGuideFilterLevel).
        private readonly List<double> anomalyLevelWindow = new List<double>();

        // (1.0.5) Siembra del percentil al abrir sesion. Sin esto, el nivel efectivo cae al
        // piso configurado durante las primeras ~50 barras (el tramo mas activo del dia) y el
        // filtro queda en su punto mas permisivo justo cuando mas selectivo deberia ser.
        // Las muestras sembradas se retiran de a una por cada muestra propia que entra, de
        // modo que el arrastre se diluye linealmente y desaparece por completo.
        private readonly List<double> anomalyLevelSeedCarry = new List<double>();
        private int anomalyLevelSeedRemaining;
        private const int ANOMALY_SEED_MAX = 250;
        private readonly List<double> anomalyLevelScratch = new List<double>();
        private int anomalyLevelLastBar = -1;
        private int anomalyLevelCachedBar = -1;
        private double anomalyLevelCachedValue = 0.0;
        private const int ANOMALY_LEVEL_MIN_SAMPLES = 30;

        // Estado intrasesion determinista (ILT: 18:00 -> 17:00 hora del chart/NY).
        // La clave cambia una sola vez por jornada y gobierna todos los estados que no
        // deben contaminar la sesion siguiente.
        private long activeAnomalySessionKey = long.MinValue;
        private bool anomalySessionInitialized = false;
        private const double GUIDE_RENDER_MIN_CHANGE = 0.25;


        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name                                      = "ILT FLOWSPEED";
                Description                               = "ILT FLOWSPEED v1.3.3 — Motor avanzado de señales con panel Flow Speed clásico, histórico persistente, autoescala visible y control visual por familia.";
                Calculate                                 = Calculate.OnEachTick;
                MaximumBarsLookBack                       = MaximumBarsLookBack.Infinite; // renderizar/evaluar sobre toda la data cargada
                IsOverlay                                 = false;
                DrawOnPricePanel                          = true;
                PaintPriceMarkers                         = false;
                IsSuspendedWhileInactive                  = false;
                ScaleJustification                        = ScaleJustification.Right;

                // 01. Flow engine
                CumulationIntervalMs                      = 1000;
                SpeedLookback                             = 30;
                MinimumTradeVolume                        = 0;
                UseBidAskClassification                   = true;
                ClassifyOnClosedBar                       = true;

                // Modos operativos (presets de la capa de decisión)
                UseOperationalModePresets               = true;
                OperationalMode                         = 2; // 1 = Activo, 2 = Balanceado, 3 = Estricto NQ

                // 02. Core anomaly thresholds
                SpeedZThreshold                           = 1.30;
                ExtremeSpeedZ                             = 2.00;
                AccelerationZThreshold                    = 0.75;
                MinimumImbalance                          = 0.20;
                RequireAnomalyForEverySignal              = true;

                // 02B. Pesos del score de anomalia compuesto (suman internamente a 1).
                AnomalyWeightMagnitude                    = 0.50;
                AnomalyWeightDirection                    = 0.30;
                AnomalyWeightAcceleration                 = 0.20;

                // 03. Price response
                MinimumRangeTicks                         = 4;
                ExcessLookback                            = 5;

                // ── Modelo de exceso en dos fases. La ventana sigue siendo editable,
                // pero la distancia ROT/ACEL se calcula automaticamente mediante un modelo
                // impulso-respuesta normalizado por volatilidad, rango y fuerza del flujo.
                ExcessConfirmWindow                       = 6;    // (1.0.7) margen para la persistencia

                // ── Gatillo base de eficiencia E. El motor lo autoajusta segun la fuerza
                // causal de la anomalia: un impulso fuerte necesita menos E; uno debil, mas.
                UseEfficiencyTrigger                      = true;
                EfficiencyTrigger                         = 0.30;
                EfficiencySmoothPeriod                    = 3;

                // 04. Visual behavior
                ShowHistogramPanel                       = true;  // master visual: oculta plots/guías/SharpDX sin detener el motor
                HistogramMaxAbsValue                      = 120.0;
                HistogramMinDisplayZ                      = 0.25;
                UseAnomalyGuideFilter                    = true;
                AnomalyGuideFilterLevel                  = 45.0;   // escala 0..100 del score compuesto
                UseAdaptiveAnomalyLevel                  = true;   // nivel calibrado a la sesion
                AnomalyLevelPercentile                   = 78.0;   // dispara solo lo mas fuerte de la sesion
                AnomalyLevelLookback                     = 250;    // ventana rodante (solo si el baseline de sesion esta OFF)
                UseSessionBaseline                       = true;   // determinismo entre equipos
                RequireSustainedAcceptance               = true;   // el veredicto inmediato debe ser decisivo
                DebugMode                                = false;
                AnomalyLevelCeiling                      = 80.0;   // nunca tan alto que mate todo
                ShowEfficiencyLine                        = true; // linea E (toggle propia, no contaminada por templates viejos)
                CompatibilityRenderMode                   = false; // fuerza plots/dibujos nativos; mismo motor matemático
                AutoFallbackToNativeRender                = true;  // si SharpDX falla, no vuelve a intentarlo en esta instancia
                CandleWidthHistogram                      = true; // histograma con ancho proporcional a la vela (render propio)
                Enable3DHistogram                          = true;   // efecto glass 3D; se auto-desactiva si hay muchas velas visibles
                Histogram3DMaxVisibleBars                  = 300;    // arriba de esto, render plano rapido automatico (anti chartlag)
                PaintBars                                 = true;
                PaintExcessBars                           = true;
                PaintMomentumBars                         = true;
                PaintContinuationBars                     = true;
                PaintRotationBars                         = true;
                ShowTextLabels                            = true;
                ShowArrows                                = true;
                MarkerOffsetTicks                         = 3;
                MinimumBarsBetweenSignals                 = 1;
                SuppressRepeatedSameSignal                = true;
                SignalLabelFontSize                       = 10;
                SignalLabelBold                           = true;

                // 05. Editable labels / acronyms
                // Anticipacion: EXB/EXS (en extremo), MB/MS (fuera del extremo).
                // Veredicto:    CB/CS (continuacion),   RB/RS (rotacion).
                ExcessBuyLabel                            = "EXB";
                ExcessSellLabel                           = "EXS";
                MomentumBuyLabel                          = "MB";
                MomentumSellLabel                         = "MS";
                AcelBuyLabel                              = "CB";
                AcelSellLabel                             = "CS";
                RotationBuyLabel                          = "RB";
                RotationSellLabel                         = "RS";

                // 06. Editable colors
                AcelBuyBrush                              = Brushes.LimeGreen;
                AcelSellBrush                             = Brushes.Red;
                MomentumBuyBrush                          = Brushes.MediumPurple;
                MomentumSellBrush                         = Brushes.Chocolate;   // (1.0.7) Sienna era ilegible sobre fondo negro
                ExcessBuyBrush                            = Brushes.Orange;
                ExcessSellBrush                           = Brushes.Gold;
                RotationBuyBrush                          = Brushes.DeepSkyBlue;
                RotationSellBrush                         = Brushes.Magenta;
                HistogramBuyBrush                         = Brushes.LimeGreen;
                HistogramSellBrush                        = Brushes.Red;
                HistogramNeutralBrush                     = Brushes.DimGray;
                GuideLineBrush                            = Brushes.DarkGray;
                ZeroLineBrush                             = Brushes.Gray;

                SetFlowSpeedDefaults();

                // Los dos plots se conservan por compatibilidad con plantillas y estrategias,
                // pero son transparentes: el panel visible pertenece exclusivamente al motor clásico.
                AddPlot(new Stroke(Brushes.Transparent, 1), PlotStyle.Line, "FS High");
                AddPlot(new Stroke(Brushes.Transparent, 1), PlotStyle.Line, "FS Mean");

                AddLine(Brushes.Transparent, 0, "UpperAnomalyGuide");
                AddLine(Brushes.Transparent, 0, "ZeroLine");
                AddLine(Brushes.Transparent, 0, "LowerAnomalyGuide");
            }
            else if (State == State.Configure)
            {
                AddDataSeries(BarsPeriodType.Tick, 1);
            }
            else if (State == State.DataLoaded)
            {
                flowWindow          = new Queue<FlowPrint>();
                lock (barDataSync)
                    barData = new Dictionary<int, BarFlowData>();
                histogramRenderSnapshot.Clear();
                histogramRenderScratch.Clear();
                signalVisualRestoreScratch.Clear();
                historicalPlotRestoreScratch.Clear();
                pendingCompletedBars = new Queue<int>();
                pendingCompletedBarSet = new HashSet<int>();
                restoreHistoricalVisualsPending = false;
                restoreHistoricalPlotsPending = false;
                customHistogramRenderDisabledForInstance = false;
                customHistogramRenderFailureCount = 0;
                lastTradePrimaryBarIndex = -1;
                lastProcessedTradeTime = DateTime.MinValue;
                hotCurIdx = -1; hotPrevIdx = int.MinValue; hotCurData = null; hotPrevData = null;
                lastAggressorSide          = 0;
                lastEvaluatedBar    = -1;
                lastSignalBar       = -999999;
                lastSignalCode      = NONE;

                pendingExcessActive   = false;
                pendingExcessIsBuy    = false;
                pendingExcessHigh     = 0.0;
                pendingExcessLow      = 0.0;
                pendingExcessEventRange = 0.0;
                pendingExcessBarIndex = -1;
                pendingExcessLastBar  = -1;
                pendingExcessBarsLeft = 0;
                pendingExcessEventMagnitude = 0.0;
                pendingExcessEventLevel = 0.0;
                pendingResponseScaleTicks = 0.0;
                pendingBestFavorableTicks = 0.0;
                pendingContinuationEvidenceBars = 0;
                pendingRotationEvidenceBars = 0;
                responseRangeScratch.Clear();
                lastExcessMarkerBar   = -999999;

                anomalyLevelWindow.Clear();
                anomalyLevelScratch.Clear();
                anomalyLevelLastBar   = -1;
                anomalyLevelCachedBar = -1;
                anomalyLevelCachedValue = 0.0;
                activeAnomalySessionKey = long.MinValue;
                anomalySessionInitialized = false;
                lastSyncedGuideLevel = double.NaN;

                ResetSessionBaseline();
                ResetMarkerStacking();
                ResetClassificationDiagnostics();
                zCacheScope = -1;

                cumulationIntervalSeconds = Math.Max(0.001, CumulationIntervalMs / 1000.0);
                ApplyOperationalModePresets();
                SyncAnomalyGuideLines();
                FreezeEditableBrushes();
                InitializeFlowSpeedEngine();
                signalFont          = new SimpleFont("Arial", SignalLabelFontSize) { Bold = SignalLabelBold };

                if (DebugMode)
                {
                    Print("═══ ILT FLOWSPEED " + INDICATOR_BUILD + " ═══");
                    Print("  Instrumento: " + (Instrument != null ? Instrument.FullName : "?")
                        + " | TickSize: " + TickSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    Print("  Modo operativo: " + OperationalMode
                        + " | Baseline: " + (UseSessionBaseline ? "SESION COMPLETA (18:00→17:00)" : "RODANTE " + SpeedLookback + " barras")
                        + " | Sensibilidad base: " + AnomalyGuideFilterLevel.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
                    Print("  IMPORTANTE: para que dos equipos coincidan, ambos deben cargar la");
                    Print("  sesion COMPLETA con Tick Replay activo y los mismos dias de datos.");
                }
            }
            else if (State == State.Historical)
            {
                QueueBuildFlowSpeedToolbarMenu();
            }
            else if (State == State.Realtime)
            {
                QueueBuildFlowSpeedToolbarMenu();

                // La transición Historical -> Realtime puede ocurrir después de un callback
                // de BIP 1. No dibujamos directamente desde ese contexto: marcamos la
                // reconstrucción y la ejecutamos usando índices explícitos de la serie primaria.
                restoreHistoricalVisualsPending = true;
                restoreHistoricalPlotsPending = true;
                try
                {
                    TriggerCustomEvent(o =>
                    {
                        FinalizePendingCompletedBars();
                        RestoreHistoricalVisualState();
                    }, null);
                }
                catch
                {
                    // Fallback: se ejecutará en el próximo OnBarUpdate primario.
                }
            }
            else if (State == State.Terminated)
            {
                PrintClassificationDiagnostics("cierre");
                QueueRemoveFlowSpeedToolbarMenu();

                // No eliminamos DrawObjects manualmente aquí. Son objetos no-globales
                // propiedad de esta instancia y NinjaTrader los libera al destruirla.
                // El borrado manual durante F5 podía competir con la instancia nueva.
                if (flowWindow != null) flowWindow.Clear();
                lock (barDataSync)
                {
                    if (barData != null) barData.Clear();
                }
                histogramRenderSnapshot.Clear();
                histogramRenderScratch.Clear();
                signalVisualRestoreScratch.Clear();
                historicalPlotRestoreScratch.Clear();
                if (pendingCompletedBars != null) pendingCompletedBars.Clear();
                if (pendingCompletedBarSet != null) pendingCompletedBarSet.Clear();
                responseRangeScratch.Clear();
                restoreHistoricalVisualsPending = false;
                restoreHistoricalPlotsPending = false;
                lastTradePrimaryBarIndex = -1;
                lastProcessedTradeTime = DateTime.MinValue;
                windowAskVolume = 0;
                windowBidVolume = 0;
                ResetZScoreCache();
                DisposeHistogramBrushes();
                DisposeFlowSpeedEngine();
            }
        }

        protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
        {
            if (marketDataUpdate.MarketDataType == MarketDataType.Bid)
            {
                lastBid = marketDataUpdate.Price;
                return;
            }

            if (marketDataUpdate.MarketDataType == MarketDataType.Ask)
            {
                lastAsk = marketDataUpdate.Price;
                return;
            }
        }

        private void QueueBuildFlowSpeedToolbarMenu()
        {
            if (ChartControl == null || flowSpeedPanelBuildQueued)
                return;

            flowSpeedPanelBuildQueued = true;
            try
            {
                ChartControl.Dispatcher.InvokeAsync(() =>
                {
                    try { BuildFlowSpeedToolbarMenu(); }
                    finally { flowSpeedPanelBuildQueued = false; }
                });
            }
            catch { flowSpeedPanelBuildQueued = false; }
        }

        private void BuildFlowSpeedToolbarMenu()
        {
            if (ChartControl == null)
                return;

            try
            {
                Chart currentChart = Window.GetWindow(ChartControl.Parent) as Chart;
                if (currentChart == null || currentChart.MainMenu == null)
                    return;

                if (flowSpeedToolbarMenu != null
                    && flowSpeedToolbarChart == currentChart
                    && currentChart.MainMenu.Contains(flowSpeedToolbarMenu))
                {
                    EnsureFlowSpeedFloatingPanel();
                    SyncFlowSpeedToolbarChecks();
                    return;
                }

                // Evita botones huérfanos después de F5 o de recargar el indicador.
                List<System.Windows.UIElement> orphaned = new List<System.Windows.UIElement>();
                foreach (System.Windows.UIElement element in currentChart.MainMenu)
                {
                    NTMenuItem existing = element as NTMenuItem;
                    if (existing != null && existing.Tag is string
                        && (string)existing.Tag == FLOW_SPEED_TOOLBAR_TAG)
                        orphaned.Add(element);
                }
                for (int i = 0; i < orphaned.Count; i++)
                    currentChart.MainMenu.Remove(orphaned[i]);

                flowSpeedToolbarChart = currentChart;
                flowSpeedToolbarMenu = new NTMenuItem
                {
                    Header = "ILT FLOWSPEED ▼",
                    Style = Application.Current.TryFindResource("MainMenuItem") as Style,
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 90, 190, 255)),
                    ToolTip = "Controles rápidos de ILT FLOWSPEED",
                    Tag = FLOW_SPEED_TOOLBAR_TAG
                };
                flowSpeedToolbarMenu.Click += FlowSpeedToolbarMenu_Click;
                flowSpeedToolbarPopup = new System.Windows.Controls.ContextMenu
                {
                    PlacementTarget = flowSpeedToolbarMenu,
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
                };

                NTMenuItem modeMenu = new NTMenuItem { Header = "Modo operativo" };
                toolbarModeActiveItem = CreateFlowSpeedToolbarCheck("Activo", OperationalMode == 1);
                toolbarModeBalancedItem = CreateFlowSpeedToolbarCheck("Balanceado", OperationalMode == 2);
                toolbarModeStrictItem = CreateFlowSpeedToolbarCheck("Estricto NQ", OperationalMode == 3);
                toolbarModeActiveItem.Click += (s, e) => SetOperationalModeFromToolbar(1);
                toolbarModeBalancedItem.Click += (s, e) => SetOperationalModeFromToolbar(2);
                toolbarModeStrictItem.Click += (s, e) => SetOperationalModeFromToolbar(3);
                modeMenu.Items.Add(toolbarModeActiveItem);
                modeMenu.Items.Add(toolbarModeBalancedItem);
                modeMenu.Items.Add(toolbarModeStrictItem);
                flowSpeedToolbarPopup.Items.Add(modeMenu);
                flowSpeedToolbarPopup.Items.Add(new System.Windows.Controls.Separator());

                toolbarShowFlowSpeedItem = CreateFlowSpeedToolbarCheck("Mostrar Flow Speed", ShowHistogramPanel);
                toolbarShowArrowsItem = CreateFlowSpeedToolbarCheck("Mostrar flechas", ShowArrows);
                toolbarShowLabelsItem = CreateFlowSpeedToolbarCheck("Mostrar etiquetas", ShowTextLabels);
                toolbarShowFlowSpeedItem.Click += (s, e) =>
                {
                    ShowHistogramPanel = toolbarShowFlowSpeedItem.IsChecked;
                    QueueFlowSpeedToolbarRefresh(false);
                };
                toolbarShowArrowsItem.Click += (s, e) =>
                {
                    ShowArrows = toolbarShowArrowsItem.IsChecked;
                    QueueFlowSpeedToolbarRefresh(true);
                };
                toolbarShowLabelsItem.Click += (s, e) =>
                {
                    ShowTextLabels = toolbarShowLabelsItem.IsChecked;
                    QueueFlowSpeedToolbarRefresh(true);
                };
                flowSpeedToolbarPopup.Items.Add(toolbarShowFlowSpeedItem);
                flowSpeedToolbarPopup.Items.Add(toolbarShowArrowsItem);
                flowSpeedToolbarPopup.Items.Add(toolbarShowLabelsItem);
                flowSpeedToolbarPopup.Items.Add(new System.Windows.Controls.Separator());

                toolbarPaintExcessItem = CreateFlowSpeedToolbarCheck("Pintar velas EXCESO (EXB/EXS)", PaintExcessBars);
                toolbarPaintMomentumItem = CreateFlowSpeedToolbarCheck("Pintar velas MOMENTUM (MB/MS)", PaintMomentumBars);
                toolbarPaintContinuationItem = CreateFlowSpeedToolbarCheck("Pintar velas CONTINUACIÓN (CB/CS)", PaintContinuationBars);
                toolbarPaintRotationItem = CreateFlowSpeedToolbarCheck("Pintar velas ROTACIÓN (RB/RS)", PaintRotationBars);
                toolbarPaintExcessItem.Click += (s, e) =>
                {
                    PaintExcessBars = toolbarPaintExcessItem.IsChecked;
                    QueueFlowSpeedToolbarRefresh(true);
                };
                toolbarPaintMomentumItem.Click += (s, e) =>
                {
                    PaintMomentumBars = toolbarPaintMomentumItem.IsChecked;
                    QueueFlowSpeedToolbarRefresh(true);
                };
                toolbarPaintContinuationItem.Click += (s, e) =>
                {
                    PaintContinuationBars = toolbarPaintContinuationItem.IsChecked;
                    QueueFlowSpeedToolbarRefresh(true);
                };
                toolbarPaintRotationItem.Click += (s, e) =>
                {
                    PaintRotationBars = toolbarPaintRotationItem.IsChecked;
                    QueueFlowSpeedToolbarRefresh(true);
                };
                flowSpeedToolbarPopup.Items.Add(toolbarPaintExcessItem);
                flowSpeedToolbarPopup.Items.Add(toolbarPaintMomentumItem);
                flowSpeedToolbarPopup.Items.Add(toolbarPaintContinuationItem);
                flowSpeedToolbarPopup.Items.Add(toolbarPaintRotationItem);

                currentChart.MainMenu.Add(flowSpeedToolbarMenu);
                EnsureFlowSpeedFloatingPanel();
                SyncFlowSpeedToolbarChecks();
            }
            catch (Exception ex)
            {
                if (DebugMode)
                    Print("ILT FLOWSPEED toolbar: " + ex.Message);
            }
        }

        private void EnsureFlowSpeedFloatingPanel()
        {
            if (ChartControl == null || ChartControl.ChartPanels == null || ChartControl.ChartPanels.Count == 0)
                return;

            ChartPanel host = ChartControl.ChartPanels[0];
            System.Collections.ObjectModel.ObservableCollection<FrameworkElement> controls
                = GetFlowSpeedPanelControls(host);
            if (controls == null)
                return;

            if (flowSpeedPanelRoot != null)
            {
                if (flowSpeedPanelHost != host)
                {
                    System.Collections.ObjectModel.ObservableCollection<FrameworkElement> previous
                        = GetFlowSpeedPanelControls(flowSpeedPanelHost);
                    if (previous != null && previous.Contains(flowSpeedPanelRoot))
                        previous.Remove(flowSpeedPanelRoot);
                }

                if (!controls.Contains(flowSpeedPanelRoot))
                    controls.Add(flowSpeedPanelRoot);

                flowSpeedPanelHost = host;
                System.Windows.Controls.Panel.SetZIndex(flowSpeedPanelRoot, int.MaxValue);
                SyncFlowSpeedToolbarChecks();
                return;
            }

            // Limpia únicamente paneles huérfanos de una instancia anterior tras F5.
            List<FrameworkElement> orphaned = new List<FrameworkElement>();
            foreach (FrameworkElement element in controls)
            {
                if (element != null && element.Tag is string
                    && (string)element.Tag == FLOW_SPEED_PANEL_TAG)
                    orphaned.Add(element);
            }
            for (int i = 0; i < orphaned.Count; i++)
                controls.Remove(orphaned[i]);

            BuildFlowSpeedFloatingPanel(host);
            controls.Add(flowSpeedPanelRoot);
            flowSpeedPanelHost = host;
            System.Windows.Controls.Panel.SetZIndex(flowSpeedPanelRoot, int.MaxValue);
            SyncFlowSpeedToolbarChecks();
        }

        private System.Collections.ObjectModel.ObservableCollection<FrameworkElement> GetFlowSpeedPanelControls(ChartPanel panel)
        {
            if (panel == null)
                return null;

            try
            {
                if (flowSpeedPanelControlsProperty == null)
                {
                    flowSpeedPanelControlsProperty = typeof(ChartPanel).GetProperty(
                        "UserControlCollection",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }

                return flowSpeedPanelControlsProperty == null
                    ? null
                    : flowSpeedPanelControlsProperty.GetValue(panel, null)
                        as System.Collections.ObjectModel.ObservableCollection<FrameworkElement>;
            }
            catch
            {
                return null;
            }
        }

        private void BuildFlowSpeedFloatingPanel(ChartPanel host)
        {
            double initialLeft = Math.Max(10.0, Math.Min(500.0, Math.Max(10.0, host.W - 262.0)));
            flowSpeedPanelRoot = new Grid
            {
                Width = 252,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(initialLeft, 48, 0, 0),
                Background = Brushes.Transparent,
                IsHitTestVisible = true,
                Visibility = Visibility.Collapsed,
                Tag = FLOW_SPEED_PANEL_TAG
            };

            Border outer = new Border
            {
                Background = MakeFlowSpeedPanelBrush(232, 13, 16, 22),
                BorderBrush = MakeFlowSpeedPanelBrush(220, 44, 170, 215),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5)
            };
            flowSpeedPanelRoot.Children.Add(outer);

            StackPanel shell = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Background = Brushes.Transparent
            };
            outer.Child = shell;

            Border header = new Border
            {
                Height = 28,
                Background = MakeFlowSpeedPanelBrush(190, 20, 105, 145),
                BorderBrush = MakeFlowSpeedPanelBrush(220, 44, 170, 215),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Cursor = Cursors.SizeAll
            };
            shell.Children.Add(header);

            Grid headerGrid = new Grid { Background = Brushes.Transparent };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition());
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
            header.Child = headerGrid;

            TextBlock title = new TextBlock
            {
                Text = "ILT FLOWSPEED",
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(7, 0, 0, 0)
            };
            Grid.SetColumn(title, 0);
            headerGrid.Children.Add(title);

            flowSpeedPanelCollapseButton = new Button
            {
                Content = "▲",
                Width = 22,
                Height = 18,
                Margin = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White,
                Background = MakeFlowSpeedPanelBrush(190, 40, 48, 58),
                BorderBrush = MakeFlowSpeedPanelBrush(230, 110, 180, 210),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0),
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold,
                FontSize = 10,
                Cursor = Cursors.Hand
            };
            flowSpeedPanelCollapseButton.Click += (s, e) =>
            {
                flowSpeedPanelExpanded = false;
                SyncFlowSpeedToolbarChecks();
                e.Handled = true;
            };
            Grid.SetColumn(flowSpeedPanelCollapseButton, 1);
            headerGrid.Children.Add(flowSpeedPanelCollapseButton);

            header.MouseLeftButtonDown += (s, e) =>
            {
                if (flowSpeedPanelRoot == null || flowSpeedPanelHost == null)
                    return;
                flowSpeedPanelDragging = true;
                flowSpeedPanelDragStart = e.GetPosition(flowSpeedPanelHost);
                flowSpeedPanelDragMargin = flowSpeedPanelRoot.Margin;
                header.CaptureMouse();
                e.Handled = true;
            };
            header.MouseMove += (s, e) =>
            {
                if (!flowSpeedPanelDragging || flowSpeedPanelRoot == null || flowSpeedPanelHost == null)
                    return;

                Point current = e.GetPosition(flowSpeedPanelHost);
                double left = flowSpeedPanelDragMargin.Left + current.X - flowSpeedPanelDragStart.X;
                double top = flowSpeedPanelDragMargin.Top + current.Y - flowSpeedPanelDragStart.Y;
                double maxLeft = Math.Max(0.0, flowSpeedPanelHost.W - flowSpeedPanelRoot.Width);
                double maxTop = Math.Max(0.0, flowSpeedPanelHost.H - 32.0);
                flowSpeedPanelRoot.Margin = new Thickness(
                    Math.Max(0.0, Math.Min(maxLeft, left)),
                    Math.Max(0.0, Math.Min(maxTop, top)), 0, 0);
                e.Handled = true;
            };
            header.MouseLeftButtonUp += (s, e) =>
            {
                flowSpeedPanelDragging = false;
                header.ReleaseMouseCapture();
                e.Handled = true;
            };

            ScrollViewer scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = Math.Max(220.0, host.H - 90.0),
                Background = Brushes.Transparent
            };
            shell.Children.Add(scroll);

            flowSpeedPanelBody = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 4, 0, 1),
                Background = Brushes.Transparent
            };
            scroll.Content = flowSpeedPanelBody;

            flowSpeedPanelBody.Children.Add(MakeFlowSpeedPanelSection("MODO OPERATIVO"));
            flowSpeedModeCombo = new ComboBox
            {
                Height = 25,
                Margin = new Thickness(2, 3, 2, 5),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold,
                FontSize = 11
            };
            flowSpeedModeCombo.Items.Add("Activo");
            flowSpeedModeCombo.Items.Add("Balanceado");
            flowSpeedModeCombo.Items.Add("Estricto NQ");
            flowSpeedModeCombo.SelectionChanged += (s, e) =>
            {
                if (!flowSpeedPanelSyncing && flowSpeedModeCombo.SelectedIndex >= 0)
                    SetOperationalModeFromToolbar(flowSpeedModeCombo.SelectedIndex + 1);
            };
            flowSpeedPanelBody.Children.Add(flowSpeedModeCombo);

            flowSpeedPanelBody.Children.Add(MakeFlowSpeedPanelSection("VISUAL"));
            toolbarShowFlowSpeedButton = MakeFlowSpeedPanelButton("FLOW SPEED");
            toolbarShowArrowsButton = MakeFlowSpeedPanelButton("FLECHAS");
            toolbarShowLabelsButton = MakeFlowSpeedPanelButton("ETIQUETAS");
            toolbarShowFlowSpeedButton.Click += (s, e) =>
            {
                ShowHistogramPanel = !ShowHistogramPanel;
                SyncFlowSpeedToolbarChecks();
                QueueFlowSpeedToolbarRefresh(false);
            };
            toolbarShowArrowsButton.Click += (s, e) =>
            {
                ShowArrows = !ShowArrows;
                SyncFlowSpeedToolbarChecks();
                QueueFlowSpeedToolbarRefresh(true);
            };
            toolbarShowLabelsButton.Click += (s, e) =>
            {
                ShowTextLabels = !ShowTextLabels;
                SyncFlowSpeedToolbarChecks();
                QueueFlowSpeedToolbarRefresh(true);
            };
            flowSpeedPanelBody.Children.Add(toolbarShowFlowSpeedButton);
            flowSpeedPanelBody.Children.Add(toolbarShowArrowsButton);
            flowSpeedPanelBody.Children.Add(toolbarShowLabelsButton);

            flowSpeedPanelBody.Children.Add(MakeFlowSpeedPanelSection("PINTADO DE VELAS"));
            toolbarPaintExcessButton = MakeFlowSpeedPanelButton("EXCESO  EXB / EXS");
            toolbarPaintMomentumButton = MakeFlowSpeedPanelButton("MOMENTUM  MB / MS");
            toolbarPaintContinuationButton = MakeFlowSpeedPanelButton("CONTINUACIÓN  CB / CS");
            toolbarPaintRotationButton = MakeFlowSpeedPanelButton("ROTACIÓN  RB / RS");
            toolbarPaintExcessButton.Click += (s, e) =>
            {
                PaintExcessBars = !PaintExcessBars;
                SyncFlowSpeedToolbarChecks();
                QueueFlowSpeedToolbarRefresh(true);
            };
            toolbarPaintMomentumButton.Click += (s, e) =>
            {
                PaintMomentumBars = !PaintMomentumBars;
                SyncFlowSpeedToolbarChecks();
                QueueFlowSpeedToolbarRefresh(true);
            };
            toolbarPaintContinuationButton.Click += (s, e) =>
            {
                PaintContinuationBars = !PaintContinuationBars;
                SyncFlowSpeedToolbarChecks();
                QueueFlowSpeedToolbarRefresh(true);
            };
            toolbarPaintRotationButton.Click += (s, e) =>
            {
                PaintRotationBars = !PaintRotationBars;
                SyncFlowSpeedToolbarChecks();
                QueueFlowSpeedToolbarRefresh(true);
            };
            flowSpeedPanelBody.Children.Add(toolbarPaintExcessButton);
            flowSpeedPanelBody.Children.Add(toolbarPaintMomentumButton);
            flowSpeedPanelBody.Children.Add(toolbarPaintContinuationButton);
            flowSpeedPanelBody.Children.Add(toolbarPaintRotationButton);
        }

        private static TextBlock MakeFlowSpeedPanelSection(string text)
        {
            return new TextBlock
            {
                Text = text,
                Height = 21,
                Margin = new Thickness(2, 3, 2, 1),
                Padding = new Thickness(5, 2, 0, 0),
                Foreground = Brushes.White,
                Background = Brushes.Transparent,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold,
                FontSize = 10
            };
        }

        private static Button MakeFlowSpeedPanelButton(string label)
        {
            return new Button
            {
                Content = label,
                Tag = label,
                Height = 27,
                Margin = new Thickness(2, 2, 2, 0),
                Foreground = MakeFlowSpeedPanelBrush(255, 200, 200, 200),
                Background = MakeFlowSpeedPanelBrush(217, 51, 51, 51),
                BorderBrush = MakeFlowSpeedPanelBrush(255, 120, 120, 120),
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Cursor = Cursors.Hand
            };
        }

        private static SolidColorBrush MakeFlowSpeedPanelBrush(byte a, byte r, byte g, byte b)
        {
            SolidColorBrush brush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(a, r, g, b));
            brush.Freeze();
            return brush;
        }

        private static void StyleFlowSpeedPanelButton(Button button, bool enabled)
        {
            if (button == null)
                return;

            string label = button.Tag as string ?? string.Empty;
            button.Content = label + (enabled ? "   ON" : "   OFF");
            button.Background = enabled
                ? MakeFlowSpeedPanelBrush(242, 51, 140, 217)
                : MakeFlowSpeedPanelBrush(217, 51, 51, 51);
            button.Foreground = enabled
                ? Brushes.White
                : MakeFlowSpeedPanelBrush(255, 200, 200, 200);
            button.BorderBrush = MakeFlowSpeedPanelBrush(255, 120, 120, 120);
        }

        private static NTMenuItem CreateFlowSpeedToolbarCheck(string header, bool isChecked)
        {
            return new NTMenuItem
            {
                Header = header,
                IsCheckable = true,
                IsChecked = isChecked,
                StaysOpenOnClick = true
            };
        }

        private void FlowSpeedToolbarMenu_Click(object sender, RoutedEventArgs e)
        {
            if (flowSpeedToolbarMenu == null)
                return;

            flowSpeedPanelExpanded = !flowSpeedPanelExpanded;
            EnsureFlowSpeedFloatingPanel();
            SyncFlowSpeedToolbarChecks();
            e.Handled = true;
        }

        private void SyncFlowSpeedToolbarChecks()
        {
            flowSpeedPanelSyncing = true;
            try
            {
            if (toolbarShowFlowSpeedItem != null) toolbarShowFlowSpeedItem.IsChecked = ShowHistogramPanel;
            if (toolbarShowArrowsItem != null) toolbarShowArrowsItem.IsChecked = ShowArrows;
            if (toolbarShowLabelsItem != null) toolbarShowLabelsItem.IsChecked = ShowTextLabels;
            if (toolbarPaintExcessItem != null) toolbarPaintExcessItem.IsChecked = PaintExcessBars;
            if (toolbarPaintMomentumItem != null) toolbarPaintMomentumItem.IsChecked = PaintMomentumBars;
            if (toolbarPaintContinuationItem != null) toolbarPaintContinuationItem.IsChecked = PaintContinuationBars;
            if (toolbarPaintRotationItem != null) toolbarPaintRotationItem.IsChecked = PaintRotationBars;
            if (toolbarModeActiveItem != null) toolbarModeActiveItem.IsChecked = OperationalMode == 1;
            if (toolbarModeBalancedItem != null) toolbarModeBalancedItem.IsChecked = OperationalMode == 2;
            if (toolbarModeStrictItem != null) toolbarModeStrictItem.IsChecked = OperationalMode == 3;

                if (flowSpeedToolbarMenu != null)
                    flowSpeedToolbarMenu.Header = flowSpeedPanelExpanded ? "ILT FLOWSPEED ▲" : "ILT FLOWSPEED ▼";
                if (flowSpeedPanelRoot != null)
                    flowSpeedPanelRoot.Visibility = flowSpeedPanelExpanded ? Visibility.Visible : Visibility.Collapsed;
                if (flowSpeedPanelCollapseButton != null)
                    flowSpeedPanelCollapseButton.Content = "▲";
                if (flowSpeedModeCombo != null)
                    flowSpeedModeCombo.SelectedIndex = Math.Max(0, Math.Min(2, OperationalMode - 1));

                StyleFlowSpeedPanelButton(toolbarShowFlowSpeedButton, ShowHistogramPanel);
                StyleFlowSpeedPanelButton(toolbarShowArrowsButton, ShowArrows);
                StyleFlowSpeedPanelButton(toolbarShowLabelsButton, ShowTextLabels);
                StyleFlowSpeedPanelButton(toolbarPaintExcessButton, PaintExcessBars);
                StyleFlowSpeedPanelButton(toolbarPaintMomentumButton, PaintMomentumBars);
                StyleFlowSpeedPanelButton(toolbarPaintContinuationButton, PaintContinuationBars);
                StyleFlowSpeedPanelButton(toolbarPaintRotationButton, PaintRotationBars);
            }
            finally
            {
                flowSpeedPanelSyncing = false;
            }
        }

        private void SetOperationalModeFromToolbar(int mode)
        {
            OperationalMode = Math.Max(1, Math.Min(3, mode));
            SyncFlowSpeedToolbarChecks();
            try
            {
                TriggerCustomEvent(o =>
                {
                    ApplyOperationalModePresets();
                    ForceRefresh();
                }, null);
            }
            catch
            {
                try { ForceRefresh(); } catch { }
            }
        }

        private void QueueFlowSpeedToolbarRefresh(bool rebuildSignalVisuals)
        {
            try
            {
                TriggerCustomEvent(o =>
                {
                    if (rebuildSignalVisuals)
                        RebuildSignalVisualsFromToolbar();
                    ForceRefresh();
                }, null);
            }
            catch
            {
                try { ForceRefresh(); } catch { }
            }
        }

        private void RebuildSignalVisualsFromToolbar()
        {
            int primaryLast = CurrentBars[0];
            if (primaryLast < 0)
                return;

            signalVisualRestoreScratch.Clear();
            lock (barDataSync)
            {
                if (barData == null)
                    return;

                foreach (KeyValuePair<int, BarFlowData> kv in barData)
                {
                    if (kv.Key < 0 || kv.Key > primaryLast || kv.Value == null
                        || kv.Value.VisualSignalCode == NONE)
                        continue;

                    signalVisualRestoreScratch.Add(new SignalVisualSnapshot
                    {
                        BarIndex = kv.Key,
                        Code = kv.Value.VisualSignalCode
                    });
                }
            }

            signalVisualRestoreScratch.Sort((a, b) => a.BarIndex.CompareTo(b.BarIndex));
            ResetMarkerStacking();
            for (int i = 0; i < signalVisualRestoreScratch.Count; i++)
            {
                SignalVisualSnapshot visual = signalVisualRestoreScratch[i];
                int barsAgo = primaryLast - visual.BarIndex;
                if (barsAgo < 0 || barsAgo > primaryLast)
                    continue;

                RemoveSignalDrawings(visual.BarIndex, visual.Code);
                BarBrushes.Set(visual.BarIndex, null);
                CandleOutlineBrushes.Set(visual.BarIndex, null);

                lock (flowSpeedSync)
                {
                    flowSpeedPaintedBars.Remove(visual.BarIndex);
                    ApplyFlowSpeedPricePaintingLocked(visual.BarIndex);
                }

                string label;
                Brush brush;
                bool drawAbove;
                MapSignalVisual(visual.Code, out label, out brush, out drawAbove);
                PaintSignalBarOnly(barsAgo, visual.Code, brush);
                DrawSignal(barsAgo, visual.Code, label, brush, drawAbove);
            }
        }

        private void QueueRemoveFlowSpeedToolbarMenu()
        {
            Chart chart = flowSpeedToolbarChart;
            NTMenuItem menu = flowSpeedToolbarMenu;
            ContextMenu popup = flowSpeedToolbarPopup;
            ChartPanel panelHost = flowSpeedPanelHost;
            Grid panelRoot = flowSpeedPanelRoot;
            if (ChartControl == null)
                return;

            try
            {
                ChartControl.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        if (menu != null)
                        {
                            menu.Click -= FlowSpeedToolbarMenu_Click;
                            if (chart != null && chart.MainMenu != null && chart.MainMenu.Contains(menu))
                                chart.MainMenu.Remove(menu);
                            menu.Items.Clear();
                        }
                        if (popup != null)
                        {
                            popup.IsOpen = false;
                            popup.Items.Clear();
                        }
                        System.Collections.ObjectModel.ObservableCollection<FrameworkElement> controls
                            = GetFlowSpeedPanelControls(panelHost);
                        if (controls != null && panelRoot != null && controls.Contains(panelRoot))
                            controls.Remove(panelRoot);
                    }
                    catch { }
                });
            }
            catch { }

            flowSpeedToolbarMenu = null;
            flowSpeedToolbarChart = null;
            flowSpeedToolbarPopup = null;
            flowSpeedPanelHost = null;
            flowSpeedPanelRoot = null;
            flowSpeedPanelBody = null;
            flowSpeedModeCombo = null;
            flowSpeedPanelCollapseButton = null;
            toolbarShowFlowSpeedButton = null;
            toolbarShowArrowsButton = null;
            toolbarShowLabelsButton = null;
            toolbarPaintExcessButton = null;
            toolbarPaintMomentumButton = null;
            toolbarPaintContinuationButton = null;
            toolbarPaintRotationButton = null;
            flowSpeedPanelExpanded = false;
            flowSpeedPanelBuildQueued = false;
            flowSpeedPanelSyncing = false;
            flowSpeedPanelDragging = false;
            toolbarShowFlowSpeedItem = null;
            toolbarShowArrowsItem = null;
            toolbarShowLabelsItem = null;
            toolbarPaintExcessItem = null;
            toolbarPaintMomentumItem = null;
            toolbarPaintContinuationItem = null;
            toolbarPaintRotationItem = null;
            toolbarModeActiveItem = null;
            toolbarModeBalancedItem = null;
            toolbarModeStrictItem = null;
        }

        protected override void OnBarUpdate()
        {
            // === CANDADO LICENCIA ILT_SUITE ===
            if (!IltEsValida_ILTFLOWSPEED()) return;

            if (CurrentBars[0] < 0)
                return;

            if (BarsInProgress == 1)
            {
                if (CurrentBars[1] < 1)
                    return;

                double flowSpeedBid = lastBid;
                double flowSpeedAsk = lastAsk;
                try
                {
                    flowSpeedBid = BarsArray[1].GetBid(CurrentBars[1]);
                    flowSpeedAsk = BarsArray[1].GetAsk(CurrentBars[1]);
                }
                catch { }

                ProcessTrade(Closes[1][0], Volumes[1][0], Times[1][0]);
                ProcessFlowSpeedTrade(Closes[1][0], Volumes[1][0], Times[1][0],
                    flowSpeedBid, flowSpeedAsk, CurrentBars[0]);
                return;
            }

            if (BarsInProgress != 0)
                return;

            // Toda finalización histórica/realtime se ejecuta desde BIP 0. BIP 1 solo
            // acumula ticks y encola la barra cerrada. Así la potencia del equipo afecta
            // únicamente el tiempo de carga, nunca el resultado ni la disponibilidad visual.
            FinalizePendingCompletedBars();

            int primaryCurrent = CurrentBars[0];

            // En histórico no se dibuja cada estado intermedio: todos los ticks ya fueron
            // procesados por el motor y el resultado se escribe una sola vez al cerrar la vela.
            // En tiempo real conservamos actualización fluida con throttle (~12.5 FPS).
            if (State == State.Realtime)
                UpdatePrimaryPlots(primaryCurrent, ShouldRefreshRealtimePlots());

            RefreshFlowSpeedVisuals(primaryCurrent);

            if (restoreHistoricalVisualsPending || restoreHistoricalPlotsPending)
                RestoreHistoricalVisualState();

            if (primaryCurrent < SpeedLookback + 2)
                return;

            // El modo intrabar sigue disponible. El modo por vela cerrada se resuelve
            // exclusivamente en FinalizePendingCompletedBars para evitar doble evaluación.
            if (!ClassifyOnClosedBar)
                EvaluateSignal(primaryCurrent, 0, false);
        }

        private int PrimaryCurrentBarSafe()
        {
            try
            {
                return CurrentBars != null && CurrentBars.Length > 0 ? CurrentBars[0] : -1;
            }
            catch
            {
                return -1;
            }
        }

        private bool ShouldRefreshRealtimePlots()
        {
            // Historical processing is already bar-based enough; keep it complete.
            if (State != State.Realtime)
                return true;

            int nowMs = Environment.TickCount;
            if (nowMs - lastRealtimePlotRefreshMs < REALTIME_PLOT_REFRESH_MS)
                return false;

            lastRealtimePlotRefreshMs = nowMs;
            return true;
        }

        private void ProcessTrade(double tradePrice, double tradeVolume, DateTime tradeTime)
        {
            int primaryBarIndex = CurrentBars[0];
            if (primaryBarIndex < 0 || double.IsNaN(tradePrice) || double.IsInfinity(tradePrice) || tradePrice <= 0.0)
                return;

            // Si la fuente rebobina timestamps al recargar o cambia de sesión, vaciamos
            // únicamente la ventana de velocidad. No contaminamos el nuevo tramo con prints
            // futuros ni con una pausa extensa del mercado. Este bookkeeping corre para
            // TODO print válido, ANTES de cualquier filtro de volumen.
            if (lastProcessedTradeTime != DateTime.MinValue)
            {
                double gapSeconds = (tradeTime - lastProcessedTradeTime).TotalSeconds;
                if (gapSeconds < -0.001 || gapSeconds > Math.Max(10.0, cumulationIntervalSeconds * 10.0))
                {
                    flowWindow.Clear();
                    windowAskVolume = 0.0;
                    windowBidVolume = 0.0;
                }
            }
            lastProcessedTradeTime = tradeTime;

            // Cierre determinista: BIP 1 detecta el cambio, pero NO toca Plots/Draw.
            // Solo encola la barra anterior para consolidarla desde BIP 0. También esto
            // ocurre antes del filtro de volumen: el cierre de barra no depende de él.
            if (lastTradePrimaryBarIndex >= 0 && primaryBarIndex != lastTradePrimaryBarIndex)
                QueueCompletedBarForPrimary(lastTradePrimaryBarIndex);

            lastTradePrimaryBarIndex = primaryBarIndex;

            // Algunos proveedores/archivos históricos pueden entregar volumen 0 en la
            // serie de 1 tick. El conteo unitario preserva dirección, velocidad relativa
            // y señales en lugar de colapsar todo el histograma a cero.
            double normalizedVolume = (!double.IsNaN(tradeVolume) && !double.IsInfinity(tradeVolume) && tradeVolume > 0.0)
                ? tradeVolume
                : 1.0;

            if (MinimumTradeVolume > 0 && tradeVolume < MinimumTradeVolume)
            {
                // El print no acumula, pero el tape sí avanza: el tick-rule debe seguir
                // comparando contra el último precio realmente negociado.
                lastTradePrice = tradePrice;
                return;
            }

            BarFlowData d = GetBarDataHot(primaryBarIndex);
            if (d == null)
                return;

            int side = ClassifyAggressorSide(tradePrice);
            double askVol = 0.0;
            double bidVol = 0.0;

            if (side > 0)
            {
                askVol = normalizedVolume;
                lastAggressorSide = 1;
            }
            else if (side < 0)
            {
                bidVol = normalizedVolume;
                lastAggressorSide = -1;
            }
            else
            {
                // side == 0 solo ocurre cuando NO existe agresor previo, es decir en los
                // primeros prints tras cargar datos o abrir sesión: no hay ninguna
                // referencia de la que heredar. Ese volumen se reparte por simetría.
                // (La persistencia de zero ticks ya la resolvió ClassifyAggressorSide.)
                askVol = normalizedVolume * 0.5;
                bidVol = normalizedVolume * 0.5;
            }

            lastTradePrice = tradePrice;

            d.AskVolume += askVol;
            d.BidVolume += bidVol;

            AddPrintToWindow(tradeTime, askVol, bidVol);
            UpdateFlowMetrics(primaryBarIndex, d);

            // Importante: no escribimos Series, PlotBrushes, DrawObjects ni BarBrushes
            // desde BIP 1. El siguiente callback primario consolidará el estado exacto.
        }

        private void QueueCompletedBarForPrimary(int barIndex)
        {
            if (barIndex < 0)
                return;

            if (pendingCompletedBars == null)
                pendingCompletedBars = new Queue<int>();
            if (pendingCompletedBarSet == null)
                pendingCompletedBarSet = new HashSet<int>();

            if (pendingCompletedBarSet.Add(barIndex))
                pendingCompletedBars.Enqueue(barIndex);
        }

        private void FinalizePendingCompletedBars()
        {
            if (pendingCompletedBars == null || pendingCompletedBars.Count == 0)
                return;

            int primaryLast = PrimaryCurrentBarSafe();
            if (primaryLast < 0)
                return;

            // La cola se construye cronológicamente desde BIP 1. Procesarla en ese mismo
            // orden conserva causalidad de sesión, percentil adaptativo y excesos pendientes.
            while (pendingCompletedBars.Count > 0)
            {
                int completedBar = pendingCompletedBars.Peek();
                if (completedBar > primaryLast)
                    break;

                pendingCompletedBars.Dequeue();
                pendingCompletedBarSet.Remove(completedBar);

                int completedBarsAgo = primaryLast - completedBar;
                if (completedBarsAgo < 0 || completedBarsAgo > primaryLast)
                    continue;

                UpdatePrimaryPlots(completedBar, false);

                if (ClassifyOnClosedBar && completedBar >= SpeedLookback + 2)
                    EvaluateSignal(completedBar, completedBarsAgo, true);
            }
        }

        // Cascada de clasificación del agresor, en orden de fiabilidad decreciente:
        //   1) Regla de cotización (solo tiempo real): el print toca el ask => compra;
        //      toca el bid => venta. Es la clasificación fiable. En histórico se omite a
        //      propósito porque el bid/ask puede venir ausente o traer la cotización actual
        //      del equipo, lo que haría divergir a dos instalaciones sobre los mismos datos.
        //   2) Tick rule: comparación contra el último precio operado. Determinista.
        //   3) PERSISTENCIA (zero tick): si el precio no cambió, el print hereda el lado del
        //      agresor anterior. NO es un fallback perezoso, es la clasificación correcta en
        //      ES/NQ: con un tick de horquilla, dos prints consecutivos al mismo precio solo
        //      pueden tener agresores distintos si la cotización se desplazó entre ambos.
        //      Mientras no se mueva, están golpeando la MISMA cola pasiva y son del mismo
        //      lado. Es exactamente el caso de absorción en un extremo (ask que se recarga
        //      una y otra vez): repartir ese volumen 50/50 inventaría una agresión contraria
        //      que no existe, cuando esa parte del mercado está pasiva.
        //      La literatura reporta caídas de precisión del tick rule concentradas en zero
        //      ticks, pero medidas sobre renta variable, donde una fracción grande de prints
        //      ocurre DENTRO de una horquilla ancha. Esa geometría no aplica a ES/NQ.
        private int ClassifyAggressorSide(double tradePrice)
        {
            // El tick rule se evalúa SIEMPRE, aunque no decida: es la sonda de diagnóstico
            // que permite medir el peso real de los zero ticks y el grado de acuerdo entre
            // ambas reglas. No altera la decisión: el orden de precedencia es el de abajo.
            int tickSide = 0;
            if (lastTradePrice > 0)
            {
                if (tradePrice > lastTradePrice)
                    tickSide = 1;
                else if (tradePrice < lastTradePrice)
                    tickSide = -1;
            }

            diagPrintCount++;
            if (lastTradePrice > 0 && tickSide == 0)
                diagZeroTickCount++;

            int quoteSide = 0;
            if (State == State.Realtime && UseBidAskClassification)
            {
                if (lastAsk > 0 && tradePrice >= lastAsk - TickSize * 0.5)
                    quoteSide = 1;
                else if (lastBid > 0 && tradePrice <= lastBid + TickSize * 0.5)
                    quoteSide = -1;
            }

            // Solo son comparables los prints donde AMBAS reglas se pronuncian.
            if (quoteSide != 0 && tickSide != 0)
            {
                diagComparableCount++;
                if (quoteSide == tickSide)
                    diagAgreeCount++;
            }

            if (quoteSide != 0)
                return quoteSide;

            if (tickSide != 0)
                return tickSide;

            return lastAggressorSide;
        }

        // Resumen por sesión de la calidad de clasificación. Dos números deciden si conviene
        // igualar histórico y tiempo real: cuánto pesa el zero tick y cuánto discrepan las
        // reglas cuando ambas pueden pronunciarse.
        private void PrintClassificationDiagnostics(string tag)
        {
            if (!DebugMode || diagPrintCount <= 0)
                return;

            double zeroPct = 100.0 * diagZeroTickCount / diagPrintCount;

            string acuerdo = diagComparableCount > 0
                ? (100.0 * diagAgreeCount / diagComparableCount).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                    + "% sobre " + diagComparableCount + " prints comparables"
                : "sin muestras en tiempo real";

            int exc = statExcCont + statExcRot + statExcExp;
            int mom = statMomCont + statMomRot + statMomExp;
            if (exc + mom > 0)
                Print("── [EVENTOS] " + tag
                    + " | EXCESO " + exc + ": cont " + Pct(statExcCont, exc)
                        + " rot " + Pct(statExcRot, exc) + " exp " + Pct(statExcExp, exc)
                    + " | MOMENTUM " + mom + ": cont " + Pct(statMomCont, mom)
                        + " rot " + Pct(statMomRot, mom) + " exp " + Pct(statMomExp, mom));

            Print("── [CLASIFICACION] " + tag
                + " | prints: " + diagPrintCount
                + " | zero tick: " + zeroPct.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%"
                + " | cotizacion vs tick rule: " + acuerdo);
        }

        private static string Pct(int part, int total)
        {
            if (total <= 0) return "n/a";
            return (100.0 * part / total).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%";
        }

        private void ResetClassificationDiagnostics()
        {
            statExcCont = statExcRot = statExcExp = 0;
            statMomCont = statMomRot = statMomExp = 0;
            diagPrintCount = 0;
            diagZeroTickCount = 0;
            diagComparableCount = 0;
            diagAgreeCount = 0;
        }

        private void AddPrintToWindow(DateTime printTime, double askVol, double bidVol)
        {
            FlowPrint fp = new FlowPrint
            {
                Time = printTime,
                AskVolume = askVol,
                BidVolume = bidVol
            };

            flowWindow.Enqueue(fp);
            windowAskVolume += askVol;
            windowBidVolume += bidVol;

            while (flowWindow.Count > 0 && (printTime - flowWindow.Peek().Time).TotalSeconds > cumulationIntervalSeconds)
            {
                FlowPrint old = flowWindow.Dequeue();
                windowAskVolume -= old.AskVolume;
                windowBidVolume -= old.BidVolume;
            }
        }

        private void UpdateFlowMetrics(int barIndex, BarFlowData d)
        {
            double currentBuySpeed = windowAskVolume / cumulationIntervalSeconds;
            double currentSellSpeed = windowBidVolume / cumulationIntervalSeconds;
            double currentNetSpeed = currentBuySpeed - currentSellSpeed;
            double currentTotalSpeed = currentBuySpeed + currentSellSpeed;

            d.Samples++;
            d.SumNetSpeed += currentNetSpeed;
            d.SumTotalSpeed += currentTotalSpeed;


            d.NetSpeed = d.SumNetSpeed / Math.Max(1, d.Samples);
            d.TotalSpeed = d.SumTotalSpeed / Math.Max(1, d.Samples);
            d.AbsNetSpeed = Math.Abs(d.NetSpeed);

            double totalAggressiveVolume = d.AskVolume + d.BidVolume;
            d.Imbalance = totalAggressiveVolume.ApproxCompare(0) == 0 ? 0 : (d.AskVolume - d.BidVolume) / totalAggressiveVolume;

            BarFlowData prev = GetPrevBarDataHot(barIndex - 1);
            d.Acceleration = prev == null ? 0 : d.NetSpeed - prev.NetSpeed;

            if (barIndex >= SpeedLookback + 2)
                UpdateZScoresFast(barIndex, d);
        }

        private void UpdatePrimaryPlots(int barIndex, bool refreshGuides)
        {
            int primaryLast = CurrentBars[0];
            int targetBarsAgo = primaryLast - barIndex;
            if (targetBarsAgo < 0 || targetBarsAgo > primaryLast)
                return;

            BarFlowData d = GetBarData(barIndex, false);
            if (d == null)
            {
                SetFlowSpeedPlotValues(barIndex, targetBarsAgo);
                return;
            }

            // OPCION A — el histograma grafica la AGRESION con signo (score compuesto M*D,
            // verde=compra / rojo=venta) SIN suavizado, para que la barra caiga exactamente en
            // su vela. La linea (plot 1) es la EFICIENCIA E: verde si el precio sigue al flujo,
            // roja si lo rechaza. "barra verde + linea E roja" = rotacion; mismo color = continuacion.
            ComputeAnomalyScore(d);

            double magnitudeZ = Math.Max(0, Math.Max(d.SpeedZ, d.AbsNetSpeedZ));

            double rawHistValue = d.AnomalySigned;
            if (magnitudeZ < HistogramMinDisplayZ)   // gate visual: barra no minimamente rapida no dibuja
                rawHistValue = 0;

            double histValue = Clamp(rawHistValue, -HistogramMaxAbsValue, HistogramMaxAbsValue);
            d.VisualHistogramValue = histValue;

            // Eficiencia E del bar actual. IMPORTANTE: se leen los precios con accesores
            // EXPLICITOS de la serie primaria (Opens[0]/Highs[0]/Lows[0]/Closes[0]). Con la
            // serie de 1 tick agregada, los accesores sin calificar (High[]/Close[]) podian
            // resolver a la serie de ticks (rango 0) y dejaban E clavada en 0.
            int pBarsAgo = targetBarsAgo;
            double eff = d.Efficiency;
            if (pBarsAgo >= 0 && pBarsAgo <= primaryLast)
            {
                double hi = Highs[0][pBarsAgo];
                double lo = Lows[0][pBarsAgo];
                double rng = hi - lo;
                if (rng > 0.0)
                {
                    double rTicks = rng / TickSize;
                    double bTicks = (Closes[0][pBarsAgo] - Opens[0][pBarsAgo]) / TickSize;
                    double cLoc = (Closes[0][pBarsAgo] - lo) / rng;
                    double rawEfficiency = ComputeEfficiency(d.AnomalySigned, bTicks, rTicks, cLoc);
                    eff = SmoothEfficiency(barIndex, rawEfficiency);
                    d.Efficiency = eff;
                }
            }

            // El score avanzado se conserva para EXB/EXS/MB/MS/CB/CS/RB/RS, pero ya no
            // alimenta el panel. Los únicos valores visuales son los del motor Flow Speed clásico.
            SetFlowSpeedPlotValues(barIndex, targetBarsAgo);
        }

        private void ApplyOperationalModePresets()
        {
            if (!UseOperationalModePresets)
                return;

            int mode = Math.Max(1, Math.Min(3, OperationalMode));

            // Los presets ajustan SOLO la capa oculta de decisión/filtrado del motor
            // (umbrales Z, imbalance, rango, debounce, piso visual). Nunca sobreescriben
            // propiedades visibles del usuario: el filtro de anomalía y su sensibilidad
            // base pertenecen al operador y, como DataLoaded corre en cada recarga,
            // pisarlas aquí dejaba esos controles sin efecto real.
            if (mode == 1) // Active / low filter: more signals for study and replay.
            {
                SpeedZThreshold                  = 1.00;
                ExtremeSpeedZ                    = 1.70;
                AccelerationZThreshold           = 0.55;
                MinimumImbalance                 = 0.12;
                RequireAnomalyForEverySignal     = true;

                MinimumRangeTicks                = 3;

                MinimumBarsBetweenSignals        = 1;
                SuppressRepeatedSameSignal       = false;
                HistogramMinDisplayZ             = 0.15;
            }
            else if (mode == 2) // Balanced: default operating mode.
            {
                SpeedZThreshold                  = 1.30;
                ExtremeSpeedZ                    = 2.00;
                AccelerationZThreshold           = 0.75;
                MinimumImbalance                 = 0.20;
                RequireAnomalyForEverySignal     = true;

                MinimumRangeTicks                = 4;

                MinimumBarsBetweenSignals        = 1;
                SuppressRepeatedSameSignal       = true;
                HistogramMinDisplayZ             = 0.25;
            }
            else // Strict NQ: fewer signals; designed for cleaner NQ execution context.
            {
                SpeedZThreshold                  = 1.65;
                ExtremeSpeedZ                    = 2.35;
                AccelerationZThreshold           = 1.00;
                MinimumImbalance                 = 0.28;
                RequireAnomalyForEverySignal     = true;

                MinimumRangeTicks                = 6;

                MinimumBarsBetweenSignals        = 3;
                SuppressRepeatedSameSignal       = true;
                HistogramMinDisplayZ             = 0.35;
            }
        }

        private void SyncAnomalyGuideLines()
        {
            if (!ShowHistogramPanel)
                return;

            // Nivel adaptativo a la sesion: las lineas guia RESPIRAN (suben en sesion volatil,
            // bajan en sesion tranquila). Con el adaptativo apagado, vuelve al nivel fijo.
            double level = CurrentAnomalyLevel(PrimaryCurrentBarSafe());

            // Avoid touching NinjaTrader line objects on every tick when the selected
            // operating mode / anomaly level has not changed. This is visual only.
            if (!double.IsNaN(lastSyncedGuideLevel) && Math.Abs(lastSyncedGuideLevel - level) < GUIDE_RENDER_MIN_CHANGE)
                return;

            try
            {
                // Lines order follows AddLine calls in SetDefaults:
                // 0 = UpperAnomalyGuide, 1 = ZeroLine, 2 = LowerAnomalyGuide.
                // Some NinjaTrader builds expose Lines as a custom collection without
                // Count/Length, so access directly and let the guarded block fail safely.
                Lines[0].Value = level;
                Lines[1].Value = 0;
                Lines[2].Value = -level;
                lastSyncedGuideLevel = level;
            }
            catch
            {
                // Visual guide sync only. Never interrupt the flow engine.
            }
        }

        private void EvaluateSignal(int barIndex, int barsAgo, bool skipIfAlreadyEvaluated)
        {
            if (barIndex < SpeedLookback + 2)
                return;

            if (skipIfAlreadyEvaluated && barIndex == lastEvaluatedBar)
                return;

            BarFlowData d = GetBarData(barIndex, false);
            if (d == null || d.Samples <= 0)
                return;

            double range = Highs[0][barsAgo] - Lows[0][barsAgo];
            if (range <= TickSize * 0.5)
                return;

            double rangeTicks = range / TickSize;
            double bodyTicks = (Closes[0][barsAgo] - Opens[0][barsAgo]) / TickSize;
            double closeLocation = (Closes[0][barsAgo] - Lows[0][barsAgo]) / range;

            // Delimita y, si corresponde, reinicia el contexto intrasesion ANTES de
            // evaluar la barra. En una recarga F5 se ejecuta en el mismo orden historico.
            EnsureAnomalySession(barIndex);

            UpdateZScoresFast(barIndex, d);
            ComputeAnomalyScore(d);   // score compuesto coherente con histograma + filtro

            // CAUSALIDAD: esta barra se evalua con el percentil construido SOLO con barras
            // anteriores. Su magnitud se incorpora en el finally para afectar la siguiente.
            d.EffectiveAnomalyLevel = CurrentAnomalyLevel(barIndex);
            double rawEfficiency = ComputeEfficiency(d.AnomalySigned, bodyTicks, rangeTicks, closeLocation);
            d.Efficiency = SmoothEfficiency(barIndex, rawEfficiency);

            try
            {
                // Histéresis del gate: la condición de re-arme se evalúa en CADA barra
                // (no solo en barras candidatas a señal). Sin esto, tras un disparo el
                // gate podía quedar desarmado indefinidamente dentro de la sesión,
                // porque solo las barras ya anómalas llegaban a consultar el piso.
                if (!anomalyGateArmed && d.AnomalyMagnitude < anomalyReArmFloor)
                    anomalyGateArmed = true;

                bool speedAnomaly = d.SpeedZ >= SpeedZThreshold || d.AbsNetSpeedZ >= SpeedZThreshold;
                bool extremeSpeed = d.SpeedZ >= ExtremeSpeedZ || d.AbsNetSpeedZ >= ExtremeSpeedZ;
                bool accelerationAnomaly = Math.Abs(d.AccelerationZ) >= AccelerationZThreshold;

                // Direccion con piso real de fuerza: se quita el escape "|| NetSpeed > 0",
                // que volvia buyFlow/sellFlow verdadero con cualquier signo, por minimo que fuera.
                bool buyFlow = d.Imbalance >= MinimumImbalance || d.NetSpeedZ >= 0.50;
                bool sellFlow = d.Imbalance <= -MinimumImbalance || d.NetSpeedZ <= -0.50;

                // Aceleracion sin el escape "|| Acceleration > 0": exige Z de aceleracion real.
                bool buyAcceleration = buyFlow && d.AccelerationZ >= AccelerationZThreshold;
                bool sellAcceleration = sellFlow && d.AccelerationZ <= -AccelerationZThreshold;

                // Confirmación multi-evidencia: se cuentan
                // componentes INDEPENDIENTES de flujo. Una sola imbalance sobre volumen fino
                // ya no alcanza; se exige acuerdo de al menos dos para las senales de continuacion.
                int buyEvidence =
                      (d.Imbalance >= MinimumImbalance ? 1 : 0)
                    + (d.NetSpeedZ >= 0.50 ? 1 : 0)
                    + (d.AccelerationZ >= AccelerationZThreshold ? 1 : 0);
                int sellEvidence =
                      (d.Imbalance <= -MinimumImbalance ? 1 : 0)
                    + (d.NetSpeedZ <= -0.50 ? 1 : 0)
                    + (d.AccelerationZ <= -AccelerationZThreshold ? 1 : 0);

                bool buyConfirmed = buyEvidence >= 2;
                bool sellConfirmed = sellEvidence >= 2;

                // ── Fase 2 (prioridad): resolver un exceso PENDIENTE por confirmacion de
                // precio. Corre ANTES del gate de anomalia porque la vela de confirmacion
                // suele ser una vela normal (el precio derivando), no una anomalia.
                int resolvedDeferred = ResolvePendingExcess(barIndex, barsAgo);
                if (resolvedDeferred != NONE)
                {
                    string dLabel; Brush dBrush; bool dAbove;
                    MapSignalVisual(resolvedDeferred, out dLabel, out dBrush, out dAbove);
                    EmitSignal(barIndex, barsAgo, d, resolvedDeferred, dLabel, dBrush, dAbove, true);
                    return;
                }

                // Gate de anomalia PURO M: el modelo M + E cubre todo. Se retiro el subsistema
                // legacy de exceso-por-secuencia (clusters + DetectSequence*): la rotacion ya se
                // resuelve por la via exceso -> pendiente -> E (confirmada por flujo real).
                bool anyAnomaly = speedAnomaly || accelerationAnomaly || extremeSpeed;

                if (RequireAnomalyForEverySignal && !anyAnomaly)
                {
                    lastEvaluatedBar = barIndex;
                    d.SignalCode = NONE;
                    return;
                }

                // ═══════ MODELO UNIFICADO M + E (limpio, sin redundancias) ═══════
                // M = agresion (score compuesto, ya calculado). E = eficiencia (d.Efficiency).
                // E es el UNICO juez del lado-precio: reemplaza strongResponse, los ticks de
                // confirmacion y las mechas. Eventos:
                //   ACELERACION (continuacion): M + acelerando + fuera de extremo + E >= +umbral.
                //   EXCESO: M en un extremo -> pendiente (anticipacion); la E lo resuelve luego
                //           (E rel. al exceso >= +umbral => continuacion ; <= -umbral => rotacion).

                bool atRecentHigh = Highs[0][barsAgo] >= HighestHigh(barsAgo + 1, ExcessLookback) - TickSize * 0.5;
                bool atRecentLow  = Lows[0][barsAgo]  <= LowestLow(barsAgo + 1, ExcessLookback)  + TickSize * 0.5;

                // El exceso debe ser una anomalia FUERTE (no cualquier empuje): magnitud unificada por
                // encima del mismo nivel que usa el filtro guia. Asi solo se marca en extremos que importan.
                bool excessStrong = d.AnomalyMagnitude >= d.EffectiveAnomalyLevel;

                bool excessBuyEvent  = (speedAnomaly || extremeSpeed) && buyFlow  && buyConfirmed  && atRecentHigh && excessStrong;
                bool excessSellEvent = (speedAnomaly || extremeSpeed) && sellFlow && sellConfirmed && atRecentLow  && excessStrong;

                // Fase 1: nuevo exceso (agresion en un extremo) => marcar pendiente (anticipacion) y dibujar.
                // Filtros anti-spam:
                //   - NO re-marcar si ya hay un exceso pendiente sin resolver (evita estampar cada barra
                //     en una tendencia: el origen del exceso de marcadores EXB/EXS).
                //   - Espaciar los marcadores por MinimumBarsBetweenSignals.
                bool excessAllowed = !pendingExcessActive
                                     && (barIndex - lastExcessMarkerBar) >= MinimumBarsBetweenSignals;

                if ((excessBuyEvent || excessSellEvent) && excessAllowed)
                {
                    bool isBuy = excessBuyEvent && !excessSellEvent
                                 ? true
                                 : (excessSellEvent && !excessBuyEvent ? false : d.NetSpeed >= 0.0);
                    StartPendingExcess(barIndex, barsAgo, isBuy, true, d, false);
                    pendingExcessHasMarker = true;   // este pendiente SI dibujo EXB/EXS
                    d.VisualSignalCode = isBuy ? EXC_BUY : EXC_SELL;
                    DrawAnticipationMarker(barsAgo, isBuy ? EXC_BUY : EXC_SELL);
                    lastExcessMarkerBar = barIndex;

                    // El exceso queda pendiente; la reacción posterior confirma continuación o rotación.
                    d.SignalCode = NONE;
                    lastEvaluatedBar = barIndex;
                    return;
                }

                // ══ FASE 2: MOMENTUM ══
                // Agresion anomala FUERA de un extremo con el precio siguiendo al flujo.
                // (1.0.6) Antes esto imprimia ACB/ACS de inmediato y abria un "watch de reversa"
                // que SOLO podia confirmar rotacion. Consecuencia medida en el log: decenas de
                // casos donde el precio recorria 100-150 ticks a favor y el evento expiraba sin
                // decir nada, porque la continuacion estaba bloqueada por diseño. Ahora el
                // momentum es un evento de anticipacion con el mismo ciclo que el exceso:
                // marca MB/MS y espera veredicto -> continuacion, rotacion o expiracion.
                double adaptiveEfficiencyTrigger = CurrentAdaptiveEfficiencyTrigger(d);
                bool priceFollowsFlow = !UseEfficiencyTrigger || d.Efficiency >= adaptiveEfficiencyTrigger;

                // UN EVENTO A LA VEZ. Un pendiente vivo —exceso o momentum— posee su ventana:
                // ningun evento de anticipacion nuevo puede abrirse mientras otro espera
                // veredicto. Esto es lo que garantiza "una anticipacion, una resolucion" y
                // elimina de raiz los marcadores duplicados en velas contiguas.
                // EXCESO y MOMENTUM son mutuamente excluyentes: la fase 1 retorna si dispara,
                // de modo que una agresion en un extremo nunca se etiqueta como momentum.
                bool momentumAllowed = !pendingExcessActive
                                       && (barIndex - lastExcessMarkerBar) >= MinimumBarsBetweenSignals;

                bool momentumBuy = buyAcceleration
                                   && buyConfirmed
                                   && speedAnomaly
                                   && rangeTicks >= MinimumRangeTicks
                                   && priceFollowsFlow;

                bool momentumSell = sellAcceleration
                                    && sellConfirmed
                                    && speedAnomaly
                                    && rangeTicks >= MinimumRangeTicks
                                    && priceFollowsFlow;

                if ((momentumBuy || momentumSell) && momentumAllowed
                    && PassesAnomalyGuideFilter(barIndex, d, momentumBuy ? MOM_BUY : MOM_SELL))
                {
                    bool isBuy = momentumBuy && !momentumSell
                                 ? true
                                 : (momentumSell && !momentumBuy ? false : d.NetSpeed >= 0.0);

                    StartPendingExcess(barIndex, barsAgo, isBuy, true, d, true);
                    pendingExcessHasMarker = true;
                    d.VisualSignalCode = isBuy ? MOM_BUY : MOM_SELL;
                    DrawAnticipationMarker(barsAgo, isBuy ? MOM_BUY : MOM_SELL);
                    lastExcessMarkerBar = barIndex;

                    d.SignalCode = NONE;
                    lastEvaluatedBar = barIndex;
                }
            }
            finally
            {
                // Una sola muestra por barra cerrada, siempre despues de decidir su senal.
                // Los returns internos tambien pasan por aqui, evitando huecos de estado.
                RecordAnomalyLevelSample(barIndex);
            }
        }

        // Mapea un codigo de senal a su etiqueta/brush/posicion. Centraliza el visual para
        // que el path normal y el path diferido (exceso->rotacion/continuacion) sean identicos.
        private void MapSignalVisual(int code, out string label, out Brush brush, out bool drawAbove)
        {
            switch (code)
            {
                case ROT_BUY:   label = RotationBuyLabel;  brush = RotationBuyBrush;  drawAbove = false; break;
                case ROT_SELL:  label = RotationSellLabel; brush = RotationSellBrush; drawAbove = true;  break;
                case ACEL_BUY:  label = AcelBuyLabel;      brush = AcelBuyBrush;      drawAbove = false; break;
                case ACEL_SELL: label = AcelSellLabel;     brush = AcelSellBrush;     drawAbove = true;  break;
                // MOMENTUM es DIRECCIONAL, no de agotamiento: la flecha apunta hacia donde
                // empuja el flujo. Compra -> flecha arriba con la etiqueta debajo de la vela;
                // venta -> flecha abajo con la etiqueta encima. Es la convencion opuesta a la
                // del EXCESO, donde la flecha señala el agotamiento en el extremo (agresion
                // compradora que muere en el maximo => flecha hacia abajo).
                case MOM_BUY:   label = MomentumBuyLabel;  brush = MomentumBuyBrush;  drawAbove = false; break;
                case MOM_SELL:  label = MomentumSellLabel; brush = MomentumSellBrush; drawAbove = true;  break;
                case MOM_BUY_EXPIRED:
                    label = MomentumBuyLabel;
                    brush = expiredMomentumBuyBrush != null ? expiredMomentumBuyBrush : MomentumBuyBrush;
                    drawAbove = false;
                    break;
                case MOM_SELL_EXPIRED:
                    label = MomentumSellLabel;
                    brush = expiredMomentumSellBrush != null ? expiredMomentumSellBrush : MomentumSellBrush;
                    drawAbove = true;
                    break;
                case EXC_BUY:   label = ExcessBuyLabel;    brush = ExcessBuyBrush;    drawAbove = true;  break;
                case EXC_SELL:  label = ExcessSellLabel;   brush = ExcessSellBrush;   drawAbove = false; break;
                case EXC_BUY_EXPIRED:
                    label = ExcessBuyLabel;
                    brush = expiredExcessBuyBrush != null ? expiredExcessBuyBrush : ExcessBuyBrush;
                    drawAbove = true;
                    break;
                case EXC_SELL_EXPIRED:
                    label = ExcessSellLabel;
                    brush = expiredExcessSellBrush != null ? expiredExcessSellBrush : ExcessSellBrush;
                    drawAbove = false;
                    break;
                default:        label = string.Empty;      brush = Brushes.Transparent; drawAbove = false; break;
            }
        }

        // Emisión centralizada: filtro de anomalía, debounce, supresión de repetidos,
        // pintado y dibujo. Las confirmaciones diferidas omiten el filtro Y el debounce:
        // su validez proviene del modelo impulso→respuesta (precio), no del histograma,
        // y su pendiente se consume al resolverse — bloquearlas dejaba marcadores
        // EXB/EXS sin resolución visible.
        private void EmitSignal(int barIndex, int barsAgo, BarFlowData d, int code, string label, Brush brush, bool drawAbove, bool isDeferred)
        {
            lastEvaluatedBar = barIndex;

            if (code == NONE)
            {
                d.SignalCode = NONE;
                return;
            }

            if (!isDeferred && !PassesAnomalyGuideFilter(barIndex, d, code))
            {
                d.SignalCode = NONE;
                return;
            }

            if (!isDeferred && MinimumBarsBetweenSignals > 0 && barIndex - lastSignalBar < MinimumBarsBetweenSignals)
            {
                d.SignalCode = NONE;
                return;
            }

            if (!isDeferred && SuppressRepeatedSameSignal && code == lastSignalCode && barIndex - lastSignalBar <= Math.Max(1, MinimumBarsBetweenSignals + 2))
            {
                d.SignalCode = NONE;
                return;
            }

            d.SignalCode = code;
            d.VisualSignalCode = code;
            lastSignalBar = barIndex;
            lastSignalCode = code;

            if (!isDeferred)
            {
                anomalyGateArmed = false;
                anomalyReArmFloor = EffectiveAnomalyLevelForBar(barIndex, d) * ANOMALY_REARM_FRACTION;
            }

            PaintSignalBarOnly(barsAgo, code, brush);
            DrawSignal(barsAgo, code, label, brush, drawAbove);
        }

        // Fase 1: registra/refresca el impulso pendiente. Ademas de los extremos,
        // congela la fuerza causal y una escala robusta de respuesta. De esta forma la
        // clasificacion posterior no depende de un numero fijo de ticks ni usa informacion futura.
        private void StartPendingExcess(int barIndex, int barsAgo, bool isBuy, bool allowContinuation, BarFlowData sourceData, bool isMomentum)
        {
            double hi = Highs[0][barsAgo];
            double lo = Lows[0][barsAgo];

            // ── PENDIENTE INVIOLABLE ──
            // Un impulso del MISMO lado extiende el evento (fusion de extremos) pero NO lo
            // re-ancla: re-anclar perdia el extremo verdadero del exceso original y reiniciaba
            // la ventana, de modo que el veredicto podia no llegar nunca.
            if (pendingExcessActive && pendingExcessIsBuy == isBuy)
            {
                if (hi > pendingExcessHigh) pendingExcessHigh = hi;
                if (lo < pendingExcessLow)  pendingExcessLow = lo;
                pendingExcessEventRange = Math.Max(TickSize, pendingExcessHigh - pendingExcessLow);

                if (sourceData != null && sourceData.AnomalyMagnitude > pendingExcessEventMagnitude)
                    pendingExcessEventMagnitude = sourceData.AnomalyMagnitude;

                // La fusion NO reescribe pendingExcessBaseRange: los umbrales siguen midiendose
                // contra el evento que origino el pendiente.

                // Si el evento original admitia continuacion, la sigue admitiendo.
                pendingAllowContinuation = pendingAllowContinuation || allowContinuation;

                if (DebugMode)
                    Print("   [FUSION] b" + barIndex + " extiende el exceso "
                        + (isBuy ? "compra" : "venta") + " -> rango "
                        + (pendingExcessEventRange / Math.Max(1e-10, TickSize)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " ticks");
                return;
            }

            // Un pendiente vivo del lado CONTRARIO no puede ser pisado: su veredicto manda.
            // La agresion opuesta ES la respuesta que ese pendiente esta esperando.
            if (pendingExcessActive)
            {
                if (DebugMode)
                    Print("   [PROTEGIDO] b" + barIndex + " impulso "
                        + (isBuy ? "compra" : "venta") + " no pisa el pendiente contrario vivo");
                return;
            }

            pendingExcessActive       = true;
            pendingExcessIsBuy        = isBuy;
            pendingExcessHigh         = hi;
            pendingExcessLow          = lo;
            pendingExcessEventRange   = Math.Max(TickSize, hi - lo);
            pendingExcessBaseRange    = pendingExcessEventRange;
            pendingExcessHasMarker    = false;   // lo activa el llamador si dibuja marcador
            pendingExcessIsMomentum   = isMomentum;
            pendingExcessBarIndex     = barIndex;
            pendingExcessLastBar      = barIndex;
            pendingExcessBarsLeft     = Math.Max(1, ExcessConfirmWindow);
            pendingAllowContinuation  = allowContinuation;

            pendingExcessEventMagnitude = sourceData == null ? 0.0 : sourceData.AnomalyMagnitude;
            pendingExcessEventLevel = sourceData == null
                ? Math.Max(1.0, CurrentAnomalyLevel(barIndex))
                : Math.Max(1.0, EffectiveAnomalyLevelForBar(barIndex, sourceData));

            double eventRangeTicks = Math.Max(1.0, pendingExcessEventRange / Math.Max(1e-10, TickSize));
            pendingResponseScaleTicks = ComputeAdaptiveResponseScaleTicks(barIndex, eventRangeTicks);
            pendingBestFavorableTicks = 0.0;
            pendingContinuationEvidenceBars = 0;
            pendingRotationEvidenceBars = 0;

            if (DebugMode)
                Print("── [PENDIENTE] b" + barIndex
                    + (isMomentum ? " MOMENTUM " : " EXCESO ") + (isBuy ? "COMPRA" : "VENTA")
                    + " | rango " + eventRangeTicks.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " ticks"
                    + " | M " + pendingExcessEventMagnitude.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                    + "/" + pendingExcessEventLevel.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                    + " | ventana " + pendingExcessBarsLeft + " barras"
                    + " | continuacion " + (allowContinuation ? "permitida" : "bloqueada"));
        }

        // Fase 2: clasificador adaptativo IMPULSO -> RESPUESTA.
        //
        // El evento de flujo es el impulso. La respuesta se mide causalmente con tres piezas:
        //   1) trabajo de precio: desplazamiento firmado normalizado por volatilidad local;
        //   2) eficiencia del cierre respecto a la direccion ORIGINAL del impulso;
        //   3) persistencia o inversion del flujo posterior.
        //
        // CONTINUACION/ACEL: hay aceptacion mas alla del extremo, cierre eficiente y flujo compatible.
        // ROTACION: hay desplazamiento adverso suficiente, rechazo del impulso y/o flujo opuesto.
        // Las distancias se recalibran con el rango del evento + mediana de rangos recientes.
        private int ResolvePendingExcess(int barIndex, int barsAgo)
        {
            if (!pendingExcessActive)
                return NONE;

            if (barIndex <= pendingExcessBarIndex)
                return NONE;

            double tick = Math.Max(1e-10, TickSize);
            double c = Closes[0][barsAgo];
            double hi = Highs[0][barsAgo];
            double lo = Lows[0][barsAgo];
            double o = Opens[0][barsAgo];
            double barRange = Math.Max(tick, hi - lo);
            double rangeTicks = barRange / tick;
            double bodyTicks = (c - o) / tick;
            double closeLocation = Clamp((c - lo) / barRange, 0.0, 1.0);

            double originalDirection = pendingExcessIsBuy ? 1.0 : -1.0;
            double priceAlignment = originalDirection * ComputePriceFollow(bodyTicks, rangeTicks, closeLocation);

            // Alineacion del flujo actual respecto al impulso original. No se exige que la
            // vela de confirmacion sea otra anomalia; por eso se usa una mezcla suave -1..+1.
            BarFlowData currentData = GetBarData(barIndex, false);
            double flowAlignment = 0.0;
            if (currentData != null && currentData.Samples > 0)
            {
                double dirZ = Math.Tanh(currentData.NetSpeedZ);
                double dirImb = MinimumImbalance <= 0.0
                    ? 0.0
                    : Clamp(currentData.Imbalance / MinimumImbalance, -1.0, 1.0);
                flowAlignment = originalDirection * Clamp(0.60 * dirZ + 0.40 * dirImb, -1.0, 1.0);
            }

            // (1.0.5) Geometria de referencia = evento ORIGINAL, con un techo de crecimiento
            // del 25%. La fusion puede extender los extremos, pero un exceso que nacio con 40
            // ticks se sigue juzgando contra ~40 ticks, no contra el tramo entero de tendencia.
            double baseRange = pendingExcessBaseRange > 0.0 ? pendingExcessBaseRange : pendingExcessEventRange;
            double geometryRange = Math.Min(pendingExcessEventRange, baseRange * 1.25);
            double eventRangeTicks = Math.Max(1.0, geometryRange / tick);
            double liveScaleTicks = ComputeAdaptiveResponseScaleTicks(barIndex, eventRangeTicks);
            pendingResponseScaleTicks = pendingResponseScaleTicks <= 0.0
                ? liveScaleTicks
                : 0.75 * pendingResponseScaleTicks + 0.25 * liveScaleTicks;

            // ══ VEREDICTO ANCLADO A LA GEOMETRIA DEL EVENTO ══
            // La referencia rectora es el RANGO DEL EXCESO, no una escala EMA de volatilidad.
            // Motivo: en graficos Range todas las barras miden lo mismo, asi que la escala
            // "adaptativa" degenera en una constante y producia una asimetria ~3:1 (rotacion
            // exigia ~110% de escala; continuacion, ~30%) que ahogaba los giros. Medir contra
            // el propio evento es simetrico, determinista y explicable en terminos de subasta:
            //   continuacion     -> el precio ACEPTA mas alla del extremo del exceso.
            //   rotacion estandar-> el precio vuelve mas alla del PUNTO MEDIO del evento.
            //   rotacion fuerte  -> el precio cierra mas alla del extremo OPUESTO (rechazo total).
            double strengthRatio = Clamp(
                pendingExcessEventMagnitude / Math.Max(1.0, pendingExcessEventLevel),
                0.75, 1.75);
            double strength01 = strengthRatio - 0.75; // 0..1

            // Un impulso mas fuerte necesita algo menos de recorrido extra para probarse.
            double geometryEase = 1.0 - 0.10 * strength01;

            // Piso anti-ruido: la escala adaptativa queda como guarda secundaria, para que
            // un evento diminuto no dispare veredictos con 1 tick de desplazamiento.
            double noiseFloorTicks = Clamp(
                pendingResponseScaleTicks * 0.15,
                1.0,
                Math.Max(1.0, eventRangeTicks * 0.50));

            // (1.0.7) 0.75 -> 1.50. Medicion sobre 834 veredictos reales: con 0.75 el umbral
            // no filtraba nada. En Range 40 toda vela recorre 40 ticks por construccion, asi
            // que 0.75 x 40 = 30 ticks los cumplia cualquier vela que cerrara en la direccion
            // del impulso; el 10% mas marginal aun superaba el umbral por un 8%. El veredicto
            // se habia degradado a "¿la vela siguiente cerro para el mismo lado?".
            // (1.0.8) 1.50 -> 1.20. El 1.50 funciono (continuacion 42-57%, margen 1.21-1.27)
            // pero era mas duro de lo necesario. 1.20 conserva la exigencia de aceptacion real
            // y deja algo mas de aire a la resolucion en la vela 2.
            double continuationTicks = Math.Max(noiseFloorTicks, eventRangeTicks * 1.20 * geometryEase);
            // (1.0.7) La rotacion escala en la misma proporcion (x2) para que ambos veredictos
            // exijan una prueba comparable. Sin esto, endurecer solo la continuacion inclinaria
            // la balanza hacia la rotacion y el criterio volveria a ser poco informativo, solo
            // que del otro lado: la medicion mostro la rotacion igual de holgada (margen
            // mediano 1.5-1.6x sobre su umbral).
            double rotationMidTicks  = Math.Max(noiseFloorTicks, eventRangeTicks * 1.00 * geometryEase);
            double rotationFullTicks = Math.Max(noiseFloorTicks * 2.0, eventRangeTicks * 2.00 * geometryEase);

            double favorableCloseTicks;
            double adverseCloseTicks;
            double favorableExcursionTicks;

            if (pendingExcessIsBuy)
            {
                favorableCloseTicks = (c - pendingExcessHigh) / tick;
                adverseCloseTicks = (pendingExcessHigh - c) / tick;
                favorableExcursionTicks = (hi - pendingExcessHigh) / tick;
            }
            else
            {
                favorableCloseTicks = (pendingExcessLow - c) / tick;
                adverseCloseTicks = (c - pendingExcessLow) / tick;
                favorableExcursionTicks = (pendingExcessLow - lo) / tick;
            }

            favorableCloseTicks = Math.Max(0.0, favorableCloseTicks);
            adverseCloseTicks = Math.Max(0.0, adverseCloseTicks);
            pendingBestFavorableTicks = Math.Max(pendingBestFavorableTicks, Math.Max(0.0, favorableExcursionTicks));

            double continuationDisplacement = Clamp(favorableCloseTicks / Math.Max(1.0, continuationTicks), 0.0, 1.0);
            double rotationDisplacement = Clamp(adverseCloseTicks / Math.Max(1.0, rotationMidTicks), 0.0, 1.0);

            double continuationScore =
                  0.60 * continuationDisplacement
                + 0.25 * Math.Max(0.0, priceAlignment)
                + 0.15 * Math.Max(0.0, flowAlignment);

            double rotationScore =
                  0.60 * rotationDisplacement
                + 0.25 * Math.Max(0.0, -priceAlignment)
                + 0.15 * Math.Max(0.0, -flowAlignment);

            // Una excursion favorable sin aceptacion (mecha que supera el extremo pero
            // vuelve dentro) es evidencia adicional de subasta fallida, no de continuacion.
            bool failedFavorableAuction = pendingBestFavorableTicks >= continuationTicks
                && favorableCloseTicks < continuationTicks * 0.25
                && priceAlignment < 0.0;
            if (failedFavorableAuction)
                rotationScore = Math.Min(1.0, rotationScore + 0.08);

            // ── Via rapida: RESPUESTA AGRESIVA contra el impulso ──
            // Si la vela de respuesta tiene, por si sola, calidad de senal en direccion
            // contraria (velocidad anomala + aceleracion en contra + flujo y precio
            // opuestos), en subasta eso ES el rechazo del extremo. No se le exige el
            // desplazamiento completo pensado para respuestas pasivas que derivan:
            // basta media distancia de rotacion (piso 2 ticks) ya recorrida al cierre.
            // Sin esta via, una V violenta nunca alcanzaba el umbral pleno a tiempo y
            // la vela de giro se perdia (o, antes del gate de arbitraje, salia ACB/ACS).
            bool aggressiveOppositeResponse = false;
            if (currentData != null && currentData.Samples > 0)
            {
                bool oppSpeedAnomaly = currentData.SpeedZ >= SpeedZThreshold
                                       || currentData.AbsNetSpeedZ >= SpeedZThreshold;
                bool oppAcceleration = (originalDirection * currentData.AccelerationZ) <= -AccelerationZThreshold;

                aggressiveOppositeResponse = oppSpeedAnomaly
                    && oppAcceleration
                    && flowAlignment <= -0.35
                    && priceAlignment <= -0.10
                    && rangeTicks >= MinimumRangeTicks;
            }

            bool rotationFastPath = aggressiveOppositeResponse
                && adverseCloseTicks >= Math.Max(2.0, rotationMidTicks * 0.70);

            bool continuationEvidence = pendingAllowContinuation
                && favorableCloseTicks >= continuationTicks * 0.60
                && priceAlignment >= -0.05
                && continuationScore >= 0.52;

            bool rotationEvidence = adverseCloseTicks >= rotationMidTicks * 0.60
                && priceAlignment <= 0.05
                && rotationScore >= 0.52;

            pendingContinuationEvidenceBars = continuationEvidence
                ? pendingContinuationEvidenceBars + 1
                : Math.Max(0, pendingContinuationEvidenceBars - 1);
            pendingRotationEvidenceBars = rotationEvidence
                ? pendingRotationEvidenceBars + 1
                : Math.Max(0, pendingRotationEvidenceBars - 1);

            // CONTINUACION: aceptacion mas alla del extremo del evento en la direccion
            // del impulso, con el precio o el flujo acompanando.
            bool continuationConfirmed = pendingAllowContinuation
                && favorableCloseTicks >= continuationTicks
                && continuationScore >= 0.55
                && (priceAlignment >= 0.05 || flowAlignment >= 0.15 || pendingContinuationEvidenceBars >= 2);

            // ROTACION FUERTE: el cierre supera el extremo opuesto del evento. La subasta
            // entera quedo rechazada; no necesita corroboracion adicional.
            bool rotationStrong = adverseCloseTicks >= rotationFullTicks && priceAlignment <= 0.0;

            // ROTACION ESTANDAR: el cierre vuelve mas alla del punto medio del evento y
            // ademas el precio o el flujo confirman el rechazo.
            bool rotationStandard = adverseCloseTicks >= rotationMidTicks
                && rotationScore >= 0.55
                && (priceAlignment <= -0.05 || flowAlignment <= -0.15 || pendingRotationEvidenceBars >= 2);

            // VIA RAPIDA: respuesta agresiva (ver arriba) con menos recorrido, porque el
            // flujo aporta la evidencia que al desplazamiento aun le falta.
            bool rotationConfirmed = rotationStrong || rotationStandard || rotationFastPath;

            // ══ (1.0.7) PERSISTENCIA DE LA ACEPTACION ══
            // Medicion: el 75-82% de los veredictos se dictaban en la vela INMEDIATA. En teoria
            // de subasta la aceptacion es TIEMPO en el precio, no el cierre de una sola vela.
            // En la primera vela de respuesta solo se admite un desenlace DECISIVO; cualquier
            // lectura marginal espera a que el mercado la sostenga al menos una vela mas.
            // Un movimiento realmente decisivo no necesita esperar: eso ya es informacion.
            int responseOrdinal = Math.Max(1, ExcessConfirmWindow) - pendingExcessBarsLeft + 1;
            if (responseOrdinal <= 1)
            {
                // ── EMPUJE DE VELA UNICA ──
                // (1.0.8) Limite fisico: en un grafico Range la vela de respuesta abarca un
                // rango fijo, asi que su cierre no puede alejarse del extremo del evento mas
                // de ~1.0 rango. Todo umbral >= 1.0 obliga a dos velas POR GEOMETRIA, no por
                // criterio. Para que la vela inmediata pueda resolver hay que juzgarla por lo
                // que si puede demostrar: recorrer casi todo su rango mas alla del extremo Y
                // hacerlo con cuerpo limpio, cierre pegado a su extremo y flujo acompañando.
                // Eso es aceptacion inequivoca en una sola vela; cualquier cosa menor espera.
                // (1.0.9) Endurecido tras medir 1052 veredictos: con 0.85 / 0.65 / 0.70 esta
                // via absorbia el 55-62% de las continuaciones y el margen mediano cayo a 0.90,
                // es decir por DEBAJO del umbral de distancia. Habia dejado de ser la excepcion
                // decisiva para convertirse en el camino principal. Ahora exige un recorrido
                // practicamente maximo para una sola vela (0.95 del rango) con cuerpo muy
                // limpio: el desenlace inequivoco que se pretendia, no una vela buena.
                bool continuationSingleBarDrive = pendingAllowContinuation
                    && favorableCloseTicks >= eventRangeTicks * 0.95 * geometryEase
                    && priceAlignment >= 0.80
                    && flowAlignment >= 0.25
                    && continuationScore >= 0.78;

                if (RequireSustainedAcceptance)
                {
                    // Via alternativa por distancia, util solo si el umbral se baja de 1.0.
                    bool continuationDecisive = favorableCloseTicks >= continuationTicks * 1.35
                                                && continuationScore >= 0.62;
                    continuationConfirmed = continuationConfirmed && continuationDecisive;

                    // Para la rotacion, "decisivo" ya esta definido: cierre mas alla del extremo
                    // opuesto, o respuesta agresiva con el flujo probando el rechazo.
                    rotationConfirmed = rotationStrong || rotationFastPath;
                }

                continuationConfirmed = continuationConfirmed || continuationSingleBarDrive;
            }

            if (DebugMode)
            {
                Print("   [RESP] b" + barIndex
                    + " fav=" + favorableCloseTicks.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                    + "/" + continuationTicks.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                    + " adv=" + adverseCloseTicks.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                    + "/" + rotationMidTicks.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                    + "|" + rotationFullTicks.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                    + " pAlign=" + priceAlignment.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                    + " fAlign=" + flowAlignment.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                    + " sC=" + continuationScore.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                    + " sR=" + rotationScore.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                    + (rotationStrong ? " [ROT-FUERTE]" : "")
                    + (rotationStandard ? " [ROT-ESTANDAR]" : "")
                    + (rotationFastPath ? " [ROT-RAPIDA]" : "")
                    + (continuationConfirmed ? " [CONT]" : "")
                    + " restan=" + pendingExcessBarsLeft);
            }

            // La rotacion fuerte manda sobre cualquier lectura de continuacion.
            if (continuationConfirmed && !rotationStrong
                && (!rotationConfirmed || continuationScore >= rotationScore))
            {
                pendingExcessActive = false;
                if (pendingExcessIsMomentum) statMomCont++; else statExcCont++;
                if (DebugMode)
                    Print("   >>> VEREDICTO: CONTINUACION (" + (pendingExcessIsBuy ? "CB" : "CS")
                        + ") en b" + barIndex + " [" + (pendingExcessIsMomentum ? "MOM" : "EXC") + "]");
                return pendingExcessIsBuy ? ACEL_BUY : ACEL_SELL;
            }

            if (rotationConfirmed)
            {
                pendingExcessActive = false;
                if (pendingExcessIsMomentum) statMomRot++; else statExcRot++;
                if (DebugMode)
                    Print("   >>> VEREDICTO: ROTACION (" + (pendingExcessIsBuy ? "RS" : "RB")
                        + ") en b" + barIndex + " [" + (pendingExcessIsMomentum ? "MOM" : "EXC") + "]");
                return pendingExcessIsBuy ? ROT_SELL : ROT_BUY;
            }

            if (barIndex > pendingExcessLastBar)
            {
                pendingExcessLastBar = barIndex;
                pendingExcessBarsLeft--;
                if (pendingExcessBarsLeft <= 0)
                {
                    // Veredicto obligatorio: si la ventana se agota sin resolucion, el
                    // marcador no queda "vivo" para siempre; se atenua y queda auditado.
                    ExpirePendingExcessMarker();
                    pendingExcessActive = false;
                }
            }

            return NONE;
        }

        // Atenua el marcador EXB/EXS cuyo pendiente expiro sin veredicto. El evento existio
        // (por eso no se borra), pero el operador debe distinguir de un vistazo un exceso
        // resuelto de uno que el mercado nunca confirmo ni rechazo.
        private void ExpirePendingExcessMarker()
        {
            if (!pendingExcessActive || pendingExcessBarIndex < 0)
                return;

            // Guarda: desde 1.0.6 todo pendiente nace de un evento de anticipacion con
            // marcador dibujado (EXB/EXS o MB/MS), pero se conserva la comprobacion para no
            // atenuar jamas una barra que no dibujo nada.
            if (!pendingExcessHasMarker)
                return;

            int primaryLast = CurrentBars[0];
            int barsAgo = primaryLast - pendingExcessBarIndex;
            if (barsAgo < 0 || barsAgo > primaryLast)
                return;

            int liveCode;
            int expiredCode;
            if (pendingExcessIsMomentum)
            {
                liveCode    = pendingExcessIsBuy ? MOM_BUY : MOM_SELL;
                expiredCode = pendingExcessIsBuy ? MOM_BUY_EXPIRED : MOM_SELL_EXPIRED;
            }
            else
            {
                liveCode    = pendingExcessIsBuy ? EXC_BUY : EXC_SELL;
                expiredCode = pendingExcessIsBuy ? EXC_BUY_EXPIRED : EXC_SELL_EXPIRED;
            }

            BarFlowData ev = GetBarData(pendingExcessBarIndex, false);
            if (ev != null)
                ev.VisualSignalCode = expiredCode;

            RemoveSignalDrawings(pendingExcessBarIndex, liveCode);

            string label;
            Brush brush;
            bool drawAbove;
            MapSignalVisual(expiredCode, out label, out brush, out drawAbove);
            PaintSignalBarOnly(barsAgo, expiredCode, brush);
            DrawSignal(barsAgo, expiredCode, label, brush, drawAbove);

            if (pendingExcessIsMomentum) statMomExp++; else statExcExp++;
            if (DebugMode)
                Print("   >>> VEREDICTO: EXPIRADO sin resolucion ("
                    + (pendingExcessIsMomentum ? "momentum " : "exceso ")
                    + (pendingExcessIsBuy ? "compra" : "venta") + " en b" + pendingExcessBarIndex + ")");
        }

        // Elimina los dibujos de una senal concreta (mismo esquema de tags que DrawSignal).
        private void RemoveSignalDrawings(int absoluteBar, int code)
        {
            string tagBase = "ILT_FLOWSPEED_" + absoluteBar + "_" + code;
            RemoveDrawObject(tagBase + "_ARW");
            RemoveDrawObject(tagBase + "_TXT");
        }

        // Fase 1 visible: marca el exceso PENDIENTE pintando la vela (naranja=compra / gold=venta)
        // con su etiqueta EXB/EXS y flecha. Es anticipacion: indica donde ocurrio el exceso; la
        // rotacion/continuacion (R/A) llegara despues en la vela que el precio confirme.
        // Dibuja un marcador de ANTICIPACION (exceso o momentum) en su barra.
        private void DrawAnticipationMarker(int barsAgo, int code)
        {
            string label;
            Brush brush;
            bool drawAbove;
            MapSignalVisual(code, out label, out brush, out drawAbove);

            PaintSignalBarOnly(barsAgo, code, brush);
            DrawSignal(barsAgo, code, label, brush, drawAbove);
        }

        #region Render (histograma 3D al ancho de la vela)

        // El histograma ancho-vela se renderiza de forma personalizada y su Plot nativo puede
        // permanecer transparente. Por eso no dejamos el rango Y al autoescalado implícito de
        // NinjaTrader: en determinadas vistas, zoom/F5 o con pocas barras no-cero, el eje podía
        // calcular un rango menor que los valores realmente dibujados y recortar el histograma.
        //
        // Se usa una escala SIMETRICA alrededor de cero porque el oscilador representa presión
        // compradora/vendedora equivalente. El margen del 10% deja espacio para glow, tapa 3D y
        // etiquetas del eje sin alterar los valores ni la lógica de señales.
        public override void OnCalculateMinMax()
        {
            CalculateFlowSpeedMinMax();
        }

        public override void OnRenderTargetChanged()
        {
            // Todo recurso Direct2D pertenece al RenderTarget que lo creó. Resize, hit-test,
            // cambio de monitor/GPU o recreación del ChartControl invalida esos recursos.
            DisposeHistogramBrushes();
            DisposeFlowSpeedRenderResources();
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            // Fallback de UI: algunos workspaces crean ChartControl después de State.Historical.
            if (flowSpeedToolbarMenu == null || flowSpeedPanelRoot == null)
                QueueBuildFlowSpeedToolbarMenu();

            // No usamos IsVisible=false porque NinjaTrader también detendría el cálculo.
            if (!ShowHistogramPanel)
            {
                DisposeHistogramBrushes();
                DisposeFlowSpeedRenderResources();
                return;
            }

            // No llamamos base.OnRender: una plantilla antigua puede restaurar los plots verde/rojo
            // y la línea E aunque sus defaults sean transparentes. Así se garantiza que el panel
            // visible contenga exclusivamente High/Low/Mean del Flow Speed clásico.
            try
            {
                RenderFlowSpeed(chartControl, chartScale);
            }
            catch (Exception ex)
            {
                DisposeFlowSpeedRenderResources();
                if (DebugMode)
                    try { Print("ILT FLOWSPEED: error de render Flow Speed clásico. " + ex.GetType().Name + ": " + ex.Message); }
                    catch { }
            }
        }

        // Dibuja las barras del histograma con un ANCHO proporcional al espaciado entre velas
        // (se adapta al zoom, como el cuerpo de la vela). Usa el valor cacheado por barra
        // (VisualHistogramValue, con signo). Verde = compra, rojo = venta. Aislado en try/catch.
        // ── Recursos cacheados del histograma (optimizacion: se crean una vez por RenderTarget,
        // no por frame -> evita chartlag). Misma tecnica "glass 3D" del ChartStyle ILT_VELAS_3D:
        // gradiente vertical (flash arriba -> base -> sombra abajo) + glow en anomalias fuertes +
        // bisel (rim claro arriba/izq, rim oscuro abajo/der) ─────────────────────────────────
        private SharpDX.Direct2D1.RenderTarget histBrushTarget;
        private SharpDX.Direct2D1.GradientStopCollection hgBuyStops, hgSellStops;
        private SharpDX.Direct2D1.LinearGradientBrush hgBuyGradient, hgSellGradient;
        private SharpDX.Direct2D1.SolidColorBrush hbBuyBevelLight, hbBuyBevelDark;
        private SharpDX.Direct2D1.SolidColorBrush hbSellBevelLight, hbSellBevelDark;
        private SharpDX.Direct2D1.SolidColorBrush hbBuyGlow1, hbBuyGlow2;
        private SharpDX.Direct2D1.SolidColorBrush hbSellGlow1, hbSellGlow2;
        private SharpDX.Direct2D1.SolidColorBrush hbBuySolid, hbSellSolid;
        private SharpDX.Direct2D1.SolidColorBrush hbCap;

        private static SharpDX.Color4 ToHistColor4(SharpDX.Color c)
        {
            return new SharpDX.Color4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
        }

        private static SharpDX.Color LerpHistColor(SharpDX.Color from, SharpDX.Color to, float t)
        {
            return new SharpDX.Color(
                (byte)(from.R + (to.R - from.R) * t),
                (byte)(from.G + (to.G - from.G) * t),
                (byte)(from.B + (to.B - from.B) * t),
                (byte)255);
        }

        private static SharpDX.Direct2D1.GradientStopCollection BuildGlassStops(SharpDX.Direct2D1.RenderTarget rt, SharpDX.Color baseC)
        {
            // Misma curva de 7 paradas que ILT_VELAS_3D (flash superior -> nucleo -> sombra inferior).
            var white = new SharpDX.Color((byte)255, (byte)255, (byte)255, (byte)255);
            var black = new SharpDX.Color((byte)0, (byte)0, (byte)0, (byte)255);

            var stops = new[]
            {
                new SharpDX.Direct2D1.GradientStop { Position = 0.00f, Color = ToHistColor4(LerpHistColor(baseC, white, 0.62f)) },
                new SharpDX.Direct2D1.GradientStop { Position = 0.12f, Color = ToHistColor4(LerpHistColor(baseC, white, 0.28f)) },
                new SharpDX.Direct2D1.GradientStop { Position = 0.38f, Color = ToHistColor4(LerpHistColor(baseC, white, 0.06f)) },
                new SharpDX.Direct2D1.GradientStop { Position = 0.62f, Color = ToHistColor4(baseC) },
                new SharpDX.Direct2D1.GradientStop { Position = 0.80f, Color = ToHistColor4(LerpHistColor(baseC, black, 0.18f)) },
                new SharpDX.Direct2D1.GradientStop { Position = 0.94f, Color = ToHistColor4(LerpHistColor(baseC, black, 0.54f)) },
                new SharpDX.Direct2D1.GradientStop { Position = 1.00f, Color = ToHistColor4(LerpHistColor(baseC, black, 0.72f)) },
            };
            return new SharpDX.Direct2D1.GradientStopCollection(rt, stops, SharpDX.Direct2D1.Gamma.StandardRgb, SharpDX.Direct2D1.ExtendMode.Clamp);
        }

        private void TryRefreshHistogramRenderSnapshot(int from, int to)
        {
            bool lockTaken = false;
            try
            {
                System.Threading.Monitor.TryEnter(barDataSync, 0, ref lockTaken);
                if (!lockTaken || barData == null)
                    return;

                histogramRenderScratch.Clear();
                for (int idx = from; idx <= to; idx++)
                {
                    BarFlowData d;
                    if (barData.TryGetValue(idx, out d))
                        histogramRenderScratch[idx] = d.VisualHistogramValue;
                }

                histogramRenderSnapshot.Clear();
                foreach (KeyValuePair<int, double> kv in histogramRenderScratch)
                    histogramRenderSnapshot[kv.Key] = kv.Value;
            }
            finally
            {
                if (lockTaken)
                    System.Threading.Monitor.Exit(barDataSync);
            }
        }

        private void EnsureHistogramBrushes()
        {
            if (histBrushTarget == RenderTarget && hgBuyGradient != null)
                return;

            DisposeHistogramBrushes();
            histBrushTarget = RenderTarget;

            var buyBase  = new SharpDX.Color((byte)56,  (byte)196, (byte)72,  (byte)255);
            var sellBase = new SharpDX.Color((byte)220, (byte)52,  (byte)52,  (byte)255);

            hgBuyStops  = BuildGlassStops(RenderTarget, buyBase);
            hgSellStops = BuildGlassStops(RenderTarget, sellBase);
            hgBuyGradient  = new SharpDX.Direct2D1.LinearGradientBrush(RenderTarget,
                new SharpDX.Direct2D1.LinearGradientBrushProperties { StartPoint = new SharpDX.Vector2(0, 0), EndPoint = new SharpDX.Vector2(0, 1) }, hgBuyStops);
            hgSellGradient = new SharpDX.Direct2D1.LinearGradientBrush(RenderTarget,
                new SharpDX.Direct2D1.LinearGradientBrushProperties { StartPoint = new SharpDX.Vector2(0, 0), EndPoint = new SharpDX.Vector2(0, 1) }, hgSellStops);

            hbBuyBevelLight  = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(190, 255, 190, 150));
            hbBuyBevelDark   = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(10, 60, 16, 150));
            hbSellBevelLight = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(255, 190, 190, 150));
            hbSellBevelDark  = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(70, 8, 8, 150));

            hbBuyGlow1  = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(56, 196, 72, 95));
            hbBuyGlow2  = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(56, 196, 72, 45));
            hbSellGlow1 = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(220, 52, 52, 95));
            hbSellGlow2 = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(220, 52, 52, 45));

            hbCap = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(255, 255, 255, 235));

            // Brushes planos de respaldo (1 sola llamada de fill, sin gradiente) para el modo
            // rapido automatico cuando hay demasiadas velas visibles (anti chartlag).
            hbBuySolid  = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, buyBase);
            hbSellSolid = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, sellBase);
        }

        private void DisposeHistogramBrushes()
        {
            if (hgBuyGradient != null)   { hgBuyGradient.Dispose();   hgBuyGradient = null; }
            if (hgSellGradient != null)  { hgSellGradient.Dispose();  hgSellGradient = null; }
            if (hgBuyStops != null)      { hgBuyStops.Dispose();      hgBuyStops = null; }
            if (hgSellStops != null)     { hgSellStops.Dispose();     hgSellStops = null; }
            if (hbBuyBevelLight != null) { hbBuyBevelLight.Dispose(); hbBuyBevelLight = null; }
            if (hbBuyBevelDark != null)  { hbBuyBevelDark.Dispose();  hbBuyBevelDark = null; }
            if (hbSellBevelLight != null){ hbSellBevelLight.Dispose();hbSellBevelLight = null; }
            if (hbSellBevelDark != null) { hbSellBevelDark.Dispose(); hbSellBevelDark = null; }
            if (hbBuyGlow1 != null)      { hbBuyGlow1.Dispose();      hbBuyGlow1 = null; }
            if (hbBuyGlow2 != null)      { hbBuyGlow2.Dispose();      hbBuyGlow2 = null; }
            if (hbSellGlow1 != null)     { hbSellGlow1.Dispose();     hbSellGlow1 = null; }
            if (hbSellGlow2 != null)     { hbSellGlow2.Dispose();     hbSellGlow2 = null; }
            if (hbCap != null)           { hbCap.Dispose();           hbCap = null; }
            if (hbBuySolid != null)      { hbBuySolid.Dispose();      hbBuySolid = null; }
            if (hbSellSolid != null)     { hbSellSolid.Dispose();     hbSellSolid = null; }
            histBrushTarget = null;
        }

        // Dibuja el histograma al ancho de la vela. Nivel de detalle AUTOMATICO (anti chartlag):
        // con pocas velas visibles (vista normal de trading) usa el efecto glass 3D completo;
        // si hay muchas velas en pantalla (zoom-out / arrastre del eje X), cambia solo a relleno
        // plano de 1 llamada por barra, sin gradiente ni bisel, para que nunca se congele el chart.
        // El bisel/glow de anomalia fuerte queda SIEMPRE reservado a esas barras (son pocas),
        // nunca se dibuja en cada vela -> ese era el costo real, no el gradiente en si.
        private void RenderHistogramBarsSafe(ChartControl chartControl, ChartScale chartScale)
        {
            if (!ShowHistogramPanel)
                return;

            if (RenderTarget == null || chartControl == null || chartScale == null || ChartBars == null)
                return;

            EnsureHistogramBrushes();
            if (hgBuyGradient == null)
                return;

            int from = ChartBars.FromIndex;
            int to   = ChartBars.ToIndex;
            if (to < from)
                return;

            // Snapshot no bloqueante: si el motor está actualizando datos justo ahora,
            // conservamos el último snapshot válido y el plot nativo sirve de respaldo visual.
            TryRefreshHistogramRenderSnapshot(from, to);

            int visibleCount = to - from + 1;
            bool use3D = Enable3DHistogram && visibleCount <= Math.Max(1, Histogram3DMaxVisibleBars);

            float yZero = chartScale.GetYByValue(0.0);
            double level = CurrentAnomalyLevel(PrimaryCurrentBarSafe());   // umbral para resaltar anomalias fuertes

            var oldAa = RenderTarget.AntialiasMode;
            RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.Aliased;   // mas barato para muchos rectangulos chicos

            try
            {
                float prevXc = float.NaN;
                float fallbackSpacing = 6f;

                for (int idx = from; idx <= to; idx++)
                {
                    // xc y el bookkeeping de espaciado se calculan SIEMPRE (aunque la barra no se
                    // dibuje), porque idx avanza de 1 en 1: asi idx-1 (prevXc) es exacto sin pagar
                    // una segunda llamada a GetXByBarIndex, sin importar cuantas barras intermedias
                    // tengan valor cero.
                    float xc = chartControl.GetXByBarIndex(ChartBars, idx);
                    float spacing = float.IsNaN(prevXc) ? fallbackSpacing : Math.Abs(xc - prevXc);
                    if (spacing < 1f)
                        spacing = fallbackSpacing;
                    fallbackSpacing = spacing;
                    prevXc = xc;

                    double v;
                    if (!histogramRenderSnapshot.TryGetValue(idx, out v))
                        continue;
                    if (double.IsNaN(v) || Math.Abs(v) < 0.000001)
                        continue;

                    float halfW = spacing * 0.30f;
                    float x0 = xc - halfW;
                    float w = 2f * halfW;

                    float yVal = chartScale.GetYByValue(v);
                    float top = Math.Min(yZero, yVal);
                    float h = Math.Abs(yVal - yZero);
                    if (h < 1f)
                        h = 1f;

                    bool buy = v >= 0.0;
                    bool isAnomaly = level > 0.0 && Math.Abs(v) >= level;
                    var rect = new SharpDX.RectangleF(x0, top, w, h);

                    if (use3D)
                    {
                        // Glow suave SOLO en anomalias fuertes: el evento real "salta" del resto
                        // del flujo normal, sin pagar el costo en cada barra.
                        if (isAnomaly)
                        {
                            var glow1 = buy ? hbBuyGlow1 : hbSellGlow1;
                            var glow2 = buy ? hbBuyGlow2 : hbSellGlow2;
                            RenderTarget.FillRectangle(new SharpDX.RectangleF(x0 - 3.8f, top - 3.8f, w + 7.6f, h + 7.6f), glow2);
                            RenderTarget.FillRectangle(new SharpDX.RectangleF(x0 - 1.8f, top - 1.8f, w + 3.6f, h + 3.6f), glow1);
                        }

                        // Cuerpo: gradiente vertical "glass" (misma tecnica que las velas 3D del chart).
                        var gradient = buy ? hgBuyGradient : hgSellGradient;
                        gradient.StartPoint = new SharpDX.Vector2(x0 + w * 0.5f, top);
                        gradient.EndPoint   = new SharpDX.Vector2(x0 + w * 0.5f, top + h);
                        RenderTarget.FillRectangle(rect, gradient);

                        // Bisel: SOLO en anomalias fuertes (son pocas) -> el relieve resalta
                        // justo donde importa, sin el costo de 4 lineas por cada vela del flujo normal.
                        if (isAnomaly && w >= 2f && h >= 2f)
                        {
                            var bevelLight = buy ? hbBuyBevelLight : hbSellBevelLight;
                            var bevelDark  = buy ? hbBuyBevelDark  : hbSellBevelDark;
                            float bl = x0 + 0.5f, br = x0 + w - 0.5f, bt = top + 0.5f, bb = top + h - 0.5f;
                            RenderTarget.DrawLine(new SharpDX.Vector2(bl, bt), new SharpDX.Vector2(br, bt), bevelLight, 1f);
                            RenderTarget.DrawLine(new SharpDX.Vector2(bl, bt), new SharpDX.Vector2(bl, bb), bevelLight, 1f);
                            RenderTarget.DrawLine(new SharpDX.Vector2(br, bt), new SharpDX.Vector2(br, bb), bevelDark, 1f);
                            RenderTarget.DrawLine(new SharpDX.Vector2(bl, bb), new SharpDX.Vector2(br, bb), bevelDark, 1f);
                        }
                    }
                    else
                    {
                        // Modo rapido (auto o forzado): 1 sola llamada de relleno plano por barra.
                        RenderTarget.FillRectangle(rect, buy ? hbBuySolid : hbSellSolid);
                    }

                    // Tapa brillante en el extremo si es anomalia fuerte. Se mantiene en ambos
                    // modos: es 1 fill barato y es justo la señal que no queremos perder nunca.
                    if (isAnomaly)
                    {
                        float capH = Math.Min(3f, h);
                        float capY = buy ? top : (top + h - capH);
                        RenderTarget.FillRectangle(new SharpDX.RectangleF(x0, capY, w, capH), hbCap);
                    }
                }
            }
            finally
            {
                RenderTarget.AntialiasMode = oldAa;
            }
        }

        #endregion

        // Escalona verticalmente los marcadores de velas contiguas del mismo lado.
        // Devuelve 0 para el primero, 1 y 2 para los inmediatamente siguientes.
        private int NextMarkerStack(int absoluteBar, bool drawAbove)
        {
            int lastBar = drawAbove ? lastMarkerBarAbove : lastMarkerBarBelow;
            int lastStack = drawAbove ? lastMarkerStackAbove : lastMarkerStackBelow;

            int stack;
            if (lastBar >= 0 && absoluteBar > lastBar && absoluteBar - lastBar <= MARKER_STACK_SPAN)
                stack = (lastStack + 1) % MARKER_STACK_LEVELS;
            else
                stack = 0;

            if (drawAbove)
            {
                lastMarkerBarAbove = absoluteBar;
                lastMarkerStackAbove = stack;
            }
            else
            {
                lastMarkerBarBelow = absoluteBar;
                lastMarkerStackBelow = stack;
            }

            return stack;
        }

        private void ResetMarkerStacking()
        {
            lastMarkerBarAbove = -1;
            lastMarkerBarBelow = -1;
            lastMarkerStackAbove = 0;
            lastMarkerStackBelow = 0;
        }

        private bool IsSignalBarPaintingEnabled(int code)
        {
            switch (Math.Abs(code))
            {
                case 2: // EXB / EXS
                case 4: // EXB / EXS expirado
                    return PaintExcessBars;
                case 5: // MB / MS
                case 6: // MB / MS expirado
                    return PaintMomentumBars;
                case 1: // CB / CS (ACEL interno)
                    return PaintContinuationBars;
                case 3: // RB / RS
                    return PaintRotationBars;
                default:
                    return false;
            }
        }

        private void PaintSignalBarOnly(int barsAgo, int code, Brush brush)
        {
            // Production visual path: do not repaint neutral/native candles.
            // Los interruptores por familia afectan únicamente la pintura de la vela.
            // El cálculo, el registro de la señal, las flechas y las etiquetas continúan activos.
            if (!PaintBars || !IsSignalBarPaintingEnabled(code) || brush == null)
                return;

            ForgetFlowSpeedPaintedBar(CurrentBars[0] - barsAgo);
            BarBrushes[barsAgo] = brush;
            CandleOutlineBrushes[barsAgo] = brush;
        }


        // Clave de sesion ILT: barras desde 18:00 pertenecen a la jornada que termina
        // al dia siguiente; barras antes de 18:00 conservan la fecha de inicio anterior.
        // Se usa la hora del chart, coherente con el resto del indicador y sus sesiones NY.
        private long GetAnomalySessionKey(int absoluteBarIndex)
        {
            int primaryLast = CurrentBars[0];
            int barsAgo = primaryLast - absoluteBarIndex;
            if (barsAgo < 0 || barsAgo > primaryLast)
                return activeAnomalySessionKey;

            DateTime t = Times[0][barsAgo];
            DateTime sessionStartDate = t.TimeOfDay >= TimeSpan.FromHours(18.0)
                ? t.Date
                : t.Date.AddDays(-1);
            return sessionStartDate.Ticks;
        }

        private void EnsureAnomalySession(int absoluteBarIndex)
        {
            long key = GetAnomalySessionKey(absoluteBarIndex);
            if (anomalySessionInitialized && key == activeAnomalySessionKey)
                return;

            // ¿Vimos nacer esta sesion o entramos a mitad de camino? Una transicion
            // presenciada (18:00) garantiza baseline completo; el primer arranque del
            // indicador solo lo garantiza si la primera barra cae en la apertura.
            // Resumen de la sesión que termina, antes de reiniciar los contadores.
            if (anomalySessionInitialized)
                PrintClassificationDiagnostics("sesion " + new DateTime(activeAnomalySessionKey).ToString("yyyy-MM-dd"));
            ResetClassificationDiagnostics();

            bool witnessedTransition = anomalySessionInitialized;
            sessionAnchoredFromOpen = witnessedTransition || IsSessionOpenBar(absoluteBarIndex);

            activeAnomalySessionKey = key;
            anomalySessionInitialized = true;

            // El baseline pertenece a la sesion: nunca cruza la pausa diaria.
            ResetSessionBaseline();
            zCacheBarIndex = -1;
            zCacheScope = -1;

            if (DebugMode)
            {
                Print("── [SESION] " + new DateTime(key).ToString("yyyy-MM-dd")
                    + " | baseline " + (sessionAnchoredFromOpen ? "COMPLETO (desde la apertura)" : "PARCIAL (arranque a mitad de sesion)")
                    + " | barra " + absoluteBarIndex);
                if (!sessionAnchoredFromOpen)
                    Print("   AVISO: esta instancia puede diferir de otra que si cargo la sesion completa.");
            }

            // Se guarda la cola de la sesion que termina para sembrar la que empieza.
            // (1.0.6) Muestreo UNIFORME de toda la sesion anterior, no su cola. Tomar las
            // ultimas N muestras retrataba solo el cierre —el tramo mas tranquilo del dia— y si
            // su percentil caia bajo el piso la siembra no aportaba nada. Un muestreo a paso
            // constante preserva la forma de la distribucion completa de la jornada.
            anomalyLevelSeedCarry.Clear();
            int srcCount = anomalyLevelWindow.Count;
            if (srcCount > 0)
            {
                int take = Math.Min(srcCount, ANOMALY_SEED_MAX);
                for (int k = 0; k < take; k++)
                {
                    int idx = (int)((long)k * srcCount / take);
                    if (idx >= srcCount) idx = srcCount - 1;
                    anomalyLevelSeedCarry.Add(anomalyLevelWindow[idx]);
                }
            }

            anomalyLevelWindow.Clear();
            anomalyLevelWindow.AddRange(anomalyLevelSeedCarry);
            anomalyLevelSeedRemaining = anomalyLevelSeedCarry.Count;
            anomalyLevelScratch.Clear();
            anomalyLevelLastBar = -1;
            anomalyLevelCachedBar = -1;
            anomalyLevelCachedValue = Math.Max(0.0, AnomalyGuideFilterLevel);
            lastSyncedGuideLevel = double.NaN;

            // (1.0.5) Un pendiente vivo no puede cruzar la pausa diaria: se cierra aqui, con
            // su marcador atenuado si lo tenia, para que ningun exceso quede sin auditar.
            if (pendingExcessActive)
            {
                ExpirePendingExcessMarker();
                pendingExcessActive = false;
            }

            // Estados decisionales que no deben cruzar la pausa diaria.
            anomalyGateArmed = true;
            anomalyReArmFloor = 0.0;
            pendingExcessActive = false;
            pendingExcessHasMarker = false;
            pendingExcessIsMomentum = false;
            pendingExcessBaseRange = 0.0;
            pendingExcessHigh = 0.0;
            pendingExcessLow = 0.0;
            pendingExcessEventRange = 0.0;
            pendingExcessBarsLeft = 0;
            pendingExcessBarIndex = -1;
            pendingExcessLastBar = -1;
            pendingExcessEventMagnitude = 0.0;
            pendingExcessEventLevel = 0.0;
            pendingResponseScaleTicks = 0.0;
            pendingBestFavorableTicks = 0.0;
            pendingContinuationEvidenceBars = 0;
            pendingRotationEvidenceBars = 0;
            responseRangeScratch.Clear();
            lastExcessMarkerBar = -999999;
            lastSignalBar = -999999;
            lastSignalCode = NONE;
        }

        // ¿La barra es la apertura de la sesion ILT (primeros minutos tras las 18:00)?
        private bool IsSessionOpenBar(int absoluteBarIndex)
        {
            int primaryLast = CurrentBars[0];
            int barsAgo = primaryLast - absoluteBarIndex;
            if (barsAgo < 0 || barsAgo > primaryLast)
                return false;

            DateTime t = Times[0][barsAgo];
            int minutes = t.Hour * 60 + t.Minute;
            return minutes >= 18 * 60 && minutes < 18 * 60 + 5;
        }

        // Registra la magnitud de UNA barra cerrada. Solo se llama DESPUES de evaluar
        // esa barra, por lo que nunca puede elevar su propio umbral.
        private void RecordAnomalyLevelSample(int closedBarIndex)
        {
            if (closedBarIndex < 0 || closedBarIndex == anomalyLevelLastBar)
                return;

            anomalyLevelLastBar = closedBarIndex;
            BarFlowData d = GetBarData(closedBarIndex, false);
            if (d == null || d.Samples <= 0 || IsDeadZoneBar(closedBarIndex))
                return;

            // Defensa adicional: una muestra de otra sesion nunca entra al buffer vigente.
            long key = GetAnomalySessionKey(closedBarIndex);
            if (!anomalySessionInitialized || key != activeAnomalySessionKey)
                return;

            // Baseline robusto (mediana + MAD) de la sesion: misma barra, misma causalidad.
            CommitSessionBaselineSample(closedBarIndex, d);

            anomalyLevelWindow.Add(Math.Abs(d.AnomalySigned));

            // Dilucion: cada muestra propia desplaza una muestra sembrada.
            if (anomalyLevelSeedRemaining > 0 && anomalyLevelWindow.Count > 0)
            {
                anomalyLevelWindow.RemoveAt(0);
                anomalyLevelSeedRemaining--;
            }

            // Con baseline de sesion, el percentil tambien cubre la sesion COMPLETA: si se
            // recortara a una ventana rodante, dos equipos con distinto punto de arranque
            // volverian a divergir justo en el umbral que decide cada senal.
            int cap = UseSessionBaseline
                ? SESSION_BASELINE_MAX
                : Math.Max(ANOMALY_LEVEL_MIN_SAMPLES, AnomalyLevelLookback);
            while (anomalyLevelWindow.Count > cap)
                anomalyLevelWindow.RemoveAt(0);

            // La muestra cambio: invalida el percentil para la proxima barra.
            anomalyLevelCachedBar = -1;
        }

        // Percentil intrasesion cacheado. Durante las primeras 30 muestras mezcla de forma
        // progresiva el piso fijo con el percentil observado para evitar saltos abruptos.
        private double CurrentAnomalyLevel(int barIndex)
        {
            double floor = Math.Max(0.0, AnomalyGuideFilterLevel);
            if (!UseAdaptiveAnomalyLevel || anomalyLevelWindow.Count == 0)
                return floor;

            if (anomalyLevelCachedBar == barIndex)
                return anomalyLevelCachedValue;

            anomalyLevelScratch.Clear();
            anomalyLevelScratch.AddRange(anomalyLevelWindow);
            anomalyLevelScratch.Sort();

            double p = Clamp(AnomalyLevelPercentile, 1.0, 99.0) / 100.0;
            int idx = (int)Math.Floor(p * (anomalyLevelScratch.Count - 1));
            if (idx < 0) idx = 0;
            if (idx >= anomalyLevelScratch.Count) idx = anomalyLevelScratch.Count - 1;

            double ceiling = Math.Max(floor, AnomalyLevelCeiling);
            double observed = Clamp(anomalyLevelScratch[idx], floor, ceiling);
            double warmupWeight = Clamp((double)anomalyLevelWindow.Count / ANOMALY_LEVEL_MIN_SAMPLES, 0.0, 1.0);
            double result = Clamp(floor + (observed - floor) * warmupWeight, floor, ceiling);

            anomalyLevelCachedBar = barIndex;
            anomalyLevelCachedValue = result;
            return result;
        }

        private double EffectiveAnomalyLevelForBar(int barIndex, BarFlowData d)
        {
            if (d != null && d.EffectiveAnomalyLevel > 0.0)
                return d.EffectiveAnomalyLevel;
            return CurrentAnomalyLevel(barIndex);
        }

        private bool PassesAnomalyGuideFilter(int barIndex, BarFlowData d, int signalCode)
        {
            if (!UseAnomalyGuideFilter)
                return true;

            double level = EffectiveAnomalyLevelForBar(barIndex, d);   // nivel causal fijado para esta barra
            if (level <= 0.0)
                return true;

            double value = d == null ? 0.0 : d.AnomalySigned;   // -100..+100
            double mag = Math.Abs(value);

            // Histeresis: el re-arme se resuelve UNA vez por barra en EvaluateSignal.
            // Aquí solo se respeta el estado del gate para no disparar pegado al borde.
            if (!anomalyGateArmed)
                return false;

            // Coherencia de direccion: una senal buy exige score positivo sobre el nivel,
            // una sell exige score negativo bajo -nivel. Si la direccion del score
            // contradice la clasificacion, se rechaza (baja conviccion / barra conflictiva).
            bool pass;
            if (signalCode > 0)
                pass = value >= level;
            else if (signalCode < 0)
                pass = value <= -level;
            else
                pass = mag >= level;

            return pass;
        }

        private void RestoreHistoricalVisualState()
        {
            if (!restoreHistoricalVisualsPending && !restoreHistoricalPlotsPending)
                return;

            FinalizePendingCompletedBars();

            int primaryLast = CurrentBars[0];
            if (primaryLast < 0)
                return;

            signalVisualRestoreScratch.Clear();
            historicalPlotRestoreScratch.Clear();
            lock (barDataSync)
            {
                if (barData == null)
                    return;

                foreach (KeyValuePair<int, BarFlowData> kv in barData)
                {
                    if (kv.Key < 0 || kv.Key > primaryLast || kv.Value == null)
                        continue;

                    historicalPlotRestoreScratch.Add(kv.Key);

                    if (kv.Value.VisualSignalCode != NONE)
                    {
                        signalVisualRestoreScratch.Add(new SignalVisualSnapshot
                        {
                            BarIndex = kv.Key,
                            Code = kv.Value.VisualSignalCode
                        });
                    }
                }
            }

            // Solo recorre barras que realmente recibieron ticks. El motor es idéntico; se
            // elimina trabajo visual vacío que penalizaba especialmente equipos antiguos.
            if (restoreHistoricalPlotsPending)
            {
                historicalPlotRestoreScratch.Sort();
                for (int i = 0; i < historicalPlotRestoreScratch.Count; i++)
                    UpdatePrimaryPlots(historicalPlotRestoreScratch[i], false);
            }

            signalVisualRestoreScratch.Sort((a, b) => a.BarIndex.CompareTo(b.BarIndex));
            if (restoreHistoricalVisualsPending)
            {
                // Se reconstruye en el mismo orden cronologico que en vivo, asi el
                // escalonado de etiquetas queda identico antes y despues de un F5.
                ResetMarkerStacking();
                for (int i = 0; i < signalVisualRestoreScratch.Count; i++)
                {
                    SignalVisualSnapshot v = signalVisualRestoreScratch[i];
                    int barsAgo = primaryLast - v.BarIndex;
                    if (barsAgo < 0 || barsAgo > primaryLast)
                        continue;

                    string label;
                    Brush brush;
                    bool drawAbove;
                    MapSignalVisual(v.Code, out label, out brush, out drawAbove);
                    PaintSignalBarOnly(barsAgo, v.Code, brush);
                    DrawSignal(barsAgo, v.Code, label, brush, drawAbove);
                }
            }

            restoreHistoricalVisualsPending = false;
            restoreHistoricalPlotsPending = false;
        }

        private void DrawSignal(int barsAgo, int code, string label, Brush brush, bool drawAbove)
        {
            bool drawLabel = ShowTextLabels && !string.IsNullOrWhiteSpace(label);
            if (!ShowArrows && !drawLabel)
                return;

            int primaryLast = CurrentBars[0];
            if (barsAgo < 0 || barsAgo > primaryLast)
                return;

            int absoluteBar = primaryLast - barsAgo;
            string tagBase = "ILT_FLOWSPEED_" + absoluteBar + "_" + code;

            // (1.0.3) Anti-colision: dos senales legitimas en velas contiguas y del mismo lado
            // (tipico del ciclo EXCESO -> RESPUESTA) dibujaban sus etiquetas casi a la misma
            // altura y el texto, mas ancho que la vela, se superponia: parecian una sola vela
            // con dos marcas. Se escalona la altura sin mover la logica ni la vela.
            int stack = NextMarkerStack(absoluteBar, drawAbove);
            double offset = MarkerOffsetTicks * TickSize * (1.0 + 0.85 * stack);
            double y = drawAbove ? Highs[0][barsAgo] + offset : Lows[0][barsAgo] - offset;

            if (ShowArrows)
            {
                string arrowTag = tagBase + "_ARW";
                if (drawAbove)
                    Draw.ArrowDown(this, arrowTag, false, barsAgo, y, brush);
                else
                    Draw.ArrowUp(this, arrowTag, false, barsAgo, y, brush);
            }

            if (drawLabel)
            {
                string textTag = tagBase + "_TXT";
                double textY = drawAbove ? y + offset * 1.5 : y - offset * 1.5;
                Draw.Text(this, textTag, false, label, barsAgo, textY, 0, brush, signalFont,
                    TextAlignment.Center, Brushes.Transparent, Brushes.Transparent, 0);
            }
        }

        private void FreezeEditableBrushes()
        {
            FreezeBrush(AcelBuyBrush);
            FreezeBrush(AcelSellBrush);
            FreezeBrush(ExcessBuyBrush);
            FreezeBrush(ExcessSellBrush);
            FreezeBrush(RotationBuyBrush);
            FreezeBrush(RotationSellBrush);
            FreezeBrush(HistogramBuyBrush);
            FreezeBrush(HistogramSellBrush);
            FreezeBrush(HistogramNeutralBrush);
            FreezeBrush(MomentumBuyBrush);
            FreezeBrush(MomentumSellBrush);
            FreezeBrush(GuideLineBrush);
            FreezeBrush(ZeroLineBrush);

            // Variantes atenuadas para excesos que expiraron sin veredicto.
            expiredExcessBuyBrush    = MakeFadedBrush(ExcessBuyBrush);
            expiredExcessSellBrush   = MakeFadedBrush(ExcessSellBrush);
            expiredMomentumBuyBrush  = MakeFadedBrush(MomentumBuyBrush);
            expiredMomentumSellBrush = MakeFadedBrush(MomentumSellBrush);
        }

        // Clona un brush del usuario con opacidad reducida (congelado, sin coste por frame).
        private Brush MakeFadedBrush(Brush source)
        {
            SolidColorBrush solid = source as SolidColorBrush;
            Color c = solid != null ? solid.Color : Colors.Gray;
            SolidColorBrush faded = new SolidColorBrush(Color.FromArgb(70, c.R, c.G, c.B));
            faded.Freeze();
            return faded;
        }

        private void FreezeBrush(Brush brush)
        {
            if (brush != null && brush.CanFreeze && !brush.IsFrozen)
                brush.Freeze();
        }

        private BarFlowData GetBarData(int barIndex, bool createIfMissing)
        {
            if (barIndex < 0)
                return null;

            lock (barDataSync)
            {
                if (barData == null)
                    return null;

                BarFlowData d;
                if (barData.TryGetValue(barIndex, out d))
                    return d;

                if (!createIfMissing)
                    return null;

                d = new BarFlowData();
                barData[barIndex] = d;

                // MaximumBarsLookBack.Infinite y la exigencia de reconstrucción histórica
                // requieren conservar una entrada por cada barra primaria cargada. La antigua
                // poda por 12 000 barras era la causa directa de histogramas y señales perdidos.
                return d;
            }
        }

        private BarFlowData GetBarDataHot(int barIndex)
        {
            if (barIndex == hotCurIdx && hotCurData != null)
                return hotCurData;
            hotCurData = GetBarData(barIndex, true);
            hotCurIdx = barIndex;
            return hotCurData;
        }

        private BarFlowData GetPrevBarDataHot(int barIndex)
        {
            if (barIndex == hotPrevIdx)
                return hotPrevData;          // puede ser null legitimamente (sin datos)
            hotPrevData = GetBarData(barIndex, false);
            hotPrevIdx = barIndex;
            return hotPrevData;
        }

        private void UpdateZScoresFast(int barIndex, BarFlowData target)
        {
            if (target == null)
                return;

            int period = Math.Max(2, SpeedLookback);
            EnsureZScoreCache(barIndex, period);

            // Warmup: sin suficientes barras validas, no fabricamos anomalias.
            if (zCacheCount < ROBUST_MIN_SAMPLES)
            {
                target.SpeedZ = 0;
                target.NetSpeedZ = 0;
                target.AbsNetSpeedZ = 0;
                target.AccelerationZ = 0;
                return;
            }

            // Z robusto = (x - mediana) / escala. La escala ya viene con piso > 0,
            // asi que no hay division por cero ni explosiones en tape plano.
            target.SpeedZ        = (target.TotalSpeed  - zCacheMedTotal)  / zCacheScaleTotal;
            target.NetSpeedZ     = (target.NetSpeed    - zCacheMedNet)    / zCacheScaleNet;
            target.AbsNetSpeedZ  = (target.AbsNetSpeed - zCacheMedAbsNet) / zCacheScaleAbsNet;
            target.AccelerationZ = (target.Acceleration - zCacheMedAccel) / zCacheScaleAccel;
        }

        // Mediana sobre una lista (la ordena in-place; las listas se limpian cada barra).
        private static double MedianInPlace(List<double> values)
        {
            int n = values.Count;
            if (n == 0)
                return 0.0;

            values.Sort();
            int mid = n / 2;
            return (n % 2 == 1) ? values[mid] : 0.5 * (values[mid - 1] + values[mid]);
        }

        // Escala robusta = 1.4826 * MAD, con piso relativo al nivel tipico (anchor) para
        // que el mercado plano no manufacture Z gigantes a partir de ticks minimos.
        private double RobustScaleFloored(List<double> values, double median, double anchor)
        {
            int n = values.Count;
            if (n == 0)
                return Math.Max(ROBUST_REL_FLOOR * anchor, 1e-9);

            madScratch.Clear();
            for (int i = 0; i < n; i++)
                madScratch.Add(Math.Abs(values[i] - median));

            double mad = MedianInPlace(madScratch);
            double robust = ROBUST_MAD_TO_SIGMA * mad;
            double floor = ROBUST_REL_FLOOR * anchor;
            return Math.Max(robust, Math.Max(floor, 1e-9));
        }

        // Reinicia el acumulador de sesion. Se llama al cargar datos y en cada
        // transicion de sesion ILT (18:00), nunca a mitad de sesion.
        private void ResetSessionBaseline()
        {
            sessTotal.Clear();
            sessNet.Clear();
            sessAbsNet.Clear();
            sessAccel.Clear();
            sessionBaselineLastBar = -1;
            sessionFirstBarIndex = -1;
            sessStatsCount = -1;
        }

        // Incorpora UNA barra cerrada ya evaluada al baseline de la sesion.
        private void CommitSessionBaselineSample(int closedBarIndex, BarFlowData d)
        {
            if (d == null || d.Samples <= 0 || closedBarIndex <= sessionBaselineLastBar)
                return;

            sessTotal.Add(d.TotalSpeed);
            sessNet.Add(d.NetSpeed);
            sessAbsNet.Add(d.AbsNetSpeed);
            sessAccel.Add(d.Acceleration);

            while (sessTotal.Count > SESSION_BASELINE_MAX)
            {
                sessTotal.RemoveAt(0);
                sessNet.RemoveAt(0);
                sessAbsNet.RemoveAt(0);
                sessAccel.RemoveAt(0);
            }

            if (sessionFirstBarIndex < 0)
                sessionFirstBarIndex = closedBarIndex;
            sessionBaselineLastBar = closedBarIndex;

            // El conjunto cambio: el proximo calculo debe rehacerse.
            zCacheBarIndex = -1;
        }

        private void EnsureZScoreCache(int barIndex, int period)
        {
            // Ambito del baseline. La via de sesion exige que TODAS las muestras
            // acumuladas sean anteriores a la barra evaluada (causalidad estricta);
            // si no lo son (recalculo de una barra ya incorporada), se usa la rodante.
            bool useSession = UseSessionBaseline
                              && sessionBaselineLastBar >= 0
                              && barIndex > sessionBaselineLastBar
                              && sessTotal.Count >= ROBUST_MIN_SAMPLES;
            int scope = useSession ? 1 : 0;

            if (zCacheBarIndex == barIndex && zCachePeriod == period && zCacheScope == scope)
                return;

            // Via de sesion con estadisticos aun vigentes: se reutilizan tal cual. El paso
            // se mide en MUESTRAS de la sesion (no en tiempo ni en barras del equipo), por
            // lo que dos instancias con la misma sesion recalculan en los mismos puntos.
            if (useSession && sessStatsCount > 0 && zCacheCount >= ROBUST_MIN_SAMPLES)
            {
                int pending = sessTotal.Count - sessStatsCount;
                int stride = sessTotal.Count > SESSION_STATS_BIG ? SESSION_STATS_STRIDE : 1;
                if (pending >= 0 && pending < stride)
                {
                    zCacheBarIndex = barIndex;
                    zCachePeriod = period;
                    zCacheScope = scope;
                    return;
                }
            }

            zCacheBarIndex = barIndex;
            zCachePeriod = period;
            zCacheScope = scope;
            zCacheCount = 0;
            zCacheMedTotal = zCacheMedNet = zCacheMedAbsNet = zCacheMedAccel = 0;
            zCacheScaleTotal = zCacheScaleNet = zCacheScaleAbsNet = zCacheScaleAccel = 0;

            zwTotal.Clear();
            zwNet.Clear();
            zwAbsNet.Clear();
            zwAccel.Clear();

            if (useSession)
            {
                // Baseline = TODA la sesion ILT en curso hasta la barra anterior. El
                // conjunto lo define el reloj del mercado, no la hora de arranque del PC.
                zwTotal.AddRange(sessTotal);
                zwNet.AddRange(sessNet);
                zwAbsNet.AddRange(sessAbsNet);
                zwAccel.AddRange(sessAccel);
            }
            else
            {
                int first = Math.Max(0, barIndex - period);

                // Solo barras previas y validas. Se excluye la zona muerta para no mezclar
                // un regimen sin actividad dentro del baseline (higiene de sesion ILT).
                for (int i = barIndex - 1; i >= first; i--)
                {
                    BarFlowData d = GetBarData(i, false);
                    if (d == null || d.Samples <= 0)
                        continue;
                    if (IsDeadZoneBar(i))
                        continue;

                    zwTotal.Add(d.TotalSpeed);
                    zwNet.Add(d.NetSpeed);
                    zwAbsNet.Add(d.AbsNetSpeed);
                    zwAccel.Add(d.Acceleration);
                }
            }

            zCacheCount = zwTotal.Count;
            if (zCacheCount < 2)
                return;

            zCacheMedTotal  = MedianInPlace(zwTotal);
            zCacheMedNet    = MedianInPlace(zwNet);
            zCacheMedAbsNet = MedianInPlace(zwAbsNet);
            zCacheMedAccel  = MedianInPlace(zwAccel);

            // Anchor = nivel tipico de actividad del instrumento (en sus propias unidades),
            // para que el piso de escala sea portable entre ES y NQ sin constantes absolutas.
            // Net y Accel comparten unidades con AbsNet, asi que se anclan a su escala.
            double scaleAbsNetRaw = RobustScaleFloored(zwAbsNet, zCacheMedAbsNet, Math.Max(zCacheMedAbsNet, 1e-9));
            double anchor = Math.Max(zCacheMedAbsNet, scaleAbsNetRaw);

            zCacheScaleTotal  = RobustScaleFloored(zwTotal,  zCacheMedTotal,  Math.Max(zCacheMedTotal, anchor));
            zCacheScaleAbsNet = Math.Max(scaleAbsNetRaw, ROBUST_REL_FLOOR * anchor);
            zCacheScaleNet    = RobustScaleFloored(zwNet,   zCacheMedNet,   anchor);
            zCacheScaleAccel  = RobustScaleFloored(zwAccel, zCacheMedAccel, anchor);

            // Sello del tramo: hasta cuantas muestras de sesion son validos estos estadisticos.
            sessStatsCount = useSession ? sessTotal.Count : -1;
        }

        private void ResetZScoreCache()
        {
            zCacheBarIndex = -1;
            zCachePeriod = -1;
            zCacheCount = 0;
            zCacheMedTotal = zCacheMedNet = zCacheMedAbsNet = zCacheMedAccel = 0;
            zCacheScaleTotal = zCacheScaleNet = zCacheScaleAbsNet = zCacheScaleAccel = 0;
            zwTotal.Clear();
            zwNet.Clear();
            zwAbsNet.Clear();
            zwAccel.Clear();
            madScratch.Clear();
            anomalyGateArmed = true;
            anomalyReArmFloor = 0.0;
        }

        private static double Clamp(double value, double minValue, double maxValue)
        {
            if (value < minValue)
                return minValue;

            if (value > maxValue)
                return maxValue;

            return value;
        }

        // Mapea un valor del rango [lo, hi] a [0, 1]. Por debajo de lo aporta 0 (el flujo
        // por debajo del umbral no infla el score); por encima de hi satura en 1.
        private static double Saturate(double value, double lo, double hi)
        {
            if (hi <= lo)
                return value >= hi ? 1.0 : 0.0;
            if (value <= lo)
                return 0.0;
            if (value >= hi)
                return 1.0;
            return (value - lo) / (hi - lo);
        }

        // Zona muerta ILT (17:00 -> 18:00, hora del chart). Defensivo ante indices fuera de rango.
        private bool IsDeadZoneBar(int absoluteBarIndex)
        {
            int primaryLast = CurrentBars[0];
            int barsAgo = primaryLast - absoluteBarIndex;
            if (barsAgo < 0 || barsAgo > primaryLast)
                return false;

            DateTime t = Times[0][barsAgo];
            int minutes = t.Hour * 60 + t.Minute;
            return minutes >= 17 * 60 && minutes < 18 * 60;
        }

        // ── Score de anomalia compuesto ───────────────────────────────────────
        // Descompone la anomalia en tres preguntas ortogonales y las combina en un
        // unico numero 0..100 con direccion. Este es el numero que ve el operador
        // en el histograma, en la linea guia y el que usa el filtro: todo coherente.
        //   M (magnitud)     : que tan inusualmente rapido (saturado entre umbral y extremo)
        //   D (direccion)    : que tan unilateral (NetSpeedZ via tanh + Imbalance), -1..+1
        //   A (aceleracion)  : esta construyendo o desvaneciendose
        private void ComputeAnomalyScore(BarFlowData d)
        {
            if (d == null)
                return;

            double m = Saturate(Math.Max(d.SpeedZ, d.AbsNetSpeedZ), SpeedZThreshold, ExtremeSpeedZ);
            double a = Saturate(Math.Abs(d.AccelerationZ), AccelerationZThreshold, ExtremeSpeedZ);

            double dirZ = Math.Tanh(d.NetSpeedZ);   // -1..+1 suave, satura cerca de |z|~2
            double dirImb = MinimumImbalance <= 0.0 ? 0.0 : Clamp(d.Imbalance / MinimumImbalance, -1.0, 1.0);
            double dir = Clamp(0.6 * dirZ + 0.4 * dirImb, -1.0, 1.0);

            double wM = Math.Max(0.0, AnomalyWeightMagnitude);
            double wD = Math.Max(0.0, AnomalyWeightDirection);
            double wA = Math.Max(0.0, AnomalyWeightAcceleration);
            double wsum = wM + wD + wA;
            if (wsum <= 0.0)
            {
                wM = 0.5; wD = 0.3; wA = 0.2; wsum = 1.0;
            }

            double magnitude = 100.0 * (wM * m + wD * Math.Abs(dir) + wA * a) / wsum;
            magnitude = Clamp(magnitude, 0.0, 100.0);

            d.AnomalyMagnitude = magnitude;
            d.AnomalySigned = (dir >= 0.0 ? 1.0 : -1.0) * magnitude;
        }

        // Umbral E autoajustado por fuerza de la anomalia causal. EfficiencyTrigger sigue
        // siendo el centro de sensibilidad, no una frontera rigida para todos los regimenes.
        private double CurrentAdaptiveEfficiencyTrigger(BarFlowData d)
        {
            double baseThreshold = Clamp(EfficiencyTrigger, 0.05, 0.80);
            if (d == null)
                return baseThreshold;

            double level = d.EffectiveAnomalyLevel > 0.0
                ? d.EffectiveAnomalyLevel
                : Math.Max(1.0, AnomalyGuideFilterLevel);
            double ratio = Clamp(d.AnomalyMagnitude / Math.Max(1.0, level), 0.75, 1.75);
            double strength01 = ratio - 0.75; // 0..1

            // Debil: hasta +15%; extremo: hasta -15%. Mantiene estabilidad y causalidad.
            double adjusted = baseThreshold * (1.15 - 0.30 * strength01);
            return Clamp(adjusted, 0.15, 0.55);
        }

        // Mediana robusta del rango de barras cerradas anteriores al evento. No usa ATR
        // exponencial porque una sola expansion no debe inflar de golpe la frontera ROT/ACEL.
        private double MedianRecentRangeTicks(int absoluteBarIndex, int lookback)
        {
            responseRangeScratch.Clear();
            if (TickSize <= 0.0 || CurrentBars[0] < 0)
                return 0.0;

            int last = absoluteBarIndex - 1;
            int first = Math.Max(0, last - Math.Max(1, lookback) + 1);
            for (int idx = last; idx >= first; idx--)
            {
                int primaryLast = CurrentBars[0];
                int ba = primaryLast - idx;
                if (ba < 0 || ba > primaryLast)
                    continue;

                double ticks = (Highs[0][ba] - Lows[0][ba]) / TickSize;
                if (!double.IsNaN(ticks) && !double.IsInfinity(ticks) && ticks > 0.0)
                    responseRangeScratch.Add(ticks);
            }

            if (responseRangeScratch.Count == 0)
                return 0.0;

            responseRangeScratch.Sort();
            int mid = responseRangeScratch.Count / 2;
            if ((responseRangeScratch.Count & 1) == 1)
                return responseRangeScratch[mid];

            return 0.5 * (responseRangeScratch[mid - 1] + responseRangeScratch[mid]);
        }

        // Escala de trabajo del precio en ticks. Combina estructura del propio evento (60%)
        // y regimen local robusto (40%); sirve igual para NQ R40, tiempo, Renko o GC sin presets.
        private double ComputeAdaptiveResponseScaleTicks(int absoluteBarIndex, double eventRangeTicks)
        {
            double eventTicks = Math.Max(1.0, eventRangeTicks);
            int lookback = Math.Max(12, Math.Min(40, SpeedLookback));
            double localMedianTicks = MedianRecentRangeTicks(absoluteBarIndex, lookback);
            if (localMedianTicks <= 0.0)
                localMedianTicks = eventTicks;

            double scale = 0.60 * eventTicks + 0.40 * localMedianTicks;
            double upper = Math.Max(4.0, Math.Max(eventTicks, localMedianTicks) * 2.50);
            return Clamp(scale, 2.0, upper);
        }

        // Respuesta puramente de precio, firmada y sin referencia al flujo actual.
        // Se reutiliza para evaluar la vela contra la direccion ORIGINAL del impulso.
        private double ComputePriceFollow(double bodyTicks, double rangeTicks, double closeLocation)
        {
            double rawEff = bodyTicks / Math.Max(1.0, rangeTicks);
            double closeEff = (Clamp(closeLocation, 0.0, 1.0) - 0.5) * 2.0;
            return Clamp(0.60 * rawEff + 0.40 * closeEff, -1.0, 1.0);
        }

        // Eficiencia E (-1..+1): proyecta el movimiento de precio (CON SIGNO) sobre la direccion
        // del flujo.  E = sign(flujo) * priceFollow,  con priceFollow = 0.6*rawEff + 0.4*closeEff
        // (cuerpo + ubicacion del cierre, FIRMADO). El signo del precio sale del cuerpo Y del cierre
        // juntos: una vela de cuerpo chico que CIERRA abajo (rechazo) da priceFollow negativo aunque
        // el cuerpo sea levemente positivo -> en absorcion (flujo compra, precio rechazado) E queda
        // NEGATIVA, como debe ser. E>0 el precio siguio al flujo (continuacion); E<0 lo rechazo
        // (rotacion/absorcion). Sin unidades => auto-escala a 40R. El bug del OHLC de la serie de
        // ticks ya esta corregido, asi que priceFollow nunca colapsa a 0 espuriamente.
        private double ComputeEfficiency(double flowRef, double bodyTicks, double rangeTicks, double closeLocation)
        {
            double priceFollow = ComputePriceFollow(bodyTicks, rangeTicks, closeLocation);

            double flowDir = flowRef > 0.0 ? 1.0
                           : (flowRef < 0.0 ? -1.0
                           : (priceFollow >= 0.0 ? 1.0 : -1.0));     // sin flujo: usa el propio precio

            return Clamp(flowDir * priceFollow, -1.0, 1.0);
        }

        // Suaviza E con una EMA corta para domar el serrucho tipico de las range bars (el cierre
        // casi siempre satura en un extremo del rango). Mezcla la E cruda de ESTA vela con la E ya
        // suavizada de la vela anterior. EfficiencySmoothPeriod = 1 => sin suavizado (alpha = 1).
        // La vela en formacion se ancla a la suavizada de la anterior (cerrada y fija) => estable
        // tick a tick. Se usa el MISMO valor suavizado para la linea y para la clasificacion.
        private double SmoothEfficiency(int barIndex, double rawE)
        {
            int per = EfficiencySmoothPeriod < 1 ? 1 : EfficiencySmoothPeriod;
            if (per <= 1)
                return rawE;

            double alpha = 2.0 / (per + 1.0);   // EMA: per=3 -> 0.5, per=5 -> 0.333

            BarFlowData prev = GetBarData(barIndex - 1, false);
            if (prev != null)
                return Clamp(alpha * rawE + (1.0 - alpha) * prev.Efficiency, -1.0, 1.0);

            return rawE;
        }

        private double HighestHigh(int startBarsAgo, int period)
        {
            double highest = double.MinValue;
            int primaryLast = CurrentBars[0];
            int maxBarsAgo = Math.Min(primaryLast, startBarsAgo + period - 1);

            for (int i = startBarsAgo; i <= maxBarsAgo; i++)
                highest = Math.Max(highest, Highs[0][i]);

            return highest == double.MinValue ? Highs[0][startBarsAgo] : highest;
        }

        private double LowestLow(int startBarsAgo, int period)
        {
            double lowest = double.MaxValue;
            int primaryLast = CurrentBars[0];
            int maxBarsAgo = Math.Min(primaryLast, startBarsAgo + period - 1);

            for (int i = startBarsAgo; i <= maxBarsAgo; i++)
                lowest = Math.Min(lowest, Lows[0][i]);

            return lowest == double.MaxValue ? Lows[0][startBarsAgo] : lowest;
        }

        #region Plot accessors
        [XmlIgnore]
        [Browsable(false)]
        public Series<double> FlowAnomalyHistogram
        {
            get { return Values[0]; }
        }

        [XmlIgnore]
        [Browsable(false)]
        public Series<double> EfficiencyLine
        {
            get { return Values[1]; }
        }

        #endregion

        #region Parámetros — 01. Operación

        [NinjaScriptProperty]
        [Range(1, 3)]
        [Display(Name = "Modo operativo (1 Activo / 2 Balanceado / 3 Estricto NQ)", Description = "También puede seleccionarse por nombre desde el menú ILT FLOWSPEED de la barra superior.", Order = 1, GroupName = "01. Operación")]
        public int OperationalMode { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Volumen mínimo por trade", Order = 2, GroupName = "01. Operación")]
        public int MinimumTradeVolume { get; set; }

        [Display(Name = "Modo diagnóstico", Description = "Escribe en la ventana Output el sello de versión, el estado del baseline de sesión y la traza completa del ciclo impulso→respuesta. Úselo para calibrar; desactívelo en operación.", Order = 3, GroupName = "01. Operación")]
        public bool DebugMode { get; set; }

        #endregion

        #region Parámetros — 02. Sensibilidad

        [NinjaScriptProperty]
        [Range(1, 60)]
        [Display(Name = "Ventana de confirmación (velas)", Order = 1, GroupName = "02. Sensibilidad")]
        public int ExcessConfirmWindow { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, 1.0)]
        [Display(Name = "Eficiencia base autoajustada (0-1)", Order = 2, GroupName = "02. Sensibilidad")]
        public double EfficiencyTrigger { get; set; }

        #endregion

        #region Parámetros — 03. Adaptación

        [NinjaScriptProperty]
        [Display(Name = "Usar filtro de anomalía", Order = 1, GroupName = "03. Adaptación")]
        public bool UseAnomalyGuideFilter { get; set; }

        [NinjaScriptProperty]
        [Range(0, 120)]
        [Display(Name = "Sensibilidad base de anomalía", Order = 2, GroupName = "03. Adaptación")]
        public double AnomalyGuideFilterLevel { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Nivel adaptativo intradía", Order = 3, GroupName = "03. Adaptación")]
        public bool UseAdaptiveAnomalyLevel { get; set; }

        [NinjaScriptProperty]
        [Range(50.0, 95.0)]
        [Display(Name = "Percentil adaptativo", Order = 4, GroupName = "03. Adaptación")]
        public double AnomalyLevelPercentile { get; set; }

        [NinjaScriptProperty]
        [Range(30, 2000)]
        [Display(Name = "Memoria adaptativa (barras)", Description = "Solo se usa con el baseline rodante. Con baseline de sesión completa, la memoria es la sesión entera.", Order = 5, GroupName = "03. Adaptación")]
        public int AnomalyLevelLookback { get; set; }

        [Display(Name = "Exigir aceptación sostenida", Description = "ON: en la primera vela de respuesta sólo se admite un veredicto decisivo; una lectura marginal espera a que el mercado la sostenga. OFF: comportamiento anterior (veredicto inmediato con el umbral normal).", Order = 3, GroupName = "02. Sensibilidad")]
        public bool RequireSustainedAcceptance { get; set; }

        [Display(Name = "Baseline de sesión completa", Description = "Calcula mediana+MAD y el percentil adaptativo sobre TODA la sesión ILT (18:00→17:00) en vez de una ventana rodante. Necesario para que distintos equipos produzcan señales idénticas.", Order = 7, GroupName = "03. Adaptación")]
        public bool UseSessionBaseline { get; set; }

        [NinjaScriptProperty]
        [Range(0, 120)]
        [Display(Name = "Techo adaptativo", Order = 6, GroupName = "03. Adaptación")]
        public double AnomalyLevelCeiling { get; set; }

        #endregion

        #region Parámetros — 04. Visual

        [NinjaScriptProperty]
        [Display(Name = "Mostrar Flow Speed", Description = "Muestra u oculta el panel Flow Speed clásico sin detener el motor avanzado ni sus señales.", Order = 1, GroupName = "04. Visual")]
        public bool ShowHistogramPanel { get; set; }

        [Browsable(false)]
        [NinjaScriptProperty]
        [Display(Name = "Mostrar línea E (eficiencia)", Order = 2, GroupName = "04. Visual")]
        public bool ShowEfficiencyLine { get; set; }

        [Browsable(false)]
        [NinjaScriptProperty]
        [Display(Name = "Histograma 3D", Order = 3, GroupName = "04. Visual")]
        public bool Enable3DHistogram { get; set; }

        [Browsable(false)]
        [NinjaScriptProperty]
        [Range(50, 3000)]
        [Display(Name = "Autoapagar 3D con N velas visibles", Order = 4, GroupName = "04. Visual")]
        public int Histogram3DMaxVisibleBars { get; set; }

        [Browsable(false)]
        [NinjaScriptProperty]
        [Display(Name = "Modo compatibilidad gráfica", Description = "Usa únicamente plots y dibujos nativos. Procesa exactamente los mismos ticks y conserva el mismo motor de señales.", Order = 5, GroupName = "04. Visual")]
        public bool CompatibilityRenderMode { get; set; }

        [Browsable(false)]
        [NinjaScriptProperty]
        [Display(Name = "Fallback gráfico automático", Description = "Si SharpDX falla, cambia automáticamente al histograma nativo sin detener el cálculo ni las señales.", Order = 6, GroupName = "04. Visual")]
        public bool AutoFallbackToNativeRender { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mostrar flechas", Order = 7, GroupName = "04. Visual")]
        public bool ShowArrows { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mostrar etiquetas", Order = 8, GroupName = "04. Visual")]
        public bool ShowTextLabels { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Pintar velas EXCESO (EXB/EXS)", Description = "Activa o desactiva solamente el pintado de velas de Exceso. El cálculo, las flechas y las etiquetas continúan activos.", Order = 9, GroupName = "04. Visual")]
        public bool PaintExcessBars { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Pintar velas MOMENTUM (MB/MS)", Description = "Activa o desactiva solamente el pintado de velas de Momentum. El cálculo, las flechas y las etiquetas continúan activos.", Order = 10, GroupName = "04. Visual")]
        public bool PaintMomentumBars { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Pintar velas CONTINUACIÓN (CB/CS)", Description = "Activa o desactiva solamente el pintado de velas de Continuación. El cálculo, las flechas y las etiquetas continúan activos.", Order = 11, GroupName = "04. Visual")]
        public bool PaintContinuationBars { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Pintar velas ROTACIÓN (RB/RS)", Description = "Activa o desactiva solamente el pintado de velas de Rotación. El cálculo, las flechas y las etiquetas continúan activos.", Order = 12, GroupName = "04. Visual")]
        public bool PaintRotationBars { get; set; }

        [NinjaScriptProperty]
        [Range(1, 50)]
        [Display(Name = "Separación de marcadores (ticks)", Order = 13, GroupName = "04. Visual")]
        public int MarkerOffsetTicks { get; set; }

        [NinjaScriptProperty]
        [Range(6, 30)]
        [Display(Name = "Tamaño de etiquetas", Order = 14, GroupName = "04. Visual")]
        public int SignalLabelFontSize { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Etiquetas en negrita", Order = 15, GroupName = "04. Visual")]
        public bool SignalLabelBold { get; set; }

        #endregion

        #region Parámetros — 05. Etiquetas

        [NinjaScriptProperty]
        [Display(Name = "Etiqueta CONTINUACIÓN BUY (CB)", Order = 5, GroupName = "05. Etiquetas")]
        public string AcelBuyLabel { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Etiqueta CONTINUACIÓN SELL (CS)", Order = 6, GroupName = "05. Etiquetas")]
        public string AcelSellLabel { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Etiqueta EXCESO BUY (EXB)", Order = 1, GroupName = "05. Etiquetas")]
        public string ExcessBuyLabel { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Etiqueta EXCESO SELL (EXS)", Order = 2, GroupName = "05. Etiquetas")]
        public string ExcessSellLabel { get; set; }

        [Display(Name = "Etiqueta MOMENTUM BUY (MB)", Order = 3, GroupName = "05. Etiquetas")]
        public string MomentumBuyLabel { get; set; }

        [Display(Name = "Etiqueta MOMENTUM SELL (MS)", Order = 4, GroupName = "05. Etiquetas")]
        public string MomentumSellLabel { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Etiqueta ROTACIÓN BUY (RB)", Order = 7, GroupName = "05. Etiquetas")]
        public string RotationBuyLabel { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Etiqueta ROTACIÓN SELL (RS)", Order = 8, GroupName = "05. Etiquetas")]
        public string RotationSellLabel { get; set; }

        #endregion

        #region Parámetros — 06. Colores de señales

        [XmlIgnore]
        [Display(Name = "Color MOMENTUM BUY (MB)", Order = 3, GroupName = "06. Colores de señales")]
        public Brush MomentumBuyBrush { get; set; }

        [Browsable(false)]
        public string MomentumBuyBrushSerializable
        {
            get { return Serialize.BrushToString(MomentumBuyBrush); }
            set { MomentumBuyBrush = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Color MOMENTUM SELL (MS)", Order = 4, GroupName = "06. Colores de señales")]
        public Brush MomentumSellBrush { get; set; }

        [Browsable(false)]
        public string MomentumSellBrushSerializable
        {
            get { return Serialize.BrushToString(MomentumSellBrush); }
            set { MomentumSellBrush = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Color CONTINUACIÓN BUY (CB)", Order = 5, GroupName = "06. Colores de señales")]
        public Brush AcelBuyBrush { get; set; }

        [Browsable(false)]
        public string AcelBuyBrushSerializable
        {
            get { return Serialize.BrushToString(AcelBuyBrush); }
            set { AcelBuyBrush = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Color CONTINUACIÓN SELL (CS)", Order = 6, GroupName = "06. Colores de señales")]
        public Brush AcelSellBrush { get; set; }

        [Browsable(false)]
        public string AcelSellBrushSerializable
        {
            get { return Serialize.BrushToString(AcelSellBrush); }
            set { AcelSellBrush = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Color EXCESO BUY (EXB)", Order = 1, GroupName = "06. Colores de señales")]
        public Brush ExcessBuyBrush { get; set; }

        [Browsable(false)]
        public string ExcessBuyBrushSerializable
        {
            get { return Serialize.BrushToString(ExcessBuyBrush); }
            set { ExcessBuyBrush = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Color EXCESO SELL (EXS)", Order = 2, GroupName = "06. Colores de señales")]
        public Brush ExcessSellBrush { get; set; }

        [Browsable(false)]
        public string ExcessSellBrushSerializable
        {
            get { return Serialize.BrushToString(ExcessSellBrush); }
            set { ExcessSellBrush = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Color ROTACIÓN BUY (RB)", Order = 7, GroupName = "06. Colores de señales")]
        public Brush RotationBuyBrush { get; set; }

        [Browsable(false)]
        public string RotationBuyBrushSerializable
        {
            get { return Serialize.BrushToString(RotationBuyBrush); }
            set { RotationBuyBrush = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Color ROTACIÓN SELL (RS)", Order = 8, GroupName = "06. Colores de señales")]
        public Brush RotationSellBrush { get; set; }

        [Browsable(false)]
        public string RotationSellBrushSerializable
        {
            get { return Serialize.BrushToString(RotationSellBrush); }
            set { RotationSellBrush = Serialize.StringToBrush(value); }
        }

        #endregion

        #region Parámetros — 07. Colores del histograma

        [Browsable(false)]
        [XmlIgnore]
        [Display(Name = "Color compra (histograma)", Order = 1, GroupName = "07. Colores del histograma")]
        public Brush HistogramBuyBrush { get; set; }

        [Browsable(false)]
        public string HistogramBuyBrushSerializable
        {
            get { return Serialize.BrushToString(HistogramBuyBrush); }
            set { HistogramBuyBrush = Serialize.StringToBrush(value); }
        }

        [Browsable(false)]
        [XmlIgnore]
        [Display(Name = "Color venta (histograma)", Order = 2, GroupName = "07. Colores del histograma")]
        public Brush HistogramSellBrush { get; set; }

        [Browsable(false)]
        public string HistogramSellBrushSerializable
        {
            get { return Serialize.BrushToString(HistogramSellBrush); }
            set { HistogramSellBrush = Serialize.StringToBrush(value); }
        }

        [Browsable(false)]
        [XmlIgnore]
        [Display(Name = "Color neutral (histograma)", Order = 3, GroupName = "07. Colores del histograma")]
        public Brush HistogramNeutralBrush { get; set; }

        [Browsable(false)]
        public string HistogramNeutralBrushSerializable
        {
            get { return Serialize.BrushToString(HistogramNeutralBrush); }
            set { HistogramNeutralBrush = Serialize.StringToBrush(value); }
        }

        [Browsable(false)]
        [XmlIgnore]
        [Display(Name = "Color líneas guía", Order = 4, GroupName = "07. Colores del histograma")]
        public Brush GuideLineBrush { get; set; }

        [Browsable(false)]
        public string GuideLineBrushSerializable
        {
            get { return Serialize.BrushToString(GuideLineBrush); }
            set { GuideLineBrush = Serialize.StringToBrush(value); }
        }

        [Browsable(false)]
        [XmlIgnore]
        [Display(Name = "Color línea cero", Order = 5, GroupName = "07. Colores del histograma")]
        public Brush ZeroLineBrush { get; set; }

        [Browsable(false)]
        public string ZeroLineBrushSerializable
        {
            get { return Serialize.BrushToString(ZeroLineBrush); }
            set { ZeroLineBrush = Serialize.StringToBrush(value); }
        }

        #endregion

        #region Parámetros — Motor interno (ocultos, no editables)
        // Capa interna del motor: la calibran los presets del modo operativo o quedan
        // fijados por diseño. Se conservan como propiedades para serialización de
        // templates y para ajuste programático desde estrategias/backtesting.

        [NinjaScriptProperty]
        [Browsable(false)]
        public bool UseOperationalModePresets { get; set; }

        [NinjaScriptProperty]
        [Range(100, 10000)]
        [Display(Name = "Data Accumulation Interval (ms)", Description = "Duración de cada evento Flow Speed. El valor original es 1000 ms.", Order = 1, GroupName = "07. Flow Speed - Cálculo")]
        public int CumulationIntervalMs { get; set; }

        [NinjaScriptProperty]
        [Range(10, 500)]
        [Browsable(false)]
        public int SpeedLookback { get; set; }

        // Visible desde 1.0.4: es la palanca de simetría histórico/tiempo real.
        // ON  = máxima precisión en vivo (regla de cotización) pero el histórico se
        //       reconstruye con tick rule: la misma sesión puede reclasificarse tras un F5.
        // OFF = tick rule en ambos estados: reproducible bit a bit entre equipos y entre
        //       vivo e histórico, a costa de algo de precisión en tiempo real.
        [Display(Name = "Regla de cotización en tiempo real", Description = "ON: usa bid/ask para clasificar el agresor en vivo (más preciso, pero el histórico se reconstruye con tick rule). OFF: tick rule siempre, idéntico en vivo, en histórico y entre equipos.", Order = 4, GroupName = "01. Operación")]
        public bool UseBidAskClassification { get; set; }

        [NinjaScriptProperty]
        [Browsable(false)]
        public bool ClassifyOnClosedBar { get; set; }

        [NinjaScriptProperty]
        [Range(0.10, 10.00)]
        [Browsable(false)]
        public double SpeedZThreshold { get; set; }

        [NinjaScriptProperty]
        [Range(0.10, 10.00)]
        [Browsable(false)]
        public double ExtremeSpeedZ { get; set; }

        [NinjaScriptProperty]
        [Range(0.10, 10.00)]
        [Browsable(false)]
        public double AccelerationZThreshold { get; set; }

        [NinjaScriptProperty]
        [Range(0.00, 1.00)]
        [Browsable(false)]
        public double MinimumImbalance { get; set; }

        [NinjaScriptProperty]
        [Browsable(false)]
        public bool RequireAnomalyForEverySignal { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, 1.0)]
        [Browsable(false)]
        public double AnomalyWeightMagnitude { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, 1.0)]
        [Browsable(false)]
        public double AnomalyWeightDirection { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, 1.0)]
        [Browsable(false)]
        public double AnomalyWeightAcceleration { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Browsable(false)]
        public int MinimumRangeTicks { get; set; }

        [NinjaScriptProperty]
        [Range(1, 50)]
        [Browsable(false)]
        public int ExcessLookback { get; set; }

        [NinjaScriptProperty]
        [Browsable(false)]
        public bool UseEfficiencyTrigger { get; set; }

        [NinjaScriptProperty]
        [Range(1, 20)]
        [Browsable(false)]
        public int EfficiencySmoothPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(10.0, 500.0)]
        [Browsable(false)]
        public double HistogramMaxAbsValue { get; set; }

        [NinjaScriptProperty]
        [Range(0.00, 5.00)]
        [Browsable(false)]
        public double HistogramMinDisplayZ { get; set; }

        [NinjaScriptProperty]
        [Browsable(false)]
        public bool CandleWidthHistogram { get; set; }

        [NinjaScriptProperty]
        [Browsable(false)]
        public bool PaintBars { get; set; }

        [NinjaScriptProperty]
        [Range(0, 20)]
        [Browsable(false)]
        public int MinimumBarsBetweenSignals { get; set; }

        [NinjaScriptProperty]
        [Browsable(false)]
        public bool SuppressRepeatedSameSignal { get; set; }

        #endregion
    }
}
 
namespace NinjaTrader.NinjaScript.Indicators.ILT_SUITE
{
	public partial class ILT_FLOWSPEED
	{
		public enum FlowSpeedDisplaySelection
		{
			BuyerSeller,
			Buyer,
			Seller,
			Anything
		}

		public enum FlowSpeedInitiativeMethod
		{
			BidVsAsk,
			UpTickDownTick
		}

		public enum FlowSpeedSizeFilter
		{
			NoFiltering,
			GreaterThanOrEqual,
			LessThanOrEqual,
			InBetween,
			RowOfSizes
		}

		private sealed class FlowSpeedBarData
		{
			public double High;
			public double Low;
			public double Mean;
			public double Sum;
			public int Samples;
			public double MeanHigh;
			public double StdHigh;
			public double MeanMean;
			public double StdMean;
		}

		private sealed class FlowSpeedStreamState
		{
			public readonly Dictionary<int, FlowSpeedBarData> Bars = new Dictionary<int, FlowSpeedBarData>();
			public int CurrentBar = -1;
			public int SessionFirstBar;
			public DateTime WindowStart = DateTime.MinValue;
			public int WindowCount;
			public double WindowVolume;
			public double CurrentSpeed;

			public void Clear()
			{
				Bars.Clear();
				CurrentBar = -1;
				SessionFirstBar = 0;
				WindowStart = DateTime.MinValue;
				WindowCount = 0;
				WindowVolume = 0.0;
				CurrentSpeed = 0.0;
			}

			// Reinicia únicamente el estado causal de la nueva sesión. Las barras ya
			// calculadas se conservan para que el histograma histórico pueda renderizarlas.
			public void BeginSession(int firstBarIndex)
			{
				CurrentBar = -1;
				SessionFirstBar = firstBarIndex;
				WindowStart = DateTime.MinValue;
				WindowCount = 0;
				WindowVolume = 0.0;
				CurrentSpeed = 0.0;
			}
		}

		private struct FlowSpeedPaintDecision
		{
			public int Priority;
			public bool OutlineOnly;
			public System.Windows.Media.Brush Brush;
		}

		private readonly object flowSpeedSync = new object();
		private FlowSpeedStreamState flowSpeedAny;
		private FlowSpeedStreamState flowSpeedBuyer;
		private FlowSpeedStreamState flowSpeedSeller;
		private double flowSpeedLastTradePrice;
		private int flowSpeedLastTickDirection;
		private readonly HashSet<int> flowSpeedPaintedBars = new HashSet<int>();
		private int flowSpeedLastRefreshMs = -100000000;
		private string flowSpeedCachedSizeRowsText;
		private readonly HashSet<long> flowSpeedCachedSizeRows = new HashSet<long>();

		private SharpDX.Direct2D1.RenderTarget flowSpeedRenderTarget;
		private SharpDX.Direct2D1.SolidColorBrush fsRiseDx;
		private SharpDX.Direct2D1.SolidColorBrush fsFallDx;
		private SharpDX.Direct2D1.SolidColorBrush fsFlatDx;
		private SharpDX.Direct2D1.SolidColorBrush fsHighCapDx;
		private SharpDX.Direct2D1.SolidColorBrush fsLowCapDx;
		private SharpDX.Direct2D1.SolidColorBrush fsLowSegmentDx;
		private SharpDX.Direct2D1.SolidColorBrush fsMeanDx;
		private SharpDX.Direct2D1.SolidColorBrush fsMeanHighDx;
		private SharpDX.Direct2D1.SolidColorBrush fsMeanLowDx;
		private SharpDX.Direct2D1.SolidColorBrush fsZeroDx;

		[Display(Name = "Filter by Initiative Side", Description = "Buyer + Seller muestra compras arriba de cero y ventas debajo. Anything incluye todos los prints.", Order = 2, GroupName = "07. Flow Speed - Cálculo")]
		public FlowSpeedDisplaySelection FlowSpeedDisplayMode { get; set; }

		[Range(2, 500)]
		[Display(Name = "Standard Deviation Period (bars)", Order = 3, GroupName = "07. Flow Speed - Cálculo")]
		public int FlowSpeedStandardDeviationPeriod { get; set; }

		[Range(0.0, 20.0)]
		[Display(Name = "Exceeding Speed Threshold (SD)", Order = 4, GroupName = "07. Flow Speed - Cálculo")]
		public double FlowSpeedExceedingThreshold { get; set; }

		[Range(0.0, 20.0)]
		[Display(Name = "Heightened Avg Speed Threshold (SD)", Order = 5, GroupName = "07. Flow Speed - Cálculo")]
		public double FlowSpeedMeanThreshold { get; set; }

		[Display(Name = "Filter by Size", Order = 6, GroupName = "07. Flow Speed - Cálculo")]
		public FlowSpeedSizeFilter FlowSpeedFilterBySize { get; set; }

		[Range(0.0, double.MaxValue)]
		[Display(Name = "Minimum Size", Order = 7, GroupName = "07. Flow Speed - Cálculo")]
		public double FlowSpeedMinimumSize { get; set; }

		[Range(0.0, double.MaxValue)]
		[Display(Name = "Maximum Size", Order = 8, GroupName = "07. Flow Speed - Cálculo")]
		public double FlowSpeedMaximumSize { get; set; }

		[Display(Name = "Row of Sizes", Description = "Lista separada por comas, por ejemplo: 1,2,5,10.", Order = 9, GroupName = "07. Flow Speed - Cálculo")]
		public string FlowSpeedSizeRows { get; set; }

		[Display(Name = "Initiative Side Estimation Technique", Order = 10, GroupName = "07. Flow Speed - Cálculo")]
		public FlowSpeedInitiativeMethod FlowSpeedInitiativeSide { get; set; }

		[Display(Name = "Data Source", Description = "El motor usa la serie interna de 1 tick para funcionar igual en velas de minutos, rango u otro tipo.", Order = 11, GroupName = "07. Flow Speed - Cálculo")]
		[ReadOnly(true)]
		public string FlowSpeedDataSource
		{
			get { return "Tick Series"; }
		}

		[Range(0, 100)]
		[Display(Name = "Bar Width (0 = automático)", Order = 1, GroupName = "08. Flow Speed - Apariencia")]
		public int FlowSpeedBarWidth { get; set; }

		[Range(1, 20)]
		[Display(Name = "Marker Thickness", Order = 2, GroupName = "08. Flow Speed - Apariencia")]
		public int FlowSpeedMarkerThickness { get; set; }

		[XmlIgnore]
		[Display(Name = "Increasing Speed Color", Order = 3, GroupName = "08. Flow Speed - Apariencia")]
		public System.Windows.Media.Brush FlowSpeedIncreasingBrush { get; set; }

		[Browsable(false)]
		public string FlowSpeedIncreasingBrushSerializable
		{
			get { return Serialize.BrushToString(FlowSpeedIncreasingBrush); }
			set { FlowSpeedIncreasingBrush = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Decreasing Speed Color", Order = 4, GroupName = "08. Flow Speed - Apariencia")]
		public System.Windows.Media.Brush FlowSpeedDecreasingBrush { get; set; }

		[Browsable(false)]
		public string FlowSpeedDecreasingBrushSerializable
		{
			get { return Serialize.BrushToString(FlowSpeedDecreasingBrush); }
			set { FlowSpeedDecreasingBrush = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Unchanged Speed Color", Order = 5, GroupName = "08. Flow Speed - Apariencia")]
		public System.Windows.Media.Brush FlowSpeedUnchangedBrush { get; set; }

		[Browsable(false)]
		public string FlowSpeedUnchangedBrushSerializable
		{
			get { return Serialize.BrushToString(FlowSpeedUnchangedBrush); }
			set { FlowSpeedUnchangedBrush = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Exceeding High Speed Cap", Order = 6, GroupName = "08. Flow Speed - Apariencia")]
		public System.Windows.Media.Brush FlowSpeedHighCapBrush { get; set; }

		[Browsable(false)]
		public string FlowSpeedHighCapBrushSerializable
		{
			get { return Serialize.BrushToString(FlowSpeedHighCapBrush); }
			set { FlowSpeedHighCapBrush = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Very Low Speed Cap", Order = 7, GroupName = "08. Flow Speed - Apariencia")]
		public System.Windows.Media.Brush FlowSpeedLowCapBrush { get; set; }

		[Browsable(false)]
		public string FlowSpeedLowCapBrushSerializable
		{
			get { return Serialize.BrushToString(FlowSpeedLowCapBrush); }
			set { FlowSpeedLowCapBrush = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Low Above Average Segment", Order = 8, GroupName = "08. Flow Speed - Apariencia")]
		public System.Windows.Media.Brush FlowSpeedLowSegmentBrush { get; set; }

		[Browsable(false)]
		public string FlowSpeedLowSegmentBrushSerializable
		{
			get { return Serialize.BrushToString(FlowSpeedLowSegmentBrush); }
			set { FlowSpeedLowSegmentBrush = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Mean Marker Color", Order = 9, GroupName = "08. Flow Speed - Apariencia")]
		public System.Windows.Media.Brush FlowSpeedMeanBrush { get; set; }

		[Browsable(false)]
		public string FlowSpeedMeanBrushSerializable
		{
			get { return Serialize.BrushToString(FlowSpeedMeanBrush); }
			set { FlowSpeedMeanBrush = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "High Mean Marker Color", Order = 10, GroupName = "08. Flow Speed - Apariencia")]
		public System.Windows.Media.Brush FlowSpeedMeanHighBrush { get; set; }

		[Browsable(false)]
		public string FlowSpeedMeanHighBrushSerializable
		{
			get { return Serialize.BrushToString(FlowSpeedMeanHighBrush); }
			set { FlowSpeedMeanHighBrush = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Low Mean Marker Color", Order = 11, GroupName = "08. Flow Speed - Apariencia")]
		public System.Windows.Media.Brush FlowSpeedMeanLowBrush { get; set; }

		[Browsable(false)]
		public string FlowSpeedMeanLowBrushSerializable
		{
			get { return Serialize.BrushToString(FlowSpeedMeanLowBrush); }
			set { FlowSpeedMeanLowBrush = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Zero Line Color", Order = 12, GroupName = "08. Flow Speed - Apariencia")]
		public System.Windows.Media.Brush FlowSpeedZeroBrush { get; set; }

		[Browsable(false)]
		public string FlowSpeedZeroBrushSerializable
		{
			get { return Serialize.BrushToString(FlowSpeedZeroBrush); }
			set { FlowSpeedZeroBrush = Serialize.StringToBrush(value); }
		}

		[Display(Name = "Enable Color Painting of Price Chart", Order = 1, GroupName = "09. Flow Speed - Price Chart Painting")]
		public bool EnableFlowSpeedPricePainting { get; set; }

		[XmlIgnore]
		[Display(Name = "Exceeding Speed Color", Order = 2, GroupName = "09. Flow Speed - Price Chart Painting")]
		public System.Windows.Media.Brush FlowSpeedExceedingCandleBrush { get; set; }

		[Browsable(false)]
		public string FlowSpeedExceedingCandleBrushSerializable
		{
			get { return Serialize.BrushToString(FlowSpeedExceedingCandleBrush); }
			set { FlowSpeedExceedingCandleBrush = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Very Exceeding Speed Color", Order = 3, GroupName = "09. Flow Speed - Price Chart Painting")]
		public System.Windows.Media.Brush FlowSpeedVeryExceedingCandleBrush { get; set; }

		[Browsable(false)]
		public string FlowSpeedVeryExceedingCandleBrushSerializable
		{
			get { return Serialize.BrushToString(FlowSpeedVeryExceedingCandleBrush); }
			set { FlowSpeedVeryExceedingCandleBrush = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Low Speed Color", Order = 4, GroupName = "09. Flow Speed - Price Chart Painting")]
		public System.Windows.Media.Brush FlowSpeedLowCandleBrush { get; set; }

		[Browsable(false)]
		public string FlowSpeedLowCandleBrushSerializable
		{
			get { return Serialize.BrushToString(FlowSpeedLowCandleBrush); }
			set { FlowSpeedLowCandleBrush = Serialize.StringToBrush(value); }
		}

		private void SetFlowSpeedDefaults()
		{
			FlowSpeedDisplayMode = FlowSpeedDisplaySelection.BuyerSeller;
			FlowSpeedStandardDeviationPeriod = 20;
			FlowSpeedExceedingThreshold = 2.0;
			FlowSpeedMeanThreshold = 2.0;
			FlowSpeedFilterBySize = FlowSpeedSizeFilter.NoFiltering;
			FlowSpeedMinimumSize = 1.0;
			FlowSpeedMaximumSize = 100.0;
			FlowSpeedSizeRows = "";
			FlowSpeedInitiativeSide = FlowSpeedInitiativeMethod.BidVsAsk;
			FlowSpeedBarWidth = 0;
			FlowSpeedMarkerThickness = 3;
			FlowSpeedIncreasingBrush = MakeFrozenBrush(64, 80, 64);
			FlowSpeedDecreasingBrush = MakeFrozenBrush(80, 64, 64);
			FlowSpeedUnchangedBrush = Brushes.DimGray;
			FlowSpeedHighCapBrush = Brushes.Red;
			FlowSpeedLowCapBrush = Brushes.Blue;
			FlowSpeedLowSegmentBrush = Brushes.Transparent;
			FlowSpeedMeanBrush = Brushes.Black;
			FlowSpeedMeanHighBrush = Brushes.Black;
			FlowSpeedMeanLowBrush = Brushes.Black;
			FlowSpeedZeroBrush = Brushes.DimGray;
			EnableFlowSpeedPricePainting = true;
			FlowSpeedExceedingCandleBrush = Brushes.Yellow;
			FlowSpeedVeryExceedingCandleBrush = Brushes.White;
			FlowSpeedLowCandleBrush = Brushes.Aqua;
		}

		private static System.Windows.Media.Brush MakeFrozenBrush(byte red, byte green, byte blue)
		{
			System.Windows.Media.SolidColorBrush result = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(red, green, blue));
			if (result.CanFreeze)
			{
				result.Freeze();
			}
			return result;
		}

		private void InitializeFlowSpeedEngine()
		{
			lock (flowSpeedSync)
			{
				flowSpeedAny = new FlowSpeedStreamState();
				flowSpeedBuyer = new FlowSpeedStreamState();
				flowSpeedSeller = new FlowSpeedStreamState();
				flowSpeedLastTradePrice = 0.0;
				flowSpeedLastTickDirection = 0;
				flowSpeedPaintedBars.Clear();
				flowSpeedCachedSizeRowsText = null;
				flowSpeedCachedSizeRows.Clear();
			}
			flowSpeedLastRefreshMs = -100000000;
			DisposeFlowSpeedRenderResources();
		}

		private void DisposeFlowSpeedEngine()
		{
			lock (flowSpeedSync)
			{
				if (flowSpeedAny != null)
				{
					flowSpeedAny.Clear();
				}
				if (flowSpeedBuyer != null)
				{
					flowSpeedBuyer.Clear();
				}
				if (flowSpeedSeller != null)
				{
					flowSpeedSeller.Clear();
				}
				flowSpeedPaintedBars.Clear();
			}
			DisposeFlowSpeedRenderResources();
		}

		private void ProcessFlowSpeedTrade(double tradePrice, double tradeVolume, DateTime tradeTime, double bid, double ask, int primaryBarIndex)
		{
			if (primaryBarIndex < 0 || double.IsNaN(tradePrice) || double.IsInfinity(tradePrice) || tradePrice <= 0.0)
			{
				return;
			}
			bool refresh = false;
			lock (flowSpeedSync)
			{
				EnsureFlowSpeedStates();
				bool firstBarOfSession = false;
				try
				{
					firstBarOfSession = base.BarsArray[0].IsFirstBarOfSessionByIndex(primaryBarIndex);
				}
				catch
				{
				}
				if (flowSpeedAny.CurrentBar != primaryBarIndex)
				{
					if (firstBarOfSession)
					{
						flowSpeedLastTradePrice = 0.0;
						flowSpeedLastTickDirection = 0;
					}
					BeginFlowSpeedBar(flowSpeedAny, primaryBarIndex, firstBarOfSession);
					BeginFlowSpeedBar(flowSpeedBuyer, primaryBarIndex, firstBarOfSession);
					BeginFlowSpeedBar(flowSpeedSeller, primaryBarIndex, firstBarOfSession);
				}

				int side = ClassifyFlowSpeedSide(tradePrice, bid, ask);
				if (!PassesFlowSpeedSizeFilter(tradeVolume))
				{
					return;
				}

				AccumulateFlowSpeedEvent(flowSpeedAny, primaryBarIndex, tradeTime, tradeVolume);
				if (side > 0)
				{
					AccumulateFlowSpeedEvent(flowSpeedBuyer, primaryBarIndex, tradeTime, tradeVolume);
				}
				else if (side < 0)
				{
					AccumulateFlowSpeedEvent(flowSpeedSeller, primaryBarIndex, tradeTime, tradeVolume);
				}
				ApplyFlowSpeedPricePaintingLocked(primaryBarIndex);
				refresh = true;
			}

			if (refresh && base.State == State.Realtime)
			{
				int tick = Environment.TickCount;
				if (tick - flowSpeedLastRefreshMs >= 60)
				{
					flowSpeedLastRefreshMs = tick;
					try
					{
						ForceRefresh();
					}
					catch
					{
					}
				}
			}
		}

		private void EnsureFlowSpeedStates()
		{
			if (flowSpeedAny == null)
			{
				flowSpeedAny = new FlowSpeedStreamState();
			}
			if (flowSpeedBuyer == null)
			{
				flowSpeedBuyer = new FlowSpeedStreamState();
			}
			if (flowSpeedSeller == null)
			{
				flowSpeedSeller = new FlowSpeedStreamState();
			}
		}

		private void BeginFlowSpeedBar(FlowSpeedStreamState stream, int barIndex, bool firstBarOfSession)
		{
			if (firstBarOfSession)
			{
				stream.BeginSession(barIndex);
			}
			else if (stream.CurrentBar < 0)
			{
				stream.SessionFirstBar = barIndex;
			}

			FlowSpeedBarData bar = new FlowSpeedBarData();
			CalculateFlowSpeedBaseline(stream, barIndex, bar);
			if (stream.WindowCount > 0)
			{
				bar.High = stream.CurrentSpeed;
				bar.Low = stream.CurrentSpeed;
				bar.Mean = stream.CurrentSpeed;
				bar.Sum = stream.CurrentSpeed;
				bar.Samples = 1;
			}
			stream.Bars[barIndex] = bar;
			stream.CurrentBar = barIndex;
		}

		private void CalculateFlowSpeedBaseline(FlowSpeedStreamState stream, int barIndex, FlowSpeedBarData target)
		{
			int period = Math.Max(2, FlowSpeedStandardDeviationPeriod);
			int first = Math.Max(stream.SessionFirstBar, barIndex - period);
			int count = 0;
			double sumHigh = 0.0;
			double sumMean = 0.0;
			for (int i = first; i < barIndex; i++)
			{
				FlowSpeedBarData prior;
				if (stream.Bars.TryGetValue(i, out prior))
				{
					sumHigh += prior.High;
					sumMean += prior.Mean;
					count++;
				}
			}
			if (count == 0)
			{
				return;
			}
			target.MeanHigh = sumHigh / count;
			target.MeanMean = sumMean / count;
			if (count < 2)
			{
				return;
			}
			double ssHigh = 0.0;
			double ssMean = 0.0;
			for (int i = first; i < barIndex; i++)
			{
				FlowSpeedBarData prior;
				if (stream.Bars.TryGetValue(i, out prior))
				{
					double dh = prior.High - target.MeanHigh;
					double dm = prior.Mean - target.MeanMean;
					ssHigh += dh * dh;
					ssMean += dm * dm;
				}
			}
			target.StdHigh = Math.Sqrt(ssHigh / (count - 1));
			target.StdMean = Math.Sqrt(ssMean / (count - 1));
		}

		private void AccumulateFlowSpeedEvent(FlowSpeedStreamState stream, int barIndex, DateTime tradeTime, double tradeVolume)
		{
			FlowSpeedBarData bar;
			if (!stream.Bars.TryGetValue(barIndex, out bar))
			{
				return;
			}
			if (stream.WindowStart == DateTime.MinValue)
			{
				stream.WindowStart = tradeTime;
			}
			stream.WindowCount++;
			if (!double.IsNaN(tradeVolume) && !double.IsInfinity(tradeVolume) && tradeVolume > 0.0)
			{
				stream.WindowVolume += tradeVolume;
			}
			double interval = Math.Max(0.001, CumulationIntervalMs / 1000.0);
			double elapsed = (tradeTime - stream.WindowStart).TotalSeconds;
			if (elapsed < 0.0)
			{
				stream.WindowStart = tradeTime;
				stream.WindowCount = 1;
				stream.WindowVolume = Math.Max(0.0, tradeVolume);
				elapsed = 0.0;
			}
			if (elapsed < interval)
			{
				stream.CurrentSpeed = stream.WindowCount / interval;
				return;
			}

			stream.CurrentSpeed = stream.WindowCount / Math.Max(0.001, elapsed);
			CommitFlowSpeedSample(bar, stream.CurrentSpeed);
			stream.WindowStart = tradeTime;
			stream.WindowCount = 0;
			stream.WindowVolume = 0.0;
		}

		private static void CommitFlowSpeedSample(FlowSpeedBarData bar, double speed)
		{
			if (bar.Samples == 0)
			{
				bar.High = speed;
				bar.Low = speed;
				bar.Sum = speed;
				bar.Samples = 1;
				bar.Mean = speed;
				return;
			}
			bar.High = Math.Max(bar.High, speed);
			bar.Low = Math.Min(bar.Low, speed);
			bar.Sum += speed;
			bar.Samples++;
			bar.Mean = bar.Sum / bar.Samples;
		}

		private int ClassifyFlowSpeedSide(double tradePrice, double bid, double ask)
		{
			int tickDirection = 0;
			if (flowSpeedLastTradePrice > 0.0)
			{
				if (tradePrice > flowSpeedLastTradePrice)
				{
					tickDirection = 1;
				}
				else if (tradePrice < flowSpeedLastTradePrice)
				{
					tickDirection = -1;
				}
				else
				{
					tickDirection = flowSpeedLastTickDirection;
				}
			}
			if (tickDirection != 0)
			{
				flowSpeedLastTickDirection = tickDirection;
			}
			flowSpeedLastTradePrice = tradePrice;

			if (FlowSpeedInitiativeSide == FlowSpeedInitiativeMethod.UpTickDownTick)
			{
				return tickDirection;
			}
			if (double.IsNaN(bid) || double.IsInfinity(bid) || bid <= 0.0)
			{
				bid = lastBid;
			}
			if (double.IsNaN(ask) || double.IsInfinity(ask) || ask <= 0.0)
			{
				ask = lastAsk;
			}
			if (bid > 0.0 && ask > 0.0)
			{
				if (ask > bid)
				{
					if (tradePrice >= ask)
					{
						return 1;
					}
					if (tradePrice <= bid)
					{
						return -1;
					}
					return 0;
				}
				if (tradePrice > ask)
				{
					return 1;
				}
				if (tradePrice < bid)
				{
					return -1;
				}
			}
			return tickDirection;
		}

		private bool PassesFlowSpeedSizeFilter(double size)
		{
			if (FlowSpeedFilterBySize == FlowSpeedSizeFilter.NoFiltering)
			{
				return true;
			}
			if (double.IsNaN(size) || double.IsInfinity(size) || size < 0.0)
			{
				return false;
			}
			switch (FlowSpeedFilterBySize)
			{
			case FlowSpeedSizeFilter.GreaterThanOrEqual:
				return size >= FlowSpeedMinimumSize;
			case FlowSpeedSizeFilter.LessThanOrEqual:
				return size <= FlowSpeedMaximumSize;
			case FlowSpeedSizeFilter.InBetween:
				return size >= Math.Min(FlowSpeedMinimumSize, FlowSpeedMaximumSize) && size <= Math.Max(FlowSpeedMinimumSize, FlowSpeedMaximumSize);
			case FlowSpeedSizeFilter.RowOfSizes:
				EnsureFlowSpeedSizeRows();
				return flowSpeedCachedSizeRows.Contains(Convert.ToInt64(Math.Round(size, MidpointRounding.AwayFromZero)));
			default:
				return true;
			}
		}

		private void EnsureFlowSpeedSizeRows()
		{
			string text = FlowSpeedSizeRows ?? "";
			if (text == flowSpeedCachedSizeRowsText)
			{
				return;
			}
			flowSpeedCachedSizeRowsText = text;
			flowSpeedCachedSizeRows.Clear();
			string[] parts = text.Split(new char[] { ',', ';', ' ', '\t', '|' }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < parts.Length; i++)
			{
				double value;
				if (double.TryParse(parts[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value) || double.TryParse(parts[i], out value))
				{
					flowSpeedCachedSizeRows.Add(Convert.ToInt64(Math.Round(value, MidpointRounding.AwayFromZero)));
				}
			}
		}

		private void RefreshFlowSpeedVisuals(int primaryBarIndex)
		{
			if (primaryBarIndex < 0)
			{
				return;
			}
			lock (flowSpeedSync)
			{
				if (flowSpeedAny == null)
				{
					return;
				}
				ApplyFlowSpeedPricePaintingLocked(primaryBarIndex);
			}
		}

		private void SetFlowSpeedPlotValues(int barIndex, int barsAgo)
		{
			if (barsAgo < 0 || base.Values == null || base.Values.Length < 2)
			{
				return;
			}
			double first = 0.0;
			double second = double.NaN;
			lock (flowSpeedSync)
			{
				if (flowSpeedAny == null)
				{
					base.Values[0][barsAgo] = first;
					base.Values[1][barsAgo] = second;
					return;
				}
				FlowSpeedBarData a;
				FlowSpeedBarData b;
				switch (FlowSpeedDisplayMode)
				{
				case FlowSpeedDisplaySelection.BuyerSeller:
					if (flowSpeedBuyer.Bars.TryGetValue(barIndex, out a))
					{
						first = a.High;
					}
					if (flowSpeedSeller.Bars.TryGetValue(barIndex, out b))
					{
						second = 0.0 - b.High;
					}
					break;
				case FlowSpeedDisplaySelection.Buyer:
					if (flowSpeedBuyer.Bars.TryGetValue(barIndex, out a))
					{
						first = a.High;
						second = a.Mean;
					}
					break;
				case FlowSpeedDisplaySelection.Seller:
					if (flowSpeedSeller.Bars.TryGetValue(barIndex, out a))
					{
						first = 0.0 - a.High;
						second = 0.0 - a.Mean;
					}
					break;
				default:
					if (flowSpeedAny.Bars.TryGetValue(barIndex, out a))
					{
						first = a.High;
						second = a.Mean;
					}
					break;
				}
			}
			base.Values[0][barsAgo] = first;
			base.Values[1][barsAgo] = second;
		}

		private FlowSpeedPaintDecision GetFlowSpeedPaintDecision(FlowSpeedBarData bar)
		{
			FlowSpeedPaintDecision result = new FlowSpeedPaintDecision();
			if (bar == null || bar.High <= 0.0)
			{
				return result;
			}
			double upperHigh = bar.MeanHigh + FlowSpeedExceedingThreshold * bar.StdHigh;
			double lowerHigh = bar.MeanHigh - FlowSpeedExceedingThreshold * bar.StdHigh;
			double upperMean = bar.MeanMean + FlowSpeedMeanThreshold * bar.StdMean;
			double lowerMean = bar.MeanMean - FlowSpeedMeanThreshold * bar.StdMean;
			bool a = bar.High > upperHigh;
			bool b = bar.Low > bar.MeanMean;
			bool c = bar.High < lowerHigh;
			bool d = bar.Mean > upperMean;
			bool e = bar.Mean < lowerMean;
			int score = (a ? 1 : 0) + (b ? 1 : 0) + (d ? 1 : 0);
			if (score >= 2)
			{
				result.Priority = 3;
				result.Brush = FlowSpeedVeryExceedingCandleBrush;
			}
			else if (score == 1)
			{
				result.Priority = 2;
				result.Brush = FlowSpeedExceedingCandleBrush;
				result.OutlineOnly = d && !a && !b;
			}
			else if (c || e)
			{
				result.Priority = 1;
				result.Brush = FlowSpeedLowCandleBrush;
			}
			return result;
		}

		private static FlowSpeedPaintDecision StrongerFlowSpeedDecision(FlowSpeedPaintDecision left, FlowSpeedPaintDecision right)
		{
			if (right.Priority > left.Priority)
			{
				return right;
			}
			if (right.Priority == left.Priority && right.Priority > 0 && left.OutlineOnly && !right.OutlineOnly)
			{
				return right;
			}
			return left;
		}

		private void ApplyFlowSpeedPricePaintingLocked(int barIndex)
		{
			if (flowSpeedAny == null)
			{
				return;
			}
			FlowSpeedPaintDecision decision = new FlowSpeedPaintDecision();
			FlowSpeedBarData bar;
			if (FlowSpeedDisplayMode == FlowSpeedDisplaySelection.BuyerSeller || FlowSpeedDisplayMode == FlowSpeedDisplaySelection.Buyer)
			{
				if (flowSpeedBuyer.Bars.TryGetValue(barIndex, out bar))
				{
					decision = StrongerFlowSpeedDecision(decision, GetFlowSpeedPaintDecision(bar));
				}
			}
			if (FlowSpeedDisplayMode == FlowSpeedDisplaySelection.BuyerSeller || FlowSpeedDisplayMode == FlowSpeedDisplaySelection.Seller)
			{
				if (flowSpeedSeller.Bars.TryGetValue(barIndex, out bar))
				{
					decision = StrongerFlowSpeedDecision(decision, GetFlowSpeedPaintDecision(bar));
				}
			}
			if (FlowSpeedDisplayMode == FlowSpeedDisplaySelection.Anything && flowSpeedAny.Bars.TryGetValue(barIndex, out bar))
			{
				decision = GetFlowSpeedPaintDecision(bar);
			}

			if (flowSpeedPaintedBars.Contains(barIndex))
			{
				base.BarBrushes.Set(barIndex, null);
				base.CandleOutlineBrushes.Set(barIndex, null);
				flowSpeedPaintedBars.Remove(barIndex);
			}
			if (!EnableFlowSpeedPricePainting || decision.Priority <= 0 || decision.Brush == null)
			{
				return;
			}
			base.CandleOutlineBrushes.Set(barIndex, decision.Brush);
			if (!decision.OutlineOnly)
			{
				base.BarBrushes.Set(barIndex, decision.Brush);
			}
			flowSpeedPaintedBars.Add(barIndex);
		}

		private void ForgetFlowSpeedPaintedBar(int absoluteBarIndex)
		{
			lock (flowSpeedSync)
			{
				flowSpeedPaintedBars.Remove(absoluteBarIndex);
			}
		}

		private void CalculateFlowSpeedMinMax()
		{
			if (!ShowHistogramPanel)
			{
				base.MinValue = -1.0;
				base.MaxValue = 1.0;
				return;
			}
			// El rango vertical pertenece a la ventana VISIBLE, no a todo el histórico.
			// Así, un pico antiguo que quedó fuera de pantalla no comprime las barras
			// actuales cuando el usuario hace zoom, desplaza o cambia el ancho de vela.
			int fromIndex = 0;
			int toIndex = Math.Max(0, CurrentBars[0]);
			if (base.ChartBars != null)
			{
				fromIndex = Math.Max(0, base.ChartBars.FromIndex);
				toIndex = Math.Min(toIndex, base.ChartBars.ToIndex);
			}
			if (toIndex < fromIndex)
			{
				fromIndex = 0;
				toIndex = Math.Max(0, CurrentBars[0]);
			}

			double maximum = 1.0;
			lock (flowSpeedSync)
			{
				if (flowSpeedAny != null)
				{
					if (FlowSpeedDisplayMode == FlowSpeedDisplaySelection.BuyerSeller || FlowSpeedDisplayMode == FlowSpeedDisplaySelection.Buyer)
					{
						maximum = Math.Max(maximum, MaximumVisibleFlowSpeed(flowSpeedBuyer, fromIndex, toIndex));
					}
					if (FlowSpeedDisplayMode == FlowSpeedDisplaySelection.BuyerSeller || FlowSpeedDisplayMode == FlowSpeedDisplaySelection.Seller)
					{
						maximum = Math.Max(maximum, MaximumVisibleFlowSpeed(flowSpeedSeller, fromIndex, toIndex));
					}
					if (FlowSpeedDisplayMode == FlowSpeedDisplaySelection.Anything)
					{
						maximum = Math.Max(maximum, MaximumVisibleFlowSpeed(flowSpeedAny, fromIndex, toIndex));
					}
				}
			}
			maximum *= 1.1;
			if (FlowSpeedDisplayMode == FlowSpeedDisplaySelection.Seller)
			{
				base.MinValue = 0.0 - maximum;
				base.MaxValue = 0.0;
			}
			else if (FlowSpeedDisplayMode == FlowSpeedDisplaySelection.BuyerSeller)
			{
				base.MinValue = 0.0 - maximum;
				base.MaxValue = maximum;
			}
			else
			{
				base.MinValue = 0.0;
				base.MaxValue = maximum;
			}
		}

		private static double MaximumVisibleFlowSpeed(FlowSpeedStreamState stream, int fromIndex, int toIndex)
		{
			double result = 0.0;
			if (stream == null || toIndex < fromIndex)
				return result;

			for (int barIndex = fromIndex; barIndex <= toIndex; barIndex++)
			{
				FlowSpeedBarData bar;
				if (stream.Bars.TryGetValue(barIndex, out bar) && bar != null)
					result = Math.Max(result, bar.High);
			}
			return result;
		}

		private void RenderFlowSpeed(ChartControl chartControl, ChartScale chartScale)
		{
			if (base.RenderTarget == null || chartControl == null || chartScale == null || base.ChartBars == null)
			{
				return;
			}
			// Crear los recursos sobre el RenderTarget del frame actual evita
			// D2DERR_WRONG_RESOURCE_DOMAIN al cambiar panel, monitor, escala o workspace.
			DisposeFlowSpeedRenderResources();
			EnsureFlowSpeedRenderResources();
			if (fsRiseDx == null)
			{
				return;
			}
			int fromIndex = base.ChartBars.FromIndex;
			int toIndex = base.ChartBars.ToIndex;
			if (toIndex < fromIndex)
			{
				return;
			}
			float zeroY = chartScale.GetYByValue(0.0);
			ChartPanel panel = chartControl.ChartPanels[chartScale.PanelIndex];
			SharpDX.Direct2D1.AntialiasMode previousMode = base.RenderTarget.AntialiasMode;
			base.RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.Aliased;
			try
			{
				base.RenderTarget.DrawLine(new SharpDX.Vector2(panel.X, zeroY), new SharpDX.Vector2(panel.X + panel.W, zeroY), fsZeroDx, 1f);
				lock (flowSpeedSync)
				{
					if (flowSpeedAny == null)
					{
						return;
					}
					for (int barIndex = fromIndex; barIndex <= toIndex; barIndex++)
					{
						float x = chartControl.GetXByBarIndex(base.ChartBars, barIndex);
						float width = ResolveFlowSpeedBarWidth(chartControl, fromIndex, toIndex, barIndex, x);
						if (FlowSpeedDisplayMode == FlowSpeedDisplaySelection.BuyerSeller || FlowSpeedDisplayMode == FlowSpeedDisplaySelection.Buyer)
						{
							RenderFlowSpeedStreamBar(chartScale, flowSpeedBuyer, barIndex, x, width, 1.0);
						}
						if (FlowSpeedDisplayMode == FlowSpeedDisplaySelection.BuyerSeller || FlowSpeedDisplayMode == FlowSpeedDisplaySelection.Seller)
						{
							RenderFlowSpeedStreamBar(chartScale, flowSpeedSeller, barIndex, x, width, -1.0);
						}
						if (FlowSpeedDisplayMode == FlowSpeedDisplaySelection.Anything)
						{
							RenderFlowSpeedStreamBar(chartScale, flowSpeedAny, barIndex, x, width, 1.0);
						}
					}
				}
			}
			finally
			{
				base.RenderTarget.AntialiasMode = previousMode;
				DisposeFlowSpeedRenderResources();
			}
		}

		private float ResolveFlowSpeedBarWidth(ChartControl chartControl, int fromIndex, int toIndex, int barIndex, float x)
		{
			if (FlowSpeedBarWidth > 0)
			{
				return Math.Max(1f, FlowSpeedBarWidth);
			}
			float spacing = 6f;
			if (barIndex < toIndex)
			{
				spacing = Math.Abs(chartControl.GetXByBarIndex(base.ChartBars, barIndex + 1) - x);
			}
			else if (barIndex > fromIndex)
			{
				spacing = Math.Abs(x - chartControl.GetXByBarIndex(base.ChartBars, barIndex - 1));
			}
			if (spacing < 1f)
			{
				spacing = 6f;
			}
			return Math.Max(1f, spacing * 0.66f);
		}

		private void RenderFlowSpeedStreamBar(ChartScale chartScale, FlowSpeedStreamState stream, int barIndex, float x, float width, double sign)
		{
			FlowSpeedBarData bar;
			if (!stream.Bars.TryGetValue(barIndex, out bar) || bar.High <= 0.0)
			{
				return;
			}
			FlowSpeedBarData previous;
			double previousHigh = stream.Bars.TryGetValue(barIndex - 1, out previous) ? previous.High : bar.High;
			SharpDX.Direct2D1.SolidColorBrush bodyBrush = fsFlatDx;
			if (bar.High > previousHigh)
			{
				bodyBrush = fsRiseDx;
			}
			else if (bar.High < previousHigh)
			{
				bodyBrush = fsFallDx;
			}

			float left = x - width * 0.5f;
			FillFlowSpeedRange(chartScale, left, width, sign * bar.Low, sign * bar.High, bodyBrush);
			double upperHigh = bar.MeanHigh + FlowSpeedExceedingThreshold * bar.StdHigh;
			double lowerHigh = bar.MeanHigh - FlowSpeedExceedingThreshold * bar.StdHigh;
			double upperMean = bar.MeanMean + FlowSpeedMeanThreshold * bar.StdMean;
			double lowerMean = bar.MeanMean - FlowSpeedMeanThreshold * bar.StdMean;
			bool a = bar.High > upperHigh;
			bool b = bar.Low > bar.MeanMean;
			bool c = bar.High < lowerHigh;
			bool d = bar.Mean > upperMean;
			bool e = bar.Mean < lowerMean;
			if (b && FlowSpeedLowSegmentBrush != null && FlowSpeedLowSegmentBrush.Opacity > 0.0)
			{
				FillFlowSpeedRange(chartScale, left, width, 0.0, sign * bar.Low, fsLowSegmentDx);
			}
			if (a)
			{
				double capStart = Math.Max(bar.Low, Math.Min(bar.High, upperHigh));
				FillFlowSpeedRange(chartScale, left, width, sign * capStart, sign * bar.High, fsHighCapDx);
			}
			if (c)
			{
				double capLength = Math.Min(Math.Max(0.0, lowerHigh - bar.High), Math.Max(0.0, bar.High - bar.Low));
				double capEnd = Math.Max(bar.Low, bar.High - capLength);
				if (capLength <= 0.0)
				{
					capEnd = bar.Low;
				}
				FillFlowSpeedRange(chartScale, left, width, sign * capEnd, sign * bar.High, fsLowCapDx);
			}
			SharpDX.Direct2D1.SolidColorBrush marker = d ? fsMeanHighDx : (e ? fsMeanLowDx : fsMeanDx);
			float meanY = chartScale.GetYByValue(sign * bar.Mean);
			base.RenderTarget.DrawLine(new SharpDX.Vector2(left, meanY), new SharpDX.Vector2(left + width, meanY), marker, Math.Max(1f, FlowSpeedMarkerThickness));
		}

		private void FillFlowSpeedRange(ChartScale chartScale, float left, float width, double firstValue, double secondValue, SharpDX.Direct2D1.SolidColorBrush brush)
		{
			if (brush == null)
			{
				return;
			}
			float y1 = chartScale.GetYByValue(firstValue);
			float y2 = chartScale.GetYByValue(secondValue);
			float top = Math.Min(y1, y2);
			float height = Math.Max(1f, Math.Abs(y2 - y1));
			base.RenderTarget.FillRectangle(new SharpDX.RectangleF(left, top, Math.Max(1f, width), height), brush);
		}

		private void EnsureFlowSpeedRenderResources()
		{
			if (flowSpeedRenderTarget == base.RenderTarget && fsRiseDx != null)
			{
				return;
			}
			DisposeFlowSpeedRenderResources();
			if (base.RenderTarget == null)
			{
				return;
			}
			flowSpeedRenderTarget = base.RenderTarget;
			fsRiseDx = CreateFlowSpeedDxBrush(FlowSpeedIncreasingBrush);
			fsFallDx = CreateFlowSpeedDxBrush(FlowSpeedDecreasingBrush);
			fsFlatDx = CreateFlowSpeedDxBrush(FlowSpeedUnchangedBrush);
			fsHighCapDx = CreateFlowSpeedDxBrush(FlowSpeedHighCapBrush);
			fsLowCapDx = CreateFlowSpeedDxBrush(FlowSpeedLowCapBrush);
			fsLowSegmentDx = CreateFlowSpeedDxBrush(FlowSpeedLowSegmentBrush);
			fsMeanDx = CreateFlowSpeedDxBrush(FlowSpeedMeanBrush);
			fsMeanHighDx = CreateFlowSpeedDxBrush(FlowSpeedMeanHighBrush);
			fsMeanLowDx = CreateFlowSpeedDxBrush(FlowSpeedMeanLowBrush);
			fsZeroDx = CreateFlowSpeedDxBrush(FlowSpeedZeroBrush);
		}

		private SharpDX.Direct2D1.SolidColorBrush CreateFlowSpeedDxBrush(System.Windows.Media.Brush source)
		{
			System.Windows.Media.SolidColorBrush solid = source as System.Windows.Media.SolidColorBrush;
			System.Windows.Media.Color color = solid == null ? System.Windows.Media.Colors.Transparent : solid.Color;
			float opacity = source == null ? 0f : (float)Math.Max(0.0, Math.Min(1.0, source.Opacity));
			SharpDX.Color4 dxColor = new SharpDX.Color4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f * opacity);
			return new SharpDX.Direct2D1.SolidColorBrush(base.RenderTarget, dxColor);
		}

		private void DisposeFlowSpeedRenderResources()
		{
			DisposeFlowSpeedDxBrush(ref fsRiseDx);
			DisposeFlowSpeedDxBrush(ref fsFallDx);
			DisposeFlowSpeedDxBrush(ref fsFlatDx);
			DisposeFlowSpeedDxBrush(ref fsHighCapDx);
			DisposeFlowSpeedDxBrush(ref fsLowCapDx);
			DisposeFlowSpeedDxBrush(ref fsLowSegmentDx);
			DisposeFlowSpeedDxBrush(ref fsMeanDx);
			DisposeFlowSpeedDxBrush(ref fsMeanHighDx);
			DisposeFlowSpeedDxBrush(ref fsMeanLowDx);
			DisposeFlowSpeedDxBrush(ref fsZeroDx);
			flowSpeedRenderTarget = null;
		}

		private static void DisposeFlowSpeedDxBrush(ref SharpDX.Direct2D1.SolidColorBrush brush)
		{
			if (brush != null)
			{
				brush.Dispose();
				brush = null;
			}
		}
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private ILT_SUITE.ILT_FLOWSPEED[] cacheILT_FLOWSPEED;
		public ILT_SUITE.ILT_FLOWSPEED ILT_FLOWSPEED(int operationalMode, int minimumTradeVolume, int excessConfirmWindow, double efficiencyTrigger, bool useAnomalyGuideFilter, double anomalyGuideFilterLevel, bool useAdaptiveAnomalyLevel, double anomalyLevelPercentile, int anomalyLevelLookback, double anomalyLevelCeiling, bool showHistogramPanel, bool showEfficiencyLine, bool enable3DHistogram, int histogram3DMaxVisibleBars, bool compatibilityRenderMode, bool autoFallbackToNativeRender, bool showArrows, bool showTextLabels, bool paintExcessBars, bool paintMomentumBars, bool paintContinuationBars, bool paintRotationBars, int markerOffsetTicks, int signalLabelFontSize, bool signalLabelBold, string acelBuyLabel, string acelSellLabel, string excessBuyLabel, string excessSellLabel, string rotationBuyLabel, string rotationSellLabel, bool useOperationalModePresets, int cumulationIntervalMs, int speedLookback, bool classifyOnClosedBar, double speedZThreshold, double extremeSpeedZ, double accelerationZThreshold, double minimumImbalance, bool requireAnomalyForEverySignal, double anomalyWeightMagnitude, double anomalyWeightDirection, double anomalyWeightAcceleration, int minimumRangeTicks, int excessLookback, bool useEfficiencyTrigger, int efficiencySmoothPeriod, double histogramMaxAbsValue, double histogramMinDisplayZ, bool candleWidthHistogram, bool paintBars, int minimumBarsBetweenSignals, bool suppressRepeatedSameSignal)
		{
			return ILT_FLOWSPEED(Input, operationalMode, minimumTradeVolume, excessConfirmWindow, efficiencyTrigger, useAnomalyGuideFilter, anomalyGuideFilterLevel, useAdaptiveAnomalyLevel, anomalyLevelPercentile, anomalyLevelLookback, anomalyLevelCeiling, showHistogramPanel, showEfficiencyLine, enable3DHistogram, histogram3DMaxVisibleBars, compatibilityRenderMode, autoFallbackToNativeRender, showArrows, showTextLabels, paintExcessBars, paintMomentumBars, paintContinuationBars, paintRotationBars, markerOffsetTicks, signalLabelFontSize, signalLabelBold, acelBuyLabel, acelSellLabel, excessBuyLabel, excessSellLabel, rotationBuyLabel, rotationSellLabel, useOperationalModePresets, cumulationIntervalMs, speedLookback, classifyOnClosedBar, speedZThreshold, extremeSpeedZ, accelerationZThreshold, minimumImbalance, requireAnomalyForEverySignal, anomalyWeightMagnitude, anomalyWeightDirection, anomalyWeightAcceleration, minimumRangeTicks, excessLookback, useEfficiencyTrigger, efficiencySmoothPeriod, histogramMaxAbsValue, histogramMinDisplayZ, candleWidthHistogram, paintBars, minimumBarsBetweenSignals, suppressRepeatedSameSignal);
		}

		public ILT_SUITE.ILT_FLOWSPEED ILT_FLOWSPEED(ISeries<double> input, int operationalMode, int minimumTradeVolume, int excessConfirmWindow, double efficiencyTrigger, bool useAnomalyGuideFilter, double anomalyGuideFilterLevel, bool useAdaptiveAnomalyLevel, double anomalyLevelPercentile, int anomalyLevelLookback, double anomalyLevelCeiling, bool showHistogramPanel, bool showEfficiencyLine, bool enable3DHistogram, int histogram3DMaxVisibleBars, bool compatibilityRenderMode, bool autoFallbackToNativeRender, bool showArrows, bool showTextLabels, bool paintExcessBars, bool paintMomentumBars, bool paintContinuationBars, bool paintRotationBars, int markerOffsetTicks, int signalLabelFontSize, bool signalLabelBold, string acelBuyLabel, string acelSellLabel, string excessBuyLabel, string excessSellLabel, string rotationBuyLabel, string rotationSellLabel, bool useOperationalModePresets, int cumulationIntervalMs, int speedLookback, bool classifyOnClosedBar, double speedZThreshold, double extremeSpeedZ, double accelerationZThreshold, double minimumImbalance, bool requireAnomalyForEverySignal, double anomalyWeightMagnitude, double anomalyWeightDirection, double anomalyWeightAcceleration, int minimumRangeTicks, int excessLookback, bool useEfficiencyTrigger, int efficiencySmoothPeriod, double histogramMaxAbsValue, double histogramMinDisplayZ, bool candleWidthHistogram, bool paintBars, int minimumBarsBetweenSignals, bool suppressRepeatedSameSignal)
		{
			if (cacheILT_FLOWSPEED != null)
				for (int idx = 0; idx < cacheILT_FLOWSPEED.Length; idx++)
					if (cacheILT_FLOWSPEED[idx] != null && cacheILT_FLOWSPEED[idx].OperationalMode == operationalMode && cacheILT_FLOWSPEED[idx].MinimumTradeVolume == minimumTradeVolume && cacheILT_FLOWSPEED[idx].ExcessConfirmWindow == excessConfirmWindow && cacheILT_FLOWSPEED[idx].EfficiencyTrigger == efficiencyTrigger && cacheILT_FLOWSPEED[idx].UseAnomalyGuideFilter == useAnomalyGuideFilter && cacheILT_FLOWSPEED[idx].AnomalyGuideFilterLevel == anomalyGuideFilterLevel && cacheILT_FLOWSPEED[idx].UseAdaptiveAnomalyLevel == useAdaptiveAnomalyLevel && cacheILT_FLOWSPEED[idx].AnomalyLevelPercentile == anomalyLevelPercentile && cacheILT_FLOWSPEED[idx].AnomalyLevelLookback == anomalyLevelLookback && cacheILT_FLOWSPEED[idx].AnomalyLevelCeiling == anomalyLevelCeiling && cacheILT_FLOWSPEED[idx].ShowHistogramPanel == showHistogramPanel && cacheILT_FLOWSPEED[idx].ShowEfficiencyLine == showEfficiencyLine && cacheILT_FLOWSPEED[idx].Enable3DHistogram == enable3DHistogram && cacheILT_FLOWSPEED[idx].Histogram3DMaxVisibleBars == histogram3DMaxVisibleBars && cacheILT_FLOWSPEED[idx].CompatibilityRenderMode == compatibilityRenderMode && cacheILT_FLOWSPEED[idx].AutoFallbackToNativeRender == autoFallbackToNativeRender && cacheILT_FLOWSPEED[idx].ShowArrows == showArrows && cacheILT_FLOWSPEED[idx].ShowTextLabels == showTextLabels && cacheILT_FLOWSPEED[idx].PaintExcessBars == paintExcessBars && cacheILT_FLOWSPEED[idx].PaintMomentumBars == paintMomentumBars && cacheILT_FLOWSPEED[idx].PaintContinuationBars == paintContinuationBars && cacheILT_FLOWSPEED[idx].PaintRotationBars == paintRotationBars && cacheILT_FLOWSPEED[idx].MarkerOffsetTicks == markerOffsetTicks && cacheILT_FLOWSPEED[idx].SignalLabelFontSize == signalLabelFontSize && cacheILT_FLOWSPEED[idx].SignalLabelBold == signalLabelBold && cacheILT_FLOWSPEED[idx].AcelBuyLabel == acelBuyLabel && cacheILT_FLOWSPEED[idx].AcelSellLabel == acelSellLabel && cacheILT_FLOWSPEED[idx].ExcessBuyLabel == excessBuyLabel && cacheILT_FLOWSPEED[idx].ExcessSellLabel == excessSellLabel && cacheILT_FLOWSPEED[idx].RotationBuyLabel == rotationBuyLabel && cacheILT_FLOWSPEED[idx].RotationSellLabel == rotationSellLabel && cacheILT_FLOWSPEED[idx].UseOperationalModePresets == useOperationalModePresets && cacheILT_FLOWSPEED[idx].CumulationIntervalMs == cumulationIntervalMs && cacheILT_FLOWSPEED[idx].SpeedLookback == speedLookback && cacheILT_FLOWSPEED[idx].ClassifyOnClosedBar == classifyOnClosedBar && cacheILT_FLOWSPEED[idx].SpeedZThreshold == speedZThreshold && cacheILT_FLOWSPEED[idx].ExtremeSpeedZ == extremeSpeedZ && cacheILT_FLOWSPEED[idx].AccelerationZThreshold == accelerationZThreshold && cacheILT_FLOWSPEED[idx].MinimumImbalance == minimumImbalance && cacheILT_FLOWSPEED[idx].RequireAnomalyForEverySignal == requireAnomalyForEverySignal && cacheILT_FLOWSPEED[idx].AnomalyWeightMagnitude == anomalyWeightMagnitude && cacheILT_FLOWSPEED[idx].AnomalyWeightDirection == anomalyWeightDirection && cacheILT_FLOWSPEED[idx].AnomalyWeightAcceleration == anomalyWeightAcceleration && cacheILT_FLOWSPEED[idx].MinimumRangeTicks == minimumRangeTicks && cacheILT_FLOWSPEED[idx].ExcessLookback == excessLookback && cacheILT_FLOWSPEED[idx].UseEfficiencyTrigger == useEfficiencyTrigger && cacheILT_FLOWSPEED[idx].EfficiencySmoothPeriod == efficiencySmoothPeriod && cacheILT_FLOWSPEED[idx].HistogramMaxAbsValue == histogramMaxAbsValue && cacheILT_FLOWSPEED[idx].HistogramMinDisplayZ == histogramMinDisplayZ && cacheILT_FLOWSPEED[idx].CandleWidthHistogram == candleWidthHistogram && cacheILT_FLOWSPEED[idx].PaintBars == paintBars && cacheILT_FLOWSPEED[idx].MinimumBarsBetweenSignals == minimumBarsBetweenSignals && cacheILT_FLOWSPEED[idx].SuppressRepeatedSameSignal == suppressRepeatedSameSignal && cacheILT_FLOWSPEED[idx].EqualsInput(input))
						return cacheILT_FLOWSPEED[idx];
			return CacheIndicator<ILT_SUITE.ILT_FLOWSPEED>(new ILT_SUITE.ILT_FLOWSPEED(){ OperationalMode = operationalMode, MinimumTradeVolume = minimumTradeVolume, ExcessConfirmWindow = excessConfirmWindow, EfficiencyTrigger = efficiencyTrigger, UseAnomalyGuideFilter = useAnomalyGuideFilter, AnomalyGuideFilterLevel = anomalyGuideFilterLevel, UseAdaptiveAnomalyLevel = useAdaptiveAnomalyLevel, AnomalyLevelPercentile = anomalyLevelPercentile, AnomalyLevelLookback = anomalyLevelLookback, AnomalyLevelCeiling = anomalyLevelCeiling, ShowHistogramPanel = showHistogramPanel, ShowEfficiencyLine = showEfficiencyLine, Enable3DHistogram = enable3DHistogram, Histogram3DMaxVisibleBars = histogram3DMaxVisibleBars, CompatibilityRenderMode = compatibilityRenderMode, AutoFallbackToNativeRender = autoFallbackToNativeRender, ShowArrows = showArrows, ShowTextLabels = showTextLabels, PaintExcessBars = paintExcessBars, PaintMomentumBars = paintMomentumBars, PaintContinuationBars = paintContinuationBars, PaintRotationBars = paintRotationBars, MarkerOffsetTicks = markerOffsetTicks, SignalLabelFontSize = signalLabelFontSize, SignalLabelBold = signalLabelBold, AcelBuyLabel = acelBuyLabel, AcelSellLabel = acelSellLabel, ExcessBuyLabel = excessBuyLabel, ExcessSellLabel = excessSellLabel, RotationBuyLabel = rotationBuyLabel, RotationSellLabel = rotationSellLabel, UseOperationalModePresets = useOperationalModePresets, CumulationIntervalMs = cumulationIntervalMs, SpeedLookback = speedLookback, ClassifyOnClosedBar = classifyOnClosedBar, SpeedZThreshold = speedZThreshold, ExtremeSpeedZ = extremeSpeedZ, AccelerationZThreshold = accelerationZThreshold, MinimumImbalance = minimumImbalance, RequireAnomalyForEverySignal = requireAnomalyForEverySignal, AnomalyWeightMagnitude = anomalyWeightMagnitude, AnomalyWeightDirection = anomalyWeightDirection, AnomalyWeightAcceleration = anomalyWeightAcceleration, MinimumRangeTicks = minimumRangeTicks, ExcessLookback = excessLookback, UseEfficiencyTrigger = useEfficiencyTrigger, EfficiencySmoothPeriod = efficiencySmoothPeriod, HistogramMaxAbsValue = histogramMaxAbsValue, HistogramMinDisplayZ = histogramMinDisplayZ, CandleWidthHistogram = candleWidthHistogram, PaintBars = paintBars, MinimumBarsBetweenSignals = minimumBarsBetweenSignals, SuppressRepeatedSameSignal = suppressRepeatedSameSignal }, input, ref cacheILT_FLOWSPEED);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.ILT_SUITE.ILT_FLOWSPEED ILT_FLOWSPEED(int operationalMode, int minimumTradeVolume, int excessConfirmWindow, double efficiencyTrigger, bool useAnomalyGuideFilter, double anomalyGuideFilterLevel, bool useAdaptiveAnomalyLevel, double anomalyLevelPercentile, int anomalyLevelLookback, double anomalyLevelCeiling, bool showHistogramPanel, bool showEfficiencyLine, bool enable3DHistogram, int histogram3DMaxVisibleBars, bool compatibilityRenderMode, bool autoFallbackToNativeRender, bool showArrows, bool showTextLabels, bool paintExcessBars, bool paintMomentumBars, bool paintContinuationBars, bool paintRotationBars, int markerOffsetTicks, int signalLabelFontSize, bool signalLabelBold, string acelBuyLabel, string acelSellLabel, string excessBuyLabel, string excessSellLabel, string rotationBuyLabel, string rotationSellLabel, bool useOperationalModePresets, int cumulationIntervalMs, int speedLookback, bool classifyOnClosedBar, double speedZThreshold, double extremeSpeedZ, double accelerationZThreshold, double minimumImbalance, bool requireAnomalyForEverySignal, double anomalyWeightMagnitude, double anomalyWeightDirection, double anomalyWeightAcceleration, int minimumRangeTicks, int excessLookback, bool useEfficiencyTrigger, int efficiencySmoothPeriod, double histogramMaxAbsValue, double histogramMinDisplayZ, bool candleWidthHistogram, bool paintBars, int minimumBarsBetweenSignals, bool suppressRepeatedSameSignal)
		{
			return indicator.ILT_FLOWSPEED(Input, operationalMode, minimumTradeVolume, excessConfirmWindow, efficiencyTrigger, useAnomalyGuideFilter, anomalyGuideFilterLevel, useAdaptiveAnomalyLevel, anomalyLevelPercentile, anomalyLevelLookback, anomalyLevelCeiling, showHistogramPanel, showEfficiencyLine, enable3DHistogram, histogram3DMaxVisibleBars, compatibilityRenderMode, autoFallbackToNativeRender, showArrows, showTextLabels, paintExcessBars, paintMomentumBars, paintContinuationBars, paintRotationBars, markerOffsetTicks, signalLabelFontSize, signalLabelBold, acelBuyLabel, acelSellLabel, excessBuyLabel, excessSellLabel, rotationBuyLabel, rotationSellLabel, useOperationalModePresets, cumulationIntervalMs, speedLookback, classifyOnClosedBar, speedZThreshold, extremeSpeedZ, accelerationZThreshold, minimumImbalance, requireAnomalyForEverySignal, anomalyWeightMagnitude, anomalyWeightDirection, anomalyWeightAcceleration, minimumRangeTicks, excessLookback, useEfficiencyTrigger, efficiencySmoothPeriod, histogramMaxAbsValue, histogramMinDisplayZ, candleWidthHistogram, paintBars, minimumBarsBetweenSignals, suppressRepeatedSameSignal);
		}

		public Indicators.ILT_SUITE.ILT_FLOWSPEED ILT_FLOWSPEED(ISeries<double> input , int operationalMode, int minimumTradeVolume, int excessConfirmWindow, double efficiencyTrigger, bool useAnomalyGuideFilter, double anomalyGuideFilterLevel, bool useAdaptiveAnomalyLevel, double anomalyLevelPercentile, int anomalyLevelLookback, double anomalyLevelCeiling, bool showHistogramPanel, bool showEfficiencyLine, bool enable3DHistogram, int histogram3DMaxVisibleBars, bool compatibilityRenderMode, bool autoFallbackToNativeRender, bool showArrows, bool showTextLabels, bool paintExcessBars, bool paintMomentumBars, bool paintContinuationBars, bool paintRotationBars, int markerOffsetTicks, int signalLabelFontSize, bool signalLabelBold, string acelBuyLabel, string acelSellLabel, string excessBuyLabel, string excessSellLabel, string rotationBuyLabel, string rotationSellLabel, bool useOperationalModePresets, int cumulationIntervalMs, int speedLookback, bool classifyOnClosedBar, double speedZThreshold, double extremeSpeedZ, double accelerationZThreshold, double minimumImbalance, bool requireAnomalyForEverySignal, double anomalyWeightMagnitude, double anomalyWeightDirection, double anomalyWeightAcceleration, int minimumRangeTicks, int excessLookback, bool useEfficiencyTrigger, int efficiencySmoothPeriod, double histogramMaxAbsValue, double histogramMinDisplayZ, bool candleWidthHistogram, bool paintBars, int minimumBarsBetweenSignals, bool suppressRepeatedSameSignal)
		{
			return indicator.ILT_FLOWSPEED(input, operationalMode, minimumTradeVolume, excessConfirmWindow, efficiencyTrigger, useAnomalyGuideFilter, anomalyGuideFilterLevel, useAdaptiveAnomalyLevel, anomalyLevelPercentile, anomalyLevelLookback, anomalyLevelCeiling, showHistogramPanel, showEfficiencyLine, enable3DHistogram, histogram3DMaxVisibleBars, compatibilityRenderMode, autoFallbackToNativeRender, showArrows, showTextLabels, paintExcessBars, paintMomentumBars, paintContinuationBars, paintRotationBars, markerOffsetTicks, signalLabelFontSize, signalLabelBold, acelBuyLabel, acelSellLabel, excessBuyLabel, excessSellLabel, rotationBuyLabel, rotationSellLabel, useOperationalModePresets, cumulationIntervalMs, speedLookback, classifyOnClosedBar, speedZThreshold, extremeSpeedZ, accelerationZThreshold, minimumImbalance, requireAnomalyForEverySignal, anomalyWeightMagnitude, anomalyWeightDirection, anomalyWeightAcceleration, minimumRangeTicks, excessLookback, useEfficiencyTrigger, efficiencySmoothPeriod, histogramMaxAbsValue, histogramMinDisplayZ, candleWidthHistogram, paintBars, minimumBarsBetweenSignals, suppressRepeatedSameSignal);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.ILT_SUITE.ILT_FLOWSPEED ILT_FLOWSPEED(int operationalMode, int minimumTradeVolume, int excessConfirmWindow, double efficiencyTrigger, bool useAnomalyGuideFilter, double anomalyGuideFilterLevel, bool useAdaptiveAnomalyLevel, double anomalyLevelPercentile, int anomalyLevelLookback, double anomalyLevelCeiling, bool showHistogramPanel, bool showEfficiencyLine, bool enable3DHistogram, int histogram3DMaxVisibleBars, bool compatibilityRenderMode, bool autoFallbackToNativeRender, bool showArrows, bool showTextLabels, bool paintExcessBars, bool paintMomentumBars, bool paintContinuationBars, bool paintRotationBars, int markerOffsetTicks, int signalLabelFontSize, bool signalLabelBold, string acelBuyLabel, string acelSellLabel, string excessBuyLabel, string excessSellLabel, string rotationBuyLabel, string rotationSellLabel, bool useOperationalModePresets, int cumulationIntervalMs, int speedLookback, bool classifyOnClosedBar, double speedZThreshold, double extremeSpeedZ, double accelerationZThreshold, double minimumImbalance, bool requireAnomalyForEverySignal, double anomalyWeightMagnitude, double anomalyWeightDirection, double anomalyWeightAcceleration, int minimumRangeTicks, int excessLookback, bool useEfficiencyTrigger, int efficiencySmoothPeriod, double histogramMaxAbsValue, double histogramMinDisplayZ, bool candleWidthHistogram, bool paintBars, int minimumBarsBetweenSignals, bool suppressRepeatedSameSignal)
		{
			return indicator.ILT_FLOWSPEED(Input, operationalMode, minimumTradeVolume, excessConfirmWindow, efficiencyTrigger, useAnomalyGuideFilter, anomalyGuideFilterLevel, useAdaptiveAnomalyLevel, anomalyLevelPercentile, anomalyLevelLookback, anomalyLevelCeiling, showHistogramPanel, showEfficiencyLine, enable3DHistogram, histogram3DMaxVisibleBars, compatibilityRenderMode, autoFallbackToNativeRender, showArrows, showTextLabels, paintExcessBars, paintMomentumBars, paintContinuationBars, paintRotationBars, markerOffsetTicks, signalLabelFontSize, signalLabelBold, acelBuyLabel, acelSellLabel, excessBuyLabel, excessSellLabel, rotationBuyLabel, rotationSellLabel, useOperationalModePresets, cumulationIntervalMs, speedLookback, classifyOnClosedBar, speedZThreshold, extremeSpeedZ, accelerationZThreshold, minimumImbalance, requireAnomalyForEverySignal, anomalyWeightMagnitude, anomalyWeightDirection, anomalyWeightAcceleration, minimumRangeTicks, excessLookback, useEfficiencyTrigger, efficiencySmoothPeriod, histogramMaxAbsValue, histogramMinDisplayZ, candleWidthHistogram, paintBars, minimumBarsBetweenSignals, suppressRepeatedSameSignal);
		}

		public Indicators.ILT_SUITE.ILT_FLOWSPEED ILT_FLOWSPEED(ISeries<double> input , int operationalMode, int minimumTradeVolume, int excessConfirmWindow, double efficiencyTrigger, bool useAnomalyGuideFilter, double anomalyGuideFilterLevel, bool useAdaptiveAnomalyLevel, double anomalyLevelPercentile, int anomalyLevelLookback, double anomalyLevelCeiling, bool showHistogramPanel, bool showEfficiencyLine, bool enable3DHistogram, int histogram3DMaxVisibleBars, bool compatibilityRenderMode, bool autoFallbackToNativeRender, bool showArrows, bool showTextLabels, bool paintExcessBars, bool paintMomentumBars, bool paintContinuationBars, bool paintRotationBars, int markerOffsetTicks, int signalLabelFontSize, bool signalLabelBold, string acelBuyLabel, string acelSellLabel, string excessBuyLabel, string excessSellLabel, string rotationBuyLabel, string rotationSellLabel, bool useOperationalModePresets, int cumulationIntervalMs, int speedLookback, bool classifyOnClosedBar, double speedZThreshold, double extremeSpeedZ, double accelerationZThreshold, double minimumImbalance, bool requireAnomalyForEverySignal, double anomalyWeightMagnitude, double anomalyWeightDirection, double anomalyWeightAcceleration, int minimumRangeTicks, int excessLookback, bool useEfficiencyTrigger, int efficiencySmoothPeriod, double histogramMaxAbsValue, double histogramMinDisplayZ, bool candleWidthHistogram, bool paintBars, int minimumBarsBetweenSignals, bool suppressRepeatedSameSignal)
		{
			return indicator.ILT_FLOWSPEED(input, operationalMode, minimumTradeVolume, excessConfirmWindow, efficiencyTrigger, useAnomalyGuideFilter, anomalyGuideFilterLevel, useAdaptiveAnomalyLevel, anomalyLevelPercentile, anomalyLevelLookback, anomalyLevelCeiling, showHistogramPanel, showEfficiencyLine, enable3DHistogram, histogram3DMaxVisibleBars, compatibilityRenderMode, autoFallbackToNativeRender, showArrows, showTextLabels, paintExcessBars, paintMomentumBars, paintContinuationBars, paintRotationBars, markerOffsetTicks, signalLabelFontSize, signalLabelBold, acelBuyLabel, acelSellLabel, excessBuyLabel, excessSellLabel, rotationBuyLabel, rotationSellLabel, useOperationalModePresets, cumulationIntervalMs, speedLookback, classifyOnClosedBar, speedZThreshold, extremeSpeedZ, accelerationZThreshold, minimumImbalance, requireAnomalyForEverySignal, anomalyWeightMagnitude, anomalyWeightDirection, anomalyWeightAcceleration, minimumRangeTicks, excessLookback, useEfficiencyTrigger, efficiencySmoothPeriod, histogramMaxAbsValue, histogramMinDisplayZ, candleWidthHistogram, paintBars, minimumBarsBetweenSignals, suppressRepeatedSameSignal);
		}
	}
}

#endregion

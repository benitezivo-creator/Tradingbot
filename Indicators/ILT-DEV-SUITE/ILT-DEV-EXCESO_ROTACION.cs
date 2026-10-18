#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using DxSolidColorBrush = SharpDX.Direct2D1.SolidColorBrush;
using DxRenderTarget = SharpDX.Direct2D1.RenderTarget;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.ILT_DEV_SUITE
{
    public class ILT_DEV_EXCESO_ROTACION : Indicator
    {
        // =========================================================================
        // INDICADOR INSTITUCIONAL: EXCESO Y ROTACIÓN — ILT-DEV SUITE (NINJATRADER 8)
        // Motor Causal FlowSpeed v1.3 + Niveles de Sesión (Asia/Europa/NY) + Delta
        // =========================================================================

        #region Enums & Structs
        public enum ModoOperativo
        {
            Activo = 0,
            Balanceado = 1,
            EstrictoNQ = 2
        }

        public enum TipoSesion
        {
            Ninguna,
            Asia,       // 19:00 -> 03:00 NY (Sídney integrada)
            Europa,     // 03:00 -> 09:30 NY (Nivel rector)
            NuevaYork   // 09:30 -> 17:00 NY
        }

        public enum TipoSenal
        {
            Ninguna = 0,
            ExcesoComprador = 1,        // EXB (Amarillo)
            ExcesoVendedor = 2,         // EXS (Amarillo)
            RotacionBajista = 3,        // RTS (Violeta)
            RotacionAlcista = 4,        // RTB (Celeste)
            ContinuacionCompradora = 5, // CB (Verde)
            ContinuacionVendedora = 6   // CS (Rojo)
        }

        public class BarFlowData
        {
            public int Samples;
            public double AskVolume;
            public double BidVolume;
            public double SumTotalSpeed;
            public double SumNetSpeed;

            public double TotalSpeed;
            public double NetSpeed;
            public double AbsNetSpeed;
            public double Acceleration;
            public double Imbalance;

            public double SpeedZ;
            public double NetSpeedZ;
            public double AbsNetSpeedZ;
            public double AccelerationZ;

            public double AnomalyMagnitude;
            public double AnomalySigned;
            public double EffectiveAnomalyLevel;
            public TipoSenal SignalCode;
        }

        public class PendingExcessState
        {
            public bool Active;
            public bool IsBuy;
            public double High;
            public double Low;
            public double EventRange;
            public double BaseRange;
            public int OriginBarIndex;
            public int LastBarIndex;
            public int BarsLeft;
            public double EventMagnitude;
            public double EventLevel;
            public double ResponseScaleTicks;
            public double BestFavorableTicks;
            public int ContinuationEvidenceBars;
            public int RotationEvidenceBars;
        }

        private struct PrintCinta
        {
            public DateTime Tiempo;
            public double   VolumenCompra;
            public double   VolumenVenta;
        }

        private struct EntradaBracketDelta
        {
            public long   Volumen;
            public double DeltaFirmado;
        }
        #endregion

        #region Constants
        private const int    ROBUST_MIN_SAMPLES      = 12;
        private const int    SESSION_BASELINE_MAX    = 500;
        private const double ROBUST_MAD_TO_SIGMA     = 1.4826;
        private const double ROBUST_REL_FLOOR        = 0.15;
        private const int    ANOMALY_LEVEL_MIN_SAMPLES = 30;
        #endregion

        #region Private Fields
        // --- Sincronización ---
        private readonly object lockSync = new object();
        private readonly object barDataSync = new object();

        // --- Zonas Horarias y Sesiones ---
        private TimeZoneInfo    nyTimeZone;
        private TipoSesion      sesionActual = TipoSesion.Ninguna;

        // --- Niveles de Sesión ---
        private double          asiaHigh = double.MinValue;
        private double          asiaLow = double.MaxValue;
        private double          ultimoAsiaHigh = double.NaN;
        private double          ultimoAsiaLow = double.NaN;

        private double          europaHigh = double.MinValue;
        private double          europaLow = double.MaxValue;
        private double          ultimoEuropaHigh = double.NaN;
        private double          ultimoEuropaLow = double.NaN;

        private double          nyHigh = double.MinValue;
        private double          nyLow = double.MaxValue;

        // --- VWAP y Perfil de Volumen ---
        private double          vwapSumPrecioVol = 0.0;
        private double          vwapSumVolumen = 0.0;
        private double          vwapActual = double.NaN;

        private readonly Dictionary<long, long> perfilVolumenSesion = new Dictionary<long, long>();
        private double          pocPrecio = double.NaN;
        private double          vahPrecio = double.NaN;
        private double          valPrecio = double.NaN;
        private long            volumenTotalSesion = 0;

        // --- Order Flow y Cinta ---
        private double          precioAnteriorTape = double.NaN;
        private int             direccionAnteriorTape = 0;
        private double          lastBid = double.NaN;
        private double          lastAsk = double.NaN;
        private double          deltaSesionAcumulado = 0.0;
        private double          deltaPrevioBarra = 0.0;

        // --- Ventana Rodante de Velocidad de Flujo ---
        private readonly Queue<PrintCinta> ventanaVelocidad = new Queue<PrintCinta>(2048);
        private double          volumenCompraVentana = 0.0;
        private double          volumenVentaVentana = 0.0;
        private double          velocidadCompraActual = 0.0;
        private double          velocidadVentaActual = 0.0;
        private double          velocidadFlujoTotal = 0.0;

        // --- Bracket Delta (Migración) ---
        private readonly Queue<EntradaBracketDelta> colaBracket = new Queue<EntradaBracketDelta>(8192);
        private long            volumenBracketActual = 0;
        private double          deltaBracketActual = 0.0;
        private bool            bracketCruzoCero = false;

        // --- Motor de Flujo e Historial por Barra ---
        private readonly Dictionary<int, BarFlowData> barData = new Dictionary<int, BarFlowData>(4096);
        private Queue<int>      pendingCompletedBars;
        private HashSet<int>    pendingCompletedBarSet;
        private int             lastTradePrimaryBarIndex = -1;

        // --- Baseline Causal de Sesión ---
        private readonly List<double> sessTotal   = new List<double>(512);
        private readonly List<double> sessNet     = new List<double>(512);
        private readonly List<double> sessAbsNet  = new List<double>(512);
        private readonly List<double> sessAccel   = new List<double>(512);
        private readonly List<double> madScratch  = new List<double>(512);
        private readonly List<double> zwTotal     = new List<double>(512);
        private readonly List<double> zwNet       = new List<double>(512);
        private readonly List<double> zwAbsNet    = new List<double>(512);
        private readonly List<double> zwAccel     = new List<double>(512);

        // --- Nivel Adaptativo Causal de Anomalía ---
        private readonly List<double> anomalyLevelWindow  = new List<double>(512);
        private readonly List<double> anomalyLevelScratch = new List<double>(512);

        // --- Máquina de Estados: Exceso Pendiente y Señales ---
        private readonly PendingExcessState pendingExcess = new PendingExcessState();
        private int             lastExcessMarkerBar = -100;
        private double          ultimoScoreRotacion = 0.0;
        private string          ultimoDetalleConfluencia = "";
        private string          ultimoDetalleDelta = "";

        // --- Anti-colisión vertical para etiquetas ---
        private readonly Dictionary<int, int> markerStackAbove = new Dictionary<int, int>();
        private readonly Dictionary<int, int> markerStackBelow = new Dictionary<int, int>();

        // --- Recursos Direct2D (Solo Panel HUD) ---
        private DxRenderTarget  dxTarget = null;
        private DxSolidColorBrush dxBrushHudBg = null;
        private DxSolidColorBrush dxBrushHudBorder = null;
        private DxSolidColorBrush dxBrushTexto = null;
        private DxSolidColorBrush dxBrushAcento = null;
        private SharpDX.DirectWrite.Factory dwFactory = null;
        private TextFormat      tfHud = null;
        private TextFormat      tfHudBold = null;
        #endregion

        #region Properties & Settings

        // 01. Modo Operativo y Motor
        [NinjaScriptProperty]
        [Display(Name = "Modo Operativo", GroupName = "01. Motor FlowSpeed", Order = 1, Description = "Balanceado (Recomendado ES/NQ), Activo (más sensible), Estricto NQ (alta convicción).")]
        public ModoOperativo Modo { get; set; }

        [Range(100, 5000)]
        [NinjaScriptProperty]
        [Display(Name = "Ventana Cinta (ms)", GroupName = "01. Motor FlowSpeed", Order = 2, Description = "Ventana temporal rodante para medir velocidad de flujo (1000 ms = 1 seg).")]
        public int VentanaVelocidadMs { get; set; }

        [Range(5, 100)]
        [NinjaScriptProperty]
        [Display(Name = "Speed Lookback (Barras)", GroupName = "01. Motor FlowSpeed", Order = 3, Description = "Barras para baseline de velocidad si el baseline de sesión está iniciando.")]
        public int SpeedLookback { get; set; }

        [Range(2, 20)]
        [NinjaScriptProperty]
        [Display(Name = "Excess Lookback (Barras)", GroupName = "01. Motor FlowSpeed", Order = 4, Description = "Barras previas para verificar que el exceso ocurre en un extremo reciente (High/Low).")]
        public int ExcessLookback { get; set; }

        [Range(2, 20)]
        [NinjaScriptProperty]
        [Display(Name = "Ventana Confirmación Rotación", GroupName = "01. Motor FlowSpeed", Order = 5, Description = "Máximo de barras posteriores para que el exceso resuelva a Rotación o Continuación.")]
        public int ExcessConfirmWindow { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Exigir Aceptación Sostenida", GroupName = "01. Motor FlowSpeed", Order = 6, Description = "Filtro institucional: en la 1ra barra de respuesta bloquea rotaciones marginales.")]
        public bool RequireSustainedAcceptance { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Nivel Anomalía Adaptativo", GroupName = "01. Motor FlowSpeed", Order = 7, Description = "Calcula el umbral efectivo de anomalía M usando el percentil 78 causal de la sesión.")]
        public bool UseAdaptiveAnomalyLevel { get; set; }

        // 02. Umbrales Personalizados
        [Range(0.5, 5.0)]
        [NinjaScriptProperty]
        [Display(Name = "Speed Z Umbral", GroupName = "02. Umbrales Calibrados", Order = 1, Description = "Z-Score mínimo de velocidad para declarar anomalía de flujo.")]
        public double SpeedZThreshold { get; set; }

        [Range(1.0, 6.0)]
        [NinjaScriptProperty]
        [Display(Name = "Extreme Speed Z", GroupName = "02. Umbrales Calibrados", Order = 2, Description = "Z-Score de velocidad extrema.")]
        public double ExtremeSpeedZ { get; set; }

        [Range(0.2, 3.0)]
        [NinjaScriptProperty]
        [Display(Name = "Acceleration Z Umbral", GroupName = "02. Umbrales Calibrados", Order = 3, Description = "Z-Score de aceleración de flujo.")]
        public double AccelerationZThreshold { get; set; }

        [Range(0.05, 0.90)]
        [NinjaScriptProperty]
        [Display(Name = "Imbalance Mínimo", GroupName = "02. Umbrales Calibrados", Order = 4, Description = "Desbalance mínimo de agresión (AskVol - BidVol)/(TotalVol).")]
        public double MinimumImbalance { get; set; }

        [Range(1, 20)]
        [NinjaScriptProperty]
        [Display(Name = "Rango Mínimo Vela (Ticks)", GroupName = "02. Umbrales Calibrados", Order = 5, Description = "Filtro anti-doji.")]
        public int MinimumRangeTicks { get; set; }

        [Range(1, 10)]
        [NinjaScriptProperty]
        [Display(Name = "Espaciado Mínimo Señales", GroupName = "02. Umbrales Calibrados", Order = 6, Description = "Barras mínimas entre nuevos marcadores de exceso.")]
        public int MinimumBarsBetweenSignals { get; set; }

        // 03. Sesiones y Niveles
        [NinjaScriptProperty]
        [Display(Name = "Dibujar Niveles y VWAP", GroupName = "03. Sesiones & Confluencias", Order = 1, Description = "Dibuja Europa High/Low, Asia High/Low y FS VWAP en el gráfico.")]
        public bool DibujarNiveles { get; set; }

        [Range(1, 30)]
        [NinjaScriptProperty]
        [Display(Name = "Tolerancia Confluencia (Ticks)", GroupName = "03. Sesiones & Confluencias", Order = 2, Description = "Distancia máxima a un nivel de sesión para validar confluencia.")]
        public int ToleranciaNivelTicks { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Gatillo Apertura 09:30 NY", GroupName = "03. Sesiones & Confluencias", Order = 3, Description = "Detecta y pondera el toque o ruptura de Europa H/L en la apertura de NY.")]
        public bool ActivarGatilloApertura { get; set; }

        // 04. Visualización y Colores
        [NinjaScriptProperty]
        [Display(Name = "Pintar Velas con Señales", GroupName = "04. Visualización", Order = 1, Description = "Colorea la vela nativa: Amarillo (Exceso), Celeste (Rotación Alcista), Violeta (Rotación Bajista).")]
        public bool PintarVelas { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Dibujar Etiquetas (EXB/EXS/RTB/RTS)", GroupName = "04. Visualización", Order = 2, Description = "Dibuja etiquetas de texto claras sobre las velas señaladas sin saturar.")]
        public bool DibujarEtiquetas { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Dibujar Flechas", GroupName = "04. Visualización", Order = 3, Description = "Dibuja flechas limpias en los giros y excesos.")]
        public bool DibujarFlechas { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mostrar Continuaciones (CB/CS)", GroupName = "04. Visualización", Order = 4, Description = "Muestra también las confirmaciones de continuación cuando el exceso no rota.")]
        public bool MostrarContinuaciones { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mostrar Panel HUD", GroupName = "04. Visualización", Order = 5, Description = "Muestra el panel dashboard en pantalla con sesión, delta, score y estado.")]
        public bool MostrarHud { get; set; }

        [Range(1, 30)]
        [NinjaScriptProperty]
        [Display(Name = "Offset Marcadores (Ticks)", GroupName = "04. Visualización", Order = 6, Description = "Distancia vertical desde la mecha para dibujar flechas y texto.")]
        public int MarkerOffsetTicks { get; set; }

        [XmlIgnore]
        [Display(Name = "Color Exceso (Amarillo)", GroupName = "05. Paleta de Colores", Order = 1)]
        public MediaBrush ColorExceso { get; set; }

        [Browsable(false)]
        public string ColorExcesoSerialize
        {
            get { return Serialize.BrushToString(ColorExceso); }
            set { ColorExceso = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Color Rotación Alcista (Celeste)", GroupName = "05. Paleta de Colores", Order = 2)]
        public MediaBrush ColorRotacionAlcista { get; set; }

        [Browsable(false)]
        public string ColorRotacionAlcistaSerialize
        {
            get { return Serialize.BrushToString(ColorRotacionAlcista); }
            set { ColorRotacionAlcista = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Color Rotación Bajista (Violeta)", GroupName = "05. Paleta de Colores", Order = 3)]
        public MediaBrush ColorRotacionBajista { get; set; }

        [Browsable(false)]
        public string ColorRotacionBajistaSerialize
        {
            get { return Serialize.BrushToString(ColorRotacionBajista); }
            set { ColorRotacionBajista = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Color Continuación (Verde Lima)", GroupName = "05. Paleta de Colores", Order = 4)]
        public MediaBrush ColorContinuacion { get; set; }

        [Browsable(false)]
        public string ColorContinuacionSerialize
        {
            get { return Serialize.BrushToString(ColorContinuacion); }
            set { ColorContinuacion = Serialize.StringToBrush(value); }
        }

        // 06. Filtros Order Flow
        [Range(0, 1000)]
        [NinjaScriptProperty]
        [Display(Name = "Filtro Volumen Mínimo Trade", GroupName = "06. Filtros Order Flow", Order = 1, Description = "Filtro para procesar bloques mayores (0 = procesar todo).")]
        public int VolumenMinimoTrade { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Clasificación Bid/Ask en Vivo", GroupName = "06. Filtros Order Flow", Order = 2, Description = "Utiliza cotizaciones Bid/Ask en tiempo real para clasificar agresión.")]
        public bool UsarClasificacionBidAsk { get; set; }

        #endregion

        #region Initialization
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description                         = "ILT-DEV-EXCESO Y ROTACION: Detección institucional causal de anomalías de flujo y giros de subasta (EXB, EXS, RTB, RTS) con confluencias multinivel y delta.";
                Name                                = "ILT-DEV-EXCESO_ROTACION";
                Calculate                           = Calculate.OnEachTick;
                IsOverlay                           = true;
                DisplayInDataBox                    = true;
                IsSuspendedWhileInactive            = true;
                BarsRequiredToPlot                  = 0;

                Modo                                = ModoOperativo.Balanceado;
                VentanaVelocidadMs                  = 1000;
                SpeedLookback                       = 30;
                ExcessLookback                      = 5;
                ExcessConfirmWindow                 = 6;
                RequireSustainedAcceptance          = true;
                UseAdaptiveAnomalyLevel             = true;

                // Calibración predeterminada (Balanceado)
                SpeedZThreshold                     = 1.30;
                ExtremeSpeedZ                       = 2.00;
                AccelerationZThreshold              = 0.75;
                MinimumImbalance                    = 0.20;
                MinimumRangeTicks                   = 4;
                MinimumBarsBetweenSignals           = 1;

                DibujarNiveles                      = true;
                ToleranciaNivelTicks                = 6;
                ActivarGatilloApertura              = true;

                PintarVelas                         = true;
                DibujarEtiquetas                    = true;
                DibujarFlechas                      = true;
                MostrarContinuaciones               = false;
                MostrarHud                          = true;
                MarkerOffsetTicks                   = 5;

                ColorExceso                         = new MediaSolidColorBrush(MediaColor.FromRgb(255, 204, 0));   // Oro #FFCC00
                ColorRotacionAlcista                = new MediaSolidColorBrush(MediaColor.FromRgb(0, 191, 255));   // DeepSkyBlue #00BFFF
                ColorRotacionBajista                = new MediaSolidColorBrush(MediaColor.FromRgb(218, 112, 214)); // MediumOrchid #DA70D6
                ColorContinuacion                   = new MediaSolidColorBrush(MediaColor.FromRgb(50, 205, 50));   // LimeGreen #32CD32

                VolumenMinimoTrade                  = 0;
                UsarClasificacionBidAsk             = true;
            }
            else if (State == State.Configure)
            {
                AplicarPresetModo();
                // Serie secundaria interna de 1 Tick para cálculo determinista de Order Flow tick a tick
                AddDataSeries(BarsPeriodType.Tick, 1);
            }
            else if (State == State.DataLoaded)
            {
                InicializarZonaHoraria();
                pendingCompletedBars    = new Queue<int>(1024);
                pendingCompletedBarSet  = new HashSet<int>();

                lock (lockSync)
                {
                    RemoveDrawObjects();
                    barData.Clear();
                    markerStackAbove.Clear();
                    markerStackBelow.Clear();
                }

                ReiniciarSesionCompleta();
            }
            else if (State == State.Terminated)
            {
                DisposeDxResources();
            }
        }

        private void AplicarPresetModo()
        {
            if (Modo == ModoOperativo.Activo)
            {
                SpeedZThreshold        = 1.00;
                ExtremeSpeedZ          = 1.70;
                AccelerationZThreshold = 0.55;
                MinimumImbalance       = 0.12;
                MinimumRangeTicks      = 3;
                MinimumBarsBetweenSignals = 1;
            }
            else if (Modo == ModoOperativo.EstrictoNQ)
            {
                SpeedZThreshold        = 1.65;
                ExtremeSpeedZ          = 2.35;
                AccelerationZThreshold = 1.00;
                MinimumImbalance       = 0.28;
                MinimumRangeTicks      = 6;
                MinimumBarsBetweenSignals = 3;
            }
            else
            {
                SpeedZThreshold        = 1.30;
                ExtremeSpeedZ          = 2.00;
                AccelerationZThreshold = 0.75;
                MinimumImbalance       = 0.20;
                MinimumRangeTicks      = 4;
                MinimumBarsBetweenSignals = 1;
            }
        }
        #endregion

        #region TimeZone & Sessions
        private void InicializarZonaHoraria()
        {
            try { nyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
            catch
            {
                try { nyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
                catch { nyTimeZone = TimeZoneInfo.Local; }
            }
        }

        private DateTime AHoraNuevaYork(DateTime tiempo)
        {
            if (nyTimeZone == null) InicializarZonaHoraria();
            if (tiempo.Kind == DateTimeKind.Utc)
                return TimeZoneInfo.ConvertTimeFromUtc(tiempo, nyTimeZone);

            DateTime local = tiempo.Kind == DateTimeKind.Local ? tiempo : DateTime.SpecifyKind(tiempo, DateTimeKind.Local);
            return TimeZoneInfo.ConvertTime(local, TimeZoneInfo.Local, nyTimeZone);
        }

        private TipoSesion ObtenerTipoSesion(DateTime tiempoNy)
        {
            TimeSpan hora = tiempoNy.TimeOfDay;
            // Asia: 19:00 -> 03:00 (Sídney integrada)
            if (hora >= new TimeSpan(19, 0, 0) || hora < new TimeSpan(3, 0, 0))
                return TipoSesion.Asia;
            // Europa: 03:00 -> 09:30 (Nivel rector)
            if (hora >= new TimeSpan(3, 0, 0) && hora < new TimeSpan(9, 30, 0))
                return TipoSesion.Europa;
            // Nueva York: 09:30 -> 17:00
            if (hora >= new TimeSpan(9, 30, 0) && hora < new TimeSpan(17, 0, 0))
                return TipoSesion.NuevaYork;

            return TipoSesion.Ninguna;
        }

        private void ActualizarSesionesYPerfil(DateTime tiempo, double precio, long volumen)
        {
            DateTime tiempoNy = AHoraNuevaYork(tiempo);
            TipoSesion nuevaSesion = ObtenerTipoSesion(tiempoNy);

            // Transición de sesión detectada
            if (nuevaSesion != sesionActual && nuevaSesion != TipoSesion.Ninguna)
            {
                if (sesionActual == TipoSesion.Asia)
                {
                    ultimoAsiaHigh = asiaHigh;
                    ultimoAsiaLow  = asiaLow;
                }
                else if (sesionActual == TipoSesion.Europa)
                {
                    ultimoEuropaHigh = europaHigh;
                    ultimoEuropaLow  = europaLow;
                }

                sesionActual = nuevaSesion;
                ReiniciarDeltaSesion();
                ReiniciarPerfilSesion();
                ReiniciarBaselineSesion();

                // Expirar cualquier exceso pendiente: no puede cruzar sesiones
                if (pendingExcess.Active)
                    pendingExcess.Active = false;

                if (sesionActual == TipoSesion.Asia) { asiaHigh = precio; asiaLow = precio; }
                else if (sesionActual == TipoSesion.Europa) { europaHigh = precio; europaLow = precio; }
                else if (sesionActual == TipoSesion.NuevaYork) { nyHigh = precio; nyLow = precio; }
            }

            // Actualizar rangos de la sesión activa
            if (sesionActual == TipoSesion.Asia)
            {
                if (precio > asiaHigh) asiaHigh = precio;
                if (precio < asiaLow)  asiaLow  = precio;
            }
            else if (sesionActual == TipoSesion.Europa)
            {
                if (precio > europaHigh) europaHigh = precio;
                if (precio < europaLow)  europaLow  = precio;
            }
            else if (sesionActual == TipoSesion.NuevaYork)
            {
                if (precio > nyHigh) nyHigh = precio;
                if (precio < nyLow)  nyLow  = precio;
            }

            // Actualizar VWAP
            vwapSumPrecioVol += precio * volumen;
            vwapSumVolumen   += volumen;
            if (vwapSumVolumen > 0)
                vwapActual = vwapSumPrecioVol / vwapSumVolumen;

            // Actualizar Perfil de Volumen
            double tick = Math.Max(1E-10, TickSize);
            long tickNivel = (long)Math.Round(precio / tick);
            if (perfilVolumenSesion.ContainsKey(tickNivel))
                perfilVolumenSesion[tickNivel] += volumen;
            else
                perfilVolumenSesion[tickNivel] = volumen;

            volumenTotalSesion += volumen;
        }

        private void RecalcularValueArea(double porcentaje)
        {
            if (perfilVolumenSesion.Count == 0 || volumenTotalSesion <= 0) return;

            long maxVol = -1;
            long pocTick = 0;
            double tick = Math.Max(1E-10, TickSize);

            foreach (KeyValuePair<long, long> kv in perfilVolumenSesion)
            {
                if (kv.Value > maxVol)
                {
                    maxVol = kv.Value;
                    pocTick = kv.Key;
                }
            }

            pocPrecio = pocTick * tick;
            long volumenObjetivo = (long)(volumenTotalSesion * porcentaje);
            long volumenAcumulado = maxVol;

            long upTick = pocTick + 1;
            long downTick = pocTick - 1;

            while (volumenAcumulado < volumenObjetivo && (perfilVolumenSesion.ContainsKey(upTick) || perfilVolumenSesion.ContainsKey(downTick)))
            {
                long volUp = 0;
                long volDown = 0;
                perfilVolumenSesion.TryGetValue(upTick, out volUp);
                perfilVolumenSesion.TryGetValue(downTick, out volDown);

                if (volUp >= volDown && volUp > 0)
                {
                    volumenAcumulado += volUp;
                    upTick++;
                }
                else if (volDown > 0)
                {
                    volumenAcumulado += volDown;
                    downTick--;
                }
                else break;
            }

            vahPrecio = (upTick - 1) * tick;
            valPrecio = (downTick + 1) * tick;
        }

        private void ReiniciarDeltaSesion()
        {
            deltaSesionAcumulado = 0.0;
            colaBracket.Clear();
            volumenBracketActual = 0;
            deltaBracketActual   = 0.0;
            bracketCruzoCero     = false;
        }

        private void ReiniciarPerfilSesion()
        {
            vwapSumPrecioVol    = 0.0;
            vwapSumVolumen      = 0.0;
            vwapActual          = double.NaN;
            perfilVolumenSesion.Clear();
            pocPrecio           = double.NaN;
            vahPrecio           = double.NaN;
            valPrecio           = double.NaN;
            volumenTotalSesion  = 0;
        }

        private void ReiniciarBaselineSesion()
        {
            sessTotal.Clear();
            sessNet.Clear();
            sessAbsNet.Clear();
            sessAccel.Clear();
            anomalyLevelWindow.Clear();
        }

        private void ReiniciarSesionCompleta()
        {
            ReiniciarDeltaSesion();
            ReiniciarPerfilSesion();
            ReiniciarBaselineSesion();
            pendingExcess.Active = false;
        }
        #endregion

        #region Order Flow & Tape Aggression (Series 1 - Tick by Tick)
        private int ClasificarAgresor(double tradePrice, double bid, double ask, bool enVivo)
        {
            // 1. Cotización en tiempo real (Bid / Ask)
            if (enVivo && UsarClasificacionBidAsk && !double.IsNaN(bid) && !double.IsNaN(ask) && ask >= bid)
            {
                double tol = TickSize * 0.5;
                if (tradePrice >= ask - tol) return 1;
                if (tradePrice <= bid + tol) return -1;
            }

            // 2. Tick Rule determinista
            if (!double.IsNaN(precioAnteriorTape) && precioAnteriorTape > 0)
            {
                if (tradePrice > precioAnteriorTape) return 1;
                if (tradePrice < precioAnteriorTape) return -1;
            }

            // 3. Persistencia de cero ticks (hereda lado previo en ES/NQ)
            if (direccionAnteriorTape != 0)
                return direccionAnteriorTape;

            return 1;
        }

        private void ProcesarOrderFlowTick(DateTime tiempo, double precio, long volumen, double bid, double ask, bool enVivo)
        {
            if (volumen <= 0 || precio <= 0.0 || double.IsNaN(precio)) return;

            if (VolumenMinimoTrade > 0 && volumen < VolumenMinimoTrade)
            {
                precioAnteriorTape = precio;
                return;
            }

            int lado = ClasificarAgresor(precio, bid, ask, enVivo);
            precioAnteriorTape = precio;
            direccionAnteriorTape = lado;

            double deltaTrade = lado * (double)volumen;
            deltaSesionAcumulado += deltaTrade;

            double volCompra = lado > 0 ? volumen : 0.0;
            double volVenta  = lado < 0 ? volumen : 0.0;

            // Ventana móvil rodante de 1 segundo (1000 ms)
            ventanaVelocidad.Enqueue(new PrintCinta { Tiempo = tiempo, VolumenCompra = volCompra, VolumenVenta = volVenta });
            volumenCompraVentana += volCompra;
            volumenVentaVentana  += volVenta;

            DateTime limiteTiempo = tiempo.AddMilliseconds(-VentanaVelocidadMs);
            while (ventanaVelocidad.Count > 0 && ventanaVelocidad.Peek().Tiempo < limiteTiempo)
            {
                PrintCinta viejo = ventanaVelocidad.Dequeue();
                volumenCompraVentana = Math.Max(0.0, volumenCompraVentana - viejo.VolumenCompra);
                volumenVentaVentana  = Math.Max(0.0, volumenVentaVentana - viejo.VolumenVenta);
            }

            double duracionSeg = Math.Max(0.1, VentanaVelocidadMs / 1000.0);
            velocidadCompraActual = volumenCompraVentana / duracionSeg;
            velocidadVentaActual  = volumenVentaVentana / duracionSeg;
            velocidadFlujoTotal   = velocidadCompraActual + velocidadVentaActual;
            double netSpeedNow    = velocidadCompraActual - velocidadVentaActual;

            // Acumular fotografía intrabar en la barra primaria activa
            int primaryBar = PrimaryCurrentBarSafe();
            if (primaryBar >= 0)
            {
                BarFlowData d = GetOrCreateBarData(primaryBar);
                lock (barDataSync)
                {
                    d.Samples++;
                    d.AskVolume     += volCompra;
                    d.BidVolume     += volVenta;
                    d.SumTotalSpeed += velocidadFlujoTotal;
                    d.SumNetSpeed   += netSpeedNow;
                }
            }

            // Actualizar Bracket Delta VND
            ActualizarBracketDelta(volumen, deltaTrade);
        }

        private void ActualizarBracketDelta(long volumen, double deltaTrade)
        {
            colaBracket.Enqueue(new EntradaBracketDelta { Volumen = volumen, DeltaFirmado = deltaTrade });
            volumenBracketActual += volumen;
            deltaBracketActual   += deltaTrade;

            while (volumenBracketActual > 1000 && colaBracket.Count > 0)
            {
                EntradaBracketDelta saliente = colaBracket.Dequeue();
                volumenBracketActual -= saliente.Volumen;
                deltaBracketActual   -= saliente.DeltaFirmado;
            }

            if (!bracketCruzoCero && Math.Abs(deltaBracketActual) > 200)
            {
                if ((deltaBracketActual > 0 && deltaSesionAcumulado < 0) || (deltaBracketActual < 0 && deltaSesionAcumulado > 0))
                    bracketCruzoCero = true;
            }
        }
        #endregion

        #region Robust Session Baseline & Z-Scores
        private BarFlowData GetOrCreateBarData(int barIndex)
        {
            lock (barDataSync)
            {
                BarFlowData d;
                if (!barData.TryGetValue(barIndex, out d))
                {
                    d = new BarFlowData();
                    barData[barIndex] = d;
                }
                return d;
            }
        }

        private BarFlowData GetBarData(int barIndex)
        {
            lock (barDataSync)
            {
                BarFlowData d;
                barData.TryGetValue(barIndex, out d);
                return d;
            }
        }

        private static double MedianaInPlace(List<double> valores)
        {
            int n = valores.Count;
            if (n == 0) return 0.0;
            valores.Sort();
            int mid = n / 2;
            return (n % 2 == 1) ? valores[mid] : 0.5 * (valores[mid - 1] + valores[mid]);
        }

        private double EscalaRobustaConPiso(List<double> valores, double mediana, double ancla)
        {
            int n = valores.Count;
            if (n == 0) return Math.Max(ROBUST_REL_FLOOR * ancla, 1e-9);

            madScratch.Clear();
            for (int i = 0; i < n; i++)
                madScratch.Add(Math.Abs(valores[i] - mediana));

            double mad = MedianaInPlace(madScratch);
            double robusta = ROBUST_MAD_TO_SIGMA * mad;
            double piso = ROBUST_REL_FLOOR * ancla;
            return Math.Max(robusta, Math.Max(piso, 1e-9));
        }

        private void ActualizarZScoresCausales(int barIndex, BarFlowData target)
        {
            if (target == null) return;

            zwTotal.Clear();
            zwNet.Clear();
            zwAbsNet.Clear();
            zwAccel.Clear();

            // Baseline de sesión causal: solo barras completadas anteriores
            if (sessTotal.Count >= ROBUST_MIN_SAMPLES)
            {
                zwTotal.AddRange(sessTotal);
                zwNet.AddRange(sessNet);
                zwAbsNet.AddRange(sessAbsNet);
                zwAccel.AddRange(sessAccel);
            }
            else
            {
                // Fallback rodante por SpeedLookback si la sesión es muy nueva
                int first = Math.Max(0, barIndex - SpeedLookback);
                for (int i = barIndex - 1; i >= first; i--)
                {
                    BarFlowData d = GetBarData(i);
                    if (d != null && d.Samples > 0)
                    {
                        zwTotal.Add(d.TotalSpeed);
                        zwNet.Add(d.NetSpeed);
                        zwAbsNet.Add(d.AbsNetSpeed);
                        zwAccel.Add(d.Acceleration);
                    }
                }
            }

            if (zwTotal.Count < ROBUST_MIN_SAMPLES)
            {
                target.SpeedZ        = 0.0;
                target.NetSpeedZ     = 0.0;
                target.AbsNetSpeedZ  = 0.0;
                target.AccelerationZ = 0.0;
                return;
            }

            double medTotal  = MedianaInPlace(zwTotal);
            double medNet    = MedianaInPlace(zwNet);
            double medAbsNet = MedianaInPlace(zwAbsNet);
            double medAccel  = MedianaInPlace(zwAccel);

            double scaleAbsNet = EscalaRobustaConPiso(zwAbsNet, medAbsNet, Math.Max(medAbsNet, 1e-9));
            double anchor = Math.Max(medAbsNet, scaleAbsNet);

            double scaleTotal = EscalaRobustaConPiso(zwTotal, medTotal, Math.Max(medTotal, anchor));
            double scaleNet   = EscalaRobustaConPiso(zwNet, medNet, anchor);
            double scaleAccel = EscalaRobustaConPiso(zwAccel, medAccel, anchor);

            target.SpeedZ        = (target.TotalSpeed  - medTotal)  / scaleTotal;
            target.NetSpeedZ     = (target.NetSpeed    - medNet)    / scaleNet;
            target.AbsNetSpeedZ  = (target.AbsNetSpeed - medAbsNet) / scaleAbsNet;
            target.AccelerationZ = (target.Acceleration - medAccel)  / scaleAccel;
        }

        private static double Saturate(double val, double low, double high)
        {
            if (high <= low) return val >= high ? 1.0 : 0.0;
            if (val <= low) return 0.0;
            if (val >= high) return 1.0;
            return (val - low) / (high - low);
        }

        private static double Clamp(double val, double min, double max)
        {
            if (val < min) return min;
            if (val > max) return max;
            return val;
        }

        private void CalcularScoreAnomalia(BarFlowData d)
        {
            if (d == null) return;

            double m = Saturate(Math.Max(d.SpeedZ, d.AbsNetSpeedZ), SpeedZThreshold, ExtremeSpeedZ);
            double a = Saturate(Math.Abs(d.AccelerationZ), AccelerationZThreshold, ExtremeSpeedZ);

            double dirZ   = Math.Tanh(d.NetSpeedZ);
            double dirImb = MinimumImbalance <= 0.0 ? 0.0 : Clamp(d.Imbalance / MinimumImbalance, -1.0, 1.0);
            double dir    = Clamp(0.60 * dirZ + 0.40 * dirImb, -1.0, 1.0);

            double magnitude = 100.0 * (0.50 * m + 0.30 * Math.Abs(dir) + 0.20 * a);
            magnitude = Clamp(magnitude, 0.0, 100.0);

            d.AnomalyMagnitude = magnitude;
            d.AnomalySigned    = (dir >= 0.0 ? 1.0 : -1.0) * magnitude;
        }

        private double CalcularNivelEfectivoAnomalia()
        {
            if (!UseAdaptiveAnomalyLevel || anomalyLevelWindow.Count < 5)
                return 45.0;

            anomalyLevelScratch.Clear();
            anomalyLevelScratch.AddRange(anomalyLevelWindow);
            anomalyLevelScratch.Sort();

            double p = Clamp(78.0, 1.0, 99.0) / 100.0;
            int idx = (int)Math.Floor(p * (anomalyLevelScratch.Count - 1));
            if (idx < 0) idx = 0;
            if (idx >= anomalyLevelScratch.Count) idx = anomalyLevelScratch.Count - 1;

            double observed = Clamp(anomalyLevelScratch[idx], 45.0, 80.0);
            double warmupWeight = Clamp((double)anomalyLevelWindow.Count / ANOMALY_LEVEL_MIN_SAMPLES, 0.0, 1.0);
            return Clamp(45.0 + (observed - 45.0) * warmupWeight, 45.0, 80.0);
        }

        private void IncorporarBarraAlBaseline(BarFlowData d)
        {
            if (d == null || d.Samples <= 0) return;

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

            anomalyLevelWindow.Add(d.AnomalyMagnitude);
            while (anomalyLevelWindow.Count > SESSION_BASELINE_MAX)
                anomalyLevelWindow.RemoveAt(0);
        }
        #endregion

        #region Helpers & Price Geometry
        private int PrimaryCurrentBarSafe()
        {
            try
            {
                if (CurrentBars != null && CurrentBars.Length > 0)
                    return CurrentBars[0];
            }
            catch { }
            return -1;
        }

        private double ObtenerMaximoReciente(int startBarsAgo, int count)
        {
            double max = double.MinValue;
            int limit = Math.Min(CurrentBars[0], startBarsAgo + count - 1);
            for (int i = startBarsAgo; i <= limit; i++)
            {
                double h = Highs[0][i];
                if (h > max) max = h;
            }
            return (max == double.MinValue && startBarsAgo <= CurrentBars[0]) ? Highs[0][startBarsAgo] : max;
        }

        private double ObtenerMinimoReciente(int startBarsAgo, int count)
        {
            double min = double.MaxValue;
            int limit = Math.Min(CurrentBars[0], startBarsAgo + count - 1);
            for (int i = startBarsAgo; i <= limit; i++)
            {
                double l = Lows[0][i];
                if (l < min) min = l;
            }
            return (min == double.MaxValue && startBarsAgo <= CurrentBars[0]) ? Lows[0][startBarsAgo] : min;
        }

        private double MedianaRangosRecientesTicks(int lookback)
        {
            List<double> list = new List<double>();
            int limit = Math.Min(CurrentBars[0], lookback);
            double tick = Math.Max(1e-10, TickSize);
            for (int i = 1; i <= limit; i++)
            {
                double r = (Highs[0][i] - Lows[0][i]) / tick;
                if (r > 0) list.Add(r);
            }
            if (list.Count == 0) return 4.0;
            list.Sort();
            return list[list.Count / 2];
        }

        private int NextMarkerStack(int barIndex, bool drawAbove)
        {
            Dictionary<int, int> dict = drawAbove ? markerStackAbove : markerStackBelow;
            int stack = 0;
            if (dict.ContainsKey(barIndex - 1))
                stack = Math.Max(stack, dict[barIndex - 1] + 1);
            if (dict.ContainsKey(barIndex - 2))
                stack = Math.Max(stack, dict[barIndex - 2] + 1);
            if (stack > 3) stack = 0;
            dict[barIndex] = stack;
            return stack;
        }
        #endregion

        #region Phase 1 & 2 Engine (Completed Bars in Series 0)
        private void EncolarBarraCompletada(int barIndex)
        {
            if (barIndex < 0) return;
            if (pendingCompletedBarSet.Add(barIndex))
                pendingCompletedBars.Enqueue(barIndex);
        }

        private void FinalizarBarrasCompletadas()
        {
            if (pendingCompletedBars == null || pendingCompletedBars.Count == 0)
                return;

            int primaryLast = PrimaryCurrentBarSafe();
            if (primaryLast < 0) return;

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

                ProcesarBarraCompletada(completedBar, completedBarsAgo);
            }
        }

        private void ProcesarBarraCompletada(int completedBar, int completedBarsAgo)
        {
            BarFlowData d = GetOrCreateBarData(completedBar);

            // Finalizar medias intrabar de la vela cerrada
            if (d.Samples > 0)
            {
                d.TotalSpeed   = d.SumTotalSpeed / d.Samples;
                d.NetSpeed     = d.SumNetSpeed / d.Samples;
                d.AbsNetSpeed  = Math.Abs(d.NetSpeed);
                double totalVol = d.AskVolume + d.BidVolume;
                d.Imbalance    = totalVol > 0 ? (d.AskVolume - d.BidVolume) / totalVol : 0.0;
            }

            // Calcular aceleración respecto a la barra cerrada previa
            BarFlowData prev = GetBarData(completedBar - 1);
            d.Acceleration = (prev != null && prev.Samples > 0) ? (d.NetSpeed - prev.NetSpeed) : 0.0;

            // Z-scores causales usando exclusivamente barras previas de la sesión
            ActualizarZScoresCausales(completedBar, d);
            CalcularScoreAnomalia(d);
            d.EffectiveAnomalyLevel = CalcularNivelEfectivoAnomalia();

            // ════════════════════════════════════════════════════════════════════════
            // FASE 2: RESOLVER EXCESO PENDIENTE (Prioridad Absoluta)
            // ════════════════════════════════════════════════════════════════════════
            if (pendingExcess.Active && completedBar > pendingExcess.OriginBarIndex)
            {
                TipoSenal resultadoResolucion = ResolverExcesoPendiente(completedBar, completedBarsAgo, d);
                if (resultadoResolucion != TipoSenal.Ninguna)
                {
                    // Emitir señal de resolución confirmada
                    EmitirSenalResolucion(completedBar, completedBarsAgo, resultadoResolucion);
                    IncorporarBarraAlBaseline(d);
                    return;
                }
            }

            // ════════════════════════════════════════════════════════════════════════
            // FASE 1: DETECCIÓN DE NUEVO EXCESO (Solo si no hay pendiente activo)
            // ════════════════════════════════════════════════════════════════════════
            if (!pendingExcess.Active && (completedBar - lastExcessMarkerBar) >= MinimumBarsBetweenSignals)
            {
                EvaluarNuevoExceso(completedBar, completedBarsAgo, d);
            }

            // Incorporar esta barra ya evaluada al baseline de sesión
            IncorporarBarraAlBaseline(d);
        }

        private TipoSenal ResolverExcesoPendiente(int barIndex, int barsAgo, BarFlowData d)
        {
            double tick = Math.Max(1e-10, TickSize);
            double c    = Closes[0][barsAgo];
            double hi   = Highs[0][barsAgo];
            double lo   = Lows[0][barsAgo];
            double o    = Opens[0][barsAgo];

            double barRange = Math.Max(tick, hi - lo);
            double rangeTicks = barRange / tick;
            double bodyTicks  = (c - o) / tick;
            double closeLocation = Clamp((c - lo) / barRange, 0.0, 1.0);

            double originalDir = pendingExcess.IsBuy ? 1.0 : -1.0;
            double priceFollow = Clamp(0.60 * (bodyTicks / Math.Max(1.0, rangeTicks)) + 0.40 * (2.0 * closeLocation - 1.0), -1.0, 1.0);
            double priceAlignment = originalDir * priceFollow;

            double flowAlignment = 0.0;
            if (d != null && d.Samples > 0)
            {
                double dirZ   = Math.Tanh(d.NetSpeedZ);
                double dirImb = MinimumImbalance <= 0.0 ? 0.0 : Clamp(d.Imbalance / MinimumImbalance, -1.0, 1.0);
                flowAlignment = originalDir * Clamp(0.60 * dirZ + 0.40 * dirImb, -1.0, 1.0);
            }

            // Geometría del evento
            double baseRange     = pendingExcess.BaseRange > 0.0 ? pendingExcess.BaseRange : pendingExcess.EventRange;
            double geometryRange = Math.Min(pendingExcess.EventRange, baseRange * 1.25);
            double eventRangeTicks = Math.Max(1.0, geometryRange / tick);

            double localMedianTicks = MedianaRangosRecientesTicks(SpeedLookback);
            double liveScaleTicks   = 0.60 * eventRangeTicks + 0.40 * localMedianTicks;
            pendingExcess.ResponseScaleTicks = pendingExcess.ResponseScaleTicks <= 0.0
                ? liveScaleTicks
                : 0.75 * pendingExcess.ResponseScaleTicks + 0.25 * liveScaleTicks;

            double strengthRatio = Clamp(pendingExcess.EventMagnitude / Math.Max(1.0, pendingExcess.EventLevel), 0.75, 1.75);
            double strength01    = strengthRatio - 0.75;
            double geometryEase  = 1.0 - 0.10 * strength01;

            double noiseFloorTicks   = Clamp(pendingExcess.ResponseScaleTicks * 0.15, 1.0, Math.Max(1.0, eventRangeTicks * 0.50));
            double continuationTicks = Math.Max(noiseFloorTicks, eventRangeTicks * 1.20 * geometryEase);
            double rotationMidTicks  = Math.Max(noiseFloorTicks, eventRangeTicks * 1.00 * geometryEase);
            double rotationFullTicks = Math.Max(noiseFloorTicks * 2.0, eventRangeTicks * 2.00 * geometryEase);

            double favorableCloseTicks;
            double adverseCloseTicks;
            double favorableExcursion;

            if (pendingExcess.IsBuy)
            {
                favorableCloseTicks = (c - pendingExcess.High) / tick;
                adverseCloseTicks   = (pendingExcess.High - c) / tick;
                favorableExcursion  = (hi - pendingExcess.High) / tick;
            }
            else
            {
                favorableCloseTicks = (pendingExcess.Low - c) / tick;
                adverseCloseTicks   = (c - pendingExcess.Low) / tick;
                favorableExcursion  = (pendingExcess.Low - lo) / tick;
            }

            favorableCloseTicks = Math.Max(0.0, favorableCloseTicks);
            adverseCloseTicks   = Math.Max(0.0, adverseCloseTicks);
            pendingExcess.BestFavorableTicks = Math.Max(pendingExcess.BestFavorableTicks, Math.Max(0.0, favorableExcursion));

            double continuationDisplacement = Clamp(favorableCloseTicks / Math.Max(1.0, continuationTicks), 0.0, 1.0);
            double rotationDisplacement     = Clamp(adverseCloseTicks / Math.Max(1.0, rotationMidTicks), 0.0, 1.0);

            double continuationScore = 0.60 * continuationDisplacement
                                     + 0.25 * Math.Max(0.0, priceAlignment)
                                     + 0.15 * Math.Max(0.0, flowAlignment);

            double rotationScore     = 0.60 * rotationDisplacement
                                     + 0.25 * Math.Max(0.0, -priceAlignment)
                                     + 0.15 * Math.Max(0.0, -flowAlignment);

            // Subasta favorable fallida: mecha supera pero cierra adentro
            if (pendingExcess.BestFavorableTicks >= continuationTicks && favorableCloseTicks < continuationTicks * 0.25 && priceAlignment < 0.0)
                rotationScore = Math.Min(1.0, rotationScore + 0.08);

            // Vía Rápida: respuesta agresiva opuesta
            bool aggressiveOpposite = false;
            if (d != null && d.Samples > 0)
            {
                bool oppSpeed = (d.SpeedZ >= SpeedZThreshold || d.AbsNetSpeedZ >= SpeedZThreshold);
                bool oppAccel = (originalDir * d.AccelerationZ) <= -AccelerationZThreshold;
                aggressiveOpposite = oppSpeed && oppAccel && flowAlignment <= -0.35 && priceAlignment <= -0.10 && rangeTicks >= MinimumRangeTicks;
            }
            bool rotationFastPath = aggressiveOpposite && adverseCloseTicks >= Math.Max(2.0, rotationMidTicks * 0.70);

            // Evidencia acumulativa con memoria
            bool contEvidence = favorableCloseTicks >= continuationTicks * 0.60 && priceAlignment >= -0.05 && continuationScore >= 0.52;
            bool rotEvidence  = adverseCloseTicks >= rotationMidTicks * 0.60 && priceAlignment <= 0.05 && rotationScore >= 0.52;

            pendingExcess.ContinuationEvidenceBars = contEvidence ? pendingExcess.ContinuationEvidenceBars + 1 : Math.Max(0, pendingExcess.ContinuationEvidenceBars - 1);
            pendingExcess.RotationEvidenceBars     = rotEvidence  ? pendingExcess.RotationEvidenceBars + 1  : Math.Max(0, pendingExcess.RotationEvidenceBars - 1);

            // Confirmaciones
            bool continuationConfirmed = favorableCloseTicks >= continuationTicks && continuationScore >= 0.55
                && (priceAlignment >= 0.05 || flowAlignment >= 0.15 || pendingExcess.ContinuationEvidenceBars >= 2);

            bool rotationStrong   = adverseCloseTicks >= rotationFullTicks && priceAlignment <= 0.0;
            bool rotationStandard = adverseCloseTicks >= rotationMidTicks && rotationScore >= 0.55
                && (priceAlignment <= -0.05 || flowAlignment <= -0.15 || pendingExcess.RotationEvidenceBars >= 2);

            bool rotationConfirmed = rotationStrong || rotationStandard || rotationFastPath;

            // Restricciones de la primera barra de respuesta
            int responseOrdinal = Math.Max(1, ExcessConfirmWindow) - pendingExcess.BarsLeft + 1;
            if (responseOrdinal <= 1)
            {
                bool singleBarDrive = favorableCloseTicks >= eventRangeTicks * 0.95 * geometryEase
                    && priceAlignment >= 0.80 && flowAlignment >= 0.25 && continuationScore >= 0.78;

                if (RequireSustainedAcceptance)
                {
                    bool contDecisive = favorableCloseTicks >= continuationTicks * 1.35 && continuationScore >= 0.62;
                    continuationConfirmed = continuationConfirmed && contDecisive;
                    // En barra 1 solo sobreviven rotación fuerte o vía rápida con flujo opuesto
                    rotationConfirmed = rotationStrong || rotationFastPath;
                }
                continuationConfirmed = continuationConfirmed || singleBarDrive;
            }

            // Arbitraje Final
            if (continuationConfirmed && !rotationStrong && (!rotationConfirmed || continuationScore >= rotationScore))
            {
                pendingExcess.Active = false;
                return pendingExcess.IsBuy ? TipoSenal.ContinuacionCompradora : TipoSenal.ContinuacionVendedora;
            }

            if (rotationConfirmed)
            {
                pendingExcess.Active = false;
                return pendingExcess.IsBuy ? TipoSenal.RotacionBajista : TipoSenal.RotacionAlcista;
            }

            // Cuenta regresiva de barras de la ventana
            if (barIndex > pendingExcess.LastBarIndex)
            {
                pendingExcess.LastBarIndex = barIndex;
                pendingExcess.BarsLeft--;
                if (pendingExcess.BarsLeft <= 0)
                {
                    pendingExcess.Active = false; // Expirado causalmente sin resolución
                }
            }

            return TipoSenal.Ninguna;
        }

        private void EvaluarNuevoExceso(int barIndex, int barsAgo, BarFlowData d)
        {
            if (d == null || d.Samples <= 0) return;

            bool speedAnomaly = (d.SpeedZ >= SpeedZThreshold || d.AbsNetSpeedZ >= SpeedZThreshold);
            bool extremeSpeed = (d.SpeedZ >= ExtremeSpeedZ || d.AbsNetSpeedZ >= ExtremeSpeedZ);

            bool buyFlow  = (d.Imbalance >= MinimumImbalance || d.NetSpeedZ >= 0.50);
            bool sellFlow = (d.Imbalance <= -MinimumImbalance || d.NetSpeedZ <= -0.50);

            int buyEvidence = (d.Imbalance >= MinimumImbalance ? 1 : 0)
                            + (d.NetSpeedZ >= 0.50 ? 1 : 0)
                            + (d.AccelerationZ >= AccelerationZThreshold ? 1 : 0);

            int sellEvidence = (d.Imbalance <= -MinimumImbalance ? 1 : 0)
                             + (d.NetSpeedZ <= -0.50 ? 1 : 0)
                             + (d.AccelerationZ <= -AccelerationZThreshold ? 1 : 0);

            bool buyConfirmed  = buyEvidence >= 2;
            bool sellConfirmed = sellEvidence >= 2;

            double tick = Math.Max(1e-10, TickSize);
            double highVal = Highs[0][barsAgo];
            double lowVal  = Lows[0][barsAgo];

            double recentHigh = ObtenerMaximoReciente(barsAgo + 1, ExcessLookback);
            double recentLow  = ObtenerMinimoReciente(barsAgo + 1, ExcessLookback);

            bool atRecentHigh = highVal >= recentHigh - tick * 0.5;
            bool atRecentLow  = lowVal  <= recentLow  + tick * 0.5;

            bool excessStrong = d.AnomalyMagnitude >= d.EffectiveAnomalyLevel;

            bool excessBuyEvent  = (speedAnomaly || extremeSpeed) && buyFlow  && buyConfirmed  && atRecentHigh && excessStrong;
            bool excessSellEvent = (speedAnomaly || extremeSpeed) && sellFlow && sellConfirmed && atRecentLow  && excessStrong;

            if (excessBuyEvent || excessSellEvent)
            {
                bool isBuy = excessBuyEvent && !excessSellEvent ? true : (excessSellEvent && !excessBuyEvent ? false : d.NetSpeed >= 0.0);

                pendingExcess.Active               = true;
                pendingExcess.IsBuy                = isBuy;
                pendingExcess.High                 = highVal;
                pendingExcess.Low                  = lowVal;
                pendingExcess.EventRange           = Math.Max(tick, highVal - lowVal);
                pendingExcess.BaseRange            = pendingExcess.EventRange;
                pendingExcess.OriginBarIndex       = barIndex;
                pendingExcess.LastBarIndex         = barIndex;
                pendingExcess.BarsLeft             = Math.Max(1, ExcessConfirmWindow);
                pendingExcess.EventMagnitude       = d.AnomalyMagnitude;
                pendingExcess.EventLevel           = d.EffectiveAnomalyLevel;
                pendingExcess.ResponseScaleTicks   = 0.60 * (pendingExcess.EventRange / tick) + 0.40 * MedianaRangosRecientesTicks(SpeedLookback);
                pendingExcess.BestFavorableTicks   = 0.0;
                pendingExcess.ContinuationEvidenceBars = 0;
                pendingExcess.RotationEvidenceBars     = 0;

                lastExcessMarkerBar = barIndex;

                // Marcar vela y dibujar etiqueta de anticipación
                string lbl = isBuy ? "EXB" : "EXS";
                MediaBrush br = ColorExceso;
                bool arriba = isBuy; // EXB encima del High, EXS debajo del Low

                if (PintarVelas && br != null)
                {
                    BarBrushes[barsAgo]           = br;
                    CandleOutlineBrushes[barsAgo] = br;
                }

                DrawSignal(barsAgo, lbl, br, arriba);
            }
        }

        private void EmitirSenalResolucion(int barIndex, int barsAgo, TipoSenal senal)
        {
            string lbl = "";
            MediaBrush br = null;
            bool arriba = false;

            if (senal == TipoSenal.RotacionBajista)
            {
                lbl = "RTS";
                br = ColorRotacionBajista;
                arriba = true; // Rotación vendedora arriba de la vela
                CalcularConfluenciaYScore(Closes[0][barsAgo], DireccionFlujo.Bajista);
            }
            else if (senal == TipoSenal.RotacionAlcista)
            {
                lbl = "RTB";
                br = ColorRotacionAlcista;
                arriba = false; // Rotación compradora debajo de la vela
                CalcularConfluenciaYScore(Closes[0][barsAgo], DireccionFlujo.Alcista);
            }
            else if (senal == TipoSenal.ContinuacionCompradora)
            {
                if (!MostrarContinuaciones) return;
                lbl = "CB";
                br = ColorContinuacion;
                arriba = false;
            }
            else if (senal == TipoSenal.ContinuacionVendedora)
            {
                if (!MostrarContinuaciones) return;
                lbl = "CS";
                br = ColorContinuacion;
                arriba = true;
            }

            if (PintarVelas && br != null)
            {
                BarBrushes[barsAgo]           = br;
                CandleOutlineBrushes[barsAgo] = br;
            }

            DrawSignal(barsAgo, lbl, br, arriba);
        }

        private void DrawSignal(int barsAgo, string label, MediaBrush brush, bool drawAbove)
        {
            int primaryLast = CurrentBars[0];
            if (barsAgo < 0 || barsAgo > primaryLast) return;

            int barIndex = primaryLast - barsAgo;
            string tagBase = string.Format("EXR_{0}_{1}", barIndex, label);
            int stack = NextMarkerStack(barIndex, drawAbove);
            double tick = Math.Max(1e-10, TickSize);
            double offset = MarkerOffsetTicks * tick * (1.0 + 0.85 * stack);
            double y = drawAbove ? Highs[0][barsAgo] + offset : Lows[0][barsAgo] - offset;

            if (DibujarFlechas)
            {
                string arrowTag = tagBase + "_ARW";
                if (drawAbove)
                    Draw.ArrowDown(this, arrowTag, false, barsAgo, y, brush);
                else
                    Draw.ArrowUp(this, arrowTag, false, barsAgo, y, brush);
            }

            if (DibujarEtiquetas && !string.IsNullOrEmpty(label))
            {
                string textTag = tagBase + "_TXT";
                double textY = drawAbove ? y + offset * 1.4 : y - offset * 1.4;
                SimpleFont sf = new SimpleFont("Segoe UI", 11) { Bold = true };
                Draw.Text(this, textTag, false, label, barsAgo, textY, 0, brush, sf,
                    System.Windows.TextAlignment.Center, System.Windows.Media.Brushes.Transparent, System.Windows.Media.Brushes.Transparent, 0);
            }
        }
        #endregion

        #region Confluence & Delta Scoring
        private enum DireccionFlujo { Alcista, Bajista }

        private void CalcularConfluenciaYScore(double precio, DireccionFlujo dirRot)
        {
            ultimoScoreRotacion = 0.0;
            int nivelesCoincidentes = 0;
            double tick = Math.Max(1E-10, TickSize);
            double tol  = ToleranciaNivelTicks * tick;
            List<string> listaConf = new List<string>();

            // 1. High / Low Europa (+10 pts, Nivel Rector de mayor peso)
            if (!double.IsNaN(ultimoEuropaHigh) && Math.Abs(precio - ultimoEuropaHigh) <= tol)
            { ultimoScoreRotacion += 10.0; nivelesCoincidentes++; listaConf.Add("EU.H"); }
            if (!double.IsNaN(ultimoEuropaLow) && Math.Abs(precio - ultimoEuropaLow) <= tol)
            { ultimoScoreRotacion += 10.0; nivelesCoincidentes++; listaConf.Add("EU.L"); }

            // 2. High / Low Asia (+8 pts)
            if (!double.IsNaN(ultimoAsiaHigh) && Math.Abs(precio - ultimoAsiaHigh) <= tol)
            { ultimoScoreRotacion += 8.0; nivelesCoincidentes++; listaConf.Add("AS.H"); }
            if (!double.IsNaN(ultimoAsiaLow) && Math.Abs(precio - ultimoAsiaLow) <= tol)
            { ultimoScoreRotacion += 8.0; nivelesCoincidentes++; listaConf.Add("AS.L"); }

            // 3. VWAP (+6 pts)
            if (!double.IsNaN(vwapActual) && Math.Abs(precio - vwapActual) <= tol)
            { ultimoScoreRotacion += 6.0; nivelesCoincidentes++; listaConf.Add("VWAP"); }

            // 4. VAH / VAL (+5 pts)
            if (!double.IsNaN(vahPrecio) && Math.Abs(precio - vahPrecio) <= tol)
            { ultimoScoreRotacion += 5.0; nivelesCoincidentes++; listaConf.Add("VAH"); }
            if (!double.IsNaN(valPrecio) && Math.Abs(precio - valPrecio) <= tol)
            { ultimoScoreRotacion += 5.0; nivelesCoincidentes++; listaConf.Add("VAL"); }

            // 5. POC (+4 pts)
            if (!double.IsNaN(pocPrecio) && Math.Abs(precio - pocPrecio) <= tol)
            { ultimoScoreRotacion += 4.0; nivelesCoincidentes++; listaConf.Add("POC"); }

            // Bono por confluencia multinivel
            if (nivelesCoincidentes > 1)
                ultimoScoreRotacion += (nivelesCoincidentes - 1) * 2.5;

            // 6. Gatillo de Apertura (09:30 NY primera barra de 15m tocando/rompiendo Europa H/L)
            if (CurrentBars[0] >= 0)
            {
                DateTime nyTime = AHoraNuevaYork(Time[0]);
                if (ActivarGatilloApertura && nyTime.Hour == 9 && nyTime.Minute >= 30 && nyTime.Minute < 45)
                {
                    ultimoScoreRotacion += 8.0;
                    listaConf.Add("APERTURA 09:30 NY");
                }
            }

            // 7. Modificador de Delta (Estructural vs Scalp)
            bool deltaConvergente = (dirRot == DireccionFlujo.Alcista && deltaSesionAcumulado > deltaPrevioBarra) ||
                                    (dirRot == DireccionFlujo.Bajista && deltaSesionAcumulado < deltaPrevioBarra);

            if (deltaConvergente)
            {
                ultimoScoreRotacion += 5.0;
                ultimoDetalleDelta = "ESTRUCTURAL (Conv)";
                listaConf.Add("DELTA CONV");
            }
            else
            {
                ultimoScoreRotacion -= 4.0;
                ultimoDetalleDelta = "SCALP (Div)";
                listaConf.Add("DELTA DIV");
            }

            ultimoDetalleConfluencia = listaConf.Count > 0 ? string.Join(" | ", listaConf.ToArray()) : "SIN CONFLUENCIA";
        }
        #endregion

        #region OnBarUpdate Callback
        protected override void OnBarUpdate()
        {
            // BIP 1: Serie secundaria de 1 Tick para cálculo de Order Flow
            if (BarsInProgress == 1)
            {
                if (CurrentBars[1] < 1) return;

                DateTime tiempo = Times[1][0];
                double precio   = Closes[1][0];
                long volumen    = Math.Max(1L, Convert.ToInt64(Volumes[1][0]));

                double flowBid = lastBid;
                double flowAsk = lastAsk;
                try
                {
                    flowBid = BarsArray[1].GetBid(CurrentBars[1]);
                    flowAsk = BarsArray[1].GetAsk(CurrentBars[1]);
                    lastBid = flowBid;
                    lastAsk = flowAsk;
                }
                catch { }

                ProcesarOrderFlowTick(tiempo, precio, volumen, flowBid, flowAsk, State == State.Realtime);
                ActualizarSesionesYPerfil(tiempo, precio, volumen);

                int primaryBar = PrimaryCurrentBarSafe();
                if (lastTradePrimaryBarIndex >= 0 && primaryBar != lastTradePrimaryBarIndex)
                {
                    EncolarBarraCompletada(lastTradePrimaryBarIndex);
                }
                lastTradePrimaryBarIndex = primaryBar;
                return;
            }

            // BIP 0: Serie primaria visible del gráfico
            if (BarsInProgress != 0 || CurrentBars[0] < 0)
                return;

            // Procesar y evaluar todas las barras cerradas que completó BIP 1
            FinalizarBarrasCompletadas();

            if (CurrentBars[0] > 0)
            {
                deltaPrevioBarra = deltaSesionAcumulado;
            }

            RecalcularValueArea(0.70);

            // Dibujar líneas rector de sesión (Europa H/L, Asia H/L, VWAP)
            DibujarLineasDeNivel();
        }

        private void DibujarLineasDeNivel()
        {
            if (!DibujarNiveles || CurrentBars[0] < 5) return;

            // Europa High / Low (Nivel Rector de mayor jerarquía)
            if (!double.IsNaN(ultimoEuropaHigh) && ultimoEuropaHigh > 0)
            {
                Draw.HorizontalLine(this, "Line_EU_H", ultimoEuropaHigh, System.Windows.Media.Brushes.LimeGreen, DashStyleHelper.Dash, 2);
                Draw.Text(this, "Txt_EU_H", string.Format("EU.H {0:F2}", ultimoEuropaHigh), 0, ultimoEuropaHigh + 2 * TickSize, System.Windows.Media.Brushes.LimeGreen);
            }
            if (!double.IsNaN(ultimoEuropaLow) && ultimoEuropaLow > 0)
            {
                Draw.HorizontalLine(this, "Line_EU_L", ultimoEuropaLow, System.Windows.Media.Brushes.LimeGreen, DashStyleHelper.Solid, 2);
                Draw.Text(this, "Txt_EU_L", string.Format("EU.L {0:F2}", ultimoEuropaLow), 0, ultimoEuropaLow - 2 * TickSize, System.Windows.Media.Brushes.LimeGreen);
            }

            // Asia High / Low
            if (!double.IsNaN(ultimoAsiaHigh) && ultimoAsiaHigh > 0)
            {
                Draw.HorizontalLine(this, "Line_AS_H", ultimoAsiaHigh, System.Windows.Media.Brushes.Gold, DashStyleHelper.Dot, 1);
            }
            if (!double.IsNaN(ultimoAsiaLow) && ultimoAsiaLow > 0)
            {
                Draw.HorizontalLine(this, "Line_AS_L", ultimoAsiaLow, System.Windows.Media.Brushes.Gold, DashStyleHelper.Dot, 1);
            }

            // FS VWAP
            if (!double.IsNaN(vwapActual) && vwapActual > 0)
            {
                Draw.HorizontalLine(this, "Line_VWAP", vwapActual, System.Windows.Media.Brushes.Yellow, DashStyleHelper.Solid, 2);
                Draw.Text(this, "Txt_VWAP", "FS VWAP", 0, vwapActual + 2 * TickSize, System.Windows.Media.Brushes.Yellow);
            }
        }
        #endregion

        #region SharpDX Direct2D HUD Dashboard (Only Screen HUD - No Clutter on Bars)
        private void DisposeDxResources()
        {
            if (dxBrushHudBg != null) { dxBrushHudBg.Dispose(); dxBrushHudBg = null; }
            if (dxBrushHudBorder != null) { dxBrushHudBorder.Dispose(); dxBrushHudBorder = null; }
            if (dxBrushTexto != null) { dxBrushTexto.Dispose(); dxBrushTexto = null; }
            if (dxBrushAcento != null) { dxBrushAcento.Dispose(); dxBrushAcento = null; }
            if (tfHud != null) { tfHud.Dispose(); tfHud = null; }
            if (tfHudBold != null) { tfHudBold.Dispose(); tfHudBold = null; }
            if (dwFactory != null) { dwFactory.Dispose(); dwFactory = null; }
            dxTarget = null;
        }

        private void EnsureDxResources(DxRenderTarget rt)
        {
            if (dxTarget != rt)
            {
                DisposeDxResources();
                dxTarget = rt;

                dxBrushHudBg     = new DxSolidColorBrush(rt, new Color4(0.06f, 0.08f, 0.11f, 0.88f));
                dxBrushHudBorder = new DxSolidColorBrush(rt, new Color4(0.20f, 0.28f, 0.38f, 0.95f));
                dxBrushTexto     = new DxSolidColorBrush(rt, new Color4(0.92f, 0.94f, 0.96f, 1.0f));
                dxBrushAcento    = new DxSolidColorBrush(rt, new Color4(0.0f, 0.75f, 1.0f, 1.0f));

                dwFactory = new SharpDX.DirectWrite.Factory();
                tfHud     = new TextFormat(dwFactory, "Segoe UI", FontWeight.Normal, FontStyle.Normal, 11f);
                tfHudBold = new TextFormat(dwFactory, "Segoe UI", FontWeight.Bold, FontStyle.Normal, 11.5f);
            }
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);

            if (!MostrarHud || chartControl == null || chartScale == null)
                return;

            DxRenderTarget rt = RenderTarget;
            if (rt == null) return;

            EnsureDxResources(rt);
            RenderHudPanel(rt, chartControl);
        }

        private void RenderHudPanel(DxRenderTarget rt, ChartControl cc)
        {
            float w = 270f;
            float h = 138f;
            float x = (float)cc.ActualWidth - w - 50f;
            float y = 25f;

            var bgRect = new RectangleF(x, y, w, h);
            rt.FillRectangle(bgRect, dxBrushHudBg);
            rt.DrawRectangle(bgRect, dxBrushHudBorder, 1.2f);

            string nomSesion = sesionActual.ToString().ToUpper();
            string deltaEstado = string.IsNullOrEmpty(ultimoDetalleDelta) ? "NEUTRAL" : ultimoDetalleDelta;
            string pendingTxt = pendingExcess.Active ? string.Format("PENDIENTE ({0}) - Restan: {1}", pendingExcess.IsBuy ? "EXB" : "EXS", pendingExcess.BarsLeft) : "IDLE (Buscando Exceso)";

            string header = string.Format("ILT-DEV EXCESO Y ROTACIÓN | {0}", nomSesion);
            string line1 = string.Format("Delta Sesión: {0:+0;-0;0} | {1}", deltaSesionAcumulado, deltaEstado);
            string line2 = string.Format("Vel. Cinta: {0:F1} c/s | Umbral Z: {1:F2}", velocidadFlujoTotal, SpeedZThreshold);
            string line3 = string.Format("Estado: {0}", pendingTxt);
            string line4 = string.Format("Últ. Score: {0:F1} pts | {1}", ultimoScoreRotacion, string.IsNullOrEmpty(ultimoDetalleConfluencia) ? "N/A" : ultimoDetalleConfluencia);
            string line5 = string.Format("VWAP: {0:F2} | EU.H: {1:F2} | EU.L: {2:F2}",
                double.IsNaN(vwapActual) ? 0 : vwapActual,
                double.IsNaN(ultimoEuropaHigh) ? 0 : ultimoEuropaHigh,
                double.IsNaN(ultimoEuropaLow) ? 0 : ultimoEuropaLow);

            var titleRect = new RectangleF(x + 10f, y + 8f, w - 20f, 18f);
            rt.DrawText(header, tfHudBold, titleRect, dxBrushAcento);

            string body = string.Join("\n", new[] { line1, line2, line3, line4, line5 });
            var textRect = new RectangleF(x + 10f, y + 28f, w - 20f, h - 34f);
            rt.DrawText(body, tfHud, textRect, dxBrushTexto);
        }
        #endregion
    }
}

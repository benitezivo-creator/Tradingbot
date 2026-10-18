#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
#endregion

// =============================================================================
//  ILT-DEV PROFILES  —  Professional TPO / Volume / Delta Market Profile
//  Platform : NinjaTrader 8
//  Version  : 8.0 PRODUCTION (timeframe-locked profile, flicker-free render)
//
//  CALCULATION ENGINE
//   - Deterministic POC/VPOC with center tie-break (CME / Dalton standard)
//   - Canonical 2-TPO Value Area expansion (shared TPO and Volume engine)
//   - Bid/Ask trade classification with Tick-Replay support (institutional grade)
//   - Delta normalization recomputed correctly on every dirty pass
//   - TIMEFRAME-INDEPENDENT: the profile is built from an internal fixed 1-minute
//     sampling series (BarsArray[2]), NOT from the chart's bars. POC/VA/IB/OR and
//     every zone are therefore identical on Range 40, 5min, 15min or any chart.
//     Changing the chart timeframe no longer alters the profile. Volume/Delta come
//     from the tick series / Tick-Replay, also timeframe-independent.
//
//  SESSION DISCIPLINE
//   - RTH window 09:30–17:00 NY, ETH window 18:00–09:30 NY (configurable)
//   - 17:00–18:00 dead zone: no profile accumulates or draws there
//   - Session rollover is detected on the 1-minute clock for precise boundaries
//   - Each profile lives strictly inside its theoretical time slot;
//     never invades the next session regardless of zoom level
//
//  RENDERING
//   - Allocation-free render path (no per-frame TextLayout / StrokeStyle churn)
//   - Cached DirectWrite formats and Direct2D stroke styles
//   - All placement is TIME-anchored (TimeToX), so profiles stay glued to their
//     real clock location independent of the chart's bar indexing
//   - Per-frame memoization of the session-window pixels (one time→bar lookup
//     per profile per frame) for smooth, low-CPU redraws
//   - LOD hysteresis + quantized silhouette merge: dragging the price (Y) axis no
//     longer makes the profile flicker, resize or jump between detail modes
//   - Zoom-aware column widths and panel widths (tight, professional look)
//   - Suite-ready render guards for shared workspaces with Heatmap, Footprint,
//     Flow Speed, VWAP and Levels. Calculation engines remain unchanged.
//
//  UI
//   - Floating WPF panel with stats and per-profile toggles (draggable,
//     minimizable, persists position across workspace reloads)
//   - Top-toolbar button (chart-window MainMenu) to show/hide the panel
//   - Granular VA/POC extension toggles: TPO RTH, TPO ETH, VP RTH, VP ETH
//   - Configurable line style (solid/dash/dot/dashdot), opacity, and color
//     for VA, IB and OR
//
//  COMPAT
//   - The "NinjaScript generated code" block at the end of the file is
//     intentionally a stub. NinjaTrader regenerates it on compile (F5).
// =============================================================================

namespace NinjaTrader.NinjaScript
{
    // Profile period mode - simplified to RTH session only.
    // Overnight ETH session is handled by the separate _currentON tracker
    // when ShowOvernight is enabled.
    public enum TpoLetterColoring    { SingleColor, IBHighlight }
    public enum TpoOrientation       { LeftToRight, RightToLeft }
    public enum TpoRenderMode        { Letters, Blocks }
    // Modern visual mode used only by the SharpDX render layer.
    // It does not change the profile calculation engine; it only decides how
    // much detail is drawn for current vs. historical sessions.
    public enum TpoProfileVisualMode { Auto, Letters, Blocks, Silhouette, Minimal }
    public enum TpoCompositeMode     { LastNSessions, LastNBars, RolloverMinutes, FixedRange }
    public enum TpoDeltaAlignment    { Centered, LeftAligned, RightAligned }
    // Estilo de dibujo del Volume Profile y el Composite:
    //   Bars    = barras horizontales clasicas (histograma)
    //   Outline = contorno de linea que sigue la forma (picos/valles), esquinas
    //             suavizadas, con relleno tenue
    //   Both    = histograma de barras + contorno encima
    public enum TpoVolumeStyle       { Bars, Outline, Both }
    // How to classify each trade as buy or sell volume:
    //   BidAsk      = real bid/ask at trade time (requires Tick Replay) - institutional grade
    //   UpDownTick  = approximate via price-change direction - works without Tick Replay
    //   Auto        = use BidAsk when bid/ask data is valid, fall back to UpDownTick otherwise
    public enum TpoDeltaMode         { Auto, BidAsk, UpDownTick }
    // Modo de periodo: Sesiones = ETH/RTH separadas (comportamiento clasico);
    // Diario = un perfil por dia completo (24h, de apertura RTH a apertura RTH);
    // Semanal = un perfil por semana completa. Solo cambia CUANDO abre/cierra
    // cada perfil y que horas absorbe — el motor de calculo es identico.
    public enum TpoSessionPeriod     { Sesiones, Diario, Semanal }
    public enum TpoLineStyle         { Solid, Dash, Dot, DashDot }
    public enum TpoDayType
    {
        Unknown,
        NonTrend,           // rango < 0.5x IB - mercado muy quieto
        Normal,             // rango ~ 1x IB - dia balanceado
        NormalVariation,    // rango 1-2x IB - extension moderada
        Trend,              // rango > 2x IB + cierre cerca del extremo - dia direccional
        DoubleDistribution, // dos clusters TPO separados por single prints
        Neutral             // rango sale por ambos lados del IB
    }
}

namespace NinjaTrader.NinjaScript.Indicators.ILT_DEV_SUITE
{
    internal class TpoLetterItem
    {
        public char   Letter;
        public double Price;
        public int    BarIndex;
        public int    PeriodIndex;
        public bool   IsIB;
    }

    internal class TpoPriceLevel
    {
        public double              Price;
        public List<TpoLetterItem> Letters = new List<TpoLetterItem>();
        public int Count { get { return Letters.Count; } }
        public char LastLetter = '\0';  // visual letter cache
        public int  LastPeriodIndex = int.MinValue; // fast path: unique TPO period id, not limited by alphabet length
        public double Volume;       // accumulated total volume at this price level
        public double BuyVolume;    // approximate buy volume (bullish bars)
        public double SellVolume;   // approximate sell volume (bearish bars)
        public double Delta { get { return BuyVolume - SellVolume; } }
    }

    internal class TpoProfileData
    {
        public DateTime                          StartTime;
        public DateTime                          EndTime;
        public bool                              IsOvernight;
        // Highest TPO sub-period index seen (latest letter). Normalizes the modern
        // time-based cell shading: earliest sub-period = dim, latest = bright.
        public int                               MaxPeriodIndex = 0;
        public Dictionary<double, TpoPriceLevel> Levels
            = new Dictionary<double, TpoPriceLevel>();
        public List<double> SortedKeys = new List<double>(); // cached ascending price ladder for calculations/rendering
        public bool SortedKeysDirty = true;                  // true only when a new price level is created
        public double POC;
        public double VAH;
        public double VAL;
        public double VolVAH;   // Volume-based Value Area High
        public double VolVAL;   // Volume-based Value Area Low
        public double VPOC;     // Volume Point of Control (deterministic, center-tie-break)
        public double IBH;
        public double IBL;
        public double ORH;   // Opening Range High
        public double ORL;   // Opening Range Low
        public double ProfileHigh;
        public double ProfileLow;
        public double LastClose;   // close of the most recent in-session 1-min bar (for AMT day-type)
        public int    TotalTPOs;
        public int    MaxCount;
        public double MaxVolume;  // for volume profile normalization
        public double MaxAbsDelta; // for delta profile normalization (max |buy-sell|)
        // Bar indices on the 1-min SAMPLING series (BarsArray[2]), NOT the chart's
        // primary series. They are metadata only — all rendering anchors by TIME
        // (see TimeToX / PrimaryBarIdxByTime), so these never drive pixel math.
        public int    FirstBarIdx;
        public int    LastBarIdx;

        // Theoretical session window in time (independent of bar accumulation).
        // RTH: WindowStart = SessionDate 09:30, WindowEnd = SessionDate 17:00.
        // ETH: WindowStart = (SessionDate-1) 18:00, WindowEnd = SessionDate 09:30.
        // These are the bounds the profile must DRAW within so it never invades
        // the next session, regardless of how many bars it currently holds.
        public DateTime WindowStart;
        public DateTime WindowEnd;
        public bool   ORComplete;
        public TpoDayType DayType = TpoDayType.Unknown;  // AMT day type classification

        // Session identity key (date-based) — lets ticks route to the correct
        // profile by timestamp rather than by relying on primary-bar boundaries.
        public DateTime SessionKey;

        // Dirty flag — set when the profile's data changes (new tick volume or
        // new TPO). POC/VA/stats are only recomputed when dirty, NOT on every
        // render or every tick. This keeps the indicator fast on large datasets.
        public bool   IsDirty = true;

        // Render lifecycle flag. Closed sessions can be frozen after their final
        // POC/VA/VPOC pass so historical profiles behave like static drawings.
        // This never blocks the current live session and never changes the data.
        public bool   IsFinalized = false;

        // Last resolved visual mode (Letters/Blocks/Silhouette/Minimal). Used by
        // the LOD hysteresis so dragging the Y axis across a threshold doesn't
        // flip the mode every frame (the cause of the resize/flicker artifact).
        // Purely a render cache — never affects the calculation engine.
        public TpoProfileVisualMode LastVisualMode = TpoProfileVisualMode.Auto;

        // Last committed vertical row-merge factor, with the tickPx it was decided
        // at. Used by StableMerge() so dragging the price (Y) axis up/down doesn't
        // make rows flip between "normal" and "grouped" sizes every frame. Render-
        // only; never affects the data.
        public int   LastMerge        = 1;
        public float LastMergeTickPx  = -1f;

        // Per-horizontal-state cache of the session window WIDTH in pixels (derived
        // from bar-count × bar distance, so it never changes with the price axis).
        // xStart is computed fresh each call (cheap) so the window follows scroll.
        public float  CachedWidthPx   = 0f;
        public int    WinCacheFrom    = int.MinValue;
        public int    WinCacheTo      = int.MinValue;
        public float  WinCacheSlot    = -1f;
        public bool   WinCacheValid   = false;

        // Persistent (cross-frame) width cache keyed by the horizontal chart state
        // (first/last visible bar + bar distance). The profile width depends ONLY on
        // horizontal geometry, so as long as the horizontal state is unchanged we
        // return the exact same width — even across the many repaints NinjaTrader
        // fires while the Y axis is being dragged. This is what fully kills the
        // "width changes when I move the price scale" flicker.
        public int    HWidthKeyFrom = int.MinValue;
        public int    HWidthKeyTo   = int.MinValue;
        public float  HWidthKeySlot = -1f;
        public int    HWidthKeyCols = -1;
        public float  HWidthValue   = -1f;
    }

    [Gui.CategoryOrder("Periodo del Perfil",   1)]
    [Gui.CategoryOrder("Order Flow",           2)]
    [Gui.CategoryOrder("Apariencia TPO",       3)]
    [Gui.CategoryOrder("Etiquetas",            4)]
    [Gui.CategoryOrder("Value Area",           5)]
    [Gui.CategoryOrder("Initial Balance",      6)]
    [Gui.CategoryOrder("Opening Range",        7)]
    [Gui.CategoryOrder("Overnight",            8)]
    [Gui.CategoryOrder("POC y Naked POC",      9)]
    [Gui.CategoryOrder("Perfil Compuesto",    10)]
    [Gui.CategoryOrder("Perfil de Volumen",   11)]
    [Gui.CategoryOrder("Perfil Delta",        12)]
    [Gui.CategoryOrder("Poor High Low",       13)]
    [Gui.CategoryOrder("Single Prints",       14)]
    [Gui.CategoryOrder("Alertas de Precio",   15)]
    [Gui.CategoryOrder("Panel Stats",         16)]
    [Gui.CategoryOrder("Avanzado",            17)]
    [Gui.CategoryOrder("Optimizacion Visual", 18)]
    public class ILT_DEV_PROFILES_FINAL : Indicator
    {
        // ── runtime state ────────────────────────────────────────────────────
        private List<TpoProfileData> _profiles        = new List<TpoProfileData>();
        private List<TpoProfileData> _overnight       = new List<TpoProfileData>();
        private TpoProfileData       _currentProfile;
        private TpoProfileData       _currentON;
        private int                  _lastProcessedBar = -1;  // avoid redundant TPO processing on each 1-min bar

        // Guards the profile collections (_profiles / _overnight / _currentProfile /
        // _currentON) against concurrent access. The data thread mutates them in
        // OnBarUpdate (session rollover) while the UI thread can rebuild them when a
        // period button (Sesión/Diario/Semanal) is clicked, and the render thread
        // iterates them in OnRender. We take this lock only around STRUCTURAL changes
        // (create/finalize/clear) and around the rebuild + render iteration — never
        // on the per-tick volume hot path — so there is no measurable contention.
        private readonly object      _profilesLock = new object();

        // Toggle button rectangles for hit-testing on mouse click
        private RectangleF           _btnTpoRect    = RectangleF.Empty;
        private RectangleF           _btnVolRect    = RectangleF.Empty;
        private RectangleF           _btnDeltaRect  = RectangleF.Empty;
        private RectangleF           _btnIBRect     = RectangleF.Empty;
        private RectangleF           _btnORRect     = RectangleF.Empty;
        private RectangleF           _btnMinRect    = RectangleF.Empty;  // minimize/expand button
        private RectangleF           _btnModeRect   = RectangleF.Empty;  // SES/DIA/SEM mode button
        private TpoSessionPeriod     _sessionModeRt = TpoSessionPeriod.Sesiones;
        private bool                 _showTpo       = true;
        private bool                 _showVolBtn    = true;
        private bool                 _showDeltaBtn  = true;
        private bool                 _showIBBtn     = true;
        private bool                 _showORBtn     = true;
        // Granular toggles: extend VA/POC independently for each session/type.
        private bool                 _extTpoRthBtn    = true;  // extend TPO RTH VA/POC
        private bool                 _extTpoEthBtn    = false; // extend TPO ETH VA/POC
        private bool                 _extVpRthBtn     = true;  // extend VP  RTH VA/VPOC
        private bool                 _extVpEthBtn     = false; // extend VP  ETH VA/VPOC
        private bool                 _statsPanelMinimized = false;  // collapse state
        // ── WPF floating panel (Fase 2) ──────────────────────────────────────
        // The stats panel + toggle buttons are now a real WPF UserControl added
        // to the chart's UserControlCollection, instead of SharpDX hit-testing.
        private System.Windows.Controls.Grid          _wpfPanel;        // root draggable container
        private System.Windows.Controls.StackPanel    _wpfBody;         // stats lines + buttons
        private System.Windows.Controls.TextBlock      _wpfStatsText;    // the stats text block
        private System.Windows.Controls.TextBlock      _wpfHeaderText;   // title in header
        private System.Windows.Controls.Button         _wpfMinBtn;       // minimize/expand
        private System.Windows.Controls.Button         _btnTpoW, _btnVolW, _btnDeltaW, _btnIBW, _btnORW;
        private System.Windows.Controls.Button         _btnExtTpoRthW, _btnExtTpoEthW;
        private System.Windows.Controls.Button         _btnExtVpRthW,  _btnExtVpEthW;
        private System.Windows.Controls.Button         _btnPerSesW, _btnPerDiaW, _btnPerSemW; // periodo: Sesión/Diario/Semanal (radio)
        private bool   _wpfBuilt        = false;   // panel constructed & added
        private bool   _wpfDragging     = false;
        private System.Windows.Point _wpfDragStart;
        private double _wpfStartLeft, _wpfStartTop;
        private bool   _wpfPosInitialized = false; // false until we place it (centered) once
        private bool   _userMovedPanel    = false; // becomes true once dragged
        // User intent flag for the toolbar Show/Hide button. Independent of
        // ShowStatsPanel (a configuration property), so toggling the toolbar
        // doesn't fight the render loop, which only re-evaluates ShowStatsPanel.
        private bool   _panelUserHidden   = true;  // hidden on load

        // ── Top toolbar button (Disco-Trading style) ─────────────────────────
        // A button injected into the chart window's main toolbar that toggles
        // the floating WPF panel's visibility. Hosted by the chart Window, NOT
        // the chart panel — so it sits on the title bar and never invades chart
        // real estate. Built once on first ChartControl.Loaded; removed on
        // Terminated. We track the host Window so we can clean up reliably.
        private NinjaTrader.Gui.Tools.NTMenuItem _toolbarBtn;
        private NinjaTrader.Gui.Chart.Chart     _chartWindow;
        private bool                            _toolbarBtnAdded = false;
        // Alert state — track which levels have already fired to avoid repeat alerts
        private double               _lastAlertPOC    = 0;
        private double               _lastAlertVAH    = 0;
        private double               _lastAlertVAL    = 0;
        private double               _lastAlertVPOC   = 0;
        private bool                 _alertNakedFired = false;

        // DirectX
        private SharpDX.Direct2D1.SolidColorBrush _dxPoc;
        private SharpDX.Direct2D1.SolidColorBrush _dxVah;
        private SharpDX.Direct2D1.SolidColorBrush _dxVal;
        private SharpDX.Direct2D1.SolidColorBrush _dxOnVah;   // ON VAH color
        private SharpDX.Direct2D1.SolidColorBrush _dxOnVal;   // ON VAL color
        private SharpDX.Direct2D1.SolidColorBrush _dxVaFill;  // VA area fill
        private SharpDX.Direct2D1.SolidColorBrush _dxIBFill;
        private SharpDX.Direct2D1.SolidColorBrush _dxIBBorder;
        private SharpDX.Direct2D1.SolidColorBrush _dxTpo;
        private SharpDX.Direct2D1.SolidColorBrush _dxTpoIB;
        private SharpDX.Direct2D1.SolidColorBrush _dxTpoVA;    // TPOs inside Value Area
        private SharpDX.Direct2D1.SolidColorBrush _dxCellHi;   // modern cell bevel highlight (top/left)
        private SharpDX.Direct2D1.SolidColorBrush _dxCellLo;   // modern cell bevel shadow (bottom/right)
        private SharpDX.Direct2D1.SolidColorBrush _dxNakedPoc;
        private SharpDX.Direct2D1.SolidColorBrush _dxBg;
        private SharpDX.Direct2D1.SolidColorBrush _dxOnBg;
        private SharpDX.Direct2D1.SolidColorBrush _dxOnTpo;
        private SharpDX.Direct2D1.SolidColorBrush _dxComposite;
        private SharpDX.Direct2D1.SolidColorBrush _dxCompositeVA;  // composite bars inside VA
        private SharpDX.Direct2D1.SolidColorBrush _dxOr;
        private SharpDX.Direct2D1.SolidColorBrush _dxOrFill;
        private SharpDX.Direct2D1.SolidColorBrush _dxVolBar;       // volume profile bar
        private SharpDX.Direct2D1.SolidColorBrush _dxVolVA;        // volume bar inside VA
        private SharpDX.Direct2D1.SolidColorBrush _dxVolPoc;       // volume POC bar
        private SharpDX.Direct2D1.SolidColorBrush _dxOnVaFill;     // ON VA fill highlight
        private SharpDX.Direct2D1.SolidColorBrush _dxStatBg;
        private SharpDX.Direct2D1.SolidColorBrush _dxStatText;
        private SharpDX.Direct2D1.SolidColorBrush _dxPoorHL;      // Poor High/Low marker
        private SharpDX.Direct2D1.SolidColorBrush _dxSinglePrint; // Single Print area
        private SharpDX.Direct2D1.SolidColorBrush _dxDeltaBuy;    // Delta profile positive (buy)
        private SharpDX.Direct2D1.SolidColorBrush _dxDeltaSell;   // Delta profile negative (sell)
        private SharpDX.Direct2D1.SolidColorBrush _dxDeltaZero;   // Delta zero line
        private SharpDX.Direct2D1.SolidColorBrush _dxBtnOn;       // Button active
        private SharpDX.Direct2D1.SolidColorBrush _dxBtnOff;      // Button inactive
        private SharpDX.Direct2D1.SolidColorBrush _dxBtnBorder;   // Button border

        private TextFormat _fmtLetter;
        private TextFormat _fmtCellLetter; // centered, slightly larger glyph for modern cell mode
        private TextFormat _fmtStat;
        private TextFormat _fmtLabel;
        private TextFormat _fmtLabelCtr;   // centered variant for POC/VA price labels

        private float _slotW;
        // Composite merge hysteresis (composite has no per-profile state object).
        private int   _compLastMerge       = 1;
        private float _compLastMergeTickPx = -1f;

        // Suite-ready render guards. Updated once at the start of OnRender and
        // consumed by labels / detail layers to avoid repeated ChartPanel math and
        // off-screen text work when several ILT-DEV indicators share the same chart.
        private float  _renderPanelTop = 0f;
        private float  _renderPanelBot = 0f;
        private double _renderPriceMin = double.MinValue;
        private double _renderPriceMax = double.MaxValue;
        private int    _lastWpfStatsUpdateMs = -100000000;
        private int    _lastCompositeRecalcMs = -100000000;

        // Cached render objects (created once, reused every frame) to avoid
        // per-call COM allocations in the hot OnRender path.
        private SharpDX.Direct2D1.StrokeStyle        _dashStyle;     // dashed VA/POC lines
        private SharpDX.Direct2D1.StrokeStyle        _dotStyle;      // dotted
        private SharpDX.Direct2D1.StrokeStyle        _dashDotStyle;  // dash-dot
        private SharpDX.Direct2D1.SolidColorBrush    _dxPanelBorder; // stats panel border

        private static readonly string Alphabet =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        // Render-hot path cache: avoids allocating a new one-character string
        // for every TPO letter drawn on every OnRender pass. This changes only
        // the drawing path; the TPO calculation engine remains untouched.
        private static readonly string[] _charTextCache = BuildCharTextCache();

        private static string[] BuildCharTextCache()
        {
            string[] arr = new string[128];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = ((char)i).ToString();
            return arr;
        }

        private static string FastCharText(char c)
        {
            int idx = (int)c;
            return idx >= 0 && idx < _charTextCache.Length ? _charTextCache[idx] : c.ToString();
        }

        // =====================================================================
        //  PROPERTIES
        // =====================================================================

        #region Periodo del Perfil

        [NinjaScriptProperty]
        [Display(Name = "Modo de periodo (Sesiones/Diario/Semanal)", GroupName = "Periodo del Perfil", Order = 0,
            Description = "Sesiones: ETH/RTH separadas. Diario: un perfil por dia completo (24h). Semanal: un perfil por semana completa. El boton del panel tambien alterna el modo en vivo.")]
        public TpoSessionPeriod SessionPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Perfiles historicos", GroupName = "Periodo del Perfil", Order = 2)]
        public int ProfilesBack { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "RTH inicio (HHmm)", GroupName = "Periodo del Perfil", Order = 3)]
        public int SessionStartHHmm { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "RTH fin (HHmm)", GroupName = "Periodo del Perfil", Order = 4)]
        public int SessionEndHHmm { get; set; }
        #endregion

        #region Apariencia TPO
        [NinjaScriptProperty]
        [Display(Name = "Orientacion perfil", GroupName = "Apariencia TPO", Order = 0)]
        public TpoOrientation ProfileOrientation { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Modo render: Letras o Bloques", GroupName = "Apariencia TPO", Order = 1)]
        public TpoRenderMode RenderMode { get; set; }

        [NinjaScriptProperty]
        [Range(1, 240)]
        [Display(Name = "Minutos por letra TPO", GroupName = "Apariencia TPO", Order = 2)]
        public int TPOMinutes { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Modo color letras", GroupName = "Apariencia TPO", Order = 3)]
        public TpoLetterColoring LetterColorMode { get; set; }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color TPO principal", GroupName = "Apariencia TPO", Order = 4)]
        public System.Windows.Media.Brush TpoColor { get; set; }
        [Browsable(false)]
        public string TpoColorSerializable
        { get { return Serialize.BrushToString(TpoColor); } set { TpoColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color TPO en IB", GroupName = "Apariencia TPO", Order = 5)]
        public System.Windows.Media.Brush TpoIBColor { get; set; }
        [Browsable(false)]
        public string TpoIBColorSerializable
        { get { return Serialize.BrushToString(TpoIBColor); } set { TpoIBColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "Tamano fuente letras", GroupName = "Apariencia TPO", Order = 6)]
        public float LetterFontSize { get; set; }

        [NinjaScriptProperty]
        [Range(0.5, 3.0)]
        [Display(Name = "Escala letra en bloque", GroupName = "Apariencia TPO", Order = 7,
            Description = "Multiplica el tamano de la letra DENTRO de cada bloque/celda en el modo Blocks. 1.0 = igual al tamano base; sube para letras mas grandes dentro de la celda.")]
        public double CellLetterScale { get; set; }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Fondo RTH", GroupName = "Apariencia TPO", Order = 7)]
        public System.Windows.Media.Brush ProfileBackground { get; set; }
        [Browsable(false)]
        public string ProfileBackgroundSerializable
        { get { return Serialize.BrushToString(ProfileBackground); } set { ProfileBackground = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "Opacidad fondo RTH (0-100)", GroupName = "Apariencia TPO", Order = 8)]
        public int BackgroundOpacity { get; set; }
        #endregion

        #region Etiquetas
        [NinjaScriptProperty]
        [Display(Name = "Etiquetas compactas (sin precio)", GroupName = "Etiquetas", Order = 0,
            Description = "Muestra solo el nombre (VAH, POC...) sin el precio. Reduce mucho el ancho horizontal y evita solapamiento entre sesiones.")]
        public bool CompactLabels { get; set; }

        [NinjaScriptProperty]
        [Range(0, 50)]
        [Display(Name = "Etiquetas: ultimas N sesiones (0=todas)", GroupName = "Etiquetas", Order = 1,
            Description = "Solo las ultimas N sesiones muestran etiquetas de texto. Las sesiones mas antiguas dibujan el perfil sin etiquetas para evitar el ruido visual. 0 = todas.")]
        public int LabelSessionsBack { get; set; }
        #endregion

        #region Value Area
        [NinjaScriptProperty]
        [Range(50, 95)]
        [Display(Name = "Porcentaje Value Area", GroupName = "Value Area", Order = 0)]
        public double ValueAreaPercent { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mostrar VAH y VAL", GroupName = "Value Area", Order = 1)]
        public bool ShowValueArea { get; set; }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color VAH (RTH)", GroupName = "Value Area", Order = 2)]
        public System.Windows.Media.Brush VahColor { get; set; }
        [Browsable(false)]
        public string VahColorSerializable
        { get { return Serialize.BrushToString(VahColor); } set { VahColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color VAL (RTH)", GroupName = "Value Area", Order = 3)]
        public System.Windows.Media.Brush ValColor { get; set; }
        [Browsable(false)]
        public string ValColorSerializable
        { get { return Serialize.BrushToString(ValColor); } set { ValColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color VAH (Overnight)", GroupName = "Value Area", Order = 4)]
        public System.Windows.Media.Brush ONVahColor { get; set; }
        [Browsable(false)]
        public string ONVahColorSerializable
        { get { return Serialize.BrushToString(ONVahColor); } set { ONVahColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color VAL (Overnight)", GroupName = "Value Area", Order = 5)]
        public System.Windows.Media.Brush ONValColor { get; set; }
        [Browsable(false)]
        public string ONValColorSerializable
        { get { return Serialize.BrushToString(ONValColor); } set { ONValColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color relleno VA", GroupName = "Value Area", Order = 6)]
        public System.Windows.Media.Brush VAFillColor { get; set; }
        [Browsable(false)]
        public string VAFillColorSerializable
        { get { return Serialize.BrushToString(VAFillColor); } set { VAFillColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "Opacidad relleno VA (0-100)", GroupName = "Value Area", Order = 7)]
        public int VAFillOpacity { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Grosor linea VA (px)", GroupName = "Value Area", Order = 8)]
        public float VaLineWidth { get; set; }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color TPO dentro del VA", GroupName = "Value Area", Order = 9)]
        public System.Windows.Media.Brush TpoVAColor { get; set; }
        [Browsable(false)]
        public string TpoVAColorSerializable
        { get { return Serialize.BrushToString(TpoVAColor); } set { TpoVAColor = Serialize.StringToBrush(value); } }
        #endregion

        #region Initial Balance
        [NinjaScriptProperty]
        [Display(Name = "Mostrar IB", GroupName = "Initial Balance", Order = 0)]
        public bool ShowIB { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Minutos IB (ej: 60 = 9:30-10:30)", GroupName = "Initial Balance", Order = 1)]
        public int IBMinutes { get; set; }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color relleno IB", GroupName = "Initial Balance", Order = 2)]
        public System.Windows.Media.Brush IBColor { get; set; }
        [Browsable(false)]
        public string IBColorSerializable
        { get { return Serialize.BrushToString(IBColor); } set { IBColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "Opacidad IB (0-100)", GroupName = "Initial Balance", Order = 3)]
        public int IBOpacity { get; set; }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color borde IB", GroupName = "Initial Balance", Order = 4)]
        public System.Windows.Media.Brush IBBorderColor { get; set; }
        [Browsable(false)]
        public string IBBorderColorSerializable
        { get { return Serialize.BrushToString(IBBorderColor); } set { IBBorderColor = Serialize.StringToBrush(value); } }
        #endregion

        #region Opening Range
        [NinjaScriptProperty]
        [Display(Name = "Mostrar Opening Range", GroupName = "Opening Range", Order = 0)]
        public bool ShowOR { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Minutos Opening Range", GroupName = "Opening Range", Order = 1)]
        public int ORMinutes { get; set; }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color borde OR", GroupName = "Opening Range", Order = 2)]
        public System.Windows.Media.Brush ORColor { get; set; }
        [Browsable(false)]
        public string ORColorSerializable
        { get { return Serialize.BrushToString(ORColor); } set { ORColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color relleno OR", GroupName = "Opening Range", Order = 3)]
        public System.Windows.Media.Brush ORFillColor { get; set; }
        [Browsable(false)]
        public string ORFillColorSerializable
        { get { return Serialize.BrushToString(ORFillColor); } set { ORFillColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "Opacidad relleno OR (0-100)", GroupName = "Opening Range", Order = 4)]
        public int ORFillOpacity { get; set; }
        #endregion

        #region Overnight
        [NinjaScriptProperty]
        [Display(Name = "Mostrar perfil Overnight", GroupName = "Overnight", Order = 0)]
        public bool ShowOvernight { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ON inicio (HHmm)", GroupName = "Overnight", Order = 1)]
        public int ONStartHHmm { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ON fin (HHmm)", GroupName = "Overnight", Order = 2)]
        public int ONEndHHmm { get; set; }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color TPO Overnight", GroupName = "Overnight", Order = 3)]
        public System.Windows.Media.Brush ONTpoColor { get; set; }
        [Browsable(false)]
        public string ONTpoColorSerializable
        { get { return Serialize.BrushToString(ONTpoColor); } set { ONTpoColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Fondo Overnight", GroupName = "Overnight", Order = 4)]
        public System.Windows.Media.Brush ONBackground { get; set; }
        [Browsable(false)]
        public string ONBackgroundSerializable
        { get { return Serialize.BrushToString(ONBackground); } set { ONBackground = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "Opacidad fondo ON (0-100)", GroupName = "Overnight", Order = 5)]
        public int ONOpacity { get; set; }
        #endregion

        #region POC y Naked POC
        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color POC", GroupName = "POC y Naked POC", Order = 0)]
        public System.Windows.Media.Brush PocColor { get; set; }
        [Browsable(false)]
        public string PocColorSerializable
        { get { return Serialize.BrushToString(PocColor); } set { PocColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "Grosor linea POC (px)", GroupName = "POC y Naked POC", Order = 1)]
        public float PocLineWidth { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mostrar Naked POC", GroupName = "POC y Naked POC", Order = 2)]
        public bool ShowNakedPOC { get; set; }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color Naked POC", GroupName = "POC y Naked POC", Order = 3)]
        public System.Windows.Media.Brush NakedPocColor { get; set; }
        [Browsable(false)]
        public string NakedPocColorSerializable
        { get { return Serialize.BrushToString(NakedPocColor); } set { NakedPocColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "Extension Naked POC (barras)", GroupName = "POC y Naked POC", Order = 4)]
        public int NakedPocExtend { get; set; }
        #endregion

        #region Perfil Compuesto
        [NinjaScriptProperty]
        [Display(Name = "Mostrar Perfil Compuesto", GroupName = "Perfil Compuesto", Order = 0)]
        public bool ShowComposite { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Modo compuesto", GroupName = "Perfil Compuesto", Order = 1)]
        public TpoCompositeMode CompositeMode { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "N sesiones (modo Sessions)", GroupName = "Perfil Compuesto", Order = 2)]
        public int CompositePeriods { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "N barras (modo Bars)", GroupName = "Perfil Compuesto", Order = 3)]
        public int CompositeBars { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Minutos rollover (modo Rollover)", GroupName = "Perfil Compuesto", Order = 4)]
        public int CompositeRolloverMinutes { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Barra inicio rango fijo (modo FixedRange)", GroupName = "Perfil Compuesto", Order = 5)]
        public int CompositeFixedStartBarsAgo { get; set; }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color compuesto", GroupName = "Perfil Compuesto", Order = 6)]
        public System.Windows.Media.Brush CompositeColor { get; set; }
        [Browsable(false)]
        public string CompositeColorSerializable
        { get { return Serialize.BrushToString(CompositeColor); } set { CompositeColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "Opacidad compuesto (0-100)", GroupName = "Perfil Compuesto", Order = 7)]
        public int CompositeOpacity { get; set; }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color VA compuesto", GroupName = "Perfil Compuesto", Order = 8)]
        public System.Windows.Media.Brush CompositeVAColor { get; set; }
        [Browsable(false)]
        public string CompositeVAColorSerializable
        { get { return Serialize.BrushToString(CompositeVAColor); } set { CompositeVAColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "Ancho max compuesto (px)", GroupName = "Perfil Compuesto", Order = 9)]
        public int CompositeMaxWidth { get; set; }
        #endregion

        #region Perfil de Volumen
        [NinjaScriptProperty]
        [Display(Name = "Mostrar Perfil de Volumen", GroupName = "Perfil de Volumen", Order = 0)]
        public bool ShowVolumeProfile { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Estilo Volumen", GroupName = "Perfil de Volumen", Order = 1,
            Description = "Bars = barras horizontales clasicas. Outline = contorno/linea suave con relleno tenue (look moderno tipo perfil pro).")]
        public TpoVolumeStyle VolumeStyle { get; set; }

        [NinjaScriptProperty]
        [Range(10, 600)]
        [Display(Name = "Ancho maximo Volumen (px)", GroupName = "Perfil de Volumen", Order = 1,
            Description = "Ancho del Volume Profile en pixeles. Es independiente del ancho del Perfil Delta, aunque compartan el mismo eje en modo superpuesto.")]
        public int VolProfileWidth { get; set; }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color barras volumen", GroupName = "Perfil de Volumen", Order = 2)]
        public System.Windows.Media.Brush VolBarColor { get; set; }
        [Browsable(false)]
        public string VolBarColorSerializable
        { get { return Serialize.BrushToString(VolBarColor); } set { VolBarColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "Opacidad barras (0-100)", GroupName = "Perfil de Volumen", Order = 3)]
        public int VolBarOpacity { get; set; }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color barras dentro del VA", GroupName = "Perfil de Volumen", Order = 4)]
        public System.Windows.Media.Brush VolVAColor { get; set; }
        [Browsable(false)]
        public string VolVAColorSerializable
        { get { return Serialize.BrushToString(VolVAColor); } set { VolVAColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color POC volumen", GroupName = "Perfil de Volumen", Order = 5)]
        public System.Windows.Media.Brush VolPocColor { get; set; }
        [Browsable(false)]
        public string VolPocColorSerializable
        { get { return Serialize.BrushToString(VolPocColor); } set { VolPocColor = Serialize.StringToBrush(value); } }
        #endregion

        #region Order Flow
        [NinjaScriptProperty]
        [Display(Name = "Modo clasificacion Delta", GroupName = "Order Flow", Order = 0,
            Description = "BidAsk = bid/ask real (requiere Tick Replay, grado institucional). UpDownTick = aproximado por direccion de precio. Auto = usa BidAsk cuando hay cotizacion valida, si no UpDownTick.")]
        public TpoDeltaMode DeltaMode { get; set; }
        #endregion

        #region Perfil Delta
        [NinjaScriptProperty]
        [Display(Name = "Mostrar Perfil Delta", GroupName = "Perfil Delta", Order = 0)]
        public bool ShowDeltaProfile { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Superponer sobre Volumen (mismo eje)", GroupName = "Perfil Delta", Order = 1,
            Description = "Si esta activo, el Delta se centra sobre el MISMO EJE del Volume Profile (volumen como capa base, delta encima) pero conserva su PROPIO ancho ('Ancho maximo Delta'), de modo que puedes hacerlo mas angosto que el volumen para distinguir las barras. Si esta desactivado, el delta va en su propia columna aparte.")]
        public bool DeltaOverlayOnVolume { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Alineacion barras", GroupName = "Perfil Delta", Order = 2)]
        public TpoDeltaAlignment DeltaAlignment { get; set; }

        [NinjaScriptProperty]
        [Range(10, 600)]
        [Display(Name = "Ancho maximo Delta (px)", GroupName = "Perfil Delta", Order = 3,
            Description = "Ancho del Perfil Delta en pixeles, independiente del ancho del Volume Profile. En modo superpuesto, si lo haces mas angosto que el volumen las barras de delta se distinguen claramente dentro del volumen.")]
        public int DeltaProfileWidth { get; set; }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color barras Buy (delta+)", GroupName = "Perfil Delta", Order = 3)]
        public System.Windows.Media.Brush DeltaBuyColor { get; set; }
        [Browsable(false)]
        public string DeltaBuyColorSerializable
        { get { return Serialize.BrushToString(DeltaBuyColor); } set { DeltaBuyColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color barras Sell (delta-)", GroupName = "Perfil Delta", Order = 4)]
        public System.Windows.Media.Brush DeltaSellColor { get; set; }
        [Browsable(false)]
        public string DeltaSellColorSerializable
        { get { return Serialize.BrushToString(DeltaSellColor); } set { DeltaSellColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "Opacidad barras (0-100)", GroupName = "Perfil Delta", Order = 5)]
        public int DeltaOpacity { get; set; }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color linea cero", GroupName = "Perfil Delta", Order = 6)]
        public System.Windows.Media.Brush DeltaZeroColor { get; set; }
        [Browsable(false)]
        public string DeltaZeroColorSerializable
        { get { return Serialize.BrushToString(DeltaZeroColor); } set { DeltaZeroColor = Serialize.StringToBrush(value); } }
        #endregion

        #region Poor High Low
        [NinjaScriptProperty]
        [Display(Name = "Mostrar Poor High/Low", GroupName = "Poor High Low", Order = 0)]
        public bool ShowPoorHL { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TPOs maximos para Poor (1-3)", GroupName = "Poor High Low", Order = 1)]
        public int PoorHLMaxTPOs { get; set; }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color Poor H/L", GroupName = "Poor High Low", Order = 2)]
        public System.Windows.Media.Brush PoorHLColor { get; set; }
        [Browsable(false)]
        public string PoorHLColorSerializable
        { get { return Serialize.BrushToString(PoorHLColor); } set { PoorHLColor = Serialize.StringToBrush(value); } }
        #endregion

        #region Single Prints
        [NinjaScriptProperty]
        [Display(Name = "Mostrar Single Prints", GroupName = "Single Prints", Order = 0)]
        public bool ShowSinglePrints { get; set; }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color Single Print", GroupName = "Single Prints", Order = 1)]
        public System.Windows.Media.Brush SinglePrintColor { get; set; }
        [Browsable(false)]
        public string SinglePrintColorSerializable
        { get { return Serialize.BrushToString(SinglePrintColor); } set { SinglePrintColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "Opacidad Single Print (0-100)", GroupName = "Single Prints", Order = 2)]
        public int SinglePrintOpacity { get; set; }
        #endregion

        #region Alertas de Precio
        [NinjaScriptProperty]
        [Display(Name = "Alerta al tocar POC", GroupName = "Alertas de Precio", Order = 0)]
        public bool AlertPOC { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Alerta al tocar VAH", GroupName = "Alertas de Precio", Order = 1)]
        public bool AlertVAH { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Alerta al tocar VAL", GroupName = "Alertas de Precio", Order = 2)]
        public bool AlertVAL { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Alerta al tocar Naked POC", GroupName = "Alertas de Precio", Order = 3)]
        public bool AlertNakedPOC { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Alerta al tocar VPOC", GroupName = "Alertas de Precio", Order = 4)]
        public bool AlertVPOC { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Sonido alerta", GroupName = "Alertas de Precio", Order = 5)]
        public string AlertSoundFile { get; set; }
        #endregion

        #region Panel Stats
        [NinjaScriptProperty]
        [Display(Name = "Mostrar panel stats", GroupName = "Panel Stats", Order = 0)]
        public bool ShowStatsPanel { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Posicion panel", GroupName = "Panel Stats", Order = 1)]
        public TextPosition StatsPanelPosition { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Tamano fuente stats", GroupName = "Panel Stats", Order = 2)]
        public float StatsFontSize { get; set; }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color texto stats", GroupName = "Panel Stats", Order = 3)]
        public System.Windows.Media.Brush StatsTextColor { get; set; }
        [Browsable(false)]
        public string StatsTextColorSerializable
        { get { return Serialize.BrushToString(StatsTextColor); } set { StatsTextColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty][XmlIgnore]
        [Display(Name = "Color fondo panel", GroupName = "Panel Stats", Order = 4)]
        public System.Windows.Media.Brush StatsBgColor { get; set; }
        [Browsable(false)]
        public string StatsBgColorSerializable
        { get { return Serialize.BrushToString(StatsBgColor); } set { StatsBgColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "Opacidad fondo (0-100)", GroupName = "Panel Stats", Order = 5)]
        public int StatsBgOpacity { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mostrar Day Type", GroupName = "Panel Stats", Order = 6)]
        public bool ShowDayType { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mostrar Fecha", GroupName = "Panel Stats", Order = 7)]
        public bool StatsShowDate { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mostrar POC/VAH/VAL", GroupName = "Panel Stats", Order = 8)]
        public bool StatsShowPOC { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mostrar IB", GroupName = "Panel Stats", Order = 9)]
        public bool StatsShowIB { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mostrar OR", GroupName = "Panel Stats", Order = 10)]
        public bool StatsShowOR { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mostrar rango Profile", GroupName = "Panel Stats", Order = 11)]
        public bool StatsShowProfileRange { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mostrar VPOC y Delta", GroupName = "Panel Stats", Order = 12)]
        public bool StatsShowVolStats { get; set; }

        // Panel use-WPF toggle. When true (default) the stats panel is a real
        // floating WPF UserControl (draggable, minimizable, remembers position).
        [NinjaScriptProperty]
        [Display(Name = "Panel WPF flotante", GroupName = "Panel Stats", Order = 13)]
        public bool UseWpfPanel { get; set; }

        // Injects a button into the chart window's main toolbar (Disco-Trading
        // style) that opens/closes the floating WPF panel. Keeps the chart area
        // unobstructed when the panel is closed.
        [NinjaScriptProperty]
        [Display(Name = "Boton en toolbar superior", GroupName = "Panel Stats", Order = 14)]
        public bool ShowToolbarButton { get; set; }

        // Persisted panel position (device-independent px, relative to chart panel).
        // Browsable(false) keeps them out of the settings grid but they STILL
        // serialize with the workspace, so the panel reopens where you left it.
        // Negative sentinel = "not placed yet" → centered horizontally on first show.
        [Browsable(false)]
        [Display(Name = "PanelX", GroupName = "Panel Stats", Order = 14)]
        public double PanelX { get; set; }

        [Browsable(false)]
        [Display(Name = "PanelY", GroupName = "Panel Stats", Order = 15)]
        public double PanelY { get; set; }

        [Browsable(false)]
        [Display(Name = "PanelMinimized", GroupName = "Panel Stats", Order = 16)]
        public bool PanelMinimizedPersist { get; set; }
        #endregion

        #region Avanzado
        [NinjaScriptProperty]
        [Display(Name = "Tamano tick (0=auto)", GroupName = "Avanzado", Order = 0)]
        public double ManualTickSize { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Separacion TPO/Volumen/Delta (px)", GroupName = "Avanzado", Order = 1)]
        public int ProfileGap { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Debug log (Output tab)", GroupName = "Avanzado", Order = 2)]
        public bool DebugLog { get; set; }

        [NinjaScriptProperty]
        [ReadOnly(true)]
        [Display(Name = "Version instalada", GroupName = "Optimizacion Visual", Order = -1,
            Description = "Marcador visual para confirmar que NinjaTrader cargo la version optimizada correcta.")]
        public string BuildVersionStamp { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Optimizar carga historica", GroupName = "Optimizacion Visual", Order = 0,
            Description = "Cuando esta activo, evita recalcular POC/VA/VPOC en cada tick historico. Los perfiles se finalizan por sesion y se actualizan en render/realtime sin cambiar el motor de datos.")]
        public bool OptimizeHistoricalLoad { get; set; }

        [NinjaScriptProperty]
        [Range(0, 2000)]
        [Display(Name = "Recalculo realtime (ms)", GroupName = "Optimizacion Visual", Order = 1,
            Description = "Throttle profesional para POC/VA/VPOC en realtime. 0 = recalcular cada render; 250-500 reduce chart-lag sin perder la captura tick a tick.")]
        public int RealtimeRecalcMs { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Cull niveles fuera de pantalla", GroupName = "Optimizacion Visual", Order = 2,
            Description = "No dibuja filas de precio que estan fuera del eje visible. No cambia los datos calculados; solo reduce trabajo de renderizado.")]
        public bool CullOffscreenPriceLevels { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Modo visual sesion actual", GroupName = "Optimizacion Visual", Order = 3,
            Description = "Auto mantiene detalle legible segun zoom; Letters/Blocks/Silhouette/Minimal solo afectan dibujo, no datos.")]
        public TpoProfileVisualMode CurrentSessionVisualMode { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Modo visual historico", GroupName = "Optimizacion Visual", Order = 4,
            Description = "Recomendado Silhouette para 10+ dias. Reduce texto/objetos historicos sin cambiar TPO, volumen ni delta calculado.")]
        public TpoProfileVisualMode HistoricalVisualMode { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Auto LOD por zoom", GroupName = "Optimizacion Visual", Order = 5,
            Description = "Activa degradacion visual inteligente: letras cerca, bloques/silueta al alejarse, minimal cuando no hay lectura util.")]
        public bool AutoLevelOfDetail { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Congelar perfiles cerrados", GroupName = "Optimizacion Visual", Order = 6,
            Description = "Finaliza perfiles cerrados y evita recalculos visuales innecesarios. No altera captura ni calculos del perfil actual.")]
        public bool FreezeClosedProfiles { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Detalles solo sesion actual", GroupName = "Optimizacion Visual", Order = 7,
            Description = "Poor High/Low y Single Prints se dibujan solo en la sesion activa para reducir carga historica.")]
        public bool DetailMarkersOnlyCurrent { get; set; }
        #endregion

        #region Zoom y Extension
        // Scale the whole profile width with chart zoom so sessions keep their
        // proportion as you zoom in/out (instead of a fixed letter-based width).
        [NinjaScriptProperty]
        [Display(Name = "Escalar ancho con zoom", GroupName = "Zoom y Extension", Order = 0)]
        public bool ScaleWidthWithZoom { get; set; }

        // Fraction of each session's on-chart bar span the profile should occupy
        // when zoom scaling is on (e.g. 60 = profile fills 60% of the session width).
        [NinjaScriptProperty]
        [Range(10, 100)]
        [Display(Name = "Ancho perfil (% del span sesion)", GroupName = "Zoom y Extension", Order = 1)]
        public int ProfileWidthPct { get; set; }

        // Extend TPO VA & POC lines to the right edge of the chart.
        [NinjaScriptProperty]
        [Display(Name = "Extender VA y POC (TPO)", GroupName = "Zoom y Extension", Order = 2)]
        public bool ExtendVaPoc { get; set; }

        // Extend Volume VA & VPOC to the right edge — only for the current and
        // previous RTH session (yesterday + today in formation).
        [NinjaScriptProperty]
        [Display(Name = "Extender VP VA/VPOC (ayer+hoy)", GroupName = "Zoom y Extension", Order = 3)]
        public bool ExtendVpVaPoc { get; set; }
        #endregion

        #region Estilo Lineas
        [NinjaScriptProperty]
        [Display(Name = "Estilo linea VA/POC", GroupName = "Estilo Lineas", Order = 0)]
        public TpoLineStyle VaLineStyle { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Estilo linea IB", GroupName = "Estilo Lineas", Order = 1)]
        public TpoLineStyle IbLineStyle { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Estilo linea OR", GroupName = "Estilo Lineas", Order = 2)]
        public TpoLineStyle OrLineStyle { get; set; }

        [NinjaScriptProperty]
        [Range(0.5, 5.0)]
        [Display(Name = "Grosor linea IB/OR", GroupName = "Estilo Lineas", Order = 3)]
        public float IbOrLineWidth { get; set; }
        #endregion

        // =====================================================================
        //  STATE CHANGE
        // =====================================================================

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description              = "ILT-DEV-PROFILES - TPO + Volume + Delta + Composite optimized visual suite for NinjaTrader 8";
                Name = "ILT-DEV-PROFILES";
                Calculate                = Calculate.OnEachTick;   // tick-precise volume
                IsOverlay                = true;
                DisplayInDataBox         = false;
                DrawOnPricePanel         = true;
                PaintPriceMarkers        = false;
                ScaleJustification       = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;

                // Professional order-flow engine:
                // With Tick Replay enabled on the data series, OnMarketData() is
                // called on HISTORICAL data, giving real Bid/Ask at each trade.
                // This enables institutional-grade delta (trade-at-ask = buy,
                // trade-at-bid = sell) instead of approximate uptick/downtick.
                // NOTE: enable "Show Tick Replay" globally in Tools > Options >
                // Market Data, then tick "Tick Replay" on the chart data series.

                // Periodo
                ProfilesBack     = 10;
                SessionStartHHmm = 930;
                SessionEndHHmm   = 1700;   // RTH closes 17:00 NY (CME pit-style cash session for ES/NQ)
                SessionPeriod    = TpoSessionPeriod.Sesiones;

                // Order Flow — Auto picks real BidAsk when available, else UpDownTick
                DeltaMode        = TpoDeltaMode.Auto;

                // Apariencia
                ProfileOrientation = TpoOrientation.LeftToRight;
                RenderMode         = TpoRenderMode.Letters;
                TPOMinutes         = 30;
                LetterColorMode    = TpoLetterColoring.IBHighlight;
                TpoColor           = System.Windows.Media.Brushes.DodgerBlue;
                TpoIBColor         = System.Windows.Media.Brushes.Gold;
                LetterFontSize     = 9f;
                CellLetterScale    = 1.25;
                ProfileBackground  = System.Windows.Media.Brushes.MidnightBlue;
                BackgroundOpacity  = 15;

                // Etiquetas - compact by default, only last 3 sessions labeled
                CompactLabels      = true;
                LabelSessionsBack  = 3;

                // VA
                ValueAreaPercent = 70.0;
                ShowValueArea    = true;
                VahColor         = System.Windows.Media.Brushes.Cyan;
                ValColor         = System.Windows.Media.Brushes.Cyan;
                ONVahColor       = System.Windows.Media.Brushes.MediumPurple;
                ONValColor       = System.Windows.Media.Brushes.MediumPurple;
                VAFillColor      = System.Windows.Media.Brushes.DodgerBlue;
                VAFillOpacity    = 10;
                VaLineWidth      = 1.5f;
                TpoVAColor       = System.Windows.Media.Brushes.DeepSkyBlue;

                // IB
                ShowIB        = true;
                IBMinutes     = 60;   // 9:30 a 10:30 = 60 minutos
                IBColor       = System.Windows.Media.Brushes.Yellow;
                IBOpacity     = 20;
                IBBorderColor = System.Windows.Media.Brushes.Yellow;

                // OR
                ShowOR       = true;
                ORMinutes    = 15;
                ORColor      = System.Windows.Media.Brushes.Magenta;
                ORFillColor  = System.Windows.Media.Brushes.Magenta;
                ORFillOpacity= 12;

                // Overnight
                ShowOvernight = true;
                ONStartHHmm   = 1800;
                ONEndHHmm     = 929;
                ONTpoColor    = System.Windows.Media.Brushes.SlateBlue;
                ONBackground  = System.Windows.Media.Brushes.Indigo;
                ONOpacity     = 20;

                // POC
                PocColor       = System.Windows.Media.Brushes.Red;
                PocLineWidth   = 2f;
                ShowNakedPOC   = true;
                NakedPocColor  = System.Windows.Media.Brushes.OrangeRed;
                NakedPocExtend = 50;

                // Compuesto
                ShowComposite              = true;
                CompositeMode              = TpoCompositeMode.LastNSessions;
                CompositePeriods           = 5;
                CompositeBars              = 200;
                CompositeRolloverMinutes   = 1440;  // 24 horas
                CompositeFixedStartBarsAgo = 500;
                CompositeColor             = System.Windows.Media.Brushes.DimGray;
                CompositeOpacity           = 60;
                CompositeVAColor           = System.Windows.Media.Brushes.DeepSkyBlue;
                CompositeMaxWidth          = 120;

                // Poor High/Low
                ShowPoorHL     = true;
                PoorHLMaxTPOs  = 1;
                PoorHLColor    = System.Windows.Media.Brushes.Magenta;

                // Single Prints
                ShowSinglePrints    = true;
                SinglePrintColor    = System.Windows.Media.Brushes.Yellow;
                SinglePrintOpacity  = 25;

                // Alertas
                AlertPOC      = false;
                AlertVAH      = false;
                AlertVAL      = false;
                AlertNakedPOC = true;
                AlertVPOC     = false;
                AlertSoundFile= "Alert1.wav";

                // Volume Profile
                ShowVolumeProfile  = true;
                VolumeStyle        = TpoVolumeStyle.Bars;
                VolProfileWidth    = 100;
                VolBarColor        = System.Windows.Media.Brushes.DimGray;
                VolBarOpacity      = 70;
                VolVAColor         = System.Windows.Media.Brushes.DeepSkyBlue;
                VolPocColor        = System.Windows.Media.Brushes.OrangeRed;

                // Perfil Delta
                ShowDeltaProfile     = true;
                DeltaOverlayOnVolume = true;   // overlay on volume by default (shared axis)
                DeltaAlignment       = TpoDeltaAlignment.LeftAligned;
                DeltaProfileWidth    = 80;
                DeltaBuyColor        = System.Windows.Media.Brushes.LimeGreen;
                DeltaSellColor       = System.Windows.Media.Brushes.Crimson;
                DeltaOpacity         = 55;     // semi-transparent so volume shows through
                DeltaZeroColor       = System.Windows.Media.Brushes.Gray;

                // Stats
                ShowStatsPanel        = true;
                StatsPanelPosition    = TextPosition.TopRight;
                StatsFontSize         = 12f;
                StatsTextColor        = System.Windows.Media.Brushes.White;
                StatsBgColor          = System.Windows.Media.Brushes.Black;
                StatsBgOpacity        = 75;
                ShowDayType           = true;
                StatsShowDate         = true;
                StatsShowPOC          = true;
                StatsShowIB           = true;
                StatsShowOR           = true;
                StatsShowProfileRange = true;
                StatsShowVolStats     = true;

                // WPF floating panel (Fase 2)
                UseWpfPanel           = true;
                ShowToolbarButton     = true;
                PanelX                = -1;   // sentinel: center horizontally on first show
                PanelY                = -1;   // sentinel: center vertically on first show
                PanelMinimizedPersist = false;

                // Zoom & extension (Fase 3)
                ScaleWidthWithZoom    = true;
                ProfileWidthPct       = 60;
                ExtendVaPoc           = false;
                ExtendVpVaPoc         = false;

                // Line styles (Fase 3)
                VaLineStyle           = TpoLineStyle.Dash;
                IbLineStyle           = TpoLineStyle.Solid;
                OrLineStyle           = TpoLineStyle.Solid;
                IbOrLineWidth         = 1.5f;

                // Avanzado
                ManualTickSize = 0;
                ProfileGap     = 25;
                DebugLog       = false;

                BuildVersionStamp        = "ILT_DEV_PROFILES_v8_0_PRODUCTION_TF_LOCKED_TPO";
                OptimizeHistoricalLoad   = true;
                RealtimeRecalcMs         = 250;
                CullOffscreenPriceLevels = true;
                CurrentSessionVisualMode = TpoProfileVisualMode.Auto;
                HistoricalVisualMode     = TpoProfileVisualMode.Silhouette;
                AutoLevelOfDetail        = true;
                FreezeClosedProfiles     = true;
                DetailMarkersOnlyCurrent = true;
            }
            else if (State == State.Configure)
            {
                // Serie 1 (tick): volumen/delta por trade en tiempo real y como
                // fallback historico cuando Tick Replay esta apagado.
                AddDataSeries(BarsPeriodType.Tick, 1);

                // Serie 2 (1-min): muestreo de TIEMPO FIJO que alimenta los TPO.
                // El perfil (TPO/POC/VA/IB/OR) se construye SIEMPRE desde esta
                // serie de 1 minuto, sin importar la temporalidad del grafico.
                // Asi el perfil es identico en Range 40, 5min, 15min o cualquier
                // grafico: cambiar la temporalidad ya NO altera VA/POC/zonas.
                // (Estandar institucional CME/Dalton: el perfil se muestrea por
                // tiempo, no por la barra visible.)
                AddDataSeries(BarsPeriodType.Minute, 1);
            }
            else if (State == State.DataLoaded)
            {
                _profiles.Clear();
                _overnight.Clear();
                _currentProfile = null;
                _currentON      = null;
                _sessionModeRt  = SessionPeriod;
                _lastProcessedBar = -1;
                _lastTickPrice    = 0;
                _lastTradePrice   = 0;
                _lastTradeDir     = 1;
                _curBid = _curAsk = 0;
                _omdActive        = false;
                InvalidateCompositeCache();

                // Detect whether Tick Replay is enabled on the primary series.
                // When ON, OnMarketData fires historically with real Bid/Ask, so
                // it becomes the authoritative source and we ignore the tick-series
                // path entirely (no double counting). When OFF, OnMarketData only
                // fires in real time, so historical volume comes from the tick series.
                _tickReplayOn = false;
                try { _tickReplayOn = (BarsArray != null && BarsArray[0] != null && BarsArray[0].IsTickReplay); }
                catch { _tickReplayOn = false; }
                if (DebugLog) Print("ILT-DEV-PROFILES: Tick Replay = " + _tickReplayOn);
            }
            else if (State == State.Historical || State == State.Realtime)
            {
                if (ChartControl != null)
                {
                    if (UseWpfPanel)
                        // Build the real WPF floating panel on the UI thread.
                        ChartControl.Dispatcher.InvokeAsync(() => BuildWpfPanel());
                    else
                        // Legacy SharpDX panel: hook chart mouse for hit-testing.
                        ChartControl.Dispatcher.InvokeAsync(() => HookMouseEvents());

                    // Top toolbar button (independent of WPF/legacy mode).
                    if (ShowToolbarButton)
                        ChartControl.Dispatcher.InvokeAsync(() => BuildToolbarButton());
                }
            }
            else if (State == State.Terminated)
            {
                UnhookMouseEvents();
                if (ChartControl != null)
                {
                    ChartControl.Dispatcher.InvokeAsync(() => RemoveWpfPanel());
                    ChartControl.Dispatcher.InvokeAsync(() => RemoveToolbarButton());
                }
                DisposeResources();
            }
        }

        // =====================================================================
        //  ON BAR UPDATE
        // =====================================================================

        protected override void OnBarUpdate()
        {
            // ── BarsInProgress 1 = tick series (precise per-tick volume) ─────
            if (BarsInProgress == 1)
            {
                AccumulateTickVolume();
                return;
            }

            // ── BarsInProgress 2 = 1-min sampling series (TPO + session logic) ─
            // ALL profile construction (TPO letters, POC/VA, IB/OR, session
            // rollover) runs from this fixed 1-minute series. Because the sample
            // period is constant, the profile is identical regardless of the
            // chart's timeframe — changing the chart from 5min to 15min to
            // Range 40 no longer alters VA/POC/IB/OR or any zone.
            if (BarsInProgress == 2)
            {
                ProcessTpoBar();
                return;
            }

            // ── BarsInProgress 0 = primary (chart) series ────────────────────
            // The primary series is used ONLY to drive rendering coordinates and
            // alert checks. It no longer feeds the profile engine, so the chart's
            // timeframe has zero effect on the calculated profile.
            if (CurrentBars[0] < 1) return;

            CheckPriceAlerts(ManualTickSize > 0
                ? ManualTickSize
                : Instrument.MasterInstrument.TickSize);
        }

        // =====================================================================
        //  TPO SAMPLING ENGINE  (BarsInProgress == 2, fixed 1-minute series)
        //
        //  Single source of truth for the profile shape. Runs on a constant
        //  1-minute clock so the TPO distribution, POC, Value Area, IB and OR
        //  are timeframe-independent. Volume/Delta still come from the tick
        //  series / OnMarketData; this engine owns TPO letters + session
        //  lifecycle only.
        // =====================================================================
        private void ProcessTpoBar()
        {
            if (CurrentBars[2] < 0) return;

            DateTime barTime = Times[2][0];
            double   barHigh = Highs[2][0];
            double   barLow  = Lows[2][0];
            int      barIdx  = CurrentBars[2];

            bool isNewBar = (barIdx != _lastProcessedBar);

            double ts = ManualTickSize > 0
                ? ManualTickSize
                : Instrument.MasterInstrument.TickSize;

            int hhmm  = barTime.Hour * 100 + barTime.Minute;
            int hhmm1 = -1;
            if (CurrentBars[2] > 0)
            {
                DateTime prevT = Times[2][1];
                hhmm1 = prevT.Hour * 100 + prevT.Minute;
            }

            // ── Session creation: ONCE per new 1-min bar only ────────────────
            // Structural changes to the profile collections are taken under the
            // lock so a concurrent period-button rebuild on the UI thread can't
            // observe a half-mutated list.
            if (isNewBar)
            {
                lock (_profilesLock)
                {
                    // RTH session rollover
                    if (IsNewRTHPeriod(hhmm, hhmm1))
                    {
                        if (_currentProfile != null) FinalizeClosedProfile(_currentProfile, ts);
                        StartNewRTHProfile(ts);

                        // The ETH window ends exactly when RTH opens (09:30). Finalize
                        // and detach the overnight profile here too so a late ETH-hour
                        // print during the day doesn't contaminate last night's profile.
                        if (ShowOvernight && _currentON != null)
                        {
                            FinalizeClosedProfile(_currentON, ts);
                            _currentON = null;
                        }
                    }
                    // Overnight session rollover
                    if (ShowOvernight && _sessionModeRt == TpoSessionPeriod.Sesiones && IsNewONPeriod(hhmm, hhmm1))
                    {
                        if (_currentON != null) FinalizeClosedProfile(_currentON, ts);
                        StartNewONProfile();
                    }
                }
            }
            _lastProcessedBar = barIdx;

            // ── TPO / range update from the fixed 1-min bar ──────────────────
            // Gated by session hours so off-session bars never expand the profile
            // range or add letters where no in-session trading occurred. The TPO
            // letter is derived from the bar TIMESTAMP (TPOMinutes), so the 1-min
            // sampling fills the profile at full resolution and identically on
            // every chart timeframe.
            // The TPO update inserts new price levels into the profile's Levels
            // dictionary, so we serialize it against the render thread and rebuild
            // with the shared lock.
            lock (_profilesLock)
            {
                if (_currentProfile != null && IsInPrimaryProfileTime(barTime))
                {
                    UpdateProfileFromBar(_currentProfile, ts, barTime, barHigh, barLow, barIdx, hhmm);
                    _currentProfile.LastClose = Closes[2][0];   // for AMT day-type, timeframe-independent
                }

                if (ShowOvernight && _currentON != null && IsInOvernightHours(hhmm))
                {
                    UpdateONProfileFromBar(_currentON, ts, barTime, barHigh, barLow, barIdx, hhmm);
                    _currentON.LastClose = Closes[2][0];
                }
            }
        }

        // =====================================================================
        //  PROFESSIONAL ORDER-FLOW ENGINE  —  OnMarketData
        //
        //  This is the institutional-grade data path. With Tick Replay enabled,
        //  OnMarketData fires for every trade BOTH historically and in real time,
        //  giving us the real Bid and Ask at the moment of each trade.
        //
        //  Trade classification (the right way):
        //    Last >= Ask  → aggressive BUY  (taker lifted the offer)
        //    Last <= Bid  → aggressive SELL (taker hit the bid)
        //    in-between    → fallback to uptick/downtick (rare; usually mid-fills)
        //
        //  Every classified trade simultaneously feeds:
        //    Volume + BuyVolume(ask) + SellVolume(bid) + Delta
        //  routed to the correct RTH or ETH profile by the trade's TIMESTAMP.
        //
        //  TPO letters are handled separately in OnBarUpdate (time-based), so a
        //  single tick stream drives all profile components consistently.
        // =====================================================================

        private double _lastTradePrice = 0;     // for uptick/downtick fallback
        private int    _lastTradeDir    = 1;     // +1 buy, -1 sell
        private double _curBid = 0;
        private double _curAsk = 0;

        protected override void OnMarketData(MarketDataEventArgs e)
        {
            // Track the inside bid/ask as they update
            if (e.MarketDataType == MarketDataType.Bid) { _curBid = e.Price; return; }
            if (e.MarketDataType == MarketDataType.Ask) { _curAsk = e.Price; return; }
            if (e.MarketDataType != MarketDataType.Last) return;

            // Engine path rules to prevent double counting:
            //  - Tick Replay ON  : OnMarketData is authoritative for BOTH historical
            //                       and realtime. Always accumulate here.
            //  - Tick Replay OFF : tick series handles historical; OnMarketData only
            //                       accumulates once we are in realtime.
            if (!_tickReplayOn && State != State.Realtime) return;

            _omdActive = true;   // OnMarketData is now contributing

            double price  = e.Price;
            double vol     = e.Volume;
            if (vol <= 0) return;

            // Determine bid/ask reference. With Tick Replay, e.Bid / e.Ask carry
            // the inside quote at trade time. If those are zero (no quote yet),
            // fall back to the running quote we tracked from Bid/Ask events.
            double bid = e.Bid > 0 ? e.Bid : _curBid;
            double ask = e.Ask > 0 ? e.Ask : _curAsk;

            bool isBuy = ClassifyTrade(price, bid, ask);

            _lastTradePrice = price;
            _lastTradeDir   = isBuy ? 1 : -1;

            double ts       = ManualTickSize > 0 ? ManualTickSize : Instrument.MasterInstrument.TickSize;
            double priceKey = Math.Round(price / ts) * ts;

            DateTime tradeTime = e.Time;
            int hhmm = tradeTime.Hour * 100 + tradeTime.Minute;

            // Route to the correct profile by timestamp.
            // RTH and ON profiles are created/closed in OnBarUpdate; here we just
            // deposit volume into whichever profile currently owns this timestamp.
            // The lock serializes against the render thread (which iterates the same
            // Levels dictionaries) and against a period-button rebuild. It is held
            // only for the two dictionary inserts, so per-tick cost is negligible.
            lock (_profilesLock)
            {
                if (_currentProfile != null && IsInPrimaryProfileTime(tradeTime))
                    AddTradeToProfile(_currentProfile, priceKey, vol, isBuy);

                if (_currentON != null && ShowOvernight && IsInOvernightHours(hhmm))
                    AddTradeToProfile(_currentON, priceKey, vol, isBuy);
            }
        }

        // Classify a trade as buy (true) or sell (false) using the configured mode.
        private bool ClassifyTrade(double price, double bid, double ask)
        {
            bool haveQuote = (bid > 0 && ask > 0 && ask >= bid);

            switch (DeltaMode)
            {
                case TpoDeltaMode.UpDownTick:
                    return UpDownTickClassify(price);

                case TpoDeltaMode.BidAsk:
                    if (haveQuote)
                    {
                        if (price >= ask) return true;    // lifted offer = buy
                        if (price <= bid) return false;   // hit bid = sell
                        // between bid/ask: midpoint split, fall back to tick rule
                        return UpDownTickClassify(price);
                    }
                    return UpDownTickClassify(price);

                case TpoDeltaMode.Auto:
                default:
                    if (haveQuote)
                    {
                        if (price >= ask) return true;
                        if (price <= bid) return false;
                        return UpDownTickClassify(price);
                    }
                    return UpDownTickClassify(price);
            }
        }

        private bool UpDownTickClassify(double price)
        {
            if (price > _lastTradePrice)      return true;
            else if (price < _lastTradePrice) return false;
            else                               return _lastTradeDir > 0;
        }

        // =====================================================================
        //  TICK-SERIES FALLBACK  (BarsInProgress == 1)
        //
        //  When Tick Replay is OFF, OnMarketData does not fire historically.
        //  In that case the 1-tick data series still gives us per-trade volume
        //  (but only uptick/downtick classification, no real bid/ask).
        //  We use a guard so we DON'T double-count: if OnMarketData already
        //  handled this trade (Tick Replay on), the tick-series path is skipped.
        // =====================================================================

        private double _lastTickPrice = 0;
        private bool   _lastTickWasBuy = true;
        private bool   _omdActive = false;   // true once OnMarketData has fired
        private bool   _tickReplayOn = false; // detected at DataLoaded
        private bool   _labelsOnForProfile = true;  // gate for current profile's labels
        private int    _lastRealtimeRecalcMs = 0;   // throttle heavy POC/VA/VPOC recomputation in realtime

        // Composite cache: the composite profile is expensive because it merges
        // multiple sessions. Keep a lightweight data cache and rebuild only when
        // contributing volume/configuration changes. This is render-only; source
        // session data remains authoritative.
        private bool _compositeDirty = true;
        private readonly Dictionary<double, double> _compVolCache     = new Dictionary<double, double>();
        private readonly Dictionary<double, double> _compBuyVolCache  = new Dictionary<double, double>();
        private readonly Dictionary<double, double> _compSellVolCache = new Dictionary<double, double>();
        private readonly List<double> _compSortedKeysCache = new List<double>();
        private double _compMaxVol = 0, _compPOC = 0, _compVAH = 0, _compVAL = 0;
        private string _compModeTag = "COMP";
        private string _compConfigKey = string.Empty;

        private void AccumulateTickVolume()
        {
            if (CurrentBars[1] < 0) return;

            // Path selection:
            //  - Tick Replay ON  → OnMarketData is authoritative (real Bid/Ask),
            //                       historically AND real-time. Skip tick series.
            //  - Tick Replay OFF → OnMarketData only fires in real time. Use the
            //                       tick series for historical, and once OMD goes
            //                       live (_omdActive) hand off to it to avoid dupes.
            if (_tickReplayOn) return;
            if (_omdActive)    return;

            double price     = Closes[1][0];
            double tickVol   = Volumes[1][0];
            double ts        = ManualTickSize > 0 ? ManualTickSize : Instrument.MasterInstrument.TickSize;
            double priceKey  = Math.Round(price / ts) * ts;

            bool isBuy;
            if (price > _lastTickPrice)      isBuy = true;
            else if (price < _lastTickPrice) isBuy = false;
            else                              isBuy = _lastTickWasBuy;

            _lastTickPrice    = price;
            _lastTickWasBuy   = isBuy;

            int hhmmRT = Times[1][0].Hour * 100 + Times[1][0].Minute;
            lock (_profilesLock)
            {
                if (_currentProfile != null && IsInPrimaryProfileTime(Times[1][0]))
                    AddTradeToProfile(_currentProfile, priceKey, tickVol, isBuy);

                if (_currentON != null && ShowOvernight && IsInOvernightHours(hhmmRT))
                    AddTradeToProfile(_currentON, priceKey, tickVol, isBuy);
            }
        }

        // Deposit a classified trade into a profile's price level.
        private void AddTradeToProfile(TpoProfileData p, double priceKey, double vol, bool isBuy)
        {
            TpoPriceLevel lvl;
            if (!p.Levels.TryGetValue(priceKey, out lvl))
            {
                lvl = new TpoPriceLevel { Price = priceKey };
                p.Levels[priceKey] = lvl;
                p.SortedKeysDirty = true;
            }

            lvl.Volume += vol;
            if (isBuy) lvl.BuyVolume  += vol;
            else       lvl.SellVolume += vol;

            if (lvl.Volume > p.MaxVolume) p.MaxVolume = lvl.Volume;
            double absDelta = Math.Abs(lvl.Delta);
            if (absDelta > p.MaxAbsDelta) p.MaxAbsDelta = absDelta;

            p.IsDirty = true;   // mark for POC/VA recompute
            InvalidateCompositeCache();
        }

        private bool IsInRTHHours(int hhmm)
        {
            // Half-open interval [start, end): the close minute (e.g. 1700) is
            // already NOT in RTH, which lets the dead zone (17:00–18:00) start
            // clean and prevents RTH from accumulating a stray tick at exactly 17:00.
            if (SessionStartHHmm > SessionEndHHmm)
                return hhmm >= SessionStartHHmm || hhmm < SessionEndHHmm;
            return hhmm >= SessionStartHHmm && hhmm < SessionEndHHmm;
        }

        // Main profile time gate.
        //   Sesiones = solo RTH 09:30–17:00.
        //   Diario   = una sesion completa 18:00→17:00, excluyendo 17:00–18:00.
        //   Semanal  = domingo 18:00→viernes 17:00, excluyendo los descansos diarios.
        private bool IsInPrimaryProfileTime(DateTime t)
        {
            int hhmm = t.Hour * 100 + t.Minute;

            if (_sessionModeRt == TpoSessionPeriod.Sesiones)
                return IsInRTHHours(hhmm);

            // En Diario/Semanal el perfil NO debe absorber el mantenimiento CME
            // 17:00–18:00; la nueva sesion diaria nace al reabrir a las 18:00.
            if (IsInDeadZone(hhmm))
                return false;

            if (_sessionModeRt == TpoSessionPeriod.Semanal)
                return IsInTradingWeekTime(t);

            // Diario: todo el bloque 18:00→17:00 pertenece al mismo perfil,
            // pero se respeta el cierre semanal de viernes 17:00 a domingo 18:00.
            if (t.DayOfWeek == DayOfWeek.Saturday) return false;
            if (t.DayOfWeek == DayOfWeek.Sunday && hhmm < ONStartHHmm) return false;
            if (t.DayOfWeek == DayOfWeek.Friday && hhmm >= SessionEndHHmm) return false;
            return true;
        }

        private bool IsInTradingWeekTime(DateTime t)
        {
            int hhmm = t.Hour * 100 + t.Minute;

            // Futuros CME: abre domingo 18:00 NY y cierra viernes 17:00 NY.
            if (t.DayOfWeek == DayOfWeek.Saturday) return false;
            if (t.DayOfWeek == DayOfWeek.Sunday)   return hhmm >= ONStartHHmm;
            if (t.DayOfWeek == DayOfWeek.Friday)   return hhmm < SessionEndHHmm;
            return true; // lunes-jueves, salvo dead-zone ya filtrado arriba
        }

        private DateTime GetDailySessionKey(DateTime t)
        {
            int hhmm = t.Hour * 100 + t.Minute;
            // La sesion diaria se identifica por la fecha de su apertura a las 18:00.
            return hhmm >= ONStartHHmm ? t.Date : t.Date.AddDays(-1);
        }

        private DateTime GetTradingWeekKey(DateTime t)
        {
            DateTime d = t.Date;
            int daysSinceSunday = (int)d.DayOfWeek; // Sunday=0 ... Saturday=6
            DateTime sunday = d.AddDays(-daysSinceSunday);

            // Si por alguna razon llega un timestamp domingo antes de 18:00,
            // pertenece logicamente a la semana anterior; normalmente no hay data.
            int hhmm = t.Hour * 100 + t.Minute;
            if (d.DayOfWeek == DayOfWeek.Sunday && hhmm < ONStartHHmm)
                sunday = sunday.AddDays(-7);

            return sunday.Date;
        }

        private DateTime GetPrimarySessionKey(DateTime t)
        {
            if (_sessionModeRt == TpoSessionPeriod.Diario)
                return GetDailySessionKey(t);
            if (_sessionModeRt == TpoSessionPeriod.Semanal)
                return GetTradingWeekKey(t);
            return t.Date;
        }

        private void CheckPriceAlerts(double ts)
        {
            if (_currentProfile == null) return;
            double hi = High[0]; double lo = Low[0];

            // Alert on POC touch
            if (AlertPOC && _currentProfile.POC != _lastAlertPOC)
            {
                if (hi >= _currentProfile.POC && lo <= _currentProfile.POC)
                {
                    Alert("POC_Touch", Priority.Medium, "Precio toca el POC: " + _currentProfile.POC.ToString("F2"),
                        AlertSoundFile, 10, System.Windows.Media.Brushes.Orange, System.Windows.Media.Brushes.Black);
                    _lastAlertPOC = _currentProfile.POC;
                }
            }

            // Alert on VAH touch
            if (AlertVAH && _currentProfile.VAH != _lastAlertVAH)
            {
                if (hi >= _currentProfile.VAH && lo <= _currentProfile.VAH)
                {
                    Alert("VAH_Touch", Priority.Medium, "Precio toca VAH: " + _currentProfile.VAH.ToString("F2"),
                        AlertSoundFile, 10, System.Windows.Media.Brushes.Cyan, System.Windows.Media.Brushes.Black);
                    _lastAlertVAH = _currentProfile.VAH;
                }
            }

            // Alert on VAL touch
            if (AlertVAL && _currentProfile.VAL != _lastAlertVAL)
            {
                if (hi >= _currentProfile.VAL && lo <= _currentProfile.VAL)
                {
                    Alert("VAL_Touch", Priority.Medium, "Precio toca VAL: " + _currentProfile.VAL.ToString("F2"),
                        AlertSoundFile, 10, System.Windows.Media.Brushes.Cyan, System.Windows.Media.Brushes.Black);
                    _lastAlertVAL = _currentProfile.VAL;
                }
            }

            // Alert on VPOC touch (volume POC)
            if (AlertVPOC)
            {
                double vpoc = GetCurrentVPOC();
                if (vpoc > 0 && vpoc != _lastAlertVPOC)
                {
                    if (hi >= vpoc && lo <= vpoc)
                    {
                        Alert("VPOC_Touch", Priority.Medium, "Precio toca VPOC: " + vpoc.ToString("F2"),
                            AlertSoundFile, 10, System.Windows.Media.Brushes.OrangeRed, System.Windows.Media.Brushes.Black);
                        _lastAlertVPOC = vpoc;
                    }
                }
            }

            // Alert on Naked POC touch
            if (AlertNakedPOC && !_alertNakedFired)
            {
                for (int i = 0; i < _profiles.Count - 1; i++)
                {
                    TpoProfileData p = _profiles[i];
                    bool touched = false;
                    for (int j = i+1; j < _profiles.Count; j++)
                        if (_profiles[j].ProfileHigh >= p.POC && _profiles[j].ProfileLow <= p.POC)
                        { touched = true; break; }
                    if (!touched && hi >= p.POC && lo <= p.POC)
                    {
                        Alert("NakedPOC_Touch", Priority.High,
                            "Precio toca Naked POC: " + p.POC.ToString("F2") + " del " + p.StartTime.ToString("MM/dd"),
                            AlertSoundFile, 10, System.Windows.Media.Brushes.Red, System.Windows.Media.Brushes.White);
                        _alertNakedFired = true;
                        break;
                    }
                }
            }
            else if (_alertNakedFired)
            {
                // Reset after price moves away
                bool stillAt = false;
                for (int i = 0; i < _profiles.Count - 1; i++)
                {
                    TpoProfileData p = _profiles[i];
                    if (hi >= p.POC - ts && lo <= p.POC + ts) { stillAt = true; break; }
                }
                if (!stillAt) _alertNakedFired = false;
            }
        }

        private double GetCurrentVPOC()
        {
            if (_currentProfile == null || _currentProfile.MaxVolume <= 0) return 0;
            // Use the deterministic VPOC computed in CalculateVolumeVA (center tie-break),
            // recomputed on the dirty pass — consistent with the volume-profile drawing.
            return _currentProfile.VPOC;
        }

        // ── RTH helpers ───────────────────────────────────────────────────────

        // New RTH session starts when we cross the SessionStartHHmm boundary
        // (i.e., previous bar was before session start, current bar is at/after).
        private bool IsNewRTHPeriod(int hhmm, int hhmm1)
        {
            DateTime nowTime = Time[0];
            bool nowIn = IsInPrimaryProfileTime(nowTime);
            if (!nowIn) return false;

            DateTime nowKey = GetPrimarySessionKey(nowTime);
            if (_currentProfile == null) return true;

            // Sesiones: abre un nuevo perfil cuando entramos a RTH.
            // Diario: abre en la reapertura 18:00 y cierra logicamente a las 17:00.
            // Semanal: abre solo cuando cambia la semana operativa CME
            //          (domingo 18:00 → viernes 17:00).
            return nowKey != _currentProfile.SessionKey;
        }

        private static int GetIsoWeekKey(DateTime d)
        {
            System.Globalization.Calendar cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
            int week = cal.GetWeekOfYear(d, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            int year = d.Year;
            if (week >= 52 && d.Month == 1) year--;        // primeros dias de enero pueden ser semana 52/53 del año previo
            if (week == 1 && d.Month == 12) year++;        // ultimos dias de diciembre pueden ser semana 1 del siguiente
            return year * 100 + week;
        }

        private void StartNewRTHProfile(double ts)
        {
            StartNewRTHProfile(ts, Time[0], CurrentBar);
        }

        private void StartNewRTHProfile(double ts, DateTime anchorTime, int anchorIdx)
        {
            TpoProfileData p = new TpoProfileData();
            p.StartTime   = anchorTime;
            p.EndTime     = anchorTime;
            p.ProfileHigh = double.MinValue;
            p.ProfileLow  = double.MaxValue;
            p.FirstBarIdx = anchorIdx;
            p.LastBarIdx  = anchorIdx;
            p.IsOvernight = false;

            _currentProfile   = p;
            p.SessionKey      = GetPrimarySessionKey(anchorTime);
            ComputeSessionWindow(p);          // theoretical profile window for drawing

            _profiles.Add(p);
            while (_profiles.Count > ProfilesBack + 1) _profiles.RemoveAt(0);
            InvalidateCompositeCache();
        }

        private void UpdateProfileFromBar(TpoProfileData p, double ts, DateTime barTime,
                                          double barHigh, double barLow, int barIndex, int hhmm)
        {
            // ── TIMEFRAME-INDEPENDENT TPO period via theoretical profile start ─
            // This anchors Diario at 18:00 and Semanal at Sunday 18:00 even when
            // Ninja timestamps minute bars at bar close (e.g., first bar 18:01).
            DateTime clockStart = p.WindowStart == DateTime.MinValue ? p.StartTime : p.WindowStart;
            int periodIndex = GetPeriodIndexForTime(barTime, clockStart);
            char letter = GetLetterForPeriod(periodIndex);

            // IB: minutes elapsed from theoretical profile start
            double elapsedMins = (barTime - clockStart).TotalMinutes;
            if (elapsedMins < 0) elapsedMins = 0;
            bool isIB = (elapsedMins < IBMinutes);

            // Opening Range tracking (also based on profile StartTime)
            if (ShowOR && !p.ORComplete)
            {
                if (elapsedMins < ORMinutes)
                {
                    if (p.ORH == 0 || barHigh > p.ORH) p.ORH = barHigh;
                    if (p.ORL == 0 || barLow  < p.ORL) p.ORL = barLow;
                }
                else
                    p.ORComplete = true;
            }

            AddTPOsForRange(p, ts, letter, isIB, barLow, barHigh, barIndex, periodIndex);

            if (barHigh > p.ProfileHigh) p.ProfileHigh = barHigh;
            if (barLow  < p.ProfileLow)  p.ProfileLow  = barLow;
            p.EndTime    = barTime;
            p.LastBarIdx = barIndex;

            if (isIB)
            {
                if (barHigh > p.IBH) p.IBH = barHigh;
                if (p.IBL < 0.000001 || barLow < p.IBL) p.IBL = barLow;
            }

            // Note: do NOT force IsDirty here. AddTPOsForRange sets IsDirty when
            // a new letter is actually added, and AddTradeToProfile sets it when
            // volume arrives. In optimized mode we do NOT sort/recalculate on every
            // historical tick; the session is finalized on rollover and the active
            // profile is refreshed from OnRender/realtime throttle.
            if (!OptimizeHistoricalLoad || State == State.Realtime || AnyPriceAlertEnabled())
                RecomputeProfileIfDirty(p, ts);
        }

        // Recompute POC / Value Area / Volume VA / day type only when the
        // profile's data has changed. Cheap when called repeatedly; the heavy
        // loops run only on the first call after new data arrives.
        private void RecomputeProfileIfDirty(TpoProfileData p, double ts)
        {
            if (!p.IsDirty) return;

            // Re-derive the volume/delta normalization maxima from scratch. MaxVolume
            // only ever grows so it is safe incrementally, but a level's |Delta| can
            // SHRINK as opposing volume arrives, so a running max would overstate it
            // and leave delta bars permanently under-scaled. Recompute both here.
            double maxVol = 0, maxAbsDelta = 0;
            foreach (KeyValuePair<double, TpoPriceLevel> kv in p.Levels)
            {
                if (kv.Value.Volume > maxVol) maxVol = kv.Value.Volume;
                double ad = Math.Abs(kv.Value.Delta);
                if (ad > maxAbsDelta) maxAbsDelta = ad;
            }
            p.MaxVolume   = maxVol;
            p.MaxAbsDelta = maxAbsDelta;

            CalculatePOCAndVA(p, ts);
            CalculateVolumeVA(p);
            ClassifyDayType(p);
            p.IsDirty = false;
        }



        // ── Overnight helpers ─────────────────────────────────────────────────

        // Returns true if hhmm falls inside the overnight window.
        // Handles midnight wrap: e.g. ONStart=1800, ONEnd=929
        //   in-window = hhmm >= 1800 OR hhmm <= 929
        private bool IsInOvernightHours(int hhmm)
        {
            if (_sessionModeRt != TpoSessionPeriod.Sesiones) return false;

            // Mutually exclusive with RTH and with the 17:00–18:00 dead zone.
            // Anything that is RTH or dead-zone cannot also be overnight.
            if (IsInRTHHours(hhmm)) return false;
            if (IsInDeadZone(hhmm)) return false;

            if (ONStartHHmm > ONEndHHmm)          // crosses midnight (e.g. 1800..929)
                return hhmm >= ONStartHHmm || hhmm <= ONEndHHmm;
            else
                return hhmm >= ONStartHHmm && hhmm <= ONEndHHmm;
        }

        // The gap between RTH close (SessionEndHHmm) and ETH open (ONStartHHmm).
        // For ES/NQ this is 17:00–18:00 NY: no profile accumulates here, and
        // nothing should be drawn either, so RTH never bleeds into ETH visually.
        private bool IsInDeadZone(int hhmm)
        {
            // Only meaningful when there is a real gap (RTH end strictly before ETH start).
            if (SessionEndHHmm <= 0 || ONStartHHmm <= 0)        return false;
            if (SessionEndHHmm >= ONStartHHmm)                  return false;
            return hhmm >= SessionEndHHmm && hhmm < ONStartHHmm;
        }

        // Convert a HHmm integer (e.g. 930, 1700) to a DateTime on a given date.
        private static DateTime HHmmToDate(DateTime date, int hhmm)
        {
            int h = (hhmm / 100) % 24;
            int m =  hhmm % 100;
            return new DateTime(date.Year, date.Month, date.Day, h, m, 0);
        }

        // Timestamp of a primary (chart) bar index, clamped and exception-safe.
        private DateTime SafePrimaryBarTime(int barIdx)
        {
            try
            {
                if (ChartBars == null || ChartBars.Bars == null) return DateTime.MinValue;
                int maxIdx = ChartBars.Bars.Count - 1;
                if (barIdx < 0) barIdx = 0;
                if (barIdx > maxIdx) barIdx = maxIdx;
                return ChartBars.Bars.GetTime(barIdx);
            }
            catch { return DateTime.MinValue; }
        }

        // Returns the primary (chart) bar index whose timestamp is at or before
        // `target`, clamped to the loaded range. Used to anchor profile rendering
        // by time so the chart timeframe never affects placement. Robust for
        // Minute/Second/Range/Renko because it relies on NT8's own time index.
        private int PrimaryBarIdxByTime(DateTime target)
        {
            try
            {
                if (ChartBars == null || ChartBars.Bars == null) return 0;
                int idx = ChartBars.Bars.GetBar(target);
                if (idx < 0) idx = 0;
                int maxIdx = ChartBars.Bars.Count - 1;
                if (idx > maxIdx) idx = maxIdx;
                return idx;
            }
            catch { return 0; }
        }

        // Map a target timestamp to chart X (pixels).
        //
        // The profile engine is fed by the fixed 1-min series, so a profile's
        // FirstBarIdx/LastBarIdx are NOT primary-series indices and must never be
        // used for chart coordinates. Instead we anchor purely by TIME: we find
        // the primary (chart) bar whose timestamp is closest to the target, take
        // its X from NT8, and project the residual time delta using the chart's
        // bar period. This makes rendering fully timeframe-independent and keeps
        // every profile glued to its real clock location.
        private float TimeToX(TpoProfileData p, DateTime target)
        {
            int anchorBar = PrimaryBarIdxByTime(target);
            DateTime anchorTime = SafePrimaryBarTime(anchorBar);
            if (anchorTime == DateTime.MinValue) anchorTime = target;

            float xAnchor = BarToX(anchorBar);

            // Convert the time delta to "bars" using the chart's primary bar period.
            // For Minute bars this is exact (1 bar = N minutes). For non-time bars
            // (Range/Renko) we fall back to a tick estimate; if anything is unknown
            // we just return xAnchor (no drift).
            double barMinutes = 1.0;
            try
            {
                if (BarsArray != null && BarsArray.Length > 0 && BarsArray[0].BarsPeriod != null)
                {
                    BarsPeriod bp = BarsArray[0].BarsPeriod;
                    if (bp.BarsPeriodType == BarsPeriodType.Minute)
                        barMinutes = Math.Max(1, bp.Value);
                    else if (bp.BarsPeriodType == BarsPeriodType.Second)
                        barMinutes = Math.Max(1, bp.Value) / 60.0;
                    else if (bp.BarsPeriodType == BarsPeriodType.Day)
                        barMinutes = Math.Max(1, bp.Value) * 60.0 * 24.0;
                    // For Tick/Range/Renko we leave barMinutes = 1 as a coarse default;
                    // the session-bounds logic below clamps the result regardless.
                }
            }
            catch { /* keep default */ }

            double deltaBars = (target - anchorTime).TotalMinutes / barMinutes;
            return xAnchor + (float)(deltaBars * _slotW);
        }

        // Pixel bounds (xStart, xEnd) of a profile's theoretical session window.
        // Used by drawing code so every profile lives strictly inside its time
        // slot and never invades the next session, regardless of zoom.
        private void GetSessionWindowPx(TpoProfileData p, out float xStart, out float xEnd)
        {
            // The session window is purely HORIZONTAL geometry. We split it into two
            // parts with different stability needs:
            //
            //   • xStart (POSITION): must track horizontal scroll, so it comes from
            //     BarToX (NT8 native). This is allowed to move when you scroll.
            //
            //   • width  (xEnd - xStart): must be ROCK-STEADY. We do NOT derive it
            //     from BarToX(WindowEnd) - BarToX(WindowStart), because NT8's
            //     GetXByBarIndex depends on ChartPanel.W, and ChartPanel.W changes
            //     by a few pixels whenever the price-axis label width changes — which
            //     happens constantly while you DRAG the Y axis (e.g. "30432,75" vs
            //     "30432,7"). That is the real cause of the width "breathing"/flicker.
            //     Instead we compute the width from the bar-count span × bar distance
            //     (_slotW), which is completely independent of ChartPanel.W and of the
            //     price axis. The window only changes when scroll/zoom changes, so we
            //     cache on that horizontal state.
            int curFrom = 0, curTo = 0;
            try { curFrom = ChartBars.FromIndex; curTo = ChartBars.ToIndex; } catch { }

            // Position part (cheap, exact, follows scroll).
            xStart = TimeToX(p, p.WindowStart);

            // Width part: cached on horizontal state, derived from bar-count × _slotW.
            if (!(p.WinCacheValid && p.WinCacheFrom == curFrom && p.WinCacheTo == curTo
                  && p.WinCacheSlot == _slotW))
            {
                int barStart = PrimaryBarIdxByTime(p.WindowStart);
                int barEnd   = PrimaryBarIdxByTime(p.WindowEnd);
                int spanBars = barEnd - barStart;
                if (spanBars < 1) spanBars = 1;

                p.CachedWidthPx = spanBars * _slotW;
                p.WinCacheFrom  = curFrom;
                p.WinCacheTo    = curTo;
                p.WinCacheSlot  = _slotW;
                p.WinCacheValid = true;
            }

            float width = p.CachedWidthPx;
            if (width < _slotW) width = _slotW;
            xEnd = xStart + width;
        }

        // Compute the theoretical session window (start/end timestamps) for a
        // profile, based purely on the configured RTH/ETH hours and the profile's
        // start date. These bounds drive the drawing extent so each session lives
        // strictly inside its own time slot and never invades the next session,
        // regardless of how many bars it currently holds.
        private void ComputeSessionWindow(TpoProfileData p)
        {
            if (p.IsOvernight)
            {
                // ETH window:
                //   start = previous day at ONStartHHmm (e.g. 18:00 prev day)
                //   end   = profile day at ONEndHHmm+1min   (e.g. 09:30 today)
                // Anchor on the profile's own StartTime to be robust across DST.
                DateTime st = p.StartTime;
                if (st.Hour * 100 + st.Minute >= ONStartHHmm)
                {
                    // We started in the evening: window covers tonight → tomorrow morning.
                    p.WindowStart = HHmmToDate(st.Date, ONStartHHmm);
                    p.WindowEnd   = HHmmToDate(st.Date.AddDays(1), SessionStartHHmm);
                }
                else
                {
                    // We started after midnight: window started yesterday evening.
                    p.WindowStart = HHmmToDate(st.Date.AddDays(-1), ONStartHHmm);
                    p.WindowEnd   = HHmmToDate(st.Date, SessionStartHHmm);
                }
            }
            else
            {
                if (_sessionModeRt == TpoSessionPeriod.Diario)
                {
                    // Diario completo: abre a las 18:00 y termina al dia siguiente 17:00.
                    DateTime sessionDate = p.SessionKey == DateTime.MinValue ? GetDailySessionKey(p.StartTime) : p.SessionKey.Date;
                    p.WindowStart = HHmmToDate(sessionDate, ONStartHHmm);
                    p.WindowEnd   = HHmmToDate(sessionDate.AddDays(1), SessionEndHHmm);
                }
                else if (_sessionModeRt == TpoSessionPeriod.Semanal)
                {
                    // Semana CME completa: domingo 18:00 → viernes 17:00.
                    DateTime weekSunday = p.SessionKey == DateTime.MinValue ? GetTradingWeekKey(p.StartTime) : p.SessionKey.Date;
                    p.WindowStart = HHmmToDate(weekSunday, ONStartHHmm);
                    p.WindowEnd   = HHmmToDate(weekSunday.AddDays(5), SessionEndHHmm);
                }
                else
                {
                    // RTH window: today's 09:30 → today's 17:00.
                    p.WindowStart = HHmmToDate(p.StartTime.Date, SessionStartHHmm);
                    p.WindowEnd   = HHmmToDate(p.StartTime.Date, SessionEndHHmm);
                }
            }
        }

        // True when we just entered the ON window (previous bar was outside, current bar is inside)
        // Also returns true on the very first bar if it is already inside the ON window.
        private bool IsNewONPeriod(int hhmm, int hhmm1)
        {
            bool nowIn  = IsInOvernightHours(hhmm);
            bool prevIn = IsInOvernightHours(hhmm1);

            if (!nowIn) return false;                    // not in ON at all
            if (_currentON == null) return true;         // first ever ON profile
            return nowIn && !prevIn;                     // just crossed into ON
        }

        private void StartNewONProfile()
        {
            StartNewONProfile(Time[0], CurrentBar);
        }

        private void StartNewONProfile(DateTime anchorTime, int anchorIdx)
        {
            TpoProfileData p = new TpoProfileData();
            p.StartTime   = anchorTime;
            p.EndTime     = anchorTime;
            // Sentinel values - first valid ON-hours bar fed via UpdateONProfile sets these
            p.ProfileHigh = double.MinValue;
            p.ProfileLow  = double.MaxValue;
            p.FirstBarIdx = anchorIdx;
            p.LastBarIdx  = anchorIdx;
            p.IsOvernight = true;

            _currentON = p;
            p.SessionKey = anchorTime.Date;
            ComputeSessionWindow(p);          // theoretical ETH window for drawing
            _overnight.Add(p);
            while (_overnight.Count > ProfilesBack + 1) _overnight.RemoveAt(0);
            InvalidateCompositeCache();
        }

        // _onLetterIdx is declared as a class field above

        private void UpdateONProfileFromBar(TpoProfileData p, double ts, DateTime barTime,
                                            double barHigh, double barLow, int barIndex, int hhmm)
        {
            // Guard: only accumulate while we are actually in overnight hours
            if (!IsInOvernightHours(hhmm)) return;

            // ── TIMEFRAME-INDEPENDENT letter assignment ───────────────────────
            DateTime clockStart = p.WindowStart == DateTime.MinValue ? p.StartTime : p.WindowStart;
            int periodIndex = GetPeriodIndexForTime(barTime, clockStart);
            char onLetter = GetLetterForPeriod(periodIndex);

            AddTPOsForRange(p, ts, onLetter, false, barLow, barHigh, barIndex, periodIndex);

            if (barHigh > p.ProfileHigh) p.ProfileHigh = barHigh;
            if (barLow  < p.ProfileLow)  p.ProfileLow  = barLow;
            p.EndTime    = barTime;
            p.LastBarIdx = barIndex;

            // Recompute only when needed. In optimized historical loading, defer
            // heavy sorting until session rollover/render to avoid chart freeze.
            if (!OptimizeHistoricalLoad || State == State.Realtime || AnyPriceAlertEnabled())
                RecomputeProfileIfDirty(p, ts);
        }

        // ── Shared helpers ────────────────────────────────────────────────────

        // =====================================================================
        //  TPO accumulation — only TPO letter accumulation, NO volume.
        //  Volume comes from AccumulateTickVolume() / OnMarketData via the tick
        //  data series, giving precise per-price volume. This decouples TPO
        //  letters from volume so:
        //   • TPO shape is consistent across timeframes (time-based letters,
        //     sampled from the fixed 1-min series)
        //   • Volume shape is consistent across timeframes (tick-based volume)
        //   • Delta is precise (real Bid/Ask with Tick Replay, uptick/downtick
        //     fallback otherwise)
        // =====================================================================

        private void AddTPOsForRange(TpoProfileData p, double ts, char letter, bool isIB,
                                     double barLow, double barHigh, int barIndex, int periodIndex)
        {
            double pLow  = Math.Round(barLow  / ts) * ts;
            double pHigh = Math.Round(barHigh / ts) * ts;

            double price = pLow;
            while (price <= pHigh + ts * 0.001)
            {
                double key = Math.Round(price / ts) * ts;
                TpoPriceLevel lvl;
                if (!p.Levels.TryGetValue(key, out lvl))
                {
                    lvl = new TpoPriceLevel { Price = key };
                    p.Levels[key] = lvl;
                    p.SortedKeysDirty = true;
                }

                // Fast path: during Tick Replay the same current letter is tested
                // thousands of times while the bar range expands. If this level
                // already received that letter, avoid rescanning the list.
                if (lvl.LastPeriodIndex == periodIndex)
                {
                    price += ts;
                    continue;
                }

                bool found = false;
                for (int i = 0; i < lvl.Letters.Count; i++)
                    if (lvl.Letters[i].PeriodIndex == periodIndex) { found = true; break; }

                if (!found)
                {
                    TpoLetterItem item = new TpoLetterItem();
                    item.Letter   = letter;
                    item.Price    = key;
                    item.BarIndex = barIndex;
                    item.PeriodIndex = periodIndex;
                    item.IsIB     = isIB;
                    lvl.Letters.Add(item);
                    lvl.LastLetter = letter;
                    lvl.LastPeriodIndex = periodIndex;
                    if (periodIndex > p.MaxPeriodIndex) p.MaxPeriodIndex = periodIndex;
                    p.IsDirty = true;
                }
                else
                {
                    lvl.LastLetter = letter;
                    lvl.LastPeriodIndex = periodIndex;
                }
                price += ts;
            }
        }

        // ── Universal time-based letter calculator ───────────────────────────
        // Uses the actual profile StartTime as anchor — works for RTH, ON, and any
        // session that crosses midnight. Completely independent of bar size.
        private int GetPeriodIndexForTime(DateTime barTime, DateTime sessionStart)
        {
            // Total minutes elapsed since this profile/session started
            double elapsedMins = (barTime - sessionStart).TotalMinutes;
            if (elapsedMins < 0) elapsedMins = 0;

            int minutesPerLetter = TPOMinutes <= 0 ? 30 : TPOMinutes;
            return (int)(elapsedMins / minutesPerLetter);
        }

        private char GetLetterForPeriod(int periodIdx)
        {
            if (periodIdx < 0) periodIdx = 0;
            // Display can safely cycle; TPO uniqueness is PeriodIndex-based, so
            // weekly profiles are no longer capped by the 62-character alphabet.
            return Alphabet[periodIdx % Alphabet.Length];
        }

        private bool AnyPriceAlertEnabled()
        {
            return AlertPOC || AlertVAH || AlertVAL || AlertNakedPOC || AlertVPOC;
        }

        private void InvalidateCompositeCache()
        {
            _compositeDirty = true;
        }

        private void FinalizeClosedProfile(TpoProfileData p, double ts)
        {
            if (p == null) return;
            RecomputeProfileIfDirty(p, ts);
            GetSortedKeys(p);
            if (FreezeClosedProfiles)
            {
                p.IsFinalized = true;
                p.IsDirty = false;
                p.SortedKeysDirty = false;
            }
            InvalidateCompositeCache();
        }

        private bool IsCurrentProfile(TpoProfileData p, bool isON)
        {
            return isON ? object.ReferenceEquals(p, _currentON)
                        : object.ReferenceEquals(p, _currentProfile);
        }

        private List<double> GetSortedKeys(TpoProfileData p)
        {
            if (p.SortedKeysDirty || p.SortedKeys == null || p.SortedKeys.Count != p.Levels.Count)
            {
                p.SortedKeys = new List<double>(p.Levels.Keys);
                p.SortedKeys.Sort();
                p.SortedKeysDirty = false;
            }
            return p.SortedKeys;
        }

        // =====================================================================
        //  POC & VALUE AREA
        // =====================================================================

        private void CalculatePOCAndVA(TpoProfileData p, double ts)
        {
            if (p.Levels.Count == 0) return;

            // Build a price-sorted, index-parallel view of the TPO counts so POC
            // and Value Area are computed deterministically and reproducibly.
            // (Iterating a Dictionary directly gives hash-order results, which can
            //  flip the POC on ties — an auditor red flag. Sorting fixes that.)
            List<double> keys = GetSortedKeys(p);

            double[] counts = new double[keys.Count];
            int total = 0;
            for (int i = 0; i < keys.Count; i++)
            {
                int c       = p.Levels[keys[i]].Count;
                counts[i]   = c;
                total      += c;
            }
            p.TotalTPOs = total;

            int pocIdx = FindControlIndex(keys, counts);
            p.POC      = keys[pocIdx];
            p.MaxCount = (int)counts[pocIdx];

            double vah, val;
            ExpandValueArea(keys, counts, pocIdx, total, out vah, out val);
            p.VAH = vah; p.VAL = val;
        }

        // =====================================================================
        //  CONTROL PRICE (POC/VPOC) — deterministic tie-break
        //  Standard market-profile convention: the Point of Control is the row
        //  with the most TPOs (or most volume); when several rows tie at the
        //  maximum, the one CLOSEST TO THE CENTER of the profile range is chosen.
        //  This matches CME/Dalton "Mind Over Markets" and is fully reproducible.
        // =====================================================================
        private int FindControlIndex(List<double> keysAsc, double[] values)
        {
            if (keysAsc.Count == 0) return 0;

            double maxV = double.MinValue;
            for (int i = 0; i < values.Length; i++)
                if (values[i] > maxV) maxV = values[i];

            double mid      = (keysAsc[0] + keysAsc[keysAsc.Count - 1]) * 0.5;
            int    best     = -1;
            double bestDist = double.MaxValue;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] < maxV - 1e-9) continue;          // not tied for max
                double d = Math.Abs(keysAsc[i] - mid);
                if (d < bestDist) { bestDist = d; best = i; }   // closest-to-center wins
            }
            return best < 0 ? 0 : best;
        }

        // =====================================================================
        //  VALUE AREA EXPANSION — canonical CME/Dalton "two-TPO" method
        //  Starting at the control row, repeatedly compare the SUM of the next two
        //  rows above the current value-area top against the SUM of the next two
        //  rows below the bottom, and annex whichever pair is larger. Continue
        //  until ValueAreaPercent (default 70%) of the total is enclosed.
        //  Works identically for TPO counts and for volume (shared by both).
        // =====================================================================
        private void ExpandValueArea(List<double> keysAsc, double[] values, int pocIdx,
                                     double total, out double vah, out double val)
        {
            vah = keysAsc[pocIdx];
            val = keysAsc[pocIdx];
            if (total <= 0) return;

            double target = total * (ValueAreaPercent / 100.0);
            double acc    = values[pocIdx];
            int    up     = pocIdx + 1;   // toward HIGHER prices (VAH side)
            int    dn     = pocIdx - 1;   // toward LOWER prices  (VAL side)

            while (acc < target && (up < keysAsc.Count || dn >= 0))
            {
                double upPair = 0; int upRows = 0;
                if (up     < keysAsc.Count) { upPair += values[up];     upRows++; }
                if (up + 1 < keysAsc.Count) { upPair += values[up + 1]; upRows++; }

                double dnPair = 0; int dnRows = 0;
                if (dn     >= 0)            { dnPair += values[dn];     dnRows++; }
                if (dn - 1 >= 0)            { dnPair += values[dn - 1]; dnRows++; }

                bool takeUp;
                if      (upRows == 0) takeUp = false;
                else if (dnRows == 0) takeUp = true;
                else                  takeUp = upPair >= dnPair;   // ties resolve upward (deterministic)

                if (takeUp)
                    for (int k = 0; k < upRows; k++) { acc += values[up]; vah = keysAsc[up]; up++; }
                else
                    for (int k = 0; k < dnRows; k++) { acc += values[dn]; val = keysAsc[dn]; dn--; }
            }
        }

        // =====================================================================
        //  VOLUME-BASED VALUE AREA
        //  Same 70% algorithm but using volume instead of TPO count
        // =====================================================================

        private void CalculateVolumeVA(TpoProfileData p)
        {
            if (p.Levels.Count == 0 || p.MaxVolume <= 0) return;

            // Price-sorted, index-parallel volume view (same determinism as TPO VA).
            List<double> keys = GetSortedKeys(p);

            double[] vols     = new double[keys.Count];
            double   totalVol = 0;
            for (int i = 0; i < keys.Count; i++)
            {
                double v = p.Levels[keys[i]].Volume;
                vols[i]  = v;
                totalVol += v;
            }
            if (totalVol <= 0) return;

            int pocIdx = FindControlIndex(keys, vols);   // deterministic VPOC
            p.VPOC     = keys[pocIdx];

            double vah, val;
            ExpandValueArea(keys, vols, pocIdx, totalVol, out vah, out val);
            p.VolVAH = vah;
            p.VolVAL = val;
        }

        // =====================================================================
        //  DAY TYPE CLASSIFICATION  (Auction Market Theory - Steidlmayer)
        //  Classifies the session profile into one of the standard AMT day types
        //  based on range relative to Initial Balance, profile shape, and close.
        // =====================================================================

        private void ClassifyDayType(TpoProfileData p)
        {
            if (p == null || p.Levels.Count == 0)            { p.DayType = TpoDayType.Unknown; return; }
            if (p.IBH <= 0 || p.IBL <= 0)                    { p.DayType = TpoDayType.Unknown; return; }

            double ibRange     = p.IBH - p.IBL;
            double profileRng  = p.ProfileHigh - p.ProfileLow;
            if (ibRange <= 0 || profileRng <= 0)             { p.DayType = TpoDayType.Unknown; return; }

            double rangeRatio  = profileRng / ibRange;

            // ── Check for Double Distribution ──────────────────────────────
            // Look for runs of consecutive single-print levels with substantial gap
            bool hasDoubleDistribution = DetectDoubleDistribution(p);

            // ── Check for Neutral Day ──────────────────────────────────────
            // Range extends BOTH above IBH and below IBL
            bool extendedUp   = p.ProfileHigh > p.IBH + 0.001;
            bool extendedDown = p.ProfileLow  < p.IBL - 0.001;
            bool isNeutral    = extendedUp && extendedDown;

            // ── Check for Trend Day ────────────────────────────────────────
            // Range > 2x IB AND close near one extreme (top or bottom 20% of range)
            double range20 = profileRng * 0.2;
            double lastBarClose = p.LastClose > 0 ? p.LastClose : ((p.ProfileHigh + p.ProfileLow) * 0.5);
            bool closeNearHigh = lastBarClose >= (p.ProfileHigh - range20);
            bool closeNearLow  = lastBarClose <= (p.ProfileLow  + range20);
            bool isTrend       = rangeRatio > 2.0 && (closeNearHigh || closeNearLow);

            // ── Final classification (priority order) ──────────────────────
            if (isTrend)
                p.DayType = TpoDayType.Trend;
            else if (hasDoubleDistribution)
                p.DayType = TpoDayType.DoubleDistribution;
            else if (isNeutral)
                p.DayType = TpoDayType.Neutral;
            else if (rangeRatio < 0.5)
                p.DayType = TpoDayType.NonTrend;
            else if (rangeRatio <= 1.15)
                p.DayType = TpoDayType.Normal;
            else
                p.DayType = TpoDayType.NormalVariation;
        }

        private bool DetectDoubleDistribution(TpoProfileData p)
        {
            // Double distribution = two clusters of TPO activity separated by a band
            // of consecutive single-print levels (typically 3+ in a row).
            if (p.Levels.Count < 8) return false;

            List<double> keys = new List<double>(p.Levels.Keys);
            keys.Sort();
            keys.Reverse();   // high to low

            // Count consecutive single-print runs that are NOT at the extremes
            double ts = ManualTickSize > 0 ? ManualTickSize : Instrument.MasterInstrument.TickSize;
            int    longestRun = 0;
            int    currentRun = 0;
            for (int i = 1; i < keys.Count - 1; i++)
            {
                if (p.Levels[keys[i]].Count == 1)
                {
                    currentRun++;
                    if (currentRun > longestRun) longestRun = currentRun;
                }
                else
                {
                    currentRun = 0;
                }
            }

            // 3+ consecutive single prints in the middle suggests double distribution
            return longestRun >= 3;
        }

        private string DayTypeToText(TpoDayType dt)
        {
            switch (dt)
            {
                case TpoDayType.NonTrend:           return "NON-TREND";
                case TpoDayType.Normal:             return "NORMAL";
                case TpoDayType.NormalVariation:    return "NORMAL VAR";
                case TpoDayType.Trend:              return "TREND";
                case TpoDayType.DoubleDistribution: return "DBL DIST";
                case TpoDayType.Neutral:            return "NEUTRAL";
                default:                            return "---";
            }
        }

        public override void OnRenderTargetChanged()
        {
            DisposeResources();
            if (RenderTarget != null) CreateResources();
        }

        private SharpDX.Direct2D1.SolidColorBrush WpfToDx(
            System.Windows.Media.Brush wpf, float alpha)
        {
            System.Windows.Media.SolidColorBrush scb = wpf as System.Windows.Media.SolidColorBrush;
            if (scb == null)
                return new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new Color4(0.5f,0.5f,0.5f,alpha));
            System.Windows.Media.Color c = scb.Color;
            return new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                new Color4(c.R/255f, c.G/255f, c.B/255f, alpha));
        }

        private void CreateResources()
        {
            _dxPoc       = WpfToDx(PocColor,          1f);
            _dxVah       = WpfToDx(VahColor,          1f);
            _dxVal       = WpfToDx(ValColor,          1f);
            _dxOnVah     = WpfToDx(ONVahColor,        1f);
            _dxOnVal     = WpfToDx(ONValColor,        1f);
            _dxVaFill    = WpfToDx(VAFillColor,       VAFillOpacity    / 100f);
            _dxIBFill    = WpfToDx(IBColor,           IBOpacity        / 100f);
            _dxIBBorder  = WpfToDx(IBBorderColor,     IBOpacity        / 100f);
            _dxTpo       = WpfToDx(TpoColor,          1f);
            _dxTpoIB     = WpfToDx(TpoIBColor,        1f);
            _dxTpoVA     = WpfToDx(TpoVAColor,        1f);
            // Modern cell bevel: a near-white highlight and a near-black shadow,
            // drawn at low opacity around each TPO tile. Opacity is modulated per
            // cell at draw time, so base alpha here is just a sane default.
            _dxCellHi    = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new Color4(1f, 1f, 1f, 1f));
            _dxCellLo    = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new Color4(0f, 0f, 0f, 1f));
            _dxNakedPoc  = WpfToDx(NakedPocColor,     1f);
            _dxBg        = WpfToDx(ProfileBackground, BackgroundOpacity / 100f);
            _dxOnBg      = WpfToDx(ONBackground,      ONOpacity         / 100f);
            _dxOnTpo     = WpfToDx(ONTpoColor,        1f);
            _dxComposite = WpfToDx(CompositeColor,    CompositeOpacity  / 100f);
            _dxCompositeVA = WpfToDx(CompositeVAColor, CompositeOpacity  / 100f);
            _dxOr        = WpfToDx(ORColor,           ORFillOpacity     / 100f);
            _dxOrFill    = WpfToDx(ORFillColor,       ORFillOpacity     / 100f);
            _dxVolBar    = WpfToDx(VolBarColor,       VolBarOpacity     / 100f);
            _dxVolVA     = WpfToDx(VolVAColor,        VolBarOpacity     / 100f);
            _dxVolPoc    = WpfToDx(VolPocColor,       1f);
            _dxOnVaFill  = WpfToDx(ONVahColor,        15f / 100f); // reuse ON color at low opacity

            _dxStatBg   = WpfToDx(StatsBgColor,   StatsBgOpacity / 100f);
            _dxStatText = WpfToDx(StatsTextColor, 1f);
            _dxPoorHL      = WpfToDx(PoorHLColor,      1f);
            _dxSinglePrint = WpfToDx(SinglePrintColor, SinglePrintOpacity / 100f);
            _dxDeltaBuy    = WpfToDx(DeltaBuyColor,    DeltaOpacity       / 100f);
            _dxDeltaSell   = WpfToDx(DeltaSellColor,   DeltaOpacity       / 100f);
            _dxDeltaZero   = WpfToDx(DeltaZeroColor,   1f);
            _dxBtnOn       = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new Color4(0.2f, 0.55f, 0.85f, 0.95f));
            _dxBtnOff      = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new Color4(0.2f, 0.2f, 0.2f, 0.85f));
            _dxBtnBorder   = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new Color4(0.7f, 0.7f, 0.7f, 1.0f));

            SharpDX.DirectWrite.Factory dw = Core.Globals.DirectWriteFactory;
            float fs = LetterFontSize > 4f ? LetterFontSize : 9f;

            _fmtLetter = new TextFormat(dw, "Consolas", fs);
            _fmtLetter.TextAlignment = TextAlignment.Leading;
            _fmtLetter.ParagraphAlignment = ParagraphAlignment.Center;

            // Modern cell mode: centered + bold, size driven by CellLetterScale so the
            // user can tune how big the glyph is inside each tile.
            float fsCell = fs * (float)(CellLetterScale > 0.1 ? CellLetterScale : 1.25);
            _fmtCellLetter = new TextFormat(dw, "Consolas", null,
                FontWeight.Bold, FontStyle.Normal, FontStretch.Normal, fsCell, "");
            _fmtCellLetter.TextAlignment = TextAlignment.Center;
            _fmtCellLetter.ParagraphAlignment = ParagraphAlignment.Center;

            float statSize = StatsFontSize > 6f ? StatsFontSize : 12f;
            _fmtStat = new TextFormat(dw, "Consolas", statSize);
            _fmtStat.TextAlignment = TextAlignment.Leading;
            _fmtStat.ParagraphAlignment = ParagraphAlignment.Near;

            _fmtLabel = new TextFormat(dw, "Consolas", null,
                FontWeight.Bold, FontStyle.Normal, FontStretch.Normal, 11f, "");
            _fmtLabel.TextAlignment = TextAlignment.Leading;
            _fmtLabel.ParagraphAlignment = ParagraphAlignment.Center;

            _fmtLabelCtr = new TextFormat(dw, "Consolas", null,
                FontWeight.Bold, FontStyle.Normal, FontStretch.Normal, 11f, "");
            _fmtLabelCtr.TextAlignment = TextAlignment.Center;
            _fmtLabelCtr.ParagraphAlignment = ParagraphAlignment.Center;

            // Cached dashed stroke (reused by every VA / POC dashed line each frame)
            StrokeStyleProperties dashProps = new StrokeStyleProperties();
            dashProps.DashStyle = SharpDX.Direct2D1.DashStyle.Dash;
            _dashStyle = new StrokeStyle(RenderTarget.Factory, dashProps);

            StrokeStyleProperties dotProps = new StrokeStyleProperties();
            dotProps.DashStyle = SharpDX.Direct2D1.DashStyle.Dot;
            _dotStyle = new StrokeStyle(RenderTarget.Factory, dotProps);

            StrokeStyleProperties dashDotProps = new StrokeStyleProperties();
            dashDotProps.DashStyle = SharpDX.Direct2D1.DashStyle.DashDot;
            _dashDotStyle = new StrokeStyle(RenderTarget.Factory, dashDotProps);

            // Cached stats-panel border brush (was allocated/disposed every frame)
            _dxPanelBorder = new SharpDX.Direct2D1.SolidColorBrush(
                RenderTarget, new Color4(0.3f, 0.7f, 1f, 0.6f));
        }

        private void DisposeResources()
        {
            DisposeDx(ref _dxPoc);       DisposeDx(ref _dxVah);      DisposeDx(ref _dxVal);
            DisposeDx(ref _dxOnVah);     DisposeDx(ref _dxOnVal);    DisposeDx(ref _dxVaFill);
            DisposeDx(ref _dxIBFill);    DisposeDx(ref _dxIBBorder); DisposeDx(ref _dxTpo);      DisposeDx(ref _dxTpoIB);
            DisposeDx(ref _dxTpoVA);     DisposeDx(ref _dxNakedPoc);  DisposeDx(ref _dxBg);       DisposeDx(ref _dxOnBg);
            DisposeDx(ref _dxCellHi);    DisposeDx(ref _dxCellLo);
            DisposeDx(ref _dxOnTpo);     DisposeDx(ref _dxComposite); DisposeDx(ref _dxCompositeVA);
            DisposeDx(ref _dxOr);
            DisposeDx(ref _dxOrFill);    DisposeDx(ref _dxVolBar);   DisposeDx(ref _dxVolVA);
            DisposeDx(ref _dxVolPoc);    DisposeDx(ref _dxOnVaFill); DisposeDx(ref _dxStatBg);
            DisposeDx(ref _dxStatText);  DisposeDx(ref _dxPoorHL);   DisposeDx(ref _dxSinglePrint);
            DisposeDx(ref _dxDeltaBuy);  DisposeDx(ref _dxDeltaSell); DisposeDx(ref _dxDeltaZero);
            DisposeDx(ref _dxBtnOn);     DisposeDx(ref _dxBtnOff);    DisposeDx(ref _dxBtnBorder);
            DisposeFmt(ref _fmtLetter);  DisposeFmt(ref _fmtStat);   DisposeFmt(ref _fmtLabel);
            DisposeFmt(ref _fmtCellLetter);
            DisposeFmt(ref _fmtLabelCtr);
            DisposeDx(ref _dxPanelBorder);
            if (_dashStyle != null) { _dashStyle.Dispose(); _dashStyle = null; }
            if (_dotStyle != null) { _dotStyle.Dispose(); _dotStyle = null; }
            if (_dashDotStyle != null) { _dashDotStyle.Dispose(); _dashDotStyle = null; }
        }

        private void DisposeDx(ref SharpDX.Direct2D1.SolidColorBrush b)
        { if (b != null) { b.Dispose(); b = null; } }

        private void DisposeFmt(ref TextFormat f)
        { if (f != null) { f.Dispose(); f = null; } }

        // =====================================================================
        //  X-COORDINATE (pure math, no NT8 GetXByBarIdx)
        // =====================================================================

        // True when profile p is the current (in-formation) RTH session or the
        // immediately previous one. Used to limit Volume VA/VPOC extension to
        // "yesterday + today" as requested. Overnight profiles never qualify.
        private bool IsCurrentOrPreviousDay(TpoProfileData p)
        {
            if (p == null) return false;

            if (p.IsOvernight)
            {
                int m = _overnight.Count;
                if (m == 0) return false;
                if (m >= 1 && ReferenceEquals(p, _overnight[m - 1])) return true;   // tonight
                if (m >= 2 && ReferenceEquals(p, _overnight[m - 2])) return true;   // last night
                return false;
            }

            int n = _profiles.Count;
            if (n == 0) return false;
            // _profiles is oldest-first; the last is the current session.
            if (n >= 1 && ReferenceEquals(p, _profiles[n - 1])) return true;   // today
            if (n >= 2 && ReferenceEquals(p, _profiles[n - 2])) return true;   // yesterday
            return false;
        }

        private void UpdateSlotWidth()
        {
            _slotW = (float)ChartControl.Properties.BarDistance;
            if (_slotW < 1f) _slotW = 1f;
        }

        // =====================================================================
        //  ZOOM-AWARE PROFILE WIDTH
        //  Returns the per-column (per-TPO) pixel width for a given session.
        //  When ScaleWidthWithZoom is ON, the whole profile is sized so that its
        //  horizontal extent (maxCols columns) fills a fraction of the session's
        //  on-chart bar span — so it grows when you zoom in and shrinks when you
        //  zoom out, staying glued to its time location. Clamped to keep letters
        //  legible (min) and prevent a thin session from exploding (max).
        //  When OFF, falls back to the classic fixed width (LetterFontSize based).
        // =====================================================================
        private float ColumnWidth(TpoProfileData p, int maxCols)
        {
            // The column width is a function of horizontal geometry ONLY: the visible
            // bar range, the bar distance (zoom), and maxCols. We key a persistent
            // cache on exactly those. While the horizontal state is unchanged — which
            // is the case throughout a price (Y) axis drag — every repaint returns the
            // identical cached width, so the profile's X extent never moves when you
            // drag the price scale.
            int curFrom = 0, curTo = 0;
            try { curFrom = ChartBars.FromIndex; curTo = ChartBars.ToIndex; } catch { }

            if (p.HWidthValue > 0f
                && p.HWidthKeyFrom == curFrom
                && p.HWidthKeyTo   == curTo
                && p.HWidthKeySlot == _slotW
                && p.HWidthKeyCols == maxCols)
                return p.HWidthValue;

            float fixedLw = LetterFontSize * 0.65f;
            if (fixedLw < 5f) fixedLw = 5f;

            float result;
            if (!ScaleWidthWithZoom || maxCols <= 0)
            {
                result = fixedLw;
            }
            else
            {
                // Use the THEORETICAL session window in pixels — not the current bar
                // span — so an in-progress session is sized for the full slot it will
                // eventually occupy and never invades the next session.
                float xWinStart, xWinEnd;
                GetSessionWindowPx(p, out xWinStart, out xWinEnd);
                float spanPx = xWinEnd - xWinStart;
                if (spanPx < _slotW) spanPx = _slotW;

                // Quantize the span to a coarse grid so sub-pixel fluctuations in the
                // time→X projection (transient repaints during a Y drag) can't nudge
                // the width frame to frame.
                spanPx = (float)(Math.Round(spanPx / 2.0) * 2.0);
                if (spanPx < _slotW) spanPx = _slotW;

                float targetW = spanPx * (ProfileWidthPct / 100f);
                float lw      = targetW / maxCols;

                float minLw = 0.10f;
                float maxLw = fixedLw;
                if (lw < minLw) lw = minLw;
                if (lw > maxLw) lw = maxLw;
                result = lw;
            }

            // Commit to the persistent horizontal-state cache.
            p.HWidthKeyFrom = curFrom;
            p.HWidthKeyTo   = curTo;
            p.HWidthKeySlot = _slotW;
            p.HWidthKeyCols = maxCols;
            p.HWidthValue   = result;
            return result;
        }

        // Horizontal offset (in px) from the bar-start anchor to where the
        // secondary profiles (Volume / Delta) should begin. When the TPO profile
        // is hidden, they collapse to the left limit (offset 0) so they sit at the
        // session's bar start instead of floating where the TPO used to be.
        private float SecondaryProfileOffset(float profileWidth)
        {
            if (!_showTpo) return 0f;          // TPO hidden → pin to left limit
            return profileWidth;
        }

        // Returns a STABLE vertical row-merge factor for a profile, given the current
        // pixels-per-tick. The naive formula ceil(2/tickPx) flips between values when
        // tickPx wobbles around a threshold (e.g. ~0.875px), which is exactly what
        // happens while you drag the price (Y) axis: rows alternate between "normal"
        // and "grouped/large" every frame — the flicker the user sees.
        //
        // Fix: hysteresis. We only change the committed merge when tickPx moves a
        // clear margin past the boundary that would justify the change, and we keep
        // the previous merge inside the dead-band. This makes the row sizing rock-
        // steady during a Y drag while still adapting on a real, sustained zoom.
        private int StableMerge(TpoProfileData p, float tickPx)
        {
            int prevMerge = p.LastMerge;
            float prevTick = p.LastMergeTickPx;
            int result = StableMergeCore(tickPx, ref prevMerge, ref prevTick);
            p.LastMerge = prevMerge;
            p.LastMergeTickPx = prevTick;
            return result;
        }

        // Core hysteresis logic shared by per-profile and composite mergers.
        private int StableMergeCore(float tickPx, ref int lastMerge, ref float lastTickPx)
        {
            if (tickPx < 0.05f) tickPx = 0.05f;

            int target = (tickPx >= 1.5f) ? 1 : (int)Math.Ceiling(2.0 / tickPx);
            if (target < 1) target = 1;

            int prev = lastMerge < 1 ? 1 : lastMerge;

            if (lastTickPx < 0f)
            {
                lastMerge = target;
                lastTickPx = tickPx;
                return target;
            }

            // Dead-band: keep the previous merge unless tickPx moved a clear margin.
            float lo = lastTickPx * 0.82f;
            float hi = lastTickPx * 1.18f;
            if (tickPx >= lo && tickPx <= hi)
                return prev;

            lastMerge = target;
            lastTickPx = tickPx;
            return target;
        }

        // Scales a fixed panel width (Volume / Delta) with chart zoom when
        // ScaleWidthWithZoom is on, so those panels grow/shrink together with the
        // TPO profile instead of staying a constant pixel width. Uses the session
        // bar span as the reference, like ColumnWidth, and clamps to sane bounds.
        private float ScaledPanelWidth(TpoProfileData p, float fixedWidth)
        {
            if (!ScaleWidthWithZoom) return fixedWidth;

            // Same session-window span as ColumnWidth so TPO/VP/Delta stay in
            // sync as the chart zooms and so the secondary panels never invade
            // the next session either.
            float xWinStart, xWinEnd;
            GetSessionWindowPx(p, out xWinStart, out xWinEnd);
            float spanPx = xWinEnd - xWinStart;
            if (spanPx < _slotW) spanPx = _slotW;

            // Target the panel at the same fraction of the session span used for
            // the TPO profile, so volume/delta visually track the TPO width.
            float target = spanPx * (ProfileWidthPct / 100f);

            // Panel-width clamps:
            //   minW — keep readable bars even on extreme zoom-out (the session-
            //          discipline cap in the drawing code may shrink it further
            //          to avoid invading the next session, that's expected).
            //   maxW — capped at the user-configured fixed width. Going beyond
            //          would mean the panel is wider than what the user asked
            //          for in properties (VolProfileWidth / DeltaProfileWidth),
            //          which feels wrong and crowds the chart.
            float minW = 12f;
            float maxW = fixedWidth;
            if (target < minW) target = minW;
            if (target > maxW) target = maxW;
            return target;
        }

        // Returns left edge pixel (in render coords) of a bar slot.
        // For bars within the visible chart range, uses NT8's native GetXByBarIndex
        // which handles DPI scaling, padding, and scroll position correctly.
        // For off-screen bars, extrapolates from the first visible bar using slot width.
        // This keeps profiles anchored to their real time location — they slide off
        // the visible area naturally when scrolling, instead of bunching at the edge.
        private float BarToX(int absBarIdx)
        {
            // Bar is within visible chart range — use NT8's accurate native method
            if (absBarIdx >= ChartBars.FromIndex && absBarIdx <= ChartBars.ToIndex)
            {
                try { return ChartControl.GetXByBarIndex(ChartBars, absBarIdx); }
                catch { /* fall through to math */ }
            }

            // Bar is off-screen — extrapolate from a known anchor.
            // Use the first visible bar as the anchor and project using slot width.
            // This gives correct (possibly negative) X for bars to the left, and
            // X > ChartPanel.W for bars to the right.
            try
            {
                int   anchorIdx = ChartBars.FromIndex;
                float anchorX   = ChartControl.GetXByBarIndex(ChartBars, anchorIdx);
                int   offset    = absBarIdx - anchorIdx;
                return anchorX + offset * _slotW;
            }
            catch
            {
                // Last-resort fallback
                int offset = absBarIdx - ChartBars.FromIndex;
                return ChartPanel.X + offset * _slotW;
            }
        }

        // True if any part of a profile spanning [firstBar, lastBar] is visible
        // in the current chart viewport. Used to skip drawing fully off-screen profiles.
        private bool IsProfileVisible(int firstBar, int lastBar, float extraPx)
        {
            return IsProfileVisible(firstBar, lastBar, extraPx, false);
        }

        // Overload that accounts for VA/POC lines extended to the right edge.
        // When `extendedToRightEdge` is true, the profile's VA/POC lines run all
        // the way to the chart's right edge, so they are visible whenever the
        // profile starts at or before the right edge — even if the profile body
        // itself has scrolled off the left side of the viewport. Without this,
        // a profile whose body is off-screen on the left would be culled and its
        // extended lines (which DO cross the visible area) would vanish.
        private bool IsProfileVisible(int firstBar, int lastBar, float extraPx, bool extendedToRightEdge)
        {
            float xL = BarToX(firstBar);
            float leftEdge  = ChartPanel.X;
            float rightEdge = ChartPanel.X + ChartPanel.W;

            if (extendedToRightEdge)
            {
                // Lines reach the right edge. Visible as long as the profile's
                // own start is not to the right of the viewport (i.e. it begins
                // somewhere at or left of the right edge). This keeps extended
                // VA/POC lines on-screen regardless of how far left the body is.
                return xL <= rightEdge;
            }

            float xR = BarToX(lastBar) + _slotW + extraPx;
            return xR >= leftEdge && xL <= rightEdge;
        }


        private bool IsProfileWindowVisible(TpoProfileData p, float extraPx, bool extendedToRightEdge)
        {
            if (p == null) return false;
            float xStart, xEnd;
            GetSessionWindowPx(p, out xStart, out xEnd);

            float leftEdge  = ChartPanel.X;
            float rightEdge = ChartPanel.X + ChartPanel.W;

            if (extendedToRightEdge)
                return xStart <= rightEdge;

            return (xEnd + extraPx) >= leftEdge && xStart <= rightEdge;
        }

        private float GetProfileGap(TpoProfileData p)
        {
            float fixedGap = ProfileGap > 0 ? ProfileGap : 4f;
            if (!ScaleWidthWithZoom || p == null) return fixedGap;

            float xStart, xEnd;
            GetSessionWindowPx(p, out xStart, out xEnd);
            float spanPx = Math.Abs(xEnd - xStart);
            if (spanPx < _slotW) spanPx = _slotW;

            // On compressed Diario/Semanal windows a fixed 25px gap can consume
            // the entire remaining slot. Scale it down with zoom so VP/Delta keep
            // visible room inside the 18:00→17:00 or weekly window.
            float zoomGap = spanPx * 0.03f;
            if (zoomGap < 2f) zoomGap = 2f;
            return Math.Min(fixedGap, zoomGap);
        }

        // Decides whether a profile at list index `idx` (out of `count` total,
        // oldest-first) should render its text labels. Only the last
        // LabelSessionsBack sessions get labels; older ones draw the profile shape
        // without labels to prevent text overlap clutter. 0 = all sessions labeled.
        private bool LabelsAllowed(int idx, int count)
        {
            if (LabelSessionsBack <= 0) return true;
            return idx >= count - LabelSessionsBack;
        }

        // =====================================================================
        //  ON RENDER
        // =====================================================================

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            if (RenderTarget == null || chartControl == null || chartScale == null || ChartPanel == null || ChartBars == null) return;
            if (_fmtLetter == null) CreateResources();

            // Lazy mouse hook fallback only for the legacy SharpDX panel.
            // In WPF mode the panel has its own event handlers; hooking the chart
            // globally adds unnecessary mouse routing pressure and can conflict
            // with other ILT-DEV Suite panels.
            if (!UseWpfPanel && !_mouseHooked) HookMouseEvents();
            else if (UseWpfPanel && _mouseHooked) UnhookMouseEvents();

            UpdateSlotWidth();
            _renderPanelTop = ChartPanel.Y - 24f;
            _renderPanelBot = ChartPanel.Y + ChartPanel.H + 24f;
            _renderPriceMin = Math.Min(chartScale.MinValue, chartScale.MaxValue);
            _renderPriceMax = Math.Max(chartScale.MinValue, chartScale.MaxValue);

            double ts = ManualTickSize > 0
                ? ManualTickSize
                : Instrument.MasterInstrument.TickSize;

            // The profile collections can be rebuilt on the UI thread when a period
            // button is pressed. Take the lock for the whole draw-iteration so we
            // never index a list that is being cleared/repopulated mid-frame.
            // The whole draw is wrapped in try/catch: a single transient SharpDX or
            // scale edge case must never bubble up and disturb the chart or the rest
            // of the ILT-DEV suite sharing this chart.
            try
            {
            lock (_profilesLock)
            {
                // Recompute any profiles whose data changed via OnMarketData between
                // bar updates. In realtime this is throttled because sorting POC/VA on
                // every tick is the main source of chart-lag; the tick data still gets
                // captured immediately and the visual/stat levels refresh on schedule.
                bool allowHeavyRecalc = ShouldRunHeavyRecalc();
                if (allowHeavyRecalc)
                {
                    for (int i = 0; i < _profiles.Count; i++)
                        if (_profiles[i].IsDirty && (!FreezeClosedProfiles || !_profiles[i].IsFinalized))
                            RecomputeProfileIfDirty(_profiles[i], ts);
                    for (int i = 0; i < _overnight.Count; i++)
                        if (_overnight[i].IsDirty && (!FreezeClosedProfiles || !_overnight[i].IsFinalized))
                            RecomputeProfileIfDirty(_overnight[i], ts);
                }

                // Overnight profiles (behind RTH). Drawn whenever overnight is on;
                // the TPO letters themselves are gated by _showTpo inside the method,
                // but IB/OR/VA/POC must render independently of the TPO toggle.
                if (ShowOvernight)
                    for (int i = 0; i < _overnight.Count; i++)
                        if (_overnight[i].Levels.Count > 0)
                        {
                            _labelsOnForProfile = LabelsAllowed(i, _overnight.Count);
                            DrawProfileGeneric(_overnight[i], chartScale, ts, true);
                        }

                // RTH profiles — always processed so IB/OR/VA/POC draw even when the
                // TPO letters are toggled off.
                for (int i = 0; i < _profiles.Count; i++)
                    if (_profiles[i].Levels.Count > 0)
                    {
                        _labelsOnForProfile = LabelsAllowed(i, _profiles.Count);
                        DrawProfileGeneric(_profiles[i], chartScale, ts, false);
                        if (_showTpo && ShouldDrawProfileDetailMarkers(_profiles[i], false, chartScale, ts))
                        {
                            if (ShowPoorHL)       DrawPoorHL(_profiles[i], chartScale, ts);
                            if (ShowSinglePrints) DrawSinglePrints(_profiles[i], chartScale, ts);
                        }
                    }
                _labelsOnForProfile = true;  // restore for composite / naked POC / etc.

                // Volume Profile pinned to right side
                if (ShowVolumeProfile && _showVolBtn) DrawVolumeProfile(chartScale);

                // Delta Profile (next to volume profile)
                if (ShowDeltaProfile && _showDeltaBtn) DrawDeltaProfile(chartScale);

                // Composite pinned to right edge
                if (ShowComposite && _profiles.Count >= 2)
                    DrawCompositeRightEdge(chartScale, ts);

                // Naked POCs
                if (ShowNakedPOC) DrawNakedPOCs(chartScale);
            }   // end lock (_profilesLock)

            // Stats panel.
            //  - WPF mode: update the floating panel's text (it draws itself).
            //  - Legacy mode: draw the SharpDX panel + buttons on the canvas.
            if (UseWpfPanel)
            {
                // If centering is still pending (chart width wasn't ready at build),
                // retry now that OnRender guarantees a valid ChartPanel size.
                if (_wpfBuilt && !_wpfPosInitialized) PlacePanel();

                if (_wpfBuilt && _wpfPanel != null)
                {
                    // Visibility is the AND of: config (ShowStatsPanel) and user
                    // intent (_panelUserHidden via the toolbar button). The
                    // toolbar can hide the panel even if the config says shown;
                    // the config can hide it regardless of the toolbar.
                    var vis = (ShowStatsPanel && !_panelUserHidden)
                        ? System.Windows.Visibility.Visible
                        : System.Windows.Visibility.Collapsed;
                    if (_wpfPanel.Visibility != vis) _wpfPanel.Visibility = vis;
                }
                if (ShowStatsPanel && !_panelUserHidden && _profiles.Count > 0)
                {
                    int nowStats = Environment.TickCount;
                    // WPF layout is not part of the profile engine. Updating the
                    // text block every render pass can create UI-thread churn when
                    // Heatmap, Footprint and Flow Speed are running together.
                    if (_lastWpfStatsUpdateMs == -100000000 || unchecked(nowStats - _lastWpfStatsUpdateMs) >= 250)
                    {
                        _lastWpfStatsUpdateMs = nowStats;
                        UpdateWpfPanelContent();
                    }
                }
            }
            else if (ShowStatsPanel && _profiles.Count > 0)
            {
                DrawStatsPanel();
            }
            }
            catch (Exception ex)
            {
                if (DebugLog) Print("ILT-DEV-PROFILES OnRender error: " + ex.Message);
            }
        }

        private bool ShouldRunHeavyRecalc()
        {
            if (!OptimizeHistoricalLoad) return true;
            if (State != State.Realtime) return true;

            int interval = RealtimeRecalcMs;
            if (interval <= 0) return true;

            int now = Environment.TickCount;
            if (_lastRealtimeRecalcMs == 0 || unchecked(now - _lastRealtimeRecalcMs) >= interval)
            {
                _lastRealtimeRecalcMs = now;
                return true;
            }
            return false;
        }

        private bool ShouldRunCompositeRecalc()
        {
            if (!OptimizeHistoricalLoad) return true;
            if (State != State.Realtime) return true;

            int interval = RealtimeRecalcMs;
            if (interval <= 0) return true;

            int now = Environment.TickCount;
            if (_lastCompositeRecalcMs == -100000000 || unchecked(now - _lastCompositeRecalcMs) >= interval)
            {
                _lastCompositeRecalcMs = now;
                return true;
            }
            return false;
        }

        private TpoProfileVisualMode ResolveProfileVisualMode(TpoProfileData p, bool isON, float columnWidth, float tickPx)
        {
            bool isCurrent = IsCurrentProfile(p, isON);
            TpoProfileVisualMode configured = isCurrent ? CurrentSessionVisualMode : HistoricalVisualMode;

            // A user-chosen FIXED mode (Letters/Blocks/Silhouette/Minimal) always
            // wins. It must NOT be downgraded by the LOD thresholds when the price
            // axis is compressed or dragged. Only Auto derives a base mode and lets
            // the LOD thresholds adjust it.
            if (configured != TpoProfileVisualMode.Auto)
            {
                p.LastVisualMode = configured;
                return configured;
            }

            // Mode is Auto: derive the natural base mode for this profile.
            TpoProfileVisualMode baseMode;
            if (isCurrent)
                baseMode = (RenderMode == TpoRenderMode.Letters) ? TpoProfileVisualMode.Letters : TpoProfileVisualMode.Blocks;
            else
                baseMode = TpoProfileVisualMode.Silhouette;

            // With Auto mode, LOD only kicks in if AutoLevelOfDetail is also on.
            if (!AutoLevelOfDetail)
            {
                p.LastVisualMode = baseMode;
                return baseMode;
            }

            // ── HYSTERESIS ───────────────────────────────────────────────────
            // Dragging the Y axis nudges tickPx (pixels per tick) up and down by a
            // fraction of a pixel every frame. If the mode switched the instant
            // tickPx crossed a hard threshold, the profile would oscillate between
            // Letters/Blocks/Silhouette on consecutive frames — the resize/flicker
            // the user reported. We add a dead-band around every threshold and only
            // commit a mode CHANGE when tickPx is clearly past it, biased by the
            // direction we'd be moving relative to the previously committed mode.
            // Inside the dead-band we keep the last committed mode, so a small Y
            // drag produces a perfectly stable picture.
            const float HYST = 0.18f;   // ±18% dead-band around each threshold
            TpoProfileVisualMode prev = p.LastVisualMode;

            // Helper thresholds (base values), then widened by hysteresis depending
            // on whether the previous frame was already at/below that detail level.
            float minLetterRowPx = Math.Max(5f, LetterFontSize * 0.7f);
            float colSilhouette  = 3.0f;
            float colLetterBlock = LetterFontSize * 0.55f;
            float colHistSil     = 5.0f;
            float minimalTick    = 0.30f;

            // Directional thresholds: when we are already in a lower-detail mode,
            // require a clearly larger value to climb back up (×(1+HYST)); when we
            // are in a higher-detail mode, require a clearly smaller value to drop
            // (×(1-HYST)). This is what removes the per-frame toggling.
            bool prevIsMinimal    = prev == TpoProfileVisualMode.Minimal;
            bool prevIsSilhouette = prev == TpoProfileVisualMode.Silhouette;
            bool prevIsBlocks     = prev == TpoProfileVisualMode.Blocks;

            float minimalDrop = minimalTick * (1f - HYST);
            float minimalRise = minimalTick * (1f + HYST);
            if (tickPx < (prevIsMinimal ? minimalRise : minimalDrop))
            {
                p.LastVisualMode = TpoProfileVisualMode.Minimal;
                return TpoProfileVisualMode.Minimal;
            }

            // Letters need vertical room: if the row is shorter than a readable
            // glyph, Letters -> Blocks. Hysteresis: once in Blocks, require a taller
            // row before going back to Letters.
            float rowDrop = minLetterRowPx * (1f - HYST);
            float rowRise = minLetterRowPx * (1f + HYST);
            if (baseMode == TpoProfileVisualMode.Letters &&
                tickPx < (prevIsBlocks ? rowRise : rowDrop))
                baseMode = TpoProfileVisualMode.Blocks;

            // Column-width based downgrades (horizontal; independent of the Y axis
            // but still hysteresis-guarded for zoom stability).
            float silDrop = colSilhouette * (1f - HYST);
            float silRise = colSilhouette * (1f + HYST);
            if (columnWidth < (prevIsSilhouette ? silRise : silDrop))
            {
                p.LastVisualMode = TpoProfileVisualMode.Silhouette;
                return TpoProfileVisualMode.Silhouette;
            }

            if (baseMode == TpoProfileVisualMode.Letters &&
                columnWidth < (prevIsBlocks ? colLetterBlock * (1f + HYST) : colLetterBlock * (1f - HYST)))
                baseMode = TpoProfileVisualMode.Blocks;

            if (!isCurrent &&
                columnWidth < (prevIsSilhouette ? colHistSil * (1f + HYST) : colHistSil * (1f - HYST)))
            {
                p.LastVisualMode = TpoProfileVisualMode.Silhouette;
                return TpoProfileVisualMode.Silhouette;
            }

            p.LastVisualMode = baseMode;
            return baseMode;
        }

        private bool ShouldDrawProfileDetailMarkers(TpoProfileData p, bool isON, ChartScale cs, double ts)
        {
            if (isON) return false;
            if (DetailMarkersOnlyCurrent && !IsCurrentProfile(p, isON)) return false;
            if (p.Levels.Count == 0 || p.ProfileHigh <= p.ProfileLow) return false;

            // Read the mode already committed for this profile in DrawProfileGeneric
            // (called earlier this frame). Re-resolving here with a different tickPx
            // clamp could disagree and reintroduce flicker, so we trust the cache.
            TpoProfileVisualMode vm = p.LastVisualMode;
            return vm == TpoProfileVisualMode.Letters || vm == TpoProfileVisualMode.Blocks;
        }

        // =====================================================================
        //  DRAW PROFILE (RTH + Overnight unified)
        // =====================================================================

        private void DrawProfileGeneric(TpoProfileData p, ChartScale cs, double ts, bool isON)
        {
            // Guard against empty / un-initialized profiles
            if (p.Levels.Count == 0) return;
            if (p.ProfileHigh <= p.ProfileLow) return;

            // Skip profiles fully outside the visible chart area.
            // Extended elements (IB/OR extension, label width) get an extra 200px margin.
            // When VA/POC extension is ON for this session type, the lines run to
            // the chart's right edge, so use the extend-aware cull to keep them
            // visible even when the profile body has scrolled off the left.
            bool tpoExtendOn = ExtendVaPoc && (isON ? _extTpoEthBtn : _extTpoRthBtn);
            if (!IsProfileWindowVisible(p, 200f, tpoExtendOn)) return;

            // Profile anchors at the THEORETICAL session start (not at the first
            // bar that arrived). This pins each session to its time slot — RTH to
            // 09:30, ETH to 18:00 of the previous evening — so a session in
            // formation never invades the next session as it grows.
            float xWinStart, xWinEnd;
            GetSessionWindowPx(p, out xWinStart, out xWinEnd);
            float xAnchor = xWinStart;
            float xEnd    = xWinEnd;
            float height  = cs.GetYByValue(p.ProfileLow - ts) - cs.GetYByValue(p.ProfileHigh + ts);
            if (height < 0) height = -height;

            // Max column count is cached during POC/VA recompute. Fall back to a
            // one-time scan only before the first dirty pass has produced MaxCount.
            int maxCols = p.MaxCount;
            if (maxCols <= 0)
                foreach (KeyValuePair<double, TpoPriceLevel> kv in p.Levels)
                    if (kv.Value.Count > maxCols) maxCols = kv.Value.Count;

            // Zoom-aware per-column width (grows/shrinks with chart zoom when enabled)
            float lw     = ColumnWidth(p, maxCols);
            float profileWidth = maxCols * lw;
            if (profileWidth < 10f) profileWidth = 10f;

            float rawTickPx = Math.Abs(cs.GetYByValue(0) - cs.GetYByValue(ts));
            float tickPx = rawTickPx;
            if (tickPx < 6f) tickPx = 6f;

            bool rightToLeft = (ProfileOrientation == TpoOrientation.RightToLeft);
            TpoProfileVisualMode visualMode = ResolveProfileVisualMode(p, isON, lw, rawTickPx);

            // Background box: anchored to bar start
            float yTop = cs.GetYByValue(p.ProfileHigh + ts);
            float yBot = cs.GetYByValue(p.ProfileLow  - ts);
            if (yBot < yTop) { float t = yBot; yBot = yTop; yTop = t; }

            SharpDX.Direct2D1.SolidColorBrush bgBrush = isON ? _dxOnBg : _dxBg;
            if (_showTpo && visualMode != TpoProfileVisualMode.Minimal)
                RenderTarget.FillRectangle(
                    new RectangleF(xAnchor, yTop, profileWidth + 4f, yBot - yTop), bgBrush);

            // IB box (RTH only) - drawn same style as Opening Range
            // Detect overlap with OR for combined labels (IBH-ORH / IBL-ORL)
            bool ibhOrhOverlap = ShowOR && _showORBtn && p.ORH > 0 && Math.Abs(p.IBH - p.ORH) < ts * 0.5;
            bool iblOrlOverlap = ShowOR && _showORBtn && p.ORL > 0 && Math.Abs(p.IBL - p.ORL) < ts * 0.5;

            if (!isON && ShowIB && _showIBBtn && p.IBH > 0 && p.IBL > 0)
            {
                float yIBH = cs.GetYByValue(p.IBH);
                float yIBL = cs.GetYByValue(p.IBL);
                // The IB box always cuts exactly at the session end (xEnd = the
                // theoretical session-window end, e.g. 17:00 RTH), respecting the
                // session-discipline contract (no pixel-based right extension that
                // could bleed several hours at zoom-out).
                float ibXEnd = xEnd;

                RenderTarget.FillRectangle(new RectangleF(xAnchor, yIBH, ibXEnd - xAnchor, yIBL - yIBH), _dxIBFill);
                // Border: top & bottom honor IbLineStyle (solid/dash/dot/dashdot);
                // the two vertical sides are drawn solid so the box reads as closed.
                // We intentionally do NOT use DrawRectangle here because its solid
                // outline would paint over the styled top/bottom lines.
                StyledLine(yIBH, xAnchor, ibXEnd, _dxIBBorder, IbOrLineWidth, IbLineStyle);
                StyledLine(yIBL, xAnchor, ibXEnd, _dxIBBorder, IbOrLineWidth, IbLineStyle);
                RenderTarget.DrawLine(new Vector2(xAnchor, yIBH), new Vector2(xAnchor, yIBL), _dxIBBorder, IbOrLineWidth);
                RenderTarget.DrawLine(new Vector2(ibXEnd,  yIBH), new Vector2(ibXEnd,  yIBL), _dxIBBorder, IbOrLineWidth);

                PutLabelWithPrice(ibhOrhOverlap ? "IBH-ORH" : "IBH", p.IBH, ibXEnd + 2f, yIBH);
                PutLabelWithPrice(iblOrlOverlap ? "IBL-ORL" : "IBL", p.IBL, ibXEnd + 2f, yIBL);
            }

            // Overnight VA highlight band (same concept as RTH IB but for ON Value Area)
            if (_showTpo && isON && ShowValueArea && p.VAH > 0 && p.VAL > 0)
            {
                float yONVAH = cs.GetYByValue(p.VAH + ts * 0.5);
                float yONVAL = cs.GetYByValue(p.VAL - ts * 0.5);
                if (yONVAL > yONVAH)
                    RenderTarget.FillRectangle(
                        new RectangleF(xAnchor, yONVAH, profileWidth + 4f, yONVAL - yONVAH), _dxOnVaFill);
            }

            // Opening Range box
            if (!isON && ShowOR && _showORBtn && p.ORH > 0 && p.ORL > 0)
            {
                float yORH   = cs.GetYByValue(p.ORH);
                float yORL   = cs.GetYByValue(p.ORL);
                // OR box always cuts exactly at the session end (xEnd), same
                // session-discipline rule as the IB box above.
                float orXEnd = xEnd;

                RenderTarget.FillRectangle(new RectangleF(xAnchor, yORH, orXEnd - xAnchor, yORL - yORH), _dxOrFill);
                // Top & bottom honor OrLineStyle; vertical sides solid (see IB note).
                StyledLine(yORH, xAnchor, orXEnd, _dxOr, IbOrLineWidth, OrLineStyle);
                StyledLine(yORL, xAnchor, orXEnd, _dxOr, IbOrLineWidth, OrLineStyle);
                RenderTarget.DrawLine(new Vector2(xAnchor, yORH), new Vector2(xAnchor, yORL), _dxOr, IbOrLineWidth);
                RenderTarget.DrawLine(new Vector2(orXEnd,  yORH), new Vector2(orXEnd,  yORL), _dxOr, IbOrLineWidth);

                // Skip ORH/ORL labels if already drawn combined with IBH/IBL above
                bool orhDrawnByIB = ShowIB && _showIBBtn && ibhOrhOverlap;
                bool orlDrawnByIB = ShowIB && _showIBBtn && iblOrlOverlap;
                if (!orhDrawnByIB) PutLabelWithPrice("ORH", p.ORH, orXEnd + 2f, yORH);
                if (!orlDrawnByIB) PutLabelWithPrice("ORL", p.ORL, orXEnd + 2f, yORL);
            }

            // TPO render layer (gated by the TPO toggle; IB/OR/VA/POC below
            // are drawn regardless so they remain visible with letters hidden).
            if (_showTpo && visualMode != TpoProfileVisualMode.Minimal)
            {
                List<double> renderKeys = GetSortedKeys(p);
                // Use a generous off-screen cull margin so rows near the top/bottom
                // edge don't pop in and out as the Y axis is dragged. A tight 4px
                // margin made objects flicker right at the panel border; 24px (the
                // same cushion used elsewhere in this render pass) keeps a smooth
                // transition while still skipping rows that are truly off-screen.
                float panelTop = ChartPanel.Y - 24f;
                float panelBot = ChartPanel.Y + ChartPanel.H + 24f;

                if (visualMode == TpoProfileVisualMode.Silhouette)
                {
                    // Modern historical mode: one lightweight silhouette bar per
                    // visible price group. It keeps the real TPO shape and value
                    // structure but avoids drawing hundreds/thousands of letters.
                    //
                    // STABILITY: the row-merge factor is decided through StableMerge()
                    // (hysteresis), so dragging the price (Y) axis up/down doesn't make
                    // the silhouette re-group its bars and flip sizes every frame.
                    float stTickPx = rawTickPx < 0.05f ? 0.05f : rawTickPx;
                    int merge = StableMerge(p, stTickPx);

                    int kIdx = 0;
                    while (kIdx < renderKeys.Count)
                    {
                        double topPrice = renderKeys[kIdx];
                        double botPrice = topPrice;
                        int maxCntInGroup = 0;
                        bool groupInVA = false;
                        bool groupIB = false;

                        for (int m = 0; m < merge && (kIdx + m) < renderKeys.Count; m++)
                        {
                            double key = renderKeys[kIdx + m];
                            TpoPriceLevel lvl = p.Levels[key];
                            if (lvl.Count > maxCntInGroup) maxCntInGroup = lvl.Count;
                            if (key >= p.VAL && key <= p.VAH) groupInVA = true;
                            if (!groupIB && LetterColorMode == TpoLetterColoring.IBHighlight)
                            {
                                for (int j = 0; j < lvl.Letters.Count; j++)
                                    if (lvl.Letters[j].IsIB) { groupIB = true; break; }
                            }
                            botPrice = key;
                        }
                        kIdx += merge;

                        if (maxCntInGroup <= 0) continue;
                        float yMid = cs.GetYByValue((topPrice + botPrice) * 0.5);
                        if (CullOffscreenPriceLevels && (yMid < panelTop || yMid > panelBot))
                            continue;

                        float rowH = Math.Max(1f, stTickPx * merge);
                        float barW = Math.Max(1f, ((float)maxCntInGroup / Math.Max(1, maxCols)) * profileWidth);
                        float xBlock = rightToLeft ? xAnchor - barW : xAnchor;

                        SharpDX.Direct2D1.SolidColorBrush br;
                        if (groupInVA)
                            br = isON ? _dxOnVah : _dxTpoVA;
                        else if (isON)
                            br = _dxOnTpo;
                        else if (groupIB)
                            br = _dxTpoIB;
                        else
                            br = _dxTpo;

                        RenderTarget.FillRectangle(new RectangleF(xBlock, yMid - rowH * 0.5f, barW, rowH), br);
                    }
                }
                else
                {
                    bool useBlocks = (visualMode == TpoProfileVisualMode.Blocks);
                    for (int kIdx = 0; kIdx < renderKeys.Count; kIdx++)
                    {
                        double key = renderKeys[kIdx];
                        TpoPriceLevel level = p.Levels[key];
                        float yLvl  = cs.GetYByValue(key);
                        if (CullOffscreenPriceLevels && (yLvl < panelTop || yLvl > panelBot))
                            continue;
                        float yTop2 = cs.GetYByValue(key + ts * 0.48);
                        float yBot2 = cs.GetYByValue(key - ts * 0.48);
                        float cellH = yBot2 - yTop2;
                        if (cellH < 1f) cellH = 1f;

                        int  cnt    = level.Count;
                        bool inVA   = (key >= p.VAL && key <= p.VAH);  // works for both RTH and ON

                        if (useBlocks)
                        {
                            // ── MODERN CELL MODE (replaces flat blocks) ───────────
                            // Each TPO is drawn as its own tile, shaded by TIME within
                            // the session (early sub-periods dim, latest bright) and
                            // given a subtle bevel (lighter top/left, darker bottom/
                            // right) so the column reads as stacked tiles like the
                            // modern Sierra-style profile. One FillRectangle + two thin
                            // lines per cell; no per-cell brush allocation (we only
                            // modulate Opacity on cached brushes).
                            int maxPi = p.MaxPeriodIndex > 0 ? p.MaxPeriodIndex : 1;
                            for (int i = 0; i < cnt; i++)
                            {
                                TpoLetterItem tpo = level.Letters[i];
                                float xL = rightToLeft ? xAnchor - (i + 1) * lw : xAnchor + i * lw;

                                SharpDX.Direct2D1.SolidColorBrush br;
                                if (inVA)
                                    br = isON ? _dxOnVah : _dxTpoVA;
                                else if (isON)
                                    br = _dxOnTpo;
                                else if (LetterColorMode == TpoLetterColoring.IBHighlight && tpo.IsIB)
                                    br = _dxTpoIB;
                                else
                                    br = _dxTpo;

                                // Time shading: normalize this TPO's sub-period to
                                // [0..1] and map to an opacity ramp (earliest ~0.45,
                                // latest 1.0). This is the "time-based shading" look.
                                float tNorm = maxPi > 0 ? (float)tpo.PeriodIndex / maxPi : 1f;
                                if (tNorm < 0f) tNorm = 0f; if (tNorm > 1f) tNorm = 1f;
                                float cellOp = 0.45f + 0.55f * tNorm;

                                float prevOp = br.Opacity;
                                br.Opacity = cellOp;
                                // Symmetric inset so each tile reads as a separate,
                                // balanced cell (small uniform gap on all 4 sides)
                                // instead of a solid wall. Scales down on tiny cells.
                                float insetX = lw   >= 6f ? 0.75f : 0f;
                                float insetY = cellH >= 6f ? 0.75f : 0f;
                                float cx = xL + insetX;
                                float cy = yTop2 + insetY;
                                float cwd = lw - insetX * 2f;     if (cwd  < 1f) cwd  = 1f;
                                float chd = cellH - insetY * 2f;  if (chd  < 1f) chd  = 1f;
                                RenderTarget.FillRectangle(
                                    new RectangleF(cx, cy, cwd, chd), br);
                                br.Opacity = prevOp;

                                // Bevel: only when cells are big enough to read it,
                                // otherwise it just adds noise/cost. Uses the inset
                                // rect so the relief frames the actual tile.
                                if (cwd >= 4f && chd >= 4f)
                                {
                                    _dxCellHi.Opacity = 0.35f * cellOp;
                                    _dxCellLo.Opacity = 0.40f;
                                    // top + left = highlight
                                    RenderTarget.DrawLine(new Vector2(cx, cy + 0.5f),
                                        new Vector2(cx + cwd, cy + 0.5f), _dxCellHi, 1f);
                                    RenderTarget.DrawLine(new Vector2(cx + 0.5f, cy),
                                        new Vector2(cx + 0.5f, cy + chd), _dxCellHi, 1f);
                                    // bottom + right = shadow
                                    RenderTarget.DrawLine(new Vector2(cx, cy + chd - 0.5f),
                                        new Vector2(cx + cwd, cy + chd - 0.5f), _dxCellLo, 1f);
                                    RenderTarget.DrawLine(new Vector2(cx + cwd - 0.5f, cy),
                                        new Vector2(cx + cwd - 0.5f, cy + chd), _dxCellLo, 1f);
                                    _dxCellHi.Opacity = 1f;
                                    _dxCellLo.Opacity = 1f;
                                }

                                // Letter centered on the tile when there's room to
                                // read it (matches the modern reference). Using the
                                // centered text format keeps every glyph symmetric
                                // inside its cell, not flush-left.
                                if (lw >= LetterFontSize * 0.5f && cellH >= 6f)
                                {
                                    float glyphH = Math.Min(tickPx, Math.Max(cellH, 7f));
                                    RenderTarget.DrawText(FastCharText(tpo.Letter), _fmtCellLetter,
                                        new RectangleF(xL, yLvl - glyphH * 0.5f, lw, glyphH), _dxCellHi);
                                }
                            }
                        }
                        else
                        {
                            for (int i = 0; i < cnt; i++)
                            {
                                TpoLetterItem tpo = level.Letters[i];
                                float         xL  = rightToLeft ? xAnchor - (i + 1) * lw : xAnchor + i * lw;

                                SharpDX.Direct2D1.SolidColorBrush br;
                                if (inVA)
                                    br = isON ? _dxOnVah : _dxTpoVA;
                                else if (isON)
                                    br = _dxOnTpo;
                                else if (LetterColorMode == TpoLetterColoring.IBHighlight && tpo.IsIB)
                                    br = _dxTpoIB;
                                else
                                    br = _dxTpo;

                                // Allocation-free text draw. Creating a TextLayout per letter
                                // per frame spawned thousands of COM objects on every repaint.
                                //
                                // The glyph box is centered on the real price Y (yLvl). Its
                                // height must not exceed the real row height (cellH) by much,
                                // otherwise compressing the Y axis makes each letter's box
                                // bleed into the rows above/below — that was the "letters jump
                                // and overlap" artifact. We use a height that stays legible
                                // (>= ~6px so the glyph isn't clipped) but never overruns the
                                // available row spacing, keeping every letter pinned to its
                                // true price.
                                float glyphH = Math.Min(tickPx, Math.Max(cellH, 6f));
                                RenderTarget.DrawText(FastCharText(tpo.Letter), _fmtLetter,
                                    new RectangleF(xL, yLvl - glyphH * 0.5f, lw + 1f, glyphH), br);
                            }
                        }
                    }
                }
            }

            // POC line + Value Area. These belong to the TPO profile, so the
            // panel's TPO button (_showTpo) must hide them along with the letters.
            // (IB/OR above stay independent — only POC/VA follow the TPO toggle.)
            if (_showTpo)
            {
                // Granular extension: RTH and ETH have independent toggles.
                bool sessionExtendOn = isON ? _extTpoEthBtn : _extTpoRthBtn;
                bool extendVaPoc     = ExtendVaPoc && sessionExtendOn;
                float xRightEdge = ChartPanel.X + ChartPanel.W;

                float yPoc    = cs.GetYByValue(p.POC);
                float xPocEnd = rightToLeft ? xAnchor - p.MaxCount * lw : xAnchor + p.MaxCount * lw;
                float xPocLineEnd = extendVaPoc && !rightToLeft ? xRightEdge : xPocEnd;
                StyledLine(yPoc, xAnchor, xPocLineEnd, _dxPoc, PocLineWidth, VaLineStyle);
                PutLabelWithPrice(isON ? "ONPOC" : "POC", p.POC, rightToLeft ? xPocEnd - 80f : xPocEnd + 2f, yPoc);

                // VAH / VAL — RTH and Overnight
                if (ShowValueArea)
                {
                    float yVAH  = cs.GetYByValue(p.VAH);
                    float yVAL  = cs.GetYByValue(p.VAL);
                    float xVEnd = rightToLeft ? xAnchor : xAnchor + profileWidth + 4f;
                    float xVSt  = rightToLeft ? xAnchor - profileWidth - 4f : xAnchor;

                    // Extension target for the dashed lines (right edge of chart panel).
                    float xVLineEnd = extendVaPoc && !rightToLeft ? xRightEdge : xVEnd;
                    // When extended, the VA fill follows the lines to the same edge.
                    float xVFillEnd = extendVaPoc && !rightToLeft ? xRightEdge : xVEnd;

                    SharpDX.Direct2D1.SolidColorBrush vahBr = isON ? _dxOnVah : _dxVah;
                    SharpDX.Direct2D1.SolidColorBrush valBr = isON ? _dxOnVal : _dxVal;

                    // VA fill rectangle (extends with the lines when extension is on)
                    if (!isON && yVAL > yVAH)
                        RenderTarget.FillRectangle(
                            new RectangleF(xVSt, yVAH, xVFillEnd - xVSt, yVAL - yVAH), _dxVaFill);

                    StyledLine(yVAH, xVSt, xVLineEnd, vahBr, VaLineWidth, VaLineStyle);
                    StyledLine(yVAL, xVSt, xVLineEnd, valBr, VaLineWidth, VaLineStyle);
                    PutLabelWithPrice(isON ? "ONVAH" : "VAH", p.VAH, rightToLeft ? xVSt - 80f : xVEnd + 2f, yVAH);
                    PutLabelWithPrice(isON ? "ONVAL" : "VAL", p.VAL, rightToLeft ? xVSt - 80f : xVEnd + 2f, yVAL);
                }
            }

            // IB dashed lines removed — the filled rectangle box already shows the IB range clearly
        }

        // =====================================================================
        //  COMPOSITE — pinned to RIGHT EDGE of chart panel
        // =====================================================================

        private void DrawCompositeRightEdge(ChartScale cs, double ts)
        {
            if (!BuildCompositeCacheIfNeeded(ts)) return;

            // ── Layout ───────────────────────────────────────────────────────
            float xRight = ChartPanel.X + ChartPanel.W - 4f;
            float maxW   = (float)CompositeMaxWidth;
            float rawTickPx = Math.Abs(cs.GetYByValue(0) - cs.GetYByValue(ts));

            // STABILITY: the row-merge factor is decided through hysteresis so
            // dragging the price (Y) axis up/down doesn't make the composite re-group
            // its bars and flip sizes every frame.
            float compTickPx = rawTickPx < 0.05f ? 0.05f : rawTickPx;
            int merge = StableMergeCore(compTickPx, ref _compLastMerge, ref _compLastMergeTickPx);
            float barH = compTickPx * merge;
            if (barH < 1f) barH = 1f;

            float panelTop = ChartPanel.Y - 24f;
            float panelBot = ChartPanel.Y + ChartPanel.H + 24f;

            // ── Draw cached bars from raw volume (no smoothing, exact proportions) ──
            bool compWantOutline = (VolumeStyle == TpoVolumeStyle.Outline || VolumeStyle == TpoVolumeStyle.Both);
            bool compWantBars    = (VolumeStyle == TpoVolumeStyle.Bars    || VolumeStyle == TpoVolumeStyle.Both);
            List<Vector2> compPts = compWantOutline ? new List<Vector2>(512) : null;

            int i2 = 0;
            while (i2 < _compSortedKeysCache.Count)
            {
                double accVol   = 0;
                double topPrice = _compSortedKeysCache[i2];
                double botPrice = topPrice;
                bool   hasPoc   = false;
                bool   inVA     = false;

                for (int m = 0; m < merge && (i2 + m) < _compSortedKeysCache.Count; m++)
                {
                    double pr = _compSortedKeysCache[i2 + m];
                    double v; if (_compVolCache.TryGetValue(pr, out v)) accVol += v;
                    if (Math.Abs(pr - _compPOC) < ts * 0.1) hasPoc = true;
                    if (pr >= _compVAL && pr <= _compVAH) inVA = true;
                    botPrice = pr;
                }
                i2 += merge;

                if (accVol <= 0 || _compMaxVol <= 0) continue;

                float y = cs.GetYByValue((topPrice + botPrice) * 0.5);
                if (CullOffscreenPriceLevels && (y < panelTop || y > panelBot))
                    continue;

                // Normalize by (MaxVol × merge) so width is invariant to the vertical
                // row-merge factor (anti-flicker on Y compression).
                float wDenom = (float)(_compMaxVol * merge);
                float w  = wDenom > 0f ? (float)(accVol) / wDenom * maxW : 0f;
                float xL = xRight - w;

                if (compWantOutline)
                    compPts.Add(new Vector2(xL, y));

                if (compWantBars)
                {
                    SharpDX.Direct2D1.SolidColorBrush br;
                    if (hasPoc)     br = _dxVolPoc;
                    else if (inVA)  br = _dxCompositeVA;
                    else            br = _dxComposite;

                    RenderTarget.FillRectangle(new RectangleF(xL, y - barH * 0.5f, w, barH), br);
                }
            }

            if (compWantOutline && compPts.Count >= 2)
                DrawStaircaseOutline(compPts, xRight, true, barH);

            // ── POC line ─────────────────────────────────────────────────────
            float yPoc = cs.GetYByValue(_compPOC);
            RenderTarget.DrawLine(new Vector2(xRight - maxW, yPoc), new Vector2(xRight, yPoc), _dxVolPoc, 2f);

            // ── VAH / VAL lines ───────────────────────────────────────────────
            if (ShowValueArea)
            {
                float yVAH = cs.GetYByValue(_compVAH);
                float yVAL = cs.GetYByValue(_compVAL);
                DashedLine(yVAH, xRight - maxW, xRight, _dxVah, 1f);
                DashedLine(yVAL, xRight - maxW, xRight, _dxVal, 1f);
                PutLabelWithPrice("VAH", _compVAH, xRight - maxW - 80f, yVAH);
                PutLabelWithPrice("VAL", _compVAL, xRight - maxW - 80f, yVAL);
            }

            PutLabel(_compModeTag, xRight - maxW - 80f, yPoc - 18f);
            PutLabelWithPrice("VPOC", _compPOC, xRight + 2f, yPoc);
        }

        private bool BuildCompositeCacheIfNeeded(double ts)
        {
            string key = CompositeMode.ToString() + "|" + CompositePeriods + "|" + CompositeBars + "|" +
                CompositeRolloverMinutes + "|" + CompositeFixedStartBarsAgo + "|" +
                ValueAreaPercent.ToString("F4") + "|" + ShowOvernight + "|" + _profiles.Count + "|" + _overnight.Count;

            if (!_compositeDirty && _compVolCache.Count > 0 && _compConfigKey == key)
                return true;

            // In realtime, honor the same heavy recalculation cadence used for
            // POC/VA/VPOC. If a valid cache exists, keep drawing it until the next
            // scheduled rebuild instead of merging sessions every frame.
            if (State == State.Realtime && _compVolCache.Count > 0 && !ShouldRunCompositeRecalc())
                return true;

            _compVolCache.Clear();
            _compBuyVolCache.Clear();
            _compSellVolCache.Clear();
            _compSortedKeysCache.Clear();
            _compMaxVol = _compPOC = _compVAH = _compVAL = 0;
            _compModeTag = "COMP";

            switch (CompositeMode)
            {
                case TpoCompositeMode.LastNSessions:
                {
                    int cnt  = Math.Min(CompositePeriods, _profiles.Count);
                    int skip = _profiles.Count - cnt;
                    for (int i = skip; i < _profiles.Count; i++)
                        AccumulateSessionVolumeToComposite(_profiles[i], _compVolCache, _compBuyVolCache, _compSellVolCache);
                    if (ShowOvernight)
                    {
                        int onCnt  = Math.Min(CompositePeriods, _overnight.Count);
                        int onSkip = _overnight.Count - onCnt;
                        for (int i = onSkip; i < _overnight.Count; i++)
                            AccumulateSessionVolumeToComposite(_overnight[i], _compVolCache, _compBuyVolCache, _compSellVolCache);
                    }
                    _compModeTag = "COMP(" + CompositePeriods + "s)";
                    break;
                }

                case TpoCompositeMode.LastNBars:
                {
                    // Profile bar indices come from the 1-min engine, so we compare
                    // by TIME instead: take the timestamp CompositeBars primary bars
                    // ago and include any profile that ended at/after it.
                    int fromBar = Math.Max(0, CurrentBar - CompositeBars);
                    DateTime cutoffB = SafePrimaryBarTime(fromBar);
                    foreach (TpoProfileData p in _profiles)
                        if (p.EndTime >= cutoffB)
                            AccumulateSessionVolumeToComposite(p, _compVolCache, _compBuyVolCache, _compSellVolCache);
                    if (ShowOvernight)
                        foreach (TpoProfileData p in _overnight)
                            if (p.EndTime >= cutoffB)
                                AccumulateSessionVolumeToComposite(p, _compVolCache, _compBuyVolCache, _compSellVolCache);
                    _compModeTag = "COMP(" + CompositeBars + "b)";
                    break;
                }

                case TpoCompositeMode.RolloverMinutes:
                {
                    DateTime cutoff = Time[0].AddMinutes(-CompositeRolloverMinutes);
                    foreach (TpoProfileData p in _profiles)
                        if (p.EndTime >= cutoff)
                            AccumulateSessionVolumeToComposite(p, _compVolCache, _compBuyVolCache, _compSellVolCache);
                    if (ShowOvernight)
                        foreach (TpoProfileData p in _overnight)
                            if (p.EndTime >= cutoff)
                                AccumulateSessionVolumeToComposite(p, _compVolCache, _compBuyVolCache, _compSellVolCache);
                    _compModeTag = "COMP(" + CompositeRolloverMinutes + "m)";
                    break;
                }

                case TpoCompositeMode.FixedRange:
                {
                    // See LastNBars: compare by TIME, not by 1-min engine indices.
                    int fromBar = Math.Max(0, CurrentBar - CompositeFixedStartBarsAgo);
                    DateTime cutoffF = SafePrimaryBarTime(fromBar);
                    foreach (TpoProfileData p in _profiles)
                        if (p.StartTime >= cutoffF)
                            AccumulateSessionVolumeToComposite(p, _compVolCache, _compBuyVolCache, _compSellVolCache);
                    if (ShowOvernight)
                        foreach (TpoProfileData p in _overnight)
                            if (p.StartTime >= cutoffF)
                                AccumulateSessionVolumeToComposite(p, _compVolCache, _compBuyVolCache, _compSellVolCache);
                    _compModeTag = "COMP(FX)";
                    break;
                }
            }

            if (_compVolCache.Count == 0)
            {
                _compositeDirty = false;
                _compConfigKey = key;
                return false;
            }

            foreach (KeyValuePair<double, double> kv in _compVolCache)
            {
                _compSortedKeysCache.Add(kv.Key);
                if (kv.Value > _compMaxVol) { _compMaxVol = kv.Value; _compPOC = kv.Key; }
            }
            _compSortedKeysCache.Sort();

            double totalVol = 0;
            foreach (double v in _compVolCache.Values) totalVol += v;
            double vaTarget = totalVol * (ValueAreaPercent / 100.0);

            int pocIdx = _compSortedKeysCache.IndexOf(_compPOC);
            if (pocIdx < 0) pocIdx = 0;

            double vaSum = _compMaxVol;
            int upIdx = pocIdx + 1, dnIdx = pocIdx - 1;
            _compVAH = _compPOC;
            _compVAL = _compPOC;

            while (vaSum < vaTarget && (upIdx < _compSortedKeysCache.Count || dnIdx >= 0))
            {
                double upAdd = (upIdx < _compSortedKeysCache.Count) ? _compVolCache[_compSortedKeysCache[upIdx]] : 0;
                double dnAdd = (dnIdx >= 0) ? _compVolCache[_compSortedKeysCache[dnIdx]] : 0;
                if (upAdd >= dnAdd && upIdx < _compSortedKeysCache.Count)
                { vaSum += upAdd; _compVAH = _compSortedKeysCache[upIdx]; upIdx++; }
                else if (dnIdx >= 0)
                { vaSum += dnAdd; _compVAL = _compSortedKeysCache[dnIdx]; dnIdx--; }
                else if (upIdx < _compSortedKeysCache.Count)
                { vaSum += upAdd; _compVAH = _compSortedKeysCache[upIdx]; upIdx++; }
            }

            _compositeDirty = false;
            _compConfigKey = key;
            return _compMaxVol > 0;
        }

        // Helper: aggregate one session's per-price volume into composite dictionaries
        private void AccumulateSessionVolumeToComposite(TpoProfileData p,
            Dictionary<double, double> vol,
            Dictionary<double, double> buyVol,
            Dictionary<double, double> sellVol)
        {
            foreach (KeyValuePair<double, TpoPriceLevel> kv in p.Levels)
            {
                if (!vol.ContainsKey(kv.Key))     vol[kv.Key]     = 0;
                if (!buyVol.ContainsKey(kv.Key))  buyVol[kv.Key]  = 0;
                if (!sellVol.ContainsKey(kv.Key)) sellVol[kv.Key] = 0;
                vol[kv.Key]     += kv.Value.Volume;
                buyVol[kv.Key]  += kv.Value.BuyVolume;
                sellVol[kv.Key] += kv.Value.SellVolume;
            }
        }

        // =====================================================================
        //  POOR HIGH / POOR LOW
        // =====================================================================
        // A Poor High/Low is when the profile extreme has <= PoorHLMaxTPOs letters.
        // It signals a weak extreme likely to be revisited.

        private void DrawPoorHL(TpoProfileData p, ChartScale cs, double ts)
        {
            if (p.Levels.Count == 0) return;
            if (!IsProfileWindowVisible(p, 200f, false)) return;

            bool rightToLeft = (ProfileOrientation == TpoOrientation.RightToLeft);
            int maxCols = p.MaxCount;
            if (maxCols <= 0)
                foreach (KeyValuePair<double, TpoPriceLevel> kv in p.Levels)
                    if (kv.Value.Count > maxCols) maxCols = kv.Value.Count;
            float lw = ColumnWidth(p, maxCols);
            float profileWidth = maxCols * lw;
            if (profileWidth < 10f) profileWidth = 10f;

            float xWinStart, xWinEnd; GetSessionWindowPx(p, out xWinStart, out xWinEnd);
            float xAnchor = xWinStart;
            float xEnd    = rightToLeft ? xAnchor - profileWidth - 4f : xAnchor + profileWidth + 4f;

            // Check highest level (Poor High)
            double phigh = p.ProfileHigh;
            double key   = Math.Round(phigh / ts) * ts;
            if (p.Levels.ContainsKey(key) && p.Levels[key].Count <= PoorHLMaxTPOs)
            {
                float y = cs.GetYByValue(phigh);
                // Draw a bracket marker at the extreme
                float xMark = rightToLeft ? xAnchor : xEnd;
                RenderTarget.DrawLine(new Vector2(xMark - 6f, y), new Vector2(xMark + 6f, y), _dxPoorHL, 2f);
                RenderTarget.DrawLine(new Vector2(xMark - 6f, y), new Vector2(xMark - 6f, y + 5f), _dxPoorHL, 2f);
                RenderTarget.DrawLine(new Vector2(xMark + 6f, y), new Vector2(xMark + 6f, y + 5f), _dxPoorHL, 2f);
                PutLabel("PH", xMark + 8f, y);
            }

            // Check lowest level (Poor Low)
            double plow = p.ProfileLow;
            key = Math.Round(plow / ts) * ts;
            if (p.Levels.ContainsKey(key) && p.Levels[key].Count <= PoorHLMaxTPOs)
            {
                float y = cs.GetYByValue(plow);
                float xMark = rightToLeft ? xAnchor : xEnd;
                RenderTarget.DrawLine(new Vector2(xMark - 6f, y), new Vector2(xMark + 6f, y), _dxPoorHL, 2f);
                RenderTarget.DrawLine(new Vector2(xMark - 6f, y), new Vector2(xMark - 6f, y - 5f), _dxPoorHL, 2f);
                RenderTarget.DrawLine(new Vector2(xMark + 6f, y), new Vector2(xMark + 6f, y - 5f), _dxPoorHL, 2f);
                PutLabel("PL", xMark + 8f, y);
            }
        }

        // =====================================================================
        //  SINGLE PRINTS
        // =====================================================================
        // Single Prints = consecutive price levels each touched by only 1 TPO letter.
        // They represent fast, directional movement and are likely to be re-visited.

        private void DrawSinglePrints(TpoProfileData p, ChartScale cs, double ts)
        {
            if (p.Levels.Count < 3) return;
            if (!IsProfileWindowVisible(p, 200f, false)) return;

            bool rightToLeft = (ProfileOrientation == TpoOrientation.RightToLeft);
            int maxCols = p.MaxCount;
            if (maxCols <= 0)
                foreach (KeyValuePair<double, TpoPriceLevel> kv in p.Levels)
                    if (kv.Value.Count > maxCols) maxCols = kv.Value.Count;
            float lw = ColumnWidth(p, maxCols);
            float profileWidth = maxCols * lw;
            if (profileWidth < 10f) profileWidth = 10f;

            float xWinStartSP, xWinEndSP; GetSessionWindowPx(p, out xWinStartSP, out xWinEndSP);
            float xAnchor = xWinStartSP;
            float tickPx  = cs.GetYByValue(0) - cs.GetYByValue(ts);
            if (tickPx < 1f) tickPx = 1f;

            // Reuse the profile's cached ascending key ladder instead of allocating
            // and sorting a brand-new list on every render pass. GetSortedKeys()
            // only rebuilds when a new price level appears (SortedKeysDirty), so for
            // finalized/stable profiles this is allocation-free per frame. The
            // single-print scan needs keys high→low, so we walk the ascending cache
            // in reverse via an index map: ascKeys[count-1-j] gives the j-th price
            // from the top, preserving the original descending logic.
            List<double> ascKeys = GetSortedKeys(p);
            int keyCount = ascKeys.Count;
            if (keyCount < 3) return;

            // Find consecutive runs of count=1 levels (real Market Profile single prints)
            // A run must have at least 2 consecutive single-letter levels to be significant
            int i = 0;
            while (i < keyCount)
            {
                double ki = ascKeys[keyCount - 1 - i];   // i-th price from the top (descending)
                // Skip extremes — those are marked as Poor H/L
                bool isExtreme = Math.Abs(ki - p.ProfileHigh) < ts * 0.1
                              || Math.Abs(ki - p.ProfileLow)  < ts * 0.1;

                if (p.Levels[ki].Count == 1 && !isExtreme)
                {
                    // Found start of a potential single-print run
                    int runStart = i;
                    int runEnd   = i;
                    while (runEnd + 1 < keyCount
                        && p.Levels[ascKeys[keyCount - 1 - (runEnd + 1)]].Count == 1
                        && Math.Abs(ascKeys[keyCount - 1 - (runEnd + 1)] - p.ProfileHigh) > ts * 0.1
                        && Math.Abs(ascKeys[keyCount - 1 - (runEnd + 1)] - p.ProfileLow)  > ts * 0.1)
                    {
                        runEnd++;
                    }

                    // Draw highlight only for runs of 2+ consecutive single prints
                    int runLen = runEnd - runStart + 1;
                    if (runLen >= 2)
                    {
                        double topPrice = ascKeys[keyCount - 1 - runStart];
                        double botPrice = ascKeys[keyCount - 1 - runEnd];
                        float  yTop    = cs.GetYByValue(topPrice + ts * 0.5);
                        float  yBot    = cs.GetYByValue(botPrice - ts * 0.5);
                        if (yBot < yTop) { float t = yTop; yTop = yBot; yBot = t; }

                        // Highlight rectangle across full profile width
                        float xRect = rightToLeft ? xAnchor - profileWidth - 4f : xAnchor;
                        float wRect = profileWidth + 4f;
                        RenderTarget.FillRectangle(
                            new RectangleF(xRect, yTop, wRect, yBot - yTop), _dxSinglePrint);

                        // Label "SP" at the right edge
                        float yMid = (yTop + yBot) * 0.5f;
                        PutLabel("SP", rightToLeft ? xRect - 30f : xRect + wRect + 2f, yMid);
                    }

                    i = runEnd + 1;
                }
                else
                {
                    i++;
                }
            }
        }

        // =====================================================================
        //  VOLUME PROFILE — drawn next to each individual profile
        // =====================================================================

        // Computes the FINAL horizontal placement of a session's volume panel:
        // its left edge (xVolStart) and width (volW), AFTER the session-discipline
        // cap. Both DrawVolProfileForSession and the Delta overlay use this single
        // source of truth so the delta can be centered exactly on the volume that
        // is actually drawn (no drift when the cap shrinks the volume panel, and
        // no dependency on zoom-scaling differences between the two widths).
        private void ComputeVolumePanelRect(TpoProfileData p, bool rightToLeft,
                                            out float xVolStart, out float volW)
        {
            int maxCols = p.MaxCount;
            if (maxCols <= 0)
                foreach (KeyValuePair<double, TpoPriceLevel> kv in p.Levels)
                    if (kv.Value.Count > maxCols) maxCols = kv.Value.Count;
            float lw           = ColumnWidth(p, maxCols);
            float profileWidth = maxCols * lw;
            if (profileWidth < 10f) profileWidth = 10f;

            float xWinStartV, xWinEndV; GetSessionWindowPx(p, out xWinStartV, out xWinEndV);
            float xAnchor = xWinStartV;
            volW          = ScaledPanelWidth(p, (float)VolProfileWidth);
            float gap     = GetProfileGap(p);
            float baseOff = SecondaryProfileOffset(profileWidth);

            xVolStart = rightToLeft
                ? xAnchor - baseOff - gap - volW
                : xAnchor + baseOff + gap;

            // SESSION-DISCIPLINE CAP (identical to the drawing code).
            if (!rightToLeft)
            {
                float roomToEnd = xWinEndV - xVolStart;
                if (roomToEnd < 1f) roomToEnd = 1f;
                if (volW > roomToEnd) volW = roomToEnd;
            }
            else
            {
                float roomLeft = xVolStart + volW - xWinStartV;
                if (roomLeft < volW)
                {
                    volW = roomLeft < 1f ? 1f : roomLeft;
                    xVolStart = xWinStartV;
                }
            }
        }

        private void DrawVolumeProfile(ChartScale cs)
        {
            double ts = ManualTickSize > 0 ? ManualTickSize : Instrument.MasterInstrument.TickSize;

            // Draw for each RTH profile
            for (int i = 0; i < _profiles.Count; i++)
                DrawVolProfileForSession(_profiles[i], cs, ts);

            // Draw for overnight profiles if visible
            if (ShowOvernight)
                for (int i = 0; i < _overnight.Count; i++)
                    DrawVolProfileForSession(_overnight[i], cs, ts);
        }

        private void DrawVolProfileForSession(TpoProfileData p, ChartScale cs, double ts)
        {
            if (p.Levels.Count == 0 || p.MaxVolume <= 0) return;
            // VP VA/VPOC extension (ayer+hoy) keeps lines running to the right
            // edge, so use the extend-aware cull to keep them visible when the
            // body has scrolled off the left of the viewport.
            bool vpExtendOn = ExtendVpVaPoc && (p.IsOvernight ? _extVpEthBtn : _extVpRthBtn);
            if (!IsProfileWindowVisible(p, 200f, vpExtendOn)) return;

            bool rightToLeft = (ProfileOrientation == TpoOrientation.RightToLeft);

            // Single source of truth for the panel rect (shared with Delta overlay).
            float xVolStart, maxW;
            ComputeVolumePanelRect(p, rightToLeft, out xVolStart, out maxW);

            float xWinStartV, xWinEndV; GetSessionWindowPx(p, out xWinStartV, out xWinEndV);

            // Exact tick height in pixels. The vertical row-merge factor is derived
            // through StableMerge() so dragging the price axis up/down does NOT make
            // the rows flip between normal and grouped sizes (anti-flicker).
            float tickPxRaw = Math.Abs(cs.GetYByValue(0) - cs.GetYByValue(ts));
            float tickPx = tickPxRaw < 0.05f ? 0.05f : tickPxRaw;
            int   merge  = StableMerge(p, tickPx);
            float barH   = tickPx * merge;
            if (barH < 1f) barH = 1f;

            // VPOC — use the deterministic value computed in CalculateVolumeVA
            // (center tie-break), consistent with the stats panel and alerts.
            double vpoc = p.VPOC > 0 ? p.VPOC : 0;
            if (vpoc <= 0)
            {
                double maxV = 0;
                foreach (KeyValuePair<double, TpoPriceLevel> kv in p.Levels)
                    if (kv.Value.Volume > maxV) { maxV = kv.Value.Volume; vpoc = kv.Key; }
            }

            double volVAH = p.VolVAH;
            double volVAL = p.VolVAL;

            // Suite-ready render path: iterate only real profile levels instead
            // of walking every possible tick between low/high. This preserves the
            // exact stored Volume/VA/VPOC data while removing thousands of empty
            // per-tick lookups on large NQ/ES sessions.
            List<double> volRenderKeys = GetSortedKeys(p);
            double visibleLow  = _renderPriceMin - ts * Math.Max(1, merge + 1);
            double visibleHigh = _renderPriceMax + ts * Math.Max(1, merge + 1);

            int vIdx = 0;
            bool wantOutline = (VolumeStyle == TpoVolumeStyle.Outline || VolumeStyle == TpoVolumeStyle.Both);
            bool wantBars    = (VolumeStyle == TpoVolumeStyle.Bars    || VolumeStyle == TpoVolumeStyle.Both);
            // In outline/both we collect (xEdge, y) points top→bottom to trace a
            // contour that follows the histogram shape (peaks/valleys), with lightly
            // rounded corners. In bars/both we also draw the classic bars.
            List<Vector2> outlinePts = wantOutline ? new List<Vector2>(256) : null;

            while (vIdx < volRenderKeys.Count)
            {
                double accVol   = 0;
                double topPrice = volRenderKeys[vIdx];
                double botPrice = topPrice;
                bool   havePrice = false;
                bool   hasPoc   = false;
                bool   inVA     = false;

                for (int m = 0; m < merge && vIdx < volRenderKeys.Count; m++, vIdx++)
                {
                    double pr = volRenderKeys[vIdx];
                    if (CullOffscreenPriceLevels && (pr < visibleLow || pr > visibleHigh))
                    {
                        botPrice = pr;
                        continue;
                    }

                    if (!havePrice) { topPrice = pr; havePrice = true; }
                    TpoPriceLevel lvl = p.Levels[pr];
                    accVol += lvl.Volume;
                    if (Math.Abs(pr - vpoc) < ts * 0.1) hasPoc = true;
                    if (pr >= volVAL && pr <= volVAH) inVA = true;
                    botPrice = pr;
                }

                if (!havePrice || accVol <= 0) continue;

                float y = cs.GetYByValue((topPrice + botPrice) * 0.5);
                if (CullOffscreenPriceLevels && (y < _renderPanelTop || y > _renderPanelBot))
                    continue;
                // Normalize by (MaxVolume × merge) so the bar width does NOT change
                // when the vertical row-merge factor changes — width invariant to
                // Y-axis compression (the definitive anti-flicker fix).
                float wDenom = (float)(p.MaxVolume * merge);
                float w = wDenom > 0f ? (float)(accVol) / wDenom * maxW : 0f;

                if (wantOutline)
                {
                    float xEdge = rightToLeft ? xVolStart - w : xVolStart + w;
                    outlinePts.Add(new Vector2(xEdge, y));
                }

                if (wantBars)
                {
                    SharpDX.Direct2D1.SolidColorBrush br;
                    if (hasPoc)      br = _dxVolPoc;
                    else if (inVA)   br = _dxVolVA;
                    else             br = _dxVolBar;

                    float bhV = barH >= 3f ? barH - 1f : barH;
                    float xbar = rightToLeft ? xVolStart - w : xVolStart;
                    RenderTarget.FillRectangle(new RectangleF(xbar, y - bhV * 0.5f, w, bhV), br);
                }
            }

            if (wantOutline && outlinePts.Count >= 2)
                DrawVolumeOutline(p, cs, outlinePts, xVolStart, rightToLeft, vpoc, volVAH, volVAL, barH);

            // Granular extension: VP RTH and VP ETH have independent toggles.
            // Still limited to the current or previous trading day so the chart
            // doesn't get cluttered with extensions from week-old sessions.
            bool sessionVpExtend = p.IsOvernight ? _extVpEthBtn : _extVpRthBtn;
            bool extendVp = ExtendVpVaPoc && sessionVpExtend
                            && IsCurrentOrPreviousDay(p) && !rightToLeft;
            float xRightEdge = ChartPanel.X + ChartPanel.W;
            float xVolLineEnd = extendVp ? xRightEdge : xVolStart + maxW;

            // ── Volume VA lines ──────────────────────────────────────────────
            if (ShowValueArea && volVAH > 0 && volVAL > 0)
            {
                float yVAH = cs.GetYByValue(volVAH);
                float yVAL = cs.GetYByValue(volVAL);

                // When extended, shade the VA band across the extension too, so the
                // background follows the lines (not just the lines on their own).
                // Banda VA siempre visible (antes solo al extender): delimita el
                // area de valor detras de las barras y hace el perfil legible de
                // un vistazo. Mismo brush translucido, cero impacto en datos.
                if (yVAL > yVAH)
                    RenderTarget.FillRectangle(
                        new RectangleF(xVolStart, yVAH, xVolLineEnd - xVolStart, yVAL - yVAH),
                        _dxVaFill);

                SharpDX.Direct2D1.SolidColorBrush vahBr = p.IsOvernight ? _dxOnVah : _dxVah;
                SharpDX.Direct2D1.SolidColorBrush valBr = p.IsOvernight ? _dxOnVal : _dxVal;
                StyledLine(yVAH, xVolStart, xVolLineEnd, vahBr, 1.5f, VaLineStyle);
                StyledLine(yVAL, xVolStart, xVolLineEnd, valBr, 1.5f, VaLineStyle);
                // Prefix the volume VA labels and push them past the TPO VA labels so
                // the two sets never overlap when VP shares the TPO axis. The TPO VA
                // labels sit at xVEnd+2; the volume ones go further right.
                float xVolLbl = Math.Max(xVolStart + maxW, xVolLineEnd) + 46f;
                PutLabelWithPrice("vVAH", volVAH, xVolLbl, yVAH);
                PutLabelWithPrice("vVAL", volVAL, xVolLbl, yVAL);
            }

            // ── VPOC line ────────────────────────────────────────────────────
            float yVpoc = cs.GetYByValue(vpoc);
            StyledLine(yVpoc, xVolStart, xVolLineEnd, _dxVolPoc, 2f, VaLineStyle);
            float xVpocLbl = Math.Max(xVolStart + maxW, xVolLineEnd) + 46f;
            PutLabelWithPrice("vVPOC", vpoc, xVpocLbl, yVpoc);
        }

        // Draws the volume profile as a smooth contour with a faint fill underneath
        // (modern "pro" look). Points are the outer edge of each price level, sorted
        // top→bottom. We build a closed polygon (contour + the vertical baseline at
        // xVolStart) for the fill, then stroke the contour on top. Allocation is one
        // PathGeometry/Sink per session per frame, disposed immediately.
        private void DrawVolumeOutline(TpoProfileData p, ChartScale cs, List<Vector2> pts,
                                       float xBaseline, bool rightToLeft,
                                       double vpoc, double volVAH, double volVAL, float rowH)
        {
            if (pts.Count < 2) return;

            DrawStaircaseOutline(pts, xBaseline, rightToLeft, rowH);

            // VPOC marker at the widest level.
            float yVpoc = cs.GetYByValue(vpoc);
            float bestX = xBaseline; float bestDy = float.MaxValue;
            for (int i = 0; i < pts.Count; i++)
            {
                float dy = Math.Abs(pts[i].Y - yVpoc);
                if (dy < bestDy) { bestDy = dy; bestX = pts[i].X; }
            }
            RenderTarget.DrawLine(new Vector2(xBaseline, yVpoc), new Vector2(bestX, yVpoc), _dxVolPoc, 2f);
        }

        // Draws a histogram-faithful contour from edge points (one per price row,
        // top→bottom) with LIGHTLY ROUNDED corners, plus a faint fill underneath.
        // The line traces each row's outer edge as a horizontal step and connects
        // adjacent rows with vertical steps, but every inner corner is chamfered/
        // rounded by a few px so the silhouette follows the real peaks and valleys
        // and looks like rounded-corner bars fused into a contour — exactly the
        // "pro" look requested. Shared by the volume profile and the composite.
        private void DrawStaircaseOutline(List<Vector2> pts, float xBaseline,
                                          bool rightToLeft, float rowH)
        {
            if (pts == null || pts.Count < 2) return;

            // Build a staircase that strictly follows the bar perimeter, derived ONLY
            // from the collected (xEdge, yCenter) points — never from barH, so it can
            // never drift away from the bars. For each row we hold its width across a
            // vertical band, and we switch widths at the MIDPOINT between adjacent row
            // centers (the shared edge of the two bars). That midpoint handling makes
            // the steps line up exactly with the bar faces regardless of row spacing,
            // merge, or gaps.
            int nPts = pts.Count;
            var steps = new List<Vector2>(nPts * 2 + 4);

            // Top cap: start at the first row's outer edge, at its top boundary.
            float firstHalf = (nPts > 1) ? Math.Abs(pts[1].Y - pts[0].Y) * 0.5f : 1f;
            float yTop0 = pts[0].Y - firstHalf;
            steps.Add(new Vector2(pts[0].X, yTop0));

            for (int i = 0; i < nPts; i++)
            {
                float x = pts[i].X;

                // Lower boundary of this row = midpoint to the next row's center
                // (or a half-row past the last center for the final row).
                float yLow;
                if (i < nPts - 1)
                    yLow = (pts[i].Y + pts[i + 1].Y) * 0.5f;
                else
                {
                    float lastHalf = (nPts > 1) ? Math.Abs(pts[i].Y - pts[i - 1].Y) * 0.5f : 1f;
                    yLow = pts[i].Y + lastHalf;
                }

                // Vertical face of this row down to its lower boundary…
                steps.Add(new Vector2(x, yLow));

                // …then the horizontal step across to the next row's width, at the
                // shared boundary, so the corner sits exactly on the bar edges.
                if (i < nPts - 1)
                    steps.Add(new Vector2(pts[i + 1].X, yLow));
            }

            // ── Faint fill bounded by the staircase and the baseline ─────────
            try
            {
                using (var geo = new SharpDX.Direct2D1.PathGeometry(RenderTarget.Factory))
                {
                    using (var sink = geo.Open())
                    {
                        sink.BeginFigure(new Vector2(xBaseline, steps[0].Y),
                            SharpDX.Direct2D1.FigureBegin.Filled);
                        for (int i = 0; i < steps.Count; i++)
                            sink.AddLine(steps[i]);
                        sink.AddLine(new Vector2(xBaseline, steps[steps.Count - 1].Y));
                        sink.EndFigure(SharpDX.Direct2D1.FigureEnd.Closed);
                        sink.Close();
                    }
                    float prevOp = _dxVolBar.Opacity;
                    _dxVolBar.Opacity = 0.16f;
                    RenderTarget.FillGeometry(geo, _dxVolBar);
                    _dxVolBar.Opacity = prevOp;
                }
            }
            catch { /* degenerate input → skip fill, keep the line */ }

            // ── Crisp staircase line strictly on the bar perimeter ───────────
            for (int i = 1; i < steps.Count; i++)
                RenderTarget.DrawLine(steps[i - 1], steps[i], _dxVolVA, 1.3f);
        }

        // =====================================================================
        //  DELTA PROFILE — buy vs sell at each price level
        //  Drawn to the right of the volume profile.
        //  Each bar extends left (sell) or right (buy) from a center axis.
        //  This is the standard "delta profile" used in order-flow analysis.
        // =====================================================================

        private void DrawDeltaProfile(ChartScale cs)
        {
            double ts = ManualTickSize > 0 ? ManualTickSize : Instrument.MasterInstrument.TickSize;

            for (int i = 0; i < _profiles.Count; i++)
                DrawDeltaForSession(_profiles[i], cs, ts);

            if (ShowOvernight)
                for (int i = 0; i < _overnight.Count; i++)
                    DrawDeltaForSession(_overnight[i], cs, ts);
        }

        private void DrawDeltaForSession(TpoProfileData p, ChartScale cs, double ts)
        {
            if (p.Levels.Count == 0 || p.MaxAbsDelta <= 0) return;
            if (!IsProfileWindowVisible(p, 200f, false)) return;

            bool rightToLeft = (ProfileOrientation == TpoOrientation.RightToLeft);

            // Profile width (mirrors DrawProfileGeneric, zoom-aware)
            int maxCols = p.MaxCount;
            if (maxCols <= 0)
                foreach (KeyValuePair<double, TpoPriceLevel> kv in p.Levels)
                    if (kv.Value.Count > maxCols) maxCols = kv.Value.Count;
            float lw = ColumnWidth(p, maxCols);
            float profileWidth = maxCols * lw;
            if (profileWidth < 10f) profileWidth = 10f;

            float xWinStartD, xWinEndD; GetSessionWindowPx(p, out xWinStartD, out xWinEndD);
            float xAnchor = xWinStartD;
            float maxW    = ScaledPanelWidth(p, (float)DeltaProfileWidth);
            float gap     = GetProfileGap(p);
            // When TPO is hidden, collapse to the left limit (offset 0).
            float baseOff = SecondaryProfileOffset(profileWidth);

            bool volOn = ShowVolumeProfile && _showVolBtn;

            // Final volume panel placement (post session-discipline cap), shared
            // with DrawVolProfileForSession so the delta lines up EXACTLY.
            float xVolStart = 0f, volW = 0f;
            if (volOn) ComputeVolumePanelRect(p, rightToLeft, out xVolStart, out volW);

            // Extra spacing to leave room for VAH/VAL labels of the volume profile
            float volLabelSpace = volOn ? 85f : 0f;

            float xBase;
            bool overlayMode = DeltaOverlayOnVolume && volOn;
            if (overlayMode)
            {
                // OVERLAY MODE: el delta comparte el MISMO ORIGEN (borde izquierdo)
                // que el Volume Profile, que dibuja sus barras desde xVolStart hacia
                // la derecha. Anclando el delta en el MISMO xVolStart, sus barras se
                // apilan exactamente sobre las de volumen (mismo punto de partida),
                // sin desfase, aunque el delta sea mas angosto (DeltaProfileWidth).
                // Antes lo centrabamos, lo que con barras de origen-izquierdo lo
                // hacia ver corrido respecto al volumen.
                xBase = xVolStart;

                // El delta nunca debe ser mas ancho que el volumen en overlay (para
                // no sobresalir de la sesion que el volumen ya respeta).
                if (volW > 0f && maxW > volW) maxW = volW;
            }
            else
            {
                // SEPARATE COLUMN MODE: delta en su propia columna a la derecha
                // (o izquierda) del volumen, pasando el espacio de etiquetas.
                xBase = rightToLeft
                    ? xAnchor - baseOff - gap - volW - volLabelSpace - gap
                    : xAnchor + baseOff + gap + volW + volLabelSpace + gap;

                // Cap de disciplina de sesion para el modo columna separada.
                if (!rightToLeft)
                {
                    float roomToEnd = xWinEndD - xBase;
                    if (roomToEnd < 1f) roomToEnd = 1f;
                    if (maxW > roomToEnd) maxW = roomToEnd;
                }
                else
                {
                    float roomLeft = xBase + maxW - xWinStartD;
                    if (roomLeft < maxW)
                    {
                        maxW  = roomLeft < 1f ? 1f : roomLeft;
                        xBase = xWinStartD + maxW;   // pin to window's left edge
                    }
                }
            }

            // Center of the delta panel. In overlay mode the panel always spans
            // [xBase, xBase+maxW] in increasing coords (centered on the volume), so
            // the center is xBase + maxW/2 regardless of orientation. In separate
            // RTL mode the panel grows leftward from xBase, hence the branch.
            float xCenter = (overlayMode || !rightToLeft)
                ? xBase + maxW * 0.5f
                : xBase - maxW * 0.5f;
            // For LeftAligned, all bars start at xBase and grow right (toward outside)
            // For RightAligned, all bars end at xBase + maxW and grow left

            // Stable vertical merge (hysteresis) so dragging the price axis doesn't
            // make rows flip between normal and grouped sizes.
            float tickPxRawD = Math.Abs(cs.GetYByValue(0) - cs.GetYByValue(ts));
            float tickPx = tickPxRawD < 0.05f ? 0.05f : tickPxRawD;
            int   merge = StableMerge(p, tickPx);
            float barH  = tickPx * merge;
            if (barH < 1f) barH = 1f;

            double priceLow  = Math.Round(p.ProfileLow  / ts) * ts;
            double priceHigh = Math.Round(p.ProfileHigh / ts) * ts;
            double visibleLow  = _renderPriceMin - ts * Math.Max(1, merge + 1);
            double visibleHigh = _renderPriceMax + ts * Math.Max(1, merge + 1);

            // Same optimization as the Volume Profile: render only stored price
            // levels. Delta values are unchanged; we simply avoid scanning empty
            // ticks between the profile low/high on every frame.
            List<double> deltaRenderKeys = GetSortedKeys(p);
            int dIdx = 0;
            while (dIdx < deltaRenderKeys.Count)
            {
                double accBuy   = 0;
                double accSell  = 0;
                double topPrice = deltaRenderKeys[dIdx];
                double botPrice = topPrice;
                bool   havePrice = false;

                for (int m = 0; m < merge && dIdx < deltaRenderKeys.Count; m++, dIdx++)
                {
                    double pr = deltaRenderKeys[dIdx];
                    if (CullOffscreenPriceLevels && (pr < visibleLow || pr > visibleHigh))
                    {
                        botPrice = pr;
                        continue;
                    }

                    if (!havePrice) { topPrice = pr; havePrice = true; }
                    TpoPriceLevel lvl = p.Levels[pr];
                    accBuy  += lvl.BuyVolume;
                    accSell += lvl.SellVolume;
                    botPrice = pr;
                }

                if (!havePrice) continue;
                double delta = accBuy - accSell;
                if (Math.Abs(delta) < 0.01) continue;

                float y  = cs.GetYByValue((topPrice + botPrice) * 0.5);
                if (CullOffscreenPriceLevels && (y < _renderPanelTop || y > _renderPanelBot))
                    continue;
                SharpDX.Direct2D1.SolidColorBrush br = (delta > 0) ? _dxDeltaBuy : _dxDeltaSell;

                float xBar, wBar;
                // Normalize by (MaxAbsDelta × merge) so the bar width is invariant to
                // the vertical row-merge factor (anti-flicker on Y-axis compression),
                // same approach as the volume profile.
                float dDenom = (float)(p.MaxAbsDelta * merge);
                float dNorm  = dDenom > 0f ? (float)Math.Abs(delta) / dDenom : 0f;
                switch (DeltaAlignment)
                {
                    case TpoDeltaAlignment.LeftAligned:
                    {
                        wBar = dNorm * maxW;
                        xBar = xBase;
                        break;
                    }
                    case TpoDeltaAlignment.RightAligned:
                    {
                        wBar = dNorm * maxW;
                        xBar = xBase + maxW - wBar;
                        break;
                    }
                    default:
                    {
                        wBar = dNorm * (maxW * 0.5f);
                        xBar = (delta > 0) ? xCenter : xCenter - wBar;
                        break;
                    }
                }

                // Misma separacion de 1px que el perfil de volumen: filas nitidas.
                float bhD = barH >= 3f ? barH - 1f : barH;
                RenderTarget.FillRectangle(new RectangleF(xBar, y - bhD * 0.5f, wBar, bhD), br);
            }

            // Vertical axis line — only meaningful for Centered mode
            float yTop = cs.GetYByValue(priceHigh + ts);
            float yBot = cs.GetYByValue(priceLow  - ts);
            if (yBot < yTop) { float t = yTop; yTop = yBot; yBot = t; }

            bool overlay = DeltaOverlayOnVolume && ShowVolumeProfile && _showVolBtn;

            if (DeltaAlignment == TpoDeltaAlignment.Centered && !overlay)
                RenderTarget.DrawLine(new Vector2(xCenter, yTop), new Vector2(xCenter, yBot), _dxDeltaZero, 1f);

            // Δ label — only in separate-column mode (in overlay the volume's
            // VAH/VAL/VPOC labels already mark the zone; a Δ here would clash).
            if (!overlay)
            {
                float labelX = xBase + maxW + 2f;
                PutLabel("Δ", labelX, (yTop + yBot) * 0.5f);
            }
        }

        // =====================================================================
        //  NAKED POCs
        // =====================================================================

        private void DrawNakedPOCs(ChartScale cs)
        {
            if (_profiles.Count < 2) return;
            for (int i = 0; i < _profiles.Count - 1; i++)
            {
                TpoProfileData p = _profiles[i];
                bool touched = false;
                for (int j = i + 1; j < _profiles.Count; j++)
                    if (_profiles[j].ProfileHigh >= p.POC && _profiles[j].ProfileLow <= p.POC)
                    { touched = true; break; }

                if (!touched)
                {
                    // Anchor the naked-POC ray by TIME (profile end), not by the
                    // 1-min engine's bar index. The ray starts where the session
                    // ended and extends to the right by NakedPocExtend bars.
                    int endBar  = PrimaryBarIdxByTime(p.EndTime);
                    int eIdx    = endBar + NakedPocExtend;
                    if (!IsProfileVisible(endBar, eIdx, 100f)) continue;

                    float y    = cs.GetYByValue(p.POC);
                    float x1   = TimeToX(p, p.EndTime);
                    int   eIdxClamped = Math.Min(eIdx, ChartBars.ToIndex);
                    float x2   = BarToX(eIdxClamped) + _slotW;
                    RenderTarget.DrawLine(new Vector2(x1, y), new Vector2(x2, y), _dxNakedPoc, 1.5f);
                    PutLabelWithPrice("nPOC", p.POC, x2 + 2f, y);
                }
            }
        }

        // =====================================================================
        //  STATS PANEL
        // =====================================================================

        private void DrawStatsPanel()
        {
            TpoProfileData p = _profiles[_profiles.Count - 1];
            if (p == null || p.Levels.Count == 0) return;

            float panW  = 250f;
            float panX  = 10f, panY = 10f;

            // ── MINIMIZED MODE — just a slim header bar ────────────────────
            if (_statsPanelMinimized)
            {
                float barH = 22f;

                // Position
                if (StatsPanelPosition == TextPosition.TopRight)
                { panX = ChartPanel.W - panW - 10f; panY = 10f; }
                else if (StatsPanelPosition == TextPosition.BottomRight)
                { panX = ChartPanel.W - panW - 10f; panY = ChartPanel.H - barH - 30f; }
                else if (StatsPanelPosition == TextPosition.BottomLeft)
                { panX = 10f; panY = ChartPanel.H - barH - 30f; }

                RectangleF bg = new RectangleF(panX - 4f, panY - 4f, panW + 8f, barH + 8f);
                RenderTarget.FillRectangle(bg, _dxStatBg);
                RenderTarget.DrawRectangle(bg, _dxPanelBorder, 1f);

                // Title text left (allocation-free)
                RenderTarget.DrawText("ILT-DEV-PROFILES", _fmtStat,
                    new RectangleF(panX, panY + 2f, panW - 28f, barH), _dxStatText);

                // Expand button ▶ on the right
                _btnMinRect = new RectangleF(panX + panW - 22f, panY, 22f, barH);
                DrawSingleButton(_btnMinRect, "▶", true);

                // Invalidate toggle button rects so they don't catch clicks
                _btnTpoRect = _btnVolRect = _btnDeltaRect = _btnIBRect = _btnORRect = RectangleF.Empty;
                _btnModeRect = RectangleF.Empty;
                return;
            }

            // ── EXPANDED MODE (full panel) ────────────────────────────────
            double range   = p.ProfileHigh - p.ProfileLow;
            double va      = p.VAH - p.VAL;
            double ibRange = (p.IBH > 0 && p.IBL > 0) ? p.IBH - p.IBL : 0;
            double orRange = (p.ORH > 0 && p.ORL > 0) ? p.ORH - p.ORL : 0;
            double vaRatio = range > 0 ? va / range * 100.0 : 0;

            double vpoc = 0, maxV = 0, totalBuy = 0, totalSell = 0;
            foreach (KeyValuePair<double, TpoPriceLevel> kv in p.Levels)
            {
                if (kv.Value.Volume > maxV) { maxV = kv.Value.Volume; vpoc = kv.Key; }
                totalBuy  += kv.Value.BuyVolume;
                totalSell += kv.Value.SellVolume;
            }
            double netDelta = totalBuy - totalSell;

            // Build lines list
            List<string> lineList = new List<string>();
            lineList.Add("-- TPO MARKET PROFILE --");
            if (ShowDayType)
                lineList.Add("Tipo Dia  : " + DayTypeToText(p.DayType));
            if (StatsShowDate)
                lineList.Add("Fecha     : " + p.StartTime.ToString("MM/dd/yyyy"));
            if (StatsShowPOC)
            {
                lineList.Add("POC       : " + p.POC.ToString("F2"));
                lineList.Add("VAH       : " + p.VAH.ToString("F2"));
                lineList.Add("VAL       : " + p.VAL.ToString("F2"));
                lineList.Add("VA Range  : " + va.ToString("F2") + " (" + vaRatio.ToString("F1") + "%)");
            }
            if (StatsShowIB)
            {
                lineList.Add("IB High   : " + (p.IBH > 0 ? p.IBH.ToString("F2") : "---"));
                lineList.Add("IB Low    : " + (p.IBL > 0 ? p.IBL.ToString("F2") : "---"));
                lineList.Add("IB Range  : " + (ibRange > 0 ? ibRange.ToString("F2") : "---"));
            }
            if (StatsShowOR && (p.ORH > 0 || p.ORL > 0))
            {
                lineList.Add("OR High   : " + (p.ORH > 0 ? p.ORH.ToString("F2") : "---"));
                lineList.Add("OR Low    : " + (p.ORL > 0 ? p.ORL.ToString("F2") : "---"));
                lineList.Add("OR Range  : " + (orRange > 0 ? orRange.ToString("F2") : "---"));
            }
            if (StatsShowProfileRange)
            {
                lineList.Add("Prof High : " + p.ProfileHigh.ToString("F2"));
                lineList.Add("Prof Low  : " + p.ProfileLow.ToString("F2"));
                lineList.Add("Prof Range: " + range.ToString("F2"));
                lineList.Add("Total TPOs: " + p.TotalTPOs.ToString());
            }
            if (StatsShowVolStats && maxV > 0)
            {
                lineList.Add("VPOC      : " + vpoc.ToString("F2"));
                lineList.Add("Vol Buy   : " + totalBuy.ToString("N0"));
                lineList.Add("Vol Sell  : " + totalSell.ToString("N0"));
                lineList.Add("Delta Net : " + (netDelta >= 0 ? "+" : "") + netDelta.ToString("N0"));
            }

            string[] lines   = lineList.ToArray();
            float    lineH   = Math.Max(14f, StatsFontSize + 4f);
            float    btnRowH = 56f;
            float    panH    = lines.Length * lineH + 12f + btnRowH;

            // Position
            if (StatsPanelPosition == TextPosition.TopRight)
            { panX = ChartPanel.W - panW - 10f; panY = 10f; }
            else if (StatsPanelPosition == TextPosition.BottomRight)
            { panX = ChartPanel.W - panW - 10f; panY = ChartPanel.H - panH - 30f; }
            else if (StatsPanelPosition == TextPosition.BottomLeft)
            { panX = 10f; panY = ChartPanel.H - panH - 30f; }

            // Background
            RectangleF bgR = new RectangleF(panX - 4f, panY - 4f, panW + 8f, panH + 8f);
            RenderTarget.FillRectangle(bgR, _dxStatBg);
            RenderTarget.DrawRectangle(bgR, _dxPanelBorder, 1f);

            // Minimize button ▼ top-right corner of the panel
            _btnMinRect = new RectangleF(panX + panW - 22f, panY, 22f, 18f);
            DrawSingleButton(_btnMinRect, "▼", true);

            // Stats lines (leave room for minimize button on first row) — allocation-free
            for (int i = 0; i < lines.Length; i++)
            {
                float availW = (i == 0) ? panW - 26f : panW;
                RenderTarget.DrawText(lines[i], _fmtStat,
                    new RectangleF(panX, panY + i * lineH, availW, lineH), _dxStatText);
            }

            // Toggle buttons row 1: TPO, Volumen, Delta
            float btnGap = 4f;
            float btnH   = 22f;
            float btnX   = panX;
            float btnY1  = panY + lines.Length * lineH + 6f;
            float btnW3  = (panW - btnGap * 2) / 3f;

            _btnTpoRect   = new RectangleF(btnX,                         btnY1, btnW3, btnH);
            _btnVolRect   = new RectangleF(btnX + (btnW3 + btnGap),      btnY1, btnW3, btnH);
            _btnDeltaRect = new RectangleF(btnX + (btnW3 + btnGap) * 2f, btnY1, btnW3, btnH);

            DrawSingleButton(_btnTpoRect,   "TPO",     _showTpo);
            DrawSingleButton(_btnVolRect,   "Volumen", _showVolBtn);
            DrawSingleButton(_btnDeltaRect, "Delta",   _showDeltaBtn);

            // Toggle buttons row 2: IB, OR, MODO (SES/DIA/SEM)
            float btnY2 = btnY1 + btnH + btnGap;
            float btnW2 = (panW - btnGap * 2) / 3f;

            _btnIBRect   = new RectangleF(btnX,                         btnY2, btnW2, btnH);
            _btnORRect   = new RectangleF(btnX + (btnW2 + btnGap),      btnY2, btnW2, btnH);
            _btnModeRect = new RectangleF(btnX + (btnW2 + btnGap) * 2f, btnY2, btnW2, btnH);

            DrawSingleButton(_btnIBRect,   "IB", _showIBBtn);
            DrawSingleButton(_btnORRect,   "OR", _showORBtn);
            DrawSingleButton(_btnModeRect, SessionModeLabel(), true);
        }

        private string SessionModeLabel()
        {
            return _sessionModeRt == TpoSessionPeriod.Diario  ? "DÍA"
                 : _sessionModeRt == TpoSessionPeriod.Semanal ? "SEM"
                 : "SES";
        }

        // Alterna SES -> DIA -> SEM. Reinicio limpio: los perfiles acumulados
        // pertenecen a las fronteras del modo anterior, asi que se vacian y el
        // motor construye el nuevo modo desde ahora en vivo; el historico completo
        // del modo elegido se reconstruye al recargar el indicador (F5/re-aplicar),
        // donde toda la serie se reprocesa con las fronteras nuevas. Los calculos
        // por nivel (TPO/VA/POC/volumen/delta) no cambian en absoluto.
        private void CycleSessionMode()
        {
            SetSessionMode(_sessionModeRt == TpoSessionPeriod.Sesiones ? TpoSessionPeriod.Diario
                         : _sessionModeRt == TpoSessionPeriod.Diario   ? TpoSessionPeriod.Semanal
                         : TpoSessionPeriod.Sesiones);
        }

        // Cambia el periodo de perfiles directamente (usado por los botones
        // Sesión/Diario/Semanal del panel WPF y por CycleSessionMode). Mismo
        // reinicio limpio descrito arriba. Si el modo no cambia, no hace nada.
        private void SetSessionMode(TpoSessionPeriod mode)
        {
            if (_sessionModeRt == mode) { RefreshPeriodButtons(); return; }

            _sessionModeRt = mode;
            SessionPeriod  = mode;   // persiste con el workspace/plantilla
            InvalidateCompositeCache();

            // Reconstruye los perfiles desde el historico ya cargado (serie 1-min)
            // para que el cambio sea inmediato y el usuario NO tenga que pulsar F5.
            // El clear de las colecciones ocurre dentro del rebuild, bajo lock, para
            // no chocar con el hilo de datos (OnBarUpdate) ni el de render.
            RebuildProfilesFromHistory();

            RefreshPeriodButtons();
            ForceRedraw();
        }

        // Reprocesa el historico ya cargado y reconstruye TODA la estructura de
        // perfiles con el modo de periodo actual, para que el cambio sea inmediato
        // sin pulsar F5. Reconstruye:
        //   - TPO + sesiones + IB/OR + POC/VA   desde la serie de 1 min (BarsArray[2])
        //   - Volumen + Buy/Sell + Delta         desde la serie de ticks (BarsArray[1])
        // El delta se clasifica por uptick/downtick en la reconstruccion (igual que
        // el fallback historico). El bid/ask real solo aplica a ticks en vivo via
        // OnMarketData; en un cambio de vista esta aproximacion es la adecuada.
        private void RebuildProfilesFromHistory()
        {
            try
            {
                if (BarsArray == null || BarsArray.Length < 3) return;
                Bars minBars  = BarsArray[2];
                if (minBars == null || minBars.Count <= 0) return;

                double ts = ManualTickSize > 0
                    ? ManualTickSize
                    : Instrument.MasterInstrument.TickSize;

                lock (_profilesLock)
                {
                    _profiles.Clear();
                    _overnight.Clear();
                    _currentProfile = null;
                    _currentON = null;
                    _lastProcessedBar = -1;

                    // ── Pasada 1: TPO + sesiones desde la serie de 1 minuto ──────
                    int count = minBars.Count;
                    int prevHhmm = -1;
                    for (int i = 0; i < count; i++)
                    {
                        DateTime bt = minBars.GetTime(i);
                        double   bh = minBars.GetHigh(i);
                        double   bl = minBars.GetLow(i);
                        double   bc = minBars.GetClose(i);
                        int      hh = bt.Hour * 100 + bt.Minute;

                        if (IsNewRTHPeriodFor(bt, hh, prevHhmm))
                        {
                            if (_currentProfile != null) FinalizeClosedProfile(_currentProfile, ts);
                            StartNewRTHProfile(ts, bt, i);
                            if (ShowOvernight && _currentON != null)
                            {
                                FinalizeClosedProfile(_currentON, ts);
                                _currentON = null;
                            }
                        }
                        if (ShowOvernight && _sessionModeRt == TpoSessionPeriod.Sesiones && IsNewONPeriod(hh, prevHhmm))
                        {
                            if (_currentON != null) FinalizeClosedProfile(_currentON, ts);
                            StartNewONProfile(bt, i);
                        }

                        if (_currentProfile != null && IsInPrimaryProfileTime(bt))
                        {
                            UpdateProfileFromBar(_currentProfile, ts, bt, bh, bl, i, hh);
                            _currentProfile.LastClose = bc;
                        }
                        if (ShowOvernight && _currentON != null && IsInOvernightHours(hh))
                        {
                            UpdateONProfileFromBar(_currentON, ts, bt, bh, bl, i, hh);
                            _currentON.LastClose = bc;
                        }

                        prevHhmm = hh;
                    }

                    // ── Pasada 2: volumen/delta desde la serie de ticks ──────────
                    // Recorremos cada trade y lo depositamos en el perfil cerrado o
                    // abierto que posee su timestamp, buscando por ventana de sesion.
                    RebuildVolumeFromTicks(ts);

                    // Recompute every profile so POC/VA + Volume VA + delta norm están
                    // listos para dibujar (no solo los abiertos).
                    for (int i = 0; i < _profiles.Count; i++)  { _profiles[i].IsDirty = true; RecomputeProfileIfDirty(_profiles[i], ts); }
                    for (int i = 0; i < _overnight.Count; i++) { _overnight[i].IsDirty = true; RecomputeProfileIfDirty(_overnight[i], ts); }

                    InvalidateCompositeCache();
                }

                if (DebugLog)
                    Print("ILT-DEV-PROFILES: rebuild -> " + _profiles.Count + " RTH, " + _overnight.Count + " ON");
            }
            catch (Exception ex)
            {
                if (DebugLog) Print("ILT-DEV-PROFILES rebuild error: " + ex.Message);
            }
        }

        // Deposita el volumen historico (serie de ticks BarsArray[1]) en los perfiles
        // ya construidos, clasificando buy/sell por uptick/downtick. Cada trade se
        // enruta al perfil RTH u ON cuya ventana teorica contiene su timestamp.
        private void RebuildVolumeFromTicks(double ts)
        {
            if (BarsArray.Length < 2) return;
            Bars tickBars = BarsArray[1];
            if (tickBars == null || tickBars.Count <= 0) return;

            // Earliest profile window start: ticks before this can't belong to any
            // profile, so we skip them outright (cheap bound for big tick series).
            DateTime earliest = DateTime.MaxValue;
            for (int i = 0; i < _profiles.Count; i++)
                if (_profiles[i].WindowStart < earliest) earliest = _profiles[i].WindowStart;
            if (ShowOvernight)
                for (int i = 0; i < _overnight.Count; i++)
                    if (_overnight[i].WindowStart < earliest) earliest = _overnight[i].WindowStart;
            if (earliest == DateTime.MaxValue) return;   // no profiles to fill

            int tc = tickBars.Count;
            double lastPrice = 0;
            bool   lastBuy   = true;

            for (int i = 0; i < tc; i++)
            {
                DateTime tt = tickBars.GetTime(i);
                if (tt < earliest) { lastPrice = tickBars.GetClose(i); continue; }

                double price = tickBars.GetClose(i);
                double vol   = tickBars.GetVolume(i);
                if (vol <= 0) { lastPrice = price; continue; }

                bool isBuy;
                if (price > lastPrice)      isBuy = true;
                else if (price < lastPrice) isBuy = false;
                else                         isBuy = lastBuy;
                lastPrice = price;
                lastBuy   = isBuy;

                double priceKey = Math.Round(price / ts) * ts;

                TpoProfileData target = FindProfileForTime(tt);
                if (target != null)
                    AddTradeToProfile(target, priceKey, vol, isBuy);
            }
        }

        // Localiza el perfil (RTH u ON) cuya ventana teorica contiene el timestamp.
        // Usado solo por la reconstruccion historica de volumen.
        private TpoProfileData FindProfileForTime(DateTime t)
        {
            // RTH/Diario/Semanal primero.
            for (int i = _profiles.Count - 1; i >= 0; i--)
            {
                TpoProfileData p = _profiles[i];
                if (t >= p.WindowStart && t < p.WindowEnd) return p;
            }
            if (ShowOvernight)
                for (int i = _overnight.Count - 1; i >= 0; i--)
                {
                    TpoProfileData p = _overnight[i];
                    if (t >= p.WindowStart && t < p.WindowEnd) return p;
                }
            return null;
        }

        // Time-explicit variant of IsNewRTHPeriod for history rebuild (no Time[0]).
        private bool IsNewRTHPeriodFor(DateTime barTime, int hhmm, int hhmm1)
        {
            if (!IsInPrimaryProfileTime(barTime)) return false;
            DateTime nowKey = GetPrimarySessionKey(barTime);
            if (_currentProfile == null) return true;
            return nowKey != _currentProfile.SessionKey;
        }

        // Estilo radio: solo el botón del periodo activo queda resaltado.
        private void RefreshPeriodButtons()
        {
            StyleToggle(_btnPerSesW, _sessionModeRt == TpoSessionPeriod.Sesiones);
            StyleToggle(_btnPerDiaW, _sessionModeRt == TpoSessionPeriod.Diario);
            StyleToggle(_btnPerSemW, _sessionModeRt == TpoSessionPeriod.Semanal);
        }

        // =====================================================================
        //  DRAW UTILITIES
        // =====================================================================

        private void DashedLine(float y, float x1, float x2,
            SharpDX.Direct2D1.SolidColorBrush brush, float width)
        {
            // Reuse the cached dash stroke — no per-call StrokeStyle allocation.
            if (_dashStyle == null) return;
            RenderTarget.DrawLine(new Vector2(x1, y), new Vector2(x2, y), brush, width, _dashStyle);
        }

        // Horizontal line honoring a configurable TpoLineStyle (solid/dash/dot/dashdot).
        // All stroke styles are cached, so this allocates nothing per frame.
        private void StyledLine(float y, float x1, float x2,
            SharpDX.Direct2D1.SolidColorBrush brush, float width, TpoLineStyle style)
        {
            SharpDX.Direct2D1.StrokeStyle ss;
            switch (style)
            {
                case TpoLineStyle.Solid:   ss = null;          break;
                case TpoLineStyle.Dot:     ss = _dotStyle;     break;
                case TpoLineStyle.DashDot: ss = _dashDotStyle; break;
                default:                   ss = _dashStyle;    break;
            }
            if (ss == null)
                RenderTarget.DrawLine(new Vector2(x1, y), new Vector2(x2, y), brush, width);
            else
                RenderTarget.DrawLine(new Vector2(x1, y), new Vector2(x2, y), brush, width, ss);
        }

        // =====================================================================
        //  TOGGLE BUTTONS — rendered inside the stats panel
        //  Click any button to show/hide that profile type
        // =====================================================================

        private void DrawSingleButton(RectangleF rect, string label, bool active)
        {
            SharpDX.Direct2D1.SolidColorBrush bg = active ? _dxBtnOn : _dxBtnOff;
            RenderTarget.FillRectangle(rect, bg);
            RenderTarget.DrawRectangle(rect, _dxBtnBorder, 1f);

            // Centered, allocation-free label (cached center format handles alignment).
            RenderTarget.DrawText(label, _fmtLabelCtr, rect, _dxStatText);
        }

        // =====================================================================
        //  WPF FLOATING PANEL  (Fase 2)
        //  A real WPF UserControl added to the chart's UserControlCollection.
        //  Draggable, minimizable, remembers its last position via PanelX/PanelY
        //  (serialized with the workspace). Centered horizontally on first show.
        //  All construction/teardown runs on the UI thread (Dispatcher).
        // =====================================================================

        private System.Windows.Media.Brush WpfBrush(System.Windows.Media.Brush b, double opacity)
        {
            // Clone a brush at a given opacity and freeze it (freezing lets WPF
            // share it across threads and skip change tracking — faster).
            System.Windows.Media.Brush c = b == null
                ? System.Windows.Media.Brushes.Black
                : b.Clone();
            c.Opacity = opacity;
            if (c.CanFreeze) c.Freeze();
            return c;
        }

        private System.Windows.Controls.Button MakeToggleButton(string label)
        {
            var btn = new System.Windows.Controls.Button();
            btn.Content    = label;
            btn.FontFamily = new System.Windows.Media.FontFamily("Consolas");
            btn.FontSize   = 11;
            btn.FontWeight = System.Windows.FontWeights.Bold;
            btn.Margin     = new System.Windows.Thickness(2);
            btn.Padding    = new System.Windows.Thickness(4, 2, 4, 2);
            btn.MinWidth   = 46;
            btn.Foreground = System.Windows.Media.Brushes.White;
            btn.BorderBrush= new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(255, 179, 179, 179));
            btn.BorderThickness = new System.Windows.Thickness(1);
            return btn;
        }

        // Visually reflect a toggle's on/off state via background color.
        private void StyleToggle(System.Windows.Controls.Button btn, bool active)
        {
            if (btn == null) return;
            btn.Background = active
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(242, 51, 140, 217))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(217, 51, 51, 51));
        }

        private void BuildWpfPanel()
        {
            if (_wpfBuilt) return;
            if (ChartControl == null || !UseWpfPanel) return;

            try
            {
                // ── Root container: a Grid we position via Margin inside the
                //    chart's UserControlCollection (a Canvas-like overlay). ──
                _wpfPanel = new System.Windows.Controls.Grid();
                _wpfPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                _wpfPanel.VerticalAlignment   = System.Windows.VerticalAlignment.Top;
                _wpfPanel.Width  = 250;

                var border = new System.Windows.Controls.Border();
                border.Background      = WpfBrush(StatsBgColor, StatsBgOpacity / 100.0);
                border.BorderBrush     = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(153, 77, 179, 255));
                border.BorderThickness = new System.Windows.Thickness(1);
                border.CornerRadius    = new System.Windows.CornerRadius(4);
                _wpfPanel.Children.Add(border);

                var outer = new System.Windows.Controls.StackPanel();
                outer.Orientation = System.Windows.Controls.Orientation.Vertical;
                border.Child = outer;

                // ── Header (drag handle + title + minimize button) ──
                var header = new System.Windows.Controls.Grid();
                header.Height = 22;
                header.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(60, 255, 255, 255));
                header.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition
                    { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
                header.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition
                    { Width = System.Windows.GridLength.Auto });

                _wpfHeaderText = new System.Windows.Controls.TextBlock();
                _wpfHeaderText.Text       = "ILT-DEV-PROFILES";
                _wpfHeaderText.FontFamily = new System.Windows.Media.FontFamily("Consolas");
                _wpfHeaderText.FontSize   = Math.Max(11, StatsFontSize);
                _wpfHeaderText.FontWeight = System.Windows.FontWeights.Bold;
                _wpfHeaderText.Foreground = WpfBrush(StatsTextColor, 1.0);
                _wpfHeaderText.Margin     = new System.Windows.Thickness(6, 2, 4, 2);
                _wpfHeaderText.VerticalAlignment = System.Windows.VerticalAlignment.Center;
                System.Windows.Controls.Grid.SetColumn(_wpfHeaderText, 0);
                header.Children.Add(_wpfHeaderText);

                _wpfMinBtn = new System.Windows.Controls.Button();
                _wpfMinBtn.Content    = _statsPanelMinimized ? "▶" : "▼";
                _wpfMinBtn.Width      = 22;
                _wpfMinBtn.Height     = 18;
                _wpfMinBtn.FontSize   = 10;
                _wpfMinBtn.Margin     = new System.Windows.Thickness(2);
                _wpfMinBtn.Foreground = System.Windows.Media.Brushes.White;
                _wpfMinBtn.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(242, 51, 140, 217));
                System.Windows.Controls.Grid.SetColumn(_wpfMinBtn, 1);
                _wpfMinBtn.Click += (s, e) => { ToggleMinimize(); };
                header.Children.Add(_wpfMinBtn);

                // Dragging is initiated from the header.
                header.MouseLeftButtonDown += Header_MouseLeftButtonDown;
                header.MouseMove           += Header_MouseMove;
                header.MouseLeftButtonUp   += Header_MouseLeftButtonUp;
                header.Cursor = System.Windows.Input.Cursors.SizeAll;
                outer.Children.Add(header);

                // ── Body (stats text + toggle buttons) ──
                _wpfBody = new System.Windows.Controls.StackPanel();
                _wpfBody.Orientation = System.Windows.Controls.Orientation.Vertical;
                _wpfBody.Margin = new System.Windows.Thickness(6, 4, 6, 6);

                _wpfStatsText = new System.Windows.Controls.TextBlock();
                _wpfStatsText.FontFamily = new System.Windows.Media.FontFamily("Consolas");
                _wpfStatsText.FontSize   = Math.Max(6, StatsFontSize);
                _wpfStatsText.Foreground = WpfBrush(StatsTextColor, 1.0);
                _wpfStatsText.TextWrapping = System.Windows.TextWrapping.NoWrap;
                _wpfBody.Children.Add(_wpfStatsText);

                // Toggle button rows
                var row1 = new System.Windows.Controls.StackPanel
                    { Orientation = System.Windows.Controls.Orientation.Horizontal,
                      Margin = new System.Windows.Thickness(0, 4, 0, 0) };
                _btnTpoW   = MakeToggleButton("TPO");
                _btnVolW   = MakeToggleButton("Volumen");
                _btnDeltaW = MakeToggleButton("Delta");
                _btnTpoW.Click   += (s, e) => { _showTpo      = !_showTpo;      StyleToggle(_btnTpoW,   _showTpo);      ForceRedraw(); };
                _btnVolW.Click   += (s, e) => { _showVolBtn   = !_showVolBtn;   StyleToggle(_btnVolW,   _showVolBtn);   ForceRedraw(); };
                _btnDeltaW.Click += (s, e) => { _showDeltaBtn = !_showDeltaBtn; StyleToggle(_btnDeltaW, _showDeltaBtn); ForceRedraw(); };
                row1.Children.Add(_btnTpoW); row1.Children.Add(_btnVolW); row1.Children.Add(_btnDeltaW);
                _wpfBody.Children.Add(row1);

                var row2 = new System.Windows.Controls.StackPanel
                    { Orientation = System.Windows.Controls.Orientation.Horizontal,
                      Margin = new System.Windows.Thickness(0, 2, 0, 0) };
                _btnIBW = MakeToggleButton("IB");
                _btnORW = MakeToggleButton("OR");
                _btnIBW.Click += (s, e) => { _showIBBtn = !_showIBBtn; StyleToggle(_btnIBW, _showIBBtn); ForceRedraw(); };
                _btnORW.Click += (s, e) => { _showORBtn = !_showORBtn; StyleToggle(_btnORW, _showORBtn); ForceRedraw(); };
                _btnIBW.MinWidth = 70; _btnORW.MinWidth = 70;
                row2.Children.Add(_btnIBW); row2.Children.Add(_btnORW);
                _wpfBody.Children.Add(row2);

                // Row 3 — TPO VA/POC extension, split by session (RTH and ETH).
                // Each session has its own toggle so you can extend yesterday's
                // RTH POC without also extending the overnight VAH, etc.
                var row3 = new System.Windows.Controls.StackPanel
                    { Orientation = System.Windows.Controls.Orientation.Horizontal,
                      Margin = new System.Windows.Thickness(0, 2, 0, 0) };
                _btnExtTpoRthW = MakeToggleButton("Ext TPO RTH");
                _btnExtTpoEthW = MakeToggleButton("Ext TPO ETH");
                _btnExtTpoRthW.Click += (s, e) => { _extTpoRthBtn = !_extTpoRthBtn; StyleToggle(_btnExtTpoRthW, _extTpoRthBtn); ForceRedraw(); };
                _btnExtTpoEthW.Click += (s, e) => { _extTpoEthBtn = !_extTpoEthBtn; StyleToggle(_btnExtTpoEthW, _extTpoEthBtn); ForceRedraw(); };
                _btnExtTpoRthW.MinWidth = 92; _btnExtTpoEthW.MinWidth = 92;
                row3.Children.Add(_btnExtTpoRthW); row3.Children.Add(_btnExtTpoEthW);
                _wpfBody.Children.Add(row3);

                // Row 4 — Volume Profile VA/VPOC extension, split by session.
                var row4 = new System.Windows.Controls.StackPanel
                    { Orientation = System.Windows.Controls.Orientation.Horizontal,
                      Margin = new System.Windows.Thickness(0, 2, 0, 0) };
                _btnExtVpRthW = MakeToggleButton("Ext VP RTH");
                _btnExtVpEthW = MakeToggleButton("Ext VP ETH");
                _btnExtVpRthW.Click += (s, e) => { _extVpRthBtn = !_extVpRthBtn; StyleToggle(_btnExtVpRthW, _extVpRthBtn); ForceRedraw(); };
                _btnExtVpEthW.Click += (s, e) => { _extVpEthBtn = !_extVpEthBtn; StyleToggle(_btnExtVpEthW, _extVpEthBtn); ForceRedraw(); };
                _btnExtVpRthW.MinWidth = 92; _btnExtVpEthW.MinWidth = 92;
                row4.Children.Add(_btnExtVpRthW); row4.Children.Add(_btnExtVpEthW);
                _wpfBody.Children.Add(row4);

                // Row 5 — Periodo del perfil (radio): Sesión / Diario / Semanal.
                // Cambiar el periodo vacía los perfiles acumulados y el motor
                // reconstruye en vivo con las nuevas fronteras (ver SetSessionMode).
                var row5 = new System.Windows.Controls.StackPanel
                    { Orientation = System.Windows.Controls.Orientation.Horizontal,
                      Margin = new System.Windows.Thickness(0, 2, 0, 0) };
                _btnPerSesW = MakeToggleButton("Sesión");
                _btnPerDiaW = MakeToggleButton("Diario");
                _btnPerSemW = MakeToggleButton("Semanal");
                _btnPerSesW.Click += (s, e) => { SetSessionMode(TpoSessionPeriod.Sesiones); ForceRedraw(); };
                _btnPerDiaW.Click += (s, e) => { SetSessionMode(TpoSessionPeriod.Diario);   ForceRedraw(); };
                _btnPerSemW.Click += (s, e) => { SetSessionMode(TpoSessionPeriod.Semanal);  ForceRedraw(); };
                _btnPerSesW.ToolTip = "Perfiles por sesión RTH/ETH";
                _btnPerDiaW.ToolTip = "Un perfil por día de trading";
                _btnPerSemW.ToolTip = "Un perfil por semana de trading";
                row5.Children.Add(_btnPerSesW); row5.Children.Add(_btnPerDiaW); row5.Children.Add(_btnPerSemW);
                _wpfBody.Children.Add(row5);

                outer.Children.Add(_wpfBody);

                // Reflect persisted toggle/min state
                StyleToggle(_btnTpoW, _showTpo); StyleToggle(_btnVolW, _showVolBtn);
                StyleToggle(_btnDeltaW, _showDeltaBtn); StyleToggle(_btnIBW, _showIBBtn);
                StyleToggle(_btnORW, _showORBtn);
                StyleToggle(_btnExtTpoRthW, _extTpoRthBtn); StyleToggle(_btnExtTpoEthW, _extTpoEthBtn);
                StyleToggle(_btnExtVpRthW,  _extVpRthBtn);  StyleToggle(_btnExtVpEthW,  _extVpEthBtn);
                RefreshPeriodButtons();
                _statsPanelMinimized = PanelMinimizedPersist;
                _wpfBody.Visibility = _statsPanelMinimized
                    ? System.Windows.Visibility.Collapsed
                    : System.Windows.Visibility.Visible;
                _wpfMinBtn.Content = _statsPanelMinimized ? "▶" : "▼";

                // Add to the chart overlay and place it.
                UserControlCollection.Add(_wpfPanel);
                _wpfBuilt = true;
                PlacePanel();
            }
            catch (Exception ex)
            {
                if (DebugLog) Print("BuildWpfPanel error: " + ex.Message);
            }
        }

        // Position the panel via Margin. Centers horizontally the first time
        // (PanelX < 0 sentinel), otherwise restores the saved position, clamped
        // to stay on-screen.
        private void PlacePanel()
        {
            if (_wpfPanel == null || ChartControl == null) return;

            double chartW = ChartPanel != null ? ChartPanel.W : 800;
            double chartH = ChartPanel != null ? ChartPanel.H : 600;
            double panelW = _wpfPanel.Width > 0 ? _wpfPanel.Width : 250;

            double x = PanelX;
            double y = PanelY;

            if (!_wpfPosInitialized)
            {
                if (PanelX < 0 || PanelY < 0)         // first ever show → center
                {
                    // Only commit the centered position once the chart panel has a
                    // real size; otherwise leave it pending so OnRender retries.
                    bool sizeReady = ChartPanel != null && ChartPanel.W > 1 && ChartPanel.H > 1;
                    // Force a layout pass so DesiredSize.Height is valid for Y.
                    _wpfPanel.Measure(new System.Windows.Size(
                        double.PositiveInfinity, double.PositiveInfinity));
                    double panelH = _wpfPanel.DesiredSize.Height > 0
                        ? _wpfPanel.DesiredSize.Height : 300;
                    if (PanelX < 0) x = Math.Max(0, (chartW - panelW) * 0.5);
                    if (PanelY < 0) y = Math.Max(0, (chartH - panelH) * 0.5);
                    if (sizeReady) _wpfPosInitialized = true;
                }
                else
                {
                    _wpfPosInitialized = true;        // restoring a saved position
                }
            }

            // Clamp so it never disappears off the edges.
            double maxX = Math.Max(0, chartW - 40);
            double maxY = Math.Max(0, chartH - 22);
            if (x < 0) x = 0; if (x > maxX) x = maxX;
            if (y < 0) y = 0; if (y > maxY) y = maxY;

            _wpfPanel.Margin = new System.Windows.Thickness(x, y, 0, 0);
            // Persist the resolved coordinates only once the position is final
            // (chart size known). While still pending we keep the -1 sentinel so
            // the next render keeps re-centering until the chart has a real size.
            if (_wpfPosInitialized) { PanelX = x; PanelY = y; }
        }

        private void ToggleMinimize()
        {
            _statsPanelMinimized = !_statsPanelMinimized;
            PanelMinimizedPersist = _statsPanelMinimized;
            if (_wpfBody != null)
                _wpfBody.Visibility = _statsPanelMinimized
                    ? System.Windows.Visibility.Collapsed
                    : System.Windows.Visibility.Visible;
            if (_wpfMinBtn != null)
                _wpfMinBtn.Content = _statsPanelMinimized ? "▶" : "▼";
        }

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_wpfPanel == null) return;
            _wpfDragging  = true;
            _wpfDragStart = e.GetPosition(ChartControl);
            _wpfStartLeft = _wpfPanel.Margin.Left;
            _wpfStartTop  = _wpfPanel.Margin.Top;
            ((System.Windows.UIElement)sender).CaptureMouse();
            e.Handled = true;
        }

        private void Header_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_wpfDragging || _wpfPanel == null) return;
            System.Windows.Point now = e.GetPosition(ChartControl);
            double nx = _wpfStartLeft + (now.X - _wpfDragStart.X);
            double ny = _wpfStartTop  + (now.Y - _wpfDragStart.Y);

            double chartW = ChartPanel != null ? ChartPanel.W : 800;
            double chartH = ChartPanel != null ? ChartPanel.H : 600;
            if (nx < 0) nx = 0; if (nx > chartW - 40) nx = chartW - 40;
            if (ny < 0) ny = 0; if (ny > chartH - 20) ny = chartH - 20;

            _wpfPanel.Margin = new System.Windows.Thickness(nx, ny, 0, 0);
            PanelX = nx; PanelY = ny;   // persist live
            _userMovedPanel = true;
        }

        private void Header_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_wpfDragging) return;
            _wpfDragging = false;
            ((System.Windows.UIElement)sender).ReleaseMouseCapture();
            e.Handled = true;
        }

        // Push the latest profile stats into the WPF text block. Called from
        // OnRender (UI thread) so it stays in sync with the data, but only the
        // string is rebuilt — cheap. Mirrors the legacy SharpDX panel content.
        private void UpdateWpfPanelContent()
        {
            if (!_wpfBuilt || _wpfStatsText == null) return;
            if (_profiles.Count == 0) return;
            if (_statsPanelMinimized) return;   // body hidden, skip string work

            TpoProfileData p = _profiles[_profiles.Count - 1];
            if (p == null || p.Levels.Count == 0) return;

            double range   = p.ProfileHigh - p.ProfileLow;
            double va      = p.VAH - p.VAL;
            double ibRange = (p.IBH > 0 && p.IBL > 0) ? p.IBH - p.IBL : 0;
            double orRange = (p.ORH > 0 && p.ORL > 0) ? p.ORH - p.ORL : 0;
            double vaRatio = range > 0 ? va / range * 100.0 : 0;

            double totalBuy = 0, totalSell = 0;
            foreach (KeyValuePair<double, TpoPriceLevel> kv in p.Levels)
            { totalBuy += kv.Value.BuyVolume; totalSell += kv.Value.SellVolume; }
            double netDelta = totalBuy - totalSell;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("-- TPO MARKET PROFILE --");
            if (ShowDayType)        sb.AppendLine("Tipo Dia  : " + DayTypeToText(p.DayType));
            if (StatsShowDate)      sb.AppendLine("Fecha     : " + p.StartTime.ToString("MM/dd/yyyy"));
            if (StatsShowPOC)
            {
                sb.AppendLine("POC       : " + p.POC.ToString("F2"));
                sb.AppendLine("VAH       : " + p.VAH.ToString("F2"));
                sb.AppendLine("VAL       : " + p.VAL.ToString("F2"));
                sb.AppendLine("VA Range  : " + va.ToString("F2") + " (" + vaRatio.ToString("F1") + "%)");
            }
            if (StatsShowIB)
            {
                sb.AppendLine("IB High   : " + (p.IBH > 0 ? p.IBH.ToString("F2") : "---"));
                sb.AppendLine("IB Low    : " + (p.IBL > 0 ? p.IBL.ToString("F2") : "---"));
                sb.AppendLine("IB Range  : " + (ibRange > 0 ? ibRange.ToString("F2") : "---"));
            }
            if (StatsShowOR && (p.ORH > 0 || p.ORL > 0))
            {
                sb.AppendLine("OR High   : " + (p.ORH > 0 ? p.ORH.ToString("F2") : "---"));
                sb.AppendLine("OR Low    : " + (p.ORL > 0 ? p.ORL.ToString("F2") : "---"));
                sb.AppendLine("OR Range  : " + (orRange > 0 ? orRange.ToString("F2") : "---"));
            }
            if (StatsShowProfileRange)
            {
                sb.AppendLine("Prof High : " + p.ProfileHigh.ToString("F2"));
                sb.AppendLine("Prof Low  : " + p.ProfileLow.ToString("F2"));
                sb.AppendLine("Prof Range: " + range.ToString("F2"));
                sb.AppendLine("Total TPOs: " + p.TotalTPOs.ToString());
            }
            if (StatsShowVolStats && p.MaxVolume > 0)
            {
                sb.AppendLine("VPOC      : " + p.VPOC.ToString("F2"));
                sb.AppendLine("Vol Buy   : " + totalBuy.ToString("N0"));
                sb.AppendLine("Vol Sell  : " + totalSell.ToString("N0"));
                sb.Append    ("Delta Net : " + (netDelta >= 0 ? "+" : "") + netDelta.ToString("N0"));
            }

            string txt = sb.ToString();
            // Only touch the UI when the text actually changed (avoids layout churn).
            if (_wpfStatsText.Text != txt) _wpfStatsText.Text = txt;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  TOOLBAR BUTTON (top, Disco-Trading style)
        //  Adds a button into the chart Window's MainMenu (top toolbar). The
        //  button toggles the WPF floating panel's visibility, so the user can
        //  keep the chart fully unobstructed when the panel isn't needed.
        //  Hosted by the chart Window, so it sits on the title bar, not on the
        //  chart area. Built on the UI thread via Dispatcher.
        // ─────────────────────────────────────────────────────────────────────
        private void BuildToolbarButton()
        {
            if (_toolbarBtnAdded || !ShowToolbarButton) return;
            if (ChartControl == null) return;

            try
            {
                // Resolve the Chart Window that hosts this ChartControl.
                _chartWindow = System.Windows.Window.GetWindow(ChartControl.Parent)
                               as NinjaTrader.Gui.Chart.Chart;
                if (_chartWindow == null || _chartWindow.MainMenu == null) return;

                _toolbarBtn = new NinjaTrader.Gui.Tools.NTMenuItem();
                _toolbarBtn.Header     = "ILT-DEV-PROFILES";
                _toolbarBtn.Style      = System.Windows.Application.Current.TryFindResource("MainMenuItem") as System.Windows.Style;
                _toolbarBtn.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 90, 190, 255));
                _toolbarBtn.ToolTip = "Mostrar / ocultar panel ILT-DEV PROFILES";

                // Stable AutomationId tied to time so a second instance on a
                // different tab doesn't clash with the first (recommended in NT8
                // forum guidance: AutomationIds must be unique per instance).
                _toolbarBtn.SetValue(System.Windows.Automation.AutomationProperties.AutomationIdProperty,
                    "ILT_DEV_TOOLBAR_BTN_" + DateTime.Now.Ticks.ToString());

                _toolbarBtn.Click += ToolbarBtn_Click;

                _chartWindow.MainMenu.Add(_toolbarBtn);
                _toolbarBtnAdded = true;

                if (DebugLog) Print("ILT-DEV-PROFILES: toolbar button added.");
            }
            catch (Exception ex)
            {
                if (DebugLog) Print("BuildToolbarButton error: " + ex.Message);
            }
        }

        private void ToolbarBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // Toggle the user-intent flag. The actual Visibility update happens
            // in the next OnRender pass, which evaluates ShowStatsPanel AND this
            // flag together — so we never fight the render loop here.
            if (!_wpfBuilt)
            {
                BuildWpfPanel();
                _panelUserHidden = false;
                return;
            }
            _panelUserHidden = !_panelUserHidden;
            // When revealing a panel the user has never dragged, recompute the
            // centered position using the now-final chart size.
            if (!_panelUserHidden && !_userMovedPanel)
            {
                _wpfPosInitialized = false;
                PanelX = -1; PanelY = -1;
                PlacePanel();
            }
            if (_wpfPanel != null)
                _wpfPanel.Visibility = _panelUserHidden
                    ? System.Windows.Visibility.Collapsed
                    : System.Windows.Visibility.Visible;
        }

        private void RemoveToolbarButton()
        {
            if (!_toolbarBtnAdded) return;
            try
            {
                if (_toolbarBtn != null)
                    _toolbarBtn.Click -= ToolbarBtn_Click;
                if (_chartWindow != null && _chartWindow.MainMenu != null && _toolbarBtn != null
                    && _chartWindow.MainMenu.Contains(_toolbarBtn))
                    _chartWindow.MainMenu.Remove(_toolbarBtn);
            }
            catch (Exception ex)
            {
                if (DebugLog) Print("RemoveToolbarButton error: " + ex.Message);
            }
            _toolbarBtn = null;
            _chartWindow = null;
            _toolbarBtnAdded = false;
        }

        private void RemoveWpfPanel()
        {
            if (!_wpfBuilt) return;
            try
            {
                if (UserControlCollection != null && _wpfPanel != null
                    && UserControlCollection.Contains(_wpfPanel))
                    UserControlCollection.Remove(_wpfPanel);
            }
            catch (Exception ex) { if (DebugLog) Print("RemoveWpfPanel error: " + ex.Message); }
            _wpfPanel = null; _wpfBody = null; _wpfStatsText = null;
            _wpfHeaderText = null; _wpfMinBtn = null;
            _btnTpoW = _btnVolW = _btnDeltaW = _btnIBW = _btnORW = null;
            _btnExtTpoRthW = _btnExtTpoEthW = _btnExtVpRthW = _btnExtVpEthW = null;
            _btnPerSesW = _btnPerDiaW = _btnPerSemW = null;
            _wpfBuilt = false;
        }

        // ── ROBUST mouse hook for NT8 ──────────────────────────────────────
        // The key insight: NT8's chart marks mouse events as Handled before they
        // reach normal event handlers. Using AddHandler with handledEventsToo=true
        // is the ONLY reliable way to receive these events. This is the same
        // pattern used by ChartTrader and other interactive NT8 indicators.
        //
        // SECOND key insight: NT8's RenderTarget draws in PHYSICAL PIXELS, but WPF
        // mouse events return DIPs (device-independent pixels = logical pixels).
        // On high-DPI displays (125%, 150%, 175%, 200% scaling), these differ.
        // We multiply WPF mouse coords by the DPI scale factor to align with render coords.
        private bool _mouseHooked = false;
        private System.Windows.Input.MouseButtonEventHandler _mouseHandler;
        private float _dpiScaleX = 1f;
        private float _dpiScaleY = 1f;

        private void HookMouseEvents()
        {
            if (_mouseHooked) return;
            if (ChartControl == null) return;

            // Detect DPI scaling factor (e.g. 1.0 = 100%, 1.5 = 150%, 2.0 = 200%)
            try
            {
                var source = System.Windows.PresentationSource.FromVisual(ChartControl);
                if (source != null && source.CompositionTarget != null)
                {
                    var m = source.CompositionTarget.TransformToDevice;
                    _dpiScaleX = (float)m.M11;
                    _dpiScaleY = (float)m.M22;
                }
            }
            catch { _dpiScaleX = _dpiScaleY = 1f; }

            _mouseHandler = new System.Windows.Input.MouseButtonEventHandler(OnChartMouseDown);

            // AddHandler with handledEventsToo=true intercepts events even after
            // the chart has already marked them as handled — this is the magic.
            ChartControl.AddHandler(
                System.Windows.UIElement.PreviewMouseLeftButtonDownEvent,
                _mouseHandler,
                true);

            _mouseHooked = true;

            if (DebugLog)
                Print(string.Format("ILT-DEV-PROFILES: Mouse hooked. DPI scale = {0:F2}x{1:F2}", _dpiScaleX, _dpiScaleY));
        }

        private void UnhookMouseEvents()
        {
            if (!_mouseHooked) return;
            if (ChartControl == null) return;
            try
            {
                if (_mouseHandler != null)
                    ChartControl.RemoveHandler(
                        System.Windows.UIElement.PreviewMouseLeftButtonDownEvent,
                        _mouseHandler);
            }
            catch { }
            _mouseHooked = false;
            _mouseHandler = null;
        }

        private void OnChartMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ChartControl == null) return;

            // Convert WPF DIPs to physical pixels (matching NT8's RenderTarget coords)
            System.Windows.Point pt = e.GetPosition(ChartControl);
            float mx = (float)(pt.X * _dpiScaleX);
            float my = (float)(pt.Y * _dpiScaleY);

            if (DebugLog)
                Print(string.Format("ILT-DEV-PROFILES click DIP({0:F0},{1:F0}) → PX({2:F0},{3:F0}) | TPO:{4} | Vol:{5} | Delta:{6}",
                    pt.X, pt.Y, mx, my, _btnTpoRect, _btnVolRect, _btnDeltaRect));

            bool hit = false;
            if      (_btnMinRect.Contains(mx, my))    { _statsPanelMinimized = !_statsPanelMinimized; hit = true; if (DebugLog) Print("  → Panel minimized = " + _statsPanelMinimized); }
            else if (_btnTpoRect.Contains(mx, my))   { _showTpo      = !_showTpo;      hit = true; if (DebugLog) Print("  → TPO toggled to " + _showTpo); }
            else if (_btnVolRect.Contains(mx, my))   { _showVolBtn   = !_showVolBtn;   hit = true; if (DebugLog) Print("  → Vol toggled to " + _showVolBtn); }
            else if (_btnDeltaRect.Contains(mx, my)) { _showDeltaBtn = !_showDeltaBtn; hit = true; if (DebugLog) Print("  → Delta toggled to " + _showDeltaBtn); }
            else if (_btnIBRect.Contains(mx, my))    { _showIBBtn    = !_showIBBtn;    hit = true; if (DebugLog) Print("  → IB toggled to " + _showIBBtn); }
            else if (_btnORRect.Contains(mx, my))    { _showORBtn    = !_showORBtn;    hit = true; if (DebugLog) Print("  → OR toggled to " + _showORBtn); }
            else if (_btnModeRect.Contains(mx, my))  { CycleSessionMode();             hit = true; if (DebugLog) Print("  → Session mode = " + _sessionModeRt); }

            if (hit)
            {
                e.Handled = true;
                ForceRedraw();
            }
        }

        private void ForceRedraw()
        {
            try
            {
                if (ChartControl != null)
                {
                    ChartControl.InvalidateVisual();
                    if (ChartControl.ChartPanels != null)
                        foreach (var panel in ChartControl.ChartPanels)
                            panel.InvalidateVisual();
                }
            }
            catch { }
        }

        private void PutLabel(string text, float x, float y)
        {
            if (!_labelsOnForProfile) return;
            if (_fmtLabel == null || _dxStatText == null) return;
            if (CullOffscreenPriceLevels && (y < _renderPanelTop || y > _renderPanelBot)) return;
            RenderTarget.DrawText(text, _fmtLabel,
                new RectangleF(x, y - 8f, 80f, 16f), _dxStatText);
        }

        // Two-line label: name on top, price below — both centered horizontally,
        // with tight vertical spacing for a clean compact look.
        // When CompactLabels is on, only the name is drawn (no price line),
        // which dramatically reduces clutter when many sessions are visible.
        private void PutLabelWithPrice(string text, double price, float x, float y)
        {
            if (!_labelsOnForProfile) return;
            if (_fmtLabel == null || _dxStatText == null) return;
            if (CullOffscreenPriceLevels && (y < _renderPanelTop || y > _renderPanelBot)) return;

            float boxW = 90f;
            float boxH = 14f;

            if (CompactLabels)
            {
                // Single compact line: just the name, vertically centered on y.
                RenderTarget.DrawText(text, _fmtLabel,
                    new RectangleF(x, y - 7f, boxW, boxH), _dxStatText);
                return;
            }

            // Top line: label name (centered via cached center format — no measuring).
            RenderTarget.DrawText(text, _fmtLabelCtr,
                new RectangleF(x, y - 13f, boxW, boxH), _dxStatText);

            // Bottom line: price value, centered horizontally.
            RenderTarget.DrawText(price.ToString("F2"), _fmtLabelCtr,
                new RectangleF(x, y - 1f, boxW, boxH), _dxStatText);
        }
    }
}

// =============================================================================
//  INSTALACION
// =============================================================================
//  1. NinjaTrader 8 -> Tools -> Edit NinjaScript -> Indicator -> New
//  2. Pegar todo este codigo (reemplazar el contenido existente)
//  3. F5 para compilar
//  4. Clic derecho grafico -> Indicators -> ILT-DEV PROFILES
//
//  *** IMPORTANTE: ACTIVAR TICK REPLAY PARA DELTA INSTITUCIONAL ***
//  Para que el Delta use Bid/Ask REAL (no aproximacion uptick/downtick) en
//  datos historicos, hay que activar Tick Replay:
//   A. Tools > Options > Market Data > marcar "Show Tick Replay"
//   B. En el grafico: Data Series (Ctrl+F) > marcar "Tick Replay"
//   C. El indicador detecta Tick Replay automaticamente y usa OnMarketData
//      con Bid/Ask real. El panel de Output mostrara "Tick Replay = True".
//
//  Si NO activas Tick Replay:
//   - El delta historico usara aproximacion uptick/downtick (modo fallback)
//   - El delta en tiempo real seguira siendo Bid/Ask real
//   - Modo "DeltaMode": Auto (recomendado) / BidAsk / UpDownTick
//
//  CONFIGURACION RECOMENDADA (ES / NQ):
//   SessionPeriod    = Sesiones (RTH) | Diario | Semanal
//   DeltaMode        = Auto    (usa Bid/Ask real cuando hay cotizacion)
//   SessionStartHHmm = 930    (RTH apertura)
//   SessionEndHHmm   = 1600   (RTH cierre)
//   ONStartHHmm      = 1800   (Overnight empieza)
//   ONEndHHmm        = 929    (Overnight termina antes del RTH)
//   ORMinutes        = 15     (Opening Range de 15 minutos)
//   ProfileOrientation = LeftToRight
//
//  ARQUITECTURA DEL MOTOR (profesional, independiente de temporalidad):
//   Serie 0 (grafico)  -> solo coordenadas de dibujo (anclado por tiempo) + alertas
//   Serie 1 (tick)     -> volumen/delta por trade (fallback sin Tick Replay)
//   Serie 2 (1-minuto) -> muestreo de TIEMPO FIJO que construye el perfil:
//                         TPO + POC/VA + IB/OR + rollover de sesion.
//                         El perfil NO depende de la barra del grafico, por eso
//                         VA/POC/zonas son identicas en Range 40, 5min, 15min, etc.
//   OnMarketData (Tick Replay) -> Buy/Sell por Bid/Ask real, ruteado por timestamp
//        -> Profile Engine (TPO + Volume + BuyVol + SellVol + Delta por precio)
//        -> POC / VA / VPOC con dirty-flag (recalculo solo cuando cambia data)
//
//  NOTA: el indicador agrega internamente una serie de 1 minuto. No necesitas
//  configurarla; se crea sola en State.Configure. El delta institucional sigue
//  dependiendo de Tick Replay como se indica arriba.
// =============================================================================

// =============================================================================
//  NinjaScript generated code intentionally removed.
//  NinjaTrader will regenerate it automatically on F5 compile.
// =============================================================================

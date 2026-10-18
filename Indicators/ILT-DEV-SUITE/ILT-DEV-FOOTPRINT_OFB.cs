// ILT_DEV_FOOTPRINT_OFB
// Standalone NinjaScript source for ILT_Dev_Suite.
// Uses NinjaTrader volumetric Bid/Ask data and owns all Direct2D resources
// through OnRenderTargetChanged; it has no external order-flow DLL dependency.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Serialization;
using NinjaTrader.Core;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.NinjaScript;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript.BarsTypes;
using NinjaTrader.NinjaScript.DrawingTools;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Ellipse = SharpDX.Direct2D1.Ellipse;
using FontFamily = System.Windows.Media.FontFamily;
using Point = System.Windows.Point;
using SolidColorBrush = SharpDX.Direct2D1.SolidColorBrush;
using TextAlignment = System.Windows.TextAlignment;

namespace NinjaTrader.NinjaScript
{
	public enum IltDevFootprintDisplayMode
	{
		SidedLadder = 1,
		SidedLadderPlus = 2,
		DeltaLadder = 3,
		Profile = 4,
		DeltaProfile = 5
	}
}

namespace NinjaTrader.NinjaScript.Indicators.ILT_DEV_SUITE
{
	public class ILT_DEV_FOOTPRINT_OFB : Indicator
	{
    internal sealed class ILT_DEV_FOTPRINT_Level
    {
        public long BidVol;
        public long AskVol;
        public long SpreadVol;

        public long Total { get { return BidVol + AskVol; } }
        public long Delta { get { return AskVol - BidVol; } }
    }

    public sealed class ILT_DEV_FOTPRINT_ImbalanceExport
    {
        public double Price;
        public string Side;
        public long DominantVolume;
        public long OppositeVolume;
        public double Ratio;
        public long Delta;

        public ILT_DEV_FOTPRINT_ImbalanceExport Clone()
        {
            return new ILT_DEV_FOTPRINT_ImbalanceExport
            {
                Price = Price,
                Side = Side,
                DominantVolume = DominantVolume,
                OppositeVolume = OppositeVolume,
                Ratio = Ratio,
                Delta = Delta
            };
        }
    }

    public sealed class ILT_DEV_FOTPRINT_ExportState
    {
        public string InstrumentFullName;
        public string InstrumentRoot;
        public string BarsPeriod;
        public string Source;
        public string Status;
        public DateTime Timestamp;
        public long DataVersion;
        public int BarIndex;
        public DateTime BarTime;
        public double BarPOC;
        public double BarDPOC;
        public long BarPOCVolume;
        public long BarPOCDelta;
        public long BarBidVolume;
        public long BarAskVolume;
        public long BarDelta;
        public long BarTotalVolume;
        public double SessionPOC;
        public double SessionDPOC;
        public long SessionPOCVolume;
        public long SessionTotalVolume;
        public long SessionBidVolume;
        public long SessionAskVolume;
        public long SessionDelta;
        public int BuyImbalanceCount;
        public int SellImbalanceCount;
        public bool StackedImbalance;
        public string StackedDirection;
        public int StackedRowsRequired;
        public List<ILT_DEV_FOTPRINT_ImbalanceExport> ImbalanceLevels = new List<ILT_DEV_FOTPRINT_ImbalanceExport>();

        public ILT_DEV_FOTPRINT_ExportState Clone()
        {
            ILT_DEV_FOTPRINT_ExportState copy = (ILT_DEV_FOTPRINT_ExportState)MemberwiseClone();
            copy.ImbalanceLevels = new List<ILT_DEV_FOTPRINT_ImbalanceExport>();
            if (ImbalanceLevels != null)
            {
                foreach (ILT_DEV_FOTPRINT_ImbalanceExport level in ImbalanceLevels)
                    if (level != null)
                        copy.ImbalanceLevels.Add(level.Clone());
            }
            return copy;
        }
    }
		private class LionSignalPayload
		{
			public int Code;

			public int ColorArgb;

			public bool ShowLabel;

			public bool ShowArrow;

			public string Label;

			public bool DrawAbove;

			public int MarkerOffsetTicks;
		}

		private struct BarImbCacheEntry
		{
			public long DataVersion;

			public int ParamSig;

			public HashSet<int> Buy;

			public HashSet<int> Sell;
		}

		private struct BarVaCacheEntry
		{
			public long DataVersion;

			public int ParamSig;

			public HashSet<int> Keys;
		}

		private class ExtendedImbalanceLineState
		{
			public int OriginBar;

			public int EndBar;

			public int PriceKey;

			public bool Buy;

			public bool Sell;

			public bool Mitigated;

			public long OriginBid;

			public long OriginAsk;

			public long OriginTotal;

			public long OriginDelta;

			public long OriginMaxBid;

			public long OriginMaxAsk;

			public long OriginMaxTotal;

			public long OriginMaxAbsDelta;
		}

		private class CandleImbalanceMarker
		{
			public int BarIndex;

			public int PriceKey;

			public bool Buy;

			public bool Sell;
		}

		private const string ILTPUB_DEV_SESSIONFOOT = "<RSAKeyValue><Modulus>6Xi/RSxNtqILBIrligimoZIBittn99yzL/b2FjKmEtnWUL/mCOObtBbvdskEhXx6ep44BthxahLWtwaFZQoSBjo3OrJO/pA267uTUAFZ0kfXEvggyBrwKfw8uMFs9i87yNUL5QiOTujfy7Z/WvYhGdIg+G9JSLZV9iOzXVwFXCz1786JMzrm4JXkgldP9zqQ4dVX0VO4XvQA8VhRh/t2lQ6bx89Oy5amS6NdNGGdV5+oLLDScwJ0xaVwBLvnqNk0KAbripykuwt2BfEQYB5YJv8PfPO641IXRsPJnrh/7wWLAZ9oL/A7VzB32H7KHNyEWoiW4JSit8KI0WoInC++qQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

		private const int MAX_FOOTPRINT_EXPORT_IMBALANCE_LEVELS = 12;

		private const int LION_NONE = 0;

		private const int LION_ACEL_BUY = 1;

		private const int LION_ACEL_SELL = -1;

		private const int LION_EXC_BUY = 2;

		private const int LION_EXC_SELL = -2;

		private const int LION_ROT_BUY = 3;

		private const int LION_ROT_SELL = -3;

		private const int LION_ABS_BUY = 4;

		private const int LION_ABS_SELL = -4;

		private const int MAX_VISIBLE_EXTENDED_IMBALANCE_LINES_PER_RENDER = 6000;

		private const int EXTENDED_IMBALANCE_CACHE_REFRESH_MS_REALTIME = 350;

		private const int MAX_VISIBLE_FOOTPRINT_BARS = 160;

		private const int MAX_CANDLE_FOOTPRINT_ROWS_PER_RENDER = 8500;

		private const int MAX_EXTENDED_IMBALANCE_SCAN_BARS = 220;

		private const int MAX_EXTENDED_LINES_PER_RENDER = 250;

		private const int MAX_IMBALANCE_MARKERS_ONLY_PER_RENDER = int.MaxValue;

		private const int MAX_SESSION_LADDER_VISIBLE_ROWS = 950;

		private const int MAX_SESSION_LADDER_TOTAL_ROWS_FOR_FULL_RENDER = 2200;

		private const float MIN_FOOTPRINT_BAR_SPACING_PX = 5.25f;

		private const float MIN_EXTENDED_LINE_BAR_SPACING_PX = 7.5f;

		private const float MIN_SESSION_LADDER_ROW_HEIGHT_PX = 0.72f;

		private const int MAX_CACHED_BARS = 600;

		private bool? iltDevOk_ILT_DEV_SESSIONFOOT = null;

		private string iltDevMotivo_ILT_DEV_SESSIONFOOT = "";

		private string iltDevFp_ILT_DEV_SESSIONFOOT;

		private static readonly object _footprintExportBusLock = new object();

		private static readonly Dictionary<string, ILT_DEV_FOTPRINT_ExportState> _footprintExportBus = new Dictionary<string, ILT_DEV_FOTPRINT_ExportState>();

		private long _lastFootprintExportMs;

		private static readonly object _lionSignalBusLock = new object();

		private static readonly Dictionary<string, Dictionary<int, LionSignalPayload>> _lionSignalBusByBar = new Dictionary<string, Dictionary<int, LionSignalPayload>>();

		private static readonly Dictionary<string, Dictionary<long, LionSignalPayload>> _lionSignalBusByTime = new Dictionary<string, Dictionary<long, LionSignalPayload>>();

		private readonly Dictionary<int, LionSignalPayload> _lionSignalLocalByBar = new Dictionary<int, LionSignalPayload>();

		private readonly Dictionary<long, LionSignalPayload> _lionSignalLocalByTime = new Dictionary<long, LionSignalPayload>();

		private readonly object _lk = new object();

		private Dictionary<int, ILT_DEV_FOTPRINT_Level> _sessionFp;

		private Dictionary<int, Dictionary<int, ILT_DEV_FOTPRINT_Level>> _barFp;

		private DateTime _sessionDate = DateTime.MinValue;

		private TimeZoneInfo _nyTz;

		private double _lastBid;

		private double _lastAsk = double.MaxValue;

		private double _lastTradePrice;

		private int _lastTradeDir;

		private long _lastRealtimeTradeMs;

		private double _lastUiPrice;

		private int _lastUiDir;

		private long _lastPriceLineMs;

		private long _lastPriceLineRefreshMs;

		private int _sessionPocKey = int.MinValue;

		private long _sessionPocVol;

		private long _sessionTotalVol;

		private long _sessionMaxDeltaAbs = 1L;

		private int _lastProcessedHistoricalBar = -1;

		private int _lastNativeVolBarProcessed = -1;

		private long _lastNativeRealtimeRefreshMs;

		private int _volToPrimaryBarOffset = int.MinValue;

		private int _lastDevVolBarIndex = -1;

		private long _lastDevVolBarProcessMs;

		private readonly Dictionary<int, long> _barFpTotals = new Dictionary<int, long>();

		private Dictionary<int, ILT_DEV_FOTPRINT_Level> _sessionLadderSnapshot;

		private long _sessionLadderSnapshotVersion = -1L;

		private static readonly HashSet<int> _emptyKeySet = new HashSet<int>();

		private readonly List<int> _ladderKeyBuffer = new List<int>(512);

		private int _diagVolBarsSummed;

		private DateTime _diagFirstVolBarTime = DateTime.MinValue;

		private DateTime _diagLastVolBarTime = DateTime.MinValue;

		private long _diagTotalBid;

		private long _diagTotalAsk;

		private VolumetricBarsType _nativeVolBarsType;

		private VolumetricBarsType _sessionVolBarsType;

		private SessionIterator _nativeSessionIterator;

		private SessionIterator _primarySessionIterator;

		private DateTime _nativeSessionBegin = DateTime.MinValue;

		private DateTime _nativeSessionEnd = DateTime.MinValue;

		private double _tick;

		private int _decs;

		private int _configuredTicksPerRow = 1;

		private SolidColorBrush _bBg;

		private SolidColorBrush _bPanel;

		private SolidColorBrush _bGrid;

		private SolidColorBrush _bText;

		private SolidColorBrush _bDim;

		private SolidColorBrush _bBid;

		private SolidColorBrush _bAsk;

		private SolidColorBrush _bDeltaPos;

		private SolidColorBrush _bDeltaNeg;

		private SolidColorBrush _bDeltaZero;

		private SolidColorBrush _bVol;

		private SolidColorBrush _bFootprintBase;

		private SolidColorBrush _bPoc;

		private SolidColorBrush _bSummaryTotal;

		private SolidColorBrush _bVaBuy;

		private SolidColorBrush _bVaSell;

		private SolidColorBrush _bImbBuy;

		private SolidColorBrush _bImbSell;

		private SolidColorBrush _bNeutral;

		private SolidColorBrush _bPriceUp;

		private SolidColorBrush _bPriceDown;

		private SolidColorBrush _bPriceNeutral;

		private SolidColorBrush _bBlockBull;

		private SolidColorBrush _bBlockBear;

		private SolidColorBrush _bBlockNeutral;

		private SolidColorBrush _bLionAcelBuy;

		private SolidColorBrush _bLionAcelSell;

		private SolidColorBrush _bLionExcessBuy;

		private SolidColorBrush _bLionExcessSell;

		private SolidColorBrush _bLionRotationBuy;

		private SolidColorBrush _bLionRotationSell;

		private SolidColorBrush _bLionAbsorptionBuy;

		private SolidColorBrush _bLionAbsorptionSell;

		private readonly Dictionary<int, SolidColorBrush> _lionExternalBrushCache = new Dictionary<int, SolidColorBrush>();

		private TextFormat _fSmall;

		private TextFormat _fText;

		private TextFormat _fBold;

		private bool _dxOk;

		private string _lastResourceSignature = "";

		private float _lastDpi = 1f;

		private HashSet<int> _lastVaKeys = new HashSet<int>();

		private long _lastVaCalcVolume = -1L;

		private Dictionary<string, RectangleF> _panelHits = new Dictionary<string, RectangleF>();

		private float _lastDX = 1f;

		private float _lastDY = 1f;

		private float _lastRenderW = 1f;

		private float _lastRenderH = 1f;

		private float _lastPlotLeft;

		private float _lastPlotRight = 1f;

		private long _lastBetaRenderErrorTicks;

		private bool _panelMouseAttached;

		private bool _panelCollapsed;

		private RectangleF _controlPanelRect = new RectangleF(0f, 0f, 0f, 0f);

		private RectangleF _controlPanelDragHandleRect = new RectangleF(0f, 0f, 0f, 0f);

		private bool _controlPanelDragging;

		private float _controlPanelDragOffsetX;

		private float _controlPanelDragOffsetY;

		private Dictionary<long, long> _imbalancePulseStartMs = new Dictionary<long, long>();

		private HashSet<long> _knownCandleImbalanceIds = new HashSet<long>();

		private bool _imbalancePulsePrimed;

		private DateTime _imbalancePulseSessionDate = DateTime.MinValue;

		private long _lastImbalancePulseRefreshMs;

		private Dictionary<int, HashSet<int>> _renderCandleBuyImbalanceCache = new Dictionary<int, HashSet<int>>();

		private Dictionary<int, HashSet<int>> _renderCandleSellImbalanceCache = new Dictionary<int, HashSet<int>>();

		private readonly Dictionary<int, BarImbCacheEntry> _candleImbKeyCache = new Dictionary<int, BarImbCacheEntry>();

		private readonly Dictionary<int, BarVaCacheEntry> _candleVaKeyCache = new Dictionary<int, BarVaCacheEntry>();

		private readonly object _extendedImbalanceCacheLock = new object();

		private List<ExtendedImbalanceLineState> _extendedImbalanceLineCache = new List<ExtendedImbalanceLineState>();

		private bool _extendedImbalanceCacheDirty = true;

		private long _footprintDataVersion;

		private long _extendedImbalanceCacheDataVersion = -1L;

		private int _extendedImbalanceCacheParamSignature = int.MinValue;

		private long _lastExtendedImbalanceCacheBuildMs;

		private RectangleF _statsWindowRect = new RectangleF(0f, 0f, 0f, 0f);

		private RectangleF _statsDragHandleRect = new RectangleF(0f, 0f, 0f, 0f);

		private bool _statsDragging;

		private float _statsDragOffsetX;

		private float _statsDragOffsetY;

		private long _sessionMaxVolAbs = 1L;

		private Grid _wpfPanel;

		private StackPanel _wpfBody;

		private TextBlock _wpfHeaderText;

		private Button _wpfMinBtn;

		private Button _btnLadderImbW;

		private Button _btnCandleImbW;

		private Button _btnExtImbW;

		private Button _btnBarSummaryW;

		private Button _btnNewImbAnimW;

		private Button _btnSessLadW;

		private Button _btnSessVaW;

		private Button _btnCandleFpW;

		private Button _btnCandleVaW;

		private Button _btnSessDeltaW;

		private Button _btnBidAskW;

		private Button _btnCandleProfileW;

		private Button _btnPriceLineW;

		private Button _btnLadSideW;

		private Button _btnRowGridW;

		private Button _btnAutoFpW;

		private Button _btnSessVolW;

		private Button _btnShowNumbersW;

		private ComboBox _cmbVisualModeW;

		private ComboBox _cmbColorPaletteW;

		private TextBlock _lblTicksPerRow;

		private TextBlock _lblSessionMin;

		private TextBlock _lblMinVolume;

		private TextBlock _lblValueAreaPct;

		private TextBlock _lblLadderPct;

		private TextBlock _lblLadderMin;

		private TextBlock _lblLadderStack;

		private TextBlock _lblCandlePct;

		private TextBlock _lblCandleMin;

		private TextBlock _lblCandleStack;

		private TextBlock _lblExtLineW;

		private TextBlock _lblExtLookback;

		private TextBlock _lblDeltaDir;

		private TextBlock _lblLadderPositionW;

		private TextBlock _lblLadderW;

		private TextBlock _lblFpMetric;

		private TextBlock _lblFpWidth;

		private TextBlock _lblFpOffset;

		private TextBlock _lblFlashOp;

		private TextBlock _lblFlashW;

		private TextBlock _lblFlashMs;

		private TextBlock _lblVolProfileW;

		private TextBlock _lblVolProfileDir;

		private TextBlock _lblVolProfileOp;

		private TextBlock _lblFontSizeW;

		private TextBlock _lblMinRowHeightW;

		private TextBlock _lblFootprintOpacityW;

		private TextBlock _lblLadderOpacityW;

		private TextBlock _lblSummaryHeightW;

		private TextBlock _lblSummaryGapW;

		private TextBlock _lblSummaryBorderW;

		private bool _wpfBuilt;

		private bool _wpfDragging;

		private Point _wpfDragStart;

		private double _wpfStartLeft;

		private double _wpfStartTop;

		private bool _wpfPosInitialized;

		private bool _userMovedPanel;

		private bool _panelUserHidden;

		private string _runtimeSettingsPath = string.Empty;

		private bool _runtimeSettingsLoaded;

		private NTMenuItem _toolbarBtn;

		private Chart _chartWindow;

		private bool _toolbarBtnAdded;

		private readonly List<int> _cacheEvictBuffer = new List<int>(64);

		[XmlIgnore]
		[Browsable(false)]
		public bool UseNativeVolumetricBidAsk { get; set; }

		[XmlIgnore]
		[Browsable(false)]
		public bool LoadHistoricalSession { get; set; }

		[NinjaScriptProperty]
		[Range(1, 20)]
		[Display(Name = "Ticks Per Row / Volumetric Ticks Per Level", GroupName = "01. Engine", Order = 0)]
		public int TicksPerRow { get; set; }

		[Browsable(false)]
		[Range(100, 5000)]
		[Display(Name = "Native Refresh MS", GroupName = "01. Engine", Order = 1)]
		[NinjaScriptProperty]
		public int NativeRefreshMs { get; set; }

		[NinjaScriptProperty]
		[Range(33, 250)]
		[Display(Name = "Visual Refresh MS", GroupName = "01. Engine", Order = 2)]
		[Browsable(false)]
		public int VisualRefreshMs { get; set; }

		[Range(1, 1440)]
		[NinjaScriptProperty]
		[Display(Name = "Session Volumetric Minutes", GroupName = "01. Engine", Order = 3)]
		[Browsable(false)]
		public int SessionVolumetricMinutes { get; set; }

		[Range(1, 2)]
		[NinjaScriptProperty]
		[Display(Name = "Session Time Mode (1 Chart / 2 NY)", GroupName = "01. Engine", Order = 4)]
		[Browsable(false)]
		public int SessionTimeMode { get; set; }

		[NinjaScriptProperty]
		[Browsable(false)]
		[Display(Name = "Show Data Diagnostics", GroupName = "01. Engine", Order = 5)]
		public bool ShowDataDiagnostics { get; set; }

		[NinjaScriptProperty]
		[Browsable(false)]
		[Range(0, 23)]
		[Display(Name = "Session Start Hour", GroupName = "01. Engine", Order = 6)]
		public int SessionStartHourNY { get; set; }

		[Display(Name = "Session End Hour", GroupName = "01. Engine", Order = 7)]
		[NinjaScriptProperty]
		[Range(0, 23)]
		[Browsable(false)]
		public int SessionEndHourNY { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Show Side Ladder", GroupName = "02. Display", Order = 1)]
		public bool ShowSessionLadder { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Visual Mode", Description = "Sided Ladder, Sided Ladder Plus, Delta Ladder, Profile or Delta Profile.", GroupName = "02. Display", Order = 0)]
		public IltDevFootprintDisplayMode VisualMode { get; set; }

		[NinjaScriptProperty]
		[Browsable(false)]
		[Display(Name = "Show Session Delta Profile", GroupName = "02. Display", Order = 1)]
		public bool ShowSessionDeltaProfile { get; set; }

		[Display(Name = "Show Session Volume Profile", GroupName = "02. Display", Order = 1)]
		[NinjaScriptProperty]
		[Browsable(false)]
		public bool ShowSessionVolumeProfile { get; set; }

		[Display(Name = "Show Session BID x ASK Column", GroupName = "02. Display", Order = 2)]
		[NinjaScriptProperty]
		[Browsable(false)]
		public bool ShowSessionBidAskColumn { get; set; }

		[Browsable(false)]
		[Display(Name = "Panel WPF flotante", GroupName = "02. Display", Order = 2, Description = "Usa panel WPF flotante (recomendado). Falso = panel SharpDX legacy dentro del chart.")]
		[NinjaScriptProperty]
		public bool UseWpfPanel { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Boton en toolbar superior", GroupName = "02. Display", Order = 3, Description = "Anade un boton 'ILT-DEV Footprint' en la barra superior del chart que muestra/oculta el panel.")]
		[Browsable(false)]
		public bool ShowToolbarButton { get; set; }

		[Browsable(false)]
		public double PanelXW { get; set; }

		[Browsable(false)]
		public double PanelYW { get; set; }

		[Browsable(false)]
		public bool PanelMinimizedPersist { get; set; }

		[Display(Name = "Show Candle Footprint", GroupName = "02. Display", Order = 1)]
		[NinjaScriptProperty]
		public bool ShowCandleFootprint { get; set; }

		[Display(Name = "Show Candle Volume/Delta Profile", GroupName = "02. Display", Order = 2, Description = "Oculta solo el histograma vela a vela; mantiene VA, imbalances y lineas extendidas.")]
		[NinjaScriptProperty]
		[Browsable(false)]
		public bool ShowCandleProfileHistogram { get; set; }

		[NinjaScriptProperty]
		[Browsable(false)]
		[Range(1, 2)]
		[Display(Name = "Candle Mode (Native Only)", GroupName = "02. Display", Order = 0)]
		public int FootprintCandleMode { get; set; }

		[Browsable(false)]
		[Display(Name = "Show Block Candles", GroupName = "02. Display", Order = 1)]
		public bool ShowBlockCandles { get; set; }

		[Browsable(false)]
		[NinjaScriptProperty]
		[Display(Name = "Hide Native Candles With Blocks", Description = "OFF lets ILT-DEV FLOWSPEED paint native signal candles above the block footprint.", GroupName = "02. Display", Order = 2)]
		public bool HideNativeCandlesWithBlocks { get; set; }

		[NinjaScriptProperty]
		[Browsable(false)]
		[Display(Name = "Show Candle Value Area", GroupName = "02. Display", Order = 2)]
		public bool ShowCandleValueArea { get; set; }

		[Display(Name = "Show Session Value Area", GroupName = "02. Display", Order = 3)]
		[NinjaScriptProperty]
		[Browsable(false)]
		public bool ShowSessionValueArea { get; set; }

		[NinjaScriptProperty]
		[Browsable(false)]
		[Display(Name = "Show Current Price Line", GroupName = "02. Display", Order = 4)]
		public bool ShowCurrentPriceLine { get; set; }

		[Display(Name = "Highlight Imbalance Values", GroupName = "02. Display", Order = 5)]
		[NinjaScriptProperty]
		public bool ShowCandleImbalances { get; set; }

		[NinjaScriptProperty]
		[Browsable(false)]
		[Display(Name = "Show Session Ladder Imbalances", GroupName = "02. Display", Order = 6)]
		public bool ShowSessionLadderImbalances { get; set; }

		[NinjaScriptProperty]
		[Browsable(false)]
		[Display(Name = "Show Extended Imbalance Lines", GroupName = "02. Display", Order = 7)]
		public bool ShowExtendedImbalanceLines { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Show Bar Delta Summary", GroupName = "02. Display", Order = 8)]
		public bool ShowBarSummary { get; set; }

		[Display(Name = "Show New Imbalance Flash", GroupName = "02. Display", Order = 8)]
		[NinjaScriptProperty]
		[Browsable(false)]
		public bool ShowNewImbalanceAnimation { get; set; }

		[XmlIgnore]
		[Browsable(false)]
		public bool ShowImbalances { get; set; }

		[Browsable(false)]
		[NinjaScriptProperty]
		[Display(Name = "Show BID x ASK Text", GroupName = "02. Display", Order = 9)]
		public bool ShowBidAskText { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Show Spread In Total", GroupName = "02. Display", Order = 10)]
		[Browsable(false)]
		public bool ShowSpreadInTotal { get; set; }

		[XmlIgnore]
		[Browsable(false)]
		public bool ShowPriceColumn { get; set; }

		[NinjaScriptProperty]
		[Browsable(false)]
		[Display(Name = "Show Control Panel", GroupName = "02. Display", Order = 12)]
		public bool ShowControlPanel { get; set; }

		[Display(Name = "Side Ladder Side (1 Right / 2 Left)", GroupName = "02. Display", Order = 2)]
		[NinjaScriptProperty]
		[Range(1, 2)]
		public int LadderSide { get; set; }

		[XmlIgnore]
		[Browsable(false)]
		public int LadderPositionPct { get; set; }

		[NinjaScriptProperty]
		[Browsable(false)]
		[Display(Name = "Ladder Imbalance %", GroupName = "03. Parameters", Order = 0)]
		[Range(100, 2000)]
		public int LadderImbalancePct { get; set; }

		[Display(Name = "Ladder Min Imbalance", GroupName = "03. Parameters", Order = 1)]
		[NinjaScriptProperty]
		[Browsable(false)]
		[Range(1, 100000)]
		public int LadderMinImbalance { get; set; }

		[Display(Name = "Ladder Imbalance Stack Rows", GroupName = "03. Parameters", Order = 2)]
		[Range(1, 5)]
		[NinjaScriptProperty]
		[Browsable(false)]
		public int LadderImbalanceStackRows { get; set; }

		[Display(Name = "Candle Imbalance %", GroupName = "03. Parameters", Order = 3)]
		[NinjaScriptProperty]
		[Range(100, 2000)]
		public int CandleImbalancePct { get; set; }

		[Display(Name = "Candle Min Imbalance", GroupName = "03. Parameters", Order = 4)]
		[Range(1, 100000)]
		[NinjaScriptProperty]
		public int CandleMinImbalance { get; set; }

		[Display(Name = "Candle Imbalance Stack Rows", GroupName = "03. Parameters", Order = 5)]
		[Range(1, 5)]
		[NinjaScriptProperty]
		[Browsable(false)]
		public int CandleImbalanceStackRows { get; set; }

		[Display(Name = "Count Zero-Print Imbalance", GroupName = "03. Parameters", Order = 6)]
		[NinjaScriptProperty]
		public bool CountZeroPrintImbalance { get; set; }

		[NinjaScriptProperty]
		[Browsable(false)]
		[Display(Name = "Candle Value Area %", GroupName = "03. Parameters", Order = 7)]
		[Range(1, 99)]
		public int ValueAreaPct { get; set; }

		[Display(Name = "Min Volume To Display", GroupName = "03. Parameters", Order = 8)]
		[NinjaScriptProperty]
		[Range(0, 1000000)]
		public int MinVolumeToDisplay { get; set; }

		[Display(Name = "Control Panel Width", GroupName = "04. Layout", Order = 0)]
		[Browsable(false)]
		[NinjaScriptProperty]
		[Range(160, 360)]
		public int ControlPanelWidth { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Control Panel X", GroupName = "04. Layout", Order = 1)]
		[Range(0, 5000)]
		[Browsable(false)]
		public int ControlPanelX { get; set; }

		[Display(Name = "Control Panel Y", GroupName = "04. Layout", Order = 2)]
		[Browsable(false)]
		[NinjaScriptProperty]
		[Range(0, 5000)]
		public int ControlPanelY { get; set; }

		[NinjaScriptProperty]
		[Range(80, 480)]
		[Display(Name = "Side Ladder Width", GroupName = "04. Layout", Order = 0)]
		public int SessionLadderWidth { get; set; }

		[Browsable(false)]
		[NinjaScriptProperty]
		[Range(40, 180)]
		[Display(Name = "BID x ASK Column Width", GroupName = "04. Layout", Order = 4)]
		public int BidAskColumnWidth { get; set; }

		[Range(24, 140)]
		[Browsable(false)]
		[Display(Name = "Delta Column Width", GroupName = "04. Layout", Order = 5)]
		[NinjaScriptProperty]
		public int DeltaColumnWidth { get; set; }

		[Range(40, 220)]
		[NinjaScriptProperty]
		[Display(Name = "Volume Profile Width", GroupName = "04. Layout", Order = 6)]
		[Browsable(false)]
		public int VolumeProfileWidth { get; set; }

		[Browsable(false)]
		[XmlIgnore]
		public int PriceColumnWidth { get; set; }

		[Browsable(false)]
		[NinjaScriptProperty]
		[Range(1, 3)]
		[Display(Name = "Volume Profile Direction (1 Left / 2 Center / 3 Right)", GroupName = "04. Layout", Order = 7)]
		public int VolumeProfileDirection { get; set; }

		[Range(1, 3)]
		[Display(Name = "Delta Histogram Direction (1 Left / 2 Center / 3 Right)", GroupName = "04. Layout", Order = 8)]
		[NinjaScriptProperty]
		[Browsable(false)]
		public int DeltaHistogramDirection { get; set; }

		[Browsable(false)]
		[NinjaScriptProperty]
		[Display(Name = "Show Session Row Grid", GroupName = "04. Layout", Order = 9)]
		public bool ShowSessionRowGrid { get; set; }

		[Display(Name = "Candle Footprint Width", GroupName = "04. Layout", Order = 10)]
		[NinjaScriptProperty]
		[Range(8, 100)]
		public int CandleFootprintWidth { get; set; }

		[Browsable(false)]
		[NinjaScriptProperty]
		[Range(-100, 100)]
		[Display(Name = "Candle Footprint Offset", GroupName = "04. Layout", Order = 11)]
		public int CandleFootprintOffset { get; set; }

		[NinjaScriptProperty]
		[Browsable(false)]
		[Display(Name = "Auto Resize Candle Footprint Width", GroupName = "04. Layout", Order = 12)]
		public bool AutoResizeCandleFootprint { get; set; }

		[NinjaScriptProperty]
		[Browsable(false)]
		[Range(1, 2)]
		[Display(Name = "Candle Footprint Bar Metric (1 Volume / 2 Delta)", GroupName = "04. Layout", Order = 13)]
		public int CandleFootprintBarMetric { get; set; }

		[NinjaScriptProperty]
		[Browsable(false)]
		[Range(1, 12)]
		[Display(Name = "Bar Summary Height", GroupName = "04. Layout", Order = 14)]
		public int SummaryBarHeight { get; set; }

		[NinjaScriptProperty]
		[Browsable(false)]
		[Range(0, 30)]
		[Display(Name = "Bar Summary Gap", GroupName = "04. Layout", Order = 15)]
		public int SummaryGap { get; set; }

		[NinjaScriptProperty]
		[Browsable(false)]
		[Range(1, 5)]
		[Display(Name = "Bar Summary Border", GroupName = "04. Layout", Order = 16)]
		public int SummaryBorderWidth { get; set; }

		[NinjaScriptProperty]
		[Browsable(false)]
		[Range(1, 2)]
		[Display(Name = "Block Candle Color Mode (1 Dir / 2 Delta)", GroupName = "04. Layout", Order = 14)]
		public int BlockCandleColorMode { get; set; }

		[Browsable(false)]
		[NinjaScriptProperty]
		[Range(1, 2)]
		[Display(Name = "Block Candle Draw Mode (1 Body / 2 Volume Rows)", GroupName = "04. Layout", Order = 15)]
		public int BlockCandleDrawMode { get; set; }

		[Display(Name = "Block Candle Width", GroupName = "04. Layout", Order = 16)]
		[Browsable(false)]
		[NinjaScriptProperty]
		[Range(1, 40)]
		public int BlockCandleWidth { get; set; }

		[Display(Name = "Block Candle Min Width", GroupName = "04. Layout", Order = 17)]
		[Browsable(false)]
		[NinjaScriptProperty]
		[Range(1, 20)]
		public int BlockCandleMinWidth { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Block Candle Max Width", GroupName = "04. Layout", Order = 18)]
		[Browsable(false)]
		[Range(1, 60)]
		public int BlockCandleMaxWidth { get; set; }

		[Browsable(false)]
		[NinjaScriptProperty]
		[Range(0, 8)]
		[Display(Name = "Block Candle Row Gap Pixels", GroupName = "04. Layout", Order = 19)]
		public int BlockCandleRowGapPixels { get; set; }

		[Range(5, 100)]
		[NinjaScriptProperty]
		[Display(Name = "Block Candle Opacity", GroupName = "04. Layout", Order = 20)]
		[Browsable(false)]
		public int BlockCandleOpacity { get; set; }

		[NinjaScriptProperty]
		[Browsable(false)]
		[Display(Name = "Show Block Candle POC", GroupName = "04. Layout", Order = 21)]
		public bool ShowBlockCandlePoc { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Stats Window X", GroupName = "04. Layout", Order = 14)]
		[Range(0, 5000)]
		[Browsable(false)]
		public int StatsWindowX { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Stats Window Y", GroupName = "04. Layout", Order = 15)]
		[Range(0, 5000)]
		[Browsable(false)]
		public int StatsWindowY { get; set; }

		[Display(Name = "Stats Window Width", GroupName = "04. Layout", Order = 16)]
		[NinjaScriptProperty]
		[Range(240, 620)]
		[Browsable(false)]
		public int StatsWindowWidth { get; set; }

		[Range(1, 30)]
		[NinjaScriptProperty]
		[Display(Name = "Min Row Height For Text", GroupName = "04. Layout", Order = 17)]
		[Browsable(false)]
		public int MinRowHeightForText { get; set; }

		[Range(6, 24)]
		[NinjaScriptProperty]
		[Browsable(false)]
		[Display(Name = "Font Size Small", GroupName = "05. Visual", Order = 0)]
		public int FontSizeSmall { get; set; }

		[NinjaScriptProperty]
		[Browsable(false)]
		[Range(6, 28)]
		[Display(Name = "Font Size Text", GroupName = "05. Visual", Order = 1)]
		public int FontSizeText { get; set; }

		[NinjaScriptProperty]
		[Browsable(false)]
		[Range(6, 30)]
		[Display(Name = "Font Size Bold", GroupName = "05. Visual", Order = 2)]
		public int FontSizeBold { get; set; }

		[Browsable(false)]
		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name = "Ladder Opacity %", GroupName = "05. Visual", Order = 3)]
		public int LadderOpacity { get; set; }

		[Range(0, 100)]
		[NinjaScriptProperty]
		[Browsable(false)]
		[Display(Name = "Footprint Opacity %", GroupName = "05. Visual", Order = 4)]
		public int FootprintOpacity { get; set; }

		[NinjaScriptProperty]
		[Browsable(false)]
		[Range(0, 100)]
		[Display(Name = "Value Area Opacity %", GroupName = "05. Visual", Order = 5)]
		public int ValueAreaOpacity { get; set; }

		[Browsable(false)]
		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name = "Volume Profile Opacity %", GroupName = "05. Visual", Order = 6)]
		public int VolumeProfileOpacity { get; set; }

		[Range(0, 100)]
		[NinjaScriptProperty]
		[Display(Name = "Delta Opacity %", GroupName = "05. Visual", Order = 7)]
		[Browsable(false)]
		public int DeltaOpacity { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name = "Imbalance Line Opacity %", GroupName = "05. Visual", Order = 8)]
		public int ImbalanceLineOpacity { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Extended Imbalance Line Width", GroupName = "05. Visual", Order = 9)]
		[Range(1, 10)]
		public int ExtendedImbalanceLineWidth { get; set; }

		[Display(Name = "Extended Imbalance Lookback Bars", GroupName = "05. Visual", Order = 10)]
		[NinjaScriptProperty]
		[Range(50, 10000)]
		[Browsable(false)]
		public int ExtendedImbalanceLookbackBars { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "New Imbalance Flash Opacity %", GroupName = "05. Visual", Order = 11)]
		[Range(0, 100)]
		public int NewImbalanceAnimationOpacity { get; set; }

		[Display(Name = "New Imbalance Flash Width", GroupName = "05. Visual", Order = 12)]
		[NinjaScriptProperty]
		[Range(1, 14)]
		public int NewImbalanceAnimationLineWidth { get; set; }

		[Display(Name = "New Imbalance Flash MS", GroupName = "05. Visual", Order = 12)]
		[NinjaScriptProperty]
		[Range(200, 5000)]
		public int NewImbalanceAnimationMs { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name = "Current Price Line Opacity", GroupName = "05. Visual", Order = 13)]
		[Browsable(false)]
		public int CurrentPriceLineOpacity { get; set; }

		[Display(Name = "Current Price Pulse MS", GroupName = "05. Visual", Order = 14)]
		[Range(200, 5000)]
		[NinjaScriptProperty]
		[Browsable(false)]
		public int CurrentPriceLinePulseMs { get; set; }

		[Range(1, 8)]
		[NinjaScriptProperty]
		[Display(Name = "Current Price Line Width", GroupName = "05. Visual", Order = 15)]
		[Browsable(false)]
		public int CurrentPriceLineWidth { get; set; }

		[XmlIgnore]
		[Display(Name = "Neutral Text", GroupName = "06. Colors", Order = 0)]
		public Brush NeutralTextColor { get; set; }

		[Browsable(false)]
		public string NeutralTextColorSerialize
		{
			get
			{
				return Serialize.BrushToString(NeutralTextColor);
			}
			set
			{
				NeutralTextColor = Serialize.StringToBrush(value);
			}
		}

		[Display(Name = "Dim Text", GroupName = "06. Colors", Order = 1)]
		[XmlIgnore]
		public Brush DimTextColor { get; set; }

		[Browsable(false)]
		public string DimTextColorSerialize
		{
			get
			{
				return Serialize.BrushToString(DimTextColor);
			}
			set
			{
				DimTextColor = Serialize.StringToBrush(value);
			}
		}

		[Display(Name = "Panel Back", GroupName = "06. Colors", Order = 2)]
		[XmlIgnore]
		public Brush PanelBackColor { get; set; }

		[Browsable(false)]
		public string PanelBackColorSerialize
		{
			get
			{
				return Serialize.BrushToString(PanelBackColor);
			}
			set
			{
				PanelBackColor = Serialize.StringToBrush(value);
			}
		}

		[XmlIgnore]
		[Display(Name = "Grid", GroupName = "06. Colors", Order = 3)]
		public Brush GridColor { get; set; }

		[Browsable(false)]
		public string GridColorSerialize
		{
			get
			{
				return Serialize.BrushToString(GridColor);
			}
			set
			{
				GridColor = Serialize.StringToBrush(value);
			}
		}

		[Display(Name = "Bid Text", GroupName = "06. Colors", Order = 4)]
		[XmlIgnore]
		public Brush BidTextColor { get; set; }

		[Browsable(false)]
		public string BidTextColorSerialize
		{
			get
			{
				return Serialize.BrushToString(BidTextColor);
			}
			set
			{
				BidTextColor = Serialize.StringToBrush(value);
			}
		}

		[XmlIgnore]
		[Display(Name = "Ask Text", GroupName = "06. Colors", Order = 5)]
		public Brush AskTextColor { get; set; }

		[Browsable(false)]
		public string AskTextColorSerialize
		{
			get
			{
				return Serialize.BrushToString(AskTextColor);
			}
			set
			{
				AskTextColor = Serialize.StringToBrush(value);
			}
		}

		[XmlIgnore]
		[Display(Name = "Delta Positive", GroupName = "06. Colors", Order = 6)]
		public Brush DeltaPositiveColor { get; set; }

		[Browsable(false)]
		public string DeltaPositiveColorSerialize
		{
			get
			{
				return Serialize.BrushToString(DeltaPositiveColor);
			}
			set
			{
				DeltaPositiveColor = Serialize.StringToBrush(value);
			}
		}

		[XmlIgnore]
		[Display(Name = "Delta Negative", GroupName = "06. Colors", Order = 7)]
		public Brush DeltaNegativeColor { get; set; }

		[Browsable(false)]
		public string DeltaNegativeColorSerialize
		{
			get
			{
				return Serialize.BrushToString(DeltaNegativeColor);
			}
			set
			{
				DeltaNegativeColor = Serialize.StringToBrush(value);
			}
		}

		[Display(Name = "Candle Footprint Volume", GroupName = "06. Colors", Order = 8)]
		[XmlIgnore]
		public Brush VolumeProfileColor { get; set; }

		[Browsable(false)]
		public string VolumeProfileColorSerialize
		{
			get
			{
				return Serialize.BrushToString(VolumeProfileColor);
			}
			set
			{
				VolumeProfileColor = Serialize.StringToBrush(value);
			}
		}

		[Display(Name = "Footprint Base", GroupName = "06. Colors", Order = 9)]
		[XmlIgnore]
		public Brush FootprintBaseColor { get; set; }

		[Browsable(false)]
		public string FootprintBaseColorSerialize
		{
			get
			{
				return Serialize.BrushToString(FootprintBaseColor);
			}
			set
			{
				FootprintBaseColor = Serialize.StringToBrush(value);
			}
		}

		[Display(Name = "POC", GroupName = "06. Colors", Order = 10)]
		[XmlIgnore]
		public Brush PocColor { get; set; }

		[Browsable(false)]
		public string PocColorSerialize
		{
			get
			{
				return Serialize.BrushToString(PocColor);
			}
			set
			{
				PocColor = Serialize.StringToBrush(value);
			}
		}

		[Display(Name = "Bar Summary Total", GroupName = "06. Colors", Order = 11)]
		[XmlIgnore]
		public Brush SummaryTotalColor { get; set; }

		[Browsable(false)]
		public string SummaryTotalColorSerialize
		{
			get
			{
				return Serialize.BrushToString(SummaryTotalColor);
			}
			set
			{
				SummaryTotalColor = Serialize.StringToBrush(value);
			}
		}

		[XmlIgnore]
		[Display(Name = "VA Buy Control", GroupName = "06. Colors", Order = 10)]
		public Brush VaBuyColor { get; set; }

		[Browsable(false)]
		public string VaBuyColorSerialize
		{
			get
			{
				return Serialize.BrushToString(VaBuyColor);
			}
			set
			{
				VaBuyColor = Serialize.StringToBrush(value);
			}
		}

		[Display(Name = "VA Sell Control", GroupName = "06. Colors", Order = 11)]
		[XmlIgnore]
		public Brush VaSellColor { get; set; }

		[Browsable(false)]
		public string VaSellColorSerialize
		{
			get
			{
				return Serialize.BrushToString(VaSellColor);
			}
			set
			{
				VaSellColor = Serialize.StringToBrush(value);
			}
		}

		[XmlIgnore]
		[Display(Name = "Buy Imbalance", GroupName = "06. Colors", Order = 12)]
		public Brush BuyImbalanceColor { get; set; }

		[Browsable(false)]
		public string BuyImbalanceColorSerialize
		{
			get
			{
				return Serialize.BrushToString(BuyImbalanceColor);
			}
			set
			{
				BuyImbalanceColor = Serialize.StringToBrush(value);
			}
		}

		[XmlIgnore]
		[Display(Name = "Sell Imbalance", GroupName = "06. Colors", Order = 13)]
		public Brush SellImbalanceColor { get; set; }

		[Browsable(false)]
		public string SellImbalanceColorSerialize
		{
			get
			{
				return Serialize.BrushToString(SellImbalanceColor);
			}
			set
			{
				SellImbalanceColor = Serialize.StringToBrush(value);
			}
		}

		[XmlIgnore]
		[Display(Name = "Current Price Up", GroupName = "06. Colors", Order = 14)]
		public Brush CurrentPriceUpColor { get; set; }

		[Browsable(false)]
		public string CurrentPriceUpColorSerialize
		{
			get
			{
				return Serialize.BrushToString(CurrentPriceUpColor);
			}
			set
			{
				CurrentPriceUpColor = Serialize.StringToBrush(value);
			}
		}

		[XmlIgnore]
		[Display(Name = "Current Price Down", GroupName = "06. Colors", Order = 15)]
		public Brush CurrentPriceDownColor { get; set; }

		[Browsable(false)]
		public string CurrentPriceDownColorSerialize
		{
			get
			{
				return Serialize.BrushToString(CurrentPriceDownColor);
			}
			set
			{
				CurrentPriceDownColor = Serialize.StringToBrush(value);
			}
		}

		[Display(Name = "Current Price Neutral", GroupName = "06. Colors", Order = 16)]
		[XmlIgnore]
		public Brush CurrentPriceNeutralColor { get; set; }

		[Browsable(false)]
		public string CurrentPriceNeutralColorSerialize
		{
			get
			{
				return Serialize.BrushToString(CurrentPriceNeutralColor);
			}
			set
			{
				CurrentPriceNeutralColor = Serialize.StringToBrush(value);
			}
		}

		[Browsable(false)]
		[Display(Name = "Show Lion Signal Labels", GroupName = "04. Layout", Order = 30)]
		public bool ShowLionSignalLabels { get; set; }

		[Display(Name = "Show Lion Signal Arrows", GroupName = "04. Layout", Order = 31)]
		[Browsable(false)]
		public bool ShowLionSignalArrows { get; set; }

		[Browsable(false)]
		[Display(Name = "Lion Marker Offset Ticks", GroupName = "04. Layout", Order = 32)]
		[Range(0, 50)]
		public int LionSignalMarkerOffsetTicks { get; set; }

		[Display(Name = "Lion Label Opacity", GroupName = "04. Layout", Order = 33)]
		[Browsable(false)]
		[Range(0, 100)]
		public int LionSignalLabelOpacity { get; set; }

		[Range(0, 100)]
		[Browsable(false)]
		[Display(Name = "Lion Arrow Opacity", GroupName = "04. Layout", Order = 34)]
		public int LionSignalArrowOpacity { get; set; }

		[XmlIgnore]
		[Browsable(false)]
		[Display(Name = "Block Bull", GroupName = "10. Block Candle Colors", Order = 0)]
		public Brush BlockBullColor { get; set; }

		[Browsable(false)]
		public string BlockBullColorSerialize
		{
			get
			{
				return Serialize.BrushToString(BlockBullColor);
			}
			set
			{
				BlockBullColor = Serialize.StringToBrush(value);
			}
		}

		[XmlIgnore]
		[Browsable(false)]
		[Display(Name = "Block Bear", GroupName = "10. Block Candle Colors", Order = 1)]
		public Brush BlockBearColor { get; set; }

		[Browsable(false)]
		public string BlockBearColorSerialize
		{
			get
			{
				return Serialize.BrushToString(BlockBearColor);
			}
			set
			{
				BlockBearColor = Serialize.StringToBrush(value);
			}
		}

		[XmlIgnore]
		[Browsable(false)]
		[Display(Name = "Block Neutral", GroupName = "10. Block Candle Colors", Order = 2)]
		public Brush BlockNeutralColor { get; set; }

		[Browsable(false)]
		public string BlockNeutralColorSerialize
		{
			get
			{
				return Serialize.BrushToString(BlockNeutralColor);
			}
			set
			{
				BlockNeutralColor = Serialize.StringToBrush(value);
			}
		}

		[Browsable(false)]
		[XmlIgnore]
		[Display(Name = "Lion ACEL BUY", GroupName = "11. Lion External Colors", Order = 0)]
		public Brush LionAcelBuyColor { get; set; }

		[Browsable(false)]
		public string LionAcelBuyColorSerialize
		{
			get
			{
				return Serialize.BrushToString(LionAcelBuyColor);
			}
			set
			{
				LionAcelBuyColor = Serialize.StringToBrush(value);
			}
		}

		[Browsable(false)]
		[Display(Name = "Lion ACEL SELL", GroupName = "11. Lion External Colors", Order = 1)]
		[XmlIgnore]
		public Brush LionAcelSellColor { get; set; }

		[Browsable(false)]
		public string LionAcelSellColorSerialize
		{
			get
			{
				return Serialize.BrushToString(LionAcelSellColor);
			}
			set
			{
				LionAcelSellColor = Serialize.StringToBrush(value);
			}
		}

		[Display(Name = "Lion EXC BUY", GroupName = "11. Lion External Colors", Order = 2)]
		[XmlIgnore]
		[Browsable(false)]
		public Brush LionExcessBuyColor { get; set; }

		[Browsable(false)]
		public string LionExcessBuyColorSerialize
		{
			get
			{
				return Serialize.BrushToString(LionExcessBuyColor);
			}
			set
			{
				LionExcessBuyColor = Serialize.StringToBrush(value);
			}
		}

		[Display(Name = "Lion EXC SELL", GroupName = "11. Lion External Colors", Order = 3)]
		[Browsable(false)]
		[XmlIgnore]
		public Brush LionExcessSellColor { get; set; }

		[Browsable(false)]
		public string LionExcessSellColorSerialize
		{
			get
			{
				return Serialize.BrushToString(LionExcessSellColor);
			}
			set
			{
				LionExcessSellColor = Serialize.StringToBrush(value);
			}
		}

		[Display(Name = "Lion ROT BUY", GroupName = "11. Lion External Colors", Order = 4)]
		[XmlIgnore]
		[Browsable(false)]
		public Brush LionRotationBuyColor { get; set; }

		[Browsable(false)]
		public string LionRotationBuyColorSerialize
		{
			get
			{
				return Serialize.BrushToString(LionRotationBuyColor);
			}
			set
			{
				LionRotationBuyColor = Serialize.StringToBrush(value);
			}
		}

		[Display(Name = "Lion ROT SELL", GroupName = "11. Lion External Colors", Order = 5)]
		[Browsable(false)]
		[XmlIgnore]
		public Brush LionRotationSellColor { get; set; }

		[Browsable(false)]
		public string LionRotationSellColorSerialize
		{
			get
			{
				return Serialize.BrushToString(LionRotationSellColor);
			}
			set
			{
				LionRotationSellColor = Serialize.StringToBrush(value);
			}
		}

		[XmlIgnore]
		[Browsable(false)]
		[Display(Name = "Lion ABS BUY", GroupName = "11. Lion External Colors", Order = 6)]
		public Brush LionAbsorptionBuyColor { get; set; }

		[Browsable(false)]
		public string LionAbsorptionBuyColorSerialize
		{
			get
			{
				return Serialize.BrushToString(LionAbsorptionBuyColor);
			}
			set
			{
				LionAbsorptionBuyColor = Serialize.StringToBrush(value);
			}
		}

		[Display(Name = "Lion ABS SELL", GroupName = "11. Lion External Colors", Order = 7)]
		[Browsable(false)]
		[XmlIgnore]
		public Brush LionAbsorptionSellColor { get; set; }

		[Browsable(false)]
		public string LionAbsorptionSellColorSerialize
		{
			get
			{
				return Serialize.BrushToString(LionAbsorptionSellColor);
			}
			set
			{
				LionAbsorptionSellColor = Serialize.StringToBrush(value);
			}
		}

		private string IltDevFingerprint_ILT_DEV_SESSIONFOOT()
		{
			if (iltDevFp_ILT_DEV_SESSIONFOOT == null)
			{
				iltDevFp_ILT_DEV_SESSIONFOOT = IltDevCalcFp_ILT_DEV_SESSIONFOOT();
			}
			return iltDevFp_ILT_DEV_SESSIONFOOT;
		}

		private bool IltDevEsValida_ILT_DEV_SESSIONFOOT()
		{
			if (iltDevOk_ILT_DEV_SESSIONFOOT.HasValue)
			{
				return iltDevOk_ILT_DEV_SESSIONFOOT.Value;
			}
			iltDevOk_ILT_DEV_SESSIONFOOT = IltDevValidar_ILT_DEV_SESSIONFOOT(out iltDevMotivo_ILT_DEV_SESSIONFOOT);
			return iltDevOk_ILT_DEV_SESSIONFOOT.Value;
		}

		private string IltDevTextoBloqueo_ILT_DEV_SESSIONFOOT()
		{
			return "ILT_DEV_SUITE - LICENCIA INVALIDA\n" + iltDevMotivo_ILT_DEV_SESSIONFOOT + "\n\nBusque la licencia en:\n" + IltDevRuta_ILT_DEV_SESSIONFOOT() + "\n\nCodigo de este equipo (envialo a tu instructor):\n" + IltDevFingerprint_ILT_DEV_SESSIONFOOT();
		}

		private bool IltDevValidar_ILT_DEV_SESSIONFOOT(out string motivo)
		{
			motivo = "";
			try
			{
				string path = IltDevRuta_ILT_DEV_SESSIONFOOT();
				if (!File.Exists(path))
				{
					motivo = "No se encontro licencia.lic en Documents\\ILT_DEV_SUITE\\";
					return false;
				}
				string contenido = File.ReadAllText(path, Encoding.UTF8);
				string cuerpo;
				string firma;
				if (!IltDevParse_ILT_DEV_SESSIONFOOT(contenido, out cuerpo, out firma))
				{
					motivo = "El formato de la licencia no es valido.";
					return false;
				}
				if (!IltDevVerificar_ILT_DEV_SESSIONFOOT(cuerpo, firma))
				{
					motivo = "Firma invalida: la licencia fue alterada o no es autentica.";
					return false;
				}
				string a = IltDevCampo_ILT_DEV_SESSIONFOOT(cuerpo, "FINGERPRINT");
				string s = IltDevCampo_ILT_DEV_SESSIONFOOT(cuerpo, "CADUCIDAD");
				if (!string.Equals(a, IltDevFingerprint_ILT_DEV_SESSIONFOOT(), StringComparison.OrdinalIgnoreCase))
				{
					motivo = "Esta licencia no corresponde a este equipo.";
					return false;
				}
				DateTime result;
				if (!DateTime.TryParse(s, out result))
				{
					motivo = "La fecha de caducidad no es valida.";
					return false;
				}
				if (DateTime.UtcNow.Date > result.Date)
				{
					motivo = "La licencia caduco el " + result.ToString("yyyy-MM-dd") + ".";
					return false;
				}
				motivo = "OK";
				return true;
			}
			catch (Exception ex)
			{
				motivo = "Error validando: " + ex.Message;
				return false;
			}
		}

		private string IltDevRuta_ILT_DEV_SESSIONFOOT()
		{
			string[] array = IltDevRutas_ILT_DEV_SESSIONFOOT();
			foreach (string text in array)
			{
				if (File.Exists(text))
				{
					return text;
				}
			}
			string[] array2 = IltDevRutas_ILT_DEV_SESSIONFOOT();
			if (array2.Length <= 0)
			{
				return "";
			}
			return array2[0];
		}

		private string[] IltDevRutas_ILT_DEV_SESSIONFOOT()
		{
			List<string> list = new List<string>();
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
			if (!string.IsNullOrEmpty(folderPath))
			{
				list.Add(Path.Combine(folderPath, "ILT_DEV_SUITE", "licencia.lic"));
			}
			string folderPath2 = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			if (!string.IsNullOrEmpty(folderPath2))
			{
				list.Add(Path.Combine(folderPath2, "OneDrive", "Documentos", "ILT_DEV_SUITE", "licencia.lic"));
				list.Add(Path.Combine(folderPath2, "OneDrive", "Documents", "ILT_DEV_SUITE", "licencia.lic"));
				list.Add(Path.Combine(folderPath2, "Documents", "ILT_DEV_SUITE", "licencia.lic"));
				list.Add(Path.Combine(folderPath2, "Documentos", "ILT_DEV_SUITE", "licencia.lic"));
			}
			string environmentVariable = Environment.GetEnvironmentVariable("OneDrive");
			if (!string.IsNullOrEmpty(environmentVariable))
			{
				list.Add(Path.Combine(environmentVariable, "Documentos", "ILT_DEV_SUITE", "licencia.lic"));
				list.Add(Path.Combine(environmentVariable, "Documents", "ILT_DEV_SUITE", "licencia.lic"));
			}
			return list.ToArray();
		}

		private bool IltDevVerificar_ILT_DEV_SESSIONFOOT(string cuerpo, string firmaB64)
		{
			using (RSACryptoServiceProvider rSACryptoServiceProvider = new RSACryptoServiceProvider())
			{
				rSACryptoServiceProvider.PersistKeyInCsp = false;
				rSACryptoServiceProvider.FromXmlString("<RSAKeyValue><Modulus>6Xi/RSxNtqILBIrligimoZIBittn99yzL/b2FjKmEtnWUL/mCOObtBbvdskEhXx6ep44BthxahLWtwaFZQoSBjo3OrJO/pA267uTUAFZ0kfXEvggyBrwKfw8uMFs9i87yNUL5QiOTujfy7Z/WvYhGdIg+G9JSLZV9iOzXVwFXCz1786JMzrm4JXkgldP9zqQ4dVX0VO4XvQA8VhRh/t2lQ6bx89Oy5amS6NdNGGdV5+oLLDScwJ0xaVwBLvnqNk0KAbripykuwt2BfEQYB5YJv8PfPO641IXRsPJnrh/7wWLAZ9oL/A7VzB32H7KHNyEWoiW4JSit8KI0WoInC++qQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>");
				byte[] bytes = Encoding.UTF8.GetBytes(cuerpo);
				byte[] signature = Convert.FromBase64String(firmaB64);
				return rSACryptoServiceProvider.VerifyData(bytes, new SHA256CryptoServiceProvider(), signature);
			}
		}

		private string IltDevCalcFp_ILT_DEV_SESSIONFOOT()
		{
			string machineName = Environment.MachineName;
			string userName = Environment.UserName;
			string text = IltDevSerieC_ILT_DEV_SESSIONFOOT();
			string s = machineName + "|" + userName + "|" + text;
			using (SHA256 sHA = SHA256.Create())
			{
				byte[] array = sHA.ComputeHash(Encoding.UTF8.GetBytes(s));
				StringBuilder stringBuilder = new StringBuilder();
				for (int i = 0; i < 6; i++)
				{
					stringBuilder.Append(array[i].ToString("X2"));
				}
				return stringBuilder.ToString();
			}
		}

		private string IltDevSerieC_ILT_DEV_SESSIONFOOT()
		{
			try
			{
				uint volumeSerialNumber = 0u;
				StringBuilder stringBuilder = new StringBuilder(261);
				StringBuilder stringBuilder2 = new StringBuilder(261);
				uint maximumComponentLength;
				uint fileSystemFlags;
				if (IltDevGetVol_ILT_DEV_SESSIONFOOT("C:\\", stringBuilder, stringBuilder.Capacity, ref volumeSerialNumber, out maximumComponentLength, out fileSystemFlags, stringBuilder2, stringBuilder2.Capacity))
				{
					return volumeSerialNumber.ToString("X8");
				}
			}
			catch
			{
			}
			return "NOSERIE";
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, EntryPoint = "GetVolumeInformation", SetLastError = true)]
		private static extern bool IltDevGetVol_ILT_DEV_SESSIONFOOT(string rootPathName, StringBuilder volumeNameBuffer, int volumeNameSize, ref uint volumeSerialNumber, out uint maximumComponentLength, out uint fileSystemFlags, StringBuilder fileSystemNameBuffer, int fileSystemNameSize);

		private bool IltDevParse_ILT_DEV_SESSIONFOOT(string contenido, out string cuerpo, out string firma)
		{
			cuerpo = "";
			firma = "";
			int num = contenido.IndexOf("===FIRMA===", StringComparison.Ordinal);
			if (num < 0)
			{
				return false;
			}
			int num2 = contenido.IndexOf("===ILT_DEV_SUITE_LICENCIA===", StringComparison.Ordinal);
			if (num2 < 0)
			{
				return false;
			}
			for (num2 += "===ILT_DEV_SUITE_LICENCIA===".Length; num2 < contenido.Length && (contenido[num2] == '\r' || contenido[num2] == '\n'); num2++)
			{
			}
			cuerpo = contenido.Substring(num2, num - num2);
			firma = contenido.Substring(num + "===FIRMA===".Length).Trim();
			return firma.Length > 0;
		}

		private string IltDevCampo_ILT_DEV_SESSIONFOOT(string cuerpo, string campo)
		{
			string[] array = cuerpo.Split('\n');
			foreach (string text in array)
			{
				string text2 = text.Trim();
				if (text2.StartsWith(campo + "=", StringComparison.Ordinal))
				{
					return text2.Substring(campo.Length + 1).Trim();
				}
			}
			return "";
		}

		public static bool TryGetExportState(string instrumentFullName, BarsPeriodType barsPeriodType, int barsPeriodValue, out ILT_DEV_FOTPRINT_ExportState state)
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			state = null;
			string exportInstrumentRoot = GetExportInstrumentRoot(instrumentFullName);
			string key = BuildFootprintExportKey(instrumentFullName, barsPeriodType, barsPeriodValue);
			string key2 = BuildFootprintExportKey(exportInstrumentRoot, barsPeriodType, barsPeriodValue);
			lock (_footprintExportBusLock)
			{
				ILT_DEV_FOTPRINT_ExportState value;
				if (_footprintExportBus.TryGetValue(key, out value) || _footprintExportBus.TryGetValue(key2, out value) || _footprintExportBus.TryGetValue(instrumentFullName ?? string.Empty, out value) || _footprintExportBus.TryGetValue(exportInstrumentRoot ?? string.Empty, out value))
				{
					state = ((value != null) ? value.Clone() : null);
					return state != null;
				}
			}
			return false;
		}

		private static string BuildFootprintExportKey(string instrumentFullName, BarsPeriodType barsPeriodType, int barsPeriodValue)
		{
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			return (instrumentFullName ?? string.Empty) + "|" + ((object)barsPeriodType).ToString() + "|" + barsPeriodValue;
		}

		private static string GetExportInstrumentRoot(string instrumentFullName)
		{
			string text = instrumentFullName ?? string.Empty;
			try
			{
				text = text.Trim();
				int num = text.IndexOf(' ');
				if (num > 0)
				{
					text = text.Substring(0, num);
				}
			}
			catch
			{
				text = instrumentFullName ?? string.Empty;
			}
			return text;
		}

		public static void PublishLionSignal(string instrumentFullName, BarsPeriodType barsPeriodType, int barsPeriodValue, int barIndex, int signalCode)
		{
		}

		public static void PublishLionSignal(string instrumentFullName, BarsPeriodType barsPeriodType, int barsPeriodValue, int barIndex, DateTime barTime, int signalCode)
		{
		}

		public static void PublishLionSignal(string instrumentFullName, BarsPeriodType barsPeriodType, int barsPeriodValue, int barIndex, DateTime barTime, int signalCode, int colorArgb, bool showLabel, bool showArrow, string label, bool drawAbove, int markerOffsetTicks)
		{
		}

		private static string BuildLionSignalBusKey(string instrumentFullName, BarsPeriodType barsPeriodType, int barsPeriodValue)
		{
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			return (instrumentFullName ?? string.Empty) + "|" + ((object)barsPeriodType).ToString() + "|" + barsPeriodValue;
		}

		private static string BuildLionSignalBusAnyPeriodKey(string instrumentFullName)
		{
			return (instrumentFullName ?? string.Empty) + "|ANY_PERIOD";
		}

		private static string GetInstrumentRootKey(string instrumentFullName)
		{
			string text = instrumentFullName;
			try
			{
				if (!string.IsNullOrEmpty(text))
				{
					string text2 = text.Trim();
					int num = text2.IndexOf(' ');
					text = ((num <= 0) ? text2 : text2.Substring(0, num));
				}
			}
			catch
			{
				text = instrumentFullName;
			}
			return text ?? string.Empty;
		}

		private static string[] BuildLionSignalBusKeys(string instrumentFullName, BarsPeriodType barsPeriodType, int barsPeriodValue)
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			List<string> list = new List<string>();
			string obj = BuildLionSignalBusKey(instrumentFullName, barsPeriodType, barsPeriodValue);
			string instrumentRootKey = GetInstrumentRootKey(instrumentFullName);
			string obj2 = BuildLionSignalBusKey(instrumentRootKey, barsPeriodType, barsPeriodValue);
			string obj3 = BuildLionSignalBusAnyPeriodKey(instrumentFullName);
			string obj4 = BuildLionSignalBusAnyPeriodKey(instrumentRootKey);
			Action<string> action = delegate(string k)
			{
				if (!string.IsNullOrEmpty(k) && !list.Contains(k))
				{
					list.Add(k);
				}
			};
			action(obj);
			action(obj2);
			action(obj3);
			action(obj4);
			return list.ToArray();
		}

		protected override void OnStateChange()
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Invalid comparison between Unknown and I4
			//IL_0487: Unknown result type (might be due to invalid IL or missing references)
			//IL_048d: Invalid comparison between Unknown and I4
			//IL_061c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0626: Expected O, but got Unknown
			//IL_04f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_04f9: Invalid comparison between Unknown and I4
			//IL_04d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_0666: Unknown result type (might be due to invalid IL or missing references)
			//IL_066c: Invalid comparison between Unknown and I4
			//IL_05fd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0607: Expected O, but got Unknown
			if ((int)base.State == 1)
			{
				Name = "ILT-DEV-FOOTPRINT_OFB";
				base.Description = "ILT_DEV_FOOTPRINT_OFB - footprint numerico y ladder lateral con cinco modos visuales. Parte de ILT_Dev_Suite.";
				base.Calculate = (Calculate)1;
				base.IsOverlay = true;
				base.DisplayInDataBox = false;
				base.DrawOnPricePanel = true;
				base.IsSuspendedWhileInactive = false;
				UseNativeVolumetricBidAsk = true;
				LoadHistoricalSession = true;
				CountZeroPrintImbalance = false;
				NativeRefreshMs = 500;
				VisualRefreshMs = 66;
				SessionVolumetricMinutes = 1380;
				SessionTimeMode = 1;
				ShowDataDiagnostics = false;
				ControlPanelX = 8;
				ControlPanelY = 26;
				StatsWindowX = 255;
				StatsWindowY = 35;
				StatsWindowWidth = 350;
				TicksPerRow = 1;
				SessionStartHourNY = 18;
				SessionEndHourNY = 17;
				ShowSessionLadder = true;
				VisualMode = IltDevFootprintDisplayMode.SidedLadderPlus;
				ShowSessionDeltaProfile = false;
				ShowSessionVolumeProfile = false;
				ShowSessionBidAskColumn = false;
				UseWpfPanel = true;
				ShowToolbarButton = true;
				PanelXW = 8.0;
				PanelYW = 28.0;
				PanelMinimizedPersist = false;
				ShowCandleFootprint = true;
				ShowCandleProfileHistogram = false;
				FootprintCandleMode = 1;
				ShowBlockCandles = false;
				HideNativeCandlesWithBlocks = false;
				BlockCandleColorMode = 1;
				BlockCandleDrawMode = 1;
				BlockCandleWidth = 7;
				BlockCandleMinWidth = 3;
				BlockCandleMaxWidth = 18;
				BlockCandleRowGapPixels = 1;
				BlockCandleOpacity = 84;
				ShowBlockCandlePoc = false;
				ShowCandleValueArea = false;
				ShowSessionValueArea = false;
				ShowCurrentPriceLine = false;
				ShowImbalances = true;
				ShowCandleImbalances = true;
				ShowSessionLadderImbalances = false;
				ShowExtendedImbalanceLines = false;
				ShowBarSummary = true;
				ShowNewImbalanceAnimation = false;
				ShowBidAskText = true;
				ShowSpreadInTotal = false;
				ShowPriceColumn = false;
				ShowControlPanel = false;
				LadderSide = 1;
				LadderPositionPct = 100;
				ControlPanelWidth = 220;
				SessionLadderWidth = 128;
				BidAskColumnWidth = 110;
				DeltaColumnWidth = 110;
				VolumeProfileWidth = 90;
				PriceColumnWidth = 74;
				VolumeProfileDirection = 2;
				DeltaHistogramDirection = 2;
				ShowSessionRowGrid = true;
				CandleFootprintWidth = 76;
				CandleFootprintOffset = 0;
				AutoResizeCandleFootprint = true;
				CandleFootprintBarMetric = 1;
				SummaryBarHeight = 3;
				SummaryGap = 4;
				SummaryBorderWidth = 1;
				MinVolumeToDisplay = 1;
				MinRowHeightForText = 11;
				LadderImbalancePct = 300;
				LadderMinImbalance = 10;
				LadderImbalanceStackRows = 1;
				CandleImbalancePct = 300;
				CandleMinImbalance = 10;
				CandleImbalanceStackRows = 1;
				ValueAreaPct = 70;
				FontSizeSmall = 10;
				FontSizeText = 11;
				FontSizeBold = 12;
				LadderOpacity = 94;
				FootprintOpacity = 80;
				ValueAreaOpacity = 32;
				VolumeProfileOpacity = 70;
				DeltaOpacity = 85;
				ImbalanceLineOpacity = 65;
				ExtendedImbalanceLineWidth = 2;
				ExtendedImbalanceLookbackBars = 1200;
				NewImbalanceAnimationOpacity = 85;
				NewImbalanceAnimationMs = 1800;
				NewImbalanceAnimationLineWidth = 6;
				CurrentPriceLineOpacity = 92;
				CurrentPriceLinePulseMs = 1400;
				CurrentPriceLineWidth = 2;
				NeutralTextColor = Brushes.LightGray;
				DimTextColor = Brushes.Gray;
				PanelBackColor = new System.Windows.Media.SolidColorBrush(Color.FromRgb(12, 14, 22));
				GridColor = new System.Windows.Media.SolidColorBrush(Color.FromRgb(56, 60, 75));
				BidTextColor = Brushes.IndianRed;
				AskTextColor = Brushes.LimeGreen;
				DeltaPositiveColor = Brushes.LimeGreen;
				DeltaNegativeColor = Brushes.Red;
				VolumeProfileColor = Brushes.LightGray;
				FootprintBaseColor = new System.Windows.Media.SolidColorBrush(Color.FromRgb(0, 148, 158));
				PocColor = Brushes.Yellow;
				SummaryTotalColor = Brushes.White;
				VaBuyColor = Brushes.LimeGreen;
				VaSellColor = Brushes.Red;
				BuyImbalanceColor = new System.Windows.Media.SolidColorBrush(Color.FromRgb(0, 255, 40));
				SellImbalanceColor = new System.Windows.Media.SolidColorBrush(Color.FromRgb(255, 32, 32));
				CurrentPriceUpColor = Brushes.LimeGreen;
				CurrentPriceDownColor = Brushes.OrangeRed;
				CurrentPriceNeutralColor = Brushes.Gold;
				BlockBullColor = Brushes.LimeGreen;
				BlockBearColor = Brushes.Red;
				BlockNeutralColor = Brushes.DimGray;
				LionAcelBuyColor = Brushes.LimeGreen;
				LionAcelSellColor = Brushes.Red;
				LionExcessBuyColor = Brushes.Orange;
				LionExcessSellColor = Brushes.Gold;
				LionRotationBuyColor = Brushes.DeepSkyBlue;
				LionRotationSellColor = Brushes.Magenta;
				LionAbsorptionBuyColor = Brushes.RoyalBlue;
				LionAbsorptionSellColor = Brushes.DarkOrange;
				ShowLionSignalLabels = false;
				ShowLionSignalArrows = false;
				LionSignalMarkerOffsetTicks = 3;
				LionSignalLabelOpacity = 100;
				LionSignalArrowOpacity = 100;
			}
			else if ((int)base.State == 2)
			{
				LadderPositionPct = LadderSide == 2 ? 0 : 100;
				LoadRuntimeSettings();
				_configuredTicksPerRow = Math.Max(1, TicksPerRow);
				UseNativeVolumetricBidAsk = true;
				FootprintCandleMode = 1;
				ShowBlockCandles = false;
				HideNativeCandlesWithBlocks = false;
				base.AddVolumetric(base.Instrument.FullName, base.BarsPeriod.BarsPeriodType, base.BarsPeriod.Value, (VolumetricDeltaType)0, _configuredTicksPerRow);
			}
			else if ((int)base.State == 4)
			{
				_sessionFp = new Dictionary<int, ILT_DEV_FOTPRINT_Level>();
				_barFp = new Dictionary<int, Dictionary<int, ILT_DEV_FOTPRINT_Level>>();
				_barFpTotals.Clear();
				_volToPrimaryBarOffset = int.MinValue;
				_lastDevVolBarIndex = -1;
				_lastDevVolBarProcessMs = 0L;
				_sessionLadderSnapshot = null;
				_sessionLadderSnapshotVersion = -1L;
				_candleImbKeyCache.Clear();
				_candleVaKeyCache.Clear();
				_nyTz = ResolveNewYorkTimeZone();
				_tick = base.Instrument.MasterInstrument.TickSize;
				_decs = Math.Max(0, CountDecimals(_tick));
				if (UseNativeVolumetricBidAsk && base.BarsArray != null && base.BarsArray.Length > 1)
				{
					BarsType barsType = base.BarsArray[1].BarsType;
					_nativeVolBarsType = (VolumetricBarsType)(object)((barsType is VolumetricBarsType) ? barsType : null);
					if (base.BarsArray.Length > 2)
					{
						BarsType barsType2 = base.BarsArray[2].BarsType;
						_sessionVolBarsType = (VolumetricBarsType)(object)((barsType2 is VolumetricBarsType) ? barsType2 : null);
					}
					try
					{
						_nativeSessionIterator = new SessionIterator(base.BarsArray[1]);
					}
					catch
					{
						_nativeSessionIterator = null;
					}
					try
					{
						_primarySessionIterator = new SessionIterator(base.BarsArray[0]);
					}
					catch
					{
						_primarySessionIterator = null;
					}
				}
				if (base.ChartControl != null)
				{
					((DispatcherObject)(object)base.ChartControl).Dispatcher.InvokeAsync(delegate
					{
						if (UseWpfPanel)
						{
							BuildBetaWpfPanel();
						}
						if (ShowToolbarButton)
						{
							BuildToolbarButton();
						}
					});
				}
			}
			else
			{
				if ((int)base.State != 8)
				{
					return;
				}
				if (base.ChartControl != null)
				{
					((DispatcherObject)(object)base.ChartControl).Dispatcher.InvokeAsync(delegate
					{
						RemoveToolbarButton();
						RemoveWpfPanel();
					});
				}
				FreeDX();
			}
		}

		protected override void OnMarketData(MarketDataEventArgs e)
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Invalid comparison between Unknown and I4
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Invalid comparison between Unknown and I4
			//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d5: Invalid comparison between Unknown and I4
			//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00de: Invalid comparison between Unknown and I4
			//IL_0091: Unknown result type (might be due to invalid IL or missing references)
			//IL_0097: Invalid comparison between Unknown and I4
			if (e == null)
			{
				return;
			}
			if ((int)e.MarketDataType == 1)
			{
				_lastBid = e.Price;
				return;
			}
			if ((int)e.MarketDataType == 0)
			{
				_lastAsk = e.Price;
				return;
			}
			if ((int)e.MarketDataType == 2)
			{
				double lastUiPrice = _lastUiPrice;
				_lastUiPrice = e.Price;
				if (lastUiPrice > 0.0)
				{
					if (e.Price > lastUiPrice)
					{
						_lastUiDir = 1;
					}
					else if (e.Price < lastUiPrice)
					{
						_lastUiDir = -1;
					}
				}
				_lastPriceLineMs = Environment.TickCount;
				if (ShowCurrentPriceLine && (int)base.State == 7)
				{
					long num = Environment.TickCount;
					if (num - _lastPriceLineRefreshMs >= Math.Max(33, VisualRefreshMs))
					{
						_lastPriceLineRefreshMs = num;
						base.ForceRefresh();
					}
				}
			}
			if (UseNativeVolumetricBidAsk || (int)e.MarketDataType != 2 || (int)base.State != 7)
			{
				return;
			}
			DateTime time = e.Time;
			if (!IsInsideNyTradingSession(time))
			{
				return;
			}
			int primaryBar = ((base.CurrentBar >= 0) ? base.CurrentBar : (-1));
			long num2 = Math.Max(0L, e.Volume);
			if (num2 > 0)
			{
				if (e.Bid > 0.0)
				{
					_lastBid = e.Bid;
				}
				if (e.Ask > 0.0)
				{
					_lastAsk = e.Ask;
				}
				ProcessTrade(e.Price, num2, primaryBar, time, true);
				long num3 = Environment.TickCount;
				if (num3 - _lastRealtimeTradeMs >= Math.Max(33, VisualRefreshMs))
				{
					_lastRealtimeTradeMs = num3;
					base.ForceRefresh();
				}
			}
		}

		protected override void OnBarUpdate()
		{
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_0078: Invalid comparison between Unknown and I4
			if (!IltDevEsValida_ILT_DEV_SESSIONFOOT())
			{
				return;
			}
			if (base.BarsInProgress == 1)
			{
				if (base.CurrentBars.Length < 2 || base.CurrentBars[1] < 0)
				{
					return;
				}
				if (_nativeVolBarsType == null && base.BarsArray != null && base.BarsArray.Length > 1)
				{
					BarsType barsType = base.BarsArray[1].BarsType;
					_nativeVolBarsType = (VolumetricBarsType)(object)((barsType is VolumetricBarsType) ? barsType : null);
				}
				if (_nativeVolBarsType == null)
				{
					return;
				}
				int num = base.CurrentBars[1];
				if ((int)base.State == 7)
				{
					if (num != _lastDevVolBarIndex)
					{
						if (_lastDevVolBarIndex >= 0 && _lastDevVolBarIndex < num)
						{
							ProcessNativeCandleVolumetricBar(_lastDevVolBarIndex);
						}
						_lastDevVolBarIndex = num;
						_lastDevVolBarProcessMs = 0L;
					}
					long num2 = Environment.TickCount;
					int num3 = Math.Max(80, Math.Min(300, NativeRefreshMs));
					if (num2 - _lastDevVolBarProcessMs < num3)
					{
						return;
					}
					_lastDevVolBarProcessMs = num2;
				}
				ProcessNativeCandleVolumetricBar(num);
			}
			else if (base.BarsInProgress != 2 && base.BarsInProgress == 0 && base.CurrentBar >= 0)
			{
				PublishLatestFootprintExportFromPrimary();
			}
		}

		private void RebuildSessionFromNativeSessionBar(int sessionBarIndex)
		{
			if (_sessionVolBarsType == null || sessionBarIndex < 0)
			{
				return;
			}
			DateTime valueAt;
			double valueAt2;
			double valueAt3;
			try
			{
				valueAt = base.Times[2].GetValueAt(sessionBarIndex);
				valueAt2 = base.Lows[2].GetValueAt(sessionBarIndex);
				valueAt3 = base.Highs[2].GetValueAt(sessionBarIndex);
			}
			catch
			{
				return;
			}
			Dictionary<int, ILT_DEV_FOTPRINT_Level> dictionary = new Dictionary<int, ILT_DEV_FOTPRINT_Level>();
			long totalVol = 0L;
			long pocVol = 0L;
			long maxDeltaAbs = 1L;
			int pocKey = int.MinValue;
			long num = 0L;
			long num2 = 0L;
			double num3 = RowPriceStep();
			double num4 = KeyToPrice(PriceToKey(valueAt2));
			double num5 = base.Instrument.MasterInstrument.RoundToTickSize(valueAt3);
			for (double num6 = num4; num6 <= num5 + _tick * 0.51; num6 += num3)
			{
				double num7 = base.Instrument.MasterInstrument.RoundToTickSize(num6);
				long num8 = 0L;
				long num9 = 0L;
				try
				{
					num8 = _sessionVolBarsType.Volumes[sessionBarIndex].GetBidVolumeForPrice(num7);
					num9 = _sessionVolBarsType.Volumes[sessionBarIndex].GetAskVolumeForPrice(num7);
				}
				catch
				{
					continue;
				}
				if (num8 > 0 || num9 > 0)
				{
					int key = PriceToKey(num7);
					AddExactBidAsk(dictionary, key, num8, num9, ref totalVol, ref pocKey, ref pocVol, ref maxDeltaAbs);
					num += Math.Max(0L, num8);
					num2 += Math.Max(0L, num9);
				}
			}
			DateTime begin;
			DateTime end;
			if (!GetNativeSessionBounds(valueAt, out begin, out end))
			{
				begin = valueAt.Date;
				end = valueAt;
			}
			lock (_lk)
			{
				_sessionDate = begin.Date;
				_nativeSessionBegin = begin;
				_nativeSessionEnd = end;
				_sessionFp = dictionary;
				_sessionTotalVol = totalVol;
				_sessionPocKey = pocKey;
				_sessionPocVol = pocVol;
				_sessionMaxDeltaAbs = Math.Max(1L, maxDeltaAbs);
				_sessionMaxVolAbs = ((dictionary != null && dictionary.Count > 0) ? Math.Max(1L, dictionary.Values.Max((ILT_DEV_FOTPRINT_Level v) => v.Total)) : 1);
				_lastVaCalcVolume = -1L;
				_diagVolBarsSummed = 1;
				_diagFirstVolBarTime = begin;
				_diagLastVolBarTime = valueAt;
				_diagTotalBid = num;
				_diagTotalAsk = num2;
			}
		}

		private void ProcessNativeCandleVolumetricBar(int volBarIndex)
		{
			//IL_0174: Unknown result type (might be due to invalid IL or missing references)
			//IL_017a: Invalid comparison between Unknown and I4
			//IL_040b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0411: Invalid comparison between Unknown and I4
			if (_nativeVolBarsType == null || volBarIndex < 0)
			{
				return;
			}
			DateTime valueAt;
			double valueAt2;
			double valueAt3;
			double valueAt4;
			try
			{
				valueAt = base.Times[1].GetValueAt(volBarIndex);
				valueAt2 = base.Lows[1].GetValueAt(volBarIndex);
				valueAt3 = base.Highs[1].GetValueAt(volBarIndex);
				valueAt4 = base.Closes[1].GetValueAt(volBarIndex);
			}
			catch
			{
				return;
			}
			DateTime begin;
			DateTime end;
			if (!GetNativeSessionBounds(valueAt, out begin, out end) || valueAt < begin || valueAt > end)
			{
				return;
			}
			int num = MapVolBarToPrimaryBar(volBarIndex, valueAt, valueAt4);
			if (num < 0)
			{
				return;
			}
			long num2 = 0L;
			Dictionary<int, ILT_DEV_FOTPRINT_Level> dictionary = new Dictionary<int, ILT_DEV_FOTPRINT_Level>();
			double num3 = RowPriceStep();
			double num4 = KeyToPrice(PriceToKey(valueAt2));
			double num5 = base.Instrument.MasterInstrument.RoundToTickSize(valueAt3);
			for (double num6 = num4; num6 <= num5 + _tick * 0.51; num6 += num3)
			{
				double num7 = base.Instrument.MasterInstrument.RoundToTickSize(num6);
				long num8 = 0L;
				long num9 = 0L;
				try
				{
					num8 = _nativeVolBarsType.Volumes[volBarIndex].GetBidVolumeForPrice(num7);
					num9 = _nativeVolBarsType.Volumes[volBarIndex].GetAskVolumeForPrice(num7);
				}
				catch
				{
					continue;
				}
				if (num8 > 0 || num9 > 0)
				{
					int key = PriceToKey(num7);
					AddExactBidAskNoStats(dictionary, key, num8, num9);
					num2 += Math.Max(0L, num8) + Math.Max(0L, num9);
				}
			}
			if (dictionary.Count == 0 && (int)base.State == 7)
			{
				return;
			}
			lock (_lk)
			{
				if (_barFp == null)
				{
					_barFp = new Dictionary<int, Dictionary<int, ILT_DEV_FOTPRINT_Level>>();
					_barFpTotals.Clear();
				}
				if (_sessionFp == null)
				{
					_sessionFp = new Dictionary<int, ILT_DEV_FOTPRINT_Level>();
				}
				if (_nativeSessionBegin == DateTime.MinValue || begin != _nativeSessionBegin)
				{
					_sessionFp.Clear();
					_sessionTotalVol = 0L;
					_sessionPocKey = int.MinValue;
					_sessionPocVol = 0L;
					_sessionMaxDeltaAbs = 1L;
					_sessionMaxVolAbs = 1L;
					_lastVaKeys.Clear();
					_lastVaCalcVolume = -1L;
					_diagVolBarsSummed = 0;
					_diagFirstVolBarTime = DateTime.MinValue;
					_diagLastVolBarTime = DateTime.MinValue;
					_diagTotalBid = 0L;
					_diagTotalAsk = 0L;
				}
				_sessionDate = begin.Date;
				_nativeSessionBegin = begin;
				_nativeSessionEnd = end;
				Dictionary<int, ILT_DEV_FOTPRINT_Level> value;
				long value2;
				if (_barFp.TryGetValue(num, out value) && value != null && _barFpTotals.TryGetValue(num, out value2) && value.Count == dictionary.Count && value2 == num2)
				{
					_diagVolBarsSummed = _barFp.Count;
					_diagFirstVolBarTime = begin;
					_diagLastVolBarTime = valueAt;
					PublishFootprintExportStateNoLock(num, value, valueAt);
					return;
				}
				if (_barFp.TryGetValue(num, out value))
				{
					SubtractMapFromSessionNoLock(value);
				}
				if (dictionary.Count > 0)
				{
					_barFp[num] = dictionary;
					_barFpTotals[num] = num2;
					AddMapToSessionNoLock(dictionary);
				}
				else if (_barFp.ContainsKey(num))
				{
					_barFp.Remove(num);
					_barFpTotals.Remove(num);
				}
				RecalculateSessionStatsNoLock();
				MarkExtendedImbalanceCacheDirtyNoLock();
				PublishFootprintExportStateNoLock(num, dictionary, valueAt);
				_diagVolBarsSummed = _barFp.Count;
				_diagFirstVolBarTime = begin;
				_diagLastVolBarTime = valueAt;
				if (ShowDataDiagnostics)
				{
					_diagTotalBid = _sessionFp.Values.Sum((ILT_DEV_FOTPRINT_Level v) => v.BidVol);
					_diagTotalAsk = _sessionFp.Values.Sum((ILT_DEV_FOTPRINT_Level v) => v.AskVol);
				}
			}
			if ((int)base.State == 7)
			{
				long num10 = Environment.TickCount;
				if (num10 - _lastNativeRealtimeRefreshMs >= Math.Max(100, NativeRefreshMs))
				{
					_lastNativeRealtimeRefreshMs = num10;
					base.ForceRefresh();
				}
			}
		}

		private void SubtractMapFromSessionNoLock(Dictionary<int, ILT_DEV_FOTPRINT_Level> oldMap)
		{
			if (oldMap == null || _sessionFp == null)
			{
				return;
			}
			foreach (KeyValuePair<int, ILT_DEV_FOTPRINT_Level> item in oldMap)
			{
				ILT_DEV_FOTPRINT_Level value;
				if (_sessionFp.TryGetValue(item.Key, out value))
				{
					value.BidVol -= Math.Max(0L, item.Value.BidVol);
					value.AskVol -= Math.Max(0L, item.Value.AskVol);
					if (value.BidVol <= 0 && value.AskVol <= 0)
					{
						_sessionFp.Remove(item.Key);
					}
				}
			}
		}

		private void AddMapToSessionNoLock(Dictionary<int, ILT_DEV_FOTPRINT_Level> newMap)
		{
			if (newMap == null || _sessionFp == null)
			{
				return;
			}
			foreach (KeyValuePair<int, ILT_DEV_FOTPRINT_Level> item in newMap)
			{
				AddExactBidAskNoStats(_sessionFp, item.Key, item.Value.BidVol, item.Value.AskVol);
			}
		}

		private void RecalculateSessionStatsNoLock()
		{
			_sessionTotalVol = 0L;
			_sessionPocKey = int.MinValue;
			_sessionPocVol = 0L;
			_sessionMaxDeltaAbs = 1L;
			_sessionMaxVolAbs = 1L;
			if (_sessionFp == null)
			{
				return;
			}
			foreach (KeyValuePair<int, ILT_DEV_FOTPRINT_Level> item in _sessionFp)
			{
				ILT_DEV_FOTPRINT_Level value = item.Value;
				long num = Math.Max(0L, value.Total);
				_sessionTotalVol += num;
				if (num > _sessionPocVol)
				{
					_sessionPocVol = num;
					_sessionPocKey = item.Key;
				}
				long num2 = Math.Abs(value.Delta);
				if (num2 > _sessionMaxDeltaAbs)
				{
					_sessionMaxDeltaAbs = num2;
				}
				if (num > _sessionMaxVolAbs)
				{
					_sessionMaxVolAbs = num;
				}
			}
			if (_sessionMaxDeltaAbs <= 0)
			{
				_sessionMaxDeltaAbs = 1L;
			}
			if (_sessionMaxVolAbs <= 0)
			{
				_sessionMaxVolAbs = 1L;
			}
		}

		private void RebuildNativeVolumetricSession(DateTime anchorTime)
		{
			DateTime begin;
			DateTime end;
			if (_nativeVolBarsType == null || base.CurrentBars == null || base.CurrentBars.Length < 2 || base.CurrentBars[1] < 0 || !GetNativeSessionBounds(anchorTime, out begin, out end))
			{
				return;
			}
			DateTime dateTime = begin;
			DateTime dateTime2 = ((anchorTime < end) ? anchorTime : end);
			int num = base.CurrentBars[1];
			int num2 = 0;
			DateTime diagFirstVolBarTime = DateTime.MinValue;
			DateTime diagLastVolBarTime = DateTime.MinValue;
			Dictionary<int, ILT_DEV_FOTPRINT_Level> dictionary = new Dictionary<int, ILT_DEV_FOTPRINT_Level>();
			Dictionary<int, Dictionary<int, ILT_DEV_FOTPRINT_Level>> dictionary2 = new Dictionary<int, Dictionary<int, ILT_DEV_FOTPRINT_Level>>();
			long totalVol = 0L;
			long pocVol = 0L;
			long maxDeltaAbs = 1L;
			int pocKey = int.MinValue;
			for (int i = 0; i <= num; i++)
			{
				DateTime valueAt;
				try
				{
					valueAt = base.Times[1].GetValueAt(i);
				}
				catch
				{
					continue;
				}
				if (!(valueAt < begin) && !(valueAt > dateTime2))
				{
					if (num2 == 0)
					{
						diagFirstVolBarTime = valueAt;
					}
					diagLastVolBarTime = valueAt;
					num2++;
					ProcessNativeVolumetricBarToMaps(i, dictionary, dictionary2, ref totalVol, ref pocKey, ref pocVol, ref maxDeltaAbs);
				}
			}
			lock (_lk)
			{
				_sessionDate = dateTime.Date;
				_nativeSessionBegin = begin;
				_nativeSessionEnd = end;
				_sessionFp = dictionary;
				_barFp = dictionary2;
				_barFpTotals.Clear();
				_sessionTotalVol = totalVol;
				_sessionPocKey = pocKey;
				_sessionPocVol = pocVol;
				_sessionMaxDeltaAbs = Math.Max(1L, maxDeltaAbs);
				_sessionMaxVolAbs = ((dictionary != null && dictionary.Count > 0) ? Math.Max(1L, dictionary.Values.Max((ILT_DEV_FOTPRINT_Level v) => v.Total)) : 1);
				_lastVaCalcVolume = -1L;
				_lastNativeVolBarProcessed = num;
				MarkExtendedImbalanceCacheDirtyNoLock();
				_diagVolBarsSummed = num2;
				_diagFirstVolBarTime = diagFirstVolBarTime;
				_diagLastVolBarTime = diagLastVolBarTime;
				_diagTotalBid = dictionary.Values.Sum((ILT_DEV_FOTPRINT_Level v) => v.BidVol);
				_diagTotalAsk = dictionary.Values.Sum((ILT_DEV_FOTPRINT_Level v) => v.AskVol);
			}
		}

		private void ProcessNativeVolumetricBarToMaps(int volBarIndex, Dictionary<int, ILT_DEV_FOTPRINT_Level> sessionMap, Dictionary<int, Dictionary<int, ILT_DEV_FOTPRINT_Level>> barMaps, ref long totalVol, ref int pocKey, ref long pocVol, ref long maxDeltaAbs)
		{
			if (_nativeVolBarsType == null || volBarIndex < 0)
			{
				return;
			}
			DateTime valueAt;
			double valueAt2;
			double valueAt3;
			double valueAt4;
			try
			{
				valueAt = base.Times[1].GetValueAt(volBarIndex);
				valueAt2 = base.Lows[1].GetValueAt(volBarIndex);
				valueAt3 = base.Highs[1].GetValueAt(volBarIndex);
				valueAt4 = base.Closes[1].GetValueAt(volBarIndex);
			}
			catch
			{
				return;
			}
			DateTime begin;
			DateTime end;
			if (!GetNativeSessionBounds(valueAt, out begin, out end) || valueAt < begin || valueAt > end)
			{
				return;
			}
			int primaryBarIndex = GetPrimaryBarIndex(valueAt, valueAt4);
			Dictionary<int, ILT_DEV_FOTPRINT_Level> value = null;
			if (primaryBarIndex >= 0 && !barMaps.TryGetValue(primaryBarIndex, out value))
			{
				value = (barMaps[primaryBarIndex] = new Dictionary<int, ILT_DEV_FOTPRINT_Level>());
			}
			double num = RowPriceStep();
			double num2 = KeyToPrice(PriceToKey(valueAt2));
			double num3 = base.Instrument.MasterInstrument.RoundToTickSize(valueAt3);
			for (double num4 = num2; num4 <= num3 + _tick * 0.51; num4 += num)
			{
				double num5 = base.Instrument.MasterInstrument.RoundToTickSize(num4);
				long num6 = 0L;
				long num7 = 0L;
				try
				{
					num6 = _nativeVolBarsType.Volumes[volBarIndex].GetBidVolumeForPrice(num5);
					num7 = _nativeVolBarsType.Volumes[volBarIndex].GetAskVolumeForPrice(num5);
				}
				catch
				{
					continue;
				}
				if (num6 > 0 || num7 > 0)
				{
					int key = PriceToKey(num5);
					AddExactBidAsk(sessionMap, key, num6, num7, ref totalVol, ref pocKey, ref pocVol, ref maxDeltaAbs);
					if (value != null)
					{
						AddExactBidAskNoStats(value, key, num6, num7);
					}
				}
			}
		}

		private void AddExactBidAsk(Dictionary<int, ILT_DEV_FOTPRINT_Level> map, int key, long bid, long ask, ref long totalVol, ref int pocKey, ref long pocVol, ref long maxDeltaAbs)
		{
			ILT_DEV_FOTPRINT_Level value;
			if (!map.TryGetValue(key, out value))
			{
				value = (map[key] = new ILT_DEV_FOTPRINT_Level());
			}
			long num = Math.Max(0L, bid);
			long num2 = Math.Max(0L, ask);
			value.BidVol += num;
			value.AskVol += num2;
			totalVol += num + num2;
			if (value.Total > pocVol)
			{
				pocVol = value.Total;
				pocKey = key;
			}
			long num3 = Math.Abs(value.Delta);
			if (num3 > maxDeltaAbs)
			{
				maxDeltaAbs = num3;
			}
		}

		private void AddExactBidAskNoStats(Dictionary<int, ILT_DEV_FOTPRINT_Level> map, int key, long bid, long ask)
		{
			ILT_DEV_FOTPRINT_Level value;
			if (!map.TryGetValue(key, out value))
			{
				value = (map[key] = new ILT_DEV_FOTPRINT_Level());
			}
			value.BidVol += Math.Max(0L, bid);
			value.AskVol += Math.Max(0L, ask);
		}

		private void ProcessNativeVolumetricBar(int volBarIndex)
		{
			if (_nativeVolBarsType == null || volBarIndex < 0)
			{
				return;
			}
			DateTime valueAt;
			double valueAt2;
			double valueAt3;
			double valueAt4;
			try
			{
				valueAt = base.Times[1].GetValueAt(volBarIndex);
				valueAt2 = base.Lows[1].GetValueAt(volBarIndex);
				valueAt3 = base.Highs[1].GetValueAt(volBarIndex);
				valueAt4 = base.Closes[1].GetValueAt(volBarIndex);
			}
			catch
			{
				return;
			}
			DateTime begin;
			DateTime end;
			if (!GetNativeSessionBounds(valueAt, out begin, out end) || valueAt < begin || valueAt > end)
			{
				return;
			}
			int primaryBarIndex = GetPrimaryBarIndex(valueAt, valueAt4);
			Dictionary<int, ILT_DEV_FOTPRINT_Level> value = null;
			lock (_lk)
			{
				if (primaryBarIndex >= 0 && !_barFp.TryGetValue(primaryBarIndex, out value))
				{
					value = new Dictionary<int, ILT_DEV_FOTPRINT_Level>();
					_barFp[primaryBarIndex] = value;
				}
				double num = RowPriceStep();
				double num2 = KeyToPrice(PriceToKey(valueAt2));
				double num3 = base.Instrument.MasterInstrument.RoundToTickSize(valueAt3);
				for (double num4 = num2; num4 <= num3 + _tick * 0.51; num4 += num)
				{
					double num5 = base.Instrument.MasterInstrument.RoundToTickSize(num4);
					long num6 = 0L;
					long num7 = 0L;
					try
					{
						num6 = _nativeVolBarsType.Volumes[volBarIndex].GetBidVolumeForPrice(num5);
						num7 = _nativeVolBarsType.Volumes[volBarIndex].GetAskVolumeForPrice(num5);
					}
					catch
					{
						continue;
					}
					if (num6 > 0 || num7 > 0)
					{
						int key = PriceToKey(num5);
						AddExactBidAsk(_sessionFp, key, num6, num7);
						if (value != null)
						{
							AddExactBidAsk(value, key, num6, num7);
						}
					}
				}
				MarkExtendedImbalanceCacheDirtyNoLock();
				_diagTotalBid = _sessionFp.Values.Sum((ILT_DEV_FOTPRINT_Level v) => v.BidVol);
				_diagTotalAsk = _sessionFp.Values.Sum((ILT_DEV_FOTPRINT_Level v) => v.AskVol);
				_diagVolBarsSummed = Math.Max(_diagVolBarsSummed, volBarIndex + 1);
				if (_diagFirstVolBarTime == DateTime.MinValue)
				{
					_diagFirstVolBarTime = valueAt;
				}
				_diagLastVolBarTime = valueAt;
			}
		}

		private void AddExactBidAsk(Dictionary<int, ILT_DEV_FOTPRINT_Level> map, int key, long bid, long ask)
		{
			ILT_DEV_FOTPRINT_Level value;
			if (!map.TryGetValue(key, out value))
			{
				value = (map[key] = new ILT_DEV_FOTPRINT_Level());
			}
			value.BidVol += Math.Max(0L, bid);
			value.AskVol += Math.Max(0L, ask);
			if (map == _sessionFp)
			{
				long num = Math.Max(0L, bid) + Math.Max(0L, ask);
				_sessionTotalVol += num;
				if (value.Total > _sessionPocVol)
				{
					_sessionPocVol = value.Total;
					_sessionPocKey = key;
				}
				long num2 = Math.Abs(value.Delta);
				if (num2 > _sessionMaxDeltaAbs)
				{
					_sessionMaxDeltaAbs = num2;
				}
				if (value.Total > _sessionMaxVolAbs)
				{
					_sessionMaxVolAbs = value.Total;
				}
			}
		}

		private void ProcessTrade(double price, long vol, int primaryBar, DateTime time, bool liveQuoteRule)
		{
			DateTime nyTradingSessionDate = GetNyTradingSessionDate(time);
			if (_sessionDate == DateTime.MinValue || nyTradingSessionDate != _sessionDate)
			{
				ResetSession(nyTradingSessionDate);
			}
			int num = PriceToKey(price);
			int side = ClassifyTrade(price, liveQuoteRule);
			lock (_lk)
			{
				AddToMap(_sessionFp, num, side, vol);
				ILT_DEV_FOTPRINT_Level iltDev_FP_Level = _sessionFp[num];
				if (iltDev_FP_Level.Total > _sessionPocVol)
				{
					_sessionPocVol = iltDev_FP_Level.Total;
					_sessionPocKey = num;
				}
				_sessionTotalVol += vol;
				long num2 = Math.Abs(iltDev_FP_Level.Delta);
				if (num2 > _sessionMaxDeltaAbs)
				{
					_sessionMaxDeltaAbs = num2;
				}
				if (iltDev_FP_Level.Total > _sessionMaxVolAbs)
				{
					_sessionMaxVolAbs = iltDev_FP_Level.Total;
				}
				if (primaryBar >= 0)
				{
					Dictionary<int, ILT_DEV_FOTPRINT_Level> value;
					if (!_barFp.TryGetValue(primaryBar, out value))
					{
						value = new Dictionary<int, ILT_DEV_FOTPRINT_Level>();
						_barFp[primaryBar] = value;
					}
					AddToMap(value, num, side, vol);
					MarkExtendedImbalanceCacheDirtyNoLock();
				}
			}
		}

		private void AddToMap(Dictionary<int, ILT_DEV_FOTPRINT_Level> map, int key, int side, long vol)
		{
			ILT_DEV_FOTPRINT_Level value;
			if (!map.TryGetValue(key, out value))
			{
				value = (map[key] = new ILT_DEV_FOTPRINT_Level());
			}
			if (side > 0)
			{
				value.AskVol += vol;
			}
			else if (side < 0)
			{
				value.BidVol += vol;
			}
			else if (_lastTradeDir >= 0)
			{
				value.AskVol += vol;
			}
			else
			{
				value.BidVol += vol;
			}
		}

		private int ClassifyTrade(double price, bool liveQuoteRule)
		{
			int num = 0;
			if (liveQuoteRule && _lastAsk < double.MaxValue && _lastBid > 0.0)
			{
				if (price >= _lastAsk - _tick * 0.5)
				{
					num = 1;
				}
				else if (price <= _lastBid + _tick * 0.5)
				{
					num = -1;
				}
			}
			if (num == 0)
			{
				num = TickRule(price);
			}
			_lastTradePrice = price;
			_lastTradeDir = num;
			return num;
		}

		private int TickRule(double price)
		{
			if (_lastTradePrice <= 0.0)
			{
				if (_lastTradeDir != 0)
				{
					return _lastTradeDir;
				}
				return 1;
			}
			if (price > _lastTradePrice)
			{
				return 1;
			}
			if (price < _lastTradePrice)
			{
				return -1;
			}
			if (_lastTradeDir != 0)
			{
				return _lastTradeDir;
			}
			return 1;
		}

		private void ResetSession(DateTime sessionDate)
		{
			lock (_lk)
			{
				_sessionDate = sessionDate.Date;
				if (_sessionFp != null)
				{
					_sessionFp.Clear();
				}
				_sessionPocKey = int.MinValue;
				_sessionPocVol = 0L;
				_sessionTotalVol = 0L;
				_sessionMaxDeltaAbs = 1L;
				_sessionMaxVolAbs = 1L;
				_lastVaKeys.Clear();
				_lastVaCalcVolume = -1L;
				_lastTradePrice = 0.0;
				_lastTradeDir = 0;
				_lastNativeVolBarProcessed = -1;
				_lastNativeRealtimeRefreshMs = 0L;
				_diagVolBarsSummed = 0;
				_diagFirstVolBarTime = DateTime.MinValue;
				_diagLastVolBarTime = DateTime.MinValue;
				_diagTotalBid = 0L;
				_diagTotalAsk = 0L;
				_imbalancePulseStartMs.Clear();
				_knownCandleImbalanceIds.Clear();
				_imbalancePulsePrimed = false;
				_imbalancePulseSessionDate = DateTime.MinValue;
				MarkExtendedImbalanceCacheDirtyNoLock();
			}
		}

		protected override void OnRender(ChartControl cc, ChartScale cs)
		{
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_024e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0253: Unknown result type (might be due to invalid IL or missing references)
			//IL_026d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0272: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d6: Unknown result type (might be due to invalid IL or missing references)
			if (!IltDevEsValida_ILT_DEV_SESSIONFOOT())
			{
				Draw.TextFixed((NinjaScriptBase)(object)this, "ILT_DEV_bloqueo", IltDevTextoBloqueo_ILT_DEV_SESSIONFOOT(), TextPosition.TopLeft);
				return;
			}
			base.OnRender(cc, cs);
			if (cc == null || cs == null || base.ChartPanel == null || base.RenderTarget == null)
			{
				return;
			}
			float width = base.RenderTarget.Size.Width;
			float height = base.RenderTarget.Size.Height;
			_lastRenderW = width;
			_lastRenderH = height;
			float num = width / Math.Max(1f, (float)((FrameworkElement)(object)cc).ActualWidth);
			float num2 = height / Math.Max(1f, (float)((FrameworkElement)(object)cc).ActualHeight);
			float dpi = Math.Max(num, num2);
			EnsureDX(dpi);
			if (!_dxOk)
			{
				return;
			}
			_lastDX = num;
			_lastDY = num2;
			float plotLeft = (float)base.ChartPanel.X;
			float plotRight = (float)(base.ChartPanel.X + base.ChartPanel.W);
			if (plotRight <= plotLeft + 20f * num)
			{
				plotLeft = 0f;
				plotRight = (float)((FrameworkElement)(object)base.ChartPanel).ActualWidth * num;
			}
			float panelWidth = Math.Max(1f, plotRight - plotLeft);
			_lastPlotLeft = plotLeft;
			_lastPlotRight = plotRight;
			float ladderWidth = ShowSessionLadder
				? Math.Min(panelWidth, Math.Min(Math.Max(80f * num, (float)SessionLadderWidth * num), Math.Max(80f * num, panelWidth * 0.42f)))
				: 0f;
			float ladderTravel = Math.Max(0f, panelWidth - ladderWidth);
			float ladderPosition = (float)Clamp(LadderPositionPct, 0, 100) / 100f;
			bool ladderUsesVisibleDeltaAxis = VisualMode == IltDevFootprintDisplayMode.DeltaLadder
				|| VisualMode == IltDevFootprintDisplayMode.DeltaProfile;
			// Delta modes are positioned by their visible zero axis, not by an
			// invisible bounding box. Thus 0% and 100% are the actual chart edges.
			float edgeInset = Math.Min(1f * num, panelWidth * 0.01f);
			float visibleAxisX = plotLeft + edgeInset + Math.Max(0f, panelWidth - edgeInset * 2f) * ladderPosition;
			float ladderX = ladderUsesVisibleDeltaAxis
				? visibleAxisX - ladderWidth * 0.5f
				: plotLeft + ladderTravel * ladderPosition;

			try
			{
				if (ShowCandleFootprint)
				{
					DrawBetaFootprints(cc, cs, plotLeft, plotRight, num, num2);
				}

				if (ShowExtendedImbalanceLines)
				{
					DrawExtendedImbalanceLines(cc, cs, plotLeft, plotRight, num, num2);
				}

				if (ShowSessionLadder && ladderWidth > 0f)
				{
					DrawBetaSideLadder(cs, ladderX, ladderWidth, height, num, num2);
				}
			}
			catch (Exception ex)
			{
				FreeDX();
				long nowTicks = DateTime.UtcNow.Ticks;
				if (nowTicks - _lastBetaRenderErrorTicks >= TimeSpan.TicksPerSecond * 5L)
				{
					_lastBetaRenderErrorTicks = nowTicks;
					base.Print((object)("ILT_DEV_FOOTPRINT_OFB render error: " + ex.Message));
				}
			}
		}

		private void DrawBetaFootprints(ChartControl cc, ChartScale cs, float plotLeft, float plotRight, float dX, float dY)
		{
			if (base.ChartBars == null || base.RenderTarget == null)
			{
				return;
			}

			int fromIndex = base.ChartBars.FromIndex;
			int toIndex = base.ChartBars.ToIndex;
			if (toIndex < fromIndex)
			{
				return;
			}

			Dictionary<int, Dictionary<int, ILT_DEV_FOTPRINT_Level>> maps = SnapshotVisibleFootprints(fromIndex, toIndex);
			if (maps == null || maps.Count == 0)
			{
				return;
			}

			int visibleBars = toIndex - fromIndex + 1;
			float firstX = cc.GetXByBarIndex(base.ChartBars, fromIndex);
			float lastX = cc.GetXByBarIndex(base.ChartBars, toIndex);
			float averageSpacing = visibleBars > 1 ? Math.Abs(lastX - firstX) / Math.Max(1, visibleBars - 1) : 40f * dX;
			if (visibleBars > 220 || averageSpacing < 5.5f * dX)
			{
				// At dense zoom levels the native chart/FlowSpeed candles remain untouched.
				// Rendering synthetic footprint bodies here would change their geometry.
				return;
			}

			double priceStep = RowPriceStep();
			double middlePrice = (cs.MaxValue + cs.MinValue) * 0.5;
			float rowHeight = Math.Max(1f, Math.Abs((float)cs.GetYByValue(middlePrice) - (float)cs.GetYByValue(middlePrice + priceStep)) * 0.90f);
			bool rowTextAllowed = rowHeight >= Math.Max(7f * dY, (float)MinRowHeightForText * dY * 0.70f);

			for (int barIndex = fromIndex; barIndex <= toIndex; barIndex++)
			{
				Dictionary<int, ILT_DEV_FOTPRINT_Level> map;
				if (!maps.TryGetValue(barIndex, out map) || map == null || map.Count == 0)
				{
					continue;
				}

				float centerX = cc.GetXByBarIndex(base.ChartBars, barIndex);
				if (centerX < plotLeft - 120f * dX || centerX > plotRight + 120f * dX)
				{
					continue;
				}

				float spacing = GetVisibleBarSpacing(cc, barIndex, dX);
				float cellWidth = GetBetaCandleCellWidth(spacing, dX);
				float renderOffset = GetCandleFootprintRenderOffset(cc, barIndex, dX);
				float left = centerX - cellWidth * 0.5f + renderOffset;
				bool textAllowed = ShowBidAskText && rowTextAllowed && cellWidth >= ((VisualMode == IltDevFootprintDisplayMode.SidedLadder || VisualMode == IltDevFootprintDisplayMode.SidedLadderPlus) ? 32f : 18f) * dX;
				RectangleF candleBody;
				double bodyLowPrice;
				double bodyHighPrice;
				float wickTop;
				float wickBottom;
				SolidColorBrush candleBrush;
				if (!TryGetBetaCandleGeometry(barIndex, cs, left, cellWidth, rowHeight, out candleBody, out bodyLowPrice, out bodyHighPrice, out wickTop, out wickBottom, out candleBrush))
				{
					continue;
				}
				long maxBid;
				long maxAsk;
				long maxTotal;
				long maxAbsDelta;
				long totalBid;
				long totalAsk;
				int pocKey;
				GetBetaMapStats(map, out maxBid, out maxAsk, out maxTotal, out maxAbsDelta, out totalBid, out totalAsk, out pocKey);
				double imbalanceRatio = (double)Math.Max(100, CandleImbalancePct) / 100.0;
				long imbalanceMinimum = Math.Max(1, CandleMinImbalance);
				HashSet<int> buyImbalanceKeys = ShowCandleImbalances ? GetStackFilteredImbalanceKeys(map, true, imbalanceRatio, imbalanceMinimum, CandleImbalanceStackRows) : _emptyKeySet;
				HashSet<int> sellImbalanceKeys = ShowCandleImbalances ? GetStackFilteredImbalanceKeys(map, false, imbalanceRatio, imbalanceMinimum, CandleImbalanceStackRows) : _emptyKeySet;

				foreach (KeyValuePair<int, ILT_DEV_FOTPRINT_Level> pair in map)
				{
					ILT_DEV_FOTPRINT_Level level = pair.Value;
					if (level == null || level.Total < Math.Max(0, MinVolumeToDisplay))
					{
						continue;
					}

					double price = KeyToPrice(pair.Key);
					if (price < cs.MinValue - priceStep || price > cs.MaxValue + priceStep)
					{
						continue;
					}

					float y = (float)cs.GetYByValue(price) - rowHeight * 0.5f;
					bool insideBody = price >= bodyLowPrice - priceStep * 0.5 && price <= bodyHighPrice + priceStep * 0.5;
					float rowLeft = left;
					float rowWidth = cellWidth;
					float rowTop = y;
					float rowBottom = y + rowHeight;
					if (insideBody)
					{
						rowTop = Math.Max(rowTop, candleBody.Top);
						rowBottom = Math.Min(rowBottom, candleBody.Bottom);
					}
					// Body and wick rows intentionally share the same horizontal geometry.
					// Their volume still controls the painted length, but their center axis,
					// POC and numeric columns can no longer jump at the open/close boundary.
					if (rowBottom > rowTop)
					{
						bool rowCanShowText = textAllowed && rowWidth >= 32f * dX;
						bool buyImbalance = buyImbalanceKeys.Contains(pair.Key);
						bool sellImbalance = sellImbalanceKeys.Contains(pair.Key);
						DrawBetaModeRow(new RectangleF(rowLeft, rowTop, rowWidth, rowBottom - rowTop), level, maxBid, maxAsk, maxTotal, maxAbsDelta, pair.Key == pocKey, rowCanShowText, true, buyImbalance, sellImbalance, dX, dY);
					}
				}

				float summaryReadableWidth = Math.Min(cellWidth, spacing * 0.92f);
				float summaryMinimumWidth = Math.Max(42f * dX, (float)FontSizeSmall * 4.2f * dX);
				bool summaryFitsWithoutOverlap = cellWidth <= spacing * 0.96f && summaryReadableWidth >= summaryMinimumWidth;
				if (ShowBarSummary && summaryFitsWithoutOverlap)
				{
					DrawBetaBarSummary(barIndex, cs, map, left, cellWidth, totalBid, totalAsk, dX, dY);
				}
			}
		}

		private void DrawBetaDenseFootprints(ChartControl cc, ChartScale cs, Dictionary<int, Dictionary<int, ILT_DEV_FOTPRINT_Level>> maps, int fromIndex, int toIndex, float plotLeft, float plotRight, float dX)
		{
			// Intentionally empty: the chart style and FlowSpeed own candle rendering.
		}

		private void DrawBetaSideLadder(ChartScale cs, float x, float width, float height, float dX, float dY)
		{
			Dictionary<int, ILT_DEV_FOTPRINT_Level> map;
			lock (_lk)
			{
				map = CloneFootprintMap(_sessionFp);
			}

			if (map == null || map.Count == 0)
			{
				DrawCenteredBetaText("ILT_DEV_FOOTPRINT_OFB - waiting for volumetric data", x + 4f * dX, 4f * dY, width - 8f * dX, 20f * dY, _fSmall, _bDim, dX, dY);
				return;
			}

			long maxBid;
			long maxAsk;
			long maxTotal;
			long maxAbsDelta;
			long totalBid;
			long totalAsk;
			int pocKey;
			GetBetaMapStats(map, out maxBid, out maxAsk, out maxTotal, out maxAbsDelta, out totalBid, out totalAsk, out pocKey);
			double ladderImbalanceRatio = (double)Math.Max(100, LadderImbalancePct) / 100.0;
			long ladderImbalanceMinimum = Math.Max(1, LadderMinImbalance);
			HashSet<int> ladderBuyImbalanceKeys = ShowCandleImbalances ? GetStackFilteredImbalanceKeys(map, true, ladderImbalanceRatio, ladderImbalanceMinimum, LadderImbalanceStackRows) : _emptyKeySet;
			HashSet<int> ladderSellImbalanceKeys = ShowCandleImbalances ? GetStackFilteredImbalanceKeys(map, false, ladderImbalanceRatio, ladderImbalanceMinimum, LadderImbalanceStackRows) : _emptyKeySet;
			double priceStep = RowPriceStep();
			double middlePrice = (cs.MaxValue + cs.MinValue) * 0.5;
			float rowHeight = Math.Max(1f, Math.Abs((float)cs.GetYByValue(middlePrice) - (float)cs.GetYByValue(middlePrice + priceStep)) * 0.90f);
			bool textAllowed = ShowBidAskText && rowHeight >= Math.Max(7f * dY, (float)MinRowHeightForText * dY * 0.66f);

			foreach (KeyValuePair<int, ILT_DEV_FOTPRINT_Level> pair in map)
			{
				ILT_DEV_FOTPRINT_Level level = pair.Value;
				if (level == null || level.Total < Math.Max(0, MinVolumeToDisplay))
				{
					continue;
				}
				double price = KeyToPrice(pair.Key);
				if (price < cs.MinValue - priceStep || price > cs.MaxValue + priceStep)
				{
					continue;
				}
				float y = (float)cs.GetYByValue(price) - rowHeight * 0.5f;
				DrawBetaModeRow(new RectangleF(x, y, width, rowHeight), level, maxBid, maxAsk, maxTotal, maxAbsDelta, pair.Key == pocKey, textAllowed, false, ladderBuyImbalanceKeys.Contains(pair.Key), ladderSellImbalanceKeys.Contains(pair.Key), dX, dY);
			}

			DrawCenteredBetaText(GetBetaModeCaption(), x + 3f * dX, 1f * dY, width - 6f * dX, 17f * dY, _fBold, _bText, dX, dY);
		}

		private void DrawBetaModeRow(RectangleF row, ILT_DEV_FOTPRINT_Level level, long maxBid, long maxAsk, long maxTotal, long maxAbsDelta, bool isPoc, bool textAllowed, bool isCandle, bool buyImbalance, bool sellImbalance, float dX, float dY)
		{
			if (row.Width <= 1f || row.Height <= 0.5f || level == null)
			{
				return;
			}

			float opacity = OpacityPct(isCandle ? FootprintOpacity : LadderOpacity);
			float bidRatio = (float)((double)Math.Max(0L, level.BidVol) / (double)Math.Max(1L, maxBid));
			float askRatio = (float)((double)Math.Max(0L, level.AskVol) / (double)Math.Max(1L, maxAsk));
			float totalRatio = (float)((double)Math.Max(0L, level.Total) / (double)Math.Max(1L, maxTotal));
			long delta = level.Delta;
			float deltaRatio = (float)((double)AbsBetaLong(delta) / (double)Math.Max(1L, maxAbsDelta));
			bool highlightedImbalance = buyImbalance || sellImbalance;
			SolidColorBrush bidNumberBrush = sellImbalance ? _bImbSell : _bText;
			SolidColorBrush askNumberBrush = buyImbalance ? _bImbBuy : _bText;
			SolidColorBrush deltaLevelBrush = buyImbalance ? _bImbBuy : (sellImbalance ? _bImbSell : (delta >= 0L ? _bDeltaPos : _bDeltaNeg));
			SolidColorBrush bidLevelBrush = sellImbalance ? _bImbSell : _bBid;
			SolidColorBrush askLevelBrush = buyImbalance ? _bImbBuy : _bAsk;
			SolidColorBrush profileLevelBrush = highlightedImbalance ? deltaLevelBrush : _bVol;
			float half = row.Width * 0.5f;
			float center = row.X + half;
			int renderDeltaDirection = DeltaHistogramDirection;
			if (!isCandle && (VisualMode == IltDevFootprintDisplayMode.DeltaLadder || VisualMode == IltDevFootprintDisplayMode.DeltaProfile))
			{
				// At an extreme, always render inward so no delta bars disappear
				// beyond the Direct2D clip while the zero axis remains exactly flush.
				if (center <= _lastPlotLeft + 1.5f * dX)
				{
					renderDeltaDirection = 3;
				}
				else if (center >= _lastPlotRight - 1.5f * dX)
				{
					renderDeltaDirection = 1;
				}
			}

			if (isCandle)
			{
				switch (VisualMode)
				{
				case IltDevFootprintDisplayMode.SidedLadder:
					FillBetaRectangle(new RectangleF(center - half * bidRatio, row.Y, half * bidRatio, row.Height), bidLevelBrush, sellImbalance ? 1f : opacity * (0.12f + 0.36f * bidRatio));
					FillBetaRectangle(new RectangleF(center, row.Y, half * askRatio, row.Height), askLevelBrush, buyImbalance ? 1f : opacity * (0.12f + 0.36f * askRatio));
					break;
				case IltDevFootprintDisplayMode.SidedLadderPlus:
					// PLUS keeps the two-sided ladder and stronger level emphasis.  A
					// second total-profile fill here produced the duplicated profile seen
					// inside the candle.
					FillBetaRectangle(new RectangleF(center - half * bidRatio, row.Y, half * bidRatio, row.Height), bidLevelBrush, sellImbalance ? 1f : opacity * (0.14f + 0.34f * bidRatio));
					FillBetaRectangle(new RectangleF(center, row.Y, half * askRatio, row.Height), askLevelBrush, buyImbalance ? 1f : opacity * (0.14f + 0.34f * askRatio));
					break;
				case IltDevFootprintDisplayMode.DeltaLadder:
					// Ladder mode is deliberately numeric-only. The proportional
					// histogram belongs exclusively to Delta Profile.
					break;
				case IltDevFootprintDisplayMode.DeltaProfile:
					float candleDeltaCapacity = half;
					float candleDeltaWidth = Math.Max(1f, candleDeltaCapacity * deltaRatio);
					((SharpDX.Direct2D1.Brush)deltaLevelBrush).Opacity = highlightedImbalance ? 1f : opacity * (0.20f + 0.48f * deltaRatio);
					FillCenterAnchoredBar(center, candleDeltaCapacity, candleDeltaWidth, renderDeltaDirection, row.Y, row.Height, deltaLevelBrush, delta >= 0L);
					break;
				case IltDevFootprintDisplayMode.Profile:
					float candleProfileWidth = Math.Max(1f, row.Width * totalRatio);
					((SharpDX.Direct2D1.Brush)profileLevelBrush).Opacity = highlightedImbalance ? 1f : opacity * (0.22f + 0.38f * totalRatio);
					FillDirectionalBar(row.X, row.Width, candleProfileWidth, VolumeProfileDirection, row.Y, row.Height, profileLevelBrush, false, true);
					break;
				}

				FillBetaRectangle(new RectangleF(center - 0.4f * dX, row.Y, Math.Max(0.8f * dX, 0.5f), row.Height), _bGrid, 0.46f);
				if (VisualMode == IltDevFootprintDisplayMode.DeltaLadder || VisualMode == IltDevFootprintDisplayMode.DeltaProfile)
				{
					DrawBetaDeltaNumber(row, delta, deltaLevelBrush, textAllowed, renderDeltaDirection, dX, dY);
				}
				else
				{
					DrawBetaBidAskNumbers(row, level, bidNumberBrush, askNumberBrush, textAllowed, dX, dY);
				}
			}
			else
			{
				switch (VisualMode)
				{
				case IltDevFootprintDisplayMode.SidedLadder:
					FillBetaRectangle(new RectangleF(center - half * bidRatio, row.Y, half * bidRatio, row.Height), bidLevelBrush, sellImbalance ? 1f : opacity * (0.22f + 0.58f * bidRatio));
					FillBetaRectangle(new RectangleF(center, row.Y, half * askRatio, row.Height), askLevelBrush, buyImbalance ? 1f : opacity * (0.22f + 0.58f * askRatio));
					DrawBetaBidAskNumbers(row, level, bidNumberBrush, askNumberBrush, textAllowed, dX, dY);
					break;
				case IltDevFootprintDisplayMode.SidedLadderPlus:
					float plusValueWidth = Math.Max(1f, row.Width * 0.78f * totalRatio);
					FillBetaRectangle(new RectangleF(center - plusValueWidth * 0.5f, row.Y, plusValueWidth, row.Height), _bVol, opacity * (0.20f + 0.38f * totalRatio));
					float plusDeltaWidth = Math.Max(1f, row.Width * 0.30f * deltaRatio);
					FillBetaRectangle(new RectangleF(delta >= 0L ? center : center - plusDeltaWidth, row.Y, plusDeltaWidth, row.Height), deltaLevelBrush, highlightedImbalance ? 1f : opacity * (0.34f + 0.54f * deltaRatio));
					DrawBetaBidAskNumbers(row, level, bidNumberBrush, askNumberBrush, textAllowed, dX, dY);
					break;
				case IltDevFootprintDisplayMode.DeltaLadder:
					DrawBetaDeltaNumber(row, delta, deltaLevelBrush, textAllowed, renderDeltaDirection, dX, dY);
					break;
				case IltDevFootprintDisplayMode.Profile:
					float profileWidth = Math.Max(1f, row.Width * totalRatio);
					((SharpDX.Direct2D1.Brush)profileLevelBrush).Opacity = highlightedImbalance ? 1f : opacity * (0.24f + 0.50f * totalRatio);
					FillDirectionalBar(row.X, row.Width, profileWidth, VolumeProfileDirection, row.Y, row.Height, profileLevelBrush, false, true);
					DrawBetaBidAskNumbers(row, level, bidNumberBrush, askNumberBrush, textAllowed, dX, dY);
					break;
				case IltDevFootprintDisplayMode.DeltaProfile:
					float deltaProfileCapacity = half;
					float deltaProfileWidth = Math.Max(1f, deltaProfileCapacity * deltaRatio);
					((SharpDX.Direct2D1.Brush)deltaLevelBrush).Opacity = highlightedImbalance ? 1f : opacity * (0.22f + 0.66f * deltaRatio);
					FillCenterAnchoredBar(center, deltaProfileCapacity, deltaProfileWidth, renderDeltaDirection, row.Y, row.Height, deltaLevelBrush, delta >= 0L);
					DrawBetaDeltaNumber(row, delta, deltaLevelBrush, textAllowed, renderDeltaDirection, dX, dY);
					break;
				}
			}

			if (isPoc)
			{
				float pocPad = isCandle ? Math.Max(3f * dX, 2f) : 0f;
				RectangleF pocRect = new RectangleF(row.X - pocPad, row.Y, row.Width + pocPad * 2f, row.Height);
				DrawBetaOutline(pocRect, _bPoc, 1f, Math.Max(1f, 1.5f * dX));
			}
		}

		private void DrawBetaBidAskNumbers(RectangleF rect, ILT_DEV_FOTPRINT_Level level, SolidColorBrush bidBrush, SolidColorBrush askBrush, bool textAllowed, float dX, float dY)
		{
			if (!textAllowed || level == null)
			{
				return;
			}
			float half = rect.Width * 0.5f;
			DrawCenteredBetaText(FQ(level.BidVol), rect.X, rect.Y, half, rect.Height, _fText, bidBrush, dX, dY);
			DrawCenteredBetaText(FQ(level.AskVol), rect.X + half, rect.Y, rect.Width - half, rect.Height, _fText, askBrush, dX, dY);
		}

		private void DrawBetaDeltaNumber(RectangleF rect, long delta, SolidColorBrush brush, bool textAllowed, int direction, float dX, float dY)
		{
			if (!textAllowed || brush == null || rect.Width <= 1f)
			{
				return;
			}
			float half = rect.Width * 0.5f;
			float center = rect.X + half;
			int align = Math.Max(1, Math.Min(3, direction));
			float textX = align == 1 ? rect.X : (align == 3 ? center : rect.X);
			float textWidth = align == 2 ? rect.Width : half;
			SolidColorBrush numberBrush = delta == 0L && _bDeltaZero != null ? _bDeltaZero : brush;
			DrawCenteredBetaText(FormatBetaDeltaValue(delta), textX, rect.Y, textWidth, rect.Height, _fText, numberBrush, dX, dY);
		}

		private string FormatBetaDeltaValue(long value)
		{
			return FQ(AbsBetaLong(value));
		}

		private bool TryGetBetaCandleGeometry(int barIndex, ChartScale cs, float x, float width, float rowHeight, out RectangleF body, out double bodyLowPrice, out double bodyHighPrice, out float wickTop, out float wickBottom, out SolidColorBrush candleBrush)
		{
			body = new RectangleF();
			bodyLowPrice = 0.0;
			bodyHighPrice = 0.0;
			wickTop = 0f;
			wickBottom = 0f;
			candleBrush = _bNeutral;
			if (cs == null || barIndex < 0)
			{
				return false;
			}

			double open;
			double high;
			double low;
			double close;
			try
			{
				open = base.Opens[0].GetValueAt(barIndex);
				high = base.Highs[0].GetValueAt(barIndex);
				low = base.Lows[0].GetValueAt(barIndex);
				close = base.Closes[0].GetValueAt(barIndex);
			}
			catch
			{
				return false;
			}

			bodyLowPrice = Math.Min(open, close);
			bodyHighPrice = Math.Max(open, close);
			float yOpen = (float)cs.GetYByValue(open);
			float yClose = (float)cs.GetYByValue(close);
			float bodyTop = Math.Min(yOpen, yClose) - rowHeight * 0.45f;
			float bodyBottom = Math.Max(yOpen, yClose) + rowHeight * 0.45f;
			if (bodyBottom - bodyTop < Math.Max(1f, rowHeight))
			{
				float middle = (bodyTop + bodyBottom) * 0.5f;
				bodyTop = middle - Math.Max(1f, rowHeight) * 0.5f;
				bodyBottom = middle + Math.Max(1f, rowHeight) * 0.5f;
			}
			body = new RectangleF(x, bodyTop, width, bodyBottom - bodyTop);
			wickTop = (float)cs.GetYByValue(high);
			wickBottom = (float)cs.GetYByValue(low);
			if (wickBottom < wickTop)
			{
				float swap = wickTop;
				wickTop = wickBottom;
				wickBottom = swap;
			}
			candleBrush = close > open ? _bAsk : (close < open ? _bBid : _bNeutral);
			return candleBrush != null;
		}

		private void DrawBetaJapaneseCandleBase(RectangleF body, float wickTop, float wickBottom, SolidColorBrush candleBrush, float dX)
		{
			if (base.RenderTarget == null || candleBrush == null || body.Width <= 0f || body.Height <= 0f)
			{
				return;
			}
			float center = body.X + body.Width * 0.5f;
			((SharpDX.Direct2D1.Brush)candleBrush).Opacity = 0.92f;
			base.RenderTarget.DrawLine(new Vector2(center, wickTop), new Vector2(center, wickBottom), (SharpDX.Direct2D1.Brush)(object)candleBrush, Math.Max(1f, 1.15f * dX));
			FillBetaRectangle(body, candleBrush, 0.10f);
		}

		private void DrawBetaBarSummary(int barIndex, ChartScale cs, Dictionary<int, ILT_DEV_FOTPRINT_Level> map, float x, float width, long bid, long ask, float dX, float dY)
		{
			if (!ShowBarSummary || cs == null || map == null || map.Count == 0 || _bSummaryTotal == null)
			{
				return;
			}

			long total = Math.Max(0L, bid) + Math.Max(0L, ask);
			long delta = ask - bid;
			double pct = total > 0L ? (double)AbsBetaLong(delta) * 100.0 / (double)total : 0.0;
			float ratio = total > 0L ? Math.Min(1f, (float)((double)AbsBetaLong(delta) / (double)total)) : 0f;
			float highY;
			float lowY;
			try
			{
				highY = (float)cs.GetYByValue(base.Highs[0].GetValueAt(barIndex));
				lowY = (float)cs.GetYByValue(base.Lows[0].GetValueAt(barIndex));
			}
			catch
			{
				highY = (float)cs.GetYByValue(KeyToPrice(map.Keys.Max()));
				lowY = (float)cs.GetYByValue(KeyToPrice(map.Keys.Min()));
			}
			if (lowY < highY)
			{
				float swap = lowY;
				lowY = highY;
				highY = swap;
			}

			float summaryWidth = Math.Max(1f, width);
			float center = x + width * 0.5f;
			float left = center - summaryWidth * 0.5f;
			// Fixed DT-style summary geometry. Its width still follows the candle
			// automatically; these vertical details are deliberately not user-sized.
			float barHeight = Math.Max(1f * dY, 3f * dY);
			float gap = 4f * dY;
			float textHeight = Math.Max(12f * dY, ((float)FontSizeSmall + 4f) * dY);
			float borderWidth = Math.Max(1f * dX, 1f * dX);
			SolidColorBrush deltaBrush = delta >= 0L ? _bDeltaPos : _bDeltaNeg;
			string deltaText = "Δ " + (delta > 0L ? "+" : "") + FQ(delta) + " (" + pct.ToString("0.0") + "%)";
			string totalText = "Σ " + FQ(total);
			float totalY;
			float barY;
			float deltaY;

			if (delta >= 0L)
			{
				totalY = highY - gap - textHeight;
				barY = totalY - gap - barHeight;
				deltaY = barY - gap - textHeight;
			}
			else
			{
				totalY = lowY + gap;
				barY = totalY + textHeight + gap;
				deltaY = barY + barHeight + gap;
			}

			DrawCenteredBetaText(totalText, left, totalY, summaryWidth, textHeight, _fSmall, _bSummaryTotal, dX, dY);
			DrawBetaOutline(new RectangleF(left, barY, summaryWidth, barHeight), _bGrid, 0.92f, borderWidth);
			if (ratio > 0f)
			{
				float innerInset = Math.Min(borderWidth, barHeight * 0.35f);
				float innerWidth = Math.Max(0f, summaryWidth - innerInset * 2f);
				float fillWidth = innerWidth * ratio;
				float fillX = delta >= 0L ? left + innerInset : left + summaryWidth - innerInset - fillWidth;
				FillBetaRectangle(new RectangleF(fillX, barY + innerInset, fillWidth, Math.Max(0.75f, barHeight - innerInset * 2f)), deltaBrush, 0.98f);
			}
			DrawCenteredBetaText(deltaText, left, deltaY, summaryWidth, textHeight, _fSmall, deltaBrush, dX, dY);
		}

		private void GetBetaMapStats(Dictionary<int, ILT_DEV_FOTPRINT_Level> map, out long maxBid, out long maxAsk, out long maxTotal, out long maxAbsDelta, out long totalBid, out long totalAsk, out int pocKey)
		{
			maxBid = 1L;
			maxAsk = 1L;
			maxTotal = 1L;
			maxAbsDelta = 1L;
			totalBid = 0L;
			totalAsk = 0L;
			pocKey = int.MinValue;
			if (map == null)
			{
				return;
			}
			foreach (KeyValuePair<int, ILT_DEV_FOTPRINT_Level> pair in map)
			{
				ILT_DEV_FOTPRINT_Level level = pair.Value;
				if (level == null)
				{
					continue;
				}
				maxBid = Math.Max(maxBid, Math.Max(0L, level.BidVol));
				maxAsk = Math.Max(maxAsk, Math.Max(0L, level.AskVol));
				if (level.Total > maxTotal || pocKey == int.MinValue)
				{
					maxTotal = Math.Max(1L, level.Total);
					pocKey = pair.Key;
				}
				maxAbsDelta = Math.Max(maxAbsDelta, AbsBetaLong(level.Delta));
				totalBid += Math.Max(0L, level.BidVol);
				totalAsk += Math.Max(0L, level.AskVol);
			}
		}

		private long AbsBetaLong(long value)
		{
			return value == long.MinValue ? long.MaxValue : Math.Abs(value);
		}

		private string GetBetaModeCaption()
		{
			switch (VisualMode)
			{
			case IltDevFootprintDisplayMode.SidedLadder:
				return "SIDED LADDER";
			case IltDevFootprintDisplayMode.SidedLadderPlus:
				return "SIDED LADDER PLUS";
			case IltDevFootprintDisplayMode.DeltaLadder:
				return "DELTA LADDER";
			case IltDevFootprintDisplayMode.Profile:
				return "PROFILE";
			default:
				return "DELTA PROFILE";
			}
		}

		private void FillBetaRectangle(RectangleF rect, SolidColorBrush brush, float opacity)
		{
			if (base.RenderTarget == null || brush == null || rect.Width <= 0f || rect.Height <= 0f)
			{
				return;
			}
			((SharpDX.Direct2D1.Brush)brush).Opacity = Math.Max(0f, Math.Min(1f, opacity));
			base.RenderTarget.FillRectangle(rect, (SharpDX.Direct2D1.Brush)(object)brush);
		}

		private void DrawBetaOutline(RectangleF rect, SolidColorBrush brush, float opacity, float strokeWidth)
		{
			if (base.RenderTarget == null || brush == null || rect.Width <= 0f || rect.Height <= 0f)
			{
				return;
			}
			((SharpDX.Direct2D1.Brush)brush).Opacity = Math.Max(0f, Math.Min(1f, opacity));
			base.RenderTarget.DrawRectangle(rect, (SharpDX.Direct2D1.Brush)(object)brush, Math.Max(0.5f, strokeWidth));
		}

		private void DrawCenteredBetaText(string text, float x, float y, float width, float height, TextFormat format, SolidColorBrush brush, float dX, float dY)
		{
			if (string.IsNullOrEmpty(text) || format == null || brush == null || width <= 1f || height <= 1f)
			{
				return;
			}
			float fontPixels = (format == _fSmall ? (float)FontSizeSmall : (format == _fBold ? (float)FontSizeBold : (float)FontSizeText)) * dY;
			float estimatedWidth = (float)text.Length * fontPixels * 0.60f;
			float textX = x + Math.Max(1f, (width - estimatedWidth) * 0.5f);
			float textY = y + Math.Max(0f, (height - fontPixels - 1f * dY) * 0.5f);
			((SharpDX.Direct2D1.Brush)brush).Opacity = 0.98f;
			T(text, textX, textY, format, brush, Math.Max(1f, width - (textX - x)), height);
		}

		private float GetCompactSessionLadderWidth(float dX)
		{
			float num = Math.Max(60f * dX, (float)Math.Max(DeltaColumnWidth, BidAskColumnWidth) * dX);
			float num2 = (ShowSessionDeltaProfile ? num : 0f);
			float num3 = (ShowSessionBidAskColumn ? num : 0f);
			float num4 = (ShowSessionVolumeProfile ? Math.Max(48f * dX, (float)VolumeProfileWidth * dX) : 0f);
			float num5 = (ShowPriceColumn ? Math.Max(48f * dX, (float)PriceColumnWidth * dX) : 0f);
			return Math.Max(120f * dX, num2 + num4 + num3 + num5 + 4f * dX);
		}

		private bool ShouldSkipDenseCandleRender(ChartControl cc, float cL, float cR, float dX, bool extendedLines)
		{
			if (cc == null || base.ChartBars == null)
			{
				return true;
			}
			int fromIndex = base.ChartBars.FromIndex;
			int toIndex = base.ChartBars.ToIndex;
			if (toIndex < fromIndex)
			{
				return true;
			}
			int num = toIndex - fromIndex + 1;
			int num2 = (extendedLines ? 220 : 160);
			if (num > num2)
			{
				return true;
			}
			float num3 = (extendedLines ? 7.5f : 5.25f) * Math.Max(1f, dX);
			int num4 = Math.Min(toIndex, fromIndex + 12);
			if (num4 > fromIndex)
			{
				try
				{
					float num5 = cc.GetXByBarIndex(base.ChartBars, fromIndex);
					float num6 = cc.GetXByBarIndex(base.ChartBars, num4);
					float num7 = Math.Abs(num6 - num5) / (float)Math.Max(1, num4 - fromIndex);
					if (num7 < num3)
					{
						return true;
					}
				}
				catch
				{
				}
			}
			float num8 = Math.Max(1f, cR - cL);
			float num9 = num8 / (float)Math.Max(1, num);
			return num9 < num3;
		}

		private bool ShouldUseCompactSessionLadder(ChartScale cs, double priceStep, float rowH, int totalRows, float dY)
		{
			if (cs == null || priceStep <= 0.0)
			{
				return true;
			}
			if (rowH < 0.18f * Math.Max(1f, dY))
			{
				return true;
			}
			return false;
		}

		private void DrawSessionLadder(ChartControl cc, ChartScale cs, float x0, float w, float H, float dX, float dY)
		{
			//IL_0296: Unknown result type (might be due to invalid IL or missing references)
			//IL_06ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_06b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_067f: Unknown result type (might be due to invalid IL or missing references)
			//IL_08bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_08ed: Unknown result type (might be due to invalid IL or missing references)
			//IL_08f6: Unknown result type (might be due to invalid IL or missing references)
			Dictionary<int, ILT_DEV_FOTPRINT_Level> sessionLadderSnapshot;
			int sessionPocKey;
			long sessionPocVol;
			long sessionTotalVol;
			long num;
			lock (_lk)
			{
				if (_sessionFp == null || _sessionFp.Count == 0)
				{
					return;
				}
				if (_sessionLadderSnapshot == null || _sessionLadderSnapshotVersion != _footprintDataVersion)
				{
					_sessionLadderSnapshot = CloneFootprintMap(_sessionFp);
					_sessionLadderSnapshotVersion = _footprintDataVersion;
				}
				sessionLadderSnapshot = _sessionLadderSnapshot;
				sessionPocKey = _sessionPocKey;
				sessionPocVol = _sessionPocVol;
				sessionTotalVol = _sessionTotalVol;
				num = Math.Max(1L, _sessionMaxDeltaAbs);
			}
			if (sessionLadderSnapshot.Count == 0)
			{
				return;
			}
			long num2 = 1L;
			foreach (ILT_DEV_FOTPRINT_Level value in sessionLadderSnapshot.Values)
			{
				if (value.Total > num2)
				{
					num2 = value.Total;
				}
			}
			float num3 = Math.Max(60f * dX, (float)Math.Max(DeltaColumnWidth, BidAskColumnWidth) * dX);
			float num4 = (ShowSessionDeltaProfile ? num3 : 0f);
			float num5 = (ShowSessionBidAskColumn ? num3 : 0f);
			float num6 = (ShowSessionVolumeProfile ? Math.Max(48f * dX, (float)VolumeProfileWidth * dX) : 0f);
			float num7 = (ShowPriceColumn ? ((float)PriceColumnWidth * dX) : 0f);
			float h = 20f * dY;
			float num8 = num4 + num6 + num5 + num7;
			float num9 = Math.Max(1f, w - 2f * dX);
			if (num8 > 0f)
			{
				float val = num9 / num8;
				val = Math.Max(0.35f, Math.Min(6f, val));
				num4 *= val;
				num6 *= val;
				num5 *= val;
				num7 *= val;
			}
			float num10 = Math.Max(1f, num4 + num6 + num5 + num7);
			if (num10 > num9)
			{
				float num11 = num9 / num10;
				num4 *= num11;
				num6 *= num11;
				num5 *= num11;
				num7 *= num11;
			}
			float num12 = x0 + dX;
			float num13 = num12 + num4;
			float num14 = num13 + num6;
			float num15 = num14 + num5;
			float num16 = num4 + num6 + num5 + num7;
			float num17 = ((num16 > 0f) ? Math.Min(x0 + w, num12 + num16) : (x0 + w));
			((SharpDX.Direct2D1.Brush)_bPanel).Opacity = OpacityPct(LadderOpacity);
			base.RenderTarget.FillRectangle(new RectangleF(x0, 0f, w, H), (SharpDX.Direct2D1.Brush)(object)_bPanel);
			if (ShowSessionDeltaProfile)
			{
				DrawHeaderCell("Δ", num12, 0f, num4, h);
			}
			if (ShowSessionVolumeProfile)
			{
				DrawHeaderCell("VOL", num13, 0f, num6, h);
			}
			if (ShowSessionBidAskColumn)
			{
				DrawHeaderCell("BID x ASK", num14, 0f, num5, h);
			}
			if (ShowPriceColumn)
			{
				DrawHeaderCell("PRICE", num15, 0f, num7, h);
			}
			DrawVLine(x0, H, dX);
			if (ShowSessionVolumeProfile)
			{
				DrawVLine(num13, H, dX);
			}
			if (ShowSessionBidAskColumn)
			{
				DrawVLine(num14, H, dX);
			}
			if (ShowPriceColumn)
			{
				DrawVLine(num15, H, dX);
			}
			DrawVLine(num17, H, dX);
			double num18 = RowPriceStep();
			double num19 = (cs.MaxValue + cs.MinValue) * 0.5;
			float num20 = Math.Max(1f, Math.Abs((float)cs.GetYByValue(num19) - (float)cs.GetYByValue(num19 + num18)));
			float num21 = Math.Max(10f * dY, ((float)FontSizeText + 3f) * dY);
			bool flag = ShowSessionBidAskColumn && ShowBidAskText && num20 >= Math.Max((float)MinRowHeightForText * dY, num21 * 0.82f);
			bool flag2 = ShowSessionRowGrid && num20 >= 2.5f * dY;
			if (ShouldUseCompactSessionLadder(cs, num18, num20, sessionLadderSnapshot.Count, dY))
			{
				flag = false;
				flag2 = false;
			}
			_ladderKeyBuffer.Clear();
			foreach (int key in sessionLadderSnapshot.Keys)
			{
				_ladderKeyBuffer.Add(key);
			}
			_ladderKeyBuffer.Sort((int a, int b) => b.CompareTo(a));
			List<int> ladderKeyBuffer = _ladderKeyBuffer;
			double num22 = cs.MinValue - num18;
			double num23 = cs.MaxValue + num18;
			double ratio = (double)Math.Max(100, LadderImbalancePct) / 100.0;
			long minDominant = Math.Max(1L, LadderMinImbalance);
			HashSet<int> hashSet = (ShowSessionLadderImbalances ? GetStackFilteredImbalanceKeys(sessionLadderSnapshot, true, ratio, minDominant, LadderImbalanceStackRows) : _emptyKeySet);
			HashSet<int> hashSet2 = (ShowSessionLadderImbalances ? GetStackFilteredImbalanceKeys(sessionLadderSnapshot, false, ratio, minDominant, LadderImbalanceStackRows) : _emptyKeySet);
			HashSet<int> hashSet3 = (ShowSessionValueArea ? GetValueAreaKeys(sessionLadderSnapshot, sessionPocKey, sessionTotalVol) : _emptyKeySet);
			bool flag3 = sessionPocKey != int.MinValue && sessionLadderSnapshot.ContainsKey(sessionPocKey) && sessionLadderSnapshot[sessionPocKey].Delta >= 0;
			foreach (int item in ladderKeyBuffer)
			{
				double num24 = KeyToPrice(item);
				if (num24 < num22 || num24 > num23)
				{
					continue;
				}
				ILT_DEV_FOTPRINT_Level iltDev_FP_Level = sessionLadderSnapshot[item];
				if (iltDev_FP_Level.Total < MinVolumeToDisplay)
				{
					continue;
				}
				float num25 = cs.GetYByValue(num24);
				if (num25 < 0f - num20 || num25 > H + num20)
				{
					continue;
				}
				float num26 = num25 - num20 * 0.5f;
				float h2 = Math.Max(1f, num20 - 1f);
				if (ShowSessionValueArea && hashSet3.Contains(item))
				{
					SolidColorBrush val2 = (flag3 ? _bVaBuy : _bVaSell);
					((SharpDX.Direct2D1.Brush)val2).Opacity = Math.Min(0.32f, OpacityPct(ValueAreaOpacity) * 0.8f);
					base.RenderTarget.FillRectangle(new RectangleF(num12, num26, Math.Max(1f, num17 - num12), num20), (SharpDX.Direct2D1.Brush)(object)val2);
				}
				if (flag2)
				{
					((SharpDX.Direct2D1.Brush)_bGrid).Opacity = 0.14f;
					base.RenderTarget.DrawLine(new Vector2(x0, num26 + num20), new Vector2(num17, num26 + num20), (SharpDX.Direct2D1.Brush)(object)_bGrid, 0.45f * dX);
				}
				if (ShowSessionDeltaProfile && num4 > 0f)
				{
					float barW = (float)Math.Min((double)Math.Abs(iltDev_FP_Level.Delta) / (double)num, 1.0) * Math.Max(1f, num4 - 2f * dX);
					if (iltDev_FP_Level.Delta > 0)
					{
						((SharpDX.Direct2D1.Brush)_bDeltaPos).Opacity = OpacityPct(DeltaOpacity);
						FillDirectionalBar(num12 + dX, num4 - 2f * dX, barW, DeltaHistogramDirection, num26 + 0.5f, h2, _bDeltaPos, true, true);
					}
					else if (iltDev_FP_Level.Delta < 0)
					{
						((SharpDX.Direct2D1.Brush)_bDeltaNeg).Opacity = OpacityPct(DeltaOpacity);
						FillDirectionalBar(num12 + dX, num4 - 2f * dX, barW, DeltaHistogramDirection, num26 + 0.5f, h2, _bDeltaNeg, true, false);
					}
				}
				if (ShowSessionVolumeProfile && num6 > 0f && iltDev_FP_Level.Total > 0)
				{
					float barW2 = (float)Math.Min((double)iltDev_FP_Level.Total / (double)num2, 1.0) * Math.Max(1f, num6 - 2f * dX);
					((SharpDX.Direct2D1.Brush)_bVol).Opacity = OpacityPct(VolumeProfileOpacity);
					FillDirectionalBar(num13 + dX, num6 - 2f * dX, barW2, VolumeProfileDirection, num26 + 0.5f, h2, _bVol, false, true);
				}
				bool flag4 = hashSet.Contains(item);
				bool flag5 = hashSet2.Contains(item);
				if (flag4 || flag5)
				{
					SolidColorBrush val3 = (flag4 ? _bImbBuy : _bImbSell);
					((SharpDX.Direct2D1.Brush)val3).Opacity = 0.12f;
					base.RenderTarget.FillRectangle(new RectangleF(num12, num26, Math.Max(1f, num17 - num12), num20), (SharpDX.Direct2D1.Brush)(object)val3);
					((SharpDX.Direct2D1.Brush)val3).Opacity = 0.62f;
					float num27 = (ShowSessionBidAskColumn ? num14 : x0);
					base.RenderTarget.DrawLine(new Vector2(num27, num25), new Vector2(num17, num25), (SharpDX.Direct2D1.Brush)(object)val3, 1.15f * dX);
				}
				if (flag && num5 > 0f)
				{
					DrawBidAskText(iltDev_FP_Level.BidVol, iltDev_FP_Level.AskVol, flag5, flag4, num14, num25 - num21 * 0.5f, num5, num21);
				}
				if (ShowPriceColumn && num20 >= Math.Max(7f * dY, num21 * 0.82f))
				{
					((SharpDX.Direct2D1.Brush)_bDim).Opacity = 0.78f;
					string txt = num24.ToString("F" + _decs);
					T(txt, num15 + 3f * dX, num25 - num21 * 0.5f, _fSmall, _bDim, num7 - 6f * dX, num21);
				}
			}
			DrawSessionPocLine(x0, num17, dX, dY, sessionPocKey, sessionPocVol, sessionLadderSnapshot, cs, hashSet3);
		}

		private void DrawSessionPocLine(float x0, float xEnd, float dX, float dY, int pocKey, long pocVol, Dictionary<int, ILT_DEV_FOTPRINT_Level> map, ChartScale cs, HashSet<int> vaKeys)
		{
			//IL_019c: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
			if (cs == null || base.RenderTarget == null || map == null)
			{
				return;
			}
			double num = RowPriceStep();
			bool ladderLeft = LadderSide == 2;
			int num2 = int.MinValue;
			int num3 = int.MaxValue;
			if (ShowSessionValueArea && vaKeys != null && vaKeys.Count > 0)
			{
				foreach (int vaKey in vaKeys)
				{
					if (vaKey > num2)
					{
						num2 = vaKey;
					}
					if (vaKey < num3)
					{
						num3 = vaKey;
					}
				}
			}
			if (ShowSessionValueArea && num2 != int.MinValue)
			{
				double price = KeyToPrice(num2);
				DrawLadderLevelTag(x0, xEnd, dX, dY, cs, price, "VAH " + price.ToString("F" + _decs), _bVaBuy, ladderLeft, 1f);
			}
			if (ShowSessionValueArea && num3 != int.MaxValue && num3 != num2)
			{
				double price2 = KeyToPrice(num3);
				DrawLadderLevelTag(x0, xEnd, dX, dY, cs, price2, "VAL " + price2.ToString("F" + _decs), _bVaSell, ladderLeft, 1f);
			}
			if (pocKey != int.MinValue && map.ContainsKey(pocKey))
			{
				double num4 = KeyToPrice(pocKey);
				if (num4 >= cs.MinValue - num && num4 <= cs.MaxValue + num)
				{
					float num5 = cs.GetYByValue(num4);
					((SharpDX.Direct2D1.Brush)_bPoc).Opacity = 0.96f;
					base.RenderTarget.DrawLine(new Vector2(x0, num5), new Vector2(xEnd, num5), (SharpDX.Direct2D1.Brush)(object)_bPoc, 1.5f * dX);
					DrawLadderLevelTag(x0, xEnd, dX, dY, cs, num4, "D-VPOC " + FQ(pocVol), _bPoc, ladderLeft, 1f);
				}
			}
		}

		private void DrawLadderLevelTag(float x0, float xEnd, float dX, float dY, ChartScale cs, double price, string txt, SolidColorBrush bgBrush, bool ladderLeft, float opacity)
		{
			//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
			if (cs != null && base.RenderTarget != null && bgBrush != null && !string.IsNullOrEmpty(txt) && !(price < cs.MinValue - RowPriceStep()) && !(price > cs.MaxValue + RowPriceStep()))
			{
				float num = cs.GetYByValue(price);
				float num2 = 16f * dY;
				float num3 = Math.Max(58f * dX, (float)txt.Length * 6.2f * dX);
				float num4;
				float num5;
				float num6;
				if (ladderLeft)
				{
					num4 = xEnd + 4f * dX;
					num5 = xEnd;
					num6 = num4;
				}
				else
				{
					num4 = x0 - num3 - 4f * dX;
					num5 = num4 + num3;
					num6 = x0;
				}
				if (num4 < 0f)
				{
					num4 = 0f;
				}
				((SharpDX.Direct2D1.Brush)bgBrush).Opacity = 0.55f * opacity;
				base.RenderTarget.DrawLine(new Vector2(num5, num), new Vector2(num6, num), (SharpDX.Direct2D1.Brush)(object)bgBrush, 1f * dX);
				((SharpDX.Direct2D1.Brush)bgBrush).Opacity = opacity;
				base.RenderTarget.FillRectangle(new RectangleF(num4, num - num2 * 0.5f, num3, num2), (SharpDX.Direct2D1.Brush)(object)bgBrush);
				((SharpDX.Direct2D1.Brush)_bBg).Opacity = 1f;
				T(txt, num4 + 4f * dX, num - 7f * dY, _fSmall, _bBg, num3 - 6f * dX, 14f * dY);
			}
		}

		private void DrawCurrentPriceLine(ChartControl cc, ChartScale cs, float cL, float cR, float H, float dX, float dY)
		{
			//IL_0175: Unknown result type (might be due to invalid IL or missing references)
			//IL_017d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0375: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0257: Unknown result type (might be due to invalid IL or missing references)
			//IL_0260: Unknown result type (might be due to invalid IL or missing references)
			//IL_02a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_02cf: Unknown result type (might be due to invalid IL or missing references)
			if (cs == null || base.RenderTarget == null)
			{
				return;
			}
			double num = _lastUiPrice;
			if (num <= 0.0)
			{
				try
				{
					if (base.CurrentBar >= 0)
					{
						num = base.Close[0];
					}
				}
				catch
				{
					num = 0.0;
				}
			}
			if (num <= 0.0)
			{
				return;
			}
			float num2 = cs.GetYByValue(num);
			if (!(num2 < -30f * dY) && !(num2 > H + 30f * dY))
			{
				long num3 = Environment.TickCount;
				long num4 = ((_lastPriceLineMs == 0) ? CurrentPriceLinePulseMs : Math.Max(0L, num3 - _lastPriceLineMs));
				float num5 = Math.Max(0f, Math.Min(1f, 1f - (float)num4 / (float)Math.Max(100, CurrentPriceLinePulseMs)));
				SolidColorBrush val = ((_lastUiDir > 0) ? _bPriceUp : ((_lastUiDir < 0) ? _bPriceDown : _bPriceNeutral));
				if (val == null)
				{
					val = _bPoc;
				}
				float num6 = Math.Max(0f, cL + 2f * dX);
				float num7 = Math.Max(num6 + 20f * dX, cR - 4f * dX);
				float num8 = Math.Max(1f, (float)CurrentPriceLineWidth * dX);
				((SharpDX.Direct2D1.Brush)val).Opacity = OpacityPct(CurrentPriceLineOpacity) * (0.55f + 0.3f * num5);
				base.RenderTarget.DrawLine(new Vector2(num6, num2), new Vector2(num7, num2), (SharpDX.Direct2D1.Brush)(object)val, num8);
				if (num5 > 0.02f)
				{
					float num9 = Math.Max(num8 + 1.5f * dX, num8 * (1f + 2.8f * num5));
					((SharpDX.Direct2D1.Brush)val).Opacity = Math.Min(0.55f, OpacityPct(CurrentPriceLineOpacity) * 0.42f * num5);
					base.RenderTarget.DrawLine(new Vector2(num6, num2), new Vector2(num7, num2), (SharpDX.Direct2D1.Brush)(object)val, num9);
					float num10 = Math.Max(num6 + 6f * dX, num7 - 14f * dX);
					float num11 = (4f + 7f * (1f - num5)) * dX;
					((SharpDX.Direct2D1.Brush)val).Opacity = Math.Min(0.7f, 0.45f * num5);
					base.RenderTarget.DrawEllipse(new Ellipse(new Vector2(num10, num2), num11, num11), (SharpDX.Direct2D1.Brush)(object)val, Math.Max(1f, 1.2f * dX));
					((SharpDX.Direct2D1.Brush)val).Opacity = Math.Min(0.9f, 0.7f * num5);
					base.RenderTarget.FillEllipse(new Ellipse(new Vector2(num10, num2), Math.Max(2f * dX, 3.2f * dX), Math.Max(2f * dX, 3.2f * dX)), (SharpDX.Direct2D1.Brush)(object)val);
				}
				string text = num.ToString("F" + _decs);
				float num12 = Math.Max(58f * dX, (float)text.Length * 7.2f * dX + 14f * dX);
				float num13 = Math.Max(17f * dY, ((float)FontSizeBold + 6f) * dY);
				float num14 = Math.Max(num6, num7 - num12);
				float num15 = num2 - num13 * 0.5f;
				((SharpDX.Direct2D1.Brush)val).Opacity = 0.92f;
				base.RenderTarget.FillRectangle(new RectangleF(num14, num15, num12, num13), (SharpDX.Direct2D1.Brush)(object)val);
				((SharpDX.Direct2D1.Brush)_bBg).Opacity = 1f;
				T(text, num14 + 6f * dX, num15 + 1.5f * dY, _fBold, _bBg, num12 - 10f * dX, num13 - 2f * dY);
			}
		}

		private void FillDirectionalBar(float x, float w, float barW, int direction, float y, float h, SolidColorBrush brush, bool signedDelta, bool positiveSide)
		{
			//IL_007c: Unknown result type (might be due to invalid IL or missing references)
			if (!(barW <= 0f) && !(w <= 0f) && brush != null)
			{
				barW = Math.Max(1f, Math.Min(barW, w));
				float num2;
				switch (Math.Max(1, Math.Min(3, direction)))
				{
				case 1:
					num2 = x;
					break;
				case 3:
					num2 = x + w - barW;
					break;
				default:
				{
					float num = x + w * 0.5f;
					num2 = (signedDelta ? (positiveSide ? num : (num - barW)) : (num - barW * 0.5f));
					break;
				}
				}
				base.RenderTarget.FillRectangle(new RectangleF(num2, y, barW, h), (SharpDX.Direct2D1.Brush)(object)brush);
			}
		}

		private void FillCenterAnchoredBar(float centerX, float sideCapacity, float barW, int direction, float y, float h, SolidColorBrush brush, bool positiveSide)
		{
			if (base.RenderTarget == null || brush == null || sideCapacity <= 0f || barW <= 0f || h <= 0f)
			{
				return;
			}
			barW = Math.Max(1f, Math.Min(barW, sideCapacity));
			int align = Math.Max(1, Math.Min(3, direction));
			float barX;
			if (align == 1)
			{
				barX = centerX - barW;
			}
			else if (align == 3)
			{
				barX = centerX;
			}
			else
			{
				barX = positiveSide ? centerX : centerX - barW;
			}
			base.RenderTarget.FillRectangle(new RectangleF(barX, y, barW, h), (SharpDX.Direct2D1.Brush)(object)brush);
		}

		private Dictionary<int, Dictionary<int, ILT_DEV_FOTPRINT_Level>> SnapshotVisibleFootprints(int fromBar, int toBar)
		{
			return SnapshotFootprintsRange(fromBar, toBar);
		}

		private Dictionary<int, Dictionary<int, ILT_DEV_FOTPRINT_Level>> SnapshotFootprintsRange(int fromBar, int toBar)
		{
			Dictionary<int, Dictionary<int, ILT_DEV_FOTPRINT_Level>> dictionary = new Dictionary<int, Dictionary<int, ILT_DEV_FOTPRINT_Level>>();
			lock (_lk)
			{
				if (_barFp == null || _barFp.Count == 0)
				{
					return dictionary;
				}
				int num = Math.Max(0, fromBar);
				int num2 = Math.Max(num, toBar);
				for (int i = num; i <= num2; i++)
				{
					Dictionary<int, ILT_DEV_FOTPRINT_Level> value;
					if (_barFp.TryGetValue(i, out value) && value != null && value.Count > 0)
					{
						dictionary[i] = (UseNativeVolumetricBidAsk ? value : CloneFootprintMap(value));
					}
				}
				return dictionary;
			}
		}

		private ILT_DEV_FOTPRINT_Level CloneLevel(ILT_DEV_FOTPRINT_Level src)
		{
			if (src == null)
			{
				return new ILT_DEV_FOTPRINT_Level();
			}
			ILT_DEV_FOTPRINT_Level iltDev_FP_Level = new ILT_DEV_FOTPRINT_Level();
			iltDev_FP_Level.BidVol = src.BidVol;
			iltDev_FP_Level.AskVol = src.AskVol;
			iltDev_FP_Level.SpreadVol = src.SpreadVol;
			return iltDev_FP_Level;
		}

		private Dictionary<int, ILT_DEV_FOTPRINT_Level> CloneFootprintMap(Dictionary<int, ILT_DEV_FOTPRINT_Level> src)
		{
			Dictionary<int, ILT_DEV_FOTPRINT_Level> dictionary = new Dictionary<int, ILT_DEV_FOTPRINT_Level>((src != null) ? src.Count : 0);
			if (src == null)
			{
				return dictionary;
			}
			foreach (KeyValuePair<int, ILT_DEV_FOTPRINT_Level> item in src)
			{
				dictionary[item.Key] = CloneLevel(item.Value);
			}
			return dictionary;
		}

		private bool FootprintMapsEqual(Dictionary<int, ILT_DEV_FOTPRINT_Level> a, Dictionary<int, ILT_DEV_FOTPRINT_Level> b)
		{
			if (object.ReferenceEquals(a, b))
			{
				return true;
			}
			if (a == null || b == null)
			{
				return false;
			}
			if (a.Count != b.Count)
			{
				return false;
			}
			foreach (KeyValuePair<int, ILT_DEV_FOTPRINT_Level> item in a)
			{
				ILT_DEV_FOTPRINT_Level value;
				if (!b.TryGetValue(item.Key, out value) || value == null || item.Value == null)
				{
					return false;
				}
				if (item.Value.BidVol != value.BidVol || item.Value.AskVol != value.AskVol)
				{
					return false;
				}
			}
			return true;
		}

		private void GetCandleFootprintStats(Dictionary<int, ILT_DEV_FOTPRINT_Level> map, out long maxBarMetric, out long totalVol, out int pocKey)
		{
			maxBarMetric = 1L;
			totalVol = 1L;
			pocKey = int.MinValue;
			long num = -1L;
			long num2 = 0L;
			if (map == null || map.Count == 0)
			{
				return;
			}
			foreach (KeyValuePair<int, ILT_DEV_FOTPRINT_Level> item in map)
			{
				ILT_DEV_FOTPRINT_Level value = item.Value;
				if (value != null)
				{
					long num3 = Math.Max(0L, value.Total);
					num2 += num3;
					if (num3 > num)
					{
						num = num3;
						pocKey = item.Key;
					}
					long candleFootprintBarMetricValue = GetCandleFootprintBarMetricValue(value);
					if (candleFootprintBarMetricValue > maxBarMetric)
					{
						maxBarMetric = candleFootprintBarMetricValue;
					}
				}
			}
			totalVol = Math.Max(1L, num2);
			maxBarMetric = Math.Max(1L, maxBarMetric);
		}

		private float GetVisibleBarSpacing(ChartControl cc, int barIndex, float dX)
		{
			if (cc == null || base.ChartBars == null)
			{
				return Math.Max(2f * dX, (float)CandleFootprintWidth * dX);
			}
			float num = cc.GetXByBarIndex(base.ChartBars, barIndex);
			float num2 = 0f;
			try
			{
				if (barIndex < base.ChartBars.ToIndex)
				{
					num2 = Math.Abs((float)cc.GetXByBarIndex(base.ChartBars, barIndex + 1) - num);
				}
				else if (barIndex > base.ChartBars.FromIndex)
				{
					num2 = Math.Abs(num - (float)cc.GetXByBarIndex(base.ChartBars, barIndex - 1));
				}
			}
			catch
			{
				num2 = 0f;
			}
			if (num2 <= 0f)
			{
				try
				{
					num2 = Math.Max(num2, (float)cc.BarWidth * dX);
				}
				catch
				{
				}
			}
			if (!(num2 > 0f))
			{
				return Math.Max(2f * dX, (float)CandleFootprintWidth * dX);
			}
			return num2;
		}

		private float GetBetaCandleCellWidth(float spacing, float dX)
		{
			float requestedWidth = Math.Max(10f * dX, (float)CandleFootprintWidth * dX);
			if (!AutoResizeCandleFootprint)
			{
				return requestedWidth;
			}
			// In automatic mode the candle spacing is authoritative. This lets the
			// footprint, POC and summary grow/shrink together with the native candle.
			return Math.Max(8f * dX, spacing * 0.92f);
		}

		private float GetCandleFootprintRenderOffset(ChartControl cc, int barIndex, float dX)
		{
			float num = (float)CandleFootprintOffset * dX;
			if (!AutoResizeCandleFootprint)
			{
				return num;
			}
			float visibleBarSpacing = GetVisibleBarSpacing(cc, barIndex, dX);
			float num2 = Math.Max(0f, visibleBarSpacing * 0.35f);
			if (num > 0f)
			{
				return Math.Min(num, num2);
			}
			if (num < 0f)
			{
				return Math.Max(num, 0f - num2);
			}
			return 0f;
		}

		private float GetCandleFootprintRenderWidth(ChartControl cc, int barIndex, float dX, float renderOffset)
		{
			float num = Math.Max(1f, (float)CandleFootprintWidth * dX);
			if (!AutoResizeCandleFootprint)
			{
				return num;
			}
			float visibleBarSpacing = GetVisibleBarSpacing(cc, barIndex, dX);
			float num2 = Math.Max(0.2f, Math.Min(2.5f, (float)CandleFootprintWidth / 36f));
			float num3 = Math.Max(1f * dX, visibleBarSpacing - Math.Abs(renderOffset) - 1f * dX);
			float val = num3 * 0.72f * num2;
			float val2 = Math.Min(Math.Max(1f * dX, 2f * dX), num3);
			float val3 = Math.Max(num, Math.Min(220f * dX, num * 2.75f));
			float val4 = Math.Max(val2, Math.Min(num3 * 0.98f, val3));
			return Math.Max(val2, Math.Min(val, val4));
		}

		private long GetCandleFootprintBarMetricValue(ILT_DEV_FOTPRINT_Level lvl)
		{
			if (lvl == null)
			{
				return 0L;
			}
			if (CandleFootprintBarMetric == 2)
			{
				return Math.Abs(lvl.Delta);
			}
			return Math.Max(0L, lvl.Total);
		}

		private long GetCandleFootprintMaxBarMetric(Dictionary<int, ILT_DEV_FOTPRINT_Level> map)
		{
			long num = 1L;
			if (map == null)
			{
				return num;
			}
			foreach (ILT_DEV_FOTPRINT_Level value in map.Values)
			{
				long candleFootprintBarMetricValue = GetCandleFootprintBarMetricValue(value);
				if (candleFootprintBarMetricValue > num)
				{
					num = candleFootprintBarMetricValue;
				}
			}
			return Math.Max(1L, num);
		}

		private SolidColorBrush GetCandleFootprintBarBrush(ILT_DEV_FOTPRINT_Level lvl, int key, int pocKey, bool buyImb, bool sellImb)
		{
			if (buyImb)
			{
				return _bImbBuy;
			}
			if (sellImb)
			{
				return _bImbSell;
			}
			if (key == pocKey)
			{
				return _bPoc;
			}
			if (CandleFootprintBarMetric == 2 && lvl != null)
			{
				if (lvl.Delta > 0)
				{
					return _bDeltaPos;
				}
				if (lvl.Delta < 0)
				{
					return _bDeltaNeg;
				}
			}
			return _bVol;
		}

		private float GetCandleFootprintBarOpacity(int key, int pocKey, bool buyImb, bool sellImb)
		{
			if (buyImb || sellImb)
			{
				return Math.Max(0.86f, OpacityPct(FootprintOpacity));
			}
			if (key == pocKey)
			{
				return 0.86f;
			}
			if (CandleFootprintBarMetric == 2)
			{
				return OpacityPct(DeltaOpacity);
			}
			return OpacityPct(FootprintOpacity);
		}

		private void DrawCandleImbalanceRowMarker(float x0, float y, float yR, float maxW, float rowH, bool buyImb, bool sellImb, float dX)
		{
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			//IL_0099: Unknown result type (might be due to invalid IL or missing references)
			//IL_00da: Unknown result type (might be due to invalid IL or missing references)
			SolidColorBrush val = (buyImb ? _bImbBuy : _bImbSell);
			if (val != null)
			{
				float num = Math.Max(1f, rowH);
				float num2 = Math.Max(1.2f * dX, Math.Min(4f * dX, maxW * 0.18f));
				((SharpDX.Direct2D1.Brush)val).Opacity = 0.16f;
				base.RenderTarget.FillRectangle(new RectangleF(x0, yR, Math.Max(1f, maxW), num), (SharpDX.Direct2D1.Brush)(object)val);
				((SharpDX.Direct2D1.Brush)val).Opacity = 0.88f;
				base.RenderTarget.DrawLine(new Vector2(x0, y), new Vector2(x0 + Math.Max(1f, maxW), y), (SharpDX.Direct2D1.Brush)(object)val, Math.Max(0.85f, 1.05f * dX));
				((SharpDX.Direct2D1.Brush)val).Opacity = 0.78f;
				base.RenderTarget.FillRectangle(new RectangleF(x0 + Math.Max(1f, maxW) - num2, yR, num2, num), (SharpDX.Direct2D1.Brush)(object)val);
			}
		}

		private bool IsPriceInsideVisibleScale(ChartScale cs, double price, double step)
		{
			if (cs == null)
			{
				return false;
			}
			if (price >= cs.MinValue - step)
			{
				return price <= cs.MaxValue + step;
			}
			return false;
		}

		private float GetPersistentImbalanceY(ChartScale cs, double price, float rowH, float dY, out bool clippedTop, out bool clippedBottom)
		{
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			clippedTop = false;
			clippedBottom = false;
			float num = cs.GetYByValue(price);
			float num2 = ((base.RenderTarget != null) ? base.RenderTarget.Size.Height : Math.Max(1f, _lastRenderH));
			float num3 = Math.Max(6f * dY, rowH * 0.65f);
			if (num < num3)
			{
				clippedTop = true;
				return num3;
			}
			if (num > num2 - num3)
			{
				clippedBottom = true;
				return Math.Max(num3, num2 - num3);
			}
			return num;
		}

		private void DrawPersistentCandleImbalanceMarker(ChartControl cc, ChartScale cs, float x0, float maxW, float rowH, double price, bool buyImb, bool sellImb, float cL, float cR, float dX, float dY, bool allowPriceTag)
		{
			//IL_0162: Unknown result type (might be due to invalid IL or missing references)
			bool clippedTop;
			bool clippedBottom;
			float persistentImbalanceY = GetPersistentImbalanceY(cs, price, rowH, dY, out clippedTop, out clippedBottom);
			float yR = persistentImbalanceY - rowH * 0.5f;
			DrawCandleImbalanceRowMarker(x0, persistentImbalanceY, yR, maxW, rowH, buyImb, sellImb, dX);
			if ((!clippedTop && !clippedBottom) || !allowPriceTag)
			{
				return;
			}
			SolidColorBrush val = (buyImb ? _bImbBuy : _bImbSell);
			if (val != null && _fSmall != null)
			{
				string text = (clippedTop ? "▲ " : "▼ ") + price.ToString("F" + _decs);
				float num = Math.Max(10f * dY, ((float)FontSizeSmall + 3f) * dY);
				float num2 = Math.Max(42f * dX, Math.Min(92f * dX, (float)text.Length * 5.8f * dX));
				float num3 = x0 + Math.Max(1f, maxW) + 2f * dX;
				if (num3 + num2 > cR)
				{
					num3 = x0 - num2 - 2f * dX;
				}
				if (num3 < cL)
				{
					num3 = cL + 1f * dX;
				}
				((SharpDX.Direct2D1.Brush)val).Opacity = 0.9f;
				((SharpDX.Direct2D1.Brush)_bPanel).Opacity = 0.72f;
				base.RenderTarget.FillRectangle(new RectangleF(num3 - 1f * dX, persistentImbalanceY - num * 0.5f, num2 + 2f * dX, num), (SharpDX.Direct2D1.Brush)(object)_bPanel);
				T(text, num3, persistentImbalanceY - num * 0.48f, _fSmall, val, num2, num);
			}
		}

		private long CandleImbalanceId(int barIndex, int priceKey, bool buyImb, bool sellImb)
		{
			long num = (buyImb ? 1 : (sellImb ? 2 : 0));
			return ((long)barIndex << 32) ^ (uint)priceKey ^ (num << 60);
		}

		private bool DrawNewImbalancePulseIfActive(int barIndex, int priceKey, bool buyImb, bool sellImb, float x0, float y, float maxW, float rowH, float dX, float flashRightX, long nowMs, bool allowNewPulse)
		{
			//IL_0194: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_01fd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0206: Unknown result type (might be due to invalid IL or missing references)
			//IL_0267: Unknown result type (might be due to invalid IL or missing references)
			//IL_0270: Unknown result type (might be due to invalid IL or missing references)
			if (!ShowNewImbalanceAnimation || (!buyImb && !sellImb))
			{
				return false;
			}
			long num = CandleImbalanceId(barIndex, priceKey, buyImb, sellImb);
			if (!_knownCandleImbalanceIds.Contains(num))
			{
				_knownCandleImbalanceIds.Add(num);
				if (allowNewPulse)
				{
					_imbalancePulseStartMs[num] = nowMs;
				}
			}
			long value;
			if (!_imbalancePulseStartMs.TryGetValue(num, out value))
			{
				return false;
			}
			int num2 = Math.Max(200, NewImbalanceAnimationMs);
			long num3 = Math.Max(0L, nowMs - value);
			if (num3 > num2)
			{
				return false;
			}
			float num4 = 1f - Math.Min(1f, (float)num3 / (float)num2);
			SolidColorBrush val = (buyImb ? _bImbBuy : _bImbSell);
			if (val == null)
			{
				return false;
			}
			float num5 = OpacityPct(NewImbalanceAnimationOpacity);
			float num6 = Math.Max(x0 + Math.Max(1f, maxW), flashRightX);
			float num7 = Math.Max(1f, NewImbalanceAnimationLineWidth) * dX;
			float num8 = Math.Max(num7, num7 + 7.5f * num4 * dX);
			float num9 = Math.Max(1.5f * dX, num7 * (0.7f + 0.3f * num4));
			float num10 = Math.Max(Math.Min(rowH, 10f * dX), Math.Min(rowH, num7 * 1.45f));
			((SharpDX.Direct2D1.Brush)val).Opacity = Math.Min(0.42f, num5 * 0.3f * num4);
			base.RenderTarget.FillRectangle(new RectangleF(x0, y - num10 * 0.5f, Math.Max(1f, num6 - x0), num10), (SharpDX.Direct2D1.Brush)(object)val);
			((SharpDX.Direct2D1.Brush)val).Opacity = Math.Min(0.9f, num5 * 0.62f * num4);
			base.RenderTarget.DrawLine(new Vector2(x0, y), new Vector2(num6, y), (SharpDX.Direct2D1.Brush)(object)val, num8);
			((SharpDX.Direct2D1.Brush)val).Opacity = Math.Min(1f, num5 * num4);
			base.RenderTarget.DrawLine(new Vector2(x0, y), new Vector2(num6, y), (SharpDX.Direct2D1.Brush)(object)val, num9);
			float num11 = Math.Max(2.5f * dX, Math.Min(rowH * 0.55f, num7 * (0.9f + 0.4f * num4)));
			((SharpDX.Direct2D1.Brush)val).Opacity = Math.Min(1f, num5 * 0.92f * num4);
			base.RenderTarget.FillEllipse(new Ellipse(new Vector2(x0, y), num11, num11), (SharpDX.Direct2D1.Brush)(object)val);
			return true;
		}

		private void CleanupImbalancePulseCache(long nowMs)
		{
			if (_imbalancePulseStartMs.Count == 0)
			{
				return;
			}
			int num = Math.Max(200, NewImbalanceAnimationMs) + 250;
			List<long> list = new List<long>();
			foreach (KeyValuePair<long, long> imbalancePulseStartM in _imbalancePulseStartMs)
			{
				if (nowMs - imbalancePulseStartM.Value > num)
				{
					list.Add(imbalancePulseStartM.Key);
				}
			}
			foreach (long item in list)
			{
				_imbalancePulseStartMs.Remove(item);
			}
			if (_knownCandleImbalanceIds.Count > 12000)
			{
				_knownCandleImbalanceIds.Clear();
				_imbalancePulsePrimed = false;
			}
		}

		private bool UseBlockCandleRenderMode()
		{
			return false;
		}

		private void DrawBlockCandles(ChartControl cc, ChartScale cs, float cL, float cR, float dX, float dY)
		{
			//IL_0251: Unknown result type (might be due to invalid IL or missing references)
			//IL_02aa: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e8: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f4: Unknown result type (might be due to invalid IL or missing references)
			if (cc == null || cs == null || base.ChartBars == null || base.RenderTarget == null)
			{
				return;
			}
			int fromIndex = base.ChartBars.FromIndex;
			int toIndex = base.ChartBars.ToIndex;
			if (toIndex < fromIndex)
			{
				return;
			}
			Dictionary<int, Dictionary<int, ILT_DEV_FOTPRINT_Level>> dictionary = SnapshotVisibleFootprints(fromIndex, toIndex);
			if (dictionary == null || dictionary.Count == 0)
			{
				return;
			}
			double num = RowPriceStep();
			double num2 = (cs.MaxValue + cs.MinValue) * 0.5;
			float num3 = Math.Max(1.5f * dY, Math.Abs((float)cs.GetYByValue(num2) - (float)cs.GetYByValue(num2 + num)) * 0.72f);
			float num4 = Math.Max(0f, (float)BlockCandleRowGapPixels * dY);
			float num5 = Math.Max(1f, num3 - num4);
			for (int i = fromIndex; i <= toIndex; i++)
			{
				Dictionary<int, ILT_DEV_FOTPRINT_Level> value;
				if (!dictionary.TryGetValue(i, out value) || value == null || value.Count == 0)
				{
					continue;
				}
				float num6 = cc.GetXByBarIndex(base.ChartBars, i);
				if (num6 < cL - 80f * dX || num6 > cR + 80f * dX)
				{
					continue;
				}
				float visibleBarSpacing = GetVisibleBarSpacing(cc, i, dX);
				float blockCandleRenderWidth = GetBlockCandleRenderWidth(visibleBarSpacing, dX);
				float num7 = num6 - blockCandleRenderWidth * 0.5f;
				int key = value.OrderByDescending((KeyValuePair<int, ILT_DEV_FOTPRINT_Level> kv) => kv.Value.Total).First().Key;
				HashSet<int> blockCandleBodyKeys = GetBlockCandleBodyKeys(i, value);
				if (blockCandleBodyKeys == null || blockCandleBodyKeys.Count == 0)
				{
					continue;
				}
				SolidColorBrush val = GetBlockCandleBrush(i, value);
				if (val == null)
				{
					val = _bBlockNeutral;
				}
				((SharpDX.Direct2D1.Brush)val).Opacity = OpacityPct(BlockCandleOpacity);
				foreach (int item in blockCandleBodyKeys.OrderBy((int k) => k))
				{
					double num8 = KeyToPrice(item);
					if (!(num8 < cs.MinValue - num) && !(num8 > cs.MaxValue + num))
					{
						float num9 = cs.GetYByValue(num8);
						float num10 = num9 - num3 * 0.5f + num4 * 0.5f;
						base.RenderTarget.FillRectangle(new RectangleF(num7, num10, blockCandleRenderWidth, num5), (SharpDX.Direct2D1.Brush)(object)val);
						if (ShowBlockCandlePoc && item == key)
						{
							((SharpDX.Direct2D1.Brush)_bPoc).Opacity = Math.Min(0.95f, Math.Max(0.35f, OpacityPct(BlockCandleOpacity) + 0.08f));
							base.RenderTarget.FillRectangle(new RectangleF(num7, num10, blockCandleRenderWidth, num5), (SharpDX.Direct2D1.Brush)(object)_bPoc);
						}
						if (ShowSessionRowGrid && num3 >= 3f * dY)
						{
							((SharpDX.Direct2D1.Brush)_bGrid).Opacity = 0.22f;
							base.RenderTarget.DrawLine(new Vector2(num7, num10), new Vector2(num7 + blockCandleRenderWidth, num10), (SharpDX.Direct2D1.Brush)(object)_bGrid, Math.Max(0.35f, 0.45f * dX));
						}
					}
				}
			}
		}

		private HashSet<int> GetBlockCandleBodyKeys(int barIndex, Dictionary<int, ILT_DEV_FOTPRINT_Level> map)
		{
			HashSet<int> hashSet = new HashSet<int>();
			if (map == null || map.Count == 0)
			{
				return hashSet;
			}
			int num = Math.Max(1, Math.Min(2, BlockCandleDrawMode));
			if (num == 2)
			{
				foreach (KeyValuePair<int, ILT_DEV_FOTPRINT_Level> item in map)
				{
					if (item.Value != null && item.Value.Total >= MinVolumeToDisplay)
					{
						hashSet.Add(item.Key);
					}
				}
				return hashSet;
			}
			double valueAt;
			double valueAt2;
			try
			{
				valueAt = base.Opens[0].GetValueAt(barIndex);
				valueAt2 = base.Closes[0].GetValueAt(barIndex);
			}
			catch
			{
				return hashSet;
			}
			int num2 = PriceToKey(Math.Min(valueAt, valueAt2));
			int num3 = PriceToKey(Math.Max(valueAt, valueAt2));
			int num4 = EffectiveTicksPerRow();
			if (num3 < num2)
			{
				int num5 = num2;
				num2 = num3;
				num3 = num5;
			}
			if (num2 == num3)
			{
				hashSet.Add(num2);
				return hashSet;
			}
			for (int i = num2; i <= num3; i += num4)
			{
				hashSet.Add(i);
			}
			return hashSet;
		}

		private SolidColorBrush GetBlockCandleBrush(int barIndex, Dictionary<int, ILT_DEV_FOTPRINT_Level> map)
		{
			int num = Math.Max(1, Math.Min(2, BlockCandleColorMode));
			if (num == 2)
			{
				long num2 = 0L;
				if (map != null)
				{
					foreach (ILT_DEV_FOTPRINT_Level value in map.Values)
					{
						if (value != null)
						{
							num2 += value.Delta;
						}
					}
				}
				if (num2 > 0)
				{
					return _bBlockBull;
				}
				if (num2 < 0)
				{
					return _bBlockBear;
				}
				return _bBlockNeutral;
			}
			return GetDirectionBlockBrush(barIndex);
		}

		private SolidColorBrush GetDirectionBlockBrush(int barIndex)
		{
			try
			{
				double valueAt = base.Opens[0].GetValueAt(barIndex);
				double valueAt2 = base.Closes[0].GetValueAt(barIndex);
				if (valueAt2 > valueAt)
				{
					return _bBlockBull;
				}
				if (valueAt2 < valueAt)
				{
					return _bBlockBear;
				}
			}
			catch
			{
			}
			return _bBlockNeutral;
		}

		private bool TryGetExternalLionSignal(int barIndex, out int code)
		{
			code = 0;
			return false;
		}

		private bool TryGetExternalLionPayload(int barIndex, out LionSignalPayload payload)
		{
			payload = null;
			return false;
		}

		private void CacheLocalLionSignal(int barIndex, long timeKey, LionSignalPayload payload)
		{
		}

		private SolidColorBrush GetExternalLionBlockBrush(LionSignalPayload payload, int barIndex, Dictionary<int, ILT_DEV_FOTPRINT_Level> map)
		{
			if (payload != null && payload.ColorArgb != 0)
			{
				SolidColorBrush externalLionArgbBrush = GetExternalLionArgbBrush(payload.ColorArgb);
				if (externalLionArgbBrush != null)
				{
					return externalLionArgbBrush;
				}
			}
			int num = ((payload != null) ? payload.Code : 0);
			switch (num)
			{
			case 1:
				return _bLionAcelBuy;
			case -1:
				return _bLionAcelSell;
			case 2:
				return _bLionExcessBuy;
			case -2:
				return _bLionExcessSell;
			case 3:
				return _bLionRotationBuy;
			case -3:
				return _bLionRotationSell;
			case 4:
				return _bLionAbsorptionBuy;
			case -4:
				return _bLionAbsorptionSell;
			default:
				if (num > 0)
				{
					return _bBlockBull;
				}
				if (num < 0)
				{
					return _bBlockBear;
				}
				return GetDirectionBlockBrush(barIndex);
			}
		}

		private SolidColorBrush GetExternalLionArgbBrush(int argb)
		{
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Expected O, but got Unknown
			if (base.RenderTarget == null || argb == 0)
			{
				return null;
			}
			SolidColorBrush value;
			if (_lionExternalBrushCache.TryGetValue(argb, out value) && value != null)
			{
				return value;
			}
			byte b = (byte)((argb >> 24) & 0xFF);
			byte b2 = (byte)((argb >> 16) & 0xFF);
			byte b3 = (byte)((argb >> 8) & 0xFF);
			byte b4 = (byte)(argb & 0xFF);
			value = new SolidColorBrush(base.RenderTarget, new Color4((float)(int)b2 / 255f, (float)(int)b3 / 255f, (float)(int)b4 / 255f, (float)(int)b / 255f));
			_lionExternalBrushCache[argb] = value;
			return value;
		}

		private void DrawLionSignalMarkers(ChartControl cc, ChartScale cs, float cL, float cR, float H, float dX, float dY)
		{
		}

		private void DrawLionSignalArrow(float x, float y, bool drawAbove, SolidColorBrush br, float size, float dX)
		{
			//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0107: Unknown result type (might be due to invalid IL or missing references)
			//IL_0119: Unknown result type (might be due to invalid IL or missing references)
			//IL_0137: Unknown result type (might be due to invalid IL or missing references)
			//IL_0149: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_007e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0090: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
			if (br != null && base.RenderTarget != null)
			{
				((SharpDX.Direct2D1.Brush)br).Opacity = OpacityPct(LionSignalArrowOpacity);
				float num = Math.Max(1.2f * dX, 1.5f * dX);
				float num2 = size * 0.55f;
				if (drawAbove)
				{
					base.RenderTarget.DrawLine(new Vector2(x, y - size), new Vector2(x, y + size * 0.15f), (SharpDX.Direct2D1.Brush)(object)br, num);
					base.RenderTarget.DrawLine(new Vector2(x, y + size * 0.15f), new Vector2(x - num2, y - size * 0.35f), (SharpDX.Direct2D1.Brush)(object)br, num);
					base.RenderTarget.DrawLine(new Vector2(x, y + size * 0.15f), new Vector2(x + num2, y - size * 0.35f), (SharpDX.Direct2D1.Brush)(object)br, num);
				}
				else
				{
					base.RenderTarget.DrawLine(new Vector2(x, y + size), new Vector2(x, y - size * 0.15f), (SharpDX.Direct2D1.Brush)(object)br, num);
					base.RenderTarget.DrawLine(new Vector2(x, y - size * 0.15f), new Vector2(x - num2, y + size * 0.35f), (SharpDX.Direct2D1.Brush)(object)br, num);
					base.RenderTarget.DrawLine(new Vector2(x, y - size * 0.15f), new Vector2(x + num2, y + size * 0.35f), (SharpDX.Direct2D1.Brush)(object)br, num);
				}
			}
		}

		private bool IsLionSignalDrawAbove(int code)
		{
			if (code != -1 && code != 2 && code != -3)
			{
				return code == -4;
			}
			return true;
		}

		private string GetLionSignalLabel(int code)
		{
			switch (code)
			{
			case 1:
				return "ACB";
			case -1:
				return "ACS";
			case 2:
				return "EXB";
			case -2:
				return "EXS";
			case 3:
				return "RTB";
			case -3:
				return "RTS";
			case 4:
				return "ABB";
			case -4:
				return "ABS";
			default:
				return string.Empty;
			}
		}

		private SolidColorBrush GetLionSignalDxBrush(int code)
		{
			switch (code)
			{
			case 1:
				return _bLionAcelBuy;
			case -1:
				return _bLionAcelSell;
			case 2:
				return _bLionExcessBuy;
			case -2:
				return _bLionExcessSell;
			case 3:
				return _bLionRotationBuy;
			case -3:
				return _bLionRotationSell;
			case 4:
				return _bLionAbsorptionBuy;
			case -4:
				return _bLionAbsorptionSell;
			default:
				return _bNeutral;
			}
		}

		private float GetBlockCandleRenderWidth(float spacing, float dX)
		{
			float val = Math.Max(1f, (float)BlockCandleWidth * dX);
			float val2 = Math.Max(1f, (float)BlockCandleMinWidth * dX);
			float val3 = Math.Max(val2, (float)BlockCandleMaxWidth * dX);
			if (!AutoResizeCandleFootprint)
			{
				return Math.Max(val2, Math.Min(val, val3));
			}
			float val4 = Math.Max(val2, spacing * 0.38f);
			return Math.Max(val2, Math.Min(Math.Min(val, val4), val3));
		}

		private List<CandleImbalanceMarker> BuildVisibleImbalanceMarkerSnapshot(int fromBar, int toBar)
		{
			List<CandleImbalanceMarker> list = new List<CandleImbalanceMarker>();
			if (!ShowCandleImbalances)
			{
				return list;
			}
			double ratio = (double)Math.Max(100, CandleImbalancePct) / 100.0;
			long minImb = Math.Max(1, CandleMinImbalance);
			int stackRows = Math.Max(1, CandleImbalanceStackRows);
			lock (_lk)
			{
				if (_barFp == null || _barFp.Count == 0)
				{
					return list;
				}
				long footprintDataVersion = _footprintDataVersion;
				int num = Math.Max(0, fromBar);
				int num2 = Math.Max(num, toBar);
				for (int num3 = num2; num3 >= num; num3--)
				{
					Dictionary<int, ILT_DEV_FOTPRINT_Level> value;
					if (_barFp.TryGetValue(num3, out value) && value != null && value.Count != 0)
					{
						HashSet<int> buyKeys;
						HashSet<int> sellKeys;
						GetCandleImbalanceKeysCached(num3, value, ratio, minImb, stackRows, footprintDataVersion, out buyKeys, out sellKeys);
						if ((buyKeys != null && buyKeys.Count != 0) || (sellKeys != null && sellKeys.Count != 0))
						{
							if (buyKeys != null)
							{
								foreach (int item in buyKeys)
								{
									ILT_DEV_FOTPRINT_Level value2;
									if (value.TryGetValue(item, out value2) && value2 != null && value2.Total >= MinVolumeToDisplay)
									{
										list.Add(new CandleImbalanceMarker
										{
											BarIndex = num3,
											PriceKey = item,
											Buy = true,
											Sell = (sellKeys != null && sellKeys.Contains(item))
										});
									}
								}
							}
							if (sellKeys != null)
							{
								foreach (int item2 in sellKeys)
								{
									ILT_DEV_FOTPRINT_Level value3;
									if ((buyKeys == null || !buyKeys.Contains(item2)) && value.TryGetValue(item2, out value3) && value3 != null && value3.Total >= MinVolumeToDisplay)
									{
										list.Add(new CandleImbalanceMarker
										{
											BarIndex = num3,
											PriceKey = item2,
											Buy = false,
											Sell = true
										});
									}
								}
							}
						}
					}
				}
				return list;
			}
		}

		private void DrawCandleImbalancesOnly(ChartControl cc, ChartScale cs, float cL, float cR, float dX, float dY)
		{
			if (cc == null || cs == null || base.ChartBars == null || base.RenderTarget == null)
			{
				return;
			}
			int fromIndex = base.ChartBars.FromIndex;
			int toIndex = base.ChartBars.ToIndex;
			if (toIndex < fromIndex)
			{
				return;
			}
			List<CandleImbalanceMarker> list = BuildVisibleImbalanceMarkerSnapshot(fromIndex, toIndex);
			if (list == null || list.Count == 0)
			{
				return;
			}
			double num = RowPriceStep();
			double num2 = (cs.MaxValue + cs.MinValue) * 0.5;
			float num3 = Math.Abs((float)cs.GetYByValue(num2) - (float)cs.GetYByValue(num2 + num));
			float rowH = Math.Max(1.35f * dY, num3 * 0.72f);
			foreach (CandleImbalanceMarker item in list)
			{
				double price = KeyToPrice(item.PriceKey);
				float num4;
				try
				{
					num4 = cc.GetXByBarIndex(base.ChartBars, item.BarIndex);
				}
				catch
				{
					continue;
				}
				if (!(num4 < cL - 4f * dX) && !(num4 > cR + 4f * dX))
				{
					float visibleBarSpacing = GetVisibleBarSpacing(cc, item.BarIndex, dX);
					float num5 = Math.Max(2f * dX, Math.Min(8f * dX, Math.Max(1f * dX, visibleBarSpacing * 0.85f)));
					float x = num4 - num5 * 0.5f;
					bool allowPriceTag = visibleBarSpacing >= 12f * dX;
					DrawPersistentCandleImbalanceMarker(cc, cs, x, num5, rowH, price, item.Buy, item.Sell, cL, cR, dX, dY, allowPriceTag);
				}
			}
		}

		private void DrawCandleFootprints(ChartControl cc, ChartScale cs, float cL, float cR, float dX, float dY)
		{
			//IL_0102: Unknown result type (might be due to invalid IL or missing references)
			//IL_0108: Invalid comparison between Unknown and I4
			//IL_05c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_05c8: Invalid comparison between Unknown and I4
			//IL_05de: Unknown result type (might be due to invalid IL or missing references)
			//IL_05e4: Invalid comparison between Unknown and I4
			//IL_0527: Unknown result type (might be due to invalid IL or missing references)
			//IL_03e3: Unknown result type (might be due to invalid IL or missing references)
			int fromIndex = base.ChartBars.FromIndex;
			int toIndex = base.ChartBars.ToIndex;
			if (toIndex < fromIndex || ShouldSkipDenseCandleRender(cc, cL, cR, dX, false))
			{
				return;
			}
			Dictionary<int, Dictionary<int, ILT_DEV_FOTPRINT_Level>> dictionary = SnapshotVisibleFootprints(fromIndex, toIndex);
			if (dictionary == null || dictionary.Count == 0)
			{
				return;
			}
			int num = 0;
			foreach (Dictionary<int, ILT_DEV_FOTPRINT_Level> value3 in dictionary.Values)
			{
				if (value3 != null)
				{
					num += value3.Count;
					if (num > 8500)
					{
						DrawCandleImbalancesOnly(cc, cs, cL, cR, dX, dY);
						return;
					}
				}
			}
			_renderCandleBuyImbalanceCache.Clear();
			_renderCandleSellImbalanceCache.Clear();
			if (_imbalancePulseSessionDate != _sessionDate)
			{
				_imbalancePulseStartMs.Clear();
				_knownCandleImbalanceIds.Clear();
				_imbalancePulsePrimed = false;
				_imbalancePulseSessionDate = _sessionDate;
			}
			long nowMs = Environment.TickCount;
			bool flag = ShowNewImbalanceAnimation && (int)base.State == 7 && _imbalancePulsePrimed;
			bool flag2 = false;
			double num2 = RowPriceStep();
			double num3 = (cs.MaxValue + cs.MinValue) * 0.5;
			float num4 = Math.Abs((float)cs.GetYByValue(num3) - (float)cs.GetYByValue(num3 + num2));
			float num5 = Math.Max(1.5f * dY, num4 * 0.72f);
			double ratio = (double)Math.Max(100, CandleImbalancePct) / 100.0;
			long minImb = Math.Max(1, CandleMinImbalance);
			long footprintDataVersion;
			lock (_lk)
			{
				footprintDataVersion = _footprintDataVersion;
			}
			TrimBarKeyCaches(fromIndex, toIndex);
			for (int i = fromIndex; i <= toIndex; i++)
			{
				Dictionary<int, ILT_DEV_FOTPRINT_Level> value;
				if (!dictionary.TryGetValue(i, out value) || value.Count == 0)
				{
					continue;
				}
				float num6 = cc.GetXByBarIndex(base.ChartBars, i);
				if (num6 > cR || num6 < cL)
				{
					continue;
				}
				long maxBarMetric;
				long totalVol;
				int pocKey;
				GetCandleFootprintStats(value, out maxBarMetric, out totalVol, out pocKey);
				if (pocKey == int.MinValue || !value.ContainsKey(pocKey))
				{
					continue;
				}
				SolidColorBrush val = ((value[pocKey].Delta >= 0) ? _bVaBuy : _bVaSell);
				HashSet<int> hashSet = (ShowCandleValueArea ? GetCandleValueAreaKeysCached(i, value, pocKey, totalVol, footprintDataVersion) : _emptyKeySet);
				HashSet<int> buyKeys;
				HashSet<int> sellKeys;
				if (ShowCandleImbalances)
				{
					GetCandleImbalanceKeysCached(i, value, ratio, minImb, CandleImbalanceStackRows, footprintDataVersion, out buyKeys, out sellKeys);
				}
				else
				{
					buyKeys = _emptyKeySet;
					sellKeys = _emptyKeySet;
				}
				_renderCandleBuyImbalanceCache[i] = buyKeys;
				_renderCandleSellImbalanceCache[i] = sellKeys;
				float candleFootprintRenderOffset = GetCandleFootprintRenderOffset(cc, i, dX);
				float num7 = num6 + candleFootprintRenderOffset;
				float candleFootprintRenderWidth = GetCandleFootprintRenderWidth(cc, i, dX, candleFootprintRenderOffset);
				if (ShowCandleValueArea && hashSet.Count > 0)
				{
					float num8 = float.MaxValue;
					float num9 = float.MinValue;
					foreach (int item in hashSet)
					{
						double num10 = KeyToPrice(item);
						if (!(num10 < cs.MinValue - num2) && !(num10 > cs.MaxValue + num2))
						{
							float num11 = cs.GetYByValue(num10);
							if (num11 < num8)
							{
								num8 = num11;
							}
							if (num11 > num9)
							{
								num9 = num11;
							}
						}
					}
					if (num8 < float.MaxValue && num9 > float.MinValue)
					{
						((SharpDX.Direct2D1.Brush)val).Opacity = OpacityPct(ValueAreaOpacity);
						base.RenderTarget.FillRectangle(new RectangleF(num7 - 1f * dX, num8 - num5 * 0.55f, candleFootprintRenderWidth + 2f * dX, num9 - num8 + num5 * 1.1f), (SharpDX.Direct2D1.Brush)(object)val);
					}
				}
				foreach (KeyValuePair<int, ILT_DEV_FOTPRINT_Level> item2 in value)
				{
					int key = item2.Key;
					ILT_DEV_FOTPRINT_Level value2 = item2.Value;
					if (value2.Total < MinVolumeToDisplay)
					{
						continue;
					}
					double num12 = KeyToPrice(key);
					bool flag3 = buyKeys.Contains(key);
					bool flag4 = sellKeys.Contains(key);
					if (!IsPriceInsideVisibleScale(cs, num12, num2))
					{
						if (flag3 || flag4)
						{
							float visibleBarSpacing = GetVisibleBarSpacing(cc, i, dX);
							bool allowPriceTag = visibleBarSpacing >= 12f * dX;
							DrawPersistentCandleImbalanceMarker(cc, cs, num7, candleFootprintRenderWidth, num5, num12, flag3, flag4, cL, cR, dX, dY, allowPriceTag);
						}
						continue;
					}
					float num13 = cs.GetYByValue(num12);
					float num14 = num13 - num5 * 0.5f;
					if (ShowCandleProfileHistogram)
					{
						long candleFootprintBarMetricValue = GetCandleFootprintBarMetricValue(value2);
						float num15 = Math.Max(1f, (float)((double)candleFootprintBarMetricValue / (double)maxBarMetric) * candleFootprintRenderWidth);
						SolidColorBrush candleFootprintBarBrush = GetCandleFootprintBarBrush(value2, key, pocKey, flag3, flag4);
						((SharpDX.Direct2D1.Brush)candleFootprintBarBrush).Opacity = GetCandleFootprintBarOpacity(key, pocKey, flag3, flag4);
						base.RenderTarget.FillRectangle(new RectangleF(num7, num14, num15, Math.Max(1f, num5)), (SharpDX.Direct2D1.Brush)(object)candleFootprintBarBrush);
					}
					if (flag3 || flag4)
					{
						DrawPersistentCandleImbalanceMarker(cc, cs, num7, candleFootprintRenderWidth, num5, num12, flag3, flag4, cL, cR, dX, dY, false);
						bool flag5 = i >= Math.Max(fromIndex, toIndex - 2);
						bool allowNewPulse = flag && flag5;
						if (DrawNewImbalancePulseIfActive(i, key, flag3, flag4, num7, num13, candleFootprintRenderWidth, num5, dX, cR, nowMs, allowNewPulse))
						{
							flag2 = true;
						}
					}
				}
			}
			if ((int)base.State == 7)
			{
				_imbalancePulsePrimed = true;
			}
			CleanupImbalancePulseCache(nowMs);
			if (flag2 && (int)base.State == 7)
			{
				long num16 = Environment.TickCount;
				if (num16 - _lastImbalancePulseRefreshMs >= Math.Max(33, VisualRefreshMs))
				{
					_lastImbalancePulseRefreshMs = num16;
					base.ForceRefresh();
				}
			}
		}

		private bool IsPrimaryBarInActiveSession(int barIndex)
		{
			if (barIndex < 0)
			{
				return false;
			}
			if (_nativeSessionBegin == DateTime.MinValue || _nativeSessionEnd == DateTime.MinValue)
			{
				return true;
			}
			try
			{
				DateTime valueAt = base.Times[0].GetValueAt(barIndex);
				return valueAt >= _nativeSessionBegin && valueAt <= _nativeSessionEnd;
			}
			catch
			{
				return false;
			}
		}

		private int GetExtendedImbalanceParamSignature()
		{
			int num = 17;
			num = num * 31 + EffectiveTicksPerRow();
			num = num * 31 + Math.Max(100, CandleImbalancePct);
			num = num * 31 + Math.Max(1, CandleMinImbalance);
			num = num * 31 + Math.Max(1, CandleImbalanceStackRows);
			num = num * 31 + Math.Max(1, MinVolumeToDisplay);
			return num * 31 + (CountZeroPrintImbalance ? 1 : 0);
		}

		private void PublishLatestFootprintExportFromPrimary()
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Invalid comparison between Unknown and I4
			if (_barFp == null || _barFp.Count == 0)
			{
				return;
			}
			if ((int)base.State == 7)
			{
				long num = Environment.TickCount;
				if (_lastFootprintExportMs > 0 && num - _lastFootprintExportMs < 250)
				{
					return;
				}
				_lastFootprintExportMs = num;
			}
			lock (_lk)
			{
				if (_barFp == null || _barFp.Count == 0)
				{
					return;
				}
				int num2 = base.CurrentBar;
				Dictionary<int, ILT_DEV_FOTPRINT_Level> value = null;
				if (num2 >= 0)
				{
					_barFp.TryGetValue(num2, out value);
				}
				if (value == null || value.Count == 0)
				{
					int num3 = int.MinValue;
					foreach (KeyValuePair<int, Dictionary<int, ILT_DEV_FOTPRINT_Level>> item in _barFp)
					{
						if (item.Value != null && item.Value.Count != 0 && item.Key <= base.CurrentBar && item.Key > num3)
						{
							num3 = item.Key;
							value = item.Value;
						}
					}
					if (num3 != int.MinValue)
					{
						num2 = num3;
					}
				}
				if (value == null || value.Count == 0)
				{
					return;
				}
				DateTime barTime = base.Time[0];
				try
				{
					if (num2 >= 0 && base.BarsArray != null && base.BarsArray.Length > 0)
					{
						barTime = base.Times[0].GetValueAt(num2);
					}
				}
				catch
				{
					barTime = base.Time[0];
				}
				PublishFootprintExportStateNoLock(num2, value, barTime);
			}
		}

		private void PublishFootprintExportStateNoLock(int primaryBar, Dictionary<int, ILT_DEV_FOTPRINT_Level> barMap, DateTime barTime)
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Invalid comparison between Unknown and I4
			//IL_02c5: Unknown result type (might be due to invalid IL or missing references)
			if (barMap == null || barMap.Count == 0)
			{
				return;
			}
			if ((int)base.State == 7)
			{
				long num = Environment.TickCount;
				if (_lastFootprintExportMs > 0 && num - _lastFootprintExportMs < 80)
				{
					return;
				}
				_lastFootprintExportMs = num;
			}
			long maxBarMetric;
			long totalVol;
			int pocKey;
			GetCandleFootprintStats(barMap, out maxBarMetric, out totalVol, out pocKey);
			long num2 = 0L;
			long num3 = 0L;
			long num4 = 0L;
			long barPOCVolume = 0L;
			long barPOCDelta = 0L;
			int num5 = int.MinValue;
			long num6 = -1L;
			foreach (KeyValuePair<int, ILT_DEV_FOTPRINT_Level> item in barMap)
			{
				ILT_DEV_FOTPRINT_Level value = item.Value;
				if (value != null)
				{
					num2 += Math.Max(0L, value.BidVol);
					num3 += Math.Max(0L, value.AskVol);
					long num7 = Math.Abs(value.Delta);
					if (num7 > num6)
					{
						num6 = num7;
						num5 = item.Key;
					}
				}
			}
			num4 = num3 - num2;
			if (pocKey != int.MinValue && barMap.ContainsKey(pocKey))
			{
				barPOCVolume = barMap[pocKey].Total;
				barPOCDelta = barMap[pocKey].Delta;
			}
			long num8 = 0L;
			long num9 = 0L;
			int num10 = int.MinValue;
			long num11 = -1L;
			if (_sessionFp != null)
			{
				foreach (KeyValuePair<int, ILT_DEV_FOTPRINT_Level> item2 in _sessionFp)
				{
					ILT_DEV_FOTPRINT_Level value2 = item2.Value;
					if (value2 != null)
					{
						num8 += Math.Max(0L, value2.BidVol);
						num9 += Math.Max(0L, value2.AskVol);
						long num12 = Math.Abs(value2.Delta);
						if (num12 > num11)
						{
							num11 = num12;
							num10 = item2.Key;
						}
					}
				}
			}
			double ratio = (double)Math.Max(100, CandleImbalancePct) / 100.0;
			long minDominant = Math.Max(1, CandleMinImbalance);
			int num13 = Math.Max(1, CandleImbalanceStackRows);
			HashSet<int> stackFilteredImbalanceKeys = GetStackFilteredImbalanceKeys(barMap, true, ratio, minDominant, num13);
			HashSet<int> stackFilteredImbalanceKeys2 = GetStackFilteredImbalanceKeys(barMap, false, ratio, minDominant, num13);
			bool flag = stackFilteredImbalanceKeys != null && stackFilteredImbalanceKeys.Count >= num13;
			bool flag2 = stackFilteredImbalanceKeys2 != null && stackFilteredImbalanceKeys2.Count >= num13;
			string stackedDirection = "NONE";
			if (flag && flag2)
			{
				stackedDirection = "MIXED";
			}
			else if (flag)
			{
				stackedDirection = "BUY";
			}
			else if (flag2)
			{
				stackedDirection = "SELL";
			}
			ILT_DEV_FOTPRINT_ExportState iltDev_FootprintExportState = new ILT_DEV_FOTPRINT_ExportState();
			iltDev_FootprintExportState.InstrumentFullName = ((base.Instrument != null) ? base.Instrument.FullName : string.Empty);
			iltDev_FootprintExportState.InstrumentRoot = GetExportInstrumentRoot(iltDev_FootprintExportState.InstrumentFullName);
			iltDev_FootprintExportState.BarsPeriod = ((base.BarsPeriod != null) ? (((object)base.BarsPeriod.BarsPeriodType).ToString() + "|" + base.BarsPeriod.Value) : string.Empty);
			iltDev_FootprintExportState.Source = "ILT_DEV_FOTPRINT";
			iltDev_FootprintExportState.Status = "OK";
			iltDev_FootprintExportState.Timestamp = DateTime.Now;
			iltDev_FootprintExportState.DataVersion = _footprintDataVersion;
			iltDev_FootprintExportState.BarIndex = primaryBar;
			iltDev_FootprintExportState.BarTime = barTime;
			iltDev_FootprintExportState.BarPOC = ((pocKey != int.MinValue) ? KeyToPrice(pocKey) : 0.0);
			iltDev_FootprintExportState.BarDPOC = ((num5 != int.MinValue) ? KeyToPrice(num5) : 0.0);
			iltDev_FootprintExportState.BarPOCVolume = barPOCVolume;
			iltDev_FootprintExportState.BarPOCDelta = barPOCDelta;
			iltDev_FootprintExportState.BarBidVolume = num2;
			iltDev_FootprintExportState.BarAskVolume = num3;
			iltDev_FootprintExportState.BarDelta = num4;
			iltDev_FootprintExportState.BarTotalVolume = num2 + num3;
			iltDev_FootprintExportState.SessionPOC = ((_sessionPocKey != int.MinValue) ? KeyToPrice(_sessionPocKey) : 0.0);
			iltDev_FootprintExportState.SessionDPOC = ((num10 != int.MinValue) ? KeyToPrice(num10) : 0.0);
			iltDev_FootprintExportState.SessionPOCVolume = _sessionPocVol;
			iltDev_FootprintExportState.SessionTotalVolume = _sessionTotalVol;
			iltDev_FootprintExportState.SessionBidVolume = num8;
			iltDev_FootprintExportState.SessionAskVolume = num9;
			iltDev_FootprintExportState.SessionDelta = num9 - num8;
			iltDev_FootprintExportState.BuyImbalanceCount = ((stackFilteredImbalanceKeys != null) ? stackFilteredImbalanceKeys.Count : 0);
			iltDev_FootprintExportState.SellImbalanceCount = ((stackFilteredImbalanceKeys2 != null) ? stackFilteredImbalanceKeys2.Count : 0);
			iltDev_FootprintExportState.StackedImbalance = flag || flag2;
			iltDev_FootprintExportState.StackedDirection = stackedDirection;
			iltDev_FootprintExportState.StackedRowsRequired = num13;
			AddImbalanceExportsNoLock(iltDev_FootprintExportState.ImbalanceLevels, barMap, stackFilteredImbalanceKeys, true);
			AddImbalanceExportsNoLock(iltDev_FootprintExportState.ImbalanceLevels, barMap, stackFilteredImbalanceKeys2, false);
			PublishFootprintExportState(iltDev_FootprintExportState);
		}

		private void AddImbalanceExportsNoLock(List<ILT_DEV_FOTPRINT_ImbalanceExport> dst, Dictionary<int, ILT_DEV_FOTPRINT_Level> map, HashSet<int> keys, bool buy)
		{
			if (dst == null || map == null || keys == null || keys.Count == 0)
			{
				return;
			}
			int num = EffectiveTicksPerRow();
			List<int> list = new List<int>(keys);
			list.Sort();
			foreach (int item in list)
			{
				if (dst.Count >= 12)
				{
					break;
				}
				ILT_DEV_FOTPRINT_Level value;
				if (!map.TryGetValue(item, out value) || value == null)
				{
					continue;
				}
				long num2 = (buy ? value.AskVol : value.BidVol);
				long num3 = 0L;
				ILT_DEV_FOTPRINT_Level value3;
				if (buy)
				{
					ILT_DEV_FOTPRINT_Level value2;
					if (map.TryGetValue(item - num, out value2) && value2 != null)
					{
						num3 = value2.BidVol;
					}
				}
				else if (map.TryGetValue(item + num, out value3) && value3 != null)
				{
					num3 = value3.AskVol;
				}
				double ratio = ((num3 > 0) ? ((double)num2 / Math.Max(1.0, num3)) : 999.0);
				ILT_DEV_FOTPRINT_ImbalanceExport iltDev_FootprintImbalanceExport = new ILT_DEV_FOTPRINT_ImbalanceExport();
				iltDev_FootprintImbalanceExport.Price = KeyToPrice(item);
				iltDev_FootprintImbalanceExport.Side = (buy ? "BUY" : "SELL");
				iltDev_FootprintImbalanceExport.DominantVolume = num2;
				iltDev_FootprintImbalanceExport.OppositeVolume = num3;
				iltDev_FootprintImbalanceExport.Ratio = ratio;
				iltDev_FootprintImbalanceExport.Delta = value.Delta;
				dst.Add(iltDev_FootprintImbalanceExport);
			}
		}

		private void PublishFootprintExportState(ILT_DEV_FOTPRINT_ExportState state)
		{
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Unknown result type (might be due to invalid IL or missing references)
			if (state == null)
			{
				return;
			}
			string text = state.InstrumentFullName ?? string.Empty;
			string text2 = state.InstrumentRoot ?? GetExportInstrumentRoot(text);
			string key = BuildFootprintExportKey(text, base.BarsPeriod.BarsPeriodType, base.BarsPeriod.Value);
			string key2 = BuildFootprintExportKey(text2, base.BarsPeriod.BarsPeriodType, base.BarsPeriod.Value);
			ILT_DEV_FOTPRINT_ExportState iltDev_FootprintExportState = state.Clone();
			lock (_footprintExportBusLock)
			{
				_footprintExportBus[key] = iltDev_FootprintExportState.Clone();
				_footprintExportBus[key2] = iltDev_FootprintExportState.Clone();
				_footprintExportBus[text] = iltDev_FootprintExportState.Clone();
				_footprintExportBus[text2] = iltDev_FootprintExportState.Clone();
			}
		}

		private void MarkExtendedImbalanceCacheDirtyNoLock()
		{
			_footprintDataVersion++;
			_extendedImbalanceCacheDirty = true;
		}

		private List<ExtendedImbalanceLineState> SnapshotExtendedImbalanceLineCache()
		{
			lock (_extendedImbalanceCacheLock)
			{
				return _extendedImbalanceLineCache ?? new List<ExtendedImbalanceLineState>();
			}
		}

		private void EnsureExtendedImbalanceLineCache()
		{
			//IL_007e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0084: Invalid comparison between Unknown and I4
			int extendedImbalanceParamSignature = GetExtendedImbalanceParamSignature();
			long footprintDataVersion;
			bool extendedImbalanceCacheDirty;
			lock (_lk)
			{
				footprintDataVersion = _footprintDataVersion;
				extendedImbalanceCacheDirty = _extendedImbalanceCacheDirty;
			}
			bool flag;
			lock (_extendedImbalanceCacheLock)
			{
				flag = extendedImbalanceCacheDirty || _extendedImbalanceCacheDataVersion != footprintDataVersion || _extendedImbalanceCacheParamSignature != extendedImbalanceParamSignature || _extendedImbalanceLineCache == null;
			}
			if (!flag)
			{
				return;
			}
			if ((int)base.State == 7)
			{
				long num = Environment.TickCount;
				if (_lastExtendedImbalanceCacheBuildMs > 0 && num - _lastExtendedImbalanceCacheBuildMs < 350)
				{
					return;
				}
				_lastExtendedImbalanceCacheBuildMs = num;
			}
			Dictionary<int, Dictionary<int, ILT_DEV_FOTPRINT_Level>> dictionary;
			lock (_lk)
			{
				if (_barFp == null || _barFp.Count == 0)
				{
					lock (_extendedImbalanceCacheLock)
					{
						_extendedImbalanceLineCache = new List<ExtendedImbalanceLineState>();
						_extendedImbalanceCacheDataVersion = footprintDataVersion;
						_extendedImbalanceCacheParamSignature = extendedImbalanceParamSignature;
					}
					_extendedImbalanceCacheDirty = false;
					return;
				}
				dictionary = new Dictionary<int, Dictionary<int, ILT_DEV_FOTPRINT_Level>>(_barFp.Count);
				foreach (KeyValuePair<int, Dictionary<int, ILT_DEV_FOTPRINT_Level>> item in _barFp)
				{
					if (item.Value != null && item.Value.Count > 0)
					{
						dictionary[item.Key] = (UseNativeVolumetricBidAsk ? item.Value : CloneFootprintMap(item.Value));
					}
				}
			}
			List<ExtendedImbalanceLineState> extendedImbalanceLineCache = BuildExtendedImbalanceLineCacheFromSnapshot(dictionary);
			lock (_extendedImbalanceCacheLock)
			{
				_extendedImbalanceLineCache = extendedImbalanceLineCache;
				_extendedImbalanceCacheDataVersion = footprintDataVersion;
				_extendedImbalanceCacheParamSignature = extendedImbalanceParamSignature;
			}
			lock (_lk)
			{
				_extendedImbalanceCacheDirty = false;
			}
		}

		private List<ExtendedImbalanceLineState> BuildExtendedImbalanceLineCacheFromSnapshot(Dictionary<int, Dictionary<int, ILT_DEV_FOTPRINT_Level>> fp)
		{
			List<ExtendedImbalanceLineState> list = new List<ExtendedImbalanceLineState>();
			if (fp == null || fp.Count == 0)
			{
				return list;
			}
			double ratio = (double)Math.Max(100, CandleImbalancePct) / 100.0;
			long minDominant = Math.Max(1, CandleMinImbalance);
			int requiredRows = Math.Max(1, CandleImbalanceStackRows);
			int num = EffectiveTicksPerRow();
			Dictionary<int, List<ExtendedImbalanceLineState>> dictionary = new Dictionary<int, List<ExtendedImbalanceLineState>>();
			int num2 = int.MaxValue;
			int num3 = int.MinValue;
			bool flag = false;
			List<int> list2 = fp.Keys.OrderBy((int k) => k).ToList();
			int endBar = ((list2.Count > 0) ? list2[list2.Count - 1] : 0);
			foreach (int item2 in list2)
			{
				Dictionary<int, ILT_DEV_FOTPRINT_Level> dictionary2 = fp[item2];
				if (dictionary2 == null || dictionary2.Count == 0)
				{
					continue;
				}
				int num4 = int.MaxValue;
				int num5 = int.MinValue;
				try
				{
					num4 = PriceToKey(base.Lows[0].GetValueAt(item2));
					num5 = PriceToKey(base.Highs[0].GetValueAt(item2));
					if (num4 > num5)
					{
						int num6 = num4;
						num4 = num5;
						num5 = num6;
					}
				}
				catch
				{
					if (dictionary2.Count > 0)
					{
						num4 = dictionary2.Keys.Min();
						num5 = dictionary2.Keys.Max();
					}
				}
				if (dictionary.Count > 0 && num4 != int.MaxValue && num5 != int.MinValue)
				{
					if (flag)
					{
						num2 = int.MaxValue;
						num3 = int.MinValue;
						foreach (int key in dictionary.Keys)
						{
							if (key < num2)
							{
								num2 = key;
							}
							if (key > num3)
							{
								num3 = key;
							}
						}
						flag = false;
					}
					if (num5 + num >= num2 && num4 - num <= num3)
					{
						List<int> list3 = new List<int>();
						foreach (int key2 in dictionary.Keys)
						{
							if (key2 >= num4 - num && key2 <= num5 + num)
							{
								list3.Add(key2);
							}
						}
						foreach (int item3 in list3)
						{
							List<ExtendedImbalanceLineState> value;
							if (!dictionary.TryGetValue(item3, out value) || value == null)
							{
								continue;
							}
							for (int num7 = 0; num7 < value.Count; num7++)
							{
								ExtendedImbalanceLineState extendedImbalanceLineState = value[num7];
								if (extendedImbalanceLineState != null && extendedImbalanceLineState.OriginBar < item2)
								{
									extendedImbalanceLineState.EndBar = item2;
									extendedImbalanceLineState.Mitigated = true;
									list.Add(extendedImbalanceLineState);
								}
							}
							dictionary.Remove(item3);
							flag = true;
						}
					}
				}
				HashSet<int> stackFilteredImbalanceKeys = GetStackFilteredImbalanceKeys(dictionary2, true, ratio, minDominant, requiredRows);
				HashSet<int> stackFilteredImbalanceKeys2 = GetStackFilteredImbalanceKeys(dictionary2, false, ratio, minDominant, requiredRows);
				if ((stackFilteredImbalanceKeys == null || stackFilteredImbalanceKeys.Count == 0) && (stackFilteredImbalanceKeys2 == null || stackFilteredImbalanceKeys2.Count == 0))
				{
					continue;
				}
				HashSet<int> hashSet = ((stackFilteredImbalanceKeys != null) ? new HashSet<int>(stackFilteredImbalanceKeys) : new HashSet<int>());
				if (stackFilteredImbalanceKeys2 != null)
				{
					hashSet.UnionWith(stackFilteredImbalanceKeys2);
				}
				long originMaxBid;
				long originMaxAsk;
				long originMaxTotal;
				long originMaxAbsDelta;
				long ignoredTotalBid;
				long ignoredTotalAsk;
				int ignoredPocKey;
				GetBetaMapStats(dictionary2, out originMaxBid, out originMaxAsk, out originMaxTotal, out originMaxAbsDelta, out ignoredTotalBid, out ignoredTotalAsk, out ignoredPocKey);
				foreach (int item4 in hashSet)
				{
					ILT_DEV_FOTPRINT_Level value2;
					if (!dictionary2.TryGetValue(item4, out value2) || value2 == null || value2.Total < MinVolumeToDisplay)
					{
						continue;
					}
					bool flag2 = stackFilteredImbalanceKeys != null && stackFilteredImbalanceKeys.Contains(item4);
					bool flag3 = stackFilteredImbalanceKeys2 != null && stackFilteredImbalanceKeys2.Contains(item4);
					if (flag2 || flag3)
					{
						ExtendedImbalanceLineState extendedImbalanceLineState2 = new ExtendedImbalanceLineState();
						extendedImbalanceLineState2.OriginBar = item2;
						extendedImbalanceLineState2.EndBar = endBar;
						extendedImbalanceLineState2.PriceKey = item4;
						extendedImbalanceLineState2.Buy = flag2;
						extendedImbalanceLineState2.Sell = flag3;
						extendedImbalanceLineState2.Mitigated = false;
						extendedImbalanceLineState2.OriginBid = Math.Max(0L, value2.BidVol);
						extendedImbalanceLineState2.OriginAsk = Math.Max(0L, value2.AskVol);
						extendedImbalanceLineState2.OriginTotal = Math.Max(0L, value2.Total);
						extendedImbalanceLineState2.OriginDelta = value2.Delta;
						extendedImbalanceLineState2.OriginMaxBid = Math.Max(1L, originMaxBid);
						extendedImbalanceLineState2.OriginMaxAsk = Math.Max(1L, originMaxAsk);
						extendedImbalanceLineState2.OriginMaxTotal = Math.Max(1L, originMaxTotal);
						extendedImbalanceLineState2.OriginMaxAbsDelta = Math.Max(1L, originMaxAbsDelta);
						ExtendedImbalanceLineState item = extendedImbalanceLineState2;
						List<ExtendedImbalanceLineState> value3;
						if (!dictionary.TryGetValue(item4, out value3))
						{
							value3 = (dictionary[item4] = new List<ExtendedImbalanceLineState>());
						}
						value3.Add(item);
						if (item4 < num2)
						{
							num2 = item4;
						}
						if (item4 > num3)
						{
							num3 = item4;
						}
					}
				}
			}
			foreach (KeyValuePair<int, List<ExtendedImbalanceLineState>> item5 in dictionary)
			{
				if (item5.Value == null)
				{
					continue;
				}
				foreach (ExtendedImbalanceLineState item6 in item5.Value)
				{
					if (item6 != null)
					{
						item6.EndBar = endBar;
						item6.Mitigated = false;
						list.Add(item6);
					}
				}
			}
			return list;
		}

		private void DrawExtendedImbalanceLines(ChartControl cc, ChartScale cs, float cL, float cR, float dX, float dY)
		{
			//IL_026e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0277: Unknown result type (might be due to invalid IL or missing references)
			//IL_02a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b1: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_0311: Unknown result type (might be due to invalid IL or missing references)
			//IL_0451: Unknown result type (might be due to invalid IL or missing references)
			if (!ShowExtendedImbalanceLines || cc == null || cs == null || base.ChartBars == null || base.RenderTarget == null)
			{
				return;
			}
			int fromIndex = base.ChartBars.FromIndex;
			int toIndex = base.ChartBars.ToIndex;
			if (toIndex < fromIndex)
			{
				return;
			}
			EnsureExtendedImbalanceLineCache();
			List<ExtendedImbalanceLineState> list = SnapshotExtendedImbalanceLineCache();
			if (list == null || list.Count == 0)
			{
				return;
			}
			float num = Math.Max(1f, ExtendedImbalanceLineWidth) * dX;
			float num2 = Math.Max(3f * dY, num + 2f * dY);
			int num3 = 0;
			for (int i = 0; i < 2; i++)
			{
				bool flag = i == 1;
				for (int j = 0; j < list.Count; j++)
				{
					ExtendedImbalanceLineState extendedImbalanceLineState = list[j];
					if (extendedImbalanceLineState == null || extendedImbalanceLineState.Mitigated != flag || extendedImbalanceLineState.OriginBar > toIndex)
					{
						continue;
					}
					int num4 = (extendedImbalanceLineState.Mitigated ? extendedImbalanceLineState.EndBar : Math.Max(extendedImbalanceLineState.EndBar, toIndex));
					if (num4 < fromIndex)
					{
						continue;
					}
					float val;
					float num5;
					double price = KeyToPrice(extendedImbalanceLineState.PriceKey);
					try
					{
						float originOffset = GetCandleFootprintRenderOffset(cc, extendedImbalanceLineState.OriginBar, dX);
						float originSpacing = GetVisibleBarSpacing(cc, extendedImbalanceLineState.OriginBar, dX);
						float originWidth = GetBetaCandleCellWidth(originSpacing, dX);
						float originCenterX = (float)cc.GetXByBarIndex(base.ChartBars, extendedImbalanceLineState.OriginBar) + originOffset;
						float originRowLeft = originCenterX - originWidth * 0.5f;
						float originRowWidth = originWidth;

						float originRowCenter = originRowLeft + originRowWidth * 0.5f;
						float originHalf = originRowWidth * 0.5f;
						float originBidRatio = (float)((double)Math.Max(0L, extendedImbalanceLineState.OriginBid) / (double)Math.Max(1L, extendedImbalanceLineState.OriginMaxBid));
						float originAskRatio = (float)((double)Math.Max(0L, extendedImbalanceLineState.OriginAsk) / (double)Math.Max(1L, extendedImbalanceLineState.OriginMaxAsk));
						float originTotalRatio = (float)((double)Math.Max(0L, extendedImbalanceLineState.OriginTotal) / (double)Math.Max(1L, extendedImbalanceLineState.OriginMaxTotal));
						float originDeltaRatio = (float)((double)AbsBetaLong(extendedImbalanceLineState.OriginDelta) / (double)Math.Max(1L, extendedImbalanceLineState.OriginMaxAbsDelta));
						switch (VisualMode)
						{
						case IltDevFootprintDisplayMode.SidedLadder:
						case IltDevFootprintDisplayMode.SidedLadderPlus:
							val = extendedImbalanceLineState.Buy ? originRowCenter + originHalf * originAskRatio : originRowCenter;
							break;
						case IltDevFootprintDisplayMode.Profile:
							float originProfileWidth = Math.Max(1f, originRowWidth * originTotalRatio);
							val = VolumeProfileDirection == 1
								? originRowLeft + originProfileWidth
								: (VolumeProfileDirection == 3 ? originRowLeft + originRowWidth : originRowCenter + originProfileWidth * 0.5f);
							break;
						case IltDevFootprintDisplayMode.DeltaLadder:
							val = DeltaHistogramDirection == 3 ? originRowLeft + originRowWidth : originRowCenter;
							break;
						case IltDevFootprintDisplayMode.DeltaProfile:
							val = (DeltaHistogramDirection == 1 || (DeltaHistogramDirection == 2 && extendedImbalanceLineState.OriginDelta < 0L))
								? originRowCenter
								: originRowCenter + originHalf * originDeltaRatio;
							break;
						default:
							val = originRowCenter;
							break;
						}
						val += Math.Max(1f, dX);
						num5 = (extendedImbalanceLineState.Mitigated ? ((float)cc.GetXByBarIndex(base.ChartBars, extendedImbalanceLineState.EndBar)) : cR);
					}
					catch
					{
						continue;
					}
					float num6 = Math.Max(cL, val);
					float num7 = (extendedImbalanceLineState.Mitigated ? Math.Min(cR, num5) : cR);
					if (num7 <= cL || num6 >= cR || num7 <= num6 + 0.5f * dX)
					{
						continue;
					}
					if (num3 >= 6000 && flag)
					{
						return;
					}
					bool clippedTop;
					bool clippedBottom;
					float persistentImbalanceY = GetPersistentImbalanceY(cs, price, num2, dY, out clippedTop, out clippedBottom);
					SolidColorBrush val2 = (extendedImbalanceLineState.Buy ? _bImbBuy : _bImbSell);
					if (val2 != null)
					{
						float num8 = OpacityPct(ImbalanceLineOpacity);
						float opacity = (extendedImbalanceLineState.Mitigated ? Math.Min(0.28f, num8 * 0.34f) : num8);
						float opacity2 = (extendedImbalanceLineState.Mitigated ? Math.Min(0.16f, num8 * 0.18f) : Math.Min(0.38f, num8 * 0.45f));
						float num9 = (extendedImbalanceLineState.Mitigated ? Math.Max(0.75f * dX, num * 0.82f) : num);
						((SharpDX.Direct2D1.Brush)val2).Opacity = opacity2;
						base.RenderTarget.DrawLine(new Vector2(num6, persistentImbalanceY), new Vector2(num7, persistentImbalanceY), (SharpDX.Direct2D1.Brush)(object)val2, Math.Max(num9, num9 + 2f * dX));
						((SharpDX.Direct2D1.Brush)val2).Opacity = opacity;
						base.RenderTarget.DrawLine(new Vector2(num6, persistentImbalanceY), new Vector2(num7, persistentImbalanceY), (SharpDX.Direct2D1.Brush)(object)val2, num9);
						if (extendedImbalanceLineState.Mitigated && num5 >= cL && num5 <= cR)
						{
							((SharpDX.Direct2D1.Brush)val2).Opacity = Math.Min(0.42f, num8 * 0.55f);
							base.RenderTarget.DrawLine(new Vector2(num7, persistentImbalanceY - num2 * 0.5f), new Vector2(num7, persistentImbalanceY + num2 * 0.5f), (SharpDX.Direct2D1.Brush)(object)val2, Math.Max(0.85f * dX, num9));
						}
						num3++;
					}
				}
			}
		}

		private bool IsBuyImbalance(Dictionary<int, ILT_DEV_FOTPRINT_Level> map, int key, double ratio, long minDominant)
		{
			ILT_DEV_FOTPRINT_Level value;
			if (!map.TryGetValue(key, out value) || value.AskVol < minDominant)
			{
				return false;
			}
			long num = 0L;
			ILT_DEV_FOTPRINT_Level value2;
			if (map.TryGetValue(key - EffectiveTicksPerRow(), out value2))
			{
				num = value2.BidVol;
			}
			if (num <= 0)
			{
				if (CountZeroPrintImbalance)
				{
					return value.AskVol >= minDominant;
				}
				return false;
			}
			return (double)value.AskVol >= Math.Ceiling((double)num * ratio);
		}

		private bool IsSellImbalance(Dictionary<int, ILT_DEV_FOTPRINT_Level> map, int key, double ratio, long minDominant)
		{
			ILT_DEV_FOTPRINT_Level value;
			if (!map.TryGetValue(key, out value) || value.BidVol < minDominant)
			{
				return false;
			}
			long num = 0L;
			ILT_DEV_FOTPRINT_Level value2;
			if (map.TryGetValue(key + EffectiveTicksPerRow(), out value2))
			{
				num = value2.AskVol;
			}
			if (num <= 0)
			{
				if (CountZeroPrintImbalance)
				{
					return value.BidVol >= minDominant;
				}
				return false;
			}
			return (double)value.BidVol >= Math.Ceiling((double)num * ratio);
		}

		private HashSet<int> GetStackFilteredImbalanceKeys(Dictionary<int, ILT_DEV_FOTPRINT_Level> map, bool buy, double ratio, long minDominant, int requiredRows)
		{
			HashSet<int> hashSet = new HashSet<int>();
			if (map == null || map.Count == 0)
			{
				return hashSet;
			}
			int num = EffectiveTicksPerRow();
			int num2 = Math.Max(1, requiredRows);
			List<int> list = new List<int>();
			foreach (int key in map.Keys)
			{
				if (buy ? IsBuyImbalance(map, key, ratio, minDominant) : IsSellImbalance(map, key, ratio, minDominant))
				{
					list.Add(key);
				}
			}
			if (list.Count == 0)
			{
				return hashSet;
			}
			list.Sort();
			int num3 = 0;
			for (int i = 1; i <= list.Count; i++)
			{
				if (i != list.Count && list[i] == list[i - 1] + num)
				{
					continue;
				}
				int num4 = i - num3;
				if (num4 >= num2)
				{
					for (int j = num3; j < i; j++)
					{
						hashSet.Add(list[j]);
					}
				}
				num3 = i;
			}
			return hashSet;
		}

		private HashSet<int> GetValueAreaKeys(Dictionary<int, ILT_DEV_FOTPRINT_Level> map, int pocKey, long totalVol)
		{
			return ComputeValueAreaKeys(map, pocKey, totalVol);
		}

		private HashSet<int> ComputeValueAreaKeys(Dictionary<int, ILT_DEV_FOTPRINT_Level> map, int pocKey, long totalVol)
		{
			if (map == null || map.Count == 0 || pocKey == int.MinValue || !map.ContainsKey(pocKey) || totalVol <= 0)
			{
				return new HashSet<int>();
			}
			double num = (double)(totalVol * Math.Max(1, Math.Min(99, ValueAreaPct))) / 100.0;
			List<int> list = new List<int>(map.Keys);
			list.Sort();
			int num2 = list.IndexOf(pocKey);
			HashSet<int> hashSet = new HashSet<int>();
			hashSet.Add(pocKey);
			long num3 = map[pocKey].Total;
			int num4 = num2 - 1;
			int num5 = num2 + 1;
			while ((double)num3 < num && (num4 >= 0 || num5 < list.Count))
			{
				long num6 = ((num4 >= 0) ? map[list[num4]].Total : (-1));
				long num7 = ((num5 < list.Count) ? map[list[num5]].Total : (-1));
				if (num7 >= num6)
				{
					hashSet.Add(list[num5]);
					num3 += map[list[num5]].Total;
					num5++;
				}
				else
				{
					hashSet.Add(list[num4]);
					num3 += map[list[num4]].Total;
					num4--;
				}
			}
			return hashSet;
		}

		private int GetCandleImbalanceParamSig(double ratio, long minImb, int stackRows)
		{
			int num = 17;
			num = num * 31 + (int)Math.Round(ratio * 1000.0);
			num = num * 31 + (int)(minImb & 0x7FFFFFFF);
			num = num * 31 + Math.Max(1, stackRows);
			num = num * 31 + EffectiveTicksPerRow();
			return num * 31 + Math.Max(0, MinVolumeToDisplay);
		}

		private void GetCandleImbalanceKeysCached(int barIndex, Dictionary<int, ILT_DEV_FOTPRINT_Level> map, double ratio, long minImb, int stackRows, long dataVersion, out HashSet<int> buyKeys, out HashSet<int> sellKeys)
		{
			int candleImbalanceParamSig = GetCandleImbalanceParamSig(ratio, minImb, stackRows);
			BarImbCacheEntry value;
			if (_candleImbKeyCache.TryGetValue(barIndex, out value) && value.DataVersion == dataVersion && value.ParamSig == candleImbalanceParamSig && value.Buy != null && value.Sell != null)
			{
				buyKeys = value.Buy;
				sellKeys = value.Sell;
				return;
			}
			buyKeys = GetStackFilteredImbalanceKeys(map, true, ratio, minImb, stackRows);
			sellKeys = GetStackFilteredImbalanceKeys(map, false, ratio, minImb, stackRows);
			_candleImbKeyCache[barIndex] = new BarImbCacheEntry
			{
				DataVersion = dataVersion,
				ParamSig = candleImbalanceParamSig,
				Buy = buyKeys,
				Sell = sellKeys
			};
		}

		private int GetCandleVaParamSig(int pocKey, long totalVol)
		{
			int num = 17;
			num = num * 31 + Math.Max(1, Math.Min(99, ValueAreaPct));
			num = num * 31 + pocKey;
			return num * 31 + (int)(totalVol & 0x7FFFFFFF);
		}

		private HashSet<int> GetCandleValueAreaKeysCached(int barIndex, Dictionary<int, ILT_DEV_FOTPRINT_Level> map, int pocKey, long totalVol, long dataVersion)
		{
			int candleVaParamSig = GetCandleVaParamSig(pocKey, totalVol);
			BarVaCacheEntry value;
			if (_candleVaKeyCache.TryGetValue(barIndex, out value) && value.DataVersion == dataVersion && value.ParamSig == candleVaParamSig && value.Keys != null)
			{
				return value.Keys;
			}
			HashSet<int> hashSet = ComputeValueAreaKeys(map, pocKey, totalVol);
			_candleVaKeyCache[barIndex] = new BarVaCacheEntry
			{
				DataVersion = dataVersion,
				ParamSig = candleVaParamSig,
				Keys = hashSet
			};
			return hashSet;
		}

		private void TrimBarKeyCaches(int fromBar, int toBar)
		{
			if (_candleImbKeyCache.Count > 600)
			{
				_cacheEvictBuffer.Clear();
				foreach (int key in _candleImbKeyCache.Keys)
				{
					if (key < fromBar - 50 || key > toBar + 50)
					{
						_cacheEvictBuffer.Add(key);
					}
				}
				for (int i = 0; i < _cacheEvictBuffer.Count; i++)
				{
					_candleImbKeyCache.Remove(_cacheEvictBuffer[i]);
				}
			}
			if (_candleVaKeyCache.Count <= 600)
			{
				return;
			}
			_cacheEvictBuffer.Clear();
			foreach (int key2 in _candleVaKeyCache.Keys)
			{
				if (key2 < fromBar - 50 || key2 > toBar + 50)
				{
					_cacheEvictBuffer.Add(key2);
				}
			}
			for (int j = 0; j < _cacheEvictBuffer.Count; j++)
			{
				_candleVaKeyCache.Remove(_cacheEvictBuffer[j]);
			}
		}

		private void DrawFloatingStatsWindow(float W, float H, float dX, float dY)
		{
			//IL_01b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_020e: Unknown result type (might be due to invalid IL or missing references)
			//IL_024b: Unknown result type (might be due to invalid IL or missing references)
			int diagVolBarsSummed;
			DateTime diagFirstVolBarTime;
			DateTime diagLastVolBarTime;
			long diagTotalBid;
			long diagTotalAsk;
			int sessionPocKey;
			long sessionPocVol;
			long sessionMaxDeltaAbs;
			int num;
			DateTime nativeSessionBegin;
			DateTime nativeSessionEnd;
			lock (_lk)
			{
				diagVolBarsSummed = _diagVolBarsSummed;
				diagFirstVolBarTime = _diagFirstVolBarTime;
				diagLastVolBarTime = _diagLastVolBarTime;
				diagTotalBid = _diagTotalBid;
				diagTotalAsk = _diagTotalAsk;
				sessionPocKey = _sessionPocKey;
				sessionPocVol = _sessionPocVol;
				sessionMaxDeltaAbs = _sessionMaxDeltaAbs;
				num = ((_sessionFp != null) ? _sessionFp.Count : 0);
				nativeSessionBegin = _nativeSessionBegin;
				nativeSessionEnd = _nativeSessionEnd;
			}
			float num2 = Math.Max(240f * dX, (float)StatsWindowWidth * dX);
			float num3 = Math.Max(20f * dY, ((float)FontSizeBold + 8f) * dY);
			float num4 = Math.Max(15f * dY, ((float)FontSizeSmall + 6f) * dY);
			float num5 = 8f * dX;
			float num6 = num3 + num4 * 8f + num5 * 1.35f;
			float num7 = ClampF((float)StatsWindowX * dX, 0f, Math.Max(0f, W - num2 - 2f * dX));
			float num8 = ClampF((float)StatsWindowY * dY, 0f, Math.Max(0f, H - num6 - 2f * dY));
			if (!_statsDragging)
			{
				StatsWindowX = Clamp((int)Math.Round(num7 / Math.Max(1f, dX)), 0, 10000);
				StatsWindowY = Clamp((int)Math.Round(num8 / Math.Max(1f, dY)), 0, 10000);
			}
			_statsWindowRect = new RectangleF(num7, num8, num2, num6);
			_statsDragHandleRect = new RectangleF(num7, num8, num2, num3);
			((SharpDX.Direct2D1.Brush)_bPanel).Opacity = 0.86f;
			base.RenderTarget.FillRectangle(_statsWindowRect, (SharpDX.Direct2D1.Brush)(object)_bPanel);
			((SharpDX.Direct2D1.Brush)_bGrid).Opacity = 0.75f;
			base.RenderTarget.DrawRectangle(_statsWindowRect, (SharpDX.Direct2D1.Brush)(object)_bGrid, 0.9f * dX);
			((SharpDX.Direct2D1.Brush)_bGrid).Opacity = (_statsDragging ? 0.62f : 0.42f);
			base.RenderTarget.FillRectangle(_statsDragHandleRect, (SharpDX.Direct2D1.Brush)(object)_bGrid);
			((SharpDX.Direct2D1.Brush)_bText).Opacity = 1f;
			T("SESSION STATS  —  drag header", num7 + num5, num8 + 3f * dY, _fBold, _bText, num2 - num5 * 2f, num3);
			long v = Math.Max(0L, diagTotalBid) + Math.Max(0L, diagTotalAsk);
			long num9 = diagTotalAsk - diagTotalBid;
			string text = ((sessionPocKey == int.MinValue) ? "--" : (KeyToPrice(sessionPocKey).ToString("F" + _decs) + " / " + FQ(sessionPocVol)));
			string text2 = ((diagFirstVolBarTime == DateTime.MinValue) ? "First --" : diagFirstVolBarTime.ToString("MM-dd HH:mm")) + "  →  " + ((diagLastVolBarTime == DateTime.MinValue) ? "Last --" : diagLastVolBarTime.ToString("HH:mm"));
			string text3 = ((nativeSessionBegin == DateTime.MinValue) ? "Session --" : nativeSessionBegin.ToString("MM-dd HH:mm")) + "  →  " + ((nativeSessionEnd == DateTime.MinValue) ? "--" : nativeSessionEnd.ToString("MM-dd HH:mm"));
			string text4 = ((CandleFootprintBarMetric == 2) ? "Delta Abs" : "Volume");
			float num10 = num8 + num3 + 6f * dY;
			((SharpDX.Direct2D1.Brush)_bDim).Opacity = 0.88f;
			T("Source: Native Volumetric BidAsk", num7 + num5, num10, _fSmall, _bDim, num2 - num5 * 2f, num4);
			num10 += num4;
			T("Bars: " + diagVolBarsSummed + "   Rows: " + num + "   Tick/Row: " + EffectiveTicksPerRow(), num7 + num5, num10, _fSmall, _bDim, num2 - num5 * 2f, num4);
			num10 += num4;
			T("Bid: " + FQ(diagTotalBid) + "   Ask: " + FQ(diagTotalAsk) + "   Vol: " + FQ(v), num7 + num5, num10, _fSmall, _bDim, num2 - num5 * 2f, num4);
			num10 += num4;
			SolidColorBrush val = ((num9 >= 0) ? _bDeltaPos : _bDeltaNeg);
			((SharpDX.Direct2D1.Brush)val).Opacity = 0.95f;
			T("Session Δ: " + FQ(num9) + "   Max Row Δ: " + FQ(sessionMaxDeltaAbs), num7 + num5, num10, _fSmall, val, num2 - num5 * 2f, num4);
			num10 += num4;
			((SharpDX.Direct2D1.Brush)_bPoc).Opacity = 0.95f;
			T("D-VPOC: " + text, num7 + num5, num10, _fSmall, _bPoc, num2 - num5 * 2f, num4);
			num10 += num4;
			((SharpDX.Direct2D1.Brush)_bDim).Opacity = 0.88f;
			T("Session: " + text3, num7 + num5, num10, _fSmall, _bDim, num2 - num5 * 2f, num4);
			num10 += num4;
			T("Loaded: " + text2, num7 + num5, num10, _fSmall, _bDim, num2 - num5 * 2f, num4);
			num10 += num4;
			T("FP Metric: " + text4 + "   Auto Width: " + (AutoResizeCandleFootprint ? "ON" : "OFF"), num7 + num5, num10, _fSmall, _bDim, num2 - num5 * 2f, num4);
		}

		private void DrawControlPanel(float W, float H, float dX, float dY)
		{
			//IL_024d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0252: Unknown result type (might be due to invalid IL or missing references)
			//IL_026e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0295: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_02cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0123: Unknown result type (might be due to invalid IL or missing references)
			//IL_0143: Unknown result type (might be due to invalid IL or missing references)
			//IL_0166: Unknown result type (might be due to invalid IL or missing references)
			//IL_02fa: Unknown result type (might be due to invalid IL or missing references)
			_panelHits.Clear();
			float num = (float)ControlPanelWidth * dX;
			float num2 = ClampF((float)ControlPanelX * dX, 0f, Math.Max(0f, W - num - 2f * dX));
			float num3 = ClampF((float)ControlPanelY * dY, 0f, Math.Max(0f, H - 24f * dY));
			float num4 = 21f * dY;
			float num5 = 18f * dY;
			float num6 = 8f * dX;
			float labelW = 112f * dX;
			float btnW = 22f * dX;
			float valW = 48f * dX;
			if (_panelCollapsed)
			{
				float num7 = 24f * dY;
				RectangleF val = default(RectangleF);
				val = new RectangleF(num2, num3, Math.Min(num, 230f * dX), num7);
				_controlPanelRect = val;
				_controlPanelDragHandleRect = new RectangleF(num2, num3, Math.Max(1f, val.Width - 26f * dX), num7);
				_panelHits["PanelCollapse"] = new RectangleF(num2 + val.Width - 24f * dX, num3, 24f * dX, num7);
				((SharpDX.Direct2D1.Brush)_bPanel).Opacity = 0.82f;
				base.RenderTarget.FillRectangle(val, (SharpDX.Direct2D1.Brush)(object)_bPanel);
				((SharpDX.Direct2D1.Brush)_bGrid).Opacity = 0.7f;
				base.RenderTarget.DrawRectangle(val, (SharpDX.Direct2D1.Brush)(object)_bGrid, 0.8f * dX);
				((SharpDX.Direct2D1.Brush)_bText).Opacity = 1f;
				T("[+] ILT-DEV FOTPRINT", num2 + 6f * dX, num3 + 4f * dY, _fBold, _bText, val.Width - 34f * dX, num7);
				Button("PanelCollapse", "+", num2 + val.Width - 24f * dX, num3 + 2f * dY, 20f * dX, num7 - 4f * dY);
				PersistControlPanelLocation(num2, num3, dX, dY);
				return;
			}
			float val2 = 18f * dY + num5 * 5f + num4 * 38f + num6 * 4f;
			float num8 = Math.Min(H - num3 - 8f * dY, val2);
			_controlPanelRect = new RectangleF(num2, num3, num, num8);
			((SharpDX.Direct2D1.Brush)_bPanel).Opacity = 0.88f;
			base.RenderTarget.FillRectangle(_controlPanelRect, (SharpDX.Direct2D1.Brush)(object)_bPanel);
			((SharpDX.Direct2D1.Brush)_bGrid).Opacity = 0.7f;
			base.RenderTarget.DrawRectangle(_controlPanelRect, (SharpDX.Direct2D1.Brush)(object)_bGrid, 0.8f * dX);
			float num9 = num3 + 6f * dY;
			float num10 = num4 + 3f * dY;
			_controlPanelDragHandleRect = new RectangleF(num2, num3, num, num10);
			((SharpDX.Direct2D1.Brush)_bGrid).Opacity = (_controlPanelDragging ? 0.58f : 0.32f);
			base.RenderTarget.FillRectangle(_controlPanelDragHandleRect, (SharpDX.Direct2D1.Brush)(object)_bGrid);
			((SharpDX.Direct2D1.Brush)_bText).Opacity = 1f;
			T("ILT-DEV-FOTPRINT  —  drag", num2 + num6, num9, _fBold, _bText, num - num6 * 2f - 28f * dX, num4);
			Button("PanelCollapse", "-", num2 + num - num6 - 22f * dX, num9, 22f * dX, num4);
			num9 += num4 + 3f * dY;
			PersistControlPanelLocation(num2, num3, dX, dY);
			Section("ENGINE / SESSION", num2 + num6, ref num9, num - num6 * 2f, num5);
			Status("Native Session BidAsk", num2 + num6, ref num9, num - num6 * 2f, num4);
			Stepper("Ticks / Row", "TicksPerRow", TicksPerRow, num2 + num6, ref num9, labelW, btnW, valW, num4, dX);
			Stepper("Session Min", "SessionVolumetricMinutes", SessionVolumetricMinutes, num2 + num6, ref num9, labelW, btnW, valW, num4, dX);
			Stepper("Min Volume", "MinVolumeToDisplay", MinVolumeToDisplay, num2 + num6, ref num9, labelW, btnW, valW, num4, dX);
			Stepper("Candle VA %", "ValueAreaPct", ValueAreaPct, num2 + num6, ref num9, labelW, btnW, valW, num4, dX);
			Section("LADDER IMBALANCE", num2 + num6, ref num9, num - num6 * 2f, num5);
			Toggle("Ladder Imb Paint", "ShowSessionLadderImbalances", ShowSessionLadderImbalances, num2 + num6, ref num9, num - num6 * 2f, num4);
			Stepper("Ladder Imb %", "LadderImbalancePct", LadderImbalancePct, num2 + num6, ref num9, labelW, btnW, valW, num4, dX);
			Stepper("Ladder Min", "LadderMinImbalance", LadderMinImbalance, num2 + num6, ref num9, labelW, btnW, valW, num4, dX);
			Stepper("Ladder Stack", "LadderImbalanceStackRows", LadderImbalanceStackRows, num2 + num6, ref num9, labelW, btnW, valW, num4, dX);
			Section("CANDLE IMBALANCE", num2 + num6, ref num9, num - num6 * 2f, num5);
			Toggle("Candle Imb Paint", "ShowCandleImbalances", ShowCandleImbalances, num2 + num6, ref num9, num - num6 * 2f, num4);
			Toggle("Extended Imb Lines", "ShowExtendedImbalanceLines", ShowExtendedImbalanceLines, num2 + num6, ref num9, num - num6 * 2f, num4);
			Stepper("Ext Line Width", "ExtendedImbalanceLineWidth", ExtendedImbalanceLineWidth, num2 + num6, ref num9, labelW, btnW, valW, num4, dX);
			Toggle("New IMB Flash", "ShowNewImbalanceAnimation", ShowNewImbalanceAnimation, num2 + num6, ref num9, num - num6 * 2f, num4);
			Stepper("Candle Imb %", "CandleImbalancePct", CandleImbalancePct, num2 + num6, ref num9, labelW, btnW, valW, num4, dX);
			Stepper("Candle Min", "CandleMinImbalance", CandleMinImbalance, num2 + num6, ref num9, labelW, btnW, valW, num4, dX);
			Stepper("Candle Stack", "CandleImbalanceStackRows", CandleImbalanceStackRows, num2 + num6, ref num9, labelW, btnW, valW, num4, dX);
			Section("LAYOUT", num2 + num6, ref num9, num - num6 * 2f, num5);
			Toggle("Session Ladder", "ShowSessionLadder", ShowSessionLadder, num2 + num6, ref num9, num - num6 * 2f, num4);
			Toggle("Session VA", "ShowSessionValueArea", ShowSessionValueArea, num2 + num6, ref num9, num - num6 * 2f, num4);
			Toggle("Ladder Delta Profile", "ShowSessionDeltaProfile", ShowSessionDeltaProfile, num2 + num6, ref num9, num - num6 * 2f, num4);
			Toggle("Ladder Volume Profile", "ShowSessionVolumeProfile", ShowSessionVolumeProfile, num2 + num6, ref num9, num - num6 * 2f, num4);
			Toggle("Ladder BID x ASK", "ShowSessionBidAskColumn", ShowSessionBidAskColumn, num2 + num6, ref num9, num - num6 * 2f, num4);
			Toggle("Candle Footprint", "ShowCandleFootprint", ShowCandleFootprint, num2 + num6, ref num9, num - num6 * 2f, num4);
			Toggle((CandleFootprintBarMetric == 2) ? "Candle Delta Profile" : "Candle Volume Profile", "ShowCandleProfileHistogram", ShowCandleProfileHistogram, num2 + num6, ref num9, num - num6 * 2f, num4);
			Toggle("Candle VA", "ShowCandleValueArea", ShowCandleValueArea, num2 + num6, ref num9, num - num6 * 2f, num4);
			Toggle("Price Line", "ShowCurrentPriceLine", ShowCurrentPriceLine, num2 + num6, ref num9, num - num6 * 2f, num4);
			Toggle("Ladder Left/Right", "LadderSide", LadderSide == 2, num2 + num6, ref num9, num - num6 * 2f, num4);
			Toggle("Row Grid", "ShowSessionRowGrid", ShowSessionRowGrid, num2 + num6, ref num9, num - num6 * 2f, num4);
			Stepper("Delta Dir 1L 2C 3R", "DeltaHistogramDirection", DeltaHistogramDirection, num2 + num6, ref num9, labelW, btnW, valW, num4, dX);
			Stepper("Ladder Position %", "LadderPositionPct", LadderPositionPct, num2 + num6, ref num9, labelW, btnW, valW, num4, dX);
			Stepper("Ladder Width", "SessionLadderWidth", SessionLadderWidth, num2 + num6, ref num9, labelW, btnW, valW, num4, dX);
			Toggle("Auto FP Width", "AutoResizeCandleFootprint", AutoResizeCandleFootprint, num2 + num6, ref num9, num - num6 * 2f, num4);
			Stepper("FP Metric 1V 2D", "CandleFootprintBarMetric", CandleFootprintBarMetric, num2 + num6, ref num9, labelW, btnW, valW, num4, dX);
			Stepper("FP Width", "CandleFootprintWidth", CandleFootprintWidth, num2 + num6, ref num9, labelW, btnW, valW, num4, dX);
			Stepper("FP Offset", "CandleFootprintOffset", CandleFootprintOffset, num2 + num6, ref num9, labelW, btnW, valW, num4, dX);
			Stepper("IMB Flash Opacity", "NewImbalanceAnimationOpacity", NewImbalanceAnimationOpacity, num2 + num6, ref num9, labelW, btnW, valW, num4, dX);
			Stepper("IMB Flash Width", "NewImbalanceAnimationLineWidth", NewImbalanceAnimationLineWidth, num2 + num6, ref num9, labelW, btnW, valW, num4, dX);
			Stepper("IMB Flash MS", "NewImbalanceAnimationMs", NewImbalanceAnimationMs, num2 + num6, ref num9, labelW, btnW, valW, num4, dX);
			((SharpDX.Direct2D1.Brush)_bDim).Opacity = 0.85f;
			T("- / + edits. Full settings in indicator properties.", num2 + num6, num9 + 3f * dY, _fSmall, _bDim, num - num6 * 2f, num4 * 2f);
		}

		private void Status(string label, float x, ref float y, float w, float h)
		{
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			RectangleF val = default(RectangleF);
			val = new RectangleF(x, y, w, h);
			((SharpDX.Direct2D1.Brush)_bDeltaPos).Opacity = 0.35f;
			base.RenderTarget.FillRectangle(val, (SharpDX.Direct2D1.Brush)(object)_bDeltaPos);
			((SharpDX.Direct2D1.Brush)_bText).Opacity = 1f;
			T("■ " + label, x + 5f, y + 3f, _fSmall, _bText, w - 10f, h);
			y += h + 5f;
		}

		private void Section(string text, float x, ref float y, float w, float h)
		{
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			((SharpDX.Direct2D1.Brush)_bGrid).Opacity = 0.45f;
			base.RenderTarget.FillRectangle(new RectangleF(x, y, w, h), (SharpDX.Direct2D1.Brush)(object)_bGrid);
			((SharpDX.Direct2D1.Brush)_bText).Opacity = 0.95f;
			T(text, x + 5f, y + 2f, _fSmall, _bText, w - 10f, h);
			y += h + 4f;
		}

		private void Stepper(string label, string key, int value, float x, ref float y, float labelW, float btnW, float valW, float h, float dX)
		{
			//IL_0082: Unknown result type (might be due to invalid IL or missing references)
			((SharpDX.Direct2D1.Brush)_bDim).Opacity = 0.9f;
			T(label, x, y + 3f, _fSmall, _bDim, labelW, h);
			float num = x + labelW;
			Button(key + ":-", "-", num, y, btnW, h);
			num += btnW + 4f * dX;
			((SharpDX.Direct2D1.Brush)_bGrid).Opacity = 0.35f;
			base.RenderTarget.FillRectangle(new RectangleF(num, y, valW, h), (SharpDX.Direct2D1.Brush)(object)_bGrid);
			((SharpDX.Direct2D1.Brush)_bText).Opacity = 1f;
			T(value.ToString(), num + 4f * dX, y + 3f, _fSmall, _bText, valW - 8f * dX, h);
			num += valW + 4f * dX;
			Button(key + ":+", "+", num, y, btnW, h);
			y += h + 5f * dX;
		}

		private void Toggle(string label, string key, bool on, float x, ref float y, float w, float h)
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			RectangleF val = default(RectangleF);
			val = new RectangleF(x, y, w, h);
			_panelHits[key] = val;
			SolidColorBrush val2 = (on ? _bDeltaPos : _bGrid);
			((SharpDX.Direct2D1.Brush)val2).Opacity = (on ? 0.55f : 0.25f);
			base.RenderTarget.FillRectangle(val, (SharpDX.Direct2D1.Brush)(object)val2);
			((SharpDX.Direct2D1.Brush)_bText).Opacity = 1f;
			T((on ? "■ " : "□ ") + label, x + 5f, y + 3f, _fSmall, _bText, w - 10f, h);
			y += h + 5f;
		}

		private void Button(string key, string label, float x, float y, float w, float h)
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			RectangleF val = default(RectangleF);
			val = new RectangleF(x, y, w, h);
			_panelHits[key] = val;
			((SharpDX.Direct2D1.Brush)_bGrid).Opacity = 0.45f;
			base.RenderTarget.FillRectangle(val, (SharpDX.Direct2D1.Brush)(object)_bGrid);
			((SharpDX.Direct2D1.Brush)_bText).Opacity = 1f;
			T(label, x + w * 0.35f, y + 3f, _fSmall, _bText, w, h);
		}

		private void PanelMouseDown(object sender, MouseButtonEventArgs e)
		{
			//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			if (base.ChartControl == null || UseWpfPanel)
			{
				return;
			}
			float x;
			float y;
			GetMousePointInRenderSpace(e, out x, out y);
			if (_panelHits != null && _panelHits.Count > 0)
			{
				foreach (KeyValuePair<string, RectangleF> panelHit in _panelHits)
				{
					if (Hit(panelHit.Value, x, y))
					{
						HandlePanelCommand(panelHit.Key);
						e.Handled = true;
						base.ForceRefresh();
						return;
					}
				}
			}
			if (ShowControlPanel && _controlPanelDragHandleRect.Width > 0f && Hit(_controlPanelDragHandleRect, x, y))
			{
				_controlPanelDragging = true;
				_controlPanelDragOffsetX = x - _controlPanelRect.Left;
				_controlPanelDragOffsetY = y - _controlPanelRect.Top;
				try
				{
					Mouse.Capture((IInputElement)base.ChartControl);
				}
				catch
				{
				}
				e.Handled = true;
				base.ForceRefresh();
			}
		}

		private void PanelMouseMove(object sender, MouseEventArgs e)
		{
			if (!UseWpfPanel && _controlPanelDragging && base.ChartControl != null)
			{
				float x;
				float y;
				GetMousePointInRenderSpace(e, out x, out y);
				float num = ((_lastRenderW > 1f) ? _lastRenderW : ((base.ChartPanel != null) ? ((float)((FrameworkElement)(object)base.ChartPanel).ActualWidth * _lastDX) : 1f));
				float num2 = ((_lastRenderH > 1f) ? _lastRenderH : ((base.ChartPanel != null) ? ((float)((FrameworkElement)(object)base.ChartPanel).ActualHeight * _lastDY) : ((float)((FrameworkElement)(object)base.ChartControl).ActualHeight * _lastDY)));
				if (_controlPanelDragging)
				{
					float num3 = ((_controlPanelRect.Width > 0f) ? _controlPanelRect.Width : Math.Max(160f * _lastDX, (float)ControlPanelWidth * _lastDX));
					float num4 = ((_controlPanelRect.Height > 0f) ? _controlPanelRect.Height : (240f * _lastDY));
					float num5 = ClampF(x - _controlPanelDragOffsetX, 0f, Math.Max(0f, num - num3 - 2f * _lastDX));
					float num6 = ClampF(y - _controlPanelDragOffsetY, 0f, Math.Max(0f, num2 - num4 - 2f * _lastDY));
					ControlPanelX = Clamp((int)Math.Round(num5 / Math.Max(1f, _lastDX)), 0, 10000);
					ControlPanelY = Clamp((int)Math.Round(num6 / Math.Max(1f, _lastDY)), 0, 10000);
				}
				e.Handled = true;
				base.ForceRefresh();
			}
		}

		private void PanelMouseUp(object sender, MouseButtonEventArgs e)
		{
			if (!UseWpfPanel && (_controlPanelDragging || _statsDragging))
			{
				_controlPanelDragging = false;
				_statsDragging = false;
				try
				{
					Mouse.Capture(null);
				}
				catch
				{
				}
				e.Handled = true;
				base.ForceRefresh();
			}
		}

		private void PersistControlPanelLocation(float x, float y, float dX, float dY)
		{
			if (!_controlPanelDragging)
			{
				ControlPanelX = Clamp((int)Math.Round(x / Math.Max(1f, dX)), 0, 10000);
				ControlPanelY = Clamp((int)Math.Round(y / Math.Max(1f, dY)), 0, 10000);
			}
		}

		private void GetMousePointInRenderSpace(MouseEventArgs e, out float x, out float y)
		{
			Point point = ((base.ChartPanel != null) ? e.GetPosition((IInputElement)base.ChartPanel) : e.GetPosition((IInputElement)base.ChartControl));
			x = (float)point.X * _lastDX;
			y = (float)point.Y * _lastDY;
		}

		private bool Hit(RectangleF r, float x, float y)
		{
			if (x >= r.Left && x <= r.Right && y >= r.Top)
			{
				return y <= r.Bottom;
			}
			return false;
		}

		private void HandlePanelCommand(string cmd)
		{
			switch (cmd)
			{
			case "PanelCollapse":
				_panelCollapsed = !_panelCollapsed;
				break;
			case "ShowDataDiagnostics":
				ShowDataDiagnostics = false;
				break;
			case "ShowSessionLadder":
				ShowSessionLadder = !ShowSessionLadder;
				break;
			case "ShowSessionDeltaProfile":
				ShowSessionDeltaProfile = !ShowSessionDeltaProfile;
				break;
			case "ShowSessionVolumeProfile":
				ShowSessionVolumeProfile = !ShowSessionVolumeProfile;
				break;
			case "ShowSessionBidAskColumn":
				ShowSessionBidAskColumn = !ShowSessionBidAskColumn;
				break;
			case "ShowCandleFootprint":
				ShowCandleFootprint = !ShowCandleFootprint;
				break;
			case "ShowCandleProfileHistogram":
				ShowCandleProfileHistogram = !ShowCandleProfileHistogram;
				break;
			case "ShowSessionValueArea":
				ShowSessionValueArea = !ShowSessionValueArea;
				break;
			case "ShowCandleValueArea":
				ShowCandleValueArea = !ShowCandleValueArea;
				break;
			case "ShowCurrentPriceLine":
				ShowCurrentPriceLine = !ShowCurrentPriceLine;
				break;
			case "ShowCandleImbalances":
				ShowCandleImbalances = !ShowCandleImbalances;
				break;
			case "ShowSessionLadderImbalances":
				ShowSessionLadderImbalances = !ShowSessionLadderImbalances;
				break;
			case "ShowExtendedImbalanceLines":
				ShowExtendedImbalanceLines = !ShowExtendedImbalanceLines;
				break;
			case "ShowBarSummary":
				ShowBarSummary = !ShowBarSummary;
				break;
			case "ShowNewImbalanceAnimation":
				ShowNewImbalanceAnimation = !ShowNewImbalanceAnimation;
				break;
			case "AutoResizeCandleFootprint":
				AutoResizeCandleFootprint = !AutoResizeCandleFootprint;
				break;
			case "ShowBidAskText":
				ShowBidAskText = !ShowBidAskText;
				break;
			case "LadderSide":
				LadderSide = ((LadderSide != 1) ? 1 : 2);
				LadderPositionPct = LadderSide == 2 ? 0 : 100;
				break;
			case "ShowSessionRowGrid":
				ShowSessionRowGrid = !ShowSessionRowGrid;
				break;
			default:
				if (cmd.Contains(":"))
				{
					string[] array = cmd.Split(':');
					if (array.Length != 2)
					{
						return;
					}
					int dir = ((array[1] == "+") ? 1 : (-1));
					AdjustParam(array[0], dir);
				}
				break;
			}
			ForceNativeCandleMode();
			PersistRuntimeSettings();
		}

		private void AdjustParam(string key, int dir)
		{
			switch (key)
			{
			case "TicksPerRow":
				TicksPerRow = Clamp(TicksPerRow + dir, 1, 20);
				try
				{
					base.Print((object)"ILT-DEV-Footprint: TicksPerRow guardado. Presiona F5/reload para reconstruir la serie Volumetric con ese valor.");
				}
				catch
				{
				}
				break;
			case "SessionVolumetricMinutes":
				SessionVolumetricMinutes = Clamp(SessionVolumetricMinutes + dir * 30, 60, 1440);
				break;
			case "MinVolumeToDisplay":
				MinVolumeToDisplay = Clamp(MinVolumeToDisplay + dir, 0, 1000000);
				break;
			case "ValueAreaPct":
				ValueAreaPct = Clamp(ValueAreaPct + dir, 1, 99);
				break;
			case "LadderImbalancePct":
				LadderImbalancePct = Clamp(LadderImbalancePct + dir * 25, 100, 2000);
				break;
			case "LadderMinImbalance":
				LadderMinImbalance = Clamp(LadderMinImbalance + dir, 1, 100000);
				break;
			case "LadderImbalanceStackRows":
				LadderImbalanceStackRows = Clamp(LadderImbalanceStackRows + dir, 1, 5);
				break;
			case "CandleImbalancePct":
				CandleImbalancePct = Clamp(CandleImbalancePct + dir * 25, 100, 2000);
				break;
			case "CandleMinImbalance":
				CandleMinImbalance = Clamp(CandleMinImbalance + dir, 1, 100000);
				break;
			case "CandleImbalanceStackRows":
				CandleImbalanceStackRows = Clamp(CandleImbalanceStackRows + dir, 1, 5);
				break;
			case "DeltaHistogramDirection":
				DeltaHistogramDirection = Clamp(DeltaHistogramDirection + dir, 1, 3);
				break;
			case "LadderPositionPct":
				LadderPositionPct = Clamp(LadderPositionPct + dir * 5, 0, 100);
				LadderSide = LadderPositionPct <= 50 ? 2 : 1;
				break;
			case "CandleFootprintBarMetric":
				CandleFootprintBarMetric = Clamp(CandleFootprintBarMetric + dir, 1, 2);
				break;
			case "SessionLadderWidth":
				SessionLadderWidth = Clamp(SessionLadderWidth + dir * 8, 80, 480);
				break;
			case "CandleFootprintWidth":
				CandleFootprintWidth = Clamp(CandleFootprintWidth + dir * 2, 8, 100);
				break;
			case "SummaryBarHeight":
				SummaryBarHeight = Clamp(SummaryBarHeight + dir, 1, 12);
				break;
			case "SummaryGap":
				SummaryGap = Clamp(SummaryGap + dir, 0, 30);
				break;
			case "SummaryBorderWidth":
				SummaryBorderWidth = Clamp(SummaryBorderWidth + dir, 1, 5);
				break;
			case "FontSizeText":
				FontSizeText = Clamp(FontSizeText + dir, 6, 28);
				break;
			case "MinRowHeightForText":
				MinRowHeightForText = Clamp(MinRowHeightForText + dir, 1, 30);
				break;
			case "FootprintOpacity":
				FootprintOpacity = Clamp(FootprintOpacity + dir * 5, 0, 100);
				break;
			case "LadderOpacity":
				LadderOpacity = Clamp(LadderOpacity + dir * 5, 0, 100);
				break;
			case "CandleFootprintOffset":
				CandleFootprintOffset = Clamp(CandleFootprintOffset + dir, -100, 100);
				break;
			case "ExtendedImbalanceLineWidth":
				ExtendedImbalanceLineWidth = Clamp(ExtendedImbalanceLineWidth + dir, 1, 10);
				break;
			case "ExtendedImbalanceLookbackBars":
				ExtendedImbalanceLookbackBars = Clamp(ExtendedImbalanceLookbackBars + dir * 100, 50, 10000);
				break;
			case "NewImbalanceAnimationOpacity":
				NewImbalanceAnimationOpacity = Clamp(NewImbalanceAnimationOpacity + dir * 5, 0, 100);
				break;
			case "NewImbalanceAnimationLineWidth":
				NewImbalanceAnimationLineWidth = Clamp(NewImbalanceAnimationLineWidth + dir, 1, 14);
				break;
			case "NewImbalanceAnimationMs":
				NewImbalanceAnimationMs = Clamp(NewImbalanceAnimationMs + dir * 100, 200, 5000);
				break;
			case "VolumeProfileWidth":
				VolumeProfileWidth = Clamp(VolumeProfileWidth + dir * 5, 40, 220);
				break;
			case "VolumeProfileDirection":
				VolumeProfileDirection = Clamp(VolumeProfileDirection + dir, 1, 3);
				break;
			case "VolumeProfileOpacity":
				VolumeProfileOpacity = Clamp(VolumeProfileOpacity + dir * 5, 0, 100);
				break;
			}
			ForceNativeCandleMode();
			PersistRuntimeSettings();
		}

		private int Clamp(int value, int min, int max)
		{
			return Math.Max(min, Math.Min(max, value));
		}

		private float ClampF(float value, float min, float max)
		{
			return Math.Max(min, Math.Min(max, value));
		}

		private void ForceNativeCandleMode()
		{
			FootprintCandleMode = 1;
			ShowBlockCandles = false;
			HideNativeCandlesWithBlocks = false;
		}

		private string GetRuntimeSettingsPath()
		{
			//IL_0083: Unknown result type (might be due to invalid IL or missing references)
			if (!string.IsNullOrEmpty(_runtimeSettingsPath))
			{
				return _runtimeSettingsPath;
			}
			string empty = string.Empty;
			try
			{
				empty = Globals.UserDataDir;
			}
			catch
			{
				empty = string.Empty;
			}
			if (string.IsNullOrEmpty(empty))
			{
				empty = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
			}
			string path = Path.Combine(empty, "templates", "Indicator", "ILT_DEV_SUITE_RUNTIME");
			string value = "Instrument";
			string value2 = "Period";
			try
			{
				if (base.Instrument != null)
				{
					value = base.Instrument.FullName;
				}
			}
			catch
			{
			}
			try
			{
				if (base.BarsPeriod != null)
				{
					value2 = ((object)base.BarsPeriod.BarsPeriodType).ToString() + "_" + base.BarsPeriod.Value;
				}
			}
			catch
			{
			}
			string path2 = "ILT_DEV_FOOTPRINT_OFB_V1_" + SanitizeFileNamePart(value) + "_" + SanitizeFileNamePart(value2) + ".ini";
			_runtimeSettingsPath = Path.Combine(path, path2);
			return _runtimeSettingsPath;
		}

		private string SanitizeFileNamePart(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return "NA";
			}
			char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
			char[] array = value.ToCharArray();
			for (int i = 0; i < array.Length; i++)
			{
				if (Array.IndexOf(invalidFileNameChars, array[i]) >= 0 || array[i] == '|' || array[i] == ':' || array[i] == '/' || array[i] == '\\')
				{
					array[i] = '_';
				}
			}
			return new string(array).Replace(" ", "_");
		}

		private void LoadRuntimeSettings()
		{
			if (_runtimeSettingsLoaded)
			{
				return;
			}
			_runtimeSettingsLoaded = true;
			try
			{
				string runtimeSettingsPath = GetRuntimeSettingsPath();
				if (string.IsNullOrEmpty(runtimeSettingsPath) || !File.Exists(runtimeSettingsPath))
				{
					ForceNativeCandleMode();
					return;
				}
				string[] array = File.ReadAllLines(runtimeSettingsPath);
				string[] array2 = array;
				foreach (string text in array2)
				{
					if (!string.IsNullOrWhiteSpace(text) && !text.TrimStart().StartsWith("#"))
					{
						int num = text.IndexOf('=');
						if (num > 0)
						{
							string key = text.Substring(0, num).Trim();
							string value = text.Substring(num + 1).Trim();
							ApplyRuntimeSetting(key, value);
						}
					}
				}
			}
			catch (Exception ex)
			{
				try
				{
					base.Print((object)("ILT-DEV-Footprint runtime settings load skipped: " + ex.Message));
				}
				catch
				{
				}
			}
			ForceNativeCandleMode();
			if (PanelXW >= 0.0 && PanelYW >= 0.0)
			{
				_userMovedPanel = true;
			}
		}

		private void PersistRuntimeSettings()
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Invalid comparison between Unknown and I4
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Invalid comparison between Unknown and I4
			if ((int)base.State == 1 || (int)base.State == 2)
			{
				return;
			}
			try
			{
				ForceNativeCandleMode();
				string runtimeSettingsPath = GetRuntimeSettingsPath();
				string directoryName = Path.GetDirectoryName(runtimeSettingsPath);
				if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
				{
					Directory.CreateDirectory(directoryName);
				}
				List<string> list = new List<string>();
				list.Add("# ILT_DEV_FOOTPRINT_OFB runtime menu settings");
				list.Add("# Auto-generated. These values make WPF panel edits survive F5/reload.");
				AddSetting(list, "TicksPerRow", TicksPerRow);
				AddSetting(list, "SessionVolumetricMinutes", SessionVolumetricMinutes);
				AddSetting(list, "VisualMode", (int)VisualMode);
				AddSetting(list, "MinVolumeToDisplay", MinVolumeToDisplay);
				AddSetting(list, "ValueAreaPct", ValueAreaPct);
				AddSetting(list, "LadderImbalancePct", LadderImbalancePct);
				AddSetting(list, "LadderMinImbalance", LadderMinImbalance);
				AddSetting(list, "LadderImbalanceStackRows", LadderImbalanceStackRows);
				AddSetting(list, "CandleImbalancePct", CandleImbalancePct);
				AddSetting(list, "CandleMinImbalance", CandleMinImbalance);
				AddSetting(list, "CandleImbalanceStackRows", CandleImbalanceStackRows);
				AddSetting(list, "ShowSessionLadder", ShowSessionLadder);
				AddSetting(list, "ShowSessionDeltaProfile", ShowSessionDeltaProfile);
				AddSetting(list, "ShowSessionVolumeProfile", ShowSessionVolumeProfile);
				AddSetting(list, "ShowSessionBidAskColumn", ShowSessionBidAskColumn);
				AddSetting(list, "ShowCandleFootprint", ShowCandleFootprint);
				AddSetting(list, "ShowCandleProfileHistogram", ShowCandleProfileHistogram);
				AddSetting(list, "ShowCandleValueArea", ShowCandleValueArea);
				AddSetting(list, "ShowSessionValueArea", ShowSessionValueArea);
				AddSetting(list, "ShowCurrentPriceLine", ShowCurrentPriceLine);
				AddSetting(list, "ShowCandleImbalances", ShowCandleImbalances);
				AddSetting(list, "ShowSessionLadderImbalances", ShowSessionLadderImbalances);
				AddSetting(list, "ShowExtendedImbalanceLines", ShowExtendedImbalanceLines);
				AddSetting(list, "ShowBarSummary", ShowBarSummary);
				AddSetting(list, "ShowNewImbalanceAnimation", ShowNewImbalanceAnimation);
				AddSetting(list, "ShowBidAskText", ShowBidAskText);
				AddSetting(list, "LadderSide", LadderSide);
				AddSetting(list, "LadderPositionPct", LadderPositionPct);
				AddSetting(list, "ShowSessionRowGrid", ShowSessionRowGrid);
				AddSetting(list, "DeltaHistogramDirection", DeltaHistogramDirection);
				AddSetting(list, "SessionLadderWidth", SessionLadderWidth);
				AddSetting(list, "AutoResizeCandleFootprint", AutoResizeCandleFootprint);
				AddSetting(list, "CandleFootprintBarMetric", CandleFootprintBarMetric);
				AddSetting(list, "CandleFootprintWidth", CandleFootprintWidth);
				AddSetting(list, "SummaryBarHeight", SummaryBarHeight);
				AddSetting(list, "SummaryGap", SummaryGap);
				AddSetting(list, "SummaryBorderWidth", SummaryBorderWidth);
				AddSetting(list, "FontSizeText", FontSizeText);
				AddSetting(list, "MinRowHeightForText", MinRowHeightForText);
				AddSetting(list, "FootprintOpacity", FootprintOpacity);
				AddSetting(list, "LadderOpacity", LadderOpacity);
				AddSetting(list, "CandleFootprintOffset", CandleFootprintOffset);
				AddSetting(list, "ExtendedImbalanceLineWidth", ExtendedImbalanceLineWidth);
				AddSetting(list, "ExtendedImbalanceLookbackBars", ExtendedImbalanceLookbackBars);
				AddSetting(list, "NewImbalanceAnimationOpacity", NewImbalanceAnimationOpacity);
				AddSetting(list, "NewImbalanceAnimationLineWidth", NewImbalanceAnimationLineWidth);
				AddSetting(list, "NewImbalanceAnimationMs", NewImbalanceAnimationMs);
				AddSetting(list, "VolumeProfileWidth", VolumeProfileWidth);
				AddSetting(list, "VolumeProfileDirection", VolumeProfileDirection);
				AddSetting(list, "VolumeProfileOpacity", VolumeProfileOpacity);
				AddSetting(list, "BidTextColor", BrushToString(BidTextColor));
				AddSetting(list, "AskTextColor", BrushToString(AskTextColor));
				AddSetting(list, "DeltaPositiveColor", BrushToString(DeltaPositiveColor));
				AddSetting(list, "DeltaNegativeColor", BrushToString(DeltaNegativeColor));
				AddSetting(list, "BuyImbalanceColor", BrushToString(BuyImbalanceColor));
				AddSetting(list, "SellImbalanceColor", BrushToString(SellImbalanceColor));
				AddSetting(list, "PanelXW", PanelXW);
				AddSetting(list, "PanelYW", PanelYW);
				AddSetting(list, "PanelMinimizedPersist", PanelMinimizedPersist);
				AddSetting(list, "FootprintCandleMode", 1);
				AddSetting(list, "ShowBlockCandles", false);
				File.WriteAllLines(runtimeSettingsPath, list.ToArray());
			}
			catch (Exception ex)
			{
				try
				{
					base.Print((object)("ILT-DEV-Footprint runtime settings save skipped: " + ex.Message));
				}
				catch
				{
				}
			}
		}

		private void AddSetting(List<string> lines, string key, int value)
		{
			lines.Add(key + "=" + value.ToString(CultureInfo.InvariantCulture));
		}

		private void AddSetting(List<string> lines, string key, double value)
		{
			lines.Add(key + "=" + value.ToString(CultureInfo.InvariantCulture));
		}

		private void AddSetting(List<string> lines, string key, bool value)
		{
			lines.Add(key + "=" + (value ? "true" : "false"));
		}

		private void AddSetting(List<string> lines, string key, string value)
		{
			lines.Add(key + "=" + (value ?? string.Empty));
		}

		private bool TryParseRuntimeInt(string value, out int result)
		{
			return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
		}

		private bool TryParseRuntimeDouble(string value, out double result)
		{
			return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
		}

		private bool TryParseRuntimeBool(string value, out bool result)
		{
			result = false;
			if (string.IsNullOrEmpty(value))
			{
				return false;
			}
			value = value.Trim();
			if (value == "1")
			{
				result = true;
				return true;
			}
			if (value == "0")
			{
				result = false;
				return true;
			}
			return bool.TryParse(value, out result);
		}

		private bool TryParseRuntimeBrush(string value, out Brush result)
		{
			result = null;
			try
			{
				result = Serialize.StringToBrush(value);
				return result != null;
			}
			catch
			{
				return false;
			}
		}

		private void ApplyRuntimeSetting(string key, string value)
		{
			int result;
			bool result2;
			double result3;
			Brush result4;
			if (key == "TicksPerRow" && TryParseRuntimeInt(value, out result))
			{
				TicksPerRow = Clamp(result, 1, 20);
			}
			else if (key == "SessionVolumetricMinutes" && TryParseRuntimeInt(value, out result))
			{
				SessionVolumetricMinutes = Clamp(result, 60, 1440);
			}
			else if (key == "VisualMode" && TryParseRuntimeInt(value, out result))
			{
				VisualMode = (IltDevFootprintDisplayMode)Clamp(result, 1, 5);
			}
			else if (key == "MinVolumeToDisplay" && TryParseRuntimeInt(value, out result))
			{
				MinVolumeToDisplay = Clamp(result, 0, 1000000);
			}
			else if (key == "ValueAreaPct" && TryParseRuntimeInt(value, out result))
			{
				ValueAreaPct = Clamp(result, 1, 99);
			}
			else if (key == "LadderImbalancePct" && TryParseRuntimeInt(value, out result))
			{
				LadderImbalancePct = Clamp(result, 100, 2000);
			}
			else if (key == "LadderMinImbalance" && TryParseRuntimeInt(value, out result))
			{
				LadderMinImbalance = Clamp(result, 1, 100000);
			}
			else if (key == "LadderImbalanceStackRows" && TryParseRuntimeInt(value, out result))
			{
				LadderImbalanceStackRows = Clamp(result, 1, 5);
			}
			else if (key == "CandleImbalancePct" && TryParseRuntimeInt(value, out result))
			{
				CandleImbalancePct = Clamp(result, 100, 2000);
			}
			else if (key == "CandleMinImbalance" && TryParseRuntimeInt(value, out result))
			{
				CandleMinImbalance = Clamp(result, 1, 100000);
			}
			else if (key == "CandleImbalanceStackRows" && TryParseRuntimeInt(value, out result))
			{
				CandleImbalanceStackRows = Clamp(result, 1, 5);
			}
			else if (key == "ShowSessionLadder" && TryParseRuntimeBool(value, out result2))
			{
				ShowSessionLadder = result2;
			}
			else if (key == "ShowSessionDeltaProfile" && TryParseRuntimeBool(value, out result2))
			{
				ShowSessionDeltaProfile = result2;
			}
			else if (key == "ShowSessionVolumeProfile" && TryParseRuntimeBool(value, out result2))
			{
				ShowSessionVolumeProfile = result2;
			}
			else if (key == "ShowSessionBidAskColumn" && TryParseRuntimeBool(value, out result2))
			{
				ShowSessionBidAskColumn = result2;
			}
			else if (key == "ShowCandleFootprint" && TryParseRuntimeBool(value, out result2))
			{
				ShowCandleFootprint = result2;
			}
			else if (key == "ShowCandleProfileHistogram" && TryParseRuntimeBool(value, out result2))
			{
				ShowCandleProfileHistogram = result2;
			}
			else if (key == "ShowCandleValueArea" && TryParseRuntimeBool(value, out result2))
			{
				ShowCandleValueArea = result2;
			}
			else if (key == "ShowSessionValueArea" && TryParseRuntimeBool(value, out result2))
			{
				ShowSessionValueArea = result2;
			}
			else if (key == "ShowCurrentPriceLine" && TryParseRuntimeBool(value, out result2))
			{
				ShowCurrentPriceLine = result2;
			}
			else if (key == "ShowCandleImbalances" && TryParseRuntimeBool(value, out result2))
			{
				ShowCandleImbalances = result2;
			}
			else if (key == "ShowSessionLadderImbalances" && TryParseRuntimeBool(value, out result2))
			{
				ShowSessionLadderImbalances = result2;
			}
			else if (key == "ShowExtendedImbalanceLines" && TryParseRuntimeBool(value, out result2))
			{
				ShowExtendedImbalanceLines = result2;
			}
			else if (key == "ShowBarSummary" && TryParseRuntimeBool(value, out result2))
			{
				ShowBarSummary = result2;
			}
			else if (key == "ShowNewImbalanceAnimation" && TryParseRuntimeBool(value, out result2))
			{
				ShowNewImbalanceAnimation = result2;
			}
			else if (key == "ShowBidAskText" && TryParseRuntimeBool(value, out result2))
			{
				ShowBidAskText = result2;
			}
			else if (key == "LadderSide" && TryParseRuntimeInt(value, out result))
			{
				LadderSide = Clamp(result, 1, 2);
				LadderPositionPct = LadderSide == 2 ? 0 : 100;
			}
			else if (key == "LadderPositionPct" && TryParseRuntimeInt(value, out result))
			{
				LadderPositionPct = Clamp(result, 0, 100);
				LadderSide = LadderPositionPct <= 50 ? 2 : 1;
			}
			else if (key == "ShowSessionRowGrid" && TryParseRuntimeBool(value, out result2))
			{
				ShowSessionRowGrid = result2;
			}
			else if (key == "DeltaHistogramDirection" && TryParseRuntimeInt(value, out result))
			{
				DeltaHistogramDirection = Clamp(result, 1, 3);
			}
			else if (key == "SessionLadderWidth" && TryParseRuntimeInt(value, out result))
			{
				SessionLadderWidth = Clamp(result, 80, 480);
			}
			else if (key == "AutoResizeCandleFootprint" && TryParseRuntimeBool(value, out result2))
			{
				AutoResizeCandleFootprint = result2;
			}
			else if (key == "CandleFootprintBarMetric" && TryParseRuntimeInt(value, out result))
			{
				CandleFootprintBarMetric = Clamp(result, 1, 2);
			}
			else if (key == "CandleFootprintWidth" && TryParseRuntimeInt(value, out result))
			{
				CandleFootprintWidth = Clamp(result, 8, 100);
			}
			else if (key == "SummaryBarHeight" && TryParseRuntimeInt(value, out result))
			{
				SummaryBarHeight = Clamp(result, 1, 12);
			}
			else if (key == "SummaryGap" && TryParseRuntimeInt(value, out result))
			{
				SummaryGap = Clamp(result, 0, 30);
			}
			else if (key == "SummaryBorderWidth" && TryParseRuntimeInt(value, out result))
			{
				SummaryBorderWidth = Clamp(result, 1, 5);
			}
			else if (key == "FontSizeText" && TryParseRuntimeInt(value, out result))
			{
				FontSizeText = Clamp(result, 6, 28);
			}
			else if (key == "MinRowHeightForText" && TryParseRuntimeInt(value, out result))
			{
				MinRowHeightForText = Clamp(result, 1, 30);
			}
			else if (key == "FootprintOpacity" && TryParseRuntimeInt(value, out result))
			{
				FootprintOpacity = Clamp(result, 0, 100);
			}
			else if (key == "LadderOpacity" && TryParseRuntimeInt(value, out result))
			{
				LadderOpacity = Clamp(result, 0, 100);
			}
			else if (key == "CandleFootprintOffset" && TryParseRuntimeInt(value, out result))
			{
				CandleFootprintOffset = Clamp(result, -100, 100);
			}
			else if (key == "ExtendedImbalanceLineWidth" && TryParseRuntimeInt(value, out result))
			{
				ExtendedImbalanceLineWidth = Clamp(result, 1, 10);
			}
			else if (key == "ExtendedImbalanceLookbackBars" && TryParseRuntimeInt(value, out result))
			{
				ExtendedImbalanceLookbackBars = Clamp(result, 50, 10000);
			}
			else if (key == "NewImbalanceAnimationOpacity" && TryParseRuntimeInt(value, out result))
			{
				NewImbalanceAnimationOpacity = Clamp(result, 0, 100);
			}
			else if (key == "NewImbalanceAnimationLineWidth" && TryParseRuntimeInt(value, out result))
			{
				NewImbalanceAnimationLineWidth = Clamp(result, 1, 14);
			}
			else if (key == "NewImbalanceAnimationMs" && TryParseRuntimeInt(value, out result))
			{
				NewImbalanceAnimationMs = Clamp(result, 200, 5000);
			}
			else if (key == "VolumeProfileWidth" && TryParseRuntimeInt(value, out result))
			{
				VolumeProfileWidth = Clamp(result, 40, 220);
			}
			else if (key == "VolumeProfileDirection" && TryParseRuntimeInt(value, out result))
			{
				VolumeProfileDirection = Clamp(result, 1, 3);
			}
			else if (key == "VolumeProfileOpacity" && TryParseRuntimeInt(value, out result))
			{
				VolumeProfileOpacity = Clamp(result, 0, 100);
			}
			else if (key == "BidTextColor" && TryParseRuntimeBrush(value, out result4))
			{
				BidTextColor = result4;
			}
			else if (key == "AskTextColor" && TryParseRuntimeBrush(value, out result4))
			{
				AskTextColor = result4;
			}
			else if (key == "DeltaPositiveColor" && TryParseRuntimeBrush(value, out result4))
			{
				DeltaPositiveColor = result4;
			}
			else if (key == "DeltaNegativeColor" && TryParseRuntimeBrush(value, out result4))
			{
				DeltaNegativeColor = result4;
			}
			else if (key == "BuyImbalanceColor" && TryParseRuntimeBrush(value, out result4))
			{
				BuyImbalanceColor = result4;
			}
			else if (key == "SellImbalanceColor" && TryParseRuntimeBrush(value, out result4))
			{
				SellImbalanceColor = result4;
			}
			else if (key == "PanelXW" && TryParseRuntimeDouble(value, out result3))
			{
				PanelXW = result3;
			}
			else if (key == "PanelYW" && TryParseRuntimeDouble(value, out result3))
			{
				PanelYW = result3;
			}
			else if (key == "PanelMinimizedPersist" && TryParseRuntimeBool(value, out result2))
			{
				PanelMinimizedPersist = result2;
			}
		}

		private void DrawHeaderCell(string txt, float x, float y, float w, float h)
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			((SharpDX.Direct2D1.Brush)_bGrid).Opacity = 0.25f;
			base.RenderTarget.FillRectangle(new RectangleF(x, y, w, h), (SharpDX.Direct2D1.Brush)(object)_bGrid);
			((SharpDX.Direct2D1.Brush)_bDim).Opacity = 0.95f;
			T(txt, x + 3f, y + 3f, _fSmall, _bDim, w - 6f, h - 4f);
		}

		private void DrawVLine(float x, float h, float dX)
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			((SharpDX.Direct2D1.Brush)_bGrid).Opacity = 0.55f;
			base.RenderTarget.DrawLine(new Vector2(x, 0f), new Vector2(x, h), (SharpDX.Direct2D1.Brush)(object)_bGrid, 0.8f * dX);
		}

		private void DrawBidAskText(long bid, long ask, bool sellImb, bool buyImb, float x, float y, float w, float h)
		{
			string text = FQ(bid);
			string text2 = FQ(ask);
			string text3 = " x ";
			float num = (float)FontSizeText * 0.62f * Math.Max(_lastDX, _lastDY);
			float num2 = (float)(text.Length + text3.Length + text2.Length) * num;
			float num3 = x + Math.Max(2f, (w - num2) * 0.5f);
			SolidColorBrush val = (sellImb ? _bImbSell : _bNeutral);
			SolidColorBrush val2 = (buyImb ? _bImbBuy : _bNeutral);
			((SharpDX.Direct2D1.Brush)val).Opacity = (sellImb ? 1f : 0.78f);
			((SharpDX.Direct2D1.Brush)val2).Opacity = (buyImb ? 1f : 0.78f);
			((SharpDX.Direct2D1.Brush)_bDim).Opacity = 0.7f;
			T(text, num3, y, _fText, val, (float)text.Length * num + 4f, h);
			T(text3, num3 + (float)text.Length * num, y, _fText, _bDim, (float)text3.Length * num + 4f, h);
			T(text2, num3 + (float)(text.Length + text3.Length) * num, y, _fText, val2, (float)text2.Length * num + 4f, h);
		}

		private void T(string txt, float x, float y, TextFormat fmt, SolidColorBrush br, float w, float h)
		{
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			if (!string.IsNullOrEmpty(txt) && fmt != null && br != null)
			{
				base.RenderTarget.DrawText(txt, fmt, new RectangleF(x, y, Math.Max(1f, w), Math.Max(1f, h)), (SharpDX.Direct2D1.Brush)(object)br);
			}
		}

		private int EffectiveTicksPerRow()
		{
			return Math.Max(1, (_configuredTicksPerRow > 0) ? _configuredTicksPerRow : TicksPerRow);
		}

		private int PriceToKey(double price)
		{
			int num = (int)Math.Round(price / _tick, MidpointRounding.AwayFromZero);
			int num2 = EffectiveTicksPerRow();
			return (int)Math.Floor((double)num / (double)num2) * num2;
		}

		private double KeyToPrice(int key)
		{
			return base.Instrument.MasterInstrument.RoundToTickSize((double)key * _tick);
		}

		private double RowPriceStep()
		{
			return Math.Max(_tick, _tick * (double)EffectiveTicksPerRow());
		}

		private int MapVolBarToPrimaryBar(int volBarIndex, DateTime t, double close)
		{
			//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f7: Invalid comparison between Unknown and I4
			int num = -1;
			try
			{
				if (base.BarsArray != null && base.BarsArray.Length > 0)
				{
					num = base.BarsArray[0].Count - 1;
				}
			}
			catch
			{
				num = -1;
			}
			if (num < 0)
			{
				return -1;
			}
			if (volBarIndex >= 0 && volBarIndex <= num)
			{
				try
				{
					if (base.Times[0].GetValueAt(volBarIndex) == t)
					{
						_volToPrimaryBarOffset = 0;
						return volBarIndex;
					}
				}
				catch
				{
				}
			}
			if (_volToPrimaryBarOffset != int.MinValue && _volToPrimaryBarOffset != 0)
			{
				int num2 = volBarIndex + _volToPrimaryBarOffset;
				if (num2 >= 0 && num2 <= num)
				{
					try
					{
						if (base.Times[0].GetValueAt(num2) == t)
						{
							return num2;
						}
					}
					catch
					{
					}
				}
			}
			int num3 = -1;
			try
			{
				num3 = base.BarsArray[0].GetBar(t);
			}
			catch
			{
				num3 = -1;
			}
			if (num3 >= 0 && num3 <= num)
			{
				try
				{
					if (base.Times[0].GetValueAt(num3) == t)
					{
						_volToPrimaryBarOffset = num3 - volBarIndex;
						return num3;
					}
				}
				catch
				{
				}
			}
			try
			{
				if ((int)base.State == 7 && t > base.Times[0].GetValueAt(num))
				{
					return -1;
				}
			}
			catch
			{
			}
			return GetPrimaryBarIndex(t, close);
		}

		private int GetPrimaryBarIndex(DateTime time, double price)
		{
			//IL_009f: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a5: Invalid comparison between Unknown and I4
			int num = ((base.CurrentBars != null && base.CurrentBars.Length > 0) ? base.CurrentBars[0] : (-1));
			if (num < 0)
			{
				return -1;
			}
			int num2 = num;
			try
			{
				if (base.BarsArray != null && base.BarsArray.Length > 0)
				{
					num2 = Math.Max(0, base.BarsArray[0].Count - 1);
				}
			}
			catch
			{
			}
			int num3 = -1;
			try
			{
				num3 = base.BarsArray[0].GetBar(time);
			}
			catch
			{
				num3 = num;
			}
			if (num3 < 0)
			{
				num3 = num;
			}
			if (num3 > num2)
			{
				num3 = num2;
			}
			try
			{
				if (base.Times[0].GetValueAt(num3) == time)
				{
					return num3;
				}
			}
			catch
			{
			}
			int num4 = 8;
			int num5 = (((int)base.State == 7) ? 4 : 120);
			int num6 = Math.Max(0, num3 - num4);
			int num7 = Math.Min(num2, num3 + num5);
			int result = num3;
			double num8 = double.MaxValue;
			double num9 = Math.Max(_tick, 1E-10);
			for (int i = num6; i <= num7; i++)
			{
				double valueAt;
				double valueAt2;
				DateTime valueAt3;
				try
				{
					valueAt = base.Highs[0].GetValueAt(i);
					valueAt2 = base.Lows[0].GetValueAt(i);
					valueAt3 = base.Times[0].GetValueAt(i);
				}
				catch
				{
					continue;
				}
				bool flag = price <= valueAt + num9 * 0.51 && price >= valueAt2 - num9 * 0.51;
				double num10 = (flag ? 0.0 : (Math.Min(Math.Abs(price - valueAt), Math.Abs(price - valueAt2)) / num9));
				double totalSeconds = (valueAt3 - time).TotalSeconds;
				double num11 = ((totalSeconds >= -0.001) ? (Math.Abs(totalSeconds) * 0.1) : (200.0 + Math.Abs(totalSeconds) * 0.02));
				double num12 = num10 * 25.0 + num11 + (double)Math.Abs(i - num3) * 0.015;
				if (flag && totalSeconds >= -0.001)
				{
					num12 -= 5.0;
				}
				if (num12 < num8)
				{
					num8 = num12;
					result = i;
				}
			}
			return result;
		}

		private int FindRetestBar(int fromBar, int toBar, double price)
		{
			double num = Math.Max(_tick, 1E-10);
			for (int i = Math.Max(0, fromBar); i <= toBar; i++)
			{
				try
				{
					double valueAt = base.Highs[0].GetValueAt(i);
					double valueAt2 = base.Lows[0].GetValueAt(i);
					if (price <= valueAt + num * 0.51 && price >= valueAt2 - num * 0.51)
					{
						return i;
					}
				}
				catch
				{
				}
			}
			return -1;
		}

		private TimeZoneInfo ResolveNewYorkTimeZone()
		{
			try
			{
				return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
			}
			catch
			{
				try
				{
					return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
				}
				catch
				{
					return TimeZoneInfo.Local;
				}
			}
		}

		private DateTime ToNewYorkTime(DateTime time)
		{
			try
			{
				if (_nyTz == null)
				{
					_nyTz = ResolveNewYorkTimeZone();
				}
				if (time.Kind == DateTimeKind.Utc)
				{
					return TimeZoneInfo.ConvertTimeFromUtc(time, _nyTz);
				}
				DateTime dateTime = ((time.Kind == DateTimeKind.Local) ? time : DateTime.SpecifyKind(time, DateTimeKind.Local));
				return TimeZoneInfo.ConvertTime(dateTime, TimeZoneInfo.Local, _nyTz);
			}
			catch
			{
				return time;
			}
		}

		private bool GetNativeSessionBounds(DateTime anchorTime, out DateTime begin, out DateTime end)
		{
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Expected O, but got Unknown
			//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_0101: Expected O, but got Unknown
			begin = DateTime.MinValue;
			end = DateTime.MinValue;
			try
			{
				if (_primarySessionIterator == null && base.BarsArray != null && base.BarsArray.Length > 0 && base.BarsArray[0] != null)
				{
					_primarySessionIterator = new SessionIterator(base.BarsArray[0]);
				}
				if (_primarySessionIterator != null)
				{
					_primarySessionIterator.GetNextSession(anchorTime, true);
					begin = _primarySessionIterator.ActualSessionBegin;
					end = _primarySessionIterator.ActualSessionEnd;
					if (begin != DateTime.MinValue && end != DateTime.MinValue && end > begin)
					{
						return true;
					}
				}
			}
			catch
			{
			}
			try
			{
				if (_nativeSessionIterator == null && base.BarsArray != null && base.BarsArray.Length > 1 && base.BarsArray[1] != null)
				{
					_nativeSessionIterator = new SessionIterator(base.BarsArray[1]);
				}
				if (_nativeSessionIterator != null)
				{
					_nativeSessionIterator.GetNextSession(anchorTime, true);
					begin = _nativeSessionIterator.ActualSessionBegin;
					end = _nativeSessionIterator.ActualSessionEnd;
					if (begin != DateTime.MinValue && end != DateTime.MinValue && end > begin)
					{
						return true;
					}
				}
			}
			catch
			{
			}
			DateTime dateTime = ((SessionTimeMode == 2) ? ToNewYorkTime(anchorTime) : anchorTime);
			DateTime dateTime2 = ((dateTime.TimeOfDay >= new TimeSpan(SessionStartHourNY, 0, 0)) ? dateTime.Date : dateTime.Date.AddDays(-1.0));
			begin = dateTime2.AddHours(SessionStartHourNY);
			end = dateTime2.AddDays(1.0).AddHours(SessionEndHourNY);
			return end > begin;
		}

		private DateTime GetNyTradingSessionDate(DateTime time)
		{
			DateTime dateTime = ((SessionTimeMode == 2) ? ToNewYorkTime(time) : time);
			TimeSpan timeOfDay = dateTime.TimeOfDay;
			if (!(timeOfDay >= new TimeSpan(SessionStartHourNY, 0, 0)))
			{
				return dateTime.Date;
			}
			return dateTime.Date.AddDays(1.0);
		}

		private bool IsInsideNyTradingSession(DateTime time)
		{
			TimeSpan timeOfDay = ((SessionTimeMode == 2) ? ToNewYorkTime(time) : time).TimeOfDay;
			if (!(timeOfDay >= new TimeSpan(SessionStartHourNY, 0, 0)))
			{
				return timeOfDay < new TimeSpan(SessionEndHourNY, 0, 0);
			}
			return true;
		}

		private string FQ(long v)
		{
			if (Math.Abs(v) >= 1000000)
			{
				return ((double)v / 1000000.0).ToString("0.##") + "M";
			}
			if (Math.Abs(v) >= 1000)
			{
				return ((double)v / 1000.0).ToString("0.#") + "K";
			}
			return v.ToString();
		}

		private int CountDecimals(double value)
		{
			string text = value.ToString(CultureInfo.InvariantCulture);
			int num = text.IndexOf('.');
			if (num >= 0)
			{
				return text.Length - num - 1;
			}
			return 0;
		}

		public override void OnRenderTargetChanged()
		{
			FreeDX();
		}

		private void EnsureDX(float dpi)
		{
			//IL_0264: Unknown result type (might be due to invalid IL or missing references)
			//IL_026e: Expected O, but got Unknown
			//IL_0282: Unknown result type (might be due to invalid IL or missing references)
			//IL_028c: Expected O, but got Unknown
			//IL_02a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b1: Expected O, but got Unknown
			string text = BuildResourceSignature(dpi);
			if (_dxOk && text == _lastResourceSignature)
			{
				return;
			}
			FreeDX();
			if (base.RenderTarget == null)
			{
				return;
			}
			try
			{
				_lastDpi = dpi;
				_bBg = NewBrush(Brushes.Black);
				_bPanel = NewBrush(PanelBackColor);
				_bGrid = NewBrush(GridColor);
				_bText = NewBrush(NeutralTextColor);
				_bNeutral = NewBrush(NeutralTextColor);
				_bDim = NewBrush(DimTextColor);
				_bBid = NewBrush(BidTextColor);
				_bAsk = NewBrush(AskTextColor);
				_bDeltaPos = NewBrush(DeltaPositiveColor);
				_bDeltaNeg = NewBrush(DeltaNegativeColor);
				_bDeltaZero = NewBrush(Brushes.White);
				_bVol = NewBrush(VolumeProfileColor);
				_bFootprintBase = NewBrush(FootprintBaseColor);
				_bPoc = NewBrush(PocColor);
				_bSummaryTotal = NewBrush(SummaryTotalColor);
				_bVaBuy = NewBrush(VaBuyColor);
				_bVaSell = NewBrush(VaSellColor);
				_bImbBuy = NewBrush(BuyImbalanceColor);
				_bImbSell = NewBrush(SellImbalanceColor);
				_bPriceUp = NewBrush(CurrentPriceUpColor);
				_bPriceDown = NewBrush(CurrentPriceDownColor);
				_bPriceNeutral = NewBrush(CurrentPriceNeutralColor);
				_bBlockBull = NewBrush(BlockBullColor);
				_bBlockBear = NewBrush(BlockBearColor);
				_bBlockNeutral = NewBrush(BlockNeutralColor);
				_bLionAcelBuy = NewBrush(LionAcelBuyColor);
				_bLionAcelSell = NewBrush(LionAcelSellColor);
				_bLionExcessBuy = NewBrush(LionExcessBuyColor);
				_bLionExcessSell = NewBrush(LionExcessSellColor);
				_bLionRotationBuy = NewBrush(LionRotationBuyColor);
				_bLionRotationSell = NewBrush(LionRotationSellColor);
				_bLionAbsorptionBuy = NewBrush(LionAbsorptionBuyColor);
				_bLionAbsorptionSell = NewBrush(LionAbsorptionSellColor);
				_fSmall = new TextFormat(Globals.DirectWriteFactory, "Consolas", (float)FontSizeSmall * dpi);
				_fText = new TextFormat(Globals.DirectWriteFactory, "Consolas", (float)FontSizeText * dpi);
				_fBold = new TextFormat(Globals.DirectWriteFactory, "Consolas", (SharpDX.DirectWrite.FontWeight)700, (SharpDX.DirectWrite.FontStyle)0, (SharpDX.DirectWrite.FontStretch)5, (float)FontSizeBold * dpi);
				_lastResourceSignature = text;
				_dxOk = true;
			}
			catch
			{
				_dxOk = false;
				FreeDX();
			}
		}

		private string BuildResourceSignature(float dpi)
		{
			return string.Join("|", dpi.ToString("0.00"), FontSizeSmall, FontSizeText, FontSizeBold, BrushToString(NeutralTextColor), BrushToString(DimTextColor), BrushToString(PanelBackColor), BrushToString(GridColor), BrushToString(BidTextColor), BrushToString(AskTextColor), BrushToString(DeltaPositiveColor), BrushToString(DeltaNegativeColor), BrushToString(VolumeProfileColor), BrushToString(FootprintBaseColor), BrushToString(PocColor), BrushToString(SummaryTotalColor), BrushToString(VaBuyColor), BrushToString(VaSellColor), BrushToString(BuyImbalanceColor), BrushToString(SellImbalanceColor), BrushToString(CurrentPriceUpColor), BrushToString(CurrentPriceDownColor), BrushToString(CurrentPriceNeutralColor), BrushToString(BlockBullColor), BrushToString(BlockBearColor), BrushToString(BlockNeutralColor), BrushToString(LionAcelBuyColor), BrushToString(LionAcelSellColor), BrushToString(LionExcessBuyColor), BrushToString(LionExcessSellColor), BrushToString(LionRotationBuyColor), BrushToString(LionRotationSellColor), BrushToString(LionAbsorptionBuyColor), BrushToString(LionAbsorptionSellColor));
		}

		private string BrushToString(Brush b)
		{
			try
			{
				return Serialize.BrushToString(b);
			}
			catch
			{
				return "";
			}
		}

		private SolidColorBrush NewBrush(Brush b)
		{
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Expected O, but got Unknown
			System.Windows.Media.SolidColorBrush solidColorBrush = b as System.Windows.Media.SolidColorBrush;
			Color color = ((solidColorBrush != null) ? solidColorBrush.Color : Colors.White);
			return new SolidColorBrush(base.RenderTarget, new Color4((float)(int)color.R / 255f, (float)(int)color.G / 255f, (float)(int)color.B / 255f, (float)(int)color.A / 255f));
		}

		private float OpacityPct(int pct)
		{
			return Math.Max(0f, Math.Min(1f, (float)pct / 100f));
		}

		private void FreeDX()
		{
			DisposeDx<SolidColorBrush>(ref _bBg);
			DisposeDx<SolidColorBrush>(ref _bPanel);
			DisposeDx<SolidColorBrush>(ref _bGrid);
			DisposeDx<SolidColorBrush>(ref _bText);
			DisposeDx<SolidColorBrush>(ref _bNeutral);
			DisposeDx<SolidColorBrush>(ref _bDim);
			DisposeDx<SolidColorBrush>(ref _bBid);
			DisposeDx<SolidColorBrush>(ref _bAsk);
			DisposeDx<SolidColorBrush>(ref _bDeltaPos);
			DisposeDx<SolidColorBrush>(ref _bDeltaNeg);
			DisposeDx<SolidColorBrush>(ref _bDeltaZero);
			DisposeDx<SolidColorBrush>(ref _bVol);
			DisposeDx<SolidColorBrush>(ref _bFootprintBase);
			DisposeDx<SolidColorBrush>(ref _bPoc);
			DisposeDx<SolidColorBrush>(ref _bSummaryTotal);
			DisposeDx<SolidColorBrush>(ref _bVaBuy);
			DisposeDx<SolidColorBrush>(ref _bVaSell);
			DisposeDx<SolidColorBrush>(ref _bImbBuy);
			DisposeDx<SolidColorBrush>(ref _bImbSell);
			DisposeDx<SolidColorBrush>(ref _bPriceUp);
			DisposeDx<SolidColorBrush>(ref _bPriceDown);
			DisposeDx<SolidColorBrush>(ref _bPriceNeutral);
			DisposeDx<SolidColorBrush>(ref _bBlockBull);
			DisposeDx<SolidColorBrush>(ref _bBlockBear);
			DisposeDx<SolidColorBrush>(ref _bBlockNeutral);
			DisposeDx<SolidColorBrush>(ref _bLionAcelBuy);
			DisposeDx<SolidColorBrush>(ref _bLionAcelSell);
			DisposeDx<SolidColorBrush>(ref _bLionExcessBuy);
			DisposeDx<SolidColorBrush>(ref _bLionExcessSell);
			DisposeDx<SolidColorBrush>(ref _bLionRotationBuy);
			DisposeDx<SolidColorBrush>(ref _bLionRotationSell);
			DisposeDx<SolidColorBrush>(ref _bLionAbsorptionBuy);
			DisposeDx<SolidColorBrush>(ref _bLionAbsorptionSell);
			foreach (KeyValuePair<int, SolidColorBrush> item in _lionExternalBrushCache)
			{
				try
				{
					if (item.Value != null)
					{
						((DisposeBase)item.Value).Dispose();
					}
				}
				catch
				{
				}
			}
			_lionExternalBrushCache.Clear();
			DisposeDx<TextFormat>(ref _fSmall);
			DisposeDx<TextFormat>(ref _fText);
			DisposeDx<TextFormat>(ref _fBold);
			_dxOk = false;
		}

		private void DisposeDx<T>(ref T obj) where T : class, IDisposable
		{
			if (obj != null)
			{
				try
				{
					obj.Dispose();
				}
				catch
				{
				}
				obj = null;
			}
		}

		private static System.Windows.Media.SolidColorBrush WpfArgb(byte a, byte r, byte g, byte b)
		{
			System.Windows.Media.SolidColorBrush solidColorBrush = new System.Windows.Media.SolidColorBrush(Color.FromArgb(a, r, g, b));
			if (solidColorBrush.CanFreeze)
			{
				solidColorBrush.Freeze();
			}
			return solidColorBrush;
		}

		private void StyleWpfToggle(Button btn, bool active)
		{
			if (btn != null)
			{
				btn.Background = (active ? WpfArgb(242, 51, 140, 217) : WpfArgb(217, 51, 51, 51));
				btn.Foreground = (active ? Brushes.White : WpfArgb(byte.MaxValue, 200, 200, 200));
			}
		}

		private Button MakeWpfToggle(string label)
		{
			Button button = new Button();
			button.Content = label;
			button.FontFamily = new FontFamily("Consolas");
			button.FontSize = 11.0;
			button.FontWeight = FontWeights.Bold;
			button.Margin = new Thickness(2.0);
			button.Padding = new Thickness(4.0, 3.0, 4.0, 3.0);
			button.Foreground = Brushes.White;
			button.BorderBrush = WpfArgb(byte.MaxValue, 120, 120, 120);
			button.BorderThickness = new Thickness(1.0);
			button.HorizontalAlignment = HorizontalAlignment.Stretch;
			return button;
		}

		private Grid MakeWpfStepperRow(string label, int initialValue, string stepperKey, out TextBlock valueLabel)
		{
			Grid grid = new Grid();
			grid.Margin = new Thickness(0.0, 1.0, 0.0, 1.0);
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(22.0)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(60.0)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(22.0)
			});
			TextBlock textBlock = new TextBlock();
			textBlock.Text = label;
			textBlock.FontFamily = new FontFamily("Consolas");
			textBlock.FontSize = 10.0;
			textBlock.Foreground = WpfArgb(byte.MaxValue, 190, 190, 190);
			textBlock.VerticalAlignment = VerticalAlignment.Center;
			textBlock.Margin = new Thickness(2.0, 0.0, 0.0, 0.0);
			Grid.SetColumn(textBlock, 0);
			grid.Children.Add(textBlock);
			Button button = new Button();
			button.Content = "−";
			button.FontFamily = new FontFamily("Consolas");
			button.FontSize = 11.0;
			button.FontWeight = FontWeights.Bold;
			button.Background = WpfArgb(180, 60, 60, 60);
			button.Foreground = Brushes.White;
			button.BorderBrush = WpfArgb(byte.MaxValue, 120, 120, 120);
			button.BorderThickness = new Thickness(1.0);
			button.Margin = new Thickness(0.0);
			button.Padding = new Thickness(0.0);
			button.Tag = stepperKey;
			button.Click += WpfStepMinus_Click;
			Grid.SetColumn(button, 1);
			grid.Children.Add(button);
			valueLabel = new TextBlock();
			valueLabel.Text = initialValue.ToString();
			valueLabel.FontFamily = new FontFamily("Consolas");
			valueLabel.FontSize = 11.0;
			valueLabel.FontWeight = FontWeights.Bold;
			valueLabel.Foreground = Brushes.White;
			valueLabel.Background = WpfArgb(180, 40, 40, 40);
			valueLabel.TextAlignment = TextAlignment.Center;
			valueLabel.VerticalAlignment = VerticalAlignment.Center;
			valueLabel.Padding = new Thickness(2.0, 2.0, 2.0, 2.0);
			Grid.SetColumn(valueLabel, 2);
			grid.Children.Add(valueLabel);
			Button button2 = new Button();
			button2.Content = "+";
			button2.FontFamily = new FontFamily("Consolas");
			button2.FontSize = 11.0;
			button2.FontWeight = FontWeights.Bold;
			button2.Background = WpfArgb(180, 60, 60, 60);
			button2.Foreground = Brushes.White;
			button2.BorderBrush = WpfArgb(byte.MaxValue, 120, 120, 120);
			button2.BorderThickness = new Thickness(1.0);
			button2.Margin = new Thickness(0.0);
			button2.Padding = new Thickness(0.0);
			button2.Tag = stepperKey;
			button2.Click += WpfStepPlus_Click;
			Grid.SetColumn(button2, 3);
			grid.Children.Add(button2);
			return grid;
		}

		private Border MakeWpfSectionHeader(string text)
		{
			Border border = new Border();
			border.Background = WpfArgb(80, 90, 130, 180);
			border.Margin = new Thickness(0.0, 4.0, 0.0, 2.0);
			border.Padding = new Thickness(3.0, 1.0, 3.0, 1.0);
			TextBlock textBlock = new TextBlock();
			textBlock.Text = text;
			textBlock.FontFamily = new FontFamily("Consolas");
			textBlock.FontSize = 10.0;
			textBlock.FontWeight = FontWeights.Bold;
			textBlock.Foreground = WpfArgb(byte.MaxValue, 220, 220, 220);
			border.Child = textBlock;
			return border;
		}

		private void WpfStepMinus_Click(object sender, RoutedEventArgs e)
		{
			Button button = sender as Button;
			if (button != null)
			{
				AdjustParam(button.Tag as string, -1);
				UpdateWpfStepperValues();
				RestyleAllWpfToggles();
				base.ForceRefresh();
			}
		}

		private void WpfStepPlus_Click(object sender, RoutedEventArgs e)
		{
			Button button = sender as Button;
			if (button != null)
			{
				AdjustParam(button.Tag as string, 1);
				UpdateWpfStepperValues();
				RestyleAllWpfToggles();
				base.ForceRefresh();
			}
		}

		private void UpdateWpfStepperValues()
		{
			if (_lblTicksPerRow != null)
			{
				_lblTicksPerRow.Text = TicksPerRow + ((TicksPerRow != EffectiveTicksPerRow()) ? "*" : "");
			}
			if (_lblSessionMin != null)
			{
				_lblSessionMin.Text = SessionVolumetricMinutes.ToString();
			}
			if (_lblMinVolume != null)
			{
				_lblMinVolume.Text = MinVolumeToDisplay.ToString();
			}
			if (_lblValueAreaPct != null)
			{
				_lblValueAreaPct.Text = ValueAreaPct.ToString();
			}
			if (_lblLadderPct != null)
			{
				_lblLadderPct.Text = LadderImbalancePct.ToString();
			}
			if (_lblLadderMin != null)
			{
				_lblLadderMin.Text = LadderMinImbalance.ToString();
			}
			if (_lblLadderStack != null)
			{
				_lblLadderStack.Text = LadderImbalanceStackRows.ToString();
			}
			if (_lblCandlePct != null)
			{
				_lblCandlePct.Text = CandleImbalancePct.ToString();
			}
			if (_lblCandleMin != null)
			{
				_lblCandleMin.Text = CandleMinImbalance.ToString();
			}
			if (_lblCandleStack != null)
			{
				_lblCandleStack.Text = CandleImbalanceStackRows.ToString();
			}
			if (_lblExtLineW != null)
			{
				_lblExtLineW.Text = ExtendedImbalanceLineWidth.ToString();
			}
			if (_lblExtLookback != null)
			{
				_lblExtLookback.Text = ExtendedImbalanceLookbackBars.ToString();
			}
			if (_lblDeltaDir != null)
			{
				_lblDeltaDir.Text = DeltaHistogramDirection.ToString();
			}
			if (_lblLadderPositionW != null)
			{
				_lblLadderPositionW.Text = LadderPositionPct.ToString();
			}
			if (_lblLadderW != null)
			{
				_lblLadderW.Text = SessionLadderWidth.ToString();
			}
			if (_lblFpMetric != null)
			{
				_lblFpMetric.Text = CandleFootprintBarMetric.ToString();
			}
			if (_btnCandleProfileW != null)
			{
				_btnCandleProfileW.Content = ((CandleFootprintBarMetric == 2) ? "Candle Delta Profile" : "Candle Volume Profile");
			}
			if (_lblFpWidth != null)
			{
				_lblFpWidth.Text = CandleFootprintWidth.ToString();
			}
			if (_lblFpOffset != null)
			{
				_lblFpOffset.Text = CandleFootprintOffset.ToString();
			}
			if (_lblFlashOp != null)
			{
				_lblFlashOp.Text = NewImbalanceAnimationOpacity.ToString();
			}
			if (_lblFlashW != null)
			{
				_lblFlashW.Text = NewImbalanceAnimationLineWidth.ToString();
			}
			if (_lblFlashMs != null)
			{
				_lblFlashMs.Text = NewImbalanceAnimationMs.ToString();
			}
			if (_lblVolProfileW != null)
			{
				_lblVolProfileW.Text = VolumeProfileWidth.ToString();
			}
			if (_lblVolProfileDir != null)
			{
				_lblVolProfileDir.Text = VolumeProfileDirection.ToString();
			}
			if (_lblVolProfileOp != null)
			{
				_lblVolProfileOp.Text = VolumeProfileOpacity.ToString();
			}
			if (_lblFontSizeW != null)
			{
				_lblFontSizeW.Text = FontSizeText.ToString();
			}
			if (_lblMinRowHeightW != null)
			{
				_lblMinRowHeightW.Text = MinRowHeightForText.ToString();
			}
			if (_lblFootprintOpacityW != null)
			{
				_lblFootprintOpacityW.Text = FootprintOpacity.ToString();
			}
			if (_lblLadderOpacityW != null)
			{
				_lblLadderOpacityW.Text = LadderOpacity.ToString();
			}
			if (_lblSummaryHeightW != null)
			{
				_lblSummaryHeightW.Text = SummaryBarHeight.ToString();
			}
			if (_lblSummaryGapW != null)
			{
				_lblSummaryGapW.Text = SummaryGap.ToString();
			}
			if (_lblSummaryBorderW != null)
			{
				_lblSummaryBorderW.Text = SummaryBorderWidth.ToString();
			}
		}

		private void ApplyFootprintDeltaPalette(int paletteIndex)
		{
			Brush positive;
			Brush negative;
			switch (paletteIndex)
			{
			case 2:
				positive = Brushes.DodgerBlue;
				negative = Brushes.DarkOrange;
				break;
			case 3:
				positive = Brushes.Cyan;
				negative = Brushes.DeepPink;
				break;
			case 4:
				positive = Brushes.WhiteSmoke;
				negative = Brushes.Gray;
				break;
			case 5:
				positive = Brushes.Cyan;
				negative = Brushes.White;
				break;
			default:
				positive = Brushes.LimeGreen;
				negative = Brushes.Red;
				break;
			}

			AskTextColor = positive;
			BidTextColor = negative;
			DeltaPositiveColor = positive;
			DeltaNegativeColor = negative;
			BuyImbalanceColor = positive;
			SellImbalanceColor = negative;
		}

		private void RestyleAllWpfToggles()
		{
			StyleWpfToggle(_btnLadderImbW, ShowSessionLadderImbalances);
			StyleWpfToggle(_btnCandleImbW, ShowCandleImbalances);
			StyleWpfToggle(_btnExtImbW, ShowExtendedImbalanceLines);
			StyleWpfToggle(_btnBarSummaryW, ShowBarSummary);
			StyleWpfToggle(_btnNewImbAnimW, ShowNewImbalanceAnimation);
			StyleWpfToggle(_btnSessLadW, ShowSessionLadder);
			StyleWpfToggle(_btnSessVaW, ShowSessionValueArea);
			StyleWpfToggle(_btnSessDeltaW, ShowSessionDeltaProfile);
			StyleWpfToggle(_btnSessVolW, ShowSessionVolumeProfile);
			StyleWpfToggle(_btnBidAskW, ShowSessionBidAskColumn);
			StyleWpfToggle(_btnCandleFpW, ShowCandleFootprint);
			StyleWpfToggle(_btnCandleProfileW, ShowCandleProfileHistogram);
			StyleWpfToggle(_btnCandleVaW, ShowCandleValueArea);
			StyleWpfToggle(_btnPriceLineW, ShowCurrentPriceLine);
			StyleWpfToggle(_btnLadSideW, LadderSide == 2);
			StyleWpfToggle(_btnRowGridW, ShowSessionRowGrid);
			StyleWpfToggle(_btnAutoFpW, AutoResizeCandleFootprint);
			StyleWpfToggle(_btnShowNumbersW, ShowBidAskText);
			if (_btnLadSideW != null)
			{
				_btnLadSideW.Content = "Ladder Side: " + (LadderSide == 2 ? "LEFT" : "RIGHT");
			}
		}

		private void BuildBetaWpfPanel()
		{
			if (_wpfBuilt || base.ChartControl == null || !UseWpfPanel)
			{
				return;
			}
			try
			{
				_wpfPanel = new Grid();
				_wpfPanel.HorizontalAlignment = HorizontalAlignment.Left;
				_wpfPanel.VerticalAlignment = VerticalAlignment.Top;
				_wpfPanel.Width = 278.0;

				Border border = new Border();
				border.Background = WpfArgb(232, 13, 16, 22);
				border.BorderBrush = WpfArgb(220, 44, 170, 215);
				border.BorderThickness = new Thickness(1.0);
				border.CornerRadius = new CornerRadius(4.0);
				_wpfPanel.Children.Add(border);

				StackPanel stackPanel = new StackPanel();
				stackPanel.Orientation = Orientation.Vertical;
				border.Child = stackPanel;

				Grid header = new Grid();
				header.Height = 24.0;
				header.Background = WpfArgb(190, 20, 105, 145);
				header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
				header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
				header.Cursor = Cursors.SizeAll;

				_wpfHeaderText = new TextBlock();
				_wpfHeaderText.Text = "ILT_DEV_FOOTPRINT_OFB";
				_wpfHeaderText.FontFamily = new FontFamily("Consolas");
				_wpfHeaderText.FontSize = 11.0;
				_wpfHeaderText.FontWeight = FontWeights.Bold;
				_wpfHeaderText.Foreground = Brushes.White;
				_wpfHeaderText.Margin = new Thickness(7.0, 3.0, 4.0, 3.0);
				_wpfHeaderText.VerticalAlignment = VerticalAlignment.Center;
				Grid.SetColumn(_wpfHeaderText, 0);
				header.Children.Add(_wpfHeaderText);

				_wpfMinBtn = new Button();
				_wpfMinBtn.Content = PanelMinimizedPersist ? "▶" : "▼";
				_wpfMinBtn.FontFamily = new FontFamily("Consolas");
				_wpfMinBtn.FontSize = 10.0;
				_wpfMinBtn.FontWeight = FontWeights.Bold;
				_wpfMinBtn.Background = WpfArgb(190, 40, 48, 58);
				_wpfMinBtn.Foreground = Brushes.White;
				_wpfMinBtn.BorderBrush = WpfArgb(230, 110, 180, 210);
				_wpfMinBtn.BorderThickness = new Thickness(1.0);
				_wpfMinBtn.Width = 24.0;
				_wpfMinBtn.Height = 19.0;
				_wpfMinBtn.Margin = new Thickness(2.0);
				_wpfMinBtn.Click += delegate { ToggleWpfMinimize(); };
				Grid.SetColumn(_wpfMinBtn, 1);
				header.Children.Add(_wpfMinBtn);
				header.MouseLeftButtonDown += WpfHeader_MouseDown;
				header.MouseLeftButtonUp += WpfHeader_MouseUp;
				header.MouseMove += WpfHeader_MouseMove;
				stackPanel.Children.Add(header);

				_wpfBody = new StackPanel();
				_wpfBody.Orientation = Orientation.Vertical;
				_wpfBody.Margin = new Thickness(7.0, 5.0, 7.0, 7.0);
				_wpfBody.Children.Add(MakeWpfSectionHeader("DISPLAY MODE"));

				Grid modeRow = new Grid();
				modeRow.Margin = new Thickness(1.0, 2.0, 1.0, 3.0);
				modeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82.0) });
				modeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
				TextBlock modeLabel = new TextBlock();
				modeLabel.Text = "Mode";
				modeLabel.FontFamily = new FontFamily("Consolas");
				modeLabel.FontSize = 10.0;
				modeLabel.Foreground = WpfArgb(byte.MaxValue, 205, 205, 205);
				modeLabel.VerticalAlignment = VerticalAlignment.Center;
				Grid.SetColumn(modeLabel, 0);
				modeRow.Children.Add(modeLabel);

				_cmbVisualModeW = new ComboBox();
				_cmbVisualModeW.FontFamily = new FontFamily("Consolas");
				_cmbVisualModeW.FontSize = 10.5;
				_cmbVisualModeW.Background = WpfArgb(byte.MaxValue, 36, 42, 51);
				_cmbVisualModeW.Foreground = Brushes.White;
				_cmbVisualModeW.BorderBrush = WpfArgb(byte.MaxValue, 90, 150, 180);
				_cmbVisualModeW.Items.Add("Sided Ladder");
				_cmbVisualModeW.Items.Add("Sided Ladder Plus");
				_cmbVisualModeW.Items.Add("Delta Ladder");
				_cmbVisualModeW.Items.Add("Profile");
				_cmbVisualModeW.Items.Add("Delta Profile");
				_cmbVisualModeW.SelectedIndex = Clamp((int)VisualMode, 1, 5) - 1;
				_cmbVisualModeW.SelectionChanged += delegate
				{
					if (_cmbVisualModeW.SelectedIndex >= 0)
					{
						VisualMode = (IltDevFootprintDisplayMode)(_cmbVisualModeW.SelectedIndex + 1);
						PersistRuntimeSettings();
						base.ForceRefresh();
					}
				};
				Grid.SetColumn(_cmbVisualModeW, 1);
				modeRow.Children.Add(_cmbVisualModeW);
				_wpfBody.Children.Add(modeRow);

				_wpfBody.Children.Add(MakeWpfSectionHeader("VISIBLE ELEMENTS"));
				_btnSessLadW = MakeWpfToggle("Side Ladder");
				_btnSessLadW.Click += delegate
				{
					ShowSessionLadder = !ShowSessionLadder;
					StyleWpfToggle(_btnSessLadW, ShowSessionLadder);
					PersistRuntimeSettings();
					base.ForceRefresh();
				};
				_wpfBody.Children.Add(_btnSessLadW);

				_btnShowNumbersW = MakeWpfToggle("Footprint Numbers");
				_btnShowNumbersW.Click += delegate
				{
					ShowBidAskText = !ShowBidAskText;
					StyleWpfToggle(_btnShowNumbersW, ShowBidAskText);
					PersistRuntimeSettings();
					base.ForceRefresh();
				};
				_wpfBody.Children.Add(_btnShowNumbersW);

				_btnCandleImbW = MakeWpfToggle("Vivid Imbalance Values");
				_btnCandleImbW.Click += delegate
				{
					ShowCandleImbalances = !ShowCandleImbalances;
					StyleWpfToggle(_btnCandleImbW, ShowCandleImbalances);
					PersistRuntimeSettings();
					base.ForceRefresh();
				};
				_wpfBody.Children.Add(_btnCandleImbW);

				_btnExtImbW = MakeWpfToggle("Extend Imbalance -> Right");
				_btnExtImbW.Click += delegate
				{
					ShowExtendedImbalanceLines = !ShowExtendedImbalanceLines;
					StyleWpfToggle(_btnExtImbW, ShowExtendedImbalanceLines);
					PersistRuntimeSettings();
					base.ForceRefresh();
				};
				_wpfBody.Children.Add(_btnExtImbW);

				_btnBarSummaryW = MakeWpfToggle("Bar Delta Summary");
				_btnBarSummaryW.Click += delegate
				{
					ShowBarSummary = !ShowBarSummary;
					StyleWpfToggle(_btnBarSummaryW, ShowBarSummary);
					PersistRuntimeSettings();
					base.ForceRefresh();
				};
				_wpfBody.Children.Add(_btnBarSummaryW);

				_btnLadSideW = MakeWpfToggle("Ladder Side: Left / Right");
				_btnLadSideW.Click += delegate
				{
					LadderSide = LadderSide == 1 ? 2 : 1;
					LadderPositionPct = LadderSide == 2 ? 0 : 100;
					RestyleAllWpfToggles();
					PersistRuntimeSettings();
					base.ForceRefresh();
				};
				_wpfBody.Children.Add(_btnLadSideW);

				_btnAutoFpW = MakeWpfToggle("Auto Footprint Width");
				_btnAutoFpW.Click += delegate
				{
					AutoResizeCandleFootprint = !AutoResizeCandleFootprint;
					StyleWpfToggle(_btnAutoFpW, AutoResizeCandleFootprint);
					PersistRuntimeSettings();
					base.ForceRefresh();
				};
				_wpfBody.Children.Add(_btnAutoFpW);

				_wpfBody.Children.Add(MakeWpfSectionHeader("DELTA / PROFILE ALIGNMENT"));
				_wpfBody.Children.Add(MakeWpfStepperRow("Delta 1L 2BI 3R", DeltaHistogramDirection, "DeltaHistogramDirection", out _lblDeltaDir));
				_wpfBody.Children.Add(MakeWpfStepperRow("Profile 1L 2C 3R", VolumeProfileDirection, "VolumeProfileDirection", out _lblVolProfileDir));
				_wpfBody.Children.Add(MakeWpfStepperRow("Ladder X % (0L / 100R)", LadderPositionPct, "LadderPositionPct", out _lblLadderPositionW));

				_wpfBody.Children.Add(MakeWpfSectionHeader("SIZE / FILTER"));
				_wpfBody.Children.Add(MakeWpfStepperRow("Ticks / Row*", TicksPerRow, "TicksPerRow", out _lblTicksPerRow));
				_wpfBody.Children.Add(MakeWpfStepperRow("Min Volume", MinVolumeToDisplay, "MinVolumeToDisplay", out _lblMinVolume));
				_wpfBody.Children.Add(MakeWpfStepperRow("FP Width (manual)", CandleFootprintWidth, "CandleFootprintWidth", out _lblFpWidth));
				_wpfBody.Children.Add(MakeWpfStepperRow("Ladder Width", SessionLadderWidth, "SessionLadderWidth", out _lblLadderW));
				_wpfBody.Children.Add(MakeWpfStepperRow("Font Size", FontSizeText, "FontSizeText", out _lblFontSizeW));
				_wpfBody.Children.Add(MakeWpfStepperRow("Text Row Height", MinRowHeightForText, "MinRowHeightForText", out _lblMinRowHeightW));

				_wpfBody.Children.Add(MakeWpfSectionHeader("FP / DELTA COLORS"));
				Grid colorRow = new Grid();
				colorRow.Margin = new Thickness(1.0, 2.0, 1.0, 3.0);
				colorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82.0) });
				colorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
				TextBlock colorLabel = new TextBlock();
				colorLabel.Text = "Palette";
				colorLabel.FontFamily = new FontFamily("Consolas");
				colorLabel.FontSize = 10.0;
				colorLabel.Foreground = WpfArgb(byte.MaxValue, 205, 205, 205);
				colorLabel.VerticalAlignment = VerticalAlignment.Center;
				Grid.SetColumn(colorLabel, 0);
				colorRow.Children.Add(colorLabel);

				_cmbColorPaletteW = new ComboBox();
				_cmbColorPaletteW.FontFamily = new FontFamily("Consolas");
				_cmbColorPaletteW.FontSize = 10.5;
				_cmbColorPaletteW.Background = WpfArgb(byte.MaxValue, 36, 42, 51);
				_cmbColorPaletteW.Foreground = Brushes.White;
				_cmbColorPaletteW.BorderBrush = WpfArgb(byte.MaxValue, 90, 150, 180);
				_cmbColorPaletteW.Items.Add("Colors...");
				_cmbColorPaletteW.Items.Add("Green / Red");
				_cmbColorPaletteW.Items.Add("Blue / Orange");
				_cmbColorPaletteW.Items.Add("Cyan / Magenta");
				_cmbColorPaletteW.Items.Add("White / Gray");
				_cmbColorPaletteW.Items.Add("Cyan / White");
				_cmbColorPaletteW.SelectedIndex = 0;
				_cmbColorPaletteW.ToolTip = "Quick FP/delta palettes. Exact colors remain available in indicator properties.";
				_cmbColorPaletteW.SelectionChanged += delegate
				{
					if (_cmbColorPaletteW.SelectedIndex > 0)
					{
						ApplyFootprintDeltaPalette(_cmbColorPaletteW.SelectedIndex);
						PersistRuntimeSettings();
						base.ForceRefresh();
					}
				};
				Grid.SetColumn(_cmbColorPaletteW, 1);
				colorRow.Children.Add(_cmbColorPaletteW);
				_wpfBody.Children.Add(colorRow);

				_wpfBody.Children.Add(MakeWpfSectionHeader("OPACITY / IMBALANCE"));
				_wpfBody.Children.Add(MakeWpfStepperRow("FP Opacity", FootprintOpacity, "FootprintOpacity", out _lblFootprintOpacityW));
				_wpfBody.Children.Add(MakeWpfStepperRow("Ladder Opacity", LadderOpacity, "LadderOpacity", out _lblLadderOpacityW));
				_wpfBody.Children.Add(MakeWpfStepperRow("Imbalance %", CandleImbalancePct, "CandleImbalancePct", out _lblCandlePct));
				_wpfBody.Children.Add(MakeWpfStepperRow("Imbalance Min", CandleMinImbalance, "CandleMinImbalance", out _lblCandleMin));

				TextBlock note = new TextBlock();
				note.Text = "* Ticks / Row requires F5 or Reload NinjaScript.";
				note.FontFamily = new FontFamily("Consolas");
				note.FontSize = 9.0;
				note.Foreground = WpfArgb(byte.MaxValue, 145, 160, 170);
				note.TextWrapping = TextWrapping.Wrap;
				note.Margin = new Thickness(1.0, 6.0, 1.0, 0.0);
				_wpfBody.Children.Add(note);

				stackPanel.Children.Add(_wpfBody);
				RestyleAllWpfToggles();
				UpdateWpfStepperValues();
				_wpfBody.Visibility = PanelMinimizedPersist ? Visibility.Collapsed : Visibility.Visible;
				base.UserControlCollection.Add(_wpfPanel);
				_wpfBuilt = true;
				PlaceWpfPanel();
				ApplyPanelVisibility();
			}
			catch (Exception ex)
			{
				base.Print((object)("BuildBetaWpfPanel error: " + ex.Message));
			}
		}

		private void BuildWpfPanel()
		{
			if (_wpfBuilt || base.ChartControl == null || !UseWpfPanel)
			{
				return;
			}
			try
			{
				_wpfPanel = new Grid();
				_wpfPanel.HorizontalAlignment = HorizontalAlignment.Left;
				_wpfPanel.VerticalAlignment = VerticalAlignment.Top;
				_wpfPanel.Width = 270.0;
				Border border = new Border();
				border.Background = WpfArgb(225, 18, 22, 30);
				border.BorderBrush = WpfArgb(180, 90, 130, 180);
				border.BorderThickness = new Thickness(1.0);
				border.CornerRadius = new CornerRadius(4.0);
				_wpfPanel.Children.Add(border);
				StackPanel stackPanel = new StackPanel();
				stackPanel.Orientation = Orientation.Vertical;
				border.Child = stackPanel;
				Grid grid = new Grid();
				grid.Height = 22.0;
				grid.Background = WpfArgb(120, 90, 130, 180);
				grid.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(1.0, GridUnitType.Star)
				});
				grid.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = GridLength.Auto
				});
				grid.Cursor = Cursors.SizeAll;
				_wpfHeaderText = new TextBlock();
				_wpfHeaderText.Text = "ILT-DEV-FOTPRINT";
				_wpfHeaderText.FontFamily = new FontFamily("Consolas");
				_wpfHeaderText.FontSize = 11.0;
				_wpfHeaderText.FontWeight = FontWeights.Bold;
				_wpfHeaderText.Foreground = Brushes.White;
				_wpfHeaderText.Margin = new Thickness(6.0, 3.0, 4.0, 3.0);
				_wpfHeaderText.VerticalAlignment = VerticalAlignment.Center;
				Grid.SetColumn(_wpfHeaderText, 0);
				grid.Children.Add(_wpfHeaderText);
				_wpfMinBtn = new Button();
				_wpfMinBtn.Content = (PanelMinimizedPersist ? "▶" : "▼");
				_wpfMinBtn.FontFamily = new FontFamily("Consolas");
				_wpfMinBtn.FontSize = 10.0;
				_wpfMinBtn.FontWeight = FontWeights.Bold;
				_wpfMinBtn.Background = WpfArgb(180, 60, 60, 60);
				_wpfMinBtn.Foreground = Brushes.White;
				_wpfMinBtn.BorderBrush = WpfArgb(byte.MaxValue, 120, 120, 120);
				_wpfMinBtn.BorderThickness = new Thickness(1.0);
				_wpfMinBtn.Width = 22.0;
				_wpfMinBtn.Height = 18.0;
				_wpfMinBtn.Margin = new Thickness(2.0);
				_wpfMinBtn.Click += delegate
				{
					ToggleWpfMinimize();
				};
				Grid.SetColumn(_wpfMinBtn, 1);
				grid.Children.Add(_wpfMinBtn);
				grid.MouseLeftButtonDown += WpfHeader_MouseDown;
				grid.MouseLeftButtonUp += WpfHeader_MouseUp;
				grid.MouseMove += WpfHeader_MouseMove;
				stackPanel.Children.Add(grid);
				_wpfBody = new StackPanel();
				_wpfBody.Orientation = Orientation.Vertical;
				_wpfBody.Margin = new Thickness(6.0, 4.0, 6.0, 6.0);
				_wpfBody.Children.Add(MakeWpfSectionHeader("ENGINE / SESSION"));
				_wpfBody.Children.Add(MakeWpfStepperRow("Ticks / Row", TicksPerRow, "TicksPerRow", out _lblTicksPerRow));
				_wpfBody.Children.Add(MakeWpfStepperRow("Session Min", SessionVolumetricMinutes, "SessionVolumetricMinutes", out _lblSessionMin));
				_wpfBody.Children.Add(MakeWpfStepperRow("Min Volume", MinVolumeToDisplay, "MinVolumeToDisplay", out _lblMinVolume));
				_wpfBody.Children.Add(MakeWpfStepperRow("Candle VA %", ValueAreaPct, "ValueAreaPct", out _lblValueAreaPct));
				_wpfBody.Children.Add(MakeWpfSectionHeader("LADDER IMBALANCE"));
				_btnLadderImbW = MakeWpfToggle("Ladder Imb Paint");
				_btnLadderImbW.Click += delegate
				{
					ShowSessionLadderImbalances = !ShowSessionLadderImbalances;
					StyleWpfToggle(_btnLadderImbW, ShowSessionLadderImbalances);
					PersistRuntimeSettings();
					base.ForceRefresh();
				};
				_wpfBody.Children.Add(_btnLadderImbW);
				_wpfBody.Children.Add(MakeWpfStepperRow("Ladder Imb %", LadderImbalancePct, "LadderImbalancePct", out _lblLadderPct));
				_wpfBody.Children.Add(MakeWpfStepperRow("Ladder Min", LadderMinImbalance, "LadderMinImbalance", out _lblLadderMin));
				_wpfBody.Children.Add(MakeWpfStepperRow("Ladder Stack", LadderImbalanceStackRows, "LadderImbalanceStackRows", out _lblLadderStack));
				_wpfBody.Children.Add(MakeWpfSectionHeader("CANDLE IMBALANCE"));
				_btnCandleImbW = MakeWpfToggle("Candle Imb Paint");
				_btnCandleImbW.Click += delegate
				{
					ShowCandleImbalances = !ShowCandleImbalances;
					StyleWpfToggle(_btnCandleImbW, ShowCandleImbalances);
					PersistRuntimeSettings();
					base.ForceRefresh();
				};
				_wpfBody.Children.Add(_btnCandleImbW);
				_btnExtImbW = MakeWpfToggle("Extended Imb Lines");
				_btnExtImbW.Click += delegate
				{
					ShowExtendedImbalanceLines = !ShowExtendedImbalanceLines;
					StyleWpfToggle(_btnExtImbW, ShowExtendedImbalanceLines);
					PersistRuntimeSettings();
					base.ForceRefresh();
				};
				_wpfBody.Children.Add(_btnExtImbW);
				_wpfBody.Children.Add(MakeWpfStepperRow("Ext Line Width", ExtendedImbalanceLineWidth, "ExtendedImbalanceLineWidth", out _lblExtLineW));
				_btnNewImbAnimW = MakeWpfToggle("New IMB Flash");
				_btnNewImbAnimW.Click += delegate
				{
					ShowNewImbalanceAnimation = !ShowNewImbalanceAnimation;
					StyleWpfToggle(_btnNewImbAnimW, ShowNewImbalanceAnimation);
					PersistRuntimeSettings();
					base.ForceRefresh();
				};
				_wpfBody.Children.Add(_btnNewImbAnimW);
				_wpfBody.Children.Add(MakeWpfStepperRow("Candle Imb %", CandleImbalancePct, "CandleImbalancePct", out _lblCandlePct));
				_wpfBody.Children.Add(MakeWpfStepperRow("Candle Min", CandleMinImbalance, "CandleMinImbalance", out _lblCandleMin));
				_wpfBody.Children.Add(MakeWpfStepperRow("Candle Stack", CandleImbalanceStackRows, "CandleImbalanceStackRows", out _lblCandleStack));
				_wpfBody.Children.Add(MakeWpfSectionHeader("LAYOUT"));
				_btnSessLadW = MakeWpfToggle("Session Ladder");
				_btnSessLadW.Click += delegate
				{
					ShowSessionLadder = !ShowSessionLadder;
					StyleWpfToggle(_btnSessLadW, ShowSessionLadder);
					PersistRuntimeSettings();
					base.ForceRefresh();
				};
				_wpfBody.Children.Add(_btnSessLadW);
				_btnSessVaW = MakeWpfToggle("Session VA");
				_btnSessVaW.Click += delegate
				{
					ShowSessionValueArea = !ShowSessionValueArea;
					StyleWpfToggle(_btnSessVaW, ShowSessionValueArea);
					PersistRuntimeSettings();
					base.ForceRefresh();
				};
				_wpfBody.Children.Add(_btnSessVaW);
				_btnSessDeltaW = MakeWpfToggle("Ladder Delta Profile");
				_btnSessDeltaW.Click += delegate
				{
					ShowSessionDeltaProfile = !ShowSessionDeltaProfile;
					StyleWpfToggle(_btnSessDeltaW, ShowSessionDeltaProfile);
					PersistRuntimeSettings();
					base.ForceRefresh();
				};
				_wpfBody.Children.Add(_btnSessDeltaW);
				_btnSessVolW = MakeWpfToggle("Ladder Volume Profile");
				_btnSessVolW.Click += delegate
				{
					ShowSessionVolumeProfile = !ShowSessionVolumeProfile;
					StyleWpfToggle(_btnSessVolW, ShowSessionVolumeProfile);
					PersistRuntimeSettings();
					base.ForceRefresh();
				};
				_wpfBody.Children.Add(_btnSessVolW);
				_btnBidAskW = MakeWpfToggle("Ladder BID x ASK");
				_btnBidAskW.Click += delegate
				{
					ShowSessionBidAskColumn = !ShowSessionBidAskColumn;
					StyleWpfToggle(_btnBidAskW, ShowSessionBidAskColumn);
					PersistRuntimeSettings();
					base.ForceRefresh();
				};
				_wpfBody.Children.Add(_btnBidAskW);
				_btnCandleFpW = MakeWpfToggle("Candle Footprint");
				_btnCandleFpW.Click += delegate
				{
					ShowCandleFootprint = !ShowCandleFootprint;
					StyleWpfToggle(_btnCandleFpW, ShowCandleFootprint);
					PersistRuntimeSettings();
					base.ForceRefresh();
				};
				_wpfBody.Children.Add(_btnCandleFpW);
				_btnCandleProfileW = MakeWpfToggle((CandleFootprintBarMetric == 2) ? "Candle Delta Profile" : "Candle Volume Profile");
				_btnCandleProfileW.Click += delegate
				{
					ShowCandleProfileHistogram = !ShowCandleProfileHistogram;
					StyleWpfToggle(_btnCandleProfileW, ShowCandleProfileHistogram);
					PersistRuntimeSettings();
					base.ForceRefresh();
				};
				_wpfBody.Children.Add(_btnCandleProfileW);
				_btnCandleVaW = MakeWpfToggle("Candle VA");
				_btnCandleVaW.Click += delegate
				{
					ShowCandleValueArea = !ShowCandleValueArea;
					StyleWpfToggle(_btnCandleVaW, ShowCandleValueArea);
					PersistRuntimeSettings();
					base.ForceRefresh();
				};
				_wpfBody.Children.Add(_btnCandleVaW);
				_btnPriceLineW = MakeWpfToggle("Price Line");
				_btnPriceLineW.Click += delegate
				{
					ShowCurrentPriceLine = !ShowCurrentPriceLine;
					StyleWpfToggle(_btnPriceLineW, ShowCurrentPriceLine);
					PersistRuntimeSettings();
					base.ForceRefresh();
				};
				_wpfBody.Children.Add(_btnPriceLineW);
				_btnLadSideW = MakeWpfToggle("Ladder Left/Right");
				_btnLadSideW.Click += delegate
				{
					LadderSide = ((LadderSide != 1) ? 1 : 2);
					LadderPositionPct = LadderSide == 2 ? 0 : 100;
					StyleWpfToggle(_btnLadSideW, LadderSide == 2);
					PersistRuntimeSettings();
					base.ForceRefresh();
				};
				_wpfBody.Children.Add(_btnLadSideW);
				_btnRowGridW = MakeWpfToggle("Row Grid");
				_btnRowGridW.Click += delegate
				{
					ShowSessionRowGrid = !ShowSessionRowGrid;
					StyleWpfToggle(_btnRowGridW, ShowSessionRowGrid);
					PersistRuntimeSettings();
					base.ForceRefresh();
				};
				_wpfBody.Children.Add(_btnRowGridW);
				_wpfBody.Children.Add(MakeWpfStepperRow("Delta Dir 1L 2C 3R", DeltaHistogramDirection, "DeltaHistogramDirection", out _lblDeltaDir));
				_wpfBody.Children.Add(MakeWpfStepperRow("Ladder Position %", LadderPositionPct, "LadderPositionPct", out _lblLadderPositionW));
				_wpfBody.Children.Add(MakeWpfStepperRow("Ladder Width", SessionLadderWidth, "SessionLadderWidth", out _lblLadderW));
				_btnAutoFpW = MakeWpfToggle("Auto FP Width");
				_btnAutoFpW.Click += delegate
				{
					AutoResizeCandleFootprint = !AutoResizeCandleFootprint;
					StyleWpfToggle(_btnAutoFpW, AutoResizeCandleFootprint);
					PersistRuntimeSettings();
					base.ForceRefresh();
				};
				_wpfBody.Children.Add(_btnAutoFpW);
				_wpfBody.Children.Add(MakeWpfStepperRow("FP Metric 1V 2D", CandleFootprintBarMetric, "CandleFootprintBarMetric", out _lblFpMetric));
				_wpfBody.Children.Add(MakeWpfStepperRow("FP Width", CandleFootprintWidth, "CandleFootprintWidth", out _lblFpWidth));
				_wpfBody.Children.Add(MakeWpfStepperRow("FP Offset", CandleFootprintOffset, "CandleFootprintOffset", out _lblFpOffset));
				_wpfBody.Children.Add(MakeWpfStepperRow("IMB Flash Opacity", NewImbalanceAnimationOpacity, "NewImbalanceAnimationOpacity", out _lblFlashOp));
				_wpfBody.Children.Add(MakeWpfStepperRow("IMB Flash Width", NewImbalanceAnimationLineWidth, "NewImbalanceAnimationLineWidth", out _lblFlashW));
				_wpfBody.Children.Add(MakeWpfStepperRow("IMB Flash MS", NewImbalanceAnimationMs, "NewImbalanceAnimationMs", out _lblFlashMs));
				_wpfBody.Children.Add(MakeWpfSectionHeader("VOLUME PROFILE SETTINGS"));
				_wpfBody.Children.Add(MakeWpfStepperRow("VP Width", VolumeProfileWidth, "VolumeProfileWidth", out _lblVolProfileW));
				_wpfBody.Children.Add(MakeWpfStepperRow("VP Dir 1L 2C 3R", VolumeProfileDirection, "VolumeProfileDirection", out _lblVolProfileDir));
				_wpfBody.Children.Add(MakeWpfStepperRow("VP Opacity", VolumeProfileOpacity, "VolumeProfileOpacity", out _lblVolProfileOp));
				TextBlock textBlock = new TextBlock();
				textBlock.Text = "- / + edits. Full settings in indicator properties.";
				textBlock.FontFamily = new FontFamily("Consolas");
				textBlock.FontSize = 9.0;
				textBlock.Foreground = WpfArgb(byte.MaxValue, 140, 140, 140);
				textBlock.TextWrapping = TextWrapping.Wrap;
				textBlock.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
				_wpfBody.Children.Add(textBlock);
				stackPanel.Children.Add(_wpfBody);
				RestyleAllWpfToggles();
				_wpfBody.Visibility = (PanelMinimizedPersist ? Visibility.Collapsed : Visibility.Visible);
				base.UserControlCollection.Add(_wpfPanel);
				_wpfBuilt = true;
				PlaceWpfPanel();
				ApplyPanelVisibility();
			}
			catch (Exception ex)
			{
				base.Print((object)("BuildWpfPanel error: " + ex.Message));
			}
		}

		private void WpfHeader_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (_wpfPanel != null)
			{
				_wpfDragging = true;
				_wpfDragStart = e.GetPosition((IInputElement)base.ChartControl);
				_wpfStartLeft = _wpfPanel.Margin.Left;
				_wpfStartTop = _wpfPanel.Margin.Top;
				IInputElement inputElement = sender as IInputElement;
				if (inputElement != null)
				{
					inputElement.CaptureMouse();
				}
				e.Handled = true;
			}
		}

		private void WpfHeader_MouseMove(object sender, MouseEventArgs e)
		{
			if (_wpfDragging && _wpfPanel != null)
			{
				Point position = e.GetPosition((IInputElement)base.ChartControl);
				double num = position.X - _wpfDragStart.X;
				double num2 = position.Y - _wpfDragStart.Y;
				double x = _wpfStartLeft + num;
				double y = _wpfStartTop + num2;
				ClampWpfPosition(ref x, ref y);
				_wpfPanel.Margin = new Thickness(x, y, 0.0, 0.0);
				PanelXW = x;
				PanelYW = y;
				_userMovedPanel = true;
			}
		}

		private void WpfHeader_MouseUp(object sender, MouseButtonEventArgs e)
		{
			_wpfDragging = false;
			IInputElement inputElement = sender as IInputElement;
			if (inputElement != null)
			{
				inputElement.ReleaseMouseCapture();
			}
			PersistRuntimeSettings();
			e.Handled = true;
		}

		private void ClampWpfPosition(ref double x, ref double y)
		{
			double num = ((base.ChartPanel != null) ? base.ChartPanel.W : 800);
			double num2 = ((base.ChartPanel != null) ? base.ChartPanel.H : 600);
			double num3 = Math.Max(0.0, num - 40.0);
			double num4 = Math.Max(0.0, num2 - 22.0);
			if (x < 0.0)
			{
				x = 0.0;
			}
			if (x > num3)
			{
				x = num3;
			}
			if (y < 0.0)
			{
				y = 0.0;
			}
			if (y > num4)
			{
				y = num4;
			}
		}

		private void PlaceWpfPanel()
		{
			if (_wpfPanel == null)
			{
				return;
			}
			double x = PanelXW;
			double y = PanelYW;
			if (!_wpfPosInitialized)
			{
				if (PanelXW < 0.0 || PanelYW < 0.0)
				{
					_wpfPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
					double num = ((_wpfPanel.Width > 0.0) ? _wpfPanel.Width : ((_wpfPanel.DesiredSize.Width > 0.0) ? _wpfPanel.DesiredSize.Width : 270.0));
					double num2 = ((_wpfPanel.DesiredSize.Height > 0.0) ? _wpfPanel.DesiredSize.Height : 400.0);
					double num3 = ((base.ChartPanel != null) ? base.ChartPanel.W : 800);
					double num4 = ((base.ChartPanel != null) ? base.ChartPanel.H : 600);
					if (PanelXW < 0.0)
					{
						x = Math.Max(0.0, (num3 - num) / 2.0);
					}
					if (PanelYW < 0.0)
					{
						y = Math.Max(0.0, (num4 - num2) / 2.0);
					}
				}
				_wpfPosInitialized = true;
			}
			ClampWpfPosition(ref x, ref y);
			_wpfPanel.Margin = new Thickness(x, y, 0.0, 0.0);
			PanelXW = x;
			PanelYW = y;
		}

		private void ToggleWpfMinimize()
		{
			PanelMinimizedPersist = !PanelMinimizedPersist;
			if (_wpfBody != null)
			{
				_wpfBody.Visibility = (PanelMinimizedPersist ? Visibility.Collapsed : Visibility.Visible);
			}
			if (_wpfMinBtn != null)
			{
				_wpfMinBtn.Content = (PanelMinimizedPersist ? "▶" : "▼");
			}
			PersistRuntimeSettings();
		}

		private void ApplyPanelVisibility()
		{
			if (_wpfPanel != null)
			{
				_wpfPanel.Visibility = (_panelUserHidden ? Visibility.Collapsed : Visibility.Visible);
			}
		}

		private void RemoveWpfPanel()
		{
			if (!_wpfBuilt)
			{
				return;
			}
			try
			{
				if (base.UserControlCollection != null && _wpfPanel != null && base.UserControlCollection.Contains(_wpfPanel))
				{
					base.UserControlCollection.Remove(_wpfPanel);
				}
			}
			catch (Exception ex)
			{
				base.Print((object)("RemoveWpfPanel error: " + ex.Message));
			}
			_wpfPanel = null;
			_wpfBody = null;
			_wpfHeaderText = null;
			_wpfMinBtn = null;
			_btnLadderImbW = (_btnCandleImbW = (_btnExtImbW = (_btnNewImbAnimW = null)));
			_btnSessLadW = (_btnSessVaW = (_btnCandleFpW = (_btnCandleVaW = null)));
			_btnSessDeltaW = (_btnBidAskW = (_btnCandleProfileW = null));
			_btnPriceLineW = (_btnLadSideW = (_btnRowGridW = null));
			_btnAutoFpW = (_btnSessVolW = null);
			_btnBarSummaryW = null;
			_lblTicksPerRow = (_lblSessionMin = (_lblMinVolume = (_lblValueAreaPct = null)));
			_lblLadderPct = (_lblLadderMin = (_lblLadderStack = null));
			_lblCandlePct = (_lblCandleMin = (_lblCandleStack = null));
			_lblExtLineW = (_lblExtLookback = null);
			_lblDeltaDir = (_lblLadderPositionW = (_lblLadderW = (_lblFpMetric = (_lblFpWidth = (_lblFpOffset = null)))));
			_lblFlashOp = (_lblFlashW = (_lblFlashMs = null));
			_lblVolProfileW = (_lblVolProfileDir = (_lblVolProfileOp = null));
			_lblFontSizeW = (_lblMinRowHeightW = (_lblFootprintOpacityW = (_lblLadderOpacityW = null)));
			_lblSummaryHeightW = (_lblSummaryGapW = (_lblSummaryBorderW = null));
			_btnShowNumbersW = null;
			_cmbVisualModeW = null;
			_cmbColorPaletteW = null;
			_wpfBuilt = false;
		}

		private void BuildToolbarButton()
		{
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Expected O, but got Unknown
			if (_toolbarBtnAdded || !ShowToolbarButton || base.ChartControl == null)
			{
				return;
			}
			try
			{
				Window window = Window.GetWindow(((FrameworkElement)(object)base.ChartControl).Parent);
				_chartWindow = (Chart)(object)((window is Chart) ? window : null);
				if (_chartWindow != null && ((NTWindow)_chartWindow).MainMenu != null)
				{
					_toolbarBtn = new NTMenuItem();
					((HeaderedItemsControl)(object)_toolbarBtn).Header = "ILT-DEV-OFB";
					((FrameworkElement)(object)_toolbarBtn).Style = Application.Current.TryFindResource("MainMenuItem") as Style;
					((Control)(object)_toolbarBtn).Foreground = new System.Windows.Media.SolidColorBrush(Color.FromArgb(byte.MaxValue, 90, 190, byte.MaxValue));
					((FrameworkElement)(object)_toolbarBtn).ToolTip = "Mostrar / ocultar ajustes de ILT_DEV_FOOTPRINT_OFB";
					((DependencyObject)(object)_toolbarBtn).SetValue(AutomationProperties.AutomationIdProperty, (object)("ILT_DEV_FOOTPRINT_OFB_TOOLBAR_BTN_" + DateTime.Now.Ticks));
					((MenuItem)(object)_toolbarBtn).Click += ToolbarBtn_Click;
					((NTWindow)_chartWindow).MainMenu.Add(_toolbarBtn);
					_toolbarBtnAdded = true;
				}
			}
			catch (Exception ex)
			{
				base.Print((object)("BuildToolbarButton error: " + ex.Message));
			}
		}

		private void ToolbarBtn_Click(object sender, RoutedEventArgs e)
		{
			if (!_wpfBuilt)
			{
				BuildBetaWpfPanel();
				_panelUserHidden = false;
				return;
			}
			_panelUserHidden = !_panelUserHidden;
			if (!_panelUserHidden && !_userMovedPanel)
			{
				_wpfPosInitialized = false;
				PanelXW = -1.0;
				PanelYW = -1.0;
				PlaceWpfPanel();
			}
			ApplyPanelVisibility();
			PersistRuntimeSettings();
		}

		private void RemoveToolbarButton()
		{
			if (!_toolbarBtnAdded)
			{
				return;
			}
			try
			{
				if (_toolbarBtn != null)
				{
					((MenuItem)(object)_toolbarBtn).Click -= ToolbarBtn_Click;
				}
				if (_chartWindow != null && ((NTWindow)_chartWindow).MainMenu != null && _toolbarBtn != null && ((NTWindow)_chartWindow).MainMenu.Contains(_toolbarBtn))
				{
					((NTWindow)_chartWindow).MainMenu.Remove(_toolbarBtn);
				}
			}
			catch (Exception ex)
			{
				base.Print((object)("RemoveToolbarButton error: " + ex.Message));
			}
			_toolbarBtn = null;
			_chartWindow = null;
			_toolbarBtnAdded = false;
		}
	}
}

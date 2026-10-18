using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Core;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;

// ILT-DEV DOMHEATMAP DEV
// Fuente interna editable con identidad separada para iterar desde NinjaScript Editor.
// No depende de archivos externos de autorización ni altera la versión BETA instalada.

namespace NinjaTrader.NinjaScript.Indicators.ILT_DEV_SUITE
{
	public enum IltDevHeatmapPaletteDev
	{
		CustomBidAsk,
		BookmapClassic,
		IceFire,
		Inferno,
		Viridis,
		Plasma,
		EmeraldGold
	}
	internal class HM6Level
	{
		public long Qty;

		public long MaxQty;

		public long PrevQty;

		public int AddCnt;

		public int RedCnt;

		public int UpdCnt;

		public int StackCnt;

		public long RecentAddQty;

		public double StackWindowStart;

		public double LastAddTime;

		public double LastStackMarkerTime;

		public long LastStackMarkerQty;

		public long AbsorbedVol;

		public long RecentAbsorbedVol;

		public int AbsorbCount;

		public double LastAbsorbTime;

		public double LastAbsorbMarkerTime;

		public long LastAbsorbMarkerQty;

		public double FirstSeen;

		public double LastSeen;

		public long DeltaBuy;

		public long DeltaSell;

		public long LastAggVol;

		public double LastAggTime;

		public long LastWallAttackRegisteredVol;

		public double LastWallAttackRegisteredTime;

		public long LastDepleteQty;

		public double LastDepleteTime;

		public long RecentPullQty;

		public double LastPullTime;

		public long RecentConsumeQty;

		public double LastConsumeTime;

		public long RecentRefillQty;

		public double LastRefillTime;

		public int RefillCount;

		public bool LastDepleteWasConsume;

		public long Delta
		{
			get
			{
				return DeltaBuy - DeltaSell;
			}
		}
	}
	internal class HM6GhostAgg
	{
		public long Vol;

		public double Time;

		public long Reg;
	}
	internal class HM6IltDevZoneAggSample
	{
		public long Qty;

		public double Time;
	}
	internal class HM6SRPending
	{
		public double Price;

		public int Side;

		public long Vol;

		public long ReAdd;

		public double Time;

		public int BarIdx = -1;
	}
	internal class HM6SRZone
	{
		public double Price;

		public bool AskSide;

		public long Qty;

		public long InitiativeQty;

		public long ReAddQty;

		public int FormationCount;

		public int State;

		public int StartBar;

		public int VisualEventBar = -1;

		public double StartTime;

		public int BreakBar = -1;

		public double LastUpdate;

		public bool Armed;

		public int TouchBar = -1;

		public int TouchCloses;

		public int FadeBar = -1;
	}
	internal class HM6SRHist
	{
		public double Price;

		public int Side;

		public long Qty;

		public int StartBar;

		public double StartTime;

		public bool Defended = true;

		public bool EverDefended;
	}
	internal class HM6IltDevSupportLevel
	{
		public double Price;

		public bool WasBid;

		public long Qty;

		public long InitiativeQty;

		public long ReAddQty;

		public long WallQty;

		public int WallPresence;

		public double WallAgeMs;

		public long WallRefillQty;

		public long WallConsumeQty;

		public long WallPullQty;

		public int WallRefillEvents;

		public int StrengthScore;

		public int StartBar;

		public int VisualEventBar = -1;

		public double StartTime;

		public int AdverseCloses;

		public int LastAdverseBar = -1;

		public double LastAdverseClose;

		public int ReactCount;

		public int LastCountedBar = -1;

		public double LastReinforceTime;

		public bool Broken;

		public int BreakBar = -1;

		public bool WallIcebergConfirmed;
	}
	internal class HM6TagFreeze
	{
		public long Qty;

		public double Time;

		public string State;
	}
	internal class HM6Pull
	{
		public double Price;

		public long Qty;

		public bool WasBid;

		public double Time;

		public float Fade;
	}
	internal class HM6Flow
	{
		public double Price;

		public long Qty;

		public bool WasBid;

		public bool IsAdd;

		public double Time;

		public float Fade;
	}
	internal class HM6PassiveEvent
	{
		public double Price;

		public long Qty;

		public bool WasBid;

		public int Type;

		public double Time;

		public long SliceSeq;

		public float Fade;

		public int BarIdx;

		public bool PriceInteracted;

		public double PriceInteractionTime;

		public int PriceInteractionBar;
	}
	internal class HM6HeatmapSlice
	{
		public long Seq;

		public int BarIdx;

		public double Time;

		public Dictionary<double, long> Bid = new Dictionary<double, long>();

		public Dictionary<double, long> Ask = new Dictionary<double, long>();

		public List<HM6PassiveEvent> Events = new List<HM6PassiveEvent>();

		public bool HasData
		{
			get
			{
				if (Bid.Count <= 0 && Ask.Count <= 0)
				{
					if (Events != null)
					{
						return Events.Count > 0;
					}
					return false;
				}
				return true;
			}
		}
	}
	internal class HM6PassiveZone
	{
		public double Price;

		public long RxSeq = -1L;

		public int RxStart = -1;

		public int RxCount = -1;

		public int RxFirst = -1;

		public int RxLast = -1;

		public bool RxHas;

		public bool WasBid;

		public int StartBar;

		public int LastBar;

		public long LastQty;

		public long MaxQty;

		public long AddQty;

		public long PullQty;

		public long ConsumeQty;

		public long RefillQty;

		public int LastEventType;

		public double FirstTime;

		public double LastUpdateTime;

		public double LastEventTime;

		public long FirstSeq;

		public long LastSeq;

		public long LastEventSeq;

		public bool Active;

		public bool Reached;

		public double ReachedTime;

		public int TouchCount;

		public long LastEventQty;

		public int EventCount;

		public int LastEventBar;

		public int RefillEvents;

		public bool DerivedWall;

		public int PresenceCount;

		public bool SpoofLabelInteracted;

		public double SpoofInteractionTime;

		public int SpoofInteractionBar;

		public long PullBurstQty;

		public double PullBurstStart;

		public double PullBurstLast;

		public double PullNearTicks = double.MaxValue;

		public bool IcebergConfirmed;

		public long IcebergQty;
	}
	internal class HM6WallCandidate
	{
		public double Price;

		public bool WasBid;

		public int FirstIndex;

		public int LastIndex;

		public int LastSeenIndex;

		public int Presence;

		public int CurrentRun;

		public int MaxRun;

		public long MaxQty;

		public long CurrentQty;

		public long SumQty;

		public double LastTime;

		public double Score;

		public int LastVisibleGap;

		public bool ActiveNow;

		public double AvgQty;

		public double DepthZ;

		public double PercentileRank;

		public double PersistenceK;

		public double DistanceK;

		public double PullRatio;

		public double SpoofRisk;

		public long AddQty;

		public long PullQty;

		public long ConsumeQty;

		public long RefillQty;

		public int TouchCount;

		public string State;

		public long RemovedQty;

		public long BrokenQty;
	}
	internal class HM6DefendedWallLine
	{
		public double Price;

		public bool WasBid;

		public int StartBar;

		public int VisualEventBar = -1;

		public int BreakBar;

		public long StartSeq;

		public long BreakSeq;

		public double EventTime;

		public double ConfirmTime;

		public double LastUpdateTime;

		public long ExecutedQty;

		public long RefillQty;

		public long BreakQty;

		public long FrozenQty;

		public bool QtyFrozen;

		public bool PreConfirmed;

		public double PreConfirmTime;

		public int PreConfirmBar = -1;

		public int FavorableCloses;

		public int LastCountedBar = -1;

		public bool Discard;

		public bool Faded;

		public int FadeBar = -1;

		public double FadeTime;

		public long FadeQty;

		public int EndBar;

		public double EndTime;

		public bool Retested;

		public long RetestQty;

		public double RetestTime;

		public double MaxAdverseTicks;

		public double MaxFavorableTicks;

		public long AttackDelta;

		public long ResponseDelta;

		public long ResponseOppositeDelta;

		public double DefenseScore;

		public int StrengthScore;

		public long AttackThresholdAtEvent;

		public int ReinforceCount;

		public int ProvenRetests;

		public bool OutcomeTallied;

		public double FavorableCloseMarginSumTicks;

		public bool SecondaryConfirmed;

		public bool SupportConfluence;

		public bool WallPersistentConfirmed;

		public bool DeltaConfirmed;

		public bool PriceConfirmed;

		public bool RefillConfirmed;

		public bool StrongAbsorption;

		public bool BuiltFromExecutedCollapse;

		public bool DeltaRequired;

		public bool FailedBreak;

		public double BreakTime;

		public bool Confirmed;

		public bool Active;

		public bool Broken;
	}
	internal class HM6PassiveSummary
	{
		public long BidQty;

		public long AskQty;

		public int BidLevels;

		public int AskLevels;

		public double BidStability;

		public double AskStability;

		public long BidPull;

		public long AskPull;

		public long BidConsume;

		public long AskConsume;

		public long BidRefill;

		public long AskRefill;

		public double BidRefillX;

		public double AskRefillX;

		public double BidScore;

		public double AskScore;

		public double NetScore;

		public string Signal;

		public bool SignalBid;

		public int SignalType;
	}
	internal class HM6Trade
	{
		public double Price;

		public double RawPrice;

		public long Vol;

		public long MaxSingle;

		public int TradeCount;

		public bool IsBuy;

		public double Time;

		public int CreatedMs;

		public int LastMs;

		public int BarIdx;

		public long Seq;

		public bool IsBlock;

		public long StampTicks;
	}
	internal class HM6BubbleRenderItem
	{
		public double Price;

		public long Vol;

		public long MaxSingle;

		public int TradeCount;

		public bool IsBuy;

		public int CreatedMs;

		public int LastMs;

		public int BarIdx;

		public long Seq;

		public bool IsBlock;

		public long StampTicks;
	}
	public class ILT_DEV_DOMHEATMAP_DEV : Indicator
	{
		private struct HM6HeatCell
		{
			public int Bar;

			public double Price;

			public long Qty;

			public bool IsBid;
		}

		private class DomSnapHM6
		{
			public Dictionary<double, long> Bid = new Dictionary<double, long>();

			public Dictionary<double, long> Ask = new Dictionary<double, long>();

			public bool HasData
			{
				get
				{
					if (Bid.Count <= 0)
					{
						return Ask.Count > 0;
					}
					return true;
				}
			}

			public DomSnapHM6 Clone()
			{
				DomSnapHM6 domSnapHM = new DomSnapHM6();
				foreach (KeyValuePair<double, long> item in Bid)
				{
					domSnapHM.Bid[item.Key] = item.Value;
				}
				foreach (KeyValuePair<double, long> item2 in Ask)
				{
					domSnapHM.Ask[item2.Key] = item2.Value;
				}
				return domSnapHM;
			}
		}

		private const int STK_MIN = 4;

		private const double PULL_FADE = 9000.0;

		private const int TRADE_HISTORY_HARD_MAX = 350000;

		private const int TRADE_TRIM_BUFFER = 8192;

		private const int BT_ARCHIVE_HARD_MAX = 250000;

		private const int HIST_SNAPSHOT_MIN_MS = 220;

		private const int CLEAN_EVENTS_MIN_MS = 90;

		private const double ZQ_CONF_K = 5.5;

		private const double ZQ_CONF_EXP = 1.2;

		private const double ZQ_CONF_MAX = 22.0;

		private const double ZQ_REBOUND_MIN_TICKS = 3.0;

		private const double ZQ_PROVEN_BONUS = 14.0;

		private const double ZQ_PROVEN_STEP = 6.0;

		private const double ZQ_PROVEN_MAX = 26.0;

		private const double ZQ_SPOOF_SCORE_CEIL = 35.0;

		private const int ZQ_ICEBERG_BONUS = 12;

		private const int ZQ_ICEBERG_FLOOR = 70;

		private const float ZQ_EVENT_BROKEN_FADE = 0.42f;

		private const int HEATMAP_NEW_BAR_GUARD_MS = 650;

		private const int LIVE_HEATMAP_WARMUP_MS = 1400;

		private const int LIVE_HEATMAP_MAX_SLICES = 1200;

		private const float LW = 116f;

		private const float LW_B = 58f;

		private const float LW_P = 0f;

		private const float LW_A = 58f;

		private const float LW_HD = 20f;

		private const float PW = 255f;

		private const float PMG = 6f;

		private const float PPD = 10f;

		private const int LADDER_SHIFT_MAX_PX = 10000;

		private const int HEAT_PAL_N = 48;

		private const int HM_BMP_W = 1024;

		private const int HM_BMP_H = 1024;

		private const int SR_RETAIN_BARS = 500;

		private const double ILT_DEV_REINFORCE_MS = 30000.0;

		private const int DEFENDED_WALL_SOFT_MAX = 600;

		private const int DEFENDED_WALL_TRIM_TARGET = 500;

		private const int DEFENDED_WALL_HARD_MAX = 2400;

		private const int SR_MAX_RENDER = 120;

		private const int AGGRESSIVE_BUBBLE_GROUP_MS = 280;

		private const int HIST_RETAIN_BARS = 6000;

		private const int HIST_TRIM_EVERY = 256;

		private Dictionary<double, HM6Level> _bid;

		private Dictionary<double, HM6Level> _ask;

		private Dictionary<int, DomSnapHM6> _hist;

		private DomSnapHM6 _snap;

		private readonly object _lk = new object();

		private List<HM6Pull> _pulls;

		private List<HM6Flow> _flows;

		private List<HM6PassiveEvent> _passiveEvents;

		private List<HM6PassiveEvent> _pendingPassiveHeatmapEvents;

		private Dictionary<string, HM6PassiveZone> _passiveZones;

		private List<HM6DefendedWallLine> _defendedWallLines;

		private HM6PassiveSummary _passiveSummary;

		private List<HM6Trade> _trades;

		private List<HM6Trade> _btArchive;

		private Dictionary<int, List<HM6Trade>> _tradesByBar;

		private Dictionary<int, List<HM6Trade>> _btArchiveByBar;

		private List<HM6BubbleRenderItem> _bubbleRenderCache;

		private HashSet<long> _bubbleSeqCache;

		private readonly List<HM6Flow> _flowRenderBuf = new List<HM6Flow>(1024);

		private readonly List<HM6PassiveEvent> _passiveRenderBuf = new List<HM6PassiveEvent>(20000);

		private readonly List<HM6PassiveZone> _wallZoneRenderBuf = new List<HM6PassiveZone>(4096);

		private readonly List<HM6HeatCell> _heatCellBuf = new List<HM6HeatCell>(4096);

		private bool _bubbleRenderCacheDirty = true;

		private int _bubbleCacheFrom = int.MinValue;

		private int _bubbleCacheTo = int.MinValue;

		private int _bubbleCacheGroup = int.MinValue;

		private int _bubbleCacheVisualMin = int.MinValue;

		private int _bubbleCacheBlockMin = int.MinValue;

		private long _bubbleCacheSeq = -1L;

		private int _bubbleCacheTradeCount = -1;

		private int _bubbleCacheBtCount = -1;

		private long _bubbleCacheMaxVol = 1L;

		private float[] _bubbleBarXCache;

		private long[] _bubbleBarTCache;

		private int _bubbleBarTCacheB0 = int.MinValue;

		private int _bubbleBarTCacheCount = -1;

		private long _bubbleBarTCacheTail;

		private Dictionary<int, float> _bubblePriceYCache;

		private long _tradeSeq;

		private long _maxTradeVol = 1L;

		private double _quoteBestBid;

		private double _quoteBestAsk = double.MaxValue;

		private DateTime _lastQuoteTime = DateTime.MinValue;

		private int _lastLiveLastMs = -100000000;

		private int _lastLiveBtMs = -100000000;

		private Dictionary<double, long> _passivePocMap;

		private double _passivePocPrice;

		private long _passivePocVol;

		private DateTime _sessionDate = DateTime.MinValue;

		private TimeZoneInfo _nyTz;

		private double _lastTapePrice;

		private int _lastTapeDir;

		private int _histPrimaryAnchorBar = -1;

		private long _maxQty;

		private double _midPx;

		private double _bestBid;

		private double _bestAsk = double.MaxValue;

		private bool _hasDom;

		private double _tick;

		private int _decs;

		private float _ldX = 1f;

		private float _ldY = 1f;

		private float _curDpi = 1f;

		private float _lastFontSmall = -1f;

		private float _lastFontBold = -1f;

		private float _lastFontLadder = -1f;

		private string _lastVisualSignature;

		private DispatcherTimer _flowTimer;

		private bool _flowTimerActive;

		private readonly int[] _zoneTallyProven = new int[4];

		private readonly int[] _zoneTallyBroken = new int[4];

		private int _lastRefreshMs = -100000000;

		private int _lastHistSnapshotMs = -100000000;

		private int _lastCleanEventsMs = -100000000;

		private int _heatmapLiveBarIdx = -1;

		private int _heatmapLiveBarBornMs = -100000000;

		private long _heatmapStableMaxQty = 1L;

		private int _heatmapStableMaxLastMs = -100000000;

		private int _lastHistSnapshotBar = -1;

		private List<HM6HeatmapSlice> _liveHeatmapSlices;

		private long _liveHeatmapSeq;

		private int _lastLiveHeatmapSliceMs = -100000000;

		private bool _liveHeatmapDomReady;

		private int _liveHeatmapWarmupUntilMs = -100000000;

		private SharpDX.Direct2D1.SolidColorBrush _bBid;

		private SharpDX.Direct2D1.SolidColorBrush _bAsk;

		private SharpDX.Direct2D1.SolidColorBrush _bLvBid;

		private SharpDX.Direct2D1.SolidColorBrush _bLvAsk;

		private SharpDX.Direct2D1.SolidColorBrush _bStBid;

		private SharpDX.Direct2D1.SolidColorBrush _bStAsk;

		private SharpDX.Direct2D1.SolidColorBrush _bAbs;

		private SharpDX.Direct2D1.SolidColorBrush _bPull;

		private SharpDX.Direct2D1.SolidColorBrush _bBubBuy;

		private SharpDX.Direct2D1.SolidColorBrush _bBubSell;

		private SharpDX.Direct2D1.SolidColorBrush _bBubBord;

		private SharpDX.Direct2D1.SolidColorBrush _bBubFlash;

		private SharpDX.Direct2D1.SolidColorBrush _bMid;

		private SharpDX.Direct2D1.SolidColorBrush _bLadBg;

		private SharpDX.Direct2D1.SolidColorBrush _bLadBid;

		private SharpDX.Direct2D1.SolidColorBrush _bLadAsk;

		private SharpDX.Direct2D1.SolidColorBrush _bLadSt;

		private SharpDX.Direct2D1.SolidColorBrush _bLadSep;

		private SharpDX.Direct2D1.SolidColorBrush _bLadTxt;

		private SharpDX.Direct2D1.SolidColorBrush _bLadHl;

		private SharpDX.Direct2D1.SolidColorBrush _bRowCur;

		private SharpDX.Direct2D1.SolidColorBrush _bPoc;

		private SharpDX.Direct2D1.SolidColorBrush _bPanBg;

		private SharpDX.Direct2D1.SolidColorBrush _bPanSh;

		private SharpDX.Direct2D1.SolidColorBrush _bPanBord;

		private SharpDX.Direct2D1.SolidColorBrush _bTxt;

		private SharpDX.Direct2D1.SolidColorBrush _bDim;

		private SharpDX.Direct2D1.SolidColorBrush _bSlBg;

		private SharpDX.Direct2D1.SolidColorBrush _bSlFg;

		private SharpDX.Direct2D1.SolidColorBrush _bOn;

		private SharpDX.Direct2D1.SolidColorBrush _bSect;

		private SharpDX.Direct2D1.SolidColorBrush _bFlowAdd;

		private SharpDX.Direct2D1.SolidColorBrush _bFlowPull;

		private SharpDX.Direct2D1.SolidColorBrush _bFlowText;

		private SharpDX.Direct2D1.SolidColorBrush _bPassiveBid;

		private SharpDX.Direct2D1.SolidColorBrush _bPassiveAsk;

		private SharpDX.Direct2D1.SolidColorBrush _bPassiveRefill;

		private SharpDX.Direct2D1.SolidColorBrush _bPassiveWarn;

		private TextFormat _fSm;

		private TextFormat _fBold;

		private TextFormat _fTiny;

		private TextFormat _fLad;

		private TextFormat _fPassiveBig;

		private TextFormat _fPassiveMid;

		private StrokeStyle _stDash;

		private StrokeStyle _stDot;

		private bool _dxOk;

		private IntPtr _dxRtRef = IntPtr.Zero;

		private RectangleF _rPan;

		private RectangleF _rTitle;

		private RectangleF _rSF;

		private RectangleF _rSL;

		private RectangleF _rSG;

		private RectangleF _rSBub;

		private RectangleF _rSLadF;

		private RectangleF _rSLadX;

		private RectangleF _rSBlock;

		private RectangleF _rTLad;

		private RectangleF _rTLin;

		private RectangleF _rTSt;

		private RectangleF _rTPull;

		private RectangleF _rTBub;

		private RectangleF _rTDir;

		private RectangleF _rTAbs;

		private RectangleF _rTPoc;

		private RectangleF _rTImb;

		private RectangleF _rTImbLine;

		private RectangleF _rTFoot;

		private RectangleF _rTSess;

		private RectangleF _rTFlow;

		private RectangleF _rTPassive;

		private RectangleF _rSImbPct;

		private RectangleF _rSImbMin;

		private RectangleF _rSImbStack;

		private RectangleF _rSImbSize;

		private RectangleF _rSSessDeltaDir;

		private RectangleF _rSFlowMin;

		private RectangleF _rSFlowFade;

		private RectangleF _rSFlowOpacity;

		private RectangleF _rSFlowSize;

		private RectangleF _rSPassiveMin;

		private RectangleF _rSPassiveDepth;

		private RectangleF _rSPassiveEvent;

		private RectangleF _rPassiveHudMin;

		private bool _passiveHudMinimized;

		private bool _col;

		private bool _drag;

		private string _dId;

		private float _dX0;

		private float _dY0;

		private float _panelX = -1f;

		private float _panelY = -1f;

		private float _panelStartX;

		private float _panelStartY;

		private float _lastPanelCanvasW;

		private float _lastPanelCanvasH;

		private float _lastPanelW;

		private float _lastPanelH;

		private Dictionary<string, RectangleF> _stepMinus = new Dictionary<string, RectangleF>();

		private Dictionary<string, RectangleF> _stepPlus = new Dictionary<string, RectangleF>();

		private Grid _wpfPanel;

		private StackPanel _wpfBody;

		private TextBlock _wpfHeaderText;

		private Button _wpfMinBtn;

		private Button _btnLadW;

		private Button _btnLinW;

		private Button _btnStW;

		private Button _btnPullW;

		private Button _btnBubW;

		private Button _btnAbsW;

		private Button _btnDirW;

		private Button _btnPocW;

		private Button _btnFlowW;

		private Button _btnPassiveW;

		private Button _btnPassiveMarkersW;

		private Button _btnPassiveWallsW;

		private Button _btnBasicVisualW;

		private Button _btnIltDevZonesW;

		private Button _btnIltDevSRW;

		private Button _btnSRHistW;

		private TextBlock _lblStepSRPersistBig;

		private TextBlock _lblStepSRPersistPass;

		private TextBlock _lblStepSRHistBig;

		private TextBlock _lblStepSRHistPass;

		private TextBlock _lblStepF;

		private TextBlock _lblStepLadF;

		private TextBlock _lblStepLadX;

		private TextBlock _lblStepG;

		private TextBlock _lblStepBub;

		private TextBlock _lblStepBlock;

		private TextBlock _lblStepFlowMin;

		private TextBlock _lblStepFlowFade;

		private TextBlock _lblStepPassiveMin;

		private TextBlock _lblStepPassiveDepth;

		private TextBlock _lblStepPassiveEvent;

		private TextBlock _lblStepPassiveWallBars;

		private TextBlock _lblStepPassiveMarkerSize;

		private TextBlock _lblStepPassiveLabelMin;

		private TextBlock _lblStepWallMinQty;

		private TextBlock _lblStepRenderWin;

		private TextBlock _lblStepHeatSpeed;

		private TextBlock _lblStepHeatPalette;

		private TextBlock _lblStepIltDevZoneAttack;

		private TextBlock _lblStepIltDevZoneScale;

		private TextBlock _lblStepIltDevZoneCloses;

		private TextBlock _lblStepIltDevZoneBand;

		private TextBlock _lblStepIltDevZoneMaxBars;

		private TextBlock _lblStepIltDevZoneHistory;

		private TextBlock _lblStepIltDevZoneAdaptive;

		private TextBlock _lblStepIltDevZoneStrength;

		private TextBlock _lblStepIltDevZoneMergeTicks;

		private TextBlock _lblStepIltDevZoneMergeSec;

		private TextBlock _lblStepIltDevSRScale;

		private TextBlock _lblStepIltDevSRReAdd;

		private TextBlock _lblStepIltDevSRWindow;

		private TextBlock _lblStepIltDevSRBand;

		private TextBlock _lblStepIltDevSRStrength;

		private TextBlock _lblStepIltDevSRPresence;

		private TextBlock _lblStepIltDevSRAge;

		private TextBlock _lblStepIltDevSRFresh;

		private TextBlock _lblStepIltDevSRRetention;

		private TextBlock _lblStepFlowOp;

		private TextBlock _lblStepFlowSize;

		private TextBlock _lblStepFlowLbl;

		private TextBlock _lblStepMaxHist;

		private Button _btnKeepHistW;

		private bool _wpfBuilt;

		private bool _wpfDragging;

		private System.Windows.Point _wpfDragStart;

		private double _wpfStartLeft;

		private double _wpfStartTop;

		private bool _wpfPosInitialized;

		private bool _userMovedPanel;

		private bool _panelUserHidden = true;

		private NTMenuItem _toolbarBtn;

		private Chart _chartWindow;

		private bool _toolbarBtnAdded;

		private bool _chartMouseHandlersAttached;

		private bool _pBasicVisual = true;

		private int _pF = 5;

		private int _pLadF = 5;

		private int _pLadderShiftPx;

		private int _pLadderBgOpacity = 35;

		private int _pL = 10;

		private int _pG = 4;

		private int _pBub = 3;

		private int _pBlock = 10;

		private int _pBtVisualMin = 50;

		private bool _pBtAuto = true;

		private bool _pLad = true;

		private bool _pLin;

		private bool _pSt = true;

		private bool _pPull = true;

		private bool _pBubShow;

		private bool _pAbs = true;

		private bool _pDir = true;

		private bool _pPoc = true;

		private bool _pFlow = true;

		private int _pFlowMin = 10;

		private int _pFlowFadeMs = 5200;

		private int _pFlowOpacity = 72;

		private int _pFlowSize = 3;

		private int _pFlowLabelMin = 30;

		private int _pFlowFps = 14;

		private int _pHeatmapMinOpacity = 4;

		private int _pHeatmapContrast = 180;

		private int _pHeatmapBarWidth = 155;

		private int _pHeatmapScrollSpeed = 4;

		private int _pFlowPanelOpacity = 12;

		private bool _pPassive = true;

		private int _pPassiveMinQty = 10;

		private int _pPassiveDepth = 10;

		private int _pPassiveEventMin = 5;

		private int _pPassiveAggWindowMs = 650;

		private int _pPassiveRefillWindowMs = 1200;

		private int _pPassiveEventFadeMs = 5200;

		private bool _pPassiveChartMarkers = true;

		private bool _pPassiveWallZones = true;

		private int _pWallMinQty = 10;

		private bool _pIltDevZones = true;

		private int _pPassiveWallMaxBars = 600;

		private int _pPassiveMarkerSize = 3;

		private int _pPassiveMarkerLabelMin = 5;

		private bool _pPassiveCleanLabelsOnTouch = true;

		private int _pPassiveLabelTouchTicks = 2;

		private int _pPassiveLabelTouchMinAgeMs = 1200;

		private int _lastPassiveLabelCleanMs = -100000000;

		private int _pBubAnimMs = 1400;

		private int _pBubGlow = 85;

		private int _pBubPulse = 35;

		private bool _pBubShockwave = true;

		private bool _pBubLightning = true;

		private bool _pBtFlash = true;

		private int _pBtFlashMs = 6000;

		private int _pBtFlashStrength = 70;

		private bool _pKeepHistoricalBubbles = true;

		private int _pMaxHistoricalBubbles = 350000;

		private static readonly Stopwatch _iltDevMonoClock = Stopwatch.StartNew();

		private int _pHeatColorMode = 1;

		private bool _pHeatLogScale = true;

		private SharpDX.Direct2D1.SolidColorBrush[] _heatPal;

		private bool _pHmScrollBmp = true;

		private SharpDX.Direct2D1.BitmapRenderTarget _hmBrt;

		private SharpDX.Direct2D1.SolidColorBrush _hmCellBrush;

		private IntPtr _hmRtRef = IntPtr.Zero;

		private double _hmAnchor;

		private long _lvScanSeq;

		private int _lvScanMs;

		private long _lvScanMax;

		private bool _lvScanHas;

		private double _passiveSummaryMs = -100000000.0;

		private List<HM6WallCandidate> _adSel;

		private long _adSelSeq = -1L;

		private int _adSelStart = -1;

		private int _adSelCount = -1;

		private int _adSelMs = -100000000;

		private readonly HashSet<long> _lblUsed = new HashSet<long>();

		private string _hudPipeTxt;

		private int _hudPipeMs;

		private string _hudTallyTxt;

		private long _hmLastPaintedSeq;

		private bool _hmBmpFail;

		private Dictionary<string, HM6GhostAgg> _ghostAgg;

		private int _pInstScalePct = 200;

		private int _pIltDevZoneAttackMinQty = 10;

		private int _pIltDevZoneScalePct = 210;

		private int _pIltDevZoneBandTicks = 4;

		private int _pIltDevZoneCandidateMaxBars = 600;

		private int _pIltDevZoneHistoryBars = 2000;

		private int _pIltDevZoneAdaptivePct = 75;

		private int _pIltDevZoneMinStrength = 70;

		private int _pIltDevZoneMergeTicks = 6;

		private int _pIltDevZoneMergeSec = 60;

		private List<HM6IltDevZoneAggSample> _iltDevZoneAggSamples;

		private double _iltDevZoneAdaptiveLastCalc = -1.0;

		private long _iltDevZoneAdaptiveCached;

		private int _pSpoofNearTicks = 24;

		private int _pZoneCloses = 2;

		private int _pFadeBars = 5;

		private int _lastClosedBarEval = -1;

		private bool _pSRZones = true;

		private int _pIltDevSRScalePct = 200;

		private int _pIltDevSRReAddPct = 40;

		private int _pIltDevSRWindowMs = 8000;

		private int _pIltDevSRMinStrength = 80;

		private int _pIltDevSRWallMinPresence = 10;

		private int _pIltDevSRWallMinAgeMs = 900;

		private int _pIltDevSRWallFreshMs = 2800;

		private int _pIltDevSRRetentionBars = 2000;

		private int _pSRBandTicks = 4;

		private int _pTagLifeSec = 60;

		private int _pRenderWinPct = 50;

		private Dictionary<string, HM6SRPending> _srPending;

		private List<HM6SRZone> _srZones;

		private SharpDX.Direct2D1.SolidColorBrush _bSRZone;

		private bool _pSRHist = true;

		private int _pSRHistWinPts = 5;

		private int _pSRPersistBig = 15;

		private int _pSRPersistPass = 12;

		private int _pSRHistBig = 40;

		private int _pSRHistPass = 20;

		private List<HM6SRHist> _srHist;

		private double _srHistDefStamp = -1E+18;

		private SharpDX.Direct2D1.SolidColorBrush _bSRHist;

		private List<HM6IltDevSupportLevel> _iltDevLevels;

		private Dictionary<string, HM6TagFreeze> _tagFreeze;

		private int _histTrimNextBar = int.MinValue;

		private readonly List<int> _histTrimScratch = new List<int>(1024);

		private int _clipDepth;

		private int _lastRenderErrMs;

		private float _hoverX = -1E+09f;

		private float _hoverY = -1E+09f;

		private static readonly string[] _fontFallback = new string[5] { "Consolas", "Lucida Console", "Courier New", "DejaVu Sans Mono", "Segoe UI" };

		private static readonly float[] HEAT_STOP_POS = new float[7] { 0f, 0.22f, 0.42f, 0.6f, 0.76f, 0.9f, 1f };

		private static readonly float[,,] HEAT_PALETTES = new float[6, 7, 3]
		{
			{
				{ 0.03f, 0.05f, 0.16f },
				{ 0.05f, 0.16f, 0.55f },
				{ 0f, 0.55f, 0.9f },
				{ 0f, 0.88f, 0.7f },
				{ 0.72f, 0.96f, 0.22f },
				{ 1f, 0.86f, 0.12f },
				{ 1f, 1f, 0.88f }
			},
			{
				{ 0.02f, 0.03f, 0.12f },
				{ 0.02f, 0.12f, 0.48f },
				{ 0f, 0.55f, 0.92f },
				{ 0.18f, 0.92f, 1f },
				{ 0.92f, 0.98f, 1f },
				{ 1f, 0.28f, 0.08f },
				{ 1f, 1f, 0.92f }
			},
			{
				{ 0.01f, 0.01f, 0.05f },
				{ 0.12f, 0.03f, 0.32f },
				{ 0.42f, 0.06f, 0.48f },
				{ 0.76f, 0.12f, 0.3f },
				{ 0.96f, 0.42f, 0.08f },
				{ 1f, 0.78f, 0.12f },
				{ 1f, 1f, 0.85f }
			},
			{
				{ 0.06f, 0.03f, 0.18f },
				{ 0.12f, 0.16f, 0.46f },
				{ 0.05f, 0.48f, 0.58f },
				{ 0.12f, 0.7f, 0.48f },
				{ 0.48f, 0.86f, 0.28f },
				{ 0.82f, 0.94f, 0.18f },
				{ 1f, 1f, 0.72f }
			},
			{
				{ 0.03f, 0.02f, 0.16f },
				{ 0.22f, 0.05f, 0.48f },
				{ 0.52f, 0.03f, 0.58f },
				{ 0.82f, 0.12f, 0.42f },
				{ 0.98f, 0.42f, 0.12f },
				{ 1f, 0.78f, 0.16f },
				{ 1f, 1f, 0.72f }
			},
			{
				{ 0.01f, 0.04f, 0.08f },
				{ 0.01f, 0.18f, 0.3f },
				{ 0f, 0.5f, 0.48f },
				{ 0.12f, 0.8f, 0.52f },
				{ 0.62f, 0.94f, 0.24f },
				{ 1f, 0.76f, 0.08f },
				{ 1f, 1f, 0.88f }
			}
		};

		public override string DisplayName
		{
			get
			{
				return base.Name;
			}
		}

		private static int IltDevNowMs
		{
			get
			{
				long elapsedMilliseconds = _iltDevMonoClock.ElapsedMilliseconds;
				if (elapsedMilliseconds < int.MaxValue)
				{
					return (int)elapsedMilliseconds;
				}
				return int.MaxValue;
			}
		}

		private bool PassiveZoneEngineRequired
		{
			get
			{
				if (!_pPassiveWallZones && !_pIltDevZones)
				{
					return _pSRZones;
				}
				return true;
			}
		}

		private double SRBandTicks
		{
			get
			{
				return Math.Max(1.0, _pSRBandTicks);
			}
		}

		[NinjaScriptProperty]
		[Display(Name = "Panel WPF flotante", GroupName = "Panel", Order = 1, Description = "Usa panel WPF flotante (recomendado). Falso = panel SharpDX alternativo dentro del chart.")]
		public bool UseWpfPanel { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Boton en toolbar superior", GroupName = "Panel", Order = 2, Description = "Anade un boton 'ILT-DEV Heatmap' en la barra superior del chart que muestra/oculta el panel.")]
		public bool ShowToolbarButton { get; set; }

		[Display(Name = "Debug render (log de errores)", GroupName = "Panel", Order = 3, Description = "Imprime en el Output cualquier excepción capturada dentro de OnRender (máx. 1 aviso cada 5 s). Dejar en falso en producción.")]
		public bool DebugRender { get; set; }

		[Browsable(false)]
		public double PanelXW { get; set; }

		[Browsable(false)]
		public double PanelYW { get; set; }

		[Browsable(false)]
		public bool PanelMinimizedPersist { get; set; }

		[Display(Name = "Ver Heatmap + Marcas", GroupName = "01 Visual básico", Order = 1, Description = "Toggle único de la capa básica. El motor sigue recopilando datos para ILT-DEV EVENTS e ILT-DEV S/R aunque esta capa esté oculta.")]
		public bool VerVisualBasico
		{
			get
			{
				return _pBasicVisual;
			}
			set
			{
				_pBasicVisual = value;
				_pPassiveChartMarkers = value;
			}
		}

		[Display(Name = "Filtro Heatmap + Marcas (lotes)", GroupName = "01 Visual básico", Order = 2, Description = "Filtro visual compartido: la misma cantidad mínima gobierna las celdas del heatmap y las marcas micro del gráfico.")]
		[NinjaScriptProperty]
		[Range(1, 500)]
		public int FiltroQty
		{
			get
			{
				return _pF;
			}
			set
			{
				_pF = Math.Max(1, Math.Min(500, value));
			}
		}

		[Display(Name = "Filtro Ladder DOM (lotes)", GroupName = "Heatmap", Order = 2, Description = "Filtro visual independiente para ocultar filas pequenas del ladder DOM. No afecta el heatmap.")]
		[NinjaScriptProperty]
		[Range(1, 500)]
		public int LadderFiltroQty
		{
			get
			{
				return _pLadF;
			}
			set
			{
				_pLadF = Math.Max(1, Math.Min(500, value));
			}
		}

		[Display(Name = "Desplazamiento Ladder Izq (px)", GroupName = "Heatmap", Order = 3, Description = "Mueve el DOM Ladder hacia la izquierda desde el borde derecho. 0 mantiene la posicion original.")]
		[NinjaScriptProperty]
		[Range(0, 10000)]
		public int LadderOffsetPx
		{
			get
			{
				return _pLadderShiftPx;
			}
			set
			{
				_pLadderShiftPx = Math.Max(0, Math.Min(10000, value));
			}
		}

		[NinjaScriptProperty]
		[Display(Name = "Tick Fijo", GroupName = "Heatmap", Order = 4, Description = "Lectura fija a 1 tick. No agrupa precios ni burbujas.")]
		[Range(1, 20)]
		public int AgruparTk
		{
			get
			{
				return 1;
			}
			set
			{
				_pG = 1;
			}
		}

		[Display(Name = "Umbral Market Pulse (lotes)", GroupName = "Avanzado - Market Pulse", Order = 3, Description = "Filtro minimo para dibujar niveles pasivos dentro del Market Pulse/liquidity histogram. No afecta Heatmap ni DOM Ladder.")]
		[NinjaScriptProperty]
		[Range(1, 500)]
		public int UmbralLin
		{
			get
			{
				return _pL;
			}
			set
			{
				_pL = Math.Max(1, Math.Min(500, value));
			}
		}

		[Display(Name = "Min captura T&S (lotes)", GroupName = "02 ILT-DEV EVENTS", Order = 12, Description = "Piso de captura del motor T&S. Alimenta absorción, ataques a muro, ILT-DEV EVENT e ILT-DEV S/R; no dibuja burbujas. Bajarlo mejora la fidelidad a costa de memoria. Recomendado 5-10.")]
		[Range(1, 200)]
		[NinjaScriptProperty]
		public int MinBurbuja
		{
			get
			{
				return _pBub;
			}
			set
			{
				_pBub = Math.Max(1, Math.Min(200, value));
			}
		}

		[Browsable(false)]
		[NinjaScriptProperty]
		[Range(1, 2000)]
		[Display(Name = "Bloque Institucional (lotes)", GroupName = "Niveles", Order = 5, Description = "Trades por encima de este umbral se marcan como bloques institucionales (borde blanco, mas grande)")]
		public int UmbralBloque
		{
			get
			{
				return _pBlock;
			}
			set
			{
				_pBlock = Math.Max(1, Math.Min(2000, value));
			}
		}

		[Browsable(false)]
		[XmlIgnore]
		public int MaxBurbujasHistoricas
		{
			get
			{
				return 350000;
			}
			set
			{
				_pMaxHistoricalBubbles = 350000;
			}
		}

		[Browsable(false)]
		[XmlIgnore]
		public bool MantenerBurbujasHistoricas
		{
			get
			{
				return true;
			}
			set
			{
				_pKeepHistoricalBubbles = true;
			}
		}

		[Display(Name = "Ver Ladder", GroupName = "Visual", Order = 5)]
		[NinjaScriptProperty]
		public bool VerLadder
		{
			get
			{
				return _pLad;
			}
			set
			{
				_pLad = value;
			}
		}

		[Browsable(false)]
		[NinjaScriptProperty]
		[Display(Name = "Ver Market Pulse", GroupName = "Visual", Order = 6, Description = "V64.30: retirado del panel por redundante con DOM Flow Pulse. Render off por defecto. Editable aquí si se necesita.")]
		public bool VerLineas
		{
			get
			{
				return _pLin;
			}
			set
			{
				_pLin = value;
			}
		}

		[NinjaScriptProperty]
		[Display(Name = "Ver Stacking", GroupName = "Visual", Order = 7)]
		public bool VerStacking
		{
			get
			{
				return _pSt;
			}
			set
			{
				_pSt = value;
			}
		}

		[NinjaScriptProperty]
		[Display(Name = "Ver Pulling", GroupName = "Visual", Order = 8)]
		public bool VerPulling
		{
			get
			{
				return _pPull;
			}
			set
			{
				_pPull = value;
			}
		}

		[Browsable(false)]
		public bool VerBurbujas
		{
			get
			{
				return false;
			}
			set
			{
				_pBubShow = false;
			}
		}

		[NinjaScriptProperty]
		[Display(Name = "Ver Absorcion", GroupName = "Visual", Order = 10, Description = "Muestra cuando una orden pasiva esta absorbiendo agresion del T&S")]
		public bool VerAbsorcion
		{
			get
			{
				return _pAbs;
			}
			set
			{
				_pAbs = value;
			}
		}

		[NinjaScriptProperty]
		[Display(Name = "Bidireccional", GroupName = "Visual", Order = 11)]
		public bool Bidireccional
		{
			get
			{
				return _pDir;
			}
			set
			{
				_pDir = value;
			}
		}

		[NinjaScriptProperty]
		[Display(Name = "Ver DOM Flow Pulse", GroupName = "Visual", Order = 12, Description = "Muestra una capa animada de cambios del DOM: stacking/add y pulling/remove.")]
		public bool VerDomFlowPulse
		{
			get
			{
				return _pFlow;
			}
			set
			{
				_pFlow = value;
			}
		}

		[Display(Name = "Flow Min Change", GroupName = "DOM Flow Pulse", Order = 1, Description = "Cambio minimo de contratos en el DOM para crear pulso visual de stacking/pulling.")]
		[NinjaScriptProperty]
		[Range(1, 500)]
		public int FlowMinChange
		{
			get
			{
				return _pFlowMin;
			}
			set
			{
				_pFlowMin = Math.Max(1, Math.Min(500, value));
			}
		}

		[Display(Name = "Flow Fade MS", GroupName = "DOM Flow Pulse", Order = 2, Description = "Duracion del desvanecimiento de los pulsos DOM en milisegundos.")]
		[Range(300, 9000)]
		[NinjaScriptProperty]
		public int FlowFadeMs
		{
			get
			{
				return _pFlowFadeMs;
			}
			set
			{
				_pFlowFadeMs = Math.Max(300, Math.Min(9000, value));
			}
		}

		[Display(Name = "Flow Opacity %", GroupName = "DOM Flow Pulse", Order = 3)]
		[NinjaScriptProperty]
		[Range(0, 100)]
		public int FlowOpacity
		{
			get
			{
				return _pFlowOpacity;
			}
			set
			{
				_pFlowOpacity = Math.Max(0, Math.Min(100, value));
			}
		}

		[Display(Name = "Flow Size", GroupName = "DOM Flow Pulse", Order = 4)]
		[NinjaScriptProperty]
		[Range(1, 5)]
		public int FlowSize
		{
			get
			{
				return _pFlowSize;
			}
			set
			{
				_pFlowSize = Math.Max(1, Math.Min(5, value));
			}
		}

		[Display(Name = "Flow Label Min", GroupName = "DOM Flow Pulse", Order = 5, Description = "Contrato minimo para mostrar etiqueta +qty/-qty en el pulso DOM.")]
		[NinjaScriptProperty]
		[Range(1, 500)]
		public int FlowLabelMin
		{
			get
			{
				return _pFlowLabelMin;
			}
			set
			{
				_pFlowLabelMin = Math.Max(1, Math.Min(500, value));
			}
		}

		[NinjaScriptProperty]
		[Display(Name = "Flow Refresh FPS", GroupName = "DOM Flow Pulse", Order = 6, Description = "Frecuencia de refresco visual del pulso. 24-30 suele ser fluido sin cargar demasiado CPU.")]
		[Range(8, 30)]
		public int FlowRefreshFps
		{
			get
			{
				return _pFlowFps;
			}
			set
			{
				_pFlowFps = Math.Max(8, Math.Min(30, value));
			}
		}

		[Display(Name = "Heatmap Min Opacity %", GroupName = "Heatmap Visual", Order = 1, Description = "Opacidad minima de ordenes pequeñas. Baja este valor para que solo destaquen las ordenes grandes.")]
		[NinjaScriptProperty]
		[Range(0, 60)]
		public int HeatmapMinOpacity
		{
			get
			{
				return _pHeatmapMinOpacity;
			}
			set
			{
				_pHeatmapMinOpacity = Math.Max(0, Math.Min(60, value));
			}
		}

		[NinjaScriptProperty]
		[Display(Name = "Heatmap Priority Contrast", GroupName = "Heatmap Visual", Order = 2, Description = "100=lineal. 150-250 prioriza liquidez grande y deja tenue la pequeña.")]
		[Range(50, 400)]
		public int HeatmapPriorityContrast
		{
			get
			{
				return _pHeatmapContrast;
			}
			set
			{
				_pHeatmapContrast = Math.Max(50, Math.Min(400, value));
			}
		}

		[Browsable(false)]
		[Range(0, 6)]
		public int HeatmapModoColor
		{
			get
			{
				return _pHeatColorMode;
			}
			set
			{
				_pHeatColorMode = Math.Max(0, Math.Min(6, value));
			}
		}

		[Display(Name = "Paleta Heatmap", GroupName = "Heatmap Visual", Order = 4, Description = "Custom usa los colores Bid/Ask editables. Las otras seis opciones son paletas térmicas de intensidad estilo Bookmap.")]
		public IltDevHeatmapPaletteDev PaletaHeatmap
		{
			get
			{
				return (IltDevHeatmapPaletteDev)Math.Max(0, Math.Min(6, _pHeatColorMode));
			}
			set
			{
				_pHeatColorMode = Math.Max(0, Math.Min(6, (int)value));
			}
		}

		[Display(Name = "Heatmap Escala Log", GroupName = "Heatmap Visual", Order = 5, Description = "Escala logarítmica de intensidad tipo Bookmap: conserva la textura de todo el libro aunque exista un muro gigante dominando la escala. Desactívalo para volver a la escala lineal clásica.")]
		public bool HeatmapEscalaLog
		{
			get
			{
				return _pHeatLogScale;
			}
			set
			{
				_pHeatLogScale = value;
			}
		}

		[Display(Name = "Ventana Heatmap % (ancho)", GroupName = "Heatmap Visual", Order = 7, Description = "Porción DERECHA reservada exclusivamente para heatmap/bitmap, marcas micro y estados básicos MURO/SPOOF/REFILL/ICEBERG. No recorta DEF, ILT-DEV EVENT, ILT-DEV SUPPORT/RESIST ni ILT-DEV S/R. ROTO/FADE son estados internos sin render. 100 = todo el gráfico.")]
		[Range(15, 100)]
		public int VentanaRenderPct
		{
			get
			{
				return _pRenderWinPct;
			}
			set
			{
				_pRenderWinPct = Math.Max(15, Math.Min(100, value));
			}
		}

		[Display(Name = "Velocidad Cinta Heatmap", GroupName = "Heatmap Visual", Order = 8, Description = "Cadencia visual del bitmap y marcas MURO/SPOOF/+/-/flechas. Rango ampliado V64.38: 1=muy lenta (600ms), 4=normal (250ms), 7=rápida (90ms), 10=máxima (30ms). No modifica el motor de cálculo.")]
		[Range(1, 10)]
		public int HeatmapScrollSpeed
		{
			get
			{
				return _pHeatmapScrollSpeed;
			}
			set
			{
				_pHeatmapScrollSpeed = Math.Max(1, Math.Min(10, value));
			}
		}

		[Display(Name = "Heatmap Bitmap Scroll (BMP)", GroupName = "Heatmap Visual", Order = 6, Description = "Técnica de Bookmap: el carril vivo se pinta una sola vez por columna en un bitmap con scroll. Cada evento queda clavado en el píxel (tiempo, precio) donde nació y avanza con el mercado. Máxima nitidez y mínimo costo GPU. Requiere Modo Color = 1.")]
		public bool HeatmapBitmapScroll
		{
			get
			{
				return _pHmScrollBmp;
			}
			set
			{
				_pHmScrollBmp = value;
				if (value)
				{
					_hmBmpFail = false;
				}
			}
		}

		[Display(Name = "Escala Institucional % (compat)", GroupName = "Motor Pasivo", Order = 40, Description = "Tamaño mínimo de los estados tácticos (SPOOF, ICEBERG, ILT-DEV EVENT) como % del BLOQUE T&S. 200 = el muro debió mostrar al menos 2x BLOQUE. Filtra micro-muros sin valor operativo: 7 lotes nunca son un spoof.")]
		[Range(100, 600)]
		[Browsable(false)]
		public int EscalaInstitucionalPct
		{
			get
			{
				return _pInstScalePct;
			}
			set
			{
				_pInstScalePct = Math.Max(100, Math.Min(600, value));
			}
		}

		[Display(Name = "Ver ILT-DEV EVENTS", GroupName = "02 ILT-DEV EVENTS", Order = 1, Description = "Activa exclusivamente la creación, evaluación y render de ILT-DEV EVENT. No depende del heatmap visible ni de Muros básicos.")]
		public bool VerIltDevZones
		{
			get
			{
				return _pIltDevZones;
			}
			set
			{
				_pIltDevZones = value;
			}
		}

		[Display(Name = "BigTrade T&S agresivos (lotes)", GroupName = "02 ILT-DEV EVENTS", Order = 2, Description = "Tamaño agresivo institucional (perilla única). Define qué print/ataque cuenta como big trader: alimenta el ataque del ILT-DEV EVENT y también el bloque que inicia la S/R. El percentil adaptativo lo eleva según la actividad reciente. Default 10.")]
		[Range(1, 2000)]
		public int IltDevZoneAtaqueMinQty
		{
			get
			{
				return _pIltDevZoneAttackMinQty;
			}
			set
			{
				_pIltDevZoneAttackMinQty = Math.Max(1, Math.Min(2000, value));
			}
		}

		[Browsable(false)]
		[Display(Name = "Escala institucional %", GroupName = "02 ILT-DEV EVENTS", Order = 3, Description = "Escala propia de ILT-DEV EVENT respecto a Ataque motor. No modifica BT visual ni ILT-DEV S/R.")]
		[Range(100, 600)]
		public int IltDevZoneEscalaPct
		{
			get
			{
				return _pIltDevZoneScalePct;
			}
			set
			{
				_pIltDevZoneScalePct = Math.Max(100, Math.Min(600, value));
			}
		}

		[Display(Name = "Banda visual/lógica (ticks)", GroupName = "02 ILT-DEV EVENTS", Order = 5, Description = "Media banda de precio de la ILT-DEV EVENT. Se usa tanto en su geometría como en la lectura estructural del evento.")]
		[Range(1, 20)]
		[Browsable(false)]
		public int IltDevZoneBandaTicks
		{
			get
			{
				return _pIltDevZoneBandTicks;
			}
			set
			{
				_pIltDevZoneBandTicks = Math.Max(1, Math.Min(20, value));
			}
		}

		[Range(20, 2000)]
		[Display(Name = "Máx barras candidato", GroupName = "02 ILT-DEV EVENTS", Order = 6, Description = "Retención exclusiva del pipeline EN DEFENSA antes de confirmarse. Las ILT-DEV EVENT confirmadas siguen siendo fijas hasta interacción.")]
		[Browsable(false)]
		public int IltDevZoneCandidatoMaxBars
		{
			get
			{
				return _pIltDevZoneCandidateMaxBars;
			}
			set
			{
				_pIltDevZoneCandidateMaxBars = Math.Max(20, Math.Min(2000, value));
			}
		}

		[Browsable(false)]
		[Display(Name = "Historial resuelto (barras)", GroupName = "02 ILT-DEV EVENTS", Order = 7, Description = "Retención propia de ILT-DEV EVENT ya mitigadas/rotas/FADE. No usa parámetros de ILT-DEV S/R.")]
		[Range(100, 10000)]
		public int IltDevZoneHistorialBars
		{
			get
			{
				return _pIltDevZoneHistoryBars;
			}
			set
			{
				_pIltDevZoneHistoryBars = Math.Max(100, Math.Min(10000, value));
			}
		}

		[Display(Name = "Ataque adaptativo percentil", GroupName = "02 ILT-DEV EVENTS", Order = 8, Description = "0 desactiva. Con 75, el piso manual se eleva suavemente al percentil 75 de los ataques institucionales de los últimos 60 segundos, con tope 3x.")]
		[Range(0, 95)]
		[Browsable(false)]
		public int IltDevZoneAtaqueAdaptativoPct
		{
			get
			{
				return _pIltDevZoneAdaptivePct;
			}
			set
			{
				_pIltDevZoneAdaptivePct = Math.Max(0, Math.Min(95, value));
				_iltDevZoneAdaptiveLastCalc = -1.0;
			}
		}

		[Display(Name = "Fuerza mínima F", GroupName = "02 ILT-DEV EVENTS", Order = 9, Description = "Fuerza normalizada mínima para promover y mostrar ILT-DEV EVENT. F no es probabilidad ni contratos; resume ataque, absorción, rechazo, cierres y confluencia.")]
		[Range(0, 100)]
		public int IltDevZoneFuerzaMin
		{
			get
			{
				return _pIltDevZoneMinStrength;
			}
			set
			{
				_pIltDevZoneMinStrength = Math.Max(0, Math.Min(100, value));
			}
		}

		[Browsable(false)]
		[Display(Name = "Fusión zonas (ticks)", GroupName = "02 ILT-DEV EVENTS", Order = 10, Description = "Distancia máxima para que defensas del mismo lado refuercen una sola ILT-DEV EVENT en vez de crear líneas duplicadas.")]
		[Range(1, 20)]
		public int IltDevZoneFusionTicks
		{
			get
			{
				return _pIltDevZoneMergeTicks;
			}
			set
			{
				_pIltDevZoneMergeTicks = Math.Max(1, Math.Min(20, value));
			}
		}

		[Display(Name = "Fusión zonas (seg)", GroupName = "02 ILT-DEV EVENTS", Order = 11, Description = "Ventana temporal de fusión. Una zona vigente y virgen cercana también puede recibir un refuerzo posterior sin mover su vela de origen.")]
		[Browsable(false)]
		[Range(10, 600)]
		public int IltDevZoneFusionSeg
		{
			get
			{
				return _pIltDevZoneMergeSec;
			}
			set
			{
				_pIltDevZoneMergeSec = Math.Max(10, Math.Min(600, value));
			}
		}

		[Display(Name = "ILT-DEV-EVENT Cierres de confirmación", GroupName = "02 ILT-DEV EVENTS", Order = 4, Description = "Cierres de vela a favor del nivel necesarios para promover una defensa a ILT-DEV EVENT. Los cierres son aceptación; las mechas son exploración. 2 = equilibrio entre solidez y llegar a tiempo al retest.")]
		[Range(1, 3)]
		public int ZoneCierresConfirmacion
		{
			get
			{
				return _pZoneCloses;
			}
			set
			{
				_pZoneCloses = Math.Max(1, Math.Min(3, value));
			}
		}

		[Browsable(false)]
		[Range(1, 40)]
		[Display(Name = "Banda S/R (ticks)", GroupName = "03 ILT-DEV S/R", Order = 5, Description = "Media banda de las zonas S/R y tolerancia de matching/confluencia, en ticks. En Range 40 de NQ, 4-8 es realista; 2 era ruido. Hasta 40 = tamaño de vela completa.")]
		public int SRBandaTicks
		{
			get
			{
				return _pSRBandTicks;
			}
			set
			{
				_pSRBandTicks = Math.Max(1, Math.Min(40, value));
			}
		}

		[Display(Name = "Vigencia etiqueta evento (s)", GroupName = "Liquidez Pasiva", Order = 46, Description = "Segundos que una etiqueta táctica instantánea (SPOOF/ROTO de muro) permanece visible. Después, la línea vuelve a leer MURO o ILT-DEV SUPPORT/RESIST y el rastro queda en el heatmap.")]
		[Range(10, 600)]
		public int VigenciaEventoSeg
		{
			get
			{
				return _pTagLifeSec;
			}
			set
			{
				_pTagLifeSec = Math.Max(10, Math.Min(600, value));
			}
		}

		[Display(Name = "Ver ILT-DEV S/R", GroupName = "03 ILT-DEV S/R PERSISTENTE", Order = 1, Description = "Memoria transaccional institucional: BigTrade + re-add pasivo del lado absorbido = nivel de referencia S/R. Estados: FRESCA, TESTEADA, ROTA. Sus filtros de tamaño son independientes (abajo).")]
		public bool SRZonesActivas
		{
			get
			{
				return _pSRZones;
			}
			set
			{
				_pSRZones = value;
			}
		}

		[Range(1, 2000)]
		[Display(Name = "BigTrade agresivos (lotes)", GroupName = "03 ILT-DEV S/R PERSISTENTE", Order = 2, Description = "V64.28: umbral agresivo PROPIO del S/R Persistente (independiente del ILT-DEV EVENT y del Histórico). Tamaño del BigTrade que inicia un nivel persistente. Subir = más selectivo.")]
		public int SRPersistBig
		{
			get
			{
				return _pSRPersistBig;
			}
			set
			{
				_pSRPersistBig = Math.Max(1, Math.Min(2000, value));
			}
		}

		[Display(Name = "Pasiva / muro (lotes)", GroupName = "03 ILT-DEV S/R PERSISTENTE", Order = 3, Description = "V64.28: umbral pasivo PROPIO del S/R Persistente. Tamaño mínimo del muro/re-add que confirma el nivel. Subir = exige más liquidez posada.")]
		[Range(1, 2000)]
		public int SRPersistPass
		{
			get
			{
				return _pSRPersistPass;
			}
			set
			{
				_pSRPersistPass = Math.Max(1, Math.Min(2000, value));
			}
		}

		[Display(Name = "Ver ILT-DEV Bracket", GroupName = "04 ILT-DEV BRACKET", Order = 1, Description = "Mapa estratégico gris punteado: donde hay agresivos (BigTrade Hist) EN CONFLUENCIA con liquidez pasiva (Pasiva Hist, requisito) se marca una línea. No depende de la fuerza F. DEFENDIDO brillante / HUÉRFANO si la pasiva se retira.")]
		public bool SRHistActivo
		{
			get
			{
				return _pSRHist;
			}
			set
			{
				_pSRHist = value;
			}
		}

		[Display(Name = "BigTrade agresivos (lotes)", GroupName = "04 ILT-DEV BRACKET", Order = 2, Description = "V64.28: umbral agresivo PROPIO del ILT-DEV Bracket (absoluto, independiente). Tamaño del BigTrade que puede marcar el mapa estratégico. Más exigente que el evento por default.")]
		[Range(1, 5000)]
		public int SRHistBig
		{
			get
			{
				return _pSRHistBig;
			}
			set
			{
				_pSRHistBig = Math.Max(1, Math.Min(5000, value));
			}
		}

		[Range(1, 2000)]
		[Display(Name = "Pasiva confluencia (lotes)", GroupName = "04 ILT-DEV BRACKET", Order = 3, Description = "V64.28: umbral pasivo PROPIO del ILT-DEV Bracket, usado como REQUISITO: la liquidez pasiva del lado defensor dentro de la ventana debe alcanzarlo para que la línea aparezca. También decide DEFENDIDO vs HUÉRFANO.")]
		public int SRHistPass
		{
			get
			{
				return _pSRHistPass;
			}
			set
			{
				_pSRHistPass = Math.Max(1, Math.Min(2000, value));
			}
		}

		[Display(Name = "Ventana confluencia (puntos)", GroupName = "04 ILT-DEV BRACKET", Order = 4, Description = "Rango en puntos alrededor del precio donde se busca la confluencia pasiva del Histórico.")]
		[Range(1, 20)]
		public int SRHistWinPts
		{
			get
			{
				return _pSRHistWinPts;
			}
			set
			{
				_pSRHistWinPts = Math.Max(1, Math.Min(20, value));
			}
		}

		[Range(100, 600)]
		[Display(Name = "Escala institucional %", GroupName = "03 ILT-DEV S/R", Order = 2, Description = "Escala propia de BigTrade/muro para ILT-DEV S/R. No modifica ILT-DEV EVENT.")]
		[Browsable(false)]
		public int IltDevSREscalaPct
		{
			get
			{
				return _pIltDevSRScalePct;
			}
			set
			{
				_pIltDevSRScalePct = Math.Max(100, Math.Min(600, value));
			}
		}

		[Range(10, 100)]
		[Display(Name = "Re-add mínimo %", GroupName = "03 ILT-DEV S/R", Order = 3, Description = "Porcentaje del BigTrade que debe reponerse del lado absorbido para formar la memoria S/R.")]
		[Browsable(false)]
		public int IltDevSRReAddPct
		{
			get
			{
				return _pIltDevSRReAddPct;
			}
			set
			{
				_pIltDevSRReAddPct = Math.Max(10, Math.Min(100, value));
			}
		}

		[Display(Name = "Ventana re-add (ms)", GroupName = "03 ILT-DEV S/R", Order = 4)]
		[Range(1000, 30000)]
		[Browsable(false)]
		public int IltDevSRVentanaMs
		{
			get
			{
				return _pIltDevSRWindowMs;
			}
			set
			{
				_pIltDevSRWindowMs = Math.Max(1000, Math.Min(30000, value));
			}
		}

		[Display(Name = "Fuerza mínima visual", GroupName = "03 ILT-DEV S/R", Order = 6, Description = "Oculta ILT-DEV SUPPORT/RESIST por debajo de Fxx sin eliminar el motor ni la memoria del nivel.")]
		[Range(0, 100)]
		[Browsable(false)]
		public int IltDevSRFuerzaMin
		{
			get
			{
				return _pIltDevSRMinStrength;
			}
			set
			{
				_pIltDevSRMinStrength = Math.Max(0, Math.Min(100, value));
			}
		}

		[Browsable(false)]
		[Range(2, 30)]
		[Display(Name = "Muro presencia mínima", GroupName = "03 ILT-DEV S/R", Order = 7)]
		public int IltDevSRMuroPresenciaMin
		{
			get
			{
				return _pIltDevSRWallMinPresence;
			}
			set
			{
				_pIltDevSRWallMinPresence = Math.Max(2, Math.Min(30, value));
			}
		}

		[Range(100, 10000)]
		[Browsable(false)]
		[Display(Name = "Muro edad mínima (ms)", GroupName = "03 ILT-DEV S/R", Order = 8)]
		public int IltDevSRMuroEdadMinMs
		{
			get
			{
				return _pIltDevSRWallMinAgeMs;
			}
			set
			{
				_pIltDevSRWallMinAgeMs = Math.Max(100, Math.Min(10000, value));
			}
		}

		[Browsable(false)]
		[Range(500, 15000)]
		[Display(Name = "Muro vigencia máxima (ms)", GroupName = "03 ILT-DEV S/R", Order = 9)]
		public int IltDevSRMuroFreshMs
		{
			get
			{
				return _pIltDevSRWallFreshMs;
			}
			set
			{
				_pIltDevSRWallFreshMs = Math.Max(500, Math.Min(15000, value));
			}
		}

		[Display(Name = "Retención S/R (barras)", GroupName = "03 ILT-DEV S/R", Order = 10, Description = "Historial propio de ILT-DEV S/R táctica. Los ILT-DEV SUPPORT/RESIST vigentes siguen sujetos solo a su invalidación por aceptación del precio.")]
		[Range(100, 10000)]
		[Browsable(false)]
		public int IltDevSRRetencionBars
		{
			get
			{
				return _pIltDevSRRetentionBars;
			}
			set
			{
				_pIltDevSRRetentionBars = Math.Max(100, Math.Min(10000, value));
			}
		}

		[Display(Name = "SPOOF Proximidad (ticks)", GroupName = "Liquidez Pasiva", Order = 43, Description = "Distancia máxima del precio al muro (en ticks) durante la ráfaga de cancelación para calificar como SPOOF. En NQ, 24 ticks = 6 puntos. Menor = más estricto.")]
		[Range(4, 100)]
		public int SpoofProximidadTicks
		{
			get
			{
				return _pSpoofNearTicks;
			}
			set
			{
				_pSpoofNearTicks = Math.Max(4, Math.Min(100, value));
			}
		}

		[Range(0, 20)]
		[Display(Name = "FADE Ventana (velas)", GroupName = "Liquidez Pasiva", Order = 42, Description = "Si un nivel ROTO es recuperado por un CIERRE dentro de esta cantidad de velas, se marca FADE: ruptura fallida, la agresión que rompió quedó atrapada. 0 = desactivado.")]
		public int FadeVentanaVelas
		{
			get
			{
				return _pFadeBars;
			}
			set
			{
				_pFadeBars = Math.Max(0, Math.Min(20, value));
			}
		}

		[Display(Name = "Heatmap Bar Width %", GroupName = "Heatmap Visual", Order = 3, Description = "Ancho horizontal del heatmap por barra. 100=ancho normal; 150-185 da más presencia al heatmap ILT-DEV.")]
		[NinjaScriptProperty]
		[Range(50, 250)]
		public int HeatmapBarWidthPct
		{
			get
			{
				return _pHeatmapBarWidth;
			}
			set
			{
				_pHeatmapBarWidth = Math.Max(50, Math.Min(250, value));
			}
		}

		[Display(Name = "DOM Flow Panel Opacity %", GroupName = "DOM Flow Pulse - Visual", Order = 4, Description = "Opacidad del fondo fijo del panel DOM Flow. Sube este valor para separar mejor el pulso del mercado.")]
		[Range(0, 60)]
		[NinjaScriptProperty]
		public int FlowPanelOpacity
		{
			get
			{
				return _pFlowPanelOpacity;
			}
			set
			{
				_pFlowPanelOpacity = Math.Max(0, Math.Min(60, value));
			}
		}

		[Browsable(false)]
		public bool VerLiquidezPasiva
		{
			get
			{
				return true;
			}
			set
			{
				_pPassive = true;
			}
		}

		[Range(1, 1000)]
		[Display(Name = "Contratos pasivo (institucional)", GroupName = "Liquidez Pasiva", Order = 2, Description = "Mínimo de contratos visibles por nivel para sumar al motor de Liquidez Pasiva.")]
		public int LiquidezPasivaMinQty
		{
			get
			{
				return _pPassiveMinQty;
			}
			set
			{
				_pPassiveMinQty = Math.Max(1, Math.Min(1000, value));
			}
		}

		[Display(Name = "Profundidad Liquidez Pasiva", GroupName = "Liquidez Pasiva", Order = 3, Description = "Cantidad de niveles cercanos al best bid/ask usados para calcular presión pasiva.")]
		[Range(1, 20)]
		public int LiquidezPasivaProfundidad
		{
			get
			{
				return _pPassiveDepth;
			}
			set
			{
				_pPassiveDepth = Math.Max(1, Math.Min(20, value));
			}
		}

		[Browsable(false)]
		[Display(Name = "Evento Min Motor", GroupName = "Motor Pasivo", Order = 4, Description = "Cambio mínimo de contratos para mostrar eventos de retiro, consumo o relleno.")]
		[Range(1, 1000)]
		public int LiquidezPasivaEventoMin
		{
			get
			{
				return _pPassiveEventMin;
			}
			set
			{
				_pPassiveEventMin = Math.Max(1, Math.Min(1000, value));
			}
		}

		[Display(Name = "Ventana Agresión MS", GroupName = "Liquidez Pasiva", Order = 5, Description = "Ventana para clasificar una reducción del libro como consumo por ejecución usando T&S reciente.")]
		[Range(120, 2000)]
		public int LiquidezPasivaAggWindowMs
		{
			get
			{
				return _pPassiveAggWindowMs;
			}
			set
			{
				_pPassiveAggWindowMs = Math.Max(120, Math.Min(2000, value));
			}
		}

		[Range(250, 5000)]
		[Display(Name = "Ventana Relleno MS", GroupName = "Liquidez Pasiva", Order = 6, Description = "Ventana para detectar relleno probable después de consumo/reducción del nivel.")]
		public int LiquidezPasivaRefillWindowMs
		{
			get
			{
				return _pPassiveRefillWindowMs;
			}
			set
			{
				_pPassiveRefillWindowMs = Math.Max(250, Math.Min(5000, value));
			}
		}

		[Browsable(false)]
		[Display(Name = "Ver Eventos en Gráfica (compat)", GroupName = "01 Visual básico", Order = 7, Description = "Muestra marcas visuales no invasivas en el precio cuando hay retiro, consumo o relleno de liquidez pasiva.")]
		public bool VerEventosLiquidezPasiva
		{
			get
			{
				return _pBasicVisual;
			}
			set
			{
				_pBasicVisual = value;
				_pPassiveChartMarkers = value;
			}
		}

		[Display(Name = "Ver Muros Persistentes", GroupName = "Liquidez Pasiva", Order = 8, Description = "Muestra u oculta la capa de MUROS (y SPOOF). También está el botón VER MUROS en el panel. El scoring de muros sigue alimentando ILT-DEV EVENT y S/R esté visible o no.")]
		public bool VerMurosLiquidezPasiva
		{
			get
			{
				return _pPassiveWallZones;
			}
			set
			{
				_pPassiveWallZones = value;
			}
		}

		[Range(1, 2000)]
		[Display(Name = "Filtro Muros (lotes)", GroupName = "Liquidez Pasiva", Order = 9, Description = "V64.39: tamaño mínimo PROPIO para que una liquidez pasiva califique como MURO (independiente de Contratos pasivo, que es el piso general del motor). Subir = solo muros grandes, menos y más de peso.")]
		public int MuroMinQty
		{
			get
			{
				return _pWallMinQty;
			}
			set
			{
				_pWallMinQty = Math.Max(1, Math.Min(2000, value));
			}
		}

		[Display(Name = "Máx Barras Muro", GroupName = "Liquidez Pasiva", Order = 11, Description = "Máximo de barras que se conserva el rastro de un muro persistente para revisar la actividad en ese nivel.")]
		[Range(20, 2000)]
		public int LiquidezPasivaMuroMaxBars
		{
			get
			{
				return _pPassiveWallMaxBars;
			}
			set
			{
				_pPassiveWallMaxBars = Math.Max(20, Math.Min(2000, value));
			}
		}

		[Range(1, 5)]
		[Display(Name = "Tamaño Marca Evento", GroupName = "Liquidez Pasiva", Order = 10, Description = "Tamaño de la marca visual de eventos de liquidez pasiva en el gráfico.")]
		public int LiquidezPasivaMarkerSize
		{
			get
			{
				return _pPassiveMarkerSize;
			}
			set
			{
				_pPassiveMarkerSize = Math.Max(1, Math.Min(5, value));
			}
		}

		[Display(Name = "Etiqueta Min Evento (compat)", GroupName = "01 Visual básico", Order = 11, Description = "Mínimo de contratos para mantener etiqueta junto a la marca del evento. Eventos menores se ven como punto/anillo.")]
		[Browsable(false)]
		[Range(1, 1000)]
		public int LiquidezPasivaLabelMin
		{
			get
			{
				return _pF;
			}
			set
			{
				_pF = Math.Max(1, Math.Min(500, value));
				_pPassiveMarkerLabelMin = _pF;
			}
		}

		[NinjaScriptProperty]
		[Display(Name = "Ver POC Historico", GroupName = "Analisis", Order = 14, Description = "Muestra el DOM POC pasivo - precio con mayor liquidez pasiva agregada desde el book")]
		public bool VerPOC
		{
			get
			{
				return _pPoc;
			}
			set
			{
				_pPoc = value;
			}
		}

		[Display(Name = "Heatmap Bid Color", GroupName = "Colores", Order = 100)]
		[XmlIgnore]
		public System.Windows.Media.Brush HeatmapBidColor { get; set; }

		[Browsable(false)]
		public string HeatmapBidColorSerializable
		{
			get
			{
				return Serialize.BrushToString(HeatmapBidColor);
			}
			set
			{
				HeatmapBidColor = Serialize.StringToBrush(value);
			}
		}

		[Display(Name = "Heatmap Ask Color", GroupName = "Colores", Order = 101)]
		[XmlIgnore]
		public System.Windows.Media.Brush HeatmapAskColor { get; set; }

		[Browsable(false)]
		public string HeatmapAskColorSerializable
		{
			get
			{
				return Serialize.BrushToString(HeatmapAskColor);
			}
			set
			{
				HeatmapAskColor = Serialize.StringToBrush(value);
			}
		}

		[XmlIgnore]
		[Display(Name = "Liquidez Bid Color", GroupName = "Colores", Order = 102)]
		public System.Windows.Media.Brush LiquidityBidColor { get; set; }

		[Browsable(false)]
		public string LiquidityBidColorSerializable
		{
			get
			{
				return Serialize.BrushToString(LiquidityBidColor);
			}
			set
			{
				LiquidityBidColor = Serialize.StringToBrush(value);
			}
		}

		[XmlIgnore]
		[Display(Name = "Liquidez Ask Color", GroupName = "Colores", Order = 103)]
		public System.Windows.Media.Brush LiquidityAskColor { get; set; }

		[Browsable(false)]
		public string LiquidityAskColorSerializable
		{
			get
			{
				return Serialize.BrushToString(LiquidityAskColor);
			}
			set
			{
				LiquidityAskColor = Serialize.StringToBrush(value);
			}
		}

		[XmlIgnore]
		[Range(1, 2000)]
		[Browsable(false)]
		[Display(Name = "BigTrade visual (lotes)", GroupName = "T&S / BigTrades", Order = 0, Description = "V64.47 — ÚNICO GATE VISUAL DEL T&S. Tamaño mínimo del cluster agresivo (mismo precio y lado, ventana de 280 ms) para dibujar la burbuja como BigTrade institucional. Es independiente de 'BigTrade T&S agresivos' del grupo 02 ILT-DEV EVENTS, que solo alimenta el motor de eventos: mover uno no altera el otro. Calibra por DENSIDAD, no por tamaño: ajusta hasta ver entre 20 y 60 BigTrades por sesión.")]
		public int BigTradeVisualMinQty
		{
			get
			{
				return _pBtVisualMin;
			}
			set
			{
				_pBtVisualMin = Math.Max(1, Math.Min(2000, value));
			}
		}

		[Browsable(false)]
		[Display(Name = "Auto BigTrade por instrumento", GroupName = "T&S / BigTrades", Order = 1)]
		public bool BigTradeAutoPorInstrumento
		{
			get
			{
				return _pBtAuto;
			}
			set
			{
				_pBtAuto = value;
			}
		}

		[Display(Name = "Color BigTrade Buy", GroupName = "T&S / BigTrades", Order = 3)]
		[XmlIgnore]
		[Browsable(false)]
		public System.Windows.Media.Brush BigTradeBuyColor { get; set; }

		[Browsable(false)]
		public string BigTradeBuyColorSerializable
		{
			get
			{
				return Serialize.BrushToString(BigTradeBuyColor);
			}
			set
			{
				BigTradeBuyColor = Serialize.StringToBrush(value);
			}
		}

		[XmlIgnore]
		[Display(Name = "Color BigTrade Sell", GroupName = "T&S / BigTrades", Order = 4)]
		[Browsable(false)]
		public System.Windows.Media.Brush BigTradeSellColor { get; set; }

		[Browsable(false)]
		public string BigTradeSellColorSerializable
		{
			get
			{
				return Serialize.BrushToString(BigTradeSellColor);
			}
			set
			{
				BigTradeSellColor = Serialize.StringToBrush(value);
			}
		}

		[Display(Name = "Text Color", GroupName = "Colores", Order = 110)]
		[XmlIgnore]
		public System.Windows.Media.Brush TextColor { get; set; }

		[Browsable(false)]
		public string TextColorSerializable
		{
			get
			{
				return Serialize.BrushToString(TextColor);
			}
			set
			{
				TextColor = Serialize.StringToBrush(value);
			}
		}

		[XmlIgnore]
		[Display(Name = "Dim Text Color", GroupName = "Colores", Order = 111)]
		public System.Windows.Media.Brush DimTextColor { get; set; }

		[Browsable(false)]
		public string DimTextColorSerializable
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

		[XmlIgnore]
		[Display(Name = "Flow Add / Stacking Color", GroupName = "DOM Flow Pulse - Visual", Order = 1)]
		public System.Windows.Media.Brush FlowAddColor { get; set; }

		[Browsable(false)]
		public string FlowAddColorSerializable
		{
			get
			{
				return Serialize.BrushToString(FlowAddColor);
			}
			set
			{
				FlowAddColor = Serialize.StringToBrush(value);
			}
		}

		[XmlIgnore]
		[Display(Name = "Flow Pulling Color", GroupName = "DOM Flow Pulse - Visual", Order = 2)]
		public System.Windows.Media.Brush FlowPullColor { get; set; }

		[Browsable(false)]
		public string FlowPullColorSerializable
		{
			get
			{
				return Serialize.BrushToString(FlowPullColor);
			}
			set
			{
				FlowPullColor = Serialize.StringToBrush(value);
			}
		}

		[XmlIgnore]
		[Display(Name = "Marca Absorción", GroupName = "Marcas Heatmap - Colores", Order = 1)]
		public System.Windows.Media.Brush AbsorptionMarkColor { get; set; }

		[Browsable(false)]
		public string AbsorptionMarkColorSerializable
		{
			get
			{
				return Serialize.BrushToString(AbsorptionMarkColor);
			}
			set
			{
				AbsorptionMarkColor = Serialize.StringToBrush(value);
			}
		}

		[XmlIgnore]
		[Display(Name = "Marca Refill", GroupName = "Marcas Heatmap - Colores", Order = 2)]
		public System.Windows.Media.Brush RefillMarkColor { get; set; }

		[Browsable(false)]
		public string RefillMarkColorSerializable
		{
			get
			{
				return Serialize.BrushToString(RefillMarkColor);
			}
			set
			{
				RefillMarkColor = Serialize.StringToBrush(value);
			}
		}

		[XmlIgnore]
		[Display(Name = "Marca Consumo", GroupName = "Marcas Heatmap - Colores", Order = 3)]
		public System.Windows.Media.Brush ConsumeMarkColor { get; set; }

		[Browsable(false)]
		public string ConsumeMarkColorSerializable
		{
			get
			{
				return Serialize.BrushToString(ConsumeMarkColor);
			}
			set
			{
				ConsumeMarkColor = Serialize.StringToBrush(value);
			}
		}

		[XmlIgnore]
		[Display(Name = "Flow Label Color", GroupName = "DOM Flow Pulse - Visual", Order = 3)]
		public System.Windows.Media.Brush FlowTextColor { get; set; }

		[Browsable(false)]
		public string FlowTextColorSerializable
		{
			get
			{
				return Serialize.BrushToString(FlowTextColor);
			}
			set
			{
				FlowTextColor = Serialize.StringToBrush(value);
			}
		}

		[XmlIgnore]
		[Browsable(false)]
		[Display(Name = "Bubble Border / 3D Accent Color", GroupName = "T&S Bubbles - Visual", Order = 3)]
		public System.Windows.Media.Brush BubbleBorderColor { get; set; }

		[Browsable(false)]
		public string BubbleBorderColorSerializable
		{
			get
			{
				return Serialize.BrushToString(BubbleBorderColor);
			}
			set
			{
				BubbleBorderColor = Serialize.StringToBrush(value);
			}
		}

		[XmlIgnore]
		[Display(Name = "Bubble Lightning Color", GroupName = "T&S Bubbles - Visual", Order = 4)]
		[Browsable(false)]
		public System.Windows.Media.Brush BubbleLightningColor { get; set; }

		[Browsable(false)]
		public string BubbleLightningColorSerializable
		{
			get
			{
				return Serialize.BrushToString(BubbleLightningColor);
			}
			set
			{
				BubbleLightningColor = Serialize.StringToBrush(value);
			}
		}

		[Range(0, 100)]
		[Display(Name = "Bubble Border Opacity %", GroupName = "T&S Bubbles - Visual", Order = 5)]
		[Browsable(false)]
		public int BubbleBorderOpacity { get; set; }

		[Display(Name = "Bubble Highlight Opacity %", GroupName = "T&S Bubbles - Visual", Order = 6)]
		[Browsable(false)]
		[Range(0, 100)]
		public int BubbleHighlightOpacity { get; set; }

		[Range(7, 30)]
		[Display(Name = "Font Size Small", GroupName = "Fuente", Order = 120)]
		public int FontSizeSmall { get; set; }

		[Display(Name = "Font Size Bold", GroupName = "Fuente", Order = 121)]
		[Range(7, 34)]
		public int FontSizeBold { get; set; }

		[Range(6, 24)]
		[Display(Name = "DOM Ladder Font Size", GroupName = "Fuente", Order = 122)]
		public int DomLadderFontSize { get; set; }

		[Range(0, 100)]
		[Display(Name = "Heatmap Opacity %", GroupName = "Opacidades", Order = 130)]
		public int HeatmapOpacity { get; set; }

		[Display(Name = "Opacidad Fondo Ladder %", GroupName = "Opacidades", Order = 131, Description = "Opacidad del fondo del DOM Ladder compacto BID/ASK. Bajalo para ver mejor el heatmap a través del panel.")]
		[Range(0, 100)]
		public int LadderBgOpacity
		{
			get
			{
				return _pLadderBgOpacity;
			}
			set
			{
				_pLadderBgOpacity = Math.Max(0, Math.Min(100, value));
			}
		}

		[Display(Name = "Opacidad Burbujas %", GroupName = "Opacidades", Order = 133)]
		[Range(0, 100)]
		[Browsable(false)]
		public int BubbleOpacity { get; set; }

		[Browsable(false)]
		[Range(0, 5000)]
		[Display(Name = "Bubble Animation MS", GroupName = "T&S Bubbles - 3D", Order = 1, Description = "Duración del pop/halo de la burbuja cuando entra o se agrupa un agresivo. 0 desactiva animación.")]
		public int BubbleAnimationMs
		{
			get
			{
				return _pBubAnimMs;
			}
			set
			{
				_pBubAnimMs = Math.Max(0, Math.Min(5000, value));
			}
		}

		[Display(Name = "Bubble Glow Strength", GroupName = "T&S Bubbles - 3D", Order = 2, Description = "Intensidad del halo, borde y brillo 3D de las burbujas.")]
		[Browsable(false)]
		[Range(0, 100)]
		public int BubbleGlowStrength
		{
			get
			{
				return _pBubGlow;
			}
			set
			{
				_pBubGlow = Math.Max(0, Math.Min(100, value));
			}
		}

		[Browsable(false)]
		[Display(Name = "Bubble Pulse Strength", GroupName = "T&S Bubbles - 3D", Order = 3, Description = "Expansión dinámica de la burbuja al entrar nuevo volumen agresivo.")]
		[Range(0, 100)]
		public int BubblePulseStrength
		{
			get
			{
				return _pBubPulse;
			}
			set
			{
				_pBubPulse = Math.Max(0, Math.Min(100, value));
			}
		}

		[Browsable(false)]
		[Display(Name = "Bubble Shockwave", GroupName = "T&S Bubbles - 3D", Order = 4, Description = "Muestra un anillo de impacto temporal en trades recientes.")]
		public bool BubbleShockwave
		{
			get
			{
				return _pBubShockwave;
			}
			set
			{
				_pBubShockwave = value;
			}
		}

		[Browsable(false)]
		[Display(Name = "Bubble Lightning Blocks", GroupName = "T&S Bubbles - 3D", Order = 5, Description = "Muestra un rayo interno en bloques institucionales/sweeps grandes.")]
		public bool BubbleLightning
		{
			get
			{
				return _pBubLightning;
			}
			set
			{
				_pBubLightning = value;
			}
		}

		[Browsable(false)]
		[Display(Name = "BigTrade Flash 3D", GroupName = "T&S Bubbles - 3D", Order = 6)]
		public bool BigTradeFlash
		{
			get
			{
				return _pBtFlash;
			}
			set
			{
				_pBtFlash = value;
			}
		}

		[Browsable(false)]
		[Display(Name = "BigTrade Flash Duración (ms)", GroupName = "T&S Bubbles - 3D", Order = 7)]
		[Range(800, 15000)]
		public int BigTradeFlashMs
		{
			get
			{
				return _pBtFlashMs;
			}
			set
			{
				_pBtFlashMs = Math.Max(800, Math.Min(15000, value));
			}
		}

		[Range(0, 100)]
		[Browsable(false)]
		[Display(Name = "BigTrade Flash Intensidad", GroupName = "T&S Bubbles - 3D", Order = 8)]
		public int BigTradeFlashStrength
		{
			get
			{
				return _pBtFlashStrength;
			}
			set
			{
				_pBtFlashStrength = Math.Max(0, Math.Min(100, value));
			}
		}

		[Range(0, 100)]
		[Display(Name = "Panel Opacity %", GroupName = "Opacidades", Order = 134)]
		public int PanelOpacity { get; set; }

		private int LiveHeatmapSliceIntervalMs()
		{
			switch (Math.Max(1, Math.Min(10, _pHeatmapScrollSpeed)))
			{
			case 1:
				return 600;
			case 2:
				return 450;
			case 3:
				return 340;
			case 4:
				return 250;
			case 5:
				return 180;
			case 6:
				return 130;
			case 7:
				return 90;
			case 8:
				return 60;
			case 9:
				return 45;
			default:
				return 30;
			}
		}

		private long InstitutionalMinQtyForBase(long baseQty, int pct)
		{
			long num = Math.Max(1L, baseQty);
			long val = (long)Math.Round((double)num * Math.Max(0.5, (double)Math.Max(50, pct) / 100.0));
			long val2 = Math.Max(1L, _pPassiveMinQty) * 3;
			return Math.Max(val, val2);
		}

		private long InstitutionalMinQtyForPct(int pct)
		{
			return InstitutionalMinQtyForBase(Math.Max(1L, _pBlock), pct);
		}

		private long IltDevZoneAttackMinQty()
		{
			return Math.Max(1L, _pIltDevZoneAttackMinQty);
		}

		private int DefaultBigTradeQtyForInstrument()
		{
			string text = null;
			try
			{
				if (base.Instrument != null && base.Instrument.MasterInstrument != null)
				{
					text = base.Instrument.MasterInstrument.Name;
				}
			}
			catch
			{
				text = null;
			}
			if (string.IsNullOrEmpty(text))
			{
				return 50;
			}
			int result;
			switch (text.ToUpperInvariant())
			{
			case "ES":
				result = 50;
				break;
			case "MES":
				result = 100;
				break;
			case "NQ":
				result = 20;
				break;
			case "MNQ":
				result = 40;
				break;
			case "RTY":
				result = 20;
				break;
			case "M2K":
				result = 40;
				break;
			case "YM":
				result = 15;
				break;
			case "MYM":
				result = 30;
				break;
			case "CL":
				result = 25;
				break;
			case "MCL":
				result = 50;
				break;
			case "GC":
				result = 20;
				break;
			case "MGC":
				result = 40;
				break;
			case "ZN":
				result = 60;
				break;
			case "ZB":
				result = 40;
				break;
			case "ZF":
				result = 60;
				break;
			case "6E":
				result = 25;
				break;
			case "6B":
				result = 15;
				break;
			case "6J":
				result = 20;
				break;
			default:
				result = 30;
				break;
			}
			return result;
		}

		private double AnchorEventPriceToBarLocked(double price, int barIdx)
		{
			if (price <= 0.0 || double.IsNaN(price) || double.IsInfinity(price))
			{
				return price;
			}
			double num;
			try
			{
				num = base.Instrument.MasterInstrument.RoundToTickSize(price);
			}
			catch
			{
				num = price;
			}
			if (barIdx < 0)
			{
				return num;
			}
			try
			{
				double valueAt = base.Highs[0].GetValueAt(barIdx);
				double valueAt2 = base.Lows[0].GetValueAt(barIdx);
				if (valueAt <= 0.0 || valueAt2 <= 0.0 || valueAt < valueAt2)
				{
					return num;
				}
				double num2 = Math.Max(_tick, 1E-10) * 1.05;
				if (num > valueAt + num2)
				{
					num = base.Instrument.MasterInstrument.RoundToTickSize(valueAt);
				}
				else if (num < valueAt2 - num2)
				{
					num = base.Instrument.MasterInstrument.RoundToTickSize(valueAt2);
				}
			}
			catch
			{
			}
			return num;
		}

		private void RecordIltDevZoneAggSampleLocked(long qty, double now)
		{
			if (qty > 0)
			{
				if (_iltDevZoneAggSamples == null)
				{
					_iltDevZoneAggSamples = new List<HM6IltDevZoneAggSample>(96);
				}
				_iltDevZoneAggSamples.Add(new HM6IltDevZoneAggSample
				{
					Qty = qty,
					Time = now
				});
				double num = now - 60000.0;
				int i;
				for (i = 0; i < _iltDevZoneAggSamples.Count && _iltDevZoneAggSamples[i].Time < num; i++)
				{
				}
				if (i > 0)
				{
					_iltDevZoneAggSamples.RemoveRange(0, i);
				}
				if (_iltDevZoneAggSamples.Count > 160)
				{
					_iltDevZoneAggSamples.RemoveRange(0, _iltDevZoneAggSamples.Count - 160);
				}
			}
		}

		private long CurrentIltDevZoneAttackMinQty(double now)
		{
			long num = IltDevZoneAttackMinQty();
			int num2 = Math.Max(0, Math.Min(95, _pIltDevZoneAdaptivePct));
			if (num2 <= 0)
			{
				return num;
			}
			if (_iltDevZoneAggSamples == null || _iltDevZoneAggSamples.Count < 8)
			{
				return num;
			}
			if (_iltDevZoneAdaptiveCached > 0 && _iltDevZoneAdaptiveLastCalc >= 0.0 && now - _iltDevZoneAdaptiveLastCalc < 2000.0)
			{
				return Math.Max(num, _iltDevZoneAdaptiveCached);
			}
			double num3 = now - 60000.0;
			List<long> list = new List<long>(_iltDevZoneAggSamples.Count);
			for (int i = 0; i < _iltDevZoneAggSamples.Count; i++)
			{
				if (_iltDevZoneAggSamples[i] != null && _iltDevZoneAggSamples[i].Time >= num3 && _iltDevZoneAggSamples[i].Qty > 0)
				{
					list.Add(_iltDevZoneAggSamples[i].Qty);
				}
			}
			if (list.Count < 8)
			{
				return num;
			}
			list.Sort();
			int val = (int)Math.Ceiling((double)((list.Count - 1) * num2) / 100.0);
			val = Math.Max(0, Math.Min(list.Count - 1, val));
			long num4 = Math.Max(num, Math.Min(num * 3, list[val]));
			if (_iltDevZoneAdaptiveCached <= 0)
			{
				_iltDevZoneAdaptiveCached = num;
			}
			_iltDevZoneAdaptiveCached = (long)Math.Round((double)_iltDevZoneAdaptiveCached * 0.7 + (double)num4 * 0.3);
			_iltDevZoneAdaptiveCached = Math.Max(num, Math.Min(num * 3, _iltDevZoneAdaptiveCached));
			_iltDevZoneAdaptiveLastCalc = now;
			return _iltDevZoneAdaptiveCached;
		}

		private long InstitutionalMinQty()
		{
			return InstitutionalMinQtyForPct(_pInstScalePct);
		}

		private long IltDevZoneInstitutionalMinQty()
		{
			return InstitutionalMinQtyForBase(IltDevZoneAttackMinQty(), _pIltDevZoneScalePct);
		}

		private long IltDevZoneInstitutionalMinQtyFor(long attackBase)
		{
			return InstitutionalMinQtyForBase(Math.Max(1L, attackBase), _pIltDevZoneScalePct);
		}

		private long IltDevSRInstitutionalMinQty()
		{
			return Math.Max(1L, _pSRPersistBig);
		}

		private long IltDevSRPassiveMinQty()
		{
			return Math.Max(1L, _pSRPersistPass);
		}

		private long BasicVisualMinQty()
		{
			return Math.Max(1L, _pF);
		}

		private int PassiveZoneRetentionBars()
		{
			int num = 120;
			if (_pPassiveWallZones)
			{
				num = Math.Max(num, Math.Max(20, Math.Min(2000, _pPassiveWallMaxBars)));
			}
			if (_pIltDevZones)
			{
				num = Math.Max(num, Math.Max(Math.Max(20, Math.Min(2000, _pIltDevZoneCandidateMaxBars)), Math.Max(100, Math.Min(10000, _pIltDevZoneHistoryBars))));
			}
			if (_pSRZones)
			{
				num = Math.Max(num, Math.Max(100, Math.Min(10000, _pIltDevSRRetentionBars)));
			}
			return num;
		}

		private float RenderWinLeft(float rightEdge)
		{
			int num = Math.Max(15, Math.Min(100, _pRenderWinPct));
			if (num >= 100)
			{
				return 0f;
			}
			return Math.Max(0f, rightEdge * (1f - (float)num / 100f));
		}

		private int PrimaryBarIndexSafe()
		{
			try
			{
				if (base.CurrentBars != null && base.CurrentBars.Length != 0 && base.CurrentBars[0] >= 0)
				{
					return base.CurrentBars[0];
				}
			}
			catch
			{
			}
			try
			{
				if (base.BarsArray != null && base.BarsArray.Length != 0 && base.BarsArray[0] != null && base.BarsArray[0].Count > 0)
				{
					return base.BarsArray[0].Count - 1;
				}
			}
			catch
			{
			}
			if (base.CurrentBar < 0)
			{
				return 0;
			}
			return base.CurrentBar;
		}

		private void RegisterSRPendingLocked(double price, int side, long vol, double now)
		{
			if (side == 0 || vol <= 0)
			{
				return;
			}
			if (_srPending == null)
			{
				_srPending = new Dictionary<string, HM6SRPending>();
			}
			string key = PassiveZoneKey(price, side < 0);
			HM6SRPending value;
			if (!_srPending.TryGetValue(key, out value) || value == null || now - value.Time > Math.Max(500.0, _pIltDevSRWindowMs))
			{
				HM6SRPending hM6SRPending = new HM6SRPending();
				hM6SRPending.Price = price;
				hM6SRPending.Side = side;
				value = hM6SRPending;
				_srPending[key] = value;
			}
			value.Vol = Math.Max(value.Vol, vol);
			value.Time = now;
			if (value.BarIdx < 0)
			{
				value.BarIdx = PrimaryBarIndexSafe();
			}
			if (_srPending.Count <= 200)
			{
				return;
			}
			List<string> list = null;
			foreach (KeyValuePair<string, HM6SRPending> item in _srPending)
			{
				if (item.Value == null || now - item.Value.Time > Math.Max(500.0, _pIltDevSRWindowMs) * 4.0)
				{
					if (list == null)
					{
						list = new List<string>();
					}
					list.Add(item.Key);
				}
			}
			if (list != null)
			{
				for (int i = 0; i < list.Count; i++)
				{
					_srPending.Remove(list[i]);
				}
			}
		}

		private void TryConfirmSRPendingLocked(double price, bool wasBid, long qty, double now)
		{
			if (_srPending == null || _srPending.Count == 0)
			{
				return;
			}
			double num = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25);
			double num2 = num * SRBandTicks;
			HM6SRPending hM6SRPending = null;
			string key = null;
			double num3 = double.MaxValue;
			foreach (KeyValuePair<string, HM6SRPending> item in _srPending)
			{
				HM6SRPending value = item.Value;
				if (value != null && !(now - value.Time > Math.Max(500.0, _pIltDevSRWindowMs)) && ((value.Side > 0) ? (!wasBid) : wasBid))
				{
					double num4 = Math.Abs(value.Price - price);
					if (num4 <= num2 + 1E-09 && num4 < num3)
					{
						num3 = num4;
						hM6SRPending = value;
						key = item.Key;
					}
				}
			}
			if (hM6SRPending != null)
			{
				hM6SRPending.ReAdd += Math.Max(0L, qty);
				if (hM6SRPending.ReAdd >= Math.Max(1L, hM6SRPending.Vol * Math.Max(10, Math.Min(100, _pIltDevSRReAddPct)) / 100))
				{
					CreateOrMergeSRZoneLocked(hM6SRPending.Price, hM6SRPending.Side > 0, hM6SRPending.Vol, hM6SRPending.ReAdd, now, hM6SRPending.BarIdx);
					_srPending.Remove(key);
				}
			}
		}

		private void CreateOrMergeSRZoneLocked(double price, bool askSide, long initiativeQty, long reAddQty, double now, int originBar)
		{
			if (_srZones == null)
			{
				_srZones = new List<HM6SRZone>();
			}
			double num = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25);
			int num2 = ((originBar >= 0) ? originBar : PrimaryBarIndexSafe());
			long num3 = Math.Max(1L, initiativeQty);
			long num4 = Math.Max(1L, reAddQty);
			long num5 = num3 + num4;
			for (int i = 0; i < _srZones.Count; i++)
			{
				HM6SRZone hM6SRZone = _srZones[i];
				if (hM6SRZone != null && hM6SRZone.AskSide == askSide && Math.Abs(hM6SRZone.Price - price) <= num * SRBandTicks + 1E-09)
				{
					hM6SRZone.Qty += num5;
					hM6SRZone.InitiativeQty = Math.Max(hM6SRZone.InitiativeQty, num3);
					hM6SRZone.ReAddQty = Math.Max(hM6SRZone.ReAddQty, num4);
					hM6SRZone.FormationCount++;
					if (hM6SRZone.StartBar < 0)
					{
						hM6SRZone.StartBar = num2;
					}
					if (hM6SRZone.VisualEventBar < 0)
					{
						hM6SRZone.VisualEventBar = num2;
					}
					hM6SRZone.State = 0;
					hM6SRZone.BreakBar = -1;
					hM6SRZone.TouchBar = -1;
					hM6SRZone.Armed = false;
					hM6SRZone.LastUpdate = now;
					TryRegisterIltDevSupportFromSRLocked(hM6SRZone, now);
					return;
				}
			}
			HM6SRZone hM6SRZone2 = new HM6SRZone();
			hM6SRZone2.Price = price;
			hM6SRZone2.AskSide = askSide;
			hM6SRZone2.Qty = num5;
			hM6SRZone2.InitiativeQty = num3;
			hM6SRZone2.ReAddQty = num4;
			hM6SRZone2.FormationCount = 1;
			hM6SRZone2.State = 0;
			hM6SRZone2.StartBar = num2;
			hM6SRZone2.VisualEventBar = num2;
			hM6SRZone2.StartTime = now;
			hM6SRZone2.LastUpdate = now;
			HM6SRZone hM6SRZone3 = hM6SRZone2;
			_srZones.Add(hM6SRZone3);
			TryRegisterIltDevSupportFromSRLocked(hM6SRZone3, now);
			TrimSRZonesLocked(num2);
		}

		private void TrimSRZonesLocked(int curBar)
		{
			if (_srZones == null || _srZones.Count == 0)
			{
				return;
			}
			int num = Math.Max(100, Math.Min(10000, _pIltDevSRRetentionBars));
			for (int num2 = _srZones.Count - 1; num2 >= 0; num2--)
			{
				HM6SRZone hM6SRZone = _srZones[num2];
				if (hM6SRZone == null)
				{
					_srZones.RemoveAt(num2);
				}
				else
				{
					int num3 = ((hM6SRZone.FadeBar >= 0) ? hM6SRZone.FadeBar : ((hM6SRZone.BreakBar >= 0) ? hM6SRZone.BreakBar : hM6SRZone.StartBar));
					if (hM6SRZone.State >= 2 && curBar - num3 > num)
					{
						_srZones.RemoveAt(num2);
					}
				}
			}
			if (_srZones.Count <= 320)
			{
				return;
			}
			while (_srZones.Count > 260)
			{
				int num4 = -1;
				int num5 = int.MaxValue;
				for (int i = 0; i < _srZones.Count; i++)
				{
					HM6SRZone hM6SRZone2 = _srZones[i];
					if (hM6SRZone2 == null)
					{
						num4 = i;
						break;
					}
					if (hM6SRZone2.State >= 2)
					{
						int num6 = ((hM6SRZone2.FadeBar >= 0) ? hM6SRZone2.FadeBar : ((hM6SRZone2.BreakBar >= 0) ? hM6SRZone2.BreakBar : hM6SRZone2.StartBar));
						if (num6 < num5)
						{
							num5 = num6;
							num4 = i;
						}
					}
				}
				if (num4 < 0)
				{
					break;
				}
				_srZones.RemoveAt(num4);
			}
		}

		private void RegisterSRHistLocked(double price, int side, long vol, double now)
		{
			if (side == 0 || vol <= 0)
			{
				return;
			}
			if (_srHist == null)
			{
				_srHist = new List<HM6SRHist>();
			}
			double num = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25);
			double num2 = Math.Max(num, (double)_pSRHistWinPts * 0.5);
			int startBar = PrimaryBarIndexSafe();
			for (int i = 0; i < _srHist.Count; i++)
			{
				HM6SRHist hM6SRHist = _srHist[i];
				if (hM6SRHist == null)
				{
					continue;
				}
				double num3 = Math.Abs(hM6SRHist.Price - price);
				if (num3 <= num + 1E-09 && hM6SRHist.Side == side && now - hM6SRHist.StartTime < 5000.0)
				{
					if (vol > hM6SRHist.Qty)
					{
						hM6SRHist.Qty = vol;
					}
					return;
				}
				if (num3 <= num2 + 1E-09)
				{
					if (vol > hM6SRHist.Qty)
					{
						hM6SRHist.Price = price;
						hM6SRHist.Side = side;
					}
					hM6SRHist.Qty += vol;
					return;
				}
			}
			bool flag = (_bid != null && _bid.Count > 0) || (_ask != null && _ask.Count > 0);
			bool flag2 = flag && SRHistPassiveConfluenceLocked(price, side, _pSRHistPass) >= Math.Max(1, _pSRHistPass);
			_srHist.Add(new HM6SRHist
			{
				Price = price,
				Side = side,
				Qty = Math.Max(1L, vol),
				StartBar = startBar,
				StartTime = now,
				EverDefended = (!flag || flag2)
			});
			if (_srHist.Count <= 24)
			{
				return;
			}
			int index = 0;
			long num4 = long.MaxValue;
			for (int j = 0; j < _srHist.Count; j++)
			{
				if (_srHist[j] != null && _srHist[j].Qty < num4)
				{
					num4 = _srHist[j].Qty;
					index = j;
				}
			}
			_srHist.RemoveAt(index);
		}

		private long SRHistPassiveConfluenceLocked(double price, int side, long cap)
		{
			Dictionary<double, HM6Level> dictionary = ((side > 0) ? _bid : _ask);
			if (dictionary == null)
			{
				return 0L;
			}
			double num = Math.Max(0.25, _pSRHistWinPts);
			long num2 = 0L;
			foreach (KeyValuePair<double, HM6Level> item in dictionary)
			{
				if (item.Value != null && Math.Abs(item.Key - price) <= num + 1E-09)
				{
					num2 += item.Value.Qty;
					if (cap > 0 && num2 >= cap)
					{
						break;
					}
				}
			}
			return num2;
		}

		private void UpdateSRHistDefenseLocked(double now)
		{
			if (_srHist == null || _srHist.Count == 0 || now - _srHistDefStamp < 2000.0)
			{
				return;
			}
			_srHistDefStamp = now;
			long num = Math.Max(1L, _pSRHistPass);
			int num2 = PrimaryBarIndexSafe();
			for (int num3 = _srHist.Count - 1; num3 >= 0; num3--)
			{
				HM6SRHist hM6SRHist = _srHist[num3];
				if (hM6SRHist == null)
				{
					_srHist.RemoveAt(num3);
				}
				else if (num2 - hM6SRHist.StartBar > 1000)
				{
					_srHist.RemoveAt(num3);
				}
				else
				{
					hM6SRHist.Defended = SRHistPassiveConfluenceLocked(hM6SRHist.Price, hM6SRHist.Side, num) >= num;
					if (hM6SRHist.Defended)
					{
						hM6SRHist.EverDefended = true;
					}
				}
			}
		}

		private static double IltDevClamp01(double v)
		{
			if (double.IsNaN(v) || double.IsInfinity(v))
			{
				return 0.0;
			}
			return Math.Max(0.0, Math.Min(1.0, v));
		}

		private static double IltDevGateNorm(double value, double gate, double saturationRatio)
		{
			if (gate <= 0.0 || value < gate)
			{
				return 0.0;
			}
			double num = Math.Max(1.01, saturationRatio);
			double num2 = value / gate;
			return 0.5 + 0.5 * IltDevClamp01((num2 - 1.0) / (num - 1.0));
		}

		private bool IsQualifiedIltDevWallLocked(HM6PassiveZone z, double price, bool wasBid, double now)
		{
			if (z == null || !z.Active || z.WasBid != wasBid)
			{
				return false;
			}
			double num = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25);
			double num2 = num * SRBandTicks;
			if (Math.Abs(z.Price - price) > num2 + 1E-09)
			{
				return false;
			}
			long num3 = IltDevSRPassiveMinQty();
			if (z.MaxQty < num3)
			{
				return false;
			}
			int num4 = Math.Max(z.PresenceCount, z.EventCount);
			double num5 = ((z.FirstTime > 0.0) ? Math.Max(0.0, now - z.FirstTime) : 0.0);
			double num6 = ((z.LastUpdateTime > 0.0) ? Math.Max(0.0, now - z.LastUpdateTime) : double.MaxValue);
			if (num4 >= Math.Max(2, _pIltDevSRWallMinPresence) && num5 >= Math.Max(100.0, _pIltDevSRWallMinAgeMs))
			{
				return num6 <= Math.Max(500.0, _pIltDevSRWallFreshMs);
			}
			return false;
		}

		private HM6PassiveZone FindBestQualifiedIltDevWallNearLocked(double price, bool wasBid, double now)
		{
			if (_passiveZones == null || _passiveZones.Count == 0)
			{
				return null;
			}
			double num = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25);
			double num2 = num * SRBandTicks;
			long num3 = IltDevSRPassiveMinQty();
			HM6PassiveZone result = null;
			double num4 = double.MinValue;
			foreach (KeyValuePair<string, HM6PassiveZone> passiveZone in _passiveZones)
			{
				HM6PassiveZone value = passiveZone.Value;
				if (IsQualifiedIltDevWallLocked(value, price, wasBid, now))
				{
					int num5 = Math.Max(value.PresenceCount, value.EventCount);
					double num6 = Math.Max(0.0, now - value.FirstTime);
					double num7 = ((num2 > 0.0) ? IltDevClamp01(Math.Abs(value.Price - price) / num2) : 0.0);
					double num8 = Math.Min(3.0, (double)value.MaxQty / (double)num3);
					double num9 = Math.Min(3.0, (double)num5 / (double)Math.Max(2, _pIltDevSRWallMinPresence));
					double num10 = Math.Min(3.0, num6 / Math.Max(100.0, _pIltDevSRWallMinAgeMs));
					double num11 = Math.Max(1.0, Math.Max(value.MaxQty, value.AddQty + value.RefillQty));
					double num12 = IltDevClamp01((double)value.PullQty / num11);
					double num13 = num8 + 0.45 * num9 + 0.35 * num10 + 0.2 * (double)Math.Min(2, value.RefillEvents) - 0.4 * num7 - 0.5 * num12;
					if (num13 > num4)
					{
						num4 = num13;
						result = value;
					}
				}
			}
			return result;
		}

		private int ComputeIltDevStrengthScore(long initiativeQty, long reAddQty, long wallQty, int wallPresence, double wallAgeMs, long wallRefillQty, long wallConsumeQty, long wallPullQty, int wallRefillEvents, int independentReactions, bool icebergConfirmed)
		{
			double gate = Math.Max(1.0, IltDevSRInstitutionalMinQty());
			double num = Math.Max(1.0, (double)(initiativeQty * Math.Max(10, Math.Min(100, _pIltDevSRReAddPct))) / 100.0);
			double gate2 = Math.Max(1.0, IltDevSRPassiveMinQty());
			double num2 = IltDevGateNorm(initiativeQty, gate, 2.5);
			double num3 = IltDevGateNorm(reAddQty, num, 2.0);
			double num4 = IltDevGateNorm(wallQty, gate2, 2.5);
			double num5 = IltDevGateNorm(wallPresence, Math.Max(2, _pIltDevSRWallMinPresence), 3.0);
			double num6 = IltDevGateNorm(wallAgeMs, Math.Max(100.0, _pIltDevSRWallMinAgeMs), 3.0);
			double num7 = 0.5 * num5 + 0.5 * num6;
			double num8 = Math.Max(1.0, Math.Max(wallConsumeQty, num));
			double num9 = IltDevClamp01((double)wallRefillQty / num8);
			if (wallRefillEvents >= 2)
			{
				num9 = Math.Max(num9, 0.8);
			}
			else if (wallRefillEvents == 1)
			{
				num9 = Math.Max(num9, 0.45);
			}
			double num10 = Math.Max(1.0, wallQty + wallRefillQty + wallConsumeQty);
			double num11 = IltDevClamp01((double)wallPullQty / num10);
			double val = 25.0 * num2 + 20.0 * num3 + 25.0 * num4 + 20.0 * num7 + 10.0 * num9 - 10.0 * num11 + Math.Min(5.0, (double)Math.Max(0, independentReactions) * 1.5);
			int num12 = (int)Math.Round(Math.Max(0.0, Math.Min(100.0, val)));
			if (icebergConfirmed)
			{
				num12 = Math.Min(100, Math.Max(num12, 70) + 12);
			}
			return num12;
		}

		private void RegisterIltDevSupportLocked(double price, bool wasBid, long initiativeQty, long reAddQty, HM6PassiveZone wall, double now, int originBar)
		{
			if (wall == null || !IsQualifiedIltDevWallLocked(wall, price, wasBid, now))
			{
				return;
			}
			if (_iltDevLevels == null)
			{
				_iltDevLevels = new List<HM6IltDevSupportLevel>();
			}
			double num = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25);
			double num2 = num * SRBandTicks;
			int num3 = ((originBar >= 0) ? originBar : PrimaryBarIndexSafe());
			long num4 = Math.Max(1L, initiativeQty);
			long num5 = Math.Max(1L, reAddQty);
			int num6 = Math.Max(wall.PresenceCount, wall.EventCount);
			double num7 = ((wall.FirstTime > 0.0) ? Math.Max(0.0, now - wall.FirstTime) : 0.0);
			for (int i = 0; i < _iltDevLevels.Count; i++)
			{
				HM6IltDevSupportLevel hM6IltDevSupportLevel = _iltDevLevels[i];
				if (hM6IltDevSupportLevel != null && !hM6IltDevSupportLevel.Broken && hM6IltDevSupportLevel.WasBid == wasBid && !(Math.Abs(hM6IltDevSupportLevel.Price - price) > num2 + 1E-09))
				{
					hM6IltDevSupportLevel.InitiativeQty = Math.Max(hM6IltDevSupportLevel.InitiativeQty, num4);
					hM6IltDevSupportLevel.ReAddQty = Math.Max(hM6IltDevSupportLevel.ReAddQty, num5);
					hM6IltDevSupportLevel.WallQty = Math.Max(hM6IltDevSupportLevel.WallQty, wall.MaxQty);
					hM6IltDevSupportLevel.WallPresence = Math.Max(hM6IltDevSupportLevel.WallPresence, num6);
					hM6IltDevSupportLevel.WallAgeMs = Math.Max(hM6IltDevSupportLevel.WallAgeMs, num7);
					hM6IltDevSupportLevel.WallRefillQty = Math.Max(hM6IltDevSupportLevel.WallRefillQty, wall.RefillQty);
					hM6IltDevSupportLevel.WallConsumeQty = Math.Max(hM6IltDevSupportLevel.WallConsumeQty, wall.ConsumeQty);
					hM6IltDevSupportLevel.WallPullQty = Math.Max(hM6IltDevSupportLevel.WallPullQty, wall.PullQty);
					hM6IltDevSupportLevel.WallRefillEvents = Math.Max(hM6IltDevSupportLevel.WallRefillEvents, wall.RefillEvents);
					hM6IltDevSupportLevel.WallIcebergConfirmed = hM6IltDevSupportLevel.WallIcebergConfirmed || wall.IcebergConfirmed;
					hM6IltDevSupportLevel.Qty = hM6IltDevSupportLevel.InitiativeQty + hM6IltDevSupportLevel.ReAddQty + hM6IltDevSupportLevel.WallQty;
					if (hM6IltDevSupportLevel.StartBar < 0)
					{
						hM6IltDevSupportLevel.StartBar = num3;
					}
					if (hM6IltDevSupportLevel.VisualEventBar < 0)
					{
						hM6IltDevSupportLevel.VisualEventBar = num3;
					}
					if (now - hM6IltDevSupportLevel.LastReinforceTime >= 30000.0)
					{
						hM6IltDevSupportLevel.ReactCount++;
						hM6IltDevSupportLevel.LastReinforceTime = now;
						hM6IltDevSupportLevel.AdverseCloses = 0;
						hM6IltDevSupportLevel.LastAdverseBar = -1;
						hM6IltDevSupportLevel.LastAdverseClose = 0.0;
					}
					hM6IltDevSupportLevel.StrengthScore = ComputeIltDevStrengthScore(hM6IltDevSupportLevel.InitiativeQty, hM6IltDevSupportLevel.ReAddQty, hM6IltDevSupportLevel.WallQty, hM6IltDevSupportLevel.WallPresence, hM6IltDevSupportLevel.WallAgeMs, hM6IltDevSupportLevel.WallRefillQty, hM6IltDevSupportLevel.WallConsumeQty, hM6IltDevSupportLevel.WallPullQty, hM6IltDevSupportLevel.WallRefillEvents, hM6IltDevSupportLevel.ReactCount, hM6IltDevSupportLevel.WallIcebergConfirmed);
					ReinforceIltDevZoneFromSupportLocked(hM6IltDevSupportLevel.Price, hM6IltDevSupportLevel.WasBid, hM6IltDevSupportLevel.StrengthScore, now);
					return;
				}
			}
			HM6IltDevSupportLevel hM6IltDevSupportLevel2 = new HM6IltDevSupportLevel();
			hM6IltDevSupportLevel2.Price = price;
			hM6IltDevSupportLevel2.WasBid = wasBid;
			hM6IltDevSupportLevel2.InitiativeQty = num4;
			hM6IltDevSupportLevel2.ReAddQty = num5;
			hM6IltDevSupportLevel2.WallQty = Math.Max(1L, wall.MaxQty);
			hM6IltDevSupportLevel2.WallPresence = num6;
			hM6IltDevSupportLevel2.WallAgeMs = num7;
			hM6IltDevSupportLevel2.WallRefillQty = Math.Max(0L, wall.RefillQty);
			hM6IltDevSupportLevel2.WallConsumeQty = Math.Max(0L, wall.ConsumeQty);
			hM6IltDevSupportLevel2.WallPullQty = Math.Max(0L, wall.PullQty);
			hM6IltDevSupportLevel2.WallRefillEvents = Math.Max(0, wall.RefillEvents);
			hM6IltDevSupportLevel2.WallIcebergConfirmed = wall.IcebergConfirmed;
			hM6IltDevSupportLevel2.StartBar = num3;
			hM6IltDevSupportLevel2.VisualEventBar = num3;
			hM6IltDevSupportLevel2.StartTime = now;
			hM6IltDevSupportLevel2.LastReinforceTime = now;
			hM6IltDevSupportLevel2.ReactCount = 0;
			hM6IltDevSupportLevel2.Broken = false;
			hM6IltDevSupportLevel2.BreakBar = -1;
			HM6IltDevSupportLevel hM6IltDevSupportLevel3 = hM6IltDevSupportLevel2;
			hM6IltDevSupportLevel3.Qty = hM6IltDevSupportLevel3.InitiativeQty + hM6IltDevSupportLevel3.ReAddQty + hM6IltDevSupportLevel3.WallQty;
			hM6IltDevSupportLevel3.StrengthScore = ComputeIltDevStrengthScore(hM6IltDevSupportLevel3.InitiativeQty, hM6IltDevSupportLevel3.ReAddQty, hM6IltDevSupportLevel3.WallQty, hM6IltDevSupportLevel3.WallPresence, hM6IltDevSupportLevel3.WallAgeMs, hM6IltDevSupportLevel3.WallRefillQty, hM6IltDevSupportLevel3.WallConsumeQty, hM6IltDevSupportLevel3.WallPullQty, hM6IltDevSupportLevel3.WallRefillEvents, 0, hM6IltDevSupportLevel3.WallIcebergConfirmed);
			_iltDevLevels.Add(hM6IltDevSupportLevel3);
			ReinforceIltDevZoneFromSupportLocked(hM6IltDevSupportLevel3.Price, hM6IltDevSupportLevel3.WasBid, hM6IltDevSupportLevel3.StrengthScore, now);
		}

		private void TryRegisterIltDevSupportFromSRLocked(HM6SRZone sr, double now)
		{
			if (sr != null && sr.State < 2)
			{
				bool wasBid = !sr.AskSide;
				HM6PassiveZone hM6PassiveZone = FindBestQualifiedIltDevWallNearLocked(sr.Price, wasBid, now);
				if (hM6PassiveZone != null)
				{
					long num = ((sr.InitiativeQty > 0) ? sr.InitiativeQty : Math.Max(1L, sr.Qty * 100 / (100L + (long)Math.Max(10, Math.Min(100, _pIltDevSRReAddPct)))));
					long reAddQty = ((sr.ReAddQty > 0) ? sr.ReAddQty : Math.Max(1L, sr.Qty - num));
					int originBar = ((sr.VisualEventBar >= 0) ? sr.VisualEventBar : sr.StartBar);
					RegisterIltDevSupportLocked(sr.Price, wasBid, num, reAddQty, hM6PassiveZone, now, originBar);
				}
			}
		}

		private void EvaluateIltDevSupportClosesLocked(double closedOpen, double closedClose, int closedBar)
		{
			if (_iltDevLevels == null || _iltDevLevels.Count == 0 || closedClose <= 0.0 || double.IsNaN(closedClose) || double.IsNaN(closedOpen))
			{
				return;
			}
			double num = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25);
			double num2 = num * SRBandTicks;
			for (int num3 = _iltDevLevels.Count - 1; num3 >= 0; num3--)
			{
				HM6IltDevSupportLevel hM6IltDevSupportLevel = _iltDevLevels[num3];
				if (hM6IltDevSupportLevel == null)
				{
					_iltDevLevels.RemoveAt(num3);
				}
				else if (hM6IltDevSupportLevel.Broken)
				{
					if (hM6IltDevSupportLevel.BreakBar >= 0 && closedBar - hM6IltDevSupportLevel.BreakBar > Math.Max(100, Math.Min(10000, _pIltDevSRRetentionBars)))
					{
						_iltDevLevels.RemoveAt(num3);
					}
				}
				else if (closedBar > hM6IltDevSupportLevel.LastCountedBar && closedBar > hM6IltDevSupportLevel.StartBar)
				{
					hM6IltDevSupportLevel.LastCountedBar = closedBar;
					double num4 = (hM6IltDevSupportLevel.WasBid ? (hM6IltDevSupportLevel.Price - num2 - num) : (hM6IltDevSupportLevel.Price + num2 + num));
					if (!(hM6IltDevSupportLevel.WasBid ? (closedClose <= num4) : (closedClose >= num4)))
					{
						hM6IltDevSupportLevel.AdverseCloses = 0;
						hM6IltDevSupportLevel.LastAdverseBar = -1;
						hM6IltDevSupportLevel.LastAdverseClose = 0.0;
					}
					else
					{
						bool flag = hM6IltDevSupportLevel.LastAdverseBar == closedBar - 1;
						if (flag)
						{
							hM6IltDevSupportLevel.AdverseCloses++;
						}
						else
						{
							hM6IltDevSupportLevel.AdverseCloses = 1;
						}
						double num5 = (closedOpen + closedClose) * 0.5;
						bool flag2 = (hM6IltDevSupportLevel.WasBid ? (num5 <= num4) : (num5 >= num4));
						bool flag3 = flag && (hM6IltDevSupportLevel.WasBid ? (closedClose <= hM6IltDevSupportLevel.LastAdverseClose - num) : (closedClose >= hM6IltDevSupportLevel.LastAdverseClose + num));
						double num6 = (hM6IltDevSupportLevel.WasBid ? Math.Max(0.0, num4 - closedClose) : Math.Max(0.0, closedClose - num4));
						bool flag4 = flag2 && num6 >= Math.Max(2.0 * num, num2 * 0.5);
						hM6IltDevSupportLevel.LastAdverseBar = closedBar;
						hM6IltDevSupportLevel.LastAdverseClose = closedClose;
						if (hM6IltDevSupportLevel.AdverseCloses >= 2 && (flag2 || flag3 || flag4))
						{
							hM6IltDevSupportLevel.Broken = true;
							hM6IltDevSupportLevel.BreakBar = closedBar;
						}
						else if (hM6IltDevSupportLevel.AdverseCloses >= 2)
						{
							hM6IltDevSupportLevel.AdverseCloses = 1;
						}
					}
				}
			}
		}

		private void UpdateSRZonesTouchLocked(double low, double high, int curBar)
		{
			if (_srZones == null || _srZones.Count == 0)
			{
				return;
			}
			double num = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25) * SRBandTicks;
			for (int i = 0; i < _srZones.Count; i++)
			{
				HM6SRZone hM6SRZone = _srZones[i];
				if (hM6SRZone == null || curBar <= hM6SRZone.StartBar)
				{
					continue;
				}
				bool flag = low <= hM6SRZone.Price + num && high >= hM6SRZone.Price - num;
				if (!hM6SRZone.Armed)
				{
					if (!flag)
					{
						hM6SRZone.Armed = true;
					}
				}
				else if (flag && hM6SRZone.TouchBar < 0 && hM6SRZone.State < 2)
				{
					hM6SRZone.TouchBar = curBar;
					hM6SRZone.TouchCloses = 0;
				}
			}
		}

		private void EvaluateSRZonesCloseLocked(double closedClose, int closedBar)
		{
			if (_srZones == null || _srZones.Count == 0 || closedClose <= 0.0 || double.IsNaN(closedClose))
			{
				return;
			}
			double num = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25);
			double num2 = num * (SRBandTicks + 1.0);
			for (int i = 0; i < _srZones.Count; i++)
			{
				HM6SRZone hM6SRZone = _srZones[i];
				if (hM6SRZone == null || closedBar <= hM6SRZone.StartBar)
				{
					continue;
				}
				bool flag = (hM6SRZone.AskSide ? (closedClose >= hM6SRZone.Price + num2) : (closedClose <= hM6SRZone.Price - num2));
				bool flag2 = (hM6SRZone.AskSide ? (closedClose <= hM6SRZone.Price - num) : (closedClose >= hM6SRZone.Price + num));
				if (hM6SRZone.State == 2)
				{
					if (_pFadeBars > 0 && hM6SRZone.BreakBar >= 0 && closedBar > hM6SRZone.BreakBar && closedBar - hM6SRZone.BreakBar <= _pFadeBars && flag2)
					{
						hM6SRZone.State = 3;
						hM6SRZone.FadeBar = closedBar;
					}
				}
				else if (hM6SRZone.State == 3)
				{
					if (hM6SRZone.FadeBar >= 0 && closedBar > hM6SRZone.FadeBar && flag)
					{
						hM6SRZone.State = 2;
						hM6SRZone.BreakBar = closedBar;
						hM6SRZone.FadeBar = -1;
					}
				}
				else
				{
					if (hM6SRZone.State > 3)
					{
						continue;
					}
					if (hM6SRZone.TouchBar >= 0)
					{
						hM6SRZone.TouchCloses++;
						if (flag)
						{
							hM6SRZone.State = 2;
							hM6SRZone.BreakBar = closedBar;
							hM6SRZone.TouchBar = -1;
						}
						else if (flag2 || hM6SRZone.TouchCloses >= 4)
						{
							hM6SRZone.State = 1;
							hM6SRZone.TouchBar = -1;
							hM6SRZone.Armed = false;
							hM6SRZone.TouchCloses = 0;
						}
					}
					else if (flag)
					{
						hM6SRZone.State = 2;
						hM6SRZone.BreakBar = closedBar;
					}
				}
			}
		}

		private long FreezeWallTagQty(double price, bool wasBid, string state, long liveQty)
		{
			if (_tagFreeze == null)
			{
				_tagFreeze = new Dictionary<string, HM6TagFreeze>();
			}
			string key = PassiveZoneKey(price, wasBid) + ":" + state;
			double num = IltDevNowMs;
			HM6TagFreeze value;
			if (!_tagFreeze.TryGetValue(key, out value) || value == null)
			{
				HM6TagFreeze hM6TagFreeze = new HM6TagFreeze();
				hM6TagFreeze.Qty = Math.Max(1L, liveQty);
				hM6TagFreeze.Time = num;
				hM6TagFreeze.State = state;
				value = hM6TagFreeze;
				_tagFreeze[key] = value;
				if (_tagFreeze.Count > 400)
				{
					List<string> list = null;
					foreach (KeyValuePair<string, HM6TagFreeze> item in _tagFreeze)
					{
						if (item.Value == null || num - item.Value.Time > 900000.0)
						{
							if (list == null)
							{
								list = new List<string>();
							}
							list.Add(item.Key);
						}
					}
					if (list != null)
					{
						for (int i = 0; i < list.Count; i++)
						{
							_tagFreeze.Remove(list[i]);
						}
					}
				}
			}
			return Math.Max(1L, value.Qty);
		}

		private bool FrozenTagVigente(double price, bool wasBid, string state, long liveQty, out long qty)
		{
			qty = FreezeWallTagQty(price, wasBid, state, liveQty);
			if (_tagFreeze == null)
			{
				return true;
			}
			string key = PassiveZoneKey(price, wasBid) + ":" + state;
			HM6TagFreeze value;
			if (_tagFreeze.TryGetValue(key, out value) && value != null)
			{
				return (double)IltDevNowMs - value.Time <= (double)Math.Max(10, _pTagLifeSec) * 1000.0;
			}
			return true;
		}

		protected override void OnStateChange()
		{
			if (base.State == State.SetDefaults)
			{
				base.Description = "ILT-DEV-DOMHEATMAP DEV - DOM Heatmap + Market Pulse + Agresivos clave T&S + Liquidez Pasiva. Parte de la ILT-DEV SUITE.";
				Name = "ILT-DEV-DOMHEATMAP DEV";
				base.Calculate = Calculate.OnEachTick;
				base.IsOverlay = true;
				base.DisplayInDataBox = false;
				base.DrawOnPricePanel = true;
				base.IsSuspendedWhileInactive = false;
				FiltroQty = 5;
				LadderFiltroQty = 5;
				LadderOffsetPx = 0;
				LadderBgOpacity = 35;
				UmbralLin = 10;
				AgruparTk = 1;
				MinBurbuja = 10;
				UmbralBloque = 50;
				VerLadder = true;
				VerLineas = true;
				VerStacking = true;
				VerPulling = true;
				VerBurbujas = false;
				VerAbsorcion = true;
				Bidireccional = true;
				VerPOC = true;
				VerDomFlowPulse = true;
				FlowMinChange = 10;
				FlowFadeMs = 5200;
				FlowOpacity = 72;
				FlowSize = 3;
				FlowLabelMin = 30;
				FlowRefreshFps = 14;
				HeatmapMinOpacity = 10;
				HeatmapPriorityContrast = 220;
				HeatmapBarWidthPct = 185;
				PaletaHeatmap = IltDevHeatmapPaletteDev.BookmapClassic;
				HeatmapEscalaLog = true;
				HeatmapBitmapScroll = true;
				HeatmapScrollSpeed = 4;
				EscalaInstitucionalPct = 200;
				VerVisualBasico = true;
				VerIltDevZones = true;
				IltDevZoneAtaqueMinQty = 20;
				IltDevZoneEscalaPct = 210;
				IltDevZoneBandaTicks = 4;
				IltDevZoneCandidatoMaxBars = 600;
				IltDevZoneHistorialBars = 2000;
				IltDevZoneAtaqueAdaptativoPct = 75;
				IltDevZoneFuerzaMin = 60;
				IltDevZoneFusionTicks = 6;
				IltDevZoneFusionSeg = 60;
				ZoneCierresConfirmacion = 2;
				FadeVentanaVelas = 5;
				SpoofProximidadTicks = 24;
				SRZonesActivas = true;
				IltDevSREscalaPct = 200;
				IltDevSRReAddPct = 40;
				IltDevSRVentanaMs = 8000;
				IltDevSRFuerzaMin = 80;
				IltDevSRMuroPresenciaMin = 10;
				IltDevSRMuroEdadMinMs = 900;
				IltDevSRMuroFreshMs = 2800;
				IltDevSRRetencionBars = 2000;
				SRBandaTicks = 4;
				VigenciaEventoSeg = 60;
				SRHistActivo = true;
				VentanaRenderPct = 50;
				SRHistWinPts = 5;
				SRPersistBig = 15;
				SRPersistPass = 12;
				SRHistBig = 40;
				SRHistPass = 20;
				FlowPanelOpacity = 12;
				VerLiquidezPasiva = true;
				LiquidezPasivaMinQty = 5;
				LiquidezPasivaProfundidad = 10;
				LiquidezPasivaEventoMin = 5;
				LiquidezPasivaAggWindowMs = 650;
				LiquidezPasivaRefillWindowMs = 1200;
				VerEventosLiquidezPasiva = true;
				VerMurosLiquidezPasiva = true;
				LiquidezPasivaMuroMaxBars = 600;
				LiquidezPasivaMarkerSize = 3;
				LiquidezPasivaLabelMin = 5;
				BubbleAnimationMs = 850;
				BubbleGlowStrength = 46;
				BubblePulseStrength = 12;
				BubbleShockwave = true;
				BubbleLightning = true;
				BigTradeVisualMinQty = 50;
				BigTradeAutoPorInstrumento = true;
				BigTradeFlash = true;
				BigTradeFlashMs = 6000;
				BigTradeFlashStrength = 70;
				MantenerBurbujasHistoricas = true;
				MaxBurbujasHistoricas = 350000;
				UseWpfPanel = true;
				ShowToolbarButton = true;
				DebugRender = false;
				PanelXW = -1.0;
				PanelYW = -1.0;
				PanelMinimizedPersist = false;
				HeatmapBidColor = Brushes.DeepSkyBlue;
				HeatmapAskColor = Brushes.Magenta;
				LiquidityBidColor = Brushes.Turquoise;
				LiquidityAskColor = Brushes.Magenta;
				BigTradeBuyColor = Brushes.LimeGreen;
				BigTradeSellColor = Brushes.Red;
				BubbleBorderColor = Brushes.WhiteSmoke;
				BubbleLightningColor = Brushes.WhiteSmoke;
				BubbleBorderOpacity = 58;
				BubbleHighlightOpacity = 38;
				TextColor = Brushes.WhiteSmoke;
				DimTextColor = Brushes.Gray;
				FlowAddColor = Brushes.DeepSkyBlue;
				FlowPullColor = Brushes.Orange;
				AbsorptionMarkColor = Brushes.Gold;
				RefillMarkColor = Brushes.WhiteSmoke;
				ConsumeMarkColor = Brushes.OrangeRed;
				FlowTextColor = Brushes.White;
				FontSizeSmall = 11;
				FontSizeBold = 12;
				DomLadderFontSize = 9;
				HeatmapOpacity = 90;
				BubbleOpacity = 78;
				PanelOpacity = 88;
			}
			else if (base.State == State.Configure)
			{
				AddDataSeries(BarsPeriodType.Tick, 1);
			}
			else if (base.State == State.DataLoaded)
			{
				_bid = new Dictionary<double, HM6Level>();
				_ask = new Dictionary<double, HM6Level>();
				_hist = new Dictionary<int, DomSnapHM6>();
				_snap = new DomSnapHM6();
				_pulls = new List<HM6Pull>();
				_flows = new List<HM6Flow>();
				_passiveEvents = new List<HM6PassiveEvent>();
				_pendingPassiveHeatmapEvents = new List<HM6PassiveEvent>();
				_passiveZones = new Dictionary<string, HM6PassiveZone>();
				_defendedWallLines = new List<HM6DefendedWallLine>();
				Array.Clear(_zoneTallyProven, 0, 4);
				Array.Clear(_zoneTallyBroken, 0, 4);
				_iltDevZoneAggSamples = new List<HM6IltDevZoneAggSample>(96);
				_passiveSummary = new HM6PassiveSummary();
				_liveHeatmapSlices = new List<HM6HeatmapSlice>(512);
				_liveHeatmapSeq = 0L;
				_lastLiveHeatmapSliceMs = -100000000;
				_trades = new List<HM6Trade>();
				_btArchive = new List<HM6Trade>();
				_tradesByBar = new Dictionary<int, List<HM6Trade>>();
				_btArchiveByBar = new Dictionary<int, List<HM6Trade>>();
				_bubbleRenderCache = new List<HM6BubbleRenderItem>(1024);
				_bubbleSeqCache = new HashSet<long>();
				_bubbleRenderCacheDirty = true;
				_tradeSeq = 0L;
				_passivePocMap = new Dictionary<double, long>();
				_nyTz = ResolveNewYorkTimeZone();
				SyncP();
				_tick = base.Instrument.MasterInstrument.TickSize;
				_decs = (int)Math.Max(0.0, 0.0 - Math.Floor(Math.Log10(_tick)));
				if (BigTradeAutoPorInstrumento)
				{
					BigTradeVisualMinQty = DefaultBigTradeQtyForInstrument();
					SyncP();
				}
				if (base.ChartControl == null)
				{
					return;
				}
				((DispatcherObject)base.ChartControl).Dispatcher.InvokeAsync(delegate
				{
					if (!_chartMouseHandlersAttached)
					{
						((UIElement)base.ChartControl).PreviewMouseDown += MD;
						((UIElement)base.ChartControl).PreviewMouseMove += MM;
						((UIElement)base.ChartControl).PreviewMouseUp += MU;
						_chartMouseHandlersAttached = true;
					}
					EnsureFlowTimer();
					if (UseWpfPanel)
					{
						BuildWpfPanel();
					}
					if (ShowToolbarButton)
					{
						BuildToolbarButton();
					}
				});
			}
			else
			{
				if (base.State != State.Terminated)
				{
					return;
				}
				if (base.ChartControl != null)
				{
					((DispatcherObject)base.ChartControl).Dispatcher.InvokeAsync(delegate
					{
						if (_chartMouseHandlersAttached)
						{
							((UIElement)base.ChartControl).PreviewMouseDown -= MD;
							((UIElement)base.ChartControl).PreviewMouseMove -= MM;
							((UIElement)base.ChartControl).PreviewMouseUp -= MU;
							_chartMouseHandlersAttached = false;
						}
						DisposeFlowTimer();
						RemoveToolbarButton();
						RemoveWpfPanel();
					});
				}
				else
				{
					DisposeFlowTimer();
				}
				FreeDX();
			}
		}

		private void SyncP()
		{
			_pBasicVisual = VerVisualBasico;
			_pF = FiltroQty;
			_pLadF = LadderFiltroQty;
			_pLadderShiftPx = LadderOffsetPx;
			_pLadderBgOpacity = LadderBgOpacity;
			_pL = UmbralLin;
			_pG = 1;
			AgruparTk = 1;
			_pBub = MinBurbuja;
			_pLad = VerLadder;
			_pLin = VerLineas;
			_pSt = VerStacking;
			_pPull = VerPulling;
			_pBubShow = false;
			_pAbs = VerAbsorcion;
			_pDir = Bidireccional;
			_pPoc = VerPOC;
			_pFlow = VerDomFlowPulse;
			_pFlowMin = FlowMinChange;
			_pFlowFadeMs = FlowFadeMs;
			_pFlowOpacity = FlowOpacity;
			_pFlowSize = FlowSize;
			_pFlowLabelMin = FlowLabelMin;
			_pFlowFps = FlowRefreshFps;
			_pHeatmapMinOpacity = HeatmapMinOpacity;
			_pHeatmapContrast = HeatmapPriorityContrast;
			_pHeatmapBarWidth = HeatmapBarWidthPct;
			_pFlowPanelOpacity = FlowPanelOpacity;
			_pPassive = true;
			_pPassiveMinQty = LiquidezPasivaMinQty;
			_pPassiveDepth = LiquidezPasivaProfundidad;
			_pPassiveEventMin = LiquidezPasivaEventoMin;
			_pPassiveAggWindowMs = LiquidezPasivaAggWindowMs;
			_pPassiveRefillWindowMs = LiquidezPasivaRefillWindowMs;
			_pPassiveChartMarkers = _pBasicVisual;
			_pPassiveWallZones = VerMurosLiquidezPasiva;
			_pIltDevZones = VerIltDevZones;
			_pIltDevZoneAttackMinQty = IltDevZoneAtaqueMinQty;
			_pBlock = IltDevZoneAtaqueMinQty;
			_pBtVisualMin = Math.Max(1, BigTradeVisualMinQty);
			_pBtAuto = BigTradeAutoPorInstrumento;
			_pIltDevZoneMinStrength = IltDevZoneFuerzaMin;
			_pZoneCloses = ZoneCierresConfirmacion;
			_pIltDevZoneScalePct = 200;
			_pIltDevZoneBandTicks = 4;
			_pIltDevZoneCandidateMaxBars = 600;
			_pIltDevZoneHistoryBars = 2000;
			_pIltDevZoneAdaptivePct = 75;
			_pIltDevZoneMergeTicks = Math.Max(2, (int)Math.Round((double)_pIltDevZoneBandTicks * 1.5));
			_pIltDevZoneMergeSec = 60;
			_pSRZones = SRZonesActivas;
			_pIltDevSRScalePct = 200;
			_pIltDevSRReAddPct = 40;
			_pIltDevSRWindowMs = 8000;
			_pIltDevSRMinStrength = Math.Min(100, _pIltDevZoneMinStrength + 10);
			_pIltDevSRWallMinPresence = 10;
			_pIltDevSRWallMinAgeMs = 900;
			_pIltDevSRWallFreshMs = 2800;
			_pIltDevSRRetentionBars = 2000;
			_pPassiveWallMaxBars = LiquidezPasivaMuroMaxBars;
			_pPassiveMarkerSize = LiquidezPasivaMarkerSize;
			_pPassiveMarkerLabelMin = _pF;
			_pBubAnimMs = BubbleAnimationMs;
			_pBubGlow = BubbleGlowStrength;
			_pBubPulse = BubblePulseStrength;
			_pBubShockwave = BubbleShockwave;
			_pBubLightning = BubbleLightning;
			_pBtFlash = BigTradeFlash;
			_pBtFlashMs = BigTradeFlashMs;
			_pBtFlashStrength = BigTradeFlashStrength;
			_pKeepHistoricalBubbles = true;
			_pMaxHistoricalBubbles = 350000;
		}

		private void ApplyRuntimeChange(bool trimTradeHistory)
		{
			SyncP();
			lock (_lk)
			{
				InvalidateBubbleRenderCacheLocked();
				if (trimTradeHistory)
				{
					TrimTradeHistoryLocked(true);
				}
				else
				{
					RecalculateMaxTradeVolLocked();
				}
			}
			UpdateWpfStepperValues();
			UpdateWpfToggleValues();
			StartFlowTimerIfNeeded();
			RequestRefresh(true);
		}

		protected override void OnMarketDepth(MarketDepthEventArgs e)
		{
			try
			{
				OnMarketDepthCore(e);
			}
			catch
			{
			}
		}

		private void OnMarketDepthCore(MarketDepthEventArgs e)
		{
			if (e == null || e.Price <= 0.0 || double.IsNaN(e.Price) || double.IsInfinity(e.Price) || (e.MarketDataType != MarketDataType.Ask && e.MarketDataType != MarketDataType.Bid))
			{
				return;
			}
			double num = NormalizeToGroupedPrice(e.Price);
			int iltDevNowMs = IltDevNowMs;
			double num2 = iltDevNowMs;
			bool flag = e.MarketDataType == MarketDataType.Ask;
			bool flag2 = false;
			lock (_lk)
			{
				Dictionary<double, HM6Level> dictionary = (flag ? _ask : _bid);
				Dictionary<double, long> dictionary2 = (flag ? _snap.Ask : _snap.Bid);
				if (e.Operation == Operation.Remove)
				{
					HM6Level value;
					if (dictionary.TryGetValue(num, out value) && value.Qty >= Math.Max(1, _pFlowMin) && (_pPull || _pFlow))
					{
						HM6Pull hM6Pull = new HM6Pull();
						hM6Pull.Price = num;
						hM6Pull.Qty = value.Qty;
						hM6Pull.WasBid = !flag;
						hM6Pull.Time = num2;
						hM6Pull.Fade = 1f;
						_pulls.Add(hM6Pull);
						if (_pulls.Count > 500)
						{
							_pulls.RemoveRange(0, _pulls.Count - 500);
						}
						HM6Flow hM6Flow = new HM6Flow();
						hM6Flow.Price = num;
						hM6Flow.Qty = value.Qty;
						hM6Flow.WasBid = !flag;
						hM6Flow.IsAdd = false;
						hM6Flow.Time = num2;
						hM6Flow.Fade = 1f;
						_flows.Add(hM6Flow);
						TrimFlowEventsLocked(350);
						flag2 = true;
					}
					if (dictionary.TryGetValue(num, out value))
					{
						flag2 |= UpdatePassiveLiquidityTransitionLocked(value, num, !flag, value.Qty, 0L, num2);
					}
					dictionary.Remove(num);
					dictionary2.Remove(num);
				}
				else
				{
					long volume = e.Volume;
					HM6Level value2;
					if (dictionary.TryGetValue(num, out value2))
					{
						long num3 = (value2.PrevQty = value2.Qty);
						value2.Qty = volume;
						value2.UpdCnt++;
						value2.LastSeen = num2;
						if (volume > value2.MaxQty)
						{
							value2.MaxQty = volume;
						}
						if (volume != num3)
						{
							flag2 |= UpdatePassiveLiquidityTransitionLocked(value2, num, !flag, num3, volume, num2);
						}
						long num4 = Math.Abs(volume - num3);
						long num5 = Math.Max(1L, _pPassiveEventMin);
						if (volume > num3)
						{
							value2.AddCnt++;
							AddPassivePoc(num, volume - num3);
							if (!(value2.LastRefillTime > 0.0) || !(Math.Abs(value2.LastRefillTime - num2) < 0.5) || !value2.LastDepleteWasConsume)
							{
								if (value2.LastAddTime <= 0.0 || num2 - value2.LastAddTime > 2500.0)
								{
									value2.RecentAddQty = 0L;
									value2.StackCnt = 0;
									value2.StackWindowStart = num2;
								}
								value2.RecentAddQty += num4;
								value2.LastAddTime = num2;
								if (num4 >= num5)
								{
									value2.StackCnt++;
								}
							}
							flag2 |= TryEmitStackingMarkerLocked(value2, num, !flag, volume, num2);
						}
						else if (volume < num3)
						{
							value2.RedCnt++;
							if (value2.LastDepleteWasConsume)
							{
								value2.StackCnt = Math.Max(0, value2.StackCnt - 1);
								value2.RecentAddQty = Math.Max(0L, value2.RecentAddQty - num4);
							}
							else
							{
								value2.StackCnt = 0;
								value2.RecentAddQty = 0L;
								value2.StackWindowStart = 0.0;
							}
						}
						if (num4 >= Math.Max(1, _pFlowMin))
						{
							HM6Flow hM6Flow2 = new HM6Flow();
							hM6Flow2.Price = num;
							hM6Flow2.Qty = num4;
							hM6Flow2.WasBid = !flag;
							hM6Flow2.IsAdd = volume > num3;
							hM6Flow2.Time = num2;
							hM6Flow2.Fade = 1f;
							_flows.Add(hM6Flow2);
							TrimFlowEventsLocked(600);
							flag2 = true;
						}
					}
					else
					{
						HM6Level hM6Level = new HM6Level();
						hM6Level.Qty = (hM6Level.MaxQty = (hM6Level.PrevQty = volume));
						hM6Level.FirstSeen = (hM6Level.LastSeen = num2);
						hM6Level.UpdCnt = 1;
						hM6Level.RecentAddQty = volume;
						hM6Level.StackWindowStart = num2;
						hM6Level.LastAddTime = num2;
						hM6Level.StackCnt = ((volume >= Math.Max(1L, _pPassiveEventMin)) ? 1 : 0);
						dictionary[num] = hM6Level;
						AddPassivePoc(num, volume);
						if (volume >= Math.Max(1, _pPassiveEventMin))
						{
							flag2 |= AddPassiveEventLocked(num, volume, !flag, 3, num2);
						}
						UpdatePassiveWallZoneLocked(num, !flag, 0L, volume, 3, volume, num2);
					}
					dictionary2[num] = volume;
					if (volume > _maxQty)
					{
						_maxQty = volume;
					}
				}
				_hasDom = (_snap != null && _snap.HasData) || (_hist != null && _hist.Count > 0) || _maxQty > 0;
				if (e.Operation == Operation.Remove)
				{
					if (flag ? (num <= _bestAsk + 1E-09) : (num >= _bestBid - 1E-09))
					{
						CalcBest();
					}
				}
				else
				{
					bool flag3 = false;
					if (flag)
					{
						if (num < _bestAsk)
						{
							_bestAsk = num;
							flag3 = true;
						}
					}
					else if (num > _bestBid)
					{
						_bestBid = num;
						flag3 = true;
					}
					if (flag3 || _midPx <= 0.0)
					{
						if (_bestAsk < double.MaxValue && _bestBid > 0.0)
						{
							_midPx = (_bestAsk + _bestBid) * 0.5;
						}
						else if (base.CurrentBar >= 0 && base.Close != null && base.Close.Count > 0)
						{
							_midPx = base.Close[0];
						}
					}
				}
				PublishCurrentSnapshotLocked(iltDevNowMs, false);
				PublishLiveHeatmapSliceLocked(iltDevNowMs, flag2);
			}
			CleanEventsThrottled(iltDevNowMs);
			if (flag2)
			{
				StartFlowTimerIfNeeded();
			}
			RequestRefresh(false);
		}

		private void AddPassivePoc(double price, long addedQty)
		{
			if (addedQty > 0)
			{
				if (_passivePocMap == null)
				{
					_passivePocMap = new Dictionary<double, long>();
				}
				long value;
				_passivePocMap.TryGetValue(price, out value);
				value += addedQty;
				_passivePocMap[price] = value;
				if (value > _passivePocVol)
				{
					_passivePocVol = value;
					_passivePocPrice = price;
				}
			}
		}

		private void CalcBest()
		{
			_bestAsk = double.MaxValue;
			_bestBid = 0.0;
			foreach (double key in _snap.Ask.Keys)
			{
				double num = key;
				if (num < _bestAsk)
				{
					_bestAsk = num;
				}
			}
			foreach (double key2 in _snap.Bid.Keys)
			{
				double num2 = key2;
				if (num2 > _bestBid)
				{
					_bestBid = num2;
				}
			}
			if (_bestAsk < double.MaxValue && _bestBid > 0.0)
			{
				_midPx = (_bestAsk + _bestBid) * 0.5;
			}
			else if (base.CurrentBar >= 0 && base.Close != null && base.Close.Count > 0)
			{
				_midPx = base.Close[0];
			}
		}

		private bool UpdatePassiveLiquidityTransitionLocked(HM6Level lv, double price, bool wasBid, long prevQty, long newQty, double now)
		{
			if (lv == null || prevQty == newQty)
			{
				return false;
			}
			long num = Math.Abs(newQty - prevQty);
			if (num <= 0)
			{
				return false;
			}
			bool flag = false;
			long num2 = Math.Max(1L, _pPassiveEventMin);
			if (newQty < prevQty)
			{
				bool flag2 = lv.LastAggTime > 0.0 && now - lv.LastAggTime <= (double)Math.Max(120, _pPassiveAggWindowMs) && lv.LastAggVol > 0 && lv.LastAggVol >= Math.Max(1L, Math.Min(num, num2));
				lv.LastDepleteQty = num;
				lv.LastDepleteTime = now;
				if (flag2)
				{
					if (lv.LastConsumeTime <= 0.0 || now - lv.LastConsumeTime > 1800.0)
					{
						lv.RecentConsumeQty = 0L;
					}
					lv.RecentConsumeQty += num;
					lv.LastConsumeTime = now;
					lv.LastDepleteWasConsume = true;
					lv.RecentPullQty = 0L;
					lv.LastPullTime = 0.0;
					flag |= AddPassiveEventLocked(price, num, wasBid, 1, now);
					UpdatePassiveWallZoneLocked(price, wasBid, prevQty, newQty, 1, num, now);
				}
				else
				{
					if (lv.LastPullTime <= 0.0 || now - lv.LastPullTime > 1800.0)
					{
						lv.RecentPullQty = 0L;
					}
					lv.RecentPullQty += num;
					lv.LastPullTime = now;
					lv.LastDepleteWasConsume = false;
					flag |= AddPassiveEventLocked(price, num, wasBid, 0, now);
					UpdatePassiveWallZoneLocked(price, wasBid, prevQty, newQty, 0, num, now);
				}
			}
			else
			{
				bool flag3 = lv.LastDepleteTime > 0.0 && now - lv.LastDepleteTime <= (double)Math.Max(250, _pPassiveRefillWindowMs);
				bool flag4 = num >= num2 || (lv.LastDepleteQty > 0 && num >= Math.Max(1L, lv.LastDepleteQty / 2));
				if (flag3 && flag4 && lv.LastDepleteWasConsume)
				{
					if (lv.LastRefillTime <= 0.0 || now - lv.LastRefillTime > (double)Math.Max(500, _pPassiveRefillWindowMs))
					{
						lv.RecentRefillQty = 0L;
					}
					lv.RecentRefillQty += num;
					lv.LastRefillTime = now;
					lv.RefillCount++;
					flag |= AddPassiveEventLocked(price, num, wasBid, 2, now);
					UpdatePassiveWallZoneLocked(price, wasBid, prevQty, newQty, 2, num, now);
				}
				else
				{
					if (num >= num2)
					{
						flag |= AddPassiveEventLocked(price, num, wasBid, 3, now);
					}
					UpdatePassiveWallZoneLocked(price, wasBid, prevQty, newQty, 3, num, now);
				}
			}
			return flag;
		}

		private void RegisterPassiveAggressionLocked(bool wasBid, double price, long vol, double now)
		{
			if (vol <= 0)
			{
				return;
			}
			MarkPassiveWallReachedLocked(price, wasBid, vol, now);
			Dictionary<double, HM6Level> dictionary = (wasBid ? _bid : _ask);
			int tapeSide = ((!wasBid) ? 1 : (-1));
			long num = CurrentIltDevZoneAttackMinQty(now);
			double num2 = Math.Max(120, _pPassiveAggWindowMs);
			if (dictionary == null)
			{
				RegisterGhostAggressionLocked(wasBid, price, vol, now);
				return;
			}
			HM6Level value;
			if (!dictionary.TryGetValue(price, out value) || value == null)
			{
				RegisterGhostAggressionLocked(wasBid, price, vol, now);
				return;
			}
			if (value.LastAggTime <= 0.0 || now - value.LastAggTime > num2)
			{
				value.LastAggVol = 0L;
				value.LastWallAttackRegisteredVol = 0L;
				value.LastWallAttackRegisteredTime = 0.0;
			}
			value.LastAggVol += vol;
			value.LastAggTime = now;
			if (wasBid)
			{
				value.DeltaSell += vol;
			}
			else
			{
				value.DeltaBuy += vol;
			}
			if (value.LastDepleteTime > 0.0 && !value.LastDepleteWasConsume && now - value.LastDepleteTime <= num2 && value.LastDepleteQty > 0 && value.LastAggVol >= Math.Max(1L, Math.Min(value.LastDepleteQty, Math.Max(1L, _pPassiveEventMin))))
			{
				long lastDepleteQty = value.LastDepleteQty;
				value.LastDepleteWasConsume = true;
				value.RecentConsumeQty = lastDepleteQty;
				value.LastConsumeTime = now;
				value.RecentPullQty = 0L;
				value.LastPullTime = 0.0;
				AddPassiveEventLocked(price, lastDepleteQty, wasBid, 1, now);
				UpdatePassiveWallZoneLocked(price, wasBid, value.Qty + lastDepleteQty, value.Qty, 1, lastDepleteQty, now);
			}
			bool flag = value.LastAggVol >= num && value.LastWallAttackRegisteredVol < num;
			bool flag2 = value.LastAggVol >= value.LastWallAttackRegisteredVol + num;
			if (flag || flag2)
			{
				long vol2 = (flag ? value.LastAggVol : Math.Max(num, value.LastAggVol - value.LastWallAttackRegisteredVol));
				RegisterWallAttackFromAggressiveTradeLocked(price, tapeSide, vol2, now);
				value.LastWallAttackRegisteredVol = value.LastAggVol;
				value.LastWallAttackRegisteredTime = now;
			}
		}

		private void RegisterGhostAggressionLocked(bool wasBid, double price, long vol, double now)
		{
			if (vol <= 0)
			{
				return;
			}
			long num = CurrentIltDevZoneAttackMinQty(now);
			double num2 = Math.Max(120, _pPassiveAggWindowMs);
			if (_ghostAgg == null)
			{
				_ghostAgg = new Dictionary<string, HM6GhostAgg>();
			}
			string key = PassiveZoneKey(price, wasBid);
			HM6GhostAgg value;
			if (!_ghostAgg.TryGetValue(key, out value) || value == null)
			{
				value = new HM6GhostAgg();
				_ghostAgg[key] = value;
			}
			if (value.Time <= 0.0 || now - value.Time > num2)
			{
				value.Vol = 0L;
				value.Reg = 0L;
			}
			value.Vol += vol;
			value.Time = now;
			bool flag = value.Vol >= num && value.Reg < num;
			bool flag2 = value.Vol >= value.Reg + num;
			if (flag || flag2)
			{
				long vol2 = (flag ? value.Vol : Math.Max(num, value.Vol - value.Reg));
				RegisterWallAttackFromAggressiveTradeLocked(price, (!wasBid) ? 1 : (-1), vol2, now);
				value.Reg = value.Vol;
			}
			if (_ghostAgg.Count <= 600)
			{
				return;
			}
			List<string> list = null;
			foreach (KeyValuePair<string, HM6GhostAgg> item in _ghostAgg)
			{
				if (item.Value == null || now - item.Value.Time > num2 * 4.0)
				{
					if (list == null)
					{
						list = new List<string>();
					}
					list.Add(item.Key);
				}
			}
			if (list != null)
			{
				for (int i = 0; i < list.Count; i++)
				{
					_ghostAgg.Remove(list[i]);
				}
			}
		}

		private bool AddPassiveEventLocked(double price, long qty, bool wasBid, int type, double now)
		{
			if (_pSRZones && (type == 2 || type == 3) && qty > 0)
			{
				TryConfirmSRPendingLocked(price, wasBid, qty, now);
			}
			if (!_pPassive || qty < Math.Max(1, _pPassiveEventMin))
			{
				return false;
			}
			if (_passiveEvents == null)
			{
				_passiveEvents = new List<HM6PassiveEvent>();
			}
			HM6PassiveEvent hM6PassiveEvent = new HM6PassiveEvent();
			hM6PassiveEvent.Price = price;
			hM6PassiveEvent.Qty = qty;
			hM6PassiveEvent.WasBid = wasBid;
			hM6PassiveEvent.Type = type;
			hM6PassiveEvent.Time = now;
			hM6PassiveEvent.SliceSeq = Math.Max(1L, _liveHeatmapSeq + 1);
			hM6PassiveEvent.Fade = 1f;
			hM6PassiveEvent.BarIdx = PrimaryBarIndexSafe();
			_passiveEvents.Add(hM6PassiveEvent);
			if (_pendingPassiveHeatmapEvents == null)
			{
				_pendingPassiveHeatmapEvents = new List<HM6PassiveEvent>();
			}
			_pendingPassiveHeatmapEvents.Add(hM6PassiveEvent);
			if (_passiveEvents.Count > 20000)
			{
				_passiveEvents.RemoveRange(0, _passiveEvents.Count - 18000);
			}
			return true;
		}

		private string PassiveZoneKey(double price, bool wasBid)
		{
			return (wasBid ? "B" : "A") + NormalizeToTickPriceKey(price);
		}

		private void UpdatePassiveWallZoneLocked(double price, bool wasBid, long prevQty, long newQty, int eventType, long eventQty, double now)
		{
			if (!_pPassive || !PassiveZoneEngineRequired || price <= 0.0 || double.IsNaN(price) || double.IsInfinity(price))
			{
				return;
			}
			if (_passiveZones == null)
			{
				_passiveZones = new Dictionary<string, HM6PassiveZone>();
			}
			long num = Math.Max(1L, _pWallMinQty);
			long num2 = Math.Max(1L, _pPassiveEventMin);
			if (newQty < num && Math.Abs(newQty - prevQty) < num2 && eventQty < num2)
			{
				return;
			}
			string key = PassiveZoneKey(price, wasBid);
			HM6PassiveZone value;
			if (!_passiveZones.TryGetValue(key, out value) || value == null)
			{
				value = new HM6PassiveZone();
				value.Price = price;
				value.WasBid = wasBid;
				value.StartBar = PrimaryBarIndexSafe();
				value.LastEventBar = -1;
				value.FirstTime = now;
				value.FirstSeq = Math.Max(1L, _liveHeatmapSeq + 1);
				value.LastSeq = value.FirstSeq;
				value.LastEventSeq = value.FirstSeq;
				value.Active = true;
				value.LastEventType = 3;
				_passiveZones[key] = value;
			}
			value.LastBar = PrimaryBarIndexSafe();
			value.LastQty = Math.Max(0L, newQty);
			if (newQty > value.MaxQty)
			{
				value.MaxQty = newQty;
			}
			value.LastUpdateTime = now;
			long num3 = Math.Max(1L, _liveHeatmapSeq + 1);
			if (value.FirstSeq <= 0)
			{
				value.FirstSeq = num3;
			}
			if (num3 > value.LastSeq)
			{
				value.LastSeq = num3;
			}
			long num4 = Math.Abs(newQty - prevQty);
			if (Math.Max(eventQty, num4) >= Math.Max(1L, _pPassiveEventMin))
			{
				value.LastEventQty = Math.Max(eventQty, num4);
				value.EventCount++;
				value.LastEventBar = PrimaryBarIndexSafe();
				value.LastEventSeq = num3;
			}
			if (newQty > prevQty)
			{
				value.AddQty += num4;
			}
			if (_pSRZones && newQty > prevQty && value.MaxQty >= IltDevSRPassiveMinQty() && _srZones != null && _srZones.Count > 0)
			{
				double num5 = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25) * SRBandTicks;
				for (int i = 0; i < _srZones.Count; i++)
				{
					HM6SRZone hM6SRZone = _srZones[i];
					if (hM6SRZone != null && hM6SRZone.State < 2 && hM6SRZone.AskSide != value.WasBid && Math.Abs(hM6SRZone.Price - value.Price) <= num5 + 1E-09 && IsQualifiedIltDevWallLocked(value, hM6SRZone.Price, value.WasBid, now))
					{
						long num6 = ((hM6SRZone.InitiativeQty > 0) ? hM6SRZone.InitiativeQty : Math.Max(1L, hM6SRZone.Qty * 100 / (100L + (long)Math.Max(10, Math.Min(100, _pIltDevSRReAddPct)))));
						long reAddQty = ((hM6SRZone.ReAddQty > 0) ? hM6SRZone.ReAddQty : Math.Max(1L, hM6SRZone.Qty - num6));
						int originBar = ((hM6SRZone.VisualEventBar >= 0) ? hM6SRZone.VisualEventBar : hM6SRZone.StartBar);
						RegisterIltDevSupportLocked(hM6SRZone.Price, value.WasBid, num6, reAddQty, value, now, originBar);
						break;
					}
				}
			}
			if (eventType == 0)
			{
				value.PullQty += Math.Max(eventQty, num4);
				if (value.PullBurstLast <= 0.0 || now - value.PullBurstLast > 2000.0)
				{
					value.PullBurstQty = 0L;
					value.PullBurstStart = now;
					value.PullNearTicks = double.MaxValue;
				}
				value.PullBurstQty += Math.Max(eventQty, num4);
				value.PullBurstLast = now;
				if (_midPx > 0.0)
				{
					double num7 = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25);
					double num8 = Math.Abs(price - _midPx) / num7;
					if (num8 < value.PullNearTicks)
					{
						value.PullNearTicks = num8;
					}
				}
			}
			if (eventType == 1)
			{
				value.ConsumeQty += Math.Max(eventQty, num4);
			}
			if (eventType == 2)
			{
				value.RefillQty += Math.Max(eventQty, num4);
				if (Math.Max(eventQty, num4) >= num2)
				{
					value.RefillEvents++;
				}
				if (!value.IcebergConfirmed && ((value.RefillEvents >= 2 && value.ConsumeQty >= Math.Max(InstitutionalMinQty(), (long)Math.Round((double)Math.Max(1L, value.MaxQty) * 1.2))) || (value.RefillEvents >= 4 && value.ConsumeQty >= InstitutionalMinQty() * 3)))
				{
					value.IcebergConfirmed = true;
					value.IcebergQty = Math.Max(value.ConsumeQty, value.RefillQty);
				}
			}
			if (eventType >= 0 && Math.Max(eventQty, num4) >= num2)
			{
				value.LastEventType = eventType;
				value.LastEventTime = now;
				value.LastEventSeq = num3;
				if (eventType != 0 || newQty >= num)
				{
					value.SpoofLabelInteracted = false;
					value.SpoofInteractionTime = 0.0;
					value.SpoofInteractionBar = -1;
				}
			}
			else if (newQty > prevQty && num4 >= num2 && newQty >= num)
			{
				value.LastEventType = 3;
				value.LastEventTime = now;
				value.LastEventSeq = num3;
				value.SpoofLabelInteracted = false;
				value.SpoofInteractionTime = 0.0;
				value.SpoofInteractionBar = -1;
			}
			if (newQty < num && (eventType == 0 || eventType == 1))
			{
				value.Active = false;
			}
			else if (newQty >= num)
			{
				value.Active = true;
			}
			if ((eventType == 0 || eventType == 1) && newQty < num)
			{
				RegisterConstructedWallFromCollapseLocked(value, eventType, prevQty, newQty, Math.Max(eventQty, num4), now);
			}
			if (eventType == 2 && Math.Max(eventQty, num4) >= num2)
			{
				RegisterConstructedWallFromRefillLocked(value, Math.Max(eventQty, num4), now);
			}
			if (eventType == 1)
			{
				RegisterWallDefenseAttemptLocked(value, now);
			}
			TrimPassiveZonesLocked(now);
		}

		private void MarkPassiveWallReachedLocked(double price, bool wasBid, long vol, double now)
		{
			if (!_pPassive || !PassiveZoneEngineRequired || _passiveZones == null)
			{
				return;
			}
			string key = PassiveZoneKey(price, wasBid);
			HM6PassiveZone value;
			if (_passiveZones.TryGetValue(key, out value) && value != null)
			{
				if (value.ReachedTime <= 0.0 || now - value.ReachedTime > 750.0)
				{
					value.TouchCount++;
				}
				value.Reached = true;
				value.ReachedTime = now;
				value.LastBar = PrimaryBarIndexSafe();
				value.LastUpdateTime = now;
				long num = Math.Max(1L, _liveHeatmapSeq + 1);
				if (num > value.LastSeq)
				{
					value.LastSeq = num;
				}
			}
		}

		private void MarkPassiveWallsTouchedByBarLocked(double low, double high, double now)
		{
			if (!_pPassive || !PassiveZoneEngineRequired || _passiveZones == null || _passiveZones.Count == 0 || double.IsNaN(low) || double.IsNaN(high) || double.IsInfinity(low) || double.IsInfinity(high))
			{
				return;
			}
			if (high < low)
			{
				double num = high;
				high = low;
				low = num;
			}
			double num2 = Math.Max(_tick, 1E-10) * 0.55;
			int lastBar = PrimaryBarIndexSafe();
			foreach (KeyValuePair<string, HM6PassiveZone> passiveZone in _passiveZones)
			{
				HM6PassiveZone value = passiveZone.Value;
				if (value != null && !(value.Price < low - num2) && !(value.Price > high + num2))
				{
					if (!value.Reached)
					{
						value.TouchCount++;
					}
					value.Reached = true;
					value.ReachedTime = now;
					value.LastBar = lastBar;
					value.LastUpdateTime = now;
					long num3 = Math.Max(1L, _liveHeatmapSeq + 1);
					if (num3 > value.LastSeq)
					{
						value.LastSeq = num3;
					}
				}
			}
		}

		private void RegisterConstructedWallFromCollapseLocked(HM6PassiveZone z, int eventType, long prevQty, long newQty, long eventQty, double now)
		{
			if (!_pPassive || !_pIltDevZones || z == null || z.Price <= 0.0 || double.IsNaN(z.Price) || double.IsInfinity(z.Price))
			{
				return;
			}
			long num = Math.Max(1L, _pWallMinQty);
			long num2 = Math.Max(1L, _pPassiveEventMin);
			long num3 = Math.Max(0L, prevQty - newQty);
			long num4 = Math.Max(eventQty, num3);
			if (z.MaxQty < Math.Max(1L, IltDevZoneInstitutionalMinQty() / 2) || (z.MaxQty < Math.Max(num * 2, num2 * 3) && z.AddQty < num2 * 3 && z.EventCount < 2 && z.RefillQty < num2))
			{
				return;
			}
			double num5 = ((z.MaxQty > 0) ? ((double)newQty / (double)Math.Max(1L, z.MaxQty)) : 0.0);
			if (newQty >= num && !(num5 <= 0.25) && num3 < Math.Max(num2, (long)Math.Round((double)Math.Max(1L, z.MaxQty) * 0.55)))
			{
				return;
			}
			double num6 = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25);
			double num7 = Math.Max(4500.0, Math.Max(120.0, _pPassiveAggWindowMs) * 10.0);
			bool flag = z.Reached && z.ReachedTime > 0.0 && now - z.ReachedTime <= num7;
			bool flag2 = eventType == 1 || z.ConsumeQty >= num2;
			double num8 = double.NaN;
			double num9 = double.NaN;
			double num10 = double.NaN;
			try
			{
				if (base.CurrentBar >= 0 && base.Low != null && base.High != null && base.Close != null && base.Low.Count > 0 && base.High.Count > 0 && base.Close.Count > 0)
				{
					num8 = base.Low[0];
					num9 = base.High[0];
					num10 = base.Close[0];
				}
			}
			catch
			{
			}
			if (double.IsNaN(num10) || num10 <= 0.0)
			{
				num10 = ((_midPx > 0.0) ? _midPx : z.Price);
			}
			if (!double.IsNaN(num8) && !double.IsNaN(num9) && num9 < num8)
			{
				double num11 = num9;
				num9 = num8;
				num8 = num11;
			}
			double num12 = num6 * 1.25;
			bool flag3 = !double.IsNaN(num8) && !double.IsNaN(num9) && z.Price >= num8 - num12 && z.Price <= num9 + num12;
			bool flag4 = Math.Abs(num10 - z.Price) <= num6 * 8.0;
			if ((!flag2 && !flag && !flag3 && (!flag4 || num4 < num2)) || (eventType == 0 && !flag2))
			{
				return;
			}
			if (!z.Reached && (flag3 || flag4 || flag2))
			{
				z.Reached = true;
				z.ReachedTime = now;
				z.TouchCount++;
			}
			if (_defendedWallLines == null)
			{
				_defendedWallLines = new List<HM6DefendedWallLine>();
			}
			double num13 = Math.Max(_tick * 0.55, 1E-10);
			HM6DefendedWallLine hM6DefendedWallLine = null;
			for (int num14 = _defendedWallLines.Count - 1; num14 >= 0; num14--)
			{
				HM6DefendedWallLine hM6DefendedWallLine2 = _defendedWallLines[num14];
				if (hM6DefendedWallLine2 == null)
				{
					_defendedWallLines.RemoveAt(num14);
				}
				else if (hM6DefendedWallLine2.WasBid == z.WasBid && Math.Abs(hM6DefendedWallLine2.Price - z.Price) <= num13 && !hM6DefendedWallLine2.Broken && (now - hM6DefendedWallLine2.EventTime <= 30000.0 || hM6DefendedWallLine2.Confirmed))
				{
					hM6DefendedWallLine = hM6DefendedWallLine2;
					break;
				}
			}
			if (hM6DefendedWallLine == null)
			{
				hM6DefendedWallLine = new HM6DefendedWallLine();
				int num15 = ((z.LastEventBar >= 0) ? z.LastEventBar : PrimaryBarIndexSafe());
				hM6DefendedWallLine.Price = AnchorEventPriceToBarLocked(z.Price, num15);
				hM6DefendedWallLine.WasBid = z.WasBid;
				hM6DefendedWallLine.StartBar = num15;
				hM6DefendedWallLine.VisualEventBar = num15;
				hM6DefendedWallLine.BreakBar = -1;
				hM6DefendedWallLine.EndBar = -1;
				hM6DefendedWallLine.StartSeq = Math.Max(1L, _liveHeatmapSeq + 1);
				hM6DefendedWallLine.EventTime = now;
				hM6DefendedWallLine.AttackThresholdAtEvent = CurrentIltDevZoneAttackMinQty(now);
				hM6DefendedWallLine.ConfirmTime = 0.0;
				hM6DefendedWallLine.Active = false;
				hM6DefendedWallLine.Confirmed = false;
				hM6DefendedWallLine.Broken = false;
				_defendedWallLines.Add(hM6DefendedWallLine);
			}
			if (hM6DefendedWallLine.VisualEventBar < 0)
			{
				hM6DefendedWallLine.VisualEventBar = ((z.LastEventBar >= 0) ? z.LastEventBar : PrimaryBarIndexSafe());
			}
			hM6DefendedWallLine.LastUpdateTime = now;
			hM6DefendedWallLine.ExecutedQty = Math.Max(hM6DefendedWallLine.ExecutedQty, Math.Max(1L, num4));
			hM6DefendedWallLine.RefillQty = Math.Max(hM6DefendedWallLine.RefillQty, Math.Max(0L, z.RefillQty));
			hM6DefendedWallLine.BuiltFromExecutedCollapse = true;
			hM6DefendedWallLine.DeltaRequired = false;
			if (Math.Abs(hM6DefendedWallLine.AttackDelta) < Math.Max(1L, num4))
			{
				hM6DefendedWallLine.AttackDelta = (z.WasBid ? (-Math.Max(1L, num4)) : Math.Max(1L, num4));
			}
			if (double.IsNaN(num10) || num10 <= 0.0)
			{
				if (!(_midPx > 0.0))
				{
					double price = z.Price;
				}
				else
				{
					double midPx = _midPx;
				}
			}
			if (!hM6DefendedWallLine.Confirmed && !hM6DefendedWallLine.Broken)
			{
				hM6DefendedWallLine.PriceConfirmed = false;
				hM6DefendedWallLine.StrongAbsorption = false;
				hM6DefendedWallLine.DefenseScore = Math.Max(hM6DefendedWallLine.DefenseScore, 44.0);
				hM6DefendedWallLine.Active = false;
				hM6DefendedWallLine.ConfirmTime = 0.0;
				hM6DefendedWallLine.LastUpdateTime = now;
			}
			TrimDefendedWallLinesLocked();
		}

		private void RegisterConstructedWallFromRefillLocked(HM6PassiveZone z, long refillQty, double now)
		{
			if (!_pPassive || !_pIltDevZones || z == null || z.Price <= 0.0 || double.IsNaN(z.Price) || double.IsInfinity(z.Price))
			{
				return;
			}
			long num = Math.Max(1L, _pWallMinQty);
			long num2 = Math.Max(1L, _pPassiveEventMin);
			long val = Math.Max(1L, refillQty);
			if (z.MaxQty < Math.Max(1L, IltDevZoneInstitutionalMinQty() / 2) || (z.MaxQty < Math.Max(num * 2, num2 * 2) && z.EventCount < 2 && z.AddQty < num2 * 2 && z.ConsumeQty < num2 && z.RefillQty < num2))
			{
				return;
			}
			double num3 = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25);
			double num4 = Math.Max(5000.0, Math.Max(120.0, _pPassiveAggWindowMs) * 10.0);
			bool flag = z.Reached && z.ReachedTime > 0.0 && now - z.ReachedTime <= num4;
			double num5 = double.NaN;
			double num6 = double.NaN;
			double num7 = double.NaN;
			try
			{
				if (base.CurrentBar >= 0 && base.Low != null && base.High != null && base.Close != null && base.Low.Count > 0 && base.High.Count > 0 && base.Close.Count > 0)
				{
					num5 = base.Low[0];
					num6 = base.High[0];
					num7 = base.Close[0];
				}
			}
			catch
			{
			}
			if (double.IsNaN(num7) || num7 <= 0.0)
			{
				num7 = ((_midPx > 0.0) ? _midPx : z.Price);
			}
			if (!double.IsNaN(num5) && !double.IsNaN(num6) && num6 < num5)
			{
				double num8 = num6;
				num6 = num5;
				num5 = num8;
			}
			double num9 = num3 * 1.25;
			bool flag2 = !double.IsNaN(num5) && !double.IsNaN(num6) && z.Price >= num5 - num9 && z.Price <= num6 + num9;
			bool flag3 = Math.Abs(num7 - z.Price) <= num3 * 8.0;
			if (!flag && !flag2 && !flag3 && z.ConsumeQty < num2)
			{
				return;
			}
			if (!z.Reached)
			{
				z.Reached = true;
				z.ReachedTime = now;
				z.TouchCount++;
			}
			if (_defendedWallLines == null)
			{
				_defendedWallLines = new List<HM6DefendedWallLine>();
			}
			double num10 = Math.Max(_tick * 0.55, 1E-10);
			HM6DefendedWallLine hM6DefendedWallLine = null;
			for (int num11 = _defendedWallLines.Count - 1; num11 >= 0; num11--)
			{
				HM6DefendedWallLine hM6DefendedWallLine2 = _defendedWallLines[num11];
				if (hM6DefendedWallLine2 == null)
				{
					_defendedWallLines.RemoveAt(num11);
				}
				else if (hM6DefendedWallLine2.WasBid == z.WasBid && Math.Abs(hM6DefendedWallLine2.Price - z.Price) <= num10 && !hM6DefendedWallLine2.Broken && (now - hM6DefendedWallLine2.EventTime <= 30000.0 || hM6DefendedWallLine2.Confirmed))
				{
					hM6DefendedWallLine = hM6DefendedWallLine2;
					break;
				}
			}
			if (hM6DefendedWallLine == null)
			{
				hM6DefendedWallLine = new HM6DefendedWallLine();
				int num12 = ((z.LastEventBar >= 0) ? z.LastEventBar : PrimaryBarIndexSafe());
				hM6DefendedWallLine.Price = AnchorEventPriceToBarLocked(z.Price, num12);
				hM6DefendedWallLine.WasBid = z.WasBid;
				hM6DefendedWallLine.StartBar = num12;
				hM6DefendedWallLine.VisualEventBar = num12;
				hM6DefendedWallLine.BreakBar = -1;
				hM6DefendedWallLine.EndBar = -1;
				hM6DefendedWallLine.StartSeq = Math.Max(1L, (z.LastEventSeq > 0) ? z.LastEventSeq : (_liveHeatmapSeq + 1));
				hM6DefendedWallLine.EventTime = now;
				hM6DefendedWallLine.AttackThresholdAtEvent = CurrentIltDevZoneAttackMinQty(now);
				hM6DefendedWallLine.ConfirmTime = 0.0;
				hM6DefendedWallLine.Active = false;
				hM6DefendedWallLine.Confirmed = false;
				hM6DefendedWallLine.Broken = false;
				_defendedWallLines.Add(hM6DefendedWallLine);
			}
			if (hM6DefendedWallLine.VisualEventBar < 0)
			{
				hM6DefendedWallLine.VisualEventBar = ((z.LastEventBar >= 0) ? z.LastEventBar : PrimaryBarIndexSafe());
			}
			hM6DefendedWallLine.LastUpdateTime = now;
			long num13 = Math.Max(0L, z.ConsumeQty);
			if (num13 > 0)
			{
				hM6DefendedWallLine.ExecutedQty = Math.Max(hM6DefendedWallLine.ExecutedQty, num13);
			}
			hM6DefendedWallLine.RefillQty = Math.Max(hM6DefendedWallLine.RefillQty, Math.Max(val, z.RefillQty));
			hM6DefendedWallLine.BuiltFromExecutedCollapse = true;
			hM6DefendedWallLine.DeltaRequired = false;
			if (num13 > 0 && Math.Abs(hM6DefendedWallLine.AttackDelta) < num13)
			{
				hM6DefendedWallLine.AttackDelta = (z.WasBid ? (-num13) : num13);
			}
			if ((z.WasBid ? (num7 >= z.Price + num3 * 2.0) : (num7 <= z.Price - num3 * 2.0)) && !hM6DefendedWallLine.Confirmed && !hM6DefendedWallLine.Broken)
			{
				hM6DefendedWallLine.PriceConfirmed = true;
				hM6DefendedWallLine.RefillConfirmed = true;
				hM6DefendedWallLine.DefenseScore = Math.Max(hM6DefendedWallLine.DefenseScore, 58.0);
				hM6DefendedWallLine.LastUpdateTime = now;
			}
			TrimDefendedWallLinesLocked();
		}

		private void RegisterWallDefenseAttemptLocked(HM6PassiveZone z, double now)
		{
			if (!_pPassive || !_pIltDevZones || z == null)
			{
				return;
			}
			long num = Math.Max(1L, _pWallMinQty);
			long num2 = CurrentIltDevZoneAttackMinQty(now);
			long num3 = Math.Max(1L, Math.Max(_pPassiveEventMin, num2));
			long num4 = Math.Max(1L, IltDevZoneInstitutionalMinQtyFor(num2) / 2);
			bool flag = z.MaxQty >= num4 || Math.Max(z.ConsumeQty, Math.Max(z.RefillQty, z.LastEventQty)) >= num4;
			bool flag2 = z.MaxQty >= Math.Max(num * 2, num3) || z.EventCount >= 2 || z.RefillQty >= Math.Max(1L, num3 / 2);
			if (!flag || !flag2)
			{
				return;
			}
			if (_defendedWallLines == null)
			{
				_defendedWallLines = new List<HM6DefendedWallLine>();
			}
			double num5 = Math.Max(_tick * 0.55, 1E-10);
			for (int num6 = _defendedWallLines.Count - 1; num6 >= 0; num6--)
			{
				HM6DefendedWallLine hM6DefendedWallLine = _defendedWallLines[num6];
				if (hM6DefendedWallLine == null)
				{
					_defendedWallLines.RemoveAt(num6);
				}
				else if (hM6DefendedWallLine.WasBid == z.WasBid && Math.Abs(hM6DefendedWallLine.Price - z.Price) <= num5 && now - hM6DefendedWallLine.EventTime < 1800.0)
				{
					if (hM6DefendedWallLine.VisualEventBar < 0)
					{
						hM6DefendedWallLine.VisualEventBar = ((z.LastEventBar >= 0) ? z.LastEventBar : PrimaryBarIndexSafe());
					}
					hM6DefendedWallLine.ExecutedQty += Math.Max(1L, z.LastEventQty);
					if (hM6DefendedWallLine.AttackThresholdAtEvent <= 0)
					{
						hM6DefendedWallLine.AttackThresholdAtEvent = num2;
					}
					hM6DefendedWallLine.LastUpdateTime = now;
					return;
				}
			}
			HM6DefendedWallLine hM6DefendedWallLine2 = new HM6DefendedWallLine();
			int num7 = ((z.LastEventBar >= 0) ? z.LastEventBar : PrimaryBarIndexSafe());
			hM6DefendedWallLine2.Price = AnchorEventPriceToBarLocked(z.Price, num7);
			hM6DefendedWallLine2.WasBid = z.WasBid;
			hM6DefendedWallLine2.StartBar = num7;
			hM6DefendedWallLine2.VisualEventBar = num7;
			hM6DefendedWallLine2.BreakBar = -1;
			hM6DefendedWallLine2.StartSeq = Math.Max(1L, (z.LastEventSeq > 0) ? z.LastEventSeq : (_liveHeatmapSeq + 1));
			hM6DefendedWallLine2.EventTime = now;
			hM6DefendedWallLine2.AttackThresholdAtEvent = num2;
			hM6DefendedWallLine2.LastUpdateTime = now;
			hM6DefendedWallLine2.ExecutedQty = Math.Max(1L, z.LastEventQty);
			hM6DefendedWallLine2.RefillQty = Math.Max(0L, z.RefillQty);
			hM6DefendedWallLine2.AttackDelta = (z.WasBid ? (-Math.Max(1L, z.LastEventQty)) : Math.Max(1L, z.LastEventQty));
			hM6DefendedWallLine2.ResponseDelta = 0L;
			hM6DefendedWallLine2.ResponseOppositeDelta = 0L;
			hM6DefendedWallLine2.DefenseScore = 0.0;
			hM6DefendedWallLine2.DeltaConfirmed = false;
			hM6DefendedWallLine2.PriceConfirmed = false;
			hM6DefendedWallLine2.RefillConfirmed = false;
			hM6DefendedWallLine2.StrongAbsorption = false;
			hM6DefendedWallLine2.BuiltFromExecutedCollapse = false;
			hM6DefendedWallLine2.DeltaRequired = false;
			hM6DefendedWallLine2.Confirmed = false;
			hM6DefendedWallLine2.Active = false;
			hM6DefendedWallLine2.Broken = false;
			_defendedWallLines.Add(hM6DefendedWallLine2);
			TrimDefendedWallLinesLocked();
		}

		private void RegisterWallAttackFromAggressiveTradeLocked(double tradePrice, int tapeSide, long vol, double now)
		{
			if (!_pPassive || !_pIltDevZones || _passiveZones == null || _passiveZones.Count == 0)
			{
				return;
			}
			long num = CurrentIltDevZoneAttackMinQty(now);
			if (tradePrice <= 0.0 || vol < num || tapeSide == 0)
			{
				return;
			}
			RecordIltDevZoneAggSampleLocked(vol, now);
			bool flag = tapeSide < 0;
			bool flag2 = tapeSide > 0;
			bool flag3 = flag;
			if (!flag && !flag2)
			{
				return;
			}
			long num2 = Math.Max(1L, _pWallMinQty);
			long num3 = num;
			double num4 = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25);
			double num5 = num4 * 2.25;
			int num6 = PrimaryBarIndexSafe();
			HM6PassiveZone hM6PassiveZone = null;
			double num7 = double.MaxValue;
			foreach (KeyValuePair<string, HM6PassiveZone> passiveZone in _passiveZones)
			{
				HM6PassiveZone value = passiveZone.Value;
				if (value == null || value.WasBid != flag3 || value.Price <= 0.0)
				{
					continue;
				}
				double num8 = Math.Abs(value.Price - tradePrice);
				if (!(num8 > num5) && !(num8 >= num7))
				{
					long num9 = Math.Max(1L, IltDevZoneInstitutionalMinQty() / 2);
					bool flag4 = value.MaxQty >= num9 || Math.Max(value.ConsumeQty, Math.Max(value.RefillQty, value.LastEventQty)) >= num9;
					bool flag5 = value.Active || value.DerivedWall || value.LastQty >= num2 || value.MaxQty >= Math.Max(num2 * 2, num3) || value.AddQty >= num3 || value.EventCount >= 2 || value.RefillQty >= Math.Max(1L, num3 / 2) || value.PresenceCount >= 3;
					if (flag4 && flag5)
					{
						hM6PassiveZone = value;
						num7 = num8;
					}
				}
			}
			if (hM6PassiveZone == null)
			{
				return;
			}
			hM6PassiveZone.Reached = true;
			hM6PassiveZone.ReachedTime = now;
			hM6PassiveZone.TouchCount++;
			hM6PassiveZone.LastBar = num6;
			hM6PassiveZone.LastUpdateTime = now;
			if (hM6PassiveZone.LastEventBar < 0)
			{
				hM6PassiveZone.LastEventBar = num6;
			}
			hM6PassiveZone.ConsumeQty = Math.Max(hM6PassiveZone.ConsumeQty, vol);
			hM6PassiveZone.LastEventQty = Math.Max(hM6PassiveZone.LastEventQty, vol);
			hM6PassiveZone.LastEventType = 1;
			hM6PassiveZone.LastEventTime = now;
			hM6PassiveZone.LastEventSeq = Math.Max(1L, _liveHeatmapSeq + 1);
			if (_defendedWallLines == null)
			{
				_defendedWallLines = new List<HM6DefendedWallLine>();
			}
			double num10 = Math.Max(num4 * 0.6, 1E-10);
			double price = ((Math.Abs(hM6PassiveZone.Price - tradePrice) <= num4 * 1.05) ? hM6PassiveZone.Price : tradePrice);
			price = AnchorEventPriceToBarLocked(price, num6);
			HM6DefendedWallLine hM6DefendedWallLine = null;
			for (int num11 = _defendedWallLines.Count - 1; num11 >= 0; num11--)
			{
				HM6DefendedWallLine hM6DefendedWallLine2 = _defendedWallLines[num11];
				if (hM6DefendedWallLine2 == null)
				{
					_defendedWallLines.RemoveAt(num11);
				}
				else if (hM6DefendedWallLine2.WasBid == hM6PassiveZone.WasBid && Math.Abs(hM6DefendedWallLine2.Price - price) <= num10 && !hM6DefendedWallLine2.Broken && (now - hM6DefendedWallLine2.EventTime <= 30000.0 || hM6DefendedWallLine2.Confirmed))
				{
					hM6DefendedWallLine = hM6DefendedWallLine2;
					break;
				}
			}
			if (hM6DefendedWallLine == null)
			{
				hM6DefendedWallLine = new HM6DefendedWallLine();
				hM6DefendedWallLine.Price = price;
				hM6DefendedWallLine.WasBid = hM6PassiveZone.WasBid;
				hM6DefendedWallLine.StartBar = num6;
				hM6DefendedWallLine.VisualEventBar = num6;
				hM6DefendedWallLine.BreakBar = -1;
				hM6DefendedWallLine.EndBar = -1;
				hM6DefendedWallLine.StartSeq = Math.Max(1L, _liveHeatmapSeq + 1);
				hM6DefendedWallLine.EventTime = now;
				hM6DefendedWallLine.AttackThresholdAtEvent = CurrentIltDevZoneAttackMinQty(now);
				hM6DefendedWallLine.ConfirmTime = 0.0;
				hM6DefendedWallLine.Active = false;
				hM6DefendedWallLine.Confirmed = false;
				hM6DefendedWallLine.Broken = false;
				hM6DefendedWallLine.BuiltFromExecutedCollapse = false;
				hM6DefendedWallLine.DeltaRequired = false;
				_defendedWallLines.Add(hM6DefendedWallLine);
			}
			if (hM6DefendedWallLine.VisualEventBar < 0)
			{
				hM6DefendedWallLine.VisualEventBar = num6;
			}
			if (hM6DefendedWallLine.AttackThresholdAtEvent <= 0)
			{
				hM6DefendedWallLine.AttackThresholdAtEvent = num3;
			}
			if (hM6DefendedWallLine.Confirmed && !hM6DefendedWallLine.Broken && hM6DefendedWallLine.Active && hM6DefendedWallLine.ConfirmTime > 0.0 && now - hM6DefendedWallLine.ConfirmTime > 1250.0)
			{
				hM6DefendedWallLine.RetestQty += Math.Max(1L, vol);
				hM6DefendedWallLine.RetestTime = now;
				hM6DefendedWallLine.BreakQty = Math.Max(hM6DefendedWallLine.BreakQty, hM6DefendedWallLine.RetestQty);
				hM6DefendedWallLine.LastUpdateTime = now;
			}
			else
			{
				hM6DefendedWallLine.LastUpdateTime = now;
				hM6DefendedWallLine.ExecutedQty = Math.Max(hM6DefendedWallLine.ExecutedQty, vol);
				hM6DefendedWallLine.AttackDelta += (hM6PassiveZone.WasBid ? (-vol) : vol);
				hM6DefendedWallLine.RefillQty = Math.Max(hM6DefendedWallLine.RefillQty, hM6PassiveZone.RefillQty);
				hM6DefendedWallLine.DefenseScore = Math.Max(hM6DefendedWallLine.DefenseScore, 52.0);
				TrimDefendedWallLinesLocked();
			}
		}

		private bool IsProtectedActiveIltDevZone(HM6DefendedWallLine d)
		{
			if (d != null && d.Confirmed && !d.Discard && !d.Broken && !d.Faded)
			{
				return !d.Retested;
			}
			return false;
		}

		private int RemoveOldestDefendedWallLinesLocked(int removeNeeded, Func<HM6DefendedWallLine, bool> canRemove)
		{
			if (_defendedWallLines == null || removeNeeded <= 0 || canRemove == null)
			{
				return 0;
			}
			int num = 0;
			int num2 = 0;
			while (num2 < _defendedWallLines.Count && num < removeNeeded)
			{
				HM6DefendedWallLine arg = _defendedWallLines[num2];
				if (canRemove(arg))
				{
					_defendedWallLines.RemoveAt(num2);
					num++;
				}
				else
				{
					num2++;
				}
			}
			return num;
		}

		private void TrimDefendedWallLinesLocked()
		{
			if (_defendedWallLines == null || _defendedWallLines.Count == 0)
			{
				return;
			}
			_defendedWallLines.RemoveAll((HM6DefendedWallLine d) => d == null || d.Discard);
			if (_defendedWallLines.Count <= 600)
			{
				return;
			}
			int num = _defendedWallLines.Count - 500;
			num -= RemoveOldestDefendedWallLinesLocked(num, (HM6DefendedWallLine d) => d != null && !d.Confirmed && !d.Broken && !d.Faded);
			if (num > 0)
			{
				int num2 = num - RemoveOldestDefendedWallLinesLocked(num, (HM6DefendedWallLine d) => d != null && (d.Broken || d.Faded || d.Retested));
			}
			if (_defendedWallLines.Count > 2400)
			{
				int removeNeeded = _defendedWallLines.Count - 2400;
				RemoveOldestDefendedWallLinesLocked(removeNeeded, (HM6DefendedWallLine d) => !IsProtectedActiveIltDevZone(d));
			}
		}

		private void RegisterDefenseResponseDeltaLocked(double tradePrice, int tapeSide, long vol, double now)
		{
			if (!_pPassive || !_pIltDevZones || _defendedWallLines == null || _defendedWallLines.Count == 0 || vol <= 0 || tapeSide == 0 || tradePrice <= 0.0)
			{
				return;
			}
			double num = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25);
			double num2 = num * 0.5;
			double num3 = 15000.0;
			for (int num4 = _defendedWallLines.Count - 1; num4 >= 0; num4--)
			{
				HM6DefendedWallLine hM6DefendedWallLine = _defendedWallLines[num4];
				if (hM6DefendedWallLine != null && !hM6DefendedWallLine.Confirmed && !hM6DefendedWallLine.Broken && !(now <= hM6DefendedWallLine.EventTime) && !(now - hM6DefendedWallLine.EventTime > num3))
				{
					bool flag = hM6DefendedWallLine.WasBid && tradePrice >= hM6DefendedWallLine.Price + num2;
					bool flag2 = !hM6DefendedWallLine.WasBid && tradePrice <= hM6DefendedWallLine.Price - num2;
					bool flag3 = Math.Abs(tradePrice - hM6DefendedWallLine.Price) <= num * 2.0;
					bool flag4 = (hM6DefendedWallLine.WasBid ? (tapeSide > 0) : (tapeSide < 0));
					bool flag5 = (hM6DefendedWallLine.WasBid ? (tapeSide < 0) : (tapeSide > 0));
					if (flag4 && (flag || flag2 || flag3))
					{
						hM6DefendedWallLine.ResponseDelta += vol;
					}
					else if (flag5 && flag3)
					{
						hM6DefendedWallLine.ResponseOppositeDelta += vol;
					}
				}
			}
		}

		private bool HasIltDevSupportConfluenceLocked(double price, bool wasBid)
		{
			if (_iltDevLevels == null || _iltDevLevels.Count == 0)
			{
				return false;
			}
			double num = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25);
			double num2 = num * (double)Math.Max(Math.Max(1, _pIltDevZoneBandTicks), Math.Max(1, _pSRBandTicks));
			for (int i = 0; i < _iltDevLevels.Count; i++)
			{
				HM6IltDevSupportLevel hM6IltDevSupportLevel = _iltDevLevels[i];
				if (hM6IltDevSupportLevel != null && !hM6IltDevSupportLevel.Broken && hM6IltDevSupportLevel.WasBid == wasBid && hM6IltDevSupportLevel.StrengthScore >= Math.Max(0, _pIltDevSRMinStrength) && Math.Abs(hM6IltDevSupportLevel.Price - price) <= num2 + 1E-09)
				{
					return true;
				}
			}
			return false;
		}

		private void TallyZoneOutcomeLocked(HM6DefendedWallLine d, bool proven)
		{
			if (d != null && !d.OutcomeTallied && d.Confirmed)
			{
				d.OutcomeTallied = true;
				int num = (d.StrengthScore - 60) / 10;
				if (num < 0)
				{
					num = 0;
				}
				else if (num > 3)
				{
					num = 3;
				}
				if (proven)
				{
					_zoneTallyProven[num]++;
				}
				else
				{
					_zoneTallyBroken[num]++;
				}
			}
		}

		private int ComputeIltDevZoneStrengthLocked(HM6DefendedWallLine d, int closesNeeded)
		{
			if (d == null)
			{
				return 0;
			}
			long num = Math.Max(1L, (d.AttackThresholdAtEvent > 0) ? d.AttackThresholdAtEvent : IltDevZoneAttackMinQty());
			long num2 = Math.Max(d.ExecutedQty, Math.Abs(d.AttackDelta));
			double num3 = (double)num2 / (double)num;
			double num4 = 15.0 * Math.Min(1.0, num3) + 5.0 * Math.Min(1.0, Math.Max(0.0, num3 - 1.0));
			double num5 = Math.Max(0.0, d.MaxAdverseTicks);
			double num6 = ((num5 <= 2.0) ? 20.0 : ((!(num5 <= 5.0)) ? Math.Max(0.0, 12.0 - (num5 - 5.0) * 2.4) : (20.0 - (num5 - 2.0) * 2.6666666666666665)));
			double num7 = 15.0 * Math.Min(1.0, Math.Max(0.0, d.MaxFavorableTicks) / 8.0);
			double num8 = ((d.FavorableCloses > 0) ? (d.FavorableCloseMarginSumTicks / (double)Math.Max(1, d.FavorableCloses)) : 0.0);
			double num9 = 10.0 + 5.0 * Math.Min(1.0, num8 / 4.0);
			double num10 = 0.0;
			if (d.DeltaConfirmed)
			{
				num10 = 10.0;
			}
			else if (d.ResponseDelta > 0)
			{
				num10 = 8.0 * Math.Min(1.0, (double)d.ResponseDelta / (double)Math.Max(1L, num));
			}
			double num11 = 0.0;
			if (d.RefillConfirmed)
			{
				num11 += 6.0;
			}
			if (d.WallPersistentConfirmed)
			{
				num11 += 6.0;
			}
			num11 = Math.Min(10.0, num11);
			int num12 = 0;
			if (num3 >= 1.0)
			{
				num12++;
			}
			if (num5 <= 2.0)
			{
				num12++;
			}
			if (d.MaxFavorableTicks >= 3.0)
			{
				num12++;
			}
			if (d.DeltaConfirmed || d.ResponseDelta > 0)
			{
				num12++;
			}
			if (d.RefillConfirmed || d.WallPersistentConfirmed)
			{
				num12++;
			}
			if (d.SupportConfluence)
			{
				num12++;
			}
			double num13 = ((num12 >= 2) ? Math.Min(22.0, 5.5 * Math.Pow(num12 - 1, 1.2)) : 0.0);
			double num14 = 0.0;
			if (d.PreConfirmBar >= 0 && d.LastCountedBar - d.PreConfirmBar > Math.Max(3, closesNeeded + 2))
			{
				num14 += 5.0;
			}
			if (num5 > 5.0)
			{
				num14 += Math.Min(10.0, (num5 - 5.0) * 2.0);
			}
			double a = num4 + num6 + num7 + num9 + num10 + num11 + num13 - num14;
			return Math.Max(0, Math.Min(100, (int)Math.Round(a)));
		}

		private void ReinforceIltDevZoneFromSupportLocked(double price, bool wasBid, int supportStrength, double now)
		{
			if (_defendedWallLines == null || _defendedWallLines.Count == 0)
			{
				return;
			}
			double num = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25);
			double num2 = num * (double)Math.Max(Math.Max(1, _pIltDevZoneBandTicks), Math.Max(1, _pSRBandTicks));
			for (int i = 0; i < _defendedWallLines.Count; i++)
			{
				HM6DefendedWallLine hM6DefendedWallLine = _defendedWallLines[i];
				if (hM6DefendedWallLine != null && hM6DefendedWallLine.Confirmed && hM6DefendedWallLine.Active && !hM6DefendedWallLine.Broken && !hM6DefendedWallLine.Discard && hM6DefendedWallLine.WasBid == wasBid && !(Math.Abs(hM6DefendedWallLine.Price - price) > num2 + 1E-09))
				{
					hM6DefendedWallLine.SupportConfluence = true;
					hM6DefendedWallLine.SecondaryConfirmed = true;
					int num3 = Math.Max(3, Math.Min(10, supportStrength / 10));
					hM6DefendedWallLine.StrengthScore = Math.Min(100, Math.Max(hM6DefendedWallLine.StrengthScore, _pIltDevZoneMinStrength) + num3);
					hM6DefendedWallLine.LastUpdateTime = now;
				}
			}
		}

		private bool TryMergeConfirmedIltDevZoneLocked(HM6DefendedWallLine incoming, double now)
		{
			if (incoming == null || _defendedWallLines == null)
			{
				return false;
			}
			double num = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25);
			double num2 = num * (double)Math.Max(1, _pIltDevZoneMergeTicks);
			double num3 = (double)Math.Max(10, _pIltDevZoneMergeSec) * 1000.0;
			HM6DefendedWallLine hM6DefendedWallLine = null;
			double num4 = double.MaxValue;
			for (int i = 0; i < _defendedWallLines.Count; i++)
			{
				HM6DefendedWallLine hM6DefendedWallLine2 = _defendedWallLines[i];
				if (hM6DefendedWallLine2 == null || hM6DefendedWallLine2 == incoming || !hM6DefendedWallLine2.Confirmed || hM6DefendedWallLine2.Broken || hM6DefendedWallLine2.Discard || hM6DefendedWallLine2.Faded || !hM6DefendedWallLine2.Active || hM6DefendedWallLine2.WasBid != incoming.WasBid)
				{
					continue;
				}
				double num5 = Math.Abs(hM6DefendedWallLine2.Price - incoming.Price);
				if (!(num5 > num2) && !(num5 >= num4))
				{
					bool flag = Math.Abs(hM6DefendedWallLine2.EventTime - incoming.EventTime) <= num3;
					bool flag2 = !hM6DefendedWallLine2.Retested && hM6DefendedWallLine2.EndBar < 0;
					if (flag || flag2)
					{
						hM6DefendedWallLine = hM6DefendedWallLine2;
						num4 = num5;
					}
				}
			}
			if (hM6DefendedWallLine == null)
			{
				return false;
			}
			HM6DefendedWallLine hM6DefendedWallLine3 = ((hM6DefendedWallLine.EventTime <= incoming.EventTime) ? hM6DefendedWallLine : incoming);
			HM6DefendedWallLine hM6DefendedWallLine4 = ((hM6DefendedWallLine3 == hM6DefendedWallLine) ? incoming : hM6DefendedWallLine);
			hM6DefendedWallLine3.ReinforceCount++;
			int num6 = Math.Min(8, 2 + hM6DefendedWallLine3.ReinforceCount * 2);
			hM6DefendedWallLine3.StrengthScore = Math.Min(100, Math.Max(hM6DefendedWallLine3.StrengthScore, hM6DefendedWallLine4.StrengthScore) + num6);
			hM6DefendedWallLine3.DefenseScore = Math.Max(hM6DefendedWallLine3.DefenseScore, hM6DefendedWallLine4.DefenseScore);
			hM6DefendedWallLine3.ExecutedQty = Math.Max(hM6DefendedWallLine3.ExecutedQty, hM6DefendedWallLine4.ExecutedQty);
			hM6DefendedWallLine3.RefillQty = Math.Max(hM6DefendedWallLine3.RefillQty, hM6DefendedWallLine4.RefillQty);
			if (Math.Abs(hM6DefendedWallLine4.AttackDelta) > Math.Abs(hM6DefendedWallLine3.AttackDelta))
			{
				hM6DefendedWallLine3.AttackDelta = hM6DefendedWallLine4.AttackDelta;
			}
			hM6DefendedWallLine3.ResponseDelta = Math.Max(hM6DefendedWallLine3.ResponseDelta, hM6DefendedWallLine4.ResponseDelta);
			hM6DefendedWallLine3.ResponseOppositeDelta = Math.Min(hM6DefendedWallLine3.ResponseOppositeDelta, hM6DefendedWallLine4.ResponseOppositeDelta);
			hM6DefendedWallLine3.FrozenQty = Math.Max(hM6DefendedWallLine3.FrozenQty, hM6DefendedWallLine4.FrozenQty);
			hM6DefendedWallLine3.QtyFrozen = hM6DefendedWallLine3.QtyFrozen || hM6DefendedWallLine4.QtyFrozen;
			hM6DefendedWallLine3.SecondaryConfirmed = hM6DefendedWallLine3.SecondaryConfirmed || hM6DefendedWallLine4.SecondaryConfirmed;
			hM6DefendedWallLine3.SupportConfluence = hM6DefendedWallLine3.SupportConfluence || hM6DefendedWallLine4.SupportConfluence;
			hM6DefendedWallLine3.WallPersistentConfirmed = hM6DefendedWallLine3.WallPersistentConfirmed || hM6DefendedWallLine4.WallPersistentConfirmed;
			hM6DefendedWallLine3.DeltaConfirmed = hM6DefendedWallLine3.DeltaConfirmed || hM6DefendedWallLine4.DeltaConfirmed;
			hM6DefendedWallLine3.RefillConfirmed = hM6DefendedWallLine3.RefillConfirmed || hM6DefendedWallLine4.RefillConfirmed;
			hM6DefendedWallLine3.LastUpdateTime = now;
			hM6DefendedWallLine4.Active = false;
			hM6DefendedWallLine4.Discard = true;
			return true;
		}

		private void UpdateDefendedWallLinesLocked(double low, double high, double close, double now)
		{
			if (!_pPassive || !_pIltDevZones || _defendedWallLines == null || _defendedWallLines.Count == 0 || double.IsNaN(low) || double.IsNaN(high) || double.IsInfinity(low) || double.IsInfinity(high))
			{
				return;
			}
			if (high < low)
			{
				double num = high;
				high = low;
				low = num;
			}
			int num2 = PrimaryBarIndexSafe();
			int num3 = Math.Max(20, Math.Min(2000, _pIltDevZoneCandidateMaxBars));
			double num4 = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25);
			double num5 = Math.Max(1.0, _pIltDevZoneBandTicks);
			double num6 = 6.0;
			double num7 = 8.0;
			double num8 = 5.0;
			double num9 = 8.0;
			double num10 = 30000.0;
			double num11 = 4.0;
			for (int num12 = _defendedWallLines.Count - 1; num12 >= 0; num12--)
			{
				HM6DefendedWallLine hM6DefendedWallLine = _defendedWallLines[num12];
				if (hM6DefendedWallLine == null || hM6DefendedWallLine.Discard)
				{
					_defendedWallLines.RemoveAt(num12);
					continue;
				}
				bool flag = hM6DefendedWallLine.Broken || hM6DefendedWallLine.Retested || hM6DefendedWallLine.RetestQty > 0;
				if (!hM6DefendedWallLine.Confirmed || flag)
				{
					bool num13;
					if (hM6DefendedWallLine.Confirmed || hM6DefendedWallLine.Broken)
					{
						num13 = num2 - hM6DefendedWallLine.StartBar > Math.Max(100, Math.Min(10000, _pIltDevZoneHistoryBars));
					}
					else
					{
						if (num2 - hM6DefendedWallLine.StartBar > num3)
						{
							goto IL_0201;
						}
						num13 = now - hM6DefendedWallLine.EventTime > Math.Max(90000.0, (double)num3 * 1500.0);
					}
					if (num13)
					{
						goto IL_0201;
					}
				}
				HM6PassiveZone value = null;
				if (_passiveZones != null && _passiveZones.TryGetValue(PassiveZoneKey(hM6DefendedWallLine.Price, hM6DefendedWallLine.WasBid), out value) && value != null)
				{
					if (value.RefillQty > hM6DefendedWallLine.RefillQty)
					{
						hM6DefendedWallLine.RefillQty = value.RefillQty;
					}
					if (value.ConsumeQty > hM6DefendedWallLine.ExecutedQty)
					{
						hM6DefendedWallLine.ExecutedQty = value.ConsumeQty;
					}
				}
				if (!hM6DefendedWallLine.Confirmed && !hM6DefendedWallLine.Broken)
				{
					double val = (hM6DefendedWallLine.WasBid ? Math.Max(0.0, (hM6DefendedWallLine.Price - low) / num4) : Math.Max(0.0, (high - hM6DefendedWallLine.Price) / num4));
					double val2 = (hM6DefendedWallLine.WasBid ? Math.Max(0.0, (high - hM6DefendedWallLine.Price) / num4) : Math.Max(0.0, (hM6DefendedWallLine.Price - low) / num4));
					hM6DefendedWallLine.MaxAdverseTicks = Math.Max(hM6DefendedWallLine.MaxAdverseTicks, val);
					hM6DefendedWallLine.MaxFavorableTicks = Math.Max(hM6DefendedWallLine.MaxFavorableTicks, val2);
					double num14 = ((close > 0.0 && !double.IsNaN(close) && !double.IsInfinity(close)) ? close : ((high > 0.0 && low > 0.0) ? ((high + low) * 0.5) : hM6DefendedWallLine.Price));
					bool flag2 = (hM6DefendedWallLine.WasBid ? (num14 >= hM6DefendedWallLine.Price + num4 * num6 || hM6DefendedWallLine.MaxFavorableTicks >= num6) : (num14 <= hM6DefendedWallLine.Price - num4 * num6 || hM6DefendedWallLine.MaxFavorableTicks >= num6));
					bool flag3 = (hM6DefendedWallLine.WasBid ? (num14 >= hM6DefendedWallLine.Price + num4 * num7 || hM6DefendedWallLine.MaxFavorableTicks >= num7) : (num14 <= hM6DefendedWallLine.Price - num4 * num7 || hM6DefendedWallLine.MaxFavorableTicks >= num7));
					bool flag4 = (hM6DefendedWallLine.WasBid ? (num14 <= hM6DefendedWallLine.Price - num4 * num9 && hM6DefendedWallLine.MaxFavorableTicks < num5 + 1.0) : (num14 >= hM6DefendedWallLine.Price + num4 * num9 && hM6DefendedWallLine.MaxFavorableTicks < num5 + 1.0));
					long num15 = Math.Max(1L, (hM6DefendedWallLine.AttackThresholdAtEvent > 0) ? hM6DefendedWallLine.AttackThresholdAtEvent : CurrentIltDevZoneAttackMinQty(now));
					if (hM6DefendedWallLine.AttackThresholdAtEvent <= 0)
					{
						hM6DefendedWallLine.AttackThresholdAtEvent = num15;
					}
					long num16 = Math.Max(1L, Math.Max(_pPassiveEventMin, num15));
					long num17 = Math.Abs(hM6DefendedWallLine.AttackDelta);
					bool flag5 = hM6DefendedWallLine.ExecutedQty >= num15 || num17 >= num15;
					long executedQty = hM6DefendedWallLine.ExecutedQty;
					long num18 = Math.Max(num15, IltDevZoneInstitutionalMinQtyFor(num15) / 2);
					bool flag6 = Math.Max(hM6DefendedWallLine.ExecutedQty, Math.Max(hM6DefendedWallLine.RefillQty, num17)) >= num18;
					bool flag7 = hM6DefendedWallLine.MaxAdverseTicks <= num8;
					bool flag8 = flag5 && flag7;
					bool flag9 = hM6DefendedWallLine.RefillQty >= num16 && (flag5 || hM6DefendedWallLine.BuiltFromExecutedCollapse || hM6DefendedWallLine.PriceConfirmed);
					bool flag10 = hM6DefendedWallLine.ResponseDelta >= Math.Max(1L, Math.Min(Math.Max(num15, Math.Max(1L, hM6DefendedWallLine.ExecutedQty) / 2), Math.Max(num15 * 3, Math.Max(1L, hM6DefendedWallLine.ExecutedQty))));
					bool flag11 = hM6DefendedWallLine.ResponseDelta >= Math.Max(1L, hM6DefendedWallLine.ResponseOppositeDelta);
					bool flag12 = value != null && value.PresenceCount >= 4 && value.FirstTime > 0.0 && now - value.FirstTime >= 900.0 && value.LastUpdateTime > 0.0 && now - value.LastUpdateTime <= 2800.0;
					bool flag13 = HasIltDevSupportConfluenceLocked(hM6DefendedWallLine.Price, hM6DefendedWallLine.WasBid);
					bool flag14 = hM6DefendedWallLine.ExecutedQty >= num15 * 2 || num17 >= num15 * 2;
					bool flag15 = flag3 || flag9 || (flag10 && flag11) || flag14 || flag12 || flag13;
					double num19 = 0.0;
					if (flag5)
					{
						num19 += 25.0;
					}
					if (flag8)
					{
						num19 += 20.0;
					}
					if (flag2)
					{
						num19 += 20.0;
					}
					if (flag3)
					{
						num19 += 15.0;
					}
					if (flag10 && flag11)
					{
						num19 += 10.0;
					}
					if (flag9)
					{
						num19 += 10.0;
					}
					if (flag14)
					{
						num19 += 5.0;
					}
					if (hM6DefendedWallLine.MaxAdverseTicks > num9)
					{
						num19 -= 18.0;
					}
					if (flag4)
					{
						num19 -= 55.0;
					}
					bool flag16 = flag6 && flag5 && flag2 && flag7 && flag15 && num19 >= 60.0;
					bool flag17 = !flag4 && flag16;
					bool flag18 = flag4 && flag5;
					hM6DefendedWallLine.DefenseScore = num19;
					hM6DefendedWallLine.PriceConfirmed = flag2 && flag3;
					hM6DefendedWallLine.DeltaConfirmed = flag10 && flag11;
					hM6DefendedWallLine.RefillConfirmed = flag9;
					hM6DefendedWallLine.SecondaryConfirmed = flag15;
					hM6DefendedWallLine.SupportConfluence = flag13;
					hM6DefendedWallLine.WallPersistentConfirmed = flag12;
					hM6DefendedWallLine.StrongAbsorption = flag8 && flag2 && !flag4;
					if (flag17)
					{
						if (!hM6DefendedWallLine.PreConfirmed)
						{
							hM6DefendedWallLine.PreConfirmed = true;
							hM6DefendedWallLine.PreConfirmTime = now;
							hM6DefendedWallLine.PreConfirmBar = num2;
							hM6DefendedWallLine.FavorableCloses = 0;
						}
						hM6DefendedWallLine.LastUpdateTime = now;
					}
					else if (flag18)
					{
						hM6DefendedWallLine.Active = false;
						hM6DefendedWallLine.Confirmed = false;
						hM6DefendedWallLine.Broken = true;
						hM6DefendedWallLine.FailedBreak = true;
						hM6DefendedWallLine.BreakBar = num2;
						hM6DefendedWallLine.EndBar = num2;
						hM6DefendedWallLine.BreakSeq = Math.Max(1L, _liveHeatmapSeq);
						hM6DefendedWallLine.BreakTime = now;
						hM6DefendedWallLine.BreakQty = Math.Max(hM6DefendedWallLine.BreakQty, Math.Max(hM6DefendedWallLine.ExecutedQty, num17));
						hM6DefendedWallLine.LastUpdateTime = now;
					}
					else if (!hM6DefendedWallLine.PreConfirmed && now - hM6DefendedWallLine.EventTime > num10)
					{
						_defendedWallLines.RemoveAt(num12);
					}
				}
				else
				{
					if (!hM6DefendedWallLine.Confirmed || hM6DefendedWallLine.Broken || !hM6DefendedWallLine.Active)
					{
						continue;
					}
					double num20 = num4 * num5;
					double num21 = num4 * num11;
					if (now - hM6DefendedWallLine.ConfirmTime < 2500.0 || num2 <= hM6DefendedWallLine.StartBar)
					{
						continue;
					}
					bool flag19 = (hM6DefendedWallLine.WasBid ? (low <= hM6DefendedWallLine.Price + num20 && hM6DefendedWallLine.MaxFavorableTicks >= num6 * 0.75) : (high >= hM6DefendedWallLine.Price - num20 && hM6DefendedWallLine.MaxFavorableTicks >= num6 * 0.75));
					double num22 = ((close > 0.0 && !double.IsNaN(close) && !double.IsInfinity(close)) ? close : ((high > 0.0 && low > 0.0) ? ((high + low) * 0.5) : hM6DefendedWallLine.Price));
					bool flag20 = (hM6DefendedWallLine.WasBid ? (num22 <= hM6DefendedWallLine.Price - num21) : (num22 >= hM6DefendedWallLine.Price + num21));
					bool flag21 = (hM6DefendedWallLine.WasBid ? (num22 <= hM6DefendedWallLine.Price - num21 * 1.75) : (num22 >= hM6DefendedWallLine.Price + num21 * 1.75));
					long num23 = Math.Max(1L, (hM6DefendedWallLine.AttackThresholdAtEvent > 0) ? hM6DefendedWallLine.AttackThresholdAtEvent : IltDevZoneAttackMinQty());
					bool flag22 = hM6DefendedWallLine.RetestQty >= num23 || (hM6DefendedWallLine.RetestTime > 0.0 && now - hM6DefendedWallLine.RetestTime <= 2500.0 && hM6DefendedWallLine.BreakQty >= num23);
					if (!(flag19 || flag20))
					{
						continue;
					}
					hM6DefendedWallLine.Retested = true;
					hM6DefendedWallLine.EndBar = num2;
					hM6DefendedWallLine.EndTime = now;
					hM6DefendedWallLine.LastUpdateTime = now;
					if (flag20 && flag21 && flag22)
					{
						TallyZoneOutcomeLocked(hM6DefendedWallLine, false);
						hM6DefendedWallLine.Active = false;
						hM6DefendedWallLine.Broken = true;
						hM6DefendedWallLine.FailedBreak = false;
						hM6DefendedWallLine.BreakBar = num2;
						hM6DefendedWallLine.BreakSeq = Math.Max(1L, _liveHeatmapSeq);
						hM6DefendedWallLine.BreakTime = now;
						hM6DefendedWallLine.BreakQty = Math.Max(hM6DefendedWallLine.BreakQty, Math.Max(hM6DefendedWallLine.RetestQty, Math.Max(hM6DefendedWallLine.ExecutedQty, Math.Abs(hM6DefendedWallLine.AttackDelta))));
					}
					else
					{
						hM6DefendedWallLine.Active = false;
						if (flag19)
						{
							TallyZoneOutcomeLocked(hM6DefendedWallLine, true);
							hM6DefendedWallLine.ProvenRetests++;
							double num24 = Math.Min(26.0, 14.0 + 6.0 * (double)(hM6DefendedWallLine.ProvenRetests - 1));
							hM6DefendedWallLine.StrengthScore = Math.Min(100, (int)Math.Round((double)Math.Max(hM6DefendedWallLine.StrengthScore, _pIltDevZoneMinStrength) + num24));
							hM6DefendedWallLine.LastUpdateTime = now;
						}
					}
				}
				continue;
				IL_0201:
				_defendedWallLines.RemoveAt(num12);
			}
		}

		private void EvaluateDefendedWallClosesLocked(double closedClose, double closedHigh, double closedLow, int closedBar, double now)
		{
			if (!_pPassive || !_pIltDevZones || _defendedWallLines == null || _defendedWallLines.Count == 0 || closedClose <= 0.0 || double.IsNaN(closedClose) || double.IsInfinity(closedClose))
			{
				return;
			}
			double num = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25);
			double num2 = 1.0;
			double num3 = 2.0;
			int num4 = Math.Max(1, Math.Min(3, _pZoneCloses));
			int num5 = num4 + 6;
			for (int num6 = _defendedWallLines.Count - 1; num6 >= 0; num6--)
			{
				HM6DefendedWallLine hM6DefendedWallLine = _defendedWallLines[num6];
				if (hM6DefendedWallLine != null && !hM6DefendedWallLine.Discard && closedBar > hM6DefendedWallLine.LastCountedBar)
				{
					hM6DefendedWallLine.LastCountedBar = closedBar;
					long num7 = Math.Max(1L, (hM6DefendedWallLine.AttackThresholdAtEvent > 0) ? hM6DefendedWallLine.AttackThresholdAtEvent : IltDevZoneAttackMinQty());
					bool flag = (hM6DefendedWallLine.WasBid ? (closedClose >= hM6DefendedWallLine.Price + num * num2) : (closedClose <= hM6DefendedWallLine.Price - num * num2));
					bool flag2 = (hM6DefendedWallLine.WasBid ? (closedClose <= hM6DefendedWallLine.Price - num * num3) : (closedClose >= hM6DefendedWallLine.Price + num * num3));
					double num8 = num * Math.Max(8.0, (double)_pIltDevZoneBandTicks + 4.0);
					bool flag3 = (hM6DefendedWallLine.WasBid ? (closedLow <= hM6DefendedWallLine.Price + num8) : (closedHigh >= hM6DefendedWallLine.Price - num8));
					double num9 = num * Math.Max(16.0, (double)_pIltDevZoneBandTicks * 4.0);
					bool flag4 = Math.Abs(closedClose - hM6DefendedWallLine.Price) <= num9;
					bool flag5 = flag3 || flag4;
					if (hM6DefendedWallLine.Broken)
					{
						if (hM6DefendedWallLine.Faded)
						{
							if (hM6DefendedWallLine.FadeBar >= 0 && closedBar > hM6DefendedWallLine.FadeBar && flag2)
							{
								hM6DefendedWallLine.Faded = false;
								hM6DefendedWallLine.Discard = true;
								hM6DefendedWallLine.Active = false;
								hM6DefendedWallLine.EndBar = closedBar;
								hM6DefendedWallLine.EndTime = now;
								hM6DefendedWallLine.LastUpdateTime = now;
							}
						}
						else if (!hM6DefendedWallLine.Faded && _pFadeBars > 0 && hM6DefendedWallLine.BreakBar >= 0 && closedBar > hM6DefendedWallLine.BreakBar && closedBar - hM6DefendedWallLine.BreakBar <= _pFadeBars && flag)
						{
							hM6DefendedWallLine.Faded = true;
							hM6DefendedWallLine.FadeBar = closedBar;
							hM6DefendedWallLine.FadeTime = now;
							hM6DefendedWallLine.FadeQty = Math.Max(1L, (hM6DefendedWallLine.BreakQty > 0) ? hM6DefendedWallLine.BreakQty : (hM6DefendedWallLine.QtyFrozen ? hM6DefendedWallLine.FrozenQty : 1));
							hM6DefendedWallLine.LastUpdateTime = now;
						}
					}
					else if (hM6DefendedWallLine.Confirmed)
					{
						if (flag2 && closedBar > hM6DefendedWallLine.StartBar)
						{
							TallyZoneOutcomeLocked(hM6DefendedWallLine, false);
							hM6DefendedWallLine.Active = false;
							hM6DefendedWallLine.Broken = true;
							hM6DefendedWallLine.FailedBreak = false;
							hM6DefendedWallLine.BreakBar = closedBar;
							hM6DefendedWallLine.EndBar = closedBar;
							hM6DefendedWallLine.BreakSeq = Math.Max(1L, _liveHeatmapSeq);
							hM6DefendedWallLine.BreakTime = now;
							if (hM6DefendedWallLine.BreakQty <= 0)
							{
								hM6DefendedWallLine.BreakQty = Math.Max(1L, Math.Max(hM6DefendedWallLine.RetestQty, Math.Max(hM6DefendedWallLine.ExecutedQty, Math.Abs(hM6DefendedWallLine.AttackDelta))));
							}
							hM6DefendedWallLine.LastUpdateTime = now;
						}
					}
					else if (hM6DefendedWallLine.PreConfirmed)
					{
						if (flag2 && closedBar > hM6DefendedWallLine.PreConfirmBar)
						{
							if (hM6DefendedWallLine.ExecutedQty >= num7 || Math.Abs(hM6DefendedWallLine.AttackDelta) >= num7)
							{
								hM6DefendedWallLine.Active = false;
								hM6DefendedWallLine.Confirmed = false;
								hM6DefendedWallLine.Broken = true;
								hM6DefendedWallLine.FailedBreak = true;
								hM6DefendedWallLine.BreakBar = closedBar;
								hM6DefendedWallLine.EndBar = closedBar;
								hM6DefendedWallLine.BreakSeq = Math.Max(1L, _liveHeatmapSeq);
								hM6DefendedWallLine.BreakTime = now;
								hM6DefendedWallLine.BreakQty = Math.Max(hM6DefendedWallLine.BreakQty, Math.Max(hM6DefendedWallLine.ExecutedQty, Math.Abs(hM6DefendedWallLine.AttackDelta)));
								hM6DefendedWallLine.LastUpdateTime = now;
							}
							else
							{
								hM6DefendedWallLine.Discard = true;
							}
						}
						else if (flag && flag5)
						{
							double num10 = (hM6DefendedWallLine.WasBid ? Math.Max(0.0, (closedClose - hM6DefendedWallLine.Price) / num) : Math.Max(0.0, (hM6DefendedWallLine.Price - closedClose) / num));
							hM6DefendedWallLine.FavorableCloses++;
							hM6DefendedWallLine.FavorableCloseMarginSumTicks += num10;
							if (hM6DefendedWallLine.FavorableCloses >= num4)
							{
								hM6DefendedWallLine.Confirmed = true;
								hM6DefendedWallLine.PreConfirmed = false;
								hM6DefendedWallLine.Active = true;
								hM6DefendedWallLine.Broken = false;
								hM6DefendedWallLine.FailedBreak = false;
								hM6DefendedWallLine.Retested = false;
								hM6DefendedWallLine.EndBar = -1;
								hM6DefendedWallLine.EndTime = 0.0;
								hM6DefendedWallLine.ConfirmTime = now;
								hM6DefendedWallLine.LastUpdateTime = now;
								if (!hM6DefendedWallLine.QtyFrozen)
								{
									hM6DefendedWallLine.FrozenQty = Math.Max(1L, Math.Max(hM6DefendedWallLine.ExecutedQty, Math.Max(hM6DefendedWallLine.RefillQty, Math.Abs(hM6DefendedWallLine.AttackDelta))));
									hM6DefendedWallLine.QtyFrozen = true;
								}
								hM6DefendedWallLine.StrengthScore = ComputeIltDevZoneStrengthLocked(hM6DefendedWallLine, num4);
								if (hM6DefendedWallLine.StrengthScore < Math.Max(0, _pIltDevZoneMinStrength))
								{
									hM6DefendedWallLine.Active = false;
									hM6DefendedWallLine.Discard = true;
								}
								else
								{
									TryMergeConfirmedIltDevZoneLocked(hM6DefendedWallLine, now);
								}
							}
						}
						else
						{
							int num11 = ((hM6DefendedWallLine.PreConfirmBar >= 0) ? hM6DefendedWallLine.PreConfirmBar : hM6DefendedWallLine.StartBar);
							if (!hM6DefendedWallLine.Confirmed && closedBar - num11 > num5 + 2 * Math.Max(0, hM6DefendedWallLine.FavorableCloses))
							{
								hM6DefendedWallLine.Discard = true;
							}
						}
					}
				}
			}
		}

		private void CleanPassiveEventLabelsOnPriceInteractionLocked(double low, double high, double close, double now)
		{
			if (!_pPassive || !_pPassiveChartMarkers || !_pPassiveCleanLabelsOnTouch || double.IsNaN(low) || double.IsNaN(high) || double.IsInfinity(low) || double.IsInfinity(high))
			{
				return;
			}
			if (high < low)
			{
				double num = high;
				high = low;
				low = num;
			}
			int iltDevNowMs = IltDevNowMs;
			if (iltDevNowMs - _lastPassiveLabelCleanMs < 90)
			{
				return;
			}
			_lastPassiveLabelCleanMs = iltDevNowMs;
			double num2 = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25);
			double tol = num2 * (double)Math.Max(1, _pPassiveLabelTouchTicks);
			double minAge = Math.Max(250.0, _pPassiveLabelTouchMinAgeMs);
			int curBar = PrimaryBarIndexSafe();
			Func<double, bool> touched = (double price) => !(price <= 0.0) && !double.IsNaN(price) && !double.IsInfinity(price) && price >= low - tol && price <= high + tol;
			Action<HM6PassiveEvent> action = delegate(HM6PassiveEvent ev)
			{
				if (ev != null && !ev.PriceInteracted && ev.Type != 2 && (ev.Type == 0 || ev.Type == 1 || ev.Type == 3) && (!(ev.Time > 0.0) || !(now - ev.Time < minAge)) && (ev.BarIdx < 0 || ev.BarIdx != curBar || !(ev.Time > 0.0) || !(now - ev.Time < minAge * 1.75)) && touched(ev.Price))
				{
					ev.PriceInteracted = true;
					ev.PriceInteractionTime = now;
					ev.PriceInteractionBar = curBar;
					ev.Fade = 0f;
				}
			};
			if (_passiveEvents != null && _passiveEvents.Count > 0)
			{
				int num3 = 0;
				int num4 = _passiveEvents.Count - 1;
				while (num4 >= 0 && num3 < 1400)
				{
					action(_passiveEvents[num4]);
					num4--;
					num3++;
				}
			}
			if (_liveHeatmapSlices != null && _liveHeatmapSlices.Count > 0)
			{
				int num5 = Math.Min(_liveHeatmapSlices.Count, Math.Max(120, Math.Min(800, _pPassiveWallMaxBars * 2)));
				int num6 = Math.Max(0, _liveHeatmapSlices.Count - num5);
				for (int num7 = _liveHeatmapSlices.Count - 1; num7 >= num6; num7--)
				{
					HM6HeatmapSlice hM6HeatmapSlice = _liveHeatmapSlices[num7];
					if (hM6HeatmapSlice != null && hM6HeatmapSlice.Events != null && hM6HeatmapSlice.Events.Count != 0)
					{
						for (int num8 = hM6HeatmapSlice.Events.Count - 1; num8 >= 0; num8--)
						{
							action(hM6HeatmapSlice.Events[num8]);
						}
					}
				}
			}
			if (_passiveZones == null || _passiveZones.Count <= 0)
			{
				return;
			}
			foreach (KeyValuePair<string, HM6PassiveZone> passiveZone in _passiveZones)
			{
				HM6PassiveZone value = passiveZone.Value;
				if (value != null && !value.SpoofLabelInteracted && IsPassiveSpoofZoneRaw(value) && (!(value.LastEventTime > 0.0) || !(now - value.LastEventTime < minAge)) && (value.LastEventBar < 0 || value.LastEventBar != curBar || !(value.LastEventTime > 0.0) || !(now - value.LastEventTime < minAge * 1.75)) && touched(value.Price))
				{
					value.SpoofLabelInteracted = true;
					value.SpoofInteractionTime = now;
					value.SpoofInteractionBar = curBar;
				}
			}
		}

		private void TrimPassiveZonesLocked(double now)
		{
			if (_passiveZones == null || _passiveZones.Count <= 0)
			{
				return;
			}
			int num = PrimaryBarIndexSafe();
			int num2 = PassiveZoneRetentionBars();
			double num3 = Math.Max(60000.0, (double)num2 * 1500.0);
			List<string> list = new List<string>();
			foreach (KeyValuePair<string, HM6PassiveZone> passiveZone in _passiveZones)
			{
				HM6PassiveZone value = passiveZone.Value;
				if (value == null)
				{
					list.Add(passiveZone.Key);
					continue;
				}
				int num4 = ((value.StartBar >= 0) ? value.StartBar : num);
				bool flag = num - num4 > num2;
				bool flag2 = value.LastUpdateTime > 0.0 && now - value.LastUpdateTime > num3 * 3.0 && !value.Active && value.Reached;
				if (flag || flag2)
				{
					list.Add(passiveZone.Key);
				}
			}
			for (int i = 0; i < list.Count; i++)
			{
				_passiveZones.Remove(list[i]);
			}
			if (_passiveZones.Count > 3000)
			{
				List<string> list2 = _passiveZones.OrderBy((KeyValuePair<string, HM6PassiveZone> kv) => (kv.Value == null) ? 0.0 : kv.Value.LastUpdateTime).Take(_passiveZones.Count - 2600).Select(delegate(KeyValuePair<string, HM6PassiveZone> kv)
				{
					KeyValuePair<string, HM6PassiveZone> keyValuePair = kv;
					return keyValuePair.Key;
				})
					.ToList();
				for (int num5 = 0; num5 < list2.Count; num5++)
				{
					_passiveZones.Remove(list2[num5]);
				}
			}
		}

		private static double Clamp01(double v)
		{
			if (!(v < 0.0))
			{
				if (!(v > 1.0))
				{
					return v;
				}
				return 1.0;
			}
			return 0.0;
		}

		private HM6PassiveSummary BuildPassiveLiquiditySummary(double now)
		{
			HM6PassiveSummary hM6PassiveSummary = new HM6PassiveSummary();
			lock (_lk)
			{
				int count = Math.Max(1, Math.Min(20, _pPassiveDepth));
				long minQty = Math.Max(1L, _pPassiveMinQty);
				List<KeyValuePair<double, HM6Level>> list = ((_bid != null) ? _bid.Where(delegate(KeyValuePair<double, HM6Level> kv)
				{
					KeyValuePair<double, HM6Level> keyValuePair = kv;
					int result;
					if (keyValuePair.Value != null)
					{
						keyValuePair = kv;
						result = ((keyValuePair.Value.Qty >= minQty) ? 1 : 0);
					}
					else
					{
						result = 0;
					}
					return (byte)result != 0;
				}).OrderByDescending(delegate(KeyValuePair<double, HM6Level> kv)
				{
					KeyValuePair<double, HM6Level> keyValuePair = kv;
					return keyValuePair.Key;
				}).Take(count)
					.ToList() : new List<KeyValuePair<double, HM6Level>>());
				List<KeyValuePair<double, HM6Level>> list2 = ((_ask != null) ? _ask.Where(delegate(KeyValuePair<double, HM6Level> kv)
				{
					KeyValuePair<double, HM6Level> keyValuePair = kv;
					int result;
					if (keyValuePair.Value != null)
					{
						keyValuePair = kv;
						result = ((keyValuePair.Value.Qty >= minQty) ? 1 : 0);
					}
					else
					{
						result = 0;
					}
					return (byte)result != 0;
				}).OrderBy(delegate(KeyValuePair<double, HM6Level> kv)
				{
					KeyValuePair<double, HM6Level> keyValuePair = kv;
					return keyValuePair.Key;
				}).Take(count)
					.ToList() : new List<KeyValuePair<double, HM6Level>>());
				double num = 0.0;
				double num2 = 0.0;
				long num3 = 0L;
				long num4 = 0L;
				foreach (KeyValuePair<double, HM6Level> item in list)
				{
					HM6Level value = item.Value;
					hM6PassiveSummary.BidQty += value.Qty;
					hM6PassiveSummary.BidLevels++;
					double num5 = Math.Max(0.0, now - value.FirstSeen);
					double num6 = Clamp01(num5 / 6500.0);
					double num7 = Clamp01((double)value.RedCnt / Math.Max(1.0, (double)value.UpdCnt + 1.0));
					double num8 = Clamp01((double)value.StackCnt / 8.0) * 0.22;
					double num9 = Clamp01(0.16 + num6 * 0.72 - num7 * 0.38 + num8);
					num += num9 * (double)value.Qty;
					num3 += value.Qty;
				}
				foreach (KeyValuePair<double, HM6Level> item2 in list2)
				{
					HM6Level value2 = item2.Value;
					hM6PassiveSummary.AskQty += value2.Qty;
					hM6PassiveSummary.AskLevels++;
					double num10 = Math.Max(0.0, now - value2.FirstSeen);
					double num11 = Clamp01(num10 / 6500.0);
					double num12 = Clamp01((double)value2.RedCnt / Math.Max(1.0, (double)value2.UpdCnt + 1.0));
					double num13 = Clamp01((double)value2.StackCnt / 8.0) * 0.22;
					double num14 = Clamp01(0.16 + num11 * 0.72 - num12 * 0.38 + num13);
					num2 += num14 * (double)value2.Qty;
					num4 += value2.Qty;
				}
				hM6PassiveSummary.BidStability = ((num3 > 0) ? (num / (double)num3) : 0.0);
				hM6PassiveSummary.AskStability = ((num4 > 0) ? (num2 / (double)num4) : 0.0);
				double num15 = Math.Max(12000.0, Math.Min(90000.0, Math.Max((double)_pPassiveEventFadeMs * 8.0, (double)_pPassiveAggWindowMs * 28.0)));
				if (_passiveEvents != null)
				{
					foreach (HM6PassiveEvent passiveEvent in _passiveEvents)
					{
						if (passiveEvent == null)
						{
							continue;
						}
						double num16 = now - passiveEvent.Time;
						if (num16 < 0.0 || num16 > num15)
						{
							continue;
						}
						double num17 = Math.Pow(1.0 - num16 / num15, 0.38);
						long num18 = (long)Math.Max(1.0, (double)passiveEvent.Qty * num17);
						if (passiveEvent.WasBid)
						{
							if (passiveEvent.Type == 0)
							{
								hM6PassiveSummary.BidPull += num18;
							}
							else if (passiveEvent.Type == 1)
							{
								hM6PassiveSummary.BidConsume += num18;
							}
							else if (passiveEvent.Type == 2)
							{
								hM6PassiveSummary.BidRefill += num18;
							}
						}
						else if (passiveEvent.Type == 0)
						{
							hM6PassiveSummary.AskPull += num18;
						}
						else if (passiveEvent.Type == 1)
						{
							hM6PassiveSummary.AskConsume += num18;
						}
						else if (passiveEvent.Type == 2)
						{
							hM6PassiveSummary.AskRefill += num18;
						}
					}
				}
				if (hM6PassiveSummary.BidPull + hM6PassiveSummary.AskPull + hM6PassiveSummary.BidConsume + hM6PassiveSummary.AskConsume + hM6PassiveSummary.BidRefill + hM6PassiveSummary.AskRefill == 0L && _passiveZones != null)
				{
					foreach (KeyValuePair<string, HM6PassiveZone> passiveZone in _passiveZones)
					{
						HM6PassiveZone value3 = passiveZone.Value;
						if (value3 == null || value3.LastEventTime <= 0.0)
						{
							continue;
						}
						double num19 = now - value3.LastEventTime;
						if (num19 < 0.0 || num19 > num15)
						{
							continue;
						}
						long num20 = Math.Max(1L, value3.LastEventQty);
						if (value3.WasBid)
						{
							if (value3.LastEventType == 0)
							{
								hM6PassiveSummary.BidPull += num20;
							}
							else if (value3.LastEventType == 1)
							{
								hM6PassiveSummary.BidConsume += num20;
							}
							else if (value3.LastEventType == 2)
							{
								hM6PassiveSummary.BidRefill += num20;
							}
						}
						else if (value3.LastEventType == 0)
						{
							hM6PassiveSummary.AskPull += num20;
						}
						else if (value3.LastEventType == 1)
						{
							hM6PassiveSummary.AskConsume += num20;
						}
						else if (value3.LastEventType == 2)
						{
							hM6PassiveSummary.AskRefill += num20;
						}
					}
				}
				hM6PassiveSummary.BidRefillX = (double)hM6PassiveSummary.BidRefill / Math.Max(1.0, hM6PassiveSummary.BidConsume + hM6PassiveSummary.BidPull);
				hM6PassiveSummary.AskRefillX = (double)hM6PassiveSummary.AskRefill / Math.Max(1.0, hM6PassiveSummary.AskConsume + hM6PassiveSummary.AskPull);
				double num21 = Math.Max(1.0, Math.Max(hM6PassiveSummary.BidQty, hM6PassiveSummary.AskQty));
				double num22 = Clamp01((double)hM6PassiveSummary.BidQty / num21);
				double num23 = Clamp01((double)hM6PassiveSummary.AskQty / num21);
				double num24 = Clamp01(hM6PassiveSummary.BidRefillX);
				double num25 = Clamp01(hM6PassiveSummary.AskRefillX);
				double num26 = Clamp01((double)hM6PassiveSummary.BidPull / Math.Max(1.0, hM6PassiveSummary.BidQty + hM6PassiveSummary.BidPull));
				double num27 = Clamp01((double)hM6PassiveSummary.AskPull / Math.Max(1.0, hM6PassiveSummary.AskQty + hM6PassiveSummary.AskPull));
				double num28 = Clamp01((double)hM6PassiveSummary.BidConsume / Math.Max(1.0, hM6PassiveSummary.BidQty + hM6PassiveSummary.BidConsume));
				double num29 = Clamp01((double)hM6PassiveSummary.AskConsume / Math.Max(1.0, hM6PassiveSummary.AskQty + hM6PassiveSummary.AskConsume));
				hM6PassiveSummary.BidScore = Clamp01(num22 * 0.3 + hM6PassiveSummary.BidStability * 0.28 + num24 * 0.3 - num26 * 0.18 - num28 * 0.08 + 0.1);
				hM6PassiveSummary.AskScore = Clamp01(num23 * 0.3 + hM6PassiveSummary.AskStability * 0.28 + num25 * 0.3 - num27 * 0.18 - num29 * 0.08 + 0.1);
				hM6PassiveSummary.NetScore = (hM6PassiveSummary.BidScore - hM6PassiveSummary.AskScore) / Math.Max(0.05, hM6PassiveSummary.BidScore + hM6PassiveSummary.AskScore);
				hM6PassiveSummary.Signal = "NEUTRAL";
				hM6PassiveSummary.SignalBid = hM6PassiveSummary.NetScore >= 0.0;
				hM6PassiveSummary.SignalType = 0;
				if ((double)hM6PassiveSummary.BidRefill > Math.Max(_pPassiveEventMin, (double)hM6PassiveSummary.BidConsume * 0.55) && hM6PassiveSummary.BidRefillX >= 0.55)
				{
					hM6PassiveSummary.Signal = "REFILL";
					hM6PassiveSummary.SignalBid = true;
					hM6PassiveSummary.SignalType = 1;
				}
				else if ((double)hM6PassiveSummary.AskRefill > Math.Max(_pPassiveEventMin, (double)hM6PassiveSummary.AskConsume * 0.55) && hM6PassiveSummary.AskRefillX >= 0.55)
				{
					hM6PassiveSummary.Signal = "REFILL";
					hM6PassiveSummary.SignalBid = false;
					hM6PassiveSummary.SignalType = 1;
				}
				else if (hM6PassiveSummary.BidPull + hM6PassiveSummary.BidConsume > Math.Max(_pPassiveEventMin, hM6PassiveSummary.BidRefill * 2) && (double)hM6PassiveSummary.BidQty < (double)hM6PassiveSummary.AskQty * 0.75)
				{
					hM6PassiveSummary.Signal = "COMPRA PASIVA EJECUTADA";
					hM6PassiveSummary.SignalBid = false;
					hM6PassiveSummary.SignalType = 2;
				}
				else if (hM6PassiveSummary.AskPull + hM6PassiveSummary.AskConsume > Math.Max(_pPassiveEventMin, hM6PassiveSummary.AskRefill * 2) && (double)hM6PassiveSummary.AskQty < (double)hM6PassiveSummary.BidQty * 0.75)
				{
					hM6PassiveSummary.Signal = "VENTA PASIVA EJECUTADA";
					hM6PassiveSummary.SignalBid = true;
					hM6PassiveSummary.SignalType = 2;
				}
				else if (Math.Abs(hM6PassiveSummary.NetScore) >= 0.18)
				{
					hM6PassiveSummary.Signal = ((hM6PassiveSummary.NetScore > 0.0) ? "COMPRA PASIVA DOMINA" : "VENTA PASIVA DOMINA");
					hM6PassiveSummary.SignalBid = hM6PassiveSummary.NetScore > 0.0;
					hM6PassiveSummary.SignalType = 4;
				}
			}
			return hM6PassiveSummary;
		}

		private void TrimFlowEventsLocked(int maxCount)
		{
			if (_flows != null && maxCount > 0 && _flows.Count > maxCount)
			{
				_flows.RemoveRange(0, _flows.Count - maxCount);
			}
		}

		private void PublishCurrentSnapshotLocked(int nowMs, bool force)
		{
			if (_hist == null || base.CurrentBar < 0 || _snap == null)
			{
				return;
			}
			if (base.CurrentBar != _lastHistSnapshotBar)
			{
				_lastHistSnapshotBar = base.CurrentBar;
				_heatmapLiveBarIdx = base.CurrentBar;
				_heatmapLiveBarBornMs = nowMs;
			}
			if (base.CurrentBar != _heatmapLiveBarIdx || nowMs - _heatmapLiveBarBornMs >= 650)
			{
				int num = Math.Max(force ? 180 : 85, 220);
				if (nowMs - _lastHistSnapshotMs >= num)
				{
					_hist[base.CurrentBar] = _snap.Clone();
					TrimDomHistory(base.CurrentBar);
					_lastHistSnapshotMs = nowMs;
				}
			}
		}

		private void PublishLiveHeatmapSliceLocked(int nowMs, bool force)
		{
			if (_snap == null || !_snap.HasData || (!force && nowMs - _lastLiveHeatmapSliceMs < LiveHeatmapSliceIntervalMs()))
			{
				return;
			}
			long num = Math.Max(1L, _pF);
			bool flag = false;
			bool flag2 = false;
			foreach (KeyValuePair<double, long> item in _snap.Bid)
			{
				if (item.Value >= num)
				{
					flag = true;
					break;
				}
			}
			foreach (KeyValuePair<double, long> item2 in _snap.Ask)
			{
				if (item2.Value >= num)
				{
					flag2 = true;
					break;
				}
			}
			if (!_liveHeatmapDomReady)
			{
				if (!flag || !flag2)
				{
					if (_pendingPassiveHeatmapEvents != null)
					{
						_pendingPassiveHeatmapEvents.Clear();
					}
					return;
				}
				_liveHeatmapDomReady = true;
				_liveHeatmapWarmupUntilMs = nowMs + 1400;
				if (_liveHeatmapSlices != null)
				{
					_liveHeatmapSlices.Clear();
				}
				if (_pendingPassiveHeatmapEvents != null)
				{
					_pendingPassiveHeatmapEvents.Clear();
				}
				_liveHeatmapSeq = 0L;
				_lastLiveHeatmapSliceMs = nowMs;
			}
			else if (nowMs < _liveHeatmapWarmupUntilMs)
			{
				if (_pendingPassiveHeatmapEvents != null)
				{
					_pendingPassiveHeatmapEvents.Clear();
				}
				_lastLiveHeatmapSliceMs = nowMs;
			}
			else
			{
				if (!flag && !flag2)
				{
					return;
				}
				if (_liveHeatmapSeq <= 0 && (!flag || !flag2))
				{
					if (_pendingPassiveHeatmapEvents != null)
					{
						_pendingPassiveHeatmapEvents.Clear();
					}
					return;
				}
				HM6HeatmapSlice hM6HeatmapSlice = new HM6HeatmapSlice();
				hM6HeatmapSlice.Seq = ++_liveHeatmapSeq;
				hM6HeatmapSlice.Time = nowMs;
				hM6HeatmapSlice.BarIdx = PrimaryBarIndexSafe();
				if (_pendingPassiveHeatmapEvents != null && _pendingPassiveHeatmapEvents.Count > 0)
				{
					for (int i = 0; i < _pendingPassiveHeatmapEvents.Count; i++)
					{
						HM6PassiveEvent hM6PassiveEvent = _pendingPassiveHeatmapEvents[i];
						if (hM6PassiveEvent != null)
						{
							hM6PassiveEvent.SliceSeq = hM6HeatmapSlice.Seq;
							hM6PassiveEvent.BarIdx = hM6HeatmapSlice.BarIdx;
							if (hM6HeatmapSlice.Events == null)
							{
								hM6HeatmapSlice.Events = new List<HM6PassiveEvent>();
							}
							hM6HeatmapSlice.Events.Add(hM6PassiveEvent);
						}
					}
					_pendingPassiveHeatmapEvents.Clear();
				}
				foreach (KeyValuePair<double, long> item3 in _snap.Bid)
				{
					if (item3.Value >= num)
					{
						hM6HeatmapSlice.Bid[item3.Key] = item3.Value;
					}
				}
				foreach (KeyValuePair<double, long> item4 in _snap.Ask)
				{
					if (item4.Value >= num)
					{
						hM6HeatmapSlice.Ask[item4.Key] = item4.Value;
					}
				}
				if (hM6HeatmapSlice.HasData)
				{
					if (_liveHeatmapSlices == null)
					{
						_liveHeatmapSlices = new List<HM6HeatmapSlice>(512);
					}
					_liveHeatmapSlices.Add(hM6HeatmapSlice);
					_lastLiveHeatmapSliceMs = nowMs;
					int num2 = Math.Max(120, Math.Min(1200, Math.Max(120, PassiveZoneRetentionBars() * 2)));
					if (_liveHeatmapSlices.Count > num2)
					{
						_liveHeatmapSlices.RemoveRange(0, _liveHeatmapSlices.Count - num2);
					}
				}
			}
		}

		private void CleanEventsThrottled(int nowMs)
		{
			if (nowMs - _lastCleanEventsMs >= 90)
			{
				_lastCleanEventsMs = nowMs;
				CleanEvents(nowMs);
			}
		}

		private void RequestRefresh(bool immediate)
		{
			if (immediate || !_flowTimerActive)
			{
				int iltDevNowMs = IltDevNowMs;
				int num = Math.Max(8, Math.Min(30, _pFlowFps));
				int num2 = ((!immediate) ? Math.Max(15, 1000 / num) : 0);
				if (immediate || iltDevNowMs - _lastRefreshMs >= num2)
				{
					_lastRefreshMs = iltDevNowMs;
					ForceChartRefreshSafe();
				}
			}
		}

		private void ForceChartRefreshSafe()
		{
			try
			{
				if (base.ChartControl != null && ((DispatcherObject)base.ChartControl).Dispatcher != null && !((DispatcherObject)base.ChartControl).Dispatcher.CheckAccess())
				{
					((DispatcherObject)base.ChartControl).Dispatcher.InvokeAsync(delegate
					{
						ForceChartRefreshOnUi();
					});
				}
				else
				{
					ForceChartRefreshOnUi();
				}
			}
			catch
			{
				try
				{
					ForceRefresh();
				}
				catch
				{
				}
			}
		}

		private void ForceChartRefreshOnUi()
		{
			try
			{
				ForceRefresh();
			}
			catch
			{
			}
		}

		protected override void OnMarketData(MarketDataEventArgs e)
		{
			if (e == null)
			{
				return;
			}
			if (e.MarketDataType == MarketDataType.Bid)
			{
				if (e.Price > 0.0)
				{
					lock (_lk)
					{
						_quoteBestBid = e.Price;
						_lastQuoteTime = DateTime.Now;
					}
				}
			}
			else if (e.MarketDataType == MarketDataType.Ask)
			{
				if (e.Price > 0.0)
				{
					lock (_lk)
					{
						_quoteBestAsk = e.Price;
						_lastQuoteTime = DateTime.Now;
					}
				}
			}
			else
			{
				if (e.MarketDataType != MarketDataType.Last || base.State != State.Realtime)
				{
					return;
				}
				long volume = e.Volume;
				if (volume <= 0 || e.Price <= 0.0)
				{
					return;
				}
				DateTime dateTime;
				try
				{
					dateTime = e.Time;
				}
				catch
				{
					dateTime = ((base.CurrentBar >= 0) ? base.Time[0] : DateTime.Now);
				}
				DateTime ny = ToNewYorkTime(dateTime);
				if (IsInsideNyTradingSessionNy(ny))
				{
					DateTime nyTradingSessionDateNy = GetNyTradingSessionDateNy(ny);
					if (_sessionDate == DateTime.MinValue || nyTradingSessionDateNy != _sessionDate)
					{
						ResetDomSession(nyTradingSessionDateNy);
					}
					int barIdx = PrimaryBarIndexSafe();
					ProcessTapeTrade(e.Price, volume, barIdx, dateTime);
					_lastLiveLastMs = IltDevNowMs;
					RequestRefresh(false);
				}
			}
		}

		private int GetTapeCaptureFloor()
		{
			return Math.Max(1, _pBub);
		}

		private bool ShouldStoreTapePrint(long vol)
		{
			return vol >= GetTapeCaptureFloor();
		}

		private void UpdateTapeDirectionOnly(double tradePrice)
		{
			if (!(tradePrice <= 0.0) && !double.IsNaN(tradePrice) && !double.IsInfinity(tradePrice))
			{
				ClassifyTradeSide(tradePrice);
			}
		}

		private void ProcessTapeTrade(double tradePrice, long vol, int barIdx, DateTime tradeTime)
		{
			if (vol <= 0 || tradePrice <= 0.0 || double.IsNaN(tradePrice) || double.IsInfinity(tradePrice))
			{
				return;
			}
			bool flag = base.State == State.Realtime;
			int num = (flag ? IltDevNowMs : 0);
			int key = NormalizeToTickPriceKey(tradePrice);
			double num2 = PriceFromKey(key);
			int num3 = ClassifyTradeSide(tradePrice);
			bool isBuy = num3 > 0 || (num3 == 0 && _lastTapeDir >= 0);
			lock (_lk)
			{
				if (barIdx >= 0 && ShouldStoreTapePrint(vol))
				{
					long ticks = tradeTime.Ticks;
					HM6Trade mergedTrade;
					if (!TryMergeAggressiveBubbleLocked(num2, vol, isBuy, barIdx, num, ticks, out mergedTrade) || mergedTrade == null)
					{
						mergedTrade = new HM6Trade();
						mergedTrade.Price = num2;
						mergedTrade.RawPrice = num2;
						mergedTrade.Vol = vol;
						mergedTrade.MaxSingle = vol;
						mergedTrade.TradeCount = 1;
						mergedTrade.IsBuy = isBuy;
						mergedTrade.Time = num;
						mergedTrade.CreatedMs = num;
						mergedTrade.LastMs = num;
						mergedTrade.BarIdx = barIdx;
						mergedTrade.StampTicks = ticks;
						mergedTrade.Seq = ++_tradeSeq;
						mergedTrade.IsBlock = false;
						_trades.Add(mergedTrade);
					}
					long num4 = Math.Max(mergedTrade.Vol, mergedTrade.MaxSingle);
					if (_pSRZones && num3 != 0 && num4 >= IltDevSRInstitutionalMinQty())
					{
						RegisterSRPendingLocked(num2, num3, num4, num);
					}
					if (_pSRHist && num3 != 0 && num4 >= Math.Max(1, _pSRHistBig))
					{
						RegisterSRHistLocked(num2, num3, num4, num);
					}
					TrimTradeHistoryLocked(false);
					if (flag)
					{
						StartFlowTimerIfNeeded();
					}
				}
				if (num3 < 0)
				{
					RegisterPassiveAggressionLocked(true, num2, vol, num);
				}
				else if (num3 > 0)
				{
					RegisterPassiveAggressionLocked(false, num2, vol, num);
				}
				RegisterDefenseResponseDeltaLocked(num2, num3, vol, num);
				UpdateDefendedWallLinesLocked(num2, num2, num2, num);
				CleanPassiveEventLabelsOnPriceInteractionLocked(num2, num2, num2, num);
				if (num3 == 0)
				{
					return;
				}
				HM6Level value = null;
				if (num3 < 0)
				{
					_bid.TryGetValue(num2, out value);
				}
				else
				{
					_ask.TryGetValue(num2, out value);
				}
				if (value == null)
				{
					return;
				}
				long val = Math.Max(1L, Math.Max(_pPassiveMinQty, _pLadF));
				long val2 = Math.Max(1L, (long)Math.Ceiling((double)Math.Max(1L, value.MaxQty) * 0.25));
				if (value.Qty < Math.Max(val, val2))
				{
					return;
				}
				if (value.LastAbsorbTime <= 0.0 || (double)num - value.LastAbsorbTime > 1800.0)
				{
					value.RecentAbsorbedVol = 0L;
					value.AbsorbCount = 0;
				}
				value.AbsorbedVol += vol;
				value.RecentAbsorbedVol += vol;
				value.AbsorbCount++;
				value.LastAbsorbTime = num;
				long num5 = Math.Max(1L, _pPassiveEventMin);
				bool flag2 = value.AbsorbCount >= 2 && value.RecentAbsorbedVol >= num5;
				bool flag3 = value.LastAbsorbMarkerTime <= 0.0 || (double)num - value.LastAbsorbMarkerTime >= 700.0;
				bool flag4 = value.RecentAbsorbedVol >= value.LastAbsorbMarkerQty + num5;
				if (flag2 && (flag3 || flag4))
				{
					value.LastAbsorbMarkerTime = num;
					value.LastAbsorbMarkerQty = value.RecentAbsorbedVol;
					if (AddPassiveEventLocked(num2, value.RecentAbsorbedVol, num3 < 0, 5, num))
					{
						PublishLiveHeatmapSliceLocked(num, true);
					}
				}
			}
		}

		private bool TryMergeAggressiveBubbleLocked(double px, long vol, bool isBuy, int barIdx, int nowMs, long stampTicks, out HM6Trade mergedTrade)
		{
			mergedTrade = null;
			if (_trades == null || _trades.Count == 0)
			{
				return false;
			}
			if (vol <= 0 || barIdx < 0)
			{
				return false;
			}
			if (stampTicks <= 0)
			{
				return false;
			}
			long num = 2800000L;
			int num2 = NormalizeToTickPriceKey(px);
			int num3 = 32;
			int num4 = _trades.Count - 1;
			int num5 = 0;
			while (num4 >= 0 && num5 < num3)
			{
				HM6Trade hM6Trade = _trades[num4];
				if (hM6Trade != null)
				{
					if (hM6Trade.BarIdx != barIdx)
					{
						break;
					}
					if (hM6Trade.IsBuy == isBuy && NormalizeToTickPriceKey((hM6Trade.RawPrice > 0.0) ? hM6Trade.RawPrice : hM6Trade.Price) == num2 && hM6Trade.StampTicks > 0)
					{
						long num6 = stampTicks - hM6Trade.StampTicks;
						if (num6 >= 0 && num6 <= num)
						{
							hM6Trade.Vol += vol;
							if (vol > hM6Trade.MaxSingle)
							{
								hM6Trade.MaxSingle = vol;
							}
							hM6Trade.TradeCount = Math.Max(1, hM6Trade.TradeCount + 1);
							hM6Trade.StampTicks = stampTicks;
							if (nowMs > 0)
							{
								hM6Trade.LastMs = nowMs;
								hM6Trade.Time = nowMs;
							}
							mergedTrade = hM6Trade;
							return true;
						}
					}
				}
				num4--;
				num5++;
			}
			return false;
		}

		private bool IsTradeArchivedLocked(long seq)
		{
			if (seq <= 0 || _btArchive == null || _btArchive.Count == 0)
			{
				return false;
			}
			for (int num = _btArchive.Count - 1; num >= 0; num--)
			{
				HM6Trade hM6Trade = _btArchive[num];
				if (hM6Trade != null && hM6Trade.Seq == seq)
				{
					return true;
				}
				if (_btArchive.Count - num > 128)
				{
					break;
				}
			}
			return false;
		}

		private void TrimTradeHistoryLocked()
		{
			TrimTradeHistoryLocked(false);
		}

		private void TrimTradeHistoryLocked(bool force)
		{
			if (_trades == null)
			{
				return;
			}
			int num = 4096;
			int num2 = (force ? num : (num + 512));
			if (_trades.Count <= num2)
			{
				return;
			}
			int num3 = _trades.Count - num;
			if (num3 > 0)
			{
				_trades.RemoveRange(0, num3);
				RebuildTradeBarIndexLocked();
				InvalidateBubbleRenderCacheLocked();
				if (force || base.State == State.Realtime)
				{
					RecalculateMaxTradeVolLocked();
				}
				InvalidateBubbleRenderCacheLocked();
			}
		}

		private void TrimBigTradeArchiveLocked(bool force)
		{
			if (_btArchive == null)
			{
				return;
			}
			int num = 250000;
			int num2 = (force ? num : (num + 8192));
			if (_btArchive.Count > num2)
			{
				int num3 = _btArchive.Count - num;
				if (num3 > 0)
				{
					_btArchive.RemoveRange(0, num3);
					RebuildBtArchiveBarIndexLocked();
					InvalidateBubbleRenderCacheLocked();
					InvalidateBubbleRenderCacheLocked();
				}
			}
		}

		private void AddTradeToBarIndexLocked(Dictionary<int, List<HM6Trade>> map, HM6Trade tr)
		{
			if (map != null && tr != null && tr.BarIdx >= 0)
			{
				List<HM6Trade> value;
				if (!map.TryGetValue(tr.BarIdx, out value))
				{
					value = new List<HM6Trade>(8);
					map[tr.BarIdx] = value;
				}
				value.Add(tr);
			}
		}

		private void RebuildTradeBarIndexLocked()
		{
			if (_tradesByBar == null)
			{
				_tradesByBar = new Dictionary<int, List<HM6Trade>>();
			}
			else
			{
				_tradesByBar.Clear();
			}
			if (_trades != null)
			{
				for (int i = 0; i < _trades.Count; i++)
				{
					AddTradeToBarIndexLocked(_tradesByBar, _trades[i]);
				}
				InvalidateBubbleRenderCacheLocked();
			}
		}

		private void RebuildBtArchiveBarIndexLocked()
		{
			if (_btArchiveByBar == null)
			{
				_btArchiveByBar = new Dictionary<int, List<HM6Trade>>();
			}
			else
			{
				_btArchiveByBar.Clear();
			}
			if (_btArchive != null)
			{
				for (int i = 0; i < _btArchive.Count; i++)
				{
					AddTradeToBarIndexLocked(_btArchiveByBar, _btArchive[i]);
				}
				InvalidateBubbleRenderCacheLocked();
			}
		}

		private void RecalculateMaxTradeVolLocked()
		{
			long num = 1L;
			if (_trades != null)
			{
				for (int i = 0; i < _trades.Count; i++)
				{
					HM6Trade hM6Trade = _trades[i];
					hM6Trade.IsBlock = Math.Max(hM6Trade.Vol, hM6Trade.MaxSingle) >= _pBtVisualMin;
					if (Math.Max(hM6Trade.Vol, hM6Trade.MaxSingle) > num)
					{
						num = Math.Max(hM6Trade.Vol, hM6Trade.MaxSingle);
					}
				}
			}
			_maxTradeVol = num;
		}

		private double GroupedPriceStep()
		{
			return Math.Max(_tick, 1E-10);
		}

		private double NormalizeToGroupedPrice(double price)
		{
			return base.Instrument.MasterInstrument.RoundToTickSize(price);
		}

		private int NormalizeToTickPriceKey(double price)
		{
			double num = Math.Max(_tick, 1E-10);
			return (int)Math.Round(price / num, MidpointRounding.AwayFromZero);
		}

		private double PriceFromKey(int key)
		{
			return base.Instrument.MasterInstrument.RoundToTickSize((double)key * Math.Max(_tick, 1E-10));
		}

		private int ClassifyTradeSide(double tradePrice)
		{
			double num = 0.0;
			double num2 = double.MaxValue;
			lock (_lk)
			{
				num = _quoteBestBid;
				num2 = _quoteBestAsk;
			}
			bool flag = num > 0.0 && num2 < double.MaxValue && num2 > 0.0 && num2 >= num;
			double num3 = Math.Max(_tick, 1E-10) * 0.5;
			if (flag)
			{
				if (tradePrice >= num2 - num3)
				{
					_lastTapePrice = tradePrice;
					_lastTapeDir = 1;
					return 1;
				}
				if (tradePrice <= num + num3)
				{
					_lastTapePrice = tradePrice;
					_lastTapeDir = -1;
					return -1;
				}
			}
			if (GetTickRuleDirection(tradePrice) < 0)
			{
				return -1;
			}
			return 1;
		}

		private bool PrimaryBarContainsPrice(int barIdx, double price, double toleranceTicks)
		{
			if (barIdx < 0)
			{
				return false;
			}
			try
			{
				double valueAt = base.Highs[0].GetValueAt(barIdx);
				double valueAt2 = base.Lows[0].GetValueAt(barIdx);
				double num = Math.Max(_tick, 1E-10) * Math.Max(0.55, toleranceTicks);
				return price <= valueAt + num && price >= valueAt2 - num;
			}
			catch
			{
				return false;
			}
		}

		private int GetPrimaryBarIndex(DateTime time, double price)
		{
			int num = ((base.CurrentBars != null && base.CurrentBars.Length != 0) ? base.CurrentBars[0] : (-1));
			if (num < 0)
			{
				return -1;
			}
			int num2 = num;
			try
			{
				if (base.BarsArray != null && base.BarsArray.Length != 0 && base.BarsArray[0] != null)
				{
					num2 = Math.Max(num, base.BarsArray[0].Count - 1);
				}
			}
			catch
			{
				num2 = num;
			}
			double toleranceTicks = 1.15;
			if (base.State == State.Realtime)
			{
				if (PrimaryBarContainsPrice(num, price, toleranceTicks))
				{
					return num;
				}
				int num3 = Math.Max(0, num - 3);
				int num4 = Math.Min(num2, num + 2);
				for (int i = num3; i <= num4; i++)
				{
					if (PrimaryBarContainsPrice(i, price, toleranceTicks))
					{
						return i;
					}
				}
				return -1;
			}
			int j = -1;
			try
			{
				if (base.BarsArray != null && base.BarsArray.Length != 0 && base.BarsArray[0] != null)
				{
					j = base.BarsArray[0].GetBar(time);
				}
			}
			catch
			{
				j = -1;
			}
			if (j < 0)
			{
				j = ((_histPrimaryAnchorBar >= 0) ? Math.Min(_histPrimaryAnchorBar, num2) : Math.Min(num, num2));
				if (j < 0)
				{
					return -1;
				}
			}
			if (j > num2)
			{
				j = num2;
			}
			if (j < 0)
			{
				j = 0;
			}
			try
			{
				int num5 = 0;
				for (; j < num2; j++)
				{
					if (num5++ >= 64)
					{
						break;
					}
					DateTime valueAt = base.Times[0].GetValueAt(j);
					if (valueAt >= time)
					{
						break;
					}
				}
			}
			catch
			{
			}
			_histPrimaryAnchorBar = j;
			return j;
		}

		private int GetTickRuleDirection(double price)
		{
			int num = ((_lastTapePrice <= 0.0) ? 1 : ((price > _lastTapePrice) ? 1 : ((price < _lastTapePrice) ? (-1) : ((_lastTapeDir == 0) ? 1 : _lastTapeDir))));
			_lastTapePrice = price;
			_lastTapeDir = num;
			return num;
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

		private DateTime GetNyTradingSessionDateNy(DateTime ny)
		{
			TimeSpan timeOfDay = ny.TimeOfDay;
			if (!(timeOfDay >= new TimeSpan(18, 0, 0)))
			{
				return ny.Date;
			}
			return ny.Date.AddDays(1.0);
		}

		private bool IsInsideNyTradingSessionNy(DateTime ny)
		{
			TimeSpan timeOfDay = ny.TimeOfDay;
			if (!(timeOfDay >= new TimeSpan(18, 0, 0)))
			{
				return timeOfDay < new TimeSpan(17, 0, 0);
			}
			return true;
		}

		private void ResetDomSession(DateTime sessionDate)
		{
			lock (_lk)
			{
				_sessionDate = sessionDate.Date;
				if (_passivePocMap != null)
				{
					_passivePocMap.Clear();
				}
				_quoteBestBid = 0.0;
				_quoteBestAsk = double.MaxValue;
				_lastQuoteTime = DateTime.MinValue;
				_lastLiveLastMs = -100000000;
				_passivePocPrice = 0.0;
				_passivePocVol = 0L;
				if (_trades != null)
				{
					TrimTradeHistoryLocked(false);
				}
				if (_btArchive != null)
				{
					TrimBigTradeArchiveLocked(false);
				}
				if (_flows != null)
				{
					_flows.Clear();
				}
				if (_passiveEvents != null)
				{
					_passiveEvents.Clear();
				}
				if (_pendingPassiveHeatmapEvents != null)
				{
					_pendingPassiveHeatmapEvents.Clear();
				}
				if (_defendedWallLines != null)
				{
					_defendedWallLines.RemoveAll((HM6DefendedWallLine d) => !IsProtectedActiveIltDevZone(d));
					TrimDefendedWallLinesLocked();
				}
				if (_liveHeatmapSlices != null)
				{
					_liveHeatmapSlices.Clear();
				}
				_liveHeatmapSeq = 0L;
				_lastLiveHeatmapSliceMs = -100000000;
				_liveHeatmapDomReady = false;
				_liveHeatmapWarmupUntilMs = -100000000;
				_lastTapePrice = 0.0;
				_lastTapeDir = 0;
				_histPrimaryAnchorBar = -1;
				if (_ghostAgg != null)
				{
					_ghostAgg.Clear();
				}
				if (_iltDevZoneAggSamples != null)
				{
					_iltDevZoneAggSamples.Clear();
				}
				_iltDevZoneAdaptiveCached = 0L;
				_iltDevZoneAdaptiveLastCalc = -1.0;
			}
		}

		protected override void OnBarUpdate()
		{
			if (base.BarsInProgress == 1)
			{
				if (base.CurrentBars[1] < 0 || base.CurrentBars[0] < 0)
				{
					return;
				}
				DateTime dateTime = base.Times[1][0];
				DateTime ny = ToNewYorkTime(dateTime);
				if (!IsInsideNyTradingSessionNy(ny))
				{
					return;
				}
				DateTime nyTradingSessionDateNy = GetNyTradingSessionDateNy(ny);
				if (_sessionDate == DateTime.MinValue || nyTradingSessionDateNy != _sessionDate)
				{
					ResetDomSession(nyTradingSessionDateNy);
				}
				if (base.State == State.Realtime && IltDevNowMs - _lastLiveLastMs < 1000)
				{
					return;
				}
				long num = (long)Math.Max(0.0, base.Volumes[1][0]);
				if (num > 0)
				{
					double num2 = base.Closes[1][0];
					if (base.State != State.Realtime && num < GetTapeCaptureFloor())
					{
						UpdateTapeDirectionOnly(num2);
						return;
					}
					int barIdx = ((base.State == State.Realtime) ? PrimaryBarIndexSafe() : GetPrimaryBarIndex(dateTime, num2));
					ProcessTapeTrade(num2, num, barIdx, dateTime);
				}
			}
			else
			{
				if (base.BarsInProgress != 0 || base.CurrentBar < 1)
				{
					return;
				}
				lock (_lk)
				{
					int iltDevNowMs = IltDevNowMs;
					PublishCurrentSnapshotLocked(iltDevNowMs, false);
					MarkPassiveWallsTouchedByBarLocked(base.Low[0], base.High[0], iltDevNowMs);
					CleanPassiveEventLabelsOnPriceInteractionLocked(base.Low[0], base.High[0], base.Close[0], iltDevNowMs);
					UpdateDefendedWallLinesLocked(base.Low[0], base.High[0], base.Close[0], iltDevNowMs);
					if (base.CurrentBar >= 1 && base.CurrentBar > _lastClosedBarEval)
					{
						_lastClosedBarEval = base.CurrentBar;
						EvaluateDefendedWallClosesLocked(base.Close[1], base.High[1], base.Low[1], base.CurrentBar - 1, iltDevNowMs);
						if (_pSRZones)
						{
							EvaluateSRZonesCloseLocked(base.Close[1], base.CurrentBar - 1);
						}
						if (_pSRZones)
						{
							EvaluateIltDevSupportClosesLocked(base.Open[1], base.Close[1], base.CurrentBar - 1);
						}
					}
					if (_pSRZones)
					{
						UpdateSRZonesTouchLocked(base.Low[0], base.High[0], base.CurrentBar);
					}
					if (_pSRHist)
					{
						UpdateSRHistDefenseLocked(iltDevNowMs);
					}
					TrimPassiveZonesLocked(iltDevNowMs);
				}
			}
		}

		private void TrimDomHistory(int currentBar)
		{
			if (_hist == null || _hist.Count == 0)
			{
				return;
			}
			if (_histTrimNextBar == int.MinValue)
			{
				_histTrimNextBar = currentBar + 256;
			}
			if (currentBar < _histTrimNextBar)
			{
				return;
			}
			_histTrimNextBar = currentBar + 256;
			int num = currentBar - 6000;
			if (num <= 0)
			{
				return;
			}
			_histTrimScratch.Clear();
			foreach (int key in _hist.Keys)
			{
				if (key < num)
				{
					_histTrimScratch.Add(key);
				}
			}
			for (int i = 0; i < _histTrimScratch.Count; i++)
			{
				_hist.Remove(_histTrimScratch[i]);
			}
		}

		private void EnsureFlowTimer()
		{
			if (base.ChartControl != null && _flowTimer == null)
			{
				_flowTimer = new DispatcherTimer(DispatcherPriority.Render, ((DispatcherObject)base.ChartControl).Dispatcher);
				_flowTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / (double)Math.Max(8, Math.Min(30, _pFlowFps)));
				_flowTimer.Tick += FlowTimerTick;
			}
		}

		private void FlowTimerTick(object sender, EventArgs e)
		{
			try
			{
				int iltDevNowMs = IltDevNowMs;
				CleanEvents(iltDevNowMs);
				bool flag;
				bool flag2;
				lock (_lk)
				{
					flag = _pFlow && _flows != null && _flows.Count > 0;
					flag2 = _pPassive && _passiveEvents != null && _passiveEvents.Any((HM6PassiveEvent ev) => ev != null && ev.Fade > 0f);
				}
				bool flag3 = _pBubShow && HasActiveBubbleAnimation(iltDevNowMs);
				if (!flag && !flag3 && !flag2)
				{
					StopFlowTimer();
				}
				else
				{
					RequestRefresh(false);
				}
			}
			catch
			{
				StopFlowTimer();
			}
		}

		private void StartFlowTimerIfNeeded()
		{
			if (base.ChartControl == null || (!_pFlow && !_pBubShow && !_pPassive))
			{
				return;
			}
			try
			{
				((DispatcherObject)base.ChartControl).Dispatcher.InvokeAsync(delegate
				{
					EnsureFlowTimer();
					if (_flowTimer != null)
					{
						_flowTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / (double)Math.Max(8, Math.Min(30, _pFlowFps)));
						if (!_flowTimer.IsEnabled)
						{
							_flowTimer.Start();
							_flowTimerActive = true;
						}
					}
				});
			}
			catch
			{
			}
		}

		private bool HasActiveBubbleAnimation(int now)
		{
			int num = Math.Max(0, _pBubAnimMs);
			int num2 = ((_pBtFlash && _pBubShow) ? Math.Max(0, _pBtFlashMs) : 0);
			int num3 = Math.Max(num, num2);
			if (num3 <= 0)
			{
				return false;
			}
			if (num2 > 0 && _lastLiveBtMs > -100000000)
			{
				int num4 = now - _lastLiveBtMs;
				if (num4 >= 0 && num4 <= num2)
				{
					return true;
				}
			}
			if (num <= 0)
			{
				return false;
			}
			Math.Max(1, _pBtVisualMin);
			lock (_lk)
			{
				if (_trades == null || _trades.Count == 0)
				{
					return false;
				}
				for (int num5 = _trades.Count - 1; num5 >= 0; num5--)
				{
					HM6Trade hM6Trade = _trades[num5];
					int num6 = ((hM6Trade.LastMs > 0) ? hM6Trade.LastMs : ((hM6Trade.Time > 0.0) ? ((int)hM6Trade.Time) : 0));
					if (num6 > 0)
					{
						int num7 = now - num6;
						if (num7 <= num)
						{
							return true;
						}
						if (num5 < _trades.Count - 80)
						{
							break;
						}
					}
				}
			}
			return false;
		}

		private void StopFlowTimer()
		{
			try
			{
				if (_flowTimer != null && _flowTimer.IsEnabled)
				{
					_flowTimer.Stop();
				}
				_flowTimerActive = false;
			}
			catch
			{
			}
		}

		private void DisposeFlowTimer()
		{
			try
			{
				if (_flowTimer != null)
				{
					if (_flowTimer.IsEnabled)
					{
						_flowTimer.Stop();
					}
					_flowTimer.Tick -= FlowTimerTick;
					_flowTimer = null;
				}
				_flowTimerActive = false;
			}
			catch
			{
			}
		}

		private void CleanEvents(double now)
		{
			lock (_lk)
			{
				if (_pulls != null)
				{
					for (int num = _pulls.Count - 1; num >= 0; num--)
					{
						_pulls[num].Fade = (float)Math.Max(0.0, 1.0 - (now - _pulls[num].Time) / 9000.0);
						if (_pulls[num].Fade <= 0f)
						{
							_pulls.RemoveAt(num);
						}
					}
				}
				if (_flows != null)
				{
					for (int num2 = _flows.Count - 1; num2 >= 0; num2--)
					{
						_flows[num2].Fade = (float)Math.Max(0.0, 1.0 - (now - _flows[num2].Time) / Math.Max(250.0, _pFlowFadeMs));
						if (_flows[num2].Fade <= 0f)
						{
							_flows.RemoveAt(num2);
						}
					}
				}
				if (_passiveEvents == null)
				{
					return;
				}
				double num3 = Math.Max(900.0, _pPassiveEventFadeMs);
				int num4 = PrimaryBarIndexSafe();
				int num5 = Math.Max(20, Math.Min(2000, _pPassiveWallMaxBars));
				for (int num6 = _passiveEvents.Count - 1; num6 >= 0; num6--)
				{
					HM6PassiveEvent hM6PassiveEvent = _passiveEvents[num6];
					if (hM6PassiveEvent == null)
					{
						_passiveEvents.RemoveAt(num6);
					}
					else
					{
						hM6PassiveEvent.Fade = (float)Math.Max(0.0, 1.0 - (now - hM6PassiveEvent.Time) / num3);
						int num7 = ((hM6PassiveEvent.BarIdx >= 0) ? hM6PassiveEvent.BarIdx : num4);
						bool flag = num4 - num7 > num5;
						bool flag2 = hM6PassiveEvent.BarIdx < 0 && now - hM6PassiveEvent.Time > num3 * 8.0;
						if (flag || flag2)
						{
							_passiveEvents.RemoveAt(num6);
						}
					}
				}
			}
		}

		protected override void OnRender(ChartControl cc, ChartScale cs)
		{
			base.OnRender(cc, cs);
			if (cc == null || cs == null || base.RenderTarget == null || base.ChartPanel == null)
			{
				return;
			}
			float width = base.RenderTarget.Size.Width;
			float height = base.RenderTarget.Size.Height;
			float num = width / Math.Max(1f, (float)((FrameworkElement)cc).ActualWidth);
			float num2 = height / Math.Max(1f, (float)((FrameworkElement)cc).ActualHeight);
			_ldX = num;
			_ldY = num2;
			EnsureDX(Math.Max(num, num2));
			if (!_dxOk)
			{
				return;
			}
			_clipDepth = 0;
			float cpW = ((base.ChartPanel != null) ? ((float)((FrameworkElement)base.ChartPanel).ActualWidth * num) : width);
			try
			{
				IltDevRenderBody(cc, cs, width, height, num, num2, cpW);
			}
			catch (Exception ex)
			{
				IltDevRenderError(ex);
			}
			finally
			{
				while (_clipDepth > 0)
				{
					_clipDepth--;
					try
					{
						base.RenderTarget.PopAxisAlignedClip();
					}
					catch
					{
						break;
					}
				}
			}
		}

		private void IltDevPushWinClip(float winL, float cR, float H, float dX)
		{
			try
			{
				base.RenderTarget.PushAxisAlignedClip(new RectangleF(winL, -8f, cR - winL + 60f * dX, H + 16f), AntialiasMode.Aliased);
				_clipDepth++;
			}
			catch
			{
			}
		}

		private void IltDevPopWinClip()
		{
			if (_clipDepth <= 0)
			{
				return;
			}
			_clipDepth--;
			try
			{
				base.RenderTarget.PopAxisAlignedClip();
			}
			catch
			{
			}
		}

		private void IltDevRenderError(Exception ex)
		{
			if (!DebugRender)
			{
				return;
			}
			int iltDevNowMs = IltDevNowMs;
			if (iltDevNowMs - _lastRenderErrMs < 5000)
			{
				return;
			}
			_lastRenderErrMs = iltDevNowMs;
			try
			{
				Print("[ILT-DEV DOMHEATMAP] Error en OnRender: " + ex.Message + " | " + ex.StackTrace);
			}
			catch
			{
			}
		}

		private void IltDevRenderBody(ChartControl cc, ChartScale cs, float W, float H, float dX, float dY, float cpW)
		{
			float num = 255f * dX + 2f;
			float num2 = 116f * dX;
			float val = Math.Max(0f, cpW - num2 - (num + 70f * dX));
			float num3 = (_pLad ? Math.Max(0f, Math.Min(val, (float)_pLadderShiftPx * dX)) : 0f);
			float num4 = (_pLad ? (cpW - num2 - num3) : cpW);
			float num5 = (_pLad ? num4 : cpW);
			float num6 = RenderWinLeft(num5);
			bool flag = num6 > 0.5f;
			if (flag)
			{
				IltDevPushWinClip(num6, cpW, H, dX);
			}
			if (_pBasicVisual)
			{
				DrawHeatmap(cc, cs, cpW, H, dX, dY);
				DrawLiveHeatmapTape(cc, cs, num5, H, dX, dY);
			}
			if (_pPassive)
			{
				DrawPassiveLiquidityWallZones(cc, cs, num5, H, dX, dY);
			}
			if (_pPassive)
			{
				DrawAdaptivePersistentWallsFinal(cc, cs, num5, H, dX, dY);
			}
			if (_pPassive && _pBasicVisual)
			{
				DrawPassiveLiquidityHeatmapEvents(cc, cs, num5, H, dX, dY);
			}
			if (flag)
			{
				IltDevPopWinClip();
			}
			if (_pLin && _pFlow)
			{
				DrawLiquidityHistogram(cs, num5, num, dX);
			}
			if (_pPoc)
			{
				DrawPOC(cs, cpW, num, dX, dY);
			}
			if (_midPx > 0.0)
			{
				DrawMid(cs, cpW, num, dX);
			}
			if (_pLad)
			{
				DrawLadder(cs, cpW, H, dX, dY);
			}
			if (flag)
			{
				IltDevPushWinClip(num6, cpW, H, dX);
			}
			if (_pPassive && _pBasicVisual)
			{
				DrawPassiveLiquidityLabelOverlay(cc, cs, num5, H, dX, dY);
			}
			if (flag)
			{
				IltDevPopWinClip();
			}
			if (_pPassive && _pSRHist)
			{
				DrawSRHistLevels(cc, cs, num5, H, dX, dY);
			}
			if (_pPassive && _pSRZones)
			{
				DrawSRZones(cc, cs, num5, H, dX, dY);
			}
			if (_pPassive && _pSRZones)
			{
				DrawIltDevSupportLevels(cc, cs, num5, H, dX, dY);
			}
			if (_pPassive && _pIltDevZones)
			{
				DrawDefendedWallLines(cc, cs, num5, H, dX, dY);
			}
			if (!UseWpfPanel)
			{
				DrawPanel(W, H, dX, dY);
			}
		}

		private float HeatmapPriorityOpacity(long qty, long localMax)
		{
			return HeatmapPriorityOpacity(qty, localMax, Math.Max(1L, _pF));
		}

		private float HeatmapPriorityOpacity(long qty, long localMax, long threshold)
		{
			if (qty <= 0 || localMax <= 0)
			{
				return 0f;
			}
			float num = OpacityPct(HeatmapOpacity);
			float num2 = Math.Min(num, OpacityPct(_pHeatmapMinOpacity));
			double num3 = Math.Max(1.0, threshold);
			double num4 = Math.Max(num3, localMax);
			if (qty < threshold)
			{
				return 0f;
			}
			if (num4 <= num3 + 1E-09)
			{
				return num2;
			}
			double num5 = HeatmapIntensityNorm(qty, localMax, threshold);
			return (float)((double)num2 + (double)(num - num2) * num5);
		}

		private double HeatmapIntensityNorm(long qty, long localMax, long threshold)
		{
			double num = Math.Max(1.0, threshold);
			double num2 = Math.Max(num, localMax);
			if (qty < threshold || num2 <= num + 1E-09)
			{
				return 0.0;
			}
			double num3 = Math.Max(num, Math.Min(num2, qty));
			double val = (_pHeatLogScale ? (Math.Log(1.0 + (num3 - num)) / Math.Log(1.0 + Math.Max(1.0, num2 - num))) : ((num3 - num) / Math.Max(1.0, num2 - num)));
			val = Math.Max(0.0, Math.Min(1.0, val));
			double num4 = Math.Max(50.0, Math.Min(400.0, _pHeatmapContrast));
			double y = Math.Max(0.45, Math.Min(2.75, num4 / 180.0));
			return Math.Pow(val, y);
		}

		private SharpDX.Direct2D1.SolidColorBrush HeatCellBrush(long qty, long localMax, long threshold, bool wasBid, out float opacity)
		{
			float num = OpacityPct(HeatmapOpacity);
			float num2 = Math.Min(num, OpacityPct(_pHeatmapMinOpacity));
			if (_pHeatColorMode <= 0 || _heatPal == null)
			{
				double num3 = HeatmapIntensityNorm(qty, localMax, threshold);
				opacity = (float)((double)num2 + (double)(num - num2) * num3);
				if (!wasBid)
				{
					return _bAsk;
				}
				return _bBid;
			}
			double num4 = HeatmapIntensityNorm(qty, localMax, threshold);
			int num5 = (int)Math.Round(num4 * 47.0);
			if (num5 < 0)
			{
				num5 = 0;
			}
			else if (num5 > 47)
			{
				num5 = 47;
			}
			opacity = (float)((double)num2 + (double)(num - num2) * (0.35 + 0.65 * Math.Sqrt(num4)));
			return _heatPal[num5];
		}

		private long StableHeatmapVisibleMax(long rawVisibleMax, long threshold)
		{
			long num = Math.Max(1L, threshold);
			long num2 = Math.Max(num, rawVisibleMax);
			int iltDevNowMs = IltDevNowMs;
			if (_heatmapStableMaxQty < num)
			{
				_heatmapStableMaxQty = num;
			}
			if (num2 >= _heatmapStableMaxQty)
			{
				_heatmapStableMaxQty = num2;
				_heatmapStableMaxLastMs = iltDevNowMs;
				return _heatmapStableMaxQty;
			}
			int num3 = iltDevNowMs - _heatmapStableMaxLastMs;
			if (num3 > 900)
			{
				double y = Math.Min(8.0, (double)num3 / 900.0);
				long val = (long)Math.Round((double)_heatmapStableMaxQty * Math.Pow(0.94, y));
				_heatmapStableMaxQty = Math.Max(num2, Math.Max(num, val));
				_heatmapStableMaxLastMs = iltDevNowMs;
			}
			return Math.Max(num2, _heatmapStableMaxQty);
		}

		private float GetHeatmapRowHeight(ChartScale cs, float dY, float scale)
		{
			if (cs == null)
			{
				return Math.Max(6f * dY, 6f) * Math.Max(0.5f, scale);
			}
			double num = (cs.MaxValue + cs.MinValue) * 0.5;
			double num2 = Math.Max(_tick, _tick * (double)Math.Max(1, _pG));
			float num3 = Math.Abs((float)cs.GetYByValue(num) - (float)cs.GetYByValue(num + num2));
			float val = Math.Max(6f * dY, 6f);
			float val2 = Math.Max(14f * dY, 14f);
			float num4 = Math.Max(val, Math.Min(val2, num3 * 1.18f));
			return num4 * Math.Max(0.55f, Math.Min(1.65f, scale));
		}

		private void DrawHeatmap(ChartControl cc, ChartScale cs, float cR, float H, float dX, float dY)
		{
			if (_hist == null || cc == null || cs == null || base.ChartBars == null || base.RenderTarget == null)
			{
				return;
			}
			int num = Math.Max(1, _pG);
			long num2 = Math.Max(1L, _pF);
			Math.Max(_tick, _tick * (double)num);
			double maxValue = cs.MaxValue;
			double minValue = cs.MinValue;
			float heatmapRowHeight = GetHeatmapRowHeight(cs, dY, 1f);
			int fromIndex = base.ChartBars.FromIndex;
			int toIndex = base.ChartBars.ToIndex;
			if (toIndex < fromIndex)
			{
				return;
			}
			int num3 = ((base.CurrentBar >= 0) ? base.CurrentBar : toIndex);
			int iltDevNowM = IltDevNowMs;
			float num4 = 2f * dX;
			if (toIndex > fromIndex)
			{
				num4 = Math.Max(2f * dX, cc.GetXByBarIndex(base.ChartBars, fromIndex + 1) - cc.GetXByBarIndex(base.ChartBars, fromIndex));
			}
			float num5 = Math.Max(50f, Math.Min(250f, _pHeatmapBarWidth));
			float num6 = ((num5 <= 100f) ? (num5 / 100f) : (0.96f + Math.Min(0.03f, (num5 - 100f) / 150f * 0.03f)));
			float val = Math.Max(1f * dX, num4 * num6);
			val = Math.Min(val, Math.Max(1f * dX, num4 * 0.99f));
			_heatCellBuf.Clear();
			lock (_lk)
			{
				for (int i = fromIndex; i <= toIndex; i++)
				{
					DomSnapHM6 value;
					if (i == num3 && _snap != null && _snap.HasData)
					{
						value = _snap;
					}
					else if (!_hist.TryGetValue(i, out value))
					{
						value = null;
					}
					if (value == null || !value.HasData)
					{
						continue;
					}
					foreach (KeyValuePair<double, long> item in value.Bid)
					{
						if (item.Value >= num2)
						{
							_heatCellBuf.Add(new HM6HeatCell
							{
								Bar = i,
								Price = item.Key,
								Qty = item.Value,
								IsBid = true
							});
						}
					}
					foreach (KeyValuePair<double, long> item2 in value.Ask)
					{
						if (item2.Value >= num2)
						{
							_heatCellBuf.Add(new HM6HeatCell
							{
								Bar = i,
								Price = item2.Key,
								Qty = item2.Value,
								IsBid = false
							});
						}
					}
				}
			}
			int count = _heatCellBuf.Count;
			if (count == 0)
			{
				return;
			}
			long num7 = num2;
			bool flag = false;
			int num8 = int.MinValue;
			bool flag2 = false;
			for (int j = 0; j < count; j++)
			{
				HM6HeatCell hM6HeatCell = _heatCellBuf[j];
				if (hM6HeatCell.Bar != num8)
				{
					num8 = hM6HeatCell.Bar;
					float num9 = cc.GetXByBarIndex(base.ChartBars, hM6HeatCell.Bar);
					flag2 = !(num9 > cR) && !(num9 < 0f - val);
				}
				if (!flag2)
				{
					continue;
				}
				float num10 = cs.GetYByValue(hM6HeatCell.Price);
				if (!(num10 + heatmapRowHeight < 0f) && !(num10 - heatmapRowHeight > H))
				{
					if (hM6HeatCell.Qty > num7)
					{
						num7 = hM6HeatCell.Qty;
					}
					flag = true;
				}
			}
			if (!flag)
			{
				return;
			}
			num7 = StableHeatmapVisibleMax(num7, num2);
			AntialiasMode antialiasMode = base.RenderTarget.AntialiasMode;
			base.RenderTarget.AntialiasMode = AntialiasMode.Aliased;
			float val2 = OpacityPct(HeatmapOpacity);
			float val3 = OpacityPct(_pHeatmapMinOpacity);
			int num11 = int.MinValue;
			bool flag3 = false;
			float x = 0f;
			float num12 = 1f;
			for (int k = 0; k < count; k++)
			{
				HM6HeatCell hM6HeatCell2 = _heatCellBuf[k];
				if (hM6HeatCell2.Bar != num11)
				{
					num11 = hM6HeatCell2.Bar;
					float num13 = cc.GetXByBarIndex(base.ChartBars, hM6HeatCell2.Bar);
					flag3 = !(num13 > cR) && !(num13 < 0f - val);
					x = num13 - val * 0.5f;
					num12 = 1f;
					if (hM6HeatCell2.Bar == num3 && num3 == _heatmapLiveBarIdx && _liveHeatmapSlices != null && _liveHeatmapSlices.Count > 0)
					{
						flag3 = false;
					}
				}
				if (!flag3)
				{
					continue;
				}
				float num14 = cs.GetYByValue(hM6HeatCell2.Price);
				if (!(num14 + heatmapRowHeight < 0f) && !(num14 - heatmapRowHeight > H))
				{
					float opacity;
					SharpDX.Direct2D1.SolidColorBrush solidColorBrush = HeatCellBrush(hM6HeatCell2.Qty, num7, num2, hM6HeatCell2.IsBid, out opacity);
					opacity *= num12;
					opacity = Math.Min(val2, Math.Max(val3, opacity));
					if (!(opacity < 0.02f))
					{
						solidColorBrush.Opacity = opacity;
						base.RenderTarget.FillRectangle(new RectangleF(x, num14 - heatmapRowHeight * 0.5f, val, heatmapRowHeight), solidColorBrush);
					}
				}
			}
			base.RenderTarget.AntialiasMode = antialiasMode;
		}

		private void DrawLiveHeatmapTape(ChartControl cc, ChartScale cs, float cR, float H, float dX, float dY)
		{
			if (cc == null || cs == null || base.ChartBars == null || base.RenderTarget == null)
			{
				return;
			}
			lock (_lk)
			{
				if (_liveHeatmapSlices == null || _liveHeatmapSlices.Count == 0)
				{
					return;
				}
			}
			float left;
			float right;
			float colW;
			int start;
			int count;
			List<HM6HeatmapSlice> slices;
			if (!_liveHeatmapDomReady || !TryGetLiveHeatmapLayout(cc, cR, dX, out left, out right, out colW, out start, out count, out slices))
			{
				return;
			}
			List<HM6HeatmapSlice> list = slices;
			if (list == null || count <= 0 || start < 0 || start >= list.Count)
			{
				return;
			}
			long num = Math.Max(1L, _pF);
			float heatmapRowHeight = GetHeatmapRowHeight(cs, dY, 1f);
			int num2 = Math.Min(list.Count, start + count);
			long num3 = ((num2 > start && list[num2 - 1] != null) ? list[num2 - 1].Seq : 0);
			int iltDevNowMs = IltDevNowMs;
			bool flag;
			long num4;
			if (num3 == _lvScanSeq && _lvScanMs != 0 && iltDevNowMs - _lvScanMs < 200)
			{
				num4 = _lvScanMax;
				flag = _lvScanHas;
			}
			else
			{
				num4 = num;
				flag = false;
				for (int i = start; i < num2; i++)
				{
					HM6HeatmapSlice hM6HeatmapSlice = list[i];
					if (hM6HeatmapSlice == null || !hM6HeatmapSlice.HasData)
					{
						continue;
					}
					foreach (KeyValuePair<double, long> item in hM6HeatmapSlice.Bid)
					{
						if (item.Value < num)
						{
							continue;
						}
						float num5 = cs.GetYByValue(item.Key);
						if (!(num5 + heatmapRowHeight < 0f) && !(num5 - heatmapRowHeight > H))
						{
							if (item.Value > num4)
							{
								num4 = item.Value;
							}
							flag = true;
						}
					}
					foreach (KeyValuePair<double, long> item2 in hM6HeatmapSlice.Ask)
					{
						if (item2.Value < num)
						{
							continue;
						}
						float num6 = cs.GetYByValue(item2.Key);
						if (!(num6 + heatmapRowHeight < 0f) && !(num6 - heatmapRowHeight > H))
						{
							if (item2.Value > num4)
							{
								num4 = item2.Value;
							}
							flag = true;
						}
					}
				}
				_lvScanSeq = num3;
				_lvScanMs = iltDevNowMs;
				_lvScanMax = num4;
				_lvScanHas = flag;
			}
			if (!flag)
			{
				return;
			}
			num4 = StableHeatmapVisibleMax(num4, num);
			AntialiasMode antialiasMode = base.RenderTarget.AntialiasMode;
			base.RenderTarget.AntialiasMode = AntialiasMode.Aliased;
			bool flag2 = false;
			if (_pHmScrollBmp && _pHeatColorMode > 0 && !_hmBmpFail)
			{
				try
				{
					flag2 = DrawLiveHeatmapCellsBitmap(cs, left, colW, start, count, list, num, num4, H);
				}
				catch
				{
					_hmBmpFail = true;
					flag2 = false;
				}
			}
			if (!flag2)
			{
				for (int j = 0; j < count; j++)
				{
					HM6HeatmapSlice hM6HeatmapSlice2 = list[start + j];
					if (hM6HeatmapSlice2 == null || !hM6HeatmapSlice2.HasData)
					{
						continue;
					}
					float x = left + (float)j * colW;
					float width = Math.Max(1.15f * dX, colW + 0.65f * dX);
					foreach (KeyValuePair<double, long> item3 in hM6HeatmapSlice2.Bid)
					{
						long value = item3.Value;
						if (value < num)
						{
							continue;
						}
						float num7 = cs.GetYByValue(item3.Key);
						if (!(num7 + heatmapRowHeight < 0f) && !(num7 - heatmapRowHeight > H))
						{
							float opacity;
							SharpDX.Direct2D1.SolidColorBrush solidColorBrush = HeatCellBrush(value, num4, num, true, out opacity);
							opacity = Math.Min(OpacityPct(HeatmapOpacity), Math.Max(OpacityPct(_pHeatmapMinOpacity), opacity));
							float num8 = Math.Min(0.58f, opacity * 0.72f);
							if (!(num8 < 0.02f))
							{
								solidColorBrush.Opacity = num8;
								base.RenderTarget.FillRectangle(new RectangleF(x, num7 - heatmapRowHeight * 0.5f, width, heatmapRowHeight), solidColorBrush);
							}
						}
					}
					foreach (KeyValuePair<double, long> item4 in hM6HeatmapSlice2.Ask)
					{
						long value2 = item4.Value;
						if (value2 < num)
						{
							continue;
						}
						float num9 = cs.GetYByValue(item4.Key);
						if (!(num9 + heatmapRowHeight < 0f) && !(num9 - heatmapRowHeight > H))
						{
							float opacity2;
							SharpDX.Direct2D1.SolidColorBrush solidColorBrush2 = HeatCellBrush(value2, num4, num, false, out opacity2);
							opacity2 = Math.Min(OpacityPct(HeatmapOpacity), Math.Max(OpacityPct(_pHeatmapMinOpacity), opacity2));
							float num10 = Math.Min(0.58f, opacity2 * 0.72f);
							if (!(num10 < 0.02f))
							{
								solidColorBrush2.Opacity = num10;
								base.RenderTarget.FillRectangle(new RectangleF(x, num9 - heatmapRowHeight * 0.5f, width, heatmapRowHeight), solidColorBrush2);
							}
						}
					}
					if (!_pPassive || !_pPassiveChartMarkers || hM6HeatmapSlice2.Events == null || hM6HeatmapSlice2.Events.Count <= 0)
					{
						continue;
					}
					int num11 = 0;
					int num12 = hM6HeatmapSlice2.Events.Count - 1;
					while (num12 >= 0 && num11 < 6)
					{
						HM6PassiveEvent hM6PassiveEvent = hM6HeatmapSlice2.Events[num12];
						if (hM6PassiveEvent != null && hM6PassiveEvent.Qty >= BasicVisualMinQty())
						{
							float num13 = cs.GetYByValue(hM6PassiveEvent.Price);
							if (!(num13 + heatmapRowHeight < 0f) && !(num13 - heatmapRowHeight > H))
							{
								num11++;
							}
						}
						num12--;
					}
				}
			}
			base.RenderTarget.AntialiasMode = antialiasMode;
		}

		private void DrawNativeHeatmapEventTag(HM6PassiveEvent ev, float anchorX, float y, float rowH, float cR, float dX, float dY)
		{
			if (ev == null || !ShouldRenderMicroEvent(ev) || base.RenderTarget == null || _bTxt == null || ev.PriceInteracted)
			{
				return;
			}
			string text = PassiveEventGlyph(ev);
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			if (ev.Qty > 0 && text.IndexOf(FQ(ev.Qty)) < 0 && Math.Abs(anchorX - _hoverX) <= 42f * dX && Math.Abs(y - _hoverY) <= 16f * dY)
			{
				text = text + " " + FQ(ev.Qty);
			}
			SharpDX.Direct2D1.SolidColorBrush solidColorBrush = PassiveEventBrush(ev);
			if (solidColorBrush != null)
			{
				int num = Math.Max(1, Math.Min(5, _pPassiveMarkerSize));
				float num2 = 0.8f + 0.18f * (float)num;
				bool flag = text.StartsWith("+") || text.StartsWith("-") || text.StartsWith("↑") || text.StartsWith("↓");
				TextFormat fmt = ((num >= 4) ? (_fSm ?? _fTiny) : (_fTiny ?? _fSm));
				float val = Math.Max(7f * dY, rowH * 0.92f * num2);
				val = Math.Min(val, Math.Max(8f * dY, rowH * 1.06f * num2));
				float val2 = (flag ? Math.Max(val * 1.85f, (float)text.Length * 5.7f * dX * num2 + 9f * dX) : Math.Max(val * 2.75f, (float)text.Length * 5.9f * dX * num2 + 10f * dX));
				val2 = Math.Min(val2, (flag ? (58f * dX) : (96f * dX)) * num2);
				float val3 = anchorX - val2 * 0.5f;
				val3 = Math.Max(255f * dX + 4f * dX, Math.Min(cR - val2 - 6f * dX, val3));
				float num3 = y - val * 0.5f;
				float num4 = Math.Max(1.4f * dX, val * 0.22f);
				if (_bPanBg != null)
				{
					_bPanBg.Opacity = 0.78f;
					base.RenderTarget.FillRoundedRectangle(new RoundedRectangle
					{
						Rect = new RectangleF(val3, num3, val2, val),
						RadiusX = num4,
						RadiusY = num4
					}, _bPanBg);
				}
				solidColorBrush.Opacity = ((ev.Type == 1) ? 0.94f : 0.88f);
				base.RenderTarget.DrawRoundedRectangle(new RoundedRectangle
				{
					Rect = new RectangleF(val3, num3, val2, val),
					RadiusX = num4,
					RadiusY = num4
				}, solidColorBrush, Math.Max(0.65f, 0.7f * dX));
				solidColorBrush.Opacity = ((ev.Type == 1) ? 0.82f : 0.62f);
				base.RenderTarget.FillRoundedRectangle(new RoundedRectangle
				{
					Rect = new RectangleF(val3, num3, Math.Max(2f, 2.2f * dX), val),
					RadiusX = num4,
					RadiusY = num4
				}, solidColorBrush);
				_bTxt.Opacity = 0.98f;
				TC(text, val3, num3 - 0.5f * dY, fmt, _bTxt, val2, val + 1f * dY);
			}
		}

		private bool TryGetLiveHeatmapLayout(ChartControl cc, float cR, float dX, out float left, out float right, out float colW, out int start, out int count, out List<HM6HeatmapSlice> slices)
		{
			left = (right = (colW = 0f));
			start = (count = 0);
			slices = null;
			if (cc == null || base.ChartBars == null || base.RenderTarget == null)
			{
				return false;
			}
			lock (_lk)
			{
				slices = ((_liveHeatmapSlices != null) ? new List<HM6HeatmapSlice>(_liveHeatmapSlices) : new List<HM6HeatmapSlice>());
			}
			if (slices == null || slices.Count == 0)
			{
				return false;
			}
			if (!_liveHeatmapDomReady)
			{
				return false;
			}
			int fromIndex = base.ChartBars.FromIndex;
			int toIndex = base.ChartBars.ToIndex;
			if (toIndex < fromIndex)
			{
				return false;
			}
			float num = 255f * dX + 2f;
			float num2 = Math.Max(num + 24f * dX, cR - 8f * dX);
			right = num2;
			left = Math.Max(num, RenderWinLeft(right));
			if (right - left < 24f * dX)
			{
				return false;
			}
			float num3 = Math.Max(24f * dX, right - left);
			int val = Math.Max(10, (int)Math.Floor(num3 / Math.Max(2f * dX, 2f)));
			count = Math.Min(slices.Count, val);
			if (count <= 0)
			{
				return false;
			}
			start = slices.Count - count;
			colW = Math.Max(1.15f * dX, num3 / (float)Math.Max(1, count));
			return true;
		}

		private bool TryGetLiveHeatmapPresenceIdxRange(List<HM6HeatmapSlice> slices, int start, int count, double price, bool wasBid, long minQty, out int first, out int last)
		{
			first = -1;
			last = -1;
			if (slices == null || count <= 0 || start < 0 || start >= slices.Count)
			{
				return false;
			}
			int num = Math.Min(slices.Count, start + count);
			long num2 = Math.Max(1L, minQty);
			for (int i = start; i < num; i++)
			{
				HM6HeatmapSlice hM6HeatmapSlice = slices[i];
				if (hM6HeatmapSlice == null)
				{
					continue;
				}
				Dictionary<double, long> dictionary = (wasBid ? hM6HeatmapSlice.Bid : hM6HeatmapSlice.Ask);
				long value;
				if (dictionary != null && dictionary.Count != 0 && dictionary.TryGetValue(price, out value) && value >= num2)
				{
					if (first < 0)
					{
						first = i;
					}
					last = i;
				}
			}
			if (first >= 0)
			{
				return last >= first;
			}
			return false;
		}

		private bool TryGetLiveHeatmapXRangeBySeqFast(List<HM6HeatmapSlice> slices, int start, int count, float left, float colW, long seq0, long seq1, out float x0, out float x1)
		{
			x0 = (x1 = 0f);
			if (slices == null || count <= 0 || start < 0 || start >= slices.Count)
			{
				return false;
			}
			if (seq0 <= 0 && seq1 <= 0)
			{
				return false;
			}
			if (seq1 > 0 && seq0 > 0 && seq1 < seq0)
			{
				long num = seq0;
				seq0 = seq1;
				seq1 = num;
			}
			int num2 = Math.Min(slices.Count, start + count);
			int num3 = -1;
			int num4 = -1;
			for (int i = start; i < num2; i++)
			{
				HM6HeatmapSlice hM6HeatmapSlice = slices[i];
				if (hM6HeatmapSlice == null)
				{
					continue;
				}
				long seq2 = hM6HeatmapSlice.Seq;
				int num5;
				if (seq0 <= 0 || seq1 <= 0)
				{
					num5 = (((seq0 <= 0) ? (seq2 <= seq1) : (seq2 >= seq0)) ? 1 : 0);
				}
				else
				{
					if (seq2 < seq0)
					{
						continue;
					}
					num5 = ((seq2 <= seq1) ? 1 : 0);
				}
				if (num5 != 0)
				{
					if (num3 < 0)
					{
						num3 = i;
					}
					num4 = i;
				}
			}
			if (num3 < 0 || num4 < num3)
			{
				return false;
			}
			x0 = left + (float)(num3 - start) * colW;
			x1 = left + (float)(num4 - start + 1) * colW;
			return true;
		}

		private bool TryGetLiveHeatmapXByTimeFast(List<HM6HeatmapSlice> slices, int start, int count, float left, float colW, double eventTime, out float x)
		{
			x = 0f;
			if (slices == null || count <= 0 || start < 0 || start >= slices.Count)
			{
				return false;
			}
			int num = Math.Min(slices.Count, start + count);
			int num2 = -1;
			double num3 = double.MaxValue;
			for (int i = start; i < num; i++)
			{
				HM6HeatmapSlice hM6HeatmapSlice = slices[i];
				if (hM6HeatmapSlice != null)
				{
					double num4 = Math.Abs(hM6HeatmapSlice.Time - eventTime);
					if (num4 < num3)
					{
						num3 = num4;
						num2 = i;
					}
				}
			}
			if (num2 < start || num3 > 15000.0)
			{
				return false;
			}
			x = left + (float)(num2 - start) * colW + colW * 0.5f;
			return true;
		}

		private void DrawLiquidityHistogram(ChartScale cs, float cR, float panX, float dX)
		{
			float num = ((base.RenderTarget != null) ? base.RenderTarget.Size.Height : 0f);
			if (num <= 0f)
			{
				return;
			}
			double num2 = (cs.MaxValue + cs.MinValue) * 0.5;
			float num3 = Math.Max(2f, Math.Abs((float)cs.GetYByValue(num2) - (float)cs.GetYByValue(num2 + GroupedPriceStep())));
			float num4 = Math.Min(145f * dX, Math.Max(86f * dX, (cR - panX) * 0.135f));
			float num5 = Math.Max(panX, cR - num4);
			num4 = Math.Max(50f * dX, cR - num5);
			float num6 = num5 + num4 * 0.5f;
			num6 = (float)Math.Round(num6) + 0.5f * dX;
			float num7 = Math.Max(16f * dX, Math.Min(num6 - num5, cR - num6) - 2f * dX);
			float num8 = Math.Max(1f * dX, 1f);
			float opacity = Math.Max(0.02f, OpacityPct(_pFlowPanelOpacity));
			_bPanBg.Opacity = opacity;
			base.RenderTarget.FillRectangle(new RectangleF(num5, 0f, num4, num), _bPanBg);
			_bLadSep.Opacity = 0.24f;
			base.RenderTarget.DrawLine(new Vector2(num5, 0f), new Vector2(num5, num), _bLadSep, 0.75f * dX);
			base.RenderTarget.DrawLine(new Vector2(cR, 0f), new Vector2(cR, num), _bLadSep, 0.75f * dX);
			_bDim.Opacity = 0.44f;
			base.RenderTarget.DrawLine(new Vector2(num6, 0f), new Vector2(num6, num), _bDim, 1f * dX);
			List<HM6Flow> flowRenderBuf = _flowRenderBuf;
			lock (_lk)
			{
				flowRenderBuf.Clear();
				if (_flows != null)
				{
					flowRenderBuf.AddRange(_flows);
				}
			}
			_bDim.Opacity = 0.64f;
			T("MARKET PULSE", num5 + 3f * dX, 4f * dX, _fSm, _bDim, num4 - 6f * dX, 14f * dX);
			if (!_pFlow || flowRenderBuf.Count == 0)
			{
				return;
			}
			double num9 = IltDevNowMs;
			double num10 = Math.Max(300.0, _pFlowFadeMs);
			long num11 = Math.Max(1L, _pFlowMin);
			long num12 = Math.Max(num11, _pFlowLabelMin);
			float num13 = OpacityPct(_pFlowOpacity);
			float num14 = 0.8f + 0.38f * (float)Math.Max(1, Math.Min(5, _pFlowSize));
			long num15 = 1L;
			foreach (HM6Flow item in flowRenderBuf)
			{
				if (item != null && item.Qty >= num11)
				{
					double num16 = num9 - item.Time;
					if (!(num16 < 0.0) && !(num16 > num10) && item.Qty > num15)
					{
						num15 = item.Qty;
					}
				}
			}
			num15 = Math.Max(1L, num15);
			float num17 = Math.Max(4.5f * dX, Math.Min(24f * dX, num3 * 1.15f * num14));
			foreach (HM6Flow item2 in flowRenderBuf)
			{
				if (item2 == null || item2.Qty < num11)
				{
					continue;
				}
				double num18 = num9 - item2.Time;
				if (num18 < 0.0 || num18 > num10)
				{
					continue;
				}
				double num19 = Math.Max(0.0, Math.Min(1.0, num18 / num10));
				float num20 = (float)Math.Pow(1.0 - num19, 0.58);
				if (num20 <= 0f)
				{
					continue;
				}
				float num21 = cs.GetYByValue(item2.Price);
				if (!(num21 < 0f - num17) && !(num21 > num + num17))
				{
					float num22 = (float)Math.Min((double)item2.Qty / (double)num15, 1.0);
					float num23 = (float)Math.Sqrt(num22);
					float num24 = (float)Math.Exp((0.0 - num18) / 420.0);
					float num25 = 1f;
					float num26 = 1f;
					float val = Math.Max(8f * dX, num7 * 0.14f);
					float val2 = Math.Max(val, num7 * 0.96f * num23 * num25);
					val2 = Math.Min(val2, num7 * 0.98f);
					float num27 = Math.Max(4.5f * dX, num17 * num26);
					float y = num21 - num27 * 0.5f;
					SharpDX.Direct2D1.SolidColorBrush solidColorBrush = (item2.WasBid ? _bPassiveBid : _bPassiveAsk);
					if (solidColorBrush == null)
					{
						solidColorBrush = (item2.IsAdd ? _bFlowAdd : _bFlowPull);
					}
					float num28 = num13 * num20 * (0.64f + 0.36f * num23);
					bool flag = _pDir && item2.WasBid;
					float x = (flag ? (num6 - val2) : (num6 + num8));
					solidColorBrush.Opacity = num28 * 0.08f;
					float num29 = Math.Min(num7, val2 + 10f * dX);
					float num30 = num27 + 2.5f * dX;
					RectangleF rect = (flag ? new RectangleF(num6 - num29, num21 - num30 * 0.5f, num29, num30) : new RectangleF(num6 + num8, num21 - num30 * 0.5f, num29, num30));
					base.RenderTarget.FillRectangle(rect, solidColorBrush);
					solidColorBrush.Opacity = num28;
					base.RenderTarget.FillRectangle(new RectangleF(x, y, val2, num27), solidColorBrush);
					solidColorBrush.Opacity = Math.Min(1f, num28 + 0.18f + num24 * 0.1f);
					float num31 = Math.Max(4f * dX, Math.Min(val2, 12f * dX * num14));
					if (flag)
					{
						base.RenderTarget.FillRectangle(new RectangleF(num6 - num31, y, num31, num27), solidColorBrush);
					}
					else
					{
						base.RenderTarget.FillRectangle(new RectangleF(num6 + num8, y, num31, num27), solidColorBrush);
					}
					solidColorBrush.Opacity = num28 * 0.4f;
					float num32 = Math.Min(num7 * 1.02f, val2 + 22f * dX * num20);
					float strokeWidth = Math.Max(1.1f * dX, Math.Min(3f * dX, 1f * dX + num23 * 2f * dX));
					if (flag)
					{
						base.RenderTarget.DrawLine(new Vector2(num6 - num32, num21), new Vector2(num6, num21), solidColorBrush, strokeWidth);
					}
					else
					{
						base.RenderTarget.DrawLine(new Vector2(num6 + num8, num21), new Vector2(num6 + num8 + num32, num21), solidColorBrush, strokeWidth);
					}
					if (item2.Qty >= num12 && num20 > 0.22f)
					{
						string text = (item2.IsAdd ? "+" : "-") + FQ(item2.Qty);
						float num33 = ((float)text.Length * 6f + 6f) * dX;
						float num34 = Math.Max(10f * dX, num27);
						_bFlowText.Opacity = Math.Min(1f, num20 * 1.1f);
						float x2 = (flag ? (num6 - val2 - num33 - 4f * dX) : (num6 + num8 + val2 + 4f * dX));
						T(text, x2, num21 - num34 * 0.5f, _fSm, _bFlowText, num33, num34);
					}
				}
			}
			_bDim.Opacity = 0.56f;
			base.RenderTarget.DrawLine(new Vector2(num6, 0f), new Vector2(num6, num), _bDim, 1.05f * dX);
		}

		private void InvalidateBubbleRenderCacheLocked()
		{
			_bubbleRenderCacheDirty = true;
		}

		private HM6BubbleRenderItem CreateBubbleItemFromTrade(HM6Trade tr, bool forceBlock)
		{
			if (tr == null)
			{
				return null;
			}
			HM6BubbleRenderItem hM6BubbleRenderItem = new HM6BubbleRenderItem();
			hM6BubbleRenderItem.Price = ((tr.RawPrice > 0.0) ? tr.RawPrice : tr.Price);
			hM6BubbleRenderItem.Vol = Math.Max(1L, tr.Vol);
			hM6BubbleRenderItem.MaxSingle = Math.Max(tr.MaxSingle, tr.Vol);
			hM6BubbleRenderItem.TradeCount = Math.Max(1, tr.TradeCount);
			hM6BubbleRenderItem.IsBuy = tr.IsBuy;
			hM6BubbleRenderItem.CreatedMs = tr.CreatedMs;
			hM6BubbleRenderItem.LastMs = tr.LastMs;
			hM6BubbleRenderItem.BarIdx = tr.BarIdx;
			hM6BubbleRenderItem.Seq = tr.Seq;
			hM6BubbleRenderItem.StampTicks = tr.StampTicks;
			hM6BubbleRenderItem.IsBlock = forceBlock || tr.IsBlock || Math.Max(tr.Vol, tr.MaxSingle) >= Math.Max(1, _pBtVisualMin);
			return hM6BubbleRenderItem;
		}

		private void EnsureBubbleRenderCacheLocked(int from, int to)
		{
			int num = Math.Max(1, _pBtVisualMin);
			int num2 = Math.Max(1, _pBtVisualMin);
			int num3 = Math.Max(1, _pG);
			int num4 = ((_trades != null) ? _trades.Count : 0);
			int num5 = ((_btArchive != null) ? _btArchive.Count : 0);
			if (!_bubbleRenderCacheDirty && _bubbleCacheFrom == from && _bubbleCacheTo == to && _bubbleCacheGroup == num3 && _bubbleCacheVisualMin == num && _bubbleCacheBlockMin == num2 && _bubbleCacheSeq == _tradeSeq && _bubbleCacheTradeCount == num4 && _bubbleCacheBtCount == num5)
			{
				return;
			}
			if (!_bubbleRenderCacheDirty && _bubbleRenderCache != null && _bubbleCacheFrom == from && _bubbleCacheTo == to && _bubbleCacheGroup == num3 && _bubbleCacheVisualMin == num && _bubbleCacheBlockMin == num2 && _tradeSeq > _bubbleCacheSeq && num4 >= _bubbleCacheTradeCount)
			{
				long bubbleCacheSeq = _bubbleCacheSeq;
				for (int num6 = _trades.Count - 1; num6 >= 0; num6--)
				{
					HM6Trade hM6Trade = _trades[num6];
					if (hM6Trade != null)
					{
						if (hM6Trade.Seq <= bubbleCacheSeq)
						{
							break;
						}
						if (hM6Trade.BarIdx >= from && hM6Trade.BarIdx <= to && (hM6Trade.Seq <= 0 || !_bubbleSeqCache.Contains(hM6Trade.Seq)))
						{
							AddBubbleTradeToCacheLocked(hM6Trade, num, num2, num3);
						}
					}
				}
				_bubbleCacheSeq = _tradeSeq;
				_bubbleCacheTradeCount = num4;
				_bubbleCacheBtCount = num5;
				_bubbleRenderCacheDirty = false;
				return;
			}
			if (_bubbleRenderCache == null)
			{
				_bubbleRenderCache = new List<HM6BubbleRenderItem>(1024);
			}
			else
			{
				_bubbleRenderCache.Clear();
			}
			if (_bubbleSeqCache == null)
			{
				_bubbleSeqCache = new HashSet<long>();
			}
			else
			{
				_bubbleSeqCache.Clear();
			}
			_bubbleCacheMaxVol = 1L;
			if (_trades != null && _trades.Count > 0)
			{
				if (_tradesByBar != null && _tradesByBar.Count > 0)
				{
					for (int i = from; i <= to; i++)
					{
						List<HM6Trade> value;
						if (_tradesByBar.TryGetValue(i, out value) && value != null)
						{
							AddBubbleBucketToCacheLocked(value, num, num2, num3);
						}
					}
				}
				else
				{
					for (int j = 0; j < _trades.Count; j++)
					{
						HM6Trade hM6Trade2 = _trades[j];
						if (hM6Trade2 != null && hM6Trade2.BarIdx >= from && hM6Trade2.BarIdx <= to)
						{
							AddBubbleTradeToCacheLocked(hM6Trade2, num, num2, num3);
						}
					}
				}
			}
			if (_btArchiveByBar != null && _btArchiveByBar.Count > 0)
			{
				for (int k = from; k <= to; k++)
				{
					List<HM6Trade> value2;
					if (!_btArchiveByBar.TryGetValue(k, out value2) || value2 == null)
					{
						continue;
					}
					for (int l = 0; l < value2.Count; l++)
					{
						HM6Trade hM6Trade3 = value2[l];
						if (hM6Trade3 == null || Math.Max(hM6Trade3.Vol, hM6Trade3.MaxSingle) < num2 || (hM6Trade3.Seq > 0 && _bubbleSeqCache.Contains(hM6Trade3.Seq)))
						{
							continue;
						}
						HM6BubbleRenderItem hM6BubbleRenderItem = CreateBubbleItemFromTrade(hM6Trade3, true);
						if (hM6BubbleRenderItem != null)
						{
							_bubbleRenderCache.Add(hM6BubbleRenderItem);
							if (hM6BubbleRenderItem.Seq > 0)
							{
								_bubbleSeqCache.Add(hM6BubbleRenderItem.Seq);
							}
							if (hM6BubbleRenderItem.Vol > _bubbleCacheMaxVol)
							{
								_bubbleCacheMaxVol = hM6BubbleRenderItem.Vol;
							}
						}
					}
				}
			}
			else if (_btArchive != null && _btArchive.Count > 0)
			{
				for (int m = 0; m < _btArchive.Count; m++)
				{
					HM6Trade hM6Trade4 = _btArchive[m];
					if (hM6Trade4 == null || hM6Trade4.BarIdx < from || hM6Trade4.BarIdx > to || Math.Max(hM6Trade4.Vol, hM6Trade4.MaxSingle) < num2 || (hM6Trade4.Seq > 0 && _bubbleSeqCache.Contains(hM6Trade4.Seq)))
					{
						continue;
					}
					HM6BubbleRenderItem hM6BubbleRenderItem2 = CreateBubbleItemFromTrade(hM6Trade4, true);
					if (hM6BubbleRenderItem2 != null)
					{
						_bubbleRenderCache.Add(hM6BubbleRenderItem2);
						if (hM6BubbleRenderItem2.Seq > 0)
						{
							_bubbleSeqCache.Add(hM6BubbleRenderItem2.Seq);
						}
						if (hM6BubbleRenderItem2.Vol > _bubbleCacheMaxVol)
						{
							_bubbleCacheMaxVol = hM6BubbleRenderItem2.Vol;
						}
					}
				}
			}
			_bubbleCacheFrom = from;
			_bubbleCacheTo = to;
			_bubbleCacheGroup = num3;
			_bubbleCacheVisualMin = num;
			_bubbleCacheBlockMin = num2;
			_bubbleCacheSeq = _tradeSeq;
			_bubbleCacheTradeCount = num4;
			_bubbleCacheBtCount = num5;
			_bubbleRenderCacheDirty = false;
		}

		private void AddBubbleBucketToCacheLocked(List<HM6Trade> bucket, int visualMin, int blockMin, int group)
		{
			if (bucket != null)
			{
				for (int i = 0; i < bucket.Count; i++)
				{
					AddBubbleTradeToCacheLocked(bucket[i], visualMin, blockMin, group);
				}
			}
		}

		private void AddBubbleTradeToCacheLocked(HM6Trade tr, int visualMin, int blockMin, int group)
		{
			if (tr == null)
			{
				return;
			}
			long num = Math.Max(tr.Vol, tr.MaxSingle);
			bool flag = tr.IsBlock || num >= blockMin;
			if (!flag && num < visualMin)
			{
				return;
			}
			HM6BubbleRenderItem hM6BubbleRenderItem = CreateBubbleItemFromTrade(tr, flag);
			if (hM6BubbleRenderItem != null)
			{
				_bubbleRenderCache.Add(hM6BubbleRenderItem);
				if (hM6BubbleRenderItem.Seq > 0)
				{
					_bubbleSeqCache.Add(hM6BubbleRenderItem.Seq);
				}
				if (hM6BubbleRenderItem.Vol > _bubbleCacheMaxVol)
				{
					_bubbleCacheMaxVol = hM6BubbleRenderItem.Vol;
				}
			}
		}

		private void DrawBubbles(ChartControl cc, ChartScale cs, float cR, float dX, float dY)
		{
			if (base.ChartBars == null)
			{
				return;
			}
			int fromIndex = base.ChartBars.FromIndex;
			int toIndex = base.ChartBars.ToIndex;
			if (toIndex < fromIndex)
			{
				return;
			}
			int num = Math.Max(0, fromIndex - 6);
			int to = toIndex + 6;
			long num2 = 1L;
			List<HM6BubbleRenderItem> bubbleRenderCache;
			lock (_lk)
			{
				if (_trades == null || _trades.Count == 0)
				{
					return;
				}
				EnsureBubbleRenderCacheLocked(num, to);
				if (_bubbleRenderCache == null || _bubbleRenderCache.Count == 0)
				{
					return;
				}
				bubbleRenderCache = _bubbleRenderCache;
				num2 = Math.Max(1L, _bubbleCacheMaxVol);
			}
			float num3 = Math.Max(dX, dY);
			float num4 = OpacityPct(BubbleOpacity);
			int iltDevNowMs = IltDevNowMs;
			int num5 = Math.Max(0, _pBubAnimMs);
			int num6 = Math.Max(1, _pBtVisualMin);
			float num7 = Math.Max(0f, Math.Min(1f, (float)_pBubGlow / 100f));
			float num8 = Math.Max(0f, Math.Min(1.25f, (float)_pBubPulse / 100f));
			float num9 = OpacityPct(BubbleBorderOpacity);
			float num10 = OpacityPct(BubbleHighlightOpacity);
			bool flag = _pBtFlash && BubbleOpacity > 0;
			int num11 = Math.Max(0, _pBtFlashMs);
			float num12 = Math.Max(0f, Math.Min(1f, (float)_pBtFlashStrength / 100f));
			float num13 = (flag ? (0.5f + 0.5f * (float)Math.Sin((double)iltDevNowMs * 0.0058)) : 0f);
			float num14 = (flag ? (0.5f + 0.5f * (float)Math.Sin((double)iltDevNowMs * 0.0135 + 1.1)) : 0f);
			int num15 = Math.Max(1, toIndex - fromIndex + 1);
			EnsureBubbleBarXCache(cc, fromIndex, toIndex, num15);
			float val = 8f * dX;
			try
			{
				if (num15 > 1 && _bubbleBarXCache != null)
				{
					val = Math.Abs(_bubbleBarXCache[1] - _bubbleBarXCache[0]);
				}
			}
			catch
			{
				val = 8f * dX;
			}
			val = Math.Max(1f * dX, val);
			if (_bubblePriceYCache == null)
			{
				_bubblePriceYCache = new Dictionary<int, float>(512);
			}
			else
			{
				_bubblePriceYCache.Clear();
			}
			int count = bubbleRenderCache.Count;
			bool flag2 = count > 900 || val < 7f * dX;
			bool flag3 = count > 2600 || val < 3.5f * dX;
			float num16 = (flag3 ? (2.3f * num3) : (flag2 ? (3.4f * num3) : (5.8f * num3)));
			float num17 = (flag3 ? (7f * num3) : (flag2 ? (10.2f * num3) : (15.2f * num3)));
			for (int i = 0; i < bubbleRenderCache.Count; i++)
			{
				HM6BubbleRenderItem hM6BubbleRenderItem = bubbleRenderCache[i];
				if (hM6BubbleRenderItem == null || hM6BubbleRenderItem.BarIdx < fromIndex - 2 || hM6BubbleRenderItem.BarIdx > toIndex + 2)
				{
					continue;
				}
				bool flag4 = hM6BubbleRenderItem.IsBlock || Math.Max(hM6BubbleRenderItem.Vol, hM6BubbleRenderItem.MaxSingle) >= num6;
				if (!flag4)
				{
					continue;
				}
				int fastBubbleRenderBar = GetFastBubbleRenderBar(hM6BubbleRenderItem, fromIndex, toIndex);
				if (fastBubbleRenderBar < fromIndex || fastBubbleRenderBar > toIndex)
				{
					continue;
				}
				int num18 = fastBubbleRenderBar - fromIndex;
				if (_bubbleBarXCache == null || num18 < 0 || num18 >= _bubbleBarXCache.Length)
				{
					continue;
				}
				float num19 = _bubbleBarXCache[num18];
				float num20 = num17 * 1.35f;
				if (num19 - num20 > cR || num19 + num20 < 0f)
				{
					continue;
				}
				int key = NormalizeToTickPriceKey(hM6BubbleRenderItem.Price);
				float value;
				if (!_bubblePriceYCache.TryGetValue(key, out value))
				{
					value = cs.GetYByValue(hM6BubbleRenderItem.Price);
					_bubblePriceYCache[key] = value;
				}
				double num21 = Math.Log(hM6BubbleRenderItem.Vol + 1);
				double val2 = Math.Log(num2 + 1);
				float val3 = (float)(num21 / Math.Max(1.0, val2));
				val3 = Math.Max(0f, Math.Min(1f, val3));
				float val4 = 0.32f + 0.68f * (float)Math.Pow(val3, 0.72);
				val4 = Math.Max(0.32f, Math.Min(1f, val4));
				float num22 = num16 + (num17 - num16) * val4;
				num22 = (flag4 ? Math.Min(num22 + 0.8f * num3, flag3 ? (7.6f * num3) : (flag2 ? (10.8f * num3) : (16.2f * num3))) : Math.Min(num22, flag3 ? (6.6f * num3) : (flag2 ? (9.4f * num3) : (12.8f * num3))));
				num19 += GetBubbleIntrabarOffset(hM6BubbleRenderItem, num18, val);
				num20 = Math.Max(num20, num22 * 1.6f);
				if (num19 - num20 > cR || num19 + num20 < 0f)
				{
					continue;
				}
				int num23 = ((hM6BubbleRenderItem.LastMs > 0) ? hM6BubbleRenderItem.LastMs : 0);
				float num24 = ((num5 > 0 && num23 > 0) ? Math.Max(0f, Math.Min(1f, (float)(iltDevNowMs - num23) / (float)num5)) : 1f);
				float num25 = 1f - num24;
				bool flag5 = num25 > 0.04f;
				float num28;
				if (flag5)
				{
					float num26 = num25;
					float num27 = num26 * num26 * (2.3f - 1.3f * num26);
					num28 = num27;
				}
				else
				{
					num28 = 0f;
				}
				float num29 = num25 * num25;
				float num30 = num22 * (1f + num8 * 0.18f * num28);
				float num31 = 0f;
				float num32 = 0f;
				if (flag && flag4 && num11 > 0 && num23 > 0)
				{
					int num33 = iltDevNowMs - num23;
					if (num33 >= 0 && num33 <= num11)
					{
						float num34 = 1f - (float)num33 / (float)num11;
						num34 = num34 * num34 * (3f - 2f * num34);
						num31 = num34 * num12 * (0.45f + 0.55f * num13);
						num32 = num34 * num12 * 0.1f * (0.4f + 0.6f * num13);
					}
				}
				num30 *= 1f + num32;
				SharpDX.Direct2D1.SolidColorBrush solidColorBrush = (hM6BubbleRenderItem.IsBuy ? _bBubBuy : _bBubSell);
				Ellipse ellipse = new Ellipse(new Vector2(num19, value), num30, num30);
				if (_bPanSh != null && !flag3)
				{
					_bPanSh.Opacity = (flag4 ? 0.2f : 0.1f) * num4;
					base.RenderTarget.FillEllipse(new Ellipse(new Vector2(num19 + num30 * 0.1f, value + num30 * 0.13f), num30 * 0.88f, num30 * 0.74f), _bPanSh);
				}
				if (!flag3 && num7 > 0.01f && (flag4 || flag5))
				{
					float num35 = num30 * (1.08f + 0.14f * num29 + 0.1f * num31);
					solidColorBrush.Opacity = (flag4 ? 0.08f : 0.05f) * num7 * num4 + 0.08f * num29 * num4 + 0.14f * num31 * num4;
					base.RenderTarget.FillEllipse(new Ellipse(new Vector2(num19, value), num35, num35), solidColorBrush);
					if (_pBubShockwave && flag4 && num25 > 0.08f)
					{
						float num36 = num30 * (1.12f + 0.44f * num24);
						solidColorBrush.Opacity = 0.2f * num25 * num7 * num4;
						base.RenderTarget.DrawEllipse(new Ellipse(new Vector2(num19, value), num36, num36), solidColorBrush, Math.Max(0.65f * dX, 0.8f));
					}
				}
				float num37 = (flag4 ? 0.64f : 0.36f) * num4;
				num37 = Math.Min(1f, num37 + num29 * 0.08f * num4);
				solidColorBrush.Opacity = num37;
				base.RenderTarget.FillEllipse(ellipse, solidColorBrush);
				if (_bPanSh != null && !flag3)
				{
					_bPanSh.Opacity = (flag4 ? 0.13f : 0.07f) * num4;
					base.RenderTarget.FillEllipse(new Ellipse(new Vector2(num19 + num30 * 0.1f, value + num30 * 0.14f), num30 * 0.64f, num30 * 0.56f), _bPanSh);
				}
				float num38 = num30 * 0.66f;
				solidColorBrush.Opacity = (flag4 ? 0.2f : 0.12f) * num4;
				base.RenderTarget.FillEllipse(new Ellipse(new Vector2(num19 - num30 * 0.02f, value - num30 * 0.03f), num38, num38), solidColorBrush);
				if (!flag3 && num30 >= 7f * num3 && _bBubBord != null)
				{
					float num39 = num30 * (flag4 ? 0.27f : 0.22f);
					_bBubBord.Opacity = (0.1f + 0.1f * num29) * num7 * num4 * num10;
					base.RenderTarget.FillEllipse(new Ellipse(new Vector2(num19 - num30 * 0.28f, value - num30 * 0.32f), num39, num39 * 0.66f), _bBubBord);
					if (flag4)
					{
						_bBubBord.Opacity = (0.18f + 0.1f * num29) * num4 * num10;
						base.RenderTarget.DrawLine(new Vector2(num19 - num30 * 0.42f, value - num30 * 0.16f), new Vector2(num19 - num30 * 0.08f, value - num30 * 0.34f), _bBubBord, Math.Max(0.65f * dX, 0.8f));
						if (num31 > 0.01f)
						{
							float num40 = num30 * (0.16f + 0.05f * num14);
							_bBubBord.Opacity = Math.Min(1f, (0.35f + 0.45f * num14) * num31 * num4);
							base.RenderTarget.FillEllipse(new Ellipse(new Vector2(num19 - num30 * 0.3f, value - num30 * 0.34f), num40, num40 * 0.82f), _bBubBord);
						}
					}
				}
				float num41 = (flag4 ? 0.76f : 0.38f) * num4 * num9;
				float val5 = (flag4 ? 1.45f : 0.72f) * dX;
				_bBubBord.Opacity = Math.Min(1f, num41 + num29 * 0.08f);
				base.RenderTarget.DrawEllipse(ellipse, _bBubBord, Math.Max(0.65f, val5));
				if (!flag3 && flag4 && _bBubFlash != null)
				{
					float opacity = Math.Min(0.7f, (0.1f + 0.16f * num25 + 0.4f * num31) * num4);
					_bBubFlash.Opacity = opacity;
					float num42 = num30 * (1.06f + 0.07f * num31);
					base.RenderTarget.DrawEllipse(new Ellipse(new Vector2(num19, value), num42, num42), _bBubFlash, Math.Max(0.8f * dX, 0.95f) * (1f + 0.7f * num31));
					if (num31 > 0.02f)
					{
						float num43 = 0.5f + 0.5f * (float)Math.Sin((double)iltDevNowMs * 0.0058 - 0.6);
						float num44 = num30 * (1.1f + 0.55f * num43);
						_bBubFlash.Opacity = Math.Min(0.55f, 0.42f * num31 * (1f - num43) * num4);
						base.RenderTarget.DrawEllipse(new Ellipse(new Vector2(num19, value), num44, num44), _bBubFlash, Math.Max(0.85f * dX, 1f));
					}
				}
				if (!flag2 && _pBubLightning && flag4 && num30 >= 11f * num3 && num25 > 0.1f)
				{
					DrawBubbleLightning(num19, value, num30, dX, num25, num4);
				}
				if (!flag3 && num30 >= 10.5f * num3 && flag4)
				{
					string text = FQ(hM6BubbleRenderItem.Vol);
					float num45 = Math.Max(24f * dX, ((float)text.Length * 5.8f + 6f) * dX);
					float num46 = Math.Max(10f * dY, 12f * num3);
					if (_bPanSh != null)
					{
						_bPanSh.Opacity = 0.16f * num4;
						base.RenderTarget.FillRoundedRectangle(new RoundedRectangle
						{
							Rect = new RectangleF(num19 - num45 * 0.5f, value - num46 * 0.5f, num45, num46),
							RadiusX = num46 * 0.42f,
							RadiusY = num46 * 0.42f
						}, _bPanSh);
					}
					_bTxt.Opacity = 0.96f;
					TC(text, num19 - num45 * 0.5f, value - num46 * 0.5f, _fSm, _bTxt, num45, num46);
				}
			}
		}

		private void EnsureBubbleBarXCache(ChartControl cc, int b0, int b1, int visibleBars)
		{
			if (cc == null || visibleBars <= 0)
			{
				return;
			}
			if (_bubbleBarXCache == null || _bubbleBarXCache.Length < visibleBars)
			{
				_bubbleBarXCache = new float[Math.Max(visibleBars, 256)];
			}
			for (int i = 0; i < visibleBars; i++)
			{
				_bubbleBarXCache[i] = cc.GetXByBarIndex(base.ChartBars, b0 + i);
			}
			if (_bubbleBarTCache == null || _bubbleBarTCache.Length < visibleBars + 1)
			{
				_bubbleBarTCache = new long[Math.Max(visibleBars + 1, 257)];
				_bubbleBarTCacheB0 = int.MinValue;
			}
			long num = base.CurrentBar;
			if (_bubbleBarTCacheB0 == b0 && _bubbleBarTCacheCount == visibleBars && _bubbleBarTCacheTail == num)
			{
				return;
			}
			try
			{
				_bubbleBarTCache[0] = ((b0 > 0) ? base.Times[0].GetValueAt(b0 - 1).Ticks : 0);
				for (int j = 0; j < visibleBars; j++)
				{
					_bubbleBarTCache[j + 1] = base.Times[0].GetValueAt(b0 + j).Ticks;
				}
				int currentBar = base.CurrentBar;
				if (currentBar >= b0 && currentBar <= b1 && currentBar >= 4)
				{
					int num2 = currentBar - b0;
					if (num2 >= 0 && num2 + 1 < _bubbleBarTCache.Length)
					{
						long num3 = 0L;
						int num4 = 0;
						for (int k = 1; k <= 3; k++)
						{
							long ticks = base.Times[0].GetValueAt(currentBar - k).Ticks;
							long ticks2 = base.Times[0].GetValueAt(currentBar - k - 1).Ticks;
							if (ticks > ticks2)
							{
								num3 += ticks - ticks2;
								num4++;
							}
						}
						long num5 = ((num4 > 0) ? (num3 / num4) : 0);
						if (num5 < 10000000)
						{
							num5 = 10000000L;
						}
						long num6 = _bubbleBarTCache[num2];
						if (num6 > 0)
						{
							_bubbleBarTCache[num2 + 1] = num6 + num5;
						}
					}
				}
				_bubbleBarTCacheB0 = b0;
				_bubbleBarTCacheCount = visibleBars;
				_bubbleBarTCacheTail = num;
			}
			catch
			{
				for (int l = 0; l <= visibleBars && l < _bubbleBarTCache.Length; l++)
				{
					_bubbleBarTCache[l] = 0L;
				}
				_bubbleBarTCacheB0 = int.MinValue;
			}
		}

		private float GetBubbleIntrabarOffset(HM6BubbleRenderItem tr, int xSlot, float barStepW)
		{
			if (tr == null || barStepW <= 2.5f)
			{
				return 0f;
			}
			if (_bubbleBarTCache == null || xSlot < 0 || xSlot + 1 >= _bubbleBarTCache.Length)
			{
				return 0f;
			}
			if (tr.StampTicks <= 0)
			{
				return 0f;
			}
			long num = _bubbleBarTCache[xSlot];
			long num2 = _bubbleBarTCache[xSlot + 1];
			if (num <= 0 || num2 <= num)
			{
				return 0f;
			}
			double num3 = (double)(tr.StampTicks - num) / (double)(num2 - num);
			if (num3 < 0.0)
			{
				num3 = 0.0;
			}
			else if (num3 > 1.0)
			{
				num3 = 1.0;
			}
			return (float)((num3 - 0.5) * (double)barStepW * 0.76);
		}

		private int GetFastBubbleRenderBar(HM6BubbleRenderItem tr, int b0, int b1)
		{
			if (tr == null || tr.BarIdx < 0)
			{
				return -1;
			}
			if (tr.BarIdx >= b0 && tr.BarIdx <= b1)
			{
				return tr.BarIdx;
			}
			return -1;
		}

		private void DrawBubbleLightning(float x, float y, float rad, float dX, float live, float globalOp)
		{
			if (_bBubFlash != null)
			{
				float opacity = Math.Min(0.62f, (0.3f + 0.22f * live) * globalOp);
				_bBubFlash.Opacity = opacity;
				float strokeWidth = Math.Max(0.75f * dX, rad * 0.034f);
				Vector2 point = new Vector2(x - rad * 0.36f, y + rad * 0.2f);
				Vector2 vector = new Vector2(x - rad * 0.08f, y - rad * 0.02f);
				Vector2 vector2 = new Vector2(x + rad * 0.04f, y + rad * 0.02f);
				Vector2 point2 = new Vector2(x + rad * 0.36f, y - rad * 0.24f);
				base.RenderTarget.DrawLine(point, vector, _bBubFlash, strokeWidth);
				base.RenderTarget.DrawLine(vector, vector2, _bBubFlash, strokeWidth);
				base.RenderTarget.DrawLine(vector2, point2, _bBubFlash, strokeWidth);
				if (_bBubBord != null)
				{
					_bBubBord.Opacity = 0.16f * globalOp * OpacityPct(BubbleBorderOpacity);
					base.RenderTarget.DrawLine(new Vector2(point.X, point.Y - Math.Max(1f, dX)), new Vector2(point2.X, point2.Y - Math.Max(1f, dX)), _bBubBord, Math.Max(0.4f * dX, 0.55f));
				}
			}
		}

		private void DrawPOC(ChartScale cs, float cR, float panX, float dX, float dY)
		{
			if (_passivePocVol > 0 && !(_passivePocPrice <= 0.0))
			{
				float num = cs.GetYByValue(_passivePocPrice);
				if (!(num < -20f) && !(num > base.RenderTarget.Size.Height + 20f))
				{
					float strokeWidth = Math.Max(1.2f * dX, 1f);
					_bPoc.Opacity = 0.86f;
					base.RenderTarget.DrawLine(new Vector2(panX, num), new Vector2(cR - 2f * dX, num), _bPoc, strokeWidth, _stDash);
					string text = "DOM POC " + FQ(_passivePocVol);
					float num2 = Math.Max(62f * dX, ((float)text.Length * 6.3f + 10f) * dX);
					float num3 = 14f * dY;
					float num4 = panX + 2f * dX;
					float num5 = num - num3 * 0.5f;
					_bPoc.Opacity = 0.92f;
					base.RenderTarget.FillRectangle(new RectangleF(num4, num5, num2, num3), _bPoc);
					_bPanBg.Opacity = 1f;
					T(text, num4 + 3f * dX, num5 + 1f * dY, _fSm, _bPanBg, num2, num3);
				}
			}
		}

		private void DrawMid(ChartScale cs, float cR, float panX, float dX)
		{
			float num = cs.GetYByValue(_midPx);
			_bMid.Opacity = 0.68f;
			base.RenderTarget.DrawLine(new Vector2(panX, num), new Vector2(cR - 2f, num), _bMid, 1f * dX, _stDash);
			string text = _midPx.ToString("F" + _decs);
			float num2 = ((float)text.Length * 7f + 12f) * dX;
			float num3 = 15f;
			float num4 = cR - num2 - 2f;
			_bMid.Opacity = 0.86f;
			base.RenderTarget.FillRectangle(new RectangleF(num4, num - num3 * 0.5f, num2, num3), _bMid);
			_bPanBg.Opacity = 1f;
			T(text, num4 + 4f * dX, num - num3 * 0.5f + 1f, _fSm, _bPanBg, num2, num3);
		}

		private string PassiveEventGlyph(HM6PassiveEvent ev)
		{
			if (ev == null)
			{
				return "";
			}
			bool flag = ev.Qty >= InstitutionalMinQty();
			string text = ((flag && ev.Qty > 0) ? (" " + FQ(ev.Qty)) : "");
			if (ev.Type == 2)
			{
				return (flag ? "◆ REFILL" : "◆") + text;
			}
			if (ev.Type == 5)
			{
				return "ABS" + text;
			}
			if (ev.Type == 4)
			{
				return "STACK" + text;
			}
			if (ev.Type == 1)
			{
				return (ev.WasBid ? "↑" : "↓") + text;
			}
			if (ev.Type == 3)
			{
				return "+" + text;
			}
			return "PULL" + text;
		}

		private bool ShouldRenderMicroEvent(HM6PassiveEvent ev)
		{
			if (ev == null)
			{
				return false;
			}
			if (ev.Type == 0)
			{
				return _pPull;
			}
			if (ev.Type == 3 || ev.Type == 4)
			{
				return _pSt;
			}
			if (ev.Type == 5)
			{
				return _pAbs;
			}
			return true;
		}

		private SharpDX.Direct2D1.SolidColorBrush PassiveEventBrush(HM6PassiveEvent ev)
		{
			if (ev == null)
			{
				return _bPassiveWarn;
			}
			if (ev.Type == 2)
			{
				return _bPassiveRefill;
			}
			if (ev.Type == 5)
			{
				return _bAbs;
			}
			if (ev.Type == 4)
			{
				return _bFlowAdd;
			}
			if (ev.Type == 1)
			{
				return _bPassiveWarn;
			}
			if (ev.Type == 3)
			{
				return _bFlowAdd;
			}
			return _bPull;
		}

		private string PassiveZoneLabel(HM6PassiveZone z)
		{
			if (z == null)
			{
				return "";
			}
			string text = (IsPassiveSpoofZone(z) ? "SPOOF" : (z.IcebergConfirmed ? "ICEBERG" : ((z.LastEventType == 2) ? "REFILL" : ((z.LastEventType != 1) ? ((z.LastEventType != 0) ? "+" : "-") : (z.WasBid ? "↑" : "↓")))));
			long num = PassiveZoneDisplayQty(z, text);
			if (num <= 0)
			{
				return text;
			}
			return text + " " + FQ(num);
		}

		private long PassiveZoneDisplayQty(HM6PassiveZone z, string baseLabel)
		{
			if (z == null)
			{
				return 0L;
			}
			long num = ((z.LastEventQty > 0) ? z.LastEventQty : Math.Max(0L, Math.Abs(z.LastQty)));
			switch (baseLabel)
			{
			case "SPOOF":
				return Math.Max(num, Math.Max(z.PullQty, (z.MaxQty > z.LastQty) ? (z.MaxQty - z.LastQty) : 0));
			case "ICEBERG":
				if (z.IcebergQty <= 0)
				{
					return Math.Max(num, Math.Max(z.RefillQty, z.ConsumeQty));
				}
				return z.IcebergQty;
			case "REFILL":
				return Math.Max(num, z.RefillQty);
			case "↑":
			case "↓":
				return Math.Max(num, z.ConsumeQty);
			case "-":
				return Math.Max(num, z.PullQty);
			case "+":
				return Math.Max(num, (z.AddQty > 0) ? z.LastEventQty : z.LastQty);
			default:
				return num;
			}
		}

		private bool IsPassiveSpoofZone(HM6PassiveZone z)
		{
			if (z != null && !z.SpoofLabelInteracted)
			{
				return IsPassiveSpoofZoneRaw(z);
			}
			return false;
		}

		private bool IsPassiveSpoofZoneRaw(HM6PassiveZone z)
		{
			if (z == null)
			{
				return false;
			}
			bool flag = z.PullBurstLast > 0.0 && (double)IltDevNowMs - z.PullBurstLast <= 3000.0;
			if (z.LastEventType != 0 && !flag)
			{
				return false;
			}
			double num = Math.Max(1.0, _pPassiveEventMin);
			double num2 = Math.Max(0.0, z.PullQty);
			double num3 = Math.Max(0.0, z.ConsumeQty);
			double num4 = Math.Max(0.0, z.RefillQty);
			double num5 = Math.Max(1.0, z.MaxQty);
			if (z.Reached || z.TouchCount > 0 || num3 >= num)
			{
				return false;
			}
			if (z.MaxQty < InstitutionalMinQty())
			{
				return false;
			}
			double num6 = ((z.LastUpdateTime > z.FirstTime) ? (z.LastUpdateTime - z.FirstTime) : 0.0);
			if (num6 < 1500.0)
			{
				return false;
			}
			if (z.PullBurstQty < (long)Math.Round(num5 * 0.5) || !(z.PullBurstLast > z.PullBurstStart - 1.0) || !(z.PullBurstLast - z.PullBurstStart <= 4000.0))
			{
				return false;
			}
			if (z.PullNearTicks > Math.Max(4.0, _pSpoofNearTicks))
			{
				return false;
			}
			double num7 = Math.Max(0.0, z.LastQty);
			double num8 = Math.Max(_pPassiveMinQty, num5 * 0.2);
			if ((!z.Active || num7 <= num8) && num2 >= Math.Max(num, num5 * 0.5) && num4 < Math.Max(num, num2 * 0.25))
			{
				return num3 < Math.Max(num, num2 * 0.35);
			}
			return false;
		}

		private void AppendLiveHeatmapDerivedWallZones(ChartControl cc, ChartScale cs, float cR, float H, float dX, float dY, List<HM6PassiveZone> zones)
		{
			float left;
			float right;
			float colW;
			int start;
			int count;
			List<HM6HeatmapSlice> slices;
			if (!_pPassive || !_pPassiveWallZones || zones == null || cc == null || cs == null || base.RenderTarget == null || !TryGetLiveHeatmapLayout(cc, cR, dX, out left, out right, out colW, out start, out count, out slices) || slices == null || count <= 0)
			{
				return;
			}
			long minQty = Math.Max(1L, _pWallMinQty);
			long evtMin = Math.Max(1L, _pPassiveEventMin);
			long wallQtyMin = Math.Max(minQty * 4, evtMin * 3);
			int minPresence = Math.Max(6, Math.Min(36, count / 10));
			int num = Math.Min(slices.Count, start + count);
			Dictionary<string, HM6PassiveZone> dictionary = new Dictionary<string, HM6PassiveZone>();
			for (int i = 0; i < zones.Count; i++)
			{
				HM6PassiveZone hM6PassiveZone = zones[i];
				if (hM6PassiveZone != null)
				{
					string key = PassiveZoneKey(hM6PassiveZone.Price, hM6PassiveZone.WasBid);
					if (!dictionary.ContainsKey(key))
					{
						dictionary[key] = hM6PassiveZone;
					}
				}
			}
			Dictionary<string, HM6PassiveZone> dictionary2 = new Dictionary<string, HM6PassiveZone>();
			for (int j = start; j < num; j++)
			{
				HM6HeatmapSlice hM6HeatmapSlice = slices[j];
				if (hM6HeatmapSlice == null)
				{
					continue;
				}
				if (hM6HeatmapSlice.Bid != null)
				{
					foreach (KeyValuePair<double, long> item in hM6HeatmapSlice.Bid)
					{
						long value = item.Value;
						if (value < minQty)
						{
							continue;
						}
						float num2 = cs.GetYByValue(item.Key);
						if (!(num2 < -30f * dY) && !(num2 > H + 30f * dY))
						{
							string key2 = PassiveZoneKey(item.Key, true);
							HM6PassiveZone value2;
							if (!dictionary2.TryGetValue(key2, out value2) || value2 == null)
							{
								value2 = new HM6PassiveZone();
								value2.Price = item.Key;
								value2.WasBid = true;
								value2.StartBar = ((hM6HeatmapSlice.BarIdx >= 0) ? hM6HeatmapSlice.BarIdx : PrimaryBarIndexSafe());
								value2.FirstTime = hM6HeatmapSlice.Time;
								value2.FirstSeq = hM6HeatmapSlice.Seq;
								value2.LastEventType = 3;
								value2.Active = true;
								value2.DerivedWall = true;
								dictionary2[key2] = value2;
							}
							value2.LastBar = ((hM6HeatmapSlice.BarIdx >= 0) ? hM6HeatmapSlice.BarIdx : value2.StartBar);
							value2.LastUpdateTime = hM6HeatmapSlice.Time;
							value2.LastSeq = hM6HeatmapSlice.Seq;
							value2.LastQty = value;
							if (value > value2.MaxQty)
							{
								value2.MaxQty = value;
							}
							value2.AddQty += value;
							value2.PresenceCount++;
							value2.EventCount = value2.PresenceCount;
							value2.LastEventQty = value2.MaxQty;
						}
					}
				}
				if (hM6HeatmapSlice.Ask == null)
				{
					continue;
				}
				foreach (KeyValuePair<double, long> item2 in hM6HeatmapSlice.Ask)
				{
					long value3 = item2.Value;
					if (value3 < minQty)
					{
						continue;
					}
					float num3 = cs.GetYByValue(item2.Key);
					if (!(num3 < -30f * dY) && !(num3 > H + 30f * dY))
					{
						string key3 = PassiveZoneKey(item2.Key, false);
						HM6PassiveZone value4;
						if (!dictionary2.TryGetValue(key3, out value4) || value4 == null)
						{
							value4 = new HM6PassiveZone();
							value4.Price = item2.Key;
							value4.WasBid = false;
							value4.StartBar = ((hM6HeatmapSlice.BarIdx >= 0) ? hM6HeatmapSlice.BarIdx : PrimaryBarIndexSafe());
							value4.FirstTime = hM6HeatmapSlice.Time;
							value4.FirstSeq = hM6HeatmapSlice.Seq;
							value4.LastEventType = 3;
							value4.Active = true;
							value4.DerivedWall = true;
							dictionary2[key3] = value4;
						}
						value4.LastBar = ((hM6HeatmapSlice.BarIdx >= 0) ? hM6HeatmapSlice.BarIdx : value4.StartBar);
						value4.LastUpdateTime = hM6HeatmapSlice.Time;
						value4.LastSeq = hM6HeatmapSlice.Seq;
						value4.LastQty = value3;
						if (value3 > value4.MaxQty)
						{
							value4.MaxQty = value3;
						}
						value4.AddQty += value3;
						value4.PresenceCount++;
						value4.EventCount = value4.PresenceCount;
						value4.LastEventQty = value4.MaxQty;
					}
				}
			}
			List<HM6PassiveZone> list = (from z in dictionary2.Values
				where z != null && (z.MaxQty >= wallQtyMin || (z.PresenceCount >= minPresence && z.MaxQty >= minQty * 2))
				orderby z.MaxQty + z.PresenceCount * evtMin descending
				select z).Take(80).ToList();
			for (int num4 = 0; num4 < list.Count; num4++)
			{
				HM6PassiveZone hM6PassiveZone2 = list[num4];
				string key4 = PassiveZoneKey(hM6PassiveZone2.Price, hM6PassiveZone2.WasBid);
				HM6PassiveZone value5;
				if (dictionary.TryGetValue(key4, out value5) && value5 != null)
				{
					value5.DerivedWall = true;
					value5.PresenceCount = Math.Max(value5.PresenceCount, hM6PassiveZone2.PresenceCount);
					if (hM6PassiveZone2.MaxQty > value5.MaxQty)
					{
						value5.MaxQty = hM6PassiveZone2.MaxQty;
					}
					if (value5.FirstSeq <= 0 || hM6PassiveZone2.FirstSeq < value5.FirstSeq)
					{
						value5.FirstSeq = hM6PassiveZone2.FirstSeq;
					}
					if (hM6PassiveZone2.LastSeq > value5.LastSeq)
					{
						value5.LastSeq = hM6PassiveZone2.LastSeq;
					}
					if (hM6PassiveZone2.LastUpdateTime > value5.LastUpdateTime)
					{
						value5.LastUpdateTime = hM6PassiveZone2.LastUpdateTime;
					}
					if (value5.LastEventQty <= 0)
					{
						value5.LastEventQty = hM6PassiveZone2.MaxQty;
					}
					if (value5.EventCount < hM6PassiveZone2.PresenceCount)
					{
						value5.EventCount = hM6PassiveZone2.PresenceCount;
					}
				}
				else
				{
					zones.Add(hM6PassiveZone2);
					dictionary[key4] = hM6PassiveZone2;
				}
			}
		}

		private void SyncAdaptiveWallCandidatesToPassiveZonesLocked(List<HM6WallCandidate> selected, double now)
		{
			if (!_pPassive || !PassiveZoneEngineRequired || selected == null || selected.Count == 0)
			{
				return;
			}
			if (_passiveZones == null)
			{
				_passiveZones = new Dictionary<string, HM6PassiveZone>();
			}
			int num = PrimaryBarIndexSafe();
			long num2 = Math.Max(1L, _liveHeatmapSeq + 1);
			long num3 = Math.Max(1L, _pWallMinQty);
			long num4 = Math.Max(1L, _pPassiveEventMin);
			for (int i = 0; i < selected.Count; i++)
			{
				HM6WallCandidate hM6WallCandidate = selected[i];
				if (hM6WallCandidate == null || hM6WallCandidate.Price <= 0.0 || hM6WallCandidate.MaxQty < num3)
				{
					continue;
				}
				string key = PassiveZoneKey(hM6WallCandidate.Price, hM6WallCandidate.WasBid);
				HM6PassiveZone value;
				if (!_passiveZones.TryGetValue(key, out value) || value == null)
				{
					value = new HM6PassiveZone();
					value.Price = hM6WallCandidate.Price;
					value.WasBid = hM6WallCandidate.WasBid;
					value.StartBar = num;
					value.LastEventBar = -1;
					value.FirstTime = now;
					value.FirstSeq = num2;
					value.LastSeq = num2;
					value.LastEventSeq = num2;
					value.LastEventType = 3;
					_passiveZones[key] = value;
				}
				value.DerivedWall = true;
				value.PresenceCount = Math.Max(value.PresenceCount, hM6WallCandidate.Presence);
				value.LastBar = num;
				value.LastUpdateTime = now;
				value.LastSeq = Math.Max(value.LastSeq, num2);
				value.MaxQty = Math.Max(value.MaxQty, hM6WallCandidate.MaxQty);
				value.LastQty = Math.Max(value.LastQty, Math.Max(hM6WallCandidate.CurrentQty, hM6WallCandidate.ActiveNow ? hM6WallCandidate.MaxQty : 0));
				value.Active = hM6WallCandidate.ActiveNow || hM6WallCandidate.State == "ACTIVO" || hM6WallCandidate.State == "DEFENDIDO" || hM6WallCandidate.Score >= 58.0;
				if (hM6WallCandidate.AddQty > 0)
				{
					value.AddQty = Math.Max(value.AddQty, hM6WallCandidate.AddQty);
				}
				if (hM6WallCandidate.RefillQty > 0)
				{
					value.RefillQty = Math.Max(value.RefillQty, hM6WallCandidate.RefillQty);
				}
				if (hM6WallCandidate.ConsumeQty > 0)
				{
					value.ConsumeQty = Math.Max(value.ConsumeQty, hM6WallCandidate.ConsumeQty);
				}
				if (hM6WallCandidate.PullQty > 0)
				{
					value.PullQty = Math.Max(value.PullQty, hM6WallCandidate.PullQty);
				}
				if (value.LastEventQty <= 0 && hM6WallCandidate.MaxQty >= num4)
				{
					value.LastEventQty = hM6WallCandidate.MaxQty;
				}
				if (!_pSRZones || !value.Active || _srZones == null || _srZones.Count <= 0 || !IsQualifiedIltDevWallLocked(value, value.Price, value.WasBid, now))
				{
					continue;
				}
				double num5 = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25) * SRBandTicks;
				for (int j = 0; j < _srZones.Count; j++)
				{
					HM6SRZone hM6SRZone = _srZones[j];
					if (hM6SRZone != null && hM6SRZone.State < 2 && hM6SRZone.AskSide != value.WasBid && !(Math.Abs(hM6SRZone.Price - value.Price) > num5 + 1E-09))
					{
						long num6 = ((hM6SRZone.InitiativeQty > 0) ? hM6SRZone.InitiativeQty : Math.Max(1L, hM6SRZone.Qty * 100 / (100L + (long)Math.Max(10, Math.Min(100, _pIltDevSRReAddPct)))));
						long reAddQty = ((hM6SRZone.ReAddQty > 0) ? hM6SRZone.ReAddQty : Math.Max(1L, hM6SRZone.Qty - num6));
						int originBar = ((hM6SRZone.VisualEventBar >= 0) ? hM6SRZone.VisualEventBar : hM6SRZone.StartBar);
						RegisterIltDevSupportLocked(hM6SRZone.Price, value.WasBid, num6, reAddQty, value, now, originBar);
						break;
					}
				}
			}
		}

		private void DrawAdaptivePersistentWallsFinal(ChartControl cc, ChartScale cs, float cR, float H, float dX, float dY)
		{
			float left;
			float right;
			float colW;
			int start;
			int count;
			List<HM6HeatmapSlice> slices;
			if (!_pPassive || !PassiveZoneEngineRequired || cc == null || cs == null || base.ChartBars == null || base.RenderTarget == null || !TryGetLiveHeatmapLayout(cc, cR, dX, out left, out right, out colW, out start, out count, out slices) || slices == null || count <= 0 || start < 0 || start >= slices.Count)
			{
				return;
			}
			long val = Math.Max(1L, _pPassiveEventMin);
			long num = ((slices[slices.Count - 1] != null) ? slices[slices.Count - 1].Seq : 0);
			int iltDevNowMs = IltDevNowMs;
			List<HM6WallCandidate> list = _adSel;
			if (list == null || _adSelSeq != num || _adSelStart != start || _adSelCount != count || iltDevNowMs - _adSelMs > 450)
			{
				long minQty = Math.Max(1L, _pWallMinQty);
				long evtMin = Math.Max(1L, _pPassiveEventMin);
				int end = Math.Min(slices.Count, start + count);
				if (end <= start)
				{
					return;
				}
				int minPresence = Math.Max(4, Math.Min(18, count / 14));
				int minRun = Math.Max(3, Math.Min(10, count / 26));
				int maxPerSide = 6;
				Dictionary<string, HM6WallCandidate> map = new Dictionary<string, HM6WallCandidate>();
				Func<double, bool, int, HM6WallCandidate> ensureCandidate = delegate(double price, bool wasBid, int idx)
				{
					string key2 = PassiveZoneKey(price, wasBid);
					HM6WallCandidate value3;
					if (!map.TryGetValue(key2, out value3) || value3 == null)
					{
						value3 = new HM6WallCandidate
						{
							Price = price,
							WasBid = wasBid,
							FirstIndex = idx,
							LastIndex = idx,
							LastSeenIndex = -999999,
							CurrentRun = 0,
							MaxRun = 0,
							Presence = 0,
							SumQty = 0L,
							MaxQty = 0L,
							CurrentQty = 0L,
							LastVisibleGap = 999999,
							ActiveNow = false,
							State = "CANDIDATO"
						};
						map[key2] = value3;
					}
					if (idx < value3.FirstIndex)
					{
						value3.FirstIndex = idx;
					}
					if (idx > value3.LastIndex)
					{
						value3.LastIndex = idx;
					}
					return value3;
				};
				Action<double, long, bool, int, HM6HeatmapSlice> action = delegate(double price, long num20, bool wasBid, int idx, HM6HeatmapSlice sl)
				{
					if (num20 >= minQty && !(price <= 0.0) && !double.IsNaN(price) && !double.IsInfinity(price))
					{
						float num21 = cs.GetYByValue(price);
						if (!(num21 < -40f * dY) && !(num21 > H + 40f * dY))
						{
							HM6WallCandidate hM6WallCandidate2 = ensureCandidate(price, wasBid, idx);
							hM6WallCandidate2.LastTime = ((sl != null) ? sl.Time : 0.0);
							hM6WallCandidate2.Presence++;
							hM6WallCandidate2.SumQty += num20;
							hM6WallCandidate2.CurrentQty = num20;
							if (num20 > hM6WallCandidate2.MaxQty)
							{
								hM6WallCandidate2.MaxQty = num20;
							}
							if (hM6WallCandidate2.LastSeenIndex == idx - 1)
							{
								hM6WallCandidate2.CurrentRun++;
							}
							else
							{
								hM6WallCandidate2.CurrentRun = 1;
							}
							hM6WallCandidate2.LastSeenIndex = idx;
							if (hM6WallCandidate2.CurrentRun > hM6WallCandidate2.MaxRun)
							{
								hM6WallCandidate2.MaxRun = hM6WallCandidate2.CurrentRun;
							}
						}
					}
				};
				Action<HM6PassiveEvent, int> action2 = delegate(HM6PassiveEvent ev, int idx)
				{
					if (ev != null && !(ev.Price <= 0.0) && ev.Qty > 0)
					{
						HM6WallCandidate hM6WallCandidate2 = ensureCandidate(ev.Price, ev.WasBid, idx);
						long num20 = Math.Max(0L, ev.Qty);
						if (ev.Type == 0)
						{
							hM6WallCandidate2.PullQty += num20;
						}
						else if (ev.Type == 1)
						{
							hM6WallCandidate2.ConsumeQty += num20;
						}
						else if (ev.Type == 2)
						{
							hM6WallCandidate2.RefillQty += num20;
						}
						else if (ev.Type == 3)
						{
							hM6WallCandidate2.AddQty += num20;
						}
						if (num20 > hM6WallCandidate2.MaxQty)
						{
							hM6WallCandidate2.MaxQty = num20;
						}
					}
				};
				for (int num2 = start; num2 < end; num2++)
				{
					HM6HeatmapSlice hM6HeatmapSlice = slices[num2];
					if (hM6HeatmapSlice == null)
					{
						continue;
					}
					if (hM6HeatmapSlice.Bid != null)
					{
						foreach (KeyValuePair<double, long> item in hM6HeatmapSlice.Bid)
						{
							action(item.Key, item.Value, true, num2, hM6HeatmapSlice);
						}
					}
					if (hM6HeatmapSlice.Ask != null)
					{
						foreach (KeyValuePair<double, long> item2 in hM6HeatmapSlice.Ask)
						{
							action(item2.Key, item2.Value, false, num2, hM6HeatmapSlice);
						}
					}
					if (hM6HeatmapSlice.Events != null && hM6HeatmapSlice.Events.Count > 0)
					{
						for (int num3 = 0; num3 < hM6HeatmapSlice.Events.Count; num3++)
						{
							action2(hM6HeatmapSlice.Events[num3], num2);
						}
					}
				}
				if (map.Count == 0)
				{
					return;
				}
				if (_passiveZones != null && _passiveZones.Count > 0)
				{
					foreach (KeyValuePair<string, HM6PassiveZone> passiveZone in _passiveZones)
					{
						HM6PassiveZone value = passiveZone.Value;
						if (value == null || value.Price <= 0.0)
						{
							continue;
						}
						float num4 = cs.GetYByValue(value.Price);
						if (num4 < -40f * dY || num4 > H + 40f * dY)
						{
							continue;
						}
						string key = PassiveZoneKey(value.Price, value.WasBid);
						HM6WallCandidate value2;
						if (map.TryGetValue(key, out value2) && value2 != null)
						{
							value2.AddQty += Math.Max(0L, value.AddQty);
							value2.PullQty += Math.Max(0L, value.PullQty);
							value2.ConsumeQty += Math.Max(0L, value.ConsumeQty);
							value2.RefillQty += Math.Max(0L, value.RefillQty);
							value2.TouchCount += Math.Max(0, value.TouchCount);
							if (value.MaxQty > value2.MaxQty)
							{
								value2.MaxQty = value.MaxQty;
							}
						}
					}
				}
				Func<bool, List<HM6WallCandidate>> func = (bool bidSide) => map.Values.Where((HM6WallCandidate c) => c != null && c.WasBid == bidSide && c.MaxQty >= minQty).ToList();
				List<HM6WallCandidate> obj = func(true);
				List<HM6WallCandidate> obj2 = func(false);
				Action<List<HM6WallCandidate>> action3 = delegate(List<HM6WallCandidate> lst)
				{
					if (lst != null && lst.Count != 0)
					{
						double meanMax = ((IEnumerable<HM6WallCandidate>)lst).Average((Func<HM6WallCandidate, double>)((HM6WallCandidate hM6WallCandidate2) => hM6WallCandidate2.MaxQty));
						double val6 = lst.Sum((HM6WallCandidate hM6WallCandidate2) => Math.Pow((double)hM6WallCandidate2.MaxQty - meanMax, 2.0)) / (double)Math.Max(1, lst.Count);
						double num20 = Math.Sqrt(Math.Max(1.0, val6));
						List<HM6WallCandidate> list3 = lst.OrderBy((HM6WallCandidate hM6WallCandidate2) => hM6WallCandidate2.MaxQty).ToList();
						double num21 = ((_lastTapePrice > 0.0) ? _lastTapePrice : ((base.CurrentBar > 0) ? base.Close[0] : lst[0].Price));
						double val7 = ((base.TickSize > 0.0) ? base.TickSize : 0.25);
						double num22 = 48.0;
						for (int num23 = 0; num23 < lst.Count; num23++)
						{
							HM6WallCandidate c = lst[num23];
							if (c != null)
							{
								c.AvgQty = (double)c.SumQty / (double)Math.Max(1, c.Presence);
								c.PersistenceK = (double)c.Presence / (double)Math.Max(1, count);
								c.DepthZ = ((double)c.MaxQty - meanMax) / num20;
								int num24 = list3.Count((HM6WallCandidate x) => x.MaxQty <= c.MaxQty);
								c.PercentileRank = (double)num24 / (double)Math.Max(1, list3.Count);
								double num25 = Math.Abs(c.Price - num21) / Math.Max(val7, 1E-07);
								c.DistanceK = 1.0 / (1.0 + num25 / num22);
								double val8 = Math.Max(c.AddQty, Math.Max((double)c.MaxQty, (double)evtMin));
								c.PullRatio = (double)c.PullQty / Math.Max(1.0, val8);
								c.LastVisibleGap = ((c.LastSeenIndex >= 0) ? Math.Max(0, end - 1 - c.LastSeenIndex) : 999999);
								int num26 = Math.Max(2, Math.Min(6, count / 40));
								double num27 = ((c.MaxQty > 0) ? ((double)c.CurrentQty / (double)Math.Max(1L, c.MaxQty)) : 0.0);
								c.ActiveNow = c.LastVisibleGap <= num26 && c.CurrentQty >= minQty && num27 >= 0.2;
								double num28 = 25.0 * Math.Max(Clamp01((c.DepthZ - 0.7) / 2.3), Clamp01((c.PercentileRank - 0.72) / 0.24));
								double num29 = 20.0 * Clamp01((c.PersistenceK - 0.16) / 0.44);
								double num30 = 10.0 * Clamp01((double)c.MaxRun / (double)Math.Max(1, minRun * 2));
								double num31 = 15.0 * Clamp01((double)c.AddQty / Math.Max(1.0, Math.Max((double)evtMin * 4.0, (double)c.MaxQty * 0.85)));
								double num32 = 15.0 * Clamp01((double)c.RefillQty / Math.Max(1.0, Math.Max((double)evtMin * 3.0, (double)c.MaxQty * 0.55)));
								double num33 = 10.0 * c.DistanceK;
								double num34 = ((c.TouchCount > 0) ? 5.0 : 0.0);
								double num35 = 15.0 * Clamp01(c.PullRatio);
								double num36 = 8.0 * Clamp01(((double)c.ConsumeQty - (double)c.RefillQty * 1.4) / Math.Max(1.0, Math.Max((double)evtMin * 2.0, (double)c.MaxQty * 0.5)));
								c.SpoofRisk = Clamp01(c.PullRatio * 0.7 + (1.0 - c.PersistenceK) * 0.2 + ((c.MaxRun <= 1) ? 0.1 : 0.0));
								double num37 = 10.0 * c.SpoofRisk;
								c.Score = num28 + num29 + num30 + num31 + num32 + num33 + num34 - num35 - num36 - num37;
								bool flag7 = (double)c.RefillQty >= Math.Max(evtMin, (double)c.ConsumeQty * 0.5) && c.ConsumeQty >= Math.Max(1L, evtMin);
								bool flag8 = c.ConsumeQty > Math.Max(evtMin, c.RefillQty * 2) && c.PullRatio < 0.4;
								long num38 = Math.Max(minQty, (long)Math.Round((double)c.MaxQty * 0.2));
								bool flag9 = (!c.ActiveNow || c.CurrentQty <= num38) && c.PullQty >= Math.Max(evtMin, Math.Max(1L, c.MaxQty / 3)) && (double)c.RefillQty < Math.Max(evtMin, (double)c.PullQty * 0.25) && (double)c.ConsumeQty < Math.Max(evtMin, (double)c.PullQty * 0.35) && (c.MaxQty >= minQty * 2 || c.AddQty >= evtMin * 2 || c.Presence >= minPresence);
								c.RemovedQty = Math.Max(c.RemovedQty, Math.Max(0L, c.PullQty));
								c.BrokenQty = Math.Max(c.BrokenQty, Math.Max(0L, c.ConsumeQty));
								if (flag7)
								{
									c.State = "DEFENDIDO";
								}
								else if (flag9)
								{
									c.State = "SPOOF";
								}
								else if (flag8)
								{
									c.State = "ROTO";
								}
								else if (c.Score >= 70.0)
								{
									c.State = "ACTIVO";
								}
								else
								{
									c.State = "CANDIDATO";
								}
								if (c.State == "SPOOF" && c.Score > 35.0)
								{
									c.Score = 35.0;
								}
							}
						}
					}
				};
				action3(obj);
				action3(obj2);
				List<HM6WallCandidate> list2 = new List<HM6WallCandidate>();
				foreach (HM6WallCandidate value4 in map.Values)
				{
					if (value4 != null && value4.Presence > 0 && !string.Equals(value4.State, "ROTO", StringComparison.OrdinalIgnoreCase))
					{
						bool flag = value4.Score >= 70.0;
						bool flag2 = value4.Score >= 58.0 && (value4.PersistenceK >= 0.38 || value4.MaxRun >= minRun || value4.RefillQty >= evtMin || value4.AddQty >= evtMin * 2);
						bool flag3 = (value4.RefillQty >= evtMin || value4.AddQty >= evtMin * 3) && value4.Score >= 52.0;
						bool flag4 = value4.State == "SPOOF" && (value4.MaxQty >= minQty * 2 || value4.AddQty >= evtMin * 2 || value4.Presence >= minPresence);
						if ((flag || flag2 || flag3 || flag4) && (flag4 || !(value4.SpoofRisk > 0.82) || !(value4.PersistenceK < 0.28) || value4.RefillQty >= evtMin))
						{
							list2.Add(value4);
						}
					}
				}
				if (list2.Count == 0)
				{
					return;
				}
				list = (from c in (from c in list2
						group c by c.WasBid).SelectMany((IGrouping<bool, HM6WallCandidate> g) => g.OrderByDescending((HM6WallCandidate c) => c.Score).Take(maxPerSide))
					orderby c.Score descending
					select c).ToList();
				if (list.Count == 0)
				{
					return;
				}
				try
				{
					lock (_lk)
					{
						SyncAdaptiveWallCandidatesToPassiveZonesLocked(list, IltDevNowMs);
					}
				}
				catch
				{
				}
				_adSel = list;
				_adSelSeq = num;
				_adSelStart = start;
				_adSelCount = count;
				_adSelMs = iltDevNowMs;
			}
			if (list == null || list.Count == 0 || !_pPassiveWallZones)
			{
				return;
			}
			float heatmapRowHeight = GetHeatmapRowHeight(cs, dY, 1f);
			float val2 = Math.Max(heatmapRowHeight * 1.05f, 4f * dY);
			float val3 = Math.Max(val2, heatmapRowHeight * 1.7f);
			for (int num5 = 0; num5 < list.Count; num5++)
			{
				HM6WallCandidate hM6WallCandidate = list[num5];
				if (hM6WallCandidate == null)
				{
					continue;
				}
				float num6 = cs.GetYByValue(hM6WallCandidate.Price);
				if (num6 < -32f * dY || num6 > H + 32f * dY || hM6WallCandidate.LastIndex < start)
				{
					continue;
				}
				float num7 = left + (float)Math.Max(0, hM6WallCandidate.FirstIndex - start) * colW;
				float num8 = left + (float)Math.Max(0, hM6WallCandidate.LastIndex - start + 1) * colW;
				if (num8 < num7)
				{
					float num9 = num7;
					num7 = num8;
					num8 = num9;
				}
				if (num8 - num7 < 12f * dX)
				{
					num8 = num7 + 12f * dX;
				}
				num7 = Math.Max(left, Math.Min(cR - 16f * dX, num7));
				num8 = Math.Max(left + 12f * dX, Math.Min(cR - 7f * dX, num8));
				if (num8 <= num7 + 4f * dX)
				{
					continue;
				}
				SharpDX.Direct2D1.SolidColorBrush solidColorBrush = (hM6WallCandidate.WasBid ? _bPassiveBid : _bPassiveAsk);
				if (solidColorBrush == null)
				{
					continue;
				}
				float num10 = (float)Clamp01((hM6WallCandidate.Score - 52.0) / 48.0);
				float val4 = (float)Math.Min(1.0, Math.Log10(1.0 + (double)hM6WallCandidate.MaxQty / (double)Math.Max(1L, val)) / 1.55);
				float num11 = Math.Max(val2, Math.Min(val3, heatmapRowHeight * (1.02f + 0.42f * Math.Max(num10, val4))));
				float num12 = num6 - num11 * 0.5f;
				float num13 = Math.Max(1.6f * dX, num11 * 0.22f);
				float num14 = num8 - num7;
				if (_bPanBg != null)
				{
					_bPanBg.Opacity = 0.72f;
					base.RenderTarget.FillRoundedRectangle(new RoundedRectangle
					{
						Rect = new RectangleF(num7 - 1f * dX, num12 - 0.8f * dY, num14 + 2f * dX, num11 + 1.6f * dY),
						RadiusX = num13,
						RadiusY = num13
					}, _bPanBg);
				}
				solidColorBrush.Opacity = 0.22f + 0.16f * num10;
				base.RenderTarget.FillRoundedRectangle(new RoundedRectangle
				{
					Rect = new RectangleF(num7, num12, num14, num11),
					RadiusX = num13,
					RadiusY = num13
				}, solidColorBrush);
				solidColorBrush.Opacity = 0.76f + 0.24f * num10;
				base.RenderTarget.DrawLine(new Vector2(num7, num6), new Vector2(num8, num6), solidColorBrush, Math.Max(1f * dY, num11 * (0.22f + 0.08f * num10)));
				base.RenderTarget.DrawRoundedRectangle(new RoundedRectangle
				{
					Rect = new RectangleF(num7, num12, num14, num11),
					RadiusX = num13,
					RadiusY = num13
				}, solidColorBrush, Math.Max(0.8f, 1.05f * dX));
				base.RenderTarget.DrawLine(new Vector2(num7, num12), new Vector2(num8, num12), solidColorBrush, Math.Max(0.65f, num11 * 0.09f));
				base.RenderTarget.DrawLine(new Vector2(num7, num12 + num11), new Vector2(num8, num12 + num11), solidColorBrush, Math.Max(0.65f, num11 * 0.09f));
				float num15 = Math.Max(1f * dX, Math.Min(3f * dX, num11 * 0.22f));
				solidColorBrush.Opacity = 0.9f;
				base.RenderTarget.FillRectangle(new RectangleF(num7, num12, num15, num11), solidColorBrush);
				base.RenderTarget.FillRectangle(new RectangleF(num8 - num15, num12, num15, num11), solidColorBrush);
				if (_bTxt == null)
				{
					continue;
				}
				bool flag5 = string.Equals(hM6WallCandidate.State, "SPOOF", StringComparison.OrdinalIgnoreCase);
				bool flag6 = string.Equals(hM6WallCandidate.State, "DEFENDIDO", StringComparison.OrdinalIgnoreCase);
				string text = "MURO";
				if (flag5)
				{
					long liveQty = Math.Max(1L, (hM6WallCandidate.RemovedQty > 0) ? hM6WallCandidate.RemovedQty : Math.Max(hM6WallCandidate.PullQty, hM6WallCandidate.MaxQty - hM6WallCandidate.CurrentQty));
					long qty;
					if (FrozenTagVigente(hM6WallCandidate.Price, hM6WallCandidate.WasBid, hM6WallCandidate.State, liveQty, out qty) && qty >= InstitutionalMinQty())
					{
						text = "SPOOF " + FQ(qty);
					}
				}
				else if (flag6)
				{
					text = "REFILL " + FQ(Math.Max(1L, (hM6WallCandidate.RefillQty > 0) ? hM6WallCandidate.RefillQty : hM6WallCandidate.CurrentQty));
				}
				SharpDX.Direct2D1.SolidColorBrush solidColorBrush2 = solidColorBrush;
				float num16 = Math.Max(heatmapRowHeight * 1.05f, 10f * dY);
				float num17 = Math.Max(54f * dX, (float)text.Length * 5.4f * dX + 10f * dX);
				float val5 = num7 + (num14 - num17) * 0.5f;
				float num18 = Math.Max(255f * dX + 3f * dX, Math.Min(cR - num17 - 5f * dX, val5));
				float num19 = num6 - num16 * 0.5f;
				if (_bPanBg != null)
				{
					_bPanBg.Opacity = 0.95f;
					base.RenderTarget.FillRoundedRectangle(new RoundedRectangle
					{
						Rect = new RectangleF(num18, num19, num17, num16),
						RadiusX = Math.Max(1.5f * dX, num16 * 0.2f),
						RadiusY = Math.Max(1.5f * dY, num16 * 0.2f)
					}, _bPanBg);
				}
				if (solidColorBrush2 != null)
				{
					solidColorBrush2.Opacity = 1f;
				}
				base.RenderTarget.DrawRoundedRectangle(new RoundedRectangle
				{
					Rect = new RectangleF(num18, num19, num17, num16),
					RadiusX = Math.Max(1.5f * dX, num16 * 0.2f),
					RadiusY = Math.Max(1.5f * dY, num16 * 0.2f)
				}, solidColorBrush2 ?? solidColorBrush, Math.Max(0.85f, 1.05f * dX));
				if (flag5 && _bPassiveWarn != null)
				{
					_bPassiveWarn.Opacity = 0.98f;
					base.RenderTarget.DrawLine(new Vector2(num18 + 2f * dX, num19 + num16 - 1.8f * dY), new Vector2(num18 + num17 - 2f * dX, num19 + num16 - 1.8f * dY), _bPassiveWarn, Math.Max(0.75f, 0.95f * dY));
				}
				_bTxt.Opacity = 1f;
				TC(text, num18 + 2f * dX, num19 - 0.4f * dY, _fTiny, _bTxt, num17 - 4f * dX, num16 + 1f * dY);
			}
		}

		private void DrawSRHistLevels(ChartControl cc, ChartScale cs, float cR, float H, float dX, float dY)
		{
			if (!_pPassive || !_pSRHist || _srHist == null || _bSRHist == null || cc == null || cs == null || base.ChartBars == null || base.RenderTarget == null)
			{
				return;
			}
			List<double> list = null;
			List<HM6SRHist> list2;
			lock (_lk)
			{
				if (_srHist == null || _srHist.Count == 0)
				{
					return;
				}
				list2 = new List<HM6SRHist>(_srHist);
				if (_srZones != null)
				{
					for (int i = 0; i < _srZones.Count; i++)
					{
						if (_srZones[i] != null && _srZones[i].State < 2)
						{
							if (list == null)
							{
								list = new List<double>();
							}
							list.Add(_srZones[i].Price);
						}
					}
				}
				if (_iltDevLevels != null)
				{
					for (int j = 0; j < _iltDevLevels.Count; j++)
					{
						if (_iltDevLevels[j] != null)
						{
							if (list == null)
							{
								list = new List<double>();
							}
							list.Add(_iltDevLevels[j].Price);
						}
					}
				}
			}
			int fromIndex = base.ChartBars.FromIndex;
			int toIndex = base.ChartBars.ToIndex;
			if (toIndex < fromIndex)
			{
				return;
			}
			double num = Math.Max(0.25, _pSRHistWinPts);
			list2.Sort(delegate(HM6SRHist a, HM6SRHist b)
			{
				long value = ((a != null) ? a.Qty : 0);
				return ((b != null) ? b.Qty : 0).CompareTo(value);
			});
			int num2 = 0;
			int num3 = 0;
			for (int num4 = 0; num4 < list2.Count; num4++)
			{
				if (num2 >= 12)
				{
					break;
				}
				HM6SRHist hM6SRHist = list2[num4];
				if (hM6SRHist == null || !hM6SRHist.EverDefended)
				{
					continue;
				}
				float num5 = cs.GetYByValue(hM6SRHist.Price);
				if (num5 < -12f * dY || num5 > H + 12f * dY)
				{
					continue;
				}
				int startBar = hM6SRHist.StartBar;
				int barIndex = ((startBar < 0) ? toIndex : Math.Min(toIndex, startBar));
				float val;
				if (startBar < fromIndex)
				{
					val = 0f;
				}
				else
				{
					try
					{
						val = cc.GetXByBarIndex(base.ChartBars, barIndex);
					}
					catch
					{
						val = Math.Max(0f, cR - 18f * dX);
					}
				}
				val = Math.Max(0f, Math.Min(cR - 8f * dX, val));
				float num6 = cR - 7f * dX;
				if (num6 <= val + 6f * dX)
				{
					continue;
				}
				num2++;
				_bSRHist.Opacity = (hM6SRHist.Defended ? 0.55f : 0.18f);
				base.RenderTarget.DrawLine(new Vector2(val, num5), new Vector2(num6, num5), _bSRHist, Math.Max(0.9f, 1f * dY), hM6SRHist.Defended ? null : _stDash);
				if (num3 >= 5)
				{
					continue;
				}
				bool flag = false;
				if (list != null)
				{
					for (int num7 = 0; num7 < list.Count; num7++)
					{
						if (Math.Abs(list[num7] - hM6SRHist.Price) <= num + 1E-09)
						{
							flag = true;
							break;
						}
					}
				}
				if (!flag)
				{
					string text = "Bracket " + FQ(Math.Max(1L, hM6SRHist.Qty));
					float num8 = Math.Max(56f * dX, (float)text.Length * 5.4f * dX + 8f * dX);
					float num9 = 11f * dY;
					float num10 = val + (num6 - val) * 0.5f - num8 * 0.5f;
					float num11 = num5 - num9 * 0.5f;
					if (_bPanBg != null)
					{
						_bPanBg.Opacity = 0.9f;
						base.RenderTarget.FillRectangle(new RectangleF(num10, num11, num8, num9), _bPanBg);
					}
					_bSRHist.Opacity = (hM6SRHist.Defended ? 0.85f : 0.35f);
					base.RenderTarget.DrawRectangle(new RectangleF(num10, num11, num8, num9), _bSRHist, Math.Max(0.8f, 0.9f * dX));
					if (_fSm != null && _bTxt != null)
					{
						_bTxt.Opacity = (hM6SRHist.Defended ? 0.92f : 0.45f);
						TC(text, num10 + 4f * dX, num11 + (num9 - 10f * dY) * 0.5f, _fSm, _bTxt, num8 - 8f * dX, 10f * dY);
					}
					num3++;
				}
			}
		}

		private void DrawIltDevSupportLevels(ChartControl cc, ChartScale cs, float cR, float H, float dX, float dY)
		{
			if (!_pPassive || !_pSRZones || _iltDevLevels == null || cc == null || cs == null || base.ChartBars == null || base.RenderTarget == null || (_bSRZone == null && _bPassiveBid == null && _bPassiveAsk == null))
			{
				return;
			}
			List<HM6DefendedWallLine> list = null;
			List<HM6IltDevSupportLevel> list2;
			lock (_lk)
			{
				if (_iltDevLevels == null || _iltDevLevels.Count == 0)
				{
					return;
				}
				list2 = _iltDevLevels.Where((HM6IltDevSupportLevel l) => l != null && l.StrengthScore >= Math.Max(0, _pIltDevSRMinStrength) && (!l.Broken || l.BreakBar >= 0)).ToList();
				if (_defendedWallLines != null)
				{
					list = _defendedWallLines.Where((HM6DefendedWallLine z) => z != null && z.Confirmed && z.Active && !z.Broken && !z.Discard && !z.Faded && z.StrengthScore >= Math.Max(0, _pIltDevZoneMinStrength)).ToList();
				}
			}
			if (list2.Count == 0)
			{
				return;
			}
			int fromIndex = base.ChartBars.FromIndex;
			int toIndex = base.ChartBars.ToIndex;
			if (toIndex < fromIndex)
			{
				return;
			}
			float num = 0f;
			float num2 = Math.Max(num + 12f * dX, cR - 7f * dX);
			float num3 = Math.Max(10f * dX, 6f);
			float heatmapRowHeight = GetHeatmapRowHeight(cs, dY, 1f);
			float num4 = Math.Max(1.2f * dX, heatmapRowHeight * 0.2f);
			float num5 = Math.Max(11f * dY, heatmapRowHeight * 0.92f);
			float num6 = Math.Max(4f * dY, num4 + 2f * dY);
			double num7 = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25);
			double num8 = num7 * SRBandTicks;
			double num9 = num7 * (double)Math.Max(Math.Max(1, _pSRBandTicks), Math.Max(1, _pIltDevZoneBandTicks));
			list2.Sort(delegate(HM6IltDevSupportLevel a, HM6IltDevSupportLevel b)
			{
				int value = ((a == null) ? (-1) : ((a.VisualEventBar >= 0) ? a.VisualEventBar : a.StartBar));
				return ((b == null) ? (-1) : ((b.VisualEventBar >= 0) ? b.VisualEventBar : b.StartBar)).CompareTo(value);
			});
			for (int num10 = 0; num10 < list2.Count; num10++)
			{
				HM6IltDevSupportLevel hM6IltDevSupportLevel = list2[num10];
				if (hM6IltDevSupportLevel == null)
				{
					continue;
				}
				bool flag = false;
				if (list != null)
				{
					for (int num11 = 0; num11 < list.Count; num11++)
					{
						HM6DefendedWallLine hM6DefendedWallLine = list[num11];
						if (hM6DefendedWallLine != null && hM6DefendedWallLine.WasBid == hM6IltDevSupportLevel.WasBid && Math.Abs(hM6DefendedWallLine.Price - hM6IltDevSupportLevel.Price) <= num9 + 1E-09)
						{
							flag = true;
							break;
						}
					}
				}
				if (flag)
				{
					continue;
				}
				bool flag2 = false;
				for (int num12 = 0; num12 < list2.Count; num12++)
				{
					HM6IltDevSupportLevel hM6IltDevSupportLevel2 = list2[num12];
					if (hM6IltDevSupportLevel2 != null && hM6IltDevSupportLevel2 != hM6IltDevSupportLevel && !hM6IltDevSupportLevel2.Broken && hM6IltDevSupportLevel2.WasBid != hM6IltDevSupportLevel.WasBid && !(Math.Abs(hM6IltDevSupportLevel2.Price - hM6IltDevSupportLevel.Price) > num9 + 1E-09))
					{
						int num13 = Math.Abs(hM6IltDevSupportLevel2.StrengthScore - hM6IltDevSupportLevel.StrengthScore);
						if (num13 < 15 || hM6IltDevSupportLevel.StrengthScore < hM6IltDevSupportLevel2.StrengthScore)
						{
							flag2 = true;
							break;
						}
					}
				}
				if (flag2)
				{
					continue;
				}
				int num14 = ((hM6IltDevSupportLevel.VisualEventBar >= 0) ? hM6IltDevSupportLevel.VisualEventBar : hM6IltDevSupportLevel.StartBar);
				int barIndex = ((num14 < 0) ? toIndex : Math.Min(toIndex, num14));
				float val;
				if (num14 < fromIndex)
				{
					val = num;
				}
				else
				{
					try
					{
						val = cc.GetXByBarIndex(base.ChartBars, barIndex);
					}
					catch
					{
						val = num2 - num3;
					}
				}
				bool broken = hM6IltDevSupportLevel.Broken;
				float num15 = Math.Max(num, Math.Min(num2 - num3, val));
				float num16 = num2;
				if (broken && hM6IltDevSupportLevel.BreakBar >= 0)
				{
					if (hM6IltDevSupportLevel.BreakBar < fromIndex)
					{
						continue;
					}
					float val2;
					try
					{
						val2 = cc.GetXByBarIndex(base.ChartBars, Math.Min(toIndex, hM6IltDevSupportLevel.BreakBar));
					}
					catch
					{
						val2 = num15 + num3;
					}
					num16 = Math.Max(num, Math.Min(num2, val2));
				}
				if (num16 < num15 + num3)
				{
					num15 = Math.Max(num, num16 - num3);
				}
				if (num16 <= num15 + 0.5f)
				{
					continue;
				}
				float num17 = cs.GetYByValue(hM6IltDevSupportLevel.Price);
				bool flag3 = num17 < num6;
				bool flag4 = num17 > H - num6;
				float num18 = Math.Max(num6, Math.Min(H - num6, num17));
				float val3 = cs.GetYByValue(hM6IltDevSupportLevel.Price + num8);
				float val4 = cs.GetYByValue(hM6IltDevSupportLevel.Price - num8);
				float num19 = Math.Min(val3, val4);
				float num20 = Math.Max(val3, val4);
				float num21 = Math.Max(num20 - num19, heatmapRowHeight * 0.85f);
				SharpDX.Direct2D1.SolidColorBrush solidColorBrush = (hM6IltDevSupportLevel.WasBid ? _bPassiveBid : _bPassiveAsk);
				if (solidColorBrush == null)
				{
					solidColorBrush = _bSRZone;
				}
				SharpDX.Direct2D1.SolidColorBrush solidColorBrush2 = ((_bSRZone != null) ? _bSRZone : solidColorBrush);
				if (solidColorBrush == null || solidColorBrush2 == null)
				{
					continue;
				}
				float num22 = Math.Max(0f, Math.Min(1f, (float)hM6IltDevSupportLevel.StrengthScore / 100f));
				float num23 = num4 * (0.78f + 0.52f * num22);
				float num24 = (broken ? 0.42f : 1f);
				if (num19 < H + 20f * dY && num19 + num21 > -20f * dY)
				{
					solidColorBrush2.Opacity = (0.04f + 0.075f * num22) * num24;
					base.RenderTarget.FillRectangle(new RectangleF(num15, num19, num16 - num15, num21), solidColorBrush2);
					solidColorBrush2.Opacity = (0.34f + 0.36f * num22) * num24;
					base.RenderTarget.DrawLine(new Vector2(num15, num19), new Vector2(num16, num19), solidColorBrush2, Math.Max(0.85f, 0.95f * dY));
					base.RenderTarget.DrawLine(new Vector2(num15, num20), new Vector2(num16, num20), solidColorBrush2, Math.Max(0.85f, 0.95f * dY));
				}
				solidColorBrush.Opacity = (0.1f + 0.2f * num22) * num24;
				base.RenderTarget.DrawLine(new Vector2(num15, num18), new Vector2(num16, num18), solidColorBrush, num23 + Math.Max(2.2f * dX, 2f));
				solidColorBrush.Opacity = (0.72f + 0.28f * num22) * num24;
				base.RenderTarget.DrawLine(new Vector2(num15, num18), new Vector2(num16, num18), solidColorBrush, num23);
				solidColorBrush.Opacity = num24;
				float num25 = Math.Max(4f * dY, heatmapRowHeight * 0.58f);
				base.RenderTarget.DrawLine(new Vector2(num15, num18 - num25), new Vector2(num15, num18 + num25), solidColorBrush, Math.Max(1f, num23));
				string text = (hM6IltDevSupportLevel.WasBid ? "ILT-DEV-SUPPORT F" : "ILT-DEV-RESIST F") + Math.Max(0, Math.Min(100, hM6IltDevSupportLevel.StrengthScore));
				if (_fSm != null && _bTxt != null)
				{
					float num26 = Math.Max(86f * dX, (float)text.Length * 5.4f * dX + 12f * dX);
					float num27 = Math.Max(num + 2f * dX, Math.Min(num2 - num26 - 2f * dX, num15 + 3f * dX));
					float num28 = Math.Max(1f * dY, Math.Min(H - num5 - 1f * dY, num18 - num5 * 0.5f));
					if (_bPanBg != null)
					{
						_bPanBg.Opacity = 0.94f * num24;
						base.RenderTarget.FillRoundedRectangle(new RoundedRectangle
						{
							Rect = new RectangleF(num27, num28, num26, num5),
							RadiusX = Math.Max(1.5f * dX, num5 * 0.22f),
							RadiusY = Math.Max(1.5f * dY, num5 * 0.22f)
						}, _bPanBg);
					}
					solidColorBrush.Opacity = 0.98f * num24;
					base.RenderTarget.DrawRoundedRectangle(new RoundedRectangle
					{
						Rect = new RectangleF(num27, num28, num26, num5),
						RadiusX = Math.Max(1.5f * dX, num5 * 0.22f),
						RadiusY = Math.Max(1.5f * dY, num5 * 0.22f)
					}, solidColorBrush, Math.Max(0.85f, 1f * dX));
					solidColorBrush2.Opacity = 0.92f * num24;
					base.RenderTarget.DrawLine(new Vector2(num27 + 2f * dX, num28 + num5 - 1.7f * dY), new Vector2(num27 + num26 - 2f * dX, num28 + num5 - 1.7f * dY), solidColorBrush2, Math.Max(0.75f, 0.9f * dY));
					_bTxt.Opacity = 0.98f * num24;
					T(text, num27 + 4f * dX, num28 + 1f * dY, _fSm, _bTxt, num26 - 6f * dX, num5);
				}
				if ((flag3 || flag4) && _fSm != null && _bTxt != null)
				{
					string text2 = (flag3 ? "▲ " : "▼ ") + (hM6IltDevSupportLevel.WasBid ? "ILT-DEV-SUPPORT " : "ILT-DEV-RESIST ") + hM6IltDevSupportLevel.Price.ToString("F" + _decs);
					float num29 = Math.Max(10f * dY, num5);
					float num30 = Math.Max(88f * dX, Math.Min(158f * dX, (float)text2.Length * 5.6f * dX));
					float num31 = Math.Max(num + 2f * dX, num2 - num30 - 3f * dX);
					float num32 = (flag3 ? (1f * dY) : (H - num29 - 1f * dY));
					if (_bPanBg != null)
					{
						_bPanBg.Opacity = 0.9f;
						base.RenderTarget.FillRectangle(new RectangleF(num31, num32, num30, num29), _bPanBg);
					}
					solidColorBrush.Opacity = 0.92f;
					base.RenderTarget.DrawRectangle(new RectangleF(num31, num32, num30, num29), solidColorBrush, Math.Max(0.8f, dX));
					_bTxt.Opacity = 0.92f;
					T(text2, num31 + 4f * dX, num32 + 1f * dY, _fSm, _bTxt, num30 - 6f * dX, num29);
				}
			}
		}

		private void DrawSRZones(ChartControl cc, ChartScale cs, float cR, float H, float dX, float dY)
		{
			if (!_pPassive || !_pSRZones || _srZones == null || _bSRZone == null || cc == null || cs == null || base.ChartBars == null || base.RenderTarget == null)
			{
				return;
			}
			List<double> list = null;
			List<HM6SRZone> list2;
			lock (_lk)
			{
				if (_srZones == null || _srZones.Count == 0)
				{
					return;
				}
				list2 = new List<HM6SRZone>(_srZones);
				if (_defendedWallLines != null)
				{
					for (int i = 0; i < _defendedWallLines.Count; i++)
					{
						HM6DefendedWallLine hM6DefendedWallLine = _defendedWallLines[i];
						if (hM6DefendedWallLine != null && hM6DefendedWallLine.Confirmed && !hM6DefendedWallLine.Broken && !hM6DefendedWallLine.Discard)
						{
							if (list == null)
							{
								list = new List<double>();
							}
							list.Add(hM6DefendedWallLine.Price);
						}
					}
				}
			}
			int fromIndex = base.ChartBars.FromIndex;
			int toIndex = base.ChartBars.ToIndex;
			if (toIndex < fromIndex)
			{
				return;
			}
			int num = PrimaryBarIndexSafe();
			double num2 = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25);
			double num3 = num2 * SRBandTicks;
			long num4 = IltDevSRInstitutionalMinQty();
			list2.Sort(delegate(HM6SRZone a, HM6SRZone b)
			{
				int num13 = ((a == null) ? 9 : ((a.State >= 2) ? 1 : 0));
				int num14 = ((b == null) ? 9 : ((b.State >= 2) ? 1 : 0));
				if (num13 != num14)
				{
					return num13.CompareTo(num14);
				}
				long value = ((a != null) ? a.Qty : 0);
				return ((b != null) ? b.Qty : 0).CompareTo(value);
			});
			int num5 = 0;
			int num6 = 0;
			for (int num7 = 0; num7 < list2.Count; num7++)
			{
				HM6SRZone hM6SRZone = list2[num7];
				if (hM6SRZone == null || hM6SRZone.State >= 2 || num - hM6SRZone.StartBar > Math.Max(100, Math.Min(10000, _pIltDevSRRetentionBars)) || num5 >= 120)
				{
					continue;
				}
				float num8 = cs.GetYByValue(hM6SRZone.Price + num3);
				float num9 = cs.GetYByValue(hM6SRZone.Price - num3);
				if (num9 < 0f || num8 > H || num9 <= num8)
				{
					continue;
				}
				int num10 = ((hM6SRZone.VisualEventBar >= 0) ? hM6SRZone.VisualEventBar : hM6SRZone.StartBar);
				int barIndex = ((num10 < 0) ? toIndex : Math.Min(toIndex, num10));
				float val;
				if (num10 < fromIndex)
				{
					val = 0f;
				}
				else
				{
					try
					{
						val = cc.GetXByBarIndex(base.ChartBars, barIndex);
					}
					catch
					{
						val = Math.Max(0f, cR - 18f * dX);
					}
				}
				val = Math.Max(0f, Math.Min(cR - 8f * dX, val));
				float num11;
				if (hM6SRZone.State >= 2 && hM6SRZone.BreakBar >= 0)
				{
					int barIndex2 = Math.Max(fromIndex, Math.Min(toIndex, hM6SRZone.BreakBar));
					num11 = cc.GetXByBarIndex(base.ChartBars, barIndex2);
				}
				else
				{
					num11 = cR - 7f * dX;
				}
				if (num11 <= val + 3f * dX)
				{
					num11 = Math.Min(cR - 7f * dX, val + Math.Max(8f * dX, 6f));
				}
				if (num11 <= val + 0.5f)
				{
					continue;
				}
				float opacity = ((hM6SRZone.State == 0) ? 0.26f : ((hM6SRZone.State == 1) ? 0.17f : ((hM6SRZone.State == 3) ? 0.12f : 0.06f)));
				_bSRZone.Opacity = opacity;
				base.RenderTarget.FillRectangle(new RectangleF(val, num8, num11 - val, num9 - num8), _bSRZone);
				_bSRZone.Opacity = ((hM6SRZone.State >= 2) ? 0.2f : 0.55f);
				float y = cs.GetYByValue(hM6SRZone.Price);
				base.RenderTarget.DrawLine(new Vector2(val, y), new Vector2(num11, y), _bSRZone, 1f * dY, (hM6SRZone.State >= 2) ? _stDash : null);
				_bSRZone.Opacity = ((hM6SRZone.State >= 2) ? 0.28f : 0.82f);
				base.RenderTarget.DrawLine(new Vector2(val, num8), new Vector2(val, num9), _bSRZone, Math.Max(0.9f, 1.1f * dX));
				if (hM6SRZone.State < 2)
				{
					num5++;
				}
				if (num10 < fromIndex || _fSm == null || _bTxt == null)
				{
					continue;
				}
				bool flag = false;
				if (list != null)
				{
					for (int num12 = 0; num12 < list.Count; num12++)
					{
						if (Math.Abs(list[num12] - hM6SRZone.Price) <= num3 + 1E-09)
						{
							flag = true;
							break;
						}
					}
				}
				if (!flag)
				{
					bool flag2 = num6 < 5 && hM6SRZone.Qty >= num4;
					string txt = (flag2 ? ("S/R " + FQ(hM6SRZone.Qty)) : "S/R");
					float mw = (flag2 ? (90f * dX) : (32f * dX));
					_bTxt.Opacity = (flag2 ? 0.82f : 0.62f);
					TC(txt, val + 4f * dX, num8 - 12f * dY, _fSm, _bTxt, mw, 12f * dY);
					if (flag2)
					{
						num6++;
					}
				}
			}
		}

		private void DrawDefendedWallLines(ChartControl cc, ChartScale cs, float cR, float H, float dX, float dY)
		{
			if (!_pPassive || !_pIltDevZones || _defendedWallLines == null || _defendedWallLines.Count == 0 || cc == null || cs == null || base.ChartBars == null || base.RenderTarget == null || (_bSRZone == null && _bPassiveBid == null && _bPassiveAsk == null))
			{
				return;
			}
			List<HM6DefendedWallLine> list;
			lock (_lk)
			{
				if (_defendedWallLines == null || _defendedWallLines.Count == 0)
				{
					return;
				}
				list = _defendedWallLines.Where((HM6DefendedWallLine d) => d != null && !d.Discard && !d.Faded && ((d.PreConfirmed && !d.Confirmed && !d.Broken) || (d.Confirmed && !d.Broken && d.StrengthScore >= Math.Max(0, _pIltDevZoneMinStrength)) || (d.Confirmed && d.Broken && d.BreakBar >= 0 && d.StrengthScore >= Math.Max(0, _pIltDevZoneMinStrength)))).ToList();
			}
			if (list.Count == 0)
			{
				return;
			}
			int fromIndex = base.ChartBars.FromIndex;
			int toIndex = base.ChartBars.ToIndex;
			if (toIndex < fromIndex)
			{
				return;
			}
			float num = 0f;
			float num2 = Math.Max(num + 12f * dX, cR - 7f * dX);
			float num3 = Math.Max(10f * dX, 6f);
			float heatmapRowHeight = GetHeatmapRowHeight(cs, dY, 1f);
			float num4 = Math.Max(1.2f * dX, heatmapRowHeight * 0.2f);
			float num5 = Math.Max(11f * dY, heatmapRowHeight * 0.92f);
			float num6 = Math.Max(4f * dY, num4 + 2f * dY);
			double num7 = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25);
			double num8 = num7 * (double)Math.Max(1, _pIltDevZoneBandTicks);
			long num9 = Math.Max(1L, IltDevZoneInstitutionalMinQty());
			list.Sort((HM6DefendedWallLine a, HM6DefendedWallLine b) => b.StartBar.CompareTo(a.StartBar));
			for (int num10 = 0; num10 < list.Count; num10++)
			{
				HM6DefendedWallLine hM6DefendedWallLine = list[num10];
				if (hM6DefendedWallLine == null || hM6DefendedWallLine.Discard)
				{
					continue;
				}
				bool flag = hM6DefendedWallLine.PreConfirmed && !hM6DefendedWallLine.Confirmed && !hM6DefendedWallLine.Broken;
				bool flag2 = hM6DefendedWallLine.Confirmed && !hM6DefendedWallLine.Broken && !hM6DefendedWallLine.Faded;
				if (!flag && !flag2)
				{
					continue;
				}
				long num11 = Math.Max(1L, Math.Max(hM6DefendedWallLine.ExecutedQty, Math.Max(hM6DefendedWallLine.RefillQty, Math.Abs(hM6DefendedWallLine.AttackDelta))));
				long num12 = ((hM6DefendedWallLine.QtyFrozen && hM6DefendedWallLine.FrozenQty > 0) ? hM6DefendedWallLine.FrozenQty : num11);
				int num13 = ((hM6DefendedWallLine.VisualEventBar >= 0) ? hM6DefendedWallLine.VisualEventBar : hM6DefendedWallLine.StartBar);
				int barIndex = ((num13 < 0) ? toIndex : Math.Min(toIndex, num13));
				float val;
				if (num13 < fromIndex)
				{
					val = num;
				}
				else
				{
					try
					{
						val = cc.GetXByBarIndex(base.ChartBars, barIndex);
					}
					catch
					{
						val = num2 - num3;
					}
				}
				float num14 = Math.Max(num, Math.Min(num2 - num3, val));
				bool flag3 = flag2 && hM6DefendedWallLine.Broken;
				float num15 = num2;
				if (flag3 && hM6DefendedWallLine.BreakBar >= 0)
				{
					if (hM6DefendedWallLine.BreakBar < fromIndex)
					{
						continue;
					}
					float val2;
					try
					{
						val2 = cc.GetXByBarIndex(base.ChartBars, Math.Min(toIndex, hM6DefendedWallLine.BreakBar));
					}
					catch
					{
						val2 = num14 + num3;
					}
					num15 = Math.Max(num, Math.Min(num2, val2));
				}
				if (num15 < num14 + num3)
				{
					num14 = Math.Max(num, num15 - num3);
				}
				if (num15 <= num14 + 0.5f)
				{
					continue;
				}
				float num16 = cs.GetYByValue(hM6DefendedWallLine.Price);
				bool flag4 = num16 < num6;
				bool flag5 = num16 > H - num6;
				float num17 = Math.Max(num6, Math.Min(H - num6, num16));
				float val3 = cs.GetYByValue(hM6DefendedWallLine.Price + num8);
				float val4 = cs.GetYByValue(hM6DefendedWallLine.Price - num8);
				float num18 = Math.Min(val3, val4);
				float num19 = Math.Max(val3, val4);
				float num20 = Math.Max(num19 - num18, heatmapRowHeight * 0.85f);
				SharpDX.Direct2D1.SolidColorBrush solidColorBrush = (hM6DefendedWallLine.WasBid ? _bPassiveBid : _bPassiveAsk);
				if (solidColorBrush == null)
				{
					solidColorBrush = _bSRZone;
				}
				SharpDX.Direct2D1.SolidColorBrush solidColorBrush2 = ((_bSRZone != null) ? _bSRZone : solidColorBrush);
				if (solidColorBrush == null || solidColorBrush2 == null)
				{
					continue;
				}
				float num21 = (flag2 ? Math.Max(0f, Math.Min(1f, (float)hM6DefendedWallLine.StrengthScore / 100f)) : Math.Max(0f, Math.Min(1f, (float)(hM6DefendedWallLine.DefenseScore / 100.0))));
				if (num21 <= 0.01f)
				{
					num21 = Math.Max(0.25f, Math.Min(1f, (float)num12 / (float)num9));
				}
				float num22 = num4 * (0.78f + 0.52f * num21);
				float num23 = (flag3 ? 0.42f : 1f);
				if (num18 < H + 20f * dY && num18 + num20 > -20f * dY)
				{
					solidColorBrush2.Opacity = (0.04f + 0.075f * num21) * num23;
					base.RenderTarget.FillRectangle(new RectangleF(num14, num18, num15 - num14, num20), solidColorBrush2);
					solidColorBrush2.Opacity = (0.34f + 0.36f * num21) * num23;
					base.RenderTarget.DrawLine(new Vector2(num14, num18), new Vector2(num15, num18), solidColorBrush2, Math.Max(0.85f, 0.95f * dY));
					base.RenderTarget.DrawLine(new Vector2(num14, num19), new Vector2(num15, num19), solidColorBrush2, Math.Max(0.85f, 0.95f * dY));
				}
				solidColorBrush.Opacity = (0.1f + 0.2f * num21) * num23;
				base.RenderTarget.DrawLine(new Vector2(num14, num17), new Vector2(num15, num17), solidColorBrush, num22 + Math.Max(2.2f * dX, 2f));
				solidColorBrush.Opacity = (0.72f + 0.28f * num21) * num23;
				base.RenderTarget.DrawLine(new Vector2(num14, num17), new Vector2(num15, num17), solidColorBrush, num22);
				solidColorBrush.Opacity = num23;
				float num24 = Math.Max(4f * dY, heatmapRowHeight * 0.58f);
				base.RenderTarget.DrawLine(new Vector2(num14, num17 - num24), new Vector2(num14, num17 + num24), solidColorBrush, Math.Max(1f, num22));
				string text = ((!flag) ? ("ILT-DEV-EVENT F" + Math.Max(0, Math.Min(100, hM6DefendedWallLine.StrengthScore))) : ("DEF " + hM6DefendedWallLine.FavorableCloses + "/" + Math.Max(1, Math.Min(3, _pZoneCloses))));
				if (_fSm != null && _bTxt != null)
				{
					float num25 = Math.Max(86f * dX, (float)text.Length * 5.4f * dX + 12f * dX);
					float num26 = Math.Max(num + 2f * dX, Math.Min(num2 - num25 - 2f * dX, num14 + 3f * dX));
					float num27 = Math.Max(1f * dY, Math.Min(H - num5 - 1f * dY, num17 - num5 * 0.5f));
					if (_bPanBg != null)
					{
						_bPanBg.Opacity = 0.94f * num23;
						base.RenderTarget.FillRoundedRectangle(new RoundedRectangle
						{
							Rect = new RectangleF(num26, num27, num25, num5),
							RadiusX = Math.Max(1.5f * dX, num5 * 0.22f),
							RadiusY = Math.Max(1.5f * dY, num5 * 0.22f)
						}, _bPanBg);
					}
					solidColorBrush.Opacity = 0.98f * num23;
					base.RenderTarget.DrawRoundedRectangle(new RoundedRectangle
					{
						Rect = new RectangleF(num26, num27, num25, num5),
						RadiusX = Math.Max(1.5f * dX, num5 * 0.22f),
						RadiusY = Math.Max(1.5f * dY, num5 * 0.22f)
					}, solidColorBrush, Math.Max(0.85f, 1f * dX));
					solidColorBrush2.Opacity = (flag ? 0.62f : 0.92f) * num23;
					base.RenderTarget.DrawLine(new Vector2(num26 + 2f * dX, num27 + num5 - 1.7f * dY), new Vector2(num26 + num25 - 2f * dX, num27 + num5 - 1.7f * dY), solidColorBrush2, Math.Max(0.75f, 0.9f * dY));
					_bTxt.Opacity = 0.98f * num23;
					T(text, num26 + 4f * dX, num27 + 1f * dY, _fSm, _bTxt, num25 - 6f * dX, num5);
				}
				if ((flag4 || flag5) && _fSm != null && _bTxt != null)
				{
					string text2 = (flag ? "DEF " : ("ILT-DEV-EVENT F" + Math.Max(0, Math.Min(100, hM6DefendedWallLine.StrengthScore)) + " "));
					string text3 = (flag4 ? "▲ " : "▼ ") + text2 + hM6DefendedWallLine.Price.ToString("F" + _decs);
					float num28 = Math.Max(10f * dY, num5);
					float num29 = Math.Max(88f * dX, Math.Min(158f * dX, (float)text3.Length * 5.6f * dX));
					float num30 = Math.Max(num + 2f * dX, num2 - num29 - 3f * dX);
					float num31 = (flag4 ? (1f * dY) : (H - num28 - 1f * dY));
					if (_bPanBg != null)
					{
						_bPanBg.Opacity = 0.9f;
						base.RenderTarget.FillRectangle(new RectangleF(num30, num31, num29, num28), _bPanBg);
					}
					solidColorBrush.Opacity = 0.92f;
					base.RenderTarget.DrawRectangle(new RectangleF(num30, num31, num29, num28), solidColorBrush, Math.Max(0.8f, dX));
					_bTxt.Opacity = 0.92f;
					T(text3, num30 + 4f * dX, num31 + 1f * dY, _fSm, _bTxt, num29 - 6f * dX, num28);
				}
			}
		}

		private void DrawPassiveLiquidityWallZones(ChartControl cc, ChartScale cs, float cR, float H, float dX, float dY)
		{
			if (!_pPassive || !_pPassiveWallZones || cc == null || cs == null || base.ChartBars == null || base.RenderTarget == null)
			{
				return;
			}
			List<HM6PassiveZone> wallZoneRenderBuf = _wallZoneRenderBuf;
			wallZoneRenderBuf.Clear();
			lock (_lk)
			{
				if (_passiveZones != null)
				{
					foreach (HM6PassiveZone value in _passiveZones.Values)
					{
						if (value != null)
						{
							wallZoneRenderBuf.Add(value);
						}
					}
				}
			}
			AppendLiveHeatmapDerivedWallZones(cc, cs, cR, H, dX, dY, wallZoneRenderBuf);
			if (wallZoneRenderBuf.Count == 0)
			{
				return;
			}
			int fromIndex = base.ChartBars.FromIndex;
			int toIndex = base.ChartBars.ToIndex;
			if (toIndex < fromIndex)
			{
				return;
			}
			double num = IltDevNowMs;
			int num2 = ((base.CurrentBar >= 0) ? base.CurrentBar : toIndex);
			int num3 = Math.Max(20, Math.Min(2000, _pPassiveWallMaxBars));
			long num4 = Math.Max(1L, _pWallMinQty);
			long num5 = BasicVisualMinQty();
			double maxValue = cs.MaxValue;
			double minValue = cs.MinValue;
			float heatmapRowHeight = GetHeatmapRowHeight(cs, dY, 1f);
			for (int i = 0; i < wallZoneRenderBuf.Count; i++)
			{
				HM6PassiveZone hM6PassiveZone = wallZoneRenderBuf[i];
				if (hM6PassiveZone == null || (hM6PassiveZone.MaxQty < num4 && hM6PassiveZone.AddQty < Math.Max(1, _pPassiveEventMin) && hM6PassiveZone.LastEventQty < Math.Max(1, _pPassiveEventMin) && hM6PassiveZone.EventCount <= 0))
				{
					continue;
				}
				long num6 = Math.Max(1L, _pPassiveEventMin);
				long num7 = Math.Max(num4 * 3, num6 * 2);
				if (!hM6PassiveZone.DerivedWall && hM6PassiveZone.MaxQty < num7 && hM6PassiveZone.AddQty < num6 * 2 && hM6PassiveZone.RefillQty < num6 && hM6PassiveZone.ConsumeQty < num6 * 2 && hM6PassiveZone.PullQty < num6 * 2 && hM6PassiveZone.EventCount < 2 && hM6PassiveZone.LastEventType != 2)
				{
					continue;
				}
				int num8 = Math.Max(0, hM6PassiveZone.StartBar);
				if (num2 - num8 > num3)
				{
					continue;
				}
				int num9 = Math.Max(num2, toIndex);
				if (num9 < num8)
				{
					num9 = num8 + 1;
				}
				if (num9 < fromIndex || num8 > toIndex)
				{
					continue;
				}
				float num10 = cs.GetYByValue(hM6PassiveZone.Price);
				if (num10 < -32f * dY || num10 > H + 32f * dY)
				{
					continue;
				}
				float num11 = cc.GetXByBarIndex(base.ChartBars, Math.Max(fromIndex, num8));
				float num12 = cc.GetXByBarIndex(base.ChartBars, Math.Min(toIndex, num9));
				float num13 = num12;
				float left;
				float right;
				float colW;
				int start;
				int count;
				List<HM6HeatmapSlice> slices;
				bool flag = TryGetLiveHeatmapLayout(cc, cR, dX, out left, out right, out colW, out start, out count, out slices);
				if (flag)
				{
					float x = num11;
					float x2 = num13;
					long num14 = ((slices.Count > 0 && slices[slices.Count - 1] != null) ? slices[slices.Count - 1].Seq : 0);
					if (hM6PassiveZone.RxSeq != num14 || hM6PassiveZone.RxStart != start || hM6PassiveZone.RxCount != count)
					{
						int first;
						int last;
						hM6PassiveZone.RxHas = TryGetLiveHeatmapPresenceIdxRange(slices, start, count, hM6PassiveZone.Price, hM6PassiveZone.WasBid, Math.Max(1L, Math.Min(Math.Max(1, _pF), Math.Max(1, _pPassiveMinQty))), out first, out last);
						hM6PassiveZone.RxFirst = first;
						hM6PassiveZone.RxLast = last;
						hM6PassiveZone.RxSeq = num14;
						hM6PassiveZone.RxStart = start;
						hM6PassiveZone.RxCount = count;
					}
					bool rxHas = hM6PassiveZone.RxHas;
					if (rxHas)
					{
						x = left + (float)(hM6PassiveZone.RxFirst - start) * colW;
						x2 = left + (float)(hM6PassiveZone.RxLast - start + 1) * colW;
					}
					if (rxHas)
					{
						num11 = x;
						num13 = x2;
					}
					else if (TryGetLiveHeatmapXRangeBySeqFast(slices, start, count, left, colW, hM6PassiveZone.FirstSeq, hM6PassiveZone.LastSeq, out x, out x2))
					{
						num11 = x;
						num13 = x2;
					}
					else
					{
						float x3 = num11;
						float x4 = num13;
						bool flag2 = hM6PassiveZone.FirstTime > 0.0 && TryGetLiveHeatmapXByTimeFast(slices, start, count, left, colW, hM6PassiveZone.FirstTime, out x3);
						bool flag3 = hM6PassiveZone.LastEventTime > 0.0 && TryGetLiveHeatmapXByTimeFast(slices, start, count, left, colW, hM6PassiveZone.LastEventTime, out x4);
						if (!flag3 && hM6PassiveZone.LastUpdateTime > 0.0)
						{
							flag3 = TryGetLiveHeatmapXByTimeFast(slices, start, count, left, colW, hM6PassiveZone.LastUpdateTime, out x4);
						}
						if (flag2 || flag3)
						{
							if (flag2)
							{
								num11 = x3;
							}
							num13 = ((!flag3) ? (right - 6f * dX) : x4);
						}
					}
				}
				if (num13 < num11)
				{
					float num15 = num11;
					num11 = num13;
					num13 = num15;
				}
				if (num13 - num11 < 10f * dX)
				{
					num13 = num11 + 10f * dX;
				}
				float num16 = (flag ? left : (255f * dX + 2f));
				if (num13 <= num16 + 2f * dX)
				{
					continue;
				}
				num11 = Math.Max(num16, Math.Min(cR - 10f * dX, num11));
				num13 = Math.Max(num16 + 10f * dX, Math.Min(cR - 8f * dX, num13));
				bool flag4 = IsPassiveSpoofZone(hM6PassiveZone);
				bool flag5 = hM6PassiveZone.LastEventType == 2 && hM6PassiveZone.RefillQty >= Math.Max(1L, _pPassiveEventMin);
				SharpDX.Direct2D1.SolidColorBrush solidColorBrush = (hM6PassiveZone.WasBid ? _bPassiveBid : _bPassiveAsk);
				if (!flag4 && hM6PassiveZone.LastEventType == 1)
				{
					solidColorBrush = _bPassiveWarn ?? solidColorBrush;
				}
				else if (flag5)
				{
					solidColorBrush = _bPassiveRefill ?? solidColorBrush;
				}
				if (solidColorBrush == null)
				{
					continue;
				}
				double num17 = ((hM6PassiveZone.LastUpdateTime > 0.0) ? (num - hM6PassiveZone.LastUpdateTime) : 0.0);
				float num18 = (hM6PassiveZone.Active ? 1f : 0.7f);
				if (hM6PassiveZone.Reached)
				{
					num18 = Math.Max(num18, 0.82f);
				}
				if (hM6PassiveZone.DerivedWall)
				{
					num18 = Math.Max(num18, 0.95f);
				}
				float num19 = ((num17 <= 3500.0) ? ((float)(1.0 - num17 / 3500.0)) : 0f);
				float num20 = ((hM6PassiveZone.LastEventType == 1 || hM6PassiveZone.LastEventType == 2 || hM6PassiveZone.LastEventType == 0) ? 0.2f : 0f);
				if (hM6PassiveZone.DerivedWall)
				{
					num20 = Math.Max(num20, 0.12f);
				}
				float num21 = 1f + Math.Min(1.15f, (float)Math.Log10(1.0 + (double)hM6PassiveZone.MaxQty / (double)Math.Max(1, _pPassiveEventMin)) * 0.42f);
				float num22 = Math.Max(heatmapRowHeight * 0.58f, Math.Min(heatmapRowHeight * 1.1f, (0.72f + 0.18f * (float)Math.Max(1, Math.Min(5, _pPassiveMarkerSize))) * heatmapRowHeight * num21));
				float num23 = Math.Max(2f * dX, num13 - num11);
				float num24 = Math.Max(heatmapRowHeight * 0.72f, Math.Min(heatmapRowHeight * 1.12f, num22));
				float num25 = num10 - num24 * 0.5f;
				float num26 = Math.Max(1.4f * dX, num24 * 0.28f);
				if (_bPanBg != null)
				{
					_bPanBg.Opacity = 0.34f;
					base.RenderTarget.FillRoundedRectangle(new RoundedRectangle
					{
						Rect = new RectangleF(num11 - 1.5f * dX, num25 - 0.8f * dY, num23 + 3f * dX, num24 + 1.6f * dY),
						RadiusX = num26,
						RadiusY = num26
					}, _bPanBg);
				}
				solidColorBrush.Opacity = Math.Min(0.82f, 0.46f + 0.16f * num18 + 0.08f * num19 + num20 * 0.35f);
				base.RenderTarget.FillRoundedRectangle(new RoundedRectangle
				{
					Rect = new RectangleF(num11, num25, num23, num24),
					RadiusX = num26,
					RadiusY = num26
				}, solidColorBrush);
				solidColorBrush.Opacity = Math.Min(1f, 0.82f + 0.1f * num18 + 0.08f * num19 + num20 * 0.55f);
				base.RenderTarget.DrawLine(new Vector2(num11, num10), new Vector2(num13, num10), solidColorBrush, Math.Max(1.35f * dY, num24 * 0.42f));
				solidColorBrush.Opacity = Math.Min(1f, 0.78f + 0.12f * num18 + 0.12f * num19 + num20 * 0.3f);
				base.RenderTarget.DrawRoundedRectangle(new RoundedRectangle
				{
					Rect = new RectangleF(num11, num25, num23, num24),
					RadiusX = num26,
					RadiusY = num26
				}, solidColorBrush, Math.Max(0.7f, 0.85f * dX));
				solidColorBrush.Opacity = Math.Min(1f, 0.86f + 0.1f * num19 + num20 * 0.2f);
				float strokeWidth = Math.Max(0.75f * dY, num24 * 0.15f);
				base.RenderTarget.DrawLine(new Vector2(num11, num25), new Vector2(num13, num25), solidColorBrush, strokeWidth);
				base.RenderTarget.DrawLine(new Vector2(num11, num25 + num24), new Vector2(num13, num25 + num24), solidColorBrush, strokeWidth);
				if (_bTxt != null && (hM6PassiveZone.DerivedWall || hM6PassiveZone.MaxQty >= Math.Max(num5, num7) || hM6PassiveZone.LastEventQty >= num5 || hM6PassiveZone.EventCount >= 2))
				{
					if (flag4 && hM6PassiveZone.PullBurstLast > 0.0 && (double)IltDevNowMs - hM6PassiveZone.PullBurstLast > (double)Math.Max(10, _pTagLifeSec) * 1000.0)
					{
						flag4 = false;
					}
					if (flag4 && !hM6PassiveZone.Active && num19 < 0.06f)
					{
						flag4 = false;
					}
					string text = (flag4 ? ("SPOOF " + FQ(FreezeWallTagQty(hM6PassiveZone.Price, hM6PassiveZone.WasBid, "SPOOF", Math.Max(1L, PassiveZoneDisplayQty(hM6PassiveZone, "SPOOF"))))) : "MURO");
					if (flag5)
					{
						text = "REFILL " + FQ(Math.Max(1L, PassiveZoneDisplayQty(hM6PassiveZone, "REFILL")));
					}
					float num27 = Math.Max(heatmapRowHeight * 0.96f, 10f * dY);
					float num28 = Math.Max(52f * dX, (float)text.Length * 5.4f * dX + 10f * dX);
					float val = num11 + (num23 - num28) * 0.5f;
					float num29 = Math.Max(255f * dX + 3f * dX, Math.Min(cR - num28 - 6f * dX, val));
					float num30 = num10 - num27 * 0.5f;
					if (_bPanBg != null)
					{
						_bPanBg.Opacity = 0.9f;
						base.RenderTarget.FillRoundedRectangle(new RoundedRectangle
						{
							Rect = new RectangleF(num29 - 0.8f * dX, num30 - 0.6f * dY, num28 + 1.6f * dX, num27 + 1.2f * dY),
							RadiusX = Math.Max(1.4f * dX, num27 * 0.18f),
							RadiusY = Math.Max(1.4f * dY, num27 * 0.18f)
						}, _bPanBg);
					}
					solidColorBrush.Opacity = 0.98f;
					base.RenderTarget.DrawRoundedRectangle(new RoundedRectangle
					{
						Rect = new RectangleF(num29, num30, num28, num27),
						RadiusX = Math.Max(1.5f * dX, num27 * 0.2f),
						RadiusY = Math.Max(1.5f * dY, num27 * 0.2f)
					}, solidColorBrush, Math.Max(0.85f, 1.05f * dX));
					base.RenderTarget.DrawLine(new Vector2(num29 + 2f * dX, num10), new Vector2(num29 + num28 - 2f * dX, num10), solidColorBrush, Math.Max(0.75f, 0.75f * dY));
					if (flag4 && _bPassiveWarn != null)
					{
						_bPassiveWarn.Opacity = 0.98f;
						base.RenderTarget.DrawLine(new Vector2(num29 + 2f * dX, num30 + num27 - 1.7f * dY), new Vector2(num29 + num28 - 2f * dX, num30 + num27 - 1.7f * dY), _bPassiveWarn, Math.Max(0.7f, 0.9f * dY));
					}
					_bTxt.Opacity = 0.98f;
					TC(text, num29 + 3f * dX, num30 - 0.4f * dY, _fTiny, _bTxt, num28 - 6f * dX, num27 + 1f * dY);
				}
				float num31 = Math.Max(1f * dX, Math.Min(3f * dX, num24 * 0.22f));
				solidColorBrush.Opacity = Math.Min(0.98f, 0.78f + 0.1f * num19 + num20 * 0.25f);
				base.RenderTarget.FillRectangle(new RectangleF(num11, num25 - heatmapRowHeight * 0.1f, num31, num24 + heatmapRowHeight * 0.2f), solidColorBrush);
				base.RenderTarget.FillRectangle(new RectangleF(num13 - num31, num25 - heatmapRowHeight * 0.1f, num31, num24 + heatmapRowHeight * 0.2f), solidColorBrush);
				if (hM6PassiveZone.EventCount > 0)
				{
					float x5;
					if (!(hM6PassiveZone.LastEventTime > 0.0) || !TryGetLiveHeatmapXByTime(cc, cR, dX, hM6PassiveZone.LastEventTime, out x5))
					{
						x5 = ((hM6PassiveZone.LastEventBar >= fromIndex && hM6PassiveZone.LastEventBar <= toIndex && hM6PassiveZone.LastEventBar >= 0) ? ((float)cc.GetXByBarIndex(base.ChartBars, hM6PassiveZone.LastEventBar)) : num13);
					}
					solidColorBrush.Opacity = Math.Min(0.95f, 0.58f + 0.22f * num19);
					base.RenderTarget.DrawLine(new Vector2(x5, num10 - heatmapRowHeight * 1.25f), new Vector2(x5, num10 + heatmapRowHeight * 1.25f), solidColorBrush, Math.Max(1f * dX, num22 * 0.34f));
				}
			}
		}

		private bool TryGetLiveHeatmapXByTime(ChartControl cc, float cR, float dX, double eventTime, out float x)
		{
			x = 0f;
			float left;
			float right;
			float colW;
			int start;
			int count;
			List<HM6HeatmapSlice> slices;
			if (!TryGetLiveHeatmapLayout(cc, cR, dX, out left, out right, out colW, out start, out count, out slices))
			{
				return false;
			}
			return TryGetLiveHeatmapXByTimeFast(slices, start, count, left, colW, eventTime, out x);
		}

		private void DrawPassiveLiquidityHeatmapEvents(ChartControl cc, ChartScale cs, float cR, float H, float dX, float dY)
		{
			if (!_pPassive || !_pPassiveChartMarkers || _passiveEvents == null || _passiveEvents.Count == 0 || cc == null || cs == null || base.ChartBars == null || base.RenderTarget == null)
			{
				return;
			}
			List<HM6PassiveEvent> passiveRenderBuf = _passiveRenderBuf;
			lock (_lk)
			{
				passiveRenderBuf.Clear();
				if (_passiveEvents != null)
				{
					passiveRenderBuf.AddRange(_passiveEvents);
				}
			}
			if (passiveRenderBuf.Count == 0)
			{
				return;
			}
			int fromIndex = base.ChartBars.FromIndex;
			int toIndex = base.ChartBars.ToIndex;
			if (toIndex < fromIndex)
			{
				return;
			}
			int num = ((base.CurrentBar >= 0) ? base.CurrentBar : toIndex);
			int num2 = Math.Max(20, Math.Min(2000, _pPassiveWallMaxBars));
			long num3 = BasicVisualMinQty();
			BasicVisualMinQty();
			double maxValue = cs.MaxValue;
			double minValue = cs.MinValue;
			float heatmapRowHeight = GetHeatmapRowHeight(cs, dY, 1f);
			if (toIndex > fromIndex)
			{
				Math.Max(2f * dX, cc.GetXByBarIndex(base.ChartBars, fromIndex + 1) - cc.GetXByBarIndex(base.ChartBars, fromIndex));
			}
			double num4 = IltDevNowMs;
			double num5 = Math.Max(900.0, _pPassiveEventFadeMs);
			int num6 = Math.Max(0, passiveRenderBuf.Count - 5000);
			for (int i = num6; i < passiveRenderBuf.Count; i++)
			{
				HM6PassiveEvent hM6PassiveEvent = passiveRenderBuf[i];
				if (hM6PassiveEvent == null || !ShouldRenderMicroEvent(hM6PassiveEvent) || hM6PassiveEvent.Qty < num3)
				{
					continue;
				}
				int num7 = ((hM6PassiveEvent.BarIdx >= 0) ? hM6PassiveEvent.BarIdx : num);
				int num8 = num - num7;
				if (num8 < 0 || num8 > num2)
				{
					continue;
				}
				int num9 = Math.Max(fromIndex, Math.Min(toIndex, num7));
				if (num9 < fromIndex || num9 > toIndex)
				{
					continue;
				}
				float num10 = cs.GetYByValue(hM6PassiveEvent.Price);
				if (num10 < -32f * dY || num10 > H + 32f * dY)
				{
					continue;
				}
				float x;
				if (!TryGetLiveHeatmapXByTime(cc, cR, dX, hM6PassiveEvent.Time, out x))
				{
					x = cc.GetXByBarIndex(base.ChartBars, num9);
				}
				if (x < -80f * dX || x > cR + 80f * dX)
				{
					continue;
				}
				double num11 = Math.Max(0.0, num4 - hM6PassiveEvent.Time);
				double x2 = ((num11 <= num5) ? Math.Max(0.0, Math.Min(1.0, 1.0 - num11 / num5)) : 0.0);
				float num12 = (float)Math.Pow(x2, 0.58);
				float num13 = ((num11 <= num5) ? ((float)Math.Exp((0.0 - num11) / 360.0)) : 0f);
				float val = 1f - Math.Min(1f, (float)Math.Max(0, num8) / (float)Math.Max(1, num2));
				val = Math.Max(0.2f, val);
				float num14 = 1f + Math.Min(1f, (float)Math.Log10(1.0 + (double)hM6PassiveEvent.Qty / (double)num3) * 0.38f);
				float num15 = 0.85f + 0.22f * (float)Math.Max(1, Math.Min(5, _pPassiveMarkerSize));
				float num16 = Math.Max(22f * dX, Math.Min(145f * dX, (28f * dX + 14f * dX * num14) * num15));
				float num17 = Math.Max(heatmapRowHeight * 0.66f, Math.Min(heatmapRowHeight * 1.05f, heatmapRowHeight * (0.68f + 0.08f * num14)));
				float num18 = (hM6PassiveEvent.WasBid ? (0f - Math.Min(num16 * 0.1f, 7f * dX)) : Math.Min(num16 * 0.1f, 7f * dX));
				float num19 = x - num16 * 0.5f + num18;
				float num20 = num10 - num17 * 0.5f;
				SharpDX.Direct2D1.SolidColorBrush solidColorBrush = PassiveEventBrush(hM6PassiveEvent);
				if (solidColorBrush == null)
				{
					continue;
				}
				bool flag = hM6PassiveEvent.Type == 1;
				bool flag2 = hM6PassiveEvent.Type == 0;
				bool flag3 = hM6PassiveEvent.Type == 2;
				int type = hM6PassiveEvent.Type;
				float num21 = (flag ? 0.66f : (flag2 ? 0.58f : (flag3 ? 0.64f : 0.54f)));
				num21 *= 0.62f + 0.38f * val;
				float num22 = Math.Min(0.92f, num21 + 0.12f * num12);
				if (flag2)
				{
					solidColorBrush.Opacity = num22;
					int num23 = 4;
					float num24 = Math.Max(2.5f * dX, num16 * 0.045f);
					float num25 = Math.Max(5f * dX, (num16 - num24 * (float)(num23 - 1)) / (float)num23);
					for (int j = 0; j < num23; j++)
					{
						float x3 = num19 + (float)j * (num25 + num24);
						base.RenderTarget.FillRoundedRectangle(new RoundedRectangle
						{
							Rect = new RectangleF(x3, num20, num25, num17),
							RadiusX = num17 * 0.45f,
							RadiusY = num17 * 0.45f
						}, solidColorBrush);
					}
				}
				else
				{
					solidColorBrush.Opacity = num22;
					base.RenderTarget.FillRoundedRectangle(new RoundedRectangle
					{
						Rect = new RectangleF(num19, num20, num16, num17),
						RadiusX = num17 * 0.45f,
						RadiusY = num17 * 0.45f
					}, solidColorBrush);
				}
				solidColorBrush.Opacity = Math.Min(0.98f, num22 + 0.12f);
				base.RenderTarget.DrawLine(new Vector2(num19, num10), new Vector2(num19 + num16, num10), solidColorBrush, Math.Max(0.75f * dY, Math.Min(2f * dY, num17 * 0.42f)));
				if (num13 > 0.035f)
				{
					solidColorBrush.Opacity = Math.Min(0.16f, 0.12f * num13 + 0.05f * num12);
					base.RenderTarget.DrawRoundedRectangle(new RoundedRectangle
					{
						Rect = new RectangleF(num19 - 5f * dX * (1f - num13), num20 - 5f * dY * (1f - num13), num16 + 10f * dX * (1f - num13), num17 + 10f * dY * (1f - num13)),
						RadiusX = 6f * dX,
						RadiusY = 6f * dY
					}, solidColorBrush, Math.Max(0.65f, 0.85f * dX));
				}
			}
		}

		private void DrawPassiveLiquidityLabelOverlay(ChartControl cc, ChartScale cs, float cR, float H, float dX, float dY)
		{
			float left;
			float right;
			float colW;
			int start;
			int count;
			List<HM6HeatmapSlice> slices;
			if (!_pPassive || !_pPassiveChartMarkers || base.RenderTarget == null || cc == null || cs == null || _bTxt == null || _bPanBg == null || !TryGetLiveHeatmapLayout(cc, cR, dX, out left, out right, out colW, out start, out count, out slices))
			{
				return;
			}
			int num = Math.Min(slices.Count, start + count);
			if (num <= start)
			{
				return;
			}
			long num2 = BasicVisualMinQty();
			float heatmapRowHeight = GetHeatmapRowHeight(cs, dY, 1f);
			int num3 = Math.Max(24, Math.Min(180, 30 + Math.Max(1, _pPassiveMarkerSize) * 22));
			int num4 = 0;
			HashSet<long> lblUsed = _lblUsed;
			lblUsed.Clear();
			int num5 = num - 1;
			while (num5 >= start && num4 < num3)
			{
				HM6HeatmapSlice hM6HeatmapSlice = slices[num5];
				if (hM6HeatmapSlice != null && hM6HeatmapSlice.Events != null && hM6HeatmapSlice.Events.Count != 0)
				{
					float anchorX = left + (float)(num5 - start) * colW + colW * 0.5f;
					int num6 = hM6HeatmapSlice.Events.Count - 1;
					while (num6 >= 0 && num4 < num3)
					{
						HM6PassiveEvent hM6PassiveEvent = hM6HeatmapSlice.Events[num6];
						if (hM6PassiveEvent != null && ShouldRenderMicroEvent(hM6PassiveEvent) && !hM6PassiveEvent.PriceInteracted && hM6PassiveEvent.Qty >= num2)
						{
							float num7 = cs.GetYByValue(hM6PassiveEvent.Price);
							if (!(num7 + heatmapRowHeight < 0f) && !(num7 - heatmapRowHeight > H))
							{
								long item = (long)NormalizeToTickPriceKey(hM6PassiveEvent.Price) * 32L + (hM6PassiveEvent.WasBid ? 16L : 0L) + (long)(hM6PassiveEvent.Type & 0xF);
								if (lblUsed.Add(item))
								{
									DrawNativeHeatmapEventTag(hM6PassiveEvent, anchorX, num7, heatmapRowHeight, cR, dX, dY);
									num4++;
								}
							}
						}
						num6--;
					}
				}
				num5--;
			}
		}

		private void DrawPassiveLiquidityHud(ChartScale cs, float cR, float panX, float dX, float dY)
		{
			if (!_pPassive || base.RenderTarget == null)
			{
				return;
			}
			double num = IltDevNowMs;
			HM6PassiveSummary hM6PassiveSummary;
			if (_passiveSummary != null && num - _passiveSummaryMs <= 150.0)
			{
				hM6PassiveSummary = _passiveSummary;
			}
			else
			{
				hM6PassiveSummary = (_passiveSummary = BuildPassiveLiquiditySummary(num));
				_passiveSummaryMs = num;
			}
			if (hM6PassiveSummary.BidQty <= 0 && hM6PassiveSummary.AskQty <= 0 && hM6PassiveSummary.BidPull <= 0 && hM6PassiveSummary.AskPull <= 0 && hM6PassiveSummary.BidRefill <= 0 && hM6PassiveSummary.AskRefill <= 0)
			{
				return;
			}
			float height = base.RenderTarget.Size.Height;
			float num2 = Math.Max(dX, dY);
			float num3 = Math.Min(145f * dX, Math.Max(86f * dX, (cR - panX) * 0.135f));
			float num4 = Math.Min(480f * dX, Math.Max(370f * dX, (cR - panX) * 0.37f));
			float num5 = Math.Max(216f * dY, 216f);
			float num6 = Math.Max(panX + num4 + 4f * dX, cR - num3 - 8f * dX);
			float num7 = Math.Max(panX + 5f * dX, num6 - num4);
			float num8 = Math.Max(2f * dY, 2f);
			if (num8 + num5 > height - 18f * dY)
			{
				num8 = Math.Max(4f * dY, height - num5 - 18f * dY);
			}
			float num9 = 8f * num2;
			RectangleF rect = new RectangleF(num7, num8, num4, num5);
			RectangleF rect2 = new RectangleF(num7 + 3f * dX, num8 + 4f * dY, num4, num5);
			float num10 = Math.Max(15f * dX, 15f);
			_rPassiveHudMin = new RectangleF(rect.Right - num10 - 8f * dX, rect.Top + 6f * dY, num10, num10);
			if (_passiveHudMinimized)
			{
				float num11 = Math.Max(25f * dY, 25f);
				RectangleF rect3 = new RectangleF(num7, num8, num4, num11);
				_bPanSh.Opacity = 0.34f;
				base.RenderTarget.FillRoundedRectangle(new RoundedRectangle
				{
					Rect = new RectangleF(num7 + 2f * dX, num8 + 3f * dY, num4, num11),
					RadiusX = num9,
					RadiusY = num9
				}, _bPanSh);
				_bPanBg.Opacity = Math.Max(0.58f, OpacityPct(PanelOpacity) * 0.68f);
				base.RenderTarget.FillRoundedRectangle(new RoundedRectangle
				{
					Rect = rect3,
					RadiusX = num9,
					RadiusY = num9
				}, _bPanBg);
				_bPanBord.Opacity = 0.7f;
				base.RenderTarget.DrawRoundedRectangle(new RoundedRectangle
				{
					Rect = rect3,
					RadiusX = num9,
					RadiusY = num9
				}, _bPanBord, Math.Max(1f, 1.1f * dX));
				_bDim.Opacity = 0.9f;
				T("LIQUIDEZ PASIVA", num7 + 10f * dX, num8 + 5f * dY, _fBold, _bDim, num4 - 48f * dX, num11 - 4f * dY);
				DrawPassiveHudMinButton(_rPassiveHudMin, false, dX, dY);
				return;
			}
			_bPanSh.Opacity = 0.42f;
			base.RenderTarget.FillRoundedRectangle(new RoundedRectangle
			{
				Rect = rect2,
				RadiusX = num9,
				RadiusY = num9
			}, _bPanSh);
			_bPanBg.Opacity = Math.Max(0.62f, OpacityPct(PanelOpacity) * 0.72f);
			base.RenderTarget.FillRoundedRectangle(new RoundedRectangle
			{
				Rect = rect,
				RadiusX = num9,
				RadiusY = num9
			}, _bPanBg);
			_bPanBord.Opacity = 0.72f;
			base.RenderTarget.DrawRoundedRectangle(new RoundedRectangle
			{
				Rect = rect,
				RadiusX = num9,
				RadiusY = num9
			}, _bPanBord, Math.Max(1f, 1.2f * dX));
			DrawPassiveHudMinButton(_rPassiveHudMin, true, dX, dY);
			bool flag = hM6PassiveSummary.NetScore >= 0.0;
			double num12 = Math.Abs(hM6PassiveSummary.NetScore);
			bool flag2 = num12 < 0.08 && hM6PassiveSummary.SignalType == 0;
			SharpDX.Direct2D1.SolidColorBrush solidColorBrush = (flag2 ? _bDim : (flag ? _bPassiveBid : _bPassiveAsk));
			float num13 = (float)Math.Round(num12 * 100.0);
			string text = (string.IsNullOrEmpty(hM6PassiveSummary.Signal) ? "SIN EVENTO" : hM6PassiveSummary.Signal);
			string txt = ((hM6PassiveSummary.SignalType != 0 && text != "NEUTRAL" && text != "SIN EVENTO") ? text : ((!flag2) ? ((flag ? "COMPRA PASIVA DOMINA " : "VENTA PASIVA DOMINA ") + num13.ToString("0") + "%") : "EQUILIBRIO PASIVO"));
			_bDim.Opacity = 0.86f;
			T("LIQUIDEZ PASIVA", num7 + 12f * dX, num8 + 6f * dY, _fBold, _bDim, num4 - 54f * dX, 18f * dY);
			solidColorBrush.Opacity = (flag2 ? 0.82f : 0.98f);
			T(txt, num7 + 12f * dX, num8 + 28f * dY, _fPassiveBig ?? _fBold, solidColorBrush, num4 - 24f * dX, 30f * dY);
			float num14 = num7 + 16f * dX;
			float num15 = num8 + 66f * dY;
			float num16 = num4 - 32f * dX;
			float num17 = Math.Max(14f * dY, 14f);
			float num18 = num14 + num16 * 0.5f;
			float num19 = num16 * 0.5f * (float)Clamp01(hM6PassiveSummary.BidScore);
			float width = num16 * 0.5f * (float)Clamp01(hM6PassiveSummary.AskScore);
			_bSlBg.Opacity = 0.66f;
			base.RenderTarget.FillRoundedRectangle(new RoundedRectangle
			{
				Rect = new RectangleF(num14, num15, num16, num17),
				RadiusX = num17 * 0.45f,
				RadiusY = num17 * 0.45f
			}, _bSlBg);
			_bPassiveBid.Opacity = 0.88f;
			base.RenderTarget.FillRoundedRectangle(new RoundedRectangle
			{
				Rect = new RectangleF(num18 - num19, num15, num19, num17),
				RadiusX = num17 * 0.45f,
				RadiusY = num17 * 0.45f
			}, _bPassiveBid);
			_bPassiveAsk.Opacity = 0.88f;
			base.RenderTarget.FillRoundedRectangle(new RoundedRectangle
			{
				Rect = new RectangleF(num18, num15, width, num17),
				RadiusX = num17 * 0.45f,
				RadiusY = num17 * 0.45f
			}, _bPassiveAsk);
			_bPassiveBid.Opacity = 0.96f;
			T("COMPRA PASIVA", num14, num15 - 15f * dY, _fTiny, _bPassiveBid, num16 * 0.48f, 13f * dY);
			_bPassiveAsk.Opacity = 0.96f;
			T("VENTA PASIVA", num14 + num16 * 0.52f, num15 - 15f * dY, _fTiny, _bPassiveAsk, num16 * 0.48f, 13f * dY);
			_bDim.Opacity = 0.72f;
			base.RenderTarget.DrawLine(new Vector2(num18, num15 - 3f * dY), new Vector2(num18, num15 + num17 + 3f * dY), _bDim, Math.Max(1f, 1f * dX));
			float num20 = 12f * dX;
			float y = num8 + 88f * dY;
			float height2 = 50f * dY;
			float num21 = 8f * dX;
			float num22 = (num4 - num20 * 2f - num21) * 0.5f;
			RectangleF rect4 = new RectangleF(num7 + num20, y, num22, height2);
			RectangleF rect5 = new RectangleF(num7 + num20 + num22 + num21, y, num22, height2);
			_bSlBg.Opacity = 0.46f;
			base.RenderTarget.FillRoundedRectangle(new RoundedRectangle
			{
				Rect = rect4,
				RadiusX = 6f * num2,
				RadiusY = 6f * num2
			}, _bSlBg);
			base.RenderTarget.FillRoundedRectangle(new RoundedRectangle
			{
				Rect = rect5,
				RadiusX = 6f * num2,
				RadiusY = 6f * num2
			}, _bSlBg);
			_bPassiveBid.Opacity = 0.62f;
			base.RenderTarget.DrawRoundedRectangle(new RoundedRectangle
			{
				Rect = rect4,
				RadiusX = 6f * num2,
				RadiusY = 6f * num2
			}, _bPassiveBid, 1f * dX);
			_bPassiveAsk.Opacity = 0.62f;
			base.RenderTarget.DrawRoundedRectangle(new RoundedRectangle
			{
				Rect = rect5,
				RadiusX = 6f * num2,
				RadiusY = 6f * num2
			}, _bPassiveAsk, 1f * dX);
			_bPassiveBid.Opacity = 0.96f;
			T("COMPRA PASIVA", rect4.Left + 8f * dX, rect4.Top + 4f * dY, _fSm, _bPassiveBid, num22 - 16f * dX, 13f * dY);
			T(FQ(hM6PassiveSummary.BidQty) + " contratos", rect4.Left + 8f * dX, rect4.Top + 19f * dY, _fPassiveMid ?? _fBold, _bTxt, num22 - 16f * dX, 20f * dY);
			_bDim.Opacity = 0.86f;
			T("BID  |  Estabilidad " + (hM6PassiveSummary.BidStability * 100.0).ToString("0") + "%  |  Niveles " + hM6PassiveSummary.BidLevels, rect4.Left + 8f * dX, rect4.Top + 36f * dY, _fTiny, _bDim, num22 - 16f * dX, 10f * dY);
			_bPassiveAsk.Opacity = 0.96f;
			T("VENTA PASIVA", rect5.Left + 8f * dX, rect5.Top + 4f * dY, _fSm, _bPassiveAsk, num22 - 16f * dX, 13f * dY);
			T(FQ(hM6PassiveSummary.AskQty) + " contratos", rect5.Left + 8f * dX, rect5.Top + 19f * dY, _fPassiveMid ?? _fBold, _bTxt, num22 - 16f * dX, 20f * dY);
			_bDim.Opacity = 0.86f;
			T("ASK  |  Estabilidad " + (hM6PassiveSummary.AskStability * 100.0).ToString("0") + "%  |  Niveles " + hM6PassiveSummary.AskLevels, rect5.Left + 8f * dX, rect5.Top + 36f * dY, _fTiny, _bDim, num22 - 16f * dX, 10f * dY);
			float y2 = num8 + 145f * dY;
			float h = 21f * dY;
			float num23 = 5f * dX;
			float num24 = (num4 - 24f * dX - num23 * 3f) / 4f;
			DrawPassiveChip("RETIRAN", FQ(hM6PassiveSummary.BidPull) + "/" + FQ(hM6PassiveSummary.AskPull), num7 + 12f * dX, y2, num24, h, flag ? _bPassiveBid : _bPassiveAsk, dX, dY);
			DrawPassiveChip("EJECUTAN", FQ(hM6PassiveSummary.BidConsume) + "/" + FQ(hM6PassiveSummary.AskConsume), num7 + 12f * dX + (num24 + num23), y2, num24, h, _bPassiveWarn, dX, dY);
			DrawPassiveChip("REFILL", hM6PassiveSummary.BidRefillX.ToString("0.0") + "x/" + hM6PassiveSummary.AskRefillX.ToString("0.0") + "x", num7 + 12f * dX + (num24 + num23) * 2f, y2, num24, h, _bPassiveRefill, dX, dY);
			DrawPassiveChip("FUERZA", (hM6PassiveSummary.BidScore * 100.0).ToString("0") + "/" + (hM6PassiveSummary.AskScore * 100.0).ToString("0"), num7 + 12f * dX + (num24 + num23) * 3f, y2, num24, h, solidColorBrush, dX, dY);
			RectangleF rect6 = new RectangleF(num7 + 12f * dX, num8 + 171f * dY, num4 - 24f * dX, 18f * dY);
			SharpDX.Direct2D1.SolidColorBrush solidColorBrush2 = (flag2 ? _bDim : (hM6PassiveSummary.SignalBid ? _bPassiveBid : _bPassiveAsk));
			if (hM6PassiveSummary.SignalType == 2)
			{
				solidColorBrush2 = _bPassiveWarn;
			}
			solidColorBrush2.Opacity = (flag2 ? 0.5f : 0.72f);
			base.RenderTarget.FillRoundedRectangle(new RoundedRectangle
			{
				Rect = rect6,
				RadiusX = 5f * num2,
				RadiusY = 5f * num2
			}, solidColorBrush2);
			_bTxt.Opacity = (flag2 ? 0.74f : 0.98f);
			if (_hudPipeTxt == null || IltDevNowMs - _hudPipeMs > 300)
			{
				int num25 = 0;
				int num26 = 0;
				int num27 = 0;
				int num28 = 0;
				int num29 = 0;
				int num30;
				int num31;
				int num32;
				int num33;
				int num34;
				int num35;
				int num36;
				int num37;
				lock (_lk)
				{
					if (_defendedWallLines != null)
					{
						for (int i = 0; i < _defendedWallLines.Count; i++)
						{
							HM6DefendedWallLine hM6DefendedWallLine = _defendedWallLines[i];
							if (hM6DefendedWallLine != null && !hM6DefendedWallLine.Discard)
							{
								if (hM6DefendedWallLine.Broken)
								{
									num28++;
								}
								else if (hM6DefendedWallLine.Confirmed)
								{
									num27++;
								}
								else if (hM6DefendedWallLine.PreConfirmed)
								{
									num26++;
								}
								else
								{
									num25++;
								}
							}
						}
					}
					if (_srZones != null)
					{
						for (int j = 0; j < _srZones.Count; j++)
						{
							if (_srZones[j] != null && _srZones[j].State < 2)
							{
								num29++;
							}
						}
					}
					num30 = _zoneTallyProven[0];
					num31 = _zoneTallyProven[1];
					num32 = _zoneTallyProven[2];
					num33 = _zoneTallyProven[3];
					num34 = _zoneTallyBroken[0];
					num35 = _zoneTallyBroken[1];
					num36 = _zoneTallyBroken[2];
					num37 = _zoneTallyBroken[3];
				}
				_hudPipeTxt = "  ·  Z " + num25 + "/" + num26 + "/" + num27 + "/" + num28 + "  SR " + num29;
				if (num30 + num31 + num32 + num33 + num34 + num35 + num36 + num37 > 0)
				{
					_hudTallyTxt = "F PROB/ROTA   90+ " + num33 + "/" + num37 + "    80 " + num32 + "/" + num36 + "    70 " + num31 + "/" + num35 + "    60 " + num30 + "/" + num34;
				}
				else
				{
					_hudTallyTxt = null;
				}
				_hudPipeMs = IltDevNowMs;
			}
			string txt2 = (flag2 ? "EVENTO: SIN EVENTO / ESPERANDO DESEQUILIBRIO" : ("EVENTO: " + text)) + _hudPipeTxt;
			TC(txt2, rect6.Left + 6f * dX, rect6.Top + 1f * dY, _fSm, _bTxt, rect6.Width - 12f * dX, rect6.Height);
			if (_hudTallyTxt != null)
			{
				RectangleF rectangleF = new RectangleF(num7 + 12f * dX, num8 + 192f * dY, num4 - 24f * dX, 16f * dY);
				_bTxt.Opacity = 0.82f;
				TC(_hudTallyTxt, rectangleF.Left + 6f * dX, rectangleF.Top, _fSm, _bTxt, rectangleF.Width - 12f * dX, rectangleF.Height);
			}
		}

		private void DrawPassiveHudMinButton(RectangleF rc, bool expanded, float dX, float dY)
		{
			if (base.RenderTarget != null)
			{
				float num = Math.Max(dX, dY);
				_bSlBg.Opacity = 0.72f;
				base.RenderTarget.FillRoundedRectangle(new RoundedRectangle
				{
					Rect = rc,
					RadiusX = 4f * num,
					RadiusY = 4f * num
				}, _bSlBg);
				_bPanBord.Opacity = 0.82f;
				base.RenderTarget.DrawRoundedRectangle(new RoundedRectangle
				{
					Rect = rc,
					RadiusX = 4f * num,
					RadiusY = 4f * num
				}, _bPanBord, Math.Max(0.8f, 0.9f * dX));
				_bTxt.Opacity = 0.94f;
				string txt = (expanded ? "–" : "+");
				TC(txt, rc.Left, rc.Top - 1f * dY, _fBold, _bTxt, rc.Width, rc.Height + 2f * dY);
			}
		}

		private void DrawPassiveChip(string label, string value, float x, float y, float w, float h, SharpDX.Direct2D1.SolidColorBrush accent, float dX, float dY)
		{
			if (base.RenderTarget != null)
			{
				float num = Math.Max(dX, dY);
				_bSlBg.Opacity = 0.4f;
				base.RenderTarget.FillRoundedRectangle(new RoundedRectangle
				{
					Rect = new RectangleF(x, y, w, h),
					RadiusX = 5f * num,
					RadiusY = 5f * num
				}, _bSlBg);
				accent.Opacity = 0.78f;
				base.RenderTarget.DrawLine(new Vector2(x + 4f * dX, y + h - 2f * dY), new Vector2(x + w - 4f * dX, y + h - 2f * dY), accent, Math.Max(1f, 1f * dX));
				_bDim.Opacity = 0.86f;
				T(label, x + 5f * dX, y + 2f * dY, _fTiny, _bDim, w - 10f * dX, h * 0.48f);
				_bTxt.Opacity = 0.96f;
				T(value, x + 5f * dX, y + 9f * dY, _fTiny, _bTxt, w - 10f * dX, h * 0.48f);
			}
		}

		private bool IsLadderStacking(HM6Level lv, long qty, double now)
		{
			if (lv == null || !_pSt || qty <= 0)
			{
				return false;
			}
			long num = Math.Max(1L, _pPassiveEventMin);
			long num2 = Math.Max(1L, _pLadF);
			bool flag = lv.LastAddTime > 0.0 && now - lv.LastAddTime <= 2500.0;
			bool flag2 = lv.StackCnt >= Math.Max(3, 3);
			bool flag3 = lv.RecentAddQty >= Math.Max(num * 2, (long)Math.Ceiling((double)Math.Max(1L, lv.MaxQty) * 0.15));
			bool flag4 = qty >= num2;
			bool result = lv.LastPullTime <= 0.0 || now - lv.LastPullTime > 1200.0;
			if (flag && flag2 && flag3 && flag4)
			{
				return result;
			}
			return false;
		}

		private bool TryEmitStackingMarkerLocked(HM6Level lv, double price, bool wasBid, long qty, double now)
		{
			if (lv == null || qty <= 0)
			{
				return false;
			}
			long num = Math.Max(1L, _pPassiveEventMin);
			long num2 = Math.Max(num * 2, (long)Math.Ceiling((double)Math.Max(1L, lv.MaxQty) * 0.15));
			if (!(lv.LastAddTime > 0.0) || !(now - lv.LastAddTime <= 2500.0) || lv.StackCnt < 3 || lv.RecentAddQty < num2 || qty < Math.Max(1L, _pLadF))
			{
				return false;
			}
			bool flag = lv.LastStackMarkerTime <= 0.0 || now - lv.LastStackMarkerTime >= 700.0;
			bool flag2 = lv.RecentAddQty >= lv.LastStackMarkerQty + num;
			if (!flag && !flag2)
			{
				return false;
			}
			lv.LastStackMarkerTime = now;
			lv.LastStackMarkerQty = lv.RecentAddQty;
			return AddPassiveEventLocked(price, lv.RecentAddQty, wasBid, 4, now);
		}

		private bool IsLadderPulling(HM6Level lv, long qty, double now)
		{
			if (lv == null || !_pPull || qty <= 0)
			{
				return false;
			}
			long val = Math.Max(1L, _pPassiveEventMin);
			bool flag = lv.LastPullTime > 0.0 && now - lv.LastPullTime <= 1800.0;
			bool flag2 = lv.RecentPullQty >= Math.Max(val, (long)Math.Ceiling((double)Math.Max(1L, lv.MaxQty) * 0.2));
			bool result = lv.LastConsumeTime <= 0.0 || lv.LastPullTime > lv.LastConsumeTime;
			if (flag && flag2)
			{
				return result;
			}
			return false;
		}

		private bool IsLadderAbsorption(HM6Level lv, double price, long qty, double now)
		{
			if (lv == null || !_pAbs || qty <= 0)
			{
				return false;
			}
			long num = Math.Max(1L, _pPassiveEventMin);
			long val = Math.Max(1L, Math.Max(_pLadF, _pPassiveMinQty));
			long val2 = Math.Max(1L, (long)Math.Ceiling((double)Math.Max(1L, lv.MaxQty) * 0.25));
			bool flag = lv.LastAggTime > 0.0 && now - lv.LastAggTime <= Math.Max(1200.0, (double)_pPassiveAggWindowMs + 700.0);
			bool flag2 = lv.LastConsumeTime > 0.0 && now - lv.LastConsumeTime <= 1800.0 && lv.RecentConsumeQty >= num;
			bool flag3 = lv.LastAbsorbTime > 0.0 && now - lv.LastAbsorbTime <= 1800.0 && lv.RecentAbsorbedVol >= num;
			bool flag4 = qty >= Math.Max(val, val2);
			double num2 = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25);
			bool result = _midPx <= 0.0 || Math.Abs(price - _midPx) <= num2 * 6.0;
			if (flag && (flag2 || flag3) && flag4)
			{
				return result;
			}
			return false;
		}

		private void DrawLadder(ChartScale cs, float cpW, float H, float dX, float dY)
		{
			float num = Math.Max(dX, dY);
			float num2 = 116f * dX;
			float num3 = 255f * dX + 2f;
			float val = Math.Max(0f, cpW - num2 - (num3 + 70f * dX));
			float num4 = Math.Max(0f, Math.Min(val, (float)_pLadderShiftPx * dX));
			float num5 = cpW - num2 - num4;
			float num6 = 58f * dX;
			float num7 = 0f * dX;
			float num8 = 58f * dX;
			float num9 = 20f * dY;
			float num10 = num5 + num6;
			float num11 = num5 + num6 + num7;
			float num12 = Math.Max(0f, Math.Min(1f, (float)_pLadderBgOpacity / 100f));
			if (num12 > 0.001f)
			{
				_bLadBg.Opacity = num12;
				base.RenderTarget.FillRectangle(new RectangleF(num5, 0f, num2, H), _bLadBg);
			}
			_bLadSep.Opacity = 0.55f * Math.Max(0.35f, num12 + 0.25f);
			base.RenderTarget.DrawLine(new Vector2(num5, 0f), new Vector2(num5, H), _bLadSep, 1.5f * dX);
			_bLadSep.Opacity = 0.26f;
			base.RenderTarget.FillRectangle(new RectangleF(num5, 0f, num2, num9), _bLadSep);
			_bDim.Opacity = 1f;
			TC("BID", num5 + 2f * dX, 0f, _fLad ?? _fSm, _bDim, num6 - 4f * dX, num9);
			TC("ASK", num11 + 2f * dX, 0f, _fLad ?? _fSm, _bDim, num8 - 4f * dX, num9);
			_bLadSep.Opacity = 0.18f;
			base.RenderTarget.DrawLine(new Vector2(num10, num9), new Vector2(num10, H), _bLadSep, Math.Max(0.7f, 0.8f * dX));
			if (_maxQty < 1)
			{
				_bDim.Opacity = 0.3f;
				T("Esperando DOM...", num5 + 6f, H * 0.45f, _fSm, _bDim, num2, 20f);
				return;
			}
			SortedDictionary<double, long[]> sortedDictionary = new SortedDictionary<double, long[]>(Comparer<double>.Create((double a, double b) => b.CompareTo(a)));
			long num13 = 1L;
			long num14 = 1L;
			bool lockTaken = false;
			object obj = null;
			try
			{
				Monitor.Enter(obj = _lk, ref lockTaken);
				foreach (KeyValuePair<double, HM6Level> item in _bid)
				{
					if (!sortedDictionary.ContainsKey(item.Key))
					{
						sortedDictionary[item.Key] = new long[2];
					}
					sortedDictionary[item.Key][0] = item.Value.Qty;
					if (item.Value.Qty > num13)
					{
						num13 = item.Value.Qty;
					}
				}
				foreach (KeyValuePair<double, HM6Level> item2 in _ask)
				{
					if (!sortedDictionary.ContainsKey(item2.Key))
					{
						sortedDictionary[item2.Key] = new long[2];
					}
					sortedDictionary[item2.Key][1] = item2.Value.Qty;
					if (item2.Value.Qty > num14)
					{
						num14 = item2.Value.Qty;
					}
				}
			}
			finally
			{
				if (lockTaken)
				{
					Monitor.Exit(obj);
				}
			}
			if (sortedDictionary.Count == 0)
			{
				_bDim.Opacity = 0.3f;
				T("Esperando DOM...", num5 + 6f, H * 0.45f, _fSm, _bDim, num2, 20f);
				return;
			}
			double num15 = (cs.MaxValue + cs.MinValue) * 0.5;
			float val2 = Math.Abs((float)cs.GetYByValue(num15) - (float)cs.GetYByValue(num15 + _tick * (double)Math.Max(1, _pG)));
			float num16 = Math.Max(6f, Math.Min(24f, DomLadderFontSize)) * num;
			float val3 = num16 + 4f * dY;
			float val4 = Math.Max(val3, 22f * dY);
			float num17 = Math.Max(val3, Math.Min(val4, val2));
			float num18 = num16 + 1f * dY;
			long num19 = Math.Max(1L, _pLadF);
			foreach (KeyValuePair<double, long[]> item3 in sortedDictionary)
			{
				double key = item3.Key;
				long num20 = item3.Value[0];
				long num21 = item3.Value[1];
				float num22 = cs.GetYByValue(key);
				if (num22 < num9 || num22 > H)
				{
					continue;
				}
				float num23 = num22 - num17 * 0.5f;
				bool flag = Math.Abs(key - _midPx) < _tick * 0.6;
				bool flag2 = num20 > 0 && num20 >= num19;
				bool flag3 = num21 > 0 && num21 >= num19;
				if (!flag && !flag2 && !flag3)
				{
					continue;
				}
				bool flag4 = false;
				bool flag5 = false;
				bool flag6 = false;
				bool flag7 = false;
				bool flag8 = false;
				bool flag9 = false;
				double now = IltDevNowMs;
				bool lockTaken2 = false;
				try
				{
					Monitor.Enter(obj = _lk, ref lockTaken2);
					HM6Level value;
					if (_bid.TryGetValue(key, out value))
					{
						flag8 = IsLadderAbsorption(value, key, num20, now);
						flag6 = !flag8 && IsLadderPulling(value, num20, now);
						flag4 = !flag8 && !flag6 && IsLadderStacking(value, num20, now);
					}
					if (_ask.TryGetValue(key, out value))
					{
						flag9 = IsLadderAbsorption(value, key, num21, now);
						flag7 = !flag9 && IsLadderPulling(value, num21, now);
						flag5 = !flag9 && !flag7 && IsLadderStacking(value, num21, now);
					}
				}
				finally
				{
					if (lockTaken2)
					{
						Monitor.Exit(obj);
					}
				}
				if (flag)
				{
					_bRowCur.Opacity = 0.2f;
					base.RenderTarget.FillRectangle(new RectangleF(num5, num23, num2, num17), _bRowCur);
				}
				if (num20 > 0)
				{
					float num24 = (float)num20 / (float)num13;
					SharpDX.Direct2D1.SolidColorBrush solidColorBrush = (flag8 ? _bAbs : (flag6 ? (_bPassiveBid ?? _bLadBid) : (flag4 ? _bLadSt : _bLadBid)));
					solidColorBrush.Opacity = (flag2 ? 0.85f : 0.3f);
					float num25 = 3f * dX;
					float num26 = Math.Max(1f, num6 - num25 * 2f);
					float num27 = num26 * num24;
					float x = (_pDir ? (num10 - num25 - num27) : (num5 + num25));
					base.RenderTarget.FillRectangle(new RectangleF(x, num23 + 1f, num27, num17 - 2f), solidColorBrush);
					if (num17 >= num18)
					{
						SharpDX.Direct2D1.SolidColorBrush solidColorBrush2 = (flag2 ? _bLadHl : _bLadTxt);
						solidColorBrush2.Opacity = (flag2 ? 1f : 0.82f);
						string txt = (flag8 ? "A " : (flag6 ? "P " : (flag4 ? "S " : ""))) + FQ(num20);
						TC(txt, num5 + 2f * dX, num23, _fLad ?? _fTiny ?? _fSm, solidColorBrush2, num6 - 4f * dX, num17);
					}
				}
				if (num21 > 0)
				{
					float num28 = (float)num21 / (float)num14;
					SharpDX.Direct2D1.SolidColorBrush solidColorBrush3 = (flag9 ? _bAbs : (flag7 ? (_bPassiveAsk ?? _bLadAsk) : (flag5 ? _bLadSt : _bLadAsk)));
					solidColorBrush3.Opacity = (flag3 ? 0.85f : 0.3f);
					float num29 = 3f * dX;
					float num30 = Math.Max(1f, num8 - num29 * 2f);
					float num31 = num30 * num28;
					float x2 = (_pDir ? (num11 + num29) : (num5 + num6 + num7 + num8 - num29 - num31));
					base.RenderTarget.FillRectangle(new RectangleF(x2, num23 + 1f, num31, num17 - 2f), solidColorBrush3);
					if (num17 >= num18)
					{
						SharpDX.Direct2D1.SolidColorBrush solidColorBrush4 = (flag3 ? _bLadHl : _bLadTxt);
						solidColorBrush4.Opacity = (flag3 ? 1f : 0.82f);
						string txt2 = (flag9 ? "A " : (flag7 ? "P " : (flag5 ? "S " : ""))) + FQ(num21);
						TC(txt2, num11 + 2f * dX, num23, _fLad ?? _fTiny ?? _fSm, solidColorBrush4, num8 - 4f * dX, num17);
					}
				}
				float num32 = Math.Max(0.3f, num12 + 0.2f);
				_bLadSep.Opacity = (flag ? 0.38f : ((flag2 || flag3) ? 0.18f : 0.07f)) * num32;
				base.RenderTarget.DrawLine(new Vector2(num5, num23), new Vector2(num5 + num2, num23), _bLadSep, flag ? 1.2f : ((flag2 || flag3) ? 0.6f : 0.35f));
			}
		}

		private void DrawPanel(float W, float H, float dX, float dY)
		{
			float num = 255f * dX;
			float num2 = 6f * dX;
			float num3 = 10f * dX;
			float num4 = Math.Min(H - num2 * 2f, _col ? Math.Max(24f * dY, 24f) : Math.Max(635f * dY, 635f));
			if (_panelX < 0f)
			{
				_panelX = num2;
			}
			if (_panelY < 0f)
			{
				_panelY = num2;
			}
			_lastPanelCanvasW = W;
			_lastPanelCanvasH = H;
			_lastPanelW = num;
			_lastPanelH = num4;
			ClampPanelPosition(num2);
			float panelX = _panelX;
			float panelY = _panelY;
			_rPan = new RectangleF(panelX, panelY, num, num4);
			_bPanSh.Opacity = 0f;
			_bPanBg.Opacity = OpacityPct(PanelOpacity);
			base.RenderTarget.FillRectangle(_rPan, _bPanBg);
			_bPanBord.Opacity = 0.5f;
			base.RenderTarget.DrawRectangle(_rPan, _bPanBord, 1f);
			float num5 = Math.Max(24f * dY, 24f);
			_rTitle = new RectangleF(panelX, panelY, num, num5);
			_bPanBord.Opacity = 0.18f;
			base.RenderTarget.FillRectangle(_rTitle, _bPanBord);
			_bTxt.Opacity = 1f;
			T((_col ? "[+]" : "[−]") + " ILT-DEV DOMHEATMAP DEV", panelX + num3, panelY + 5f * dY, _fBold, _bTxt, num - num3, num5);
			if (!_col)
			{
				float num6 = panelX + num3;
				float cy = panelY + num5 + 4f * dY;
				float num7 = num - num3 * 2f;
				_stepMinus.Clear();
				_stepPlus.Clear();
				cy = SH("HEATMAP / LADDER", num6, cy, num7, dX, dY);
				cy = SR("f", "MIN HEATMAP", _pF, 1, 500, num6, cy, num7, dX, dY, ref _rSF);
				cy = SR("heatpalette", "PALETA", _pHeatColorMode, 0, 6, num6, cy, num7, dX, dY, ref _rSL);
				cy = SR("ladf", "MIN LADDER", _pLadF, 1, 500, num6, cy, num7, dX, dY, ref _rSLadF);
				cy = SR("ladx", "LADDER X IZQ", _pLadderShiftPx, 0, 10000, num6, cy, num7, dX, dY, ref _rSLadX);
				cy = SR("g", "TICK FIJO", 1, 1, 1, num6, cy, num7, dX, dY, ref _rSG);
				cy += 3f * dY;
				cy = SH("T&S / EVENTOS", num6, cy, num7, dX, dY);
				cy = SR("bub", "MIN CAPTURA", _pBub, 1, 200, num6, cy, num7, dX, dY, ref _rSBub);
				cy += 4f * dY;
				cy = SH("MARKET PULSE", num6, cy, num7, dX, dY);
				_rTFlow = RC(num6, cy, num7, 21f * dY);
				Tog(_rTFlow, "DOM FLOW PULSE", _pFlow);
				cy += 25f * dY;
				cy = SR("flowmin", "FLOW MIN", _pFlowMin, 1, 500, num6, cy, num7, dX, dY, ref _rSFlowMin);
				cy += 4f * dY;
				cy = SH("VISUALIZACION", num6, cy, num7, dX, dY);
				float num8 = (num7 - 5f * dX) * 0.5f;
				float num9 = 21f * dY;
				_rTLad = RC(num6, cy, num8, num9);
				_rTLin = RC(num6 + num8 + 5f * dX, cy, num8, num9);
				Tog(_rTLad, "LADDER", _pLad);
				Tog(_rTLin, "MARKET PULSE", _pLin);
				cy += num9 + 4f * dY;
				_rTSt = RC(num6, cy, num8, num9);
				_rTPull = RC(num6 + num8 + 5f * dX, cy, num8, num9);
				Tog(_rTSt, "STACKING", _pSt);
				Tog(_rTPull, "PULLING", _pPull);
				cy += num9 + 4f * dY;
				_rTAbs = RC(num6, cy, num7, num9);
				Tog(_rTAbs, "ABSORCION", _pAbs);
				cy += num9 + 4f * dY;
				_rTDir = RC(num6, cy, num7, num9);
				Tog(_rTDir, _pDir ? "BI-DIRECCIONAL (Bid← Ask→)" : "UNI-DIRECCIONAL", _pDir);
				cy += num9 + 4f * dY;
				_rTPoc = RC(num6, cy, num7, num9);
				Tog(_rTPoc, "DOM POC", _pPoc);
			}
		}

		private float SH(string t, float cx, float cy, float sw, float dX, float dY)
		{
			float num = 15f * dY;
			_bSect.Opacity = 0.3f;
			base.RenderTarget.FillRectangle(new RectangleF(cx - 2f, cy, sw + 4f, num), _bSect);
			_bDim.Opacity = 0.9f;
			T(t, cx + 3f * dX, cy + 1f * dY, _fBold, _bDim, sw, num);
			return cy + num + 4f * dY;
		}

		private float SR(string key, string lbl, int val, int lo, int hi, float cx, float cy, float sw, float dX, float dY, ref RectangleF rc)
		{
			float num = Math.Max(20f * dY, 20f);
			rc = new RectangleF(cx, cy, sw, num);
			_bDim.Opacity = 0.72f;
			T(lbl, cx, cy + 2f * dY, _fSm, _bDim, sw * 0.55f, num);
			float num2 = Math.Max(21f * dX, 21f);
			float num3 = Math.Max(4f * dX, 4f);
			float num4 = Math.Max(68f * dX, 68f);
			float num5 = cx + sw - (num2 * 2f + num4 + num3 * 2f);
			float y = cy + Math.Max(1f * dY, 1f);
			float height = Math.Max(18f * dY, 18f);
			RectangleF rectangleF = new RectangleF(num5, y, num2, height);
			RectangleF rect = new RectangleF(num5 + num2 + num3, y, num4, height);
			RectangleF rectangleF2 = new RectangleF(num5 + num2 + num3 + num4 + num3, y, num2, height);
			_stepMinus[key] = rectangleF;
			_stepPlus[key] = rectangleF2;
			_bSlBg.Opacity = 0.28f;
			base.RenderTarget.FillRectangle(rectangleF, _bSlBg);
			base.RenderTarget.FillRectangle(rect, _bSlBg);
			base.RenderTarget.FillRectangle(rectangleF2, _bSlBg);
			_bPanBord.Opacity = 0.34f;
			base.RenderTarget.DrawRectangle(rectangleF, _bPanBord, 0.8f * dX);
			base.RenderTarget.DrawRectangle(rect, _bPanBord, 0.8f * dX);
			base.RenderTarget.DrawRectangle(rectangleF2, _bPanBord, 0.8f * dX);
			_bTxt.Opacity = 0.95f;
			TC("−", rectangleF.X, rectangleF.Y, _fBold, _bTxt, rectangleF.Width, rectangleF.Height);
			TC(val.ToString(), rect.X, rect.Y, _fBold, _bTxt, rect.Width, rect.Height);
			TC("+", rectangleF2.X, rectangleF2.Y, _fBold, _bTxt, rectangleF2.Width, rectangleF2.Height);
			return cy + num + 5f * dY;
		}

		private RectangleF RC(float x, float y, float w, float h)
		{
			return new RectangleF(x, y, w, h);
		}

		private void Tog(RectangleF rc, string lbl, bool on)
		{
			SharpDX.Direct2D1.SolidColorBrush solidColorBrush = (on ? _bOn : _bSlBg);
			solidColorBrush.Opacity = (on ? 0.62f : 0.22f);
			base.RenderTarget.FillRectangle(rc, solidColorBrush);
			_bPanBord.Opacity = 0.42f;
			base.RenderTarget.DrawRectangle(rc, _bPanBord, 1f);
			_bTxt.Opacity = (on ? 1f : 0.32f);
			T((on ? "■ " : "□ ") + lbl, rc.X + 5f, rc.Y + 3f, _fSm, _bTxt, rc.Width - 5f, rc.Height);
		}

		private void MD(object s, MouseButtonEventArgs e)
		{
			System.Windows.Point position = e.GetPosition(base.ChartControl);
			float num = (float)position.X * _ldX;
			float num2 = (float)position.Y * _ldY;
			if (_pPassive && Hit(_rPassiveHudMin, num, num2))
			{
				_passiveHudMinimized = !_passiveHudMinimized;
				e.Handled = true;
				RequestRefresh(true);
			}
			else
			{
				if (UseWpfPanel || (!Hit(_rPan, num, num2) && !HitPanelControl(num, num2)))
				{
					return;
				}
				e.Handled = true;
				if (Hit(_rTitle, num, num2))
				{
					_drag = true;
					_dId = "panel";
					_dX0 = num;
					_dY0 = num2;
					_panelStartX = _panelX;
					_panelStartY = _panelY;
					try
					{
						if (base.ChartControl != null)
						{
							((UIElement)base.ChartControl).CaptureMouse();
						}
						return;
					}
					catch
					{
						return;
					}
				}
				if (_col)
				{
					return;
				}
				foreach (KeyValuePair<string, RectangleF> stepMinu in _stepMinus)
				{
					if (Hit(stepMinu.Value, num, num2))
					{
						ApplyStep(stepMinu.Key, -1);
						return;
					}
				}
				foreach (KeyValuePair<string, RectangleF> stepPlu in _stepPlus)
				{
					if (Hit(stepPlu.Value, num, num2))
					{
						ApplyStep(stepPlu.Key, 1);
						return;
					}
				}
				if (Hit(_rTLad, num, num2))
				{
					_pLad = !_pLad;
					VerLadder = _pLad;
					ApplyRuntimeChange(false);
				}
				else if (Hit(_rTLin, num, num2))
				{
					_pLin = !_pLin;
					VerLineas = _pLin;
					ApplyRuntimeChange(false);
				}
				else if (Hit(_rTSt, num, num2))
				{
					_pSt = !_pSt;
					VerStacking = _pSt;
					ApplyRuntimeChange(false);
				}
				else if (Hit(_rTPull, num, num2))
				{
					_pPull = !_pPull;
					VerPulling = _pPull;
					ApplyRuntimeChange(false);
				}
				else if (Hit(_rTAbs, num, num2))
				{
					_pAbs = !_pAbs;
					VerAbsorcion = _pAbs;
					ApplyRuntimeChange(false);
				}
				else if (Hit(_rTFlow, num, num2))
				{
					_pFlow = !_pFlow;
					VerDomFlowPulse = _pFlow;
					ApplyRuntimeChange(false);
				}
				else if (Hit(_rTDir, num, num2))
				{
					_pDir = !_pDir;
					Bidireccional = _pDir;
					ApplyRuntimeChange(false);
				}
				else if (Hit(_rTPoc, num, num2))
				{
					_pPoc = !_pPoc;
					VerPOC = _pPoc;
					ApplyRuntimeChange(false);
				}
			}
		}

		private void MM(object s, MouseEventArgs e)
		{
			try
			{
				System.Windows.Point position = e.GetPosition(base.ChartControl);
				_hoverX = (float)position.X * _ldX;
				_hoverY = (float)position.Y * _ldY;
			}
			catch
			{
			}
			if (!UseWpfPanel && _drag)
			{
				System.Windows.Point position2 = e.GetPosition(base.ChartControl);
				float num = (float)position2.X * _ldX;
				float num2 = (float)position2.Y * _ldY;
				if (_dId == "panel")
				{
					_panelX = _panelStartX + (num - _dX0);
					_panelY = _panelStartY + (num2 - _dY0);
					ClampPanelPosition(6f * _ldX);
					RequestRefresh(true);
				}
				e.Handled = true;
			}
		}

		private void ApplyStep(string id, int dir)
		{
			int num = 1;
			if (id == "flowfade")
			{
				num = 100;
			}
			if (id == "ladx")
			{
				num = 50;
			}
			if (id == "passdepth")
			{
				num = 1;
			}
			switch (id)
			{
			case "passwallbars":
			case "zonebars":
			case "zonehist":
			case "srretain":
				num = 20;
				break;
			}
			if (id == "renderwin")
			{
				num = 5;
			}
			if (id == "zoneattack")
			{
				num = 1;
			}
			if (id == "zonepct" || id == "srpct")
			{
				num = 10;
			}
			if (id == "zoneadapt" || id == "zonestrength")
			{
				num = 5;
			}
			if (id == "zonemergesec")
			{
				num = 10;
			}
			if (id == "srreadd" || id == "srstrength")
			{
				num = 5;
			}
			if (id == "srwindow")
			{
				num = 500;
			}
			if (id == "srage")
			{
				num = 100;
			}
			if (id == "srfresh")
			{
				num = 100;
			}
			int num2 = dir * num;
			switch (id)
			{
			case "f":
				FiltroQty = Clamp(_pF + num2, 1, 500);
				_pF = FiltroQty;
				break;
			case "renderwin":
				VentanaRenderPct = Clamp(_pRenderWinPct + num2, 15, 100);
				_pRenderWinPct = VentanaRenderPct;
				break;
			case "heatspeed":
				HeatmapScrollSpeed = Clamp(_pHeatmapScrollSpeed + num2, 1, 10);
				_pHeatmapScrollSpeed = HeatmapScrollSpeed;
				_lastLiveHeatmapSliceMs = -100000000;
				break;
			case "heatpalette":
				_pHeatColorMode += dir;
				if (_pHeatColorMode < 0)
				{
					_pHeatColorMode = 6;
				}
				else if (_pHeatColorMode > 6)
				{
					_pHeatColorMode = 0;
				}
				PaletaHeatmap = (IltDevHeatmapPaletteDev)_pHeatColorMode;
				_hmBmpFail = false;
				break;
			case "ladf":
				LadderFiltroQty = Clamp(_pLadF + num2, 1, 500);
				_pLadF = LadderFiltroQty;
				break;
			case "ladx":
				LadderOffsetPx = Clamp(_pLadderShiftPx + num2, 0, 10000);
				_pLadderShiftPx = LadderOffsetPx;
				break;
			case "g":
				AgruparTk = 1;
				_pG = 1;
				break;
			case "l":
				UmbralLin = Clamp(_pL + num2, 1, 500);
				_pL = UmbralLin;
				break;
			case "bub":
				MinBurbuja = Clamp(_pBub + num2, 1, 200);
				_pBub = MinBurbuja;
				break;
			case "block":
				UmbralBloque = Clamp(_pBlock + num2, 1, 2000);
				_pBlock = UmbralBloque;
				break;
			case "btvis":
				BigTradeVisualMinQty = Clamp(_pBtVisualMin + num2, 1, 2000);
				_pBtVisualMin = BigTradeVisualMinQty;
				BigTradeAutoPorInstrumento = false;
				_pBtAuto = false;
				break;
			case "flowmin":
				FlowMinChange = Clamp(_pFlowMin + num2, 1, 500);
				_pFlowMin = FlowMinChange;
				break;
			case "flowfade":
				FlowFadeMs = Clamp(_pFlowFadeMs + num2, 300, 9000);
				_pFlowFadeMs = FlowFadeMs;
				break;
			case "flowop":
				FlowOpacity = Clamp(_pFlowOpacity + num2, 0, 100);
				_pFlowOpacity = FlowOpacity;
				break;
			case "flowsize":
				FlowSize = Clamp(_pFlowSize + num2, 1, 5);
				_pFlowSize = FlowSize;
				break;
			case "flowlbl":
				FlowLabelMin = Clamp(_pFlowLabelMin + num2, 1, 500);
				_pFlowLabelMin = FlowLabelMin;
				break;
			case "zoneattack":
				IltDevZoneAtaqueMinQty = Clamp(_pIltDevZoneAttackMinQty + num2, 1, 2000);
				_pIltDevZoneAttackMinQty = IltDevZoneAtaqueMinQty;
				break;
			case "zonepct":
				IltDevZoneEscalaPct = Clamp(_pIltDevZoneScalePct + num2, 100, 600);
				_pIltDevZoneScalePct = IltDevZoneEscalaPct;
				break;
			case "zonecloses":
				ZoneCierresConfirmacion = Clamp(_pZoneCloses + num2, 1, 3);
				_pZoneCloses = ZoneCierresConfirmacion;
				break;
			case "zoneband":
				IltDevZoneBandaTicks = Clamp(_pIltDevZoneBandTicks + num2, 1, 20);
				_pIltDevZoneBandTicks = IltDevZoneBandaTicks;
				break;
			case "zonebars":
				IltDevZoneCandidatoMaxBars = Clamp(_pIltDevZoneCandidateMaxBars + num2, 20, 2000);
				_pIltDevZoneCandidateMaxBars = IltDevZoneCandidatoMaxBars;
				break;
			case "zonehist":
				IltDevZoneHistorialBars = Clamp(_pIltDevZoneHistoryBars + num2, 100, 10000);
				_pIltDevZoneHistoryBars = IltDevZoneHistorialBars;
				break;
			case "zoneadapt":
				IltDevZoneAtaqueAdaptativoPct = Clamp(_pIltDevZoneAdaptivePct + num2, 0, 95);
				_pIltDevZoneAdaptivePct = IltDevZoneAtaqueAdaptativoPct;
				_iltDevZoneAdaptiveLastCalc = -1.0;
				break;
			case "zonestrength":
				IltDevZoneFuerzaMin = Clamp(_pIltDevZoneMinStrength + num2, 0, 100);
				_pIltDevZoneMinStrength = IltDevZoneFuerzaMin;
				break;
			case "zonemergeticks":
				IltDevZoneFusionTicks = Clamp(_pIltDevZoneMergeTicks + num2, 1, 20);
				_pIltDevZoneMergeTicks = IltDevZoneFusionTicks;
				break;
			case "zonemergesec":
				IltDevZoneFusionSeg = Clamp(_pIltDevZoneMergeSec + num2, 10, 600);
				_pIltDevZoneMergeSec = IltDevZoneFusionSeg;
				break;
			case "srreadd":
				IltDevSRReAddPct = Clamp(_pIltDevSRReAddPct + num2, 10, 100);
				_pIltDevSRReAddPct = IltDevSRReAddPct;
				break;
			case "srwindow":
				IltDevSRVentanaMs = Clamp(_pIltDevSRWindowMs + num2, 1000, 30000);
				_pIltDevSRWindowMs = IltDevSRVentanaMs;
				break;
			case "srband":
				SRBandaTicks = Clamp(_pSRBandTicks + num2, 1, 40);
				_pSRBandTicks = SRBandaTicks;
				break;
			case "srstrength":
				IltDevSRFuerzaMin = Clamp(_pIltDevSRMinStrength + num2, 0, 100);
				_pIltDevSRMinStrength = IltDevSRFuerzaMin;
				break;
			case "srpresence":
				IltDevSRMuroPresenciaMin = Clamp(_pIltDevSRWallMinPresence + num2, 2, 30);
				_pIltDevSRWallMinPresence = IltDevSRMuroPresenciaMin;
				break;
			case "srage":
				IltDevSRMuroEdadMinMs = Clamp(_pIltDevSRWallMinAgeMs + num2, 100, 10000);
				_pIltDevSRWallMinAgeMs = IltDevSRMuroEdadMinMs;
				break;
			case "srfresh":
				IltDevSRMuroFreshMs = Clamp(_pIltDevSRWallFreshMs + num2, 500, 15000);
				_pIltDevSRWallFreshMs = IltDevSRMuroFreshMs;
				break;
			case "srretain":
				IltDevSRRetencionBars = Clamp(_pIltDevSRRetentionBars + num2, 100, 10000);
				_pIltDevSRRetentionBars = IltDevSRRetencionBars;
				break;
			case "srpersistbig":
				SRPersistBig = Clamp(_pSRPersistBig + num2, 1, 2000);
				_pSRPersistBig = SRPersistBig;
				break;
			case "srpersistpass":
				SRPersistPass = Clamp(_pSRPersistPass + num2, 1, 2000);
				_pSRPersistPass = SRPersistPass;
				break;
			case "srhistbig":
				SRHistBig = Clamp(_pSRHistBig + num2, 1, 5000);
				_pSRHistBig = SRHistBig;
				break;
			case "srhistpass":
				SRHistPass = Clamp(_pSRHistPass + num2, 1, 2000);
				_pSRHistPass = SRHistPass;
				break;
			case "passmin":
				LiquidezPasivaMinQty = Clamp(_pPassiveMinQty + num2, 1, 1000);
				_pPassiveMinQty = LiquidezPasivaMinQty;
				break;
			case "passdepth":
				LiquidezPasivaProfundidad = Clamp(_pPassiveDepth + num2, 1, 20);
				_pPassiveDepth = LiquidezPasivaProfundidad;
				break;
			case "passevt":
				LiquidezPasivaEventoMin = Clamp(_pPassiveEventMin + num2, 1, 1000);
				_pPassiveEventMin = LiquidezPasivaEventoMin;
				break;
			case "wallminqty":
				MuroMinQty = Clamp(_pWallMinQty + num2, 1, 2000);
				_pWallMinQty = MuroMinQty;
				break;
			case "passwallbars":
				LiquidezPasivaMuroMaxBars = Clamp(_pPassiveWallMaxBars + num2, 20, 2000);
				_pPassiveWallMaxBars = LiquidezPasivaMuroMaxBars;
				break;
			case "passmarksz":
				LiquidezPasivaMarkerSize = Clamp(_pPassiveMarkerSize + num2, 1, 5);
				_pPassiveMarkerSize = LiquidezPasivaMarkerSize;
				break;
			case "passlblmin":
				LiquidezPasivaLabelMin = Clamp(_pPassiveMarkerLabelMin + num2, 1, 1000);
				_pPassiveMarkerLabelMin = _pF;
				break;
			}
			ApplyRuntimeChange(false);
		}

		private void MU(object s, MouseButtonEventArgs e)
		{
			if (UseWpfPanel)
			{
				return;
			}
			if (_drag)
			{
				e.Handled = true;
				if (_dId == "panel")
				{
					System.Windows.Point position = e.GetPosition(base.ChartControl);
					float num = (float)position.X * _ldX;
					float num2 = (float)position.Y * _ldY;
					float num3 = Math.Max(4f, 4f * Math.Max(_ldX, _ldY));
					if (Math.Abs(num - _dX0) <= num3 && Math.Abs(num2 - _dY0) <= num3)
					{
						_col = !_col;
						RequestRefresh(true);
					}
				}
			}
			_drag = false;
			_dId = null;
			try
			{
				if (base.ChartControl != null)
				{
					((UIElement)base.ChartControl).ReleaseMouseCapture();
				}
			}
			catch
			{
			}
		}

		private bool HitPanelControl(float x, float y)
		{
			if (Hit(_rTitle, x, y) || Hit(_rTLad, x, y) || Hit(_rTLin, x, y) || Hit(_rTSt, x, y) || Hit(_rTPull, x, y) || Hit(_rTBub, x, y) || Hit(_rTAbs, x, y) || Hit(_rTDir, x, y) || Hit(_rTPoc, x, y) || Hit(_rTFlow, x, y) || Hit(_rTPassive, x, y))
			{
				return true;
			}
			foreach (KeyValuePair<string, RectangleF> stepMinu in _stepMinus)
			{
				if (Hit(stepMinu.Value, x, y))
				{
					return true;
				}
			}
			foreach (KeyValuePair<string, RectangleF> stepPlu in _stepPlus)
			{
				if (Hit(stepPlu.Value, x, y))
				{
					return true;
				}
			}
			return false;
		}

		private bool Hit(RectangleF rc, float x, float y)
		{
			if (x >= rc.Left && x <= rc.Right && y >= rc.Top)
			{
				return y <= rc.Bottom;
			}
			return false;
		}

		private void ClampPanelPosition(float margin)
		{
			float num = Math.Max(margin, _lastPanelCanvasW - _lastPanelW - margin);
			float num2 = Math.Max(margin, _lastPanelCanvasH - _lastPanelH - margin);
			if (_panelX < margin)
			{
				_panelX = margin;
			}
			if (_panelY < margin)
			{
				_panelY = margin;
			}
			if (_panelX > num)
			{
				_panelX = num;
			}
			if (_panelY > num2)
			{
				_panelY = num2;
			}
		}

		private int Clamp(int v, int lo, int hi)
		{
			if (v >= lo)
			{
				if (v <= hi)
				{
					return v;
				}
				return hi;
			}
			return lo;
		}

		private void T(string txt, float x, float y, TextFormat fmt, SharpDX.Direct2D1.SolidColorBrush br, float mw, float mh)
		{
			if (fmt == null || string.IsNullOrEmpty(txt))
			{
				return;
			}
			TextLayout textLayout = new TextLayout(Globals.DirectWriteFactory, txt, fmt, Math.Max(1f, mw), Math.Max(1f, mh));
			try
			{
				base.RenderTarget.DrawTextLayout(new Vector2(x, y), textLayout, br);
			}
			finally
			{
				if (textLayout != null)
				{
					((IDisposable)textLayout).Dispose();
				}
			}
		}

		private void TC(string txt, float x, float y, TextFormat fmt, SharpDX.Direct2D1.SolidColorBrush br, float mw, float mh)
		{
			if (fmt == null || string.IsNullOrEmpty(txt))
			{
				return;
			}
			TextLayout textLayout = new TextLayout(Globals.DirectWriteFactory, txt, fmt, Math.Max(1f, mw), Math.Max(1f, mh));
			try
			{
				textLayout.TextAlignment = SharpDX.DirectWrite.TextAlignment.Center;
				textLayout.ParagraphAlignment = ParagraphAlignment.Center;
				textLayout.WordWrapping = WordWrapping.NoWrap;
				base.RenderTarget.DrawTextLayout(new Vector2(x, y), textLayout, br);
			}
			finally
			{
				if (textLayout != null)
				{
					((IDisposable)textLayout).Dispose();
				}
			}
		}

		private static string FQ(long q)
		{
			if (q >= 1000000)
			{
				return ((double)q / 1000000.0).ToString("0.0") + "M";
			}
			if (q >= 1000)
			{
				return ((double)q / 1000.0).ToString("0.0") + "K";
			}
			return q.ToString();
		}

		private static TextFormat MakeFont(SharpDX.DirectWrite.Factory dw, float size, SharpDX.DirectWrite.FontWeight weight = SharpDX.DirectWrite.FontWeight.Normal)
		{
			string[] fontFallback = _fontFallback;
			string[] array = fontFallback;
			foreach (string fontFamilyName in array)
			{
				try
				{
					return new TextFormat(dw, fontFamilyName, weight, SharpDX.DirectWrite.FontStyle.Normal, SharpDX.DirectWrite.FontStretch.Normal, size);
				}
				catch
				{
				}
			}
			return new TextFormat(dw, "Arial", size);
		}

		private void EnsureDX(float dpiScale = 1f)
		{
			float num = Math.Max(7f, Math.Min(30f, FontSizeSmall));
			float num2 = Math.Max(7f, Math.Min(34f, FontSizeBold));
			float num3 = Math.Max(6f, Math.Min(24f, DomLadderFontSize));
			string text = VisualSettingsSignature();
			bool flag = Math.Abs(dpiScale - _curDpi) > 0.04f;
			bool flag2 = Math.Abs(num - _lastFontSmall) > 0.01f || Math.Abs(num2 - _lastFontBold) > 0.01f || Math.Abs(num3 - _lastFontLadder) > 0.01f;
			bool flag3 = _lastVisualSignature != text;
			IntPtr zero = IntPtr.Zero;
			IntPtr intPtr;
			try
			{
				intPtr = ((base.RenderTarget != null) ? base.RenderTarget.NativePointer : IntPtr.Zero);
			}
			catch
			{
				intPtr = IntPtr.Zero;
			}
			bool flag4 = intPtr != IntPtr.Zero && _dxRtRef != intPtr;
			if (_dxOk && flag4)
			{
				FreeDX();
				_hmBmpFail = false;
			}
			if (_dxOk && !flag && !flag2 && !flag3)
			{
				return;
			}
			if (_dxOk && flag3)
			{
				FreeDX();
			}
			if (_dxOk && (flag || flag2))
			{
				if (_fSm != null)
				{
					_fSm.Dispose();
					_fSm = null;
				}
				if (_fBold != null)
				{
					_fBold.Dispose();
					_fBold = null;
				}
				if (_fTiny != null)
				{
					_fTiny.Dispose();
					_fTiny = null;
				}
				if (_fLad != null)
				{
					_fLad.Dispose();
					_fLad = null;
				}
				if (_fPassiveBig != null)
				{
					_fPassiveBig.Dispose();
					_fPassiveBig = null;
				}
				if (_fPassiveMid != null)
				{
					_fPassiveMid.Dispose();
					_fPassiveMid = null;
				}
				_curDpi = dpiScale;
				_lastFontSmall = num;
				_lastFontBold = num2;
				_lastFontLadder = num3;
				_lastVisualSignature = text;
				SharpDX.DirectWrite.Factory directWriteFactory = Globals.DirectWriteFactory;
				float size = Math.Max(7f, num * dpiScale);
				float size2 = Math.Max(6f, num * 0.72f * dpiScale);
				float size3 = Math.Max(8f, num2 * dpiScale);
				float size4 = Math.Max(6f, num3 * dpiScale);
				float size5 = Math.Max(14f, num2 * 1.62f * dpiScale);
				float size6 = Math.Max(10f, num2 * 1.12f * dpiScale);
				_fSm = MakeFont(directWriteFactory, size);
				_fTiny = MakeFont(directWriteFactory, size2);
				_fLad = MakeFont(directWriteFactory, size4);
				_fBold = MakeFont(directWriteFactory, size3, SharpDX.DirectWrite.FontWeight.Bold);
				_fPassiveBig = MakeFont(directWriteFactory, size5, SharpDX.DirectWrite.FontWeight.Bold);
				_fPassiveMid = MakeFont(directWriteFactory, size6, SharpDX.DirectWrite.FontWeight.Bold);
				return;
			}
			_curDpi = dpiScale;
			_lastFontSmall = num;
			_lastFontBold = num2;
			_lastFontLadder = num3;
			_lastVisualSignature = text;
			try
			{
				RenderTarget renderTarget = base.RenderTarget;
				SharpDX.DirectWrite.Factory directWriteFactory2 = Globals.DirectWriteFactory;
				_bBid = SB(renderTarget, HeatmapBidColor, 0f, 0.78f, 1f, 1f);
				_bAsk = SB(renderTarget, HeatmapAskColor, 1f, 0f, 0.85f, 1f);
				_bLvBid = SB(renderTarget, LiquidityBidColor, 0.12f, 0.8f, 0.92f, 1f);
				_bLvAsk = SB(renderTarget, LiquidityAskColor, 1f, 0f, 0.85f, 1f);
				_bStBid = SB(renderTarget, LiquidityBidColor, 0.1f, 0.95f, 0.92f, 1f);
				_bStAsk = SB(renderTarget, LiquidityAskColor, 1f, 0f, 0.85f, 1f);
				_bAbs = SB(renderTarget, AbsorptionMarkColor, 1f, 0.9f, 0.1f, 1f);
				_bPull = SB(renderTarget, FlowPullColor, 1f, 0.52f, 0.05f, 1f);
				_bBubBuy = SB(renderTarget, BigTradeBuyColor, 0.15f, 0.92f, 0.3f, 1f);
				_bBubSell = SB(renderTarget, BigTradeSellColor, 0.95f, 0.1f, 0.1f, 1f);
				_bBubBord = SB(renderTarget, BubbleBorderColor, 0.62f, 0.12f, 0.1f, 1f);
				_bBubFlash = SB(renderTarget, BubbleLightningColor, 0.95f, 0.2f, 0.05f, 1f);
				_bMid = SB(renderTarget, 1f, 0.9f, 0.1f, 1f);
				_bLadBg = SB(renderTarget, 0.01f, 0.01f, 0.03f, 1f);
				_bLadBid = SB(renderTarget, LiquidityBidColor, 0.1f, 0.78f, 0.92f, 1f);
				_bLadAsk = SB(renderTarget, LiquidityAskColor, 1f, 0f, 0.85f, 1f);
				_bLadSt = SB(renderTarget, LiquidityBidColor, 0.1f, 0.95f, 0.92f, 1f);
				_bLadSep = SB(renderTarget, 0.42f, 0.45f, 0.55f, 1f);
				_bLadTxt = SB(renderTarget, 0.85f, 0.87f, 0.92f, 1f);
				_bLadHl = SB(renderTarget, 1f, 1f, 1f, 1f);
				_bRowCur = SB(renderTarget, 1f, 0.9f, 0.1f, 1f);
				_bPoc = SB(renderTarget, 1f, 0.78f, 0f, 1f);
				_bPanBg = SB(renderTarget, 0.04f, 0.04f, 0.08f, 1f);
				_bPanSh = SB(renderTarget, 0f, 0f, 0f, 1f);
				_bPanBord = SB(renderTarget, 0.36f, 0.38f, 0.56f, 1f);
				_bTxt = SB(renderTarget, TextColor, 0.94f, 0.95f, 1f, 1f);
				_bDim = SB(renderTarget, DimTextColor, 0.68f, 0.7f, 0.78f, 1f);
				_bSlBg = SB(renderTarget, 0.15f, 0.15f, 0.22f, 1f);
				_bSlFg = SB(renderTarget, 0.25f, 0.42f, 0.88f, 1f);
				_bOn = SB(renderTarget, 0.1f, 0.5f, 0.88f, 1f);
				_bSect = SB(renderTarget, 0.26f, 0.3f, 0.5f, 1f);
				_bFlowAdd = SB(renderTarget, FlowAddColor, 0f, 0.78f, 1f, 1f);
				_bFlowPull = SB(renderTarget, FlowPullColor, 1f, 0.52f, 0.05f, 1f);
				_bFlowText = SB(renderTarget, FlowTextColor, 0.95f, 0.95f, 1f, 1f);
				_bPassiveBid = SB(renderTarget, LiquidityBidColor, 0f, 0.95f, 0.92f, 1f);
				_bPassiveAsk = SB(renderTarget, LiquidityAskColor, 1f, 0.05f, 0.7f, 1f);
				_bPassiveRefill = SB(renderTarget, RefillMarkColor, 0.9f, 0.95f, 1f, 1f);
				_bPassiveWarn = SB(renderTarget, ConsumeMarkColor, 1f, 0.3f, 0.08f, 1f);
				float size7 = Math.Max(7f, num * dpiScale);
				float size8 = Math.Max(6f, num * 0.72f * dpiScale);
				float size9 = Math.Max(8f, num2 * dpiScale);
				float size10 = Math.Max(6f, num3 * dpiScale);
				float size11 = Math.Max(14f, num2 * 1.62f * dpiScale);
				float size12 = Math.Max(10f, num2 * 1.12f * dpiScale);
				_fSm = MakeFont(directWriteFactory2, size7);
				_fTiny = MakeFont(directWriteFactory2, size8);
				_fLad = MakeFont(directWriteFactory2, size10);
				_fBold = MakeFont(directWriteFactory2, size9, SharpDX.DirectWrite.FontWeight.Bold);
				_fPassiveBig = MakeFont(directWriteFactory2, size11, SharpDX.DirectWrite.FontWeight.Bold);
				_fPassiveMid = MakeFont(directWriteFactory2, size12, SharpDX.DirectWrite.FontWeight.Bold);
				StrokeStyleProperties properties = new StrokeStyleProperties
				{
					DashStyle = SharpDX.Direct2D1.DashStyle.Custom
				};
				_stDash = new StrokeStyle(renderTarget.Factory, properties, new float[2] { 5f, 3f });
				StrokeStyleProperties properties2 = new StrokeStyleProperties
				{
					DashStyle = SharpDX.Direct2D1.DashStyle.Custom
				};
				_stDot = new StrokeStyle(renderTarget.Factory, properties2, new float[2] { 2f, 3f });
				BuildHeatPalette(renderTarget);
				_bSRZone = SB(renderTarget, 0.98f, 0.72f, 0.18f, 1f);
				_bSRHist = SB(renderTarget, 0.82f, 0.85f, 0.9f, 1f);
				_dxOk = true;
				_dxRtRef = intPtr;
			}
			catch
			{
				_dxOk = false;
				_dxRtRef = IntPtr.Zero;
			}
		}

		private void BuildHeatPalette(RenderTarget rt)
		{
			DisposeHeatPalette();
			_heatPal = new SharpDX.Direct2D1.SolidColorBrush[48];
			int num = Math.Max(1, Math.Min(6, _pHeatColorMode)) - 1;
			for (int i = 0; i < 48; i++)
			{
				float num2 = (float)i / 47f;
				int j;
				for (j = 0; j < HEAT_STOP_POS.Length - 2 && num2 > HEAT_STOP_POS[j + 1]; j++)
				{
				}
				float num3 = Math.Max(1E-06f, HEAT_STOP_POS[j + 1] - HEAT_STOP_POS[j]);
				float num4 = Math.Max(0f, Math.Min(1f, (num2 - HEAT_STOP_POS[j]) / num3));
				float red = HEAT_PALETTES[num, j, 0] + (HEAT_PALETTES[num, j + 1, 0] - HEAT_PALETTES[num, j, 0]) * num4;
				float green = HEAT_PALETTES[num, j, 1] + (HEAT_PALETTES[num, j + 1, 1] - HEAT_PALETTES[num, j, 1]) * num4;
				float blue = HEAT_PALETTES[num, j, 2] + (HEAT_PALETTES[num, j + 1, 2] - HEAT_PALETTES[num, j, 2]) * num4;
				_heatPal[i] = new SharpDX.Direct2D1.SolidColorBrush(rt, new Color4(red, green, blue, 1f));
			}
		}

		private void DisposeHeatPalette()
		{
			if (_heatPal == null)
			{
				return;
			}
			for (int i = 0; i < _heatPal.Length; i++)
			{
				if (_heatPal[i] != null)
				{
					try
					{
						_heatPal[i].Dispose();
					}
					catch
					{
					}
				}
			}
			_heatPal = null;
		}

		private Color4 HeatColor4(double t)
		{
			float num = (float)Math.Max(0.0, Math.Min(1.0, t));
			int num2 = Math.Max(1, Math.Min(6, _pHeatColorMode)) - 1;
			int i;
			for (i = 0; i < HEAT_STOP_POS.Length - 2 && num > HEAT_STOP_POS[i + 1]; i++)
			{
			}
			float num3 = Math.Max(1E-06f, HEAT_STOP_POS[i + 1] - HEAT_STOP_POS[i]);
			float num4 = Math.Max(0f, Math.Min(1f, (num - HEAT_STOP_POS[i]) / num3));
			float red = HEAT_PALETTES[num2, i, 0] + (HEAT_PALETTES[num2, i + 1, 0] - HEAT_PALETTES[num2, i, 0]) * num4;
			float green = HEAT_PALETTES[num2, i, 1] + (HEAT_PALETTES[num2, i + 1, 1] - HEAT_PALETTES[num2, i, 1]) * num4;
			float blue = HEAT_PALETTES[num2, i, 2] + (HEAT_PALETTES[num2, i + 1, 2] - HEAT_PALETTES[num2, i, 2]) * num4;
			return new Color4(red, green, blue, 1f);
		}

		private static string HeatPaletteShortName(int palette)
		{
			switch (palette)
			{
			case 0:
				return "CUSTOM";
			case 2:
				return "ICE/FIRE";
			case 3:
				return "INFERNO";
			case 4:
				return "VIRIDIS";
			case 5:
				return "PLASMA";
			case 6:
				return "EMERALD";
			default:
				return "BOOKMAP";
			}
		}

		private void DisposeHeatBitmap()
		{
			if (_hmCellBrush != null)
			{
				try
				{
					_hmCellBrush.Dispose();
				}
				catch
				{
				}
			}
			_hmCellBrush = null;
			if (_hmBrt != null)
			{
				try
				{
					_hmBrt.Dispose();
				}
				catch
				{
				}
			}
			_hmBrt = null;
			_hmRtRef = IntPtr.Zero;
			_hmLastPaintedSeq = 0L;
		}

		private bool EnsureHeatBitmap()
		{
			if (base.RenderTarget == null)
			{
				return false;
			}
			IntPtr zero = IntPtr.Zero;
			IntPtr nativePointer;
			try
			{
				nativePointer = base.RenderTarget.NativePointer;
			}
			catch
			{
				return false;
			}
			if (_hmBrt != null && _hmRtRef == nativePointer && _hmCellBrush != null)
			{
				return true;
			}
			DisposeHeatBitmap();
			try
			{
				_hmBrt = new SharpDX.Direct2D1.BitmapRenderTarget(base.RenderTarget, CompatibleRenderTargetOptions.None, new Size2F(1024f, 1024f), new Size2(1024, 1024), null);
				_hmCellBrush = new SharpDX.Direct2D1.SolidColorBrush(_hmBrt, new Color4(0f, 0f, 0f, 0f));
				_hmBrt.BeginDraw();
				_hmBrt.Clear(new Color4(0f, 0f, 0f, 0f));
				_hmBrt.EndDraw();
				_hmRtRef = nativePointer;
				_hmLastPaintedSeq = 0L;
				_hmAnchor = 0.0;
				return true;
			}
			catch
			{
				DisposeHeatBitmap();
				_hmBmpFail = true;
				return false;
			}
		}

		private bool DrawLiveHeatmapCellsBitmap(ChartScale cs, float left, float colW, int start, int count, List<HM6HeatmapSlice> slices, long thr, long visibleMax, float H)
		{
			if (!EnsureHeatBitmap())
			{
				return false;
			}
			if (slices == null || count <= 0)
			{
				return false;
			}
			double num = Math.Max(_tick, (base.TickSize > 0.0) ? base.TickSize : 0.25);
			if (num <= 0.0 || double.IsNaN(num))
			{
				return false;
			}
			int num2 = 512;
			int num3 = Math.Min(slices.Count, start + count);
			if (num3 <= start)
			{
				return false;
			}
			long seq = slices[start].Seq;
			long seq2 = slices[num3 - 1].Seq;
			double num4 = ((_midPx > 0.0) ? _midPx : ((base.CurrentBar >= 0 && base.Close != null && base.Close.Count > 0) ? base.Close[0] : 0.0));
			if (num4 <= 0.0)
			{
				return false;
			}
			if (_hmLastPaintedSeq > seq2)
			{
				_hmAnchor = 0.0;
			}
			if (_hmAnchor <= 0.0 || Math.Abs(num4 - _hmAnchor) / num > 292.0)
			{
				_hmAnchor = num4;
				_hmLastPaintedSeq = Math.Max(0L, seq - 1);
				try
				{
					_hmBrt.BeginDraw();
					_hmBrt.Clear(new Color4(0f, 0f, 0f, 0f));
					_hmBrt.EndDraw();
				}
				catch
				{
					_hmBmpFail = true;
					return false;
				}
			}
			long num5 = Math.Max(_hmLastPaintedSeq + 1, seq2 - 1023);
			if (num5 <= seq2)
			{
				float num6 = OpacityPct(HeatmapOpacity);
				float minOp = Math.Min(num6, OpacityPct(_pHeatmapMinOpacity));
				try
				{
					_hmBrt.BeginDraw();
					for (int i = start; i < num3; i++)
					{
						HM6HeatmapSlice hM6HeatmapSlice = slices[i];
						if (hM6HeatmapSlice == null || hM6HeatmapSlice.Seq < num5)
						{
							continue;
						}
						int num7 = (int)(hM6HeatmapSlice.Seq % 1024);
						RectangleF clipRect = new RectangleF(num7, 0f, 1f, 1024f);
						_hmBrt.PushAxisAlignedClip(clipRect, AntialiasMode.Aliased);
						_hmBrt.Clear(new Color4(0f, 0f, 0f, 0f));
						_hmBrt.PopAxisAlignedClip();
						if (!hM6HeatmapSlice.HasData)
						{
							continue;
						}
						foreach (KeyValuePair<double, long> item in hM6HeatmapSlice.Bid)
						{
							PaintHeatBitmapCell(num7, item.Key, item.Value, thr, visibleMax, num, num2, minOp, num6);
						}
						foreach (KeyValuePair<double, long> item2 in hM6HeatmapSlice.Ask)
						{
							PaintHeatBitmapCell(num7, item2.Key, item2.Value, thr, visibleMax, num, num2, minOp, num6);
						}
					}
					_hmBrt.EndDraw();
					_hmLastPaintedSeq = seq2;
				}
				catch
				{
					_hmBmpFail = true;
					return false;
				}
			}
			double val = _hmAnchor + ((double)num2 + 0.5) * num;
			double val2 = _hmAnchor + ((double)(num2 - 1024) + 0.5) * num;
			float num8 = cs.GetYByValue(val);
			float num9 = cs.GetYByValue(val2);
			if (num9 <= num8)
			{
				return false;
			}
			long num10 = seq2 - 1023;
			int num11 = start;
			while (num11 < num3)
			{
				HM6HeatmapSlice hM6HeatmapSlice2 = slices[num11];
				if (hM6HeatmapSlice2 == null || hM6HeatmapSlice2.Seq < num10)
				{
					num11++;
					continue;
				}
				int j;
				for (j = num11; j + 1 < num3; j++)
				{
					HM6HeatmapSlice hM6HeatmapSlice3 = slices[j];
					HM6HeatmapSlice hM6HeatmapSlice4 = slices[j + 1];
					if (hM6HeatmapSlice3 == null || hM6HeatmapSlice4 == null || hM6HeatmapSlice4.Seq != hM6HeatmapSlice3.Seq + 1 || (int)(hM6HeatmapSlice4.Seq % 1024) == 0)
					{
						break;
					}
				}
				long seq3 = slices[num11].Seq;
				long seq4 = slices[j].Seq;
				int num12 = (int)(seq3 % 1024);
				float x = left + (float)(num11 - start) * colW;
				RectangleF value = new RectangleF(x, num8, (float)(j - num11 + 1) * colW, num9 - num8);
				RectangleF value2 = new RectangleF(num12, 0f, seq4 - seq3 + 1, 1024f);
				base.RenderTarget.DrawBitmap(_hmBrt.Bitmap, value, 0.82f, BitmapInterpolationMode.NearestNeighbor, value2);
				num11 = j + 1;
			}
			return true;
		}

		private void PaintHeatBitmapCell(int col, double price, long qty, long thr, long visibleMax, double step, int center, float minOp, float maxOp)
		{
			if (qty >= thr)
			{
				int num = center - (int)Math.Round((price - _hmAnchor) / step);
				if (num >= 0 && num < 1024)
				{
					double num2 = HeatmapIntensityNorm(qty, visibleMax, thr);
					Color4 color = HeatColor4(num2);
					color.Alpha = (float)Math.Max(0.02, Math.Min(1.0, (double)minOp + (double)(maxOp - minOp) * (0.35 + 0.65 * Math.Sqrt(num2))));
					_hmCellBrush.Color = color;
					_hmBrt.FillRectangle(new RectangleF(col, num, 1f, 1f), _hmCellBrush);
				}
			}
		}

		private static SharpDX.Direct2D1.SolidColorBrush SB(RenderTarget rt, float r, float g, float b, float a)
		{
			return new SharpDX.Direct2D1.SolidColorBrush(rt, new Color4(r, g, b, a));
		}

		private static SharpDX.Direct2D1.SolidColorBrush SB(RenderTarget rt, System.Windows.Media.Brush brush, float fallbackR, float fallbackG, float fallbackB, float fallbackA)
		{
			return new SharpDX.Direct2D1.SolidColorBrush(rt, BrushToColor4(brush, fallbackR, fallbackG, fallbackB, fallbackA));
		}

		private string VisualSettingsSignature()
		{
			return string.Join("|", BrushKey(HeatmapBidColor), BrushKey(HeatmapAskColor), BrushKey(LiquidityBidColor), BrushKey(LiquidityAskColor), BrushKey(TextColor), BrushKey(DimTextColor), BrushKey(FlowAddColor), BrushKey(FlowPullColor), BrushKey(FlowTextColor), BrushKey(AbsorptionMarkColor), BrushKey(RefillMarkColor), BrushKey(ConsumeMarkColor), _pHeatColorMode.ToString(), HeatmapOpacity.ToString(), PanelOpacity.ToString(), LadderBgOpacity.ToString(), FontSizeSmall.ToString(), FontSizeBold.ToString(), DomLadderFontSize.ToString());
		}

		private static string BrushKey(System.Windows.Media.Brush brush)
		{
			System.Windows.Media.SolidColorBrush solidColorBrush = brush as System.Windows.Media.SolidColorBrush;
			if (solidColorBrush != null)
			{
				return solidColorBrush.Color.ToString();
			}
			return "null";
		}

		private static Color4 BrushToColor4(System.Windows.Media.Brush brush, float fallbackR, float fallbackG, float fallbackB, float fallbackA)
		{
			System.Windows.Media.SolidColorBrush solidColorBrush = brush as System.Windows.Media.SolidColorBrush;
			if (solidColorBrush == null)
			{
				return new Color4(fallbackR, fallbackG, fallbackB, fallbackA);
			}
			System.Windows.Media.Color color = solidColorBrush.Color;
			return new Color4((float)(int)color.R / 255f, (float)(int)color.G / 255f, (float)(int)color.B / 255f, (float)(int)color.A / 255f);
		}

		private static float OpacityPct(int value)
		{
			return (float)Math.Max(0, Math.Min(100, value)) / 100f;
		}

		private void FreeDX()
		{
			SharpDX.Direct2D1.SolidColorBrush[] array = new SharpDX.Direct2D1.SolidColorBrush[38]
			{
				_bBid, _bAsk, _bLvBid, _bLvAsk, _bStBid, _bStAsk, _bAbs, _bPull, _bBubBuy, _bBubSell,
				_bBubBord, _bBubFlash, _bMid, _bPoc, _bLadBg, _bLadBid, _bLadAsk, _bLadSt, _bLadSep, _bLadTxt,
				_bLadHl, _bRowCur, _bPanBg, _bPanSh, _bPanBord, _bTxt, _bDim, _bSlBg, _bSlFg, _bOn,
				_bSect, _bFlowAdd, _bFlowPull, _bFlowText, _bPassiveBid, _bPassiveAsk, _bPassiveRefill, _bPassiveWarn
			};
			SharpDX.Direct2D1.SolidColorBrush[] array2 = array;
			SharpDX.Direct2D1.SolidColorBrush[] array3 = array2;
			foreach (SharpDX.Direct2D1.SolidColorBrush solidColorBrush in array3)
			{
				if (solidColorBrush != null)
				{
					try
					{
						solidColorBrush.Dispose();
					}
					catch
					{
					}
				}
			}
			DisposeHeatPalette();
			DisposeHeatBitmap();
			if (_bSRZone != null)
			{
				try
				{
					_bSRZone.Dispose();
				}
				catch
				{
				}
			}
			_bSRZone = null;
			if (_bSRHist != null)
			{
				try
				{
					_bSRHist.Dispose();
				}
				catch
				{
				}
			}
			_bSRHist = null;
			if (_fSm != null)
			{
				try
				{
					_fSm.Dispose();
				}
				catch
				{
				}
			}
			if (_fBold != null)
			{
				try
				{
					_fBold.Dispose();
				}
				catch
				{
				}
			}
			if (_fTiny != null)
			{
				try
				{
					_fTiny.Dispose();
				}
				catch
				{
				}
			}
			if (_fLad != null)
			{
				try
				{
					_fLad.Dispose();
				}
				catch
				{
				}
			}
			if (_fPassiveBig != null)
			{
				try
				{
					_fPassiveBig.Dispose();
				}
				catch
				{
				}
			}
			if (_fPassiveMid != null)
			{
				try
				{
					_fPassiveMid.Dispose();
				}
				catch
				{
				}
			}
			if (_stDash != null)
			{
				try
				{
					_stDash.Dispose();
				}
				catch
				{
				}
			}
			if (_stDot != null)
			{
				try
				{
					_stDot.Dispose();
				}
				catch
				{
				}
			}
			_bBid = null;
			_bAsk = null;
			_bLvBid = null;
			_bLvAsk = null;
			_bStBid = null;
			_bStAsk = null;
			_bAbs = null;
			_bPull = null;
			_bBubBuy = null;
			_bBubSell = null;
			_bBubBord = null;
			_bBubFlash = null;
			_bMid = null;
			_bPoc = null;
			_bLadBg = null;
			_bLadBid = null;
			_bLadAsk = null;
			_bLadSt = null;
			_bLadSep = null;
			_bLadTxt = null;
			_bLadHl = null;
			_bRowCur = null;
			_bPanBg = null;
			_bPanSh = null;
			_bPanBord = null;
			_bTxt = null;
			_bDim = null;
			_bSlBg = null;
			_bSlFg = null;
			_bOn = null;
			_bSect = null;
			_bFlowAdd = null;
			_bFlowPull = null;
			_bFlowText = null;
			_bPassiveBid = null;
			_bPassiveAsk = null;
			_bPassiveRefill = null;
			_bPassiveWarn = null;
			_fSm = null;
			_fBold = null;
			_fTiny = null;
			_fLad = null;
			_fPassiveBig = null;
			_fPassiveMid = null;
			_stDash = null;
			_stDot = null;
			_dxOk = false;
			_dxRtRef = IntPtr.Zero;
		}

		private static System.Windows.Media.SolidColorBrush WpfArgb(byte a, byte r, byte g, byte b)
		{
			System.Windows.Media.SolidColorBrush solidColorBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(a, r, g, b));
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
			button.FontFamily = new System.Windows.Media.FontFamily("Consolas");
			button.FontSize = 11.0;
			button.FontWeight = FontWeights.Bold;
			button.Margin = new Thickness(2.0);
			button.Padding = new Thickness(4.0, 3.0, 4.0, 3.0);
			button.Foreground = Brushes.White;
			button.BorderBrush = WpfArgb(byte.MaxValue, 120, 120, 120);
			button.BorderThickness = new Thickness(1.0);
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
			textBlock.FontFamily = new System.Windows.Media.FontFamily("Consolas");
			textBlock.FontSize = 10.0;
			textBlock.Foreground = WpfArgb(byte.MaxValue, 190, 190, 190);
			textBlock.VerticalAlignment = VerticalAlignment.Center;
			textBlock.Margin = new Thickness(2.0, 0.0, 0.0, 0.0);
			Grid.SetColumn(textBlock, 0);
			grid.Children.Add(textBlock);
			Button button = new Button();
			button.Content = "−";
			button.FontFamily = new System.Windows.Media.FontFamily("Consolas");
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
			valueLabel.FontFamily = new System.Windows.Media.FontFamily("Consolas");
			valueLabel.FontSize = 11.0;
			valueLabel.FontWeight = FontWeights.Bold;
			valueLabel.Foreground = Brushes.White;
			valueLabel.Background = WpfArgb(180, 40, 40, 40);
			valueLabel.TextAlignment = System.Windows.TextAlignment.Center;
			valueLabel.VerticalAlignment = VerticalAlignment.Center;
			valueLabel.Padding = new Thickness(2.0, 2.0, 2.0, 2.0);
			Grid.SetColumn(valueLabel, 2);
			grid.Children.Add(valueLabel);
			Button button2 = new Button();
			button2.Content = "+";
			button2.FontFamily = new System.Windows.Media.FontFamily("Consolas");
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
			textBlock.FontFamily = new System.Windows.Media.FontFamily("Consolas");
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
				ApplyStep(button.Tag as string, -1);
				UpdateWpfStepperValues();
				e.Handled = true;
			}
		}

		private void WpfStepPlus_Click(object sender, RoutedEventArgs e)
		{
			Button button = sender as Button;
			if (button != null)
			{
				ApplyStep(button.Tag as string, 1);
				UpdateWpfStepperValues();
				e.Handled = true;
			}
		}

		private void UpdateWpfStepperValues()
		{
			if (_lblStepF != null)
			{
				_lblStepF.Text = _pF.ToString();
			}
			if (_lblStepRenderWin != null)
			{
				_lblStepRenderWin.Text = _pRenderWinPct + "%";
			}
			if (_lblStepHeatSpeed != null)
			{
				_lblStepHeatSpeed.Text = _pHeatmapScrollSpeed.ToString();
			}
			if (_lblStepHeatPalette != null)
			{
				_lblStepHeatPalette.Text = HeatPaletteShortName(_pHeatColorMode);
			}
			if (_lblStepLadF != null)
			{
				_lblStepLadF.Text = _pLadF.ToString();
			}
			if (_lblStepLadX != null)
			{
				_lblStepLadX.Text = _pLadderShiftPx.ToString();
			}
			if (_lblStepG != null)
			{
				_lblStepG.Text = _pG.ToString();
			}
			if (_lblStepBub != null)
			{
				_lblStepBub.Text = _pBub.ToString();
			}
			if (_lblStepBlock != null)
			{
				_lblStepBlock.Text = _pBtVisualMin.ToString();
			}
			if (_lblStepFlowMin != null)
			{
				_lblStepFlowMin.Text = _pFlowMin.ToString();
			}
			if (_lblStepFlowFade != null)
			{
				_lblStepFlowFade.Text = _pFlowFadeMs.ToString();
			}
			if (_lblStepFlowOp != null)
			{
				_lblStepFlowOp.Text = _pFlowOpacity.ToString();
			}
			if (_lblStepFlowSize != null)
			{
				_lblStepFlowSize.Text = _pFlowSize.ToString();
			}
			if (_lblStepFlowLbl != null)
			{
				_lblStepFlowLbl.Text = _pFlowLabelMin.ToString();
			}
			if (_lblStepPassiveMin != null)
			{
				_lblStepPassiveMin.Text = _pPassiveMinQty.ToString();
			}
			if (_lblStepWallMinQty != null)
			{
				_lblStepWallMinQty.Text = _pWallMinQty.ToString();
			}
			if (_lblStepSRPersistBig != null)
			{
				_lblStepSRPersistBig.Text = _pSRPersistBig.ToString();
			}
			if (_lblStepSRPersistPass != null)
			{
				_lblStepSRPersistPass.Text = _pSRPersistPass.ToString();
			}
			if (_lblStepSRHistBig != null)
			{
				_lblStepSRHistBig.Text = _pSRHistBig.ToString();
			}
			if (_lblStepSRHistPass != null)
			{
				_lblStepSRHistPass.Text = _pSRHistPass.ToString();
			}
			if (_lblStepPassiveDepth != null)
			{
				_lblStepPassiveDepth.Text = _pPassiveDepth.ToString();
			}
			if (_lblStepPassiveEvent != null)
			{
				_lblStepPassiveEvent.Text = _pPassiveEventMin.ToString();
			}
			if (_lblStepPassiveWallBars != null)
			{
				_lblStepPassiveWallBars.Text = _pPassiveWallMaxBars.ToString();
			}
			if (_lblStepPassiveMarkerSize != null)
			{
				_lblStepPassiveMarkerSize.Text = _pPassiveMarkerSize.ToString();
			}
			if (_lblStepPassiveLabelMin != null)
			{
				_lblStepPassiveLabelMin.Text = _pF.ToString();
			}
			if (_lblStepIltDevZoneAttack != null)
			{
				_lblStepIltDevZoneAttack.Text = _pIltDevZoneAttackMinQty.ToString();
			}
			if (_lblStepIltDevZoneScale != null)
			{
				_lblStepIltDevZoneScale.Text = _pIltDevZoneScalePct.ToString();
			}
			if (_lblStepIltDevZoneCloses != null)
			{
				_lblStepIltDevZoneCloses.Text = _pZoneCloses.ToString();
			}
			if (_lblStepIltDevZoneBand != null)
			{
				_lblStepIltDevZoneBand.Text = _pIltDevZoneBandTicks.ToString();
			}
			if (_lblStepIltDevZoneMaxBars != null)
			{
				_lblStepIltDevZoneMaxBars.Text = _pIltDevZoneCandidateMaxBars.ToString();
			}
			if (_lblStepIltDevZoneHistory != null)
			{
				_lblStepIltDevZoneHistory.Text = _pIltDevZoneHistoryBars.ToString();
			}
			if (_lblStepIltDevZoneAdaptive != null)
			{
				_lblStepIltDevZoneAdaptive.Text = _pIltDevZoneAdaptivePct.ToString();
			}
			if (_lblStepIltDevZoneStrength != null)
			{
				_lblStepIltDevZoneStrength.Text = _pIltDevZoneMinStrength.ToString();
			}
			if (_lblStepIltDevZoneMergeTicks != null)
			{
				_lblStepIltDevZoneMergeTicks.Text = _pIltDevZoneMergeTicks.ToString();
			}
			if (_lblStepIltDevZoneMergeSec != null)
			{
				_lblStepIltDevZoneMergeSec.Text = _pIltDevZoneMergeSec.ToString();
			}
			if (_lblStepIltDevSRScale != null)
			{
				_lblStepIltDevSRScale.Text = _pIltDevSRScalePct.ToString();
			}
			if (_lblStepIltDevSRReAdd != null)
			{
				_lblStepIltDevSRReAdd.Text = _pIltDevSRReAddPct.ToString();
			}
			if (_lblStepIltDevSRWindow != null)
			{
				_lblStepIltDevSRWindow.Text = _pIltDevSRWindowMs.ToString();
			}
			if (_lblStepIltDevSRBand != null)
			{
				_lblStepIltDevSRBand.Text = _pSRBandTicks.ToString();
			}
			if (_lblStepIltDevSRStrength != null)
			{
				_lblStepIltDevSRStrength.Text = _pIltDevSRMinStrength.ToString();
			}
			if (_lblStepIltDevSRPresence != null)
			{
				_lblStepIltDevSRPresence.Text = _pIltDevSRWallMinPresence.ToString();
			}
			if (_lblStepIltDevSRAge != null)
			{
				_lblStepIltDevSRAge.Text = _pIltDevSRWallMinAgeMs.ToString();
			}
			if (_lblStepIltDevSRFresh != null)
			{
				_lblStepIltDevSRFresh.Text = _pIltDevSRWallFreshMs.ToString();
			}
			if (_lblStepIltDevSRRetention != null)
			{
				_lblStepIltDevSRRetention.Text = _pIltDevSRRetentionBars.ToString();
			}
		}

		private void UpdateWpfToggleValues()
		{
			if (_btnLadW != null)
			{
				StyleWpfToggle(_btnLadW, _pLad);
			}
			if (_btnLinW != null)
			{
				StyleWpfToggle(_btnLinW, _pLin);
			}
			if (_btnStW != null)
			{
				StyleWpfToggle(_btnStW, _pSt);
			}
			if (_btnPullW != null)
			{
				StyleWpfToggle(_btnPullW, _pPull);
			}
			if (_btnBubW != null)
			{
				StyleWpfToggle(_btnBubW, _pBubShow);
			}
			if (_btnAbsW != null)
			{
				StyleWpfToggle(_btnAbsW, _pAbs);
			}
			if (_btnDirW != null)
			{
				_btnDirW.Content = (_pDir ? "BI-DIRECCIONAL (Bid← Ask→)" : "UNI-DIRECCIONAL");
				StyleWpfToggle(_btnDirW, _pDir);
			}
			if (_btnPocW != null)
			{
				StyleWpfToggle(_btnPocW, _pPoc);
			}
			if (_btnFlowW != null)
			{
				StyleWpfToggle(_btnFlowW, _pFlow);
			}
			if (_btnPassiveW != null)
			{
				StyleWpfToggle(_btnPassiveW, _pPassive);
			}
			if (_btnPassiveMarkersW != null)
			{
				StyleWpfToggle(_btnPassiveMarkersW, _pBasicVisual);
			}
			if (_btnPassiveWallsW != null)
			{
				StyleWpfToggle(_btnPassiveWallsW, _pPassiveWallZones);
			}
			if (_btnBasicVisualW != null)
			{
				StyleWpfToggle(_btnBasicVisualW, _pBasicVisual);
			}
			if (_btnIltDevZonesW != null)
			{
				StyleWpfToggle(_btnIltDevZonesW, _pIltDevZones);
			}
			if (_btnIltDevSRW != null)
			{
				StyleWpfToggle(_btnIltDevSRW, _pSRZones);
			}
			if (_btnSRHistW != null)
			{
				StyleWpfToggle(_btnSRHistW, _pSRHist);
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
				_wpfPanel.Width = 255.0;
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
				_wpfHeaderText.Text = "ILT-DEV-DOMHEATMAP DEV";
				_wpfHeaderText.FontFamily = new System.Windows.Media.FontFamily("Consolas");
				_wpfHeaderText.FontSize = 11.0;
				_wpfHeaderText.FontWeight = FontWeights.Bold;
				_wpfHeaderText.Foreground = Brushes.White;
				_wpfHeaderText.Margin = new Thickness(6.0, 3.0, 4.0, 3.0);
				_wpfHeaderText.VerticalAlignment = VerticalAlignment.Center;
				Grid.SetColumn(_wpfHeaderText, 0);
				grid.Children.Add(_wpfHeaderText);
				_wpfMinBtn = new Button();
				_wpfMinBtn.Content = (PanelMinimizedPersist ? "▶" : "▼");
				_wpfMinBtn.FontFamily = new System.Windows.Media.FontFamily("Consolas");
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
				_wpfBody.Children.Add(MakeWpfSectionHeader("VISUAL BÁSICO"));
				_btnBasicVisualW = MakeWpfToggle("HEATMAP + MARCAS");
				_btnBasicVisualW.Click += delegate(object s, RoutedEventArgs e)
				{
					VerVisualBasico = !VerVisualBasico;
					_pBasicVisual = VerVisualBasico;
					_pPassiveChartMarkers = _pBasicVisual;
					ApplyRuntimeChange(false);
					e.Handled = true;
				};
				_btnBasicVisualW.HorizontalAlignment = HorizontalAlignment.Stretch;
				_wpfBody.Children.Add(_btnBasicVisualW);
				_wpfBody.Children.Add(MakeWpfStepperRow("FILTRO VISUAL", _pF, "f", out _lblStepF));
				_wpfBody.Children.Add(MakeWpfStepperRow("VENTANA HEATMAP %", _pRenderWinPct, "renderwin", out _lblStepRenderWin));
				_wpfBody.Children.Add(MakeWpfStepperRow("VELOCIDAD CINTA", _pHeatmapScrollSpeed, "heatspeed", out _lblStepHeatSpeed));
				_wpfBody.Children.Add(MakeWpfStepperRow("PALETA", _pHeatColorMode, "heatpalette", out _lblStepHeatPalette));
				_wpfBody.Children.Add(MakeWpfStepperRow("MARCA SIZE", _pPassiveMarkerSize, "passmarksz", out _lblStepPassiveMarkerSize));
				_btnPassiveWallsW = MakeWpfToggle("MUROS / SPOOF");
				_btnPassiveWallsW.Click += delegate(object s, RoutedEventArgs e)
				{
					VerMurosLiquidezPasiva = !VerMurosLiquidezPasiva;
					_pPassiveWallZones = VerMurosLiquidezPasiva;
					ApplyRuntimeChange(false);
					e.Handled = true;
				};
				_btnPassiveWallsW.HorizontalAlignment = HorizontalAlignment.Stretch;
				_wpfBody.Children.Add(_btnPassiveWallsW);
				_wpfBody.Children.Add(MakeWpfStepperRow("FILTRO MUROS", _pWallMinQty, "wallminqty", out _lblStepWallMinQty));
				_wpfBody.Children.Add(MakeWpfSectionHeader("LADDER"));
				_wpfBody.Children.Add(MakeWpfStepperRow("MIN LADDER", _pLadF, "ladf", out _lblStepLadF));
				_wpfBody.Children.Add(MakeWpfStepperRow("LADDER X", _pLadderShiftPx, "ladx", out _lblStepLadX));
				_wpfBody.Children.Add(MakeWpfStepperRow("TICK FIJO", 1, "g", out _lblStepG));
				_wpfBody.Children.Add(MakeWpfSectionHeader("T&S / MOTOR"));
				_wpfBody.Children.Add(MakeWpfStepperRow("MIN CAPTURA", _pBub, "bub", out _lblStepBub));
				_wpfBody.Children.Add(MakeWpfSectionHeader("DOM FLOW PULSE"));
				_btnFlowW = MakeWpfToggle("DOM FLOW PULSE");
				_btnFlowW.Click += delegate(object s, RoutedEventArgs e)
				{
					VerDomFlowPulse = !VerDomFlowPulse;
					_pFlow = VerDomFlowPulse;
					ApplyRuntimeChange(false);
					e.Handled = true;
				};
				_btnFlowW.HorizontalAlignment = HorizontalAlignment.Stretch;
				_wpfBody.Children.Add(_btnFlowW);
				_wpfBody.Children.Add(MakeWpfStepperRow("FLOW MIN", _pFlowMin, "flowmin", out _lblStepFlowMin));
				_wpfBody.Children.Add(MakeWpfSectionHeader("ILT-DEV-EVENTS"));
				_btnIltDevZonesW = MakeWpfToggle("VER ILT-DEV EVENTS");
				_btnIltDevZonesW.Click += delegate(object s, RoutedEventArgs e)
				{
					VerIltDevZones = !VerIltDevZones;
					_pIltDevZones = VerIltDevZones;
					ApplyRuntimeChange(false);
					e.Handled = true;
				};
				_btnIltDevZonesW.HorizontalAlignment = HorizontalAlignment.Stretch;
				_wpfBody.Children.Add(_btnIltDevZonesW);
				_wpfBody.Children.Add(MakeWpfStepperRow("BIGTRADE AGRESIVOS", _pIltDevZoneAttackMinQty, "zoneattack", out _lblStepIltDevZoneAttack));
				_wpfBody.Children.Add(MakeWpfStepperRow("CIERRES", _pZoneCloses, "zonecloses", out _lblStepIltDevZoneCloses));
				_wpfBody.Children.Add(MakeWpfStepperRow("FUERZA MIN", _pIltDevZoneMinStrength, "zonestrength", out _lblStepIltDevZoneStrength));
				_wpfBody.Children.Add(MakeWpfSectionHeader("ILT-DEV-S/R PERSISTENTE"));
				_btnIltDevSRW = MakeWpfToggle("VER ILT-DEV S/R");
				_btnIltDevSRW.Click += delegate(object s, RoutedEventArgs e)
				{
					SRZonesActivas = !SRZonesActivas;
					_pSRZones = SRZonesActivas;
					ApplyRuntimeChange(false);
					e.Handled = true;
				};
				_btnIltDevSRW.HorizontalAlignment = HorizontalAlignment.Stretch;
				_wpfBody.Children.Add(_btnIltDevSRW);
				_wpfBody.Children.Add(MakeWpfStepperRow("BIGTRADE AGRES.", _pSRPersistBig, "srpersistbig", out _lblStepSRPersistBig));
				_wpfBody.Children.Add(MakeWpfStepperRow("PASIVA / MURO", _pSRPersistPass, "srpersistpass", out _lblStepSRPersistPass));
				_wpfBody.Children.Add(MakeWpfSectionHeader("ILT-DEV-BRACKET"));
				_btnSRHistW = MakeWpfToggle("VER BRACKET");
				_btnSRHistW.Click += delegate(object s, RoutedEventArgs e)
				{
					SRHistActivo = !SRHistActivo;
					_pSRHist = SRHistActivo;
					ApplyRuntimeChange(false);
					e.Handled = true;
				};
				_btnSRHistW.HorizontalAlignment = HorizontalAlignment.Stretch;
				_wpfBody.Children.Add(_btnSRHistW);
				_wpfBody.Children.Add(MakeWpfStepperRow("BIGTRADE AGRES.", _pSRHistBig, "srhistbig", out _lblStepSRHistBig));
				_wpfBody.Children.Add(MakeWpfStepperRow("PASIVA CONFLU.", _pSRHistPass, "srhistpass", out _lblStepSRHistPass));
				_wpfBody.Children.Add(MakeWpfSectionHeader("VISUALIZACION"));
				_btnLadW = MakeWpfToggle("LADDER");
				_btnLadW.Click += delegate(object s, RoutedEventArgs e)
				{
					VerLadder = !VerLadder;
					_pLad = VerLadder;
					ApplyRuntimeChange(false);
					e.Handled = true;
				};
				_btnLadW.HorizontalAlignment = HorizontalAlignment.Stretch;
				_wpfBody.Children.Add(_btnLadW);
				Grid grid2 = MakeTwoColRow();
				_btnStW = MakeWpfToggle("STACKING");
				_btnPullW = MakeWpfToggle("PULLING");
				_btnStW.Click += delegate(object s, RoutedEventArgs e)
				{
					VerStacking = !VerStacking;
					_pSt = VerStacking;
					ApplyRuntimeChange(false);
					e.Handled = true;
				};
				_btnPullW.Click += delegate(object s, RoutedEventArgs e)
				{
					VerPulling = !VerPulling;
					_pPull = VerPulling;
					ApplyRuntimeChange(false);
					e.Handled = true;
				};
				AddTwoColChildren(grid2, _btnStW, _btnPullW);
				_wpfBody.Children.Add(grid2);
				_btnAbsW = MakeWpfToggle("ABSORCION");
				_btnAbsW.Click += delegate(object s, RoutedEventArgs e)
				{
					VerAbsorcion = !VerAbsorcion;
					_pAbs = VerAbsorcion;
					ApplyRuntimeChange(false);
					e.Handled = true;
				};
				_btnAbsW.HorizontalAlignment = HorizontalAlignment.Stretch;
				_wpfBody.Children.Add(_btnAbsW);
				_btnDirW = MakeWpfToggle(_pDir ? "BI-DIRECCIONAL (Bid← Ask→)" : "UNI-DIRECCIONAL");
				_btnDirW.HorizontalAlignment = HorizontalAlignment.Stretch;
				_btnDirW.Click += delegate(object s, RoutedEventArgs e)
				{
					Bidireccional = !Bidireccional;
					_pDir = Bidireccional;
					ApplyRuntimeChange(false);
					e.Handled = true;
				};
				_wpfBody.Children.Add(_btnDirW);
				_btnPocW = MakeWpfToggle("DOM POC");
				_btnPocW.HorizontalAlignment = HorizontalAlignment.Stretch;
				_btnPocW.Click += delegate(object s, RoutedEventArgs e)
				{
					VerPOC = !VerPOC;
					_pPoc = VerPOC;
					ApplyRuntimeChange(false);
					e.Handled = true;
				};
				_wpfBody.Children.Add(_btnPocW);
				ScrollViewer scrollViewer = new ScrollViewer();
				scrollViewer.Content = _wpfBody;
				scrollViewer.MaxHeight = 640.0;
				scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
				scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
				stackPanel.Children.Add(scrollViewer);
				StyleWpfToggle(_btnLadW, _pLad);
				StyleWpfToggle(_btnLinW, _pLin);
				StyleWpfToggle(_btnStW, _pSt);
				StyleWpfToggle(_btnPullW, _pPull);
				StyleWpfToggle(_btnAbsW, _pAbs);
				StyleWpfToggle(_btnDirW, _pDir);
				StyleWpfToggle(_btnPocW, _pPoc);
				StyleWpfToggle(_btnFlowW, _pFlow);
				StyleWpfToggle(_btnPassiveW, _pPassive);
				StyleWpfToggle(_btnPassiveWallsW, _pPassiveWallZones);
				StyleWpfToggle(_btnBasicVisualW, _pBasicVisual);
				StyleWpfToggle(_btnIltDevZonesW, _pIltDevZones);
				StyleWpfToggle(_btnIltDevSRW, _pSRZones);
				StyleWpfToggle(_btnSRHistW, _pSRHist);
				UpdateWpfStepperValues();
				_wpfBody.Visibility = (PanelMinimizedPersist ? Visibility.Collapsed : Visibility.Visible);
				base.UserControlCollection.Add(_wpfPanel);
				_wpfBuilt = true;
				PlaceWpfPanel();
				ApplyPanelVisibility();
			}
			catch
			{
			}
		}

		private Grid MakeTwoColRow()
		{
			Grid grid = new Grid();
			grid.Margin = new Thickness(0.0, 1.0, 0.0, 1.0);
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			return grid;
		}

		private void AddTwoColChildren(Grid g, Button left, Button right)
		{
			left.HorizontalAlignment = HorizontalAlignment.Stretch;
			right.HorizontalAlignment = HorizontalAlignment.Stretch;
			Grid.SetColumn(left, 0);
			Grid.SetColumn(right, 1);
			g.Children.Add(left);
			g.Children.Add(right);
		}

		private void WpfHeader_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (_wpfPanel != null)
			{
				_wpfDragging = true;
				_wpfDragStart = e.GetPosition(base.ChartControl);
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
				System.Windows.Point position = e.GetPosition(base.ChartControl);
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
			e.Handled = true;
		}

		private void ClampWpfPosition(ref double x, ref double y)
		{
			double num = ((base.ChartPanel != null) ? base.ChartPanel.W : 800);
			double num2 = ((base.ChartPanel != null) ? base.ChartPanel.H : 600);
			if (_wpfPanel != null && _wpfPanel.Width > 0.0)
			{
				double width = _wpfPanel.Width;
			}
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
					double num = ((_wpfPanel.Width > 0.0) ? _wpfPanel.Width : ((_wpfPanel.DesiredSize.Width > 0.0) ? _wpfPanel.DesiredSize.Width : 255.0));
					double num2 = ((_wpfPanel.DesiredSize.Height > 0.0) ? _wpfPanel.DesiredSize.Height : 300.0);
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
			catch
			{
			}
			_wpfPanel = null;
			_wpfBody = null;
			_wpfHeaderText = null;
			_wpfMinBtn = null;
			_btnLadW = (_btnLinW = (_btnStW = (_btnPullW = null)));
			_btnBubW = (_btnAbsW = (_btnDirW = (_btnPocW = (_btnFlowW = (_btnPassiveW = (_btnPassiveMarkersW = null))))));
			_btnKeepHistW = null;
			_lblStepF = (_lblStepLadF = (_lblStepG = null));
			_lblStepRenderWin = (_lblStepHeatSpeed = (_lblStepHeatPalette = null));
			_lblStepBub = (_lblStepBlock = null);
			_lblStepFlowMin = (_lblStepFlowFade = (_lblStepFlowOp = (_lblStepFlowSize = (_lblStepFlowLbl = null))));
			_lblStepPassiveMin = (_lblStepPassiveDepth = (_lblStepPassiveEvent = (_lblStepPassiveMarkerSize = (_lblStepPassiveLabelMin = null))));
			_lblStepMaxHist = null;
			_wpfBuilt = false;
		}

		private void BuildToolbarButton()
		{
			if (_toolbarBtnAdded || !ShowToolbarButton || base.ChartControl == null)
			{
				return;
			}
			try
			{
				Window window = Window.GetWindow(((FrameworkElement)base.ChartControl).Parent);
				_chartWindow = (Chart)((window is Chart) ? window : null);
				if (_chartWindow != null && _chartWindow.MainMenu != null)
				{
					_toolbarBtn = new NTMenuItem();
					((HeaderedItemsControl)_toolbarBtn).Header = "ILT-DEV-HEATMAP DEV";
					((FrameworkElement)_toolbarBtn).Style = Application.Current.TryFindResource("MainMenuItem") as Style;
					((Control)_toolbarBtn).Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(byte.MaxValue, 90, 190, byte.MaxValue));
					((FrameworkElement)_toolbarBtn).ToolTip = "Mostrar / ocultar panel ILT-DEV DOM Heatmap";
					((DependencyObject)_toolbarBtn).SetValue(AutomationProperties.AutomationIdProperty, "ILT_DEV_HEATMAP_DEV_TOOLBAR_BTN_" + DateTime.Now.Ticks);
					((MenuItem)_toolbarBtn).Click += ToolbarBtn_Click;
					_chartWindow.MainMenu.Add(_toolbarBtn);
					_toolbarBtnAdded = true;
				}
			}
			catch
			{
			}
		}

		private void ToolbarBtn_Click(object sender, RoutedEventArgs e)
		{
			if (!_wpfBuilt)
			{
				BuildWpfPanel();
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
					((MenuItem)_toolbarBtn).Click -= ToolbarBtn_Click;
				}
				if (_chartWindow != null && _chartWindow.MainMenu != null && _toolbarBtn != null && _chartWindow.MainMenu.Contains(_toolbarBtn))
				{
					_chartWindow.MainMenu.Remove(_toolbarBtn);
				}
			}
			catch
			{
			}
			_toolbarBtn = null;
			_chartWindow = null;
			_toolbarBtnAdded = false;
		}
	}
}

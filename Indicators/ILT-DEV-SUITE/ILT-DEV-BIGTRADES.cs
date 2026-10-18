#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
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
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

// ---------------------------------------------------------------------------
//  ILT-DEV BIGTRADES  -  Ali G / IronLionTrader
//
//  Burbujas de operaciones grandes sobre el grafico de precio.
//
//  El motor de captura, fusion y dibujado es el mismo que el de ILT-DEV DOMHEATMAP:
//  se recupero metodo a metodo desde el ensamblado para que las burbujas se
//  vean exactamente igual. Aqui va solo: cinta -> burbujas. Nada de heatmap,
//  ladder, muros, S/R ni liquidez pasiva.
//
//  Los tipos y el nombre del indicador son distintos a proposito, para que
//  este indicador y ILT-DEV DOMHEATMAP puedan convivir en el mismo NinjaTrader.
// ---------------------------------------------------------------------------

/// Una operacion agresiva capturada de la cinta.
public class IltDevBtTrade
{
	public double	Price;
	public double	RawPrice;
	public long		Vol;
	public long		MaxSingle;
	public int		TradeCount;
	public bool		IsBuy;
	public double	Time;
	public int		CreatedMs;
	public int		LastMs;
	public int		BarIdx;
	public long		Seq;
	public bool		IsBlock;
	public long		StampTicks;
}

/// Copia inmutable de una operacion, lista para dibujar.
public class IltDevBtBubble
{
	public double	Price;
	public long		Vol;
	public long		MaxSingle;
	public int		TradeCount;
	public bool		IsBuy;
	public int		CreatedMs;
	public int		LastMs;
	public int		BarIdx;
	public long		Seq;
	public bool		IsBlock;
	public long		StampTicks;
}

namespace NinjaTrader.NinjaScript.Indicators.ILT_DEV_SUITE
{
	public class ILT_DEV_BIGTRADES : Indicator
	{
		#region ===================== CONSTANTES =====================

		private const string	ToolbarBtnTag				= "ILT_DEV_BIGTRADES_BTN";
		private const int		NumFilasPanel				= 5;

		private const int		TRADE_HISTORY_HARD_MAX		= 350000;
		private const int		BT_ARCHIVE_HARD_MAX			= 250000;
		private const int		TRADE_TRIM_BUFFER			= 8192;

		/// Ventana de fusion de agresiones en el mismo precio y lado: 280 ms.
		private const long		AGGRESSIVE_BUBBLE_GROUP_TICKS = 2800000L;

		#endregion

		#region ===================== CAMPOS =====================

		private static readonly Stopwatch	iltDevMonoClock	= Stopwatch.StartNew();
		private static readonly string[]	fontFallback	= new string[]
			{ "Consolas", "Lucida Console", "Courier New", "DejaVu Sans Mono", "Segoe UI" };

		private readonly object	lk = new object();

		// --- cinta ---
		private List<IltDevBtTrade>					trades			= new List<IltDevBtTrade>(4096);
		private List<IltDevBtTrade>					btArchive		= new List<IltDevBtTrade>(4096);
		private Dictionary<int, List<IltDevBtTrade>>	tradesByBar		= new Dictionary<int, List<IltDevBtTrade>>();
		private Dictionary<int, List<IltDevBtTrade>>	btArchiveByBar	= new Dictionary<int, List<IltDevBtTrade>>();

		private long		tradeSeq;
		private long		maxTradeVol		= 1L;
		private double		lastTapePrice;
		private int			lastTapeDir;
		private double		quoteBestBid;
		private double		quoteBestAsk	= double.MaxValue;
		private int			lastLiveLastMs	= -100000000;
		private int			histPrimaryAnchorBar = -1;

		/// Umbral sugerido para el instrumento actual. Se calcula una vez al cargar
		/// porque se consulta en caliente, en cada operacion y en cada fotograma.
		private int			autoQty			= 50;

		/// Suelo de captura que estaba en vigor cuando se proceso el historico.
		/// Si luego se baja, lo viejo ya no se puede recuperar sin recargar.
		private int			sueloAlCargar	= 1;
		private DateTime	sessionDate		= DateTime.MinValue;
		private double		tick			= 0.25;
		private TimeZoneInfo nyTz;

		// --- cache de dibujo ---
		private List<IltDevBtBubble>		bubbleRenderCache	= new List<IltDevBtBubble>(1024);
		private HashSet<long>			bubbleSeqCache		= new HashSet<long>();
		private Dictionary<int, float>	bubblePriceYCache	= new Dictionary<int, float>(512);

		/// Copia que usa el hilo de dibujo. La lista viva se puede vaciar desde el
		/// hilo de datos (cambio de sesion) en mitad de un fotograma.
		private readonly List<IltDevBtBubble> renderBuf = new List<IltDevBtBubble>(1024);
		private bool	bubbleRenderCacheDirty	= true;
		private int		bubbleCacheFrom			= int.MinValue;
		private int		bubbleCacheTo			= int.MinValue;
		private int		bubbleCacheVisualMin	= -1;
		private int		bubbleCacheVisualMax	= -1;
		private long	bubbleCacheSeq			= -1;
		private int		bubbleCacheTradeCount	= -1;
		private int		bubbleCacheBtCount		= -1;
		private long	bubbleCacheMaxVol		= 1L;

		private float[]	bubbleBarXCache;
		private long[]	bubbleBarTCache;
		private int		bubbleBarTCacheB0		= int.MinValue;
		private int		bubbleBarTCacheCount	= -1;
		private long	bubbleBarTCacheTail		= -1;

		// --- refresco ---
		private DispatcherTimer	flowTimer;
		private volatile bool	flowTimerActive;
		private volatile bool	terminado;
		private int				lastRefreshMs;
		private int				lastLiveBtMs	= int.MinValue;
		private volatile bool	pendienteRepintar;

		// --- contadores de diagnostico ---
		private const string	DiagTag			= "ILT_DEV_BIGTRADES_DIAG";
		private int				diagPrints;			// eventos Last recibidos en tiempo real
		private int				diagGuardados;		// prints que pasaron el suelo de captura
		private int				diagBigTrades;		// operaciones que cruzaron el umbral
		private int				diagBurbujas;		// burbujas dibujadas en el ultimo fotograma
		private int				diagUltimoBigMs		= int.MinValue;
		private long			diagMayorPrint;		// print individual mas grande visto
		private long			diagMayorAgregado;	// mayor volumen ya fusionado en una burbuja
		private int				diagHistorico;		// operaciones procesadas desde la serie de 1 tick
		private int				diagPrimerPrintMs	= int.MinValue;
		private string			diagUltimoAviso		= "";
		private int				diagUltimoPintadoMs;

		// --- recursos SharpDX ---
		private SharpDX.Direct2D1.SolidColorBrush	bBubBuy;
		private SharpDX.Direct2D1.SolidColorBrush	bBubSell;
		private SharpDX.Direct2D1.SolidColorBrush	bBubBord;
		private SharpDX.Direct2D1.SolidColorBrush	bBubFlash;
		private SharpDX.Direct2D1.SolidColorBrush	bPanSh;
		private SharpDX.Direct2D1.SolidColorBrush	bTxt;
		private SharpDX.DirectWrite.TextFormat		fSm;

		private bool	dxOk;
		private IntPtr	dxRtRef			= IntPtr.Zero;
		private float	curDpi			= -1f;
		private float	lastFontSmall	= -1f;
		private string	lastVisualSig	= null;

		// --- UI ---
		private Chart		chartWindow;
		private NTMenuItem	toolbarBtn;
		private bool		toolbarBtnAdded;
		private Border		wpfOuterPanel;
		private bool		wpfPanelAdded;
		private bool		wpfPanelMinimized;
		private bool		panelUserHidden;
		private Button		wpfMinimizeButton;
		private Button		btnBurbujas;
		private TextBox		cajaMin;
		private TextBox		cajaMax;
		private TextBox		cajaTamano;
		private Button		btnAcumMin;
		private TextBox		cajaActiva;
		private Action<int>	aplicarActiva;
		private string		bufferActivo	= string.Empty;
		private bool			tecladoEnganchado;

		/// Donde quedo enganchado el manejador de teclado. Se guarda aparte porque
		/// al cerrar el indicador chartWindow ya se ha puesto a null antes de que
		/// corra la limpieza en el hilo de la interfaz.
		private Chart			ventanaTeclado;
		private ChartControl	controlTeclado;
		private Grid[]		filasPanel = new Grid[NumFilasPanel];
		private bool		wpfPanelDragging;
		private System.Windows.Point wpfDragStartPoint;
		private double		wpfDragStartLeft;
		private double		wpfDragStartTop;
		private double		wpfPanelLeft		= 14;
		private double		wpfPanelTop		= 14;
		private bool		restorePanelOpen;

		#endregion

		#region ===================== CICLO DE VIDA =====================

		public override string DisplayName
		{
			get { return Name; }
		}

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Name = "ILT-DEV-BIGTRADES";
				Description					= "ILT-DEV-BIGTRADES: burbujas de operaciones grandes sobre el grafico. Captura la cinta, fusiona las agresiones del mismo precio y lado dentro de 280 ms, y dibuja cada bloque con su animacion de entrada.";
				IsOverlay					= true;
				DisplayInDataBox			= false;
				DrawOnPricePanel			= true;
				PaintPriceMarkers			= false;
				IsSuspendedWhileInactive	= false;
				Calculate					= Calculate.OnEachTick;

				// 01. General
				VerBurbujas					= true;
				ShowToolbarButton			= true;
				Diagnostico					= false;
				FlowRefreshFps				= 14;
				AcumularPrints				= true;

				// 02. Umbral
				BigTradeVisualMinQty		= 0;	// 0 = automatico por instrumento
				BigTradeVisualMaxQty		= 0;	// 0 = sin limite

				// 04. Colores
				TamanoBurbujaPct			= 100;
				RadioMinimoPx				= 0;
				RadioMaximoPx				= 0;

				BigTradeBuyColor			= Brushes.LimeGreen;
				BigTradeSellColor			= Brushes.Red;
				BubbleBorderColor			= Brushes.WhiteSmoke;
				BubbleLightningColor		= Brushes.WhiteSmoke;
				TextColor					= Brushes.WhiteSmoke;

				// 05. Opacidad
				BubbleOpacity				= 78;
				BubbleBorderOpacity			= 58;
				BubbleHighlightOpacity		= 38;

				// 06. Animacion
				BubbleAnimationMs			= 850;
				BubbleGlowStrength			= 46;
				BubblePulseStrength			= 12;
				BubbleShockwave				= true;
				BubbleLightning				= true;
				BigTradeFlash				= true;
				BigTradeFlashMs				= 6000;
				BigTradeFlashStrength		= 70;

				// 07. Texto
				FontSizeSmall				= 11;
			}
			else if (State == State.Configure)
			{
				try { tick = Instrument != null && Instrument.MasterInstrument != null ? Instrument.MasterInstrument.TickSize : 0.25; }
				catch { tick = 0.25; }

				if (tick <= 0) tick = 0.25;

				// Serie de 1 tick. Es la que permite reconstruir las burbujas del
				// historico: en el historico no hay OnMarketData, solo esta serie.
				AddDataSeries(BarsPeriodType.Tick, 1);
			}
			else if (State == State.DataLoaded)
			{
				try { tick = Instrument != null && Instrument.MasterInstrument != null ? Instrument.MasterInstrument.TickSize : 0.25; }
				catch { tick = 0.25; }

				if (tick <= 0) tick = 0.25;

				autoQty			= Math.Max(1, DefaultBigTradeQtyForInstrument());
				sueloAlCargar	= GetTapeCaptureFloor();

				ResetSesion(DateTime.MinValue);
			}
			else if (State == State.Historical)
			{
				if (ChartControl != null && ShowToolbarButton)
					ChartControl.Dispatcher.InvokeAsync(() =>
					{
						LoadRuntimePanelState();
						BuildToolbarButton();

						if (restorePanelOpen)
						{
							panelUserHidden = false;
							EnsureWpfControlPanel();
						}
					});
			}
			else if (State == State.Terminated)
			{
				terminado = true;

				if (chartWindow == null && ChartControl != null)
				{
					try { chartWindow = Window.GetWindow(ChartControl.Parent) as Chart; }
					catch { }
				}

				Chart		winRef		= chartWindow;
				NTMenuItem	btnRef		= toolbarBtn;
				Border		panelRef	= wpfOuterPanel;

				// Las brochas y la fuente son del hilo de la interfaz: liberarlas desde
				// el hilo de datos mientras OnRender las esta usando tumba NinjaTrader.
				if (ChartControl != null)
					ChartControl.Dispatcher.InvokeAsync(() =>
					{
						DesengancharTeclado();
						StopFlowTimer();
						CleanupChartUi(winRef, btnRef, panelRef);
						FreeDx();
					});
				else
				{
					DesengancharTeclado();
					StopFlowTimer();
					FreeDx();
				}

				toolbarBtn		= null;
				wpfOuterPanel	= null;
				toolbarBtnAdded	= false;
				wpfPanelAdded	= false;
				chartWindow		= null;
			}
		}

		protected override void OnBarUpdate()
		{
			if (BarsInProgress == 0)
			{
				if (!Diagnostico)
					return;

				// Como mucho cuatro veces por segundo.
				int ahora = IltDevNowMs;

				if (ahora - diagUltimoPintadoMs < 250)
					return;

				diagUltimoPintadoMs = ahora;

				PintarDiagnostico();
				return;
			}

			if (BarsInProgress != 1)
				return;

			ProcesarTickDeSerie();
		}

		/// Cada barra de la serie de 1 tick es una operacion. Es la unica via para
		/// reconstruir las burbujas del historico, porque OnMarketData no se ejecuta
		/// sobre datos historicos.
		private void ProcesarTickDeSerie()
		{
			if (CurrentBars == null || CurrentBars.Length < 2)
				return;

			if (CurrentBars[1] < 0 || CurrentBars[0] < 0)
				return;

			DateTime t = Times[1][0];
			DateTime ny = ToNewYorkTime(t);

			if (!IsInsideNyTradingSessionNy(ny))
				return;

			DateTime sess = GetNyTradingSessionDateNy(ny);

			if (sessionDate == DateTime.MinValue || sess != sessionDate)
				ResetSesion(sess);

			// En tiempo real manda OnMarketData, que ademas trae bid y ask.
			// Si acaba de procesar, esta misma operacion ya esta contada.
			if (State == State.Realtime && IltDevNowMs - lastLiveLastMs < 1000)
				return;

			long vol = (long)Math.Max(0.0, Volumes[1][0]);

			if (vol <= 0)
				return;

			double price = Closes[1][0];

			// Las operaciones pequenas del historico no se guardan, pero si
			// alimentan la regla del tick para no perder la direccion.
			if (State != State.Realtime && vol < GetTapeCaptureFloor())
			{
				UpdateTapeDirectionOnly(price);
				return;
			}

			int barIdx = State != State.Realtime
				? GetPrimaryBarIndex(t, price)
				: PrimaryBarIndexSafe();

			diagHistorico++;

			ProcessTapeTrade(price, vol, barIdx, t);
		}

		/// A que vela del grafico pertenece una operacion del historico.
		private int GetPrimaryBarIndex(DateTime t, double price)
		{
			if (CurrentBars == null || CurrentBars.Length <= 0)
				return -1;

			int actual = CurrentBars[0];

			if (actual < 0)
				return -1;

			int ultima = actual;

			try
			{
				if (BarsArray != null && BarsArray.Length > 0 && BarsArray[0] != null)
					ultima = Math.Max(actual, BarsArray[0].Count - 1);
			}
			catch { ultima = actual; }

			double tol = 1.15;

			if (State == State.Realtime)
			{
				if (PrimaryBarContainsPrice(actual, price, tol))
					return actual;

				int desde = Math.Max(0, actual - 3);
				int hasta = Math.Min(ultima, actual + 2);

				for (int i = desde; i <= hasta; i++)
					if (PrimaryBarContainsPrice(i, price, tol))
						return i;

				return -1;
			}

			int idx = -1;

			try
			{
				if (BarsArray != null && BarsArray.Length > 0 && BarsArray[0] != null)
					idx = BarsArray[0].GetBar(t);
			}
			catch { idx = -1; }

			if (idx < 0)
			{
				idx = histPrimaryAnchorBar < 0
					? Math.Min(actual, ultima)
					: Math.Min(histPrimaryAnchorBar, ultima);

				if (idx < 0)
					return -1;
			}

			if (idx > ultima)	idx = ultima;
			if (idx < 0)		idx = 0;

			try
			{
				int pasos = 0;

				while (true)
				{
					if (Times[0].GetValueAt(idx) >= t)
						break;

					idx++;

					if (idx >= ultima)
						break;

					if (pasos++ >= 64)
						break;
				}
			}
			catch { }

			histPrimaryAnchorBar = idx;

			return idx;
		}

		private bool PrimaryBarContainsPrice(int bar, double price, double tol)
		{
			if (bar < 0)
				return false;

			try
			{
				double alto = Highs[0].GetValueAt(bar);
				double bajo = Lows[0].GetValueAt(bar);

				double margen = Math.Max(tick, 1e-10) * Math.Max(0.55, tol);

				if (price > alto + margen)
					return false;

				return !(price < bajo - margen);
			}
			catch { }

			return false;
		}

		/// Cuadro de estado. Sirve para ver de un vistazo por que no aparecen
		/// burbujas: si no llegan prints, si el umbral se los come todos, o si
		/// el indicador todavia no esta en tiempo real.
		private void PintarDiagnostico()
		{
			try
			{
				string estado = State == State.Realtime ? "TIEMPO REAL" : ("historico (" + State + ")");

				string desde = "-";

				if (diagUltimoBigMs != int.MinValue)
				{
					int seg = (IltDevNowMs - diagUltimoBigMs) / 1000;

					if (seg >= 0)
						desde = seg < 90 ? (seg + "s") : ((seg / 60) + "m");
				}

				string umbral = "BT " + UmbralMin
					+ (BigTradeVisualMinQty > 0 ? "" : " (auto)")
					+ (BigTradeVisualMaxQty > 0 ? " a " + BigTradeVisualMaxQty : " sin tope");

				string ritmo = "";

				if (diagPrimerPrintMs != int.MinValue)
				{
					int seg = (IltDevNowMs - diagPrimerPrintMs) / 1000;

					if (seg >= 0)
						ritmo = " en " + (seg < 90 ? (seg + "s") : ((seg / 60) + "m"));
				}

				string txt =
					  "ILT-DEV-BIGTRADES  ·  " + estado
					+ "\nhistorico " + diagHistorico
					+ "   prints " + diagPrints + ritmo
					+ "   guardados " + diagGuardados
					+ "   bigtrades " + diagBigTrades
					+ "\nburbujas en pantalla " + diagBurbujas
					+ "   ultimo hace " + desde
					+ "\n" + umbral
					+ (AcumularPrints ? "   acum ON" : "   acum OFF")
					+ " (captura desde " + GetTapeCaptureFloor() + ")"
					+ "\nprint mayor " + diagMayorPrint
					+ "   agregado mayor " + diagMayorAgregado;

				if (!string.IsNullOrEmpty(diagUltimoAviso))
					txt += "\naviso: " + diagUltimoAviso;

				Draw.TextFixed(this, DiagTag, txt, TextPosition.TopLeft);
			}
			catch { }
		}

		#endregion

		#region ===================== CINTA =====================

		/// Reloj monotono en ms. Da la vuelta cada ~24,8 dias en vez de quedarse
		/// clavado en int.MaxValue: al dar la vuelta las burbujas vivas pasan a
		/// contarse como terminadas y todo sigue, en lugar de congelarse para siempre.
		private static int IltDevNowMs
		{
			get { return (int)(iltDevMonoClock.ElapsedMilliseconds & 0x7FFFFFFF); }
		}

		protected override void OnMarketData(MarketDataEventArgs e)
		{
			if (e == null)
				return;

			if (e.MarketDataType == MarketDataType.Bid)
			{
				if (e.Price > 0)
					lock (lk) { quoteBestBid = e.Price; }

				return;
			}

			if (e.MarketDataType == MarketDataType.Ask)
			{
				if (e.Price > 0)
					lock (lk) { quoteBestAsk = e.Price; }

				return;
			}

			if (e.MarketDataType != MarketDataType.Last || State != State.Realtime)
				return;

			diagPrints++;

			if (diagPrimerPrintMs == int.MinValue)
				diagPrimerPrintMs = IltDevNowMs;

			long vol = e.Volume;

			if (vol > diagMayorPrint)
				diagMayorPrint = vol;

			if (vol <= 0 || !(e.Price > 0))
				return;

			DateTime t;

			try { t = e.Time; }
			catch
			{
				try { t = CurrentBar < 0 ? DateTime.Now : Time[0]; }
				catch { t = DateTime.Now; }
			}

			DateTime ny = ToNewYorkTime(t);

			if (!IsInsideNyTradingSessionNy(ny))
			{
				diagUltimoAviso = "pausa 17-18 NY";
				return;
			}

			DateTime sess = GetNyTradingSessionDateNy(ny);

			if (sessionDate == DateTime.MinValue || sess != sessionDate)
				ResetSesion(sess);

			ProcessTapeTrade(e.Price, vol, PrimaryBarIndexSafe(), t);

			lastLiveLastMs = IltDevNowMs;

			bool nuevo = pendienteRepintar;
			pendienteRepintar = false;

			StartFlowTimerIfNeeded();
			RequestRefresh(nuevo);
		}

		/// Cambio de dia de negociacion. NO se borra el historico de operaciones:
		/// solo se recorta si excede el tope, para que las burbujas de sesiones
		/// anteriores sigan en el grafico.
		private void ResetSesion(DateTime sess)
		{
			lock (lk)
			{
				sessionDate		= sess.Date;
				quoteBestBid	= 0.0;
				quoteBestAsk	= double.MaxValue;
				lastLiveLastMs	= -100000000;

				TrimTradeHistoryLocked(false);
				TrimBigTradeArchiveLocked(false);
				InvalidateBubbleRenderCacheLocked();
			}
		}

		private int PrimaryBarIndexSafe()
		{
			try
			{
				if (CurrentBars != null && CurrentBars.Length > 0 && CurrentBars[0] >= 0)
					return CurrentBars[0];
			}
			catch { }

			try
			{
				if (BarsArray != null && BarsArray.Length > 0 && BarsArray[0] != null && BarsArray[0].Count > 0)
					return BarsArray[0].Count - 1;
			}
			catch { }

			return CurrentBar < 0 ? 0 : CurrentBar;
		}

		private int NormalizeToTickPriceKey(double price)
		{
			double t = Math.Max(tick, 1e-10);
			return (int)Math.Round(price / t, 1);
		}

		private double PriceFromKey(int key)
		{
			double p = (double)key * Math.Max(tick, 1e-10);

			try
			{
				if (Instrument != null && Instrument.MasterInstrument != null)
					return Instrument.MasterInstrument.RoundToTickSize(p);
			}
			catch { }

			return p;
		}

		/// Umbral minimo en vigor. Si la propiedad esta en 0 manda el cuadro por
		/// instrumento; si tiene un numero, manda ese numero y nada lo pisa.
		private int UmbralMin
		{
			get { return BigTradeVisualMinQty > 0 ? BigTradeVisualMinQty : Math.Max(1, autoQty); }
		}

		/// Umbral maximo en vigor. 0 en la propiedad significa sin limite.
		private int UmbralMax
		{
			get { return BigTradeVisualMaxQty > 0 ? BigTradeVisualMaxQty : int.MaxValue; }
		}

		/// Suelo de captura: por debajo de esto el print ni se guarda, asi que
		/// tampoco puede sumarse con otros.
		///
		/// Con ACUM MIN encendido se guarda todo (suelo 1) y los prints del mismo
		/// precio y lado se suman dentro de 280 ms: un bigtrade puede salir de una
		/// rafaga de prints pequenos, que es lo normal en NQ.
		/// Apagado, solo se guardan los que ya llegan al umbral por si solos.
		private int GetTapeCaptureFloor()
		{
			return AcumularPrints ? 1 : UmbralMin;
		}

		private bool ShouldStoreTapePrint(long vol)
		{
			return !(vol < (long)GetTapeCaptureFloor());
		}

		private int GetTickRuleDirection(double price)
		{
			int dir;

			if (!(lastTapePrice > 0))			dir = 1;
			else if (price > lastTapePrice)		dir = 1;
			else if (price < lastTapePrice)		dir = -1;
			else								dir = lastTapeDir != 0 ? lastTapeDir : 1;

			lastTapePrice	= price;
			lastTapeDir		= dir;

			return dir;
		}

		/// Alimenta la regla del tick con una operacion que no se va a guardar.
		/// Sirve para que el historico no pierda la direccion al saltarse los
		/// prints pequenos.
		private void UpdateTapeDirectionOnly(double price)
		{
			if (price <= 0 || double.IsNaN(price) || double.IsInfinity(price))
				return;

			ClassifyTradeSide(price);
		}

		/// +1 agresor comprador, -1 agresor vendedor.
		/// Se decide contra bid/ask con media horquilla de tolerancia; si no hay
		/// cotizacion utilizable se cae a la regla del tick.
		private int ClassifyTradeSide(double price)
		{
			double bid = 0;
			double ask = double.MaxValue;

			lock (lk)
			{
				bid = quoteBestBid;
				ask = quoteBestAsk;
			}

			bool hayCotizacion = bid > 0 && ask < double.MaxValue && ask > 0 && !(ask < bid);

			double medioTick = Math.Max(tick, 1e-10) * 0.5;

			if (hayCotizacion)
			{
				if (!(price < ask - medioTick))
				{
					lastTapePrice	= price;
					lastTapeDir		= 1;
					return 1;
				}

				if (!(price > bid + medioTick))
				{
					lastTapePrice	= price;
					lastTapeDir		= -1;
					return -1;
				}
			}

			return GetTickRuleDirection(price) < 0 ? -1 : 1;
		}

		private void ProcessTapeTrade(double price, long vol, int barIdx, DateTime time)
		{
			if (vol <= 0)
				return;

			if (price <= 0 || double.IsNaN(price) || double.IsInfinity(price))
				return;

			bool	live	= State == State.Realtime;
			int		nowMs	= live ? IltDevNowMs : 0;

			int		key		= NormalizeToTickPriceKey(price);
			double	p		= PriceFromKey(key);
			int		lado	= ClassifyTradeSide(price);

			bool isBuy = lado > 0 ? true : (lado < 0 ? false : !(lastTapeDir < 0));

			lock (lk)
			{
				if (!(barIdx >= 0 && ShouldStoreTapePrint(vol)))
					return;

				diagGuardados++;

				long		stamp = time.Ticks;
				IltDevBtTrade	tr;

				if (!TryMergeAggressiveBubbleLocked(p, vol, isBuy, barIdx, nowMs, stamp, out tr) || tr == null)
				{
					tr = new IltDevBtTrade();

					tr.Price		= p;
					tr.RawPrice		= p;
					tr.Vol			= vol;
					tr.MaxSingle	= vol;
					tr.TradeCount	= 1;
					tr.IsBuy		= isBuy;
					tr.Time			= (double)nowMs;
					tr.CreatedMs	= nowMs;
					tr.LastMs		= nowMs;
					tr.BarIdx		= barIdx;
					tr.StampTicks	= stamp;
					tr.Seq			= ++tradeSeq;
					tr.IsBlock		= false;

					trades.Add(tr);
					AddTradeToBarIndexLocked(tradesByBar, tr);
				}

				long pico = Math.Max(tr.Vol, tr.MaxSingle);

				if (pico > diagMayorAgregado)
					diagMayorAgregado = pico;

				if (!(pico < (long)UmbralMin))
				{
					if (!IsTradeArchivedLocked(tr.Seq))
					{
						if (btArchive == null)
							btArchive = new List<IltDevBtTrade>();

						btArchive.Add(tr);
						AddTradeToBarIndexLocked(btArchiveByBar, tr);
						TrimBigTradeArchiveLocked(false);
					}

					tr.IsBlock = true;

					if (live)
					{
						lastLiveBtMs		= nowMs;
						diagUltimoBigMs		= nowMs;
						pendienteRepintar	= true;
					}

					diagBigTrades++;
				}

				if (pico > maxTradeVol)
					maxTradeVol = pico;

				TrimTradeHistoryLocked(false);
				InvalidateBubbleRenderCacheLocked();
			}
		}

		/// Une el print con la ultima burbuja del mismo precio y lado si entra en
		/// la ventana de 280 ms. Recorre como mucho 32 operaciones hacia atras y
		/// corta en cuanto cambia de vela.
		private bool TryMergeAggressiveBubbleLocked(double price, long vol, bool isBuy, int barIdx,
			int nowMs, long stampTicks, out IltDevBtTrade merged)
		{
			merged = null;

			if (trades == null || trades.Count == 0)
				return false;

			if (vol <= 0 || barIdx < 0)
				return false;

			if (stampTicks <= 0)
				return false;

			long	ventana		= AGGRESSIVE_BUBBLE_GROUP_TICKS;
			int		key			= NormalizeToTickPriceKey(price);
			int		maxScan		= 32;
			int		i			= trades.Count - 1;
			int		recorridas	= 0;

			while (i >= 0 && recorridas < maxScan)
			{
				IltDevBtTrade t = trades[i];

				if (t != null)
				{
					if (t.BarIdx != barIdx)
						return false;

					if (t.IsBuy == isBuy)
					{
						double pt = t.RawPrice > 0 ? t.RawPrice : t.Price;

						if (NormalizeToTickPriceKey(pt) == key && t.StampTicks > 0)
						{
							long dt = stampTicks - t.StampTicks;

							if (dt >= 0 && dt <= ventana)
							{
								t.Vol += vol;

								if (vol > t.MaxSingle)
									t.MaxSingle = vol;

								t.TradeCount	= Math.Max(1, t.TradeCount + 1);
								t.StampTicks	= stampTicks;

								if (nowMs > 0)
								{
									t.LastMs	= nowMs;
									t.Time		= (double)nowMs;
								}

								merged = t;
								return true;
							}
						}
					}
				}

				i--;
				recorridas++;
			}

			return false;
		}

		private bool IsTradeArchivedLocked(long seq)
		{
			if (seq <= 0 || btArchive == null || btArchive.Count == 0)
				return false;

			int i = btArchive.Count - 1;

			while (true)
			{
				IltDevBtTrade t = btArchive[i];

				if (t != null && t.Seq == seq)
					return true;

				if (btArchive.Count - i > 128)
					return false;

				i--;

				if (i < 0)
					return false;
			}
		}

		private static void AddTradeToBarIndexLocked(Dictionary<int, List<IltDevBtTrade>> idx, IltDevBtTrade t)
		{
			if (idx == null || t == null || t.BarIdx < 0)
				return;

			List<IltDevBtTrade> lista;

			if (!idx.TryGetValue(t.BarIdx, out lista))
			{
				lista = new List<IltDevBtTrade>(8);
				idx[t.BarIdx] = lista;
			}

			lista.Add(t);
		}

		private void RebuildTradeBarIndexLocked()
		{
			if (tradesByBar == null)
				tradesByBar = new Dictionary<int, List<IltDevBtTrade>>();
			else
				tradesByBar.Clear();

			if (trades == null)
				return;

			for (int i = 0; i < trades.Count; i++)
				AddTradeToBarIndexLocked(tradesByBar, trades[i]);

			InvalidateBubbleRenderCacheLocked();
		}

		private void RebuildBtArchiveBarIndexLocked()
		{
			if (btArchiveByBar == null)
				btArchiveByBar = new Dictionary<int, List<IltDevBtTrade>>();
			else
				btArchiveByBar.Clear();

			if (btArchive == null)
				return;

			for (int i = 0; i < btArchive.Count; i++)
				AddTradeToBarIndexLocked(btArchiveByBar, btArchive[i]);

			InvalidateBubbleRenderCacheLocked();
		}

		private void RecalculateMaxTradeVolLocked()
		{
			long max = 1L;

			if (trades != null)
			{
				for (int i = 0; i < trades.Count; i++)
				{
					IltDevBtTrade t = trades[i];

					t.IsBlock = !(Math.Max(t.Vol, t.MaxSingle) < (long)UmbralMin);

					if (Math.Max(t.Vol, t.MaxSingle) > max)
						max = Math.Max(t.Vol, t.MaxSingle);
				}
			}

			maxTradeVol = max;
		}

		private void TrimTradeHistoryLocked(bool force)
		{
			if (trades == null)
				return;

			int limite = force ? TRADE_HISTORY_HARD_MAX : TRADE_HISTORY_HARD_MAX + TRADE_TRIM_BUFFER;

			if (trades.Count <= limite)
				return;

			int exceso = trades.Count - TRADE_HISTORY_HARD_MAX;

			if (exceso <= 0)
				return;

			trades.RemoveRange(0, exceso);
			RebuildTradeBarIndexLocked();
			InvalidateBubbleRenderCacheLocked();

			if (force || State == State.Realtime)
				RecalculateMaxTradeVolLocked();

			InvalidateBubbleRenderCacheLocked();
		}

		private void TrimBigTradeArchiveLocked(bool force)
		{
			if (btArchive == null)
				return;

			int limite = force ? BT_ARCHIVE_HARD_MAX : BT_ARCHIVE_HARD_MAX + TRADE_TRIM_BUFFER;

			if (btArchive.Count <= limite)
				return;

			int exceso = btArchive.Count - BT_ARCHIVE_HARD_MAX;

			if (exceso <= 0)
				return;

			btArchive.RemoveRange(0, exceso);
			RebuildBtArchiveBarIndexLocked();
			InvalidateBubbleRenderCacheLocked();
		}

		#endregion

		#region ===================== UMBRAL POR INSTRUMENTO =====================

		/// Minimo de contratos por defecto de cada futuro. Es el mismo cuadro que
		/// usa ILT-DEV DOMHEATMAP. Se aplica al cargar los datos y solo cuando la
		/// propiedad "Umbral automatico" esta encendida.
		private int DefaultBigTradeQtyForInstrument()
		{
			string n = null;

			try
			{
				if (Instrument != null && Instrument.MasterInstrument != null)
					n = Instrument.MasterInstrument.Name;
			}
			catch { n = null; }

			if (string.IsNullOrEmpty(n))
				return 50;

			switch (n.ToUpperInvariant())
			{
				case "ES":	return 50;
				case "MES":	return 100;
				case "NQ":	return 20;
				case "MNQ":	return 40;
				case "RTY":	return 20;
				case "M2K":	return 40;
				case "YM":	return 15;
				case "MYM":	return 30;
				case "CL":	return 25;
				case "MCL":	return 50;
				case "GC":	return 20;
				case "MGC":	return 40;
				case "ZN":	return 60;
				case "ZB":	return 40;
				case "ZF":	return 60;
				case "6E":	return 25;
				case "6B":	return 15;
				case "6J":	return 20;
			}

			return 30;
		}

		#endregion

		#region ===================== HORARIO =====================

		private DateTime ToNewYorkTime(DateTime t)
		{
			if (nyTz == null)
				nyTz = ResolveNewYorkTimeZone();

			try
			{
				if (t.Kind == DateTimeKind.Utc)
					return TimeZoneInfo.ConvertTimeFromUtc(t, nyTz);

				DateTime local = t.Kind == DateTimeKind.Local ? t : DateTime.SpecifyKind(t, DateTimeKind.Local);
				return TimeZoneInfo.ConvertTime(local, TimeZoneInfo.Local, nyTz);
			}
			catch { }

			return t;
		}

		private static TimeZoneInfo ResolveNewYorkTimeZone()
		{
			try { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
			catch { }

			try { return TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
			catch { }

			return TimeZoneInfo.Local;
		}

		/// La cinta se captura toda la sesion menos la pausa de 17:00 a 18:00 NY.
		private static bool IsInsideNyTradingSessionNy(DateTime ny)
		{
			TimeSpan h = ny.TimeOfDay;
			return h >= new TimeSpan(18, 0, 0) || h < new TimeSpan(17, 0, 0);
		}

		private static DateTime GetNyTradingSessionDateNy(DateTime ny)
		{
			return ny.TimeOfDay >= new TimeSpan(18, 0, 0) ? ny.Date.AddDays(1) : ny.Date;
		}

		#endregion

		#region ===================== CACHE DE BURBUJAS =====================

		private void InvalidateBubbleRenderCacheLocked()
		{
			bubbleRenderCacheDirty = true;
		}

		private IltDevBtBubble CreateBubbleItemFromTrade(IltDevBtTrade t, bool forzarBloque)
		{
			if (t == null)
				return null;

			IltDevBtBubble b = new IltDevBtBubble();

			b.Price			= t.RawPrice > 0 ? t.RawPrice : t.Price;
			b.Vol			= Math.Max(1L, t.Vol);
			b.MaxSingle		= Math.Max(t.MaxSingle, t.Vol);
			b.TradeCount	= Math.Max(1, t.TradeCount);
			b.IsBuy			= t.IsBuy;
			b.CreatedMs		= t.CreatedMs;
			b.LastMs		= t.LastMs;
			b.BarIdx		= t.BarIdx;
			b.Seq			= t.Seq;
			b.StampTicks	= t.StampTicks;
			b.IsBlock		= forzarBloque || t.IsBlock
								|| !(Math.Max(t.Vol, t.MaxSingle) < (long)UmbralMin);

			return b;
		}

		private void AddBubbleTradeToCacheLocked(IltDevBtTrade t, int minVol, int minBloque, int maxVol)
		{
			if (t == null)
				return;

			long pico = Math.Max(t.Vol, t.MaxSingle);

			// Filtro superior: las operaciones mas grandes que el maximo no se pintan.
			if (pico > (long)maxVol)
				return;

			// El umbral se evalua de nuevo aqui, no se hereda de t.IsBlock.
			// IsBlock se marco con el umbral que hubiera al capturar la operacion:
			// si se confia en el, subir el umbral no quita nunca una burbuja, solo
			// bajarlo anade. Asi el filtro funciona en los dos sentidos y al momento.
			bool esBloque = !(pico < (long)minBloque);

			if (!esBloque && pico < (long)minVol)
				return;

			IltDevBtBubble b = CreateBubbleItemFromTrade(t, esBloque);

			if (b == null)
				return;

			bubbleRenderCache.Add(b);

			if (b.Seq > 0)
				bubbleSeqCache.Add(b.Seq);

			if (b.Vol > bubbleCacheMaxVol)
				bubbleCacheMaxVol = b.Vol;
		}

		private void AddBubbleBucketToCacheLocked(List<IltDevBtTrade> bucket, int minVol, int minBloque, int maxVol)
		{
			if (bucket == null)
				return;

			for (int i = 0; i < bucket.Count; i++)
				AddBubbleTradeToCacheLocked(bucket[i], minVol, minBloque, maxVol);
		}

		/// Reconstruye la lista de burbujas visibles. Si solo han entrado
		/// operaciones nuevas desde la ultima vez, las agrega sin rehacer todo.
		private void EnsureBubbleRenderCacheLocked(int desde, int hasta)
		{
			int minVisual	= UmbralMin;
			int maxVisual	= UmbralMax;
			int nTrades		= trades    != null ? trades.Count    : 0;
			int nArchivo	= btArchive != null ? btArchive.Count : 0;

			bool sinCambios = !bubbleRenderCacheDirty
				&& bubbleCacheFrom			== desde
				&& bubbleCacheTo			== hasta
				&& bubbleCacheVisualMin		== minVisual
				&& bubbleCacheVisualMax		== maxVisual
				&& bubbleCacheSeq			== tradeSeq
				&& bubbleCacheTradeCount	== nTrades
				&& bubbleCacheBtCount		== nArchivo;

			if (sinCambios)
				return;

			bool soloNuevas = !bubbleRenderCacheDirty
				&& bubbleRenderCache		!= null
				&& bubbleCacheFrom			== desde
				&& bubbleCacheTo			== hasta
				&& bubbleCacheVisualMin		== minVisual
				&& bubbleCacheVisualMax		== maxVisual
				&& tradeSeq					>  bubbleCacheSeq
				&& !(nTrades < bubbleCacheTradeCount);

			if (soloNuevas)
			{
				long ultima = bubbleCacheSeq;

				for (int i = trades.Count - 1; i >= 0; i--)
				{
					IltDevBtTrade t = trades[i];

					if (t == null)
						continue;

					if (t.Seq <= ultima)
						break;

					if (t.BarIdx < desde || t.BarIdx > hasta)
						continue;

					if (t.Seq > 0 && bubbleSeqCache.Contains(t.Seq))
						continue;

					AddBubbleTradeToCacheLocked(t, minVisual, minVisual, maxVisual);
				}

				bubbleCacheSeq			= tradeSeq;
				bubbleCacheTradeCount	= nTrades;
				bubbleCacheBtCount		= nArchivo;
				bubbleRenderCacheDirty	= false;
				return;
			}

			if (bubbleRenderCache == null)	bubbleRenderCache = new List<IltDevBtBubble>(1024);
			else							bubbleRenderCache.Clear();

			if (bubbleSeqCache == null)		bubbleSeqCache = new HashSet<long>();
			else							bubbleSeqCache.Clear();

			bubbleCacheMaxVol = 1L;

			if (trades != null && trades.Count > 0)
			{
				if (tradesByBar != null && tradesByBar.Count > 0)
				{
					for (int b = desde; b <= hasta; b++)
					{
						List<IltDevBtTrade> bucket;

						if (tradesByBar.TryGetValue(b, out bucket) && bucket != null)
							AddBubbleBucketToCacheLocked(bucket, minVisual, minVisual, maxVisual);
					}
				}
				else
				{
					for (int i = 0; i < trades.Count; i++)
					{
						IltDevBtTrade t = trades[i];

						if (t == null || t.BarIdx < desde || t.BarIdx > hasta)
							continue;

						AddBubbleTradeToCacheLocked(t, minVisual, minVisual, maxVisual);
					}
				}
			}

			if (btArchiveByBar != null && btArchiveByBar.Count > 0)
			{
				for (int b = desde; b <= hasta; b++)
				{
					List<IltDevBtTrade> bucket;

					if (!btArchiveByBar.TryGetValue(b, out bucket) || bucket == null)
						continue;

					for (int j = 0; j < bucket.Count; j++)
						AgregarDelArchivoLocked(bucket[j], minVisual, maxVisual);
				}
			}
			else if (btArchive != null && btArchive.Count > 0)
			{
				for (int i = 0; i < btArchive.Count; i++)
				{
					IltDevBtTrade t = btArchive[i];

					if (t == null || t.BarIdx < desde || t.BarIdx > hasta)
						continue;

					AgregarDelArchivoLocked(t, minVisual, maxVisual);
				}
			}

			bubbleCacheFrom			= desde;
			bubbleCacheTo			= hasta;
			bubbleCacheVisualMin	= minVisual;
			bubbleCacheVisualMax	= maxVisual;
			bubbleCacheSeq			= tradeSeq;
			bubbleCacheTradeCount	= nTrades;
			bubbleCacheBtCount		= nArchivo;
			bubbleRenderCacheDirty	= false;
		}

		private void AgregarDelArchivoLocked(IltDevBtTrade t, int minBloque, int maxVol)
		{
			if (t == null)
				return;

			long pico = Math.Max(t.Vol, t.MaxSingle);

			if (pico < (long)minBloque || pico > (long)maxVol)
				return;

			if (t.Seq > 0 && bubbleSeqCache.Contains(t.Seq))
				return;

			IltDevBtBubble b = CreateBubbleItemFromTrade(t, true);

			if (b == null)
				return;

			bubbleRenderCache.Add(b);

			if (b.Seq > 0)
				bubbleSeqCache.Add(b.Seq);

			if (b.Vol > bubbleCacheMaxVol)
				bubbleCacheMaxVol = b.Vol;
		}

		#endregion

		#region ===================== REFRESCO =====================

		/// Hay animacion viva? Decide si el temporizador sigue pidiendo repintados.
		private bool HasActiveBubbleAnimation(int nowMs)
		{
			int anim = Math.Max(0, BubbleAnimationMs);

			int flash = (BigTradeFlash && VerBurbujas) ? Math.Max(0, BigTradeFlashMs) : 0;

			if (Math.Max(anim, flash) <= 0)
				return false;

			if (flash > 0 && lastLiveBtMs > -100000000)
			{
				int d = nowMs - lastLiveBtMs;

				if (d >= 0 && d <= flash)
					return true;
			}

			if (anim <= 0)
				return false;

			lock (lk)
			{
				if (trades == null || trades.Count == 0)
					return false;

				for (int i = trades.Count - 1; ; )
				{
					IltDevBtTrade t = trades[i];

					int last = t.LastMs > 0 ? t.LastMs : (t.Time > 0 ? (int)t.Time : 0);

					if (last > 0)
					{
						int d = nowMs - last;

						if (d >= 0 && d <= anim)
							return true;
					}

					if (i < trades.Count - 80)
						break;

					i--;

					if (i < 0)
						break;
				}
			}

			return false;
		}

		private void EnsureFlowTimer()
		{
			if (flowTimer != null)
				return;

			flowTimer = new DispatcherTimer();
			flowTimer.Tick += (o, e) => FlowTimerTick();
		}

		/// Se llama desde el hilo de datos en cada print, asi que sale por la puerta
		/// de atras si el temporizador ya esta corriendo: encolar un despacho por
		/// operacion satura la cola de la interfaz, y reasignar Interval con el
		/// temporizador en marcha empuja el vencimiento hacia delante y la animacion
		/// se congela justo cuando mas cinta hay.
		private void StartFlowTimerIfNeeded()
		{
			if (terminado || flowTimerActive || ChartControl == null || !VerBurbujas)
				return;

			try
			{
				ChartControl.Dispatcher.InvokeAsync(() =>
				{
					try
					{
						if (terminado)
							return;

						EnsureFlowTimer();

						if (flowTimer == null || flowTimer.IsEnabled)
							return;

						int fps = Math.Max(8, Math.Min(30, FlowRefreshFps));

						flowTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / fps);
						flowTimer.Start();
						flowTimerActive = true;
					}
					catch { }
				});
			}
			catch { }
		}

		private void StopFlowTimer()
		{
			try
			{
				if (flowTimer != null && flowTimer.IsEnabled)
				{
					flowTimer.Stop();
					flowTimerActive = false;
				}
			}
			catch { }
		}

		private void FlowTimerTick()
		{
			try
			{
				if (terminado)
				{
					StopFlowTimer();
					return;
				}

				int now = IltDevNowMs;

				if (!(VerBurbujas && HasActiveBubbleAnimation(now)))
				{
					StopFlowTimer();
					return;
				}

				RequestRefresh(false);
			}
			catch { StopFlowTimer(); }
		}

		/// nuevoBigTrade = true fuerza el repintado aunque el temporizador de
		/// animacion no este corriendo (animacion apagada). El limite de cuadros
		/// por segundo se respeta siempre, para no ahogar la interfaz.
		private void RequestRefresh(bool nuevoBigTrade)
		{
			if (!nuevoBigTrade && !flowTimerActive)
				return;

			int now		= IltDevNowMs;
			int fps		= Math.Max(8, Math.Min(30, FlowRefreshFps));
			int minGap	= Math.Max(15, 1000 / fps);

			if (now - lastRefreshMs < minGap)
				return;

			lastRefreshMs = now;
			ForceChartRefreshSafe();
		}

		private void ForceChartRefreshSafe()
		{
			try
			{
				if (ChartControl != null && ChartControl.Dispatcher != null && !ChartControl.Dispatcher.CheckAccess())
				{
					ChartControl.Dispatcher.InvokeAsync(() => ForceChartRefreshOnUi());
					return;
				}

				ForceChartRefreshOnUi();
			}
			catch { }
		}

		private void ForceChartRefreshOnUi()
		{
			try { ForceRefresh(); }
			catch { }
		}

		#endregion

		#region ===================== RECURSOS SHARPDX =====================

		private static float OpacityPct(int v)
		{
			return Math.Max(0, Math.Min(100, v)) / 100f;
		}

		private static string FQ(long v)
		{
			if (v >= 1000000L)	return (v / 1000000.0).ToString("0.0") + "M";
			if (v >= 1000L)		return (v / 1000.0).ToString("0.0") + "K";

			return v.ToString();
		}

		private static SharpDX.Color4 BrushToColor4(System.Windows.Media.Brush br, float r, float g, float b, float a)
		{
			System.Windows.Media.SolidColorBrush sb = br as System.Windows.Media.SolidColorBrush;

			if (sb == null)
				return new SharpDX.Color4(r, g, b, a);

			System.Windows.Media.Color c = sb.Color;

			return new SharpDX.Color4(c.R / 255f, c.G / 255f, c.B / 255f, a);
		}

		private static SharpDX.Direct2D1.SolidColorBrush SB(SharpDX.Direct2D1.RenderTarget rt,
			System.Windows.Media.Brush br, float r, float g, float b, float a)
		{
			return new SharpDX.Direct2D1.SolidColorBrush(rt, BrushToColor4(br, r, g, b, a));
		}

		private static SharpDX.DirectWrite.TextFormat MakeFont(SharpDX.DirectWrite.Factory dw, float size,
			SharpDX.DirectWrite.FontWeight peso)
		{
			for (int i = 0; i < fontFallback.Length; i++)
			{
				try
				{
					return new SharpDX.DirectWrite.TextFormat(dw, fontFallback[i], null, peso,
						SharpDX.DirectWrite.FontStyle.Normal, SharpDX.DirectWrite.FontStretch.Normal, size);
				}
				catch { }
			}

			return new SharpDX.DirectWrite.TextFormat(dw, "Arial", size);
		}

		private string VisualSettingsSignature()
		{
			return string.Concat(
				Serialize.BrushToString(BigTradeBuyColor), "|",
				Serialize.BrushToString(BigTradeSellColor), "|",
				Serialize.BrushToString(BubbleBorderColor), "|",
				Serialize.BrushToString(BubbleLightningColor), "|",
				Serialize.BrushToString(TextColor));
		}

		private void FreeDx()
		{
			try { if (bBubBuy   != null) bBubBuy.Dispose(); }   catch { }
			try { if (bBubSell  != null) bBubSell.Dispose(); }  catch { }
			try { if (bBubBord  != null) bBubBord.Dispose(); }  catch { }
			try { if (bBubFlash != null) bBubFlash.Dispose(); } catch { }
			try { if (bPanSh    != null) bPanSh.Dispose(); }    catch { }
			try { if (bTxt      != null) bTxt.Dispose(); }      catch { }
			try { if (fSm       != null) fSm.Dispose(); }       catch { }

			bBubBuy = bBubSell = bBubBord = bBubFlash = bPanSh = bTxt = null;
			fSm		= null;
			dxOk	= false;
		}

		private void EnsureDx(float dpi)
		{
			float fuente = Math.Max(7f, Math.Min(30f, FontSizeSmall));
			string firma = VisualSettingsSignature();

			bool cambioDpi		= Math.Abs(dpi - curDpi) > 0.04f;
			bool cambioFuente	= Math.Abs(fuente - lastFontSmall) > 0.01f;
			bool cambioColor	= lastVisualSig != firma;

			IntPtr rtRef = IntPtr.Zero;

			try { rtRef = RenderTarget != null ? RenderTarget.NativePointer : IntPtr.Zero; }
			catch { rtRef = IntPtr.Zero; }

			bool otroRt = rtRef != IntPtr.Zero && dxRtRef != rtRef;

			if (dxOk && otroRt)
				FreeDx();

			if (dxOk && !cambioDpi && !cambioFuente && !cambioColor)
				return;

			if (dxOk)
				FreeDx();

			if (RenderTarget == null)
				return;

			curDpi			= dpi;
			lastFontSmall	= fuente;
			lastVisualSig	= firma;
			dxRtRef			= rtRef;

			// Las brochas y la fuente van en dos intentos separados a proposito:
			// la fuente solo hace falta para el numero de dentro de la burbuja, y
			// si fallara no debe impedir que se dibuje absolutamente nada.
			try
			{
				SharpDX.Direct2D1.RenderTarget rt = RenderTarget;

				bBubBuy		= SB(rt, BigTradeBuyColor,		0.15f, 0.92f, 0.30f, 1f);
				bBubSell	= SB(rt, BigTradeSellColor,		0.95f, 0.10f, 0.10f, 1f);
				bBubBord	= SB(rt, BubbleBorderColor,		0.62f, 0.12f, 0.10f, 1f);
				bBubFlash	= SB(rt, BubbleLightningColor,	0.95f, 0.20f, 0.05f, 1f);
				bPanSh		= SB(rt, null,					0f,    0f,    0f,    1f);
				bTxt		= SB(rt, TextColor,				0.94f, 0.95f, 1.00f, 1f);

				dxOk = true;
			}
			catch
			{
				FreeDx();
				return;
			}

			try { fSm = MakeFont(Globals.DirectWriteFactory, Math.Max(7f, fuente * dpi), SharpDX.DirectWrite.FontWeight.Normal); }
			catch { fSm = null; }
		}

		/// Texto centrado dentro de un rectangulo.
		private void TC(string s, float x, float y, SharpDX.DirectWrite.TextFormat f,
			SharpDX.Direct2D1.SolidColorBrush b, float w, float h)
		{
			if (f == null || string.IsNullOrEmpty(s))
				return;

			SharpDX.DirectWrite.TextLayout tl = null;

			try
			{
				tl = new SharpDX.DirectWrite.TextLayout(Globals.DirectWriteFactory, s, f,
					Math.Max(1f, w), Math.Max(1f, h));

				tl.TextAlignment		= SharpDX.DirectWrite.TextAlignment.Center;
				tl.ParagraphAlignment	= SharpDX.DirectWrite.ParagraphAlignment.Center;
				tl.WordWrapping			= SharpDX.DirectWrite.WordWrapping.NoWrap;

				RenderTarget.DrawTextLayout(new SharpDX.Vector2(x, y), tl, b);
			}
			catch { }
			finally
			{
				if (tl != null)
					tl.Dispose();
			}
		}

		#endregion

		#region ===================== DIBUJO =====================

		public override void OnRenderTargetChanged()
		{
			// NinjaTrader ha tirado el render target: las brochas y la fuente que
			// colgaban de el ya no valen.
			FreeDx();
		}

		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			base.OnRender(chartControl, chartScale);

			if (terminado || !VerBurbujas || chartControl == null || chartScale == null
				|| RenderTarget == null || ChartPanel == null)
				return;

			float W = RenderTarget.Size.Width;
			float H = RenderTarget.Size.Height;

			float sx = W / Math.Max(1f, (float)chartControl.ActualWidth);
			float sy = H / Math.Max(1f, (float)chartControl.ActualHeight);

			EnsureDx(Math.Max(sx, sy));

			if (!dxOk)
				return;

			float anchoPanel = (float)ChartPanel.ActualWidth * sx;

			if (anchoPanel <= 0)
				anchoPanel = W;

			try { DrawBubbles(chartControl, chartScale, anchoPanel, sx, sy); }
			catch { }
		}

		/// Indice de la ultima vela cargada.
		/// Dentro de OnRender no se usa CurrentBar: fuera del recorrido de barras
		/// su valor no apunta necesariamente a la ultima vela.
		private long UltimaBarraGrafico()
		{
			try
			{
				if (BarsArray != null && BarsArray.Length > 0 && BarsArray[0] != null && BarsArray[0].Count > 0)
					return BarsArray[0].Count - 1;
			}
			catch { }

			return CurrentBar;
		}

		private void EnsureBubbleBarXCache(ChartControl cc, int b0, int ultimaBarra, int cuantas)
		{
			if (cc == null || cuantas <= 0)
				return;

			if (bubbleBarXCache == null || bubbleBarXCache.Length < cuantas)
				bubbleBarXCache = new float[Math.Max(cuantas, 256)];

			for (int i = 0; i < cuantas; i++)
				bubbleBarXCache[i] = (float)cc.GetXByBarIndex(ChartBars, b0 + i);

			if (bubbleBarTCache == null || bubbleBarTCache.Length < cuantas + 1)
			{
				bubbleBarTCache		= new long[Math.Max(cuantas + 1, 257)];
				bubbleBarTCacheB0	= int.MinValue;
			}

			long cola = UltimaBarraGrafico();

			bool hayQueRehacer = !(bubbleBarTCacheB0 == b0
				&& bubbleBarTCacheCount == cuantas
				&& bubbleBarTCacheTail  == cola);

			if (!hayQueRehacer)
				return;

			try
			{
				bubbleBarTCache[0] = b0 > 0 ? Times[0].GetValueAt(b0 - 1).Ticks : 0L;

				for (int i = 0; i < cuantas; i++)
					bubbleBarTCache[i + 1] = Times[0].GetValueAt(b0 + i).Ticks;

				// La vela en curso todavia no tiene hora de cierre: se estima con
				// la duracion media de las tres anteriores.
				int cb = (int)cola;

				if (cb >= b0 && cb <= ultimaBarra && cb >= 4)
				{
					int hueco = cb - b0;

					if (hueco >= 0 && hueco + 1 < bubbleBarTCache.Length)
					{
						long suma = 0L;
						int  n    = 0;

						for (int k = 1; k <= 3; k++)
						{
							long a = Times[0].GetValueAt(cb - k).Ticks;
							long b = Times[0].GetValueAt(cb - k - 1).Ticks;

							if (a > b)
							{
								suma += a - b;
								n++;
							}
						}

						long paso = n > 0 ? suma / n : 0L;

						if (paso < 10000000L)
							paso = 10000000L;

						long t = bubbleBarTCache[hueco];

						if (t > 0)
							bubbleBarTCache[hueco + 1] = t + paso;
					}
				}

				bubbleBarTCacheB0		= b0;
				bubbleBarTCacheCount	= cuantas;
				bubbleBarTCacheTail		= cola;
			}
			catch
			{
				for (int i = 0; i <= cuantas && i < bubbleBarTCache.Length; i++)
					bubbleBarTCache[i] = 0L;

				bubbleBarTCacheB0 = int.MinValue;
			}
		}

		/// Desplazamiento dentro de la vela segun el momento exacto del print.
		private float GetBubbleIntrabarOffset(IltDevBtBubble b, int hueco, float anchoVela)
		{
			if (b == null || !(anchoVela > 2.5f))
				return 0f;

			if (bubbleBarTCache == null || hueco < 0 || hueco + 1 >= bubbleBarTCache.Length)
				return 0f;

			if (b.StampTicks <= 0)
				return 0f;

			long t0 = bubbleBarTCache[hueco];
			long t1 = bubbleBarTCache[hueco + 1];

			if (t0 <= 0 || t1 <= t0)
				return 0f;

			double f = (double)(b.StampTicks - t0) / (double)(t1 - t0);

			if (f < 0.0)		f = 0.0;
			else if (f > 1.0)	f = 1.0;

			return (float)((f - 0.5) * anchoVela * 0.76);
		}

		private static int GetFastBubbleRenderBar(IltDevBtBubble b, int minBar, int maxBar)
		{
			if (b == null || b.BarIdx < 0)
				return -1;

			if (b.BarIdx < minBar || b.BarIdx > maxBar)
				return -1;

			return b.BarIdx;
		}

		private void DrawBubbles(ChartControl cc, ChartScale scale, float limiteDer, float sx, float sy)
		{
			if (ChartBars == null)
				return;

			int desdeVis = ChartBars.FromIndex;
			int hastaVis = ChartBars.ToIndex;

			if (hastaVis < desdeVis)
			{
				diagBurbujas = 0;
				return;
			}

			int desde = Math.Max(0, desdeVis - 6);
			int hasta = hastaVis + 6;

			long maxVol;

			lock (lk)
			{
				if (trades == null || trades.Count == 0)
				{
					diagBurbujas = 0;
					return;
				}

				EnsureBubbleRenderCacheLocked(desde, hasta);

				if (bubbleRenderCache == null || bubbleRenderCache.Count == 0)
				{
					diagBurbujas = 0;
					return;
				}

				// Copia para dibujar: la lista viva la puede vaciar el hilo de datos
				// al cambiar de sesion en mitad del fotograma.
				renderBuf.Clear();
				renderBuf.AddRange(bubbleRenderCache);

				maxVol = Math.Max(1L, bubbleCacheMaxVol);
			}

			List<IltDevBtBubble> lista = renderBuf;

			float	ancho		= Math.Max(sx, sy);
			float	op			= OpacityPct(BubbleOpacity);
			int		now			= IltDevNowMs;
			int		animMs		= Math.Max(0, BubbleAnimationMs);
			int		minVisual	= UmbralMin;
			long	maxVisual	= (long)UmbralMax;

			float	glow		= Math.Max(0f, Math.Min(1f,    BubbleGlowStrength  / 100f));
			float	pulso		= Math.Max(0f, Math.Min(1.25f, BubblePulseStrength / 100f));
			float	opBorde		= OpacityPct(BubbleBorderOpacity);
			float	opBrillo	= OpacityPct(BubbleHighlightOpacity);

			bool	flashOn		= BigTradeFlash && BubbleOpacity > 0;
			int		flashMs		= Math.Max(0, BigTradeFlashMs);
			float	flashFuerza	= Math.Max(0f, Math.Min(1f, BigTradeFlashStrength / 100f));

			float	osc1		= flashOn ? (0.5f + 0.5f * (float)Math.Sin(now * 0.0058)) : 0f;
			float	osc2		= flashOn ? (0.5f + 0.5f * (float)Math.Sin(now * 0.0135 + 1.1)) : 0f;

			int nBarras = Math.Max(1, hastaVis - desdeVis + 1);

			EnsureBubbleBarXCache(cc, desdeVis, hastaVis, nBarras);

			float anchoVela = 8f * sx;

			try
			{
				if (nBarras > 1 && bubbleBarXCache != null)
					anchoVela = Math.Abs(bubbleBarXCache[1] - bubbleBarXCache[0]);
			}
			catch { anchoVela = 8f * sx; }

			anchoVela = Math.Max(1f * sx, anchoVela);

			if (bubblePriceYCache == null)	bubblePriceYCache = new Dictionary<int, float>(512);
			else							bubblePriceYCache.Clear();

			int  n			= lista.Count;
			bool denso		= n > 900  || anchoVela < 7.0f * sx;
			bool muyDenso	= n > 2600 || anchoVela < 3.5f * sx;

			// --- tamano ---
			// esc multiplica todo. Los radios pueden fijarse a mano en pixeles;
			// en 0 los calcula el indicador segun lo apretadas que esten las velas.
			float esc = Math.Max(0.2f, Math.Min(4f, TamanoBurbujaPct / 100f));

			float rMin = muyDenso ?  2.3f * ancho : (denso ?  3.4f * ancho :  5.8f * ancho);
			float rMax = muyDenso ?  7.0f * ancho : (denso ? 10.2f * ancho : 15.2f * ancho);
			float tope = muyDenso ?  7.6f * ancho : (denso ? 10.8f * ancho : 16.2f * ancho);

			if (RadioMinimoPx > 0)	rMin = RadioMinimoPx * ancho;
			if (RadioMaximoPx > 0)	rMax = RadioMaximoPx * ancho;

			rMin *= esc;
			rMax *= esc;
			tope *= esc;

			if (rMax < rMin)	rMax = rMin;
			if (tope < rMax)	tope = rMax;

			int dibujadas = 0;

			for (int i = 0; i < n; i++)
			{
				IltDevBtBubble b = lista[i];

				if (b == null)
					continue;

				if (b.BarIdx < desdeVis - 2 || b.BarIdx > hastaVis + 2)
					continue;

				long picoB = Math.Max(b.Vol, b.MaxSingle);

				if (picoB > maxVisual)
					continue;

				// Igual que en la cache: el umbral manda sobre la marca guardada.
				bool esBig = !(picoB < (long)minVisual);

				if (!esBig)
					continue;

				int barra = GetFastBubbleRenderBar(b, desdeVis, hastaVis);

				if (barra < desdeVis || barra > hastaVis)
					continue;

				int hueco = barra - desdeVis;

				if (bubbleBarXCache == null || hueco < 0 || hueco >= bubbleBarXCache.Length)
					continue;

				float x		= bubbleBarXCache[hueco];
				float margen = rMax * 1.35f;

				if (x - margen > limiteDer)	continue;
				if (x + margen < 0f)		continue;

				int   clave = NormalizeToTickPriceKey(b.Price);
				float y;

				if (!bubblePriceYCache.TryGetValue(clave, out y))
				{
					y = (float)scale.GetYByValue(b.Price);
					bubblePriceYCache[clave] = y;
				}

				// Tamano por volumen, en escala logaritmica sobre el mayor visible.
				double lv	= Math.Log(b.Vol + 1);
				double lmax	= Math.Log(maxVol + 1);
				float  f	= (float)(lv / Math.Max(1.0, lmax));

				f = Math.Max(0f, Math.Min(1f, f));

				float t = 0.32f + 0.68f * (float)Math.Pow(f, 0.72);
				t = Math.Max(0.32f, Math.Min(1f, t));

				float r = rMin + (rMax - rMin) * t;

				r = Math.Min(r + 0.8f * ancho * esc, tope);

				x += GetBubbleIntrabarOffset(b, hueco, anchoVela);

				margen = Math.Max(margen, r * 1.6f);

				if (x - margen > limiteDer)	continue;
				if (x + margen < 0f)		continue;

				int   ultimo	= b.LastMs > 0 ? b.LastMs : 0;
				int   transcurr	= now - ultimo;
				float edad		= (animMs <= 0 || ultimo <= 0 || transcurr < 0)
									? 1f
									: Math.Max(0f, Math.Min(1f, (float)transcurr / (float)animMs));

				float inv		= 1f - edad;
				bool  animando	= inv > 0.04f;
				float pop		= animando ? (inv * inv * (2.3f - 1.3f * inv)) : 0f;
				float inv2		= inv * inv;

				float radio = r * (1f + pulso * 0.18f * pop);

				float flashF = 0f, flashGlow = 0f, flashEsc = 0f;

				if (flashOn && esBig && flashMs > 0 && ultimo > 0)
				{
					int d = now - ultimo;

					if (d >= 0 && d <= flashMs)
					{
						flashF		= 1f - (float)d / (float)flashMs;
						flashF		= flashF * flashF * (3f - 2f * flashF);
						flashGlow	= flashF * flashFuerza * (0.45f + 0.55f * osc1);
						flashEsc	= flashF * flashFuerza * 0.1f * (0.4f + 0.6f * osc1);
						radio	   *= (1f + flashEsc);
					}
				}

				SharpDX.Direct2D1.SolidColorBrush br = b.IsBuy ? bBubBuy : bBubSell;

				if (br == null)
					continue;

				SharpDX.Direct2D1.Ellipse elipse =
					new SharpDX.Direct2D1.Ellipse(new SharpDX.Vector2(x, y), radio, radio);

				// --- sombra proyectada ---
				if (bPanSh != null && !muyDenso)
				{
					bPanSh.Opacity = (esBig ? 0.2f : 0.1f) * op;

					RenderTarget.FillEllipse(new SharpDX.Direct2D1.Ellipse(
						new SharpDX.Vector2(x + radio * 0.1f, y + radio * 0.13f),
						radio * 0.88f, radio * 0.74f), bPanSh);
				}

				// --- halo ---
				if (!muyDenso && glow > 0.01f && (esBig || animando))
				{
					float rHalo = radio * (1.08f + 0.14f * inv2 + 0.10f * flashGlow);

					br.Opacity = (esBig ? 0.08f : 0.05f) * glow * op
							   + 0.08f * inv2 * op
							   + 0.14f * flashGlow * op;

					RenderTarget.FillEllipse(new SharpDX.Direct2D1.Ellipse(
						new SharpDX.Vector2(x, y), rHalo, rHalo), br);

					// --- onda expansiva ---
					if (BubbleShockwave && esBig && inv > 0.08f)
					{
						float rOnda = radio * (1.12f + 0.44f * edad);

						br.Opacity = 0.2f * inv * glow * op;

						RenderTarget.DrawEllipse(new SharpDX.Direct2D1.Ellipse(
							new SharpDX.Vector2(x, y), rOnda, rOnda), br,
							Math.Max(0.65f * sx, 0.8f));
					}
				}

				// --- cuerpo ---
				float opCuerpo = (esBig ? 0.64f : 0.36f) * op;
				opCuerpo = Math.Min(1f, opCuerpo + inv2 * 0.08f * op);

				br.Opacity = opCuerpo;
				RenderTarget.FillEllipse(elipse, br);

				// --- sombra interna ---
				if (bPanSh != null && !muyDenso)
				{
					bPanSh.Opacity = (esBig ? 0.13f : 0.07f) * op;

					RenderTarget.FillEllipse(new SharpDX.Direct2D1.Ellipse(
						new SharpDX.Vector2(x + radio * 0.1f, y + radio * 0.14f),
						radio * 0.64f, radio * 0.56f), bPanSh);
				}

				// --- nucleo ---
				float rNucleo = radio * 0.66f;

				br.Opacity = (esBig ? 0.2f : 0.12f) * op;

				RenderTarget.FillEllipse(new SharpDX.Direct2D1.Ellipse(
					new SharpDX.Vector2(x - radio * 0.02f, y - radio * 0.03f), rNucleo, rNucleo), br);

				// --- brillo ---
				if (!muyDenso && radio >= 7f * ancho * esc && bBubBord != null)
				{
					float rBrillo = radio * (esBig ? 0.27f : 0.22f);

					bBubBord.Opacity = (0.1f + 0.1f * inv2) * glow * op * opBrillo;

					RenderTarget.FillEllipse(new SharpDX.Direct2D1.Ellipse(
						new SharpDX.Vector2(x - radio * 0.28f, y - radio * 0.32f),
						rBrillo, rBrillo * 0.66f), bBubBord);

					if (esBig)
					{
						bBubBord.Opacity = (0.18f + 0.1f * inv2) * op * opBrillo;

						RenderTarget.DrawLine(
							new SharpDX.Vector2(x - radio * 0.42f, y - radio * 0.16f),
							new SharpDX.Vector2(x - radio * 0.08f, y - radio * 0.34f),
							bBubBord, Math.Max(0.65f * sx, 0.8f));

						if (flashGlow > 0.01f)
						{
							float rChispa = radio * (0.16f + 0.05f * osc2);

							bBubBord.Opacity = Math.Min(1f, (0.35f + 0.45f * osc2) * flashGlow * op);

							RenderTarget.FillEllipse(new SharpDX.Direct2D1.Ellipse(
								new SharpDX.Vector2(x - radio * 0.30f, y - radio * 0.34f),
								rChispa, rChispa * 0.82f), bBubBord);
						}
					}
				}

				// --- contorno ---
				if (bBubBord != null)
				{
					float opCont		= (esBig ? 0.76f : 0.38f) * op * opBorde;
					float grosorCont	= (esBig ? 1.45f : 0.72f) * sx;

					bBubBord.Opacity = Math.Min(1f, opCont + inv2 * 0.08f);

					RenderTarget.DrawEllipse(elipse, bBubBord, Math.Max(0.65f, grosorCont));
				}

				// --- destello del bigtrade ---
				if (!muyDenso && esBig && bBubFlash != null)
				{
					float aF = Math.Min(0.7f, (0.1f + 0.16f * inv + 0.4f * flashGlow) * op);

					bBubFlash.Opacity = aF;

					float rF = radio * (1.06f + 0.07f * flashGlow);

					RenderTarget.DrawEllipse(new SharpDX.Direct2D1.Ellipse(
						new SharpDX.Vector2(x, y), rF, rF), bBubFlash,
						Math.Max(0.8f * sx, 0.95f) * (1f + 0.7f * flashGlow));

					if (flashGlow > 0.02f)
					{
						float osc3 = 0.5f + 0.5f * (float)Math.Sin(now * 0.0058 - 0.6);
						float rF2  = radio * (1.1f + 0.55f * osc3);

						bBubFlash.Opacity = Math.Min(0.55f, 0.42f * flashGlow * (1f - osc3) * op);

						RenderTarget.DrawEllipse(new SharpDX.Direct2D1.Ellipse(
							new SharpDX.Vector2(x, y), rF2, rF2), bBubFlash,
							Math.Max(0.85f * sx, 1f));
					}
				}

				// --- rayo ---
				if (!denso && BubbleLightning && esBig
					&& radio >= 11f * ancho * esc && inv > 0.1f)
					DrawBubbleLightning(x, y, radio, sx, inv, op);

				// --- etiqueta con el volumen ---
				if (!muyDenso && radio >= 10.5f * ancho * esc && esBig)
				{
					string txt = FQ(b.Vol);

					float w = Math.Max(24f * sx, (txt.Length * 5.8f + 6f) * sx);
					float h = Math.Max(10f * sy, 12f * ancho);

					if (bPanSh != null)
					{
						bPanSh.Opacity = 0.16f * op;

						SharpDX.Direct2D1.RoundedRectangle rr = new SharpDX.Direct2D1.RoundedRectangle();

						rr.Rect		= new SharpDX.RectangleF(x - w * 0.5f, y - h * 0.5f, w, h);
						rr.RadiusX	= h * 0.42f;
						rr.RadiusY	= h * 0.42f;

						RenderTarget.FillRoundedRectangle(rr, bPanSh);
					}

					if (bTxt != null && fSm != null)
					{
						bTxt.Opacity = 0.96f;
						TC(txt, x - w * 0.5f, y - h * 0.5f, fSm, bTxt, w, h);
					}
				}

				dibujadas++;
			}

			diagBurbujas = dibujadas;
		}

		private void DrawBubbleLightning(float x, float y, float r, float sx, float inv, float op)
		{
			if (bBubFlash == null)
				return;

			bBubFlash.Opacity = Math.Min(0.62f, (0.3f + 0.22f * inv) * op);

			float grosor = Math.Max(0.75f * sx, r * 0.034f);

			SharpDX.Vector2 p1 = new SharpDX.Vector2(x - r * 0.36f, y + r * 0.20f);
			SharpDX.Vector2 p2 = new SharpDX.Vector2(x - r * 0.08f, y - r * 0.02f);
			SharpDX.Vector2 p3 = new SharpDX.Vector2(x + r * 0.04f, y + r * 0.02f);
			SharpDX.Vector2 p4 = new SharpDX.Vector2(x + r * 0.36f, y - r * 0.24f);

			RenderTarget.DrawLine(p1, p2, bBubFlash, grosor);
			RenderTarget.DrawLine(p2, p3, bBubFlash, grosor);
			RenderTarget.DrawLine(p3, p4, bBubFlash, grosor);

			if (bBubBord != null)
			{
				bBubBord.Opacity = 0.16f * op * OpacityPct(BubbleBorderOpacity);

				RenderTarget.DrawLine(
					new SharpDX.Vector2(p1.X, p1.Y - Math.Max(1f, sx)),
					new SharpDX.Vector2(p4.X, p4.Y - Math.Max(1f, sx)),
					bBubBord, Math.Max(0.4f * sx, 0.55f));
			}
		}

		#endregion

		#region ===================== BOTON TOOLBAR + PANEL WPF =====================

		private void CleanupChartUi(Chart win, NTMenuItem btn, Border panel)
		{
			cajaActiva		= null;
			aplicarActiva	= null;

			try
			{
				if (btn != null)
				{
					btn.Click -= ToolbarBtn_Click;

					if (win != null && win.MainMenu != null && win.MainMenu.Contains(btn))
						win.MainMenu.Remove(btn);
				}
			}
			catch { }

			try
			{
				if (panel != null && UserControlCollection.Contains(panel))
					UserControlCollection.Remove(panel);
			}
			catch { }
		}

		private void BuildToolbarButton()
		{
			if (toolbarBtnAdded || !ShowToolbarButton || ChartControl == null)
				return;

			try
			{
				chartWindow = Window.GetWindow(ChartControl.Parent) as Chart;

				if (chartWindow == null || chartWindow.MainMenu == null)
					return;

				RemoveOrphanButtonsThisChart();

				toolbarBtn				= new NTMenuItem();
				toolbarBtn.Header		= "ILT-DEV-BIGTRADES";
				toolbarBtn.Style		= Application.Current.TryFindResource("MainMenuItem") as Style;
				toolbarBtn.Foreground	= new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 90, 190, 255));
				toolbarBtn.ToolTip		= "Mostrar / ocultar panel ILT-DEV BIGTRADES";
				toolbarBtn.Tag			= ToolbarBtnTag;
				toolbarBtn.Click	   += ToolbarBtn_Click;

				chartWindow.MainMenu.Add(toolbarBtn);
				toolbarBtnAdded = true;
			}
			catch (Exception ex)
			{
				try { Print("BuildToolbarButton error: " + ex.Message); }
				catch { }
			}
		}

		private void RemoveOrphanButtonsThisChart()
		{
			try
			{
				if (chartWindow == null || chartWindow.MainMenu == null)
					return;

				List<System.Windows.UIElement> aBorrar = new List<System.Windows.UIElement>();

				foreach (System.Windows.UIElement el in chartWindow.MainMenu)
				{
					NTMenuItem mi = el as NTMenuItem;

					if (mi != null && mi.Tag is string && (string)mi.Tag == ToolbarBtnTag)
						aBorrar.Add(el);
				}

				foreach (System.Windows.UIElement el in aBorrar)
					chartWindow.MainMenu.Remove(el);
			}
			catch { }
		}

		private void ToolbarBtn_Click(object sender, RoutedEventArgs e)
		{
			if (!wpfPanelAdded || wpfOuterPanel == null)
			{
				panelUserHidden = false;
				EnsureWpfControlPanel();
				SaveRuntimePanelState();
				return;
			}

			panelUserHidden = !panelUserHidden;
			ApplyPanelVisibilityFromToolbar();
			SaveRuntimePanelState();
		}

		private void ApplyPanelVisibilityFromToolbar()
		{
			if (wpfOuterPanel == null)
				return;

			wpfOuterPanel.Visibility = panelUserHidden ? Visibility.Collapsed : Visibility.Visible;
		}

		private void EnsureWpfControlPanel()
		{
			if (wpfPanelAdded && wpfOuterPanel != null)
			{
				ApplyPanelVisibilityFromToolbar();
				return;
			}

			try
			{
				Border outer = new Border
				{
					CornerRadius		= new CornerRadius(4),
					BorderThickness		= new Thickness(1),
					BorderBrush			= new SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 44, 170, 215)),
					Background			= new SolidColorBrush(System.Windows.Media.Color.FromArgb(232, 13, 16, 22)),
					Padding				= new Thickness(0),
					HorizontalAlignment	= HorizontalAlignment.Left,
					VerticalAlignment	= VerticalAlignment.Top,
					Margin				= new Thickness(wpfPanelLeft, wpfPanelTop, 0, 0),
					Width				= 236
				};

				outer.Child = BuildPanelInner();

				UserControlCollection.Add(outer);

				wpfOuterPanel	= outer;
				wpfPanelAdded	= true;

				ApplyPanelVisibilityFromToolbar();
				RestoreWpfPanelPosition();

				// Tras el layout ya se conoce el alto real; solo se corrige la
				// posicion si el grafico quedo mas pequeno durante la recarga.
				if (ChartControl != null)
					ChartControl.Dispatcher.InvokeAsync(() => RestoreWpfPanelPosition());
			}
			catch (Exception ex)
			{
				try { Print("ILT-DEV-BIGTRADES panel add error: " + ex.Message); }
				catch { }

				wpfOuterPanel	= null;
				wpfPanelAdded	= false;
			}
		}

		private Grid BuildPanelInner()
		{
			Grid root = new Grid();

			double[] alturas = new double[] { -1, 6, -1, 3, -1, 3, -1, 6, -1, 4, -1, 6 };

			for (int i = 0; i < alturas.Length; i++)
			{
				RowDefinition rd = new RowDefinition();
				rd.Height = alturas[i] >= 0 ? new GridLength(alturas[i]) : GridLength.Auto;
				root.RowDefinitions.Add(rd);
			}

			Grid header = new Grid
			{
				Height		= 22,
				Background	= new SolidColorBrush(System.Windows.Media.Color.FromArgb(190, 20, 105, 145)),
				Cursor		= Cursors.SizeAll
			};

			header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

			TextBlock title = new TextBlock
			{
				Text				= "ILT-DEV-BIGTRADES",
				FontFamily			= new FontFamily("Consolas"),
				FontSize			= 11,
				FontWeight			= FontWeights.Bold,
				Foreground			= Brushes.White,
				VerticalAlignment	= VerticalAlignment.Center,
				Margin				= new Thickness(6, 2, 4, 2)
			};

			Grid.SetColumn(title, 0);
			header.Children.Add(title);

			wpfMinimizeButton = new Button
			{
				Content		= wpfPanelMinimized ? "\u25B6" : "\u25BC",
				Width		= 22,
				Height		= 18,
				Margin		= new Thickness(2),
				FontFamily	= new FontFamily("Consolas"),
				FontSize	= 10,
				FontWeight	= FontWeights.Bold,
				Foreground	= Brushes.White,
				Background	= new SolidColorBrush(System.Windows.Media.Color.FromArgb(190, 40, 48, 58)),
				BorderBrush	= new SolidColorBrush(System.Windows.Media.Color.FromArgb(230, 110, 180, 210)),
				BorderThickness = new Thickness(1),
				Padding		= new Thickness(0),
				Focusable	= false,
				Cursor		= Cursors.Hand
			};

			wpfMinimizeButton.Click += (o, e) =>
			{
				CerrarCajaActiva(false);
				wpfPanelMinimized = !wpfPanelMinimized;
				ApplyPanelMinimized();
				e.Handled = true;
			};

			Grid.SetColumn(wpfMinimizeButton, 1);
			header.Children.Add(wpfMinimizeButton);

			header.MouseLeftButtonDown	+= OnWpfPanelDragMouseDown;
			header.MouseMove			+= OnWpfPanelDragMouseMove;
			header.MouseLeftButtonUp	+= OnWpfPanelDragMouseUp;

			Grid.SetRow(header, 0);
			root.Children.Add(header);

			cajaMin		= CrearCajaNumero(v => BigTradeVisualMinQty = v);
			cajaMax		= CrearCajaNumero(v => BigTradeVisualMaxQty = v);
			cajaTamano	= CrearCajaNumero(v => TamanoBurbujaPct = v);

			filasPanel[0] = CrearFilaNumero("BT MIN",   cajaMin,    v => BigTradeVisualMinQty = v, () => UmbralMin);
			filasPanel[1] = CrearFilaNumero("BT MAX",   cajaMax,    v => BigTradeVisualMaxQty = v, () => BigTradeVisualMaxQty);
			filasPanel[2] = CrearFilaNumero("TAMANO %", cajaTamano, v => TamanoBurbujaPct = v,     () => TamanoBurbujaPct);

			Grid.SetRow(filasPanel[0], 2);	root.Children.Add(filasPanel[0]);
			Grid.SetRow(filasPanel[1], 4);	root.Children.Add(filasPanel[1]);
			Grid.SetRow(filasPanel[2], 6);	root.Children.Add(filasPanel[2]);

			btnAcumMin = CreatePanelButton("ACUM MIN");
			btnAcumMin.Click += (o, e) =>
			{
				CerrarCajaActiva(false);
				AcumularPrints = !AcumularPrints;
				TrasCambioDePanel();
				e.Handled = true;
			};

			Grid acumCont = new Grid();
			acumCont.Children.Add(btnAcumMin);
			filasPanel[3] = acumCont;

			Grid.SetRow(acumCont, 8);
			root.Children.Add(acumCont);

			btnBurbujas = CreatePanelButton("BURBUJAS");
			btnBurbujas.Click += (o, e) =>
			{
				CerrarCajaActiva(false);
				VerBurbujas = !VerBurbujas;
				UpdateWpfPanelState();
				RequestChartRefresh();
				e.Handled = true;
			};

			Grid contBtn = new Grid();
			contBtn.Children.Add(btnBurbujas);
			filasPanel[4] = contBtn;

			Grid.SetRow(contBtn, 10);
			root.Children.Add(contBtn);

			UpdateWpfPanelState();

			return root;
		}

		/// Fila del panel: etiqueta, boton menos, casilla y boton mas.
		/// Los botones existen porque en algunos equipos el grafico se queda con el
		/// teclado; con ellos siempre se puede ajustar sin escribir.
		private Grid CrearFilaNumero(string etiqueta, TextBox caja, Action<int> aplicar, Func<int> leer)
		{
			Grid fila = new Grid();

			fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
			fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
			fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });

			TextBlock lbl = new TextBlock
			{
				Text				= etiqueta,
				FontFamily			= new FontFamily("Consolas"),
				FontSize			= 11,
				FontWeight			= FontWeights.Bold,
				Foreground			= Brushes.White,
				VerticalAlignment	= VerticalAlignment.Center,
				Margin				= new Thickness(9, 0, 4, 0)
			};

			Grid.SetColumn(lbl, 0);
			fila.Children.Add(lbl);

			Button menos = CrearBotonPaso("-");
			menos.Click += (o, e) =>
			{
				CerrarCajaActiva(false);

				int v = leer();
				aplicar(Math.Max(0, v - PasoDe(Math.Max(1, v - 1))));
				TrasCambioDePanel();
				e.Handled = true;
			};

			Grid.SetColumn(menos, 1);
			fila.Children.Add(menos);

			Grid.SetColumn(caja, 2);
			fila.Children.Add(caja);

			Button mas = CrearBotonPaso("+");
			mas.Click += (o, e) =>
			{
				CerrarCajaActiva(false);

				int v = leer();
				aplicar(v + PasoDe(v));
				TrasCambioDePanel();
				e.Handled = true;
			};

			Grid.SetColumn(mas, 3);
			fila.Children.Add(mas);

			return fila;
		}

		/// Salto proporcional al tamano, para no tener que pulsar cien veces.
		private static int PasoDe(int v)
		{
			if (v < 20)		return 1;
			if (v < 100)	return 5;
			if (v < 500)	return 25;

			return 100;
		}

		private Button CrearBotonPaso(string texto)
		{
			return new Button
			{
				Content			= texto,
				FontFamily		= new FontFamily("Consolas"),
				FontSize		= 11,
				FontWeight		= FontWeights.Bold,
				Width			= 20,
				Height			= 20,
				Margin			= new Thickness(0, 0, 2, 0),
				Foreground		= new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 200, 200, 200)),
				Background		= new SolidColorBrush(System.Windows.Media.Color.FromArgb(217, 51, 51, 51)),
				BorderBrush		= new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 120, 120, 120)),
				BorderThickness	= new Thickness(1),
				Cursor			= Cursors.Hand,
				Focusable		= false
			};
		}

		/// Casilla numerica.
		///
		/// El grafico de NinjaTrader escucha el teclado para su buscador de
		/// instrumentos y se queda con las pulsaciones antes de que lleguen aqui:
		/// escribes 40 y se abre el buscador con "40" dentro. Depender del foco de
		/// WPF no basta, asi que la casilla no se edita sola: al pulsar sobre ella
		/// se engancha un manejador en la ventana del grafico que atiende las teclas
		/// y las marca como consumidas, de modo que NinjaTrader ya no las ve.
		private TextBox CrearCajaNumero(Action<int> aplicar)
		{
			TextBox caja = new TextBox
			{
				FontFamily		= new FontFamily("Consolas"),
				FontSize		= 11,
				FontWeight		= FontWeights.Bold,
				Height			= 20,
				Foreground		= Brushes.White,
				Background		= FondoCaja(false),
				BorderBrush		= new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 96, 96, 96)),
				BorderThickness	= new Thickness(1),
				Padding			= new Thickness(3, 0, 3, 0),
				Margin			= new Thickness(0, 0, 2, 0),
				TextAlignment	= System.Windows.TextAlignment.Right,
				Focusable		= true,
				CaretBrush		= Brushes.White
			};

			caja.PreviewMouseLeftButtonDown += (o, e) =>
			{
				// El clic se queda aqui: si llega al grafico, el grafico recupera
				// el teclado y volvemos al problema.
				e.Handled = true;
				ActivarCaja(caja, aplicar);
			};

			// Por si en algun equipo si llegan las teclas a la casilla: que no suban.
			caja.KeyDown	+= (o, e) => { e.Handled = true; };
			caja.KeyUp		+= (o, e) => { e.Handled = true; };
			caja.TextInput	+= (o, e) => { e.Handled = true; };

			return caja;
		}

		private static SolidColorBrush FondoCaja(bool activa)
		{
			return activa
				? new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 20, 52, 78))
				: new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 26, 26, 26));
		}

		private void ActivarCaja(TextBox caja, Action<int> aplicar)
		{
			CerrarCajaActiva(true);

			cajaActiva		= caja;
			aplicarActiva	= aplicar;
			bufferActivo	= string.Empty;

			try
			{
				caja.Background	= FondoCaja(true);
				caja.Text		= string.Empty;
				caja.Focus();
			}
			catch { }

			EngancharTeclado();
		}

		/// Cierra la edicion en curso. aplicar=false descarta lo escrito.
		private void CerrarCajaActiva(bool aplicar)
		{
			TextBox		caja	= cajaActiva;
			Action<int>	accion	= aplicarActiva;
			string		buf		= bufferActivo;

			cajaActiva		= null;
			aplicarActiva	= null;
			bufferActivo	= string.Empty;

			if (caja == null)
				return;

			try { caja.Background = FondoCaja(false); }
			catch { }

			int v;

			if (aplicar && accion != null && buf.Length > 0 && int.TryParse(buf, out v))
			{
				accion(v);
				TrasCambioDePanel();
				return;
			}

			UpdateWpfPanelState();
		}

		/// Refresca el panel y repinta tras cambiar un valor.
		private void TrasCambioDePanel()
		{
			lock (lk)
				InvalidateBubbleRenderCacheLocked();

			// Subir un umbral se ve al instante: es solo filtrar lo que ya hay.
			// Bajar el suelo de captura no: lo que no se guardo en su momento no
			// esta, y hace falta recargar para reprocesar el historico.
			int suelo = GetTapeCaptureFloor();

			diagUltimoAviso = suelo < sueloAlCargar
				? "suelo de captura ahora " + suelo + ", se cargo con " + sueloAlCargar + ". Recarga (F5) para reprocesar el historico"
				: string.Empty;

			UpdateWpfPanelState();
			RequestChartRefresh();
		}

		private void EngancharTeclado()
		{
			if (tecladoEnganchado)
				return;

			try
			{
				if (chartWindow != null)
				{
					chartWindow.PreviewKeyDown += OnTeclaPanel;
					ventanaTeclado		= chartWindow;
					tecladoEnganchado	= true;
				}
				else if (ChartControl != null)
				{
					ChartControl.PreviewKeyDown += OnTeclaPanel;
					controlTeclado		= ChartControl;
					tecladoEnganchado	= true;
				}
			}
			catch { }
		}

		private void DesengancharTeclado()
		{
			if (!tecladoEnganchado)
				return;

			try { if (ventanaTeclado != null) ventanaTeclado.PreviewKeyDown -= OnTeclaPanel; }
			catch { }

			try { if (controlTeclado != null) controlTeclado.PreviewKeyDown -= OnTeclaPanel; }
			catch { }

			ventanaTeclado		= null;
			controlTeclado		= null;
			tecladoEnganchado	= false;
		}

		private void OnTeclaPanel(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (cajaActiva == null)
				return;

			int digito = -1;

			if (e.Key >= Key.D0 && e.Key <= Key.D9)
				digito = (int)e.Key - (int)Key.D0;
			else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
				digito = (int)e.Key - (int)Key.NumPad0;

			if (digito >= 0)
			{
				if (bufferActivo.Length < 6)
					bufferActivo += (char)('0' + digito);

				try { cajaActiva.Text = bufferActivo; }
				catch { }

				e.Handled = true;
				return;
			}

			if (e.Key == Key.Back)
			{
				if (bufferActivo.Length > 0)
					bufferActivo = bufferActivo.Substring(0, bufferActivo.Length - 1);

				try { cajaActiva.Text = bufferActivo; }
				catch { }

				e.Handled = true;
				return;
			}

			if (e.Key == Key.Enter || e.Key == Key.Tab)
			{
				CerrarCajaActiva(true);
				e.Handled = true;
				return;
			}

			if (e.Key == Key.Escape)
			{
				CerrarCajaActiva(false);
				e.Handled = true;
			}

			// Cualquier otra tecla sigue su camino: no se toca nada del grafico.
		}

		private void ApplyPanelMinimized()
		{
			if (wpfMinimizeButton != null)
				wpfMinimizeButton.Content = wpfPanelMinimized ? "\u25B6" : "\u25BC";

			Visibility v = wpfPanelMinimized ? Visibility.Collapsed : Visibility.Visible;

			for (int i = 0; i < NumFilasPanel; i++)
				if (filasPanel[i] != null)
					filasPanel[i].Visibility = v;
		}

		private Button CreatePanelButton(string texto)
		{
			return new Button
			{
				Content			= texto,
				FontFamily		= new FontFamily("Consolas"),
				FontSize		= 11,
				FontWeight		= FontWeights.Bold,
				Height			= 24,
				Margin			= new Thickness(9, 0, 9, 0),
				Padding			= new Thickness(4, 2, 4, 2),
				Foreground		= Brushes.White,
				BorderBrush		= new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 120, 120, 120)),
				BorderThickness	= new Thickness(1),
				Cursor			= Cursors.Hand,
				Focusable		= false
			};
		}

		/// Refleja en el panel los valores que hay en vigor. BT MIN muestra el
		/// umbral real: si la propiedad esta en 0, el que corresponde al instrumento.
		private void UpdateWpfPanelState()
		{
			if (cajaMin != null && cajaMin != cajaActiva)
				cajaMin.Text = UmbralMin.ToString();

			if (cajaMax != null && cajaMax != cajaActiva)
				cajaMax.Text = BigTradeVisualMaxQty > 0 ? BigTradeVisualMaxQty.ToString() : "0";

			if (cajaTamano != null && cajaTamano != cajaActiva)
				cajaTamano.Text = TamanoBurbujaPct.ToString();

			StylePanelButton(btnAcumMin, AcumularPrints);
			StylePanelButton(btnBurbujas, VerBurbujas);
		}

		private void StylePanelButton(Button b, bool activo)
		{
			if (b == null)
				return;

			b.Background = activo
				? new SolidColorBrush(System.Windows.Media.Color.FromArgb(242, 51, 140, 217))
				: new SolidColorBrush(System.Windows.Media.Color.FromArgb(217, 51, 51, 51));

			b.Foreground = activo
				? Brushes.White
				: new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 200, 200, 200));

			b.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 120, 120, 120));
		}

		private string RuntimePanelStateKey()
		{
			string instrumento = string.Empty;

			try
			{
				if (Instrument != null)
					instrumento = Instrument.FullName ?? string.Empty;
			}
			catch { }

			int chartId = ChartControl == null ? 0 : ChartControl.GetHashCode();
			return ToolbarBtnTag + "_STATE_" + chartId.ToString(CultureInfo.InvariantCulture) + "_" + instrumento;
		}

		private void SaveRuntimePanelState()
		{
			try
			{
				if (Application.Current == null || ChartControl == null)
					return;

				bool abierto = wpfPanelAdded && wpfOuterPanel != null && !panelUserHidden;
				string valor = (abierto ? "1" : "0") + "|"
					+ wpfPanelLeft.ToString("R", CultureInfo.InvariantCulture) + "|"
					+ wpfPanelTop.ToString("R", CultureInfo.InvariantCulture);

				Application.Current.Properties[RuntimePanelStateKey()] = valor;
			}
			catch { }
		}

		private void LoadRuntimePanelState()
		{
			try
			{
				if (Application.Current == null || ChartControl == null)
					return;

				string texto = Application.Current.Properties[RuntimePanelStateKey()] as string;

				if (string.IsNullOrEmpty(texto))
					return;

				string[] partes = texto.Split('|');
				double x;
				double y;

				if (partes.Length != 3
					|| !double.TryParse(partes[1], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
					|| !double.TryParse(partes[2], NumberStyles.Float, CultureInfo.InvariantCulture, out y))
					return;

				restorePanelOpen = partes[0] == "1";
				panelUserHidden = !restorePanelOpen;
				wpfPanelLeft = Math.Max(0, x);
				wpfPanelTop = Math.Max(0, y);
			}
			catch { }
		}

		private void RestoreWpfPanelPosition()
		{
			if (wpfOuterPanel == null || ChartControl == null)
				return;

			double ancho = wpfOuterPanel.ActualWidth > 0 ? wpfOuterPanel.ActualWidth : wpfOuterPanel.Width;
			double alto = wpfOuterPanel.ActualHeight;
			double maxLeft = Math.Max(0, ChartControl.ActualWidth - ancho - 2);
			double maxTop = alto > 0
				? Math.Max(0, ChartControl.ActualHeight - alto - 2)
				: Math.Max(0, ChartControl.ActualHeight - 2);

			wpfPanelLeft = Math.Max(0, Math.Min(maxLeft, wpfPanelLeft));
			wpfPanelTop = Math.Max(0, Math.Min(maxTop, wpfPanelTop));
			wpfOuterPanel.Margin = new Thickness(wpfPanelLeft, wpfPanelTop, 0, 0);
		}

		private void OnWpfPanelDragMouseDown(object sender, MouseButtonEventArgs e)
		{
			if (wpfOuterPanel == null || ChartControl == null)
				return;

			CerrarCajaActiva(false);

			wpfPanelDragging	= true;
			wpfDragStartPoint	= e.GetPosition(ChartControl);
			wpfDragStartLeft	= wpfOuterPanel.Margin.Left;
			wpfDragStartTop		= wpfOuterPanel.Margin.Top;

			try
			{
				System.Windows.UIElement el = sender as System.Windows.UIElement;
				if (el != null) el.CaptureMouse();
			}
			catch { }

			e.Handled = true;
		}

		private void OnWpfPanelDragMouseMove(object sender, MouseEventArgs e)
		{
			if (!wpfPanelDragging || wpfOuterPanel == null || ChartControl == null)
				return;

			System.Windows.Point p = e.GetPosition(ChartControl);

			double maxLeft	= Math.Max(0, ChartControl.ActualWidth  - wpfOuterPanel.ActualWidth  - 2);
			double maxTop	= Math.Max(0, ChartControl.ActualHeight - wpfOuterPanel.ActualHeight - 2);

			double newLeft	= wpfDragStartLeft + (p.X - wpfDragStartPoint.X);
			double newTop	= wpfDragStartTop  + (p.Y - wpfDragStartPoint.Y);

			if (newLeft < 0) newLeft = 0; else if (newLeft > maxLeft) newLeft = maxLeft;
			if (newTop  < 0) newTop  = 0; else if (newTop  > maxTop)  newTop  = maxTop;

			wpfPanelLeft = newLeft;
			wpfPanelTop = newTop;
			wpfOuterPanel.Margin = new Thickness(newLeft, newTop, 0, 0);

			e.Handled = true;
		}

		private void OnWpfPanelDragMouseUp(object sender, MouseButtonEventArgs e)
		{
			if (!wpfPanelDragging)
				return;

			wpfPanelDragging = false;

			try
			{
				System.Windows.UIElement el = sender as System.Windows.UIElement;
				if (el != null) el.ReleaseMouseCapture();
			}
			catch { }

			SaveRuntimePanelState();
			e.Handled = true;
		}

		/// Repintado inmediato tras tocar el panel.
		///
		/// InvalidateVisual() no basta: el grafico de NinjaTrader no repinta los
		/// indicadores por esa via, asi que el cambio no se veia hasta que llegaba
		/// un tick o se recargaba con F5. Hay que pasar por ForceRefresh(), que es
		/// lo mismo que usa el temporizador de animacion.
		private void RequestChartRefresh()
		{
			ForceChartRefreshSafe();

			try
			{
				if (ChartControl != null)
					ChartControl.InvalidateVisual();
			}
			catch { }
		}

		#endregion

		#region ===================== PROPIEDADES =====================

		// ---------- 01. General ----------

		[Display(Name = "Ver burbujas", GroupName = "01. General", Order = 1)]
		public bool VerBurbujas { get; set; }

		[Display(Name = "Boton en toolbar superior", GroupName = "01. General", Order = 2)]
		public bool ShowToolbarButton { get; set; }

		[Display(Name = "Cuadro de diagnostico", Description = "Muestra arriba a la izquierda cuantos prints llegan, cuantos pasan el umbral y cuantas burbujas hay en pantalla. Util para saber por que no aparece nada.", GroupName = "01. General", Order = 3)]
		public bool Diagnostico { get; set; }

		private int pFlowFps = 14;
		[Range(8, 30)]
		[Display(Name = "Refresco de animacion (fps)", Description = "Cuadros por segundo del repintado mientras hay animacion viva.", GroupName = "01. General", Order = 3)]
		public int FlowRefreshFps
		{
			get { return pFlowFps; }
			set { pFlowFps = Math.Max(8, Math.Min(30, value)); }
		}

		[Display(Name = "Acum Min (acumular prints)", Description = "Encendido: se guarda todo y los prints del mismo precio y lado se suman dentro de 280 ms, asi que un bigtrade puede salir de una rafaga de operaciones pequenas. Es lo que hace que en NQ aparezcan burbujas. Apagado: solo cuenta el print grande de una sola pieza.", GroupName = "02. Umbral", Order = 3)]
		public bool AcumularPrints { get; set; }

		// ---------- 02. Umbral ----------

		private int pBtVisualMin;
		[Range(0, 100000)]
		[Display(Name = "BT minimo (0 = automatico)", Description = "Tamano minimo para que una operacion se pinte como burbuja. En 0 usa el cuadro por instrumento (ES 50, NQ 20, CL 25...). Cualquier otro numero manda sobre el automatico y no lo pisa nada.", GroupName = "02. Umbral", Order = 1)]
		public int BigTradeVisualMinQty
		{
			get { return pBtVisualMin; }
			set { pBtVisualMin = Math.Max(0, Math.Min(100000, value)); }
		}

		private int pBtVisualMax;
		[Range(0, 100000)]
		[Display(Name = "BT maximo (0 = sin tope)", Description = "Por encima de esta cantidad la operacion deja de pintarse. Sirve para aislar un rango de tamanos. En 0 no hay tope.", GroupName = "02. Umbral", Order = 2)]
		public int BigTradeVisualMaxQty
		{
			get { return pBtVisualMax; }
			set { pBtVisualMax = Math.Max(0, Math.Min(100000, value)); }
		}

		// ---------- 03. Tamano ----------

		private int pTamanoPct = 100;
		[Range(20, 400)]
		[Display(Name = "Tamano de burbuja (%)", Description = "Escala de todas las burbujas. 100 es el tamano original; 50 las deja a la mitad y 200 al doble. Tambien se cambia desde el panel.", GroupName = "03. Tamano", Order = 1)]
		public int TamanoBurbujaPct
		{
			get { return pTamanoPct; }
			set { pTamanoPct = Math.Max(20, Math.Min(400, value)); }
		}

		private int pRadioMin;
		[Range(0, 200)]
		[Display(Name = "Radio minimo (px, 0 = automatico)", Description = "Extremo inferior del rango de tamanos. Las burbujas chicas quedan algo por encima de este valor: el reparto arranca a un tercio del rango para que una operacion pequena no salga invisible. En 0 lo calcula el indicador segun lo juntas que esten las velas.", GroupName = "03. Tamano", Order = 2)]
		public int RadioMinimoPx
		{
			get { return pRadioMin; }
			set { pRadioMin = Math.Max(0, Math.Min(200, value)); }
		}

		private int pRadioMax;
		[Range(0, 200)]
		[Display(Name = "Radio maximo (px, 0 = automatico)", Description = "Tamano de la burbuja mas grande. En 0 lo calcula el indicador. Si queda por debajo del minimo, se iguala al minimo.", GroupName = "03. Tamano", Order = 3)]
		public int RadioMaximoPx
		{
			get { return pRadioMax; }
			set { pRadioMax = Math.Max(0, Math.Min(200, value)); }
		}

		// ---------- 04. Colores ----------

		[XmlIgnore]
		[Display(Name = "Compra", GroupName = "04. Colores", Order = 1)]
		public Brush BigTradeBuyColor { get; set; }

		[Browsable(false)]
		public string BigTradeBuyColorSerialize
		{
			get { return Serialize.BrushToString(BigTradeBuyColor); }
			set { BigTradeBuyColor = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Venta", GroupName = "04. Colores", Order = 2)]
		public Brush BigTradeSellColor { get; set; }

		[Browsable(false)]
		public string BigTradeSellColorSerialize
		{
			get { return Serialize.BrushToString(BigTradeSellColor); }
			set { BigTradeSellColor = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Contorno", GroupName = "04. Colores", Order = 3)]
		public Brush BubbleBorderColor { get; set; }

		[Browsable(false)]
		public string BubbleBorderColorSerialize
		{
			get { return Serialize.BrushToString(BubbleBorderColor); }
			set { BubbleBorderColor = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Rayo / destello", GroupName = "04. Colores", Order = 4)]
		public Brush BubbleLightningColor { get; set; }

		[Browsable(false)]
		public string BubbleLightningColorSerialize
		{
			get { return Serialize.BrushToString(BubbleLightningColor); }
			set { BubbleLightningColor = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Texto", GroupName = "04. Colores", Order = 5)]
		public Brush TextColor { get; set; }

		[Browsable(false)]
		public string TextColorSerialize
		{
			get { return Serialize.BrushToString(TextColor); }
			set { TextColor = Serialize.StringToBrush(value); }
		}

		// ---------- 05. Opacidad ----------

		private int pBubOpacity = 78;
		[Range(0, 100)]
		[Display(Name = "Burbuja %", GroupName = "05. Opacidad", Order = 1)]
		public int BubbleOpacity
		{
			get { return pBubOpacity; }
			set { pBubOpacity = Math.Max(0, Math.Min(100, value)); }
		}

		private int pBubBordOpacity = 58;
		[Range(0, 100)]
		[Display(Name = "Contorno %", GroupName = "05. Opacidad", Order = 2)]
		public int BubbleBorderOpacity
		{
			get { return pBubBordOpacity; }
			set { pBubBordOpacity = Math.Max(0, Math.Min(100, value)); }
		}

		private int pBubHiOpacity = 38;
		[Range(0, 100)]
		[Display(Name = "Brillo %", GroupName = "05. Opacidad", Order = 3)]
		public int BubbleHighlightOpacity
		{
			get { return pBubHiOpacity; }
			set { pBubHiOpacity = Math.Max(0, Math.Min(100, value)); }
		}

		// ---------- 06. Animacion ----------

		private int pBubAnimMs = 850;
		[Range(0, 5000)]
		[Display(Name = "Duracion entrada (ms)", Description = "0 deja las burbujas quietas.", GroupName = "06. Animacion", Order = 1)]
		public int BubbleAnimationMs
		{
			get { return pBubAnimMs; }
			set { pBubAnimMs = Math.Max(0, Math.Min(5000, value)); }
		}

		private int pBubGlow = 46;
		[Range(0, 100)]
		[Display(Name = "Halo", GroupName = "06. Animacion", Order = 2)]
		public int BubbleGlowStrength
		{
			get { return pBubGlow; }
			set { pBubGlow = Math.Max(0, Math.Min(100, value)); }
		}

		private int pBubPulse = 12;
		[Range(0, 100)]
		[Display(Name = "Pulso", GroupName = "06. Animacion", Order = 3)]
		public int BubblePulseStrength
		{
			get { return pBubPulse; }
			set { pBubPulse = Math.Max(0, Math.Min(100, value)); }
		}

		[Display(Name = "Onda expansiva", GroupName = "06. Animacion", Order = 4)]
		public bool BubbleShockwave { get; set; }

		[Display(Name = "Rayo", GroupName = "06. Animacion", Order = 5)]
		public bool BubbleLightning { get; set; }

		[Display(Name = "Destello del bigtrade", GroupName = "06. Animacion", Order = 6)]
		public bool BigTradeFlash { get; set; }

		private int pBtFlashMs = 6000;
		[Range(800, 15000)]
		[Display(Name = "Duracion destello (ms)", GroupName = "06. Animacion", Order = 7)]
		public int BigTradeFlashMs
		{
			get { return pBtFlashMs; }
			set { pBtFlashMs = Math.Max(800, Math.Min(15000, value)); }
		}

		private int pBtFlashStrength = 70;
		[Range(0, 100)]
		[Display(Name = "Fuerza destello", GroupName = "06. Animacion", Order = 8)]
		public int BigTradeFlashStrength
		{
			get { return pBtFlashStrength; }
			set { pBtFlashStrength = Math.Max(0, Math.Min(100, value)); }
		}

		// ---------- 07. Texto ----------

		private int pFontSizeSmall = 11;
		[Range(7, 30)]
		[Display(Name = "Tamano del volumen", Description = "Tamano del numero que aparece dentro de las burbujas grandes.", GroupName = "07. Texto", Order = 1)]
		public int FontSizeSmall
		{
			get { return pFontSizeSmall; }
			set { pFontSizeSmall = Math.Max(7, Math.Min(30, value)); }
		}

		#endregion
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private ILT_DEV_SUITE.ILT_DEV_BIGTRADES[] cacheILT_DEV_BIGTRADES;
		public ILT_DEV_SUITE.ILT_DEV_BIGTRADES ILT_DEV_BIGTRADES()
		{
			return ILT_DEV_BIGTRADES(Input);
		}

		public ILT_DEV_SUITE.ILT_DEV_BIGTRADES ILT_DEV_BIGTRADES(ISeries<double> input)
		{
			if (cacheILT_DEV_BIGTRADES != null)
				for (int idx = 0; idx < cacheILT_DEV_BIGTRADES.Length; idx++)
					if (cacheILT_DEV_BIGTRADES[idx] != null &&  cacheILT_DEV_BIGTRADES[idx].EqualsInput(input))
						return cacheILT_DEV_BIGTRADES[idx];
			return CacheIndicator<ILT_DEV_SUITE.ILT_DEV_BIGTRADES>(new ILT_DEV_SUITE.ILT_DEV_BIGTRADES(), input, ref cacheILT_DEV_BIGTRADES);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.ILT_DEV_SUITE.ILT_DEV_BIGTRADES ILT_DEV_BIGTRADES()
		{
			return indicator.ILT_DEV_BIGTRADES(Input);
		}

		public Indicators.ILT_DEV_SUITE.ILT_DEV_BIGTRADES ILT_DEV_BIGTRADES(ISeries<double> input )
		{
			return indicator.ILT_DEV_BIGTRADES(input);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.ILT_DEV_SUITE.ILT_DEV_BIGTRADES ILT_DEV_BIGTRADES()
		{
			return indicator.ILT_DEV_BIGTRADES(Input);
		}

		public Indicators.ILT_DEV_SUITE.ILT_DEV_BIGTRADES ILT_DEV_BIGTRADES(ISeries<double> input )
		{
			return indicator.ILT_DEV_BIGTRADES(input);
		}
	}
}

#endregion

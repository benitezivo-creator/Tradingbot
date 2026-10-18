#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
//  ILT-DEV SUITE - ILT-DEV VWAP  (VERSION LIBERADA / SIN LICENCIA)
//
//  Mismo motor de calculo y render que ILT_DEV_VWAPS.dll, sin la validacion de
//  licencia: funciona en cualquier equipo.
//
//  Se llama ILT-DEV VWAP (sin la S) y todos sus tipos internos llevan nombre
//  propio, asi que convive en el mismo NinjaTrader con ILT_DEV_VWAPS.dll sin
//  chocar. En la lista de indicadores aparecen los dos por separado.
// ---------------------------------------------------------------------------

public class IltDevVwapPoint
{
	public DateTime	Time;
	public double	V0;
	public double	V1;
	public double	V2;
	public double	V3;
	public double	V4;
	public double	V5;
	public double	V6;
	public bool		BandsReady;
	public bool		IsAnchor;
}

public class IltDevVwapSegment
{
	public DateTime					Key;
	public DateTime					StartTime;
	public DateTime					EndTime;
	public bool						IsClosed;
	public List<IltDevVwapPoint>	Points = new List<IltDevVwapPoint>();
}

namespace NinjaTrader.NinjaScript.Indicators.ILT_DEV_SUITE
{
	public class ILT_DEV_VWAP : Indicator
	{
		#region ===================== CAMPOS =====================

		private sealed class WpfPanelReloadState
		{
			public double Left = 14;
			public double Top = 14;
			public bool HasPosition;
			public bool IsOpen;
		}

		private static readonly ConditionalWeakTable<ChartControl, WpfPanelReloadState> wpfPanelReloadStates
			= new ConditionalWeakTable<ChartControl, WpfPanelReloadState>();

		private const int	DailySessionsPerWeek		= 5;
		private const int	MaxVisibleRenderPoints		= 520;
		private const int	RenderInvalidateThrottleMs	= 350;
		private const float	LabelPixelOffset			= 6f;
		private const float	LabelTextWidth				= 82f;
		private const float	LabelTextHeight				= 18f;
		private const string ToolbarBtnTag				= "ILT_DEV_VWAP_BTN";

		private double		weeklyCumVolume;
		private double		weeklyCumPV;
		private double		weeklyCumP2V;
		private double		fullCumVolume;
		private double		fullCumPV;
		private double		fullCumP2V;
		private double		rthCumVolume;
		private double		rthCumPV;
		private double		rthCumP2V;

		private DateTime	currentWeeklyKey;
		private DateTime	currentFullSessionKey;
		private DateTime	currentRthSessionKey;

		private int			lastTickBarAccum		= -1;
		private DateTime	lastDxInvalidateUtc		= DateTime.MinValue;

		private List<IltDevVwapSegment>	weeklyRenderSegments	= new List<IltDevVwapSegment>();
		private List<IltDevVwapSegment>	fullRenderSegments		= new List<IltDevVwapSegment>();
		private List<IltDevVwapSegment>	rthRenderSegments		= new List<IltDevVwapSegment>();

		private IltDevVwapSegment	currentWeeklyRenderSegment;
		private IltDevVwapSegment	currentFullRenderSegment;
		private IltDevVwapSegment	currentRthRenderSegment;

		private SharpDX.Direct2D1.SolidColorBrush[]	dxBrushes;
		private SharpDX.Direct2D1.StrokeStyle		dxDashStyle;
		private SharpDX.Direct2D1.StrokeStyle		dxDotStyle;
		private SharpDX.Direct2D1.StrokeStyle		dxDashDotStyle;
		private SharpDX.Direct2D1.StrokeStyle		dxDashDotDotStyle;
		private SharpDX.DirectWrite.TextFormat		dxLabelTextFormat;

		private Chart		chartWindow;
		private NTMenuItem	toolbarBtn;
		private bool		toolbarBtnAdded;

		private Border		wpfOuterPanel;
		private Button		wpfMinimizeButton;
		private bool		wpfPanelMinimized;
		private bool		wpfPanelAdded;
		private bool		panelUserHidden			= true;
		private Button		wpfWeeklyButton;
		private Button		wpfRthButton;
		private Button		wpfFullButton;
		private Button		wpfBandsButton;
		private Button		wpfWeeklySigmaButton;
		private Button		wpfRthSigmaButton;
		private Button		wpfFullSigmaButton;
		private Grid		wpfRowWeekly;
		private Grid		wpfRowRth;
		private Grid		wpfRowFull;
		private bool		wpfPanelDragging;
		private System.Windows.Point		wpfDragStartPoint;
		private double		wpfDragStartLeft;
		private double		wpfDragStartTop;

		private string[]	weeklyLabelText;
		private string[]	fullLabelText;
		private string[]	rthLabelText;

		private int WeeklySegmentCap	{ get { return Math.Max(1, WeeksOfHistory); } }
		private int DailySegmentCap		{ get { return Math.Max(1, WeeksOfHistory) * DailySessionsPerWeek; } }

		#endregion

		#region ===================== CICLO DE VIDA =====================

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Name = "ILT-DEV-VWAP";
				Description					= "ILT-DEV-VWAP: Real VWAP from a secondary 1-tick data series, without Tick Replay. Time engine fixed to New York/session time internally. Weekly Sunday 18:00 NY to Friday 16:59:59 NY, Full Session 18:00 to 16:59:59 NY, and user-configurable RTH schedule. SharpDX LITE rendering, no native plots.";
				IsOverlay					= true;
				DisplayInDataBox			= false;
				DrawOnPricePanel			= true;
				PaintPriceMarkers			= false;
				IsSuspendedWhileInactive	= false;
				Calculate					= Calculate.OnBarClose;

				StDev1				= 1;
				StDev2				= 2;
				StDev3				= 3;

				ShowWeekly			= true;
				ShowFullSession		= true;
				ShowRTH				= true;
				ShowWeeklyBands		= true;
				ShowFullBands		= true;
				ShowRthBands		= true;

				RthStartTime		= new DateTime(1, 1, 1, 9, 30, 0);
				RthEndTime			= new DateTime(1, 1, 1, 17, 0, 0);

				ShowLabels			= true;
				LabelFontSize		= 11;
				ShowToolbarButton	= true;
				WeeksOfHistory		= 3;

				WeeklyVwapBrush		= Brushes.MediumSeaGreen;
				WeeklyBand1Brush	= Brushes.SeaGreen;
				WeeklyBand2Brush	= Brushes.DarkSeaGreen;
				WeeklyBand3Brush	= Brushes.PaleGreen;

				FullVwapBrush		= Brushes.Gold;
				FullBand1Brush		= Brushes.Goldenrod;
				FullBand2Brush		= Brushes.DarkGoldenrod;
				FullBand3Brush		= Brushes.Peru;

				RthVwapBrush		= Brushes.DeepSkyBlue;
				RthBand1Brush		= Brushes.DodgerBlue;
				RthBand2Brush		= Brushes.Orange;
				RthBand3Brush		= Brushes.MediumVioletRed;

				WeeklyVwapStyle		= DashStyleHelper.Solid;
				WeeklyBand1Style	= DashStyleHelper.Dash;
				WeeklyBand2Style	= DashStyleHelper.Dash;
				WeeklyBand3Style	= DashStyleHelper.Dot;

				FullVwapStyle		= DashStyleHelper.Solid;
				FullBand1Style		= DashStyleHelper.Dash;
				FullBand2Style		= DashStyleHelper.Dash;
				FullBand3Style		= DashStyleHelper.Dot;

				RthVwapStyle		= DashStyleHelper.Solid;
				RthBand1Style		= DashStyleHelper.Dash;
				RthBand2Style		= DashStyleHelper.Dash;
				RthBand3Style		= DashStyleHelper.Dot;
			}
			else if (State == State.Configure)
			{
				weeklyLabelText	= BuildLabelTexts("W");
				fullLabelText	= BuildLabelTexts("FS");
				rthLabelText	= BuildLabelTexts("RTH");

				AddDataSeries(BarsPeriodType.Tick, 1);
			}
			else if (State == State.DataLoaded)
			{
				ResetAllAccumulators();
				ResetAllRenderSegments();

				lastTickBarAccum		= -1;
				currentWeeklyKey		= DateTime.MinValue;
				currentFullSessionKey	= DateTime.MinValue;
				currentRthSessionKey	= DateTime.MinValue;
			}
			else if (State == State.Historical)
			{
				if (ChartControl != null && ShowToolbarButton)
					ChartControl.Dispatcher.InvokeAsync(() => BuildToolbarButton());
			}
			else if (State == State.Terminated)
			{
				CaptureWpfPanelReloadState();

				if (chartWindow == null && ChartControl != null)
				{
					try { chartWindow = Window.GetWindow(ChartControl.Parent) as Chart; }
					catch { }
				}

				Chart		winRef		= chartWindow;
				NTMenuItem	btnRef		= toolbarBtn;
				Border		panelRef	= wpfOuterPanel;

				if (ChartControl != null)
					ChartControl.Dispatcher.InvokeAsync(() => CleanupChartUi(winRef, btnRef, panelRef));

				DisposeDxResources();

				toolbarBtn		= null;
				wpfOuterPanel	= null;
				toolbarBtnAdded	= false;
				wpfPanelAdded	= false;
				chartWindow		= null;
			}
		}

		private void CleanupChartUi(Chart win, NTMenuItem btn, Border panel)
		{
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

		protected override void OnBarUpdate()
		{
			if (BarsInProgress == 1)
			{
				if (CurrentBars[1] < 0)
					return;

				if (CurrentBars[1] == lastTickBarAccum)
					return;

				bool changed		= AccumulateTickBar(0);
				lastTickBarAccum	= CurrentBars[1];

				if (changed && State == State.Realtime)
					RequestThrottledDxRefresh();

				return;
			}

			if (BarsInProgress != 0)
				return;

			if (CurrentBars[0] < 0)
				return;

			RenderAllSessions();
		}

		#endregion

		#region ===================== ACUMULACION =====================

		private bool AccumulateTickBar(int barsAgo)
		{
			double price	= Closes[1][barsAgo];
			double volume	= Volumes[1][barsAgo];

			if (volume <= 0 || double.IsNaN(price) || price <= 0)
				return false;

			DateTime	chartTime	= Times[1][barsAgo];
			DateTime	nyTime		= GetNewYorkTime(chartTime);
			int			hhmmss		= GetHhmmss(nyTime);

			CloseExpiredRenderSegments(chartTime);
			AccumulateAllSessions(nyTime, hhmmss, price, volume);
			UpdateTickDrivenRenderSnapshots(chartTime, nyTime, hhmmss);

			return true;
		}

		private void AccumulateAllSessions(DateTime nyTime, int hhmmss, double price, double volume)
		{
			if (volume <= 0)
				return;

			if (IsInsideWeeklySession(nyTime, hhmmss))
			{
				DateTime key = GetWeeklySessionKey(nyTime);

				if (currentWeeklyKey == DateTime.MinValue || key != currentWeeklyKey)
				{
					ResetWeeklyAccumulators();
					currentWeeklyKey = key;

					BeginRenderSegment(weeklyRenderSegments, ref currentWeeklyRenderSegment, key,
						GetChartTimeFromNewYork(key), GetWeeklySessionEndChartTime(key), WeeklySegmentCap);
				}

				Accumulate(ref weeklyCumVolume, ref weeklyCumPV, ref weeklyCumP2V, price, volume);
			}

			if (IsInsideFullSession(hhmmss))
			{
				DateTime key = GetFullSessionKey(nyTime, hhmmss);

				if (currentFullSessionKey == DateTime.MinValue || key != currentFullSessionKey)
				{
					ResetFullAccumulators();
					currentFullSessionKey = key;

					BeginRenderSegment(fullRenderSegments, ref currentFullRenderSegment, key,
						GetChartTimeFromNewYork(key), GetFullSessionEndChartTime(key), DailySegmentCap);
				}

				Accumulate(ref fullCumVolume, ref fullCumPV, ref fullCumP2V, price, volume);
			}

			if (IsInsideRthSession(hhmmss))
			{
				DateTime key = GetRthSessionKey(nyTime, hhmmss);

				if (currentRthSessionKey == DateTime.MinValue || key != currentRthSessionKey)
				{
					ResetRthAccumulators();
					currentRthSessionKey = key;

					BeginRenderSegment(rthRenderSegments, ref currentRthRenderSegment, key,
						GetRthSessionStartChartTime(key), GetRthSessionEndChartTime(key), DailySegmentCap);
				}

				Accumulate(ref rthCumVolume, ref rthCumPV, ref rthCumP2V, price, volume);
			}
		}

		private void RenderAllSessions()
		{
			if (State == State.Realtime)
				RequestThrottledDxRefresh();
		}

		private void UpdateTickDrivenRenderSnapshots(DateTime chartTime, DateTime nyTime, int hhmmss)
		{
			if (IsInsideWeeklySession(nyTime, hhmmss) && weeklyCumVolume > 0)
				AppendOrUpdateTickRenderPoint(currentWeeklyRenderSegment, chartTime, weeklyCumVolume, weeklyCumPV, weeklyCumP2V);

			if (IsInsideFullSession(hhmmss) && fullCumVolume > 0)
				AppendOrUpdateTickRenderPoint(currentFullRenderSegment, chartTime, fullCumVolume, fullCumPV, fullCumP2V);

			if (IsInsideRthSession(hhmmss) && rthCumVolume > 0)
				AppendOrUpdateTickRenderPoint(currentRthRenderSegment, chartTime, rthCumVolume, rthCumPV, rthCumP2V);
		}

		private void RequestThrottledDxRefresh()
		{
			if (ChartControl == null)
				return;

			DateTime now = DateTime.UtcNow;

			if (lastDxInvalidateUtc != DateTime.MinValue && (now - lastDxInvalidateUtc).TotalMilliseconds < RenderInvalidateThrottleMs)
				return;

			lastDxInvalidateUtc = now;

			try { ChartControl.InvalidateVisual(); }
			catch { }
		}

		private void Accumulate(ref double cumVolume, ref double cumPV, ref double cumP2V, double price, double volume)
		{
			double pv = price * volume;

			cumVolume	+= volume;
			cumPV		+= pv;
			cumP2V		+= price * pv;
		}

		private void ResetAllAccumulators()
		{
			ResetWeeklyAccumulators();
			ResetFullAccumulators();
			ResetRthAccumulators();
		}

		private void ResetWeeklyAccumulators()	{ weeklyCumP2V	= weeklyCumPV	= weeklyCumVolume	= 0; }
		private void ResetFullAccumulators()	{ fullCumP2V	= fullCumPV		= fullCumVolume		= 0; }
		private void ResetRthAccumulators()		{ rthCumP2V		= rthCumPV		= rthCumVolume		= 0; }

		private void ResetAllRenderSegments()
		{
			weeklyRenderSegments.Clear();
			fullRenderSegments.Clear();
			rthRenderSegments.Clear();

			currentWeeklyRenderSegment	= null;
			currentFullRenderSegment	= null;
			currentRthRenderSegment		= null;
		}

		#endregion

		#region ===================== SESIONES / TIEMPO =====================

		private DateTime GetNewYorkTime(DateTime chartTime)
		{
			return chartTime;
		}

		private DateTime GetChartTimeFromNewYork(DateTime nyTime)
		{
			return nyTime;
		}

		private int GetHhmmss(DateTime t)
		{
			return t.Hour * 10000 + t.Minute * 100 + t.Second;
		}

		private bool IsInsideWeeklySession(DateTime nyTime, int hhmmss)
		{
			if (nyTime.DayOfWeek == DayOfWeek.Saturday)
				return false;

			if (nyTime.DayOfWeek == DayOfWeek.Sunday)
				return hhmmss >= 180000;

			if (nyTime.DayOfWeek == DayOfWeek.Friday)
				return hhmmss < 170000;

			return true;
		}

		private DateTime GetWeeklySessionKey(DateTime nyTime)
		{
			int			offset	= ((int)nyTime.DayOfWeek + 7) % 7;
			DateTime	anchor	= nyTime.Date.AddDays(-offset).AddHours(18);

			if (nyTime < anchor)
				anchor = anchor.AddDays(-7);

			return anchor;
		}

		private bool IsInsideFullSession(int hhmmss)
		{
			if (hhmmss < 170000)
				return true;

			return hhmmss >= 180000;
		}

		private DateTime GetFullSessionKey(DateTime nyTime, int hhmmss)
		{
			if (hhmmss < 180000)
				return nyTime.Date.AddDays(-1).AddHours(18);

			return nyTime.Date.AddHours(18);
		}

		private int GetConfiguredRthStartHhmmss()	{ return GetHhmmss(RthStartTime); }
		private int GetConfiguredRthEndHhmmss()		{ return GetHhmmss(RthEndTime); }

		private bool IsInsideRthSession(int hhmmss)
		{
			int start	= GetConfiguredRthStartHhmmss();
			int end		= GetConfiguredRthEndHhmmss();

			if (start == end)
				return false;

			if (start < end)
				return hhmmss >= start && hhmmss < end;

			return hhmmss >= start || hhmmss < end;
		}

		private DateTime GetRthSessionKey(DateTime nyTime, int hhmmss)
		{
			int start	= GetConfiguredRthStartHhmmss();
			int end		= GetConfiguredRthEndHhmmss();

			if (start > end && hhmmss < end)
				return nyTime.Date.AddDays(-1);

			return nyTime.Date;
		}

		private DateTime GetWeeklySessionEndChartTime(DateTime key)
		{
			return GetChartTimeFromNewYork(key.Date.AddDays(5).AddHours(17));
		}

		private DateTime GetFullSessionEndChartTime(DateTime key)
		{
			return GetChartTimeFromNewYork(key.Date.AddDays(1).AddHours(17));
		}

		private DateTime GetRthSessionEndChartTime(DateTime key)
		{
			int			start	= GetConfiguredRthStartHhmmss();
			int			end		= GetConfiguredRthEndHhmmss();
			DateTime	fin		= key.Date.Add(RthEndTime.TimeOfDay);

			if (start > end)
				fin = fin.AddDays(1);

			return GetChartTimeFromNewYork(fin);
		}

		private DateTime GetRthSessionStartChartTime(DateTime key)
		{
			return GetChartTimeFromNewYork(key.Date.Add(RthStartTime.TimeOfDay));
		}

		#endregion

		#region ===================== SEGMENTOS DE DIBUJO =====================

		private DateTime ClampRenderTimeToSegment(IltDevVwapSegment seg, DateTime t)
		{
			if (seg == null)
				return t;

			if (seg.StartTime != DateTime.MinValue && t < seg.StartTime)
				return seg.StartTime;

			if (seg.EndTime != DateTime.MinValue && t > seg.EndTime)
				return seg.EndTime;

			return t;
		}

		private void CloseExpiredRenderSegments(DateTime chartTime)
		{
			CloseRenderSegmentIfExpired(currentWeeklyRenderSegment,	chartTime, weeklyCumVolume,	weeklyCumPV,	weeklyCumP2V);
			CloseRenderSegmentIfExpired(currentFullRenderSegment,	chartTime, fullCumVolume,	fullCumPV,		fullCumP2V);
			CloseRenderSegmentIfExpired(currentRthRenderSegment,		chartTime, rthCumVolume,	rthCumPV,		rthCumP2V);
		}

		private void CloseRenderSegmentIfExpired(IltDevVwapSegment seg, DateTime chartTime, double cumVolume, double cumPV, double cumP2V)
		{
			if (seg == null || seg.IsClosed || seg.EndTime == DateTime.MinValue)
				return;

			if (chartTime < seg.EndTime)
				return;

			if (cumVolume > 0)
				AppendOrUpdateExactRenderPoint(seg, seg.EndTime, cumVolume, cumPV, cumP2V);

			seg.IsClosed = true;
		}

		private void BeginRenderSegment(List<IltDevVwapSegment> list, ref IltDevVwapSegment current,
			DateTime key, DateTime startTime, DateTime endTime, int cap)
		{
			current = new IltDevVwapSegment
			{
				Key			= key,
				StartTime	= startTime,
				EndTime		= endTime,
				IsClosed	= false
			};

			list.Add(current);

			if (cap < 1)
				cap = 1;

			while (list.Count > cap)
				list.RemoveAt(0);
		}

		private void AppendCollapsedAnchor(IltDevVwapSegment seg, DateTime time, double vwap)
		{
			if (seg == null || double.IsNaN(vwap) || double.IsInfinity(vwap))
				return;

			if (seg.Points.Count > 0)
				return;

			seg.Points.Add(new IltDevVwapPoint
			{
				Time		= time,
				V0			= vwap,
				V1			= vwap,
				V2			= vwap,
				V3			= vwap,
				V4			= vwap,
				V5			= vwap,
				V6			= vwap,
				BandsReady	= true,
				IsAnchor	= true
			});
		}

		private void AppendOrUpdateTickRenderPoint(IltDevVwapSegment seg, DateTime time, double cumVolume, double cumPV, double cumP2V)
		{
			if (seg == null || seg.IsClosed || cumVolume <= 0)
				return;

			time = ClampRenderTimeToSegment(seg, time);

			double vwap		= cumPV / cumVolume;
			double varianza	= (cumP2V / cumVolume) - (vwap * vwap);
			double sd		= varianza > 0 ? Math.Sqrt(varianza) : 0;

			List<IltDevVwapPoint> pts = seg.Points;

			if (pts.Count == 0)
				AppendCollapsedAnchor(seg, seg.StartTime, vwap);

			if (pts.Count > 0 && time <= pts[0].Time)
			{
				if (seg.StartTime.Year	== time.Year
					&& seg.StartTime.Month	== time.Month
					&& seg.StartTime.Day	== time.Day
					&& seg.StartTime.Hour	== time.Hour
					&& seg.StartTime.Minute	== time.Minute)
					return;
			}

			IltDevVwapPoint last	= pts[pts.Count - 1];
			bool sameMinute			= !last.IsAnchor
									&& last.Time.Year	== time.Year
									&& last.Time.Month	== time.Month
									&& last.Time.Day	== time.Day
									&& last.Time.Hour	== time.Hour
									&& last.Time.Minute	== time.Minute;

			IltDevVwapPoint target;

			if (sameMinute)
			{
				target		= last;
				target.Time	= time;
			}
			else
			{
				target = new IltDevVwapPoint { Time = time, IsAnchor = false };
				pts.Add(target);
				TrimRenderPointList(pts);
			}

			target.V0			= vwap;
			target.V1			= vwap + sd * StDev1;
			target.V2			= vwap - sd * StDev1;
			target.V3			= vwap + sd * StDev2;
			target.V4			= vwap - sd * StDev2;
			target.V5			= vwap + sd * StDev3;
			target.V6			= vwap - sd * StDev3;
			target.BandsReady	= true;
		}

		private void AppendOrUpdateExactRenderPoint(IltDevVwapSegment seg, DateTime time, double cumVolume, double cumPV, double cumP2V)
		{
			if (seg == null || seg.IsClosed || cumVolume <= 0)
				return;

			time = ClampRenderTimeToSegment(seg, time);

			double vwap		= cumPV / cumVolume;
			double varianza	= (cumP2V / cumVolume) - (vwap * vwap);
			double sd		= varianza > 0 ? Math.Sqrt(varianza) : 0;

			List<IltDevVwapPoint> pts = seg.Points;

			if (pts.Count == 0)
			{
				AppendCollapsedAnchor(seg, time, vwap);
				return;
			}

			if (time <= pts[0].Time)
				return;

			IltDevVwapPoint last = pts[pts.Count - 1];
			IltDevVwapPoint target;

			if (last.Time == time && !last.IsAnchor)
			{
				target = last;
			}
			else
			{
				target = new IltDevVwapPoint { Time = time, IsAnchor = false };
				pts.Add(target);
				TrimRenderPointList(pts);
			}

			target.V0			= vwap;
			target.V1			= vwap + sd * StDev1;
			target.V2			= vwap - sd * StDev1;
			target.V3			= vwap + sd * StDev2;
			target.V4			= vwap - sd * StDev2;
			target.V5			= vwap + sd * StDev3;
			target.V6			= vwap - sd * StDev3;
			target.BandsReady	= true;
		}

		private void TrimRenderPointList(List<IltDevVwapPoint> pts)
		{
			// El binario original conserva todos los puntos del segmento.
		}

		#endregion

		#region ===================== RENDER SHARPDX =====================

		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			base.OnRender(chartControl, chartScale);

			if (chartControl == null || chartScale == null || RenderTarget == null)
				return;

			if (dxBrushes == null || dxBrushes.Length < 21 || dxBrushes[0] == null)
				CreateDxResources();

			DateTime visibleFrom;
			DateTime visibleTo;

			GetVisibleTimeRange(out visibleFrom, out visibleTo);

			DrawRenderSegments(chartControl, chartScale, weeklyRenderSegments,	0,	ShowWeekly,			ShowWeeklyBands,	visibleFrom, visibleTo);
			DrawRenderSegments(chartControl, chartScale, fullRenderSegments,		7,	ShowFullSession,	ShowFullBands,		visibleFrom, visibleTo);
			DrawRenderSegments(chartControl, chartScale, rthRenderSegments,		14,	ShowRTH,			ShowRthBands,		visibleFrom, visibleTo);

			DrawDxLabels(chartControl, chartScale, weeklyRenderSegments,	0,	ShowWeekly,			ShowWeeklyBands,	weeklyLabelText,	visibleFrom, visibleTo);
			DrawDxLabels(chartControl, chartScale, fullRenderSegments,	7,	ShowFullSession,	ShowFullBands,		fullLabelText,		visibleFrom, visibleTo);
			DrawDxLabels(chartControl, chartScale, rthRenderSegments,	14,	ShowRTH,			ShowRthBands,		rthLabelText,		visibleFrom, visibleTo);
		}

		public override void OnRenderTargetChanged()
		{
			DisposeDxResources();

			if (RenderTarget != null)
				CreateDxResources();
		}

		private void GetVisibleTimeRange(out DateTime from, out DateTime to)
		{
			from	= DateTime.MinValue;
			to		= DateTime.MaxValue;

			try
			{
				if (ChartBars == null || BarsArray == null || BarsArray.Length == 0 || BarsArray[0] == null)
					return;

				int firstIdx	= Math.Max(0, ChartBars.FromIndex);
				int lastIdx		= Math.Min(BarsArray[0].Count - 1, ChartBars.ToIndex);

				if (lastIdx < firstIdx)
					return;

				from	= BarsArray[0].GetTime(firstIdx).AddMinutes(-10);
				to		= BarsArray[0].GetTime(lastIdx).AddMinutes(10);
			}
			catch
			{
				from	= DateTime.MinValue;
				to		= DateTime.MaxValue;
			}
		}

		private int FindFirstPointAtOrAfter(List<IltDevVwapPoint> pts, DateTime t)
		{
			if (pts == null || pts.Count == 0 || t == DateTime.MinValue)
				return 0;

			int lo		= 0;
			int hi		= pts.Count - 1;
			int result	= pts.Count;

			while (lo <= hi)
			{
				int mid = lo + ((hi - lo) >> 1);

				if (pts[mid].Time >= t)
				{
					result	= mid;
					hi		= mid - 1;
				}
				else
					lo = mid + 1;
			}

			return result;
		}

		private int FindFirstPointAfter(List<IltDevVwapPoint> pts, DateTime t)
		{
			if (pts == null || pts.Count == 0)
				return 0;

			if (t == DateTime.MaxValue)
				return pts.Count;

			int lo		= 0;
			int hi		= pts.Count - 1;
			int result	= pts.Count;

			while (lo <= hi)
			{
				int mid = lo + ((hi - lo) >> 1);

				if (pts[mid].Time > t)
				{
					result	= mid;
					hi		= mid - 1;
				}
				else
					lo = mid + 1;
			}

			return result;
		}

		private void DrawRenderSegments(ChartControl chartControl, ChartScale chartScale,
			List<IltDevVwapSegment> segments, int brushBase, bool showVwap, bool showBands,
			DateTime visibleFrom, DateTime visibleTo)
		{
			if ((!showVwap && !showBands) || segments == null || segments.Count == 0 || dxBrushes == null)
				return;

			SharpDX.Direct2D1.SolidColorBrush b0 = GetDxBrush(brushBase);
			SharpDX.Direct2D1.SolidColorBrush b1 = GetDxBrush(brushBase + 1);
			SharpDX.Direct2D1.SolidColorBrush b2 = GetDxBrush(brushBase + 2);
			SharpDX.Direct2D1.SolidColorBrush b3 = GetDxBrush(brushBase + 3);
			SharpDX.Direct2D1.SolidColorBrush b4 = GetDxBrush(brushBase + 4);
			SharpDX.Direct2D1.SolidColorBrush b5 = GetDxBrush(brushBase + 5);
			SharpDX.Direct2D1.SolidColorBrush b6 = GetDxBrush(brushBase + 6);

			// Dos interruptores independientes: la linea del VWAP (b0) y las seis
			// lineas de desviacion (b1..b6). Con el pincel en null el bucle las
			// salta sin coste extra, asi que se puede dejar una sin la otra.
			if (!showVwap)
				b0 = null;

			if (!showBands)
				b1 = b2 = b3 = b4 = b5 = b6 = null;

			SharpDX.Direct2D1.StrokeStyle s0 = GetDxStrokeStyle(OrigStyle(brushBase));
			SharpDX.Direct2D1.StrokeStyle s1 = GetDxStrokeStyle(OrigStyle(brushBase + 1));
			SharpDX.Direct2D1.StrokeStyle s2 = GetDxStrokeStyle(OrigStyle(brushBase + 2));
			SharpDX.Direct2D1.StrokeStyle s3 = GetDxStrokeStyle(OrigStyle(brushBase + 3));
			SharpDX.Direct2D1.StrokeStyle s4 = GetDxStrokeStyle(OrigStyle(brushBase + 4));
			SharpDX.Direct2D1.StrokeStyle s5 = GetDxStrokeStyle(OrigStyle(brushBase + 5));
			SharpDX.Direct2D1.StrokeStyle s6 = GetDxStrokeStyle(OrigStyle(brushBase + 6));

			float minX = float.MinValue;
			float maxX = float.MaxValue;

			try
			{
				if (ChartPanel != null)
				{
					minX = (float)ChartPanel.X - 40f;
					maxX = (float)(ChartPanel.X + ChartPanel.W) + 40f;
				}
			}
			catch { }

			for (int i = 0; i < segments.Count; i++)
			{
				DrawRenderSegmentSinglePass(chartControl, chartScale, segments[i], minX, maxX,
					visibleFrom, visibleTo, b0, b1, b2, b3, b4, b5, b6, s0, s1, s2, s3, s4, s5, s6);
			}
		}

		private SharpDX.Direct2D1.SolidColorBrush GetDxBrush(int index)
		{
			if (dxBrushes == null || index < 0 || index >= dxBrushes.Length)
				return null;

			return dxBrushes[index];
		}

		private Brush OrigBrush(int index)
		{
			switch (index)
			{
				case 0:					return WeeklyVwapBrush	?? Brushes.MediumSeaGreen;
				case 1: case 2:			return WeeklyBand1Brush	?? Brushes.SeaGreen;
				case 3: case 4:			return WeeklyBand2Brush	?? Brushes.DarkSeaGreen;
				case 5: case 6:			return WeeklyBand3Brush	?? Brushes.PaleGreen;
				case 7:					return FullVwapBrush	?? Brushes.Gold;
				case 8: case 9:			return FullBand1Brush	?? Brushes.Goldenrod;
				case 10: case 11:		return FullBand2Brush	?? Brushes.DarkGoldenrod;
				case 12: case 13:		return FullBand3Brush	?? Brushes.Peru;
				case 14:				return RthVwapBrush		?? Brushes.DeepSkyBlue;
				case 15: case 16:		return RthBand1Brush	?? Brushes.DodgerBlue;
				case 17: case 18:		return RthBand2Brush	?? Brushes.Orange;
				case 19: case 20:		return RthBand3Brush	?? Brushes.MediumVioletRed;
				default:				return Brushes.White;
			}
		}

		private DashStyleHelper OrigStyle(int index)
		{
			switch (index)
			{
				case 0:					return WeeklyVwapStyle;
				case 1: case 2:			return WeeklyBand1Style;
				case 3: case 4:			return WeeklyBand2Style;
				case 5: case 6:			return WeeklyBand3Style;
				case 7:					return FullVwapStyle;
				case 8: case 9:			return FullBand1Style;
				case 10: case 11:		return FullBand2Style;
				case 12: case 13:		return FullBand3Style;
				case 14:				return RthVwapStyle;
				case 15: case 16:		return RthBand1Style;
				case 17: case 18:		return RthBand2Style;
				case 19: case 20:		return RthBand3Style;
				default:				return DashStyleHelper.Solid;
			}
		}

		private SharpDX.Direct2D1.StrokeStyle GetDxStrokeStyle(DashStyleHelper style)
		{
			switch (style)
			{
				case DashStyleHelper.Dash:			return dxDashStyle;
				case DashStyleHelper.DashDot:		return dxDashDotStyle;
				case DashStyleHelper.DashDotDot:	return dxDashDotDotStyle;
				case DashStyleHelper.Dot:			return dxDotStyle;
				default:							return null;
			}
		}

		private void DrawRenderSegmentSinglePass(ChartControl chartControl, ChartScale chartScale, IltDevVwapSegment seg,
			float minX, float maxX, DateTime visibleFrom, DateTime visibleTo,
			SharpDX.Direct2D1.SolidColorBrush b0, SharpDX.Direct2D1.SolidColorBrush b1, SharpDX.Direct2D1.SolidColorBrush b2,
			SharpDX.Direct2D1.SolidColorBrush b3, SharpDX.Direct2D1.SolidColorBrush b4, SharpDX.Direct2D1.SolidColorBrush b5,
			SharpDX.Direct2D1.SolidColorBrush b6,
			SharpDX.Direct2D1.StrokeStyle s0, SharpDX.Direct2D1.StrokeStyle s1, SharpDX.Direct2D1.StrokeStyle s2,
			SharpDX.Direct2D1.StrokeStyle s3, SharpDX.Direct2D1.StrokeStyle s4, SharpDX.Direct2D1.StrokeStyle s5,
			SharpDX.Direct2D1.StrokeStyle s6)
		{
			if (seg == null || seg.Points == null || seg.Points.Count < 2)
				return;

			SharpDX.Vector2 p0 = new SharpDX.Vector2();
			SharpDX.Vector2 p1 = new SharpDX.Vector2();
			SharpDX.Vector2 p2 = new SharpDX.Vector2();
			SharpDX.Vector2 p3 = new SharpDX.Vector2();
			SharpDX.Vector2 p4 = new SharpDX.Vector2();
			SharpDX.Vector2 p5 = new SharpDX.Vector2();
			SharpDX.Vector2 p6 = new SharpDX.Vector2();

			bool has0 = false, has1 = false, has2 = false, has3 = false, has4 = false, has5 = false, has6 = false;

			float prevX = float.NaN;

			List<IltDevVwapPoint> pts = seg.Points;

			int startIdx = FindFirstPointAtOrAfter(pts, visibleFrom);

			if (startIdx > 0)
				startIdx--;

			if (startIdx >= pts.Count)
				return;

			int endIdx = FindFirstPointAfter(pts, visibleTo);

			if (endIdx <= startIdx)
				return;

			endIdx = Math.Min(endIdx, pts.Count);

			int count	= endIdx - startIdx;
			int step	= count <= MaxVisibleRenderPoints ? 1 : Math.Max(1, count / MaxVisibleRenderPoints);
			int lastIdx	= endIdx - 1;

			for (int i = startIdx; i < endIdx; i += step)
			{
				if (step > 1 && i + step >= endIdx && i != lastIdx)
					i = lastIdx;

				IltDevVwapPoint p = pts[i];

				float x = (float)chartControl.GetXByTime(p.Time);

				if (!float.IsNaN(prevX) && prevX > maxX && x > maxX)
					return;

				bool inRange;

				if (float.IsNaN(prevX))
					inRange = true;
				else if (prevX < minX && x < minX)
					inRange = false;
				else if (prevX <= maxX)
					inRange = true;
				else
					inRange = !(x > maxX);

				if (b0 != null && IsFinite(p.V0))
				{
					SharpDX.Vector2 cur = new SharpDX.Vector2(x, (float)chartScale.GetYByValue(p.V0));

					if (has0 && inRange)
						RenderTarget.DrawLine(p0, cur, b0, 2.5f, s0);

					p0		= cur;
					has0	= true;
				}
				else
					has0 = false;

				if (p.BandsReady)
				{
					if (b1 != null && IsFinite(p.V1))
					{
						SharpDX.Vector2 cur = new SharpDX.Vector2(x, (float)chartScale.GetYByValue(p.V1));

						if (has1 && inRange)
							RenderTarget.DrawLine(p1, cur, b1, 1.2f, s1);

						p1		= cur;
						has1	= true;
					}
					else
						has1 = false;

					if (b2 != null && IsFinite(p.V2))
					{
						SharpDX.Vector2 cur = new SharpDX.Vector2(x, (float)chartScale.GetYByValue(p.V2));

						if (has2 && inRange)
							RenderTarget.DrawLine(p2, cur, b2, 1.2f, s2);

						p2		= cur;
						has2	= true;
					}
					else
						has2 = false;

					if (b3 != null && IsFinite(p.V3))
					{
						SharpDX.Vector2 cur = new SharpDX.Vector2(x, (float)chartScale.GetYByValue(p.V3));

						if (has3 && inRange)
							RenderTarget.DrawLine(p3, cur, b3, 1f, s3);

						p3		= cur;
						has3	= true;
					}
					else
						has3 = false;

					if (b4 != null && IsFinite(p.V4))
					{
						SharpDX.Vector2 cur = new SharpDX.Vector2(x, (float)chartScale.GetYByValue(p.V4));

						if (has4 && inRange)
							RenderTarget.DrawLine(p4, cur, b4, 1f, s4);

						p4		= cur;
						has4	= true;
					}
					else
						has4 = false;

					if (b5 != null && IsFinite(p.V5))
					{
						SharpDX.Vector2 cur = new SharpDX.Vector2(x, (float)chartScale.GetYByValue(p.V5));

						if (has5 && inRange)
							RenderTarget.DrawLine(p5, cur, b5, 1f, s5);

						p5		= cur;
						has5	= true;
					}
					else
						has5 = false;

					if (b6 != null && IsFinite(p.V6))
					{
						SharpDX.Vector2 cur = new SharpDX.Vector2(x, (float)chartScale.GetYByValue(p.V6));

						if (has6 && inRange)
							RenderTarget.DrawLine(p6, cur, b6, 1f, s6);

						p6		= cur;
						has6	= true;
					}
					else
						has6 = false;
				}
				else
				{
					has1 = has2 = has3 = has4 = has5 = has6 = false;
				}

				prevX = x;
			}
		}

		private void DrawDxLabels(ChartControl chartControl, ChartScale chartScale, List<IltDevVwapSegment> segments,
			int brushBase, bool showVwap, bool showBands, string[] labels, DateTime visibleFrom, DateTime visibleTo)
		{
			if (!ShowLabels || (!showVwap && !showBands) || segments == null || labels == null || labels.Length < 7
				|| dxLabelTextFormat == null || dxBrushes == null || chartControl == null || chartScale == null)
				return;

			IltDevVwapPoint p = GetLastVisibleRenderPoint(segments, visibleFrom, visibleTo);

			if (p == null)
				return;

			float x = (float)chartControl.GetXByTime(p.Time) + LabelPixelOffset;

			try
			{
				if (ChartPanel != null)
				{
					float left	= (float)ChartPanel.X + 2f;
					float right	= (float)(ChartPanel.X + ChartPanel.W) - LabelTextWidth - 2f;

					if (x < left)
						x = left;

					if (x > right)
						x = right;
				}
			}
			catch { }

			if (showVwap)
				DrawDxLabel(labels[0], x, p.V0, brushBase,	chartScale);

			// Sin bandas tampoco se pintan sus etiquetas (+1s, -1s, ...).
			if (!showBands)
				return;

			DrawDxLabel(labels[1], x, p.V1, brushBase + 1,	chartScale);
			DrawDxLabel(labels[2], x, p.V2, brushBase + 2,	chartScale);
			DrawDxLabel(labels[3], x, p.V3, brushBase + 3,	chartScale);
			DrawDxLabel(labels[4], x, p.V4, brushBase + 4,	chartScale);
			DrawDxLabel(labels[5], x, p.V5, brushBase + 5,	chartScale);
			DrawDxLabel(labels[6], x, p.V6, brushBase + 6,	chartScale);
		}

		private IltDevVwapPoint GetLastVisibleRenderPoint(List<IltDevVwapSegment> segments, DateTime visibleFrom, DateTime visibleTo)
		{
			if (segments == null || segments.Count == 0)
				return null;

			for (int s = segments.Count - 1; s >= 0; s--)
			{
				IltDevVwapSegment seg = segments[s];

				if (seg == null || seg.Points == null || seg.Points.Count == 0)
					continue;

				List<IltDevVwapPoint> pts = seg.Points;

				int idx = FindFirstPointAfter(pts, visibleTo) - 1;

				if (idx < 0)
					continue;

				for (int i = idx; i >= 0; i--)
				{
					IltDevVwapPoint p = pts[i];

					if (p == null)
						continue;

					if (p.Time < visibleFrom)
						break;

					return p;
				}
			}

			return null;
		}

		private void DrawDxLabel(string text, float x, double value, int brushIndex, ChartScale chartScale)
		{
			if (!IsFinite(value) || RenderTarget == null)
				return;

			SharpDX.Direct2D1.SolidColorBrush brush = GetDxBrush(brushIndex);

			if (brush == null)
				return;

			float y = (float)chartScale.GetYByValue(value);

			SharpDX.RectangleF rect = new SharpDX.RectangleF(x, y - 9f, LabelTextWidth, LabelTextHeight);

			RenderTarget.DrawText(text, dxLabelTextFormat, rect, brush);
		}

		private bool IsFinite(double v)
		{
			return !double.IsNaN(v) && !double.IsInfinity(v);
		}

		private string[] BuildLabelTexts(string prefix)
		{
			return new string[]
			{
				prefix + " VWAP",
				string.Concat(prefix, " +", StDev1, "σ"),
				string.Concat(prefix, " -", StDev1, "σ"),
				string.Concat(prefix, " +", StDev2, "σ"),
				string.Concat(prefix, " -", StDev2, "σ"),
				string.Concat(prefix, " +", StDev3, "σ"),
				string.Concat(prefix, " -", StDev3, "σ")
			};
		}

		private void CreateDxResources()
		{
			if (RenderTarget == null)
				return;

			DisposeDxResources();

			dxBrushes = new SharpDX.Direct2D1.SolidColorBrush[21];

			for (int i = 0; i < dxBrushes.Length; i++)
				dxBrushes[i] = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToDxColor(OrigBrush(i)));

			dxDashStyle			= new SharpDX.Direct2D1.StrokeStyle(Globals.D2DFactory, new SharpDX.Direct2D1.StrokeStyleProperties { DashStyle = SharpDX.Direct2D1.DashStyle.Dash });
			dxDotStyle			= new SharpDX.Direct2D1.StrokeStyle(Globals.D2DFactory, new SharpDX.Direct2D1.StrokeStyleProperties { DashStyle = SharpDX.Direct2D1.DashStyle.Dot });
			dxDashDotStyle		= new SharpDX.Direct2D1.StrokeStyle(Globals.D2DFactory, new SharpDX.Direct2D1.StrokeStyleProperties { DashStyle = SharpDX.Direct2D1.DashStyle.DashDot });
			dxDashDotDotStyle	= new SharpDX.Direct2D1.StrokeStyle(Globals.D2DFactory, new SharpDX.Direct2D1.StrokeStyleProperties { DashStyle = SharpDX.Direct2D1.DashStyle.DashDotDot });

			dxLabelTextFormat	= new SharpDX.DirectWrite.TextFormat(Globals.DirectWriteFactory, "Arial",
									SharpDX.DirectWrite.FontWeight.Bold,
									SharpDX.DirectWrite.FontStyle.Normal,
									SharpDX.DirectWrite.FontStretch.Normal,
									Math.Max(6, LabelFontSize));
		}

		private SharpDX.Color ToDxColor(Brush brush)
		{
			SolidColorBrush scb = brush as SolidColorBrush;

			if (scb == null)
				return new SharpDX.Color(255, 255, 255, 255);

			System.Windows.Media.Color c = scb.Color;

			return new SharpDX.Color(c.R, c.G, c.B, c.A);
		}

		private void DisposeDxResources()
		{
			if (dxBrushes != null)
			{
				for (int i = 0; i < dxBrushes.Length; i++)
				{
					if (dxBrushes[i] != null)
					{
						dxBrushes[i].Dispose();
						dxBrushes[i] = null;
					}
				}

				dxBrushes = null;
			}

			if (dxDashStyle != null)		{ dxDashStyle.Dispose();		dxDashStyle			= null; }
			if (dxDotStyle != null)			{ dxDotStyle.Dispose();			dxDotStyle			= null; }
			if (dxDashDotStyle != null)		{ dxDashDotStyle.Dispose();		dxDashDotStyle		= null; }
			if (dxDashDotDotStyle != null)	{ dxDashDotDotStyle.Dispose();	dxDashDotDotStyle	= null; }
			if (dxLabelTextFormat != null)	{ dxLabelTextFormat.Dispose();	dxLabelTextFormat	= null; }
		}

		#endregion

		#region ===================== BOTON TOOLBAR + PANEL WPF =====================

		private void BuildToolbarButton()
		{
			if (toolbarBtnAdded || !ShowToolbarButton)
				return;

			if (ChartControl == null)
				return;

			try
			{
				chartWindow = Window.GetWindow(ChartControl.Parent) as Chart;

				if (chartWindow == null || chartWindow.MainMenu == null)
					return;

				RemoveOrphanButtonsThisChart();

				toolbarBtn			= new NTMenuItem();
				toolbarBtn.Header	= "ILT-DEV-VWAP";
				toolbarBtn.Style	= Application.Current.TryFindResource("MainMenuItem") as Style;
				toolbarBtn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 90, 190, 255));
				toolbarBtn.ToolTip	= "Mostrar / ocultar panel ILT-DEV VWAP";
				toolbarBtn.Tag		= ToolbarBtnTag;
				toolbarBtn.Click   += ToolbarBtn_Click;

				chartWindow.MainMenu.Add(toolbarBtn);
				toolbarBtnAdded = true;

				RestoreWpfPanelAfterReload();
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
				CaptureWpfPanelReloadState();
				return;
			}

			panelUserHidden = !panelUserHidden;
			ApplyPanelVisibilityFromToolbar();
			CaptureWpfPanelReloadState();
		}

		private void ApplyPanelVisibilityFromToolbar()
		{
			if (wpfOuterPanel == null)
				return;

			wpfOuterPanel.Visibility = panelUserHidden ? Visibility.Collapsed : Visibility.Visible;
		}

		private void RestoreWpfPanelAfterReload()
		{
			if (ChartControl == null)
				return;

			WpfPanelReloadState state;

			if (!wpfPanelReloadStates.TryGetValue(ChartControl, out state) || !state.IsOpen)
				return;

			panelUserHidden = false;
			EnsureWpfControlPanel();
		}

		private Thickness GetWpfPanelReloadMargin()
		{
			if (ChartControl == null)
				return new Thickness(14, 14, 0, 0);

			WpfPanelReloadState state;

			if (!wpfPanelReloadStates.TryGetValue(ChartControl, out state) || !state.HasPosition)
				return new Thickness(14, 14, 0, 0);

			return new Thickness(state.Left, state.Top, 0, 0);
		}

		private void CaptureWpfPanelReloadState()
		{
			if (ChartControl == null || wpfOuterPanel == null)
				return;

			WpfPanelReloadState state = wpfPanelReloadStates.GetValue(
				ChartControl, key => new WpfPanelReloadState());

			state.Left		= wpfOuterPanel.Margin.Left;
			state.Top		= wpfOuterPanel.Margin.Top;
			state.HasPosition	= true;
			state.IsOpen		= !panelUserHidden;
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
					Margin				= GetWpfPanelReloadMargin(),
					Width				= 206
				};

				outer.Child = BuildPanelInner();

				UserControlCollection.Add(outer);

				wpfOuterPanel	= outer;
				wpfPanelAdded	= true;

				ApplyPanelVisibilityFromToolbar();
				CaptureWpfPanelReloadState();
			}
			catch (Exception ex)
			{
				try { Print("ILT-DEV-VWAP panel add error: " + ex.Message); }
				catch { }

				wpfOuterPanel	= null;
				wpfPanelAdded	= false;
			}
		}

		private Grid BuildPanelInner()
		{
			Grid root = new Grid();

			double[] rowHeights = new double[] { -1, 6, -1, 4, -1, 4, -1, 8, -1, 6 };

			for (int i = 0; i < rowHeights.Length; i++)
			{
				RowDefinition rd = new RowDefinition();
				rd.Height = rowHeights[i] >= 0 ? new GridLength(rowHeights[i]) : GridLength.Auto;
				root.RowDefinitions.Add(rd);
			}

			Grid header = new Grid
			{
				Height		= 22,
				Background	= new SolidColorBrush(System.Windows.Media.Color.FromArgb(190, 20, 105, 145)),
				Cursor		= Cursors.SizeAll
			};

			ColumnDefinition c0 = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
			header.ColumnDefinitions.Add(c0);

			ColumnDefinition c1 = new ColumnDefinition { Width = GridLength.Auto };
			header.ColumnDefinitions.Add(c1);

			TextBlock title = new TextBlock
			{
				Text				= "ILT-DEV-VWAP",
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
				Content		= wpfPanelMinimized ? "▶" : "▼",
				Width		= 22,
				Height		= 18,
				Margin		= new Thickness(2),
				FontFamily	= new FontFamily("Consolas"),
				FontSize	= 10,
				FontWeight	= FontWeights.Bold,
				Foreground	= Brushes.White,
				Background	= new SolidColorBrush(System.Windows.Media.Color.FromArgb(190, 40, 48, 58)),
				BorderBrush	= new SolidColorBrush(System.Windows.Media.Color.FromArgb(230, 110, 180, 210)),
				BorderThickness = new Thickness(1)
			};

			wpfMinimizeButton.Click += (o, e) =>
			{
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

			// Cada sesion es una fila con dos interruptores independientes:
			// el ancho enciende/apaga la linea del VWAP, el estrecho sus desviaciones.
			wpfWeeklyButton			= CreatePanelButton("VWAP SEM");
			wpfWeeklySigmaButton	= CreateSigmaButton("Desviaciones del VWAP semanal");

			wpfWeeklyButton.Click += (o, e) =>
			{
				ShowWeekly = !ShowWeekly;
				TrasCambioDePanel();
				e.Handled = true;
			};

			wpfWeeklySigmaButton.Click += (o, e) =>
			{
				ShowWeeklyBands = !ShowWeeklyBands;
				TrasCambioDePanel();
				e.Handled = true;
			};

			wpfRowWeekly = BuildSessionRow(wpfWeeklyButton, wpfWeeklySigmaButton);
			Grid.SetRow(wpfRowWeekly, 2);
			root.Children.Add(wpfRowWeekly);

			wpfRthButton		= CreatePanelButton("VWAP RTH");
			wpfRthSigmaButton	= CreateSigmaButton("Desviaciones del VWAP RTH");

			wpfRthButton.Click += (o, e) =>
			{
				ShowRTH = !ShowRTH;
				TrasCambioDePanel();
				e.Handled = true;
			};

			wpfRthSigmaButton.Click += (o, e) =>
			{
				ShowRthBands = !ShowRthBands;
				TrasCambioDePanel();
				e.Handled = true;
			};

			wpfRowRth = BuildSessionRow(wpfRthButton, wpfRthSigmaButton);
			Grid.SetRow(wpfRowRth, 4);
			root.Children.Add(wpfRowRth);

			wpfFullButton		= CreatePanelButton("VWAP FS");
			wpfFullSigmaButton	= CreateSigmaButton("Desviaciones del VWAP Full Session");

			wpfFullButton.Click += (o, e) =>
			{
				ShowFullSession = !ShowFullSession;
				TrasCambioDePanel();
				e.Handled = true;
			};

			wpfFullSigmaButton.Click += (o, e) =>
			{
				ShowFullBands = !ShowFullBands;
				TrasCambioDePanel();
				e.Handled = true;
			};

			wpfRowFull = BuildSessionRow(wpfFullButton, wpfFullSigmaButton);
			Grid.SetRow(wpfRowFull, 6);
			root.Children.Add(wpfRowFull);

			// Atajo: apaga las desviaciones de las tres sesiones de golpe, y si ya
			// estan todas apagadas las vuelve a encender. No es un interruptor
			// aparte, solo mueve los tres de arriba.
			wpfBandsButton = CreatePanelButton("DESVIACIONES");
			wpfBandsButton.Click += (o, e) =>
			{
				bool encender = !AlgunaBandaVisible;

				ShowWeeklyBands	= encender;
				ShowFullBands	= encender;
				ShowRthBands	= encender;

				TrasCambioDePanel();
				e.Handled = true;
			};
			Grid.SetRow(wpfBandsButton, 8);
			root.Children.Add(wpfBandsButton);

			UpdateWpfPanelState();

			return root;
		}

		private bool AlgunaBandaVisible
		{
			get { return ShowWeeklyBands || ShowFullBands || ShowRthBands; }
		}

		private void TrasCambioDePanel()
		{
			UpdateWpfPanelState();
			RequestChartRefresh();
		}

		private Grid BuildSessionRow(Button principal, Button sigma)
		{
			Grid fila = new Grid();

			fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

			principal.Margin	= new Thickness(8, 0, 3, 0);
			sigma.Margin		= new Thickness(0, 0, 8, 0);

			Grid.SetColumn(principal, 0);
			Grid.SetColumn(sigma, 1);

			fila.Children.Add(principal);
			fila.Children.Add(sigma);

			return fila;
		}

		private Button CreateSigmaButton(string tip)
		{
			Button b = CreatePanelButton("σ");

			b.Width		= 30;
			b.FontSize	= 13;
			b.ToolTip	= tip;

			return b;
		}

		private void ApplyPanelMinimized()
		{
			if (wpfMinimizeButton != null)
				wpfMinimizeButton.Content = wpfPanelMinimized ? "▶" : "▼";

			Visibility v = wpfPanelMinimized ? Visibility.Collapsed : Visibility.Visible;

			if (wpfRowWeekly != null)		wpfRowWeekly.Visibility		= v;
			if (wpfRowRth != null)			wpfRowRth.Visibility		= v;
			if (wpfRowFull != null)			wpfRowFull.Visibility		= v;
			if (wpfBandsButton != null)		wpfBandsButton.Visibility	= v;
		}

		private Button CreatePanelButton(string texto)
		{
			Button b = new Button
			{
				Content			= texto,
				FontFamily		= new FontFamily("Consolas"),
				FontSize		= 11,
				FontWeight		= FontWeights.Bold,
				Height			= 26,
				Margin			= new Thickness(8, 0, 8, 0),
				Padding			= new Thickness(4, 2, 4, 2),
				Foreground		= Brushes.White,
				BorderBrush		= new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 120, 120, 120)),
				BorderThickness	= new Thickness(1),
				Cursor			= Cursors.Hand,
				Focusable		= false
			};

			return b;
		}

		private void UpdateWpfPanelState()
		{
			StylePanelButton(wpfWeeklyButton,		ShowWeekly);
			StylePanelButton(wpfRthButton,			ShowRTH);
			StylePanelButton(wpfFullButton,			ShowFullSession);

			StylePanelButton(wpfWeeklySigmaButton,	ShowWeeklyBands);
			StylePanelButton(wpfRthSigmaButton,		ShowRthBands);
			StylePanelButton(wpfFullSigmaButton,	ShowFullBands);

			StylePanelButton(wpfBandsButton,		AlgunaBandaVisible);
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
		}

		private void OnWpfPanelDragMouseDown(object sender, MouseButtonEventArgs e)
		{
			if (wpfOuterPanel == null || ChartControl == null)
				return;

			wpfPanelDragging	= true;
			wpfDragStartPoint	= e.GetPosition(ChartControl);
			wpfDragStartLeft	= wpfOuterPanel.Margin.Left;
			wpfDragStartTop		= wpfOuterPanel.Margin.Top;

			try
			{
				System.Windows.UIElement el = sender as System.Windows.UIElement;

				if (el != null)
					el.CaptureMouse();
			}
			catch { }

			e.Handled = true;
		}

		private void OnWpfPanelDragMouseMove(object sender, MouseEventArgs e)
		{
			if (!wpfPanelDragging || wpfOuterPanel == null || ChartControl == null)
				return;

			System.Windows.Point p = e.GetPosition(ChartControl);

			double dx = p.X - wpfDragStartPoint.X;
			double dy = p.Y - wpfDragStartPoint.Y;

			double maxLeft	= Math.Max(0, ChartControl.ActualWidth  - wpfOuterPanel.ActualWidth  - 2);
			double maxTop	= Math.Max(0, ChartControl.ActualHeight - wpfOuterPanel.ActualHeight - 2);

			double newLeft	= wpfDragStartLeft + dx;
			double newTop	= wpfDragStartTop  + dy;

			if (newLeft < 0)			newLeft = 0;
			else if (newLeft > maxLeft)	newLeft = maxLeft;

			if (newTop < 0)				newTop = 0;
			else if (newTop > maxTop)	newTop = maxTop;

			wpfOuterPanel.Margin = new Thickness(newLeft, newTop, 0, 0);
			CaptureWpfPanelReloadState();

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

				if (el != null)
					el.ReleaseMouseCapture();
			}
			catch { }

			CaptureWpfPanelReloadState();

			e.Handled = true;
		}

		private void RequestChartRefresh()
		{
			try
			{
				if (ChartControl != null)
					ChartControl.InvalidateVisual();
			}
			catch { }
		}

		#endregion

		#region ===================== PROPIEDADES =====================

		[Browsable(false)]
		[XmlIgnore]
		public bool AssumeChartTimeIsNewYork { get { return true; } set { } }

		[NinjaScriptProperty]
		[Display(Name = "Boton en toolbar superior", GroupName = "01. Visual", Order = 9)]
		public bool ShowToolbarButton { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Show Weekly VWAP", GroupName = "01. Visual", Order = 1)]
		public bool ShowWeekly { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Show Full Session VWAP", GroupName = "01. Visual", Order = 2)]
		public bool ShowFullSession { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Show RTH VWAP", GroupName = "01. Visual", Order = 3)]
		public bool ShowRTH { get; set; }

		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "RTH Start Time", GroupName = "01. Visual", Order = 4)]
		public DateTime RthStartTime { get; set; }

		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "RTH End Time", GroupName = "01. Visual", Order = 5)]
		public DateTime RthEndTime { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Show Labels", GroupName = "01. Visual", Order = 6)]
		public bool ShowLabels { get; set; }

		[NinjaScriptProperty]
		[Range(6, int.MaxValue)]
		[Display(Name = "Label Font Size", GroupName = "01. Visual", Order = 7)]
		public int LabelFontSize { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Semanas de historico", GroupName = "01. Visual", Order = 8)]
		public int WeeksOfHistory { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Desviaciones del VWAP semanal", GroupName = "03. Desviaciones", Order = 4)]
		public bool ShowWeeklyBands { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Desviaciones del VWAP Full Session", GroupName = "03. Desviaciones", Order = 5)]
		public bool ShowFullBands { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Desviaciones del VWAP RTH", GroupName = "03. Desviaciones", Order = 6)]
		public bool ShowRthBands { get; set; }

		[NinjaScriptProperty]
		[Range(0.0, double.MaxValue)]
		[Display(Name = "StDev 1", GroupName = "03. Desviaciones", Order = 1)]
		public double StDev1 { get; set; }

		[NinjaScriptProperty]
		[Range(0.0, double.MaxValue)]
		[Display(Name = "StDev 2", GroupName = "03. Desviaciones", Order = 2)]
		public double StDev2 { get; set; }

		[NinjaScriptProperty]
		[Range(0.0, double.MaxValue)]
		[Display(Name = "StDev 3", GroupName = "03. Desviaciones", Order = 3)]
		public double StDev3 { get; set; }

		[XmlIgnore]
		[Display(Name = "Weekly VWAP", GroupName = "02. Colors", Order = 1)]
		public Brush WeeklyVwapBrush { get; set; }

		[Browsable(false)]
		public string WeeklyVwapBrushSerialize
		{
			get { return Serialize.BrushToString(WeeklyVwapBrush); }
			set { WeeklyVwapBrush = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Weekly Band 1", GroupName = "02. Colors", Order = 2)]
		public Brush WeeklyBand1Brush { get; set; }

		[Browsable(false)]
		public string WeeklyBand1BrushSerialize
		{
			get { return Serialize.BrushToString(WeeklyBand1Brush); }
			set { WeeklyBand1Brush = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Weekly Band 2", GroupName = "02. Colors", Order = 3)]
		public Brush WeeklyBand2Brush { get; set; }

		[Browsable(false)]
		public string WeeklyBand2BrushSerialize
		{
			get { return Serialize.BrushToString(WeeklyBand2Brush); }
			set { WeeklyBand2Brush = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Weekly Band 3", GroupName = "02. Colors", Order = 4)]
		public Brush WeeklyBand3Brush { get; set; }

		[Browsable(false)]
		public string WeeklyBand3BrushSerialize
		{
			get { return Serialize.BrushToString(WeeklyBand3Brush); }
			set { WeeklyBand3Brush = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "FS VWAP", GroupName = "02. Colors", Order = 5)]
		public Brush FullVwapBrush { get; set; }

		[Browsable(false)]
		public string FullVwapBrushSerialize
		{
			get { return Serialize.BrushToString(FullVwapBrush); }
			set { FullVwapBrush = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "FS Band 1", GroupName = "02. Colors", Order = 6)]
		public Brush FullBand1Brush { get; set; }

		[Browsable(false)]
		public string FullBand1BrushSerialize
		{
			get { return Serialize.BrushToString(FullBand1Brush); }
			set { FullBand1Brush = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "FS Band 2", GroupName = "02. Colors", Order = 7)]
		public Brush FullBand2Brush { get; set; }

		[Browsable(false)]
		public string FullBand2BrushSerialize
		{
			get { return Serialize.BrushToString(FullBand2Brush); }
			set { FullBand2Brush = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "FS Band 3", GroupName = "02. Colors", Order = 8)]
		public Brush FullBand3Brush { get; set; }

		[Browsable(false)]
		public string FullBand3BrushSerialize
		{
			get { return Serialize.BrushToString(FullBand3Brush); }
			set { FullBand3Brush = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "RTH VWAP", GroupName = "02. Colors", Order = 9)]
		public Brush RthVwapBrush { get; set; }

		[Browsable(false)]
		public string RthVwapBrushSerialize
		{
			get { return Serialize.BrushToString(RthVwapBrush); }
			set { RthVwapBrush = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "RTH Band 1", GroupName = "02. Colors", Order = 10)]
		public Brush RthBand1Brush { get; set; }

		[Browsable(false)]
		public string RthBand1BrushSerialize
		{
			get { return Serialize.BrushToString(RthBand1Brush); }
			set { RthBand1Brush = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "RTH Band 2", GroupName = "02. Colors", Order = 11)]
		public Brush RthBand2Brush { get; set; }

		[Browsable(false)]
		public string RthBand2BrushSerialize
		{
			get { return Serialize.BrushToString(RthBand2Brush); }
			set { RthBand2Brush = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "RTH Band 3", GroupName = "02. Colors", Order = 12)]
		public Brush RthBand3Brush { get; set; }

		[Browsable(false)]
		public string RthBand3BrushSerialize
		{
			get { return Serialize.BrushToString(RthBand3Brush); }
			set { RthBand3Brush = Serialize.StringToBrush(value); }
		}

		[Display(Name = "Weekly VWAP Style", GroupName = "04. Line Styles", Order = 1)]
		public DashStyleHelper WeeklyVwapStyle { get; set; }

		[Display(Name = "Weekly Band 1 Style", GroupName = "04. Line Styles", Order = 2)]
		public DashStyleHelper WeeklyBand1Style { get; set; }

		[Display(Name = "Weekly Band 2 Style", GroupName = "04. Line Styles", Order = 3)]
		public DashStyleHelper WeeklyBand2Style { get; set; }

		[Display(Name = "Weekly Band 3 Style", GroupName = "04. Line Styles", Order = 4)]
		public DashStyleHelper WeeklyBand3Style { get; set; }

		[Display(Name = "FS VWAP Style", GroupName = "04. Line Styles", Order = 5)]
		public DashStyleHelper FullVwapStyle { get; set; }

		[Display(Name = "FS Band 1 Style", GroupName = "04. Line Styles", Order = 6)]
		public DashStyleHelper FullBand1Style { get; set; }

		[Display(Name = "FS Band 2 Style", GroupName = "04. Line Styles", Order = 7)]
		public DashStyleHelper FullBand2Style { get; set; }

		[Display(Name = "FS Band 3 Style", GroupName = "04. Line Styles", Order = 8)]
		public DashStyleHelper FullBand3Style { get; set; }

		[Display(Name = "RTH VWAP Style", GroupName = "04. Line Styles", Order = 9)]
		public DashStyleHelper RthVwapStyle { get; set; }

		[Display(Name = "RTH Band 1 Style", GroupName = "04. Line Styles", Order = 10)]
		public DashStyleHelper RthBand1Style { get; set; }

		[Display(Name = "RTH Band 2 Style", GroupName = "04. Line Styles", Order = 11)]
		public DashStyleHelper RthBand2Style { get; set; }

		[Display(Name = "RTH Band 3 Style", GroupName = "04. Line Styles", Order = 12)]
		public DashStyleHelper RthBand3Style { get; set; }

		#endregion
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private ILT_DEV_SUITE.ILT_DEV_VWAP[] cacheILT_DEV_VWAP;
		public ILT_DEV_SUITE.ILT_DEV_VWAP ILT_DEV_VWAP(bool showToolbarButton, bool showWeekly, bool showFullSession, bool showRTH, DateTime rthStartTime, DateTime rthEndTime, bool showLabels, int labelFontSize, int weeksOfHistory, bool showWeeklyBands, bool showFullBands, bool showRthBands, double stDev1, double stDev2, double stDev3)
		{
			return ILT_DEV_VWAP(Input, showToolbarButton, showWeekly, showFullSession, showRTH, rthStartTime, rthEndTime, showLabels, labelFontSize, weeksOfHistory, showWeeklyBands, showFullBands, showRthBands, stDev1, stDev2, stDev3);
		}

		public ILT_DEV_SUITE.ILT_DEV_VWAP ILT_DEV_VWAP(ISeries<double> input, bool showToolbarButton, bool showWeekly, bool showFullSession, bool showRTH, DateTime rthStartTime, DateTime rthEndTime, bool showLabels, int labelFontSize, int weeksOfHistory, bool showWeeklyBands, bool showFullBands, bool showRthBands, double stDev1, double stDev2, double stDev3)
		{
			if (cacheILT_DEV_VWAP != null)
				for (int idx = 0; idx < cacheILT_DEV_VWAP.Length; idx++)
					if (cacheILT_DEV_VWAP[idx] != null && cacheILT_DEV_VWAP[idx].ShowToolbarButton == showToolbarButton && cacheILT_DEV_VWAP[idx].ShowWeekly == showWeekly && cacheILT_DEV_VWAP[idx].ShowFullSession == showFullSession && cacheILT_DEV_VWAP[idx].ShowRTH == showRTH && cacheILT_DEV_VWAP[idx].RthStartTime == rthStartTime && cacheILT_DEV_VWAP[idx].RthEndTime == rthEndTime && cacheILT_DEV_VWAP[idx].ShowLabels == showLabels && cacheILT_DEV_VWAP[idx].LabelFontSize == labelFontSize && cacheILT_DEV_VWAP[idx].WeeksOfHistory == weeksOfHistory && cacheILT_DEV_VWAP[idx].ShowWeeklyBands == showWeeklyBands && cacheILT_DEV_VWAP[idx].ShowFullBands == showFullBands && cacheILT_DEV_VWAP[idx].ShowRthBands == showRthBands && cacheILT_DEV_VWAP[idx].StDev1 == stDev1 && cacheILT_DEV_VWAP[idx].StDev2 == stDev2 && cacheILT_DEV_VWAP[idx].StDev3 == stDev3 && cacheILT_DEV_VWAP[idx].EqualsInput(input))
						return cacheILT_DEV_VWAP[idx];
			return CacheIndicator<ILT_DEV_SUITE.ILT_DEV_VWAP>(new ILT_DEV_SUITE.ILT_DEV_VWAP(){ ShowToolbarButton = showToolbarButton, ShowWeekly = showWeekly, ShowFullSession = showFullSession, ShowRTH = showRTH, RthStartTime = rthStartTime, RthEndTime = rthEndTime, ShowLabels = showLabels, LabelFontSize = labelFontSize, WeeksOfHistory = weeksOfHistory, ShowWeeklyBands = showWeeklyBands, ShowFullBands = showFullBands, ShowRthBands = showRthBands, StDev1 = stDev1, StDev2 = stDev2, StDev3 = stDev3 }, input, ref cacheILT_DEV_VWAP);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.ILT_DEV_SUITE.ILT_DEV_VWAP ILT_DEV_VWAP(bool showToolbarButton, bool showWeekly, bool showFullSession, bool showRTH, DateTime rthStartTime, DateTime rthEndTime, bool showLabels, int labelFontSize, int weeksOfHistory, bool showWeeklyBands, bool showFullBands, bool showRthBands, double stDev1, double stDev2, double stDev3)
		{
			return indicator.ILT_DEV_VWAP(Input, showToolbarButton, showWeekly, showFullSession, showRTH, rthStartTime, rthEndTime, showLabels, labelFontSize, weeksOfHistory, showWeeklyBands, showFullBands, showRthBands, stDev1, stDev2, stDev3);
		}

		public Indicators.ILT_DEV_SUITE.ILT_DEV_VWAP ILT_DEV_VWAP(ISeries<double> input , bool showToolbarButton, bool showWeekly, bool showFullSession, bool showRTH, DateTime rthStartTime, DateTime rthEndTime, bool showLabels, int labelFontSize, int weeksOfHistory, bool showWeeklyBands, bool showFullBands, bool showRthBands, double stDev1, double stDev2, double stDev3)
		{
			return indicator.ILT_DEV_VWAP(input, showToolbarButton, showWeekly, showFullSession, showRTH, rthStartTime, rthEndTime, showLabels, labelFontSize, weeksOfHistory, showWeeklyBands, showFullBands, showRthBands, stDev1, stDev2, stDev3);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.ILT_DEV_SUITE.ILT_DEV_VWAP ILT_DEV_VWAP(bool showToolbarButton, bool showWeekly, bool showFullSession, bool showRTH, DateTime rthStartTime, DateTime rthEndTime, bool showLabels, int labelFontSize, int weeksOfHistory, bool showWeeklyBands, bool showFullBands, bool showRthBands, double stDev1, double stDev2, double stDev3)
		{
			return indicator.ILT_DEV_VWAP(Input, showToolbarButton, showWeekly, showFullSession, showRTH, rthStartTime, rthEndTime, showLabels, labelFontSize, weeksOfHistory, showWeeklyBands, showFullBands, showRthBands, stDev1, stDev2, stDev3);
		}

		public Indicators.ILT_DEV_SUITE.ILT_DEV_VWAP ILT_DEV_VWAP(ISeries<double> input , bool showToolbarButton, bool showWeekly, bool showFullSession, bool showRTH, DateTime rthStartTime, DateTime rthEndTime, bool showLabels, int labelFontSize, int weeksOfHistory, bool showWeeklyBands, bool showFullBands, bool showRthBands, double stDev1, double stDev2, double stDev3)
		{
			return indicator.ILT_DEV_VWAP(input, showToolbarButton, showWeekly, showFullSession, showRTH, rthStartTime, rthEndTime, showLabels, labelFontSize, weeksOfHistory, showWeeklyBands, showFullBands, showRthBands, stDev1, stDev2, stDev3);
		}
	}
}

#endregion

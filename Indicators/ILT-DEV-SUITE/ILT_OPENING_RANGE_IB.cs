#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Core;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
#endregion

// ---------------------------------------------------------------------------
//  ILT OPENING RANGE + INITIAL BALANCE  -  Ali G / IronLionTrader
//
//  Dos estudios en un solo indicador con motor dinámico de Volume Profile:
//    - Opening Range (OR), con duración configurable por sesión
//    - Initial Balance (IB), con duración configurable por sesión
//    - Volume Profile (VAH, POC, VAL) calculado por ticks durante la ventana
//
//  Ambos empiezan en la apertura configurada de Asia, Europa o New York.
//  Cada rango puede mostrarse como recuadro, líneas o ambos, y prolongarse
//  hasta el cierre de su propia sesión. El cálculo usa una serie interna de
//  un minuto para no depender del tipo de vela visible en el gráfico.
//
//  El motor de tiempo asume que el gráfico está en horario de New York,
//  igual que el resto de la suite ILT.
// ---------------------------------------------------------------------------

namespace NinjaTrader.NinjaScript.Indicators.ILT_SUITE
{
	public enum IltOrIbModoVisual
	{
		Recuadro,
		Lineas,
		RecuadroYLineas
	}

	public enum IltOrIbLadoEtiqueta
	{
		Izquierda,
		Derecha,
		Personalizada
	}

	public enum IltOrIbPosVertical
	{
		Arriba,
		SobreLaLinea,
		Abajo
	}

	public class IltOrIbEtiqueta
	{
		public string	Texto;
		public float	X;
		public float	Top;
		public float	Ancho;
		public float	Alto;
		public object	Brocha;
	}

	public class IltOrIbInstancia
	{
		public int		TipoSesion;	// 0 = Asia, 1 = Europa, 2 = New York
		public int		TipoRango;	// 0 = Opening Range, 1 = Initial Balance
		public DateTime	InicioSesion;
		public DateTime	FinSesion;
		public DateTime	FinRango;
		public double	Alto;
		public double	Bajo;
		public DateTime	UltimaBarra;
		public bool		Completo;
		public bool		TieneDatos;

		// --- Motor dinámico de Volume Profile ---
		public Dictionary<long, double> VolumenPorTick = new Dictionary<long, double>();
		public double	VolumenTotal;
		public double	PocPrecio = double.NaN;
		public double	VahPrecio = double.NaN;
		public double	ValPrecio = double.NaN;
		public bool		TieneProfile;

		public void CalcularValueArea(double porcentaje, double tickSize)
		{
			if (VolumenPorTick == null || VolumenPorTick.Count == 0 || VolumenTotal <= 0.0 || tickSize <= 0.0)
				return;

			long maxTick = 0;
			double maxVol = -1.0;

			foreach (KeyValuePair<long, double> kvp in VolumenPorTick)
			{
				if (kvp.Value > maxVol)
				{
					maxVol = kvp.Value;
					maxTick = kvp.Key;
				}
			}

			if (maxVol <= 0.0)
				return;

			PocPrecio = maxTick * tickSize;

			double pct = Math.Max(0.10, Math.Min(1.0, porcentaje / 100.0));
			double targetVol = VolumenTotal * pct;
			double volAcumulado = maxVol;
			long upTick = maxTick + 1;
			long downTick = maxTick - 1;

			while (volAcumulado < targetVol && (VolumenPorTick.ContainsKey(upTick) || VolumenPorTick.ContainsKey(downTick)))
			{
				double volUp = 0.0;
				double volDown = 0.0;
				VolumenPorTick.TryGetValue(upTick, out volUp);
				VolumenPorTick.TryGetValue(downTick, out volDown);

				if (volUp >= volDown && volUp > 0.0)
				{
					volAcumulado += volUp;
					upTick++;
				}
				else if (volDown > 0.0)
				{
					volAcumulado += volDown;
					downTick--;
				}
				else
				{
					break;
				}
			}

			VahPrecio = (upTick - 1) * tickSize;
			ValPrecio = (downTick + 1) * tickSize;
			TieneProfile = true;
		}

		public void LimpiarMemoria()
		{
			if (VolumenPorTick != null)
			{
				VolumenPorTick.Clear();
				VolumenPorTick = null;
			}
		}
	}

	public class ILT_OPENING_RANGE_IB : Indicator
	{
		#region ===================== CAMPOS =====================

		private const int NumSesiones = 3;
		private const int NumRangos = 2;
		private const int SerieCalculo = 1;
		private const string ToolbarBtnTag = "ILT_OPENING_RANGE_IB_BTN";

		private readonly List<IltOrIbInstancia> instancias = new List<IltOrIbInstancia>();
		private readonly IltOrIbInstancia[,] abiertos = new IltOrIbInstancia[NumSesiones, NumRangos];

		private readonly SharpDX.Direct2D1.SolidColorBrush[,] dxLinea = new SharpDX.Direct2D1.SolidColorBrush[NumSesiones, NumRangos];
		private readonly SharpDX.Direct2D1.SolidColorBrush[,] dxCaja = new SharpDX.Direct2D1.SolidColorBrush[NumSesiones, NumRangos];
		private readonly SharpDX.Direct2D1.SolidColorBrush[,] dxCajaVA = new SharpDX.Direct2D1.SolidColorBrush[NumSesiones, NumRangos];
		private SharpDX.Direct2D1.SolidColorBrush dxPoc;
		private SharpDX.Direct2D1.SolidColorBrush dxFondo;
		private SharpDX.Direct2D1.StrokeStyle dxDash;
		private SharpDX.Direct2D1.StrokeStyle dxDot;
		private SharpDX.Direct2D1.StrokeStyle dxDashDot;
		private SharpDX.Direct2D1.StrokeStyle dxDashDotDot;
		private SharpDX.DirectWrite.TextFormat dxTexto;

		private int cacheFuente = -1;
		private readonly int[] cacheOpacidadCaja = new int[] { -1, -1 };
		private int cacheOpacidadVa = -1;
		private int cacheOpacidadFondo = -1;
		private Brush cacheFondoBrush;
		private Brush cachePocBrush;
		private readonly Brush[,] cacheColor = new Brush[NumSesiones, NumRangos];

		private Chart chartWindow;
		private NTMenuItem toolbarBtn;
		private bool toolbarBtnAdded;
		private Border wpfOuterPanel;
		private Button wpfMinimizeButton;
		private bool wpfPanelAdded;
		private const int NumBotones = 10;
		private readonly Button[] wpfBotones = new Button[NumBotones];
		private StackPanel wpfPanelContent;
		private bool wpfPanelDragging;
		private System.Windows.Point wpfDragStartPoint;
		private double wpfDragStartLeft;
		private double wpfDragStartTop;

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
				Name = "ILT OPENING RANGE + IB";
				Description = "Opening Range e Initial Balance configurables para Asia, Europa y New York con motor dinámico de Volume Profile (VAH, POC, VAL) por ticks.";
				IsOverlay = true;
				DisplayInDataBox = false;
				DrawOnPricePanel = true;
				PaintPriceMarkers = false;
				IsSuspendedWhileInactive = false;
				Calculate = Calculate.OnEachTick;
				BarsRequiredToPlot = 0;

				// 01. General
				Mostrar = true;
				DiasHistorico = 5;
				ShowToolbarButton = true;
				PanelVisible = false;
				PanelMinimizado = false;
				PanelPosicionX = 14;
				PanelPosicionY = 14;

				// 02. Asia
				MostrarAsia = true;
				AsiaInicio = new DateTime(1, 1, 1, 18, 30, 0);
				AsiaFin = new DateTime(1, 1, 1, 3, 0, 0);
				AsiaMinutosOR = 30;
				AsiaMinutosIB = 60;
				ColorAsiaOR = Brushes.Crimson;
				ColorAsiaIB = Brushes.OrangeRed;

				// 03. Europa
				MostrarEuropa = true;
				EuropaInicio = new DateTime(1, 1, 1, 3, 0, 0);
				EuropaFin = new DateTime(1, 1, 1, 9, 30, 0);
				EuropaMinutosOR = 30;
				EuropaMinutosIB = 60;
				ColorEuropaOR = Brushes.LawnGreen;
				ColorEuropaIB = Brushes.Gold;

				// 04. New York
				MostrarNewYork = true;
				NewYorkInicio = new DateTime(1, 1, 1, 9, 30, 0);
				NewYorkFin = new DateTime(1, 1, 1, 17, 0, 0);
				NewYorkMinutosOR = 30;
				NewYorkMinutosIB = 60;
				ColorNewYorkOR = Brushes.Cyan;
				ColorNewYorkIB = Brushes.DodgerBlue;

				// 05. Opening Range
				MostrarOpeningRange = true;
				ModoOpeningRange = IltOrIbModoVisual.Lineas;
				OpacidadCajaOR = 12;
				GrosorOR = 2;
				EstiloOR = DashStyleHelper.Solid;
				ExtenderOR = true;

				// 06. Initial Balance
				MostrarInitialBalance = true;
				ModoInitialBalance = IltOrIbModoVisual.Recuadro;
				OpacidadCajaIB = 9;
				GrosorIB = 2;
				EstiloIB = DashStyleHelper.Dash;
				ExtenderIB = true;

				// 07. Volume Profile (VAH, POC, VAL)
				MostrarVolumeProfile = true;
				PorcentajeValueArea = 70.0;
				MostrarPOC = true;
				GrosorPOC = 3;
				EstiloPOC = DashStyleHelper.Solid;
				ColorPOC = Brushes.Yellow;

				MostrarValueAreaLines = true;
				GrosorVA = 1;
				EstiloVA = DashStyleHelper.Dash;

				MostrarValueAreaCaja = true;
				OpacidadValueAreaCaja = 16;

				// 08. Etiquetas
				MostrarEtiquetas = true;
				IncluirPrecio = true;
				EtiquetasResumidas = false;
				LadoEtiqueta = IltOrIbLadoEtiqueta.Derecha;
				PosicionVertical = IltOrIbPosVertical.SobreLaLinea;
				PosicionEtiquetaPct = 100;
				TamanoEtiqueta = 11;
				ColorFondoEtiqueta = Brushes.Black;
				OpacidadFondoPct = 70;
			}
			else if (State == State.Configure)
			{
				// El OR y el IB se calculan con minutos reales mediante una serie secundaria de 1 minuto.
				AddDataSeries(BarsPeriodType.Minute, 1);
			}
			else if (State == State.DataLoaded)
			{
				instancias.Clear();

				for (int sesion = 0; sesion < NumSesiones; sesion++)
					for (int rango = 0; rango < NumRangos; rango++)
						abiertos[sesion, rango] = null;
			}
			else if (State == State.Historical)
			{
				if (ChartControl != null)
				{
					ChartControl.Dispatcher.InvokeAsync(() =>
					{
						LoadRuntimePanelState();

						if (ShowToolbarButton)
							BuildToolbarButton();

						if (PanelVisible)
							EnsureWpfControlPanel();
					});
				}
			}
			else if (State == State.Terminated)
			{
				if (chartWindow == null && ChartControl != null)
				{
					try { chartWindow = Window.GetWindow(ChartControl.Parent) as Chart; }
					catch { }
				}

				Chart winRef = chartWindow;
				NTMenuItem btnRef = toolbarBtn;
				Border panelRef = wpfOuterPanel;

				if (ChartControl != null)
					ChartControl.Dispatcher.InvokeAsync(() => CleanupChartUi(winRef, btnRef, panelRef));

				DisposeDxResources();

				toolbarBtn = null;
				wpfOuterPanel = null;
				wpfPanelContent = null;
				toolbarBtnAdded = false;
				wpfPanelAdded = false;
				chartWindow = null;
			}
		}

		protected override void OnBarUpdate()
		{
			if (!Mostrar || BarsInProgress != SerieCalculo || CurrentBars[SerieCalculo] < 0)
				return;

			DateTime t = Times[SerieCalculo][0];
			double alto = Highs[SerieCalculo][0];
			double bajo = Lows[SerieCalculo][0];
			double vol  = Volumes[SerieCalculo][0];
			int hhmmss = Hhmmss(t);

			for (int tipoSesion = 0; tipoSesion < NumSesiones; tipoSesion++)
			{
				int ini = Hhmmss(InicioDe(tipoSesion));
				int fin = Hhmmss(FinDe(tipoSesion));

				if (!DentroDeVentana(hhmmss, ini, fin))
				{
					CerrarRangosSesion(tipoSesion);
					continue;
				}

				DateTime inicioSesion = ClaveDeSesion(t, ini, fin);
				DateTime finSesion = FinDeSesion(inicioSesion, ini, fin);

				ProcesarRango(tipoSesion, 0, t, inicioSesion, finSesion, alto, bajo, vol);
				ProcesarRango(tipoSesion, 1, t, inicioSesion, finSesion, alto, bajo, vol);
			}
		}

		private void ProcesarRango(int tipoSesion, int tipoRango, DateTime t,
			DateTime inicioSesion, DateTime finSesion, double alto, double bajo, double volumen)
		{
			int minutos = MinutosDe(tipoSesion, tipoRango);
			DateTime finRango = inicioSesion.AddMinutes(Math.Max(1, minutos));

			if (finRango > finSesion)
				finRango = finSesion;

			IltOrIbInstancia actual = abiertos[tipoSesion, tipoRango];

			if (actual != null && actual.InicioSesion != inicioSesion)
			{
				actual.Completo = true;
				actual.LimpiarMemoria();
				abiertos[tipoSesion, tipoRango] = null;
				actual = null;
			}

			// Las barras de un minuto de NinjaTrader llevan la hora de cierre.
			if (t > finRango)
			{
				if (actual != null)
				{
					actual.Completo = true;
					actual.LimpiarMemoria();
					abiertos[tipoSesion, tipoRango] = null;
				}

				return;
			}

			if (actual == null)
			{
				actual = BuscarInstancia(tipoSesion, tipoRango, inicioSesion);

				if (actual == null)
				{
					actual = new IltOrIbInstancia
					{
						TipoSesion = tipoSesion,
						TipoRango = tipoRango,
						InicioSesion = inicioSesion,
						FinSesion = finSesion,
						FinRango = finRango,
						Alto = alto,
						Bajo = bajo,
						UltimaBarra = t,
						Completo = false,
						TieneDatos = true
					};

					instancias.Add(actual);
					PodarHistorico();
				}

				abiertos[tipoSesion, tipoRango] = actual;
			}

			if (alto > actual.Alto) actual.Alto = alto;
			if (bajo < actual.Bajo) actual.Bajo = bajo;
			actual.UltimaBarra = t;
			actual.TieneDatos = true;

			// Acumulación de Volume Profile por nivel de ticks del instrumento
			if (MostrarVolumeProfile && actual.VolumenPorTick != null && volumen > 0.0)
			{
				double tickSize = Math.Max(1e-10, TickSize);
				long lowTick = (long)Math.Round(bajo / tickSize);
				long highTick = (long)Math.Round(alto / tickSize);

				if (highTick >= lowTick)
				{
					long ticksCount = highTick - lowTick + 1;
					double volPorTick = volumen / (double)ticksCount;

					for (long tk = lowTick; tk <= highTick; tk++)
					{
						double vExistente;
						if (actual.VolumenPorTick.TryGetValue(tk, out vExistente))
							actual.VolumenPorTick[tk] = vExistente + volPorTick;
						else
							actual.VolumenPorTick[tk] = volPorTick;
					}

					actual.VolumenTotal += volumen;
					actual.CalcularValueArea(PorcentajeValueArea, tickSize);
				}
			}
		}

		private IltOrIbInstancia BuscarInstancia(int tipoSesion, int tipoRango, DateTime inicioSesion)
		{
			for (int i = instancias.Count - 1; i >= 0; i--)
			{
				IltOrIbInstancia x = instancias[i];

				if (x != null && x.TipoSesion == tipoSesion && x.TipoRango == tipoRango
					&& x.InicioSesion == inicioSesion)
					return x;
			}

			return null;
		}

		private void CerrarRangosSesion(int tipoSesion)
		{
			for (int tipoRango = 0; tipoRango < NumRangos; tipoRango++)
			{
				IltOrIbInstancia actual = abiertos[tipoSesion, tipoRango];

				if (actual == null)
					continue;

				actual.Completo = true;
				actual.LimpiarMemoria();
				abiertos[tipoSesion, tipoRango] = null;
			}
		}

		private void PodarHistorico()
		{
			int dias = Math.Max(1, DiasHistorico);
			HashSet<DateTime> vistos = new HashSet<DateTime>();

			for (int i = instancias.Count - 1; i >= 0; i--)
				vistos.Add(instancias[i].InicioSesion.Date);

			if (vistos.Count <= dias)
				return;

			List<DateTime> fechas = new List<DateTime>(vistos);
			fechas.Sort();
			DateTime corte = fechas[fechas.Count - dias];

			instancias.RemoveAll(x => x.InicioSesion.Date < corte);
		}

		#endregion

		#region ===================== CONFIGURACION POR SESION =====================

		private bool SesionActiva(int tipoSesion)
		{
			if (tipoSesion == 0) return MostrarAsia;
			if (tipoSesion == 1) return MostrarEuropa;
			return MostrarNewYork;
		}

		private bool RangoActivo(int tipoRango)
		{
			return tipoRango == 0 ? MostrarOpeningRange : MostrarInitialBalance;
		}

		private DateTime InicioDe(int tipoSesion)
		{
			if (tipoSesion == 0) return AsiaInicio;
			if (tipoSesion == 1) return EuropaInicio;
			return NewYorkInicio;
		}

		private DateTime FinDe(int tipoSesion)
		{
			if (tipoSesion == 0) return AsiaFin;
			if (tipoSesion == 1) return EuropaFin;
			return NewYorkFin;
		}

		private int MinutosDe(int tipoSesion, int tipoRango)
		{
			if (tipoSesion == 0) return tipoRango == 0 ? AsiaMinutosOR : AsiaMinutosIB;
			if (tipoSesion == 1) return tipoRango == 0 ? EuropaMinutosOR : EuropaMinutosIB;
			return tipoRango == 0 ? NewYorkMinutosOR : NewYorkMinutosIB;
		}

		private Brush ColorDe(int tipoSesion, int tipoRango)
		{
			if (tipoSesion == 0)
				return tipoRango == 0 ? (ColorAsiaOR ?? Brushes.Crimson) : (ColorAsiaIB ?? Brushes.OrangeRed);

			if (tipoSesion == 1)
				return tipoRango == 0 ? (ColorEuropaOR ?? Brushes.LawnGreen) : (ColorEuropaIB ?? Brushes.Gold);

			return tipoRango == 0 ? (ColorNewYorkOR ?? Brushes.Cyan) : (ColorNewYorkIB ?? Brushes.DodgerBlue);
		}

		private IltOrIbModoVisual ModoDe(int tipoRango)
		{
			return tipoRango == 0 ? ModoOpeningRange : ModoInitialBalance;
		}

		private int OpacidadCajaDe(int tipoRango)
		{
			return tipoRango == 0 ? OpacidadCajaOR : OpacidadCajaIB;
		}

		private int GrosorDe(int tipoRango)
		{
			return tipoRango == 0 ? GrosorOR : GrosorIB;
		}

		private DashStyleHelper EstiloDe(int tipoRango)
		{
			return tipoRango == 0 ? EstiloOR : EstiloIB;
		}

		private bool ExtenderDe(int tipoRango)
		{
			return tipoRango == 0 ? ExtenderOR : ExtenderIB;
		}

		private bool DibujaCaja(int tipoRango)
		{
			IltOrIbModoVisual modo = ModoDe(tipoRango);
			return modo == IltOrIbModoVisual.Recuadro || modo == IltOrIbModoVisual.RecuadroYLineas;
		}

		private bool DibujaLineas(int tipoRango)
		{
			IltOrIbModoVisual modo = ModoDe(tipoRango);
			return modo == IltOrIbModoVisual.Lineas || modo == IltOrIbModoVisual.RecuadroYLineas;
		}

		private string NombreSesion(int tipoSesion)
		{
			if (EtiquetasResumidas)
			{
				if (tipoSesion == 0) return "A";
				if (tipoSesion == 1) return "E";
				return "NY";
			}

			if (tipoSesion == 0) return "ASIA";
			if (tipoSesion == 1) return "EUROPA";
			return "NY";
		}

		private string EtiquetaDe(int tipoSesion, int tipoRango, string extremo)
		{
			string rango = tipoRango == 0 ? "OR" : "IB";

			if (EtiquetasResumidas)
				return NombreSesion(tipoSesion) + rango + "_" + extremo;

			return NombreSesion(tipoSesion) + " " + rango + " " + extremo;
		}

		private static int Hhmmss(DateTime t)
		{
			return t.Hour * 10000 + t.Minute * 100 + t.Second;
		}

		private static bool DentroDeVentana(int hhmmss, int ini, int fin)
		{
			if (ini == fin)
				return false;

			if (ini < fin)
				return hhmmss >= ini && hhmmss < fin;

			return hhmmss >= ini || hhmmss < fin;
		}

		private static DateTime ClaveDeSesion(DateTime t, int ini, int fin)
		{
			DateTime baseDia = t.Date.AddHours(ini / 10000)
				.AddMinutes((ini / 100) % 100)
				.AddSeconds(ini % 100);

			if (ini > fin && Hhmmss(t) < fin)
				baseDia = baseDia.AddDays(-1);

			return baseDia;
		}

		private static DateTime FinDeSesion(DateTime inicio, int ini, int fin)
		{
			DateTime f = inicio.Date.AddHours(fin / 10000)
				.AddMinutes((fin / 100) % 100)
				.AddSeconds(fin % 100);

			if (ini > fin)
				f = f.AddDays(1);

			return f;
		}

		#endregion

		#region ===================== DIBUJO =====================

		public override void OnRenderTargetChanged()
		{
			DisposeDxResources();

			if (RenderTarget != null)
				CreateDxResources();
		}

		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			base.OnRender(chartControl, chartScale);

			if (!Mostrar || chartControl == null || chartScale == null || RenderTarget == null || ChartPanel == null)
				return;

			if (instancias.Count == 0)
				return;

			CreateDxResources();

			DateTime desde, hasta;
			RangoVisible(out desde, out hasta);

			float limIzq = (float)ChartPanel.X;
			float limDer = (float)(ChartPanel.X + ChartPanel.W);
			float altoEtiqueta = TamanoEtiqueta + 6f;
			List<IltOrIbEtiqueta> pendientes = new List<IltOrIbEtiqueta>();

			// Pasada 0: Rellenos de cajas de rango y Value Area.
			// Pasada 1: Líneas (High/Low, VAH, POC, VAL) y Etiquetas.
			for (int pasada = 0; pasada < 2; pasada++)
			{
				for (int i = 0; i < instancias.Count; i++)
				{
					IltOrIbInstancia r = instancias[i];

					if (!r.TieneDatos || !SesionActiva(r.TipoSesion) || !RangoActivo(r.TipoRango))
						continue;

					DateTime finDatos = r.Completo ? r.FinRango : r.UltimaBarra;
					DateTime finVisual = ExtenderDe(r.TipoRango) ? r.FinSesion : finDatos;

					if (finVisual < desde || r.InicioSesion > hasta)
						continue;

					float x1Real = (float)chartControl.GetXByTime(r.InicioSesion);
					float x2Real = (float)chartControl.GetXByTime(finVisual);
					float x1 = Math.Max(x1Real, limIzq);
					float x2 = Math.Min(x2Real, limDer);

					if (x2 - x1 < 1f)
						continue;

					float yAlto = (float)chartScale.GetYByValue(r.Alto);
					float yBajo = (float)chartScale.GetYByValue(r.Bajo);

					// ── PASADA 0: RELLENOS ──
					if (pasada == 0)
					{
						// 1. Recuadro exterior del rango
						if (DibujaCaja(r.TipoRango) && OpacidadCajaDe(r.TipoRango) > 0
							&& dxCaja[r.TipoSesion, r.TipoRango] != null)
						{
							float top = Math.Min(yAlto, yBajo);
							float alto = Math.Abs(yBajo - yAlto);

							if (alto >= 1f)
							{
								try
								{
									RenderTarget.FillRectangle(
										new SharpDX.RectangleF(x1, top, x2 - x1, alto),
										dxCaja[r.TipoSesion, r.TipoRango]);
								}
								catch { }
							}
						}

						// 2. Recuadro translúcido de la Value Area (VAL a VAH)
						if (MostrarVolumeProfile && MostrarValueAreaCaja && r.TieneProfile
							&& !double.IsNaN(r.VahPrecio) && !double.IsNaN(r.ValPrecio)
							&& dxCajaVA[r.TipoSesion, r.TipoRango] != null)
						{
							float yVah = (float)chartScale.GetYByValue(r.VahPrecio);
							float yVal = (float)chartScale.GetYByValue(r.ValPrecio);
							float topVa = Math.Min(yVah, yVal);
							float altoVa = Math.Abs(yVal - yVah);

							if (altoVa >= 1f)
							{
								try
								{
									RenderTarget.FillRectangle(
										new SharpDX.RectangleF(x1, topVa, x2 - x1, altoVa),
										dxCajaVA[r.TipoSesion, r.TipoRango]);
								}
								catch { }
							}
						}

						continue;
					}

					// ── PASADA 1: LÍNEAS Y ETIQUETAS ──
					SharpDX.Direct2D1.SolidColorBrush br = dxLinea[r.TipoSesion, r.TipoRango];
					if (br == null)
						continue;

					// Líneas High y Low del rango
					if (DibujaLineas(r.TipoRango))
					{
						DibujarLinea(x1, x2, yAlto, br, GrosorDe(r.TipoRango), EstiloDe(r.TipoRango));
						DibujarLinea(x1, x2, yBajo, br, GrosorDe(r.TipoRango), EstiloDe(r.TipoRango));
					}

					// Líneas y niveles de Volume Profile (VAH, POC, VAL)
					if (MostrarVolumeProfile && r.TieneProfile)
					{
						// Value Area High (VAH)
						if (MostrarValueAreaLines && !double.IsNaN(r.VahPrecio))
						{
							float yVah = (float)chartScale.GetYByValue(r.VahPrecio);
							DibujarLinea(x1, x2, yVah, br, GrosorVA, EstiloVA);
						}

						// Point of Control (POC) con resaltado diferencial
						if (MostrarPOC && !double.IsNaN(r.PocPrecio))
						{
							float yPoc = (float)chartScale.GetYByValue(r.PocPrecio);
							SharpDX.Direct2D1.SolidColorBrush brPoc = dxPoc ?? br;
							DibujarLinea(x1, x2, yPoc, brPoc, GrosorPOC, EstiloPOC);
						}

						// Value Area Low (VAL)
						if (MostrarValueAreaLines && !double.IsNaN(r.ValPrecio))
						{
							float yVal = (float)chartScale.GetYByValue(r.ValPrecio);
							DibujarLinea(x1, x2, yVal, br, GrosorVA, EstiloVA);
						}
					}

					// Etiquetas
					if (MostrarEtiquetas && dxTexto != null)
					{
						Apuntar(pendientes, EtiquetaDe(r.TipoSesion, r.TipoRango, "H"), r.Alto,
							x1Real, x2Real, yAlto, altoEtiqueta, br);
						Apuntar(pendientes, EtiquetaDe(r.TipoSesion, r.TipoRango, "L"), r.Bajo,
							x1Real, x2Real, yBajo, altoEtiqueta, br);

						if (MostrarVolumeProfile && r.TieneProfile)
						{
							if (MostrarValueAreaLines && !double.IsNaN(r.VahPrecio))
							{
								float yVah = (float)chartScale.GetYByValue(r.VahPrecio);
								Apuntar(pendientes, EtiquetaDe(r.TipoSesion, r.TipoRango, "VAH"), r.VahPrecio,
									x1Real, x2Real, yVah, altoEtiqueta, br);
							}

							if (MostrarPOC && !double.IsNaN(r.PocPrecio))
							{
								float yPoc = (float)chartScale.GetYByValue(r.PocPrecio);
								SharpDX.Direct2D1.SolidColorBrush brPoc = dxPoc ?? br;
								Apuntar(pendientes, EtiquetaDe(r.TipoSesion, r.TipoRango, "POC"), r.PocPrecio,
									x1Real, x2Real, yPoc, altoEtiqueta, brPoc);
							}

							if (MostrarValueAreaLines && !double.IsNaN(r.ValPrecio))
							{
								float yVal = (float)chartScale.GetYByValue(r.ValPrecio);
								Apuntar(pendientes, EtiquetaDe(r.TipoSesion, r.TipoRango, "VAL"), r.ValPrecio,
									x1Real, x2Real, yVal, altoEtiqueta, br);
							}
						}
					}
				}
			}

			DibujarEtiquetas(pendientes);
		}

		private void Apuntar(List<IltOrIbEtiqueta> lista, string nombre, double precio,
			float x1, float x2, float y, float alto, SharpDX.Direct2D1.SolidColorBrush br)
		{
			if (float.IsNaN(y) || float.IsInfinity(y))
				return;

			string texto = IncluirPrecio ? nombre + " " + FormatearPrecio(precio) : nombre;
			float ancho = Math.Max(34f, texto.Length * TamanoEtiqueta * 0.62f + 8f);
			float sep = 4f;
			float limIzq = (float)ChartPanel.X;
			float limDer = (float)(ChartPanel.X + ChartPanel.W);
			float visIzq = Math.Max(x1, limIzq);
			float visDer = Math.Min(x2, limDer);

			if (visDer <= limIzq || visIzq >= limDer)
				return;

			float x;

			switch (LadoEtiqueta)
			{
				case IltOrIbLadoEtiqueta.Derecha:
					x = visDer - ancho - sep;
					break;

				case IltOrIbLadoEtiqueta.Personalizada:
					float pct = (float)Math.Max(0, Math.Min(100, PosicionEtiquetaPct)) / 100f;
					x = x1 + (x2 - x1) * pct;
					break;

				default:
					x = visIzq + sep;
					break;
			}

			if (x < limIzq + 2f) x = limIzq + 2f;
			if (x + ancho > limDer - 2f) x = limDer - ancho - 2f;

			float top;

			switch (PosicionVertical)
			{
				case IltOrIbPosVertical.Arriba: top = y - alto - 2f; break;
				case IltOrIbPosVertical.Abajo: top = y + 2f; break;
				default: top = y - alto / 2f; break;
			}

			lista.Add(new IltOrIbEtiqueta
			{
				Texto = texto,
				X = x,
				Top = top,
				Ancho = ancho,
				Alto = alto,
				Brocha = br
			});
		}

		private void DibujarEtiquetas(List<IltOrIbEtiqueta> lista)
		{
			if (lista.Count == 0)
				return;

			lista.Sort(delegate(IltOrIbEtiqueta a, IltOrIbEtiqueta b)
			{
				return a.Top.CompareTo(b.Top);
			});

			List<IltOrIbEtiqueta> colocadas = new List<IltOrIbEtiqueta>();

			for (int i = 0; i < lista.Count; i++)
			{
				IltOrIbEtiqueta e = lista[i];
				float limIzq = (float)ChartPanel.X;
				float margen = e.Alto * 2f;
				float topOrig = e.Top;

				while (Choca(e, colocadas) && e.Top - topOrig <= margen)
					e.Top += 2f;

				if (Choca(e, colocadas))
				{
					e.Top = topOrig;

					for (int intento = 0; intento < 3 && Choca(e, colocadas); intento++)
					{
						float nuevo = e.X - (e.Ancho + 4f);

						if (nuevo < limIzq + 2f)
							break;

						e.X = nuevo;
					}
				}

				colocadas.Add(e);

				try
				{
					if (OpacidadFondoPct > 0 && dxFondo != null)
					{
						const float pad = 3f;
						RenderTarget.FillRectangle(
							new SharpDX.RectangleF(e.X - pad, e.Top - 1f, e.Ancho + pad, e.Alto + 2f), dxFondo);
					}

					RenderTarget.DrawText(e.Texto, dxTexto,
						new SharpDX.RectangleF(e.X, e.Top, e.Ancho, e.Alto),
						(SharpDX.Direct2D1.SolidColorBrush)e.Brocha);
				}
				catch { }
			}
		}

		private static bool Choca(IltOrIbEtiqueta e, List<IltOrIbEtiqueta> colocadas)
		{
			for (int i = 0; i < colocadas.Count; i++)
			{
				IltOrIbEtiqueta o = colocadas[i];
				bool cruzaX = e.X < o.X + o.Ancho && o.X < e.X + e.Ancho;
				bool cruzaY = e.Top < o.Top + o.Alto && o.Top < e.Top + e.Alto;

				if (cruzaX && cruzaY)
					return true;
			}

			return false;
		}

		private void DibujarLinea(float x1, float x2, float y,
			SharpDX.Direct2D1.SolidColorBrush br, int grosor, DashStyleHelper estilo)
		{
			if (x2 - x1 < 1f || float.IsNaN(y) || float.IsInfinity(y))
				return;

			try
			{
				RenderTarget.DrawLine(new SharpDX.Vector2(x1, y), new SharpDX.Vector2(x2, y),
					br, Math.Max(1, grosor), EstiloDx(estilo));
			}
			catch { }
		}

		private string FormatearPrecio(double p)
		{
			try
			{
				if (Instrument != null && Instrument.MasterInstrument != null)
					return Instrument.MasterInstrument.FormatPrice(p);
			}
			catch { }

			return p.ToString("0.##", CultureInfo.InvariantCulture);
		}

		private void RangoVisible(out DateTime desde, out DateTime hasta)
		{
			desde = DateTime.MinValue;
			hasta = DateTime.MaxValue;

			try
			{
				if (ChartBars == null || BarsArray == null || BarsArray.Length == 0 || BarsArray[0] == null)
					return;

				int a = Math.Max(0, ChartBars.FromIndex);
				int b = Math.Min(BarsArray[0].Count - 1, ChartBars.ToIndex);

				if (b < a)
					return;

				desde = BarsArray[0].GetTime(a).AddMinutes(-10);
				hasta = BarsArray[0].GetTime(b).AddDays(2);
			}
			catch
			{
				desde = DateTime.MinValue;
				hasta = DateTime.MaxValue;
			}
		}

		#endregion

		#region ===================== RECURSOS SHARPDX =====================

		private void CreateDxResources()
		{
			if (RenderTarget == null)
				return;

			for (int tipoSesion = 0; tipoSesion < NumSesiones; tipoSesion++)
			{
				for (int tipoRango = 0; tipoRango < NumRangos; tipoRango++)
				{
					Brush c = ColorDe(tipoSesion, tipoRango);

					if (dxLinea[tipoSesion, tipoRango] == null
						|| !object.ReferenceEquals(cacheColor[tipoSesion, tipoRango], c))
					{
						if (dxLinea[tipoSesion, tipoRango] != null)
						{
							dxLinea[tipoSesion, tipoRango].Dispose();
							dxLinea[tipoSesion, tipoRango] = null;
						}

						dxLinea[tipoSesion, tipoRango] = new SharpDX.Direct2D1.SolidColorBrush(
							RenderTarget, ToDxColor(c, 1.0));
						cacheColor[tipoSesion, tipoRango] = c;

						if (dxCaja[tipoSesion, tipoRango] != null)
						{
							dxCaja[tipoSesion, tipoRango].Dispose();
							dxCaja[tipoSesion, tipoRango] = null;
						}

						if (dxCajaVA[tipoSesion, tipoRango] != null)
						{
							dxCajaVA[tipoSesion, tipoRango].Dispose();
							dxCajaVA[tipoSesion, tipoRango] = null;
						}
					}

					int opacidad = OpacidadCajaDe(tipoRango);

					if (dxCaja[tipoSesion, tipoRango] == null || cacheOpacidadCaja[tipoRango] != opacidad)
					{
						if (dxCaja[tipoSesion, tipoRango] != null)
						{
							dxCaja[tipoSesion, tipoRango].Dispose();
							dxCaja[tipoSesion, tipoRango] = null;
						}

						double op = Math.Max(0, Math.Min(100, opacidad)) / 100.0;
						dxCaja[tipoSesion, tipoRango] = new SharpDX.Direct2D1.SolidColorBrush(
							RenderTarget, ToDxColor(c, op));
					}

					// Caja de Value Area
					if (dxCajaVA[tipoSesion, tipoRango] == null || cacheOpacidadVa != OpacidadValueAreaCaja)
					{
						if (dxCajaVA[tipoSesion, tipoRango] != null)
						{
							dxCajaVA[tipoSesion, tipoRango].Dispose();
							dxCajaVA[tipoSesion, tipoRango] = null;
						}

						double opVa = Math.Max(0, Math.Min(100, OpacidadValueAreaCaja)) / 100.0;
						dxCajaVA[tipoSesion, tipoRango] = new SharpDX.Direct2D1.SolidColorBrush(
							RenderTarget, ToDxColor(c, opVa));
					}
				}
			}

			cacheOpacidadCaja[0] = OpacidadCajaOR;
			cacheOpacidadCaja[1] = OpacidadCajaIB;
			cacheOpacidadVa = OpacidadValueAreaCaja;

			// Brocha específica para POC
			Brush pocBr = ColorPOC ?? Brushes.Yellow;
			if (dxPoc == null || !object.ReferenceEquals(cachePocBrush, pocBr))
			{
				if (dxPoc != null) { dxPoc.Dispose(); dxPoc = null; }
				dxPoc = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToDxColor(pocBr, 1.0));
				cachePocBrush = pocBr;
			}

			if (dxFondo == null || cacheOpacidadFondo != OpacidadFondoPct
				|| !object.ReferenceEquals(cacheFondoBrush, ColorFondoEtiqueta))
			{
				if (dxFondo != null)
				{
					dxFondo.Dispose();
					dxFondo = null;
				}

				double op = Math.Max(0, Math.Min(100, OpacidadFondoPct)) / 100.0;
				dxFondo = new SharpDX.Direct2D1.SolidColorBrush(
					RenderTarget, ToDxColor(ColorFondoEtiqueta, op));
				cacheOpacidadFondo = OpacidadFondoPct;
				cacheFondoBrush = ColorFondoEtiqueta;
			}

			if (dxTexto == null || cacheFuente != TamanoEtiqueta)
			{
				if (dxTexto != null)
				{
					dxTexto.Dispose();
					dxTexto = null;
				}

				dxTexto = new SharpDX.DirectWrite.TextFormat(Globals.DirectWriteFactory, "Arial",
					SharpDX.DirectWrite.FontWeight.Bold,
					SharpDX.DirectWrite.FontStyle.Normal,
					SharpDX.DirectWrite.FontStretch.Normal,
					Math.Max(6, TamanoEtiqueta));

				cacheFuente = TamanoEtiqueta;
			}

			if (dxDash == null)
			{
				dxDash = CrearEstilo(SharpDX.Direct2D1.DashStyle.Dash);
				dxDot = CrearEstilo(SharpDX.Direct2D1.DashStyle.Dot);
				dxDashDot = CrearEstilo(SharpDX.Direct2D1.DashStyle.DashDot);
				dxDashDotDot = CrearEstilo(SharpDX.Direct2D1.DashStyle.DashDotDot);
			}
		}

		private SharpDX.Direct2D1.StrokeStyle CrearEstilo(SharpDX.Direct2D1.DashStyle e)
		{
			return new SharpDX.Direct2D1.StrokeStyle(Globals.D2DFactory,
				new SharpDX.Direct2D1.StrokeStyleProperties { DashStyle = e });
		}

		private SharpDX.Direct2D1.StrokeStyle EstiloDx(DashStyleHelper e)
		{
			switch (e)
			{
				case DashStyleHelper.Dash: return dxDash;
				case DashStyleHelper.DashDot: return dxDashDot;
				case DashStyleHelper.DashDotDot: return dxDashDotDot;
				case DashStyleHelper.Dot: return dxDot;
				default: return null;
			}
		}

		private SharpDX.Color ToDxColor(Brush brush, double opacidad)
		{
			SolidColorBrush scb = brush as SolidColorBrush;

			if (scb == null)
				return new SharpDX.Color((byte)255, (byte)255, (byte)255, (byte)(255 * opacidad));

			System.Windows.Media.Color c = scb.Color;

			return new SharpDX.Color(c.R, c.G, c.B,
				(byte)(c.A * Math.Max(0, Math.Min(1, opacidad))));
		}

		private void DisposeDxResources()
		{
			for (int tipoSesion = 0; tipoSesion < NumSesiones; tipoSesion++)
			{
				for (int tipoRango = 0; tipoRango < NumRangos; tipoRango++)
				{
					if (dxLinea[tipoSesion, tipoRango] != null)
					{
						dxLinea[tipoSesion, tipoRango].Dispose();
						dxLinea[tipoSesion, tipoRango] = null;
					}

					if (dxCaja[tipoSesion, tipoRango] != null)
					{
						dxCaja[tipoSesion, tipoRango].Dispose();
						dxCaja[tipoSesion, tipoRango] = null;
					}

					if (dxCajaVA[tipoSesion, tipoRango] != null)
					{
						dxCajaVA[tipoSesion, tipoRango].Dispose();
						dxCajaVA[tipoSesion, tipoRango] = null;
					}

					cacheColor[tipoSesion, tipoRango] = null;
				}
			}

			if (dxPoc != null) { dxPoc.Dispose(); dxPoc = null; }
			if (dxFondo != null) { dxFondo.Dispose(); dxFondo = null; }
			if (dxDash != null) { dxDash.Dispose(); dxDash = null; }
			if (dxDot != null) { dxDot.Dispose(); dxDot = null; }
			if (dxDashDot != null) { dxDashDot.Dispose(); dxDashDot = null; }
			if (dxDashDotDot != null) { dxDashDotDot.Dispose(); dxDashDotDot = null; }
			if (dxTexto != null) { dxTexto.Dispose(); dxTexto = null; }

			cacheFuente = -1;
			cacheOpacidadCaja[0] = -1;
			cacheOpacidadCaja[1] = -1;
			cacheOpacidadVa = -1;
			cacheOpacidadFondo = -1;
			cacheFondoBrush = null;
			cachePocBrush = null;
		}

		#endregion

		#region ===================== BOTON TOOLBAR + PANEL WPF =====================

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

				toolbarBtn = new NTMenuItem();
				toolbarBtn.Header = "ILT OR + IB";
				toolbarBtn.Style = Application.Current.TryFindResource("MainMenuItem") as Style;
				toolbarBtn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 90, 190, 255));
				toolbarBtn.ToolTip = "Mostrar / ocultar panel ILT OPENING RANGE + IB";
				toolbarBtn.Tag = ToolbarBtnTag;
				toolbarBtn.Click += ToolbarBtn_Click;

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
				PanelVisible = true;
				EnsureWpfControlPanel();
				SaveRuntimePanelState();
				return;
			}

			PanelVisible = !PanelVisible;
			ApplyPanelVisibilityFromToolbar();
			SaveRuntimePanelState();
		}

		private void ApplyPanelVisibilityFromToolbar()
		{
			if (wpfOuterPanel == null)
				return;

			wpfOuterPanel.Visibility = PanelVisible ? Visibility.Visible : Visibility.Collapsed;
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
					CornerRadius = new CornerRadius(4),
					BorderThickness = new Thickness(1),
					BorderBrush = PanelBrush(220, 44, 170, 215),
					Background = PanelBrush(232, 13, 16, 22),
					Padding = new Thickness(0),
					HorizontalAlignment = HorizontalAlignment.Left,
					VerticalAlignment = VerticalAlignment.Top,
					Margin = new Thickness(Math.Max(0, PanelPosicionX), Math.Max(0, PanelPosicionY), 0, 0),
					Width = 276
				};

				outer.Child = BuildPanelInner();
				UserControlCollection.Add(outer);

				wpfOuterPanel = outer;
				wpfPanelAdded = true;
				ApplyPanelVisibilityFromToolbar();
				RestoreWpfPanelPosition();

				if (ChartControl != null)
					ChartControl.Dispatcher.InvokeAsync(() => RestoreWpfPanelPosition());
			}
			catch (Exception ex)
			{
				try { Print("ILT OR + IB panel add error: " + ex.Message); }
				catch { }

				wpfOuterPanel = null;
				wpfPanelContent = null;
				wpfPanelAdded = false;
			}
		}

		private Grid BuildPanelInner()
		{
			Grid root = new Grid();

			root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

			Grid header = new Grid
			{
				Height = 24,
				Background = PanelBrush(190, 20, 105, 145),
				Cursor = Cursors.SizeAll
			};

			header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

			TextBlock title = new TextBlock
			{
				Text = "ILT OPENING RANGE + IB",
				FontFamily = new FontFamily("Consolas"),
				FontSize = 11,
				FontWeight = FontWeights.Bold,
				Foreground = Brushes.White,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(5, 1, 4, 1)
			};

			Grid.SetColumn(title, 0);
			header.Children.Add(title);

			wpfMinimizeButton = new Button
			{
				Content = PanelMinimizado ? "▶" : "▼",
				Width = 22,
				Height = 18,
				Margin = new Thickness(2),
				FontFamily = new FontFamily("Consolas"),
				FontSize = 10,
				FontWeight = FontWeights.Bold,
				Foreground = Brushes.White,
				Background = PanelBrush(190, 40, 48, 58),
				BorderBrush = PanelBrush(230, 110, 180, 210),
				BorderThickness = new Thickness(1),
				Padding = new Thickness(0),
				Focusable = false,
				Cursor = Cursors.Hand
			};

			wpfMinimizeButton.Click += (o, e) =>
			{
				PanelMinimizado = !PanelMinimizado;
				ApplyPanelMinimized();
				SaveRuntimePanelState();
				e.Handled = true;
			};

			Grid.SetColumn(wpfMinimizeButton, 1);
			header.Children.Add(wpfMinimizeButton);

			header.MouseLeftButtonDown += OnWpfPanelDragMouseDown;
			header.MouseMove += OnWpfPanelDragMouseMove;
			header.MouseLeftButtonUp += OnWpfPanelDragMouseUp;

			Grid.SetRow(header, 0);
			root.Children.Add(header);

			wpfPanelContent = new StackPanel
			{
				Orientation = Orientation.Vertical,
				Margin = new Thickness(5, 3, 5, 5)
			};

			// SESIONES
			wpfPanelContent.Children.Add(CreatePanelSectionHeader("SESIONES"));

			wpfBotones[0] = CreatePanelButton("ASIA");
			wpfBotones[0].Click += (o, e) =>
			{
				MostrarAsia = !MostrarAsia;
				UpdateWpfPanelState();
				RequestChartRefresh();
				e.Handled = true;
			};
			wpfPanelContent.Children.Add(wpfBotones[0]);

			wpfBotones[1] = CreatePanelButton("EUROPA");
			wpfBotones[1].Click += (o, e) =>
			{
				MostrarEuropa = !MostrarEuropa;
				UpdateWpfPanelState();
				RequestChartRefresh();
				e.Handled = true;
			};
			wpfPanelContent.Children.Add(wpfBotones[1]);

			wpfBotones[2] = CreatePanelButton("NEW YORK");
			wpfBotones[2].Click += (o, e) =>
			{
				MostrarNewYork = !MostrarNewYork;
				UpdateWpfPanelState();
				RequestChartRefresh();
				e.Handled = true;
			};
			wpfPanelContent.Children.Add(wpfBotones[2]);

			// RANGOS
			wpfPanelContent.Children.Add(CreatePanelSectionHeader("RANGOS"));

			wpfBotones[3] = CreatePanelButton("OPENING RANGE");
			wpfBotones[3].Click += (o, e) =>
			{
				MostrarOpeningRange = !MostrarOpeningRange;
				UpdateWpfPanelState();
				RequestChartRefresh();
				e.Handled = true;
			};
			wpfPanelContent.Children.Add(wpfBotones[3]);

			wpfBotones[4] = CreatePanelButton("INITIAL BALANCE");
			wpfBotones[4].Click += (o, e) =>
			{
				MostrarInitialBalance = !MostrarInitialBalance;
				UpdateWpfPanelState();
				RequestChartRefresh();
				e.Handled = true;
			};
			wpfPanelContent.Children.Add(wpfBotones[4]);

			// VOLUME PROFILE
			wpfPanelContent.Children.Add(CreatePanelSectionHeader("VOLUME PROFILE"));

			wpfBotones[8] = CreatePanelButton("VOLUME PROFILE (VA/POC)");
			wpfBotones[8].Click += (o, e) =>
			{
				MostrarVolumeProfile = !MostrarVolumeProfile;
				UpdateWpfPanelState();
				RequestChartRefresh();
				e.Handled = true;
			};
			wpfPanelContent.Children.Add(wpfBotones[8]);

			wpfBotones[9] = CreatePanelButton("RECUADRO VALUE AREA");
			wpfBotones[9].Click += (o, e) =>
			{
				MostrarValueAreaCaja = !MostrarValueAreaCaja;
				UpdateWpfPanelState();
				RequestChartRefresh();
				e.Handled = true;
			};
			wpfPanelContent.Children.Add(wpfBotones[9]);

			// ETIQUETAS
			wpfPanelContent.Children.Add(CreatePanelSectionHeader("ETIQUETAS"));

			wpfBotones[5] = CreatePanelButton("MOSTRAR ETIQUETAS");
			wpfBotones[5].Click += (o, e) =>
			{
				MostrarEtiquetas = !MostrarEtiquetas;
				UpdateWpfPanelState();
				RequestChartRefresh();
				e.Handled = true;
			};
			wpfPanelContent.Children.Add(wpfBotones[5]);

			wpfBotones[6] = CreatePanelButton("INCLUIR EL PRECIO");
			wpfBotones[6].Click += (o, e) =>
			{
				IncluirPrecio = !IncluirPrecio;
				UpdateWpfPanelState();
				RequestChartRefresh();
				e.Handled = true;
			};
			wpfPanelContent.Children.Add(wpfBotones[6]);

			wpfBotones[7] = CreatePanelButton("ETIQUETAS RESUMIDAS");
			wpfBotones[7].Click += (o, e) =>
			{
				EtiquetasResumidas = !EtiquetasResumidas;
				UpdateWpfPanelState();
				RequestChartRefresh();
				e.Handled = true;
			};
			wpfPanelContent.Children.Add(wpfBotones[7]);

			Grid.SetRow(wpfPanelContent, 1);
			root.Children.Add(wpfPanelContent);

			UpdateWpfPanelState();
			ApplyPanelMinimized();
			return root;
		}

		private Border CreatePanelSectionHeader(string texto)
		{
			return new Border
			{
				Height = 18,
				Margin = new Thickness(0, 3, 0, 1),
				Background = PanelBrush(190, 20, 105, 145),
				Child = new TextBlock
				{
					Text = texto,
					FontFamily = new FontFamily("Consolas"),
					FontSize = 9,
					FontWeight = FontWeights.Bold,
					Foreground = Brushes.White,
					VerticalAlignment = VerticalAlignment.Center,
					Margin = new Thickness(3, 0, 2, 0)
				}
			};
		}

		private void ApplyPanelMinimized()
		{
			if (wpfMinimizeButton != null)
				wpfMinimizeButton.Content = PanelMinimizado ? "▶" : "▼";

			if (wpfPanelContent != null)
				wpfPanelContent.Visibility = PanelMinimizado ? Visibility.Collapsed : Visibility.Visible;
		}

		private Button CreatePanelButton(string texto)
		{
			return new Button
			{
				Content = texto,
				FontFamily = new FontFamily("Consolas"),
				FontSize = 11,
				FontWeight = FontWeights.Bold,
				Height = 23,
				Margin = new Thickness(2, 1, 2, 1),
				Padding = new Thickness(4, 1, 4, 1),
				Foreground = Brushes.White,
				BorderBrush = PanelBrush(255, 120, 120, 120),
				BorderThickness = new Thickness(1),
				Cursor = Cursors.Hand,
				Focusable = false
			};
		}

		private void UpdateWpfPanelState()
		{
			StylePanelButton(wpfBotones[0], MostrarAsia);
			StylePanelButton(wpfBotones[1], MostrarEuropa);
			StylePanelButton(wpfBotones[2], MostrarNewYork);
			StylePanelButton(wpfBotones[3], MostrarOpeningRange);
			StylePanelButton(wpfBotones[4], MostrarInitialBalance);
			StylePanelButton(wpfBotones[5], MostrarEtiquetas);
			StylePanelButton(wpfBotones[6], IncluirPrecio);
			StylePanelButton(wpfBotones[7], EtiquetasResumidas);
			StylePanelButton(wpfBotones[8], MostrarVolumeProfile);
			StylePanelButton(wpfBotones[9], MostrarValueAreaCaja);
		}

		private void StylePanelButton(Button b, bool activo)
		{
			if (b == null)
				return;

			b.Background = activo
				? PanelBrush(242, 51, 140, 217)
				: PanelBrush(217, 51, 51, 51);

			b.Foreground = activo
				? Brushes.White
				: PanelBrush(255, 200, 200, 200);

			b.BorderBrush = PanelBrush(255, 120, 120, 120);
		}

		private static SolidColorBrush PanelBrush(byte a, byte r, byte g, byte b)
		{
			return new SolidColorBrush(System.Windows.Media.Color.FromArgb(a, r, g, b));
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

			PanelPosicionX = Math.Max(0, Math.Min(maxLeft, PanelPosicionX));
			PanelPosicionY = Math.Max(0, Math.Min(maxTop, PanelPosicionY));
			wpfOuterPanel.Margin = new Thickness(PanelPosicionX, PanelPosicionY, 0, 0);
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

				string valor = (PanelVisible ? "1" : "0") + "|"
					+ (PanelMinimizado ? "1" : "0") + "|"
					+ PanelPosicionX.ToString("R", CultureInfo.InvariantCulture) + "|"
					+ PanelPosicionY.ToString("R", CultureInfo.InvariantCulture);

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

				object guardado = Application.Current.Properties[RuntimePanelStateKey()];
				string texto = guardado as string;

				if (string.IsNullOrEmpty(texto))
					return;

				string[] partes = texto.Split('|');

				if (partes.Length != 4)
					return;

				double x;
				double y;

				if (!double.TryParse(partes[2], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
					|| !double.TryParse(partes[3], NumberStyles.Float, CultureInfo.InvariantCulture, out y))
					return;

				PanelVisible = partes[0] == "1";
				PanelMinimizado = partes[1] == "1";
				PanelPosicionX = Math.Max(0, x);
				PanelPosicionY = Math.Max(0, y);
			}
			catch { }
		}

		private void OnWpfPanelDragMouseDown(object sender, MouseButtonEventArgs e)
		{
			if (wpfOuterPanel == null || ChartControl == null)
				return;

			wpfPanelDragging = true;
			wpfDragStartPoint = e.GetPosition(ChartControl);
			wpfDragStartLeft = wpfOuterPanel.Margin.Left;
			wpfDragStartTop = wpfOuterPanel.Margin.Top;

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
			double maxLeft = Math.Max(0, ChartControl.ActualWidth - wpfOuterPanel.ActualWidth - 2);
			double maxTop = Math.Max(0, ChartControl.ActualHeight - wpfOuterPanel.ActualHeight - 2);
			double newLeft = wpfDragStartLeft + (p.X - wpfDragStartPoint.X);
			double newTop = wpfDragStartTop + (p.Y - wpfDragStartPoint.Y);

			if (newLeft < 0) newLeft = 0; else if (newLeft > maxLeft) newLeft = maxLeft;
			if (newTop < 0) newTop = 0; else if (newTop > maxTop) newTop = maxTop;

			PanelPosicionX = newLeft;
			PanelPosicionY = newTop;
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

		[Display(Name = "Mostrar", GroupName = "01. General", Order = 1)]
		public bool Mostrar { get; set; }

		[Range(1, 400)]
		[Display(Name = "Dias de historico", Description = "Cuantos dias de rangos se dibujan hacia atras.", GroupName = "01. General", Order = 2)]
		public int DiasHistorico { get; set; }

		[Display(Name = "Boton en toolbar superior", GroupName = "01. General", Order = 3)]
		public bool ShowToolbarButton { get; set; }

		[Browsable(false)]
		public bool PanelVisible { get; set; }

		[Browsable(false)]
		public bool PanelMinimizado { get; set; }

		[Browsable(false)]
		public double PanelPosicionX { get; set; }

		[Browsable(false)]
		public double PanelPosicionY { get; set; }

		[Display(Name = "Mostrar", GroupName = "02. Asia", Order = 1)]
		public bool MostrarAsia { get; set; }

		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "Inicio", GroupName = "02. Asia", Order = 2)]
		public DateTime AsiaInicio { get; set; }

		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "Fin", GroupName = "02. Asia", Order = 3)]
		public DateTime AsiaFin { get; set; }

		[Range(1, 1440)]
		[Display(Name = "Opening Range (minutos)", GroupName = "02. Asia", Order = 4)]
		public int AsiaMinutosOR { get; set; }

		[Range(1, 1440)]
		[Display(Name = "Initial Balance (minutos)", GroupName = "02. Asia", Order = 5)]
		public int AsiaMinutosIB { get; set; }

		[XmlIgnore]
		[Display(Name = "Color Opening Range", GroupName = "02. Asia", Order = 6)]
		public Brush ColorAsiaOR { get; set; }

		[Browsable(false)]
		public string ColorAsiaORSerialize
		{
			get { return Serialize.BrushToString(ColorAsiaOR); }
			set { ColorAsiaOR = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Color Initial Balance", GroupName = "02. Asia", Order = 7)]
		public Brush ColorAsiaIB { get; set; }

		[Browsable(false)]
		public string ColorAsiaIBSerialize
		{
			get { return Serialize.BrushToString(ColorAsiaIB); }
			set { ColorAsiaIB = Serialize.StringToBrush(value); }
		}

		[Display(Name = "Mostrar", GroupName = "03. Europa", Order = 1)]
		public bool MostrarEuropa { get; set; }

		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "Inicio", GroupName = "03. Europa", Order = 2)]
		public DateTime EuropaInicio { get; set; }

		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "Fin", GroupName = "03. Europa", Order = 3)]
		public DateTime EuropaFin { get; set; }

		[Range(1, 1440)]
		[Display(Name = "Opening Range (minutos)", GroupName = "03. Europa", Order = 4)]
		public int EuropaMinutosOR { get; set; }

		[Range(1, 1440)]
		[Display(Name = "Initial Balance (minutos)", GroupName = "03. Europa", Order = 5)]
		public int EuropaMinutosIB { get; set; }

		[XmlIgnore]
		[Display(Name = "Color Opening Range", GroupName = "03. Europa", Order = 6)]
		public Brush ColorEuropaOR { get; set; }

		[Browsable(false)]
		public string ColorEuropaORSerialize
		{
			get { return Serialize.BrushToString(ColorEuropaOR); }
			set { ColorEuropaOR = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Color Initial Balance", GroupName = "03. Europa", Order = 7)]
		public Brush ColorEuropaIB { get; set; }

		[Browsable(false)]
		public string ColorEuropaIBSerialize
		{
			get { return Serialize.BrushToString(ColorEuropaIB); }
			set { ColorEuropaIB = Serialize.StringToBrush(value); }
		}

		[Display(Name = "Mostrar", GroupName = "04. New York", Order = 1)]
		public bool MostrarNewYork { get; set; }

		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "Inicio", GroupName = "04. New York", Order = 2)]
		public DateTime NewYorkInicio { get; set; }

		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "Fin", GroupName = "04. New York", Order = 3)]
		public DateTime NewYorkFin { get; set; }

		[Range(1, 1440)]
		[Display(Name = "Opening Range (minutos)", GroupName = "04. New York", Order = 4)]
		public int NewYorkMinutosOR { get; set; }

		[Range(1, 1440)]
		[Display(Name = "Initial Balance (minutos)", GroupName = "04. New York", Order = 5)]
		public int NewYorkMinutosIB { get; set; }

		[XmlIgnore]
		[Display(Name = "Color Opening Range", GroupName = "04. New York", Order = 6)]
		public Brush ColorNewYorkOR { get; set; }

		[Browsable(false)]
		public string ColorNewYorkORSerialize
		{
			get { return Serialize.BrushToString(ColorNewYorkOR); }
			set { ColorNewYorkOR = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Color Initial Balance", GroupName = "04. New York", Order = 7)]
		public Brush ColorNewYorkIB { get; set; }

		[Browsable(false)]
		public string ColorNewYorkIBSerialize
		{
			get { return Serialize.BrushToString(ColorNewYorkIB); }
			set { ColorNewYorkIB = Serialize.StringToBrush(value); }
		}

		[Display(Name = "Mostrar Opening Range", GroupName = "05. Opening Range", Order = 1)]
		public bool MostrarOpeningRange { get; set; }

		[Display(Name = "Visualizacion", Description = "Recuadro, lineas de high/low, o ambos.", GroupName = "05. Opening Range", Order = 2)]
		public IltOrIbModoVisual ModoOpeningRange { get; set; }

		[Range(0, 100)]
		[Display(Name = "Opacidad del recuadro (%)", Description = "0 = sin relleno.", GroupName = "05. Opening Range", Order = 3)]
		public int OpacidadCajaOR { get; set; }

		[Range(1, 20)]
		[Display(Name = "Grosor de lineas", GroupName = "05. Opening Range", Order = 4)]
		public int GrosorOR { get; set; }

		[Display(Name = "Estilo de lineas", GroupName = "05. Opening Range", Order = 5)]
		public DashStyleHelper EstiloOR { get; set; }

		[Display(Name = "Extender hasta fin de sesion", Description = "Extiende el recuadro o las lineas hasta el cierre configurado de la sesion.", GroupName = "05. Opening Range", Order = 6)]
		public bool ExtenderOR { get; set; }

		[Display(Name = "Mostrar Initial Balance", GroupName = "06. Initial Balance", Order = 1)]
		public bool MostrarInitialBalance { get; set; }

		[Display(Name = "Visualizacion", Description = "Recuadro, lineas de high/low, o ambos.", GroupName = "06. Initial Balance", Order = 2)]
		public IltOrIbModoVisual ModoInitialBalance { get; set; }

		[Range(0, 100)]
		[Display(Name = "Opacidad del recuadro (%)", Description = "0 = sin relleno.", GroupName = "06. Initial Balance", Order = 3)]
		public int OpacidadCajaIB { get; set; }

		[Range(1, 20)]
		[Display(Name = "Grosor de lineas", GroupName = "06. Initial Balance", Order = 4)]
		public int GrosorIB { get; set; }

		[Display(Name = "Estilo de lineas", GroupName = "06. Initial Balance", Order = 5)]
		public DashStyleHelper EstiloIB { get; set; }

		[Display(Name = "Extender hasta fin de sesion", Description = "Extiende el recuadro o las lineas hasta el cierre configurado de la sesion.", GroupName = "06. Initial Balance", Order = 6)]
		public bool ExtenderIB { get; set; }

		// 07. Volume Profile (VAH, POC, VAL)
		[Display(Name = "Mostrar Volume Profile", Description = "Calcula y proyecta VAH, POC y VAL durante y después de la ventana del rango.", GroupName = "07. Volume Profile", Order = 1)]
		public bool MostrarVolumeProfile { get; set; }

		[Range(10.0, 100.0)]
		[Display(Name = "Porcentaje Value Area (%)", Description = "Porcentaje objetivo de volumen para VAH y VAL (70% institucional).", GroupName = "07. Volume Profile", Order = 2)]
		public double PorcentajeValueArea { get; set; }

		[Display(Name = "Mostrar POC", GroupName = "07. Volume Profile", Order = 3)]
		public bool MostrarPOC { get; set; }

		[Range(1, 20)]
		[Display(Name = "Grosor POC", Description = "Resaltado visual diferencial para el Point of Control.", GroupName = "07. Volume Profile", Order = 4)]
		public int GrosorPOC { get; set; }

		[Display(Name = "Estilo POC", GroupName = "07. Volume Profile", Order = 5)]
		public DashStyleHelper EstiloPOC { get; set; }

		[XmlIgnore]
		[Display(Name = "Color POC", Description = "Color para la línea de POC (Point of Control).", GroupName = "07. Volume Profile", Order = 6)]
		public Brush ColorPOC { get; set; }

		[Browsable(false)]
		public string ColorPOCSerialize
		{
			get { return Serialize.BrushToString(ColorPOC); }
			set { ColorPOC = Serialize.StringToBrush(value); }
		}

		[Display(Name = "Mostrar Líneas Value Area", Description = "Dibuja las líneas de VAH y VAL.", GroupName = "07. Volume Profile", Order = 7)]
		public bool MostrarValueAreaLines { get; set; }

		[Range(1, 20)]
		[Display(Name = "Grosor Value Area", GroupName = "07. Volume Profile", Order = 8)]
		public int GrosorVA { get; set; }

		[Display(Name = "Estilo Value Area", GroupName = "07. Volume Profile", Order = 9)]
		public DashStyleHelper EstiloVA { get; set; }

		[Display(Name = "Mostrar Recuadro Value Area", Description = "Rellena con color translúcido el área de valor entre VAL y VAH.", GroupName = "07. Volume Profile", Order = 10)]
		public bool MostrarValueAreaCaja { get; set; }

		[Range(0, 100)]
		[Display(Name = "Opacidad Recuadro Value Area (%)", GroupName = "07. Volume Profile", Order = 11)]
		public int OpacidadValueAreaCaja { get; set; }

		// 08. Etiquetas
		[Display(Name = "Mostrar etiquetas", GroupName = "08. Etiquetas", Order = 1)]
		public bool MostrarEtiquetas { get; set; }

		[Display(Name = "Incluir el precio", GroupName = "08. Etiquetas", Order = 2)]
		public bool IncluirPrecio { get; set; }

		[Display(Name = "Etiquetas resumidas", Description = "Abreviadas: AOR_H, AOR_POC, EIB_VAH, etc.", GroupName = "08. Etiquetas", Order = 3)]
		public bool EtiquetasResumidas { get; set; }

		[Display(Name = "Lado de la etiqueta", Description = "Izquierda o Derecha del rango. Personalizada usa el porcentaje.", GroupName = "08. Etiquetas", Order = 4)]
		public IltOrIbLadoEtiqueta LadoEtiqueta { get; set; }

		[Range(0, 100)]
		[Display(Name = "Posicion horizontal (%)", Description = "Solo si el lado es Personalizada.", GroupName = "08. Etiquetas", Order = 5)]
		public int PosicionEtiquetaPct { get; set; }

		[Display(Name = "Posicion vertical", GroupName = "08. Etiquetas", Order = 6)]
		public IltOrIbPosVertical PosicionVertical { get; set; }

		[Range(6, 48)]
		[Display(Name = "Tamano del texto", GroupName = "08. Etiquetas", Order = 7)]
		public int TamanoEtiqueta { get; set; }

		[XmlIgnore]
		[Display(Name = "Color de fondo", GroupName = "08. Etiquetas", Order = 8)]
		public Brush ColorFondoEtiqueta { get; set; }

		[Browsable(false)]
		public string ColorFondoEtiquetaSerialize
		{
			get { return Serialize.BrushToString(ColorFondoEtiqueta); }
			set { ColorFondoEtiqueta = Serialize.StringToBrush(value); }
		}

		[Range(0, 100)]
		[Display(Name = "Opacidad del fondo (%)", Description = "0 = sin fondo.", GroupName = "08. Etiquetas", Order = 9)]
		public int OpacidadFondoPct { get; set; }

		#endregion
	}
}

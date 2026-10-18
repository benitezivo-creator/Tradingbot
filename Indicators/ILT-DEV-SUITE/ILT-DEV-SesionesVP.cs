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
using NinjaTrader.Cbi;
using NinjaTrader.Core;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
#endregion

// ---------------------------------------------------------------------------
//  ILT-DEV SESSIONS  -  Ali G / IronLionTrader
//
//  Marca las sesiones de Asia, Europa y New York:
//    - caja del rango de la sesion, sin contornos
//    - lineas de high y low, que pueden prolongarse hasta el cierre de NY
//    - linea del precio de apertura de la sesion
//    - etiquetas con lado y fondo configurables
//
//  El motor de tiempo asume que el grafico esta en horario de New York,
//  igual que el resto de la suite ILT-DEV.
// ---------------------------------------------------------------------------

public enum IltDevSesionVPLadoEtiqueta
{
	Izquierda,
	Derecha,
	Personalizada
}

public enum IltDevSesionVPPosVertical
{
	Arriba,
	SobreLaLinea,
	Abajo
}

/// Etiqueta pendiente de dibujar, para poder resolver choques antes de pintarla.
public class IltDevSesionVPEtiqueta
{
	public string	Texto;
	public float	X;
	public float	Top;
	public float	Ancho;
	public float	Alto;
	public object	Brocha;
}

/// Una aparicion concreta de una sesion en un dia concreto.
public class IltDevSesionVPInstancia
{
	public int		Tipo;			// 0 = Asia, 1 = Europa, 2 = New York
	public DateTime	Inicio;
	public DateTime	Fin;
	public DateTime	FinExtension;	// cierre de NY del mismo dia de negociacion
	public double	Apertura;
	public double	Alto;
	public double	Bajo;
	public DateTime	UltimaBarra;	// hora de la ultima vela que cayo dentro de la sesion
	public bool		Cerrada;
	public bool		TieneDatos;
	public readonly SortedDictionary<double, double> VolumenPrecio = new SortedDictionary<double, double>();
	public readonly SortedDictionary<double, double> VolumenOR = new SortedDictionary<double, double>();
	public double VAH, POC, VAL, ORVAH, ORPOC, ORVAL;
	public double VolumenTotal, VolumenORTotal;
}

namespace NinjaTrader.NinjaScript.Indicators.ILT_DEV_SUITE
{
	public class ILT_DEV_SesionesVP : Indicator
	{
		#region ===================== CAMPOS =====================

		private const int	NumSesiones		= 3;
		private const string ToolbarBtnTag	= "ILT_DEV_SesionesVP_BTN";
		private const double ValueAreaPercent = 70.0;
		private const int OpeningRangeMinutes = 15;
		private bool tickReplayOn;
		private bool marketDataActive;
		private double lastTickPrice;
		private double lastTradePrice;
		private int lastTradeDirection = 1;
		private double currentBid, currentAsk;

		private readonly List<IltDevSesionVPInstancia>	instancias	= new List<IltDevSesionVPInstancia>();
		private readonly IltDevSesionVPInstancia[]		abiertas	= new IltDevSesionVPInstancia[NumSesiones];

		private SharpDX.Direct2D1.SolidColorBrush[]	dxLinea		= new SharpDX.Direct2D1.SolidColorBrush[NumSesiones];
		private SharpDX.Direct2D1.SolidColorBrush[]	dxCaja		= new SharpDX.Direct2D1.SolidColorBrush[NumSesiones];
		private SharpDX.Direct2D1.SolidColorBrush	dxFondo;
		private SharpDX.Direct2D1.SolidColorBrush dxVah, dxPoc, dxVal;
		private SharpDX.Direct2D1.StrokeStyle		dxDash;
		private SharpDX.Direct2D1.StrokeStyle		dxDot;
		private SharpDX.Direct2D1.StrokeStyle		dxDashDot;
		private SharpDX.Direct2D1.StrokeStyle		dxDashDotDot;
		private SharpDX.DirectWrite.TextFormat		dxTexto;

		private int		cacheFuente			= -1;
		private int		cacheOpacidadCaja	= -1;
		private int		cacheOpacidadFondo	= -1;
		private Brush	cacheFondoBrush;
		private readonly Brush[] cacheColor		= new Brush[NumSesiones];

		private Chart		chartWindow;
		private NTMenuItem	toolbarBtn;
		private bool		toolbarBtnAdded;
		private Border		wpfOuterPanel;
		private Button		wpfMinimizeButton;
		private bool		wpfPanelMinimized;
		private bool		wpfPanelAdded;
		private bool		panelUserHidden = true;
		private const int	NumBotones		= 4;	// ASIA, EUROPA, NEW YORK, OPENING
		private Button[]	wpfBotones		= new Button[NumBotones];
		private bool		wpfPanelDragging;
		private System.Windows.Point		wpfDragStartPoint;
		private double		wpfDragStartLeft;
		private double		wpfDragStartTop;

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
				Name = "ILT-DEV-SesionesVP";
				Description					= "ILT-DEV-SesionesVP: marca Asia, Europa y New York con caja de rango, lineas de high y low prolongables hasta el cierre de NY, y linea del precio de apertura. Horario de cada sesion configurable. El grafico debe estar en horario de New York.";
				IsOverlay					= true;
				DisplayInDataBox			= false;
				DrawOnPricePanel			= true;
				PaintPriceMarkers			= false;
				IsSuspendedWhileInactive	= false;
				Calculate					= Calculate.OnEachTick;

				// 01. General
				Mostrar				= true;
				DiasHistorico		= 5;
				ShowToolbarButton	= true;

				// 02-04. Sesiones (horarios y colores de la suite ILT-DEV)
				MostrarAsia			= true;
				AsiaInicio			= new DateTime(1, 1, 1, 18, 30, 0);
				AsiaFin				= new DateTime(1, 1, 1,  3,  0, 0);
				ColorAsia			= Brushes.Crimson;

				MostrarEuropa		= true;
				EuropaInicio		= new DateTime(1, 1, 1,  3,  0, 0);
				EuropaFin			= new DateTime(1, 1, 1,  9, 30, 0);
				ColorEuropa			= Brushes.LawnGreen;

				MostrarNewYork		= true;
				NewYorkInicio		= new DateTime(1, 1, 1,  9, 30, 0);
				NewYorkFin			= new DateTime(1, 1, 1, 17,  0, 0);
				ColorNewYork		= Brushes.Cyan;

				// 05. Caja
				MostrarCaja			= true;
				OpacidadCaja		= 12;

				// 06. Lineas high / low
				MostrarHighLow		= true;
				GrosorHighLow		= 2;
				EstiloHighLow		= DashStyleHelper.Solid;
				ExtenderHighLow		= true;

				// 07. Linea de apertura
				MostrarApertura		= true;
				GrosorApertura		= 1;
				EstiloApertura		= DashStyleHelper.Dash;
				ExtenderApertura	= true;

				// 08. Etiquetas
				MostrarEtiquetas	= true;
				IncluirPrecio		= true;
				EtiquetasResumidas	= false;
				LadoEtiqueta		= IltDevSesionVPLadoEtiqueta.Derecha;
				PosicionVertical	= IltDevSesionVPPosVertical.SobreLaLinea;
				PosicionEtiquetaPct	= 100;
				TamanoEtiqueta		= 11;
				ColorFondoEtiqueta	= Brushes.Black;
				OpacidadFondoPct	= 70;
			}
			else if (State == State.DataLoaded)
			{
				instancias.Clear();

				for (int i = 0; i < NumSesiones; i++)
					abiertas[i] = null;
				try { tickReplayOn = BarsArray != null && BarsArray[0] != null && BarsArray[0].IsTickReplay; }
				catch { tickReplayOn = false; }
			}
			else if (State == State.Configure)
			{
				// Igual que ILT-DEV-PROFILES: ticks para volumen y 1 minuto
				// para reloj/límites exactos, independiente del gráfico Range 40.
				AddDataSeries(BarsPeriodType.Tick, 1);
				AddDataSeries(BarsPeriodType.Minute, 1);
			}
			else if (State == State.Historical)
			{
				if (ChartControl != null && ShowToolbarButton)
					ChartControl.Dispatcher.InvokeAsync(() => BuildToolbarButton());
			}
			else if (State == State.Terminated)
			{
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

		protected override void OnBarUpdate()
		{
			if (BarsInProgress == 1) { AccumulateTickSeries(); return; }
			if (BarsInProgress == 2) { UpdateSessionClock(Times[2][0], Opens[2][0], Highs[2][0], Lows[2][0]); return; }
			if (BarsInProgress == 0 && Mostrar && CurrentBar >= 0)
				UpdateSessionClock(Time[0], Open[0], High[0], Low[0]);
		}

		private void UpdateSessionClock(DateTime t, double op, double hi, double lo)
		{
			for (int tipo = 0; tipo < NumSesiones; tipo++)
			{
				int hhmmss = Hhmmss(t), ini = Hhmmss(InicioDe(tipo)), fin = Hhmmss(FinDe(tipo));
				if (!SesionActiva(tipo) || !DentroDeVentana(hhmmss, ini, fin)) { CerrarSiAbierta(tipo); continue; }
				DateTime clave = ClaveDeSesion(t, ini, fin);
				IltDevSesionVPInstancia actual = abiertas[tipo];
				if (actual == null || actual.Inicio != clave)
				{
					CerrarSiAbierta(tipo);
					actual = new IltDevSesionVPInstancia { Tipo=tipo, Inicio=clave, Fin=FinDeSesion(clave,ini,fin),
						FinExtension=ProximoCierreNy(clave), Apertura=op, Alto=hi, Bajo=lo, UltimaBarra=t,
						Cerrada=false, TieneDatos=true };
					instancias.Add(actual); abiertas[tipo]=actual; PodarHistorico();
				}
				else { actual.Alto=Math.Max(actual.Alto,hi); actual.Bajo=Math.Min(actual.Bajo,lo); actual.UltimaBarra=t; }
			}
		}

		private void AccumulateTickSeries()
		{
			if (tickReplayOn || marketDataActive || CurrentBars[1] < 0) return;
			AddVolumeAt(Times[1][0], Closes[1][0], Volumes[1][0]);
		}

		protected override void OnMarketData(MarketDataEventArgs e)
		{
			if (e.MarketDataType == MarketDataType.Bid) { currentBid=e.Price; return; }
			if (e.MarketDataType == MarketDataType.Ask) { currentAsk=e.Price; return; }
			if (e.MarketDataType != MarketDataType.Last) return;
			if (!tickReplayOn && State != State.Realtime) return;
			marketDataActive = true;
			AddVolumeAt(e.Time, e.Price, e.Volume);
			if (e.Price > lastTradePrice) lastTradeDirection=1;
			else if (e.Price < lastTradePrice) lastTradeDirection=-1;
			lastTradePrice=e.Price;
		}

		private void AddVolumeAt(DateTime t, double price, double volume)
		{
			if (volume <= 0) return;
			double ts = Instrument.MasterInstrument.TickSize;
			double key = Math.Round(price / ts) * ts;
			for (int tipo=0; tipo<NumSesiones; tipo++)
			{
				if (!SesionActiva(tipo)) continue;
				int ini=Hhmmss(InicioDe(tipo)), fin=Hhmmss(FinDe(tipo));
				if (!DentroDeVentana(Hhmmss(t),ini,fin)) continue;
				DateTime clave=ClaveDeSesion(t,ini,fin);
				IltDevSesionVPInstancia s=null;
				for (int i=instancias.Count-1;i>=0;i--) if (instancias[i].Tipo==tipo && instancias[i].Inicio==clave) { s=instancias[i]; break; }
				if (s==null) continue;
				AddPriceVolume(s.VolumenPrecio,key,volume); s.VolumenTotal+=volume;
				if (tipo==2 && t >= s.Inicio && t < s.Inicio.AddMinutes(OpeningRangeMinutes))
				{ AddPriceVolume(s.VolumenOR,key,volume); s.VolumenORTotal+=volume; }
				CalculateVolumeLevels(s.VolumenPrecio,s.VolumenTotal,out s.VAH,out s.POC,out s.VAL);
				if (s.VolumenORTotal>0) CalculateVolumeLevels(s.VolumenOR,s.VolumenORTotal,out s.ORVAH,out s.ORPOC,out s.ORVAL);
			}
		}

		private static void AddPriceVolume(SortedDictionary<double,double> map,double price,double volume)
		{ double old; map.TryGetValue(price,out old); map[price]=old+volume; }

		private static void CalculateVolumeLevels(SortedDictionary<double,double> map,double total,out double vah,out double poc,out double val)
		{
			vah=poc=val=0; if(map==null||map.Count==0||total<=0)return;
			List<double> keys=new List<double>(map.Keys); double mid=(keys[0]+keys[keys.Count-1])*0.5,max=-1; int pi=0;
			for(int i=0;i<keys.Count;i++) if(map[keys[i]]>max || (Math.Abs(map[keys[i]]-max)<1e-9 && Math.Abs(keys[i]-mid)<Math.Abs(keys[pi]-mid))) {max=map[keys[i]];pi=i;}
			int lo=pi,hi=pi; double acc=map[keys[pi]],target=total*ValueAreaPercent/100.0;
			while(acc<target&&(lo>0||hi<keys.Count-1))
			{
				int upRows=Math.Min(2,keys.Count-1-hi),dnRows=Math.Min(2,lo);
				double up=0,dn=0;
				for(int k=1;k<=upRows;k++)up+=map[keys[hi+k]];
				for(int k=1;k<=dnRows;k++)dn+=map[keys[lo-k]];
				if(upRows==0 || (dnRows>0 && dn>up)) { for(int k=1;k<=dnRows;k++){lo--;acc+=map[keys[lo]];} }
				else { for(int k=1;k<=upRows;k++){hi++;acc+=map[keys[hi]];} }
			}
			poc=keys[pi];vah=keys[hi];val=keys[lo];
		}

		private void CerrarSiAbierta(int tipo)
		{
			if (abiertas[tipo] == null)
				return;

			abiertas[tipo].Cerrada = true;
			abiertas[tipo] = null;
		}

		/// Deja solo los ultimos N dias de negociacion.
		private void PodarHistorico()
		{
			int dias = Math.Max(1, DiasHistorico);

			// Se cuentan dias distintos de inicio, no instancias.
			HashSet<DateTime> vistos = new HashSet<DateTime>();

			for (int i = instancias.Count - 1; i >= 0; i--)
				vistos.Add(instancias[i].Inicio.Date);

			if (vistos.Count <= dias)
				return;

			List<DateTime> fechas = new List<DateTime>(vistos);
			fechas.Sort();

			DateTime corte = fechas[fechas.Count - dias];

			instancias.RemoveAll(x => x.Inicio.Date < corte);
		}

		#endregion

		#region ===================== HORARIOS =====================

		private bool SesionActiva(int tipo)
		{
			if (tipo == 0) return MostrarAsia;
			if (tipo == 1) return MostrarEuropa;
			return MostrarNewYork;
		}

		private DateTime InicioDe(int tipo)
		{
			if (tipo == 0) return AsiaInicio;
			if (tipo == 1) return EuropaInicio;
			return NewYorkInicio;
		}

		private DateTime FinDe(int tipo)
		{
			if (tipo == 0) return AsiaFin;
			if (tipo == 1) return EuropaFin;
			return NewYorkFin;
		}

		private Brush ColorDe(int tipo)
		{
			if (tipo == 0) return ColorAsia   ?? Brushes.Crimson;
			if (tipo == 1) return ColorEuropa ?? Brushes.LawnGreen;
			return ColorNewYork ?? Brushes.Cyan;
		}

		private string NombreDe(int tipo)
		{
			if (EtiquetasResumidas)
			{
				if (tipo == 0) return "A";
				if (tipo == 1) return "E";
				return "NY";
			}

			if (tipo == 0) return "ASIA";
			if (tipo == 1) return "EUROPA";
			return "NY";
		}

		/// Resumida pega el sufijo al nombre (AH, EL, NYO); completa lo separa (ASIA H).
		private string EtiquetaDe(int tipo, string sufijo)
		{
			return EtiquetasResumidas ? NombreDe(tipo) + sufijo : NombreDe(tipo) + " " + sufijo;
		}

		private static int Hhmmss(DateTime t)
		{
			return t.Hour * 10000 + t.Minute * 100 + t.Second;
		}

		/// Soporta ventanas que cruzan la medianoche (inicio > fin).
		private static bool DentroDeVentana(int hhmmss, int ini, int fin)
		{
			if (ini == fin)
				return false;

			if (ini < fin)
				return hhmmss >= ini && hhmmss < fin;

			return hhmmss >= ini || hhmmss < fin;
		}

		/// Momento exacto en que arranco la sesion a la que pertenece t.
		private static DateTime ClaveDeSesion(DateTime t, int ini, int fin)
		{
			DateTime baseDia = t.Date.AddHours(ini / 10000)
									 .AddMinutes((ini / 100) % 100)
									 .AddSeconds(ini % 100);

			if (ini > fin && Hhmmss(t) < fin)
				baseDia = baseDia.AddDays(-1);	// estamos en la cola del dia anterior

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

		/// Primer cierre de New York en o despues del arranque de la sesion.
		private DateTime ProximoCierreNy(DateTime inicio)
		{
			int nyFin = Hhmmss(NewYorkFin);

			DateTime cierre = inicio.Date.AddHours(nyFin / 10000)
										 .AddMinutes((nyFin / 100) % 100)
										 .AddSeconds(nyFin % 100);

			if (cierre <= inicio)
				cierre = cierre.AddDays(1);

			return cierre;
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

			// Cierre de la sesion en curso. Se saca del grafico, NO de Time[0]:
			// dentro de OnRender el indice de barra de las series no apunta a la ultima vela.
			DateTime ultimaGrafico = UltimaHoraGrafico();

			float xUltimaBarra = float.NaN;

			if (ultimaGrafico > DateTime.MinValue)
			{
				try { xUltimaBarra = (float)chartControl.GetXByTime(ultimaGrafico); }
				catch { xUltimaBarra = float.NaN; }
			}

			List<IltDevSesionVPEtiqueta> pendientes = new List<IltDevSesionVPEtiqueta>();

			for (int i = 0; i < instancias.Count; i++)
			{
				IltDevSesionVPInstancia s = instancias[i];

				if (!s.TieneDatos || !SesionActiva(s.Tipo))
					continue;

				// La sesion en curso llega hasta la ultima vela del grafico.
				DateTime finCaja	= s.Cerrada
									? s.Fin
									: (ultimaGrafico > s.UltimaBarra ? ultimaGrafico : s.UltimaBarra);

				DateTime finLinea	= ExtenderHighLow ? s.FinExtension : finCaja;

				if (finLinea < desde || s.Inicio > hasta)
					continue;

				float x1 = (float)chartControl.GetXByTime(s.Inicio);

				// Extremo real de la linea: aqui es donde termina la sesion americana.
				float xLineaReal	= (float)chartControl.GetXByTime(finLinea);
				float xCajaReal		= (float)chartControl.GetXByTime(finCaja);

				// Red de seguridad: la caja de una sesion abierta nunca se queda corta.
				if (!s.Cerrada && !float.IsNaN(xUltimaBarra) && xUltimaBarra > xCajaReal)
					xCajaReal = xUltimaBarra;

				// Para pintar se recorta al panel; para colocar la etiqueta se usa el real.
				float xLinea	= Math.Min(xLineaReal, limDer);
				float xCaja		= Math.Min(xCajaReal,  limDer);

				float yAlto = (float)chartScale.GetYByValue(s.Alto);
				float yBajo = (float)chartScale.GetYByValue(s.Bajo);
				float yAp   = (float)chartScale.GetYByValue(s.Apertura);

				// --- caja, sin contornos ---
				if (MostrarCaja && OpacidadCaja > 0 && dxCaja[s.Tipo] != null && xCaja - x1 >= 1f)
				{
					float top  = Math.Min(yAlto, yBajo);
					float alto = Math.Abs(yBajo - yAlto);

					if (alto >= 1f)
					{
						try
						{
							RenderTarget.FillRectangle(
								new SharpDX.RectangleF(x1, top, xCaja - x1, alto), dxCaja[s.Tipo]);
						}
						catch { }
					}
				}

				SharpDX.Direct2D1.SolidColorBrush br = dxLinea[s.Tipo];

				if (br == null)
					continue;

				if (MostrarHighLow)
				{
					DibujarLinea(x1, xLinea, yAlto, br, GrosorHighLow, EstiloHighLow);
					DibujarLinea(x1, xLinea, yBajo, br, GrosorHighLow, EstiloHighLow);
				}

				float xAperturaReal	= ExtenderApertura ? xLineaReal : xCajaReal;
				float xApertura		= Math.Min(xAperturaReal, limDer);

				if (MostrarApertura)
					DibujarLinea(x1, xApertura, yAp, br, GrosorApertura, EstiloApertura);

				// --- etiquetas: se apuntan aqui, se resuelven y dibujan al final ---
				if (MostrarEtiquetas && dxTexto != null)
				{
					if (MostrarHighLow)
					{
						Apuntar(pendientes, EtiquetaDe(s.Tipo, "H"), s.Alto, x1, xLineaReal, yAlto, altoEtiqueta, br);
						Apuntar(pendientes, EtiquetaDe(s.Tipo, "L"), s.Bajo, x1, xLineaReal, yBajo, altoEtiqueta, br);
					}

					if (MostrarApertura)
						Apuntar(pendientes, EtiquetaDe(s.Tipo, "O"), s.Apertura, x1, xAperturaReal, yAp, altoEtiqueta, br);
				}
			}

			// Volume Profile VAH/POC/VAL y el perfil OR de 15 min de NY.
			for (int i = 0; i < instancias.Count; i++)
			{
				IltDevSesionVPInstancia s = instancias[i];
				if (s.VolumenTotal <= 0) continue;
				float x1=(float)chartControl.GetXByTime(s.Inicio);
				float x2=Math.Min((float)chartControl.GetXByTime(s.FinExtension),limDer);
				if(x2<=limIzq||x1>=limDer)continue;
				x1=Math.Max(x1,limIzq);
				DrawVpLine(x1,x2,(float)chartScale.GetYByValue(s.VAH),dxVah,1,null);
				DrawVpLine(x1,x2,(float)chartScale.GetYByValue(s.POC),dxPoc,2,dxDot);
				DrawVpLine(x1,x2,(float)chartScale.GetYByValue(s.VAL),dxVal,1,null);
				if(s.Tipo==2 && s.VolumenORTotal>0)
				{
					DrawVpLine(x1,x2,(float)chartScale.GetYByValue(s.ORVAH),dxVah,2,null);
					DrawVpLine(x1,x2,(float)chartScale.GetYByValue(s.ORPOC),dxPoc,3,dxDot);
					DrawVpLine(x1,x2,(float)chartScale.GetYByValue(s.ORVAL),dxVal,2,null);
				}
			}
			DibujarEtiquetas(pendientes);
		}

		private void DrawVpLine(float x1,float x2,float y,SharpDX.Direct2D1.SolidColorBrush brush,float width,SharpDX.Direct2D1.StrokeStyle style)
		{
			if(brush==null||float.IsNaN(y)||float.IsInfinity(y)||x2<=x1)return;
			try{RenderTarget.DrawLine(new SharpDX.Vector2(x1,y),new SharpDX.Vector2(x2,y),brush,width,style);}catch{}
		}

		/// Calcula donde iria la etiqueta y la deja anotada, sin pintarla todavia.
		private void Apuntar(List<IltDevSesionVPEtiqueta> lista, string nombre, double precio,
			float x1, float x2, float y, float alto, SharpDX.Direct2D1.SolidColorBrush br)
		{
			if (float.IsNaN(y) || float.IsInfinity(y))
				return;

			string texto = IncluirPrecio ? nombre + " " + FormatearPrecio(precio) : nombre;

			float ancho = Math.Max(34f, texto.Length * TamanoEtiqueta * 0.62f + 8f);
			float sep	= 4f;

			float limIzq = (float)ChartPanel.X;
			float limDer = (float)(ChartPanel.X + ChartPanel.W);

			// Tramo de la linea que de verdad se ve dentro del panel.
			// El final de la linea (cierre de NY) es una hora futura: al ampliar las velas
			// NinjaTrader lo proyecta cada vez mas a la derecha y acaba saliendose del grafico.
			// En ese caso la etiqueta se pega al borde visible, que es justo donde se ve
			// terminar la linea, en lugar de desaparecer.
			float visIzq = Math.Max(x1, limIzq);
			float visDer = Math.Min(x2, limDer);

			// Solo se descarta si la linea entera queda fuera del panel.
			if (visDer <= limIzq || visIzq >= limDer)
				return;

			float x;

			switch (LadoEtiqueta)
			{
				case IltDevSesionVPLadoEtiqueta.Derecha:
					x = visDer - ancho - sep;	// pegada al final visible de la linea
					break;

				case IltDevSesionVPLadoEtiqueta.Personalizada:
					float pct = (float)Math.Max(0, Math.Min(100, PosicionEtiquetaPct)) / 100f;
					x = x1 + (x2 - x1) * pct;
					break;

				default:
					x = visIzq + sep;
					break;
			}

			if (x < limIzq + 2f)			x = limIzq + 2f;
			if (x + ancho > limDer - 2f)	x = limDer - ancho - 2f;

			float top;

			switch (PosicionVertical)
			{
				case IltDevSesionVPPosVertical.Arriba:		top = y - alto - 2f;	break;
				case IltDevSesionVPPosVertical.Abajo:		top = y + 2f;			break;
				default:								top = y - alto / 2f;	break;
			}

			lista.Add(new IltDevSesionVPEtiqueta
			{
				Texto	= texto,
				X		= x,
				Top		= top,
				Ancho	= ancho,
				Alto	= alto,
				Brocha	= br
			});
		}

		/// Resuelve choques reales entre etiquetas y las pinta.
		/// Primero intenta separarlas en vertical (como mucho dos alturas, para que ninguna
		/// se aleje de su linea) y despues escalonarlas hacia la izquierda.
		/// Si aun asi se cruzan se dibujan igual: ninguna etiqueta se pierde.
		private void DibujarEtiquetas(List<IltDevSesionVPEtiqueta> lista)
		{
			if (lista.Count == 0)
				return;

			lista.Sort(delegate(IltDevSesionVPEtiqueta a, IltDevSesionVPEtiqueta b)
			{
				return a.Top.CompareTo(b.Top);
			});

			List<IltDevSesionVPEtiqueta> colocadas = new List<IltDevSesionVPEtiqueta>();

			for (int i = 0; i < lista.Count; i++)
			{
				IltDevSesionVPEtiqueta e = lista[i];

				float limIzqE	= (float)ChartPanel.X;
				float margen	= e.Alto * 2f;
				float topOrig	= e.Top;

				// 1) separar en vertical, sin alejarse de su linea
				while (Choca(e, colocadas) && e.Top - topOrig <= margen)
					e.Top += 2f;

				// 2) si aun choca, escalonar hacia la izquierda en vez de perderla
				if (Choca(e, colocadas))
				{
					e.Top = topOrig;

					for (int intento = 0; intento < 3 && Choca(e, colocadas); intento++)
					{
						float nuevo = e.X - (e.Ancho + 4f);

						if (nuevo < limIzqE + 2f)
							break;

						e.X = nuevo;
					}
				}

				// 3) en el peor caso se dibuja igual: una etiqueta nunca desaparece.
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

		private static bool Choca(IltDevSesionVPEtiqueta e, List<IltDevSesionVPEtiqueta> colocadas)
		{
			for (int i = 0; i < colocadas.Count; i++)
			{
				IltDevSesionVPEtiqueta o = colocadas[i];

				bool cruzaX = e.X < o.X + o.Ancho && o.X < e.X + e.Ancho;
				bool cruzaY = e.Top < o.Top + o.Alto && o.Top < e.Top + e.Alto;

				if (cruzaX && cruzaY)
					return true;
			}

			return false;
		}

		private void DibujarLinea(float x1, float x2, float y, SharpDX.Direct2D1.SolidColorBrush br, int grosor, DashStyleHelper estilo)
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

		/// Hora de la ultima vela cargada en el grafico.
		/// Dentro de OnRender no se puede usar Time[0]: se evalua fuera del recorrido de
		/// barras y su indice no apunta a la ultima vela, asi que la sesion en curso
		/// terminaba antes de su propio arranque y su caja no llegaba a dibujarse.
		private DateTime UltimaHoraGrafico()
		{
			try
			{
				if (BarsArray == null || BarsArray.Length == 0 || BarsArray[0] == null)
					return DateTime.MinValue;

				int n = BarsArray[0].Count;

				if (n <= 0)
					return DateTime.MinValue;

				return BarsArray[0].GetTime(n - 1);
			}
			catch { }

			return DateTime.MinValue;
		}

		private void RangoVisible(out DateTime desde, out DateTime hasta)
		{
			desde	= DateTime.MinValue;
			hasta	= DateTime.MaxValue;

			try
			{
				if (ChartBars == null || BarsArray == null || BarsArray.Length == 0 || BarsArray[0] == null)
					return;

				int a = Math.Max(0, ChartBars.FromIndex);
				int b = Math.Min(BarsArray[0].Count - 1, ChartBars.ToIndex);

				if (b < a)
					return;

				desde	= BarsArray[0].GetTime(a).AddMinutes(-10);
				hasta	= BarsArray[0].GetTime(b).AddDays(2);
			}
			catch
			{
				desde	= DateTime.MinValue;
				hasta	= DateTime.MaxValue;
			}
		}

		#endregion

		#region ===================== RECURSOS SHARPDX =====================

		private void CreateDxResources()
		{
			if (dxVah == null) dxVah = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(30,144,255,255));
			if (dxPoc == null) dxPoc = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(255,215,0,255));
			if (dxVal == null) dxVal = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(220,20,60,255));
			if (RenderTarget == null)
				return;

			for (int i = 0; i < NumSesiones; i++)
			{
				Brush c = ColorDe(i);

				if (dxLinea[i] == null || !object.ReferenceEquals(cacheColor[i], c))
				{
					if (dxLinea[i] != null) { dxLinea[i].Dispose(); dxLinea[i] = null; }

					dxLinea[i]		= new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToDxColor(c, 1.0));
					cacheColor[i]	= c;

					if (dxCaja[i] != null) { dxCaja[i].Dispose(); dxCaja[i] = null; }
				}

				if (dxCaja[i] == null || cacheOpacidadCaja != OpacidadCaja)
				{
					if (dxCaja[i] != null) { dxCaja[i].Dispose(); dxCaja[i] = null; }

					double op = Math.Max(0, Math.Min(100, OpacidadCaja)) / 100.0;
					dxCaja[i] = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToDxColor(c, op));
				}
			}

			cacheOpacidadCaja = OpacidadCaja;

			if (dxFondo == null || cacheOpacidadFondo != OpacidadFondoPct
				|| !object.ReferenceEquals(cacheFondoBrush, ColorFondoEtiqueta))
			{
				if (dxFondo != null) { dxFondo.Dispose(); dxFondo = null; }

				double op			= Math.Max(0, Math.Min(100, OpacidadFondoPct)) / 100.0;
				dxFondo				= new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToDxColor(ColorFondoEtiqueta, op));
				cacheOpacidadFondo	= OpacidadFondoPct;
				cacheFondoBrush		= ColorFondoEtiqueta;
			}

			if (dxTexto == null || cacheFuente != TamanoEtiqueta)
			{
				if (dxTexto != null) { dxTexto.Dispose(); dxTexto = null; }

				dxTexto = new SharpDX.DirectWrite.TextFormat(Globals.DirectWriteFactory, "Arial",
					SharpDX.DirectWrite.FontWeight.Bold,
					SharpDX.DirectWrite.FontStyle.Normal,
					SharpDX.DirectWrite.FontStretch.Normal,
					Math.Max(6, TamanoEtiqueta));

				cacheFuente = TamanoEtiqueta;
			}

			if (dxDash == null)
			{
				dxDash			= CrearEstilo(SharpDX.Direct2D1.DashStyle.Dash);
				dxDot			= CrearEstilo(SharpDX.Direct2D1.DashStyle.Dot);
				dxDashDot		= CrearEstilo(SharpDX.Direct2D1.DashStyle.DashDot);
				dxDashDotDot	= CrearEstilo(SharpDX.Direct2D1.DashStyle.DashDotDot);
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
				case DashStyleHelper.Dash:			return dxDash;
				case DashStyleHelper.DashDot:		return dxDashDot;
				case DashStyleHelper.DashDotDot:	return dxDashDotDot;
				case DashStyleHelper.Dot:			return dxDot;
				default:							return null;
			}
		}

		private SharpDX.Color ToDxColor(Brush brush, double opacidad)
		{
			SolidColorBrush scb = brush as SolidColorBrush;

			if (scb == null)
				return new SharpDX.Color((byte)255, (byte)255, (byte)255, (byte)(255 * opacidad));

			System.Windows.Media.Color c = scb.Color;

			return new SharpDX.Color(c.R, c.G, c.B, (byte)(c.A * Math.Max(0, Math.Min(1, opacidad))));
		}

		private void DisposeDxResources()
		{
			if (dxVah != null) { dxVah.Dispose(); dxVah=null; }
			if (dxPoc != null) { dxPoc.Dispose(); dxPoc=null; }
			if (dxVal != null) { dxVal.Dispose(); dxVal=null; }
			for (int i = 0; i < NumSesiones; i++)
			{
				if (dxLinea[i] != null) { dxLinea[i].Dispose(); dxLinea[i] = null; }
				if (dxCaja[i]  != null) { dxCaja[i].Dispose();  dxCaja[i]  = null; }
				cacheColor[i] = null;
			}

			if (dxFondo != null)		{ dxFondo.Dispose();		dxFondo = null; }
			if (dxDash != null)			{ dxDash.Dispose();			dxDash = null; }
			if (dxDot != null)			{ dxDot.Dispose();			dxDot = null; }
			if (dxDashDot != null)		{ dxDashDot.Dispose();		dxDashDot = null; }
			if (dxDashDotDot != null)	{ dxDashDotDot.Dispose();	dxDashDotDot = null; }
			if (dxTexto != null)		{ dxTexto.Dispose();		dxTexto = null; }

			cacheFuente			= -1;
			cacheOpacidadCaja	= -1;
			cacheOpacidadFondo	= -1;
			cacheFondoBrush		= null;
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

				toolbarBtn				= new NTMenuItem();
				toolbarBtn.Header		= "ILT-DEV-SesionesVP";
				toolbarBtn.Style		= Application.Current.TryFindResource("MainMenuItem") as Style;
				toolbarBtn.Foreground	= new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 90, 190, 255));
				toolbarBtn.ToolTip		= "Mostrar / ocultar panel ILT-DEV SESSIONS";
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
				return;
			}

			panelUserHidden = !panelUserHidden;
			ApplyPanelVisibilityFromToolbar();
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
					BorderBrush			= new SolidColorBrush(System.Windows.Media.Color.FromArgb(153, 77, 179, 255)),
					Background			= new SolidColorBrush(System.Windows.Media.Color.FromArgb(230, 17, 17, 17)),
					Padding				= new Thickness(0),
					HorizontalAlignment	= HorizontalAlignment.Left,
					VerticalAlignment	= VerticalAlignment.Top,
					Margin				= new Thickness(14, 14, 0, 0),
					Width				= 188
				};

				outer.Child = BuildPanelInner();

				UserControlCollection.Add(outer);

				wpfOuterPanel	= outer;
				wpfPanelAdded	= true;

				ApplyPanelVisibilityFromToolbar();
			}
			catch (Exception ex)
			{
				try { Print("ILT-DEV-SesionesVP panel add error: " + ex.Message); }
				catch { }

				wpfOuterPanel	= null;
				wpfPanelAdded	= false;
			}
		}

		private Grid BuildPanelInner()
		{
			Grid root = new Grid();

			double[] alturas = new double[] { -1, 6, -1, 4, -1, 4, -1, 4, -1, 6 };

			for (int i = 0; i < alturas.Length; i++)
			{
				RowDefinition rd = new RowDefinition();
				rd.Height = alturas[i] >= 0 ? new GridLength(alturas[i]) : GridLength.Auto;
				root.RowDefinitions.Add(rd);
			}

			Grid header = new Grid
			{
				Height		= 22,
				Background	= new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 255, 255, 255)),
				Cursor		= Cursors.SizeAll
			};

			header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

			TextBlock title = new TextBlock
			{
				Text				= "ILT-DEV-SesionesVP",
				FontFamily			= new FontFamily("Consolas"),
				FontSize			= 12,
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
				FontSize	= 10,
				Foreground	= Brushes.White,
				Background	= new SolidColorBrush(System.Windows.Media.Color.FromArgb(242, 51, 140, 217))
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

			wpfBotones[0] = CreatePanelButton("ASIA");
			wpfBotones[0].Click += (o, e) => { MostrarAsia = !MostrarAsia; UpdateWpfPanelState(); RequestChartRefresh(); e.Handled = true; };
			Grid.SetRow(wpfBotones[0], 2);
			root.Children.Add(wpfBotones[0]);

			wpfBotones[1] = CreatePanelButton("EUROPA");
			wpfBotones[1].Click += (o, e) => { MostrarEuropa = !MostrarEuropa; UpdateWpfPanelState(); RequestChartRefresh(); e.Handled = true; };
			Grid.SetRow(wpfBotones[1], 4);
			root.Children.Add(wpfBotones[1]);

			wpfBotones[2] = CreatePanelButton("NEW YORK");
			wpfBotones[2].Click += (o, e) => { MostrarNewYork = !MostrarNewYork; UpdateWpfPanelState(); RequestChartRefresh(); e.Handled = true; };
			Grid.SetRow(wpfBotones[2], 6);
			root.Children.Add(wpfBotones[2]);

			wpfBotones[3] = CreatePanelButton("OPENING");
			wpfBotones[3].Click += (o, e) => { MostrarApertura = !MostrarApertura; UpdateWpfPanelState(); RequestChartRefresh(); e.Handled = true; };
			Grid.SetRow(wpfBotones[3], 8);
			root.Children.Add(wpfBotones[3]);

			UpdateWpfPanelState();

			return root;
		}

		private void ApplyPanelMinimized()
		{
			if (wpfMinimizeButton != null)
				wpfMinimizeButton.Content = wpfPanelMinimized ? "▶" : "▼";

			Visibility v = wpfPanelMinimized ? Visibility.Collapsed : Visibility.Visible;

			for (int i = 0; i < NumBotones; i++)
				if (wpfBotones[i] != null)
					wpfBotones[i].Visibility = v;
		}

		private Button CreatePanelButton(string texto)
		{
			return new Button
			{
				Content			= texto,
				FontFamily		= new FontFamily("Consolas"),
				FontSize		= 11,
				FontWeight		= FontWeights.Bold,
				Height			= 26,
				Margin			= new Thickness(8, 0, 8, 0),
				Padding			= new Thickness(4, 2, 4, 2),
				Foreground		= Brushes.White,
				BorderBrush		= new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 179, 179, 179)),
				BorderThickness	= new Thickness(1),
				Cursor			= Cursors.Hand,
				Focusable		= false
			};
		}

		private void UpdateWpfPanelState()
		{
			StylePanelButton(wpfBotones[0], MostrarAsia);
			StylePanelButton(wpfBotones[1], MostrarEuropa);
			StylePanelButton(wpfBotones[2], MostrarNewYork);
			StylePanelButton(wpfBotones[3], MostrarApertura);
		}

		private void StylePanelButton(Button b, bool activo)
		{
			if (b == null)
				return;

			b.Background = activo
				? new SolidColorBrush(System.Windows.Media.Color.FromArgb(242, 51, 140, 217))
				: new SolidColorBrush(System.Windows.Media.Color.FromArgb(217, 51, 51, 51));
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
		[Display(Name = "Dias de historico", Description = "Cuantos dias de sesiones se dibujan hacia atras.", GroupName = "01. General", Order = 2)]
		public int DiasHistorico { get; set; }

		[Display(Name = "Boton en toolbar superior", GroupName = "01. General", Order = 3)]
		public bool ShowToolbarButton { get; set; }

		[Display(Name = "Mostrar", GroupName = "02. Asia", Order = 1)]
		public bool MostrarAsia { get; set; }

		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "Inicio", GroupName = "02. Asia", Order = 2)]
		public DateTime AsiaInicio { get; set; }

		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "Fin", GroupName = "02. Asia", Order = 3)]
		public DateTime AsiaFin { get; set; }

		[XmlIgnore]
		[Display(Name = "Color", GroupName = "02. Asia", Order = 4)]
		public Brush ColorAsia { get; set; }

		[Browsable(false)]
		public string ColorAsiaSerialize
		{
			get { return Serialize.BrushToString(ColorAsia); }
			set { ColorAsia = Serialize.StringToBrush(value); }
		}

		[Display(Name = "Mostrar", GroupName = "03. Europa", Order = 1)]
		public bool MostrarEuropa { get; set; }

		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "Inicio", GroupName = "03. Europa", Order = 2)]
		public DateTime EuropaInicio { get; set; }

		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "Fin", GroupName = "03. Europa", Order = 3)]
		public DateTime EuropaFin { get; set; }

		[XmlIgnore]
		[Display(Name = "Color", GroupName = "03. Europa", Order = 4)]
		public Brush ColorEuropa { get; set; }

		[Browsable(false)]
		public string ColorEuropaSerialize
		{
			get { return Serialize.BrushToString(ColorEuropa); }
			set { ColorEuropa = Serialize.StringToBrush(value); }
		}

		[Display(Name = "Mostrar", GroupName = "04. New York", Order = 1)]
		public bool MostrarNewYork { get; set; }

		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "Inicio", GroupName = "04. New York", Order = 2)]
		public DateTime NewYorkInicio { get; set; }

		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "Fin", GroupName = "04. New York", Order = 3)]
		public DateTime NewYorkFin { get; set; }

		[XmlIgnore]
		[Display(Name = "Color", GroupName = "04. New York", Order = 4)]
		public Brush ColorNewYork { get; set; }

		[Browsable(false)]
		public string ColorNewYorkSerialize
		{
			get { return Serialize.BrushToString(ColorNewYork); }
			set { ColorNewYork = Serialize.StringToBrush(value); }
		}

		[Display(Name = "Mostrar caja", GroupName = "05. Caja", Order = 1)]
		public bool MostrarCaja { get; set; }

		[Range(0, 100)]
		[Display(Name = "Opacidad de la caja (%)", Description = "0 = sin caja. La caja no lleva contornos.", GroupName = "05. Caja", Order = 2)]
		public int OpacidadCaja { get; set; }

		[Display(Name = "Mostrar high / low", GroupName = "06. Lineas High / Low", Order = 1)]
		public bool MostrarHighLow { get; set; }

		[Range(1, 20)]
		[Display(Name = "Grosor", GroupName = "06. Lineas High / Low", Order = 2)]
		public int GrosorHighLow { get; set; }

		[Display(Name = "Estilo", GroupName = "06. Lineas High / Low", Order = 3)]
		public DashStyleHelper EstiloHighLow { get; set; }

		[Display(Name = "Extender hasta el cierre de NY", Description = "Prolonga las lineas hacia la derecha hasta que cierra New York.", GroupName = "06. Lineas High / Low", Order = 4)]
		public bool ExtenderHighLow { get; set; }

		[Display(Name = "Mostrar apertura", GroupName = "07. Linea de apertura", Order = 1)]
		public bool MostrarApertura { get; set; }

		[Range(1, 20)]
		[Display(Name = "Grosor", GroupName = "07. Linea de apertura", Order = 2)]
		public int GrosorApertura { get; set; }

		[Display(Name = "Estilo", GroupName = "07. Linea de apertura", Order = 3)]
		public DashStyleHelper EstiloApertura { get; set; }

		[Display(Name = "Extender hasta el cierre de NY", Description = "Independiente del de high / low.", GroupName = "07. Linea de apertura", Order = 4)]
		public bool ExtenderApertura { get; set; }

		[Display(Name = "Mostrar etiquetas", GroupName = "08. Etiquetas", Order = 1)]
		public bool MostrarEtiquetas { get; set; }

		[Display(Name = "Incluir el precio", GroupName = "08. Etiquetas", Order = 2)]
		public bool IncluirPrecio { get; set; }

		[Display(Name = "Etiquetas resumidas", Description = "Abrevia el nombre: AH / AL / AO, EH / EL / EO, NYH / NYL / NYO.", GroupName = "08. Etiquetas", Order = 3)]
		public bool EtiquetasResumidas { get; set; }

		[Display(Name = "Lado de la etiqueta", Description = "Izquierda o Derecha de la linea. Personalizada usa el porcentaje.", GroupName = "08. Etiquetas", Order = 4)]
		public IltDevSesionVPLadoEtiqueta LadoEtiqueta { get; set; }

		[Range(0, 100)]
		[Display(Name = "Posicion horizontal (%)", Description = "Solo si el lado es Personalizada.", GroupName = "08. Etiquetas", Order = 5)]
		public int PosicionEtiquetaPct { get; set; }

		[Display(Name = "Posicion vertical", Description = "Arriba de la linea, montada sobre ella, o debajo.", GroupName = "08. Etiquetas", Order = 6)]
		public IltDevSesionVPPosVertical PosicionVertical { get; set; }

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



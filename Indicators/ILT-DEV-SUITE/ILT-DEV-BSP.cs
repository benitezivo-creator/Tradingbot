#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Core;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using SharpDX;
using SharpDX.DirectWrite;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.ILT_DEV_SUITE
{
    public class ILT_DEV_BSP : Indicator
    {
    	private sealed class EstadoOHLC
    	{
    		public bool Iniciada;
    		public double Open;
    		public double High;
    		public double Low;
    		public double Close;
    	}

    	private sealed class VelaPresion
    	{
    		public bool Iniciada;
    
    		public double Open;
    
    		public double High;
    
    		public double Low;
    
    		public double Close;
    
    		public double DeltaBarra;
    
    		public double MetricaBarra;
    
    		public long Trades;
    
    		public long Contratos;
    
    		public long TradesCompra;
    
    		public long TradesVenta;
    
    		public long VolumenCompra;
    
    		public long VolumenVenta;
    
    		public long ClasifCotizacion;
    
    		public long ClasifTickRule;
    
    		public long ClasifPersistencia;
    
    		public long SinClasificar;
    
    		public double Pulso;
    
    		public double Desequilibrio;
    
    		public DateTime PrimerTiempo;
    
    		public DateTime UltimoTiempo;

    		public readonly EstadoOHLC PorVela = new EstadoOHLC();

    		public readonly EstadoOHLC Acumulado = new EstadoOHLC();

    		public readonly EstadoOHLC Bracket = new EstadoOHLC();
    	}
    
    	private sealed class RegistroSesion
    	{
    		public DateTime Inicio;
    
    		public double Delta;
    
    		public double PrecioApertura;
    
    		public double PrecioCierre;
    
    		public bool TienePrecio;
    
    		public bool Completa;
    	}
    
	private struct EntradaBracket
	{
		public long Volumen;
    
    		public double MetricaFirmada;
    
		public double Metrica;
	}

	private struct NivelMapaDelta
	{
		public long PrecioTicks;

		public double Precio;

		public double Delta;

		public string Etiqueta;
	}

	private enum TipoSesionMapaDelta
	{
		Ninguna,
		ETH,
		RTH
	}
    
    	private readonly List<VelaPresion> velas = new List<VelaPresion>(4096);
    
	private readonly Queue<EntradaBracket> bracket = new Queue<EntradaBracket>(8192);

	// El mapa siempre usa volumen por precio (contratos), independientemente
	// de que el grafico BSP principal este configurado en volumen o prints.
	private readonly Dictionary<long, double> mapaDeltaEth = new Dictionary<long, double>();

	private readonly Dictionary<long, double> mapaDeltaRth = new Dictionary<long, double>();

	private DateTime fechaOperacionMapa = DateTime.MinValue;
    
    	private long bracketVolumen;
    
    	private double bracketDelta;
    
    	private double bracketMetrica;
    
    	private double precioAnterior = double.NaN;
    
    	private int direccionAnterior;
    
    	private double bidVivo = double.NaN;
    
    	private double askVivo = double.NaN;
    
    	private long totalPrints;
    
    	private long printsTickCero;
    
    	private long printsComparables;
    
    	private long printsCoincidentes;
    
    	private long sesionCotizacion;
    
    	private long sesionTickRule;
    
    	private long sesionPersistencia;
    
    	private long sesionSinClasificar;
    
    	private double deltaSesion;
    
    	private long volumenSesionCompra;
    
    	private long volumenSesionVenta;
    
    	private int barraUltimoResetSesion = -1;
    
    	private readonly List<RegistroSesion> libroSesiones = new List<RegistroSesion>(16);
    
    	private RegistroSesion sesionActiva;
    
    	private bool aperturaObservada;
    
    	private double emaIntensidad;
    
    	private double pulsoActual;
    
    	private DateTime bracketInicio = DateTime.MinValue;
    
    	private DateTime bracketFin = DateTime.MinValue;
    
    	private int ultimaBarraTickHistorico = -1;
    
    	private int ultimoIndiceMapeado = -1;
    
    	private bool tickReplayVerificado;
    
    	private bool tickReplayActivo;
    
    	private bool esPrimariaTemporal;
    
    	private readonly object candado = new object();
    
    	private bool avisoTickReplayEmitido;
    
    	private TextFormat fmtTitulo;
    
    	private TextFormat fmtEtiqueta;
    
    	private TextFormat fmtValor;
    
    	private TextFormat fmtValorDer;
    
    	private TextFormat fmtCentro;

    	private int modoDelta = 3;
    
    	[NinjaScriptProperty]
    	[Display(Name = "Métrica base (1 Volumen / 2 Ticks)", Description = "1 = Volumen: cuenta contratos. 2 = Ticks: cuenta un punto por operación, lo que reduce el peso de las órdenes grandes.", Order = 1, GroupName = "01. Cálculo")]
    	[Range(1, 2)]
    	public int Metrica { get; set; }
    
    	[Browsable(false)]
    	[Range(1, 3)]
    	[Display(Name = "Modo de la línea (1 Vela / 2 Sesión / 3 Bracket)", Description = "1 = Delta propio de cada vela. 2 = Delta acumulado de la sesión. 3 = Bracket rodante de volumen, con horizonte constante en actividad.", Order = 2, GroupName = "01. Cálculo")]
    	public int ModoDelta
    	{
    		get { return modoDelta; }
    		set
    		{
    			int nuevoModo = Math.Max(1, Math.Min(3, value));
    			if (modoDelta == nuevoModo)
    				return;

    			modoDelta = nuevoModo;
    			lock (candado)
    			{
    				for (int i = 0; i < velas.Count; i++)
    					SincronizarModo(velas[i]);
    			}
    		}
    	}
    
    	[NinjaScriptProperty]
    	[Range(1, 2)]
    	[Display(Name = "Clasificación (1 Cotización / 2 Uniforme)", Description = "1 = Cotización en vivo y tick rule en histórico: máxima precisión instantánea. 2 = Uniforme: tick rule también en vivo, con lo que la curva queda idéntica en vivo, en histórico y entre equipos, a costa de ~11% de precisión.", Order = 3, GroupName = "01. Cálculo")]
    	public int Politica { get; set; }
    
    	[Display(Name = "Reiniciar en cada sesión", Description = "Reinicia el acumulado y el bracket en la apertura de sesión, y archiva la jornada en el libro direccional.", Order = 4, GroupName = "01. Cálculo")]
    	[NinjaScriptProperty]
    	public bool ReiniciarEnSesion { get; set; }
    
    	[NinjaScriptProperty]
    	[Display(Name = "Volumen mínimo por trade", Description = "Descarta del delta los prints por debajo de este tamaño. En 0 no filtra nada, que es lo recomendado en ES/NQ.", Order = 5, GroupName = "01. Cálculo")]
    	[Range(0, int.MaxValue)]
    	public int VolumenMinimoPorTrade { get; set; }
    
    	[NinjaScriptProperty]
    	[Display(Name = "Umbral del bracket", Description = "Contratos que abarca la ventana rodante. Define un horizonte constante en actividad y no en tiempo.", Order = 1, GroupName = "02. Bracket")]
    	[Range(1, 100000000)]
    	public long UmbralBracket { get; set; }
    
    	[Display(Name = "Base del bracket (1 Clasificados / 2 Todos)", Description = "1 = Solo clasificados: evita que operaciones indeterminadas expulsen de la ventana a operaciones firmadas. 2 = Todos los trades: la ventana cubre todo el volumen.", Order = 2, GroupName = "02. Bracket")]
    	[NinjaScriptProperty]
    	[Range(1, 2)]
    	public int BaseBracket { get; set; }
    
    	[NinjaScriptProperty]
    	[Display(Name = "Mostrar velas", Order = 1, GroupName = "03. Visual")]
    	public bool MostrarVelas { get; set; }
    
    	[NinjaScriptProperty]
    	[Range(10, 100)]
    	[Display(Name = "Ancho de vela (%)", Description = "Porcentaje del ancho real de las velas del gráfico. En 100 quedan exactamente alineadas con las de precio.", Order = 2, GroupName = "03. Visual")]
    	public int AnchoVelaPorcentaje { get; set; }
    
    	[Display(Name = "Mostrar línea cero", Order = 3, GroupName = "03. Visual")]
    	[NinjaScriptProperty]
    	public bool MostrarLineaCero { get; set; }
    
    	[Display(Name = "Mostrar panel", Order = 4, GroupName = "03. Visual")]
    	[NinjaScriptProperty]
    	public bool MostrarHud { get; set; }
    
    	[Display(Name = "Escala del panel", Order = 8, GroupName = "03. Visual")]
    	[Range(0.7, 2.0)]
    	[NinjaScriptProperty]
    	public double EscalaPanel { get; set; }
    
    	[NinjaScriptProperty]
    	[Display(Name = "Mostrar pulso", Description = "Dirección por desequilibrio de volumen, magnitud por intensidad de ejecución relativa a la sesión. Rango -100..+100.", Order = 5, GroupName = "03. Visual")]
    	public bool MostrarPulso { get; set; }
    
    	[NinjaScriptProperty]
    	[Display(Name = "Mostrar libro de sesiones", Description = "Delta de cierre de las últimas sesiones, su balance y la lectura de contexto de la jornada anterior.", Order = 6, GroupName = "03. Visual")]
    	public bool MostrarLibroSesiones { get; set; }
    
    	[Display(Name = "Sesiones en el balance", Order = 7, GroupName = "03. Visual")]
    	[NinjaScriptProperty]
    	[Range(1, 12)]
    	public int SesionesEnBalance { get; set; }
    
    	[XmlIgnore]
    	[Display(Name = "Color compra", Order = 1, GroupName = "04. Colores")]
    	public System.Windows.Media.Brush ColorCompra { get; set; }
    
    	[Browsable(false)]
    	public string ColorCompraSerializable
    	{
    		get
    		{
    			return Serialize.BrushToString(ColorCompra);
    		}
    		set
    		{
    			ColorCompra = Serialize.StringToBrush(value);
    		}
    	}
    
    	[XmlIgnore]
    	[Display(Name = "Color venta", Order = 2, GroupName = "04. Colores")]
    	public System.Windows.Media.Brush ColorVenta { get; set; }
    
    	[Browsable(false)]
    	public string ColorVentaSerializable
    	{
    		get
    		{
    			return Serialize.BrushToString(ColorVenta);
    		}
    		set
    		{
    			ColorVenta = Serialize.StringToBrush(value);
    		}
    	}
    
    	[Display(Name = "Color neutro", Order = 3, GroupName = "04. Colores")]
    	[XmlIgnore]
    	public System.Windows.Media.Brush ColorNeutro { get; set; }
    
    	[Browsable(false)]
    	public string ColorNeutroSerializable
    	{
    		get
    		{
    			return Serialize.BrushToString(ColorNeutro);
    		}
    		set
    		{
    			ColorNeutro = Serialize.StringToBrush(value);
    		}
    	}
    
    	[XmlIgnore]
    	[Display(Name = "Color línea cero", Order = 4, GroupName = "04. Colores")]
    	public System.Windows.Media.Brush ColorLineaCero { get; set; }
    
    	[Browsable(false)]
    	public string ColorLineaCeroSerializable
    	{
    		get
    		{
    			return Serialize.BrushToString(ColorLineaCero);
    		}
    		set
    		{
    			ColorLineaCero = Serialize.StringToBrush(value);
    		}
    	}
    
    	[XmlIgnore]
    	[Display(Name = "Color del texto", Order = 5, GroupName = "04. Colores")]
    	public System.Windows.Media.Brush ColorTexto { get; set; }
    
    	[Browsable(false)]
    	public string ColorTextoSerializable
    	{
    		get
    		{
    			return Serialize.BrushToString(ColorTexto);
    		}
    		set
    		{
    			ColorTexto = Serialize.StringToBrush(value);
    		}
    	}
    
    	[Browsable(false)]
    	[XmlIgnore]
    	public Series<double> Presion { get { return base.Values[0]; } }
    
    	[Browsable(false)]
    	[XmlIgnore]
    	public Series<double> PresionAlto { get { return base.Values[1]; } }
    
    	[Browsable(false)]
    	[XmlIgnore]
    	public Series<double> PresionBajo { get { return base.Values[2]; } }
    
    	[Browsable(false)]
    	[XmlIgnore]
    	public Series<double> DeltaBarra { get { return base.Values[3]; } }
    
    	[XmlIgnore]
    	[Browsable(false)]
    	public Series<double> Pulso { get { return base.Values[4]; } }
    
    	[XmlIgnore]
    	[Browsable(false)]
    	public Series<double> DeltaSesion { get { return base.Values[5]; } }
    
    	private void CoreOnStateChange()
    	{
    		if (base.State == State.SetDefaults)
    		{
    			base.Description = "ILT-DEV-BSP v3.0.5 — presión compradora y vendedora, bracket VND y velas de presión.";
    			Name = "ILT-DEV-BSP";
    			base.Calculate = Calculate.OnEachTick;
    			base.IsOverlay = false;
    			base.IsSuspendedWhileInactive = false;
    			base.DisplayInDataBox = true;
    			base.DrawOnPricePanel = false;
    			base.PaintPriceMarkers = false;
    			Metrica = 1;
    			ModoDelta = 3;
    			Politica = 1;
    			ReiniciarEnSesion = true;
    			VolumenMinimoPorTrade = 0;
    			UmbralBracket = 10000L;
    			BaseBracket = 1;
    			MostrarVelas = true;
    			AnchoVelaPorcentaje = 100;
    			MostrarLineaCero = true;
    			MostrarHud = false;
    			EscalaPanel = 1.0;
    			MostrarPulso = true;
    			MostrarLibroSesiones = false;
    			SesionesEnBalance = 5;
    			ColorCompra = Brushes.LimeGreen;
    			ColorVenta = Brushes.Red;
    			ColorNeutro = Brushes.DimGray;
    			ColorLineaCero = Brushes.DimGray;
    			ColorTexto = Brushes.Gainsboro;
    			AddPlot(Brushes.Transparent, "Presion");
    			AddPlot(Brushes.Transparent, "PresionAlto");
    			AddPlot(Brushes.Transparent, "PresionBajo");
    			AddPlot(Brushes.Transparent, "DeltaBarra");
    			AddPlot(Brushes.Transparent, "Pulso");
    			AddPlot(Brushes.Transparent, "DeltaSesion");
    			base.IsAutoScale = true;
    		}
    		else if (base.State == State.Configure)
    		{
    			AddDataSeries(BarsPeriodType.Tick, 1);
    		}
    		else if (base.State == State.DataLoaded)
    		{
    			lock (candado)
    			{
    				ReiniciarTodo();
    			}
    			esPrimariaTemporal = EsPrimariaBasadaEnTiempo();
    			VerificarTickReplay();
    		}
    		else if (base.State == State.Terminated)
    		{
    			LiberarFormatos();
    		}
    	}
    
    	private void AsegurarFormatos()
    	{
    		float escalaEfectiva = EscalaHudEfectiva();
    		if (fmtTitulo == null || double.IsNaN(lastHudRenderScale)
    			|| Math.Abs(lastHudRenderScale - escalaEfectiva) > 0.01)
    		{
    			if (fmtTitulo != null)
    				LiberarFormatos();

    			lastHudRenderScale = escalaEfectiva;
    			float num = (float)Acotar(13.0 * escalaEfectiva, 5.5, 26.0);
    			fmtTitulo = new TextFormat(Globals.DirectWriteFactory, "Segoe UI", SharpDX.DirectWrite.FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Normal, num + 1f);
    			fmtEtiqueta = new TextFormat(Globals.DirectWriteFactory, "Segoe UI", SharpDX.DirectWrite.FontWeight.DemiBold, SharpDX.DirectWrite.FontStyle.Normal, num - 2f);
    			fmtValor = new TextFormat(Globals.DirectWriteFactory, "Consolas", SharpDX.DirectWrite.FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Normal, num);
    			fmtValorDer = new TextFormat(Globals.DirectWriteFactory, "Consolas", SharpDX.DirectWrite.FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Normal, num);
    			fmtValorDer.TextAlignment = SharpDX.DirectWrite.TextAlignment.Trailing;
    			fmtCentro = new TextFormat(Globals.DirectWriteFactory, "Consolas", SharpDX.DirectWrite.FontWeight.DemiBold, SharpDX.DirectWrite.FontStyle.Normal, num - 2.5f);
    			fmtCentro.TextAlignment = SharpDX.DirectWrite.TextAlignment.Center;
    			fmtCentro.ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center;
    			fmtTitulo.ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center;
    			fmtEtiqueta.ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center;
    			fmtValor.ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center;
    			fmtValorDer.ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center;
    		}
    	}
    
    	private void LiberarFormatos()
    	{
    		if (fmtTitulo != null)
    		{
    			fmtTitulo.Dispose();
    			fmtTitulo = null;
    		}
    		if (fmtEtiqueta != null)
    		{
    			fmtEtiqueta.Dispose();
    			fmtEtiqueta = null;
    		}
    		if (fmtValor != null)
    		{
    			fmtValor.Dispose();
    			fmtValor = null;
    		}
    		if (fmtValorDer != null)
    		{
    			fmtValorDer.Dispose();
    			fmtValorDer = null;
    		}
    		if (fmtCentro != null)
    		{
    			fmtCentro.Dispose();
    			fmtCentro = null;
    		}
    		lastHudRenderScale = double.NaN;
    	}
    
    	private static bool EsFinitoPositivo(double v)
    	{
    		if (!double.IsNaN(v) && !double.IsInfinity(v))
    		{
    			return v > 0.0;
    		}
    		return false;
    	}
    
    	private static double Acotar(double v, double min, double max)
    	{
    		if (!(v < min))
    		{
    			if (!(v > max))
    			{
    				return v;
    			}
    			return max;
    		}
    		return min;
    	}

    	private float AnchoPanelAjustado(float anchoIdeal, float escala)
    	{
    		if (base.ChartPanel == null)
    			return anchoIdeal;

    		float disponible = Math.Max(80f, (float)base.ChartPanel.W - 24f * escala);
    		return Math.Min(anchoIdeal, disponible);
    	}

	private float EscalaHudEfectiva()
	{
		float solicitada = (float)Acotar(EscalaPanel, 0.7, 2.0);
		if (base.ChartPanel == null || base.ChartPanel.H <= 0)
			return solicitada;

		// El nuevo HUD vertical se apoya en el borde inferior. Se reduce de
		// forma proporcional cuando el subpanel es bajo, sin desaparecer.
		float espacioVertical = Math.Max(32f, (float)base.ChartPanel.H);
		float ajustada = Math.Min(solicitada, espacioVertical / 182f);
		return Math.Max(0.32f, ajustada);
	}
    
    	private static double RazonFirmada(double numerador, double denominador)
    	{
    		if (!(Math.Abs(denominador) < 1E-10))
    		{
    			return numerador / denominador;
    		}
    		return 0.0;
    	}
    
    	private bool EsPrimariaBasadaEnTiempo()
    	{
    		if (base.BarsPeriod == null)
    		{
    			return false;
    		}
    		switch (base.BarsPeriod.BarsPeriodType)
    		{
    		case BarsPeriodType.Second:
    		case BarsPeriodType.Minute:
    		case BarsPeriodType.Day:
    		case BarsPeriodType.Week:
    		case BarsPeriodType.Month:
    		case BarsPeriodType.Year:
    			return true;
    		default:
    			return false;
    		}
    	}
    
    	private void VerificarTickReplay()
    	{
    		if (!tickReplayVerificado)
    		{
    			tickReplayVerificado = true;
    			try
    			{
    				tickReplayActivo = base.Bars != null && base.Bars.IsTickReplay;
    			}
    			catch
    			{
    				tickReplayActivo = false;
    			}
    			if (!tickReplayActivo && !avisoTickReplayEmitido)
    			{
    				avisoTickReplayEmitido = true;
    				Print("ILT-DEV-BSP · Tick Replay inactivo. En tiempo real la clasificación es exacta igualmente;");
    				Print("          el histórico se reconstruye con tick rule y persistencia en ticks cero,");
    				Print("          que es determinista y reproducible entre equipos.");
    			}
    		}
    	}
    
    	private void ReiniciarTodo()
    	{
    		velas.Clear();
    		bracket.Clear();
    		bracketVolumen = 0L;
    		bracketDelta = 0.0;
    		bracketMetrica = 0.0;
    		bracketInicio = DateTime.MinValue;
    		bracketFin = DateTime.MinValue;
    		precioAnterior = double.NaN;
    		direccionAnterior = 0;
    		bidVivo = double.NaN;
    		askVivo = double.NaN;
    		totalPrints = 0L;
    		printsTickCero = 0L;
		printsComparables = 0L;
		printsCoincidentes = 0L;
		mapaDeltaEth.Clear();
		mapaDeltaRth.Clear();
		fechaOperacionMapa = DateTime.MinValue;
		ReiniciarSesion();
    		libroSesiones.Clear();
    		sesionActiva = null;
    		barraUltimoResetSesion = -1;
    		ultimaBarraTickHistorico = -1;
    		ultimoIndiceMapeado = -1;
    		aperturaObservada = false;
    	}
    
    	private void ReiniciarSesion()
    	{
    		deltaSesion = 0.0;
    		volumenSesionCompra = 0L;
    		volumenSesionVenta = 0L;
    		sesionCotizacion = 0L;
    		sesionTickRule = 0L;
    		sesionPersistencia = 0L;
    		sesionSinClasificar = 0L;
    		emaIntensidad = 0.0;
		pulsoActual = 0.0;
	}

	private static DateTime ObtenerFechaOperacionMapa(DateTime tiempo)
	{
		int hhmm = tiempo.Hour * 100 + tiempo.Minute;
		return hhmm >= 1800 ? tiempo.Date.AddDays(1.0) : tiempo.Date;
	}

	private static TipoSesionMapaDelta ObtenerTipoSesionMapa(DateTime tiempo)
	{
		int hhmm = tiempo.Hour * 100 + tiempo.Minute;
		if (hhmm >= 1800 || hhmm < 930)
			return TipoSesionMapaDelta.ETH;
		if (hhmm >= 930 && hhmm < 1600)
			return TipoSesionMapaDelta.RTH;
		return TipoSesionMapaDelta.Ninguna;
	}

	private void AcumularMapaDelta(DateTime tiempo, double precio, long volumen, int direccion)
	{
		if (direccion == 0 || volumen <= 0 || !EsFinitoPositivo(precio))
			return;

		DateTime fecha = ObtenerFechaOperacionMapa(tiempo);
		if (fechaOperacionMapa == DateTime.MinValue || fecha != fechaOperacionMapa)
		{
			mapaDeltaEth.Clear();
			mapaDeltaRth.Clear();
			fechaOperacionMapa = fecha;
		}

		TipoSesionMapaDelta tipo = ObtenerTipoSesionMapa(tiempo);
		Dictionary<long, double> mapa = tipo == TipoSesionMapaDelta.ETH
			? mapaDeltaEth
			: (tipo == TipoSesionMapaDelta.RTH ? mapaDeltaRth : null);
		if (mapa == null)
			return;

		double tick = Math.Max(1E-10, base.TickSize);
		long precioTicks = (long)Math.Round(precio / tick, MidpointRounding.AwayFromZero);
		double anterior;
		mapa.TryGetValue(precioTicks, out anterior);
		mapa[precioTicks] = anterior + direccion * (double)volumen;
	}
    
    	private int ClasificarAgresor(double precio, double bid, double ask, bool enVivo, out int metodo)
    	{
    		int num = 0;
    		if (EsFinitoPositivo(precioAnterior))
    		{
    			if (precio > precioAnterior)
    			{
    				num = 1;
    			}
    			else if (precio < precioAnterior)
    			{
    				num = -1;
    			}
    		}
    		totalPrints++;
    		if (EsFinitoPositivo(precioAnterior) && num == 0)
    		{
    			printsTickCero++;
    		}
    		int num2 = 0;
    		if (enVivo && Politica == 1 && EsFinitoPositivo(bid) && EsFinitoPositivo(ask) && ask >= bid)
    		{
    			double num3 = base.TickSize * 1E-06;
    			if (precio >= ask - num3)
    			{
    				num2 = 1;
    			}
    			else if (precio <= bid + num3)
    			{
    				num2 = -1;
    			}
    		}
    		if (num2 != 0 && num != 0)
    		{
    			printsComparables++;
    			if (num2 == num)
    			{
    				printsCoincidentes++;
    			}
    		}
    		if (num2 != 0)
    		{
    			metodo = 0;
    			return num2;
    		}
    		if (num != 0)
    		{
    			metodo = 1;
    			return num;
    		}
    		if (direccionAnterior != 0)
    		{
    			metodo = 2;
    			return direccionAnterior;
    		}
    		metodo = 3;
    		return 0;
    	}
    
    	private int ResolverIndiceBarra(DateTime tiempo, bool enVivo)
    	{
    		if (base.CurrentBars == null || base.CurrentBars.Length == 0 || base.CurrentBars[0] < 0)
    		{
    			return -1;
    		}
    		int num = base.CurrentBars[0];
    		int num2 = (esPrimariaTemporal ? MapearPorTiempoDeBarra(tiempo, num) : ((!enVivo && !tickReplayActivo) ? MapearPorTiempoDeBarra(tiempo, num) : num));
    		if (num2 < 0)
    		{
    			return -1;
    		}
    		if (num2 > num + 1)
    		{
    			num2 = num + 1;
    		}
    		if (!enVivo && ultimoIndiceMapeado >= 0 && num2 < ultimoIndiceMapeado)
    		{
    			num2 = ultimoIndiceMapeado;
    		}
    		ultimoIndiceMapeado = num2;
    		return num2;
    	}
    
    	private int MapearPorTiempoDeBarra(DateTime tiempo, int ultima)
    	{
    		int i = ((ultimoIndiceMapeado >= 0) ? ultimoIndiceMapeado : 0);
    		if (i > ultima)
    		{
    			i = ultima;
    		}
    		if (i < 0)
    		{
    			i = 0;
    		}
    		for (; i < ultima; i++)
    		{
    			int num = ultima - i;
    			if (num < 0 || num > ultima)
    			{
    				break;
    			}
    			DateTime dateTime;
    			try
    			{
    				dateTime = base.Times[0][num];
    			}
    			catch
    			{
    				break;
    			}
    			if (tiempo <= dateTime)
    			{
    				break;
    			}
    		}
    		if (i == ultima)
    		{
    			try
    			{
    				if (tiempo > base.Times[0][0])
    				{
    					i = ultima + 1;
    				}
    			}
    			catch
    			{
    			}
    		}
    		return i;
    	}
    
    	protected override void OnBarUpdate()
    	{
    		if (base.BarsInProgress == 0)
    		{
    			if (base.CurrentBars[0] < 0)
    			{
    				return;
    			}
    			lock (candado)
    			{
    				VelaPresion velaPresion = AsegurarVela(base.CurrentBars[0]);
    				if (velaPresion != null)
    				{
    					PublicarPlots(base.CurrentBars[0], velaPresion);
    				}
    				return;
    			}
    		}
    		if (base.BarsInProgress == 1 && base.State != State.Realtime && base.CurrentBars.Length >= 2 && base.CurrentBars[1] >= 0 && base.CurrentBars[1] != ultimaBarraTickHistorico)
    		{
    			ultimaBarraTickHistorico = base.CurrentBars[1];
    			ProcesarPrint(base.Times[1][0], base.Closes[1][0], Math.Max(1L, Convert.ToInt64(base.Volumes[1][0])), double.NaN, double.NaN, enVivo: false);
    		}
    	}
    
    	protected override void OnMarketData(MarketDataEventArgs e)
    	{
    		if (e == null || base.State != State.Realtime)
    		{
    			return;
    		}
    		if (e.MarketDataType == MarketDataType.Bid)
    		{
    			if (EsFinitoPositivo(e.Price))
    			{
    				bidVivo = e.Price;
    			}
    		}
    		else if (e.MarketDataType == MarketDataType.Ask)
    		{
    			if (EsFinitoPositivo(e.Price))
    			{
    				askVivo = e.Price;
    			}
    		}
    		else if (e.MarketDataType == MarketDataType.Last)
    		{
    			double bid = (EsFinitoPositivo(e.Bid) ? e.Bid : bidVivo);
    			double ask = (EsFinitoPositivo(e.Ask) ? e.Ask : askVivo);
    			ProcesarPrint(e.Time, e.Price, Math.Max(1L, e.Volume), bid, ask, enVivo: true);
    		}
    	}
    
    	private void ProcesarPrint(DateTime tiempo, double precio, long volumen, double bid, double ask, bool enVivo)
    	{
    		if (!EsFinitoPositivo(precio) || volumen <= 0)
    		{
    			return;
    		}
    		lock (candado)
    		{
    			int num = ResolverIndiceBarra(tiempo, enVivo);
    			if (num < 0)
    			{
    				return;
    			}
    			ComprobarSesion(num);
    			VelaPresion velaPresion = AsegurarVela(num);
    			if (velaPresion == null)
    			{
    				return;
    			}
    			int metodo;
    			int num2 = ClasificarAgresor(precio, bid, ask, enVivo, out metodo);
    			bool flag = volumen >= Math.Max(0, VolumenMinimoPorTrade);
    			switch (metodo)
    			{
    			case 0:
    				velaPresion.ClasifCotizacion++;
    				sesionCotizacion++;
    				break;
    			case 1:
    				velaPresion.ClasifTickRule++;
    				sesionTickRule++;
    				break;
    			case 2:
    				velaPresion.ClasifPersistencia++;
    				sesionPersistencia++;
    				break;
    			default:
    				velaPresion.SinClasificar++;
    				sesionSinClasificar++;
    				break;
    			}
    			if (velaPresion.Trades == 0)
    			{
    				velaPresion.PrimerTiempo = tiempo;
    			}
    			velaPresion.UltimoTiempo = tiempo;
    			velaPresion.Trades++;
    			velaPresion.Contratos += volumen;
    			double num3 = 0.0;
    			double num4 = 0.0;
			if (flag && num2 != 0)
			{
				num3 = ((Metrica == 1) ? ((double)volumen) : 1.0);
				num4 = (double)num2 * num3;
				AcumularMapaDelta(tiempo, precio, volumen, num2);
				velaPresion.DeltaBarra += num4;
    				velaPresion.MetricaBarra += num3;
    				deltaSesion += num4;
    				if (num2 > 0)
    				{
    					velaPresion.TradesCompra++;
    					velaPresion.VolumenCompra += volumen;
    					volumenSesionCompra += volumen;
    				}
    				else
    				{
    					velaPresion.TradesVenta++;
    					velaPresion.VolumenVenta += volumen;
    					volumenSesionVenta += volumen;
    				}
    			}
    			if (BaseBracket == 2 || (flag && num2 != 0))
    			{
    				ActualizarBracket(tiempo, volumen, num3, num4);
    			}
    			RegistrarPrecioSesion(tiempo, precio);
    			precioAnterior = precio;
    			if (num2 != 0)
    			{
    				direccionAnterior = num2;
    			}
    			ActualizarVela(num, velaPresion);
    			PublicarPlots(num, velaPresion);
    		}
    	}
    
    	private void ActualizarBracket(DateTime tiempo, long volumen, double metrica, double metricaFirmada)
    	{
    		bracket.Enqueue(new EntradaBracket
    		{
    			Volumen = volumen,
    			Metrica = metrica,
    			MetricaFirmada = metricaFirmada
    		});
    		bracketVolumen += volumen;
    		bracketDelta += metricaFirmada;
    		bracketMetrica += metrica;
    		bracketFin = tiempo;
    		if (bracketInicio == DateTime.MinValue)
    		{
    			bracketInicio = tiempo;
    		}
    		long num = Math.Max(1L, UmbralBracket);
    		while (bracketVolumen > num && bracket.Count > 1)
    		{
    			EntradaBracket entradaBracket = bracket.Dequeue();
    			bracketVolumen -= entradaBracket.Volumen;
    			bracketDelta -= entradaBracket.MetricaFirmada;
    			bracketMetrica -= entradaBracket.Metrica;
    		}
    		if (bracket.Count == 0)
    		{
    			bracketInicio = tiempo;
    		}
    	}
    
    	private double ValorPresion(VelaPresion vela)
    	{
    		if (ModoDelta == 1)
    			return vela.DeltaBarra;
    		if (ModoDelta == 2)
    			return deltaSesion;
    		return bracketDelta;
    	}
    
    	private VelaPresion AsegurarVela(int indice)
    	{
    		if (indice < 0)
    		{
    			return null;
    		}
    		while (velas.Count <= indice)
    		{
    			velas.Add(new VelaPresion());
    		}
    		return velas[indice];
    	}
    
    	private void ActualizarVela(int indice, VelaPresion vela)
    	{
    		ActualizarEstado(vela.PorVela, vela.DeltaBarra);
    		ActualizarEstado(vela.Acumulado, deltaSesion);
    		ActualizarEstado(vela.Bracket, bracketDelta);
    		SincronizarModo(vela);
    		ActualizarPulso(vela);
    	}

    	private static void ActualizarEstado(EstadoOHLC estado, double valor)
    	{
    		if (double.IsNaN(valor) || double.IsInfinity(valor))
    			valor = estado.Iniciada ? estado.Close : 0.0;

    		if (!estado.Iniciada)
    		{
    			estado.Iniciada = true;
    			estado.Open = valor;
    			estado.High = valor;
    			estado.Low = valor;
    		}

    		estado.Close = valor;
    		if (valor > estado.High)
    			estado.High = valor;
    		if (valor < estado.Low)
    			estado.Low = valor;
    	}

    	private void SincronizarModo(VelaPresion vela)
    	{
    		if (vela == null)
    			return;

    		EstadoOHLC estado = ModoDelta == 1
    			? vela.PorVela
    			: (ModoDelta == 2 ? vela.Acumulado : vela.Bracket);

    		vela.Iniciada = estado.Iniciada;
    		vela.Open = estado.Open;
    		vela.High = estado.High;
    		vela.Low = estado.Low;
    		vela.Close = estado.Close;
    	}
    
    	private void ActualizarPulso(VelaPresion vela)
    	{
    		long volumenCompra = vela.VolumenCompra;
    		long volumenVenta = vela.VolumenVenta;
    		long num = volumenCompra + volumenVenta;
    		vela.Desequilibrio = ((num > 0) ? Acotar(RazonFirmada(volumenCompra - volumenVenta, num), -1.0, 1.0) : 0.0);
    		double num2 = ((vela.UltimoTiempo > vela.PrimerTiempo) ? (vela.UltimoTiempo - vela.PrimerTiempo).TotalSeconds : 0.0);
    		double num3 = ((num2 > 0.05) ? ((double)vela.Contratos / num2) : 0.0);
    		if (num3 > 0.0)
    		{
    			emaIntensidad = ((emaIntensidad <= 0.0) ? num3 : (emaIntensidad + 0.04 * (num3 - emaIntensidad)));
    		}
    		double num4 = ((emaIntensidad > 0.0 && num3 > 0.0) ? Acotar(num3 / emaIntensidad, 0.3, 1.8) : 1.0);
    		pulsoActual = Acotar(100.0 * vela.Desequilibrio * num4, -100.0, 100.0);
    		vela.Pulso = pulsoActual;
    	}
    
    	private void ComprobarSesion(int indice)
    	{
    		if (indice < 0 || indice == barraUltimoResetSesion)
    		{
    			return;
    		}
    		bool flag;
    		try
    		{
    			flag = indice == 0 || base.BarsArray[0].IsFirstBarOfSessionByIndex(indice);
    		}
    		catch
    		{
    			flag = indice == 0;
    		}
    		if (flag)
    		{
    			barraUltimoResetSesion = indice;
    			ArchivarSesion();
    			aperturaObservada = true;
    			if (ReiniciarEnSesion)
    			{
    				ReiniciarSesion();
    				bracket.Clear();
    				bracketVolumen = 0L;
    				bracketDelta = 0.0;
    				bracketMetrica = 0.0;
    				bracketInicio = DateTime.MinValue;
    				bracketFin = DateTime.MinValue;
    			}
    		}
    	}
    
    	private void ArchivarSesion()
    	{
    		if (sesionActiva == null || !sesionActiva.TienePrecio)
    		{
    			sesionActiva = null;
    			return;
    		}
    		sesionActiva.Delta = deltaSesion;
    		if (sesionActiva.Completa)
    		{
    			libroSesiones.Add(sesionActiva);
    			while (libroSesiones.Count > 12)
    			{
    				libroSesiones.RemoveAt(0);
    			}
    		}
    		sesionActiva = null;
    	}
    
    	private void RegistrarPrecioSesion(DateTime tiempo, double precio)
    	{
    		if (sesionActiva == null)
    		{
    			sesionActiva = new RegistroSesion
    			{
    				Inicio = tiempo,
    				PrecioApertura = precio,
    				PrecioCierre = precio,
    				TienePrecio = true,
    				Completa = aperturaObservada
    			};
    		}
    		else
    		{
    			sesionActiva.PrecioCierre = precio;
    		}
    	}
    
    	private static string DescribirContexto(double delta, double movimientoTicks)
    	{
    		if (Math.Abs(movimientoTicks) < 1.0)
    		{
    			return "PLANO";
    		}
    		bool flag = movimientoTicks > 0.0;
    		bool flag2 = delta > 0.0;
    		if (flag)
    		{
    			if (!flag2)
    			{
    				return "ABSORCION ALCISTA";
    			}
    			return "INICIATIVA ALCISTA";
    		}
    		if (!flag2)
    		{
    			return "INICIATIVA BAJISTA";
    		}
    		return "ABSORCION BAJISTA";
    	}
    
    	private static string FormatoCompacto(double valor)
    	{
    		double num = Math.Abs(valor);
    		string text = ((valor > 0.0) ? "+" : ((valor < 0.0) ? "-" : ""));
    		if (num >= 1000000.0)
    		{
    			return text + (num / 1000000.0).ToString("0.0", CultureInfo.InvariantCulture) + "M";
    		}
    		if (num >= 1000.0)
    		{
    			return text + (num / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + "k";
    		}
    		return text + num.ToString("0", CultureInfo.InvariantCulture);
    	}

    	private void PublicarPlots(int indice, VelaPresion vela)
    	{
    		if (indice >= 0 && base.CurrentBars[0] >= 0)
    		{
    			int num = base.CurrentBars[0] - indice;
    			if (num >= 0 && num <= base.CurrentBars[0])
    			{
    				double num2 = (vela.Iniciada ? vela.Close : 0.0);
    				base.Values[0][num] = num2;
    				base.Values[1][num] = (vela.Iniciada ? vela.High : num2);
    				base.Values[2][num] = (vela.Iniciada ? vela.Low : num2);
    				base.Values[3][num] = vela.DeltaBarra;
    				base.Values[4][num] = vela.Pulso;
    				base.Values[5][num] = deltaSesion;
    			}
    		}
    	}
    
    	public override void OnCalculateMinMax()
    	{
    		double num = double.MaxValue;
    		double num2 = double.MinValue;
    		lock (candado)
    		{
    			if (velas.Count == 0 || base.ChartBars == null)
    			{
    				base.MinValue = -1.0;
    				base.MaxValue = 1.0;
    				return;
    			}
    			int num3 = Math.Max(0, base.ChartBars.FromIndex);
    			int num4 = Math.Min(base.ChartBars.ToIndex, velas.Count - 1);
    			for (int i = num3; i <= num4; i++)
    			{
    				VelaPresion velaPresion = velas[i];
    				if (velaPresion != null && velaPresion.Iniciada)
    				{
    					if (velaPresion.Low < num)
    					{
    						num = velaPresion.Low;
    					}
    					if (velaPresion.High > num2)
    					{
    						num2 = velaPresion.High;
    					}
    				}
    			}
    		}
    		if (num > num2)
    		{
    			base.MinValue = -1.0;
    			base.MaxValue = 1.0;
    			return;
    		}
    		if (num > 0.0)
    		{
    			num = 0.0;
    		}
    		if (num2 < 0.0)
    		{
    			num2 = 0.0;
    		}
    		double num5 = Math.Max((num2 - num) * 0.06, base.TickSize);
    		base.MinValue = num - num5;
    		base.MaxValue = num2 + num5;
    	}
    
    	private void CoreOnRender(ChartControl chartControl, ChartScale chartScale)
    	{
    		if (chartControl == null || chartScale == null || base.Bars == null || base.RenderTarget == null)
    		{
    			return;
    		}
    		if (MostrarHud || MostrarLibroSesiones)
    		{
    			lock (candado)
    			{
    				if (MostrarHud)
    					DibujarPanel();
    				else
    					DibujarLibroSesionesIndependiente();
    			}
    		}
    		if (!MostrarVelas)
    		{
    			return;
    		}
    		SharpDX.Direct2D1.Brush brush = null;
    		SharpDX.Direct2D1.Brush brush2 = null;
    		SharpDX.Direct2D1.Brush brush3 = null;
    		SharpDX.Direct2D1.Brush brush4 = null;
    		try
    		{
    			brush = ColorCompra.ToDxBrush(base.RenderTarget);
    			brush2 = ColorVenta.ToDxBrush(base.RenderTarget);
    			brush3 = ColorNeutro.ToDxBrush(base.RenderTarget);
    			brush4 = ColorLineaCero.ToDxBrush(base.RenderTarget);
    			if (MostrarLineaCero)
    			{
    				float y = chartScale.GetYByValue(0.0);
    				base.RenderTarget.DrawLine(new Vector2(base.ChartPanel.X, y), new Vector2(base.ChartPanel.X + base.ChartPanel.W, y), brush4, 1f);
    			}
    			int num = Math.Max(0, base.ChartBars.FromIndex);
    			int num2 = Math.Min(base.ChartBars.ToIndex, velas.Count - 1);
    			double num3;
    			try
    			{
    				num3 = chartControl.GetBarPaintWidth(base.ChartBars);
    			}
    			catch
    			{
    				num3 = chartControl.BarWidth * 2.0 + (double)chartControl.Properties.BarDistance;
    			}
    			float num4 = (float)Math.Max(1.0, num3 * Acotar(AnchoVelaPorcentaje, 10.0, 100.0) / 100.0);
    			float num5 = num4 * 0.5f;
    			lock (candado)
    			{
    				for (int i = num; i <= num2; i++)
    				{
    					VelaPresion velaPresion = velas[i];
    					if (velaPresion != null && velaPresion.Iniciada)
    					{
    						float num6 = chartControl.GetXByBarIndex(base.ChartBars, i);
    						float num7 = chartScale.GetYByValue(velaPresion.Open);
    						float num8 = chartScale.GetYByValue(velaPresion.Close);
    						float y2 = chartScale.GetYByValue(velaPresion.High);
    						float y3 = chartScale.GetYByValue(velaPresion.Low);
    						double num9 = velaPresion.Close - velaPresion.Open;
    						SharpDX.Direct2D1.Brush brush5 = ((num9 > 0.0) ? brush : ((num9 < 0.0) ? brush2 : brush3));
    						base.RenderTarget.DrawLine(new Vector2(num6, y2), new Vector2(num6, y3), brush5, 1f);
    						float y4 = Math.Min(num7, num8);
    						float num10 = Math.Abs(num8 - num7);
    						if (num10 < 1f)
    						{
    							num10 = 1f;
    						}
    						base.RenderTarget.FillRectangle(new RectangleF(num6 - num5, y4, num4, num10), brush5);
    					}
    				}
    			}
    		}
    		catch (Exception)
    		{
    		}
    		finally
    		{
    			if (brush != null) brush.Dispose();
    			if (brush2 != null) brush2.Dispose();
    			if (brush3 != null) brush3.Dispose();
    			if (brush4 != null) brush4.Dispose();
    		}
    	}
    
    	private void DibujarPanel()
    	{
    		AsegurarFormatos();
    		SharpDX.Direct2D1.Brush brush = null;
    		SharpDX.Direct2D1.Brush brush2 = null;
    		SharpDX.Direct2D1.Brush brush3 = null;
    		SharpDX.Direct2D1.Brush brush4 = null;
    		SharpDX.Direct2D1.Brush brush5 = null;
    		SharpDX.Direct2D1.Brush brush6 = null;
    		SharpDX.Direct2D1.Brush brush7 = null;
    		try
    		{
    			brush = new SharpDX.Direct2D1.SolidColorBrush(base.RenderTarget, new Color4(0.04f, 0.05f, 0.07f, 0.88f));
    			brush2 = new SharpDX.Direct2D1.SolidColorBrush(base.RenderTarget, new Color4(0.35f, 0.38f, 0.44f, 0.9f));
    			brush3 = ColorTexto.ToDxBrush(base.RenderTarget);
    			brush4 = new SharpDX.Direct2D1.SolidColorBrush(base.RenderTarget, new Color4(0.55f, 0.58f, 0.63f, 1f));
    			brush5 = ColorCompra.ToDxBrush(base.RenderTarget);
    			brush6 = ColorVenta.ToDxBrush(base.RenderTarget);
    			brush7 = ColorNeutro.ToDxBrush(base.RenderTarget);
    			float num = EscalaHudEfectiva();
    			float num2 = 24f * num;
    			float num3 = 12f * num;
    			float num4 = AnchoPanelAjustado(420f * num, num);
    			bool flag = MostrarLibroSesiones && libroSesiones.Count > 0;
    			float num5 = (flag ? (num2 * 4.9f) : 0f);
    			int num6 = 2 + (MostrarPulso ? 1 : 0);
    			float height = num3 * 2f + num2 * 1.15f + num2 * 0.55f + num2 * (float)num6 + num2 + num5;
    			float num7 = (float)(base.ChartPanel.X + base.ChartPanel.W) - num4 - 12f * num;
    			float num8 = (float)base.ChartPanel.Y + 10f * num;
    			RectangleF rect = new RectangleF(num7, num8, num4, height);
    			base.RenderTarget.FillRectangle(rect, brush);
    			base.RenderTarget.DrawRectangle(rect, brush2, 1.2f);
    			float num9 = num7 + num3;
    			float num10 = num8 + num3;
    			float num11 = num4 - num3 * 2f;
    			float num12 = num11 * 0.19f;
    			float num13 = num11 * 0.44f;
    			float num14 = num11 * 0.17f;
    			float width = num11 * 0.2f;
    			float num15 = num9 + num12;
    			float num16 = num15 + num13;
    			float num17 = num16 + num14;
    			base.RenderTarget.DrawText("ILT-DEV-BSP", fmtTitulo, new RectangleF(num9, num10, num11 * 0.35f, num2), brush3);
    			string text = ((ModoDelta == 3) ? ("BRACKET " + Math.Max(1L, UmbralBracket).ToString("#,0", CultureInfo.InvariantCulture)) : ((ModoDelta == 2) ? "ACUMULADO SESION" : "DELTA POR VELA"));
    			base.RenderTarget.DrawText(text, fmtEtiqueta, new RectangleF(num9 + num11 * 0.35f, num10, num11 * 0.65f, num2), brush4);
    			num10 += num2 * 1.15f;
    			if (ModoDelta == 3)
    			{
    				long num18 = Math.Max(1L, UmbralBracket);
    				float num19 = (float)Acotar((double)bracketVolumen / (double)num18, 0.0, 1.0);
    				float height2 = Math.Max(3f, num2 * 0.17f);
    				base.RenderTarget.FillRectangle(new RectangleF(num9, num10, num11, height2), brush7);
    				base.RenderTarget.FillRectangle(new RectangleF(num9, num10, num11 * num19, height2), brush4);
    			}
    			num10 += num2 * 0.55f;

    			VelaPresion velaPresion = null;
    			for (int num20 = Math.Min(base.CurrentBars[0], velas.Count - 1); num20 >= 0; num20--)
    			{
    				if (velas[num20] != null && velas[num20].Iniciada)
    				{
    					velaPresion = velas[num20];
    					break;
    				}
    			}
    			double valor = velaPresion == null ? 0.0 : velaPresion.Close;
    			double escala = Math.Max(Math.Abs(base.MaxValue), Math.Abs(base.MinValue));
    			DibujarMedidor("PRESION", valor, escala, FormatoCompacto(valor), num9, num10, num12, num15, num13, num16, num14, num2, brush3, brush4, brush5, brush6, brush7);
    			num10 += num2;
    			if (MostrarPulso)
    			{
    				string text2 = ((Math.Abs(pulsoActual) < 10.0) ? "PLANO" : ((pulsoActual > 0.0) ? "COMPRA" : "VENTA"));
    				DibujarMedidor("PULSO", pulsoActual, 100.0, pulsoActual.ToString("+0;-0;0", CultureInfo.InvariantCulture), num9, num10, num12, num15, num13, num16, num14, num2, brush3, brush4, brush5, brush6, brush7);
    				base.RenderTarget.DrawText(text2, fmtEtiqueta, new RectangleF(num17 + num3 * 0.4f, num10, width, num2), (Math.Abs(pulsoActual) < 10.0) ? brush4 : ((pulsoActual > 0.0) ? brush5 : brush6));
    				num10 += num2;
    			}
    			double num21 = Math.Abs(deltaSesion);
    			for (int i = 0; i < libroSesiones.Count; i++)
    			{
    				if (libroSesiones[i] != null)
    				{
    					num21 = Math.Max(num21, Math.Abs(libroSesiones[i].Delta));
    				}
    			}
    			DibujarMedidor("SESION", deltaSesion, num21, FormatoCompacto(deltaSesion), num9, num10, num12, num15, num13, num16, num14, num2, brush3, brush4, brush5, brush6, brush7);
    			num10 += num2;
    			base.RenderTarget.DrawText("CLASIF", fmtEtiqueta, new RectangleF(num9, num10, num12, num2), brush4);
    			long num22 = sesionCotizacion + sesionTickRule + sesionPersistencia + sesionSinClasificar;
    			if (num22 > 0)
    			{
    				float num23 = Math.Max(5f, num2 * 0.3f);
    				float y = num10 + (num2 - num23) * 0.5f;
    				float num24 = (float)((double)sesionCotizacion / (double)num22);
    				float num25 = (float)((double)sesionTickRule / (double)num22);
    				float num26 = (float)((double)sesionPersistencia / (double)num22);
    				base.RenderTarget.FillRectangle(new RectangleF(num15, y, num13, num23), brush7);
    				base.RenderTarget.FillRectangle(new RectangleF(num15, y, num13 * num24, num23), brush5);
    				base.RenderTarget.FillRectangle(new RectangleF(num15 + num13 * num24, y, num13 * num25, num23), brush3);
    				base.RenderTarget.FillRectangle(new RectangleF(num15 + num13 * (num24 + num25), y, num13 * num26, num23), brush4);
    				string text3 = ((printsComparables > 0) ? ((100.0 * (double)printsCoincidentes / (double)printsComparables).ToString("0.0", CultureInfo.InvariantCulture) + "%") : "--");
    				base.RenderTarget.DrawText(text3, fmtValorDer, new RectangleF(num16, num10, num14, num2), brush4);
    				base.RenderTarget.DrawText("acuerdo", fmtEtiqueta, new RectangleF(num17 + num3 * 0.4f, num10, width, num2), brush4);
    			}
    			num10 += num2;
    			if (!flag)
    			{
    				return;
    			}
    			int num27 = Math.Min(Math.Max(1, SesionesEnBalance), libroSesiones.Count);
    			double num28 = 1.0;
    			double num29 = 0.0;
    			for (int j = 0; j < num27; j++)
    			{
    				RegistroSesion registroSesion = libroSesiones[libroSesiones.Count - 1 - j];
    				if (registroSesion != null)
    				{
    					num29 += registroSesion.Delta;
    					num28 = Math.Max(num28, Math.Abs(registroSesion.Delta));
    				}
    			}
    			float num30 = num2 * 0.85f;
    			float num31 = num2 * 2f;
    			float num32 = num2 * 0.85f;
    			float y2 = num10;
    			float num33 = num10 + num30;
    			float num34 = num33 + num31 * 0.5f;
    			float num35 = num33 + num31;
    			float num36 = num11 * 0.62f;
    			float num37 = num36 / (float)Math.Max(1, num27);
    			base.RenderTarget.DrawLine(new Vector2(num9, num34), new Vector2(num9 + num36, num34), brush4, 1f);
    			for (int k = 0; k < num27; k++)
    			{
    				RegistroSesion registroSesion2 = libroSesiones[libroSesiones.Count - 1 - k];
    				if (registroSesion2 != null)
    				{
    					float num38 = num9 + num36 - (float)(k + 1) * num37;
    					float x = num38 + num37 * 0.26f;
    					float width2 = num37 * 0.48f;
    					float num39 = (float)(Math.Abs(registroSesion2.Delta) / num28) * (num31 * 0.42f);
    					if (num39 < 2f)
    					{
    						num39 = 2f;
    					}
    					RectangleF rect2 = ((registroSesion2.Delta >= 0.0) ? new RectangleF(x, num34 - num39, width2, num39) : new RectangleF(x, num34, width2, num39));
    					base.RenderTarget.FillRectangle(rect2, (registroSesion2.Delta >= 0.0) ? brush5 : brush6);
    					base.RenderTarget.DrawText(FormatoCompacto(registroSesion2.Delta), fmtCentro, new RectangleF(num38, y2, num37, num30), (registroSesion2.Delta >= 0.0) ? brush5 : brush6);
    					base.RenderTarget.DrawText("D" + (k + 1).ToString(CultureInfo.InvariantCulture), fmtCentro, new RectangleF(num38, num35, num37, num32), brush4);
    				}
    			}
    			float x2 = num9 + num36 + num3 * 0.5f;
    			float width3 = num11 - num36 - num3 * 0.5f;
    			base.RenderTarget.DrawText("BALANCE " + num27.ToString(CultureInfo.InvariantCulture) + "d", fmtEtiqueta, new RectangleF(x2, num34 - num2 * 0.95f, width3, num2), brush4);
    			base.RenderTarget.DrawText(FormatoCompacto(num29), fmtValor, new RectangleF(x2, num34 - num2 * 0.02f, width3, num2), (num29 > 0.0) ? brush5 : ((num29 < 0.0) ? brush6 : brush4));
    			RegistroSesion registroSesion3 = libroSesiones[libroSesiones.Count - 1];
    			if (registroSesion3 != null)
    			{
    				float y3 = num35 + num32 + num2 * 0.12f;
    				base.RenderTarget.DrawLine(new Vector2(num9, y3), new Vector2(num9 + num11, y3), brush2, 1f);
    				double movimientoTicks = (registroSesion3.PrecioCierre - registroSesion3.PrecioApertura) / Math.Max(1E-10, base.TickSize);
    				string text4 = DescribirContexto(registroSesion3.Delta, movimientoTicks);
    				bool flag2 = text4.StartsWith("ABSOR", StringComparison.Ordinal);
    				base.RenderTarget.DrawText("AYER", fmtEtiqueta, new RectangleF(num9, y3, num12, num2), brush4);
    				base.RenderTarget.DrawText(text4, fmtEtiqueta, new RectangleF(num15, y3, num13 + num14, num2), (!flag2) ? brush3 : ((registroSesion3.Delta > 0.0) ? brush6 : brush5));
    				base.RenderTarget.DrawText(movimientoTicks.ToString("+0;-0;0", CultureInfo.InvariantCulture) + "t", fmtValorDer, new RectangleF(num17, y3, width, num2), brush4);
    			}
    		}
    		catch (Exception)
    		{
    		}
    		finally
    		{
    			if (brush != null) brush.Dispose();
    			if (brush2 != null) brush2.Dispose();
    			if (brush3 != null) brush3.Dispose();
    			if (brush4 != null) brush4.Dispose();
    			if (brush5 != null) brush5.Dispose();
    			if (brush6 != null) brush6.Dispose();
    			if (brush7 != null) brush7.Dispose();
    		}
    	}
    
    	private void DibujarLibroSesionesIndependiente()
    	{
    		AsegurarFormatos();
    		SharpDX.Direct2D1.Brush fondo = null;
    		SharpDX.Direct2D1.Brush borde = null;
    		SharpDX.Direct2D1.Brush texto = null;
    		SharpDX.Direct2D1.Brush tenue = null;
    		SharpDX.Direct2D1.Brush compra = null;
    		SharpDX.Direct2D1.Brush venta = null;
    		try
    		{
    			fondo = new SharpDX.Direct2D1.SolidColorBrush(base.RenderTarget, new Color4(0.04f, 0.05f, 0.07f, 0.9f));
    			borde = new SharpDX.Direct2D1.SolidColorBrush(base.RenderTarget, new Color4(0.35f, 0.38f, 0.44f, 0.95f));
    			texto = ColorTexto.ToDxBrush(base.RenderTarget);
    			tenue = new SharpDX.Direct2D1.SolidColorBrush(base.RenderTarget, new Color4(0.55f, 0.58f, 0.63f, 1f));
    			compra = ColorCompra.ToDxBrush(base.RenderTarget);
    			venta = ColorVenta.ToDxBrush(base.RenderTarget);

    			float escala = EscalaHudEfectiva();
    			float fila = 24f * escala;
    			float margen = 12f * escala;
    			float ancho = AnchoPanelAjustado(360f * escala, escala);
    			int cantidad = Math.Min(Math.Max(1, SesionesEnBalance), libroSesiones.Count);
    			float alto = margen * 2f + fila * (cantidad > 0 ? 4.4f : 2.3f);
    			float x = PosicionPanel == 1
    				? (float)base.ChartPanel.X + 12f * escala
    				: (float)(base.ChartPanel.X + base.ChartPanel.W) - ancho - 12f * escala;
			float y = (float)base.ChartPanel.Y + 10f * escala;
    			RectangleF panel = new RectangleF(x, y, ancho, alto);
    			base.RenderTarget.FillRectangle(panel, fondo);
    			base.RenderTarget.DrawRectangle(panel, borde, 1.2f);

    			float interiorX = x + margen;
    			float interiorY = y + margen;
    			float interiorAncho = ancho - margen * 2f;
    			base.RenderTarget.DrawText("LIBRO DE SESIONES", fmtTitulo,
    				new RectangleF(interiorX, interiorY, interiorAncho, fila), texto);

    			if (cantidad <= 0)
    			{
    				base.RenderTarget.DrawText("SIN SESIONES CERRADAS", fmtEtiqueta,
    					new RectangleF(interiorX, interiorY + fila, interiorAncho, fila), tenue);
    				return;
    			}

    			double maximo = 1.0;
    			double balance = 0.0;
    			for (int i = 0; i < cantidad; i++)
    			{
    				RegistroSesion sesion = libroSesiones[libroSesiones.Count - 1 - i];
    				if (sesion == null)
    					continue;
    				balance += sesion.Delta;
    				maximo = Math.Max(maximo, Math.Abs(sesion.Delta));
    			}

    			float graficoY = interiorY + fila * 1.05f;
    			float graficoAlto = fila * 2.15f;
    			float centroY = graficoY + graficoAlto * 0.5f;
    			float graficoAncho = interiorAncho * 0.64f;
    			float espacio = graficoAncho / Math.Max(1, cantidad);
    			base.RenderTarget.DrawLine(new Vector2(interiorX, centroY),
    				new Vector2(interiorX + graficoAncho, centroY), tenue, 1f);

    			for (int i = 0; i < cantidad; i++)
    			{
    				RegistroSesion sesion = libroSesiones[libroSesiones.Count - 1 - i];
    				if (sesion == null)
    					continue;

    				float celdaX = interiorX + graficoAncho - (i + 1) * espacio;
    				float barraX = celdaX + espacio * 0.28f;
    				float barraAncho = Math.Max(2f, espacio * 0.44f);
    				float barraAlto = (float)(Math.Abs(sesion.Delta) / maximo) * graficoAlto * 0.42f;
    				barraAlto = Math.Max(2f, barraAlto);
    				RectangleF barra = sesion.Delta >= 0.0
    					? new RectangleF(barraX, centroY - barraAlto, barraAncho, barraAlto)
    					: new RectangleF(barraX, centroY, barraAncho, barraAlto);
    				base.RenderTarget.FillRectangle(barra, sesion.Delta >= 0.0 ? compra : venta);
    				base.RenderTarget.DrawText(FormatoCompacto(sesion.Delta), fmtCentro,
    					new RectangleF(celdaX, graficoY - fila * 0.08f, espacio, fila * 0.8f),
    					sesion.Delta >= 0.0 ? compra : venta);
    				base.RenderTarget.DrawText("D" + (i + 1).ToString(CultureInfo.InvariantCulture), fmtCentro,
    					new RectangleF(celdaX, graficoY + graficoAlto - fila * 0.72f, espacio, fila * 0.72f), tenue);
    			}

    			float resumenX = interiorX + graficoAncho + margen * 0.5f;
    			float resumenAncho = interiorAncho - graficoAncho - margen * 0.5f;
    			base.RenderTarget.DrawText("BALANCE " + cantidad.ToString(CultureInfo.InvariantCulture) + "d", fmtEtiqueta,
    				new RectangleF(resumenX, centroY - fila, resumenAncho, fila), tenue);
    			base.RenderTarget.DrawText(FormatoCompacto(balance), fmtValor,
    				new RectangleF(resumenX, centroY, resumenAncho, fila),
    				balance > 0.0 ? compra : (balance < 0.0 ? venta : tenue));

    			RegistroSesion anterior = libroSesiones[libroSesiones.Count - 1];
    			if (anterior != null)
    			{
    				float contextoY = graficoY + graficoAlto + fila * 0.12f;
    				double movimientoTicks = (anterior.PrecioCierre - anterior.PrecioApertura) / Math.Max(1E-10, base.TickSize);
    				string contexto = DescribirContexto(anterior.Delta, movimientoTicks);
    				bool absorcion = contexto.StartsWith("ABSOR", StringComparison.Ordinal);
    				base.RenderTarget.DrawLine(new Vector2(interiorX, contextoY),
    					new Vector2(interiorX + interiorAncho, contextoY), borde, 1f);
    				base.RenderTarget.DrawText("AYER", fmtEtiqueta,
    					new RectangleF(interiorX, contextoY, interiorAncho * 0.16f, fila), tenue);
    				base.RenderTarget.DrawText(contexto, fmtEtiqueta,
    					new RectangleF(interiorX + interiorAncho * 0.16f, contextoY, interiorAncho * 0.62f, fila),
    					absorcion ? (anterior.Delta > 0.0 ? venta : compra) : texto);
    				base.RenderTarget.DrawText(movimientoTicks.ToString("+0;-0;0", CultureInfo.InvariantCulture) + "t", fmtValorDer,
    					new RectangleF(interiorX + interiorAncho * 0.78f, contextoY, interiorAncho * 0.22f, fila), tenue);
    			}
    		}
    		catch (Exception)
    		{
    		}
    		finally
    		{
    			if (fondo != null) fondo.Dispose();
    			if (borde != null) borde.Dispose();
    			if (texto != null) texto.Dispose();
    			if (tenue != null) tenue.Dispose();
    			if (compra != null) compra.Dispose();
    			if (venta != null) venta.Dispose();
    		}
    	}

    	private void DibujarMedidor(string etiqueta, double valor, double escala, string textoValor, float x, float y, float colEtiqueta, float xMedidor, float colMedidor, float xValor, float colValor, float fila, SharpDX.Direct2D1.Brush texto, SharpDX.Direct2D1.Brush tenue, SharpDX.Direct2D1.Brush compra, SharpDX.Direct2D1.Brush venta, SharpDX.Direct2D1.Brush neutro)
    	{
    		base.RenderTarget.DrawText(etiqueta, fmtEtiqueta, new RectangleF(x, y, colEtiqueta, fila), tenue);
    		float num = Math.Max(5f, fila * 0.3f);
    		float num2 = y + (fila - num) * 0.5f;
    		float num3 = xMedidor + colMedidor * 0.5f;
    		base.RenderTarget.FillRectangle(new RectangleF(xMedidor, num2, colMedidor, num), neutro);
    		if (escala > 1E-09)
    		{
    			float num4 = (float)Acotar(valor / escala, -1.0, 1.0);
    			float num5 = Math.Abs(num4) * (colMedidor * 0.5f);
    			if (num5 > 0.5f)
    			{
    				RectangleF rect = (((double)num4 >= 0.0) ? new RectangleF(num3, num2, num5, num) : new RectangleF(num3 - num5, num2, num5, num));
    				base.RenderTarget.FillRectangle(rect, ((double)num4 >= 0.0) ? compra : venta);
    			}
    		}
    		base.RenderTarget.DrawLine(new Vector2(num3, num2 - 2f), new Vector2(num3, num2 + num + 2f), texto, 1f);
    		base.RenderTarget.DrawText(textoValor, fmtValorDer, new RectangleF(xValor, y, colValor, fila), texto);
    	}

        private Grid controlRoot;
        private Grid controlHeader;
        private StackPanel controlBody;
        private ScrollViewer controlScroll;
        private Button configPanelCloseButton;
        private ComboBox modeCombo;
        private ComboBox paletteCombo;
        private Button masterButton;
        private Button pulseButton;
        private Button sessionsButton;
        private Button sideButton;
        private Button fontMinusButton;
        private Button fontPlusButton;
        private Button deltaMapMasterButton;
        private Button deltaMapBarsButton;
        private Button deltaMapLinesButton;
        private Button deltaMapSideButton;
        private Canvas deltaMapOverlay;
        private ChartPanel deltaMapHostPanel;
        private bool deltaMapUpdateQueued;
        private ChartControl hostChartControl;
        private ChartPanel configurationHostPanel;
        private Chart chartWindow;
        private NTMenuItem toolbarButton;
        private bool toolbarButtonAdded;
        private bool controlsBuilt;
        private bool controlsBuildQueued;
        private bool syncingControls;
        private bool configExpanded;
        private bool configUserMoved;
        private bool controlPanelDragging;
        private System.Windows.Point controlDragStart;
        private double controlDragStartLeft;
        private double controlDragStartTop;
        private bool configurationStateLoaded;
        private string configurationStateKey;
        private double lastPanelX = double.NaN;
        private double lastPanelY = double.NaN;
        private double lastPanelWidth = double.NaN;
        private double lastPanelHeight = double.NaN;
        private double lastHudRenderScale = double.NaN;
        private MethodInfo releaseFormatsMethod;
        private PropertyInfo panelUserControlsProperty;
        private const string ToolbarButtonTagPrefix = "ILT_DEV_BSP_TOOLBAR_BUTTON";
        private readonly string toolbarButtonInstanceId = Guid.NewGuid().ToString("N");
        private int toolbarButtonNumber;
        private static readonly object ConfigurationStateFileLock = new object();

        public override string DisplayName
        {
            get { return "ILT-DEV-BSP"; }
        }

        protected override void OnStateChange()
        {
            CoreOnStateChange();

            if (State == State.SetDefaults)
            {
                Name = "ILT-DEV-BSP";
                Description             = "ILT-DEV-BSP con panel de control interactivo";
                ModoPanel               = 3;
                PanelCompleto           = false;
                PulsoPanel              = true;
                LibroSesionesPanel      = false;
                PosicionPanel           = 2;
                TamanoFuentePanel       = 13;
                MapaDeltaCompleto       = true;
                MapaDeltaBarras         = true;
                MapaDeltaLineas         = true;
                PosicionMapaDelta       = 2;
                ConfigPanelX            = -1.0;
                ConfigPanelY            = -1.0;
                ConfigPanelExpanded     = false;
                ConfigPanelStateKey     = string.Empty;
            }
            else if (State == State.DataLoaded)
            {
                ModoDelta = ModoPanel;
                configExpanded = ConfigPanelExpanded;
                configUserMoved = ConfigPanelX >= 0.0 && ConfigPanelY >= 0.0;
                ApplyFontScale(false);
                ApplyVisibilityState();
            }
            else if (State == State.Historical || State == State.Realtime)
            {
                if (ChartControl != null)
                    QueueControlPanelBuild(ChartControl);
            }
            else if (State == State.Terminated)
            {
                PersistConfigurationState();
                ChartControl chart = hostChartControl;
                if (chart != null)
                    chart.Dispatcher.InvokeAsync(new Action(RemoveControlPanel));
                else
                    RemoveControlPanel();
            }
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            // En algunos espacios de trabajo ChartControl todavía es null cuando
            // llega State.Historical. El primer render garantiza la creación.
            QueueControlPanelBuild(chartControl);

            if (RenderTarget == null || ChartPanel == null)
                return;

            bool oldHud = MostrarHud;
            bool oldBook = MostrarLibroSesiones;

            try
            {
                MostrarHud = false;
                MostrarLibroSesiones = false;
                CoreOnRender(chartControl, chartScale);
            }
            finally
            {
                MostrarHud = oldHud;
                MostrarLibroSesiones = oldBook;
            }

            lock (candado)
            {
                if (oldHud)
                    DibujarPanelVertical(oldBook);
                if (oldBook)
                    DibujarLibroSesionesInferior(oldHud);
            }

            DibujarMarcadorDelta(chartScale);
            DibujarMapaDelta(chartControl);
        }

        private void ObtenerGeometriaPanelesInferiores(bool conHud, bool conLibro, float escala,
            out float hudX, out float hudAncho, out float libroX, out float libroAncho)
        {
            float izquierda = (float)ChartPanel.X + 4f * escala;
            float derecha = (float)(ChartPanel.X + ChartPanel.W) - 4f * escala;
            float disponible = Math.Max(80f, derecha - izquierda);
            float separacion = conHud && conLibro ? 4f * escala : 0f;
            hudAncho = conHud ? 224f * escala : 0f;
            libroAncho = conLibro ? 250f * escala : 0f;
            float total = hudAncho + libroAncho + separacion;

            if (total > disponible && total > 0f)
            {
                float factor = disponible / total;
                hudAncho *= factor;
                libroAncho *= factor;
                separacion *= factor;
            }

            hudX = izquierda;
            libroX = izquierda;
            if (PosicionPanel == 1)
            {
                if (conHud)
                {
                    hudX = izquierda;
                    libroX = hudX + hudAncho + separacion;
                }
                else
                {
                    libroX = izquierda;
                }
            }
            else
            {
                if (conHud)
                {
                    hudX = derecha - hudAncho;
                    libroX = hudX - separacion - libroAncho;
                }
                else
                {
                    libroX = derecha - libroAncho;
                }
            }
        }

        private void DibujarPanelVertical(bool conLibro)
        {
            AsegurarFormatos();
            SharpDX.Direct2D1.Brush fondo = null;
            SharpDX.Direct2D1.Brush borde = null;
            SharpDX.Direct2D1.Brush texto = null;
            SharpDX.Direct2D1.Brush tenue = null;
            SharpDX.Direct2D1.Brush compra = null;
            SharpDX.Direct2D1.Brush venta = null;
            SharpDX.Direct2D1.Brush neutro = null;
            try
            {
                fondo = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                    new Color4(0.04f, 0.05f, 0.07f, 0.88f));
                borde = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                    new Color4(0.35f, 0.38f, 0.44f, 0.90f));
                texto = ColorTexto.ToDxBrush(RenderTarget);
                tenue = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                    new Color4(0.55f, 0.58f, 0.63f, 1f));
                compra = ColorCompra.ToDxBrush(RenderTarget);
                venta = ColorVenta.ToDxBrush(RenderTarget);
                neutro = ColorNeutro.ToDxBrush(RenderTarget);

                float escala = EscalaHudEfectiva();
                float x;
                float ancho;
                float libroX;
                float libroAncho;
                ObtenerGeometriaPanelesInferiores(true, conLibro, escala,
                    out x, out ancho, out libroX, out libroAncho);
                float alto = Math.Min((float)ChartPanel.H, 178f * escala);
                float y = (float)(ChartPanel.Y + ChartPanel.H) - alto;
                RectangleF panel = new RectangleF(x, y, ancho, alto);
                RenderTarget.FillRectangle(panel, fondo);
                RenderTarget.DrawRectangle(panel, borde, 1f);

                float margen = Math.Max(4f, 8f * escala);
                float fila = Math.Max(10f, 20f * escala);
                float interiorX = x + margen;
                float interiorAncho = Math.Max(20f, ancho - margen * 2f);
                RenderTarget.DrawText("ILT-DEV-BSP", fmtTitulo,
                    new RectangleF(interiorX, y + 2f * escala, interiorAncho * 0.42f, fila), texto);
                string modo = ModoDelta == 3
                    ? "BRACKET " + Math.Max(1L, UmbralBracket).ToString("#,0", CultureInfo.InvariantCulture)
                    : (ModoDelta == 2 ? "ACUMULADO" : "POR VELA");
                RenderTarget.DrawText(modo, fmtEtiqueta,
                    new RectangleF(interiorX + interiorAncho * 0.42f, y + 2f * escala,
                        interiorAncho * 0.58f, fila), tenue);

                float cabeceraFin = y + fila + 4f * escala;
                RenderTarget.DrawLine(new Vector2(interiorX, cabeceraFin),
                    new Vector2(interiorX + interiorAncho, cabeceraFin), borde, 1f);

                VelaPresion ultima = null;
                for (int i = Math.Min(CurrentBars[0], velas.Count - 1); i >= 0; i--)
                {
                    if (velas[i] != null && velas[i].Iniciada)
                    {
                        ultima = velas[i];
                        break;
                    }
                }

                double presion = ultima == null ? 0.0 : ultima.Close;
                double escalaPresion = Math.Max(1.0, Math.Max(Math.Abs(MinValue), Math.Abs(MaxValue)));
                double escalaSesion = Math.Max(1.0, Math.Abs(deltaSesion));
                for (int i = 0; i < libroSesiones.Count; i++)
                    if (libroSesiones[i] != null)
                        escalaSesion = Math.Max(escalaSesion, Math.Abs(libroSesiones[i].Delta));

                int columnas = MostrarPulso ? 3 : 2;
                float pieAlto = Math.Max(28f, 38f * escala);
                float graficoY = cabeceraFin + 2f * escala;
                float baseY = y + alto - pieAlto;
                float graficoAlto = Math.Max(8f, baseY - graficoY - 2f * escala);
                float columna = interiorAncho / Math.Max(1, columnas);
                int indice = 0;
                DibujarBarraVerticalPanel("PRESION", presion, escalaPresion,
                    interiorX + columna * indice++, graficoY, columna, graficoAlto, baseY,
                    compra, venta, neutro, tenue, texto);
                if (MostrarPulso)
                    DibujarBarraVerticalPanel("PULSO", pulsoActual, 100.0,
                        interiorX + columna * indice++, graficoY, columna, graficoAlto, baseY,
                        compra, venta, neutro, tenue, texto);
                DibujarBarraVerticalPanel("SESION", deltaSesion, escalaSesion,
                    interiorX + columna * indice, graficoY, columna, graficoAlto, baseY,
                    compra, venta, neutro, tenue, texto);

                long clasificados = sesionCotizacion + sesionTickRule + sesionPersistencia + sesionSinClasificar;
                string acuerdo = printsComparables > 0
                    ? (100.0 * printsCoincidentes / printsComparables).ToString("0.0", CultureInfo.InvariantCulture) + "%"
                    : "--";
                float pieY = baseY + 1f * escala;
                RenderTarget.DrawLine(new Vector2(interiorX, pieY),
                    new Vector2(interiorX + interiorAncho, pieY), borde, 1f);
                RenderTarget.DrawText("CLASIF", fmtEtiqueta,
                    new RectangleF(interiorX, pieY, interiorAncho * 0.34f, pieAlto * 0.48f), tenue);
                RenderTarget.DrawText(acuerdo + " acuerdo", fmtValorDer,
                    new RectangleF(interiorX + interiorAncho * 0.34f, pieY,
                        interiorAncho * 0.66f, pieAlto * 0.48f), clasificados > 0 ? texto : tenue);

                if (ModoDelta == 3)
                {
                    float progreso = (float)Acotar((double)bracketVolumen / Math.Max(1L, UmbralBracket), 0.0, 1.0);
                    float progresoY = y + alto - Math.Max(3f, 4f * escala);
                    RenderTarget.FillRectangle(new RectangleF(interiorX, progresoY,
                        interiorAncho, Math.Max(2f, 3f * escala)), neutro);
                    RenderTarget.FillRectangle(new RectangleF(interiorX, progresoY,
                        interiorAncho * progreso, Math.Max(2f, 3f * escala)), tenue);
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                if (fondo != null) fondo.Dispose();
                if (borde != null) borde.Dispose();
                if (texto != null) texto.Dispose();
                if (tenue != null) tenue.Dispose();
                if (compra != null) compra.Dispose();
                if (venta != null) venta.Dispose();
                if (neutro != null) neutro.Dispose();
            }
        }

        private void DibujarBarraVerticalPanel(string etiqueta, double valor, double maximo,
            float x, float y, float ancho, float alto, float baseY,
            SharpDX.Direct2D1.Brush compra, SharpDX.Direct2D1.Brush venta,
            SharpDX.Direct2D1.Brush neutro, SharpDX.Direct2D1.Brush tenue,
            SharpDX.Direct2D1.Brush texto)
        {
            float barraAncho = Math.Max(5f, ancho * 0.34f);
            float barraX = x + (ancho - barraAncho) * 0.5f;
            float etiquetaAlto = Math.Max(10f, alto * 0.20f);
            float disponible = Math.Max(2f, alto - etiquetaAlto * 2f);
            float magnitud = maximo > 1E-09
                ? (float)Acotar(Math.Abs(valor) / maximo, 0.0, 1.0)
                : 0f;
            float barraAlto = magnitud <= 0f ? 1f : Math.Max(2f, disponible * magnitud);
            float barraBase = baseY - etiquetaAlto;
            SharpDX.Direct2D1.Brush direccion = valor > 0.0 ? compra : (valor < 0.0 ? venta : neutro);

            // El valor acompana la punta de la barra mientras esta se forma.
            float valorY = Math.Max(y, barraBase - barraAlto - etiquetaAlto);
            RenderTarget.DrawText(FormatoCompacto(valor), fmtCentro,
                new RectangleF(x, valorY, ancho, etiquetaAlto), valor == 0.0 ? tenue : direccion);
            RenderTarget.FillRectangle(new RectangleF(barraX, barraBase - barraAlto,
                barraAncho, barraAlto), direccion);
            RenderTarget.DrawText(etiqueta, fmtCentro,
                new RectangleF(x, barraBase, ancho, etiquetaAlto), texto);
        }

        private void DibujarLibroSesionesInferior(bool juntoAlHud)
        {
            AsegurarFormatos();
            SharpDX.Direct2D1.Brush fondo = null;
            SharpDX.Direct2D1.Brush borde = null;
            SharpDX.Direct2D1.Brush texto = null;
            SharpDX.Direct2D1.Brush tenue = null;
            SharpDX.Direct2D1.Brush compra = null;
            SharpDX.Direct2D1.Brush venta = null;
            try
            {
                fondo = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                    new Color4(0.04f, 0.05f, 0.07f, 0.88f));
                borde = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                    new Color4(0.35f, 0.38f, 0.44f, 0.90f));
                texto = ColorTexto.ToDxBrush(RenderTarget);
                tenue = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                    new Color4(0.55f, 0.58f, 0.63f, 1f));
                compra = ColorCompra.ToDxBrush(RenderTarget);
                venta = ColorVenta.ToDxBrush(RenderTarget);

                float escala = EscalaHudEfectiva();
                float hudX;
                float hudAncho;
                float x;
                float ancho;
                ObtenerGeometriaPanelesInferiores(juntoAlHud, true, escala,
                    out hudX, out hudAncho, out x, out ancho);
                float alto = Math.Min((float)ChartPanel.H, 136f * escala);
                float y = (float)(ChartPanel.Y + ChartPanel.H) - alto;
                RectangleF panel = new RectangleF(x, y, ancho, alto);
                RenderTarget.FillRectangle(panel, fondo);
                RenderTarget.DrawRectangle(panel, borde, 1f);

                float margen = Math.Max(4f, 7f * escala);
                float fila = Math.Max(10f, 19f * escala);
                float interiorX = x + margen;
                float interiorY = y + 2f * escala;
                float interiorAncho = Math.Max(20f, ancho - margen * 2f);
                RenderTarget.DrawText("LIBRO DE SESIONES", fmtTitulo,
                    new RectangleF(interiorX, interiorY, interiorAncho, fila), texto);

                int cantidad = Math.Min(Math.Max(1, SesionesEnBalance), libroSesiones.Count);
                if (cantidad <= 0)
                {
                    RenderTarget.DrawText("SIN SESIONES CERRADAS", fmtEtiqueta,
                        new RectangleF(interiorX, interiorY + fila, interiorAncho, fila), tenue);
                    return;
                }

                double maximo = 1.0;
                double balance = 0.0;
                for (int i = 0; i < cantidad; i++)
                {
                    RegistroSesion sesion = libroSesiones[libroSesiones.Count - 1 - i];
                    if (sesion == null)
                        continue;
                    balance += sesion.Delta;
                    maximo = Math.Max(maximo, Math.Abs(sesion.Delta));
                }

                float graficoY = interiorY + fila;
                float pieAlto = Math.Max(16f, fila * 1.15f);
                float graficoAlto = Math.Max(18f, alto - (graficoY - y) - pieAlto - margen);
                float graficoAncho = interiorAncho * 0.64f;
                float centroY = graficoY + graficoAlto * 0.5f;
                float espacio = graficoAncho / Math.Max(1, cantidad);
                RenderTarget.DrawLine(new Vector2(interiorX, centroY),
                    new Vector2(interiorX + graficoAncho, centroY), tenue, 1f);
                for (int i = 0; i < cantidad; i++)
                {
                    RegistroSesion sesion = libroSesiones[libroSesiones.Count - 1 - i];
                    if (sesion == null)
                        continue;
                    float celdaX = interiorX + graficoAncho - (i + 1) * espacio;
                    float barraX = celdaX + espacio * 0.28f;
                    float barraAncho = Math.Max(2f, espacio * 0.44f);
                    float barraAlto = Math.Max(1f,
                        (float)(Math.Abs(sesion.Delta) / maximo) * graficoAlto * 0.43f);
                    RectangleF barra = sesion.Delta >= 0.0
                        ? new RectangleF(barraX, centroY - barraAlto, barraAncho, barraAlto)
                        : new RectangleF(barraX, centroY, barraAncho, barraAlto);
                    SharpDX.Direct2D1.Brush color = sesion.Delta >= 0.0 ? compra : venta;
                    RenderTarget.FillRectangle(barra, color);
                    RenderTarget.DrawText("D" + (i + 1).ToString(CultureInfo.InvariantCulture), fmtCentro,
                        new RectangleF(celdaX, graficoY + graficoAlto - fila * 0.72f,
                            espacio, fila * 0.72f), tenue);
                }

                float resumenX = interiorX + graficoAncho + margen * 0.4f;
                float resumenAncho = interiorAncho - graficoAncho - margen * 0.4f;
                RenderTarget.DrawText("BALANCE " + cantidad.ToString(CultureInfo.InvariantCulture) + "d",
                    fmtEtiqueta, new RectangleF(resumenX, centroY - fila, resumenAncho, fila), tenue);
                RenderTarget.DrawText(FormatoCompacto(balance), fmtValor,
                    new RectangleF(resumenX, centroY, resumenAncho, fila),
                    balance > 0.0 ? compra : (balance < 0.0 ? venta : tenue));

                RegistroSesion anterior = libroSesiones[libroSesiones.Count - 1];
                if (anterior != null)
                {
                    float pieY = y + alto - pieAlto;
                    double movimientoTicks = (anterior.PrecioCierre - anterior.PrecioApertura)
                        / Math.Max(1E-10, TickSize);
                    string contexto = DescribirContexto(anterior.Delta, movimientoTicks);
                    bool absorcion = contexto.StartsWith("ABSOR", StringComparison.Ordinal);
                    RenderTarget.DrawLine(new Vector2(interiorX, pieY),
                        new Vector2(interiorX + interiorAncho, pieY), borde, 1f);
                    RenderTarget.DrawText("AYER", fmtEtiqueta,
                        new RectangleF(interiorX, pieY, interiorAncho * 0.18f, pieAlto), tenue);
                    RenderTarget.DrawText(contexto, fmtEtiqueta,
                        new RectangleF(interiorX + interiorAncho * 0.18f, pieY,
                            interiorAncho * 0.60f, pieAlto),
                        absorcion ? (anterior.Delta > 0.0 ? venta : compra) : texto);
                    RenderTarget.DrawText(movimientoTicks.ToString("+0;-0;0", CultureInfo.InvariantCulture) + "t",
                        fmtValorDer, new RectangleF(interiorX + interiorAncho * 0.78f, pieY,
                            interiorAncho * 0.22f, pieAlto), tenue);
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                if (fondo != null) fondo.Dispose();
                if (borde != null) borde.Dispose();
                if (texto != null) texto.Dispose();
                if (tenue != null) tenue.Dispose();
                if (compra != null) compra.Dispose();
                if (venta != null) venta.Dispose();
            }
        }

        private NivelMapaDelta[] ObtenerNivelesMapa(Dictionary<long, double> mapa)
        {
            NivelMapaDelta[] niveles = new NivelMapaDelta[4];
            niveles[0].Etiqueta = "D1+";
            niveles[1].Etiqueta = "D2+";
            niveles[2].Etiqueta = "D1-";
            niveles[3].Etiqueta = "D2-";

            foreach (KeyValuePair<long, double> par in mapa)
            {
                double delta = par.Value;
                if (delta > 0.0)
                {
                    if (delta > niveles[0].Delta)
                    {
                        niveles[1] = niveles[0];
                        niveles[1].Etiqueta = "D2+";
                        niveles[0] = CrearNivelMapa(par.Key, delta, "D1+");
                    }
                    else if (delta > niveles[1].Delta)
                    {
                        niveles[1] = CrearNivelMapa(par.Key, delta, "D2+");
                    }
                }
                else if (delta < 0.0)
                {
                    if (niveles[2].Delta == 0.0 || delta < niveles[2].Delta)
                    {
                        niveles[3] = niveles[2];
                        niveles[3].Etiqueta = "D2-";
                        niveles[2] = CrearNivelMapa(par.Key, delta, "D1-");
                    }
                    else if (niveles[3].Delta == 0.0 || delta < niveles[3].Delta)
                    {
                        niveles[3] = CrearNivelMapa(par.Key, delta, "D2-");
                    }
                }
            }
            return niveles;
        }

        private NivelMapaDelta CrearNivelMapa(long precioTicks, double delta, string etiqueta)
        {
            NivelMapaDelta nivel = new NivelMapaDelta();
            nivel.PrecioTicks = precioTicks;
            nivel.Precio = precioTicks * Math.Max(1E-10, TickSize);
            nivel.Delta = delta;
            nivel.Etiqueta = etiqueta;
            return nivel;
        }

        private void DibujarMapaDelta(ChartControl chartControl)
        {
            if (chartControl == null)
                return;

            if (!chartControl.Dispatcher.CheckAccess())
            {
                if (deltaMapUpdateQueued)
                    return;
                deltaMapUpdateQueued = true;
                chartControl.Dispatcher.InvokeAsync(delegate
                {
                    deltaMapUpdateQueued = false;
                    ActualizarMapaDeltaWpf(chartControl);
                });
                return;
            }

            ActualizarMapaDeltaWpf(chartControl);
        }

        private void ActualizarMapaDeltaWpf(ChartControl chartControl)
        {
            ChartPanel panelPrecio = null;
            try
            {
                panelPrecio = chartControl.ChartPanels == null || chartControl.ChartPanels.Count == 0
                    ? null : chartControl.ChartPanels[0];
            }
            catch
            {
            }
            if (panelPrecio == null)
                return;

            AsegurarOverlayMapaDelta(panelPrecio);
            if (deltaMapOverlay == null)
                return;

            deltaMapOverlay.Width = Math.Max(1.0, panelPrecio.W);
            deltaMapOverlay.Height = Math.Max(1.0, panelPrecio.H);
            deltaMapOverlay.Children.Clear();
            deltaMapOverlay.Visibility = MapaDeltaCompleto
                && (MapaDeltaBarras || MapaDeltaLineas)
                ? Visibility.Visible : Visibility.Collapsed;
            if (deltaMapOverlay.Visibility != Visibility.Visible)
                return;

            NivelMapaDelta[] eth;
            NivelMapaDelta[] rth;
            lock (candado)
            {
                eth = ObtenerNivelesMapa(mapaDeltaEth);
                rth = ObtenerNivelesMapa(mapaDeltaRth);
            }

            System.Windows.Media.Brush compra = ClonarBrushMapa(ColorCompra, 1.0);
            System.Windows.Media.Brush venta = ClonarBrushMapa(ColorVenta, 1.0);
            System.Windows.Media.Brush tenue = MakeColor(230, 97, 105, 120);
            System.Windows.Media.Brush texto = ClonarBrushMapa(ColorTexto, 1.0);

            if (MapaDeltaLineas)
            {
                ChartScale escalaPrecio = null;
                try { escalaPrecio = panelPrecio.Scales[ScaleJustification.Right]; }
                catch { }
                if (escalaPrecio == null)
                {
                    try { escalaPrecio = panelPrecio.Scales[ScaleJustification.Left]; }
                    catch { }
                }
                if (escalaPrecio != null)
                {
                    AgregarLineasMapaWpf(panelPrecio, escalaPrecio, eth, "ETH", 0, compra, venta);
                    AgregarLineasMapaWpf(panelPrecio, escalaPrecio, rth, "RTH", 1, compra, venta);
                }
            }

            if (!MapaDeltaBarras)
                return;

            double maximo = 1.0;
            for (int i = 0; i < 4; i++)
            {
                maximo = Math.Max(maximo, Math.Abs(eth[i].Delta));
                maximo = Math.Max(maximo, Math.Abs(rth[i].Delta));
            }

            double escalaVisual = Math.Max(0.65, Math.Min(1.5, TamanoFuentePanel / 13.0));
            double celdaAncho = Math.Max(30.0, 34.0 * escalaVisual);
            double barraAncho = Math.Max(10.0, 18.0 * escalaVisual);
            double grupoAncho = celdaAncho * 4.0;
            double separacion = Math.Max(10.0, 14.0 * escalaVisual);
            double totalAncho = grupoAncho * 2.0 + separacion;
            double anchoDisponible = Math.Max(80.0, panelPrecio.W - 16.0 * escalaVisual);
            if (totalAncho > anchoDisponible)
            {
                separacion = Math.Max(6.0, Math.Min(separacion, anchoDisponible * 0.05));
                celdaAncho = Math.Max(12.0, (anchoDisponible - separacion) / 8.0);
                barraAncho = Math.Min(barraAncho, Math.Max(6.0, celdaAncho * 0.56));
                grupoAncho = celdaAncho * 4.0;
                totalAncho = grupoAncho * 2.0 + separacion;
            }
            double x = PosicionMapaDelta == 1
                ? 8.0 * escalaVisual
                : panelPrecio.W - totalAncho - 8.0 * escalaVisual;
            x = Math.Max(0.0, Math.Min(x, Math.Max(0.0, panelPrecio.W - totalAncho)));
            double etiquetasAlto = Math.Max(34.0, 38.0 * escalaVisual);
            double baseY = panelPrecio.H - etiquetasAlto;
            double maxAlto = Math.Max(12.0,
                Math.Min(120.0 * escalaVisual, panelPrecio.H * 0.24));

            AgregarBarrasMapaWpf(eth, "ETH", x, baseY, maxAlto, barraAncho,
                celdaAncho, grupoAncho, maximo, compra, venta, tenue, texto);
            AgregarBarrasMapaWpf(rth, "RTH", x + grupoAncho + separacion,
                baseY, maxAlto, barraAncho, celdaAncho, grupoAncho, maximo,
                compra, venta, tenue, texto);
        }

        private void AsegurarOverlayMapaDelta(ChartPanel panelPrecio)
        {
            if (deltaMapOverlay != null && deltaMapHostPanel == panelPrecio)
            {
                System.Collections.ObjectModel.ObservableCollection<FrameworkElement> actuales
                    = GetPanelControls(panelPrecio);
                if (actuales != null && actuales.Contains(deltaMapOverlay))
                    return;
            }

            QuitarOverlayMapaDelta();
            System.Collections.ObjectModel.ObservableCollection<FrameworkElement> controles
                = GetPanelControls(panelPrecio);
            if (controles == null)
                return;

            deltaMapOverlay = new Canvas();
            deltaMapOverlay.Background = Brushes.Transparent;
            deltaMapOverlay.IsHitTestVisible = false;
            deltaMapOverlay.ClipToBounds = true;
            deltaMapOverlay.HorizontalAlignment = HorizontalAlignment.Left;
            deltaMapOverlay.VerticalAlignment = VerticalAlignment.Top;
            deltaMapOverlay.Margin = new Thickness(0);
            System.Windows.Controls.Panel.SetZIndex(deltaMapOverlay, int.MaxValue - 20);
            controles.Add(deltaMapOverlay);
            deltaMapHostPanel = panelPrecio;
        }

        private void QuitarOverlayMapaDelta()
        {
            try
            {
                System.Collections.ObjectModel.ObservableCollection<FrameworkElement> controles
                    = GetPanelControls(deltaMapHostPanel);
                if (deltaMapOverlay != null && controles != null
                    && controles.Contains(deltaMapOverlay))
                    controles.Remove(deltaMapOverlay);
            }
            catch
            {
            }
            deltaMapOverlay = null;
            deltaMapHostPanel = null;
        }

        private System.Windows.Media.Brush ClonarBrushMapa(System.Windows.Media.Brush origen,
            double opacidad)
        {
            SolidColorBrush solido = origen as SolidColorBrush;
            SolidColorBrush copia = solido == null
                ? new SolidColorBrush(Colors.Gainsboro)
                : new SolidColorBrush(solido.Color);
            copia.Opacity = Acotar(opacidad, 0.0, 1.0);
            copia.Freeze();
            return copia;
        }

        private void AgregarLineasMapaWpf(ChartPanel panelPrecio, ChartScale escalaPrecio,
            NivelMapaDelta[] niveles, string sesion, int columna, System.Windows.Media.Brush compra,
            System.Windows.Media.Brush venta)
        {
            List<int> visibles = new List<int>(4);
            double[] posiciones = new double[niveles.Length];
            for (int i = 0; i < niveles.Length; i++)
            {
                if (niveles[i].Delta == 0.0 || !EsFinitoPositivo(niveles[i].Precio))
                    continue;
                double y = escalaPrecio.GetYByValue(niveles[i].Precio) - panelPrecio.Y;
                if (y < 0.0 || y > panelPrecio.H)
                    continue;
                posiciones[i] = y;
                visibles.Add(i);
                System.Windows.Media.Brush color = niveles[i].Delta > 0.0 ? compra : venta;
                System.Windows.Shapes.Line linea = new System.Windows.Shapes.Line();
                linea.X1 = 0.0;
                linea.X2 = panelPrecio.W;
                linea.Y1 = y;
                linea.Y2 = y;
                linea.Stroke = color;
                linea.StrokeThickness = (i == 0 || i == 2) ? 1.35 : 0.85;
                linea.Opacity = (i == 0 || i == 2) ? 0.82 : 0.58;
                linea.IsHitTestVisible = false;
                deltaMapOverlay.Children.Add(linea);
            }

            visibles.Sort(delegate(int a, int b)
            {
                return posiciones[a].CompareTo(posiciones[b]);
            });
            const double altoEtiqueta = 15.0;
            const double espacioEtiqueta = 2.0;
            double[] superiores = new double[visibles.Count];
            double siguiente = 0.0;
            for (int i = 0; i < visibles.Count; i++)
            {
                double deseada = Math.Max(0.0, posiciones[visibles[i]] - altoEtiqueta * 0.5);
                superiores[i] = Math.Max(deseada, siguiente);
                siguiente = superiores[i] + altoEtiqueta + espacioEtiqueta;
            }
            if (visibles.Count > 0 && siguiente - espacioEtiqueta > panelPrecio.H)
            {
                double ajuste = siguiente - espacioEtiqueta - panelPrecio.H;
                for (int i = 0; i < superiores.Length; i++)
                    superiores[i] = Math.Max(0.0, superiores[i] - ajuste);
            }

            double anchoEtiqueta = 88.0;
            double xEtiqueta = PosicionMapaDelta == 1
                ? 4.0 + columna * (anchoEtiqueta + 4.0)
                : panelPrecio.W - (columna + 1) * (anchoEtiqueta + 4.0);
            xEtiqueta = Math.Max(0.0, Math.Min(xEtiqueta,
                Math.Max(0.0, panelPrecio.W - anchoEtiqueta)));
            for (int orden = 0; orden < visibles.Count; orden++)
            {
                int indice = visibles[orden];
                System.Windows.Media.Brush color = niveles[indice].Delta > 0.0 ? compra : venta;
                string etiqueta = sesion + " " + niveles[indice].Etiqueta + " "
                    + FormatoCompacto(niveles[indice].Delta);
                AgregarTextoMapa(etiqueta, xEtiqueta, superiores[orden],
                    anchoEtiqueta, altoEtiqueta, color,
                    Math.Max(8.0, TamanoFuentePanel * 0.68), System.Windows.TextAlignment.Left);
            }
        }

        private void AgregarBarrasMapaWpf(NivelMapaDelta[] niveles, string sesion,
            double x, double baseY, double maxAlto, double barraAncho, double celdaAncho,
            double grupoAncho, double maximo, System.Windows.Media.Brush compra,
            System.Windows.Media.Brush venta, System.Windows.Media.Brush tenue,
            System.Windows.Media.Brush texto)
        {
            double fuente = Math.Max(8.0, TamanoFuentePanel * 0.72);
            for (int i = 0; i < niveles.Length; i++)
            {
                double celdaX = x + i * celdaAncho;
                double barraX = celdaX + (celdaAncho - barraAncho) * 0.5;
                double alto = niveles[i].Delta == 0.0
                    ? 0.0 : Math.Max(2.0, Math.Abs(niveles[i].Delta) / maximo * maxAlto);
                System.Windows.Media.Brush color = niveles[i].Delta > 0.0
                    ? compra : (niveles[i].Delta < 0.0 ? venta : tenue);
                if (niveles[i].Delta != 0.0)
                {
                    System.Windows.Shapes.Rectangle barra = new System.Windows.Shapes.Rectangle();
                    barra.Width = barraAncho;
                    barra.Height = alto;
                    barra.Fill = color;
                    barra.IsHitTestVisible = false;
                    Canvas.SetLeft(barra, barraX);
                    Canvas.SetTop(barra, baseY - alto);
                    deltaMapOverlay.Children.Add(barra);

                    AgregarTextoMapa(FormatoCompacto(niveles[i].Delta), celdaX,
                        Math.Max(0.0, baseY - alto - 17.0), celdaAncho, 17.0,
                        color, fuente, System.Windows.TextAlignment.Center);
                }
                AgregarTextoMapa(niveles[i].Etiqueta, celdaX, baseY,
                    celdaAncho, 15.0, niveles[i].Delta == 0.0 ? tenue : texto,
                    fuente, System.Windows.TextAlignment.Center);
            }

            System.Windows.Shapes.Line baseLinea = new System.Windows.Shapes.Line();
            baseLinea.X1 = x;
            baseLinea.X2 = x + grupoAncho;
            baseLinea.Y1 = baseY;
            baseLinea.Y2 = baseY;
            baseLinea.Stroke = tenue;
            baseLinea.StrokeThickness = 1.0;
            baseLinea.IsHitTestVisible = false;
            deltaMapOverlay.Children.Add(baseLinea);
            AgregarTextoMapa(sesion, x, baseY + 14.0, grupoAncho, 16.0,
                texto, fuente, System.Windows.TextAlignment.Center);
        }

        private void AgregarTextoMapa(string contenido, double x, double y,
            double ancho, double alto, System.Windows.Media.Brush color,
            double fuente, System.Windows.TextAlignment alineacion)
        {
            TextBlock bloque = new TextBlock();
            bloque.Text = contenido;
            bloque.Width = Math.Max(1.0, ancho);
            bloque.Height = Math.Max(1.0, alto);
            bloque.Foreground = color;
            bloque.FontFamily = new System.Windows.Media.FontFamily("Consolas");
            bloque.FontWeight = FontWeights.Bold;
            bloque.FontSize = fuente;
            bloque.TextAlignment = alineacion;
            bloque.IsHitTestVisible = false;
            Canvas.SetLeft(bloque, x);
            Canvas.SetTop(bloque, y);
            deltaMapOverlay.Children.Add(bloque);
        }

        private void DibujarLineasMapaSesion(ChartPanel panelPrecio, ChartScale escalaPrecio,
            NivelMapaDelta[] niveles, string sesion, SharpDX.Direct2D1.Brush compra,
            SharpDX.Direct2D1.Brush venta)
        {
            for (int i = 0; i < niveles.Length; i++)
            {
                if (niveles[i].Delta == 0.0 || !EsFinitoPositivo(niveles[i].Precio))
                    continue;
                float y = escalaPrecio.GetYByValue(niveles[i].Precio);
                if (y < panelPrecio.Y || y > panelPrecio.Y + panelPrecio.H)
                    continue;
                SharpDX.Direct2D1.Brush color = niveles[i].Delta > 0.0 ? compra : venta;
                float grosor = (i == 0 || i == 2) ? 1.35f : 0.85f;
                RenderTarget.DrawLine(new Vector2((float)panelPrecio.X, y),
                    new Vector2((float)(panelPrecio.X + panelPrecio.W), y), color, grosor);
                string etiqueta = sesion + " " + niveles[i].Etiqueta + " "
                    + FormatoCompacto(niveles[i].Delta);
                float ancho = Math.Max(92f, etiqueta.Length * 6.2f);
                float x = PosicionMapaDelta == 1
                    ? (float)panelPrecio.X + 4f
                    : (float)(panelPrecio.X + panelPrecio.W) - ancho - 4f;
                RenderTarget.DrawText(etiqueta, fmtEtiqueta,
                    new RectangleF(x, y - 15f, ancho, 15f), color);
            }
        }

        private void DibujarBarrasMapaSesion(NivelMapaDelta[] niveles, string sesion,
            float x, float baseY, float maxAlto, float barraAncho, float hueco,
            float grupoAncho, double maximo, SharpDX.Direct2D1.Brush compra,
            SharpDX.Direct2D1.Brush venta, SharpDX.Direct2D1.Brush tenue,
            SharpDX.Direct2D1.Brush texto)
        {
            for (int i = 0; i < niveles.Length; i++)
            {
                float barraX = x + hueco + i * (barraAncho + hueco);
                float alto = niveles[i].Delta == 0.0
                    ? 1f
                    : Math.Max(2f, (float)(Math.Abs(niveles[i].Delta) / maximo) * maxAlto);
                SharpDX.Direct2D1.Brush color = niveles[i].Delta > 0.0
                    ? compra : (niveles[i].Delta < 0.0 ? venta : tenue);
                RenderTarget.FillRectangle(new RectangleF(barraX, baseY - alto,
                    barraAncho, alto), color);
                RenderTarget.DrawText(FormatoCompacto(niveles[i].Delta), fmtCentro,
                    new RectangleF(barraX - hueco, baseY - alto - 17f,
                        barraAncho + hueco * 2f, 17f), color);
                RenderTarget.DrawText(niveles[i].Etiqueta, fmtCentro,
                    new RectangleF(barraX - hueco, baseY,
                        barraAncho + hueco * 2f, 15f), texto);
            }
            RenderTarget.DrawLine(new Vector2(x, baseY),
                new Vector2(x + grupoAncho, baseY), tenue, 1f);
            RenderTarget.DrawText(sesion, fmtCentro,
                new RectangleF(x, baseY + 14f, grupoAncho, 16f), texto);
        }

        private void DibujarMarcadorDelta(ChartScale chartScale)
        {
            if (chartScale == null || RenderTarget == null || ChartPanel == null || !MostrarVelas)
                return;

            double valorDelta = double.NaN;
            lock (candado)
            {
                for (int i = Math.Min(CurrentBars[0], velas.Count - 1); i >= 0; i--)
                {
                    if (velas[i] != null && velas[i].Iniciada)
                    {
                        valorDelta = velas[i].Close;
                        break;
                    }
                }
            }

            if (double.IsNaN(valorDelta) || double.IsInfinity(valorDelta))
                return;

            AsegurarFormatos();
            SharpDX.Direct2D1.Brush fondo = null;
            SharpDX.Direct2D1.Brush borde = null;
            SharpDX.Direct2D1.Brush direccion = null;
            SharpDX.Direct2D1.PathGeometry geometria = null;
            SharpDX.Direct2D1.GeometrySink trazado = null;
            try
            {
                float escala = (float)Acotar(EscalaPanel, 0.7, 2.0);
                float alto = 22f * escala;
                float mitad = alto * 0.5f;
                float y = chartScale.GetYByValue(valorDelta);
                y = Math.Max((float)ChartPanel.Y + mitad + 1f,
                    Math.Min((float)(ChartPanel.Y + ChartPanel.H) - mitad - 1f, y));

                string etiqueta = valorDelta.ToString("+#,0;-#,0;0", CultureInfo.InvariantCulture);
                float ancho = Math.Max(62f * escala, (etiqueta.Length * 8f + 16f) * escala);
                float punta = 7f * escala;
                float derecha = (float)(ChartPanel.X + ChartPanel.W) - 1f;
                float izquierda = derecha - ancho;
                float xPunta = izquierda - punta;

                fondo = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                    new Color4(0.025f, 0.03f, 0.04f, 0.96f));
                borde = ColorTexto.ToDxBrush(RenderTarget);
                direccion = (valorDelta > 0.0 ? ColorCompra
                    : (valorDelta < 0.0 ? ColorVenta : ColorNeutro)).ToDxBrush(RenderTarget);

                RenderTarget.DrawLine(
                    new Vector2((float)ChartPanel.X, y),
                    new Vector2(xPunta, y), direccion, 1f);

                geometria = new SharpDX.Direct2D1.PathGeometry(Globals.D2DFactory);
                trazado = geometria.Open();
                trazado.BeginFigure(new Vector2(xPunta, y), SharpDX.Direct2D1.FigureBegin.Filled);
                trazado.AddLine(new Vector2(izquierda, y - mitad));
                trazado.AddLine(new Vector2(derecha, y - mitad));
                trazado.AddLine(new Vector2(derecha, y + mitad));
                trazado.AddLine(new Vector2(izquierda, y + mitad));
                trazado.EndFigure(SharpDX.Direct2D1.FigureEnd.Closed);
                trazado.Close();
                trazado.Dispose();
                trazado = null;

                RenderTarget.FillGeometry(geometria, fondo);
                RenderTarget.DrawGeometry(geometria, borde, 1f);
                RenderTarget.DrawText(etiqueta, fmtCentro,
                    new RectangleF(izquierda, y - mitad, ancho, alto), borde);
            }
            catch (Exception)
            {
            }
            finally
            {
                if (trazado != null) trazado.Dispose();
                if (geometria != null) geometria.Dispose();
                if (fondo != null) fondo.Dispose();
                if (borde != null) borde.Dispose();
                if (direccion != null) direccion.Dispose();
            }
        }

        private void QueueControlPanelBuild(ChartControl chartControl)
        {
            if (controlsBuildQueued || chartControl == null)
                return;

            hostChartControl = chartControl;
            ChartPanel menuHost = GetConfigurationHostPanel();
            System.Collections.ObjectModel.ObservableCollection<FrameworkElement> menuControls
                = GetPanelControls(menuHost);
            bool attached = false;
            bool toolbarAttached = false;
            try
            {
                attached = controlsBuilt
                    && controlRoot != null
                    && menuHost != null
                    && configurationHostPanel == menuHost
                    && menuControls != null
                    && menuControls.Contains(controlRoot);
                toolbarAttached = toolbarButtonAdded
                    && toolbarButton != null
                    && chartWindow != null
                    && chartWindow.MainMenu != null
                    && chartWindow.MainMenu.Contains(toolbarButton);
            }
            catch
            {
                attached = false;
                toolbarAttached = false;
            }

            bool layoutChanged = menuHost != null
                && (double.IsNaN(lastPanelWidth)
                    || Math.Abs(lastPanelX - menuHost.X) > 0.5
                    || Math.Abs(lastPanelY - menuHost.Y) > 0.5
                    || Math.Abs(lastPanelWidth - menuHost.W) > 0.5
                    || Math.Abs(lastPanelHeight - menuHost.H) > 0.5);

            if (attached && toolbarAttached && !layoutChanged)
                return;

            controlsBuildQueued = true;
            chartControl.Dispatcher.InvokeAsync(delegate
            {
                try
                {
                    EnsureControlPanelAttached();
                }
                finally
                {
                    controlsBuildQueued = false;
                }
            });
        }

        private void EnsureControlPanelAttached()
        {
            EnsureConfigurationStateLoaded();
            EnsureToolbarButtonAttached();

            ChartPanel menuHost = GetConfigurationHostPanel();
            System.Collections.ObjectModel.ObservableCollection<FrameworkElement> menuControls
                = GetPanelControls(menuHost);
            if (menuHost == null || menuControls == null)
                return;

            if (controlRoot == null)
            {
                controlsBuilt = false;
                BuildControlPanel();
                return;
            }

            try
            {
                System.Collections.ObjectModel.ObservableCollection<FrameworkElement> previousControls
                    = GetPanelControls(configurationHostPanel);
                if (configurationHostPanel != null && configurationHostPanel != menuHost
                    && previousControls != null && previousControls.Contains(controlRoot))
                    previousControls.Remove(controlRoot);

                if (!menuControls.Contains(controlRoot))
                    menuControls.Add(controlRoot);

                configurationHostPanel = menuHost;
                controlsBuilt = true;
                System.Windows.Controls.Panel.SetZIndex(controlRoot, int.MaxValue);
                SyncControlAppearance();
            }
            catch (Exception ex)
            {
                Print("ILT-DEV-BSP panel restore: " + ex.Message);
            }
        }

        private ChartPanel GetConfigurationHostPanel()
        {
            try
            {
                if (hostChartControl == null || hostChartControl.ChartPanels == null
                    || hostChartControl.ChartPanels.Count == 0)
                    return null;

                // El indice cero siempre corresponde al panel principal de precio.
                return hostChartControl.ChartPanels[0];
            }
            catch
            {
                return null;
            }
        }

        private System.Collections.ObjectModel.ObservableCollection<FrameworkElement> GetPanelControls(ChartPanel panel)
        {
            if (panel == null)
                return null;

            try
            {
                if (panelUserControlsProperty == null)
                {
                    panelUserControlsProperty = typeof(ChartPanel).GetProperty(
                        "UserControlCollection",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }

                return panelUserControlsProperty == null
                    ? null
                    : panelUserControlsProperty.GetValue(panel, null)
                        as System.Collections.ObjectModel.ObservableCollection<FrameworkElement>;
            }
            catch
            {
                return null;
            }
        }

        private void EnsureConfigurationStateLoaded()
        {
            if (configurationStateLoaded)
                return;

            configExpanded = ConfigPanelExpanded;
            configUserMoved = ConfigPanelX >= 0.0 && ConfigPanelY >= 0.0;

            try
            {
                int chartKey = 0;
                int indicatorKey = 0;
                try
                {
                    chartKey = ChartHashCodeDeserialized;
                    indicatorKey = IndicatorId;
                }
                catch
                {
                }

                configurationStateKey = (chartKey != 0 || indicatorKey != 0)
                    ? "C" + chartKey.ToString(CultureInfo.InvariantCulture)
                        + "_I" + indicatorKey.ToString(CultureInfo.InvariantCulture)
                    : ConfigPanelStateKey;

                if (string.IsNullOrEmpty(configurationStateKey))
                    configurationStateKey = Guid.NewGuid().ToString("N");
                ConfigPanelStateKey = configurationStateKey;

                string path = ConfigurationStateFilePath();
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    lock (ConfigurationStateFileLock)
                    {
                        foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                        {
                            int separator = line.IndexOf('=');
                            if (separator <= 0)
                                continue;

                            string key = line.Substring(0, separator).Trim();
                            string value = line.Substring(separator + 1).Trim();
                            double number;
                            if (key == "EXPANDED")
                                ConfigPanelExpanded = value == "1"
                                    || value.Equals("true", StringComparison.OrdinalIgnoreCase);
                            else if (key == "X" && double.TryParse(value, NumberStyles.Float,
                                CultureInfo.InvariantCulture, out number))
                                ConfigPanelX = number;
                            else if (key == "Y" && double.TryParse(value, NumberStyles.Float,
                                CultureInfo.InvariantCulture, out number))
                                ConfigPanelY = number;
                        }
                    }
                }
            }
            catch
            {
            }

            configExpanded = ConfigPanelExpanded;
            configUserMoved = ConfigPanelX >= 0.0 && ConfigPanelY >= 0.0;
            configurationStateLoaded = true;
        }

        private string ConfigurationStateFilePath()
        {
            try
            {
                if (string.IsNullOrEmpty(configurationStateKey))
                    return string.Empty;

                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                    "ILT_DEV_SUITE", "Settings");
                string instrumentName = Instrument != null && Instrument.MasterInstrument != null
                    && !string.IsNullOrEmpty(Instrument.MasterInstrument.Name)
                    ? Instrument.MasterInstrument.Name
                    : "DEFAULT";

                foreach (char invalid in Path.GetInvalidFileNameChars())
                    instrumentName = instrumentName.Replace(invalid, '_');

                return Path.Combine(folder, "ILT_DEV_BSP_PANEL_" + instrumentName
                    + "_" + configurationStateKey + ".cfg");
            }
            catch
            {
                return string.Empty;
            }
        }

        private void PersistConfigurationState()
        {
            if (!configurationStateLoaded)
                return;

            try
            {
                ConfigPanelExpanded = configExpanded;
                if (configUserMoved && controlRoot != null)
                {
                    ConfigPanelX = controlRoot.Margin.Left;
                    ConfigPanelY = controlRoot.Margin.Top;
                }

                string path = ConfigurationStateFilePath();
                if (string.IsNullOrEmpty(path))
                    return;

                string folder = Path.GetDirectoryName(path);
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string[] lines = new string[]
                {
                    "VERSION=1",
                    "EXPANDED=" + (ConfigPanelExpanded ? "1" : "0"),
                    "X=" + ConfigPanelX.ToString("R", CultureInfo.InvariantCulture),
                    "Y=" + ConfigPanelY.ToString("R", CultureInfo.InvariantCulture)
                };

                lock (ConfigurationStateFileLock)
                {
                    string temporary = path + ".tmp";
                    File.WriteAllLines(temporary, lines, Encoding.UTF8);
                    if (File.Exists(path))
                        File.Delete(path);
                    File.Move(temporary, path);
                }
            }
            catch
            {
            }
        }

        private void EnsureToolbarButtonAttached()
        {
            if (hostChartControl == null)
                return;

            try
            {
                Chart currentWindow = Window.GetWindow(hostChartControl.Parent) as Chart;
                if (currentWindow == null || currentWindow.MainMenu == null)
                    return;

                if (toolbarButtonAdded && toolbarButton != null
                    && chartWindow == currentWindow
                    && currentWindow.MainMenu.Contains(toolbarButton))
                    return;

                if (toolbarButton != null)
                {
                    toolbarButton.Click -= ToolbarButton_Click;
                    if (chartWindow != null && chartWindow.MainMenu != null
                        && chartWindow.MainMenu.Contains(toolbarButton))
                        chartWindow.MainMenu.Remove(toolbarButton);
                }

                chartWindow = currentWindow;
                toolbarButtonNumber = FindAvailableToolbarButtonNumber();

                toolbarButton = new NTMenuItem();
                toolbarButton.Header = GetToolbarButtonCaption(false);
                toolbarButton.Style = Application.Current.TryFindResource("MainMenuItem") as Style;
                toolbarButton.Foreground = MakeColor(255, 90, 190, 255);
                toolbarButton.ToolTip = "Abrir o cerrar la configuracion de ILT-DEV-BSP";
                toolbarButton.Tag = ToolbarButtonTagPrefix + "|"
                    + toolbarButtonNumber.ToString(CultureInfo.InvariantCulture) + "|"
                    + toolbarButtonInstanceId;
                toolbarButton.Click += ToolbarButton_Click;
                chartWindow.MainMenu.Add(toolbarButton);
                toolbarButtonAdded = true;
            }
            catch (Exception ex)
            {
                toolbarButtonAdded = false;
                Print("ILT-DEV-BSP toolbar: " + ex.Message);
            }
        }

        private int FindAvailableToolbarButtonNumber()
        {
            if (chartWindow == null || chartWindow.MainMenu == null)
                return 1;

            try
            {
                HashSet<int> used = new HashSet<int>();
                foreach (System.Windows.UIElement element in chartWindow.MainMenu)
                {
                    NTMenuItem item = element as NTMenuItem;
                    string tag = item == null ? null : item.Tag as string;
                    if (string.IsNullOrEmpty(tag)
                        || !tag.StartsWith(ToolbarButtonTagPrefix + "|", StringComparison.Ordinal))
                        continue;

                    string[] parts = tag.Split('|');
                    int number;
                    if (parts.Length >= 2
                        && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
                        && number > 0)
                        used.Add(number);
                }

                int available = 1;
                while (used.Contains(available))
                    available++;
                return available;
            }
            catch
            {
                return 1;
            }
        }

        private string GetToolbarButtonCaption(bool expanded)
        {
            string number = toolbarButtonNumber <= 1
                ? string.Empty
                : " " + toolbarButtonNumber.ToString(CultureInfo.InvariantCulture);
            return "ILT-DEV-BSP" + number + (expanded ? " ▲" : " ▼");
        }

        private void ToolbarButton_Click(object sender, RoutedEventArgs e)
        {
            configExpanded = !configExpanded;
            ConfigPanelExpanded = configExpanded;
            EnsureControlPanelAttached();
            SyncControlAppearance();
            PersistConfigurationState();

            if (hostChartControl != null)
                hostChartControl.InvalidateVisual();

            e.Handled = true;
        }

        private void BuildControlPanel()
        {
            ChartPanel menuHost = GetConfigurationHostPanel();
            System.Collections.ObjectModel.ObservableCollection<FrameworkElement> menuControls
                = GetPanelControls(menuHost);
            if (controlsBuilt || hostChartControl == null || menuHost == null
                || menuControls == null)
                return;

            try
            {
                controlRoot = new Grid();
                controlRoot.VerticalAlignment = VerticalAlignment.Top;
                controlRoot.Background = Brushes.Transparent;
                controlRoot.IsHitTestVisible = true;
                controlRoot.Visibility = Visibility.Collapsed;
                System.Windows.Controls.Panel.SetZIndex(controlRoot, int.MaxValue);

                Border border = new Border();
                border.Background = MakeColor(232, 13, 16, 22);
                border.BorderBrush = MakeColor(220, 44, 170, 215);
                border.BorderThickness = new Thickness(1);
                border.CornerRadius = new CornerRadius(4);
                border.Padding = new Thickness(0);
                controlRoot.Children.Add(border);

                StackPanel outer = new StackPanel();
                outer.Orientation = Orientation.Vertical;
                outer.Background = Brushes.Transparent;
                border.Child = outer;

                controlHeader = new Grid();
                controlHeader.Height = 22;
                controlHeader.Background = MakeColor(190, 20, 105, 145);
                controlHeader.Cursor = Cursors.SizeAll;
                controlHeader.ColumnDefinitions.Add(new ColumnDefinition());
                controlHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                controlHeader.MouseLeftButtonDown += ControlHeader_MouseLeftButtonDown;
                controlHeader.MouseMove += ControlHeader_MouseMove;
                controlHeader.MouseLeftButtonUp += ControlHeader_MouseLeftButtonUp;
                controlHeader.LostMouseCapture += ControlHeader_LostMouseCapture;
                outer.Children.Add(controlHeader);

                TextBlock floatingTitle = new TextBlock();
                floatingTitle.Text = "ILT-DEV-BSP";
                floatingTitle.FontFamily = new System.Windows.Media.FontFamily("Consolas");
                floatingTitle.FontSize = 11;
                floatingTitle.FontWeight = FontWeights.Bold;
                floatingTitle.Foreground = Brushes.White;
                floatingTitle.VerticalAlignment = VerticalAlignment.Center;
                floatingTitle.Margin = new Thickness(7, 1, 5, 1);
                Grid.SetColumn(floatingTitle, 0);
                controlHeader.Children.Add(floatingTitle);

                configPanelCloseButton = MakeButton("▼", 22);
                configPanelCloseButton.Height = 18;
                configPanelCloseButton.Margin = new Thickness(2);
                configPanelCloseButton.Background = MakeColor(190, 40, 48, 58);
                configPanelCloseButton.BorderBrush = MakeColor(230, 110, 180, 210);
                configPanelCloseButton.Foreground = Brushes.White;
                configPanelCloseButton.FontFamily = new System.Windows.Media.FontFamily("Consolas");
                configPanelCloseButton.FontSize = 10;
                configPanelCloseButton.FontWeight = FontWeights.Bold;
                configPanelCloseButton.ToolTip = "Cerrar la configuracion de ILT-DEV-BSP";
                configPanelCloseButton.Cursor = Cursors.Arrow;
                configPanelCloseButton.Click += delegate(object sender, RoutedEventArgs e)
                {
                    configExpanded = false;
                    ConfigPanelExpanded = false;
                    SyncControlAppearance();
                    PersistConfigurationState();
                    e.Handled = true;
                };
                Grid.SetColumn(configPanelCloseButton, 1);
                controlHeader.Children.Add(configPanelCloseButton);

                controlBody = new StackPanel();
                controlBody.Orientation = Orientation.Vertical;
                controlBody.Margin = new Thickness(5, 3, 5, 5);
                controlBody.Background = Brushes.Transparent;
                controlScroll = new ScrollViewer();
                controlScroll.Background = Brushes.Transparent;
                controlScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                controlScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                controlScroll.Content = controlBody;
                outer.Children.Add(controlScroll);

                Grid modeRow = new Grid();
                modeRow.Margin = new Thickness(0, 2, 0, 0);
                modeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
                modeRow.ColumnDefinitions.Add(new ColumnDefinition());
                controlBody.Children.Add(modeRow);

                TextBlock modeLabel = MakeLabel("DELTA");
                Grid.SetColumn(modeLabel, 0);
                modeRow.Children.Add(modeLabel);

                modeCombo = MakeModeCombo();
                Grid.SetColumn(modeCombo, 1);
                modeRow.Children.Add(modeCombo);

                WrapPanel panelRow = new WrapPanel();
                panelRow.Orientation = Orientation.Horizontal;
                panelRow.Margin = new Thickness(0, 2, 0, 0);
                controlBody.Children.Add(panelRow);

                masterButton = MakeButton("PANEL ON", 88);
                masterButton.Click += delegate
                {
                    PanelCompleto = !PanelCompleto;
                    ApplyVisibilityState();
                    SyncControlAppearance();
                    RequestRedraw();
                };
                panelRow.Children.Add(masterButton);

                sideButton = MakeButton("DERECHA", 78);
                sideButton.ToolTip = "Cambiar el panel entre izquierda y derecha";
                sideButton.Click += delegate
                {
                    PosicionPanel = PosicionPanel == 1 ? 2 : 1;
                    SyncControlAppearance();
                    RequestRedraw();
                };
                panelRow.Children.Add(sideButton);

                WrapPanel switchesRow = new WrapPanel();
                switchesRow.Orientation = Orientation.Horizontal;
                switchesRow.Margin = new Thickness(0, 2, 0, 0);
                controlBody.Children.Add(switchesRow);

                pulseButton = MakeButton("PULSO", 66);
                pulseButton.Click += delegate
                {
                    PulsoPanel = !PulsoPanel;
                    ApplyVisibilityState();
                    SyncControlAppearance();
                    RequestRedraw();
                };
                switchesRow.Children.Add(pulseButton);

                sessionsButton = MakeButton("LIBRO", 66);
                sessionsButton.ToolTip = "Mostrar u ocultar el libro aunque el panel principal esté apagado";
                sessionsButton.Click += delegate
                {
                    LibroSesionesPanel = !LibroSesionesPanel;
                    ApplyVisibilityState();
                    SyncControlAppearance();
                    RequestRedraw();
                };
                switchesRow.Children.Add(sessionsButton);

                fontMinusButton = MakeButton("A-", 38);
                fontMinusButton.ToolTip = "Reducir tamaño de las letras";
                fontMinusButton.Click += delegate
                {
                    TamanoFuentePanel = Math.Max(9, TamanoFuentePanel - 1);
                    ApplyFontScale(true);
                };
                switchesRow.Children.Add(fontMinusButton);

                fontPlusButton = MakeButton("A+", 38);
                fontPlusButton.ToolTip = "Aumentar tamaño de las letras";
                fontPlusButton.Click += delegate
                {
                    TamanoFuentePanel = Math.Min(26, TamanoFuentePanel + 1);
                    ApplyFontScale(true);
                };
                switchesRow.Children.Add(fontPlusButton);

                TextBlock mapLabel = MakeLabel("MAPA DELTA · ETH / RTH");
                mapLabel.Margin = new Thickness(4, 6, 2, 1);
                controlBody.Children.Add(mapLabel);

                WrapPanel mapMainRow = new WrapPanel();
                mapMainRow.Orientation = Orientation.Horizontal;
                controlBody.Children.Add(mapMainRow);

                deltaMapMasterButton = MakeButton("MAPA ON", 88);
                deltaMapMasterButton.ToolTip = "Mostrar u ocultar por completo el mapa de delta ETH/RTH";
                deltaMapMasterButton.Click += delegate
                {
                    MapaDeltaCompleto = !MapaDeltaCompleto;
                    SyncControlAppearance();
                    RequestRedraw();
                };
                mapMainRow.Children.Add(deltaMapMasterButton);

                deltaMapSideButton = MakeButton("DERECHA", 78);
                deltaMapSideButton.ToolTip = "Cambiar las barras del mapa entre izquierda y derecha";
                deltaMapSideButton.Click += delegate
                {
                    PosicionMapaDelta = PosicionMapaDelta == 1 ? 2 : 1;
                    SyncControlAppearance();
                    RequestRedraw();
                };
                mapMainRow.Children.Add(deltaMapSideButton);

                WrapPanel mapVisibilityRow = new WrapPanel();
                mapVisibilityRow.Orientation = Orientation.Horizontal;
                controlBody.Children.Add(mapVisibilityRow);

                deltaMapBarsButton = MakeButton("BARRAS", 82);
                deltaMapBarsButton.ToolTip = "Mostrar u ocultar solamente las barras del mapa";
                deltaMapBarsButton.Click += delegate
                {
                    MapaDeltaBarras = !MapaDeltaBarras;
                    SyncControlAppearance();
                    RequestRedraw();
                };
                mapVisibilityRow.Children.Add(deltaMapBarsButton);

                deltaMapLinesButton = MakeButton("LINEAS", 82);
                deltaMapLinesButton.ToolTip = "Mostrar u ocultar solamente las lineas del mapa en precio";
                deltaMapLinesButton.Click += delegate
                {
                    MapaDeltaLineas = !MapaDeltaLineas;
                    SyncControlAppearance();
                    RequestRedraw();
                };
                mapVisibilityRow.Children.Add(deltaMapLinesButton);

                Grid colorsRow = new Grid();
                colorsRow.Margin = new Thickness(0, 2, 0, 0);
                colorsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
                colorsRow.ColumnDefinitions.Add(new ColumnDefinition());
                controlBody.Children.Add(colorsRow);

                TextBlock colorsLabel = MakeLabel("BARRAS");
                Grid.SetColumn(colorsLabel, 0);
                colorsRow.Children.Add(colorsLabel);

                paletteCombo = new ComboBox();
                paletteCombo.HorizontalAlignment = HorizontalAlignment.Stretch;
                paletteCombo.MinWidth = 80;
                paletteCombo.Height = 22;
                paletteCombo.Margin = new Thickness(2);
                paletteCombo.FontFamily = new System.Windows.Media.FontFamily("Consolas");
                paletteCombo.FontSize = 11;
                paletteCombo.FontWeight = FontWeights.Bold;
                paletteCombo.Foreground = MakeColor(255, 200, 200, 200);
                paletteCombo.Background = MakeColor(217, 51, 51, 51);
                paletteCombo.BorderBrush = MakeColor(255, 120, 120, 120);
                paletteCombo.ToolTip = "Paletas rápidas. Los colores exactos también se cambian en las propiedades del indicador.";
                paletteCombo.Items.Add("Colores...");
                paletteCombo.Items.Add("Verde / Rojo");
                paletteCombo.Items.Add("Azul / Naranja");
                paletteCombo.Items.Add("Cian / Magenta");
                paletteCombo.Items.Add("Blanco / Gris");
                paletteCombo.Items.Add("Cian / Blanco");
                paletteCombo.SelectedIndex = 0;
                paletteCombo.SelectionChanged += delegate
                {
                    if (syncingControls || paletteCombo.SelectedIndex <= 0)
                        return;
                    ApplyPalette(paletteCombo.SelectedIndex);
                    RequestRedraw();
                };
                Grid.SetColumn(paletteCombo, 1);
                colorsRow.Children.Add(paletteCombo);

                menuControls.Add(controlRoot);
                configurationHostPanel = menuHost;
                controlsBuilt = true;

                ApplyFontScale(false);
                PlaceControlPanel();
                SyncControlAppearance();
            }
            catch (Exception ex)
            {
                Print("ILT-DEV-BSP panel: " + ex.Message);
                RemoveControlPanel();
            }
        }

        private void RemoveControlPanel()
        {
            QuitarOverlayMapaDelta();
            try
            {
                if (toolbarButton != null)
                    toolbarButton.Click -= ToolbarButton_Click;
                if (chartWindow != null && chartWindow.MainMenu != null
                    && toolbarButton != null && chartWindow.MainMenu.Contains(toolbarButton))
                    chartWindow.MainMenu.Remove(toolbarButton);
            }
            catch
            {
            }

            try
            {
                System.Collections.ObjectModel.ObservableCollection<FrameworkElement> menuControls
                    = GetPanelControls(configurationHostPanel);
                if (controlRoot != null && configurationHostPanel != null
                    && menuControls != null && menuControls.Contains(controlRoot))
                    menuControls.Remove(controlRoot);
            }
            catch
            {
            }

            controlRoot = null;
            controlHeader = null;
            controlBody = null;
            controlScroll = null;
            configPanelCloseButton = null;
            modeCombo = null;
            paletteCombo = null;
            masterButton = null;
            pulseButton = null;
            sessionsButton = null;
            sideButton = null;
            fontMinusButton = null;
            fontPlusButton = null;
            deltaMapMasterButton = null;
            deltaMapBarsButton = null;
            deltaMapLinesButton = null;
            deltaMapSideButton = null;
            deltaMapUpdateQueued = false;
            configurationHostPanel = null;
            toolbarButton = null;
            toolbarButtonAdded = false;
            toolbarButtonNumber = 0;
            chartWindow = null;
            controlsBuilt = false;
            controlsBuildQueued = false;
            controlPanelDragging = false;
            lastPanelX = double.NaN;
            lastPanelY = double.NaN;
            lastPanelWidth = double.NaN;
            lastPanelHeight = double.NaN;
            configExpanded = false;
        }

        private ComboBox MakeModeCombo()
        {
            ComboBox combo = new ComboBox();
            combo.HorizontalAlignment = HorizontalAlignment.Stretch;
            combo.MinWidth = 70;
            combo.Height = 22;
            combo.Margin = new Thickness(2);
            combo.Padding = new Thickness(5, 0, 3, 0);
            combo.Foreground = Brushes.White;
            combo.Background = MakeColor(242, 51, 140, 217);
            combo.BorderBrush = MakeColor(255, 120, 120, 120);
            combo.FontFamily = new System.Windows.Media.FontFamily("Consolas");
            combo.FontSize = 11;
            combo.FontWeight = FontWeights.Bold;
            combo.ToolTip = "Cambiar modo de delta";
            combo.Items.Add("DELTA POR VELA");
            combo.Items.Add("DELTA ACUMULADO");
            combo.Items.Add("DELTA BRACKET");
            combo.SelectionChanged += delegate
            {
                if (syncingControls || combo.SelectedIndex < 0)
                    return;

                ModoPanel = combo.SelectedIndex + 1;
                ModoDelta = ModoPanel;
                RequestRedraw();
            };
            return combo;
        }

        private void ApplyVisibilityState()
        {
            if (!PanelCompleto)
            {
                // El interruptor maestro afecta únicamente al HUD. Las velas
                // y la línea cero del gráfico de delta permanecen visibles.
                MostrarHud = false;
                MostrarPulso = false;
                MostrarLibroSesiones = LibroSesionesPanel;
            }
            else
            {
                MostrarHud = true;
                MostrarPulso = PulsoPanel;
                MostrarLibroSesiones = LibroSesionesPanel;
            }
        }

        private void ApplyFontScale(bool redraw)
        {
            EscalaPanel = Math.Max(0.7, Math.Min(2.0, TamanoFuentePanel / 13.0));
            ReleaseBaseTextFormats();
            ApplyResponsiveControlLayout();

            if (redraw)
                RequestRedraw();
        }

        private void ApplyResponsiveControlLayout()
        {
            if (controlRoot == null)
                return;

            ChartPanel menuHost = GetConfigurationHostPanel();
            double availableWidth = menuHost == null ? 286.0 : Math.Max(190.0, menuHost.W - 20.0);
            double fittedWidth = Math.Min(286.0, availableWidth);

            controlRoot.Width = fittedWidth;
            controlRoot.MaxWidth = fittedWidth;
            controlRoot.LayoutTransform = new ScaleTransform(1.0, 1.0);
            controlRoot.Visibility = configExpanded ? Visibility.Visible : Visibility.Collapsed;
            System.Windows.Controls.Panel.SetZIndex(controlRoot, int.MaxValue);

            if (controlScroll != null)
            {
                double availableHeight = menuHost == null ? 180.0 : Math.Max(35.0, menuHost.H - 32.0);
                controlScroll.MaxHeight = availableHeight;
            }

            PlaceControlPanel();

            if (menuHost != null)
            {
                lastPanelX = menuHost.X;
                lastPanelY = menuHost.Y;
                lastPanelWidth = menuHost.W;
                lastPanelHeight = menuHost.H;
            }
        }

        private void ReleaseBaseTextFormats()
        {
            try
            {
                if (releaseFormatsMethod == null)
                {
                    releaseFormatsMethod = typeof(ILT_DEV_BSP).GetMethod(
                        "LiberarFormatos",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                }

                if (releaseFormatsMethod != null)
                    releaseFormatsMethod.Invoke(this, null);
            }
            catch
            {
            }
        }

        private void PlaceControlPanel()
        {
            if (controlRoot == null)
                return;

            controlRoot.HorizontalAlignment = HorizontalAlignment.Left;
            double visualScale = 1.0;
            ScaleTransform transform = controlRoot.LayoutTransform as ScaleTransform;
            if (transform != null)
                visualScale = transform.ScaleX;

            double visualWidth = controlRoot.Width * visualScale;
            ChartPanel menuHost = GetConfigurationHostPanel();
            // La coleccion del panel principal usa coordenadas locales; no se
            // suma su X/Y porque eso desplazaria la configuracion dos veces.
            double top = 8.0;
            double left;
            double slotOffset = Math.Max(0, toolbarButtonNumber - 1) * (visualWidth + 8.0);
            if (menuHost == null)
            {
                double hostWidth = hostChartControl == null ? visualWidth + 20.0 : hostChartControl.ActualWidth;
                left = PosicionPanel == 1
                    ? 10.0 + slotOffset
                    : Math.Max(10.0, hostWidth - visualWidth - 10.0 - slotOffset);
            }
            else
            {
                controlRoot.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double visualHeight = Math.Max(22.0, controlRoot.DesiredSize.Height * visualScale);
                double maxLeft = Math.Max(0.0, menuHost.W - visualWidth);
                double maxTop = Math.Max(0.0, menuHost.H - visualHeight);

                if (configUserMoved && ConfigPanelX >= 0.0 && ConfigPanelY >= 0.0)
                {
                    left = Math.Max(0.0, Math.Min(ConfigPanelX, maxLeft));
                    top = Math.Max(0.0, Math.Min(ConfigPanelY, maxTop));
                    ConfigPanelX = left;
                    ConfigPanelY = top;
                }
                else
                {
                    left = PosicionPanel == 1
                        ? 10.0 + slotOffset
                        : Math.Max(10.0, menuHost.W - visualWidth - 10.0 - slotOffset);
                    left = Math.Max(0.0, Math.Min(left, maxLeft));
                    top = Math.Max(0.0, Math.Min(top, maxTop));
                }
            }

            controlRoot.Margin = new Thickness(left, top, 0, 0);
        }

        private void ControlHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ChartPanel menuHost = GetConfigurationHostPanel();
            if (controlRoot == null || menuHost == null
                || (configPanelCloseButton != null && configPanelCloseButton.IsMouseOver))
                return;

            controlPanelDragging = true;
            controlDragStart = e.GetPosition(menuHost);
            controlDragStartLeft = controlRoot.Margin.Left;
            controlDragStartTop = controlRoot.Margin.Top;
            UIElement element = sender as UIElement;
            if (element != null)
                element.CaptureMouse();
            e.Handled = true;
        }

        private void ControlHeader_MouseMove(object sender, MouseEventArgs e)
        {
            if (!controlPanelDragging || controlRoot == null)
                return;

            ChartPanel menuHost = GetConfigurationHostPanel();
            if (menuHost == null)
                return;

            System.Windows.Point current = e.GetPosition(menuHost);
            double left = controlDragStartLeft + current.X - controlDragStart.X;
            double top = controlDragStartTop + current.Y - controlDragStart.Y;
            double width = Math.Max(40.0, controlRoot.ActualWidth);
            double height = Math.Max(22.0, controlRoot.ActualHeight);
            left = Math.Max(0.0, Math.Min(left, Math.Max(0.0, menuHost.W - width)));
            top = Math.Max(0.0, Math.Min(top, Math.Max(0.0, menuHost.H - height)));

            controlRoot.Margin = new Thickness(left, top, 0, 0);
            ConfigPanelX = left;
            ConfigPanelY = top;
            configUserMoved = true;
            e.Handled = true;
        }

        private void ControlHeader_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!controlPanelDragging)
                return;

            controlPanelDragging = false;
            UIElement element = sender as UIElement;
            if (element != null)
                element.ReleaseMouseCapture();
            PersistConfigurationState();
            e.Handled = true;
        }

        private void ControlHeader_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (!controlPanelDragging)
                return;

            controlPanelDragging = false;
            PersistConfigurationState();
        }

        private void SyncControlAppearance()
        {
            syncingControls = true;
            try
            {
                if (modeCombo != null)
                    modeCombo.SelectedIndex = Math.Max(0, Math.Min(2, ModoPanel - 1));
                if (controlBody != null)
                    controlBody.Visibility = Visibility.Visible;
                if (controlScroll != null)
                    controlScroll.Visibility = Visibility.Visible;
                if (controlRoot != null)
                    controlRoot.Visibility = configExpanded ? Visibility.Visible : Visibility.Collapsed;
                if (toolbarButton != null)
                    toolbarButton.Header = GetToolbarButtonCaption(configExpanded);
                if (masterButton != null)
                {
                    masterButton.Content = PanelCompleto ? "PANEL ON" : "PANEL OFF";
                    StyleToggle(masterButton, PanelCompleto);
                }
                if (pulseButton != null)
                    StyleToggle(pulseButton, PulsoPanel);
                if (sessionsButton != null)
                    StyleToggle(sessionsButton, LibroSesionesPanel);
                if (sideButton != null)
                    sideButton.Content = PosicionPanel == 1 ? "IZQUIERDA" : "DERECHA";
                if (deltaMapMasterButton != null)
                {
                    deltaMapMasterButton.Content = MapaDeltaCompleto ? "MAPA ON" : "MAPA OFF";
                    StyleToggle(deltaMapMasterButton, MapaDeltaCompleto);
                }
                if (deltaMapBarsButton != null)
                    StyleToggle(deltaMapBarsButton, MapaDeltaBarras);
                if (deltaMapLinesButton != null)
                    StyleToggle(deltaMapLinesButton, MapaDeltaLineas);
                if (deltaMapSideButton != null)
                    deltaMapSideButton.Content = PosicionMapaDelta == 1 ? "IZQUIERDA" : "DERECHA";

                ApplyResponsiveControlLayout();
            }
            finally
            {
                syncingControls = false;
            }
        }

        private void ApplyPalette(int palette)
        {
            if (palette == 1)
            {
                ColorCompra = Brushes.LimeGreen;
                ColorVenta = Brushes.Red;
                ColorNeutro = Brushes.DimGray;
            }
            else if (palette == 2)
            {
                ColorCompra = Brushes.DodgerBlue;
                ColorVenta = Brushes.DarkOrange;
                ColorNeutro = Brushes.SlateGray;
            }
            else if (palette == 3)
            {
                ColorCompra = Brushes.Cyan;
                ColorVenta = Brushes.DeepPink;
                ColorNeutro = Brushes.DimGray;
            }
            else if (palette == 4)
            {
                ColorCompra = Brushes.WhiteSmoke;
                ColorVenta = Brushes.Gray;
                ColorNeutro = Brushes.DimGray;
            }
            else if (palette == 5)
            {
                ColorCompra = Brushes.Cyan;
                ColorVenta = Brushes.White;
                ColorNeutro = Brushes.DimGray;
            }
        }

        private void RequestRedraw()
        {
            ChartControl chart = ChartControl ?? hostChartControl;
            if (chart == null)
                return;

            chart.Dispatcher.InvokeAsync(delegate
            {
                EnsureControlPanelAttached();
                chart.InvalidateVisual();
                if (ChartPanel != null)
                    ChartPanel.InvalidateVisual();
            });
        }

        private Button MakeButton(string text, double width)
        {
            Button button = new Button();
            button.Content = text;
            button.Width = width;
            button.Height = 22;
            button.Margin = new Thickness(2);
            button.Padding = new Thickness(3, 0, 3, 0);
            button.Foreground = MakeColor(255, 200, 200, 200);
            button.Background = MakeColor(217, 51, 51, 51);
            button.BorderBrush = MakeColor(255, 120, 120, 120);
            button.FontFamily = new System.Windows.Media.FontFamily("Consolas");
            button.FontSize = 11;
            button.FontWeight = FontWeights.Bold;
            return button;
        }

        private TextBlock MakeLabel(string text)
        {
            TextBlock label = new TextBlock();
            label.Text = text;
            label.Foreground = Brushes.Gainsboro;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.Margin = new Thickness(4, 0, 2, 0);
            label.FontFamily = new System.Windows.Media.FontFamily("Consolas");
            label.FontSize = 10;
            label.FontWeight = FontWeights.Bold;
            return label;
        }

        private void StyleToggle(Button button, bool active)
        {
            if (button == null)
                return;
            button.Background = active
                ? MakeColor(242, 51, 140, 217)
                : MakeColor(217, 51, 51, 51);
            button.Foreground = active
                ? Brushes.White
                : MakeColor(255, 200, 200, 200);
            button.BorderBrush = MakeColor(255, 120, 120, 120);
        }

        private SolidColorBrush MakeColor(byte alpha, byte red, byte green, byte blue)
        {
            SolidColorBrush brush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, red, green, blue));
            brush.Freeze();
            return brush;
        }

        #region Panel properties

        [NinjaScriptProperty]
        [Range(1, 3)]
        [Display(Name = "Modo de delta (1 Vela / 2 Sesión / 3 Bracket)", Description = "1 = Por vela. 2 = Acumulado de sesión. 3 = Bracket rodante.", Order = 2, GroupName = "01. Cálculo")]
        public int ModoPanel { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Panel completo", Description = "Muestra u oculta solamente el panel informativo. El gráfico de delta permanece visible.", Order = 2, GroupName = "00. Panel interactivo")]
        public bool PanelCompleto { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Pulso", Description = "Control independiente del pulso.", Order = 3, GroupName = "00. Panel interactivo")]
        public bool PulsoPanel { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Libro de sesiones", Description = "Control independiente del libro de sesiones.", Order = 4, GroupName = "00. Panel interactivo")]
        public bool LibroSesionesPanel { get; set; }

        [NinjaScriptProperty]
        [Range(1, 2)]
        [Display(Name = "Posición", Description = "Coloca el panel a la izquierda o a la derecha.", Order = 5, GroupName = "00. Panel interactivo")]
        public int PosicionPanel { get; set; }

        [NinjaScriptProperty]
        [Range(9, 26)]
        [Display(Name = "Tamaño de letras", Description = "Tamaño del texto del panel.", Order = 6, GroupName = "00. Panel interactivo")]
        public int TamanoFuentePanel { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mapa delta completo", Description = "Control maestro del mapa ETH/RTH.", Order = 1, GroupName = "05. Mapa delta ETH/RTH")]
        public bool MapaDeltaCompleto { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Barras del mapa", Description = "Muestra las barras D1+, D2+, D1- y D2- de ETH y RTH.", Order = 2, GroupName = "05. Mapa delta ETH/RTH")]
        public bool MapaDeltaBarras { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Líneas del mapa", Description = "Extiende al gráfico principal los precios de los niveles delta del mapa.", Order = 3, GroupName = "05. Mapa delta ETH/RTH")]
        public bool MapaDeltaLineas { get; set; }

        [NinjaScriptProperty]
        [Range(1, 2)]
        [Display(Name = "Posición del mapa", Description = "1 = izquierda, 2 = derecha.", Order = 4, GroupName = "05. Mapa delta ETH/RTH")]
        public int PosicionMapaDelta { get; set; }

        [Browsable(false)]
        public double ConfigPanelX { get; set; }

        [Browsable(false)]
        public double ConfigPanelY { get; set; }

        [Browsable(false)]
        public bool ConfigPanelExpanded { get; set; }

        [Browsable(false)]
        public string ConfigPanelStateKey { get; set; }

        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private ILT_DEV_SUITE.ILT_DEV_BSP[] cacheILT_DEV_BSP;
		public ILT_DEV_SUITE.ILT_DEV_BSP ILT_DEV_BSP(int metrica, int politica, bool reiniciarEnSesion, int volumenMinimoPorTrade, long umbralBracket, int baseBracket, bool mostrarVelas, int anchoVelaPorcentaje, bool mostrarLineaCero, bool mostrarHud, double escalaPanel, bool mostrarPulso, bool mostrarLibroSesiones, int sesionesEnBalance, int modoPanel, bool panelCompleto, bool pulsoPanel, bool libroSesionesPanel, int posicionPanel, int tamanoFuentePanel, bool mapaDeltaCompleto, bool mapaDeltaBarras, bool mapaDeltaLineas, int posicionMapaDelta)
		{
			return ILT_DEV_BSP(Input, metrica, politica, reiniciarEnSesion, volumenMinimoPorTrade, umbralBracket, baseBracket, mostrarVelas, anchoVelaPorcentaje, mostrarLineaCero, mostrarHud, escalaPanel, mostrarPulso, mostrarLibroSesiones, sesionesEnBalance, modoPanel, panelCompleto, pulsoPanel, libroSesionesPanel, posicionPanel, tamanoFuentePanel, mapaDeltaCompleto, mapaDeltaBarras, mapaDeltaLineas, posicionMapaDelta);
		}

		public ILT_DEV_SUITE.ILT_DEV_BSP ILT_DEV_BSP(ISeries<double> input, int metrica, int politica, bool reiniciarEnSesion, int volumenMinimoPorTrade, long umbralBracket, int baseBracket, bool mostrarVelas, int anchoVelaPorcentaje, bool mostrarLineaCero, bool mostrarHud, double escalaPanel, bool mostrarPulso, bool mostrarLibroSesiones, int sesionesEnBalance, int modoPanel, bool panelCompleto, bool pulsoPanel, bool libroSesionesPanel, int posicionPanel, int tamanoFuentePanel, bool mapaDeltaCompleto, bool mapaDeltaBarras, bool mapaDeltaLineas, int posicionMapaDelta)
		{
			if (cacheILT_DEV_BSP != null)
				for (int idx = 0; idx < cacheILT_DEV_BSP.Length; idx++)
					if (cacheILT_DEV_BSP[idx] != null && cacheILT_DEV_BSP[idx].Metrica == metrica && cacheILT_DEV_BSP[idx].Politica == politica && cacheILT_DEV_BSP[idx].ReiniciarEnSesion == reiniciarEnSesion && cacheILT_DEV_BSP[idx].VolumenMinimoPorTrade == volumenMinimoPorTrade && cacheILT_DEV_BSP[idx].UmbralBracket == umbralBracket && cacheILT_DEV_BSP[idx].BaseBracket == baseBracket && cacheILT_DEV_BSP[idx].MostrarVelas == mostrarVelas && cacheILT_DEV_BSP[idx].AnchoVelaPorcentaje == anchoVelaPorcentaje && cacheILT_DEV_BSP[idx].MostrarLineaCero == mostrarLineaCero && cacheILT_DEV_BSP[idx].MostrarHud == mostrarHud && cacheILT_DEV_BSP[idx].EscalaPanel == escalaPanel && cacheILT_DEV_BSP[idx].MostrarPulso == mostrarPulso && cacheILT_DEV_BSP[idx].MostrarLibroSesiones == mostrarLibroSesiones && cacheILT_DEV_BSP[idx].SesionesEnBalance == sesionesEnBalance && cacheILT_DEV_BSP[idx].ModoPanel == modoPanel && cacheILT_DEV_BSP[idx].PanelCompleto == panelCompleto && cacheILT_DEV_BSP[idx].PulsoPanel == pulsoPanel && cacheILT_DEV_BSP[idx].LibroSesionesPanel == libroSesionesPanel && cacheILT_DEV_BSP[idx].PosicionPanel == posicionPanel && cacheILT_DEV_BSP[idx].TamanoFuentePanel == tamanoFuentePanel && cacheILT_DEV_BSP[idx].MapaDeltaCompleto == mapaDeltaCompleto && cacheILT_DEV_BSP[idx].MapaDeltaBarras == mapaDeltaBarras && cacheILT_DEV_BSP[idx].MapaDeltaLineas == mapaDeltaLineas && cacheILT_DEV_BSP[idx].PosicionMapaDelta == posicionMapaDelta && cacheILT_DEV_BSP[idx].EqualsInput(input))
						return cacheILT_DEV_BSP[idx];
			return CacheIndicator<ILT_DEV_SUITE.ILT_DEV_BSP>(new ILT_DEV_SUITE.ILT_DEV_BSP(){ Metrica = metrica, Politica = politica, ReiniciarEnSesion = reiniciarEnSesion, VolumenMinimoPorTrade = volumenMinimoPorTrade, UmbralBracket = umbralBracket, BaseBracket = baseBracket, MostrarVelas = mostrarVelas, AnchoVelaPorcentaje = anchoVelaPorcentaje, MostrarLineaCero = mostrarLineaCero, MostrarHud = mostrarHud, EscalaPanel = escalaPanel, MostrarPulso = mostrarPulso, MostrarLibroSesiones = mostrarLibroSesiones, SesionesEnBalance = sesionesEnBalance, ModoPanel = modoPanel, PanelCompleto = panelCompleto, PulsoPanel = pulsoPanel, LibroSesionesPanel = libroSesionesPanel, PosicionPanel = posicionPanel, TamanoFuentePanel = tamanoFuentePanel, MapaDeltaCompleto = mapaDeltaCompleto, MapaDeltaBarras = mapaDeltaBarras, MapaDeltaLineas = mapaDeltaLineas, PosicionMapaDelta = posicionMapaDelta }, input, ref cacheILT_DEV_BSP);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.ILT_DEV_SUITE.ILT_DEV_BSP ILT_DEV_BSP(int metrica, int politica, bool reiniciarEnSesion, int volumenMinimoPorTrade, long umbralBracket, int baseBracket, bool mostrarVelas, int anchoVelaPorcentaje, bool mostrarLineaCero, bool mostrarHud, double escalaPanel, bool mostrarPulso, bool mostrarLibroSesiones, int sesionesEnBalance, int modoPanel, bool panelCompleto, bool pulsoPanel, bool libroSesionesPanel, int posicionPanel, int tamanoFuentePanel, bool mapaDeltaCompleto, bool mapaDeltaBarras, bool mapaDeltaLineas, int posicionMapaDelta)
		{
			return indicator.ILT_DEV_BSP(Input, metrica, politica, reiniciarEnSesion, volumenMinimoPorTrade, umbralBracket, baseBracket, mostrarVelas, anchoVelaPorcentaje, mostrarLineaCero, mostrarHud, escalaPanel, mostrarPulso, mostrarLibroSesiones, sesionesEnBalance, modoPanel, panelCompleto, pulsoPanel, libroSesionesPanel, posicionPanel, tamanoFuentePanel, mapaDeltaCompleto, mapaDeltaBarras, mapaDeltaLineas, posicionMapaDelta);
		}

		public Indicators.ILT_DEV_SUITE.ILT_DEV_BSP ILT_DEV_BSP(ISeries<double> input , int metrica, int politica, bool reiniciarEnSesion, int volumenMinimoPorTrade, long umbralBracket, int baseBracket, bool mostrarVelas, int anchoVelaPorcentaje, bool mostrarLineaCero, bool mostrarHud, double escalaPanel, bool mostrarPulso, bool mostrarLibroSesiones, int sesionesEnBalance, int modoPanel, bool panelCompleto, bool pulsoPanel, bool libroSesionesPanel, int posicionPanel, int tamanoFuentePanel, bool mapaDeltaCompleto, bool mapaDeltaBarras, bool mapaDeltaLineas, int posicionMapaDelta)
		{
			return indicator.ILT_DEV_BSP(input, metrica, politica, reiniciarEnSesion, volumenMinimoPorTrade, umbralBracket, baseBracket, mostrarVelas, anchoVelaPorcentaje, mostrarLineaCero, mostrarHud, escalaPanel, mostrarPulso, mostrarLibroSesiones, sesionesEnBalance, modoPanel, panelCompleto, pulsoPanel, libroSesionesPanel, posicionPanel, tamanoFuentePanel, mapaDeltaCompleto, mapaDeltaBarras, mapaDeltaLineas, posicionMapaDelta);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.ILT_DEV_SUITE.ILT_DEV_BSP ILT_DEV_BSP(int metrica, int politica, bool reiniciarEnSesion, int volumenMinimoPorTrade, long umbralBracket, int baseBracket, bool mostrarVelas, int anchoVelaPorcentaje, bool mostrarLineaCero, bool mostrarHud, double escalaPanel, bool mostrarPulso, bool mostrarLibroSesiones, int sesionesEnBalance, int modoPanel, bool panelCompleto, bool pulsoPanel, bool libroSesionesPanel, int posicionPanel, int tamanoFuentePanel, bool mapaDeltaCompleto, bool mapaDeltaBarras, bool mapaDeltaLineas, int posicionMapaDelta)
		{
			return indicator.ILT_DEV_BSP(Input, metrica, politica, reiniciarEnSesion, volumenMinimoPorTrade, umbralBracket, baseBracket, mostrarVelas, anchoVelaPorcentaje, mostrarLineaCero, mostrarHud, escalaPanel, mostrarPulso, mostrarLibroSesiones, sesionesEnBalance, modoPanel, panelCompleto, pulsoPanel, libroSesionesPanel, posicionPanel, tamanoFuentePanel, mapaDeltaCompleto, mapaDeltaBarras, mapaDeltaLineas, posicionMapaDelta);
		}

		public Indicators.ILT_DEV_SUITE.ILT_DEV_BSP ILT_DEV_BSP(ISeries<double> input , int metrica, int politica, bool reiniciarEnSesion, int volumenMinimoPorTrade, long umbralBracket, int baseBracket, bool mostrarVelas, int anchoVelaPorcentaje, bool mostrarLineaCero, bool mostrarHud, double escalaPanel, bool mostrarPulso, bool mostrarLibroSesiones, int sesionesEnBalance, int modoPanel, bool panelCompleto, bool pulsoPanel, bool libroSesionesPanel, int posicionPanel, int tamanoFuentePanel, bool mapaDeltaCompleto, bool mapaDeltaBarras, bool mapaDeltaLineas, int posicionMapaDelta)
		{
			return indicator.ILT_DEV_BSP(input, metrica, politica, reiniciarEnSesion, volumenMinimoPorTrade, umbralBracket, baseBracket, mostrarVelas, anchoVelaPorcentaje, mostrarLineaCero, mostrarHud, escalaPanel, mostrarPulso, mostrarLibroSesiones, sesionesEnBalance, modoPanel, panelCompleto, pulsoPanel, libroSesionesPanel, posicionPanel, tamanoFuentePanel, mapaDeltaCompleto, mapaDeltaBarras, mapaDeltaLineas, posicionMapaDelta);
		}
	}
}

#endregion

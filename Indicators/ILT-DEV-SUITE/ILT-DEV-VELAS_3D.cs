#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using SharpDX;
using SharpDX.Direct2D1;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;
using DxBrush = SharpDX.Direct2D1.Brush;
using DxGradientStop = SharpDX.Direct2D1.GradientStop;
using DxGradientStopCollection = SharpDX.Direct2D1.GradientStopCollection;
using DxLinearGradientBrush = SharpDX.Direct2D1.LinearGradientBrush;
using DxSolidColorBrush = SharpDX.Direct2D1.SolidColorBrush;
using DxRenderTarget = SharpDX.Direct2D1.RenderTarget;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.ILT_DEV_SUITE
{
    public enum Neutral3DPalette
    {
        CharcoalGlass,
        TitaniumSlate,
        StealthDark,
        Custom
    }

    public class ILT_DEV_VELAS_3D : Indicator
    {
        #region Private Fields
        // SharpDX Resources
        private DxRenderTarget                  dxTarget;
        private DxGradientStopCollection        upStops;
        private DxGradientStopCollection        downStops;
        private DxLinearGradientBrush           upGradientBrush;
        private DxLinearGradientBrush           downGradientBrush;
        private DxSolidColorBrush               wickBrushDx;
        private DxSolidColorBrush               outlineBrushDx;
        private DxSolidColorBrush               bevelLightBrushDx;
        private DxSolidColorBrush               bevelDarkBrushDx;

        // Cached WPF Brushes for native candle painting
        private MediaBrush                      nativeUpBrush;
        private MediaBrush                      nativeDownBrush;
        private MediaBrush                      nativeWickBrush;

        // Cached colors for resource rebuilding
        private MediaColor                      lastUpColor;
        private MediaColor                      lastDownColor;
        private MediaColor                      lastWickColor;
        private float                           lastShine;
        private float                           lastShadow;
        #endregion

        #region Properties

        // 01. Paleta y Modo
        [NinjaScriptProperty]
        [Display(Name = "Paleta Neutra", GroupName = "01. Estilo Neutro", Order = 1, Description = "Paleta predefinida para velas neutras.")]
        public Neutral3DPalette PresetPalette { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Diferenciar Alcista / Bajista", GroupName = "01. Estilo Neutro", Order = 2, Description = "Aplica una ligera distinción de luminosidad entre velas alcistas y bajistas para lectura de flujo sin colores estridentes.")]
        public bool DifferentiateUpAndDown { get; set; }

        // 02. Colores Personalizados
        [XmlIgnore]
        [Display(Name = "Vela Alcista (Neutro)", GroupName = "02. Colores Personalizados", Order = 1, Description = "Color de cuerpo para velas alcistas o de cierre neutro superior.")]
        public MediaBrush UpCandleColor { get; set; }

        [Browsable(false)]
        public string UpCandleColorSerialize
        {
            get { return Serialize.BrushToString(UpCandleColor); }
            set { UpCandleColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Vela Bajista (Neutro)", GroupName = "02. Colores Personalizados", Order = 2, Description = "Color de cuerpo para velas bajistas.")]
        public MediaBrush DownCandleColor { get; set; }

        [Browsable(false)]
        public string DownCandleColorSerialize
        {
            get { return Serialize.BrushToString(DownCandleColor); }
            set { DownCandleColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Color Mecha", GroupName = "02. Colores Personalizados", Order = 3, Description = "Color de las mechas.")]
        public MediaBrush WickColor { get; set; }

        [Browsable(false)]
        public string WickColorSerialize
        {
            get { return Serialize.BrushToString(WickColor); }
            set { WickColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Color Contorno / Bisel", GroupName = "02. Colores Personalizados", Order = 4, Description = "Color del contorno del cuerpo.")]
        public MediaBrush OutlineColor { get; set; }

        [Browsable(false)]
        public string OutlineColorSerialize
        {
            get { return Serialize.BrushToString(OutlineColor); }
            set { OutlineColor = Serialize.StringToBrush(value); }
        }

        // 03. Efecto 3D Glass
        [NinjaScriptProperty]
        [Display(Name = "Activar Acabado 3D Glass", GroupName = "03. Acabado Glass 3D", Order = 1, Description = "Aplica gradiente volumétrico vertical y biselado tridimensional.")]
        public bool Enable3DGlass { get; set; }

        [Range(0.0, 1.0)]
        [NinjaScriptProperty]
        [Display(Name = "Intensidad Brillo Superior", GroupName = "03. Acabado Glass 3D", Order = 2, Description = "Intensidad del flash superior en el gradiente de la vela (0.0 = plano, 1.0 = máximo brillo).")]
        public float TopShineIntensity { get; set; }

        [Range(0.0, 1.0)]
        [NinjaScriptProperty]
        [Display(Name = "Intensidad Sombra Inferior", GroupName = "03. Acabado Glass 3D", Order = 3, Description = "Intensidad de la sombra inferior en el cuerpo de la vela.")]
        public float BottomShadowIntensity { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Bisel Tridimensional (Rim)", GroupName = "03. Acabado Glass 3D", Order = 4, Description = "Dibuja bordes claros arriba/izquierda y oscuros abajo/derecha para crear efecto de cristal biselado.")]
        public bool EnableBevel { get; set; }

        [Range(10, 100)]
        [NinjaScriptProperty]
        [Display(Name = "Ancho Vela %", GroupName = "03. Acabado Glass 3D", Order = 5, Description = "Ancho porcentual del cuerpo respecto al espaciado de barras del gráfico.")]
        public int CandleWidthPercent { get; set; }

        // 04. Integración con Señales
        [NinjaScriptProperty]
        [Display(Name = "Respetar Señales de Otros Indicadores", GroupName = "04. Integración de Flujo", Order = 1, Description = "Si otro indicador (Event Force, BigTrades, etc.) colorea una barra con BarBrush, respeta ese color y le aplica el acabado 3D.")]
        public bool RespectOtherIndicatorSignals { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Pintar Velas Nativas", GroupName = "04. Integración de Flujo", Order = 2, Description = "Actualiza BarBrush y CandleOutlineBrush nativos para sincronizar cualquier otra vista o captura.")]
        public bool PaintNativeCandles { get; set; }

        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description                         = "ILT VELAS 3D: Velas institucionales en color neutro con relieve 3D Glass, bisel perimetral y soporte para destacar señales de Order Flow.";
                Name = "ILT-DEV-VELAS_3D";
                Calculate                           = Calculate.OnBarClose;
                IsOverlay                           = true;
                DisplayInDataBox                    = false;
                IsSuspendedWhileInactive            = true;
                BarsRequiredToPlot                  = 0;

                // Valores por defecto: Paleta Charcoal Glass (como en la imagen de referencia)
                PresetPalette                       = Neutral3DPalette.CharcoalGlass;
                DifferentiateUpAndDown              = true;

                UpCandleColor                       = new MediaSolidColorBrush(MediaColor.FromRgb(58, 62, 70));   // #3A3E46
                DownCandleColor                     = new MediaSolidColorBrush(MediaColor.FromRgb(42, 45, 52));   // #2A2D34
                WickColor                           = new MediaSolidColorBrush(MediaColor.FromRgb(125, 132, 142)); // #7D848E
                OutlineColor                        = new MediaSolidColorBrush(MediaColor.FromRgb(75, 80, 89));   // #4B5059

                Enable3DGlass                       = true;
                TopShineIntensity                   = 0.55f;
                BottomShadowIntensity               = 0.65f;
                EnableBevel                         = true;
                CandleWidthPercent                  = 82;

                RespectOtherIndicatorSignals        = true;
                PaintNativeCandles                  = false;
            }
            else if (State == State.Configure)
            {
                ApplyPresetPaletteColors();
            }
            else if (State == State.Terminated)
            {
                DisposeDxResources();
            }
        }

        private void ApplyPresetPaletteColors()
        {
            switch (PresetPalette)
            {
                case Neutral3DPalette.CharcoalGlass:
                    // Estilo institucional de referencia: grafito oscuro con brillo sutil y mechas plata
                    UpCandleColor   = new MediaSolidColorBrush(MediaColor.FromRgb(58, 62, 70));
                    DownCandleColor = new MediaSolidColorBrush(MediaColor.FromRgb(42, 45, 52));
                    WickColor       = new MediaSolidColorBrush(MediaColor.FromRgb(125, 132, 142));
                    OutlineColor    = new MediaSolidColorBrush(MediaColor.FromRgb(75, 80, 89));
                    break;

                case Neutral3DPalette.TitaniumSlate:
                    // Gris titanio acero mate
                    UpCandleColor   = new MediaSolidColorBrush(MediaColor.FromRgb(68, 72, 80));
                    DownCandleColor = new MediaSolidColorBrush(MediaColor.FromRgb(50, 54, 62));
                    WickColor       = new MediaSolidColorBrush(MediaColor.FromRgb(145, 150, 160));
                    OutlineColor    = new MediaSolidColorBrush(MediaColor.FromRgb(90, 95, 105));
                    break;

                case Neutral3DPalette.StealthDark:
                    // Ultra oscuro minimalista para destacar Footprint y DOM
                    UpCandleColor   = new MediaSolidColorBrush(MediaColor.FromRgb(38, 40, 45));
                    DownCandleColor = new MediaSolidColorBrush(MediaColor.FromRgb(26, 28, 32));
                    WickColor       = new MediaSolidColorBrush(MediaColor.FromRgb(95, 100, 110));
                    OutlineColor    = new MediaSolidColorBrush(MediaColor.FromRgb(50, 53, 60));
                    break;

                case Neutral3DPalette.Custom:
                default:
                    // Respeta las propiedades configuradas manualmente
                    break;
            }

            if (!DifferentiateUpAndDown)
            {
                DownCandleColor = UpCandleColor;
            }

            nativeUpBrush   = UpCandleColor;
            nativeDownBrush = DownCandleColor;
            nativeWickBrush = WickColor;
        }

        protected override void OnBarUpdate()
        {
            if (!PaintNativeCandles)
                return;

            // Si se deben respetar señales de otros indicadores y la barra ya tiene un color de señal asignado, preservarlo
            if (RespectOtherIndicatorSignals && BarBrush != null && BarBrush != nativeUpBrush && BarBrush != nativeDownBrush)
                return;

            // Sincroniza las brochas nativas de NinjaTrader si está habilitado
            if (Close[0] >= Open[0])
            {
                BarBrush            = nativeUpBrush;
                CandleOutlineBrush  = nativeWickBrush;
            }
            else
            {
                BarBrush            = nativeDownBrush;
                CandleOutlineBrush  = nativeWickBrush;
            }
        }

        #region SharpDX Direct2D Rendering
        private void DisposeDxResources()
        {
            if (upGradientBrush != null) { upGradientBrush.Dispose(); upGradientBrush = null; }
            if (downGradientBrush != null) { downGradientBrush.Dispose(); downGradientBrush = null; }
            if (upStops != null) { upStops.Dispose(); upStops = null; }
            if (downStops != null) { downStops.Dispose(); downStops = null; }
            if (wickBrushDx != null) { wickBrushDx.Dispose(); wickBrushDx = null; }
            if (outlineBrushDx != null) { outlineBrushDx.Dispose(); outlineBrushDx = null; }
            if (bevelLightBrushDx != null) { bevelLightBrushDx.Dispose(); bevelLightBrushDx = null; }
            if (bevelDarkBrushDx != null) { bevelDarkBrushDx.Dispose(); bevelDarkBrushDx = null; }
            dxTarget = null;
        }

        private static SharpDX.Color4 ToDxColor4(MediaColor c, float alphaMultiplier = 1.0f)
        {
            return new SharpDX.Color4(c.R / 255f, c.G / 255f, c.B / 255f, (c.A / 255f) * alphaMultiplier);
        }

        private static bool SameColor(SharpDX.Color4 dxCol, MediaColor medCol)
        {
            float r = medCol.R / 255f;
            float g = medCol.G / 255f;
            float b = medCol.B / 255f;
            return Math.Abs(dxCol.Red - r) < 0.02f && Math.Abs(dxCol.Green - g) < 0.02f && Math.Abs(dxCol.Blue - b) < 0.02f;
        }

        private static SharpDX.Color4 LerpColor4(SharpDX.Color4 from, SharpDX.Color4 to, float t)
        {
            return new SharpDX.Color4(
                from.Red + (to.Red - from.Red) * t,
                from.Green + (to.Green - from.Green) * t,
                from.Blue + (to.Blue - from.Blue) * t,
                from.Alpha + (to.Alpha - from.Alpha) * t);
        }

        private DxGradientStopCollection Create3DGlassStops(DxRenderTarget rt, SharpDX.Color4 baseC, float shine, float shadow)
        {
            SharpDX.Color4 whiteC = new SharpDX.Color4(1f, 1f, 1f, baseC.Alpha);
            SharpDX.Color4 blackC = new SharpDX.Color4(0f, 0f, 0f, baseC.Alpha);

            // 7 Paradas de gradiente volumétrico Glass 3D (flash superior -> núcleo -> sombra inferior)
            var stops = new[]
            {
                new DxGradientStop { Position = 0.00f, Color = LerpColor4(baseC, whiteC, Math.Min(1.0f, shine * 1.15f)) },
                new DxGradientStop { Position = 0.14f, Color = LerpColor4(baseC, whiteC, shine * 0.55f) },
                new DxGradientStop { Position = 0.35f, Color = LerpColor4(baseC, whiteC, shine * 0.15f) },
                new DxGradientStop { Position = 0.60f, Color = baseC },
                new DxGradientStop { Position = 0.82f, Color = LerpColor4(baseC, blackC, shadow * 0.35f) },
                new DxGradientStop { Position = 0.94f, Color = LerpColor4(baseC, blackC, shadow * 0.75f) },
                new DxGradientStop { Position = 1.00f, Color = LerpColor4(baseC, blackC, Math.Min(1.0f, shadow * 1.10f)) }
            };

            return new DxGradientStopCollection(rt, stops, Gamma.StandardRgb, ExtendMode.Clamp);
        }

        private void EnsureDxResources(DxRenderTarget rt)
        {
            MediaSolidColorBrush upScb    = UpCandleColor as MediaSolidColorBrush;
            MediaSolidColorBrush downScb  = DownCandleColor as MediaSolidColorBrush;
            MediaSolidColorBrush wickScb  = WickColor as MediaSolidColorBrush;
            MediaSolidColorBrush outScb   = OutlineColor as MediaSolidColorBrush;

            MediaColor upC    = upScb != null ? upScb.Color : MediaColor.FromRgb(58, 62, 70);
            MediaColor downC  = downScb != null ? downScb.Color : MediaColor.FromRgb(42, 45, 52);
            MediaColor wickC  = wickScb != null ? wickScb.Color : MediaColor.FromRgb(125, 132, 142);
            MediaColor outC   = outScb != null ? outScb.Color : MediaColor.FromRgb(75, 80, 89);

            if (dxTarget != rt ||
                upC != lastUpColor || downC != lastDownColor || wickC != lastWickColor ||
                TopShineIntensity != lastShine || BottomShadowIntensity != lastShadow)
            {
                DisposeDxResources();
                dxTarget            = rt;
                lastUpColor         = upC;
                lastDownColor       = downC;
                lastWickColor       = wickC;
                lastShine           = TopShineIntensity;
                lastShadow          = BottomShadowIntensity;

                upStops             = Create3DGlassStops(rt, ToDxColor4(upC), TopShineIntensity, BottomShadowIntensity);
                downStops           = Create3DGlassStops(rt, ToDxColor4(downC), TopShineIntensity, BottomShadowIntensity);

                var brushProps      = new LinearGradientBrushProperties
                {
                    StartPoint      = new SharpDX.Vector2(0, 0),
                    EndPoint        = new SharpDX.Vector2(0, 1)
                };

                upGradientBrush     = new DxLinearGradientBrush(rt, brushProps, upStops);
                downGradientBrush   = new DxLinearGradientBrush(rt, brushProps, downStops);

                wickBrushDx         = new DxSolidColorBrush(rt, ToDxColor4(wickC));
                outlineBrushDx      = new DxSolidColorBrush(rt, ToDxColor4(outC));

                // Luces de bisel perimetral
                bevelLightBrushDx   = new DxSolidColorBrush(rt, new SharpDX.Color4(1f, 1f, 1f, 0.22f));
                bevelDarkBrushDx    = new DxSolidColorBrush(rt, new SharpDX.Color4(0f, 0f, 0f, 0.38f));
            }
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            if (chartControl == null || chartScale == null || ChartBars == null || Bars == null)
                return;

            int fromIdx = ChartBars.FromIndex;
            int toIdx   = ChartBars.ToIndex;

            if (fromIdx < 0 || toIdx < fromIdx || fromIdx >= Bars.Count)
                return;

            DxRenderTarget rt = RenderTarget;
            if (rt == null)
                return;

            EnsureDxResources(rt);

            float barWidthRatio = (CandleWidthPercent / 100.0f);
            float baseBarWidth  = (float)chartControl.GetBarPaintWidth(ChartBars);
            if (baseBarWidth <= 0)
                baseBarWidth    = (float)chartControl.BarWidth;
            if (baseBarWidth <= 0)
                baseBarWidth    = 4.0f;

            float candleWidth   = Math.Max(2.0f, baseBarWidth * barWidthRatio);

            for (int idx = fromIdx; idx <= toIdx; idx++)
            {
                if (idx < 0 || idx >= Bars.Count)
                    continue;

                double openVal  = Bars.GetOpen(idx);
                double highVal  = Bars.GetHigh(idx);
                double lowVal   = Bars.GetLow(idx);
                double closeVal = Bars.GetClose(idx);

                int xCenter     = chartControl.GetXByBarIndex(ChartBars, idx);
                int openY       = chartScale.GetYByValue(openVal);
                int closeY      = chartScale.GetYByValue(closeVal);
                int highY       = chartScale.GetYByValue(highVal);
                int lowY        = chartScale.GetYByValue(lowVal);

                bool isUp       = (closeVal >= openVal);

                float bodyTop   = Math.Min(openY, closeY);
                float bodyBottom= Math.Max(openY, closeY);
                float bodyHeight= Math.Max(1.0f, bodyBottom - bodyTop);

                // 1. Dibujar Mechas (Wicks)
                if (highY < bodyTop)
                {
                    rt.DrawLine(
                        new SharpDX.Vector2(xCenter, highY),
                        new SharpDX.Vector2(xCenter, bodyTop),
                        wickBrushDx,
                        1.0f);
                }

                if (lowY > bodyBottom)
                {
                    rt.DrawLine(
                        new SharpDX.Vector2(xCenter, bodyBottom),
                        new SharpDX.Vector2(xCenter, lowY),
                        wickBrushDx,
                        1.0f);
                }

                // 2. Dibujar Cuerpo de Vela
                float left  = xCenter - (candleWidth * 0.5f);
                float right = left + candleWidth;
                var bodyRect= new SharpDX.RectangleF(left, bodyTop, candleWidth, bodyHeight);

                // Comprobar si otra herramienta / indicador colocó una señal de color en esta barra
                DxBrush overrideBrush = null;
                if (RespectOtherIndicatorSignals)
                {
                    overrideBrush = chartControl.GetBarOverrideBrush(ChartBars, idx);
                }

                DxSolidColorBrush scbDx = overrideBrush as DxSolidColorBrush;
                if (scbDx != null)
                {
                    MediaSolidColorBrush upScb = nativeUpBrush as MediaSolidColorBrush;
                    MediaSolidColorBrush dnScb = nativeDownBrush as MediaSolidColorBrush;
                    if ((upScb != null && SameColor(scbDx.Color, upScb.Color)) ||
                        (dnScb != null && SameColor(scbDx.Color, dnScb.Color)))
                    {
                        scbDx = null;
                    }
                }

                if (Enable3DGlass && scbDx == null)
                {
                    // Render 3D Glass Neutro
                    DxLinearGradientBrush gradBrush = isUp ? upGradientBrush : downGradientBrush;
                    gradBrush.StartPoint = new SharpDX.Vector2(xCenter, bodyTop);
                    gradBrush.EndPoint   = new SharpDX.Vector2(xCenter, bodyBottom);

                    rt.FillRectangle(bodyRect, gradBrush);

                    // Contorno base
                    rt.DrawRectangle(bodyRect, outlineBrushDx, 1.0f);

                    // Bisel perimetral 3D (luz arriba/izquierda, sombra abajo/derecha)
                    if (EnableBevel && bodyHeight > 3.0f && candleWidth > 3.0f)
                    {
                        rt.DrawLine(new SharpDX.Vector2(left + 1, bodyTop + 1), new SharpDX.Vector2(right - 1, bodyTop + 1), bevelLightBrushDx, 1.0f);
                        rt.DrawLine(new SharpDX.Vector2(left + 1, bodyTop + 1), new SharpDX.Vector2(left + 1, bodyBottom - 1), bevelLightBrushDx, 1.0f);

                        rt.DrawLine(new SharpDX.Vector2(left + 1, bodyBottom - 1), new SharpDX.Vector2(right - 1, bodyBottom - 1), bevelDarkBrushDx, 1.0f);
                        rt.DrawLine(new SharpDX.Vector2(right - 1, bodyTop + 1), new SharpDX.Vector2(right - 1, bodyBottom - 1), bevelDarkBrushDx, 1.0f);
                    }
                }
                else if (scbDx != null)
                {
                    // Barra con señal externa de color (ACB, EXB, etc.): le aplicamos brillo y sombra respetando el color de la señal
                    using (DxGradientStopCollection sigStops = Create3DGlassStops(rt, scbDx.Color, TopShineIntensity, BottomShadowIntensity))
                    using (DxLinearGradientBrush sigBrush = new DxLinearGradientBrush(rt, new LinearGradientBrushProperties
                    {
                        StartPoint = new SharpDX.Vector2(xCenter, bodyTop),
                        EndPoint   = new SharpDX.Vector2(xCenter, bodyBottom)
                    }, sigStops))
                    {
                        rt.FillRectangle(bodyRect, sigBrush);
                        rt.DrawRectangle(bodyRect, outlineBrushDx, 1.0f);
                    }
                }
                else
                {
                    // Relleno plano neutro
                    rt.FillRectangle(bodyRect, isUp ? outlineBrushDx : wickBrushDx);
                    rt.DrawRectangle(bodyRect, outlineBrushDx, 1.0f);
                }
            }
        }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private ILT_DEV_SUITE.ILT_DEV_VELAS_3D[] cacheILT_VELAS_3D;
		public ILT_DEV_SUITE.ILT_DEV_VELAS_3D ILT_DEV_VELAS_3D()
		{
			return ILT_DEV_VELAS_3D(Input);
		}

		public ILT_DEV_SUITE.ILT_DEV_VELAS_3D ILT_DEV_VELAS_3D(ISeries<double> input)
		{
			if (cacheILT_VELAS_3D != null)
				for (int idx = 0; idx < cacheILT_VELAS_3D.Length; idx++)
					if (cacheILT_VELAS_3D[idx] != null &&  cacheILT_VELAS_3D[idx].EqualsInput(input))
						return cacheILT_VELAS_3D[idx];
			return CacheIndicator<ILT_DEV_SUITE.ILT_DEV_VELAS_3D>(new ILT_DEV_SUITE.ILT_DEV_VELAS_3D(), input, ref cacheILT_VELAS_3D);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.ILT_DEV_SUITE.ILT_DEV_VELAS_3D ILT_DEV_VELAS_3D()
		{
			return indicator.ILT_DEV_VELAS_3D(Input);
		}

		public Indicators.ILT_DEV_SUITE.ILT_DEV_VELAS_3D ILT_DEV_VELAS_3D(ISeries<double> input )
		{
			return indicator.ILT_DEV_VELAS_3D(input);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.ILT_DEV_SUITE.ILT_DEV_VELAS_3D ILT_DEV_VELAS_3D()
		{
			return indicator.ILT_DEV_VELAS_3D(Input);
		}

		public Indicators.ILT_DEV_SUITE.ILT_DEV_VELAS_3D ILT_DEV_VELAS_3D(ISeries<double> input )
		{
			return indicator.ILT_DEV_VELAS_3D(input);
		}
	}
}

#endregion

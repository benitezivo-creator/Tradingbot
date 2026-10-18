
#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml.Serialization;
using NinjaTrader.Data;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
using D2D = SharpDX.Direct2D1;
using DW = SharpDX.DirectWrite;
using DxBrush = SharpDX.Direct2D1.SolidColorBrush;
using DxColor4 = SharpDX.Color4;
using DxRawRect = SharpDX.RectangleF;
using DxVec2 = SharpDX.Vector2;
#endregion

// -----------------------------------------------------------------------------
// ILT_DEV_GAMMATRIX_OPTIONS_FLOW_V1_16_DASHED_PULSE_EDGE_BARS
// Iron Lion Traders - Pure GEXBot Options Flow / Dealer Map
// 100% GEXBot. No AMT, no VWAP, no OR/IB, no futures order-flow.
// Purpose: synthetic options DOM / dealer pressure ladder projected on NQ_NDX.
// V1.21: Disco-style toolbar button for floating HMI visibility + internal panel controls.
// -----------------------------------------------------------------------------

namespace NinjaTrader.NinjaScript.Indicators.ILT_DEV_SUITE
{
    public class ILT_DEV_GAMMATRIX : Indicator
    {
        private class RawLevel
        {
            public double Price;
            public string Category;
            public string Type;
            public string Label;
            public double Strength;
            public double Direction;
            public double Amount;
            public bool HasAmount;
            public string Source;
        }

        private class DealerZone
        {
            public double Price;
            public double Low;
            public double High;
            public string Label;
            public string Mode;
            public string Grade;
            public string Bias;
            public double Gex;
            public double Dex;
            public double Vanna;
            public double Charm;
            public double Iv;
            public double Flow;
            public double Big;
            public double Amount;
            public bool HasAmount;
            public double Score;
            public double Direction;
            public bool Active;
            public bool Near;
            public double ProbReaction = 50;   // % calibrado de reaccion en la zona
            public double ProbRupture = 50;    // % calibrado de ruptura (suman 100)
        }

        private class BigFlowEvent
        {
            public DateTime Time;
            public double Price;
            public string Label;
            public string Category;
            public double Impact;
            public double Direction;
            public double Amount;
            public bool HasAmount;
            public string Mode;
        }

        private class OrderflowPulse
        {
            public string Status = "OFF";
            public DateTime Time = DateTime.MinValue;
            public double GexFlow;
            public double DexFlow;
            public double CvrFlow;
            public double NetDex;
            public double Vanna;
            public double Charm;
            public double Direction;
            public double Pulse;
            public double CallPressure;
            public double PutPressure;
        }

        private readonly object dataLock = new object();
        private readonly List<RawLevel> rawLevels = new List<RawLevel>();
        private readonly List<DealerZone> dealerZones = new List<DealerZone>();
        private readonly List<BigFlowEvent> bigFlowTape = new List<BigFlowEvent>();
        private readonly Queue<double> pulseHistory = new Queue<double>();
        private readonly OrderflowPulse pulse = new OrderflowPulse();

        private DateTime lastZoneBuildUtc = DateTime.MinValue;
        private DateTime lastApiFetchUtc = DateTime.MinValue;
        private volatile bool apiBusy;
        private string apiStatus = "OFF";
        private string regimeText = "ESPERAR";
        private string dealerBiasText = "NEUTRO";
        private string readText = "SIN DATA GEXBOT";
        private string activeZoneText = "N/A";
        private string topZoneText = "N/A";
        private string lastJsonSummary = "";

        // Runtime HMI controls. These do not change saved indicator parameters.
        private bool runtimeHmiVisible = true;
        private bool runtimeHmiMinimized = false;
        // Runtime chart-zone labels are controlled only by the internal GEX ON/OFF button.
        // Toolbar IltDevGammatrix controls only floating HMI panel visibility.
        private bool runtimeZoneLabelsVisible = true;
        private bool toolbarInstallRequested = false;
        private StackPanel hmiToolbarPanel;
        private Button hmiToggleButton;
        private Button gexLabelToggleButton;
        private NinjaTrader.Gui.Tools.NTMenuItem toolbarMenuItem;
        private NinjaTrader.Gui.Tools.NTMenuItem gexMenuItem;
        private NinjaTrader.Gui.Chart.Chart chartWindowRef;
        private Button hmiMinimizeButton;
        private Panel hmiToolbarHost;

        // Runtime draggable SharpDX HMI panel.
        private bool hmiDragHandlersAttached = false;
        private bool hmiIsDragging = false;
        private System.Windows.Point hmiDragStartPoint;
        private int hmiDragStartOffsetX;
        private int hmiDragStartOffsetY;
        private float lastHmiX = -1, lastHmiY = -1, lastHmiW = 0, lastHmiH = 0;
        private float hmiInternalToggleX = -1, hmiInternalToggleY = -1, hmiInternalToggleW = 0, hmiInternalToggleH = 0;
        private float hmiInternalMinX = -1, hmiInternalMinY = -1, hmiInternalMinW = 0, hmiInternalMinH = 0;

        // SharpDX resources
        private DxBrush bgBrush, headerBrush, borderBrush, textBrush, mutedBrush, barBgBrush;
        private DxBrush cyanBrush, greenBrush, yellowBrush, orangeBrush, redBrush, magentaBrush, whiteBrush, softBlueBrush;
        private DW.TextFormat titleFmt, headFmt, textFmt, smallFmt, microFmt, bubbleFmt, bubbleSmallFmt, chipFmt;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "ILT-DEV-GAMMATRIX";
                Description = "Pure GEXBot Options Flow / Dealer Map V1.25. GEX ON/OFF fixed with real WPF overlay button: toggles right-edge zone labels, leaving only MM bars when OFF.";
                Calculate = Calculate.OnPriceChange;
                IsOverlay = true;
                DrawOnPricePanel = true;
                PaintPriceMarkers = false;
                DisplayInDataBox = false;
                IsSuspendedWhileInactive = true;

                EnableGexBotApi = false;
                ApiKey = string.Empty;
                BaseUrl = "https://api.gex.bot/v2";
                GexSymbol = "NQ_NDX";
                RefreshSeconds = 20;
                RequestTimeoutMs = 4500;

                EnableStateGex = true;
                GexCategories = "gex_full,gex_zero,gex_one";
                EnableStateGreeks = true;
                GreekCategories = "gamma,delta,vanna,charm";
                EnableOrderflow = true;
                EnableMaxChange = true;
                MaxChangeCategories = "gex_full,gex_zero,gex_one,gamma,delta,vanna";
                MaxCategoriesPerCycle = 12;
                CustomOrderflowEndpoint = string.Empty;

                PriceFilterPoints = 2500;
                MergeZoneTicks = 16;
                ActiveZoneTicks = 14;
                NearZoneTicks = 60;
                MinimumZoneScore = 35;
                MaxVisibleZones = 14;
                BigFlowImpactThreshold = 50;
                FlowNormalizer = 1000000;

                ShowHmiPanel = true;
                ShowOptionsDom = false;
                ShowBigFlowTape = true;
                ShowBigFlowBubbles = true;
                ShowPriceZoneBands = true;
                PanelAnchor = "TopLeft";
                PanelWidth = 1120;
                PanelHeight = 820;
                PanelXOffset = 22;
                PanelYOffset = 48;
                PanelOpacity = 94;
                LadderWidth = 980;
                LadderXOffset = 18;
                FontSize = 24;
                RowHeight = 78;
                BigFlowTapeRows = 8;
            }
            else if (State == State.DataLoaded)
            {
                lastApiFetchUtc = DateTime.MinValue;
            }
            else if (State == State.Terminated)
            {
                RemoveToolbarButtonsAsync();
                RemoveHmiDragHandlersAsync();
                DisposeDxResources();
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 1)
                return;

            if (EnableGexBotApi)
                FetchGexBotAsyncIfNeeded();

            lock (dataLock)
            {
                // Las zonas se reconstruyen como maximo cada 250 ms: con
                // Calculate.OnPriceChange este metodo dispara muchas veces por
                // segundo y la construccion (LINQ + buckets) es costosa. Entre
                // reconstrucciones solo se refrescan los flags Active/Near, que es
                // lo unico que depende del precio tick a tick.
                if ((DateTime.UtcNow - lastZoneBuildUtc).TotalMilliseconds >= 250)
                {
                    lastZoneBuildUtc = DateTime.UtcNow;
                    BuildDealerZonesLocked();
                }
                else
                    UpdateZoneProximityLocked();
                UpdateDecisionStateLocked();
                if (pulseHistory.Count > 80)
                    pulseHistory.Dequeue();
                pulseHistory.Enqueue(Math.Max(-100, Math.Min(100, pulse.Direction == 0 ? pulse.Pulse : pulse.Direction)));
            }
        }

        private void UpdateZoneProximityLocked()
        {
            foreach (DealerZone z in dealerZones)
            {
                z.Active = DistanceToPrice(z) <= Math.Max(1, ActiveZoneTicks) * TickSize;
                z.Near = DistanceToPrice(z) <= Math.Max(1, NearZoneTicks) * TickSize;
            }
        }

        #region API engine
        private void FetchGexBotAsyncIfNeeded()
        {
            if (apiBusy)
                return;
            if (string.IsNullOrWhiteSpace(ApiKey) || string.IsNullOrWhiteSpace(BaseUrl) || string.IsNullOrWhiteSpace(GexSymbol))
            {
                apiStatus = "API NO CONFIGURADA";
                return;
            }
            if ((DateTime.UtcNow - lastApiFetchUtc).TotalSeconds < Math.Max(5, RefreshSeconds))
                return;

            lastApiFetchUtc = DateTime.UtcNow;
            apiBusy = true;
            string key = ApiKey;
            string baseUrl = BaseUrl;
            string ticker = GexSymbol;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    List<RawLevel> levels = new List<RawLevel>();
                    List<BigFlowEvent> tape = new List<BigFlowEvent>();
                    OrderflowPulse newPulse = new OrderflowPulse();
                    string summary = string.Empty;

                    int calls = 0;
                    int okCalls = 0;
                    if (EnableStateGex)
                    {
                        foreach (string cat in SplitCsv(GexCategories).Take(Math.Max(1, MaxCategoriesPerCycle)))
                        {
                            calls++;
                            try
                            {
                                // Classic full endpoint contains the major levels plus strike values, so we can attach MM amount to Major Call/Put/Gamma zones.
                                string json = HttpGet(BuildClassicUrl(baseUrl, ticker, NormalizeGexCategory(cat), string.Empty), key, RequestTimeoutMs);
                                ParseLevelJson(json, cat, "CLASSIC", levels, tape, false);
                                summary += cat + " ";
                                okCalls++;
                            }
                            catch { summary += cat + "(ERR) "; }
                        }
                    }

                    if (EnableStateGreeks)
                    {
                        foreach (string cat in SplitCsv(GreekCategories).Take(Math.Max(1, MaxCategoriesPerCycle)))
                        {
                            calls++;
                            try
                            {
                                string json = HttpGet(BuildStateUrl(baseUrl, ticker, NormalizeGreekCategory(cat), "majors"), key, RequestTimeoutMs);
                                ParseLevelJson(json, cat, "GREEK", levels, tape, false);
                                summary += cat + " ";
                                okCalls++;
                            }
                            catch { summary += cat + "(ERR) "; }
                        }
                    }

                    if (EnableMaxChange)
                    {
                        foreach (string cat in SplitCsv(MaxChangeCategories).Take(Math.Max(1, MaxCategoriesPerCycle)))
                        {
                            calls++;
                            try
                            {
                                string ncat = NormalizeMixedCategory(cat);
                                string json = HttpGet(IsClassicCategory(ncat) ? BuildClassicUrl(baseUrl, ticker, ncat, "maxchange") : BuildStateUrl(baseUrl, ticker, ncat, "maxchange"), key, RequestTimeoutMs);
                                ParseLevelJson(json, cat, "MAXCHANGE", levels, tape, true);
                                summary += "MC:" + cat + " ";
                                okCalls++;
                            }
                            catch { summary += "MC:" + cat + "(ERR) "; }
                        }
                    }

                    if (EnableOrderflow)
                    {
                        calls++;
                        try
                        {
                            string url = BuildOrderflowUrl(baseUrl, ticker);
                            string json = HttpGet(url, key, RequestTimeoutMs);
                            ParseOrderflowJson(json, newPulse);
                            summary += "ORDERFLOW ";
                            okCalls++;
                        }
                        catch { summary += "ORDERFLOW(ERR) "; }
                    }

                    lock (dataLock)
                    {
                        rawLevels.Clear();
                        rawLevels.AddRange(FilterValidLevels(levels));

                        // V1.6: el tape ya no se borra si en un refresh no hay MaxChange nuevo.
                        // Esto evita que el Big Flow Tape desaparezca fuera de horario o entre ciclos de API.
                        List<BigFlowEvent> freshTape = FilterBigFlow(tape);
                        if (freshTape.Count == 0)
                            freshTape = FilterBigFlow(BuildTapeFromMaxChangeLevels(levels));
                        MergeBigFlowTapeLocked(freshTape);

                        CopyPulse(newPulse, pulse);
                        apiStatus = (okCalls == calls ? "API OK " : okCalls > 0 ? "API PARCIAL " : "API ERR ") + DateTime.Now.ToString("HH:mm:ss") + " | " + okCalls + "/" + calls + " req";
                        lastJsonSummary = CompactApiSummary(summary.Trim());
                    }
                }
                catch (Exception ex)
                {
                    apiStatus = "API ERR: " + ShortError(ex.Message);
                }
                finally
                {
                    apiBusy = false;
                }
            });
        }

        private IEnumerable<string> SplitCsv(string s)
        {
            return (s ?? string.Empty).Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0);
        }

        private string BuildStateUrl(string baseUrl, string ticker, string category, string mode)
        {
            string url = baseUrl.TrimEnd('/') + "/" + Uri.EscapeDataString(ticker.Trim()) + "/state/" + Uri.EscapeDataString(category.Trim());
            if (!string.IsNullOrWhiteSpace(mode))
                url += "/" + mode.Trim('/');
            return url;
        }

        private string BuildClassicUrl(string baseUrl, string ticker, string category, string mode)
        {
            string url = baseUrl.TrimEnd('/') + "/" + Uri.EscapeDataString(ticker.Trim()) + "/classic/" + Uri.EscapeDataString(category.Trim());
            if (!string.IsNullOrWhiteSpace(mode))
                url += "/" + mode.Trim('/');
            return url;
        }

        private bool IsClassicCategory(string category)
        {
            string c = (category ?? string.Empty).ToLowerInvariant();
            return c == "gex_full" || c == "gex_zero" || c == "gex_one" || c == "full" || c == "zero" || c == "one";
        }

        private string BuildOrderflowUrl(string baseUrl, string ticker)
        {
            string endpoint = (CustomOrderflowEndpoint ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                endpoint = endpoint.Replace("{ticker}", ticker).Replace("{symbol}", ticker);
                return endpoint.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? endpoint : baseUrl.TrimEnd('/') + "/" + endpoint.TrimStart('/');
            }
            return baseUrl.TrimEnd('/') + "/" + Uri.EscapeDataString(ticker.Trim()) + "/orderflow/orderflow";
        }

        private string NormalizeGexCategory(string raw)
        {
            string c = (raw ?? string.Empty).Trim().ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
            if (c == "full" || c == "gex" || c == "gamma" || c == "gexfull") return "gex_full";
            if (c == "zero" || c == "0" || c == "0dte" || c == "gex0") return "gex_zero";
            if (c == "one" || c == "1" || c == "1dte" || c == "gex1") return "gex_one";
            return c;
        }

        private string NormalizeGreekCategory(string raw)
        {
            string c = (raw ?? string.Empty).Trim().ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
            if (c == "dex") return "delta";
            if (c == "vex") return "vanna";
            return c;
        }

        private string NormalizeMixedCategory(string raw)
        {
            string c = (raw ?? string.Empty).Trim().ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
            if (c.StartsWith("gex") || c == "full" || c == "one" || c == "zero" || c == "0dte" || c == "1dte") return NormalizeGexCategory(c);
            return NormalizeGreekCategory(c);
        }

        private string HttpGet(string url, string key, int timeoutMs)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = Math.Max(1000, timeoutMs);
            request.ReadWriteTimeout = Math.Max(1000, timeoutMs);
            request.Accept = "application/json";
            request.UserAgent = "ILT-DEV-GAMMATRIX-OPTIONS-FLOW/1.0";
            request.Headers["Authorization"] = key.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? key : "Bearer " + key;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (System.IO.StreamReader reader = new System.IO.StreamReader(response.GetResponseStream()))
                return reader.ReadToEnd();
        }

        private void ParseLevelJson(string json, string category, string source, List<RawLevel> levels, List<BigFlowEvent> tape, bool asBigFlow)
        {
            if (string.IsNullOrWhiteSpace(json))
                return;

            string cat = NormalizeMixedCategory(category).ToUpperInvariant();
            Dictionary<double, double> strikeAmounts = ExtractStrikeAmountMap(json);

            // V1.7: GEXBot OpenAPI does not expose explicit call_wall/put_wall fields in the public majors schema.
            // It exposes zero_gamma, major_pos/neg and strikes/profile data. We derive practical wall proxies from those.
            ParseClassicMajorWallFields(json, cat, source, levels, tape, asBigFlow, strikeAmounts);
            ParseStrikeProfileWalls(json, cat, source, levels, tape, asBigFlow);

            AddNamedLevel(json, levels, cat, source, "gamma_flip", "GAMMA FLIP", "FLIP", 92, 0, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "gammaFlip", "GAMMA FLIP", "FLIP", 92, 0, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "zero_gamma", "GAMMA FLIP", "FLIP", 92, 0, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "call_wall", "CALL WALL", "CALL", 90, -1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "callWall", "CALL WALL", "CALL", 90, -1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "callwall", "CALL WALL", "CALL", 90, -1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "major_call", "MAJOR CALL", "CALL", 88, -1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "majorCall", "MAJOR CALL", "CALL", 88, -1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "major_call_strike", "MAJOR CALL", "CALL", 88, -1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "majorCallStrike", "MAJOR CALL", "CALL", 88, -1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "top_call", "MAJOR CALL", "CALL", 84, -1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "topCall", "MAJOR CALL", "CALL", 84, -1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "call_strike", "MAJOR CALL", "CALL", 82, -1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "callStrike", "MAJOR CALL", "CALL", 82, -1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "put_wall", "PUT WALL", "PUT", 90, 1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "putWall", "PUT WALL", "PUT", 90, 1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "putwall", "PUT WALL", "PUT", 90, 1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "major_put", "MAJOR PUT", "PUT", 88, 1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "majorPut", "MAJOR PUT", "PUT", 88, 1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "major_put_strike", "MAJOR PUT", "PUT", 88, 1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "majorPutStrike", "MAJOR PUT", "PUT", 88, 1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "top_put", "MAJOR PUT", "PUT", 84, 1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "topPut", "MAJOR PUT", "PUT", 84, 1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "put_strike", "MAJOR PUT", "PUT", 82, 1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "putStrike", "MAJOR PUT", "PUT", 82, 1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "mcall", "MAJOR CALL", "CALL", 84, -1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "mput", "MAJOR PUT", "PUT", 84, 1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "call", "MAJOR CALL", "CALL", 78, -1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "put", "MAJOR PUT", "PUT", 78, 1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "callStrikePrice", "MAJOR CALL", "CALL", 82, -1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "putStrikePrice", "MAJOR PUT", "PUT", 82, 1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "max_pain", "MAX PAIN", "PIN", 78, 0, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "maxPain", "MAX PAIN", "PIN", 78, 0, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "major_pos", cat + " +", "POS", 82, 1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "majorPositive", cat + " +", "POS", 82, 1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "major_long", cat + " LONG", "POS", 80, 1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "major_neg", cat + " -", "NEG", 82, -1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "majorNegative", cat + " -", "NEG", 82, -1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "major_short", cat + " SHORT", "NEG", 80, -1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "zero_mcall", "0DTE CALL", "CALL", 78, -1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "zero_mput", "0DTE PUT", "PUT", 78, 1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "one_mcall", "1DTE CALL", "CALL", 74, -1, strikeAmounts);
            AddNamedLevel(json, levels, cat, source, "one_mput", "1DTE PUT", "PUT", 74, 1, strikeAmounts);

            // V1.6: captura objetos anidados tipo "call_wall": { "strike": ..., "value": ... }
            // Muchas respuestas no traen el wall como número directo sino como objeto.
            ParseNamedWallObjects(json, cat, source, levels, tape, asBigFlow, strikeAmounts);

            // Generic object parser: any object with strike/price/level becomes a zone candidate.
            foreach (Match obj in Regex.Matches(json, "\\{[^{}]{0,900}\\}", RegexOptions.Singleline))
            {
                string block = obj.Value;
                double price = ExtractNumber(block, "strike", "strike_price", "strikePrice", "price", "level", "price_level", "priceLevel", "spot", "underlying", "x", "k");
                if (!IsValidPriceCandidate(price))
                    continue;

                double raw = ExtractNumber(block, "value", "amount", "score", "impact", "magnitude", "change", "change_value", "changeValue", "delta", "gamma", "gex", "dex", "vanna", "charm", "notional", "premium", "volume", "oi", "open_interest", "openInterest");
                double amount = ExtractAmountCandidate(block, raw);
                bool hasAmount = !double.IsNaN(amount) && Math.Abs(amount) > 0;
                double strength = double.IsNaN(raw) ? 58 : NormalizeMagnitude(raw);
                string label = ExtractString(block, "label", "name", "type", "side", "kind", "option_type", "optionType", "contract_type", "contractType", "put_call", "putCall", "right");
                label = NormalizeOptionLabel(label, block, cat);
                if (string.IsNullOrWhiteSpace(label)) label = cat;
                double dir = InferDirectionFromText(block + " " + label);
                if (dir == 0) dir = InferDirectionFromCategory(cat, label, raw);

                levels.Add(new RawLevel { Price = price, Category = cat, Type = label.ToUpperInvariant(), Label = CleanLabel(label, cat), Strength = strength, Direction = dir, Amount = hasAmount ? amount : 0, HasAmount = hasAmount, Source = source });

                if (asBigFlow || strength >= BigFlowImpactThreshold)
                {
                    tape.Add(new BigFlowEvent
                    {
                        Time = DateTime.Now,
                        Price = price,
                        Label = BuildBigFlowLabel(CleanLabel(label, cat), cat, strength, dir),
                        Category = cat,
                        Impact = strength,
                        Direction = dir,
                        Amount = hasAmount ? amount : 0,
                        HasAmount = hasAmount,
                        Mode = dir < -0.2 ? "PUT/PRESIÓN BAJISTA" : dir > 0.2 ? "CALL/PRESIÓN ALCISTA" : "CAMBIO"
                    });
                }
            }
        }


        private void ParseClassicMajorWallFields(string json, string cat, string source, List<RawLevel> levels, List<BigFlowEvent> tape, bool asBigFlow, Dictionary<double, double> strikeAmounts)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            // Official GEXBot schema exposes these as major positive/negative GEX by volume/OI.
            // We label them as +GEX/-GEX walls and infer CALL/PUT context from relation to spot.
            double spot = ExtractNumber(json, "spot", "underlying", "underlying_price", "underlyingPrice");

            AddMajorWallField(json, cat, source, levels, tape, asBigFlow, strikeAmounts, spot, "major_pos_vol", "+GEX", 88, 1);
            AddMajorWallField(json, cat, source, levels, tape, asBigFlow, strikeAmounts, spot, "major_pos_oi", "+GEX", 86, 1);
            AddMajorWallField(json, cat, source, levels, tape, asBigFlow, strikeAmounts, spot, "mpos_vol", "+GEX", 88, 1);
            AddMajorWallField(json, cat, source, levels, tape, asBigFlow, strikeAmounts, spot, "mpos_oi", "+GEX", 86, 1);
            AddMajorWallField(json, cat, source, levels, tape, asBigFlow, strikeAmounts, spot, "major_long_gamma", "+GEX", 88, 1);
            AddMajorWallField(json, cat, source, levels, tape, asBigFlow, strikeAmounts, spot, "major_long", "+GEX", 84, 1);

            AddMajorWallField(json, cat, source, levels, tape, asBigFlow, strikeAmounts, spot, "major_neg_vol", "-GEX", 88, -1);
            AddMajorWallField(json, cat, source, levels, tape, asBigFlow, strikeAmounts, spot, "major_neg_oi", "-GEX", 86, -1);
            AddMajorWallField(json, cat, source, levels, tape, asBigFlow, strikeAmounts, spot, "mneg_vol", "-GEX", 88, -1);
            AddMajorWallField(json, cat, source, levels, tape, asBigFlow, strikeAmounts, spot, "mneg_oi", "-GEX", 86, -1);
            AddMajorWallField(json, cat, source, levels, tape, asBigFlow, strikeAmounts, spot, "major_short_gamma", "-GEX", 88, -1);
            AddMajorWallField(json, cat, source, levels, tape, asBigFlow, strikeAmounts, spot, "major_short", "-GEX", 84, -1);
        }

        private void AddMajorWallField(string json, string cat, string source, List<RawLevel> levels, List<BigFlowEvent> tape, bool asBigFlow, Dictionary<double, double> strikeAmounts, double spot, string key, string signLabel, double strength, double rawDirection)
        {
            double p = ExtractNumber(json, key);
            if (!IsValidPriceCandidate(p)) return;

            double amount = LookupStrikeAmount(strikeAmounts, p);
            if (double.IsNaN(amount))
                amount = ExtractNumber(json, key + "_value", key + "Value", key + "_amount", key + "Amount", key + "_gex", key + "Gex", key + "_notional", key + "Notional");
            bool hasAmount = !double.IsNaN(amount) && Math.Abs(amount) > 0;
            if (!hasAmount && rawDirection != 0) amount = rawDirection * Math.Max(1, strength);

            string label = BuildInferredWallLabel(p, spot, signLabel);
            string type = label.Contains("CALL") ? "CALL" : label.Contains("PUT") ? "PUT" : signLabel.Contains("+") ? "POS" : "NEG";
            double direction = label.Contains("CALL") ? -1 : label.Contains("PUT") ? 1 : rawDirection;

            RawLevel rl = new RawLevel { Price = p, Category = cat, Type = type, Label = label, Strength = strength, Direction = direction, Amount = hasAmount ? amount : 0, HasAmount = hasAmount, Source = source };
            levels.Add(rl);
            if (asBigFlow || strength >= BigFlowImpactThreshold)
                tape.Add(new BigFlowEvent { Time = DateTime.Now, Price = p, Label = BuildBigFlowLabel(label, cat, strength, direction), Category = cat, Impact = strength, Direction = direction, Amount = hasAmount ? amount : 0, HasAmount = hasAmount, Mode = direction < -0.2 ? "CALL/RESISTENCIA" : direction > 0.2 ? "PUT/SOPORTE" : "CAMBIO" });
        }

        private string BuildInferredWallLabel(double price, double spot, string signLabel)
        {
            bool hasSpot = IsValidPriceCandidate(spot);
            if (signLabel.Contains("+"))
            {
                if (hasSpot && price >= spot) return "CALL WALL* +GEX";
                if (hasSpot && price < spot) return "PUT SOPORTE* +GEX";
                return "+GEX WALL";
            }
            else
            {
                if (hasSpot && price <= spot) return "PUT WALL* -GEX";
                if (hasSpot && price > spot) return "CALL RIESGO* -GEX";
                return "-GEX WALL";
            }
        }

        private void ParseStrikeProfileWalls(string json, string cat, string source, List<RawLevel> levels, List<BigFlowEvent> tape, bool asBigFlow)
        {
            string body = ExtractJsonArrayBody(json, "strikes");
            if (string.IsNullOrWhiteSpace(body)) return;

            double spot = ExtractNumber(json, "spot", "underlying", "underlying_price", "underlyingPrice");
            double bestPosStrike = double.NaN, bestPosAmount = 0;
            double bestNegStrike = double.NaN, bestNegAmount = 0;

            foreach (Match rowMatch in Regex.Matches(body, @"\[(?<row>[^\[\]]{1,900})\]", RegexOptions.Singleline))
            {
                List<double> nums = ExtractAllNumbers(rowMatch.Groups["row"].Value);
                if (nums.Count < 2) continue;
                double strike = nums[0];
                if (!IsValidPriceCandidate(strike)) continue;

                for (int i = 1; i < nums.Count; i++)
                {
                    double v = nums[i];
                    if (double.IsNaN(v) || double.IsInfinity(v) || v == 0) continue;
                    if (v > bestPosAmount) { bestPosAmount = v; bestPosStrike = strike; }
                    if (v < bestNegAmount) { bestNegAmount = v; bestNegStrike = strike; }
                }
            }

            if (IsValidPriceCandidate(bestPosStrike) && bestPosAmount > 0)
                AddProfileWallLevel(cat, source, levels, tape, asBigFlow, bestPosStrike, bestPosAmount, BuildInferredWallLabel(bestPosStrike, spot, "+GEX"), 84, 1);
            if (IsValidPriceCandidate(bestNegStrike) && bestNegAmount < 0)
                AddProfileWallLevel(cat, source, levels, tape, asBigFlow, bestNegStrike, bestNegAmount, BuildInferredWallLabel(bestNegStrike, spot, "-GEX"), 84, -1);
        }

        private void AddProfileWallLevel(string cat, string source, List<RawLevel> levels, List<BigFlowEvent> tape, bool asBigFlow, double price, double amount, string label, double minStrength, double rawDirection)
        {
            double strength = Math.Max(minStrength, NormalizeMagnitude(amount));
            string type = label.Contains("CALL") ? "CALL" : label.Contains("PUT") ? "PUT" : rawDirection > 0 ? "POS" : "NEG";
            double direction = label.Contains("CALL") ? -1 : label.Contains("PUT") ? 1 : rawDirection;
            RawLevel rl = new RawLevel { Price = price, Category = cat, Type = type, Label = label, Strength = Math.Min(100, strength), Direction = direction, Amount = amount, HasAmount = true, Source = source + "/PROFILE" };
            levels.Add(rl);
            if (asBigFlow || strength >= BigFlowImpactThreshold)
                tape.Add(new BigFlowEvent { Time = DateTime.Now, Price = price, Label = BuildBigFlowLabel(label, cat, strength, direction), Category = cat, Impact = Math.Min(100, strength), Direction = direction, Amount = amount, HasAmount = true, Mode = direction < -0.2 ? "CALL/RESISTENCIA" : direction > 0.2 ? "PUT/SOPORTE" : "CAMBIO" });
        }

        private string ExtractJsonArrayBody(string json, string key)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(key)) return string.Empty;
            Match m = Regex.Match(json, "[\\\"']" + Regex.Escape(key) + "[\\\"']\\s*:", RegexOptions.IgnoreCase);
            if (!m.Success) return string.Empty;
            int start = json.IndexOf('[', m.Index + m.Length);
            if (start < 0) return string.Empty;
            int depth = 0;
            for (int i = start; i < json.Length; i++)
            {
                if (json[i] == '[') depth++;
                else if (json[i] == ']')
                {
                    depth--;
                    if (depth == 0)
                        return json.Substring(start + 1, i - start - 1);
                }
            }
            return string.Empty;
        }

        private List<double> ExtractAllNumbers(string s)
        {
            List<double> nums = new List<double>();
            if (string.IsNullOrWhiteSpace(s)) return nums;
            foreach (Match m in Regex.Matches(s, @"-?\d+(?:[\.,]\d+)?(?:[eE][-+]?\d+)?"))
            {
                double v;
                if (TryParseDouble(m.Value, out v)) nums.Add(v);
            }
            return nums;
        }

        private void ParseNamedWallObjects(string json, string cat, string source, List<RawLevel> levels, List<BigFlowEvent> tape, bool asBigFlow, Dictionary<double, double> strikeAmounts)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            foreach (Match m in Regex.Matches(json, @"[""'](?<key>[^""']{3,80})[""']\s*:\s*(?<obj>\{[^{}]{0,1200}\})", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                string rawKey = m.Groups["key"].Value;
                string key = rawKey.Trim().ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
                string block = m.Groups["obj"].Value;

                string label = null;
                string type = null;
                double strength = 78;
                double direction = 0;

                if (key.Contains("call_wall") || key.Contains("callwall")) { label = "CALL WALL"; type = "CALL"; strength = 92; direction = -1; }
                else if (key.Contains("put_wall") || key.Contains("putwall")) { label = "PUT WALL"; type = "PUT"; strength = 92; direction = 1; }
                else if (key.Contains("major_call") || key.Contains("top_call") || key.Contains("mcall") || key == "call" || key.Contains("call_strike")) { label = "MAJOR CALL"; type = "CALL"; strength = 88; direction = -1; }
                else if (key.Contains("major_put") || key.Contains("top_put") || key.Contains("mput") || key == "put" || key.Contains("put_strike")) { label = "MAJOR PUT"; type = "PUT"; strength = 88; direction = 1; }
                else if (key.Contains("gamma_flip") || key.Contains("zero_gamma") || key.Contains("flip")) { label = "GAMMA FLIP"; type = "FLIP"; strength = 92; direction = 0; }
                else if (key.Contains("max_pain") || key.Contains("maxpain")) { label = "MAX PAIN"; type = "PIN"; strength = 78; direction = 0; }
                else if (key.Contains("major_pos") || key.Contains("major_positive") || key.Contains("major_long")) { label = cat + " +"; type = "POS"; strength = 82; direction = 1; }
                else if (key.Contains("major_neg") || key.Contains("major_negative") || key.Contains("major_short")) { label = cat + " -"; type = "NEG"; strength = 82; direction = -1; }
                else continue;

                double price = ExtractNumber(block, "strike", "strike_price", "strikePrice", "price", "level", "price_level", "priceLevel", "spot", "underlying", "x", "k", "value");
                if (!IsValidPriceCandidate(price))
                {
                    // Si el objeto no trae strike/price, intenta leer el valor directo asociado al key completo.
                    price = ExtractNumber(json, rawKey, rawKey.Replace("_", ""));
                }
                if (!IsValidPriceCandidate(price)) continue;

                double amount = ExtractAmountCandidate(block, double.NaN);
                if (double.IsNaN(amount)) amount = LookupStrikeAmount(strikeAmounts, price);
                bool hasAmount = !double.IsNaN(amount) && Math.Abs(amount) > 0;
                double raw = ExtractNumber(block, "impact", "score", "magnitude", "change", "change_value", "changeValue", "amount", "notional", "premium", "exposure", "gex", "dex", "gamma", "delta", "vanna", "charm");
                double localStrength = double.IsNaN(raw) ? strength : Math.Max(strength * 0.72, NormalizeMagnitude(raw));

                RawLevel rl = new RawLevel { Price = price, Category = cat, Type = type, Label = label, Strength = Math.Min(100, localStrength), Direction = direction, Amount = hasAmount ? amount : 0, HasAmount = hasAmount, Source = source };
                levels.Add(rl);

                if (asBigFlow || rl.Strength >= BigFlowImpactThreshold)
                {
                    tape.Add(new BigFlowEvent { Time = DateTime.Now, Price = price, Label = BuildBigFlowLabel(label, cat, rl.Strength, direction), Category = cat, Impact = rl.Strength, Direction = direction, Amount = hasAmount ? amount : 0, HasAmount = hasAmount, Mode = direction < -0.2 ? "PUT/PRESIÓN BAJISTA" : direction > 0.2 ? "CALL/PRESIÓN ALCISTA" : "CAMBIO" });
                }
            }
        }

        private void AddNamedLevel(string json, List<RawLevel> levels, string cat, string source, string key, string label, string type, double strength, double direction, Dictionary<double, double> strikeAmounts)
        {
            double p = ExtractNumber(json, key);
            if (!IsValidPriceCandidate(p)) return;

            double amount = LookupStrikeAmount(strikeAmounts, p);
            if (double.IsNaN(amount))
                amount = ExtractNumber(json, key + "_value", key + "Value", key + "_amount", key + "Amount", key + "_gex", key + "Gex", key + "_notional", key + "Notional");
            bool hasAmount = !double.IsNaN(amount) && Math.Abs(amount) > 0;

            levels.Add(new RawLevel { Price = p, Category = cat, Type = type, Label = label, Strength = strength, Direction = direction, Amount = hasAmount ? amount : 0, HasAmount = hasAmount, Source = source });
        }

        private void ParseOrderflowJson(string json, OrderflowPulse p)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            p.GexFlow = SumNumbers(json, "gexoflow", "one_gexoflow", "zero_gexoflow");
            p.DexFlow = SumNumbers(json, "dexoflow", "one_dexoflow", "zero_dexoflow", "net_dex", "agg_dex");
            p.CvrFlow = SumNumbers(json, "cvroflow", "one_cvroflow", "zero_cvroflow", "zcvr", "ocvr", "zgr", "ogr", "ivoflow", "iv_flow", "vol_flow", "volatility_flow", "iv", "volatility");
            p.NetDex = ExtractNumber(json, "net_dex", "agg_dex");
            p.Vanna = SumNumbers(json, "zvanna", "ovanna", "vanna", "vanna_flow");
            p.Charm = SumNumbers(json, "zcharm", "ocharm", "charm", "charm_flow");
            p.CallPressure = SumNumbers(json, "zero_mcall", "one_mcall", "call_flow", "callFlow", "calls");
            p.PutPressure = SumNumbers(json, "zero_mput", "one_mput", "put_flow", "putFlow", "puts");

            double directional = p.DexFlow + 0.65 * p.GexFlow + 0.35 * p.NetDex + 0.20 * (p.CallPressure - p.PutPressure);
            p.Direction = NormalizeSigned(directional);
            p.Pulse = Math.Max(NormalizeMagnitude(p.GexFlow), Math.Max(NormalizeMagnitude(p.DexFlow), Math.Max(NormalizeMagnitude(p.CvrFlow), Math.Abs(p.Direction))));
            p.Status = "OK " + DateTime.Now.ToString("HH:mm:ss");
            p.Time = DateTime.UtcNow;
        }

        private List<RawLevel> FilterValidLevels(List<RawLevel> list)
        {
            double price = Close != null && CurrentBar >= 0 ? Close[0] : 0;
            double window = Math.Max(250, PriceFilterPoints);
            return list.Where(l => IsValidPriceCandidate(l.Price) && (price <= 0 || Math.Abs(l.Price - price) <= window))
                       .GroupBy(l => l.Category + "|" + l.Type + "|" + Math.Round(l.Price / TickSize) + "|" + l.Label)
                       .Select(g => g.OrderByDescending(x => x.Strength).First())
                       .ToList();
        }

        private List<BigFlowEvent> FilterBigFlow(List<BigFlowEvent> tape)
        {
            double price = Close != null && CurrentBar >= 0 ? Close[0] : 0;
            double window = Math.Max(250, PriceFilterPoints);
            return tape.Where(e => IsValidPriceCandidate(e.Price) && e.Impact >= BigFlowImpactThreshold && (price <= 0 || Math.Abs(e.Price - price) <= window)).ToList();
        }

        private List<BigFlowEvent> BuildTapeFromMaxChangeLevels(List<RawLevel> levels)
        {
            List<BigFlowEvent> outTape = new List<BigFlowEvent>();
            if (levels == null) return outTape;

            foreach (RawLevel l in levels.Where(x => x != null && (x.Source ?? string.Empty).ToUpperInvariant().Contains("MAXCHANGE") && x.Strength >= BigFlowImpactThreshold))
            {
                outTape.Add(new BigFlowEvent
                {
                    Time = DateTime.Now,
                    Price = l.Price,
                    Label = BuildBigFlowLabel(l.Label, l.Category, l.Strength, l.Direction),
                    Category = l.Category,
                    Impact = l.Strength,
                    Direction = l.Direction,
                    Amount = l.HasAmount ? l.Amount : 0,
                    HasAmount = l.HasAmount,
                    Mode = l.Direction < -0.2 ? "PUT/PRESIÓN BAJISTA" : l.Direction > 0.2 ? "CALL/PRESIÓN ALCISTA" : "CAMBIO"
                });
            }
            return outTape;
        }

        private void MergeBigFlowTapeLocked(List<BigFlowEvent> freshTape)
        {
            DateTime cutoff = DateTime.Now.AddMinutes(-90);
            List<BigFlowEvent> merged = bigFlowTape.Where(e => e != null && e.Time >= cutoff).ToList();
            if (freshTape != null)
                merged.AddRange(freshTape);

            bigFlowTape.Clear();
            foreach (BigFlowEvent e in merged
                .Where(e => e != null && IsValidPriceCandidate(e.Price))
                .GroupBy(e => Math.Round(e.Price / Math.Max(TickSize, 0.0001)) + "|" + (e.Category ?? "") + "|" + (e.Label ?? ""))
                .Select(g => g.OrderByDescending(x => x.Time).ThenByDescending(x => x.Impact).First())
                .OrderByDescending(e => e.Time)
                .ThenByDescending(e => e.Impact)
                .Take(40))
            {
                bigFlowTape.Add(e);
            }
        }

        private string CompactApiSummary(string summary)
        {
            if (string.IsNullOrWhiteSpace(summary)) return string.Empty;
            string[] parts = summary.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> ok = new List<string>();
            List<string> err = new List<string>();
            foreach (string p in parts)
            {
                if (p.IndexOf("(ERR)", StringComparison.OrdinalIgnoreCase) >= 0) err.Add(p.Replace("(ERR)", ""));
                else ok.Add(p);
            }
            string s = "OK: " + string.Join(",", ok.Take(6));
            if (err.Count > 0) s += " | FALLA: " + string.Join(",", err.Take(6));
            return s.Length > 110 ? s.Substring(0, 110) + "..." : s;
        }

        private void CopyPulse(OrderflowPulse src, OrderflowPulse dst)
        {
            dst.Status = src.Status;
            dst.Time = src.Time;
            dst.GexFlow = src.GexFlow;
            dst.DexFlow = src.DexFlow;
            dst.CvrFlow = src.CvrFlow;
            dst.NetDex = src.NetDex;
            dst.Vanna = src.Vanna;
            dst.Charm = src.Charm;
            dst.Direction = src.Direction;
            dst.Pulse = src.Pulse;
            dst.CallPressure = src.CallPressure;
            dst.PutPressure = src.PutPressure;
        }
        #endregion

        #region Zone engine
        private void BuildDealerZonesLocked()
        {
            dealerZones.Clear();
            if (rawLevels.Count == 0)
                return;

            List<RawLevel> sorted = rawLevels.OrderBy(l => l.Price).ToList();
            double merge = Math.Max(1, MergeZoneTicks) * TickSize;
            List<List<RawLevel>> buckets = new List<List<RawLevel>>();
            foreach (RawLevel l in sorted)
            {
                List<RawLevel> b = buckets.LastOrDefault();
                if (b == null || Math.Abs(l.Price - b.Average(x => x.Price)) > merge)
                {
                    b = new List<RawLevel>();
                    buckets.Add(b);
                }
                b.Add(l);
            }

            foreach (List<RawLevel> b in buckets)
            {
                DealerZone z = new DealerZone();
                z.Price = b.Average(x => x.Price);
                z.Low = b.Min(x => x.Price);
                z.High = b.Max(x => x.Price);
                z.Gex = ScoreCategory(b, "GEX", "GAMMA", "CALL", "PUT", "FLIP");
                z.Dex = ScoreCategory(b, "DEX", "DELTA");
                z.Vanna = ScoreCategory(b, "VANNA");
                z.Charm = ScoreCategory(b, "CHARM");
                z.Iv = ScoreCategory(b, "IV", "CVR", "VOL");
                if (z.Iv < 1 && pulse.Status.StartsWith("OK", StringComparison.OrdinalIgnoreCase) && Math.Abs(pulse.CvrFlow) > 0)
                    z.Iv = GetLocalCvrScore(z.Price);
                z.Big = Math.Max(0, bigFlowTape.Where(e => Math.Abs(e.Price - z.Price) <= merge * 2).Select(e => e.Impact).DefaultIfEmpty(0).Max());
                RawLevel amountSource = b.Where(x => x.HasAmount).OrderByDescending(x => Math.Abs(x.Amount)).FirstOrDefault();
                z.HasAmount = amountSource != null;
                z.Amount = z.HasAmount ? amountSource.Amount : 0;
                BigFlowEvent amountEvent = bigFlowTape.Where(e => e.HasAmount && Math.Abs(e.Price - z.Price) <= merge * 2).OrderByDescending(e => Math.Abs(e.Amount)).FirstOrDefault();
                if (!z.HasAmount && amountEvent != null) { z.HasAmount = true; z.Amount = amountEvent.Amount; }
                z.Flow = GetLocalFlowScore(z.Price);
                z.Direction = CombinedDirection(b, z);
                z.Score = ComputeDealerZoneScore(z, b);
                z.Grade = Grade(z.Score);
                z.Bias = z.Direction < -15 ? "BAJISTA" : z.Direction > 15 ? "ALCISTA" : "MIXTO";
                z.Mode = InferZoneMode(z);
                ComputeZoneProbabilityLocked(z);
                RawLevel top = b.OrderByDescending(x => x.Strength).FirstOrDefault();
                z.Label = BuildZoneLabel(z, top, b);
                z.Active = DistanceToPrice(z) <= Math.Max(1, ActiveZoneTicks) * TickSize;
                z.Near = DistanceToPrice(z) <= Math.Max(1, NearZoneTicks) * TickSize;
                if (z.Score >= MinimumZoneScore || z.Active || z.Near)
                    dealerZones.Add(z);
            }

            dealerZones.Sort((a, b) =>
            {
                double aa = (a.Active ? 10000 : a.Near ? 5000 : 0) + a.Score;
                double bb = (b.Active ? 10000 : b.Near ? 5000 : 0) + b.Score;
                int c = bb.CompareTo(aa);
                if (c != 0) return c;
                return DistanceToPrice(a).CompareTo(DistanceToPrice(b));
            });
        }

        private double ComputeDealerZoneScore(DealerZone z, List<RawLevel> bucket)
        {
            // V1.1: el score ya no castiga una zona porque venga de una sola categoría.
            // Si GEXBot marca un strike importante o MaxChange fuerte, debe aparecer en el DOM.
            double weighted = 0.30 * z.Gex + 0.20 * z.Dex + 0.15 * z.Vanna + 0.10 * z.Charm + 0.10 * z.Iv + 0.10 * z.Flow + 0.15 * z.Big;
            double maxComponent = Math.Max(z.Gex, Math.Max(z.Dex, Math.Max(z.Vanna, Math.Max(z.Charm, Math.Max(z.Iv, Math.Max(z.Flow, z.Big))))));
            int evidence = 0;
            if (z.Gex >= 15) evidence++;
            if (z.Dex >= 15) evidence++;
            if (z.Vanna >= 15) evidence++;
            if (z.Charm >= 15) evidence++;
            if (z.Iv >= 15) evidence++;
            if (z.Flow >= 15) evidence++;
            if (z.Big >= 15) evidence++;

            double confluenceBonus = evidence >= 5 ? 18 : evidence == 4 ? 14 : evidence == 3 ? 10 : evidence == 2 ? 6 : 0;
            double sourceBonus = bucket.Select(x => x.Category).Distinct().Count() >= 2 ? 5 : 0;
            double bigBonus = z.Big >= BigFlowImpactThreshold ? 8 : 0;

            // Un solo strike con IMP 58 debe ser visible como zona B/C alta, no desaparecer.
            double singleSourceFloor = maxComponent >= 50 ? maxComponent * 0.86 : maxComponent * 0.72;
            double score = Math.Max(weighted, singleSourceFloor) + confluenceBonus + sourceBonus + bigBonus;
            return Math.Min(100, Math.Max(0, score));
        }

        private double ScoreCategory(List<RawLevel> b, params string[] keys)
        {
            double s = 0;
            foreach (RawLevel l in b)
            {
                string text = (l.Category + " " + l.Type + " " + l.Label + " " + l.Source).ToUpperInvariant();
                if (keys.Any(k => text.Contains(k)))
                    s = Math.Max(s, l.Strength);
            }
            return Math.Min(100, s);
        }

        private double GetLocalFlowScore(double price)
        {
            double basePulse = pulse.Status.StartsWith("OK", StringComparison.OrdinalIgnoreCase) ? pulse.Pulse : 0;
            // Orderflow is global; use full weight only close to current price, less weight for distant zones.
            double dTicks = Math.Abs(price - Close[0]) / Math.Max(TickSize, 0.0001);
            double weight = dTicks <= ActiveZoneTicks ? 1.0 : dTicks <= NearZoneTicks ? 0.65 : 0.35;
            return Math.Min(100, basePulse * weight);
        }

        private double GetLocalCvrScore(double price)
        {
            double basePulse = pulse.Status.StartsWith("OK", StringComparison.OrdinalIgnoreCase) ? NormalizeMagnitude(pulse.CvrFlow) : 0;
            double dTicks = Math.Abs(price - Close[0]) / Math.Max(TickSize, 0.0001);
            double weight = dTicks <= ActiveZoneTicks ? 1.0 : dTicks <= NearZoneTicks ? 0.65 : 0.35;
            return Math.Min(100, basePulse * weight);
        }

        private double CombinedDirection(List<RawLevel> b, DealerZone z)
        {
            double levelDir = b.Sum(x => x.Direction * Math.Max(1, x.Strength)) / Math.Max(1, b.Sum(x => Math.Max(1, x.Strength)));
            double flowDir = pulse.Status.StartsWith("OK", StringComparison.OrdinalIgnoreCase) ? pulse.Direction / 100.0 : 0;
            return Math.Max(-100, Math.Min(100, 100.0 * (0.62 * levelDir + 0.38 * flowDir)));
        }

        private string BuildZoneLabel(DealerZone z, RawLevel top, List<RawLevel> b)
        {
            if (top == null) return "ZONA DEALER";
            string prefix = top.Label;
            if (prefix.Length > 28) prefix = prefix.Substring(0, 28);
            string mix = string.Join("+", b.Select(x => ShortCat(x.Category)).Distinct().Take(4));
            string wall = b.Any(x => (x.Type ?? "").ToUpperInvariant().Contains("CALL")) ? "CALL" : b.Any(x => (x.Type ?? "").ToUpperInvariant().Contains("PUT")) ? "PUT" : "";
            if (!string.IsNullOrWhiteSpace(wall) && !prefix.Contains(wall)) prefix = prefix + " " + wall;
            return prefix + (string.IsNullOrWhiteSpace(mix) ? "" : " | " + mix);
        }

        private string ShortCat(string c)
        {
            c = (c ?? "").ToUpperInvariant();
            if (c.Contains("GEX_ZERO")) return "0DTE";
            if (c.Contains("GEX_ONE")) return "1DTE";
            if (c.Contains("GEX_FULL")) return "GEX";
            if (c.Contains("DELTA")) return "DEX";
            if (c.Contains("GAMMA")) return "GAM";
            if (c.Contains("VANNA")) return "VAN";
            if (c.Contains("CHARM")) return "CHR";
            return c.Length > 4 ? c.Substring(0, 4) : c;
        }

        private string InferZoneMode(DealerZone z)
        {
            bool strongFlow = z.Flow >= 60 || z.Big >= 65;
            bool strongGex = z.Gex >= 70;
            bool directional = Math.Abs(z.Direction) >= 35;
            if (directional && strongFlow && z.Direction < 0) return "CONT VENTA";
            if (directional && strongFlow && z.Direction > 0) return "CONT COMPRA";
            if (strongGex && z.Flow < 35 && z.Iv < 50) return "RECHAZO / PIN";
            if (strongGex && z.Big >= 70) return "ABS / LIBERACIÓN";
            if (z.Iv >= 70 && z.Direction < -10) return "IV/CVR CARA";
            if (z.Iv >= 70 && z.Direction > 10) return "IV/CVR SOPORTE";
            if (z.Vanna >= 65 || z.Charm >= 65) return "VANNA/CHARM";
            if (z.Score >= 75) return "ZONA CLAVE";
            return "ESPERAR";
        }

        // === Probabilidad por zona: REACCION% vs RUPTURA% (suman 100) ===
        // Lectura directa para el operador en el DOM, la banda y el HMI.
        //  - REACCION sube con: pared GEX fuerte, vanna/IV estabilizadores, flujo de
        //    capital EN CONTRA del acercamiento (defienden la zona) y gamma larga.
        //  - RUPTURA sube con: flujo fuerte A FAVOR del acercamiento, big flow en la
        //    zona, gamma corta y paredes GEX debiles.
        // Calibracion honesta: sin orderflow vivo o con zona de score bajo, el numero
        // se acerca a 50/50 (sin edge) en vez de fingir precision.
        private void ComputeZoneProbabilityLocked(DealerZone z)
        {
            bool pulseOk = pulse.Status.StartsWith("OK", StringComparison.OrdinalIgnoreCase);
            double approach = z.Price >= Close[0] ? 1.0 : -1.0;             // lado desde el que llega el precio
            double flowDir = pulseOk ? Math.Max(-1, Math.Min(1, pulse.Direction / 100.0)) : 0;
            double align = flowDir * approach;                               // >0 = el capital empuja HACIA/A TRAVES de la zona

            double rot = 0.45 * z.Gex
                       + 0.15 * z.Vanna
                       + 0.10 * z.Iv
                       + (align < 0 ? 40.0 * Math.Abs(align) : 0)
                       + (regimeText != null && regimeText.StartsWith("GAMMA LARGA") ? 10 : 0);

            double cont = (align > 0 ? 0.50 * z.Flow * align + 22.0 * align : 0.15 * z.Flow)
                        + 0.30 * z.Big
                        + (regimeText != null && regimeText.StartsWith("GAMMA CORTA") ? 14 : 0)
                        + Math.Max(0, 22.0 - z.Gex * 0.22);

            rot = Math.Max(0, Math.Min(100, rot));
            cont = Math.Max(0, Math.Min(100, cont));
            double total = rot + cont;
            double pReac = total > 1 ? rot / total * 100.0 : 50.0;

            double conf = 1.0;
            if (!pulseOk) conf *= 0.65;
            conf *= z.Score >= 85 ? 1.0 : z.Score >= 70 ? 0.92 : z.Score >= 55 ? 0.80 : 0.65;
            if (Math.Max(rot, cont) < 40) conf *= 0.70;

            pReac = 50.0 + (pReac - 50.0) * Math.Max(0, Math.Min(1, conf));
            z.ProbReaction = Math.Round(pReac);
            z.ProbRupture = 100.0 - z.ProbReaction;
        }

        private void UpdateDecisionStateLocked()
        {
            DealerZone active = dealerZones.FirstOrDefault(z => z.Active);
            DealerZone near = dealerZones.FirstOrDefault(z => z.Near);
            DealerZone top = active ?? near ?? dealerZones.FirstOrDefault();

            string flowState = pulse.Status.StartsWith("OK", StringComparison.OrdinalIgnoreCase) ? (pulse.Direction < -25 ? "PRESIÓN BAJISTA" : pulse.Direction > 25 ? "PRESIÓN ALCISTA" : "PRESIÓN MIXTA") : "SIN ORDERFLOW";
            dealerBiasText = flowState;
            regimeText = InferRegimeText();
            if (top == null)
            {
                BigFlowEvent bf = bigFlowTape.OrderByDescending(e => e.Impact).FirstOrDefault();
                if (bf != null)
                {
                    readText = "BIG FLOW SIN ZONA DOM";
                    activeZoneText = FormatPrice(bf.Price) + " | IMP " + bf.Impact.ToString("0") + (bf.HasAmount ? " | " + FormatAmountMM(bf.Amount) : "");
                    topZoneText = bf.Label;
                }
                else
                {
                    readText = "ESPERAR / SIN ZONAS GEXBOT";
                    activeZoneText = "N/A";
                    topZoneText = "N/A";
                }
                return;
            }

            activeZoneText = (active != null ? "ACTIVA " : near != null ? "CERCA " : "TOP ") + FormatPrice(top.Price) + " [" + top.Grade + "] RE " + top.ProbReaction.ToString("0") + "/RU " + top.ProbRupture.ToString("0") + (top.HasAmount ? " | " + FormatAmountMM(top.Amount) : "");
            topZoneText = top.Label + " | " + top.Mode;
            if (active != null)
                readText = top.Mode + " | ZONA ACTIVA";
            else if (near != null)
                readText = "PREPARAR | ZONA CERCANA";
            else
                readText = "MAPA DEALER | ESPERAR PRECIO";
        }

        private string InferRegimeText()
        {
            double gexBias = dealerZones.Take(5).Sum(z => z.Direction * (z.Gex / 100.0));
            if (pulse.Status.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
                gexBias += pulse.Direction * 0.45;
            if (gexBias < -35) return "GAMMA CORTA / EXPANSIÓN";
            if (gexBias > 35) return "GAMMA LARGA / REACCIÓN";
            return "TRANSICIÓN / MIXTO";
        }
        #endregion

        #region Rendering
        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);
            if (RenderTarget == null || ChartPanel == null || chartControl == null || chartScale == null)
                return;

            EnsureToolbarButtonsAsync();
            EnsureHmiDragHandlersAsync();

            List<DealerZone> zones;
            List<BigFlowEvent> tape;
            OrderflowPulse p = new OrderflowPulse();
            string status, read, regime, bias, active, top;
            lock (dataLock)
            {
                zones = dealerZones.Select(CloneZone).ToList();
                tape = bigFlowTape.Select(CloneTape).ToList();
                CopyPulse(pulse, p);
                status = apiStatus;
                read = readText;
                regime = regimeText;
                bias = dealerBiasText;
                active = activeZoneText;
                top = topZoneText;
            }

            if (ShowPriceZoneBands)
                DrawZoneBands(chartScale, zones);
            if (ShowOptionsDom)
                DrawOptionsDom(chartScale, zones);
            if (ShowBigFlowBubbles)
                DrawBigFlowBubbles(chartControl, chartScale, tape);
            if (ShowHmiPanel && runtimeHmiVisible)
            {
                if (runtimeHmiMinimized)
                    DrawHmiMiniPanel(p, status, read, regime, bias, active, top);
                else
                    DrawHmiPanel(zones, tape, p, status, read, regime, bias, active, top);
            }
        }

        public override void OnRenderTargetChanged()
        {
            base.OnRenderTargetChanged();
            DisposeDxResources();
            if (RenderTarget == null) return;
            bgBrush = CreateBrush(10, 15, 22, PanelOpacity / 100f);
            headerBrush = CreateBrush(16, 28, 40, 0.96f);
            borderBrush = CreateBrush(70, 95, 120, 0.75f);
            textBrush = CreateBrush(235, 245, 255, 0.97f);
            mutedBrush = CreateBrush(135, 155, 175, 0.92f);
            barBgBrush = CreateBrush(35, 48, 61, 0.94f);
            cyanBrush = CreateBrush(0, 195, 255, 0.98f);
            greenBrush = CreateBrush(38, 220, 135, 0.98f);
            yellowBrush = CreateBrush(245, 210, 75, 0.98f);
            orangeBrush = CreateBrush(255, 135, 65, 0.98f);
            redBrush = CreateBrush(255, 78, 92, 0.98f);
            magentaBrush = CreateBrush(225, 95, 255, 0.98f);
            whiteBrush = CreateBrush(255, 255, 255, 0.95f);
            softBlueBrush = CreateBrush(60, 115, 175, 0.88f);

            titleFmt = CreateFormat("Segoe UI Semibold", Math.Max(20, FontSize + 8));
            headFmt = CreateFormat("Segoe UI Semibold", Math.Max(17, FontSize + 3));
            textFmt = CreateFormat("Segoe UI Semibold", Math.Max(16, FontSize + 1));
            smallFmt = CreateFormat("Segoe UI Semibold", Math.Max(14, FontSize - 1));
            microFmt = CreateFormat("Segoe UI", Math.Max(12, FontSize - 3));
            bubbleFmt = CreateCenterFormat("Segoe UI Semibold", Math.Max(12, FontSize - 1));
            bubbleSmallFmt = CreateCenterFormat("Segoe UI Semibold", Math.Max(10, FontSize - 3));
            chipFmt = CreateCenterFormat("Segoe UI Semibold", Math.Max(13, FontSize - 2));
        }

        private void DrawHmiPanel(List<DealerZone> zones, List<BigFlowEvent> tape, OrderflowPulse p, string status, string read, string regime, string bias, string active, string top)
        {
            float w = Math.Max(860, PanelWidth);
            float h = Math.Max(650, PanelHeight);
            float x = GetPanelX(w);
            float y = GetPanelY(h);
            DrawRoundRect(x, y, w, h, 18, bgBrush, borderBrush, 1.3f);
            FillRect(x, y, w, 72, headerBrush);
            DrawText("ILT-DEV-GAMMATRIX OPTIONS FLOW", x + 24, y + 10, w - 360, 34, titleFmt, whiteBrush);
            DrawText("100% GEXBOT  |  DEALER ZONES  |  BIG FLOW", x + 24, y + 44, w - 360, 24, smallFmt, mutedBrush);
            DrawInternalHmiButtons(x, y, w);

            float cy = y + 88;
            float chipW = (w - 68) / 3f;
            DrawChip(x + 24, cy, chipW, 38, regime, RegimeBrush(regime));
            DrawChip(x + 34 + chipW, cy, chipW, 38, bias, DirectionBrush(p.Direction));
            DrawChip(x + 44 + chipW * 2, cy, chipW, 38, p.Status.StartsWith("OK") ? "ORDERFLOW OK" : "SIN OFLOW", p.Status.StartsWith("OK") ? greenBrush : orangeBrush);
            cy += 52;

            // Main decision card: bigger, centered visually in the layout.
            DrawCard(x + 24, cy, w - 48, 104, "LECTURA OPERATIVA", read + "\n" + active + "\n" + top, PickModeBrush(top));
            cy += 122;

            float cardGap = 14;
            float cardW = (w - 48 - cardGap * 2) / 3f;
            DrawCard(x + 24, cy, cardW, 98, "PULSO DEALER", "INT " + p.Pulse.ToString("0") + "   DIR " + p.Direction.ToString("+0;-0;0"), DirectionBrush(p.Direction));
            DrawCard(x + 24 + cardW + cardGap, cy, cardW, 98, "GEXBOT", status + (string.IsNullOrWhiteSpace(lastJsonSummary) ? "" : "\n" + CompactText(lastJsonSummary, 84)), status.StartsWith("API OK") ? greenBrush : status.StartsWith("API PARCIAL") ? orangeBrush : redBrush);
            DrawCard(x + 24 + (cardW + cardGap) * 2, cy, cardW, 98, "BIG FLOW", tape.Count > 0 ? tape[0].Label + "\nIMP " + tape[0].Impact.ToString("0") + (tape[0].HasAmount ? "   " + FormatAmountMM(tape[0].Amount) : "") : "SIN EVENTOS", tape.Count > 0 ? DirectionBrush(tape[0].Direction * 100) : mutedBrush);
            cy += 116;

            float gaugeH = 34;
            DrawGaugeRow(x + 28, cy, w - 56, "GEX FLOW", NormalizeMagnitude(p.GexFlow), DirectionBrush(p.Direction), p.GexFlow.ToString("0")); cy += gaugeH;
            DrawGaugeRow(x + 28, cy, w - 56, "DEX FLOW", NormalizeMagnitude(p.DexFlow), DirectionBrush(p.Direction), p.DexFlow.ToString("0")); cy += gaugeH;
            DrawGaugeRow(x + 28, cy, w - 56, "VANNA", NormalizeMagnitude(p.Vanna), magentaBrush, p.Vanna.ToString("0")); cy += gaugeH;
            DrawGaugeRow(x + 28, cy, w - 56, "CHARM", NormalizeMagnitude(p.Charm), yellowBrush, p.Charm.ToString("0")); cy += 44;

            float bottomTapeH = Math.Max(118, 24 + Math.Max(3, BigFlowTapeRows) * 22);
            float dealerMapHeight = Math.Max(300, y + h - cy - bottomTapeH - 34);
            DrawDealerMapPanel(x + 24, cy, w - 48, dealerMapHeight, zones);
            cy += dealerMapHeight + 14;
            DrawTapePanel(x + 24, cy, w - 48, Math.Min(bottomTapeH, y + h - cy - 18), tape);
        }

        private void DrawDealerMapPanel(float x, float y, float w, float h, List<DealerZone> zones)
        {
            DxBrush panelBrush = CreateBrush(16, 25, 36, 0.94f);
            DrawRoundRect(x, y, w, h, 14, panelBrush, null, 0);
            SafeDispose(panelBrush);
            DrawText("OPTIONS DOM / DEALER ZONES", x + 18, y + 12, w - 36, 30, headFmt, cyanBrush);

            float yy = y + 54;
            float colZone = x + 18;
            float colMM   = x + w * 0.33f;
            float colGex  = x + w * 0.55f;
            float colDex  = x + w * 0.64f;
            float colVan  = x + w * 0.73f;
            float colFlow = x + w * 0.82f;
            float colProb = x + w * 0.91f;

            DrawText("ZONA / STRIKE", colZone, yy, w * 0.30f, 26, smallFmt, mutedBrush);
            DrawText("MM ABS", colMM, yy, w * 0.20f, 26, smallFmt, mutedBrush);
            DrawText("GEX", colGex, yy, 70, 26, smallFmt, mutedBrush);
            DrawText("DEX", colDex, yy, 70, 26, smallFmt, mutedBrush);
            DrawText("VAN", colVan, yy, 70, 26, smallFmt, mutedBrush);
            DrawText("FLOW", colFlow, yy, 82, 26, smallFmt, mutedBrush);
            DrawText("PROB / MODO", colProb, yy, w * 0.09f, 26, smallFmt, mutedBrush);
            yy += 34;

            int rows = Math.Min(MaxVisibleZones, Math.Max(1, (int)((h - 96) / Math.Max(52, RowHeight))));
            double maxAmount = MaxAbsAmount(zones.Take(rows));
            foreach (DealerZone z in zones.Take(rows))
            {
                float rh = Math.Max(58, RowHeight - 6);
                DxBrush rowBrush = z.Active ? CreateBrush(34, 70, 76, 0.95f) : z.Near ? CreateBrush(26, 48, 60, 0.90f) : CreateBrush(18, 28, 38, 0.76f);
                DrawRoundRect(x + 8, yy, w - 16, rh, 8, rowBrush, null, 0);
                SafeDispose(rowBrush);

                double ratio = MmVisualRatio(z, maxAmount);
                DxBrush accent = z.Direction < -10 ? redBrush : z.Direction > 10 ? greenBrush : yellowBrush;

                FillRect(x + 8, yy, 6, rh, accent);
                FillRect(x + 8, yy, (w - 16) * (float)ratio, 4.5f, accent);

                float amountW = w * 0.20f;
                FillRoundRect(colMM, yy + 14, amountW, 15, 8, barBgBrush);
                FillRoundRect(colMM, yy + 14, amountW * (float)ratio, 15, 8, accent);

                string prefix = z.Active ? "● " : z.Near ? "○ " : "• ";
                DrawText(prefix + z.Label, colZone, yy + 8, w * 0.30f, 26, textFmt, z.Active ? whiteBrush : textBrush);
                DrawText(FormatPrice(z.Price), colZone, yy + 38, w * 0.30f, 24, smallFmt, mutedBrush);
                DrawText(z.HasAmount ? FormatAmountMM(z.Amount) : "-", colMM + 8, yy + 36, amountW - 16, 24, smallFmt, z.HasAmount ? yellowBrush : mutedBrush);

                DrawMiniBar(colGex, yy + 23, 78, 14, z.Gex, magentaBrush);
                DrawMiniBar(colDex, yy + 23, 78, 14, z.Dex, cyanBrush);
                DrawMiniBar(colVan, yy + 23, 78, 14, z.Vanna, yellowBrush);
                DrawMiniBar(colFlow, yy + 23, 88, 14, z.Flow, DirectionBrush(z.Direction));
                DrawText((z.ProbReaction >= z.ProbRupture ? "RE " + z.ProbReaction.ToString("0") : "RU " + z.ProbRupture.ToString("0")) + "%", colProb, yy + 8, w * 0.08f, 24, textFmt, PickModeBrush(z.Mode));
                DrawText(z.Mode, colProb, yy + 38, w * 0.08f, 24, smallFmt, PickModeBrush(z.Mode));

                yy += rh + 8;
            }
        }

        private void DrawTapePanel(float x, float y, float w, float h, List<BigFlowEvent> tape)
        {
            DxBrush tapeBrush = CreateBrush(14, 22, 31, 0.94f);
            DrawRoundRect(x, y, w, h, 12, tapeBrush, null, 0);
            SafeDispose(tapeBrush);
            DrawText("BIG FLOW TAPE", x + 16, y + 10, w - 32, 28, headFmt, orangeBrush);
            float yy = y + 44;
            foreach (BigFlowEvent e in tape.Take(Math.Max(1, BigFlowTapeRows)))
            {
                string line = e.Time.ToString("HH:mm:ss") + "  " + e.Label + "  " + FormatPrice(e.Price) + "  IMP " + e.Impact.ToString("0") + (e.HasAmount ? "  " + FormatAmountMM(e.Amount) : "");
                DrawText(line, x + 16, yy, w - 32, 24, smallFmt, DirectionBrush(e.Direction * 100));
                yy += 24;
                if (yy > y + h - 12) break;
            }
            if (tape.Count == 0)
                DrawText("Sin cambios nuevos | se conservan eventos recientes por 90 min", x + 16, yy, w - 32, 24, smallFmt, mutedBrush);
        }

        private void DrawOptionsDom(ChartScale chartScale, List<DealerZone> zones)
        {
            // V1.15: right-side detailed cards are intentionally disabled.
            // The chart now uses only clean right-edge MM interest bars, while the HMI panel
            // carries the detailed zone table. This avoids duplicated labels over the map.
            return;
        }

        private void DrawZoneBands(ChartScale chartScale, List<DealerZone> zones)
        {
            if (zones == null || zones.Count == 0 || RenderTarget == null || ChartPanel == null)
                return;

            // V1.23: right-edge MM interest bars with optional zone labels controlled by GEX ON/OFF.
            // - Bars remain anchored to the exact price through a dotted guide.
            // - When labels are ON, the label/bar block may shift slightly up/down to avoid overlap.
            // - The internal GEX ON/OFF button toggles only these chart labels, not the HMI panel.
            List<DealerZone> visibleZones = zones
                .Where(z => z != null && z.Score >= MinimumZoneScore && IsValidPriceCandidate(z.Price))
                .Take(MaxVisibleZones)
                .OrderBy(z => chartScale.GetYByValue(z.Price))
                .ToList();

            if (visibleZones.Count == 0)
                return;

            double maxAmount = MaxAbsAmount(visibleZones);
            float chartLeft = (float)ChartPanel.X;
            float chartRight = (float)(ChartPanel.X + ChartPanel.W);
            float chartWidth = (float)ChartPanel.W;
            float rightPad = 8f + Math.Max(0, LadderXOffset) * 0.10f;
            float barRight = chartRight - rightPad;
            float maxBarW = Math.Max(360f, Math.Min(chartWidth * 0.56f, Math.Max(520f, LadderWidth * 0.72f)));
            float minBarW = Math.Max(150f, Math.Min(maxBarW * 0.30f, 260f));
            float phase = (float)((Math.Sin(DateTime.UtcNow.TimeOfDay.TotalMilliseconds / 330.0) + 1.0) * 0.5);
            List<float> usedLabelCenters = new List<float>();

            foreach (DealerZone z in visibleZones)
            {
                float exactY = (float)chartScale.GetYByValue(z.Price);
                if (exactY < ChartPanel.Y - 100 || exactY > ChartPanel.Y + ChartPanel.H + 100)
                    continue;

                bool isActive = z.Active;
                bool isNear = z.Near;
                double ratio = MmVisualRatio(z, maxAmount);
                float barW = minBarW + (maxBarW - minBarW) * (float)Math.Max(0.0, Math.Min(1.0, ratio));
                barW = Math.Min(barW, Math.Max(90f, barRight - chartLeft - 14f));
                float barX = Math.Max(chartLeft + 12f, barRight - barW);
                float barH = runtimeZoneLabelsVisible
                    ? (isActive ? 62f : isNear ? 58f : 54f)
                    : (isActive ? 37f : isNear ? 33f : 29f);

                float centerY = exactY;
                if (runtimeZoneLabelsVisible)
                    centerY = ResolveZoneLabelCenter(exactY, barH + 8f, usedLabelCenters);

                float barY = centerY - barH / 2f;

                bool isFlipZone = IsFlipZone(z);
                byte rr = isFlipZone ? (byte)245 : z.Direction < -10 ? (byte)255 : z.Direction > 10 ? (byte)34 : (byte)245;
                byte gg = isFlipZone ? (byte)210 : z.Direction < -10 ? (byte)70  : z.Direction > 10 ? (byte)214 : (byte)210;
                byte bb = isFlipZone ? (byte)75  : z.Direction < -10 ? (byte)92  : z.Direction > 10 ? (byte)135 : (byte)75;
                DxBrush guideBrush = CreateBrush(rr, gg, bb, isActive ? 0.36f : isNear ? 0.25f : 0.17f);
                DxBrush shadow = CreateBrush(0, 0, 0, 0.32f);
                DxBrush body = CreateBrush(rr, gg, bb, isActive ? 0.46f : isNear ? 0.36f : 0.30f);
                DxBrush core = CreateBrush(rr, gg, bb, isActive ? 0.95f : 0.86f);
                DxBrush border = CreateBrush(rr, gg, bb, isActive ? (0.55f + 0.35f * phase) : isNear ? 0.48f : 0.30f);
                DxBrush glow = CreateBrush(rr, gg, bb, isActive ? (0.12f + 0.20f * phase) : isNear ? 0.08f : 0.00f);
                DxBrush textShadow = CreateBrush(0, 0, 0, 0.94f);
                DxBrush labelMuted = CreateBrush(225, 236, 245, 0.70f);

                try
                {
                    // Dotted exact-price guide. It stays on the real strike/price even when the label block is offset.
                    if (barX > chartLeft + 24f)
                        DrawDashedHorizontal(chartLeft + 2f, barX - 8f, exactY, guideBrush, isActive ? 1.6f : 1.0f, 9f, 7f);

                    // Short vertical/diagonal locator when the label block is shifted for overlap protection.
                    if (runtimeZoneLabelsVisible && Math.Abs(centerY - exactY) > 3f)
                    {
                        RenderTarget.DrawLine(new DxVec2(barX - 8f, exactY), new DxVec2(barX - 2f, centerY), guideBrush, 1.0f);
                    }

                    if (isActive || isNear)
                        FillRoundRect(barX - 2f, barY - 2f, barW + 4f, barH + 4f, 9, glow);

                    FillRoundRect(barX + 3, barY + 3, barW, barH, 7, shadow);
                    FillRoundRect(barX, barY, barW, barH, 7, body);

                    // Top histogram band: width is based on absolute MM / score.
                    float topBandH = Math.Max(5f, runtimeZoneLabelsVisible ? 6.5f : barH * 0.18f);
                    float bandX = barX + 8f;
                    float bandW = Math.Max(18f, (barW - 16f) * (float)Math.Max(0.16, Math.Min(1.0, ratio)));
                    FillRoundRect(bandX, barY + 4f, bandW, topBandH, topBandH / 2f, core);

                    if (isActive || isNear)
                        DrawRoundRect(barX, barY, barW, barH, 7, null, border, isActive ? 2.2f : 1.2f);

                    if (isActive)
                    {
                        float scanW = Math.Max(28f, barW * 0.16f);
                        float scanX = barX + 8f + (barW - scanW - 16f) * phase;
                        DxBrush scan = CreateBrush(255, 255, 255, 0.10f + 0.12f * phase);
                        FillRoundRect(scanX, barY + 5f, scanW, barH - 10f, 5, scan);
                        SafeDispose(scan);
                    }

                    string amount = z.HasAmount ? FormatAmountMM(z.Amount) : z.Score.ToString("0") + "%";
                    if (isFlipZone && !amount.ToUpperInvariant().StartsWith("FLIP") && !runtimeZoneLabelsVisible)
                        amount = "FLIP " + amount;

                    if (runtimeZoneLabelsVisible)
                    {
                        float pad = 13f;
                        string line1 = CompactText((isFlipZone ? "GAMMA FLIP | " : "") + z.Label, 46);
                        string line2 = FormatPrice(z.Price) + "   " + amount + "   " + z.Grade + "   RE " + z.ProbReaction.ToString("0") + "% / RU " + z.ProbRupture.ToString("0") + "%";
                        string mode = CompactText(z.Mode, 24);

                        DrawText(line1, barX + pad + 1.2f, barY + 12.5f + 1.2f, barW * 0.62f, 22f, smallFmt, textShadow);
                        DrawText(line1, barX + pad, barY + 12.5f, barW * 0.62f, 22f, smallFmt, whiteBrush);
                        DrawText(line2, barX + pad + 1.2f, barY + 35f + 1.2f, barW * 0.62f, 20f, microFmt, textShadow);
                        DrawText(line2, barX + pad, barY + 35f, barW * 0.62f, 20f, microFmt, isFlipZone ? yellowBrush : labelMuted);

                        float rightW = Math.Min(180f, Math.Max(110f, barW * 0.26f));
                        DrawTextCenter(mode, barX + barW - rightW - 12f + 1.2f, barY + 17f + 1.2f, rightW, 24f, smallFmt, textShadow);
                        DrawTextCenter(mode, barX + barW - rightW - 12f, barY + 17f, rightW, 24f, smallFmt, PickModeBrush(z.Mode));
                    }
                    else
                    {
                        float labelH = Math.Max(20f, FontSize + 5);
                        float labelW = Math.Min(barW - 22f, Math.Max(90f, amount.Length * Math.Max(10f, FontSize * 0.60f)));
                        float tx = barX + (barW - labelW) / 2f;
                        float ty = barY + (barH - labelH) / 2f - 1f;
                        DrawTextCenter(amount, tx + 1.4f, ty + 1.4f, labelW, labelH, headFmt, textShadow);
                        DrawTextCenter(amount, tx, ty, labelW, labelH, headFmt, whiteBrush);
                    }
                }
                finally
                {
                    SafeDispose(guideBrush);
                    SafeDispose(shadow);
                    SafeDispose(body);
                    SafeDispose(core);
                    SafeDispose(border);
                    SafeDispose(glow);
                    SafeDispose(textShadow);
                    SafeDispose(labelMuted);
                }
            }
        }

        private float ResolveZoneLabelCenter(float desiredCenterY, float fullHeight, List<float> usedCenters)
        {
            float minCenter = (float)ChartPanel.Y + fullHeight / 2f + 4f;
            float maxCenter = (float)(ChartPanel.Y + ChartPanel.H) - fullHeight / 2f - 4f;
            float y = Math.Max(minCenter, Math.Min(maxCenter, desiredCenterY));
            float minDistance = Math.Max(28f, fullHeight);

            bool changed = true;
            int guard = 0;
            while (changed && guard++ < 20)
            {
                changed = false;
                foreach (float used in usedCenters)
                {
                    if (Math.Abs(y - used) < minDistance)
                    {
                        y = used + minDistance;
                        y = Math.Max(minCenter, Math.Min(maxCenter, y));
                        changed = true;
                    }
                }
            }
            usedCenters.Add(y);
            return y;
        }

        private void DrawBigFlowBubbles(ChartControl cc, ChartScale cs, List<BigFlowEvent> tape)
        {
            if (tape.Count == 0 || ChartBars == null) return;

            int i = 0;
            double maxAmount = 0;
            foreach (BigFlowEvent ev in tape.Take(8))
                if (ev.HasAmount)
                    maxAmount = Math.Max(maxAmount, Math.Abs(ev.Amount));

            foreach (BigFlowEvent e in tape.Take(8))
            {
                if (!IsValidPriceCandidate(e.Price)) continue;

                int bar = Math.Max(0, CurrentBar - i * 3);
                float x = cc.GetXByBarIndex(ChartBars, bar);
                float y = (float)cs.GetYByValue(e.Price);

                double amountRatio = e.HasAmount && maxAmount > 0
                    ? Math.Sqrt(Math.Abs(e.Amount) / Math.Max(0.0000001, maxAmount))
                    : Math.Min(1.0, e.Impact / 100.0);

                // Bookmap-style soft bubble: visible but not cartoonish.
                // Area is proportional to relative MM/impact, not the text width.
                float r = 13 + (float)(29 * amountRatio);
                string txt = e.HasAmount ? FormatAmountBubble(e.Amount) : e.Impact.ToString("0");
                DrawBubbleBookmapStyle(x, y, r, e.Direction, txt);
                i++;
            }
        }

        private void DrawBubbleBookmapStyle(float cx, float cy, float radius, double direction, string text)
        {
            if (RenderTarget == null) return;

            bool sell = direction < -0.15;
            bool buy = direction > 0.15;

            // More neutral, institutional colors. No white border.
            DxBrush glowBrush = sell ? CreateBrush(255, 55, 72, 0.14f) : buy ? CreateBrush(20, 220, 140, 0.14f) : CreateBrush(235, 245, 255, 0.12f);
            DxBrush shadowBrush = CreateBrush(0, 0, 0, 0.42f);
            DxBrush baseBrush = sell ? CreateBrush(245, 42, 62, 0.78f) : buy ? CreateBrush(20, 206, 126, 0.78f) : CreateBrush(222, 232, 240, 0.72f);
            DxBrush lowerBrush = sell ? CreateBrush(145, 0, 18, 0.23f) : buy ? CreateBrush(0, 112, 72, 0.22f) : CreateBrush(80, 92, 104, 0.17f);
            DxBrush topGlassBrush = CreateBrush(255, 255, 255, sell || buy ? 0.22f : 0.34f);
            DxBrush capBrush = CreateBrush(255, 255, 255, sell || buy ? 0.18f : 0.28f);
            DxBrush textShadowBrush = CreateBrush(0, 0, 0, 0.88f);
            DxBrush textMainBrush = whiteBrush;

            try
            {
                // Shadow behind and soft glow around the marker.
                FillCircle(cx + radius * 0.12f, cy + radius * 0.16f, radius * 1.02f, shadowBrush, null, 0);
                FillCircle(cx, cy, radius * 1.20f, glowBrush, null, 0);

                // Main body.
                FillCircle(cx, cy, radius, baseBrush, null, 0);

                // Bottom depth, like a soft 3D sphere. No border/rim.
                FillCircle(cx, cy + radius * 0.32f, radius * 0.72f, lowerBrush, null, 0);

                // Top translucent white/gray glazing similar to the reference capture.
                FillCircle(cx - radius * 0.08f, cy - radius * 0.22f, radius * 0.72f, topGlassBrush, null, 0);
                FillCircle(cx - radius * 0.30f, cy - radius * 0.40f, radius * 0.26f, capBrush, null, 0);

                // Centered high-contrast label; no dark capsule, so it stays visually close to the reference.
                float labelH = Math.Max(18f, Math.Min(radius * 0.72f, FontSize + 12));
                float labelW = Math.Max(radius * 1.65f, text.Length * Math.Max(7.2f, FontSize * 0.52f));
                float tx = cx - labelW / 2f;
                float ty = cy - labelH / 2f;
                DW.TextFormat fmt = radius >= 24 ? bubbleFmt : bubbleSmallFmt;
                DrawTextCenter(text, tx + 1.2f, ty + 1.2f, labelW, labelH, fmt, textShadowBrush);
                DrawTextCenter(text, tx, ty, labelW, labelH, fmt, textMainBrush);
            }
            finally
            {
                SafeDispose(glowBrush);
                SafeDispose(shadowBrush);
                SafeDispose(baseBrush);
                SafeDispose(lowerBrush);
                SafeDispose(topGlassBrush);
                SafeDispose(capBrush);
                SafeDispose(textShadowBrush);
            }
        }

        private void DrawCard(float x, float y, float w, float h, string title, string value, DxBrush accent)
        {
            DxBrush cardBrush = CreateBrush(18, 30, 42, 0.90f);
            DrawRoundRect(x, y, w, h, 12, cardBrush, null, 0);
            SafeDispose(cardBrush);
            FillRect(x, y, 6, h, accent ?? cyanBrush);
            DrawText(title, x + 18, y + 10, w - 36, 24, smallFmt, mutedBrush);
            DrawText(value, x + 18, y + 38, w - 36, h - 40, textFmt, accent ?? textBrush);
        }

        private void DrawGaugeRow(float x, float y, float w, string label, double val, DxBrush b, string valueText)
        {
            DrawText(label, x, y, 155, 28, smallFmt, mutedBrush);
            FillRoundRect(x + 170, y + 8, w - 290, 16, 8, barBgBrush);
            FillRoundRect(x + 170, y + 8, (w - 290) * (float)Math.Max(0, Math.Min(100, val)) / 100f, 16, 8, b);
            DrawText(valueText, x + w - 105, y, 105, 28, smallFmt, b);
        }

        private void DrawPulseHistory(float x, float y, float w, float h)
        {
            DxBrush pulseBrush = CreateBrush(16, 25, 36, 0.93f);
            DrawRoundRect(x, y, w, h, 10, pulseBrush, null, 0);
            SafeDispose(pulseBrush);
            DrawText("PULSO ORDERFLOW GEXBOT", x + 12, y + 7, w - 24, 18, smallFmt, cyanBrush);
            List<double> hist;
            lock (dataLock) hist = pulseHistory.ToList();
            if (hist.Count < 2) return;
            float left = x + 12, top = y + 30, ww = w - 24, hh = h - 42;
            RenderTarget.DrawLine(new DxVec2(left, top + hh / 2), new DxVec2(left + ww, top + hh / 2), borderBrush, 0.7f);
            for (int i = 1; i < hist.Count; i++)
            {
                float x1 = left + ww * (i - 1) / Math.Max(1, hist.Count - 1);
                float x2 = left + ww * i / Math.Max(1, hist.Count - 1);
                float y1 = top + hh / 2 - (float)hist[i - 1] / 100f * hh / 2;
                float y2 = top + hh / 2 - (float)hist[i] / 100f * hh / 2;
                DxBrush b = hist[i] < -5 ? redBrush : hist[i] > 5 ? greenBrush : cyanBrush;
                RenderTarget.DrawLine(new DxVec2(x1, y1), new DxVec2(x2, y2), b, 1.6f);
            }
        }
        #endregion



        private void DrawInternalHmiButtons(float x, float y, float w)
        {
            // El toggle GEX vive ahora en la toolbar superior (menu real de la
            // ventana), no como boton dibujado + overlay. Se anulan los rects para
            // que ningun hit-test antiguo capture clics fantasma.
            hmiInternalToggleX = hmiInternalToggleY = hmiInternalToggleW = hmiInternalToggleH = 0;
            hmiInternalMinX = hmiInternalMinY = hmiInternalMinW = hmiInternalMinH = 0;
        }

        private void DrawPanelButton(float x, float y, float w, float h, string text, DxBrush accent)
        {
            DxBrush fill = CreateBrush(18, 35, 48, 0.96f);
            DrawRoundRect(x, y, w, h, 8, fill, accent ?? borderBrush, 1.2f);
            SafeDispose(fill);
            FillRect(x + 1, y + 1, w - 2, 4, accent ?? cyanBrush);
            DrawTextCenter(text, x + 4, y + 7, w - 8, h - 8, chipFmt, whiteBrush);
        }

        private bool HitTestPanelButton(System.Windows.Point p, float x, float y, float w, float h)
        {
            if (w <= 0 || h <= 0)
                return false;

            // V1.24: robust hit test for SharpDX-drawn HMI buttons.
            // Some NinjaTrader templates/DPI settings report mouse coordinates in WPF DIPs while
            // RenderTarget coordinates may be device pixels. Test raw and DPI-adjusted coordinates.
            double pad = 10.0;
            if (PointInRect(p.X, p.Y, x, y, w, h, pad))
                return true;

            try
            {
                double sx = 1.0, sy = 1.0;
                System.Windows.PresentationSource ps = ChartControl != null ? System.Windows.PresentationSource.FromVisual(ChartControl) : null;
                if (ps != null && ps.CompositionTarget != null)
                {
                    sx = ps.CompositionTarget.TransformToDevice.M11;
                    sy = ps.CompositionTarget.TransformToDevice.M22;
                }

                if (sx > 0 && sy > 0)
                {
                    if (PointInRect(p.X * sx, p.Y * sy, x, y, w, h, pad))
                        return true;
                    if (PointInRect(p.X / sx, p.Y / sy, x, y, w, h, pad))
                        return true;
                }
            }
            catch { }

            return false;
        }

        private bool PointInRect(double px, double py, float x, float y, float w, float h, double pad)
        {
            return px >= x - pad && px <= x + w + pad && py >= y - pad && py <= y + h + pad;
        }

        private void DrawHmiMiniPanel(OrderflowPulse p, string status, string read, string regime, string bias, string active, string top)
        {
            float w = Math.Min(Math.Max(520, PanelWidth * 0.52f), 820);
            float h = 108;
            float x = GetPanelX(w);
            float y = GetPanelY(h);
            lastHmiX = x; lastHmiY = y; lastHmiW = w; lastHmiH = h;
            DrawRoundRect(x, y, w, h, 14, bgBrush, borderBrush, 1.1f);
            FillRect(x, y, w, 40, headerBrush);
            DrawText("ILT-DEV-GAMMATRIX", x + 16, y + 7, w - 32, 27, headFmt, whiteBrush);
            float miniGap = 10f;
            float miniChipW = (w - 32f - miniGap * 2f) / 3f;
            DrawChip(x + 16, y + 46, miniChipW, 30, regime, RegimeBrush(regime));
            DrawChip(x + 16 + miniChipW + miniGap, y + 46, miniChipW, 30, bias, DirectionBrush(p.Direction));
            DrawChip(x + 16 + (miniChipW + miniGap) * 2, y + 46, miniChipW, 30, p.Status.StartsWith("OK") ? "OFLOW OK" : "SIN OFLOW", p.Status.StartsWith("OK") ? greenBrush : orangeBrush);
            DrawText(CompactText(active + " | " + top, 95), x + 16, y + 80, w - 32, 24, smallFmt, PickModeBrush(top));
        }

        private void EnsureToolbarButtonsAsync()
        {
            if (toolbarInstallRequested || ChartControl == null)
                return;
            toolbarInstallRequested = true;
            try
            {
                ChartControl.Dispatcher.BeginInvoke(new Action(InstallToolbarButtons));
            }
            catch { }
        }

        private void InstallToolbarButtons()
        {
            try
            {
                if (toolbarMenuItem != null || ChartControl == null)
                    return;

                // Botones INTEGRADOS en la toolbar superior real de la ventana del
                // grafico (MainMenu), igual que Disco Trading y el resto de la suite
                // ILT-DEV. Esto reemplaza el boton flotante sobre el chart y el overlay
                // WPF del GEX ON/OFF: los overlays posicionados con coordenadas de
                // render se desalinean con DPI/offsets y el hit-test duplicaba el
                // toggle (doble alternancia = "no funciona").
                chartWindowRef = System.Windows.Window.GetWindow(ChartControl.Parent) as NinjaTrader.Gui.Chart.Chart;
                if (chartWindowRef == null || chartWindowRef.MainMenu == null)
                    return;

                toolbarMenuItem = new NinjaTrader.Gui.Tools.NTMenuItem
                {
                    Header = "ILT-DEV-GAMMATRIX",
                    Style = System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style
                };
                toolbarMenuItem.Click += (s2, e2) =>
                {
                    runtimeHmiVisible = !runtimeHmiVisible;
                    if (!runtimeHmiVisible)
                        runtimeHmiMinimized = false;
                    UpdateToolbarButtons();
                    SafeInvalidateChart();
                };
                chartWindowRef.MainMenu.Add(toolbarMenuItem);

                gexMenuItem = new NinjaTrader.Gui.Tools.NTMenuItem
                {
                    Header = "GEX \u2713",
                    Style = System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style
                };
                gexMenuItem.Click += (s2, e2) =>
                {
                    runtimeZoneLabelsVisible = !runtimeZoneLabelsVisible;
                    UpdateToolbarButtons();
                    SafeInvalidateChart();
                };
                chartWindowRef.MainMenu.Add(gexMenuItem);

                UpdateToolbarButtons();
            }
            catch { }
        }

        private Button CreateGexOverlayButton()
        {
            Button b = new Button
            {
                Content = runtimeZoneLabelsVisible ? "GEX ON" : "GEX OFF",
                Width = 134,
                Height = 34,
                Margin = new Thickness(0, 0, 0, 0),
                Padding = new Thickness(8, 0, 8, 1),
                FontSize = Math.Max(13, Math.Min(18, FontSize - 4)),
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 245, 255, 250)),
                Background = new SolidColorBrush(Color.FromArgb(235, 18, 35, 48)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 38, 220, 135)),
                BorderThickness = new Thickness(1.2),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand,
                Visibility = Visibility.Collapsed
            };
            return b;
        }

        private void UpdateGexLabelToggleOverlayAsync()
        {
            try
            {
                if (ChartControl != null)
                    ChartControl.Dispatcher.BeginInvoke(new Action(UpdateGexLabelToggleOverlay));
            }
            catch { }
        }

        private void UpdateGexLabelToggleOverlay()
        {
            // Obsoleto: el toggle GEX vive en la toolbar superior. Si quedara un
            // overlay creado por una version anterior, se mantiene oculto.
            try { if (gexLabelToggleButton != null) gexLabelToggleButton.Visibility = Visibility.Collapsed; } catch { }
        }

        private Button CreateToolbarButton(string text, double width)
        {
            Button b = new Button
            {
                Content = text,
                Width = width,
                Height = 24,
                Margin = new Thickness(0, 0, 0, 0),
                Padding = new Thickness(8, 0, 8, 1),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 245, 250, 255)),
                Background = new SolidColorBrush(Color.FromArgb(235, 42, 130, 210)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            return b;
        }

        private void UpdateToolbarButtons()
        {
            try
            {
                if (toolbarMenuItem != null)
                {
                    toolbarMenuItem.Header = "ILT-DEV-GAMMATRIX";
                    toolbarMenuItem.ToolTip = runtimeHmiVisible ? "Ocultar panel ILT-DEV GAMMATRIX" : "Mostrar panel ILT-DEV GAMMATRIX";
                    toolbarMenuItem.Foreground = runtimeHmiVisible
                        ? new SolidColorBrush(Color.FromArgb(255, 90, 190, 255))
                        : new SolidColorBrush(Color.FromArgb(230, 160, 172, 184));
                }
                if (gexMenuItem != null)
                {
                    gexMenuItem.Header = runtimeZoneLabelsVisible ? "GEX \u2713" : "GEX \u2013";
                    gexMenuItem.ToolTip = runtimeZoneLabelsVisible
                        ? "Ocultar etiquetas de zonas (deja solo barras MM)"
                        : "Mostrar etiquetas completas de zonas";
                    gexMenuItem.Foreground = runtimeZoneLabelsVisible
                        ? new SolidColorBrush(Color.FromArgb(255, 70, 225, 150))
                        : new SolidColorBrush(Color.FromArgb(230, 160, 172, 184));
                }
            }
            catch { }
        }

        private void EnsureHmiDragHandlersAsync()
        {
            if (hmiDragHandlersAttached || ChartControl == null)
                return;
            try
            {
                ChartControl.Dispatcher.BeginInvoke(new Action(AttachHmiDragHandlers));
            }
            catch { }
        }

        private void AttachHmiDragHandlers()
        {
            try
            {
                if (hmiDragHandlersAttached || ChartControl == null)
                    return;
                ChartControl.MouseDown += OnHmiChartMouseDown;
                ChartControl.MouseMove += OnHmiChartMouseMove;
                ChartControl.MouseUp += OnHmiChartMouseUp;
                hmiDragHandlersAttached = true;
            }
            catch { }
        }

        private void RemoveHmiDragHandlersAsync()
        {
            try
            {
                if (ChartControl != null)
                    ChartControl.Dispatcher.BeginInvoke(new Action(RemoveHmiDragHandlers));
                else
                    RemoveHmiDragHandlers();
            }
            catch { RemoveHmiDragHandlers(); }
        }

        private void RemoveHmiDragHandlers()
        {
            try
            {
                if (hmiDragHandlersAttached && ChartControl != null)
                {
                    ChartControl.MouseDown -= OnHmiChartMouseDown;
                    ChartControl.MouseMove -= OnHmiChartMouseMove;
                    ChartControl.MouseUp -= OnHmiChartMouseUp;
                }
            }
            catch { }
            hmiDragHandlersAttached = false;
            hmiIsDragging = false;
        }

        private void OnHmiChartMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (!runtimeHmiVisible || ChartControl == null || lastHmiW <= 0 || lastHmiH <= 0)
                    return;

                System.Windows.Point p = e.GetPosition(ChartControl);

                bool insidePanel = p.X >= lastHmiX && p.X <= lastHmiX + lastHmiW && p.Y >= lastHmiY && p.Y <= lastHmiY + lastHmiH;
                bool inHeader = p.Y <= lastHmiY + 72;
                if (!insidePanel || !inHeader)
                    return;

                hmiIsDragging = true;
                hmiDragStartPoint = p;
                hmiDragStartOffsetX = PanelXOffset;
                hmiDragStartOffsetY = PanelYOffset;
                PanelAnchor = "TopLeft";
                try { ChartControl.CaptureMouse(); } catch { }
                e.Handled = true;
            }
            catch { }
        }

        private void OnHmiChartMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                if (!hmiIsDragging || ChartControl == null || ChartPanel == null)
                    return;
                System.Windows.Point p = e.GetPosition(ChartControl);
                double dx = p.X - hmiDragStartPoint.X;
                double dy = p.Y - hmiDragStartPoint.Y;
                int nx = hmiDragStartOffsetX + (int)Math.Round(dx);
                int ny = hmiDragStartOffsetY + (int)Math.Round(dy);
                int maxX = Math.Max(0, (int)(ChartPanel.W - Math.Max(260, lastHmiW)));
                int maxY = Math.Max(0, (int)(ChartPanel.H - Math.Max(80, lastHmiH)));
                PanelXOffset = Math.Max(0, Math.Min(maxX, nx));
                PanelYOffset = Math.Max(0, Math.Min(maxY, ny));
                SafeInvalidateChart();
                e.Handled = true;
            }
            catch { }
        }

        private void OnHmiChartMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (!hmiIsDragging)
                    return;
                hmiIsDragging = false;
                if (ChartControl != null)
                    try { ChartControl.ReleaseMouseCapture(); } catch { }
                SafeInvalidateChart();
                e.Handled = true;
            }
            catch { }
        }

        private void RemoveToolbarButtonsAsync()
        {
            try
            {
                if (ChartControl != null)
                    ChartControl.Dispatcher.BeginInvoke(new Action(RemoveToolbarButtons));
                else
                    RemoveToolbarButtons();
            }
            catch { RemoveToolbarButtons(); }
        }

        private void RemoveToolbarButtons()
        {
            try
            {
                if (hmiToolbarHost != null && hmiToolbarPanel != null)
                    hmiToolbarHost.Children.Remove(hmiToolbarPanel);
            }
            catch { }
            try
            {
                if (hmiToolbarHost != null && gexLabelToggleButton != null)
                    hmiToolbarHost.Children.Remove(gexLabelToggleButton);
            }
            catch { }
            try
            {
                if (chartWindowRef != null && chartWindowRef.MainMenu != null)
                {
                    if (toolbarMenuItem != null && chartWindowRef.MainMenu.Contains(toolbarMenuItem))
                        chartWindowRef.MainMenu.Remove(toolbarMenuItem);
                    if (gexMenuItem != null && chartWindowRef.MainMenu.Contains(gexMenuItem))
                        chartWindowRef.MainMenu.Remove(gexMenuItem);
                }
            }
            catch { }
            toolbarMenuItem = null;
            gexMenuItem = null;
            chartWindowRef = null;

            hmiToolbarPanel = null;
            hmiToggleButton = null;
            gexLabelToggleButton = null;
            hmiMinimizeButton = null;
            hmiToolbarHost = null;
            toolbarInstallRequested = false;
        }

        private void SafeInvalidateChart()
        {
            try
            {
                if (ChartControl != null)
                    ChartControl.InvalidateVisual();
            }
            catch { }
        }

        private string CompactText(string s, int maxChars)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            s = s.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return s.Length <= maxChars ? s : s.Substring(0, Math.Max(1, maxChars - 3)) + "...";
        }

        #region Drawing helpers
        private float GetPanelX(float w)
        {
            string a = (PanelAnchor ?? "TopRight").ToLowerInvariant();
            if (a.Contains("left")) return (float)ChartPanel.X + PanelXOffset;
            return (float)ChartPanel.X + (float)ChartPanel.W - w - PanelXOffset;
        }
        private float GetPanelY(float h)
        {
            string a = (PanelAnchor ?? "TopRight").ToLowerInvariant();
            if (a.Contains("bottom")) return (float)ChartPanel.Y + (float)ChartPanel.H - h - PanelYOffset;
            return (float)ChartPanel.Y + PanelYOffset;
        }
        private DxBrush CreateBrush(byte r, byte g, byte b, float a) { return RenderTarget == null ? null : new DxBrush(RenderTarget, new DxColor4(r / 255f, g / 255f, b / 255f, a)); }
        private DW.TextFormat CreateFormat(string family, float size) { DW.TextFormat f = new DW.TextFormat(Core.Globals.DirectWriteFactory, family, size); f.WordWrapping = DW.WordWrapping.NoWrap; return f; }
        private DW.TextFormat CreateCenterFormat(string family, float size)
        {
            DW.TextFormat f = new DW.TextFormat(Core.Globals.DirectWriteFactory, family, size);
            f.WordWrapping = DW.WordWrapping.NoWrap;
            f.TextAlignment = DW.TextAlignment.Center;
            f.ParagraphAlignment = DW.ParagraphAlignment.Center;
            return f;
        }
        private void FillRect(float x, float y, float w, float h, DxBrush b) { if (b != null) RenderTarget.FillRectangle(new DxRawRect(x, y, w, h), b); }
        private void DrawRoundRect(float x, float y, float w, float h, float r, DxBrush fill, DxBrush stroke, float sw)
        {
            D2D.RoundedRectangle rr = new D2D.RoundedRectangle { Rect = new DxRawRect(x, y, w, h), RadiusX = r, RadiusY = r };
            if (fill != null) RenderTarget.FillRoundedRectangle(rr, fill);
            if (stroke != null && sw > 0) RenderTarget.DrawRoundedRectangle(rr, stroke, sw);
        }
        private void FillRoundRect(float x, float y, float w, float h, float r, DxBrush fill) { DrawRoundRect(x, y, w, h, r, fill, null, 0); }
        private void FillCircle(float cx, float cy, float radius, DxBrush fill, DxBrush stroke, float strokeWidth)
        {
            D2D.Ellipse ellipse = new D2D.Ellipse(new DxVec2(cx, cy), radius, radius);
            if (fill != null) RenderTarget.FillEllipse(ellipse, fill);
            if (stroke != null && strokeWidth > 0) RenderTarget.DrawEllipse(ellipse, stroke, strokeWidth);
        }
        private void DrawText(string text, float x, float y, float w, float h, DW.TextFormat fmt, DxBrush b)
        {
            if (string.IsNullOrEmpty(text) || fmt == null || b == null) return;
            RenderTarget.DrawText(text, fmt, new DxRawRect(x, y, w, h), b);
        }
        private void DrawTextCenter(string text, float x, float y, float w, float h, DW.TextFormat fmt, DxBrush b)
        {
            if (string.IsNullOrEmpty(text) || fmt == null || b == null) return;
            RenderTarget.DrawText(text, fmt, new DxRawRect(x, y, w, h), b);
        }
        private void DrawChip(float x, float y, float w, float h, string text, DxBrush b)
        {
            // V1.18: header chips are true centered pills.
            // This keeps the object/text symmetric even when the panel is resized.
            DrawRoundRect(x, y, w, h, h / 2, b ?? barBgBrush, null, 0);
            DrawTextCenter(text, x + 8, y + 1, w - 16, h - 2, chipFmt ?? bubbleSmallFmt ?? microFmt, whiteBrush);
        }
        private void DrawMiniBar(float x, float y, float w, float h, double val, DxBrush b)
        {
            FillRoundRect(x, y, w, h, h / 2, barBgBrush);
            FillRoundRect(x, y, w * (float)Math.Max(0, Math.Min(100, val)) / 100f, h, h / 2, b ?? cyanBrush);
        }

        private void DrawDashedHorizontal(float x1, float x2, float y, DxBrush brush, float thickness, float dashLength, float gapLength)
        {
            if (brush == null || x2 <= x1 || RenderTarget == null)
                return;
            float x = x1;
            float dash = Math.Max(2f, dashLength);
            float gap = Math.Max(2f, gapLength);
            while (x < x2)
            {
                float xe = Math.Min(x + dash, x2);
                RenderTarget.DrawLine(new DxVec2(x, y), new DxVec2(xe, y), brush, thickness);
                x += dash + gap;
            }
        }

        private bool IsFlipZone(DealerZone z)
        {
            if (z == null)
                return false;
            string s = ((z.Label ?? string.Empty) + " " + (z.Mode ?? string.Empty) + " " + (z.Bias ?? string.Empty)).ToUpperInvariant();
            return s.Contains("GAMMA FLIP") || s.Contains("ZERO GAMMA") || s.Contains(" FLIP") || s.Contains("| FLIP");
        }

        private DxBrush DirectionBrush(double d) { return d < -15 ? redBrush : d > 15 ? greenBrush : cyanBrush; }
        private DxBrush RegimeBrush(string s) { s = (s ?? "").ToUpperInvariant(); return s.Contains("CORTA") ? redBrush : s.Contains("LARGA") ? greenBrush : yellowBrush; }
        private DxBrush PickModeBrush(string s)
        {
            s = (s ?? "").ToUpperInvariant();
            if (s.Contains("CONT") || s.Contains("COMPRA")) return greenBrush;
            if (s.Contains("VENTA")) return redBrush;
            if (s.Contains("RECHAZ") || s.Contains("PIN")) return yellowBrush;
            if (s.Contains("ABS") || s.Contains("RISK")) return orangeBrush;
            return cyanBrush;
        }
        private void SafeDispose(IDisposable d) { if (d != null) try { d.Dispose(); } catch { } }
        private void DisposeDxResources()
        {
            SafeDispose(bgBrush); SafeDispose(headerBrush); SafeDispose(borderBrush); SafeDispose(textBrush); SafeDispose(mutedBrush); SafeDispose(barBgBrush);
            SafeDispose(cyanBrush); SafeDispose(greenBrush); SafeDispose(yellowBrush); SafeDispose(orangeBrush); SafeDispose(redBrush); SafeDispose(magentaBrush); SafeDispose(whiteBrush); SafeDispose(softBlueBrush);
            SafeDispose(titleFmt); SafeDispose(headFmt); SafeDispose(textFmt); SafeDispose(smallFmt); SafeDispose(microFmt); SafeDispose(bubbleFmt); SafeDispose(bubbleSmallFmt); SafeDispose(chipFmt);
            bgBrush = headerBrush = borderBrush = textBrush = mutedBrush = barBgBrush = null;
            cyanBrush = greenBrush = yellowBrush = orangeBrush = redBrush = magentaBrush = whiteBrush = softBlueBrush = null;
            titleFmt = headFmt = textFmt = smallFmt = microFmt = bubbleFmt = bubbleSmallFmt = chipFmt = null;
        }
        #endregion

        #region Utilities
        private Dictionary<double, double> ExtractStrikeAmountMap(string json)
        {
            Dictionary<double, double> map = new Dictionary<double, double>();
            if (string.IsNullOrWhiteSpace(json)) return map;
            foreach (Match m in Regex.Matches(json, @"\[\s*(-?\d+(?:[\.,]\d+)?)\s*,\s*(-?\d+(?:[\.,]\d+)?(?:[eE][-+]?\d+)?)\s*,\s*(-?\d+(?:[\.,]\d+)?(?:[eE][-+]?\d+)?)", RegexOptions.Singleline))
            {
                double strike, a, b;
                if (!TryParseDouble(m.Groups[1].Value, out strike) || !TryParseDouble(m.Groups[2].Value, out a) || !TryParseDouble(m.Groups[3].Value, out b))
                    continue;
                if (!IsValidPriceCandidate(strike))
                    continue;
                double amount = Math.Abs(a) >= Math.Abs(b) ? a : b;
                if (!map.ContainsKey(strike) || Math.Abs(amount) > Math.Abs(map[strike]))
                    map[strike] = amount;
            }
            return map;
        }

        private double LookupStrikeAmount(Dictionary<double, double> map, double price)
        {
            if (map == null || map.Count == 0 || !IsValidPriceCandidate(price)) return double.NaN;
            double bestKey = double.NaN;
            double bestDist = double.MaxValue;
            foreach (double k in map.Keys)
            {
                double d = Math.Abs(k - price);
                if (d < bestDist) { bestDist = d; bestKey = k; }
            }
            double tolerance = Math.Max(TickSize * Math.Max(1, MergeZoneTicks), 8 * TickSize);
            if (!double.IsNaN(bestKey) && bestDist <= tolerance) return map[bestKey];
            return double.NaN;
        }

        private double ExtractAmountCandidate(string block, double rawFallback)
        {
            double amount = ExtractNumber(block, "notional", "premium", "exposure", "gex", "dex", "vanna", "charm", "value", "amount", "change", "change_value", "changeValue");
            if (!double.IsNaN(amount) && Math.Abs(amount) > 0) return amount;
            return rawFallback;
        }

        private string FormatAmountBubble(double amount)
        {
            if (double.IsNaN(amount) || double.IsInfinity(amount) || amount == 0) return "-";
            double a = Math.Abs(amount);
            string sign = amount < 0 ? "-" : "+";
            // Compact label for circular markers: avoids unreadable long text inside the bubble.
            double mm = a >= 1000000 ? a / 1000000.0 : a;
            if (mm >= 1000) return sign + (mm / 1000.0).ToString("0.0") + "B";
            if (mm >= 100) return sign + mm.ToString("0") + "M";
            if (mm >= 10) return sign + mm.ToString("0") + "M";
            return sign + mm.ToString("0.0") + "M";
        }

        private string FormatAmountMM(double amount)
        {
            if (double.IsNaN(amount) || double.IsInfinity(amount) || amount == 0) return "-";
            double a = Math.Abs(amount);
            string sign = amount < 0 ? "-" : "+";
            // GEXBot strike values commonly arrive already in MM-style chart units; very large notionals are converted to MM.
            double mm = a >= 1000000 ? a / 1000000.0 : a;
            if (mm >= 100) return sign + mm.ToString("0") + "MM";
            if (mm >= 10) return sign + mm.ToString("0.0") + "MM";
            return sign + mm.ToString("0.00") + "MM";
        }


        private double MaxAbsAmount(IEnumerable<DealerZone> zones)
        {
            double max = 0;
            if (zones == null) return 0;
            foreach (DealerZone z in zones)
                if (z != null && z.HasAmount)
                    max = Math.Max(max, Math.Abs(z.Amount));
            return max;
        }

        private double MmVisualRatio(DealerZone z, double maxAmount)
        {
            if (z == null) return 0.45;
            if (z.HasAmount && maxAmount > 0)
            {
                double r = Math.Sqrt(Math.Abs(z.Amount) / Math.Max(0.0000001, maxAmount));
                return Math.Max(0.38, Math.Min(1.0, 0.22 + 0.78 * r));
            }
            // If the API did not provide an MM amount for this row, use score as a fallback but keep it visually smaller.
            return Math.Max(0.42, Math.Min(0.72, 0.30 + 0.42 * (z.Score / 100.0)));
        }

        private double ExtractNumber(string json, params string[] keys)
        {
            if (string.IsNullOrEmpty(json)) return double.NaN;
            foreach (string k in keys)
            {
                Match m = Regex.Match(json, "[\"']" + Regex.Escape(k) + "[\"']\\s*:\\s*[\"']?(-?\\d+(?:[\\.,]\\d+)?(?:[eE][-+]?\\d+)?)[\"']?", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    double v;
                    if (TryParseDouble(m.Groups[1].Value, out v)) return v;
                }
            }
            return double.NaN;
        }
        private string ExtractString(string json, params string[] keys)
        {
            foreach (string k in keys)
            {
                Match m = Regex.Match(json, "[\"']" + Regex.Escape(k) + "[\"']\\s*:\\s*[\"']([^\"']{1,80})[\"']", RegexOptions.IgnoreCase);
                if (m.Success) return m.Groups[1].Value;
            }
            return string.Empty;
        }
        private double SumNumbers(string json, params string[] keys)
        {
            double sum = 0; bool any = false;
            foreach (string k in keys)
            {
                foreach (Match m in Regex.Matches(json, "[\"']" + Regex.Escape(k) + "[\"']\\s*:\\s*[\"']?(-?\\d+(?:[\\.,]\\d+)?(?:[eE][-+]?\\d+)?)[\"']?", RegexOptions.IgnoreCase))
                {
                    double v; if (TryParseDouble(m.Groups[1].Value, out v)) { sum += v; any = true; }
                }
            }
            return any ? sum : 0;
        }
        private bool TryParseDouble(string s, out double v)
        {
            s = (s ?? "").Trim().Replace(",", ".");
            return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v);
        }
        private bool IsValidPriceCandidate(double p)
        {
            if (double.IsNaN(p) || double.IsInfinity(p) || p <= 0) return false;
            if (p < 500) return false;
            return true;
        }
        private double NormalizeMagnitude(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return 0;
            double a = Math.Abs(v);
            if (a <= 1.0) return Math.Min(100, a * 100.0);
            if (a <= 10.0) return Math.Min(100, a * 10.0);
            double norm = Math.Max(1000, FlowNormalizer);
            return Math.Min(100, 100.0 * Math.Log10(1 + a) / Math.Log10(1 + norm));
        }
        private double NormalizeSigned(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v) || v == 0) return 0;
            double s = Math.Sign(v) * NormalizeMagnitude(v);
            return Math.Max(-100, Math.Min(100, s));
        }
        private string NormalizeOptionLabel(string label, string block, string cat)
        {
            string l = (label ?? string.Empty).Trim();
            string txt = (block ?? string.Empty).ToUpperInvariant();
            string up = l.ToUpperInvariant();
            if (up == "C" || up == "CALLS" || up == "CALL") return "MAJOR CALL";
            if (up == "P" || up == "PUTS" || up == "PUT") return "MAJOR PUT";
            if (Regex.IsMatch(txt, "\\\"RIGHT\\\"\\s*:\\s*\\\"C\\\"", RegexOptions.IgnoreCase) || txt.Contains("OPTION_TYPE\":\"CALL") || txt.Contains("CONTRACT_TYPE\":\"CALL")) return "MAJOR CALL";
            if (Regex.IsMatch(txt, "\\\"RIGHT\\\"\\s*:\\s*\\\"P\\\"", RegexOptions.IgnoreCase) || txt.Contains("OPTION_TYPE\":\"PUT") || txt.Contains("CONTRACT_TYPE\":\"PUT")) return "MAJOR PUT";
            return string.IsNullOrWhiteSpace(l) ? cat : l;
        }

        private double InferDirectionFromText(string s)
        {
            s = (s ?? "").ToUpperInvariant();
            if (Regex.IsMatch(s, "\\\"(RIGHT|PUT_CALL|PUTCALL|OPTION_TYPE|CONTRACT_TYPE)\\\"\\s*:\\s*\\\"P(UT)?\\\"", RegexOptions.IgnoreCase)) return -1;
            if (Regex.IsMatch(s, "\\\"(RIGHT|PUT_CALL|PUTCALL|OPTION_TYPE|CONTRACT_TYPE)\\\"\\s*:\\s*\\\"C(ALL)?\\\"", RegexOptions.IgnoreCase)) return 1;
            if (s.Contains("PUT") || s.Contains("NEG") || s.Contains("SHORT") || s.Contains("BEAR") || s.Contains("SELL")) return -1;
            if (s.Contains("CALL") || s.Contains("POS") || s.Contains("LONG") || s.Contains("BULL") || s.Contains("BUY")) return 1;
            return 0;
        }
        private double InferDirectionFromCategory(string cat, string label, double raw)
        {
            double d = InferDirectionFromText(cat + " " + label);
            if (d != 0) return d;
            if (!double.IsNaN(raw) && raw != 0) return Math.Sign(raw);
            return 0;
        }
        private string BuildBigFlowLabel(string label, string cat, double impact, double dir)
        {
            string side = dir < -0.2 ? "PUT/BAJISTA" : dir > 0.2 ? "CALL/ALCISTA" : "CAMBIO";
            string baseLabel = ShortCat(cat);
            if (!string.IsNullOrWhiteSpace(label) && label != cat && !label.Contains("GEXBOT"))
                baseLabel = label;
            return (baseLabel + " " + side).Trim();
        }

        private string CleanLabel(string label, string cat)
        {
            string l = (label ?? "").Trim();
            if (l.Length == 0) l = cat;
            l = l.Replace("_", " ").ToUpperInvariant();
            if (l.Length > 22) l = l.Substring(0, 22);
            return l;
        }
        private string Grade(double s) { return s >= 85 ? "A+" : s >= 70 ? "A" : s >= 55 ? "B" : "C"; }
        private double DistanceToPrice(DealerZone z) { return Math.Abs(Close[0] - z.Price); }
        private string FormatPrice(double p) { return Instrument != null && Instrument.MasterInstrument != null ? Instrument.MasterInstrument.FormatPrice(p) : p.ToString("0.00"); }
        private string ShortError(string s) { if (string.IsNullOrEmpty(s)) return "ERR"; s = s.Replace('\r', ' ').Replace('\n', ' '); return s.Length <= 58 ? s : s.Substring(0, 58) + "..."; }
        private DealerZone CloneZone(DealerZone z) { return new DealerZone { Price = z.Price, Low = z.Low, High = z.High, Label = z.Label, Mode = z.Mode, Grade = z.Grade, Bias = z.Bias, Gex = z.Gex, Dex = z.Dex, Vanna = z.Vanna, Charm = z.Charm, Iv = z.Iv, Flow = z.Flow, Big = z.Big, Amount = z.Amount, HasAmount = z.HasAmount, Score = z.Score, Direction = z.Direction, Active = z.Active, Near = z.Near, ProbReaction = z.ProbReaction, ProbRupture = z.ProbRupture }; }
        private BigFlowEvent CloneTape(BigFlowEvent e) { return new BigFlowEvent { Time = e.Time, Price = e.Price, Label = e.Label, Category = e.Category, Impact = e.Impact, Direction = e.Direction, Amount = e.Amount, HasAmount = e.HasAmount, Mode = e.Mode }; }
        #endregion

        #region Properties
        [NinjaScriptProperty]
        [Display(Name = "Activar API GEXBot", GroupName = "01. GEXBot", Order = 0)]
        public bool EnableGexBotApi { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Clave API", GroupName = "01. GEXBot", Order = 1)]
        public string ApiKey { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Base URL", GroupName = "01. GEXBot", Order = 2)]
        public string BaseUrl { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Símbolo GEXBot", GroupName = "01. GEXBot", Order = 3)]
        public string GexSymbol { get; set; }

        [NinjaScriptProperty]
        [Range(5, 600)]
        [Display(Name = "Actualizar cada segundos", GroupName = "01. GEXBot", Order = 4)]
        public int RefreshSeconds { get; set; }

        [NinjaScriptProperty]
        [Range(1000, 15000)]
        [Display(Name = "Timeout request ms", GroupName = "01. GEXBot", Order = 5)]
        public int RequestTimeoutMs { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Leer State GEX", GroupName = "02. Data GEXBot", Order = 0)]
        public bool EnableStateGex { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Categorías GEX", GroupName = "02. Data GEXBot", Order = 1)]
        public string GexCategories { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Leer Greeks State", GroupName = "02. Data GEXBot", Order = 2)]
        public bool EnableStateGreeks { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Categorías Greeks", GroupName = "02. Data GEXBot", Order = 3)]
        public string GreekCategories { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Leer Orderflow GEXBot", GroupName = "02. Data GEXBot", Order = 4)]
        public bool EnableOrderflow { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Endpoint Orderflow personalizado", GroupName = "02. Data GEXBot", Order = 5)]
        public string CustomOrderflowEndpoint { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Leer MaxChange / Big Flow", GroupName = "02. Data GEXBot", Order = 6)]
        public bool EnableMaxChange { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Categorías MaxChange", GroupName = "02. Data GEXBot", Order = 7)]
        public string MaxChangeCategories { get; set; }

        [NinjaScriptProperty]
        [Range(1, 30)]
        [Display(Name = "Máx categorías por ciclo", GroupName = "02. Data GEXBot", Order = 8)]
        public int MaxCategoriesPerCycle { get; set; }

        [NinjaScriptProperty]
        [Range(100, 10000)]
        [Display(Name = "Filtro puntos alrededor del precio", GroupName = "03. Motor Zonas", Order = 0)]
        public double PriceFilterPoints { get; set; }

        [NinjaScriptProperty]
        [Range(1, 200)]
        [Display(Name = "Unir zonas ticks", GroupName = "03. Motor Zonas", Order = 1)]
        public int MergeZoneTicks { get; set; }

        [NinjaScriptProperty]
        [Range(1, 200)]
        [Display(Name = "Zona activa ticks", GroupName = "03. Motor Zonas", Order = 2)]
        public int ActiveZoneTicks { get; set; }

        [NinjaScriptProperty]
        [Range(1, 500)]
        [Display(Name = "Zona cercana ticks", GroupName = "03. Motor Zonas", Order = 3)]
        public int NearZoneTicks { get; set; }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Score mínimo zona", GroupName = "03. Motor Zonas", Order = 4)]
        public double MinimumZoneScore { get; set; }

        [NinjaScriptProperty]
        [Range(3, 30)]
        [Display(Name = "Máx zonas visibles", GroupName = "03. Motor Zonas", Order = 5)]
        public int MaxVisibleZones { get; set; }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Umbral Big Flow", GroupName = "03. Motor Zonas", Order = 6)]
        public double BigFlowImpactThreshold { get; set; }

        [NinjaScriptProperty]
        [Range(1000, 100000000)]
        [Display(Name = "Normalizador Flow", GroupName = "03. Motor Zonas", Order = 7)]
        public double FlowNormalizer { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mostrar HMI", GroupName = "04. Visual", Order = 0)]
        public bool ShowHmiPanel { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mostrar Options DOM", GroupName = "04. Visual", Order = 1)]
        public bool ShowOptionsDom { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mostrar Big Flow Tape", GroupName = "04. Visual", Order = 2)]
        public bool ShowBigFlowTape { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mostrar burbujas Big Flow", GroupName = "04. Visual", Order = 3)]
        public bool ShowBigFlowBubbles { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mostrar bandas de zonas", GroupName = "04. Visual", Order = 4)]
        public bool ShowPriceZoneBands { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Anclaje panel", GroupName = "04. Visual", Order = 5)]
        public string PanelAnchor { get; set; }

        [NinjaScriptProperty]
        [Range(420, 1400)]
        [Display(Name = "Ancho panel", GroupName = "04. Visual", Order = 6)]
        public int PanelWidth { get; set; }

        [NinjaScriptProperty]
        [Range(360, 1200)]
        [Display(Name = "Alto panel", GroupName = "04. Visual", Order = 7)]
        public int PanelHeight { get; set; }

        [NinjaScriptProperty]
        [Range(0, 400)]
        [Display(Name = "Offset X panel", GroupName = "04. Visual", Order = 8)]
        public int PanelXOffset { get; set; }

        [NinjaScriptProperty]
        [Range(0, 400)]
        [Display(Name = "Offset Y panel", GroupName = "04. Visual", Order = 9)]
        public int PanelYOffset { get; set; }

        [NinjaScriptProperty]
        [Range(30, 100)]
        [Display(Name = "Opacidad panel", GroupName = "04. Visual", Order = 10)]
        public int PanelOpacity { get; set; }

        [NinjaScriptProperty]
        [Range(260, 1600)]
        [Display(Name = "Ancho Options DOM", GroupName = "04. Visual", Order = 11)]
        public int LadderWidth { get; set; }

        [NinjaScriptProperty]
        [Range(0, 400)]
        [Display(Name = "Offset X Options DOM", GroupName = "04. Visual", Order = 12)]
        public int LadderXOffset { get; set; }

        [NinjaScriptProperty]
        [Range(8, 26)]
        [Display(Name = "Tamaño fuente", GroupName = "04. Visual", Order = 13)]
        public int FontSize { get; set; }

        [NinjaScriptProperty]
        [Range(20, 140)]
        [Display(Name = "Alto filas", GroupName = "04. Visual", Order = 14)]
        public int RowHeight { get; set; }

        [NinjaScriptProperty]
        [Range(1, 20)]
        [Display(Name = "Filas Big Flow Tape", GroupName = "04. Visual", Order = 15)]
        public int BigFlowTapeRows { get; set; }
        #endregion
    }
}


#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private ILT_DEV_SUITE.ILT_DEV_GAMMATRIX[] cacheILT_DEV_GAMMATRIX;
		public ILT_DEV_SUITE.ILT_DEV_GAMMATRIX ILT_DEV_GAMMATRIX(bool enableGexBotApi, string apiKey, string baseUrl, string gexSymbol, int refreshSeconds, int requestTimeoutMs, bool enableStateGex, string gexCategories, bool enableStateGreeks, string greekCategories, bool enableOrderflow, string customOrderflowEndpoint, bool enableMaxChange, string maxChangeCategories, int maxCategoriesPerCycle, double priceFilterPoints, int mergeZoneTicks, int activeZoneTicks, int nearZoneTicks, double minimumZoneScore, int maxVisibleZones, double bigFlowImpactThreshold, double flowNormalizer, bool showHmiPanel, bool showOptionsDom, bool showBigFlowTape, bool showBigFlowBubbles, bool showPriceZoneBands, string panelAnchor, int panelWidth, int panelHeight, int panelXOffset, int panelYOffset, int panelOpacity, int ladderWidth, int ladderXOffset, int fontSize, int rowHeight, int bigFlowTapeRows)
		{
			return ILT_DEV_GAMMATRIX(Input, enableGexBotApi, apiKey, baseUrl, gexSymbol, refreshSeconds, requestTimeoutMs, enableStateGex, gexCategories, enableStateGreeks, greekCategories, enableOrderflow, customOrderflowEndpoint, enableMaxChange, maxChangeCategories, maxCategoriesPerCycle, priceFilterPoints, mergeZoneTicks, activeZoneTicks, nearZoneTicks, minimumZoneScore, maxVisibleZones, bigFlowImpactThreshold, flowNormalizer, showHmiPanel, showOptionsDom, showBigFlowTape, showBigFlowBubbles, showPriceZoneBands, panelAnchor, panelWidth, panelHeight, panelXOffset, panelYOffset, panelOpacity, ladderWidth, ladderXOffset, fontSize, rowHeight, bigFlowTapeRows);
		}

		public ILT_DEV_SUITE.ILT_DEV_GAMMATRIX ILT_DEV_GAMMATRIX(ISeries<double> input, bool enableGexBotApi, string apiKey, string baseUrl, string gexSymbol, int refreshSeconds, int requestTimeoutMs, bool enableStateGex, string gexCategories, bool enableStateGreeks, string greekCategories, bool enableOrderflow, string customOrderflowEndpoint, bool enableMaxChange, string maxChangeCategories, int maxCategoriesPerCycle, double priceFilterPoints, int mergeZoneTicks, int activeZoneTicks, int nearZoneTicks, double minimumZoneScore, int maxVisibleZones, double bigFlowImpactThreshold, double flowNormalizer, bool showHmiPanel, bool showOptionsDom, bool showBigFlowTape, bool showBigFlowBubbles, bool showPriceZoneBands, string panelAnchor, int panelWidth, int panelHeight, int panelXOffset, int panelYOffset, int panelOpacity, int ladderWidth, int ladderXOffset, int fontSize, int rowHeight, int bigFlowTapeRows)
		{
			if (cacheILT_DEV_GAMMATRIX != null)
				for (int idx = 0; idx < cacheILT_DEV_GAMMATRIX.Length; idx++)
					if (cacheILT_DEV_GAMMATRIX[idx] != null && cacheILT_DEV_GAMMATRIX[idx].EnableGexBotApi == enableGexBotApi && cacheILT_DEV_GAMMATRIX[idx].ApiKey == apiKey && cacheILT_DEV_GAMMATRIX[idx].BaseUrl == baseUrl && cacheILT_DEV_GAMMATRIX[idx].GexSymbol == gexSymbol && cacheILT_DEV_GAMMATRIX[idx].RefreshSeconds == refreshSeconds && cacheILT_DEV_GAMMATRIX[idx].RequestTimeoutMs == requestTimeoutMs && cacheILT_DEV_GAMMATRIX[idx].EnableStateGex == enableStateGex && cacheILT_DEV_GAMMATRIX[idx].GexCategories == gexCategories && cacheILT_DEV_GAMMATRIX[idx].EnableStateGreeks == enableStateGreeks && cacheILT_DEV_GAMMATRIX[idx].GreekCategories == greekCategories && cacheILT_DEV_GAMMATRIX[idx].EnableOrderflow == enableOrderflow && cacheILT_DEV_GAMMATRIX[idx].CustomOrderflowEndpoint == customOrderflowEndpoint && cacheILT_DEV_GAMMATRIX[idx].EnableMaxChange == enableMaxChange && cacheILT_DEV_GAMMATRIX[idx].MaxChangeCategories == maxChangeCategories && cacheILT_DEV_GAMMATRIX[idx].MaxCategoriesPerCycle == maxCategoriesPerCycle && cacheILT_DEV_GAMMATRIX[idx].PriceFilterPoints == priceFilterPoints && cacheILT_DEV_GAMMATRIX[idx].MergeZoneTicks == mergeZoneTicks && cacheILT_DEV_GAMMATRIX[idx].ActiveZoneTicks == activeZoneTicks && cacheILT_DEV_GAMMATRIX[idx].NearZoneTicks == nearZoneTicks && cacheILT_DEV_GAMMATRIX[idx].MinimumZoneScore == minimumZoneScore && cacheILT_DEV_GAMMATRIX[idx].MaxVisibleZones == maxVisibleZones && cacheILT_DEV_GAMMATRIX[idx].BigFlowImpactThreshold == bigFlowImpactThreshold && cacheILT_DEV_GAMMATRIX[idx].FlowNormalizer == flowNormalizer && cacheILT_DEV_GAMMATRIX[idx].ShowHmiPanel == showHmiPanel && cacheILT_DEV_GAMMATRIX[idx].ShowOptionsDom == showOptionsDom && cacheILT_DEV_GAMMATRIX[idx].ShowBigFlowTape == showBigFlowTape && cacheILT_DEV_GAMMATRIX[idx].ShowBigFlowBubbles == showBigFlowBubbles && cacheILT_DEV_GAMMATRIX[idx].ShowPriceZoneBands == showPriceZoneBands && cacheILT_DEV_GAMMATRIX[idx].PanelAnchor == panelAnchor && cacheILT_DEV_GAMMATRIX[idx].PanelWidth == panelWidth && cacheILT_DEV_GAMMATRIX[idx].PanelHeight == panelHeight && cacheILT_DEV_GAMMATRIX[idx].PanelXOffset == panelXOffset && cacheILT_DEV_GAMMATRIX[idx].PanelYOffset == panelYOffset && cacheILT_DEV_GAMMATRIX[idx].PanelOpacity == panelOpacity && cacheILT_DEV_GAMMATRIX[idx].LadderWidth == ladderWidth && cacheILT_DEV_GAMMATRIX[idx].LadderXOffset == ladderXOffset && cacheILT_DEV_GAMMATRIX[idx].FontSize == fontSize && cacheILT_DEV_GAMMATRIX[idx].RowHeight == rowHeight && cacheILT_DEV_GAMMATRIX[idx].BigFlowTapeRows == bigFlowTapeRows && cacheILT_DEV_GAMMATRIX[idx].EqualsInput(input))
						return cacheILT_DEV_GAMMATRIX[idx];
			return CacheIndicator<ILT_DEV_SUITE.ILT_DEV_GAMMATRIX>(new ILT_DEV_SUITE.ILT_DEV_GAMMATRIX(){ EnableGexBotApi = enableGexBotApi, ApiKey = apiKey, BaseUrl = baseUrl, GexSymbol = gexSymbol, RefreshSeconds = refreshSeconds, RequestTimeoutMs = requestTimeoutMs, EnableStateGex = enableStateGex, GexCategories = gexCategories, EnableStateGreeks = enableStateGreeks, GreekCategories = greekCategories, EnableOrderflow = enableOrderflow, CustomOrderflowEndpoint = customOrderflowEndpoint, EnableMaxChange = enableMaxChange, MaxChangeCategories = maxChangeCategories, MaxCategoriesPerCycle = maxCategoriesPerCycle, PriceFilterPoints = priceFilterPoints, MergeZoneTicks = mergeZoneTicks, ActiveZoneTicks = activeZoneTicks, NearZoneTicks = nearZoneTicks, MinimumZoneScore = minimumZoneScore, MaxVisibleZones = maxVisibleZones, BigFlowImpactThreshold = bigFlowImpactThreshold, FlowNormalizer = flowNormalizer, ShowHmiPanel = showHmiPanel, ShowOptionsDom = showOptionsDom, ShowBigFlowTape = showBigFlowTape, ShowBigFlowBubbles = showBigFlowBubbles, ShowPriceZoneBands = showPriceZoneBands, PanelAnchor = panelAnchor, PanelWidth = panelWidth, PanelHeight = panelHeight, PanelXOffset = panelXOffset, PanelYOffset = panelYOffset, PanelOpacity = panelOpacity, LadderWidth = ladderWidth, LadderXOffset = ladderXOffset, FontSize = fontSize, RowHeight = rowHeight, BigFlowTapeRows = bigFlowTapeRows }, input, ref cacheILT_DEV_GAMMATRIX);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.ILT_DEV_SUITE.ILT_DEV_GAMMATRIX ILT_DEV_GAMMATRIX(bool enableGexBotApi, string apiKey, string baseUrl, string gexSymbol, int refreshSeconds, int requestTimeoutMs, bool enableStateGex, string gexCategories, bool enableStateGreeks, string greekCategories, bool enableOrderflow, string customOrderflowEndpoint, bool enableMaxChange, string maxChangeCategories, int maxCategoriesPerCycle, double priceFilterPoints, int mergeZoneTicks, int activeZoneTicks, int nearZoneTicks, double minimumZoneScore, int maxVisibleZones, double bigFlowImpactThreshold, double flowNormalizer, bool showHmiPanel, bool showOptionsDom, bool showBigFlowTape, bool showBigFlowBubbles, bool showPriceZoneBands, string panelAnchor, int panelWidth, int panelHeight, int panelXOffset, int panelYOffset, int panelOpacity, int ladderWidth, int ladderXOffset, int fontSize, int rowHeight, int bigFlowTapeRows)
		{
			return indicator.ILT_DEV_GAMMATRIX(Input, enableGexBotApi, apiKey, baseUrl, gexSymbol, refreshSeconds, requestTimeoutMs, enableStateGex, gexCategories, enableStateGreeks, greekCategories, enableOrderflow, customOrderflowEndpoint, enableMaxChange, maxChangeCategories, maxCategoriesPerCycle, priceFilterPoints, mergeZoneTicks, activeZoneTicks, nearZoneTicks, minimumZoneScore, maxVisibleZones, bigFlowImpactThreshold, flowNormalizer, showHmiPanel, showOptionsDom, showBigFlowTape, showBigFlowBubbles, showPriceZoneBands, panelAnchor, panelWidth, panelHeight, panelXOffset, panelYOffset, panelOpacity, ladderWidth, ladderXOffset, fontSize, rowHeight, bigFlowTapeRows);
		}

		public Indicators.ILT_DEV_SUITE.ILT_DEV_GAMMATRIX ILT_DEV_GAMMATRIX(ISeries<double> input , bool enableGexBotApi, string apiKey, string baseUrl, string gexSymbol, int refreshSeconds, int requestTimeoutMs, bool enableStateGex, string gexCategories, bool enableStateGreeks, string greekCategories, bool enableOrderflow, string customOrderflowEndpoint, bool enableMaxChange, string maxChangeCategories, int maxCategoriesPerCycle, double priceFilterPoints, int mergeZoneTicks, int activeZoneTicks, int nearZoneTicks, double minimumZoneScore, int maxVisibleZones, double bigFlowImpactThreshold, double flowNormalizer, bool showHmiPanel, bool showOptionsDom, bool showBigFlowTape, bool showBigFlowBubbles, bool showPriceZoneBands, string panelAnchor, int panelWidth, int panelHeight, int panelXOffset, int panelYOffset, int panelOpacity, int ladderWidth, int ladderXOffset, int fontSize, int rowHeight, int bigFlowTapeRows)
		{
			return indicator.ILT_DEV_GAMMATRIX(input, enableGexBotApi, apiKey, baseUrl, gexSymbol, refreshSeconds, requestTimeoutMs, enableStateGex, gexCategories, enableStateGreeks, greekCategories, enableOrderflow, customOrderflowEndpoint, enableMaxChange, maxChangeCategories, maxCategoriesPerCycle, priceFilterPoints, mergeZoneTicks, activeZoneTicks, nearZoneTicks, minimumZoneScore, maxVisibleZones, bigFlowImpactThreshold, flowNormalizer, showHmiPanel, showOptionsDom, showBigFlowTape, showBigFlowBubbles, showPriceZoneBands, panelAnchor, panelWidth, panelHeight, panelXOffset, panelYOffset, panelOpacity, ladderWidth, ladderXOffset, fontSize, rowHeight, bigFlowTapeRows);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.ILT_DEV_SUITE.ILT_DEV_GAMMATRIX ILT_DEV_GAMMATRIX(bool enableGexBotApi, string apiKey, string baseUrl, string gexSymbol, int refreshSeconds, int requestTimeoutMs, bool enableStateGex, string gexCategories, bool enableStateGreeks, string greekCategories, bool enableOrderflow, string customOrderflowEndpoint, bool enableMaxChange, string maxChangeCategories, int maxCategoriesPerCycle, double priceFilterPoints, int mergeZoneTicks, int activeZoneTicks, int nearZoneTicks, double minimumZoneScore, int maxVisibleZones, double bigFlowImpactThreshold, double flowNormalizer, bool showHmiPanel, bool showOptionsDom, bool showBigFlowTape, bool showBigFlowBubbles, bool showPriceZoneBands, string panelAnchor, int panelWidth, int panelHeight, int panelXOffset, int panelYOffset, int panelOpacity, int ladderWidth, int ladderXOffset, int fontSize, int rowHeight, int bigFlowTapeRows)
		{
			return indicator.ILT_DEV_GAMMATRIX(Input, enableGexBotApi, apiKey, baseUrl, gexSymbol, refreshSeconds, requestTimeoutMs, enableStateGex, gexCategories, enableStateGreeks, greekCategories, enableOrderflow, customOrderflowEndpoint, enableMaxChange, maxChangeCategories, maxCategoriesPerCycle, priceFilterPoints, mergeZoneTicks, activeZoneTicks, nearZoneTicks, minimumZoneScore, maxVisibleZones, bigFlowImpactThreshold, flowNormalizer, showHmiPanel, showOptionsDom, showBigFlowTape, showBigFlowBubbles, showPriceZoneBands, panelAnchor, panelWidth, panelHeight, panelXOffset, panelYOffset, panelOpacity, ladderWidth, ladderXOffset, fontSize, rowHeight, bigFlowTapeRows);
		}

		public Indicators.ILT_DEV_SUITE.ILT_DEV_GAMMATRIX ILT_DEV_GAMMATRIX(ISeries<double> input , bool enableGexBotApi, string apiKey, string baseUrl, string gexSymbol, int refreshSeconds, int requestTimeoutMs, bool enableStateGex, string gexCategories, bool enableStateGreeks, string greekCategories, bool enableOrderflow, string customOrderflowEndpoint, bool enableMaxChange, string maxChangeCategories, int maxCategoriesPerCycle, double priceFilterPoints, int mergeZoneTicks, int activeZoneTicks, int nearZoneTicks, double minimumZoneScore, int maxVisibleZones, double bigFlowImpactThreshold, double flowNormalizer, bool showHmiPanel, bool showOptionsDom, bool showBigFlowTape, bool showBigFlowBubbles, bool showPriceZoneBands, string panelAnchor, int panelWidth, int panelHeight, int panelXOffset, int panelYOffset, int panelOpacity, int ladderWidth, int ladderXOffset, int fontSize, int rowHeight, int bigFlowTapeRows)
		{
			return indicator.ILT_DEV_GAMMATRIX(input, enableGexBotApi, apiKey, baseUrl, gexSymbol, refreshSeconds, requestTimeoutMs, enableStateGex, gexCategories, enableStateGreeks, greekCategories, enableOrderflow, customOrderflowEndpoint, enableMaxChange, maxChangeCategories, maxCategoriesPerCycle, priceFilterPoints, mergeZoneTicks, activeZoneTicks, nearZoneTicks, minimumZoneScore, maxVisibleZones, bigFlowImpactThreshold, flowNormalizer, showHmiPanel, showOptionsDom, showBigFlowTape, showBigFlowBubbles, showPriceZoneBands, panelAnchor, panelWidth, panelHeight, panelXOffset, panelYOffset, panelOpacity, ladderWidth, ladderXOffset, fontSize, rowHeight, bigFlowTapeRows);
		}
	}
}

#endregion

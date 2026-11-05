using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using Why.Economy.Emphasis;
using Why.Economy.Land;

namespace Why.Economy.Layers
{
    /// <summary>
    /// The government's two levers on the market layer beside the bedrock (view "money"; Data/economy/money-and-trade.json,
    /// from FRED / BEA / Freddie Mac via Tools/history): a chart standing beside the wave on its own time axis, so a year
    /// on it stands beside the same year on the wave and the road.
    /// <list type="bullet">
    /// <item><b>The price of money.</b> The federal funds rate the central bank sets (gold), the 30-year mortgage rate
    /// households pay (rose) and the 10-year Treasury yield the state pays its lenders (ice), one height per percent: the
    /// cost of every river of debt on the hill.</item>
    /// <item><b>The edge of trade.</b> The effective tariff rate (customs duties over goods imports, steel) on the same
    /// scale: low through the free-trade decades, rising in 2018-2019 and in 2025; the economy's openness (exports plus
    /// imports over GDP) in a strip of its own beneath, on its own scale.</item>
    /// </list>
    /// Marked moments are read off the data (the highest rate, the years at zero, the tariff jump and the rounds that held),
    /// not authored. A dark panel stands just behind the chart, so the wave behind it reads as faint ground (seen from the
    /// chart's side: turned round to the hills' side, the panel fades out).
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillMoneyLayer : GraphLayer
    {
        public override int Order => 64;
        public const string Path = "Data/economy/money-and-trade";
        public override IEnumerable<string> RequiredTexts => new[] { Path };
        /// <summary>Where the chart stands (land-local x: beyond the far side of the widest wave, 2025's), a percent's
        /// height and the chart's floor (high enough that the trade strip under it stays in the view's frame).</summary>
        public const float ChartX = 290, PerPercent = 2.1f, Floor = 8;
        /// <summary>The trade strip under the rates: its top (the strip's 35%), a percent of GDP's height, its range.</summary>
        const float StripTop = Floor - 7.5f, PerShare = .28f, StripMax = 35, StripBottom = StripTop - StripMax * PerShare;
        /// <summary>The series take the hues the view's key names them by (the bank's rate, the mortgage, the state's
        /// borrowing, the tariff); trade, a share rather than a rate, a warm grey of its own.</summary>
        static readonly Color Gold = EconomyStyle.Capital, Rose = EconomyStyle.Desire, Ice = EconomyStyle.Fear, Steel = EconomyStyle.Government,
            White = new Color(1, .97f, .92f), Sand = new Color(.80f, .77f, .68f);
        readonly List<TextMeshPro> labels = new List<TextMeshPro>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        string json; LandSnapshot snapshot; HillLandscape land; GameObject content;
        /// <summary>The labels' material: drawn after the dark panel (text's own queue comes before every line and surface).</summary>
        Material labelMaterial;
        /// <summary>The dark panel behind the chart and its extent (land-local: its plane's x, its bottom and top, its first and
        /// last z), for fading it by the side it is seen from.</summary>
        MeshRenderer backing; float backingX; Vector2 backingY, backingZ;
        /// <summary>The chart's lines (its rules, series, moments and the trade strip's top) and the trade strip's fill, and how
        /// much of the panel shows from where the camera stands (Tick).</summary>
        MeshRenderer chartLines, chartFill; float side = 1;
        /// <summary>The emphasis keys of the chart's parts (EmphasisTable: full in this view, absent elsewhere), whose highlight
        /// ids their meshes carry.</summary>
        static class Part
        {
            public static readonly int Chart = EconomyEmphasis.Key("money.chart"), Fill = EconomyEmphasis.Key("money.fill"), Backing = EconomyEmphasis.Key("money.backing");
        }

        public override void Prepare(GraphContext ctx) { json = ctx.Text(Path); }
        public override void Upload(GraphContext ctx)
        {
            EconomyStage.Land().Place(transform);
            // the panel fades as the camera turns edge-on to it (Tick)
            EconomyEmphasis.Multiplier("money.backing", "seen from its side", () => side);
        }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (HillLandscapeLayer.Current == null || LandService.Current == null) return;
            if (HillLandscapeLayer.Current.Source != LandService.Current) return;
            if (snapshot != LandService.Current || land != HillLandscapeLayer.Current)
            {
                // on its own view the chart is rebuilt at once, so its last year and the subtitle's (LandService.ShownYear)
                // change together; elsewhere it waits its turn
                if (LandView.PresetId != "money" && !HillRebuilds.Take()) return;
                Build();
            }
            if (content == null) return;
            bool on = LandView.PresetId == "money";
            content.SetActive(on); if (!on) return;
            foreach (var t in labels) t.transform.rotation = rig.Cam.transform.rotation;
            // The panel stands in front of the wave only from the chart's own side (+x). Orbited round to the hills' side, the
            // wave is in front of it, and a transparent surface is drawn in its queue's order, not by depth: it would darken
            // the hills. So it fades out as the camera turns edge-on to it and stays hidden beyond. The angle is taken from the
            // panel's nearest point, not its middle: zoomed in close to one end of the chart, the camera is still in front of it.
            if (backing)
            {
                Vector3 eye = transform.InverseTransformPoint(rig.Cam.transform.position);
                Vector3 off = eye - new Vector3(backingX, Mathf.Clamp(eye.y, backingY.x, backingY.y), Mathf.Clamp(eye.z, backingZ.x, backingZ.y));
                side = Mathf.Clamp01(4 * off.x / Mathf.Max(1e-3f, off.magnitude));
            }
            EconomyEmphasis.Apply(chartLines, Part.Chart); EconomyEmphasis.Apply(chartFill, Part.Fill); EconomyEmphasis.Apply(backing, Part.Backing);
        }

        static List<(int year, float value)> Series(JObject root, string key)
        {
            var list = new List<(int, float)>();
            if (root[key] is JArray a) foreach (var p in a) if (p is JArray pair && pair.Count >= 2 && pair[1].Type != JTokenType.Null) list.Add(((int)pair[0], (float)pair[1]));
            return list;
        }

        void Build()
        {
            Clear(); snapshot = LandService.Current; land = HillLandscapeLayer.Current;
            if (string.IsNullOrEmpty(json)) return;
            JObject root; try { root = JObject.Parse(json); } catch { return; }
            content = new GameObject("Money and trade"); content.transform.SetParent(transform, false);
            float x = Mathf.Max(ChartX, land.XMax + 10);
            int last = land.Year;
            var b = new LineMeshBuilder(); var fill = new SurfaceMeshBuilder();
            float Y(float percent) => Floor + percent * PerPercent;
            // the scale: a rule every 5 percent along the whole wake, and the years every decade
            float z0 = HillLandscape.Z(1947), z1 = HillLandscape.Z(last);
            for (int pct = 0; pct <= 20; pct += 5)
            {
                b.AddSegment(new Vector3(x, Y(pct), z0), new Vector3(x, Y(pct), z1), WithAlpha(Steel, pct == 0 ? .5f : .18f), pct == 0 ? 1.4f : .8f, 0, EconomyEmphasis.Id(Part.Chart), 1);
                Label(pct + "%", new Vector3(x, Y(pct), z0 - 4), .45f, WithAlpha(Steel, .8f));
            }
            for (int y = 1950; y <= last; y += 10) Label(y.ToString(LandFacts.Ci), new Vector3(x, Floor - 2.6f, HillLandscape.Z(y)), .48f, Ice);
            Trade(b, fill, root, x, z0, z1, last);
            Curve(b, root, "fedFunds", x, Y, Gold, 3.2f, last, "the federal funds rate (the central bank's lever)");
            Curve(b, root, "mortgage30", x, Y, Rose, 2.2f, last, "30-year mortgage (what households pay)");
            Curve(b, root, "treasury10", x, Y, Ice, 2.2f, last, "10-year Treasury (what the state pays its lenders)");
            Curve(b, root, "tariffRate", x, Y, Steel, 3, last, "tariffs: customs duties / goods imports");
            Moments(b, root, x, Y, last);
            Label(string.Join("\n", legend), new Vector3(x, Y(19), z0 + 4), .5f, White, TextAlignmentOptions.Left);
            legend.Clear();
            // the second line ends short of the tour card the money stop docks at the right top (about 1345 px across at 1080p)
            Label("<b>THE PRICE OF MONEY, AND TRADE</b>\nthe state's two levers on the market: what borrowing costs, and the tax on imported goods",
                new Vector3(x, Y(23), (z0 + z1) * .5f), .7f, White);
            // The dark panel just behind the chart (the camera looks at it from beyond, +x): the wave behind reads as faint
            // ground, and no crest, halo or lifeline stands among the series.
            Backing(x - .8f, z0 - 10, HillLandscape.Z(last + .5f) + 26, StripBottom - 3, Y(23) + 5);
            Surface(fill); Lines(b);
        }

        /// <summary>The economy's openness (exports plus imports over GDP) in its own strip under the rates, on its own scale:
        /// on the rates' scale its 25% of GDP would stand on their 5% rule.</summary>
        void Trade(LineMeshBuilder b, SurfaceMeshBuilder fill, JObject root, float x, float z0, float z1, int last)
        {
            var trade = Series(root, "tradeShare");
            if (trade.Count < 2) return;
            float S(float share) => StripBottom + share * PerShare;
            for (int pct = 0; pct <= 30; pct += 10)
            {
                b.AddSegment(new Vector3(x, S(pct), z0), new Vector3(x, S(pct), z1), WithAlpha(Sand, pct == 0 ? .45f : .15f), pct == 0 ? 1.2f : .8f, 0, EconomyEmphasis.Id(Part.Chart), 1);
                if (pct > 0) Label(pct == 30 ? "30% of GDP" : pct + "%", new Vector3(x, S(pct), z0 - 4), .42f, WithAlpha(Sand, .85f));
            }
            var low = new List<Vector3>(); var high = new List<Vector3>(); var top = new List<LinePoint>();
            foreach (var (year, v) in trade)
            {
                if (year > last) continue;
                float z = HillLandscape.Z(year + .5f);
                low.Add(new Vector3(x + .05f, S(0), z)); high.Add(new Vector3(x + .05f, S(v), z)); top.Add(new LinePoint(new Vector3(x, S(v), z), WithAlpha(Sand, .9f), 1.8f, 0, 1));
            }
            fill.AddBand(low, high, new[] { (Color32)WithAlpha(Sand, .22f) }, EconomyEmphasis.Id(Part.Fill), 1);
            b.AddPolyline(top, EconomyEmphasis.Id(Part.Chart));
            // the latest year the strip reaches (the crest's, when the wave stands back in time or replays), named between
            // its upper rules over the 1960s, when trade was a tenth of GDP and the strip is empty above it
            var (ty, tv) = trade[0];
            foreach (var p in trade) if (p.year <= last) (ty, tv) = p;
            Label("OPEN TO TRADE · exports + imports: " + tv.ToString("0", LandFacts.Ci) + "% of GDP in " + ty + " (" + trade[0].value.ToString("0", LandFacts.Ci) + "% in " + trade[0].year + ")",
                new Vector3(x, S(24.5f), HillLandscape.Z(Mathf.Min(1968, ty))), .42f, WithAlpha(Sand, .95f));
        }

        /// <summary>A series along time on the chart, labeled at its end with its latest value.</summary>
        void Curve(LineMeshBuilder b, JObject root, string key, float x, Func<float, float> Y, Color color, float width, int last, string name)
        {
            var s = Series(root, key); if (s.Count < 2) return;
            var pts = new List<LinePoint>();
            foreach (var (year, v) in s) { if (year > last) continue; pts.Add(new LinePoint(new Vector3(x, Y(v), HillLandscape.Z(year + .5f)), WithAlpha(color, .9f), width, 0, 1)); }
            if (pts.Count < 2) return;
            b.AddPolyline(pts, EconomyEmphasis.Id(Part.Chart));
            int endYear = Math.Min(last, s[s.Count - 1].year); float endValue = 0;
            foreach (var (year, v) in s) if (year == endYear) endValue = v;
            // "•", not "●": the labels' font has the one and not the other (a fallback glyph would draw under the panel)
            legend.Add("<color=#" + ColorUtility.ToHtmlStringRGB(Color.Lerp(color, White, .2f)) + "><size=150%>•</size> " + name + "</color> · " + endValue.ToString("0.0", LandFacts.Ci) + "% in " + endYear);
        }
        readonly List<string> legend = new List<string>();

        /// <summary>Moments read off the data: the highest federal funds rate, the years it stood at zero, the tariff rate's
        /// largest one-year rise and the rounds of rises that held.</summary>
        void Moments(LineMeshBuilder b, JObject root, float x, Func<float, float> Y, int last)
        {
            var funds = Series(root, "fedFunds"); var tariffs = Series(root, "tariffRate");
            if (funds.Count > 0)
            {
                var peak = funds[0]; foreach (var p in funds) if (p.year <= last && p.value > peak.value) peak = p;
                Label("peak " + peak.value.ToString("0.0", LandFacts.Ci) + "% in " + peak.year + ": money at its dearest", new Vector3(x, Y(peak.value) + 2f, HillLandscape.Z(peak.year)), .5f, Color.Lerp(Gold, White, .4f));
                int zeroFrom = -1, zeroTo = -1, bestFrom = -1, bestTo = -1;
                foreach (var p in funds)
                {
                    if (p.year > last) break;
                    if (p.value < .5f) { if (zeroFrom < 0) zeroFrom = p.year; zeroTo = p.year; if (zeroTo - zeroFrom > bestTo - bestFrom) { bestFrom = zeroFrom; bestTo = zeroTo; } }
                    else zeroFrom = -1;
                }
                if (bestFrom > 0)
                    Label("near zero " + bestFrom + "-" + bestTo + ": money almost free, asset prices lifted", new Vector3(x, Y(0) - 4.5f, HillLandscape.Z((bestFrom + bestTo) * .5f)), .45f, Color.Lerp(Gold, White, .4f));
            }
            if (tariffs.Count > 1)
            {
                Color tag = Color.Lerp(Steel, White, .4f);
                int jump = -1; float rise = 0;
                for (int i = 1; i < tariffs.Count; i++) { if (tariffs[i].year > last) break; float d = tariffs[i].value - tariffs[i - 1].value; if (d > rise) { rise = d; jump = i; } }
                if (jump > 0)
                {
                    string text = "tariffs " + tariffs[jump - 1].value.ToString("0.0", LandFacts.Ci) + "% to " + tariffs[jump].value.ToString("0.0", LandFacts.Ci) + "% in " + tariffs[jump].year;
                    Vector3 at = new Vector3(x, Y(tariffs[jump].value), HillLandscape.Z(tariffs[jump].year + .5f));
                    // at the chart's end the label starts over the point and runs out past it, clear of the series
                    if (tariffs[jump].year >= last) { Vector3 to = at + Vector3.up * 3.2f; b.AddSegment(at, to, WithAlpha(Steel, .6f), 1, 0, EconomyEmphasis.Id(Part.Chart), 1); Label(text, to + new Vector3(0, .5f, -.4f), .5f, tag, TextAlignmentOptions.Left); }
                    else Label(text, at + new Vector3(0, 6, 0), .5f, tag);
                }
                // Rounds that held: a run of yearly rises that lifted the rate by half and kept most of it the next year
                // (2018-2019). A one-year spike that fell straight back (1975) is not one; the largest rise has its own label.
                for (int i = 1; i < tariffs.Count && tariffs[i].year <= last;)
                {
                    if (tariffs[i].value <= tariffs[i - 1].value) { i++; continue; }
                    int from = i - 1; while (i < tariffs.Count && tariffs[i].year <= last && tariffs[i].value > tariffs[i - 1].value) i++;
                    int to = i - 1;
                    bool held = i < tariffs.Count && tariffs[i].year <= last && tariffs[i].value >= .8f * tariffs[to].value;
                    if (!held || tariffs[to].value < 1.5f * tariffs[from].value || jump > from && jump <= to) continue;
                    // above every series in the years the label spans
                    float high = 0;
                    foreach (string key in new[] { "fedFunds", "mortgage30", "treasury10", "tariffRate" })
                        foreach (var (year, v) in Series(root, key)) if (year >= tariffs[from].year - 5 && year <= tariffs[to].year + 4 && year <= last) high = Mathf.Max(high, v);
                    Vector3 end = new Vector3(x, Y(tariffs[to].value), HillLandscape.Z(tariffs[to].year + .5f)), label = new Vector3(x, Y(high + 2), HillLandscape.Z(tariffs[from + 1].year));
                    b.AddSegment(end, label + Vector3.down * 1.1f, WithAlpha(Steel, .6f), 1, 0, EconomyEmphasis.Id(Part.Chart), 1);
                    Label("tariffs " + tariffs[from].value.ToString("0.0", LandFacts.Ci) + "% to " + tariffs[to].value.ToString("0.0", LandFacts.Ci) + "% in " + tariffs[from + 1].year + "-" + tariffs[to].year, label, .5f, tag);
                }
            }
        }

        void Lines(LineMeshBuilder b)
        {
            // blended at intensity 1: where the series cross, each keeps its hue instead of adding up to white under the bloom
            Mesh mesh = b.ToMesh("Money and trade"); owned.Add(mesh);
            Material m = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3248, false)); owned.Add(m);
            var r = AddMesh("Money and trade", mesh, m); r.transform.SetParent(content.transform, false); chartLines = r;
        }

        void Surface(SurfaceMeshBuilder b)
        {
            Mesh mesh = b.ToMesh("Money and trade fill"); owned.Add(mesh);
            Material m = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1, 3246)); owned.Add(m);
            m.SetFloat("_EdgeSoft", 0);
            var r = AddMesh("Money and trade fill", mesh, m); r.transform.SetParent(content.transform, false); chartFill = r;
        }

        /// <summary>A dark panel in the chart's plane at <paramref name="x"/>, drawn after every layer of the wave (queues up
        /// to 3242) and before the chart's own fill, lines and labels. At .92: at .75 the crest's bright marks seen edge-on
        /// behind it still added up to the series' own brightness, in their hues, and read as data inside the plot.</summary>
        void Backing(float x, float z0, float z1, float y0, float y1)
        {
            var b = new SurfaceMeshBuilder(); Color c = WithAlpha(GraphStyle.Background, .92f);
            b.AddBand(new[] { new Vector3(x, y0, z0), new Vector3(x, y0, z1) }, new[] { new Vector3(x, y1, z0), new Vector3(x, y1, z1) }, new[] { (Color32)c }, EconomyEmphasis.Id(Part.Backing), 1);
            Mesh mesh = b.ToMesh("Money and trade backing"); owned.Add(mesh);
            Material m = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1, 3244)); owned.Add(m);
            m.SetFloat("_EdgeSoft", 0);
            var r = AddMesh("Money and trade backing", mesh, m); r.transform.SetParent(content.transform, false);
            backing = r; backingX = x; backingY = new Vector2(y0, y1); backingZ = new Vector2(z0, z1);
        }

        void Label(string text, Vector3 at, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject("Money label"); go.transform.SetParent(content.transform, false); go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>(); t.text = text; t.fontSize = 24; t.alignment = align; t.color = color;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.sizeDelta = new Vector2(90, 6);
            if (align == TextAlignmentOptions.Left) t.rectTransform.pivot = new Vector2(0, .5f);
            // text keeps the font's queue (before every line and surface): the chart's own comes after its dark panel
            if (labelMaterial == null && t.fontSharedMaterial != null) { labelMaterial = new Material(t.fontSharedMaterial) { renderQueue = 3250 }; owned.Add(labelMaterial); }
            if (labelMaterial != null) t.fontSharedMaterial = labelMaterial;
            go.transform.localScale = Vector3.one * size; labels.Add(t);
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        void Clear()
        {
            foreach (var o in owned) if (o) Destroy(o);
            if (content) Destroy(content);
            owned.Clear(); labels.Clear(); content = null; labelMaterial = null; backing = chartLines = chartFill = null;
        }

        void OnDestroy() => Clear();
    }
}

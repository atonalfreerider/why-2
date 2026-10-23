using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
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
    /// scale: low through the free-trade decades, rising in 2025; the economy's openness (exports plus imports over GDP)
    /// as a faint band beneath.</item>
    /// </list>
    /// Marked moments are read off the data (the highest rate, the years at zero, the tariff jump), not authored.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillMoneyLayer : GraphLayer
    {
        public override int Order => 64;
        public const string Path = "Data/economy/money-and-trade";
        public override IEnumerable<string> RequiredTexts => new[] { Path };
        /// <summary>Where the chart stands (land-local x: beyond the far side of the widest wave, 2025's), a percent's
        /// height and the chart's floor.</summary>
        public const float ChartX = 290, PerPercent = 2.4f, Floor = 2;
        static readonly Color Gold = new Color(1, .72f, .25f), Rose = new Color(1, .32f, .52f), Ice = new Color(.45f, .80f, 1), Steel = new Color(.70f, .76f, .86f),
            White = new Color(1, .97f, .92f);
        readonly List<TextMeshPro> labels = new List<TextMeshPro>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        string json; LandSnapshot snapshot; HillLandscape land; GameObject content;

        public override void Prepare(GraphContext ctx) { json = ctx.Text(Path); }
        public override void Upload(GraphContext ctx) { EconomyStage.Land().Place(transform); }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (HillLandscapeLayer.Current == null || LandService.Current == null) return;
            if (HillLandscapeLayer.Current.Source != LandService.Current) return;
            if (snapshot != LandService.Current || land != HillLandscapeLayer.Current)
            {
                if (!HillRebuilds.Take()) return;
                Build();
            }
            if (content == null) return;
            bool on = LandView.PresetId == "money";
            content.SetActive(on); if (!on) return;
            foreach (var t in labels) t.transform.rotation = rig.Cam.transform.rotation;
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
                b.AddSegment(new Vector3(x, Y(pct), z0), new Vector3(x, Y(pct), z1), WithAlpha(Steel, pct == 0 ? .5f : .18f), pct == 0 ? 1.4f : .8f, 0, 0, 1);
                Label(pct + "%", new Vector3(x, Y(pct), z0 - 4), .45f, WithAlpha(Steel, .8f));
            }
            for (int y = 1950; y <= last; y += 10) Label(y.ToString(LandFacts.Ci), new Vector3(x, Floor - 2.6f, HillLandscape.Z(y)), .48f, Ice);
            // the openness of the economy: exports plus imports over GDP, a faint band (its own scale: 1 unit per 2%)
            var trade = Series(root, "tradeShare");
            if (trade.Count > 1)
            {
                var low = new List<Vector3>(); var high = new List<Vector3>();
                foreach (var (year, v) in trade) { if (year > last) continue; float z = HillLandscape.Z(year); low.Add(new Vector3(x + .05f, Floor, z)); high.Add(new Vector3(x + .05f, Floor + v * .5f, z)); }
                fill.AddBand(low, high, new[] { (Color32)WithAlpha(Steel, .10f) }, 0, 1);
                // the latest year the band reaches (the crest's, when the wave stands back in time or replays)
                var (ty, tv) = trade[0];
                foreach (var p in trade) if (p.year <= last) (ty, tv) = p;
                Label("open to trade: exports + imports " + tv.ToString("0", LandFacts.Ci) + "% of GDP in " + ty + " (" + trade[0].value.ToString("0", LandFacts.Ci) + "% in " + trade[0].year + ")",
                    new Vector3(x, Floor + tv * .5f + 1.4f, HillLandscape.Z(ty) - 30), .45f, WithAlpha(Steel, .85f));
            }
            Curve(b, root, "fedFunds", x, Y, Gold, 3.2f, last, "the federal funds rate (the central bank's lever)");
            Curve(b, root, "mortgage30", x, Y, Rose, 2.2f, last, "30-year mortgage (what households pay)");
            Curve(b, root, "treasury10", x, Y, Ice, 2.2f, last, "10-year Treasury (what the state pays its lenders)");
            Curve(b, root, "tariffRate", x, Y, Steel, 3, last, "tariffs: customs duties / goods imports");
            Moments(root, x, Y, last);
            Label(string.Join("\n", legend), new Vector3(x, Y(19), z0 + 4), .5f, White, TextAlignmentOptions.Left);
            legend.Clear();
            Label("<b>THE PRICE OF MONEY AND THE EDGE OF TRADE</b>\nthe state's levers on every river of debt and on the economy's border, on the wave's own time axis",
                new Vector3(x, Y(23), (z0 + z1) * .5f), .7f, White);
            Surface(fill); Lines(b);
        }

        /// <summary>A series along time on the chart, labeled at its end with its latest value.</summary>
        void Curve(LineMeshBuilder b, JObject root, string key, float x, Func<float, float> Y, Color color, float width, int last, string name)
        {
            var s = Series(root, key); if (s.Count < 2) return;
            var pts = new List<LinePoint>();
            foreach (var (year, v) in s) { if (year > last) continue; pts.Add(new LinePoint(new Vector3(x, Y(v), HillLandscape.Z(year + .5f)), WithAlpha(color, .9f), width, 0, 2.4f)); }
            if (pts.Count < 2) return;
            b.AddPolyline(pts, 0);
            int endYear = Math.Min(last, s[s.Count - 1].year); float endValue = 0;
            foreach (var (year, v) in s) if (year == endYear) endValue = v;
            legend.Add("<color=#" + ColorUtility.ToHtmlStringRGB(Color.Lerp(color, White, .2f)) + ">● " + name + "</color> · " + endValue.ToString("0.0", LandFacts.Ci) + "% in " + endYear);
        }
        readonly List<string> legend = new List<string>();

        /// <summary>Moments read off the data: the highest federal funds rate, the years it stood at zero, the tariff rate's
        /// largest one-year rise.</summary>
        void Moments(JObject root, float x, Func<float, float> Y, int last)
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
                int jump = -1; float rise = 0;
                for (int i = 1; i < tariffs.Count; i++) { if (tariffs[i].year > last) break; float d = tariffs[i].value - tariffs[i - 1].value; if (d > rise) { rise = d; jump = i; } }
                if (jump > 0)
                    Label("tariffs " + tariffs[jump - 1].value.ToString("0.0", LandFacts.Ci) + "% → " + tariffs[jump].value.ToString("0.0", LandFacts.Ci) + "% in " + tariffs[jump].year,
                        new Vector3(x, Y(tariffs[jump].value) + 6f, HillLandscape.Z(tariffs[jump].year) - 10), .5f, Color.Lerp(Steel, White, .4f));
            }
        }

        void Lines(LineMeshBuilder b)
        {
            Mesh mesh = b.ToMesh("Money and trade"); owned.Add(mesh);
            Material m = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3238, true)); owned.Add(m);
            var r = AddMesh("Money and trade", mesh, m); r.transform.SetParent(content.transform, false);
        }

        void Surface(SurfaceMeshBuilder b)
        {
            Mesh mesh = b.ToMesh("Money and trade fill"); owned.Add(mesh);
            Material m = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1, 3236)); owned.Add(m);
            var r = AddMesh("Money and trade fill", mesh, m); r.transform.SetParent(content.transform, false);
        }

        void Label(string text, Vector3 at, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject("Money label"); go.transform.SetParent(content.transform, false); go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>(); t.text = text; t.fontSize = 24; t.alignment = align; t.color = color;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.sizeDelta = new Vector2(90, 6);
            if (align == TextAlignmentOptions.Left) t.rectTransform.pivot = new Vector2(0, .5f);
            go.transform.localScale = Vector3.one * size; labels.Add(t);
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        void Clear()
        {
            foreach (var o in owned) if (o) Destroy(o);
            if (content) Destroy(content);
            owned.Clear(); labels.Clear(); content = null;
        }

        void OnDestroy() => Clear();
    }
}

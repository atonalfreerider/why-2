using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using Why.Economy.Draw;
using Why.Economy.Emphasis;
using Why.Economy.Land;
using Roles = Why.Economy.EconomyStyle.Roles;

namespace Why.Economy.Layers
{
    /// <summary>
    /// The government's two levers on the market, and the trade they act on, beside the bedrock (view "money";
    /// Data/economy/money-and-trade.json, from FRED / BEA / Freddie Mac via Tools/history, and instant.json's BEA tables): a
    /// chart standing beside the wave on its own time axis, so a year on it stands beside the same year on the wave and the
    /// road.
    /// <list type="bullet">
    /// <item><b>The price of money.</b> The federal funds rate the central bank sets (gold), the 30-year mortgage rate
    /// households pay (rose) and the 10-year Treasury yield the state pays its lenders (ice), one height per percent: the
    /// cost of every river of debt on the hill.</item>
    /// <item><b>The edge of trade.</b> The effective tariff rate (customs duties over goods imports, steel) on the same
    /// scale: low through the free-trade decades, rising in 2018-2019 and in 2025.</item>
    /// <item><b>Trade and its balance</b> in a strip of its own beneath, on its own scale (<see cref="Trade"/>): exports above
    /// its axis and imports below, each a share of GDP (BEA NIPA Table 4.1 over Table 1.1.5's GDP), the gap between them
    /// hatched as owed, and at the year shown the goods and the money paid for them crossing the strip as packets on the flow
    /// clock (<see cref="PacketMeshBuilder"/>, <see cref="FlowClock"/>), so the chart moves while the year is frozen, the tour
    /// paused or the replay held.</item>
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
        public override IEnumerable<string> RequiredTexts => new[] { Path, TradeTable.Path };
        /// <summary>Where the chart stands (land-local x: beyond the far side of the widest wave, 2025's), a percent's
        /// height and the chart's floor (high enough that the trade strip under it stays in the view's frame).</summary>
        public const float ChartX = 290, PerPercent = 2.1f, Floor = 8;
        /// <summary>The trade strip under the rates: its top and its bottom, the band the openness strip it replaced stood in,
        /// so the view's frame and the dark panel are as they were. Exports' room above the strip's axis and imports' below
        /// share it (<see cref="Trade"/>).</summary>
        const float StripTop = Floor - 7.5f, StripBottom = StripTop - 9.8f;
        /// <summary>
        /// The strip's packets: path units a second every one travels (about 25 pixels a second at the view's pose) and how
        /// many a second each lane carries. One rate for every lane, under the four a second that strobe at 30 frames a second:
        /// a flow's size is its lane's length on the strip, never its packets' pace, and a lane rebuilt at another year
        /// (the chip, the replay) keeps its pattern.
        /// </summary>
        const float PacketSpeed = 2.5f, PacketRate = 1.6f;
        /// <summary>The packets' sizes on screen (pixels: goods' squares, money's dots), and how far each direction keeps to
        /// its right of the year's column, so the goods rising and the money falling pass each other.</summary>
        const float GoodsPx = 3.5f, MoneyPx = 3, PassPx = 2.5f;
        /// <summary>Screen pixels per unit at the chart from the view's pose (128 units off, at 1080p): the mark code sizes
        /// dashes by it; the strip's one mark, the hatched gap, is sized on screen by its material.</summary>
        const float PosePxPerUnit = 1303.7f / 128;
        /// <summary>The series take the hues the view's key names them by (the bank's rate, the mortgage, the state's
        /// borrowing, the tariff); trade takes commerce's role, and the money paid for it the owners'.</summary>
        static readonly Color Gold = EconomyStyle.Capital, Rose = EconomyStyle.Desire, Ice = EconomyStyle.Fear, Steel = EconomyStyle.Government,
            White = new Color(1, .97f, .92f);
        readonly List<TextMeshPro> labels = new List<TextMeshPro>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        string json; LandSnapshot snapshot; HillLandscape land; GameObject content;
        /// <summary>The trade strip's tables, read once on the loading worker (null when instant.json lacks them: no strip).</summary>
        TradeTable trade;
        /// <summary>The labels' material: drawn after the dark panel (text's own queue comes before every line and surface).</summary>
        Material labelMaterial;
        /// <summary>The dark panel behind the chart and its extent (land-local: its plane's x, its bottom and top, its first and
        /// last z), for fading it by the side it is seen from.</summary>
        MeshRenderer backing; float backingX; Vector2 backingY, backingZ;
        /// <summary>The chart's lines (its rules, series, moments and the trade strip's edges and column) and the trade strip's
        /// fill, and how much of the panel shows from where the camera stands (Tick).</summary>
        MeshRenderer chartLines, chartFill; float side = 1;
        /// <summary>The trade strip's packets (with the chart's lines) and its hatched gap (with its fill).</summary>
        MeshRenderer tradePackets; readonly List<MeshRenderer> tradeOwed = new List<MeshRenderer>();
        /// <summary>The emphasis keys of the chart's parts (EmphasisTable: full in this view, absent elsewhere), whose highlight
        /// ids their meshes carry.</summary>
        static class Part
        {
            public static readonly int Chart = EconomyEmphasis.Key("money.chart"), Fill = EconomyEmphasis.Key("money.fill"), Backing = EconomyEmphasis.Key("money.backing");
        }

        public override void Prepare(GraphContext ctx) { json = ctx.Text(Path); trade = TradeTable.Read(ctx.Text(TradeTable.Path)); }
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
            // the strip's packets and its hatched gap dim and glow with the lines and the fill they belong to
            EconomyEmphasis.Apply(tradePackets, Part.Chart);
            foreach (var r in tradeOwed) EconomyEmphasis.Apply(r, Part.Fill);
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
            // the trade strip's hatched gap, and its packets: two lanes each side of its axis, two points each
            var marks = new MarkMeshes(PosePxPerUnit); var packets = new PacketMeshBuilder(8);
            float Y(float percent) => Floor + percent * PerPercent;
            // the scale: a rule every 5 percent along the whole wake, and the years every decade
            float z0 = HillLandscape.Z(1947), z1 = HillLandscape.Z(last);
            for (int pct = 0; pct <= 20; pct += 5)
            {
                b.AddSegment(new Vector3(x, Y(pct), z0), new Vector3(x, Y(pct), z1), WithAlpha(Steel, pct == 0 ? .5f : .18f), pct == 0 ? 1.4f : .8f, 0, EconomyEmphasis.Id(Part.Chart), 1);
                Label(pct + "%", new Vector3(x, Y(pct), z0 - 4), .45f, WithAlpha(Steel, .8f));
            }
            for (int y = 1950; y <= last; y += 10) Label(y.ToString(LandFacts.Ci), new Vector3(x, Floor - 2.6f, HillLandscape.Z(y)), .48f, Ice);
            Trade(b, fill, marks, packets, x, z0, z1, last);
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
            Surface(fill); Owed(marks); Lines(b); Packets(packets);
        }

        /// <summary>
        /// Trade and its balance, in a strip of its own under the rates (the user asked to "show trade deficits"; the flows
        /// plan's proposal 4c, Docs/research/politics/maps/government-flows-power-2026-11-07.json): exports above the strip's
        /// axis and imports below it, each a share of the year's GDP, by year on the chart's time axis up to the year shown.
        /// A share keeps 1950's $12 billion of exports and 2025's $3.3 trillion on one readable scale, as the openness strip
        /// this replaced did (the strip's whole height is that openness); the dollars are printed. The trade that pays for
        /// itself, the smaller side mirrored about the axis, is a solid core (measured); the gap between the two, the larger
        /// side's excess, is hatched as owed (the mark code's form, as the politics view hatches a deficit): a deficit hangs on
        /// the imports' side, a surplus stands on the exports'. A year the tables do not hold is not drawn: a gap in a line
        /// leaves a gap in the strip, and the strip ends at the tables' last year where the chart runs past it (2026).
        /// </summary>
        void Trade(LineMeshBuilder b, SurfaceMeshBuilder fill, MarkMeshes marks, PacketMeshBuilder packets, float x, float z0, float z1, int last)
        {
            var t = trade;
            int shown = t?.Latest(last) ?? -1;
            if (shown < 0) return;
            // the scale from the most each side reaches in the whole table, not only up to the year shown, so it holds through
            // the replay: exports' room above the axis and imports' below share the strip's band
            float up = Mathf.Ceil(t.MaxExports + .25f), down = Mathf.Ceil(t.MaxImports + .25f);
            float perShare = (StripTop - StripBottom) / (up + down), axis = StripBottom + down * perShare;
            float S(float share) => axis + share * perShare;
            float id = EconomyEmphasis.Id(Part.Chart), fillId = EconomyEmphasis.Id(Part.Fill);
            Color ink = Roles.Encoded(Roles.Commerce);
            float zShown = HillLandscape.Z(shown + .5f), zEnd = Mathf.Max(z1, zShown);

            // the axis (the border: what leaves stands above it, what arrives hangs below) and a rule at 10% of GDP each side
            b.AddSegment(new Vector3(x, axis, z0), new Vector3(x, axis, zEnd), WithAlpha(Roles.Commerce, .5f), 1.2f, 0, id, 1);
            foreach (float share in new[] { 10f, -10f })
            {
                if (share > up || -share > down) continue;
                b.AddSegment(new Vector3(x, S(share), z0), new Vector3(x, S(share), zEnd), WithAlpha(Roles.Commerce, .15f), .8f, 0, id, 1);
                Label(share > 0 ? "10% of GDP" : "10%", new Vector3(x, S(share), z0 - 4), .42f, WithAlpha(ink, .85f));
            }
            Label("exports", new Vector3(x, axis + .8f, z0 - 4), .42f, WithAlpha(ink, .95f));
            Label("imports", new Vector3(x, axis - .8f, z0 - 4), .42f, WithAlpha(ink, .95f));

            // Year by year: the core, both gaps (each zero high in the years the other side is the larger, so it closes to a
            // point where the balance changes sign) and the two outer edges. A year the tables lack ends a run; the next
            // measured year starts another.
            Color32 core = WithAlpha(Roles.Commerce, .22f), owed = WithAlpha(Roles.Commerce, .85f), edge = WithAlpha(Roles.Commerce, .9f);
            var coreLow = new List<Vector3>(); var coreHigh = new List<Vector3>();
            var deficitLow = new List<Vector3>(); var deficitHigh = new List<Vector3>();
            var surplusLow = new List<Vector3>(); var surplusHigh = new List<Vector3>();
            var top = new List<LinePoint>(); var bottom = new List<LinePoint>();
            void Run()
            {
                fill.AddBand(coreLow, coreHigh, new[] { core }, fillId, 1);
                marks.Hatched.AddBand(deficitLow, deficitHigh, new[] { owed }, fillId);
                marks.Hatched.AddBand(surplusLow, surplusHigh, new[] { owed }, fillId);
                b.AddPolyline(top, id); b.AddPolyline(bottom, id);
                coreLow.Clear(); coreHigh.Clear(); deficitLow.Clear(); deficitHigh.Clear(); surplusLow.Clear(); surplusHigh.Clear();
                top.Clear(); bottom.Clear();
            }
            for (int year = t.First; year <= shown; year++)
            {
                if (!t.Measured(year)) { Run(); continue; }
                float e = t.ExportsShare(year), i = t.ImportsShare(year), m = Mathf.Min(e, i), z = HillLandscape.Z(year + .5f);
                coreLow.Add(new Vector3(x + .05f, S(-m), z)); coreHigh.Add(new Vector3(x + .05f, S(m), z));
                deficitLow.Add(new Vector3(x + .05f, S(-i), z)); deficitHigh.Add(new Vector3(x + .05f, S(-m), z));
                surplusLow.Add(new Vector3(x + .05f, S(m), z)); surplusHigh.Add(new Vector3(x + .05f, S(e), z));
                top.Add(new LinePoint(new Vector3(x, S(e), z), edge, 1.6f, 0, 1));
                bottom.Add(new LinePoint(new Vector3(x, S(-i), z), edge, 1.6f, 0, 1));
            }
            Run();
            TradeYear(b, packets, t, shown, x, axis, S, zShown, id, ink);
        }

        /// <summary>
        /// The year shown on the trade strip: its column, the packets crossing it and its numbers beside it. Goods leave
        /// across the axis and rise to the exports' height; goods from abroad rise from the imports' depth to the axis; the
        /// money paid for each goes the other way, so a dot falls for every square that rises. Each lane keeps to its right,
        /// so the goods pass on the column's later side and the money on its earlier one. They move on the flow clock, so the
        /// column is never still; every lane keeps its rate and its seed from year to year, so a new year (the chip, the
        /// replay) carries the pattern along with the column instead of breaking it.
        /// </summary>
        void TradeYear(LineMeshBuilder b, PacketMeshBuilder packets, TradeTable t, int year, float x, float axis, Func<float, float> S, float z, float id, Color ink)
        {
            float e = t.ExportsShare(year), i = t.ImportsShare(year), xp = x + .15f;
            b.AddSegment(new Vector3(x, S(-i), z), new Vector3(x, S(e), z), WithAlpha(Roles.Commerce, .45f), 1, 0, id, 1);
            var exported = new[] { new Vector3(xp, axis, z), new Vector3(xp, S(e), z) };
            var imported = new[] { new Vector3(xp, S(-i), z), new Vector3(xp, axis, z) };
            Color goods = WithAlpha(Roles.Commerce, .95f), money = WithAlpha(Roles.Owners, .95f);
            packets.AddLane(exported, PacketKind.Goods, goods, PacketRate, PacketSpeed, GoodsPx, id, offsetPx: PassPx);
            packets.AddLane(exported, PacketKind.Money, money, PacketRate, PacketSpeed, MoneyPx, id, offsetPx: PassPx, seed: .5f, backward: true);
            packets.AddLane(imported, PacketKind.Goods, goods, PacketRate, PacketSpeed, GoodsPx, id, offsetPx: PassPx, seed: .25f);
            packets.AddLane(imported, PacketKind.Money, money, PacketRate, PacketSpeed, MoneyPx, id, offsetPx: PassPx, seed: .75f, backward: true);

            // Its numbers, left-aligned past the goods' side of the column: exports just above the axis's height and imports
            // just below, then the gap, the current account and what the moving marks are. With 2025 shown each line ends short
            // of the screen's right edge at 1080p: at most about 17.5 of the label's ems, 175 of the 195 pixels left there.
            float left = z + 1.5f;
            string when = " in " + year.ToString(LandFacts.Ci);
            double exports = t.Exports(year), imports = t.Imports(year), balance = exports - imports, current = t.Current(year);
            Color text = WithAlpha(ink, .95f), dim = WithAlpha(ink, .8f);
            void Row(string s, float y, Color c) => Label(s, new Vector3(x, y, left), .42f, c, TextAlignmentOptions.Left);
            Row("<b>TRADE</b> · " + TradeTable.Source, axis + 1.8f, text);
            Row("exports " + LandFacts.Money(exports) + when + " · " + Share(e) + " of GDP", axis + .65f, text);
            Row("imports " + LandFacts.Money(imports) + when + " · " + Share(i) + " of GDP", axis - .65f, text);
            // the hatched gap's amount; the key says what it means (a deficit financed from abroad, a surplus lent abroad: the
            // current account below is the balance that adds to the net debt, this line is goods and services only), as the
            // words here would run past the screen's right edge at 1080p
            Row((balance < 0 ? "deficit " : "surplus ") + LandFacts.Money(Math.Abs(balance)) + when + ", hatched", axis - 1.8f, text);
            Row(double.IsNaN(current) ? "current account not in the table for " + year.ToString(LandFacts.Ci) : "current account " + LandFacts.Money(current) + when, axis - 2.95f, dim);
            Row("moving: <color=#" + Hex(Roles.Commerce) + ">squares goods</color>, <color=#" + Hex(Roles.Owners) + ">dots money</color>", axis - 4.1f, dim);
        }

        /// <summary>A share of GDP as the strip prints it: "10.8%".</summary>
        static string Share(float percent) => percent.ToString("0.0", LandFacts.Ci) + "%";

        /// <summary>A role as a label's rich text names it (sRGB-encoded, as text takes its colours).</summary>
        static string Hex(Color role) => ColorUtility.ToHtmlStringRGB(Roles.Encoded(role));

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

        /// <summary>The trade strip's hatched gap, in the mark code's owed material: after the fill, before the lines.</summary>
        void Owed(MarkMeshes marks) => tradeOwed.AddRange(marks.Upload(content.transform, "Money and trade owed", 3247, owned));

        /// <summary>The trade strip's packets, after the lines and before the labels: built once a rebuild, moved by the shader
        /// on the flow clock with no work a frame.</summary>
        void Packets(PacketMeshBuilder p)
        {
            if (p.VertexCount == 0) return;
            Mesh mesh = p.ToMesh("Money and trade packets"); owned.Add(mesh);
            Material m = GraphMaterials.Raw(GraphMaterials.Packets(Color.white, 1, 3249)); owned.Add(m);
            var r = AddMesh("Money and trade packets", mesh, m); r.transform.SetParent(content.transform, false); tradePackets = r;
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
            owned.Clear(); labels.Clear(); content = null; labelMaterial = null; backing = chartLines = chartFill = tradePackets = null;
            tradeOwed.Clear();
        }

        void OnDestroy() => Clear();

        /// <summary>
        /// The foreign transactions the trade strip draws, from instant.json's BEA tables (Tools/instant/build.py: the NIPA flat
        /// files' current vintage, one vintage for every line): Table 4.1's exports (line 2), imports (line 18) and current
        /// account (line 33), and Table 1.1.5's GDP (line 1), by year in billions of current dollars. A line is taken by its
        /// number when it carries the BEA series asked for, else found by that series (should a later build renumber the
        /// table); a year a line holds no value for is NaN, never zero. Pure: read once, on the loading worker.
        /// </summary>
        sealed class TradeTable
        {
            public const string Path = Model.InstantModel.Path, Source = "BEA NIPA Tables 4.1, 1.1.5";
            /// <summary>The first and last year any of the four lines holds.</summary>
            public readonly int First, Last;
            /// <summary>The largest share of GDP (percent) exports and imports reach in any year the tables hold.</summary>
            public readonly float MaxExports, MaxImports;
            readonly double[] exports, imports, current, gdp;

            TradeTable(int first, int last, double[] exports, double[] imports, double[] current, double[] gdp)
            {
                First = first; Last = last;
                this.exports = exports; this.imports = imports; this.current = current; this.gdp = gdp;
                for (int y = first; y <= last; y++)
                    if (Measured(y)) { MaxExports = Math.Max(MaxExports, ExportsShare(y)); MaxImports = Math.Max(MaxImports, ImportsShare(y)); }
            }

            /// <summary>Whether the tables hold the year's exports, imports and GDP, so its shares can be drawn.</summary>
            public bool Measured(int year) =>
                year >= First && year <= Last && Amount(exports[year - First]) && Amount(imports[year - First]) && Amount(gdp[year - First]) && gdp[year - First] > 0;

            /// <summary>The year's dollars ($B); NaN when a line holds none.</summary>
            public double Exports(int year) => At(exports, year);
            public double Imports(int year) => At(imports, year);
            public double Current(int year) => At(current, year);

            /// <summary>The year's exports and imports as shares of its GDP (percent).</summary>
            public float ExportsShare(int year) => (float)(100 * At(exports, year) / At(gdp, year));
            public float ImportsShare(int year) => (float)(100 * At(imports, year) / At(gdp, year));

            /// <summary>The latest year up to <paramref name="year"/> whose shares can be drawn; -1 when there is none.</summary>
            public int Latest(int year)
            {
                for (int y = Math.Min(year, Last); y >= First; y--)
                    if (Measured(y)) return y;
                return -1;
            }

            double At(double[] line, int year) => year >= First && year <= Last ? line[year - First] : double.NaN;

            static bool Amount(double v) => !double.IsNaN(v) && !double.IsInfinity(v) && v >= 0;

            /// <summary>The tables from instant.json's text; null when it is missing, unreadable or lacks them.</summary>
            public static TradeTable Read(string json)
            {
                if (string.IsNullOrEmpty(json)) return null;
                try
                {
                    var tables = (JObject.Parse(json)["nipa"] as JObject)?["tables"] as JObject;
                    var foreign = (tables?["T40100"] as JObject)?["lines"] as JObject;
                    var product = (tables?["T10105"] as JObject)?["lines"] as JObject;
                    if (foreign == null || product == null) return null;
                    var lines = new[] { Line(foreign, "2", "B020RC"), Line(foreign, "18", "B021RC"), Line(foreign, "33", "A124RC"), Line(product, "1", "A191RC") };
                    int first = int.MaxValue, last = int.MinValue;
                    foreach (var (start, values) in lines)
                        if (values.Length > 0) { first = Math.Min(first, start); last = Math.Max(last, start + values.Length - 1); }
                    if (first > last) return null;
                    double[] OnYears((int start, double[] values) line)
                    {
                        var a = new double[last - first + 1];
                        for (int k = 0; k < a.Length; k++) a[k] = double.NaN;
                        for (int k = 0; k < line.values.Length; k++) a[line.start + k - first] = line.values[k];
                        return a;
                    }
                    return new TradeTable(first, last, OnYears(lines[0]), OnYears(lines[1]), OnYears(lines[2]), OnYears(lines[3]));
                }
                catch (Exception)
                {
                    return null;
                }
            }

            /// <summary>A line's values from its first year ("values[i] is year first + i"; a null is NaN), or none.</summary>
            static (int start, double[] values) Line(JObject lines, string number, string series)
            {
                var line = lines[number] as JObject;
                if (line == null || (string)line["series"] != series)
                {
                    line = null;
                    foreach (var p in lines.Properties())
                        if (p.Value is JObject l && (string)l["series"] == series) { line = l; break; }
                }
                if (line == null || !(line["values"] is JArray v) || line["first"]?.Type != JTokenType.Integer) return (0, Array.Empty<double>());
                var values = new double[v.Count];
                for (int k = 0; k < v.Count; k++) values[k] = v[k].Type == JTokenType.Float || v[k].Type == JTokenType.Integer ? (double)v[k] : double.NaN;
                return ((int)line["first"], values);
            }
        }
    }
}

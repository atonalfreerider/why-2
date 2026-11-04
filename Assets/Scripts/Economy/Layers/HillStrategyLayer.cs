using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Why.Economy.Land;
using Why.Economy.Model;
using Why.Economy.UI;

namespace Why.Economy.Layers
{
    /// <summary>
    /// Companies spend too (view "strategy"; the data is <see cref="CorporatePrograms"/>): every stored firm runs the
    /// people's program at its own scale. From each firm's place on its hill its year's money leaves in the directions it
    /// goes: running the business runs out to its suppliers' hills (amber) and to its own workers (blue); investment and
    /// research leave forward over the crest into the future, split into the fear lane (ice: spending so as not to lose
    /// position) and the desire lane (rose: spending for new demand) by Jev's judgment; advertising climbs to the attention
    /// platforms (rose); acquisitions and stakes reach across (white); buybacks and dividends rise to the owners (gold);
    /// cash taxes drop into the bedrock and lobbying runs to the officeholders (steel). White arrows show which way Jev
    /// judged the money moves the firm in the hierarchy (climb, reach down, spread across, lean on the state, harvest).
    /// <para>The AI cascade: the five hyperscalers' AI investment pours down the hill into computers and chips and into
    /// data-center buildings, in proportion to the year's US totals for each (BEA computers and peripherals, Census data
    /// centers). Above the wave a readout sets the firms against the US totals (business investment, advertising, payouts,
    /// lobbying, M&amp;A, the hyperscalers' capex and guidance).</para>
    /// <para>The trail: behind the crest every firm's fiscal years 2015-2025 (<see cref="CorporatePrograms.Firm.History"/>)
    /// lie along its lifeline in the wake, each over its own year's stretch of z: the year's investment and research as a
    /// lane split into fear and desire by that year's judgment (made on its numbers alone), the years running as one
    /// ribbon into the crest, and its owners' payout as a gold stalk, so a firm's direction and defensiveness can be
    /// followed through the cycle; the focused firm's years carry chevrons for which way its money moved it, and its first
    /// year and every turn of direction are named.</para>
    /// Widths are the square root of dollars. One firm is drawn in full at a time (the selected or hovered one, else the
    /// largest investor); the others' streams are faint. Fiscal years differ by firm (each label says which). Splits of a
    /// firm's costs come from its industry's accounts; the judgments are model evidence, not measurements.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillStrategyLayer : GraphLayer
    {
        public override int Order => 63;
        public override IEnumerable<string> RequiredTexts => new[] { CorporatePrograms.Path };
        enum Part { Streams, Focus, Arrows, Cascade, Trail, Count }
        static readonly Color Gold = new Color(1, .70f, .22f), Rose = new Color(1, .26f, .50f), Ice = new Color(.30f, .72f, 1),
            Blue = new Color(.36f, .70f, 1), White = new Color(1, .97f, .92f), Steel = new Color(.62f, .70f, .82f), Amber = new Color(1, .80f, .52f);
        /// <summary>The tickers whose capital spending is the AI build-out (calendar or nearest fiscal year).</summary>
        static readonly string[] Hyperscalers = { "MSFT", "GOOGL", "AMZN", "META", "ORCL" };
        /// <summary>How far a flow's pulse brightens a line (1 = the shader's fourfold): a sheen, under the bloom. Only a chosen
        /// firm's streams keep the full pulse (<see cref="DrawFocus"/>).</summary>
        const float Pulse = .2f;
        /// <summary>The firm pinned by the strategy panel (tower index; -1: follow hover, else the largest investor).</summary>
        public static int Pinned = -1;
        /// <summary>The focused firm (tower index), its readout and the US totals' readout, for the strategy panel.</summary>
        public static int Focused { get; private set; } = -1;
        public static string FocusText { get; private set; } = "";
        public static string ReadoutText { get; private set; } = "";
        /// <summary>The focused firm's years as rows (the trail in numbers), for the strategy panel.</summary>
        public static string HistoryText { get; private set; } = "";
        /// <summary>The towers with a program, largest spender first (the panel's order for previous / next).</summary>
        public static readonly List<int> Ranked = new List<int>();
        readonly List<(Renderer renderer, Part part)> renderers = new List<(Renderer, Part)>();
        readonly List<(TextMeshPro text, int firm)> labels = new List<(TextMeshPro, int)>();
        /// <summary>The focused trail's names (its first year and every turn of direction), reused as the focus moves.</summary>
        readonly List<TextMeshPro> trailLabels = new List<TextMeshPro>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        string json;
        LandSnapshot snapshot; HillLandscape land; CorporatePrograms programs; GameObject content;
        MeshRenderer focusRenderer; TextMeshPro focusLabel;
        int focused = -2;
        /// <summary>Whether the focused firm was chosen (pinned, explored or hovered) rather than the default largest investor:
        /// only a chosen firm's marks may bloom (<see cref="cap"/>).</summary>
        bool focusChosen;
        /// <summary>The highest intensity the marks being drawn may take: 1 (below the bloom) but for a chosen firm's.</summary>
        float cap = 1;
        Vector3 attention, seats;

        public override void Prepare(GraphContext ctx) { json = ctx.Text(CorporatePrograms.Path); }
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
            bool on = LandView.PresetId == "strategy";
            content.SetActive(on); if (!on) return;
            int f = Focus(out bool chosen);
            if (f != focused || chosen != focusChosen) { focused = f; focusChosen = chosen; Focused = f; DrawFocus(); }
            foreach (var e in labels) { e.text.gameObject.SetActive(true); e.text.transform.rotation = rig.Cam.transform.rotation; }
            if (focusLabel) { focusLabel.gameObject.SetActive(focused >= 0); focusLabel.transform.rotation = rig.Cam.transform.rotation; }
            foreach (var l in trailLabels)
            {
                bool show = focused >= 0 && !string.IsNullOrEmpty(l.text);
                l.gameObject.SetActive(show); if (show) l.transform.rotation = rig.Cam.transform.rotation;
            }
        }

        /// <summary>The firm drawn in full (tower index): the pinned or explorer's firm, else the hovered or selected company
        /// line's tower (<paramref name="chosen"/>), else the largest investor.</summary>
        int Focus(out bool chosen)
        {
            chosen = true;
            if (programs == null) return -1;
            if (Pinned >= 0 && Pinned < snapshot.Land.Towers.Length && programs.OfTower(Tower(Pinned)) != null) return Pinned;
            if (HillExplorer.Company >= 0 && programs.OfTower(Tower(HillExplorer.Company)) != null) return HillExplorer.Company;
            int life = HillWaveLayer.HoverCompany >= 0 ? HillWaveLayer.HoverCompany : HillWaveLayer.SelectedCompany;
            var lives = CompanyLives.Get();
            if (life >= 0 && lives != null && life < lives.Lives.Length)
            {
                var towers = snapshot.Land.Towers;
                for (int t = 0; t < towers.Length; t++) if (CompanyLives.Matches(towers[t].Ticker, towers[t].Name, lives.Lives[life])) return t;
            }
            chosen = false;
            int best = -1; double most = 0;
            for (int t = 0; t < snapshot.Land.Towers.Length; t++) { var p = programs.OfTower(Tower(t)); if (p != null && p.Invest > most) { most = p.Invest; best = t; } }
            return best;
        }

        /// <summary>The capture index of a tower.</summary>
        int Tower(int t) => t >= 0 && t < snapshot.Land.Towers.Length ? snapshot.Land.Towers[t].Company : -1;

        void Build()
        {
            Clear();
            snapshot = LandService.Current; land = HillLandscapeLayer.Current;
            CorporatePrograms.Provide(LandService.Model.Data, json); programs = CorporatePrograms.Get();
            if (programs == null || snapshot.Land.Towers.Length == 0) return;
            foreach (string problem in programs.Problems(snapshot.Land.Towers.Length)) Debug.LogError("[Why] Strategy check failed: " + problem);
            content = new GameObject("Corporate programs"); content.transform.SetParent(transform, false);
            var data = LandService.Model.Data;
            // Where attention is sold (the internet platforms' firms, else the internet hill) and where officeholders sit.
            attention = Vector3.zero; int n = 0; var towers = snapshot.Land.Towers;
            var internet = data.IndustryById("internet");
            for (int t = 0; t < towers.Length; t++) if (internet != null && towers[t].Industry == internet.Index) { attention += HillActivityLayer.CompanyHalo(t); n++; }
            attention = n > 0 ? attention / n + Vector3.up * 3 : internet != null ? land.Halo(internet.Index) + Vector3.up * 3 : land.CloudCenter;
            seats = Vector3.zero; n = 0;
            foreach (var s in HillFoundationLayer.Seats) { seats += s.At; n++; }
            seats = n > 0 ? seats / n : new Vector3(HillLandscape.FootX, HillLandscape.Bedrock, land.CrestZ);
            var faint = new LineMeshBuilder(); var fill = new SurfaceMeshBuilder(); var arrows = new LineMeshBuilder();
            var trail = new LineMeshBuilder(); var trailFill = new SurfaceMeshBuilder(); int years = 0;
            double invest = 0, owners = 0, ads = 0, lobbying = 0, research = 0, deals = 0, taxes = 0, operations = 0;
            Ranked.Clear();
            for (int t = 0; t < towers.Length; t++) if (programs.OfTower(Tower(t)) != null) Ranked.Add(t);
            Ranked.Sort((a, c) => programs.OfTower(Tower(c)).Total.CompareTo(programs.OfTower(Tower(a)).Total));
            for (int t = 0; t < towers.Length; t++)
            {
                var p = programs.OfTower(Tower(t)); if (p == null) continue;
                Streams(faint, fill, t, p, .3f, false);
                Arrows(arrows, t, p);
                Trail(trail, trailFill, t, p, .3f, false); years += p.History.Length;
                invest += p.Invest; owners += p.Owners; ads += p.Attention; lobbying += double.IsNaN(p.Lobbying) ? 0 : p.Lobbying; research += p.Research;
                deals += p.Deals; taxes += p.State; operations += p.Operations;
                // the largest spenders are named with the way their money moves them; every firm is named in the panel
                if (Ranked.IndexOf(t) < 12)
                    Label("<b>" + p.Name + "</b>\n" + CorporatePrograms.DirectionShort[p.Main] + " " + LandFacts.Percent(p.Direction[p.Main]), HillActivityLayer.CompanyHalo(t) + Vector3.up * 3.5f, .4f, Color.Lerp(White, Steel, .25f), t);
            }
            // the 25 firms' faint streams, trails and arrows cross each other: blended, not added, so the crossings keep
            // their hues; the focused firm's marks add light over them
            Surface(fill, Part.Streams); Lines(faint, Part.Streams, true, false); Lines(arrows, Part.Arrows, false, false);
            Surface(trailFill, Part.Trail); Lines(trail, Part.Trail, true, false);
            Cascade();
            focusRenderer = Lines(new LineMeshBuilder(), Part.Focus, true, true); focused = -2;
            focusLabel = Label("", Vector3.zero, .6f, White, -1);
            Readout(invest, research, owners, ads, deals, taxes, lobbying, operations);
            Debug.Log("[Why] Strategy " + snapshot.Year + ": " + programs.Firms.Length + " firms (judgments " + (programs.Model ?? "absent") + "); they spent " +
                LandFacts.Money(operations) + " running the business, invested " + LandFacts.Money(invest) + " + research " + LandFacts.Money(research) +
                ", paid owners " + LandFacts.Money(owners) + ", advertising " + LandFacts.Money(ads) + ", deals " + LandFacts.Money(deals) + ", cash taxes " +
                LandFacts.Money(taxes) + ", lobbying $" + lobbying.ToString("0", LandFacts.Ci) + "M; hyperscalers " + LandFacts.Money(programs.Hyperscalers().y2025) +
                " in 2025, guiding " + LandFacts.Money(programs.Hyperscalers().y2026) + " for 2026.");
            Debug.Log("[Why] Strategy trail: " + years + " firm-years " + CorporatePrograms.FirstYear + "-" + CorporatePrograms.LastYear + " (collected " +
                (programs.HistoryCollected ?? "?") + ", " + programs.HistoryGaps.Length + " gaps named by the collector); a year spans " +
                (HillLandscape.Z(2025) - HillLandscape.Z(2024)).ToString("0.00", LandFacts.Ci) + " units along the wake; " + TrailSummary());
        }

        /// <summary>The trail's story for the log: how the firms' investment and payouts moved from the first year to the last
        /// (the firms with both years), and which firms turned direction.</summary>
        string TrailSummary()
        {
            double invest0 = 0, invest1 = 0, owners0 = 0, owners1 = 0; int both = 0; var turned = new List<string>();
            foreach (var f in programs.Firms)
            {
                if (f.History.Length < 2) continue;
                var a = f.History[0]; var z = f.History[f.History.Length - 1];
                if (a.Year == CorporatePrograms.FirstYear && z.Year == CorporatePrograms.LastYear)
                { both++; invest0 += a.Invest + a.Research; invest1 += z.Invest + z.Research; owners0 += a.Owners; owners1 += z.Owners; }
                if (a.Judged && z.Judged && a.Main != z.Main) turned.Add(f.Name + " " + CorporatePrograms.DirectionShort[a.Main] + " → " + CorporatePrograms.DirectionShort[z.Main]);
            }
            return both + " firms with both ends invested and researched " + LandFacts.Money(invest0) + " in " + CorporatePrograms.FirstYear + " and " +
                LandFacts.Money(invest1) + " in " + CorporatePrograms.LastYear + ", paid owners " + LandFacts.Money(owners0) + " and " + LandFacts.Money(owners1) +
                "; turned: " + (turned.Count == 0 ? "none" : string.Join(", ", turned));
        }

        // ------------------------------------------------------------------ one firm's streams

        void Streams(LineMeshBuilder b, SurfaceMeshBuilder fill, int t, CorporatePrograms.Firm p, float alpha, bool full)
        {
            Vector3 foot = HillActivityLayer.CompanyPosition(t), halo = HillActivityLayer.CompanyHalo(t);
            float W(double billions) => (full ? .6f : .4f) + Mathf.Sqrt((float)Math.Max(0, billions)) * (full ? .55f : .32f);
            // Running the business: out to its suppliers' hills (the four largest supplying industries when drawn in full, else two) and workers on its own slope.
            var bySupplier = new double[land.Hills.Length];
            CorporatePrograms.SplitOperations(p, ValueCapture.Get(), out double suppliers, out double workers, bySupplier);
            var order = new List<int>(); for (int i = 0; i < bySupplier.Length; i++) if (bySupplier[i] > 0) order.Add(i);
            order.Sort((a, c) => bySupplier[c].CompareTo(bySupplier[a]));
            for (int k = 0; k < order.Count && k < (full ? 4 : 2); k++)
                Arc(b, foot + Vector3.up * .5f, land.Market(order[k]) + Vector3.up * .3f, WithAlpha(Amber, alpha), W(bySupplier[order[k]]), C(full ? 2.2f : 1.2f), 1.5f + .08f * Vector3.Distance(foot, land.Market(order[k])));
            if (p.Industry >= 0 && workers > 0)
                Arc(b, foot + Vector3.up * .4f, land.Market(p.Industry) + Vector3.up * .5f, WithAlpha(Blue, alpha), W(workers), C(full ? 2.2f : 1.2f), 1.2f);
            // The future: investment and research leave forward over the crest, in the fear and desire lanes.
            double future = p.Invest + p.Research;
            if (future > 0)
            {
                Vector3 lip = new Vector3(halo.x, halo.y + 2.5f, land.CrestZ + 2), ahead = new Vector3(halo.x + 3, Mathf.Max(4, halo.y * .55f), land.CrestZ + (full ? 16 : 12));
                var path = new Vector3[25];
                for (int s = 0; s < path.Length; s++) { float u = s / 24f; path[s] = Vector3.Lerp(Vector3.Lerp(halo, lip, u), Vector3.Lerp(lip, ahead, u), u); }
                Lanes(fill, b, path, (full ? .3f : .15f) + Mathf.Sqrt((float)future) * (full ? .16f : .09f), p.Defensive, alpha, t);
            }
            if (p.Owners > 0) Arc(b, halo + Vector3.up * .4f, land.CloudCenter, WithAlpha(Gold, alpha), W(p.Owners), C(full ? 3f : 1.6f), 4);
            if (p.Attention > 0) Arc(b, halo + Vector3.up * .3f, attention, WithAlpha(Rose, alpha), W(p.Attention), C(full ? 2.6f : 1.4f), 3);
            if (p.Deals > 0 && p.Industry >= 0) Arc(b, halo, land.Halo(p.Industry) + new Vector3(0, 1.5f, -2), WithAlpha(White, alpha * .9f), W(p.Deals), C(full ? 2.2f : 1.1f), 2.5f);
            if (p.State > 0)
            {
                Vector3 down = new Vector3(foot.x, HillLandscape.Bedrock + .3f, Mathf.Min(foot.z, land.CrestZ - 1));
                var drop = new LinePoint[12];
                for (int s = 0; s < drop.Length; s++) drop[s] = new LinePoint(Vector3.Lerp(foot, down, s / 11f), WithAlpha(Steel, alpha), W(p.State), 0, C(full ? 2 : 1.1f));
                b.AddFlowPath(drop, 0, .5f, t * .3f);
            }
            if (!double.IsNaN(p.Lobbying) && p.Lobbying > 0)
                Arc(b, foot, seats + Vector3.up * 2, WithAlpha(Steel, alpha * (full ? 1 : .6f)), full ? 1.6f : .8f, C(full ? 2.6f : 1), 2);
        }

        /// <summary>An intensity no higher than <see cref="cap"/>: hue-coded marks stay below the bloom unless chosen.</summary>
        float C(float intensity) => Mathf.Min(intensity, cap);

        /// <summary>Which way the money moves the firm: an arrow for each direction Jev gave at least a fifth of its weight,
        /// as long as its probability (spreading across: a ring around the firm). Judgments, not dollars: drawn below the
        /// bloom, so they never outshine the money.</summary>
        void Arrows(LineMeshBuilder b, int t, CorporatePrograms.Firm p)
        {
            if (!p.Judged) return;
            Vector3 at = HillActivityLayer.CompanyHalo(t) + Vector3.up * 1.2f;
            for (int d = 0; d < CorporatePrograms.Directions; d++)
            {
                float w = p.Direction[d]; if (w < .2f) continue;
                float length = 5 + 16 * w; Color c = WithAlpha(White, .5f + .5f * w); float width = 2.4f + 5 * w;
                Vector3 dir = Way(d, at, snapshot.Year);
                if (dir == Vector3.zero) { Ring(b, at, 1.2f + 3 * w, c, width, 1); continue; }
                dir = dir.normalized; Vector3 tip = at + dir * length;
                b.AddSegment(at, tip, c, width, 0, 0);
                Vector3 side = Vector3.Cross(dir, Vector3.forward); if (side.sqrMagnitude < 1e-4f) side = Vector3.right; side.Normalize();
                b.AddSegment(tip, tip - dir * 2f + side * 1.2f, c, width, 0, 0);
                b.AddSegment(tip, tip - dir * 2f - side * 1.2f, c, width, 0, 0);
            }
        }

        void DrawFocus()
        {
            var b = new LineMeshBuilder(); var fill = new SurfaceMeshBuilder();
            var p = focused >= 0 ? programs.OfTower(Tower(focused)) : null;
            int named = 0;
            if (p != null)
            {
                // a chosen firm's marks may bloom (a highlight), pulses too; the default largest investor's stay in their hues
                cap = focusChosen ? float.MaxValue : 1;
                focusRenderer.sharedMaterial.SetFloat("_Flow", focusChosen ? 1 : Pulse);
                Streams(b, fill, focused, p, .95f, true);
                Trail(b, fill, focused, p, .95f, true);
                cap = 1;
                // the firm's name over its tower first: its trail's names keep clear of it (NameTrail)
                focusLabel.text = "<b>" + p.Name + "</b>"; focusLabel.transform.localPosition = HillActivityLayer.CompanyHalo(focused) + Vector3.up * 7;
                named = NameTrail(p);
                var bySupplier = new double[land.Hills.Length];
                CorporatePrograms.SplitOperations(p, ValueCapture.Get(), out double suppliers, out double workers, bySupplier);
                string D(int d) => CorporatePrograms.DirectionShort[d] + " " + LandFacts.Percent(p.Direction[d]);
                var dirs = new List<int>(); for (int d = 0; d < CorporatePrograms.Directions; d++) dirs.Add(d);
                dirs.Sort((a, c) => p.Direction[c].CompareTo(p.Direction[a]));
                // the investment's split is Jev's judgment, named as such with its confidence (at most one half for every firm),
                // so it is not read as the year readout's model fear share of household spending. The line wraps in the panel
                // (for every firm in landscape): the bracket, the demand share and the lobbying each stay whole (<nobr>), never split
                string text = "<b>" + p.Name + "</b> · " + p.Period + (double.IsNaN(p.Revenue) ? "" : " · revenue " + LandFacts.Money(p.Revenue)) +
                    "\n<color=#FFCC85>runs the business " + LandFacts.Money(p.Operations) + "</color> (suppliers ≈ " + LandFacts.Money(suppliers) + " · <color=#6FB6FF>workers ≈ " + LandFacts.Money(workers) + "</color>)" +
                    "\ninvests " + LandFacts.Money(p.Invest) + (p.Research > 0 ? " + research " + LandFacts.Money(p.Research) : "") + ": <color=#4FB8FF>defensive " + LandFacts.Percent(p.Defensive) +
                    "</color>" + (p.Judged ? " <nobr>(Jev, confidence " + p.DefensiveConfidence.ToString("0.00", LandFacts.Ci) + " of 1)</nobr>" : "") +
                    " · <nobr><color=#FF5C8A>for new demand " + LandFacts.Percent(1 - p.Defensive) + "</color></nobr> · AI " + LandFacts.Percent(p.Ai) +
                    "\n" + (p.Attention > 0 ? "<color=#FF5C8A>advertising " + LandFacts.Money(p.Attention) + "</color> · " : "") + (p.Deals > 0 ? "deals " + LandFacts.Money(p.Deals) + " · " : "") +
                    "<color=#FFB347>owners " + LandFacts.Money(p.Owners) + "</color> · taxes " + LandFacts.Money(p.State) +
                    (double.IsNaN(p.Lobbying) ? "" : " · <nobr>lobbying $" + p.Lobbying.ToString("0.0", LandFacts.Ci) + "M</nobr>") +
                    "\n<b>which way its money moves it</b> (Jev): " + D(dirs[0]) + " · " + D(dirs[1]) + " · " + D(dirs[2]) +
                    (string.IsNullOrEmpty(p.Strategy) ? "" : "\n\n<size=90%><i>What it says: " + p.Strategy + "</i></size>");
                FocusText = text;
                HistoryText = HistoryRows(p);
            }
            else FocusText = HistoryText = "";
            for (int k = named; k < trailLabels.Count; k++) trailLabels[k].text = "";
            Replace(focusRenderer, b);
            if (focusFill != null) { var filter = focusFill.GetComponent<MeshFilter>(); owned.Remove(filter.sharedMesh); Destroy(filter.sharedMesh); filter.sharedMesh = fill.ToMesh("Strategy focus lanes"); owned.Add(filter.sharedMesh); }
            else { focusFill = Surface(fill, Part.Focus); }
        }
        MeshRenderer focusFill;

        // ------------------------------------------------------------------ the trail behind the crest

        /// <summary>
        /// A firm's years behind the crest (<see cref="CorporatePrograms.Firm.History"/>) along the firm's lifeline (past the
        /// line's end at its tower, carried on at the tower's height): each year's investment and research as a lane over its
        /// calendar year's stretch of the time axis (the crest's year up to the crest), split into the fear lane (ice) and the
        /// desire lane (rose) by that year's judgment and as wide as the crest's lanes for the same dollars, so the years run
        /// as one ribbon into the crest, with a spine down its middle in the year's motive color (rose .. ice by the fear
        /// share) as many pixels thick as the square root of its dollars, which reads from afar; and its owners' payout as a
        /// gold stalk, as tall as the square root of its dollars, at the fiscal year's midpoint within that stretch. The
        /// focused firm's years carry a chevron for which way its money moved it.
        /// </summary>
        void Trail(LineMeshBuilder b, SurfaceMeshBuilder fill, int t, CorporatePrograms.Firm p, float alpha, bool full)
        {
            if (p.History.Length == 0) return;
            int life = LifeOf(t);
            foreach (var y in p.History)
            {
                if (!Stretch(y, out double from, out double to, out double mid)) continue;
                var path = new Vector3[9];
                for (int s = 0; s < path.Length; s++) path[s] = TrailAt(life, p, from + (to - from) * s / (path.Length - 1)) + Vector3.up * .2f;
                double future = y.Invest + y.Research;
                if (future > 0)
                {
                    Lanes(fill, b, path, (full ? .3f : .15f) + Mathf.Sqrt((float)future) * (full ? .16f : .09f), y.Defensive, alpha, t);
                    // the spine: the year's motive as one color (rose desire .. ice fear) and its dollars as pixels, so the
                    // trail reads from afar where the lanes are too narrow to split
                    var spine = new LinePoint[path.Length]; Color motive = WithAlpha(Color.Lerp(Rose, Ice, y.Defensive), Mathf.Min(1, alpha * 1.4f));
                    float px = (full ? 2 : 1) + Mathf.Sqrt((float)future) * (full ? .6f : .3f);
                    for (int s = 0; s < path.Length; s++) spine[s] = new LinePoint(path[s] + Vector3.up * .05f, motive, px, 0, C(full ? 2.2f : 1.4f));
                    b.AddFlowPath(spine, t, .5f, t * .29f);
                }
                Vector3 c = TrailAt(life, p, mid) + Vector3.up * .2f;
                if (y.Owners > 0) b.AddSegment(c, c + Vector3.up * Stalk(y, full), WithAlpha(Gold, alpha), full ? 3.2f : 1.8f, 0, 0, C(full ? 2.4f : 1.4f));
                if (full && y.Judged) Chevron(b, c + Vector3.up * .3f, y, y.Year);
            }
        }

        /// <summary>A history year's stretch of the time axis: its calendar year (less a small gap on each side, so the years
        /// read apart), cut at the crest; and the fiscal year's midpoint within it. False when nothing of it lies before the crest.</summary>
        bool Stretch(CorporatePrograms.Firm y, out double from, out double to, out double mid)
        {
            from = y.Year - .46; to = Math.Min(y.Year + .46, snapshot.Year);
            mid = Math.Max(from, Math.Min(y.Position, to));
            return to - from > .05;
        }

        /// <summary>The height of a trail year's gold stalk: the square root of its owners' dollars.</summary>
        static float Stalk(CorporatePrograms.Firm y, bool full) => Mathf.Sqrt((float)Math.Max(0, y.Owners)) * (full ? .75f : .4f);

        /// <summary>Names the focused trail's first year and every year whose main direction turned from the year before
        /// (its way, defensive and AI shares), above its stalk; returns how many names it used. The strategy view, the only one
        /// that shows them, sees a name as wide as several years of the trail, and a firm can turn in years running at stalks
        /// of one height: from the newest back, each name is raised a name's height at a time until it clears every newer name
        /// it would print over across that view's frame, and the firm's own name over its tower (<see cref="DrawFocus"/> sets it
        /// first), near which its newest years stand. That view runs a trail right to left into the present, and the largest
        /// investor's newest years stand at the HUD's title panel: the older names, to their right, are the ones raised.</summary>
        int NameTrail(CorporatePrograms.Firm p)
        {
            int used = 0, life = LifeOf(focused);
            for (int k = 0; k < p.History.Length; k++)
            {
                var y = p.History[k];
                bool turn = k > 0 && y.Judged && p.History[k - 1].Judged && y.Main != p.History[k - 1].Main;
                if (k > 0 && !turn) continue;
                while (trailLabels.Count <= used) trailLabels.Add(Label("", Vector3.zero, .3f, Color.Lerp(White, Steel, .15f), -1));
                if (!Stretch(y, out _, out _, out double mid)) continue;
                var label = trailLabels[used++];
                label.text = "<b>" + y.Year + "</b> " + (y.Judged ? CorporatePrograms.DirectionShort[y.Main] + "\ndefensive " + LandFacts.Percent(y.Defensive) + " · AI " + LandFacts.Percent(y.Ai) : "");
                label.transform.localPosition = TrailAt(life, p, mid) + Vector3.up * (Stalk(y, true) + 1.8f);
            }
            var frame = EconomyStage.Land(); var pose = ViewPresets.Get("strategy").Pose();
            Quaternion look = Quaternion.Inverse(frame.Rotation) * Quaternion.Euler(pose.Pitch, pose.Yaw, 0);
            Vector3 across = look * Vector3.right, up = look * Vector3.up;
            // a label's middle across and up that view's frame, its half width and its height (land units, with a little air).
            // The view looks down and back along the wake: a name some years back from the tower stands higher in the frame than
            // its height alone says, and a raise shows shortened by the pitch (up.y)
            (float x, float y, float half, float tall) Extent(TextMeshPro label)
            {
                Vector3 at = label.transform.localPosition; Vector2 size = label.GetPreferredValues(label.text) * label.transform.localScale.x;
                return (Vector3.Dot(at, across), Vector3.Dot(at, up), size.x * .5f + .3f, size.y + .1f);
            }
            var placed = new List<(float x, float y, float half, float tall)> { Extent(focusLabel) };
            for (int k = used - 1; k >= 0; k--)
            {
                var label = trailLabels[k]; var (x, y0, half, tall) = Extent(label); float y = y0;
                // a name raised to just over another can land a rounding error short of it: the test leaves that slack, or the
                // same name would be raised to the same height forever
                for (bool moved = true; moved;)
                {
                    moved = false;
                    foreach (var q in placed)
                        if (Mathf.Abs(x - q.x) < half + q.half && Mathf.Abs(y - q.y) < (tall + q.tall) * .5f * .999f) { y = q.y + (tall + q.tall) * .5f; moved = true; }
                }
                placed.Add((x, y, half, tall));
                label.transform.localPosition += Vector3.up * ((y - y0) / up.y);
            }
            return used;
        }

        /// <summary>A firm's place on its lifeline at a (fractional) year (<see cref="HillWaveLayer.CompanyAtYear"/>); without
        /// a line, above its industry's ridge.</summary>
        Vector3 TrailAt(int life, CorporatePrograms.Firm p, double year)
        {
            if (life >= 0 && HillWaveLayer.CompanyAtYear(life, year, out Vector3 at)) return at;
            float z = Mathf.Min(HillLandscape.Z(year), land.CrestZ - .4f);
            if (p.Industry < 0) return new Vector3(HillLandscape.FootX, 4, z);
            Vector2 ridge = land.RidgeAt(year, p.Industry);
            return new Vector3(ridge.x, land.RidgeTopAt(year, p.Industry) + 4, z);
        }

        /// <summary>The company life (<see cref="CompanyLives"/>) of a tower, or -1.</summary>
        int LifeOf(int t)
        {
            var lives = CompanyLives.Get(); var towers = snapshot.Land.Towers;
            return lives == null || t < 0 || t >= towers.Length ? -1 : lives.OfTower(towers[t].Ticker, towers[t].Name);
        }

        /// <summary>A small white chevron at a trail year, pointing the way Jev judged that year's money moved the firm, as
        /// long as the main direction's probability (spreading across: a ring).</summary>
        void Chevron(LineMeshBuilder b, Vector3 at, CorporatePrograms.Firm y, int year)
        {
            int d = y.Main; float w = y.Direction[d];
            Color c = WithAlpha(White, .45f + .5f * w); float width = 2f + 2.5f * w;
            Vector3 dir = Way(d, at, year);
            if (dir == Vector3.zero) { Ring(b, at, .5f + 1.2f * w, c, width, C(1.6f + w)); return; }
            dir = dir.normalized; Vector3 tip = at + dir * (1.2f + 2.8f * w);
            b.AddSegment(at, tip, c, width, 0, 0, C(1.6f + 2 * w));
            Vector3 side = Vector3.Cross(dir, Vector3.forward); if (side.sqrMagnitude < 1e-4f) side = Vector3.right; side.Normalize();
            b.AddSegment(tip, tip - dir * .8f + side * .5f, c, width, 0, 0, C(1.6f + 2 * w));
            b.AddSegment(tip, tip - dir * .8f - side * .5f, c, width, 0, 0, C(1.6f + 2 * w));
        }

        /// <summary>Which way a direction points from a place in a year: climbing toward the tech tier, reaching down toward
        /// raw materials, leaning down into the state's bedrock, harvesting up to the owners' cloud; zero for spreading
        /// across (drawn as a ring).</summary>
        Vector3 Way(int d, Vector3 at, int year)
        {
            switch ((CorporatePrograms.Direction)d)
            {
                case CorporatePrograms.Direction.Climb: { var tech = land.TierBand(year, 4); return new Vector3((tech.x + tech.y) * .5f - at.x, 18, 0); }
                case CorporatePrograms.Direction.IntegrateDown: { var raw = land.TierBand(year, 1); return new Vector3((raw.x + raw.y) * .5f - at.x, -18, 0); }
                case CorporatePrograms.Direction.LeanOnState: return Vector3.down;
                case CorporatePrograms.Direction.Harvest: return land.CloudCenter - at;
                default: return Vector3.zero;
            }
        }

        /// <summary>The focused firm's years as rows for the panel: investment and research, owners' payout, the defensive
        /// share of its investment, the AI share and the main way its money moved it, each judged on the year's numbers alone.</summary>
        static string HistoryRows(CorporatePrograms.Firm p)
        {
            if (p.History.Length == 0) return "";
            var h = p.History; var sb = new System.Text.StringBuilder();
            // "defensive" is the tightest header for its column: from 51% it ends about two letters (10 units at 10.5 pt) before
            // the AI column at 65%
            sb.Append("<b>THE TRAIL</b> · ").Append(h[0].Year).Append('-').Append(h[h.Length - 1].Year)
              .Append(" from its 10-Ks (SEC XBRL); Jev judged each year on its numbers alone\n")
              .Append("<color=#9AA8B8>year<pos=17%>invest + R&D<pos=39%>owners<pos=51%>defensive<pos=65%>AI<pos=74%>way</color>\n");
            foreach (var y in h)
            {
                sb.Append(y.Year);
                if (!string.IsNullOrEmpty(y.FiscalYear) && y.FiscalYear != "FY" + y.Year) sb.Append(" <size=75%>").Append(y.FiscalYear).Append("</size>");
                sb.Append("<pos=17%>").Append(LandFacts.Money(y.Invest + y.Research)).Append("<pos=39%><color=#FFB347>").Append(LandFacts.Money(y.Owners))
                  .Append("</color><pos=51%><color=#4FB8FF>").Append(y.Judged ? LandFacts.Percent(y.Defensive) : "-").Append("</color><pos=65%>")
                  .Append(y.Judged ? LandFacts.Percent(y.Ai) : "-").Append("<pos=74%>").Append(y.Judged ? CorporatePrograms.DirectionShort[y.Main] : "-").Append('\n');
            }
            var last = h[h.Length - 1];
            if (last.Judged && p.Judged && last.Main != p.Main)
                sb.Append("<size=85%><color=#9AA8B8>With its own words (above) Jev reads ").Append(last.Year).Append(" as ")
                  .Append(CorporatePrograms.DirectionShort[p.Main]).Append(", on the numbers alone as ").Append(CorporatePrograms.DirectionShort[last.Main]).Append(".</color></size>");
            return sb.ToString();
        }

        /// <summary>The AI build-out pouring down the hill: each hyperscaler's AI investment (its investment times Jev's AI
        /// share) to computers and chips and to data-center construction, split by the year's US totals for the two.</summary>
        void Cascade()
        {
            var data = LandService.Model.Data; var hardware = data.IndustryById("hardware"); var construction = data.IndustryById("construction");
            if (hardware == null || construction == null) return;
            double computers = programs.Total("privateFixedInvestment2025.computersAndPeripherals"), centers = programs.Total("dataCenterConstruction2025.annual2025");
            if (double.IsNaN(computers) || double.IsNaN(centers) || computers + centers <= 0) return;
            double toChips = computers / (computers + centers), total = 0;
            var b = new LineMeshBuilder(); var towers = snapshot.Land.Towers;
            for (int t = 0; t < towers.Length; t++)
            {
                var p = programs.OfTower(Tower(t)); if (p == null || Array.IndexOf(Hyperscalers, p.Ticker) < 0) continue;
                double ai = p.Invest * p.Ai; if (ai <= 0) continue; total += ai;
                Vector3 halo = HillActivityLayer.CompanyHalo(t);
                Arc(b, halo + Vector3.up * .6f, land.Market(hardware.Index) + Vector3.up * 1.2f, WithAlpha(Gold, .6f), .8f + Mathf.Sqrt((float)(ai * toChips)) * .45f, 1, 6);
                Arc(b, halo + Vector3.up * .6f, land.Market(construction.Index) + Vector3.up * 1.2f, WithAlpha(Gold, .5f), .8f + Mathf.Sqrt((float)(ai * (1 - toChips))) * .45f, 1, 6);
            }
            Lines(b, Part.Cascade, true, false).sharedMaterial.SetFloat("_FlowSpeed", .8f);
            if (total > 0)
                Label("<b>THE AI BUILD-OUT POURS DOWN THE HILL</b>\nthe hyperscalers' AI investment ≈ " + LandFacts.Money(total) + " (their investment × the AI share Jev judged)" +
                      "\n→ computers & chips " + LandFacts.Percent(toChips) + " · data-center buildings " + LandFacts.Percent(1 - toChips) + " (US 2025: computers $" + computers.ToString("0", LandFacts.Ci) +
                      "B, data centers $" + centers.ToString("0", LandFacts.Ci) + "B)", land.Market(hardware.Index) + new Vector3(0, 14, 2), .2f, Color.Lerp(Gold, White, .4f), -1);
        }

        /// <summary>The firms set against the US totals, floating above the summit.</summary>
        void Readout(double invest, double research, double owners, double ads, double deals, double taxes, double lobbying, double operations)
        {
            string M(double b) => double.IsNaN(b) ? "n/a" : LandFacts.Money(b);
            var h = programs.Hyperscalers();
            double nonres = programs.Total("privateFixedInvestment2025.nonresidential"), adsAll = programs.Total("totalAdSpend"), digital = programs.Total("digitalAdRevenue.total"),
                social = programs.Total("digitalAdRevenue.socialMedia"), buybacks = programs.Total("sp500Buybacks2025.ttmSep2025"), dividends = programs.Total("sp500Dividends2025.ttmSep2025"),
                lobbyAll = programs.Total("federalLobbying2025.total"), ma = programs.Total("usMergersAcquisitions2025");
            string text = "<b>COMPANIES SPEND TOO</b> · the " + programs.Firms.Length + " largest firms in their latest fiscal year" +
                "\nran their businesses on " + M(operations) + " · invested " + M(invest) + " + research " + M(research) + " · paid owners " + M(owners) +
                "\nadvertised " + M(ads) + " (disclosed) · bought " + M(deals) + " of companies and stakes · paid " + M(taxes) + " in cash taxes · lobbied $" + lobbying.ToString("0", LandFacts.Ci) + "M" +
                "\n<color=#9AA8B8>All US business in 2025: invested " + M(nonres) + " (BEA) · advertising " + M(adsAll) + ", digital " + M(digital) + ", social " + M(social) +
                " · S&P 500 buybacks " + M(buybacks) + " + dividends " + M(dividends) + " (12 months to Sep 2025) · lobbying " + M(lobbyAll) + " · M&A $" +
                (double.IsNaN(ma) ? "n/a" : ma.ToString("0.0", LandFacts.Ci) + "T") + "</color>" +
                "\n<color=#FFC04A>AI: the hyperscalers spent " + M(h.y2025) + " on capital in 2025 and guide " + M(h.y2026) + " for 2026</color>";
            ReadoutText = text;
        }

        // ------------------------------------------------------------------ plumbing

        static string Wrap(string s, int width, int lines)
        {
            var words = s.Split(' '); var sb = new System.Text.StringBuilder(); int col = 0, row = 1;
            foreach (var w in words)
            {
                if (col + w.Length > width) { if (row >= lines) { sb.Append('…'); break; } sb.Append('\n'); col = 0; row++; }
                sb.Append(w).Append(' '); col += w.Length + 1;
            }
            return sb.ToString().TrimEnd();
        }

        static void Arc(LineMeshBuilder b, Vector3 a, Vector3 z, Color color, float width, float intensity, float lift)
        {
            var pts = new LinePoint[25];
            for (int s = 0; s < pts.Length; s++) { float t = s / 24f; pts[s] = new LinePoint(Vector3.Lerp(a, z, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * lift, color, width, 0, intensity); }
            b.AddFlowPath(pts, 0, .5f);
        }

        void Lanes(SurfaceMeshBuilder fill, LineMeshBuilder b, Vector3[] path, float width, float fear, float alpha, int id)
        {
            float split = -width * .5f + width * fear;
            for (int lane = 0; lane < 2; lane++)
            {
                float e0 = lane == 0 ? -width * .5f : split + .02f, e1 = lane == 0 ? split - .02f : width * .5f;
                if (e1 <= e0) continue;
                Color hue = lane == 0 ? Ice : Rose; hue.a = alpha;
                var left = new Vector3[path.Length]; var right = new Vector3[path.Length]; var mid = new LinePoint[path.Length];
                for (int k = 0; k < path.Length; k++)
                {
                    Vector3 d = path[Math.Min(k + 1, path.Length - 1)] - path[Math.Max(0, k - 1)];
                    Vector3 normal = Vector3.Cross(d, Vector3.up); normal = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.right;
                    left[k] = path[k] + normal * e0; right[k] = path[k] + normal * e1;
                    mid[k] = new LinePoint((left[k] + right[k]) * .5f, hue, 1.2f, 0, C(2));
                }
                fill.AddBand(left, right, new[] { (Color32)hue }, 0, C(1.1f)); b.AddFlowPath(mid, 0, 1, id * .41f + lane * .2f);
            }
        }

        static void Ring(LineMeshBuilder b, Vector3 c, float r, Color color, float width, float intensity)
        {
            var pts = new Vector3[33];
            for (int s = 0; s < pts.Length; s++) { float a = s * Mathf.PI * 2 / 32; pts[s] = c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r); }
            b.AddPolyline(pts, color, width, 0, 0, intensity);
        }

        MeshRenderer Lines(LineMeshBuilder b, Part part, bool flow, bool additive)
        {
            Mesh mesh = b.ToMesh("Strategy " + part); owned.Add(mesh);
            Material material = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3236, additive, 0, flow ? Pulse : 0)); owned.Add(material);
            if (flow) { material.SetFloat("_FlowFreq", 1.6f); material.SetFloat("_FlowSpeed", .5f); }
            var r = AddMesh("Strategy " + part, mesh, material); r.transform.SetParent(content.transform, false);
            renderers.Add((r, part)); return r;
        }

        MeshRenderer Surface(SurfaceMeshBuilder b, Part part)
        {
            Mesh mesh = b.ToMesh("Strategy " + part); owned.Add(mesh);
            Material material = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1, 3234)); owned.Add(material);
            var r = AddMesh("Strategy " + part, mesh, material); r.transform.SetParent(content.transform, false);
            renderers.Add((r, part)); return r;
        }

        void Replace(MeshRenderer r, LineMeshBuilder b)
        {
            var filter = r.GetComponent<MeshFilter>(); Mesh old = filter.sharedMesh; owned.Remove(old); Destroy(old);
            filter.sharedMesh = b.ToMesh(r.name); owned.Add(filter.sharedMesh);
        }

        TextMeshPro Label(string text, Vector3 at, float size, Color color, int firm)
        {
            var go = new GameObject("Strategy label"); go.transform.SetParent(content.transform, false); go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>(); t.text = text; t.fontSize = 24; t.alignment = TextAlignmentOptions.Center;
            t.color = color; t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.sizeDelta = new Vector2(80, 8);
            go.transform.localScale = Vector3.one * size;
            if (!string.IsNullOrEmpty(text)) labels.Add((t, firm));
            return t;
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        void Clear()
        {
            foreach (var o in owned) if (o) Destroy(o);
            if (content) Destroy(content);
            owned.Clear(); renderers.Clear(); labels.Clear(); trailLabels.Clear(); content = null; focusRenderer = null; focusLabel = null; focusFill = null;
        }

        void OnDestroy() { Clear(); Focused = -1; FocusText = ReadoutText = HistoryText = ""; Ranked.Clear(); }
    }
}

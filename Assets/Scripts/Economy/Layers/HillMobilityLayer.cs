using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Why.Economy.Land;
using Why.Economy.Model;

namespace Why.Economy.Layers
{
    /// <summary>
    /// What moves people up or down the hill (view "mobility"; the model is <see cref="Mobility"/>):
    /// <list type="bullet">
    /// <item><b>The cohorts drift.</b> On the hills, every cohort of the shown year gets an arrow from where its adult
    /// members stood a decade before (their mean household income rank then, on the same hill, at that year's place on
    /// the wake) to where they stand now: gold where they climbed, rose where they fell, faint where they held. The
    /// largest moves are named with the event that sets the cohort apart (a lost job, retirement, found work ...).</item>
    /// <item><b>Who moved up or down.</b> Beside the wave, the hillside's altitude pulled out into a ladder of five rungs
    /// (household income fifths) at the window's start and at the crest, joined by streams of people: up in gold, down in
    /// rose, staying in steel; widths are people, and so are the streams' opacity and their cores, so the stayers (the
    /// largest flows) are not the faintest. By default the ladder counts like for like with the tax records: adults 25 and
    /// older, ranked among themselves at each end, with the US Treasury's panel drawn over it as dashed outlines; the
    /// mobility panel switches to every adult (<see cref="AllAdults"/>). Beside the bottom and top rungs, dot plots set
    /// the model against the records. Above the ladder what came with the climbs, beneath it what came with the falls,
    /// as bars of how many times more often the movers had each event than everyone of their age; at each rung, for the
    /// people the ladder counts, by the fifth they end in (the panel's members, or every adult at both ends), the share of
    /// income saved and earned by what people own (an arrow up only where that is positive), debt service (down) and the
    /// fear share of spending.</item>
    /// </list>
    /// Synthetic records (one line = 100,000 people), calibrated to national totals: the events co-occur with moves in
    /// the model; they are not causal estimates, and the view says so under both lists. Every mark is drawn at intensity 1
    /// and blended: the bloom starts above 1 and is kept for what the pointer holds, so the gold, rose and steel of the
    /// moves stay true where streams cross; sizes are widths and opacities.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillMobilityLayer : GraphLayer
    {
        public override int Order => 61;
        public const string BenchmarksPath = "Data/economy/mobility-benchmarks";
        public override IEnumerable<string> RequiredTexts => new[] { BenchmarksPath };
        /// <summary>The model drawn (null before the lives are ready); read by the "You" panel.</summary>
        public static Mobility Shown { get; private set; }
        /// <summary>The ladder's place beside the wave (land-local): its x plane, the bottom rung's height, the rung spacing.</summary>
        public const float LadderX = -80, LadderY = 4, RungStep = 7.5f;
        /// <summary>Where the ladder's middle stands (for the view's camera) once built.</summary>
        public static Vector3 LadderCenter { get; private set; } = new Vector3(LadderX, 20, -10);
        public static float LadderSpan { get; private set; } = 20;
        /// <summary>Whom the ladder counts. False (the default): like for like with the tax records, adults 25 and older at
        /// the window's start ranked among themselves at each end (<see cref="Mobility.PanelMoves"/>). True: every adult at
        /// both ends ranked among all adults (<see cref="Mobility.Moves"/>), where young adults climb as they leave home;
        /// the "You" panel's odds read this population, by age. The tour always shows the default.</summary>
        public static bool AllAdults;
        bool builtAll;
        enum Part { Drift, Ladder, Ribbons, Count }
        /// <summary>How far a flow's pulse brightens a line (1 = the shader's fourfold): a sheen, under the bloom.</summary>
        const float Pulse = .2f;
        static readonly Color Gold = EconomyStyle.Capital, Rose = EconomyStyle.Desire, Steel = EconomyStyle.Government,
            White = new Color(1, .96f, .90f), Ice = new Color(.45f, .82f, 1);
        readonly List<(Renderer renderer, Part part)> renderers = new List<(Renderer, Part)>();
        /// <summary>Labels, the cohort each names (-1: none) and whether it shows without a selection.</summary>
        readonly List<(TextMeshPro text, Part part, int player, bool named)> labels = new List<(TextMeshPro, Part, int, bool)>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        LandSnapshot snapshot; HillLandscape land; GameObject content;
        int selection = -2;

        string benchmarksJson;
        public override void Prepare(GraphContext ctx) { benchmarksJson = ctx.Text(BenchmarksPath); }
        public override void Upload(GraphContext ctx) { EconomyStage.Land().Place(transform); }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (HillLandscapeLayer.Current == null || LandService.Current == null || LandService.Model?.Lives == null) return;
            if (HillLandscapeLayer.Current.Source != LandService.Current) return;
            bool all = Counting();
            if (snapshot != LandService.Current || land != HillLandscapeLayer.Current || all != builtAll)
            {
                if (!HillRebuilds.Take()) return;
                Build();
            }
            if (content == null) return;
            string view = LandView.PresetId;
            bool on = view == "mobility" || view == "you" || view == "people";
            content.SetActive(on); if (!on) return;
            selection = EconomyState.SelectedPlayer >= 0 ? EconomyState.SelectedPlayer : HillLandscapeLayer.HoverPlayer;
            foreach (var e in renderers) if (e.renderer) GraphMaterials.SetAlpha(e.renderer.sharedMaterial, Emphasis(e.part, view));
            foreach (var e in labels)
            {
                // the ladder's labels in its view; a cohort's while it is selected or hovered, else only the largest moves
                bool show = e.part == Part.Drift ? (view == "people" || view == "mobility") && (selection >= 0 ? e.player == selection : e.named) : view == "mobility";
                e.text.gameObject.SetActive(show); if (show) e.text.transform.rotation = rig.Cam.transform.rotation;
            }
        }

        float Emphasis(Part part, string view)
        {
            // the people view (class is altitude) carries the cohorts' drift on the hills; the ladder is the mobility view's
            if (view == "people") return part == Part.Drift ? .9f : 0;
            if (view == "you") return part == Part.Drift ? .25f : .12f;
            return part == Part.Ribbons ? .9f : 1;
        }

        /// <summary>Whether the ladder counts every adult now (the tour shows like for like whatever was chosen).</summary>
        static bool Counting() => AllAdults && !(GraphRoot.Instance != null && GraphRoot.Instance.TourActive);

        void Build()
        {
            // switching whom the ladder counts redraws it from the same model
            bool same = Shown != null && snapshot == LandService.Current && land == HillLandscapeLayer.Current;
            Clear();
            snapshot = LandService.Current; land = HillLandscapeLayer.Current; builtAll = Counting();
            var model = same ? Shown : Mobility.Build(LandService.Model.Lives, snapshot.Players, snapshot.Year);
            Shown = model;
            if (model == null || model.Lines == 0) return;
            content = new GameObject("Mobility"); content.transform.SetParent(transform, false);
            Drift(model); Ladder(model);
            if (same) return;
            Debug.Log("[Why] Mobility " + model.Log);
            foreach (string problem in model.Problems()) Debug.LogError("[Why] Mobility check failed: " + problem);
        }

        // ------------------------------------------------------------------ the cohorts drift on their hills

        void Drift(Mobility m)
        {
            var b = new LineMeshBuilder(); var players = snapshot.Players.Players;
            float z0 = HillLandscape.Z(m.From), z1 = HillLandscape.Z(m.Year);
            var ranked = new List<(float weight, int player)>();
            for (int p = 0; p < players.Length && p < m.Drifts.Length; p++)
            {
                if (land.Cloud[p]) continue;
                var d = m.Drifts[p];
                if (d.Tracked < 3) continue;
                int hill = land.IndustryOf[p]; bool owner = players[p].Group == Group.Owners;
                Vector3 then = land.Rung(m.From, hill, d.RankThen, owner, z0, .6f), now = land.Rung(m.Year, hill, d.RankNow, owner, z1, .6f);
                float delta = d.RankNow - d.RankThen;
                Color c = delta > .02f ? Gold : delta < -.02f ? Rose : Steel;
                float alpha = Mathf.Abs(delta) > .02f ? .85f : .35f;
                float width = .8f + Mathf.Sqrt(d.Members) * .32f;
                var pts = new LinePoint[17];
                for (int s = 0; s < pts.Length; s++)
                {
                    float t = s / 16f; Vector3 q = Vector3.Lerp(then, now, t);
                    q.y = Mathf.Max(land.Ground(q.x, q.z) + .5f, Mathf.Lerp(then.y, now.y, t)) + Mathf.Sin(t * Mathf.PI) * (1 + 6 * Mathf.Abs(delta));
                    pts[s] = new LinePoint(q, WithAlpha(c, alpha * Mathf.Lerp(.35f, 1, t)), width);
                }
                b.AddFlowPath(pts, 0, .35f, p * .23f);
                // the arrow's head points along the last step
                Vector3 dir = (pts[16].Data - pts[13].Data); dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
                Vector3 side = Vector3.Cross(dir, Vector3.up); side = side.sqrMagnitude > 1e-6f ? side.normalized : Vector3.right;
                b.AddSegment(now, now - dir * 1.1f + side * .55f, WithAlpha(c, alpha), width, 0, 0);
                b.AddSegment(now, now - dir * 1.1f - side * .55f, WithAlpha(c, alpha), width, 0, 0);
                ranked.Add((d.Members * Mathf.Abs(delta), p));
            }
            Lines(b, Part.Drift, true);
            // Name the largest moves; every cohort's label shows while it is selected.
            ranked.Sort((a, c) => c.weight.CompareTo(a.weight));
            for (int k = 0; k < ranked.Count; k++)
            {
                int p = ranked[k].player; var d = m.Drifts[p];
                int hill = land.IndustryOf[p]; bool owner = players[p].Group == Group.Owners;
                Vector3 now = land.Rung(m.Year, hill, d.RankNow, owner, z1, .6f);
                float delta = d.RankNow - d.RankThen;
                string why = d.Cause >= 0 ? "\n" + LandFacts.Percent(d.CauseShare) + " " + Mobility.CauseNames[d.Cause] + " (x" + d.CauseLift.ToString("0.0", LandFacts.Ci) + " the average)" : "";
                string text = "<b>" + HillLandscapeLayer.PlayerTitle(players[p]) + "</b>\nincome rank " + LandFacts.Percent(d.RankThen) + " → " + LandFacts.Percent(d.RankNow) +
                    " (" + (delta >= 0 ? "+" : "−") + (100 * Mathf.Abs(delta)).ToString("0", LandFacts.Ci) + " pts since " + m.From + ")" + why;
                Label(text, now + Vector3.up * 2.2f, .16f, Color.Lerp(delta > .02f ? Gold : delta < -.02f ? Rose : Steel, White, .45f), Part.Drift, p, k < 8);
            }
        }

        // ------------------------------------------------------------------ the decade's ladder beside the wave

        void Ladder(Mobility m)
        {
            float z0 = HillLandscape.Z(m.From), z1 = HillLandscape.Z(m.Year) - .5f, span = z1 - z0;
            LadderCenter = new Vector3(LadderX, LadderY + RungStep * 2 + 2, (z0 + z1) * .5f); LadderSpan = Mathf.Abs(span);
            var fill = new SurfaceMeshBuilder(); var edges = new LineMeshBuilder(); var bars = new LineMeshBuilder(); var outlines = new LineMeshBuilder();
            const int Q = Mobility.Quintiles;
            // whom it counts: like for like with the tax records (adults 25+ ranked among themselves), or every adult
            bool all = builtAll; var moves = all ? m.Moves : m.PanelMoves;
            double Out(int a) { double s = 0; for (int c = 0; c < Q; c++) s += moves[a, c]; return s; }
            double In(int c) { double s = 0; for (int a = 0; a < Q; a++) s += moves[a, c]; return s; }
            double Share(int a, int c) { double s = Out(a); return s > 0 ? moves[a, c] / s : 0; }
            double counted = 0, cell = 1; foreach (double v in moves) { counted += v; cell = Math.Max(cell, v); }
            double biggest = 1; for (int q = 0; q < Q; q++) biggest = Math.Max(biggest, Math.Max(Out(q), In(q)));
            float perPerson = (RungStep * .62f) / (float)biggest;
            float Y(int q) => LadderY + q * RungStep;
            // Each rung is a node at both ends: its stack of people, centered on the rung's altitude.
            var outTop = new float[Q]; var inTop = new float[Q];
            for (int q = 0; q < Q; q++) { outTop[q] = Y(q) + (float)Out(q) * perPerson * .5f; inTop[q] = Y(q) + (float)In(q) * perPerson * .5f; }
            // Streams leave each start node top-down by destination (highest first) and enter each end node top-down by origin.
            var leave = (float[])outTop.Clone(); var enter = (float[])inTop.Clone();
            for (int a = Q - 1; a >= 0; a--)
                for (int c = Q - 1; c >= 0; c--)
                {
                    double people = moves[a, c]; if (people <= 0) continue;
                    float h = (float)people * perPerson;
                    float a0 = leave[a], a1 = a0 - h; leave[a] = a1;
                    float c0 = enter[c], c1 = c0 - h;
                    enter[c] = c1;
                    Color col = c > a ? Gold : c < a ? Rose : Steel;
                    // opacity and the core follow the people in the stream, so the stayers (the largest flows) are not the faintest
                    float size = (float)(people / cell), alpha = .16f + .44f * size, core = Mathf.Sqrt(size);
                    var low = new Vector3[25]; var high = new Vector3[25]; var mid = new LinePoint[25];
                    for (int s = 0; s < 25; s++)
                    {
                        float t = s / 24f, e = t * t * (3 - 2 * t), z = z0 + span * t;
                        float top = Mathf.Lerp(a0, c0, e), bottom = Mathf.Lerp(a1, c1, e);
                        high[s] = new Vector3(LadderX, top, z); low[s] = new Vector3(LadderX, bottom, z);
                        mid[s] = new LinePoint(new Vector3(LadderX - .05f, (top + bottom) * .5f, z), WithAlpha(col, .3f + .45f * core), .4f + 1.4f * core);
                    }
                    fill.AddBand(low, high, new[] { (Color32)WithAlpha(col, alpha) }, 0);
                    edges.AddPolyline(high, WithAlpha(col, Mathf.Min(1, alpha * 1.4f)), .8f, 0, 0);
                    edges.AddFlowPath(mid, 0, .5f, (a * 5 + c) * .37f);
                }
            // Like for like, the tax records' own decade stands on the same rungs as hollow outlines of its streams.
            if (!all) Records(outlines, Out, perPerson, Y, z0, z1);
            // The rungs themselves: a bright bar at each end for the people in it, a faint rail between.
            for (int q = 0; q < Q; q++)
            {
                float h0 = (float)Out(q) * perPerson, h1 = (float)In(q) * perPerson;
                bars.AddSegment(new Vector3(LadderX, Y(q) - h0 * .5f, z0), new Vector3(LadderX, Y(q) + h0 * .5f, z0), WithAlpha(White, .9f), 4, 0, 0);
                bars.AddSegment(new Vector3(LadderX, Y(q) - h1 * .5f, z1), new Vector3(LadderX, Y(q) + h1 * .5f, z1), WithAlpha(White, .95f), 5, 0, 0);
                // the forces of the people this ladder counts, by the fifth they end in: the panel's members, or every adult at both ends
                var rung = all ? m.Rungs[q] : m.PanelRungs[q];
                double stay = Share(q, q), climbedFrom = 0, fellFrom = 0;
                for (int c = 0; c < Q; c++) { if (c > q) climbedFrom += Share(q, c); if (c < q) fellFrom += Share(q, c); }
                string head = "<b>" + Mobility.QuintileNames[q].ToUpperInvariant() + "</b>";
                string went = "of those here in " + m.From + ": " + (q < Q - 1 ? "climbed " + LandFacts.Percent(climbedFrom) + " · " : "") + "stayed " + LandFacts.Percent(stay) + (q > 0 ? " · fell " + LandFacts.Percent(fellFrom) : "");
                string saves = rung.SavingRate >= 0 ? "saves " + LandFacts.Percent(rung.SavingRate) + " of income" : "spends " + LandFacts.Percent(1 - rung.SavingRate) + " of income (borrows, draws down)";
                string forces = saves + " · " + LandFacts.Percent(rung.OwnedShare) + " earned by what they own · debt service " + LandFacts.Percent(rung.DebtService) +
                    "\nfear " + LandFacts.Percent(rung.Fear) + " of spending · runway " + rung.Runway.ToString("0.0", LandFacts.Ci) + " yr · in control " + LandFacts.Percent(rung.InControl);
                Label(head + "  " + went + "\n" + forces, new Vector3(LadderX - .4f, Y(q), z1 + 1.6f), .24f, Color.Lerp(White, Steel, .2f), Part.Ladder, -1, true, TextAlignmentOptions.Left, new Vector2(80, 6));
                // At the rung: what people save and earn by owning (up) and pay in debt service (down), as shares of income;
                // no arrow up where the sum is negative (a fifth that spends more than its income).
                float lift = (float)(rung.SavingRate + rung.OwnedShare) * 14, pull = (float)rung.DebtService * 14;
                Vector3 at = new Vector3(LadderX - .3f, Y(q), z1 + .6f);
                if (lift > .05f) Arrow(bars, at, at + Vector3.up * Mathf.Max(.3f, lift), Gold, 3.2f);
                if (pull > .05f) Arrow(bars, at, at - Vector3.up * Mathf.Max(.3f, pull), Rose, 3.2f);
            }
            // Years at the two ends, just under the bottom rung's stack and the tip of its debt-service arrow (a quarter of
            // income at most in the record), so the replay's readout at the foot of the screen does not cover them. Each stands
            // inside its end, flush with it: the start's clear of the records' last line, the year's clear of the arrow and of the
            // bottom fifth's dot plot beside it. And the title.
            Label("<b>" + m.From + "</b>", new Vector3(LadderX, LadderY - RungStep * .6f, z0), .3f, Ice, Part.Ladder, -1, true, TextAlignmentOptions.Left);
            Label("<b>" + m.Year + "</b>", new Vector3(LadderX, LadderY - RungStep * .6f, z1), .3f, Ice, Part.Ladder, -1, true, TextAlignmentOptions.Right);
            string who = all ? "who were adults in both " + m.From + " and " + m.Year + ", ranked among all adults (young adults climb as they leave home)"
                : "aged 25 and older in " + m.From + ", ranked among themselves as the tax records rank people";
            Label("<b>WHO MOVED UP OR DOWN, AND WHAT CAME WITH IT</b> · " + m.From + " → " + m.Year +
                "\nSIMULATED ADULTS, calibrated to national totals: " + (counted / 1e6).ToString("0", LandFacts.Ci) + "M " + who +
                "\nfive rungs of household income · streams are people: gold climbed · rose fell · steel stayed" + (all ? "" : " · dashed outlines: US tax records, 1996-2005") +
                "\nat each rung, shares of income: <color=#FFB347>↑ saved or earned by what they own</color> · <color=#FF5C8A>↓ paid in debt service</color>",
                new Vector3(LadderX, Y(Q - 1) + RungStep * 1.35f, (z0 + z1) * .5f + 4), .3f, White, Part.Ladder, -1);
            // What came with the moves stands on the past's side of the ladder: the climbs by the upper rungs, the falls by the lower.
            Causes(m, true, new Vector3(LadderX, Y(Q - 1) + RungStep * .2f, z0 - 2.5f));
            Causes(m, false, new Vector3(LadderX, Y(1) + RungStep * .2f, z0 - 2.5f));
            // the records under the falls (five lines, the longest citation wrapped), centered here and lifted as they grew: the
            // last ends about where the start's year beside it begins, so the block stays clear of the replay's readout at the
            // foot of the screen
            Benchmarks(m, all, Y(0), Y(Q - 1), z1 + 1.6f, new Vector3(LadderX, Y(0) - RungStep * .32f, z0 - 2.5f));
            Surface(fill, Part.Ribbons);
            Lines(edges, Part.Ribbons, true);
            Lines(outlines, Part.Ladder, false);
            Lines(bars, Part.Ladder, false);
        }

        /// <summary>The US Treasury's closed panel (taxpayers 25+, 1996-2005, fifths ranked within the panel: the records
        /// the like-for-like ladder matches) as dashed hollow outlines of its streams, stacked on the same rungs as the
        /// model's, so each stream can be read against its record.</summary>
        void Records(LineMeshBuilder b, Func<int, double> rowPeople, float perPerson, Func<int, float> Y, float z0, float z1)
        {
            var matrix = Matrix("treasury2007Table2"); if (matrix == null) return;
            const int Q = Mobility.Quintiles; float span = z1 - z0;
            var leave = new float[Q]; var enter = new float[Q]; var rows = new double[Q, Q];
            for (int a = 0; a < Q; a++) for (int c = 0; c < Q; c++) rows[a, c] = rowPeople(a) * matrix[a][c] / 100;
            for (int q = 0; q < Q; q++)
            {
                double inflow = 0; for (int a = 0; a < Q; a++) inflow += rows[a, q];
                leave[q] = Y(q) + (float)rowPeople(q) * perPerson * .5f; enter[q] = Y(q) + (float)inflow * perPerson * .5f;
            }
            Color line = WithAlpha(White, .5f);
            for (int a = Q - 1; a >= 0; a--)
                for (int c = Q - 1; c >= 0; c--)
                {
                    float h = (float)rows[a, c] * perPerson; if (h <= 0) continue;
                    float a0 = leave[a], a1 = a0 - h; leave[a] = a1;
                    float c0 = enter[c], c1 = c0 - h; enter[c] = c1;
                    foreach (var (from, to) in new[] { (a0, c0), (a1, c1) })
                        for (int s = 0; s < 24; s += 2)
                        {
                            float t0 = s / 24f, t1 = (s + 1) / 24f, e0 = t0 * t0 * (3 - 2 * t0), e1 = t1 * t1 * (3 - 2 * t1);
                            b.AddSegment(new Vector3(LadderX - .1f, Mathf.Lerp(from, to, e0), z0 + span * t0), new Vector3(LadderX - .1f, Mathf.Lerp(from, to, e1), z0 + span * t1), line, 1, 0, 0, 1);
                        }
                }
        }

        /// <summary>A published decade matrix from mobility-benchmarks.json (rows: the fifth at the start, percent ending in
        /// each fifth), or null.</summary>
        double[][] Matrix(string id)
        {
            if (string.IsNullOrEmpty(benchmarksJson)) return null;
            try
            {
                foreach (var row in Newtonsoft.Json.Linq.JObject.Parse(benchmarksJson)["decade"] ?? new Newtonsoft.Json.Linq.JArray())
                    if ((string)row["id"] == id && row["matrix"] is Newtonsoft.Json.Linq.JArray rows && rows.Count == Mobility.Quintiles)
                    {
                        var m = new double[Mobility.Quintiles][];
                        for (int a = 0; a < m.Length; a++) m[a] = rows[a].ToObject<double[]>();
                        return m;
                    }
            }
            catch { }
            return null;
        }

        /// <summary>What came with the climbs or the falls of prime-age adults, on the past's side of the ladder: a bar per
        /// leading event as long as its lift (how many times as often the movers had it as all adults of their age), ending
        /// at the ladder, with a dashed line at once (as often); the share of movers who had it is in words, and under the
        /// list the reminder that these are associations in the simulation, not causes.</summary>
        void Causes(Mobility m, bool up, Vector3 at)
        {
            var b = new LineMeshBuilder(); var list = m.Leading(up, 1.3, .04, 5);
            list.Sort((x, y) => m.Lift(y, up).CompareTo(m.Lift(x, up)));
            Color c = up ? Gold : Rose; const float PerTimes = 4, Row = 1.7f;
            double moved = (up ? m.PrimeUp : m.PrimeDown) / (double)Math.Max(1, m.PrimeLines);
            string title = (up ? "<b>WHAT CAME WITH THE CLIMBS</b>" : "<b>WHAT CAME WITH THE FALLS</b>") + "\nadults 25-54 in " + m.From + ": " + LandFacts.Percent(moved) +
                (up ? " climbed" : " fell") + " a fifth or more\nbars: how many times as often as all adults 25-54";
            // at .24 the population line reads about as easily as the captions it qualifies (at .26 the falls' title would reach
            // the edge of tour 13's card)
            Label(title, at + Vector3.up * 2.6f, .24f, Color.Lerp(c, White, .35f), Part.Ladder, -1, true, TextAlignmentOptions.Right, new Vector2(80, 6));
            float yTop = at.y + .9f, yBottom = at.y - Math.Max(0, list.Count - 1) * Row - .9f;
            if (list.Count > 0)
            {
                for (float y = yBottom; y < yTop; y += .5f)
                    b.AddSegment(new Vector3(at.x - .05f, y, at.z - PerTimes), new Vector3(at.x - .05f, Mathf.Min(yTop, y + .25f), at.z - PerTimes), WithAlpha(White, .6f), 1.2f, 0, 0, 1);
                Label("1× as often", new Vector3(at.x, yBottom - .45f, at.z - PerTimes), .16f, Color.Lerp(Steel, White, .4f), Part.Ladder, -1);
            }
            for (int k = 0; k < list.Count; k++)
            {
                var cause = list[k]; float lift = (float)m.Lift(cause, up), share = (float)m.ShareWith(cause, up), y = at.y - k * Row;
                Vector3 end = new Vector3(at.x, y, at.z), start = end - Vector3.forward * (PerTimes * lift);
                b.AddSegment(start, end, WithAlpha(c, .9f), 7, 0, 0);
                // at .26 about 10 px to the em from the view's distance (at .19, captions of 6 px capitals blurred); they grow
                // left, away from the ladder, into the empty side of the view (the longest of any decade, 1960-70's ×3.4 debt
                // discharged, still ends about half a unit inside the portrait frame)
                Label("×" + lift.ToString("0.0", LandFacts.Ci) + " " + Mobility.CauseNames[(int)cause] + " · " + LandFacts.Percent(share) + (up ? " of those who climbed" : " of those who fell"),
                    start - Vector3.forward * .6f, .26f, Color.Lerp(c, White, .5f), Part.Ladder, -1, true, TextAlignmentOptions.Right, new Vector2(80, 5));
            }
            // as large as the captions: the caveat qualifies them, so it reads as easily
            if (list.Count == 0) Label("no event stands out", at, .26f, Steel, Part.Ladder, -1, true, TextAlignmentOptions.Right, new Vector2(60, 5));
            Label("associations in the simulation, not causes", new Vector3(at.x, yBottom - 1.3f, at.z), .26f, Color.Lerp(c, White, .6f), Part.Ladder, -1, true, TextAlignmentOptions.Right, new Vector2(60, 5));
            Lines(b, Part.Ladder, false);
        }

        /// <summary>The model against the records (mobility-benchmarks.json) as dot plots beside the rungs they are about,
        /// in the gaps between the rungs' labels: the share of the top fifth still there a decade later (under the top
        /// rung's label, clear of the year's readout at the top right of the screen), of the bottom fifth that reached the
        /// top (just above the bottom rung) and of the bottom fifth still there (below it). Each sets the model like for
        /// like with the records (adults 25+, ranked among themselves) against the US Treasury's tax-record panel and a
        /// family survey (PSID), with every adult in the model for contrast; and what event studies measure for the falls.</summary>
        void Benchmarks(Mobility m, bool all, float bottomRung, float topRung, float z, Vector3 events)
        {
            if (string.IsNullOrEmpty(benchmarksJson)) return;
            Newtonsoft.Json.Linq.JObject root;
            try { root = Newtonsoft.Json.Linq.JObject.Parse(benchmarksJson); } catch { return; }
            double Cell(string id, string key)
            {
                foreach (var row in root["decade"] ?? new Newtonsoft.Json.Linq.JArray())
                    if ((string)row["id"] == id && row[key] != null && row[key].Type != Newtonsoft.Json.Linq.JTokenType.Null) return (double)row[key] / 100;
                return double.NaN;
            }
            const string Tax = "treasury2007Table2", Survey = "psidHungerford1990s";
            var b = new LineMeshBuilder();
            // each plot (from its "0" to the top of its name, about 4.5 units) centered in its gap between the rungs' labels
            Dots(b, "STAYED IN THE TOP FIFTH", m.PanelShare(4, 4), m.Share(4, 4), Cell(Tax, "topStay"), Cell(Survey, "topStay"), topRung - RungStep * .68f, z, all);
            Dots(b, "ROSE FROM BOTTOM TO TOP", m.PanelShare(0, 4), m.Share(0, 4), Cell(Tax, "bottomToTop"), Cell(Survey, "bottomToTop"), bottomRung + RungStep * .32f, z, all);
            Dots(b, "STAYED IN THE BOTTOM FIFTH", m.PanelShare(0, 0), m.Share(0, 0), Cell(Tax, "bottomStay"), Cell(Survey, "bottomStay"), bottomRung - RungStep * .7f, z, all);
            Lines(b, Part.Ladder, false);
            var ev = root["events"];
            // as large as the captions of the bars above (at .17 the citations' figures did not resolve), and wrapped 101 wide
            // (about 26 units): unwrapped, the longest citation ran about 7 units off the left of the portrait frame, which shows
            // 35 units either side of the view's target. Only that one breaks; the next longest needs 99.
            if (ev != null)
                Label("<b>IN THE RECORDS</b>\n" + (string)ev["jobLoss"]?["label"] + "\n" + (string)ev["divorce"]?["label"] + "\n" + (string)ev["health"]?["label"],
                    events, .26f, new Color(.80f, .70f, .76f), Part.Ladder, -1, true, TextAlignmentOptions.Right, new Vector2(101, 8)).textWrappingMode = TextWrappingModes.Normal;
        }

        /// <summary>One dot plot on a 0-100% axis running away from the ladder (toward the future): the model like for like
        /// (filled), the tax records (ring), the survey (diamond) and the model counting every adult (cross), under its name
        /// and two lines giving each mark's value, all at scale .4 (about 14 px from the view's distance at 1920x1080: twice
        /// the 7 px the old .2 block measured). Each line is shorter than the 18-unit axis (at most about 17 units, the name
        /// 16), so all of it fits the portrait frame too, which shows about 20 units right of where the plots start.</summary>
        void Dots(LineMeshBuilder b, string what, double model, double everyone, double tax, double survey, float y, float z0, bool all)
        {
            const float Length = 18, Mark = .32f;
            float Z(double share) => z0 + Length * Mathf.Clamp01((float)share);
            Color axis = WithAlpha(Steel, .55f), marks = new Color(.92f, .95f, 1f), records = Color.Lerp(Steel, White, .55f), faint = WithAlpha(Steel, .9f);
            b.AddSegment(new Vector3(LadderX - .4f, y, z0), new Vector3(LadderX - .4f, y, z0 + Length), axis, 1.2f, 0, 0, 1);
            foreach (float t in new[] { 0f, .5f, 1f }) b.AddSegment(new Vector3(LadderX - .4f, y - .25f, z0 + Length * t), new Vector3(LadderX - .4f, y + .25f, z0 + Length * t), axis, 1.2f, 0, 0, 1);
            float x = LadderX - .5f;
            if (!double.IsNaN(tax)) Circle(b, new Vector3(x, y, Z(tax)), Mark, records, 1.6f);
            if (!double.IsNaN(survey))
            {
                Vector3 c = new Vector3(x, y, Z(survey));
                Vector3[] d = { c + Vector3.up * Mark * 1.2f, c + Vector3.forward * Mark * 1.2f, c - Vector3.up * Mark * 1.2f, c - Vector3.forward * Mark * 1.2f, c + Vector3.up * Mark * 1.2f };
                b.AddPolyline(d, records, 1.6f, 0, 0, 1);
            }
            Vector3 e = new Vector3(x, y, Z(everyone));
            b.AddSegment(e + new Vector3(0, Mark, -Mark), e + new Vector3(0, -Mark, Mark), faint, 1.4f, 0, 0, 1);
            b.AddSegment(e + new Vector3(0, Mark, Mark), e + new Vector3(0, -Mark, -Mark), faint, 1.4f, 0, 0, 1);
            Vector3 dot = new Vector3(x - .05f, y, Z(model));
            for (float r = .07f; r <= Mark; r += .07f) Circle(b, dot, r, marks, 2.4f);
            string Pct(double v) => double.IsNaN(v) ? "n/a" : LandFacts.Percent(v);
            Label("<b>" + what + "</b>", new Vector3(LadderX - .4f, y + 3.2f, z0), .4f, new Color(.84f, .89f, .95f), Part.Ladder, -1, true, TextAlignmentOptions.Left, new Vector2(100, 6));
            // the values in two lines (at y + 2.1 and y + 1), each shorter than the axis
            Label("● model " + Pct(model) + (all ? "" : " (drawn)") + "  ○ tax records " + Pct(tax) +
                  "\n◊ survey " + Pct(survey) + "  <color=#8D9BB0>× all adults " + Pct(everyone) + (all ? " (drawn)" : "") + "</color>",
                  new Vector3(LadderX - .4f, y + 1.55f, z0), .4f, new Color(.84f, .89f, .95f), Part.Ladder, -1, true, TextAlignmentOptions.Left, new Vector2(100, 6));
            Label("0", new Vector3(LadderX - .4f, y - .75f, z0), .2f, axis, Part.Ladder, -1);
            Label("100%", new Vector3(LadderX - .4f, y - .75f, z0 + Length), .2f, axis, Part.Ladder, -1);
        }

        static void Circle(LineMeshBuilder b, Vector3 c, float r, Color color, float width)
        {
            var pts = new Vector3[25];
            for (int s = 0; s < pts.Length; s++) { float a = s * Mathf.PI * 2 / 24; pts[s] = c + new Vector3(0, Mathf.Sin(a) * r, Mathf.Cos(a) * r); }
            b.AddPolyline(pts, color, width, 0, 0, 1);
        }

        // ------------------------------------------------------------------ plumbing

        static void Arrow(LineMeshBuilder b, Vector3 a, Vector3 z, Color c, float width)
        {
            b.AddSegment(a, z, c, width, 0, 0);
            Vector3 d = (z - a).normalized * .45f;
            b.AddSegment(z, z - d + Vector3.forward * .3f, c, width, 0, 0);
            b.AddSegment(z, z - d - Vector3.forward * .3f, c, width, 0, 0);
        }

        /// <summary>A mesh of lines, blended: where streams cross, the top one's hue shows instead of a sum turning white.</summary>
        void Lines(LineMeshBuilder b, Part part, bool flow)
        {
            Mesh mesh = b.ToMesh("Mobility " + part); owned.Add(mesh);
            Material material = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3226, false, 0, flow ? Pulse : 0)); owned.Add(material);
            if (flow) { material.SetFloat("_FlowFreq", 1.6f); material.SetFloat("_FlowSpeed", .45f); }
            var r = AddMesh("Mobility " + part, mesh, material); r.transform.SetParent(content.transform, false);
            renderers.Add((r, part));
        }

        void Surface(SurfaceMeshBuilder b, Part part)
        {
            Mesh mesh = b.ToMesh("Mobility " + part); owned.Add(mesh);
            Material material = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1, 3224)); owned.Add(material);
            var r = AddMesh("Mobility " + part, mesh, material); r.transform.SetParent(content.transform, false);
            renderers.Add((r, part));
        }

        TextMeshPro Label(string text, Vector3 at, float size, Color color, Part part, int player, bool named = true,
            TextAlignmentOptions align = TextAlignmentOptions.Center, Vector2? box = null)
        {
            var go = new GameObject("Mobility label"); go.transform.SetParent(content.transform, false); go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>(); t.text = text; t.fontSize = 24; t.alignment = align;
            t.color = color; t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.sizeDelta = box ?? new Vector2(40, 6);
            if (align == TextAlignmentOptions.Left) t.rectTransform.pivot = new Vector2(0, .5f);
            else if (align == TextAlignmentOptions.Right) t.rectTransform.pivot = new Vector2(1, .5f);
            go.transform.localScale = Vector3.one * size; labels.Add((t, part, player, named)); return t;
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        void Clear()
        {
            foreach (var o in owned) if (o) Destroy(o);
            if (content) Destroy(content);
            owned.Clear(); renderers.Clear(); labels.Clear(); content = null;
        }

        void OnDestroy() { Clear(); Shown = null; }
    }
}

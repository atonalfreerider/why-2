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
    /// <item><b>The decade's ladder.</b> Beside the wave, the hillside's altitude pulled out into a ladder of five rungs
    /// (household income fifths) at the window's start and at the crest, joined by streams of people: up in gold, down in
    /// rose, staying in steel; widths are people. Above it what came with the climbs, beneath it what came with the
    /// falls (the share of movers who had each event and how much more often than everyone); at each rung its forces:
    /// the share of income saved and the share earned by what people own (lifting), debt service (pulling down) and the
    /// fear share of spending.</item>
    /// </list>
    /// Synthetic records (one line = 100,000 people), calibrated to national totals: the events co-occur with moves in
    /// the model; they are not causal estimates.
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
        enum Part { Drift, Ladder, Ribbons, Count }
        static readonly Color Gold = new Color(1, .70f, .22f), Rose = new Color(1, .26f, .48f), Steel = new Color(.58f, .68f, .80f),
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
            if (snapshot != LandService.Current || land != HillLandscapeLayer.Current) Build();
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

        void Build()
        {
            Clear();
            snapshot = LandService.Current; land = HillLandscapeLayer.Current;
            var lives = LandService.Model.Lives;
            var model = Mobility.Build(lives, snapshot.Players, snapshot.Year);
            Shown = model;
            if (model == null || model.Lines == 0) return;
            content = new GameObject("Mobility"); content.transform.SetParent(transform, false);
            Drift(model); Ladder(model);
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
                    pts[s] = new LinePoint(q, WithAlpha(c, alpha * Mathf.Lerp(.35f, 1, t)), width, 0, 1.4f + 2.6f * Mathf.Abs(delta) * 4);
                }
                b.AddFlowPath(pts, 0, .35f, p * .23f);
                // the arrow's head points along the last step
                Vector3 dir = (pts[16].Data - pts[13].Data); dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
                Vector3 side = Vector3.Cross(dir, Vector3.up); side = side.sqrMagnitude > 1e-6f ? side.normalized : Vector3.right;
                b.AddSegment(now, now - dir * 1.1f + side * .55f, WithAlpha(c, alpha), width, 0, 0, 2.4f);
                b.AddSegment(now, now - dir * 1.1f - side * .55f, WithAlpha(c, alpha), width, 0, 0, 2.4f);
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
            var fill = new SurfaceMeshBuilder(); var edges = new LineMeshBuilder(); var bars = new LineMeshBuilder();
            const int Q = Mobility.Quintiles;
            double biggest = 1; for (int q = 0; q < Q; q++) biggest = Math.Max(biggest, Math.Max(m.StartedIn(q), m.EndedIn(q)));
            float perPerson = (RungStep * .62f) / (float)biggest;
            float Y(int q) => LadderY + q * RungStep;
            // Each rung is a node at both ends: its stack of people, centered on the rung's altitude.
            var outTop = new float[Q]; var inTop = new float[Q];
            for (int q = 0; q < Q; q++) { outTop[q] = Y(q) + (float)m.StartedIn(q) * perPerson * .5f; inTop[q] = Y(q) + (float)m.EndedIn(q) * perPerson * .5f; }
            // Streams leave each start node top-down by destination (highest first) and enter each end node top-down by origin.
            var leave = (float[])outTop.Clone(); var enter = (float[])inTop.Clone();
            for (int a = Q - 1; a >= 0; a--)
                for (int c = Q - 1; c >= 0; c--)
                {
                    double people = m.Moves[a, c]; if (people <= 0) continue;
                    float h = (float)people * perPerson;
                    float a0 = leave[a], a1 = a0 - h; leave[a] = a1;
                    float c0 = enter[c], c1 = c0 - h;
                    enter[c] = c1;
                    Color col = c > a ? Gold : c < a ? Rose : Steel;
                    float alpha = c == a ? .16f : Mathf.Lerp(.30f, .55f, Mathf.Clamp01(Mathf.Abs(c - a) / 2f));
                    var low = new Vector3[25]; var high = new Vector3[25]; var mid = new LinePoint[25];
                    for (int s = 0; s < 25; s++)
                    {
                        float t = s / 24f, e = t * t * (3 - 2 * t), z = z0 + span * t;
                        float top = Mathf.Lerp(a0, c0, e), bottom = Mathf.Lerp(a1, c1, e);
                        high[s] = new Vector3(LadderX, top, z); low[s] = new Vector3(LadderX, bottom, z);
                        mid[s] = new LinePoint(new Vector3(LadderX - .05f, (top + bottom) * .5f, z), WithAlpha(col, c == a ? .25f : .7f), c == a ? .8f : 1.2f, 0, c == a ? 1 : 2.2f);
                    }
                    fill.AddBand(low, high, new[] { (Color32)WithAlpha(col, alpha) }, 0, 1.2f);
                    edges.AddPolyline(high, WithAlpha(col, alpha * 1.4f), .8f, 0, 0, 1.4f);
                    if (c != a) edges.AddFlowPath(mid, 0, .5f, (a * 5 + c) * .37f);
                }
            // The rungs themselves: a bright bar at each end for the people in it, a faint rail between.
            for (int q = 0; q < Q; q++)
            {
                float h0 = (float)m.StartedIn(q) * perPerson, h1 = (float)m.EndedIn(q) * perPerson;
                bars.AddSegment(new Vector3(LadderX, Y(q) - h0 * .5f, z0), new Vector3(LadderX, Y(q) + h0 * .5f, z0), WithAlpha(White, .9f), 4, 0, 0, 2.4f);
                bars.AddSegment(new Vector3(LadderX, Y(q) - h1 * .5f, z1), new Vector3(LadderX, Y(q) + h1 * .5f, z1), WithAlpha(White, .95f), 5, 0, 0, 3.2f);
                var rung = m.Rungs[q];
                double stay = m.Share(q, q), climbedFrom = 0, fellFrom = 0;
                for (int c = 0; c < Q; c++) { if (c > q) climbedFrom += m.Share(q, c); if (c < q) fellFrom += m.Share(q, c); }
                string head = "<b>" + Mobility.QuintileNames[q].ToUpperInvariant() + "</b>";
                string moves = "of those here in " + m.From + ": " + (q < Q - 1 ? "climbed " + LandFacts.Percent(climbedFrom) + " · " : "") + "stayed " + LandFacts.Percent(stay) + (q > 0 ? " · fell " + LandFacts.Percent(fellFrom) : "");
                string saves = rung.SavingRate >= 0 ? "saves " + LandFacts.Percent(rung.SavingRate) + " of income" : "spends " + LandFacts.Percent(1 - rung.SavingRate) + " of income (borrows, draws down)";
                string forces = saves + " · " + LandFacts.Percent(rung.OwnedShare) + " earned by what they own · debt service " + LandFacts.Percent(rung.DebtService) +
                    "\nfear " + LandFacts.Percent(rung.Fear) + " of spending · runway " + rung.Runway.ToString("0.0", LandFacts.Ci) + " yr · in control " + LandFacts.Percent(rung.InControl);
                Label(head + "  " + moves + "\n" + forces, new Vector3(LadderX - .4f, Y(q), z1 + 1.6f), .24f, Color.Lerp(White, Steel, .2f), Part.Ladder, -1, true, TextAlignmentOptions.Left, new Vector2(80, 6));
                // Forces at the rung: lifting (saving + owned income, gold, up) and pulling (debt service, rose, down).
                float lift = (float)Math.Max(0, rung.SavingRate + rung.OwnedShare) * 14, pull = (float)Math.Max(0, rung.DebtService) * 14;
                Vector3 at = new Vector3(LadderX - .3f, Y(q), z1 + .6f);
                Arrow(bars, at, at + Vector3.up * Mathf.Max(.3f, lift), Gold, 3.2f, 3.6f);
                Arrow(bars, at, at - Vector3.up * Mathf.Max(.3f, pull), Rose, 3.2f, 3.2f);
            }
            // Years at the two ends, and the title.
            Label("<b>" + m.From + "</b>", new Vector3(LadderX, LadderY - RungStep * .9f, z0), .3f, Ice, Part.Ladder, -1);
            Label("<b>" + m.Year + "</b>", new Vector3(LadderX, LadderY - RungStep * .9f, z1), .3f, Ice, Part.Ladder, -1);
            Label("<b>THE DECADE'S LADDER</b> · " + (m.People / 1e6).ToString("0", LandFacts.Ci) + "M adults alive in " + m.From + " and " + m.Year +
                "\nthe hill's altitude pulled out: five rungs of household income · streams are people · gold climbed · rose fell · steel stayed" +
                "\nat each rung: <color=#FFB347>↑ saving and income from ownership lift</color> · <color=#FF5C8A>↓ debt service pulls down</color>",
                new Vector3(LadderX, Y(Q - 1) + RungStep * 1.35f, (z0 + z1) * .5f + 4), .3f, White, Part.Ladder, -1);
            // What came with the moves stands on the past's side of the ladder: the climbs by the upper rungs, the falls by the lower.
            Causes(m, true, new Vector3(LadderX, Y(Q - 1) + RungStep * .2f, z0 - 2.5f));
            Causes(m, false, new Vector3(LadderX, Y(1) + RungStep * .2f, z0 - 2.5f));
            Benchmarks(m, new Vector3(LadderX - .4f, LadderY - RungStep * .95f, z1 + 1.6f), new Vector3(LadderX, Y(0) - RungStep * .55f, z0 - 2.5f));
            Surface(fill, Part.Ribbons);
            Lines(edges, Part.Ribbons, true);
            Lines(bars, Part.Ladder, false);
        }

        /// <summary>What came with the climbs or the falls of prime-age adults, on the past's side of the ladder: a bar per
        /// leading event as long as the share of movers who had it, ending at the ladder, and its lift.</summary>
        void Causes(Mobility m, bool up, Vector3 at)
        {
            var b = new LineMeshBuilder(); var list = m.Leading(up, 1.3, .04, 5);
            Color c = up ? Gold : Rose; const float Length = 12;
            double moved = (up ? m.PrimeUp : m.PrimeDown) / (double)Math.Max(1, m.PrimeLines);
            string title = (up ? "<b>WHAT CAME WITH THE CLIMBS</b>" : "<b>WHAT CAME WITH THE FALLS</b>") + "\nadults 25-54 in " + m.From + ": " + LandFacts.Percent(moved) +
                (up ? " climbed" : " fell") + " a fifth or more";
            Label(title, at + Vector3.up * 2.2f, .22f, Color.Lerp(c, White, .35f), Part.Ladder, -1, true, TextAlignmentOptions.Right, new Vector2(80, 6));
            for (int k = 0; k < list.Count; k++)
            {
                var cause = list[k]; float share = (float)m.ShareWith(cause, up), y = at.y - k * 1.7f;
                Vector3 end = new Vector3(at.x, y, at.z), start = end - Vector3.forward * (Length * share);
                b.AddSegment(start, end, WithAlpha(c, .9f), 7, 0, 0, 2.6f);
                Label(LandFacts.Percent(share) + " " + Mobility.CauseNames[(int)cause] + " · x" + m.Lift(cause, up).ToString("0.0", LandFacts.Ci) + " the rate of all",
                    start - Vector3.forward * .6f, .19f, Color.Lerp(c, White, .5f), Part.Ladder, -1, true, TextAlignmentOptions.Right, new Vector2(80, 5));
            }
            if (list.Count == 0) Label("no event stands out", at, .18f, Steel, Part.Ladder, -1, true, TextAlignmentOptions.Right, new Vector2(60, 5));
            Lines(b, Part.Ladder, false);
        }

        /// <summary>The model against the data (mobility-benchmarks.json): the decade's stickiness at the bottom and the top
        /// beside the published matrices (the Treasury's closed panel is the closest analogue: adults 25+, fifths ranked
        /// within the panel), and what event studies measure for the falls the model shows.</summary>
        void Benchmarks(Mobility m, Vector3 at, Vector3 events)
        {
            if (string.IsNullOrEmpty(benchmarksJson)) return;
            Newtonsoft.Json.Linq.JObject root;
            try { root = Newtonsoft.Json.Linq.JObject.Parse(benchmarksJson); } catch { return; }
            string Cell(string id, string key)
            {
                foreach (var row in root["decade"] ?? new Newtonsoft.Json.Linq.JArray())
                    if ((string)row["id"] == id && row[key] != null && row[key].Type != Newtonsoft.Json.Linq.JTokenType.Null) return ((double)row[key]).ToString("0", LandFacts.Ci) + "%";
                return "n/a";
            }
            string text = "<b>THE MODEL AGAINST THE DATA</b> · share of a fifth that stayed over a decade" +
                "\nbottom fifth: model " + LandFacts.Percent(m.Share(0, 0)) + " (adults 25+, ranked within the panel: " + LandFacts.Percent(m.PanelShare(0, 0)) +
                ") · US Treasury 1996-2005 " + Cell("treasury2007Table2", "bottomStay") + " · PSID 1990s " + Cell("psidHungerford1990s", "bottomStay") +
                "\ntop fifth: model " + LandFacts.Percent(m.Share(4, 4)) + " (" + LandFacts.Percent(m.PanelShare(4, 4)) + ") · Treasury " + Cell("treasury2007Table2", "topStay") +
                " · PSID " + Cell("psidHungerford1990s", "topStay") +
                "\nbottom to top: model " + LandFacts.Percent(m.Share(0, 4)) + " (" + LandFacts.Percent(m.PanelShare(0, 4)) + ") · Treasury " + Cell("treasury2007Table2", "bottomToTop") +
                "\n<color=#9AA8B8>like for like (adults 25+, fifths ranked within the panel) the model holds the Treasury's stickiness; across all adults the bottom\nlooks more fluid because young adults rise as they leave home and start work</color>";
            Label(text, at, .2f, new Color(.80f, .86f, .93f), Part.Ladder, -1, true, TextAlignmentOptions.Left, new Vector2(110, 8));
            var ev = root["events"];
            if (ev != null)
                Label("<b>IN THE RECORDS</b>\n" + (string)ev["jobLoss"]?["label"] + "\n" + (string)ev["divorce"]?["label"] + "\n" + (string)ev["health"]?["label"],
                    events, .17f, new Color(.80f, .70f, .76f), Part.Ladder, -1, true, TextAlignmentOptions.Right, new Vector2(140, 8));
        }

        // ------------------------------------------------------------------ plumbing

        static void Arrow(LineMeshBuilder b, Vector3 a, Vector3 z, Color c, float width, float intensity)
        {
            b.AddSegment(a, z, c, width, 0, 0, intensity);
            Vector3 d = (z - a).normalized * .45f;
            b.AddSegment(z, z - d + Vector3.forward * .3f, c, width, 0, 0, intensity);
            b.AddSegment(z, z - d - Vector3.forward * .3f, c, width, 0, 0, intensity);
        }

        void Lines(LineMeshBuilder b, Part part, bool flow)
        {
            Mesh mesh = b.ToMesh("Mobility " + part); owned.Add(mesh);
            Material material = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3226, true, 0, flow ? 1 : 0)); owned.Add(material);
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

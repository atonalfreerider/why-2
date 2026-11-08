using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Why.Economy.Emphasis;
using Why.Economy.Land;
using Why.Economy.Model;
using Roles = Why.Economy.EconomyStyle.Roles;

namespace Why.Economy.Layers
{
    /// <summary>
    /// The government's key moments as the topology the economy has to adapt to (the user's seventh direction, verbatim in
    /// Docs/research/advantage/brief-v5.md; Docs/hills-design/POLITICS.md, "Moments as topology"). In the landscape view
    /// ("Layers of the economy", where each industry is a ridge through time as wide as its share of value added) each key
    /// moment up to the year shown stands on the land at its date (<see cref="HillLandscape.Z"/>; the year's own and the last
    /// year's drawn in behind the crest, <see cref="MomentZ"/>) as a thin translucent fence
    /// across the ridge of every industry it touched with a score of <see cref="MinScore"/> or more:
    /// <list type="bullet">
    /// <item>as wide as the ridge stands at that date, where the industry's own relief rises above the strata
    /// (<see cref="HillLandscape.Bump"/>, the slices the surface itself is drawn from there);</item>
    /// <item>its foot on the surface the ribs draw (<see cref="HillLandscape.Ground"/>), following it across the ridge;</item>
    /// <item>as tall as <see cref="HeightPerPoint"/> times the score, in the moment's kind colour
    /// (<see cref="HillPoliticsLayer.KindColor"/>): a faint fill, pickets and a rail along its top.</item>
    /// </list>
    /// <para>The scores are Jev's (TypeSafe's model): how directly the moment changed the rules, costs, demand or competition
    /// the industry faced in the United States in the five years after it, 0-4 (<see cref="Politics.Touch"/>;
    /// Tools/politics/touches.py, Docs/research/politics/touches.json). They are judgments, not measurements: a rail is dashed
    /// where Jev's answer was uncertain (confidence under <see cref="Uncertain"/>, the history view's walls' rule). The government's own
    /// two industries (federal; state and local) lie in the bedrock with no ridge, so their touches are not fenced (the
    /// politics panel lists them), nor are the moments before the land's first rib (1947: Bretton Woods).</para>
    /// <para>The past fades as the politics view's does (<see cref="FadeYears"/>, <see cref="PastAlpha"/>), so the land reads
    /// first; the fences never change the land. A moment of the last <see cref="RecentYears"/> years is named above its
    /// tallest fence ("2022 · CHIPS and Science Act"): role labels the label system places by rank and culls where they would
    /// crowd; older moments are named in the politics view and its panel, which list every touch. (Naming every moment with
    /// a touch of 3.5 or more as well set 20 names over 2025's land, whose plates hid the wave's own company names.) Nothing moves: a fence is a standing rule, not a flow,
    /// and nothing in the data travels along it, so it carries no packet lane (a marching lane on each of 2025's 124 fences
    /// would pull the eye off the land).</para>
    /// <para>Drawn in the landscape view only (the emphasis table's moments rows; its names only there), and built there, a
    /// rebuild under <see cref="HillRebuilds"/>, whenever the land or the politics data changed. politics.json is parsed by
    /// <see cref="HillPoliticsLayer"/> (its Prepare), which this layer only reads (<see cref="Politics.Get"/>). In the smoke
    /// test a check after every capture of the landscape logs what it drew and fails a year whose fences are missing, and
    /// <c>-smokeextra moments</c> captures the landscape at 1985 and 2025.</para>
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillMomentsLayer : GraphLayer
    {
        /// <summary>The view the fences stand in: "Layers of the economy", whose ridges they stand on.</summary>
        public const string View = "landscape";

        /// <summary>The smoke check after every capture of the view, and the sequence <c>-smokeextra moments</c>.</summary>
        public const string CheckName = "moments", Sequence = "moments";

        /// <summary>Ticks after the land's layer (55), whose land it reads in the same frame, and the politics layer (67), whose
        /// Prepare parses politics.json (this layer's Prepare does nothing, so their shared tier of workers does not matter).</summary>
        public override int Order => 68;

        /// <summary>
        /// The least score a touch is fenced at: Jev's level 2, "a clear but secondary effect: the moment itself changed the
        /// rules, costs, demand or competition" of the industry "more than it changed most industries'". Below it (1: only at
        /// one remove, through a minor input or another industry's business; 0: not at all, or only through the economy as a
        /// whole) the moment did not change the industry's own ground. The politics panel lists the same touches.
        /// </summary>
        public const float MinScore = 2;

        /// <summary>The moments of this many years before the year shown are named, whatever their scores: the politics view
        /// names its latest moments over the same span (its NamedYears).</summary>
        const float RecentYears = 3;

        /// <summary>
        /// World units of fence per point of Jev's score. The tallest (4) stands 9 units, a little over the median ridge's own
        /// relief over the strata at the latest year (about 6.8 units; 5.2 in 1985's smaller economy), so a strong rule stands
        /// out of the hills it fences from the landscape's pose (6.8 px a unit at 1080 lines, about 190 units away): at 1.5 a
        /// unit the first frames showed the fences only as faint strokes; a 2 now stands 4.4 units, about 30 px at the crest.
        /// </summary>
        public const float HeightPerPoint = 2.2f;

        /// <summary>
        /// The past fades as the politics view's does (its private FadeYears and PastAlpha, the same values): a fence this many
        /// years before the middle of the year shown draws at <see cref="PastAlpha"/> of its alpha, eased from the whole at the
        /// year shown, so the wake's old rules stay faint behind the land.
        /// </summary>
        const float FadeYears = 20, PastAlpha = .25f;

        /// <summary>A fence's alphas at the year shown (times its fade): a light fill, so the ridge and its company lines read
        /// through it (0.12 did not read at the landscape's distance); pickets; a rail along its top.</summary>
        const float FillAlpha = .24f, PicketAlpha = .55f, RailAlpha = 1f;

        /// <summary>Line widths (px): the pickets hairlines, the rail a little heavier than the wave's ridge lines (1.2).</summary>
        const float PicketPx = 1, RailPx = 1.5f;

        /// <summary>
        /// Units between pickets across a ridge (two samples of the surface between them): 10 px apart at the crest from the
        /// landscape's pose, 7 px in 1985's wake, so a fence reads as posts and a rail rather than as one more rib.
        /// </summary>
        const float PicketStep = 1.5f;

        /// <summary>A fence's foot stands this far above the surface, as the wave's ridge lines do (.05; the ribs .03), so it does
        /// not flicker against the rib of its year.</summary>
        const float Lift = .05f;

        /// <summary>Never past the crest: a moment of the year shown stands on the crest's own hills, as the land model keeps
        /// everything of the present this far behind the crest (HillLandscape.OnHill, At, ValleyFloor).</summary>
        const float CrestClear = .4f;

        /// <summary>
        /// The years before the year shown whose moments, with the year shown's own, are drawn in together toward the crest
        /// (<see cref="MomentZ"/>): the crest stands at the year's start, so the year's moments (and the last weeks of the year
        /// before) would all stand past it, and clamped there they would stand in one row; drawn in, they keep their order.
        /// </summary>
        const float CrestLead = 1;

        /// <summary>Jev's confidence under which a judgment is uncertain (the history view's walls, the judge's report): the rail
        /// is dashed, half drawn.</summary>
        const float Uncertain = .15f;

        /// <summary>Where a ridge is searched for: this many samples across the reach of its slices around the fence's year, each edge
        /// then found to a thousandth of a sample by halving; the least relief (world units) that counts as standing.</summary>
        const int ScanSteps = 64, EdgeSteps = 10;

        const float Relief = .001f;

        /// <summary>
        /// A name stands this far over its tallest fence's rail (world units) and this many pixels above its point; where that
        /// is taken, further up in steps of 18 px, four at most (the politics view's steps), then the label system's candidate
        /// places with a leader, else it is culled.
        /// </summary>
        const float NameLift = .6f, NameOffsetPx = 12, NameStepPx = 18;

        /// <summary>
        /// A name's rank among the view's names (higher placed first): a point of Jev's score outranks 25 years, so the recent
        /// rules aimed squarely at an industry come first and old landmarks keep their places over the lesser recent ones.
        /// </summary>
        const float NameRank = 200, RankPerPoint = 25;

        /// <summary>An old moment's name keeps this much of its ink at least, so the wake's names read under the present's.</summary>
        const float OldNameAlpha = .7f;

        /// <summary>Render queues: the fill after the wave's and the land's surfaces (3194, 3195), the lines after their lines
        /// (3204, 3205), so the rails draw over the ridge lines they cross.</summary>
        const int FillQueue = 3196, LineQueue = 3207;

        /// <summary>The years <c>-smokeextra moments</c> captures, the wait for a year's land, and the settling after a focus (the
        /// smoke test's own, 2.6 s).</summary>
        static readonly int[] SmokeYears = { 1985, 2025 };

        const float LandWaitSeconds = 30, SettleSeconds = 2.6f;

        enum Part
        {
            Fences,
            Rails
        }

        /// <summary>The emphasis keys (EmphasisTable's moments rows: the landscape view only): the walls (fill and pickets) and
        /// their rails; the meshes carry their highlight ids.</summary>
        static readonly int[] Keys = EconomyEmphasis.Keys("moments", "fences", "rails");

        static float Id(Part part) => EconomyEmphasis.Id(Keys[(int)part]);

        static readonly bool SmokeRun = Array.IndexOf(Environment.GetCommandLineArgs(), "-smoketest") >= 0;
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        GameObject content;
        HillLandscape builtLand;
        Politics builtModel;
        bool labelsShown;
        readonly List<(MeshRenderer renderer, Part part)> renderers = new List<(MeshRenderer, Part)>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly List<HillLabel> labels = new List<HillLabel>();
        readonly List<Vector3> bottom = new List<Vector3>(64), top = new List<Vector3>(64);
        readonly Color32[] fillColor = new Color32[1];
        readonly HashSet<int> ridges = new HashSet<int>();
        readonly List<(LabelSpec spec, Rect letters, float em)> shownLabels = new List<(LabelSpec, Rect, float)>();

        /// <summary>What the last build drew and left out: fences, the moments they belong to, the touches of
        /// <see cref="MinScore"/> or more it should draw (on the land, up to the year, with a ridge), those on the government's
        /// own industries, the moments before the land's first rib, and touches on an industry the land does not have.</summary>
        int fences, moments, fenceable, government, early, unknown;

        // ------------------------------------------------------------------ life cycle

        public override void Prepare(GraphContext ctx) { }

        public override void Upload(GraphContext ctx)
        {
            EconomyStage.Land().Place(transform);
            if (!SmokeRun) return;
            SmokeChecks.Register(CheckName, CheckView);
            SmokeChecks.RegisterSequence(Sequence, Smoke());
        }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            HillLandscape land = HillLandscapeLayer.Current;
            Politics model = Politics.Get();
            bool here = LandView.PresetId == View;
            // built only where it is drawn: a year changed in another view waits until the landscape is shown again (entered,
            // the old fences stand at the emphasis eased from 0 for the frame or two before their turn). Until its turn to
            // rebuild (HillRebuilds), as every hills layer, it keeps what it drew: the replay's years do not blink
            if (here && (content == null || land != builtLand || model != builtModel) && land != null && model != null &&
                LandService.Model?.Data != null && HillRebuilds.Take())
                Build(land, model);
            if (content != null)
                foreach ((MeshRenderer r, Part part) in renderers) EconomyEmphasis.Apply(r, Keys[(int)part]);
            ShowLabels(here && content != null);
        }

        void OnDestroy()
        {
            Clear();
            if (labelsShown) HillLabels.Clear(this);
            labelsShown = false;
        }

        // ------------------------------------------------------------------ the build

        void Build(HillLandscape land, Politics model)
        {
            Clear();
            builtLand = land;
            builtModel = model;
            content = new GameObject("Key moments");
            content.transform.SetParent(transform, false);
            var data = LandService.Model.Data;
            var fill = new SurfaceMeshBuilder();
            var pickets = new LineMeshBuilder(2048);
            var rails = new LineMeshBuilder(1024);
            // the middle of the year shown, where the politics view measures its past from
            float now = land.Year + .5f;
            fences = moments = fenceable = government = early = unknown = 0;
            ridges.Clear();
            labels.Clear();
            foreach (Politics.Moment m in model.Moments)
            {
                // up to the year shown: its own moments too, which stand on the crest's hills
                if (float.IsNaN(m.Date) || m.Date >= land.Year + 1) continue;
                bool before = m.Date < land.FirstYear, named = land.Year - m.Date <= RecentYears;
                float z = MomentZ(land, m.Date), fade = Fade(now - m.Date), tallest = -1;
                Color kind = HillPoliticsLayer.KindColor(m.Kind);
                Vector3 peak = default;
                foreach (Politics.Touch t in m.Touches)
                {
                    // NaN-safe: a score that is not a number is not fenced
                    if (!(t.Score >= MinScore)) continue;
                    var industry = data.IndustryById(t.Industry);
                    if (industry == null || industry.Index < 0 || industry.Index >= land.Hills.Length)
                    {
                        unknown++;
                        continue;
                    }

                    if (land.Hills[industry.Index].Tier == 0)
                    {
                        government++;
                        continue;
                    }

                    if (before) continue;
                    fenceable++;
                    if (!Span(land, industry.Index, z, out float x0, out float x1)) continue;
                    Vector3 rail = Fence(fill, pickets, rails, land, x0, x1, z, t.Score * HeightPerPoint, kind, fade, t.Confidence < Uncertain);
                    fences++;
                    ridges.Add(industry.Index);
                    if (t.Score <= tallest) continue;
                    tallest = t.Score;
                    peak = rail;
                }

                if (before && tallest < 0 && HasFenceable(m, data, land)) early++;
                if (tallest < 0) continue;
                moments++;
                if (named) labels.Add(Name(m, peak, tallest, now - m.Date, fade));
            }

            Surface(fill, Part.Fences);
            Lines(pickets, Part.Fences);
            Lines(rails, Part.Rails);
            if (labelsShown) HillLabels.Set(this, labels);
            Debug.Log($"[Why] Moments {land.Year.ToString(Ci)}: {Summary()}; {labels.Count} names set");
        }

        /// <summary>Whether a moment has a touch the land could fence (before the land's first rib, where it is left out).</summary>
        static bool HasFenceable(Politics.Moment m, Data.EconomyData data, HillLandscape land)
        {
            foreach (Politics.Touch t in m.Touches)
            {
                if (!(t.Score >= MinScore)) continue;
                var industry = data.IndustryById(t.Industry);
                if (industry != null && industry.Index >= 0 && industry.Index < land.Hills.Length && land.Hills[industry.Index].Tier > 0) return true;
            }

            return false;
        }

        /// <summary>How much of its alpha a moment this many years before the middle of the year shown keeps (the politics
        /// view's Age).</summary>
        static float Fade(float age) => Mathf.Lerp(1, PastAlpha, Mathf.SmoothStep(0, 1, age / FadeYears));

        /// <summary>
        /// Where a moment stands along time: at its date (<see cref="HillLandscape.Z"/>), except the moments from
        /// <see cref="CrestLead"/> years before the year shown to its end, which are drawn in, in their order, between where
        /// that span starts and <see cref="CrestClear"/> behind the crest. At their dates the year's own moments would stand
        /// past the crest (it stands at the year's start), in the future, and clamped there they would share one place (2022's
        /// Fed tightening, CHIPS and the IRA on Construction's ridge as one fence).
        /// </summary>
        static float MomentZ(HillLandscape land, float date)
        {
            float z = HillLandscape.Z(date), edge = land.CrestZ - CrestClear, from = HillLandscape.Z(land.Year - CrestLead),
                to = HillLandscape.Z(land.Year + 1);
            if (z <= from || !(edge > from) || !(to > from)) return Mathf.Min(z, edge);
            return from + (edge - from) * Mathf.Clamp01((z - from) / (to - from));
        }

        /// <summary>
        /// Where an industry's ridge stands across the land at <paramref name="z"/>: from where its own relief first rises above
        /// the strata to where it last does (<see cref="HillLandscape.Bump"/>), so the fence spans the ridge the surface draws
        /// there, from the same slices (the crest's own over the last few years before it). Searched within the reach of the
        /// industry's slices around z's year and at the crest (its band's centre plus or minus its ridge's half width). False
        /// where it has no ridge.
        /// </summary>
        static bool Span(HillLandscape land, int industry, float z, out float x0, out float x1)
        {
            x0 = x1 = 0;
            // the year the surface reads at z (a moment drawn in toward the crest stands a little before its date)
            int year = Mathf.FloorToInt((float)HillLandscape.YearAt(z));
            float lo = float.MaxValue, hi = float.MinValue;
            // the crest's slice only where the surface holds it (its last few years): an old moment's search stays on the ridge's
            // reach of its own years, decades of the hillside's widening away from the crest's
            bool crest = z >= land.CrestZ - HillLandscape.CrestDepth;
            for (int k = 0; k < (crest ? 3 : 2); k++)
            {
                int y = k == 0 ? year : k == 1 ? year + 1 : land.Year;
                Vector2 band = land.IndustryBand(y, industry), ridge = land.IndustryRidge(y, industry);
                lo = Mathf.Min(lo, band.x - ridge.x);
                hi = Mathf.Max(hi, band.x + ridge.x);
            }

            // a unit clear of the reach on both sides: the scan starts and ends where no relief stands
            lo -= 1;
            hi += 1;
            if (!(hi > lo)) return false;
            float step = (hi - lo) / ScanSteps;
            int first = -1, last = -1;
            for (int s = 0; s <= ScanSteps; s++)
            {
                if (land.Bump(industry, lo + s * step, z) <= Relief) continue;
                if (first < 0) first = s;
                last = s;
            }

            if (first < 0) return false;
            x0 = Edge(land, industry, z, lo + first * step, lo + Mathf.Max(0, first - 1) * step);
            x1 = Edge(land, industry, z, lo + last * step, lo + Mathf.Min(ScanSteps, last + 1) * step);
            return x1 > x0;
        }

        /// <summary>The edge of a ridge between a point where it stands and one where it does not, found by halving.</summary>
        static float Edge(HillLandscape land, int industry, float z, float inside, float outside)
        {
            for (int k = 0; k < EdgeSteps; k++)
            {
                float mid = (inside + outside) * .5f;
                if (land.Bump(industry, mid, z) > Relief) inside = mid;
                else outside = mid;
            }

            return inside;
        }

        /// <summary>
        /// One fence across a ridge at <paramref name="z"/>, from <paramref name="x0"/> to <paramref name="x1"/>: its foot on the
        /// surface, sampled twice between pickets, a faint fill up to its rail <paramref name="height"/> above, pickets at both
        /// ends and every <see cref="PicketStep"/> between, and the rail, dashed (every other sample drawn) where the judgment is
        /// uncertain. Returns the rail's highest point, where the moment's name stands.
        /// </summary>
        Vector3 Fence(SurfaceMeshBuilder fill, LineMeshBuilder pickets, LineMeshBuilder rails, HillLandscape land, float x0, float x1, float z,
            float height, Color kind, float fade, bool uncertain)
        {
            int gaps = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / PicketStep)), n = 2 * gaps + 1;
            bottom.Clear();
            top.Clear();
            Vector3 peak = default;
            for (int k = 0; k < n; k++)
            {
                float x = Mathf.Lerp(x0, x1, k / (float)(n - 1));
                var foot = new Vector3(x, land.Ground(x, z) + Lift, z);
                Vector3 rail = foot + Vector3.up * height;
                bottom.Add(foot);
                top.Add(rail);
                if (k == 0 || rail.y > peak.y) peak = rail;
            }

            fillColor[0] = WithAlpha(kind, FillAlpha * fade);
            fill.AddBand(bottom, top, fillColor, Id(Part.Fences));
            Color picket = WithAlpha(kind, PicketAlpha * fade), line = WithAlpha(kind, RailAlpha * fade);
            for (int k = 0; k < n; k += 2) pickets.AddSegment(bottom[k], top[k], picket, PicketPx, 0, Id(Part.Fences));
            if (!uncertain) rails.AddPolyline(top, line, RailPx, 0, Id(Part.Rails));
            else
            {
                for (int k = 0; k + 1 < n; k += 2) rails.AddSegment(top[k], top[k + 1], line, RailPx, 0, Id(Part.Rails));
            }

            return peak;
        }

        /// <summary>
        /// A moment's name over its tallest fence: its year in bold, then its title (the year the title begins with is not said
        /// twice), in its kind's ink on the dark plate, ranked by the score and the age (<see cref="NameRank"/>), the old ones a
        /// little dimmer.
        /// </summary>
        static HillLabel Name(Politics.Moment m, Vector3 rail, float score, float age, float fade)
        {
            string year = Mathf.FloorToInt(m.Date).ToString(Ci), title = m.Title ?? "";
            if (title.StartsWith(year + " ", StringComparison.Ordinal)) title = title.Substring(year.Length + 1);
            Color ink = Roles.Encoded(Color.Lerp(HillPoliticsLayer.KindColor(m.Kind), Roles.InkText, .45f));
            ink.a = Mathf.Max(fade, OldNameAlpha);
            var steps = new Vector2[4];
            for (int k = 0; k < steps.Length; k++) steps[k] = new Vector2(0, NameOffsetPx + NameStepPx * (k + 1));
            return new HillLabel
            {
                Text = "<b>" + year + "</b> · " + title, At = rail + Vector3.up * NameLift, Role = LabelRole.Note, Part = "moments.rails",
                Rank = NameRank + RankPerPoint * score - Mathf.Max(0, age), Color = ink, Plate = Roles.Encoded(Roles.InkBacking),
                PixelOffset = new Vector2(0, NameOffsetPx), Fallbacks = steps,
            };
        }

        /// <summary>What the last build drew, for the log and the smoke check.</summary>
        string Summary() =>
            $"{fences} fences on {ridges.Count} ridges for {moments} moments up to {builtLand?.Year.ToString(Ci) ?? "-"} (of {fenceable} touches of " +
            $"{MinScore.ToString("0", Ci)} or more on a ridge); left out: {government} touches on the government's own industries (no ridge), " +
            $"{early} {(early == 1 ? "moment" : "moments")} before the land's first rib, {unknown} touches on industries the land does not have";

        // ------------------------------------------------------------------ meshes and labels

        void Lines(LineMeshBuilder b, Part part)
        {
            if (b.VertexCount == 0) return;
            Mesh mesh = b.ToMesh("Key moments " + part);
            Material m = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, LineQueue, false));
            Own(AddMesh("Key moments " + part, mesh, m), mesh, m, part);
        }

        void Surface(SurfaceMeshBuilder b, Part part)
        {
            if (b.VertexCount == 0) return;
            Mesh mesh = b.ToMesh("Key moments " + part + " fill");
            Material m = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1, FillQueue));
            Own(AddMesh("Key moments " + part + " fill", mesh, m), mesh, m, part);
        }

        void Own(MeshRenderer r, Mesh mesh, Material material, Part part)
        {
            r.transform.SetParent(content.transform, false);
            renderers.Add((r, part));
            owned.Add(mesh);
            owned.Add(material);
        }

        /// <summary>Destroys what the last build made (its meshes, materials and content); its names stay until replaced.</summary>
        void Clear()
        {
            foreach (UnityEngine.Object o in owned)
                if (o)
                    Destroy(o);
            owned.Clear();
            renderers.Clear();
            if (content) Destroy(content);
            content = null;
            builtLand = null;
            builtModel = null;
        }

        /// <summary>Puts the names on screen or takes them off, once per change.</summary>
        void ShowLabels(bool show)
        {
            if (show == labelsShown) return;
            labelsShown = show;
            if (show) HillLabels.Set(this, labels);
            else HillLabels.Clear(this);
        }

        static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        // ------------------------------------------------------------------ smoke

        /// <summary>
        /// After every capture of the landscape view: logs what the fences drew ("[Why] moments landscape 2025: 124 fences on 20
        /// ridges for 42 moments ...; 24 names set, 15 placed") and fails a view that draws none where the land has touches to
        /// fence (2025's has 124), one where a touch found no ridge, or one whose fences were not built for the land on screen
        /// (outside the replay, whose frames may come before every layer has caught up).
        /// </summary>
        IEnumerable<string> CheckView(ViewPreset view)
        {
            var problems = new List<string>();
            if (view == null || view.Id != View) return problems;
            HillLandscape land = HillLandscapeLayer.Current;
            // built for the year on screen: a land replaced at the same year (a snapshot's next version) is drawn again at the
            // next free rebuild frame (HillRebuilds), which a capture in between must not fail on
            bool fresh = land != null && content != null && builtLand != null && builtLand.Year == land.Year && builtModel == Politics.Get();
            Debug.Log($"[Why] moments {View} {(land != null ? land.Year.ToString(Ci) : "-")}: " + (fresh ? Summary() : "not built for the land on screen") +
                      $"; {labels.Count} names set, {Placed()} placed");
            if (Politics.Get() == null) problems.Add("no politics data (politics.json): no fences");
            else if (land == null) problems.Add("no land on screen: no fences");
            else if (!fresh)
            {
                if (!EconomyReplay.Active) problems.Add($"the fences were not built for the land on screen ({land.Year.ToString(Ci)})");
            }
            else if (fenceable > 0 && fences == 0)
            {
                problems.Add($"the landscape at {land.Year.ToString(Ci)} draws no fences for its {fenceable} touches of {MinScore.ToString("0", Ci)} or more");
            }
            else if (fences < fenceable)
            {
                // every industry above the bedrock has a ridge at every year of the land: a touch without one is a defect
                problems.Add($"{fenceable - fences} of the {fenceable} touches to fence at {land.Year.ToString(Ci)} found no ridge to stand on");
            }

            return problems;
        }

        /// <summary>How many of the names the label system placed in its last layout.</summary>
        int Placed()
        {
            LabelSystem system = GraphRoot.Instance != null ? GraphRoot.Instance.Labels : null;
            if (system == null || !labelsShown || labels.Count == 0) return 0;
            shownLabels.Clear();
            system.Shown(shownLabels);
            int n = 0;
            foreach (var (spec, _, _) in shownLabels)
            {
                foreach (HillLabel l in labels)
                {
                    if (l.Spec != spec) continue;
                    n++;
                    break;
                }
            }

            return n;
        }

        /// <summary>
        /// <c>-smokeextra moments</c>: the landscape view at 1985 and at 2025 (economy-moments-1985, economy-moments-2025), each
        /// once its year's land is on screen and the view has flown to that year's crest and settled, captured with every check
        /// (this layer's logs its fences and names); then the year it found.
        /// </summary>
        IEnumerator Smoke()
        {
            GraphRoot root = GraphRoot.Instance;
            if (root == null) yield break;
            int before = EconomyState.Year;
            foreach (int year in SmokeYears)
            {
                EconomyState.SetYear(year);
                yield return WaitForLand(year);
                // the catalog's poses follow the land on screen at the land's view layer's next tick (LandViewLayer.FollowCatalog);
                // focused after it, the camera flies to this year's crest, not the last one's (Focus flies before OnFocus)
                yield return null;
                yield return null;
                root.Focus(View, 0f);
                yield return new WaitForSecondsRealtime(SettleSeconds);
                yield return SmokeChecks.Capture("economy-moments-" + year.ToString(Ci), root.CurrentPreset);
            }

            EconomyState.SetYear(before);
            yield return WaitForLand(before);
        }

        /// <summary>Waits until a year's land is on screen in every land layer (<see cref="LandService.ShownYear"/>); an error
        /// past <see cref="LandWaitSeconds"/>.</summary>
        static IEnumerator WaitForLand(int year)
        {
            for (float w = 0; w < LandWaitSeconds; w += Time.unscaledDeltaTime)
            {
                HillLandscape land = HillLandscapeLayer.Current;
                if (land != null && land.Year == year && LandService.ShownYear == year) yield break;
                yield return null;
            }

            Debug.LogError($"[Why] moments: the land of {year.ToString(Ci)} was not on screen {LandWaitSeconds.ToString("0", Ci)} s after it was asked for");
        }
    }
}

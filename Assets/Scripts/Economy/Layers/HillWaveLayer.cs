using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Why.Economy.Land;
using Why.Economy.Model;

namespace Why.Economy.Layers
{
    /// <summary>
    /// The economy as a wave moving forward through time (<see cref="HillLandscape"/>): the expanded cross-section of the
    /// end of the causality graph. Behind the crest lies the wake: one rib per year (that year's cross-section), the
    /// industries' ridges running along time and the steps between the strata. At the crest (the shown year) the cut
    /// face shows the strata standing on one another (government bedrock, raw materials, manufacturing and
    /// infrastructure, services, tech) and the front breaks forward into the future.
    /// <para>Companies live on the wave as people live on the road (<see cref="CompanyLives"/>): each lifeline is born on
    /// its industry's ridge, rises and falls with its market value (height, as the towers' beams), and ends: bankrupt
    /// lines fall to the ground, acquired ones merge into their buyer's line, survivors arrive at the crest (the 25 stored
    /// firms at their towers). A line's color is how Jev judged the company sells: gold where it meets needs, rose toward
    /// manufactured want, ice toward sold fear. Values are approximate.</para>
    /// <para>Thin blue threads carry the population's lifelines from the road out to the cohorts they form at the crest:
    /// the cross-section of the people, expanded into the economy.</para>
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillWaveLayer : GraphLayer
    {
        public override int Order => 54;
        public override IEnumerable<string> RequiredTexts => new[] { CompanyLives.LivesPath, CompanyLives.JudgmentsPath };
        /// <summary>The company line under the pointer and the one clicked (-1: none); indices into CompanyLives.Lives.</summary>
        public static int HoverCompany { get; private set; } = -1;
        public static int SelectedCompany { get; private set; } = -1;
        enum Part { Ribs, Ridges, Face, Crest, Lives, Focus, Fan, Future, Count }
        static readonly Color Steel = new Color(.46f, .58f, .72f), Ice = new Color(.62f, .84f, 1), White = new Color(1, .96f, .9f),
            People = new Color(.36f, .62f, 1);
        readonly List<(Renderer renderer, Part part)> renderers = new List<(Renderer, Part)>();
        readonly List<(TextMeshPro text, int company, bool always)> labels = new List<(TextMeshPro, int, bool)>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly List<(int company, Vector3 at)> samples = new List<(int, Vector3)>();
        readonly Dictionary<int, List<LinePoint>> paths = new Dictionary<int, List<LinePoint>>();
        string livesJson, judgmentsJson;
        LandSnapshot snapshot; HillLandscape land; CompanyLives lives; GameObject content;
        MeshRenderer focus; TextMeshPro focusLabel;
        int focused = -2, programRevision = -1; Vector2 pressed; LandFrame frame;

        public override void Prepare(GraphContext ctx) { livesJson = ctx.Text(CompanyLives.LivesPath); judgmentsJson = ctx.Text(CompanyLives.JudgmentsPath); }
        public override void Upload(GraphContext ctx) { frame = EconomyStage.Land(); frame.Place(transform); }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (HillLandscapeLayer.Current == null || LandService.Current == null) return;
            int revision = PlayerMindProgram.Active?.Revision ?? -1;
            if (snapshot != LandService.Current || land != HillLandscapeLayer.Current || revision != programRevision) { programRevision = revision; Build(); }
            if (content == null) return;
            string view = LandView.PresetId;
            bool hidden = view == "mind";
            content.SetActive(!hidden); if (hidden) { HoverCompany = -1; return; }
            foreach (var e in renderers) if (e.renderer) GraphMaterials.SetAlpha(e.renderer.sharedMaterial, Emphasis(e.part, view));
            Pick(rig.Cam);
            int f = HoverCompany >= 0 ? HoverCompany : SelectedCompany;
            if (f != focused) { focused = f; DrawFocus(); }
            bool companies = view == "companies" || view == "landscape" || view == "overview";
            foreach (var e in labels)
            {
                bool show = view == "section" ? false : e.company == -3 ? view == "rivers" : e.company < 0 ? e.always || view == "landscape" || view == "companies" || view == "overview" || view == "foundation"
                    : e.company == f || companies && e.always;
                e.text.gameObject.SetActive(show);
                if (show) e.text.transform.rotation = rig.Cam.transform.rotation;
            }
            if (focusLabel) { focusLabel.gameObject.SetActive(f >= 0); focusLabel.transform.rotation = rig.Cam.transform.rotation; }
        }

        float Emphasis(Part part, string view)
        {
            bool person = EconomyState.SelectedPlayer >= 0;
            switch (part)
            {
                case Part.Ribs: return view == "landscape" ? .85f : view == "overview" || view == "companies" ? .55f : view == "section" ? .3f : view == "society" || view == "betrayal" ? .08f : .22f;
                case Part.Ridges: return view == "landscape" ? 1 : view == "overview" ? .7f : view == "companies" ? .45f : view == "roots" ? .4f : .2f;
                case Part.Face: return view == "landscape" || view == "foundation" ? 1 : view == "overview" ? .8f : view == "society" || view == "betrayal" ? .15f : .4f;
                case Part.Crest: return view == "society" || view == "betrayal" ? .3f : 1;
                case Part.Lives: return (view == "companies" ? 1 : view == "overview" || view == "landscape" ? .75f : view == "capture" ? .3f : view == "roots" ? .25f : .1f) * (person ? .4f : 1);
                case Part.Focus: return 1;
                case Part.Fan: return view == "people" ? .7f : view == "section" ? .5f : view == "overview" ? .14f : .04f;
                case Part.Future: return view == "rivers" ? .9f : view == "people" ? .35f : view == "overview" ? .12f : .04f;
            }
            return 1;
        }

        // ------------------------------------------------------------------ build

        void Build()
        {
            Clear();
            snapshot = LandService.Current; land = HillLandscapeLayer.Current;
            CompanyLives.Provide(LandService.Model.Data, livesJson, judgmentsJson); lives = CompanyLives.Get();
            content = new GameObject("Economy wave"); content.transform.SetParent(transform, false);
            Wake(); Face(); Lifelines(); Fan(); Future();
            focus = Lines(new LineMeshBuilder(), Part.Focus, true, true); focused = -2;
            focusLabel = Label("", Vector3.zero, .3f, White);
            int born = 0, ended = 0;
            if (lives != null) foreach (var l in lives.Lives) { if (l.Founded >= land.FirstYear && l.Founded <= land.Year) born++; if (!l.Alive && l.Ended <= land.Year && l.Ended >= land.FirstYear) ended++; }
            Debug.Log("[Why] Wave " + land.FirstYear + "-" + land.Year + ": " + (land.Year - land.FirstYear + 1) + " ribs over " +
                (land.CrestZ - land.TailZ).ToString("F0", LandFacts.Ci) + " units; crest width " + land.CrestWidth.ToString("F0", LandFacts.Ci) +
                " (" + land.SliceWidth(land.FirstYear).ToString("F0", LandFacts.Ci) + " in " + land.FirstYear + "); companies " + (lives?.Lives.Length ?? 0) +
                " (" + born + " born, " + ended + " ended in the window; " + (lives?.Reassigned ?? 0) + " on Jev's hill; judgments " + (lives?.Model ?? "absent") + ").");
        }

        /// <summary>The wake: a rib per year (decades brighter and named), the industries' ridges along time and the
        /// steps between the strata.</summary>
        void Wake()
        {
            var ribs = new LineMeshBuilder(); var ridges = new LineMeshBuilder(); var data = LandService.Model.Data;
            var pts = new List<Vector3>();
            for (int y = land.FirstYear; y <= land.Year; y++)
            {
                float z = HillLandscape.Z(y), end = HillLandscape.FootX + land.SliceWidth(y) + 18;
                pts.Clear();
                for (float x = HillLandscape.FootX - 8; x <= end; x += 1.25f) pts.Add(new Vector3(x, land.Ground(x, z) + .03f, z));
                bool decade = y % 10 == 0, lustrum = y % 5 == 0;
                ribs.AddPolyline(pts, WithAlpha(Steel, decade ? .55f : lustrum ? .26f : .10f), decade ? 1.3f : .8f, 0, 0, decade ? 1.4f : 1);
                if (decade) labels.Add((Label(y.ToString(LandFacts.Ci), new Vector3(HillLandscape.FootX - 9, 1.2f, z), .3f, Ice), -1, true));
            }
            var line = new List<LinePoint>();
            for (int i = 0; i < land.Hills.Length; i++)
            {
                if (land.Hills[i].Tier == 0) continue;
                Color c = Ridge(data.Industries[i].Level, land.Hills[i].Tier);
                line.Clear();
                for (float y = land.FirstYear; y <= land.Year + .001f; y += .5f)
                {
                    float z = HillLandscape.Z(y), x = land.RidgeAt(y, i).x;
                    line.Add(new LinePoint(new Vector3(x, land.RidgeTopAt(y, i) + .05f, z), c, 1.2f, 0, 1.2f));
                }
                ridges.AddPolyline(line, 0);
            }
            // The steps between the strata: where each tier's layer begins, and the hillside's summit edge.
            for (int k = 2; k <= 5; k++)
            {
                line.Clear();
                for (int y = land.FirstYear; y <= land.Year; y++)
                {
                    Vector2 band = land.TierBand(y, Math.Min(k, 4)); float x = k <= 4 ? band.x : band.y, z = HillLandscape.Z(y);
                    line.Add(new LinePoint(new Vector3(x, land.Strata(x, z) + .05f, z), WithAlpha(Steel, .6f), 1, 0, 1.3f));
                }
                ridges.AddPolyline(line, 0);
            }
            Lines(ribs, Part.Ribs, false, false);
            Lines(ridges, Part.Ridges, false, false);
        }

        /// <summary>The crest: the cut face of the strata (each tier's layer over the ones beneath it, the bedrock under
        /// all), the bright crest line and the front breaking forward into the future.</summary>
        void Face()
        {
            var fill = new SurfaceMeshBuilder(); var lines = new LineMeshBuilder(); var crest = new LineMeshBuilder();
            int year = land.Year; float z = land.CrestZ + .05f, x0 = HillLandscape.FootX - 8, x1 = HillLandscape.FootX + land.CrestWidth + 18;
            Color[] tier = { EconomyStyle.Government, EconomyStyle.Matter, EconomyStyle.CapitalDim, Color.Lerp(EconomyStyle.CapitalDim, EconomyStyle.Capital, .5f), EconomyStyle.Capital };
            float[] alpha = { .07f, .07f, .06f, .07f, .09f };
            var low = new List<Vector3>(); var high = new List<Vector3>();
            for (float x = x0; x <= x1; x += 1) { low.Add(new Vector3(x, HillLandscape.Bedrock, z)); high.Add(new Vector3(x, 0, z)); }
            fill.AddBand(low, high, new[] { (Color32)WithAlpha(tier[0], alpha[0]) }, 0, 1);
            lines.AddPolyline(low, WithAlpha(Steel, .5f), 1, 0, 0, 1.2f);
            for (int k = 1; k <= 4; k++)
            {
                low.Clear(); high.Clear(); var top = new List<Vector3>();
                for (float x = x0; x <= x1; x += 1)
                {
                    float a = land.LayerAt(year, k - 1, x), b = land.LayerAt(year, k, x);
                    low.Add(new Vector3(x, a, z)); high.Add(new Vector3(x, b, z)); top.Add(new Vector3(x, b + .02f, z));
                }
                fill.AddBand(low, high, new[] { (Color32)WithAlpha(tier[k], alpha[k]) }, 0, 1.2f);
                lines.AddPolyline(top, WithAlpha(Steel, .55f), 1, 0, 0, 1.2f);
                Vector2 band = land.TierBand(year, k); float mid = (band.x + band.y) * .5f;
                string[] names = { "", "RAW MATERIALS", "MANUFACTURING · INFRASTRUCTURE", "SERVICES", "TECH" };
                labels.Add((Label(names[k], new Vector3(mid, (land.LayerAt(year, k - 1, mid) + land.LayerAt(year, k, mid)) * .5f, z + .3f), .5f, Color.Lerp(tier[k], White, .5f)), -1, false));
            }
            labels.Add((Label("GOVERNMENT · the bedrock everything stands on", new Vector3(HillLandscape.FootX + land.CrestWidth * .22f, HillLandscape.Bedrock * .5f, z + .3f), .5f, Ice), -1, false));
            // The ridges above the strata at the crest, then the crest line itself, bright: the wave's leading edge.
            var ridgeFill = new List<Vector3>(); var strata = new List<Vector3>(); var edge = new List<LinePoint>();
            for (float x = x0; x <= x1; x += .5f)
            {
                float s = land.LayerAt(year, 4, x), g = land.Ground(x, land.CrestZ);
                strata.Add(new Vector3(x, s, z)); ridgeFill.Add(new Vector3(x, g, z));
                edge.Add(new LinePoint(new Vector3(x, g + .05f, land.CrestZ), WithAlpha(White, .95f), 2.4f, 0, 3.2f));
            }
            fill.AddBand(strata, ridgeFill, new[] { (Color32)WithAlpha(EconomyStyle.Capital, .22f) }, 0, 1.6f);
            crest.AddPolyline(edge, 0);
            // The front breaks forward: spray arcs fall from the crest into the future.
            for (float x = x0 + 4; x <= x1 - 4; x += 3.5f)
            {
                float g = land.Ground(x, land.CrestZ); if (g < .5f) continue;
                var arc = new List<LinePoint>();
                for (int s = 0; s <= 14; s++)
                {
                    float t = s / 14f;
                    Vector3 p = new Vector3(x, g + 1.4f * Mathf.Sin(t * Mathf.PI * .6f) - g * .75f * t * t, land.CrestZ + 10 * t);
                    arc.Add(new LinePoint(p, WithAlpha(Color.Lerp(White, EconomyStyle.Capital, .3f), .55f * (1 - t)), 1.2f, 0, 2));
                }
                crest.AddFlowPath(arc, 0, .6f, x * .1f);
            }
            labels.Add((Label("<b>" + year + "</b> · the crest: where value is being created now", new Vector3(HillLandscape.FootX + land.CrestWidth * .5f, land.Ground(HillLandscape.FootX + land.CrestWidth * .5f, land.CrestZ) + 12, land.CrestZ + 2), .36f, White), -1, true));
            labels.Add((Label("FUTURE →  not yet made", new Vector3(HillLandscape.FootX + land.CrestWidth + 16, land.LayerAt(year, 4, HillLandscape.FootX + land.CrestWidth * .9f) + 14, land.CrestZ + 22), .5f, WithAlpha(Ice, .8f)), -1, false));
            labels.Add((Label("THE WAKE · the economy's past, one rib a year", new Vector3(HillLandscape.FootX + land.SliceWidth((land.FirstYear + land.Year) / 2) * .5f, land.Ground(HillLandscape.FootX + 30, HillLandscape.Z((land.FirstYear + land.Year) / 2)) + 16, HillLandscape.Z((land.FirstYear + land.Year) / 2)), .32f, Ice), -1, false));
            Surface(fill, Part.Face);
            Lines(lines, Part.Face, false, false);
            var r = Lines(crest, Part.Crest, true, true); r.sharedMaterial.SetFloat("_FlowFreq", 1.5f); r.sharedMaterial.SetFloat("_FlowSpeed", .7f);
        }

        // ------------------------------------------------------------------ company lifelines

        /// <summary>Height of a company above its ridge: the towers' beam (log of market value), grown in over its first years.</summary>
        static float Lift(double value) => 2 + Mathf.Log10(1 + (float)Math.Max(0, value)) * 2.4f;

        void Lifelines()
        {
            if (lives == null) return;
            var b = new LineMeshBuilder(); var data = LandService.Model.Data;
            // Same-industry companies spread across their ridge in order of founding.
            var byIndustry = new Dictionary<int, List<int>>();
            for (int i = 0; i < lives.Lives.Length; i++)
            {
                var l = lives.Lives[i]; if (!byIndustry.TryGetValue(l.Industry, out var list)) byIndustry[l.Industry] = list = new List<int>();
                list.Add(i);
            }
            foreach (var list in byIndustry.Values) list.Sort((a, c) => lives.Lives[a].Founded.CompareTo(lives.Lives[c].Founded));
            var offset = new float[lives.Lives.Length];
            foreach (var list in byIndustry.Values) for (int k = 0; k < list.Count; k++) offset[list[k]] = list.Count == 1 ? 0 : (k / (float)(list.Count - 1) - .5f) * 1.6f;
            // The stored firms' towers (where survivors arrive), placed as HillActivityLayer places them.
            var towers = snapshot.Land.Towers; var halo = new Dictionary<int, Vector3>(); var count = new int[land.Hills.Length]; var total = new int[land.Hills.Length];
            foreach (var t in towers) total[t.Industry]++;
            foreach (var t in towers)
            {
                Vector3 seat = land.FirmSeat(t.Industry, count[t.Industry], total[t.Industry]); count[t.Industry]++;
                int life = lives.OfTower(t.Ticker, t.Name);
                if (life >= 0) halo[life] = seat + Vector3.up * (2 + Mathf.Log10(1 + (float)Math.Max(0, t.MarketCap)) * 2.4f);
            }
            var ranked = new List<(double value, int life)>();
            for (int i = 0; i < lives.Lives.Length; i++)
            {
                var path = Path(i, offset[i], halo.TryGetValue(i, out Vector3 h) ? h : (Vector3?)null);
                if (path == null) continue;
                paths[i] = path; b.AddFlowPath(path, i, .25f, i * .37f);
                var l = lives.Lives[i];
                for (int k = 0; k < path.Count; k += 4) samples.Add((i, path[k].Data));
                // Birth on the ground of its ridge; death by its fate.
                Vector3 first = path[0].Data, last = path[path.Count - 1].Data; Color c = path[0].Color;
                if (l.Founded >= land.FirstYear) Ring(b, new Vector3(first.x, land.Ground(first.x, first.z) + .05f, first.z), .5f, WithAlpha(c, .8f), 1.4f, 2);
                int end = l.Alive || l.Ended > land.Year ? land.Year : l.Ended;
                if (!l.Alive && l.Ended <= land.Year)
                {
                    if (l.Fate == "bankrupt") { b.AddSegment(last + new Vector3(-.6f, 0, -.6f), last + new Vector3(.6f, 0, .6f), WithAlpha(EconomyStyle.Defect, .9f), 2, 0, 0, 2); b.AddSegment(last + new Vector3(-.6f, 0, .6f), last + new Vector3(.6f, 0, -.6f), WithAlpha(EconomyStyle.Defect, .9f), 2, 0, 0, 2); }
                    else Ring(b, last, .45f, WithAlpha(c, .9f), 1.6f, 2.2f);
                    string fate = l.Fate == "bankrupt" ? "failed " + l.Ended : l.Fate == "breakup" ? "broken up " + l.Ended
                        : (l.Fate == "acquired" ? "bought " : "merged ") + l.Ended + (l.Successor >= 0 ? " → " + lives.Lives[l.Successor].Name : l.SuccessorName != null ? " → " + l.SuccessorName : "");
                    labels.Add((Label(l.Name + " · " + fate, last + Vector3.up * 1.6f, .3f, Color.Lerp(c, White, .4f)), i, true));
                }
                else ranked.Add((l.ValueAt(end), i));
            }
            // Survivors are named at the crest when large; every line is named on hover.
            ranked.Sort((a, c) => c.value.CompareTo(a.value));
            for (int n = 0; n < ranked.Count; n++)
            {
                int i = ranked[n].life; var path = paths[i]; Vector3 last = path[path.Count - 1].Data;
                if (!halo.ContainsKey(i)) labels.Add((Label(lives.Lives[i].Name, last + Vector3.up * 1.6f, .3f, Color.Lerp(path[path.Count - 1].Color, White, .4f)), i, n < 14));
            }
            Lines(b, Part.Lives, true, true).sharedMaterial.SetFloat("_FlowSpeed", .25f);
        }

        /// <summary>A company's lifeline: from its birth (or the wake's first year) along its ridge, at the height of its
        /// market value, to its end. Survivors with a tower bend into the tower's halo; acquired companies curve into
        /// their buyer's line; failed ones fall to the ground.</summary>
        List<LinePoint> Path(int index, float offset, Vector3? tower)
        {
            var l = lives.Lives[index];
            float start = Mathf.Max(l.Founded, land.FirstYear), end = l.Alive || l.Ended > land.Year ? land.Year : l.Ended;
            if (end < start || start > land.Year) return null;
            Color color = Color.Lerp(EconomyStyle.Capital, EconomyStyle.Motive(l.FearShare), l.Judged ? Mathf.Clamp01(l.Persuaded) * .85f : 0);
            float glow = 1.2f + .9f * (l.Judged ? l.LockedIn : 0);
            var path = new List<LinePoint>();
            float arrive = tower.HasValue ? (float)HillLandscape.YearAt(tower.Value.z) : end;
            for (float y = start; y <= end + .001f; y += .25f)
            {
                float z = HillLandscape.Z(y); Vector2 ridge = land.RidgeAt(y, l.Industry);
                float x = ridge.x + offset * ridge.y * .5f;
                double v = l.ValueAt(y); float lift = Lift(v) * (l.Founded >= land.FirstYear ? Mathf.SmoothStep(0, 1, (y - l.Founded) / 3f) : 1);
                Vector3 p = new Vector3(x, land.RidgeTopAt(y, l.Industry) + lift, z);
                if (tower.HasValue)
                {
                    // The last years bend toward the tower's halo, arriving exactly on it.
                    float t = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(arrive - 4, arrive, y));
                    if (y > arrive) break;
                    p = Vector3.Lerp(p, new Vector3(tower.Value.x, tower.Value.y, z), t);
                }
                path.Add(new LinePoint(p, WithAlpha(color, .9f), .9f + .45f * Mathf.Log10(1 + (float)v), 0, glow));
            }
            if (path.Count == 0) return null;
            if (tower.HasValue) path.Add(new LinePoint(tower.Value, WithAlpha(color, .9f), path[path.Count - 1].WidthPx, 0, glow));
            Vector3 last = path[path.Count - 1].Data;
            if (!l.Alive && l.Ended <= land.Year)
            {
                if (l.Fate == "bankrupt")
                    for (int s = 1; s <= 4; s++) { float t = s / 4f; Vector3 p = new Vector3(last.x, Mathf.Lerp(last.y, land.Ground(last.x, last.z) + .1f, t * t), last.z + .8f * t); path.Add(new LinePoint(p, WithAlpha(color, .9f * (1 - t * .5f)), .9f, 0, glow)); }
                else if (l.Successor >= 0 && lives.Lives[l.Successor].Founded <= l.Ended)
                {
                    var buyer = lives.Lives[l.Successor]; Vector2 ridge = land.RidgeAt(l.Ended + .5f, buyer.Industry);
                    float z = HillLandscape.Z(l.Ended + .5f), x = ridge.x;
                    Vector3 to = new Vector3(x, land.RidgeTopAt(l.Ended + .5f, buyer.Industry) + Lift(buyer.ValueAt(l.Ended + .5f)), z);
                    for (int s = 1; s <= 8; s++) { float t = s / 8f; path.Add(new LinePoint(Vector3.Lerp(last, to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.5f, WithAlpha(color, .9f), .9f, 0, glow)); }
                }
            }
            return path;
        }

        void DrawFocus()
        {
            var b = new LineMeshBuilder();
            if (focused >= 0 && paths.TryGetValue(focused, out var path))
            {
                var bright = new List<LinePoint>(path.Count);
                foreach (var p in path) bright.Add(new LinePoint(p.Data + Vector3.up * .02f, WithAlpha(p.Color, 1), p.WidthPx + 2.2f, 0, 4.5f));
                b.AddFlowPath(bright, 0, .25f);
                var l = lives.Lives[focused]; Vector3 at = path[path.Count - 1].Data;
                double peak = l.Peak(out int peakYear);
                string life = l.Founded + "–" + (l.Alive ? "" : l.Ended.ToString(LandFacts.Ci)) + (l.Alive ? " · alive" : " · " + l.Fate);
                string text = "<b>" + l.Name + "</b>  " + life + (l.Event != null ? " (" + l.Event + ")" : "") +
                    "\n" + LandService.Model.Data.Industries[l.Industry].Name + " · " + l.Sells +
                    "\npeak " + LandFacts.Money(peak) + " in " + peakYear + " (2025 $, approximate" + (l.Private ? ", private valuation" : "") + ")";
                if (l.Judged) text += "\nhow it sells (Jev): manufactured want " + LandFacts.Percent(l.Manufactured) + " · fear " + LandFacts.Percent(l.FearSold) +
                    " · captive " + LandFacts.Percent(l.Captive) + " · habit " + LandFacts.Percent(l.Habit);
                focusLabel.text = text; focusLabel.transform.localPosition = at + Vector3.up * 3.2f;
            }
            Replace(focus, b);
        }

        /// <summary>Threads from the road (the population's lifelines at the crest year) out to every cohort on the wave.</summary>
        void Fan()
        {
            var b = new LineMeshBuilder();
            Vector3 road = frame.Local(EconomyStage.OnRoad(land.Year, GraphStyle.HumansY, EconomyStyle.FramingRho));
            for (int i = 0; i < land.People.Length; i++)
            {
                if (land.Cloud[i]) continue;
                Vector3 to = land.People[i] + Vector3.up * .3f; var pts = new LinePoint[25];
                for (int s = 0; s < pts.Length; s++)
                {
                    float t = s / 24f; Vector3 p = Vector3.Lerp(road, to, t); p.y = Mathf.Lerp(road.y, to.y, t * t) + Mathf.Sin(t * Mathf.PI) * 2;
                    pts[s] = new LinePoint(p, WithAlpha(People, .5f), .7f, 0, 1.2f);
                }
                b.AddFlowPath(pts, 0, .4f, i * .21f);
            }
            labels.Add((Label("the road of lives fans out into the economy", road + new Vector3(4, 4, -2), .18f, People), -1, false));
            Lines(b, Part.Fan, true, true);
        }

        /// <summary>Money along time: each cohort's saving leaves over the crest into the future as gold (capital that
        /// builds what comes next), and its debt arrives from the future as a desire / fear stream (spending drawn from
        /// income not yet earned), colored by the cohort's motive. Widths follow the square root of dollars; the
        /// scenario's balances are used while one runs.</summary>
        void Future()
        {
            var b = new LineMeshBuilder(); var players = snapshot.Players.Players; var program = PlayerMindProgram.Active;
            if (program != null && program.Players.Length != players.Length) program = null;
            double saved = 0, owed = 0;
            for (int i = 0; i < players.Length; i++)
            {
                if (land.Cloud[i]) continue;
                double saving = program?.Players[i].Saving ?? players[i].Saving, debt = program?.Players[i].Debt ?? players[i].Debt;
                float fear = program?.Players[i].Fear ?? players[i].Fear;
                Vector3 home = land.People[i] + Vector3.up * .4f, lip = new Vector3(home.x, land.Ground(home.x, land.CrestZ) + 1.5f, land.CrestZ + 1);
                Vector3 ahead = new Vector3(home.x, lip.y * .55f + 3, land.CrestZ + 16);
                if (saving > .05)
                {
                    saved += saving;
                    Stream(b, home, lip, ahead, WithAlpha(EconomyStyle.Capital, .8f), .6f + Mathf.Sqrt((float)saving) * .35f, false);
                }
                if (debt > .05)
                {
                    owed += debt;
                    Color motive = EconomyStyle.Motive(fear);
                    Stream(b, ahead + new Vector3(1.5f, 2, 0), lip + new Vector3(1.5f, 0, 0), home + new Vector3(.4f, 0, 0), WithAlpha(motive, .75f), .5f + Mathf.Sqrt((float)debt) * .06f, true);
                }
            }
            labels.Add((Label("SAVING → capital sent into the future " + LandFacts.Money(saved) + " a year\n← DEBT  spending drawn from future income " + LandFacts.Money(owed),
                new Vector3(HillLandscape.FootX + land.CrestWidth * .5f, land.Ground(HillLandscape.FootX + land.CrestWidth * .5f, land.CrestZ) + 22, land.CrestZ + 14), .4f, White), -3, false));
            Lines(b, Part.Future, true, true).sharedMaterial.SetFloat("_FlowSpeed", .5f);
        }

        static void Stream(LineMeshBuilder b, Vector3 a, Vector3 mid, Vector3 z, Color color, float width, bool inward)
        {
            var pts = new LinePoint[21];
            for (int s = 0; s < pts.Length; s++)
            {
                float t = s / 20f; Vector3 p = Vector3.Lerp(Vector3.Lerp(a, mid, t), Vector3.Lerp(mid, z, t), t);
                pts[s] = new LinePoint(p, WithAlpha(color, color.a * (inward ? Mathf.Lerp(.25f, 1, t) : Mathf.Lerp(1, .25f, t))), width, 0, 1.8f);
            }
            b.AddFlowPath(pts, 0, .6f);
        }

        // ------------------------------------------------------------------ picking

        void Pick(Camera camera)
        {
            HoverCompany = -1;
            var mouse = Mouse.current;
            if (mouse == null || UI.HillExplorer.Depth >= 3 || EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;
            if (Emphasis(Part.Lives, LandView.PresetId) < .2f && SelectedCompany < 0) return;
            Vector2 pointer = mouse.position.ReadValue(); float best = 12 * Screen.height / 1080f;
            foreach (var s in samples)
            {
                Vector3 screen = camera.WorldToScreenPoint(frame.World(s.at));
                if (screen.z <= 0) continue;
                float d = Vector2.Distance(pointer, screen); if (d < best) { best = d; HoverCompany = s.company; }
            }
            if (mouse.leftButton.wasPressedThisFrame) pressed = pointer;
            if (mouse.leftButton.wasReleasedThisFrame && Vector2.Distance(pressed, pointer) < 5 && HillLandscapeLayer.HoverPlayer < 0)
                SelectedCompany = HoverCompany;
        }

        // ------------------------------------------------------------------ plumbing

        static Color Ridge(string level, int tier)
        {
            if (level == "matter") return WithAlpha(EconomyStyle.Matter, .55f);
            if (level == "life") return WithAlpha(EconomyStyle.Life, .55f);
            return WithAlpha(Color.Lerp(EconomyStyle.CapitalDim, EconomyStyle.Capital, (tier - 2) / 2f), .5f);
        }

        static void Ring(LineMeshBuilder b, Vector3 c, float r, Color color, float width, float intensity)
        {
            var pts = new Vector3[25];
            for (int s = 0; s < pts.Length; s++) { float a = s * Mathf.PI / 12; pts[s] = c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r); }
            b.AddPolyline(pts, color, width, 0, 0, intensity);
        }

        MeshRenderer Lines(LineMeshBuilder b, Part part, bool flow, bool additive)
        {
            Mesh mesh = b.ToMesh("Wave " + part); owned.Add(mesh);
            Material material = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3204, additive, 0, flow ? 1 : 0)); owned.Add(material);
            if (flow) { material.SetFloat("_FlowFreq", 2); material.SetFloat("_FlowSpeed", .4f); }
            var r = AddMesh("Wave " + part, mesh, material); r.transform.SetParent(content.transform, false);
            renderers.Add((r, part)); return r;
        }

        void Surface(SurfaceMeshBuilder b, Part part)
        {
            Mesh mesh = b.ToMesh("Wave " + part); owned.Add(mesh);
            Material material = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1, 3194)); owned.Add(material);
            var r = AddMesh("Wave " + part, mesh, material); r.transform.SetParent(content.transform, false);
            renderers.Add((r, part));
        }

        void Replace(MeshRenderer r, LineMeshBuilder b)
        {
            var filter = r.GetComponent<MeshFilter>(); Mesh old = filter.sharedMesh; owned.Remove(old); Destroy(old);
            filter.sharedMesh = b.ToMesh(r.name); owned.Add(filter.sharedMesh);
        }

        TextMeshPro Label(string text, Vector3 at, float size, Color color)
        {
            var go = new GameObject("Wave label"); go.transform.SetParent(content.transform, false); go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>(); t.text = text; t.fontSize = 24; t.alignment = TextAlignmentOptions.Center;
            t.color = color; t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.sizeDelta = new Vector2(60, 8);
            go.transform.localScale = Vector3.one * size * LabelScale; return t;
        }

        /// <summary>The wave is seen from a few hundred units: its labels are drawn larger than the hills' close-up ones.</summary>
        const float LabelScale = 2.2f;

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        void Clear()
        {
            foreach (var o in owned) if (o) Destroy(o);
            if (content) Destroy(content);
            owned.Clear(); renderers.Clear(); labels.Clear(); samples.Clear(); paths.Clear(); content = null; focus = null; focusLabel = null;
        }

        void OnDestroy() { Clear(); HoverCompany = SelectedCompany = -1; }
    }
}

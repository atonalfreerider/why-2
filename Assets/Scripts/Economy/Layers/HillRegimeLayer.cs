using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using Why.Economy.Land;
using Why.Economy.Model;

namespace Why.Economy.Layers
{
    /// <summary>
    /// Other governments as model changes (view "regimes"; the data is <see cref="Regimes"/>): the same hills - the United
    /// States' industries as BEA measures them - stood on another state's foundation, drawn from that state's measured
    /// structure. Only the foundation and its reach change; the hills, the people and the money they make stay the US's.
    /// <list type="bullet">
    /// <item><b>The bedrock</b> under the crest is as deep as the regime's government spending (IMF; the same scale as the
    /// federal block of the other views: 20% of GDP is 14 units), in strata of what it lives on (its revenue mix: income
    /// and profit taxes, social contributions, taxes on spending, property, oil and other resource revenue, the profits of
    /// state enterprises, the rest), a white line at its revenue and the gap below it borrowed. A thin line marks the
    /// United States' depth for comparison.</item>
    /// <item><b>Summits.</b> Each hill's summit carries a ring split steel and gold: steel is the state's share of the
    /// leading firm named there (the file names each state's holdings, with owner and source), gold the private owners';
    /// summits no firm is named for are private, except in the Soviet Union, where every one was the state's by law.
    /// Steel beams rise from the bedrock into the summits the state owns: under state capitalism the state does not only
    /// lie beneath the hills, it owns their tops.</item>
    /// <item><b>Strands.</b> Grey up into each hill as strongly as the state's rules bind it, gold back down as strongly as
    /// the industry shapes them (Jev's judgments for the regime; the US keeps the foundation view's own).</item>
    /// <item><b>Rivers.</b> The money people spend runs from the valley to every market; a steel lane as wide as the
    /// state's share of all spending (spending / GDP) dives through the bedrock on the way, the rest runs straight.</item>
    /// <item><b>What the state lives on</b> falls into it from where it comes: income taxes and contributions from the
    /// people, taxes on spending from the markets, profit taxes from the private summits, state enterprises' profits from
    /// the steel summits, resource revenue from the ground under the raw hills.</item>
    /// <item><b>Votes</b> run down from the people into the state as wide as the electoral democracy index (V-Dem);
    /// <b>trade</b> crosses the far border as wide as exports plus imports over GDP (World Bank); the top 1%'s gold ring
    /// above the hills is as large as their share of wealth, a white ring inside it their share of income (WID).</item>
    /// </list>
    /// The regime shown is pinned by <see cref="UI.RegimePanel"/>; unpinned, the view steps through them. Every number on
    /// screen is measured (with its year in the panel) except the strands (Jev) and the drawing's own choices, which the
    /// panel lists.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillRegimeLayer : GraphLayer
    {
        public const string View = "regimes";
        public override int Order => 65;
        public override IEnumerable<string> RequiredTexts => new[] { Regimes.Path };
        enum Part { Bedrock, Strata, Summits, Beams, Strands, Rivers, Taxes, Votes, Trade, Cloud, Count }
        static readonly Color Gold = new Color(1, .70f, .24f), Steel = new Color(.62f, .74f, .90f), White = new Color(1, .97f, .92f),
            Rose = new Color(1, .40f, .58f), Ice = new Color(.45f, .80f, 1), Oil = new Color(.86f, .56f, .20f), Grey = new Color(.80f, .83f, .88f);
        /// <summary>The bedrock's strata by <see cref="Regimes.Part"/>: stone tinted toward where each part comes from.</summary>
        static readonly Color[] PartColor =
        {
            new Color(.46f, .60f, .80f), new Color(.80f, .66f, .38f), new Color(.52f, .70f, .84f), new Color(.80f, .50f, .60f),
            new Color(.62f, .58f, .50f), new Color(.86f, .56f, .20f), new Color(.60f, .70f, .84f), new Color(.46f, .48f, .52f)
        };
        /// <summary>Bedrock depth per share of GDP (world units): the federal block's scale (20% of GDP is 14 units deep).</summary>
        public const float DepthPerShare = 70;
        /// <summary>How far back along the wake the regime's bedrock reaches (its numbers are one year's).</summary>
        const float Run = 22;
        /// <summary>Seconds each regime stays on screen when none is pinned.</summary>
        const float CycleSeconds = 9;
        /// <summary>Where the bedrock's labels stand (land-local x): toward the hills' side, which the view frames on the
        /// left, clear of the panel on the right.</summary>
        const float LabelX = 150;

        /// <summary>The regime pinned by the panel (index into <see cref="Regimes.All"/>; -1: step through them).</summary>
        public static int Pinned = -1;
        /// <summary>The regime on screen (index), its readout for the panel, and how the drawing reads it.</summary>
        public static int Shown { get; private set; } = -1;
        public static string ReadoutText { get; private set; } = "";
        public static string DrawingText { get; private set; } = "";

        readonly List<(Renderer renderer, Part part)> renderers = new List<(Renderer, Part)>();
        readonly List<TextMeshPro> labels = new List<TextMeshPro>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        string json;
        LandSnapshot snapshot; HillLandscape land; Regimes regimes; GameObject content;
        int built = -2;
        bool checkedOnce, wasOn;
        float enteredAt;

        public override void Prepare(GraphContext ctx) { json = ctx.Text(Regimes.Path); }
        public override void Upload(GraphContext ctx)
        {
            EconomyStage.Land().Place(transform);
            // the smoke test captures every regime (pinned in turn, then back to stepping through them)
            ViewVariants.Register(View, () => Regimes.Get()?.All.Length ?? 0, k => Pinned = k, k => Regimes.Get()?.All[k].Id ?? k.ToString());
        }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (HillLandscapeLayer.Current == null || LandService.Current == null) return;
            if (HillLandscapeLayer.Current.Source != LandService.Current) return;
            bool on = LandView.PresetId == View;
            if (!on) { wasOn = false; if (content) content.SetActive(false); return; }
            // stepping through starts at the baseline each time the view opens (the tour's narration follows that order)
            if (!wasOn) { wasOn = true; enteredAt = Time.unscaledTime; }
            if (regimes == null)
            {
                Regimes.Provide(LandService.Model.Data, json); regimes = Regimes.Get();
                if (regimes == null) return;
                if (!checkedOnce)
                {
                    checkedOnce = true;
                    foreach (string problem in regimes.Problems(LandService.Model.Data)) Debug.LogError("[Why] Regimes check failed: " + problem);
                }
            }
            int n = regimes.All.Length; if (n == 0) return;
            int shown = Pinned >= 0 && Pinned < n ? Pinned : (int)((Time.unscaledTime - enteredAt) / CycleSeconds) % n;
            if (snapshot != LandService.Current || land != HillLandscapeLayer.Current || shown != built) Build(shown);
            if (content == null) return;
            content.SetActive(true);
            foreach (var t in labels) t.transform.rotation = rig.Cam.transform.rotation;
        }

        // ------------------------------------------------------------------ build

        void Build(int index)
        {
            Clear();
            snapshot = LandService.Current; land = HillLandscapeLayer.Current; built = index; Shown = index;
            var data = LandService.Model.Data; var r = regimes.All[index]; var us = regimes.Baseline;
            content = new GameObject("Regime: " + r.Name); content.transform.SetParent(transform, false);
            double gdp = snapshot.Land.Gdp;
            float g = r.Spending.Has ? (float)r.Spending.Value : 0, rev = r.Revenue.Has ? (float)r.Revenue.Value : 0;
            float x0 = land.XMin, x1 = land.XMax, top = HillLandscape.Bedrock, cz = land.CrestZ;

            Bedrock(r, us, g, rev, gdp, x0, x1, top, cz);
            var steel = Summits(r, data);
            Strands(r);
            Rivers(r, g);
            Taxes(r, rev, gdp, steel);
            Votes(r);
            Trade(r);
            Cloud(r);

            // the title above the crest
            string title = "<b>" + r.Name.ToUpperInvariant() + "</b> · " + r.Form + " · " + r.Era;
            string sub = r.Baseline ? "the hills on their own state: the baseline the other foundations are set against"
                : "the same US hills on " + Possessive(r) + " state: only the foundation and its reach change";
            Label(title + "\n<size=65%>" + sub + "</size>", new Vector3(LabelX, land.CloudY + 30, cz + 4), 1.9f, White);
            ReadoutText = Readout(r, us, data);
            DrawingText = Drawing(r);
            Debug.Log("[Why] Regime " + r.Name + " (" + r.Era + "): spending " + Pct(r.Spending) + ", revenue " + Pct(r.Revenue) + ", " +
                      CountSteel(r, data) + " of " + Production(data) + " summits the state's, regulation and capture " + (r.Judged != null ? "judged" : "not judged") + ".");
        }

        static string Possessive(Regimes.Regime r) => r.Id == "su" ? "the Soviet" : r.Name.EndsWith("s") ? r.Name + "'" : r.Name + "'s";

        /// <summary>The regime's bedrock under the crest: as deep as its spending, in strata of its revenue mix, the revenue
        /// line and the borrowed gap beneath it, the US depth for comparison.</summary>
        void Bedrock(Regimes.Regime r, Regimes.Regime us, float g, float rev, double gdp, float x0, float x1, float top, float cz)
        {
            var faces = new SurfaceMeshBuilder(); var edges = new LineMeshBuilder(); var strata = new LineMeshBuilder();
            float zb = cz - Run, depth = Mathf.Max(g, rev) * DepthPerShare, bottom = top - depth;
            float y = top;
            if (r.Mix != null)
            {
                for (int k = 0; k < Regimes.Parts; k++)
                {
                    float d = (float)r.Mix[k] * rev * DepthPerShare; if (d <= .01f) continue;
                    Slab(faces, x0, x1, y, y - d, cz, zb, PartColor[k], .62f);
                    edges.AddPolyline(new[] { new Vector3(x0, y - d, cz), new Vector3(x1, y - d, cz) }, WithAlpha(White, .35f), 1, 0, 0, 1.3f);
                    // a stratum names itself when it can hold a line of text (about 3% of GDP); the panel lists them all
                    if (d >= 2.2f)
                        Label("<b>" + PartName(k).ToUpperInvariant() + "</b>  " + LandFacts.Percent(r.Mix[k] * rev, 1) + " of GDP · " +
                              LandFacts.Percent(r.Mix[k]) + " of revenue", new Vector3(LabelX, y - d * .5f, cz + .6f),
                              Mathf.Clamp(d * .32f, .75f, 1.15f), Color.Lerp(PartColor[k], White, .55f));
                    y -= d;
                }
            }
            else
            {
                Slab(faces, x0, x1, top, top - rev * DepthPerShare, cz, zb, PartColor[(int)Regimes.Part.Other], .5f);
                Label("revenue " + LandFacts.Percent(rev, 1) + " of GDP (its sources not found)", new Vector3(LabelX, top - rev * DepthPerShare * .5f, cz + .6f), 1f, Grey);
                y = top - rev * DepthPerShare;
            }
            // the revenue line, and what lies beneath it: borrowed (spending above revenue) or a surplus
            float yr = top - rev * DepthPerShare, yg = top - g * DepthPerShare;
            if (g > rev + .001f)
            {
                Slab(faces, x0, x1, yr, yg, cz, zb, new Color(.10f, .11f, .14f), .75f);
                for (float x = x0 + 4; x < x1; x += 8) strata.AddSegment(new Vector3(x, yr, cz + .2f), new Vector3(x + 4, yg, cz + .2f), WithAlpha(Grey, .25f), .8f, 0, 0, 1);
                if (yr - yg >= 2.2f) Label("<b>BORROWED</b>  " + LandFacts.Percent(g - rev, 1) + " of GDP: spending above revenue", new Vector3(LabelX, (yr + yg) * .5f, cz + .6f), Mathf.Clamp((yr - yg) * .32f, .75f, 1.1f), Grey);
            }
            else if (rev > g + .001f && yg - yr >= 2.2f)
                Label("<b>SURPLUS</b>  revenue " + LandFacts.Percent(rev - g, 1) + " of GDP above spending (the steel line marks spending)", new Vector3(LabelX, (yr + yg) * .5f, cz + .6f), .9f, Grey);
            strata.AddPolyline(new[] { new Vector3(x0, yr, cz + .3f), new Vector3(x1, yr, cz + .3f) }, WithAlpha(White, .95f), 2, 0, 0, 2.6f);
            strata.AddPolyline(new[] { new Vector3(x0, yg, cz + .3f), new Vector3(x1, yg, cz + .3f) }, WithAlpha(Steel, .9f), 2.4f, 0, 0, 2.2f);
            // the depth in words beneath the bedrock
            Label("<b>THE STATE SPENDS " + LandFacts.Percent(g, 1) + " OF GDP</b> and takes in " + LandFacts.Percent(rev, 1) + "\n<size=75%>on this economy: " +
                  Money(g * gdp) + " of $" + (gdp / 1000).ToString("0.0", LandFacts.Ci) + "T · the white line: revenue · the steel line: spending</size>",
                  new Vector3(LabelX, bottom - 6, cz + .8f), 1.45f, Color.Lerp(Steel, White, .5f));
            // the US for comparison
            if (!r.Baseline && us != null && us.Spending.Has)
            {
                float yu = top - (float)us.Spending.Value * DepthPerShare;
                for (float x = x0; x < x1; x += 6) edges.AddSegment(new Vector3(x, yu, cz + .5f), new Vector3(Mathf.Min(x1, x + 3.4f), yu, cz + .5f), WithAlpha(White, .8f), 1.6f, 0, 0, 2);
                Label("- - the United States: " + LandFacts.Percent(us.Spending.Value, 1), new Vector3(x1 - 30, yu - 2.2f, cz + .8f), .9f, White);
            }
            // the block's outline
            foreach (float yy in new[] { top, bottom })
                edges.AddPolyline(new[] { new Vector3(x0, yy, zb), new Vector3(x0, yy, cz), new Vector3(x1, yy, cz), new Vector3(x1, yy, zb) }, WithAlpha(Steel, .55f), 1.2f, 0, 0, 1.4f);
            edges.AddSegment(new Vector3(x0, top, cz), new Vector3(x0, bottom, cz), WithAlpha(Steel, .55f), 1.2f, 0, 0, 1.4f);
            edges.AddSegment(new Vector3(x1, top, cz), new Vector3(x1, bottom, cz), WithAlpha(Steel, .55f), 1.2f, 0, 0, 1.4f);
            Surface(faces, Part.Bedrock); Lines(edges, Part.Bedrock, false); Lines(strata, Part.Strata, false);
        }

        /// <summary>Each hill's summit ring split steel (the state's share of the leading firm named there) and gold, with
        /// steel beams up from the bedrock into what the state owns. Returns each industry's steel share.</summary>
        double[] Summits(Regimes.Regime r, Data.EconomyData data)
        {
            var rings = new LineMeshBuilder(); var beams = new LineMeshBuilder();
            int n = land.Hills.Length; var steel = new double[n];
            int named = 0;
            for (int j = 0; j < n; j++)
            {
                var hill = land.Hills[j]; if (hill.Tier == 0) continue;
                double s = steel[j] = r.StateShare(j);
                double owners = Math.Max(0, snapshot.Land.Sectors[j].Owners);
                float radius = 1.5f + Mathf.Sqrt((float)owners) * .16f;
                Vector3 h = land.Halo(j);
                for (int ring = 0; ring < 2; ring++)
                {
                    float rr = radius * (ring == 0 ? 1 : .78f);
                    Arc(rings, h, rr, 0, (float)s, WithAlpha(Steel, .95f), 3, 3.4f);
                    Arc(rings, h, rr, (float)s, 1, WithAlpha(Gold, .9f), 2.4f, 2.6f);
                }
                if (s > .01)
                {
                    Vector3 foot = new Vector3(hill.Center.x, HillLandscape.Bedrock, land.CrestZ - 2.5f);
                    var pts = new LinePoint[24];
                    for (int k = 0; k < pts.Length; k++)
                    {
                        float t = k / 23f;
                        pts[k] = new LinePoint(Vector3.Lerp(foot, h, t) + Vector3.forward * Mathf.Sin(t * Mathf.PI) * 1.5f, WithAlpha(Steel, .85f), 1 + 5 * (float)s, 0, 2.2f + 1.4f * (float)s);
                    }
                    beams.AddFlowPath(pts, 0, .35f, j * .2f);
                }
                var firm = r.SummitFirm(j);
                if (firm != null)
                {
                    named++;
                    string share = firm.Share <= 0 ? "private" : double.IsNaN(firm.StateShare) ? "state-controlled" : LandFacts.Percent(firm.StateShare) + " state";
                    Label("<b>" + firm.Name + "</b> · " + share, h + Vector3.up * (radius + 2.6f), .85f, s >= .5 ? Color.Lerp(Steel, White, .45f) : Color.Lerp(Gold, White, .45f));
                }
            }
            if (r.DefaultOwnership >= .5 && named == 0)
                Label("<b>EVERY SUMMIT THE STATE'S</b> · " + (r.DefaultNote ?? "state property"), new Vector3(LabelX, land.CloudY - 4, land.CrestZ + 2), 1.25f, Color.Lerp(Steel, White, .4f));
            Lines(rings, Part.Summits, false); Lines(beams, Part.Beams, true);
            return steel;
        }

        /// <summary>Regulation up into each hill (grey) and capture back down (gold), as the regime's judgments say.</summary>
        void Strands(Regimes.Regime r)
        {
            var b = new LineMeshBuilder();
            for (int j = 0; j < land.Hills.Length; j++)
            {
                var hill = land.Hills[j]; if (hill.Tier == 0) continue;
                float reg = r.Regulation[j], cap = r.Capture[j];
                if (float.IsNaN(reg)) continue;
                Vector3 foot = new Vector3(hill.Center.x - 1.2f, HillLandscape.Bedrock, land.CrestZ - 3), into = land.Market(j) + Vector3.up * .5f;
                Strand(b, foot, into, WithAlpha(Grey, .75f), .5f + 3 * reg, 1.4f + 1.6f * reg, 1.2f);
                if (!float.IsNaN(cap) && cap > .05f) Strand(b, into + Vector3.right * 1.2f, foot + Vector3.right * 2.4f, WithAlpha(Gold, .7f), .4f + 3 * cap, 1.4f + 2f * cap, -1.2f);
            }
            Lines(b, Part.Strands, true);
        }

        /// <summary>The money people spend, from the valley to every market (width: the square root of the hill's value
        /// added): a steel lane as wide as the state's share of all spending dives through the bedrock, the rest runs straight.</summary>
        void Rivers(Regimes.Regime r, float g)
        {
            var b = new LineMeshBuilder();
            for (int j = 0; j < land.Hills.Length; j++)
            {
                var hill = land.Hills[j]; if (hill.Tier == 0) continue;
                float w = .6f + Mathf.Sqrt((float)Math.Max(0, snapshot.Land.Sectors[j].ValueAdded)) * .07f;
                Vector3 m = land.Market(j), from = land.ValleyFloor(.5f, land.CrestZ - 1.5f - (j % 5) * .6f, .4f);
                // the private lane: an arch over the ground
                var direct = new LinePoint[26];
                for (int k = 0; k < direct.Length; k++) { float t = k / 25f; direct[k] = new LinePoint(Vector3.Lerp(from, m, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * (3 + j % 4), WithAlpha(Rose, .7f), Mathf.Max(.4f, w * (1 - g)), 0, 1.8f); }
                b.AddFlowPath(direct, 0, .5f, j * .13f);
                if (g <= .005f) continue;
                // the state's lane: down into the bedrock and back up to the market
                float xd = Mathf.Lerp(from.x, m.x, .5f), yd = HillLandscape.Bedrock - 4 - (j % 4) * 1.2f;
                Vector3 a = from, c = m, bottom = new Vector3(xd, yd, land.CrestZ - 1.2f);
                var through = new LinePoint[34];
                for (int k = 0; k < through.Length; k++)
                {
                    float t = k / 33f; Vector3 p = Vector3.Lerp(Vector3.Lerp(a, bottom, t), Vector3.Lerp(bottom, c, t), t);
                    through[k] = new LinePoint(p, WithAlpha(Steel, .85f), Mathf.Max(.4f, w * g), 0, 2.2f);
                }
                b.AddFlowPath(through, 0, .5f, j * .17f);
            }
            Lines(b, Part.Rivers, true);
        }

        /// <summary>What the state lives on, falling into the bedrock from where each part comes from (widths: the square
        /// root of the part's dollars on this economy).</summary>
        void Taxes(Regimes.Regime r, float rev, double gdp, double[] steel)
        {
            if (r.Mix == null) return;
            var b = new LineMeshBuilder(); var players = snapshot.Players.Players; int n = land.Hills.Length;
            float top = HillLandscape.Bedrock;
            double D(Regimes.Part p) => r.Mix[(int)p] * rev * gdp;   // $B on this economy
            float W(double dollars) => .4f + Mathf.Sqrt((float)Math.Max(0, dollars)) * .05f;
            void Drop(Vector3 from, Color c, double dollars, float phase, float bend = 0)
            {
                if (dollars < 1) return;
                Vector3 to = new Vector3(from.x + bend, top - .5f, Mathf.Min(from.z, land.CrestZ - 1));
                var pts = new LinePoint[14];
                for (int k = 0; k < pts.Length; k++) pts[k] = new LinePoint(Vector3.Lerp(from, to, k / 13f), WithAlpha(c, .75f), W(dollars), 0, 1.8f);
                b.AddFlowPath(pts, 0, .45f, phase);
            }
            // income taxes and contributions from the people (every fifth group, by what each pays)
            double paid = 0; for (int p = 0; p < players.Length; p++) paid += Math.Max(0, players[p].Taxes);
            double people = D(Regimes.Part.Personal) + D(Regimes.Part.Social);
            if (paid > 0)
                for (int p = 0; p < players.Length; p += 5)
                {
                    double share = 0; for (int q = p; q < Math.Min(players.Length, p + 5); q++) share += Math.Max(0, players[q].Taxes);
                    Drop(land.People[p], PartColor[(int)Regimes.Part.Personal], people * share / paid, p * .07f);
                }
            // taxes on spending from each market, by its value added; profit taxes from the private summits, state
            // enterprises' profits from the steel ones, by their owners' surplus
            double va = 0, privateOwners = 0, stateOwners = 0;
            for (int j = 0; j < n; j++)
            {
                if (land.Hills[j].Tier == 0) continue;
                var sector = snapshot.Land.Sectors[j]; double o = Math.Max(0, sector.Owners);
                va += Math.Max(0, sector.ValueAdded); privateOwners += o * (1 - steel[j]); stateOwners += o * steel[j];
            }
            for (int j = 0; j < n; j++)
            {
                if (land.Hills[j].Tier == 0) continue;
                var sector = snapshot.Land.Sectors[j]; double o = Math.Max(0, sector.Owners);
                if (va > 0) Drop(land.Market(j) + Vector3.up * .4f, PartColor[(int)Regimes.Part.Goods], D(Regimes.Part.Goods) * Math.Max(0, sector.ValueAdded) / va, j * .21f, -1.5f);
                if (privateOwners > 0) Drop(land.Halo(j), PartColor[(int)Regimes.Part.Corporate], D(Regimes.Part.Corporate) * o * (1 - steel[j]) / privateOwners, j * .23f, 1.5f);
                if (stateOwners > 0) Drop(land.Halo(j), PartColor[(int)Regimes.Part.Enterprises], D(Regimes.Part.Enterprises) * o * steel[j] / stateOwners, j * .29f, 2.5f);
            }
            // property taxes from homes (the real estate hill), resource revenue up out of the ground under the raw hills
            var data = LandService.Model.Data;
            var estate = data.IndustryById("real_estate");
            if (estate != null) Drop(land.Market(estate.Index) + Vector3.up * .8f, PartColor[(int)Regimes.Part.Property], D(Regimes.Part.Property), .4f, 2);
            double resources = D(Regimes.Part.Resources);
            if (resources >= 1)
            {
                var ids = new[] { "oil_gas", "mining_metals" }; var weights = new[] { .85, .15 };
                for (int i = 0; i < ids.Length; i++)
                {
                    var ind = data.IndustryById(ids[i]); if (ind == null) continue;
                    var hill = land.Hills[ind.Index];
                    Vector3 from = new Vector3(hill.Center.x, land.Ground(hill.Center.x, land.CrestZ - 2) - 1, land.CrestZ - 2);
                    Drop(from, Oil, resources * weights[i], i * .5f);
                    double share = r.Mix[(int)Regimes.Part.Resources];
                    if (i == 0) Label("<b>OIL AND RESOURCES</b> " + LandFacts.Percent(share) + " of revenue" + (share >= .4 ? ": the state lives on the ground more than on its people" : ""),
                        from + new Vector3(0, 7, 3), 1.05f, Color.Lerp(Oil, White, .4f));
                }
            }
            Lines(b, Part.Taxes, true);
        }

        /// <summary>Votes: white threads from the people, across the whole crest, down into the state, as wide and bright as the
        /// electoral democracy index.</summary>
        void Votes(Regimes.Regime r)
        {
            if (!r.Democracy.Has) return;
            var b = new LineMeshBuilder(); float v = (float)r.Democracy.Value;
            var players = snapshot.Players.Players; var order = new List<int>();
            for (int p = 0; p < players.Length; p++) if (!land.Cloud[p]) order.Add(p);
            order.Sort((a, c) => land.People[a].x.CompareTo(land.People[c].x));
            const int Threads = 14;
            for (int k = 0; k < Threads && order.Count > 0; k++)
            {
                Vector3 at = land.People[order[(int)((k + .5f) / Threads * order.Count)]];
                Vector3 from = at + Vector3.up * 1.5f, to = new Vector3(at.x, HillLandscape.Bedrock + .2f, Mathf.Min(at.z, land.CrestZ - .5f));
                var pts = new LinePoint[16];
                for (int i = 0; i < pts.Length; i++) pts[i] = new LinePoint(Vector3.Lerp(from, to, i / 15f), WithAlpha(White, .2f + .7f * v), .3f + 3.2f * v, 0, .8f + 2.4f * v);
                b.AddFlowPath(pts, 0, .5f, k * .3f);
            }
            string prev = double.IsNaN(r.Democracy.Previous) ? "" : " (" + r.Democracy.Previous.ToString("0.00", LandFacts.Ci) + " in " + r.Democracy.PreviousYear + ")";
            Label("<b>VOTES</b> · electoral democracy " + r.Democracy.Value.ToString("0.00", LandFacts.Ci) + " of 1 in " + r.Democracy.Year + prev +
                  "\n<size=80%>white threads from the people into the state, as strong as their choice reaches it (V-Dem)</size>",
                  new Vector3(LabelX - 40, HillLandscape.Bedrock + 6, land.CrestZ + 3), 1f, White);
            Lines(b, Part.Votes, true);
        }

        /// <summary>Trade across the far border: flows out above (gold) and in below (ice), each lane as wide as half of exports
        /// plus imports over GDP.</summary>
        void Trade(Regimes.Regime r)
        {
            var b = new LineMeshBuilder(); float x = land.XMax + 6, z = land.CrestZ - 4;
            float h = HillLandscape.Bedrock + 10;
            b.AddSegment(new Vector3(x, HillLandscape.Bedrock, z), new Vector3(x, h + 24, z), WithAlpha(Steel, .6f), 1.4f, 0, 0, 1.6f);
            if (r.Trade.Has)
            {
                float t = (float)r.Trade.Value, w = 1 + 8 * t;
                for (int dir = 0; dir < 2; dir++)
                {
                    float y = h + (dir == 0 ? 15 : 6);
                    var pts = new LinePoint[20];
                    for (int k = 0; k < pts.Length; k++)
                    {
                        float s = k / 19f, xx = dir == 0 ? Mathf.Lerp(x - 20, x + 20, s) : Mathf.Lerp(x + 20, x - 20, s);
                        pts[k] = new LinePoint(new Vector3(xx, y + Mathf.Sin(s * Mathf.PI) * 2.5f, z), WithAlpha(dir == 0 ? Gold : Ice, .85f), w * .5f, 0, 2.2f);
                    }
                    b.AddFlowPath(pts, 0, .4f, dir * .5f);
                }
                Label("<b>TRADE</b> · exports + imports " + LandFacts.Percent(t) + " of GDP in " + r.Trade.Year + "\n<size=80%>out above (gold), in below (ice), across the border</size>",
                      new Vector3(x, h + 30, z), 1f, Color.Lerp(Steel, White, .5f));
            }
            else Label("<b>TRADE</b> · no defensible measure found", new Vector3(x, h + 30, z), 1f, Grey);
            Lines(b, Part.Trade, true);
        }

        /// <summary>The top 1%: a gold ring as large as their share of wealth, a white ring inside as large as their share
        /// of income (both: area proportional to the share).</summary>
        void Cloud(Regimes.Regime r)
        {
            var b = new LineMeshBuilder(); Vector3 c = land.CloudCenter + new Vector3(0, 4, 26);
            const float Scale = 22;
            if (r.Top1Wealth.Has) Ring(b, c, Mathf.Sqrt((float)r.Top1Wealth.Value) * Scale, WithAlpha(Gold, .95f), 3.2f, 3);
            else Dashed(b, c, Mathf.Sqrt(.3f) * Scale, WithAlpha(Grey, .5f));
            if (r.Top1Income.Has) Ring(b, c, Mathf.Sqrt((float)r.Top1Income.Value) * Scale, WithAlpha(White, .9f), 2.2f, 2.4f);
            string wealth = r.Top1Wealth.Has ? LandFacts.Percent(r.Top1Wealth.Value) + " of wealth (" + r.Top1Wealth.Year + ")" : "wealth not measured";
            string income = r.Top1Income.Has ? LandFacts.Percent(r.Top1Income.Value) + " of income (" + r.Top1Income.Year + ")" : "income not measured";
            Label("<b>THE TOP 1%</b> · " + wealth + " · " + income + "\n<size=80%>gold ring: their share of wealth · white: of income (area)</size>",
                  c + Vector3.up * (Scale * .62f + 5), 1f, Color.Lerp(Gold, White, .45f));
            Lines(b, Part.Cloud, false);
        }

        // ------------------------------------------------------------------ text

        static string PartName(int k) => k switch
        {
            0 => "personal income taxes", 1 => "taxes on profits", 2 => "social contributions", 3 => "taxes on goods & services",
            4 => "property taxes", 5 => "oil & resource revenue", 6 => "state enterprises' profits", _ => "other revenue"
        };

        static string Pct(Regimes.Measure m) => m.Has ? LandFacts.Percent(m.Value, 1) : "not found";

        static string Money(double billions) => LandFacts.Money(billions);

        static int Production(Data.EconomyData data) { int n = 0; foreach (var i in data.Industries) if (i.TierIndex > 0) n++; return n; }

        static int CountSteel(Regimes.Regime r, Data.EconomyData data)
        {
            int n = 0; for (int j = 0; j < data.Industries.Count; j++) if (data.Industries[j].TierIndex > 0 && r.StateShare(j) >= .5) n++;
            return n;
        }

        /// <summary>The panel's account of the regime: what is measured (each with its year and source), what Jev judged.</summary>
        static string Readout(Regimes.Regime r, Regimes.Regime us, Data.EconomyData data)
        {
            var s = new StringBuilder();
            string Year(Regimes.Measure m) => m.Has ? " <color=#8A93A0>(" + m.Year + ")</color>" : "";
            string Vs(Regimes.Measure m, Regimes.Measure u, bool pct = true) => r.Baseline || u == null || !u.Has ? "" : " <color=#8A93A0>· US " + (pct ? LandFacts.Percent(u.Value, 1) : u.Value.ToString("0.00", LandFacts.Ci)) + "</color>";
            s.Append("<b>").Append(r.Name).Append("</b> · ").Append(r.Form).Append(" · ").Append(r.Era).Append('\n');
            s.Append("<color=#B9C7DA><b>MEASURED</b></color>\n");
            s.Append("Government spending <b>").Append(Pct(r.Spending)).Append("</b> of GDP").Append(Year(r.Spending)).Append(Vs(r.Spending, us?.Spending)).Append('\n');
            s.Append("Revenue <b>").Append(Pct(r.Revenue)).Append("</b>").Append(Year(r.Revenue)).Append(Vs(r.Revenue, us?.Revenue)).Append('\n');
            if (r.Mix != null)
            {
                s.Append("It lives on: ");
                var order = new List<int>(); for (int k = 0; k < Regimes.Parts; k++) if (r.Mix[k] >= .02) order.Add(k);
                order.Sort((a, b) => r.Mix[b].CompareTo(r.Mix[a]));
                for (int i = 0; i < order.Count; i++) s.Append(i > 0 ? " · " : "").Append(PartName(order[i])).Append(' ').Append(LandFacts.Percent(r.Mix[order[i]]));
                s.Append(" <color=#8A93A0>(").Append(r.MixYear).Append(r.MixBasis == "tax" ? ", tax shares scaled to revenue" : "").Append(")</color>\n");
            }
            else s.Append("Its revenue mix: not found\n");
            if (r.StateFirms.Has)
                s.Append("The state's share of listed firms' value <b>").Append(LandFacts.Percent(r.StateFirms.Value)).Append("</b>").Append(Year(r.StateFirms))
                 .Append(r.Baseline || us == null || !us.StateFirms.Has ? "" : " <color=#8A93A0>· US " + LandFacts.Percent(us.StateFirms.Value) + "</color>").Append('\n');
            else s.Append("The state's share of listed firms' value: not found\n");
            if (r.Fortune.Has)
                s.Append("Fortune Global 500: <b>").Append(r.Fortune.Count).Append("</b> of its firms state-controlled").Append(Year(r.Fortune)).Append('\n');
            // the summits are each industry's largest named firm; the state's other holdings do not color a summit
            var summits = new List<string>(); var holdings = new List<string>();
            foreach (var f in r.Firms)
            {
                if (f.Industry < 0) continue;
                string share = f.Share <= 0 ? "private" : double.IsNaN(f.StateShare) ? "state-controlled" : LandFacts.Percent(f.StateShare) + " state";
                if (f.Leads) summits.Add(f.Name + " " + share);
                else if (f.Share > 0) holdings.Add(f.Name + " " + share);
            }
            if (summits.Count > 0) s.Append("Summits (each industry's largest named firm): ").Append(string.Join(" · ", summits)).Append('\n');
            if (r.DefaultOwnership >= .5) s.Append("Every other summit: the state's (").Append(r.DefaultNote).Append(")\n");
            if (holdings.Count > 0) s.Append("The state's other holdings: ").Append(string.Join(" · ", holdings)).Append('\n');
            s.Append("Trade <b>").Append(Pct(r.Trade)).Append("</b> of GDP").Append(Year(r.Trade)).Append(Vs(r.Trade, us?.Trade)).Append('\n');
            string modelled = r.Top1Wealth.Has || double.IsNaN(r.Top1Wealth.Modelled) ? "" : " <color=#8A93A0>(WID models " + LandFacts.Percent(r.Top1Wealth.Modelled, 1) + " for " + r.Top1Wealth.ModelledYear + "; not drawn)</color>";
            s.Append("Top 1%: <b>").Append(r.Top1Wealth.Has ? LandFacts.Percent(r.Top1Wealth.Value, 1) : "not measured").Append("</b> of wealth").Append(Year(r.Top1Wealth)).Append(modelled)
             .Append(" · <b>").Append(Pct(r.Top1Income)).Append("</b> of income").Append(Year(r.Top1Income)).Append('\n');
            if (r.Democracy.Has)
                s.Append("Electoral democracy <b>").Append(r.Democracy.Value.ToString("0.00", LandFacts.Ci)).Append("</b> of 1").Append(Year(r.Democracy))
                 .Append(double.IsNaN(r.Democracy.Previous) ? "" : " <color=#8A93A0>· " + r.Democracy.Previous.ToString("0.00", LandFacts.Ci) + " in " + r.Democracy.PreviousYear + "</color>").Append(Vs(r.Democracy, us?.Democracy, false)).Append('\n');
            s.Append("\n<color=#B9C7DA><b>JUDGED</b></color>\n");
            s.Append(r.Judged != null ? "Regulation (grey) and capture (gold) strands: " + r.Judged + "\n" : "Regulation and capture: not judged for this regime\n");
            if (!string.IsNullOrEmpty(r.Notes)) s.Append("\n<color=#8A93A0>").Append(r.Notes).Append("</color>\n");
            s.Append("\n<color=#8A93A0>Sources and quotes: Tools/regimes/sources.json; built ").Append(Regimes.Get()?.Built).Append("</color>");
            return s.ToString();
        }

        /// <summary>The drawing's own choices, so the panel can say what is not data.</summary>
        static string Drawing(Regimes.Regime r) =>
            "<color=#B9C7DA><b>THE DRAWING'S CHOICES</b></color>\n" +
            "The hills, people and money are the United States' (BEA); only the foundation changes.\n" +
            "Bedrock depth = spending (20% of GDP = 14 units, the federal block's scale); strata = the revenue mix; white line = revenue.\n" +
            "Summit rings: steel = the state's share of the industry's largest named firm (by revenue); summits with no firm named are " + (r.DefaultOwnership >= .5 ? "the state's by law" : "private") + ".\n" +
            "Steel lanes: the state's share of all spending, drawn through every river alike.\n" +
            "What it lives on falls in from where it comes: " + string.Join(", ", Regimes.PartFrom, 0, 7) + ".\n" +
            "Votes, trade, the 1% rings: widths and areas from the measures above.";

        // ------------------------------------------------------------------ drawing helpers

        /// <summary>A box of the bedrock: its front face at the crest, its top, and its two ends back along the wake.</summary>
        static void Slab(SurfaceMeshBuilder b, float x0, float x1, float yTop, float yBottom, float zFront, float zBack, Color color, float alpha)
        {
            Color front = color; front.a = alpha; Color side = color; side.a = alpha * .45f;
            b.AddBand(new[] { new Vector3(x0, yTop, zFront), new Vector3(x1, yTop, zFront) }, new[] { new Vector3(x0, yBottom, zFront), new Vector3(x1, yBottom, zFront) }, new[] { (Color32)front }, 0, 1);
            foreach (float x in new[] { x0, x1 })
                b.AddBand(new[] { new Vector3(x, yTop, zFront), new Vector3(x, yTop, zBack) }, new[] { new Vector3(x, yBottom, zFront), new Vector3(x, yBottom, zBack) }, new[] { (Color32)side }, 0, 1);
        }

        static void Strand(LineMeshBuilder b, Vector3 a, Vector3 z, Color color, float width, float intensity, float bow)
        {
            var pts = new LinePoint[21];
            for (int s = 0; s < pts.Length; s++) { float t = s / 20f; pts[s] = new LinePoint(Vector3.Lerp(a, z, t) + Vector3.right * Mathf.Sin(t * Mathf.PI) * bow, color, width, 0, intensity); }
            b.AddFlowPath(pts, 0, .4f);
        }

        /// <summary>An arc of a ring facing the crest's front (the x-y plane) from fraction f0 to f1 of the circle,
        /// clockwise from the top.</summary>
        static void Arc(LineMeshBuilder b, Vector3 c, float r, float f0, float f1, Color color, float width, float intensity)
        {
            if (f1 - f0 < .004f) return;
            int n = Mathf.Max(3, Mathf.CeilToInt((f1 - f0) * 64));
            var pts = new Vector3[n + 1];
            for (int s = 0; s <= n; s++) { float a = Mathf.PI * .5f - Mathf.Lerp(f0, f1, s / (float)n) * Mathf.PI * 2; pts[s] = c + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0); }
            b.AddPolyline(pts, color, width, 0, 0, intensity);
        }

        /// <summary>A ring facing the camera's side of the wave (in the x-y plane).</summary>
        static void Ring(LineMeshBuilder b, Vector3 c, float r, Color color, float width, float intensity)
        {
            var pts = new Vector3[65];
            for (int s = 0; s < pts.Length; s++) { float a = s * Mathf.PI * 2 / 64; pts[s] = c + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0); }
            b.AddPolyline(pts, color, width, 0, 0, intensity);
        }

        static void Dashed(LineMeshBuilder b, Vector3 c, float r, Color color)
        {
            for (int s = 0; s < 32; s += 2)
            {
                float a0 = s * Mathf.PI * 2 / 32, a1 = (s + 1) * Mathf.PI * 2 / 32;
                b.AddSegment(c + new Vector3(Mathf.Cos(a0) * r, Mathf.Sin(a0) * r, 0), c + new Vector3(Mathf.Cos(a1) * r, Mathf.Sin(a1) * r, 0), color, 1.4f, 0, 0, 1.4f);
            }
        }

        MeshRenderer Lines(LineMeshBuilder b, Part part, bool flow)
        {
            Mesh mesh = b.ToMesh("Regime " + part); owned.Add(mesh);
            Material m = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3242, true, 0, flow ? 1 : 0)); owned.Add(m);
            if (flow) { m.SetFloat("_FlowFreq", 1.2f); m.SetFloat("_FlowSpeed", .45f); }
            var r = AddMesh("Regime " + part, mesh, m); r.transform.SetParent(content.transform, false);
            renderers.Add((r, part)); return r;
        }

        void Surface(SurfaceMeshBuilder b, Part part)
        {
            Mesh mesh = b.ToMesh("Regime " + part); owned.Add(mesh);
            Material m = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1, 3240)); owned.Add(m);
            var r = AddMesh("Regime " + part, mesh, m); r.transform.SetParent(content.transform, false);
            renderers.Add((r, part));
        }

        void Label(string text, Vector3 at, float size, Color color)
        {
            var go = new GameObject("Regime label"); go.transform.SetParent(content.transform, false); go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>(); t.text = text; t.fontSize = 24; t.alignment = TextAlignmentOptions.Center; t.color = color;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.sizeDelta = new Vector2(90, 8);
            go.transform.localScale = Vector3.one * size; labels.Add(t);
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        void Clear()
        {
            foreach (var o in owned) if (o) Destroy(o);
            if (content) Destroy(content);
            owned.Clear(); renderers.Clear(); labels.Clear(); content = null;
        }

        void OnDestroy() { Clear(); Shown = -1; ReadoutText = DrawingText = ""; }
    }
}

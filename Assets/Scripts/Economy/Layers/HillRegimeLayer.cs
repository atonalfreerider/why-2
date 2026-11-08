using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using Why.Economy.Emphasis;
using Why.Economy.Land;
using Why.Economy.Model;

namespace Why.Economy.Layers
{
    /// <summary>
    /// Other governments as model changes (view "regimes"; the data is <see cref="Regimes"/>): a thought experiment that
    /// stands the same hills - the United States' industries as BEA measures them - on another state's foundation, drawn
    /// from that state's measured structure. Only the foundation and its reach change; the hills, the people and the money
    /// they make stay the US's, so the title asks "what if" and the caption sets the country's own economy beside them.
    /// <list type="bullet">
    /// <item><b>The bedrock</b> under the crest is as deep as the regime's government spending at all levels (IMF general
    /// government; the same scale as the federal block of the other views: 20% of GDP is 14 units), in strata of what it
    /// lives on (its revenue mix: income and profit taxes, social contributions, taxes on spending, property, oil and other
    /// resource revenue, the profits of state enterprises, the rest), a white line at its revenue and the gap below it
    /// borrowed. A thin line marks the United States' depth for comparison; on the US's own bedrock a dashed line marks
    /// federal outlays alone, the block the other views draw.</item>
    /// <item><b>Summits.</b> Each hill's summit carries a ring split steel and gold: steel is the state's share of the
    /// leading firm ranked there (the file names each state's holdings, with owner and source), gold the private owners';
    /// in the Soviet Union every summit was the state's by law. A summit no firm is ranked for is a grey dotted ring, not
    /// ranked (steel dotted where the largest firm the research names there is the state's). Steel beams rise from the
    /// bedrock into the summits the state owns: under state capitalism the state does not only lie beneath the hills, it
    /// owns their tops. A steel gauge beside them gives the state's share of the value of the country's listed firms.</item>
    /// <item><b>Strands.</b> Grey up into each hill as strongly as the state's rules bind it, gold back down as strongly as
    /// the industry shapes them (Jev's judgments for the regime; the US keeps the foundation view's own).</item>
    /// <item><b>What the state lives on</b> falls into it from where it comes: income taxes and contributions from the
    /// people, taxes on spending from the markets, profit taxes from the private summits, state enterprises' profits from
    /// the steel summits, resource revenue from the ground under the raw hills.</item>
    /// <item><b>Votes</b> run down from the people into the state as wide as the electoral democracy index (V-Dem);
    /// <b>trade</b> crosses the far border as wide as exports plus imports over GDP (World Bank); the top 1%'s gold ring
    /// above the hills is as large as their share of wealth, a white ring inside it their share of income (WID).</item>
    /// </list>
    /// The regime shown is pinned by <see cref="UI.RegimePanel"/>, or by the tour stop's cues as its card names each state
    /// (through <see cref="ViewVariants"/>); unpinned, the view steps through them (faster during a tour stop without
    /// cues, holding the last). Every number on screen is measured (with its year in the panel) except the strands
    /// (Jev) and the drawing's own choices, which the panel lists under its sources and method. Every mark is drawn at
    /// intensity 1 and blended (the bloom starts above 1 and is kept for what the pointer holds), so steel, gold and the
    /// strata's tints stay apart where lines cross.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillRegimeLayer : GraphLayer
    {
        public const string View = "regimes";
        public override int Order => 65;
        public override IEnumerable<string> RequiredTexts => new[] { Regimes.Path };
        enum Part { Bedrock, Strata, Summits, Beams, Strands, Taxes, Votes, Trade, Cloud, Count }
        /// <summary>Each part's emphasis key, by <see cref="Part"/>: its alpha in every view and state is the emphasis table's
        /// (EmphasisTable: full in this view, absent elsewhere; the trade gauge steps aside while the tour runs), and its
        /// meshes carry the key's highlight ids.</summary>
        static readonly int[] Keys = EconomyEmphasis.Keys("regime", "bedrock", "strata", "summits", "beams", "strands", "taxes", "votes", "trade", "cloud");
        static float Id(Part part) => EconomyEmphasis.Id(Keys[(int)part]);
        /// <summary>How far a flow's pulse brightens a line (1 = the shader's fourfold): a sheen, under the bloom.</summary>
        const float Pulse = .2f;
        static readonly Color Gold = EconomyStyle.Capital, Steel = EconomyStyle.Government, White = new Color(1, .97f, .92f),
            Ice = new Color(.45f, .80f, 1), Oil = new Color(.86f, .56f, .20f), Grey = new Color(.80f, .78f, .74f);
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
        /// <summary>Seconds each regime stays on screen when none is pinned. The tour's stop pins each regime with its cues
        /// as the card names it, the Soviet Union last; a tour stop without cues falls back to shorter steps that stop on
        /// the last regime.</summary>
        const float CycleSeconds = 9, TourCycleSeconds = 5;
        /// <summary>The firm labels' smallest scale (about 11 px from the view's 300 units).</summary>
        const float FirmLabelScale = 1;
        /// <summary>Where the title, the gauge and the votes stand (land-local x): toward the hills' side, which the view
        /// frames on the left, clear of the panel on the right.</summary>
        const float LabelX = 150;
        /// <summary>Where the bedrock's names and caption stand (land-local x): a little toward the valley from the middle of
        /// its face, so the tour's card, docked at the left bottom over the face's left quarter, leaves the start of every
        /// line in view; the dashed lines' names stand 70 units further toward the valley, beside them.</summary>
        const float BedrockLabelX = 104;

        /// <summary>The regime pinned by the panel or a tour cue (index into <see cref="Regimes.All"/>; -1: step through them).</summary>
        public static int Pinned = -1;
        /// <summary>The regime on screen (index), its readout for the panel (three plain lines, then what is measured and
        /// judged), and its sources and method (the drawing's choices, files, model versions, links), which the panel
        /// keeps folded.</summary>
        public static int Shown { get; private set; } = -1;
        public static string ReadoutText { get; private set; } = "";
        public static string DrawingText { get; private set; } = "";

        readonly List<(Renderer renderer, Part part)> renderers = new List<(Renderer, Part)>();
        readonly List<TextMeshPro> labels = new List<TextMeshPro>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        string json;
        LandSnapshot snapshot; HillLandscape land; Regimes regimes; GameObject content;
        /// <summary>The font's material drawn after the regime's marks (queue 3252), for every name the layer writes; and the
        /// dark plate behind the names read over gold, a line or the light strata (a unit square scaled to each name, and its
        /// material drawn under the summits' rings, the names' leaders (3251) and the raised text, queue 3250).</summary>
        Material raisedText, plateMaterial; Mesh plateQuad;
        /// <summary>The trade gauge's name, which steps aside with its lanes while the tour's card covers the far border.</summary>
        TextMeshPro tradeLabel;
        /// <summary>The names written on the bedrock's face, which lie in it instead of turning to the camera, and whether they
        /// are read from its front (the future's side, where the view stands) or from behind, the camera gone round.</summary>
        readonly List<TextMeshPro> inFace = new List<TextMeshPro>();
        bool faceFront = true;
        int built = -2;
        bool checkedOnce, wasOn;
        float enteredAt;

        public override void Prepare(GraphContext ctx) { json = ctx.Text(Regimes.Path); }
        public override void Upload(GraphContext ctx)
        {
            EconomyStage.Land().Place(transform);
            // the smoke test captures every regime (pinned in turn, then back to stepping through them), and the tour's
            // cues pin each by its id as the card names it
            ViewVariants.Register(View, () => Regimes.Get()?.All.Length ?? 0, k => Pinned = k, k => Regimes.Get()?.All[k].Id ?? k.ToString());
            EconomyEmphasis.Multiplier("regime.trade", "the tour's card", () => EmphasisTable.RegimeTourCard(GraphRoot.Instance != null && GraphRoot.Instance.TourActive));
        }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (HillLandscapeLayer.Current == null || LandService.Current == null) return;
            if (HillLandscapeLayer.Current.Source != LandService.Current) return;
            bool on = LandView.PresetId == View;
            if (!on) { wasOn = false; if (content) content.SetActive(false); return; }
            // stepping through starts at the baseline each time the view opens, in the order the tour's card names the states
            if (!wasOn) { wasOn = true; enteredAt = GraphTime.Now; }
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
            // on the graph's clock (GraphTime): real time, and in a capture the recording's own, so the tour's steps land on the same frames
            // (a capture's clock can run ahead of real time, so GraphTime.Now may step back when it ends: start the cycle again
            // rather than step to a negative state)
            float stepped = GraphTime.Now - enteredAt; if (stepped < 0) { enteredAt = GraphTime.Now; stepped = 0; } bool tour = GraphRoot.Instance != null && GraphRoot.Instance.TourActive;
            int shown = Pinned >= 0 && Pinned < n ? Pinned : tour ? Mathf.Min(n - 1, (int)(stepped / TourCycleSeconds)) : (int)(stepped / CycleSeconds) % n;
            if (snapshot != LandService.Current || land != HillLandscapeLayer.Current || shown != built)
            {
                if (!HillRebuilds.Take()) return;
                Build(shown);
            }
            if (content == null) return;
            content.SetActive(true);
            // the tour docks its card at the left bottom, over the far border where the trade gauge stands: seen through the
            // card, the gauge's words read as the card's own (the panel gives the same share), so the gauge steps aside (its
            // multiplier, EmphasisTable.RegimeTourCard)
            foreach (var (rend, part) in renderers) EconomyEmphasis.Apply(rend, Keys[(int)part]);
            if (tradeLabel) tradeLabel.gameObject.SetActive(!tour);
            foreach (var t in labels) t.transform.rotation = rig.Cam.transform.rotation;
            // the names written on the bedrock's face lie in it (Bedrock), read from the side of it the camera stands on
            bool front = content.transform.InverseTransformPoint(rig.Cam.transform.position).z >= land.CrestZ;
            if (front != faceFront) { faceFront = front; foreach (var t in inFace) t.transform.localRotation = Facing(front); }
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

            // The household rivers are not drawn here: split by spending over GDP they repeated the bedrock's depth and
            // claimed every purchase passes through the state.
            Bedrock(r, us, g, rev, gdp, x0, x1, top, cz);
            var steel = Summits(r, data, ResourceName(r, rev, gdp));
            Gauge(r);
            Strands(r);
            Taxes(r, rev, gdp, steel);
            Votes(r);
            Trade(r);
            Cloud(r);

            // the title above the crest: a thought experiment, not the country (its own economy is in the caption)
            string title = r.Baseline ? "<b>THE US ECONOMY ON ITS OWN STATE</b>" : "<b>WHAT IF: THE US ECONOMY ON " + Possessive(r).ToUpperInvariant() + " STATE</b>";
            string sub = r.Baseline ? "the United States · " + r.Form + ", measured " + r.Era + ": the baseline the thought experiments are set against"
                : r.Name + " · " + r.Form + ", measured " + r.Era + " · the hills, people and money stay the US's of " + land.Year + "; only the foundation and its reach change";
            Label(title + "\n<size=65%>" + sub + "</size>", new Vector3(LabelX, land.CloudY + 30, cz + 4), 1.9f, White);
            ReadoutText = Readout(r, us, data, gdp);
            DrawingText = Drawing(r);
            Debug.Log("[Why] Regime " + r.Name + " (" + r.Era + "): spending " + Pct(r.Spending) + ", revenue " + Pct(r.Revenue) + ", " +
                      CountSteel(r, data) + " of " + Production(data) + " summits the state's, " + unrankedShown + " not ranked, regulation and capture " + (r.Judged != null ? "judged" : "not judged") + ".");
        }

        static string Possessive(Regimes.Regime r) => r.Id == "su" ? "the Soviet" : r.Name.EndsWith("s") ? r.Name + "'" : r.Name + "'s";

        /// <summary>The regime's bedrock under the crest: as deep as its spending, in strata of its revenue mix, the revenue
        /// line and the borrowed gap beneath it, the US depth for comparison.</summary>
        void Bedrock(Regimes.Regime r, Regimes.Regime us, float g, float rev, double gdp, float x0, float x1, float top, float cz)
        {
            var faces = new SurfaceMeshBuilder(); var edges = new LineMeshBuilder(); var strata = new LineMeshBuilder();
            float zb = cz - Run, depth = Mathf.Max(g, rev) * DepthPerShare, bottom = top - depth;
            // the names written on the face (their extent across it, their height and half their height), which the dashed
            // comparison lines leave a gap for (Dashes)
            var written = new List<(float x0, float x1, float y, float half)>();
            // A name written on the face lies in it (Facing), in the plane of its revenue line, instead of turning to the
            // camera: the face is seen in perspective, its lines sloping some 4 px in 100, and a name that faced the camera
            // stayed level and ran out of a thin stratum at its ends, over the next one or across the revenue line (which
            // struck OTHER REVENUE through). Lying in the face, it slopes with its stratum in every pose and stays inside it
            // (a stratum's capitals, .69 em, are at most .6 of its depth). A name below size 1 (a thin stratum's, some 6 px
            // capitals) is bold throughout, and measured bold for the gaps left across it: its figures' regular strokes,
            // under a pixel wide, reached under 3:1 on the stratum where its bold name reached 3.3 to 5:1.
            void FaceName(string text, float at, float size, Color color)
            {
                var t = Label(text, new Vector3(BedrockLabelX, at, cz + .3f), size, color);
                labels.Remove(t); inFace.Add(t); t.transform.localRotation = Facing(faceFront);
                if (size < 1) t.fontStyle = FontStyles.Bold;
                Vector2 half = t.GetPreferredValues(text) * size * .5f;
                written.Add((BedrockLabelX - half.x - 1, BedrockLabelX + half.x + 1, at, half.y));
            }
            // a stratum's name, in the ink that reads further from the stratum (Ink: dark on every one of them)
            void StratumName(string text, float at, float size, Color tint, float alpha) => FaceName(text, at, size, Ink(tint, OnScreen(tint, alpha)));
            float y = top;
            if (r.Mix != null)
            {
                for (int k = 0; k < Regimes.Parts; k++)
                {
                    float d = (float)r.Mix[k] * rev * DepthPerShare; if (d <= .01f) continue;
                    Slab(faces, Id(Part.Bedrock), x0, x1, y, y - d, cz, zb, PartColor[k], .62f);
                    edges.AddPolyline(new[] { new Vector3(x0, y - d, cz), new Vector3(x1, y - d, cz) }, WithAlpha(White, .35f), 1, 0, Id(Part.Bedrock));
                    // a stratum names itself when it can hold a line of text (about 3% of GDP); the panel lists them all
                    if (d >= 2.2f)
                        StratumName("<b>" + PartName(k).ToUpperInvariant() + "</b>  " + LandFacts.Percent(r.Mix[k] * rev, 1) + " of GDP · " +
                                    LandFacts.Percent(r.Mix[k]) + " of revenue", y - d * .5f, Mathf.Clamp(d * .32f, .8f, 1.2f), PartColor[k], .62f);
                    y -= d;
                }
            }
            else
            {
                Slab(faces, Id(Part.Bedrock), x0, x1, top, top - rev * DepthPerShare, cz, zb, PartColor[(int)Regimes.Part.Other], .5f);
                StratumName("revenue " + LandFacts.Percent(rev, 1) + " of GDP (its sources not found)", top - rev * DepthPerShare * .5f, 1f, PartColor[(int)Regimes.Part.Other], .5f);
                y = top - rev * DepthPerShare;
            }
            // the revenue line, and what lies beneath it: borrowed (spending above revenue) or a surplus
            float yr = top - rev * DepthPerShare, yg = top - g * DepthPerShare;
            if (g > rev + .001f)
            {
                Slab(faces, Id(Part.Bedrock), x0, x1, yr, yg, cz, zb, new Color(.10f, .11f, .14f), .75f);
                // the band's name first, so its hatch leaves a gap across it as the dashed lines do (Dashes): its strokes
                // ran through the letters, and the one in the space before the figure read as a slash ("/7.8%")
                if (yr - yg >= 2.2f) FaceName("<b>BORROWED</b>  " + LandFacts.Percent(g - rev, 1) + " of GDP: spending above revenue", (yr + yg) * .5f, Mathf.Clamp((yr - yg) * .32f, .75f, 1.1f), Grey);
                for (float x = x0 + 4; x < x1; x += 8)
                {
                    bool clear = true;
                    foreach (var w in written) if (x + 4 > w.x0 && x < w.x1 && w.y < yr && w.y > yg) { clear = false; break; }
                    if (clear) strata.AddSegment(new Vector3(x, yr, cz + .2f), new Vector3(x + 4, yg, cz + .2f), WithAlpha(Grey, .25f), .8f, 0, Id(Part.Strata), 1);
                }
            }
            else if (rev > g + .001f && yg - yr >= 2.2f)
                FaceName("<b>SURPLUS</b>  revenue " + LandFacts.Percent(rev - g, 1) + " of GDP above spending (the steel line marks spending)", (yr + yg) * .5f, .9f, Grey);
            // A dashed line across the face, broken where it would strike through a name written there or run close along it:
            // within 1.8 units of a name's line (some 8 px in the view) it would read as the name's underline or overline.
            void Dashes(float at, Color color)
            {
                for (float x = x0; x < x1; x += 6)
                {
                    float end = Mathf.Min(x1, x + 3.4f); bool clear = true;
                    foreach (var w in written) if (end > w.x0 && x < w.x1 && Mathf.Abs(at - w.y) < w.half + 1.8f) { clear = false; break; }
                    if (clear) edges.AddSegment(new Vector3(x, at, cz + .5f), new Vector3(end, at, cz + .5f), color, 1.6f, 0, Id(Part.Bedrock));
                }
            }
            strata.AddPolyline(new[] { new Vector3(x0, yr, cz + .3f), new Vector3(x1, yr, cz + .3f) }, WithAlpha(White, .95f), 2, 0, Id(Part.Strata));
            strata.AddPolyline(new[] { new Vector3(x0, yg, cz + .3f), new Vector3(x1, yg, cz + .3f) }, WithAlpha(Steel, .9f), 2.4f, 0, Id(Part.Strata));
            // the depth in words beneath the bedrock (and beneath the US line and its name, when they run lower: 3.2 units
            // under the name, which stands 3 under its line, so some 10 px of black lie between the name and the caption's
            // first line, which can end just short of where the name starts): every level of government, and for another
            // state a counterfactual in dollars set beside the country's own economy; the lines' key takes a line of its
            // own (the first line stands where it stood with two), which keeps the widest line, with a rouble economy in it,
            // narrow enough to start right of the tour's card
            float caption = Mathf.Min(bottom, !r.Baseline && us != null && us.Spending.Has ? top - (float)us.Spending.Value * DepthPerShare - 6.2f : bottom) - 8f;
            string economy = "$" + (gdp / 1000).ToString("0.0", LandFacts.Ci) + "T";
            string words = r.Baseline
                ? "<b>ALL LEVELS OF US GOVERNMENT SPEND " + LandFacts.Percent(g, 1) + " OF " + r.Spending.Of + "</b> and take in " + LandFacts.Percent(rev, 1) +
                  "\n<size=75%>federal, state and local: " + LandFacts.Money(g * gdp, 1) + " of " + economy + "\nwhite line: revenue · steel line: spending</size>"
                : "<b>IF THE US SPENT LIKE " + Called(r).ToUpperInvariant() + ": " + LandFacts.Percent(g, 1) + " OF " + r.Spending.Of + ", " + LandFacts.Money(g * gdp, 1) + "</b>" +
                  "\n<size=75%>all levels of government, of the US's " + economy + " · " + OwnEconomy(r) + "\nwhite line: revenue · steel line: spending</size>";
            Label(words, new Vector3(BedrockLabelX, caption, cz + .8f), 1.45f, Color.Lerp(Steel, White, .5f));
            // the US for comparison, named toward the valley from the bedrock's names (the caption stands beneath its name)
            if (!r.Baseline && us != null && us.Spending.Has)
            {
                float yu = top - (float)us.Spending.Value * DepthPerShare, at = yu - 3;
                Dashes(yu, WithAlpha(White, .8f));
                // Where this state's bedrock runs deeper than the name stands, the name lies on its face, white on the light
                // strata (2.6:1) with their edges through it: there it stands on a plate. Under a shallower bedrock it stands
                // on black and needs none (a plate there would only box it). It stands 3 units under its line (the caption
                // beneath it is placed for that): the line slopes across the level plate, which at 2.2 met its dashes at the
                // plate's right end.
                var usName = Label("- - the United States: " + LandFacts.Percent(us.Spending.Value, 1), new Vector3(BedrockLabelX - 70, at, cz + .8f), 1, White);
                if (bottom < at + 1.7f) Plate(usName);
            }
            // The US's own bedrock is general government; the block of the other views is federal outlays alone (the
            // foundation view's crest year), drawn as a dashed line inside it so the change of depth between them reads.
            if (r.Baseline && HillFoundationLayer.History != null)
            {
                var federal = default(Why.Humans.GovernmentHistory.UsYear);
                foreach (var row in HillFoundationLayer.History.us) if (row.year <= land.Year && (federal == null || row.year > federal.year)) federal = row;
                if (federal != null && federal.Outlays > 0)
                {
                    float yf = top - federal.Outlays * DepthPerShare;
                    Dashes(yf, WithAlpha(Steel, .9f));
                    // its two lines stand between the dashes and the revenue line, which lie closer than two lines of text
                    // and slope across them: on a plate, the line its second line meets passes behind it
                    Plate(Label("- - federal outlays alone: " + LandFacts.Percent(federal.Outlays, 1) + " of GDP (" + federal.year + ")\n<size=80%>the government block of the other views</size>",
                          new Vector3(BedrockLabelX - 70, yf - 2.6f, cz + .8f), 1, Color.Lerp(Steel, White, .6f)));
                }
            }
            // the block's outline
            foreach (float yy in new[] { top, bottom })
                edges.AddPolyline(new[] { new Vector3(x0, yy, zb), new Vector3(x0, yy, cz), new Vector3(x1, yy, cz), new Vector3(x1, yy, zb) }, WithAlpha(Steel, .55f), 1.2f, 0, Id(Part.Bedrock));
            edges.AddSegment(new Vector3(x0, top, cz), new Vector3(x0, bottom, cz), WithAlpha(Steel, .55f), 1.2f, 0, Id(Part.Bedrock));
            edges.AddSegment(new Vector3(x1, top, cz), new Vector3(x1, bottom, cz), WithAlpha(Steel, .55f), 1.2f, 0, Id(Part.Bedrock));
            Surface(faces, Part.Bedrock); Lines(edges, Part.Bedrock, false); Lines(strata, Part.Strata, false);
        }

        /// <summary>Each hill's summit ring split steel (the state's share of the leading firm ranked there) and gold, with
        /// steel beams up from the bedrock into what the state owns; a summit nothing decides (no ranked firm, no law) is a
        /// dotted ring, not ranked, never a guessed "private". The firms' names stand above their rings on leaders and on
        /// plates, raised until they clear each other, the rings under their letters and the name already standing among them
        /// (<paramref name="standing"/>: the resource revenue's); the rings and leaders are drawn over the plates. Returns
        /// each industry's steel share.</summary>
        double[] Summits(Regimes.Regime r, Data.EconomyData data, (float x0, float x1, float y)? standing)
        {
            var rings = new LineMeshBuilder(); var beams = new LineMeshBuilder(); var leaders = new LineMeshBuilder();
            int n = land.Hills.Length; var steel = new double[n];
            var names = new List<(TextMeshPro text, Vector3 ring, float top, float width)>();
            // every summit's ring, named or not, which the names start clear of (Stack)
            var drawn = new List<(Vector3 center, float radius)>();
            unrankedShown = 0;
            for (int j = 0; j < n; j++)
            {
                var hill = land.Hills[j]; if (hill.Tier == 0) continue;
                double s = steel[j] = r.StateShare(j);
                double owners = Math.Max(0, snapshot.Land.Sectors[j].Owners);
                float radius = 1.5f + Mathf.Sqrt((float)owners) * .16f;
                Vector3 h = land.Halo(j); drawn.Add((h, radius));
                var firm = r.SummitFirm(j);
                if (!r.Decided(j))
                {
                    // steel dots where the largest firm the research names here is the state's, though none was ranked
                    var mark = r.UnrankedMark(j); unrankedShown++;
                    Dashed(rings, Id(Part.Summits), h, radius, WithAlpha(mark != null && mark.Controlled ? Steel : Grey, .75f), 1.6f);
                    if (mark != null)
                        names.Add(Name("<b>" + Short(mark.Firm) + "</b> · " + (mark.Controlled ? "state" : "private") + ", not ranked", h, radius, Color.Lerp(Steel, White, .45f)));
                    continue;
                }
                for (int ring = 0; ring < 2; ring++)
                {
                    float rr = radius * (ring == 0 ? 1 : .78f);
                    Arc(rings, Id(Part.Summits), h, rr, 0, (float)s, WithAlpha(Steel, .95f), 3);
                    Arc(rings, Id(Part.Summits), h, rr, (float)s, 1, WithAlpha(Gold, .9f), 2.4f);
                }
                if (s > .01)
                {
                    Vector3 foot = new Vector3(hill.Center.x, HillLandscape.Bedrock, land.CrestZ - 2.5f);
                    var pts = new LinePoint[24];
                    for (int k = 0; k < pts.Length; k++)
                    {
                        float t = k / 23f;
                        pts[k] = new LinePoint(Vector3.Lerp(foot, h, t) + Vector3.forward * Mathf.Sin(t * Mathf.PI) * 1.5f, WithAlpha(Steel, .8f), 1 + 4 * (float)s);
                    }
                    beams.AddFlowPath(pts, Id(Part.Beams), .35f, j * .2f);
                }
                if (firm != null)
                    names.Add(Name("<b>" + Short(firm.Name) + "</b> · " + Owner(firm), h, radius, s >= .5 ? Color.Lerp(Steel, White, .45f) : Color.Lerp(Gold, White, .45f)));
            }
            Stack(names, drawn, leaders, standing);
            // the rings and the leaders are drawn over the names' plates (3250) and under their letters (3252): under the
            // plates a ring a name stood on was a broken arc, and a leader was hidden where it crossed the plates of the names
            // stacked below its own, so which ring a name stood on could no longer be told
            Lines(rings, Part.Summits, false, 3251); Lines(beams, Part.Beams, true); Lines(leaders, Part.Summits, false, 3251);
            return steel;
        }

        /// <summary>The resource revenue's name, over the raw hills it comes out of, for a state that lives on oil (null:
        /// none): made before the firms' names, which stand clear of it. Returns its extent across the screen and its height.</summary>
        (float x0, float x1, float y)? ResourceName(Regimes.Regime r, float rev, double gdp)
        {
            var oil = LandService.Model.Data.IndustryById("oil_gas");
            if (r.Mix == null || oil == null) return null;
            double share = r.Mix[(int)Regimes.Part.Resources];
            if (share * rev * gdp < 1) return null;
            // above the oil hill's summit, high enough to clear its ring; the falls it names start under it (Taxes). The hill
            // stands near the face's valley end, so the long name is centered 5 units toward the hills (+x runs left on
            // screen): about 22 px between its end and the panel, which it otherwise met with no margin. It lies over the
            // hills' wireframe and a steel beam, so it stands on a plate like the firms' names around it; the leaders of the
            // names stacked above it still cross it, over the plate
            var hill = land.Hills[oil.Index];
            Vector3 at = new Vector3(hill.Center.x + 5, land.Ground(hill.Center.x, land.CrestZ - 2) + 8, land.CrestZ + 1);
            string text = "<b>OIL AND RESOURCES</b> " + LandFacts.Percent(share) + " of revenue" + (share >= .4 ? ": the state lives on the ground more than on its people" : "");
            const float Size = 1.05f;
            float half = Plate(Label(text, at, Size, Color.Lerp(Oil, White, .4f))).GetPreferredValues(text).x * Size * .5f;
            return (at.x - half, at.x + half, at.y);
        }

        /// <summary>Summits drawn not ranked in the regime on screen (the gauge's key counts them).</summary>
        int unrankedShown;

        /// <summary>A firm's name for a summit, measured (world width) so <see cref="Stack"/> can keep names apart. It stands on
        /// a plate: the names sit on gold, the wave's ridges running back from the clusters (HillWaveLayer), where gold
        /// letters, and the grey of a state's, keep little contrast. The summits' rings stay over the plate, and Stack
        /// starts the name clear of those under its letters.</summary>
        (TextMeshPro text, Vector3 ring, float top, float width) Name(string text, Vector3 ring, float radius, Color color)
        {
            var t = Plate(Label(text, ring + Vector3.up * (radius + 2.4f), FirmLabelScale, color));
            return (t, ring, ring.y + radius, t.GetPreferredValues(text).x * FirmLabelScale);
        }

        /// <summary>Stands each name above its ring, the lowest rings first, raised a line at a time until it clears every
        /// name placed before it and the one already <paramref name="standing"/>, with a leader down to its ring (the view
        /// looks along z, so x and y are the screen's). It starts clear of every ring under its letters where it first
        /// stands, not only its own (<paramref name="rings"/>: every summit's): a cluster's rings stand side by side at
        /// about one height, and a name started at its own ring's top stood on the higher ones beside it, gold letters on
        /// gold arcs. Only those: a ring that meets only its plate's margin, or one the clearing raises it into further up,
        /// is drawn over the plate and moves nothing (clearing those too threw names over the summits beside them).</summary>
        static void Stack(List<(TextMeshPro text, Vector3 ring, float top, float width)> names, List<(Vector3 center, float radius)> rings,
            LineMeshBuilder leaders, (float x0, float x1, float y)? standing)
        {
            // a line: the font's line height (1.15 em of 2.4 units, 2.76) and a little air; at 2.7 stacked names touched. A
            // name stands that far above a ring's top: its plate's half height (1.67), the ring's line and a little air
            const float Line = 3.2f * FirmLabelScale, Gap = 1.5f, Above = 2.2f;
            names.Sort((a, b) => a.top.CompareTo(b.top));
            var placed = new List<(float x0, float x1, float y)>();
            if (standing.HasValue) placed.Add(standing.Value);
            foreach (var (text, ring, top, width) in names)
            {
                float start = top + Above, y = start, half = width * .5f + Gap;
                foreach (var (center, radius) in rings)
                {
                    // the ring's highest and lowest points under the name's letters, against the name where it first stands
                    float dx = Mathf.Max(0, Mathf.Abs(center.x - ring.x) - width * .5f);
                    if (dx >= radius) continue;
                    float arc = Mathf.Sqrt(radius * radius - dx * dx);
                    if (center.y + arc > start - Above && center.y - arc < start + Above) y = Mathf.Max(y, center.y + arc + Above);
                }
                // a name raised to p.y + Line can land a rounding error short of a full line above p, so the test leaves
                // that much slack: without it the same name was raised to the same height forever (the regimes view hung)
                for (bool moved = true; moved;)
                {
                    moved = false;
                    foreach (var p in placed)
                        if (ring.x + half > p.x0 && ring.x - half < p.x1 && Mathf.Abs(y - p.y) < Line * .999f) { y = p.y + Line; moved = true; }
                }
                placed.Add((ring.x - half, ring.x + half, y));
                text.transform.localPosition = new Vector3(ring.x, y, ring.z);
                leaders.AddSegment(new Vector3(ring.x, top + .3f, ring.z), new Vector3(ring.x, y - Line * .45f, ring.z), WithAlpha(text.color, .55f), 1, 0, Id(Part.Summits), 1);
            }
        }

        /// <summary>A steel gauge of the state's share of the value of the country's listed firms (OECD; the USSR's: all of
        /// it, by law), above the summits on the hills' side: what rings of one firm each cannot say about the whole market.
        /// Its key also names the dotted rings.</summary>
        void Gauge(Regimes.Regime r)
        {
            var b = new LineMeshBuilder(); var m = r.StateFirms;
            // from the future +x runs to the left: the gauge fills from its left end (x0) toward x1
            float x0 = LabelX + 88, x1 = LabelX + 18, y = land.CloudY + 6, z = land.CrestZ + 4;
            float share = m.Has ? Mathf.Clamp01((float)m.Value) : 0;
            b.AddSegment(new Vector3(x0, y, z), new Vector3(x1, y, z), WithAlpha(Grey, .3f), 0, 1.8f, Id(Part.Summits), 1);
            if (share > 0) b.AddSegment(new Vector3(x0, y, z), new Vector3(Mathf.Lerp(x0, x1, share), y, z), WithAlpha(Steel, .95f), 0, 2.2f, Id(Part.Summits));
            string head = !m.Has ? "<b>THE STATE'S SHARE OF ITS LISTED FIRMS</b>: not found"
                : m.Basis == "by law" ? "<b>EVERY SUMMIT THE STATE'S</b> · " + (r.DefaultNote ?? "state property by law")
                : "<b>THE STATE HOLDS " + LandFacts.Percent(m.Value) + " OF THE VALUE OF " + (r.Baseline ? "US" : Possessive(r).ToUpperInvariant()) + " LISTED FIRMS</b> (" + m.Year + ")";
            string key = "steel: the state's share" + (unrankedShown > 0 ? " · dotted rings: " + unrankedShown + " summits no firm is ranked for" : "");
            // the key line stands about 2 px clear of the bar, which the view's yaw tilts up toward its right end: at 3.4
            // the letters' feet there, and the 'g' of "rings", ran into it
            Label(head + "\n<size=80%>" + key + "</size>", new Vector3((x0 + x1) * .5f, y + 4f, z), 1.1f, Color.Lerp(Steel, White, .5f));
            Lines(b, Part.Summits, false);
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
                Strand(b, Id(Part.Strands), foot, into, WithAlpha(Grey, .5f), .4f + 2.2f * reg, 1.2f);
                if (!float.IsNaN(cap) && cap > .05f) Strand(b, Id(Part.Strands), into + Vector3.right * 1.2f, foot + Vector3.right * 2.4f, WithAlpha(Gold, .5f), .3f + 2.2f * cap, -1.2f);
            }
            Lines(b, Part.Strands, true);
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
                for (int k = 0; k < pts.Length; k++) pts[k] = new LinePoint(Vector3.Lerp(from, to, k / 13f), WithAlpha(c, .5f), W(dollars));
                b.AddFlowPath(pts, Id(Part.Taxes), .45f, phase);
            }
            // income taxes and contributions from the people (every fifth group, by what each pays)
            double paid = 0; for (int p = 0; p < players.Length; p++) paid += Math.Max(0, players[p].Taxes);
            double people = D(Regimes.Part.Personal) + D(Regimes.Part.Social);
            if (paid > 0)
                for (int p = 0; p < players.Length; p += 10)
                {
                    double share = 0; for (int q = p; q < Math.Min(players.Length, p + 10); q++) share += Math.Max(0, players[q].Taxes);
                    Drop(land.People[p], PartColor[(int)Regimes.Part.Personal], people * share / paid, p * .07f);
                }
            // taxes on spending from the markets, by their value added; profit taxes from the private summits and state
            // enterprises' profits from the steel ones, by their owners' surplus
            double stateOwners = 0;
            for (int j = 0; j < n; j++) if (land.Hills[j].Tier > 0) stateOwners += Math.Max(0, snapshot.Land.Sectors[j].Owners) * steel[j];
            // drawn from the eight largest hills by value added (each part's width is still its share of the whole), so the
            // falls read as streams rather than a curtain
            var largest = new List<int>(); for (int j = 0; j < n; j++) if (land.Hills[j].Tier > 0) largest.Add(j);
            largest.Sort((a, c) => snapshot.Land.Sectors[c].ValueAdded.CompareTo(snapshot.Land.Sectors[a].ValueAdded));
            if (largest.Count > 8) largest.RemoveRange(8, largest.Count - 8);
            double vaShown = 0, privShown = 0, stateShown = 0;
            foreach (int j in largest) { var sec = snapshot.Land.Sectors[j]; double o = Math.Max(0, sec.Owners); vaShown += Math.Max(0, sec.ValueAdded); privShown += o * (1 - steel[j]); stateShown += o * steel[j]; }
            // the state enterprises' profits rise from every steel summit (there are few)
            for (int j = 0; j < n; j++)
            {
                if (land.Hills[j].Tier == 0 || steel[j] <= 0 || stateOwners <= 0) continue;
                Drop(land.Halo(j), PartColor[(int)Regimes.Part.Enterprises], D(Regimes.Part.Enterprises) * Math.Max(0, snapshot.Land.Sectors[j].Owners) * steel[j] / stateOwners, j * .29f, 2.5f);
            }
            foreach (int j in largest)
            {
                var sector = snapshot.Land.Sectors[j]; double o = Math.Max(0, sector.Owners);
                if (vaShown > 0) Drop(land.Market(j) + Vector3.up * .4f, PartColor[(int)Regimes.Part.Goods], D(Regimes.Part.Goods) * Math.Max(0, sector.ValueAdded) / vaShown, j * .21f, -1.5f);
                if (privShown > 0) Drop(land.Halo(j), PartColor[(int)Regimes.Part.Corporate], D(Regimes.Part.Corporate) * o * (1 - steel[j]) / privShown, j * .23f, 1.5f);
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
                    // the falls are named above the oil hill's summit (ResourceName)
                    Vector3 from = new Vector3(hill.Center.x, land.Ground(hill.Center.x, land.CrestZ - 2) - 1, land.CrestZ - 2);
                    Drop(from, Oil, resources * weights[i], i * .5f);
                }
            }
            Lines(b, Part.Taxes, true);
        }

        /// <summary>Votes: white threads from the people, across the whole crest, down into the state, as wide and as opaque as
        /// the electoral democracy index.</summary>
        void Votes(Regimes.Regime r)
        {
            if (!r.Democracy.Has) return;
            var b = new LineMeshBuilder(); float v = (float)r.Democracy.Value;
            var players = snapshot.Players.Players; var order = new List<int>();
            for (int p = 0; p < players.Length; p++) if (!land.Cloud[p]) order.Add(p);
            order.Sort((a, c) => land.People[a].x.CompareTo(land.People[c].x));
            const int Threads = 9;
            for (int k = 0; k < Threads && order.Count > 0; k++)
            {
                Vector3 at = land.People[order[(int)((k + .5f) / Threads * order.Count)]];
                Vector3 from = at + Vector3.up * 1.5f, to = new Vector3(at.x, HillLandscape.Bedrock + .2f, Mathf.Min(at.z, land.CrestZ - .5f));
                var pts = new LinePoint[16];
                for (int i = 0; i < pts.Length; i++) pts[i] = new LinePoint(Vector3.Lerp(from, to, i / 15f), WithAlpha(White, .2f + .7f * v), .3f + 3.2f * v);
                b.AddFlowPath(pts, Id(Part.Votes), .5f, k * .3f);
            }
            // the name stands across the threads it names and the falls of taxes beside them: on a plate they pass behind it
            string prev = double.IsNaN(r.Democracy.Previous) ? "" : " (" + r.Democracy.Previous.ToString("0.00", LandFacts.Ci) + " in " + r.Democracy.PreviousYear + ")";
            Plate(Label("<b>VOTES</b> · electoral democracy " + r.Democracy.Value.ToString("0.00", LandFacts.Ci) + " of 1 in " + r.Democracy.Year + prev +
                  "\n<size=80%>white threads from the people into the state, as strong as their choice reaches it (V-Dem)</size>",
                  new Vector3(LabelX - 40, HillLandscape.Bedrock + 6, land.CrestZ + 3), 1f, White));
            Lines(b, Part.Votes, true);
        }

        /// <summary>Trade across the far border: flows out above (gold) and in below (ice), each lane as wide as half of exports
        /// plus imports over GDP. Its name, centred over the lanes, reaches over the end of the hills, whose first outline ran
        /// up through its words ("GDP| in 2024"): it stands on a plate, which steps aside with it during the tour.</summary>
        void Trade(Regimes.Regime r)
        {
            var b = new LineMeshBuilder(); float x = land.XMax + 6, z = land.CrestZ - 4;
            float h = HillLandscape.Bedrock + 10;
            b.AddSegment(new Vector3(x, HillLandscape.Bedrock, z), new Vector3(x, h + 24, z), WithAlpha(Steel, .6f), 1.4f, 0, Id(Part.Trade));
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
                        pts[k] = new LinePoint(new Vector3(xx, y + Mathf.Sin(s * Mathf.PI) * 2.5f, z), WithAlpha(dir == 0 ? Gold : Ice, .85f), w * .5f);
                    }
                    b.AddFlowPath(pts, Id(Part.Trade), .4f, dir * .5f);
                }
                tradeLabel = Plate(Label("<b>TRADE</b> · exports + imports " + LandFacts.Percent(t) + " of GDP in " + r.Trade.Year + "\n<size=80%>out above (gold), in below (ice), across the border</size>",
                      new Vector3(x, h + 30, z), 1f, Color.Lerp(Steel, White, .5f)));
            }
            else tradeLabel = Plate(Label("<b>TRADE</b> · no single defensible share (why: the panel)", new Vector3(x, h + 30, z), 1f, Grey));
            Lines(b, Part.Trade, true);
        }

        /// <summary>The top 1%: a gold ring as large as their share of wealth, a white ring inside as large as their share
        /// of income (both: area proportional to the share).</summary>
        void Cloud(Regimes.Regime r)
        {
            var b = new LineMeshBuilder(); Vector3 c = land.CloudCenter + new Vector3(0, 4, 26);
            const float Scale = 22;
            if (r.Top1Wealth.Has) Ring(b, Id(Part.Cloud), c, Mathf.Sqrt((float)r.Top1Wealth.Value) * Scale, WithAlpha(Gold, .95f), 3.2f);
            else Dashed(b, Id(Part.Cloud), c, Mathf.Sqrt(.3f) * Scale, WithAlpha(Grey, .5f));
            if (r.Top1Income.Has) Ring(b, Id(Part.Cloud), c, Mathf.Sqrt((float)r.Top1Income.Value) * Scale, WithAlpha(White, .9f), 2.2f);
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

        /// <summary>A revenue part in plain words, for the panel's opening lines.</summary>
        static string Plain(int k) => k switch
        {
            0 => "taxes on people's income", 1 => "taxes on company profits", 2 => "social insurance contributions from pay", 3 => "taxes on what people buy",
            4 => "property taxes", 5 => "selling oil and other resources", 6 => "the profits of state-owned firms", _ => "fees, interest and other revenue"
        };

        static string Pct(Regimes.Measure m) => m.Has ? LandFacts.Percent(m.Value, 1) : "not found";

        /// <summary>Who owns a firm, in a word or two: the state (with its stake when known), a minority stake, or private.</summary>
        static string Owner(Regimes.Firm f) => f.Controlled ? "state" + (double.IsNaN(f.StateShare) ? "" : " " + LandFacts.Percent(f.StateShare))
            : !double.IsNaN(f.StateShare) && f.StateShare > 0 ? LandFacts.Percent(f.StateShare) + " state" : "private";

        /// <summary>The regime as a sentence names it ("the Soviet Union", "Sweden").</summary>
        static string Called(Regimes.Regime r) => r.Id == "su" ? "the Soviet Union" : r.Name;

        /// <summary>The country's own economy in a few words ("Sweden's own economy: $610B (2024)").</summary>
        static string OwnEconomy(Regimes.Regime r)
        {
            string whose = r.Id == "su" ? "the Soviet Union's" : Possessive(r);
            var e = r.Economy; if (e == null || !e.Has) return whose + " own economy: not found";
            string size = e.Unit == "billion US dollars" ? LandFacts.Money(e.Value) : e.Value.ToString("0", LandFacts.Ci) + " " + e.Unit;
            return whose + " own economy: " + size + " (" + e.Year + ")";
        }

        /// <summary>How many of the country's firms the Fortune tally counts ("79 of 116": 116; 0 when absent).</summary>
        static int Listed(Regimes.Measure fortune)
        {
            if (fortune == null || string.IsNullOrEmpty(fortune.Count)) return 0;
            int of = fortune.Count.LastIndexOf(" of ", StringComparison.Ordinal);
            return of >= 0 && int.TryParse(fortune.Count.Substring(of + 4), out int n) ? n : 0;
        }

        static readonly System.Text.RegularExpressions.Regex Link = new System.Text.RegularExpressions.Regex(@",?\s*https?://[^\s;)]+");

        /// <summary>A note without its links, which go to the sources.</summary>
        static string WithoutLinks(string text, List<string> links)
        {
            foreach (System.Text.RegularExpressions.Match m in Link.Matches(text)) links.Add(m.Value.TrimStart(',', ' '));
            return Link.Replace(text, "");
        }

        /// <summary>The regime in three plain lines: who owns the biggest firms, who pays for the state, who chooses the
        /// government (the first two from the measures, the last in the drawing's words beside V-Dem's index).</summary>
        static string Opening(Regimes.Regime r)
        {
            var s = new StringBuilder(); var m = r.StateFirms;
            // the biggest firms: the Fortune tally where the country has enough firms on it to count, else its stock market
            bool tally = r.Fortune.Has && Listed(r.Fortune) >= 10;
            double share = tally ? r.Fortune.Value : m.Has ? m.Value : double.NaN;
            s.Append("<b>Who owns the biggest firms?</b> ");
            if (m.Has && m.Basis == "by law") s.Append("The state: the basic means of production were ").Append(r.DefaultNote ?? "state property by law").Append('.');
            else if (double.IsNaN(share)) s.Append("Not found.");
            else
            {
                s.Append(share > .5 ? "Mostly the state: it " : "Mostly private owners: the state ");
                if (tally) s.Append("controls ").Append(r.Fortune.Count.Replace(" of ", " of its ")).Append(" firms among the world's 500 largest");
                if (tally && m.Has) s.Append(" and ");
                if (m.Has) s.Append("holds ").Append(LandFacts.Percent(m.Value)).Append(" of the value of the country's listed firms");
                s.Append('.');
            }
            s.Append("\n<b>Who pays for the state?</b> ");
            if (r.Mix == null) s.Append("Where its revenue comes from was not found.");
            else
            {
                var order = new List<int>(); for (int k = 0; k < Regimes.Parts; k++) if (r.Mix[k] >= .02) order.Add(k);
                order.Sort((a, b) => r.Mix[b].CompareTo(r.Mix[a]));
                var parts = new List<string>(); double sum = 0;
                foreach (int k in order)
                {
                    parts.Add(Plain(k) + " (" + LandFacts.Percent(r.Mix[k]) + (parts.Count == 0 ? " of its revenue)" : ")"));
                    sum += r.Mix[k]; if (sum >= .5 || parts.Count == 3) break;
                }
                string last = parts[parts.Count - 1]; parts.RemoveAt(parts.Count - 1);
                s.Append("Above all ").Append(parts.Count > 0 ? string.Join(", ", parts) + " and " + last : last);
                if (r.Revenue.Has) s.Append("; it takes in ").Append(LandFacts.Percent(r.Revenue.Value, 1)).Append(" of ").Append(r.Revenue.Of);
                s.Append('.');
            }
            string chooser = string.IsNullOrEmpty(r.Chooses) ? "not said" : char.ToUpperInvariant(r.Chooses[0]) + r.Chooses.Substring(1);
            s.Append("\n<b>Who chooses the government?</b> ").Append(chooser);
            if (r.Democracy.Has) s.Append(" (electoral democracy ").Append(r.Democracy.Value.ToString("0.00", LandFacts.Ci)).Append(" of 1, V-Dem)");
            s.Append('.');
            return s.ToString();
        }

        /// <summary>A firm's name without its legal tail, for a label on a summit.</summary>
        static readonly Dictionary<string, string> ShortNames = new Dictionary<string, string>
        {
            { "Industrial & Commercial Bank of China", "ICBC" }, { "China State Construction Engineering", "CSCEC" },
            { "China National Petroleum", "CNPC" }, { "Exxon Mobil Holdings", "ExxonMobil" }, { "Verizon Communications", "Verizon" },
            { "Huawei Investment & Holding", "Huawei" }, { "China Mobile Communications", "China Mobile" }, { "United Parcel Service", "UPS" },
        };

        static string Short(string name)
        {
            if (ShortNames.TryGetValue(name, out string s)) return s;
            foreach (string tail in new[] { " Investment & Holding", " Holdings", " Holding Group", " Group", " Communications", " Corporation", " Co." })
                if (name.EndsWith(tail)) return name.Substring(0, name.Length - tail.Length);
            return name;
        }

        static int Production(Data.EconomyData data) { int n = 0; foreach (var i in data.Industries) if (i.TierIndex > 0) n++; return n; }

        static int CountSteel(Regimes.Regime r, Data.EconomyData data)
        {
            int n = 0; for (int j = 0; j < data.Industries.Count; j++) if (data.Industries[j].TierIndex > 0 && r.StateShare(j) >= .5) n++;
            return n;
        }

        /// <summary>The panel's account of the regime: three plain lines, then what is measured (each with its year) and
        /// what Jev judged. Files, model versions and links stay in <see cref="Drawing"/>, the sources and method.</summary>
        static string Readout(Regimes.Regime r, Regimes.Regime us, Data.EconomyData data, double gdp)
        {
            var s = new StringBuilder();
            string Year(Regimes.Measure m) => m.Has ? " <color=#8A93A0>(" + m.Year + ")</color>" : "";
            string Vs(Regimes.Measure m, Regimes.Measure u, bool pct = true) => r.Baseline || u == null || !u.Has ? "" : " <color=#8A93A0>· US " + (pct ? LandFacts.Percent(u.Value, 1) : u.Value.ToString("0.00", LandFacts.Ci)) + "</color>";
            s.Append("<b>").Append(r.Name).Append("</b> · ").Append(r.Form).Append(" · ").Append(r.Era).Append('\n');
            s.Append(Opening(r)).Append("\n\n");
            s.Append("<color=#B9C7DA><b>MEASURED</b></color>\n");
            string own = OwnEconomy(r);
            s.Append(char.ToUpperInvariant(own[0])).Append(own.Substring(1)).Append(r.Baseline ? "" : " <color=#8A93A0>· the US hills drawn here: " + LandFacts.Money(gdp, 1) + "</color>").Append('\n');
            if (r.Spending.Has) s.Append("Government spending <b>").Append(Pct(r.Spending)).Append("</b> of ").Append(r.Spending.Of).Append(Year(r.Spending)).Append(Vs(r.Spending, us?.Spending)).Append('\n');
            else s.Append("Government spending: not found\n");
            s.Append("Revenue <b>").Append(Pct(r.Revenue)).Append("</b>").Append(Year(r.Revenue)).Append(Vs(r.Revenue, us?.Revenue)).Append('\n');
            if (r.Mix != null)
            {
                s.Append("It lives on: ");
                var order = new List<int>(); for (int k = 0; k < Regimes.Parts; k++) if (r.Mix[k] >= .02) order.Add(k);
                order.Sort((a, b) => r.Mix[b].CompareTo(r.Mix[a]));
                // each source keyed by a dot in its stratum's colour on the face, where the thin strata carry no name
                for (int i = 0; i < order.Count; i++)
                    s.Append(i > 0 ? " · " : "").Append("<nobr><color=#").Append(ColorUtility.ToHtmlStringRGB(OnScreen(PartColor[order[i]], .62f).gamma))
                     .Append(">\u25CF</color> ").Append(PartName(order[i])).Append(' ').Append(LandFacts.Percent(r.Mix[order[i]])).Append("</nobr>");
                s.Append(" <color=#8A93A0>(").Append(r.MixYear).Append(r.MixBasis == "tax" ? ", tax shares scaled to revenue" : "").Append(")</color>\n");
            }
            else s.Append("Its revenue mix: not found\n");
            if (r.StateFirms.Has && r.StateFirms.Basis == "by law")
                s.Append("The state's share of the means of production: <b>all of it, by law</b>").Append(Year(r.StateFirms)).Append('\n');
            else if (r.StateFirms.Has)
                s.Append("The state's share of listed firms' value <b>").Append(LandFacts.Percent(r.StateFirms.Value)).Append("</b>").Append(Year(r.StateFirms))
                 .Append(r.Baseline || us == null || !us.StateFirms.Has ? "" : " <color=#8A93A0>· US " + LandFacts.Percent(us.StateFirms.Value) + "</color>").Append('\n');
            else s.Append("The state's share of listed firms' value: not found\n");
            // a tally of one or two firms says little; it is given where the country has ten or more on the list
            if (r.Fortune.Has && Listed(r.Fortune) >= 10)
                s.Append("Of its firms among the world's 500 largest (Fortune), <b>").Append(r.Fortune.Count).Append("</b> are state-controlled").Append(Year(r.Fortune)).Append('\n');
            // the summits are each industry's largest firm; the state's other holdings do not color a summit
            var steel = new List<string>(); var part = new List<string>(); var gold = new List<string>(); var holdings = new List<string>();
            foreach (var f in r.Firms)
            {
                if (f.Industry < 0) continue;
                string where = Short(f.Name) + " (" + data.Industries[f.Industry].Name.ToLowerInvariant() + ")";
                if (f.Leads) (f.Controlled ? steel : f.Share > 0 ? part : gold).Add(f.Controlled || f.Share <= 0 ? where : where.TrimEnd(')') + ", " + LandFacts.Percent(f.StateShare) + ")");
                else if (f.Share > 0 && r.UnrankedMark(f.Industry)?.Firm != f.Name) holdings.Add(Short(f.Name) + " " + (f.Controlled ? (double.IsNaN(f.StateShare) ? "controlled" : LandFacts.Percent(f.StateShare)) : LandFacts.Percent(f.StateShare)));
            }
            if (steel.Count > 0) s.Append("<color=#B9C7DA>Steel summits</color> (the state controls the industry's largest firm): ").Append(string.Join(" · ", steel)).Append('\n');
            if (part.Count > 0) s.Append("Partly the state's: ").Append(string.Join(" · ", part)).Append('\n');
            if (gold.Count > 0) s.Append("<color=#FFC266>Gold summits</color> (private): ").Append(string.Join(" · ", gold)).Append('\n');
            if (r.DefaultOwnership >= .5) s.Append("Every summit: the state's (").Append(r.DefaultNote).Append(")\n");
            // summits no firm is ranked for are drawn as such; a mark names the largest firm the research found there
            var open = new List<string>(); var marked = new List<string>();
            for (int j = 0; j < data.Industries.Count; j++)
            {
                if (data.Industries[j].TierIndex == 0 || r.Decided(j)) continue;
                var mark = r.UnrankedMark(j); string name = data.Industries[j].Name.ToLowerInvariant();
                if (mark == null) open.Add(name);
                else marked.Add(name + " (its largest firm named, " + Short(mark.Firm) + ", is " + (mark.Controlled ? "the state's" : "private") + ")");
            }
            if (open.Count + marked.Count > 0)
                s.Append("<color=#A9B0BA>Not ranked</color> (dotted rings: no firm compared there").Append(marked.Count > 0 ? ", or the comparison was lost" : "").Append("): ")
                 .Append(string.Join(" · ", marked)).Append(marked.Count > 0 && open.Count > 0 ? " · " : "")
                 .Append(open.Count > 6 ? open.Count + (marked.Count > 0 ? " more" : "") + " summits" : string.Join(", ", open)).Append('\n');
            if (holdings.Count > 0) s.Append("The state's other holdings: ").Append(string.Join(" · ", holdings)).Append('\n');
            if (r.Trade.Has) s.Append("Trade <b>").Append(Pct(r.Trade)).Append("</b> of ").Append(r.Trade.Of).Append(Year(r.Trade)).Append(Vs(r.Trade, us?.Trade)).Append('\n');
            else s.Append("Trade: ").Append(string.IsNullOrEmpty(r.Trade.Note) ? "no single defensible share" : char.ToLowerInvariant(r.Trade.Note[0]) + r.Trade.Note.Substring(1)).Append('\n');
            string modelled = r.Top1Wealth.Has || double.IsNaN(r.Top1Wealth.Modelled) ? "" : " <color=#8A93A0>(WID models " + LandFacts.Percent(r.Top1Wealth.Modelled, 1) + " for " + r.Top1Wealth.ModelledYear + "; not drawn)</color>";
            s.Append("Top 1%: <b>").Append(r.Top1Wealth.Has ? LandFacts.Percent(r.Top1Wealth.Value, 1) : "not measured").Append("</b> of wealth").Append(Year(r.Top1Wealth)).Append(modelled)
             .Append(" · <b>").Append(Pct(r.Top1Income)).Append("</b> of income").Append(Year(r.Top1Income)).Append('\n');
            if (r.Democracy.Has)
                s.Append("Electoral democracy <b>").Append(r.Democracy.Value.ToString("0.00", LandFacts.Ci)).Append("</b> of 1").Append(Year(r.Democracy))
                 .Append(double.IsNaN(r.Democracy.Previous) ? "" : " <color=#8A93A0>· " + r.Democracy.Previous.ToString("0.00", LandFacts.Ci) + " in " + r.Democracy.PreviousYear + "</color>").Append(Vs(r.Democracy, us?.Democracy, false)).Append('\n');
            s.Append("\n<color=#B9C7DA><b>JUDGED</b></color>\n");
            s.Append(r.Judged != null ? "Regulation (grey) and capture (gold) strands: judged industry by industry by Jev, an AI model; judgments, not measurement\n"
                : "Regulation and capture: not judged for this regime\n");
            if (!string.IsNullOrEmpty(r.Notes)) s.Append("\n<color=#8A93A0>").Append(WithoutLinks(r.Notes, new List<string>())).Append("</color>\n");
            return s.ToString();
        }

        /// <summary>The panel's folded sources and method: the drawing's own choices (what is not data), then where the
        /// numbers and judgments come from, with files, model versions and links.</summary>
        static string Drawing(Regimes.Regime r)
        {
            var links = new List<string>(); if (!string.IsNullOrEmpty(r.Notes)) WithoutLinks(r.Notes, links);
            var regimes = Regimes.Get();
            return "<color=#B9C7DA><b>THE DRAWING'S CHOICES</b></color>\n" +
                "A thought experiment: the hills, people and money are the United States' (BEA); only the foundation changes.\n" +
                "Bedrock depth = spending by all levels of government (20% of GDP = 14 units, the federal block's scale); strata = the revenue mix; white line = revenue" +
                (r.Baseline ? "; dashed line = federal outlays alone, the block of the other views" : "") + ".\n" +
                "Summit rings: each industry's largest firm (Fortune Global 500 2026, else its national leader), steel where the state controls it, a steel arc for a minority stake; " +
                (r.DefaultOwnership >= .5 ? "every summit is drawn the state's by law" : "dotted rings: no firm ranked there (steel dots: the largest firm named there is the state's)") + ".\n" +
                "Gauge: the state's share of the value of the country's listed firms (OECD).\n" +
                "What it lives on falls in from where it comes: " + string.Join(", ", Regimes.PartFrom, 0, 7) + ".\n" +
                "Votes, trade, the 1% rings: widths and areas from the measures above.\n\n" +
                "<color=#B9C7DA><b>SOURCES</b></color>\n" +
                "Each measure with its source, link and the text it was read from: Tools/regimes/sources.json (built " + regimes?.Built + " by Tools/regimes/build.py).\n" +
                (r.Judged != null ? "Strands: " + r.Judged + (regimes?.JevModel != null && !r.Judged.Contains(regimes.JevModel) ? " (" + regimes.JevModel + ")" : "") + ".\n" : "") +
                (r.Economy.Has && !string.IsNullOrEmpty(r.Economy.Source) ? "Own economy: " + r.Economy.Source + ".\n" : "") +
                (links.Count > 0 ? "Links in the note: " + string.Join(" · ", links) + "\n" : "");
        }

        // ------------------------------------------------------------------ drawing helpers

        /// <summary>A box of the bedrock: its front face at the crest, its top, and its two ends back along the wake.</summary>
        static void Slab(SurfaceMeshBuilder b, float id, float x0, float x1, float yTop, float yBottom, float zFront, float zBack, Color color, float alpha)
        {
            Color front = color; front.a = alpha; Color side = color; side.a = alpha * .45f;
            b.AddBand(new[] { new Vector3(x0, yTop, zFront), new Vector3(x1, yTop, zFront) }, new[] { new Vector3(x0, yBottom, zFront), new Vector3(x1, yBottom, zFront) }, new[] { (Color32)front }, id, 1);
            foreach (float x in new[] { x0, x1 })
                b.AddBand(new[] { new Vector3(x, yTop, zFront), new Vector3(x, yTop, zBack) }, new[] { new Vector3(x, yBottom, zFront), new Vector3(x, yBottom, zBack) }, new[] { (Color32)side }, id, 1);
        }

        /// <summary>A slab of the bedrock as it reaches the screen where it is solid, under its name (the strata soften toward
        /// their edges): its tint at the slab's alpha over the background, opaque and linear.</summary>
        static Color OnScreen(Color tint, float alpha) { Color c = Color.Lerp(GraphStyle.Background, tint, alpha); c.a = 1; return c; }

        /// <summary>The ink of a stratum's name (<paramref name="face"/>: the stratum as it reaches the screen): its tint
        /// darkened or lightened, whichever stands further from the face by WCAG's contrast ratio. Every stratum is light
        /// (luminance .3 to .43 on screen, where the near-white ink the names had stayed under 2.5:1), so every name is dark,
        /// 6.5:1 to 8.6:1: nearly black, for the thin strata's names, whose strokes at some 6 px capitals cover only about
        /// .85 of a pixel, so the stratum shows through them (at .8 toward black, 5.9:1 to 7.2:1, they read 3.3 to 5:1).</summary>
        static Color Ink(Color tint, Color face)
        {
            Color dark = Color.Lerp(tint, Color.black, .9f), light = Color.Lerp(tint, White, .82f);
            // text decodes its colour from sRGB (EconomyStyle.Roles.Encoded); the face, a vertex colour, is linear already
            float f = Luminance(face) + .05f;
            return f / (Luminance(dark.linear) + .05f) >= (Luminance(light.linear) + .05f) / f ? dark : light;
        }

        /// <summary>The relative luminance of a linear colour.</summary>
        static float Luminance(Color c) => .2126f * c.r + .7152f * c.g + .0722f * c.b;

        static void Strand(LineMeshBuilder b, float id, Vector3 a, Vector3 z, Color color, float width, float bow)
        {
            var pts = new LinePoint[21];
            for (int s = 0; s < pts.Length; s++) { float t = s / 20f; pts[s] = new LinePoint(Vector3.Lerp(a, z, t) + Vector3.right * Mathf.Sin(t * Mathf.PI) * bow, color, width); }
            b.AddFlowPath(pts, id, .4f);
        }

        /// <summary>An arc of a ring facing the crest's front (the x-y plane) from fraction f0 to f1 of the circle,
        /// clockwise from the top.</summary>
        static void Arc(LineMeshBuilder b, float id, Vector3 c, float r, float f0, float f1, Color color, float width)
        {
            if (f1 - f0 < .004f) return;
            int n = Mathf.Max(3, Mathf.CeilToInt((f1 - f0) * 64));
            var pts = new Vector3[n + 1];
            for (int s = 0; s <= n; s++) { float a = Mathf.PI * .5f - Mathf.Lerp(f0, f1, s / (float)n) * Mathf.PI * 2; pts[s] = c + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0); }
            b.AddPolyline(pts, color, width, 0, id);
        }

        /// <summary>A ring facing the camera's side of the wave (in the x-y plane).</summary>
        static void Ring(LineMeshBuilder b, float id, Vector3 c, float r, Color color, float width)
        {
            var pts = new Vector3[65];
            for (int s = 0; s < pts.Length; s++) { float a = s * Mathf.PI * 2 / 64; pts[s] = c + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0); }
            b.AddPolyline(pts, color, width, 0, id);
        }

        static void Dashed(LineMeshBuilder b, float id, Vector3 c, float r, Color color, float width = 1.4f)
        {
            for (int s = 0; s < 32; s += 2)
            {
                float a0 = s * Mathf.PI * 2 / 32, a1 = (s + 1) * Mathf.PI * 2 / 32;
                b.AddSegment(c + new Vector3(Mathf.Cos(a0) * r, Mathf.Sin(a0) * r, 0), c + new Vector3(Mathf.Cos(a1) * r, Mathf.Sin(a1) * r, 0), color, width, 0, id);
            }
        }

        /// <summary>A mesh of lines, blended: where they cross, the top one's hue shows instead of a sum turning white. Drawn at
        /// the regime's queue, 3242, unless another is given (the summits' rings and the names' leaders, over the plates).</summary>
        MeshRenderer Lines(LineMeshBuilder b, Part part, bool flow, int queue = 3242)
        {
            Mesh mesh = b.ToMesh("Regime " + part); owned.Add(mesh);
            Material m = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, queue, false, 0, flow ? Pulse : 0)); owned.Add(m);
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

        TextMeshPro Label(string text, Vector3 at, float size, Color color)
        {
            var go = new GameObject("Regime label"); go.transform.SetParent(content.transform, false); go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>(); t.text = text; t.fontSize = 24; t.alignment = TextAlignmentOptions.Center; t.color = color;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.sizeDelta = new Vector2(90, 8);
            // the names are read over the summits' rings and beams and the bedrock's translucent face, which the font's own
            // queue would draw over them: they take a copy of its material drawn last
            if (raisedText == null && t.fontSharedMaterial != null) { raisedText = new Material(t.fontSharedMaterial) { renderQueue = 3252 }; owned.Add(raisedText); }
            if (raisedText != null) t.fontSharedMaterial = raisedText;
            go.transform.localScale = Vector3.one * size; labels.Add(t); return t;
        }

        /// <summary>A dark plate behind a label: its text with the label system's margin, .35 em sideways and .12 em above and
        /// below (TextMeshPro sets 2.4 local units per em at its font size 24), drawn after the regime's other marks (queue
        /// 3250) and before the summits' rings and the names' leaders (3251), which stay whole over it, and the raised text
        /// (3252). At .9 rather than the .75 of the plates elsewhere: names stand on lines about as light as their letters
        /// (VOTES on its white threads, TRADE on the first hill's outline, the oil's name on a steel beam) or of their hue
        /// (the firms' gold on the wave's gold ridges), and a quarter of such a line would leave the first ones about 3 to
        /// 4:1. It is the label's child, so it turns with it.</summary>
        TextMeshPro Plate(TextMeshPro t)
        {
            if (plateQuad == null)
            {
                var b = new SurfaceMeshBuilder();
                b.AddBand(new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0) }, new[] { new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0) },
                    new[] { (Color32)WithAlpha(GraphStyle.Background, .9f) }, 0, 1);
                plateQuad = b.ToMesh("Regime plate"); owned.Add(plateQuad);
                plateMaterial = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1, 3250)); plateMaterial.SetFloat("_EdgeSoft", 0); owned.Add(plateMaterial);
            }
            Vector2 s = t.GetPreferredValues(t.text) + new Vector2(.7f, .24f) * 2.4f;
            var r = AddMesh("Regime plate", plateQuad, plateMaterial); r.transform.SetParent(t.transform, false); r.transform.localScale = new Vector3(s.x, s.y, 1);
            return t;
        }

        /// <summary>The turn that lays a name in the bedrock's face (the x-y plane), read from its front (+z, the future's side,
        /// where the view stands) or from behind. TextMeshPro is read from its -z side, so from the front a name turns half
        /// round: its +x then runs along the land's -x, left to right on the screen from there.</summary>
        static Quaternion Facing(bool front) => front ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        void Clear()
        {
            foreach (var o in owned) if (o) Destroy(o);
            if (content) Destroy(content);
            owned.Clear(); renderers.Clear(); labels.Clear(); inFace.Clear(); content = null; raisedText = plateMaterial = null; plateQuad = null; tradeLabel = null;
        }

        void OnDestroy() { Clear(); Shown = -1; ReadoutText = DrawingText = ""; }
    }
}

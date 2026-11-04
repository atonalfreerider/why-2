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
    /// Value creation and capture on the landscape (<see cref="ValueCapture"/>). Households' desire / fear rivers reach
    /// each hill's market on its valley-facing slope; there the money splits by the hill's 2024 accounts and its supply
    /// chain: wages run back down to the people who earn them (blue), production taxes drop to the government bedrock
    /// (steel), upkeep stays (a faint ring) and the owners' surplus climbs the hill as a gold river into the summit's
    /// halo. The halo's size is the surplus households' spending generates there; its rose / ice arcs are the share Jev
    /// judged a manufactured want / a sold fear, its white ticks the share that is captive or habitual. From the halo,
    /// gold rises to whoever holds the claims: business owners on the slopes, diversified wealth in the clouds, abroad.
    /// Purchases between industries that households' spending sets off run underground. Every player carries a ledger
    /// ring: gold when it collects more owners' surplus than its own spending pays, rose when it pays more.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillCaptureLayer : GraphLayer
    {
        public override int Order => 57;
        public override IEnumerable<string> RequiredTexts => new[] { ValueCapture.JudgmentsPath };
        /// <summary>The ledger drawn (the scenario's when one runs).</summary>
        public static ValueCapture.Ledger Shown { get; private set; }
        enum Part { Climb, Halo, Glow, Ascent, Wages, Taxes, Ledger, Roots, Selected, Count }
        static readonly Color Gold = new Color(1, .68f, .20f), Blue = new Color(.30f, .70f, 1), Rose = new Color(1, .23f, .48f),
            Ice = new Color(.40f, .80f, 1), Steel = new Color(.50f, .60f, .72f), White = new Color(1, .96f, .88f), Amber = new Color(1, .82f, .55f);
        readonly List<(Renderer renderer, Part part)> renderers = new List<(Renderer, Part)>();
        readonly List<(TextMeshPro text, int industry)> labels = new List<(TextMeshPro, int)>();
        /// <summary>Labels kept for the next rebuild (under the layer, not the content): each year's swap rebuilds the layer,
        /// and making a text object costs more than drawing a halo.</summary>
        readonly List<TextMeshPro> spare = new List<TextMeshPro>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        LandSnapshot snapshot;
        HillLandscape land;
        ValueCapture model;
        ValueCapture.Ledger ledger;
        GameObject content;
        /// <summary>The capture view's caption: the year's split of a household dollar in words over a bar of its parts.</summary>
        Transform caption;
        /// <summary>The camera distance (the capture view's) at which the caption has its drawn size; it keeps that size on screen.</summary>
        const float CaptionDistance = 170;
        /// <summary>How far a flow's pulse brightens a line (1 = the shader's fourfold): a sheen, under the bloom. Only the
        /// selected player's arcs keep the full pulse.</summary>
        const float Pulse = .2f;
        int revision = -1, selection = -2, lod = -1;

        public override void Prepare(GraphContext ctx) { ValueCapture.Provide(ctx.Text(ValueCapture.JudgmentsPath)); }
        public override void Upload(GraphContext ctx) { EconomyStage.Land().Place(transform); }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (HillLandscapeLayer.Current == null || LandService.Current == null) return;
            var program = PlayerMindProgram.Active;
            if (snapshot != LandService.Current || land != HillLandscapeLayer.Current || revision != (program?.Revision ?? -1) || selection != EconomyState.SelectedPlayer)
            {
                if (!HillRebuilds.Take()) return;
                Build();
            }
            if (content == null) return;
            string view = LandView.PresetId;
            bool hidden = view == "mind" || view == "section";
            content.SetActive(!hidden);
            if (hidden) { foreach (var entry in labels) entry.text.gameObject.SetActive(false); return; }
            lod = rig.Pose.Distance < 35 ? 2 : rig.Pose.Distance < 100 ? 1 : 0;
            foreach (var entry in renderers)
                if (entry.renderer) GraphMaterials.SetAlpha(entry.renderer.sharedMaterial, Emphasis(entry.part, view));
            // The dollar's split, named once, where the view is about it; it keeps its size on screen. Built on the view's
            // first frame, not with the rest: the replay rebuilds this layer every year in views that never show it.
            bool named = view == "capture";
            if (named && caption == null) Caption();
            if (caption) caption.gameObject.SetActive(named);
            if (named)
            {
                caption.rotation = rig.Cam.transform.rotation;
                caption.localScale = Vector3.one * Mathf.Max(.5f, Vector3.Distance(rig.Cam.transform.position, caption.position) / CaptionDistance);
            }
            int focus = HillExplorer.Industry >= 0 ? HillExplorer.Industry : HillLandscapeLayer.HoverIndustry;
            foreach (var entry in labels)
            {
                bool show = view == "capture" || !EconomyViews.Focused(view) && (entry.industry == focus && !PersonFocus.IsView(view) ||
                    lod >= 1 && view != "society" && view != "betrayal" && view != "foundation" && !PersonFocus.IsView(view));
                entry.text.gameObject.SetActive(show);
                if (show) entry.text.transform.rotation = rig.Cam.transform.rotation;
            }
        }

        float Emphasis(Part part, string view)
        {
            bool selected = EconomyState.SelectedPlayer >= 0;
            float dim = selected ? .35f : 1;
            switch (view)
            {
                case "society": case "betrayal": return part == Part.Halo || part == Part.Glow ? .12f : .02f;
                case "atom": case "dilemma": case "ladder": return part == Part.Halo || part == Part.Glow ? .25f : .02f;
                case "roots": return part == Part.Roots ? 1 : part == Part.Halo || part == Part.Glow ? .35f : .06f;
                case "foundation": return part == Part.Taxes ? 1 : part == Part.Halo || part == Part.Glow ? .3f : part == Part.Selected ? 1 : .08f;
                // focused views: the halos stay as landmarks (brighter where the story is the money's destination)
                case "mobility": case "money": return part == Part.Halo || part == Part.Glow ? .1f : .02f;
                // the regimes view draws its own summits (steel where the state owns them)
                case "regimes": return .02f;
                case "strategy": return part == Part.Halo || part == Part.Glow ? .45f : .03f;
                case "you": return part == Part.Halo || part == Part.Glow ? .35f : part == Part.Selected ? 1 : .03f;
                // the rivers of desire and fear are that view's story: where their money goes next steps back under them
                case "rivers": return part == Part.Selected ? 1 : part == Part.Climb ? .3f * dim : part == Part.Halo || part == Part.Glow ? .25f
                    : part == Part.Ascent ? .12f * dim : part == Part.Roots ? .1f : part == Part.Ledger ? .2f : .25f * dim;
            }
            switch (part)
            {
                case Part.Halo: return 1;
                case Part.Glow: return view == "capture" ? 1 : .8f;
                case Part.Climb: return view == "capture" ? 1 : .8f * dim;
                // who keeps the money draws every part of the dollar at its weight: wages (41¢ in 2025) as strong as the
                // owners' climb (27¢), production taxes (7¢) near it, the owners' ascent to their holders no stronger
                case Part.Ascent: return (view == "capture" ? .4f : view == "people" ? .15f : .07f) * dim;
                case Part.Wages: return (view == "capture" ? 1 : view == "people" ? .9f : view == "landscape" ? .55f : .16f) * dim;
                case Part.Taxes: return (view == "capture" ? .8f : view == "landscape" ? .35f : .1f) * dim;
                case Part.Ledger: return view == "capture" || view == "people" ? 1 : lod >= 1 ? .7f : .2f;
                case Part.Roots: return view == "capture" ? .35f : .04f;
                case Part.Selected: return 1;
            }
            return 1;
        }

        void Build()
        {
            Clear();
            snapshot = LandService.Current; land = HillLandscapeLayer.Current; model = ValueCapture.Get();
            var program = PlayerMindProgram.Active;
            revision = program?.Revision ?? -1; selection = EconomyState.SelectedPlayer;
            if (model == null || land == null || snapshot?.Players?.Players == null) return;
            if (program != null && program.Players.Length != snapshot.Players.Players.Length) program = null;
            ledger = model.Account(snapshot, program); Shown = ledger;
            content = new GameObject("Value creation and capture"); content.transform.SetParent(transform, false);
            Climbs(); Halos(); Ascent(); Wages(); Taxes(); Ledgers(); Roots(); Selected();
            double owners = ledger.OwnersTotal, spent = 0; foreach (double v in ledger.Spent) spent += v;
            Debug.Log("[Why] Capture " + snapshot.Year + ": households spend " + LandFacts.Money(spent) + "; wages " +
                LandFacts.Money(ledger.Total[0]) + ", taxes " + LandFacts.Money(ledger.Total[1]) + ", upkeep " + LandFacts.Money(ledger.Total[2]) +
                ", owners " + LandFacts.Money(owners) + " (abroad " + LandFacts.Money(ledger.Abroad) + "), imports " + LandFacts.Money(ledger.Imports) +
                "; Leontief residual " + model.MaxInverseResidual.ToString("G3", LandFacts.Ci) + "; judgments " +
                (model.Judged ? model.Judgments.Count + " by " + model.Model + " (" + model.JudgedOn + ")" : "absent, category motives used") + ".");
        }

        float HaloRadius(int j) => .7f + Mathf.Sqrt((float)Math.Max(0, ledger.Owners[j])) * .12f;

        /// <summary>
        /// The capture view's caption, from the ledger's totals: of each household dollar in the shown year (before income
        /// taxes), the cents that pay wages, the owners, upkeep, imports and the taxes on production (sales, excise and
        /// property taxes), over a bar of the same parts in the colors the view draws them in (upkeep and imports, which it
        /// hardly draws, in greys). In the scene, so it shows during the tour too, when the HUD's subtitle is hidden.
        /// </summary>
        void Caption()
        {
            caption = new GameObject("Capture caption").transform; caption.SetParent(content.transform, false); caption.gameObject.SetActive(false);
            // over the wake on the valley side, clear of the climbs and the arcs in the capture view's frame
            caption.localPosition = new Vector3(60, 32, land.CrestZ - 70);
            double sum = 0; for (int k = 0; k < ValueCapture.Parts; k++) sum += Math.Max(0, ledger.Total[k]);
            if (sum <= 0) return;
            var order = new[] { ValueCapture.Part.Wages, ValueCapture.Part.Owners, ValueCapture.Part.Upkeep, ValueCapture.Part.Imports, ValueCapture.Part.Taxes };
            string[] names = { "wages", "owners", "upkeep", "imports", "sales and property taxes" };
            Color[] colors = { Blue, Gold, new Color(.70f, .74f, .80f), new Color(.46f, .49f, .54f), Steel };
            var bar = new LineMeshBuilder(8); var words = new List<string>(); float x = -24, length = 48;
            for (int k = 0; k < order.Length; k++)
            {
                double share = Math.Max(0, ledger.Total[(int)order[k]]) / sum; float w = length * (float)share;
                if (w > .5f) bar.AddSegment(new Vector3(x + .15f, 0, 0), new Vector3(x + w - .15f, 0, 0), colors[k], 12, 0, 0);
                x += w;
                words.Add("<color=#" + ColorUtility.ToHtmlStringRGB(Color.Lerp(colors[k], Color.white, .25f)) + ">" + (100 * share).ToString("0", LandFacts.Ci) + "¢ " + names[k] + "</color>");
            }
            // drawn after the hills (queue 3252): the font's own queue and the bar's would let the wake's lines draw over them
            Mesh mesh = bar.ToMesh("Capture caption bar"); owned.Add(mesh);
            Material material = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3252, false)); owned.Add(material);
            AddMesh("Capture caption bar", mesh, material).transform.SetParent(caption, false);
            var head = Label("<b>OF EACH HOUSEHOLD DOLLAR IN " + snapshot.Year + "</b>, before income taxes", Vector3.up * 2.4f, .8f, new Color(.92f, .95f, 1), false);
            var parts = Label(string.Join(" · ", words), Vector3.down * 2.2f, .75f, Color.white, false);
            Material text = head.fontSharedMaterial != null ? new Material(head.fontSharedMaterial) { renderQueue = 3252 } : null;
            if (text != null) { owned.Add(text); head.fontSharedMaterial = text; parts.fontSharedMaterial = text; }
            head.transform.SetParent(caption, false); parts.transform.SetParent(caption, false);
        }

        void Climbs()
        {
            var fill = new SurfaceMeshBuilder(); var lines = new LineMeshBuilder();
            for (int j = 0; j < land.Hills.Length; j++)
            {
                var hill = land.Hills[j]; double k = ledger.Owners[j];
                if (hill.Tier == 0 || k <= .5) continue;
                // The gold river starts where the household river ends and winds up the valley side to the halo.
                var path = new List<LinePoint>();
                for (int s = 0; s <= 44; s++)
                {
                    float t = s / 44f, angle = HillLandscape.Valley + Mathf.Sin(t * 5.5f + j) * .09f * (1 - t);
                    Vector3 p = land.At(hill, angle, Mathf.Lerp(.86f, .08f, t), .32f);
                    Color c = Color.Lerp(Color.Lerp(Rose, Ice, .5f), Gold, Mathf.SmoothStep(0, 1, t * 5));
                    path.Add(new LinePoint(p, c, 1.6f));
                }
                Vector3 top = path[path.Count - 1].Data, halo = land.Halo(j);
                for (int s = 1; s <= 6; s++) path.Add(new LinePoint(Vector3.Lerp(top, halo - Vector3.up * .15f, s / 6f), Gold, 1.6f));
                float width = .12f + Mathf.Sqrt((float)k) * .06f;
                Ribbon(fill, path, width, Gold, .55f);
                lines.AddFlowPath(path, 0, .8f, j * .37f);
            }
            Surface(fill, Part.Climb, false);
            var r = Lines(lines, Part.Climb, true, true); r.sharedMaterial.SetFloat("_FlowFreq", 1.4f); r.sharedMaterial.SetFloat("_FlowSpeed", .55f);
        }

        void Halos()
        {
            var rings = new LineMeshBuilder(); var bed = new SurfaceMeshBuilder();
            var ranked = new List<int>();
            for (int j = 0; j < land.Hills.Length; j++)
            {
                if (land.Hills[j].Tier == 0 || ledger.Owners[j] <= .5) continue;
                ranked.Add(j);
                // Drawn in their hues (intensity 1): bloom is left to what is hovered or selected.
                Vector3 h = land.Halo(j); float R = HaloRadius(j);
                Circle(rings, h, R, Gold, 3, 1, 1);
                Circle(rings, h + Vector3.up * .06f, R * 1.10f, Gold, 1.4f, 1, .55f);
                Circle(rings, h - Vector3.up * .04f, R * .80f, White, 1, 1, .5f);
                rings.AddSegment(land.Hills[j].Summit, h, WithAlpha(Gold, .35f), 1, 0, 0);
                // The levers behind this hill's captured surplus (Jev judgments, owners'-surplus weighted).
                double m = model.IndustryLevers[j][0], f = model.IndustryLevers[j][1], persuaded = model.IndustryPersuaded(j);
                float rose = (float)(Math.PI * 2 * persuaded * (m + f > 0 ? m / (m + f) : .5)), ice = (float)(Math.PI * 2 * persuaded * (m + f > 0 ? f / (m + f) : .5));
                Arc(rings, h + Vector3.up * .12f, R * 1.24f, Mathf.PI * .5f, Mathf.PI * .5f + rose, Rose, 2.6f, 1);
                Arc(rings, h + Vector3.up * .12f, R * 1.24f, Mathf.PI * .5f + rose, Mathf.PI * .5f + rose + ice, Ice, 2.6f, 1);
                int ticks = Mathf.RoundToInt((float)model.IndustryLockedIn(j) * 32);
                for (int t = 0; t < ticks; t++)
                {
                    float a = Mathf.PI * 1.5f + (t - ticks * .5f) * Mathf.PI * 2 / 32;
                    Vector3 at = h + new Vector3(Mathf.Cos(a) * R * 1.38f, .12f, Mathf.Sin(a) * R * 1.38f);
                    rings.AddSegment(at - Vector3.up * R * .07f, at + Vector3.up * R * .07f, WithAlpha(White, .8f), 1.4f, 0, 0);
                }
                Annulus(bed, h, R * .45f, R, 0, .20f); Annulus(bed, h, R, R * 1.55f, .20f, 0);
            }
            Lines(rings, Part.Halo, false, true);
            Surface(bed, Part.Glow, true);
            ranked.Sort((a, b) => ledger.Owners[b].CompareTo(ledger.Owners[a]));
            for (int n = 0; n < ranked.Count; n++)
            {
                int j = ranked[n]; if (n >= 9 && ledger.Owners[j] < 60) continue;
                Vector3 h = land.Halo(j); float R = HaloRadius(j);
                double cents = ledger.Output[j] > 0 ? 100 * ledger.Owners[j] / ledger.Output[j] : 0;
                string text = "<b>" + LandService.Model.Data.Industries[j].Name + "</b>\nowners keep " + LandFacts.Money(ledger.Owners[j]) +
                    " a year from household spending\n" + cents.ToString("F0", LandFacts.Ci) + "¢ of each $ it produces · persuaded " +
                    LandFacts.Percent(model.IndustryPersuaded(j)) + " · locked in " + LandFacts.Percent(model.IndustryLockedIn(j));
                labels.Add((Label(text, h + Vector3.up * (1.1f + R * .35f), .17f, new Color(1, .86f, .55f)), j));
            }
        }

        void Ascent()
        {
            var b = new LineMeshBuilder(); var players = snapshot.Players.Players;
            var order = new List<int>();
            for (int j = 0; j < land.Hills.Length; j++)
            {
                if (land.Hills[j].Tier == 0 || ledger.Owners[j] <= .5) continue;
                Vector3 h = land.Halo(j);
                order.Clear(); for (int p = 0; p < players.Length; p++) if (ledger.ReceivedFrom[p][j] > .05) order.Add(p);
                order.Sort((x, y) => ledger.ReceivedFrom[y][j].CompareTo(ledger.ReceivedFrom[x][j]));
                double pool = 0, drawn = 0; foreach (int p in order) pool += ledger.ReceivedFrom[p][j];
                for (int n = 0; n < order.Count && n < 10 && drawn < pool * .85; n++)
                {
                    int p = order[n]; double amount = ledger.ReceivedFrom[p][j]; drawn += amount;
                    Vector3 to = land.People[p] + Vector3.up * .9f;
                    Arch(b, h, to, WithAlpha(Gold, .55f), .6f + Mathf.Sqrt((float)amount) * .32f, 1, Vector3.Distance(h, to) * .12f + 1);
                }
                double abroad = ledger.Owners[j] * model.AbroadShare;
                if (abroad > 3) Arch(b, h, land.Abroad, WithAlpha(Gold, .25f), .5f + Mathf.Sqrt((float)abroad) * .25f, 1, 6);
            }
            // hundreds of arcs converge on the clouds: blended, not added, so their crossings keep their gold
            var r = Lines(b, Part.Ascent, true, false); r.sharedMaterial.SetFloat("_FlowFreq", 1.2f); r.sharedMaterial.SetFloat("_FlowSpeed", .35f);
            labels.Add((Label("ABROAD\nforeign holders of US corporate equity", land.Abroad + Vector3.up * 2, .2f, new Color(1, .8f, .5f)), -2));
        }

        void Wages()
        {
            var b = new LineMeshBuilder(); var players = snapshot.Players.Players; var order = new List<int>();
            for (int j = 0; j < land.Hills.Length; j++)
            {
                if (ledger.Wages[j] <= .5) continue;
                Vector3 market = land.Market(j);
                order.Clear(); for (int p = 0; p < players.Length; p++) if (ledger.WagesFrom[p][j] > .3) order.Add(p);
                order.Sort((x, y) => ledger.WagesFrom[y][j].CompareTo(ledger.WagesFrom[x][j]));
                for (int n = 0; n < order.Count && n < 7; n++)
                {
                    int p = order[n];
                    GroundPath(b, market, land.People[p] + Vector3.up * .2f, WithAlpha(Blue, .7f), .7f + Mathf.Sqrt((float)ledger.WagesFrom[p][j]) * .22f, 1);
                }
            }
            var r = Lines(b, Part.Wages, true, false); r.sharedMaterial.SetFloat("_FlowFreq", 2.5f); r.sharedMaterial.SetFloat("_FlowSpeed", .5f);
        }

        void Taxes()
        {
            var b = new LineMeshBuilder();
            for (int j = 0; j < land.Hills.Length; j++)
            {
                if (land.Hills[j].Tier == 0) continue;
                Vector3 market = land.Market(j);
                if (ledger.Taxes[j] > .5)
                {
                    var drop = new LinePoint[12];
                    for (int s = 0; s < drop.Length; s++) drop[s] = new LinePoint(Vector3.Lerp(market, new Vector3(market.x, HillLandscape.Bedrock + .4f, market.z), s / 11f), WithAlpha(Steel, .6f), .6f + Mathf.Sqrt((float)ledger.Taxes[j]) * .25f);
                    b.AddFlowPath(drop, 0, .6f, j * .2f);
                }
                if (ledger.Upkeep[j] > .5) Circle(b, market + Vector3.up * .05f, .25f + Mathf.Sqrt((float)ledger.Upkeep[j]) * .05f, WithAlpha(Steel, .35f), .9f, 1, 1);
            }
            Lines(b, Part.Taxes, true, false);
        }

        void Ledgers()
        {
            var b = new LineMeshBuilder(); var players = snapshot.Players.Players;
            for (int p = 0; p < players.Length; p++)
            {
                double net = ledger.Net(p); Vector3 at = land.People[p] + Vector3.up * .06f;
                float r = .35f + Mathf.Log10(1 + (float)Math.Abs(net)) * .55f;
                Color c = net >= 0 ? Gold : Rose;
                Circle(b, at, r, WithAlpha(c, .85f), 1.6f, 1, 1, 48);
                if (Math.Abs(net) > 10) Circle(b, at + Vector3.up * .05f, r * 1.18f, WithAlpha(c, .4f), 1, 1, 1, 48);
            }
            Lines(b, Part.Ledger, false, false);
        }

        void Roots()
        {
            var b = new LineMeshBuilder(); int n = land.Hills.Length;
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
            {
                double v = ledger.Inputs[i, j]; if (i == j || v < 25) continue;
                // Money runs from the buyer (j) to its supplier (i) underground, beneath the goods it pays for.
                Vector3 a = land.Market(j), z = land.Market(i); var pts = new LinePoint[33];
                for (int s = 0; s < pts.Length; s++)
                {
                    float t = s / 32f; Vector3 p = Vector3.Lerp(a, z, t);
                    p.y = Mathf.Min(p.y - 6 * Mathf.Sin(t * Mathf.PI), land.Ground(p.x, p.z) - 3 - 3 * Mathf.Sin(t * Mathf.PI));
                    pts[s] = new LinePoint(p, WithAlpha(Amber, .5f), .5f + Mathf.Sqrt((float)v) * .09f);
                }
                b.AddFlowPath(pts, 0, .5f, (i * 7 + j) * .13f);
            }
            Lines(b, Part.Roots, true, false);
        }

        void Selected()
        {
            int p = EconomyState.SelectedPlayer; var players = snapshot.Players.Players;
            if (p < 0 || p >= players.Length) return;
            var b = new LineMeshBuilder(); var program = PlayerMindProgram.Active;
            var spend = program != null && program.Players.Length == players.Length ? program.Players[p].Category : players[p].Category;
            Vector3 home = land.People[p] + Vector3.up * .9f;
            for (int j = 0; j < land.Hills.Length; j++)
            {
                if (land.Hills[j].Tier == 0) continue;
                double paid = 0; for (int c = 0; c < 6; c++) paid += Math.Max(0, spend[c]) * (model.CategoryOwnersBy[c][j] - model.CategorySelfOwnedBy[c][j]);
                Vector3 h = land.Halo(j);
                // What the player's own spending pays into this halo (rose), and what this halo pays back to the player (gold).
                if (paid > .02) Arch(b, home, h, WithAlpha(Rose, .9f), .9f + Mathf.Sqrt((float)paid) * .9f, 3.2f, Vector3.Distance(h, home) * .10f + .5f);
                double got = ledger.ReceivedFrom[p][j] - ledger.SelfOwnedBy[p][j];
                if (got > .02) Arch(b, h, home, WithAlpha(Gold, .95f), .9f + Mathf.Sqrt((float)got) * .9f, 4.5f, Vector3.Distance(h, home) * .16f + 1);
            }
            var r = Lines(b, Part.Selected, true, true); r.sharedMaterial.SetFloat("_FlowFreq", 1.6f); r.sharedMaterial.SetFloat("_FlowSpeed", .6f);
        }

        // ------------------------------------------------------------------ geometry

        void Ribbon(SurfaceMeshBuilder fill, List<LinePoint> path, float width, Color color, float alpha)
        {
            int n = path.Count; var left = new Vector3[n]; var mid = new Vector3[n]; var right = new Vector3[n];
            for (int k = 0; k < n; k++)
            {
                Vector3 d = path[Math.Min(k + 1, n - 1)].Data - path[Math.Max(0, k - 1)].Data;
                Vector3 normal = new Vector3(-d.z, 0, d.x); normal = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.right;
                float taper = Mathf.Min(1, Mathf.Min(k, n - 1 - k) / 4f + .25f);
                mid[k] = path[k].Data - Vector3.up * .02f; left[k] = mid[k] + normal * width * .5f * taper; right[k] = mid[k] - normal * width * .5f * taper;
            }
            Color32 core = WithAlpha(color, alpha), edge = WithAlpha(color, 0);
            fill.AddBand(mid, left, new[] { core }, new[] { edge }, 0);
            fill.AddBand(mid, right, new[] { core }, new[] { edge }, 0);
        }

        void GroundPath(LineMeshBuilder b, Vector3 a, Vector3 z, Color color, float width, float intensity)
        {
            var pts = new LinePoint[25];
            for (int s = 0; s < pts.Length; s++)
            {
                float t = s / 24f; Vector3 p = Vector3.Lerp(a, z, t);
                p.y = Mathf.Max(land.Ground(p.x, p.z) + .22f, Mathf.Lerp(a.y, z.y, t)) + Mathf.Sin(t * Mathf.PI) * .35f;
                pts[s] = new LinePoint(p, color, width, 0, intensity);
            }
            b.AddFlowPath(pts, 0, .8f);
        }

        static void Arch(LineMeshBuilder b, Vector3 a, Vector3 z, Color color, float width, float intensity, float lift)
        {
            var pts = new LinePoint[29];
            for (int s = 0; s < pts.Length; s++)
            {
                float t = s / 28f; pts[s] = new LinePoint(Vector3.Lerp(a, z, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * lift, color, width, 0, intensity);
            }
            b.AddFlowPath(pts, 0, .5f);
        }

        static void Circle(LineMeshBuilder b, Vector3 c, float r, Color color, float width, float intensity, float alpha, int segments = 96)
        {
            var pts = new Vector3[segments + 1];
            for (int s = 0; s <= segments; s++) { float a = s * Mathf.PI * 2 / segments; pts[s] = c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r); }
            color.a *= alpha; b.AddPolyline(pts, color, width, 0, 0, intensity);
        }

        static void Arc(LineMeshBuilder b, Vector3 c, float r, float a0, float a1, Color color, float width, float intensity)
        {
            if (a1 - a0 < .01f) return;
            int n = Mathf.Max(2, Mathf.CeilToInt((a1 - a0) / (Mathf.PI * 2) * 96)); var pts = new Vector3[n + 1];
            for (int s = 0; s <= n; s++) { float a = Mathf.Lerp(a0, a1, s / (float)n); pts[s] = c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r); }
            b.AddPolyline(pts, color, width, 0, 0, intensity);
        }

        static void Annulus(SurfaceMeshBuilder b, Vector3 c, float r0, float r1, float a0, float a1)
        {
            var inner = new Vector3[65]; var outer = new Vector3[65];
            for (int s = 0; s <= 64; s++) { float a = s * Mathf.PI * 2 / 64; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); inner[s] = c + d * r0; outer[s] = c + d * r1; }
            b.AddBand(inner, outer, new[] { (Color32)WithAlpha(Gold, a0) }, new[] { (Color32)WithAlpha(Gold, a1) }, 0);
        }

        // ------------------------------------------------------------------ plumbing

        MeshRenderer Lines(LineMeshBuilder b, Part part, bool flow, bool additive)
        {
            Mesh mesh = b.ToMesh("Capture " + part); owned.Add(mesh);
            Material material = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3215, additive, 0, !flow ? 0 : part == Part.Selected ? 1 : Pulse)); owned.Add(material);
            var r = AddMesh("Capture " + part, mesh, material); r.transform.SetParent(content.transform, false);
            renderers.Add((r, part)); return r;
        }

        MeshRenderer Surface(SurfaceMeshBuilder b, Part part, bool additive)
        {
            Mesh mesh = b.ToMesh("Capture " + part); owned.Add(mesh);
            Material material = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1, 3212, additive)); owned.Add(material);
            var r = AddMesh("Capture " + part, mesh, material); r.transform.SetParent(content.transform, false);
            renderers.Add((r, part)); return r;
        }

        /// <summary>A label at a point of the land: a kept one (<see cref="spare"/>, returned by <see cref="Clear"/>) unless
        /// <paramref name="kept"/> is false, for one that goes with the content.</summary>
        TextMeshPro Label(string text, Vector3 at, float size, Color color, bool kept = true)
        {
            TextMeshPro t;
            if (kept && spare.Count > 0) { t = spare[spare.Count - 1]; spare.RemoveAt(spare.Count - 1); t.gameObject.SetActive(true); }
            else
            {
                t = new GameObject("Capture label").AddComponent<TextMeshPro>(); t.fontSize = 24; t.alignment = TextAlignmentOptions.Center;
                t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.sizeDelta = new Vector2(40, 8);
            }
            t.transform.SetParent(kept ? transform : content.transform, false); t.transform.localPosition = at;
            t.transform.localScale = Vector3.one * size; t.text = text; t.color = color; return t;
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        void Clear()
        {
            foreach (var o in owned) if (o) Destroy(o);
            if (content) Destroy(content);
            foreach (var entry in labels) if (entry.text) { entry.text.gameObject.SetActive(false); spare.Add(entry.text); }
            owned.Clear(); renderers.Clear(); labels.Clear(); content = null; caption = null;
        }

        void OnDestroy() { Clear(); Shown = null; }
    }
}

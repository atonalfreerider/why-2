using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Why.Economy.Land;
using Why.Economy.Model;
using Why.Humans;
using Why.Humans.Social;

namespace Why.Economy.Layers
{
    /// <summary>
    /// The government: a grey block beneath the whole wave. Along time (the wave's z) its depth is the year's federal
    /// outlays as a share of GDP and its strata are what they buy, from the top: social programs (Social Security,
    /// health, income security), the military, interest to bondholders and the rest (OMB historical tables, via
    /// Data/government.json; a white line inside marks receipts, so the gap below it is borrowing). At the crest (the
    /// shown year) the block meets the economy:
    /// <list type="bullet">
    /// <item>taxes come in, from every household's income and payroll (steel drops) and from each industry's profits
    /// (corporate tax, in proportion to its operating surplus);</item>
    /// <item>regulation goes out: a grey line up into every hill, as strong as Jev judged the industry regulated, and a
    /// gold line back down as strong as the industry shapes its own rules (regulatory capture, Jev);</item>
    /// <item>program pillars rise under whom they serve (Social Security to the retirees, health under the health hill,
    /// defense under manufacturing and hardware, schools / police / roads under the valley floor, other programs to the
    /// lowest rungs, interest to the clouds of wealth that hold the bonds), with benefits arcing to each household;</item>
    /// <item>the political actors of the year sit on the block's crest edge: presidents and other officeholders from the
    /// government file and the officeholders among the popular people (<see cref="Seats"/>; the social layer draws their
    /// white stars there).</item>
    /// </list>
    /// Years without the file scale FY2025 (circuit.json) by GDP. Detail grows with distance and in the foundation view.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillFoundationLayer : GraphLayer
    {
        public override int Order => 58;
        public override IEnumerable<string> RequiredTexts => new[] { GovernmentHistory.Path };
        enum Part { Block, Strata, Pillars, Benefits, Taxes, Regulation, Seats, Selected, Count }
        static readonly Color Steel = new Color(.50f, .62f, .76f), Ice = new Color(.55f, .82f, 1), Gold = new Color(1, .68f, .22f), Blue = new Color(.30f, .70f, 1);
        /// <summary>The block's strata, top down: social programs, military, interest, other.</summary>
        static readonly Color[] StratumColor = { new Color(.66f, .71f, .77f), new Color(.38f, .41f, .46f), new Color(.60f, .54f, .40f), new Color(.50f, .52f, .55f) };
        static readonly string[] StratumName = { "SOCIAL PROGRAMS", "MILITARY", "INTEREST TO BONDHOLDERS", "EVERYTHING ELSE" };
        /// <summary>Block depth per share of GDP (world units): 20% of GDP is 14 units deep.</summary>
        const float DepthPerShare = 70;

        /// <summary>One public program: its outlays ($B) and where it rises.</summary>
        public sealed class Pillar { public string Name, Note; public double Dollars; public Vector3 Base, Top; public Color Color; public readonly List<(int player, double dollars)> Serves = new List<(int, double)>(); }
        /// <summary>A political actor seated at the government's crest edge in the shown year.</summary>
        public sealed class Seat { public string Name, Office; public bool Executive, National; public int Person = -1; public Vector3 At; }
        public static IReadOnlyList<Pillar> Pillars => pillars;
        public static IReadOnlyList<Seat> Seats => seats;
        /// <summary>Incremented whenever the seats are rebuilt (a new year or land).</summary>
        public static int SeatsVersion { get; private set; }
        /// <summary>The parsed government history (null without the file).</summary>
        public static GovernmentHistory History { get; private set; }
        static readonly List<Pillar> pillars = new List<Pillar>();
        static readonly List<Seat> seats = new List<Seat>();
        readonly List<(Renderer renderer, Part part)> renderers = new List<(Renderer, Part)>();
        readonly List<(TextMeshPro text, int lod)> labels = new List<(TextMeshPro, int)>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        LandSnapshot snapshot; HillLandscape land; GameObject content;
        int selection = -2, lod;
        HillLandscape seatedFor;

        public override void Prepare(GraphContext ctx) { History = GovernmentHistory.Parse(ctx.Text(GovernmentHistory.Path)); }
        public override void Upload(GraphContext ctx) { EconomyStage.Land().Place(transform); }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (HillLandscapeLayer.Current == null || LandService.Current == null) return;
            if (snapshot != LandService.Current || land != HillLandscapeLayer.Current || selection != EconomyState.SelectedPlayer) Build();
            if (content == null) return;
            string view = LandView.PresetId; bool hidden = view == "mind" || view == "section";
            content.SetActive(!hidden); if (hidden) return;
            lod = view == "foundation" ? 2 : rig.Pose.Distance < 35 ? 2 : rig.Pose.Distance < 100 ? 1 : 0;
            foreach (var e in renderers) if (e.renderer) GraphMaterials.SetAlpha(e.renderer.sharedMaterial, Emphasis(e.part, view));
            foreach (var e in labels)
            {
                bool show = view == "foundation" || lod >= e.lod && view != "society" && view != "betrayal" && view != "capture" && !PersonFocus.IsView(view);
                e.text.gameObject.SetActive(show); if (show) e.text.transform.rotation = rig.Cam.transform.rotation;
            }
        }

        float Emphasis(Part part, string view)
        {
            if (view == "foundation") return part == Part.Block ? .8f : 1;
            if (view == "society" || view == "betrayal") return part == Part.Block ? .25f : .02f;
            if (PersonFocus.IsView(view)) return part == Part.Block || part == Part.Pillars ? .12f : .02f;
            switch (part)
            {
                case Part.Block: return .55f;
                case Part.Strata: return view == "overview" || view == "landscape" ? .45f : .7f;
                case Part.Pillars: return view == "overview" || view == "landscape" || view == "companies" ? .10f : view == "capture" ? .2f : .35f;
                case Part.Benefits: return view == "people" ? .12f : lod >= 2 ? .5f : .06f;
                case Part.Taxes: return lod >= 2 ? .45f : view == "rivers" ? .3f : .05f;
                case Part.Regulation: return view == "capture" ? .8f : lod >= 1 ? .45f : .15f;
                case Part.Seats: return 1;
                case Part.Selected: return 1;
            }
            return 1;
        }

        void Build()
        {
            Clear(); snapshot = LandService.Current; land = HillLandscapeLayer.Current; selection = EconomyState.SelectedPlayer;
            if (land == null || snapshot?.Players?.Players == null) return;
            content = new GameObject("Government"); content.transform.SetParent(transform, false);
            var data = LandService.Model.Data; var circuit = data.Circuit; var players = snapshot.Players.Players;
            double gdp = snapshot.Land.Gdp, scale = circuit != null && circuit.IncomeOf("gdp") > 0 ? gdp / circuit.IncomeOf("gdp") : 1;
            var us = History?.Us(land.Year);
            pillars.Clear();
            Vector3 Under(params string[] ids)
            {
                Vector3 sum = Vector3.zero; int n = 0;
                foreach (string id in ids) { var ind = data.IndustryById(id); if (ind == null) continue; sum += land.Hills[ind.Index].Center; n++; }
                return n > 0 ? sum / n : Vector3.zero;
            }
            Vector3 Of(Func<Player, bool> who)
            {
                Vector3 sum = Vector3.zero; int n = 0;
                for (int p = 0; p < players.Length; p++) if (who(players[p])) { sum += land.People[p]; n++; }
                return n > 0 ? sum / n : Vector3.zero;
            }
            // The year's budget from the history when there is one, else FY2025 scaled by GDP.
            double G(string key, Func<GovernmentHistory.UsYear, float> share) => us != null ? share(us) * gdp : (circuit?.GovernmentOf(key) ?? 0) * scale;
            var ss = Add("SOCIAL SECURITY", "retirement and disability pensions, federal", G("socialSecurity", u => u.socialSecurity), Of(p => p.Group == Group.RetiredSocialSecurity || p.Group == Group.RetiredSavings), Ice);
            var health = Add("MEDICARE + MEDICAID", "federal health programs; hospitals and insurers are paid", G("health", u => u.health), Under("health"), Ice);
            Add("MILITARY", "national defense, federal", G("defense", u => u.defense), Under("manufacturing", "hardware"), StratumColor[1]);
            var local = Add("SCHOOLS · POLICE · ROADS · COURTS", "state and local purchases", G("stateLocalPurchases", u => u.stateLocal), Of(p => p.Rung <= 1 && p.Group != Group.Top1), Blue);
            var other = Add("OTHER FEDERAL", "agencies, food and income support, veterans", G("otherOutlays", u => u.incomeSecurity + u.otherOutlays), Of(p => p.Group == Group.OutOfWork || p.Group == Group.WorkingPoor), Steel);
            var bonds = Add("INTEREST TO BONDHOLDERS", "net interest on the federal debt: taxes paid to lenders", G("netInterest", u => u.netInterest), Of(p => p.Group == Group.Top1), Gold);
            if (bonds.Top != Vector3.zero) bonds.Top.y = land.CloudY - 4;
            double topCapital = 0; foreach (var p in players) if (p.Group == Group.Top1) topCapital += Math.Max(0, p.Capital);
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i]; double otherBenefits = Math.Max(0, p.Transfers - p.SocialSecurity);
                if (p.SocialSecurity > .05) ss.Serves.Add((i, p.SocialSecurity));
                if (otherBenefits > .05) { health.Serves.Add((i, otherBenefits * .6)); other.Serves.Add((i, otherBenefits * .4)); }
                if (p.Rung <= 1 && p.Group != Group.Top1) local.Serves.Add((i, 0));
                if (p.Group == Group.Top1 && topCapital > 0) bonds.Serves.Add((i, bonds.Dollars * Math.Max(0, p.Capital) / topCapital));
            }
            Block(us, circuit, scale, gdp); Columns(); Benefits(); Taxes(us, gdp); Regulation(); SeatActors(); Selected();
        }

        Pillar Add(string name, string note, double dollars, Vector3 under, Color color)
        {
            int k = pillars.Count;
            var pillar = new Pillar { Name = name, Note = note, Dollars = dollars, Color = color,
                Base = new Vector3(HillLandscape.FootX + 8 + k * land.CrestWidth / 6.5f, HillLandscape.Bedrock - 2, land.CrestZ - 26 - (k % 2) * 12) };
            pillar.Top = under == Vector3.zero ? pillar.Base + Vector3.up * 14 : new Vector3(under.x, land.Ground(under.x, under.z) - 1.2f, under.z);
            pillars.Add(pillar); return pillar;
        }

        /// <summary>The block through time: its strata on the front (road) and back faces, the strata's boundaries along
        /// the wake, receipts as a white line, decade ticks, and the crest's cross-section with its numbers.</summary>
        void Block(GovernmentHistory.UsYear us, Why.Economy.Data.CircuitFile circuit, double scale, double gdp)
        {
            var faces = new SurfaceMeshBuilder(); var edges = new LineMeshBuilder(); var strata = new LineMeshBuilder();
            float x0 = land.XMin, x1 = land.XMax, top = HillLandscape.Bedrock;
            var years = new List<GovernmentHistory.UsYear>();
            if (History != null) foreach (var y in History.us) if (y.year >= land.FirstYear && y.year <= land.Year) years.Add(y);
            if (years.Count >= 2)
            {
                int n = years.Count;
                var bounds = new float[5][]; for (int k = 0; k < 5; k++) bounds[k] = new float[n];
                var receipts = new Vector3[n]; var zs = new float[n];
                for (int i = 0; i < n; i++)
                {
                    var y = years[i]; zs[i] = HillLandscape.Z(y.year);
                    float[] part = { y.Social, y.defense, y.netInterest, y.otherOutlays };
                    bounds[0][i] = top; for (int k = 0; k < 4; k++) bounds[k + 1][i] = bounds[k][i] - part[k] * DepthPerShare;
                    receipts[i] = new Vector3(x0 - .05f, top - y.receipts * DepthPerShare, zs[i]);
                }
                foreach (float x in new[] { x0, x1 })
                    for (int k = 0; k < 4; k++)
                    {
                        var upper = new Vector3[n]; var lower = new Vector3[n];
                        for (int i = 0; i < n; i++) { upper[i] = new Vector3(x, bounds[k][i], zs[i]); lower[i] = new Vector3(x, bounds[k + 1][i], zs[i]); }
                        Color c = StratumColor[k]; c.a = x == x0 ? .42f : .22f;
                        faces.AddBand(upper, lower, new[] { (Color32)c }, 0, 1);
                    }
                // The bottom of the block across the wave, and each stratum boundary along the front face.
                var bottom0 = new Vector3[n]; var bottom1 = new Vector3[n];
                for (int i = 0; i < n; i++) { bottom0[i] = new Vector3(x0, bounds[4][i], zs[i]); bottom1[i] = new Vector3(x1, bounds[4][i], zs[i]); }
                faces.AddBand(bottom0, bottom1, new[] { (Color32)new Color(.30f, .32f, .35f, .30f) }, 0, 1);
                for (int k = 1; k <= 4; k++)
                {
                    var line = new Vector3[n]; for (int i = 0; i < n; i++) line[i] = new Vector3(x0, bounds[k][i], zs[i]);
                    strata.AddPolyline(line, WithAlpha(Color.Lerp(StratumColor[k - 1], Color.white, .3f), .8f), 1.2f, 0, 0, 1.4f);
                }
                strata.AddPolyline(receipts, new Color(.95f, .97f, 1f, .9f), 1.6f, 0, 0, 2.4f);
                for (int i = 0; i < n; i++)
                {
                    if (years[i].year % 10 != 0) continue;
                    edges.AddSegment(new Vector3(x0, top, zs[i]), new Vector3(x0, bounds[4][i], zs[i]), WithAlpha(Color.white, .35f), .8f, 0, 0, 1.2f);
                    Label(years[i].year + "\n" + LandFacts.Percent(years[i].Outlays) + " of GDP", new Vector3(x0 - 3, bounds[4][i] - 1.2f, zs[i]), .16f, new Color(.78f, .82f, .88f), 1);
                }
                // The crest's cross-section: the year's strata across the whole wave.
                var cur = years[n - 1]; float[] shares = { cur.Social, cur.defense, cur.netInterest, cur.otherOutlays };
                float yk = top;
                for (int k = 0; k < 4; k++)
                {
                    float d = shares[k] * DepthPerShare;
                    Rect(faces, x0, x1, yk, yk - d, land.CrestZ, StratumColor[k], .55f);
                    edges.AddPolyline(new[] { new Vector3(x0, yk - d, land.CrestZ), new Vector3(x1, yk - d, land.CrestZ) }, WithAlpha(Color.white, .4f), 1, 0, 0, 1.4f);
                    Label("<b>" + StratumName[k] + "</b> " + LandFacts.Money(shares[k] * gdp) + " · " + LandFacts.Percent(shares[k], 1) + " of GDP",
                        new Vector3(x0 + 14, yk - d * .5f, land.CrestZ + .6f), .2f, Color.Lerp(StratumColor[k], Color.white, .45f), 0);
                    yk -= d;
                }
                Label("<b>THE GOVERNMENT</b> · " + cur.year + "\nreceipts " + LandFacts.Money(cur.receipts * gdp) + " (" + LandFacts.Percent(cur.receipts, 1) +
                    ") · outlays " + LandFacts.Money(cur.Outlays * gdp) + " (" + LandFacts.Percent(cur.Outlays, 1) + ")" +
                    "\nincome tax " + LandFacts.Percent(cur.individual, 1) + " · payroll " + LandFacts.Percent(cur.payroll, 1) + " · corporate " + LandFacts.Percent(cur.corporate, 1) + " of GDP",
                    new Vector3(x0 + 10, top + 3, land.CrestZ + 6), .3f, new Color(.80f, .86f, .94f), 0);
            }
            else
            {
                // No history: a flat slab with the FY2025 figures scaled to the year.
                Rect(faces, x0, x1, top, top - 14, land.CrestZ, StratumColor[3], .4f);
                double receipts = (circuit?.GovernmentOf("receipts") ?? 0) * scale;
                Label("<b>THE GOVERNMENT</b> · " + land.Year + " (FY2025 scaled by GDP)\nfederal receipts " + LandFacts.Money(receipts), new Vector3(x0 + 10, top + 3, land.CrestZ + 6), .3f, new Color(.80f, .86f, .94f), 0);
            }
            // The block's outline: its top along the wake and its crest face.
            edges.AddPolyline(new[] { new Vector3(x0, top, land.TailZ), new Vector3(x0, top, land.CrestZ), new Vector3(x1, top, land.CrestZ), new Vector3(x1, top, land.TailZ) }, WithAlpha(Steel, .6f), 1, 0, 0, 1.2f);
            double corp = (circuit?.GovernmentOf("corporateTax") ?? 0) * scale, dividends = (circuit?.IncomeOf("dividends") ?? 0) * scale, buybacks = (circuit?.IncomeOf("buybacks") ?? 0) * scale;
            if (us != null) corp = us.corporate * gdp;
            Label("corporate tax " + LandFacts.Money(corp) + " vs dividends " + LandFacts.Money(dividends) + " + buybacks " + LandFacts.Money(buybacks) + (us != null && land.Year != 2025 ? " (payouts scaled from 2025)" : ""),
                new Vector3(x1 - 30, top + 1.6f, land.CrestZ + 4), .2f, new Color(1, .85f, .6f), 1);
            Surface(faces, Part.Block); Lines(edges, Part.Block, false, true); Lines(strata, Part.Strata, false, true);
        }

        void Columns()
        {
            var b = new LineMeshBuilder();
            foreach (var p in pillars)
            {
                float r = .5f + Mathf.Sqrt((float)Math.Max(0, p.Dollars)) * .045f;
                Vector3 bend = new Vector3(p.Top.x, Mathf.Lerp(p.Base.y, p.Top.y, .55f), p.Top.z);
                var spine = new List<LinePoint>();
                for (int s = 0; s <= 30; s++)
                {
                    float t = s / 30f; Vector3 q = Vector3.Lerp(Vector3.Lerp(p.Base, bend, t), Vector3.Lerp(bend, p.Top, t), t);
                    spine.Add(new LinePoint(q, p.Color, 2.2f, 0, 2.4f));
                    if (s % 3 == 0) Circle(b, q, r * (1 - .35f * t), WithAlpha(p.Color, .75f), 1.2f, 1.8f);
                }
                b.AddFlowPath(spine, 0, .5f);
                Circle(b, p.Base, r * 1.4f, p.Color, 2, 2.6f);
                Label("<b>" + p.Name + "</b> " + LandFacts.Money(p.Dollars) + "\n" + p.Note, p.Base + Vector3.up * 2.4f + Vector3.back * (r + 1), .22f, Color.Lerp(p.Color, Color.white, .35f), 1);
            }
            var renderer = Lines(b, Part.Pillars, true, true); renderer.sharedMaterial.SetFloat("_FlowFreq", 1.2f); renderer.sharedMaterial.SetFloat("_FlowSpeed", .45f);
        }

        void Benefits()
        {
            var b = new LineMeshBuilder();
            foreach (var p in pillars)
                foreach (var serve in p.Serves)
                {
                    Vector3 to = land.People[serve.player] + Vector3.up * .3f;
                    float width = serve.dollars > 0 ? .5f + Mathf.Sqrt((float)serve.dollars) * .3f : .5f;
                    Arch(b, p.Top, to, WithAlpha(p.Color, .7f), width, 1.8f, 1 + Vector3.Distance(p.Top, to) * .05f);
                }
            Lines(b, Part.Benefits, true, true);
        }

        /// <summary>Taxes in: every household's income and payroll taxes drop into the block; each industry's corporate
        /// tax runs down from its market in proportion to its operating surplus.</summary>
        void Taxes(GovernmentHistory.UsYear us, double gdp)
        {
            var b = new LineMeshBuilder(); var players = snapshot.Players.Players;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i].Taxes <= .05) continue;
                Vector3 from = land.People[i]; var drop = new LinePoint[10];
                for (int s = 0; s < drop.Length; s++) drop[s] = new LinePoint(Vector3.Lerp(from, new Vector3(from.x, HillLandscape.Bedrock, from.z), s / 9f), WithAlpha(Steel, .55f), .4f + Mathf.Sqrt((float)players[i].Taxes) * .22f, 0, 1.4f);
                b.AddFlowPath(drop, 0, .4f, i * .17f);
            }
            double corporate = us != null ? us.corporate * gdp : (LandService.Model.Data.Circuit?.GovernmentOf("corporateTax") ?? 0);
            double surplus = 0; foreach (var s in snapshot.Land.Sectors) if (s.Tier != Tier.Gov) surplus += Math.Max(0, s.Owners);
            for (int j = 0; j < land.Hills.Length; j++)
            {
                if (land.Hills[j].Tier == 0 || surplus <= 0) continue;
                double tax = corporate * Math.Max(0, snapshot.Land.Sectors[j].Owners) / surplus;
                if (tax < 2) continue;
                Vector3 m = land.Market(j), down = new Vector3(m.x, HillLandscape.Bedrock, Mathf.Min(m.z, land.CrestZ - 1.5f));
                var pts = new LinePoint[12];
                for (int s = 0; s < pts.Length; s++) pts[s] = new LinePoint(Vector3.Lerp(m, down, s / 11f), WithAlpha(new Color(1, .85f, .55f), .55f), .5f + Mathf.Sqrt((float)tax) * .2f, 0, 1.6f);
                b.AddFlowPath(pts, 0, .4f, j * .3f);
            }
            Lines(b, Part.Taxes, true, true);
        }

        /// <summary>Regulation up into every hill (grey, Jev's regulation score) and regulatory capture back down (gold).</summary>
        void Regulation()
        {
            if (History?.industries == null || History.industries.Count == 0) return;
            var b = new LineMeshBuilder(); var data = LandService.Model.Data;
            for (int j = 0; j < land.Hills.Length; j++)
            {
                var hill = land.Hills[j];
                if (hill.Tier == 0 || !History.industries.TryGetValue(data.Industries[j].Id, out var r)) continue;
                Vector3 foot = new Vector3(hill.Center.x - 1.2f, HillLandscape.Bedrock, land.CrestZ - 3), into = land.Market(j) + Vector3.up * .5f;
                Strand(b, foot, into, new Color(.80f, .83f, .88f, .75f), .6f + 3.2f * r.regulation, 1.4f + 1.6f * r.regulation, 1.2f);
                if (r.capture > .05f)
                    Strand(b, into + Vector3.right * 1.2f, foot + Vector3.right * 2.4f, WithAlpha(Gold, .75f), .4f + 3.2f * r.capture, 1.6f + 2.4f * r.capture, -1.2f);
                Label(data.Industries[j].Name + "\nregulated " + LandFacts.Percent(r.regulation) + " · capture " + LandFacts.Percent(r.capture),
                    Vector3.Lerp(foot, into, .5f) + Vector3.left * 2, .14f, new Color(.82f, .85f, .9f), 2);
            }
            Lines(b, Part.Regulation, true, true);
        }

        /// <summary>The year's political actors at the block's crest edge: the government file's officeholders (presidents)
        /// and the popular people in office, deduplicated by name; heads of state first, then the executive, then the
        /// national legislature, then state and local offices behind them.</summary>
        void SeatActors()
        {
            seats.Clear(); int year = land.Year;
            var byName = new Dictionary<string, Seat>(StringComparer.OrdinalIgnoreCase);
            if (History != null)
                foreach (var a in History.InOffice("united_states", year))
                    if (!byName.ContainsKey(a.name)) byName[a.name] = new Seat { Name = a.name, Office = a.office, Executive = !a.office.Contains("legislat"), National = true };
            var people = HillSocialLayer.People;
            if (people != null)
                foreach (var p in people.people)
                {
                    if (p.region != "us" || !p.InOffice(year, out var o)) continue;
                    if (!byName.TryGetValue(p.name, out var seat)) byName[p.name] = seat = new Seat { Name = p.name, Office = o.Name, Executive = o.Executive, National = o.National };
                    seat.Person = p.Index;
                }
            var list = new List<Seat>(byName.Values);
            // Reform movements press on the state from outside: they stand in front of the block, not on it.
            int Rank(Seat s) => s.Office.Contains("reform") ? 5 : s.Office.Contains("head of state") || s.Office.StartsWith("President of") ? 0 : s.Office.StartsWith("Vice President") ? 1 : s.National && s.Executive ? 2 : s.National ? 3 : 4;
            list.Sort((a, c) => Rank(a).CompareTo(Rank(c)));
            var b = new LineMeshBuilder(); float x0 = land.XMin + 4, span = land.XMax - land.XMin - 8; int front = 0, back = 0, outside = 0;
            foreach (var s in list)
            {
                int rank = Rank(s); bool local = rank == 4, reform = rank == 5; int k = reform ? outside++ : local ? back++ : front++;
                s.At = new Vector3(x0 + (k + .5f) * Mathf.Min(7f, span / Math.Max(1, list.Count)), HillLandscape.Bedrock + .3f, land.CrestZ + (reform ? 4 : local ? -6 : -1.8f));
                Color c = reform ? new Color(.92f, .94f, .98f) : new Color(.70f, .74f, .80f);
                b.AddSegment(s.At, s.At + Vector3.up * 1.4f, c, 2, 0, 0, 2.2f); Circle(b, s.At + Vector3.up * 1.7f, .3f, c, 1.6f, 2.4f);
                Circle(b, s.At, .7f, WithAlpha(c, .6f), 1.2f, 1.6f);
                Label(s.Name + "\n" + s.Office, s.At + Vector3.up * 2.6f, .16f, new Color(.86f, .9f, .95f), rank <= 1 || reform ? 0 : 1);
                seats.Add(s);
            }
            Lines(b, Part.Seats, false, true);
            if (seatedFor != land) { seatedFor = land; SeatsVersion++; }
        }

        void Selected()
        {
            int i = EconomyState.SelectedPlayer; var players = snapshot.Players.Players;
            if (i < 0 || i >= players.Length) return;
            var b = new LineMeshBuilder(); Vector3 at = land.People[i];
            if (players[i].Taxes > .01) b.AddFlowPath(new[] { new LinePoint(at, Steel, 2.5f, 0, 3), new LinePoint(new Vector3(at.x, HillLandscape.Bedrock, at.z), Steel, 2.5f, 0, 3) }, 0, .4f);
            foreach (var p in pillars) foreach (var s in p.Serves) if (s.player == i && s.dollars > 0) Arch(b, p.Top, at + Vector3.up * .3f, p.Color, 2.5f, 4, 2);
            Lines(b, Part.Selected, true, true);
        }

        static void Strand(LineMeshBuilder b, Vector3 a, Vector3 z, Color color, float width, float intensity, float bow)
        {
            var pts = new LinePoint[21];
            for (int s = 0; s < pts.Length; s++) { float t = s / 20f; pts[s] = new LinePoint(Vector3.Lerp(a, z, t) + Vector3.right * Mathf.Sin(t * Mathf.PI) * bow, color, width, 0, intensity); }
            b.AddFlowPath(pts, 0, .4f);
        }

        static void Rect(SurfaceMeshBuilder b, float x0, float x1, float y0, float y1, float z, Color color, float alpha)
        {
            color.a = alpha;
            b.AddBand(new[] { new Vector3(x0, y0, z), new Vector3(x1, y0, z) }, new[] { new Vector3(x0, y1, z), new Vector3(x1, y1, z) }, new[] { (Color32)color }, 0, 1);
        }

        static void Arch(LineMeshBuilder b, Vector3 a, Vector3 z, Color color, float width, float intensity, float lift)
        {
            var pts = new LinePoint[25];
            for (int s = 0; s < pts.Length; s++) { float t = s / 24f; pts[s] = new LinePoint(Vector3.Lerp(a, z, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * lift, color, width, 0, intensity); }
            b.AddFlowPath(pts, 0, .5f);
        }

        static void Circle(LineMeshBuilder b, Vector3 c, float r, Color color, float width, float intensity)
        {
            var pts = new Vector3[49];
            for (int s = 0; s < pts.Length; s++) { float a = s * Mathf.PI * 2 / 48; pts[s] = c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r); }
            b.AddPolyline(pts, color, width, 0, 0, intensity);
        }

        MeshRenderer Lines(LineMeshBuilder b, Part part, bool flow, bool additive)
        {
            Mesh mesh = b.ToMesh("Government " + part); owned.Add(mesh);
            Material material = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3190, additive, 0, flow ? 1 : 0)); owned.Add(material);
            var r = AddMesh("Government " + part, mesh, material); r.transform.SetParent(content.transform, false);
            renderers.Add((r, part)); return r;
        }

        void Surface(SurfaceMeshBuilder b, Part part)
        {
            Mesh mesh = b.ToMesh("Government " + part); owned.Add(mesh);
            Material material = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1, 3185)); owned.Add(material);
            var r = AddMesh("Government " + part, mesh, material); r.transform.SetParent(content.transform, false);
            renderers.Add((r, part));
        }

        void Label(string text, Vector3 at, float size, Color color, int minLod)
        {
            var go = new GameObject("Government label"); go.transform.SetParent(content.transform, false); go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>(); t.text = text; t.fontSize = 24; t.alignment = TextAlignmentOptions.Center;
            t.color = color; t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.sizeDelta = new Vector2(40, 8);
            go.transform.localScale = Vector3.one * size; labels.Add((t, minLod));
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        void Clear()
        {
            foreach (var o in owned) if (o) Destroy(o);
            if (content) Destroy(content);
            owned.Clear(); renderers.Clear(); labels.Clear(); content = null;
        }

        void OnDestroy() { Clear(); pillars.Clear(); seats.Clear(); History = null; }
    }
}

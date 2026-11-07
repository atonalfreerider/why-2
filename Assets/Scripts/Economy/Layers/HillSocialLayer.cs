using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Why.Economy.Emphasis;
using Why.Economy.Land;
using Why.Economy.Model;
using Why.Humans.Social;

namespace Why.Economy.Layers
{
    /// <summary>
    /// The United States' popular people above the economy (Data/social/people.json): a cloud floating over the wave,
    /// apart from and higher than the gold pool of diversified wealth, laid out by the social graph's own force-directed
    /// layout and drawn white, never gold, so attention and capital never read as one; both are named from any distance.
    /// Category clouds (Kardashian Jenner, leading men, American sports, MAGA, liberal, tech ...) are rings in their
    /// colors around their members.
    /// <para>Where a popular person is also a capital holder (Jev judged which listed company they found, run or
    /// control), they are not in the cloud: their star stands on the gold inner circle of owners around that company's
    /// tower (or on its lifeline at the crest when it has no tower), joined to their other companies by gold, with only a
    /// faint thread back to the cloud. Officeholders of the shown year sit at the government's crest edge
    /// (<see cref="HillFoundationLayer.Seats"/>). People appear once they are adults alive in the shown year; their size
    /// is their 2025 following summed across Instagram, TikTok, YouTube and X, and Instagram's follows place them, so before
    /// Instagram (<see cref="PlatformYear"/>) the cloud is hidden, and in its own view a faint ghost named as the 2025
    /// snapshot.</para>
    /// <para>Level of detail by camera distance: far, the glow and the most followed stars; nearer, every star and the
    /// category rings and names of the most followed; close, the mutual follows and every name (outside the social view, a
    /// person's name only where it is large enough to read).</para>
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillSocialLayer : GraphLayer
    {
        public override int Order => 59;
        public override IEnumerable<string> RequiredTexts => new[] { SocialPeople.Path, CorporatePrograms.Path };
        /// <summary>The parsed social graph (null without the file); read by the government's seats.</summary>
        public static SocialPeople People { get; private set; }
        /// <summary>Where each drawn person stands this year (land-local), and how: 0 cloud, 1 company, 2 office.</summary>
        public static readonly Dictionary<int, (Vector3 at, int place)> Placed = new Dictionary<int, (Vector3, int)>();
        public static int HoverPerson { get; private set; } = -1;
        enum Part { Glow, Stars, Bright, Ties, Rings, Anchors, Threads, Elite, Loop, Count }
        /// <summary>Each part's emphasis key, by <see cref="Part"/>: its alpha in every view and state is the emphasis table's
        /// (EmphasisTable), and its meshes carry the key's highlight ids.</summary>
        static readonly int[] Keys = EconomyEmphasis.Keys("social", "glow", "stars", "bright", "ties", "rings", "anchors", "threads", "elite", "loop");
        static float Id(Part part) => EconomyEmphasis.Id(Keys[(int)part]);
        const float CloudRadius = 32, Squash = .5f;
        /// <summary>The year Instagram, whose follows place the stars, began (their sizes are 2025 follower counts summed
        /// across platforms): before it they measure nobody's attention of the time.</summary>
        const int PlatformYear = HillPresets.AttentionFirstYear;
        /// <summary>Label kinds (the person field of <see cref="labels"/> for labels that are not a person): the cloud's
        /// name, shown at every distance; the social view's own; the ones shown from mid distance.</summary>
        const int NameLabel = -3, SocialLabel = -2, NearLabel = -1;
        /// <summary>How far the cloud's name stands toward the past from the cloud's center (land units): over the cloud's
        /// past half, which the starting view shows right of the HUD's title panel and the attention view left of the year's
        /// readout (about 50 px clear of each at 1920 x 1080).</summary>
        const float TitleBack = 24;
        /// <summary>How far a flow's pulse brightens a line (1 = the shader's fourfold): a sheen, under the bloom.</summary>
        const float Pulse = .2f;
        /// <summary>How much of the white a star's strokes lay down in the cloud, where the stars and ties are blended, not
        /// added (decision 36: the hills have no tonemapper, and the core's added stars summed to a flat white). Where strokes
        /// cross, the white builds as the tonemapper used to compress their sum (one .65 of it, two .88, three .96), so the
        /// denser core still reads brighter, and never passes the white.</summary>
        const float StarAlpha = .65f;
        /// <summary>The smallest em (px at 1080 lines) at which a person's name shows outside the social view: the road's
        /// fan's, by the same measure (HillWaveLayer.RoadMinPx and EmPx). The level of detail goes by the cloud's distance,
        /// and the office seats far in front of it showed their names 4-5 px tall in the people view.</summary>
        const float NameMinPx = HillWaveLayer.RoadMinPx;
        static readonly Color Gold = new Color(1, .68f, .20f), Grey = new Color(.70f, .74f, .80f);
        readonly List<(Renderer renderer, Part part)> renderers = new List<(Renderer, Part)>();
        readonly List<(TextMeshPro text, int person, float fame)> labels = new List<(TextMeshPro, int, float)>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        LandSnapshot snapshot; HillLandscape land; GameObject content; LandFrame frame;
        Vector3 center;
        int seats = -1, lod = -1;
        /// <summary>The cloud's name (kept legible from any distance), and its text without and with the key of its rings, and
        /// with the key in portrait (its long lines broken in two).</summary>
        TextMeshPro title; string titleFar, titleNear, titleNearPortrait;
        /// <summary>The font's material drawn after the hills and the cloud's lines (queue 3252), shared by the labels read over
        /// them; and the dark plate behind those that stand on bright marks (a unit square scaled to each label, and its
        /// material drawn just before the raised text, queue 3251).</summary>
        Material raisedText, plateMaterial; Mesh plateQuad;

        string programsJson;
        public override void Prepare(GraphContext ctx) { People = SocialPeople.Parse(ctx.Text(SocialPeople.Path)); programsJson = ctx.Text(CorporatePrograms.Path); }
        public override void Upload(GraphContext ctx)
        {
            frame = EconomyStage.Land(); frame.Place(transform);
            // outside its own view the cloud's detail follows its distance, and before Instagram the cloud is a ghost
            // (EmphasisTable.SocialDetail, SocialEarly)
            foreach (string key in new[] { "social.stars", "social.ties", "social.rings", "social.threads", "social.elite" })
                EconomyEmphasis.Multiplier(key, "level of detail", () => EmphasisTable.SocialDetail(key, EconomyEmphasis.View, lod));
            foreach (string key in new[] { "social.glow", "social.stars", "social.bright", "social.ties", "social.rings", "social.anchors", "social.threads", "social.elite", "social.loop" })
                EconomyEmphasis.Multiplier(key, "before Instagram", () => EmphasisTable.SocialEarly(land != null && land.Year < PlatformYear));
        }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (People == null || HillLandscapeLayer.Current == null || LandService.Current == null) return;
            // Rebuild for a new year, and once the firms and seats it anchors to have been rebuilt.
            if (snapshot != LandService.Current || land != HillLandscapeLayer.Current || seats != HillFoundationLayer.SeatsVersion)
            {
                if (!HillRebuilds.Take()) return;
                Build();
            }
            if (content == null) return;
            string view = LandView.PresetId;
            // Off its topic the cloud is not drawn: in the person and focused views, in the money's own views (who keeps it,
            // the rivers, the bedrock, the roots), in the companies' lives (whose key names no white mark, and whose frame
            // cuts the cloud at its top, its name above it), in the games (society, betrayal) and in the present taken apart
            // (whose own cloud comes with its figures, and which the wave's cloud stood behind at full strength); before Instagram only its
            // own view shows it, as a ghost.
            bool early = land.Year < PlatformYear, social = view == "social";
            bool hidden = view == "mind" || view == "section" || PersonFocus.IsView(view) || view == "mobility" || view == "you" || view == "money" || view == "regimes"
                || view == "capture" || view == "rivers" || view == "foundation" || view == "roots" || view == "companies" || view == "society" || view == "betrayal" || view == "present" || early && !social;
            content.SetActive(!hidden); if (hidden) { HoverPerson = -1; return; }
            float d = Vector3.Distance(rig.Cam.transform.position, frame.World(center));
            lod = d < 45 ? 2 : d < 140 ? 1 : 0;
            foreach (var e in renderers) EconomyEmphasis.Apply(e.renderer, Keys[(int)e.part]);
            foreach (var l in labels)
            {
                bool show = l.person == NameLabel || (l.person == SocialLabel ? social : l.person < 0 ? lod >= 1 || social
                    : lod >= 2 || (lod >= 1 || social) && l.fame > .62f || Placed.TryGetValue(l.person, out var p) && p.place > 0 && (lod >= 1 || social));
                // outside its own view a person's name too small to read is not shown (NameMinPx)
                if (show && l.person >= 0 && !social) show = HillWaveLayer.EmPx(rig, l.text.transform) >= NameMinPx;
                l.text.gameObject.SetActive(show); if (show) l.text.transform.rotation = rig.Cam.transform.rotation;
            }
            if (title)
            {
                // the name reads from any distance (about 12 px capitals); the key of the rings joins it nearer or in its view.
                // In portrait the attention view's camera stands twice as far back, so the name (the same size on screen) is
                // twice as large in the scene and, centered on its place, reaches down into the names over the cloud: there it
                // stands on its place instead, and the lines its long ones break into are added upward
                bool near = lod >= 1 || social, tall = near && ScreenLayout.IsPortrait;
                string text = tall ? titleNearPortrait : near ? titleNear : titleFar;
                if (title.text != text) title.text = text;
                title.verticalAlignment = tall ? VerticalAlignmentOptions.Bottom : VerticalAlignmentOptions.Middle;
                title.transform.localScale = Vector3.one * Mathf.Max(.42f, Vector3.Distance(rig.Cam.transform.position, title.transform.position) * .0055f);
            }
            Pick(rig.Cam);
        }

        void Build()
        {
            Clear(); Placed.Clear();
            snapshot = LandService.Current; land = HillLandscapeLayer.Current; seats = HillFoundationLayer.SeatsVersion;
            if (land == null) return;
            content = new GameObject("Popular people"); content.transform.SetParent(transform, false);
            int year = land.Year; bool early = year < PlatformYear;
            // Above the wave, over the road side of the crest and higher than the gold pool of wealth.
            center = new Vector3(HillLandscape.FootX + land.CrestWidth * .30f, land.CloudY + 20, land.CrestZ - 10);
            var lives = CompanyLives.Get();
            var stars = new LineMeshBuilder(); var bright = new LineMeshBuilder(); var ties = new LineMeshBuilder(); var rings = new LineMeshBuilder();
            var anchors = new LineMeshBuilder(); var threads = new LineMeshBuilder(); var glow = new SurfaceMeshBuilder();
            var us = People.ByRegion[Math.Max(0, People.RegionIndex("us"))];
            var shown = new List<SocialPeople.Person>();
            foreach (var p in us) if (p.Alive(year) && year - p.born.Value >= 18) shown.Add(p);
            shown.Sort((a, b) => b.fol.CompareTo(a.fol));
            var seatOf = new Dictionary<int, Vector3>();
            foreach (var s in HillFoundationLayer.Seats) if (s.Person >= 0) seatOf[s.Person] = s.At + Vector3.up * 2.2f;
            var cats = new Dictionary<int, List<Vector3>>(); var groups = new List<TextMeshPro>();
            float top = center.y;   // the highest star in the cloud: its name stands just above it, inside the starting frames
            for (int rank = 0; rank < shown.Count; rank++)
            {
                var p = shown[rank];
                float career = Mathf.Clamp01((year - p.born.Value - 18) / 8f), fame = p.Fame * (.35f + .65f * career);
                Vector3 inCloud = center + Vector3.Scale(Spread(p.Layout), new Vector3(CloudRadius, CloudRadius * Squash, CloudRadius));
                // Capital first: a person who controls a listed company stands in its circle of owners. Stars are white at
                // intensity 1: fame is their size, and bloom is left to what is hovered or selected.
                if (Anchor(p, lives, out Vector3 firm, out var others))
                {
                    Star(anchors, Id(Part.Anchors), firm, .35f + .55f * fame);
                    foreach (var o in others) Rope(anchors, Id(Part.Anchors), firm, o, WithAlpha(Gold, .7f), 1.2f, 1.5f);
                    Rope(threads, Id(Part.Threads), firm, inCloud, WithAlpha(SocialPeople.White, .35f), .6f, 4);
                    if (seatOf.TryGetValue(p.Index, out Vector3 office)) Rope(threads, Id(Part.Threads), firm, office, WithAlpha(Grey, .5f), .7f, -3);
                    Placed[p.Index] = (firm, 1); Name(p, firm, fame, true);
                    continue;
                }
                if (seatOf.TryGetValue(p.Index, out Vector3 seat))
                {
                    Star(anchors, Id(Part.Anchors), seat, .3f + .5f * fame);
                    Rope(threads, Id(Part.Threads), seat, inCloud, WithAlpha(SocialPeople.White, .25f), .5f, 6);
                    Placed[p.Index] = (seat, 2); Name(p, seat, fame, true);
                    continue;
                }
                Star(rank < 16 ? bright : stars, Id(rank < 16 ? Part.Bright : Part.Stars), inCloud, .22f + .7f * fame, StarAlpha);
                Placed[p.Index] = (inCloud, 0); top = Mathf.Max(top, inCloud.y);
                if (p.cloud >= 0) { if (!cats.TryGetValue(p.cloud, out var list)) cats[p.cloud] = list = new List<Vector3>(); list.Add(inCloud); }
                if (rank < 60 || fame > .6f) Name(p, inCloud, fame, false);
            }
            foreach (var e in People.edges)
            {
                if (!Placed.TryGetValue(e[0], out var a) || !Placed.TryGetValue(e[1], out var b)) continue;
                Color c = SocialPeople.White; c.a = e.Length > 2 && e[2] > 0 ? .28f : .12f;
                ties.AddSegment(a.at, b.at, c, .7f, 0, Id(Part.Ties));
            }
            foreach (var kv in cats)
            {
                if (kv.Value.Count < 3) continue;
                Vector3 c = Vector3.zero; foreach (var v in kv.Value) c += v; c /= kv.Value.Count;
                float spread = 0; foreach (var v in kv.Value) spread += (v - c).sqrMagnitude;
                float r = Mathf.Clamp(Mathf.Sqrt(spread / kv.Value.Count) * 1.5f, 2, 12);
                Color col = People.CloudColor(kv.Key); col.a = .6f;
                Circle(rings, Id(Part.Rings), c, r, col, 1.4f, 1); Circle(rings, Id(Part.Rings), c + Vector3.up * .3f, r * 1.04f, WithAlpha(col, .25f), 1, 1);
                // its name on a plate, drawn after the stars, in its ring's hue lightened to read on the plate: Stack steps the
                // names that meet the eight's plates down into the cloud's brightest band, where the stars, drawn after the
                // font's own queue, washed out a name without one
                var cloudName = Raised(TextLabel(People.CloudLabel(kv.Key), c + Vector3.up * (r * .5f + 1.5f), .3f, Color.Lerp(People.CloudColor(kv.Key), Color.white, .45f), NearLabel, 1));
                Plate(cloudName); groups.Add(cloudName);
            }
            // Influence among the influential: a ring as wide as the number of the most-followed who follow them.
            var elite = new LineMeshBuilder(); var ranked = new List<SocialPeople.Person>();
            foreach (var p in shown) if (p.elite >= 8 && Placed.TryGetValue(p.Index, out var at)) { Circle(elite, Id(Part.Elite), at.at, .4f + Mathf.Sqrt(p.elite) * .32f, WithAlpha(new Color(.85f, .9f, 1f), .55f), 1.6f, 1); ranked.Add(p); }
            ranked.Sort((a, b) => b.elite.CompareTo(a.elite));
            // The eight most followed by the others are named in the social view, on plates over the cloud's stars and names.
            // Those in the cloud give only their number, which the ring key just above says counts (RingKey): the names the
            // core's hubs crowd together stay short enough to stack (Stack) above its brightest stars, not over them. One at a
            // company's circle or an office seat stands far below the key, where a bare number would read as an age, and says
            // what it counts.
            string graph = Graph(); var names = new List<TextMeshPro>();
            for (int k = 0; k < ranked.Count && k < 8; k++)
            {
                var p = ranked[k]; var stands = Placed[p.Index];
                string count = stands.place == 0 ? p.elite.ToString() : "followed by " + p.elite + " of the " + graph + "most-followed accounts";
                var label = Raised(TextLabel(p.name + " · " + count, stands.at + Vector3.up * (2.4f + Mathf.Sqrt(p.elite) * .32f), .5f, new Color(.9f, .94f, 1f), SocialLabel, 1));
                Plate(label); names.Add(label);
            }
            // then the category clouds' names: two clouds whose centers lie close put theirs on one spot (Kardashian Jenner
            // and MAGA printed over each other under Obama's plate), and one stood under the cloud's name, in its key's letters
            Stack(names, groups);
            // the rings overlap in the core: blended, not added, as the stars and ties are (below), so their crossings do not
            // turn white
            Lines(elite, Part.Elite, false, false);
            Loop();
            Glow(glow, Id(Part.Glow), center, CloudRadius * 1.15f, .035f);
            // The cloud's name, at every distance: what the stars are and what sizes them, so they are never read as the 1%.
            // the follower counts are summed across Instagram, TikTok, YouTube and X (people.json's note), not Instagram's alone;
            // on two lines, about 370 px wide, so it fits between the HUD's panels; the key of its rings, which also says what
            // the number after a name in the cloud counts, is about 600 px in the attention view and ends about 25 px left of the
            // HUD's right panel. In portrait the name is as large against the screen's height, so its long lines (the key, about
            // 1,030 px; before Instagram, the snapshot's note) would run past the 1080 px wide frame: there each breaks in two
            // (turn)
            string Heading(string turn) => "<b>ATTENTION</b> · the most-followed Americans\n<size=75%>followers across platforms, " + (early
                ? "a 2025 snapshot: drawn faint," + turn + "as Instagram, whose follows place them, began in " + PlatformYear : "2025") + "</size>";
            string RingKey(string turn) => "\n<size=75%>white rings and numbers: followed by the most-followed" + turn + "gold rings: their companies · grey: in office</size>";
            titleFar = Heading(" "); titleNear = titleFar + RingKey(" · "); titleNearPortrait = Heading("\n") + RingKey("\n");
            // above the highest star and the names of the most-followed over it (up to 5 units), not at the cloud's bound,
            // which the starting view's frame cuts off; over the cloud's past half (TitleBack), clear of the HUD's panels
            title = TextLabel(titleFar, new Vector3(center.x, top + 7.5f, center.z - TitleBack), .42f, new Color(.92f, .97f, 1f), NameLabel, 1);
            // of no height, so it centers on that place, or in portrait stands on it (Tick)
            title.rectTransform.sizeDelta = new Vector2(30, 0);
            // drawn after the hills (queue 3252), so the stars and threads drawn after the font's own queue stay off it
            Raised(title);
            Surface(glow, Part.Glow);
            // the stars and the ties blended, not added (StarAlpha): with no tonemapper over the hills, the core's crossed
            // strokes and mutual follows summed far past the white, flat and blooming through the plates of the names there.
            // The lone stars at a company or a seat are still added.
            Lines(stars, Part.Stars, false, false); Lines(bright, Part.Bright, false, false); Lines(ties, Part.Ties, false, false); Lines(rings, Part.Rings, false, false);
            Lines(anchors, Part.Anchors); Lines(threads, Part.Threads, true);
        }

        /// <summary>How many accounts the social graph holds, as people.json's note says ("the 1,139 most-followed"), with a
        /// trailing space; empty when the note does not say.</summary>
        static string Graph()
        {
            var m = System.Text.RegularExpressions.Regex.Match(People?.note ?? "", @"the ([\d,]+) most-followed");
            return m.Success ? m.Groups[1].Value + " " : "";
        }

        /// <summary>
        /// The attention loop (social view): companies pay the platforms to reach people (US digital advertising), the
        /// platforms and brands pay the creators who hold attention (US creator-economy ad spend), the people give their
        /// time and take in the wants, and spend at the companies (household spending; the share of the owners' surplus in it
        /// that Jev judged sold by persuasion). Four arcs between where each stands, as wide as the log of their dollars,
        /// each labeled with its source (corporate-programs.json economy totals; the capture ledger).
        /// </summary>
        void Loop()
        {
            CorporatePrograms.Provide(LandService.Model.Data, programsJson); var programs = CorporatePrograms.Get();
            var towers = snapshot.Land.Towers; var internet = LandService.Model.Data.IndustryById("internet");
            Vector3 firms = Vector3.zero, platforms = Vector3.zero; int nf = 0, np = 0;
            for (int t = 0; t < towers.Length; t++)
            {
                Vector3 halo = HillActivityLayer.CompanyHalo(t);
                if (internet != null && towers[t].Industry == internet.Index) { platforms += halo; np++; } else { firms += halo; nf++; }
            }
            if (nf == 0 || np == 0 || programs == null) return;
            firms /= nf; platforms /= np;
            Vector3 people = Vector3.zero; int n = 0;
            for (int i = 0; i < land.People.Length; i++) if (!land.Cloud[i]) { people += land.People[i]; n++; }
            people = n > 0 ? people / n : land.CloudCenter;
            double spending = 0; foreach (var p in snapshot.Players.Players) spending += Math.Max(0, p.Spending);
            var ledger = HillCaptureLayer.Shown; double paid = 0, persuaded = 0;
            if (ledger != null) for (int p = 0; p < ledger.Paid.Length; p++) { paid += ledger.Paid[p]; persuaded += ledger.PaidPersuaded[p]; }
            double ads = programs.Total("digitalAdRevenue.total"), social = programs.Total("digitalAdRevenue.socialMedia"),
                creators = programs.Total("influencerMarketing.usCreatorEconomyAdSpend_IAB"), minutes = programs.Total("socialMediaTime.usAdultSocialNetworkUsers_eMarketer");
            var b = new LineMeshBuilder(); Color rose = new Color(1, .30f, .52f), gold = new Color(1, .72f, .25f), white = new Color(.92f, .96f, 1f);
            float W(double billions) => 1.5f + Mathf.Log10(1 + (float)Math.Max(0, billions)) * 2.2f;
            void Arc(Vector3 a, Vector3 z, Color c, float width, float lift, string text, bool onward = false)
            {
                var pts = new LinePoint[33];
                for (int k = 0; k < pts.Length; k++) { float t = k / 32f; pts[k] = new LinePoint(Vector3.Lerp(a, z, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * lift, WithAlpha(c, .85f), width); }
                b.AddFlowPath(pts, Id(Part.Loop), .25f);
                // over the arc's middle, which a steep arc crosses in the label's own hue: drawn after the lines, on a plate;
                // an onward label starts at the middle and runs on to the right instead of centering on it
                var label = Raised(TextLabel(text, pts[16].Data + Vector3.up * 3.5f, .72f, Color.Lerp(c, Color.white, .45f), SocialLabel, 1));
                if (onward) { label.alignment = TextAlignmentOptions.Left; label.rectTransform.pivot = new Vector2(0, .5f); }
                Plate(label);
            }
            // The platforms' halos and owner rings crowd the advertising arc's far end, and centered on the arc's middle the
            // name's first word stood on their glow (the attention view read "ADVE...NG"); it runs on from the middle toward
            // the firms instead, as the line falls away under it (anchored nearer the firms, it met the attention's name).
            Arc(firms, platforms, gold, W(ads), 10, "<b>ADVERTISING</b> · $" + ads.ToString("0", LandFacts.Ci) + "B of US digital ads in 2025, $" + social.ToString("0", LandFacts.Ci) + "B of it on social media (IAB/PwC)", true);
            Arc(platforms, center, gold, W(creators), 8, "<b>TO THE CREATORS</b> · ≈ $" + creators.ToString("0", LandFacts.Ci) + "B of US creator-economy ad spend (IAB 2025)");
            Arc(center, people, rose, W(spending * .01), 6, "<b>ATTENTION GIVEN, WANTS TAKEN IN</b> · about " + (minutes / 60).ToString("0.0", LandFacts.Ci) + " hours a day on social media per adult user (eMarketer 2025)");
            Arc(people, firms, rose, W(spending * .05), 4, "<b>SPENDING</b> · " + LandFacts.Money(spending) + " a year from households · " +
                (paid > 0 ? LandFacts.Percent(persuaded / paid) + " of the owners' surplus inside it sold by persuasion (Jev)" : ""));
            Lines(b, Part.Loop, true, false);
        }

        /// <summary>The company circle a capital holder stands in: their first listed company with a tower (its gold
        /// inner circle of owners), else its lifeline at the crest; the others they are tied to.</summary>
        bool Anchor(SocialPeople.Person p, CompanyLives lives, out Vector3 at, out List<Vector3> others)
        {
            at = default; others = new List<Vector3>(); bool found = false;
            if (lives == null || p.firms.Length == 0) return false;
            foreach (string id in p.firms)
            {
                int life = Array.FindIndex(lives.Lives, l => l.Id == id);
                if (life < 0 || lives.Lives[life].Founded > land.Year) continue;
                Vector3 spot;
                if (HillActivityLayer.OwnerCircle(lives.Lives[life], out Vector3 c, out float r))
                {
                    float a = (p.Index * 2.399963f) % (Mathf.PI * 2);
                    spot = c + new Vector3(Mathf.Cos(a) * r, .55f, Mathf.Sin(a) * r);
                }
                else if (HillWaveLayer.CompanyAtCrest(life, out Vector3 crest)) spot = crest + Vector3.up * 1.2f;
                else continue;
                if (!found) { at = spot; found = true; } else others.Add(spot);
            }
            return found;
        }

        void Name(SocialPeople.Person p, Vector3 at, float fame, bool placed)
        {
            var lives = CompanyLives.Get(); var names = new List<string>();
            foreach (string id in p.firms) { var l = lives == null ? null : Array.Find(lives.Lives, x => x.Id == id); names.Add(l != null ? l.Name : id); }
            string extra = placed && names.Count > 0 ? "\n" + string.Join(" · ", names) : "";
            TextLabel(p.name + extra, at + Vector3.up * (1 + fame), .16f + .12f * fame, placed ? new Color(1, .93f, .8f) : new Color(.88f, .95f, 1f), p.Index, fame);
        }

        void Pick(Camera cam)
        {
            HoverPerson = -1;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null || lod < 1) return;
            Vector2 pointer = mouse.position.ReadValue(); float best = 14 * Screen.height / 1080f;
            foreach (var kv in Placed)
            {
                Vector3 s = cam.WorldToScreenPoint(frame.World(kv.Value.at));
                float d = Vector2.Distance(pointer, s);
                if (s.z > 0 && d < best) { best = d; HoverPerson = kv.Key; }
            }
            // the hovered person's name shows however small or far (Tick turns only the names it shows)
            if (HoverPerson >= 0)
                foreach (var l in labels) if (l.person == HoverPerson) { l.text.gameObject.SetActive(true); l.text.transform.rotation = cam.transform.rotation; }
        }

        /// <summary>The force-directed layout crowds its hubs at the center; a radial power spreads the core so stars stay apart.</summary>
        static Vector3 Spread(Vector3 v)
        {
            float r = v.magnitude; return r < 1e-5f ? v : v * (Mathf.Pow(r, .55f) / r);
        }

        /// <summary>A star: three crossed strokes and a small ring, in the people's white at <paramref name="alpha"/> (the
        /// cloud's <see cref="StarAlpha"/>, blended; whole for one at a company or a seat, added).</summary>
        static void Star(LineMeshBuilder b, float id, Vector3 at, float r, float alpha = 1)
        {
            Color w = WithAlpha(SocialPeople.White, alpha);
            b.AddSegment(at - Vector3.right * r, at + Vector3.right * r, w, 1.8f, 0, id);
            b.AddSegment(at - Vector3.up * r, at + Vector3.up * r, w, 1.8f, 0, id);
            b.AddSegment(at - Vector3.forward * r, at + Vector3.forward * r, w, 1.8f, 0, id);
            Circle(b, id, at, r * .45f, w, 1.4f, .8f);
        }

        static void Rope(LineMeshBuilder b, float id, Vector3 a, Vector3 z, Color color, float width, float lift)
        {
            var pts = new LinePoint[21];
            for (int s = 0; s < pts.Length; s++) { float t = s / 20f; pts[s] = new LinePoint(Vector3.Lerp(a, z, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * lift, color, width); }
            b.AddFlowPath(pts, id, .3f);
        }

        static void Circle(LineMeshBuilder b, float id, Vector3 c, float r, Color color, float width, float intensity)
        {
            var pts = new Vector3[49];
            for (int s = 0; s < pts.Length; s++) { float a = s * Mathf.PI * 2 / 48; pts[s] = c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r); }
            b.AddPolyline(pts, color, width, 0, id, intensity);
        }

        static void Glow(SurfaceMeshBuilder b, float id, Vector3 c, float r, float alpha)
        {
            for (int axis = 0; axis < 2; axis++)
            {
                var inner = new Vector3[49]; var outer = new Vector3[49];
                for (int k = 0; k < 49; k++)
                {
                    float a = k * Mathf.PI * 2 / 48; Vector3 d = axis == 0 ? new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) : new Vector3(Mathf.Cos(a), Mathf.Sin(a) * Squash, 0);
                    inner[k] = c; outer[k] = c + d * r;
                }
                Color a0 = SocialPeople.White; a0.a = alpha; Color a1 = a0; a1.a = 0;
                b.AddBand(inner, outer, new[] { (Color32)a0 }, new[] { (Color32)a1 }, id);
            }
        }

        void Lines(LineMeshBuilder b, Part part, bool flow = false, bool additive = true)
        {
            Mesh mesh = b.ToMesh("Popular " + part); owned.Add(mesh);
            Material m = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3220, additive, 0, flow ? Pulse : 0)); owned.Add(m);
            var r = AddMesh("Popular " + part, mesh, m); r.transform.SetParent(content.transform, false); renderers.Add((r, part));
        }

        void Surface(SurfaceMeshBuilder b, Part part)
        {
            Mesh mesh = b.ToMesh("Popular " + part); owned.Add(mesh);
            Material m = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1, 3218, true)); owned.Add(m);
            var r = AddMesh("Popular " + part, mesh, m); r.transform.SetParent(content.transform, false); renderers.Add((r, part));
        }

        TextMeshPro TextLabel(string text, Vector3 at, float size, Color color, int person, float fame)
        {
            var go = new GameObject("Popular label"); go.transform.SetParent(content.transform, false); go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>(); t.text = text; t.fontSize = 24; t.alignment = TextAlignmentOptions.Center;
            t.color = color; t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.sizeDelta = new Vector2(30, 6);
            go.transform.localScale = Vector3.one * size; go.SetActive(false); labels.Add((t, person, fame));
            return t;
        }

        /// <summary>Gives a label the raised font material (<see cref="raisedText"/>). The label has woken (it was made under
        /// the active content), so it has the font's material to copy.</summary>
        TextMeshPro Raised(TextMeshPro t)
        {
            if (raisedText == null && t.fontSharedMaterial != null) { raisedText = new Material(t.fontSharedMaterial) { renderQueue = 3252 }; owned.Add(raisedText); }
            if (raisedText != null) t.fontSharedMaterial = raisedText;
            return t;
        }

        /// <summary>A dark plate behind a raised label, sized by <see cref="PlateSize"/>: the stars, names and lines behind it
        /// are hidden, so the label reads over them. Opaque: the names Stack lowers into the cloud's core stand on its densest
        /// stars, and the tenth of them a .9 plate let through (and a quarter, at .75) left those names on mid-grey. It turns
        /// and hides with the label, under its text (to the right of its place for a left-aligned one).</summary>
        void Plate(TextMeshPro t)
        {
            if (plateQuad == null)
            {
                var b = new SurfaceMeshBuilder();
                b.AddBand(new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0) }, new[] { new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0) },
                    new[] { (Color32)WithAlpha(GraphStyle.Background, 1) }, 0, 1);
                plateQuad = b.ToMesh("Popular plate"); owned.Add(plateQuad);
                plateMaterial = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1, 3251)); plateMaterial.SetFloat("_EdgeSoft", 0); owned.Add(plateMaterial);
            }
            var r = AddMesh("Popular plate", plateQuad, plateMaterial); r.transform.SetParent(t.transform, false); r.transform.localScale = PlateSize(t);
            if (t.alignment == TextAlignmentOptions.Left) r.transform.localPosition = new Vector3(t.rectTransform.rect.xMin + t.GetPreferredValues(t.text).x * .5f, 0, 0);
        }

        /// <summary>A label's plate in the label's own units: its text with the label system's margin, .35 em sideways and
        /// .12 em above and below (TextMeshPro sets 2.4 local units per em at its font size 24).</summary>
        static Vector3 PlateSize(TextMeshPro t)
        {
            Vector2 s = t.GetPreferredValues(t.text) + new Vector2(.7f, .24f) * 2.4f;
            return new Vector3(s.x, s.y, 1);
        }

        /// <summary>
        /// Keeps the names of the most followed by the others (shown only in the social view), then the category clouds' names
        /// (<paramref name="under"/>), from printing over each other in the social view: in order, each is lowered a plate's
        /// height at a time until its plate's box clears every one placed before it (lowered, not raised: the cloud's name
        /// stands just above, and a category's name starts no higher than the highest of the eight, which keep clear of it).
        /// Judged as that view's camera sees them, by angle: the hubs crowd the cloud's core at depths a tenth of the camera's
        /// distance apart, which moves them on screen by as much as a name's height.
        /// </summary>
        void Stack(List<TextMeshPro> names, List<TextMeshPro> under)
        {
            var pose = ViewPresets.Get("social").Pose();
            Quaternion view = Quaternion.Inverse(frame.Rotation) * Quaternion.Euler(pose.Pitch, pose.Yaw, 0);
            Vector3 across = view * Vector3.right, up = view * Vector3.up, ahead = view * Vector3.forward, eye = frame.Local(pose.Target) - ahead * pose.Distance;
            var placed = new List<(float x, float y, float halfX, float halfY)>();
            // places one no higher than cap and returns its height (none: behind the camera)
            float Place(TextMeshPro t, float cap)
            {
                Vector3 at = t.transform.localPosition, v = at - eye; float depth = Vector3.Dot(v, ahead);
                if (depth < 1) return float.NegativeInfinity;
                // the name's place and its plate's half size as tangents of the view (across and up over the depth)
                Vector3 half = PlateSize(t) * (t.transform.localScale.x * .5f / depth);
                float x = Vector3.Dot(v, across) / depth, start = Vector3.Dot(v, up) / depth, y = Mathf.Min(start, cap);
                // a name lowered to just under another can land a rounding error short of it: the test leaves that slack, or
                // the same name would be lowered to the same place forever
                for (bool moved = true; moved;)
                {
                    moved = false;
                    foreach (var q in placed)
                        if (Mathf.Abs(x - q.x) < half.x + q.halfX && Mathf.Abs(y - q.y) < (half.y + q.halfY) * .999f) { y = q.y - half.y - q.halfY; moved = true; }
                }
                placed.Add((x, y, half.x, half.y));
                // down across the view's frame, at the same depth
                t.transform.localPosition = at + up * ((y - start) * depth);
                return y;
            }
            float highest = float.NegativeInfinity;
            foreach (var t in names) highest = Mathf.Max(highest, Place(t, float.PositiveInfinity));
            // the eight stand below the cloud's name, and so, no higher, does a category's name (with none of them, its own)
            foreach (var t in under) Place(t, float.IsNegativeInfinity(highest) ? float.PositiveInfinity : highest);
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        void Clear()
        {
            foreach (var o in owned) if (o) Destroy(o);
            if (content) Destroy(content);
            owned.Clear(); renderers.Clear(); labels.Clear(); content = null; title = null; raisedText = plateMaterial = null; plateQuad = null;
        }

        void OnDestroy() { Clear(); Placed.Clear(); People = null; HoverPerson = -1; }
    }
}

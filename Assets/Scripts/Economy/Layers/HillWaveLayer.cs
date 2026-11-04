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
    /// <para>An arrow lies along time at the crest's far end, pointing into the future whichever way the camera looks; a
    /// crest that stands back in time (view "y1972") is outlined by today's for scale.</para>
    /// <para>Data lines blend at intensity 1 at most, so their hues stay true where they cross and nothing blooms; the
    /// line under the pointer is the one highlight (<see cref="GraphStyle.HighlightGlow"/>).</para>
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillWaveLayer : GraphLayer
    {
        public override int Order => 54;
        public override IEnumerable<string> RequiredTexts => new[] { CompanyLives.LivesPath, CompanyLives.JudgmentsPath };
        /// <summary>The company line under the pointer and the one clicked (-1: none); indices into CompanyLives.Lives.</summary>
        public static int HoverCompany { get; private set; } = -1;
        static HillWaveLayer active;
        /// <summary>Where a company's lifeline stands at the crest (or ends), land-local: index into CompanyLives.Lives.</summary>
        public static bool CompanyAtCrest(int life, out Vector3 at)
        {
            at = default;
            if (active == null || !active.paths.TryGetValue(life, out var path) || path.Count == 0) return false;
            at = path[path.Count - 1].Data; return true;
        }
        /// <summary>Where a company's lifeline passes a (fractional) year, land-local: its point at that year's z; past the
        /// line's end (a survivor arrives at its tower a little behind the crest) the end carried forward to the year's z,
        /// never past the crest. False before the line begins or when it is not drawn.</summary>
        public static bool CompanyAtYear(int life, double year, out Vector3 at)
        {
            at = default;
            if (active == null || active.land == null || !active.paths.TryGetValue(life, out var path) || path.Count == 0) return false;
            float z = HillLandscape.Z(year);
            if (z < path[0].Data.z) return false;
            for (int k = 1; k < path.Count; k++)
            {
                Vector3 a = path[k - 1].Data, b = path[k].Data;
                if (z > b.z) continue;
                at = b.z - a.z > 1e-5f ? Vector3.Lerp(a, b, (z - a.z) / (b.z - a.z)) : b;
                return true;
            }
            Vector3 end = path[path.Count - 1].Data;
            at = new Vector3(end.x, end.y, Mathf.Min(z, active.land.CrestZ - .4f));
            return true;
        }
        public static int SelectedCompany { get; private set; } = -1;
        /// <summary>The company names shown now, each its land-local middle (where Tick last placed it) and half size in world
        /// units: the replay's tags keep clear of them (<see cref="HillReplayLayer"/>).</summary>
        public static void ShownNames(List<(Vector3 at, Vector2 half)> into)
        {
            if (active == null) return;
            foreach (var n in active.companyNames)
                if (n.Text && n.Text.gameObject.activeInHierarchy && n.Half != Vector2.zero) into.Add((n.Text.transform.localPosition, n.Half * n.Text.transform.localScale.x));
        }
        enum Part { Ribs, Ridges, Face, Crest, Spray, Lives, Focus, Fan, Future, Saving, Arrow, Ghost, Plate, Count }
        static readonly Color Steel = new Color(.46f, .58f, .72f), Ice = new Color(.62f, .84f, 1), White = new Color(1, .96f, .9f),
            People = new Color(.36f, .62f, 1);
        /// <summary>Label groups beside the companies' (an index into CompanyLives.Lives): the wave's structure (named in its
        /// own views, everywhere when "always"), the road's fan where it meets the wave (not in 1972's view, where the road
        /// runs on past the crest), the rivers' saving and debt, an earlier crest beside today's, the families' fan, the arrow
        /// of time.</summary>
        const int Structure = -1, Road = -2, Rivers = -3, Earlier = -4, Families = -5, TimeArrow = -6;
        /// <summary>How far a flow's pulse brightens a data line (1 = the shader's fourfold): a sheen that shows the
        /// direction and stays under the bloom, which starts above 1.</summary>
        const float Pulse = .2f;
        readonly List<(Renderer renderer, Part part)> renderers = new List<(Renderer, Part)>();
        readonly List<(TextMeshPro text, int company, bool always)> labels = new List<(TextMeshPro, int, bool)>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly List<(int company, Vector3 at)> samples = new List<(int, Vector3)>();
        readonly Dictionary<int, List<LinePoint>> paths = new Dictionary<int, List<LinePoint>>();
        /// <summary>The lifelines that end at a stored firm's tower (the firms the strategy view tells).</summary>
        readonly HashSet<int> towered = new HashSet<int>();
        string livesJson, judgmentsJson;
        LandSnapshot snapshot; HillLandscape land; CompanyLives lives; GameObject content;
        /// <summary>Today's land (<see cref="Latest"/>), kept from when it was on screen or built once, for the outline of
        /// today's crest over an earlier one.</summary>
        HillLandscape today;
        /// <summary>The crest is drawn beside today's (<see cref="BesideToday"/>): today's outline, the tiers' shares then and
        /// now, and the names read over both crests. Set by a rebuild's first half for both halves.</summary>
        bool besideToday;
        /// <summary>The labels read over the wave's lines (the names beside today's crest, the crest's own and the companies' in
        /// the views of the wave and the encounters', the wake's, the fan's): a copy of the font's material drawn after the hills
        /// (text keeps the font's queue, before every line and surface, elsewhere). The hover card has a copy of its own, drawn
        /// after these.</summary>
        Material raisedText;
        MeshRenderer focus; TextMeshPro focusLabel;
        /// <summary>The hover card's plate, drawn after the raised names and before the card's text, sized to the card's text
        /// whenever it changes (<see cref="DrawFocus"/>).</summary>
        Transform cardPlate;
        /// <summary>The hover card's box (its text and plate, measured again when its text changes), where it stands over its
        /// line's end before it steps clear of the crest's name, and whether it stood stepped up last frame (<see cref="LiftCard"/>).</summary>
        Placed card; Vector3 cardAt; bool cardLifted;
        /// <summary>The time arrow's name, and whether the hover card covered it when last placed: the name hides while the card
        /// is over it rather than read cut in half at the card plate's edge (<see cref="Tick"/>).</summary>
        Placed arrowName; bool cardOverArrow;
        /// <summary>The gap (the card's ems) a hover card stepped up clear of the crest's name leaves over the name's plate: half an
        /// em, 7 to 11 px, about the 8 px the label system keeps between its labels, so the name reads as a label of its own
        /// rather than as the card's last line.</summary>
        const float CardGap = .5f;
        /// <summary>The crest's name: raised on its plate in the views of the wave and the encounters' (<see cref="Plated"/>), at
        /// the font's queue elsewhere, under the view's own lines and cards (<see cref="Raise"/>); not shown in the regimes view,
        /// whose summits are named there, on the ladder, where the life's path ends through it, in the rivers view, where it stands
        /// in the trunks' stack of names, nor in the capture and attention views, whose wage lines, loop and golden halos run
        /// through it.</summary>
        TextMeshPro crestName;
        /// <summary>A label Tick moves clear of others on screen, or one they keep clear of: where it stands before it moves,
        /// its half size (local units, measured once it is shown), the margin around it that hides what is behind (its
        /// plate's), and where it was last moved (lines for the bedrock's, a place for a company's, <see cref="Shift"/>).</summary>
        sealed class Placed { public TextMeshPro Text; public Vector3 At; public Vector2 Half, Margin; public int Lift; }
        /// <summary>The companies' names in the order they were built (<see cref="StackNames"/>) and the labels drawn over them
        /// that they keep clear of (the crest's and the wake's on their plates), the bedrock's name and the raw materials' it steps
        /// clear of (<see cref="ClearBedrock"/>), and the boxes placed so far in a frame.</summary>
        readonly List<Placed> companyNames = new List<Placed>();
        Placed crestPlate, wakeName, bedrockName, rawName;
        readonly List<Rect> placed = new List<Rect>();
        /// <summary>The decades' years along the wake's edge: shown whole or not at all (<see cref="Tick"/>).</summary>
        readonly List<Placed> decades = new List<Placed>();
        /// <summary>Places a company's name may take on each side of its line's end (<see cref="Shift"/>), and the room (px) a
        /// moved label needs to step back toward its place (a name, the hover card, a decade's year the frame's edge hid), so
        /// a camera moving slowly past (an orbit, the replay following the crest) does not set it flickering.</summary>
        const int NameLift = 7; const float Spare = 3;
        /// <summary>How far over its line's end a company's name stands (world units, about a line).</summary>
        const float NameRise = 1.6f;
        /// <summary>A plate's margin around its label's text (local units): the label system's, .35 em sideways and .12 em
        /// above and below, TextMeshPro setting 2.4 local units per em at its font size 24.</summary>
        static readonly Vector2 PlateMargin = new Vector2(.35f, .12f) * 2.4f;
        /// <summary>How much of what is behind a plate it hides: the label system's .75 over the wave's lines; .92, as the time
        /// axis's beacon's, where a bloomed mark runs behind the text (the road's beam, the highlighted line under the hover
        /// card), whose quarter would still show bright.</summary>
        const float PlateAlpha = .75f, BrightPlateAlpha = .92f;
        /// <summary>The smallest em (px at 1080 lines) at which the road's fan is named: the wake's name reads at about 10 px
        /// from the overview, and the views that name the fan see it at 7 to 9 px, where the 'Now' beacon and the road's time
        /// post stand on it. The attention cloud's names outside its view keep to it too (HillSocialLayer.NameMinPx).</summary>
        internal const float RoadMinPx = 11;
        /// <summary>The view the names were last placed for and where the eye stood (Tick); no view after a rebuild.</summary>
        string stackedView; Vector3 stackedEye;
        /// <summary>How far a headset's eye may move before the names are placed again, as a share of the pose's distance
        /// (about a centimetre at arm's length, CameraRig.XrViewDistance): the head moves every frame, and they would hop as
        /// it sways. Turning it moves them all alike.</summary>
        const float XrSway = .01f;
        int focused = -2, programRevision = -1; Vector2 pressed; LandFrame frame;
        /// <summary>1 while a rebuild has drawn its first half and waits for a frame for its second (see <see cref="Begin"/>).</summary>
        int stage;
        /// <summary>The wave drawn for the previous land, on screen until the rebuild's second half swaps the new one in.</summary>
        GameObject retired;
        readonly List<UnityEngine.Object> retiredOwned = new List<UnityEngine.Object>();

        public override void Prepare(GraphContext ctx) { livesJson = ctx.Text(CompanyLives.LivesPath); judgmentsJson = ctx.Text(CompanyLives.JudgmentsPath); }
        public override void Upload(GraphContext ctx) { active = this; frame = EconomyStage.Land(); frame.Place(transform); }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (HillLandscapeLayer.Current == null || LandService.Current == null) return;
            int revision = PlayerMindProgram.Active?.Revision ?? -1;
            // 1972's view draws today's crest beside the one it shows: entered or left on the same year, the wave is rebuilt
            if (snapshot != LandService.Current || land != HillLandscapeLayer.Current || revision != programRevision || BesideToday(HillLandscapeLayer.Current) != besideToday)
            {
                if (!HillRebuilds.Take()) return;
                programRevision = revision; Begin();
            }
            // the second half of a rebuild takes the next free frame (the same one for a land published blocking)
            if (stage == 1)
            {
                if (!HillRebuilds.Take()) return;
                Finish();
            }
            if (content == null) return;
            string view = LandView.PresetId;
            bool hidden = view == "mind";
            content.SetActive(!hidden); if (hidden) { HoverCompany = -1; return; }
            foreach (var e in renderers) if (e.renderer) GraphMaterials.SetAlpha(e.renderer.sharedMaterial, Emphasis(e.part, view));
            Pick(rig.Cam);
            int f = HoverCompany >= 0 ? HoverCompany : SelectedCompany;
            // the spending view tells the stored firms at their towers: a company clicked in another view is not lit there (the
            // pointer only finds the stored firms' lines, see Pick)
            if (view == "strategy" && f >= 0 && !towered.Contains(f)) f = -1;
            if (f != focused) { focused = f; DrawFocus(); }
            bool companies = view == "companies" || view == "landscape" || view == "overview";
            // the structure is named in the views of the whole wave and in 1972's, where the crest stands back in time
            bool structure = companies || view == "foundation" || view == "y1972";
            foreach (var e in labels)
            {
                // the money chart stands in front of the wave: none of the wave's labels may read as the chart's; the road's fan
                // is named only where its name is large enough to read (RoadMinPx); the time arrow's name stays hidden while the
                // hover card covers it (the card, placed after the labels, hides it on the frame it comes over it)
                bool show = view != "money" && (e.company == Families ? view == "section" : view != "section" && (e.company == Rivers ? view == "rivers"
                    : e.company == Earlier ? view == "y1972" : e.company == Road ? (companies || view == "foundation") && EmPx(rig, e.text.transform) >= RoadMinPx
                    : e.company == TimeArrow ? Emphasis(Part.Arrow, view) > 0 && !cardOverArrow
                    : e.company < 0 ? e.always || structure : e.company == f || companies && e.always));
                // the regimes view names its own summits there (HillRegimeLayer): the crest's name would stand among their
                // rings and names; on the ladder the life's path and the hills' outlines run through it, and the walker's
                // caption carries the years; in the rivers view it stands between two trunks' names on their plates
                // (HillActivityLayer), as one more line of their stack; in the capture and attention views the wage lines, the
                // attention loop and the owners' golden halos cross its letters, and a plate would blank them. The year chip
                // carries the year there outside the tour, which hides it; in capture the caption of the dollar's split names it
                // too, and the tour shows both views at the default year (LandViewLayer.EnterTour), the year the attention stop's
                // captions give for their data.
                if (e.text == crestName && (view == HillRegimeLayer.View || view == "ladder" || view == "rivers" || view == "capture" || view == "social")) show = false;
                e.text.gameObject.SetActive(show);
                if (show) e.text.transform.rotation = rig.Cam.transform.rotation;
            }
            // a decade's year shows whole or not at all: cut by the frame's edge it reads as another number ("19" and "1" at the
            // capture view's right edge). As the names below, a flight leaves each year as it stands until it lands (a new wave
            // or view decides at once) and a hidden year needs Spare px of room to show again, so none blinks at the edge as the
            // camera moves past; in a headset, whose eye moves every frame, every year shows.
            bool years = view != "money" && view != "section", decide = view != stackedView || !rig.Flying; Rect screen = rig.Cam.pixelRect;
            foreach (var d in decades)
            {
                if (decide) d.Text.gameObject.SetActive(years && (rig.XrOrigin != null || Whole(rig, d, screen)));
                if (d.Text.gameObject.activeSelf) d.Text.transform.rotation = rig.Cam.transform.rotation;
            }
            // a new wave or view sets the names over or under the lines before they are placed
            if (view != stackedView) Raise(view);
            // a flight between poses (the tour's, a view's) leaves the names where they stand until it lands, so none hops on
            // the way as overlaps open and close; in a headset they move once the eye has (XrSway). A new wave or view places
            // them at once.
            Vector3 eye = rig.Cam.transform.position; float sway = XrSway * rig.Pose.Distance;
            if (view != stackedView || !rig.Flying && (rig.XrOrigin == null || (eye - stackedEye).sqrMagnitude > sway * sway))
            {
                StackNames(rig, Plated(view)); ClearBedrock(rig);
                stackedView = view; stackedEye = eye;
            }
            if (focusLabel)
            {
                // a company with no line in this wave (one clicked in another year's) has no card (DrawFocus); the spending view
                // has none: its panel and the name over the tower tell the focused firm, and a card over the tower's halo would
                // cover the firm's arrows and streams, which leave from there (HillStrategyLayer)
                bool shown = f >= 0 && view != "money" && view != "strategy" && focusLabel.text.Length > 0;
                focusLabel.gameObject.SetActive(shown);
                if (shown)
                {
                    focusLabel.transform.localPosition = cardAt; focusLabel.transform.rotation = rig.Cam.transform.rotation;
                    FitCard(rig); LiftCard(rig);
                }
                // the time arrow's name under the card's plate would read cut in half at its edge: it hides on the frame the card
                // comes over it, a new wave's first too (a new wave decides afresh, and the labels above have just shown it),
                // stays hidden while the card covers it and shows again the frame after it has Spare px of room; only in the
                // views that name the arrow
                cardOverArrow = shown && arrowName != null && Emphasis(Part.Arrow, view) > 0
                    && Grown(OnScreen(rig, card), cardOverArrow ? Spare : 0).Overlaps(OnScreen(rig, arrowName));
                if (cardOverArrow) arrowName.Text.gameObject.SetActive(false);
            }
        }

        /// <summary>Whether a decade's year stands whole inside the frame: a shown year until the frame's edge cuts it, a hidden
        /// one once it has Spare px of room (<see cref="Tick"/>); none behind the camera.</summary>
        static bool Whole(CameraRig rig, Placed d, Rect screen)
        {
            Rect box = OnScreen(rig, d), room = Grown(screen, d.Text.gameObject.activeSelf ? 0 : -Spare);
            return box.width > 0 && box.xMin >= room.xMin && box.xMax <= room.xMax && box.yMin >= room.yMin && box.yMax <= room.yMax;
        }

        /// <summary>Steps the hover card, where it stands over the crest's name and would cover it, up clear of the name's plate,
        /// leaving a gap (<see cref="CardGap"/>): every survivor's line ends at the crest, around its name. A card under the name
        /// stays over its line's end. A card stepped up stays up until its centre falls Spare px under the name's or it has Spare
        /// px of room, so an orbit or a zoom that brings the two level does not set it hopping a card's height and back.</summary>
        void LiftCard(CameraRig rig)
        {
            Transform t = focusLabel.transform; float ppu = rig.PixelsPerUnit(t.position);
            bool lift = false;
            if (crestPlate != null && crestName.gameObject.activeSelf && ppu > 0)
            {
                Rect box = OnScreen(rig, card), crest = OnScreen(rig, crestPlate);
                // the name's box and the gap over it (TextMeshPro sets 2.4 units per em at its font size 24)
                float mid = crest.center.y; crest.yMax += CardGap * 2.4f * t.lossyScale.x * ppu;
                lift = Grown(box, cardLifted ? Spare : 0).Overlaps(crest) && box.center.y > mid + (cardLifted ? -Spare : Spare);
                if (lift) t.position += rig.Cam.transform.up * (Mathf.Max(0, crest.yMax - box.yMin) / ppu);
            }
            cardLifted = lift;
        }

        /// <summary>
        /// Sets the crest's name and the companies' over or under the lines for a view (once a view, and for a new wave): raised in
        /// the views of the wave and the encounters' (<see cref="Plated"/>), the crest's on its plate, so a name the stack moves
        /// into the crest's lines and crowns (or the encounters' ties) reads over them (<see cref="StackNames"/> lowers one it finds
        /// no free place for); elsewhere at the font's queue, under the view's own lines and cards.
        /// </summary>
        void Raise(string view)
        {
            if (!raisedText) return;
            bool plated = Plated(view);
            if (crestName) Use(crestName, plated);
            foreach (var n in companyNames) Use(n.Text, plated);
        }

        /// <summary>Gives a label the raised material or its font's own.</summary>
        void Use(TextMeshPro t, bool raised)
        {
            Material m = raised ? raisedText : t.font.material;
            if (t.fontSharedMaterial != m) t.fontSharedMaterial = m;
        }

        /// <summary>A label's em on screen (px at 1080 lines): TextMeshPro sets 2.4 world units per em for its font size 24 at
        /// scale 1. The attention cloud's names are measured by it too (HillSocialLayer).</summary>
        internal static float EmPx(CameraRig rig, Transform t) => 2.4f * t.lossyScale.x * rig.PixelsPerUnit(t.position) * 1080f / Mathf.Max(1, rig.Cam.pixelHeight);

        /// <summary>The hover card's size on screen (px per em at 1080 lines): a card sized in the world is a few pixels tall
        /// from the overview and, from the close views, has letters 40 px tall across the legend.</summary>
        const float CardMinPx = 14, CardMaxPx = 22, CardSize = .3f;

        void FitCard(CameraRig rig)
        {
            // TextMeshPro sets 2.4 world units per em for its font size 24 at scale 1
            float ppu = rig.PixelsPerUnit(focusLabel.transform.position), em = 2.4f * CardSize * LabelScale * ppu * 1080f / Mathf.Max(1, rig.Cam.pixelHeight);
            float k = em > 1e-3f ? Mathf.Clamp(em, CardMinPx, CardMaxPx) / em : 1;
            focusLabel.transform.localScale = Vector3.one * CardSize * LabelScale * k;
        }

        float Emphasis(Part part, string view)
        {
            // Today's crest outlined over an earlier one belongs to 1972's view; the plates behind the names the lines cross to
            // the views of the wave and the encounters' (Plated); the arrow of time to the views of the whole wave (from the
            // foundation's low pose it falls in the corner under the title).
            if (part == Part.Ghost) return view == "y1972" ? .6f : 0;
            if (part == Part.Plate) return Plated(view) ? 1 : 0;
            if (part == Part.Arrow) return view == "overview" || view == "landscape" || view == "companies" || view == "y1972" ? .85f : 0;
            bool person = EconomyState.SelectedPlayer >= 0;
            if (PersonFocus.IsView(view))
            {
                // The ladder runs across the wake: its ribs and ridges are the ground the life climbs over.
                bool ladder = view == "ladder";
                switch (part)
                {
                    case Part.Ribs: return ladder ? .5f : .14f;
                    case Part.Ridges: return ladder ? .55f : .25f;
                    case Part.Face: return ladder ? .3f : .12f;
                    case Part.Crest: return ladder ? .6f : .22f;
                    case Part.Spray: return .04f;
                    case Part.Focus: return 1;
                    default: return .04f;
                }
            }
            if (view == "money")
            {
                // The chart stands in front of the wave on the same time axis: a bright crest would climb past the chart's 20%
                // rule like a fifth series, so only the ribs remain, as the ground of the years.
                switch (part)
                {
                    case Part.Ribs: return .1f;
                    case Part.Ridges: return .06f;
                    case Part.Face: return .05f;
                    case Part.Crest: return .06f;
                    default: return 0;
                }
            }
            if (EconomyViews.Focused(view))
            {
                // the wave is the faint ground of a focused view; the companies' spending view keeps their lifelines
                switch (part)
                {
                    case Part.Ribs: return .2f;
                    case Part.Ridges: return .28f;
                    case Part.Face: return .14f;
                    case Part.Crest: return .45f;
                    case Part.Spray: return .02f;
                    case Part.Lives: return view == "strategy" ? .4f : .05f;
                    case Part.Focus: return 1;
                    default: return .03f;
                }
            }
            switch (part)
            {
                case Part.Ribs: return view == "landscape" ? .85f : view == "overview" || view == "companies" ? .55f : view == "section" ? .3f : view == "society" || view == "betrayal" ? .08f : .22f;
                case Part.Ridges: return view == "landscape" ? 1 : view == "overview" ? .7f : view == "companies" ? .45f : view == "roots" ? .4f : .2f;
                // The rivers tell desire and fear: the gold face and the crest step back below them.
                case Part.Face: return view == "landscape" || view == "foundation" ? 1 : view == "overview" ? .8f : view == "society" || view == "betrayal" ? .15f : view == "rivers" ? .25f : .4f;
                case Part.Crest: return view == "society" || view == "betrayal" || view == "rivers" ? .3f : 1;
                // The front breaking forward is the wave's own image: full in its views, a trace where another story leads.
                case Part.Spray: return view == "overview" || view == "landscape" || view == "companies" ? .8f : view == "section" || view == "y1972" ? .5f : .12f;
                case Part.Lives: return (view == "companies" ? 1 : view == "overview" || view == "landscape" ? .75f : view == "capture" ? .3f : view == "roots" ? .25f : .1f) * (person ? .4f : 1);
                case Part.Focus: return 1;
                // The families' view: the fan is named there and steps back so the family links read.
                case Part.Fan: return view == "people" ? .7f : view == "section" ? .35f : view == "overview" ? .14f : .04f;
                // The rivers view is the households' spending split rose / ice: saving (gold) and the debts arriving from the
                // future (most households' mix of motives, which blends to near white) step back below it.
                case Part.Future: return view == "rivers" ? .3f : view == "people" ? .35f : view == "overview" ? .12f : .04f;
                case Part.Saving: return view == "rivers" ? .3f : view == "people" ? .35f : view == "overview" ? .12f : .04f;
            }
            return 1;
        }

        /// <summary>The views that look at the wave itself, 1972's beside today's crest, the households' on their hills, the
        /// bedrock's, whose arcs run up across the wake, and the encounters' (society, betrayal), whose ties run dense through
        /// the crest's name: there the names the lines and crowns cross stand on their plates, the crest's own raised over
        /// them.</summary>
        static bool Plated(string view) => view == "overview" || view == "landscape" || view == "companies" || view == "y1972" || view == "people" || view == "foundation"
            || view == "society" || view == "betrayal";

        // ------------------------------------------------------------------ build

        /// <summary>
        /// A rebuild's first half: the wake and the crest's face, into new content kept hidden while the wave drawn for the
        /// previous land stays on screen; <see cref="Finish"/> adds the lifelines in the next free frame and swaps them. Half
        /// a rebuild a frame keeps a new year's swap from freezing the screen (HillRebuilds).
        /// </summary>
        void Begin()
        {
            if (stage == 1) Clear();   // the land changed again before the second half
            else Retire();
            snapshot = LandService.Current; land = HillLandscapeLayer.Current; besideToday = BesideToday(land);
            CompanyLives.Provide(LandService.Model.Data, livesJson, judgmentsJson); lives = CompanyLives.Get();
            content = new GameObject("Economy wave"); content.transform.SetParent(transform, false); content.SetActive(false);
            Wake(); Face(); Arrow();
            stage = 1;
        }

        /// <summary>A rebuild's second half: the company lifelines, the fan, the future and an earlier crest beside today's,
        /// then the swap.</summary>
        void Finish()
        {
            Lifelines(); Fan(); Future(); ThenAndNow();
            focus = Lines(new LineMeshBuilder(), Part.Focus, true, true); focused = -2;
            // the hover card reads over everything on the wave, the raised names on their plates too: its own plate and text are
            // drawn after theirs, the plate wherever the card shows (Part.Focus) and dark enough for the highlighted line, which
            // runs under the card's middle from its end
            focusLabel = Label("", Vector3.zero, CardSize, White, true);
            if (raisedText) { var text = new Material(raisedText) { renderQueue = 3254 }; owned.Add(text); focusLabel.fontSharedMaterial = text; }
            cardPlate = Plate(focusLabel, BrightPlateAlpha, 3253, Part.Focus); card = new Placed { Text = focusLabel, Margin = PlateMargin };
            DropRetired();
            content.SetActive(true);
            stage = 0;
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
                ribs.AddPolyline(pts, WithAlpha(Steel, decade ? .55f : lustrum ? .26f : .10f), decade ? 1.3f : .8f, 0, 0, 1);
                if (decade) decades.Add(new Placed { Text = Label(y.ToString(LandFacts.Ci), new Vector3(HillLandscape.FootX - 9, 1.2f, z), .3f, Ice) });
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
                    line.Add(new LinePoint(new Vector3(x, land.RidgeTopAt(y, i) + .05f, z), c, 1.2f, 0, 1));
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
                    line.Add(new LinePoint(new Vector3(x, land.Strata(x, z) + .05f, z), WithAlpha(Steel, .6f), 1, 0, 1));
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
            var fill = new SurfaceMeshBuilder(); var lines = new LineMeshBuilder(); var crest = new LineMeshBuilder(); var spray = new LineMeshBuilder();
            int year = land.Year; float z = land.CrestZ + .05f, x0 = HillLandscape.FootX - 8, x1 = HillLandscape.FootX + land.CrestWidth + 18;
            Color[] tier = { EconomyStyle.Government, EconomyStyle.Matter, EconomyStyle.CapitalDim, Color.Lerp(EconomyStyle.CapitalDim, EconomyStyle.Capital, .5f), EconomyStyle.Capital };
            float[] alpha = { .07f, .07f, .06f, .07f, .09f };
            var low = new List<Vector3>(); var high = new List<Vector3>();
            for (float x = x0; x <= x1; x += 1) { low.Add(new Vector3(x, HillLandscape.Bedrock, z)); high.Add(new Vector3(x, 0, z)); }
            fill.AddBand(low, high, new[] { (Color32)WithAlpha(tier[0], alpha[0]) }, 0, 1);
            lines.AddPolyline(low, WithAlpha(Steel, .5f), 1, 0, 0, 1);
            for (int k = 1; k <= 4; k++)
            {
                low.Clear(); high.Clear(); var top = new List<Vector3>();
                for (float x = x0; x <= x1; x += 1)
                {
                    float a = land.LayerAt(year, k - 1, x), b = land.LayerAt(year, k, x);
                    low.Add(new Vector3(x, a, z)); high.Add(new Vector3(x, b, z)); top.Add(new Vector3(x, b + .02f, z));
                }
                fill.AddBand(low, high, new[] { (Color32)WithAlpha(tier[k], alpha[k]) }, 0, 1.2f);
                lines.AddPolyline(top, WithAlpha(Steel, .55f), 1, 0, 0, 1);
                Vector2 band = land.TierBand(year, k); float mid = (band.x + band.y) * .5f;
                string[] names = { "", "RAW MATERIALS", "MANUFACTURING · INFRASTRUCTURE", "SERVICES", "TECH" };
                Vector3 at = new Vector3(mid, (land.LayerAt(year, k - 1, mid) + land.LayerAt(year, k, mid)) * .5f, z + .3f);
                var tierName = Label(names[k], at, .5f, Color.Lerp(tier[k], White, .5f), besideToday);
                labels.Add((tierName, Structure, false));
                if (k == 1) rawName = new Placed { Text = tierName, At = at };
                Then(k, at, Color.Lerp(tier[k], White, .5f));
            }
            // beside today's crest the shares stand under the names: the bedrock's name steps down and up the hillside, clear
            // of the raw materials' share above it and, in 1972's pose, of the wake's "1970" at the right end of its row;
            // elsewhere Tick steps it clear of the raw materials' name where a pose prints one on the other
            Vector3 bedrock = new Vector3(HillLandscape.FootX + land.CrestWidth * (besideToday ? .25f : .22f), HillLandscape.Bedrock * (besideToday ? .7f : .5f), z + .3f);
            var government = Label("GOVERNMENT · the bedrock everything stands on", bedrock, .5f, Ice, besideToday);
            labels.Add((government, Structure, false));
            bedrockName = new Placed { Text = government, At = bedrock };
            Then(0, bedrock, Ice);
            // The ridges above the strata at the crest, then the crest line itself, bright: the wave's leading edge.
            var ridgeFill = new List<Vector3>(); var strata = new List<Vector3>(); var edge = new List<LinePoint>();
            for (float x = x0; x <= x1; x += .5f)
            {
                float s = land.LayerAt(year, 4, x), g = land.Ground(x, land.CrestZ);
                strata.Add(new Vector3(x, s, z)); ridgeFill.Add(new Vector3(x, g, z));
                edge.Add(new LinePoint(new Vector3(x, g + .05f, land.CrestZ), WithAlpha(White, .95f), 2.4f, 0, 1));
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
                    arc.Add(new LinePoint(p, WithAlpha(Color.Lerp(White, EconomyStyle.Capital, .3f), .55f * (1 - t)), 1.2f, 0, 1));
                }
                spray.AddFlowPath(arc, 0, .6f, x * .1f);
            }
            // the crest's name stands where the crowns, the companies' lines and the arcs run behind it (beside today's crest, its
            // own crest and today's outline; in the encounters', their ties): it reads on a plate in the views of the wave and the
            // encounters' (Plated, Tick)
            crestName = Label("<b>" + year + "</b> · the crest: that year's economy", new Vector3(HillLandscape.FootX + land.CrestWidth * .5f, land.Ground(HillLandscape.FootX + land.CrestWidth * .5f, land.CrestZ) + 12, land.CrestZ + 2), .36f, White, plate: true);
            labels.Add((crestName, Structure, true));
            // the wake's name reads over its ribs, about 10 px an em from the overview, on a plate where the crest's hillside, the
            // crowns, the companies' bundles and the bedrock's arcs run behind it
            var wake = Label("THE WAKE · the economy's past, one rib a year", new Vector3(HillLandscape.FootX + land.SliceWidth((land.FirstYear + land.Year) / 2) * .5f, land.Ground(HillLandscape.FootX + 30, HillLandscape.Z((land.FirstYear + land.Year) / 2)) + 16, HillLandscape.Z((land.FirstYear + land.Year) / 2)), .4f, Ice, plate: true);
            labels.Add((wake, Structure, false));
            // both are drawn over the companies' names, which keep clear of them and their plates (StackNames): every survivor's
            // line ends at the crest, around its name
            crestPlate = new Placed { Text = crestName, Margin = PlateMargin }; wakeName = new Placed { Text = wake, Margin = PlateMargin };
            Surface(fill, Part.Face);
            Lines(lines, Part.Face, false, false);
            var r = Lines(crest, Part.Crest, true, false); r.sharedMaterial.SetFloat("_FlowFreq", 1.5f); r.sharedMaterial.SetFloat("_FlowSpeed", .7f);
            r = Lines(spray, Part.Spray, true, false); r.sharedMaterial.SetFloat("_FlowFreq", 1.5f); r.sharedMaterial.SetFloat("_FlowSpeed", .7f);
        }

        /// <summary>The direction of time at the crest: an arrow laid along the wave's axis from the crest's far end out over
        /// the front, named at its middle. It stands in the world, so it points the way time runs from any pose (an arrow in
        /// text faces the camera and points screen-right: into the past, from every pose that looks back at the crest).</summary>
        void Arrow()
        {
            var b = new LineMeshBuilder(); Color c = WithAlpha(Ice, .8f);
            float x = HillLandscape.FootX + land.CrestWidth + 16, y = land.LayerAt(land.Year, 4, HillLandscape.FootX + land.CrestWidth * .9f) + 14, l = 2.2f;
            Vector3 tail = new Vector3(x, y, land.CrestZ + 1), head = new Vector3(x, y, land.CrestZ + 22);
            b.AddSegment(tail, head, c, 2, 0, 0, 1);
            // the head: one chevron across the ground and one upright, so it reads from above and from the side
            b.AddPolyline(new[] { head + new Vector3(-l * .6f, 0, -l), head, head + new Vector3(l * .6f, 0, -l) }, c, 2, 0, 0, 1);
            b.AddPolyline(new[] { head + new Vector3(0, -l * .6f, -l), head, head + new Vector3(0, l * .6f, -l) }, c, 2, 0, 0, 1);
            Lines(b, Part.Arrow, false, false);
            int latest = Latest;
            string after = land.Year >= latest ? "FUTURE · after " + land.Year + ", not yet made" : "LATER YEARS · " + (land.Year + 1) + " to " + latest;
            // beside today's crest, today's outline runs through the name: it stands on a plate. Tick hides it under the hover card.
            var label = Label(after, (tail + head) * .5f + Vector3.up * 5, .5f, c, plate: besideToday);
            labels.Add((label, TimeArrow, false)); arrowName = new Placed { Text = label, Margin = besideToday ? PlateMargin : Vector2.zero };
        }

        /// <summary>The present the wave is compared with: the year the land opens on (2025), which the views of the whole
        /// wave frame as now.</summary>
        static int Latest => EconomyState.DefaultYear(EconomyState.MaxYear);

        /// <summary>Whether a land's crest is drawn beside today's: an earlier year in 1972's view, the only view that shows the
        /// comparison. The years a replay or the scrubber passes through elsewhere build none of it.</summary>
        static bool BesideToday(HillLandscape l) => LandView.PresetId == "y1972" && l.Year < Latest;

        /// <summary>Under a tier's name on a crest that stands back in time (1972's view): the tier's share of all output then
        /// and today (the bedrock is tier 0). Sized for that view's pose (HillPresets, 190 units away): TECH's, the farthest,
        /// is 14 px an em at 1080 lines; a new pose needs them measured again.</summary>
        void Then(int tier, Vector3 at, Color color)
        {
            if (!besideToday) return;
            int latest = Latest; var data = LandService.Model.Data;
            // TECH's, the longest line at the narrowest tier, steps toward the camera so it ends before the TECH crowns
            labels.Add((Label(LandFacts.Percent(TierShare(data, tier, land.Year)) + " of all output · " + LandFacts.Percent(TierShare(data, tier, latest)) + " in " + latest,
                tier == 4 ? at + new Vector3(0, -2.1f, 6) : at + Vector3.down * 3.1f, .43f, color, true), Earlier, false));
        }

        /// <summary>A tier's share of all industries' value added in a year (BEA, industries.json).</summary>
        static double TierShare(Data.EconomyData data, int tier, int year)
        {
            double part = 0, all = 0;
            foreach (var ind in data.Industries) { double v = Math.Max(0, ind.ValueAdded.GrowthAt(year)); all += v; if (ind.TierIndex == tier) part += v; }
            return all > 0 ? part / all : 0;
        }

        /// <summary>All industries' value added in a year, in today's dollars.</summary>
        static double RealOutput(Data.EconomyData data, int year)
        {
            double sum = 0;
            foreach (var ind in data.Industries) sum += data.Real(Math.Max(0, ind.ValueAdded.GrowthAt(year)), year);
            return sum;
        }

        /// <summary>
        /// A crest that stands back in time beside today's (view "y1972"): today's crest outlined over it, at its place, so
        /// the two compare where they stand (today's own place in time is where the camera stands to look back at 1972),
        /// with how much more the economy makes after inflation; the road of lives runs on past the crest to now.
        /// </summary>
        void ThenAndNow()
        {
            int latest = Latest;
            if (land.Year == latest) today = land;
            if (!besideToday) return;
            // only the year's geometry is read: the players placed on it are the shown year's
            if (today == null || today.Year != latest) today = new HillLandscape(new LandSnapshot { Year = latest, Players = snapshot.Players });
            var b = new LineMeshBuilder(); var outline = new List<LinePoint>();
            // the label stands over the outline's middle tiers: clear of the year chip on the left and the arrow above the far end
            float z = land.CrestZ + .15f, x1 = HillLandscape.FootX + today.CrestWidth + 18, mid = HillLandscape.FootX + today.CrestWidth * .4f, top = 0;
            for (float x = HillLandscape.FootX - 8; x <= x1; x += .5f)
            {
                float g = today.Ground(x, today.CrestZ); if (Mathf.Abs(x - mid) < 25) top = Mathf.Max(top, g);
                outline.Add(new LinePoint(new Vector3(x, g + .05f, z), WithAlpha(White, .7f), 1.4f, 0, 1));
            }
            b.AddPolyline(outline, 0);
            Lines(b, Part.Ghost, false, false);
            var data = LandService.Model.Data;
            double growth = RealOutput(data, latest) / Math.Max(1e-9, RealOutput(data, land.Year));
            // the outline's peak runs through the caption, and the road's bright beam behind its label: both stand on plates, the
            // beam's as dark as the time axis's beacon's over the same beam
            labels.Add((Label("outline: the crest of <b>" + latest + "</b>, for scale\nthe economy makes " + growth.ToString("0.0", LandFacts.Ci) + " times as much as in " + land.Year + ", after inflation",
                new Vector3(mid, top + 9, z + 1), .34f, White, plate: true), Earlier, false));
            // a few years past the crest: the road nearer now runs under the camera that looks back at the crest
            Vector3 road = frame.Local(EconomyStage.OnRoad(Math.Min(land.Year + 6, latest), GraphStyle.HumansY, EconomyStyle.FramingRho));
            labels.Add((Label("the road of lives continues to now", road + Vector3.up * 4, .3f, People, plate: true, plateAlpha: BrightPlateAlpha), Earlier, false));
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
                if (life >= 0) { halo[life] = seat + Vector3.up * (2 + Mathf.Log10(1 + (float)Math.Max(0, t.MarketCap)) * 2.4f); towered.Add(life); }
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
                if (l.Founded >= land.FirstYear) Ring(b, new Vector3(first.x, land.Ground(first.x, first.z) + .05f, first.z), .5f, WithAlpha(c, .8f), 1.4f, 1);
                int end = l.Alive || l.Ended > land.Year ? land.Year : l.Ended;
                if (!l.Alive && l.Ended <= land.Year)
                {
                    if (l.Fate == "bankrupt") { b.AddSegment(last + new Vector3(-.6f, 0, -.6f), last + new Vector3(.6f, 0, .6f), WithAlpha(EconomyStyle.Defect, .9f), 2, 0, 0, 1); b.AddSegment(last + new Vector3(-.6f, 0, .6f), last + new Vector3(.6f, 0, -.6f), WithAlpha(EconomyStyle.Defect, .9f), 2, 0, 0, 1); }
                    else Ring(b, last, .45f, WithAlpha(c, .9f), 1.6f, 1);
                    string fate = l.Fate == "bankrupt" ? "failed " + l.Ended : l.Fate == "breakup" ? "broken up " + l.Ended
                        : (l.Fate == "acquired" ? "bought " : "merged ") + l.Ended + (l.Successor >= 0 ? " → " + lives.Lives[l.Successor].Name : l.SuccessorName != null ? " → " + l.SuccessorName : "");
                    Name(l.Name + " · " + fate, last, Color.Lerp(c, White, .4f), i, true);
                }
                else ranked.Add((l.ValueAt(end), i));
            }
            // Survivors are named at the crest when large; every line is named on hover.
            ranked.Sort((a, c) => c.value.CompareTo(a.value));
            for (int n = 0; n < ranked.Count; n++)
            {
                int i = ranked[n].life; var path = paths[i]; Vector3 last = path[path.Count - 1].Data;
                if (!halo.ContainsKey(i)) Name(lives.Lives[i].Name, last, Color.Lerp(path[path.Count - 1].Color, White, .4f), i, n < 14);
            }
            Lines(b, Part.Lives, true, false).sharedMaterial.SetFloat("_FlowSpeed", .25f);
        }

        /// <summary>A company's name over the end of its line, shown on hover (and in the views of the whole wave when
        /// <paramref name="always"/>), moved clear of the labels before it on screen (<see cref="StackNames"/>).</summary>
        void Name(string text, Vector3 end, Color color, int life, bool always)
        {
            Vector3 at = end + Vector3.up * NameRise;
            var t = Label(text, at, .3f, color);
            labels.Add((t, life, always)); companyNames.Add(new Placed { Text = t, At = at });
        }

        /// <summary>
        /// Moves each company name shown, in the order they were built, to the first of its places (<see cref="Shift"/>) where
        /// its box on screen meets neither the labels drawn over it (the crest's and the wake's on their plates) nor the names
        /// placed before it; to the last place when none is free. Companies that ended in the same years on neighbouring ridges
        /// end at one point (Lehman Brothers and Washington Mutual, Bear Stearns and Countrywide in 2008), every survivor ends
        /// at the crest, around its name, and which way their names must part depends on the pose. A name stands
        /// <paramref name="raised"/> over the lines (<see cref="Raise"/>) once it has a free place; one that found none stays at
        /// the font's queue, under the plates it meets, rather than print over their text.
        /// </summary>
        void StackNames(CameraRig rig, bool raised)
        {
            placed.Clear();
            Keep(rig, crestPlate); Keep(rig, wakeName);
            foreach (var n in companyNames)
            {
                if (!n.Text.gameObject.activeSelf) continue;
                Transform t = n.Text.transform; t.localPosition = n.At;
                Rect box = OnScreen(rig, n);
                // a line: the text's own height
                float line = 2 * n.Half.y * t.localScale.y;
                int j = 0;
                while (j < 2 * NameLift && Meets(j < n.Lift ? Grown(box, Spare) : box, placed))
                {
                    j++;
                    t.localPosition = n.At + Vector3.up * Shift(j, line);
                    box = OnScreen(rig, n);
                }
                if (raisedText) Use(n.Text, raised && (j < 2 * NameLift || !Meets(box, placed)));
                n.Lift = j;
                placed.Add(box);
            }
        }

        /// <summary>
        /// How far a company name's place <paramref name="j"/> stands from its own (world units, <paramref name="line"/> being
        /// its text's height): by turns under its line's end, as far under as its own stands over it, and a line up, each pair
        /// a line further out. Each name takes the nearest free place, over the end before under it; a whole line down would
        /// centre it on the end, its line's last stretch and mark (× a failure, ○ a takeover) drawn through its letters.
        /// </summary>
        static float Shift(int j, float line) => j % 2 == 1 ? -2 * NameRise - (j - 1) / 2 * line : j / 2 * line;

        /// <summary>Takes a label's box on screen, while it is shown, before the names are placed: they keep clear of it.</summary>
        void Keep(CameraRig rig, Placed p)
        {
            if (p != null && p.Text.gameObject.activeSelf) placed.Add(OnScreen(rig, p));
        }

        /// <summary>Steps the bedrock's name (whole lines, at most three) off the raw materials' name, on the side of it where
        /// it stands on screen: the companies view and the tour's last pose print one on the other, and no one place clears it
        /// in every pose. Beside today's crest both keep the places 1972's view was measured for (<see cref="Face"/>).</summary>
        void ClearBedrock(CameraRig rig)
        {
            if (bedrockName == null || rawName == null || besideToday || !bedrockName.Text.gameObject.activeSelf || !rawName.Text.gameObject.activeSelf) return;
            Transform t = bedrockName.Text.transform; t.localPosition = bedrockName.At;
            Rect raw = OnScreen(rig, rawName), box = OnScreen(rig, bedrockName);
            float line = 2 * bedrockName.Half.y * t.localScale.y * (box.center.y > raw.center.y ? 1 : -1);
            int k = 0;
            while (k < 3 && (k < bedrockName.Lift ? Grown(box, Spare) : box).Overlaps(raw))
            {
                k++;
                t.localPosition = bedrockName.At + Vector3.up * (line * k);
                box = OnScreen(rig, bedrockName);
            }
            bedrockName.Lift = k;
        }

        static Rect Grown(Rect r, float px) => new Rect(r.x - px, r.y - px, r.width + 2 * px, r.height + 2 * px);

        /// <summary>A label's box on screen (pixels): its text's half size and its margin about its place (the text is centred
        /// there), at its scale and the camera's pixels per unit there; empty behind the camera.</summary>
        static Rect OnScreen(CameraRig rig, Placed p)
        {
            if (p.Half == Vector2.zero) p.Half = p.Text.GetPreferredValues(p.Text.text) * .5f;
            Transform t = p.Text.transform; Vector3 at = t.position, s = rig.Cam.WorldToScreenPoint(at);
            if (s.z <= 0) return Rect.zero;
            Vector2 half = (p.Half + p.Margin) * (t.lossyScale.x * rig.PixelsPerUnit(at));
            return new Rect(s.x - half.x, s.y - half.y, 2 * half.x, 2 * half.y);
        }

        static bool Meets(Rect box, List<Rect> boxes)
        {
            foreach (var b in boxes) if (box.Overlaps(b)) return true;
            return false;
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
            // captive customers brighten a line a little, never past 1, where the bloom would wash its hue to white
            float glow = .8f + .2f * (l.Judged ? Mathf.Clamp01(l.LockedIn) : 0);
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
            // a company with no line in this wave (one clicked in another year's) has no card
            string text = "";
            if (focused >= 0 && paths.TryGetValue(focused, out var path))
            {
                var bright = new List<LinePoint>(path.Count);
                foreach (var p in path) bright.Add(new LinePoint(p.Data + Vector3.up * .02f, WithAlpha(p.Color, 1), p.WidthPx + 2.2f, 0, GraphStyle.HighlightGlow));
                b.AddFlowPath(bright, 0, .25f);
                var l = lives.Lives[focused]; Vector3 at = path[path.Count - 1].Data;
                double peak = l.Peak(out int peakYear);
                string life = l.Founded + "–" + (l.Alive ? "" : l.Ended.ToString(LandFacts.Ci)) + (l.Alive ? " · alive" : " · " + l.Fate);
                text = "<b>" + l.Name + "</b>  " + life + (l.Event != null ? " (" + l.Event + ")" : "") +
                    "\n" + LandService.Model.Data.Industries[l.Industry].Name + " · " + l.Sells +
                    "\npeak " + LandFacts.Money(peak) + " in " + peakYear + " (2025 $, approximate" + (l.Private ? ", private valuation" : "") + ")";
                if (l.Judged) text += "\nhow it sells (Jev): manufactured want " + LandFacts.Percent(l.Manufactured) + " · fear " + LandFacts.Percent(l.FearSold) +
                    " · captive " + LandFacts.Percent(l.Captive) + " · habit " + LandFacts.Percent(l.Habit);
                cardAt = at + Vector3.up * 3.2f;
            }
            // a new card (another company's, or a new wave's) decides afresh whether it steps clear of the crest's name
            focusLabel.text = text; cardLifted = false;
            if (text.Length > 0) { cardPlate.localScale = PlateSize(focusLabel); card.Half = Vector2.zero; }
            Replace(focus, b);
        }

        /// <summary>Threads from the road (the population's lifelines at the crest year) out to every cohort on the wave.</summary>
        void Fan()
        {
            var b = new LineMeshBuilder();
            Vector3 road = frame.Local(EconomyStage.OnRoad(land.Year, GraphStyle.HumansY, EconomyStyle.FramingRho)), toward = Vector3.zero; int n = 0;
            for (int i = 0; i < land.People.Length; i++)
            {
                if (land.Cloud[i]) continue;
                Vector3 to = land.People[i] + Vector3.up * .3f; var pts = new LinePoint[25]; toward += to; n++;
                for (int s = 0; s < pts.Length; s++)
                {
                    float t = s / 24f; Vector3 p = Vector3.Lerp(road, to, t); p.y = Mathf.Lerp(road.y, to.y, t * t) + Mathf.Sin(t * Mathf.PI) * 2;
                    pts[s] = new LinePoint(p, WithAlpha(People, .5f), .7f, 0, 1);
                }
                b.AddFlowPath(pts, 0, .4f, i * .21f);
            }
            // the fan's name reads over the crest, the threads and the road's ribbons that run behind it, on a plate; it is shown
            // only where it is large enough to read (Tick)
            labels.Add((Label("the road of lives fans out into the economy", road + new Vector3(4, 4, -2), .18f, People, plate: true), Road, false));
            // The families' view looks along the fan from a few dozen units: its own smaller label, a little way out along it.
            if (n > 0) labels.Add((Label("the road of lives fans out into the economy", Vector3.Lerp(road, toward / n, .15f) + Vector3.up * 2.5f, .1f, People, true), Families, false));
            Lines(b, Part.Fan, true, false);
        }

        /// <summary>Money along time: each cohort's saving leaves over the crest into the future as gold (capital that
        /// builds what comes next), and its debt arrives from the future as a desire / fear stream (spending drawn from
        /// income not yet earned), colored by the cohort's motive. Widths follow the square root of dollars; the
        /// scenario's balances are used while one runs.</summary>
        void Future()
        {
            var b = new LineMeshBuilder(); var gold = new LineMeshBuilder(); var players = snapshot.Players.Players; var program = PlayerMindProgram.Active;
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
                    Stream(gold, home, lip, ahead, WithAlpha(EconomyStyle.Capital, .8f), .6f + Mathf.Sqrt((float)saving) * .35f, false);
                }
                if (debt > .05)
                {
                    owed += debt;
                    Color motive = EconomyStyle.Motive(fear);
                    Stream(b, ahead + new Vector3(1.5f, 2, 0), lip + new Vector3(1.5f, 0, 0), home + new Vector3(.4f, 0, 0), WithAlpha(motive, .75f), .5f + Mathf.Sqrt((float)debt) * .06f, true);
                }
            }
            // no arrows in the text: it faces the camera, which looks back at the crest from the future
            labels.Add((Label("SAVING · capital sent into the future: " + LandFacts.Money(saved) + " a year\nDEBT · spending drawn from future income: " + LandFacts.Money(owed) + " owed",
                new Vector3(HillLandscape.FootX + land.CrestWidth * .5f, land.Ground(HillLandscape.FootX + land.CrestWidth * .5f, land.CrestZ) + 32, land.CrestZ - 6), .4f, White), Rivers, false));
            Lines(b, Part.Future, true, false).sharedMaterial.SetFloat("_FlowSpeed", .5f);
            Lines(gold, Part.Saving, true, false).sharedMaterial.SetFloat("_FlowSpeed", .5f);
        }

        static void Stream(LineMeshBuilder b, Vector3 a, Vector3 mid, Vector3 z, Color color, float width, bool inward)
        {
            var pts = new LinePoint[21];
            for (int s = 0; s < pts.Length; s++)
            {
                float t = s / 20f; Vector3 p = Vector3.Lerp(Vector3.Lerp(a, mid, t), Vector3.Lerp(mid, z, t), t);
                pts[s] = new LinePoint(p, WithAlpha(color, color.a * (inward ? Mathf.Lerp(.25f, 1, t) : Mathf.Lerp(1, .25f, t))), width, 0, 1);
            }
            b.AddFlowPath(pts, 0, .6f);
        }

        // ------------------------------------------------------------------ picking

        void Pick(Camera camera)
        {
            HoverCompany = -1;
            if (!PointerInput.Available || UI.HillExplorer.Depth >= 3 || EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;
            if (Emphasis(Part.Lives, LandView.PresetId) < .2f && SelectedCompany < 0) return;
            // the spending view tells the stored firms (at their towers): the pointer finds only their lines there, each a firm the
            // view can draw in full (HillStrategyLayer.Focus)
            bool strategy = LandView.PresetId == "strategy";
            Vector2 pointer = PointerInput.Position; float best = 12 * camera.pixelHeight / 1080f;
            foreach (var s in samples)
            {
                if (strategy && !towered.Contains(s.company)) continue;
                Vector3 screen = camera.WorldToScreenPoint(frame.World(s.at));
                if (screen.z <= 0) continue;
                float d = Vector2.Distance(pointer, screen); if (d < best) { best = d; HoverCompany = s.company; }
            }
            if (PointerInput.PressedThisFrame) pressed = pointer;
            if (PointerInput.ReleasedThisFrame && Vector2.Distance(pressed, pointer) < 5 && HillLandscapeLayer.HoverPlayer < 0)
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

        /// <summary>A mesh of lines. Data lines blend over what is behind them (where many meet, the top one's hue shows instead
        /// of a sum turning white); additive lines are the highlight, with the full pulse.</summary>
        MeshRenderer Lines(LineMeshBuilder b, Part part, bool flow, bool additive)
        {
            Mesh mesh = b.ToMesh("Wave " + part); owned.Add(mesh);
            Material material = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3204, additive, 0, flow ? additive ? 1 : Pulse : 0)); owned.Add(material);
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

        /// <summary>A label facing the camera. One read <paramref name="over"/> the crest's lines is drawn after the hills, and
        /// one on a <paramref name="plate"/> (hiding <paramref name="plateAlpha"/> of what is behind it) also hides the lines
        /// behind it.</summary>
        TextMeshPro Label(string text, Vector3 at, float size, Color color, bool over = false, bool plate = false, float plateAlpha = PlateAlpha)
        {
            // a raised label is made under the layer (active), so the text wakes with the font's material to raise and can be
            // measured for its plate, then joins the hidden content
            over |= plate;
            var go = new GameObject("Wave label"); go.transform.SetParent(over ? transform : content.transform, false); go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>(); t.text = text; t.fontSize = 24; t.alignment = TextAlignmentOptions.Center;
            t.color = color; t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.sizeDelta = new Vector2(60, 8);
            if (over)
            {
                if (raisedText == null && t.fontSharedMaterial != null) { raisedText = new Material(t.fontSharedMaterial) { renderQueue = 3252 }; owned.Add(raisedText); }
                if (raisedText != null) t.fontSharedMaterial = raisedText;
                if (plate) Plate(t, plateAlpha);
                go.transform.SetParent(content.transform, false);
            }
            go.transform.localScale = Vector3.one * size * LabelScale; return t;
        }

        /// <summary>A dark plate behind a label (a unit square scaled by <see cref="PlateSize"/>; it turns, scales and hides with
        /// the label), hiding <paramref name="alpha"/> of what is behind it: drawn after the hills and before the label's raised
        /// text (<paramref name="queue"/>), shown as its <paramref name="part"/> is (a name's in the views of the wave and the
        /// encounters', <see cref="Plated"/>; the hover card's wherever the card is). A label whose text changes sizes its plate
        /// again when it does (the hover card's, <see cref="DrawFocus"/>).</summary>
        Transform Plate(TextMeshPro t, float alpha, int queue = 3251, Part part = Part.Plate)
        {
            var b = new SurfaceMeshBuilder();
            b.AddBand(new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0) }, new[] { new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0) },
                new[] { (Color32)WithAlpha(GraphStyle.Background, alpha) }, 0, 1);
            Mesh mesh = b.ToMesh("Wave plate"); owned.Add(mesh);
            Material material = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1, queue)); owned.Add(material);
            material.SetFloat("_EdgeSoft", 0);
            var r = AddMesh("Wave plate", mesh, material); r.transform.SetParent(t.transform, false); r.transform.localScale = PlateSize(t);
            renderers.Add((r, part)); return r.transform;
        }

        /// <summary>A label's plate in the label's own units: its text with the label system's margin (<see cref="PlateMargin"/>)
        /// on every side.</summary>
        static Vector3 PlateSize(TextMeshPro t)
        {
            Vector2 s = t.GetPreferredValues(t.text) + 2 * PlateMargin;
            return new Vector3(s.x, s.y, 1);
        }

        /// <summary>The wave is seen from a few hundred units: its labels are drawn larger than the hills' close-up ones.</summary>
        const float LabelScale = 2.2f;

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        void Clear()
        {
            foreach (var o in owned) if (o) Destroy(o);
            if (content) Destroy(content);
            owned.Clear(); renderers.Clear(); labels.Clear(); samples.Clear(); paths.Clear(); towered.Clear(); content = null; focus = null; focusLabel = null; raisedText = null;
            companyNames.Clear(); decades.Clear(); crestName = null; cardPlate = null; crestPlate = wakeName = bedrockName = rawName = arrowName = card = null; stackedView = null;
            cardOverArrow = false;
        }

        /// <summary>Sets the wave on screen aside (still drawn) for a rebuild: it goes when the new one is complete.</summary>
        void Retire()
        {
            DropRetired();
            retired = content; retiredOwned.AddRange(owned);
            owned.Clear(); renderers.Clear(); labels.Clear(); samples.Clear(); paths.Clear(); towered.Clear(); content = null; focus = null; focusLabel = null; raisedText = null;
            companyNames.Clear(); decades.Clear(); crestName = null; cardPlate = null; crestPlate = wakeName = bedrockName = rawName = arrowName = card = null; stackedView = null;
            cardOverArrow = false;
        }

        void DropRetired()
        {
            foreach (var o in retiredOwned) if (o) Destroy(o);
            if (retired) Destroy(retired);
            retiredOwned.Clear(); retired = null;
        }

        void OnDestroy() { Clear(); DropRetired(); HoverCompany = SelectedCompany = -1; if (active == this) active = null; }
    }
}

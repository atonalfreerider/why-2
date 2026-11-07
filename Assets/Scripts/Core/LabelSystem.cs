using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Why
{
    /// <summary>
    /// A label's size role (spec 3.5, S04), for the labels of the hills and the new views: the height at 1080p and the
    /// placement priority come from it (<see cref="LabelSystem.RolePx"/>, <see cref="LabelSystem.RolePriority"/>). A role
    /// label is placed and drawn as S04 asks: its height clamped to 12-22 px (and at least 1 degree of the view in a headset,
    /// a title 1.2), four candidate places with a leader before it is dropped, clear of the HUD's blocks, LDR, with a dark
    /// halo. <see cref="None"/> is every label made before S04: sized, placed and drawn as it always was.
    /// </summary>
    public enum LabelRole
    {
        /// <summary>Not a role label: its own <see cref="LabelSpec.SizePx"/> and <see cref="LabelSpec.Priority"/>, as before S04.</summary>
        None,

        /// <summary>A view's or a structure's title: 18 px at 1080p, priority 10, 1.2 degrees in a headset.</summary>
        Title,

        /// <summary>A name (a floor, an economy, a flow): 15 px, priority 8.</summary>
        Name,

        /// <summary>A number: 13 px, priority 6.</summary>
        Value,

        /// <summary>A note (a source, a date): 12 px, priority 4.</summary>
        Note
    }

    /// <summary>A text label pinned to a data-space point, optionally with an icon left of the text.</summary>
    public sealed class LabelSpec
    {
        public string Text;               // TextMeshPro rich text allowed; may be empty when Icon is set
        public Vector3 Data;              // (u, y, rho)
        public float Priority = 1;        // higher wins when labels collide
        public float Rank;                // breaks ties between equal priorities (higher first), e.g. a figure's prominence
        public float SizePx = 13;         // on-screen text height (a role label's is clamped to 12-22 px, LabelSystem.RolePx by default)
        public Color Color = GraphStyle.Text;
        public Vector2 PixelOffset;       // screen-space offset from the point
        public TextAlignmentOptions Align = TextAlignmentOptions.Left;
        public string AnchorKey;          // tooltip/blurb source, optional
        public IdRange Ids = IdRange.Empty; // label brightens when these ids are highlighted
        public bool Hidden;               // toggled by layers for level of detail
        public bool HandoffFade;          // life/matter labels dissolve before the human branch
        public string Icon;               // optional icon id (see Icons), drawn left of the text; Text may then be empty
        public bool Fixed;                // Data is a world position (a diagram beside the timeline), not data space
        public float FixedRange;          // Fixed labels hide while the camera target is farther than this (world units; 0 = never)
        public Color Plate;               // a plate drawn behind the text and icon (alpha 0 = none), for labels over bright marks
        public Vector2[] Fallbacks;       // further screen-space offsets, tried in order when PixelOffset's place is taken or off screen
        public bool Undimmed;             // keeps its brightness while the tour highlights something else (a note in a mass's bloom)
        public LabelRole Role;            // a role label (spec 3.5): sized, placed and drawn as S04 asks (see LabelRole); None keeps the old ways
        public string Part;               // emphasis key ("capture.owners"): alpha from LabelSystem.PartAlpha, hidden below 0.3
        public Action OnClick;            // a click or pinch on the label runs this (main thread)
        public bool Candidates;           // tries the four candidate places, with a leader, before it is dropped (role labels always do)
        public bool OnTop;                // drawn over the scene's depth (ZTest Always), for a view whose marks write depth; off: as before

        internal float Width;             // estimated width in px at SizePx, icon included
        internal int TextVersion;         // bumped by LabelSystem.SetText so a shown label re-reads its text
        internal float IconEm;            // icon quad size in em, 0 without a known icon (resolved when the label system takes the spec)
        internal float WidthEm;           // Width in em (EstimateEm with the icon and the plate's margins)
        internal float EstimateEm;        // the text's share of WidthEm, as EstimateWidth estimates it
        internal float TextEm = -1;       // the text's width in em as TextMeshPro sets it, for MeasuredVersion (-1: not measured)
        internal float TextLinesEm;       // the text's height in em as TextMeshPro sets it (its lines)
        internal int MeasuredVersion = -1;
        internal object Owner;            // the group that holds it (LabelSystem.Replace); null when added for good (Add)
        internal bool Removed;            // its group was replaced or cleared
        internal int PartIndex = -1;      // its part's row in the label system's part table
        internal float Order;             // placement priority: Priority, and a role label's subject bonus
    }

    /// <summary>
    /// Places labels every frame the view changes: projects all specs, then greedily keeps the
    /// highest-priority labels that do not overlap on screen. Zooming in spreads points apart, so more
    /// labels fit - level of detail for free. Labels are billboards with constant pixel size. A label's
    /// text stands wholly on screen and its letters clear of the UI blocks that keep labels out
    /// (<see cref="KeepClear"/>; it may stand right against one), at its offset or at the first of its
    /// fallbacks that has room, or the label is not shown: one cut by the frame's edge or half under a
    /// panel reads as a defect.
    /// </summary>
    /// <remarks>
    /// A label with an icon shows it left of the text, as a sprite child of the text so it shares the
    /// billboard transform (one local unit = one em = SizePx on screen) and fades, highlights and hovers
    /// with it. The alignment places the whole group: Left starts at the anchor with the icon, Right ends at
    /// the anchor with the text, Center centres icon and text together. An icon-only label (empty Text) is a
    /// marker read on its own, so its icon is drawn larger.
    ///
    /// The hills and the new views label through the same system (spec 3.5, S04; <c>Economy.HillLabels</c> is their
    /// adapter): owner groups that a layer replaces whole every year (<see cref="Replace"/>, <see cref="Clear"/>), labels bound
    /// to an emphasis part (<see cref="LabelSpec.Part"/>, <see cref="PartAlpha"/>), and role labels
    /// (<see cref="LabelSpec.Role"/>): clamped sizes and degrees in a headset, four candidate places with a 1 px leader,
    /// the HUD's blocks kept clear (<see cref="HudRects"/>), LDR colours with a TextMeshPro underlay halo, drawn by the base
    /// camera like every label (no overlay camera, no second canvas). A label with <see cref="LabelSpec.OnClick"/> runs it on a
    /// click or a pinch. A label that is not a role label is placed and drawn as before, except that where the estimate of
    /// its width (<see cref="EstimateWidth"/>, about 0.2 em wide) would drop it at the frame's edge or at a block, its text is
    /// measured instead; and a block that moves labels aside (<see cref="MoveAside"/>) sends it to the candidate places too.
    /// </remarks>
    public sealed class LabelSystem : MonoBehaviour
    {
        public int MaxVisible = 110;
        public float Padding = 4;

        /// <summary>Icon quad size in em beside text; the glyph fills it minus the atlas margin.</summary>
        const float IconEm = 1.45f;

        /// <summary>
        /// Icon quad size in em of an icon-only label: with no text to help, the glyph needs more pixels to be
        /// told apart (about 18 px instead of 15 at a 12 px label on a 1080p screen).
        /// </summary>
        const float IconOnlyEm = 1.8f;

        /// <summary>Fraction of the icon quad the glyph fills: the quad without its transparent margin.</summary>
        const float IconGlyphFraction = 1 - 2 * IconRaster.Margin;

        /// <summary>Gap between the icon glyph and the text, in em.</summary>
        const float IconGapEm = 0.3f;

        /// <summary>Label line height in em, for the collision rect.</summary>
        const float LineEm = 1.15f;

        /// <summary>Width of the text rect in em (text never wraps; the rect only carries the alignment).</summary>
        const float TextRectEm = 20;

        /// <summary>Size of superscript and subscript characters relative to the text, for width estimates.</summary>
        const float ScriptScale = 0.6f;

        /// <summary>Sorting order of labels and their icons: above the graph.</summary>
        const int SortingOrder = 10;

        /// <summary>A plate's margin around its text, in em (sideways, and above and below).</summary>
        const float PlateMarginEm = 0.35f, PlateMarginYEm = 0.12f;

        /// <summary>
        /// Half the height of capitals and digits about a label's centre, in em (the font centres its line between ascent
        /// and descent, 0.35 em over the baseline, and its cap line is at 0.69 em): the box a block keeps out.
        /// </summary>
        const float CapEm = 0.35f;

        /// <summary>
        /// How far a label's letters may reach under a block (px at 1080p): their antialiased edge and the width estimate's
        /// error. A letter cut deeper reads clipped; descenders may dip under a panel's top.
        /// </summary>
        const float BlockSlackPx = 2f;

        /// <summary>
        /// A block this close to a screen edge (px at 1080p; the HUD's margin is 24, about 35 in portrait) also keeps labels
        /// out of the strip between them: a label squeezed in under the preset bar reads as part of the HUD.
        /// </summary>
        const float EdgeStripPx = 40f;

        // ------------------------------------------------------------------ role labels (spec 3.5)

        /// <summary>A role label's height at 1080p is clamped to these (px; spec 3.5): the smallest that reads, the largest that fits.</summary>
        public const float MinRolePx = 12, MaxRolePx = 22;

        /// <summary>The least of the view a role label's em takes in a headset (degrees; spec 3.5): 1, and 1.2 for a title.</summary>
        public const float RoleDegrees = 1f, TitleDegrees = 1.2f;

        /// <summary>A label bound to a part (<see cref="LabelSpec.Part"/>) is hidden while the part's alpha is below this (spec 3.5).</summary>
        public const float PartHiddenBelow = 0.3f;

        /// <summary>
        /// A role label's part at this alpha or more is a subject of the view (spec 3.1: subject 1, support 0.45, context
        /// 0.12; this is between subject and support), and its labels gain <see cref="SubjectBonus"/> priority (spec 3.5).
        /// </summary>
        public const float SubjectAlpha = 0.7f, SubjectBonus = 3;

        /// <summary>The most role labels placed at once (spec 3.5): on a screen, and in a headset.</summary>
        public const int RoleBudget = 140, RoleBudgetXr = 60;

        /// <summary>
        /// How far each of a label's four candidate places stands from its own place (em; spec 3.5), tried in this order:
        /// right, left, above, below.
        /// </summary>
        public const float CandidateEm = 1.2f;

        static readonly Vector2[] CandidateDirections = { Vector2.right, Vector2.left, Vector2.up, Vector2.down };

        /// <summary>A label moved further than this from its own place (px at 1080p) draws a leader back to its point (spec 3.5).</summary>
        const float LeaderMinPx = 8f;

        /// <summary>A leader's width, and the gap it leaves at its point and at the label (px at 1080p).</summary>
        const float LeaderPx = 1f, LeaderGapPx = 2f;

        /// <summary>
        /// The role labels' halo (spec 3.5): a TextMeshPro underlay, black at alpha 0.7, under the text in the same draw. Its
        /// dilation is as wide as the font's distance field allows: the field spans its gradient scale (10 texels of the
        /// 86-point atlas), 5 texels each side of a glyph's edge, and TextMeshPro pads each glyph's quad by 7.3 texels per
        /// unit of underlay dilation plus 1.25 (ShaderUtilities.GetPadding), so at 0.5 the quad stays inside the field and no
        /// box shows round the glyphs. The halo's half-alpha edge then stands 1.8 texels off the letters' (0.26 px at 12 px,
        /// 0.47 px at 22 px) and it fades out half a pixel further: the spec's 1.5 px needs an atlas with a wider field.
        /// </summary>
        static readonly Color HaloColor = new Color(0, 0, 0, 0.7f);

        const float HaloDilate = 0.5f;

        /// <summary>A press that moves further than this (px at 1080p) before its release is a drag, not a click (as the HUD's).</summary>
        const float ClickSlopPx = 6f;

        /// <summary>The shader keywords of TextMeshPro's drop shadow preset, the one underlay variant every build carries.</summary>
        static readonly string[] HaloKeywords = { "OUTLINE_ON", "UNDERLAY_ON" };

        static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth"), UnderlayColorId = Shader.PropertyToID("_UnderlayColor"),
            UnderlayXId = Shader.PropertyToID("_UnderlayOffsetX"), UnderlayYId = Shader.PropertyToID("_UnderlayOffsetY"),
            UnderlayDilateId = Shader.PropertyToID("_UnderlayDilate"), UnderlaySoftnessId = Shader.PropertyToID("_UnderlaySoftness");

        sealed class Slot
        {
            public TextMeshPro Tmp;
            public SpriteRenderer Icon;   // created the first time the slot shows an icon, then reused
            public bool IconShown;
            public SpriteRenderer Plate;  // created the first time the slot shows a plate, then reused
            public bool PlateShown;
            public SpriteRenderer Leader; // created the first time the slot shows a leader, then reused
            public bool LeaderShown;
            public Material Material;     // the font's own material, which every label but a role label keeps
            public LabelSpec Spec;
            public int TextVersion;       // the spec's TextVersion this slot shows
            public float Alpha;
            public float Fade = 1;        // last focus/handoff fade while placed, kept while fading out
            public bool Used;
            public bool OnTop;            // its spec's LabelSpec.OnTop when last assigned: its plate and leader follow it
        }

        /// <summary>A label the last layout placed, and where.</summary>
        struct Placed
        {
            public LabelSpec Spec;
            public Vector3 World;

            /// <summary>0: its own offset; k: fallback k - 1; past its fallbacks: a candidate place, its own moved by <see cref="Shift"/>.</summary>
            public int Place;

            /// <summary>A candidate place's move from the label's own (screen px).</summary>
            public Vector2 Shift;

            /// <summary>Screen px per px of the label's own offsets (<see cref="UiScale"/>, or a role label's em over its size).</summary>
            public float Unit;

            /// <summary>Screen px per em.</summary>
            public float EmPx;

            /// <summary>The padded line box (collisions, hover), the line box and the letters' box (blocks, metrics), screen px.</summary>
            public Rect Rect, Line, Letters;

            public float Fade;

            /// <summary>Whether a leader runs back to the label's point, and its ends; the label's point with its offset (screen px).</summary>
            public bool Leader;

            public Vector2 From, To, Point;
        }

        /// <summary>
        /// A label's boxes about its point at one size (screen px): the group (icon, text and a plate's margins) from Left
        /// across Width and Height tall, centred on the point's height; the letters from X0 to X1 and Half above and below.
        /// </summary>
        readonly struct Box
        {
            public readonly float Left, Width, Height, Margin, X0, X1, Half;

            Box(float left, float width, float height, float margin, float x0, float x1, float half)
            {
                Left = left;
                Width = width;
                Height = height;
                Margin = margin;
                X0 = x0;
                X1 = x1;
                Half = half;
            }

            /// <summary>
            /// A label's boxes for a group <paramref name="lw"/> px wide at <paramref name="size"/> times
            /// <paramref name="scale"/> px per em (in that order, as the boxes were always reckoned), its lines
            /// <paramref name="lineEm"/> tall and its letters <paramref name="capEm"/> above and below its centre. Width covers
            /// icon, gap and text, so the alignment places the whole group (see Assign), and a plate's margins, which reach past
            /// the group on both sides. What a block keeps out is the letters and icon, a slack inside their outline, not the
            /// padded line box (a date right above the readout reads whole; only labels keep the padding between them).
            /// </summary>
            public static Box Of(LabelSpec s, float lw, float size, float scale, float lineEm, float capEm)
            {
                float lh = size * scale * Mathf.Max(lineEm, s.IconEm * IconGlyphFraction);
                float margin = s.Plate.a > 0 ? PlateMarginEm * size * scale : 0;
                float left = s.Align == TextAlignmentOptions.Right ? margin - lw
                    : s.Align == TextAlignmentOptions.Center ? -lw * 0.5f
                    : -margin;
                float slack = BlockSlackPx * UiScale;
                float half = Mathf.Max(0, size * scale * Mathf.Max(capEm, 0.5f * s.IconEm * IconGlyphFraction) - slack);
                return new Box(left, lw, lh, margin, left + margin + slack, left + lw - margin - slack, half);
            }

            /// <summary>
            /// The text and icon wholly on screen with the point at (x, y) (a plate may run to the frame's edge; the padding
            /// only spaces labels apart).
            /// </summary>
            public bool OnScreen(float x, float y, float w, float h) =>
                !(x + Left + Margin < 0 || x + Left + Width - Margin > w || y - Height * 0.5f < 0 || y + Height * 0.5f > h);

            public Rect Line(float x, float y) => new Rect(x + Left, y - Height * 0.5f, Width, Height);

            public Rect Letters(float x, float y) => Rect.MinMaxRect(x + X0, y - Half, x + X1, y + Half);
        }

        /// <summary>What <see cref="Test"/> found at a place: room, no room, or no room only because a block moves labels aside.</summary>
        const int Free = 0, NoRoom = 1, Aside = 2;

        readonly List<LabelSpec> specs = new List<LabelSpec>();
        readonly List<LabelSpec> pending = new List<LabelSpec>();
        readonly object gate = new object();
        readonly List<Slot> slots = new List<Slot>();
        readonly List<Rect> placedRects = new List<Rect>();

        /// <summary>The labels placed by the last layout.</summary>
        readonly List<Placed> placed = new List<Placed>();

        /// <summary>The leaders the last layout drew (screen px), which later labels and leaders keep clear of.</summary>
        readonly List<(Vector2 from, Vector2 to)> leaders = new List<(Vector2, Vector2)>();

        /// <summary>
        /// Groups to swap in at the next flush, by owner (null: clear the group), and the size of each group taken; owners
        /// compare by reference, so any object (a layer, a string key) names a group.
        /// </summary>
        readonly Dictionary<object, List<LabelSpec>> pendingGroups = new Dictionary<object, List<LabelSpec>>(ReferenceComparer.Instance);

        readonly Dictionary<object, int> groupSizes = new Dictionary<object, int>(ReferenceComparer.Instance);

        /// <summary>The measured texts (width and lines, em) of the group being replaced, for its new labels of the same words.</summary>
        readonly Dictionary<string, Vector2> replacedTexts = new Dictionary<string, Vector2>(StringComparer.Ordinal);

        /// <summary>Role labels among the specs (the layout scans past <see cref="MaxVisible"/> other labels only for them).</summary>
        int roleSpecs;

        /// <summary>The parts labels are bound to: key, alpha this frame, shown (at least <see cref="PartHiddenBelow"/>) and subject.</summary>
        readonly Dictionary<string, int> partIds = new Dictionary<string, int>(StringComparer.Ordinal);

        readonly List<string> partKeys = new List<string>();
        readonly List<float> partAlpha = new List<float>();
        readonly List<bool> partShown = new List<bool>(), partSubject = new List<bool>();
        bool partProblemLogged;

        /// <summary>UI blocks no label goes under (see <see cref="KeepClear"/>), destroyed ones pruned as they are met.</summary>
        static readonly List<RectTransform> clearBlocks = new List<RectTransform>();

        /// <summary>UI blocks that move labels aside (see <see cref="MoveAside"/>).</summary>
        static readonly List<RectTransform> asideBlocks = new List<RectTransform>();

        static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>
        /// Screen rects of the blocks shown at the last layout, and this frame's measure of them: the blocks on a screen-space
        /// canvas (every label keeps out), those on a headset's world-space canvases (role labels keep out), the blocks that
        /// move labels aside, and the HUD's blocks (<see cref="HudRects"/>; role labels keep out).
        /// </summary>
        readonly List<Rect> blockRects = new List<Rect>(), measuredBlocks = new List<Rect>(), worldRects = new List<Rect>(),
            measuredWorld = new List<Rect>(), asideRects = new List<Rect>(), measuredAside = new List<Rect>(),
            hudRects = new List<Rect>(), measuredHud = new List<Rect>();

        bool hudProblemLogged;

        /// <summary>What the last layout did with the role labels: none placed for want of room, hidden by their part, moved.</summary>
        int roleCulled, roleHidden;

        CameraRig rig;
        int lastCam = -1, lastWarp = -1, lastHighlightHash, frame;
        bool dirty = true, dataLabelsHidden;

        /// <summary>The TextMeshPro that measures texts (never drawn), and the role labels' halo material, made when first needed.</summary>
        TextMeshPro measure;

        Material halo;
        bool haloTried;

        static Sprite line;

        /// <summary>A click on a label with an action: the label pressed and where.</summary>
        LabelSpec pressed;

        Vector2 pressedAt;

        static readonly Comparison<LabelSpec> PlacementOrder = ComparePlacementOrder;

        /// <summary>
        /// While true only fixed (world-space) labels are placed: a scene sets it while the camera looks at a diagram
        /// that stands beyond the timeline's end, so the timeline's own labels in front of it do not crowd its view.
        /// </summary>
        public bool DataLabelsHidden
        {
            get => dataLabelsHidden;
            set
            {
                if (value == dataLabelsHidden) return;
                dataLabelsHidden = value;
                dirty = true;
            }
        }

        /// <summary>
        /// Pixel sizes are authored for a 1080p screen: scaled by the short side, and
        /// <see cref="ScreenLayout.PortraitLabelZoom"/> times more in portrait (phone videos).
        /// </summary>
        public static float UiScale => ScreenLayout.LabelScale;

        /// <summary>The label under the mouse, if any (for tooltips).</summary>
        public LabelSpec Hovered { get; private set; }

        /// <summary>
        /// The emphasis of a part (spec 3.1) for labels bound to one (<see cref="LabelSpec.Part"/>): its alpha, 0 to 1, read on
        /// the main thread every frame. The label's alpha follows it, it is hidden below <see cref="PartHiddenBelow"/>, and a
        /// role label's part at <see cref="SubjectAlpha"/> or more is a subject. Null: every part at 1 (Economy.HillLabels.PartAlpha
        /// sets it; WP13 wires it to the emphasis table).
        /// </summary>
        public static Func<string, float> PartAlpha { get; set; }

        /// <summary>
        /// Main thread: adds the screen rects (pixels, origin bottom-left, as the camera projects) of the HUD's blocks shown now
        /// to a list, every frame; role labels keep their letters out from under them, as from the blocks of
        /// <see cref="KeepClear"/>. The economy sets it (Economy.UI.HudBlocks: the title, the legend, the panels, the tour card,
        /// the bar, the year chip); null adds none.
        /// </summary>
        public Action<List<Rect>> HudRects { get; set; }

        /// <summary>The colour of the leaders (as sprites take it, sRGB): a rule's grey; the economy gives its own ink's rule.</summary>
        public Color LeaderColor { get; set; } = GraphStyle.AxisDim;

        /// <summary>Every label the system holds (added for good, or in a group), after the last flush.</summary>
        public int Count => specs.Count;

        public void Init(CameraRig cameraRig) => rig = cameraRig;

        // ------------------------------------------------------------------ roles

        /// <summary>A role's height at 1080p (px; spec 3.5): title 18, name 15, value 13, note 12; <see cref="LabelSpec.SizePx"/>'s default otherwise.</summary>
        public static float RolePx(LabelRole role) =>
            role == LabelRole.Title ? 18 : role == LabelRole.Name ? 15 : role == LabelRole.Value ? 13 : role == LabelRole.Note ? 12 : 13;

        /// <summary>A role's placement priority (spec 3.5): title 10, name 8, value 6, note 4 (a subject's labels gain <see cref="SubjectBonus"/>).</summary>
        public static float RolePriority(LabelRole role) =>
            role == LabelRole.Title ? 10 : role == LabelRole.Name ? 8 : role == LabelRole.Value ? 6 : role == LabelRole.Note ? 4 : 1;

        /// <summary>The least of the view a role label's em takes in a headset (degrees).</summary>
        public static float RoleDegreesOf(LabelRole role) => role == LabelRole.Title ? TitleDegrees : RoleDegrees;

        // ------------------------------------------------------------------ adding, groups, blocks

        /// <summary>
        /// Thread safe: queue a label (layers call this from Prepare). A label needs text, an icon, or both;
        /// the first icon label starts rasterizing the icon atlas in the background.
        /// </summary>
        public void Add(LabelSpec spec)
        {
            if (!Admit(spec)) return;
            lock (gate) pending.Add(spec);
        }

        /// <summary>
        /// Thread safe: makes <paramref name="labels"/> the whole of <paramref name="owner"/>'s group from the next frame on,
        /// in place of what the group held: a layer that rebuilds (every year of the replay) replaces its labels instead of
        /// adding more. The specs are the system's from then on (a caller does not change them, except through
        /// <see cref="SetText"/> and <see cref="LabelSpec.Hidden"/> on the main thread). Several replacements before a frame
        /// keep the last. A label replaced by one of the same text keeps its place on screen without fading out and in.
        /// </summary>
        public void Replace(object owner, IEnumerable<LabelSpec> labels)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner), "A group of labels needs an owner.");
            List<LabelSpec> group = new List<LabelSpec>();
            if (labels != null)
            {
                foreach (LabelSpec s in labels)
                {
                    if (Admit(s)) group.Add(s);
                }
            }

            lock (gate) pendingGroups[owner] = group;
        }

        /// <summary>Thread safe: takes <paramref name="owner"/>'s group off the screen from the next frame on.</summary>
        public void Clear(object owner)
        {
            if (owner == null) return;
            lock (gate) pendingGroups[owner] = null;
        }

        /// <summary>Main thread: how many labels <paramref name="owner"/>'s group holds after the last flush.</summary>
        public int GroupCount(object owner) => owner != null && groupSizes.TryGetValue(owner, out int n) ? n : 0;

        /// <summary>
        /// Main thread: how many of the labels the system holds belong to <paramref name="owner"/> (counted one by one: equal to
        /// <see cref="GroupCount"/> unless a replaced label was left behind).
        /// </summary>
        public int Held(object owner)
        {
            int n = 0;
            for (int i = 0; i < specs.Count; i++)
            {
                if (owner != null && ReferenceEquals(specs[i].Owner, owner)) n++;
            }

            return n;
        }

        /// <summary>Whether a label can be shown (text, an icon, or both); the first icon label starts the icon atlas.</summary>
        static bool Admit(LabelSpec spec)
        {
            if (spec == null) return false;
            bool icon = Icons.Has(spec.Icon);
            if (!icon && !string.IsNullOrEmpty(spec.Icon)) Debug.LogWarning($"Label '{spec.Text}': unknown icon '{spec.Icon}'.");
            if (!icon && string.IsNullOrEmpty(spec.Text)) return false;
            if (icon) Icons.Prepare();
            return true;
        }

        public void MarkDirty() => dirty = true;

        /// <summary>
        /// Main thread: keeps labels out from under a UI block (the HUD's legend and preset bar, the tour's card). Its owner
        /// registers it once; while it is shown its screen rect is measured every frame, so labels re-place when it moves,
        /// appears or goes. Only blocks on a screen-space overlay canvas count for every label: in a headset the panels hang
        /// in the world, where only role labels keep out from under them (as the eye sees them).
        /// </summary>
        public static void KeepClear(RectTransform block)
        {
            if (block != null && !clearBlocks.Contains(block)) clearBlocks.Add(block);
        }

        /// <summary>
        /// Main thread: a block that stands over the graph's labels on purpose (the director's caption plate and the ring at
        /// its arrow's tip): a label its letters meet, at its own place and its fallbacks, tries the four candidate places
        /// (<see cref="CandidateEm"/>, with a leader) before it is dropped, so the names it covers move aside instead of hiding
        /// or standing half under it. Registered once like <see cref="KeepClear"/>; counts while shown, on a screen-space canvas.
        /// </summary>
        public static void MoveAside(RectTransform block)
        {
            if (block != null && !asideBlocks.Contains(block)) asideBlocks.Add(block);
        }

        /// <summary>
        /// Measures the blocks shown now (screen pixels, as the camera projects; one standing within
        /// <see cref="EdgeStripPx"/> of a screen edge reaches to it); true when one moved, appeared or went since the last measure.
        /// </summary>
        bool MeasureBlocks()
        {
            Camera cam = rig.Cam;
            float strip = EdgeStripPx * ScreenLayout.UiScale, w = cam.pixelWidth, h = cam.pixelHeight;
            measuredBlocks.Clear();
            measuredWorld.Clear();
            measuredAside.Clear();
            measuredHud.Clear();
            Measure(clearBlocks, measuredBlocks, measuredWorld, cam, strip, w, h);
            Measure(asideBlocks, measuredAside, null, cam, strip, w, h);
            Action<List<Rect>> hud = HudRects;
            if (hud != null)
            {
                try
                {
                    hud(measuredHud);
                }
                catch (Exception e)
                {
                    measuredHud.Clear();
                    if (!hudProblemLogged)
                    {
                        hudProblemLogged = true;
                        Debug.LogError("[Why] labels: the HUD's blocks could not be measured, so role labels keep out of the keep-out blocks only: " + e);
                    }
                }

                for (int i = 0; i < measuredHud.Count; i++) measuredHud[i] = ToEdges(measuredHud[i], strip, w, h);
            }

            // all four, not the first that changed: each list keeps this frame's measure
            bool moved = Took(measuredBlocks, blockRects);
            moved |= Took(measuredWorld, worldRects);
            moved |= Took(measuredAside, asideRects);
            moved |= Took(measuredHud, hudRects);
            return moved;
        }

        /// <summary>
        /// The screen rects of the registered blocks shown now: those on a screen-space overlay canvas into
        /// <paramref name="overlay"/> (its world corners are screen pixels), those on an enabled world-space canvas (a headset's
        /// HUD plane) projected by the camera into <paramref name="world"/>, unless it is null.
        /// </summary>
        static void Measure(List<RectTransform> blocks, List<Rect> overlay, List<Rect> world, Camera cam, float strip, float w, float h)
        {
            for (int i = blocks.Count - 1; i >= 0; i--)
            {
                RectTransform block = blocks[i];
                if (block == null)
                {
                    blocks.RemoveAt(i); // destroyed with its scene
                    continue;
                }

                if (!block.gameObject.activeInHierarchy) continue;
                Canvas canvas = block.GetComponentInParent<Canvas>();
                Canvas screen = canvas != null ? canvas.rootCanvas : null;
                if (screen == null || !screen.enabled) continue;
                if (screen.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    block.GetWorldCorners(Corners); // an overlay canvas's world corners are screen pixels
                    overlay.Add(ToEdges(Rect.MinMaxRect(Corners[0].x, Corners[0].y, Corners[2].x, Corners[2].y), strip, w, h));
                }
                else if (world != null && screen.renderMode == RenderMode.WorldSpace && Project(block, cam, out Rect r))
                {
                    world.Add(ToEdges(r, strip, w, h));
                }
            }
        }

        /// <summary>A world-space block's screen rect (pixels), false when any corner stands behind the camera.</summary>
        internal static bool Project(RectTransform block, Camera cam, out Rect r)
        {
            r = default;
            if (cam == null) return false;
            block.GetWorldCorners(Corners);
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            for (int c = 0; c < 4; c++)
            {
                Vector3 p = cam.WorldToScreenPoint(Corners[c]);
                if (p.z <= 0) return false;
                x0 = Mathf.Min(x0, p.x);
                y0 = Mathf.Min(y0, p.y);
                x1 = Mathf.Max(x1, p.x);
                y1 = Mathf.Max(y1, p.y);
            }

            r = Rect.MinMaxRect(x0, y0, x1, y1);
            return true;
        }

        /// <summary>A block's rect reaching to a screen edge it stands within <paramref name="strip"/> of.</summary>
        static Rect ToEdges(Rect r, float strip, float w, float h)
        {
            if (r.xMin < strip) r.xMin = 0;
            if (r.yMin < strip) r.yMin = 0;
            if (r.xMax > w - strip) r.xMax = w;
            if (r.yMax > h - strip) r.yMax = h;
            return r;
        }

        /// <summary>Keeps this frame's measure of a list of blocks; true when it differs from the last (by half a pixel or more).</summary>
        static bool Took(List<Rect> measured, List<Rect> last)
        {
            bool same = measured.Count == last.Count;
            for (int i = 0; same && i < measured.Count; i++)
            {
                Rect a = measured[i], b = last[i];
                same = Mathf.Abs(a.xMin - b.xMin) < 0.5f && Mathf.Abs(a.yMin - b.yMin) < 0.5f &&
                       Mathf.Abs(a.xMax - b.xMax) < 0.5f && Mathf.Abs(a.yMax - b.yMax) < 0.5f;
            }

            if (same) return false;
            last.Clear();
            last.AddRange(measured);
            return true;
        }

        /// <summary>
        /// Main thread: changes the text of a label that was already added (a live readout, a strategy name), re-measures
        /// it for placement and refreshes it where it is shown.
        /// </summary>
        public void SetText(LabelSpec spec, string text)
        {
            if (spec == null || spec.Text == text) return;
            spec.Text = text;
            bool hasText = !string.IsNullOrEmpty(text);
            float em = EstimateWidth(text);
            spec.EstimateEm = em;
            if (spec.IconEm > 0) em += spec.IconEm * IconGlyphFraction + (hasText ? IconGapEm : 0);
            if (spec.Plate.a > 0) em += 2 * PlateMarginEm;
            spec.WidthEm = em;
            spec.Width = em * spec.SizePx;
            spec.TextVersion++;
            dirty = true;
        }

        void FlushPending()
        {
            lock (gate)
            {
                if (pending.Count == 0 && pendingGroups.Count == 0) return;
                foreach (LabelSpec s in pending)
                {
                    Take(s, null);
                    specs.Add(s);
                }

                pending.Clear();
                foreach (KeyValuePair<object, List<LabelSpec>> g in pendingGroups)
                {
                    RemoveGroup(g.Key);
                    if (g.Value == null)
                    {
                        groupSizes.Remove(g.Key);
                        continue;
                    }

                    foreach (LabelSpec s in g.Value)
                    {
                        Take(s, g.Key);
                        specs.Add(s);
                    }

                    groupSizes[g.Key] = g.Value.Count;
                    replacedTexts.Clear();
                }

                pendingGroups.Clear();
                replacedTexts.Clear(); // a cleared group's, which no label took
            }

            // a new part reads as shown and no subject until LateUpdate reads it, before the layout (and sorts again if it is one)
            Sort();
            dirty = true;
        }

        /// <summary>Takes a spec in (main thread, under the gate): its estimated width, its icon's size, its group and its part.</summary>
        void Take(LabelSpec s, object owner)
        {
            bool hasText = !string.IsNullOrEmpty(s.Text);
            float em = EstimateWidth(s.Text);
            s.EstimateEm = em;
            s.IconEm = !Icons.Has(s.Icon) ? 0 : hasText ? IconEm : IconOnlyEm;
            if (s.IconEm > 0) em += s.IconEm * IconGlyphFraction + (hasText ? IconGapEm : 0);
            if (s.Plate.a > 0) em += 2 * PlateMarginEm;
            s.WidthEm = em;
            s.Width = em * s.SizePx;
            s.Owner = owner;
            s.Removed = false;
            s.PartIndex = string.IsNullOrEmpty(s.Part) ? -1 : PartId(s.Part);
            if (s.Role != LabelRole.None) roleSpecs++;
            // a text its group held before keeps its measure: a layer that rebuilds every year repeats most of its words
            if (owner != null && s.TextEm < 0 && s.Text != null && replacedTexts.TryGetValue(s.Text, out Vector2 measured))
            {
                s.TextEm = measured.x;
                s.TextLinesEm = measured.y;
                s.MeasuredVersion = s.TextVersion;
            }
        }

        /// <summary>
        /// Takes a group's labels out of the specs, in place (they may still fade out where they were shown), keeping their
        /// texts' measures for the labels that replace them (<see cref="replacedTexts"/>).
        /// </summary>
        void RemoveGroup(object owner)
        {
            int kept = 0;
            for (int i = 0; i < specs.Count; i++)
            {
                LabelSpec s = specs[i];
                if (ReferenceEquals(s.Owner, owner))
                {
                    s.Removed = true;
                    if (s.Role != LabelRole.None) roleSpecs--;
                    if (s.Text != null && s.TextEm >= 0 && s.MeasuredVersion == s.TextVersion) replacedTexts[s.Text] = new Vector2(s.TextEm, s.TextLinesEm);
                    continue;
                }

                specs[kept++] = s;
            }

            if (kept < specs.Count) specs.RemoveRange(kept, specs.Count - kept);
        }

        /// <summary>The placement order of every spec from its priority now (as before S04, a layer may change a priority between flushes).</summary>
        void Sort()
        {
            for (int i = 0; i < specs.Count; i++)
            {
                LabelSpec s = specs[i];
                s.Order = s.Role != LabelRole.None && s.PartIndex >= 0 && partSubject[s.PartIndex] ? s.Priority + SubjectBonus : s.Priority;
            }

            specs.Sort(PlacementOrder);
        }

        /// <summary>
        /// Placement order: higher priority first, then higher rank; remaining ties broken by text and then position, so
        /// the same graph always shows the same labels however the layers' worker threads happened to interleave their
        /// Add calls (screenshots and recorded videos are reproducible).
        /// </summary>
        static int ComparePlacementOrder(LabelSpec a, LabelSpec b)
        {
            int c = b.Order.CompareTo(a.Order);
            if (c != 0) return c;
            c = b.Rank.CompareTo(a.Rank);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.Text, b.Text);
            if (c != 0) return c;
            c = a.Data.x.CompareTo(b.Data.x);
            if (c != 0) return c;
            c = a.Data.y.CompareTo(b.Data.y);
            return c != 0 ? c : a.Data.z.CompareTo(b.Data.z);
        }

        // ------------------------------------------------------------------ parts

        /// <summary>A part's row in the part table (main thread), added at 1, shown and not a subject until the first read.</summary>
        int PartId(string key)
        {
            if (partIds.TryGetValue(key, out int id)) return id;
            id = partKeys.Count;
            partIds.Add(key, id);
            partKeys.Add(key);
            partAlpha.Add(1);
            partShown.Add(true);
            partSubject.Add(false);
            return id;
        }

        /// <summary>
        /// Reads every part's alpha (<see cref="PartAlpha"/>); true when a part was shown or hidden, or became or stopped being
        /// a subject (then the specs are sorted again: a subject's role labels place first). A provider that throws counts its
        /// parts at 1 and is reported once.
        /// </summary>
        bool UpdateParts()
        {
            Func<string, float> provider = PartAlpha;
            bool shownChanged = false, subjectChanged = false;
            for (int i = 0; i < partKeys.Count; i++)
            {
                float a = 1;
                if (provider != null)
                {
                    try
                    {
                        a = provider(partKeys[i]);
                    }
                    catch (Exception e)
                    {
                        a = 1;
                        if (!partProblemLogged)
                        {
                            partProblemLogged = true;
                            Debug.LogError($"[Why] labels: the part alpha of '{partKeys[i]}' could not be read, so its labels show at full alpha: {e}");
                        }
                    }
                }

                a = float.IsNaN(a) ? 1 : Mathf.Clamp01(a);
                partAlpha[i] = a;
                bool shown = a >= PartHiddenBelow, subject = a >= SubjectAlpha;
                if (shown != partShown[i])
                {
                    partShown[i] = shown;
                    shownChanged = true;
                }

                if (subject != partSubject[i])
                {
                    partSubject[i] = subject;
                    subjectChanged = true;
                }
            }

            if (subjectChanged && roleSpecs > 0) Sort();
            return shownChanged || (subjectChanged && roleSpecs > 0);
        }

        // ------------------------------------------------------------------ widths

        /// <summary>
        /// Width in em of the visible characters. Rich-text tags such as &lt;sup&gt; are skipped; characters
        /// inside &lt;sup&gt; or &lt;sub&gt; count at script size, and inside &lt;size=NN%&gt; at that size. It runs about
        /// 0.2 em wide of the text as set (the margin added at the end); where that decides whether a label stands,
        /// <see cref="Measured"/> measures the text itself.
        /// </summary>
        static float EstimateWidth(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            float w = 0, script = 1, size = 1;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<')
                {
                    int end = TagEnd(text, i);
                    if (end > 0)
                    {
                        if (IsTag(text, i, "sup") || IsTag(text, i, "sub")) script = ScriptScale;
                        else if (IsTag(text, i, "/sup") || IsTag(text, i, "/sub")) script = 1;
                        else if (IsTag(text, i, "/size")) size = 1;
                        else if (TrySizePercent(text, i, end, out float percent)) size = percent;
                        i = end;
                        continue;
                    }
                }

                w += script * size * (c == ' ' ? 0.28f : char.IsUpper(c) ? 0.64f : char.IsDigit(c) ? 0.55f : 0.5f);
            }

            return w > 0 ? w + 0.2f : 0;
        }

        /// <summary>
        /// The factor of a &lt;size=NN%&gt; tag opening at i and closing at end (whole percent only); false for
        /// any other tag, including absolute sizes, which the estimate ignores.
        /// </summary>
        static bool TrySizePercent(string text, int i, int end, out float factor)
        {
            factor = 1;
            const string prefix = "size=";
            int start = i + 1 + prefix.Length;
            if (end - start < 2 || end - start > 5 || text[end - 1] != '%' ||
                string.Compare(text, i + 1, prefix, 0, prefix.Length, System.StringComparison.OrdinalIgnoreCase) != 0)
            {
                return false;
            }

            int percent = 0;
            for (int j = start; j < end - 1; j++)
            {
                char c = text[j];
                if (c < '0' || c > '9') return false;
                percent = percent * 10 + (c - '0');
            }

            factor = percent / 100f;
            return true;
        }

        /// <summary>Index of the '&gt;' closing a rich-text tag that opens at i, or -1 when the '&lt;' is literal text.</summary>
        static int TagEnd(string text, int i)
        {
            if (i + 1 >= text.Length) return -1;
            char first = text[i + 1];
            if (first != '/' && first != '#' && !char.IsLetter(first)) return -1;
            int limit = Mathf.Min(text.Length, i + 64);
            for (int j = i + 2; j < limit; j++)
            {
                if (text[j] == '>') return j;
                if (text[j] == '<') return -1;
            }

            return -1;
        }

        /// <summary>True when the tag opening at i is exactly &lt;name&gt; (case-insensitive, like TextMeshPro).</summary>
        static bool IsTag(string text, int i, string name)
        {
            int close = i + 1 + name.Length;
            return close < text.Length && text[close] == '>' &&
                   string.Compare(text, i + 1, name, 0, name.Length, System.StringComparison.OrdinalIgnoreCase) == 0;
        }

        /// <summary>
        /// Main thread: the spec's text as TextMeshPro sets it (width and height in em, its lines), measured once per text by
        /// a TextMeshPro of the labels' own font and size that is never drawn.
        /// </summary>
        void Measured(LabelSpec s)
        {
            if (s.MeasuredVersion == s.TextVersion && s.TextEm >= 0) return;
            s.MeasuredVersion = s.TextVersion;
            if (string.IsNullOrEmpty(s.Text))
            {
                s.TextEm = 0;
                s.TextLinesEm = 0;
                return;
            }

            if (measure == null)
            {
                GameObject go = new GameObject("Measure");
                go.transform.SetParent(transform, false);
                measure = go.AddComponent<TextMeshPro>();
                measure.fontSize = 10; // as a slot's: one local unit per em
                measure.textWrappingMode = TextWrappingModes.NoWrap;
                measure.overflowMode = TextOverflowModes.Overflow;
                measure.rectTransform.sizeDelta = new Vector2(TextRectEm, 2);
                measure.renderer.enabled = false;
            }

            Vector2 size = measure.GetPreferredValues(s.Text);
            s.TextEm = size.x;
            s.TextLinesEm = size.y;
        }

        // ------------------------------------------------------------------ layout

        void LateUpdate()
        {
            if (rig == null) return;
            FlushPending();
            frame++;

            bool partsChanged = UpdateParts();
            int hiHash = Highlighter.HasHighlight ? frame / 20 : 0;
            bool blocksMoved = MeasureBlocks();
            bool changed = dirty || blocksMoved || partsChanged || rig.Version != lastCam || GraphWarp.Version != lastWarp ||
                           hiHash != lastHighlightHash;
            if (changed)
            {
                Layout();
                lastCam = rig.Version;
                lastWarp = GraphWarp.Version;
                lastHighlightHash = hiHash;
                dirty = false;
            }

            UpdateSlots();
            UpdateHover();
            UpdateClick();
        }

        void Layout()
        {
            Camera cam = rig.Cam;
            placed.Clear();
            placedRects.Clear();
            leaders.Clear();
            roleCulled = roleHidden = 0;
            WarpState warp = GraphWarp.Current;
            float w = cam.pixelWidth, h = cam.pixelHeight;
            bool xr = Xr.XrSession.Active;
            int budget = xr ? RoleBudgetXr : RoleBudget, legacy = 0, roles = 0;
            Vector3 eye = cam.transform.position;

            for (int i = 0; i < specs.Count; i++)
            {
                LabelSpec s = specs[i];
                bool role = s.Role != LabelRole.None;
                if (!role && legacy >= MaxVisible)
                {
                    // the other labels are done; role labels have a budget of their own
                    if (roleSpecs == 0 || roles >= budget) break;
                    continue;
                }

                if (s.Hidden || (dataLabelsHidden && !s.Fixed)) continue;
                if (s.Fixed && s.FixedRange > 0 && (rig.Pose.Target - s.Data).sqrMagnitude > s.FixedRange * s.FixedRange) continue;
                float fade = s.Fixed ? 1f : GraphWarp.FocusFade(s.Data.x, warp);
                if (s.HandoffFade && !s.Fixed) fade *= GraphStyle.HandoffFade(s.Data.x);
                if (fade < 0.35f) continue;
                if (s.PartIndex >= 0 && !partShown[s.PartIndex])
                {
                    if (role) roleHidden++;
                    continue;
                }

                Vector3 world = s.Fixed ? s.Data : GraphWarp.ToWorld(s.Data.x, s.Data.y, s.Data.z, warp);
                Vector3 sp = cam.WorldToScreenPoint(world);
                if (sp.z <= cam.nearClipPlane) continue;
                if (role && roles >= budget)
                {
                    roleCulled++;
                    continue;
                }

                if (Place(s, role, world, sp, fade, w, h, xr, eye))
                {
                    if (role) roles++;
                    else legacy++;
                }
                else if (role)
                {
                    roleCulled++;
                }
            }
        }

        /// <summary>
        /// Places one label: its own offset, then its fallbacks, the first place free and with the text and icon wholly on
        /// screen; then, for a role label, one that asks for them, or one a block that moves labels aside stood over, the four
        /// candidate places (<see cref="CandidateEm"/> from its own: right, left, above, below), with a leader back to its point
        /// when it moved further than <see cref="LeaderMinPx"/> and that leader crosses no label or leader already placed.
        /// </summary>
        bool Place(LabelSpec s, bool role, Vector3 world, Vector3 sp, float fade, float w, float h, bool xr, Vector3 eye)
        {
            float em, unit;
            Box est, tru = default;
            int truState = 0;
            if (role)
            {
                // clamped (spec 3.5), portrait's zoom included, and at least the role's degrees of the view in a headset; its
                // own offsets grow with it
                float size = Mathf.Clamp(s.SizePx, MinRolePx, MaxRolePx);
                em = size * UiScale;
                if (xr) em = Mathf.Max(em, DegreesPx(world, eye, RoleDegreesOf(s.Role)));
                unit = em / size;
                Measured(s);
                est = RoleBox(s, size, unit);
                truState = -1;
            }
            else
            {
                em = s.SizePx * UiScale;
                unit = UiScale;
                est = Box.Of(s, s.Width * UiScale, s.SizePx, UiScale, LineEm, CapEm);
            }

            int places = 1 + (s.Fallbacks?.Length ?? 0);
            bool aside = false;
            for (int k = 0; k < places; k++)
            {
                Vector2 offset = Offset(s, k) * unit;
                float x = sp.x + offset.x, y = sp.y + offset.y;
                int found = Test(s, role, x, y, est, ref tru, ref truState, w, h, out Rect line, out Rect letters);
                if (found == Aside) aside = true;
                if (found != Free) continue;
                Add(s, world, k, Vector2.zero, unit, em, line, letters, fade, false, default, default, new Vector2(x, y));
                return true;
            }

            if (!role && !s.Candidates && !aside) return false;
            Vector2 own = Offset(s, 0) * unit;
            float step = CandidateEm * em;
            for (int c = 0; c < CandidateDirections.Length; c++)
            {
                Vector2 shift = CandidateDirections[c] * step;
                float x = sp.x + own.x + shift.x, y = sp.y + own.y + shift.y;
                if (Test(s, role, x, y, est, ref tru, ref truState, w, h, out Rect line, out Rect letters) != Free) continue;
                Vector2 from = default, to = default;
                bool leader = step > LeaderMinPx * UiScale && LeaderSegment(new Vector2(sp.x, sp.y), line, out from, out to);
                if (leader && LeaderCrosses(from, to)) continue;
                Add(s, world, places + c, shift, unit, em, line, letters, fade, leader, from, to, new Vector2(x, y));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Whether a label can stand with its point at (x, y): its padded line box clear of the labels and leaders already
        /// placed, its text and icon wholly on screen, its letters clear of the blocks (<see cref="Fits"/>). A label that is not
        /// a role label is first tested as before, at its estimated width (<see cref="EstimateWidth"/>, which also keeps its
        /// spacing from its neighbours); only where that leaves it off screen or under a block is its text measured and
        /// tested again (the estimate runs about 0.2 em wide: 'Animals' cleared the caption plate at the tetrapods' stop).
        /// </summary>
        int Test(LabelSpec s, bool role, float x, float y, in Box est, ref Box tru, ref int truState, float w, float h,
            out Rect line, out Rect letters)
        {
            line = est.Line(x, y);
            letters = est.Letters(x, y);
            if (Taken(new Rect(line.x - Padding, line.y - Padding, line.width + Padding * 2, line.height + Padding * 2))) return NoRoom;
            int fit = Fits(role, est, x, y, w, h, letters);
            if (fit == Free || role) return fit;
            if (truState == 0)
            {
                truState = -1;
                if (!string.IsNullOrEmpty(s.Text) && s.WidthEm > 0)
                {
                    Measured(s);
                    float width = s.Width * (s.WidthEm - s.EstimateEm + s.TextEm) / s.WidthEm;
                    tru = Box.Of(s, width * UiScale, s.SizePx, UiScale, LineEm, CapEm);
                    truState = 1;
                }
            }

            if (truState != 1) return fit;
            letters = tru.Letters(x, y);
            return Fits(false, tru, x, y, w, h, letters);
        }

        /// <summary>
        /// The text and icon wholly on screen and the letters clear of the keep-out blocks (and, for a role label, of a
        /// headset's panels and the HUD's blocks); <see cref="Aside"/> when only a block that moves labels aside is in the way.
        /// </summary>
        int Fits(bool role, in Box b, float x, float y, float w, float h, Rect letters)
        {
            if (!b.OnScreen(x, y, w, h) || Under(blockRects, letters)) return NoRoom;
            if (role && (Under(worldRects, letters) || Under(hudRects, letters))) return NoRoom;
            return Under(asideRects, letters) ? Aside : Free;
        }

        /// <summary>A role label's boxes at <paramref name="size"/> times <paramref name="unit"/> px per em, from its measured text (all its lines).</summary>
        static Box RoleBox(LabelSpec s, float size, float unit)
        {
            bool hasText = !string.IsNullOrEmpty(s.Text);
            float width = s.TextEm;
            if (s.IconEm > 0) width += s.IconEm * IconGlyphFraction + (hasText ? IconGapEm : 0);
            if (s.Plate.a > 0) width += 2 * PlateMarginEm;
            float lines = Mathf.Max(s.TextLinesEm, LineEm);
            return Box.Of(s, width * size * unit, size, unit, lines, CapEm + 0.5f * (lines - LineEm));
        }

        /// <summary>Screen px that <paramref name="degrees"/> of the view take at a world point (a headset's eye at <paramref name="eye"/>).</summary>
        float DegreesPx(Vector3 world, Vector3 eye, float degrees) =>
            2f * Vector3.Distance(world, eye) * Mathf.Tan(0.5f * degrees * Mathf.Deg2Rad) * rig.PixelsPerUnit(world);

        void Add(LabelSpec s, Vector3 world, int place, Vector2 shift, float unit, float em, Rect line, Rect letters, float fade,
            bool leader, Vector2 from, Vector2 to, Vector2 point)
        {
            Rect r = new Rect(line.x - Padding, line.y - Padding, line.width + Padding * 2, line.height + Padding * 2);
            placedRects.Add(r);
            if (leader) leaders.Add((from, to));
            placed.Add(new Placed
            {
                Spec = s, World = world, Place = place, Shift = shift, Unit = unit, EmPx = em, Rect = r, Line = line,
                Letters = letters, Fade = fade, Leader = leader, From = from, To = to, Point = point
            });
        }

        /// <summary>The screen offset of a label's place k (0: its own, k: fallback k - 1), in pixels at 1080p.</summary>
        static Vector2 Offset(LabelSpec s, int place) =>
            place > 0 && s.Fallbacks != null && place <= s.Fallbacks.Length ? s.Fallbacks[place - 1] : s.PixelOffset;

        /// <summary>True when a rect overlaps a label already placed, or a leader crosses it.</summary>
        bool Taken(Rect r)
        {
            for (int j = 0; j < placedRects.Count; j++)
            {
                if (placedRects[j].Overlaps(r)) return true;
            }

            for (int j = 0; j < leaders.Count; j++)
            {
                if (SegmentMeets(leaders[j].from, leaders[j].to, r)) return true;
            }

            return false;
        }

        /// <summary>True when a label's letters (see <see cref="CapEm"/>) reach under one of the blocks.</summary>
        static bool Under(List<Rect> blocks, Rect letters)
        {
            for (int j = 0; j < blocks.Count; j++)
            {
                if (blocks[j].Overlaps(letters)) return true;
            }

            return false;
        }

        /// <summary>
        /// The leader from a moved label's point to its line box: from just off the point to just off the box's nearest
        /// edge; false when the point stands at the box (the label still covers it).
        /// </summary>
        static bool LeaderSegment(Vector2 point, Rect box, out Vector2 from, out Vector2 to)
        {
            Vector2 near = new Vector2(Mathf.Clamp(point.x, box.xMin, box.xMax), Mathf.Clamp(point.y, box.yMin, box.yMax));
            Vector2 d = near - point;
            float length = d.magnitude, gap = LeaderGapPx * UiScale;
            from = to = point;
            if (length <= 2 * gap + LeaderPx * UiScale) return false;
            Vector2 u = d / length;
            from = point + u * gap;
            to = near - u * gap;
            return true;
        }

        /// <summary>True when a leader would cross a label or a leader already placed (stacked names' leaders crossed the names below).</summary>
        bool LeaderCrosses(Vector2 from, Vector2 to)
        {
            for (int j = 0; j < placed.Count; j++)
            {
                if (SegmentMeets(from, to, placed[j].Line)) return true;
            }

            for (int j = 0; j < leaders.Count; j++)
            {
                if (SegmentsCross(from, to, leaders[j].from, leaders[j].to)) return true;
            }

            return false;
        }

        /// <summary>True when the segment from a to b meets a rect (Liang-Barsky clipping).</summary>
        static bool SegmentMeets(Vector2 a, Vector2 b, Rect r)
        {
            float t0 = 0, t1 = 1, dx = b.x - a.x, dy = b.y - a.y;
            return Clip(-dx, a.x - r.xMin, ref t0, ref t1) && Clip(dx, r.xMax - a.x, ref t0, ref t1) &&
                   Clip(-dy, a.y - r.yMin, ref t0, ref t1) && Clip(dy, r.yMax - a.y, ref t0, ref t1);
        }

        static bool Clip(float p, float q, ref float t0, ref float t1)
        {
            if (Mathf.Abs(p) < 1e-6f) return q >= 0;
            float t = q / p;
            if (p < 0)
            {
                if (t > t1) return false;
                if (t > t0) t0 = t;
            }
            else
            {
                if (t < t0) return false;
                if (t < t1) t1 = t;
            }

            return true;
        }

        /// <summary>True when two segments cross (touching ends do not count).</summary>
        static bool SegmentsCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float d1 = Cross(c, d, a), d2 = Cross(c, d, b), d3 = Cross(a, b, c), d4 = Cross(a, b, d);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }

        static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        /// <summary>
        /// True when a screen rect (pixels, as the camera projects) meets a label with a plate placed by the last layout: its
        /// plate with the padding the layout keeps around it, which also covers the error of its width estimate. A layer
        /// whose own world labels the label system does not place hides one there rather than leave it cut at the plate's
        /// edge, where a cut year reads as another number (the economy wave's decade years under the generation names). The
        /// layout runs in LateUpdate, so a layer's Tick sees the last frame's.
        /// </summary>
        public bool UnderPlate(Rect r)
        {
            for (int j = 0; j < placed.Count; j++)
            {
                if (placed[j].Spec.Plate.a > 0 && placed[j].Rect.Overlaps(r)) return true;
            }

            return false;
        }

        /// <summary>
        /// True when a screen rect (pixels, as the camera projects) meets a block that role labels keep out of, as the last
        /// frame measured them: the keep-out blocks, a headset's panels, the blocks that move labels aside and the HUD's.
        /// </summary>
        public bool UnderHud(Rect r) => Under(blockRects, r) || Under(worldRects, r) || Under(asideRects, r) || Under(hudRects, r);

        // ------------------------------------------------------------------ drawing

        void UpdateSlots()
        {
            foreach (Slot slot in slots) slot.Used = false;

            // keep existing assignments stable to avoid re-creating text meshes
            for (int i = 0; i < placed.Count; i++)
            {
                Placed p = placed[i];
                LabelSpec s = p.Spec;
                Slot slot = null;
                foreach (Slot candidate in slots)
                {
                    if (!candidate.Used && candidate.Spec == s)
                    {
                        slot = candidate;
                        break;
                    }
                }

                if (slot != null && slot.TextVersion != s.TextVersion)
                {
                    Assign(slot, s); // the text changed while shown
                }

                if (slot == null && s.Owner != null)
                {
                    // a group's label replaced by its twin (the same text, from its owner's next build) keeps the slot and its
                    // alpha: a layer that rebuilds every year would otherwise fade all its labels out and in again
                    foreach (Slot candidate in slots)
                    {
                        LabelSpec old = candidate.Spec;
                        if (candidate.Used || old == null || !old.Removed || !ReferenceEquals(old.Owner, s.Owner) || old.Text != s.Text) continue;
                        slot = candidate;
                        slot.Spec = s;
                        Assign(slot, s);
                        break;
                    }
                }

                if (slot == null)
                {
                    foreach (Slot candidate in slots)
                    {
                        if (!candidate.Used && (candidate.Spec == null || candidate.Alpha <= 0.01f))
                        {
                            slot = candidate;
                            break;
                        }
                    }

                    if (slot == null)
                    {
                        slot = CreateSlot();
                    }

                    if (slot.Spec != s)
                    {
                        slot.Spec = s;
                        slot.Alpha = 0;
                        Assign(slot, s);
                    }
                }

                slot.Used = true;
                Position(slot, p);
            }

            float dt = GraphTime.Delta;
            foreach (Slot slot in slots)
            {
                if (slot.Used) continue;
                slot.Alpha = Mathf.MoveTowards(slot.Alpha, 0, dt * 6);
                if (slot.Alpha <= 0.01f)
                {
                    if (slot.Tmp.gameObject.activeSelf) slot.Tmp.gameObject.SetActive(false); // hides the icon too
                    slot.Spec = null;
                }
                else
                {
                    SetColor(slot, slot.Fade); // keep the last fade: a label dropped for fading out must not flash brighter
                }
            }
        }

        /// <summary>
        /// Points a new or recycled slot at a spec: sets the text, shows, swaps or hides the icon, and arranges
        /// both around the anchor (the transform origin) in em. The icon always sits left of the text; Right and
        /// Center use the text's rendered width so the icon hugs it exactly. A role label draws with the halo.
        /// </summary>
        void Assign(Slot slot, LabelSpec s)
        {
            TextMeshPro tmp = slot.Tmp;
            if (!tmp.gameObject.activeSelf) tmp.gameObject.SetActive(true);
            // the font's own material, which the slot had before it ever showed a role label
            if (slot.Material == null) slot.Material = tmp.fontSharedMaterial;
            Material material = s.Role != LabelRole.None ? Halo(slot.Material) ?? slot.Material : slot.Material;
            if (s.OnTop) material = OnTopVariant(material);
            if (material != null && tmp.fontSharedMaterial != material) tmp.fontSharedMaterial = material;
            slot.OnTop = s.OnTop;
            tmp.text = s.Text ?? string.Empty;
            slot.TextVersion = s.TextVersion;
            Sprite sprite = s.IconEm > 0 ? Icons.GetSprite(s.Icon) : null;
            slot.IconShown = sprite != null;
            if (sprite == null)
            {
                if (slot.Icon != null && slot.Icon.enabled) slot.Icon.enabled = false;
                tmp.alignment = s.Align;
                tmp.rectTransform.pivot = s.Align == TextAlignmentOptions.Right ? new Vector2(1, 0.5f)
                    : s.Align == TextAlignmentOptions.Center ? new Vector2(0.5f, 0.5f)
                    : new Vector2(0, 0.5f);
                if (s.Plate.a > 0)
                {
                    float w = tmp.preferredWidth;
                    PlacePlate(slot, s.Align == TextAlignmentOptions.Right ? -w : s.Align == TextAlignmentOptions.Center ? -0.5f * w : 0, w);
                }
                else
                {
                    HidePlate(slot);
                }

                return;
            }

            if (slot.Icon == null) slot.Icon = CreateIcon(tmp.transform);
            slot.Icon.sprite = sprite;
            slot.Icon.color = Color.clear; // SetColor fades it in with the text
            if (!slot.Icon.enabled) slot.Icon.enabled = true;

            bool hasText = !string.IsNullOrEmpty(s.Text);
            float glyph = s.IconEm * IconGlyphFraction;
            float lead = glyph + (hasText ? IconGapEm : 0); // icon and gap before the text
            float textEm = hasText && s.Align != TextAlignmentOptions.Left ? tmp.preferredWidth : 0;
            float left = s.Align == TextAlignmentOptions.Right ? -(lead + textEm)
                : s.Align == TextAlignmentOptions.Center ? -0.5f * (lead + textEm)
                : 0;

            // the text starts at left + lead: a left-aligned rect whose pivot sits that far to its left
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.rectTransform.pivot = new Vector2(-(left + lead) / TextRectEm, 0.5f);
            Transform icon = slot.Icon.transform;
            icon.localPosition = new Vector3(left + 0.5f * glyph, 0, 0);
            icon.localScale = new Vector3(s.IconEm, s.IconEm, s.IconEm); // the sprite is one unit square
            if (s.Plate.a > 0) PlacePlate(slot, left, lead + (hasText ? tmp.preferredWidth : 0));
            else HidePlate(slot);
        }

        /// <summary>
        /// Shows the slot's plate behind the group that starts at <paramref name="left"/> and is <paramref name="width"/> em
        /// wide, with a margin, as tall as the text's lines. Drawn one sorting order below the text, so the text and icon
        /// stay on top of it (a TextMeshPro &lt;mark&gt; draws its highlight over the glyphs).
        /// </summary>
        void PlacePlate(Slot slot, float left, float width)
        {
            if (slot.Plate == null) slot.Plate = CreatePlate(slot.Tmp.transform);
            if (!slot.Plate.enabled) slot.Plate.enabled = true;
            Material plateMaterial = slot.OnTop ? OnTopSprites : Icons.Material;
            if (plateMaterial != null && slot.Plate.sharedMaterial != plateMaterial) slot.Plate.sharedMaterial = plateMaterial;
            slot.PlateShown = true;
            slot.Plate.color = Color.clear; // SetColor fades it in with the text
            float height = Mathf.Max(slot.Tmp.preferredHeight, LineEm) + 2 * PlateMarginYEm;
            slot.Plate.size = new Vector2(width + 2 * PlateMarginEm, height);
            slot.Plate.transform.localPosition = new Vector3(left + 0.5f * width, 0, 0);
        }

        static void HidePlate(Slot slot)
        {
            slot.PlateShown = false;
            if (slot.Plate != null && slot.Plate.enabled) slot.Plate.enabled = false;
        }

        void Position(Slot slot, in Placed p)
        {
            if (!slot.Tmp.gameObject.activeSelf) slot.Tmp.gameObject.SetActive(true);
            Transform camT = rig.transform;
            float ppu = rig.PixelsPerUnit(p.World);
            if (ppu <= 0) return;
            LabelSpec s = slot.Spec;
            bool role = s.Role != LabelRole.None;
            // its own offset or fallback as the layer has it now (a layer may move it between layouts), and a candidate's move
            int own = 1 + (s.Fallbacks?.Length ?? 0);
            Vector2 pixels = Offset(s, p.Place < own ? p.Place : 0);
            Vector3 offset = (camT.right * pixels.x + camT.up * pixels.y) * (role ? p.Unit : UiScale) / ppu;
            if (p.Place >= own) offset += (camT.right * p.Shift.x + camT.up * p.Shift.y) / ppu;
            Transform t = slot.Tmp.transform;
            t.SetPositionAndRotation(p.World + offset, camT.rotation);
            float scale = (role ? p.EmPx : s.SizePx * UiScale) / ppu;
            t.localScale = new Vector3(scale, scale, scale);
            if (p.Leader) ShowLeader(slot, p, scale * ppu);
            else HideLeader(slot);
            slot.Alpha = Mathf.MoveTowards(slot.Alpha, 1, GraphTime.Delta * 5);
            slot.Fade = p.Fade;
            SetColor(slot, p.Fade);
        }

        /// <summary>
        /// Shows the slot's leader: a line <see cref="LeaderPx"/> wide in the label's own plane (one local unit is one em,
        /// <paramref name="em"/> screen px), from the label's point to the label, so it moves and fades with it.
        /// </summary>
        void ShowLeader(Slot slot, in Placed p, float em)
        {
            if (em <= 0) return;
            if (slot.Leader == null) slot.Leader = CreateLeader(slot.Tmp.transform);
            if (!slot.Leader.enabled) slot.Leader.enabled = true;
            Material leaderMaterial = slot.OnTop ? OnTopSprites : Icons.Material;
            if (leaderMaterial != null && slot.Leader.sharedMaterial != leaderMaterial) slot.Leader.sharedMaterial = leaderMaterial;
            slot.LeaderShown = true;
            Vector2 a = (p.From - p.Point) / em, b = (p.To - p.Point) / em, d = b - a;
            Transform t = slot.Leader.transform;
            t.localPosition = new Vector3(0.5f * (a.x + b.x), 0.5f * (a.y + b.y), 0);
            t.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            t.localScale = new Vector3(d.magnitude, LeaderPx * UiScale / em, 1);
        }

        static void HideLeader(Slot slot)
        {
            slot.LeaderShown = false;
            if (slot.Leader != null && slot.Leader.enabled) slot.Leader.enabled = false;
        }

        void SetColor(Slot slot, float fade)
        {
            LabelSpec s = slot.Spec;
            if (s == null) return;
            // an undimmed label stays out of the tour's dimming only: a viewer's own selection (a clicked label, a followed
            // figure) dims it with the rest, or it would read as part of the selection
            bool dimmed = Highlighter.HasHighlight && !(s.Undimmed && Touring);
            float boost = IsHighlighted(s) ? 1f : dimmed ? 0.45f : 0.85f;
            // the pointer lifts its label only outside the tour: the HUD gives it no tooltip or click then, and a label
            // under a resting pointer lifted to full brightness read as the director's pick
            if (Hovered == s && !Touring) boost = 1f;
            // a label bound to a part fades with it (a removed part's row stays, so the index holds)
            float part = s.PartIndex >= 0 && s.PartIndex < partAlpha.Count ? partAlpha[s.PartIndex] : 1f;
            bool role = s.Role != LabelRole.None;
            Color c = role ? Ldr(s.Color) : s.Color;
            c.a *= slot.Alpha * fade * boost * part;
            if (slot.Tmp.color != c) slot.Tmp.color = c;
            if (slot.IconShown && slot.Icon.color != c) slot.Icon.color = c;
            if (slot.PlateShown)
            {
                // the plate fades in and out with its label but does not dim with it: a dimmed label over bright marks
                // still needs the dark behind it (dimmed with the text, it let the lines through the letters)
                Color p = role ? Ldr(s.Plate) : s.Plate;
                p.a *= slot.Alpha * fade * part;
                if (slot.Plate.color != p) slot.Plate.color = p;
            }

            if (slot.LeaderShown)
            {
                Color l = Ldr(LeaderColor);
                l.a *= slot.Alpha * fade * boost * part;
                if (slot.Leader.color != l) slot.Leader.color = l;
            }
        }

        /// <summary>A colour at most 1 in every channel: a role label stays under the hills' bloom threshold (1.1).</summary>
        static Color Ldr(Color c) => new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), Mathf.Clamp01(c.a));

        /// <summary>
        /// True while the guided tour runs: its director owns the highlight then (the HUD takes no clicks, and a followed
        /// figure is let go).
        /// </summary>
        static bool Touring => GraphRoot.Instance != null && GraphRoot.Instance.TourActive;

        static bool IsHighlighted(LabelSpec s)
        {
            if (s.Ids.IsEmpty || !Highlighter.HasHighlight) return false;
            return Highlighter.IsHighlighted(s.Ids);
        }

        Slot CreateSlot()
        {
            GameObject go = new GameObject("Label");
            go.transform.SetParent(transform, false);
            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = 10; // ~1 world unit per line; scaled to pixels in Position()
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.rectTransform.sizeDelta = new Vector2(TextRectEm, 2);
            tmp.renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tmp.renderer.receiveShadows = false;
            tmp.sortingOrder = SortingOrder;
            Slot slot = new Slot { Tmp = tmp, Material = tmp.fontSharedMaterial };
            slots.Add(slot);
            return slot;
        }

        /// <summary>
        /// The role labels' material (main thread, made once): the font's own, with a black underlay at 0.7 alpha
        /// (<see cref="HaloDilate"/>) under its letters. It carries the keywords of TextMeshPro's drop shadow preset (outline
        /// and underlay, the outline at width 0), the variant a build always holds: an underlay alone may be stripped. Null
        /// when the font's shader has no underlay (role labels then draw without a halo; reported once).
        /// </summary>
        /// <summary>Always-on-top copies of the label materials (<see cref="LabelSpec.OnTop"/>), made once per base material:
        /// the same material with its depth test set to Always, as a screen-space canvas sets it for its UI.</summary>
        readonly Dictionary<Material, Material> onTop = new Dictionary<Material, Material>();

        Material OnTopVariant(Material m)
        {
            if (m == null) return null;
            if (!onTop.TryGetValue(m, out Material v))
            {
                v = new Material(m) { name = m.name + " (on top)" };
                v.SetInt(GuiZTestId, (int)UnityEngine.Rendering.CompareFunction.Always);
                onTop[m] = v;
            }

            return v;
        }

        static readonly int GuiZTestId = Shader.PropertyToID("unity_GUIZTestMode");

        /// <summary>The plates and leaders of on-top labels: UI/Default (it reads the depth test from the material, where
        /// Sprites/Default always tests), alpha blended like the icons' material.</summary>
        Material OnTopSprites
        {
            get
            {
                if (onTopSprites != null || onTopSpritesTried) return onTopSprites;
                onTopSpritesTried = true;
                Shader shader = Shader.Find("UI/Default");
                if (shader == null) return null;
                onTopSprites = new Material(shader) { name = "Label plates (on top)" };
                onTopSprites.SetInt(GuiZTestId, (int)UnityEngine.Rendering.CompareFunction.Always);
                return onTopSprites;
            }
        }

        Material onTopSprites;
        bool onTopSpritesTried;

        Material Halo(Material font)
        {
            if (halo != null || haloTried || font == null) return halo;
            haloTried = true;
            if (!font.HasProperty(UnderlayColorId) || !font.HasProperty(UnderlayDilateId))
            {
                Debug.LogWarning($"[Why] labels: the labels' font material '{font.name}' has no underlay, so role labels draw without a halo");
                return null;
            }

            halo = new Material(font) { name = font.name + " (label halo)" };
            foreach (string keyword in HaloKeywords) halo.EnableKeyword(keyword);
            halo.SetFloat(OutlineWidthId, 0);
            halo.SetColor(UnderlayColorId, HaloColor);
            halo.SetFloat(UnderlayXId, 0);
            halo.SetFloat(UnderlayYId, 0);
            halo.SetFloat(UnderlayDilateId, HaloDilate);
            halo.SetFloat(UnderlaySoftnessId, 0);
            ShaderUtilities.UpdateShaderRatios(halo);
            return halo;
        }

        /// <summary>
        /// A slot's icon renderer: a child of the text, so it inherits the billboard rotation and the em scale
        /// (<see cref="Assign"/> sizes and places it). Unlit, alpha blended, no depth write (see
        /// <see cref="Icons"/>), sorted with the text.
        /// </summary>
        static SpriteRenderer CreateIcon(Transform text)
        {
            GameObject go = new GameObject("Icon");
            go.transform.SetParent(text, false);
            SpriteRenderer icon = go.AddComponent<SpriteRenderer>();
            Material material = Icons.Material;
            if (material != null) icon.sharedMaterial = material;
            icon.sortingOrder = SortingOrder;
            icon.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            icon.receiveShadows = false;
            return icon;
        }

        /// <summary>
        /// A slot's plate: a 9-sliced rounded rectangle child of the text (one local unit = one em), alpha blended like the
        /// icons, one sorting order below the text so it is drawn first.
        /// </summary>
        static SpriteRenderer CreatePlate(Transform text)
        {
            GameObject go = new GameObject("Plate");
            go.transform.SetParent(text, false);
            SpriteRenderer plate = go.AddComponent<SpriteRenderer>();
            Material material = Icons.Material;
            if (material != null) plate.sharedMaterial = material;
            plate.sprite = UiFactory.Rounded;
            plate.drawMode = SpriteDrawMode.Sliced;
            plate.sortingOrder = SortingOrder - 1;
            plate.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            plate.receiveShadows = false;
            return plate;
        }

        /// <summary>
        /// A slot's leader: a one-unit white square child of the text, stretched and turned by <see cref="ShowLeader"/>, alpha
        /// blended like the icons, under the text with the plates.
        /// </summary>
        static SpriteRenderer CreateLeader(Transform text)
        {
            if (line == null)
            {
                Texture2D white = Texture2D.whiteTexture;
                line = Sprite.Create(white, new Rect(0, 0, white.width, white.height), new Vector2(0.5f, 0.5f), white.width);
                line.name = "WhyLeader";
            }

            GameObject go = new GameObject("Leader");
            go.transform.SetParent(text, false);
            SpriteRenderer leader = go.AddComponent<SpriteRenderer>();
            Material material = Icons.Material;
            if (material != null) leader.sharedMaterial = material;
            leader.sprite = line;
            leader.sortingOrder = SortingOrder - 1;
            leader.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            leader.receiveShadows = false;
            return leader;
        }

        // ------------------------------------------------------------------ pointer

        void UpdateHover()
        {
            Hovered = null;
            if (!PointerInput.Available) return;
            Vector2 m = PointerInput.Position;
            for (int i = 0; i < placed.Count; i++)
            {
                if (placed[i].Rect.Contains(m))
                {
                    Hovered = placed[i].Spec;
                    return;
                }
            }
        }

        /// <summary>
        /// A label with an action (<see cref="LabelSpec.OnClick"/>) runs it when pressed and released over it without a drag
        /// (a mouse click, or a pinch of the hand ray in a headset: <see cref="PointerInput"/>), outside the tour and not
        /// through a panel. The HUD leaves such a label to it (it does not focus it).
        /// </summary>
        void UpdateClick()
        {
            if (!PointerInput.Available)
            {
                pressed = null;
                return;
            }

            Vector2 m = PointerInput.Position;
            if (PointerInput.PressedThisFrame)
            {
                bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                pressed = Hovered != null && Hovered.OnClick != null && !Touring && !overUi ? Hovered : null;
                pressedAt = m;
            }

            if (!PointerInput.ReleasedThisFrame || pressed == null) return;
            LabelSpec s = pressed;
            pressed = null;
            float slop = ClickSlopPx * UiScale;
            if (Hovered == s && (m - pressedAt).sqrMagnitude <= slop * slop) Run(s);
        }

        /// <summary>
        /// Main thread: runs the action of the label with one placed at a screen point (pixels, as the camera projects), as a
        /// click there would; false when no label with an action stands there. For tests and for paths that are not the
        /// pointer's.
        /// </summary>
        public bool ClickAt(Vector2 screen)
        {
            for (int i = 0; i < placed.Count; i++)
            {
                if (!placed[i].Rect.Contains(screen)) continue;
                LabelSpec s = placed[i].Spec;
                if (s.OnClick == null) return false;
                Run(s);
                return true;
            }

            return false;
        }

        static void Run(LabelSpec s)
        {
            try
            {
                s.OnClick();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Why] labels: the action of '{s.Text}' failed: {e}");
            }
        }

        // ------------------------------------------------------------------ metrics

        /// <summary>
        /// Main thread: the labels the last layout placed, as the smoke test measures them (spec 3.5's metrics), one line
        /// without its prefix: "placed N, culled M, hidden H, moved K (L leaders), overlaps 0, min 12.0 px, max 18.0 px, under
        /// HUD 0" for the role labels (sizes in px at 1080p, portrait's zoom included; in a headset also the least angle of a
        /// label's em), then the other labels placed and how many of them stand under the HUD's blocks (they keep out of the
        /// keep-out blocks only). The role labels' problems go to <paramref name="problems"/>: an overlap (letters on letters,
        /// or a leader through a label), a label under a block, a size outside 12-22 px, or under its degrees in a headset.
        /// </summary>
        internal string Metrics(List<string> problems)
        {
            Camera cam = rig != null ? rig.Cam : null;
            Vector3 eye = cam != null ? cam.transform.position : Vector3.zero;
            bool xr = Xr.XrSession.Active;
            float zoom = ScreenLayout.LabelScale / Mathf.Max(ScreenLayout.UiScale, 1e-4f);
            int n = 0, others = 0, othersUnder = 0, moved = 0, leadersShown = 0, overlaps = 0, under = 0, small = 0, large = 0, narrow = 0;
            float min = float.MaxValue, max = 0, minDegrees = float.MaxValue;
            for (int i = 0; i < placed.Count; i++)
            {
                Placed p = placed[i];
                if (p.Spec.Role == LabelRole.None)
                {
                    others++;
                    if (Under(hudRects, p.Letters)) othersUnder++;
                    continue;
                }

                n++;
                float px = p.EmPx / Mathf.Max(ScreenLayout.UiScale, 1e-4f);
                min = Mathf.Min(min, px);
                max = Mathf.Max(max, px);
                if (p.Place > (p.Spec.Fallbacks?.Length ?? 0)) moved++;
                if (p.Leader) leadersShown++;
                if (UnderHud(p.Letters)) under++;
                if (!xr && px < MinRolePx - 0.05f) small++;
                if (!xr && px > MaxRolePx * zoom + 0.05f) large++;
                if (xr && cam != null)
                {
                    float ppu = rig.PixelsPerUnit(p.World), distance = Vector3.Distance(p.World, eye);
                    float degrees = ppu > 0 && distance > 0 ? 2f * Mathf.Atan(0.5f * p.EmPx / ppu / distance) * Mathf.Rad2Deg : 0;
                    minDegrees = Mathf.Min(minDegrees, degrees);
                    if (degrees < RoleDegreesOf(p.Spec.Role) * 0.995f) narrow++;
                }

                // each pair once: a later label, or an earlier one that is not a role label (a role label's pairs are its own)
                for (int j = 0; j < placed.Count; j++)
                {
                    if (j == i || (j < i && placed[j].Spec.Role != LabelRole.None)) continue;
                    if (Meet(p.Letters, placed[j].Letters)) overlaps++;
                }

                if (!p.Leader) continue;
                for (int j = 0; j < placed.Count; j++)
                {
                    if (j != i && SegmentMeets(p.From, p.To, placed[j].Letters)) overlaps++;
                }
            }

            string line = $"placed {n}, culled {roleCulled}, hidden {roleHidden}, moved {moved} ({leadersShown} leaders), overlaps {overlaps}, " +
                          (n > 0 ? $"min {min.ToString("0.0", Ci)} px, max {max.ToString("0.0", Ci)} px, " : "min - px, max - px, ") +
                          (xr ? n > 0 ? $"min {minDegrees.ToString("0.00", Ci)} deg, " : "min - deg, " : "") +
                          $"under HUD {under}; other labels: {others} placed, {othersUnder} under the HUD's panels";
            if (overlaps > 0) problems.Add($"{overlaps} overlaps: role labels' letters on another label's, or a leader through a label");
            if (under > 0) problems.Add($"{under} role labels stand under a HUD block or a keep-out block");
            if (small > 0) problems.Add($"{small} role labels are under {MinRolePx.ToString("0", Ci)} px (smallest {min.ToString("0.0", Ci)} px)");
            if (large > 0) problems.Add($"{large} role labels are over {(MaxRolePx * zoom).ToString("0", Ci)} px (largest {max.ToString("0.0", Ci)} px)");
            if (narrow > 0) problems.Add($"{narrow} role labels take less than their degrees of the view in the headset (least {minDegrees.ToString("0.00", Ci)})");
            return line;
        }

        static readonly System.Globalization.CultureInfo Ci = System.Globalization.CultureInfo.InvariantCulture;

        /// <summary>True when two letter boxes share more than a hair (half a pixel each way).</summary>
        static bool Meet(Rect a, Rect b) =>
            a.xMin < b.xMax - 0.5f && b.xMin < a.xMax - 0.5f && a.yMin < b.yMax - 0.5f && b.yMin < a.yMax - 0.5f;

        /// <summary>Main thread: each label the last layout placed, with its letters' box (screen px) and its em (px), for tests.</summary>
        public void Shown(List<(LabelSpec spec, Rect letters, float em)> into)
        {
            for (int i = 0; i < placed.Count; i++) into.Add((placed[i].Spec, placed[i].Letters, placed[i].EmPx));
        }

        /// <summary>Owners compare by reference (a destroyed layer is still its own group; nothing native is asked off the main thread).</summary>
        sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            public new bool Equals(object a, object b) => ReferenceEquals(a, b);

            public int GetHashCode(object o) => RuntimeHelpers.GetHashCode(o);
        }
    }
}

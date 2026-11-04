using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Why
{
    /// <summary>A text label pinned to a data-space point, optionally with an icon left of the text.</summary>
    public sealed class LabelSpec
    {
        public string Text;               // TextMeshPro rich text allowed; may be empty when Icon is set
        public Vector3 Data;              // (u, y, rho)
        public float Priority = 1;        // higher wins when labels collide
        public float Rank;                // breaks ties between equal priorities (higher first), e.g. a figure's prominence
        public float SizePx = 13;         // on-screen text height
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

        internal float Width;             // estimated width in px at SizePx, icon included
        internal int TextVersion;         // bumped by LabelSystem.SetText so a shown label re-reads its text
        internal float IconEm;            // icon quad size in em, 0 without a known icon (resolved when the label system takes the spec)
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

        sealed class Slot
        {
            public TextMeshPro Tmp;
            public SpriteRenderer Icon;   // created the first time the slot shows an icon, then reused
            public bool IconShown;
            public SpriteRenderer Plate;  // created the first time the slot shows a plate, then reused
            public bool PlateShown;
            public LabelSpec Spec;
            public int TextVersion;       // the spec's TextVersion this slot shows
            public float Alpha;
            public float Fade = 1;        // last focus/handoff fade while placed, kept while fading out
            public bool Used;
        }

        readonly List<LabelSpec> specs = new List<LabelSpec>();
        readonly List<LabelSpec> pending = new List<LabelSpec>();
        readonly object gate = new object();
        readonly List<Slot> slots = new List<Slot>();
        readonly List<Rect> placedRects = new List<Rect>();

        /// <summary>The labels placed by the last layout; place 0 is the spec's own offset, place k its fallback k - 1.</summary>
        readonly List<(LabelSpec spec, Vector3 world, int place, Rect rect, float fade)> placed =
            new List<(LabelSpec, Vector3, int, Rect, float)>();

        /// <summary>UI blocks no label goes under (see <see cref="KeepClear"/>), destroyed ones pruned as they are met.</summary>
        static readonly List<RectTransform> clearBlocks = new List<RectTransform>();

        static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>Screen rects of the blocks shown at the last layout, and this frame's measure of them.</summary>
        readonly List<Rect> blockRects = new List<Rect>(), measuredBlocks = new List<Rect>();

        CameraRig rig;
        int lastCam = -1, lastWarp = -1, lastHighlightHash, frame;
        bool dirty = true, dataLabelsHidden;

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

        public void Init(CameraRig cameraRig) => rig = cameraRig;

        /// <summary>
        /// Thread safe: queue a label (layers call this from Prepare). A label needs text, an icon, or both;
        /// the first icon label starts rasterizing the icon atlas in the background.
        /// </summary>
        public void Add(LabelSpec spec)
        {
            if (spec == null) return;
            bool icon = Icons.Has(spec.Icon);
            if (!icon && !string.IsNullOrEmpty(spec.Icon)) Debug.LogWarning($"Label '{spec.Text}': unknown icon '{spec.Icon}'.");
            if (!icon && string.IsNullOrEmpty(spec.Text)) return;
            if (icon) Icons.Prepare();
            lock (gate) pending.Add(spec);
        }

        public void MarkDirty() => dirty = true;

        /// <summary>
        /// Main thread: keeps labels out from under a UI block (the HUD's legend and preset bar, the tour's card). Its owner
        /// registers it once; while it is shown its screen rect is measured every frame, so labels re-place when it moves,
        /// appears or goes. Only blocks on a screen-space overlay canvas count: in a headset the panels hang in the world.
        /// </summary>
        public static void KeepClear(RectTransform block)
        {
            if (block != null && !clearBlocks.Contains(block)) clearBlocks.Add(block);
        }

        /// <summary>
        /// Measures the blocks shown now into <see cref="blockRects"/> (screen pixels, as the camera projects; one standing
        /// within <see cref="EdgeStripPx"/> of a screen edge reaches to it); true when one moved, appeared or went since the
        /// last measure.
        /// </summary>
        bool MeasureBlocks()
        {
            measuredBlocks.Clear();
            float strip = EdgeStripPx * ScreenLayout.UiScale, w = rig.Cam.pixelWidth, h = rig.Cam.pixelHeight;
            for (int i = clearBlocks.Count - 1; i >= 0; i--)
            {
                RectTransform block = clearBlocks[i];
                if (block == null)
                {
                    clearBlocks.RemoveAt(i); // destroyed with its scene
                    continue;
                }

                if (!block.gameObject.activeInHierarchy) continue;
                Canvas canvas = block.GetComponentInParent<Canvas>();
                Canvas screen = canvas != null ? canvas.rootCanvas : null;
                if (screen == null || !screen.enabled || screen.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                block.GetWorldCorners(Corners); // an overlay canvas's world corners are screen pixels
                Rect r = Rect.MinMaxRect(Corners[0].x, Corners[0].y, Corners[2].x, Corners[2].y);
                if (r.xMin < strip) r.xMin = 0;
                if (r.yMin < strip) r.yMin = 0;
                if (r.xMax > w - strip) r.xMax = w;
                if (r.yMax > h - strip) r.yMax = h;
                measuredBlocks.Add(r);
            }

            bool same = measuredBlocks.Count == blockRects.Count;
            for (int i = 0; same && i < measuredBlocks.Count; i++)
            {
                Rect a = measuredBlocks[i], b = blockRects[i];
                same = Mathf.Abs(a.xMin - b.xMin) < 0.5f && Mathf.Abs(a.yMin - b.yMin) < 0.5f &&
                       Mathf.Abs(a.xMax - b.xMax) < 0.5f && Mathf.Abs(a.yMax - b.yMax) < 0.5f;
            }

            if (same) return false;
            blockRects.Clear();
            blockRects.AddRange(measuredBlocks);
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
            if (spec.IconEm > 0) em += spec.IconEm * IconGlyphFraction + (hasText ? IconGapEm : 0);
            if (spec.Plate.a > 0) em += 2 * PlateMarginEm;
            spec.Width = em * spec.SizePx;
            spec.TextVersion++;
            dirty = true;
        }

        void FlushPending()
        {
            lock (gate)
            {
                if (pending.Count == 0) return;
                foreach (LabelSpec s in pending)
                {
                    bool hasText = !string.IsNullOrEmpty(s.Text);
                    float em = EstimateWidth(s.Text);
                    s.IconEm = !Icons.Has(s.Icon) ? 0 : hasText ? IconEm : IconOnlyEm;
                    if (s.IconEm > 0) em += s.IconEm * IconGlyphFraction + (hasText ? IconGapEm : 0);
                    if (s.Plate.a > 0) em += 2 * PlateMarginEm;
                    s.Width = em * s.SizePx;
                    specs.Add(s);
                }

                pending.Clear();
            }

            specs.Sort(ComparePlacementOrder);
            dirty = true;
        }

        /// <summary>
        /// Placement order: higher priority first, then higher rank; remaining ties broken by text and then position, so
        /// the same graph always shows the same labels however the layers' worker threads happened to interleave their
        /// Add calls (screenshots and recorded videos are reproducible).
        /// </summary>
        static int ComparePlacementOrder(LabelSpec a, LabelSpec b)
        {
            int c = b.Priority.CompareTo(a.Priority);
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

        /// <summary>
        /// Width in em of the visible characters. Rich-text tags such as &lt;sup&gt; are skipped; characters
        /// inside &lt;sup&gt; or &lt;sub&gt; count at script size, and inside &lt;size=NN%&gt; at that size.
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

        void LateUpdate()
        {
            if (rig == null) return;
            FlushPending();
            frame++;

            int hiHash = Highlighter.HasHighlight ? frame / 20 : 0;
            bool blocksMoved = MeasureBlocks();
            bool changed = dirty || blocksMoved || rig.Version != lastCam || GraphWarp.Version != lastWarp ||
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
        }

        void Layout()
        {
            Camera cam = rig.Cam;
            placed.Clear();
            placedRects.Clear();
            WarpState warp = GraphWarp.Current;
            float w = cam.pixelWidth, h = cam.pixelHeight;

            for (int i = 0; i < specs.Count && placed.Count < MaxVisible; i++)
            {
                LabelSpec s = specs[i];
                if (s.Hidden || (dataLabelsHidden && !s.Fixed)) continue;
                if (s.Fixed && s.FixedRange > 0 && (rig.Pose.Target - s.Data).sqrMagnitude > s.FixedRange * s.FixedRange) continue;
                float fade = s.Fixed ? 1f : GraphWarp.FocusFade(s.Data.x, warp);
                if (s.HandoffFade && !s.Fixed) fade *= GraphStyle.HandoffFade(s.Data.x);
                if (fade < 0.35f) continue;

                Vector3 world = s.Fixed ? s.Data : GraphWarp.ToWorld(s.Data.x, s.Data.y, s.Data.z, warp);
                Vector3 sp = cam.WorldToScreenPoint(world);
                if (sp.z <= cam.nearClipPlane) continue;

                // Width covers icon, gap and text, so the alignment places the whole group (see Assign), and a plate's
                // margins, which reach past the group on both sides
                float lw = s.Width * UiScale, lh = s.SizePx * UiScale * Mathf.Max(LineEm, s.IconEm * IconGlyphFraction);
                float margin = s.Plate.a > 0 ? PlateMarginEm * s.SizePx * UiScale : 0;
                float left = s.Align == TextAlignmentOptions.Right ? margin - lw
                    : s.Align == TextAlignmentOptions.Center ? -lw * 0.5f
                    : -margin;

                // what a block keeps out: the letters and icon, a slack inside their outline, not the padded line box (a
                // date right above the readout reads whole; only labels keep the padding between them)
                float slack = BlockSlackPx * UiScale;
                float half = Mathf.Max(0, s.SizePx * UiScale * Mathf.Max(CapEm, 0.5f * s.IconEm * IconGlyphFraction) - slack);
                float x0 = left + margin + slack, x1 = left + lw - margin - slack;

                // the label's own offset, then its fallbacks: the first place free and with the text and icon wholly on
                // screen (a plate may run to the frame's edge; the padding only spaces labels apart)
                int places = 1 + (s.Fallbacks?.Length ?? 0);
                for (int k = 0; k < places; k++)
                {
                    Vector2 offset = Offset(s, k);
                    float x = sp.x + offset.x * UiScale, y = sp.y + offset.y * UiScale;
                    if (x + left + margin < 0 || x + left + lw - margin > w || y - lh * 0.5f < 0 || y + lh * 0.5f > h) continue;
                    Rect r = new Rect(x + left - Padding, y - lh * 0.5f - Padding, lw + Padding * 2, lh + Padding * 2);
                    if (Taken(r) || Blocked(Rect.MinMaxRect(x + x0, y - half, x + x1, y + half))) continue;
                    placedRects.Add(r);
                    placed.Add((s, world, k, r, fade));
                    break;
                }
            }
        }

        /// <summary>The screen offset of a label's place k (0: its own, k: fallback k - 1), in pixels at 1080p.</summary>
        static Vector2 Offset(LabelSpec s, int place) =>
            place > 0 && s.Fallbacks != null && place <= s.Fallbacks.Length ? s.Fallbacks[place - 1] : s.PixelOffset;

        /// <summary>True when a rect overlaps a label already placed.</summary>
        bool Taken(Rect r)
        {
            for (int j = 0; j < placedRects.Count; j++)
            {
                if (placedRects[j].Overlaps(r)) return true;
            }

            return false;
        }

        /// <summary>True when a label's letters (see <see cref="CapEm"/>) reach under a block that keeps labels out.</summary>
        bool Blocked(Rect letters)
        {
            for (int j = 0; j < blockRects.Count; j++)
            {
                if (blockRects[j].Overlaps(letters)) return true;
            }

            return false;
        }

        void UpdateSlots()
        {
            foreach (Slot slot in slots) slot.Used = false;

            // keep existing assignments stable to avoid re-creating text meshes
            for (int i = 0; i < placed.Count; i++)
            {
                LabelSpec s = placed[i].spec;
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
                Position(slot, placed[i].world, placed[i].place, placed[i].fade);
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
        /// Center use the text's rendered width so the icon hugs it exactly.
        /// </summary>
        void Assign(Slot slot, LabelSpec s)
        {
            TextMeshPro tmp = slot.Tmp;
            if (!tmp.gameObject.activeSelf) tmp.gameObject.SetActive(true);
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

        void Position(Slot slot, Vector3 world, int place, float fade)
        {
            if (!slot.Tmp.gameObject.activeSelf) slot.Tmp.gameObject.SetActive(true);
            Transform camT = rig.transform;
            float ppu = rig.PixelsPerUnit(world);
            if (ppu <= 0) return;
            LabelSpec s = slot.Spec;
            Vector2 pixels = Offset(s, place);
            Vector3 offset = (camT.right * pixels.x + camT.up * pixels.y) * UiScale / ppu;
            Transform t = slot.Tmp.transform;
            t.SetPositionAndRotation(world + offset, camT.rotation);
            float scale = s.SizePx * UiScale / ppu;
            t.localScale = new Vector3(scale, scale, scale);
            slot.Alpha = Mathf.MoveTowards(slot.Alpha, 1, GraphTime.Delta * 5);
            slot.Fade = fade;
            SetColor(slot, fade);
        }

        void SetColor(Slot slot, float fade)
        {
            LabelSpec s = slot.Spec;
            if (s == null) return;
            // an undimmed label stays out of the tour's dimming only: a viewer's own selection (a clicked label, a followed
            // figure) dims it with the rest, or it would read as part of the selection
            bool dimmed = Highlighter.HasHighlight && !(s.Undimmed && Touring);
            float boost = IsHighlighted(s) ? 1f : dimmed ? 0.45f : 0.85f;
            if (Hovered == s) boost = 1f;
            Color c = s.Color;
            c.a *= slot.Alpha * fade * boost;
            if (slot.Tmp.color != c) slot.Tmp.color = c;
            if (slot.IconShown && slot.Icon.color != c) slot.Icon.color = c;
            if (slot.PlateShown)
            {
                // the plate fades in and out with its label but does not dim with it: a dimmed label over bright marks
                // still needs the dark behind it (dimmed with the text, it let the lines through the letters)
                Color p = s.Plate;
                p.a *= slot.Alpha * fade;
                if (slot.Plate.color != p) slot.Plate.color = p;
            }
        }

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
            Slot slot = new Slot { Tmp = tmp };
            slots.Add(slot);
            return slot;
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

        void UpdateHover()
        {
            Hovered = null;
            if (!PointerInput.Available) return;
            Vector2 m = PointerInput.Position;
            for (int i = 0; i < placed.Count; i++)
            {
                if (placed[i].rect.Contains(m))
                {
                    Hovered = placed[i].spec;
                    return;
                }
            }
        }
    }
}

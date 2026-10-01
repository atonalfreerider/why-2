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

        internal float Width;             // estimated width in px at SizePx, icon included
        internal int TextVersion;         // bumped by LabelSystem.SetText so a shown label re-reads its text
        internal float IconEm;            // icon quad size in em, 0 without a known icon (resolved when the label system takes the spec)
    }

    /// <summary>
    /// Places labels every frame the view changes: projects all specs, then greedily keeps the
    /// highest-priority labels that do not overlap on screen. Zooming in spreads points apart, so more
    /// labels fit - level of detail for free. Labels are billboards with constant pixel size.
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

        sealed class Slot
        {
            public TextMeshPro Tmp;
            public SpriteRenderer Icon;   // created the first time the slot shows an icon, then reused
            public bool IconShown;
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
        readonly List<(LabelSpec spec, Vector3 world, Rect rect, float fade)> placed =
            new List<(LabelSpec, Vector3, Rect, float)>();

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
                    s.Width = em * s.SizePx;
                    specs.Add(s);
                }

                pending.Clear();
            }

            specs.Sort(ComparePlacementOrder);
            dirty = true;
        }

        /// <summary>
        /// Placement order: higher priority first; ties broken by text and then position, so the same graph always
        /// shows the same labels however the layers' worker threads happened to interleave their Add calls (screenshots
        /// and recorded videos are reproducible).
        /// </summary>
        static int ComparePlacementOrder(LabelSpec a, LabelSpec b)
        {
            int c = b.Priority.CompareTo(a.Priority);
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
            bool changed = dirty || rig.Version != lastCam || GraphWarp.Version != lastWarp || hiHash != lastHighlightHash;
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
            float w = Screen.width, h = Screen.height;

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
                sp.x += s.PixelOffset.x * UiScale;
                sp.y += s.PixelOffset.y * UiScale;

                // Width covers icon, gap and text, so the alignment places the whole group (see Assign)
                float lw = s.Width * UiScale, lh = s.SizePx * UiScale * Mathf.Max(LineEm, s.IconEm * IconGlyphFraction);
                float x0 = s.Align == TextAlignmentOptions.Right ? sp.x - lw
                    : s.Align == TextAlignmentOptions.Center ? sp.x - lw * 0.5f
                    : sp.x;
                Rect r = new Rect(x0 - Padding, sp.y - lh * 0.5f - Padding, lw + Padding * 2, lh + Padding * 2);
                if (r.xMax < 0 || r.xMin > w || r.yMax < 0 || r.yMin > h) continue;

                bool overlaps = false;
                for (int j = 0; j < placedRects.Count; j++)
                {
                    if (placedRects[j].Overlaps(r))
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (overlaps) continue;
                placedRects.Add(r);
                placed.Add((s, world, r, fade));
            }
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
                Position(slot, placed[i].world, placed[i].fade);
            }

            float dt = Time.unscaledDeltaTime;
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
        }

        void Position(Slot slot, Vector3 world, float fade)
        {
            if (!slot.Tmp.gameObject.activeSelf) slot.Tmp.gameObject.SetActive(true);
            Transform camT = rig.transform;
            float ppu = rig.PixelsPerUnit(world);
            if (ppu <= 0) return;
            LabelSpec s = slot.Spec;
            Vector3 offset = (camT.right * s.PixelOffset.x + camT.up * s.PixelOffset.y) * UiScale / ppu;
            Transform t = slot.Tmp.transform;
            t.SetPositionAndRotation(world + offset, camT.rotation);
            float scale = s.SizePx * UiScale / ppu;
            t.localScale = new Vector3(scale, scale, scale);
            slot.Alpha = Mathf.MoveTowards(slot.Alpha, 1, Time.unscaledDeltaTime * 5);
            slot.Fade = fade;
            SetColor(slot, fade);
        }

        void SetColor(Slot slot, float fade)
        {
            LabelSpec s = slot.Spec;
            if (s == null) return;
            float boost = IsHighlighted(s) ? 1f : Highlighter.HasHighlight ? 0.45f : 0.85f;
            if (Hovered == s) boost = 1f;
            Color c = s.Color;
            c.a *= slot.Alpha * fade * boost;
            if (slot.Tmp.color != c) slot.Tmp.color = c;
            if (slot.IconShown && slot.Icon.color != c) slot.Icon.color = c;
        }

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

        void UpdateHover()
        {
            Hovered = null;
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 m = mouse.position.ReadValue();
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

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Why
{
    /// <summary>A text label pinned to a data-space point.</summary>
    public sealed class LabelSpec
    {
        public string Text;
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

        internal float Width;             // estimated width in px at SizePx
    }

    /// <summary>
    /// Places labels every frame the view changes: projects all specs, then greedily keeps the
    /// highest-priority labels that do not overlap on screen. Zooming in spreads points apart, so more
    /// labels fit - level of detail for free. Labels are billboards with constant pixel size.
    /// </summary>
    public sealed class LabelSystem : MonoBehaviour
    {
        public int MaxVisible = 110;
        public float Padding = 4;

        sealed class Slot
        {
            public TextMeshPro Tmp;
            public LabelSpec Spec;
            public float Alpha;
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
        bool dirty = true;

        /// <summary>Pixel sizes are authored for a 1080p screen and scaled to the actual height.</summary>
        public static float UiScale => Mathf.Max(0.5f, Screen.height / 1080f);

        /// <summary>The label under the mouse, if any (for tooltips).</summary>
        public LabelSpec Hovered { get; private set; }

        public void Init(CameraRig cameraRig) => rig = cameraRig;

        /// <summary>Thread safe: queue a label (layers call this from Prepare).</summary>
        public void Add(LabelSpec spec)
        {
            if (spec == null || string.IsNullOrEmpty(spec.Text)) return;
            lock (gate) pending.Add(spec);
        }

        public void MarkDirty() => dirty = true;

        void FlushPending()
        {
            lock (gate)
            {
                if (pending.Count == 0) return;
                foreach (LabelSpec s in pending)
                {
                    s.Width = EstimateWidth(s.Text) * s.SizePx;
                    specs.Add(s);
                }

                pending.Clear();
            }

            specs.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            dirty = true;
        }

        static float EstimateWidth(string text)
        {
            float w = 0;
            foreach (char c in text)
            {
                w += c == ' ' ? 0.28f : char.IsUpper(c) ? 0.64f : char.IsDigit(c) ? 0.55f : 0.5f;
            }

            return w + 0.2f;
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
                if (s.Hidden) continue;
                float fade = GraphWarp.FocusFade(s.Data.x, warp);
                if (s.HandoffFade) fade *= GraphStyle.HandoffFade(s.Data.x);
                if (fade < 0.35f) continue;

                Vector3 world = GraphWarp.ToWorld(s.Data.x, s.Data.y, s.Data.z, warp);
                Vector3 sp = cam.WorldToScreenPoint(world);
                if (sp.z <= cam.nearClipPlane) continue;
                sp.x += s.PixelOffset.x * UiScale;
                sp.y += s.PixelOffset.y * UiScale;

                float lw = s.Width * UiScale, lh = s.SizePx * UiScale * 1.15f;
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
                        slot.Tmp.text = s.Text;
                        slot.Tmp.alignment = s.Align;
                        slot.Tmp.rectTransform.pivot = s.Align == TextAlignmentOptions.Right ? new Vector2(1, 0.5f)
                            : s.Align == TextAlignmentOptions.Center ? new Vector2(0.5f, 0.5f)
                            : new Vector2(0, 0.5f);
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
                    if (slot.Tmp.gameObject.activeSelf) slot.Tmp.gameObject.SetActive(false);
                    slot.Spec = null;
                }
                else
                {
                    SetColor(slot, 1);
                }
            }
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
            tmp.rectTransform.sizeDelta = new Vector2(20, 2);
            tmp.renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tmp.renderer.receiveShadows = false;
            tmp.sortingOrder = 10;
            Slot slot = new Slot { Tmp = tmp };
            slots.Add(slot);
            return slot;
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

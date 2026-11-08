using UnityEngine;

namespace Why.Director
{
    /// <summary>Where the narration panel sits on screen.</summary>
    public enum PanelSlot
    {
        Center,
        LeftTop,
        LeftMiddle,
        LeftBottom,
        RightTop,
        RightMiddle,
        RightBottom,

        /// <summary>Portrait: a full-width sheet docked at the top edge.</summary>
        Top,

        /// <summary>Portrait: a full-width sheet docked at the bottom edge, above the HUD legend.</summary>
        Bottom
    }

    /// <summary>
    /// Screen geometry for the director: projecting anchors under a camera pose, clamping off-screen
    /// targets to the screen edge, and choosing a panel slot that never covers what the panel points at.
    /// Positions are canvas units with the origin at the bottom-left corner of the canvas. Landscape uses
    /// the side slots; a portrait screen (500 canvas units wide) docks the panel as a top or bottom sheet.
    /// </summary>
    public static class PanelPlacement
    {
        /// <summary>Distance of side panels from the left/right screen edge.</summary>
        public const float SideMargin = 56f;

        /// <summary>Distance of top/bottom panels from the screen edge (clears HUD bars).</summary>
        public const float EdgeMargin = 96f;

        /// <summary>
        /// Distance of bottom-left panels from the bottom edge in landscape until the HUD reports where its legend block
        /// ends (<see cref="LegendTop"/>): clear of a short legend and the lifeline readout above it (24 + ~75 + 10 + ~33).
        /// </summary>
        public const float LandscapeBottomMargin = 150f;

        /// <summary>Gap between the top of the HUD's legend block and a panel docked above it.</summary>
        const float LegendGap = 14f;

        /// <summary>
        /// Top of the HUD's bottom-left block, canvas units above the bottom edge: the legend and, while lifelines show,
        /// the lifeline readout above it, which stay on screen through the tour. The HUD reports it whenever the block
        /// changes (an economy view's key runs from two lines to eight); 0 until it does. Bottom-left panels and the
        /// portrait bottom sheet dock above it.
        /// </summary>
        public static float LegendTop { get; set; }

        /// <summary>
        /// Landscape: where left-side panels begin, canvas units from the left edge, while something holds the screen's left
        /// edge for the stop on screen (the tour's picture cards, <see cref="UI.TourPictures"/>, which set it); 0, or anything
        /// below <see cref="SideMargin"/>, leaves them at <see cref="SideMargin"/>. <see cref="Choose"/> keeps its clearance
        /// from the target with the panels where this puts them.
        /// </summary>
        public static float LeftInset { get; set; }

        /// <summary>Minimum gap between the panel and the point it refers to.</summary>
        public const float Clearance = 56f;

        /// <summary>Inset of clamped (off-screen) arrow tips from the screen edge.</summary>
        public const float EdgeInset = 44f;

        /// <summary>Portrait: distance of the sheets from the left and right screen edges.</summary>
        public const float PortraitSideMargin = 10f;

        /// <summary>Portrait: distance of the top sheet from the top edge (the HUD title steps aside during the tour).</summary>
        public const float PortraitTopMargin = 18f;

        /// <summary>
        /// Portrait: distance of the bottom sheet from the bottom edge until the HUD reports its legend block
        /// (<see cref="LegendTop"/>). During the tour the HUD keeps only the legend (16 + ~62 units) and, while
        /// lifelines show, the readout above it (+10 + ~33) down there; the sheet sits just above both (the HUD and
        /// the director share the portrait canvas units).
        /// </summary>
        public const float PortraitBottomMargin = 130f;

        /// <summary>
        /// Portrait: targets below this share of the screen height send the sheet to the top (the bottom sheet
        /// is meant to reach up to ~45% of a 9:16 screen; with their keys and evidence the longest stops reach higher).
        /// </summary>
        const float PortraitLowTarget = 0.5f;

        /// <summary>
        /// Portrait: the share of the screen height a bottom sheet should reach up to, its dock included: what the
        /// placement above assumes and the director frames anchors over (<see cref="PortraitSheetMaxHeight"/>).
        /// </summary>
        const float PortraitSheetReach = 0.45f;

        public static Vector2 SlotAnchor(PanelSlot slot)
        {
            switch (slot)
            {
                case PanelSlot.LeftTop: return new Vector2(0, 1);
                case PanelSlot.LeftMiddle: return new Vector2(0, 0.5f);
                case PanelSlot.LeftBottom: return new Vector2(0, 0);
                case PanelSlot.RightTop: return new Vector2(1, 1);
                case PanelSlot.RightMiddle: return new Vector2(1, 0.5f);
                case PanelSlot.RightBottom: return new Vector2(1, 0);
                case PanelSlot.Top: return new Vector2(0.5f, 1);
                case PanelSlot.Bottom: return new Vector2(0.5f, 0);
                default: return new Vector2(0.5f, 0.5f);
            }
        }

        /// <summary>
        /// anchoredPosition of a panel in a slot (its pivot equals the slot anchor). Bottom-left panels and the portrait
        /// bottom sheet stand above the HUD's legend block where it ends now, so it follows the view's key.
        /// </summary>
        public static Vector2 SlotOffset(PanelSlot slot)
        {
            if (slot == PanelSlot.Top) return new Vector2(0, -PortraitTopMargin);
            if (slot == PanelSlot.Bottom) return new Vector2(0, LegendTop > 0 ? LegendTop + LegendGap : PortraitBottomMargin);
            Vector2 a = SlotAnchor(slot);
            float x = a.x < 0.25f ? Mathf.Max(SideMargin, LeftInset) : a.x > 0.75f ? -SideMargin : 0;
            float y = a.y > 0.75f ? -EdgeMargin : a.y < 0.25f ? BottomMargin(a.x < 0.25f) : 0;
            return new Vector2(x, y);
        }

        /// <summary>
        /// A landscape bottom panel's distance from the bottom edge: on the left above the HUD's legend block (the only
        /// HUD block left down there during the tour), elsewhere clear of the edge.
        /// </summary>
        static float BottomMargin(bool left)
        {
            if (!left) return EdgeMargin;
            return LegendTop > 0 ? Mathf.Max(EdgeMargin, LegendTop + LegendGap) : LandscapeBottomMargin;
        }

        /// <summary>
        /// Portrait: the tallest a step sheet should be on a canvas this tall, docked at the bottom, so it reaches no
        /// higher than <see cref="PortraitSheetReach"/> of the screen.
        /// </summary>
        public static float PortraitSheetMaxHeight(float canvasHeight) =>
            canvasHeight * PortraitSheetReach - SlotOffset(PanelSlot.Bottom).y;

        /// <summary>Direction a panel slides in from: its nearest screen edge (cards rise from below).</summary>
        public static Vector2 SlideDirection(PanelSlot slot)
        {
            if (slot == PanelSlot.Top) return Vector2.up;
            Vector2 a = SlotAnchor(slot);
            if (a.x < 0.25f) return Vector2.left;
            if (a.x > 0.75f) return Vector2.right;
            return Vector2.down;
        }

        /// <summary>Width of a portrait sheet on a canvas this wide.</summary>
        public static float PortraitSheetWidth(float canvasWidth) => canvasWidth - 2 * PortraitSideMargin;

        /// <summary>The rectangle a panel of this size occupies in a slot.</summary>
        public static Rect SlotRect(PanelSlot slot, Vector2 size, Vector2 canvas)
        {
            Vector2 a = SlotAnchor(slot);
            Vector2 pivot = Vector2.Scale(a, canvas) + SlotOffset(slot);
            return new Rect(pivot - Vector2.Scale(a, size), size);
        }

        /// <summary>
        /// The landscape slot a step names for its card (<see cref="TourStep.Side"/>: "left" or "right", with "top" or
        /// "bottom" for a corner; "bottom" alone is the left corner), or null for none. Some views keep their subject
        /// where an untargeted card would land: the money chart's first decades, the attention loop's legs.
        /// </summary>
        public static PanelSlot? Preferred(string side)
        {
            if (string.IsNullOrWhiteSpace(side)) return null;
            string s = side.ToLowerInvariant();
            bool right = s.Contains("right"), top = s.Contains("top"), bottom = s.Contains("bottom");
            if (top) return right ? PanelSlot.RightTop : PanelSlot.LeftTop;
            if (bottom) return right ? PanelSlot.RightBottom : PanelSlot.LeftBottom;
            return right ? PanelSlot.RightMiddle : s.Contains("left") ? PanelSlot.LeftMiddle : (PanelSlot?)null;
        }

        /// <summary>
        /// Picks the slot for a panel. Side panels go to the half of the screen opposite the target, at mid
        /// height; if that would still come too close (narrow screens, targets near the center) the panel
        /// moves to the vertical half opposite the target as well. Cards prefer the center. A preferred slot
        /// (landscape only) is taken without a target, and tried first with one.
        /// </summary>
        public static PanelSlot Choose(Vector2 size, Vector2 canvas, bool hasTarget, Vector2 target, bool preferCenter,
            PanelSlot? preferred = null)
        {
            if (ScreenLayout.IsPortrait) return ChoosePortrait(size, canvas, hasTarget, target, preferCenter);
            if (!hasTarget) return preferred ?? (preferCenter ? PanelSlot.Center : PanelSlot.LeftMiddle);
            if (preferred.HasValue && Distance(SlotRect(preferred.Value, size, canvas), target) >= Clearance) return preferred.Value;

            bool right = target.x > canvas.x * 0.5f;
            bool high = target.y > canvas.y * 0.5f;
            PanelSlot opposite = right ? PanelSlot.LeftMiddle : PanelSlot.RightMiddle;
            PanelSlot oppositeCorner = right
                ? high ? PanelSlot.LeftBottom : PanelSlot.LeftTop
                : high ? PanelSlot.RightBottom : PanelSlot.RightTop;
            PanelSlot sameSideCorner = right
                ? high ? PanelSlot.RightBottom : PanelSlot.RightTop
                : high ? PanelSlot.LeftBottom : PanelSlot.LeftTop;
            PanelSlot oppositeSameHeight = right
                ? high ? PanelSlot.LeftTop : PanelSlot.LeftBottom
                : high ? PanelSlot.RightTop : PanelSlot.RightBottom;

            PanelSlot[] candidates = preferCenter
                ? new[] { PanelSlot.Center, opposite, oppositeCorner, sameSideCorner }
                : new[] { opposite, oppositeCorner, sameSideCorner, oppositeSameHeight, PanelSlot.Center };
            return FirstClear(candidates, size, canvas, target);
        }

        /// <summary>
        /// Portrait: the panel is a full-width sheet, so only its height is free. It docks at the bottom (above
        /// the legend) unless the target is low on the screen, then at the top; cards prefer the center.
        /// </summary>
        static PanelSlot ChoosePortrait(Vector2 size, Vector2 canvas, bool hasTarget, Vector2 target, bool preferCenter)
        {
            if (!hasTarget) return preferCenter ? PanelSlot.Center : PanelSlot.Bottom;
            bool low = target.y < canvas.y * PortraitLowTarget;
            PanelSlot away = low ? PanelSlot.Top : PanelSlot.Bottom;
            PanelSlot other = low ? PanelSlot.Bottom : PanelSlot.Top;
            PanelSlot[] candidates = preferCenter
                ? new[] { PanelSlot.Center, away, other }
                : new[] { away, other };
            return FirstClear(candidates, size, canvas, target);
        }

        /// <summary>The first candidate that keeps <see cref="Clearance"/> from the target, else the farthest one.</summary>
        static PanelSlot FirstClear(PanelSlot[] candidates, Vector2 size, Vector2 canvas, Vector2 target)
        {
            PanelSlot best = candidates[0];
            float bestDistance = -1;
            foreach (PanelSlot slot in candidates)
            {
                float d = Distance(SlotRect(slot, size, canvas), target);
                if (d >= Clearance) return slot;
                if (d > bestDistance)
                {
                    bestDistance = d;
                    best = slot;
                }
            }

            return best;
        }

        /// <summary>Distance from a point to a rectangle (0 inside).</summary>
        public static float Distance(Rect r, Vector2 p)
        {
            float dx = Mathf.Max(0, Mathf.Max(r.xMin - p.x, p.x - r.xMax));
            float dy = Mathf.Max(0, Mathf.Max(r.yMin - p.y, p.y - r.yMax));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Viewport position (0..1) of a world point seen from a camera pose (mirrors CameraRig's pose to
        /// transform mapping), so the panel can be placed for where a flight will end. fovDegrees is the
        /// vertical field of view and aspect the screen's width / height (below 1 in portrait). Points behind
        /// the pose are pushed far out in their direction; returns false for those.
        /// </summary>
        public static bool ViewportUnderPose(CameraPose pose, float fovDegrees, float aspect, Vector3 world,
            out Vector2 viewport)
        {
            Quaternion rotation = Quaternion.Euler(pose.Pitch, pose.Yaw, 0);
            Vector3 eye = pose.Target - rotation * Vector3.forward * pose.Distance;
            Vector3 local = Quaternion.Inverse(rotation) * (world - eye);
            float tan = Mathf.Tan(fovDegrees * 0.5f * Mathf.Deg2Rad);
            if (local.z <= 1e-5f)
            {
                Vector2 dir = new Vector2(local.x, local.y);
                if (dir.sqrMagnitude < 1e-12f) dir = Vector2.down;
                viewport = new Vector2(0.5f, 0.5f) + dir.normalized * 10f;
                return false;
            }

            viewport = new Vector2(0.5f + 0.5f * local.x / (local.z * tan * Mathf.Max(aspect, 1e-3f)),
                0.5f + 0.5f * local.y / (local.z * tan));
            return true;
        }

        /// <summary>
        /// Clamps a canvas point to the screen (inset from the edges) when it is off screen or behind the
        /// camera. dir is the unit direction from the screen center toward the real target. Returns true
        /// when the point had to be clamped.
        /// </summary>
        public static bool ClampToScreen(Vector2 p, bool behind, Vector2 canvas, out Vector2 clamped, out Vector2 dir)
        {
            Vector2 center = canvas * 0.5f;
            // a point behind the camera projects mirrored through the screen center
            if (behind) p = canvas - p;
            bool off = behind || p.x < 0 || p.y < 0 || p.x > canvas.x || p.y > canvas.y;
            dir = p - center;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.down;
            if (!off)
            {
                clamped = p;
                return false;
            }

            Vector2 half = new Vector2(Mathf.Max(center.x - EdgeInset, 1), Mathf.Max(center.y - EdgeInset, 1));
            float tx = Mathf.Abs(dir.x) > 1e-6f ? half.x / Mathf.Abs(dir.x) : float.MaxValue;
            float ty = Mathf.Abs(dir.y) > 1e-6f ? half.y / Mathf.Abs(dir.y) : float.MaxValue;
            clamped = center + dir * Mathf.Min(tx, ty);
            return true;
        }

        /// <summary>
        /// Where an arrow leaves the panel: the middle of the edge facing the target, slid along that edge
        /// toward the target so the arrow stays short, plus the outward direction of that edge.
        /// </summary>
        public static Vector2 EdgePoint(Rect r, Vector2 target, float gap, out Vector2 outward)
        {
            const float cornerInset = 28f;
            Vector2 d = target - r.center;
            float nx = d.x / Mathf.Max(r.width * 0.5f, 1f);
            float ny = d.y / Mathf.Max(r.height * 0.5f, 1f);
            if (Mathf.Abs(nx) >= Mathf.Abs(ny))
            {
                outward = new Vector2(d.x >= 0 ? 1 : -1, 0);
                float inset = Mathf.Min(cornerInset, r.height * 0.5f);
                float y = Mathf.Clamp(target.y, r.yMin + inset, r.yMax - inset);
                return new Vector2(d.x >= 0 ? r.xMax + gap : r.xMin - gap, y);
            }

            outward = new Vector2(0, d.y >= 0 ? 1 : -1);
            float insetX = Mathf.Min(cornerInset, r.width * 0.5f);
            float x = Mathf.Clamp(target.x, r.xMin + insetX, r.xMax - insetX);
            return new Vector2(x, d.y >= 0 ? r.yMax + gap : r.yMin - gap);
        }
    }
}

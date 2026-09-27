using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Why.Director
{
    /// <summary>
    /// The director's attention arrow: a smooth curve with an arrowhead from the narration panel to the
    /// thing it talks about, dashes flowing toward the target, and a pulsing ring (with a ripple) at the
    /// target. Off-screen targets get the arrowhead at the screen edge, pointing toward them. Small rings
    /// mark the step's other highlighted anchors. Neutral white: hue is reserved for the graph's levels.
    ///
    /// Coordinates are canvas units relative to this graphic's rect, whose pivot is its bottom-left corner.
    /// The mesh is rebuilt every frame while visible, so the arrow lives on its own sub-canvas.
    /// </summary>
    public sealed class AttentionArrow : MaskableGraphic
    {
        public const float RingRadius = 15f;

        const float LineHalfWidth = 0.95f;
        const float DashLength = 9f;
        const float DashPeriod = 17f;
        const float DashSpeed = 34f;
        const float HeadLength = 13f;
        const float HeadHalfWidth = 6.5f;
        const float TipGap = 7f;
        const float StartTaper = 36f;
        const float MinCurveLength = 24f;

        Vector2 from, fromDir, to, toDir;
        bool clamped, hasTarget;
        float opacity, reveal, clock;

        readonly List<Vector2> markers = new List<Vector2>();
        readonly List<Vector2> curve = new List<Vector2>();
        readonly List<float> lengths = new List<float>();
        readonly List<Vector2> strip = new List<Vector2>();
        readonly List<float> stripAlpha = new List<float>();

        /// <summary>Unit direction in which the arrow arrives at its tip (for placing captions).</summary>
        public Vector2 TipDirection { get; private set; } = Vector2.right;

        /// <summary>
        /// Sets the arrow for this frame. from/fromDir: start point on the panel edge and the edge's outward
        /// direction; tip: target point, or the clamped screen-edge point with offscreenDir pointing at the
        /// real target; opacity 0..1; revealFraction 0..1 draws the curve on from the panel.
        /// </summary>
        public void SetArrow(Vector2 start, Vector2 startDir, Vector2 tip, Vector2 offscreenDir, bool isClamped,
            float alpha, float revealFraction)
        {
            hasTarget = true;
            from = start;
            fromDir = startDir;
            to = tip;
            toDir = offscreenDir;
            clamped = isClamped;
            opacity = Mathf.Clamp01(alpha);
            reveal = Mathf.Clamp01(revealFraction);
            float d = (to - from).magnitude;
            Vector2 c1 = from + fromDir * (d * 0.4f);
            Vector2 arrive = clamped ? toDir : to - c1;
            TipDirection = arrive.sqrMagnitude > 1e-8f ? arrive.normalized : Vector2.right;
        }

        /// <summary>No arrow this frame (markers may still show).</summary>
        public void ClearArrow(float markerAlpha)
        {
            hasTarget = false;
            opacity = Mathf.Clamp01(markerAlpha);
        }

        /// <summary>Canvas positions of secondary markers (the step's other highlights).</summary>
        public List<Vector2> Markers => markers;

        /// <summary>Advances the animation and schedules a mesh rebuild (call once per frame while shown).</summary>
        public void Animate(float dt)
        {
            clock += dt;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float o = opacity * color.a;
            if (o <= 0.002f) return;

            Color white = color;
            foreach (Vector2 m in markers)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(clock * 2.6f + m.x * 0.013f);
                UiStroke.Ring(vh, m, 6.5f + 1.2f * pulse, 0.6f, Fade(white, o * (0.3f + 0.2f * pulse)), 28);
                UiStroke.Disc(vh, m, 1.6f, Fade(white, o * 0.55f), 10);
            }

            if (!hasTarget) return;
            if (!clamped) DrawTarget(vh, white, o);
            DrawCurve(vh, white, o);
        }

        void DrawTarget(VertexHelper vh, Color white, float o)
        {
            float appear = Smooth(Mathf.Clamp01(reveal * 2.5f));
            float breathe = Mathf.Sin(clock * (2f * Mathf.PI / 1.7f));
            float r = (RingRadius + 1.4f * breathe) * (0.55f + 0.45f * appear);
            UiStroke.Ring(vh, to, r, 1.1f, Fade(white, o * 0.9f * appear));

            // an expanding ripple draws the eye
            float p = Mathf.Repeat(clock / 1.9f, 1f);
            UiStroke.Ring(vh, to, RingRadius * 0.6f + 30f * p, 0.8f, Fade(white, o * 0.5f * (1 - p) * (1 - p) * appear));
            UiStroke.Disc(vh, to, 2.4f, Fade(white, o * appear));
        }

        void DrawCurve(VertexHelper vh, Color white, float o)
        {
            float d = (to - from).magnitude;
            if (d < MinCurveLength + (clamped ? 0 : RingRadius + TipGap)) return;

            // cubic Bezier: leave the panel along its edge normal, arrive along the tip direction
            Vector2 p0 = from, p1 = from + fromDir * (d * 0.4f), p3 = to;
            Vector2 p2 = to - TipDirection * (d * 0.3f);
            int n = Mathf.Clamp(Mathf.CeilToInt(d / 7f), 16, 120);
            curve.Clear();
            lengths.Clear();
            float total = 0;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, s = 1 - t;
                Vector2 q = s * s * s * p0 + 3 * s * s * t * p1 + 3 * s * t * t * p2 + t * t * t * p3;
                if (i > 0) total += (q - curve[i - 1]).magnitude;
                curve.Add(q);
                lengths.Add(total);
            }

            // the stroke stops short of the target ring; the head ends at the tip (or at the ring)
            float headEnd = (total - (clamped ? 0 : RingRadius + TipGap)) * Smooth(reveal);
            if (headEnd < 2f) return;
            float lineEnd = headEnd - HeadLength * 0.6f;

            // a faint continuous rail under the flowing dashes
            AddSpan(vh, 0, lineEnd, 0.6f, Fade(white, o * 0.22f));

            float phase = Mathf.Repeat(clock * DashSpeed, DashPeriod);
            for (float a = phase - DashPeriod; a < lineEnd; a += DashPeriod)
            {
                AddSpan(vh, Mathf.Max(0, a), Mathf.Min(lineEnd, a + DashLength), LineHalfWidth, Fade(white, o * 0.92f));
            }

            float headAlpha = Smooth(Mathf.InverseLerp(0.75f, 1f, reveal));
            Vector2 tip = PointAt(headEnd);
            Vector2 dir = tip - PointAt(Mathf.Max(0, headEnd - 4f));
            dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : TipDirection;
            if (clamped) dir = toDir;
            Vector2 side = new Vector2(-dir.y, dir.x) * HeadHalfWidth;
            Vector2 back = tip - dir * HeadLength;
            UiStroke.Triangle(vh, tip, back + side, back - side, Fade(white, o * headAlpha));
        }

        /// <summary>Strokes the part of the sampled curve between arc lengths a and b.</summary>
        void AddSpan(VertexHelper vh, float a, float b, float halfWidth, Color c)
        {
            if (b - a < 0.5f) return;
            strip.Clear();
            stripAlpha.Clear();
            strip.Add(PointAt(a));
            stripAlpha.Add(Taper(a));
            for (int i = 0; i < lengths.Count; i++)
            {
                if (lengths[i] <= a) continue;
                if (lengths[i] >= b) break;
                strip.Add(curve[i]);
                stripAlpha.Add(Taper(lengths[i]));
            }

            strip.Add(PointAt(b));
            stripAlpha.Add(Taper(b));
            UiStroke.Polyline(vh, strip, stripAlpha, 0, strip.Count, halfWidth, c);
        }

        /// <summary>The arrow grows out of the panel: alpha rises over the first few units.</summary>
        static float Taper(float s) => Smooth(Mathf.Clamp01(s / StartTaper));

        Vector2 PointAt(float s)
        {
            if (s <= 0) return curve[0];
            int lo = 0, hi = lengths.Count - 1;
            if (s >= lengths[hi]) return curve[hi];
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (lengths[mid] < s) lo = mid;
                else hi = mid;
            }

            float span = lengths[hi] - lengths[lo];
            return Vector2.Lerp(curve[lo], curve[hi], span > 1e-6f ? (s - lengths[lo]) / span : 0);
        }

        static Color Fade(Color c, float a)
        {
            c.a = Mathf.Clamp01(a);
            return c;
        }

        static float Smooth(float t) => t * t * (3 - 2 * t);
    }
}

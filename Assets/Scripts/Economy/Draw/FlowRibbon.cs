using System.Collections.Generic;
using UnityEngine;
using Roles = Why.Economy.EconomyStyle.Roles;

namespace Why.Economy.Draw
{
    /// <summary>Where a ribbon starts, passes or ends: a point in its plane and the direction the money moves there.</summary>
    public readonly struct RibbonPort
    {
        public readonly Vector3 Point;

        /// <summary>Unit direction of travel at the point (zero: along the chord to the next point).</summary>
        public readonly Vector3 Direction;

        public RibbonPort(Vector3 point, Vector3 direction)
        {
            Point = point;
            Direction = direction.sqrMagnitude > 1e-12f ? direction.normalized : Vector3.zero;
        }
    }

    /// <summary>One lane of a ribbon: its share of the width and its colour.</summary>
    public readonly struct RibbonLane
    {
        public readonly float Share;
        public readonly Color Color;

        public RibbonLane(float share, Color color)
        {
            Share = share;
            Color = color;
        }
    }

    /// <summary>One ribbon to draw (<see cref="FlowRibbon.Add"/>): its route, its amount on a scale, its lanes and its form.</summary>
    public sealed class RibbonSpec
    {
        /// <summary>The ports it leaves and enters (centres of its ends), and the points it passes on the way, in order.</summary>
        public RibbonPort From, To;

        public RibbonPort[] Via;

        /// <summary>The plane's normal, toward the camera: the band's width lies across the route within the plane.</summary>
        public Vector3 Normal = Vector3.back;

        /// <summary>The amount it carries (dollars a year, a stock, people) and world units per unit of it: width = amount × scale.</summary>
        public double Amount;

        public float Scale = 1;

        /// <summary>
        /// Its lanes from its left edge to its right (looking along the flow; <see cref="FlowRibbon.Lanes(MotiveLanes)"/> for a
        /// motive split), their shares normalized; none is one lane of <see cref="Color"/>.
        /// </summary>
        public RibbonLane[] Lanes;

        public Color Color = Color.white;

        /// <summary>Multiplies every lane's alpha.</summary>
        public float Alpha = 1;

        /// <summary>A measured total whose split is judged: both lanes solid, a 1 px seam between them (the label carries "J").</summary>
        public bool Seam;

        /// <summary>
        /// Judged by Jev as a whole (the mark code's judged form): its outline drawn, its fill at alpha 0.5 × confidence, and
        /// nothing below 0.5. Not a number (the default) is measured: solid.
        /// </summary>
        public float Confidence = float.NaN;

        /// <summary>The arrowhead's length (world): the band narrows to a point over it at <see cref="To"/>. 0: none; below 0: the default.</summary>
        public float Arrow = -1;

        public float Id;

        /// <summary>
        /// Hermite tangents as long as each piece's chord times this (higher: rounder turns that hold their direction longer).
        /// The band is offset across its route, so a turn tighter than half its width folds the inner edge: route with room.
        /// </summary>
        public float Tension = 1;

        /// <summary>Samples per piece of the route.</summary>
        public int Segments = 24;
    }

    /// <summary>What <see cref="FlowRibbon.Add"/> drew, for labels and checks.</summary>
    public struct RibbonResult
    {
        /// <summary>False when nothing was drawn: no amount, or a judgment below the floor ("not drawn: too uncertain").</summary>
        public bool Drawn;

        /// <summary>Drawn as a <see cref="FlowRibbon.HairlinePx"/> hairline: narrower than that at its scale (its label carries the mark).</summary>
        public bool Hairline;

        /// <summary>The width it stands for (world: amount × scale) and that width on screen in the view it was built for (px).</summary>
        public float Width, WidthPx;

        /// <summary>The route's length (world).</summary>
        public float Length;

        /// <summary>The route's middle and the unit vector toward its left edge there (to place a label beside it); its tip.</summary>
        public Vector3 Mid, MidSide, Tip;
    }

    /// <summary>
    /// The shared ribbon (spec 3.3): money moving between two ports as a band in a plane facing the camera, routed by cubic
    /// Hermite pieces through its ports (stacked ports never cross when routes keep their order), as wide as its amount on
    /// the view's scale (width = amount × scale, measured on rendered pixels within 2% by PaletteCheck), split into lanes,
    /// and pointed at its end, since direction is an arrowhead, never a hue. Narrower on screen than
    /// <see cref="HairlinePx"/> it is drawn as a hairline of that width, carrying the mark "‹" (<see cref="Label"/>). LDR
    /// and alpha-blended, through the mark code's meshes (<see cref="MarkMeshes"/>). Any thread.
    /// </summary>
    public static class FlowRibbon
    {
        /// <summary>A ribbon narrower than this on screen is drawn as a hairline this wide (px).</summary>
        public const float HairlinePx = 1.5f;

        /// <summary>The mark a hairline's label carries: the ribbon is narrower than drawn.</summary>
        public const string HairlineMark = "‹";

        /// <summary>The default arrowhead: this many widths long, but at most this share of the route.</summary>
        public const float ArrowWidths = 0.6f, ArrowShare = 0.25f;

        /// <summary>A hairline's arrowhead: a chevron with arms this long (px).</summary>
        public const float ChevronPx = 6f;

        /// <summary>Width (world) of an amount on a scale (world units per unit of amount); 0 when it is not a finite positive number.</summary>
        public static float Width(double amount, float scale)
        {
            double w = amount * scale;
            return w > 0 && w < float.MaxValue ? (float)w : 0f;
        }

        /// <summary>A motive split as lanes: desire's on the left, fear's on the right (spec 3.2).</summary>
        public static RibbonLane[] Lanes(MotiveLanes split) =>
            new[] { new RibbonLane(split.DesireShare, split.DesireColor), new RibbonLane(split.FearShare, split.FearColor) };

        /// <summary>A ribbon's label: a hairline's carries <see cref="HairlineMark"/> before its text.</summary>
        public static string Label(string text, in RibbonResult drawn) => drawn.Hairline ? HairlineMark + " " + text : text;

        /// <summary>
        /// The route's centre line: Hermite pieces from <see cref="RibbonSpec.From"/> through each of
        /// <see cref="RibbonSpec.Via"/> to <see cref="RibbonSpec.To"/>, leaving and entering each point along its direction,
        /// with tangents as long as the piece's chord times <see cref="RibbonSpec.Tension"/>. The lists are cleared, then get
        /// the points and their unit tangents.
        /// </summary>
        public static void Route(RibbonSpec spec, List<Vector3> points, List<Vector3> tangents)
        {
            points.Clear();
            tangents.Clear();
            int pieces = 1 + (spec.Via?.Length ?? 0), n = Mathf.Max(2, spec.Segments);
            RibbonPort a = spec.From;
            for (int k = 0; k < pieces; k++)
            {
                RibbonPort b = k < pieces - 1 ? spec.Via[k] : spec.To;
                Vector3 p0 = a.Point, p1 = b.Point, chord = p1 - p0;
                Vector3 along = chord.sqrMagnitude > 1e-12f ? chord.normalized : Vector3.right;
                float m = chord.magnitude * Mathf.Max(0, spec.Tension);
                Vector3 m0 = (a.Direction.sqrMagnitude > 0 ? a.Direction : along) * m;
                Vector3 m1 = (b.Direction.sqrMagnitude > 0 ? b.Direction : along) * m;
                for (int i = k == 0 ? 0 : 1; i <= n; i++)
                {
                    float t = i / (float)n, t2 = t * t, t3 = t2 * t;
                    Vector3 p = (2 * t3 - 3 * t2 + 1) * p0 + (t3 - 2 * t2 + t) * m0 + (-2 * t3 + 3 * t2) * p1 + (t3 - t2) * m1;
                    Vector3 d = (6 * t2 - 6 * t) * p0 + (3 * t2 - 4 * t + 1) * m0 + (-6 * t2 + 6 * t) * p1 + (3 * t2 - 2 * t) * m1;
                    points.Add(p);
                    tangents.Add(d.sqrMagnitude > 1e-12f ? d.normalized : along);
                }

                a = b;
            }
        }

        /// <summary>
        /// Adds one ribbon to <paramref name="marks"/>: its lanes as bands in <see cref="MarkMeshes.Fills"/>, its seam, outline or
        /// hairline in <see cref="MarkMeshes.Edges"/>, all with the spec's highlight id. Whether it is a hairline is decided
        /// by the marks' pixels per unit (the view it is built for).
        /// </summary>
        public static RibbonResult Add(MarkMeshes marks, RibbonSpec spec)
        {
            var result = new RibbonResult { Width = Width(spec.Amount, spec.Scale) };
            result.WidthPx = result.Width * marks.PxPerUnit;
            bool judged = !float.IsNaN(spec.Confidence);
            if (result.Width <= 0 || judged && !MarkCode.Drawn(spec.Confidence)) return result;

            int capacity = (1 + (spec.Via?.Length ?? 0)) * Mathf.Max(2, spec.Segments) + 2;
            var points = new List<Vector3>(capacity);
            var tangents = new List<Vector3>(capacity);
            Route(spec, points, tangents);
            float length = 0;
            for (int i = 1; i < points.Count; i++) length += Vector3.Distance(points[i - 1], points[i]);
            float arrow = spec.Arrow < 0 ? Mathf.Min(ArrowWidths * result.Width, ArrowShare * length) : Mathf.Min(spec.Arrow, length);
            bool hairline = result.WidthPx < HairlinePx;
            // the band narrows from exactly an arrowhead's length before the tip: without a point there it would start
            // narrowing at the sample before, drawn narrower than its amount over part of a sample and pointed longer
            if (!hairline && arrow > 0) SplitAt(points, tangents, length - arrow);
            int n = points.Count;
            var along = new float[n];
            for (int i = 1; i < n; i++) along[i] = along[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            length = along[n - 1];
            Vector3 normal = spec.Normal.sqrMagnitude > 1e-12f ? spec.Normal.normalized : Vector3.back;
            var sides = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 side = Vector3.Cross(tangents[i], normal);
                sides[i] = side.sqrMagnitude > 1e-12f ? side.normalized : Vector3.up;
            }

            int mid = Middle(along, 0.5f * length);
            result.Drawn = true;
            result.Length = length;
            result.Mid = points[mid];
            result.MidSide = sides[mid];
            result.Tip = points[n - 1];

            RibbonLane[] lanes = spec.Lanes != null && spec.Lanes.Length > 0 ? spec.Lanes : new[] { new RibbonLane(1, spec.Color) };
            float total = 0;
            foreach (RibbonLane lane in lanes) total += Mathf.Max(0, lane.Share);
            if (total <= 0) return result;

            if (hairline)
            {
                // too narrow to see at its width: a hairline in its largest lane's colour, its direction a chevron
                result.Hairline = true;
                Color c = Strongest(lanes);
                c.a *= spec.Alpha;
                marks.Edges.AddPolyline(points, c, HairlinePx, 0, spec.Id);
                if (arrow != 0)
                {
                    float arm = ChevronPx / marks.PxPerUnit;
                    Vector3 tip = points[n - 1], back = tangents[n - 1] * arm, across = sides[n - 1] * (0.6f * arm);
                    marks.Edges.AddPolyline(new[] { tip - back + across, tip, tip - back - across }, c, HairlinePx, 0, spec.Id);
                }

                return result;
            }

            // each lane a band between its two offsets from the centre line (left positive), narrowing to the tip together
            float fill = judged ? MarkCode.JudgedFill(spec.Confidence) : 1f;
            var half = new float[n];
            for (int i = 0; i < n; i++)
            {
                float taper = arrow > 0 ? Mathf.Clamp01((length - along[i]) / arrow) : 1f;
                half[i] = 0.5f * result.Width * taper;
            }

            float before = 0;
            for (int k = 0; k < lanes.Length; k++)
            {
                float share = Mathf.Max(0, lanes[k].Share) / total;
                if (share <= 0) continue;
                var left = new Vector3[n];
                var right = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    left[i] = points[i] + sides[i] * (half[i] * (1 - 2 * before));
                    right[i] = points[i] + sides[i] * (half[i] * (1 - 2 * (before + share)));
                }

                Color c = lanes[k].Color;
                c.a *= spec.Alpha * fill;
                marks.Fills.AddBand(right, left, new Color32[] { c }, spec.Id);
                before += share;
                if (spec.Seam && k < lanes.Length - 1 && before < 0.9999f)
                {
                    Color seam = Roles.InkBacking;
                    seam.a = spec.Alpha;
                    marks.Edges.AddPolyline(right, seam, MarkCode.SeamPx, 0, spec.Id);
                }
            }

            if (judged)
            {
                // the judged form's outline: up the left edge to the tip, back along the right edge, closed across the start
                // (begun halfway across it, so the line's ends meet straight)
                Color edge = Strongest(lanes);
                edge.a = spec.Alpha;
                var loop = new List<Vector3>(2 * n + 3) { points[0] };
                for (int i = 0; i < n; i++) loop.Add(points[i] + sides[i] * half[i]);
                for (int i = n - 1; i >= 0; i--) loop.Add(points[i] - sides[i] * half[i]);
                loop.Add(points[0]);
                marks.Edges.AddPolyline(loop, edge, MarkCode.EdgePx, 0, spec.Id);
            }

            return result;
        }

        /// <summary>
        /// Puts a point on the route exactly <paramref name="at"/> along it, on the chord between the two samples around it (so
        /// the drawn line keeps its shape) with their tangents blended, unless a sample already stands there.
        /// </summary>
        static void SplitAt(List<Vector3> points, List<Vector3> tangents, float at)
        {
            float walked = 0;
            for (int i = 1; i < points.Count; i++)
            {
                float step = Vector3.Distance(points[i - 1], points[i]);
                if (walked + step < at)
                {
                    walked += step;
                    continue;
                }

                float f = step > 0 ? (at - walked) / step : 0;
                if (f <= 1e-4f || f >= 1 - 1e-4f) return;
                Vector3 tangent = Vector3.Lerp(tangents[i - 1], tangents[i], f);
                points.Insert(i, Vector3.Lerp(points[i - 1], points[i], f));
                tangents.Insert(i, tangent.sqrMagnitude > 1e-12f ? tangent.normalized : tangents[i - 1]);
                return;
            }
        }

        /// <summary>The index of the first point at least this far along the route.</summary>
        static int Middle(float[] along, float at)
        {
            for (int i = 0; i < along.Length; i++)
            {
                if (along[i] >= at) return i;
            }

            return along.Length - 1;
        }

        /// <summary>The colour of the widest lane (a hairline's and a judged outline's).</summary>
        static Color Strongest(RibbonLane[] lanes)
        {
            int best = 0;
            for (int k = 1; k < lanes.Length; k++)
            {
                if (lanes[k].Share > lanes[best].Share) best = k;
            }

            return lanes[best].Color;
        }
    }
}

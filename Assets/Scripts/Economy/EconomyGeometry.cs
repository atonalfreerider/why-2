using System;
using System.Collections.Generic;
using UnityEngine;

namespace Why.Economy
{
    /// <summary>The four cells of a payoff diamond, named by the tip they hold.</summary>
    public enum DiamondCell
    {
        Top = 0,
        Right = 1,
        Bottom = 2,
        Left = 3
    }

    /// <summary>
    /// Plain shapes for the diagrams that stand beyond the present end of the road (<see cref="Station"/>): payoff
    /// diamonds (a 2x2 square turned 45 degrees) and their cells, circles, discs and clouds, arrows, polygons and
    /// sparks. Points are local station coordinates; flat shapes lie in the plane of constant z through their center.
    /// The results go into the usual builders and are drawn with raw materials (<see cref="GraphMaterials.Raw"/>).
    /// Safe on any thread: the scratch lists are per thread, so drawing allocates nothing beyond the builders' own
    /// storage.
    /// </summary>
    public static class EconomyGeometry
    {
        /// <summary>Angle between an arrow's shaft and each side of its head.</summary>
        const float ArrowHeadDegrees = 28f;

        [ThreadStatic] static List<Vector3> scratch;

        static List<Vector3> Scratch()
        {
            List<Vector3> s = scratch ??= new List<Vector3>(64);
            s.Clear();
            return s;
        }

        /// <summary>An LDR color with an alpha, as vertex colors store it.</summary>
        public static Color32 Tint(Color c, float alpha) =>
            new Color32((byte)(Mathf.Clamp01(c.r) * 255), (byte)(Mathf.Clamp01(c.g) * 255), (byte)(Mathf.Clamp01(c.b) * 255),
                (byte)(Mathf.Clamp01(alpha) * 255));

        /// <summary>The same color with its alpha scaled (fading a shape out).</summary>
        public static Color32 Fade(Color32 c, float factor) =>
            new Color32(c.r, c.g, c.b, (byte)Mathf.Clamp(Mathf.RoundToInt(c.a * factor), 0, 255));

        // ------------------------------------------------------------------ diamonds

        /// <summary>
        /// The tip of a diamond centered at <paramref name="c"/> (half-width to the side tips, half-height to the top and
        /// bottom ones).
        /// </summary>
        public static Vector3 Tip(Vector3 c, float halfWidth, float halfHeight, DiamondCell cell)
        {
            switch (cell)
            {
                case DiamondCell.Top: return new Vector3(c.x, c.y + halfHeight, c.z);
                case DiamondCell.Right: return new Vector3(c.x + halfWidth, c.y, c.z);
                case DiamondCell.Bottom: return new Vector3(c.x, c.y - halfHeight, c.z);
                default: return new Vector3(c.x - halfWidth, c.y, c.z);
            }
        }

        /// <summary>Center of a cell of a diamond (where its payoff is written).</summary>
        public static Vector3 CellCenter(Vector3 c, float halfWidth, float halfHeight, DiamondCell cell) =>
            Vector3.Lerp(c, Tip(c, halfWidth, halfHeight, cell), 0.5f);

        /// <summary>
        /// Midpoint of the edge that runs from a cell's tip clockwise to the next tip (Top -> Right is the upper-right
        /// edge); the cell is bounded by this midpoint and the one before its tip.
        /// </summary>
        public static Vector3 EdgeMid(Vector3 c, float halfWidth, float halfHeight, DiamondCell from)
        {
            DiamondCell to = (DiamondCell)(((int)from + 1) % 4);
            return Vector3.Lerp(Tip(c, halfWidth, halfHeight, from), Tip(c, halfWidth, halfHeight, to), 0.5f);
        }

        /// <summary>
        /// A diamond's outline, closed in the middle of its upper-left edge so the seam lies on a straight run and every
        /// corner gets a proper join.
        /// </summary>
        public static void DiamondOutline(LineMeshBuilder lines, Vector3 c, float halfWidth, float halfHeight, Color32 color,
            float widthPx, float id, float intensity = 1)
        {
            List<Vector3> pts = Scratch();
            pts.Add(EdgeMid(c, halfWidth, halfHeight, DiamondCell.Left));
            pts.Add(Tip(c, halfWidth, halfHeight, DiamondCell.Top));
            pts.Add(Tip(c, halfWidth, halfHeight, DiamondCell.Right));
            pts.Add(Tip(c, halfWidth, halfHeight, DiamondCell.Bottom));
            pts.Add(Tip(c, halfWidth, halfHeight, DiamondCell.Left));
            pts.Add(pts[0]);
            lines.AddPolyline(pts, color, widthPx, 0, id, intensity);
        }

        /// <summary>The two lines that divide a diamond into its four cells (between opposite edge midpoints).</summary>
        public static void DiamondCross(LineMeshBuilder lines, Vector3 c, float halfWidth, float halfHeight, Color32 color,
            float widthPx, float id, float intensity = 1)
        {
            Vector3 upperLeft = EdgeMid(c, halfWidth, halfHeight, DiamondCell.Left);
            Vector3 lowerRight = EdgeMid(c, halfWidth, halfHeight, DiamondCell.Right);
            Vector3 upperRight = EdgeMid(c, halfWidth, halfHeight, DiamondCell.Top);
            Vector3 lowerLeft = EdgeMid(c, halfWidth, halfHeight, DiamondCell.Bottom);
            lines.AddSegment(upperLeft, lowerRight, color, widthPx, 0, id, intensity);
            lines.AddSegment(upperRight, lowerLeft, color, widthPx, 0, id, intensity);
        }

        /// <summary>Fills one cell of a diamond (itself a small diamond: the center, two edge midpoints and a tip).</summary>
        public static void FillCell(SurfaceMeshBuilder fills, Vector3 c, float halfWidth, float halfHeight, DiamondCell cell,
            Color32 color, float id, float intensity = 1)
        {
            CellCorners(c, halfWidth, halfHeight, cell, out Vector3 before, out Vector3 tip, out Vector3 after);
            fills.AddTriangle(c, before, tip, color, id, intensity);
            fills.AddTriangle(c, tip, after, color, id, intensity);
        }

        /// <summary>
        /// Fills half a cell, split along the line between its two edge midpoints: the outer half holds the tip, the inner
        /// half the diamond's center. In a side cell the outer half is the side's player and the inner half the other one.
        /// </summary>
        public static void FillHalfCell(SurfaceMeshBuilder fills, Vector3 c, float halfWidth, float halfHeight, DiamondCell cell,
            bool outer, Color32 color, float id, float intensity = 1)
        {
            CellCorners(c, halfWidth, halfHeight, cell, out Vector3 before, out Vector3 tip, out Vector3 after);
            fills.AddTriangle(before, outer ? tip : c, after, color, id, intensity);
        }

        static void CellCorners(Vector3 c, float halfWidth, float halfHeight, DiamondCell cell, out Vector3 before,
            out Vector3 tip, out Vector3 after)
        {
            DiamondCell previous = (DiamondCell)(((int)cell + 3) % 4);
            before = EdgeMid(c, halfWidth, halfHeight, previous);
            tip = Tip(c, halfWidth, halfHeight, cell);
            after = EdgeMid(c, halfWidth, halfHeight, cell);
        }

        // ------------------------------------------------------------------ round shapes

        /// <summary>A circle (closed polyline) in the plane of constant z.</summary>
        public static void Circle(LineMeshBuilder lines, Vector3 c, float radius, int segments, Color32 color, float widthPx,
            float id, float intensity = 1)
        {
            Arc(lines, c, radius, 0, 360, segments, color, widthPx, id, intensity);
        }

        /// <summary>An arc from one angle to another (degrees, counterclockwise from +x).</summary>
        public static void Arc(LineMeshBuilder lines, Vector3 c, float radius, float fromDegrees, float toDegrees, int segments,
            Color32 color, float widthPx, float id, float intensity = 1)
        {
            List<Vector3> pts = Scratch();
            segments = Math.Max(2, segments);
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(fromDegrees, toDegrees, i / (float)segments) * Mathf.Deg2Rad;
                pts.Add(new Vector3(c.x + radius * Mathf.Cos(a), c.y + radius * Mathf.Sin(a), c.z));
            }

            lines.AddPolyline(pts, color, widthPx, 0, id, intensity);
        }

        /// <summary>A filled disc (a fan of triangles) in the plane of constant z.</summary>
        public static void Disc(SurfaceMeshBuilder fills, Vector3 c, float radius, int segments, Color32 color, float id,
            float intensity = 1)
        {
            segments = Math.Max(3, segments);
            Vector3 previous = new Vector3(c.x + radius, c.y, c.z);
            for (int i = 1; i <= segments; i++)
            {
                float a = i * 2f * Mathf.PI / segments;
                Vector3 p = new Vector3(c.x + radius * Mathf.Cos(a), c.y + radius * Mathf.Sin(a), c.z);
                fills.AddTriangle(c, previous, p, color, id, intensity);
                previous = p;
            }
        }

        /// <summary>
        /// A cloud: a circle whose rim bulges into <paramref name="lobes"/> rounded lobes, each
        /// <paramref name="depth"/> of the radius deep (the notebook's brains). The outline starts and closes on the top of
        /// a lobe, where it is smooth.
        /// </summary>
        public static void Cloud(LineMeshBuilder lines, Vector3 c, float radius, int lobes, float depth, Color32 color,
            float widthPx, float id, float intensity = 1)
        {
            List<Vector3> pts = Scratch();
            int perLobe = 8, n = Math.Max(3, lobes) * perLobe;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                float a = t * 2f * Mathf.PI + Mathf.PI * 0.5f;
                // |sin| gives rounded bulges that meet in creases, like a drawn cloud
                float r = radius * (1 - depth + depth * Mathf.Abs(Mathf.Sin((t * lobes + 0.5f) * Mathf.PI)));
                pts.Add(new Vector3(c.x + r * Mathf.Cos(a), c.y + r * Mathf.Sin(a), c.z));
            }

            lines.AddPolyline(pts, color, widthPx, 0, id, intensity);
        }

        /// <summary>
        /// A quadratic curve from <paramref name="a"/> to <paramref name="b"/> pulled toward <paramref name="control"/>
        /// (it leaves a toward the control point and arrives at b from it).
        /// </summary>
        public static void Curve(LineMeshBuilder lines, Vector3 a, Vector3 control, Vector3 b, int segments, Color32 color,
            float widthPx, float id, float intensity = 1)
        {
            List<Vector3> pts = Scratch();
            segments = Math.Max(2, segments);
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments, u = 1 - t;
                pts.Add(u * u * a + 2 * u * t * control + t * t * b);
            }

            lines.AddPolyline(pts, color, widthPx, 0, id, intensity);
        }

        // ------------------------------------------------------------------ marks

        /// <summary>An arrow from one point to another with a two-stroke head at the end (in the plane of constant z).</summary>
        public static void Arrow(LineMeshBuilder lines, Vector3 from, Vector3 to, float head, Color32 color, float widthPx,
            float id, float intensity = 1)
        {
            lines.AddSegment(from, to, color, widthPx, 0, id, intensity);
            Vector3 back = from - to;
            back.z = 0;
            if (back.sqrMagnitude < 1e-10f) return;
            back = back.normalized * head;
            Quaternion left = Quaternion.AngleAxis(ArrowHeadDegrees, Vector3.forward);
            Quaternion right = Quaternion.AngleAxis(-ArrowHeadDegrees, Vector3.forward);
            List<Vector3> pts = Scratch();
            pts.Add(to + left * back);
            pts.Add(to);
            pts.Add(to + right * back);
            lines.AddPolyline(pts, color, widthPx, 0, id, intensity);
        }

        /// <summary>A polygon's outline through its corners (closed when asked, the seam in the middle of the first edge).</summary>
        public static void Polygon(LineMeshBuilder lines, IReadOnlyList<Vector3> corners, bool closed, Color32 color,
            float widthPx, float id, float intensity = 1)
        {
            int n = corners.Count;
            if (n < 2) return;
            List<Vector3> pts = Scratch();
            if (closed)
            {
                Vector3 seam = Vector3.Lerp(corners[0], corners[1], 0.5f);
                pts.Add(seam);
                for (int i = 1; i < n; i++) pts.Add(corners[i]);
                pts.Add(corners[0]);
                pts.Add(seam);
            }
            else
            {
                for (int i = 0; i < n; i++) pts.Add(corners[i]);
            }

            lines.AddPolyline(pts, color, widthPx, 0, id, intensity);
        }

        /// <summary>A small four-pointed spark (a mistake, a flash), in the plane of constant z.</summary>
        public static void Spark(LineMeshBuilder lines, Vector3 c, float radius, Color32 color, float widthPx, float id,
            float intensity = 1)
        {
            lines.AddSegment(new Vector3(c.x - radius, c.y, c.z), new Vector3(c.x + radius, c.y, c.z), color, widthPx, 0, id,
                intensity);
            lines.AddSegment(new Vector3(c.x, c.y - radius, c.z), new Vector3(c.x, c.y + radius, c.z), color, widthPx, 0, id,
                intensity);
            float d = radius * 0.5f;
            Color32 faint = Fade(color, 0.7f);
            lines.AddSegment(new Vector3(c.x - d, c.y - d, c.z), new Vector3(c.x + d, c.y + d, c.z), faint, widthPx * 0.8f, 0, id,
                intensity);
            lines.AddSegment(new Vector3(c.x - d, c.y + d, c.z), new Vector3(c.x + d, c.y - d, c.z), faint, widthPx * 0.8f, 0, id,
                intensity);
        }
    }
}

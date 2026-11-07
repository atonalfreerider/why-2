using System;
using UnityEngine;

namespace Why.Economy.Land
{
    /// <summary>
    /// How a body is drawn wherever it appears: a company in the 2026 strata (HillAdvantageLayer), or a state, an army, a
    /// navy, a population or a faith in history (HillHistoryLayer). The user, 7 Nov 2026: "more of a point cloud
    /// representation with a boundary outline, rather than wireframes", "we still need point cloud occlusion", and the
    /// "boundaried occludable pointclouds should not just apply to companies".
    /// <list type="bullet">
    /// <item>The cloud: the surface's points and a skin of points under it, each coloured by the parts at its place.</item>
    /// <item>The occluder: the surface, shrunk inside the skin, drawn into depth only, so whatever stands behind the body is
    /// hidden while the body shows as points.</item>
    /// <item>The silhouette: the body's outline as seen from an eye, a closed line in the plane facing it.</item>
    /// </list>
    /// Pure geometry into builders and meshes; the caller owns the renderers.
    /// </summary>
    public static class PointBodies
    {
        /// <summary>The hidden solid's size against the body's surface: the skin of points lies between the two.</summary>
        public const float OccluderDepth = .9f;

        /// <summary>A point's dash, in land units: about a pixel at the views' distances, so a dot reads as a dot.</summary>
        public const float DotLength = .3f;

        /// <summary>The body's points into a builder (in its own frame): the surface (opaque as its parts are tangible, hot where a
        /// frontier blooms, lit where a soft one) and a skin under it. <paramref name="skin"/> points under each surface point.</summary>
        public static void Cloud(LineMeshBuilder b, Organism shape, float id, int skin, System.Random rnd, Color frontier, float alpha = 1)
        {
            for (int i = 0; i < shape.Points.Length; i++)
            {
                float f = shape.Frontier[i], g = shape.Tint[i];
                Color c = Color.Lerp(shape.Colors[i], Color.Lerp(shape.Colors[i], Color.white, .45f), g * .7f);
                c = Color.Lerp(c, frontier, f * .9f);
                c.a = Mathf.Clamp01((.62f + .3f * shape.Solidity[i] + .2f * g + .3f * f) * (shape.Known ? 1 : .7f)) * alpha;
                Vector3 p = shape.Points[i];
                b.AddSegment(p, p + Vector3.up * DotLength, c, 3.2f + .8f * g + 2.4f * f, 0, id, f > 0 ? .9f + 5f * f * f : .93f);
            }

            for (int i = 0; i < shape.Points.Length; i++)
                for (int k = 0; k < skin; k++)
                {
                    float u = Mathf.Lerp(OccluderDepth + .015f, .995f, (float)rnd.NextDouble());
                    Vector3 p = shape.Points[i] * u + RandomDir(rnd) * (.025f * shape.MeanRadius * (float)rnd.NextDouble());
                    Color c = shape.ColorAt(p);
                    c.a = (.45f + .3f * (u - OccluderDepth) / (1 - OccluderDepth)) * (shape.Known ? 1 : .7f) * alpha;
                    b.AddSegment(p, p + Vector3.up * DotLength, c, 2.8f, 0, id, .92f);
                }
        }

        /// <summary>The body's hidden solid: its surface shrunk inside the skin, as a mesh in its own frame.</summary>
        public static Mesh Occluder(Organism shape, int detail, string name)
        {
            var verts = new Vector3[shape.Points.Length];
            var cols = new Color[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i] = shape.Points[i] * OccluderDepth;
                cols[i] = Color.white;
            }

            var mesh = new Mesh { name = name, vertices = verts, colors = cols, triangles = Organism.GeodesicFaces(detail) };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The occluders' material: the surface shader with its colour blended away and depth written, just after the
        /// labels (queue 3000) and before every mark (3180 and up). One per caller (it owns its materials).</summary>
        public static Material OccluderMaterial()
        {
            var m = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1, 3001));
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 1);
            m.SetFloat("_EdgeSoft", 0);
            return m;
        }

        static readonly float[] reach = new float[96], front = new float[96];

        /// <summary>The body's silhouette from <paramref name="eye"/> (both in the builder's frame, the body at
        /// <paramref name="centre"/>): a closed line in the plane facing the eye at the reach of the body's points in each
        /// direction, lit where a frontier blooms. Returns the body's mean colour (for its other marks).</summary>
        public static Color Silhouette(LineMeshBuilder b, Organism shape, Vector3 centre, Vector3 eye, float id, float alpha, Color frontier, float widthPx = 1.3f)
        {
            Vector3 v = eye - centre;
            v = v.sqrMagnitude > 1e-3f ? v.normalized : Vector3.forward;
            Vector3 u = Vector3.Cross(v, Mathf.Abs(v.y) > .95f ? Vector3.right : Vector3.up).normalized, w = Vector3.Cross(v, u);
            int stride = Mathf.Max(1, shape.Points.Length / 700);
            int bins = Mathf.Clamp(shape.Points.Length / 5 / stride, 24, reach.Length);
            Array.Clear(reach, 0, reach.Length);
            Array.Clear(front, 0, front.Length);
            Color mean = Color.black;
            int count = 0;
            for (int i = 0; i < shape.Points.Length; i += stride)
            {
                Vector3 p = shape.Points[i];
                float a = Vector3.Dot(p, u), c = Vector3.Dot(p, w), r = Mathf.Sqrt(a * a + c * c);
                int k = (int)((Mathf.Atan2(c, a) / (2 * Mathf.PI) + 1) * bins) % bins;
                if (r > reach[k]) reach[k] = r;
                if (shape.Frontier[i] > front[k]) front[k] = shape.Frontier[i];
                mean += shape.Colors[i];
                count++;
            }

            mean /= Mathf.Max(1, count);
            for (int pass = 0; pass < 3; pass++)
                for (int k = 0; k < bins; k++)
                    if (reach[k] <= 0) reach[k] = Mathf.Max(reach[(k + bins - 1) % bins], reach[(k + 1) % bins]);
            Color line = mean * 1.15f;
            line = Color.Lerp(line, Color.white, .12f);
            line.a = .62f * alpha;
            Vector3 prev = Vector3.zero;
            for (int k = 0; k <= bins; k++)
            {
                int kk = k % bins;
                float r = Mathf.Max(reach[kk], .5f * (reach[(kk + bins - 1) % bins] + reach[(kk + 1) % bins])) * 1.03f;
                float ang = (kk + .5f) * 2 * Mathf.PI / bins;
                Vector3 p = centre + (u * Mathf.Cos(ang) + w * Mathf.Sin(ang)) * r;
                if (k > 0)
                {
                    float f = front[kk];
                    Color fc = frontier;
                    fc.a = alpha;
                    b.AddSegment(prev, p, f > 0 ? Color.Lerp(line, fc, f) : line, widthPx + 1.8f * f, 0, id, f > 0 ? .95f + 4.5f * f * f : .95f);
                }

                prev = p;
            }

            return mean;
        }

        /// <summary>A dotted circle facing the eye (the market value's, a territory's seen edge-on): dashes across the view.</summary>
        public static void DottedCircle(LineMeshBuilder b, Vector3 centre, float radius, Vector3 eye, Color c, float widthPx, float id, float intensity)
        {
            Vector3 v = eye - centre;
            v = v.sqrMagnitude > 1e-3f ? v.normalized : Vector3.forward;
            Vector3 u = Vector3.Cross(v, Mathf.Abs(v.y) > .95f ? Vector3.right : Vector3.up).normalized, w = Vector3.Cross(v, u);
            int n = Mathf.Clamp(Mathf.RoundToInt(radius * 2.4f), 24, 160);
            for (int i = 0; i < n; i++)
            {
                float ang = i * 2 * Mathf.PI / n;
                Vector3 p = centre + (u * Mathf.Cos(ang) + w * Mathf.Sin(ang)) * radius, t = -u * Mathf.Sin(ang) + w * Mathf.Cos(ang);
                b.AddSegment(p - t * DotLength * .5f, p + t * DotLength * .5f, c, widthPx, 0, id, intensity);
            }
        }

        public static Vector3 RandomDir(System.Random rnd)
        {
            float z = (float)rnd.NextDouble() * 2 - 1, a = (float)rnd.NextDouble() * Mathf.PI * 2, s = Mathf.Sqrt(1 - z * z);
            return new Vector3(s * Mathf.Cos(a), z, s * Mathf.Sin(a));
        }
    }
}

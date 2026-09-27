using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Why.Director
{
    /// <summary>
    /// Anti-aliased vector primitives for uGUI meshes. Every shape gets a thin feathered rim whose alpha
    /// falls to zero, so hairline strokes stay smooth on an overlay canvas without MSAA.
    /// </summary>
    public static class UiStroke
    {
        /// <summary>Width of the feathered rim (canvas units).</summary>
        public const float Feather = 1.1f;

        /// <summary>
        /// A stroke along points [first, first + count) of a polyline, with a per-point alpha multiplier.
        /// Normals are averaged at interior points, which is smooth for gently curving lines.
        /// </summary>
        public static void Polyline(VertexHelper vh, List<Vector2> points, List<float> alpha, int first, int count,
            float halfWidth, Color color)
        {
            if (count < 2) return;
            int start = vh.currentVertCount;
            int last = first + count - 1;
            for (int i = first; i <= last; i++)
            {
                Vector2 prev = points[Mathf.Max(first, i - 1)];
                Vector2 next = points[Mathf.Min(last, i + 1)];
                Vector2 t = next - prev;
                t = t.sqrMagnitude > 1e-10f ? t.normalized : Vector2.right;
                Vector2 n = new Vector2(-t.y, t.x);
                Vector2 p = points[i];
                Color32 core = WithAlpha(color, color.a * alpha[i]);
                Color32 rim = WithAlpha(color, 0);
                vh.AddVert(p - n * (halfWidth + Feather), rim, Vector4.zero);
                vh.AddVert(p - n * halfWidth, core, Vector4.zero);
                vh.AddVert(p + n * halfWidth, core, Vector4.zero);
                vh.AddVert(p + n * (halfWidth + Feather), rim, Vector4.zero);
            }

            for (int i = 0; i < count - 1; i++)
            {
                int a = start + i * 4, b = a + 4;
                for (int lane = 0; lane < 3; lane++)
                {
                    vh.AddTriangle(a + lane, a + lane + 1, b + lane + 1);
                    vh.AddTriangle(a + lane, b + lane + 1, b + lane);
                }
            }
        }

        /// <summary>A circle outline.</summary>
        public static void Ring(VertexHelper vh, Vector2 center, float radius, float halfWidth, Color color,
            int segments = 48)
        {
            if (color.a <= 0.002f || radius <= 0) return;
            float r0 = Mathf.Max(0, radius - halfWidth - Feather), r1 = Mathf.Max(0, radius - halfWidth);
            float r2 = radius + halfWidth, r3 = radius + halfWidth + Feather;
            Color32 core = color, rim = WithAlpha(color, 0);
            int start = vh.currentVertCount;
            for (int i = 0; i <= segments; i++)
            {
                float a = i * (2f * Mathf.PI / segments);
                Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                vh.AddVert(center + d * r0, rim, Vector4.zero);
                vh.AddVert(center + d * r1, core, Vector4.zero);
                vh.AddVert(center + d * r2, core, Vector4.zero);
                vh.AddVert(center + d * r3, rim, Vector4.zero);
            }

            for (int i = 0; i < segments; i++)
            {
                int a = start + i * 4, b = a + 4;
                for (int lane = 0; lane < 3; lane++)
                {
                    vh.AddTriangle(a + lane, a + lane + 1, b + lane + 1);
                    vh.AddTriangle(a + lane, b + lane + 1, b + lane);
                }
            }
        }

        /// <summary>A filled circle.</summary>
        public static void Disc(VertexHelper vh, Vector2 center, float radius, Color color, int segments = 20)
        {
            if (color.a <= 0.002f || radius <= 0) return;
            Color32 core = color, rim = WithAlpha(color, 0);
            int c = vh.currentVertCount;
            vh.AddVert(center, core, Vector4.zero);
            for (int i = 0; i <= segments; i++)
            {
                float a = i * (2f * Mathf.PI / segments);
                Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                vh.AddVert(center + d * radius, core, Vector4.zero);
                vh.AddVert(center + d * (radius + Feather), rim, Vector4.zero);
            }

            for (int i = 0; i < segments; i++)
            {
                int a = c + 1 + i * 2, b = a + 2;
                vh.AddTriangle(c, a, b);
                vh.AddTriangle(a, a + 1, b + 1);
                vh.AddTriangle(a, b + 1, b);
            }
        }

        /// <summary>A filled triangle (arrowheads).</summary>
        public static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            if (color.a <= 0.002f) return;
            Vector2 g = (a + b + c) / 3f;
            Color32 core = color, rim = WithAlpha(color, 0);
            int s = vh.currentVertCount;
            vh.AddVert(a, core, Vector4.zero);
            vh.AddVert(b, core, Vector4.zero);
            vh.AddVert(c, core, Vector4.zero);
            vh.AddVert(a + (a - g).normalized * (Feather * 2f), rim, Vector4.zero);
            vh.AddVert(b + (b - g).normalized * (Feather * 2f), rim, Vector4.zero);
            vh.AddVert(c + (c - g).normalized * (Feather * 2f), rim, Vector4.zero);
            vh.AddTriangle(s, s + 1, s + 2);
            for (int i = 0; i < 3; i++)
            {
                int j = (i + 1) % 3;
                vh.AddTriangle(s + i, s + 3 + i, s + 3 + j);
                vh.AddTriangle(s + i, s + 3 + j, s + j);
            }
        }

        static Color32 WithAlpha(Color c, float a)
        {
            c.a = Mathf.Clamp01(a);
            return c;
        }
    }
}

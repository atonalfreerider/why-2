using System;

namespace Why
{
    /// <summary>
    /// Pure C# rasterizer of the procedural icon atlas (no UnityEngine dependency, so it runs on worker threads
    /// and renders outside Unity). Glyphs are white monochrome shapes stored as 8-bit alpha coverage. Every
    /// primitive is a signed distance function in glyph units, turned into coverage with a one-pixel linear
    /// ramp (analytic anti-aliasing); shapes combine in drawing order by union (max) or erase (min with the
    /// complement), so a glyph reads like a small vector drawing.
    /// </summary>
    /// <remarks>
    /// Glyph units: the cell's content box (the cell minus <see cref="Margin"/> on every side) spans
    /// [-1, 1] on both axes, y up. One stroke family for all glyphs (<see cref="Stroke"/>, <see cref="Thin"/>,
    /// <see cref="Hair"/>) keeps the set visually consistent; filled glyphs stay a little smaller than outlined
    /// ones so they share an optical size.
    /// </remarks>
    public static class IconRaster
    {
        /// <summary>Cell size in pixels (one icon per cell).</summary>
        public const int CellPx = 128;

        /// <summary>Cells per atlas row and column.</summary>
        public const int Columns = 4;

        /// <summary>Atlas side in pixels.</summary>
        public const int AtlasPx = CellPx * Columns;

        /// <summary>Empty border around each glyph as a fraction of the cell (keeps mip levels from bleeding).</summary>
        public const float Margin = 0.08f;

        /// <summary>Icon ids in atlas order (cell i at column i % Columns, row i / Columns from the top). Must match the constants in <c>Icons</c>.</summary>
        public static readonly string[] Ids =
        {
            "universe", "cosmic_web", "first_stars", "laniakea",
            "supercluster", "galaxy_group", "galaxy", "nebula",
            "sun", "planets", "earth", "moon"
        };

        const float Stroke = 0.15f; // main outline weight (~1.2 px at 20 px)
        const float Thin = 0.105f;  // secondary lines
        const float Hair = 0.075f;  // filaments, tapered tips

        static readonly Action<Cell>[] Glyphs =
        {
            Universe, CosmicWeb, FirstStars, Laniakea,
            Supercluster, GalaxyGroup, Galaxy, Nebula,
            Sun, Planets, Earth, Moon
        };

        static readonly Lazy<byte[][]> mips = new Lazy<byte[][]>(() => BuildMips(RenderAtlas(), AtlasPx));

        /// <summary>The atlas and its full mip chain (level 0 first, alpha only, row 0 at the bottom), rendered once on first use. Thread safe.</summary>
        public static byte[][] Mips => mips.Value;

        /// <summary>Atlas index of an icon id, or -1.</summary>
        public static int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < Ids.Length; i++)
            {
                if (string.Equals(Ids[i], id, StringComparison.Ordinal)) return i;
            }

            return -1;
        }

        /// <summary>Bottom-left pixel of an icon's cell in the atlas (texture space, row 0 at the bottom).</summary>
        public static void CellOrigin(int index, out int x, out int y)
        {
            x = index % Columns * CellPx;
            y = AtlasPx - (index / Columns + 1) * CellPx;
        }

        /// <summary>Renders every glyph into a fresh atlas: alpha coverage, <see cref="AtlasPx"/> squared, row 0 at the bottom.</summary>
        public static byte[] RenderAtlas()
        {
            byte[] atlas = new byte[AtlasPx * AtlasPx];
            for (int i = 0; i < Glyphs.Length; i++)
            {
                Cell cell = new Cell();
                Glyphs[i](cell);
                CellOrigin(i, out int ox, out int oy);
                for (int y = 0; y < CellPx; y++)
                {
                    int row = (oy + y) * AtlasPx + ox;
                    for (int x = 0; x < CellPx; x++)
                    {
                        atlas[row + x] = (byte)(cell.A[y * CellPx + x] * 255f + 0.5f);
                    }
                }
            }

            return atlas;
        }

        /// <summary>
        /// Full mip chain by 2x2 box filtering (an area average, the ideal reduction of coverage). Cells are
        /// power-of-two aligned, so no level mixes two cells until a cell is a single texel.
        /// </summary>
        public static byte[][] BuildMips(byte[] level0, int size)
        {
            int count = 1;
            for (int s = size; s > 1; s >>= 1) count++;
            byte[][] levels = new byte[count][];
            levels[0] = level0;
            for (int l = 1; l < count; l++)
            {
                int src = size >> (l - 1), dst = src >> 1;
                byte[] a = levels[l - 1], b = new byte[dst * dst];
                for (int y = 0; y < dst; y++)
                {
                    for (int x = 0; x < dst; x++)
                    {
                        int i = 2 * y * src + 2 * x;
                        b[y * dst + x] = (byte)((a[i] + a[i + 1] + a[i + src] + a[i + src + 1] + 2) >> 2);
                    }
                }

                levels[l] = b;
            }

            return levels;
        }

        // ---------------------------------------------------------------- glyphs

        /// <summary>Big Bang: tapered rays of varied length bursting from a bright core.</summary>
        static void Universe(Cell c)
        {
            float[] length = { 0.94f, 0.64f, 0.84f, 0.58f, 0.9f, 0.68f, 0.86f, 0.6f, 0.92f, 0.66f, 0.8f, 0.62f };
            for (int i = 0; i < length.Length; i++)
            {
                double a = Math.PI / 2 + i * 2 * Math.PI / length.Length;
                float x = (float)Math.Cos(a), y = (float)Math.Sin(a);
                c.Segment(0, 0, length[i] * x, length[i] * y, 0.2f, 0.06f);
            }

            c.Disc(0, 0, 0.26f);
        }

        /// <summary>Intergalactic gas: a few nodes joined by thin filaments.</summary>
        static void CosmicWeb(Cell c)
        {
            float[] x = { -0.06f, -0.7f, 0.28f, 0.78f, 0.4f, -0.58f };
            float[] y = { -0.02f, 0.46f, 0.72f, -0.1f, -0.76f, -0.58f };
            float[] r = { 0.19f, 0.13f, 0.12f, 0.14f, 0.12f, 0.13f };
            int[] edges = { 0, 1, 0, 2, 0, 4, 0, 5, 2, 3, 3, 4, 1, 5, 1, 2 };
            for (int e = 0; e < edges.Length; e += 2)
            {
                int a = edges[e], b = edges[e + 1];
                c.Segment(x[a], y[a], x[b], y[b], Thin, Thin);
            }

            for (int i = 0; i < x.Length; i++) c.Disc(x[i], y[i], r[i]);
        }

        /// <summary>First stars: a four-pointed sparkle and a small companion.</summary>
        static void FirstStars(Cell c)
        {
            c.Sparkle(-0.14f, -0.12f, 0.64f, 0.84f, 3f);
            c.Sparkle(0.6f, 0.6f, 0.24f, 0.3f, 3f);
        }

        /// <summary>Laniakea: streamlines of galaxy flow converging on one attractor.</summary>
        static void Laniakea(Cell c)
        {
            const float ox = 0.12f, px = 0.52f + ox, py = 0;
            float[] start = { 104, 142, 180, 218, 256 };
            float[] reach = { 0.9f, 0.88f, 0.92f, 0.88f, 0.9f };
            for (int i = 0; i < start.Length; i++)
            {
                double a = start[i] * Math.PI / 180;
                float sx = ox + reach[i] * (float)Math.Cos(a), sy = reach[i] * (float)Math.Sin(a);
                // leave the rim heading inward, arrive horizontally at the attractor
                float c1x = ox + (sx - ox) * 0.55f, c1y = sy * 0.55f;
                c.Bezier(sx, sy, c1x, c1y, px - 0.5f, py, px, py, Thin, 0.15f);
            }

            c.Disc(px, py, 0.17f);
        }

        /// <summary>Virgo Supercluster: a clump of dots of varied sizes, denser toward the centre.</summary>
        static void Supercluster(Cell c)
        {
            const int n = 11;
            for (int k = 0; k < n; k++)
            {
                float f = (k + 0.5f) / n;
                float r = 0.82f * (float)Math.Pow(f, 0.75);
                double a = k * 2.39996 + 0.35 * Math.Sin(k * 12.9898);
                float size = 0.19f + (0.095f - 0.19f) * f;
                // lifted a little: the outermost dots fall low, and this centres the clump's box
                c.Disc(r * (float)Math.Cos(a), 0.065f + r * (float)Math.Sin(a), size);
            }
        }

        /// <summary>Local Group: a larger and a smaller spiral with a few dwarf companions.</summary>
        static void GalaxyGroup(Cell c)
        {
            c.Spiral(-0.24f, -0.14f, 0.68f, 0.4f, 1f, 0.17f, 0.16f, 0.085f, 0.95f);
            c.Spiral(0.54f, 0.54f, 0.42f, 2.2f, 0.62f, 0.13f, 0.13f, 0.08f, 0.8f);
            c.Disc(0.74f, -0.44f, 0.1f);
            c.Disc(-0.74f, 0.6f, 0.09f);
            c.Disc(0.26f, -0.8f, 0.075f);
        }

        /// <summary>Milky Way: a two-arm logarithmic spiral around a bright bulge.</summary>
        static void Galaxy(Cell c)
        {
            c.Spiral(0, 0, 1f, -0.47f, 1f, 0.25f, 0.19f, 0.08f, 1.2f);
        }

        /// <summary>Solar nebula: a young star inside a flattened, tilted disk.</summary>
        static void Nebula(Cell c)
        {
            const float tilt = -0.35f;
            c.EllipseRing(0, 0, 0.93f, 0.38f, tilt, Stroke);
            c.EllipseRing(0, 0, 0.58f, 0.22f, tilt, Thin);
            c.EraseDisc(0, 0, 0.32f);
            c.Disc(0, 0, 0.22f);
        }

        /// <summary>Sun: a filled disc with eight short rays.</summary>
        static void Sun(Cell c)
        {
            c.Disc(0, 0, 0.44f);
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4;
                float x = (float)Math.Cos(a), y = (float)Math.Sin(a);
                c.Segment(0.64f * x, 0.64f * y, 0.9f * x, 0.9f * y, Stroke, Stroke);
            }
        }

        /// <summary>Planets: a planet with a tilted ring (Saturn).</summary>
        static void Planets(Cell c)
        {
            const float tilt = 0.4f, rx = 0.92f, ry = 0.3f, ring = 0.12f;
            c.EllipseArc(0, 0, rx, ry, tilt, 0, Math.PI, ring); // back half, hidden where it crosses the planet
            c.Disc(0, 0, 0.52f);
            // a gap where the front half crosses the planet (only there, so the ring tips stay whole)
            c.EraseEllipseArc(0, 0, rx, ry, tilt, Math.PI + 0.6, 2 * Math.PI - 0.6, ring + 0.12f);
            c.EllipseArc(0, 0, rx, ry, tilt, Math.PI, 2 * Math.PI, ring);
        }

        /// <summary>Earth: a globe with its equator, one meridian and the latitudes.</summary>
        static void Earth(Cell c)
        {
            const float r = 0.88f;
            c.Ring(0, 0, r, Stroke);
            c.EllipseRing(0, 0, 0.4f, r, 0, Thin);
            c.Segment(-r, 0, r, 0, Thin, Thin);
            float h = 0.5f, w = (float)Math.Sqrt(r * r - h * h);
            c.Segment(-w, h, w, h, Hair, Hair);
            c.Segment(-w, -h, w, -h, Hair, Hair);
        }

        /// <summary>Moon: a crescent.</summary>
        static void Moon(Cell c)
        {
            c.Disc(0.05f, 0.02f, 0.84f);
            c.EraseDisc(0.45f, 0.32f, 0.72f);
        }

        // ---------------------------------------------------------------- canvas

        /// <summary>One cell's coverage buffer with the drawing primitives, in glyph units.</summary>
        sealed class Cell
        {
            /// <summary>Coverage, row 0 at the bottom.</summary>
            public readonly float[] A = new float[CellPx * CellPx];

            const float Scale = CellPx * (1 - 2 * Margin) * 0.5f; // pixels per glyph unit
            const float Half = CellPx * 0.5f;

            /// <summary>Paints a shape given by its signed distance (glyph units) over a bounding box.</summary>
            void Paint(float minX, float minY, float maxX, float maxY, Func<float, float, float> sdf, bool erase)
            {
                int x0 = Math.Max(0, (int)Math.Floor(minX * Scale + Half) - 2);
                int x1 = Math.Min(CellPx - 1, (int)Math.Ceiling(maxX * Scale + Half) + 2);
                int y0 = Math.Max(0, (int)Math.Floor(minY * Scale + Half) - 2);
                int y1 = Math.Min(CellPx - 1, (int)Math.Ceiling(maxY * Scale + Half) + 2);
                for (int py = y0; py <= y1; py++)
                {
                    float gy = (py + 0.5f - Half) / Scale;
                    for (int px = x0; px <= x1; px++)
                    {
                        float gx = (px + 0.5f - Half) / Scale;
                        float cov = 0.5f - sdf(gx, gy) * Scale;
                        if (cov <= 0) continue;
                        if (cov > 1) cov = 1;
                        int i = py * CellPx + px;
                        if (erase) A[i] = Math.Min(A[i], 1 - cov);
                        else if (cov > A[i]) A[i] = cov;
                    }
                }
            }

            public void Disc(float cx, float cy, float r) =>
                Paint(cx - r, cy - r, cx + r, cy + r, (x, y) => Length(x - cx, y - cy) - r, false);

            public void EraseDisc(float cx, float cy, float r) =>
                Paint(cx - r, cy - r, cx + r, cy + r, (x, y) => Length(x - cx, y - cy) - r, true);

            /// <summary>Circle outline of radius r (centre of the stroke) and full width w.</summary>
            public void Ring(float cx, float cy, float r, float w)
            {
                float e = r + w;
                Paint(cx - e, cy - e, cx + e, cy + e, (x, y) => Math.Abs(Length(x - cx, y - cy) - r) - 0.5f * w, false);
            }

            /// <summary>Round-capped segment whose full width tapers linearly from wa to wb.</summary>
            public void Segment(float ax, float ay, float bx, float by, float wa, float wb) =>
                SegmentPaint(ax, ay, bx, by, wa, wb, false);

            void SegmentPaint(float ax, float ay, float bx, float by, float wa, float wb, bool erase)
            {
                float e = 0.5f * Math.Max(wa, wb);
                float dx = bx - ax, dy = by - ay, len2 = Math.Max(dx * dx + dy * dy, 1e-12f);
                Paint(Math.Min(ax, bx) - e, Math.Min(ay, by) - e, Math.Max(ax, bx) + e, Math.Max(ay, by) + e, (x, y) =>
                {
                    float wx = x - ax, wy = y - ay;
                    float t = Clamp01((wx * dx + wy * dy) / len2);
                    return Length(wx - dx * t, wy - dy * t) - 0.5f * (wa + (wb - wa) * t);
                }, erase);
            }

            /// <summary>Polyline through the points with a per-point full width; a union of tapered capsules.</summary>
            public void Polyline(float[] xs, float[] ys, float[] ws, bool erase = false)
            {
                for (int i = 1; i < xs.Length; i++) SegmentPaint(xs[i - 1], ys[i - 1], xs[i], ys[i], ws[i - 1], ws[i], erase);
            }

            /// <summary>Cubic Bezier stroke with a width tapering from w0 to w1.</summary>
            public void Bezier(float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3, float w0, float w1)
            {
                const int n = 32;
                float[] xs = new float[n + 1], ys = new float[n + 1], ws = new float[n + 1];
                for (int i = 0; i <= n; i++)
                {
                    float t = (float)i / n, s = 1 - t;
                    float b0 = s * s * s, b1 = 3 * s * s * t, b2 = 3 * s * t * t, b3 = t * t * t;
                    xs[i] = b0 * x0 + b1 * x1 + b2 * x2 + b3 * x3;
                    ys[i] = b0 * y0 + b1 * y1 + b2 * y2 + b3 * y3;
                    ws[i] = w0 + (w1 - w0) * t;
                }

                Polyline(xs, ys, ws);
            }

            /// <summary>Arc of a rotated ellipse (parameter t from t0 to t1) with full stroke width w.</summary>
            public void EllipseArc(float cx, float cy, float rx, float ry, float rotation, double t0, double t1, float w) =>
                EllipsePath(cx, cy, rx, ry, rotation, t0, t1, w, false);

            public void EraseEllipseArc(float cx, float cy, float rx, float ry, float rotation, double t0, double t1, float w) =>
                EllipsePath(cx, cy, rx, ry, rotation, t0, t1, w, true);

            public void EllipseRing(float cx, float cy, float rx, float ry, float rotation, float w) =>
                EllipsePath(cx, cy, rx, ry, rotation, 0, 2 * Math.PI, w, false);

            void EllipsePath(float cx, float cy, float rx, float ry, float rotation, double t0, double t1, float w, bool erase)
            {
                int n = Math.Max(8, (int)(Math.Abs(t1 - t0) / (2 * Math.PI) * 96));
                float[] xs = new float[n + 1], ys = new float[n + 1], ws = new float[n + 1];
                float cr = (float)Math.Cos(rotation), sr = (float)Math.Sin(rotation);
                for (int i = 0; i <= n; i++)
                {
                    double t = t0 + (t1 - t0) * i / n;
                    float ex = rx * (float)Math.Cos(t), ey = ry * (float)Math.Sin(t);
                    xs[i] = cx + ex * cr - ey * sr;
                    ys[i] = cy + ex * sr + ey * cr;
                    ws[i] = w;
                }

                Polyline(xs, ys, ws, erase);
            }

            /// <summary>
            /// Two-arm logarithmic spiral with a bulge. radius is the outer extent, rotation the start angle,
            /// flat squashes it vertically (inclination), arms taper from w0 to w1 over the given turns.
            /// </summary>
            public void Spiral(float cx, float cy, float radius, float rotation, float flat, float bulge, float w0, float w1,
                float turns)
            {
                Paint(cx - bulge, cy - bulge, cx + bulge, cy + bulge,
                    (x, y) => Length(x - cx, (y - cy) / flat) * Math.Min(1, flat) - bulge * Math.Min(1, flat), false);
                const int n = 64;
                float r0 = bulge * 0.8f, r1 = radius - 0.5f * w1;
                double sweep = turns * 2 * Math.PI, b = Math.Log(r1 / r0) / sweep;
                float[] xs = new float[n + 1], ys = new float[n + 1], ws = new float[n + 1];
                for (int arm = 0; arm < 2; arm++)
                {
                    for (int i = 0; i <= n; i++)
                    {
                        double th = sweep * i / n;
                        double r = r0 * Math.Exp(b * th), a = rotation + arm * Math.PI + th;
                        xs[i] = cx + (float)(r * Math.Cos(a));
                        ys[i] = cy + (float)(r * Math.Sin(a)) * flat;
                        ws[i] = w0 + (w1 - w0) * i / n;
                    }

                    Polyline(xs, ys, ws);
                }
            }

            /// <summary>Four-pointed sparkle with half-sizes rx, ry; higher sharpness pinches the waist.</summary>
            public void Sparkle(float cx, float cy, float rx, float ry, float sharpness)
            {
                const int n = 128;
                float[] xs = new float[n], ys = new float[n];
                for (int i = 0; i < n; i++)
                {
                    double t = 2 * Math.PI * i / n;
                    double co = Math.Cos(t), si = Math.Sin(t);
                    xs[i] = cx + rx * (float)(Math.Sign(co) * Math.Pow(Math.Abs(co), sharpness));
                    ys[i] = cy + ry * (float)(Math.Sign(si) * Math.Pow(Math.Abs(si), sharpness));
                }

                Polygon(xs, ys);
            }

            /// <summary>Filled polygon (even-odd) with its exact signed distance.</summary>
            public void Polygon(float[] xs, float[] ys)
            {
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                for (int i = 0; i < xs.Length; i++)
                {
                    minX = Math.Min(minX, xs[i]);
                    maxX = Math.Max(maxX, xs[i]);
                    minY = Math.Min(minY, ys[i]);
                    maxY = Math.Max(maxY, ys[i]);
                }

                Paint(minX, minY, maxX, maxY, (x, y) =>
                {
                    float best = float.MaxValue;
                    bool inside = false;
                    for (int i = 0, j = xs.Length - 1; i < xs.Length; j = i++)
                    {
                        float ex = xs[i] - xs[j], ey = ys[i] - ys[j];
                        float wx = x - xs[j], wy = y - ys[j];
                        float t = Clamp01((wx * ex + wy * ey) / Math.Max(ex * ex + ey * ey, 1e-12f));
                        float qx = wx - ex * t, qy = wy - ey * t;
                        best = Math.Min(best, qx * qx + qy * qy);
                        if ((ys[j] > y) != (ys[i] > y) && x < ex * (y - ys[j]) / ey + xs[j]) inside = !inside;
                    }

                    float d = (float)Math.Sqrt(best);
                    return inside ? -d : d;
                }, false);
            }

            static float Length(float x, float y) => (float)Math.Sqrt(x * x + y * y);

            static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
        }
    }
}

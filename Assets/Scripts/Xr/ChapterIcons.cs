using System;
using System.Collections.Generic;
using UnityEngine;

namespace Why.Xr
{
    /// <summary>
    /// Line-art icons for the chapters of the guided tour (one per view) and the menu's controls, drawn once as signed
    /// distance fields into white-on-transparent sprites and tinted by the graph's level colors: red matter, green life,
    /// blue humans; gold, rose and ice in the economy. Unknown ids get a plain ring.
    /// </summary>
    public static class ChapterIcons
    {
        const int Size = 128;
        const float W = 0.055f; // stroke half width, in icon units (the icon spans -1 .. 1)

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>The icon of a view preset or control id ("play", "pause", "back", "next", "stop", "scene", "recenter").</summary>
        public static Sprite Get(string id)
        {
            id ??= "";
            if (cache.TryGetValue(id, out Sprite s)) return s;
            Func<Vector2, float> shape = Shape(id);
            Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            Color32[] px = new Color32[Size * Size];
            float aa = 2.4f / Size;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / Size * 2 - 1, (y + 0.5f) / Size * 2 - 1);
                float d = shape(p);
                byte a = (byte)(Mathf.Clamp01(0.5f - d / aa) * 255);
                px[y * Size + x] = new Color32(255, 255, 255, a);
            }

            tex.SetPixels32(px);
            tex.Apply(true, true);
            s = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100);
            cache[id] = s;
            return s;
        }

        /// <summary>The level color of a chapter: what part of the graph the view is about.</summary>
        public static Color Tint(string id)
        {
            switch (id)
            {
                case "cosmos": case "stars": case "earth": return GraphStyle.Matter;
                case "life": case "complex_life": case "mammals": case "hominins": return GraphStyle.Life;
                case "prehistory": case "civilizations": case "modern": case "smv": case "present": case "footprint":
                case "people": case "section": case "society": case "betrayal": case "atom": case "ladder":
                    return GraphStyle.Humans;
                case "capture": case "companies": case "foundation": return new Color(1f, 0.82f, 0.45f);
                case "rivers": case "mind": case "dilemma": return new Color(1f, 0.62f, 0.78f);
                default: return new Color(0.9f, 0.92f, 1f);
            }
        }

        // ------------------------------------------------------------------ shapes (distance to the ink, < 0 inside)

        static Func<Vector2, float> Shape(string id)
        {
            switch (id)
            {
                // the causality graph
                case "overview": return p => Spiral(p);
                case "cosmos": return p => Min(Disc(p, Vector2.zero, 0.16f), Rays(p, 8, 0.32f, 0.82f));
                case "stars": return p => Star(p, 5, 0.78f, 0.34f);
                case "earth": return p => Min(Ring(p, Vector2.zero, 0.42f), Ring(p, Vector2.zero, 0.72f, 0.035f), Disc(p, new Vector2(0.72f, 0.06f), 0.1f));
                case "life": return p => Min(Ring(p, Vector2.zero, 0.7f), Disc(p, new Vector2(0.12f, 0.08f), 0.2f));
                case "complex_life": return p => Fish(p);
                case "mammals": return p => Tree(p);
                case "hominins": return p => Figure(p, 0f, 1f);
                case "prehistory": return p => Flame(p);
                case "civilizations": return p => Min(Seg(p, new Vector2(-0.75f, -0.6f), new Vector2(0.75f, -0.6f)), Seg(p, new Vector2(-0.75f, -0.6f), new Vector2(0, 0.7f)), Seg(p, new Vector2(0.75f, -0.6f), new Vector2(0, 0.7f)), Seg(p, new Vector2(-0.38f, 0.05f), new Vector2(0.38f, 0.05f)));
                case "modern": return p => Gear(p);
                case "footprint": return p => Min(Disc(p, new Vector2(0, -0.2f), 0.34f), Disc(p, new Vector2(-0.42f, 0.38f), 0.11f), Disc(p, new Vector2(-0.15f, 0.55f), 0.11f), Disc(p, new Vector2(0.15f, 0.55f), 0.11f), Disc(p, new Vector2(0.42f, 0.38f), 0.11f));
                case "smv": return p => Min(Figure(p, -0.36f, 0.8f), Figure(p, 0.36f, 0.8f));
                case "present": return p => Min(Disc(p, Vector2.zero, 0.14f), Ring(p, Vector2.zero, 0.45f), Ring(p, Vector2.zero, 0.76f, 0.035f));
                case "stars_formation": return p => Star(p, 4, 0.7f, 0.25f);
                // the economy
                case "landscape": return p => Min(Hill(p, -0.35f, 0.55f), Hill(p, 0.35f, 0.85f));
                case "foundation": return p => Min(Seg(p, new Vector2(-0.8f, -0.7f), new Vector2(0.8f, -0.7f)), Seg(p, new Vector2(-0.8f, 0.6f), new Vector2(0.8f, 0.6f)), Seg(p, new Vector2(-0.5f, -0.7f), new Vector2(-0.5f, 0.6f)), Seg(p, new Vector2(0, -0.7f), new Vector2(0, 0.6f)), Seg(p, new Vector2(0.5f, -0.7f), new Vector2(0.5f, 0.6f)));
                case "companies": return p => Min(Box(p, new Vector2(-0.3f, -0.1f), new Vector2(0.22f, 0.75f)), Box(p, new Vector2(0.32f, -0.35f), new Vector2(0.2f, 0.5f)));
                case "capture": return p => Min(Ring(p, Vector2.zero, 0.62f), Dots(p, 8, 0.62f, 0.1f), Disc(p, Vector2.zero, 0.2f));
                case "people": return p => Figure(p, 0f, 1f);
                case "rivers": return p => Min(Wave(p, 0.35f), Wave(p, -0.05f), Wave(p, -0.45f));
                case "mind": return p => Min(Ring(p, new Vector2(-0.25f, 0.05f), 0.42f), Ring(p, new Vector2(0.25f, 0.05f), 0.42f), Seg(p, new Vector2(0, -0.6f), new Vector2(0, -0.85f)));
                case "atom": return p => Min(Disc(p, Vector2.zero, 0.15f), Ellipse(p, 0f), Ellipse(p, 60f), Ellipse(p, -60f));
                case "dilemma": return p => Min(Seg(p, new Vector2(-0.8f, 0.3f), new Vector2(0.4f, 0.3f)), Seg(p, new Vector2(0.4f, 0.3f), new Vector2(0.15f, 0.55f)), Seg(p, new Vector2(0.8f, -0.3f), new Vector2(-0.4f, -0.3f)), Seg(p, new Vector2(-0.4f, -0.3f), new Vector2(-0.15f, -0.55f)));
                case "ladder": return p => Min(Seg(p, new Vector2(-0.4f, -0.85f), new Vector2(-0.25f, 0.85f)), Seg(p, new Vector2(0.4f, -0.85f), new Vector2(0.55f, 0.85f)), Rungs(p));
                case "society": return p => Min(Disc(p, new Vector2(0, 0.55f), 0.15f), Disc(p, new Vector2(-0.55f, -0.4f), 0.15f), Disc(p, new Vector2(0.55f, -0.4f), 0.15f), Seg(p, new Vector2(0, 0.55f), new Vector2(-0.55f, -0.4f)), Seg(p, new Vector2(0, 0.55f), new Vector2(0.55f, -0.4f)), Seg(p, new Vector2(-0.55f, -0.4f), new Vector2(0.55f, -0.4f)));
                case "section": return p => Min(Box(p, Vector2.zero, new Vector2(0.75f, 0.55f)), Seg(p, new Vector2(-0.75f, 0), new Vector2(0.75f, 0)));
                // controls
                case "play": return p => Tri(p, new Vector2(-0.35f, -0.55f), new Vector2(-0.35f, 0.55f), new Vector2(0.6f, 0));
                case "pause": return p => Min(Bar(p, -0.25f), Bar(p, 0.25f));
                case "next": return p => Min(Tri(p, new Vector2(-0.55f, -0.5f), new Vector2(-0.55f, 0.5f), new Vector2(0.25f, 0)), Bar(p, 0.45f));
                case "back": return p => Min(Tri(p, new Vector2(0.55f, -0.5f), new Vector2(0.55f, 0.5f), new Vector2(-0.25f, 0)), Bar(p, -0.45f));
                case "stop": return p => Fill(Box(p, Vector2.zero, new Vector2(0.45f, 0.45f)));
                case "scene": return p => Min(Ring(p, new Vector2(-0.3f, 0), 0.38f), Hill(p, 0.35f, 0.5f));
                case "recenter": return p => Min(Ring(p, Vector2.zero, 0.55f), Seg(p, new Vector2(0, 0.35f), new Vector2(0, 0.9f)), Seg(p, new Vector2(0, -0.35f), new Vector2(0, -0.9f)), Seg(p, new Vector2(0.35f, 0), new Vector2(0.9f, 0)), Seg(p, new Vector2(-0.35f, 0), new Vector2(-0.9f, 0)));
                default: return p => Ring(p, Vector2.zero, 0.6f);
            }
        }

        static float Min(params float[] d)
        {
            float m = float.MaxValue;
            foreach (float v in d) m = Mathf.Min(m, v);
            return m;
        }

        static float Fill(float d) => d; // boxes and discs are filled already; strokes use Seg / Ring

        static float Disc(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        static float Ring(Vector2 p, Vector2 c, float r, float w = W) => Mathf.Abs((p - c).magnitude - r) - w;

        static float Seg(Vector2 p, Vector2 a, Vector2 b, float w = W)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return (p - (a + ab * t)).magnitude - w;
        }

        static float Box(Vector2 p, Vector2 c, Vector2 half)
        {
            Vector2 d = new Vector2(Mathf.Abs(p.x - c.x) - half.x, Mathf.Abs(p.y - c.y) - half.y);
            float outside = new Vector2(Mathf.Max(d.x, 0), Mathf.Max(d.y, 0)).magnitude;
            float inside = Mathf.Min(Mathf.Max(d.x, d.y), 0);
            return Mathf.Abs(outside + inside) - W; // outline
        }

        static float Bar(Vector2 p, float x) => Seg(p, new Vector2(x, -0.5f), new Vector2(x, 0.5f), 0.1f);

        static float Tri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            // filled triangle: inside when on the same side of all edges
            float e0 = Edge(p, a, b), e1 = Edge(p, b, c), e2 = Edge(p, c, a);
            bool inside = (e0 >= 0 && e1 >= 0 && e2 >= 0) || (e0 <= 0 && e1 <= 0 && e2 <= 0);
            float d = Min(Seg(p, a, b, 0), Seg(p, b, c, 0), Seg(p, c, a, 0));
            return inside ? -d : d;
        }

        static float Edge(Vector2 p, Vector2 a, Vector2 b) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

        /// <summary>The graph itself: the clock of deep time turning three quarters, then the straight human branch.</summary>
        static float Spiral(Vector2 p)
        {
            float d = float.MaxValue;
            Vector2 prev = Vector2.zero;
            const int n = 64;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                float a = Mathf.PI * 1.5f - t * Mathf.PI * 1.5f; // 6 o'clock clockwise to 3 o'clock
                float r = 0.18f + 0.5f * t;
                Vector2 q = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r - 0.05f);
                if (i > 0) d = Mathf.Min(d, Seg(p, prev, q));
                prev = q;
            }

            // the branch leaves the circle straight
            d = Mathf.Min(d, Seg(p, prev, prev + new Vector2(0, 0.42f)));
            return Mathf.Min(d, Disc(p, prev + new Vector2(0, 0.42f), 0.1f));
        }

        static float Rays(Vector2 p, int count, float r0, float r1)
        {
            float d = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2 / count;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                d = Mathf.Min(d, Seg(p, dir * r0, dir * r1));
            }

            return d;
        }

        static float Dots(Vector2 p, int count, float r, float size)
        {
            float d = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2 / count;
                d = Mathf.Min(d, Disc(p, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, size));
            }

            return d;
        }

        static float Star(Vector2 p, int points, float outer, float inner)
        {
            float d = float.MaxValue;
            Vector2 prev = Vector2.zero, first = Vector2.zero;
            for (int i = 0; i <= points * 2; i++)
            {
                float a = Mathf.PI / 2 + i * Mathf.PI / points;
                float r = i % 2 == 0 ? outer : inner;
                Vector2 q = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (i > 0) d = Mathf.Min(d, Seg(p, prev, q));
                else first = q;
                prev = q;
            }

            return d;
        }

        static float Fish(Vector2 p)
        {
            Vector2 q = new Vector2(p.x / 0.75f, p.y / 0.42f);
            float body = Mathf.Abs(q.magnitude - 1) * 0.42f - W;
            return Min(body, Seg(p, new Vector2(0.72f, 0), new Vector2(0.95f, 0.32f)), Seg(p, new Vector2(0.72f, 0), new Vector2(0.95f, -0.32f)),
                Disc(p, new Vector2(-0.42f, 0.1f), 0.07f));
        }

        static float Tree(Vector2 p) =>
            Min(Seg(p, new Vector2(0, -0.85f), new Vector2(0, 0.1f)), Seg(p, new Vector2(0, -0.2f), new Vector2(-0.55f, 0.45f)),
                Seg(p, new Vector2(0, 0.1f), new Vector2(0.5f, 0.7f)), Seg(p, new Vector2(0, 0.1f), new Vector2(-0.05f, 0.8f)),
                Seg(p, new Vector2(-0.3f, 0.12f), new Vector2(-0.6f, 0.05f)));

        static float Figure(Vector2 p, float x, float s) =>
            Min(Ring(p, new Vector2(x, 0.52f * s), 0.18f * s), Seg(p, new Vector2(x, 0.3f * s), new Vector2(x, -0.3f * s)),
                Seg(p, new Vector2(x, -0.3f * s), new Vector2(x - 0.25f * s, -0.82f * s)), Seg(p, new Vector2(x, -0.3f * s), new Vector2(x + 0.25f * s, -0.82f * s)),
                Seg(p, new Vector2(x - 0.32f * s, 0.08f * s), new Vector2(x + 0.32f * s, 0.08f * s)));

        static float Flame(Vector2 p)
        {
            float d = float.MaxValue;
            Vector2 prev = Vector2.zero;
            for (int i = 0; i <= 48; i++)
            {
                float t = i / 48f * Mathf.PI * 2;
                float r = 0.45f;
                Vector2 q = new Vector2(Mathf.Sin(t) * r * (0.6f + 0.4f * Mathf.Cos(t)), -0.3f - Mathf.Cos(t) * 0.5f + (1 - Mathf.Cos(t)) * 0.18f);
                if (i > 0) d = Mathf.Min(d, Seg(p, prev, q));
                prev = q;
            }

            return d;
        }

        static float Gear(Vector2 p)
        {
            float d = Min(Ring(p, Vector2.zero, 0.5f), Ring(p, Vector2.zero, 0.18f));
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                d = Mathf.Min(d, Seg(p, dir * 0.55f, dir * 0.8f, 0.09f));
            }

            return d;
        }

        static float Hill(Vector2 p, float x, float h)
        {
            float d = float.MaxValue;
            Vector2 prev = Vector2.zero;
            for (int i = 0; i <= 24; i++)
            {
                float t = i / 24f;
                Vector2 q = new Vector2(x - 0.55f + 1.1f * t, -0.6f + h * Mathf.Sin(t * Mathf.PI));
                if (i > 0) d = Mathf.Min(d, Seg(p, prev, q));
                prev = q;
            }

            return d;
        }

        static float Wave(Vector2 p, float y)
        {
            float d = float.MaxValue;
            Vector2 prev = Vector2.zero;
            for (int i = 0; i <= 24; i++)
            {
                float t = i / 24f;
                Vector2 q = new Vector2(-0.8f + 1.6f * t, y + 0.12f * Mathf.Sin(t * Mathf.PI * 3));
                if (i > 0) d = Mathf.Min(d, Seg(p, prev, q));
                prev = q;
            }

            return d;
        }

        static float Ellipse(Vector2 p, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            Vector2 q = new Vector2(Mathf.Cos(a) * p.x + Mathf.Sin(a) * p.y, -Mathf.Sin(a) * p.x + Mathf.Cos(a) * p.y);
            Vector2 e = new Vector2(q.x / 0.8f, q.y / 0.3f);
            return Mathf.Abs(e.magnitude - 1) * 0.3f - W * 0.8f;
        }

        static float Rungs(Vector2 p)
        {
            float d = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                float y = -0.55f + i * 0.4f;
                float t = (y + 0.85f) / 1.7f;
                d = Mathf.Min(d, Seg(p, new Vector2(-0.4f + 0.15f * t, y), new Vector2(0.4f + 0.15f * t, y)));
            }

            return d;
        }
    }
}

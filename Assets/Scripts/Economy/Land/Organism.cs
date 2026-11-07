using System;
using System.Collections.Generic;
using UnityEngine;
using Why.Economy.Model;

namespace Why.Economy.Land
{
    /// <summary>
    /// A company as one organic body (design Docs/hills-design/ADVANTAGE.md, section 4): its balance sheet's parts are lobes
    /// of a single implicit surface (each lobe a Gaussian as wide as ∛ of its dollars), so the parts fuse into one whole:
    /// equity at the core, cash, what it built, what it bought and its stakes swelling above, debt and leases sagging below.
    /// The surface is sampled along a geodesic sphere (shared vertices, so its points and its wire edges come from one
    /// topology), roughened by a seeded organic noise. Each point takes the muted colour of the lobes that make it, and a
    /// frontier factor (0..1) where the body faces those its advantage presses on (its dependents, its rivals): the layer
    /// gives those points and edges intense bloom and leaves the rest muted. Pure: vectors and colours only.
    /// </summary>
    public sealed class Organism
    {
        /// <summary>Muted tones of the balance sheet's parts (sRGB): the bloom is kept for the frontiers.</summary>
        public static readonly Color Equity = new Color(.62f, .64f, .68f), Cash = new Color(.48f, .66f, .54f), Built = new Color(.46f, .54f, .68f),
            Bought = new Color(.60f, .51f, .68f), Stakes = new Color(.70f, .62f, .44f), Debt = new Color(.66f, .36f, .34f), Leases = new Color(.68f, .48f, .34f),
            Unknown = new Color(.52f, .54f, .58f);

        public readonly Vector3[] Points;     // local, from the body's centre
        public readonly Color[] Colors;
        public readonly float[] Frontier;     // 0 muted .. 1 the hottest frontier
        public readonly float[] Solidity;     // how tangible the parts are there: 0.3 (goodwill, stakes) .. 1 (what it built)
        public readonly int[] Edges;          // pairs of point indices
        public readonly float MeanRadius, MaxRadius;
        public readonly bool Known;           // drawn from a balance sheet (else one lobe sized by its value)

        public readonly struct Lobe
        {
            public readonly Vector3 Centre;
            public readonly float Size, Solid;
            public readonly Color Color;

            public Lobe(Vector3 centre, float size, Color color, float solid)
            {
                Centre = centre; Size = size; Color = color; Solid = solid;
            }
        }

        /// <summary>A frontier: a direction from the body (unit, land-local) and how hard its advantage presses there (0..1).</summary>
        public readonly struct Front
        {
            public readonly Vector3 Direction;
            public readonly float Strength;

            public Front(Vector3 direction, float strength)
            {
                Direction = direction; Strength = strength;
            }
        }

        /// <summary>The lobes of a company's balance sheet (overlapping, so they fuse): the core of book equity; above it cash,
        /// what it built, what it bought, its stakes; below it debt and leases. A world's year scales them with its equity.
        /// Without a balance sheet, one lobe a twelfth of its market value.</summary>
        public static List<Lobe> Lobes(ContestNetwork.Body b, out bool known)
        {
            var lobes = new List<Lobe>(8);
            known = false;
            var parts = b.Kind == ContestNetwork.Kind.Company ? ContestNetwork.PartsOf(b) : new List<ContestNetwork.Part>();
            float core = 0;
            foreach (var p in parts) if (p.Kind == ContestNetwork.PartKind.Core) core = Math.Max(core, p.Radius);
            if (parts.Count == 0 || core <= 0)
            {
                float r = Math.Max(1.4f, ContestNetwork.Radius(double.IsNaN(b.Value) ? 1 : b.Value / 12));
                lobes.Add(new Lobe(Vector3.zero, r, Unknown, .3f));
                return lobes;
            }

            known = true;
            lobes.Add(new Lobe(Vector3.zero, core, Equity, .6f));
            float twist = Hash01(b.Id) * Mathf.PI * 2;
            int above = 0, k = 0;
            foreach (var p in parts) if (Above(p.Kind)) above++;
            foreach (var p in parts)
            {
                if (p.Kind == ContestNetwork.PartKind.Core || p.Kind == ContestNetwork.PartKind.Promises || p.Radius <= 0) continue;
                Vector3 dir;
                if (Above(p.Kind))
                {
                    float ang = twist + k++ * Mathf.PI * 2 / Math.Max(1, above);
                    dir = new Vector3(Mathf.Cos(ang) * .8f, .6f, Mathf.Sin(ang) * .8f).normalized;
                }
                else if (p.Kind == ContestNetwork.PartKind.Debt) dir = new Vector3(.12f, -1, .08f).normalized;
                else dir = new Vector3(Mathf.Cos(twist + 2.2f) * .6f, -.8f, Mathf.Sin(twist + 2.2f) * .6f).normalized;
                // lobes overlap the core so the parts fuse into one body
                lobes.Add(new Lobe(dir * (core * .72f + p.Radius * .45f), p.Radius, ColorOf(p.Kind), SolidOf(p.Kind)));
            }

            return lobes;
        }

        /// <summary>How tangible a part is: what it built and what it owes read solid; what it bought and its stakes ghostly.</summary>
        public static float SolidOf(ContestNetwork.PartKind k) => k switch
        {
            ContestNetwork.PartKind.Built => 1f, ContestNetwork.PartKind.Debt => .9f, ContestNetwork.PartKind.Cash => .8f, ContestNetwork.PartKind.Leases => .7f,
            ContestNetwork.PartKind.Stakes => .45f, ContestNetwork.PartKind.Bought => .32f, _ => .6f,
        };

        static bool Above(ContestNetwork.PartKind k) =>
            k == ContestNetwork.PartKind.Cash || k == ContestNetwork.PartKind.Built || k == ContestNetwork.PartKind.Bought || k == ContestNetwork.PartKind.Stakes;

        public static Color ColorOf(ContestNetwork.PartKind k) => k switch
        {
            ContestNetwork.PartKind.Cash => Cash, ContestNetwork.PartKind.Built => Built, ContestNetwork.PartKind.Bought => Bought,
            ContestNetwork.PartKind.Stakes => Stakes, ContestNetwork.PartKind.Debt => Debt, ContestNetwork.PartKind.Leases => Leases, _ => Equity,
        };

        /// <summary>The body: each geodesic direction marched out to the lobes' iso-surface (the field's value e^-1, where a lone
        /// lobe's surface is its size), roughened, coloured by the lobes there, and lit where it faces a frontier.</summary>
        public Organism(ContestNetwork.Body b, IList<Front> fronts, int detail)
        {
            var lobes = Lobes(b, out Known);
            var (dirs, edges) = Geodesic(detail);
            Points = new Vector3[dirs.Length];
            Colors = new Color[dirs.Length];
            Frontier = new float[dirs.Length];
            Solidity = new float[dirs.Length];
            Edges = edges;
            float reach = 0;
            foreach (var l in lobes) reach = Mathf.Max(reach, l.Centre.magnitude + l.Size * 2.2f);
            float seed = Hash01(b.Id) * 31, sum = 0, max = 0;
            const float iso = .3679f;
            for (int i = 0; i < dirs.Length; i++)
            {
                Vector3 d = dirs[i];
                // march out to the last point inside, then refine by halving
                float lo = 0, hi = reach;
                const int steps = 26;
                for (int s = 1; s <= steps; s++)
                {
                    float r = reach * s / steps;
                    if (Field(lobes, d * r) >= iso) lo = r;
                }

                hi = Mathf.Min(reach, lo + reach / steps);
                for (int s = 0; s < 6; s++)
                {
                    float mid = (lo + hi) * .5f;
                    if (Field(lobes, d * mid) >= iso) lo = mid;
                    else hi = mid;
                }

                float noise = Mathf.Sin(2.1f * d.x + seed) * Mathf.Sin(2.7f * d.y + seed * .7f) * Mathf.Sin(3.3f * d.z + seed * 1.3f)
                              + .5f * Mathf.Sin(5.3f * d.x + 1.7f * d.z + seed * 2.1f) * Mathf.Sin(4.1f * d.y - seed);
                float r0 = Mathf.Max(.4f, lo) * (1 + .055f * noise);
                Vector3 p = d * r0;
                Points[i] = p;
                sum += r0;
                max = Mathf.Max(max, r0);
                // colour: the lobes that make the surface here, weighted by their share of the field
                float wsum = 0, solid = 0;
                Color c = Color.black;
                foreach (var l in lobes)
                {
                    float w = Mathf.Exp(-(p - l.Centre).sqrMagnitude / (l.Size * l.Size));
                    c += l.Color * w;
                    solid += l.Solid * w;
                    wsum += w;
                }

                Colors[i] = wsum > 0 ? c / wsum : Unknown;
                Solidity[i] = wsum > 0 ? solid / wsum : .3f;
                float f = 0;
                if (fronts != null)
                    foreach (var fr in fronts)
                        f = Mathf.Max(f, fr.Strength * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.78f, .975f, Vector3.Dot(d, fr.Direction))));
                Frontier[i] = f;
            }

            MeanRadius = sum / Math.Max(1, dirs.Length);
            MaxRadius = max;
        }

        static float Field(List<Lobe> lobes, Vector3 p)
        {
            float f = 0;
            foreach (var l in lobes) f += Mathf.Exp(-(p - l.Centre).sqrMagnitude / (l.Size * l.Size));
            return f;
        }

        // ------------------------------------------------------------------ the geodesic sphere (indexed, shared vertices)

        static readonly Dictionary<int, (Vector3[] dirs, int[] edges)> Cache = new Dictionary<int, (Vector3[], int[])>();

        /// <summary>The unit icosphere subdivided <paramref name="levels"/> times: 12, 42, 162 or 642 vertices, and its edges.</summary>
        public static (Vector3[] dirs, int[] edges) Geodesic(int levels)
        {
            lock (Cache)
            {
                if (Cache.TryGetValue(levels, out var hit)) return hit;
                float t = (1 + Mathf.Sqrt(5)) / 2;
                var v = new List<Vector3>
                {
                    new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0), new Vector3(0, -1, t), new Vector3(0, 1, t),
                    new Vector3(0, -1, -t), new Vector3(0, 1, -t), new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
                };
                for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
                var faces = new List<int>
                {
                    0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                    3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
                };
                for (int l = 0; l < levels; l++)
                {
                    var mids = new Dictionary<long, int>();
                    int Mid(int a, int b)
                    {
                        long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                        if (mids.TryGetValue(key, out int m)) return m;
                        v.Add(((v[a] + v[b]) * .5f).normalized);
                        return mids[key] = v.Count - 1;
                    }

                    var next = new List<int>(faces.Count * 4);
                    for (int i = 0; i < faces.Count; i += 3)
                    {
                        int a = faces[i], b = faces[i + 1], c = faces[i + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                        next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                    }

                    faces = next;
                }

                var seen = new HashSet<long>();
                var edges = new List<int>();
                for (int i = 0; i < faces.Count; i += 3)
                    for (int e = 0; e < 3; e++)
                    {
                        int a = faces[i + e], b = faces[i + (e + 1) % 3];
                        long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                        if (seen.Add(key)) { edges.Add(a); edges.Add(b); }
                    }

                var result = (v.ToArray(), edges.ToArray());
                Cache[levels] = result;
                return result;
            }
        }

        public static float Hash01(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in s) h = (h ^ c) * 16777619;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }
    }
}

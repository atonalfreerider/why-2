using System;
using System.Collections.Generic;
using UnityEngine;
using Why.Economy.Data;

namespace Why.Economy.Land
{
    /// <summary>
    /// The present's hills as terrain of advantage (design "exploded", E2c): every market industry's hill is a field of
    /// peaks, one for each of its named firms and a spread of smaller ones for the rest of the industry.
    /// <list type="bullet">
    /// <item>A peak's footprint is its share of the industry: the firm's revenue over the industry's output (the BEA
    /// input-output table's, scaled to the year). Revenue is worldwide, so where the named firms' revenue passes
    /// <see cref="MaxNamed"/> of the output the industry is widened to fit (≈, said in the legend).</item>
    /// <item>Its height is its net margin (net income over revenue), <see cref="HeightPerMargin"/> exploded units per 100%.
    /// Footprint times height is therefore the firm's profit.</item>
    /// <item><see cref="NormalMargin"/> (the Fortune 500's profits over its revenue) is normal profit: the layer draws it
    /// as a glass plane, and what rises above it, gold, is advantage.</item>
    /// <item>The rest of the industry is drawn as smaller peaks of falling size (a Zipf spread, illustrative) at the
    /// industry's typical net margin, so the ground between the named firms has texture without inventing a firm.</item>
    /// </list>
    /// The peaks' outlines are warped by noise and their flanks carved by gullies (never their summits: a summit's height
    /// is the margin exactly), and they blend where they meet, so a hill reads as one organic body. The largest purchases
    /// between hills (<see cref="Flows"/>) are kept for the layer's veins, and each terrace's operating surplus
    /// (<see cref="Surplus"/>) for its capital threads. Pure C#: no Unity objects (Mathf only).
    /// </summary>
    public sealed class PresentPeaks
    {
        /// <summary>One peak: a named firm (<see cref="Name"/> set) or one of the rest of its industry.</summary>
        public sealed class Peak
        {
            public int Industry, Tier;

            /// <summary>The firm's name; null for the rest of the industry.</summary>
            public string Name;

            /// <summary>Revenue and net income ($B, worldwide, the firm's fiscal year), its share of the hill and its net margin.</summary>
            public double Revenue, NetIncome, Share, Margin;

            /// <summary>Whether its margin is the firm's own (else the industry's typical one: no net income reported).</summary>
            public bool MarginKnown;

            /// <summary>Its centre (crest-unit x, exploded d), its radii across and along the depth and its height (exploded
            /// units), and its noise seed.</summary>
            public float X, D, Rx, Rd, H, Seed;

            public bool Named => Name != null;
        }

        /// <summary>One hill's field: its industry's figures and the rectangle its peaks stand in (crest x, exploded d).</summary>
        public sealed class Field
        {
            public int Industry, Tier;
            public double Output, Surplus, Typical, NamedShare;
            public float X0, X1, D0, D1;
            public readonly List<Peak> Peaks = new List<Peak>();
        }

        /// <summary>One purchase between two hills ($B a year, the table's ratio scaled to the year).</summary>
        public struct Flow
        {
            public int Seller, Buyer, SellerTier, BuyerTier;
            public double Value;
        }

        /// <summary>Exploded units of height per 100% net margin.</summary>
        public const float HeightPerMargin = 45;

        /// <summary>The ground every field stands on (exploded units): low, so the valleys between industries read.</summary>
        public const float Mat = .9f;

        /// <summary>The most purchases kept as veins.</summary>
        public const int MaxFlows = 30;

        /// <summary>The share of an industry its named firms may take at most (worldwide revenue against domestic output).</summary>
        public const double MaxNamed = .7;

        /// <summary>The fields' depth range (share of D) and their inset from their band's edges (exploded units).</summary>
        const float DepthFrom = .1f, DepthTo = .9f, Inset = 1.4f;

        /// <summary>How the peaks blend where they meet: the p of a p-norm (higher, sharper saddles).</summary>
        const float Blend = 4;

        public readonly PresentLayout Layout;
        public readonly double NormalMargin;
        public readonly int TableYear;

        public readonly List<Field> Fields = new List<Field>();
        public readonly List<Peak> Peaks = new List<Peak>();
        public readonly List<Flow> Flows = new List<Flow>();

        /// <summary>Each terrace's operating surplus ($B a year), raw materials (0) to tech (3).</summary>
        public readonly double[] Surplus = new double[4];

        /// <summary>The named firms placed, those left out (no revenue), and the peaks whose centre left their band.</summary>
        public readonly int Named, Unplaced, Outside;

        readonly Field[] byIndustry;
        readonly List<Field>[] byTier = { new List<Field>(), new List<Field>(), new List<Field>(), new List<Field>() };

        public PresentPeaks(PresentLayout layout, IReadOnlyList<Industry> industries, IoTable io, double normalMargin)
        {
            Layout = layout;
            NormalMargin = normalMargin > 0 ? normalMargin : .1;
            TableYear = io?.Year ?? 0;
            byIndustry = new Field[industries.Count];
            var index = new Dictionary<string, int>();
            for (int i = 0; i < industries.Count; i++) index[industries[i].Id] = i;

            foreach (PresentLayout.Hill h in layout.Hills)
            {
                Industry ind = industries[h.Industry];
                Dictionary<string, double> acct = null;
                io?.Accounts?.TryGetValue(ind.Id, out acct);
                double va0 = Get(acct, "valueAdded"), scale = va0 > 0 ? h.ValueAdded / va0 : 0;
                double output = Get(acct, "output") * scale, surplus = Math.Max(0, Get(acct, "surplus")) * scale;
                if (output <= 0) output = h.ValueAdded * 2; // not in the table: about the economy's output over value added
                var f = new Field
                {
                    Industry = h.Industry, Tier = h.Tier, Output = output, Surplus = surplus,
                    Typical = ind.ProfitMargin > 0 ? ind.ProfitMargin : (output > 0 ? .4 * surplus / output : .05),
                    X0 = h.Center - h.Band + Inset / PresentLayout.S, X1 = h.Center + h.Band - Inset / PresentLayout.S,
                    D0 = DepthFrom * PresentLayout.D, D1 = DepthTo * PresentLayout.D
                };
                Surplus[h.Tier - 1] += surplus;
                byIndustry[h.Industry] = f;
                Fields.Add(f);
                byTier[h.Tier - 1].Add(f);
                if (f.X1 <= f.X0 || h.Flat) continue;

                // the named firms: share of the (widened) industry, margin, footprint and height
                var firms = new List<Peak>();
                double sumRevenue = 0;
                foreach (Company c in ind.Companies ?? new List<Company>())
                {
                    if (c.Revenue <= 0)
                    {
                        Unplaced++;
                        continue;
                    }

                    bool known = c.NetIncome != 0;
                    firms.Add(new Peak
                    {
                        Industry = h.Industry, Tier = h.Tier, Name = c.Name, Revenue = c.Revenue, NetIncome = c.NetIncome,
                        Margin = known ? c.NetIncome / c.Revenue : f.Typical, MarginKnown = known
                    });
                    sumRevenue += c.Revenue;
                }

                float w = (f.X1 - f.X0) * PresentLayout.S, l = f.D1 - f.D0, cx = (f.X0 + f.X1) * .5f;
                double denominator = Math.Max(output, sumRevenue / MaxNamed);
                firms.Sort((a, b) => b.Revenue.CompareTo(a.Revenue));
                foreach (Peak p in firms)
                {
                    p.Share = p.Revenue / denominator;
                    f.NamedShare += p.Share;
                    p.H = Height(p.Margin);
                    Size(p, (float)(p.Share * w * l), w, l);
                    p.Seed = Hash(h.Industry * 31 + firms.IndexOf(p)) * 100;
                }

                // along the depth, the largest in the middle and the rest alternating behind and in front, a little overlapped;
                // across, alternating sides of the band's centre
                var chain = new List<Peak>();
                for (int i = 0; i < firms.Count; i++)
                    if (i % 2 == 1) chain.Insert(0, firms[i]);
                    else chain.Add(firms[i]);
                float cursor = 0;
                var centres = new float[chain.Count];
                for (int i = 0; i < chain.Count; i++)
                {
                    centres[i] = cursor + chain[i].Rd * .8f;
                    cursor += chain[i].Rd * 1.6f;
                }

                float squeeze = cursor > l * .96f ? l * .96f / cursor : 1, middle = PresentLayout.HillCenter * PresentLayout.D;
                for (int i = 0; i < chain.Count; i++)
                {
                    Peak p = chain[i];
                    p.D = Mathf.Clamp(middle + (centres[i] - cursor * .5f) * squeeze, f.D0 + p.Rd * .4f, f.D1 - p.Rd * .4f);
                    int rank = firms.IndexOf(p);
                    float side = rank == 0 ? 0 : (rank % 2 == 1 ? 1 : -1);
                    p.X = cx + side * Mathf.Max(0, w * .5f - p.Rx) * .55f / PresentLayout.S;
                    f.Peaks.Add(p);
                    Named++;
                    if (p.X < h.Center - h.Band || p.X > h.Center + h.Band) Outside++;
                }

                // the rest of the industry: smaller peaks of falling size filling the ground the named firms leave
                double rest = Math.Max(0, 1 - f.NamedShare);
                int n = Mathf.Clamp(Mathf.RoundToInt((float)(w * l * rest / 420)), 3, 36);
                double weights = 0;
                for (int j = 0; j < n; j++) weights += 1.0 / (j + 1.5);
                for (int j = 0; j < n; j++)
                {
                    var p = new Peak { Industry = h.Industry, Tier = h.Tier, Margin = f.Typical, Share = rest * (1.0 / (j + 1.5)) / weights };
                    p.H = Height(f.Typical * (.55 + .9 * Hash(h.Industry * 977 + j)));
                    Size(p, (float)(.85 * p.Share * w * l), w, l);
                    p.Seed = Hash(h.Industry * 131 + j + 7) * 100;
                    // a few candidates (a Halton sequence) inside the field: the one least inside a named firm
                    float best = float.MaxValue;
                    for (int t = 0; t < 12; t++)
                    {
                        int k = h.Industry * 53 + j * 12 + t + 1;
                        float x = Mathf.Lerp(f.X0 + p.Rx * .5f / PresentLayout.S, f.X1 - p.Rx * .5f / PresentLayout.S, Halton(k, 2));
                        float d = Mathf.Lerp(f.D0 + p.Rd * .5f, f.D1 - p.Rd * .5f, Halton(k, 3));
                        float overlap = 0;
                        foreach (Peak q in firms) overlap = Mathf.Max(overlap, 1.2f - Q(q, x, d));
                        if (overlap < best)
                        {
                            best = overlap;
                            p.X = x;
                            p.D = d;
                        }
                    }

                    f.Peaks.Add(p);
                }

                Peaks.AddRange(f.Peaks);
            }

            // the largest purchases between two different hills, at the year
            if (io?.Accounts == null) return;
            var flows = new List<Flow>();
            foreach ((string from, string to, double v) in io.Flows())
            {
                if (!index.TryGetValue(from, out int s) || !index.TryGetValue(to, out int b) || s == b || v <= 0) continue;
                Field seller = byIndustry[s], buyer = byIndustry[b];
                if (seller == null || buyer == null) continue;
                double scale = buyer.Output / Math.Max(1e-9, Get(io.Accounts[industries[b].Id], "output"));
                flows.Add(new Flow { Seller = s, Buyer = b, SellerTier = seller.Tier, BuyerTier = buyer.Tier, Value = v * scale });
            }

            flows.Sort((a, b) => b.Value.CompareTo(a.Value));
            for (int i = 0; i < flows.Count && i < MaxFlows; i++) Flows.Add(flows[i]);
        }

        /// <summary>A hill's field, or null.</summary>
        public Field Of(int industry) => industry >= 0 && industry < byIndustry.Length ? byIndustry[industry] : null;

        /// <summary>The height of a margin (exploded units): a loss stands as a low stub.</summary>
        public static float Height(double margin) => margin > 0 ? Mathf.Max(1.2f, (float)margin * HeightPerMargin) : .6f;

        /// <summary>The margin a height stands for.</summary>
        public static float MarginOf(float exploded) => exploded / HeightPerMargin;

        /// <summary>The terrain's height above terrace k's plateau at (crest x, exploded d), exploded units.</summary>
        public float Exploded(int tier, float x, float d)
        {
            List<Field> fields = byTier[tier - 1];
            for (int i = 0; i < fields.Count; i++)
            {
                Field f = fields[i];
                // a peak's warped outline reaches a little past its field: look a few units wider
                if (x < f.X0 - 3 / PresentLayout.S || x > f.X1 + 3 / PresentLayout.S) continue;
                // the field's edge wanders (no straight valleys between industries)
                float wander = 3 * (Mathf.PerlinNoise(x * PresentLayout.S * .06f + f.Industry * 7.1f, d * .06f) - .5f);
                float ex = Mathf.Min(x - f.X0, f.X1 - x) * PresentLayout.S + wander, ed = Mathf.Min(d - f.D0, f.D1 - d) + wander;
                float mat = Mat * Smooth(ex / 4) * Smooth(ed / 8);
                float sum = Mathf.Pow(mat, Blend);
                List<Peak> peaks = f.Peaks;
                for (int j = 0; j < peaks.Count; j++)
                {
                    Peak p = peaks[j];
                    float q = Q(p, x, d);
                    if (q >= 1.4f) continue;
                    // the outline warped (lobes), the flanks carved (gullies); neither touches the summit
                    float n = Mathf.PerlinNoise(x * PresentLayout.S * .11f + p.Seed, d * .11f + p.Seed * 1.7f);
                    float w = q * (.72f + .56f * n);
                    if (w >= 1) continue;
                    float g = 1 - Mathf.Abs(2 * Mathf.PerlinNoise(x * PresentLayout.S * .32f + p.Seed * 2.3f, d * .32f - p.Seed) - 1);
                    float s = (1 - w) * (1 - w), h = p.H * s * (1 - .2f * w * g);
                    sum += Mathf.Pow(h, Blend);
                }

                return Mathf.Pow(sum, 1 / Blend);
            }

            return 0;
        }

        /// <summary>The terrain's height above terrace k's plateau, crest units (the frame's y).</summary>
        public float HeightAt(int tier, float x, float d) => Exploded(tier, x, d) / PresentLayout.S;

        /// <summary>The tallest point a terrace's terrain reaches (crest units, with the blend's lift).</summary>
        public float MaxHeight(int tier)
        {
            float most = Mat;
            foreach (Field f in byTier[tier - 1])
            foreach (Peak p in f.Peaks)
                most = Mathf.Max(most, p.H);
            return most * 1.1f / PresentLayout.S;
        }

        /// <summary>A peak's summit in its terrace's frame (crest x, crest y above the plateau, exploded d).</summary>
        public Vector3 Summit(Peak p) => new Vector3(p.X, HeightAt(p.Tier, p.X, p.D), p.D);

        // ------------------------------------------------------------------ helpers

        /// <summary>A peak's radii for a footprint (exploded units²): round where its field is wide enough, else stretched
        /// along the depth into a ridge.</summary>
        static void Size(Peak p, float area, float width, float length)
        {
            // never steeper than a cone of slope ~3 (a needle hides its own volume); only the smallest peaks grow by it
            float r = Mathf.Sqrt(Mathf.Max(0, area) / Mathf.PI), least = Mathf.Max(.6f, p.H * .32f);
            p.Rx = Mathf.Max(least, Mathf.Min(r, width * .45f));
            p.Rd = Mathf.Max(least, Mathf.Min(area / (Mathf.PI * p.Rx), length * .5f));
        }

        /// <summary>A point's squared normalised distance from a peak's centre (1 on its outline).</summary>
        static float Q(Peak p, float x, float d)
        {
            float u = (x - p.X) * PresentLayout.S / p.Rx, v = (d - p.D) / p.Rd;
            return u * u + v * v;
        }

        static float Smooth(float v)
        {
            v = Mathf.Clamp01(v);
            return v * v * (3 - 2 * v);
        }

        static float Hash(int i)
        {
            uint h = (uint)i * 2654435761u;
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }

        static float Halton(int i, int b)
        {
            float f = 1, r = 0;
            while (i > 0)
            {
                f /= b;
                r += f * (i % b);
                i /= b;
            }

            return r;
        }

        static double Get(Dictionary<string, double> d, string key) => d != null && d.TryGetValue(key, out double v) ? v : 0;
    }
}

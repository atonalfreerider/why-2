using System;
using System.Collections.Generic;
using UnityEngine;
using Why.Economy.Data;

namespace Why.Economy.Land
{
    /// <summary>
    /// The present's hills built of what they are made of (design "exploded", E2b): every market industry's hill is a pile
    /// of blocks, each block <see cref="BlockValue"/> of the year's output. A hill's footprint is its value added (the
    /// layout's area check, A1, holds: its grid of columns fills its band), so its height is output over value added: a hill
    /// stands as tall as it leans on the others. From the ground up its courses are
    /// <list type="bullet">
    /// <item>what it buys from every other industry (the BEA input-output table, <see cref="IoTable"/>), each seller's
    /// blocks together in one sector of the ground floor, coloured by the seller's tier; grey, what the table does not
    /// allocate to the 25 industries (imports and the rest);</item>
    /// <item>its value added: wages (blue), production taxes (the state's colour), operating surplus (a gold crown).</item>
    /// </list>
    /// The table is 2024's: other years scale its ratios by the industry's value added that year (≈, said in the legend).
    /// The largest purchases between hills are kept as <see cref="Flows"/> (the layer draws them as arcs from the seller's
    /// summit to the seller's own blocks in the buyer), and the crowns' surplus as <see cref="Surplus"/> per tier (the
    /// layer's capital threads). Pure C#: no Unity objects, safe on any thread.
    /// </summary>
    public sealed class PresentBlocks
    {
        /// <summary>What a block's colour says.</summary>
        public enum Kind { Input, Unallocated, Wages, Taxes, Surplus }

        /// <summary>One block: its hill, its column (crest-unit x, exploded d of its centre), its course (0 the ground), what
        /// it is, and for an input the seller's industry index and tier (0 the state).</summary>
        public struct Block
        {
            public int Hill, Course, Seller, SellerTier;
            public float X, D;
            public Kind Kind;
        }

        /// <summary>One purchase between two hills drawn as an arc ($B a year, the table's ratio scaled to the year).</summary>
        public struct Flow
        {
            public int Seller, Buyer, SellerTier, BuyerTier;
            public double Value;
        }

        /// <summary>One hill's pile: its grid (columns across × rows deep, their centres' origin and pitch in its terrace's
        /// frame), its columns' heights in blocks, and its blocks by kind.</summary>
        public sealed class Pile
        {
            public int Industry, Tier, Columns, Rows;
            public float X0, D0, PitchX;
            public int[] Heights;
            public double Output, ValueAdded;
            public readonly Dictionary<Kind, int> Count = new Dictionary<Kind, int>();
            public readonly Dictionary<int, int> BySeller = new Dictionary<int, int>();
            public int Blocks;
        }

        /// <summary>About this many blocks in all: the year's output sets a block's value (rounded to $5B, $10B, $20B, $25B or
        /// $50B) so the present keeps its vertex budget (design §9.3) whatever the year.</summary>
        public const int TargetBlocks = 2400;

        /// <summary>A pile's mean height in blocks over the whole present (its own is its output over value added, scaled).</summary>
        const float MeanCourses = 2.4f;

        /// <summary>The blocks' depth range: the middle of each terrace's depth (share of D), leaving its near and far edges
        /// as ground.</summary>
        const float DepthFrom = .1f, DepthTo = .9f;

        /// <summary>The most arcs drawn between hills.</summary>
        public const int MaxFlows = 12;

        public readonly PresentLayout Layout;

        /// <summary>A block's value ($B a year) and its edge (exploded units: x and y are crest units × S).</summary>
        public readonly double BlockValue;

        public readonly float Edge;

        public readonly List<Block> Blocks = new List<Block>();
        public readonly List<Pile> Piles = new List<Pile>();
        public readonly List<Flow> Flows = new List<Flow>();

        /// <summary>Each terrace's operating surplus ($B a year), raw materials (0) to tech (3).</summary>
        public readonly double[] Surplus = new double[4];

        /// <summary>The input-output table's year, and whether the shown year is it.</summary>
        public readonly int TableYear;

        /// <summary>The largest gap between a pile's blocks and its output over the block value (blocks; rounding: at most 1).</summary>
        public readonly int WorstRounding;

        readonly Pile[] byIndustry;

        public PresentBlocks(PresentLayout layout, IReadOnlyList<Industry> industries, IoTable io)
        {
            Layout = layout;
            TableYear = io?.Year ?? 0;
            byIndustry = new Pile[industries.Count];
            if (io?.Accounts == null) return;

            // index the table by industry id, its columns (what each buyer buys from each seller)
            var index = new Dictionary<string, int>();
            for (int i = 0; i < industries.Count; i++) index[industries[i].Id] = i;
            var bought = new Dictionary<int, List<(int seller, double value)>>();
            foreach ((string from, string to, double v) in io.Flows())
            {
                if (!index.TryGetValue(from, out int s) || !index.TryGetValue(to, out int b) || v <= 0) continue;
                if (!bought.TryGetValue(b, out List<(int, double)> list)) bought[b] = list = new List<(int, double)>();
                list.Add((s, v));
            }

            // each hill's output and its parts at the year: the table's ratios times the year's value added
            double total = 0, width = 0;
            foreach (PresentLayout.Hill h in layout.Hills)
            {
                Industry ind = industries[h.Industry];
                if (!io.Accounts.TryGetValue(ind.Id, out Dictionary<string, double> acct)) continue;
                double va0 = Get(acct, "valueAdded"), scale = va0 > 0 ? h.ValueAdded / va0 : 0;
                var pile = new Pile { Industry = h.Industry, Tier = h.Tier, Output = Get(acct, "output") * scale, ValueAdded = h.ValueAdded };
                if (pile.Output <= 0) continue;
                byIndustry[h.Industry] = pile;
                Piles.Add(pile);
                total += pile.Output;
                width += 2 * h.Band * PresentLayout.S;
            }

            if (Piles.Count == 0 || width <= 0) return;
            BlockValue = Nice(total / TargetBlocks);
            float depth = (DepthTo - DepthFrom) * PresentLayout.D;
            // the edge that gives the whole present its mean height: cells = blocks / mean
            Edge = Mathf.Sqrt((float)(width * depth * MeanCourses / (total / BlockValue)));
            int rows = Mathf.Max(1, Mathf.FloorToInt(depth / Edge));

            foreach (Pile pile in Piles)
            {
                PresentLayout.Hill h = layout.Hills.Find(x => x.Industry == pile.Industry);
                Industry ind = industries[pile.Industry];
                io.Accounts.TryGetValue(ind.Id, out Dictionary<string, double> acct);
                double scale = pile.Output / Math.Max(1e-9, Get(acct, "output"));

                // the parts in blocks, largest remainder so they sum to the pile's rounded output
                var parts = new List<(Kind kind, int seller, double value)>();
                double allocated = 0;
                if (bought.TryGetValue(pile.Industry, out List<(int seller, double value)> sellers))
                {
                    // the sellers in tier order (the state, raw materials up to tech), largest first within a tier
                    sellers.Sort((a, b) =>
                    {
                        int ta = Tier(layout, industries, a.seller), tb = Tier(layout, industries, b.seller);
                        return ta != tb ? ta.CompareTo(tb) : b.value.CompareTo(a.value);
                    });
                    foreach ((int s, double v) in sellers)
                    {
                        parts.Add((Kind.Input, s, v * scale));
                        allocated += v * scale;
                    }
                }

                parts.Add((Kind.Unallocated, -1, Math.Max(0, Get(acct, "intermediate") * scale - allocated)));
                parts.Add((Kind.Wages, -1, Get(acct, "compensation") * scale));
                parts.Add((Kind.Taxes, -1, Math.Max(0, Get(acct, "productionTaxes")) * scale));
                parts.Add((Kind.Surplus, -1, Math.Max(0, Get(acct, "surplus")) * scale));
                Surplus[pile.Tier - 1] += Math.Max(0, Get(acct, "surplus")) * scale;
                int n = Math.Max(1, (int)Math.Round(pile.Output / BlockValue));
                int[] counts = LargestRemainder(parts, n);

                // the grid: as many columns as fit the band at the edge (at least one), centred in it
                float bandWorld = 2 * h.Band * PresentLayout.S;
                pile.Columns = Mathf.Max(1, Mathf.FloorToInt(bandWorld / Edge));
                pile.Rows = rows;
                // a band narrower than a block holds one column as wide as the band (no block over a neighbour's band)
                float pitchX = Mathf.Min(Edge, bandWorld / pile.Columns) / PresentLayout.S;
                pile.PitchX = pitchX;
                pile.X0 = h.Center - pitchX * (pile.Columns - 1) * .5f;
                pile.D0 = PresentLayout.D * .5f - Edge * (rows - 1) * .5f;
                pile.Heights = Heights(pile.Columns, rows, n);

                // the blocks in course order, each course swept by angle about the pile's centre, take the parts in order:
                // the inputs' sellers become sectors of the ground floor, value added the courses above
                var cells = new List<(int course, float angle, int c, int r)>(n);
                for (int c = 0; c < pile.Columns; c++)
                for (int r = 0; r < rows; r++)
                {
                    float u = pile.Columns > 1 ? c / (float)(pile.Columns - 1) - .5f : 0, v = rows > 1 ? r / (float)(rows - 1) - .5f : 0;
                    float angle = Mathf.Atan2(v, u);
                    for (int k = 0; k < pile.Heights[c * rows + r]; k++) cells.Add((k, angle, c, r));
                }

                cells.Sort((a, b) => a.course != b.course ? a.course.CompareTo(b.course) : a.angle.CompareTo(b.angle));
                int part = 0, left = counts.Length > 0 ? counts[0] : 0;
                foreach ((int course, float _, int c, int r) in cells)
                {
                    while (left <= 0 && part < counts.Length - 1) left = counts[++part];
                    (Kind kind, int seller, double _) = parts[part];
                    left--;
                    int st = seller >= 0 ? Tier(layout, industries, seller) : 0;
                    Blocks.Add(new Block
                    {
                        Hill = pile.Industry, Course = course, Seller = seller, SellerTier = st, Kind = kind,
                        X = pile.X0 + c * pitchX, D = pile.D0 + r * Edge
                    });
                    pile.Count[kind] = (pile.Count.TryGetValue(kind, out int was) ? was : 0) + 1;
                    if (seller >= 0) pile.BySeller[seller] = (pile.BySeller.TryGetValue(seller, out int s0) ? s0 : 0) + 1;
                    pile.Blocks++;
                }

                WorstRounding = Math.Max(WorstRounding, Math.Abs(pile.Blocks - (int)Math.Round(pile.Output / BlockValue)));
            }

            // the largest purchases between two different hills, at the year
            var flows = new List<Flow>();
            foreach (KeyValuePair<int, List<(int seller, double value)>> kv in bought)
            {
                Pile buyer = byIndustry[kv.Key];
                if (buyer == null) continue;
                double scale = buyer.Output / Math.Max(1e-9, Get(io.Accounts[industries[kv.Key].Id], "output"));
                foreach ((int s, double v) in kv.Value)
                {
                    Pile seller = s < byIndustry.Length ? byIndustry[s] : null;
                    if (seller == null || s == kv.Key) continue;
                    flows.Add(new Flow { Seller = s, Buyer = kv.Key, SellerTier = seller.Tier, BuyerTier = buyer.Tier, Value = v * scale });
                }
            }

            flows.Sort((a, b) => b.Value.CompareTo(a.Value));
            for (int i = 0; i < flows.Count && i < MaxFlows; i++) Flows.Add(flows[i]);
        }

        /// <summary>A hill's pile, or null (no pile: flat or not in the table).</summary>
        public Pile Of(int industry) => industry >= 0 && industry < byIndustry.Length ? byIndustry[industry] : null;

        /// <summary>The blocks' top at (x, d) in a terrace's frame: crest units above its plateau (0 off every pile).</summary>
        public float HeightAt(int tier, float x, float d)
        {
            foreach (Pile p in Piles)
            {
                if (p.Tier != tier) continue;
                float pitchX = p.PitchX;
                int c = Mathf.RoundToInt((x - p.X0) / pitchX), r = Mathf.RoundToInt((d - p.D0) / Edge);
                if (c < 0 || c >= p.Columns || r < 0 || r >= p.Rows) continue;
                if (Mathf.Abs(x - (p.X0 + c * pitchX)) > pitchX * .5f) continue;
                return p.Heights[c * p.Rows + r] * Edge / PresentLayout.S;
            }

            return 0;
        }

        /// <summary>The tallest pile on a terrace (crest units above its plateau).</summary>
        public float MaxHeight(int tier)
        {
            int most = 0;
            foreach (Pile p in Piles)
                if (p.Tier == tier)
                    foreach (int h in p.Heights) most = Math.Max(most, h);
            return most * Edge / PresentLayout.S;
        }

        /// <summary>The centre of a hill's blocks that match (terrace frame, crest x and y above the plateau, d); false when it has
        /// none.</summary>
        public bool Centroid(int hill, Func<Block, bool> match, out Vector3 at)
        {
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (Block b in Blocks)
            {
                if (b.Hill != hill || !match(b)) continue;
                sum += new Vector3(b.X, (b.Course + .5f) * Edge / PresentLayout.S, b.D);
                n++;
            }

            at = n > 0 ? sum / n : Vector3.zero;
            return n > 0;
        }

        // ------------------------------------------------------------------ helpers

        static double Get(Dictionary<string, double> d, string key) => d != null && d.TryGetValue(key, out double v) ? v : 0;

        static int Tier(PresentLayout layout, IReadOnlyList<Industry> industries, int industry)
        {
            foreach (PresentLayout.Hill h in layout.Hills)
                if (h.Industry == industry)
                    return h.Tier;
            return 0; // the state (federal, state and local) and anything not on a terrace
        }

        /// <summary>A round block value at or above <paramref name="v"/> ($B): 5, 10, 20, 25, 50 times a power of ten.</summary>
        static double Nice(double v)
        {
            if (v <= 0) return 1;
            double p = Math.Pow(10, Math.Floor(Math.Log10(v)));
            foreach (double m in new[] { 1, 2, 2.5, 5, 10 })
                if (m * p >= v * .85)
                    return m * p;
            return 10 * p;
        }

        static int[] LargestRemainder(List<(Kind kind, int seller, double value)> parts, int n)
        {
            double sum = 0;
            foreach (var p in parts) sum += Math.Max(0, p.value);
            var counts = new int[parts.Count];
            if (sum <= 0) return counts;
            var rest = new List<(double frac, int i)>();
            int given = 0;
            for (int i = 0; i < parts.Count; i++)
            {
                double exact = Math.Max(0, parts[i].value) / sum * n;
                counts[i] = (int)Math.Floor(exact);
                given += counts[i];
                rest.Add((exact - counts[i], i));
            }

            rest.Sort((a, b) => b.frac.CompareTo(a.frac));
            for (int k = 0; k < n - given && k < rest.Count; k++) counts[rest[k].i]++;
            return counts;
        }

        /// <summary>
        /// Column heights (blocks) summing to <paramref name="n"/>: a dome over the grid (highest at its centre), every column
        /// at least one block while there are enough (else the most central columns), the rest by largest remainder.
        /// </summary>
        static int[] Heights(int columns, int rows, int n)
        {
            int cells = columns * rows;
            var h = new int[cells];
            var weight = new float[cells];
            float sum = 0;
            for (int c = 0; c < columns; c++)
            for (int r = 0; r < rows; r++)
            {
                float u = columns > 1 ? 2 * c / (float)(columns - 1) - 1 : 0, v = rows > 1 ? 2 * r / (float)(rows - 1) - 1 : 0;
                float w = .15f + Mathf.Max(0, 1 - u * u) * Mathf.Max(0, 1 - v * v);
                weight[c * rows + r] = w;
                sum += w;
            }

            var order = new List<int>(cells);
            for (int i = 0; i < cells; i++) order.Add(i);
            order.Sort((a, b) => weight[b].CompareTo(weight[a]));
            if (n <= cells)
            {
                for (int k = 0; k < n; k++) h[order[k]] = 1;
                return h;
            }

            int extra = n - cells, given = 0;
            var rest = new List<(float frac, int i)>(cells);
            for (int i = 0; i < cells; i++)
            {
                float exact = weight[i] / sum * extra;
                int whole = Mathf.FloorToInt(exact);
                h[i] = 1 + whole;
                given += whole;
                rest.Add((exact - whole, i));
            }

            rest.Sort((a, b) => b.frac.CompareTo(a.frac));
            for (int k = 0; k < extra - given && k < rest.Count; k++) h[rest[k].i]++;
            return h;
        }
    }
}

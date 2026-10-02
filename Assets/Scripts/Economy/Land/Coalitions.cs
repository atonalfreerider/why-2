using System;
using System.Collections.Generic;

namespace Why.Economy.Land
{
    /// <summary>
    /// The season's coalitions (SPEC 5.3, 5.5): Clauset-Newman-Moore greedy modularity communities of the pair graph,
    /// their numbering across the five detections by maximum member overlap (so a coalition keeps its number while the
    /// season plays), and the normalized mutual information that says what the coalitions follow (the land, the group,
    /// the party). Pure and deterministic: dense arrays, index-order loops, every tie broken by the lower index.
    /// </summary>
    public static class Coalitions
    {
        /// <summary>Two gains closer than this are equal; the merge then goes to the lower index pair.</summary>
        const double GainTie = 1e-12;

        /// <summary>The dense e_ij matrix of <see cref="Detect"/>, one per thread, reused.</summary>
        [ThreadStatic] static double[] matrix;

        /// <summary>
        /// The CNM communities of an undirected weighted graph of <paramref name="n"/> nodes given as edges (a[k], b[k],
        /// w[k]), the weight counted in both directions as the modularity's 2m: starting from singletons, repeatedly merge
        /// the two adjacent communities with the largest modularity gain ΔQ = 2 (e_ij − a_i a_j) (ties within 1e-12: the
        /// lower index pair, the lower community absorbing the higher) until no merge gains. Each community is sorted
        /// ascending; the list is ordered by lowest member. O(n³) at worst on a dense matrix (n ≈ 120: well under 1 ms).
        /// </summary>
        public static List<int[]> Detect(int n, int[] a, int[] b, double[] w)
        {
            List<int[]> result = new List<int[]>(n);
            double m2 = 0;
            for (int k = 0; k < w.Length; k++) m2 += 2 * Math.Max(0, w[k]);
            if (n == 0) return result;
            if (m2 <= 0)
            {
                for (int i = 0; i < n; i++) result.Add(new[] { i });
                return result;
            }

            // the dense matrix of e_ij is this thread's buffer (n² doubles would land on the large-object heap every call)
            if (matrix == null || matrix.Length < n * n) matrix = new double[n * n];
            double[] e = matrix;
            Array.Clear(e, 0, n * n);
            double[] deg = new double[n];
            for (int k = 0; k < w.Length; k++)
            {
                double v = Math.Max(0, w[k]) / m2;
                if (v <= 0 || a[k] == b[k]) continue;
                e[a[k] * n + b[k]] += v;
                e[b[k] * n + a[k]] += v;
                deg[a[k]] += v;
                deg[b[k]] += v;
            }

            // each community's neighbors (ascending), so a scan visits only the adjacent pairs, in the dense order
            List<int>[] members = new List<int>[n], near = new List<int>[n];
            bool[] alive = new bool[n];
            for (int i = 0; i < n; i++)
            {
                members[i] = new List<int> { i };
                near[i] = new List<int>();
                alive[i] = true;
            }

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (j != i && e[i * n + j] > 0) near[i].Add(j);
                }
            }

            while (true)
            {
                double best = 0;
                int bi = -1, bj = -1;
                for (int i = 0; i < n; i++)
                {
                    if (!alive[i]) continue;
                    foreach (int j in near[i])
                    {
                        if (j <= i || e[i * n + j] <= 0) continue;
                        double dq = 2 * (e[i * n + j] - deg[i] * deg[j]);
                        if (bi < 0 || dq > best + GainTie)
                        {
                            best = dq;
                            bi = i;
                            bj = j;
                        }
                    }
                }

                if (bi < 0 || best <= 0) break;

                // merge bj into bi: its rows and columns add up, its self-loops disappear, its neighbors become bi's
                members[bi].AddRange(members[bj]);
                alive[bj] = false;
                foreach (int t in near[bj])
                {
                    e[bi * n + t] += e[bj * n + t];
                    e[t * n + bi] += e[t * n + bj];
                    e[bj * n + t] = e[t * n + bj] = 0;
                    near[t].Remove(bj);
                    if (t != bi)
                    {
                        Insert(near[bi], t);
                        Insert(near[t], bi);
                    }
                }

                near[bi].Remove(bi);
                near[bi].Remove(bj);
                near[bj].Clear();
                e[bi * n + bi] = 0;
                deg[bi] += deg[bj];
                deg[bj] = 0;
            }

            for (int i = 0; i < n; i++)
            {
                if (!alive[i]) continue;
                members[i].Sort();
                result.Add(members[i].ToArray());
            }

            return result;
        }

        /// <summary>
        /// Numbers the coalitions of every detection (each a list of communities, already cut to the coalitions of at
        /// least the minimum size) and returns each player's coalition per detection (-1: none). The last detection's
        /// coalitions are numbered 0, 1, ... by size (ties: the lowest member); walking back, each earlier detection's
        /// coalitions take the numbers of the later detection's they share most members with (greedy on the overlap,
        /// ties by the lower number, then the earlier coalition), and the ones left take new numbers in size order.
        /// </summary>
        public static int[][] Number(int players, IReadOnlyList<List<int[]>> detections)
        {
            int count = detections.Count;
            int[][] label = new int[count][];
            for (int d = 0; d < count; d++)
            {
                label[d] = new int[players];
                for (int p = 0; p < players; p++) label[d][p] = -1;
            }

            if (count == 0) return label;
            int next = 0;
            List<int[]> last = BySize(detections[count - 1]);
            foreach (int[] c in last)
            {
                foreach (int p in c) label[count - 1][p] = next;
                next++;
            }

            for (int d = count - 2; d >= 0; d--)
            {
                List<int[]> cur = BySize(detections[d]);
                int[] later = label[d + 1];

                // overlaps with the later detection's numbered coalitions
                List<(int overlap, int id, int index)> cand = new List<(int, int, int)>();
                for (int i = 0; i < cur.Count; i++)
                {
                    Dictionary<int, int> shared = new Dictionary<int, int>();
                    foreach (int p in cur[i])
                    {
                        int id = later[p];
                        if (id >= 0) shared[id] = shared.TryGetValue(id, out int v) ? v + 1 : 1;
                    }

                    foreach (KeyValuePair<int, int> kv in shared) cand.Add((kv.Value, kv.Key, i));
                }

                cand.Sort((x, y) => x.overlap != y.overlap ? y.overlap.CompareTo(x.overlap)
                    : x.id != y.id ? x.id.CompareTo(y.id) : x.index.CompareTo(y.index));
                int[] idOf = new int[cur.Count];
                for (int i = 0; i < idOf.Length; i++) idOf[i] = -1;
                HashSet<int> taken = new HashSet<int>();
                foreach ((int overlap, int id, int index) c in cand)
                {
                    if (idOf[c.index] >= 0 || taken.Contains(c.id)) continue;
                    idOf[c.index] = c.id;
                    taken.Add(c.id);
                }

                for (int i = 0; i < cur.Count; i++)
                {
                    if (idOf[i] < 0) idOf[i] = next++;
                    foreach (int p in cur[i]) label[d][p] = idOf[i];
                }
            }

            return label;
        }

        /// <summary>Adds a value to an ascending list unless it is there.</summary>
        static void Insert(List<int> list, int v)
        {
            int at = list.BinarySearch(v);
            if (at < 0) list.Insert(~at, v);
        }

        /// <summary>Communities by size (largest first), ties by their lowest member.</summary>
        static List<int[]> BySize(List<int[]> communities)
        {
            List<int[]> list = new List<int[]>(communities);
            list.Sort((x, y) => x.Length != y.Length ? y.Length.CompareTo(x.Length) : x[0].CompareTo(y[0]));
            return list;
        }

        /// <summary>
        /// Normalized mutual information 2 I(a; b) / (H(a) + H(b)) of two labelings (non-negative labels) of the same items
        /// (natural logs; 0 when both are constant). The callers give every player outside a coalition its own label.
        /// </summary>
        public static double Nmi(IReadOnlyList<int> a, IReadOnlyList<int> b)
        {
            int n = a.Count;
            if (n == 0 || b.Count != n) return 0;
            Dictionary<int, int> ca = new Dictionary<int, int>(), cb = new Dictionary<int, int>();
            Dictionary<long, int> cab = new Dictionary<long, int>();
            for (int i = 0; i < n; i++)
            {
                ca[a[i]] = ca.TryGetValue(a[i], out int x) ? x + 1 : 1;
                cb[b[i]] = cb.TryGetValue(b[i], out int y) ? y + 1 : 1;
                long key = ((long)a[i] << 32) ^ (uint)b[i];
                cab[key] = cab.TryGetValue(key, out int z) ? z + 1 : 1;
            }

            // sums in a fixed order (sorted keys), so the value is the same on every run
            List<long> keys = new List<long>(cab.Keys);
            keys.Sort();
            double mi = 0;
            foreach (long key in keys)
            {
                int ai = (int)(key >> 32), bi = (int)(uint)(key & 0xFFFFFFFFL);
                double pab = cab[key] / (double)n, pa = ca[ai] / (double)n, pb = cb[bi] / (double)n;
                mi += pab * Math.Log(pab / (pa * pb));
            }

            double ha = Entropy(ca, n), hb = Entropy(cb, n);
            return ha + hb > 0 ? 2 * mi / (ha + hb) : 0;
        }

        static double Entropy(Dictionary<int, int> counts, int n)
        {
            List<int> keys = new List<int>(counts.Keys);
            keys.Sort();
            double h = 0;
            foreach (int k in keys)
            {
                double p = counts[k] / (double)n;
                h -= p * Math.Log(p);
            }

            return h;
        }
    }
}

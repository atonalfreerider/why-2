using System;
using System.Collections.Generic;
using System.Globalization;

namespace Why.Life
{
    /// <summary>
    /// A time-calibrated phylogeny stored as flat arrays. Parsed in a single pass (O(n)) from Newick.
    /// Node 0 is the root. Ages are in millions of years ago; for an internal node the age is when its
    /// lineage splits into its children, for a leaf it is 0 (extant).
    /// </summary>
    public sealed class PhyloTree
    {
        public readonly List<int> Parent = new List<int>();
        public readonly List<float> BranchLength = new List<float>();
        public readonly List<string> Label = new List<string>();
        public readonly List<List<int>> Children = new List<List<int>>();

        public int Count => Parent.Count;
        public float[] Depth;       // distance from root
        public float[] AgeMya;      // RootAge - Depth
        public float RootAgeMya;

        public bool IsLeaf(int i) => Children[i].Count == 0;

        int NewNode(int parent)
        {
            int id = Parent.Count;
            Parent.Add(parent);
            BranchLength.Add(0);
            Label.Add(null);
            Children.Add(new List<int>(2));
            if (parent >= 0) Children[parent].Add(id);
            return id;
        }

        public static PhyloTree ParseNewick(string s)
        {
            PhyloTree t = new PhyloTree();
            int cur = t.NewNode(-1);
            int n = s.Length;
            int i = 0;
            while (i < n)
            {
                char c = s[i];
                switch (c)
                {
                    case '(':
                        cur = t.NewNode(cur);
                        i++;
                        break;
                    case ',':
                        cur = t.NewNode(t.Parent[cur]);
                        i++;
                        break;
                    case ')':
                        cur = t.Parent[cur];
                        i++;
                        break;
                    case ':':
                    {
                        int start = ++i;
                        while (i < n && s[i] != ',' && s[i] != ')' && s[i] != ';' && s[i] != '[' && s[i] != '(') i++;
                        float.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture,
                            out float len);
                        t.BranchLength[cur] = len;
                        break;
                    }
                    case '[':
                        while (i < n && s[i] != ']') i++;
                        i++;
                        break;
                    case ';':
                        i = n;
                        break;
                    default:
                    {
                        if (char.IsWhiteSpace(c))
                        {
                            i++;
                            break;
                        }

                        int start = i;
                        while (i < n && s[i] != ':' && s[i] != ',' && s[i] != ')' && s[i] != '(' && s[i] != ';' &&
                               s[i] != '[') i++;
                        t.Label[cur] = s.Substring(start, i - start).Trim();
                        break;
                    }
                }
            }

            t.ComputeDepths();
            return t;
        }

        void ComputeDepths()
        {
            int n = Count;
            Depth = new float[n];
            AgeMya = new float[n];
            // parents are always created before their children, so a forward pass suffices
            float maxLeafDepth = 0;
            for (int i = 0; i < n; i++)
            {
                Depth[i] = Parent[i] < 0 ? 0 : Depth[Parent[i]] + BranchLength[i];
                if (Children[i].Count == 0 && Depth[i] > maxLeafDepth) maxLeafDepth = Depth[i];
            }

            RootAgeMya = maxLeafDepth;
            // leaves are extant (float rounding would otherwise give them ages of a few thousand years)
            for (int i = 0; i < n; i++) AgeMya[i] = Children[i].Count == 0 ? 0 : Math.Max(0, RootAgeMya - Depth[i]);
        }

        /// <summary>Index of the first leaf with this label, or -1.</summary>
        public int FindLeaf(string label)
        {
            for (int i = 0; i < Count; i++)
            {
                if (Children[i].Count == 0 && Label[i] == label) return i;
            }

            return -1;
        }

        /// <summary>Most recent common ancestor of two nodes.</summary>
        public int Mrca(int a, int b)
        {
            if (a < 0 || b < 0) return -1;
            HashSet<int> seen = new HashSet<int>();
            for (int x = a; x >= 0; x = Parent[x]) seen.Add(x);
            for (int y = b; y >= 0; y = Parent[y])
            {
                if (seen.Contains(y)) return y;
            }

            return 0;
        }
    }
}

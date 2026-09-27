using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Why.Life
{
    /// <summary>
    /// GREEN layer: the tree of life (TimeTree of Life 2009, 1610 families, root 4.2 Ga) drawn as an
    /// expanding set of roots. Time runs along the clock. Children are ordered by food chain (top
    /// predators inside) with our own lineage (-> Hominidae) always first, so the inner track is the path
    /// that leads to us.
    ///
    /// Layout: a lineage is a chain of first children from a split down to a leaf. Each lineage is born
    /// beside its parent with zero weight and gains weight gradually; its radius is the summed weight of
    /// the living lineages inside it. The tree therefore widens only as lineages multiply - there is no
    /// fixed outer edge. Life dissolves before the straight human branch at 3 o'clock, except our own
    /// lineage, which stays lit until Homo sapiens rises into the human layer (300,000 years ago).
    ///
    /// Everything is computed on a worker thread and uploaded as one mesh (one draw call).
    /// </summary>
    public sealed class LifeLayer : GraphLayer
    {
        public const string TreePath = "TimetreeOfLife2009";
        public const string TraitsPath = "Data/life_traits";
        public const string CladesPath = "Data/life_clades";

        /// <summary>Radial extent of the fully grown root system (world units at rhoScale 1).</summary>
        public const float TotalWidth = 2.8f;

        /// <summary>How much more space a lineage claims as it ages (1 + RootGrowth * age^2).</summary>
        const float RootGrowth = 40f;

        const float GridStep = 0.001f;
        const string Us = "Hominidae";
        const double SapiensYearsAgo = 300_000;

        public override int Order => 10;
        public override IEnumerable<string> RequiredTexts => new[] { TreePath, TraitsPath, CladesPath };

        public PhyloTree Tree { get; private set; }
        public int LineagePathLength { get; private set; }

        LineMeshBuilder lines;
        Material lineMat;

        // lineage layout
        int lineageCount;
        int[] nodeLineage;          // node -> lineage
        float[] lineageBirthU;      // lineage -> arc where it branches off
        float[] lineageRamp;        // lineage -> arc over which it gains full weight
        float gridTop, gridBottom, growthSpan, prefixTotal = 1f, logK = 1f, logNorm = 1f;
        int gridCount;
        float[] prefix;             // [grid * lineageCount + lineage] = summed weight inside the lineage

        sealed class Trait
        {
            [JsonProperty("t")] public float T = 2f;
            [JsonProperty("role")] public string Role;
            [JsonProperty("common")] public string Common;
        }

        sealed class CladeDto
        {
            public string id;
            public string name;
            public string[] mrca;
            public int tier = 2;
            public string blurb;
        }

        sealed class EventDto
        {
            public string id;
            public string name;
            public double mya;
            public int tier = 2;
            public string blurb;
        }

        sealed class CladeFile
        {
            public List<CladeDto> clades = new List<CladeDto>();
            public List<EventDto> events = new List<EventDto>();
        }

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            string newick = ctx.Text(TreePath);
            if (string.IsNullOrEmpty(newick)) return;
            PhyloTree tree = PhyloTree.ParseNewick(newick);
            Tree = tree;
            int n = tree.Count;

            Dictionary<string, Trait> traits = ParseOr(ctx.Text(TraitsPath), new Dictionary<string, Trait>());
            CladeFile cladeFile = ParseOr(ctx.Text(CladesPath), new CladeFile());

            // --- ordering: our lineage first, then by food-chain position (top predators inside) ---
            int us = tree.FindLeaf(Us);
            bool[] onPath = new bool[n];
            for (int x = us; x >= 0; x = tree.Parent[x]) onPath[x] = true;

            float[] maxT = new float[n], sumT = new float[n];
            int[] leaves = new int[n];
            for (int i = n - 1; i >= 0; i--) // children always have larger indices than parents
            {
                if (tree.IsLeaf(i))
                {
                    float t = traits.TryGetValue(tree.Label[i] ?? "", out Trait tr) ? tr.T : 2f;
                    maxT[i] = sumT[i] = t;
                    leaves[i] = 1;
                }
                else
                {
                    foreach (int c in tree.Children[i])
                    {
                        maxT[i] = Math.Max(maxT[i], maxT[c]);
                        sumT[i] += sumT[c];
                        leaves[i] += leaves[c];
                    }
                }
            }

            for (int i = 0; i < n; i++)
            {
                List<int> ch = tree.Children[i];
                if (ch.Count < 2) continue;
                ch.Sort((a, b) =>
                {
                    if (onPath[a] != onPath[b]) return onPath[a] ? -1 : 1;
                    float ka = 0.6f * maxT[a] + 0.4f * sumT[a] / leaves[a];
                    float kb = 0.6f * maxT[b] + 0.4f * sumT[b] / leaves[b];
                    return kb.CompareTo(ka);
                });
            }

            // --- preorder numbering (highlight ids) and lineages (first-child chains) ---
            int[] pre = new int[n];
            int[] size = new int[n];
            List<int> order = new List<int>(n);
            Stack<int> stack = new Stack<int>();
            stack.Push(0);
            while (stack.Count > 0)
            {
                int x = stack.Pop();
                pre[x] = order.Count;
                order.Add(x);
                List<int> ch = tree.Children[x];
                for (int k = ch.Count - 1; k >= 0; k--) stack.Push(ch[k]);
            }

            for (int k = order.Count - 1; k >= 0; k--)
            {
                int x = order[k];
                size[x] = 1;
                foreach (int c in tree.Children[x]) size[x] += size[c];
            }

            float uHandoff = GraphWarp.BasePath.HandoffArc;
            float uRoot = DeepTime.Arc(tree.RootAgeMya * 1e6);
            float[] nodeU = new float[n];
            for (int i = 0; i < n; i++) nodeU[i] = tree.IsLeaf(i) ? 0f : DeepTime.Arc(tree.AgeMya[i] * 1e6);
            nodeLineage = new int[n];
            List<int> lineageStart = new List<int>();
            List<float> births = new List<float>();
            foreach (int x in order) // preorder: lineages are numbered in inner-to-outer order
            {
                int p = tree.Parent[x];
                if (p < 0 || tree.Children[p][0] != x)
                {
                    nodeLineage[x] = lineageStart.Count;
                    lineageStart.Add(x);
                    births.Add(p < 0 ? uRoot : nodeU[p]);
                }
                else
                {
                    nodeLineage[x] = nodeLineage[p];
                }
            }

            lineageCount = lineageStart.Count;
            lineageBirthU = births.ToArray();
            lineageRamp = new float[lineageCount];
            for (int l = 0; l < lineageCount; l++)
            {
                lineageRamp[l] = l == 0 ? 0f : Mathf.Clamp(0.45f * (lineageBirthU[l] - uHandoff), 0.004f, 0.05f);
            }

            // --- summed inner weight of every lineage on a fine arc grid ---
            gridTop = uRoot;
            gridBottom = uHandoff;
            growthSpan = Mathf.Max(uRoot - uHandoff, 1e-3f);
            gridCount = Mathf.CeilToInt((gridTop - gridBottom) / GridStep) + 1;
            prefix = new float[gridCount * lineageCount];
            float total = 0;
            for (int g = 0; g < gridCount; g++)
            {
                float u = GridU(g);
                float sum = 0;
                int row = g * lineageCount;
                for (int l = 0; l < lineageCount; l++)
                {
                    prefix[row + l] = sum;
                    sum += Weight(l, u);
                }

                total = Mathf.Max(total, sum);
            }

            // map summed weight to radius logarithmically: early, deep structure and the lineages nearest
            // us get room, the crowded outer bundles of recent families are compressed
            prefixTotal = Mathf.Max(total, 1f);
            logK = prefixTotal * 0.004f;
            logNorm = TotalWidth / Mathf.Log(1f + prefixTotal / logK);

            LineagePathLength = 0;
            for (int x = us; x >= 0; x = tree.Parent[x]) LineagePathLength++;
            float maxRho = TotalWidth;

            // --- geometry: one polyline per lineage ---
            lines = new LineMeshBuilder(160_000);
            List<LinePoint> pts = new List<LinePoint>(512);
            List<float> ids = new List<float>(512);
            float y = GraphStyle.LifeY;
            float uSapiens = DeepTime.Arc(SapiensYearsAgo);
            for (int l = 0; l < lineageCount; l++)
            {
                bool ours = l == 0;
                float uBirth = lineageBirthU[l];
                float uEnd = uHandoff;
                if (uBirth <= uEnd) continue;

                // the chain of nodes along this lineage, oldest first
                List<int> chain = new List<int>();
                for (int x = lineageStart[l]; ; x = tree.Children[x][0])
                {
                    chain.Add(x);
                    if (tree.IsLeaf(x)) break;
                }

                pts.Clear();
                ids.Clear();
                int ci = 0;
                float rampEnd = uBirth - lineageRamp[l];
                float u = uBirth;
                while (true)
                {
                    // advance to the node that covers this arc (a node's own edge ends at its split)
                    while (ci < chain.Count - 1 && u < nodeU[chain[ci]]) ci++;
                    int node = chain[ci];

                    float rho = RhoAt(l, u);
                    float relevance = 1f - Mathf.Clamp01(rho / maxRho);
                    float handoff = GraphStyle.HandoffFade(u);
                    float fade = ours ? OurFade(u, uSapiens, uHandoff) : handoff * handoff * handoff;
                    float alpha = (ours ? 1f : 0.08f + 0.3f * relevance * relevance) * fade;
                    float intensity = ours ? 2.2f : 0.4f + 0.4f * relevance;
                    float widthWorld = 0.0006f * Mathf.Log(1 + leaves[node], 2) + (ours ? 0.004f : 0f);
                    pts.Add(new LinePoint(new Vector3(u, y, rho), Tint(alpha), ours ? 2.2f : 1f, widthWorld, intensity));
                    ids.Add(GraphIds.LifeNode(pre[node]));

                    if (u <= uEnd) break;
                    float step = u > rampEnd ? GridStep : GridStep * 2.5f;
                    u = Mathf.Max(uEnd, u - step);
                }

                lines.AddPolyline(pts, ids, ours ? 1f : 0f);
            }

            // --- anchors and labels ---
            RegisterLeaves(ctx, tree, traits, pre);
            RegisterClades(ctx, tree, cladeFile, pre, size);
            RegisterEvents(ctx, cladeFile);

            ctx.Share("life.layer", this);
            Debug.Log($"[Why] LifeLayer.Prepare {sw.ElapsedMilliseconds} ms ({lineageCount} lineages, {lines.VertexCount / 2} points)");
        }

        float GridU(int g) => gridTop - g * GridStep;

        /// <summary>
        /// Space a lineage claims at an arc: 0 before it branches off, easing in, then growing with the square
        /// of its age. Ancient splits therefore open wide gaps between bundles while recent splits stay tight,
        /// and the whole system keeps diverging like roots instead of running in parallel tracks.
        /// </summary>
        float Weight(int l, float u)
        {
            float b = lineageBirthU[l];
            if (u > b) return 0f;
            float age = (b - u) / growthSpan;
            float growth = 1f + RootGrowth * age * age;
            float r = lineageRamp[l];
            if (r <= 0) return growth;
            float t = Mathf.Clamp01((b - u) / r);
            return t * t * (3 - 2 * t) * growth;
        }

        /// <summary>Radial position of a lineage at an arc (interpolated on the grid).</summary>
        public float RhoAt(int lineage, float u)
        {
            float gf = Mathf.Clamp((gridTop - u) / GridStep, 0, gridCount - 1);
            int g0 = Mathf.Min((int)gf, gridCount - 2);
            float t = gf - g0;
            float a = prefix[g0 * lineageCount + lineage];
            float b = prefix[(g0 + 1) * lineageCount + lineage];
            return logNorm * Mathf.Log(1f + (a + (b - a) * t) / logK);
        }

        /// <summary>Radial position of a node's lineage at an arc.</summary>
        public float NodeRho(int node, float u) => RhoAt(nodeLineage[node], Mathf.Clamp(u, gridBottom, gridTop));

        /// <summary>Our lineage stays lit until Homo sapiens rises into the human layer, then hands over.</summary>
        static float OurFade(float u, float uSapiens, float uHandoff)
        {
            float t = Mathf.Clamp01((u - uHandoff) / Mathf.Max(uSapiens - uHandoff, 1e-5f));
            return 0.25f + 0.75f * t * t * (3 - 2 * t);
        }

        void RegisterLeaves(GraphContext ctx, PhyloTree tree, Dictionary<string, Trait> traits, int[] pre)
        {
            for (int v = 0; v < tree.Count; v++)
            {
                if (!tree.IsLeaf(v) || string.IsNullOrEmpty(tree.Label[v])) continue;
                string label = tree.Label[v];
                traits.TryGetValue(label, out Trait tr);
                string display = tr != null && !string.IsNullOrEmpty(tr.Common) ? $"{tr.Common} ({label})" : label;
                double splitYa = tree.AgeMya[tree.Parent[v]] * 1e6;
                float u0 = DeepTime.Arc(splitYa);
                float rho = NodeRho(v, u0 - 0.004f);
                Anchor a = new Anchor
                {
                    Key = "leaf:" + label,
                    Label = display,
                    Blurb = tr != null ? $"{Cap(tr.Role)} - trophic level {tr.T:0.0}" : null,
                    Level = GraphLevel.Life,
                    YearsAgo = splitYa,
                    EndYearsAgo = 0,
                    Y = GraphStyle.LifeY,
                    Rho = rho,
                    Ids = IdRange.Single(GraphIds.LifeNode(pre[v])),
                    Tier = 3
                };
                Anchors.Register(a);

                // label the family where its own branch begins (only where life is still visible)
                if (GraphStyle.HandoffFade(u0 - 0.004f) < 0.3f && label != Us) continue;
                bool isUs = label == Us;
                ctx.Labels.Add(new LabelSpec
                {
                    Text = display,
                    Data = new Vector3(u0 - 0.004f, GraphStyle.LifeY, rho),
                    Priority = isUs ? 60 : 0.2f + (tr?.T ?? 2) * 0.02f,
                    SizePx = isUs ? 15 : 10.5f,
                    Color = isUs ? GraphStyle.Text : GraphStyle.TextDim,
                    PixelOffset = new Vector2(4, 6),
                    AnchorKey = a.Key,
                    HandoffFade = !isUs,
                    Ids = a.Ids
                });
            }
        }

        void RegisterClades(GraphContext ctx, PhyloTree tree, CladeFile file, int[] pre, int[] size)
        {
            foreach (CladeDto c in file.clades)
            {
                if (c?.mrca == null || c.mrca.Length == 0 || string.IsNullOrEmpty(c.id)) continue;
                int a = tree.FindLeaf(c.mrca[0]);
                int b = c.mrca.Length > 1 ? tree.FindLeaf(c.mrca[1]) : a;
                int node = a == b ? a : tree.Mrca(a, b);
                if (node < 0)
                {
                    Debug.LogWarning($"[Why] clade '{c.id}' MRCA leaves not found: {string.Join(",", c.mrca)}");
                    continue;
                }

                // a clade is anchored at its crown (where it starts to diversify); a single leaf at its stem
                double stemYa = node == 0 ? tree.RootAgeMya * 1e6 : tree.AgeMya[tree.Parent[node]] * 1e6;
                double crownYa = tree.AgeMya[node] * 1e6;
                double ya = crownYa > 0 ? crownYa : stemYa;
                float u = DeepTime.Arc(ya);
                Anchor anchor = new Anchor
                {
                    Key = "clade:" + c.id,
                    Label = c.name ?? c.id,
                    Blurb = c.blurb,
                    Level = GraphLevel.Life,
                    YearsAgo = ya,
                    EndYearsAgo = 0,
                    Y = GraphStyle.LifeY,
                    Rho = NodeRho(node, u),
                    Ids = new IdRange(GraphIds.LifeNode(pre[node]), GraphIds.LifeNode(pre[node] + size[node] - 1)),
                    Tier = c.tier
                };
                Anchors.Register(anchor);

                ctx.Labels.Add(new LabelSpec
                {
                    Text = anchor.Label,
                    Data = anchor.Data,
                    Priority = c.tier == 1 ? 40 : c.tier == 2 ? 20 : 8,
                    SizePx = c.tier == 1 ? 15 : c.tier == 2 ? 13 : 11.5f,
                    Color = GraphStyle.Text,
                    PixelOffset = new Vector2(6, 9),
                    AnchorKey = anchor.Key,
                    HandoffFade = c.id != "hominidae",
                    Ids = anchor.Ids
                });
            }

            // our whole lineage is always addressable
            Anchors.Register(new Anchor
            {
                Key = "clade:_lineage", Label = "Our lineage", Level = GraphLevel.Life,
                YearsAgo = tree.RootAgeMya * 1e6, EndYearsAgo = 0, Y = GraphStyle.LifeY, Rho = 0,
                Ids = new IdRange(GraphIds.LifeNode(0), GraphIds.LifeNode(LineagePathLength - 1)), Tier = 1
            });
        }

        void RegisterEvents(GraphContext ctx, CladeFile file)
        {
            foreach (EventDto e in file.events)
            {
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                Anchor a = new Anchor
                {
                    Key = "lifeevent:" + e.id,
                    Label = e.name ?? e.id,
                    Blurb = e.blurb,
                    Level = GraphLevel.Life,
                    YearsAgo = e.mya * 1e6,
                    EndYearsAgo = e.mya * 1e6,
                    Y = GraphStyle.LifeY,
                    Rho = -0.06f,
                    Tier = e.tier
                };
                Anchors.Register(a);
                ctx.Labels.Add(new LabelSpec
                {
                    Text = a.Label,
                    Data = a.Data,
                    Priority = e.tier == 1 ? 30 : e.tier == 2 ? 14 : 6,
                    SizePx = e.tier == 1 ? 13 : 11.5f,
                    Color = GraphStyle.TextDim,
                    Align = TMPro.TextAlignmentOptions.Right,
                    PixelOffset = new Vector2(-6, 0),
                    AnchorKey = a.Key
                });
            }
        }

        public override void Upload(GraphContext ctx)
        {
            if (lines == null) return;
            lineMat = GraphMaterials.Line(GraphStyle.Life, 1f, GraphMaterials.QueueLife + 1, true, 0f, 1f);
            AddMesh("TreeOfLife", lines.ToMesh("TreeOfLife"), lineMat);

            Highlighter.SetPersistent("life", new[]
            {
                (new IdRange(GraphIds.LifeNode(0), GraphIds.LifeNode(LineagePathLength - 1)), 1.6f)
            });

            lines = null;
        }

        static Color32 Tint(float alpha) => new Color32(255, 255, 255, (byte)Mathf.Clamp(alpha * 255f, 0, 255));

        static T ParseOr<T>(string json, T fallback) where T : class
        {
            if (string.IsNullOrEmpty(json)) return fallback;
            try
            {
                return JsonConvert.DeserializeObject<T>(json) ?? fallback;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Why] could not parse {typeof(T).Name}: {e.Message}");
                return fallback;
            }
        }

        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1).Replace('_', ' ');
    }
}

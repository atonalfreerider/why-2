using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Why.Life
{
    /// <summary>
    /// GREEN layer: the tree of life (TimeTree of Life 2009, 1610 families, root 4.2 Ga) as a radial
    /// phylogram. Time runs along the clock; each lineage keeps its own radial track. Children are
    /// ordered by food chain (top predators inside) with our own lineage (-> Hominidae) always first, so
    /// the inner track is the path that leads to us and every other branch peels off outward.
    ///
    /// Everything is computed on a worker thread and uploaded as one mesh (one draw call).
    /// </summary>
    public sealed class LifeLayer : GraphLayer
    {
        public const string TreePath = "TimetreeOfLife2009";
        public const string TraitsPath = "Data/life_traits";
        public const string CladesPath = "Data/life_clades";

        /// <summary>Radial spacing between leaf tracks.</summary>
        public const float TrackSpacing = 0.0016f;

        const float ArcStep = 0.0025f;
        const string Us = "Hominidae";

        public override int Order => 10;
        public override IEnumerable<string> RequiredTexts => new[] { TreePath, TraitsPath, CladesPath };

        public PhyloTree Tree { get; private set; }
        public int[] Preorder { get; private set; }     // node -> preorder index
        public int[] SubtreeSize { get; private set; }  // node -> number of nodes in subtree
        public float[] Rho { get; private set; }        // node -> radial track
        public int LineagePathLength { get; private set; }

        LineMeshBuilder lines;
        SurfaceMeshBuilder veil;
        Material lineMat, veilMat;

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

            // --- preorder numbering, leaf tracks ---
            int[] pre = new int[n];
            int[] size = new int[n];
            float[] rho = new float[n];
            int counter = 0, leafIndex = 0;
            Stack<int> stack = new Stack<int>();
            stack.Push(0);
            List<int> order = new List<int>(n);
            while (stack.Count > 0)
            {
                int x = stack.Pop();
                pre[x] = counter++;
                order.Add(x);
                List<int> ch = tree.Children[x];
                for (int k = ch.Count - 1; k >= 0; k--) stack.Push(ch[k]);
                if (ch.Count == 0) rho[x] = TrackSpacing * leafIndex++;
            }

            for (int k = order.Count - 1; k >= 0; k--)
            {
                int x = order[k];
                size[x] = 1;
                foreach (int c in tree.Children[x]) size[x] += size[c];
                if (!tree.IsLeaf(x)) rho[x] = rho[tree.Children[x][0]]; // the first (innermost) child continues straight
            }

            Preorder = pre;
            SubtreeSize = size;
            Rho = rho;
            int pathLen = 0;
            for (int x = us; x >= 0; x = tree.Parent[x]) pathLen++;
            LineagePathLength = pathLen;
            float maxRho = TrackSpacing * Math.Max(1, leafIndex - 1);

            // --- geometry ---
            lines = new LineMeshBuilder(260_000);
            List<LinePoint> pts = new List<LinePoint>(512);
            float y = GraphStyle.LifeY;
            for (int v = 1; v < n; v++)
            {
                int p = tree.Parent[v];
                float u0 = DeepTime.Arc(tree.AgeMya[p] * 1e6);
                float u1 = tree.IsLeaf(v) ? DeepTime.NowArc : DeepTime.Arc(tree.AgeMya[v] * 1e6);
                if (u0 <= u1) continue;

                bool lineage = onPath[v];
                float relevance = 1f - rho[v] / maxRho;                     // 1 inside .. 0 outside
                float widthWorld = 0.0006f * Mathf.Log(1 + leaves[v], 2) + (lineage ? 0.004f : 0f);
                float intensity = lineage ? 2.2f : 0.45f + 0.45f * relevance;
                float baseAlpha = lineage ? 1f : 0.14f + 0.36f * relevance * relevance;
                float widthPx = lineage ? 2.2f : 1f;

                pts.Clear();
                float r0 = rho[p], r1 = rho[v];
                float span = u0 - u1;
                // lineages split off with an S-curve over a short arc, then keep their track
                float bend = Mathf.Abs(r1 - r0) > 1e-6f ? Mathf.Min(span * 0.5f, 0.012f + 1.2f * Mathf.Abs(r1 - r0) * 0.05f) : 0f;
                if (bend > 0)
                {
                    const int bendSteps = 10;
                    for (int k = 0; k <= bendSteps; k++)
                    {
                        float f = k / (float)bendSteps;
                        float e = f * f * (3 - 2 * f);
                        float u = u0 - bend * f;
                        pts.Add(new LinePoint(new Vector3(u, y, Mathf.Lerp(r0, r1, e)),
                            Tint(baseAlpha * PresentFade(u, lineage)), widthPx, widthWorld, intensity));
                    }
                }
                else
                {
                    pts.Add(new LinePoint(new Vector3(u0, y, r1), Tint(baseAlpha * PresentFade(u0, lineage)), widthPx,
                        widthWorld, intensity));
                }

                float uStart = u0 - bend;
                int steps = Mathf.Max(1, Mathf.CeilToInt((uStart - u1) / ArcStep));
                for (int k = 1; k <= steps; k++)
                {
                    float u = Mathf.Lerp(uStart, u1, k / (float)steps);
                    pts.Add(new LinePoint(new Vector3(u, y, r1), Tint(baseAlpha * PresentFade(u, lineage)), widthPx,
                        widthWorld, intensity));
                }

                lines.AddPolyline(pts, GraphIds.LifeNode(pre[v]), lineage ? 1f : 0f);
            }

            // --- a faint veil under the branches: the extent of life at each moment ---
            veil = new SurfaceMeshBuilder();
            BuildVeil(tree, rho, y, maxRho);

            // --- anchors and labels ---
            RegisterLeaves(ctx, tree, traits, pre, rho, leaves);
            RegisterClades(ctx, tree, cladeFile, pre, size, rho, us);
            RegisterEvents(ctx, cladeFile);

            ctx.Share("life.layer", this);
        }

        void BuildVeil(PhyloTree tree, float[] rho, float y, float maxRho)
        {
            // outermost track alive at each sampled arc
            const int samples = 420;
            float uStart = DeepTime.Arc(tree.RootAgeMya * 1e6);
            float[] outer = new float[samples + 1];
            float[] us = new float[samples + 1];
            for (int s = 0; s <= samples; s++) us[s] = Mathf.Lerp(uStart, DeepTime.NowArc, s / (float)samples);
            for (int v = 1; v < tree.Count; v++)
            {
                float u0 = DeepTime.Arc(tree.AgeMya[tree.Parent[v]] * 1e6);
                for (int s = 0; s <= samples; s++)
                {
                    if (us[s] <= u0 && rho[v] > outer[s]) outer[s] = rho[v];
                }
            }

            List<Vector3> inner = new List<Vector3>(samples + 1), outerPts = new List<Vector3>(samples + 1);
            List<Color32> cols = new List<Color32>(samples + 1);
            for (int s = 0; s <= samples; s++)
            {
                inner.Add(new Vector3(us[s], y - 0.002f, -0.01f));
                outerPts.Add(new Vector3(us[s], y - 0.002f, outer[s] + 0.02f));
                float f = s / (float)samples;
                cols.Add(Tint(Mathf.SmoothStep(0, 1, f * 8) * PresentFade(us[s], false)));
            }

            veil.AddBand(inner, outerPts, cols, GraphIds.LifeNode(0), 1f, 0f);
        }

        void RegisterLeaves(GraphContext ctx, PhyloTree tree, Dictionary<string, Trait> traits, int[] pre, float[] rho,
            int[] leaves)
        {
            for (int v = 0; v < tree.Count; v++)
            {
                if (!tree.IsLeaf(v) || string.IsNullOrEmpty(tree.Label[v])) continue;
                string label = tree.Label[v];
                traits.TryGetValue(label, out Trait tr);
                string display = tr != null && !string.IsNullOrEmpty(tr.Common) ? $"{tr.Common} ({label})" : label;
                double splitYa = tree.AgeMya[tree.Parent[v]] * 1e6;
                Anchor a = new Anchor
                {
                    Key = "leaf:" + label,
                    Label = display,
                    Blurb = tr != null ? $"{Cap(tr.Role)} - trophic level {tr.T:0.0}" : null,
                    Level = GraphLevel.Life,
                    YearsAgo = splitYa,
                    EndYearsAgo = 0,
                    Y = GraphStyle.LifeY,
                    Rho = rho[v],
                    Ids = IdRange.Single(GraphIds.LifeNode(pre[v])),
                    Tier = 3
                };
                Anchors.Register(a);

                // label the family where its own branch begins
                float u0 = DeepTime.Arc(splitYa);
                float priority = label == Us ? 60 : 0.2f + (tr?.T ?? 2) * 0.02f;
                ctx.Labels.Add(new LabelSpec
                {
                    Text = display,
                    Data = new Vector3(u0 - 0.004f, GraphStyle.LifeY, rho[v]),
                    Priority = priority,
                    SizePx = label == Us ? 15 : 10.5f,
                    Color = label == Us ? GraphStyle.Text : GraphStyle.TextDim,
                    PixelOffset = new Vector2(4, 6),
                    AnchorKey = a.Key,
                    Ids = a.Ids
                });
            }
        }

        void RegisterClades(GraphContext ctx, PhyloTree tree, CladeFile file, int[] pre, int[] size, float[] rho, int us)
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

                // a clade begins where its stem branch splits from its parent
                double ya = node == 0 ? tree.RootAgeMya * 1e6 : tree.AgeMya[tree.Parent[node]] * 1e6;
                double crownYa = tree.AgeMya[node] * 1e6;
                Anchor anchor = new Anchor
                {
                    Key = "clade:" + c.id,
                    Label = c.name ?? c.id,
                    Blurb = c.blurb,
                    Level = GraphLevel.Life,
                    YearsAgo = crownYa > 0 ? crownYa : ya,
                    EndYearsAgo = 0,
                    Y = GraphStyle.LifeY,
                    Rho = rho[node],
                    Ids = new IdRange(GraphIds.LifeNode(pre[node]), GraphIds.LifeNode(pre[node] + size[node] - 1)),
                    Tier = c.tier
                };
                Anchors.Register(anchor);

                ctx.Labels.Add(new LabelSpec
                {
                    Text = anchor.Label,
                    Data = new Vector3(anchor.U, GraphStyle.LifeY, rho[node]),
                    Priority = c.tier == 1 ? 40 : c.tier == 2 ? 20 : 8,
                    SizePx = c.tier == 1 ? 15 : c.tier == 2 ? 13 : 11.5f,
                    Color = GraphStyle.Text,
                    PixelOffset = new Vector2(6, 9),
                    AnchorKey = anchor.Key,
                    Ids = anchor.Ids
                });
            }

            // the whole tree and our lineage are always addressable
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

            veilMat = GraphMaterials.Surface(new Color(GraphStyle.Life.r, GraphStyle.Life.g, GraphStyle.Life.b, 0.06f),
                1f, GraphMaterials.QueueLife, true, 0f);
            veilMat.SetFloat("_EdgeSoft", 0.35f);
            AddMesh("LifeVeil", veil.ToMesh("LifeVeil"), veilMat);

            Highlighter.SetPersistent("life", new[]
            {
                (new IdRange(GraphIds.LifeNode(0), GraphIds.LifeNode(LineagePathLength - 1)), 1.6f)
            });

            lines = null;
            veil = null;
        }

        static Color32 Tint(float alpha) => new Color32(255, 255, 255, (byte)Mathf.Clamp(alpha * 255f, 0, 255));

        /// <summary>
        /// The last quadrant of the clock is the last few years shrinking to "now": every extant family
        /// is still alive there, so all lines except our own lineage dissipate into the present.
        /// </summary>
        public static float PresentFade(float u, bool lineage)
        {
            if (lineage) return 1f;
            float f = Mathf.Clamp01((u - 0.16f) / (0.27f - 0.16f));
            return 0.12f + 0.88f * f * f * (3 - 2 * f);
        }

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

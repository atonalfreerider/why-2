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
    /// The mass extinctions narrow the tree (<see cref="MassExtinctions"/>): the TimeTree holds only families that survive
    /// today, so the share of species each extinction removed scales the living weight inside the log, B(u) on top of
    /// the summed weight, and the tree shrinks at each extinction and grows back over its recovery. A thin outline along
    /// the tree's outer edge shows the dips as spikes even where a recovery is a hairline on the clock.
    ///
    /// Everything is computed on a worker thread and uploaded as one mesh (one draw call).
    /// </summary>
    public sealed class LifeLayer : GraphLayer
    {
        public const string TreePath = "TimetreeOfLife2009";
        public const string TraitsPath = "Data/life_traits";
        public const string CladesPath = "Data/life_clades";
        public const string DomesticationPath = "Data/domestication";

        /// <summary>Radial extent of the fully grown root system (world units at rhoScale 1).</summary>
        public const float TotalWidth = 3.6f;

        /// <summary>
        /// Highlight ids of the outline along the tree's outer edge, from the free top of the life range (GraphIds.LifeBase,
        /// 10 000 .. 19 999; the tree's nodes take LifeBase + preorder, 10 000 .. 13 103 for the 3104 nodes of the TimeTree
        /// of 2009): 19 000 for the outline outside the mass extinctions, 19 001 + k for the dip and rebound of the k-th
        /// extinction (oldest first, the order of <see cref="MassExtinctions"/>). Each extinction's lifeevent anchor carries
        /// its id, with those of the dips nested in its window, so highlighting the event lights its dip.
        /// </summary>
        public const int OutlineId = GraphIds.LifeBase + 9_000;

        public const int DipIdBase = OutlineId + 1;

        /// <summary>Radius over which the roots fade toward transparent black (no outer border).</summary>
        const float OuterFade = 2.0f;

        /// <summary>How much more space a lineage claims as it ages (1 + RootGrowth * age^2).</summary>
        const float RootGrowth = 40f;

        /// <summary>
        /// The outline along the tree's outer edge: faint life green, so the roots still fade out without a hard border; it
        /// glows where the director highlights an extinction's dip.
        /// </summary>
        const float OutlineAlpha = 0.3f, OutlineIntensity = 0.55f, OutlineWidthPx = 1.2f;

        const float GridStep = 0.001f;
        const string Us = "Hominidae";
        const double SapiensYearsAgo = 300_000;

        public override int Order => 10;
        public override IEnumerable<string> RequiredTexts => new[] { TreePath, TraitsPath, CladesPath, DomesticationPath };

        public PhyloTree Tree { get; private set; }
        public int LineagePathLength { get; private set; }

        /// <summary>Trophic level, role and common name of every leaf (life_traits.json), read-only after Prepare.</summary>
        public IReadOnlyDictionary<string, Trait> Traits { get; private set; }

        /// <summary>
        /// The mass extinctions the tree narrows at (life_clades.json events with a loss): B(u), immutable after Prepare and
        /// safe to read from other layers' worker threads.
        /// </summary>
        public MassExtinctions Extinctions { get; private set; } = MassExtinctions.None;

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
        float[] totals;             // [grid] = summed weight of every lineage (the tree's outer edge)

        /// <summary>One leaf of life_traits.json: trophic level t (1 producers .. 5), ecological role, common name.</summary>
        public sealed class Trait
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

            // a mass extinction: the share of species lost and in what measure, the years until the named measure had
            // recovered, and the fact ids (Docs/research/life) both numbers come from
            public double? loss;
            public string lossMeasure;
            public double? recoveryMyr;
            public string recoveryMeasure;
            public string[] facts;
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
            Traits = traits;
            CladeFile cladeFile = ParseOr(ctx.Text(CladesPath), new CladeFile());
            Dictionary<string, double> domesticated = DomesticatedFamilies(ctx.Text(DomesticationPath));
            List<string> problems = new List<string>();
            MassExtinctions extinctions = new MassExtinctions(ExtinctionsOf(cladeFile), problems);
            Extinctions = extinctions;

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
            totals = new float[gridCount];
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

                totals[g] = sum;
                total = Mathf.Max(total, sum);
            }

            // map summed weight to radius logarithmically: early, deep structure and the lineages nearest
            // us get room, the crowded outer bundles of recent families are compressed
            prefixTotal = Mathf.Max(total, 1f);
            logK = prefixTotal * 0.004f;
            logNorm = TotalWidth / Mathf.Log(1f + prefixTotal / logK);

            LineagePathLength = 0;
            for (int x = us; x >= 0; x = tree.Parent[x]) LineagePathLength++;


            // --- geometry: one polyline per lineage (about 200 000 points with the extinctions' cliffs and windows) ---
            lines = new LineMeshBuilder(220_000);
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

                // families people domesticated stay lit until that moment, then rise into the farm layer
                float uDomesticated = domesticated.TryGetValue(tree.Label[chain[chain.Count - 1]] ?? "", out double domYa)
                    ? DeepTime.Arc(domYa)
                    : -1f;

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

                    // the mass extinctions narrow the tree (B inside the log). It narrows without brightening: relevance
                    // stays with the undipped radius, and lines squeezed closer together are dimmed by the squeeze, which
                    // keeps the light per area as it was
                    float b = extinctions.B(u);
                    float weight = PrefixAt(l, u);
                    float rho0 = Radius(weight, 1f);
                    float rho = b < 1f ? Radius(weight, b) : rho0;
                    float squeeze = b < 1f ? Squeeze(weight, b) : 1f;
                    // relevance: the farther from our lineage, the fainter - fading out to transparent black
                    float relevance = Mathf.Exp(-Mathf.Pow(rho0 / OuterFade, 2.2f));
                    float handoff = GraphStyle.HandoffFade(u);
                    float fade = ours ? OurFade(u, uSapiens, uHandoff)
                        : uDomesticated > 0 ? DomesticatedFade(u, uDomesticated, handoff)
                        : handoff * handoff * handoff;
                    // attention on our direct ancestors: the rest of the biosphere is a quiet backdrop
                    float alpha = (ours ? 1f : 0.2f * relevance * squeeze) * fade;
                    // every lineage drops at the same arc, so its cliffs would stack into one bright spoke: the cliff
                    // stays unlit and the outline draws it
                    if (!ours && extinctions.OnCliff(u)) alpha = 0f;
                    // a rebound narrower than a few pixels would stack into a spoke the same way: only the outline lights it
                    else if (!ours) alpha *= extinctions.LitShare(u);
                    float intensity = ours ? 3f : 0.25f + 0.3f * relevance;
                    float widthWorld = 0.0006f * Mathf.Log(1 + leaves[node], 2) + (ours ? 0.004f : 0f);
                    pts.Add(new LinePoint(new Vector3(u, y, rho), Tint(alpha), ours ? 2.8f : 1f, widthWorld, intensity));
                    ids.Add(GraphIds.LifeNode(pre[node]));

                    if (u <= uEnd) break;
                    // both sides of every extinction's cliff, and fine steps through its recovery
                    float step = u > rampEnd ? GridStep : GridStep * 2.5f;
                    u = extinctions.Next(u, step, uEnd);
                }

                lines.AddPolyline(pts, ids, ours ? 1f : 0f);
            }

            AddOutline(pts, ids, y, uHandoff);

            // --- anchors and labels ---
            RegisterLeaves(ctx, tree, traits, pre);
            RegisterClades(ctx, tree, cladeFile, pre, size, onPath);
            RegisterEvents(ctx, cladeFile);

            // --- self-checks of the collapse: an error fails the smoke test ---
            extinctions.Verify(gridTop, gridBottom, problems);
            for (int k = 0; k < extinctions.Count; k++)
            {
                MassExtinctions.Extinction e = extinctions[k];
                float before = OuterRho(e.UTop, extinctions.B(e.UTop)), after = OuterRho(e.UEvent, extinctions.B(e.UEvent));
                if (!(after < before))
                {
                    problems.Add($"'{e.Id}': the tree's outer edge right after the extinction ({after:0.000}) is not narrower " +
                                 $"than just before it ({before:0.000})");
                }
            }

            if (GraphIds.LifeNode(n - 1) >= OutlineId) problems.Add($"the tree's {n} node ids reach the outline's ({OutlineId})");
            foreach (string problem in problems) Debug.LogError("[Why] mass extinctions: " + problem);

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

        /// <summary>Radial position of a lineage at an arc (interpolated on the grid), narrowed by the mass extinctions.</summary>
        public float RhoAt(int lineage, float u) => Radius(PrefixAt(lineage, u), Extinctions.B(u));

        /// <summary>
        /// Radius of a summed weight with the living weight scaled by b: B(u) applies inside the log, so the radius stays
        /// the log of the summed weight standing inside (b = 1: the undipped tree). The grid and the normalization keep the
        /// undipped weights: on 0.001-arc rows the dips (0.0004 to 0.013 arc) would alias.
        /// </summary>
        float Radius(float weight, float b) => logNorm * Mathf.Log(1f + b * weight / logK);

        /// <summary>
        /// How much closer lines stand where b narrows the tree: d radius / d undipped radius at this weight, b near the
        /// inner track (where the log is almost linear) rising toward 1 at large weights.
        /// </summary>
        float Squeeze(float weight, float b) => b * (logK + weight) / (logK + b * weight);

        /// <summary>The undipped summed weight inside a lineage at an arc (interpolated on the grid).</summary>
        float PrefixAt(int lineage, float u)
        {
            float gf = Mathf.Clamp((gridTop - u) / GridStep, 0, gridCount - 1);
            int g0 = Mathf.Min((int)gf, gridCount - 2);
            float t = gf - g0;
            float p0 = prefix[g0 * lineageCount + lineage];
            float p1 = prefix[(g0 + 1) * lineageCount + lineage];
            return p0 + (p1 - p0) * t;
        }

        /// <summary>The tree's summed living weight at an arc, every lineage alive (interpolated on the grid).</summary>
        float TotalAt(float u)
        {
            float gf = Mathf.Clamp((gridTop - u) / GridStep, 0, gridCount - 1);
            int g0 = Mathf.Min((int)gf, gridCount - 2);
            float t = gf - g0;
            return totals[g0] + (totals[g0 + 1] - totals[g0]) * t;
        }

        /// <summary>Radius of the tree's outer edge at an arc with the living weight scaled by b.</summary>
        float OuterRho(float u, float b) => Radius(TotalAt(u), b);

        /// <summary>Radial position of a node's lineage at an arc (narrowed where a mass extinction's dip holds it).</summary>
        public float NodeRho(int node, float u) => RhoAt(nodeLineage[node], Mathf.Clamp(u, gridBottom, gridTop));

        /// <summary>Where the node's lineage would stand without the extinctions: fades by distance from our lineage use it.</summary>
        float UndippedNodeRho(int node, float u) => Radius(PrefixAt(nodeLineage[node], Mathf.Clamp(u, gridBottom, gridTop)), 1f);

        /// <summary>The events of life_clades.json that carry a loss: the mass extinctions the tree collapses at.</summary>
        static IEnumerable<MassExtinctions.Extinction> ExtinctionsOf(CladeFile file)
        {
            foreach (EventDto e in file.events)
            {
                if (e?.loss == null || string.IsNullOrEmpty(e.id)) continue;
                yield return new MassExtinctions.Extinction(e.id, e.mya, e.loss.Value, e.recoveryMyr ?? 0);
            }
        }

        /// <summary>
        /// One thin line along the tree's outer edge over its whole span, rho = logNorm ln(1 + B(u) total(u) / logK): the
        /// collapses read as spikes in a diversity curve even where a window is a hairline on the log clock. Each
        /// extinction's dip carries its own highlight id (<see cref="DipIdBase"/>), the rest <see cref="OutlineId"/>; the
        /// line dissolves before the human branch like the rest of the biosphere.
        /// </summary>
        void AddOutline(List<LinePoint> pts, List<float> ids, float y, float uEnd)
        {
            MassExtinctions extinctions = Extinctions;
            pts.Clear();
            ids.Clear();
            float u = gridTop;
            while (true)
            {
                float handoff = GraphStyle.HandoffFade(u);
                int dip = extinctions.DipAt(u);
                pts.Add(new LinePoint(new Vector3(u, y, OuterRho(u, extinctions.B(u))), Tint(OutlineAlpha * handoff * handoff * handoff),
                    OutlineWidthPx, 0f, OutlineIntensity));
                ids.Add(dip >= 0 ? DipIdBase + dip : OutlineId);
                if (u <= uEnd) break;
                u = extinctions.Next(u, GridStep, uEnd);
            }

            lines.AddPolyline(pts, ids, 0f);
        }

        /// <summary>Fully lit until domestication, then gone within a short arc (the thread rises into the farm layer).</summary>
        static float DomesticatedFade(float u, float uDomesticated, float handoff)
        {
            float t = Mathf.Clamp01((u - (uDomesticated - 0.004f)) / 0.004f);
            return Mathf.Max(handoff * handoff * handoff, 0.9f * t * t * (3 - 2 * t));
        }

        /// <summary>Wild families that were domesticated (leaf label -> earliest domestication, years ago).</summary>
        static Dictionary<string, double> DomesticatedFamilies(string json)
        {
            Dictionary<string, double> map = new Dictionary<string, double>();
            if (string.IsNullOrEmpty(json)) return map;
            try
            {
                Newtonsoft.Json.Linq.JToken list = Newtonsoft.Json.Linq.JObject.Parse(json)["domestications"];
                if (list == null) return map;
                foreach (Newtonsoft.Json.Linq.JToken d in list)
                {
                    string family = (string)d["wildFamily"];
                    double ya = (double?)d["yearsAgo"] ?? 0;
                    if (string.IsNullOrEmpty(family) || ya <= 0) continue;
                    map[family] = map.TryGetValue(family, out double prev) ? Math.Max(prev, ya) : ya;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Why] could not read domestications: {e.Message}");
            }

            return map;
        }

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
                    Color = isUs ? GraphStyle.Text : Faded(GraphStyle.TextDim, UndippedNodeRho(v, u0 - 0.004f), 0.2f),
                    PixelOffset = new Vector2(4, 6),
                    AnchorKey = a.Key,
                    HandoffFade = !isUs,
                    Ids = a.Ids
                });
            }
        }

        void RegisterClades(GraphContext ctx, PhyloTree tree, CladeFile file, int[] pre, int[] size, bool[] onPath)
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
                double ya = tree.IsLeaf(node) ? stemYa : crownYa;
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

                bool ancestral = onPath[node];
                ctx.Labels.Add(new LabelSpec
                {
                    Text = anchor.Label,
                    Data = anchor.Data,
                    // our ancestors (clades that contain us) lead; the rest of the biosphere recedes
                    Priority = (c.tier == 1 ? 40 : c.tier == 2 ? 20 : 8) + (ancestral ? 30 : 0),
                    SizePx = c.tier == 1 ? 15 : c.tier == 2 ? 13 : 11.5f,
                    Color = ancestral ? GraphStyle.Text : Faded(GraphStyle.TextDim, UndippedNodeRho(node, u), 0.3f),
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

                // a mass extinction carries its dip on the outline (and the dips nested in its window): highlighting the
                // event lights them, and its card states the loss and the recovery in the data's own measures
                int k = Extinctions.IndexOf(e.id);
                Anchor a = new Anchor
                {
                    Key = "lifeevent:" + e.id,
                    Label = e.name ?? e.id,
                    Blurb = k >= 0 ? JoinBlurb(e.blurb, LossText(e)) : e.blurb,
                    Level = GraphLevel.Life,
                    YearsAgo = e.mya * 1e6,
                    EndYearsAgo = e.mya * 1e6,
                    Y = GraphStyle.LifeY,
                    Rho = -0.06f,
                    Ids = k >= 0 ? new IdRange(DipIdBase + k, DipIdBase + Extinctions.LastNested(k)) : IdRange.Empty,
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
                    AnchorKey = a.Key,
                    // the label brightens with its own dip: highlighting the end-Permian (whose window holds the
                    // end-Triassic's dip) lights both labels, highlighting the end-Triassic only its own
                    Ids = k >= 0 ? IdRange.Single(DipIdBase + k) : IdRange.Empty
                });
            }
        }

        /// <summary>
        /// What a mass extinction's dip stands for, in the data's own measures, e.g. "Lost: 81% of marine species. Recovery:
        /// global marine diversity took 75 million years, to the Middle Jurassic." The depth of the dip is a share of
        /// species, never measured biomass, and the card says so.
        /// </summary>
        static string LossText(EventDto e)
        {
            string percent = Math.Round((e.loss ?? 0) * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
            string text = $"Lost: {percent}% of {(string.IsNullOrEmpty(e.lossMeasure) ? "species" : e.lossMeasure)}.";
            if (!string.IsNullOrEmpty(e.recoveryMeasure)) text += $" Recovery: {e.recoveryMeasure}.";
            return text + " The tree's dip stands for this share of species, not for measured biomass.";
        }

        static string JoinBlurb(string blurb, string more) => string.IsNullOrEmpty(blurb) ? more : blurb + "\n\n" + more;

        public override void Upload(GraphContext ctx)
        {
            if (lines == null) return;
            lineMat = GraphMaterials.Line(GraphStyle.Life, 1f, GraphMaterials.QueueLife + 1, true, 0f, 1f);
            AddMesh("TreeOfLife", lines.ToMesh("TreeOfLife"), lineMat);

            Highlighter.SetPersistent("life", new[]
            {
                (new IdRange(GraphIds.LifeNode(0), GraphIds.LifeNode(LineagePathLength - 1)), 2.2f)
            });

            lines = null;
        }

        static Color32 Tint(float alpha) => new Color32(255, 255, 255, (byte)Mathf.Clamp(alpha * 255f, 0, 255));

        /// <summary>Label color that fades with distance from our lineage, like the roots themselves.</summary>
        static Color Faded(Color c, float rho, float floor)
        {
            c.a *= floor + (1f - floor) * Mathf.Exp(-Mathf.Pow(Mathf.Max(0, rho) / OuterFade, 2.2f));
            return c;
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

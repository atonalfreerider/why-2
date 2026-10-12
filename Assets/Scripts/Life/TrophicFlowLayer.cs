using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Why.Life
{
    /// <summary>
    /// GREEN overlay on the tree of life: calories flowing up the food chain. The tree orders its branches by
    /// food chain (top predators inside, our own lineage innermost); this layer draws the relationship that
    /// ordering stands for - every level of the chain lives on the calories of the level beneath it, and only
    /// roughly 10% of them pass each step up (Lindeman's ecological efficiency; the rest is spent as heat and
    /// waste).
    ///
    /// At a few moments of the history of life (<see cref="Slices"/>) a chain of representative lineages is
    /// picked - producers, herbivores, predators, top predators - and an arc of energy is drawn from each level
    /// to the next, between the actual branches of the tree at that time. Arcs rise above the life plane as
    /// bridges, shrink about tenfold in width and dim with every step, and carry the causal flow pulse from
    /// prey to predator, so the pulses travel inward, toward the inner track and our lineage.
    ///
    /// Trophic levels come from life_traits.json: a family's level is its role and trophic level t (see
    /// <see cref="LevelOf"/>), and a lineage at a moment takes the dominant level of the families descending
    /// from it. The representatives are hand-picked textbook food chains of each age; each is checked against
    /// the data when the layer builds (dominant level, levels rising along the chain) and skipped if it fails.
    ///
    /// Shown in the life views (life, complex life, mammals, or the lens unrolled over one of the slices) and
    /// faded out elsewhere, unless the director points at it.
    /// </summary>
    public sealed class TrophicFlowLayer : GraphLayer
    {
        /// <summary>Highlight ids of the calorie arcs: 16 per slice from here (unused by the other layers of the graph).</summary>
        public const int IdBase = 50_000;

        const int IdsPerSlice = 16;

        // trophic levels (Lindeman): producers, primary consumers, secondary consumers, top predators
        const int Producers = 0, Herbivores = 1, Predators = 2, TopPredators = 3, LevelCount = 4;

        static readonly string[] LevelNames = { "producers", "herbivores & filter feeders", "predators", "top predators" };
        static readonly string[] LevelShort = { "producers", "herbivores", "predators", "top predators" };

        /// <summary>Consumers below this trophic level (carnivorous plants, mixotrophs) are not a clean level.</summary>
        const float ConsumerMinT = 2f;

        /// <summary>Consumers below this trophic level are primary consumers (herbivores, filter feeders).</summary>
        const float HerbivoreMaxT = 2.5f;

        /// <summary>
        /// Consumers at or above this trophic level are top predators (t ~ 4, with a margin so insect eaters,
        /// which reach 3.5 in the data, stay secondary consumers).
        /// </summary>
        const float PredatorMaxT = 3.8f;

        /// <summary>Share of a lineage's classified descendant families that must belong to its level.</summary>
        const float MinPurity = 0.5f;

        /// <summary>Share of the energy passed from one trophic level to the next (roughly 10%).</summary>
        const float Efficiency = 0.1f;

        // geometry: arcs are bridges above the life plane, higher for longer spans (but below the human layer)
        const int ArcSamples = 48;
        const float ArcLift = 0.08f, ArcLiftPerRho = 0.22f, ArcLiftMax = 0.33f;

        // width of the first arc (the producers' energy); every further step is about ten times thinner, with a
        // hairline floor so the last step stays visible from afar (close up the world width dominates)
        const float ArcWidthWorld = 0.04f, ArcWidthPx = 7f, HairlinePx = 1f;
        const float ArcAlpha = 0.85f;

        // flow pulses along the arcs: pulses per data unit, speed, and at least this many on a short arc
        const float FlowFreq = 4f, FlowSpeed = 0.9f, MinPulses = 2f;

        const float EmphasisSpeed = 1.6f;
        const float LabelsBelow = 0.35f;

        /// <summary>The views the calorie flow belongs to (besides a lens unrolled over one of the slices).</summary>
        static readonly string[] Views = { "life", "complex_life", "mammals" };

        /// <summary>One link of a food chain: a leaf family whose lineage at the slice stands for its level.</summary>
        readonly struct Rep
        {
            public readonly string Leaf;
            public readonly string Name;

            public Rep(string leaf, string name)
            {
                Leaf = leaf;
                Name = name;
            }
        }

        /// <summary>A moment of the history of life with a food chain, lowest level first.</summary>
        sealed class Slice
        {
            public string Id;
            public string Era;
            public string Setting;
            public double YearsAgo;
            public Rep[] Chain;
        }

        /// <summary>
        /// The moments, all well before life dissolves ahead of the human branch (5 million years ago): the
        /// Devonian seas (the first with top predators in the tree of today's families), the age of dinosaurs,
        /// and the grasslands of the Miocene, on which our own lineage is already the innermost branch.
        /// </summary>
        static readonly Slice[] Slices =
        {
            new Slice
            {
                Id = "devonian", Era = "Devonian", Setting = "In the Devonian seas", YearsAgo = 380e6,
                Chain = new[]
                {
                    new Rep("Chlorophyta", "green algae"), new Rep("Lepadidae", "crustaceans"),
                    new Rep("Octopodidae-1", "cephalopods"), new Rep("Lamnidae", "sharks")
                }
            },
            new Slice
            {
                Id = "jurassic", Era = "Jurassic", Setting = "In the age of dinosaurs", YearsAgo = 150e6,
                Chain = new[]
                {
                    new Rep("Cycadaceae", "cycads"), new Rep("Chrysomelidae-1", "leaf beetles"),
                    new Rep("Ranidae", "amphibians"), new Rep("Crocodylidae", "crocodilians")
                }
            },
            new Slice
            {
                Id = "miocene", Era = "Miocene", Setting = "On the Miocene grasslands", YearsAgo = 12e6,
                Chain = new[]
                {
                    new Rep("Poaceae", "grasses"), new Rep("Lepidoptera", "moths and butterflies"),
                    new Rep("Hirundinidae", "swallows"), new Rep("Falconidae", "falcons")
                }
            },
        };

        public override int Order => 31; // the first build tier after LifeLayer's (everything below 30 builds at once)

        LineMeshBuilder lines;
        Material lineMat;
        MeshRenderer lineRenderer;
        readonly List<LabelSpec> labels = new List<LabelSpec>();
        float emphasis = -1;

        static IdRange AllIds => new IdRange(IdBase, IdBase + Slices.Length * IdsPerSlice - 1);

        static IdRange SliceIds(int s) => new IdRange(IdBase + s * IdsPerSlice, IdBase + (s + 1) * IdsPerSlice - 1);

        /// <summary>A representative resolved on the tree: its lineage at the slice and the level it stands for.</summary>
        struct Endpoint
        {
            public string Name;
            public int Level;
            public float Rho;
        }

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            LifeLayer life = ctx.Shared<LifeLayer>("life.layer");
            PhyloTree tree = life?.Tree;
            if (tree == null || life.Traits == null) return;

            // --- classified descendant families per node and level (children have larger indices) ---
            int n = tree.Count;
            int[] counts = new int[n * LevelCount];
            for (int i = n - 1; i >= 0; i--)
            {
                if (tree.IsLeaf(i))
                {
                    life.Traits.TryGetValue(tree.Label[i] ?? "", out LifeLayer.Trait tr);
                    int level = LevelOf(tr);
                    if (level >= 0) counts[i * LevelCount + level] = 1;
                    continue;
                }

                foreach (int c in tree.Children[i])
                {
                    for (int k = 0; k < LevelCount; k++) counts[i * LevelCount + k] += counts[c * LevelCount + k];
                }
            }

            // --- one food chain per slice ---
            lines = new LineMeshBuilder(Slices.Length * LevelCount * (ArcSamples + 2));
            List<Endpoint> chain = new List<Endpoint>(LevelCount);
            List<LinePoint> pts = new List<LinePoint>(ArcSamples + 1);
            for (int s = 0; s < Slices.Length; s++)
            {
                Slice slice = Slices[s];
                float u = DeepTime.Arc(slice.YearsAgo);
                if (GraphStyle.HandoffFade(u) < 0.99f)
                {
                    Debug.LogWarning($"[Why] trophic slice '{slice.Id}' lies where life dissolves; skipped");
                    continue;
                }

                ResolveChain(tree, life, counts, slice, u, chain);
                if (chain.Count < 2) continue;

                IdRange ids = SliceIds(s);
                float lowest = float.MaxValue, highest = float.MinValue, topLift = 0;
                foreach (Endpoint e in chain)
                {
                    lowest = Mathf.Min(lowest, e.Rho);
                    highest = Mathf.Max(highest, e.Rho);
                }

                Anchor anchor = new Anchor
                {
                    Key = "trophic:" + slice.Id,
                    Label = $"Calories flow upward ({slice.Era})",
                    Blurb = Blurb(slice, chain),
                    Level = GraphLevel.Life,
                    YearsAgo = slice.YearsAgo,
                    EndYearsAgo = slice.YearsAgo,
                    Y = GraphStyle.LifeY,
                    Rho = 0.5f * (lowest + highest),
                    Ids = ids,
                    Tier = 2
                };
                Anchors.Register(anchor);

                // arcs: from each level to the next, the energy that reaches the upper level
                for (int k = 0; k + 1 < chain.Count; k++)
                {
                    Endpoint a = chain[k], b = chain[k + 1];
                    float energy = Mathf.Pow(Efficiency, b.Level - chain[0].Level - 1); // 1 for the first step
                    float lift = Mathf.Min(ArcLiftMax, ArcLift + ArcLiftPerRho * Mathf.Abs(a.Rho - b.Rho));
                    topLift = Mathf.Max(topLift, lift);
                    float length = AddArc(pts, u, a.Rho, b.Rho, lift, energy);
                    // at least a couple of pulses on every arc, however short
                    float phasePerUnit = Mathf.Max(1f, MinPulses / (FlowFreq * Mathf.Max(length, 1e-3f)));
                    lines.AddFlowPath(pts, ids.Min + k, phasePerUnit, 0.37f * s + 0.23f * k);

                    labels.Add(Label(ctx, StepText(chain, k), new Vector3(u, GraphStyle.LifeY + lift, 0.5f * (a.Rho + b.Rho)),
                        k == 0 ? 15 : 14, 11.5f, GraphStyle.TextDim, new Vector2(0, 8), TMPro.TextAlignmentOptions.Center,
                        anchor));
                }

                // the representatives: a bright dot on their branch, named with their level
                for (int k = 0; k < chain.Count; k++)
                {
                    Endpoint e = chain[k];
                    float energy = Mathf.Pow(Efficiency, e.Level - chain[0].Level); // the energy held at this level
                    Vector3 at = new Vector3(u, GraphStyle.LifeY, e.Rho);
                    lines.AddSegment(at, at + new Vector3(0, 0.01f, 0), Tint(1f), 5f, 0f, ids.Max,
                        1.2f + 1.8f * Mathf.Sqrt(energy));
                    labels.Add(Label(ctx, $"{LevelNames[e.Level]}: {e.Name}", at, 18, 12, GraphStyle.Text,
                        new Vector2(6, -12), TMPro.TextAlignmentOptions.Left, anchor));
                }

                labels.Add(Label(ctx, anchor.Label, new Vector3(u, GraphStyle.LifeY + topLift + 0.05f, anchor.Rho), 26, 13,
                    GraphStyle.Text, new Vector2(0, 10), TMPro.TextAlignmentOptions.Center, anchor));
            }

            Debug.Log($"[Why] TrophicFlowLayer.Prepare {sw.ElapsedMilliseconds} ms ({lines.PolylineCount} lines)");
        }

        /// <summary>
        /// Trophic level of a family (-1 = not on the grazing chain). Producers by role; consumers by their
        /// trophic level t: below 2.5 herbivores and filter feeders, below 3.8 predators, above top predators.
        /// Decomposers, detritivores, parasites and symbionts feed beside the chain and are left out.
        /// </summary>
        static int LevelOf(LifeLayer.Trait tr)
        {
            if (tr == null || string.IsNullOrEmpty(tr.Role)) return -1;
            switch (tr.Role)
            {
                case "producer":
                case "chemotroph":
                    return Producers;
                case "herbivore":
                case "filter_feeder":
                case "omnivore":
                case "insectivore":
                case "carnivore":
                case "apex":
                    if (tr.T < ConsumerMinT) return -1;
                    return tr.T < HerbivoreMaxT ? Herbivores : tr.T < PredatorMaxT ? Predators : TopPredators;
                default:
                    return -1;
            }
        }

        /// <summary>
        /// Resolves a slice's representatives on the tree: the lineage each family descends from at that moment,
        /// the level that lineage stands for (the dominant level of its classified descendants) and its radius.
        /// Representatives that fail the data (unknown, mixed, or not a step up the chain) are skipped.
        /// </summary>
        static void ResolveChain(PhyloTree tree, LifeLayer life, int[] counts, Slice slice, float u, List<Endpoint> chain)
        {
            chain.Clear();
            double mya = slice.YearsAgo / 1e6;
            foreach (Rep rep in slice.Chain)
            {
                int leaf = tree.FindLeaf(rep.Leaf);
                if (leaf < 0)
                {
                    Debug.LogWarning($"[Why] trophic slice '{slice.Id}': family '{rep.Leaf}' not in the tree");
                    continue;
                }

                // climb to the node whose branch spans the moment (it split later than the moment, its parent earlier)
                int node = leaf;
                while (tree.Parent[node] >= 0 && tree.AgeMya[tree.Parent[node]] <= mya) node = tree.Parent[node];
                if (tree.Parent[node] < 0 && tree.RootAgeMya <= mya) continue;

                int best = -1, total = 0;
                for (int k = 0; k < LevelCount; k++)
                {
                    int c = counts[node * LevelCount + k];
                    total += c;
                    if (best < 0 || c > counts[node * LevelCount + best]) best = k;
                }

                bool rises = chain.Count == 0 || best > chain[chain.Count - 1].Level;
                if (total == 0 || counts[node * LevelCount + best] < MinPurity * total || !rises)
                {
                    Debug.LogWarning($"[Why] trophic slice '{slice.Id}': '{rep.Leaf}' does not stand for the next level; skipped");
                    continue;
                }

                chain.Add(new Endpoint { Name = rep.Name, Level = best, Rho = life.NodeRho(node, u) });
            }
        }

        /// <summary>
        /// Fills pts with one arc at arc u from radius rhoA up over the life plane to rhoB; brighter and wider
        /// for more energy. Returns its length in data units.
        /// </summary>
        static float AddArc(List<LinePoint> pts, float u, float rhoA, float rhoB, float lift, float energy)
        {
            pts.Clear();
            float widthPx = HairlinePx + ArcWidthPx * energy;
            float widthWorld = ArcWidthWorld * energy;
            float intensity = 1.2f + 1.8f * Mathf.Sqrt(energy);
            float length = 0;
            Vector3 prev = Vector3.zero;
            for (int i = 0; i <= ArcSamples; i++)
            {
                float t = i / (float)ArcSamples;
                float bridge = Mathf.Sin(Mathf.PI * t);
                Vector3 p = new Vector3(u, GraphStyle.LifeY + lift * bridge, Mathf.Lerp(rhoA, rhoB, t));
                if (i > 0) length += Vector3.Distance(prev, p);
                prev = p;
                // the arc emerges from the branches and is brightest in flight
                pts.Add(new LinePoint(p, Tint(ArcAlpha * (0.55f + 0.45f * bridge)), widthPx, widthWorld, intensity));
            }

            return length;
        }

        /// <summary>"producers → herbivores: ~10% of the energy", then "~10% again, ~1% of the producers' energy".</summary>
        static string StepText(List<Endpoint> chain, int k)
        {
            Endpoint a = chain[k], b = chain[k + 1];
            string step = Percent(Mathf.Pow(Efficiency, b.Level - a.Level));
            string head = $"{LevelShort[a.Level]} → {LevelShort[b.Level]}: ~{step}";
            if (k == 0) return head + " of the energy";
            string total = Percent(Mathf.Pow(Efficiency, b.Level - chain[0].Level));
            return $"{head} again, ~{total} of the {LevelShort[chain[0].Level]}' energy";
        }

        static string Blurb(Slice slice, List<Endpoint> chain)
        {
            string lowest = chain[0].Name, top = chain[chain.Count - 1].Name;
            string total = Percent(Mathf.Pow(Efficiency, chain[chain.Count - 1].Level - chain[0].Level));
            string middle = "";
            for (int k = 1; k < chain.Count - 1; k++) middle += (k == 1 ? "" : ", then ") + chain[k].Name;
            string through = middle.Length > 0 ? $" through {middle}" : "";
            return $"{slice.Setting}, the calories {lowest} captured from sunlight fed the {top}{through}. Each step up " +
                   $"the food chain passes on only roughly 10% of the energy (the rest is spent as heat and waste), so " +
                   $"about {total} reaches the top: predators are few, and the tree orders them toward its inner track, " +
                   "where our own lineage runs.";
        }

        static string Percent(float share) => (share * 100f).ToString("0.###", CultureInfo.InvariantCulture) + "%";

        static LabelSpec Label(GraphContext ctx, string text, Vector3 data, float priority, float sizePx, Color color,
            Vector2 offset, TMPro.TextAlignmentOptions align, Anchor anchor)
        {
            LabelSpec spec = new LabelSpec
            {
                Text = text,
                Data = data,
                Priority = priority,
                SizePx = sizePx,
                Color = color,
                PixelOffset = offset,
                Align = align,
                AnchorKey = anchor.Key,
                Ids = anchor.Ids,
                HandoffFade = true,
                Hidden = true // the graph opens on the overview; Tick shows them in the life views
            };
            ctx.Labels.Add(spec);
            return spec;
        }

        public override void Upload(GraphContext ctx)
        {
            if (lines == null) return;
            if (lines.PolylineCount > 0)
            {
                lineMat = GraphMaterials.Line(GraphStyle.Life, 1f, GraphMaterials.QueueLife + 2, true, 0f, 1f);
                lineMat.SetFloat("_FlowFreq", FlowFreq);
                lineMat.SetFloat("_FlowSpeed", FlowSpeed);
                GraphMaterials.FadeBeforeHumanBranch(lineMat);
                GraphMaterials.SetAlpha(lineMat, 0f);
                lineRenderer = AddMesh("TrophicFlow", lines.ToMesh("TrophicFlow"), lineMat);
                lineRenderer.enabled = false;
            }

            lines = null;
        }

        /// <summary>Emphasis: full in the life views (or while the director points at it), faded out elsewhere.</summary>
        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (lineMat == null) return;
            GraphRoot root = GraphRoot.Instance;
            float target = root != null && InView(root.CurrentPreset) ? 1f : 0f;
            if (Highlighter.IsHighlighted(AllIds)) target = 1f;
            float next = emphasis < 0 ? target : Mathf.MoveTowards(emphasis, target, Time.unscaledDeltaTime * EmphasisSpeed);
            if (Mathf.Abs(next - emphasis) < 1e-4f) return;

            bool wasShown = emphasis >= LabelsBelow;
            emphasis = next;
            GraphMaterials.SetAlpha(lineMat, emphasis);
            lineRenderer.enabled = emphasis > 1e-3f;

            bool shown = emphasis >= LabelsBelow;
            if (shown == wasShown) return;
            foreach (LabelSpec l in labels) l.Hidden = !shown;
            root?.Labels.MarkDirty();
        }

        /// <summary>The life views, or the lens unrolled over a stretch of time that holds one of the slices.</summary>
        static bool InView(ViewPreset preset)
        {
            if (preset == null) return false;
            if (Array.IndexOf(Views, preset.Id) >= 0) return true;
            if (preset.Id != "lens" || preset.Polar) return false;
            foreach (Slice s in Slices)
            {
                if (s.YearsAgo <= preset.YaOld && s.YearsAgo >= preset.YaNew) return true;
            }

            return false;
        }

        static Color32 Tint(float alpha) => new Color32(255, 255, 255, (byte)Mathf.Clamp(alpha * 255f, 0, 255));
    }
}

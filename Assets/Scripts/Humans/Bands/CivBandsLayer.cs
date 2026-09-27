using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Why.Humans.Bands
{
    /// <summary>
    /// BLUE layer, civilization streams. Every civilization is a stream at <see cref="GraphStyle.HumansY"/>
    /// whose width is its relative power (Histomap, Sparks 1931, extended to the present), West inside to
    /// East outside; the whole layer widens with (log) world population. Streams emerge from their parents
    /// and dissolve into their neighbors; thin lineage links carry the other causes forward.
    ///
    /// Homo sapiens rises out of the life layer's inner track 300,000 years ago (the level jump) as one
    /// prehistoric stream that hands over to the first civilizations. Our path follows the inner edge of
    /// the layer from that jump to the present moment. Wars scar the streams that fought them.
    ///
    /// Geometry comes from the shared <see cref="HumanWorld"/> model (built at Order 20), so the population
    /// curves and lifelines of the other human layers fit exactly inside these bands. Three meshes: band
    /// fills (alpha blended), lines (additive, bloom when highlighted) and the green half of the jump.
    /// </summary>
    public sealed class CivBandsLayer : GraphLayer
    {
        /// <summary>Highlight id of our path (the connector and the inner edge of the human layer).</summary>
        public const int LineageId = 999_999;

        /// <summary>
        /// First highlight id of the war marks (one id per war, in demography.json order). Lies in the free
        /// range between the tree of life and the civilization blocks (see Docs/ARCHITECTURE.md).
        /// </summary>
        public const int WarIdBase = 990_000;

        /// <summary>Anchor key of our path through the human layer.</summary>
        public const string LineageAnchor = "civ:_lineage";

        // band fill level of detail: the fill steps back when lifelines drawn inside the bands matter
        const float CivilizationsFillAlpha = 0.6f;
        const float ModernFillAlpha = 0.45f;
        const float SmvFillAlpha = 0.3f;
        const float CloseFillAlpha = 0.4f;
        const float CloseDistance = 0.8f;
        const float FarDistance = 3f;
        const float FillAlphaSpeed = 1.5f;

        // relevance: content fades with distance from the inner track
        const float FillRhoFade = 3.5f;
        const float LineRhoFade = 5f;
        const float FillEdgeSoft = 0.3f;

        /// <summary>Highlight id of war <paramref name="warIndex"/> (index into HumanWorld.Wars).</summary>
        public static int WarId(int warIndex) => WarIdBase + warIndex;

        public override int Order => 30;

        BandGeometry geometry;
        Material fillMat, lineMat, lifeMat;
        float fillAlpha = 1f, appliedAlpha = -1f;

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            HumanWorld world = ctx.Shared<HumanWorld>(HumanWorld.SharedKey);
            if (world == null || world.Humanity == null)
            {
                Debug.LogWarning("[Why] CivBandsLayer: no shared HumanWorld; the civilization streams are skipped");
                return;
            }

            BandGeometry g = new BandGeometry(world);
            g.Build();
            BandAnnotations.Register(ctx, world, g);
            geometry = g;
            Debug.Log($"[Why] CivBandsLayer.Prepare {sw.ElapsedMilliseconds} ms ({g.Streams.Count} streams, " +
                      $"{g.Wars.Count} wars, {g.Fill.VertexCount} fill vertices, {g.Lines.VertexCount / 2} line points)");
        }

        public override void Upload(GraphContext ctx)
        {
            if (geometry == null) return;

            fillMat = GraphMaterials.Surface(GraphStyle.Humans, 1f, GraphMaterials.QueueHumans, false, FillRhoFade);
            fillMat.SetFloat("_EdgeSoft", FillEdgeSoft);
            AddMesh("CivBands", geometry.Fill.ToMesh("CivBands"), fillMat);

            lineMat = GraphMaterials.Line(GraphStyle.Humans, 1f, GraphMaterials.QueueHumans + 1, true, LineRhoFade, 1f);
            AddMesh("CivEdges", geometry.Lines.ToMesh("CivEdges"), lineMat);

            // the lower half of the level jump still belongs to the life lineage it rises out of
            lifeMat = GraphMaterials.Line(GraphStyle.Life, 1f, GraphMaterials.QueueHumans + 1, true, 0f, 1f);
            AddMesh("HumansJump", geometry.LifeLines.ToMesh("HumansJump"), lifeMat);

            geometry = null;
        }

        /// <summary>
        /// Level of detail: in the civilizations, modern and smv presets, and whenever the camera is close,
        /// the band fills fade back so the population curves and lifelines inside them stay readable. Edges,
        /// links and our path keep their strength.
        /// </summary>
        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (fillMat == null || rig == null) return;
            fillAlpha = Mathf.MoveTowards(fillAlpha, TargetFillAlpha(rig), Time.unscaledDeltaTime * FillAlphaSpeed);
            if (Mathf.Abs(fillAlpha - appliedAlpha) < 0.002f) return;
            GraphMaterials.SetAlpha(fillMat, fillAlpha);
            appliedAlpha = fillAlpha;
        }

        static float TargetFillAlpha(CameraRig rig)
        {
            GraphRoot root = GraphRoot.Instance;
            string preset = root != null && root.CurrentPreset != null ? root.CurrentPreset.Id : null;
            float byPreset;
            switch (preset)
            {
                case "civilizations": byPreset = CivilizationsFillAlpha; break;
                case "modern": byPreset = ModernFillAlpha; break;
                case "smv": byPreset = SmvFillAlpha; break;
                default: byPreset = 1f; break;
            }

            float far = Mathf.InverseLerp(CloseDistance, FarDistance, rig.Pose.Distance);
            float byDistance = Mathf.Lerp(CloseFillAlpha, 1f, far * far * (3f - 2f * far));
            return Mathf.Min(byPreset, byDistance);
        }
    }
}

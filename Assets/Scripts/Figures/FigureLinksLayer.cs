using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Why.Figures
{
    /// <summary>
    /// Influence threads between famous figures: an arc rises from the influencer's lifeline at the moment of
    /// influence and lands on the influenced person's lifeline, with a causal pulse travelling forward in
    /// time. Built after the lifeline layers (which register the "figure:" anchors).
    /// </summary>
    public sealed class FigureLinksLayer : GraphLayer
    {
        public const string SharedKey = "figures.list";

        const int Samples = 28;

        public override int Order => 50;
        public override IEnumerable<string> RequiredTexts => new[] { FigureData.Path };

        LineMeshBuilder lines;
        Material material;

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            List<Figure> figures = FigureData.Parse(ctx.Text(FigureData.Path));
            ctx.Share(SharedKey, figures);

            lines = new LineMeshBuilder(4096);
            List<LinePoint> pts = new List<LinePoint>(Samples + 1);
            List<float> ids = new List<float>(Samples + 1);
            int links = 0;
            foreach (Figure to in figures)
            {
                if (!Anchors.TryGet(to.AnchorKey, out Anchor b)) continue;
                foreach (Figure from in to.Influencers)
                {
                    if (!Anchors.TryGet(from.AnchorKey, out Anchor a)) continue;

                    // the moment of influence: the influenced person's youth, while the influencer is alive
                    double fromEnd = from.End(ctx.NowYear), toEnd = to.End(ctx.NowYear);
                    double tA = Math.Min(fromEnd, Math.Max(from.born + 15, to.born + 18));
                    double tB = Math.Min(toEnd, Math.Max(to.born + 18, tA));
                    float uA = DeepTime.Arc(Math.Max(ctx.NowYear - tA, 1e-6));
                    float uB = DeepTime.Arc(Math.Max(ctx.NowYear - tB, 1e-6));
                    float gap = (float)Math.Max(1, tB - from.born);
                    float bulge = 0.05f + 0.025f * Mathf.Log(1 + gap / 10f);

                    pts.Clear();
                    ids.Clear();
                    for (int i = 0; i <= Samples; i++)
                    {
                        float t = i / (float)Samples;
                        float s = t * t * (3 - 2 * t);
                        float u = Mathf.Lerp(uA, uB, s);
                        float rho = Mathf.Lerp(a.Rho, b.Rho, s);
                        float y = Mathf.Lerp(a.Y, b.Y, s) + bulge * Mathf.Sin(Mathf.PI * t);
                        byte alpha = (byte)(255 * (0.35f + 0.4f * Mathf.Sin(Mathf.PI * t)));
                        pts.Add(new LinePoint(new Vector3(u, y, rho), new Color32(255, 255, 255, alpha), 1.2f, 0, 1.4f));
                        ids.Add(t < 0.5f ? a.Ids.Min : b.Ids.Min);
                    }

                    lines.AddPolyline(pts, ids, 1f);
                    links++;
                }
            }

            Debug.Log($"[Why] FigureLinksLayer.Prepare {sw.ElapsedMilliseconds} ms ({figures.Count} figures, {links} influence links)");
        }

        public override void Upload(GraphContext ctx)
        {
            if (lines == null || lines.VertexCount == 0) return;
            material = GraphMaterials.Line(GraphStyle.Humans, 1f, GraphMaterials.QueueHumans + 6, true, 0f, 1f);
            material.SetFloat("_FlowFreq", 400f);
            AddMesh("InfluenceLinks", lines.ToMesh("InfluenceLinks"), material);
            lines = null;
        }

        /// <summary>Links only matter at human scale: hide them in deep-time views.</summary>
        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (material == null) return;
            float u = GraphWarp.Current.Unroll;
            float onBranch = GraphRoot.Instance != null && GraphRoot.Instance.CurrentPreset != null &&
                             GraphRoot.Instance.CurrentPreset.Id == "overview" ? 0.35f : 1f;
            GraphMaterials.SetAlpha(material, Mathf.Lerp(onBranch, 1f, u));
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using Why.Humans.Lives;

namespace Why.Humans.Bands
{
    /// <summary>
    /// The state beneath every civilization (Data/government.json): a grey curtain hanging under each stream through its
    /// whole history, as deep as the share of output its government took, in strata of what it bought: social
    /// programs (grain doles and temple redistribution in antiquity, poor relief, then pensions and health care) at the
    /// top, the military beneath, everything else at the bottom. Published data where it exists (modern states),
    /// elsewhere Jev's judgments calibrated on that data; judged spans are drawn fainter.
    /// <para>The state meets its society through threads: taxes fall from the stream into the curtain (as dense as
    /// its revenue) and programs rise back up (as dense as its social spending). Its political actors (rulers, generals,
    /// reformers, presidents) are bright marks along the curtain's top for their years in office, each tied up to their
    /// own lifeline when they are a drawn figure. The year under the camera reads out each visible state's shares, so
    /// moving along the timeline runs the layer forward and backward through history.</para>
    /// </summary>
    public sealed class CivGovernmentLayer : GraphLayer
    {
        public override int Order => 38;
        public override IEnumerable<string> RequiredTexts => new[] { GovernmentHistory.Path };
        /// <summary>Curtain depth for a share of output (data-space height), on a square-root scale so the thin premodern
        /// states stay visible: 5% hangs .09 below its stream, 10% .13, a quarter or more .2 (above the farm layer).</summary>
        static float Depth(float share) => Mathf.Min(.2f, .4f * Mathf.Sqrt(Mathf.Max(0, share)));
        const float Gap = .012f;
        const double StepYears = 5;
        static readonly Color Social = new Color(.68f, .73f, .80f), Military = new Color(.38f, .41f, .46f), Other = new Color(.52f, .54f, .57f),
            Thread = new Color(.62f, .66f, .72f), Actor = new Color(.86f, .89f, .94f);

        GovernmentHistory history;
        HumanWorld world;
        static CivGovernmentLayer active;

        /// <summary>The middle of a state's curtain at a year (data space), for modules and tours that point at it.</summary>
        public static bool Probe(string civId, double year, out Vector3 data)
        {
            data = default;
            var l = active; if (l?.history == null || l.world == null || !l.world.ById.TryGetValue(civId, out Civ civ)) return false;
            if (!l.history.At(civId, year, out var s) || !l.world.TryBand(civ, year, out float lo, out float hi)) return false;
            data = new Vector3(DeepTime.Arc(l.world.NowYear - year), GraphStyle.HumansY - Gap - .5f * Depth(s.social + s.military + s.other), .5f * (lo + hi));
            return true;
        }
        SurfaceMeshBuilder curtain;
        LineMeshBuilder lines, threads;
        MeshRenderer curtainRenderer, lineRenderer, threadRenderer;
        readonly List<(Civ civ, LabelSpec label)> readouts = new List<(Civ, LabelSpec)>();
        readonly List<LabelSpec> actorLabels = new List<LabelSpec>();
        LabelSystem labels;
        int lastYear = int.MinValue;

        public override void Prepare(GraphContext ctx)
        {
            history = GovernmentHistory.Parse(ctx.Text(GovernmentHistory.Path));
            world = ctx.Shared<HumanWorld>(HumanWorld.SharedKey);
            if (history == null || world == null) return;
            ctx.Share(GovernmentHistory.SharedKey, history);
            labels = ctx.Labels;
            curtain = new SurfaceMeshBuilder(); lines = new LineMeshBuilder(20_000); threads = new LineMeshBuilder(20_000);
            var figures = ctx.Shared<FigurePaths>(FigurePaths.SharedKey);
            int drawn = 0;
            foreach (var civ in world.Civs)
            {
                if (!history.civs.ContainsKey(civ.Id)) continue;
                Curtain(civ); drawn++;
                var readout = new LabelSpec { Text = "", Priority = 8, SizePx = 10, Color = new Color(.78f, .82f, .88f), Hidden = true, PixelOffset = new Vector2(4, -10) };
                labels?.Add(readout); readouts.Add((civ, readout));
            }
            int actors = 0;
            foreach (var a in history.actors)
                if (world.ById.TryGetValue(a.civ ?? "", out Civ civ) && Mark(civ, a, figures)) actors++;
            Debug.Log("[Why] CivGovernment: " + drawn + " states beneath their streams, " + actors + " political actors");
        }

        /// <summary>The curtain under one stream: three strata stacked down from just under the stream's center line.</summary>
        void Curtain(Civ civ)
        {
            var (start, end) = world.Span(civ);
            var tops = new List<Vector3>[3]; var bottoms = new List<Vector3>[3];
            for (int k = 0; k < 3; k++) { tops[k] = new List<Vector3>(); bottoms[k] = new List<Vector3>(); }
            var colors = new List<Color32>[3]; for (int k = 0; k < 3; k++) colors[k] = new List<Color32>();
            var edge = new List<LinePoint>();
            // The block's top and bottom faces across the stream's width, so it reads as a block from above as well.
            var topLo = new List<Vector3>(); var topHi = new List<Vector3>(); var botLo = new List<Vector3>(); var botHi = new List<Vector3>();
            var faceColors = new List<Color32>();
            void Flush()
            {
                if (topLo.Count >= 2) { curtain.AddBand(topLo, topHi, faceColors, 0, 1); curtain.AddBand(botLo, botHi, faceColors, 0, 1); }
                topLo.Clear(); topHi.Clear(); botLo.Clear(); botHi.Clear(); faceColors.Clear();
                for (int k = 0; k < 3; k++)
                {
                    if (tops[k].Count >= 2) curtain.AddBand(tops[k], bottoms[k], colors[k], 0, 1);
                    tops[k].Clear(); bottoms[k].Clear(); colors[k].Clear();
                }
                if (edge.Count >= 2) lines.AddPolyline(edge, 0);
                edge.Clear();
            }
            int n = 0;
            for (double year = start; year <= end; year = Math.Min(end, year + StepYears))
            {
                if (!history.At(civ.Id, year, out var s) || !world.TryBand(civ, year, out float lo, out float hi)) { Flush(); if (year >= end) break; continue; }
                float u = DeepTime.Arc(world.NowYear - year), rho = .5f * (lo + hi), y = GraphStyle.HumansY - Gap;
                bool judged = s.basis == "judgment";
                float alpha = judged ? .30f + .25f * s.confidence : .70f;
                float[] parts = { s.social, s.military, s.other };
                float total = parts[0] + parts[1] + parts[2], depth = Depth(total);
                Color[] cols = { Social, Military, Other };
                for (int k = 0; k < 3; k++)
                {
                    float d = total > 0 ? depth * parts[k] / total : 0;
                    tops[k].Add(new Vector3(u, y, rho)); bottoms[k].Add(new Vector3(u, y - d, rho));
                    Color c = cols[k]; c.a = alpha; colors[k].Add(c);
                    y -= d;
                }
                edge.Add(new LinePoint(new Vector3(u, y, rho), new Color(.80f, .83f, .88f, judged ? .5f : .85f), 1.3f, 0, 1.5f));
                float inset = .15f * (hi - lo), yTop = GraphStyle.HumansY - Gap;
                topLo.Add(new Vector3(u, yTop, lo + inset)); topHi.Add(new Vector3(u, yTop, hi - inset));
                botLo.Add(new Vector3(u, y, lo + inset)); botHi.Add(new Vector3(u, y, hi - inset));
                faceColors.Add(new Color(.55f, .58f, .63f, alpha * .35f));
                // Taxes fall from the stream, programs rise to it: one thread each every few samples, more for bigger states.
                if (n++ % Math.Max(1, (int)(6 - 10 * s.revenue)) == 0)
                {
                    float top = GraphStyle.HumansY - Gap, spread = .3f * (hi - lo);
                    float off = ((n * .618034f) % 1f - .5f) * spread;
                    threads.AddSegment(new Vector3(u, GraphStyle.HumansY + .02f, rho + off), new Vector3(u, top - Depth(s.revenue) * .5f, rho + off),
                        new Color(Thread.r, Thread.g, Thread.b, .45f), 1, 0, 0, 1.4f);
                    if (s.social > .01f)
                        threads.AddSegment(new Vector3(u, top - Depth(s.social) * .5f, rho - off), new Vector3(u, GraphStyle.HumansY + .03f, rho - off),
                            new Color(Social.r, Social.g, Social.b, .5f), 1, 0, 0, 1.6f);
                }
                if (year >= end) break;
            }
            Flush();
        }

        /// <summary>A political actor's years in office along the top of their state's curtain, tied up to their lifeline.</summary>
        bool Mark(Civ civ, GovernmentHistory.Actor a, FigurePaths figures)
        {
            double from = a.from, to = Math.Min(a.to ?? world.NowYear, world.NowYear);
            if (to < from) return false;
            var pts = new List<LinePoint>(); Vector3 mid = default; bool any = false;
            for (double year = from; year <= to; year = Math.Min(to, year + 1))
            {
                if (!world.TryBand(civ, year, out float lo, out float hi)) { if (year >= to) break; continue; }
                var p = new Vector3(DeepTime.Arc(world.NowYear - year), GraphStyle.HumansY - Gap * .5f, .5f * (lo + hi));
                pts.Add(new LinePoint(p, Actor, 2.2f, .0008f, 3.2f));
                if (!any || year <= .5 * (from + to)) mid = p; any = true;
                if (year >= to) break;
            }
            if (!any) return false;
            if (pts.Count == 1) pts.Add(new LinePoint(pts[0].Data + new Vector3(-.0004f, 0, 0), Actor, 2.2f, .0008f, 3.2f));
            lines.AddPolyline(pts, 0);
            if (!string.IsNullOrEmpty(a.figure) && figures != null && figures.TryGetPoint(a.figure, .5 * (from + to), out Vector3 life))
                lines.AddSegment(mid, life, new Color(Actor.r, Actor.g, Actor.b, .55f), 1, 0, 0, 2f);
            var label = new LabelSpec { Text = a.name + " · " + a.office, Data = mid, Priority = 3, SizePx = 9.5f, Color = Actor, PixelOffset = new Vector2(4, -8) };
            labels?.Add(label); actorLabels.Add(label);
            return true;
        }

        public override void Upload(GraphContext ctx)
        {
            active = this;
            if (curtain == null) return;
            curtainRenderer = AddMesh("State curtains", curtain.ToMesh("State curtains"), GraphMaterials.Surface(Color.white, 1, GraphMaterials.QueueHumans - 2));
            lineRenderer = AddMesh("State edges and actors", lines.ToMesh("State edges"), GraphMaterials.Line(Color.white, 1, GraphMaterials.QueueHumans - 1, true));
            threadRenderer = AddMesh("Taxes and programs", threads.ToMesh("State threads"), GraphMaterials.Line(Color.white, 1, GraphMaterials.QueueHumans - 1, true));
            curtain = null; lines = threads = null;
        }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (history == null || rig == null || readouts.Count == 0) return;
            // The year under the camera: each state alive then reads out its shares at its stream.
            GraphWarp.Inverse(rig.Pose.Target, out float u, out float _);
            int year = (int)Math.Round(world.NowYear - DeepTime.YearsAgo(u));
            if (year < -3500 || rig.Pose.Distance > 16) { if (lastYear != int.MinValue) { foreach (var r in readouts) r.label.Hidden = true; labels?.MarkDirty(); lastYear = int.MinValue; } return; }
            if (year == lastYear) return;
            lastYear = year;
            foreach (var (civ, label) in readouts)
            {
                bool show = history.At(civ.Id, year, out var s) && world.TryBand(civ, year, out float lo, out float hi);
                label.Hidden = !show;
                if (!show) continue;
                world.TryBand(civ, year, out float l2, out float h2);
                label.Data = new Vector3(DeepTime.Arc(world.NowYear - year), GraphStyle.HumansY - Gap - Depth(s.revenue), .5f * (l2 + h2));
                label.Text = civ.Name + " " + Year(year) + " · state " + Pct(s.revenue) + " of output · military " + Pct(s.military) + " · social " + Pct(s.social) + " · regulation index " + s.regulation.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) +
                             (s.basis == "judgment" ? " (judged)" : "");
                labels?.SetText(label, label.Text);
            }
            labels?.MarkDirty();
        }

        static string Pct(float share) => (100 * share).ToString(share < .1f ? "0.0" : "0", System.Globalization.CultureInfo.InvariantCulture) + "%";
        static string Year(int y) => y < 0 ? (-y) + " BCE" : y.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}

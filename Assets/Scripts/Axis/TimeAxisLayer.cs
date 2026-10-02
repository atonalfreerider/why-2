using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using TMPro;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Why.Axis
{
    /// <summary>
    /// The clock face and time axis. A thin neutral base line runs just inside the content along the whole
    /// path: around the clock from the Big Bang (6 o'clock) to 3 o'clock, then down the straight human
    /// branch to the present. Tick marks name "nice" moments at every scale (13.8 Ga ... 1 CE ... 2000 ...
    /// 1 second ago) so the super-logarithmic clock and the near-linear branch stay legible; a glowing "Now"
    /// beacon rises through all three levels at the tip of the branch, and a faint post marks where time
    /// begins at the Big Bang.
    ///
    /// Scaffolding only: no hue, low intensity, highlight id 0. Everything lives in data space, so the
    /// same geometry reads as a clock face in polar views and as a ruler in unrolled ones. The base line is
    /// dimmer around the clock, where the red matter lineage already traces the path right beside it, and
    /// brightens along the branch, where life and matter have dissolved and it is the path's only floor.
    ///
    /// Level of detail is per tick and view dependent: a tick fades as a tick of equal or higher rank gets
    /// close on screen, which works for the polar clock and for any unrolled window alike. A tick's label
    /// hides with it and is pushed to the inner side of its tick whatever the camera angle.
    /// </summary>
    [GraphScenes(GraphScene.Why, GraphScene.Economy)]
    public sealed class TimeAxisLayer : GraphLayer
    {
        /// <summary>Height of the ring and ticks: just under the matter level, the floor of the graph.</summary>
        public const float AxisY = GraphStyle.MatterY - 0.005f;

        /// <summary>Radial position of the base ring (just inside the inner track).</summary>
        public const float RingRho = -0.015f;

        /// <summary>Radial position where ticks start; they grow inward from here.</summary>
        public const float TickRho = -0.02f;

        /// <summary>Top of the "Now" beacon: above the highest human lifelines.</summary>
        public const float BeaconTop = GraphStyle.HumansY + GraphStyle.SmvHeight + 0.15f;

        /// <summary>Anchor key of the beacon label ("now" itself is resolved by the core).</summary>
        public const string BeaconAnchor = "now:beacon";

        // ring sampling: at most ArcStep in u and RingMaxLn in ln(yearsAgo + RingLogOffset), so the ring
        // stays round on the clock and smooth while any preset window unrolls (smallest log offset 400)
        const float ArcStep = 0.0025f;
        const double RingLogOffset = 300;
        const double RingMaxLn = 0.005;

        // base line opacity around the clock (beside the matter lineage) and along the human branch
        const float RingAlphaClock = 0.3f;
        const float RingAlphaBranch = 0.55f;

        // level of detail: a tick fades in as its nearest equal-or-higher-rank neighbor moves from
        // FadeStartPx to FadeFullPx away on screen (1080p reference pixels)
        const float FadeStartPx = 5;
        const float FadeFullPx = 16;
        const float LabelHideBelow = 0.08f;
        const float LabelShowAbove = 0.14f;
        const float LabelGapPx = 3;

        const float TickWidthWorld = 0.0006f;
        const float BeaconIntensity = 2f;
        const float BeaconPulse = 0.2f;

        struct TickStyle
        {
            public float Length;     // radial length (data units)
            public float WidthPx;
            public float Alpha;
            public float Intensity;
            public Color Color;
            public float LabelSize;
            public Color LabelColor;
        }

        public override int Order => 1;

        LineMeshBuilder lines;
        DynamicLineMesh tickMesh;
        Material material;

        TimeTick[] ticks;
        int[] tickSegment;       // tick -> segment in tickMesh
        int[] gridSegment;       // tick -> vertical grid segment, or -1
        LabelSpec[] labels;      // tick -> label, or null
        bool[] nudge;            // tick -> label shifted toward the present (the Big Bang tick, beside its post)
        Vector3[] screen;        // tick -> screen position of its base, per view

        int lastCam = -1, lastWarp = -1, lastWidth, lastHeight;

        /// <summary>
        /// A scene's dimming of the axis and its ticks (1 = none): the economy's land views set it each frame to the road's
        /// emphasis (0.35 while the land is open); the causality scene never sets it.
        /// </summary>
        public static float SceneAlpha = 1f;

        float appliedAlpha = 1f;

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();

            lines = new LineMeshBuilder(2048);
            BuildRing();
            BuildSeam();
            BuildBeacon(ctx);
            int labelCount = BuildTicks(ctx);

            Debug.Log($"[Why] TimeAxisLayer.Prepare {sw.Elapsed.TotalMilliseconds:0.0} ms " +
                      $"({ticks.Length} ticks, {labelCount} labels, {lines.VertexCount + tickMesh.SegmentCount * 4} vertices)");
        }

        // --- base line, Big Bang post and beacon (static) --------------------------------------------------

        /// <summary>The base line from the Big Bang to now, handing over from the clock to the human branch.</summary>
        void BuildRing()
        {
            List<LinePoint> pts = new List<LinePoint>(4096);
            float u = 1f;
            pts.Add(RingPoint(u));
            while (u > DeepTime.NowArc)
            {
                float step = ArcStep;
                double d = Math.Abs(LnAge(u) - LnAge(u - step));
                if (d > RingMaxLn) step = Mathf.Max(step * (float)(RingMaxLn / d), 1e-5f);
                u = Mathf.Max(u - step, DeepTime.NowArc);
                pts.Add(RingPoint(u));
            }

            lines.AddPolyline(pts, GraphIds.None);
        }

        static LinePoint RingPoint(float u)
        {
            float alpha = Mathf.Lerp(RingAlphaBranch, RingAlphaClock, GraphStyle.HandoffFade(u));
            return new LinePoint(new Vector3(u, AxisY, RingRho), Tint(GraphStyle.Axis, alpha), 1.1f, 0.0008f);
        }

        static double LnAge(float u) => Math.Log(DeepTime.YearsAgo(Math.Max(u, 0)) + RingLogOffset);

        /// <summary>
        /// A faint vertical post at the Big Bang where time begins, answering the "Now" beacon at the other
        /// end of the path (a left edge through all three levels in unrolled views of the early universe).
        /// </summary>
        void BuildSeam()
        {
            lines.AddSegment(new Vector3(1f, AxisY, 0), new Vector3(1f, GraphStyle.HumansY + GraphStyle.SmvHeight, 0),
                Tint(GraphStyle.AxisDim, 0.6f), 1f, 0, GraphIds.None);
        }

        /// <summary>
        /// The present moment: a white line rising through matter, life and humans (brightening upward),
        /// with a slow pulse and a notch at each level's height.
        /// </summary>
        void BuildBeacon(GraphContext ctx)
        {
            float u = DeepTime.NowArc;
            float[] ys = { AxisY, GraphStyle.LifeY, GraphStyle.HumansY, GraphStyle.HumansY + GraphStyle.SmvHeight, BeaconTop };
            float[] alphas = { 0.45f, 0.65f, 0.8f, 1f, 1f };
            List<LinePoint> pts = new List<LinePoint>(ys.Length);
            for (int i = 0; i < ys.Length; i++)
            {
                pts.Add(new LinePoint(new Vector3(u, ys[i], 0), Tint(Color.white, alphas[i]), 1.6f, 0.002f,
                    BeaconIntensity));
            }

            lines.AddPolyline(pts, GraphIds.None, BeaconPulse);

            Color32 notch = Tint(Color.white, 0.8f);
            foreach (float y in new[] { GraphStyle.MatterY, GraphStyle.LifeY, GraphStyle.HumansY })
            {
                lines.AddSegment(new Vector3(u, y, -0.012f), new Vector3(u, y, 0.012f), notch, 1.5f, 0, GraphIds.None,
                    1.4f);
            }

            Anchor anchor = new Anchor
            {
                Key = BeaconAnchor,
                Label = "Now",
                Blurb = "This moment. Matter, life and humans all arrive here: everything on the clock is part " +
                        "of the chain of causes that leads to this instant.",
                Level = GraphLevel.None,
                YearsAgo = 0,
                EndYearsAgo = 0,
                Y = BeaconTop,
                Rho = 0,
                Tier = 1
            };
            Anchors.Register(anchor);

            ctx.Labels.Add(new LabelSpec
            {
                Text = "Now",
                Data = new Vector3(u, BeaconTop, 0),
                Priority = 100,
                SizePx = 15,
                Color = GraphStyle.Text,
                Align = TextAlignmentOptions.Center,
                PixelOffset = new Vector2(0, 13),
                AnchorKey = BeaconAnchor
            });
        }

        // --- ticks (dynamic level of detail) ----------------------------------------------------------

        int BuildTicks(GraphContext ctx)
        {
            ticks = TimeTicks.Build(ctx.NowYear).ToArray();
            int n = ticks.Length;
            tickMesh = new DynamicLineMesh(n + 32);
            tickSegment = new int[n];
            gridSegment = new int[n];
            labels = new LabelSpec[n];
            nudge = new bool[n];
            screen = new Vector3[n];

            int labelCount = 0;
            for (int i = 0; i < n; i++)
            {
                TimeTick t = ticks[i];
                TickStyle st = Style(t.Class);
                float end = TickRho - st.Length;
                Color32 c = Tint(st.Color, st.Alpha);
                tickSegment[i] = tickMesh.AddSegment(new Vector3(t.U, AxisY, TickRho), new Vector3(t.U, AxisY, end), c, c,
                    st.WidthPx, TickWidthWorld, GraphIds.None, st.Intensity);

                // faint vertical grid at major moments helps read time across the three levels
                bool interior = t.YearsAgo > 0 && t.YearsAgo < DeepTime.AgeU;
                gridSegment[i] = t.Class == TickClass.Major && interior
                    ? tickMesh.AddSegment(new Vector3(t.U, AxisY, RingRho), new Vector3(t.U, GraphStyle.HumansY, RingRho),
                        Tint(GraphStyle.Axis, 0.1f), Tint(GraphStyle.Axis, 0.025f), 1f, 0, GraphIds.None)
                    : -1;

                if (t.Text == null) continue;
                labels[i] = new LabelSpec
                {
                    Text = t.Text,
                    Data = new Vector3(t.U, AxisY, end),
                    Priority = t.LabelPriority,
                    SizePx = st.LabelSize,
                    Color = st.LabelColor,
                    Align = TextAlignmentOptions.Center,
                    // hover tooltip and click-to-focus through the core's dynamic "time:" anchors
                    AnchorKey = "time:" + t.YearsAgo.ToString("R", CultureInfo.InvariantCulture)
                };
                nudge[i] = t.YearsAgo >= DeepTime.AgeU;
                ctx.Labels.Add(labels[i]);
                labelCount++;
            }

            return labelCount;
        }

        static TickStyle Style(TickClass c)
        {
            switch (c)
            {
                case TickClass.Major:
                    return new TickStyle
                    {
                        Length = 0.07f, WidthPx = 1.4f, Alpha = 0.85f, Intensity = 1.2f, Color = GraphStyle.Axis,
                        LabelSize = 12.5f, LabelColor = GraphStyle.Text
                    };
                case TickClass.Medium:
                    return new TickStyle
                    {
                        Length = 0.045f, WidthPx = 1.1f, Alpha = 0.6f, Intensity = 1f, Color = GraphStyle.Axis,
                        LabelSize = 11f, LabelColor = GraphStyle.TextDim
                    };
                case TickClass.Minor:
                    return new TickStyle
                    {
                        Length = 0.03f, WidthPx = 1f, Alpha = 0.45f, Intensity = 1f, Color = GraphStyle.Axis,
                        LabelSize = 10.5f, LabelColor = GraphStyle.TextDim
                    };
                default:
                    return new TickStyle
                    {
                        Length = 0.02f, WidthPx = 1f, Alpha = 0.6f, Intensity = 1f, Color = GraphStyle.AxisDim,
                        LabelSize = 10f,
                        LabelColor = new Color(GraphStyle.TextDim.r, GraphStyle.TextDim.g, GraphStyle.TextDim.b, 0.75f)
                    };
            }
        }

        public override void Upload(GraphContext ctx)
        {
            if (lines == null || tickMesh == null) return;
            material = GraphMaterials.Line(Color.white, 1f, GraphMaterials.QueueOverlay - 10, true, 0f, 1f);
            AddMesh("TimeAxis", lines.ToMesh("TimeAxis"), material);
            AddMesh("TimeTicks", tickMesh.CreateMesh("TimeTicks"), material);
            lines = null;
        }

        // --- per view ---------------------------------------------------------------------------------

        /// <summary>Re-evaluates tick fades and label placement whenever the camera, warp or screen changes.</summary>
        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (ticks == null || rig == null || rig.Cam == null) return;
            if (material != null && SceneAlpha != appliedAlpha)
            {
                appliedAlpha = SceneAlpha;
                GraphMaterials.SetAlpha(material, appliedAlpha);
            }

            int width = Screen.width, height = Screen.height;
            if (rig.Version == lastCam && GraphWarp.Version == lastWarp && width == lastWidth && height == lastHeight)
            {
                return;
            }

            lastCam = rig.Version;
            lastWarp = GraphWarp.Version;
            lastWidth = width;
            lastHeight = height;

            // GraphWarp.ToWorld without a state uses the current warp with its derived frame cached, so this
            // loop does not re-derive the junction frame for every tick
            Camera cam = rig.Cam;
            float ui = LabelSystem.UiScale;
            for (int i = 0; i < ticks.Length; i++)
            {
                screen[i] = cam.WorldToScreenPoint(GraphWarp.ToWorld(ticks[i].U, AxisY, TickRho));
            }

            float margin = 200 * ui;
            for (int i = 0; i < ticks.Length; i++)
            {
                Vector3 s = screen[i];
                // off-screen ticks are re-evaluated by the view change that brings them back
                if (s.z <= 0 || s.x < -margin || s.y < -margin || s.x > width + margin || s.y > height + margin) continue;

                float fade = Smooth01((Spacing(i) / ui - FadeStartPx) / (FadeFullPx - FadeStartPx));
                tickMesh.SetFade(tickSegment[i], fade);
                if (gridSegment[i] >= 0) tickMesh.SetFade(gridSegment[i], fade);

                LabelSpec label = labels[i];
                if (label == null) continue;
                if (label.Hidden ? fade > LabelShowAbove : fade < LabelHideBelow) label.Hidden = !label.Hidden;
                if (!label.Hidden) PlaceLabel(i, label, cam);
            }

            tickMesh.Apply();
            ctx.Labels.MarkDirty();
        }

        /// <summary>Screen distance to the nearest tick of equal or higher rank (pixels).</summary>
        float Spacing(int i)
        {
            float best = float.MaxValue;
            Vector3 s = screen[i];
            int older = ticks[i].Older, younger = ticks[i].Younger;
            if (older >= 0 && screen[older].z > 0) best = Mathf.Min(best, Distance2D(s, screen[older]));
            if (younger >= 0 && screen[younger].z > 0) best = Mathf.Min(best, Distance2D(s, screen[younger]));
            return best;
        }

        /// <summary>
        /// Centers the label just past the inner end of its tick, along the tick's on-screen direction, so
        /// it never covers the ring or the content outside it: beside the tick at 3 and 9 o'clock, under it
        /// at 12 o'clock and in unrolled views, above it at 6 o'clock, left of it along the human branch.
        /// </summary>
        void PlaceLabel(int i, LabelSpec label, Camera cam)
        {
            Vector3 d = label.Data;
            Vector3 end = cam.WorldToScreenPoint(GraphWarp.ToWorld(d));
            if (end.z <= 0) return;

            Vector2 dir = Direction(screen[i], end, new Vector2(0, -1));
            float halfW = LabelWidth(label) * 0.5f;
            float halfH = label.SizePx * 0.575f;
            Vector2 offset = dir * (halfW * Mathf.Abs(dir.x) + halfH * Mathf.Abs(dir.y) + LabelGapPx);

            if (nudge[i])
            {
                // step toward the present so the label sits beside the Big Bang post, not across it
                Vector3 ahead = cam.WorldToScreenPoint(GraphWarp.ToWorld(d.x - 0.003f, d.y, d.z));
                if (ahead.z > 0) offset += Direction(end, ahead, Vector2.zero) * (halfW + LabelGapPx);
            }

            label.PixelOffset = offset;
        }

        /// <summary>Label width in reference pixels (the label system's own estimate once it has seen the label).</summary>
        static float LabelWidth(LabelSpec label) =>
            label.Width > 0 ? label.Width : label.Text.Length * 0.55f * label.SizePx;

        static Vector2 Direction(Vector3 from, Vector3 to, Vector2 fallback)
        {
            Vector2 v = new Vector2(to.x - from.x, to.y - from.y);
            float length = v.magnitude;
            return length > 0.5f ? v / length : fallback;
        }

        static float Distance2D(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dy = a.y - b.y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3 - 2 * x);
        }

        static Color32 Tint(Color c, float alpha) =>
            new Color32((byte)(Mathf.Clamp01(c.r) * 255f + 0.5f), (byte)(Mathf.Clamp01(c.g) * 255f + 0.5f),
                (byte)(Mathf.Clamp01(c.b) * 255f + 0.5f), (byte)(Mathf.Clamp01(alpha) * 255f + 0.5f));
    }
}

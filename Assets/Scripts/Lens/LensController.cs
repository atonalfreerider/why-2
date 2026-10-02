using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Why.Lens
{
    /// <summary>
    /// Free-form re-scaling: unroll the ring around whatever the camera is looking at, then widen or
    /// narrow the time window and morph between logarithmic and linear time. Presets are fixed stops;
    /// the lens makes any period of history examinable at any scale.
    ///
    /// U = unroll here / roll back up, [ and ] = wider / narrower window, L = cycle log / mixed / linear.
    /// The causality graph only: the economy scene keeps one lens window for every view (EconomyStage), and its cut
    /// and land are placed in that window.
    /// </summary>
    [GraphScenes(GraphScene.Why)]
    public sealed class LensController : GraphModule
    {
        /// <summary>Log offset as a fraction of the window's geometric center: small = log, large = linear.</summary>
        static readonly float[] LinearityLevels = { 0.02f, 0.35f, 6f };

        /// <summary>
        /// Portrait: the unrolled span per unit of camera distance, relative to landscape (a turned portrait view
        /// fits ~0.7 distance units of timeline between its bars, a landscape view ~1.5 across its width).
        /// </summary>
        const float PortraitSpan = 0.5f;

        GraphRoot root;
        double yaOld, yaNew;
        int linearity = 1;
        bool lensActive;

        public override int Order => 5;

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            root.FocusChanged += p => lensActive = p != null && p.Id == "lens";
        }

        void Update()
        {
            if (root == null || !root.IsLoaded || root.TourActive) return;
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.uKey.wasPressedThisFrame)
            {
                if (GraphWarp.Target.Unroll > 0.5f) root.Focus("overview");
                else UnrollAtTarget();
            }

            if (!lensActive && GraphWarp.Target.Unroll < 0.5f) return;
            if (kb.rightBracketKey.wasPressedThisFrame) Rescale(0.5);
            if (kb.leftBracketKey.wasPressedThisFrame) Rescale(2.0);
            if (kb.lKey.wasPressedThisFrame)
            {
                if (!lensActive) AdoptCurrentWindow();
                linearity = (linearity + 1) % LinearityLevels.Length;
                Apply(1.2f);
            }
        }

        /// <summary>Unroll a window centered on the camera target, sized by the camera distance.</summary>
        public void UnrollAtTarget()
        {
            Vector3 target = root.Rig.Pose.Target;
            float u = ArcAt(target);
            // a portrait view shows about half the stretch of path a landscape view shows from the same distance
            float distance = root.Rig.Pose.Distance * (ScreenLayout.IsPortrait ? PortraitSpan : 1f);
            float span = Mathf.Clamp(distance / (2f * Mathf.PI * GraphStyle.R0) * 0.55f, 0.003f, 0.2f);
            yaOld = DeepTime.YearsAgo(Mathf.Min(1f, u + span));
            yaNew = DeepTime.YearsAgo(Mathf.Max(DeepTime.NowArc, u - span));
            linearity = 1;
            Apply(2.2f);
        }

        void AdoptCurrentWindow()
        {
            WarpState w = GraphWarp.Target;
            double c = Math.Max(w.LogOffset, 1e-9);
            double half = w.FadeHalfLength / Math.Max(w.KLin, 1e-6);
            double center = Math.Log(Math.Max(w.FocusYearsAgo, 0) + c);
            yaOld = Math.Exp(center + half) - c;
            yaNew = Math.Max(0, Math.Exp(center - half) - c);
            linearity = 1;
        }

        /// <summary>Scale the window span in log space around its geometric center (factor &lt; 1 zooms in).</summary>
        void Rescale(double factor)
        {
            if (!lensActive) AdoptCurrentWindow();
            double lo = Math.Log(Math.Max(yaNew, 1e-3) + 1), hi = Math.Log(yaOld + 1);
            double mid = 0.5 * (lo + hi), half = Math.Max(0.5 * (hi - lo) * factor, 1e-4);
            yaOld = Math.Min(DeepTime.AgeU, Math.Exp(mid + half) - 1);
            yaNew = Math.Max(0, Math.Exp(mid - half) - 1);
            Apply(1.2f);
        }

        void Apply(float seconds)
        {
            double geo = Math.Sqrt(Math.Max(yaOld, 1) * Math.Max(yaNew, 1));
            double logOffset = Math.Max(1e-3, geo * LinearityLevels[linearity] + (yaOld - yaNew) * 0.02 * linearity);
            ViewPreset p = new ViewPreset
            {
                Id = "lens",
                Title = "Lens",
                Subtitle = DeepTime.FormatYearsAgo(yaOld, DeepTime.NowYear) + "  to  " +
                           DeepTime.FormatYearsAgo(yaNew, DeepTime.NowYear) +
                           (linearity == 0 ? "  (log)" : linearity == 2 ? "  (linear)" : ""),
                YaOld = yaOld,
                YaNew = yaNew,
                LogOffset = logOffset,
                Length = 12,
                StrataEmphasis = 0.35f,
                TargetRho = RhoAt(root.Rig.Pose.Target),
                TargetY = root.Rig.Pose.Target.y,
                Pitch = Mathf.Clamp(root.Rig.Pose.Pitch, 25, 75),
                Distance = 10
            };
            root.Focus(p, seconds);
        }

        /// <summary>The clock arc under a world position (inverse of the current warp).</summary>
        public static float ArcAt(Vector3 world)
        {
            GraphWarp.Inverse(world, out float u, out float _);
            return u;
        }

        /// <summary>Relevance (rho) under a world position (inverse of the current warp).</summary>
        public static float RhoAt(Vector3 world)
        {
            GraphWarp.Inverse(world, out float _, out float rho);
            return rho;
        }
    }
}

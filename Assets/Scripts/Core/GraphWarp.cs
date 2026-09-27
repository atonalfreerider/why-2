using System;
using UnityEngine;

namespace Why
{
    /// <summary>
    /// Parameters of the data-space -> world-space warp (see Docs/ARCHITECTURE.md).
    /// </summary>
    [Serializable]
    public struct WarpState
    {
        /// <summary>Focus arc: the point of the path that stays fixed while unrolling.</summary>
        public float FocusArc;

        /// <summary>0 = the base path (clock + straight human branch), 1 = the lens window is a straight timeline.</summary>
        public float Unroll;

        /// <summary>Lens log offset in years: small = logarithmic time, large = linear.</summary>
        public double LogOffset;

        /// <summary>Years ago at the center of the lens window.</summary>
        public double FocusYearsAgo;

        /// <summary>World units per ln unit of (yearsAgo + LogOffset) in the lens.</summary>
        public float KLin;

        public float RhoScale;
        public float YScale;

        /// <summary>Half length (world units) of the lens window; content beyond fades.</summary>
        public float FadeHalfLength;

        public float FadeSoftness;

        /// <summary>How strongly out-of-window content fades (usually = Unroll).</summary>
        public float FadeAmount;

        public static WarpState Polar => new WarpState
        {
            FocusArc = 0.5f,
            Unroll = 0,
            LogOffset = 1e3,
            FocusYearsAgo = 1e3,
            KLin = 1,
            RhoScale = 1,
            YScale = 1,
            FadeHalfLength = 1e4f,
            FadeSoftness = 1,
            FadeAmount = 0
        };

        /// <summary>
        /// A state that unrolls the window [yaNew, yaOld] into a straight timeline of the given length.
        /// </summary>
        public static WarpState Window(double yaOld, double yaNew, double logOffset, float length, float unroll = 1,
            float rhoScale = 1, float yScale = 1)
        {
            double lo = Math.Log(Math.Max(yaNew, 0) + logOffset);
            double hi = Math.Log(Math.Max(yaOld, 0) + logOffset);
            double center = 0.5 * (lo + hi);
            double yaF = Math.Exp(center) - logOffset;
            return new WarpState
            {
                FocusArc = DeepTime.Arc(Math.Max(yaF, 1e-9)),
                Unroll = unroll,
                LogOffset = logOffset,
                FocusYearsAgo = yaF,
                KLin = (float)(length / Math.Max(hi - lo, 1e-9)),
                RhoScale = rhoScale,
                YScale = yScale,
                FadeHalfLength = length * 0.5f,
                FadeSoftness = length * 0.25f,
                FadeAmount = unroll
            };
        }

        public static WarpState Lerp(WarpState a, WarpState b, float t)
        {
            // Lens parameters are irrelevant while nothing is unrolled; snap them so the unroll animation
            // straightens directly into the target window instead of sweeping through others.
            // The focus has no effect on the base path either, so the path unrolls around its final focus
            // instead of sliding along while it straightens.
            if (a.Unroll < 1e-3f)
            {
                a.LogOffset = b.LogOffset;
                a.FocusYearsAgo = b.FocusYearsAgo;
                a.KLin = b.KLin;
                a.FocusArc = b.FocusArc;
            }

            if (b.Unroll < 1e-3f)
            {
                b.LogOffset = a.LogOffset;
                b.FocusYearsAgo = a.FocusYearsAgo;
                b.KLin = a.KLin;
                b.FocusArc = a.FocusArc;
            }

            return new WarpState
            {
                FocusArc = Mathf.Lerp(a.FocusArc, b.FocusArc, t),
                Unroll = Mathf.Lerp(a.Unroll, b.Unroll, t),
                LogOffset = LogLerp(a.LogOffset, b.LogOffset, t),
                FocusYearsAgo = LogLerp(a.FocusYearsAgo + a.LogOffset, b.FocusYearsAgo + b.LogOffset, t) -
                                LogLerp(a.LogOffset, b.LogOffset, t),
                KLin = (float)LogLerp(a.KLin, b.KLin, t),
                RhoScale = (float)LogLerp(a.RhoScale, b.RhoScale, t),
                YScale = (float)LogLerp(a.YScale, b.YScale, t),
                FadeHalfLength = (float)LogLerp(a.FadeHalfLength, b.FadeHalfLength, t),
                FadeSoftness = (float)LogLerp(a.FadeSoftness, b.FadeSoftness, t),
                FadeAmount = Mathf.Lerp(a.FadeAmount, b.FadeAmount, t)
            };
        }

        static double LogLerp(double a, double b, float t)
        {
            a = Math.Max(a, 1e-12);
            b = Math.Max(b, 1e-12);
            return Math.Exp(Math.Log(a) + (Math.Log(b) - Math.Log(a)) * t);
        }
    }

    /// <summary>
    /// Owns the current warp, animates it, pushes it to the shaders, and mirrors the vertex shader's
    /// data -> world mapping on the CPU (labels, anchors, picking, camera framing).
    ///
    /// The base path is a circle from the Big Bang (6 o'clock) clockwise to 3 o'clock, where the human
    /// era leaves the circle on a straight tangent branch with near-linear time up to the present. The
    /// lens (Unroll) straightens the path around a focus and re-maps time to a window.
    /// </summary>
    public static class GraphWarp
    {
        static readonly int WarpA = Shader.PropertyToID("_WhyWarpA");
        static readonly int WarpB = Shader.PropertyToID("_WhyWarpB");
        static readonly int WarpC = Shader.PropertyToID("_WhyWarpC");
        static readonly int JId = Shader.PropertyToID("_WhyJ");
        static readonly int BaseAId = Shader.PropertyToID("_WhyBaseA");
        static readonly int BaseBId = Shader.PropertyToID("_WhyBaseB");
        static readonly int LnAge = Shader.PropertyToID("_WhyLnAge");

        static WarpState from = WarpState.Polar;
        static WarpState to = WarpState.Polar;
        static float t = 1, duration = 1;

        public static WarpState Current { get; private set; } = WarpState.Polar;
        public static WarpState Target => to;

        /// <summary>Incremented whenever the warp changes; consumers cache against it.</summary>
        public static int Version { get; private set; }

        public static bool Animating => t < 1;

        public static void Set(WarpState state)
        {
            from = to = Current = state;
            t = 1;
            Version++;
            Push();
        }

        public static void AnimateTo(WarpState target, float seconds)
        {
            if (seconds <= 0)
            {
                Set(target);
                return;
            }

            from = Current;
            to = target;
            duration = seconds;
            t = 0;
        }

        /// <summary>Advance the animation; call once per frame.</summary>
        public static void Tick(float dt)
        {
            if (t >= 1) return;
            t = Mathf.Min(1, t + dt / duration);
            float e = t * t * t * (t * (t * 6 - 15) + 10); // smootherstep
            Current = WarpState.Lerp(from, to, e);
            Version++;
            Push();
        }

        // ------------------------------------------------------------------ base path

        /// <summary>
        /// The unwarped path: a circle of radius R0 from the Big Bang (6 o'clock) clockwise to 3 o'clock,
        /// then a straight human branch tangent to the circle with near-linear time up to the present.
        /// </summary>
        public static class BasePath
        {
            /// <summary>Years ago at which the path leaves the circle (the dawn of civilizations, ~3000 BCE).</summary>
            public const double HandoffYearsAgo = 5000;

            /// <summary>Clockwise angle travelled on the circle from the Big Bang to the handoff (3 o'clock).</summary>
            public const float HandoffAngle = 1.5f * Mathf.PI;

            /// <summary>Length of the straight human branch (world units).</summary>
            public const float BranchLength = 6f;

            /// <summary>Log offset of the branch's time axis (large = linear; this is mostly linear).</summary>
            public const double BranchLogOffset = 3000;

            /// <summary>Clock arc of the handoff (3 o'clock).</summary>
            public static readonly float HandoffArc = DeepTime.Arc(HandoffYearsAgo);

            public static readonly float SigmaHandoff = GraphStyle.R0 * HandoffAngle;
            public static readonly float SigmaPerArc = SigmaHandoff / (1f - HandoffArc);
            static readonly double lnYaHC = Math.Log(HandoffYearsAgo + BranchLogOffset);
            static readonly double lnCH = Math.Log(BranchLogOffset);
            public static readonly float SigmaPerLn = (float)(BranchLength / (lnYaHC - lnCH));
            public static readonly float LnHandoffC = (float)lnYaHC;

            /// <summary>Arc length along the base path from the Big Bang (shader float math).</summary>
            public static float Sigma(float u, float ya)
            {
                if (u >= HandoffArc) return (1f - u) * SigmaPerArc;
                return SigmaHandoff + SigmaPerLn * (LnHandoffC - Mathf.Log(ya + (float)BranchLogOffset));
            }

            /// <summary>Position, tangent (toward the present) and outward normal of the unwarped path (xz).</summary>
            public static void Frame(float sigma, out Vector2 p, out Vector2 t, out Vector2 n)
            {
                if (sigma <= SigmaHandoff)
                {
                    float theta = 2f * Mathf.PI - sigma / GraphStyle.R0;
                    n = new Vector2(Mathf.Sin(theta), -Mathf.Cos(theta));
                    t = new Vector2(-Mathf.Cos(theta), -Mathf.Sin(theta));
                    p = GraphStyle.R0 * n;
                    return;
                }

                // at 3 o'clock: outward is +x, toward the present is -z
                n = new Vector2(1, 0);
                t = new Vector2(0, -1);
                p = new Vector2(GraphStyle.R0, 0) + t * (sigma - SigmaHandoff);
            }
        }

        /// <summary>Warp quantities derived from a state (what the shader receives).</summary>
        struct Derived
        {
            public float SigmaF, SH, BendRadius, LensC, LensLnYaF;
            public Vector2 PJ, TJ, PF, TF, NF;
        }

        static Derived Derive(WarpState s)
        {
            Derived d;
            float unroll = Mathf.Clamp01(s.Unroll);
            d.LensC = (float)Math.Max(s.LogOffset, 1e-9);
            d.LensLnYaF = (float)Math.Log(Math.Max(s.FocusYearsAgo, 0) + Math.Max(s.LogOffset, 1e-9));
            float uF = Mathf.Max(s.FocusArc, 1e-4f);
            d.SigmaF = BasePath.Sigma(uF, ShaderYearsAgo(uF));
            BasePath.Frame(d.SigmaF, out d.PF, out d.TF, out d.NF);

            float sLinH = s.KLin * (d.LensLnYaF - Mathf.Log((float)BasePath.HandoffYearsAgo + d.LensC));
            d.SH = Mathf.Lerp(BasePath.SigmaHandoff - d.SigmaF, sLinH, unroll);
            d.BendRadius = GraphStyle.R0 / Mathf.Max(1f - unroll, 1e-3f);

            if (d.SigmaF <= BasePath.SigmaHandoff)
            {
                // the focus is on the circle: walk the (straightening) circle to the junction
                float phi = d.SH / d.BendRadius;
                float sh = Mathf.Sin(phi * 0.5f);
                d.PJ = d.PF + d.TF * (d.BendRadius * Mathf.Sin(phi)) - d.NF * (2f * d.BendRadius * sh * sh);
                d.TJ = d.TF * Mathf.Cos(phi) - d.NF * Mathf.Sin(phi);
            }
            else
            {
                // the focus is on the straight branch: the junction lies behind it on the same line
                d.PJ = d.PF + d.TF * d.SH;
                d.TJ = d.TF;
            }

            return d;
        }

        static Derived current;
        static int currentVersion = -1;

        static Derived CurrentDerived()
        {
            if (currentVersion != Version)
            {
                current = Derive(Current);
                currentVersion = Version;
            }

            return current;
        }

        public static void Push()
        {
            WarpState s = Current;
            Derived d = Derive(s);
            current = d;
            currentVersion = Version;
            Shader.SetGlobalVector(WarpA, new Vector4(d.SigmaF, GraphStyle.R0, Mathf.Clamp01(s.Unroll), s.YScale));
            Shader.SetGlobalVector(WarpB, new Vector4(d.LensC, d.LensLnYaF, s.KLin, s.RhoScale));
            Shader.SetGlobalVector(WarpC, new Vector4(s.FadeHalfLength, Mathf.Max(s.FadeSoftness, 1e-4f), s.FadeAmount, d.SH));
            Shader.SetGlobalVector(JId, new Vector4(d.PJ.x, d.PJ.y, d.TJ.x, d.TJ.y));
            Shader.SetGlobalVector(BaseAId, new Vector4(BasePath.HandoffArc, BasePath.SigmaPerArc, BasePath.SigmaHandoff, BasePath.SigmaPerLn));
            Shader.SetGlobalVector(BaseBId, new Vector4((float)BasePath.BranchLogOffset, BasePath.LnHandoffC, d.BendRadius,
                GraphStyle.HandoffFadeStartArc));
            Shader.SetGlobalFloat(LnAge, (float)DeepTime.LnAgeU);
        }

        /// <summary>Years ago for an arc, as the shader computes it (float precision).</summary>
        public static float ShaderYearsAgo(float u)
        {
            u = Mathf.Max(u, 1e-4f);
            float a = -1.4f - 2.39f * u;
            float g = Mathf.Pow(u, a) * Mathf.Log(u);
            return Mathf.Exp(g + (float)DeepTime.LnAgeU);
        }

        /// <summary>Lens time coordinate (world units from the focus, positive toward the present) - drives the focus fade.</summary>
        public static float UnrolledS(float u, WarpState s)
        {
            float c = (float)Math.Max(s.LogOffset, 1e-9);
            float lnYaF = (float)Math.Log(Math.Max(s.FocusYearsAgo, 0) + c);
            return s.KLin * (lnYaF - Mathf.Log(ShaderYearsAgo(u) + c));
        }

        public static Vector3 ToWorld(float u, float y, float rho) => ToWorld(u, y, rho, CurrentDerived(), Current);

        public static Vector3 ToWorld(Vector3 data) => ToWorld(data.x, data.y, data.z, CurrentDerived(), Current);

        /// <summary>Exact mirror of WhyToWorld in WhyCommon.hlsl.</summary>
        public static Vector3 ToWorld(float u, float y, float rho, WarpState s) => ToWorld(u, y, rho, Derive(s), s);

        static Vector3 ToWorld(float u, float y, float rho, Derived d, WarpState s)
        {
            u = Mathf.Max(u, 1e-4f);
            float ya = ShaderYearsAgo(u);
            float sLin = s.KLin * (d.LensLnYaF - Mathf.Log(ya + d.LensC));
            float arc = Mathf.Lerp(BasePath.Sigma(u, ya) - d.SigmaF, sLin, Mathf.Clamp01(s.Unroll));
            Vector2 nj = new Vector2(-d.TJ.y, d.TJ.x);
            float rhoW = rho * s.RhoScale;

            Vector2 p;
            if (arc >= d.SH)
            {
                p = d.PJ + d.TJ * (arc - d.SH) + nj * rhoW;
            }
            else
            {
                float R = d.BendRadius;
                float phi = (arc - d.SH) / R;
                float sh = Mathf.Sin(phi * 0.5f);
                Vector2 c = d.PJ + d.TJ * (R * Mathf.Sin(phi)) - nj * (2f * R * sh * sh);
                Vector2 n = nj * Mathf.Cos(phi) + d.TJ * Mathf.Sin(phi);
                p = c + n * rhoW;
            }

            return new Vector3(p.x, y * s.YScale, p.y);
        }

        /// <summary>Fade factor for out-of-focus time, mirroring the shader.</summary>
        public static float FocusFade(float u, WarpState s)
        {
            if (s.FadeAmount <= 0) return 1;
            float d = Mathf.Abs(UnrolledS(u, s));
            float k = Mathf.Clamp01((d - s.FadeHalfLength) / Mathf.Max(s.FadeSoftness, 1e-4f));
            k = k * k * (3 - 2 * k);
            return 1 - s.FadeAmount * k;
        }

        /// <summary>Outward normal of the path (world xz) at the focus.</summary>
        public static Vector3 FocusNormal(WarpState s)
        {
            Derived d = Derive(s);
            return new Vector3(d.NF.x, 0, d.NF.y);
        }

        /// <summary>Tangent of the path (world xz) at the focus, pointing into the past.</summary>
        public static Vector3 FocusTangent(WarpState s)
        {
            Derived d = Derive(s);
            return new Vector3(-d.TF.x, 0, -d.TF.y);
        }

        /// <summary>Outward normal of the path (world xz) at an arc under a warp (camera framing, picking).</summary>
        public static Vector3 NormalAt(float u, WarpState s)
        {
            Vector3 a = ToWorld(u, 0, 0, s);
            Vector3 b = ToWorld(u, 0, 0.05f, s);
            Vector3 n = b - a;
            n.y = 0;
            return n.sqrMagnitude > 1e-12f ? n.normalized : FocusNormal(s);
        }

        /// <summary>
        /// Approximate inverse of the current warp: the arc and relevance under a world position (xz),
        /// found by searching along the path. Used for picking and the lens.
        /// </summary>
        public static void Inverse(Vector3 world, out float u, out float rho)
        {
            WarpState s = Current;
            Derived d = CurrentDerived();
            Vector2 q = new Vector2(world.x, world.z);
            float best = float.MaxValue, bestU = 0.5f;
            const int n = 800;
            for (int i = 0; i <= n; i++)
            {
                float ui = Mathf.Lerp(DeepTime.NowArc, 1f, i / (float)n);
                Vector3 p = ToWorld(ui, 0, 0, d, s);
                float dd = (new Vector2(p.x, p.z) - q).sqrMagnitude;
                if (dd < best)
                {
                    best = dd;
                    bestU = ui;
                }
            }

            // refine with a ternary search around the best sample
            float lo = Mathf.Max(DeepTime.NowArc, bestU - 1f / n), hi = Mathf.Min(1f, bestU + 1f / n);
            for (int it = 0; it < 40; it++)
            {
                float m1 = lo + (hi - lo) / 3f, m2 = hi - (hi - lo) / 3f;
                Vector3 p1 = ToWorld(m1, 0, 0, d, s), p2 = ToWorld(m2, 0, 0, d, s);
                if ((new Vector2(p1.x, p1.z) - q).sqrMagnitude < (new Vector2(p2.x, p2.z) - q).sqrMagnitude) hi = m2;
                else lo = m1;
            }

            u = 0.5f * (lo + hi);
            Vector3 b = ToWorld(u, 0, 0, d, s);
            Vector3 nrm = NormalAt(u, s);
            rho = Vector3.Dot(world - b, nrm) / Mathf.Max(s.RhoScale, 1e-4f);
        }
    }
}

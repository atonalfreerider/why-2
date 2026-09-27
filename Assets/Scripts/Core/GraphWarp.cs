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
        /// <summary>Focus arc: the point of the ring that stays fixed while unrolling.</summary>
        public float FocusArc;

        /// <summary>0 = polar clock, 1 = the focused period is a straight timeline.</summary>
        public float Unroll;

        /// <summary>Log offset in years: small = logarithmic unrolled time, large = linear.</summary>
        public double LogOffset;

        /// <summary>Years ago at the center of the unrolled window.</summary>
        public double FocusYearsAgo;

        /// <summary>World units per ln unit of (yearsAgo + LogOffset) in the unrolled layout.</summary>
        public float KLin;

        public float RhoScale;
        public float YScale;

        /// <summary>Half length (world units) of the unrolled window; content beyond fades.</summary>
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
            // Unrolled-space parameters are irrelevant while the ring is polar; snap them so the unroll
            // animation straightens directly into the target window instead of sweeping through others.
            if (a.Unroll < 1e-3f)
            {
                a.LogOffset = b.LogOffset;
                a.FocusYearsAgo = b.FocusYearsAgo;
                a.KLin = b.KLin;
            }

            if (b.Unroll < 1e-3f)
            {
                b.LogOffset = a.LogOffset;
                b.FocusYearsAgo = a.FocusYearsAgo;
                b.KLin = a.KLin;
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
    /// </summary>
    public static class GraphWarp
    {
        static readonly int WarpA = Shader.PropertyToID("_WhyWarpA");
        static readonly int WarpB = Shader.PropertyToID("_WhyWarpB");
        static readonly int WarpC = Shader.PropertyToID("_WhyWarpC");
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

        public static void Push()
        {
            WarpState s = Current;
            double c = Math.Max(s.LogOffset, 1e-9);
            float lnYaF = (float)Math.Log(Math.Max(s.FocusYearsAgo, 0) + c);
            Shader.SetGlobalVector(WarpA, new Vector4(s.FocusArc, GraphStyle.R0, Mathf.Clamp01(s.Unroll), s.YScale));
            Shader.SetGlobalVector(WarpB, new Vector4((float)c, lnYaF, s.KLin, s.RhoScale));
            Shader.SetGlobalVector(WarpC, new Vector4(s.FadeHalfLength, Mathf.Max(s.FadeSoftness, 1e-4f), s.FadeAmount, 0));
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

        /// <summary>Unrolled time coordinate (world units from the focus) - also drives the focus fade.</summary>
        public static float UnrolledS(float u, WarpState s)
        {
            float c = (float)Math.Max(s.LogOffset, 1e-9);
            float lnYaF = (float)Math.Log(Math.Max(s.FocusYearsAgo, 0) + c);
            return s.KLin * (Mathf.Log(ShaderYearsAgo(u) + c) - lnYaF);
        }

        public static Vector3 ToWorld(float u, float y, float rho) => ToWorld(u, y, rho, Current);

        public static Vector3 ToWorld(Vector3 data) => ToWorld(data.x, data.y, data.z, Current);

        /// <summary>Exact mirror of WhyToWorld in WhyCommon.hlsl.</summary>
        public static Vector3 ToWorld(float u, float y, float rho, WarpState s)
        {
            float r0 = GraphStyle.R0;
            float unroll = Mathf.Clamp01(s.Unroll);
            float sPolar = r0 * 2f * Mathf.PI * (u - s.FocusArc);
            float sLin = UnrolledS(u, s);
            float arc = Mathf.Lerp(sPolar, sLin, unroll);
            float R = r0 / Mathf.Max(1f - unroll, 1e-3f);
            float dphi = arc / R;
            float rhoW = rho * s.RhoScale;

            float thF = 2f * Mathf.PI * s.FocusArc;
            Vector2 n = new Vector2(Mathf.Sin(thF), -Mathf.Cos(thF));
            Vector2 tan = new Vector2(Mathf.Cos(thF), Mathf.Sin(thF));

            float sh = Mathf.Sin(dphi * 0.5f);
            float along = (R + rhoW) * Mathf.Sin(dphi);
            float outward = rhoW * Mathf.Cos(dphi) - 2f * R * sh * sh;
            Vector2 p = r0 * n + tan * along + n * outward;
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

        /// <summary>Outward normal of the ring (world xz) at the focus.</summary>
        public static Vector3 FocusNormal(WarpState s)
        {
            float thF = 2f * Mathf.PI * s.FocusArc;
            return new Vector3(Mathf.Sin(thF), 0, -Mathf.Cos(thF));
        }

        /// <summary>Tangent of the ring (world xz) at the focus, pointing into the past.</summary>
        public static Vector3 FocusTangent(WarpState s)
        {
            float thF = 2f * Mathf.PI * s.FocusArc;
            return new Vector3(Mathf.Cos(thF), 0, Mathf.Sin(thF));
        }
    }
}

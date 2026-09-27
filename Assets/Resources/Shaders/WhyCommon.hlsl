#ifndef WHY_COMMON_INCLUDED
#define WHY_COMMON_INCLUDED

// Global warp parameters (set by GraphWarp.Push). See Docs/ARCHITECTURE.md.
//
// The base path is a circle of radius R0 from the Big Bang (6 o'clock) clockwise to 3 o'clock, where
// the human era leaves the circle on a straight tangent line with near-linear time up to the present.
// A lens (unroll) straightens the path around a focus and re-maps time to a window.
float4 _WhyWarpA;   // sigma at focus, R0, time remap (base -> lens, trails unroll), yScale
float4 _WhyWarpB;   // lens log offset C, ln(yaF + C), kLin, rhoScale
float4 _WhyWarpC;   // fade half length, fade softness, fade amount, s at the circle/line junction
float4 _WhyJ;       // junction frame: position.xz, tangent (toward the present).xz
float4 _WhyBaseA;   // handoff arc uH, sigma per unit u on the circle, sigma at the handoff, sigma per ln unit on the line
float4 _WhyBaseB;   // line log offset CH, ln(yaH + CH), bend radius R', arc where life/matter start to fade
float _WhyLnAge;    // ln(age of universe in years)

// Highlighter: up to 8 inclusive id ranges (min, max, glow, attention) and a dim factor applied to
// everything outside the attention ranges (persistent glows have attention = 0 and still dim).
float4 _WhyHi[8];
float _WhyHiCount;
float _WhyDim;
float _WhyTime;

#define WHY_TWO_PI 6.28318530718

float WhyYearsAgo(float u)
{
    u = max(u, 1e-4);
    float a = -1.4 - 2.39 * u;
    float g = pow(u, a) * log(u);
    return exp(g + _WhyLnAge);
}

// Arc length along the base path from the Big Bang (circle, then the straight human branch).
float WhyBaseSigma(float u, float ya)
{
    if (u >= _WhyBaseA.x) return (1.0 - u) * _WhyBaseA.y;
    return _WhyBaseA.z + _WhyBaseA.w * (_WhyBaseB.y - log(ya + _WhyBaseB.x));
}

// Lens time coordinate (world units from the focus, positive toward the present).
float WhyUnrolledS(float u)
{
    return _WhyWarpB.z * (_WhyWarpB.y - log(WhyYearsAgo(u) + _WhyWarpB.x));
}

// data = (u, y, rho) -> world position. Mirrors GraphWarp.ToWorld.
float3 WhyToWorld(float3 data, out float sLin)
{
    float u = max(data.x, 1e-4);
    float ya = WhyYearsAgo(u);
    sLin = _WhyWarpB.z * (_WhyWarpB.y - log(ya + _WhyWarpB.x));
    float s = lerp(WhyBaseSigma(u, ya) - _WhyWarpA.x, sLin, saturate(_WhyWarpA.z));

    float sH = _WhyWarpC.w;
    float2 pj = _WhyJ.xy;
    float2 tj = _WhyJ.zw;
    float2 nj = float2(-tj.y, tj.x);
    float rhoW = data.z * _WhyWarpB.w;

    float2 p;
    if (s >= sH)
    {
        // straight human branch
        p = pj + tj * (s - sH) + nj * rhoW;
    }
    else
    {
        // circle (radius grows toward infinity as the lens unrolls)
        float R = _WhyBaseB.z;
        float phi = (s - sH) / R;
        float sh = sin(phi * 0.5);
        float2 c = pj + tj * (R * sin(phi)) - nj * (2.0 * R * sh * sh);
        float2 n = nj * cos(phi) + tj * sin(phi);
        p = c + n * rhoW;
    }

    return float3(p.x, data.y * _WhyWarpA.w, p.y);
}

float3 WhyToWorld(float3 data)
{
    float sLin;
    return WhyToWorld(data, sLin);
}

// 1 inside the lens window, fading outside it.
float WhyFocusFade(float sLin)
{
    float d = abs(sLin);
    float k = saturate((d - _WhyWarpC.x) / max(_WhyWarpC.y, 1e-4));
    k = k * k * (3.0 - 2.0 * k);
    return 1.0 - _WhyWarpC.z * k;
}

// Life and matter dissolve before the human branch (1 before the fade, 0 at the handoff).
float WhyHandoffFade(float u)
{
    float t = saturate((u - _WhyBaseA.x) / max(_WhyBaseB.w - _WhyBaseA.x, 1e-5));
    return t * t * (3.0 - 2.0 * t);
}

// Returns glow multiplier (>= 1) for highlighted ids, and an alpha multiplier for the rest.
void WhyHighlight(float id, out float glow, out float alphaMul)
{
    glow = 1.0;
    bool hit = false;
    int count = (int)_WhyHiCount;
    [loop] for (int i = 0; i < 8; i++)
    {
        if (i >= count) break;
        float4 h = _WhyHi[i];
        if (id >= h.x - 0.5 && id <= h.y + 0.5)
        {
            glow = max(glow, h.z);
            hit = hit || h.w > 0.5;
        }
    }
    alphaMul = hit ? 1.0 : (1.0 - _WhyDim);
}

// Cheap value noise / fbm for the dissipating matter layer.
float WhyHash(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float WhyNoise(float3 x)
{
    float3 i = floor(x);
    float3 f = frac(x);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(lerp(WhyHash(i + float3(0, 0, 0)), WhyHash(i + float3(1, 0, 0)), f.x),
                     lerp(WhyHash(i + float3(0, 1, 0)), WhyHash(i + float3(1, 1, 0)), f.x), f.y),
                lerp(lerp(WhyHash(i + float3(0, 0, 1)), WhyHash(i + float3(1, 0, 1)), f.x),
                     lerp(WhyHash(i + float3(0, 1, 1)), WhyHash(i + float3(1, 1, 1)), f.x), f.y), f.z);
}

float WhyFbm(float3 p)
{
    float v = 0.0;
    float a = 0.5;
    [unroll] for (int i = 0; i < 4; i++)
    {
        v += a * WhyNoise(p);
        p = p * 2.03 + 17.1;
        a *= 0.5;
    }
    return v;
}

#endif

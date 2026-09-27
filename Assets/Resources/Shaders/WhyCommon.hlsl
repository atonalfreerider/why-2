#ifndef WHY_COMMON_INCLUDED
#define WHY_COMMON_INCLUDED

// Global warp parameters (set by GraphWarp.Push). See Docs/ARCHITECTURE.md.
float4 _WhyWarpA;   // focusArc, R0, unroll, yScale
float4 _WhyWarpB;   // logOffset C, ln(yaF + C), kLin, rhoScale
float4 _WhyWarpC;   // fade half length, fade softness, fade amount, -
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

float WhyUnrolledS(float u)
{
    return _WhyWarpB.z * (log(WhyYearsAgo(u) + _WhyWarpB.x) - _WhyWarpB.y);
}

// data = (u, y, rho) -> world position. Mirrors GraphWarp.ToWorld.
float3 WhyToWorld(float3 data, out float sLin)
{
    float uF = _WhyWarpA.x;
    float r0 = _WhyWarpA.y;
    float unroll = saturate(_WhyWarpA.z);

    float sPolar = r0 * WHY_TWO_PI * (data.x - uF);
    sLin = WhyUnrolledS(data.x);
    float arc = lerp(sPolar, sLin, unroll);
    float R = r0 / max(1.0 - unroll, 1e-3);
    float dphi = arc / R;
    float rhoW = data.z * _WhyWarpB.w;

    float thF = WHY_TWO_PI * uF;
    float2 n = float2(sin(thF), -cos(thF));
    float2 t = float2(cos(thF), sin(thF));

    float sh = sin(dphi * 0.5);
    float along = (R + rhoW) * sin(dphi);
    float outward = rhoW * cos(dphi) - 2.0 * R * sh * sh;
    float2 p = r0 * n + t * along + n * outward;
    return float3(p.x, data.y * _WhyWarpA.w, p.y);
}

float3 WhyToWorld(float3 data)
{
    float sLin;
    return WhyToWorld(data, sLin);
}

// 1 inside the unrolled focus window, fading outside it.
float WhyFocusFade(float sLin)
{
    float d = abs(sLin);
    float k = saturate((d - _WhyWarpC.x) / max(_WhyWarpC.y, 1e-4));
    k = k * k * (3.0 - 2.0 * k);
    return 1.0 - _WhyWarpC.z * k;
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

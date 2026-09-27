// Screen-space expanded polylines in graph data space (see Docs/ARCHITECTURE.md, LineMeshBuilder).
Shader "Why/Line"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 1
        _WidthScale ("Width Scale", Float) = 1
        _Flow ("Causal Flow Pulse", Float) = 0
        _FlowFreq ("Flow Frequency (per arc)", Float) = 60
        _FlowSpeed ("Flow Speed", Float) = 0.35
        _RhoFade ("Radial Fade Distance (0 = off)", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "WhyLine"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "WhyCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Alpha;
                float _WidthScale;
                float _Flow;
                float _FlowFreq;
                float _FlowSpeed;
                float _RhoFade;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            struct Attributes
            {
                float3 pos : POSITION;
                float3 prev : TEXCOORD0;
                float3 next : TEXCOORD1;
                float4 p : TEXCOORD2;   // side, width px, width world, id
                float2 q : TEXCOORD3;   // intensity, flow
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 posCS : SV_POSITION;
                float4 color : COLOR;
                float3 edge : TEXCOORD0;   // signed px across, half width px, arc u
                float flow : TEXCOORD1;
            };

            // Pull a neighbor that is behind the camera onto the near side along the segment.
            float4 ClipNeighbor(float4 c, float4 n)
            {
                const float eps = 1e-3;
                if (n.w < eps && c.w > eps)
                {
                    float k = (c.w - eps) / max(c.w - n.w, 1e-6);
                    n = lerp(c, n, k);
                }
                return n;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float sLin;
                float3 w = WhyToWorld(IN.pos, sLin);
                float4 c = TransformWorldToHClip(w);
                float4 cp = ClipNeighbor(c, TransformWorldToHClip(WhyToWorld(IN.prev)));
                float4 cn = ClipNeighbor(c, TransformWorldToHClip(WhyToWorld(IN.next)));

                if (c.w < 1e-3)
                {
                    // behind the camera: collapse outside the clip volume
                    OUT.posCS = float4(0, 0, -2, 1);
                    OUT.color = 0;
                    OUT.edge = 0;
                    OUT.flow = 0;
                    return OUT;
                }

                float2 scr = _ScreenParams.xy;
                float2 s = c.xy / c.w * 0.5 * scr;
                float2 sp = cp.xy / max(cp.w, 1e-3) * 0.5 * scr;
                float2 sn = cn.xy / max(cn.w, 1e-3) * 0.5 * scr;

                float2 d1 = s - sp;
                float2 d2 = sn - s;
                float l1 = length(d1);
                float l2 = length(d2);
                bool has1 = l1 > 1e-4;
                bool has2 = l2 > 1e-4;
                d1 = has1 ? d1 / l1 : float2(0, 0);
                d2 = has2 ? d2 / l2 : float2(0, 0);
                if (!has1) d1 = d2;
                if (!has2) d2 = d1;

                float2 tng = d1 + d2;
                float tl = length(tng);
                tng = tl > 1e-4 ? tng / tl : d1;
                float2 nrm = float2(-tng.y, tng.x);
                float miter = 1.0 / max(dot(nrm, float2(-d1.y, d1.x)), 0.35);
                if (!has1 && !has2)
                {
                    nrm = float2(0, 1);
                    miter = 1;
                }

                // width = constant pixels + world units projected to pixels
                float pxPerUnit = abs(UNITY_MATRIX_P[1][1]) * 0.5 * scr.y / c.w;
                float width = (IN.p.y + IN.p.z * pxPerUnit) * _WidthScale;
                float subPixel = saturate(width);
                width = max(width, 1.0);
                float halfW = width * 0.5 + 1.0; // 1px padding for anti-aliasing

                float2 off = nrm * IN.p.x * halfW * miter;
                c.xy += off * 2.0 / scr * c.w;
                OUT.posCS = c;

                float glow, alphaMul;
                WhyHighlight(IN.p.w, glow, alphaMul);
                float rhoFade = _RhoFade > 0 ? exp(-max(IN.pos.z, 0) / _RhoFade) : 1.0;
                float a = IN.color.a * _Alpha * _Color.a * WhyFocusFade(sLin) * alphaMul * subPixel * rhoFade;
                OUT.color = float4(IN.color.rgb * _Color.rgb * IN.q.x * glow, a);
                OUT.edge = float3(IN.p.x * halfW * miter, width * 0.5 * miter, IN.pos.x);
                OUT.flow = IN.q.y * _Flow;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float d = abs(IN.edge.x);
                float coverage = saturate(IN.edge.y + 0.5 - d);
                float3 rgb = IN.color.rgb;
                if (IN.flow > 0)
                {
                    // pulses travel with time: from larger u (past) toward u = 0 (present)
                    float ph = frac(IN.edge.z * _FlowFreq + _WhyTime * _FlowSpeed);
                    float pulse = pow(ph, 24.0);
                    rgb *= 1.0 + IN.flow * pulse * 3.0;
                }
                return half4(rgb, IN.color.a * coverage);
            }
            ENDHLSL
        }
    }
}

// Filled bands in graph data space (see Docs/ARCHITECTURE.md, SurfaceMeshBuilder).
Shader "Why/Surface"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 1
        _RhoFade ("Radial Fade Distance (0 = off)", Float) = 0
        _HandoffFade ("Fade Out Before The Human Branch", Float) = 0
        _EdgeSoft ("Edge Softness (across band)", Range(0, 0.5)) = 0.15
        _NoiseScale ("Noise Scale", Float) = 3
        _NoiseContrast ("Noise Contrast", Float) = 1.6
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
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
            Name "WhySurface"
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
                float _RhoFade;
                float _HandoffFade;
                float _EdgeSoft;
                float _NoiseScale;
                float _NoiseContrast;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            struct Attributes
            {
                float3 pos : POSITION;
                float4 p : TEXCOORD0;   // id, intensity, across (0..1), noise amount
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 posCS : SV_POSITION;
                float4 color : COLOR;
                float3 world : TEXCOORD0;
                float2 band : TEXCOORD1;   // across, noise amount
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float sLin;
                float3 w = WhyToWorld(IN.pos, sLin);
                OUT.posCS = TransformWorldToHClip(w);
                OUT.world = w;

                float glow, alphaMul;
                WhyHighlight(IN.p.x, glow, alphaMul);
                float rhoFade = _RhoFade > 0 ? exp(-max(IN.pos.z, 0) / _RhoFade) : 1.0;
                float a = IN.color.a * _Alpha * _Color.a * WhyFocusFade(sLin) * alphaMul * rhoFade;
                a *= lerp(1.0, WhyHandoffFade(IN.pos.x), _HandoffFade);
                OUT.color = float4(IN.color.rgb * _Color.rgb * IN.p.y * glow, a);
                OUT.band = float2(IN.p.z, IN.p.w);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float a = IN.color.a;
                if (_EdgeSoft > 0)
                {
                    float across = IN.band.x;
                    a *= smoothstep(0.0, _EdgeSoft, across) * smoothstep(0.0, _EdgeSoft, 1.0 - across) * 0.6 + 0.4;
                }

                if (IN.band.y > 0)
                {
                    float3 q = float3(IN.world.xz * _NoiseScale, _WhyTime * 0.03);
                    float n = WhyFbm(q);
                    n = saturate((n - 0.5) * _NoiseContrast + 0.5);
                    a *= lerp(1.0, n * n * 1.8, IN.band.y);
                }

                return half4(IN.color.rgb, a);
            }
            ENDHLSL
        }
    }
}

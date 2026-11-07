// Filled bands in graph data space (see Docs/ARCHITECTURE.md, SurfaceMeshBuilder).
Shader "Why/Surface"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 1
        _RhoFade ("Radial Fade Distance (0 = off)", Float) = 0
        _HandoffFade ("Fade Out Before The Human Branch", Float) = 0
        _Raw ("Object Space (no warp)", Float) = 0
        _EdgeSoft ("Edge Softness (across band)", Range(0, 0.5)) = 0.15
        _NoiseScale ("Noise Scale", Float) = 3
        _NoiseContrast ("Noise Contrast", Float) = 1.6
        _Hatch ("Hatch: period px, duty, strength (strength 0 = off)", Vector) = (0, 0, 0, 0)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
        // solid geometry that must hide what is behind it (the present's blocks); every band keeps the default, off
        [Enum(Off, 0, On, 1)] _ZWrite ("Depth Write", Float) = 0
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
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "WhyCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Alpha;
                float _RhoFade;
                float _HandoffFade;
                float _Raw;
                float _EdgeSoft;
                float _NoiseScale;
                float _NoiseContrast;
                float4 _Hatch;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            struct Attributes
            {
                float3 pos : POSITION;
                float4 p : TEXCOORD0;   // id, intensity, across (0..1), noise amount
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 posCS : SV_POSITION;
                float4 color : COLOR;
                float3 world : TEXCOORD0;
                float2 band : TEXCOORD1;   // across, noise amount
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float sLin;
                float3 w = WhyPlace(IN.pos, _Raw, sLin);
                OUT.posCS = TransformWorldToHClip(w);
                OUT.world = w;

                float glow, alphaMul;
                WhyHighlight(IN.p.x, glow, alphaMul);
                float rhoFade = _RhoFade > 0 ? exp(-max(IN.pos.z, 0) / _RhoFade) : 1.0;
                float a = IN.color.a * _Alpha * _Color.a * WhyFocusFade(sLin) * alphaMul * rhoFade;
                a *= lerp(1.0, WhyHandoffFade(IN.pos.x), _HandoffFade);
                float revealGlow;
                a *= WhyReveal(IN.pos.x, IN.pos.z, IN.p.x, _Raw, revealGlow);
                OUT.color = float4(IN.color.rgb * _Color.rgb * IN.p.y * glow * revealGlow, a);
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

                // owed, the mark code's hatch (spec 3.3): stripes at 45 degrees on the screen, one every _Hatch.x pixels,
                // _Hatch.y of each period drawn and the gaps faded by _Hatch.z (1: empty), anti-aliased over a pixel. At
                // strength 0, every material's default, the surface is drawn as it always was.
                if (_Hatch.z > 0 && _Hatch.x > 0)
                {
                    float across = (IN.posCS.x + IN.posCS.y) * 0.70710678 / _Hatch.x;   // periods across the stripes
                    float fromStripe = min(frac(across), 1.0 - frac(across)) * _Hatch.x; // pixels from a stripe's middle
                    float stripe = _Hatch.y >= 1.0 ? 1.0 : saturate(0.5 * _Hatch.y * _Hatch.x - fromStripe + 0.5);
                    a *= lerp(1.0, stripe, saturate(_Hatch.z));
                }

                return half4(IN.color.rgb, a);
            }
            ENDHLSL
        }
    }
}

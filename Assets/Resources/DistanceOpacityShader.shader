Shader "Custom/DistanceOpacityShader" {
    Properties {
        _Color ("Color", Color) = (1,1,1,1)
        _Center ("Center Point", Vector) = (0,0,-2,0)
        _MaxDistance ("Max Distance", Float) = 10.0
    }
    SubShader {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        LOD 100

        CGINCLUDE
        #include "UnityCG.cginc"
        float4 _Center;
        float _MaxDistance;
        ENDCG

        // First pass: Find minimum distance
        Pass {
            ZWrite On
            ColorMask 0

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct v2f {
                float4 vertex : SV_POSITION;
                float dist : TEXCOORD0;
            };

            v2f vert (appdata_base v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.dist = length(worldPos - _Center.xyz);
                return o;
            }

            float4 frag (v2f i) : SV_Target {
                return i.dist;
            }
            ENDCG
        }

        // Second pass: Render with gradient
        Pass {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            fixed4 _Color;

            struct v2f {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            v2f vert (appdata_base v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            sampler2D_float _CameraDepthTexture;

            fixed4 frag (v2f i) : SV_Target {
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float minDist = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUV);
                
                float currentDist = length(i.worldPos - _Center.xyz);
                float normalizedDist = saturate((currentDist - minDist) / _MaxDistance);
                
                fixed4 col = _Color;
                col.a *= (1 - normalizedDist);
                return col;
            }
            ENDCG
        }
    }
    FallBack "Transparent/VertexLit"
}
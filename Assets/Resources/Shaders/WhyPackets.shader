// Things that move along paths - money, goods, capital, people, promises - as packets marching on the flow clock (see
// PacketMeshBuilder, FlowClock and Docs/ARCHITECTURE.md). Lanes are expanded on screen as Why/Line expands its polylines;
// the fragment draws the packet nearest each pixel, placed from the lane's distance and the clock alone.
Shader "Why/Packets"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 1
        _RhoFade ("Radial Fade Distance (0 = off)", Float) = 0
        _HandoffFade ("Fade Out Before The Human Branch", Float) = 0
        _Raw ("Object Space (no warp)", Float) = 0
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
            Name "WhyPackets"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
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
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            struct Attributes
            {
                float3 pos : POSITION;
                float3 prev : TEXCOORD0;
                float3 next : TEXCOORD1;
                float4 p : TEXCOORD2;      // side, size px, size world, id
                float3 q : TEXCOORD3;      // intensity, kind (PacketKind), offset px to the right of travel
                float4 lane : TEXCOORD4;   // cycles from the source, cycles to the sink, packets a second, seed
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 posCS : SV_POSITION;
                float4 color : COLOR;
                float3 edge : TEXCOORD0;   // signed px across, packet radius px, kind
                float4 lane : TEXCOORD1;   // as Attributes.lane
                UNITY_VERTEX_OUTPUT_STEREO
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
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float sLin;
                float3 w = WhyPlace(IN.pos, _Raw, sLin);
                float4 c = TransformWorldToHClip(w);
                float4 cp = ClipNeighbor(c, TransformWorldToHClip(WhyPlace(IN.prev, _Raw)));
                float4 cn = ClipNeighbor(c, TransformWorldToHClip(WhyPlace(IN.next, _Raw)));

                if (c.w < 1e-3)
                {
                    // behind the camera: collapse outside the clip volume
                    OUT.posCS = float4(0, 0, -2, 1);
                    OUT.color = 0;
                    OUT.edge = 0;
                    OUT.lane = 0;
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

                // size = constant pixels + world units projected to pixels, as Why/Line's width
                float pxPerUnit = abs(UNITY_MATRIX_P[1][1]) * 0.5 * scr.y / c.w;
                float size = IN.p.y + IN.p.z * pxPerUnit;
                float subPixel = saturate(size);
                size = max(size, 1.0);
                float halfW = size * 0.5 + 1.0; // 1px padding for anti-aliasing

                // the vertices run in the direction of travel, so nrm is the lane's left on screen (mirrored by a flipped
                // projection, which _ProjectionParams.x undoes): the lane keeps to its right by its offset, and the two
                // directions of a two-way lane pass each other
                float right = IN.q.z * _ProjectionParams.x;
                float2 off = nrm * (IN.p.x * halfW - right) * miter;
                c.xy += off * 2.0 / scr * c.w;
                OUT.posCS = c;

                float glow, alphaMul;
                WhyHighlight(IN.p.w, glow, alphaMul);
                float rhoFade = _RhoFade > 0 ? exp(-max(IN.pos.z, 0) / _RhoFade) : 1.0;
                float a = IN.color.a * _Alpha * _Color.a * WhyFocusFade(sLin) * alphaMul * subPixel * rhoFade;
                a *= lerp(1.0, WhyHandoffFade(IN.pos.x), _HandoffFade);
                float revealGlow;
                a *= WhyReveal(IN.pos.x, IN.pos.z, IN.p.w, _Raw, revealGlow);
                OUT.color = float4(IN.color.rgb * _Color.rgb * IN.q.x * glow * revealGlow, a);
                OUT.edge = float3(IN.p.x * halfW, size * 0.5, IN.q.y);
                OUT.lane = IN.lane;
                return OUT;
            }

            // seconds a packet takes to fade in as it leaves its source, and out as it nears its sink
            #define PacketFadeSeconds 0.4

            // Signed distance (px) from p to a box of the given half extents around the origin: negative inside.
            float BoxDistance(float2 p, float2 extent)
            {
                float2 q = abs(p) - extent;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);
            }

            // How much of the pixel at p (px from the packet's centre: along the lane, across it) a packet of radius r px covers,
            // anti-aliased over a pixel: one shape a kind (PacketKind).
            float PacketCover(float kind, float2 p, float r)
            {
                // money: a round dot
                if (kind < 0.5) return saturate(r + 0.5 - length(p));
                // goods: a square of the dot's area, a crate
                if (kind < 1.5) return saturate(0.5 - BoxDistance(p, float2(r * 0.886, r * 0.886)));
                // capital: an ingot, a dot drawn out along the lane
                if (kind < 2.5) return saturate(r * 0.7 + 0.5 - length(float2(max(abs(p.x) - r * 0.8, 0.0), p.y)));
                // people: a soft dot with no edge
                if (kind < 3.5)
                {
                    float t = saturate(1.0 - length(p) / r);
                    return t * t * (3.0 - 2.0 * t);
                }
                // a promise: a thin dash, at least a pixel thick
                return saturate(0.5 - BoxDistance(p, float2(r * 1.4, max(r * 0.3, 0.5))));
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // the lane's packets stand at the middle of each cycle and the pattern moves rate cycles a second from the source
                // toward the sink (WhyFlowCycles keeps it unbroken when the clock wraps): a packet's place is a function of
                // distance and clock alone
                float fromSource = IN.lane.x;
                float ahead = frac(fromSource + IN.lane.w - WhyFlowCycles(IN.lane.z)) - 0.5;   // cycles past the nearest packet's centre
                // cycles per pixel along the lane, taken outside any branch (a derivative needs every pixel of its quad)
                float perPx = max(length(float2(ddx(fromSource), ddy(fromSource))), 1e-6);
                float r = IN.edge.y;
                float cover = PacketCover(IN.edge.z, float2(ahead / perPx, IN.edge.x), r);

                // the packet fades in once its far end is clear of the source and out before its far end reaches the sink (an
                // ingot reaches 1.5 r along the lane from its centre, the longest shape), over PacketFadeSeconds or a quarter of
                // the lane's crossing if that is shorter, so none pops into being or is cut by the lane's ends
                float rate = max(IN.lane.z, 1e-4);
                float reach = 1.5 * r * perPx;                                  // cycles
                float fade = max(min(PacketFadeSeconds, 0.25 * (IN.lane.x + IN.lane.y) / rate), 1e-3);
                float sinceSource = (fromSource - ahead - reach) / rate;        // seconds since it was clear of its source
                float untilSink = (IN.lane.y + ahead - reach) / rate;           // seconds until it touches its sink
                cover *= saturate(sinceSource / fade) * saturate(untilSink / fade);

                return half4(IN.color.rgb, IN.color.a * cover);
            }
            ENDHLSL
        }
    }
}

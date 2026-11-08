// The scale of space in the red layer (ExpansionGridLayer): one fan in graph data space whose waves are drawn here, per
// pixel - space 10x inside 10x inside 10x, each level opened by its spacing on screen, by the rule SpaceLod.cs mirrors
// (see Docs/ARCHITECTURE.md for the warp, Docs/DATA.md for the field).
Shader "Why/ScaleWaves"
{
    Properties
    {
        [HDR] _Color ("Line Color (alpha: the lines' opacity)", Color) = (0.6, 0.555, 0.495, 0.3)
        _NodeColor ("Node Color", Color) = (1, 0.42, 0.1, 1)
        [HDR] _CrestColor ("Crest Color (alpha: the crests' opacity)", Color) = (1, 0.62, 0.32, 0.55)
        _Alpha ("Alpha", Range(0, 1)) = 1
        _HandoffFade ("Fade Out Before The Human Branch", Float) = 0
        _Id ("Highlight Id", Float) = 0
        _Open ("Emerge px, open px, levels drawn, -", Vector) = (3, 7.4, 5, 0)
        _Weights ("Weight of levels 0..3", Vector) = (1, 0.6, 0.3, 0.16)
        _Floors ("Floor of levels 0..3 once the next has opened", Vector) = (0.35, 0.2, 0.12, 0.12)
        _Fine ("Weight of level 4, -, -, -", Vector) = (0.09, 0, 0, 0)
        _Range ("Least and greatest decade drawn (log10 m today), -, -", Vector) = (3, 26.64, 0, 0)
        _Fades ("Inner decades, horizon from (share of the fan), bound decades, -", Vector) = (0.4, 0.62, 0.3, 0)
        _Width ("Width: px, per px of gap, most world units, most px", Vector) = (1, 0.03, 0.03, 3)
        _Node ("Node: glow (HDR), most width (ln of the spacing), half length (px), -", Vector) = (3, 0.25, 5, 0)
        _Pulse ("Swell: strength, decades per second, half width (decades), lead below the trunk", Vector) = (0.25, 2, 0.8, 1.5)
        _WaveTime ("Swell clock (s)", Float) = 0
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
            Name "WhyScaleWaves"
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
                float4 _NodeColor;
                float4 _CrestColor;
                float _Alpha;
                float _HandoffFade;
                float _Id;
                float4 _Open;
                float4 _Weights;
                float4 _Floors;
                float4 _Fine;
                float4 _Range;
                float4 _Fades;
                float4 _Width;
                float4 _Node;
                float4 _Pulse;
                float _WaveTime;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            struct Attributes
            {
                float3 pos : POSITION;    // u, y, rho
                float4 color : COLOR;     // a: the dissolve ahead of the human branch
                float4 wave : TEXCOORD0;  // decades today above the strip's base, decades above the trunk, rho over the fan's width, decades free of a body's edge
                float2 strip : TEXCOORD1; // the strip's base decade (an integer), world units per decade at this moment
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 posCS : SV_POSITION;
                float4 wave : TEXCOORD0;
                float4 misc : TEXCOORD1;  // the strip's base decade, px per world unit, alpha, glow
                float decade : TEXCOORD2; // ln of the px a decade spans outward on screen (the nodes' measure)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float sLin;
                float3 w = WhyToWorld(IN.pos, sLin);
                OUT.posCS = TransformWorldToHClip(w);

                float glow, alphaMul;
                WhyHighlight(_Id, glow, alphaMul);
                float a = IN.color.a * _Alpha * WhyFocusFade(sLin) * alphaMul;
                a *= lerp(1.0, WhyHandoffFade(IN.pos.x), _HandoffFade);
                float revealGlow;
                a *= WhyReveal(IN.pos.x, IN.pos.z, _Id, 0.0, revealGlow);

                // pixels per world unit at the vertex's depth: caps a line's world width, as Why/Line widens its lines
                float pxPerUnit = abs(UNITY_MATRIX_P[1][1]) * 0.5 * _ScreenParams.y / max(OUT.posCS.w, 1e-4);

                // the px a decade spans outward here, from a step outward a hundredth of a decade long: smooth across the fan,
                // where the wave coordinate's derivatives are flat over each triangle, and blind to a crest's tilt, so a node
                // marks where a line opens as the comb widens, not where a collapse briefly crowds the crests
                float upd = max(IN.strip.y, 1e-6);
                float outward = max(0.01 * upd, 1e-4);
                float4 out2 = TransformWorldToHClip(WhyToWorld(IN.pos + float3(0.0, 0.0, outward)));
                float2 halfScreen = 0.5 * _ScreenParams.xy;
                float2 s1 = OUT.posCS.xy / max(OUT.posCS.w, 1e-5) * halfScreen;
                float2 s2 = out2.xy / max(out2.w, 1e-5) * halfScreen;
                float decadePx = (OUT.posCS.w > 1e-4 && out2.w > 1e-4) ? length(s2 - s1) / outward * upd : 1e-6;

                OUT.wave = IN.wave;
                OUT.misc = float4(IN.strip.x, pxPerUnit, a, glow * revealGlow);
                OUT.decade = log(max(decadePx, 1e-6));
                return OUT;
            }

            // the narrowest a node's band of spacing gets (natural log): a line whose spacing changes slower than this over a
            // node's half length glows dimmer, in proportion
            #define NodeLeast 0.004

            float WaveSmooth(float t)
            {
                t = saturate(t);
                return t * t * (3.0 - 2.0 * t);
            }

            // 0 while a gap on screen is under the emerge spacing, 1 from the open spacing, smooth in log between (SpaceLod.Emerge)
            float Emerge(float px)
            {
                return WaveSmooth(log(max(px, 1e-6) / _Open.x) / log(_Open.y / _Open.x));
            }

            float LevelWeight(int level)
            {
                return level == 0 ? _Weights.x : level == 1 ? _Weights.y : level == 2 ? _Weights.z : level == 3 ? _Weights.w : _Fine.x;
            }

            float LevelFloor(int level)
            {
                return level == 0 ? _Floors.x : level == 1 ? _Floors.y : level == 2 ? _Floors.z : _Floors.w;
            }

            // One line, d pixels from the fragment: its gap to the next line of its level or coarser and the gap the next level
            // opens beside it (decades; 0 for the finest level drawn), read at g decades a pixel (SpaceLod.Alpha); its width
            // (a pixel, wider as its gap opens, at most some world units and some pixels); and its node, the glint where its gap
            // outward (at decadePx a decade) passes the open spacing, nodeWidth the natural log of the gap it spans.
            void AddLine(int level, float d, float gap, float nextGap, float g, float decadePx, float nodeWidth, float pxPerUnit,
                inout float lines, inout float nodes)
            {
                float s = gap / g;
                float width = min(_Width.x + min(_Width.y * s, _Width.z * pxPerUnit), _Width.w);
                float cover = saturate(0.5 * width + 0.5 - d);
                if (cover <= 0.0) return;
                float weight = LevelWeight(level);
                float open = Emerge(s);
                float hand = nextGap > 0.0 ? lerp(1.0, LevelFloor(level), Emerge(nextGap / g)) : 1.0;
                lines += weight * open * hand * cover;
                float n = log(max(gap * decadePx, 1e-6) / _Open.y) / nodeWidth;
                nodes += sqrt(weight) * open * exp(-n * n) * cover;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // the wave coordinate (decades today) in two parts: the strip's base decade, exact once rounded, and the rest,
                // small enough that float precision resolves a thousandth of a decade on the outermost waves
                float vl = IN.wave.x;
                float base = round(IN.misc.x);
                // decades per pixel across the waves, taken before any branch: exact under the perspective, the lens and the
                // fan-out of the waves
                float2 dv = float2(ddx(vl), ddy(vl));
                float g = max(length(dv), 1e-10);
                // a node is a glint a few pixels long: how fast the decades' spacing changes along the waves here sets how
                // near the open spacing a line's gap must be to glow, never wider than the node's most width; where the spacing
                // hardly changes along a line it has no point of opening, and the glint fades rather than run along it
                float2 dDecade = float2(ddx(IN.decade), ddy(IN.decade));
                float nodeSpan = abs(dot(dDecade, float2(-dv.y, dv.x) / g)) * _Node.z;
                float nodeWidth = clamp(nodeSpan, NodeLeast, max(_Node.y, NodeLeast));
                float nodeShare = saturate(nodeSpan / NodeLeast);
                float decadePx = exp(IN.decade);

                // born on the inner track, dissolving toward the horizon, gone where the matter fell into a body
                float a = IN.misc.z
                        * WaveSmooth(IN.wave.y / _Fades.x)
                        * (1.0 - WaveSmooth((IN.wave.z - _Fades.y) / max(1.0 - _Fades.y, 1e-4)))
                        * WaveSmooth(IN.wave.w / _Fades.z);
                // nothing reads where even the decades are crowded under the emerge spacing
                if (a <= 1e-4 || 1.0 / g < _Open.x) return half4(0, 0, 0, 0);

                float kl = floor(vl);
                float f = vl - kl;                  // where in its wave: 0 on the inner crest, 1 on the outer
                float k = base + kl;                // the wave: 10^k .. 10^(k+1) m from us today
                float m = exp2(f * 3.32192809489);  // 10^f: the distance today over 10^k, 1 .. 10
                float pxPerUnit = IN.misc.y;
                float lines = 0.0;
                float crests = 0.0;
                float nodes = 0.0;

                // the crests (level 0), 10^k and 10^(k+1) m; the first cell beside each is the next level's 1..2
                const float firstCell = 0.30103;    // log10(2)
                // the crests carry their own warm colour, so each tenfold wave leads the ten finer ones inside it
                if (k >= _Range.x - 1e-3 && k <= _Range.y) AddLine(0, f / g, 1.0, firstCell, g, decadePx, nodeWidth, pxPerUnit, crests, nodes);
                if (k + 1.0 >= _Range.x - 1e-3 && k + 1.0 <= _Range.y) AddLine(0, (1.0 - f) / g, 1.0, firstCell, g, decadePx, nodeWidth, pxPerUnit, crests, nodes);

                // the nested levels: the two lines of each level's lattice around the fragment (a farther one is at least its own
                // gap away, out of reach of its width)
                int levels = (int)(_Open.z + 0.5);
                [loop] for (int level = 1; level < 5; level++)
                {
                    if (level >= levels) break;
                    float lattice = level == 1 ? 1.0 : level == 2 ? 0.1 : level == 3 ? 0.01 : 0.001;
                    // the level's widest cell (just outside a crest) still crowded: neither it nor any finer level shows
                    if (log10(1.0 + lattice) / g < _Open.x) break;
                    float jLo = floor(m / lattice);
                    [unroll] for (int side = 0; side < 2; side++)
                    {
                        float j = jLo + side;
                        // a line a coarser level draws: the multiples of ten, and for the first level the crest at 1
                        if (fmod(j, 10.0) < 0.5 || (level == 1 && j < 1.5)) continue;
                        float x = j * lattice;
                        float at = log10(x);
                        float vLine = k + at;
                        if (vLine < _Range.x || vLine > _Range.y) continue;
                        float gap = log10(1.0 + lattice / x);
                        float nextGap = level + 1 < levels ? log10(1.0 + 0.1 * lattice / x) : 0.0;
                        AddLine(level, abs(f - at) / g, gap, nextGap, g, decadePx, nodeWidth, pxPerUnit, lines, nodes);
                    }
                }

                // the swell that runs outward through the decades, from below the trunk to past the horizon
                float swell = (base + vl - (_Range.x - _Pulse.w + _WaveTime * _Pulse.y)) / max(_Pulse.z, 1e-3);
                float pulse = 1.0 + _Pulse.x * exp(-swell * swell);

                float3 rgb = (_Color.rgb * _Color.a * lines + _CrestColor.rgb * _CrestColor.a * crests
                              + _NodeColor.rgb * (_Node.x * nodeShare) * nodes) * pulse * IN.misc.w;
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}

# why-2 architecture

A causality graph from the Big Bang to the present moment, rendered as a clock-like ring.
This document is the contract between the core (`Assets/Scripts/Core`, `Assets/Resources/Shaders`) and the
layers (`Matter`, `Life`, `Humans`, `Director`, `UI`).

## Visual grammar

| Axis / channel | Meaning |
| --- | --- |
| Angle (clockwise) | Time on the super-logarithmic clock `Ft(t) = t^(t^(-1.4 - 2.39t))`: Big Bang at 6 o'clock (`u = 1`), life around 10, recorded history near 2-3, the last few years shrinking to seconds from 3 o'clock back to the present moment at 6 o'clock (`u -> 0`). |
| Radius (`rho`) | Relevance to us. `rho = 0` is the inner track (our lineage). Other matter, species and peoples split off outward and fade. |
| Height (`y`) | Hierarchy: matter (bottom), life (middle), humans (top). Inside the human layer, lifelines rise and fall with modeled social market value. |
| Hue | Exclusively the level: red = matter, green = life, blue = humans. Everything else (text, UI, ticks) is neutral grey/white. |
| Intensity / bloom | Highlight and attention (director mode, our lineage, famous figures). |
| Alpha | Dissipation with distance from our lineage, out-of-focus time, level of detail. |

## Data space

Every vertex is stored in **data space** `(u, y, rho)`:

* `u` - clock arc in `[0, 1]`, `u = DeepTime.Arc(yearsAgo)`; `1` = Big Bang, `0` = now.
* `y` - height (`GraphStyle.MatterY`, `LifeY`, `HumansY` + offsets).
* `rho` - radial offset outward from the base ring (`>= 0` for content, negative for axis labels).

The vertex shader (`Assets/Resources/Shaders/WhyCommon.hlsl`, mirrored exactly by `GraphWarp.ToWorld` in C#)
maps data space to world space through the **warp**, driven by global shader parameters:

* `uF` focus arc, `R0` base radius, `unroll` in `[0, 1]`, `yScale`
* `C` log offset in years, `lnYaF = ln(yaF + C)`, `kLin` world units per ln unit, `rhoScale`

```
ya     = AgeU * Ft(u)                         // years ago, recomputed in the shader
sPolar = R0 * 2pi * (u - uF)                  // arc length from the focus on the clock
sLin   = kLin * (ln(ya + C) - lnYaF)          // unrolled time: log for small C, ~linear for large C
s      = lerp(sPolar, sLin, unroll)
R      = R0 / (1 - unroll)                    // the ring straightens as it unrolls
dphi   = s / R
world  = R0*N + T*(R + rho')*sin(dphi) + N*(rho'*cos(dphi) - 2R*sin^2(dphi/2)),  rho' = rho*rhoScale
```

`N`/`T` are the outward normal / tangent of the ring at the focus angle `2pi*uF`. At `unroll = 0` this
is exactly the polar clock; at `unroll = 1` the focused period is a straight timeline tangent to the
ring (past to the left, present to the right when viewed from inside the ring). Re-scaling the graph
is therefore just animating these globals (`GraphWarp.AnimateTo`), with zero mesh rebuilds.

## Rendering

* URP (`Assets/Settings/WhyURP.asset`), HDR, MSAA 4x, post-processing volume created at runtime
  (bloom, ACES tonemapping, vignette).
* `Why/Line` - screen-space expanded polylines with miter joins and analytic anti-aliasing; width is
  `widthPx + widthWorld * pixelsPerUnit`, so lines never vanish at huge scale and thicken close up.
  Additive blending, HDR color -> bloom.
* `Why/Surface` - filled bands (alpha blended) with radial dissipation and optional nebula noise.
* All meshes use 32-bit indices and infinite bounds (the vertex shader moves vertices).

### Line vertex layout (`LineMeshBuilder`)

| Attribute | Format | Content |
| --- | --- | --- |
| POSITION | float3 | data position of this point |
| TEXCOORD0 | float3 | data position of the previous point (== this for the first) |
| TEXCOORD1 | float3 | data position of the next point (== this for the last) |
| TEXCOORD2 | float4 | side (+-1), width px, width world, highlight id |
| TEXCOORD3 | float2 | intensity (HDR multiplier), flow (0..1 causal pulse) |
| COLOR | unorm8 x4 | tint rgb, alpha |

### Surface vertex layout (`SurfaceMeshBuilder`)

| Attribute | Format | Content |
| --- | --- | --- |
| POSITION | float3 | data position |
| TEXCOORD0 | float4 | highlight id, intensity, across (0 inner edge .. 1 outer edge), noise amount |
| COLOR | unorm8 x4 | tint rgb, alpha |

## Highlight ids

Ids are exact integers stored in floats (< 2^24). `Highlighter` uploads up to 8 inclusive id ranges
with a glow multiplier, plus a global dim factor for everything not highlighted.

| Range | Owner |
| --- | --- |
| 0 | never highlighted (axes, grid) |
| 1 .. 999 | matter bands (`GraphIds.Matter(index)`) |
| 10 000 .. 19 999 | tree of life, `10 000 + preorder node index`; a clade is a contiguous range; our lineage (root -> Hominidae) is `[10 000, 10 000 + depth]` because the human-bearing child is always ordered first |
| 1 000 000 + c * 100 000 | civilization `c` block: `+0` band, `+1..999` figures of that civ (`GraphIds.Figure`), `+1000..` lifelines ordered by birth date (a generation is a contiguous range) |

## Anchors

`Anchors` maps keys to data-space points (+ id range, label, tier, blurb). Keys follow `kind:id`:
`matter:`, `epoch:`, `clade:`, `lifeevent:`, `leaf:`, `civ:`, `figure:`, `war:`, `gen:`, `smv:us`,
`time:<yearsAgo>`, `now`. Layers register anchors while building; the director, labels and camera
resolve them through `GraphWarp.ToWorld`.

## Layers

Each layer derives from `GraphLayer` and builds in two steps so loading never blocks:

1. `PrepareAsync(GraphContext)` - runs on a worker thread: parse data, lay out, fill builders.
   No Unity object APIs here (math structs are fine).
2. `Upload(GraphContext)` - main thread: create meshes/renderers from the builders, register labels.

`GraphRoot` loads text assets on the main thread, runs all layers' `PrepareAsync` concurrently, then
uploads. Target: interactive in well under two seconds.

## View presets

`ViewPresets` defines named focus states (`overview`, `cosmos`, `stars`, `earth`, `life`,
`complex_life`, `mammals`, `hominins`, `prehistory`, `civilizations`, `modern`, `smv`, `present`):
a time window, unroll amount, log offset, radial/height scale and a camera pose. The number keys,
the HUD and the director all go through `GraphRoot.Focus(preset)`.

## Data files (`Assets/Resources/Data`)

`matter.json`, `life_traits.json`, `life_clades.json`, `civilizations.json` (+ `../powerByYearsAgo.json`),
`demography.json`, `figures.json`, `tour.json`, and `smv/*.csv` (UN US population pyramids, marriage,
divorce, partners, single-parent homes). Schemas are documented in `Docs/DATA.md`.

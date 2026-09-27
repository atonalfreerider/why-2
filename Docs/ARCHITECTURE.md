# why-2 architecture

A causality graph from the Big Bang to the present moment: a clock of deep time that becomes a straight
human branch at 3 o'clock.
This document is the contract between the core (`Assets/Scripts/Core`, `Assets/Resources/Shaders`) and the
layers (`Matter`, `Life`, `Humans`, `Director`, `UI`).

## Visual grammar

| Axis / channel | Meaning |
| --- | --- |
| Path (clockwise) | Time. From the Big Bang at 6 o'clock (`u = 1`) the path runs clockwise around a circle on the super-logarithmic clock `Ft(t) = t^(t^(-1.4 - 2.39t))` (life begins near 10 o'clock). At 3 o'clock (5000 years ago, the dawn of civilizations) it is not a clock anymore: the human branch leaves the circle on a straight tangent line with near-linear time, ending at the present moment. Life and matter dissolve before the branch; only our lineage rises into it. |
| Radius (`rho`) | Relevance to us. `rho = 0` is the inner track (our lineage). Other matter, species and peoples split off outward and fade. |
| Height (`y`) | Hierarchy: matter (bottom), life (middle), humans (top). Inside the human layer, lifelines rise and fall with modeled social market value. |
| Hue | Exclusively the level: red = matter, green = life, blue = humans. Everything else (text, UI, ticks) is neutral grey/white. |
| Intensity / bloom | Highlight and attention (director mode, our lineage, famous figures). |
| Alpha | Dissipation with distance from our lineage, out-of-focus time, level of detail. |

## Data space

Every vertex is stored in **data space** `(u, y, rho)`:

* `u` - clock arc in `[0, 1]`, `u = DeepTime.Arc(yearsAgo)`; `1` = Big Bang, `0` = now.
* `y` - height (`GraphStyle.MatterY`, `LifeY`, `HumansY` + offsets).
* `rho` - offset outward from the base path (`>= 0` for content, negative for axis labels).

The vertex shader (`Assets/Resources/Shaders/WhyCommon.hlsl`, mirrored exactly by `GraphWarp.ToWorld` in C#)
maps data space to world space through the **warp**.

**Base path** (`GraphWarp.BasePath`): arc length `sigma(u)` from the Big Bang -

```
sigma = (1 - u) / (1 - uH) * R0 * 3pi/2                          (u >= uH, the circle: 6 -> 3 o'clock)
sigma = sigmaH + L * (ln(yaH + CH) - ln(ya + CH)) / ln((yaH + CH) / CH)   (u < uH, the straight branch)
```

with `uH = Arc(5000 years ago)`, `L = 6` world units and `CH = 3000` years (near-linear time).

**Lens** (`WarpState`): a focus `uF`, `unroll` in `[0, 1]`, and a time window (`C` log offset,
`lnYaF = ln(yaF + C)`, `kLin` world units per ln unit). Arc length from the focus:

```
ya   = AgeU * Ft(u)                                 // years ago, recomputed in the shader
sLin = kLin * (lnYaF - ln(ya + C))                  // lens time: log for small C, ~linear for large C
s    = lerp(sigma(u) - sigma(uF), sLin, unroll)
```

Geometry: the circle part's curvature is scaled by `(1 - unroll)` around the focus (radius
`R' = R0 / (1 - unroll)`), the branch stays straight. The CPU derives the junction frame
(`_WhyJ`: position and tangent where circle meets branch, at `s = sH`); the shader then places
`s >= sH` on the line `PJ + TJ (s - sH) + rho' NJ` and `s < sH` on the circle through the junction.
At `unroll = 0` this is the base path; at `unroll = 1` the lens window is a straight timeline tangent
at the focus (past to the left, present to the right when viewed from inside). Re-scaling is only
animating these globals (`GraphWarp.AnimateTo`), with zero mesh rebuilds.

**Handoff fade**: `GraphStyle.HandoffFade(u)` (C#) / `WhyHandoffFade(u)` with material property
`_HandoffFade = 1` (`GraphMaterials.FadeBeforeHumanBranch`) dissolve life and matter between 5 million
years ago and the handoff. Labels opt in with `LabelSpec.HandoffFade`.

## Rendering

* URP (`Assets/Settings/WhyURP.asset`), HDR, MSAA 4x, post-processing volume created at runtime
  (bloom, neutral tonemapping, vignette).
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

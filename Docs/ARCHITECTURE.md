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

## Portrait / vertical mode

Tutorial videos are also recorded at 9:16 for phones. Landscape is the authored layout; every portrait branch
is keyed off `ScreenLayout.IsPortrait`, so landscape poses and layouts are untouched.

* **`ScreenLayout`** (Core) is the one source of truth for the screen shape: `IsPortrait` (height / width above
  1.05 turns portrait, below 1 / 1.05 turns back), `Aspect`, `SafeArea`, `UiScale` (short side / 1080) and
  `LabelScale` (graph labels: `UiScale`, times `PortraitLabelZoom` = 1.5 in portrait). `Refresh()` re-reads the
  screen at most once per frame and bumps `Version` / `OrientationVersion`.
* **V** (`ScreenLayout.ToggleVertical`) switches to 9:16 and back. In the Editor it sets the Game view to a fixed
  1080x1920 size (`Assets/Scripts/Editor/PortraitGameView.cs`, which drives the internal Game view size list by
  reflection; also the menu **Why > Game View > Portrait 1080x1920 / Landscape 1920x1080**). In a player it opens
  a 9:16 window 90% of the display height (e.g. 547x972 on a 1080p display) and restores the old window after.
  Record 1080x1920 videos from the Editor (that menu, or Unity Recorder at 1080x1920), not from the player window.
  A window resized to portrait by hand is laid out the same way.
* **Canvases**: `UiFactory.CreateCanvas` adds `CanvasLayout`, which (before the scaler runs) switches the scaler
  between the landscape reference (1920x1080, matched to the height) and the portrait reference
  (`UiFactory.PortraitReference`, 500x889, expanded): a 1080x1920 frame draws 2.16 px per canvas unit, so the
  13-unit HUD text is 28 px and the tour's 17-unit narration 37 px. `UiFactory.CanvasSize` / `CanvasScale` give
  the canvas the screen is about to have, for layouts made on the frame the screen flips.
* **Re-framing**: when `OrientationVersion` changes, `GraphRoot` re-applies the current preset for the new shape
  in LateUpdate (warp and camera over `GraphRoot.ReframeSeconds`) and then raises `OrientationChanged`: the
  director re-frames its stop and moves its panel, the figure tracker holds its glide until the flight ends.
* **View presets** (`ViewPreset.Pose()`, `PortraitFraming`):
  * `Turn` (unrolled windows, the default): the camera turns 90 degrees to look along the timeline into the past,
    pitched 50-60 degrees; distance and target are solved so the stretch the landscape view shows fits between
    the HUD bars (normalized device y -0.48 .. 0.76), then the view slides outward until its nearest row starts
    0.4 units inside the base path. Relevance (rho) runs across the screen, outward to the right.
  * `Narrow` (`smv`, `footprint`: views whose message is height): the landscape orientation stays, the time window
    is squeezed to the portrait width, the camera steps back 1.25x and heights get 1.5x.
  * Polar: the whole clock (`overview`) steps back until `PortraitWidth` (13.5 units) fits across; one arc of the
    clock (`life`) looks down more steeply (`PortraitPitch`) with its band of relevance (`PortraitRho`) fitted
    between the bars, instead of stepping back until the far side of the clock shows.
  * Time direction therefore differs: turned views put the past at the top and the present at the bottom, narrow
    and polar views keep the landscape convention (past at the left). This is deliberate; the narration never
    relies on left / right.
* **UI**: the HUD re-flows (title beside the buttons, the preset bar in balanced full-width rows along the bottom,
  legend and readout above it, the help sheet in one column); the tour panel is a full-width sheet docked at the
  bottom (above the legend) or the top (when its anchor sits low), and anchors are framed a little above the
  middle; the figure tracker's panels become sheets under the title, and its people list starts hidden (F). While
  following a figure, the glide keeps the current moment of the life between the card and the bottom bar (from
  birth just under the card to death just above the legend); the camera travels only the share of the lifeline
  that does not fit there.

## Data files (`Assets/Resources/Data`)

`matter.json`, `life_traits.json`, `life_clades.json`, `civilizations.json` (+ `../powerByYearsAgo.json`),
`demography.json`, `figures.json`, `tour.json`, and `smv/*.csv` (UN US population pyramids, marriage,
divorce, partners, single-parent homes). Schemas are documented in `Docs/DATA.md`.

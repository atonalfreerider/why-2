# Code map: what the competitive-advantage redesign will change

Read-only map, 7 Nov 2026, of the 2026 cross-section ("the present, taken apart", view id `present`, key I).
Paths are under `Assets/Scripts/` unless they start with `Docs/` or `Assets/Resources`. Line numbers are from the tree at
HEAD 65ce5d6. "Exploded units" are land-local world units (see section 2).

What will be built (brief.md, "What the research is for"): actors as objects (size = market cap, valuation or assets;
**altitude = the share of the surplus an actor keeps**; depth = supply-chain position, rivals side by side), rivers of money
by motive (desire or fear) with choke points, flashpoint sparks between actors, and scenarios that move the landscape.

---------------------------------------------------------------------------------------------------------------------

## 1. How the present view is built, step by step

### 1.1 Entry point and state

- The view is the `HillPresentLayer` (`Economy/Layers/HillPresentLayer.cs:41`), `[GraphScenes(EconomyLayouts.HillsScene)]`,
  `Order => 58` (:46), view id `"present"` (:44). It is a `GraphLayer`; `GraphRoot` creates every layer of the scene by
  reflection over `[GraphScenes]` (`Core/GraphScenes.cs:13,39`, `Core/GraphRoot.cs:472`), so a new layer class is picked up
  with no registration.
- **Key I** is not in the preset table's key dispatch alone: the preset has `KeyCode.I` (`Economy/HillPresets.cs:68`), and
  the layer also reads `kb.iKey` itself (`HillPresentLayer.cs:194-206`): I enters (`root.Focus(View)`), I again replays,
  Shift+I plays the choreography backward and returns to `overview`. It is guarded by `root.AllowPresetKeys`
  (`Core/GraphRoot.cs:66`) and `HudKit.TypingInField()`.
- The universe scene has its own preset also called `"present"` (`Core/ViewPresets.cs:456`, "The present moment") and
  `UI/HudPresetBar.cs:34` labels it "Present". The two are in different scenes (economy vs why); a new view id must not
  reuse either.
- Lifecycle: `Prepare` (:141, worker thread, only fetches the shared `InstantModel`), `Upload` (:143, places the layer on
  the land frame `EconomyStage.Land().Place(transform)` and, in a smoke run, registers the sequence), `OnFocus` (:149,
  plays the choreography, or `Rest()` in a smoke run so catalog frames are frame-exact), `Tick` (:164).
- `Tick` builds only while the view is shown or fading (:170-175). The build condition is "the land snapshot changed"
  (`snapshot != LandService.Current || land != HillLandscapeLayer.Current`, :177) and it must win the frame's slot:
  `HillRebuilds.Take()` (:179; `Economy/HillRebuilds.cs:20`: one layer rebuilds per frame after a year swap; a blocking
  publish, version 1 or the first land rebuilds at once). So the whole view is built once per year's land, on the main
  thread, in one frame (no worker for the meshes today; design 9.3 wanted a worker).

### 1.2 Data

`Build()` (:322-359):
1. `land = HillLandscapeLayer.Current` (a `HillLandscape`, the wave's geometry for the shown year), `data =
   LandService.Model.Data` (`EconomyData`), `instant = model.Year(land.Year)` (`InstantModel`, the year's accounts),
   `state = instant.State` (outlays for the bedrock).
2. `layout = new PresentLayout(land, data.Industries, state.Outlays)` (:331).
3. `normal = Fortune500Profits / Fortune500Revenue` from `data.Circuit` (:333, 10%).
4. `peaks = new PresentPeaks(layout, data.Industries, circuit.Io, normal)` (:334).
5. Then in order: `Bedrock` (:347), `Terraces` (:348), `Guides` (:349), `Firms` (:350), `Labels` (:351), `Flows` (:352),
   `Capital` (:353), `Key = MakeKey(...)` (:354), `Log` (:355), `Apply()` (:358).

Where the numbers come from (all loaded before the layer, by `EconomyLoaderLayer`, `Economy/Layers/EconomyLoaderLayer.cs:25`
via `EconomyData.Paths`, `Economy/Data/EconomyData.cs:26-28`, `EconomyData.Parse` :123):
- `Resources/Data/economy/industries.json` -> `Industry` (`EconomyData.cs:409-456`): `Id`, `Name`, `Level`, `ProfitMargin`,
  `ValueAdded` (a `YearSeries`), `Companies` (`List<Company>`). 25 industries, 95 named companies. `Company`
  (:458-480): `Name`, `Ticker`, `Revenue`, `NetIncome`, `MarketCap` ($B, 2025/26), `Year`, `Note`, `UsEmployees`.
- `circuit.json` -> `data.Circuit` (`Io` input-output table, `Fortune500*`, `GroupEquityHistory`, `ForeignEquityShare`,
  `Capture` = the 25 towers).
- `PresentPeaks` turns them into objects: for each `PresentLayout.Hill` (an industry with a band on the crest), one
  `Field` (`Land/PresentPeaks.cs:51`) of `Peak`s (:30). A named peak has `Revenue`, `NetIncome`, `Share`
  (revenue / max(industry output, sum of named revenue / 0.7), :152), `Margin`, `H` (= `Height(margin)` =
  margin x 45, :244), `Rx`, `Rd` (radii, `Size` :304), `X`, `D`. Rest-of-industry peaks are a Zipf spread at the
  industry's typical margin (:190-219, illustrative). `Flows` = the 30 largest IO purchases (:224-237).
- Today the market cap is read nowhere in the present; only revenue and net income size and raise a peak.

### 1.3 Layout (pure C#, `Land/PresentLayout.cs`)

- `PresentLayout` (:28): constants `S=2` (scale over the crest's x and y), `D=200` (depth), `Gap0=40`, `Gx=14`, `Gy=16`,
  `Gb=24`, `Apron=60` (:33). Four `Terrace`s (:91, tier 1 raw materials .. 4 tech) each the crest cut face's column over its
  tier band; `Hill` (:107) one per industry (`Center`, `Band` half width, `ValueAdded`, `Flat`).
- The choreography is a set of pure functions of a clock 0..6.4 s (`Seconds`, :39): phases Face, Cut, Grow, Separate,
  Rise, Arrive (:42-61, `PhaseStart/End` :64). `Root(part, seconds)` (:274) gives a `RootPose` (position, scale);
  `Rise`, `Reveal`, `Guides` (:283-294).
- Checks, run on every build and by the smoke test: `AreaCheck` A1 (:304), `Coincidence` A2 (:336), `Disjoint` A3 (:384).

### 1.4 Meshes and materials (all in `HillPresentLayer`)

One mesh per part, built once, all driven by root transforms and alpha. The part list: `enum Part` (:48)
Bedrock, Strata, Terraces, Hills, Guides, Firms, Flows, Capital, with emphasis keys `present.*` (:51).
- `Bedrock` (:363-402): `SurfaceMeshBuilder` quads (strata of outlays, apron) + `LineMeshBuilder` edges. 2 renderers.
- `Terraces` (:406-466): per tier, a slab mesh (strata on both faces, plateau, glass plane at normal profit :446-451) and
  a terrain mesh `Terrain(k)` (:608-672): grid of `Cell=1.6` exploded units, heights from `peaks.Exploded`, vertex colour by
  margin (earth below normal profit, `GoldDeep`..`GoldBright` above, contours every 5%, a glass line at the normal margin),
  per-vertex `glow` intensity `1 + 1.6 g^2` for bloom (:649). 8 renderers. The solid terrain material has `_ZWrite 1`
  (:408-410).
- `Guides` (:511-533): dashed lines from each part's near corners back to the crest face (`GraphMaterials.SetDash`).
- `Firms` (:540-561): one ring (a 24-gon polyline, `Ring` :1040) on every peak above normal profit; gold deeper and
  wider with the excess. Also fills `firmTops` (summit, peak) used by labels and capital threads.
- `Flows` (:681-734) and `Vein` (:742-764): the 30 IO purchases as meandering tapered lines from the seller's biggest
  peak to a branch point to the buyer's three biggest peaks; the top 6 carry pulses (`AddFlowPath`).
- `Capital` (:779-862): threads from each terrace's surplus and the 10 most profitable firms' summits to a hub high over
  the present, then to five owner discs (`Disc` :884) sized by share (Fed DFA).
- Materials: `SurfaceMaterial(queue)` (:1052) = `GraphMaterials.Raw(GraphMaterials.Surface(...))`, `_EdgeSoft 0`;
  `Lines(...)` (:1064) = `GraphMaterials.Raw(GraphMaterials.Line(..., additive))`. `Raw` places vertices in object space,
  not through the timeline warp (`Core/GraphLayer.cs:170`). Queues 3184-3194 (:400-401, 408, 531, 560, 730, 857, 861).
- `AddRenderer` (:1071) registers `(renderer, part)`; `Apply` (:287-310) sets each renderer's alpha every frame from
  `EconomyEmphasis.Get(key)` times the choreography share (`EconomyEmphasis.Set`, `Emphasis/EconomyEmphasis.cs:320`).
  `Clear` (:1086) destroys every owned mesh/material.

### 1.5 Labels

`Labels(instant, data)` (:896-945) builds a `List<HillLabel>` with land-local `At`, a `LabelRole`
(Title, Name, Note), a `Part` ("present.firms", ...) and a `Rank`; `ShowLabels` (:312) hands them to
`HillLabels.Set(this, labels)` once `Reveal > .5` (:187) and `Clear`s them otherwise. Flow and capital labels are added
inside `Flows` (:721-727) and `Capital` (:843-856). Content today: tier names with value added and jobs, the state, the 8
firms with the largest net income ("NAME · m% margin · s% share"), the 2 largest below-normal firms, the normal-profit
plane, the 3 largest veins, the owners. `FirmLabels=8`, `IndustryLabels=2` (:85).

### 1.6 Camera poses

`Economy/HillPresets.cs`: `Add(HillPresentLayer.View, ..., KeyCode.I, new Vector3(PresentX=290, 70, crest + Gap0 + 100),
yaw 160, pitch 24, distance 590)` (:67-68, :84). Poses are land-local targets via `Make` (:99-105): `FixedTarget =
f.World(target)`, `FixedYaw = f.Yaw + yaw`, so yaw 180 would look straight back at the crest from the future; 160 is 20
degrees off the wave's axis. During the choreography the layer flies the rig itself (`Fly` :279, `Advance` :247-277):
`PresentFace()` (:88, target (110, 24, crest), yaw 172, pitch 6, distance 300) -> `PresentRise()` (:92, target
(270, 40, crest+140), yaw 160, pitch 18, distance 700) -> the catalog pose (distance 590). `PortraitWidth 600`
(:102) sets portrait framing. The view's legend (`ViewKey`) is `HillPresentLayer.Key` (:93, `MakeKey` :972), reached through
`HillPresets.Key` (:215) and drawn by `UI/HudLegend.cs:133`.

---------------------------------------------------------------------------------------------------------------------

## 2. Coordinate frames and units of the present cross-section

- **World vs land.** `EconomyStage.Land()` (`Economy/EconomyStage.cs:39`) is a `LandFrame` (`Land/LandFrame.cs:15`): origin
  = the road's present moment on the ground, rotation = the road's direction, so land-local **+z runs along time away from the
  past, +x across the hillside (valley to hillside, toward the right seen from the road), +y up**. Land units are world
  units (rotation and translation only; `LandFrame.World` :39).
- **Crest.** `HillLandscape.Z(year)` (`Land/HillLandscape.cs:206`) is the z of a year; `land.CrestZ = Z(Year)` (:106). The 2026
  cross-section (the crest's cut face) is the plane z = CrestZ. On it: x from `HillLandscape.FootX = 18` to about
  18 + `WidthNow` (190) = 208, y from the valley floor 0 up to about `HeightNow` 58, the state's bedrock below at y in
  [-18, 0] (`Bedrock`, :51; the exploded present replaces its depth with the outlays, 4 units per $1T, `PresentLayout.cs:36`).
  Constants at `HillLandscape.cs:57`.
- **The exploded present.** Its near edge stands `Gap0` = 40 past the crest: `NearZ = CrestZ + 40`, and depth d (exploded)
  runs 0..200 from there (far edge z = CrestZ + 240; the bedrock's apron reaches d = 260). `PresentLayout.cs:13-20`.
- **A point in a part's frame to land-local:** `p_land = Root.Position + Scale * (x, y, d)`; at the choreography's end
  `Position = (tier*14, tier*16, NearZ)`, bedrock `(0, -24, NearZ)`, `Scale = (2, 2, 1)` (`Root` :274-280). So:
  - land x = 2 * crest-x + 14 * tier. The four terraces span about x 50..580 (`HillPresets.cs:82-84`).
  - land y = 2 * `Terrace.Top` + terrain height + 16 * tier (a hill root sits at y = Top in the terrace frame,
    :460-462). Terrain height is stored in crest units (exploded / 2) and doubled back by the root's scale, so
    **a peak's land-local height = its `H` in exploded units**, 45 per 100% margin (`PresentPeaks.HeightPerMargin` :67).
    NVIDIA at ~55% reads about 25 units; the glass plane (10% normal) is 4.5 units over each plateau.
  - land z = NearZ + d.
- **Scale.** Area is true: a terrace is `S*(band width)*D` and a hill's footprint is `S*2*Band*D` (`PresentLayout.cs:99,115`),
  1,000 square units = `PerThousand` $B of value added a year (:148, :193; printed in the legend, :977). A peak's footprint
  = share of its industry's output (not market cap); volume = profit (:11-16 of PresentPeaks).
- **Helpers to put something on the ground:** `OnGround(tier, x, d, lift)` (`HillPresentLayer.cs:574`), `InTerrace(tier,
  local)` (:583), `GroundUnder(tier, world)` (:767), `layout.OnTerrace` (`PresentLayout.cs:233`), `peaks.Summit(p)`
  (`PresentPeaks.cs:298`), `peaks.HeightAt(tier,x,d)` (:285). All return land-local points; the layer's transform
  (placed in `Upload`) converts to world.
- **How the camera reaches it.** `HillPresets.Present*` poses (1.6) plus `CameraRig.FlyTo(pose, seconds)`
  (`Core/CameraRig.cs:87`); the choreography calls it at cues 0, 1, 2. The wave is mostly absent in the present column of
  the emphasis table (wave.ribs/ridges/spray/lives 0, wave.face and wave.crest .2; `Emphasis/EmphasisTable.cs:77-89`,
  column "prsnt"), so the crest it came from shows only as a faint outline.
- **Vertical order today** (what "altitude" means now): tiers are lifted by `Gy` per tier (raw lowest, tech highest) and each
  peak's height is net margin. Supply-chain depth (d) is today a within-industry arrangement (largest firm in the middle,
  others alternating, `PresentPeaks.cs:163-188`); it carries no meaning yet.

---------------------------------------------------------------------------------------------------------------------

## 3. Reusable pieces

### 3.1 Objects sized by a value (tokens, peaks)

- **Peaks.** `PresentPeaks.Peak` + `Size` (:304) + `Exploded` (:250): p-norm blend of warped cones, summit exact. Reuse
  for "an actor is a peak" only if footprint and height keep meaning (they do: share and margin). Cost: `Exploded` is called
  per grid vertex (~14k per terrace); adding 100 more peaks is cheap, but a peak list per terrace is scanned linearly.
- **Rings and discs.** `Ring` (:1040, polyline circle in the XZ plane) and `Disc` (:884, triangle fan, `SurfaceMeshBuilder`)
  with radius = `1.5 + 11 sqrt(share)` (:838) is the owner-disc pattern: area proportional to value. Use the same
  `sqrt` rule for a market-cap sphere or disc.
- **Mark code tokens.** `Draw/MarkCode.cs:132` (`MarkMeshes`, `Add(m, form, quad, color, id, confidence)` :220, `MotiveRing` :275,
  `Chip` :289): measured = solid, judged = outline at confidence, modelled = hatched; these are camera-facing quads in a
  plane (`MarkQuad.Rect` :55). The image-token plan (`Docs/research/holistic/maps/objects.md`, section 4: one draw call per page,
  4 vertices per token) is not built: no thumbnail pipeline exists yet, and only three real people are approved.
- **Heights from value:** `HillWaveLayer.Lift(value)` = `2 + log10(1 + v) * 2.4` (`Layers/HillWaveLayer.cs:626`), the wave's
  tower beam (log of market value). A log scale keeps NVIDIA ($5T) and a $5B firm both on screen; the present's peaks are
  linear in margin, so market cap needs its own size rule (disc radius or sphere, not height).

### 3.2 Ribbons and rivers with animated flow

Three families exist; pick by what must be true:
- **Lines with path pulses (current present veins and threads).** `LineMeshBuilder.AddFlowPath(points, id, phasePerUnit,
  startPhase)` (`Core/LineMeshBuilder.cs:138`): each point's phase = 2 + startPhase + distance*phasePerUnit; the line
  shader runs pulses along the path at the material's `_FlowFreq` and `_FlowSpeed`; `LinePoint.WidthPx`, `WidthWorld`
  (:9-26), `Intensity` > 1 blooms. Used by `Vein` (:742) and `Thread` (:866); flow material setup at :731-733 and
  :858-860 (`_Flow 1.2..1.4`, `_FlowFreq 2..3`, `_FlowSpeed .25..3`). **Width is in pixels or world units per point, so a river
  whose width must equal money needs `WidthWorld = amount * scale`** (the veins today use sqrt of value, not linear).
- **The shared ribbon (money as a true-width band, with motive lanes).** `Draw/FlowRibbon.cs:113` (`FlowRibbon.Add(marks,
  spec)`): `RibbonSpec` (:36) `From`/`To`/`Via` ports, `Amount * Scale` = width (within 2%, checked by PaletteCheck),
  `Lanes` from `FlowRibbon.Lanes(EconomyStyle.MotiveSplit(width, fearShare))` (`Draw/FlowRibbon.cs:135`,
  `Economy/EconomyStyle.cs:91`): **desire on the left in `Roles.Desire` #EF5B98, fear on the right in `Roles.Fear` #79E4F0,
  never blended** (`EconomyStyle.cs:112-115`). `Seam`/`Confidence` for judged splits, an arrowhead for direction,
  `HairlinePx` 1.5 floor. The band lies in a plane facing the camera (`Normal`), so it suits a flat diagram or a stage, not a
  hillside seen from several yaws; it is drawn through `MarkMeshes` (LDR, alpha-blended, no bloom).
- **Surface ribbons in 3D.** `HillCaptureLayer.Ribbon` (`Layers/HillCaptureLayer.cs:358`) builds a flat ribbon along a path from
  a `SurfaceMeshBuilder`; `HillCaptureLayer.cs:189` uses it for climbs. A 3D river with real width and two motive lanes would
  be a new builder, modelled on `SurfaceMeshBuilder.AddBand(inner, outer, colorsInner, colorsOuter, glowInner, glowOuter, id)`
  (`Core/SurfaceMeshBuilder.cs:98`, used by `Terrain` :668), one band per lane.
- **The existing "rivers of desire and fear" view** (`rivers`, key 6, `HillPresets.cs:33`) is not a ribbon class: it is
  `Layers/FlowsLayer.cs` (groups `LandGroup.Rivers`, `GRivers` :49, built around :1200 and :1346, queues
  `LandStyle.QueueRivers`), coloured per lane by `EconomyStyle.Desire` and `.Fear` (`EconomyStyle.cs:28-29`) with the judged
  fear share per purchase (`Tools/judge-purchases.py`, `Resources/Data/economy/purchase-judgments.json`). Its legend is at
  `HillPresets.cs:236`. For firm-level desire and fear, `CompanyLives.Life.Manufactured`, `FearSold`, `FearShare`
  (`Model/CompanyLives.cs:34-42`, judged by Jev: how a company sells) already exist for 75 companies.

### 3.3 Glowing sparks and bloom

- Bloom starts above intensity 1.1 and no tonemapper on the hills (`objects.md`, "No tonemapper on the hills", decision 36).
  Marks that mean something stay LDR; light is `LinePoint.Intensity` > 1 (`LineMeshBuilder.cs:16`), the surface `intensity`
  / `glow` arrays of `AddBand` and `AddTriangle(a, b, c, color, id, intensity)` (`SurfaceMeshBuilder.cs:123`). `Terrain` gives the
  gold `1 + 1.6 g^2` (:649); the normal-profit line gets 1.4 (:658).
- There is no particle system. A **spark** = a short bright `AddFlowPath` (a few points, `Intensity` 2-3, pulse
  `_FlowFreq 1`) or a `Ring` at intensity > 1, as `Impactors` do (`Matter/ImpactorLayer.cs:156,262`: "a path pulse, one pass
  every 9 s, flashes at the impact"). The red-flash pattern (`rise` line) is the closest existing "event at a point".
- `EconomyEmphasis.Get` must stay within 0..1 for every key (`EconomyEmphasis.Check`, :631-645: "past intensity 1" is a
  smoke error), so glow comes from vertex or point intensity, never from an alpha over 1.

### 3.4 Labels and hover cards

- **Labels:** `HillLabel` (`Economy/HillLabels.cs:36`): `Text` (TMP rich text, <= 12 words, spec rule 7), `At` (+ `Frame` Land /
  Stage / World), `Role` (Title 18 px prio 10, Name 15/8, Value 13/6, Note 12/4), `Part` (emphasis key; hidden below .3,
  `PartHiddenBelow` `Core/LabelSystem.cs:163`), `Provenance` (Measured / Judged italics with "J 0.74", not drawn below .5 /
  Modelled "≈"), `Plate`, `PixelOffset`, `OnClick` (:94), `Rank`. `HillLabels.Set(owner, labels)` (:221) and `Clear` (:242)
  replace the owner's set; `HillLabels.Spec` (:254) converts. The label system (`Core/LabelSystem.cs`) lays them out in
  screen space: `MaxVisible = 110` legacy labels (:103), `RoleBudget 140` desktop, `RoleBudgetXr 60` (:172), role label
  size 12-22 px (:157), titles 1.2 degrees. Leaders (`LeaderPx`, :186), hover (`root.Labels.Hovered`) and `ClickAt`.
- **Hover card.** `UI/HudTooltip.cs:12` (`new HudTooltip(canvasRect)`; `Show(key, heading, meta, body)` :65, `Update` :77,
  `Hide` :91, `Tick(mouse, canvas, dt)` :98) is the card; `LandPicker` shows one for the land's things
  (`Economy/UI/LandPicker.cs:101,438-461`; it skips when `root.Labels.Hovered != null`, so labels win over cards). `HillExplorer`
  and `EconomyControls` (:98, :156, :411-423) use the same class. **The present view has no picker and no hover at all today**
  (grep: no `PointerInput` or `HudTooltip` in `HillPresentLayer`; the present's strings are only labels and the legend).
  The wave's hover is the model: `HillWaveLayer.Pick` (:925-946) projects sampled points of each lifeline with
  `camera.WorldToScreenPoint(frame.World(at))` against `PointerInput.Position` (`Core/PointerInput.cs:33`, hands and desktop),
  best within 12 px at 1080p, returns when the pointer is over UI (`EventSystem.IsPointerOverGameObject`) and sets
  `HoverCompany`; a click selects (`SelectedCompany`, :943). `HillStrategyLayer` (:143) reads those statics to focus a firm.
  Reuse that projection loop over `firmTops` (about 88 entries) for the present.
- **Panels.** Pattern: a `GraphModule` with `[GraphScenes(EconomyLayouts.HillsScene)]`, `UiFactory.CreateCanvas(name, 47,
  transform)`, `UiFactory.Panel` anchored top right at (-18, -176), `Update` shows it when `LandView.PresetId == "<view>"`
  (`UI/StrategyPanel.cs:20-60`, `UI/RegimePanel.cs:21-60`). The sorting order ladder: HUD 40, economy panels 43-47, tour 50.

### 3.5 Toggle UI for scenarios

- `UI/RegimePanel.cs` is the closest: one `UiFactory.Button(panel, name, label, size, onClick)` (`Core/UiFactory.cs:186`) per
  regime in a row, a "step through all" button (`HillRegimeLayer.Pinned = -1`), the selected button highlighted with
  `ActiveFill/ActiveText` (:23), reading text, and "Sources & method" folded (`methodOpen`). **Buttons, not keys, so it works by
  pinch in the headset.** The state lives in a static on the layer (`HillRegimeLayer.Pinned`), the panel reads it.
- `Layers/HillRegimeLayer.cs` (990 lines) redraws the bedrock and summits when the regime changes: its scenario-driven
  redraw is the pattern for "scenario changes the landscape" (data in `Model/Regimes.cs`, `Resources/Data/economy/regimes.json`).
- The what-if chip and year chip sit in `UI/EconomyControls.cs` (:120-135); `HudPresetBar` lists views. Scenario toggles for the
  present should be a panel like the above, shown only for `present`, with the layer reading a static `Scenario` (same pattern
  as `Pinned`). A tour stop can pin a scenario (the regime panel's cues do, "the stop's cues pin each regime").
- Variants of one view (`Core/ViewVariants.cs`, `EconomyEmphasis.ShowVariant`, :496) are the framework's own answer to "one
  view, several states with different emphasis"; the exploded-present design named states Money / Owners / People
  (`EXPLODED-PRESENT.md` section 5 and 338). Not implemented for the present.

### 3.6 A marker on the wave at a year, between two company lifelines

- Lifelines: `HillWaveLayer.Lifelines` (:630-684) builds one path per `CompanyLives.Life` and stores it in `paths[i]` (a
  `List<LinePoint>`; `LinePoint.Data` is land-local; z increases with the year). Same-industry companies are spread across their
  ridge in order of founding (`offset`, :640-642); survivors end at their tower or at the crest.
- **Static accessors** (the thing to call): `HillWaveLayer.CompanyAtYear(life, year, out Vector3 at)` (:48-65): the lifeline's
  point at a fractional year, by `z = HillLandscape.Z(year)` interpolated along the path; past the end it carries the end
  forward to the year's z, never past the crest; false before the line starts or if no wave is built (`active == null`).
  `CompanyAtCrest(life, out at)` (:39-44) is the last point. **Between two lifelines at a year:**
  `a` and `b` from two calls, `Vector3.Lerp(a, b, t)` (t = the rival's share, or .5), and mark it with a ring or a short
  `AddFlowPath` in a mesh parented to the land frame. Both calls return land-local points, so a marker built in
  `HillPresentLayer`'s content (land-local) needs no conversion.
- **From a name to a life index.** `CompanyLives.Get()` (`Model/CompanyLives.cs:70`) holds `Lives[]` (75 companies: `Id`,
  `Name`, `Ticker`, `Industry`, `Founded`, `Ended`, `Fate`, `Successor`, `Value` series, `ValueAt(year)`) and
  `OfTower(ticker, name)` (:124) / `Matches(ticker, name, life)` (:119) to map a `Company` or capture tower to a life.
  `Life.Id` equals the research's actor id for most firms (`nvidia`), but not by contract: add an `actorId` to the new data
  file rather than matching by name. Firms not in `company-lives.json` (OpenAI, Anthropic, TSMC, ASML, SK Hynix) have no
  lifeline: a marker between two lifelines is only possible for the 75 and for years from their first waypoint
  (`Life.FirstValueYear`); historical flashpoints (Fairchild vs Shockley, 1957) need those firms added to
  `company-lives.json` (it has `value` waypoints in nominal $B, `founded`, `ended`, `fate`, `successor`).
- **Visibility caveat.** The lifelines are drawn by `wave.lives`, which is 0 in the `present` column (`EmphasisTable.cs:82`),
  and `HillWaveLayer.Tick` hides content only for `mind` (:205-207), so paths exist in the present view, only invisible.
  A flashpoint marker in the present view that wants to show where the wave's past stands needs `wave.lives` raised for the
  view (row edit, or `EconomyEmphasis.Contribute`/`PushOverride`, :385, :452) or its own mesh. The wave's camera poses for
  history are `companies` (key 0, `HillPresets.cs:45`) and `overview`.

---------------------------------------------------------------------------------------------------------------------

## 4. Hook points for a new "advantage" layer

### 4.1 Replace or sit beside PresentPeaks

Decision: **beside, behind a switch, then replace.** PresentPeaks is the single source of truth for the terrain, the
veins, the labels, the checks (A1-A3, A8) and the legend; the other views do not use it. Nothing outside
`HillPresentLayer` references `PresentPeaks` (grep: only `HillPresentLayer.cs` and its own file), and `PresentLayout` is used
by `HillPresets` (constants only: `Gap0`, `D`) and the layer. So both can change without touching the other views.

Recommended seams, in order of cost:
1. **New pure class `Land/AdvantageModel.cs`** (no Unity objects, like `PresentPeaks`): reads the new data file, the
   `PresentLayout`, `EconomyData`, `CompanyLives`; produces `Actor` objects (id, name, kind, tier or layer, size value,
   dominance 0..1 = share of the surplus kept, supply-chain depth, position x/y/d), `River`s (from, to, $B a year, kind, desire
   or fear share), `Choke`points (actor, in-degree weight) and `Flashpoint`s (holder, rivals, year, power). Its
   positions are in the same frame as `PresentPeaks` (crest-unit x, exploded d) so `OnGround`, `InTerrace` and the label code work.
2. **New layer class `Layers/HillAdvantageLayer.cs`** with its own `View` id (or the same `present` with a switch),
   `Order` 59, `[GraphScenes(HillsScene)]`, its own `Part` enum and emphasis keys `advantage.*`, built in the same
   `HillRebuilds.Take()` slot pattern (copy `Tick` :164-188). Copy `Build`'s skeleton (Bedrock, Terraces) by moving those two
   methods and `Apply` into a base or helper class, or by making HillPresentLayer expose a flag
   `Advantage` and branching in `Build` at :347-353 (Terraces keep the tier terraces; replace `Terrain(k)` :608 by the
   advantage terrain, and `Firms`/`Flows`/`Capital`/`Labels` by new parts). Branching in place is the smaller diff; a new
   layer is cleaner for the smoke test (a second sequence name).
3. **Leave `PresentPeaks` intact** while the research is partial: the advantage layer covers the actors in the data
   file (about 60-80 of the AI chain and the largest firms), and the rest of each industry stays PresentPeaks' Zipf ground,
   so the 88 firms of industries.json do not vanish when a firm is missing from the research.

What to add to the existing view-level plumbing (each is required, or the smoke test fails):
- `Emphasis/EmphasisTable.cs`: a row for every new key in every one of the 24 columns (:242-249 are the present's), or
  `EconomyEmphasis.RegisterView` / `Contribute` (:366, :385). `Problems()` (:587) errors for a key read but declared nowhere and
  for a catalog view with no row; values must be in 0..1.
- `HillPresets.cs`: the view's `Add(...)` (:67) and, for a new id, `HillPresets.Key` (:215, the legend), the short title
  list (:176-180) and the bar groups (:187-192); a new id also needs `EconomyEmphasis.Focused` (:262) and
  `EconomyViews.Focused` (`Economy/EconomyViews.cs:55`) if it should hide the wave like the present does.
- A key for a new state: `kb.iKey` is taken (:198); scenario toggles are buttons, not keys.

### 4.2 Loading a new data file under Resources/Data/economy

1. Put `advantage.json` (and `advantage.json.meta`: copy a neighbour's, change the guid) in
   `Assets/Resources/Data/economy/`. **New .json files under Assets need .meta files** (HANDOFF, "Resources").
2. In the layer: `public override IEnumerable<string> RequiredTexts => new[] { "Data/economy/advantage" };`
   (`Core/GraphLayer.cs:57`; patterns: `HillStrategyLayer.cs:39`, `HillWaveLayer.cs:35`, `HillRegimeLayer.cs:51`). `GraphRoot.Start`
   (`Core/GraphRoot.cs:152-169`) collects every layer's paths, `Resources.Load<TextAsset>`, `Context.SetText`, then unloads the
   asset; a missing file is a warning, not an error.
3. `Prepare(GraphContext ctx)` (worker thread; `ctx.Text(path)`, `Core/GraphLayer.cs:27`) reads the string and keeps it. Parse
   in `Prepare` for a pure loader (Json.NET is used everywhere: `JsonConvert.DeserializeObject<T>`), but **cross-reference
   against `EconomyData` in the build (main thread), once `LandService.Model.Data` exists**: `CompanyLives.Provide(data, lives,
   judgments)` (`Model/CompanyLives.cs:73`) is the pattern (static `Get()`, parsed once, any thread).
4. The facts have to pass the truth gate before they are in the file: `Tools/advantage/verify.py` writes
   `Docs/research/advantage/gate/<cluster>.json`. A loader should read only facts marked verified (the brief's JSON has
   `facts`, `actors`, `flows`, `chokepoints`, `flashpoints`, `lineage`); anything dated 2026 is drawn only through the 2026
   gate (HANDOFF, "Truth"). Plan a **build step** (a script in `Tools/advantage/`) that merges the six cluster JSONs and
   the gate into one `advantage.json` with the numbers the scene needs, rather than parsing `facts` with quotes at runtime
   (the six files hold quotes and URLs that are dead weight on a Quest). Keep the quotes in a second file for the legend's
   "sources" and the hover card.
5. Compile check before any build: `python3 Tools/compile-check.py --refs-from ~/Desktop/Unity/.why-2-build`
   (HANDOFF, "Building"; mirror only, never in the main checkout while the editor is open).
6. Existing loaders to copy: `Model/CompanyLives.cs` (two files, cross-referencing `IndustryById`), `Model/Regimes.cs`,
   `Model/CorporatePrograms.cs` (`Path` const, `Provide`).

### 4.3 What the smoke test checks for the present view

- Command (HANDOFF): `EXTRA="-smoketour -smokereplay" work/shots.sh TAG [VIEWS|all] [WxH]`, and for the present
  `-smoketest -smokeextra present` (`EXPLODED-PRESENT.md` section 10: "Run with EXTRA=-smoketour -smokereplay -smokeextra present").
  `SmokeTest` (`Core/SmokeTest.cs:38`) captures every catalog view (:240), the tour (:278-303) and then the named sequences
  (:306, `SmokeChecks.RunSequences`); a requested sequence that did not run is an error (:318-321). The run ends with
  `Application.Quit(errors > 0 ? 1 : 0)` and the line `[Why] smoke: finished, N errors`. No pointer is present unless
  `-smokepointer X,Y` parks one (SmokeTest.cs header :35-36), so **hover code is never exercised by a default run**; to test a
  hover card the sequence must park a pointer or call the hover function directly.
- Catalog frame for `present`: because `SmokeRun` (`HillPresentLayer.cs:89`) holds the view at rest, the catalog's
  `economy-view-present` frame shows the end state with the choreography done (`OnFocus` :154).
- `Smoke()` (:990-1033), registered as sequence `present` (:146): focuses the view, waits up to 10 s for a build ("the layer
  built nothing in 10 s" is an error), logs the checks, captures `economy-present-t000`, `-t050`, `-t100` (the clock held,
  `Hold`), counts active renderers (error over `MaxRenderers = 16`, :78, :1011-1014), plays the choreography forward and
  back with the camera and logs the longest frame (`present choreography max frame N ms`; a warning over 50 ms).
- `Log` (:950-968): `[Why] present YEAR: tiers 4, hills N, area max err x%, t0 coincidence, apart at t1, firms n (k outside
  their hills), bedrock, peaks, veins, surplus`, and as **errors**: area errors over 1 point (A1), coincidence over .5 (A2),
  overlap or gaps under `Gx`/`Gy` (A3), firms outside their hill's footprint (A8), no named firm placed.
- Checks run after every capture, in every view (`SmokeChecks`, `Core/SmokeChecks.cs:21`): `EconomyEmphasis.Check` (a key's value
  over 1 or a key with no row is an error), `HillLabels` checks (the "labels" and "labels-stress" checks: no label pixel
  brighter than 1.05 x its colour, i.e. no bloom halo on text, :140-141, :408, :665), `PaletteCheck` (colour pairs under
  deuteranopia and protanopia, `-smokeextra palette`, `Draw/PaletteCheck.cs`), `ModelFacts.Problems()` at the end.
- To add: a `[Why] advantage ...` log with errors for (a) every actor's size and altitude traceable to a verified fact id,
  (b) no two actors overlapping, (c) rivers' endpoints at actors, sums of a node's in and out within the data, (d) the choke
  point list non-empty, (e) renderers and vertices within budget, (f) a scenario toggle changes at least one altitude.
  Register it as sequence `advantage` (same `SmokeChecks.RegisterSequence`, `HillPresentLayer.cs:146`), capture
  `economy-advantage-<scenario>` frames only for significant changes (the user: "only take screenshots ... when there are
  significant changes").

---------------------------------------------------------------------------------------------------------------------

## 5. Risks

### 5.1 Performance (meshes, draw calls, the Quest)

- **Renderer budget is already full.** The present keeps 15 renderers active (bedrock 2, 4 slabs, 4 terrains, guides 1, firms 1,
  flows 1, capital 2) against `MaxRenderers = 16` (:78); the smoke test fails at 17. A new layer that adds rivers by motive
  (2), sparks (1), actor objects (1) and tokens (1) must remove others (merge slabs and guides into one, drop strata, or
  raise the cap in step with design 9.3's "<= 14 on Quest" target, which the present already exceeds). The design's Quest table
  (`EXPLODED-PRESENT.md:399-409`): <= 14 draw calls in XR, <= 28k vertices for terraces and bedrock, lines under 5k vertices,
  <= 60 labels (`RoleBudgetXr`, `Core/LabelSystem.cs:172`), choreography frames <= 13.9 ms on device (72 Hz); desktop gate 50 ms.
- **Vertices are above the doc's numbers.** The layer's comment says about 20,000 vertices (:73-75), but `Terrain` is a grid of
  1.6 units: a 190-unit-wide tier band is 2 * band / 1.6 columns (clamped to 400) times about 113 rows, so one terrace is
  about 10-15k vertices and four are 40-60k (SurfaceMeshBuilder writes 28-byte vertices; one `AddBand` per row pair, no
  indexed sharing between row pairs, so check `VertexCount` before relying on it). Measure with `SurfaceMeshBuilder.VertexCount`
  (:33). A bigger cell on Quest (`Cell` :120) or fewer terrain meshes is the lever.
- **Build cost is one main-thread frame.** `Terrain` evaluates `peaks.Exploded` (a Perlin per peak per vertex, linear scan of
  the terrace's peaks) for ~14k vertices x 4 terraces at build; adding peaks scales it. Keep new geometry out of the same
  frame or split across `HillRebuilds` slots; the replay (1 year/s) rebuilds the present too if it is in view
  (`HillRebuilds.cs` summary), and `LayerClock` (`Core/LayerClock.cs:48`, `Measure("name")`, report of frames over 50 ms)
  is how the smoke test finds it: wrap a new layer's build in `using (LayerClock.Measure("advantage: build"))`.
- **Per-frame work.** `Apply` is transforms and `_Alpha` only. Pulses are in the shader (`_FlowFreq`, `_FlowSpeed`), so
  animated rivers cost no CPU. A hover that projects 100 points per frame is fine; a hover that raycasts meshes is not
  (no colliders exist).
- **XR.** Design says the wave goes absent in XR (crest outline only); this is not implemented (`PointerInput.IsXr`,
  `Core/PointerInput.cs:28`). An advantage layer with 80 actors and 100 rivers must have an XR profile (fewer rivers, no
  strata, bigger cell) or it is dropped on device. Bloom is cheap on desktop but costs on Quest; HDR and MSAA 4x are on
  (`objects.md`, "URP"). SpriteRenderers (images) are not SRP-batched.
- **Material count.** Each `Part` makes its own `Material` (owned list, :128, destroyed in `Clear` :1088); eleven is fine,
  fifty is not. One material per kind (additive lines, blended lines, solid surface, additive surface) is the target.
- **Rebuild on year change.** The present is per year's land; a scenario toggle must not rebuild meshes. Move actors by
  transform (like the choreography) or rebuild only the actor/river parts, not `Terrain`, and crossfade.

### 5.2 Label collisions

- The present already loses labels: HANDOFF open items ("only some firm labels survive layout", "the owners' labels crowd at
  yaw 160"). The label system drops lower-priority labels that collide (`LabelSystem` layout: role priority and `Rank`,
  `SubjectBonus`, leaders) and hides labels whose part alpha is < .3 (`PartHiddenBelow`). 88 named firms plus 80 actors
  cannot all be named: **name only the subject** (the hovered or scenario's winners and losers) and let the rest be hover
  cards; today's rule is 8 firms + 2 below-normal firms (:937-940).
- Words are limited to twelve (`HillLabel.Text` doc), role heights 12-22 px, and a label may not exceed 1.05 x its colour
  (no bloom behind text: put a `Plate` on labels over bright gold, `HillLabel.Plate`).
- Flashpoint and river labels need their own `Part` so they can be hidden independently; a label's `Part` alpha is
  `HillLabels.PartAlpha` (a func over `EconomyEmphasis`).
- Anchoring: labels at `At` land-local; ring and summit points are lifted (`+up*2.5`) to clear the peak. A tall peak
  hides a label at the camera's yaw 160 / pitch 24; the layer has no occlusion test, the label system lays out in screen
  space only.
- Icon labels (`HillLabel.Icon`, `LabelSpec.IconEm`) are the path for logos once an image pipeline exists; only free logos
  or monogram tiles, and only three approved real people (Musk, Trump, Winfrey).

### 5.3 The emphasis rules: objects first, lines only to make a point

User direction (4 Nov, HANDOFF): "there is too much line clutter ... object emphasis and only use line emphasis when
illustrating a point. faded and bloomed lines can still be present, but used sparingly".
- Today the present already draws a lot of lines: 30 veins with 3 branches each (x4 polylines), 15 capital threads, rings on
  every above-normal firm, guides, strata lines; rows in the table are all 1 (`present.*` columns are 1 for flows and capital,
  `EmphasisTable.cs:242-249`) so nothing is demoted. `EconomyEmphasis.Problems` allows at most `MaxSubjects` subject-level
  entries in a view row (:595-599, "only a synthesis view may" exceed it).
- The redesign has to make rivers rare: draw the rivers of **one story at a time** (a selected actor's in and out, or
  the choke point under study), fade the rest to the `Level` the table names (`Level` in `EconomyEmphasis.Alpha(level)`,
  :79; the four levels are subject, support, context, absent), and use `PushOverride`/`PushLit` (:452, :483) to light a
  selection like `LandPicker` does (hover dim .35, select dim .5, highest eight ids, `LandPicker.cs:43`). Put rivers in
  their own `Part` keys per kind (revenue, cost, capex, capital) so a scenario can show one kind.
- Make the **objects** carry the argument: size = scale, altitude = dominance, bloom on the above-normal gold (already
  there), choke points as bright nodes (a ring plus a small disc, intensity > 1), flashpoints as sparks only when selected
  or when the scenario names them. Sparks for all 40 flashpoints at once would be line clutter again.
- Keep the **truth marks**: measured solid, judged italic with J, modelled hatched or "≈" (`HillLabel.Provenance`,
  `MarkCode`): the research's `basis` (reported, contracted, estimate) maps to these. The predictions (who rises, who falls)
  are modelled; say so ("assumed: ..." `MarkCode.Assumed` :186).
- Colour rules: desire rose and fear ice (`EconomyStyle.Roles`), never blended; gold is **only** advantage (the present's
  legend says so, `HillPresentLayer.cs:974`); the tier earth tones and the capital gold must not be reused for the
  new motive colours; the palette check measures pairs under colour blindness, so new colours need
  `-smokeextra palette` to pass.
- The wave is the causality structure and must stay: the present remains the end of the timeline, entered by I; do not change
  `HillWaveLayer`, the lifelines or the `wave.*` rows except by adding a row value in the present's column.

### 5.4 Data and truth risks that touch the code

- **Mismatched scales.** The terrain is margin-by-share of the **US** input-output table (domestic output) while the research
  has worldwide revenue, market caps and valuations (OpenAI, Anthropic: private, no industry output); the current code widens
  industries when named revenue passes 70% (`PresentPeaks.MaxNamed`). Altitude as "share of surplus kept" is a new
  quantity: it needs a defined formula (for example firm net income over the sum of net income of its supply-chain layer, or
  margin above normal times revenue), computed in code from facts and shown in the legend, not authored.
- **Actors not in industries.json** (states, labs, investors, armies, species) have no industry, no tier and no band:
  `PresentLayout.Hill` and `Terrace` have no place for them. They need a second kind of object (stand on the bedrock for
  states, on a "capital" platform for investors, as the owners' discs do, `HillPresentLayer.cs:833-848`) and a
  `kind` field in the new file. States already have the bedrock.
- **2026 facts** need the gate (`dated2026`); a loader that shows a number the gate has not passed breaks the project's rule.
- **Historical flashpoints** (Mongols, Napoleon, Fairchild) have no home in the 2026 cross-section. Per the brief they are on
  the wave (back through history); the lifeline marker (3.6) works only for firms in `company-lives.json`. Non-firm history
  needs another layer or a panel; the hillside is the wrong place for armies.
- **Unity specifics.** New .cs and .json need .meta files; keep the GUID of renamed files (PresentBlocks became PresentPeaks
  with the same GUID); do not run Unity while the editor is open and build only in the mirror
  (`~/Desktop/Unity/.why-2-build`, HANDOFF "Building and verifying"); check `free -g` for 4 GB and `pgrep -x 'Unity Main Thre'`.
- **Jev.** Semantic judgments (which flows are desire or fear, which power a firm has) may go through `Tools/jev.ps1` or
  `Tools/jev.py`, never through code at runtime: the app reads judgments from a file (`company-judgments.json`,
  `purchase-judgments.json`), as `CompanyLives` does. Treat the score as evidence; do not put keys in the files.

---------------------------------------------------------------------------------------------------------------------

## 6. Quick index of files an implementation will touch or read

| File | Why |
| --- | --- |
| `Economy/Layers/HillPresentLayer.cs` | host of the view; fork or branch `Build` (:322), `Apply` (:287), `Labels` (:896), `Smoke` (:990) |
| `Economy/Land/PresentPeaks.cs` | terrain from peaks; keep, or add `AdvantageModel` beside it |
| `Economy/Land/PresentLayout.cs` | frame and choreography; constants S, D, Gap0, Gx, Gy |
| `Economy/HillPresets.cs` | pose (:67), legend (:215, :236), bar groups (:187) |
| `Economy/Emphasis/EmphasisTable.cs` | one row per new key in all 24 columns (present's column 19) |
| `Economy/Emphasis/EconomyEmphasis.cs` | keys, levels, overrides, `Check` |
| `Economy/Model/CompanyLives.cs`, `Layers/HillWaveLayer.cs:39-65` | lifeline positions at a year |
| `Economy/Draw/FlowRibbon.cs`, `Draw/MarkCode.cs`, `EconomyStyle.cs:91` | ribbons, marks, motive lanes |
| `Core/LineMeshBuilder.cs`, `Core/SurfaceMeshBuilder.cs`, `Core/GraphLayer.cs:88` | meshes, flow pulses, materials |
| `Economy/HillLabels.cs`, `Core/LabelSystem.cs` | labels and budgets |
| `UI/HudTooltip.cs`, `Economy/UI/RegimePanel.cs`, `Core/UiFactory.cs` | hover card, panel with buttons |
| `Core/SmokeTest.cs`, `Core/SmokeChecks.cs`, `Core/LayerClock.cs`, `Economy/HillRebuilds.cs` | verification and timing |
| `Assets/Resources/Data/economy/{industries,company-lives,circuit}.json` | the data that sizes and places things today |
| `Docs/hills-design/EXPLODED-PRESENT.md` (sections 9.3, 10), `HANDOFF.md` | design budgets and acceptance |

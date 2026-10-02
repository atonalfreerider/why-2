# SPEC2: the hill landscape game (economy scene, second redesign)

The one specification the second redesign is built from. It merges the four part designs (terrain, people and wealth,
the mind, couples and births) into one consistent design, resolves every conflict between them (Appendix A), and fixes
the contracts, checks and build plan. Where this document and a part disagree, **this document wins**; the parts remain
the background for derivations and rejected alternatives.

**Revision 2** (after the review of revision 1; decisions A35-A52, prototype `$SP/redesign2/synth/stand3.py`): controller
lines are seated one by one on the crowns the ledger apportions to them, every crown of $1T or more is seated and every
crown carries its controlled companies' payouts as vapor (3.2, 3.5, 4.5); all crowns hang in one **crown band** above the
highest summit, so height means control across the whole land (2.1, 3.5); clouds float over the hills their members
overweight, between the hills, spread over four gaps (3.6); floor rows are ordered by wealth (3.3); the program's PLAY
stage feeds the next year (6.3b); the contracts hold every cross-package dependency, the UI's bowl dependencies have named
replacements and every "Done when" is measurable at its package's place in the merge order (11.2, 12); the checks have
triggers and sources; the bowl's restore is exercised at integration (1.1, 12.1); the games page, trading assets and debt
have marks on the land (3.8, 4.6, 5.5).

**The authority is the user's words** (2026-10-02, verbatim in `$SP/redesign2/BRIEF.md`): *"keep but disable the
circular torus economy layout and replace with landscape game"*; *"a landscape with hills representing the
hierarchies"*; *"golden crowns of owners over certain sectors to show who is in control of those companies"*; *"pools of
ultra wealthy floating at cloud level between the hills to show when people own stakes in many different industries but
don't control them"*; *"the less wealthy should live on the valley floors - again if they earn from a job in a sector,
they should be close to that hill"*; *"any capital that flows to a sector should be a river flowing up the hill"*; *"I
didn't mean for the "control, not in control" to be taken literally. it moreso should be apparent from where a person
sits in the landscape"*; the desire / fear brain *"a separate 3d visualization ... but it should also be the fundamental
program that each player runs in the landscape economy simulation. tracking assets, debt, spending behavior, income per
player ... generational wealth, family inheritance and allowance ... children living with or depending on parents should
be shown in the hierarchy"*; *"on the smv graph, we need to show relationships between men and women, and children are
born from two adults intersecting in the mid-plane of the graph"*. Everything the user asked for in the first redesign
still holds (the cut that expands into a 3D landscape, spending that pools, groups in a basic tit-for-tat simulation with
in-group / out-group affinity, players grouped by 2026 socioeconomic groups, topology rather than geography, the visual
language: glowing lines and veils on black; gold = capital, blue = people, rose = desire, ice = fear, steel =
government).

## Conventions

* `$SP` = `/tmp/claude-0/-home-user-why-2/dc9bf553-91de-54d2-9f1f-9831e1f24cfa/scratchpad`. The repository is
  `/home/user/why-2`, branch `claude/economy-visualization-2026-n8j6kd`, HEAD `16262b8`. Code paths are under
  `Assets/Scripts/Economy/` unless they start with `Assets/`, `Docs/` or `Humans/` (= `Assets/Scripts/Humans/`).
* **Land-local coordinates** (`LandFrame`, unchanged): origin on the plaza `LandStyle.PlazaGap` = 10 u past the road's
  end, +x to the right seen from the road, +y up, **+z away from the road** (the land's depth). The land spans
  x ∈ [−7, 7], z ∈ [−7.575, +7.575]. Prose sometimes uses the depth **d = z + 7.575** (0 at the land's front edge);
  every contract uses z.
* Units: world units (u); dollars in $B nominal of the year unless written $T or "2025$".
* Tags: **[proto]** measured by a prototype run on the repository's data (Appendix B); **[data]** read from
  `Assets/Resources/Data/economy/*.json`; **[code]** read in the repository at HEAD; **[log]** read from the current
  build's harness log (`$SP/redesign/build/lead-fix6/land.log`); **[design]** a judgment printed as such;
  **[recalled]** a value to verify before it enters a data file.
* "The lives" = `Model/EconomicLives` + `Model/LivesSimulation` (per person per year). "A player" = the people of one
  census cell (group × anchor × party), each line = 100,000 people.
* Work packages are named **WP0-WP6** (section 12). Every file and every section names its owner.

---

## 0. The design on one page

**The land is the cut laid back and grown.** One year cut out of the road lies back into a **staircase of five
benches** rising away from the road: the government plain in front (y 0), then raw (0.70), make (1.40), services (2.10)
and tech (2.80). On each bench stands its **range: one hill per industry**, its footprint as large as the value it adds
(46 u² per GDP: 1 u² = $0.67T in 2025), its summit set by its stratum (every tech summit stands above every services
summit, and so on down, strictly). A hill is the wall's band stood up: its **foot is wages** (light blue), its middle
ring upkeep (steel), its **cap is what owners keep (gold)**. Suppliers stand in front of and below their buyers; within
a range each hill sits over the hills it trades with most; **roots** under the benches carry the purchases between
industries up into each buyer. The government is the plain and its two mesas: the ground the ranges stand on, where
taxes seep and transfers spring.

**Where a person sits is what they control** (never a label). In front of every range lies its **valley floor**, a
tread 1.20 u deep with a **river** down its middle. The less wealthy live on the floors **in rows in front of the hill
that pays them**, ordered by wealth: the wealthiest wage earners and retirees at the hill's feet, the poorest at the lip
(the row is set by the player's mean wealth against the year's median: 3.3). **Owners of private businesses and the
self-employed stand on gold-rimmed ledges on the slope of their own hill**, higher with wealth. **Controllers** (the top
1% lines whose wealth is a controlling stake) sit in **golden crowns hanging in one crown band above the highest summit
(y 3.55-4.20), each over the hill it controls on a gold stem**: a crown as large as the market value it steers, a jewel
per controlled company, a named seat per measured controlling holder (Page & Brin, Zuckerberg, the Walton family …) and
the model's controller lines seated around it. The **diversified ultra wealthy** (every other 1% line: stakes in many
industries, control of none) float as **cloud pools at cloud level (y 4.50) between the hills**, each over the hills its
members overweight, with wisps to every hill it owns. So **height is control everywhere on the land**: every floor and
slope (≤ 3.23) is below every crown (≥ 3.55), every crown below every cloud. **Children** sit beneath their parents,
joined by a stem; **adult children who live with their parents** stand in their own player and also as a hollow dot
beside their parent, joined by a home thread that carries the **allowance**; **estates** fall as gold streams from where
the departed stood to their heirs.

**Every flow is water, and only capital climbs a hill.** Wages run off a hill's foot down to its people. Spending runs
along the valley rivers (ice lane = fear, rose lane = desire, white glitter = fantasy, never blended), climbs or descends
between benches only through gorges, and **pools in lakes on the river in front of each hill** it pays. **Capital
flowing to an industry is a gold river flowing up its hill** from its foot to its gold cap, fed by rain from the cloud
level (savers' money) and by the hill's own springs (depreciation and retained earnings). Profits rise as gold vapor from
the caps: the controlled companies' share passes through the crown over the hill (control: the money passes through the
controllers' hands), the rest rises to the clouds between the hills and rains onto the savers on the floors. Saving
evaporates up to the cloud level (trading assets glittering, deposits and pensions plain); credit falls from it through
the banking hill. Taxes seep into the ground and run in
steel conduits to the mesas; transfers spring up as fountains where people live.

**The season of tit for tat plays on the terrain**: coalitions are territories drawn on the ground, ties are arcs between
players (bridges across valleys between coalitions), standing marks the alphas, omegas and anti-alphas; beside each
coalition's label a small **cooperation ladder** climbs from the one-round defection diamond toward mutual cooperation
over the season's rounds, with a **standing pyramid** of its alphas, betas, omegas and anti-alphas (the notebook's games
page).

**Every player runs one program, the mind**, every year: MEMORY (its balance sheet, buffers, what it inherited) → WORLD
(wages, business and capital income, transfers, allowance, estates, taxes) → FEEL (five desires against five fears,
stress, seven neurochemicals) → OS (reason against feeling, the modes of thought) → DECIDE (save or borrow, six spending
categories each with fear and fantasy dollars) → ACT (debts, homes, assets) → PLAY (tit for tat) → OUTCOME (agency,
pain, relief, satisfaction). A player's program is its members' programs summed. **The loop is closed**: this year's
balance sheet is next year's MEMORY, and this year's PLAY (each adult's cooperation with spouse, neighbours and the other
tribe) is next year's FEEL (the cooperative fear isolation less and desire the collective more, which moves their
spending between categories: 6.3b). **The years play** (Space): each year
every household runs its program, the census regroups the people, players glide to where their new balance sheets put
them, crowns and clouds grow and shrink, pain, relief and satisfaction flash. **The mind is also a separate 3D object**
6.5 u left of the land: a brain-shaped wireframe, fear hemisphere ice and desire hemisphere rose, the drives in its deep
floor, the chemicals hanging beneath, a reason waterline dividing the lit cortex from the limbic rest, the modes as an
orbit, memory behind, income entering its brainstem, the decisions leaving its front toward the land; `G` runs one year of
the selected player's program as a 12-second staged sequence; `P` shows every player as a particle in mind space.

**On the population graph** (the road's lifelines: women on the inner side, men on the outer side) the **mid-plane** is
drawn, **married couples run beside it**, and **every child born from 1946 on is born on it**: its mother's and father's
lines bend in and touch there, and the child's line starts at that point.

**Kept but disabled:** the circular bowl ("torus") stays in the repository under `Economy/Bowl/`, compiling, under the
retired scene id `economy-bowl` that no scene loads. Its models are reused wherever they still fit.

Prototype views of the merged layout: `$SP/redesign2/synth/sketch-top-2025.png` (top view of the merged 2025 layout),
the terrain part's `$SP/redesign2/terrain/sketch-16x9.png`, the mind part's `$SP/redesign2/mind/mock-*.png`, the SMV
part's `$SP/redesign2/smv/c5/families*.png`.

---

## 1. Kept, disabled and removed

### 1.1 The bowl: kept but disabled under a retired scene id

* **Mechanism** (the pattern of the retired stations, `[GraphScenes("economy-retired")]` at `a6aee2d`): a new constant
  `EconomyScenes.Bowl = "economy-bowl"` (new file `Economy/EconomyScenes.cs`; no Core change). No Unity scene file carries
  that id and the harness is never run with it, so the bowl's layers are never created. `Core/GraphScenes.cs` and
  `Core/ViewPresets.cs` are **not** changed (the bowl is not loadable; this keeps every Core file, shared with the
  causality scene, untouched).
* **Moved to `Economy/Bowl/`** (with their `.meta` files, `git mv`, namespaces unchanged, code unchanged except the
  attribute): `Layers/LandscapeLayer.cs`, `Layers/FlowsLayer.cs`, `Layers/InhabitantsLayer.cs`, `Layers/SocialLayer.cs`,
  `Layers/SectionLayer.cs` (each now `[GraphScenes(EconomyScenes.Bowl)]`), `Land/LandLayout.cs`,
  `Land/PlayerPlacement.cs`, `Land/MoneyRouting.cs`, `Land/CanalRouting.cs`, `Land/LandPick.cs`. New in `Economy/Bowl/`:
  `BowlPresets.cs` and `BowlViews.cs`, verbatim copies of HEAD's `EconomyPresets.cs` and `EconomyViews.cs` with the
  classes renamed (`BowlPresets`, `BowlViews`, `BowlViewSpec`), their tables padded with zero rows for the new
  `LandGroup` members, unused. `LandService.BuildBowl` keeps HEAD's `LandService.Build` body verbatim (unused).
* **Contracts the bowl needs stay** in `Land/LandTypes.cs`: `Place`, `SectorGeom`, `TowerGeom`, `LandGeometry`,
  `MoneyFlows`, `CanalLane`, `Patch`, every existing `Player`, `FlowKind` and `LandGroup` member, `LandSnapshot.Land` and
  `.Money` (null in the hills). New members are only appended.
* **Restore recipe** (written into `Docs/ECONOMY.md` by WP6, scripted as `$SP/redesign2/checks/restore-bowl.sh
  <worktree>`): retag the five bowl layers to `GraphScene.Economy`, retag the hill layers (`TerrainLayer`, `CrownsLayer`,
  `PeopleLayer`, `HillFlowsLayer`, `SeasonLayer`, `FamiliesLayer`, `CutLayer`, `SignalsLayer`, `MindLayer`) to the bowl id,
  make `LandService.Build` call `BuildBowl`, and swap `EconomyPresets` / `EconomyViews` for `BowlPresets` / `BowlViews`.
  Nothing else: **every UI path keeps its bowl branch** (taken when `LandService.Current.Terrain == null`: `LandPicker` →
  `LandPick`, the outline → `LandPick.BowlOutline`, the facts → `s.Land` / `s.Money`, the season → `SocialLayer.Shown`;
  10.6, 11.1), so the four edits above restore the bowl.
* The bowl is guarded by the compile check after every work package, and **its restore is exercised once at integration**
  (12.1): the script applies the recipe in a scratch worktree, renders `landscape` and `people`, and the result is
  recorded in the acceptance checklist ("bowl restorable", 13 #24).

### 1.2 The models: reused, extended, new

| Model | Status | Use in the hills |
| --- | --- | --- |
| `Data/EconomyData` and the JSON files | reused | everything; data additions in 11.5 |
| `Model/MoneyCircuit` (`CircuitYear`) | reused | totals, payouts abroad, government split, labels |
| `Model/WallGeometry.Split` | reused | each hill's wages / upkeep / owners zones |
| `Land/CapitalSources` | reused | capital income by paying hill (wealth group mixes); the clouds' holdings |
| `Land/SellerMatrix` (RAS) | reused | spending's first recipients (lakes) |
| `Land/RootsModel` | extended (WP1): a geometry-free `Select` is added; `Build`, `Line`, `RootsOf` unchanged | the 79 roots ≥ $50B; new geometry under the benches (4.9) |
| `Land/SocialSeason`, `Land/Coalitions` | reused unchanged | the season on the players (its `land` argument is unused and passed `null`) |
| `Layers/SocialLayer`'s runtime duties | moved (copied) to `SeasonLayer` (WP2) and `Land/LandSeason` (WP0) | the season on screen (`LandSeason.Shown`), the base / betrayal switch, the viewer's incident, the ready report (5.0) |
| `Land/LandMath`, `Land/LandFacts`, `Land/LandFrame`, `Land/LandView` | reused; `LandMath` gains `PackLine` (WP0); `LandView` gains `Phases` (WP0) and may gain members (WP5) | helpers, frame, view state |
| `Land/PlayerCensus` | extended (WP2) | `BuildHills`: the census with the hills' anchors (3.2); `Build` (bowl) unchanged |
| `Model/LivesSimulation`, `Model/EconomicLives` | extended (WP0 adds the record fields and accessors as zero stubs; WP3 fills them) | the record fields, the estate log, the program trace, **the allowance**, **the play feedback** (6.3b), the line style |
| `Model/TraitSampler`, `LivesInputs`, `LivesReport`, `PrisonersDilemma`, `GamesSetup` | reused (`LivesReport` gains lines) | the lives and the season |
| `Layers/IndustryWallLayer`, `Layers/EconomyLoaderLayer` | reused (the loader publishes the SMV bonds option, WP4) | the wall on the road; data |
| `Humans/Smv/*` | extended, scoped (WP4) | couples and births, only when the economy publishes the option (8) |
| `LandLayout`, `PlayerPlacement`, `MoneyRouting`, `CanalRouting`, `LandPick` | kept, disabled (`Bowl/`) | not used; `MoneyAccounts` re-implements MoneyRouting's geometry-free rules and must reproduce its totals |

### 1.3 What the economy scene creates (layers)

| Layer | Order | Owner | Status | Draws |
| --- | --- | --- | --- | --- |
| `TimeAxisLayer`, `HumanWorldLoader`, `SmvLayer` | 1, 20, 30 | shared | unchanged / WP4 (bonds) | the road's axis, the population and its lifelines |
| `EconomyLoaderLayer` | 25 | WP4 | changed | publishes `SmvBondsOptions` |
| `IndustryWallLayer` | 40 | – | unchanged | the wall under the road |
| `LandViewLayer` | 44 | WP0 wires, WP5 owns | changed | view state, emphasis, morph clock, overrides; ticks `LandPlayback` and `LandTween` (wired by WP0) |
| `LandModelLayer` | 45 | WP0 | changed | the ledger, the first snapshot, the log |
| `TerrainLayer` | 50 | WP1 | new | the land, contours, zone rims, company marks, lakes, roots, overlays, labels |
| `CrownsLayer` | 51 | WP2 | new | crowns, stems, jewels, tethers, named seats, controller dots |
| `PeopleLayer` | 52 | WP2 | new | player glyphs (discs, ledges, cloud lenses, dots, home dots, debt rings, inheritance arcs, mirages), glide markers |
| `HillFlowsLayer` | 53 | WP1 | new | every flow of section 4 |
| `SeasonLayer` | 54 | WP2 | new | territories, ties, standing, ladders and pyramids; the season on screen (`LandSeason.Shown`), the betrayal switch, the viewer's incident (5.0) |
| `FamiliesLayer` | 55 | WP2 | new | child stems, home threads, ghosts, estate streams |
| `CutLayer` | 56 | WP5 | new | the cut on the road and the unfold |
| `SignalsLayer` | 57 | WP3 | new | pain, relief, satisfaction flashes on the land |
| `MindLayer` | 58 | WP3 | new | the 3D mind (player, person, nation, population modes; the tether) |

### 1.4 Removed from what the scene draws (the code stays where noted)

* **The bowl's look**: rings, terraces, rim, lip canal, falls, towers and plinths, the crown ring over the bowl, the
  wage patches (all in `Bowl/`).
* **Every literal "in control" encoding** (the user: not literal): the lifelines no longer turn gold when "in control";
  the cut's and the land's dots are no longer gold for "in control"; the cut's readout drops "12% in control". **Gold
  means capital everywhere**: on the road, a lifeline is gold in the years its person is in the **top 1%** (the people of
  the crowns and the clouds), and so is its dot in the cut and on the land (3.8). The agency / in-control measure stays in
  the lives, the inspectors, the mind view's OUTCOME stage and the `[Why] Facts` log line.
* **The company towers.** The 25 named companies become **company marks** on their hill's gold cap (2.6), **jewels** in
  the crowns they are controlled from (3.5), and named wage sources (4.2).
* **The bowl's "mind" view** of mirages over discs (key 7) is replaced by the 3D mind (7). Mirages stay on the glyphs.
* **The bowl's tie signals** keep their data (`SocialSeasonResult.Events`) and their looks, renamed **tie breaks / tie
  mends / tie holds**; the notebook's pain, relief and satisfaction become life events of the program (6.4).
* Nothing is deleted from the repository.

---

## 2. The terrain (WP1)

The terrain part's design (`part-terrain.md` 1-2) with the merged constants of A3 (treads 1.20 deep, risers 0.70, the gov
plain 1.20 deep: room for four rows of people and the river, A1-A2). Reference numbers: `$SP/redesign2/synth/terrain2.py`
(`terrain2_out.txt`), which re-runs the terrain part's prototype with these constants.

### 2.1 Frame and constants

The frame constants every package uses are the contract class `HillFrame` (11.2, WP0). WP1's own constants live in
`Land/Hills/HillStyle.cs`.

| Quantity | Value | Note |
| --- | --- | --- |
| Land | x ∈ [−7, 7] (`Width` 14), z ∈ [−7.575, 7.575] (`Depth` 15.15) | fixed for all years; `ZOf(d) = d − 7.575` |
| Front edge | z = −7.575, i.e. 2.425 u past the road's end | the bedrock's front face is what the cut becomes (9) |
| Bench heights `BenchY` | gov 0, raw 0.70, make 1.40, services 2.10, tech 2.80 | the strata |
| Depth budget (d) | `Front` 1.20 (the gov tread) + `GovZone` 1.00 (the mesas) + 4 × (`Riser` 0.70 + `Tread` 1.20) + `DZones` 5.00 (the four hill zones) + `Back` 0.35 = 15.15 | A3 |
| Hill zones | Z_t = `ZMin` 0.70 + (5.00 − 4 × 0.70) × VA_t(Y) / Σ private VA(Y) | Σ Z_t = 5.00 every year: benches breathe, the land never moves |
| Area of value added | `AreaGdp` = 46 u² per GDP(Y) | 2025: 1 u² = $0.67T |
| Own rise of a hill | h_i = clamp(0.95 b_i, 0.12, 0.80); mesas 0.35 | summit S_i = BenchY_t + h_i; OwnMax − OwnMin = 0.68 < 0.70 = bench step: strict |
| Packing | `Edge` 0.30 at each side, `Gap` 0.22 between feet | |
| Crown band | `CrownBase` **3.55** (every crown's band bottom ≥ it), `CrownTopMax` **4.20** [design] | above every summit (the highest is a tech summit: 3.17-3.23 in 1950-2025 [proto]; `HillLayout.Build` checks summit ≤ CrownBase − 0.10 every year, a terrain check), so every floor and slope position is below every crown (A35) |
| Cloud level `CloudY` | BenchY_tech + OwnMax + 0.90 = **4.50** | above every summit and crown (top ≤ 4.20) |
| Rasters | `Raster` 0.05 (281 × 304 cells); terrain mesh `Mesh` 0.10 (141 × 152 = 21.4K vertices) | |
| Flow widths | `WidthGdp` 1.0 u per GDP (money flows), `CapitalWidthGdp` = `RootWidthGdp` = 4.0 ("×4") | $1T = 0.0325 u in 2025 |
| Lakes | `LakeAreaGdp` = 4.6 u² per GDP (a tenth of the hill scale), `LakeBMax` 0.16 | A2 |
| Tread rows (from the lip) | band 0 at v 1.07, band 1 at 0.87, the **river** at 0.60, band 2 at 0.33, band 3 at 0.13 | `HillFrame.BandV`, `RoadV` |
| Line floor | every dollar-coded line ≥ 0.8 px; alpha × max(0.3, min(1, $ / $20B)) | the bowl's `LandStyle.DollarFade` |

### 2.2 The staircase

Along d: the gov tread [0, 1.20], the mesa zone [1.20, 2.20]; then for t = 1..4: a **riser** (0.70, smoothstep, 56° at
its steepest), the **tread** (1.20), the **hill zone** (Z_t); then the back margin 0.35.

```
d_riser1 = Front + GovZone = 2.20;  d_lip_t = d_riser_t + Riser;  d_hills_t = d_lip_t + Tread;  d_riser_{t+1} = d_hills_t + Z_t
G(d)     = Σ_{t=1..4} (BenchY_t − BenchY_{t−1}) · smoothstep((d − d_riser_t) / Riser)          the ground's height
```

2025 **[proto]** (lip..hills of each tread, in d): gov 0.00-1.20, raw 2.90-4.10, make 5.61-6.81, services 8.90-10.10,
tech 12.69-13.89; zones raw 0.81, make 1.39, services 1.90, tech 0.91. In land-local z the treads' lips are at −7.575,
−4.675, −1.965, +1.322, +5.118 and the rivers at z = lip + 0.60: −6.975, −4.075, −1.365, +1.922, +5.718. 1972: make
5.71-6.91, services 9.40-10.60, tech 12.75-13.95 (zones 0.91 1.79 1.45 0.85). 1950: make 5.85-7.05, services 9.63-10.83,
tech 12.79-13.99.

The land stands on a **bedrock slab** (steel, y −0.30 up to the ground): its front face and both side faces are drawn.
Treads, risers and the gov tread are **ground** (alpha 0.05 treads, 0.09 risers; tier tint at low saturation: gov
steel, raw clay, make bronze, services steel-blue, tech violet-grey = `LandStyle.TierTint`).

### 2.3 The order (fixed for all years)

`HillLayout.Arrange(data)` runs once at load on 2024 value added and the BEA 2024 Use table (`circuit.json io.flows`,
between-industry only, W = F + Fᵀ): rows = the five tiers; within a row start with value added descending, laid centre-out
(largest in the middle, then alternately right, left); `x = PackLine(row order, desired, half widths a_i)` on
[−7 + Edge, 7 − Edge], gap 0.22; then **12 sweeps** (even sweeps rows raw, make, services, tech; odd sweeps services, make,
raw): key(i) = Σ_{j: |tier j − tier i| = 1} W_ij x_j / Σ W_ij (no neighbours: keep x), sort the row by (key, index), x =
PackLine(row, key). Keep `Order[t]` and `DesiredX[i]` (the last key).

Result **[proto]**, left to right: gov federal, state & local; raw utilities, oil & gas, mining & metals, agriculture;
make construction, trade, transport, manufacturing; services real estate, entertainment, banking, legal, professional,
finance, insurance, other services, hospitality, health, education; tech media & telecom, software, internet, hardware.
Of the between-industry dollars of 2024 ($15.85T): 21.9% come from a range in front (supplier lower), 41.0% stay within a
range, 37.2% come from a range behind; 54% of the dollars between adjacent ranges connect hills within 2 u in x. The roots
view prints this (it is not hidden).

### 2.4 Hills

Per year Y (`HillLayout.Build(data, Y)`):

```
A_i   = AreaGdp · VA_i(Y) / GDP(Y)                     (Present = VA_i ≥ $1B: software 1950 has no hill, its frontage stays)
b_i   = min(Z_t / 2, sqrt(A_i / π)),  a_i = A_i / (π b_i)          ellipse semi-axes; gov mesas: b ≤ 0.50
x_i   = PackLine(Order[t], DesiredX, a) per row
d_i   = d_hills_t + b_i + max(0, Z_t − 2 b_i) · 0.5 · (cf_max,t − cf_i) / (cf_max,t − cf_min,t)
        cf_i = min(1, pce_i / output_i) (2024): consumer-facing hills nearer the valley
relax 300 iterations; pairs (i < j) of a row in index order: ellipse extents along the centre line
        r_i = 1 / sqrt((u_x / a_i)² + (u_d / b_i)²); overlap = r_i + r_j + Gap − |c_j − c_i|;
        if overlap > 0 move both apart along the line, i by overlap · A_j / (A_i + A_j), j by the rest;
      then pull every hill 2% toward its target; clamp x ∈ [−7 + Edge + a_i, 7 − Edge − a_i],
        d ∈ [d_hills_t + b_i, d_hills_t + Z_t + 0.35 − b_i]
h_i = clamp(0.95 b_i, 0.12, 0.80) (mesas 0.35);  S_i = BenchY_t + h_i;  z_i = d_i − 7.575
```

**Profile.** Hill: own_i = h_i (1 − ρ²)² for ρ < 1 (biweight), ρ² = ((x − x_i)/a_i)² + ((z − z_i)/b_i)². Mesa:
own = h (1 − smoothstep((ρ − 0.5) / 0.5)). **Height** H(x, z) = G + Σ_i own_i. A raster cell belongs to the hill with the
largest own height there when it is ≥ 0.015, else it is ground. **Zones** (area-true in ρ, from `WallGeometry.Split`):
owners' cap ρ < √o_i, upkeep √o_i ≤ ρ < √(o_i + u_i), wages beyond. **Contours of a hill** (people's slope rows, 3.4): the
ellipse at ρ_e = sqrt(1 − sqrt(e)) for an elevation fraction e of a hill; for a mesa ρ_e = 0.5 + 0.5 · smoothstep⁻¹(1 − e)
with smoothstep⁻¹(y) = 0.5 − sin(asin(1 − 2y) / 3).

2025 **[proto]** (x, z, footprint 2a × 2b, summit S; owners' share o, upkeep u):

| Range | Hills |
| --- | --- |
| gov (mesas, S 0.35) | federal −1.76, −5.87, 2.11 × 1.00 · state & local +1.76, −5.87, 4.45 × 1.00 |
| raw (bench 0.70) | utilities −0.08, −3.07, 1.11 × 0.81, 1.08 · oil & gas +1.33, −3.07, 1.08 × 0.81, 1.08 · mining & metals +2.46, −3.07, 0.56 × 0.56, 0.97 · agriculture +3.33, −3.09, 0.72 × 0.72, 1.04 |
| make (1.40) | construction −5.78, −0.07, 1.84 × 1.39, 2.06 · trade −2.01, −0.07, 5.25 × 1.39, 2.06 · transport +1.54, −0.07, 1.41 × 1.39, 2.06 · manufacturing +4.06, −0.07, 3.21 × 1.39, 2.06 |
| services (2.10) | entertainment −6.29, +2.93, 0.82 × 0.82, 2.49 · real estate −4.57, +3.82, 4.26 × 1.90, 2.90 (o 0.57) · legal −2.03, +4.32, 0.90 × 0.90, 2.53 · banking −1.97, +3.24, 1.44 × 1.44, 2.79 · professional +0.21, +3.82, 2.97 × 1.90, 2.90 · finance +1.85, +3.01, 0.98 × 0.98, 2.57 · insurance +2.47, +4.13, 1.27 × 1.27, 2.70 · other services +3.02, +3.12, 1.19 × 1.19, 2.66 · health +4.59, +3.82, 2.38 × 1.90, 2.90 (o 0.09) · hospitality +6.02, +3.20, 1.36 × 1.36, 2.75 · education +6.29, +4.31, 0.81 × 0.81, 2.49 |
| tech (2.80) | media & telecom −3.56, +6.77, 1.46 × 0.91, 3.23 · software −1.60, +6.77, 2.03 × 0.91, 3.23 · internet +0.26, +6.77, 1.24 × 0.91, 3.23 · hardware +1.48, +6.72, 0.78 × 0.78, 3.17 |

Summits are strictly ordered by tier in 1950, 1972 and 2025; the steepest hill slope is 55° (the mesas' edges 63°, the
risers 56°) **[proto]**. 1950: manufacturing is a 7.0-wide massif and agriculture the largest raw hill; the tech bench
holds media & telecom and hardware only. The century reads as the land's shape.

### 2.5 Treads: rows, the river, lakes, gorges, frontages

* **Tread** of tier t: z from its lip (`LipZ`) to its hill zone (`HillsZ` = LipZ + 1.20). Tread-relative depth
  v = z − LipZ (0 at the lip, 1.20 at the hills' feet line). The gov tread's lip is the land's front edge.
* **Rows** (people, 3.3): band 0 at v 1.07 (nearest the hills), band 1 at 0.87, band 2 at 0.33, band 3 at 0.13 (the lip).
* **The valley river**: the centre line v = 0.60 of every tread, x ∈ [−6.7, 6.7], y = tread + 0.02. The rows beside it
  (bands 1 and 2) stand 0.27 from its centre; the river's drawn half-width never exceeds 0.14 (the busiest river is 0.27 u
  wide both ways [proto]; checked in the Rivers line) and lakes reach 0.16 from it, so a disc (r ≤ 0.09 + the large-disc
  shift of 3.3) never touches the water. Placement therefore needs no flow output (A8).
* **Lakes** (one per hill with direct household spending ≥ $5B; `HillLayout.AddLakes(terrain, accounts)` after the
  accounts): an ellipse **on the river in front of its hill**, area = 4.6 u² × inflow / GDP, b_L = min(`LakeBMax` 0.16,
  √(area/π)), a_L = area / (π b_L), centre (x_L, river z) with x_L = PackLine of the tread's lakes
  (desired = the hill's x, gap 0.10, within [−6.7, 6.7]); surface y = tread + 0.03. The river runs through its lakes: the
  dollars for hill k leave the river inside lake k (the river narrows past it). 2025 [proto, the bowl's pools] (2a × 2b):
  health 2.23 × 0.32 (x +4.27), trade 1.91 × 0.32, real estate 1.89 × 0.32, manufacturing 1.25 × 0.32, hospitality 0.82,
  other services 0.57, finance 0.36, transport 0.32. Overflow (inflow > VA): a second, brighter inner veil, and the label
  says so.
* **Gorges**: the only places where spending changes benches. Up to 3 per riser t (from bench t−1 up to t), at the
  lowest crests of the range in front (crest(x) = the highest own height over range t−1's hill zone, 0.05 bins; local
  minima sorted by (crest, x), kept ≥ 2.0 apart, |x| ≤ 6.55). Each gorge is a least-cost path on the 0.10 raster from
  river t−1 at x_g to river t at x_g inside x_g ± 1.5: step cost = length × (1 + 25 × own height + 4 × |ground slope|),
  8-neighbour Dijkstra, ties by cell index. A gorge crosses tread t−1's back rows and tread t's front rows: those rows
  block x_g ± 0.15. 2025 **[proto]**: gov→raw −6.50, −4.45, −0.70; raw→make −6.50, −4.45, −2.40; make→services −4.85,
  +0.65, +5.70; services→tech −1.25, +1.45, +3.55 (±0.3 in C#).
* **Frontages**: each tread is split in x at the midpoints between consecutive hill centres of its range (the outermost
  run to the land's edge). They are used for aggregation (4.11) and logged (how many players stand outside their hill's
  frontage), never as hard limits.

### 2.6 Above the land

* **Cloud level** y 4.50: drawn only as a faint dashed horizon line along the land's back edge (alpha 0.12) and as the
  plane the clouds lie on (3.6).
* **The crown band** y ∈ [3.55, 4.20] over the whole land: every crown hangs there over its hill's summit on a gold stem
  (3.5); nothing else is drawn in the band but the stems, jewels' tethers and the vapor passing through.
* **Company marks** (the `capture` companies, years ≥ 2024): on hill i's gold cap at ρ = 0.5 √o_i, spread over the cap's
  front half at azimuths −60° … +60° around −z (the camera's side), by market value (largest centred); a gold diamond of
  half-size 0.02 + 0.06 √(market value / GDP), intensity 2.0. Their labels ("Apple $3.8T") show in the capital view
  (6 largest). They are the sources of the named employers' wage streams (4.2) and the ends of the crown jewels'
  tethers (3.5). The capture ratio ("named profit / owners' share: hardware 9.2×") is in the hill's hover card.
* **Overlays** (the notebook's TECH items, `industries.json overlays`, from 2023): **AI build-out**: a white-blue
  lattice block (the bowl's scaffold white (0.86, 0.92, 1.0), copied into `HillStyle`) on the tech bench at x =
  hardware's x + 1.2 (the empty end of the tech range), its dollars in its **footprint, not its height**: a square of
  area `AreaGdp` × capex / GDP (the hills' scale; $410B → 0.78 × 0.78) and a fixed height equal to the tech range's
  highest own rise (2025: 0.43), so it never stands above a summit, a crown or the cloud level (height means control,
  A44); **social media & online ads**: a halo ring 0.10 outside the internet hill's foot, its arc split by ad revenue
  (social ads bright). Group `Overlays`.

### 2.7 Rendering the land (`TerrainLayer`, order 50)

Only `Why/Surface` and `Why/Line` (unlit, translucent, ZWrite off, bloom), every mesh land-local with
`GraphMaterials.Raw` on GameObjects placed by `LandFrame.Place`; all shading baked into vertex colors.

| Element | Geometry | Look | Group |
| --- | --- | --- | --- |
| Terrain surface | one heightfield mesh on the 0.10 grid, rows emitted back to front (far z first); **small hills (a < 0.6: 2025 mining & metals, agriculture, entertainment, legal, finance, hardware, education) get a fine patch on a 0.03 grid** over their bounding box expanded to whole coarse cells (the coarse cells under it are not emitted; the patch's boundary vertices take the coarse mesh's bilinear heights, so there are no cracks), its triangles in the Hills and Caps submeshes; ≤ 10K vertices in all | color = zone color × shade, shade = 0.55 + 0.45 max(0, n·l), l = normalize(−0.45, 0.75, −0.48); alpha: tread 0.05, riser 0.09, wages 0.18, upkeep 0.14, cap 0.42 (intensity 1.5); a triangle goes to the submesh of the zone of ρ at its centroid (vertex colors stay per vertex), so a cap's color edge lies within one cell of its gold rim: 0.10 on large hills, 0.03 on the small ones | Ground (treads, risers) / Hills (wage and upkeep zones) / Caps (cap zone) as submeshes |
| Contours | marching squares on the 0.05 raster of H where a hill owns the cell, every 0.10 u, majors every 0.50 u | 0.8 px alpha 0.22 (majors 1.1 px 0.40), the zone color whitened 50% | Contours |
| Zone rims | per hill the ellipses ρ = √o and ρ = √(o + u) lifted onto H | gold 1.4 px intensity 1.8 / steel 1 px | Caps / Hills |
| Bench edges | each tread's lip and hill line | tier hue 1 px alpha 0.35 | Ground |
| Bedrock | front and side faces, y −0.30 to the ground profile | steel alpha 0.06, edges 1 px | Ground |
| Lakes | ellipse at tread + 0.03 (32 segments) | veil (0.80, 0.88, 1.00) alpha 0.20; shoreline two-tone never blended: rose over (1 − fear share) of the arc from the inlet, ice over the rest, 2 px intensity 1.6 | Lakes |
| Company marks | 2.6 | gold diamonds | Caps |
| Overlays | 2.6 | as the bowl's | Overlays |
| Roots | 4.9 | | Roots |
| Labels, anchors | 10.4 | | |

Raw red / life green stay tints of the raw hills' wage and upkeep zones (oil & gas, mining, utilities: matter red;
agriculture: life green); gold is only on caps, rims, company marks, capital rivers, vapor, crowns, clouds' rims, the
1%'s dots and estates.

**Roots and the hills' outline for the UI**: `TerrainLayer` draws the roots of `TerrainGeometry.Roots` (filled by WP1's
`HillRoots.Build`, 4.9); `HillPick.Outline` (WP5) reads the terrain, crowns and clouds (10.6).

---

## 3. Where people and wealth sit (WP2)

The people part's grammar (`part-people.md`) on the merged terrain: floors are **rows on the tread in front of the hill**
(A1), not rings around the foot. Reference numbers: `$SP/redesign2/synth/stand.py` (`stand_2025_out.txt`,
`stand_1972_out.txt`) and its revision-2 extension `stand3.py` (`stand3_2025_out.txt`: seats, the crown band, clouds,
rows by wealth) on the person-year dump of HEAD + the allowance (`$SP/redesign2/mind/py_a0.5.csv`), with an
approximation of the census below; the C# re-measures every count.

### 3.1 The grammar: the kind of standing is control, the place within it is wealth

| Zone | Who | Where | Mark | Wealth inside the zone reads as |
| --- | --- | --- | --- | --- |
| **Floor** (y = the tread: 0-2.80) | everyone who earns from a job as an employee (PMC, public servants, office, frontline and trades, the working poor), retirees, students, the out of work | **rows on the tread in front of the hill that pays them** (retirees: the hill they last worked for; Social Security retirees: the federal mesa; students: education; the out of work: state & local) | disc ∝ √people | the **row, by the player's mean adult wealth**: band 0 at the hill's feet (≥ 2 × the year's median) … band 3 at the lip (< a quarter of the median) |
| **Slope** (≤ the hill's 68% contour, ≤ 3.09) | business owners and the self-employed (gig and freelance) | **gold-rimmed ledges on the front face of their own hill** | disc ∝ √people on a ledge | **height on the slope**: gig at 10% of the hill's rise, owners at 32 / 50 / 68% by wealth |
| **Crown** (the crown band, 3.55-4.20) | **controllers**: the 1% lines whose wealth is a controlling stake (the control ledger, 3.2), each seated on the crown the ledger assigns it | seated around the **golden crown hanging in the crown band over the hill it controls**, on a gold stem down to the summit | crown ∝ √(market value controlled) | crown size; jewels = the named companies controlled; named seats = their measured controlling holders |
| **Cloud** (y 4.50) | the **diversified 1%**: every other 1% line | **cloud pools at cloud level between the hills**, each over the hills its members overweight (3.6) | lens ∝ √wealth | lens size; its wisps to every hill it owns |
| (beneath / beside) | **children**; **adult children living with their parents** | minors directly beneath their parent's dot (a stem); dependents in their own player and as a hollow home dot beside the parent's dot, joined by a home thread | 1.6 px dots; threads | – |

No mark says "in control". Gold means capital: crowns, ledge rims, cloud rims, estates, **and the dots of the top 1%**
(3.8). **Height is control across the whole land, not only within a hill** (A35): the zones are strictly stacked in
world height everywhere (every floor and slope position < 3.55 ≤ every crown's band bottom; every crown top ≤ 4.20 <
4.50 the clouds), and the `crowns` view looks at the stack in elevation (10.2) so the screen shows the same order.
Within a zone, the place is wealth: the floor row, the slope's contour, the crown's size, the cloud's size.

What a viewer sees in 2025 **[proto, stand3]**: ~130 players; ~109 on the floors (the gov tread included), ~12 on the
slopes, 5 crown players (6 controller lines seated on internet ×2, manufacturing, trade, insurance, software), ~4 clouds;
19 crowns ≥ $100B, internet's the largest ($6.07T controlled: Alphabet, Meta; named seats Page & Brin, Zuckerberg); 727
children beneath their parents; 295 adult children at home (175 of them receiving an allowance, $379B); 158 parent estates
over 2021-2025 ($5.6T) falling to heirs.

### 3.2 The census (Hills mode) and the control ledger

**The control ledger** (`Land/People/ControlLedger.cs`, built once per load, after the lives, for 1946-2026):

```
quota f* = (top1.businessShare × PBE + Insiders) / top1.netWorth = (0.529 × $16.0T + $2.76T) / $55.0T = 0.204
           (2025; held for every year until groups.json gains a businessShare history; groups.json zones.controlQuota)
eligible (each 1% unit in year Y: WealthGroup == 3; a couple both in the 1% is one unit, its wealth the sum):
  1 runs a business: SelfEmployed && Business > 0 this year
  2 heir: received an estate (`EconomicLives.Estates`, kind child/grandchild) from a parent who was a controller in the parent's last
    year, and has been a controller every year since
  3 founder: careerSe ≥ 5 (years self-employed in the person's records up to Y)
  4 fill (only while the eligible wealth is below the quota): the rest by PersonTraits.Enterprise descending
order    (runs desc, heir desc, careerSe desc, Enterprise desc, lowest person index); take units until their wealth
         reaches f* × W1(Y) (W1 = the 1%'s wealth); the last unit may overshoot. No random numbers.
```

2025 **[proto]**: 27 lines in the 1%, 6 controllers (22.1% of the 1%'s wealth against the quota 20.4%); 1972: 14 lines,
4-5 controllers. The model's gap is said in the log and the docs: in the lives the top 1% receive 13% of proprietors'
income where the DFA says 53%; the ledger chooses which of the 1%'s existing wealth is a controlling stake and does not
change the lives.

**Seats** (A36): the ledger also says **which crown each controller unit sits on**, so the model's seats agree with the
measured crowns. Per year, over the crowns of the year (3.5: V_k from `CrownModel.Values(model, year)`, k with V_k ≥ the
crown threshold; at least the hill of largest V_k):

```
seats_k  = Hamilton apportionment of the year's n controller units over the crowns by V_k (floor of n V_k / Σ V, the rest
           by the largest remainders; ties by larger V_k, then industry index)
assign   (units by wealth descending, then person index; each pass fills only open seats)
  0 keep:       the unit's seat of last year, while that crown still has an open seat (no seat-hopping in playback)
  1 own:        the crown of the unit's own industry (the business it runs; else its career self-employment's most
                frequent industry; else its last)
  2 own tier:   the open seat of largest V_k in the own industry's tier
  3 the rest:   the open seat of largest V_k
```

`ControlLedger.SeatOf(person, year)` returns the seat's industry (−1 for a non-controller); a couple unit shares one seat.
2025 **[proto, stand3]**: seats internet 2, manufacturing 1, trade 1, insurance 1, software 1 (every crown of $1T or more
has a seat); the six lines (own industries software, hardware, trade, professional, finance, federal) sit on software
(own), internet (own tier), trade (own), insurance (own tier), internet, manufacturing: own industry 2 of 6, own tier 4 of 6.
The hover and the log say it: "seated by the ledger on the crowns of the measured control (V_k); this line's own business
in the lives: professional services". In 1972 V_k is the private part only (the model mix of the lives' business
income), so the seats mostly fall on the lines' own industries.

**The census** (`PlayerCensus.BuildHills(model, pop, ledger, year)`, WP2; `Build` for the bowl unchanged): the existing
rule (groups by priority, Erikson dominance for non-employed spouses, cells place → tier → rest → largest, the size test
≥ 10 lines or ≥ 3 lines with ≥ 1% of net worth, the party split, children with the mother's player) with three anchor
changes; **crown cells are exempt from the size test and from the party split** (one player per seated crown, whatever
its size: a crown seat is never merged into another crown, A36):

| Group | Anchor in Hills mode | Key example | `Player.Anchor` (the season's exposure) | `Player.Hill` |
| --- | --- | --- | --- | --- |
| The 1%, controller lines | `crown:<seat industry>` (`ControlLedger.SeatOf`), never merged | `top1\|crown:internet\|X` | the seat's industry | that industry |
| The 1%, the other lines | `cloud:<home tier>` (tier of the current or last industry; `cloud:none` if never worked) → rest | `top1\|cloud:services\|D` | −1 | −1 |
| Comfortable retirees | `former:<industry>` (the last record with `Employed`, scanning back ≤ 45 years; none → education) → `tier:` → rest | `retired_savings\|former:health\|R` | −1 | the former industry |

Pseudo-anchors keep their hills: Social Security retirees → federal, students → education, the out of work → state &
local. `tier:` and `rest` cells take the hill of their members' plurality industry by wages + business income (ties: the
lower industry index). 2025 **[proto, stand3]**: 130 players (the C# will differ by a few: 122-136 passes), zones floor
109, slope 12, crown 5, cloud 4; 1972: ~87 players.

### 3.3 Floor rows (`Land/People/StandPlacement.cs`)

```
disc r      = HillFrame.DiscK √(people / 1M), DiscK = 0.030       (A24; r ≤ 0.105 up to 12.2M people; 2025 max 0.103)
row         = by the player's mean adult wealth w against the year's median adult wealth m (2025 $134K): band 0 w ≥ 2m
              (= the slopes' w_lo: the wealthiest non-owners nearest the hill), 1 m ≤ w < 2m, 2 m/4 ≤ w < m, 3 w < m/4 (the
              lip); a row is monotone in wealth by construction (A43)
tread       = the tier of the player's hill;  z = LipZ(tread) + BandV[band];  y = GroundY on the tread (+0.005)
per (tread, band): players sorted by (their hill's x, group order PMC, public, office, comfortable retirees, frontline,
              Social Security retirees, working poor, students, out of work; key; index)
free intervals of the row: [−6.7, 6.7] minus x_g ± 0.15 of every gorge crossing it
each player goes to the interval containing its hill's x (else the nearest; ties: the lower interval)
x           = PackLine(desired = the hill's x, half = r, interval, gap 0.03) per interval
order       = per tread the rows beside the river first (1, 2), then the outer rows (0, 3); a disc of an outer row also
              treats as blocked the x-interval of every placed inner-row disc closer than 0.03 to it (only large discs can be)
overflow    = an interval whose players do not fit moves its last players (by the sort) to the other row on the same side
              of the river (0 ↔ 1, 2 ↔ 3); then logged as spills
large disc  = r > 0.09 moves away from the river by (r − 0.09) (2025: 4 players, r 0.090-0.103)
```

2025 **[proto, stand3]**: rows by wealth hold 48 / 18 / 24 / 19 floor players (band 0 PMC 28, comfortable retirees 15,
Social Security retirees 4, out of work 1; band 3 working poor 13, students 3, out of work 2, frontline 1); no row is more
than 16% full; spills 0; the largest distance from a player's hill's x is 0.37 u; 2 of 109 floor players stand just
outside their hill's frontage (0.15 u). (By group alone, as in revision 1, 45 of 310 same-hill pairs were ordered against
their wealth: comfortable retirees, $1.06M a head, stood behind the PMC, $0.68M.) 1972 (by group, revision 1): 0 spills,
0.25 u, 0 outside; WP2 re-measures it by wealth.

`Player.Pos` = the disc centre; `GroundX` = its x; `Tread` = its tier; `Band` = its row (+10 per spill).

### 3.4 Slope ledges

```
wealth fraction t = clamp(ln(w / w_lo) / ln(w_hi / w_lo), 0, 1); w = the members' mean adult wealth;
                    w_lo = 2 × the median adult wealth of the year (2025 $268K), w_hi = the poorest 1% line's (2025 $7.9M)
row               = gig and freelance → 0; owners → 1 (t < 1/3), 2 (t < 2/3), 3
elevation e       = {0.10, 0.32, 0.50, 0.68}[row];  the contour = the hill's ellipse at ρ_e (2.4)
packing           = along the contour's front half (the half facing −z), parametrized by arc length; desired = its front
                    point (x_i, z_i − b ρ_e); PackLine with gap 0.03; blocked: the capital river's crossing ± 0.10;
                    overflow to the next row up, then down (logged)
ledge             = a flat veil of radius r + 0.04 at the contour's height (alpha 0.12) with a gold rim (1.2 px, intensity 1.4)
```

No slope player stands above e = 0.68: the summit belongs to the crown. Within each hill elevation never decreases with
wealth (checked). A slope player on a mesa (the gov hills) uses the mesa's contour. 2025 **[proto]**: 12 slope players in
rows {0: 5, 1: 1, 2: 6}; every row fits.

### 3.5 Crowns (`Land/People/CrownModel.cs`, `Layers/CrownsLayer.cs`)

**Value controlled** per hill k (every year):

```
PBE_Y      = DFA private business equity ($16.0T for 2025; other years 7.6 × proprietors' income_Y, circuit.json incomeHistory)
Private_k  = top1.businessShare × PBE_Y × p_k,Y;  p_k,Y = the lives' business income by industry of year Y (Σ Business of
             self-employed records with Industry k) / its total; logged "(model mix)" (A22)
V_k        = Private_k + Σ_{controlled j in k} MarketCap_j + Σ_{founder-led j in k} insiderEconomic_j × MarketCap_j
             (from 2024, when circuit.json capture has the companies; before: the private part only)
classes    = controlled: the insider / founder / family bloc holds ≥ 20% of the votes (La Porta et al. 1999): the crown
             counts the whole market value; founder-led: 1-20%: the stake; widely held < 1%: no jewel
```

Insider blocs **[recalled, to verify against each company's latest DEF 14A before entry; A23]**: Alphabet (Page, Brin 6%
/ 51% class B) controlled; SpaceX (Musk 42% / 79%) controlled; Meta (Zuckerberg 13% / 61%) controlled; Berkshire
(Buffett 14% / 30%) controlled; Walmart (Walton family 45%) controlled; Palantir (Karp, Cohen, Thiel 7% / 50% class F)
controlled; Oracle (Ellison 41%) controlled; Amazon (Bezos 8.3%), NVIDIA (Huang 3.5%), Tesla (Musk 13%), Eli Lilly (Lilly
Endowment 10%), Netflix (Hastings 1.5%) founder-led; Apple, Microsoft, Broadcom, AMD, JPMorgan, Visa, Exxon, J&J,
Mastercard, BofA, Costco, Caterpillar, Chevron widely held. 2025 **[proto]**: controlled $10.64T, founder stakes $0.72T,
the 1%'s private firms $8.46T: **$19.82T in 19 crowns ≥ $100B**; internet $6.07T (Alphabet, Meta), manufacturing $3.41T
(SpaceX; Tesla, Lilly), trade $2.65T (Walmart; Amazon), insurance $1.56T (Berkshire), software $1.18T (Palantir, Oracle);
hardware only $0.23T (NVIDIA's founder stake): Apple, Broadcom and AMD are widely held, so hardware's profit rises to the
clouds. That contrast is the point of the crowns, and the vapor shows it (4.5): internet's payouts pass through its crown
(controlled share 0.99), hardware's go straight to the clouds and the savers (0.02).

`CrownModel.Values(model, year)` (pure, geometry-free; read by the ledger's seats and by the placement, which copies E_k and c_k into `PlayerSet.Crowns` for the flows, 4.5) returns
V_k, its parts and the **controlled share** of each crowned hill's payouts:

```
E_k   = max(Σ named market caps in k, MarketCapTotal_Y × O_k / Σ_private O) + PBE_Y × O_k / Σ_private O
        the hill's equity value, an estimate (O_k = the hill's owners' share of value added, WallGeometry.Split;
        MarketCapTotal_Y = circuit.json marketCapTotal × GDP_Y / GDP_2025 for other years); logged "(estimate)"
c_k   = min(1, V_k / E_k)        2025 [proto, stand3]: internet 0.99, insurance 0.45, manufacturing 0.39, trade 0.23,
                                  software 0.23, the private-only crowns ~0.13, hardware 0.02
```

**Geometry** (A35): one crown per hill with V_k ≥ $100B (2025$, scaled by the year's GDP over 2025's; at least the hill
of largest V_k), **all in the common crown band**:

```
r_k   = StockK √(V_k / GDP_Y), StockK = 0.75                        (r 0.1 = $0.55T in 2025; internet 0.333)
y_k   = CrownBase + 0.8 r_k  (the band's bottom; bigger crowns hang higher)   centre (x_k, z_k) = the summit's
top_k = y_k + 0.25 r_k (band) + 0.45 r_k (points) ≤ CrownTopMax 4.20  (else r_k is capped; never in 2025: internet's top
        4.05 [proto, stand3])
crowns whose rings intersect in x-z (distance < r_a + r_b + 0.05) and overlap in y: the smaller rises in steps of
        0.15 until clear (never above the top limit; then it moves outward along the line between centres)
stem  = a vertical gold line from the summit (x_k, S_k, z_k) to the band's bottom: what the crown controls
```

2025 **[proto, stand3]**: band bottoms 3.59-3.82, tops ≤ 4.05, 0 lifts, 0 moves; the longest stem 2.55 (agriculture,
from its raw summit 1.04). Every floor and slope position (≤ 3.09 for a ledge, 2.80 for a floor; summits ≤ 3.23) lies
below every crown.

Drawing (`CrownsLayer`, group `Crowns`): **band** = two horizontal circles (48 segments) at y_k and y_k + 0.25 r_k with a
veil strip (gold `EconomyStyle.Capital`, lines 1.6 px intensity 1.8, veil alpha 0.18); **points** = 8 triangles rising
0.45 r_k from the band's top, the first centred toward −z; **stem** = 1.2 px gold, alpha 0.35, intensity 1.2, dashed below
the hill's cap rim height (so it reads as "over", not "into", the land); **jewels** on the band's front half by value:
controlled = a filled diamond of half-size max(0.02, 0.25 √(MarketCap/GDP)) intensity 2.4, founder-led = a hollow diamond
of the stake's size intensity 1.4; **tethers** = one gold hairline (1 px, alpha 0.25) per jewel down to its company mark
on the cap (2.6); **shadow** = the ring projected on the summit (1 px alpha 0.12); **named seats** (the measured holders,
`circuit.json capture[].holder` of each controlled or founder-led jewel: Page & Brin, Zuckerberg, the Walton family, Musk,
Buffett, Ellison, Karp / Cohen / Thiel, Bezos, Huang, the Lilly Endowment, Hastings) = a hollow gold ring (r 0.025, 1.4 px)
on the band's top rim above its jewel, with a hairline to it; **controller dots** = the seated model lines evenly around
the band's back half at mid-height (filled gold, 2.6 px, the lifeline's highlight id). Hollow = measured, filled = the
model: the legend says so. Crowns are static (no spin).

**Labels** (the 5 largest; crowns view all ≥ $1T): "Internet · $6.07T controlled · Page & Brin (Alphabet), Zuckerberg
(Meta)"; a crown with no controlled jewel names its founder stakes or "the top 1%'s own firms". The hover card says both
the measured and the modeled: "Internet: $6.07T controlled · Alphabet (Page, Brin, 51% of votes) and Meta (Zuckerberg,
61%) · the top 1%'s own firms here $0.24T · in this model: 2 controller lines seated (0.2M adults; seated by the ledger,
their own businesses in the lives: hardware, finance) · payouts through this crown $xB a year (99% of internet's)".
Before 2024 the crowns are the private part only; the legend says "1972: crowns show the top 1%'s own firms; company
control from 2024".

### 3.6 Clouds (`Land/People/CloudModel.cs`)

A cloud is a player of the diversified 1% (zone Cloud):

```
holdings  m_i,k = (1 − h) Mix[3]_k + h [k == home(i)],  h = 0.15 when home(i) ≥ 0 (current or last industry), else 0
          (Mix = CapitalSources.Mix(data, Y); 0.15 = own-industry tilt [design, Döskeland & Hvide 2011; Benartzi 2001, recalled])
          Holdings_p,k = Σ_{i ∈ p} Financial_i × 1e5 / 1e9 × m_i,k    ($B; Financial from the record, 6.2)
over_p,k  = max(0, Holdings_p,k / Σ_k Holdings_p,k − Mix[3]_k)      the cloud's overweights against the market (A37)
centre    c_p = Σ_k over_p,k (x_k, z_k) / Σ_k over_p,k at y = CloudY (no overweight: the holdings' centroid);
          R_p = StockK √(W_p / GDP_Y) (W = members' net worth)
between   a centre with any hill's ρ < 0.8 (x-z) moves to the nearest point of the 0.05 raster within 2.5 u where every
          hill's ρ ≥ 0.8 and the lens fits in the land (ties by cell index): **between the hills**
separate  60 iterations, fixed order (clouds by key, then crowns by industry): a cloud closer to a crown (x-z) than
          R + r + 0.10 moves away by the deficit; cloud pairs closer than R_a + R_b + 0.05 push apart along a→b by half the
          deficit each (equal positions: a along +x, b along −x); clamp into the land shrunk by R
```

The holdings (where its money is) give the cloud its **wisps** (4.5: a fan from every hill it owns); its overweights
(where its members made their fortunes and still hold more than the market) give its **place**. Every cloud has the same
market mix underneath (banking 0.20, trade 0.14, manufacturing 0.11, real estate 0.10, professional 0.07 …), so the
holdings' centroid would put every cloud over the same spot; revision 1 did that (4 clouds within 1 × 2 u over the
services tread).

2025 **[proto, stand3]**: 4 clouds ($36T, 21 lines), each in a different gap: `cloud:services` ($24.1T, R 0.66; over
professional, entertainment, other services) between finance and professional at (+1.06, +3.28); `cloud:tech` ($5.4T;
software, internet) between internet and software at (−1.13, +6.47); `cloud:make` ($5.5T; trade, manufacturing) between
trade and transport at (+0.13, −0.07); `cloud:rest` ($1.6T; its members worked for the governments) between the mesas at
(−0.02, −5.92): spread over 12.4 u of depth and 2.2 u across, 4 distinct gaps, the largest shift from the overweight
centre 0.32 u, clearance to a crown ring ≥ 0.10.

**Glyph**: a horizontal lens of radius R (pale gold-white `#E8DDC0`, alpha 0.10 at the centre falling to 0.03 at the
rim, 32 × 4 triangles), its rim gold 1.2 px with the tribe's dash pattern; the members' dots sunflower-packed over the
lens at y + 0.02 + 0.10 × reason (gold, 2.6 px: the 1%); children beneath (3.7). Hover: "Diversified 1% · home: services ·
R · 4 lines (0.4M adults) · $6.7T · owns: trade 17%, manufacturing 13%, banking 12%, … · more than the market in:
professional, entertainment · controls nothing".

### 3.7 Children, dependents, generational wealth (`Land/People/FamilyModel.cs`, `Layers/FamiliesLayer.cs`)

* **Minors beneath their parents.** Every child line is a 1.6 px dot directly beneath its mother's dot (else its
  father's): at the disc's ground height + 0.005 (floor, slope), the band's bottom − 0.10 (crown), the lens − 0.10
  (cloud), offset 0.012 u per sibling around the parent's x-z (golden angle), with a **stem** (vertical hairline child →
  parent, people blue 1 px alpha 0.30, the child's highlight id). A child with no living parent in the record hangs under
  its player's centre with no stem. 2025 **[proto]**: 727 minors under 91 players; by the parents' zone floor 588, slope
  65, gov tread 38, cloud 1, crown 0.
* **Child cost** (A9): what a household spends on its minors, an accounting split of its spending, not new money:
  `spending × 0.3k / (1 + 0.5 [spouse] + 0.3k)` (OECD-modified, k ≤ 4 kids). Shown as the stem's pulse and in the
  inspector. 2025 **[proto]**: $1.53T, $22.0K a child.
* **Adult children at home** (dependents, `PersonYear.Dependent`: single, no child, no home, under 25): their own player
  (students, working poor, frontline …) and a **home thread** from the dependent's dot to the mother's (else father's)
  dot: an arc with apex max(y0, y1) + 0.15 + 0.05 × distance, people blue 1 px, alpha 0.18 (0.6 in the families view),
  **width = the allowance** (6.3) at `WidthPerB` with the line floor, pulsing toward the dependent. Drawn at full strength
  in the families view, faintly (the `Dependents` row: 0.12) in the people and crowns views, and in every view for the
  selected or hovered player. **The home dot** (A42): each dependent also has a hollow dot (1.6 px ring, people blue,
  alpha 0.8, the dependent's highlight id) beside its parent's dot, offset outward from the parent's player centre by
  0.02 (golden angle per sibling), drawn wherever the parent's dots are (group `Dots`): an adult child who lives with or
  depends on its parents is shown in the parents' place in the hierarchy as well as in its own. Only 3 of the 260
  dependents with a living parent fall in the parent's player [proto, stand3], so without the home dot the link would be
  invisible outside the families view. 2025: 295 dependents, 259 with a parent alive in a player; 175 receive an
  allowance ($379B).
* **Estates** (`EconomicLives.Estates`, the estate log of 6.2), the events of the window [Y − 4, Y] with Kind ≠ spouse: a **ghost** (hollow grey ring
  r 0.03, 1 px, alpha 0.4) stands 0.35 above the player of year Y whose key begins with the departed's own group and anchor
  (largest by adults; else the group's largest player), ghosts stacking 0.06 apart; an **estate stream** (gold arc ghost →
  heir's dot, apex max + 0.20 + 0.06 d, width = dollars × `WidthPerB`, alpha 0.6 × 0.7^(Y − year), pulse toward the heir),
  aggregated per (ghost, heir player). 2025 **[proto]**: 158 parent estates 2021-2025, $5.61T nominal (35 in 2025, $1.86T).
* **Inherited share**: an inner ring on each disc (radius 0.6 r) with a gold arc of the share of members who have
  inherited from a parent (`EconomicLives.InheritedFromParentsBy`). 2025 **[proto]**: clouds 0.86, crowns 0.67, gov tread
  0.50, floor 0.26, slopes 0.18 (revision 1's census; the crowns' share is re-measured with the seats): generational
  wealth reads as height.
* **Crowns passed down**: the ledger's heir rule. The lives send a business to a surviving spouse and to children as
  financial wealth, so in this model control usually becomes diversified wealth (a crown's estate becomes a cloud);
  successions are counted and, when one falls in the window, its stream runs from the ghost at the crown to the heir in
  the same crown ("passed to an heir in <year>").

### 3.8 The player glyph (`Layers/PeopleLayer.cs`)

Placed at `Player.Pos` with `Player.Radius`: the disc (veil + rim, the tribe's dash pattern: D solid, R dashed, I
dotted, M dash-dot), the members' dots (sunflower slots `HillFigure.DotSlot`: the bowl's `Figure` reads the bowl's polar
place, so the hills use the contract twin of 11.2 at `Pos`; height 0.03 + 0.25 × reason), spouse links, the OS line
(reason 0.5), the head ring (`HillFigure.Head`), the mirage (fantasy share), the standing marks (5), the inheritance ring
and the **debt ring** of floor and slope discs (A45: the notebook's "tracking assets, debt"): a dashed gold ring 0.005 under the disc (dash 0.03
on, 0.02 off, 1 px, alpha 0.45; the memory shelf's "hanging" debt look), radius r × (1 + 0.6 × min(1, D / $250K)) with D
the members' debt per adult (mortgage + consumer), so a ring that reaches 1.6 r is a player with $250K a head or more owed;
group `Debt` (people and families views; its hover "debt $58K an adult, consumer $14K"). **Dots: gold for members in the
top 1% (WealthGroup 3), people blue otherwise** (the lifelines use the same rule, 8.5). Slope players stand on their ledge; crown players are drawn by `CrownsLayer` (their dots around the
band); cloud players as lenses. `PlayerSet.DotAt[person]` (land-local) is the one source of every dot's position, used by
the cut's morph (9), the families layer, the signals and picking. Anchors: `land:player:<key>`, `land:group:<group>`,
`land:owners` (the 1% and business owners: the tour's end card), `land:crown` (every crown) and `land:crown:<industry>`.
During a year tween (6.6) the layer draws **glide markers** (a disc ring and the head dot per player, ≤ 4K vertices
rebuilt per frame) from the related player's old position to the new one, then cross-fades to the new glyphs.

---

## 4. The flows (WP1)

The terrain part's flows (`part-terrain.md` 4) with the merged places (A8, A30, A31). Every flow is a `FlowPath`
(land-local points, dollars attached; smoothed by Catmull-Rom through its nodes + 2 Chaikin passes, sampled every
≤ 0.08 u). Width is always dollars; aggregation never changes a total.

### 4.1 The accounts (`Land/Hills/MoneyAccounts.cs`: geometry-free)

Per player, from its members' records (the bowl's rules of `MoneyRouting.Build`, re-implemented without geometry; A19):
wages by paying industry and named employer (Y ≥ 2024, split in expectation by US employees), business income by
industry, **capital income by paying hill** (each member's capital income × `CapitalSources.Mix[its wealth group]`;
**for cloud players by their holdings shares** `Holdings_p,k / Σ Holdings_p` so the vapor fan matches where the cloud
sits; **for crown players, capital and business income both at their seat's hill**: the ledger's seat is the stake the
line's wealth stands for, so its income is that hill's payout, A36), transfers (Social Security and the Medicare share federal, the rest state & local), taxes, spending by the six
categories with fear and fantasy dollars (`MoneyRouting.SplitMotives`, run by the census as today), gross saving and
gross borrowing (members who save / dissave; gross saving split into **trading assets** (stocks, crypto, home equity) and
**deposits and pensions** (deposits, retirement, own business) by `spending.json`'s saving items of the year: 2025 51% /
49%), **allowance in and out** (6.3). The RAS seller matrix
(`SellerMatrix.Balance` on the players' domestic category totals) gives each player's dollars per (sink hill) and
imports: f_pk = Σ_c s_pc C[c][k] (1 − import_c). Pools (lakes' inflows by hill with fear and fantasy parts), payouts by
sector, payouts abroad (`CircuitYear` links "payout"), credit, net lending X = saving − credit, production taxes by hill
(taxShare × VA) and corporate taxes (`government.corporateTax`), the investment estimate (4.4). **The payouts through
each crown** are drawn by `HillFlows.Build` (after the placement, from `PlayerSet.Crowns[k].ControlledShare`, 3.5):
CrownIn_k = max(c_k × (PayoutBySector_k + the hill's share of payouts abroad), Seated_k) (Seated_k = the crown players'
capital and business income from k), of which Seated_k ends at the crown and the rest goes on to the hill's other
recipients in proportion to what each receives from k (A38); no account changes, only the path the dollars take.

**Each player's identity**: wages + business + capital + transfers + allowance in + borrowing = taxes + spending + gross
saving + allowance out (≤ 0.5%). Totals 2025 = the bowl's **[log]**: wages $15.73T, business $2.11T, capital $5.33T,
transfers $4.95T, taxes $5.25T (federal 87%), spending $21.63T (abroad $2.27T), saving $2.16T, borrowing $0.93T (net
$1.23T), payouts abroad $435B; the allowance moves money inside the household sector and leaves every national total
unchanged (6.3).

### 4.2 Wages: runoff from the hill's foot (light blue, `LandStyle.Wages`)

* **Floor players**: from the hill's wage band (ρ = 0.5 (√(o + u) + 1)) at the azimuth of the destination seen from the
  hill's centre, limited to ±80° around −z and kept ≥ 0.15 u from the capital river's head; then **downhill** by steepest
  descent on H (step 0.04 along −∇H until own height < 0.015: the foot), then level across the tread to the disc (a
  front-row player's stream crosses the river). A player paid by a hill of another tread: the level part follows the
  valley network (4.3's router) from the foot's river point. Named employers: the stream starts at the company mark (a gold
  dot at the source).
* **Slope players**: a short level arc along the slope from the wage band to the ledge.
* **Crown and cloud players** (the 1% who work for a wage): an arc from the paying hill's wage band rising to the player
  (apex max(y) + 0.3), light blue.
* Width dollars × `WidthPerB`, pulses toward the player. 2025 **[proto, stand-in]**: state & local $2.32T, professional
  $1.80T, health $1.63T, trade $1.56T, construction $1.01T, manufacturing $0.98T.

### 4.3 Spending: rivers along the valleys into the lakes (ice / rose, glitter)

**Rule.** Spending runs only on the ground: along the valley rivers, and between benches through the gorges; it never
climbs a hill; it ends in the lake in front of the hill it pays. Only capital climbs a hill. (The legend: "Spending runs
along the valleys into the lakes in front of the hills; only capital climbs a hill.")

**Entry points.** Floor players: a rivulet from the disc's river-side edge straight to the river (≤ 0.5 u). Players above
the floor enter the river of their tread at their `GroundX` by a thin dashed vertical **descent** (the rich spend on the
ground): slope players at their ledge's x; crown players at their hill's x on the hill's tread; cloud players at their
centre's x on the tread nearest their centre's z.

**Router** (`Land/Hills/ValleyNetwork.cs`, deterministic): the graph is the five rivers (sampled every 0.05 in x) plus
the gorges. For each (player p, sink hill k) with f_pk > 0: same tread → along the river from x_p to the lake of k; other
tread → a dynamic programme over the gorge choices of each riser between the two treads (3 per riser), cost = Σ |Δx|
along rivers + gorge path lengths, ties: the smaller x. Each river bin keeps two directed flows (+x, −x), each split
fear / desire / fantasy and by category; each gorge keeps up and down. Dollars from all players and sinks merge in a bin:
**the rivers pool together where people's spending converges**, and each lake holds what reaches its hill. Imports leave
by the nearest side edge of the player's tread (x = ±7, "the border") as a fall over the edge fading to alpha 0 over 1.2 u.

**Drawing.** Each river has two banks: +x flows on the far half (z + 0.06), −x flows on the near half (z − 0.06); each
bank is two adjacent flat ribbons, **ice** (fear dollars) toward the river's centre and **rose** (desire dollars)
outside, 0.004 apart, alpha-blended (not additive) at alpha 0.75, intensity ≤ 1.0; each ribbon has a 0.8 px centre line
with flow pulses (0.8 phase per unit) in its direction. Gorges the same (up on the left bank, down on the right).
**Glitter** = fantasy: one 4-point cross (5 px, intensity 2.6, white-rose) per $5B of fantasy dollars on a segment,
0.04-0.24 above it, golden-ratio placement. The six categories are separate submeshes of the same ribbons, so the rivers
view can light one (the legend's chips BASE: necessities, collective; SELFISH: jeopardy, escapism; MATING: status, growth).

2025 **[proto, stand-in settlements]**: $19.32-19.36T reach the lakes, $2.27-2.31T go abroad; 40% of the dollars stay on
their tread, 35% climb a riser (people paying a higher stratum), 25% go down; the busiest river bin carries $4.9T one way
(0.16 u), $8.2T both ways (0.27 u), on the services tread between professional and finance; the busiest gorge (make →
services at x +0.65) $3.39T down and $2.65T up. Lakes **[log, the bowl's pools]**: health $3.75T (158% of its value added:
overflowing), trade $3.21T (84%), real estate $3.18T (75%), manufacturing $2.10T (90%), hospitality $1.37T (140%); nearly
dry: professional 7%, software 14%, internet 27%.

### 4.4 Capital: a gold river up the hill

* **Investment by industry is an estimate** (the repository has investment by commodity, not by investing industry):
  I_tot(Y) = Σ `finalDemand.investment` × GDP(Y) / GDP(2024); I_i(Y) = I_tot(Y) × CFC_i(Y) / Σ_private CFC(Y),
  CFC_i = depShare_i × VA_i(Y). Every label says "estimate". 2025 **[proto]**: $5.67T; real estate $1.58T, manufacturing
  $517B, trade $455B, professional $351B, media & telecom $282B, health $282B, transport $261B, oil & gas $234B. The gov
  hills get none (public investment is in their upkeep).
* **Funding**: external = the clouds' net lending X = saving − credit (2025: $2.16T − $0.93T = $1.23T), shared by
  I_i / I_tot (real estate $343B); internal = I_i − external_i (the hill's own depreciation allowances and retained
  earnings: real estate $1.24T).
* **Geometry**: the river's **head is at the hill's front foot** (ρ = 1.02 at x_i + 0.25 a_i, the front side); it climbs
  by **steepest ascent** on own_i (step 0.04 along +∇own_i) and ends at the cap rim ρ = √o_i: **capital becomes
  ownership**. The internal part joins at the upkeep ring as a short steel-gold **spring** (ρ = √(o + u) + 0.05, 0.3 long).
  The external part is the river below that junction, fed by a **rain column** above the head (3 dashed vertical gold
  lines 0.05 apart from CloudY down to the head, alpha 0.25).
* Width dollars × `CapitalWidthPerB` (×4: real estate 0.21 u at the cap); gold alpha 0.7, intensity 1.6, pulses
  **uphill** (0.6 phase per unit). One river per hill ≥ $20B (23 of 23 private hills in 2025). Every capital path is
  monotone up (y never falls by more than 1e-4).

### 4.5 Profits: vapor to the crowns and the clouds; rain onto the savers

All payouts leave the hill's **gold cap** as vapor (from points on ρ = 0.3 √o_i, spread so wisps do not stack):

* **through the crown** (every crown, seated or not, A38): CrownIn_k (4.1) rises up the crown's stem as one wisp from
  the cap to the band: **the controlled companies' payouts pass through the controllers' hands**. The seated players'
  part ends at the band (their dots); the rest leaves the crown's top as onward wisps to the same kinds of recipients as
  below (clouds, rain, abroad), so an internet dollar paid to a pension fund visibly passes through Page & Brin's and
  Zuckerberg's crown, and a hardware dollar (c 0.02) does not;
* to a slope player's business income from this hill: a straight wisp across to it;
* to a cloud: a rising quadratic Bézier from the cap (control point (cap x, CloudY − 0.3, cap z)) to the lens: **a fan
  of wisps from many summits converging on one cloud is the picture of owning stakes in many industries**;
* to a floor player (pensions, the middle's dividends, rent and interest): up to the cloud level above the hill, then
  **rain** down onto the player (2 dashed vertical lines from CloudY to the disc, alpha 0.25);
* abroad: wisps drift off the land's back edge at the cloud level and fade over 1.5 u.

Width dollars × `WidthPerB`; gold alpha 0.45, intensity 1.4; small sparkles along the wisps (one per $20B, white-gold).
2025 **[log]**: business $2.11T, capital $5.33T (real estate $1.69T, banking $1.15T, trade $0.51T, manufacturing $0.38T,
professional $0.25T), abroad $435B.

### 4.6 Saving and credit (gold)

* **Saving** ($2.16T): every player with positive gross saving sends two thin gold wisps straight up to the cloud level
  (capital rises; saving "evaporates"), side by side 0.01 apart: **trading assets** (stocks, crypto, home equity: the
  notebook's "TRADING ASSETS (Crypto, Real estate)" under MATING) with white glitter by their fantasy (`spending.json`
  item fantasy: crypto 0.9, stocks 0.2, home 0.15) and **deposits and pensions** plain (4.1's split). The legend's MATING
  chips become status, growth, **trading assets**.
* **Credit** ($0.93T): from the cloud level down onto the **banking** hill's cap (a dashed gold column), then a dashed
  gold stream down the banking hill's face and along the valley network to each player who borrows (dashed 0.06 on, 0.04
  off, alpha 0.5).
* **The capital account at the cloud level** (checked): saving in = credit out + external investment out (2025: $2.16T =
  $0.93T + $1.23T); capital income passes through it unchanged.

### 4.7 Taxes and transfers: the state's ground water (steel)

* **Taxes seep.** Personal taxes ($5.25T): a short steel drip (0.15 u) from each player's ground point into the ground;
  corporate taxes ($452B) drip from the caps; taxes on production ($2.15T) from the upkeep rings. Underground **conduits**
  (steel, y = ground − 0.30 − 0.03 k by rank k) run under each river toward x = 0, then forward under every riser along
  x = 0 to the gov tread, and rise into the mesas: personal taxes 87% federal, 13% state & local
  (`government.stateLocalPersonalTaxes`); corporate federal; production taxes state & local by
  `stateLocalProductionTaxes` / total ($1.88T of $2.15T), the rest federal **[data]**. Water seeps down: everything under
  the ground flows toward the front.
* **Transfers spring.** From the mesas conduits run back under the rivers (0.06 deeper) and rise as a **fountain** at
  each recipient's ground point: a vertical steel jet (height 0.15 + 0.6 × $ / $200B, ≤ 0.9) with a ring at its foot;
  recipients above the floor get a dashed steel vertical from the fountain to the player. Social Security and the
  Medicare share from the federal mesa, the rest from state & local.
* Width dollars × `WidthPerB`, alpha 0.5 underground (visible through the translucent ground), 0.8 above it.

### 4.8 Allowance, child cost, estates

* The **allowance** (6.3) is money from a parents' household to its dependent's household: drawn as the home threads'
  width (3.7) and counted in each player's identity (4.1); it is not a valley flow.
* **Child cost** is a split of a household's own spending (3.7): stems' pulses, no flow.
* **Estates** are wealth, not income: the gold estate streams of the families view (3.7).

### 4.9 Roots: what each hill stands on (underground)

`RootsModel`'s selection is reused (79 flows ≥ $50B carrying 72.7% of the $15.85T between industries; mesh roots for the
rest; own purchases as root balls; other years scale the 2024 table by the buyer's value added) through a new
geometry-free accessor WP1 adds to `RootsModel.cs` (A39): `RootsModel.Select(EconomyData data, int year) → RootSel[]`
(`struct RootSel { RootKind Kind; int From, To, Index, Largest, Rank; double Table, Dollars; }`, in `Build`'s order: the
roots by buyer, then one mesh root per buyer, then the balls), reading the same private table; `Build`, `Line` and
`RootsOf` are not touched, so the bowl's roots cannot change. `HillRoots.Build(data, terrain, year)` (WP1) turns the
selection into `RootGeom`s (the existing type: `Points`, `Width`, `Lowest` filled with the hills' geometry) in
`TerrainGeometry.Roots`; its log line checks that Σ dollars of the selection = the table's kept total scaled to the year
(as `RootsModel.Line`'s check does). Geometry: leave the
supplier's underside at its centre (y = BenchY_s − 0.25), travel at y = G − 0.45 − 0.012 k (k = rank among the buyer's
roots), x and z lerping, rise vertically into the buyer's centre from below; root balls: a closed loop under the hill at
ρ = 0.6, 0.12 below its bench. Width × `RootWidthPerB`, the supplier's tier hue (`LandStyle.RootTint`), alpha 0.35,
pulses toward the buyer. The direction honesty of 2.3 is printed in the roots line.

### 4.10 Identities checked every build (`HillFlows.Check`, `MoneyAccounts.Check`)

| # | Identity | Tolerance |
| --- | --- | --- |
| 1 | each player: in = out (4.1, the allowance included) | ≤ 0.5% |
| 2 | Σ wage streams = Σ players' wages; Σ vapor = business + capital income + payouts abroad | ≤ 0.1% |
| 3 | every river bin and gorge: in = out at junctions | ≤ $0.01B |
| 4 | Σ lakes + abroad = Σ spending | ≤ 0.1% |
| 5 | cloud level: saving = credit + external investment | ≤ 0.5% |
| 6 | Σ capital rivers = I_tot; Σ external = X | ≤ 0.1% |
| 7 | taxes into the mesas = Σ taxes; federal / state & local split = the circuit's | ≤ 0.1% |
| 8 | spending paths never on a hill (no point where any hill's own height ≥ 0.05); capital paths monotone up (y never falls by > 1e-4); no disc touches a river ribbon or a lake | 0 violations |
| 9 | Σ hill areas = AreaGdp (hills with VA > $1B); summits strictly ordered by tier | ≤ 0.01 u² / exact |
| 10 | Σ allowance in = Σ allowance out; national disposable income unchanged by it | ≤ $0.01B |
| 11 | the accounts' totals = the lives' (`EconomicLives.Aggregate`): wages, spending, taxes, net saving | ≤ 0.1% |
| 12 | every crown: vapor in = seated players' part + onward wisps; Σ vapor with the crowns as junctions = identity 2's total; every crown ≥ $100B has an inflow ≥ $0.5B (drawn at the line floor) | ≤ $0.01B / 0 violations |
| 13 | saving wisps: trading assets + deposits and pensions = gross saving, per player | ≤ $0.01B |

### 4.11 Aggregation and levels (the first build's legibility lesson)

1. **One story per view at full strength**; everything else ≤ 0.15, except the land itself (ground, hills, caps ≥ 0.35)
   (10.3).
2. Dense flows aggregate outside their own view: wages one stream per (hill, destination tread) to the dollar-weighted
   mean x of its paid players there (~40 in 2025, against ~180 per player); vapor one wisp per (hill, kind: crown / cloud
   / rain / abroad); saving, transfers and taxes per frontage. In their own views (people for wages and vapor, capital for
   vapor, saving and credit, state for taxes and transfers) one path per (hill, player) ≥ $5B (vapor ≥ $10B), smaller
   amounts merged into the player's largest path of the same kind (nothing dropped). `FlowPath.Level` = 0 (aggregate) or
   1 (per player); the layer builds both and the view's emphasis picks.
3. Labels are generated from the numbers (`LandFacts`, ±10% band for "about the same").
4. No hue is shared between meanings: gold capital only, light blue wages, ice / rose motives, white fantasy, steel state,
   tier hues for ground and roots, people blue for people.

---

## 5. The season of tit for tat on the terrain (WP2; the model unchanged)

`SocialSeason` and `Coalitions` run unchanged on the year's players (96 rounds, the GSS-calibrated openings, noise 0.02,
the shadow of the future 0.95, forgiveness by the higher OS, partner choice, tribal memory; the betrayal season of round
48, computed lazily). Its exposure rule reads `Player.Anchor` (the industry that pays a player; −1 for clouds and
comfortable retirees, so their dealings follow group and generation) and, when records exist, the members' most common
employed industry: unchanged. Positions are not inputs to the season. `SeasonLayer` (order 54) draws it on the terrain.

### 5.0 The season's runtime duties (moved from the bowl's `SocialLayer`)

`SocialLayer` is retired with the bowl, but it did more than draw; `SeasonLayer` takes over each duty with the same
semantics (its code may start as a copy of `SocialLayer`'s non-drawing parts):

| Duty | In `SocialLayer` at HEAD | In the hills |
| --- | --- | --- |
| The season on screen, for the UI | `static SocialSeasonResult Shown`, `static int CoalitionsAt(season, round)` | **`Land/LandSeason.cs`** (WP0, a contract): `LandSeason.Shown`, `LandSeason.CoalitionsAt` (the same code, copied), `LandSeason.SetShown(season)` (main thread, called by `SeasonLayer` at each swap and with `null` on destroy). The UI reads `LandSeason.Shown ?? SocialLayer.Shown` (the bowl's branch keeps working) |
| Which season shows | `SeasonOf(s)`: the betrayal season when `LandView.ShowBetrayal` and it exists, else the society's; the viewer's own incident season over both | the same rule; built ahead on a worker (the society's while the betrayal shows and back) and swapped at once |
| The betrayal season of a snapshot published while the betrayal view shows | `LandService.Betrayal(false)` once per snapshot version | the same call |
| The viewer's incident ("betray on this tie") | takes `EconomyState.PendingIncident`, runs `SocialSeason.Run(data, s.Land, players, settings, year, incident)` on a worker (the selected player betrays at the next round; at the season's end at round 48), then `LandView.Replay()` or `LandView.Play(true)` | the same, with `land` = `null` (the season does not read it) |
| Readiness | `LandService.ReportReady("SocialLayer", version)` at each swap | `LandService.ReportReady("SeasonLayer", version)` at each swap, so `ShownYear` waits for the season too |
| Round ticking | none (`LandView.Advance` in `LandViewLayer` ticks the round) | unchanged: `LandViewLayer` (WP5) |
| Tie signals | pain / relief / satisfaction flashes on ties | the same looks, named tie breaks / mends / holds (5.2) |

Checked: the `SeasonLayer.Prepare` log line and its ready report (hc_people `log`); the society view's panel is
populated (WP5's scratch-harness run with `Economy/UI` compiled: `SocialPanel` shows "4 coalitions" in `society`, 12).

### 5.1 Coalitions as territories (`Land/People/Territories.cs`)

```
F_c(x, z) = Σ_{p in coalition c, zone Floor or Slope} exp(−|(x, z) − (x_p, z_p)|² / (2 σ²)),  σ = 0.40, cut at 3σ
grid      h = 0.04 over the land (350 × 379); a cell's owner = argmax_c F_c when max_c F_c ≥ τ = 0.35, else none
boundary  marching squares on each coalition's indicator → polylines → Chaikin × 2 → draped at H + 0.012
```

Every player weighs 1 (presence, not size). Drawn: a boundary line (1.4 px, the coalition's color: the bowl's palette
`SocialLayer.CoalitionColors` copied into `StandStyle`, alpha 0.55) and a fill veil (alpha 0.05, the grid's cells merged into runs per row). Crown and
cloud players carry a ring in their coalition's color at r + 0.03 (they have no ground). One label per coalition at the
area centroid of its largest component (the existing text: "Coalition 3 · 28 players · trade, manufacturing · 77% inside ·
cooperation 0.73"). Territories are computed for each detection round (0, 24, 48, 72, 96) in the snapshot and cross-fade
over 0.6 s during playback of the season.

### 5.2 Ties

The same pair selection as the bowl's betrayal view (each player's strongest tie and the ties among both players' three
strongest; ≈ 120 of ~1,000 pairs with ~130 players); the **society view draws less**: each player's strongest tie (≈ 110)
and the 12 strongest ties across coalitions. Arcs between tie anchors (floor and slope: the disc centre + 0.30; crown: the
band's centre; cloud: the lens' underside):

| Tie | Apex | Style |
| --- | --- | --- |
| within a coalition | max(y0, y1) + 0.10 + 0.05 d (hugs the land) | blue, width and brightness by mutual cooperation (as the bowl) |
| across coalitions | max(y0, y1) + 0.30 + 0.10 d (a bridge over the valleys) | violet |
| feud (mutual < 0.12) | as its kind | grey, the middle missing (as the bowl) |

Standing marks (alpha's second ring, omega's dimmed head, anti-alpha's tick) are drawn at the new positions. The tie events
of the season keep their looks and are named **tie breaks** (a tie's cooperation falls ≥ 0.05 in a round), **tie mends**
(a feud ends) and **tie holds** (mutual cooperation held 12 rounds) in the legend and the hovers; the notebook's pain,
relief and satisfaction are the program's life events (6.4). The betrayal view's incident tie is the brightest arc of its
view.

### 5.3 Clutter budget

In the society view the land's other groups ease to: ground and hills 0.35, flows ≤ 0.05, crowns 0.4, clouds 0.5,
children, threads and estates 0. Never more than ~120 tie arcs, 4-6 territories, 6 labels, 6 ladders and pyramids.

### 5.4 Why the season keeps no memory across years

The season stays one year's game, solved per year from the year's GSS trust. Chaining seasons would make a 2025 preset
depend on 75 earlier seasons (≈ 5 s blocking) and the yearly calibration would erase most of the carried memory. The
memory that carries the game from year to year is the program's (6): balance sheets, buffers, saving history, estates,
allowances, and **each adult's cooperation, which feeds the next year's drives** (6.3b: the households' game, which the
lives already play every year against spouse, neighbours and the other tribe, is what closes the loop; the players'
season on the land shows the year's dealings between groups and is not fed back). Stated in the docs and the PLAY line.

### 5.5 The games page on the land: ladders and pyramids (group `Games`, WP2)

The notebook's games page (single-round Nash → the defection diamond; repeated tit for tat climbing a ladder to Pareto
efficiency; tribal tit for tat as pyramids of standing) gets one small mark per coalition, beside its territory label
(society and betrayal views):

* **The cooperation ladder**: a vertical ladder 0.6 u tall standing on the ground 0.25 to the label's right: two white
  rails (1 px, alpha 0.4) and 13 rungs for rounds 0, 8, …, 96 (bottom to top), each rung's lit length = the mean
  mutual cooperation min(c_AB, c_BA) over the coalition's in-group pairs at that round (`SocialSeasonResult.CoopAB/BA`),
  colored in the coalition's color, the current round's rung brighter (intensity 2). At the foot a small hollow diamond
  "one round: both defect (Nash)"; at the top a tick "mutual cooperation (Pareto)". In the betrayal view the incident's
  coalition's ladder shows the dip and the climb back (the echo and its end by forgiveness).
* **The standing pyramid**: four stacked bars 0.3 u wide beside the ladder, top to bottom alpha, beta, omega, anti-alpha,
  each bar's length ∝ the count of the coalition's players with that standing (`SocialSeasonResult.Standing`), with their
  marks' looks (alpha's second ring, omega's dimmed head, anti-alpha's tick) at the bar's end.

Anchors `land:ladder:<coalition>`; label text "Coalition 3 · ladder: round 0 0.41 → round 96 0.73 · 2 alphas, 21 betas,
4 omegas, 1 anti-alpha" in the hover.

---

## 6. The mind: each player's program, and the game over the years (WP3)

The mind part's design (`part-mind.md` 1-5): the lives already run the desire / fear program per household per year;
this part names its stages, records them, adds what the notebook asks for that the lives lack (the allowance, the seven
neurochemicals, the modes, the signals), sums it per player and plays the years.

### 6.1 The program (per household, per year: the lives' year, named)

| # | Stage | Takes | Computes (the lives' code, `LivesSimulation`) | Gives |
| --- | --- | --- | --- | --- |
| 1 | **MEMORY** | last year's record | balance sheet at the start (financial, home, business, mortgage, consumer debt); runway; debt service; the thin-buffer factor; saving history; inherited so far; the parent's rank | the start of year |
| 2 | **WORLD** | the era (rates, prices, employment, wage index), the family log, deaths | employment, industry, earnings, business, capital income, Social Security, Medicare, transfers, **estates in, allowance in / out (new)**, taxes (each calibrated to the year's national accounts) | income by source, disposable income |
| 3 | **FEEL** | traits, age, married, parent, buffer | 10 drive weights (the household's mean); stress = 1.0 × debt service / 0.4 + 0.5 × jobless (22-61) + 0.4 × buffer; **chemicals (new, 6.4)** | drives, stress, chemicals |
| 4 | **OS** | propensity, maturity, stress, the year's shift | reason (the higher OS when ≥ 0.5), future; **modes (new, 6.4)** | reason, future, modes |
| 5 | **DECIDE** | income percentile, traits, reason, drives, children, age, interest, the year's saving rate and PCE mix | saving rate = base + traits + age + kids + shift; spending; six category shares from their tilts (trait, drive, children, age, interest); fear and fantasy dollars | saving, six categories with fear and fantasy |
| 6 | **ACT** | saving, balance sheet, mortgage rate, APR, equity and home prices | principal; repay or borrow; discharge; equity share; gains; business value; home sales and purchases | the end of year |
| 7 | **PLAY** | strategy, spouse, tribe, the year's mix of strategies; at the player level the season (5) | each adult's cooperation (the lives' `Cooperation`: spouse 0.40, community 0.35, the other tribe 0.25, at HEAD); at the player level standing and coalition | **next year's FEEL** (6.3b) |
| – | **OUTCOME** | 1-7 | agency (0.35 autonomy + 0.15 saving + 0.10 debt + 0.25 reason + 0.15 fantasy), in control (agency ≥ the calibrated cut with autonomy); **signals (new, 6.4)** | |

**The loop closes across years** (the game): stage 6's balance sheet is next year's stage 1; a thin buffer this year
raises next year's fears of starvation and exposure and lowers next year's reason; debt service is next year's stress;
wealth built by saving and gains moves the household up the wealth groups, which the census turns into a new place on the
land (floor → slope → crown / cloud); estates carry memory from one generation to the next; allowances carry income from
parents to their adult children; **last year's cooperation sets this year's fear of isolation and desire for the
collective** (6.3b), which move the spending categories and the fear share. No stage is decoration: each stage's output
is read by a later stage or by next year's.

### 6.2 The record and the trace (no number changes)

`PersonYear` gains (written in `RecordHousehold` / `Settle`, money split evenly in a couple as every money field is):
`Financial`, `Home`, `BusinessEquity` (so `Wealth = Financial + Home + BusinessEquity − Debt` exactly), `AllowanceIn`,
`AllowanceOut`, `Borrowed` (new consumer debt), `Repaid` (principal + consumer debt repaid), `Gains` (holding gains),
`DischargeAmount`, `ClosureLoss` (float $), `Dependent` (bool). `LivesSimulation.Bequeath` appends an **estate log**,
one entry per heir: `struct EstateEvent { short Year; int From, To; float Amount; byte Kind; }` (Kind 0 spouse, 1 child,
2 grandchild, 3 parent, 4 sibling), exposed as `EconomicLives.Estates` (sorted by year, then From, then To).

A trace store (`Model/ProgramTrace.cs`), one row per household-year written where the values are computed (inside the
existing `Parallel.For` bodies, each to its own row: deterministic): the drives as used (10 bytes × 250), stress and
buffer, the saving-rate parts (base, traits, age, kids; short 1e-4), the 30 category tilt parts (short 1e-3 log units),
the equity share, flags (floor bound, shock, bought home, sold home, defaulted, dependent, received / paid allowance).
≈ 110,000 household-years × 90 B + a row index = **10.7 MB**. `EconomicLives` exposes `TraceOf(person, year)`,
`CalibrationOf(year)`, `AllowanceTotal(year)`, `Estates`.

**Acceptance:** with the allowance off (φ = 0) and the play feedback off (κ = 0) the lives' 27-line report is
byte-identical to HEAD's except the size and timing line **[proto C#, φ = 0: 0 lines differ]**. The switches are the
harness's `WHY_ECON_ALLOWANCE=0` and `WHY_ECON_PLAYFEED=0` (parsed by WP0 into `EconomyState.LivesOverrides`, read by
the lives when they read φ and κ from `psyche.json`; unset in Unity).

### 6.3 The allowance (the first model change)

At the end of `Incomes` (after taxes, before the saving rate), two passes over the heads in index order:

```
pass 1 (reads only): for each dependent household d with a living parent's household P = ParentHead(d), P ≠ d, Yd_P > 0:
    eq_P   = 1 + 0.5 (adults_P − 1) + 0.3 min(4, kids_P)          OECD-modified equivalent adults of the parents
    need_d = 0.5 Yd_P / eq_P                                       the family's standard for one more adult (weight 0.5)
    A_d    = φ · clamp(need_d − Yd_d, 0, 0.25 Yd_P),  φ = 0.5      parents fund half the gap, at most 1/8 of their Yd
pass 2 (writes, index order): Yd_d += A_d; Yd_P −= A_d; allowOut_P += A_d
```

Everything after it runs as today on the new disposable incomes; income percentiles and taxes are computed before and do
not move; national totals do not move (a transfer inside the household sector). φ = 0.5 and the 0.25 cap are **[design]**;
anchors **[recalled]**: parents spend ~$500B a year on adult children 18-34 (Merrill Lynch & Age Wave 2018); 59% of
parents of 18-34-year-olds helped financially in the past year (Pew, January 2024). Measured **[proto C#]**: 1972 $20B
(104 of 153 dependents), 2000 $155B, 2018 ≈ $260B, **2025 $379B (175 of 295 dependents receive, 1.7% of DPI; 263 paying
households; the PMC pays 82%; students receive $128B, the working poor $148B)**. Side effects against HEAD: totals by
source unchanged to the cent; 2025 category factors move ≤ 0.02; the in-control cut 0.843 → 0.845; net worth 2025
$152.11T → $151.10T; top 1% share 31.4% → 31.1%; homeownership 65.3% → 65.5%; one notable changes (indebted #1160 →
#1276); bowl-census players 116 → 118. Every expectation of 11.8 is measured with the allowance and the play feedback on.

### 6.3b The play feedback (the second model change; closes the program's loop, A40)

The lives already play every year: each adult's `Cooperation` (spouse, a neighbour drawn from the year's strategy mix,
someone of the other tribe, discounted by partisan distrust) is written after the year's money and read by nothing but
the year's mean. Revision 2 carries it into the next year's FEEL, the way `prevBuffer` already carries the buffer:

```
after year y's cooperation pass:  z_i = clamp((Coop_i,y − mean_y) / sd_y, −2.5, 2.5) over the year's adults
                                  (prevCoopZ[i]; 0 for a person not adult in y)
in year y + 1's Drives (before each pole is normalized):
    desire[collective] ×= exp(+κ z_i),  fear[isolation] ×= exp(−κ z_i),  κ = 0.15 [design, psyche.json program.playFeed]
```

(In code: the factor enters `DrivesAt` through a new optional argument of the private `Drives` overload, default 0, so
the public `Drives(LivesInputs, …)` used outside the lives is unchanged.) The cooperative fear isolation less and want the
collective more; through the drive tilt of the spending categories (`DriveTiltGain`) this moves a cooperative household's
spending toward the collective category and away from fear-driven categories, lowers its fear share, and moves oxytocin
and cortisol in the mind (6.4). It **does not move totals**: income, taxes, disposable income, the saving rate (which
does not read the drives), spending, saving and wealth are unchanged to the cent (WP3 verifies it: the report's income,
saving and net-worth lines are identical between κ = 0 and κ = 0.15; a difference is a bug), so the census, the
placement, the crowns, the clouds and the accounts' totals are unchanged; the allowance's numbers (6.3) are unchanged. The national category totals stay on the data too (the lives' yearly category factors, `YearCalibration.
Categories`, absorb the shift: each moves ≤ 0.02 [to be measured]); what moves is **who** spends on what: the
cooperative households' collective share rises, the isolated households' falls, and with it the lakes' fear shares, the
chemicals and agency (its fantasy part). The trace records the factor (`TraceRow.PlayFeed`, a short 1e-3 log units). The
listing's PLAY line says what it fed: "PLAY tit for tat · opens 0.31 · forgives 0.08 · tribal memory 0.38 · cooperation
0.73 · beta · households 0.68 → next year isolation ×0.97, collective ×1.03" (illustrative factors: exp(∓0.15 z) at z = 0.2). The numbers of 4.3's fear shares and of
6.4-6.5 were measured with κ = 0 [proto] and move slightly; WP3 re-measures them.

Logged: "[Why] Lives play feedback: κ 0.15; 2025 collective share of the top / bottom cooperation quintile a% / b%;
category factors max |Δ| ≤ 0.02; totals by source unchanged; checks 2/2 PASS" (the numbers measured by WP3).

Not in this build (A40, 11.5): a what-if lever that reruns the lives from a year with a changed parameter (forgiveness,
φ, a saving shift; the lives take ≤ 790 ms) and plays the years against the base. It needs a second `EconomicLives` and a
second snapshot cache; the docs offer it as the follow-up.

### 6.4 Chemicals, modes, signals (`Model/MindModel.cs`: derived per adult-year, lazy, LRU 16 years)

* **Chemicals** (an index of what drives the spending, not a measurement): a_k = (1 − fearShare) desire_k (k = food,
  collective, law, shelter, sex), a_{5+k} = fearShare × fear_k (starvation, isolation, murder, exposure, childless);
  chem_c = Σ_k a_k M[k, c] with M[k, c] = 1 / |chemicals(k)| for psyche.json's memberships; cortisol += 0.25 × stress,
  adrenaline += 0.25 × [Loss > 0 or Discharged] (gains [design]); Index_c = chem_c / (the 2025 adult mean). [proto] 2025
  cortisol 0.60-1.52 across players (the 1% 0.60-0.70); 1972 vs 2025 adult means: dopamine 1.12, serotonin 1.13,
  endorphins 1.13, oxytocin 0.99, testosterone 1.02, cortisol 0.88, adrenaline 0.90.
* **Modes** (a time budget calibrated to psyche.json's shares: converse 0.14, learn 0.05, task 0.34, deep thought 0.05,
  rumination 0.12, daydream 0.30): m_k = share_k × mult_k with converse exp(0.30 E) × (married 1.10 : 0.95), learn
  exp(0.30 O) × (age < 25: 2.0; < 35: 1.2; else 1.0), task (employed 1.20 : 0.70) × (kids 1.10 : 1.0), deep thought
  exp(0.60 reasonZ), rumination exp(0.40 stressZ − 0.30 reasonZ + 0.20 N), daydream exp(−0.20 reasonZ); then 30 rounds
  of iterative proportional fitting (rows sum to 1, each mode's adult mean = its share). Coefficients [design]. Modes do
  not feed back into money.
* **Signals** (life events; an adult counts once per signal per year): **pain** = a large uninsured bill (Loss > 0),
  consumer debt discharged, lost the job (employed last year, not this year, 22-61), widowed (a spouse's estate, EstateLog
  kind 0); **relief** = consumer debt paid down to 0 from > 0, back to work (22-61); **satisfaction** = saved ≥ 10% of
  disposable income three years running, bought a home, a child born this year (`SmvPerson.Birth`, mother or father).
  [proto] 2025 pain 7.0%, relief 4.8%, satisfaction 14.3% of adults (±2 pt with widowhood and births); 1972 8.1%, 7.6%,
  37.2%.

### 6.5 The player's program (`Land/Mind/ProgramModel.cs`) and its listing

`ProgramModel.Build(model, players, season, year)` returns one `ProgramYear` per player and one for the nation (and, on
demand, a person's household). Dollars are sums of the members' records (× 100,000 / 1e9 → $B); drives, stress, reason,
future, agency, chemicals, modes, β, λ, comparison are adult means (one person, one mind) with p10 and p90; fear and
fantasy shares spending-weighted; the saving-rate parts disposable-weighted; tilt parts spending-weighted; signals, the
higher-OS share, in control, married, with children and inherited are shares of adults; PLAY is the season's per-player
values (opening, forgiveness = higher-OS share / 3, tribal memory = 0.5 × polarization × (1 − higher-OS share),
cooperation at round 96, standing, coalition). A household split across two players contributes each adult's half to
each. The contract is `ProgramYear` (11.2).

**The listing** (`ProgramModel.Listing`): 9 lines (a header and the 8 stages; FEEL, DECIDE and PLAY wrap to a second line: 12 printed lines) generated from the numbers (`LandFacts` formats; drives in descending
weight; the top tilt parts with |part| ≥ 0.05 named; comparisons with the nation in `LandFacts`' ±10% band):

```
frontline · trade · I · 2025 · 6.8M adults, 3.7M children, 7 adult children at home
MEMORY  net worth $99K/adult (median $32K) · debt $58K (consumer $14K) · runway 0.0 yr · own a home 40% · inherited 7%
WORLD   wages $341B · capital $36B · transfers $80B · allowance in $7B · taxes $70B → disposable $397B
FEEL    fear 55%: exposure .32 starvation .29 murder .23 isolation .15 · desire 45%: shelter .32 food .25 collective .25
        stress 0.64 → cortisol 1.08 · testosterone 1.11 · oxytocin 0.89 · rumination 13% · task 36%
OS      reason 0.36 (24% on the higher OS) · future 0.55 · present bias β 0.91 · loss aversion λ 2.13
DECIDE  save 2.1% · spend $389B: necessities 42% · jeopardy 31% · status 13% · escapism 8% · collective 4% · growth 1%
        fantasy 16% · allowance out $2B
PLAY    tit for tat · opens 0.31 · forgives 0.08 · tribal memory 0.38 · cooperation 0.73 · beta
        households 0.68 → next year isolation ×0.97, collective ×1.03
OUTCOME agency 0.39 · pain 6% · relief 1% · satisfaction 7%
```

(The OUTCOME line says agency, not "in control"; the in-control share is in the hover and the inspector, A26.) Five
2025 examples [proto C#]: `frontline|trade|I` stress 0.64 → cortisol 1.08, reason 0.36, save 2.1%, agency 0.39;
`pmc|health|I` 0.33 → 0.82, 0.47, 6.7%, 0.56, allowance out $25B; `out_of_work|safety_net/genx|I` 1.28 → 1.47, 0.26,
−8.6%, 0.35; the largest cloud player ~0.01 → 0.62, 0.58, 26.9%, 0.86; `students|schools/genz|I` 0.61 → 1.08, 0.30,
−3.5%, 0.28, allowance in $68B.

**Following people across years**: players are rebuilt every year; a selection follows its people: on a year change the
selected player becomes the new year's player holding the most of the old selection's adults alive in both years (ties:
more people, then the lower index); none → the selection clears. `ProgramModel.Related(prevSet, nextSet)` gives that
mapping for every player (the tween partners and the mind's trails). Logged: "[Why] Selection 2024 frontline|trade|I →
2025 frontline|trade|I (61 of 66 members)".

### 6.6 The game: the years play (`Land/Mind/LandPlayback.cs`, `Land/LandTween.cs`)

| | |
| --- | --- |
| Range | 1950 (`EconomyState.MinYear`) → 2025; 2026 (an H1 estimate) only by `.`, marked "estimate" |
| Controls | `Space` play / pause (not while the tour runs); `,` `.` step (existing); speed 0.75 / 1.5 / 3 s a year (default 1.5) |
| Step | at the end of a year's dwell: `LandService.Request(Y+1)`; `LandService.Prefetch(Y+2)` builds into the cache without publishing (`CacheYears` 6) |
| Never skips | if Y+1 is not built when its turn comes, the clock waits (logged "[Why] Playback waited n ms at 1987") |
| On publish | `LandTween.Begin(prev, next, related)`: 0.8 s, ease in-out cubic; the signals of Y+1 fire; the mind (if shown) morphs |
| Stop | at 2025, or any preset change, pick or drag |
| Harness | `WHY_ECON_PLAY=1972:2` starts playback at 1972 with blocking builds for 2 steps (the steps land at fixed frames) |

**What moves during the 0.8 s tween** (A14): the terrain surface's vertex heights and colors lerp between the two years'
fields (fixed grid topology; ≤ 1.5 ms a frame); players, crowns and clouds glide as **markers** (a ring at each
player's / crown's / cloud's position lerped from its related partner's, ≤ 4K vertices a frame; a player without a
partner grows or shrinks at its own position), then the new glyphs, crowns and clouds cross-fade in over 0.25 s;
contours, rims, lakes, flows, roots, families and territories cross-fade over 0.4 s.

**Signals on the land** (`Layers/SignalsLayer.cs`): when a year lands, every player with a signal share ≥ 3% flashes at
its head (`Pos` + 0.32 up): **pain** an ice ring expanding 0 → 0.25 u over 0.6 s (alpha 0.7 × share / 0.2, ≤ 0.7);
**relief** a white pulse rising 0.3 u; **satisfaction** a rose-gold ring holding 0.6 s; players in index order with a
15 ms stagger, one mesh rebuilt per frame (≤ 3K vertices). In 2008-2010 pain fires across the floors; in 1950-1972
satisfaction dominates.

**The score** per player and year, over the members adult in both Y−1 and Y: ΔW = saving + gains + estates (from parents
and spouses) + debt written off − business closures + family residual (what pooling and splitting balance sheets moved
between people: marriage, divorce, a spouse joining another player; printed, never hidden). The panel shows it as a
stacked bar ("2008 · frontline · trade · I: −$41B: saving +$6B, gains −$58B, estates +$9B, written off +$3B, closures
−$1B"). The nation's sum equals the lives' net-worth change (the lives' money check) within $1B, after arrivals and
estates leaving the record.

---

## 7. The separate 3D mind view (WP3)

The mind part's view (`part-mind.md` 6) unchanged except A26 (the agency label). Mockups at this geometry with 2025
numbers: `$SP/redesign2/mind/mock-player-frontline-trade.png`, `mock-player-top1.png`, `mock-population.png`.

### 7.1 Placement (`Land/Mind/MindFrame.cs`)

**Mind-local coordinates** are the mind part's: x right, y up, **z front** (toward the default camera, i.e. toward the
road). All numbers of 7.2-7.8 are in these coordinates. `MindFrame.ToLand(x, y, z) = (−13.5 + x, y, −z)` places them in
land-local coordinates: the origin is land-local (−13.5, 0, 0) (6.5 u left of the land's left edge, on the floor) and z is
mirrored so the brain's front (prefrontal pole, decisions, gate) faces the road and the camera; the fear hemisphere
(x < 0) is on the viewer's left. The mirror is safe: `Why/Surface` is `Cull Off` and lines have no winding [code].
Extent x −3.2..3.2, y 0..4.2, z −4.8..4.9. World-space raw geometry (`GraphMaterials.Raw`, `Why/Line`, `Why/Surface`),
group `Mind` (the population mode's particles `MindPopulation`, the tether `Tether`). In the overview the brain stands
faintly beside the land (alpha 0.3); land views hide it.

### 7.2 The shell: the brain

Ellipsoid centre C = (0, 2.1, 0), semi-axes (2.2, 1.5, 2.8); two hemispheres each pushed 0.08 off the midline (a 0.16
fissure): **left = FEAR (ice), right = DESIRE (rose)**. Latitude rings at v ∈ {−0.75, −0.5, −0.25, 0, 0.25, 0.5, 0.75,
0.92} (per hemisphere a half ring (s(0.08 + 2.2ρ cos θ), 2.1 + 1.5v, 2.8ρ sin θ), ρ = √(1 − v²), θ ∈ [−90°, 90°], 48
segments); meridians in the planes x = s(0.08 + 2.2u), u ∈ {0.15, 0.40, 0.65, 0.85} (96 segments, segments with
v < −0.8 left out: the brainstem's opening). 1.0 px lines; hemisphere intensity 0.5 + 1.5 × its pole's share (fear share
left, 1 − fear share right).

* **The reason waterline** (the OS): the plane y_r = 3.6 − 3.0 × reason (the lit top fraction = the share of decisions
  made by reason). Segments above y_r: 0.7 white + 0.3 tint, alpha 0.55, intensity × 1.3 (**the higher OS: the lit
  cortex**); below: the tint, alpha 0.35, × 0.8 (**the default OS**); the waterline ring white 2.2 px intensity 2.4,
  label "reason 0.36 · 24% of adults on the higher OS".
* **OS dials** at the waterline's right end (x 2.6): three 240° arcs r 0.14 with needles: present bias β (0.5-1.05),
  loss aversion λ (1-3.5), social comparison (−1.5..1.5 sd); hovers: the biases of psyche.json tied to each.
* **Joint goals** (the notebook's "Joint"): three white arches over the fissure at y 3.55, z −1.0 / 0 / +1.0 (half
  circles r 0.35 in the x-y plane), intensity 0.6 + 2 × share: partnership (married), purpose (future orientation),
  parenting (with children).

### 7.3 The drives (the deep floor)

Ten nodes at y 1.25; pole s = −1 fear, +1 desire; node k = 0..4 in notebook order (food / starvation, collective /
isolation, law / murder, shelter / exposure, sex / childless: each desire faces its fear across the fissure):
ψ_k = −60° + 30°k, P = (s(0.45 + 1.15 cos ψ_k), 1.25, 1.15 sin ψ_k). Glyph: three orthogonal circles (24 segments) of
radius 0.06 + 0.30 w_k plus a horizontal veil disc (alpha 0.15 × activation); activation = pole share × w_k × 5,
intensity 0.6 + 2.0 × activation. Label "shelter 0.32".

### 7.4 The chemicals (beneath)

Seven vials under the brain at x = −1.8 + 0.6c, z = 0.9, c = 0..6 in the order cortisol, adrenaline, testosterone,
dopamine, endorphins, oxytocin, serotonin (fear's chemicals on fear's side): a 3 px vertical line from y 0.10 to
0.10 + 0.42 × min(2.2, index), topped by a circle r 0.05 + 0.04 × index; color by psyche.json pole (cortisol, adrenaline
ice; testosterone half ice half rose; the others rose); intensity 0.8 + 0.8 × index. Links from each drive node to the
top of each of its chemicals' vials (22 links, alpha min(0.6, 0.1 + 2.5 a_k / |chemicals(k)|), 0.8 px, the drive's pole
color). Label "cortisol 1.08"; hover: psyche.json's caveat on the chemical.

### 7.5 The modes (the orbit)

A stepped ring around the shell, ellipse semi-axes (2.55, 3.15) in x-z, arcs in the order task, converse, learn, deep
thought, daydream, rumination (clockwise seen from above from the front), each arc's angle ∝ its time share, at height
2.1 + 0.30 × os (+1 learn, deep thought; 0 task, converse; −1 daydream, rumination), joined by vertical steps; 1.6 px
white (rumination ice-tinted 50%), intensity 0.6 + 4 × share; a clock hand (a bright 0.08 u dot) runs once around per
shown year. Label "task 36%".

### 7.6 Memory and world (behind)

* **MEMORY shelf** at z −4.4, baseline y 1.4 (a gold hairline x −2.0..2.0): five boxes (w 0.36, d 0.24) at x −1.6, −0.8,
  0, 0.8, 1.6: financial, home, business **rising** (solid gold), mortgage, consumer debt **hanging** (gold 0.7, dashed
  edges); height per adult h = min(1.6, 0.45 log10(1 + $ per adult / $10K)) ($10K 0.14, $100K 0.47, $1M 0.90, $10M
  1.35). Start-of-year values morphing to the end of the year in the ACT stage. Above the shelf: a Big Five pentagon (r
  0.35), "inherited from parents 7% · parents' rank 0.55", three saving-history ticks. Five gold hairlines (alpha 0.15)
  from the box tops to the shell's back pole (0, 2.1, −2.8).
* **WORLD intake**: income pipes on the floor (y 0.05) from z −4.3 to the brainstem B = (0, 0.6, −1.0) (cubic Bézier,
  24 points), side by side over 0.9 u in total, widths ∝ dollars: wages (light blue), business and capital (gold),
  transfers (steel), allowance in (people blue), estates (gold, only in a year with one), borrowing (gold dashed); pulses
  toward B. **Taxes leave before any decision**: a steel pipe from B down to a sink disc at (−0.9, 0, −0.4) labeled
  "taxes $70B". **World dials** on the floor at x −2.8, z −4..−2: six 240° arcs (employment rate, mortgage rate, card
  APR, stock return, home prices, GSS trust).

### 7.7 Strategy (in front): the decisions

Each category's conduit leaves the shell where spending.json's `mind` coordinates put the decision (x: −desire … +fear,
y: reason): exit x_e = −1.6 × mind.x, y_e = 0.9 + 2.4 × mind.y on the front surface z_e = 2.8 √(1 − (x_e/2.2)² −
((y_e − 2.1)/1.5)²) + 0.05 (jeopardy leaves low on the fear side, escapism low on the desire side, saving and growth
high). Each conduit is a cubic Bézier (P0, P0 + (0, 0, 1.2), P3 + (0, 0.6, −1.5), P3) to the **gate** at z = 4.3,
y 0.6, where conduits lie side by side ordered by fear share, fear side first: jeopardy (0.85),
saving (0.60), collective (0.55), necessities (0.50), growth (0.35), status (0.25), escapism (0.15), then **allowance out**
(people blue, from B). Total gate width 3.0 u ∝ spending + positive saving + allowance out (composition, not size; the size
is in the labels). A category conduit is **two lanes, ice (fear dollars) and rose (desire dollars), never blended**;
fantasy glitter along the lane (count min(30, 400 × fantasy $ / total), positions `LandMath.Hash01(category, k)`).
Dissaving: no saving conduit; a dashed gold borrowing pipe enters at the intake. Gate labels "status $52B · 25% fear ·
43% fantasy". **Agency**: a gold ring around the prefrontal pole (0, 2.1, 2.92), r 0.30, facing the front, filled to
agency × 360°, label **"agency 0.39"** (the in-control share in its hover, A26). **PLAY**: three concentric horizontal
rings (r 0.18, 0.26, 0.34) at (0, 3.75, 2.3) filled to opening, forgiveness × 3, cooperation at round 96; label "tit for
tat · cooperation 0.72 · beta · coalition 1".

### 7.8 Population mode (`P`)

The shell, drives, chemicals and modes show the **nation's** program dimmed to 0.45; every player is a **particle** at
x = −1.9 clamp((fear − 0.53) / 0.13, −1, 1) (fear left), y = 0.75 + 2.7 clamp((reason − 0.10) / 0.70, 0, 1) (the higher
OS above), z = −2.4 + 4.8 clamp((future − 0.30) / 0.70, 0, 1) (the planners in front) (ranges fixed for all years: player-years 1950-2025 fear
p1-p99 0.437-0.608, reason 0.211-0.686, future 0.429-0.876 [proto]). A particle: two rings (16 segments) of radius
0.03 √(people / 1M) colored by zone (floor people blue, slope blue with a gold rim, crown gold, cloud pale gold-white).
**Trails**: where the player's present members' minds were in each of the last 10 years (members alive, adults), a
polyline alpha 0.35 → 0 backward. Group labels at the people-weighted centroids. [proto, 1950 → 2025]: the 1% rise to
future 0.82 and reason 0.57; the out of work sink to reason 0.26, fear 0.58; Social Security retirees move to the fear
side (0.53 → 0.61).

### 7.9 One year staged (`G`) and the years (`Space`)

`EconomyState.MindPhase` p runs 0 → 1 over 12 s; each stage lights in turn, earlier stages stay lit at 0.6: MEMORY
0-0.125 (shelf, hairlines pulse into the back pole); WORLD 0.125-0.29 (intake pulses, taxes drop to the sink, dials
swing); FEEL 0.29-0.46 (drive nodes brighten 0.2 → activation, links pulse, vials fill 0 → index); OS 0.46-0.58 (the
waterline rises from the floor to y_r, the cortex above it whitens, the clock hand runs once); DECIDE 0.58-0.79 (conduits
fill from their exits to the gate, glitter appears); ACT 0.79-0.92 (memory boxes morph to the end of year, borrowing /
repayment pulses, the agency arc fills); PLAY + signals 0.92-1.00 (play rings fill; pain = ice ripples down the lower
shell, relief = a white pulse brainstem → crown, satisfaction = the waterline glows rose-gold, each with the year's
shares). `G` again jumps to the settled state (p = 1). While the years play the mind does not stage: on each published
year it **morphs** (0.8 s, with the land's tween) from last year's geometry to this year's (fixed topology: every element
always emitted, zero-sized when absent) and the clock hand turns once per year; labels swap at t = 0.5.

### 7.10 Modes of the view; the tether

* **Player mode** (default): the selected player (default: the largest employee player by adult lines, ties by index:
  2025 `frontline|trade|I`, 1972 `frontline|manufacturing|D` [proto C#]).
* **Person mode**: the household of the inspected lifeline (the person inspector's "Mind" button or `M`): MEMORY shows
  the person's own traits, the spouse's dot beside, the children; PLAY the person's own strategy.
* **Nation mode** / **population mode** (`P`).
* **mind-land** (landscape only): the camera between the mind and the land; the selected player's **tether**: its intake
  pipes leave its `Pos` on the land and arc (apex max(y) + 2.5) into the intake; its gate conduits arc from the gate to the
  player's `GroundX` on its river and continue as its real rivulets; widths as in the mind; the land at 0.35 except the
  selected player's flows. In portrait the `people` view shows a 0.6 u stub "→ mind" over the selected player instead.

### 7.11 Meshes and budget

Eight renderers (Memory, World, Feel, Chemicals, Os, Modes, Decide, Play) so the staged sequence only sets `_Alpha` /
intensity per renderer; moving fronts (waterline, conduit fill, vial fill, clock hand, signal ripples) are one small mesh
rebuilt per frame (≤ 4K vertices). Static geometry ≈ 4.6K points ≈ 19K vertices per year (budget 26K); population
particles ≈ 130 × 34 points + trails ≈ 5.5K points.

---

## 8. The population graph: couples and births (WP4)

The SMV part's design (`part-smv.md`), prototyped on a private copy (`$SP/redesign2/smv/proto.patch`, 915 lines) and
rendered (`$SP/redesign2/smv/c5/`, `c5p/`, `c5off/`). Every number below is **[proto]** from that run.

### 8.1 What the viewer sees

* **The mid-plane** is drawn: a faint vertical veil through the stream's centre (KnotColor alpha 0.03, every 4 steps from
  1946 to now, from the floor of the human layer to the top of the value axis) with a hairline along its top (alpha 0.3).
  Women run on its inner side, men on its outer side, as before.
* **Couples run beside the mid-plane**: from the wedding on, both spouses' lines ease to half their distance from it
  (ρ ← c + (ρ − c)(1 − 0.5 r), r eased in over 1 y after the wedding, out over 1 y after its end). A faint **wedding tie**
  crosses the plane; at a divorce a **broken tie** (two half-ties stopping short of the plane) and the lines peel back.
* **Every child born from 1946 on is born on the mid-plane**: its mother's and father's lines bend in over the 1.5 years
  before the birth and touch at one point on the plane, at the mean of their two heights; the child's line starts at that
  point and falls to the children's core (glowing for its first year); a small **bead** (a cross, blue-white, never gold)
  marks the meeting. Two births of one parent ≤ 2.5 years apart keep that parent on the plane between them: a married
  couple with close children braids along the plane.
* **The rule for the viewer**: a line touches the mid-plane only where a child is born; every line starts either on the
  mid-plane (born here) or at the stream's edge (arrived as an immigrant, fading in).
* **One family, legible**: the `couples` view (key 9) draws one real family bright over the dim crowd with four labels
  ("mother", "father", "married 1972", "a child, 1985 · each meeting = 100,000 births"); clicking any lifeline draws that
  person's family the same way.

### 8.2 Scope: a switch only the economy turns on

`SmvBondsOptions` is published by `EconomyLoaderLayer` (order 25) under `SmvPopulation.BondsKey = "smv.bonds"` (the
mechanism of `ISmvLineStyle` under `StyleKey`). `SmvLayer.Prepare` (order 30) reads it; **null (the causality scene) →
nothing changes** (no bonds pass, `BuildParentLinks` as today, meshes byte-identical: verified, `whycheck` "14 presets, 0
differ: PASS", the Why log line unchanged "152948 fine / 15295 coarse … 4283 parent links"). With options: `sim.Run()` →
`style?.Prepare(sim)` (the lives run on the unbent simulation) → `new SmvBonds(sim, options).Apply()` → geometry with
`Bonds` → `BuildBonds()`, `BuildMidPlane()`, no `BuildParentLinks()`. `SmvPopulation.Bonds` is null in the causality
scene. Turning the bonds on in `Why.unity` later is one line plus a new whycheck baseline: offered as a follow-up, not done.

### 8.3 The bonds pass (`Humans/Smv/SmvBonds.cs`; worker, single-threaded, no randomness)

It rewrites `SampleY`, `SampleRho`, `StartY`, `StartRho` in place so every display consumer (the lines, the pickers, the
cut's dots, the selected line, the land's unfold) agrees with the drawing; no model reads the samples [code]. Constants
(defaults = the economy's): `FromYear` 1946, `CoupleSqueeze` 0.5, `CoupleRampIn` / `CoupleRampOut` 1.0 / 1.0 y,
`BendBefore` / `BendAfter` 1.5 / 1.0 y, `BriefBefore` / `BriefAfter` 0.75 / 0.5 y (a father not married to the mother),
`BridgeYears` 2.5, `ChildDescentTau` 0.8 y, `ArrivalEdge` / `ArrivalTau` 1.0 × envelope / 0.5 y, `GlowGain` / `GlowAlpha`
2.0 / 1.2, `NewbornGlowYears` 1.0, `KnotColor` (0.70, 0.80, 1.0), `TieColor` (0.62, 0.72, 1.0). Steps in this order:
1 couples (squeeze), 2 arrivals (from the edge, before the bends: 78 misses otherwise), 3 births (record `SmvBirth {Child,
Mother, Father, Step, Y, Kind}`; Y = the mean of the parents' heights at the step; Kind Married / Unmarried / MotherOnly),
4 bends (pull to the meeting with the biweight K over A = 4 steps before (2 if brief) and B = 6 after (3 if brief); the
bridge between births ≤ 10 steps apart; `glow` bytes), 5 newborns (start at the knot, fall with τ 0.8 y, glow e^(−age)),
6 verify (child, mother, father within 1e-5 of the knot: **0 misses**), 7 the featured family (marriages not divorced,
started 1972-1984, lasting to ≥ 2020, ≥ 2 married births with the median birth year in 1982-1988; lowest score
−min(3, n) × 100 + |median − 1985| + index × 1e-6: marriage 846, wife 1752, husband 1646, from 1972.50, births 1973.00 /
1977.25 / 1985.00 / 1990.25), 8 checksum Σ (y × 1000 + ρ) in index order = 735025915.254744 (three runs).

### 8.4 Geometry (`SmvGeometry`, only when `Bonds != null`)

Lines: glow per point (intensity × (1 + 2.0 g), alpha × (1 + 1.2 g), applied after `Style.Restyle` so the economy's tint
is kept); a bonded child's line starts exactly at its knot; arrivals fade in over 1 y. Fine points 152,948 → 183,161
(+19.8%), coarse 15,295 → 18,291. **Marks** (a new `LineMeshBuilder`, mesh "SmvBondMarks", queue `QueueHumans + 2`): a
bead per birth (a cross on the plane, KnotColor alpha 0.45 (mother-only 0.18), 1.6 px, intensity 1.6, the child's id;
3,090), wedding ties and broken ties (TieColor alpha 0.10, 0.7 px, each half its spouse's id; 2,401): 43,928 vertices.
**Coarse tier**: a bead (2.2 px) for every birth whose child, mother or father is coarse (848); arms (the non-coarse
parent's last 1.5 y into the knot) for every birth with a coarse child (542); the legend: "zoomed out, one line in ten: a
dip to the mid-plane ending in a dot is a child not drawn at this scale".

### 8.5 The economy's side

* `EconomyLoaderLayer` publishes `new SmvBondsOptions()`; the harness-only switch `SMV_BONDS=0` stays for comparison
  renders.
* **The line style** (WP3's `EconomicLives.Restyle`, A15): an adult's line is capital gold in the years its person is in
  the top 1% (`WealthGroup == 3`, gold weight `ControlGold` 0.9 blended across years), otherwise the population's blue
  at `RestBrightness`; heavy fantasy still dims a line. The bonds' glow multiplies after it.
* `SmvLayer.BondsAlpha` (new static, default 1) is set each frame by `LandViewLayer` from the `Bonds` row of the emphasis
  table and reset to 1 when the scene unloads.
* `SmvLayer` adds, only with bonds on, the blurb sentence on `smv:us` ("… Married couples run beside the mid-plane between
  the women's and the men's sides; every child's line starts where its mother's and father's lines meet on the mid-plane
  (each meeting is 100,000 births); immigrants' lines enter from the edge.") and the label "mid-plane: where children are
  born" at (U(1956), HumansY + SmvHeight + 0.01, c(1956)), priority 9, 11 px, TextDim.
* **The `couples` view** (key 9, A12): title "Couples and children", subtitle "Each child's line starts where its
  mother's and father's lines meet on the mid-plane · couples run beside it · 1 line = 100,000 people";
  `PopulationDetail = true`; pose target `OnRoad(t_mid, Ȳ, c(t_mid))` with t_mid = (Start − 3 + last birth + 4) / 2 of the
  featured family (1981.9) and Ȳ the mean knot height (0.79), yaw the road normal from the inner (women's) side, pitch
  35, distance 3.6, PortraitWidth 5.4; before the population exists it falls back to (1982, 0.79, c(1982)) and
  `EconomyPresets.Refresh` repositions it. Every land group 0 and `HideRoadLabels = false` (no bench label may fall on the
  road). Labels (couples view only, on the featured family's bent samples): "mother" (the wife's line at Start − 2,
  priority 30, HumansFemale), "father" (the husband's, 30, HumansMale), "married 1972" (the wedding tie's midpoint, 28,
  TextDim), "a child, 1985 · each meeting = 100,000 births" (the knot of the median birth, 32, Text).
* **Family lines** (`Economy/UI/PersonLine.cs`, WP4, its public API kept: `Show(pop, lives, person)`, `Tick`, `Dispose`):
  the selected person (whole life, alpha 0.75, 1.8 px), each spouse ([Start − 1, min(End + 1, death)], 0.55, 1.4 px)
  with yearly half-ties through the plane (TieColor alpha 0.35), the parents ([k − 8, k] into the person's own knot), each
  child ([k, k + 16]), the beads of these births (3.5 px, alpha 1, intensity 2.5); ≤ 700 points, < 1 ms on the main
  thread. With no selection in the couples view it draws the featured family. `SmvBonds.FamilyIds(person, List<IdRange>)`
  (the person, spouses, parents, children; ≤ 16 ranges) is what `PersonInspector` (WP5) hands to `Highlighter.Set`.

**Contract for everyone** (8.3): with the bonds on, `SampleY` no longer equals `SmvModel.Height(Value)` within ~1.5 years
of a birth or ~3 years after an arrival: never derive social market value from a sample; read `SmvPerson` / the lives.

---

## 9. The cut and the transition (WP5, `Layers/CutLayer.cs`, order 56)

`CutLayer` begins as a copy of HEAD's `SectionLayer` card (frame, card bars, lifeline dots, readout, the year slide, the
callouts) with the bowl's morph replaced by the hills' (A17); `SectionLayer` stays in `Bowl/` untouched. The cut on the
road is unchanged except: **the dots are gold for the top 1% and people blue otherwise** (A15), and the readout reads
"2025 · GDP $30.8T · 274M adults in 2,742 lines · gold: the top 1%" (no in-control share). The callouts run from the
frame's corners to the land's front edge.

One clock `LandView.MorphTime` ∈ [0, 2.4] s (unfold at 1×, fold at 2×), as today, read through **one phase function**
every layer uses (a WP0 contract, so WP1 and WP5 agree): `LandView.Phases(float morphTime, out float sA, out float sB,
out float sC, out float reveal)`, each eased in-out cubic over its window below (0 before, 1 after; the dots' per-player
start is computed by `CutLayer` from `morphTime`). **Who draws what**: `CutLayer` (WP5) draws the card, the flying bars,
**the tiles in phases B and C** (until they have faded at sC = 1) and the flying dots; `TerrainLayer` (WP1) draws the
ground (y × sB in phase B), the hills (y = G + sC Σ own in phase C) and fades its contours and rims in with sC; every
other land layer multiplies its alpha by `reveal`.

| Phase | Time (s) | What moves | Formula |
| --- | --- | --- | --- |
| **A Lift** | 0.00-0.40 | card bars and dots | translate from the cut to the land's front edge (z −7.575, x 0), facing the camera; ease in-out cubic |
| **B Lie back** | 0.30-1.10 | bars → **tiles on the staircase**; the ground grows | bar i (tier t) flies to (x_i, BenchY_t + 0.01, z_i) along a Bézier (control = midpoint + 0.8 up), turning from vertical to horizontal; its size goes from (bar width 0.6, bar height) to the rectangle (√π a_i, √π b_i) by w(s) = w0^(1−s) w1^s, ℓ(s) = k(s) A_i / w(s), k(s) = k_card^(1−s) (area k(s) A_i throughout, exactly A_i at the end); the bar's three strips (wages, upkeep, owners) become three **nested** rectangles of the same areas; the ground mesh is drawn with y × s: the staircase rises out of the plaza |
| **C Rise** | 1.00-2.00 | the hills grow out of the tiles | terrain vertex y = G + s_C × Σ own (per-frame update of the ≤ 31K terrain vertices, ≤ 1.5 ms, done by `TerrainLayer` reading `LandView.Phases`); the tiles (drawn by `CutLayer`) turn into the zone ellipses (superellipse exponent 8 → 2) and fade (1 − s_C) as `TerrainLayer`'s rims and contours fade in |
| **Dots** | 0.40-1.90 | each lifeline dot flies to `PlayerSet.DotAt[person]` | quadratic Bézier, control = midpoint + (0, 1.6, 0); start 0.40 + 0.9 × rank of its player's x / players; 0.6 s; ease in-out |
| **D Reveal** | 2.00-2.40 | flows, lakes, people, crowns, clouds, labels fade in | `LandView.Reveal` 0 → 1 |

Fold: Reveal 1 → 0 in 0.2 s, then C → B → A backward, 1.2 s. The camera flies from the section pose to the landscape pose
in 2.2 s (`GraphRoot.Focus`). Harness: `WHY_ECON_MORPH=0.9` freezes the staircase with its tiles, `=1.5` the
half-grown hills. Budget: the morph mesh ≤ 25K vertices rewritten per frame ≤ 2 ms (the bowl's: 16,446 at 0.75-0.88 ms).

---

## 10. Views at 16:9 and 9:16, and legibility

### 10.1 The catalog (18 presets; `EconomyViews.Ids` in this order)

| Key | Id | Title / subtitle (subtitles never assert the thesis) | Story at full strength | Owner of the pose and the column |
| --- | --- | --- | --- | --- |
| 1 | `overview` | **The economy, 1946 - 2026** / "The road of time; one year cut out and grown into a land" | the road, the cut, the whole land | WP5 |
| 2 | `section` | **The cut through {Y}** / "Every industry's band and every life that pierces {Y}" | the cut | WP5 |
| 3 | `landscape` | **The hills of value** / "Each industry a hill as large as the value it adds, as high as its stratum; gold is what owners keep" | ground, hills, contours, caps | WP1 |
| 4 | `capital` | **Capital climbs, profit rises** / "Investment runs up the hills; profit rises to the crowns and the clouds" | capital rivers, vapor, saving and credit, crowns, clouds, company marks, overlays | WP1 |
| 5 | `people` | **Who sits where** / "{n} players: wage earners on the valley floors, owners on the slopes, controllers in the crowns, the diversified rich in the clouds" (generated) | glyphs, dots, crowns, clouds, wages, vapor | WP2 |
| 6 | `rivers` | **Where the money goes** / "Spending runs along the valleys into the lakes in front of the hills: ice is fear, rose desire, glitter fantasy" | spending, glitter, lakes, imports | WP1 |
| 7 | `mind` | **The program every player runs** / "What {player} took in, felt, decided and did in {Y}" (generated) | the 3D mind | WP3 |
| 8 | `society` | **How people organize** / "Tit for tat between the players: who cooperates, how forgiveness helps, how coalitions form" | territories, ties, standing | WP2 |
| 9 | `couples` | **Couples and children** / (8.5) | the population graph's bonds | WP4 |
| 0 | `families` | **Families and inheritance** / "Children under their parents, adult children at home, estates falling to heirs" | children, home threads, estates | WP2 |
| – | `roots` | **What each hill stands on** / "Purchases between industries rise into each buyer from below" | roots, bedrock | WP1 |
| – | `state` | **Taxes and transfers** / "Taxes seep down to the state; transfers spring up where people live" | seeps, conduits, mesas, fountains | WP1 |
| – | `crowns` | **Crowns and clouds** / "Control sits in the crowns over the hills; diversified wealth floats in the clouds between them" | the vertical stack floor → slopes → crowns → clouds | WP2 |
| – | `betrayal` | **One betrayal** / "Tit for tat echoes it; forgiveness ends it" | the betrayal season's ties | WP2 |
| – | `y1972` | **The land in 1972** / "The same cut, fifty-three years earlier" | as landscape, year 1972 | WP1 |
| – | `mind-pop` | **The nation's minds** / "Every player in mind space: fear to the left, reason up, the future forward" | particles and trails | WP3 |
| – | `mind-land` | **A program and its land** / "The selected player's income enters its mind; its decisions run back to its rivers" | the tether (landscape only; portrait falls back to `mind`) | WP3 |
| – | `mind1972` | **The program in 1972** / as mind | the mind, year 1972 | WP3 |

`ViewSpec` additions: `MindScope Mind` (Player / Nation; `mind-pop` sets population mode), `bool LandscapeOnly`
(`mind-land`). Temporary years: `y1972` and `mind1972` (1972).

### 10.2 Poses (starting values; each owner fits them with the harness until the "must be in frame" list holds)

Land views use `OnLand(land-local target, yaw offset from LandFrame.Yaw, pitch, distance)`; mind views use the mind's
target through `MindFrame.ToLand`. Portrait: the existing rule (distance = max(Distance, PortraitWidth / (2 tan 22.5° ×
aspect)); the target moves down by 0.2 of the visible height so the subject sits in the upper 60% above the bottom
sheet). Vertical FOV 45°.

| Id | 16:9 (target; yaw; pitch; distance) | 9:16 (pitch; PortraitWidth) | Must be in frame (16:9 and 9:16) |
| --- | --- | --- | --- |
| overview | midpoint of `OnRoad(1990, 0.3, FramingRho)` and `World(0, 1.5, 0)`; road normal + 20°; 28; 34 | 28; 30 | the 1950 tick, the cut, the whole land and its back peaks |
| section | unchanged | unchanged | the whole frame and its readout |
| landscape | (0, 1.6, 0.3); 0; 38; 22.5 | 58; 14.5 | the whole land, 5 bench labels, 25 hill labels (≥ 24 at 9:16) |
| capital | (0, 2.4, 2.1); 0; 22; 17 | 30; 13 | make, services and tech ranges, the cloud level, every capital river, the 5 largest crowns |
| people | (0, 2.0, 0.0); 0; 30; 20 | 40; 14.5 | every tread with its rows, the crowns, the clouds |
| rivers | (0, 1.2, 0.6); 0; 62; 24.75 | 66; 14 | every river, gorge and lake; the 6 largest lake labels |
| mind | mind (0, 1.6, 0.2); −40; 16; 15 | 24; 9.5 (distance 21) | the shell, the gate labels, the memory shelf, the vials |
| society | (0, 1.6, 0.6); 0; 60; 22 | 64; 14 | every territory and its label |
| couples | 8.5 | 8.5 | the four family labels |
| families | (−2.0, 1.4, −1.4); 0; 35; 7 | 45; 6 | trade's frontage with its stems, at least 20 home threads, the estate streams of the window |
| roots | (0, 0.9, 0); −24; 14; 19.25 | 20 (yaw 0); 14 | all 79 roots, the bedrock |
| state | (0, 0.6, −3.8); 0; 46; 13.75 | 52; 13 | the gov tread, both mesas, the raw and make treads, the central conduit |
| crowns | **elevation**: (0, 3.42, 0); 0; 0; 22 (the eye at y 3.42, between the highest person (≤ 3.37 with dots) and the crown band (≥ 3.55): every floor and slope projects below the horizon, every crown and cloud above it, so the screen shows the stack [proto, stand3: 10 px between the highest person and the lowest crown bottom, 19 px between the highest crown top and the lowest cloud]) | 0; 15 (distance 30; 14 px, 28 px) | the five largest crowns, every cloud, the floor rows of make and services |
| betrayal | as society | as society | the incident's tie |
| y1972 | as landscape | as landscape | as landscape |
| mind-pop | mind (0, 1.9, 0); −40; 16; 13.5 | 24; 9.5 | ≥ 90% of particles |
| mind-land | midway between the mind's and the land's centres at y 1.5; 0; 24; 34 | (falls back to mind) | the mind, the land, the tether |
| mind1972 | as mind | as mind | as mind |

The `crowns` view is exempt from the portrait rule's target shift (its eye height is its point); its bottom sheet covers
only the floors' lower part.

**Every "must be in frame" item is a frame item**, checked by projecting its points with the preset's camera, not by
labels (a Core anchor holds one point, so the land keeps its own list): each land layer registers, at each build, its
items' land-local points in `LandFrameItems` (a WP0 contract: `LandFrameItems.Set(string key, Vector3[] points)`,
main thread, cleared per snapshot); with `WHY_DUMP_LAND=1`, `LandViewLayer` logs at each preset's `OnFocus` one line per
item, "[Why] Frame <preset>: <key> in|out <x0> <y0> <x1> <y1>" (the screen box of its projected points; "in" when every
point projects inside the image), and `hc_views.presets` checks each preset's list from those lines (an item kind with a
count, "≥ 20 home threads", counts "in" lines). The keys (also registered as anchors where they have one point, 10.4): bench and hill labels `land:bench:<tier>`, `land:hill:<industry>`;
every capital river `land:capital:<industry>` (head and end points); the largest crowns `land:crown:<industry>` (ring
points); every cloud `land:cloud:<key>` (lens rim points); every tread with its rows `land:row:<tier>:<band>` (row ends);
every river, gorge and lake `land:river:<tier>`, `land:gorge:<riser>:<k>`, `land:lake:<industry>`; all 79 roots
`land:root:<from>><to>` (lowest point and both ends); the bedrock `land:bedrock` (its 8 corners); home threads
`land:thread:<k>` (apex; "≥ 20" counts anchors in frame); trade's frontage `land:frontage:trade`; estate streams
`land:estate:<k>`; the gov tread and mesas `land:bench:gov`, `land:mesa:*`; the central conduit `land:taxes`; the mind's
parts `mind:*`; every territory `land:coalition:<n>`; the couples labels' anchors `smv:family:*` (8.5); the 1950 tick and
the cut `road:tick:1950`, `land:cut`. (`EconomyViews` table; rows = `LandGroup`, eased at `LandStyle.EmphasisRate`, land groups × `Reveal`)

Rule: each view's story at 1; the land itself (ground, hills, caps) ≥ 0.35 where it is the stage; everything else ≤
0.15. The bowl-only groups (Terraces, Sectors, Pools, Towers, Crown, Income, CapitalFlows, Rivers, Taxes) are 0 in every
column. Columns: ov = overview, se = section, la = landscape, ca = capital, pe = people, ri = rivers, mi = mind, so =
society, co = couples, fa = families, ro = roots, st = state, cr = crowns, be = betrayal, 72 = y1972, mp = mind-pop, ml =
mind-land, m72 = mind1972.

| Group (owner) | ov | se | la | ca | pe | ri | mi | so | co | fa | ro | st | cr | be | 72 | mp | ml | m72 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Road (WP5) | 1 | .3 | .35 | .35 | .35 | .35 | .15 | .35 | 1 | .35 | .35 | .35 | .35 | .35 | .35 | .15 | .35 | .15 |
| Cut (WP5) | 1 | 1 | .6 | .6 | .6 | .6 | .1 | .6 | .6 | .6 | .6 | .6 | .6 | .6 | .6 | .1 | .6 | .1 |
| Ground (WP1) | .8 | 0 | 1 | .6 | .8 | .7 | .15 | .35 | 0 | .5 | .35 | .9 | .6 | .35 | 1 | 0 | .35 | .15 |
| Hills (WP1) | .8 | 0 | 1 | .7 | .6 | .5 | .15 | .35 | 0 | .4 | .35 | .4 | .8 | .35 | 1 | 0 | .35 | .15 |
| Contours (WP1) | .5 | 0 | 1 | .5 | .4 | .35 | .1 | .2 | 0 | .3 | .2 | .3 | .5 | .2 | 1 | 0 | .2 | .1 |
| Caps (WP1) | .8 | 0 | 1 | 1 | .6 | .35 | .15 | .3 | 0 | .35 | .3 | .3 | 1 | .3 | 1 | 0 | .35 | .15 |
| Lakes (WP1) | .5 | 0 | .5 | .2 | .3 | 1 | 0 | .15 | 0 | .15 | .1 | .2 | .2 | .15 | .5 | 0 | .3 | 0 |
| Roots (WP1) | .15 | 0 | .15 | .05 | .03 | .03 | 0 | .02 | 0 | .02 | 1 | .05 | .03 | .02 | .15 | 0 | .05 | 0 |
| Spending (WP1) | .3 | 0 | .15 | .05 | .1 | 1 | 0 | .05 | 0 | .05 | .05 | .1 | .05 | .05 | .15 | 0 | .1 | 0 |
| Glitter (WP1) | .2 | 0 | .1 | .03 | .05 | 1 | 0 | .03 | 0 | .03 | 0 | .05 | .03 | .03 | .1 | 0 | .1 | 0 |
| Wages (WP1) | .15 | 0 | .1 | .1 | 1 | .1 | 0 | .05 | 0 | .05 | .05 | .1 | .1 | .05 | .1 | 0 | .1 | 0 |
| Capital (WP1) | .3 | 0 | .15 | 1 | .1 | .1 | 0 | .05 | 0 | .05 | .05 | .05 | .15 | .05 | .15 | 0 | .1 | 0 |
| Vapor (WP1) | .2 | 0 | .1 | 1 | .6 | .05 | 0 | .05 | 0 | .05 | .05 | .05 | .3 | .05 | .1 | 0 | .1 | 0 |
| SavingCredit (WP1) | .1 | 0 | .05 | 1 | .15 | .05 | 0 | .02 | 0 | .02 | 0 | .05 | .1 | .02 | .05 | 0 | .05 | 0 |
| TaxesTransfers (WP1) | .1 | 0 | .05 | .03 | .15 | .1 | 0 | .02 | 0 | .02 | .02 | 1 | .03 | .02 | .05 | 0 | .05 | 0 |
| Abroad (WP1) | .1 | 0 | .1 | .5 | .05 | 1 | 0 | .02 | 0 | .02 | 0 | .1 | .05 | .02 | .1 | 0 | .05 | 0 |
| Overlays (WP1) | .3 | 0 | .3 | 1 | .05 | .05 | 0 | .05 | 0 | .05 | .05 | .05 | .3 | .05 | .3 | 0 | .05 | 0 |
| Glyphs (WP2) | .5 | 0 | .35 | .3 | 1 | .35 | 0 | .8 | 0 | .8 | .1 | .35 | 1 | .8 | .35 | 0 | .35 | 0 |
| Dots (WP2) | .3 | 0 | .15 | .15 | 1 | .15 | 0 | .3 | 0 | 1 | .05 | .1 | .6 | .3 | .15 | 0 | .2 | 0 |
| Mirages (WP2) | .1 | 0 | .05 | .05 | .3 | .6 | 0 | .03 | 0 | .05 | 0 | .05 | .05 | .03 | .05 | 0 | .05 | 0 |
| Crowns (WP2) | .6 | 0 | .4 | 1 | 1 | .15 | 0 | .4 | 0 | .5 | .1 | .15 | 1 | .4 | .4 | 0 | .35 | 0 |
| Clouds (WP2) | .5 | 0 | .3 | 1 | 1 | .15 | 0 | .5 | 0 | .6 | .1 | .15 | 1 | .5 | .3 | 0 | .35 | 0 |
| Children (WP2) | .1 | 0 | .05 | 0 | .3 | 0 | 0 | 0 | 0 | 1 | 0 | 0 | .2 | 0 | .05 | 0 | 0 | 0 |
| Dependents (WP2) | 0 | 0 | 0 | 0 | .12 | 0 | 0 | 0 | 0 | 1 | 0 | 0 | .12 | 0 | 0 | 0 | 0 | 0 |
| Debt (WP2) | 0 | 0 | 0 | 0 | .8 | 0 | 0 | 0 | 0 | .8 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Games (WP2) | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 0 | 1 | 0 | 0 | 0 | 0 |
| Estates (WP2) | 0 | 0 | 0 | .15 | .1 | 0 | 0 | 0 | 0 | 1 | 0 | 0 | .15 | 0 | 0 | 0 | 0 | 0 |
| Ties (WP2) | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 0 | 1 | 0 | 0 | 0 | 0 |
| Coalitions (WP2) | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 0 | .6 | 0 | 0 | 0 | 0 |
| Signals (WP3) | 0 | 0 | .5 | .3 | 1 | .3 | 0 | .5 | 0 | .5 | 0 | .3 | .5 | .3 | .5 | 0 | .5 | 0 |
| Mind (WP3) | .3 | 0 | 0 | 0 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | .45 | 1 | 1 |
| MindPopulation (WP3) | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 1 | 0 | 0 |
| Tether (WP3) | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 1 | 0 |
| Bonds (WP4) | .5 | .6 | .5 | .5 | .5 | .5 | .15 | .5 | 1 | .5 | .5 | .5 | .5 | .5 | .5 | .15 | .5 | .15 |

`Bonds` is not drawn by a land layer: `LandViewLayer` hands its eased value to `SmvLayer.BondsAlpha` each frame, as it
hands `Road` to `TimeAxisLayer.SceneAlpha` / `SmvLayer.SceneAlpha`. The selected player's own flows ignore the table
(the highlight lights them at 1, the rest of their groups at 0.25).

**Label groups shown** (a group's labels show when the preset lists it and its alpha ≥ 0.3): overview Ground, Cut;
section Cut; landscape Ground, Hills; capital Capital, Vapor, SavingCredit, Crowns, Caps (company marks), Overlays;
people Ground, Glyphs, Crowns, Clouds; rivers Lakes, Abroad; mind Mind; society Coalitions; couples Bonds; families
Dependents, Estates, Glyphs; roots Ground, Roots; state TaxesTransfers; crowns Crowns, Clouds, Ground; betrayal
Coalitions; y1972 Ground, Hills; mind-pop MindPopulation, Mind; mind-land Mind, Tether; mind1972 Mind. `HideRoadLabels`:
false in overview and couples, true elsewhere. `HoldRound`: society 96, betrayal 50 (with the betrayal season).

### 10.4 Labels and anchors

All `Fixed` world positions (`LandFrame.World` / `MindFrame.ToLand`), text generated by `LandFacts`:

| Label | Position | Priority | Group | Text (2025) |
| --- | --- | --- | --- | --- |
| benches (5) | the left bedrock face at each tread height, x −7.05 | 40 | Ground | "SERVICES · $14.8T · 48% of GDP" |
| hills | summit + 0.15 | 10 + 60 × VA share | Hills | "Real estate · $4.24T · owners keep 57%" |
| lakes ≥ $300B | the lake's front edge | 14 | Lakes | "Health · households pay it $3.75T directly (158% of what it creates) · 54% fear" |
| capital rivers, the 6 largest | the river's head | 22 | Capital | "Capital into real estate $1.58T a year (estimate) · $343B from savers" |
| vapor, the 6 largest hills | cap + 0.6 | 20 | Vapor | "Real estate pays out $1.69T: rent and dividends" |
| cloud level | the back edge, right | 30 | SavingCredit | "Cloud level · savers $2.16T up · credit $0.93T and investment $1.23T down" |
| company marks, the 6 largest | the mark | 18 | Caps | "NVIDIA $5.49T" |
| crowns, the 5 largest (crowns view: every crown ≥ $1T) | the crown's top + 0.1 | 32 | Crowns | "Internet · $6.07T controlled · Page & Brin (Alphabet), Zuckerberg (Meta)" |
| named seats (crowns view) | the seat ring + 0.05 | 16 | Crowns | "Walton family · Walmart 45%" |
| clouds | the lens' rim, front | 26 | Clouds | "Diversified 1% · $24.1T · owns trade 14%, manufacturing 11% … · over professional, entertainment" |
| crown band (crowns view) | the right edge at y 3.55 | 30 | Crowns | "Crown band · control" (and "Cloud level · diversified wealth" at 4.50, "Floors and slopes · work and own firms" at the bench edge) |
| players (people view) | the largest player of each hill, head + 0.1 | 12 | Glyphs | "Frontline · trade · I · 6.8M" |
| mesas | above each mesa | 34 | TaxesTransfers | "Federal · taxes in $<..>T · transfers out $<..>T" |
| imports | the edge fall | 26 | Abroad | "Abroad $2.27T of household spending (imports)" |
| roots, the 6 largest | the root's lowest point | 24 | Roots | "Professional services → trade $704B" |
| estates, the 3 largest streams | the stream's apex | 20 | Estates | "Estate $41B to an heir in 2024" |
| dependents | the largest thread bundle's apex | 18 | Dependents | "Allowance to adult children at home $379B" |
| coalitions | the largest component's centroid | 30 | Coalitions | "Coalition 3 · 28 players · trade, manufacturing · 77% inside · cooperation 0.73" |
| mind | 7 | 20-40 | Mind | 7.2-7.7 |

**Anchors** (`kind:id`): `land:bench:<tier>`, `land:hill:<industry>`, `land:lake:<industry>`, `land:capital:<industry>`,
`land:vapor:<industry>`, `land:company:<ticker or name>`, `land:crown`, `land:crown:<industry>`, `land:cloud:<key>`,
`land:clouds` (the cloud level), `land:player:<key>`, `land:group:<group>`, `land:owners`, `land:river:<category>`,
`land:gorge:<riser>:<k>`, `land:taxes`, `land:transfers`, `land:mesa:federal`, `land:mesa:state_local`, `land:abroad`,
`land:ai`, `land:ads`, `land:root:<from>><to>`, `land:coalition:<n>`, `land:children`, `land:dependents`,
`land:estates`, `land:cut`, `land:seat:<holder>`, `land:ladder:<coalition>`, `land:bedrock`, `land:row:<tier>:<band>`,
`land:frontage:<industry>`, `land:thread:<k>`, `land:estate:<k>`, `land:home:<person>`, `mind:shell:left`, `mind:shell:right`, `mind:top`, `mind:bottom`, `mind:reason`,
`mind:drive:<k>`, `mind:chem:<name>`, `mind:mode:<name>`, `mind:gate:<category>`, `mind:memory:<box>`, `mind:agency`,
`mind:play`, `mind:particle:<key>`. The tour's end card keeps `land:owners` and `land:crown` (so the shared
`Director/DirectorMode.cs` is not edited).

**Highlight ids** (`Land/HillIds.cs`, WP0; clear of the bowl's 80,000-95,000 and of the civilizations' 1,000,000+):
`Hill(i, part)` 96,000 + 8i + part (0 wages, 1 upkeep, 2 cap, 3 contour, 4 lake, 5 roots in, 6 capital river, 7 label);
`Bench(t)` 96,300 + t; `River(t, lane)` 96,400 + 8t + lane (0-5 categories, 6 imports); `Gorge(k)` 96,450 + k;
`Vapor(i)` 96,500 + i; `CloudLevel` 96,560; `TaxConduit` 96,570; `Transfer(k)` 96,600 + k (k < 100); `Crown(i, part)`
96,700 + 8i + part (0 band, 1 points, 2 jewels, 3 tethers, 4 shadow, 5 stem, 6 named seats, 7 the vapor through it); `Company(c)` 96,900 + c; `Estate(k)` 97,000 + k
(k < 500); `Games(c, part)` 97,500 + 2c + part (c < 50; 0 ladder, 1 pyramid); `Mind(stage, k)` 97,600 + 100 × stage + k (stage 0-7, k < 100); `Particle(p)` 98,500 + p (p < 1,000). Players
keep `EconomyIds.LandPlayer(p, part)` (81,000 + 8p; part 6, spare in the bowl, is the signals'); ties
`EconomyIds.LandTie`, coalitions `LandCoalition`, roots `LandRoot`, the cut `LandCut`; lifelines and children keep their
lifeline ids.

### 10.5 Legibility rules (each a check in 11.8)

1. One story per view at full strength; the rest ≤ 0.15 (10.3).
2. Dense flows aggregate outside their own views (4.11); ties ≤ 120; territories ≤ 6; labels by priority with no placed
   label of priority ≥ 30 outside the image.
3. Every dollar-coded line is ≥ 0.8 px; widths are dollars; the legend states the scale ("1 u² = $0.67T of value added ·
   lakes ×1/10 · width 0.1 = $3.1T a year · capital and roots ×4").
4. Hues are meanings, never shared: gold capital (caps, rims, company marks, capital rivers, vapor, rain, saving, credit,
   debt rings, crowns, their stems and named seats, cloud rims, the 1%'s dots and lines, estates), light blue wages, ice fear, rose desire, white fantasy, steel
   state, people blue people (home dots and threads included), tier tints for ground and roots, violet for ties across
   coalitions, the coalitions' palette for territories, ladders and pyramids.
5. Rose and ice are drawn side by side, alpha-blended, never added (additive rose beside ice blooms white: the bowl's
   lesson).
6. Panels never cover their subject: the land's silhouette and the mind's shell are kept clear (`EconomyUiLayout`), at
   16:9 and 9:16; in portrait the panels are a bottom sheet and the subject sits in the upper 60%.
7. Height is control: no glyph or structure stands above the highest summit but the crowns (in the band) and the clouds
   (at the cloud level); only flows (vapor, saving, rain, credit, stems, tethers) cross that height (the AI block's height
   is capped, 2.6; A35).

### 10.6 Interaction and panels

Keys (WP5, `UI/EconomyControls.cs`) are ignored while the tour runs, i.e. while `GraphRoot.AllowPresetKeys` is false (the
tour holds Space, P, M and the arrows then, A34).

| Input | Where | Does |
| --- | --- | --- |
| 1-9, 0 | anywhere | the keyed presets (`GraphRoot` maps Alpha0-9 already) |
| `,` `.` (Shift: 5 years) | anywhere | year step (existing) |
| `Space` | anywhere | play / pause the years (6.6) |
| `N` | anywhere | next notable person (existing) |
| click | land | select a player / crown / cloud / lake / hill / river (`HillPick` → `EconomyState.Select(kind, index)`, 11.2); its flows brighten, others to 0.25 |
| `M` or double-click a player | land | fly (1.2 s) to the mind of the selection; `M` in a mind view flies back to the last land view |
| `Tab` / `Shift+Tab` | mind views | next / previous player by index |
| `P` | mind views | player ↔ population mode; click a particle selects that player |
| `G` | mind views | run the shown year as the 12 s staged sequence; again: jump to the settled state |
| "Mind" button | person inspector | person mode for the inspected lifeline |
| `Esc` | anywhere | clear the selection (existing) |
| hover | land, mind | the part's card: its numbers and the rule that made them (generated text) |

**Panels** (WP5, `Economy/UI`): `PlayerPanel` gains the zone, the place ("on the valley floor in front of trade, row 2:
frontline"), crowns (jewels, measured vs modeled), clouds (holdings), the program summary (3 lines of the listing) and a
"Mind" button; **`MindPanel`** (new; right column 360 reference px in landscape, bottom sheet in portrait): the listing
(6.5), sparklines 1950-Y of the followed members' income, spending, saving rate, net worth and debt per adult (2025
dollars), the score bar (6.6), buttons Run year (G), Population (P), Back to land (M); `LandLegend`: the unit line, the
river category chips (MATING: status, growth, trading assets), the zones ("floor · slope · crown · cloud", "hollow seat:
the measured holder · filled dot: the model's line"), the tie events' names; `PersonInspector`: the family highlight
(8.5) and the "Mind" button; `SocialPanel`: unchanged controls (noise, shadow of the future, polarization, partner choice,
who forgives). Picking (`Land/HillPick.cs`, new, WP5): hills by `ITerrainField.Owner` at the ray's hit (march the ray
over H in 0.05 steps), lakes by ellipse, rivers and flows ≤ 6 px from a ribbon's centre line, players by disc / ledge /
lens, crowns by band, clouds by lens; `HillPick.Outline(LandSnapshot s, Camera cam, List<Vector2> hull, List<Vector2>
scratch)` = the screen hull of the terrain's silhouette (bedrock corners, summits), the crowns' rings and tops and the
clouds' lenses: what the panels must not cover.

**The UI's bowl dependencies and their replacements** (WP5; every edit keeps the bowl branch, taken when
`LandService.Current?.Terrain == null`, so the restore recipe of 1.1 holds):

| File | Reads at HEAD | In the hills |
| --- | --- | --- |
| `SocialPanel.cs` (lines 234, 258, 272) | `SocialLayer.Shown`, `SocialLayer.CoalitionsAt` | `LandSeason.Shown ?? SocialLayer.Shown`, `LandSeason.CoalitionsAt` |
| `PlayerPanel.cs` (300, 331) | `SocialLayer.Shown`; `SetSelection(-1, SelectedTower, SelectedTie)` | `LandSeason.Shown ?? …`; `EconomyState.Select(SelectionKind.None, -1)`; zone, place, crown, cloud, program rows |
| `PersonInspector.cs` (271) | `Layers.SocialLayer.Shown` | `LandSeason.Shown ?? …`; the family highlight `SmvBonds.FamilyIds` (stub from WP0), the "Mind" button |
| `LandPicker.cs` (165, 396, 524 and the pick path) | `RootsModel.Build(model.Data, s.Land, s.Year)`, `LandPick.*`, `SocialLayer.Shown`, towers | `s.Terrain.Roots` (4.9), `HillPick.*`, `LandSeason.Shown ?? …`; company marks instead of towers |
| `HudBlocks.cs` (126) | `LandPick.BowlOutline(EconomyStage.Land(), cam, s.Land?.Towers, hull, scratch)` | `HillPick.Outline(s, cam, hull, scratch)` |
| `EconomyUiLayout.cs` (55, the panel check) | the bowl's outline (through `HudBlocks`) | the hills' outline, the mind's shell box in mind views |
| `EconomyFacts.cs` (454, 473, 503, 535-538, 587, 758-850) | `s.Land.Gdp`, `s.Land.Towers`, `LandGroup.Towers`, `LandStyle.AreaGdp / WidthGdp / HeightGdp`, `s.Money.Paths`, `s.Money.CategoryToSeller` | `s.Terrain.Gdp`; `s.Terrain.Companies`; `LandGroup.Caps` (company marks); `HillFrame.AreaGdp / WidthGdp / CapitalWidthGdp / LakeAreaGdp` (the unit line of 10.5 rule 3, no "height = market value": heights are strata); `s.Flows.Paths`; `s.Accounts.CategoryToSeller` |
| `EconomyControls.cs` (316, 420) | `Figure.Head(player)` (the bowl's polar place); `YearFacts.TowersShown` | `HillFigure.Head(player)` (11.2); the companies readout for the capital view |
| `PersonPanel.cs`, `LifelinePicker.cs`, `PersonFraming.cs` | no bowl dependency at HEAD (checked) | unchanged, unless a dot on the land is needed: then `PlayerSet.DotAt[person]` (the one source of dot positions, 3.8) |
| `LandViewLayer.cs` (150, 293-325: `WHY_ECON_SELECT`) | `SetSelection(player, tower, tie)`; `tower:<ticker>` | `EconomyState.Select(kind, index)` for `player:`, `crown:`, `cloud:`, `hill:`, `lake:`, `pair:` (`tower:` maps to the company's hill) |
| `LandLegend.cs` | the bowl's chips | the chips and zones above |

---

## 11. Engineering

Engine constraints (unchanged): Unity 6 URP, C# 9, one assembly; everything built at runtime in code; `LineMeshBuilder` /
`SurfaceMeshBuilder` with `Why/Line` and `Why/Surface` (unlit, translucent, ZWrite off, bloom, `Cull Off`); land and
mind meshes land-local with `GraphMaterials.Raw`; layers `Prepare` on workers (tier = Order / 10, Order < 30 tier 0),
`Upload` / `Tick` on the main thread; labels, anchors, highlight ids, view presets; code under `Economy/Land` and
`Economy/Layers` never references `Economy/UI` (the harness does not compile `Economy/UI`).

### 11.1 Files and owners

**New** (`.meta` for every new file and folder via `python3 $SP/gen-metas.py <worktree>`):

| File | Owner | Content |
| --- | --- | --- |
| `Economy/EconomyScenes.cs` | WP0 | `EconomyScenes.Bowl = "economy-bowl"` |
| `Economy/Land/Hills/HillTypes.cs` | WP0 | `HillFrame`, `ITerrainField`, `TerrainGeometry`, `HillGeom`, `BenchGeom`, `GorgeGeom`, `LakeGeom`, `CompanyMark`, `HillAccounts`, `HillMoney`, `RiverProfile`, `HillFigure` (11.2) |
| `Economy/Land/People/PeopleTypes.cs` | WP0 | `CrownGeom`, `CrownJewel`, `CrownSeat`, `CrownValues`, `ControlStats`, `ChildStem`, `HomeThread`, `EstateStream`, `FamilyResult`, `TerritoryShape`, `TerritoryRun`, `TerritoryResult` |
| `Economy/Land/Mind/ProgramTypes.cs` | WP0 | `MindScope`, `ProgramYear` |
| `Economy/Land/HillIds.cs` | WP0 | the id blocks of 10.4 |
| `Economy/Land/LandTween.cs` | WP0 | the tween state (11.2), implemented in full by WP0 |
| `Economy/Land/LandSeason.cs` | WP0 | the season on screen for the UI (5.0) |
| `Economy/Land/LandFrameItems.cs` | WP0 | the frame items' registry (10.2) |
| `Economy/Land/Mind/LandPlayback.cs` | WP0 creates the stub (`Tick(dt)` does nothing, "(demo)"); then WP3 | 6.6 |
| `Economy/Land/People/CrownModel.cs` | WP0 creates the stub (`Values` returns zeros, "(demo)"); then WP2 | 3.5 |
| `Economy/Model/ProgramTrace.cs` | WP0 creates `TraceRow` and an empty store; then WP3 | 6.2 |
| `Humans/Smv/SmvBonds.cs` | WP0 creates `SmvBondsOptions` and the stub `SmvBonds` (`FamilyIds` adds the person's own range only); then WP4 | 8.3 |
| `Economy/Land/Hills/HillRoots.cs` | WP1 | 4.9 |
| `$SP/redesign2/checks/restore-bowl.sh` | WP0 writes; the lead runs it at integration | 1.1 |
| `Economy/Views/EconomyViews.{Base,Terrain,People,Mind,Couples}.cs`, `Economy/Views/EconomyPresets.{Base,Terrain,People,Mind,Couples}.cs` | WP0 creates; then WP5 / WP1 / WP2 / WP3 / WP4 | partial files: each owner's emphasis rows, view specs and poses (11.2) |
| `Economy/Bowl/BowlPresets.cs`, `Economy/Bowl/BowlViews.cs` | WP0 | HEAD's catalog and table, renamed, unused |
| `Economy/Land/Hills/HillStyle.cs`, `HillLayout.cs`, `TerrainField.cs`, `ValleyNetwork.cs`, `InvestmentModel.cs`, `MoneyAccounts.cs`, `HillFlows.cs` | WP1 (WP0 stubs `HillLayout`, `TerrainField`, `MoneyAccounts`, `HillFlows` with the final signatures) | 2, 4 |
| `Economy/Layers/TerrainLayer.cs`, `Economy/Layers/HillFlowsLayer.cs` | WP1 | 2.7, 4 |
| `Economy/Land/People/StandStyle.cs`, `ControlLedger.cs`, `CloudModel.cs`, `StandPlacement.cs`, `FamilyModel.cs`, `Territories.cs` | WP2 (WP0 stubs `ControlLedger`, `StandPlacement`, `FamilyModel`, `Territories`) | 3, 5 |
| `Economy/Layers/PeopleLayer.cs`, `CrownsLayer.cs`, `FamiliesLayer.cs`, `SeasonLayer.cs` | WP2 | 3, 5 |
| `Economy/Model/MindModel.cs` | WP3 | 6.4 |
| `Economy/Land/Mind/MindStyle.cs`, `ProgramModel.cs`, `MindFrame.cs`, `MindGeometry.cs` | WP3 (WP0 stubs `ProgramModel`) | 6, 7 |
| `Economy/Layers/MindLayer.cs`, `Economy/Layers/SignalsLayer.cs` | WP3 | 6.6, 7 |
| `Economy/Layers/CutLayer.cs`, `Economy/Land/HillPick.cs`, `Economy/UI/MindPanel.cs` | WP5 | 9, 10.6 |
| `$SP/redesign2/checks/hillcheck.py`, `hc_terrain.py`, `hc_people.py`, `hc_mind.py`, `hc_smv.py`, `hc_views.py`, `hc_tour.py` and their `*.json` expectations | WP0 creates all; each module then belongs to its WP (terrain WP1, people WP2, mind WP3, smv WP4, views WP5, tour WP6) | 11.8 |

**Changed**:

| File | Owner | Change |
| --- | --- | --- |
| `Economy/Land/LandTypes.cs` | WP0 | append-only additions (11.2); the bowl's members untouched |
| `Economy/Land/LandMath.cs` | WP0 | `PackLine` (the linear twin of `PackRow`) |
| `Economy/Land/LandStyle.cs` | WP0 | `CacheYears` 4 → 6; nothing else (other packages put constants in their own style files) |
| `Economy/Land/LandService.cs` | WP0 | the hills' pipeline `Build`, `BuildBowl` (HEAD's `Build`, unused), `Prefetch`, the ledger, the checks line, the builders' log lines |
| `Economy/Layers/LandModelLayer.cs` | WP0 | builds the ledger and the first snapshot; prints the log (11.8), including `HillLayout.Build` for 1950, 1972 and 2025 at load (the reference years' Hills lines) |
| `Economy/EconomyState.cs` | WP0 | the mind and playback state, the selection contract, the lives' overrides, harness overrides (11.2) |
| `Economy/Land/LandView.cs` | WP0 adds `Phases`; then WP5 | 9 |
| `Economy/Layers/LandViewLayer.cs` | WP0 wires `LandPlayback.Tick`, `LandTween.Tick`, `BondsAlpha` and the `WHY_DUMP_LAND` frame lines; then WP5 | 10 |
| `Economy/Model/EconomicLives.cs`, `LivesSimulation.cs` | WP0 adds the record fields (zero-filled: no number changes), `EstateEvent`, and the accessors `Estates` (empty), `TraceOf` (default row), `CalibrationOf`, `AllowanceTotal` (0); then WP3 | 6.2 |
| `Humans/Smv/SmvLayer.cs`, `SmvShared.cs` | WP0 adds `SmvLayer.BondsAlpha` (static, 1) and `SmvPopulation.BondsKey` / `Bonds` (null): additive, guarded by whycheck; then WP4 | 8 |
| `Economy/EconomyViews.cs`, `Economy/EconomyPresets.cs` | WP0 rewrites as partial classes; then WP5 | `ViewSpec` (+ `Mind`, `LandscapeOnly`), the catalog assembly, overview, section, the portrait rule |
| `Economy/Land/PlayerCensus.cs` | WP2 (WP0 adds the `BuildHills` stub) | `BuildHills` (3.2), `careerSe`, former industry; `Build` (bowl) unchanged |
| `Economy/Model/LivesSimulation.cs`, `EconomicLives.cs`, `LivesReport.cs`, `LivesInputs.cs` | WP3 (after WP0's stubs) | record fields filled, estate log, trace, allowance, play feedback (κ from `psyche.json`), accessors, the line style (8.5), report lines |
| `Humans/Smv/SmvShared.cs`, `SmvGeometry.cs`, `SmvLayer.cs` | WP4 (after WP0's additions) | the bonds, scoped by the null option (8.2) |
| `Economy/Layers/EconomyLoaderLayer.cs` | WP4 | publishes `SmvBondsOptions` |
| `Economy/UI/PersonLine.cs` | WP4 | family lines (8.5), API kept |
| `Economy/Layers/LandViewLayer.cs`, `Economy/Land/LandView.cs` | WP5 (after WP0's wiring) | the 18 views, the emphasis, overrides `WHY_ECON_*` through the selection contract |
| `Economy/Land/RootsModel.cs` | WP1 | adds `RootSel` and `Select` only (additive; `Build`, `Line`, `RootsOf` untouched, so the bowl's roots cannot change) |
| `Economy/UI/LandPicker.cs`, `PlayerPanel.cs`, `LandLegend.cs`, `EconomyControls.cs`, `PersonInspector.cs`, `SocialPanel.cs`, `EconomyUiLayout.cs`, `HudBlocks.cs`, `EconomyFacts.cs` (and `PersonPanel.cs`, `LifelinePicker.cs`, `PersonFraming.cs` only if needed) | WP5 | the edits of the table in 10.6, each keeping its bowl branch |
| `Assets/Resources/Data/economy/circuit.json`, `groups.json` | WP2 | 11.5 |
| `Assets/Resources/Data/economy/psyche.json` | WP3 | 11.5 |
| `Assets/Resources/Data/economy/tour.json`, `Docs/ECONOMY.md`, `Docs/ARCHITECTURE.md`, `README.md` | WP6 | the tour and the docs |
| `Director/TourData.cs` | WP6 | **scoped shared change**: only the body of `EconomyBuiltIn()` (the economy's fallback tour, used when tour.json is missing) |

**Moved and retagged** (WP0, 1.1): `Layers/LandscapeLayer.cs`, `FlowsLayer.cs`, `InhabitantsLayer.cs`, `SocialLayer.cs`,
`SectionLayer.cs`, `Land/LandLayout.cs`, `PlayerPlacement.cs`, `MoneyRouting.cs`, `CanalRouting.cs`, `LandPick.cs` →
`Economy/Bowl/` (with `.meta`). `MoneyRouting.SplitMotives` stays where it is and both censuses keep calling it.

**Not changed**: every file under `Assets/Scripts/Core/`, `Axis/`, `Humans/` other than the four SMV files above,
`Director/` other than `TourData.EconomyBuiltIn`, `UI/`, `Lens/`, `Matter/`, `Life/`, `Domination/`, `Figures/`;
`Assets/Scenes/*.unity`; `Land/SocialSeason.cs`, `Coalitions.cs`, `SellerMatrix.cs`, `CapitalSources.cs`,
`LandFacts.cs`, `LandFrame.cs`; `Layers/IndustryWallLayer.cs`; `industries.json`.

**Files touched by two packages, in sequence** (never in parallel): WP0 first adds the contract members, then the named
owner; the owner may not change a WP0 member's signature (11.2's freeze).

### 11.2 The contracts (WP0 writes them; frozen once WP0 merges; changes only by the lead with every owner told)

```csharp
// ---------------------------------------------------------------- Economy/EconomyScenes.cs
namespace Why.Economy
{
    /// <summary>Economy scene ids no scene file loads: retired layers keep compiling under them (SPEC2 1.1).</summary>
    public static class EconomyScenes { public const string Bowl = "economy-bowl"; }
}

// ---------------------------------------------------------------- Economy/Land/LandTypes.cs (appended)
namespace Why.Economy.Land
{
    public enum Zone : byte { Floor = 0, Slope = 1, Crown = 2, Cloud = 3 }

    public enum FlowKind : byte
    {
        Wages = 0, WagesCompany = 1, Business = 2, Capital = 3, Transfers = 4,                      // the bowl's, kept
        Payout = 5, PayoutAbroad = 6, Saving = 7, Investment = 8, Credit = 9, Borrowing = 10,
        Rivulet = 11, River = 12, Distributary = 13, Waterfall = 14, Taxes = 15, Abroad = 16,
        Runoff = 17, WageArc = 18, ValleyRiver = 19, Gorge = 20, Descent = 21, EdgeFall = 22,      // the hills (SPEC2 4)
        CapitalRiver = 23, CapitalRain = 24, CapitalSpring = 25, Vapor = 26, Rain = 27, Evaporation = 28,
        CreditColumn = 29, CreditStream = 30, TaxSeep = 31, TaxConduit = 32, TransferConduit = 33, Fountain = 34,
        VaporThrough = 35, VaporOnward = 36, EvaporationTrading = 37                                  // revision 2 (4.5, 4.6)
    }

    public enum LandGroup : byte
    {
        Road = 0, Cut, Terraces, Sectors, Pools, Roots, Towers, Crown, Overlays, Glyphs, Dots, Mirages,     // the bowl's, kept
        Income, CapitalFlows, Rivers, Glitter, Taxes, Ties, Coalitions,
        Ground, Hills, Contours, Caps, Lakes, Spending, Wages, Capital, Vapor, SavingCredit, TaxesTransfers, Abroad,  // SPEC2
        Crowns, Clouds, Children, Dependents, Estates, Signals, Mind, MindPopulation, Tether, Bonds,
        Debt, Games,                                                                                        // revision 2
        Count
    }

    // Player: appended members (SPEC2 3, 6)
    //  public Zone Zone;                                   the kind of standing
    //  public int Hill = -1;                               industry index of its hill; -1 for a cloud
    //  public int Tread = -1;                              tier of the tread its ground point is on
    //  public int Band = -1;                               floor row 0-3 (+10 per spill) or slope row 0-3; -1 crown, cloud
    //  public Vector3 Pos;                                 land-local: disc / ledge / crown band / lens centre
    //  public float GroundX;                               x where its spending, taxes and fountains meet its tread's river
    //  public int CrownIndex = -1;                         index in PlayerSet.Crowns (crown players)
    //  public double ControlWealth, BusinessEquity, Financial, Home;            // $B, the members' sums
    //  public readonly double[] Holdings = new double[25];                      // cloud players: $B by industry
    //  public float Inherited, Pain, Relief, Satisfaction;                      // shares of adults
    //  public int[] Dependents = System.Array.Empty<int>();                    // member adults living with their parents
    //  public double AllowanceIn, AllowanceOut, ChildCost, Borrowed, Repaid, Estates;   // $B
    //  public int Program = -1;                            index in LandSnapshot.Programs
    //  public double Debt;                                 $B, the members' mortgage + consumer debt (the debt ring, 3.8)
    //  public double SaveTrading, SaveDeposits;            $B, gross saving split (4.1)
    //  (Radius: disc radius for floor and slope players, the crown's r for crown players, the lens R for clouds)
    //  Zone, Hill and Anchor are set by the census (BuildHills); Tread, Band, Pos, GroundX, CrownIndex by StandPlacement

    // PlayerSet: appended members
    //  public Vector3[] DotAt = System.Array.Empty<Vector3>();   per person index: land-local dot; x = NaN when not drawn
    //  public Vector3[] HomeDotAt = System.Array.Empty<Vector3>(); per person index: a dependent's home dot beside its
    //                                                            parent's dot (3.7); x = NaN when none
    //  public CrownGeom[] Crowns = System.Array.Empty<CrownGeom>();
    //  public ControlStats Control;
    //  public string StandLog, CrownLog, CloudLog;

    // FlowPath: appended members
    //  public byte Level;              0 aggregate (every view), 1 per player (its own views: 4.11)
    //  public int Player = -1;         the player it belongs to (highlight); -1 aggregate
    //  public int Category = -1;       0-5 spending category; -1 otherwise
    //  public int Hill = -1;           the hill it starts or ends at

    // LandSnapshot: appended members
    //  public TerrainGeometry Terrain; public HillAccounts Accounts; public HillMoney Flows;
    //  public FamilyResult Families; public TerritoryResult Territories;
    //  public ProgramYear[] Programs = System.Array.Empty<ProgramYear>(); public ProgramYear Nation; public string ProgramLog;
}

// ---------------------------------------------------------------- Economy/Land/Hills/HillTypes.cs
namespace Why.Economy.Land
{
    /// <summary>The land's fixed frame (SPEC2 2.1). Land-local: x ∈ [−7, 7], z ∈ [−Depth/2, Depth/2], +z away from the road.</summary>
    public static class HillFrame
    {
        public const float Width = 14f, Depth = 15.15f, Edge = 0.30f, Gap = 0.22f;
        public static readonly float[] BenchY = { 0f, 0.70f, 1.40f, 2.10f, 2.80f };
        public const float Front = 1.20f, GovZone = 1.00f, Riser = 0.70f, Tread = 1.20f, DZones = 5.00f, ZMin = 0.70f, Back = 0.35f;
        public const float CloudY = 4.50f, CrownBase = 3.55f, CrownTopMax = 4.20f;
        public const float RoadV = 0.60f;                                          // the river's centre, from the tread's lip
        public static readonly float[] BandV = { 1.07f, 0.87f, 0.33f, 0.13f };     // floor rows 0-3, from the lip
        public static readonly float[] SlopeE = { 0.10f, 0.32f, 0.50f, 0.68f };    // slope rows: elevation fractions
        public const float DiscK = 0.030f, StockK = 0.75f;                          // r = DiscK √(people/1M); r = StockK √($/GDP)
        public const float AreaGdp = 46f, LakeAreaGdp = 4.6f, LakeBMax = 0.16f, WidthGdp = 1.0f, CapitalWidthGdp = 4.0f, RootWidthGdp = 4.0f;
        public const float MindX = -13.5f;                                          // the mind's origin (land-local x)
        public static float ZOf(float d) => d - Depth * 0.5f;
        public static float DOf(float z) => z + Depth * 0.5f;
    }

    /// <summary>The land's height field and its queries (pure, any thread, deterministic). Land-local x, z.</summary>
    public interface ITerrainField
    {
        float Height(float x, float z);                       // H = ground + Σ own (bilinear on the 0.05 raster)
        float Ground(float x, float z);                       // G: treads and risers only
        int Owner(float x, float z);                          // hill whose own height dominates (≥ 0.015) there; −1 ground
        float Own(int hill, float x, float z);                // that hill's own height (the exact profile)
        float Rho(int hill, float x, float z);                // that hill's normalized radius
        Vector3 OnSurface(float x, float z, float lift = 0f); // (x, H + lift, z)
        Vector2 Gradient(float x, float z);                   // ∇H (central differences)
        int TreadAt(float z);                                 // tier whose tread contains z; −1 elsewhere
        bool Free(float x, float z, float clearance);         // on a tread, clear of lakes, gorge blocks and hill feet by clearance
        void Contour(int hill, float e, System.Collections.Generic.List<Vector3> into, int points = 64);
                                                              // the hill's own contour at elevation fraction e (0 foot … 1 summit),
                                                              // CCW seen from above starting at its front point (−z), lifted onto H
    }

    public sealed class HillGeom
    {
        public int Industry; public Tier Tier; public bool Present, Mesa;    // Present: VA ≥ $1B
        public float X, Z, A, B;                                             // centre (land-local), semi-axes along x and z
        public float Own, BaseY, SummitY;                                    // own rise, its bench's height, summit height
        public float RhoCap, RhoUpkeep;                                      // √o, √(o + u)
        public double ValueAdded, Wages, Upkeep, Owners;                     // $B, the year's (WallGeometry.Split)
        public float Frontage0, Frontage1;                                   // x interval of its frontage on its tread
        public Vector3[] CapitalPath;                                        // the capital river's centre line, foot → cap rim (null for mesas)
        public Vector3 Summit => new Vector3(X, SummitY, Z);
    }

    public sealed class BenchGeom { public Tier Tier; public float Y, RiserZ0, LipZ, HillsZ, ZoneDepth, RiverZ; }   // RiserZ0: NaN for gov
    public sealed class GorgeGeom { public int Riser; public float X; public Vector3[] Path; }      // Riser t: from tier t−1's river up to t's
    public sealed class LakeGeom { public int Industry; public float X, Z, A, B, Y; public double Inflow, Fear, Fantasy; public bool Overflow; }
    public sealed class CompanyMark { public int Company, Industry; public string Name, Ticker; public Vector3 At; public double MarketCap, Revenue, NetIncome; }

    public sealed class TerrainGeometry
    {
        public int Year; public double Gdp;                                   // $B nominal
        public float AreaPerB, LakeAreaPerB, WidthPerB, CapitalWidthPerB;     // world units per $B, the year's
        public HillGeom[] Hills;                                              // 25, EconomyData.Industries order
        public BenchGeom[] Benches;                                           // 5, tier order
        public GorgeGeom[] Gorges = System.Array.Empty<GorgeGeom>();          // ≤ 12
        public LakeGeom[] Lakes = new LakeGeom[25];                           // by industry, null: none (HillLayout.AddLakes)
        public CompanyMark[] Companies = System.Array.Empty<CompanyMark>();   // capture companies from 2024
        public RootGeom[] Roots = System.Array.Empty<RootGeom>();             // HillRoots.Build (4.9); the bowl's RootGeom type
        public int[][] Order;                                                 // per tier, industries left to right (fixed)
        public ITerrainField Field;
        public string Log, ValleyLog; public double Checksum;
    }

    /// <summary>The year's money per player without geometry (MoneyAccounts.Build, SPEC2 4.1).</summary>
    public sealed class HillAccounts
    {
        public int Year;
        public double[,] CategoryToSeller;                                    // [6, 25] RAS shares (rows sum to 1)
        public readonly double[] ImportShare = new double[6];
        public double[][] Sink;                                               // [player][c × 25 + k]: category c paid first to hill k ($B)
        public double[] Imports;                                              // [player] $B
        public double[][] CapitalBy;                                          // [player][25] capital income by paying hill ($B)
        public double[] SaveGross, BorrowGross;                               // [player] $B
        public double[] SaveTrading, SaveDeposits;                            // [player] $B: SaveGross split (4.1)
        public readonly double[] PoolInflow = new double[25], PoolFear = new double[25], PoolFantasy = new double[25];
        public readonly double[] PayoutBySector = new double[25], ProductionTaxBy = new double[25];
        public readonly double[] InvestmentBy = new double[25], ExternalBy = new double[25];
        public double PayoutAbroad, SavingIn, Credit, NetLending, Investment, CorporateTaxes, Allowance;
        public string Log; public double Checksum;
    }

    public sealed class RiverProfile                                          // one tread's river, binned every Step in x
    {
        public int Tier; public float Z, X0, Step;
        public double[] PlusFear, PlusDesire, PlusFantasy, MinusFear, MinusDesire, MinusFantasy;   // $B per bin
        public double[][] PlusCategory, MinusCategory;                        // [6][bin]
        public float[] HalfWidth;                                             // drawn half-width per bin (both banks)
    }

    public sealed class HillMoney                                             // HillFlows.Build (SPEC2 4)
    {
        public int Year;
        public readonly System.Collections.Generic.List<FlowPath> Paths = new System.Collections.Generic.List<FlowPath>();
        public RiverProfile[] Rivers = new RiverProfile[5];
        public readonly double[] CrownIn = new double[25], CrownSeated = new double[25];   // $B by crown industry (4.5)
        public string Log, RiversLog, CapitalLog, VaporLog, StateLog; public double Checksum;
    }

    /// <summary>The hills' twin of the bowl's Figure (which reads the bowl's polar place): glyph points at Player.Pos.</summary>
    public static class HillFigure
    {
        public static Vector3 Head(Player p) => default;                      // Pos + (0, LandStyle.HeadY, 0)        WP0
        public static Vector3 DotSlot(Player p, int k, int n, float reason) => default;   // sunflower at Pos (3.8)  WP0
        public static Vector3 ChildSlot(Player p, int k, int n) => default;   // ring at r + 0.02 at Pos's height      WP0
    }
}

// ---------------------------------------------------------------- Economy/Land/People/PeopleTypes.cs
namespace Why.Economy.Land
{
    public struct CrownJewel { public int Company; public byte Kind; public double Value, Stake, Votes; public string Holder; public bool Recalled; }  // Kind 0 controlled, 1 founder-led
    public struct CrownSeat { public int Jewel; public string Holder; public Vector3 At; public bool Recalled; }  // a named seat (measured holder)
    public sealed class CrownGeom
    {
        public int Industry; public double Value, Named, Private;            // $B, the year's
        public double EquityValue, ControlledShare;                           // E_k ($B, estimate) and c_k (3.5)
        public CrownJewel[] Jewels = System.Array.Empty<CrownJewel>();
        public CrownSeat[] Seats = System.Array.Empty<CrownSeat>();           // named seats, one per holder
        public Vector3 Center; public float Radius, BandY, Top, StemBottom;   // land-local: band bottom y, top y, the summit's y
        public int[] Players = System.Array.Empty<int>();                     // controller players seated (the ledger's seats)
        public int SeatsApportioned;                                          // controller units the ledger put here
    }
    public sealed class ControlStats { public int Lines, Controllers, Running, Founders, Heirs, Filled, Successions, OwnSeat, OwnTierSeat; public double Share, Quota; }

    /// <summary>V_k and its parts per industry for a year (CrownModel.Values, SPEC2 3.5): geometry-free, any thread.</summary>
    public sealed class CrownValues
    {
        public int Year;
        public readonly double[] Value = new double[25], Named = new double[25], Private = new double[25];
        public readonly double[] EquityValue = new double[25], ControlledShare = new double[25];
        public readonly bool[] Crowned = new bool[25];                        // V_k ≥ the year's threshold (or the largest)
        public string Log;
    }

    public struct ChildStem { public int Child, Parent, Player; public double Cost; }                    // Cost: child cost, $B
    public struct HomeThread { public int Dependent, Parent, From, To; public double Allowance; public Vector3 HomeDot; }   // From: the parent's player
    public struct EstateStream { public int Year, From, Heir, GhostPlayer, HeirPlayer; public double Dollars; public bool Succession; public Vector3 Ghost; }
    public sealed class FamilyResult
    {
        public int Year;
        public ChildStem[] Stems = System.Array.Empty<ChildStem>();
        public HomeThread[] Threads = System.Array.Empty<HomeThread>();
        public EstateStream[] Estates = System.Array.Empty<EstateStream>();
        public string Log; public double Checksum;
    }

    public sealed class TerritoryShape { public int Coalition; public Vector3[] Points; }                 // a closed boundary, draped
    public struct TerritoryRun { public int Coalition; public float X0, X1, Z; }                          // a fill run of grid cells
    public sealed class TerritoryResult
    {
        public int Year;
        public TerritoryShape[][] Shapes;                                     // [detection 0..4]
        public TerritoryRun[][] Runs;                                         // [detection]
        public Vector3[][] LabelAt;                                           // [detection][coalition id]
        public string Log; public double Checksum;
    }
}

// ---------------------------------------------------------------- Economy/Land/Mind/ProgramTypes.cs
namespace Why.Economy.Land
{
    public enum MindScope : byte { Player = 0, Person = 1, Nation = 2 }

    /// <summary>One player's (a household's, or the nation's) program in one year (SPEC2 6.5).</summary>
    public sealed class ProgramYear
    {
        public int Year; public MindScope Scope; public int Player = -1, Person = -1; public string Key;
        public int Adults, Children, Households, Dependents;                                   // lines
        // WORLD ($B)
        public double Wages, Business, Capital, Transfers, SocialSecurity, AllowanceIn, Estates, Borrowed, Taxes, Disposable;
        public readonly double[] WagesBy = new double[25];
        public float EmploymentRate, MortgageRate, CardApr, EquityReturn, HomePriceGrowth, Trust;
        // MEMORY (start of year, $B)
        public double Financial0, Home0, Business0, Mortgage0, Consumer0;
        public float Runway0, DebtService0, Buffer, Inherited, ParentRank, SavingRate1, SavingRate2;
        // FEEL
        public readonly float[] Desire = new float[5], Fear = new float[5];                    // psyche.json order
        public float FearShare, Stress, StressDebt, StressJobless, StressBuffer;
        public readonly float[] Chemicals = new float[7];   // dopamine, serotonin, oxytocin, endorphins, cortisol, adrenaline, testosterone
        public readonly float[] Modes = new float[6];       // converse, learn, task, deep thought, rumination, daydream
        // OS
        public float Reason, ReasonP10, ReasonP90, HigherOs, Future, Beta, Lambda, Comparison, Married, WithKids;
        // DECIDE ($B)
        public double Spending, Saving, AllowanceOut, ChildCost;
        public float SavingRate, SaveBase, SaveTraits, SaveAge, SaveKids, SaveShift;
        public readonly double[] Category = new double[6], CategoryFear = new double[6], CategoryFantasy = new double[6];
        public readonly float[] Tilt = new float[30];       // [category × 5]: trait, drive, children, age, interest
        // ACT ($B)
        public double Financial1, Home1, Business1, Mortgage1, Consumer1, Repaid, Gains, Discharged, Closures;
        public float EquityShare, HomeBuys, HomeSales, Defaults;
        public double WealthChange, FamilyResidual;         // the score (6.6)
        // PLAY
        public float Opening, Forgiveness, TribalMemory, Cooperation; public byte Standing; public int Coalition = -1;
        public float HouseholdCooperation, FeedIsolation, FeedCollective;  // the lives' adult-mean cooperation; next year's factors (6.3b)
        public readonly float[] Strategy = new float[7];
        // OUTCOME
        public float Agency, InControl, Fantasy, Pain, Relief, Satisfaction;
        // the view
        public Vector3 MindPos; public string Listing;      // MindPos: the population mode's particle (7.8)
    }
}

// ---------------------------------------------------------------- Economy/Land/LandTween.cs
namespace Why.Economy.Land
{
    /// <summary>A year change in playback (SPEC2 6.6): main thread; layers read it in Tick.</summary>
    public static class LandTween
    {
        public const float Seconds = 0.8f;
        public static LandSnapshot Prev { get; private set; }
        public static LandSnapshot Next { get; private set; }
        public static float T { get; private set; } = 1f;               // eased (in-out cubic) 0 → 1
        public static bool Active => Prev != null && T < 1f;
        public static int[] RelatedPrev { get; private set; }           // [next player] → prev player or −1
        public static int[] RelatedNext { get; private set; }           // [prev player] → next player or −1
        public static void Begin(LandSnapshot prev, LandSnapshot next, int[] relatedPrev, int[] relatedNext) { /* WP0 */ }
        public static void Tick(float dt) { /* WP0: called by LandViewLayer */ }
        public static void Reset() { /* WP0 */ }
    }

    /// <summary>The season on screen, for the UI (SPEC2 5.0). Main thread. SeasonLayer sets it; the bowl's SocialLayer keeps its own.</summary>
    public static class LandSeason
    {
        public static SocialSeasonResult Shown { get; private set; }
        public static void SetShown(SocialSeasonResult season) { /* WP0 */ }
        public static int CoalitionsAt(SocialSeasonResult s, int round) { /* WP0: SocialLayer.CoalitionsAt's code, copied */ return 0; }
    }

    /// <summary>Points of the "must be in frame" items (SPEC2 10.2). Main thread; layers set, LandViewLayer logs.</summary>
    public static class LandFrameItems
    {
        public static void Set(string key, Vector3[] landLocalPoints) { /* WP0 */ }
        public static void ClearPrefix(string prefix) { /* WP0: a layer clears its own keys before a rebuild */ }
        public static System.Collections.Generic.IReadOnlyDictionary<string, Vector3[]> All { get; }
    }

    /// <summary>The playback clock (SPEC2 6.6). Main thread. WP0 stub; WP3 implements; LandViewLayer ticks it before LandTween.</summary>
    public static class LandPlayback
    {
        public static bool Playing { get; }
        public static void Tick(float dt) { /* WP3 */ }
    }

    // LandView (appended, WP0): the one phase function of the unfold (SPEC2 9); pure
    //  public static void Phases(float morphTime, out float sA, out float sB, out float sC, out float reveal);
}

// ---------------------------------------------------------------- Economy/EconomyState.cs (appended, WP0)
//  public enum SelectionKind : byte { None, Player, Crown, Cloud, Hill, Lake, River, Tie, Company }
//  public static SelectionKind SelectedKind { get; }  public static int SelectedIndex { get; }   // player / industry / tier / company
//  public static void Select(SelectionKind kind, int index, int tie = -1);   // sets SelectedPlayer too when kind == Player
//                                                     (so every reader of SelectedPlayer keeps working); SelectedTower stays
//                                                     −1 in the hills; SetSelection(player, tower, tie) is kept for the bowl
//  public static class LivesOverrides { public static readonly bool AllowanceOff, PlayFeedOff; }   // WHY_ECON_ALLOWANCE=0, WHY_ECON_PLAYFEED=0

// ---------------------------------------------------------------- the lives (WP0 adds, zero-filled; WP3 fills; SPEC2 6.2)
//  PersonYear: public float Financial, Home, BusinessEquity, AllowanceIn, AllowanceOut, Borrowed, Repaid, Gains,
//              DischargeAmount, ClosureLoss; public bool Dependent;
//  public struct EstateEvent { public short Year; public int From, To; public float Amount; public byte Kind; }   // 0 spouse … 4 sibling
//  EconomicLives: public IReadOnlyList<EstateEvent> Estates;  public TraceRow TraceOf(int person, int year);
//                 internal YearCalibration CalibrationOf(int year) (the existing class);  public double AllowanceTotal(int year);
//  Model/ProgramTrace.cs: public struct TraceRow { drives (10 bytes), stress, buffer, saving parts (4 shorts), tilt parts
//              (30 shorts), equity share, PlayFeed (short), flags (ushort) }  and the store (empty until WP3)

// ---------------------------------------------------------------- Humans/Smv (WP0 adds, additive; WP4 fills; SPEC2 8)
//  SmvLayer: public static float BondsAlpha = 1f;
//  SmvPopulation: public const string BondsKey = "smv.bonds"; public SmvBonds Bonds;   (null: no bonds; always null until WP4)
//  public sealed class SmvBondsOptions { the constants of 8.3 with their defaults }
//  public sealed class SmvBonds { public static void FamilyIds(SmvPopulation pop, int person, List<IdRange> into)
//              /* stub: the person's own lifeline range */ }

// ---------------------------------------------------------------- builders (final signatures; WP0 stubs, owners implement)
//  HillLayout.Arrange(EconomyData data)                                         → void (once; caches Order, DesiredX)        WP1
//  HillLayout.Build(EconomyData data, int year)                                 → TerrainGeometry (no lakes)                 WP1
//  HillLayout.AddLakes(TerrainGeometry terrain, HillAccounts accounts)          → void                                       WP1
//  MoneyAccounts.Build(EconomyModel model, PlayerSet players, int year)         → HillAccounts                               WP1
//  HillFlows.Build(EconomyModel model, TerrainGeometry terrain, PlayerSet players, HillAccounts accounts, int year) → HillMoney  WP1
//  CrownModel.Values(EconomyModel model, int year)                              → CrownValues (3.5; geometry-free)          WP2
//  ControlLedger.Build(EconomyModel model)                                      → ControlLedger (once per load)              WP2
//      bool IsController(int person, int year); int SeatOf(int person, int year) (industry, −1);
//      ControlStats StatsOf(int year); string LogOf(int year)
//  RootsModel.Select(EconomyData data, int year)                                → RootSel[] (4.9; additive)                 WP1
//  HillRoots.Build(EconomyData data, TerrainGeometry terrain, int year)         → RootGeom[] (into terrain.Roots)          WP1
//  PlayerCensus.BuildHills(EconomyModel model, SmvPopulation pop, ControlLedger ledger, int year) → PlayerSet            WP2
//  StandPlacement.Place(PlayerSet players, TerrainGeometry terrain, HillAccounts accounts, EconomyModel model,
//                       ControlLedger ledger, int year)                         → void (fills Tread … Pos, Crowns, DotAt, HomeDotAt, logs)  WP2
//  FamilyModel.Build(EconomyModel model, PlayerSet players, int year)           → FamilyResult                               WP2
//  Territories.Build(SocialSeasonResult season, PlayerSet players, TerrainGeometry terrain) → TerritoryResult            WP2
//  ProgramModel.Build(EconomyModel model, PlayerSet players, SocialSeasonResult season, int year, out ProgramYear nation,
//                     out string log)                                           → ProgramYear[]                              WP3
//  ProgramModel.Related(PlayerSet prev, PlayerSet next, EconomyModel model, out int[] relatedPrev, out int[] relatedNext)  WP3
//  ProgramModel.Listing(ProgramYear p)                                          → string (9 lines)                           WP3
//  LandMath.PackLine(IReadOnlyList<double> desired, IReadOnlyList<double> half, double lo, double hi, double gap,
//                    out bool fits)                                             → double[] centres, in the given order        WP0
//  MindFrame.ToLand(Vector3 mindLocal)                                          → Vector3 land-local (7.1)                   WP3
//  SmvBonds (Humans/Smv/SmvBonds.cs): Births, BirthOf[], BirthsAsParent[][], Featured, Glow, Checksum, Summary,
//      static void FamilyIds(SmvPopulation pop, int person, List<IdRange> into); SmvPopulation.Bonds (null in "why") WP4
```

**`EconomyState` additions** (WP0): `MindScope Mind` (default Player), `string SelectedKey`, `int MindPerson = −1`,
`bool PopulationMode`, `bool Playing`, `float SecondsPerYear = 1.5f`, `float MindPhase = 1f`, with setters `SetMind(scope,
person)`, `SetSelectedKey`, `SetPopulationMode`, `SetPlaying`, `SetSpeed`, `SetMindPhase` (each bumps `Version` and raises
`Changed`), the selection contract above (`SelectionKind`, `Select`), `LivesOverrides`, and the harness overrides read
at load: `WHY_ECON_PLAYER=<key>`, `WHY_ECON_MIND=player|nation|<person>`, `WHY_ECON_MIND_PHASE=<0..1>`,
`WHY_ECON_PLAY=<year>:<steps>`, `WHY_ECON_ALLOWANCE=0`, `WHY_ECON_PLAYFEED=0` (unset in Unity). Every land layer, panel and
picker reads the selection through `SelectedKind` / `SelectedIndex` (and `SelectedPlayer`, kept in step).

**The view tables as partial classes** (WP0 creates every part; owners fill theirs): `EconomyViews` assembles
`Rows = BaseRows ∪ TerrainRows ∪ PeopleRows ∪ MindRows ∪ CouplesRows` (each `static readonly (LandGroup group, float[]
alpha)[]` over the 18 columns of 10.3; a group missing from every part is 0; a group in two parts is a build error caught
by a static check) and calls `BaseSpecs(d)`, `TerrainSpecs(d)`, `PeopleSpecs(d)`, `MindSpecs(d)`, `CouplesSpecs(d)` for each
owner's views' label groups, held rounds, years and modes. `EconomyPresets.Build()` lists `Overview`, `Section` (base),
`TerrainPresets(land, portrait)` (landscape, capital, rivers, roots, state, y1972), `PeoplePresets(...)` (people,
society, families, crowns, betrayal), `MindPresets(...)` (mind, mind-pop, mind-land, mind1972), `CouplesPreset(...)`,
then orders them by `EconomyViews.Ids`. Both classes keep their public API, which `ViewPresets`, `LandViewLayer`, the HUD
and the tour call: `EconomyPresets.Build`, `Refresh`, `RefreshSection`, `CopyPose`; `EconomyViews.Ids`, `All`, `Get`.

**Stubs WP0 ships** (so every package starts on a running scene): `HillLayout.Build` returns the 2025 hills of 2.4 for
every year (a constant table, logged "(demo)"); `TerrainField` implements the profile formulas of 2.2-2.4 on that table;
`MoneyAccounts.Build` fills the totals from the players' records and an even split over hills ("(demo)");
`HillFlows.Build` returns no paths; `ControlLedger` names no controllers (`SeatOf` −1); `CrownModel.Values` returns
zeros; `BuildHills` = the bowl's census without its placement (zones from the group: the 1% → Cloud, owners and gig →
Slope, the rest Floor; `Hill` = the anchor's industry, else state & local); `StandPlacement.Place` puts every player **on
a floor row, never on the river**: band 1 for floor players, band 0 for slope players, band 2 for the 1%, each row packed
with `PackLine` at the hill's x on the hill's tread (so identity 8's disc clause holds from WP0 on), and fills `DotAt`
with `HillFigure.DotSlot`; `FamilyModel`, `Territories`, `ProgramModel` return empty results; `LandPlayback.Tick` does
nothing; `SmvBonds.FamilyIds` returns the person's own range; the lives' new record fields are 0 and their accessors
empty. **WP0 also implements in full**: `LandTween`, `LandSeason`, `LandFrameItems`, `LandView.Phases`, `HillFigure`,
the selection contract, the overrides, `SmvLayer.BondsAlpha` (multiplied into the SMV lines' alpha; 1 = HEAD's look), and
the wiring in `LandViewLayer.Tick`: `LandService.Tick()`, then `LandPlayback.Tick(dt)`, then `LandTween.Tick(dt)`, then
the emphasis (with `SmvLayer.BondsAlpha` from the `Bonds` row), then the `WHY_DUMP_LAND` frame lines at a preset's
`OnFocus`. Every stub's log line carries "(demo)" and its "checks" count as INFO, not FAIL, until its owner's package
lands (the first build's pattern).

### 11.3 The pipeline (`LandService.Build`, WP0; pure, any thread, deterministic)

```
load (LandModelLayer.Prepare, tier 4, worker):
  HillLayout.Arrange(data)                                    once (≤ 5 ms)
  HillLayout.Build(data, 1950 / 1972 / 2025)                  the reference years' "[Why] Hills" lines (≤ 25 ms each; logged, discarded)
  ledger = ControlLedger.Build(model)                         once, after the lives; seats per year from CrownModel.Values (≤ 25 ms)
  first  = LandService.Build(model, pop, ledger, EconomyState.Year, EconomyState.Social); LandService.Init(model, pop, ledger, first)

LandService.Build(model, pop, ledger, year, settings):
  terrain  = HillLayout.Build(data, year); terrain.Roots = HillRoots.Build(data, terrain, year)   terrain without lakes
  players  = PlayerCensus.BuildHills(model, pop, ledger, year)                    the census (Hills mode): keys, Zone, Hill
  accounts = MoneyAccounts.Build(model, players, year)                            money without geometry
  HillLayout.AddLakes(terrain, accounts)                                          lakes on the rivers (A8)
  StandPlacement.Place(players, terrain, accounts, model, ledger, year)           crowns (CrownModel.Values) → floor rows →
                                                                                  ledges → clouds → DotAt, HomeDotAt
  parallel { flows    = HillFlows.Build(model, terrain, players, accounts, year)  every FlowPath, rivers, crowns' vapor, identities
             society  = SocialSeason.Run(data, null, players, settings, year)     unchanged
             families = FamilyModel.Build(model, players, year) }
  territories = Territories.Build(society, players, terrain)
  programs    = ProgramModel.Build(model, players, society, year, out nation, out programLog)
  snapshot    = { Year, Terrain, Players, Accounts, Flows, Society, Families, Territories, Programs, Nation, Circuit }
  ChecksLine  = "terrain, roots, valleys, accounts, players, control, crowns, clouds, rivers, capital, vapor, state,
                 families, society, territories, programs" each "n/m", then PASS / FAIL
  every builder's log line is printed when the snapshot is built (blocking or not)

new season settings (WithSociety): society, territories and programs are rebuilt; the rest is shared.
year change: the whole Build on a worker; layers rebuild their meshes on workers and swap together (ReportReady); in
  playback LandTween runs (6.6); else the 0.4 s cross-fade as today.
per frame: emphasis alphas, the morph (terrain heights while 1.0 < MorphTime < 2.0), the tween, flow pulses (shader
  time), the mind's staged fronts, signals.
```

### 11.4 Algorithms (where each is specified)

Arrangement, staircase, footprints, relaxation, heights, zones, contours: 2.2-2.4. Lakes, gorges (Dijkstra), rows'
blocks, frontages: 2.5. Floor rows, ledges, crowns, clouds, families, glyph: 3.3-3.8. Accounts, runoff, router (DP over
gorges, bins), capital (steepest ascent), vapor, saving, credit, taxes, transfers, roots, identities, aggregation: 4.
Territories (kernel field, marching squares, Chaikin), ties: 5. The program, trace, allowance, chemicals, modes (IPF),
signals, aggregation, listing, related players, playback, tween, score: 6. The mind's geometry and stages: 7. The bonds
pass: 8.3. The unfold: 9. `PackLine` = pool-adjacent-violators on (desired − cumulative offsets), clamped into
[lo + half₀, hi − (offset_last + half_last)]; when the run does not fit, centred and `fits = false`.

### 11.5 Data changes

* `circuit.json capture[]` (WP2): `insiderEconomic`, `insiderVoting` (shares 0..1), `holder` (text), `holderSource`
  (the proxy statement and year when verified online, else `"recalled (unverified)"`, which the hover marks with "~").
  The values of 3.5 are the starting point; verify each against the company's latest DEF 14A / 10-K before entry.
* `groups.json` (WP2): a `zones` block `{ controlQuota: 0.204, controlQuotaSource: "DFA 2026Q1 top-1% business share ×
  private business equity + named insiders' stakes, over the top 1%'s net worth", controlVotes: 0.20, founderMin: 0.01,
  founderYears: 5, homeTilt: 0.15, homeTiltSource: "design; own-industry overweighting (Döskeland & Hvide 2011; Benartzi
  2001), recalled" }`.
* `psyche.json` (WP3): a `program` block: the chemicals' state gains (0.25, 0.25), the mode multipliers and 30 fit rounds,
  the allowance constants (φ 0.5, cap 0.25, weights 0.5 / 0.3) with the recalled anchors (Merrill Lynch & Age Wave 2018;
  Pew 2024), the play feedback (`playFeed` κ 0.15, the z clamp 2.5), the signal definitions, the mind-space ranges; each
  item with `basis: "design"` and a note.
* `spending.json` is read, not changed: its saving items split gross saving into trading assets and deposits and pensions
  (4.1); `circuit.json marketCapTotal` gives the crowns' equity values (3.5).
* Not in this build (follow-ups, said in the docs): BEA Fixed Assets Table 3.7ESI investment by industry
  (`industries.json investment2024`: would replace the investment estimate of 4.4); BEA NIPA 6.12D proprietors' income by
  industry (`proprietors2024`: would replace the crowns' "model mix" of 3.5); a `top1.businessShare` history 1989-2026;
  equity value by industry (would replace E_k's estimate, 3.5); the what-if lever (6.3b).
* Checked by `$SP/datacheck/check.sh <worktree>` (no new warnings).

### 11.6 Budgets

| Item | Thread | Budget |
| --- | --- | --- |
| The scene's load (harness: from the first Prepare to the last Upload) | workers + main | ≤ 2.9 s (HEAD ~1.6 s; + bonds ≤ 0.3 s, + lives ≤ 0.05 s, + the reference years' terrain ≤ 0.08 s, + the hills' layers) |
| The lives (allowance + trace + record fields) | worker | ≤ 790 ms (HEAD 607-740); memory + 17 MB |
| `SmvBonds.Apply` | worker | ≤ 80 ms; fine points ≤ +25%; marks ≤ 60K vertices; glow ≤ 2 MB |
| `HillLayout.Arrange` / `Build` | worker | ≤ 5 ms once / ≤ 25 ms a year |
| `ControlLedger.Build` (with the seats of every year) | worker | ≤ 25 ms once |
| Census + accounts + placement (crowns, clouds) | worker | ≤ 35 + 10 + 10 ms |
| `HillFlows.Build` | worker | ≤ 25 ms |
| Season (unchanged) ∥ families ∥ flows; territories; programs | worker | ≤ 70 ms; ≤ 25 ms; ≤ 9 ms |
| One snapshot (a year) | worker | ≤ 200 ms |
| Layers' rebuild of a year (all land layers, workers) | workers | ≤ 150 ms; a playback step well under the 1.5 s dwell |
| Vertices | | terrain ≤ 100K (+ ≤ 10K fine patches, 2.7), flows ≤ 60K, people ≤ 55K (home dots, debt rings), crowns ≤ 12K (stems, seats), families ≤ 30K, season ≤ 32K (ladders, pyramids), cut 15K + morph ≤ 25K, signals ≤ 3K, mind ≤ 26K + particles ≤ 6K, SMV marks ≤ 60K (the bowl's ~230K land vertices are not created) |
| Per frame (main) | main | the morph ≤ 2 ms; the tween's markers ≤ 1 ms; the mind's fronts ≤ 1 ms; the year morph of the mind ≤ 0.8 ms |

### 11.7 Determinism

No random numbers in new code (the existing seeded simulations are unchanged). Every sort ends on an index tie-break
(person, player, industry, company, cell); relaxation and separation loops run a fixed number of iterations in a fixed
order; Dijkstra and the router break ties by cell index / the smaller x; sums run in index order; doubles in models,
floats in meshes; `Parallel.For` bodies write only their own rows; glitter and sparkles by `LandMath.Hash01`; animation
time from the frame clock (pinned by the harness through `WHY_ECON_MORPH`, `WHY_ECON_MIND_PHASE`, `WHY_ECON_PLAY`).
Every builder prints a checksum: terrain Σ(x + z + summit); flows Σ dollars × point count; ledger Σ(year × controller
index × (seat industry + 1)); placement Σ index × (x + 2y + 3z); crowns Σ value; families Σ estate dollars × heir index; territories Σ boundary
x; trace Σ quantized fields × row; programs Σ index × (spending + 3 reason + 7 cortisol); mind mesh Σ vertex x; bonds
Σ (y × 1000 + ρ). **Two runs with `WHY_NOW` pinned print identical log lines and byte-identical `.base.png` files.**

### 11.8 Verifying with the headless harness

**Setup and commands** (per package, private copies):

```
SP=/tmp/claude-0/-home-user-why-2/dc9bf553-91de-54d2-9f1f-9831e1f24cfa/scratchpad; export WHY_NOW=2026-10-01T12:00:00Z
$SP/agent-tools.sh <wp> <worktree>
$SP/cc-<wp>/check.sh <worktree>                                                   # COMPILE OK (Economy/UI included)
$SP/datacheck/check.sh <worktree>                                                 # data loads, no new warnings
OUT=$SP/redesign2/build/<wp>
WHY_ANCHORS_OUT=$OUT/anchors.json WHY_DUMP_LAND=1 WHY_REPO=<worktree> $SP/run-<wp>/run.sh --scene economy --render all --out $OUT/land > $OUT/land.log
WHY_REPO=<worktree> $SP/run-<wp>/run.sh --scene economy --size 1080x1920 --render all --out $OUT/portrait > $OUT/portrait.log
WHY_ECON_MORPH=0.9 WHY_REPO=<worktree> $SP/run-<wp>/run.sh --scene economy --render landscape --out $OUT/stairs > $OUT/stairs.log
WHY_ECON_MORPH=1.5 WHY_REPO=<worktree> $SP/run-<wp>/run.sh --scene economy --render landscape --out $OUT/rise > $OUT/rise.log
WHY_ECON_MIND_PHASE=0.40 WHY_REPO=<worktree> $SP/run-<wp>/run.sh --scene economy --render mind --out $OUT/phase > $OUT/phase.log
WHY_ECON_PLAYER='out_of_work|safety_net/genx|I' WHY_REPO=<worktree> $SP/run-<wp>/run.sh --scene economy --render mind --out $OUT/mind-ow > $OUT/mind-ow.log
WHY_ECON_PLAY=1972:2 WHY_REPO=<worktree> $SP/run-<wp>/run.sh --scene economy --render people --out $OUT/play > $OUT/play.log
WHY_ECON_ALLOWANCE=0 WHY_ECON_PLAYFEED=0 WHY_REPO=<worktree> $SP/run-<wp>/run.sh --scene economy --render section --out $OUT/lives0 > $OUT/lives0.log   # the lives' identity proof
SMV_BONDS=0 WHY_REPO=<worktree> $SP/run-<wp>/run.sh --scene economy --render couples --out $OUT/nobonds > $OUT/nobonds.log
$SP/whycheck.sh <worktree> <wp>                                                   # "14 presets, 0 differ: PASS"
python3 $SP/redesign2/checks/hillcheck.py $OUT/land.log $OUT --expect <wp list> [--log2 <second run's log>]
python3 $SP/redesign2/synth/terrain2.py 2025 1972 1950                            # the terrain reference
```

**Environment variables**: `WHY_NOW` (pins the clock), `WHY_ECON_YEAR`, `WHY_ECON_MORPH`, `WHY_ECON_ROUND`,
`WHY_ECON_SELECT` (`player:<key>`, `crown:<industry>`, `cloud:<key>`, `hill:<industry>`, `lake:<industry>`, `pair:<k>`, through `EconomyState.Select`), `WHY_ECON_REWIRE`,
`WHY_ECON_FORGIVE` (existing); `WHY_ECON_PLAYER`, `WHY_ECON_MIND`, `WHY_ECON_MIND_PHASE`, `WHY_ECON_PLAY`,
`WHY_ECON_ALLOWANCE=0` (φ = 0), `WHY_ECON_PLAYFEED=0` (κ = 0) (new, WP0 parses, WP3 / WP5 act); `WHY_DUMP_LAND=1` (new;
at each preset's `OnFocus`: `PeopleLayer` (WP2) logs "[Why] Stand screen <preset>: <key> <zone> <x> <y> <worldY>; …" with
the camera's `WorldToScreenPoint` of each player's `Pos`, and "[Why] Stand crowns <preset>: <industry> <band-bottom
screen y> <top screen y> <x0> <x1>; …"; `LandViewLayer` (WP0 wiring) logs the "[Why] Frame <preset>: <key> in|out <box>"
lines of every frame item (10.2): crowns, clouds, threads, rows, roots, …, which are also the boxes the pixel checks
use); `SMV_BONDS=0` (harness-only, WP4); `WHY_ANCHORS_OUT` (harness: keys, labels, blurbs).

**Log lines** (exact prefixes; 2025 unless said; expected values with tolerances in brackets; a line printed by a stub
carries "(demo)"):

```
[Why] economic lives: 4996 lines x 100,000 people, 1946-2026: … (allowance φ 0.5, play feedback κ 0.15)  WP3
      the 27-line report: with φ = 0 and κ = 0 (lives0.log) identical to HEAD's but the size and timing line; with
      φ 0.5 the diffs of 6.3, with κ 0.15 only category and fear-share lines move
[Why] Lives play feedback: κ 0.15; 2025 collective share of the top / bottom cooperation quintile a% / b%; category
      factors max |Δ| ≤ 0.02; totals by source unchanged; checks 2/2 PASS                                 WP3 [a > b]
[Why] Lives allowance: 1972 $20B (104 of 153 dependents) … 2025 $379B (175 of 295; 1.7% of DPI; 263 paying households;
      PMC pays 82%); 2018 ≈ $260B vs ~$500B for 18-34 (Merrill Lynch 2018, recalled); checks 2/2 PASS            [±2%; counts ±3]
[Why] Program trace: 1946-2026 ≈110,000 household-years, 10.7 MB; shares from parts max |Δ| ≤ 0.001; saving rates
      max |Δ| ≤ 0.0005; Wealth = Financial + Home + Business − Debt max |Δ| ≤ $1; checks 3/3 PASS; checksum …
[Why] SmvLayer.Prepare <ms>: 4996 lines x 100,000 people (500 coarse), 681 steps from 1856.5, 962772 samples -> 183161 fine /
      18291 coarse points in 3 + 1 meshes, 0 parent links; 2078 marriages, 797 divorces, 4353 births with a mother,
      530 immigrants                                                         WP4 [every count exact; the timing free]
[Why] SmvBonds … ms: 3090 births at the mid-plane from 1946 (2795 married parents, 295 unmarried, 0 mother only;
      0 misses; 1263 older births kept), 0 shared steps, max 10 births per parent, 8841 bridged samples; parents lift
      1.44 (max 4.58) value units, reach 0.095 married / 0.230 unmarried x envelope; newborns drop 2.40; 401124
      married samples squeezed x0.50; 530 arrivals from the edge; checksum 735025915.254744; marks: 3090 beads (848
      coarse), 2401 ties, 542 coarse arms, 43928 vertices                                                [exact]
[Why] SmvBonds featured marriage 846: wife 1752 husband 1646 from 1972.50; births 1973.00@y0.796 … 1990.25@y0.775  [exact]
[Why] Hills 2025: GDP $30.76T = 46 u2 per GDP (1 u2 = $0.67T); land 14.0 x 15.15; treads gov 0.00-1.20 raw 2.90-4.10
      make 5.61-6.81 services 8.90-10.10 tech 12.69-13.89 (zones 0.81 1.39 1.90 0.91); summits gov 0.35 raw 0.97-1.08
      make 2.06 services 2.49-2.90 tech 3.17-3.23 strictly ordered; steepest hill 55 deg, mesa edges 63, risers 56;
      IO from a range in front 21.9% within 41.0% from behind 37.2%; adjacent within 2 u 54%; 25 hills, areas 46.00 u2;
      highest summit 3.23 < crown base 3.55; checks 4/4 PASS; checksum …                                                    WP1 [d and summits ±0.02; IO exact]
[Why] Hills 1972: … treads … make 5.71-6.91 services 9.40-10.60 tech 12.75-13.95 (zones 0.91 1.79 1.45 0.85) …
[Why] Hills 1950: … make 5.85-7.05 services 9.63-10.83 tech 12.79-13.99 (zones 1.05 1.88 1.26 0.81) …
      (the 1950 and 1972 lines are printed by LandModelLayer at load from HillLayout.Build of those years, 11.3; every
      Hills line ends "highest summit <y> < crown base 3.55")
[Why] Roots 2025: 79 roots >= $50B carry 72.7% of $15.85T; mesh n, balls n; dollars = the table's kept total scaled to
      the year; checks 1/1 PASS                                                                       WP1 [exact]
[Why] Valleys 2025: rivers 5; gorges gov->raw -6.50 -4.45 -0.70, raw->make -6.50 -4.45 -2.40, make->services -4.85
      +0.65 +5.70, services->tech -1.25 +1.45 +3.55; lakes n (health 2.23 x 0.32, trade 1.91 x 0.32, real_estate 1.89 x
      0.32, manufacturing 1.25 x 0.32); cloud level 4.50; checks 2/2 PASS                     [gorges ±0.3; lake widths ±5%]
[Why] Accounts 2025: wages $15.73T, business $2.11T, capital $5.33T, transfers $4.95T, taxes $5.25T (federal 87%),
      spending $21.63T (abroad $2.27T), saving $2.16T, borrowing $0.93T (net $1.23T), allowance $0.38T in = out; RAS n it,
      error ≤ 0.1%; pools $19.36T; players in = out max 0.0%; checks 3/3 PASS; checksum …    [totals ±0.5%; pools ±2%]
[Why] Players 2025: 130 players (1% n, owners n, …); zones floor 109 (gov tread n), slope 12, crown 5, cloud 4; rows by
      wealth 48/18/24/19 (median $134K), monotone; spills 0, max |x − hill x| 0.37 u, outside frontage 2; ledges:
      overflows 0; checks 5/5 PASS; checksum …                                                                   WP2
      [players 122-136; floor 100-115; slope 10-16; crown 4-7; cloud 3-6; spills 0; max shift ≤ 0.8; outside ≤ 4;
       row order monotone in wealth within every hill's players]
[Why] Control 2025: 1% 27 lines; controllers 6 lines in n players (n running a business, founders n, heirs n, filled n);
      control wealth 22.1% of the 1%'s (quota 20.4%); seats internet 2, manufacturing 1, trade 1, insurance 1, software 1
      (own industry 2/6, own tier 4/6; kept from last year n); successions 1950-2026 n; 1972: 4 controllers of 14 lines;
      checks 4/4 PASS; checksum …   [controllers 4-8; share ≥ quota and ≤ quota + one unit; successions ≤ 3; every crown
      ≥ $1T seated; seats = the Hamilton apportionment of V_k exactly]
[Why] Crowns 2025: 19 crowns >= $100B; controlled named $10.64T + founder stakes $0.72T + the 1%'s private $8.46T =
      $19.82T; largest internet $6.07T r 0.333 y 3.82 (Page & Brin, Zuckerberg); band 3.59-3.82, highest top 4.05 (limit
      4.20); controlled shares internet 0.99 … hardware 0.02 (estimate); named seats n; private split (model mix); lifts n;
      checks 4/4 PASS     [crowns 15-21; named exact; private ±0.1 of 0.529 × PBE; bottoms ≥ 3.55; top ≤ 4.20; c_k ±0.05]
[Why] Clouds 2025: 4 clouds, 21 lines, $36T; home tilt 0.15; placed by overweights; gaps (finance|professional)
      (internet|software) (trade|transport) (federal|state_local); span x 2.2 z 12.4 u; shift from the overweight centre
      max 0.32 u; min gap 0.05, to crowns >= 0.10, over a hill (ρ < 0.8) 0; checks 4/4 PASS
      [clouds 3-6; shift ≤ 0.6; distinct gaps ≥ 3 (or span x ≥ 4 u)]
[Why] Rivers 2025: spending $21.63T: lakes $19.36T, abroad $2.27T; level a% up b% down c%; busiest river $xT one way
      (w u), half-width max ≤ 0.14; busiest gorge …; glitter n sparks; discs on a river 0; checks 3/3 PASS   WP1
      [level 30-50%, up 25-45%; busiest one-way ≤ 0.25 u]
[Why] Capital 2025: investment $5.67T (estimate: ~ depreciation), real estate $1.58T, manufacturing $517B, trade $455B,
      professional $351B; external $1.23T (saving $2.16T − credit $0.93T), internal $4.44T; rivers 23; uphill
      violations 0; checks 2/2 PASS                                                       [±0.5%; violations 0]
[Why] Vapor 2025: business $2.11T, capital $5.33T (real estate $1.69T, banking $1.15T, trade $0.51T), abroad $435B;
      through crowns $xT (internet $xB …), seated $xB, onward $xT; wisps to crowns 19, to clouds n, rain n; saving
      trading ≈ $1.10T deposits ≈ $1.06T (51% / 49%); cloud level in $2.16T out $2.16T; checks 4/4 PASS
      [±0.5%; every crown an inflow; identities 12 and 13]
[Why] State 2025: taxes $5.25T personal + $2.15T production + $452B corporate; mesas federal $..T state & local $..T;
      transfers $4.95T in n fountains; checks 1/1 PASS                                    [±0.5%]
[Why] Families 2025: minors 727 under 91 players (stems 727, 35 without a parent); dependents 295 (259 threads, 259
      home dots, allowance $379B in 175); child cost $1.53T ($22.0K a child); estates 2021-2025 158 from a parent ($5.61T,
      35 in 2025); ghosts n; successions 0; inherited share cloud 0.86 crown n floor 0.26 slope 0.18; checks 4/4 PASS
      [counts ±10%; dollars ±5%; cloud > floor and crown > floor; home dots = threads]                   WP2
[Why] Society 2025: … (the season's line, unchanged in form; players as the census's)                    [checks 2/2]
[Why] Territories 2025: 4 coalitions over 5 detections; largest component ≥ 0.8 of each coalition's players; ties
      drawn ≤ 120; ladders 4 (round 0 → 96 in-group cooperation n → n), pyramids 4; checks 2/2 PASS        WP2
[Why] SeasonLayer ready: SeasonLayer reported version n (season society|betrayal|incident)                       WP2
[Why] Mind 2025: chemicals (adult means) 1.000 x7; 1972 dopamine 1.12 serotonin 1.13 endorphins 1.13 oxytocin 0.99
      testosterone 1.02 cortisol 0.88 adrenaline 0.90; modes = psyche shares ±0.001; signals pain 7.0% relief 4.8%
      satisfaction 14.3%; checks 2/2 PASS                                          WP3 [±0.02; signals ±2 pt]
[Why] Programs 2025: n players; Σ players = lives: wages $15.73T spending $21.63T saving $1.23T taxes $5.25T (0.000%);
      allowance $379B in = out; default frontline|trade|I; ranges cortisol 0.60-1.52 rumination 0.04-0.27 reason
      0.19-0.77; checks 4/4 PASS; checksum …                                     [sums exact to 0.1%; ranges ±0.05]
[Why] Program listing 2025 frontline|trade|I: <the 9 lines of 6.5, tab-separated>
[Why] Score 2025: Σ players ΔW + arrivals − estates leaving = the lives' net-worth change within $1B; family residual
      |Σ| ≤ $5B; checks 1/1 PASS
[Why] Land checks 2025: terrain 4/4, roots 1/1, valleys 2/2, accounts 3/3, players 5/5, control 4/4, crowns 4/4,
      clouds 4/4, rivers 3/3, capital 2/2, vapor 4/4, state 1/1, families 4/4, society 2/2, territories 2/2, programs 4/4
      PASS                                                                                                       WP0
[Why] Facts 2025: … (the model's thesis numbers, unchanged in form)
[Why] TerrainLayer.Prepare <ms>: <n> vertices (surface 21.4K, contours n, rims n, lakes n, roots n, overlays n)      WP1
[Why] HillFlowsLayer.Prepare <ms>: <n> vertices (rivers n, glitter n, wages n, capital n, vapor n, state n); paths n (level 0 n, level 1 n)
[Why] PeopleLayer.Prepare <ms>: <n> vertices; n players, n dots; CrownsLayer.Prepare …; FamiliesLayer.Prepare …;
      SeasonLayer.Prepare …                                                                                WP2
[Why] CutLayer.Prepare <ms>: card n vertices, n adult and n child dots (n gold: the top 1%); readout "2025 · GDP $30.8T
      · 274M adults in 2,742 lines · gold: the top 1%"; [Why] CutLayer morph (unfold): n frames, mean ≤ 2 ms   WP5
      [gold dots = the year's top-1% adult lines exactly; ≤ 3% of adult dots]
[Why] MindLayer 2025 player frontline|trade|I: ≈19K vertices, build n ms; reason line y 2.52; gate $397B           WP3
[Why] Playback 1972 → 1974: 2 steps, 2 tweens, max frame n ms, waited 0 ms (blocking builds)                    WP3
[Why] Views: 18 presets (10 keyed); emphasis 43 groups x 18; label groups ok                                     WP5
```

**`hillcheck.py`** (the driver, WP0) runs the modules and prints `hillcheck <module>.<check>: PASS|FAIL|INFO|SKIP
(details)`, exit 1 on any required FAIL; `--expect` lists the packages whose lines must be real (a "(demo)" line of a
listed package FAILs; of an unlisted one is INFO). **An expected value whose source is another package** (each is tagged
`"from": "<wp>"` in the module's JSON: the allowance in the Accounts and Families lines from WP3, the seats and the crowns'
inflows from WP2, the terrain's real geometry from WP1, …) **is checked only when that package is in `--expect`**, and INFO
otherwise, so every package's own checks can pass on its branch before the packages it reads from have merged (12.1).
Modules and their checks:

| Module (owner) | Check | Rule |
| --- | --- | --- |
| hc_terrain (WP1) | `log` | the Hills (2025, 1972, 1950), Roots, Valleys, Accounts, Rivers, Capital, Vapor, State lines parse and match the expectations above |
| | `reference` | `[Why] Hills 2025/1972/1950` treads, footprints and summits = `synth/terrain2.py` ±0.02 |
| | `labels` | landscape and y1972: 5 bench labels and ≥ 25 (16:9) / ≥ 24 (9:16) hill labels placed |
| | `hues` | `rivers.base.png`: ice (hue 180-200°, s ≥ 0.3, v ≥ 0.25) ≥ 0.8% of the frame, rose (325-350°) ≥ 0.5%, 0.8 ≤ ice / rose ≤ 2.5; inside each lake label's box both hues |
| | `gold` | `capital.base.png`: gold (30-50°, s ≥ 0.4) ≥ 3% of the frame; above each of the 3 largest caps (± 20 px of the cap's projected x, from the summit up to the cloud level; the boxes from the Frame lines of `land:capital:<industry>`) ≥ 40 gold pixels |
| | `overlay` | the AI block's top (Frame line `land:ai`, world y in the dump) ≤ the tech range's highest summit |
| | `stairs` | `stairs/landscape.base.png` differs from `land/landscape.base.png` in ≥ 5% of pixels; `rise/` differs from both by ≥ 2% |
| hc_people (WP2) | `log` | the Players, Control, Crowns, Clouds, Families, Territories, SeasonLayer ready lines |
| | `geometry` | (in-builder checks, counted in the lines) every player placed; no two discs of a row closer than 0.03; floor y = ground ± 0.005; floor rows monotone in mean wealth within each hill's players; slope ledges on their contour ± 0.02 and elevation non-decreasing in wealth per hill; every crown's band bottom ≥ 3.55 and > its summit + 0.15, top ≤ 4.20; clouds at y 4.50 exactly; no cloud over another cloud, a crown or a hill (ρ < 0.8); every child's dot below its parent's; Σ estate streams = Σ window events |
| | `seats` | (Control and Crowns lines) every crown ≥ $1T has ≥ 1 seated controller line and a vapor inflow; every controller line sits on its ledger seat (`SeatOf`) and nowhere else; the seats equal the Hamilton apportionment; every crown with a controlled jewel has its named seats |
| | `stack` | **strict, in world y** (every land view: from the Stand screen dumps' world y): max over floor and slope `Pos` (and their dots) < min crown band bottom; max crown top < 4.50 the clouds. **Strict, in screen y in the `crowns` view** (16:9 and 9:16; "Stand screen crowns" and "Stand crowns crowns"): every floor and slope player's screen y below (larger than) every crown's band-bottom screen y by ≥ 6 px, every crown top's above every cloud's by ≥ 6 px. **Per hill in the `people` and `capital` views** (oblique: a land seen from above shows depth as height, so only the column is compared): each crown's band bottom projects above its own summit and above every floor and slope player of its own hill |
| | `crowns` | `crowns.base.png`: in each of the 5 largest crowns' boxes (the Frame lines of `land:crown:<industry>`) ≥ 40 gold pixels (38-52°, s ≥ 0.45, v ≥ 0.55); cloud pixels (s ≤ 0.25, v 0.25-0.6) above every crown box |
| | `clouds` | the Clouds line: ≥ 3 distinct gaps (or span x ≥ 4 u); in `crowns.base.png` at 16:9 every cloud's Frame box is in, and no two clouds' boxes overlap by more than 25% of the smaller |
| | `families` | ≥ 95% of the in-frame child dots project below their parent dots (dump); in the `people` view ≥ 90% of the dependents with a living parent in a player have a home dot in frame (Frame lines `land:home:<person>`) |
| | `portrait` | `portrait/crowns.png`: the 5 largest crowns and ≥ 3 clouds inside the frame (Frame lines) |
| | `games` | `society.base.png`: each coalition's `land:ladder:<n>` Frame box in, with ≥ 30 non-black pixels; the Territories line's ladder values rise from round 0 to round 96 for ≥ 3 of 4 coalitions |
| hc_mind (WP3) | `lives` | `lives0.log` (`WHY_ECON_ALLOWANCE=0 WHY_ECON_PLAYFEED=0`): the 27-line report identical to HEAD's (`$SP/redesign/build/lead-fix6/land.log`) but the size and timing line; the main run: the diffs of 6.3, and against a φ-only run only category and fear-share lines differ |
| | `log` | the Lives allowance, Lives play feedback, Program trace, Mind, Programs, Program listing, Score, MindLayer, Playback lines |
| | `poles` | `mind.base.png`: inside the shell's box, ice pixels (185-205°) left of the fissure vs rose (320-350°) right, ratio within ±30% of (0.5 + 1.5f) / (0.5 + 1.5(1 − f)) (f = the fear share) |
| | `reason` | the `mind:reason` label's screen y between `mind:top` and `mind:bottom` at the fraction reason ± 0.05 from the top |
| | `gate` | 8 gate labels for the default player (7 categories incl. saving + allowance out) summing to spending + saving + allowance out ± $1B; jeopardy left of escapism |
| | `chemicals` | the 7 vial labels ordered cortisol → serotonin left to right; cortisol's vial taller for `out_of_work\|safety_net/genx\|I` than for the default player |
| | `population` | `mind-pop`: one particle anchor per player; ≥ 90% inside the frame; the 1% group label above the out-of-work label |
| | `phase` | `phase/mind.base.png`: the Decide renderer's lit pixels < 5% of the settled render's; the drive nodes' ≥ 80% |
| | `portrait` | `portrait/mind.png`: the shell, the gate labels and the memory shelf inside the frame |
| hc_smv (WP4) | `log` | the SmvLayer and SmvBonds lines exact; misses 0; married + unmarried + mother-only = births; births + older = births with a mother; beads = births; parent links 0 |
| | `knot` | `couples.base.png`: the brightest channel in a 9×9 window at the "a child, 1985" label anchor ≥ 230 (with `SMV_BONDS=0`: lower) |
| | `labels` | couples: "mother", "father", "married 1972", "a child, 1985 …" placed; no bench, hill or land label placed |
| hc_views (WP5) | `presets` | all 18 presets render at 16:9 and 9:16 without exceptions; no placed label of priority ≥ 30 outside the image; each preset's must-be-in-frame items "in" in its Frame lines (10.2) |
| | `panels` | the scratch-harness run with `Economy/UI` compiled: `EconomyUiLayout.Check` reports no panel over `HillPick.Outline` or the mind's shell at 1920×1080 and 1080×1920; `SocialPanel` shows the coalitions in `society`; the player panel shows a crown player's seat |
| | `bowl` | the economy log has no Prepare line of `LandscapeLayer`, `FlowsLayer`, `InhabitantsLayer`, `SocialLayer`, `SectionLayer`; the five files carry `[GraphScenes(EconomyScenes.Bowl)]` |
| | `cut` | `section.base.png` has the frame, 25 bars and ≥ 2,000 dot pixels; the readout has no "in control"; the CutLayer line's gold dots = the top-1% lines and ≤ 3% of adult dots |
| | `emphasis` | the Views line: 43 rows × 18 columns; no group defined twice |
| | `restore` | (integration) `restore-bowl.sh` output: compile OK, `landscape` and `people` render without exception with the five bowl layers' Prepare lines and no hill layer's; side by side with `$SP/redesign/build/lead-fix6/land/{landscape,people}.base.png` (expected differences: the allowance's players 116 → 118, the lifelines' gold rule) |
| hc_tour (WP6) | `tour` | every tour step's `focus` is a preset id and its `anchor` / `highlight` keys exist in anchors.json; the numbers in its text match the log within rounding |
| all | `why` | `whycheck.sh`: 14 presets, 0 differ; the Why log's `SmvLayer.Prepare` line = HEAD's (152948 / 15295 / 4283 parent links) and no `SmvBonds` line |
| all | `determinism` | `--log2`: every checksum and log line equal; `.base.png` byte-identical |

---

## 12. Build plan

Seven work packages: **WP0 (contracts and skeleton) alone first**, then **WP1-WP6 in parallel**, each in its own git
worktree branched from WP0's merge, with disjoint file ownership (11.1). No package edits a file it does not own; a
package that needs a contract change asks the lead (the change goes into WP0's files on the integration branch and every
package rebases). Each package commits with the repository's attribution lines and hands over: its commit, its render
folder `$SP/redesign2/build/<wp>/`, its logs and its `hillcheck` output.

```
git -C /home/user/why-2 worktree add $SP/wt/wp0 -b claude/hills-wp0 claude/economy-visualization-2026-n8j6kd
… WP0 merges into claude/economy-visualization-2026-n8j6kd …
git -C /home/user/why-2 worktree add $SP/wt/wp<k> -b claude/hills-wp<k> claude/economy-visualization-2026-n8j6kd   (k = 1..6)
```

### WP0: contracts and skeleton (lead; alone; ~1 day)

* **Content**: 1.1 (the `Bowl/` move and retags, `EconomyScenes`, `BowlPresets`, `BowlViews`, `LandService.BuildBowl`,
  `restore-bowl.sh`); **every contract of 11.2**: `LandTypes` additions, `HillTypes` (with `HillFigure`), `PeopleTypes`
  (with `CrownValues`), `ProgramTypes`, `HillIds`, `LandTween` (full), `LandSeason` (full), `LandFrameItems` (full),
  `LandView.Phases` (full), `LandPlayback` (stub), `CrownModel` (stub), `LandMath.PackLine`, `LandStyle.CacheYears` 6,
  `EconomyState` (the selection contract, `LivesOverrides`, the mind and playback state, the overrides), **the lives'
  record fields, `EstateEvent`, `TraceRow` and accessors as zero stubs** (no number changes: the 27-line report identical
  to HEAD's), **`SmvLayer.BondsAlpha`, `SmvPopulation.BondsKey` / `Bonds`, `SmvBondsOptions`, the `SmvBonds` stub**;
  the wiring of `LandViewLayer.Tick` (11.2); the pipeline (11.3) in `LandService` and `LandModelLayer` with the stubs of
  11.2 (placement on the rows); `EconomyViews` / `EconomyPresets` rewritten as partial classes with all 18 presets (the
  poses of 10.2, the table of 10.3) and every partial file created; the checks (`hillcheck.py` and all modules with the
  expectations of 11.8, "(demo)"- and `from`-aware).
* **Done when**: COMPILE OK; the economy renders all 18 presets at 16:9 and 9:16 with no exception; every log line of
  11.8 prints (stubs with "(demo)"); `hillcheck --expect wp0` PASS (all owned lines real, the rest INFO); `hc_views.bowl`
  PASS (no bowl layer created, all five retagged); `hc_mind.lives` PASS on the record stubs (the report identical to
  HEAD's); identity 8's disc clause PASS on the stub rows; the `WHY_ECON_PLAY=1972:2` run prints a Playback line (stub:
  "0 steps (demo)") and the tween ticks; `whycheck` PASS (the SMV additions change nothing in the causality scene);
  `git grep -n "class LandLayout\|class MoneyRouting"` finds them under `Economy/Bowl/`.

### WP1: the terrain and the flows

* **Owns**: `Land/Hills/*` (but `HillTypes.cs`), `Land/RootsModel.cs` (the additive `Select` only),
  `Layers/TerrainLayer.cs`, `Layers/HillFlowsLayer.cs`, `Views/EconomyViews.Terrain.cs`, `Views/EconomyPresets.Terrain.cs`,
  `checks/hc_terrain.*`.
* **Content**: 2 (arrangement, staircase, hills, zones, treads, lakes, gorges, frontages, company marks, overlays, the
  terrain mesh with its fine patches and its morph by `LandView.Phases` and `LandTween`, contours, rims, bedrock, labels,
  anchors, frame items), 4 (the accounts, the saving split, the router, every flow, the crowns' vapor, roots through
  `RootsModel.Select` and `HillRoots`, identities, aggregation levels), the poses and rows of its views.
* **Done when** (on its branch; values `from` WP2 / WP3 are INFO until those merge, 11.8): `hc_terrain` all PASS (log,
  reference against `terrain2.py`, labels, hues, gold, overlay, stairs); identities 1-13 PASS in the log (identity 12 is
  vacuous on WP0's zero crowns, real after WP2); budgets of 11.6 held (the Prepare lines); determinism PASS; COMPILE OK;
  whycheck PASS.

### WP2: people, wealth, the season and the families

* **Owns**: `Land/PlayerCensus.cs`, `Land/People/*` (but `PeopleTypes.cs`; with `CrownModel.cs` after WP0's stub),
  `Layers/PeopleLayer.cs`, `CrownsLayer.cs`, `FamiliesLayer.cs`, `SeasonLayer.cs`, `Views/*.People.cs`, `circuit.json`
  (capture insider fields and holders), `groups.json` (zones), `checks/hc_people.*`.
* **Content**: 3 (the ledger and its seats, `CrownModel.Values`, the Hills census, rows by wealth, ledges, crowns in the
  band with stems, jewels and named seats, clouds by overweights, children, dependents and home dots, estates,
  inheritance, the glyph with the debt ring, glide markers, the Stand dumps), 5 (`SeasonLayer` with the duties of 5.0,
  territories, ties, standing, ladders and pyramids, the tie events' names), the poses and rows of people, society,
  families, crowns, betrayal.
* **Done when** (measured after rebasing on WP3 and WP1, which merge before it, 12.1; on its branch before that, the
  values `from` WP3 (dependents, allowance, estates, the heir rule) are INFO): `hc_people` all PASS (log, geometry, seats,
  stack, crowns, clouds, families, portrait, games); determinism; COMPILE OK; whycheck PASS.

### WP3: the lives, the program and the mind

* **Owns**: `Model/LivesSimulation.cs`, `EconomicLives.cs`, `LivesReport.cs`, `ProgramTrace.cs`, `MindModel.cs`,
  `Land/Mind/*` (but `ProgramTypes.cs`), `Layers/MindLayer.cs`, `Layers/SignalsLayer.cs`, `Views/*.Mind.cs`,
  `psyche.json` (program block), `checks/hc_mind.*`.
* **Content**: 6.2 (filling WP0's record fields, the estate log, the trace: first, with φ = 0 and κ = 0, proving "0 lines
  differ"), 6.3 (the allowance), 6.3b (the play feedback), 6.4, 6.5, 6.6 (`LandPlayback` replacing WP0's stub, the
  related players, signals, the score), 7 (the mind view), 8.5's line style (gold = the top 1%).
* **Done when** (WP3 merges first, so it depends only on WP0: the Hills census is WP0's stub, whose bowl keys include
  `frontline|trade|I`): `hc_mind` all PASS (lives, log, poles, reason, gate, chemicals, population, phase, portrait); the
  φ = 0, κ = 0 run proves the record change alone moves nothing; the allowance's numbers within 6.3's; the play
  feedback's line PASS; the Playback line "2 steps, 2 tweens" (the tick is WP0's wiring); determinism; COMPILE OK;
  whycheck PASS.

### WP4: couples and births on the population graph

* **Owns**: `Humans/Smv/SmvBonds.cs`, `SmvShared.cs`, `SmvGeometry.cs`, `SmvLayer.cs`, `Economy/Layers/EconomyLoaderLayer.cs`,
  `Economy/UI/PersonLine.cs`, `Views/*.Couples.cs`, `checks/hc_smv.*`.
* **Content**: 8 (apply `$SP/redesign2/smv/proto.patch` as the starting point on top of WP0's additive SMV members, then
  the `couples` id and key 9, the `Bonds` row, the family lines and the real `FamilyIds`).
* **Done when**: `hc_smv` all PASS (log exact, knot, labels); **whycheck PASS and the Why log's `SmvLayer.Prepare` line
  unchanged, no `SmvBonds` line in Why**; determinism (the bonds checksum on two runs); COMPILE OK.

### WP5: the cut, the views and the UI

* **Owns**: `Layers/CutLayer.cs`, `Layers/LandViewLayer.cs`, `Land/LandView.cs`, `Land/HillPick.cs`,
  `EconomyViews.cs`, `EconomyPresets.cs`, `Views/*.Base.cs`, every `Economy/UI/*` file except `PersonLine.cs`,
  `checks/hc_views.*`.
* **Content**: 9 (the cut copied from `SectionLayer`, its gold rule and readout, the unfold on `LandView.Phases`, the
  tiles), 10 (the catalog assembly, overview and section poses, the portrait rule, `LandViewLayer` applying views on top
  of WP0's wiring, the keys of 10.6, the selection through `EconomyState.Select`, the panels, `MindPanel`, picking and
  the outline with `HillPick`, the legend, **every UI edit of the table in 10.6, each keeping its bowl branch**).
* **Done when** (measured after rebasing on WP1, WP2 and WP4, which merge before it; on its branch before that, the
  must-be-in-frame items of other packages' layers are INFO): `hc_views` all PASS (presets, panels, bowl, cut, emphasis);
  `stairs` / `rise` renders differ as required; the UI compiles (COMPILE OK covers `Economy/UI`); whycheck PASS.

### WP6: the tour and the docs

* **Owns**: `Assets/Resources/Data/economy/tour.json`, `Docs/ECONOMY.md`, `Docs/ARCHITECTURE.md`, `README.md`,
  `Director/TourData.cs` (only `EconomyBuiltIn`), `checks/hc_tour.*`.
* **Content**: a **13-step** tour (road; the cut; the hills of value; what each hill stands on; capital climbs; who sits
  where; crowns and clouds; where the money goes; the program (mind); how people organize; couples and children; families
  and inheritance; any year and Space) with focus presets, anchors and numbers from the log; `ECONOMY.md` rewritten for the
  hills (reading the scene, the model, the program, the views and keys, limits, the restore recipe of 1.1, the follow-ups
  of 11.5); `ARCHITECTURE.md`'s scenes section names `economy-bowl` as retired; the built-in fallback tour of
  `TourData.EconomyBuiltIn` in the same words.
* **Done when**: `hc_tour` PASS on the integrated build (written in parallel from this spec, re-measured after WP5
  merges); COMPILE OK; whycheck PASS (TourData's change is inside the economy's branch).

### 12.1 Merge order and integration

1. **WP0** (alone).
2. **WP3** first of the parallel packages: it changes the lives (the allowance), so every count downstream is measured
   on its numbers; the others rebase and re-measure.
3. **WP1**, then **WP2** (placement verified on the real terrain), then **WP4** (independent; whycheck again), then
   **WP5** (the cut and the views tuned on the real layers), then **WP6** (numbers re-measured).
4. **Integration** (the lead, on the integration branch): the full `hillcheck --expect all` at 16:9 and 9:16, the morph
   renders, the playback and phase runs, the `lives0` run, two runs for determinism, whycheck, the budgets, **the bowl's
   restore** (`restore-bowl.sh` in a scratch worktree: the recipe of 1.1, compile, render `landscape` and `people`,
   `hc_views.restore`), a review of every preset's render against the user's words (13), the expectations frozen in the
   checks' JSON.

**Dependencies between packages and when each "Done when" is measured.** Every cross-package dependency is a WP0
contract (11.2), so every package compiles and runs from WP0's merge. A package's own checks are measured on its branch;
a check whose expected value comes from another package (`from` in the checks' JSON) is INFO until that package is in
`--expect`. Each package's final "Done when" is measured after rebasing on every package merged before it: WP3 on WP0;
WP1 on WP3; WP2 on WP3 and WP1; WP4 on WP3, WP1 and WP2 (it reads none of them; whycheck again); WP5 on all but WP6; WP6 on all. No "Done when"
names a check whose source merges later.

Each merge: `git merge --no-ff`, COMPILE OK, `hillcheck --expect <merged so far>`, whycheck; a FAIL blocks the next
merge.

---

## 13. Acceptance checklist (verifiable from harness renders and logs)

**The user's words**

1. [ ] **The bowl is kept and disabled**: the five bowl layers compile in `Economy/Bowl/` under `[GraphScenes(EconomyScenes.Bowl)]`;
   the economy log has no Prepare line of any of them (`hc_views.bowl`).
2. [ ] **A landscape of hills for the hierarchy**: `landscape.base.png` shows five benches and 25 hill labels (24 at
   9:16); the Hills line says "strictly ordered" for 2025, 1972 and 1950 (`hc_terrain.log`, `reference`, `labels`).
3. [ ] **Golden crowns of owners over the sectors they control, showing who**: `crowns.base.png` has gold in the 5
   largest crowns' boxes; the Crowns line lists internet (Page & Brin, Zuckerberg) as the largest; every crown ≥ $1T has
   a seated controller line and the vapor of its controlled payouts; every controller line sits on its ledger seat; the
   crown labels name the holders (`hc_people.crowns`, `seats`, `log`).
4. [ ] **Pools of the ultra wealthy at cloud level between the hills**: clouds at y 4.50, not over any hill, crown or
   other cloud (`hc_people.geometry`), in ≥ 3 distinct gaps between hills (`hc_people.clouds`); their pixels above every
   crown box in `crowns.base.png`.
5. [ ] **The less wealthy on the valley floors, close to the hill that pays them**: rows on the treads with 0 spills,
   max |x − hill x| ≤ 0.8 u, ≤ 4 outside their frontage, the rows monotone in wealth (the Players line).
6. [ ] **Capital flowing to a sector is a river flowing up the hill**: the Capital line's "uphill violations 0", 23
   rivers; gold columns above the 3 largest caps in `capital.base.png` (`hc_terrain.gold`).
7. [ ] **Control is read from where a person sits, not from a label**: the strict stack floor and slope < crown band
   < clouds in world height in every land view, and on screen in the `crowns` elevation view (`hc_people.stack`); no
   rendered label or readout says "in control" (`hc_views.cut` and a label scan of every `labels.json`); lifelines and
   dots are gold only for the top 1% (WP3's style; the CutLayer line's gold-dot count = the top-1% lines, ≤ 3% of adult
   dots); no object but a crown or a cloud stands above the highest summit (`hc_terrain.overlay`).
8. [ ] **The desire / fear brain as a separate 3D view**: `mind.base.png` passes `poles`, `reason`, `gate`, `chemicals`;
   `mind-pop` passes `population`; `phase` passes.
9. [ ] **…and the program every player runs, as a game whose plays have consequences**: the Programs line sums equal
   the lives (0.000%); the listing line prints the 9 lines for the default player, its PLAY line naming what it feeds
   into next year; the Lives play feedback line PASS (cooperation moves next year's spending mix); the Score line PASS;
   the Playback line shows 2 steps and 2 tweens with 0 ms waited.
10. [ ] **Assets, debt, spending, income per player; generational wealth, inheritance and allowance**: the listing's
    MEMORY / WORLD / DECIDE lines carry them; on the land, the debt rings (people view) and the trading-assets wisps
    (capital view); the Lives allowance line ($379B ± 2% in 2025); the Families line (estates, inherited shares
    cloud > floor, crown > floor).
11. [ ] **Children living with or depending on parents shown in the hierarchy**: ≥ 95% of child dots below their parents;
    ≥ 90% of dependents with a living parent have a home dot in frame in the people view (`hc_people.families`);
    `families.base.png` shows ≥ 20 home threads (Frame lines `land:thread:<k>` in, and ≥ 20 people-blue pixels (hue
    205-225°, s ≥ 0.3) inside each of 20 of their boxes).
12. [ ] **Relationships between men and women on the SMV graph; children born where two lines meet on the mid-plane**:
    the SmvBonds line (3,090 births, 0 misses); `couples.base.png`'s knot ≥ 230 and the four family labels placed
    (`hc_smv`).

**The first redesign's asks still hold**

13. [ ] The cut expands into the land: `stairs` and `rise` renders differ as required; the CutLayer morph line mean ≤ 2 ms.
14. [ ] Spending pools: lakes in front of their hills; `rivers.base.png` passes `hues` (ice and rose, never blended).
15. [ ] Tit for tat with in-group / out-group affinity, and the notebook's games page: the Society line (checks 2/2);
    `society.base.png` shows 4-6 territories, their labels, ladders and pyramids (`hc_people.games`); the betrayal view's
    incident tie is drawn; the society panel is populated (`hc_views.panels`).
16. [ ] Players grouped by the 12 groups of 2026; topology, not geography (the Players line's group shares within 0.5 pt
    of `groups.json`'s targets, as today).

**Engineering**

17. [ ] COMPILE OK (Economy/UI included); `datacheck` no new warnings.
18. [ ] **The causality scene unchanged**: whycheck "14 presets, 0 differ: PASS"; the Why log's SmvLayer line = HEAD's;
    no Core, Axis, UI, Lens or Director file changed except `TourData.EconomyBuiltIn` (a `git diff --stat` check).
19. [ ] All 18 presets render at 1920×1080 and 1080×1920 with no exception and their must-be-in-frame lists hold
    (`hc_views.presets`).
20. [ ] Budgets of 11.6 (the Prepare and timing lines; the load ≤ 2.9 s).
21. [ ] Determinism: two runs give identical log lines and byte-identical `.base.png` (`--log2`).
22. [ ] The tour's 13 steps resolve (presets, anchors) and their numbers match the log (`hc_tour`).
23. [ ] `Docs/ECONOMY.md` describes the hills, the program, the views and keys, the limits and the bowl's restore recipe.
24. [ ] **Bowl restorable: PASS**: `restore-bowl.sh` at integration compiles, renders `landscape` and `people` with the
    bowl's five layers, and the renders match `lead-fix6`'s up to the expected differences (`hc_views.restore`).
25. [ ] Every package's "Done when" passed at its place in the merge order, with no check whose source merged later
    (12.1).

---

## Appendix A. Decisions on the conflicts between the parts

| # | Conflict | Parts | Decision (and why) |
| --- | --- | --- | --- |
| A1 | Where floor players stand | people: band curves around each hill's foot; terrain: frontages on the tread | **Rows on the tread in front of the hill**, the band = the row (feet → lip). Hills of a range nearly touch (gap 0.22) and many stand back from the tread: rings would fall between hills; rows keep "valley floor" literal and "close to that hill" by packing at the hill's x (2025: max shift 0.37 u with rows by wealth, A43). |
| A2 | Lakes at the hills' feet vs people at the feet | terrain: lake at the front foot (b ≤ 0.32, a quarter of the hill scale) | **Lakes on the valley river in front of their hill**, a tenth of the hill scale, b ≤ 0.17: four rows, a river and a lake do not fit 1.0 u; a river widening into lakes reads as pooling. |
| A3 | Tread depth | terrain: 1.00, risers 0.90, gov front 0.90 | **1.20, 0.70, 1.20** (land 15.15 deep; risers 56°, hills ≤ 55°): room for the rows and the river. |
| A4 | The terrain interface | people: `ILandTerrain`; terrain: `TerrainGeometry` + `TerrainField` | **`TerrainGeometry` + `ITerrainField`** with the people's `Contour(k, e)`, `Free`, `TreadAt`; land-local z everywhere (no d in contracts). |
| A5 | Cloud level | terrain: 4.50 fixed; people: 1.25 × tallest + 0.30 | **4.50 fixed** (above every summit and crown). |
| A6 | Crown heights | terrain: S + 0.35 … 1.0; people: S + 0.20 + 0.8 r | Revision 1: S + 0.35 + 0.8 r. **Superseded by A35** (the crown band). |
| A7 | Cloud positions | terrain: slots between hills; people: holdings centroid | Revision 1: holdings centroid. **Superseded by A37** (overweights, between the hills). |
| A8 | Pipeline order | terrain: placement before flows; people: rivers first | terrain → census → accounts → **lakes** → placement → flows: lakes and gorges (terrain) block rows; the router needs the players. |
| A9 | "Allowance" | people: imputed child share; mind: a real transfer to adult children at home | **Allowance = the real transfer** ($379B 2025); the imputation is **child cost**. |
| A10 | Who writes the record fields | both | **WP3** writes all (one change to the lives). |
| A11 | Pain / relief / satisfaction | the bowl's tie events; mind: life events | **Life events**; the tie events are renamed tie breaks / mends / holds. |
| A12 | Two `families` views | people (land) and SMV (road) | **`couples`** (key 9, the road) and **`families`** (key 0, the land). |
| A13 | Key 7 | the bowl's mirage view | **The 3D mind**. |
| A14 | Year transitions | mind: identical-topology morphs in every layer; terrain: cross-fades | **Terrain height morph + glide markers + 0.4 s cross-fades**; `ILandTweenable` dropped (far less code, the same reading). |
| A15 | Gold on dots and lines | people: crown dots gold, cloud dots blue; HEAD: gold "in control" | **Gold = the top 1%** on lines, cut dots and land dots; no in-control encoding anywhere (the user: not literal). |
| A16 | Towers | people assumed they might stay | **No towers**: company marks on the caps; jewel tethers end at the marks. |
| A17 | `SectionLayer` | terrain: the cut stays, the morph moves | One class: **retired** with the bowl; `CutLayer` (a copy of the card + the hills' morph). |
| A18 | `InhabitantsLayer`, `SocialLayer` edited in place | people | **Retired** with the bowl; `PeopleLayer`, `SeasonLayer` new (disjoint ownership, the bowl intact). |
| A19 | Splitting `MoneyRouting` | terrain: `MoneyRouting` calls `MoneyAccounts` | **`MoneyRouting` untouched**; `MoneyAccounts` new with the same rules, verified by equal totals. |
| A20 | The bowl renderable as `--scene economy-bowl` | terrain | **Not loadable** (no Core changes); kept compiling; a restore recipe. |
| A21 | Investment by industry | terrain: an estimate by depreciation | Adopted, labeled "estimate"; the BEA table is a follow-up. |
| A22 | The crowns' private split | people: proprietors' income 2024 data | **The model mix** (the lives' business income by industry), logged "(model mix)"; the data is a follow-up. |
| A23 | Insider stakes | recalled | Entered with `holderSource`; "~" in hovers when unverified. |
| A24 | Disc scale | the bowl's 0.035 | **0.030** (rows 0.20 apart hold discs up to 12M people). |
| A25 | Where the mind stands | mind: 6.5 u left of the land | Adopted; faint (0.3) in the overview, hidden in land views. |
| A26 | "In control" in the mind | mind: "agency 0.39 · 1% in control" | The ring says **"agency 0.39"**; the in-control share only in hovers and the inspector. |
| A27 | Hill labels under crowns | terrain: summit + 0.15 | Kept for landscape / y1972 (crowns 0.4, unlabeled there); crown labels at the crown top in capital, people, crowns. |
| A28 | Player counts | people ~123, mind 118 | Measured with both changes: 126 [proto]; with the crown seats (A36) **130 [proto, stand3], 122-136 passes**. |
| A29 | Season memory across years | mind: none | Adopted (5.4). |
| A30 | Wages to the 1% and slope players | terrain: ground descent | **Arcs** from the wage band to the player (light blue). |
| A31 | Capital river head | terrain: beside the lake on the tread | **At the hill's front foot** (the lake moved onto the river). |
| A32 | Who edits the view tables | every part assumed it would | **Partial classes per owner** (11.2). |
| A33 | The mind's z axis | mind: "z front" | Built in the mind part's coordinates, placed with z mirrored (`MindFrame.ToLand`), safe with `Cull Off`. |
| A34 | Keys during the tour | mind: Space, P, M | Ignored while the tour holds the keys (`GraphRoot.AllowPresetKeys` false). |

**Revision 2** (the review of revision 1):

| # | Issue | Options | Decision (and why) |
| --- | --- | --- | --- |
| A35 | Height meant stratum, not control, across the land (a tech-floor worker at 2.80 above the raw crowns at ~1.6) | a common crown band; crowns per hill height (rev. 1) with a per-hill check | **One crown band** (bottoms ≥ 3.55 above every summit, tops ≤ 4.20), each crown over its hill on a gold stem: floors and slopes < crowns < clouds everywhere in world height; the `crowns` view is an elevation whose eye sits between the people and the band, so the screen shows the same order (10 px / 19 px margins [proto]). Oblique views compare per hill (a land seen from above shows depth as height). |
| A36 | All 6 controller lines collapsed into one census player over software; 18 of 19 crowns empty | exempt crown cells from the size test; seat per line on its own industry; apportion seats by V_k | **The ledger seats each controller unit** by a Hamilton apportionment over V_k (keep last year's seat, then own industry, then own tier, then the rest), crown cells are never merged, so every crown ≥ $1T is seated (2025: internet 2, manufacturing, trade, insurance, software); the seat is the line's industry for the census, the accounts and the vapor. The model's own business industry differs for 4 of 6 lines; the hover says so. The **measured holders** are named seats (hollow), the model's lines dots (filled). |
| A37 | The 4 clouds collapsed into one clump (every cloud's holdings are the market mix + a 0.15 tilt) | DFA asset-kind split of the 1%; centroid of overweights | **Overweights** (holdings minus the market mix) give the place, then the nearest point between the hills (every ρ ≥ 0.8): 4 gaps over 12.4 u [proto]; the holdings still give the wisps (the "many industries" cue). The DFA split would need data the build does not have per line. |
| A38 | Vapor reached only seated crowns, so controlled and widely held hills looked alike | route only seated income; route the controlled share through every crown | **Every crown passes its hill's controlled payouts** (c_k = V_k / E_k, an estimate) on to their recipients, seated lines keeping their own: control shows as money passing through the controllers' hands, no account changes. |
| A39 | `RootsModel` gives no geometry-free selection | re-implement in Land/Hills; add an accessor | **Add `RootsModel.Select`** (additive; `Build`, `Line`, `RootsOf` untouched): one selection, the bowl unchanged by construction. |
| A40 | The program's PLAY stage fed nothing; the game was a replay | feed cooperation into next year; a what-if lever rerunning the lives | **Feed the lives' own cooperation into next year's drives** (κ 0.15): cheap, deterministic, inside the existing yearly loop, totals unchanged; the what-if lever (a second lives and snapshot cache) is the follow-up. The players' season stays one year's game (5.4). |
| A41 | The retired `SocialLayer` held runtime duties the UI reads | edit the bowl's layer; move the duties | **`SeasonLayer` takes every duty** (5.0), the static moves to the contract `LandSeason`; the UI reads `LandSeason.Shown ?? SocialLayer.Shown` and every UI edit keeps its bowl branch, so the restore recipe stays four edits. |
| A42 | Dependents sit among the poor with no visible link outside the families view | home dots; faint threads | **Both**: a hollow home dot beside the parent's dot wherever the parent's dots show, and threads at 0.12 in the people and crowns views. |
| A43 | Affluent non-owners share the floor with the poor; rows by group invert wealth (45 of 310 pairs) | rows by wealth; a foot terrace for the top 10% | **Rows by the player's mean wealth** against the year's median: the place within the floor is wealth, as the grammar says; no terrace (it would read as ownership). |
| A44 | The AI tower (1.64 u) was the tallest object | cap its height; dollars in width | **Dollars in the footprint** at the hills' area scale, height = the tech range's highest own rise. |
| A45 | Debt per player had no mark on the land | a ring; a hanging bar | **A dashed gold debt ring under the disc** (the memory shelf's hanging-debt look). |
| A46 | "Trading assets" disappeared into saving | a separate flow; a split of the saving wisp | **The saving wisp splits** into trading assets (glittering by fantasy) and deposits and pensions; a MATING chip. |
| A47 | The games page had no visual | a separate view; marks in the society view | **A ladder and a pyramid per coalition** in the society and betrayal views. |
| A48 | Packages could not compile or pass in parallel | more contracts; a different merge order | **WP0 holds every cross-package member** (record fields, SMV hooks, selection, phases, playback tick, frame items, the season static, crown values); checks tagged `from` are INFO until their source merges; each "Done when" is measured at its place in the merge order. |
| A49 | Must-be-in-frame items were not labels and had no source | label anchors only; a frame-item registry | **`LandFrameItems`** with points, logged as screen boxes by `WHY_DUMP_LAND`: the boxes every pixel check uses. |
| A50 | Small hills render blocky on the 0.10 grid | per-triangle colors; fine patches | **Fine 0.03 patches** for hills with a < 0.6 (≤ 10K vertices), submesh by the triangle's centroid. |
| A51 | The tour's step count | merge two steps; 13 | **13 steps**, as listed. |
| A52 | The bowl was only proven to compile | a harness-only switch; a scripted restore | **A scripted restore at integration** (`restore-bowl.sh`): exercises the real recipe without adding a switch to the code. |

## Appendix B. Prototypes and reference numbers

| Path | What |
| --- | --- |
| `$SP/redesign2/synth/terrain2.py` → `terrain2_out.txt`, `hills-<year>.json`, `top-<year>.png` | the merged terrain (A3): treads, hills, summits, slopes, gorges; the reference of `hc_terrain.reference` |
| `$SP/redesign2/synth/stand.py` → `stand_2025_out.txt`, `stand_1972_out.txt`, `stand-<year>.json` | the merged placement on HEAD + the allowance: the census approximation, the ledger, floor rows, ledges, lakes on the rivers, crowns, clouds |
| `$SP/redesign2/synth/stand3.py` → `stand3_2025_out.txt`, `crowns3-2025.json` | revision 2: the controller lines and their seats, the controlled shares, the crown band, clouds by overweights between the hills, rows by wealth, dependents vs their parents' players; the stack's screen margins were computed with the same data and a pinhole camera (16:9 eye 3.42 pitch 0 distance 22: 10 px / 19 px; 9:16 distance 30: 14 px / 28 px) |
| `$SP/redesign2/synth/sketch_top.py` → `sketch-top-2025.png` | a top view of the merged 2025 layout |
| `$SP/redesign2/terrain/` | the terrain part's prototypes (`terrain.py`, `flows.py`, `render3d.py`, `sketch.py`) |
| `$SP/redesign2/people/` | the people part's prototypes (`proto_v3.py`, `crowns.py`) |
| `$SP/redesign2/mind/` | the mind part's instrumented copy, the allowance diff, the dumps (`py_a0.5.csv`), `proto.py`, `listing.py`, the mockups |
| `$SP/redesign2/smv/` | the SMV part's patch (`proto.patch`), renders (`c5/`, `c5p/`, `c5off/`) and logs |
| `$SP/redesign/build/lead-fix6/land.log` | HEAD's log (the bowl's money totals, pools, season) |

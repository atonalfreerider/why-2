# Part: the hill terrain and its flows

One part of the economy scene's second redesign ("keep but disable the circular torus economy layout and replace with
landscape game"). This part designs the **land itself** (a staircase of benches carrying one hill per industry, valley
floors, lakes, the cloud level), **every flow on it** (wages, spending, capital, profits, saving, credit, taxes,
transfers, imports, the input-output roots), **the unfold** from the cut on the road, and **the views**. The other parts
(people and wealth; the mind program and its 3D view; couples and births) are named in section 8, with what this part needs
from them and what it gives them.

* `$SP` = `/tmp/claude-0/-home-user-why-2/dc9bf553-91de-54d2-9f1f-9831e1f24cfa/scratchpad`; prototypes in
  `$SP/redesign2/terrain/` (section 11). Numbers marked **[proto]** come from them, run on the repository's data;
  **[data]** are read from `Assets/Resources/Data/economy/*.json`; **[bowl]** are the current build's log
  (`$SP/redesign/build/lead-fix6/land.log`), which the reused money model keeps.
* Paths are under `Assets/Scripts/Economy/` unless they start with `Assets/` or `Docs/`. The repository was not edited.
* "d" is depth into the land from its front edge (the edge nearest the road), in units; land-local
  `z = d − 7.425` (section 1.1).

---

## 0. The design on one page

**The land is the cut laid back and grown.** The cut's stack of 25 bands lies back into a **staircase**: five benches
rising away from the road, the government plain in front (y 0), then raw (0.70), make (1.40), services (2.10) and tech
(2.80) at the back. On each bench stands its **range**: one **hill per industry**, its footprint **area = the value it adds**
(46 u² per GDP: 1 u² = $0.67T in 2025), its **summit set by its tier** (bench height + its own rise of 0.12-0.80, so every
tech summit stands above every services summit, and so on down: strict). The government is the **plain and its two mesas**
(federal, state and local) in front: the ground the ranges stand on, where taxes drain and transfers spring.

**A hill is the wall's band stood up.** Concentric zones, area-true: the **foot** is wages (light blue), the middle ring
upkeep (steel: production taxes and depreciation), the **cap** is what owners keep (**gold**). Real estate is 57% gold;
health 9%; finance 3%. Contour lines every 0.1 u carry the relief; shading is baked into vertex colors.

**Placement carries the hierarchy and the dependencies.** Suppliers stand in front of and below their buyers (the
notebook's strata are the benches). Within a range, each hill sits over the hills it trades with most (barycentric
ordering on the 2024 input-output table, fixed for all years) and consumer-facing industries stand nearer the valley
(health, hospitality in front; professional services at the back). The order never reshuffles; benches **breathe** with
the year: a bench is as deep as its tier's share of value added (make is the deepest bench in 1950, services in 2025).

**Valley floors** are each bench's tread in front of its range, 1.0 u deep, where the less wealthy live, each in the
**frontage** of the hill that pays them. A **valley road** runs along every tread; three **gorges** per riser climb
through the passes between hills. Each hill has a **lake** at its foot, as large as the household spending it is paid
directly. **Cloud level** is y 4.50, above every summit; **crowns** hover 0.35-1.0 over summits.

**Every flow is water, and only capital climbs a hill.**

| Flow | Water | Where | Look |
| --- | --- | --- | --- |
| Wages $15.73T | **runoff**: from a hill's foot down to its workers | hill foot → frontage | light blue streams |
| Spending $21.63T | **rivers** along the valley roads and up / down the gorges, pooling into the **lakes** at the hills' feet | settlements → roads → lakes | ice lane = fear, rose lane = desire, never blended; white glitter = fantasy |
| Imports $2.27T | rivers off the land's side edges | roads → edge falls | rose / ice fading out |
| Capital: investment $5.67T (estimate) | **a gold river up the hill**, from the lake's side to the gold cap | foot → cap | gold, width ×4 |
| Profits: business income $2.11T, capital income $5.33T | **vapor** rising from the gold caps to the crowns over the hill and to the cloud pools between hills; **rain** onto the valley's savers (pensions) | cap → crown / cloud / valley | gold wisps |
| Saving $2.16T, credit $0.93T | saving **evaporates** from the valleys to the cloud level; the clouds lend through the banking hill (credit) and **rain** the rest ($1.23T) onto the capital rivers' heads | valley → clouds → hills | gold, dashed for credit |
| Taxes $5.25T personal + $2.15T on production | **seep** into the ground; steel conduits under the valleys run down to the mesas | settlement / upkeep band → mesas | steel, underground |
| Transfers $4.95T | **springs** from the ground water at the recipients | mesas → fountains | steel |
| Purchases between industries $15.85T | **roots** under the benches, rising into each buyer from below | supplier → buyer | supplier's tier color, ×4 |

**Views tell one story each** (section 7): landscape (the hills of value), capital (rivers up, vapor to crowns and
clouds), rivers (spending), roots, state (taxes and transfers), plus the other parts' people, owners, mind and society
views. Everything else is dimmed to ≤ 0.15 and dense flows are aggregated.

Prototype renders (painter's preview with the Unity FOV, not the shaders): `$SP/redesign2/terrain/sketch-16x9.png`,
`sketch-9x16.png` (2025 with stand-in settlements, crowns and cloud pools), `hills-1950.png`, `hills-1972.png`,
`hills-2025.png`, top views `top-<year>.png`.

---

## 1. The land

### 1.1 Frame and units

* **Frame.** `LandFrame` is reused unchanged (origin C on the plaza, +z along the road away from the past, +y up). The
  land is centred on C: x ∈ [−7, 7], z ∈ [−7.425, +7.425] (depth `HillStyle.Depth` = 14.85, fixed for all years). Its
  front edge is `PlazaGap` − 7.425 = 2.575 u past the road's end. Every land mesh is built land-local and drawn with
  `GraphMaterials.Raw` on GameObjects placed with `LandFrame.Place`, as the bowl's were.
* **Units** (`HillStyle`, the year's GDP normalizes the land; the road and wall keep the absolute growth):

| Quantity | Channel | Constant | 2025 reading |
| --- | --- | --- | --- |
| Value added | hill footprint area | `AreaGdp` = 46 u² per GDP(Y) | 1 u² = $0.67T |
| Household spending paid directly | lake area | `LakeAreaGdp` = 11.5 u² per GDP (a quarter of the hill scale: a lake a quarter of its hill's area means households pay it its whole value added) | health lake 1.40 u² |
| Money flows (wages, spending, taxes, transfers, vapor, saving, credit) | world width | `WidthGdp` = 1.0 u per GDP (as today) | $1T = 0.0325 u |
| Capital rivers, roots | world width | `CapitalWidthGdp` = `RootWidthGdp` = 4.0 ("×4" in the legend) | real estate $1.58T: 0.21 u |
| Tier | bench height | `BenchY` = 0, 0.70, 1.40, 2.10, 2.80 | |
| Size within a tier | the hill's own rise | 0.12-0.80 (1.2) | |
| Motive / whose money | hue | ice fear, rose desire, white glitter fantasy, gold capital, light blue wages, steel state, tier hues for roots | |

Every dollar-coded line keeps the bowl's floor: 0.8 px minimum, alpha × min(1, $/20B) (≥ 0.3).

### 1.2 The staircase (benches)

Along d (front to back): the **gov plain** (front margin `Front` 0.90, then the mesa zone 1.00 deep), then for each tier
t = 1..4: a **riser** (`Riser` 0.90, smoothstep), the **valley tread** (`Valley` 1.00), the **hill zone** (depth Z_t), then
the back margin 0.35.

```
Z_t      = ZMin + (DZones − 4 ZMin) · VA_t(Y) / Σ_{tiers 1..4} VA(Y)      ZMin 0.70, DZones 5.00   (Σ Z_t = 5.00 every year)
d_riser1 = Front + 1.00 = 1.90;  d_valley_t = d_riser_t + Riser;  d_hills_t = d_valley_t + Valley;  d_riser_{t+1} = d_hills_t + Z_t
G(d)     = Σ_{t=1..4} (BenchY_t − BenchY_{t−1}) · smoothstep((d − d_riser_t) / Riser)           ground height
```

The bench depth "breathes" with the year (a tier's zone is as deep as its share of private value added, above a 0.70
floor so raw and tech stay legible); the land's total depth is constant, so a year change slides benches and never moves
the land. 2025 **[proto]**: risers at d 1.90 / 4.61 / 7.90 / 11.69, hill zones raw 0.81, make 1.39, services 1.90, tech
0.91 deep. 1950: make 1.88, services 1.27. Risers are 49° at their steepest.

The land stands on a **bedrock slab** (steel, y −0.30 to the ground): its front face and two side faces are drawn (the
front face is what the cut becomes, 6). The gov plain, the treads and the risers are **ground** (alpha 0.05-0.10, tier
tint at low saturation: raw red-brown, make bronze, services steel-blue, tech violet-grey; gov steel).

### 1.3 The order (fixed for all years)

`HillLayout.Arrange(data)` runs once at load on 2024 value added and the BEA 2024 Use table (`circuit.json io.flows`,
between-industry only; W = F + Fᵀ):

```
rows      = the five tiers; within a row start with value added descending, laid centre-out (largest in the middle,
            then alternately right, left)
x         = PackRow(row order, desired x, half widths a_i) on [−7 + Edge, 7 − Edge], Edge 0.30, gap Gap 0.22
            (minimum-displacement 1D packing: pool-adjacent-violators on desired − offsets, then clamp into the row;
             the linear twin of LandMath.PackRow)
12 sweeps: even sweeps rows raw, make, services, tech; odd sweeps services, make, raw
            key(i) = Σ_{j: |tier j − tier i| = 1} W_ij x_j / Σ W_ij      (ties: industry index)
            sort the row by (key, index); x = PackRow(row, key)
keep      Order[t] (left to right) and DesiredX[i] = the last key
```

Result **[proto]**, left to right: gov federal, state & local; raw utilities, oil & gas, mining & metals, agriculture;
make construction, trade, transport, manufacturing; services real estate, entertainment, banking, legal, professional,
finance, insurance, other services, hospitality, health, education; tech media & telecom, software, internet, hardware.
Construction stands under real estate, trade under professional services, the tech range behind professional services
and real estate (their largest suppliers: professional → software $143B, → internet $142B **[data]**).

### 1.4 Hills: footprint, place, height

Per year Y (`HillLayout.Build(data, Y)`):

```
A_i     = AreaGdp · VA_i(Y) / GDP(Y)                         (no hill when VA_i < $1B: software 1950; its frontage stays)
b_i     = min(Z_t / 2, sqrt(A_i / π));  a_i = A_i / (π b_i)  ellipse semi-axes: round when the zone allows, wider when not
                                                             (gov mesas: b ≤ 0.50)
x_i     = PackRow(Order[t], DesiredX, a) per row           (target x)
d_i     = d_hills_t + b_i + max(0, Z_t − 2 b_i) · 0.5 · (cf_max,t − cf_i) / (cf_max,t − cf_min,t)
          cf_i = min(1, pce_i / output_i) (2024 final demand and Use-table output): consumer-facing hills nearer the valley
relax   300 iterations, pairs (i < j) of a row in index order: ellipse extents along the centre line
          r_i = 1 / sqrt((u_x / a_i)² + (u_d / b_i)²); overlap = r_i + r_j + Gap − |c_j − c_i|;
          if overlap > 0 move both apart along the line, i by overlap · A_j / (A_i + A_j), j by the rest;
        then pull every hill 2% toward its target and clamp x into [−7 + Edge + a_i, 7 − Edge − a_i],
          d into [d_hills_t + b_i, d_hills_t + Z_t + 0.35 − b_i]   (a hill may lean onto the next riser)
own     h_i = clamp(0.95 · b_i, 0.12, 0.80); gov mesas 0.35          (max slope 1.54 h / b ≤ 56°)
summit  S_i = BenchY_t + h_i                                         (OwnMax − OwnMin = 0.68 < bench step 0.70: strict)
```

**Profile.** Hill: `own_i(p) = h_i (1 − ρ²)²` for ρ < 1 (biweight), ρ² = ((x − x_i)/a_i)² + ((d − d_i)/b_i)². Mesa:
`h (1 − smoothstep((ρ − 0.5) / 0.5))` (flat top, 0.5 of the radius falling to the plain). **Height field**
`H(x, d) = G(d) + Σ_i own_i(x, d)` (overlapping feet add; the 2D relaxation keeps them ≤ Gap apart).

**Zones** (area-true in the hill's own ρ: the area inside ρ is ρ² of the footprint), from the wall's split
(`WallGeometry.Split`, reused): owners' cap ρ < √o_i, upkeep √o_i ≤ ρ < √(o_i + u_i), wages the rest out to the foot.
Each raster cell belongs to the hill with the largest own height there (if ≥ 0.015), else it is ground.

2025 **[proto]** (x, d, footprint 2a × 2b, summit):

| Range | Hills |
| --- | --- |
| gov (plain) | federal −1.76, 1.40, 2.11 × 1.00, 0.35 · state & local +1.76, 1.40, 4.45 × 1.00, 0.35 |
| raw (bench 0.70) | utilities −0.08, 1.10 × 0.82, 1.08 · oil & gas +1.33, 1.08 × 0.82, 1.08 · mining & metals +2.46, 0.56 × 0.56, 0.97 · agriculture +3.33, 0.72 × 0.72, 1.04 |
| make (1.40) | construction −5.78, 1.84 × 1.39, 2.06 · trade −2.01, 5.25 × 1.39, 2.06 · transport +1.54, 1.40 × 1.39, 2.06 · manufacturing +4.06, 3.22 × 1.39, 2.06 |
| services (2.10) | real estate −4.57 d 11.10, 4.26 × 1.90, 2.90 · professional +0.21, 2.96 × 1.90, 2.90 · health +4.59, 2.38 × 1.90, 2.90 · banking −1.97, 1.44, 2.79 · hospitality +6.02, 1.36, 2.75 · insurance +2.47, 1.27, 2.70 · other services +3.02, 1.19, 2.66 · finance +1.85, 0.98, 2.57 · legal −2.03, 0.90, 2.53 · education +6.29, 0.82, 2.49 · entertainment −6.29, 0.82, 2.49 |
| tech (2.80) | media & telecom −3.56, 1.46 × 0.91, 3.23 · software −1.60, 2.02 × 0.91, 3.23 · internet +0.26, 1.24 × 0.91, 3.23 · hardware +1.48, 0.78 × 0.78, 3.17 |

Summits are strictly ordered by tier in 1950, 1972 and 2025; the steepest hill slope is 56° (the mesas' edges 62°)
**[proto]**. 1950: manufacturing is a 6.1-wide massif and agriculture the largest raw hill; services are small hills on a
shallow bench; the tech bench holds media & telecom and hardware only. The picture of the century is the land's shape.

**Input-output direction** (all between-industry dollars of the 2024 table, $15.85T): 21.9% come from a range in front
(supplier lower), 41.0% stay within a range, 37.2% come from a range behind (mostly services selling down to make and
raw). Of the dollars between adjacent ranges, 54% connect hills within 2 u of each other in x **[proto]**. Printed, not
hidden (10.2).

### 1.5 Valleys, frontages, lakes, gorges (the ground plan)

* **Valley tread** of bench t: d ∈ [d_valley_t, d_hills_t] (1.0 deep); for the gov plain d ∈ [0, 0.90].
* **Valley road**: the centre line of each tread at d_valley_t + 0.42 (gov plain: d 0.45), x ∈ [−6.7, 6.7], y = tread
  + 0.02. It is where the spending rivers run (4.2); people keep it clear (a corridor of ±0.07).
* **Frontages**: each tread is split in x among its range's hills at the midpoints between consecutive hill centres
  (the outermost frontages run to the land's edge). Hill i's frontage is where its workers settle (8). Hills pushed back
  by the relaxation keep their frontage at their x: their people live in front of the range, at most 1.6 u from the
  hill's foot **[proto]**.
* **Lakes** (one per hill with direct household spending ≥ $5B): an ellipse on the tread at the hill's front foot,
  `area = LakeAreaGdp · inflow / GDP`, b_L = min(0.32, √(area/π)), a_L = area / (π b_L), centre (x_i, d_i − b_i − 0.6 b_L)
  clipped to the tread; lakes of one tread are packed in x (PackRow, gap 0.10) so they never overlap. Surface y = tread
  + 0.03. 2025 widths: health 2.8, real estate 2.4, trade 2.3, manufacturing 1.5 **[proto]**.
* **Gorges**: three per riser t (from bench t−1 up to t), where the range in front is lowest. `crest(x)` = the highest
  own height over range t−1's hill zone at x (0.05 bins); take local minima sorted by (crest, x), keep up to 3 at least
  2.0 apart. Each gorge is a least-cost path on the 0.10 raster from road t−1 at x_g to road t at x_g, inside the band
  x_g ± 1.5: step cost = length · (1 + 25 · own height + 4 · |ground slope|), 8-neighbour Dijkstra, ties by cell index
  (deterministic). 2025 **[proto]**: raw → make at x −6.55, −4.50, −2.45; make → services at −4.85, +0.65, +5.70; services →
  tech at −1.25, +1.45, +3.55 (the services range's three lowest passes); gov → raw at −6.55, −4.50, −0.70.

### 1.6 The cloud level and the anchors above the land

* **Cloud level** `CloudY` = BenchY_tech + OwnMax + 0.90 = **4.50**: a fixed plane above every summit (3.23 max). Drawn only
  as a faint dashed horizon line along the land's back edge (alpha 0.12) and as the plane the cloud pools sit on.
* **Crown band** over hill i: y ∈ [S_i + 0.35, S_i + 1.00] above the summit point (x_i, d_i).
* **Company anchors** (`capture` companies with Y ≥ 2024): points on hill i's gold cap at ρ = 0.5 √o_i, spread over the
  front half of the cap at angles −60° … +60° from the viewer's side, by market value (largest centred).
* **Cloud slots** ("between the hills"): for every pair of hills (i, j) of the same or adjacent ranges whose footprints
  are ≤ 1.5 apart, the midpoint of the gap between their ellipses at y = CloudY; slots closer than 0.8 merge (their hill
  lists join). 2025: 16 slots, each between 2 and 5 hills (1972: 17, 1950: 16) **[proto: `terrain.cloud_slots`]**. The
  people part chooses which slots hold pools (8).

---

## 2. Rendering the land (`TerrainLayer`)

Only `Why/Surface` and `Why/Line` (unlit, translucent, ZWrite off, bloom); all shading baked into vertex colors.

| Element | Geometry | Look |
| --- | --- | --- |
| **Terrain surface** | one heightfield mesh on a 0.10 grid (141 × 149 = 21K vertices, 41.6K triangles), rows emitted back to front (far d first) so the translucent triangles composite correctly for the forward-looking views | vertex color = zone color × shade; shade = 0.55 + 0.45 · max(0, n · l), l = normalize(−0.45, 0.75, −0.48) (light from the upper left front); alpha: ground 0.05, risers 0.09, wages 0.18, upkeep 0.14, owners' cap 0.42 (intensity 1.5) |
| **Contours** | marching squares on the 0.05 raster of H, only where a hill owns the cell: every 0.10 u; every 0.50 u "major" | `Why/Line` 0.8 px, alpha 0.22 (major 0.40, 1.1 px), the zone's color whitened 50%; the relief reads from lines, not light |
| **Zone rims** | per hill, the ellipses ρ = √o and ρ = √(o + u), lifted onto H | gold 1.4 px intensity 1.8 (cap rim), steel 1 px (upkeep rim) |
| **Bench edges** | the tread's front edge and the hill zone's front line, per bench | tier hue 1 px, alpha 0.35 |
| **Bedrock** | front and side faces from y −0.30 to the ground profile | steel, alpha 0.06, edge lines 1 px |
| **Lakes** | the ellipse at the tread + 0.03 (32 segments); overflow: when inflow > VA, a second, brighter inner veil and the label says so | veil (0.80, 0.88, 1.00) alpha 0.20; shoreline two-tone, never blended: rose over (1 − fear) of the arc from the inlet, ice over the rest, 2 px intensity 1.6 |
| **Roots** | 4.6 | |
| **Labels** | 7.3 | |

Raw red / life green stay as tints of the raw hills' wage and upkeep zones (oil & gas, mining, utilities matter red;
agriculture life green); only owners' caps, capital rivers, vapor, crowns and clouds are gold (the lead's "gold means
capital" rule).

---

## 3. What the people stand on (the vertical code this terrain offers)

Valley floor (tread, y = BenchY_t) → hill slopes (wage foot, upkeep ring) → gold caps (y = S_i) → crowns (S_i + 0.35..1.0)
→ cloud level (4.50). The terrain guarantees: every summit is below every crown band of its own hill and below the cloud
level; every tread is below its own range's caps. Where each player sits is the people part's choice (8); the terrain
offers the floors, frontages, slopes (ρ bands), crown bands, company anchors and cloud slots.

---

## 4. The flows (`HillFlows`, drawn by `HillFlowsLayer`)

The money model is the bowl's, split so it has no geometry (`MoneyAccounts`, 9.2): every player's wages by industry and
named employer, business income by industry, capital income by industry (the `CapitalSources` mix of its members'
wealth groups), transfers (Social Security, Medicare share), taxes, spending by the six categories with fear and fantasy
dollars, saving, borrowing; the RAS category → first-recipient matrix (`SellerMatrix`); pools (direct household
spending by industry), payouts by sector, payouts abroad, credit, net saving, imports. Totals are the bowl's (2025
**[bowl]**: wages $15.73T, business $2.11T, capital $5.33T, transfers $4.95T, taxes $5.25T (federal 87%), spending
$21.63T, abroad $2.27T, saving $2.16T, borrowing $0.93T, payouts abroad $435B). The flows below give them a place.

Every flow path is a `FlowPath` (the contract type is reused, with new `FlowKind`s, 8.1), points land-local, dollars
attached. Paths are smoothed (Catmull-Rom through the nodes, 2 Chaikin passes, `LandMath`) and sampled every ≤ 0.08 u.

### 4.1 Wages: runoff from the hill's foot (light blue)

* **Data.** Player p's wages from industry i (`WagesBy[i]`), split in expectation among named employers by US employees
  (the bowl's rule, Y ≥ 2024).
* **Geometry.** Source = the point on hill i's **wage band** (ρ = 0.5 (√(o + u) + 1)) facing the destination: the azimuth
  of the destination seen from the hill's centre, limited to the front half (±80° around the −d direction). The stream
  runs **downhill** by steepest descent on H (step 0.04 along −∇H, until own height < 0.015: the foot), then level across
  the tread to the player's disc. If the player lives on another tread or another hill's frontage, the level part follows
  the valley network (4.2's router) from the foot's road point. Named employers: the stream starts at the company anchor
  instead (a gold dot at the source).
* **Width** dollars × `WidthGdp`; pulses toward the player.
* **Aggregation.** In the people view (the people part's) and the work view: one stream per (hill, player) ≥ $5B,
  smaller amounts merge into the player's largest wage stream (nothing dropped). Every other view: one stream per (hill,
  destination frontage), its width the sum: about 40 streams in 2025 instead of about 180 (estimate).
* **2025** **[proto, stand-in]**: state & local $2.32T (0.075 u wide), professional $1.80T, health $1.63T, trade
  $1.56T, construction $1.01T, manufacturing $0.98T. Runoff reads at once as "the hill pays its valley".

### 4.2 Spending: rivers to the lakes (ice / rose, glitter)

**Rule.** Spending runs only on the ground: along the valley roads and through the gorges; it never climbs a hill: it
ends in the lake at a hill's foot, and the hill drinks it. (Only capital climbs a hill, 4.3.)

**Router** (`ValleyNetwork`, deterministic): the graph is the five roads (sampled every 0.05 in x) plus the gorges.
Each player has an **entry point**: floor players their disc (a rivulet from the disc's road-side edge straight to the
road, 0.25 long at most); players above the floor (slopes, crowns, clouds: the people part's places) the road point at
their **ground x** on their anchor's tread, reached by a thin dashed vertical "descent" from the player (the rich do spend
on the ground). For each (player p, sink hill k) with dollars f_pk = Σ_c s_pc C[c][k] (1 − import_c):

```
same tread:      road from x_p to x_k
other tread:     DP over the gorge choices of each riser between the two treads (3 per riser):
                 cost = Σ |Δx| along the roads + gorge path lengths; ties: smaller x
then             lake channel from road x_k to the lake (straight +d, ≤ 0.4)
```

**Accumulation.** Each road bin keeps two directed flows (+x, −x), each split fear / desire / fantasy; each gorge keeps
up and down. Dollars from all players and sinks merge in a bin: rivers **pool together** where people's spending
converges. Imports leave by the nearest side edge of the player's tread (x = ±7: "the border") as a fall over the edge
fading to alpha 0 over 1.2 u.

**Drawing.** For each road, two banks: +x flows on the far half (d + 0.06), −x flows on the near half (d − 0.06); each
bank is two adjacent flat ribbons, **ice** (fear dollars) toward the road's centre and **rose** (desire dollars) outside,
0.004 apart, alpha-blended (not additive) alpha 0.75, intensity ≤ 1.0 (the bowl's lesson: additive rose beside ice
blooms white). Each ribbon has a 0.8 px centre line with flow pulses (0.8 phase per unit) in its direction. Gorges the
same, up flows on the left bank, down on the right. **Glitter** = fantasy dollars: one 4-point cross (5 px, intensity
2.6, white-rose) per $5B of fantasy dollars on a segment, 0.04-0.24 above it, golden-ratio placement (as the bowl).
**Categories** are separate submeshes of the same ribbons (6 per bank), so the rivers view can light one category
(the legend's chips BASE: necessities, collective; SELFISH: jeopardy, escapism; MATING: status, growth).

**2025** **[proto, stand-in settlements, the bowl's RAS matrix]**: $19.32T reach the lakes, $2.31T go abroad; 40% of the
dollars stay on their tread, 35% climb a riser (people paying a higher stratum), 25% go down one; the busiest road bin
carries $4.9T one way (0.16 u wide), $8.2T both ways (0.27 u), on the services tread between professional and finance;
the busiest gorge (make → services at x +0.65) carries $3.39T down and $2.65T up. Lakes **[bowl's pools, current build]**:
health $3.75T (158% of its value added: an overflowing lake), trade $3.21T (84%), real estate $3.18T (75%),
manufacturing $2.10T (90%), hospitality $1.37T (140%); nearly dry: professional 7%, software 14%, internet 27%.

Why spending climbs risers but not hills: a riser is the step between strata (money moving from people to a higher
stratum's sellers); a hill is an industry, and climbing it is capital's privilege. The legend says: "Spending runs along
the valleys into the lakes at the hills' feet; only capital climbs a hill."

### 4.3 Capital: a gold river up the hill

* **Data.** Gross private fixed investment by industry, I_i(Y). The repository has investment by **commodity**
  (`finalDemand.investment`: what investment buys, $5.40T in 2024), not by the industry that invests. Estimate (marked
  "estimate" in every label): `I_tot(Y) = Σ finalDemand.investment × GDP(Y) / GDP(2024)`;
  `I_i(Y) = I_tot(Y) · CFC_i(Y) / Σ_private CFC(Y)`, `CFC_i = depShare_i · VA_i(Y)` (each industry's depreciation:
  investment by industry tracks the capital it wears out). 2025: $5.67T; real estate $1.58T, manufacturing $517B, trade
  $455B, professional $351B, media & telecom $282B, health $282B, transport $261B, oil & gas $234B **[proto]**. The
  government hills get none (public investment is in their upkeep band). **Data task** (optional, 9.4): BEA Fixed Assets
  Table 3.7ESI (investment in private fixed assets by industry, 2024) into `industries.json` `investment2024`; then
  `I_i(Y) = I_tot(Y) · inv2024_i / Σ inv2024` and the label drops "estimate".
* **Funding.** External = the clouds' net lending to the land, X(Y) = saving − credit (2025: $2.16T − $0.93T = $1.23T,
  the bowl's net saving), shared by I_i / I_tot: real estate $343B, manufacturing $112B. Internal = I_i − external_i (the
  hill's own depreciation allowances and retained earnings: real estate $1.24T).
* **Geometry.** The river's **head** is on the tread beside the lake, at the hill's front foot, offset +0.25 a_i in x from
  the lake's centre (the wage runoff uses the −x side, so they never cross). It climbs by **steepest ascent** on own_i
  (step 0.04 along +∇own_i, from ρ = 1.02) up the hill's face and ends at the cap rim ρ = √o_i: **capital becomes
  ownership**. The internal part joins at the upkeep ring as a short steel-gold spring from the hill's own upkeep band
  (ρ = √(o + u) + 0.05, 0.3 long): its depreciation and retained earnings re-invested. The external part is the river
  below that junction; a **rain column** above the head (3 dashed vertical gold lines, 0.05 apart, from CloudY to the
  head, alpha 0.25) brings it down from the cloud level.
* **Width** dollars × `CapitalWidthGdp` (×4: real estate 0.21 u at the cap, manufacturing 0.067 u); external part
  0.046 u for real estate. Gold, alpha 0.7, intensity 1.6, pulses **uphill** (0.6 phase per unit).
* **Aggregation.** One river per hill ≥ $20B (23 of 23 private hills in 2025); none is merged.

### 4.4 Profits: vapor to the crowns and the clouds; rain onto the savers

* **Data.** Business income of player p from hill i (`BusinessBy[i]`, $2.11T); capital income of p from hill i
  (p's capital income × `CapitalSources.Mix[wealth group][i]` summed over its members, $5.33T, real estate $1.69T,
  banking $1.15T, trade $0.51T, manufacturing $0.38T, professional $0.25T **[bowl]**); payouts abroad by sector ($435B).
* **Geometry.** All vapor leaves the hill's **gold cap** (from points on ρ = 0.3 √o_i, spread so wisps do not stack):
  * to a player above this hill (a crown, or a business owner on the hill's slope): a straight wisp up to it;
  * to a player at cloud level (a cloud pool): a rising quadratic Bézier from the cap, control point (cap x, CloudY −
    0.3, cap d), to the pool: a fan of wisps from many summits converging on one pool **is** the picture of owning stakes in
    many industries;
  * to a player on the floor (pensions, the middle's dividends, rent and interest): the vapor rises to the cloud level
    above the hill and **rains** down onto the player (2 dashed vertical lines from CloudY to the disc, alpha 0.25);
  * abroad: wisps drift off the land's back edge at the cloud level and fade over 1.5 u.
* **Width** dollars × `WidthGdp`; gold, alpha 0.45, intensity 1.4; small sparkles rise along the wisps (one per $20B,
  white-gold, the vapor's texture).
* **Aggregation.** Per (hill, receiving player) ≥ $10B, smaller merge into the hill's largest link of the same kind;
  rain per receiving player ≥ $5B (merged into one rain column per player whatever the hills). Outside the capital and
  owners views: one wisp per (hill, kind: crown / cloud / rain / abroad), its width the sum.

### 4.5 Saving, credit (gold)

* **Saving** ($2.16T): every player with positive saving sends a thin gold wisp straight up to the cloud level
  (aggregated per frontage outside the capital view; ≥ $5B per player in it). Water evaporates; capital is what rises.
* **Credit** ($0.93T): from the cloud level down onto the **banking** hill's cap (a dashed gold column), then a dashed gold
  stream down the banking hill's face and along the valley network to each player who dissaves (≥ $5B; else merged
  into the largest). Dashed (0.06 on, 0.04 off), alpha 0.5.
* **The capital account at the cloud level** (checked, 5): saving in = credit out + external investment out
  (2025: $2.16T = $0.93T + $1.23T); capital income passes through it unchanged.

### 4.6 Taxes and transfers: the state's ground water (steel)

* **Taxes seep.** Personal taxes ($5.25T): a short steel drip (0.15 u) from each settlement **into the ground** at its
  road point; corporate taxes ($452B, `government.corporateTax`) drip from the gold caps; taxes on production (Σ taxShare ·
  VA = $2.15T in 2025) drip from the upkeep rings. Underground **conduits** (steel, y = ground − 0.30 − 0.03 k by rank k)
  run under each road toward its centre, then down the central line x = 0 under every riser to the gov plain, and rise
  into the mesas: personal taxes 87% federal, 13% state & local (`government.stateLocalPersonalTaxes`); corporate taxes
  federal; production taxes state & local by `stateLocalProductionTaxes` / total ($1.88T of $2.15T), the rest federal
  **[data]**. Everything under the ground flows toward the front: water seeps down.
* **Transfers spring.** From the mesas, conduits run back under the roads (reverse lanes, 0.06 deeper) and rise as a
  **fountain** at each recipient: a vertical steel jet (height 0.15 + 0.6 × $ / $200B, capped at 0.9) with a ring at its
  foot. Social Security and the Medicare share from the federal mesa, the rest from state & local (the bowl's split).
* **Width** dollars × `WidthGdp`, alpha 0.5 underground (visible through the translucent ground), 0.8 above ground.
  Aggregation: per frontage outside the state view; per player ≥ $5B in it.

### 4.7 Roots: what each hill stands on (IO, underground)

`RootsModel`'s selection is reused (79 between-industry flows ≥ $50B, 72.7% of $15.85T; mesh roots for the rest; own
purchases as root balls; other years scale the 2024 table by the buyer's value added). New geometry: leave the
supplier's underside at its centre (y = BenchY_s − 0.25), travel at depth y = G(d) − 0.45 − 0.012 k (k = rank among the
buyer's roots), x and d lerping, rise vertically into the buyer's centre from below. Root balls: a closed loop under the
hill at ρ = 0.6, 0.12 below its bench. Width × `RootWidthGdp`, the supplier's tier hue, alpha 0.35, pulses toward the
buyer. Direction honesty (1.4) printed in the roots line.

### 4.8 Identities checked every build (`HillFlows.Check`)

| # | Identity | Tolerance |
| --- | --- | --- |
| 1 | each player: in = out (the accounts' identity, unchanged) | ≤ 0.5% |
| 2 | Σ wages streams = Σ players' wages; Σ vapor = business + capital income + payouts abroad | ≤ 0.1% |
| 3 | every road bin and gorge: in = out (junctions) | ≤ $0.01B |
| 4 | Σ lakes + abroad = Σ spending | ≤ 0.1% |
| 5 | cloud level: saving = credit + external investment | ≤ 0.5% |
| 6 | Σ capital rivers = I_tot; Σ external = X | ≤ 0.1% |
| 7 | taxes into the mesas = Σ taxes; federal / state & local split = the circuit's | ≤ 0.1% |
| 8 | spending paths: no point on a hill (own height ≥ 0.05) except lake inlets; capital paths: monotone up (y never falls by > 1e-4) | 0 violations |
| 9 | Σ hill areas = AreaGdp (25 hills, VA > $1B); summits strictly ordered by tier | ≤ 0.01 u² / exact |

---

## 5. Aggregation and legibility rules (the first build's lesson)

1. **One story per view at full strength**; everything else ≤ 0.15 (the hills themselves ≥ 0.35 so the land always reads).
2. **Dense flows aggregate** outside their own view: wages per (hill, frontage) ~40; vapor per (hill, kind) ≤ 100;
   saving, transfers and taxes per frontage; spending is already merged by the router.
3. **Width is always dollars**; aggregation never changes a total (identity 2).
4. **Labels are generated from numbers** (`LandFacts`, ±10% band for "about the same").
5. **No hue is shared between meanings**: gold capital only (caps, capital rivers, vapor, crowns, clouds), light blue
   wages, ice / rose motives, white fantasy, steel state, tier hues for ground and roots.

---

## 6. The unfold: the cut becomes the land

`SectionLayer` keeps the cut on the road unchanged (frame, card bars, lifeline dots, readout). The new morph lives in
`HillSectionLayer` (the bowl's morph stays in the disabled `SectionLayer` path). One clock `LandView.MorphTime` ∈ [0, 2.4]
s (unfold at 1×, fold at 2×), as today.

| Phase | Time (s) | What moves | Formula |
| --- | --- | --- | --- |
| **A Lift** | 0.00-0.40 | card bars and dots | translate from the cut to the land's front edge (d = 0, x = 0), facing the camera; ease in-out cubic |
| **B Lie back** | 0.30-1.10 | bars → **tiles on the staircase**; the ground grows | bar i (tier t) flies to its hill's centre (x_i, BenchY_t + 0.01, d_i) along a Bézier with control (midpoint + 0.8 up); it turns from vertical to horizontal (axis +y → +d by 90° · s); its size goes from (bar width 0.6, bar height h_i) to the rectangle (√π a_i, √π b_i) by w(s) = w0^(1−s) w1^s, ℓ(s) = k(s) A_i / w(s), k(s) = k_card^(1−s) · 1^s, so its area is k(s) A_i throughout and exactly A_i at the end (A_i in u²; k_card = the card's area per u² of value added). The bar's three strips (wages bottom, upkeep, owners top) become three **nested** rectangles (wages outer, owners inner) of the same areas. The ground mesh is drawn with y × s_B: the staircase rises out of the plaza |
| **C Rise** | 1.00-2.00 | the hills grow out of the tiles | terrain vertex y = G(d) + s_C · Σ own_i (per-frame CPU update of the 21K terrain vertices, ≤ 1.5 ms); the tiles' rectangles turn into the zone ellipses (superellipse exponent 8 → 2) and fade (alpha 1 − s_C) as the zone rims and contours (alpha s_C) take over |
| **Dots** | 0.40-1.90 | each lifeline dot flies to its player's place (the people part's `Figure.DotSlot`) | quadratic Bézier, control = midpoint + (0, 1.6, 0); start 0.40 + 0.9 × rank of the player's x / players; 0.6 s, ease in-out |
| **D Reveal** | 2.00-2.40 | flows, lakes, people, crowns, clouds, labels fade in | `LandView.Reveal` 0 → 1 |

Fold: Reveal 1 → 0 in 0.2 s, then C → B → A backward, 1.2 s. The camera flies from the section pose to the landscape
pose in 2.2 s (`GraphRoot.Focus`): it rises and dollies forward while the card lies back away from it and the hills grow
in front of it. `WHY_ECON_MORPH=0.9` freezes the staircase with its tiles, `=1.5` the half-grown hills.

---

## 7. Views

### 7.1 Presets (16:9 and 9:16)

Poses are land-local (target, yaw offset from `LandFrame.Yaw`, pitch, distance). Distances were fitted with the
prototype's camera (vertical FOV 45, the HUD's top 10% and an 8% bottom margin kept free) so the listed points fit
**[proto]**. Portrait distance follows today's rule (distance = max(Distance, PortraitWidth / (2 tan 22.5° · aspect)))
and today's 0.2-of-the-visible-height aim, which keeps the land in the upper 60% above the bottom sheet.

| Key | Id | Title / subtitle | 16:9 pose (target; yaw; pitch; distance) | 9:16 (pitch; PortraitWidth) | Must be in frame |
| --- | --- | --- | --- | --- | --- |
| 1 | `overview` | **The economy, 1946 - 2026** / "The road of time; one year cut out and grown into a land" | midpoint of `OnRoad(1990, 0.3, FramingRho)` and `World(0, 1.5, 0)`; road normal + 20°; 28; 34 | 28; 30 | the 1950 tick, the cut, the whole land and its back peaks |
| 2 | `section` | **The cut through {Y}** (unchanged) | unchanged | unchanged | the whole frame and readout |
| 3 | `landscape` | **The hills of value** / "Each industry a hill as large as the value it adds, as high as its stratum; gold is what owners keep" | (0, 1.6, 0.3); 0; 38; 22.5 | 58; 14.5 | the whole land, 5 bench labels, 25 hill labels (≥ 24 at 9:16) |
| 4 | `capital` | **Capital climbs, profit rises** / "Investment runs up the hills; profit rises to the crowns and the clouds" | (0, 2.4, 2.1); 0; 22; 17 | 30; 13 | make, services and tech ranges, the cloud level, every capital river |
| 6 | `rivers` | **Where the money goes** / "Spending runs along the valleys into the lakes at the hills' feet: ice is fear, rose desire, glitter fantasy" | (0, 1.2, 0.6); 0; 62; 24.75 | 66; 14 | every road, gorge and lake; the 6 largest lake labels |
| – | `roots` | **What each hill stands on** / "Purchases between industries rise into each buyer from below" | (0, 0.9, 0); −24; 14; 19.25 | 20 (yaw 0); 14 | all 79 roots, the bedrock |
| – | `state` | **Taxes and transfers** / "Taxes seep down to the state; transfers spring up where people live" | (0, 0.6, −3.8); 0; 46; 13.75 | 52; 13 | the plain, both mesas, raw and make treads, the central conduit |
| – | `y1972` | **The land in 1972** / "The same cut, fifty-three years earlier" | as landscape; temporary year 1972 | as landscape | as landscape |

Keys 5, 7, 8 (people / owners, mind, society) and the keyless `betrayal` belong to the other parts; their poses should
use the same frame (the people view: target (0, 1.4, −1.0), pitch 34, distance 15 shows the raw, make and services
treads at 80 px per unit).

### 7.2 Emphasis (this part's groups; `LandGroup` gains them, 8.1)

| Group | overview | section | landscape | capital | rivers | roots | state | people* | mind* | society* | y1972 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Road (the road and wall) | 1 | 1 | .35 | .35 | .35 | .35 | .35 | .35 | .35 | .35 | .35 |
| Cut | 1 | 1 | .6 | .6 | .6 | .6 | .6 | .6 | .6 | .6 | .6 |
| Ground | .8 | 0 | 1 | .6 | .7 | .35 | .9 | .8 | .5 | .5 | 1 |
| Hills | .8 | 0 | 1 | .7 | .5 | .35 | .4 | .6 | .4 | .4 | 1 |
| Contours | .5 | 0 | 1 | .6 | .35 | .2 | .3 | .5 | .3 | .3 | 1 |
| Caps (gold zones, rims) | .8 | 0 | 1 | 1 | .4 | .3 | .3 | .6 | .4 | .3 | 1 |
| Lakes | .5 | 0 | .5 | .3 | 1 | .1 | .2 | .4 | .4 | .15 | .5 |
| Roots | .2 | 0 | .3 | .1 | .05 | 1 | .05 | .05 | .05 | .05 | .3 |
| Spending | .4 | 0 | .3 | .1 | 1 | .05 | .15 | .3 | .6 | .1 | .3 |
| Glitter | .3 | 0 | .2 | .05 | 1 | 0 | .05 | .2 | 1 | .05 | .2 |
| Wages | .3 | 0 | .3 | .15 | .15 | .05 | .2 | 1 | .2 | .1 | .3 |
| Capital (rivers, rain, springs) | .4 | 0 | .4 | 1 | .15 | .05 | .1 | .3 | .2 | .1 | .4 |
| Vapor (business, capital income, abroad) | .3 | 0 | .3 | 1 | .1 | .05 | .1 | .6 | .2 | .1 | .3 |
| Saving & credit | .2 | 0 | .15 | 1 | .1 | 0 | .1 | .4 | .2 | .05 | .15 |
| Taxes & transfers | .2 | 0 | .15 | .05 | .15 | .05 | 1 | .4 | .1 | .05 | .15 |
| Abroad (imports, payouts abroad) | .2 | 0 | .2 | .4 | .6 | 0 | .1 | .1 | .1 | 0 | .2 |

(*) suggested values for the other parts' views; they own those columns.

### 7.3 Labels and anchors

All `Fixed` world positions (`LandFrame.World`), generated text (`LandFacts`), shown when the preset lists the group and
its alpha ≥ 0.3.

| Label | Position | Priority | Group | Text (2025) |
| --- | --- | --- | --- | --- |
| benches (all five) | on the left side face of the bedrock at each tread height, x = −7.05 | 40 | Ground | "SERVICES · $14.8T · 48% of GDP" |
| hills | summit + 0.15 | 10 + 60 × VA share | Hills | "Real estate · $4.24T · owners keep 57%" |
| lakes ≥ $300B | lake front edge | 14 | Lakes | "Health · households pay it $3.75T directly (158% of what it creates) · <fear>% fear" |
| capital rivers, the 6 largest | river head | 22 | Capital | "Capital into real estate $1.58T a year (estimate) · $343B from savers" |
| vapor, the 6 largest hills | cap + 0.6 | 20 | Vapor | "Real estate pays out $1.69T: rent and dividends" |
| cloud level | back edge, right | 30 | Saving & credit | "Cloud level · savers $2.16T up · credit $0.93T and investment $1.23T down" |
| roads | none (the legend's chips name the categories) | | | |
| state | above each mesa | 34 | Taxes | "Federal · taxes in $<..>T · transfers out $<..>T" (generated from the conduits) |
| imports | the edge fall | 26 | Abroad | "Abroad $2.27T of household spending (imports)" |
| roots, the 6 largest | the root's lowest point | 24 | Roots | "Professional services → trade $704B" |

**Anchors** (`kind:id`): `land:bench:<tier>`, `land:hill:<industry>` (replaces `land:sector:`), `land:lake:<industry>`
(replaces `land:pool:`), `land:capital:<industry>`, `land:vapor:<industry>`, `land:clouds`, `land:road:<tier>`,
`land:gorge:<riser>:<k>`, `land:taxes`, `land:transfers`, `land:mesa:<federal|state_local>`, `land:abroad`,
`land:root:<from>><to>` (kept), `land:cut` (kept). **Highlight ids** (`EconomyIds`, a new block clear of the bowl's
80,000-95,000 so both can coexist): `Hill(i, part)` 96,000 + 8 i + part (0 wages, 1 upkeep, 2 cap, 3 contour, 4 lake,
5 roots in, 6 capital river, 7 label); `Bench(t)` 96,300 + t; `Road(t, lane)` 96,400 + 8 t + lane (lanes 0-5
categories, 6 imports); `Gorge(k)` 96,450 + k; `Vapor(i)` 96,500 + i; `CloudLevel` 96,560; `TaxConduit` 96,570;
`Transfer(frontage)` 96,600 + k; `Root(k)` reuses `LandRoot`.

---

## 8. Interfaces with the other parts

### 8.1 What this part offers (contract, `Land/Hills/HillTypes.cs`, frozen after the contract package merges)

```csharp
namespace Why.Economy.Land.Hills
{
    public sealed class HillGeom
    {
        public int Industry; public Tier Tier; public bool Present;          // Present: VA >= $1B
        public float X, D, A, B, Own, BaseY, SummitY;                        // centre (x, d), semi-axes, own rise, bench, summit
        public float RhoCap, RhoUpkeep;                                       // sqrt(o), sqrt(o + u)
        public double ValueAdded, Wages, Upkeep, Owners;                     // $B, the year's
        public float Frontage0, Frontage1;                                    // x interval of its frontage on its tread
        public Vector3 Summit => new Vector3(X, SummitY, D);                 // land-local with d (World() applies z = d - 7.425)
        public Vector3[] CompanyAnchors;                                      // capture companies on this hill (Y >= 2024)
        public LakeGeom Lake;                                                 // null when no direct household spending
    }
    public sealed class LakeGeom { public float X, D, A, B, Y; public double Inflow, Fear, Fantasy; }
    public sealed class BenchGeom { public Tier Tier; public float Y, RiserD, ValleyD, HillsD, ZoneDepth, RoadD; }
    public sealed class GorgeGeom { public int Riser; public float X; public Vector3[] Path; }
    public sealed class CloudSlot { public Vector3 Point; public int[] Hills; }
    public sealed class TerrainGeometry
    {
        public int Year; public double Gdp; public float AreaPerB, LakeAreaPerB, WidthPerB, CapitalWidthPerB;
        public HillGeom[] Hills;                                              // 25, EconomyData.Industries order
        public BenchGeom[] Benches;                                           // 5
        public GorgeGeom[] Gorges; public CloudSlot[] CloudSlots;
        public float CloudY;                                                  // 4.50
        public TerrainField Field;                                            // the rasters below
        public string Log; public double Checksum;
    }
    public sealed class TerrainField                                          // pure, any thread
    {
        public float Height(float x, float d);         // H, bilinear on the 0.05 raster
        public float Ground(float x, float d);         // G only (the tread / riser height)
        public int Owner(float x, float d);            // hill index, -1 for ground
        public float Rho(int hill, float x, float d);  // the hill's own normalized radius
        public bool IsFloor(float x, float d);         // ground, not a riser, not a lake, not the road corridor
        public Vector3 OnSurface(float x, float d, float lift = 0);
        public Vector3 Downhill(Vector3 p);            // one steepest-descent step (0.04)
    }
    public static class HillLayout { public static TerrainGeometry Build(EconomyData data, int year); }   // pure, ≤ 25 ms
}
```

Offered to **people and wealth**: the vertical code (3), floors and frontages (where the less wealthy settle: each
player's frontage is its anchor's; pseudo-anchors as the bowl's: Social Security retirees and the out of work on the
gov plain in front of their mesa, comfortable retirees at finance, students at education), slopes (ρ bands for owners of
small businesses: the people part may put a business owner on its hill's upkeep ring), crown bands and company anchors
(crowns), cloud slots (pools of the diversified). Also `FlowPath`s ending at their players, so their glyphs can light
their own flows (`EconomyIds`), and the morph clock.

Offered to **the mind program**: per player, the dollars of every flow (`MoneyAccounts`) and the rivers' fear / desire /
fantasy split, so the mind view can show "where this player's money went" on the land (highlight its paths).

Offered to **couples and births**: nothing new; the cut's dots keep the lifeline ids.

### 8.2 What this part needs

| From | What | Used for |
| --- | --- | --- |
| people and wealth | `Player` (the bowl's type, kept): money fields; **place**: `Place` (Floor, Slope, Crown, Cloud), position land-local (x, y, d), disc radius, `GroundX` (where its spending, taxes and descent enter the ground: default = its disc x for floor players, its anchor hill's x otherwise), `Tread` (tier of the tread it stands on or belongs to); cloud pools as players at cloud level; crowns as players (or player parts) over hills | the router's entry points, wage destinations, vapor targets, rain, fountains, credit |
| people and wealth | discs kept out of the road corridor (±0.07) and the lakes (`TerrainField.IsFloor`) | rivers never run under a disc |
| mind program | the per-player accounts: if the mind program changes spending, saving or borrowing, it writes the same `Player` fields before `MoneyAccounts` runs (wages, business, capital, transfers, taxes, spending by 6 categories with fear and fantasy, saving, borrowing) | every flow width |
| all | the `LandGroup` columns of their own views (7.2) and their label groups | emphasis |

### 8.3 Keeping the bowl

The bowl stays compiling and is never created in `Economy.unity`: its land layers (`LandscapeLayer`, `FlowsLayer`, the
bowl morph of `SectionLayer`, and whatever the people part retires: `InhabitantsLayer`) change to
`[GraphScenes(GraphScene.EconomyBowl)]` with `GraphScene.EconomyBowl = "economy-bowl"` (Core/GraphScenes.cs gains the
constant and `IsEconomy` returns true for both ids). Shared economy layers (road, wall, SMV, loader, `LandModelLayer`,
`LandViewLayer`) carry both ids. The bowl's presets move to `BowlPresets` (registered only in `economy-bowl`). Result:
Unity's economy scene shows the hills; the harness can still render the bowl with `--scene economy-bowl` (a regression
check that its log stays byte-identical, 10.3). Models shared by both: `PlayerCensus` (groups, anchors), `MoneyAccounts`,
`SellerMatrix`, `CapitalSources`, `RootsModel` (selection), `SocialSeason`, `Coalitions`.

---

## 9. Engineering

### 9.1 Files

| File | Status | Content |
| --- | --- | --- |
| `Land/Hills/HillTypes.cs` | new (contract) | 8.1 |
| `Land/Hills/HillStyle.cs` | new | every constant of sections 1-7 (AreaGdp 46, LandW 14, Depth 14.85, Edge 0.30, Gap 0.22, BenchY, Front 0.90, GovZone 1.00, Riser 0.90, Valley 1.00, DZones 5.00, ZMin 0.70, OwnK 0.95, OwnMin 0.12, OwnMax 0.80, MesaY 0.35, MesaEdge 0.50, CloudY 4.50, Raster 0.05, Mesh 0.10, RoadD 0.42, LakeAreaGdp 11.5, LakeBMax 0.32, CapitalWidthGdp 4, Gorges 3, GorgeSep 2.0, flow thresholds 5 / 10 / 20 $B) |
| `Land/Hills/HillLayout.cs` | new | 1.2-1.6: arrangement (once), staircase, footprints, packing, relaxation, heights, frontages, lakes (after the accounts), gorges, cloud slots, log, checksum |
| `Land/Hills/TerrainField.cs` | new | rasters (H, G, owner, ρ), bilinear queries, steepest descent / ascent, marching squares contours |
| `Land/Hills/ValleyNetwork.cs` | new | roads, gorge paths (Dijkstra), the DP router, bin accumulation |
| `Land/Hills/HillFlows.cs` | new | 4.1-4.8: every `FlowPath`, aggregation per view level, identities, log |
| `Land/Hills/InvestmentModel.cs` | new | 4.3's estimate (and the data path when `investment2024` exists) |
| `Land/MoneyAccounts.cs` | new (moved) | the geometry-free part of `MoneyRouting.Build` (4.1 of the bowl spec, the RAS, the crown's sums); `MoneyRouting` calls it first (the bowl's log byte-identical) |
| `Layers/TerrainLayer.cs` | new | Order 50: section 2, roots, labels, anchors; `[GraphScenes(Economy)]` |
| `Layers/HillFlowsLayer.cs` | new | Order 53: section 4's meshes (ribbons, glitter, streams, vapor, rain, conduits, fountains), per-category submeshes |
| `Layers/HillSectionLayer.cs` | new | Order 56: section 6's morph (the cut itself stays in `SectionLayer`, shared) |
| `Land/LandTypes.cs` | changed | `FlowKind` gains Runoff, Road, Gorge, LakeInlet, CapitalRiver, CapitalRain, CapitalSpring, Vapor, Rain, Evaporation, Credit, TaxSeep, TaxConduit, TransferConduit, Fountain, EdgeFall; `LandGroup` gains Ground, Hills, Contours, Caps, Lakes, Spending, Wages, Capital, Vapor, SavingCredit, TaxesTransfers, Abroad (the bowl's members stay) |
| `Land/LandService.cs`, `Land/LandView.cs`, `Layers/LandModelLayer.cs` | changed | the snapshot gains `TerrainGeometry Terrain` and `HillFlowsResult HillMoney`; `LandModelLayer` builds `HillLayout` instead of `LandLayout` in the economy scene (both in economy-bowl) |
| `EconomyViews.cs`, `EconomyPresets.cs` | changed | 7.1, 7.2 (bowl tables move to `BowlViews`, `BowlPresets`) |
| `Core/GraphScenes.cs` | changed (one constant, one comparison) | 8.3; the why scene is unaffected (`Current == Why` path unchanged) |
| `Layers/LandscapeLayer.cs`, `Layers/FlowsLayer.cs`, `Layers/SectionLayer.cs` (bowl morph part) | retagged | `[GraphScenes(GraphScene.EconomyBowl)]` |
| `UI/LandPicker.cs`, `Land/LandPick.cs` | changed | hills by `TerrainField.Owner` on the ray's hit (march the ray over H, 0.05 steps), lakes by ellipse, rivers ≤ 6 px to the ribbon centre lines, capital rivers and vapor ≤ 6 px |
| `UI/LandLegend.cs` | changed | the unit line: "1 u² = $0.67T of value added · lakes ×¼ · width 0.1 = $3.1T a year · capital and roots ×4"; category chips |
| `Assets/Resources/Data/economy/industries.json` | optional | `investment2024` (9.4) |

Code under `Land/` and `Layers/` never references `UI/` (the harness does not compile `Economy/UI`).

### 9.2 Pipeline

1. **Load** (`LandModelLayer.Prepare`, tier 4, worker): census → **people part's placement** → `MoneyAccounts` →
   `HillLayout.Build` (terrain without lakes) → lakes and frontages filled from the accounts' pools → `HillFlows.Build`
   (needs the players' places) → season (unchanged) → log lines.
2. **Prepare of tier-5 layers**: `TerrainLayer` builds the terrain mesh, contours, rims, lakes, roots; `HillFlowsLayer` the
   flow meshes. **Upload** creates renderers on GameObjects placed with `LandFrame.Place`.
3. **Year change**: the snapshot rebuilds on a worker; both layers rebuild their builders on a worker; meshes swap together
   (`LandService.ReportReady`) with the 0.4 s cross-fade (benches slide; hills grow and shrink in place).
4. **Per frame**: emphasis alphas (material `_Alpha`), the morph (terrain y update only while 1.0 < t < 2.0), flow pulses
   (shader time), nothing else.

### 9.3 Performance budget

| Item | Thread | Budget |
| --- | --- | --- |
| `HillLayout.Arrange` (once) | worker | ≤ 5 ms (12 sweeps × 25 hills) |
| `HillLayout.Build` per year (relaxation 300 × ≤ 55 pairs, rasters 281 × 298 cells × 25 hills with bounding boxes, gorges 9 Dijkstra on ≤ 4.5K cells) | worker | ≤ 25 ms |
| `HillFlows.Build` (router: ≤ 130 players × 25 sinks × ≤ 81 gorge paths; steepest paths ≤ 200 × 60 steps) | worker | ≤ 25 ms |
| `TerrainLayer` meshes: surface 21K vertices; contours (marching squares over cells whose range spans a level) ≈ 15K points → ≈ 60K line vertices; rims, bench edges, lakes ≈ 8K | worker | ≤ 20 ms, ≤ 90K vertices |
| `HillFlowsLayer`: road ribbons 5 roads × 280 bins × 2 banks × 2 lanes × 2 vertices ≈ 11K; gorges ≈ 2K; glitter ≈ 4K; streams, vapor, rain, conduits ≈ 400 paths × 25 points × 4 ≈ 40K | worker | ≤ 25 ms, ≤ 60K vertices |
| Morph frame (terrain y update + tiles) | main | ≤ 2 ms |
| Year change total | workers | ≤ 120 ms |
| Scene: new vertices ≤ 150K (the bowl's land layers, about 230K, are not created) | | |

### 9.4 Determinism and data

* No random numbers; every sort has an index tie-break; Dijkstra and DP ties broken by cell index / smaller x; doubles in
  the models, floats in meshes; one worker per snapshot.
* Checksums: layout Σ (x + d + summit) over hills; flows Σ dollars × (point count) over paths; both in their log lines.
* **Data** (optional task): BEA Fixed Assets Table 3.7ESI, "Investment in Private Fixed Assets by Industry",
  2024, aggregated to the 25 ids → `industries.json` `investment2024` with `investmentSource`; the loader keeps the
  depreciation estimate when it is missing. Nothing else is added: value added, shares, IO, final demand, pools, payouts,
  taxes and transfers are already in the repository.

---

## 10. Verifying with the headless harness

### 10.1 Commands

```
SP=/tmp/claude-0/-home-user-why-2/dc9bf553-91de-54d2-9f1f-9831e1f24cfa/scratchpad; export WHY_NOW=2026-10-01T12:00:00Z
$SP/agent-tools.sh <wp> <worktree>
$SP/cc-<wp>/check.sh <worktree>                                                               # COMPILE OK
WHY_ANCHORS_OUT=$OUT/anchors.json WHY_REPO=<worktree> $SP/run-<wp>/run.sh --scene economy --render all --out $OUT/land
WHY_REPO=<worktree> $SP/run-<wp>/run.sh --scene economy --size 1080x1920 --render all --out $OUT/portrait
WHY_ECON_MORPH=0.9 WHY_REPO=<worktree> $SP/run-<wp>/run.sh --scene economy --render section,landscape --out $OUT/stairs
WHY_ECON_MORPH=1.5 WHY_REPO=<worktree> $SP/run-<wp>/run.sh --scene economy --render landscape --out $OUT/rise
WHY_REPO=<worktree> $SP/run-<wp>/run.sh --scene economy-bowl --render landscape,rivers --out $OUT/bowl   # the kept bowl
WHY_REPO=<worktree> $SP/run-<wp>/run.sh --scene why --render all --out $OUT/why                         # = $SP/why-cur
python3 $SP/redesign2/checks/hillcheck.py $OUT/land/log.txt $OUT                                        # 10.3
python3 $SP/redesign2/terrain/terrain.py 2025 1972 1950                                                 # the reference
```

### 10.2 Log lines (exact prefixes; 2025 values from the prototype, tolerances in brackets)

```
[Why] Hills 2025: GDP $30.76T = 46 u2 per GDP (1 u2 = $0.67T); land 14.0 x 14.85; benches d raw 3.80 make 6.51
      services 9.80 tech 13.59 (zones 0.81 1.39 1.90 0.91); summits gov 0.35 raw 0.97-1.08 make 2.06 services 2.49-2.90
      tech 3.17-3.23 strictly ordered; steepest hill 56 deg; IO from a range in front 21.9% within 41.0% from behind
      37.2%; 25 hills, areas 46.00 u2; checks 2/2 PASS; checksum <..>     (benches ±0.02; summits ±0.02; IO exact)
[Why] Hills 1972: ... benches d raw 3.80 make 6.61 services 10.30 tech 13.65 ...   [Why] Hills 1950: ... make 6.75 services 10.53 tech 13.69
[Why] Valleys 2025: roads 5; gorges gov->raw -6.55 -4.50 -0.70, raw->make -6.55 -4.50 -2.45, make->services -4.85
      +0.65 +5.70, services->tech -1.25 +1.45 +3.55; lakes <n> (health 2.8 x 0.64 ...); cloud level 4.50; cloud slots 16
      (gorges ±0.3; slots ±3)
[Why] Rivers 2025: spending $21.63T: lakes $19.36T, abroad $2.27T; level <a>% up <b>% down <c>%; busiest road
      $<x>T one way (<w> u); busiest gorge <..>; glitter <n> sparks; checks 3/3 PASS
      (with real players: level 30-50%, up 25-45%; busiest one-way road ≤ 0.25 u)
[Why] Capital 2025: investment $5.67T (estimate: ~ depreciation), real estate $1.58T, manufacturing $517B, trade $455B,
      professional $351B; external $1.23T (saving $2.16T − credit $0.93T), internal $4.44T; rivers 23; uphill
      violations 0                                                                                  (exact)
[Why] Vapor 2025: business $2.11T, capital $5.33T (real estate $1.69T, banking $1.15T, trade $0.51T), abroad $435B;
      wisps to crowns <n>, to clouds <n>, rain <n>; cloud level in $2.16T out $2.16T; checks 2/2 PASS
[Why] State 2025: taxes $5.25T personal + $2.15T production + $452B corporate; mesas federal <..> state & local <..>;
      transfers $4.95T in <n> fountains; checks 1/1 PASS
[Why] TerrainLayer.Prepare <ms>: <n> vertices (surface <n> ≈ 21K, contours <n>); HillFlowsLayer.Prepare <ms>: <n> vertices
```

### 10.3 Checks (`$SP/redesign2/checks/hillcheck.py`, to be written by the contract package)

| Check | Rule |
| --- | --- |
| `log` | the lines above parse; expected values and tolerances; all "checks n/n PASS" |
| `labels` | `landscape` and `y1972`: 5 bench labels and ≥ 25 (16:9) / ≥ 24 (9:16) hill labels placed; no placed label of priority ≥ 30 outside the image |
| `hues` | `rivers.base.png`: ice pixels (hue 180-200°, s ≥ 0.3, v ≥ 0.25) ≥ 0.8% of the frame, rose (325-350°) ≥ 0.5%, 0.8 ≤ ice / rose ≤ 2.5; inside each lake label's box both hues present |
| `gold` | `capital.base.png`: gold pixels (30-50°, s ≥ 0.4) ≥ 3% of the frame; in the column above each of the 3 largest caps (± 20 px of the cap's projected x, from the summit up to the cloud level's projected y) gold pixels ≥ 40 (the vapor) |
| `stairs` | `stairs/landscape.base.png` differs from `land/landscape.base.png` by ≥ 5% of pixels, and `rise/landscape.base.png` differs from both by ≥ 2% |
| `bowl` | `bowl/log.txt`'s `[Why] Land 2025` and `[Why] Money identities 2025` lines equal the current build's (the kept bowl is unchanged) |
| `why` | every `why/*.base.png` equals `$SP/why-cur/*.base.png` |
| `determinism` | two runs' checksums are equal |
| `reference` | `[Why] Hills 2025/1972/1950` bench depths, hill footprints and summits equal `terrain.py`'s within ±0.02 |

---

## 11. Prototypes behind this part (`$SP/redesign2/terrain/`)

* `terrain.py`: the arrangement, staircase, footprints, relaxation, heights, zones and their checks; renders top views.
  `python3 terrain.py 2025 1972 1950` prints the `[Hills <Y>]` lines quoted above.
* `render3d.py`: a painter's-algorithm perspective preview with the scene's camera model (vertical FOV 45, target / yaw /
  pitch / distance), used to fit the preset distances (7.1).
* `flows.py`: the valley router with stand-in settlements (2025 adults of `$SP/redesign/groups/out/personyears.csv`
  grouped by the hill that pays them; pseudo-anchors as the bowl's), the bowl's seller matrix (`synth/sellers.py`), the
  investment estimate, wages, taxes.
* `sketch.py`: the composite renders `sketch-16x9.png`, `sketch-9x16.png` (stand-in crowns over the company hills and six
  stand-in cloud pools; illustrative only).
* Earlier explorations kept for the record: `terrain_v1.py` (flat valley floor, hills summed on tier ridges: rejected,
  73-82° needles for small high-tier hills), `terrain_v2.py` (1D rows: rejected, services overflow the width),
  `terrain_v3.py` (fixed zone depths: rejected, raw and tech benches mostly bare).

**Decisions and why.**

1. *Benches, not a flat floor.* With one floor level, a hill whose area is its value added and whose summit is its
   tier's must be a needle when it is small (hardware: 0.44 u radius, 1.8 u high = 77°): the prototype's v1 had a
   median hill slope of 74°. Standing each range on its stratum's bench keeps every hill ≤ 56° and every valley visible
   from the front (stadium seating), and it is the cut's staircase, so the unfold reads as one motion.
2. *Spending may climb a riser, never a hill.* People live low and spend on higher strata: 35% of spending dollars
   climb a riser **[proto]**. Forbidding it would need siphons or aqueducts that nobody would read. The rule kept is
   the user's: only capital climbs a hill (to its gold cap); spending stops at the lakes at the hills' feet.
3. *Investment is an estimate* until BEA's investment-by-industry table is added (9.4); the labels say so.
4. *Benches breathe, the order does not.* The land's depth and the left-to-right order are fixed, so a year change
   grows and shrinks hills in place and slides the benches: 1950's make massif and 2025's services range are the same
   land.
5. *Risks.* The services bench is crowded in 2025 (11 hills in 2D): the relaxation keeps every pair ≥ 0.22 apart, but
   frontages of hills pushed back are up to 1.6 u from their feet; if the people part finds that too far, raise `DZones`
   to 5.4 (land depth 15.25). Portrait framing is width-bound (the land is about square): portrait views show the whole
   land at 31-40 u, with clouds and crowns above it and the bottom sheet below.

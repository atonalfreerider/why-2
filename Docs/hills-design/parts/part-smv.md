# Part: couples and births on the population graph (SMV)

`SP=/tmp/claude-0/-home-user-why-2/dc9bf553-91de-54d2-9f1f-9831e1f24cfa/scratchpad`. The design was prototyped on a private
copy of the repo (`$SP/redesign2/smv/repo`; the full diff is `$SP/redesign2/smv/proto.patch`, 915 lines) and rendered with
the headless harness (`$SP/run-d-smv`, `WHY_NOW=2026-10-01T12:00:00Z`). Every number marked **[proto]** comes from that
run. The renders are in `$SP/redesign2/smv/c5/` (16:9), `c5p/` (9:16) and `c5off/` (the same views with the bonds off).
Comparisons: `cmp-v2-obl.png` (top: today, bottom: this design) and `cmp-v2-near.png`.

The user's words: *"on the smv graph, we need to show relationships between men and women, and children are born from two
adults intersecting in the mid-plane of the graph"*.

---

## 0. What the viewer sees

* **The mid-plane** is drawn: a faint vertical veil through the stream's centre, running from the floor of the human layer
  to the top of the value axis and from 1946 to now. A hairline runs along its top. Women run on its inner side and men on
  its outer side, as before.
* **Couples run beside the mid-plane.** From the wedding on, both spouses' lines ease to half their distance from the
  mid-plane (the simulation already mirrors them). A married couple is a pair of lines hugging the plane from both sides.
  A faint **tie** crosses the plane at the wedding. At a divorce a **broken tie** is drawn (two half-ties that stop short of
  the plane), and the lines peel back outward over a year.
* **Every child born from 1946 on is born on the mid-plane.** The mother's and father's lines bend in over the 1.5 years
  before the birth and touch at one point on the plane, at the mean of their two heights. The child's line starts at that
  point and falls to the children's core near the floor, glowing for its first year. A small **bead** marks the meeting.
  When the same parent has two births 2.5 years apart or less, that parent's line stays on the plane between them. A
  married couple with close children therefore runs together along the plane, which reads as a braid.
* **Rule for the viewer:** a line touches the mid-plane only where a child is born. Every line starts either on the
  mid-plane (born here) or at the stream's edge (arrived as an immigrant).
* **One family, legible.** In the new `families` view (key 9), one real family of the simulation is drawn bright over the
  dim crowd with four labels: the mother, the father, the wedding, and one child ("each meeting = 100,000 births"). Clicking
  any lifeline draws that person's family the same way: spouses with yearly ties, the parents meeting at the person's own
  birth, and the stems of the person's children. The highlight lights all of them.

Evidence **[proto]**, `c5/familiesobl.png`: the featured couple (married 1972.5, births in 1973.00, 1977.25, 1985.00 and
1990.25) braids along the mid-plane, with four bright knots and four stems falling to the floor. In `c5/familiesnear.png`
the 1985 birth is an X whose third arm descends. The brightest pixel in a 9×9 window on the knot is 255; with the bonds
off it is 160.

---

## 1. Facts the design stands on (read from the code, measured)

| Fact | Value | Source |
| --- | --- | --- |
| Data space | (u = clock arc, y = height, ρ = radius); the mid-plane is ρ = `Center[k]` | `SmvSimulation.Place` |
| Women / men | offset −/+ from the centre, as a fraction of `Envelope[k]` (the half width available to one sex) | `Place` |
| Single adults | rank offset 0.08 … 1.0 (`SmvModel.RankOffset`) | |
| Married adults | the mean of both rank offsets (0.04 + 0.5·o) × closeness (1 → 0.6 over 25 years), mirrored | `Place` |
| Children | offset 0.05·age/18 (girls inner, boys outer), height 0 … 0.08·SmvHeight; **offset 0 at age 0** | `Place`, `SmvModel.Height` |
| Newborn today | starts on the mother's line (smoothed into the core, τ 0.8 y); a separate faint link from the father | `PlaceNewborn`, `BuildParentLinks` |
| Envelope (data ρ) | 1960 0.137 · 1990 0.261 · 2020 0.359 **[proto]** | log `SmvScale` |
| Economy lens | 16 world units for 1946 → now (0.2 u / year), ρ × 2.5, y × 3 (SmvHeight 0.3 → 0.9 u) | `EconomyStyle` |
| So, in world units (1990) | one side of the envelope = 0.65 u; a married couple at the median is about 0.1 u apart across the plane and 0.25 u apart in height | |
| Population | 4,996 lines × 100,000; coarse tier 500 (every 10th per sex); 2,078 marriages, 797 divorces; 4,353 births with a mother (1,263 of them before 1946); 530 immigrants | `SmvLayer.Prepare` log |
| Births from 1946 | **3,090** = 2,795 to married parents + 295 to an unmarried mother and a father + 0 mother-only (all 70 father-less births are before 1946) **[proto]**; 3,090 × 100,000 ≈ 309M, close to US births 1946-2025 (≈ 300-310M [recalled]) | |
| At the birth, before bending | parents differ by 2.76 value units in height on average (p90 4.94); each travels 1.44 units to the meeting (max 4.58); the distance across is 0.095 × envelope for married parents and 0.230 for unmarried ones **[proto]** | |
| Sample consumers | display only: `SmvGeometry`, `LifelinePicker`, `PersonLine`, `PersonFraming`, `SectionLayer` (the cut's dots), `LandPick`. **No model reads `SampleY` / `SampleRho`** (grep), so rewriting the samples changes only the drawing | |

Consequence: married parents are already close to the plane across ρ. The meeting is therefore mostly **vertical**, as the
two heights converge. The **child's fall** from the meeting height to the floor (2.40 value units on average **[proto]**)
is the largest motion. This design makes those two motions the glyph: the vertical meeting and the fall.

---

## 2. Scope: a switch only the economy turns on

* New options class `SmvBondsOptions`, published by `EconomyLoaderLayer` (Order 25) under a new key
  `SmvPopulation.BondsKey = "smv.bonds"`. It uses the same mechanism as `ISmvLineStyle` under `StyleKey`.
* `SmvLayer.Prepare` (Order 30) reads `ctx.Shared<SmvBondsOptions>(BondsKey)`. If the result is **null** (the causality
  scene), nothing changes: no bonds pass, `BuildParentLinks` runs as today, and the meshes are byte-identical. **Verified
  [proto]**: `whycheck.sh` reported "14 presets, 0 differ: PASS", and the Why log line is unchanged ("152948 fine / 15295
  coarse … 4283 parent links").
* If options are present, `SmvLayer` runs the following, in this order:
  1. `sim.Run()`
  2. `style?.Prepare(sim)`: the economy's lives are modelled on the *unbent* simulation
  3. `bonds = new SmvBonds(sim, options); bonds.Apply()`
  4. geometry with `Bonds = bonds`
  5. `BuildBonds()`, `BuildMidPlane()`, and **no** `BuildParentLinks()`
* `SmvPopulation` gains `public SmvBonds Bonds;`, which is null in the causality scene.
* **The causality scene does not get the switch now.** Its smv view is pinned pixel for pixel by `whycheck`, and its
  narrative ("children's lines start on their mothers' lines; father links") is complete. The switch is a load-time option,
  not a per-view one: the bonds rewrite the samples once, and keeping both drawings would double the 0.96M-sample arrays.
  Turning it on in `Why.unity` later takes one line: a Why layer before Order 30 publishes `new SmvBondsOptions
  { FromYear = 1950 }`. That change would also need a new whycheck baseline for the `smv` / `present` presets. I recommend
  offering it to the user as a follow-up, not doing it now.

---

## 3. The bonds pass (`SmvBonds.Apply`, worker thread, single-threaded, no randomness)

It runs once on the finished simulation and **rewrites `SampleY`, `SampleRho`, `StartY` and `StartRho` in place**, so every
display consumer (the lines, the pickers, the cut's dots, the selected line, the land's unfold) agrees with the drawing.
Notation: k is a step (0.25 y); c_k = `Center[k]`; E_k = `Envelope[k]`; S(x) = smoothstep(clamp01(x)); K(x) = (1 − x²)² for
|x| < 1 and 0 otherwise.

### 3.1 Constants (`SmvBondsOptions`, defaults are the economy's)

| Constant | Value | Meaning |
| --- | --- | --- |
| `FromYear` | 1946 | births and arrivals before this keep today's drawing (off the economy's road) |
| `CoupleSqueeze` | 0.5 | married offsets × 0.5 |
| `CoupleRampIn` / `CoupleRampOut` | 1.0 / 1.0 y | the squeeze eases in after the wedding and out after the end |
| `BendBefore` / `BendAfter` | 1.5 / 1.0 y | a parent's reach into a meeting (married parents, all mothers) |
| `BriefBefore` / `BriefAfter` | 0.75 / 0.5 y | the reach of a father not married to the mother ("a brief encounter") |
| `BridgeYears` | 2.5 | two births of one parent at most this far apart: the line stays on the plane between them |
| `ChildDescentTau` | 0.8 y | the newborn leaves the meeting point (same τ as the simulation's own smoothing) |
| `ArrivalEdge` / `ArrivalTau` | 1.0 × envelope / 0.5 y | immigrants start at the edge of their side and glide in |
| `GlowGain` / `GlowAlpha` | 2.0 / 1.2 | drawing: intensity × (1 + 2.0 g), alpha × (1 + 1.2 g) where the line bonds |
| `NewbornGlowYears` | 1.0 | the child's stem glows, g = e^(−age / 1.0) |
| `KnotColor` / `TieColor` | (0.70, 0.80, 1.0) / (0.62, 0.72, 1.0) | blue-white: people's blue raised to white, never gold (gold = capital) |

### 3.2 Steps, in this order. The order matters: an immigrant mother arriving shortly before a birth gave 78 misses until arrivals moved before the bends

1. **Couples.** Index the marriages per person from `MarriageLog`. For every sample of a married or once-married person:
   `r(t) = max over their marriages m` of
   * S((t − m.Start) / 1.0) while t < End, and
   * S((End − Start) / 1.0) · (1 − S((t − End) / 1.0)) after End.

   Then `ρ ← c_k + (ρ − c_k) · (1 − 0.5 r)`. Widowhood (End = the spouse's death) and divorce ease out the same way.
   401,124 samples are squeezed **[proto]**.
2. **Arrivals.** For each immigrant with `Enter ≥ FromYear` (530):
   * `edge = c_k0 ± 1.0 · E_k0` (men +, women −) at k0 = `FirstStep`; `Δ = edge − ρ[k0]`;
     `ρ[k0 + j] += Δ · e^(−0.25 j / 0.5)` until the factor is below 1e-4.
   * `StartRho = edge`.
   * Drawing: alpha × S((t − Enter) / 1.0), so the arrival fades in. Without the fade, the glides read as hooks sticking
     out of the stream's rim (`c4/familiestop.png`).
3. **Births.** For each person c with `Mother ≥ 0`, `BirthStep ≥ 0`, samples, and `TimeOf(BirthStep) ≥ FromYear`
   (`BirthStep == FirstStep` for every native-born line):
   * The father counts only if he is alive at k.
   * `Y = (y_mother[k] + y_father[k]) / 2`, or `y_mother[k]` with no father.
   * Kind: Married if the father is the mother's husband in a marriage covering t; else Unmarried; else MotherOnly.
   * Record `SmvBirth { Child, Mother, Father, Step = k, Y, Kind }`, `BirthOf[c] = index`, and `BirthsAsParent[parent]`
     sorted by step.
   * When a parent has two births at the same step, both knots take the mean Y. 0 cases from 1946; 8 before **[proto]**.
4. **Bends.** For each parent P with births b_1 … b_n (steps s_i, heights Y_i), over k ∈ [s_1 − 6, s_n + 4]:
   * k = s_i: pull = 1, target = Y_i (exact meeting);
   * s_i < k < s_(i+1) and s_(i+1) − s_i ≤ 10 steps: pull = 1, target = lerp(Y_i, Y_(i+1), (k − s_i) / (s_(i+1) − s_i))
     (the **bridge**: 8,841 samples **[proto]**);
   * otherwise: pull = max(K((k − s_i) / A_i), K((k − s_(i+1)) / B_(i+1))), with A = 4 steps (2 if brief) and B = 6 steps
     (3 if brief). "Brief" means Kind = Unmarried and P is the father. The target is the Y of the birth that gave the max.

   Then `y ← y + (target − y)·pull`, `ρ ← c_k + (ρ − c_k)(1 − pull)`, and `glow[s] = max(glow[s], round(255·pull))`.
   `glow` is a `byte[]` parallel to the samples.
5. **Newborns.** For each birth:
   * `Δy = Y − y_c[k]`, `Δρ = c_k − ρ_c[k]`; `y_c[k + j] += Δy·e^(−0.25 j / 0.8)`, and the same for ρ, until the factor
     is below 1e-4.
   * `glow = max(glow, 255·e^(−0.25 j / 1.0))` while that is above 0.02.
   * `StartY = Y`, `StartRho = c_k`.

   Since a child's core offset is 0 at age 0, the correction across ρ is tiny; the motion is the fall in y.
6. **Verify.** For every birth, the child, the mother and the father (if any) are within 1e-5 of (Y, c_k) at step k. If
   not, count a miss. Expected: **0 misses** **[proto]**.
7. **Featured family** (for the `families` view, 6.3). Candidates are marriages that are:
   * not divorced,
   * started in [1972, 1984],
   * End = +∞ or ≥ 2020,
   * with ≥ 2 Married births of this couple whose median birth year is in [1982, 1988].

   Score = −min(3, n)·100 + |median − 1985| + index·1e-6; the lowest wins. **[proto]**: marriage 846 (wife 1752, husband
   1646, from 1972.50; births 1973.00 / 1977.25 / 1985.00 / 1990.25 at heights 0.796 / 0.799 / 0.782 / 0.775). The
   population depends on `NowYear` (the random stream covers births up to now), so the family is chosen at run time, not
   hard-coded.
8. **Checksum**: Σ over samples of (y·1000 + ρ) as a double, in index order. **[proto]**: 735025915.254744, identical on
   three runs.

---

## 4. Geometry (`SmvGeometry`, changes apply only when `Bonds != null`)

### 4.1 Lines (`LineShaper`)

* `Gather` reads `bg[j] = Bonds.Glow[s] / 255` per point. For a **bonded child** (`Bonds.BirthOf[p] ≥ 0`) it drops the
  `Enter` point: the line starts exactly at the knot at `TimeOf(BirthStep)`, not ≤ 0.25 y earlier.
* `Shape`:
  * `intensity *= 1 + 2.0·g` and `alpha *= 1 + 1.2·g`, applied after `Style.Restyle`, so the economy's gold / blue tint is
    kept;
  * arrivals: alpha × S((t − Enter) / 1.0).
* Thinning is unchanged (`TolY` 0.0012, `TolRho` 0.0015). The bends add points: fine 152,948 → **183,161** (+19.8%) and
  coarse 15,295 → **18,291** **[proto]**.
* Both tiers draw the same bent lines (the coarse tier is a subset), so the cross-fade between tiers never jumps.

### 4.2 Marks (new `LineMeshBuilder Marks`, fine tier, its own mesh "SmvBondMarks")

| Mark | Geometry (data space) | Style | Id | Count [proto] |
| --- | --- | --- | --- | --- |
| **Bead** per birth | a small cross on the plane: (u(t ± 0.02 y), Y, c_k) and (u(t), Y ± 0.0014, c_k) | KnotColor, alpha 0.45 (MotherOnly: 0.18, "hollow"), 1.6 px, intensity 1.6 | the child's lifeline | 3,090 |
| **Wedding tie** (Start ≥ FromYear − 1) | two half-ties from each spouse's sample at `StepAt(Start)` to the midpoint moved onto the plane (ρ = c_k), reach 1.0 | TieColor, alpha 0.10, 0.7 px | each half its spouse's | 2,401 (weddings + divorces) |
| **Broken tie** (divorce) | the same at `StepAt(End)`, reach 0.6: they stop short and leave a gap at the plane | same | same | (in the above) |

Marks total **43,928 vertices** **[proto]**. The bead is a cross, not a dash along time. A 0.3-year dash grew to about 100
px close up and covered the frame with white strokes (`c4/familiesnear.png`). The cross is 2-20 px at every zoom.

### 4.3 The mid-plane (`BuildMidPlane(FromYear, GraphIds.Civ(civ))`)

* `Surfaces.AddBand` from (u_k, HumansY, c_k) to (u_k, HumansY + SmvHeight, c_k), every 4 steps from `FromYear` to now.
* Fill: KnotColor at alpha 0.03, faded in over 4 years.
* Top hairline: alpha 0.3, 1 px, in `Markers`.

### 4.4 Coarse tier (1 line = 1,000,000 people)

* **Bead** (2.2 px, into `Coarse`) for every birth where the child, the mother or the father is in the coarse tier: **848**.
* **Arms** for every birth with a coarse child. Each parent who is *not* coarse gets their real samples from k − 6 to k
  (1.5 y), with alpha 0.45·f and intensity 1 + f, where f runs 0 → 1 toward the knot. CoarseAdultPx, the parent's id.
  **542 arms.** Every coarse child's line therefore starts where two lines meet.
* A coarse parent whose child is not drawn shows a dip to the plane ending in a bead. The legend reads: "zoomed out, one
  line in ten: a dip to the mid-plane ending in a dot is a child not drawn at this scale."

### 4.5 Unchanged

The population curves, the generation planes, the density histogram (built on the bent samples) and the LOD logic are
unchanged.

---

## 5. `SmvLayer` changes

* Prepare: the order in §2, then one log line:
  `[Why] SmvBonds <ms> ms: <summary>; marks: <beads> beads (<coarse> coarse), <ties> ties, <arms> coarse arms, <v> vertices`.
* Upload: `Marks` becomes mesh "SmvBondMarks" with its own line material (queue `QueueHumans + 2`).
* Tick: `Apply(marksMat, marksRenderer, fineAlpha · crowd · SceneAlpha · BondsAlpha)`. New static
  `public static float BondsAlpha = 1f`, which the economy sets per view (6.2). The coarse beads and arms ride on the
  coarse material.
* Anchor `smv:us` blurb, only when the bonds are on: "… Married couples run beside the mid-plane between the women's and
  the men's sides; every child's line starts where its mother's and father's lines meet on the mid-plane (each meeting is
  100,000 births); immigrants' lines enter from the edge."
* New label, only when the bonds are on: "mid-plane: where children are born" at (U(1956), HumansY + SmvHeight + 0.01,
  c(1956)), priority 9, 11 px, TextDim. It sits alongside the existing "women" / "men" labels.

---

## 6. The economy side

### 6.1 Loader

`EconomyLoaderLayer.Prepare` publishes `new SmvBondsOptions()` under `SmvPopulation.BondsKey`. The prototype uses an
environment variable `SMV_BONDS=0` to switch it off for comparison renders. Keep that variable as a harness-only switch, or
drop it.

### 6.2 Emphasis (`BondsAlpha`, a new "Bonds" row in the views' emphasis table)

| overview | section | families | the land views (landscape, capital, rivers, roots, state, people, mind, society, y1972) |
| --- | --- | --- | --- |
| 0.5 | 0.6 | 1 | 0.5 (the road is already at 0.35 there) |

`LandViewLayer` sets it beside `SmvLayer.SceneAlpha` each frame and resets it to 1 when the scene unloads.

### 6.3 The `families` view (key 9)

* Id `families`, title **"Couples and children"**, subtitle "Each child's line starts where its mother's and father's lines
  meet on the mid-plane · couples run beside it · 1 line = 100,000 people".
* `PopulationDetail = true`, which forces the fine tier.
* **Pose**:
  * target = `EconomyStage.OnRoad(t_mid, Ȳ, c(t_mid))`, where t_mid = (Start − 3 + last birth + 4) / 2 of the featured
    family (1981.9 [proto]) and Ȳ = the mean knot height (0.79);
  * yaw = the road normal at t_mid. The camera is on the inner (women's) side looking outward, as the prototype's
    `sideYaw` does;
  * pitch 35, distance 3.6 (26 years across at 16:9), PortraitWidth 5.4.
* Before the population exists, the pose falls back to (1982, 0.79, c(1982)). `EconomyPresets.Refresh` repositions the
  view once `SmvPopulation.Bonds` is available (the catalog is already refreshed in place).
* Emphasis: road 1, every land group 0. The bench labels currently leak onto the road in road close-ups: "TECH · $2.6T" in
  `c5/familiesobl.png`.
* **Labels** (economy, families view only, positions on the featured family's bent samples):

| Label | Position | Priority | Colour |
| --- | --- | --- | --- |
| "mother" | the wife's line at Start − 2 | 30 | HumansFemale |
| "father" | the husband's line at Start − 2 | 30 | HumansMale |
| "married 1972" | the wedding tie's midpoint on the plane | 28 | TextDim |
| "a child, 1985 · each meeting = 100,000 births" | the knot of the median birth | 32 | Text |

### 6.4 Family lines (the selected person; replaces the internals of `Economy/UI/PersonLine.cs` → `FamilyLines`)

Rebuilt on the main thread when the selection changes; at most about 700 points, under 1 ms. It reads the bent samples
through `SmvPopulation.PointAt`. With no selection in the `families` view it draws the featured family, with both spouses
treated as selected.

| Member | Span | Style |
| --- | --- | --- |
| the person | whole life (as today) | alpha 0.75, 1.8 px, intensity 0.5, economy tint |
| each spouse | [Start − 1, min(End + 1, death)] | alpha 0.55, 1.4 px |
| ties to each spouse | every 4 steps (1 y) of the marriage, half-ties through the plane | TieColor alpha 0.35, 1 px |
| the parents (if `BirthOf[p] ≥ 0`) | [k − 8, k] (2 y into the person's own knot) | alpha 0.55, 1.4 px |
| each child (births where p is a parent) | [k, k + 16] (the first 4 y of the stem) | alpha 0.55, 1.2 px |
| the beads of these births | as 4.2 | 3.5 px, alpha 1, intensity 2.5 |

**Highlight**: `PersonInspector` sets `Highlighter.Set` with `SmvBonds.FamilyIds(person)`, which covers the person, the
spouses, the parents and the children; at most 16 ranges, 6 typical. Each member's family lines carry that member's id, so
the glow lights them. The beads carry the child's id, so selecting a child lights its birth.

### 6.5 What else follows the bent samples automatically

* the cut's dots (`SectionLayer`): a parent within 1.5 years of a birth at the cut year stands on or near the plane;
* `LifelinePicker` and `LandPick`: a hover at a knot hits one of three coincident lines. The existing tie-break is kept;
  the bead's id is the child's;
* `PersonFraming`.

---

## 7. Interfaces

**Needs:**

* From **terrain & views** (the owner of `EconomyPresets` / `EconomyViews` and the emphasis table):
  * key 9 and the `families` preset (6.3);
  * the "Bonds" row (6.2);
  * land groups at 0 in `families`;
  * `LandViewLayer` driving `SmvLayer.BondsAlpha`.
* From **people & wealth**: nothing new. They keep the lifeline ids on dots and stems.
* From **the mind**: nothing.

**Offers** (to all):

* `SmvPopulation.Bonds` (null in Why):
  * `Births` (Child, Mother, Father, Step, Y, Kind);
  * `BirthOf[person]`, `BirthsAsParent[person]`;
  * `Featured` (marriage index);
  * `FamilyIds(person, List<IdRange>)`.
* People part 10.4: selecting a stem lights its bead (same id), and selecting a couple's dots lights both lines and their
  ties.
* Mind part: `Births[].Kind` is available if their "a child born this year" event wants married vs unmarried.
* **Contract for everyone**: with the bonds on, `SampleY` no longer equals `SmvModel.Height(Value)` within about 1.5 years
  of a birth or about 3 years after an arrival. Never derive social market value from a sample position; read
  `SmvPerson` / the lives instead. Nobody does today.

---

## 8. Files

| File | Change |
| --- | --- |
| `Humans/Smv/SmvBonds.cs` | **new**: `SmvBondsOptions`, `SmvBirthKind`, `SmvBirth`, `SmvBonds` (3.2; ≈ 440 lines in the prototype, including the featured family and the summary) |
| `Humans/Smv/SmvShared.cs` | `BondsKey`; `SmvPopulation.Bonds` |
| `Humans/Smv/SmvGeometry.cs` | the `Bonds` field; glow and the child start in `Gather` / `Shape`; the arrival fade; `Marks`; `BuildBonds`, `BuildMidPlane`, `AddTie`, `AddArm`; counters |
| `Humans/Smv/SmvLayer.cs` | the order of §2, the log line, the marks mesh and material, `BondsAlpha`, the blurb and mid-plane label when the bonds are on |
| `Economy/Layers/EconomyLoaderLayer.cs` | publish the options |
| `Economy/EconomyPresets.cs`, `EconomyViews.cs` | `families` (key 9), the Bonds emphasis row (with the views' owner) |
| `Economy/Layers/LandViewLayer.cs` | set / reset `SmvLayer.BondsAlpha` |
| `Economy/UI/PersonLine.cs` → `FamilyLines.cs`, `PersonInspector.cs` | family drawing and family highlight (6.4); featured-family labels |
| `Docs/ECONOMY.md` | the People row and a "Couples and children" paragraph; the `families` view in the views table |

---

## 9. Performance budget (measured on the harness machine, **[proto]**)

| Item | Measured | Budget |
| --- | --- | --- |
| Bonds pass (worker, single thread) | 38-51 ms | ≤ 80 ms |
| `SmvLayer.Prepare` in the economy | 1,137-1,241 ms vs 879-949 ms without (the simulation itself varies 0.72-0.91 s run to run) | ≤ +300 ms |
| Fine / coarse points | 183,161 / 18,291 (+20%) | ≤ +25% |
| Marks mesh | 43,928 vertices, 1 draw call | ≤ 60K |
| Coarse additions | 848 beads, 542 arms (in the coarse mesh) | — |
| Memory | `Glow` byte[962,772] ≈ 0.94 MB + per-person indices ≈ 0.1 MB | ≤ 2 MB |
| Tick | one more `Apply` | ~0 |
| Family lines | rebuild ≤ 700 points per selection, main thread | ≤ 1 ms |
| Causality scene | nothing runs | 0 |

---

## 10. Determinism

* The pass is single-threaded, iterates in person-index order (birth order) and `MarriageLog` order, and draws no random
  numbers.
* The geometry's parallel workers only read the samples, and every worker's output is a contiguous birth-order range, as
  today.
* The checksum (3.2 step 8) and the counts must be identical between two runs with the clock pinned. **[proto]**: three
  runs gave checksum 735025915.254744, 3,090 births and 0 misses.

---

## 11. Verification with the harness

Set up: `$SP/agent-tools.sh <name> <tree>` and `export WHY_NOW=2026-10-01T12:00:00Z`.

1. **Log lines** (`--scene economy --render families`). Expected with the constants above **[proto]**:
   ```
   [Why] SmvLayer.Prepare … 4996 lines x 100,000 people (500 coarse), 681 steps from 1856.5, 962772 samples -> 183161 fine /
         18291 coarse points in 3 + 1 meshes, 0 parent links; 2078 marriages, 797 divorces, 4353 births with a mother, 530 immigrants
   [Why] SmvBonds … ms: 3090 births at the mid-plane from 1946 (2795 married parents, 295 unmarried, 0 mother only; 0 misses;
         1263 older births kept), 0 shared steps, max 10 births per parent, 8841 bridged samples; parents lift 1.44 (max 4.58)
         value units, reach 0.095 married / 0.230 unmarried x envelope; newborns drop 2.40; 401124 married samples squeezed
         x0.50; 530 arrivals from the edge; checksum 735025915.254744; marks: 3090 beads (848 coarse), 2401 ties, 542 coarse
         arms, 43928 vertices
   ```
   Checks for a `smvcheck.py`, which reads the log:
   * misses = 0;
   * married + unmarried + mother-only = births;
   * births + older = "births with a mother" (4,353);
   * arrivals = immigrants entering ≥ 1946;
   * beads = births;
   * parent links = 0;
   * fine points within +25% of 152,948;
   * the checksum is equal on two consecutive runs.
2. **The count of births drawn at the mid-plane** is the "births at the mid-plane" number with 0 misses: each one's three
   lines are verified within 1e-5 of the knot (3.2 step 6). A featured-family line is also logged:
   `[Why] SmvBonds featured marriage 846: wife 1752 husband 1646 from 1972.50; births 1973.00@y0.796 … 1990.25@y0.775`.
3. **Renders**: `families` (16:9 and `--size 1080x1920`), `overview` and `section`. Image checks on the `.base.png` and
   `.labels.json`:
   * the knot: the max channel in a 9×9 window at the anchor pixel of the "a child, 1985" label is ≥ 230 (255 with the
     bonds on and 160 with them off, **[proto]** `familiesnear`);
   * the labels "mother", "father", "married 1972" and "a child, 1985 …" are placed (present in `labels.json`), and no bench
     label ("TECH ·", "SERVICES ·") is placed in `families`;
   * the overview and the section differ from today only inside the road's population box. Mask the box by the anchors of
     "United States 1950 - now" and the cut frame; there are 0 differing pixels in the land half of the frame.
4. **The causality scene is unchanged**: `$SP/whycheck.sh <tree> <name>` → "14 presets, 0 differ: PASS" (**verified
   [proto]**). Also, the Why log's `SmvLayer.Prepare` line equals today's (152948 / 15295 / 4283 parent links) and no
   `SmvBonds` line is printed.
5. **Compile** with `$SP/cc-<name>/check.sh <tree>`, the Unity reference assemblies.

---

## 12. Rejected (tried or measured)

* **Stitches every 5 years of each marriage** (prototype b1-b5: 10,644-17,149 stitches, 126K vertices). In side views they
  are vertical hairlines everywhere (`b5-fam/families.png`); noise that hides the lines. Kept only at weddings and divorces,
  and as yearly ties for the *selected* family.
* **Veils between spouses**: about 600 concurrent couples' ribbons would turn the stream's centre into haze.
* **Couples at a shared height**: this would destroy the graph's meaning (height = each person's social market value).
* **Marking couples by colour**: gold already means control and capital in the economy.
* **Bead as a dash along time** (0.3 y): about 100 px strokes close up. The cross keeps a constant feel.
* **Arms for every coarse birth** (2,282 in the first prototype): too many fragments when zoomed out. Arms are kept for
  coarse children only (542); a coarse parent's other births show a bead.
* **Meetings before 1946**: the 1,263 older births are off the economy's road (the lens starts at 1946), and their mothers
  come only from the backfilled survivors of 1950. They keep today's drawing. (The "max 10 births per parent" in the log
  counts births from 1946 only: one simulated parent with 10 children, which comes from the simulation's fertility rule
  and is not changed here.)

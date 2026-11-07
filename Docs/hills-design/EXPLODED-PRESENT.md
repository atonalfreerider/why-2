# The present, taken apart: an exploded view of the crest

Design for the user's request of 6 Nov 2026 (verbatim):

> I still want the economic current cross-section displayed as a exploded view at the end of the timeline. So not
> following a cross-section, but instead exploded into a full-scale hillside economy that takes advantage of the 3D space.

The standing direction of 4 Nov also applies. Objects come first: firms, people and governments are drawn as objects,
with open-licensed thumbnails. Lines appear only to make a point, and faded or bloomed lines are used sparingly. Views
are holistic states, not graphs.

This is a design only. No repository file was edited and nothing was built or run. The frames read for it are
`Shots/dl1/economy-tour-{01,02,05,06,07,11,20}`, `why-tour-{64,65}` and `Shots/g3b2/economy-emphasis-crest-on.png`.

---

## 0. In one page

- **What the user means.**
  - *The economic current cross-section* is the wave's crest. Its cut face is the shown year's strata (bedrock, raw
    materials, manufacturing and infrastructure, services, tech) with the industries' ridges on top. Today that
    cross-section is only *followed*: the wave extrudes it backwards through time, rib by rib, and the crest is always
    seen as the leading edge of a time-extruded solid.
  - *The end of the timeline* is where the road of lives and the wave end, at the present. Section 2 gives the reading
    chosen and the alternative.
- **The proposal.** Pull the crest's cut face forward, out of the wave, into the empty space beyond the present. Today
  that space holds only the FUTURE arrow, and it was the old bowl's plaza. There the cut face is extruded into depth and
  taken apart like an engineering exploded drawing:
  - the state's bedrock drops to the bottom;
  - the four tiers lift apart into four terraces;
  - each industry's ridge swells into a hill whose footprint area is its value added;
  - the objects land on it. Firms are tokens on their hill. Households are figures, 1 per 1M households, standing on the
    slope of the hill that pays them, higher by income fifth. The state's officeholders and the households it pays stand
    on the bedrock's apron. The top 1% float in the gold pool between the summits, and the most-followed Americans float
    above that.
  - The wave stays behind it, faded, as the past it came out of.
- **Lines** appear in three places only:
  1. faint explosion guides that show where each part came from on the crest (they fade once it has opened);
  2. five *point* states, each lit on its own: a dollar's split, money up / supplies down in the gaps between terraces,
     the state's circuit, who owns the hills, and the frontier;
  3. on request, a selected hill's suppliers.
- **Relation to the stage and M1.** The exploded present *absorbs* M1 ("One year, all at once"):
  - It takes M1's key (I), its model (InstantModel, WP10, built), its questions (spec §1), its states (Money, Owners,
    People) and its panel.
  - It drops M1's flat towers, manifolds and 15-ribbon routing.
  - The stage (WP19, not built) stays for M2 and M3, with its origin moved past the exploded present.
- **From the universe.** The universe scene is unchanged. Its tour already hands over to the economy at the "Now"
  beacon. The economy tour then runs bridge → wave → layers → **explode**, and its last stop puts the year back into the
  wave. For a visitor arriving by Tab, the overview gains a door in front of the crest: "THE PRESENT, TAKEN APART (I)".
- **First slice (one day):** `PresentLayout` and `HillPresentLayer`. They draw the bedrock and four terraces, extruded
  from the crest's own profile, with the industries' hills, tier labels and the 25 firms as gold rings. They add the
  6-second explosion (transforms and one shader uniform only), the `present` preset on key I, and a smoke sequence that
  captures t = 0, 0.5 and 1 and logs the area and coincidence checks. Figures, tokens with images and the point lines
  come after (section 9.4).

---

## 1. What exists today

| Thing | Where | What it is now |
| --- | --- | --- |
| **The wave** | `Layers/HillWaveLayer.cs` (summary at its top: "the expanded cross-section of the end of the causality graph") | One rib per year, from 1947 to the crest. The crest is the shown year. Its *cut face* shows the strata standing on one another, and the front "breaks forward into the future". A FUTURE arrow lies beyond it: "FUTURE · after 2025, not yet made". |
| **The terrain** | `Land/HillLandscape.cs` | Land-local x runs across the wave (the hillside's foot at `FootX = 18`, `WidthNow = 190`, `HeightNow = 58`), y is height and z is time (`Z(year)`, the road's own position). It offers `LayerAt(year, tier, x)`, `SliceStrata(year, x)`, `TierBand(year, tier)`, `IndustryBand(year, i)`, `IndustryRidge(year, i)` (half width, peak), `Bump(i, x, z)`, `RungRadius` (class altitude), `Halo`, `FirmSeat`, `CloudCenter` and `Abroad`. The bedrock is at y = -18. Past the crest the terrain falls away over `FrontRun = 7` units, and nothing else is drawn there. |
| **Views on the crest** | `HillPresets.cs` | `landscape` (key 3, an oblique view at the crest), `foundation` (9) and `regimes` (pose (88, 24, crest), yaw 172, pitch 6, distance 300). `regimes` is the only view that already looks at the cut face *from the future*. That is frame 05: the strata slab's front face, with summits and rings. |
| **The section view** | `section` (key 2, "Families through time") | The road's lifelines, not the economy's cross-section. The wave's `Fan` part carries the road's lifelines out to the cohorts at the crest. |
| **The stage** | MODALITIES §4, WP19 | Not built (there is no `Economy/Stage/`). Its origin would be `Land().World(120, 0, 240)`, 240 units into the empty future, with cameras at z ≈ +570 to +660 looking back. It is absent-only: no wave in its views. |
| **M1 "the instant"** | MODALITIES §5, WP20 and WP24-WP26 | Not built. It draws two flat towers on the state's base: households by income on the left, tiers on the right. Its states are Income, Balance and People, its income statement has eight steps, and about 15 ribbons run through manifolds, all frontal and on a plane. |
| **InstantModel** | `Model/InstantModel.cs`, `Model/InstantModelLayer.cs` (WP10) | **Built.** For each year and cached, it gives: `InstantYear.Groups[6]`, `Tiers[4]`, `State`, `Dollar`, `Between`, `Stocks`, `Flows`, `Nodes`, the frontier, attention and `Facts`. Its checks are in `Problems()`. It is shared as `economy.instant`. |
| **The firms** | `EconomyData.Capture` (25: `Name`, `Ticker`, `Industry`, `MarketCap`, `Revenue`, `NetIncome`); `CompanyLives` (75 lives: `Value`, `Fate`, `Sells`, `Ended`; Jev's `Manufactured` and `FearSold`) | Drawn today as gold line rings on beams (HillActivityLayer) and as lifelines on the wave. |
| **The households** | `PlayerSet.Players` (about 116-128 cohorts: `Anchor` industry, `Rung`, `Group`, `Adults`, `WagesBy[25]`, `Wealth`, `Debt`) | 640 marker renderers today (objects.md §1.1). |
| **Images** | `Tools/images/fetch.py` (draft); `Docs/research/holistic/images.md`, `maps/objects.md` §4 | No tokens yet. The proposed design is one `Why/Thumb` billboard mesh per layer, with ids `firm:`, `person:`, `state:` and `figure:`, and attribution reachable in the app. |
| **The universe's end** | `Director/DirectorMode.cs` 361-366 (`ContinueIntoEconomy`), 640-690 (the end card); `Core/GraphRoot.cs` `OpenOtherScene` | The tour ends at the Now beacon ("Everything arrives here", frame `why-tour-65`). "Continue: the economy" loads the economy scene and starts its tour at `hills_bridge`, where the road fans into the wave (frame `economy-tour-01`). Tab switches scenes with a 0.35 s fade. |
| **A precedent** | `Docs/ECONOMY.md` (the retired bowl) | "Past the road's end it opens into a land": the cut's card flew to a plaza 10 units past the road's end, lay back into a staircase and curled into a bowl. The user then asked for hills rather than a bowl (BRIEF, 2 Oct), and the wave replaced the plaza. The new request asks again for the present *opened up* at the end, now as hills, at full scale and in 3D. |

**What the frames show.**
- In the overview and the landscape view (dl1 02 and 06), the crest is a thin white profile at the far left, about 80 px
  wide. The present is the smallest, most foreshortened thing on screen, while the past takes about 80% of the frame.
- In the regimes view (05), the cut face is legible from the future: a slab of strata with rings on its summits. It is
  still a 2D section seen face-on, and the rings stand in a line along one edge.
- In the people view (11), the objects (rings and markers) pile along the crest's profile line and the time ribs run
  through them. Nothing on screen has room in depth.

The request's "takes advantage of the 3D space" answers exactly this: give the present the volume the past now has.

---

## 2. Which "end of the timeline"

**Chosen: the space past the crest, in the economy scene.**
- The crest is where the economy's time axis ends. The road of lives (the universe's human branch, continued) ends
  there too, so this is where "now" is in both scenes.
- The space beyond it is empty, and the scene already calls it the future. It is where the old bowl stood and where the
  stage would have stood.
- Putting the exploded present there keeps the wave's meaning: the past lies behind, and the present is taken apart in
  front of it. "Not following a cross-section" then reads as *not extruded along time*: the present gets its own volume
  instead of being only the front edge of the wake.
- The economy tour reaches it as the climax of the wave's act. The tour's last stop reverses it, putting the year back
  into the wave, so the exploded present is also, literally, where the walkthrough's timeline ends.

**Alternative: the end of the universe's timeline.**
- Under this reading, the Now beacon at the tip of the human branch (frames 64 and 65) would burst into the US economy
  inside the universe scene.
- It is rejected as the primary reading, for three reasons:
  - the handoff rule is "the universe scene stays as it is";
  - the economy's data, models, layers and labels live in the economy scene;
  - the branch tip is a sub-pixel point among about 200 nations' streams, so the US economy would have to fly out of
    one band among many.
- Part of it is kept (section 7): the economy scene's bridge stop already continues the road from the universe's end,
  and the explosion follows the road to the crest. The user experiences "the end of the timeline opens up" without the
  universe scene changing.
- If the user meant the universe's end literally, a second option exists: a universe-scene card link, "The present,
  taken apart ▸", that opens the economy directly on the explosion. It needs the user's approval because it touches the
  universe's end card.

**A third, minor reading:** the end of the *replay* (R), when the crest reaches the latest year. It is covered: when the
replay ends, the door in front of the crest pulses (section 6.4). The explosion is not triggered automatically.

---

## 3. Composition

### 3.1 The present frame

`PresentFrame` holds pure math and is computed from `EconomyStage.Land()`, so it mirrors with the land if S07's
`MirrorLand` ships. The explosion has to come out of the cut face, so it follows the face's handedness.

- **Origin:** land-local (0, 0, C + Gap0), where C = `HillLandscape.Z(year)` (the crest) and Gap0 = 40.
- **Axes:** the land's own. x runs across, from the road side to the summit side; y is up; z points away from the
  crest, into the future.
- **Mapping:** `World(x, y, d) = Land().World(x, y, C + Gap0 + d)`, for d from 0 to D.

| Constant | Value | Why |
| --- | --- | --- |
| S (scale over the crest's own x and y) | 2.0 | "Full scale": the crest's 190 × 58 profile becomes 380 × 116, twice the wave's width, so the present outweighs any single rib. |
| D (depth of the extrusion) | 200 | Each terrace becomes a plateau with room for hills, figures and walking cameras. Area stays true because every terrace has the same depth (below). |
| Gap0 (crest to the present's near edge) | 40 | The crest and its cut face stay visible behind, as the origin, and the guides have room to read. |
| Gx (x gap between neighbouring terraces) | 14 | A slot you can see the flows through (state `money`). |
| Gy (extra lift per tier) | 16 | The exploded-drawing separation: tier k is lifted k·Gy above its place in the stack. |
| Gb (the bedrock's drop) | 24 | The state sits visibly *under* everything, with a gap where taxes fall and benefits rise. |
| Apron | 60 | The bedrock reaches 60 units past the terraces toward the viewer (d from -60 to D). This is the "valley floor" of the exploded present. |

**Area is true.** Every terrace is D deep, and its x width is S times its tier band on the crest (`TierBand`, which is
proportional to value added). A terrace's footprint is therefore proportional to its tier's value added. The same holds
for each industry inside it (`IndustryBand`). The printed scale is "▢ 1,000 square units = $X B a year". It is computed
from the crest's `CrestValueWidth` and the year's private value added. For 2025 it is roughly 1,000 u² ≈ $0.36T: about
380 × 200 units for $27.3T, before the risers are subtracted, so the runtime value is the one printed.

**Heights are assumed, as on the wave.** The terraces' heights come from the crest's strata, which are an assumed order
of dependence, and the legend says so ("height: assumed order of dependence, not measured"). Measured quantities are
carried by area (value added), by counts (figures) and by token size (market value).

### 3.2 The parts, bottom to top

| Part | Geometry (present frame) | What it shows |
| --- | --- | --- |
| **The state (bedrock)** | A slab spanning x from -20 to 420 and d from -60 to D, with its top at y = -Gb + S·(-18) ≈ -60. Its thickness is `State.Outlays` × k (k = 4 units per $1T, so 2025 ≈ 43). Its front face (d = -60, toward the viewer) is layered into strata, top to bottom: `OwnWork`, `Transfers` (benefits), `PurchasesNet`, `Interest`, `OtherOutlays`. A light line marks `Receipts`, and the band below it (`Borrowing`) is hatched. | The state underlies everything, with the slabs of what it spends. It is the same structure as the regimes view's slab (frame 05), lying flat. |
| **The apron (valley floor)** | The bedrock's exposed top in front of the terraces, d from -60 to 0. | Where the households the state pays and those with no paying hill stand, and where the year's officeholders stand on the front edge. |
| **Terrace 1 (raw)** | x from S·T0[1] to S·T1[1] (≈ 19 wide), y = the strata top × S + 1·Gy, d from 0 to D. | Oil, gas, metals, farms. |
| **Terrace 2 (make)** | ≈ 119 wide, + Gx, lifted 2·Gy. | Manufacturing and infrastructure. |
| **Terrace 3 (services)** | ≈ 207 wide, + 2Gx, lifted 3·Gy. | The widest by far. |
| **Terrace 4 (tech)** | ≈ 36 wide, + 3Gx, lifted 4·Gy. This is the top step, at the far right. | It is narrow (9.4% of output) but holds the largest tokens: the point of the comparison strip, made by objects. |
| **Hills** | On each terrace, each industry's x band carries a mound. Across x it uses the crest's ridge profile (`IndustryRidge` half width and peak, × S). Along d it has a cosine falloff centred at d = 0.55·D, so each ridge on the crest becomes a hill and not a long rib. Industries with less than 0.5% of value added stay as flat ground in their band, named on hover. | Industries as hills. Their footprints are area-true, and their relief is the wave's (assumed). |
| **The capital pool** | A soft gold veil at y = (the summit of tech + 40), centred over services and tech, as `HillLandscape.CloudCenter` places it relative to the crest. | The top 1%'s diversified claims: "pools of the ultra wealthy at cloud level between the hills" (BRIEF, 2 Oct). |
| **The attention cloud** | Above the pool, y + 40 more, x from the social graph's layout. | The most-followed Americans (2025 only). |
| **ABROAD** | An outlined doorway past tech's far edge, at x ≈ 440 and d ≈ 0.5·D. | The rest of the world. In the money and owners states it is where imports and owners abroad go, and it is a door to M2 (G) once M2 exists. |
| **The frontier** | A gold plume rising from tech's far edge (d = D), leaning further into the future. | What tech pays into what comes next. |
| **The wave, behind** | Unchanged, at context (desktop) or as the crest outline only (XR). | The past this present came out of. |

Seen from the default camera (section 4), the whole composition reads as a hillside that has been *pulled apart*. It
climbs from the near-left (raw, low) to the far-right (tech, high), as the cut face climbs. It stands on a floor (the
state), with the money's clouds above and the crest it came from visible behind.

---

## 4. States and camera poses

Poses use HillPresets' convention: a land-local target, yaw relative to the land's forward (180 looks back from the
future at the crest), pitch in degrees and distance in units. In the poses, `C` is the crest's z and `P` = C + Gap0 is
the present's near edge. In XR every pose becomes a diorama (README "Meta Quest"): the target sits at arm's length and
the world is scaled to it.

### 4.1 Explosion phases (one choreography, about 6.4 s, reversible)

Everything is built before the first frame of the animation, in one `HillRebuilds` slot while the camera flies. During
the animation only root transforms and one shader uniform per root (`_Rise`) change. No mesh is rebuilt mid-flight, as
spec §9.2 requires.

| t (s) | Phase | What moves | Camera |
| --- | --- | --- | --- |
| 0.0-1.0 | **Face** | The wave's keys ease to context (0.12). The crest's cut face lights. The FUTURE arrow fades. | Flies from the current pose to `present.face`: target (110, 24, C), yaw 172, pitch 6, distance 300 (the regimes pose). |
| 1.0-2.0 | **Cut** | A copy of the cut face (five slabs, built from `SliceStrata` and `LayerAt` at the crest year, at scale 1 and depth 0.5) slides forward from z = C to z = P. | It holds, then begins to rise. |
| 2.0-3.4 | **Grow** | The slabs scale to S in x and y about the foot, and to D in depth: their localScale moves from (1, 1, 0.0025) to (S, S, 1). The present becomes full scale. | It pulls back and up to `present.rise`: target (200, 40, P + 100), yaw 160, pitch 18, distance 520. |
| 3.4-4.6 | **Separate** | Each tier's root translates by (k·Gx, k·Gy, 0). The bedrock drops by Gb and reveals its apron. The guides (dashed, alpha 0.35) join each terrace's four near corners to the matching corners of its band on the crest face. | It holds. |
| 4.6-5.4 | **Rise** | The hills rise: `_Rise` goes from 0 to 1 on the terraces' surface material, scaling relief in the vertex shader. The guides fade to 0.12. | It eases to `present` (below). |
| 5.4-6.4 | **Arrive** | Objects arrive by rank: firms first, largest first; then figures, flying out of the road's crest point along the wave's existing fan (a nod to the lives the universe delivered); then the state's officeholders; then the pool and the cloud. A token's per-vertex rank is compared with `_Reveal` from 0 to 1, so it costs one uniform per mesh. | It holds. The title card appears: "The present, taken apart · ⟨year⟩". |

Reverse ("Back into the wave", 2.0 s) runs the phases backwards at three times the speed and ends on the overview.

### 4.2 Resting states (view variants of `present`)

Each state has one subject at 1, everything else at 0.12 or absent, and one printed scale. Where a state has steps,
cues light them one at a time.

| State (variant) | Subject, at 1 | Lines | Pose: target, yaw, pitch, distance | The sentence it lets a viewer say (M1's §1 questions) |
| --- | --- | --- | --- | --- |
| `present` (**default**, "taken apart") | terraces, hills, firm tokens, figures, bedrock, labels | the guides only, at 0.12 | (200, 40, P + 100), 160, 22, 520 | "Services is the biggest ground; tech is small ground with the biggest firms; everyone stands on the state." |
| `present-assembled` | the cut face on the crest | none | `present.face` | (the starting point; also the reverse's end before the overview) |
| `present-people` | figures, and their altitude rings on each hill | none | (180, 10, P + 40), 165, 14, 380 | "Which fifth stands where: the low fifths on the valley floor and the low slopes, the top on the summits and in the pool." |
| `present-money` (steps: `dollar`, `between`, `state`, `abroad`) | one point line at a time (section 5) | yes, one family per step | `dollar`: (120, 20, P + 30), 170, 12, 260; `between`: (230, 30, P + 60), 150, 8, 340 (looking into the gaps); `state`: (200, -40, P - 30), 175, 4, 420; `abroad`: (360, 40, P + 100), 140, 14, 300 | "How a household dollar splits"; "money goes up and supplies come down"; "where the state takes in and pays out". |
| `present-owners` (M1's Balance) | the pool, firm tokens sized by market value, the figures' wealth rings (`NetWorth`), and the threads | the threads (section 5) | (240, 90, P + 80), 155, 26, 480 | "Which classes own the capital; the top tier holds more market value than output." |
| `present-attention` | the cloud's faces, the five capital holders' faces on their firms | the five tethers (dotted) | (220, 160, P + 80), 165, -6, 300 | "Attention floats above money; five of the most-followed own the hills." |
| `present-frontier` | the tech terrace and the plume | the plume | (420, 70, P + D), 130, 16, 220 | "Tech pays $594B a year into what comes next." |
| `present-walk` (desktop: press W A S D in `present`; XR: "stand here") | the figure picked, or by default a 40-60 figure on services | none | a first-person pose 2 units above that figure, looking up the hill toward the summit and the pool | "What the economy looks like from where a typical household stands." |

Close-ups of single terraces come from clicking a terrace label (a pose fitted to its bounds, so a terrace fills about
70% of the free rectangle). They are not catalog presets.

**Portrait (V).** The terraces' staircase is wide, so portrait uses the yaw 200 mirror pose, which looks up the
staircase from the raw end. The money and owners states move the steps' numbers to the bottom sheet.

---

## 5. Lines, and the point each makes

The rule follows the 4 Nov direction. In the default state the only lines are the guides at 0.12. Every other line
belongs to one state or step, makes one point and carries that point as its label.

| Line | State or step | Geometry | The point (printed on it, at most 12 words) | Data |
| --- | --- | --- | --- | --- |
| **Explosion guides** | the explosion; then 0.12; at 0.6 on a terrace's hover | Dashed and steel-grey: four per terrace and four for the bedrock, from the near corners to the cut face on the crest. | "taken from the crest: ⟨year⟩'s cut face" (once, on the bedrock's guide) | `TierBand`, `LayerAt` |
| **A dollar's split** | money · `dollar` | One animated coin path. It starts at a 40-60 figure on a services hill, runs to that hill's market (`HillLandscape.Market`) and splits into five coins: pay goes back to figures on several hills, owners up to the pool, upkeep into the hill, imports to ABROAD, taxes down to the bedrock. It leaves no persistent ribbon, and the 100¢ bar in the panel lights at the end. | "of each household dollar: 41¢ pay · 27¢ owners · 14¢ upkeep · 11¢ imports · 7¢ taxes" | `InstantDollar.Cents[5]`, `InstantDollar.Parts` |
| **Up and down in the gaps** | money · `between` | Two vertical arrows in each of the three gaps between terraces, widths on the flow scale (5 units per $1T a year, M1's scale): money up and supplies down. This is the one place where the explosion's gaps are *used*: the flow is drawn where the separation is. | "money between industries: 37% up · 22% down · 41% within (2024)" | `InstantBetween.BoundaryUp[3]`, `BoundaryDown[3]`, `Up`, `Down`, `Within` |
| **The state's circuit** | money · `state` | Two downpipes, one from the figures' apron (households' taxes) and one from under the terraces (industries' taxes), and two risers, benefits to the apron and purchases into the terraces. All four cross the bedrock gap Gb. The borrowing band is hatched on the slab's face. | "takes in $⟨Receipts⟩T · pays out $⟨Outlays⟩T · borrows $⟨Borrowing⟩T" | `InstantState.TaxesHouseholds`, `TaxesIndustries`, `Benefits`, `Purchases`, `Receipts`, `Outlays`, `Borrowing` |
| **Abroad** | money · `abroad` | One conduit from the terraces to the ABROAD door (imports) and one back (exports). | "imports $⟨m⟩T · exports $⟨x⟩T (⟨year⟩)" | `InstantYear.Imports`, `Exports` |
| **Who owns the hills** | owners | Gold threads, 1 per $1T of listed claims. They run from the firm tokens up to the pool and from there down to the figures' wealth rings by fifth, with ⌊18.4%⌋ to ABROAD. They are bundled: a firm's threads leave as one strand. | "1 gold thread = $1T of claims on US companies · ≈ every household holds the market's mix" | `InstantStocks.Threads`, `ThreadsAbroad`, `InstantGroup.Threads` |
| **Tethers** | attention | Five dotted white lines from the five capital holders' faces in the cloud down to their firm tokens. | "where attention and capital meet (J)" | `InstantYear.CapitalHolders` (Jev's matches) |
| **The frontier** | frontier | A gold plume of width $ × 5, alpha fading upward. | "$⟨594⟩B a year into what comes next (10 largest tech firms)" | `FrontierCapex`, `FrontierResearch`, `FrontierFirms` |
| **A hill's suppliers** | any state, on request ("What does it buy?" on a selected hill) | Faint lines from the hill's foot down to the hills it buys from, routed *under* the terraces through the gaps. Width is on the flow scale and only the top five are shown. | "⟨industry⟩ buys most from ⟨a⟩, ⟨b⟩, ⟨c⟩ (2024)" | `RootsModel` (the industry input-output table the wave's `roots` view draws) |

**No lines anywhere else.** In particular:
- Households are not linked to their hills: where a figure stands says who pays it (BRIEF requirement 7).
- There are no rivers of spending: the dollar's coin path makes that point once.
- Firms carry no lifelines: the wave behind carries them.

**Bloom.** Only the hovered or selected object's ring and the coin in flight may exceed 1.0. Everything else stays LDR
(decision 36).

---

## 6. Objects and their data bindings

Every object is a token in the `Why/Thumb` billboard mesh (objects.md §4.3a), or a line glyph until the image pipeline
lands (section 9.4). Ring styles follow the mark code: solid means measured, dotted means modelled (≈), and an outline
with J means judged by Jev.

### 6.1 The ground

| Object | Geometry binding | Label (LabelSystem) | Hover card |
| --- | --- | --- | --- |
| Terrace k | width = S·(`TierBand(year, k).y` - `.x`); lift k·Gy | "SERVICES · $⟨Tiers[2].ValueAdded⟩T · ⟨Jobs⟩M jobs" on its near face | `InstantTier`: `ValueAdded`, `Jobs`, `Pay`, `Taxes`, `Upkeep`, `Owners`, `OutputShare`, `JobsShare`, `MarketValueShare` ("at least"), `ProfitsShare` (or "no data yet") |
| Industry hill i | band = S·`IndustryBand(year, i)`; relief S·`IndustryRidge(year, i).y` × falloff | the name on the summit for the top ones per terrace by value added (with a label budget); the rest on hover | the industry's value added (`EconomyData.Industries[i].ValueAdded.GrowthAt(year)`), its share of the tier, its stored firms, and "buys most from…" (button) |
| Bedrock strata | thicknesses = `State.OwnWork`, `Transfers`, `PurchasesNet`, `Interest`, `OtherOutlays` × k; the receipts line at `Receipts` | "THE STATE · all levels of government · spends $⟨Outlays⟩T · takes in $⟨Receipts⟩T · borrows $⟨Borrowing⟩T" | each stratum's $ with its NIPA line, and `Jobs` (19.8M full-time equivalents) |

### 6.2 Firms

| What | Binding | Token |
| --- | --- | --- |
| The 25 stored firms | `EconomyData.Capture[j]`: `Industry` → the hill; `MarketCap` → the token's size (√, clamped from 22 to 64 px on screen and from 1.2° to 3° in XR) and a stem of height 2 + 2.4·log10(1 + cap), as the wave's beams use; `NetIncome` → the ring's width; `Revenue` on the card | A logo, or a monogram tile when the logo is a wide wordmark (objects.md §5.3), with a solid owners-gold ring. They sit on the hill's summit side facing the viewer, spaced by `FirmSeat` logic recomputed on the hill's front slope. |
| The other living firms of the year | `CompanyLives.Lives` where `Alive` (or `Ended` > year) and `ValueAt(year)` > 0, and that are not among the 25 | A small monogram at 0.6 alpha, a dotted ring if the value is approximate, and its name on hover. They are capped at 50. |
| Their selling mode | `Life.Manufactured`, `FearSold` (Jev, J) | A thin inner arc in rose (want) or ice (fear), shown only in `present-money` and on the card. Colour keeps one meaning per state. |
| Owners at their firm | people.json `firms` (Musk: tesla and spacex; Zuckerberg; Bezos; Gates; Cook), resolved through `InstantYear.CapitalHolders` | A face token on the firm's ring (approved real people only, and only in the years the source covers) |

### 6.3 Households (figures)

- **One figure is 1M households.** The counts come from `InstantGroup.Households` (DFA), about 27 per fifth, 25.7 for
  80-99 and 1.35 for the top 1%. The total is about 135 figures.
- **Each figure's role**, and so where it stands, comes from `InstantGroup.Dots[4]`, which is already computed as 1 per
  1M households by role, `Roles` = pay, owning, the state, none:

| Role | Where the figure stands | Altitude |
| --- | --- | --- |
| **pay** | On a hill. The group's pay dots are spread over industries in proportion to that fifth's wages by industry, summed from `PlayerSet.Players` (`WagesBy[25]` of the players whose income per household ranks in the fifth). This is modelled (≈) and the dotted ring says so. | `HillLandscape.RungRadius[fifth]` on the hill's viewer-facing slope: the bottom fifth at the foot, the 80-99 group near the summit. |
| **owning** | On the summit side, beside the firm tokens of the hill where most of the group's `Owning` income is earned. Top-1% owning figures stand *in the pool*. | the summit or the cloud |
| **the state** | On the apron, in front of the bedrock's strata (public payroll, Social Security, the safety net). | the valley floor |
| **none** | On the apron's near edge (students, homemakers, the out of work). | the lowest |

- **Token:** a generic figure (OpenMoji or Twemoji, per images.md §2.6) on a people-blue dotted ring. Colour carries one
  meaning, so the fifth is shown by *altitude*, never by hue, and `present-people` prints the rung's name on each ring:
  "40-60% · 26.9M households".
- **Card:** `InstantGroup.Name`, `Households`, `GrossIncome`, `SourceShares` (pay · owning · the state · other ·
  borrowed), `UseShares`, `NetWorth`, `Saving`, `RoleShares` with `RoleN` ("≈ simulated, n = ⟨RoleN⟩").
- **In `present-owners`:** each figure carries a gold wealth ring with area ∝ the group's `NetWorth` / `Households`, and a
  hatched rose ring for `Liabilities`. This is M1's balance state as objects.

### 6.4 The state's people, attention and doors

| Object | Binding |
| --- | --- |
| The year's president and legislators (the seats) | government.json `actors` in office in the year, and people.json `gov`. Faces with steel rings stand along the bedrock's front edge on the apron (presidents' portraits are public domain, per objects.md §0.5). |
| The attention cloud | people.json, the US people with at least 10M followers (`InstantYear.CloudPeople`). The top 12 (`MostFollowed`) are faces with white rings, the rest points. The cloud is drawn in 2025 only; other years show the outline "attention: one 2025 snapshot". |
| The capital pool | `InstantGroup` top 1%: `NetWorth` sets the veil's volume (area ∝ $), and its 1-2 figures stand in it. |
| ABROAD | `Imports`, `Exports`, `ThreadsAbroad` and `Stocks.PublicDebtAbroad` on its card. It is a door to M2 (G) once built. |
| The door on the overview (S06's "STOP TIME" door, renamed) | "■ THE PRESENT, TAKEN APART · ⟨year⟩ (I)", in front of the crest. It pulses when the replay reaches the last year. |

---

## 7. How the universe's end leads in

1. **The universe tour (unchanged).** Stop 64 ("The end of the branch") and stop 65 ("Everything arrives here", at the
   Now beacon) lead to the end card, and "Continue: the economy" loads the economy scene (`ContinueIntoEconomy`).
2. **Economy stop 1, `hills_bridge` (unchanged pose).** The United States stream continues as the road and fans into the
   wave at the crest. Its card already says "At now the road of lives fans out into the economy's wave."
3. **Stops 2-3 (wave, layers).** These are as today: the past, then the crest's face read bottom-up.
4. **New stop, `present_explode`,** after the layers stop. "Now take the present apart. Pull this year's cut face out of
   the wave, give it the room the past has, and stand every part where it belongs." The six-second choreography plays
   with the card.
   - In the Arrive phase, the figures leave the road's crest point along the wave's fan. This links them back: these
     figures are the lives that the universe's branch delivered to now.
5. **M1's tour stops 4-13** (instant_stop … instant_all) are re-posed onto the present's states. They keep their texts'
   facts (ModelFacts `instant.*`, already published by WP10), with "floor" reworded to "terrace" or "hill", and
   "tower" reworded to "the hillside".
6. **The last stop, `hills_return`:** "Put the year back into the wave". This is the reverse choreography, ending on the
   overview. It is the same idea spec §11 #21 had.

**Arrival by Tab or by the hand menu's "the other scene".** The economy opens on the overview, as now. The door in front
of the crest and key I take the visitor in.

**Optional, needs the user's approval because it touches the universe scene's end card:** a second button on the
universe end card, "Straight to the present ▸". It would open the economy with a one-shot flag that runs the bridge pose
for 2 s and then the explosion, skipping the wave's stops.

---

## 8. Relation to the stage and M1: absorb (recommended), host or replace

| Option | What it means | For | Against |
| --- | --- | --- | --- |
| **Absorb (recommended)** | The exploded present *is* view `instant` (key I, title "The present, taken apart", short title "Present"). It keeps InstantModel, M1's five questions (§1), M1's scales where they are $ flows (5 units per $1T a year), the states (Income → `present-money`, Balance → `present-owners`, People → `present-people`), the steps as cues, the InstantPanel design (§10: "the year in five lines", "Follow a dollar", MEASURED / JUDGED / MODELLED / CHOICES / NO DATA), the year coverage rules (§5.9) and the flip-book replay. It drops the two flat towers, the manifolds, `InstantRoutes` (port stacking and 15 ribbons) and the scene's comparison strip, which moves to the panel as a 4 × 4 table. | It answers the user's request and the 4 Nov direction (objects, not ribbons on a plane). The built model is reused whole, and nothing built is thrown away: WP19-WP26 are unbuilt. The explosion *is* the "stop time" transition spec §9.2 wanted (quads cut from the crest face into the towers), now without needing S07's mirror. | Some of M1's precision is lost. A tower floor's height read off a common baseline compares six groups exactly, whereas figures on hills compare counts and positions. The panel's numbers carry the exact comparison. |
| **Host** | The exploded present is the new default, and M1's towers remain as a `flat` state: terraces slide into tower floors and figures stack into class floors. | It keeps both a holistic view and a precise one. | It doubles the build, and the morph is hard: area-true terraces become height-true floors. |
| **Replace the stage altogether** | M2 and M3 also leave the stage and stand in the exploded space. | One space. | Out of scope. M2 is a row of 14 economies, not the US present, and M3 is a time corridor. |

**Changes the absorb option makes to the plan.**

| Package | Change |
| --- | --- |
| WP19 | `StageFrame`'s origin moves from z +240 to z +520 so that M2 and M3 stand beyond the present. The stage's cameras sit at about +850 to +940, and stage views stay absent-only. |
| WP20 | Replaced by E1 + E2 (below). |
| WP24 | Reduced to E4: four point lines and the coin path, with no manifold routing. |
| WP25 | Kept in part: the frontier, the attention cloud builder and the tethers. The boundary arrows move into the gaps. |
| WP26 | Becomes the owners and people states as objects (E3/E4). |
| WP33 | ("stop time") Becomes the explosion choreography (E2). |
| WP34 | (the panel) Kept. |
| WP36 | Stops 4-13 are re-posed (section 7). |
| Decision 28's M1 cap (≤ 12 draws) | Applies to `present` (section 9.3). |

---

## 9. Build sketch

### 9.1 Files and classes

| File | Kind | Content |
| --- | --- | --- |
| `Assets/Scripts/Economy/Land/PresentLayout.cs` | pure C#, any thread | `PresentFrame` (origin, `World(x, y, d)`, `Local`); the constants S, D, Gap0, Gx, Gy, Gb and Apron; `Terrace(k)` (x0, x1, base y, top y, offset at t = 1); `Hill(i)` (band, peak, centre d); `Profile(k, x)` (the crest's strata at x, from `HillLandscape.LayerAt`); `AreaShare(k)`; `RungPoint(hill, fifth, slot)`; `ApronPoint(role, slot)`; `Explode(t)` (each root's translation and scale at phase time t, as pure functions so the smoke test can check them). |
| `Assets/Scripts/Economy/Layers/HillPresentLayer.cs` | GraphLayer, `[GraphScenes(EconomyLayouts.HillsScene)]`, Order about 58 (after the wave) | It builds the bedrock and four terrace meshes (`SurfaceMeshBuilder` grids of 128 × 48 per terrace with the falloff relief in vertex y; `_Rise` scales relief), the guides (one `LineMeshBuilder` mesh) and the labels (`HillLabels`). It also builds the firm glyphs: gold rings for the first slice, `Why/Thumb` tokens later. It runs the choreography in `Tick` and registers the variants, the emphasis keys `present.*` and the anchors `present:bedrock`, `present:tiers`, `present:tech` and `present:people`. |
| `Assets/Scripts/Economy/Layers/PresentPeople.cs` | helper | Figure placement: `InstantGroup.Dots` × role, with the pay dots spread by `PlayerSet` wages by industry and fifth (≈). It builds one token mesh and the wealth rings for the owners state. |
| `Assets/Scripts/Economy/Layers/PresentLines.cs` | helper | The point lines (section 5): the coin path, the gap arrows, the state's pipes, the abroad conduits, the threads, the tethers, the plume and suppliers on request. Each family is one mesh with its own emphasis key. |
| `Assets/Scripts/Economy/UI/PresentPanel.cs` | panel (or `InstantPanel` per WP34, renamed) | The state buttons, the step chips, "the year in five lines", the 100¢ bar, the 4 × 4 output / jobs / profits / market-value table, an assembled ↔ exploded slider (desktop and XR), and "Stand here". |
| `Assets/Scripts/Economy/HillPresets.cs` (change) | | It adds `present` (key I) and `present.face`, its short title "Present", the chapter "The wave", and the door on the overview. |
| `Assets/Scripts/Economy/Emphasis/EmphasisTable.cs` (change) | | It adds a `present` column: the wave's rows at 0.12 on desktop and 0 except `wave.crest` and `wave.face` in XR, and the `present.*` rows. |
| `Assets/Resources/Data/Walkthrough/economy_tour.json` and `economy_narration.json` (change) | | It adds `present_explode`, and re-poses or adds the instant stops (no voicing without approval). |
| `Assets/Scripts/Core/SmokeChecks` registration (in HillPresentLayer) | | The `present` sequence (section 10). |

All new `.cs` files need `.meta` files.

### 9.2 Effort

| Package | Content | Effort | Depends on |
| --- | --- | --- | --- |
| E1 | PresentLayout and its checks | small (half a day) | HillLandscape |
| E2 | HillPresentLayer: terrain, explosion, labels, firm glyphs, preset, key, door | medium (1-2 days) | E1 |
| E3 | Figures and tokens: households, firms, faces, cloud, pool | medium | E1, InstantModel; image pipeline for images (glyphs until then) |
| E4 | The point lines and states (money, owners, attention, frontier) | medium | E2, E3 |
| E5 | Panel, walk-in, hover and select, suppliers on request, the replay flip-book | medium | E2-E4; the WP34 design |
| E6 | Tour stops, portrait and XR framing, the Quest profile | medium | E2-E5 |

**First shippable slice (one day): E1 plus the core of E2.**

- **Draws:** the bedrock slab with its five strata on the front face and the receipts line (no hatching yet); the four
  terraces extruded from the crest profile with the hills' relief and `_Rise`; the guides; tier labels with $ and jobs;
  and the 25 firms as the existing gold-ring glyph (HillActivityLayer's halo geometry reused) on their hills, sized by
  `MarketCap`.
- **Does:** the 6.4 s choreography forward and in reverse, `present` and `present.face` in the catalog, key I in, and
  Shift+I or the door back. It registers the `present` column with every other key at 0.12 (desktop).
- **Not yet:** figures, images, point lines, the panel, XR specifics beyond the diorama default, and the tour.
- **Why this slice:** it shows the user the exploded view itself, the request, in one build. Every later package only
  adds objects or states onto ground that is already right.

### 9.3 Quest performance

| Item | Budget |
| --- | --- |
| Draw calls in `present` (XR) | ≤ 14: bedrock 1, terraces 4, guides 1, firm tokens 1, figure tokens 1, faces and cloud 1, pool 1, the lit point family 1, crest outline 1, labels (SpriteRenderers count only when they carry images). The wave's other renderers are absent in XR. |
| Vertices | Terraces 4 × 6.1k plus bedrock 3k ≈ 28k. Tokens about 260 quads (1k vertices). Lines below 5k. |
| Labels | ≤ 60 in XR (the role budget). Industry names only for the top 2 per terrace; others on hover. |
| Animation | Transforms and three uniforms (`_Rise`, `_Reveal`, `_Alpha`), with no allocation per frame. Choreography frames are ≤ 13.9 ms on device (72 Hz). Desktop's gate is ≤ 50 ms. |
| Build | One `HillRebuilds` slot on a worker (the terrain and tokens of one year) in ≤ 40 ms, done while the camera flies (phase Face). A year change in `present` rebuilds in a slot and crossfades. |
| XR interaction | The assembled ↔ exploded slider is on the hand menu's view page. Two-hand pinch keeps meaning "scale the world", so there is no new gesture. "Stand here" places the diorama at life scale around a figure: about 1 unit = 1 m, so a hill is tens of metres. |

---

## 10. Acceptance a smoke test can measure

Run with `EXTRA="-smoketour -smokereplay -smokeextra present"`.

| # | Check | Measured how |
| --- | --- | --- |
| A1 | **Area is true.** Each terrace's footprint share equals `InstantTier.OutputShare` within 1%, and each hill's share of its terrace equals its share of the tier's value added within 1%. | The log prints `[Why] present 2025: tiers 4, hills N, area max err x.x%`. |
| A2 | **It comes out of the crest.** At t = 0, every slab's near-face vertices lie on the crest's cut face (`HillLandscape.LayerAt(year, k, x)` at z = C) within 0.5 units. | `PresentLayout.Explode(0)` against the profile, logged: `t0 coincidence max 0.xx`. |
| A3 | **It is apart.** At t = 1, the terraces' and bedrock's bounding boxes are pairwise disjoint, with gaps ≥ Gx and ≥ Gy in their axes. | Logged per pair. |
| A4 | **Frames.** The run captures `economy-present-t000.png`, `-t050.png` and `-t100.png` (the sequence holds t at those values), plus `economy-view-present.png`, `-present-people`, `-present-money-{dollar, between, state, abroad}`, `-present-owners`, `-present-attention` and `-present-frontier`. | `Shots/TAG`. The pixel gate (pixdiff) compares the wave's views with fresh baselines and allows no change beyond noise + 0.5% in any view other than the overview (which gains the door). |
| A5 | **Frame time.** No frame over 50 ms during the explosion and the reverse on desktop. | Logged as `present choreography max frame NN ms`. |
| A6 | **Draw calls.** Active renderers in `present` ≤ 16 on desktop (the wave at context excluded) and ≤ 14 in `-xr`. | `[Why] present: N renderers active` |
| A7 | **Objects outnumber lines.** In `present` (default), line renderers active = 1 (the guides) at alpha ≤ 0.12; in each `present-money` step, exactly one point-line family is at 1. | Emphasis values logged per renderer. |
| A8 | **Firms.** 25 firm glyphs, each inside its hill's footprint, with token or ring size monotone in `MarketCap`. | Logged. |
| A9 | **Figures** (E3). The count equals Σ round(`Dots`) ± 1 per group, each pay figure stands on a hill of nonzero modelled wages for its fifth, and the altitude order bottom fifth < … < 80-99 holds on every hill that has more than one fifth. | Logged. |
| A10 | **Labels.** Overlaps 0, minimum 12 px (1° in XR), and the tier labels print the same $ as the facts `instant.tier.*`. | The label system's existing check. |
| A11 | **Facts.** Every number on the present (labels, captions, point-line texts) traces to a `ModelFacts` key. | `Tools/check-facts.py`. |
| A12 | **The universe is unchanged.** `why-tour-*` frames pass the pixel gate against the baselines. | pixdiff |
| A13 | **Other years.** `economy-view-present-1972.png` shows that year's crest exploded, an undivided class note (coverage), and no cloud. | Smoke sequence with the year chip set to 1972. |
| A14 | The full smoke run ends with 0 errors. | |

The first slice satisfies A1-A8 (figures excluded), A10, A12 and A14.

---

## 11. Risks

1. **Reading the request wrong.** "Not following a cross-section" might mean *drop the wave entirely* rather than *give
   the present its own volume beside the wave*. This design keeps the wave behind at context, and removing it would be
   one emphasis column. Ask the user (section 12).
2. **3D occlusion.** Terraces at different heights hide each other's far slopes, and figures on back slopes hide behind
   summits.
   - The staircase climbs away from the camera's default yaw (160°), so each higher terrace is further away.
   - Figures stand only on viewer-facing slopes.
   - The walk-in and terrace close-ups cover the rest.
   - The pixel check is A10 (overlaps) plus a review of the frames.
3. **Two scales side by side.** The wave's width is the square root of real output, while the present's area is linear
   in $. The present is not "the crest at a larger size" in the same units. The title card says "area = $ a year"; the
   guides show where each part came from, not equal size.
4. **Modelled placement.** Households by industry and fifth come from the simulation's players (≈), not from data, so
   the rings are dotted and the card prints n. A viewer may read a figure on the chips hill as "a chip worker in the
   bottom fifth" literally. Caption: "1 figure = 1M households · where they work is simulated".
5. **Assumed heights again.** The terraces' heights inherit the wave's assumed order. Because the explosion adds vertical
   gaps, "higher" could look even more like "more important". The tier labels keep "an order of dependence, not
   importance" in the legend.
6. **Plan churn.** Absorbing M1 rewrites unbuilt packages (WP20, WP24-WP26, WP33) and spec §5. It needs the user's
   decision and a short spec amendment, which the lead merges as in the handoff, step 2.
7. **The image pipeline.** Tokens with images depend on the pipeline (handoff build order 1). Until then figures and
   firms are line glyphs, which is still objects but not yet the thumbnails asked for.
8. **The stage's location.** Moving `StageFrame`'s origin to +520 is a one-line change before WP19 is built. If WP19 is
   built first, every stage pose moves.
9. **S07 (the mirror).** The present frame follows `LandFrame`, so it mirrors with the land. Labels are screen-space and
   unaffected. The portrait and walk-in poses need a check with the flag on.
10. **Quest.** The wave plus the present is too much on device, so the wave goes absent in XR (crest outline only). The
    choreography's 6.4 s at 72 Hz must not cause hitches when the token meshes are first uploaded, so warm them up
    during the Face phase.
11. **Clutter returning through states.** Each point line is limited to its own step, and A7 enforces it. The owners
    state's 75 threads are bundled per firm.

---

## 12. Questions for the user

1. **Keep the wave behind the exploded present at low strength (recommended), or hide it there?**
2. **Should the exploded present replace M1's flat towers (absorb, recommended), or should the towers stay as a second
   state?**
3. **Should the universe's end card get a "Straight to the present ▸" button?** This touches the universe scene, which
   is otherwise unchanged.
4. **Default year.** The exploded view shows the crest's year, 2025 by default. Should it be titled "the present" only
   for the latest year, and "⟨year⟩, taken apart" otherwise (proposed)?

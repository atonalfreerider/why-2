# why-2 data

All runtime data lives in `Assets/Resources` and is loaded as `TextAsset`s by the layers. Every file
below was either part of the original projects (why-2 and smv) or authored for this version from
mainstream sources and then checked by an independent reviewer pass. Values marked approximate are
modeled, not measured; the graph is an education tool about cause and effect, not a primary source.

## Matter (red) - `Data/matter.json`

```json
{ "ageOfUniverseYears": 13.787e9,
  "items":  [{ "id", "name", "startYa", "endYa", "massKg", "rank", "parent", "inPath", "blurb", "source",
               "sizeM", "bound", "icon" }],
  "epochs": [{ "id", "name", "ya", "afterBigBangYears", "tier", "blurb", "source" }] }
```

* `items` are the bands of the red layer. `rank` orders them radially (0 = innermost = Earth). Bands with
  `inPath` form our lineage, nested through `parent`: universe -> Laniakea Supercluster -> Virgo
  Supercluster -> Local Group -> Milky Way -> solar nebula -> Sun -> Earth.
* `sizeM` (diameter, metres) with `massKg` gives each band's density. A band is shaded by the density of
  its *own* region (its matter minus what formed out of it, over its volume minus theirs), from about
  3x10^30 atoms/m^3 for Earth to 2x10^5 between the stars and ~0.3 between the galaxies, so the gaps
  between the contours read as orders of magnitude. The fan labels quote these densities.
* `bound: false` marks structures gravity does not hold together (superclusters, the universe): they
  thin out and dim as space expands.
* `icon` names the glyph drawn beside the item's labels (`Core/Icons.cs`: universe, cosmic_web,
  first_stars, laniakea, supercluster, galaxy_group, galaxy, nebula, sun, planets, earth, moon).
* The red envelope follows the real expansion of space: a flat Lambda-CDM scale factor
  (Omega_Lambda 0.69, 1/H0 = 14.4 Gyr), so most of the fan opens in the first few billion years.
  Groups that form inside an older body (the solar system inside the Milky Way) open gradually over a
  long arc, so there is no step in scale anywhere.
* The same numbers make the layer a map with a definite scale (`Scripts/Matter/MatterScale.cs`), a comb of
  comoving distances from our lineage: at every moment the distance from us grows exponentially with rho,
  from the trunk at rho = 0 (a comoving kilometre: 10^3 m today, scaled back by the scale factor) to the
  horizon at the envelope's edge (the observable universe, 4.4e26 m, scaled back), 23.6 decades across
  the fan. A bound body of our lineage gathers its matter from a comoving region `CollapseFactor` (6)
  times its radius; as it forms, its band's edge and the matter inside it relax inward onto its radius
  today, so near the present every lineage body ends at its real radius (`sizeM` / 2).
* The scale grid (`Scripts/Matter/ExpansionGridLayer.cs`, shader `Why/ScaleWaves`) draws that field as
  waves: wave k is the shell of matter 10^k .. 10^(k+1) m from us today, from 10^-1 m (the matter that
  collapsed into the Earth, the Sun and the solar nebula, which slides into the trunk as they form) to the
  horizon, cut into ten by 2..9 x 10^k, then tenths, hundredths and thousandths. A level opens where its
  lines are 3 to 7.4 px apart on screen and fades to a floor as the next opens (`Scripts/Matter/SpaceLod.cs`
  mirrors the rule). The HUD reads the scale of space at the centre of the screen above its key: the area
  one pixel covers (physical m² at that moment: the outward scale squared, the screen's other direction
  being time) and the spacing of the finest waves fully open there; the probe (G) reads the same at the
  cursor, with metres from us and metres per world unit.
* `epochs` are cosmic and geologic markers (Big Bang ... Chicxulub impact).
* Sources: Planck 2018; Tully et al. 2014 (*The Laniakea supercluster of galaxies*, Nature);
  Wikipedia *Chronology of the universe*, *Observable universe*, *Virgo Supercluster*, *Local Group*,
  *Milky Way*, *Formation and evolution of the Solar System*; Immerman, *The universe*
  (people.cs.umass.edu/~immerman/stanford/universe.html). Replaces the original `energy.json`.

## Life (green)

* `TimetreeOfLife2009.txt` - the TimeTree of Life (Hedges & Kumar 2009) family-level Newick tree:
  1610 leaves, ultrametric, root at 4200 Ma. Unchanged from the original project.
* `Data/life_traits.json` - `{ "<leaf label>": { "t": 1.0-5.0, "role": "...", "common": "..." } }`, the
  trophic level (food-chain position), ecological role and a lay common name for every leaf. Used to
  order branches with top predators on the inside track. Hominidae is fixed at 5.0 (the reference point).
  The calorie flow layer (`Life/TrophicFlowLayer.cs`) groups families into Lindeman's trophic levels:
  producers = roles `producer` and `chemotroph`; consumers (`herbivore`, `filter_feeder`, `omnivore`,
  `insectivore`, `carnivore`, `apex`) by `t`: below 2.5 herbivores and filter feeders, below 3.8
  predators (secondary consumers; insect eaters reach 3.5), 3.8 and above top predators; consumers below
  t 2 (carnivorous plants) and the roles `decomposer`, `detritivore`, `parasite` and `symbiont` feed beside
  the grazing chain and are left out. A lineage at a moment takes the dominant level (at least half) of the
  classified families descending from it. The representative chains (Devonian 380 Ma, Jurassic 150 Ma,
  Miocene 12 Ma) are hand-picked textbook food chains, checked against this mapping when the layer builds;
  the ~10% transfer per step is Lindeman's rule of thumb for ecological efficiency.
* `Data/life_clades.json` -
  `{ "clades": [{ "id", "name", "mrca": [leafA, leafB], "treeAgeMya", "leafCount", "tier", "blurb" }],
     "events": [{ "id", "name", "mya", "tier", "blurb",
                  "loss", "lossMeasure", "recoveryMyr", "recoveryMeasure", "facts" }] }`. A clade is the most recent
  common ancestor of two leaves; events are dated milestones (Great Oxidation, Cambrian explosion, the Big Five
  extinctions, first hominins, agriculture...).
* The Big Five events also give what the tree collapses by (`Scripts/Life/MassExtinctions.cs`): `loss`, the share
  of species lost (0..1), in the measure `lossMeasure` names ("marine species", "all species"); `recoveryMyr`, the
  millions of years until the measure `recoveryMeasure` names had recovered ("global marine diversity took 75
  million years, to the Middle Jurassic"); and `facts`, the ids of the facts both numbers come from, in
  `Docs/research/life/extinctions-big-five.json` (each passed the truth gate and a skeptical second reading; see
  that folder's README). The TimeTree holds only families alive today, so the losses come from here: the tree's
  living weight drops to 1 - loss at the event and grows back along an S-curve by the end of the recovery, and
  overlapping windows multiply (the end-Triassic strikes inside the end-Permian's rebound). The depth of a dip is
  the share of species lost, not measured biomass, which deep time does not have; each event's card says so
  ("Lost: 81% of marine species. Recovery: ... The tree's dip stands for this share of species, not for measured
  biomass."). A thin outline along the tree's outer edge draws every dip, and highlighting an event lights its dip
  (the end-Permian's with the end-Triassic's inside it). An event without `loss` draws no dip; one whose loss is not
  between 0 and 1, or whose recovery is longer than the time since it or too short to draw, is left out with an
  error. Only the present-day collapse is measured biomass: the farm layer's wild mammals stream (below).

## Humans (blue)

* `powerByYearsAgo.json` - digitized from John B. Sparks' *Histomap* (1931): for every 50 years from
  4000 to 100 years before 2024, each stream's left position and width in Histomap units
  (1699 units = 100% of world power). The digitization is partial (not every stream of the chart).
* `Data/civilizations.json` -
  `{ "epochYear": 2024, "totalWidth": 1699, "civs": [{ "id", "key", "name", "region", "startYear",
     "endYear", "parents", "blurb", "source", "names" }], "extension": { "<yearsAgo>": { "<key>": { "Item1", "Item2" } } } }`.
  Metadata for every stream, causal `parents`, and an extension that (a) adds streams missing from the
  digitization (United States, Britain, Russia, Japan, Ottomans, Mongols) and (b) continues all streams
  from 1924 to 2024 using approximate shares of world GDP (Maddison Project) as the power proxy.
  The optional `names` (`[{ "from", "name" }]`, `from` a calendar year) give a stream its name by period, so
  the people of a year and the present edge read the state of the time ("East Francia" from 874, "Holy Roman
  Empire" from 962, "German states" from 1806, "Germany" from 1871); before the first entry, and without `names`, the
  stream is `name`. A stream can carry people of another state: the Holy Roman Empire / Germany stream also carries the
  Habsburg lands, so the people panel names those figures' own states by period (`FigureTracker.OwnStates`: Freud in
  Austria-Hungary in 1914, Mendel in the Austrian Empire).
* `Data/demography.json` - world population (HYDE 3.2 / Our World in Data / UN WPP), crude birth rates,
  era life tables (survival by age, female advantage), sex ratio at birth, major wars with approximate
  male excess mortality ("the gender difference, especially with war"), and US generations.
* `Data/figures.json` - famous historical figures `{ "id", "name", "born", "died", "gender", "civ",
  "prominence", "tier", "role", "blurb" }` drawn as highlighted lifelines in their civilization.
* `Data/smv/*.csv` - from the smv project: UN World Population Prospects single-age population of the
  United States by sex, 1950-2022 (thousands); US marriages and divorces per 1000 (Our World in Data /
  CDC NCHS); single-parent homes (US Census); number of sexual partners by age and sex (bedbible.com,
  CDC NSFG key statistics).

### Modeling notes

* Civilization population is approximated as world population x relative power share. Relative power is
  not population, but it is the only per-civilization series available across 4000 years.
* Lifelines are statistical: each line represents N people (shown on screen), born in proportion to the
  stream's births, living a lifespan drawn from the era's life table, shortened for men during wars.
* "Social market value" follows the storyline in the smv README (see `Assets/Scripts/Humans/Shared/SmvModel.cs`
  for every parameter). It is a model of an idea, drawn so it can be examined, not a measurement.
* The United States 1950-now is simulated in detail from the UN age pyramids with marriage, divorce,
  births and partner counts; children's lines start from their parents.

## The hierarchy of human domination

* `Data/domestication.json` - life under human control, drawn in green directly beneath the
  civilizations: global cropland and grazing land (HYDE 3.5, 2025, via Our World in Data; cross-checked against
  the HYDE 3.2 totals of Klein Goldewijk et al. 2017, which differ by 5-10 percent in 1700-1850), livestock
  biomass by type in megatonnes of carbon (Bar-On et al. 2018, Greenspoon et al. 2023, FAO), crop, human and
  wild-mammal biomass, and 35 domestications (dog, sheep, cattle, wheat, rice, maize...), each anchored where
  the species enters the farm layer (`wildFamily` = the exact leaf label of its wild family in the tree of
  life, which stays lit until the moment of domestication). Livestock is drawn on the same mass scale as the
  human layer.
  Wild land and marine mammals (`wildMammalMtC`, after Bar-On, Phillips and Milo 2018, Greenspoon et al.
  2023 and Barnosky 2008; a rough reconstruction before about 1900) are one stream below the farm, on
  the life plane (`GraphStyle.LifeY`), on the livestock's carbon scale and width rule: the present mass
  extinction as biomass, shrinking while livestock grows. The series begins at full strength, so the
  stream fades in over the 2000 years before its first year at that first value (a width rising from
  zero would draw a growth the data does not have), and a line marks its outer edge as the cropland's
  inner edge marks the livestock's. Its anchor `farm:wild_mammals` stands where the series begins; its
  tooltip quotes the series' first and last values, the livestock in that last year and these sources.
  Its id (`FarmBase + 9000`) lies outside `farm:all` (`FarmBase` .. `FarmBase + 99`), so picking the
  livestock and crops does not light the wild mammals nor the reverse, but inside the farm stratum's
  range, so it comes back to full strength with the farm; the tour's `farm_rises` stop names
  `farm:wild_mammals` beside `farm:all` to light both. The series must be at least two [year, MtC] pairs
  of finite numbers, the years rising and no mass below zero; when it is missing or malformed the layer
  logs an error and leaves the stream out.
* `Data/extraction.json` - matter under human control, drawn in red beneath the farm layer: annual global
  extraction of fossil fuels, metal ores and non-metallic minerals (UN IRP Global Material Flows,
  Krausmann et al. 2009/2018; rough historical estimates before 1900), 16 materials with first use
  (each rises out of Earth's matter band), and 30 extraction milestones.
* `Data/figures.json` also carries `influencedBy` (figure ids) and `influenceNote` - well-established
  direct influences (teacher and student, succession, acknowledged intellectual debt) drawn as threads
  between lifelines.

## Director - `Data/tour.json`

`{ "title", "steps": [{ "id", "title", "text", "focus", "anchor", "highlight", "hold" }] }` - `focus` is a
view preset id, `anchor`/`highlight` are anchor keys (`matter:`, `epoch:`, `clade:`, `lifeevent:`,
`leaf:`, `civ:`, `figure:`, `war:`, `gen:`, `smv:us`, `smv:wave` (economy: the road of lives at the year shown,
where the wave fans out; registered by SmvLayer), `farm:`, `domestication:`, `extraction:`,
`extractionevent:`, `resource:`, `time:<yearsAgo>`, `now`).

A step may also carry (both tours read them; empty or missing fields are left out):

* `key` - `[{ "color", "label" }]`: the card's own key, a few swatches of what the view draws. `color` is a
  named hue (`gold`, `rose`, `ice`, `blue`, `steel`, `white`, `grey`, `wake`, `red`, `green`, `humans`) or
  `#RRGGBB`. The economy's names are the palette's (`EconomyStyle`: capital, desire, fear, wages, government; `white`
  is the attention cloud's), the tones the HUD legend draws its swatches in, so a card's key and the legend beside it
  agree; two marks the palette does not name keep their own: `grey`, the stone grey of rules (the bedrock's regulation
  strands), and `wake`, the steel blue of the wave's yearly ribs. `red`, `green` and `humans` are the universe's matter,
  life and humans.
* `reading` - one sentence under the swatches: how to read the view (what height, width or position mean).
* `measured`, `judged`, `assumed` - the card's footnote, in the three kinds of evidence the narration keeps
  apart: what is measured, what Jev (an AI model) judged, and what the drawing assumes.
* `caption` - the attention arrow's caption, in place of the anchor's own name.
* `side` - the screen side the card prefers where the view's subject sits under its usual place: `left` or
  `right`, either with `top` or `bottom` (`bottom` alone is the left bottom).
* `wait` - `true` for a stop that asks the viewer to do something: autoplay waits for Next there.
* `cues` - `[{ "at", "show" }]`: from `at` seconds into the stop, the view shows its state named `show` (the
  names a view registers with `ViewVariants`, e.g. `se` for Sweden in the regimes view); leaving the stop
  returns the view to its default.
* `effect` - a scripted effect played instead of a camera flight (`genesis`: the Big Bang's flash).

* `Data/tour_narration.json` - `{ "model", "generated", "steps": [{ "id", "narration" }] }`: the spoken
  script, one entry per tour step (60-110 words each), written by `Tools/generate-narration.ps1` with
  the OpenAI API from the steps' on-screen text and the four threads of the story (the three levels and
  how each rests on the ones before, the one-way arrow of cause and effect, how scale works, the chain of
  causes leading to the viewer).
* `Audio/Tour/<step id>.ogg` - the narration voiced by `Tools/synthesize-narration.ps1` with the Cartesia
  API (voice "Clive - Measured Expert", mono Vorbis via ffmpeg, streamed at runtime; see
  `Scripts/Editor/NarrationImport.cs`). A step without a clip is read silently at the usual pace.

## The economy scene (`Data/economy/*.json`)

The economy scene (`Assets/Scenes/Economy.unity`, see `Docs/ECONOMY.md`) reads seven files, parsed by
`Scripts/Economy/Data/EconomyData.cs` (which validates sums and ids and logs what does not add up). They were authored on
2026-10-01 from BEA, Federal Reserve, BLS, Census, SSA and other tables (most through GitHub mirrors of the primary
downloads, because the agencies' sites were not reachable), each checked by an independent pass whose fixes are logged in
the file's own `corrections` array. Every file carries `vintage` and `sources`; numbers that could not be checked against a
primary table are marked `recalled` or `derived` in their notes.

Two caveats apply to all of them:

* **Vintage.** BEA's annual update of 30 September 2026 revised 2022 - 2025 (2025 GDP +0.3%, the 2025 saving rate from 4.6% to
  5.4%, profits down). Its industry and income levels could not be downloaded in time, so the files keep the earlier vintage
  for internal consistency and use the revised saving rate where it matters; expect about 1% on 2025 levels.
* **Models, not measurements.** The industries' value added, the national accounts and the wealth groups are measured; how
  money is routed between wealth groups, the categories' motives (desire or fear, fantasy) and everything in `psyche.json` are
  modeling choices informed by the literature, documented in each file and in `Docs/ECONOMY.md`.

### `Data/economy/industries.json`

The industry wall: each industry's value added from 1947 to 2025 plus a 2026 estimate, and how it splits between workers, taxes, depreciation and owners. The industries sum to GDP every year (within 0.09%).

**Schema**

- `gdp` holds nominal GDP in $B from 1946 to 2026. `deflator` is the GDP price index with 2025 = 100. `laborShare` is compensation divided by GDI, 1947 to 2025.
- `tiers` lists the five tiers: gov, raw, make, services and tech.
- `industries` lists the 25 ids from the design. Each has `valueAdded` ($B, one point per year from 1947 to 2026), `compShare`, `taxShare`, `depShare`, `gosShare2024` and `profitMargin`. It also has `employment` (FTE thousands for 1950, 1970, 1990, 2010 and 2024), `corporateProfits` (2025; 0 when it cannot be separated, in which case `corporateProfitsGroup` names the combined NIPA line), up to 5 `companies` and a `note` on its method.
- `overlays` holds `social` (US digital ads, platform ad revenue) and `ai` (hyperscaler capex for 2025 and 2026, Nvidia, frontier-lab run rates).

**Sources**

- BEA GDP by industry: historical tables (1947–1996), integrated accounts (1997–2016), annual tables (2017–2024), quarterly data (2025).
- BEA 2024 Use table; NIPA 1.10 and 6.16D; BEA FTE 2024; BLS CES employment.
- Damodaran (January 2026) and FactSet margins; company filings and September 2026 market caps; IAB/PwC ad data.

All BEA data are from before the September 30, 2026 annual update, which raised 2025 GDP by 0.3%.

**Modeling notes**

- **Detail before 1963:** BEA's historical tables only give totals for information, finance and professional services before 1963, so those years are split by 1963 shares. Before 1963, state and local government is its 1963 ratio (0.716) of state and local consumption and investment, and federal is the rest of government.
- **Software:** Before 2017, software publishers are reported together with print publishers, so their share is interpolated between documented anchor years. Print publishing is counted in `media_telecom`.
- **Depreciation:** `depShare` is an estimate. Capital-intensity guesses are scaled to sum to NIPA private depreciation for 2024 ($4.0T). For government, depreciation equals general government's operating surplus.
- **Tariffs:** The trade shares are for 2025 and include $264B of customs duties.
- **2026:** Each industry's 2026 value is its share of GDP in 2026Q1 times estimated 2026 GDP. Estimated 2026 GDP is 2025 GDP times the ratio of first-half 2026 to first-half 2025.
- **Employment:** 1950 and 1970 values are approximate (±15%).

### `Data/economy/circuit.json`

This file calibrates the 2025 money circuit. Money moves from industries to income, then to the four wealth groups and government, then to spending. Amounts are nominal $B; shares run from 0 to 1. A python check verifies every identity listed here.

**Schema**

- `income` holds the GDI components. GDP equals GDI plus `statisticalDiscrepancy`, and GDI is built with `corporateProfitsDomestic`. `corporateProfits` is the national figure, which adds $527B earned abroad. It splits into taxes, `dividends` and `retained`. `buybacks` are nonfinancial net buybacks and come out of `retained`.
- `personal` holds NIPA 2.6: DPI is personal income minus taxes, and saving is DPI minus outlays. `government` holds federal FY2025 figures plus NIPA 2025 figures for transfers, purchases and state and local.
- `groups` covers bottom50, next40, next9 and top1. Each group has households, net worth (DFA 2026Q1), nine flow shares that sum to 1 across groups, `pensionShare` and a 2025 budget.
- `groupHistory` covers net-worth shares from 1950 to 2026. `groupEquityHistory` covers equity shares from 1989 to 2026.
- `io.flows` lists 2024 flows of at least $5B, plus the diagonal, as [supplier, user, $B] over the 25 industry ids. `finalDemand` is broken down by commodity; imports are negative.
- `capture` lists the 25 largest companies by market cap (`name`, `ticker`, `industry`, `marketCap`, `netIncome`, `revenue`, `fiscalPeriod`, `note`). Market-cap aggregates, `incomeHistory` and `personalHistory` sit alongside it. Each company also carries its US employees for the land's named employers (the wage arcs from its tower): `usEmployees` (thousands), `usEmployeesBasis` (`"10-K FY2025"` when read from the latest annual report's human-capital section, 13 of 25; `"recalled"` otherwise, ±20%, and the tower's card marks it "≈") and `usEmployeesSource` (where the number was read, or why no US split was found).

**Sources**

- BEA NIPA 1.10, 2.6 and 6.16D, from before the 2026 update.
- The BEA 2024 Use table, with all 71 industries.
- The Fed's Z.1 and Distributional Financial Accounts (DFA).
- SCF 2022, BLS CE 2024, and CBO/Treasury FY2025.
- Company filings and market caps from September 2026.

**Modeling notes**

- **Carried-forward values:** The 2025 figures for net interest, production taxes, depreciation and purchases are 2024 values grown with GDP. Production taxes also include the jump in tariffs.
- **Income-ranked data:** CE wages, CE taxes and means-tested benefits are reported by income group. A Gumbel copula (θ = 1.87) moves them to wealth groups. It is fitted to DFA net worth by income and matches SCF median incomes. Social Security and Medicare use a wealth-by-age model.
- **Consumption:** Consumption equals DPI minus saving. The top 1% saves 35% and the next 9% saves 12% (Saez–Zucman); the bottom 90% rate (−4.7%) is solved for.
- **Before 1989:** Group shares come from Saez–Zucman, spliced to DFA's 1989 levels.
- **Zero values:** A revenue or net income of 0 means unknown (SpaceX).

### `Data/economy/spending.json`

Where US households' money goes in 2025, sorted into the notebook's seven categories. Items cover all of PCE ($20,954.9B) exactly, and saving items add up to NIPA personal saving ($1,046.8B).

**Schema**

- `categories` lists the seven categories in a fixed order. Each one has its notebook words, a `bracket`, desire and fear weights (each set sums to 1), `fearShare`, a `mind` point, `fantasy` and `pce`. Its `items` list the money ($B), `industries` (weights that sum to 1 over the 25 industry ids), `importShare` and the item's own `fantasy`, with the BEA lines named in `bea`.
- `outsidePce` covers two flows that are not PCE: consumer interest ($577.7B, under jeopardy) and transfers paid ($286.8B, under collective).
- `history` gives one row per year from 1950 to 2026: seven shares of disposable income that sum to 1. `historyMapping` shows how each BEA major type splits into the categories.
- `byQuintile` holds the BLS CE 2024 quintiles. `ceToPce` and `pceBasisShares` scale them to PCE.
- `markets` holds 56 headline markets, used as labels only.

**Sources**

- BEA NIPA 2.4.5U (April 2026) and 2.6 (through July 2026), both from before the September 2026 annual update.
- BEA's 2024 Use table, run through a Leontief consumer-dollar matrix (research/flows.json).
- BLS CE 2024, Fed DFA 2026Q1, and BEA's saving by income quintile.

**Modeling notes**

- **Basic vs. status:** Shelter, vehicles, clothing and furnishings are split at the CE second-quintile level. The basic share (0.73, 0.55, 0.66 and 0.58) is a necessity. Everything above it counts as status.
- **Visitors:** Spending by nonresidents ($208.9B) is subtracted from the items visitors buy (hotels, meals, tuition).
- **Asset fees:** Charges for holding assets (FISIM, asset management, brokerage: $1,009B) count as PCE. They are filed under jeopardy and flagged `assetFee`.
- **Industry weights:** Weights are the domestic value added each dollar pays for along the supply chain. Goods are split into producer, trade and freight. Margins are scaled to the Use table total.
- **Saving uses:** Each quintile's saving is assumed to go into its assets in proportion to its DFA holdings. Crypto ($30B) is a recalled anchor with low confidence. Weights for saving items show which intermediaries receive the money, not value added.
- **History:** History uses 2025 splits within each BEA major type, and the 1959 service mix before 1959.
- **Judgment calls:** Fantasy and the desire/fear weights are judgments. The CE survey misses most vice spending.

### `Data/economy/psyche.json`

The mind behind the money. For each simulated life, this file sets the drives, traits and biases, and the evidence for what being "in control" means. A build script (scratchpad `psy_build/build_psyche.py`) derives every computed value, so each derivation can be rerun.

**Schema**

- `desires` and `fears`: the notebook's five pairs. Each has a blurb, chemicals, a `weight` (sums to 1 per pole) and a `lifeStage` curve: `floor + (1 - floor) * Gaussian(peakAge, spread)`.
- `chemicals`: seven entries, each with a pole, function, markets and a caveat.
- `modes`: the five modes as shares of waking time. `modeResidual` holds daydreaming (0.30).
- `os`: higher share 0.40, default share 0.60, and the joint domains.
- `signals`, `hierarchy`: four ranks, with shares as speculative priors.
- `biases`: 21 entries, including ego depletion as a warning.
- `traits`: plus `traitCorrelations`, which is positive definite.
- `tilts`, plus `savingRatePp` and `lifeStageTilts`.
- `agency`: thresholds and weights, and 34 anchors, including `in_control`, `fantasy_*`, `default_*` and `fragile_*`.

**Sources**

- research/psychology.json and research/behavior.json, with the editor's corrections: Madrian–Shea 37% → 86%, SHED 2024, and saving rates after the September 2026 revision.
- spending.json, for drive weights and fantasy shares implied by spending.
- Big Five age and sex gradients from the SAPA bfi and TIPI samples.

**Modeling notes**

- **Desire weights:** taken from the 2025 desire-side money.
- **Fear weights:** half money, half stated prevalence (no $400 buffer, loneliness, fear of walking alone at night).
- **Tilts:** mapped from the research shift coefficients. Signs come from the literature; sizes are speculative, and none exceeds 0.25.
- **Present-bias β:** sampled from the research mixture, mean 0.92 and sd 0.14. The research summary's 0.88 does not match its own mixture.
- **`in_control` = 0.12 (range 0.06–0.18):** 0.28 have material autonomy, × 0.55 of those have a deliberate plan, × 0.75 of those have little fantasy or debt. Cross-checks: 31% engaged × 35% with a plan, 9% millionaire adults, 6% self-employed.
- **The thesis:** "few in control" holds. "Many buy fantasy" holds only partly: about half of adults are stable default-followers, and fantasy spending rises with income.

**Caveats**

- Most psychology values are recalled, not verified.
- Chemicals are labels for latent states, not measured chemistry.
- Judgment calls: hierarchy shares, agreeableness and reason tilts, exposure and childless prevalence, agency weights.
- The top income quintile's fantasy share is 0.23, so score the fantasy component softly.

### `Data/economy/games.json`

The prisoner's dilemma: each simulated person's strategy and cooperation (the lives), and the land's season of tit for tat
between the players (the `social` block). Shares run from 0 to 1.

**Schema**

- `payoff`: Axelrod's T=5, R=3, P=1, S=0.
- `strategies`: the engine's seven ids. Each has a one-sentence `rule`, `os` and `populationShare` (they sum to 1), plus `notebook`, `anchor` and `source`. TitForTat, GenerousTitForTat and WinStayLoseShift are `higher`. AlwaysCooperate, AlwaysDefect, Grim and Random are `default`. AlwaysCooperate is kind, but it never weighs evidence about the other player or curates its network.
- `shadow`, `tournaments`, `lessons` and `empirical`: theory and lab benchmarks.
- `tribes`: `partyId` rows [year, {dem, rep, ind}] for 1952–2025, at most 4 years apart, and GSS `trust` for 1972–2024 (with trust by age, cohort and party). `distrustIndex` is the affective-polarization series the season's party term scales with (`TribesData.AffectivePolarization`). Further fields cover polarization, cross-party marriage and sorting.
- `spec`: numbers only (the loader reads a `Dictionary<string,double>`), read by the lives (noise, continuation, distrust, party inheritance). Their explanations are in `specNotes`. `tribeSize`, `alphaSway`, `mutation`, `generations` and `gamesPerPair` (and their `specNotes`) are historical: they configured the retired games station (two tribes and a replicator floor), which the land's season replaced.
- `checks`: engine values of the retired games station (`replicator`, `tribeGame`), kept as a historical record; nothing in the scene reads them. The season's regression values are the `[Why] Society` log lines.
- `social` (the land's season, `Land/SocialSeason.cs`; spec 5.1-5.6):
  - the season: `rounds` 96, `pairsPerPlayer` 10, `noise` 0.02, `continuation` 0.95, `roundsPerSecond` 4, `detections` (the coalition rounds 0, 24, 48, 72, 96), `incidentRound` 48 (the betrayal);
  - the levers: `forgiveness` 0.3333 (generous tit for tat, applied to each player's share of adults on the higher OS, `higherOsReason` 0.5), `tribalMemory` 0.5 (toward the other party the default OS repays what its own side received), `learning` 0.10 and `partnerChoice` 1.0 (partner choice), `affinityBound` 2.0;
  - `affinity`: the opening's terms (same tribe through `tribeMatrix`, same group, adjacent and far rungs, generations, industry and tier, owner with worker, a class slope) and the GSS trust offsets by age (`ageTrustLogit`);
  - `exposure`: how much more often a player deals with its own industry, tier, group and generation;
  - `strangerCooperation`: the lab's stranger cooperation (Sally 1995, 0.474) at its era's trust (0.40), which fixes the opening level of every year from that year's GSS trust;
  - `notes`: the source or the word "design parameter" for each value.

**Sources**

- Axelrod (1980, 1984), Nowak and Sigmund (1992), and Dal Bo and Frechette.
- Strategy-method type shares from Thoni and Volk (2018).
- GSS 1972–2024 microdata; ANES 1952–1968 and Pew, both recalled; Gallup 2025.
- The social block: Balliet, Wu and De Dreu (2014, in-group favoritism d = 0.32); Iyengar and Westwood (2015) and the ANES thermometers (party); Lickel et al. (2006, vicarious retribution); Rand, Arbesman and Christakis (2011, partner choice); Sally (1995).

**Modeling notes**

- **Strategy shares:** the research mix maps conditional cooperators (0.61) onto the reciprocal rules and free riders onto AlwaysDefect (0.20). The higher-OS share comes to 0.52.
- **Distrust (0.22):** in-group favoritism d = 0.32, scaled by the SD of an opening move at 0.74 cooperation, gives a 0.14 drop. Dividing by the share of opening moves the penalty can hit (0.643) gives 0.22.
- **The season** is a mean-field model of crowds: a tie carries the share of one player's people who cooperate with the other's. `learning`, `partnerChoice`, `tribalMemory`, `pairsPerPlayer` and the exposure weights are design parameters, not measurements.

**Caveats**

- The lab literature is recalled, the discontinuity d and the thermometer values most of all. The strategy mix is a judgment (±0.05).
- partyId is a cross-section of all adults. Fixing each person's party at age 18 would make older cohorts too Democratic.
- 2025 party shares are GSS 2024 moved by Gallup's change.
- The GSS changed to web plus face-to-face interviews in 2021, which may account for part of the drop in trust.

### `Data/economy/history.json`

This file holds 54 household time series, mostly 1946-2026, used to calibrate EconomicLives year by year. Money is in nominal dollars of each year; deflate it with `cpi`. A python check confirms the required ids are present, x values rise strictly, values fall in plausible ranges, and the arrays are compact.

**Schema**

- `series.<id>` = `{unit, points: [[x, v], ...], source, basis, note}`. `basis` is `github-data`, `recalled` or `derived`.
- x is a calendar year except in eight cross-sections, whose `unit` says "NOT a calendar year". Age is x for `ageEarningsMale/Female` (16-85), `netWorthByAge` and `homeownershipByAge`. Income percentile is x for `effectiveTaxRate`, `personalTaxRateCE`, `transferShareOfIncome` and `savingRateByPercentile`.
- Extras beyond the required ids: FTYR earnings and ratio, unspliced WID wealth shares, `bottom50IncomeShare`, `averageWageIndex` and `populationWPP`.

**Sources**

- BLS (CPI, CPS, CE) and BEA (saving rate, DPI).
- The Fed (Z.1, G.19, DFA, SCF).
- Census (CPS ASEC income, HVS, MSPUS, population V2025).
- Case-Shiller and Shiller, WID.world, NCHS, UN WPP 2024 and the GSS.
- Recalled values are marked in each note: Damodaran total returns, Census FTYR earnings, SSA benefits and wage index, poverty thresholds, G.19 card rates, CBO/ITEP tax rates.

**Modeling notes**

- **Saving rate:** 2022-2025 use the critique's post-update rates (3.5, 6.1, 6.3, 5.4). 2026 is August 2026 (4.1).
- **Earnings by sex:** These are derived. Men's FTYR median is scaled by 0.887 to match the CPS 2024 all-worker median of about $52K. The female/male ratio is the FTYR ratio times a part-time multiplier from GSS microdata. Profile × median ÷ 0.79 (men) or 0.82 (women) keeps the level right.
- **Wealth shares:** From 1989 these are DFA, consistent with circuit.json. Before 1989, WID is spliced to them.
- **Population:** Uses the Census basis. Rescale WPP pyramids by `population / populationWPP`.
- **Cross-checks:** Recalled total returns match Shiller data (correlation 0.988, same geometric mean). SSA benefits drift smoothly against the all-beneficiary data.

**Caveats**

- `effectiveTaxRate` is low-confidence CBO/ITEP recall, ±3 pp. Calibrate its level to BEA taxes.
- `savingRateByPercentile` and `transferShareOfIncome` mix BEA and CE data and carry about ±5 pp at the ends. `transferShareOfIncome` is cash only (it includes Social Security) and is kept for reference: the lives model pays Social Security and Medicare by entitlement and means-tests the other transfers.
- Before 1972, these are approximate: self-employment (likely 2-3 pp low), mortgage rates and card APR.
- DPI is the pre-update vintage.
- WID shares are flat after 2022 (nowcast).
- These series alone do not test "few control their life path". Combine self-employment, saving by percentile and wealth by age.

### `Data/economy/groups.json`

The twelve 2026 socioeconomic groups the land's players are cut from (spec 3.1-3.2; `Land/PlayerCensus.cs`). Every adult
alive in a year lands in exactly one group by a priority rule on its own record (`PersonYear` fields only); thresholds are
relative (a rank, a wealth group, the year's median earnings), so the same rule works for 1950 and 2025.

**Schema**

- `rule`: the priority rule in eight lines (the 1%; the self-employed: business owners or gig; employees: PMC, working poor, public servants, office, frontline; the non-employed: Social Security or comfortable retirees; a non-employed spouse of an employed adult takes the spouse's group, Erikson's dominance rule; students or out of work; children follow their mother, else their father).
- `thresholds`: `pmcRank` 0.80, `poorRank` 0.30, `retireAge` 62, `ssDependence` 0.5, `youngAge` 25.
- `officeIndustries` (11 ids) and `governmentIndustries` (federal, state_local).
- `cells`: a player is a cell of group × anchor × party with at least `minLines` 10 lines, or `richMinLines` 3 lines holding `richWealthShare` 1% of national net worth; smaller cells join their tier, then their group's rest.
- `generations`: Silent, Boomer, Gen X, Millennial, Gen Z by birth year (the season's generation affinity).
- `pseudoAnchors`: where the groups without an employer stand: pensions (comfortable retirees, at finance), Social Security (at federal), schools (students, at education), the safety net (out of work, at state and local).
- `groups`: twelve entries with `id`, `name`, `short`, `rung` (0-5, the distance from the bowl), `role` (own, work, dependent), the `rule` in one line, the `literature` share (%), `model2025` (the model's share of adults in 2025) and `where` (where the group stands on the land).
- `sources`: each with a mark: `v` verified, `recalled`.

**Sources**

- Pew Research Center (2024), lower / middle / upper income 30 / 51 / 19 [v]; BLS Contingent and Alternative Employment Arrangements, July 2023 (independent contractors 7.4% of employed) [v]; SSA fact sheet (39% of men and 44% of women 65+ get half or more of their income from Social Security) [v]; Gilbert's class model [v]; Erikson (1984), the dominance rule [recalled]; Dingel and Neiman (2020), jobs that can be done at home [recalled].

**Modeling notes**

- Each group's model share for 2025 sits inside or next to its literature range (PMC 19.7% against 15-19%, frontline 21.6% against 20-25%, Social Security retirees 11.3% against 9-11%).
- One model change came with the groups: the self-employed are never assigned to a government industry (the lives' record writer).

### `Data/economy/tour.json`

The economy's guided tour, the same schema as `Data/tour.json` (`title`, `steps[]` with `id`, `title`, `text`, `focus`,
`anchor`, `highlight`, `hold`): twelve steps, the road, the cut, the land, the roots, capture, the people, the rivers, the
mind, the society, one betrayal, 1972 and "any year". `focus` is an economy preset id (the year steps use the keyless
presets `roots`, `betrayal` and `y1972`); anchors are the land's keys (`land:cut`, `land:tier:<tier>`,
`land:root:<from>><to>`, `land:tower:<ticker>`, `land:group:<group>`, `land:river:<category>`, `land:coalition:<k>`,
`land:owners`, `land:crown`), `smv:us` and `smv:wave` (the road of lives at the year shown, where the wave fans out;
registered by SmvLayer). Every number in a step's text is one the scene computes and prints in its log
at the same precision (`116 players`, `0.686`, `$21.63T`), and the check script fails the tour when one is missing.

### `Data/economy/corporate-programs.json`

Written by `Tools/corporate/build.py` from `Tools/corporate/filings.json` and `Tools/corporate/economy.json` (both
tracked, every number with its source). `firms[]`: each stored firm (`circuit.json` capture) in its latest fiscal year
from its audited filing on SEC EDGAR - `revenue`, `costOfRevenue`, `rd`, `sga`, `advertising` (where disclosed),
`capex`, `financeLeases`, `buybacks`, `dividends`, `acquisitions`, `stakes` (stakes in other companies from the filing's
other outflows), `incomeTaxesPaid`, `stockComp`, `netIncome`, `operatingCosts` (a filing's own total where cost of
revenue is only one of its cost lines), `employees` (thousands), `lobbying` (calendar 2025, USD millions, summed from
the firm's LDA reports), `strategy` (where the firm says its money goes, paraphrased), `sources`, `notes` - and Jev's
judgments: `direction` (probability of climb, integrate_down, expand_across, lean_on_state, harvest), `defensive` (0
investing for new demand .. 1 investing for fear of losing position), `ai` (share of investment judged to go to AI), each
with its confidence. `economy`: the US totals for 2025 (advertising, digital and social ads, creator spend, social media
time, private fixed investment by type, business R&D, payouts, lobbying, M&A, hyperscaler capex and 2026 guidance,
data-center construction), each as `value`, `unit`, `year`, `source`, `note`. Money in USD billions.

Each firm's `history[]` holds its fiscal years 2015-2025 from `Tools/corporate/history.json`: `year` (the calendar year
holding most of the fiscal year's days), `fiscalYear` (the firm's own name for it), `start`, `end`, `position` (its
midpoint less half a year, where the scene draws it on the time axis), `revenue`, `costOfRevenue`, `rd`, `sga`, `capex`,
`financeLeases`, `buybacks`, `dividends`, `acquisitions`, `incomeTaxesPaid`, `netIncome`, `stockComp`,
`noninterestExpense` (whichever the firm's statements have), and Jev's three judgments with their confidences, made on
the year's numbers alone (`judgedOn`: `numbers`; the latest year too, so it differs from the firm's own judgment above
only by what its words add). `historyNotes` says what is particular to a firm (fiscal-year ends, combined lines,
predecessor registrants), `historyFilings` maps each accession the values came from to its EDGAR folder, and the top
level carries `historyCollected` and `historyGaps`.

`Tools/corporate/history.json` (tracked, written by `Tools/corporate/history.py`) is the history with every value's
source: its US-GAAP or the firm's own extension concept and the accession of the 10-K it was read from (the latest
reporting the period, so earlier years are as later filings restated them), from the SEC's XBRL company facts and, for
extension concepts, the 10-K's XBRL instance. SpaceX's 2023-2025 are the audited statements of its 2026 IPO prospectus
(424B4) by line name, recast to include xAI and X; Palantir starts in 2018. `gaps` names what is missing: Palantir's
2015-2017 and finance-leased assets before Alphabet's 2022 and Meta's 2019 disclosures (the scene counts none). A 10-K
without a buyback, dividend or acquisition line counts zero, with that as its source.

### `Data/economy/mobility-benchmarks.json` and `Data/economy/money-and-trade.json`

Written by `Tools/history/build.py` from `Tools/history/sources.json` (research of 2026-10-22, each number read in its
cited source). The benchmarks: published decade income-mobility matrices (US Treasury 1996-2005, open and closed panels;
Auten & Gee; PSID 1980s and 1990s; Bradbury) with their bottom-stays, top-stays and bottom-to-top cells, Chetty et al.'s
intergenerational matrix and rank-rank slope, and event studies (job displacement, divorce, hospital admissions, poverty
entries). Money and trade: `fedFunds`, `mortgage30`, `treasury10` (annual averages; 2026 the months available),
`tariffRate` (customs duties over goods imports, NIPA calendar years, gross of the 2026 IEEPA refunds), `customsDuties`,
`goodsImports`, `tradeShare` (exports plus imports over GDP), as `[year, value]`.

### `Data/economy/regimes.json`

Written by `Tools/regimes/build.py`: other governments as model changes, for the `regimes` view. Five regimes - the United
States (`baseline`), Sweden, China, Saudi Arabia and the Soviet Union around 1980 - each with `id`, `name`, `form`,
`chooses` (who chooses the government, in plain words: the drawing's words from `sources.json`, not a measure, read beside
V-Dem's index in the panel's third opening line), `era`, `year` and these measures, every one an object
`{value, year, source, url, note, quote}` (`value` absent when nothing defensible was found):

* `spending`, `revenue` - general government total expenditure and revenue, shares of GDP: IMF Fiscal Monitor (October
  2026) through the DataMapper API (`G_X_G01_GDP_PT`, `GGR_G01_GDP_PT`), 2024; the Soviet state budget from the research in
  `sources.json`.
* `revenueMix` - what the state lives on, as shares of its revenue (`parts`: `personal`, `corporate`, `social`, `goods`,
  `property`, `resources`, `enterprises`, `other`, summing to one), computed by the build from its sourced `inputs`: OECD
  tax structures are shares of tax revenue, scaled by tax revenue over total revenue (OECD tax-to-GDP over IMF revenue of the
  same year; the remainder is other revenue); Saudi Arabia's and the USSR's budgets use their own categories. `computed`
  says how.
* `stateFirms` - the public sector's share of listed companies' market value (OECD Corporate Governance Factbook 2025; the
  Soviet Union's: all of it by law, `basis` "by law"); `fortune` - the state-controlled share of the country's Fortune
  Global 500 2026 firms (`count`). `ownership` - `default` (1: every summit is the state's by law, the Soviet Union's; 0:
  nothing decides a summit no firm is ranked for, so the view draws it as a dotted "not ranked" ring, never as private,
  and the 0 only splits the profit taxes that fall from the summits) and `defaultNote`, read only where the default is 1:
  the law that makes it so ("state property by law (1977 Constitution, art. 11)"). The "privately owned" that the four
  default-0 regimes still carry from `sources.json` is an older reading of the 0; the code does not read it
  (`Regimes.cs` loads the note only with a default of 1). Then `firms[]` (`name`, `industry`: one of the scene's
  production industries, `stateShare`, `controlled`, `holder`, `revenue`, `url`, `quote`, `note`, `leads`): with `leads`
  true, the industry's summit - its largest firm on the Fortune Global 500 2026 (controlled when Fortune's profile says
  "Government owned 50% or more"), or for Sweden and Saudi Arabia the largest by revenue of the national firms compared;
  without, one of the state's other holdings. And `unranked[]` (`industry`, `firm`, `controlled`,
  `stateShare`, `note`): industries whose largest firm could not be ranked (`sources.json`), each with the largest firm
  the research names there among `firms` and why; the summit stays not ranked, its dotted ring steel when that firm is the
  state's (China's health and agriculture: Sinopharm, COFCO). The build logs each as a gap.
* `economy` - the country's own GDP, with a `unit` in place of a share, so the view can set the US hills on its state
  beside the country itself: IMF World Economic Outlook (October 2026, `NGDPD`, "billion US dollars") for the modern
  regimes; the Soviet Union's 1980 GDP in "billion rubles" (`note`: the GDP its spending and revenue shares are of).
* `trade` - exports plus imports over GDP: World Bank WDI `NE.TRD.GNFS.ZS`.
* `top1Wealth`, `top1Income` - the top 1%'s shares of net personal wealth and of pre-tax national income (equal-split
  adults): WID bulk download (2026-10-15), each at its latest year WID grades as observed (income grade 3+, wealth 1+).
  WID's model imputations (grade 0) are kept as `modelled` for the panel and never drawn.
* `democracy` - V-Dem's electoral democracy index (v16, via Our World in Data), latest year with the year before as
  `previous`; the USSR is V-Dem's "Russia" unit in 1980.
* `industries` - per production industry, `regulation` and `capture` (0..1): Jev Scores of how heavily the state's rules
  bind the industry and how much it shapes them (`industriesJudged` names the model). The US keeps `government.json`'s own
  judgments, so the baseline draws as the foundation view does.

`Tools/regimes/sources.json` holds the regimes and the research read by hand, with the URL and the text each value was read
from; API downloads and Jev answers are cached in `Tools/regimes/.cache` (gitignored).

### `Data/social/people.json`: `elite`

Added by `Tools/social/elite.py` without rerunning the Wikidata and Jev stages: how many of the social graph's other
accounts (the 1,139 most-followed) follow each person, from the graph's directed follow edges.

### Expected values and how they were measured

The land's log lines (`[Why] Land`, `Roots`, `Players`, `Money`, `Pools`, `Society`, `Society controls`, `Betrayal`,
`Facts`, `Land checks`) are compared with expected values and tolerances by the redesign's check script
(`landcheck.py` and its `landcheck_expected.json`, outside the repository). The spec's first values came from Python
prototypes run on an earlier dump of the lives (119 players). After the people's package fixed the census the lives give
116 players, so the 2025 values of the Players, Money (imports and falls), Pools and Society lines were re-measured on the
C# build itself (harness runs of the merged tree) and replaced the prototype's; the originals are
kept beside them for reference. The Land and Roots lines were not re-measured: they still match the prototypes (fills
within 1 point, offsets and roots exact). Every land builder prints a checksum, and two runs print the same ones.
The tour and `Docs/ECONOMY.md` quote the re-measured numbers.

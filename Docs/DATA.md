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
* The same numbers make the layer a map with a definite scale (`Scripts/Matter/MatterScale.cs`): each
  lineage body's band ends at its real radius (`sizeM` / 2, unbound structures and the observable
  universe, 4.4e26 m, scaled back by the scale factor), the inner track is 1 m by convention, and the
  distance from our lineage grows exponentially with rho between those knots. The scale grid draws
  shells at every round distance (10^N m, with log-ruler steps between) and comoving rays; the HUD's
  probe (G) reads metres from us and metres per world unit at any point.
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
* `Data/life_clades.json` -
  `{ "clades": [{ "id", "name", "mrca": [leafA, leafB], "treeAgeMya", "leafCount", "tier", "blurb" }],
     "events": [{ "id", "name", "mya", "tier", "blurb" }] }`. A clade is the most recent common
  ancestor of two leaves; events are dated milestones (Great Oxidation, Cambrian explosion, the Big Five
  extinctions, first hominins, agriculture...).

## Humans (blue)

* `powerByYearsAgo.json` - digitized from John B. Sparks' *Histomap* (1931): for every 50 years from
  4000 to 100 years before 2024, each stream's left position and width in Histomap units
  (1699 units = 100% of world power). The digitization is partial (not every stream of the chart).
* `Data/civilizations.json` -
  `{ "epochYear": 2024, "totalWidth": 1699, "civs": [{ "id", "key", "name", "region", "startYear",
     "endYear", "parents", "blurb", "source" }], "extension": { "<yearsAgo>": { "<key>": { "Item1", "Item2" } } } }`.
  Metadata for every stream, causal `parents`, and an extension that (a) adds streams missing from the
  digitization (United States, Britain, Russia, Japan, Ottomans, Mongols) and (b) continues all streams
  from 1924 to 2024 using approximate shares of world GDP (Maddison Project) as the power proxy.
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
  civilizations: global cropland and pasture (HYDE 3.2/3.3), livestock biomass by type in megatonnes of
  carbon (Bar-On et al. 2018, Greenspoon et al. 2023, FAO), crop, human and wild-mammal biomass, and
  35 domestications (dog, sheep, cattle, wheat, rice, maize...), each anchored where the species enters
  the farm layer (`wildFamily` = the exact leaf label of its wild family in the tree of life, which stays
  lit until the moment of domestication). Livestock is drawn on the same mass scale as the human layer.
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
`leaf:`, `civ:`, `figure:`, `war:`, `gen:`, `smv:us`, `farm:`, `domestication:`, `extraction:`,
`extractionevent:`, `resource:`, `time:<yearsAgo>`, `now`).

* `Data/tour_narration.json` - `{ "model", "generated", "steps": [{ "id", "narration" }] }`: the spoken
  script, one entry per tour step (60-110 words each), written by `Tools/generate-narration.ps1` with
  the OpenAI API from the steps' on-screen text and the four threads of the story (the three levels and
  how each rests on the ones before, the one-way arrow of cause and effect, how scale works, the chain of
  causes leading to the viewer).
* `Audio/Tour/<step id>.ogg` - the narration voiced by `Tools/synthesize-narration.ps1` with the Cartesia
  API (voice "Clive - Measured Expert", mono Vorbis via ffmpeg, streamed at runtime; see
  `Scripts/Editor/NarrationImport.cs`). A step without a clip is read silently at the usual pace.

## The economy scene (`Data/economy/*.json`)

The economy scene (`Assets/Scenes/Economy.unity`, see `Docs/ECONOMY.md`) reads six files, parsed by
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
- `capture` lists the 25 largest companies by market cap. Market-cap aggregates, `incomeHistory` and `personalHistory` sit alongside it.

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

This file holds the data for the games station and for each person's cooperation: the prisoner's dilemma between people and between tribes. Shares run from 0 to 1.

**Schema**

- `payoff`: Axelrod's T=5, R=3, P=1, S=0.
- `strategies`: the engine's seven ids. Each has a one-sentence `rule`, `os` and `populationShare` (they sum to 1), plus `notebook`, `anchor` and `source`. TitForTat, GenerousTitForTat and WinStayLoseShift are `higher`. AlwaysCooperate, AlwaysDefect, Grim and Random are `default`. AlwaysCooperate is kind, but it never weighs evidence about the other player or curates its network.
- `shadow`, `tournaments`, `lessons` and `empirical`: theory and lab benchmarks.
- `tribes`: `partyId` rows [year, {dem, rep, ind}] for 1952–2025, at most 4 years apart, and GSS `trust` for 1972–2024. Further fields cover trust by age, cohort and party, polarization, cross-party marriage and sorting.
- `spec`: numbers only (the loader reads a `Dictionary<string,double>`). Their explanations are in `specNotes`.
- `checks`: exact engine values for regression.

**Sources**

- Axelrod (1980, 1984), Nowak and Sigmund, and Dal Bo and Frechette.
- Strategy-method type shares from Thoni and Volk (2018).
- GSS 1972–2024 microdata; ANES 1952–1968 and Pew, both recalled; Gallup 2025.

**Modeling notes**

- **Strategy shares:** the research mix maps conditional cooperators (0.61) onto the reciprocal rules and free riders onto AlwaysDefect (0.20). The higher-OS share comes to 0.52.
- **Distrust (0.22):** in-group favoritism d = 0.32, scaled by the SD of an opening move at 0.74 cooperation, gives a 0.14 drop. Dividing by the share of opening moves the penalty can hit (0.643) gives 0.22. As a check, the C# TribeGame then cooperates 0.265 less than person-vs-person play of the same mix, inside the discontinuity band of 0.20–0.40.
- **Other spec values:** noise 0.02, continuation 0.95, tribeSize 24, alphaSway 0.25 (anchored on Asch conformity) and mutation 0.01.

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

# The economy scene

`Assets/Scenes/Economy.unity` is a second scene built on the same rendering core and the same United States
population as the causality graph. Time runs along a road from 1946 to now: the people's lifelines above it, a wall of
the industries that pay them beneath. A glowing **cut** stands across the road at one year (2025 by default). The cut is
that year's cross-section, and past the road's end it opens into a **land**: a stepped bowl whose terraces are the
industries, whose rim is where the people stand, where money is drawn as roots underground, rivers on the ground and
arcs in the air, and where the people play a season of tit for tat with each other.

Open the scene and press Play. Everything is built at runtime, as in the causality graph.

## Reading the scene

### The road and the cut

| | |
| --- | --- |
| **The road** | Time runs along a straight road from 1946 (left) to now (right). Every view shares this one road, so it never moves; only the camera does. |
| **People** | The blue lines above the road are the United States population from 1950 to now, exactly as in the causality graph: one line for 100,000 people, women on the inner side and men on the outer side, height = modeled social market value. A line turns **gold** for the years its people are **in control** of their own path (see *Agency* below). |
| **The industry wall** | Beneath the people stands the value added of 25 industries in 2025 dollars, stacked in the notebook's order from the ground up: government (the foundation), raw (oil, gas and metals in matter red, farms in life green), manufacturing and infrastructure, services, and tech nearest the people. It grows almost tenfold along the road. Each industry's band is split by who receives its value: wages (dim), upkeep (production taxes and depreciation, a faint sliver) and the **owners' share (bright)**. |
| **The cut** | A frame across the road at the selected year. It slices the wall's 25 bands and every lifeline alive in the year: 2,742 adult dots and 727 children in 2025. Its readout: "2025 · GDP $30.8T · 274M adults in 2,742 lines · 12% in control". |

On entering a land view the cut's card flies to the **plaza** 10 units past the road's end, its stack of bands lies back
into a **staircase** (five steps rising away from the viewer, government lowest, tech highest), the steps curl around a
vertical axis into rings and the rings spin into place: the bowl, 12.3 units across and 1.75 deep. The lifeline dots fly to
the rim, where they become the dots of their players. Every band's area stays proportional to its value added throughout.

### The bowl

The land shows **composition**, normalized to the year's GDP: 24 square units are one year's GDP (1 square = $1.28T in
2025), so 1950 and 2025 are the same size and the road and the wall keep the absolute growth. The land legend prints the
unit system for the year shown: "1 square = $1.28T a year · width 0.1 = $3.1T a year · roots ×4 · height 1 = $5.1T of
market value · a dot = 100,000 people".

* **Terraces.** Five rings, lowest to highest: government (the floor at the center), raw, make, services, tech; the
  people live on the rim above tech. In 2025 the sectors fill 61% of the government floor, 19% of raw, 54% of make, 92% of
  services and 14% of tech; services are $14.85T, 48% of GDP.
* **Sectors.** Each industry is a sector of its ring, its area its value added, split radially into wages (inner), upkeep
  and the **owners' share (gold, outer)**: owners keep 26% of GDP. Each sector is turned to sit over the suppliers that
  feed it most (the ring order and offsets are fixed for all years, so the land never reshuffles).
* **Roots.** Beneath the terraces the roots are what industries buy from each other (the BEA 2024 Use table): the 79
  flows of at least $50B carry 72.7% of the $15.85T traded between industries, and each rises into its buyer from below.
  Their direction is counted honestly: of all $20.76T of purchases, 16.7% rise from a supplier on a lower ring, 31.3%
  stay within a ring, 23.7% are an industry's purchases from itself (root balls) and 28.4% come down from a higher ring. A year other than 2024 scales the 2024 table
  by each buyer's value added (root labels show the year's dollars: professional services → trade is $654B in the table
  and $704B at 2025 value added).
* **Pools.** Where households' money is first paid: each industry's pool is as large as the spending it receives directly
  (the first recipient: the money builder, below). Health care overflows (households pay it $3.75T, 158% of its value added); professional
  services (7%), software (14%) and internet platforms (27%) stay nearly dry: they are paid by other industries, along the
  roots, and by advertisers.
* **Towers.** The 25 most valuable companies stand as gold towers on their sector: height = market value (NVIDIA $5.49T,
  1.07 units), cross-section = profit, foot ring = revenue. The four chip and device makers stand on a plinth: their named
  profits are 9.2 times what the US hardware industry's owners keep (profits earned worldwide against US value added);
  internet 1.2 times, software 0.8.
* **The crown.** Above the bowl's center hangs a gold ring: every private sector's payouts (dividends, rent, interest)
  rise into it, $5.33T a year to owners at home and $435B abroad, saving rises into it ($2.16T), and capital income
  ($5.33T), credit ($928B) and investment ($1.23T) fall from it.
* **Overlays.** Over tech, the AI scaffold (hyperscaler capital spending) and the ads halo (digital advertising).

### The inhabitants: 116 players in 12 groups

A player is a cell of the twelve 2026 socioeconomic groups × the industry that pays them (their **anchor**) × their
party, with at least 10 lines (1M adults), or 3 lines holding 1% of national net worth. In 2025 there are **116 players**;
the median player is 1.8M adults (2.2M people), the largest 11.8M. Every adult alive in the year lands in exactly one group
by a priority rule on its own record (`groups.json`); a non-employed spouse of an employed adult takes the spouse's
group (Erikson's dominance rule); children join their mother's player.

| Group | Rule (2025 share of adults in the model; the literature's range) |
| --- | --- |
| The 1% | the top 1% of adults by net worth (1.0%; 1%) |
| Business owners | self-employed earning at least the median, or wealthy (4.1%; 3-4%) |
| Gig & freelance | self-employed otherwise (2.8%; 2.5-4.5%) |
| Professional-managerial class | employees in the top fifth of household income (19.7%; 15-19%) |
| Working poor | employees in the bottom 30% of household income (6.5%; 4-13%) |
| Public servants | employees of federal, state or local government (4.4%; 4-5%) |
| Office & knowledge workers | employees in an office industry (6.9%; 6-10%) |
| Frontline & trades workers | employees otherwise (21.6%; 20-25%) |
| Comfortable retirees | not employed, 62+ or on Social Security, Social Security less than half of cash income (11.0%; 11-14%) |
| Social Security retirees | the same, Social Security at least half (11.3%; 9-11%) |
| Students & young adults | not employed, under 25 (3.7%; about 4%) |
| Out of work | not employed otherwise (7.1%; 6-9%) |

**Where each stands.** Everyone stands on the rim: the angle is the industry that pays them, the distance from the bowl
their class rung (rows packed so no two discs overlap). Owners and the self-employed stand on gold plinths (14 in 2025);
the 1% who work where named companies are (controllers) stand on top of their industry's tallest tower; the
capital-only 1% stand on the crown (3). Retirees, students and the out of work stand at the industry that pays them:
pensions at finance, Social Security at the federal sector, schools at education, the safety net at state and local.

**The glyph.** Each player is a disc of **dots, one per lifeline** (the dot and the line on the road share one highlight
id). A dot's height is its person's reason (high = the higher OS); gold dots are in control; a tribe dash names the
party; a head ring is split ice (fear) and rose (desire); a white **mirage** over the player is as large as its fantasy
spending; spouse links join married dots; standing marks (alpha, beta, omega) come from the season.

### The money: three heights

| Height | What moves | Look |
| --- | --- | --- |
| Underground | industries buying from industries: **roots** | the supplier's tier color |
| On the ground | money **leaving** people: spending and taxes | **rivers**: ice lane = fear, rose lane = desire, never blended; white glitter = fantasy; steel = taxes |
| In the air | money **reaching** people and capital moving | arcs: light blue = wages, gold = capital, steel = transfers |

* **Income in the air.** Wages arc from **wage patches** tiling each sector's wage strip (one patch per player, as large
  as its wages there) and from the towers of named employers ($405B in 27 arcs, by the companies' US employees); business
  income rises from the gold strips; capital income falls from the crown; transfers rise from the floor like fountains.
  The `people` view draws every player's own arcs (arcs under $5B are left out: $187B in 2025). Every other view draws
  one arc per kind of income, group and rim cluster of players (159 in 2025), starting at the dollar-weighted mean of
  their sources, so the arcs stay readable; widths stay in dollars. Owners' payouts rise into the crown as 8 sector
  streams (one per sector paying $250B or more, one per ring for the rest). The capital-only 1% stand on the crown
  itself: their capital income ($1.21T in 2025) starts and ends there and is not drawn.
* **Rivers on the ground.** Each player's spending runs inward as a rivulet; rivulets merge into creeks and enter the
  **lip canal**, a ring channel along the rim's inner edge with one lane per spending category. Each lane runs the shorter
  way round to its category's **fall**, drops over the lip and runs down the terraces (water never climbs: 0 uphill
  points), branching into the **pools** of the industries it is first paid to. Taxes ($5.25T, 87% federal) fall as a steel
  river to the government floor; imports ($2.27T) pour out over the rim.
* **The categories** are the notebook's: necessities, escapism, jeopardy, status, growth, collective (and saving, which
  rises to the crown). The canal's lanes are grouped and labeled as the notebook brackets them: BASE, SELFISH, MATING.

### The society: a season of tit for tat

The players play 96 rounds of **plain tit for tat** with their partners (each player's ten highest exposures: the same
industry, tier, group and generation; 916 pairs in 2025). A tie carries shares, not moves: x[p→q] is the share of p's
people who cooperate with q's. Openings come from in-group and out-group affinity calibrated to GSS trust (strangers
open at 0.308 in 2025); forgiveness comes from the members on the higher OS (generous tit for tat, a third of
defections forgiven); tribal memory from the default OS (toward the other party, it repays what its own side received);
partners who cooperate better than one's average are met more (partner choice). No random numbers: the same year and
settings give the same season bit for bit.

The `society` view shows each player's strongest dealings over the rim (its strongest tie and the ties among the three
strongest of both players: about 120 of the 916 pairs): blue within a coalition, violet across, grey and broken where a
pair feuds; brightness and width follow mutual cooperation. The **coalitions** found by modularity (CNM) at rounds 0,
24, 48, 72 and 96 are colored bands along the rim with one label each. Players never move: the land stays the
organization. The `betrayal` view replays the season with one incident: in round 48 the
largest cooperating cross-party tie is broken once, and the season shows how far it echoes.

### The mind

The thesis ("many buy fantasy, few control their path") is drawn on the players and the rivers, not on a separate
diagram: fear and desire are the ice and rose lanes and the head rings, reason is the dots' height, fantasy is the
glitter over the rivers and the mirages over the players, agency is the gold dots, plinths and the crown. The `mind`
view looks low across the rim so dots and mirages stand in profile, with the neurochemicals labeled at the falls and the
thesis line: "2025 · 12% of adults are in control · they buy as much fantasy as the rest (18% of an adult's spending vs
17%) · what sets them apart: reason 0.70 vs 0.36, and where their money comes from" (both fantasy shares are means over
adults; the 19% of all spending that buys fantasy is dollar-weighted, so it is not compared with them). Every comparison word
in a sentence is generated from its numbers with a ±10% band for "about the same".

## Views and controls

| Key | View |
| --- | --- |
| 1 | **The economy, 1946 - 2026**: the road, the cut and the bowl beyond it |
| 2 | **The cut through 2025**: every industry's band and every life that pierces the year |
| 3 | **Where value is created**: the five terraces, the sectors and their gold owners' share |
| 4 | **Where value is captured**: the towers and the crown |
| 5 | **Who stands where**: the players on the rim, their dots and income arcs |
| 6 | **Where the money goes**: the canal, the falls, the rivers and the pools |
| 7 | **Desire, fear and fantasy**: the rim in profile, dots, mirages, the thesis line |
| 8 | **How people organize**: the season's ties and coalitions |
| (bar) | **What each industry stands on** (roots), **One betrayal**, **The land in 1972** (the same cut, 53 years earlier) |

Each view eases every layer group toward its own emphasis (one table, `EconomyViews`), so a view shows its story at full
strength and the rest dimmed; no view rebuilds geometry.

| Input | Action |
| --- | --- |
| , and . | the year one back / forward (with Shift: ten); the cut slides along the road and the land opens the year |
| Year chip | ‹ › step; drag the scrubber to slide the cut, release to open the year. Under it the year's readout: "2025 · GDP $30.8T · in control 12% · fantasy 19% · fear 54% · trust 25% (GSS) · cooperation 0.73" |
| Click the wall | in views 1 and 2: open the year clicked |
| Hover the land | light a sector and its roots, a river, a tower, a tie; read its card |
| Click a player | the **player inspector**: who (the group's rule, its anchor and named employers, tribes, generations), its money (income by source, taxes, spending by category with fear and fantasy, saving, wealth, the identity "in = spending + saving + taxes"), its mind (reason, fear, future, fantasy, agency, in control), its place in the season, and "Show a member" |
| Click a dot or a lifeline | the **person inspector**: one person's household, work, money, motives and whole life; "Plays as" links to their player |
| Click a tower | its owners: the **ownership fan**, gold threads to every player in proportion to its equity dollars, plus abroad and pensions |
| N | the next notable person (the owner, the heir, the striver, the escapist, the indebted, the retiree, the young, the forgiver, the avenger); with a player open, its next member |
| Social panel | views 8 and betrayal: play, step, replay; forgiveness (by the OS / everyone / nobody), mistakes, the chance of meeting again, polarization, partner choice; select a tie and **Betray** to queue an incident at the next round |
| T | the guided tour, 12 stops (Space / Right next, Left back, P pause, Esc exit) |
| V, H, F3, Esc | vertical mode, help, stats and clearing, as in the causality graph (the time lens is the causality graph's only: every economy view shares one window of time) |

## The model

The scene is a model of an idea, drawn from data so it can be examined. Every parameter lives in
`Assets/Resources/Data/economy/*.json` with its source (`Docs/DATA.md`); the models are in
`Assets/Scripts/Economy/Model/` (the year's economy and the lives) and `Assets/Scripts/Economy/Land/` (the land).

### Industries (`industries.json`, `IndustryWallLayer`)

Twenty-five industries in five tiers, with BEA value added for every year from 1947 to 2025 (and a 2026 estimate),
summing to GDP every year. Each industry splits its value added by its 2024 input-output shares into compensation,
production taxes, depreciation and the owners' share (net operating surplus: profits, proprietors' income, rent and
interest); compensation is scaled over time with the economy's labor share.

### The money circuit (`circuit.json`, `MoneyCircuit`)

For any year since 1947 the circuit follows the money around once, in nominal dollars: industries add value; it splits
into wages, the owners' share, production taxes and depreciation; the four wealth groups of the Federal Reserve's
distributional accounts, the government and holders abroad receive it; taxes and transfers turn it into what each group
spends; spending returns to the industries. The 2025 circuit is calibrated to the national accounts; earlier years carry
it back with the industries' value added, BEA's government and trade accounts, the labor share, the wealth groups'
history and the spending history. Every node balances within 1.1% of GDP in 2025 and within 2.8% in every year; the land
legend prints the year's balance and names its gaps. The circuit is no longer drawn: the land is its picture (value added =
sectors, the split = strips, receivers = players, the state = the floor, uses = rivers, the return = pools, capture =
towers and the crown); it supplies the payouts abroad, the government's split and the accounts readout.

### Economic lives (`EconomicLives`, `LivesSimulation`, `TraitSampler`)

Every line of the population (4,996 lines, about 200,000 person-years from 1946 to 2026) gets a life:

* **At birth**: correlated personality traits (the Big Five, present bias, loss aversion, social comparison, locus of
  control); an earnings rank that partly inherits the parent's (rank-rank slope 0.35; Chetty et al. measure 0.34); a party
  (inherited with probability 0.6); a strategy for repeated dealings (tit for tat, generous tit for tat, win-stay
  lose-shift, always cooperate, always defect, grim trigger, random).
* **Every year**: employment and earnings by sex, age and rank; self-employment (the self-employed are never placed in
  government industries); capital income on what they own; Social Security and Medicare by entitlement; means-tested
  transfers; the 2020-21 emergency transfers; taxes by income percentile; marriages from the population's own, pooling
  money; children; spending in the seven categories, tilted by personality and life stage and calibrated every year to the
  data; saving, debt, homes and inheritance.
* **The state of mind behind the spending**: the share moved by fear rather than desire, the share of decisions made by
  reason (the notebook's higher OS) rather than feeling (the default OS), orientation to the future, the share of spending
  that buys a fantasy, cooperation, and **agency**.

**Agency** combines material autonomy (owning a business, or financial assets that cover three years of spending),
reason, saving, low debt service and low fantasy (`psyche.json`). A person is **in control** when their agency is high
*and* they have material autonomy; the cut is calibrated once so that 12% of adults are in control in 2025 (evidence
anchors range from 6% to 18%), and the same cut is applied to every year.

Calibration in 2025 (model vs data): compensation $15.73T (15.73), transfers $4.95T (4.95), personal taxes $5.25T (5.25),
disposable income $22.87T (22.87), saving rate 5.4% (5.4), homeownership 65.3% (65.3), wealth shares top 1% / top 10% /
bottom 50% 31.4 / 68.0 / 1.5% (DFA 31.4 / 67.8 / 2.5), children of the bottom fifth reaching the top fifth 7.2% (7.5%).
The log of every run (`[Why] economic lives`) prints the full table.

### The land's builders (`Land/`)

A year's **snapshot** (`LandService`) is built in this order; each builder is pure (no Unity objects, any thread),
deterministic, and logs a line with its checks and a checksum (8.8 of the spec):

1. **Layout** (`LandLayout`): ring radii, the barycentric order of the sectors on each ring and the ring offsets that
   put every buyer over its suppliers (computed once per load for all years), sector spans by value added, towers and
   plinths. **Roots** (`RootsModel`): the flows of the 2024 table, scaled to the year.
2. **Census** (`PlayerCensus`, `PlayerPlacement`): the groups, cells, party split, children, the members' money and
   minds, the motives by category (4.1), and the places on the rim.
3. **Money** (`MoneyRouting`, `SellerMatrix`, `CanalRouting`, `CapitalSources`): **first recipients** of each category's
   dollars from an (I − A) prior on the 2024 input-output table balanced by RAS to the players' category totals and BEA's
   personal consumption by commodity; rivulets, creeks, the canal (each lane the shorter way to its fall; falls at the
   weighted circular median of their recipients, at least 10° apart, taxes at the federal sector), monotone river beds,
   distributaries into the pools; income arcs, the crown's flows. Eight identities are checked every build: each player's
   budget closes, totals equal the lives', every junction balances, the pools plus imports equal spending, no water
   climbs, the crown balances, the tax river reaches the floor, the areas sum to 24.
4. **Season** (`SocialSeason`, `Coalitions`): pairs, openings, the 96 rounds, coalitions (CNM, numbered across
   detections), standing (alpha, beta, omega, anti-alpha) and signals (pain, relief, satisfaction). Four **control
   seasons** (nobody forgives, everyone forgives, no partner choice, polarization × 2) run after the load as light seasons
   (the rounds and their readouts only) for the log; the **betrayal** season is computed the first time the betrayal view,
   the tour or the panel needs it.
5. **Circuit**: `MoneyCircuit.Build(Y)` for the accounts readout.

## What the model says (2025)

These are the model's results, shown in the scene rather than tuned. Two of them rest on judged weights rather than
measurement: the fear / desire motive and the fantasy share of each spending category (spending.json), so the fear and
fantasy totals below are the spending mix times those judgments.

* **Few are in control.** 12.0% of adults own the means to steer their own path (by construction of the calibration):
  63% of the 1%, 49% of business owners, 4% of frontline workers. The history the same cut produces is the model's own:
  about 14 - 17% from 1950 to 1975, falling to about 11% from 1995 to 2015.
* **Fantasy does not separate them.** Fantasy is 19% of all spending (dollar-weighted), and people in control buy as
  much of it as the rest (on average 0.18 vs 0.17 of an adult's spending). Fantasy rises with income (0.14 of spending in the poorest fifth, 0.21 in the richest).
  What sets the people in control apart is reason (0.70 vs 0.36), age (61 vs 47), inheritance (61% had inherited from a
  parent vs 26%), self-employment (23% vs 4%) and where their money comes from: capital.
* **Fear moves more money than desire.** By the categories' judged motive weights, 54% of household money is spent
  moving away from something feared. The
  jeopardy river alone carries $6.90T, 85% of it fear; it falls next to health care, which receives more from households
  than it adds in value.
* **Owners keep about a quarter.** Owners keep 26% of GDP. $5.33T a year rises into the crown from the private sectors
  and falls on those who own; $435B leaves for owners abroad.
* **The bowl stands on roots that flow down as well as up.** $15.85T a year moves between industries; 37% of it comes
  down from a supplier on a higher terrace and only 22% rises from a lower one: the hierarchy is a picture of the
  notebook's order, not a law of the data, and the land says so.
* **Cooperation is learned, not given.** Strangers would open at 0.308 (GSS trust); partners, who share an industry,
  group or generation, open at 0.333; forgiveness lets cooperation climb to 0.686 in 12 rounds and settle at 0.729. If nobody forgave it would settle at 0.425; if everyone did, at 0.868. Without partner
  choice it reaches 0.717. Doubling polarization opens the gap between co-partisans and the other party (0.770 vs 0.685,
  from 0.741 vs 0.699).
* **Coalitions follow the land, not party.** Four coalitions form (41, 38, 28 and 9 players), with 69-80% of their
  dealings inside: professional services, health and hospitality; the retirees and the safety net; trade, manufacturing
  and construction; government. They match the anchor industry (NMI 0.56) and not party (0.01). This is partly by
  design: who deals with whom follows industry, tier, group and generation, party only tilts how a pair opens, and the
  model draws party independently of class.
* **One betrayal echoes and ends.** The round-48 betrayal between two comfortable retirees of opposite parties hits 2
  players and is calm after 7 rounds.
* **1972 was a different land.** The making terrace was 80% full and services 54%; 80 players; partners opened at
  0.584 (strangers 0.541: trust was higher) and cooperation reached 0.795, higher than in 2025.

## Limitations

**Measured and modeled.** The land mixes measurements and models; this ledger says which is which.

| Measured (data, with sources in `Docs/DATA.md`) | Modeled (choices, documented in the files and the spec) |
| --- | --- |
| Value added by industry 1947-2025, GDP | The 2026 estimate (first half annualized) |
| The split of value added (2024 shares), the labor share | Compensation over time scaled by the labor share |
| Purchases between industries (BEA 2024 Use table) | Roots of other years: the 2024 table scaled by each buyer's value added |
| Personal consumption by commodity, imports | First recipients: an (I − A) prior balanced by RAS (the flagged industries in the Money line are where the prior and BEA disagree most) |
| National income and personal accounts 2025; wealth groups (DFA) | The circuit's earlier years; routing between wealth groups |
| Companies' market value, revenue and profit (filings) | Their US employees (10-K where reachable, else recalled, ±20%, shown "≈"); named employers' wage arcs by those shares |
| The population (UN pyramids, marriage, divorce) | Every person's economic life, calibrated to the national totals |
| Group shares in the literature (Pew, BLS, SSA, Gilbert) | The twelve groups' priority rule and thresholds; cells; the dominance rule |
| GSS trust by year and age; party identification (ANES, GSS, Gallup); lab stranger cooperation (Sally 1995) | Affinity weights, exposure, forgiveness from the higher OS, tribal memory, learning, partner choice, pairs per player |
| Spending by category (PCE items) | Each category's fear and fantasy shares; the psychology (`psyche.json`) |

* The data's vintage predates BEA's annual update of 30 September 2026 (about 1% on 2025 levels).
* Household debt in the model is lower than in the data (debt to income 68% vs 91%): car and student installment loans
  are not modeled.
* With one line per 100,000 people, about 27 lines make the top 1%; small groups (students 3 players, public servants 4)
  are coarse. The census gives 116 players in 2025 (the spec's prototype gave 119 on an earlier lives run).
* The land is normalized to GDP: it shows composition, not growth (the road and the wall show growth).
* The season is a mean-field model of crowds (shares, not moves); its parameters marked "design parameter" in
  `games.json` were chosen, not measured. Party is assigned by the lives with national marginals but without Pew's
  education and class gradients: coalitions that ignore party are partly a consequence (the next model task).
* The ownership fan apportions a tower's equity by the model's capital income and the wealth groups' equity shares,
  with pensions and abroad as stubs: the insiders' stakes are not modeled.
* The capital-only 1% stand on the crown, so their capital income (about a fifth of it) starts and ends at the same
  place: its arcs have no length and it is read from the crown's label and the player inspector, not from an arc.
* The psychology (drive weights, tilts, the chemicals) is literature-informed modeling, mostly not verified against
  primary sources (marked in `psyche.json`). Social market value (the lifelines' height) is the causality graph's model.

## Architecture

* `GraphRoot` has a serialized scene id; `Economy.unity` sets it to `economy`. Layers and modules declare their scenes
  with `[GraphScenes(...)]`; types without it belong to the causality graph only, so that scene is unchanged (verified
  pixel for pixel headless). The economy scene reuses the time axis, the human world, the United States population
  (`SmvLayer`), the HUD, the lens and the director.
* `EconomyLoaderLayer` (Order 25) loads the data and publishes `EconomicLives` as the population's line style, so the
  lives are simulated on the finished population inside `SmvLayer.Prepare`. `IndustryWallLayer` (40) builds the wall.
* `LandModelLayer` (45, tier 4) builds the default year's snapshot: layout, census, then the money and the season in
  parallel, the circuit; it prints the log lines and shares the snapshot (`economy.land`). `LandService` builds other years
  on a worker (or blocking for a preset), caches the last few, publishes them (`Changed`) and moves `ShownYear` once every
  land layer has swapped. `LandViewLayer` (44) applies a preset's `ViewSpec` (`EconomyViews`): the morph target, a
  temporary year, the held round, the betrayal season, the emphasis targets; every frame it runs the morph clock, eases
  each layer group's alpha and steps the season's playback (`LandView`).
* The land layers (tier 5) read the snapshot and draw: `LandscapeLayer` (50: terraces, sectors, pools, roots, towers,
  crown, overlays), `InhabitantsLayer` (52: glyphs, dots, mirages, standing marks), `FlowsLayer` (53: income arcs, capital
  flows, the canal, falls, rivers, glitter, taxes, the ownership fan), `SocialLayer` (54: ties, signals, coalitions) and
  `SectionLayer` (56: the cut, its card and dots, the unfold). Each rebuilds on a worker when a new snapshot arrives,
  cross-fades the old meshes out and reports ready. The land is drawn in plain world space (`GraphMaterials.Raw`) in the
  frame `EconomyStage.Land()`; its labels and anchors are `Fixed` world positions.
* The UI modules share one state, `EconomyState` (year, the scrubber's preview year, person, selection, the season's
  settings, a queued incident): `EconomyControls` (the year chip and scrubber), `LandPicker` (`LandPick`'s pure hit tests),
  `PlayerPanel`, `PersonInspector`, `SocialPanel`, `LandLegend`; `EconomyUiLayout` keeps the panels clear of the bowl at
  both aspects.
* `ViewPresets` keeps the scene's catalog (`EconomyPresets`): the road views aim at the road under the one shared lens
  (`EconomyStage.TimelineWarp`), the land views at land-local poses.
* Determinism: no random numbers in the land; every sort has an index tie-break; the harness prints every builder's
  checksum, and two runs print the same.

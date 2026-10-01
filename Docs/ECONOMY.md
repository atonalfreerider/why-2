# The economy scene

`Assets/Scenes/Economy.unity` is a second scene built on the same rendering core and the same United States
population as the causality graph. It adds a **corporate layer** and the **money circuits** that connect industries,
owners, households and consumers, and it gives every simulated person an economic life and a psychology: what they
earn and own, what they buy, whether they buy it moving toward something they want or away from something they fear,
and whether they steer their own life path. Beyond the present end of the timeline stand three diagrams: the money
circuit of any year, a map of the mind behind the money, and the prisoner's dilemma between people and between tribes.

Open the scene and press Play. Everything is built at runtime in about two seconds, as in the causality graph.

## Reading the scene

| | |
| --- | --- |
| **The road** | Time runs along a straight road from 1946 (left) to now (right). Every view of the scene shares this one road, so it never moves; only the camera does. |
| **People** | The blue lines above the road are the United States population from 1950 to now, exactly as in the causality graph: one line for 100,000 people, women on the inner side and men on the outer side, height = modeled social market value. A line turns **gold** for the years its people are **in control** of their own path (see *Agency* below). |
| **The industry wall** | Beneath the people stands a wall of the industries that pay them: value added by industry each year in 2025 dollars, stacked in the notebook's order from the ground up - government (the foundation), raw (oil, gas and metals in matter red, farms in life green), manufacturing and infrastructure, services, and tech nearest the people. The wall is as tall as the real economy: it grows almost tenfold along the road. Each industry's band is split by who receives its value: the dim lower part pays wages, a faint sliver pays production taxes and replaces worn-out capital, and the **bright top is what owners keep**. |
| **Money threads** | Threads connect the wall and the people at sampled years: income rising from the industry a person works in (blue) or owns (gold), spending falling back to the industries that capture it, tinted by motive. Pulses travel with the money. |
| **Hue** | Gold = capital (corporations, profits, people in control); blue = people; **rose = desire** (money spent moving toward what people want); **ice = fear** (money spent moving away from what people fear); steel = government; red and green only for raw matter and life. Text and axes are neutral grey. |
| **Stations** | Beyond the present end of the road: the **money circuit** (key 3, and the companies that capture the most in key 4), the **mind** (key 6) and the **games** (keys 7 and 8). They are diagrams in plain world space; their labels show only while the camera is near them. |

## Views and controls

| Key | View |
| --- | --- |
| 1 | **The economy, 1946 - 2026**: people above, industries below, money between |
| 2 | **Where value is created**: the industry wall since the 1970s, the bright parts kept by owners |
| 3 | **The money circuit** of the selected year |
| 4 | **Where value is captured**: profits, payouts and the most valuable companies |
| 5 | **Who owns their path**: the population in detail, the people in control in gold |
| 6 | **Desire and fear**: the mind map of the selected year |
| 7 | **Cooperation**: the prisoner's dilemma between two people |
| 8 | **Tribes**: tit for tat between two tribes, and the evolution of strategies |

| Input | Action |
| --- | --- |
| , and . | the year of the money circuit and the mind map (with Shift: ten years) |
| Click a lifeline | look inside one person: income, spending, motives, agency, the games they play |
| N | the next notable person (the owner, the heir, the striver, the escapist, the indebted, the retiree, ...) |
| Games panel | choose two strategies, the chance of mistakes and the chance of meeting again |
| T | the guided tour (Space / Right next, Left back, P pause, Esc exit) |
| U, [ ], L, V, H, F3, Esc | the time lens, vertical mode, help, stats and clearing, as in the causality graph |

## The model

The scene is a model of an idea, drawn from data so it can be examined. The industries, the national accounts, the
wealth groups and the population are measured; how money is routed between groups, the motives behind each kind of
spending and the psychology of each person are modeling choices informed by the literature. Every parameter lives in
`Assets/Resources/Data/economy/*.json` with its source (see `Docs/DATA.md`), and every model is in
`Assets/Scripts/Economy/Model/`.

### Industries (`industries.json`, `IndustryWallLayer`)

Twenty-five industries in five tiers, with BEA value added for every year from 1947 to 2025 (and a 2026 estimate),
summing to GDP every year. Each industry splits its value added by its 2024 input-output shares into compensation,
production taxes, depreciation and the owners' share (net operating surplus: profits, proprietors' income, rent and
interest); compensation is scaled over time with the economy's labor share. Tech (software, internet platforms,
chips, media) is about 8% of GDP; services are about 60%.

### The money circuit (`circuit.json`, `MoneyCircuit`, `CircuitLayer`)

For any year since 1947, the circuit follows the money around once, in nominal dollars:

1. **Industries add value** (the five tiers, summing to GDP).
2. **Split**: wages, the owners' share, production taxes, depreciation.
3. **Who receives it**: the four wealth groups of the Federal Reserve's distributional accounts (bottom 50%, 50 - 90%,
   90 - 99%, top 1%) by their shares of wages, business income, equities, real estate and interest; the government
   (production, corporate and personal taxes); holders abroad; reinvestment.
4. **After taxes and transfers**: what each group has to spend, with the state's transfers and its borrowing.
5. **What it is spent on**: the notebook's seven categories (necessities, escapism, jeopardy, status, growth,
   collective, saving), public goods, investment and exports.
6. **Back into industries**: every use returns to the tiers that capture its value along the supply chain (the 2024
   input-output table), closing the loop under the floor.

The 2025 circuit is calibrated to the national income and personal income accounts; earlier years carry it back with
the industries' value added, BEA's annual government and trade accounts, the labor share, the wealth groups' history
and the spending history. Every node balances within 1.1% of GDP in 2025 and within 2.8% in every year; the remaining
gaps (income from abroad, government fees, the statistical discrepancy) are named in each year's notes.

### Economic lives (`EconomicLives`, `LivesSimulation`, `TraitSampler`)

Every line of the population (4,996 lines, about 200,000 person-years from 1946 to 2026) gets a life:

* **At birth**: correlated personality traits (the Big Five, present bias, loss aversion, social comparison, locus of
  control) from published distributions and correlations; an earnings rank that partly inherits the parent's (the
  model's rank-rank slope is 0.35; Chetty et al. measure 0.34); a party (inherited with probability 0.6); a strategy
  for repeated dealings with others (tit for tat, generous tit for tat, win-stay lose-shift, always cooperate, always
  defect, grim trigger, random), in the population shares lab evidence suggests.
* **Every year**: employment and earnings by sex, age and rank; self-employment; capital income on what they own; Social
  Security; transfers and taxes by income percentile; married couples (from the population's own marriages) pool
  their money and children add needs; spending in the seven categories, tilted by personality and life stage and
  calibrated every year so the population matches the data; saving, debt, homes and inheritance (spouse first, then
  children).
* **The state of mind behind the spending**: the share moved by fear rather than desire, the share of decisions made by
  reason (the notebook's higher OS) rather than feeling (the default OS), orientation to the future, the share of
  spending that buys a fantasy, cooperation with others, and **agency**.

**Agency** combines material autonomy (owning a business, or financial assets that cover three years of spending),
reason, saving, low debt service and low fantasy, as defined in `psyche.json`. A person is **in control** when their
agency is high *and* they have material autonomy; the cut is calibrated once, so that 12% of adults are in control in
2025 (the evidence anchors range from 6% to 18%: business owners, written financial plans, financial runway, retirement
on track), and the same cut is applied to every year.

Calibration in 2025 (model vs data): compensation $15.7T (15.7), transfers $4.95T (4.95), personal taxes $5.25T (5.25),
disposable income $22.9T (22.9), saving rate 5.4% (5.4), homeownership 65% (65), wealth shares top 1% / top 10% /
bottom 50% about 31 / 66 / 2.5% (31 / 68 / 2.5), dollar millionaires 9% of adults (8.9%), children of the bottom fifth
reaching the top fifth 7.2% (7.5%). The log of every run (Unity console, `[Why] economic lives`) prints the full table.

### Games (`games.json`, `PrisonersDilemma`, `GamesLayer`)

The prisoner's dilemma with Axelrod's payoffs (temptation 5, reward 3, punishment 1, sucker 0). One round always ends in
mutual defection (the Nash equilibrium). Repeated with a chance of meeting again, cooperation can climb the Pareto
ladder: tit for tat cannot be exploited when the chance of meeting again is at least two thirds. Mistakes start feuds
between tit-for-tat players; generous tit for tat (forgiving a third of defections) ends them. Two tribes repay each
other in kind on what their tribe received, with a distrust of the other tribe taken from the in-group literature; the
floor shows how strategies spread by their payoff over 200 generations (replicator dynamics with mutation).

## What the model says

These are the model's results for 2025, shown in the scene rather than tuned:

* **Few are in control.** About 12% of adults own the means to steer their own path, by construction of the
  calibration; the history the same cut produces is the model's own: about 13 - 17% from 1950 to 1975, falling to 9 -
  10% around 2005 - 2015.
* **Fantasy does not separate them.** People in control spend the same share on fantasy (0.17) and the same share out
  of fear (0.55) as everyone else. Fantasy spending *rises* with income (0.14 of spending in the bottom quintile, 0.21
  in the top). What distinguishes the people in control is reason (0.69 vs 0.36 for everyone else), age (61 vs 47),
  inheritance (62% inherited money vs 28%) and self-employment (23% vs 4%).
* **Fear moves more money than desire.** About 54% of household money is spent moving away from something feared:
  health and insurance, debt, legal protection, basic shelter.
* **Owners keep about a quarter.** Wages take about 51% of GDP; the owners' share (profits, proprietors' income, rent
  and interest) is about 26%; the top 1% receive a third of all corporate payouts (dividends and buybacks) and the top 10% about two thirds (four fifths of what stays in the country); the bottom half receives about 1%.

## Limitations

* The data's vintage predates BEA's annual update of 30 September 2026 (about 1% on 2025 levels).
* Household debt in the model is lower than in the data (debt to income 64% vs 91%): car and student installment loans
  are not modeled.
* With one line per 100,000 people, only about 27 lines make the top 1%: its wealth share varies by several points with
  the random seed (the scene's seed is fixed, so it is reproducible).
* The psychology (drive weights, tilts, the chemicals) is literature-informed modeling; most of it could not be verified
  against primary sources in this round and is marked so in `psyche.json`.
* Social market value (the height of the lifelines) is the causality graph's model and does not read the economy.

## Architecture

* `GraphRoot` has a serialized scene id; `Economy.unity` sets it to `economy`. Layers and modules declare the scenes they
  belong to with `[GraphScenes(...)]`; types without it belong to the causality graph only, so that scene is
  unchanged (verified pixel for pixel headless). The economy scene reuses the time axis, the human world, the United
  States population (`SmvLayer`), the HUD, the lens and the director.
* `SmvLayer` publishes the finished population (`SmvPopulation`) and lets another scene's model restyle its lines
  (`ISmvLineStyle`): `EconomyLoaderLayer` (Order 25) publishes `EconomicLives` as that style, so the lives are simulated
  on the finished population inside `SmvLayer.Prepare` and the lines are drawn with them.
* Stations are drawn in plain world space: materials with `_Raw` = 1 place vertices through the renderer's transform
  instead of the warp (`GraphMaterials.Raw`, `WhyPlace` in the shaders); their labels and anchors are `Fixed` in world
  space and hide while the camera is away. Lines can pulse along a path rather than along time
  (`LineMeshBuilder.AddFlowPath`).
* `ViewPresets` keeps a catalog per scene; economy views either look at the road (`OnRoad`) or at a station
  (`FixedTarget`, `FixedYaw`) under the one shared lens (`EconomyStage.TimelineWarp`).

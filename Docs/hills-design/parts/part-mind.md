# Part: the mind — each player's program and its 3D view

The desire / fear brain of the notebook as (a) **the program every player runs**, year by year, in the landscape game,
and (b) **a separate 3D view** of that program running. One design; numbers, not adjectives.

* `$SP` = `/tmp/claude-0/-home-user-why-2/dc9bf553-91de-54d2-9f1f-9831e1f24cfa/scratchpad`. Paths are under
  `Assets/Scripts/Economy/` unless they start with `Assets/` or `Docs/`. The repository was not edited.
* **[proto]** numbers come from `$SP/redesign2/mind/` (Appendix): a private copy of HEAD 16262b8 with a scratch dump
  layer, run in the headless harness (`WHY_NOW=2026-10-01T12:00:00Z`, seed 2026), then `proto.py` / `listing.py` /
  `mock.py`. **[proto C#]** = measured by the C# change itself in that copy (the allowance step of 2.4 was implemented
  there and run). **[code]** = read in the repository. **[data]** = `Assets/Resources/Data/economy/*.json`.
  **[recalled]** = to verify before it enters a data file. **[design]** = a judgment of this part, printed as such.

---

## 0. The design on one page

**One program, run by every household, every year.** The lives (`Model/LivesSimulation.cs`) already run a full
decision loop per household per year: they carry a balance sheet and last year's stress (memory), take the year's
income, prices and family (world), weigh five desires against five fears by age and state (feel), set a share of
decisions made by reason (the OS), choose a saving rate and a spending mix with a motive (decide), settle debts, homes
and assets (act), and play tit for tat with their partners (play). This part **names those stages, records what each
stage computed** (a compact trace, no change to any number), **adds the three pieces the notebook asks for that the
lives lack**: an **allowance** from parents to the adult children who live with them (a real transfer between
households: $379B in 2025 [proto C#]), the **seven neurochemicals** and the **five modes** as derived, calibrated
per-person indices, and the notebook's **signals** (pain, relief, satisfaction) as life events.

**A player's program is its members' program, summed.** A player (a group of people) runs no extra code: each member
household runs the program; the player's `ProgramYear` is the sum of their dollars and the mean of their minds, with the
spread (p10-p90) kept. The season of tit for tat (`SocialSeason`) is the one stage that really runs at the player level.
Every `ProgramYear` prints as a 9-line **program listing** (MEMORY, WORLD, FEEL, OS, DECIDE, ACT, PLAY, OUTCOME):

```
frontline · trade · I · 2025 · 6.8M adults, 3.7M children, 7 adult children at home          [proto C#, 2025]
MEMORY  net worth $99K/adult (median $32K) · debt $58K (consumer $14K) · runway 0.0 yr · own a home 40% · inherited 7%
WORLD   wages $341B · capital $36B · transfers $80B · allowance in $7B · taxes $70B → disposable $397B
FEEL    fear 55%: exposure .32 starvation .29 murder .23 isolation .15 · desire 45%: shelter .32 food .25 collective .25
        stress 0.64 → cortisol 1.08 · testosterone 1.11 · oxytocin 0.89 · rumination 13% · task 36%
OS      reason 0.36 (24% on the higher OS) · future 0.55 · present bias β 0.91 · loss aversion λ 2.13
DECIDE  save 2.1% · spend $389B: necessities 42% · jeopardy 31% · status 13% · escapism 8% · collective 4% · growth 1%
        fantasy 16% · allowance out $2B
PLAY    tit for tat · opens 0.31* · forgives 0.08 · tribal memory 0.38 · cooperation 0.73* · beta*
OUTCOME agency 0.39 · in control 1% · pain 6% · relief 1% · satisfaction 7%
```

(* the season was not run for this prototype listing: opening and cooperation are the 2025 means of the season's log,
p0 0.308 and round-96 cooperation 0.729, and the standing is the majority rank; forgiveness = 24% / 3 and tribal
memory = 0.5 × 1.0 × (1 − 0.24) are this player's.)

**The landscape plays the years.** `Space` plays 1950 → 2025 at 1.5 s a year: each year is a turn; every household runs
its program; the census regroups the people into players; players glide (0.8 s) to where their new balance sheets,
jobs and businesses put them; crowns and clouds grow, shrink, appear; signals flash on the players whose members were
hurt, relieved or satisfied that year. The year's **score** per player is the change of its members' net worth, split
into saving, holding gains, inheritance, allowance, debt written off and business closures.

**The mind is a separate 3D object** standing 6.5 u to the left of the land: a **brain-shaped wireframe** (an ellipsoid
5.6 u long), **fear hemisphere left (ice), desire hemisphere right (rose)**, the 10 drives as glowing nodes in its deep
floor, the 7 neurochemicals as vials hanging beneath it, the **reason waterline** dividing the lit cortex (higher OS,
white) from the limbic rest (default OS), the 5 modes + daydream as an orbit of time shares around it, the joint goals as
three arches across the fissure, **memory** (the balance sheet) as gold bars behind it, **world** (income) as pipes
entering its brainstem from behind, and **strategy** (the decisions) as conduits leaving its prefrontal front, split
ice / rose by motive with fantasy glitter, toward the land. `G` runs one year of the selected player's program as a
12-second sequence of lit stages. `P` switches to the **population mode**: the nation's program, with every player as
a particle placed by fear (x), reason (y) and future (z), trailing where its people's minds were for ten years. Click a
player on the land and press `M`: the camera flies to its mind.

Mockups at the spec's geometry and the 2025 numbers [proto]: `$SP/redesign2/mind/mock-player-frontline-trade.png`,
`mock-player-top1.png`, `mock-population.png` (a CPU wireframe; the C# does not exist yet).

---

## 1. What the lives already compute (per person, per year) [code]

`EconomicLives.Prepare` runs `TraitSampler` (pass A, traits at birth) then `LivesSimulation.Run` (pass B, year-major
1946-2026, 607-740 ms, 200,020 person-years, 33.6-35.2 MB). Per year: `Households` → `Individuals` → `Incomes` →
`SpendingAndSaving` → `Settle` (`Mind`, `Balance`, records) → after the loop `Finish` (tribes, cooperation, wealth groups).

| Stage | What exists | Where | Stored? |
| --- | --- | --- | --- |
| **Traits (fixed)** | Big Five z (C, N, A, O, E), present bias β, loss aversion λ, social comparison, locus, earnings rank and the parent's rank (rank-rank 0.34 copula), reason propensity, PD strategy, lean → tribe, enterprise, 5 desire and 5 fear weights (lognormal σ 0.3 around psyche.json weights), SS claim age | `TraitSampler.Sample` | `PersonTraits` |
| **Memory (state carried)** | balance sheet `fin`, `house`, `mort`, `debt`, `biz`, mortgage age; career (SS); last year's thin-buffer factor `prevBuffer` = clamp01(1 − 4 runway) and debt service `prevDebtService`; saving rates of the last 2 years `save1`, `save2`; `hasChild`; employment latent (AR(1)); industry (sticky) | arrays in `LivesSimulation` | only `Wealth`, `Debt`, `ConsumerDebt`, `Runway`, `DebtService` in `PersonYear` |
| **World (inputs)** | age; marriage (`MarriageLog`); deaths → `Bequeath` (spouse, else children, grandchildren, parents, siblings); separations sell the home; children in the mother's household; employment vs the era's rate; self-employment; industry; earnings (age profile × rank, median-scaled); capital income on wealth; SS; Medicare; means-tested transfers (a dependent is tested with its parents); taxes by percentile; the era's mortgage rate, card APR, equity return, home prices, wage index | `Households`, `Individuals`, `Incomes` | income fields, `Inherited` |
| **Feel (drives)** | per adult: desire k = trait weight × life-stage curve (sex × 0.7 if married), pole-normalized; fear k = trait × stage (childless: fertile window only, 0 for parents; isolation × 0.75 married / × 1.25 single; starvation and exposure × (1 + last year's thin buffer)); the household's drives = mean of its adults | `Drives`, `HouseholdDrives` | no (recomputable per person by `EconomicLives.DriveWeights`) |
| **OS (reason)** | reason = σ(propensity + offset + maturity(age) − stress + year shift); stress = 1.0 × last debt service / 0.4 + 0.5 × jobless (22-61) + 0.4 × last thin buffer; the year shift keeps the adults' mean at 0.40 (psyche.json `os.higher.share`) | `Individual`, `Individuals` | `Reason`; the stress term no |
| **Decide (saving)** | rate = base(income percentile) + traits (savingRatePp incl. reason z) + life stage + 1 pt per child up to 4, + one shift so the aggregate hits the year's saving rate; floor = −(assets + borrowing room), 0 for dependents; a large uninsured bill (jeopardy realized) | `HouseholdTilts`, `SpendingAndSaving` | `Saving`, `Spending`, `Loss` |
| **Decide (spending)** | share_c = base_c(income percentile) × exp(clamp(trait tilts + drive pull + children + age (health, student) + debt interest, ±3)), then one factor per category for the year's PCE mix | `CategoryTilts`, `Align` | the 6 shares; the parts no |
| **Motive** | fear share = Σ share_c × fearShare_c; fantasy = Σ share_c × fantasy_c; future (β and saving); agency = 0.35 autonomy + 0.15 saving + 0.10 debt + 0.25 reason + 0.15 fantasy; in control = agency ≥ cut (0.843) with autonomy | `Mind` | yes |
| **Act** | mortgage principal, repay consumer debt or draw assets then borrow, discharge (debt > limit), equity share by wealth and λ (risk), price gains, business value, elderly sell, renters buy to the era's ownership rate | `Balance`, `Settle` | `Wealth`, `Debt`, `Homeowner`, `Discharged`; flows and risk no |
| **Play** | strategy vs the year's partner mix, spouse, out-tribe distrust | `Finish`, `Cooperation` | `Cooperation` |

**What the lives do not compute** (this part adds 2.4-2.7): money from parents to the adult children who live with them
(today a dependent is its own household with its own income and may not dissave: a student with no job spends only its
transfers); the chemicals; the modes per person (psyche.json has population shares only); the signals; the flows of the
balance sheet (borrowed, repaid, gains, closures) and the risk share in the record.

---

## 2. The program (per household, per year)

### 2.1 The pipeline

The program is the lives' year for one household `h` (one or two adults, their minors), with the stages named after
the notebook. Formulas are the code's; **(cal)** marks a quantity rescaled every year so the population matches the data.

| # | Stage | Inputs | Computes | Outputs (to the next stage, the record and the land) |
| --- | --- | --- | --- | --- |
| 1 | **MEMORY** | last year's record | balance sheet at the start; runway; debt service; thin buffer; saving history; inherited so far; the parent's rank | `Financial0, Home0, Business0, Mortgage0, Consumer0`, `Buffer`, `DebtService0` |
| 2 | **WORLD** | the era (rates, prices, employment, wage index), the family log, the deaths | employment, industry, earnings (cal), business (cal), capital (cal), SS, Medicare, transfers (cal), estates in, **allowance in/out (new)**, taxes (cal) | income by source, `Disposable` |
| 3 | **FEEL** | traits, age, married, parent, `Buffer` | 10 drive weights (household mean); the household's fear share comes out of stage 5; **stress** = 1.0·DS + 0.5·jobless + 0.4·Buffer; **chemicals** (2.5) | `Desire[5]`, `Fear[5]`, `Stress`, `Chemicals[7]` |
| 4 | **OS** | propensity, maturity(age), stress, year shift (cal) | reason (higher OS when ≥ 0.5); future; **modes** (2.6) | `Reason`, `Future`, `Modes[6]` |
| 5 | **DECIDE** | income percentile, traits, reason, drives, children, age, interest, the year's saving rate and PCE mix (cal) | saving rate = base + traits + age + kids + shift; spending; 6 shares with their 5 tilt parts; fear and fantasy dollars | `Saving`, `Category[6]`, `CategoryFear[6]`, `CategoryFantasy[6]` |
| 6 | **ACT** | saving, balance sheet, mortgage rate, APR, equity and home prices, the home market | principal; repay or borrow; discharge; equity share (risk); gains; business value; sales; purchases | `Financial1 … Consumer1`, `Borrowed`, `Repaid`, `Gains`, `Discharge`, `Closure`, `EquityShare` |
| 7 | **PLAY** | strategy, spouse, tribe; at the player level the season (opening, forgiveness = higher-OS share / 3, tribal memory = 0.5 · pol · (1 − higher-OS share)) | cooperation; standing; coalition | `Cooperation`, `Standing` |
| – | **OUTCOME** | stages 1-7 | agency; in control; **signals** (2.7) | `Agency`, `InControl`, `Pain`, `Relief`, `Satisfaction` |

**The loop closes across years** (this is what makes it a game): stage 6's balance sheet is next year's stage 1; a thin
buffer this year raises next year's fears of starvation and exposure (stage 3) and lowers next year's reason (stage 4);
next year's debt service is stress; the wealth that saving and gains build moves the household up the wealth groups,
which the census turns into a new place on the land (floor → slope → crown / cloud); estates move memory from one
generation to the next; allowances move income from parents to their adult children.

### 2.2 The trace: recording what each stage computed (no number changes)

`LivesSimulation` gains a sink, `ProgramTraceStore` (new, `Model/ProgramTrace.cs`), written at the points where the
values are computed, one row per **household-year** (both spouses' slots point to it):

| Field | Type, scale | Written in |
| --- | --- | --- |
| `D0..D4`, `F0..F4`: the household's drives as used | byte, × 250 | `CategoryTilts` (after `HouseholdDrives`) |
| `Stress` (mean of the adults), `Buffer` | byte, × 100 (0-2.55); byte × 250 | `Individual`; `HouseholdTilts` |
| `SaveBase, SaveTraits, SaveAge, SaveKids` | short, 1e-4 (±3.27) | `HouseholdTilts` |
| `Tilt[c, part]`: 6 categories × (trait, drive, children, age, interest) | short, 1e-3 log units | `CategoryTilts` |
| `EquityShare` | byte × 250 | `Balance` |
| `Flags`: floor bound, shock, bought home, sold home, defaulted, dependent, received allowance, paid allowance | byte (bits) | `SpendingAndSaving`, `Balance`, `Settle` |

Size: about 110,000 household-years × 90 B + a 4-byte row index per store slot = **10.7 MB** (the lives hold 35 MB).
Writes happen inside the existing `Parallel.For` bodies, each to its own household's row: deterministic. Year
constants (saving shift, category factors, reason shift) are already in `YearCalibration`; `EconomicLives` exposes them
(`CalibrationOf(year)`).

**`PersonYear` gains** (record writer only, money split evenly in a couple as every money field is): `Financial`,
`Home`, `BusinessEquity` (so `Wealth = Financial + Home + BusinessEquity − Debt` exactly), `AllowanceIn`,
`AllowanceOut`, `Borrowed` (new consumer debt), `Repaid` (mortgage principal + consumer debt repaid), `Gains`
(holding gains), `DischargeAmount`, `ClosureLoss`, and `Dependent` (bool). `Financial`, `BusinessEquity`, `Dependent`
and the `EstateLog` are the people part's 3.2 request: **this part's WP-M1 writes all of them** in one change.

**Acceptance (measured):** with the allowance off, the lives' log is byte-identical to HEAD's except the size and
timing line. [proto C#: two int fields added to `PersonYear` and written in `RecordHousehold` and `Settle`; the 27-line
lives report diffed against HEAD's run: 0 lines differ.]

### 2.3 Families in the program: what each household knows of its kin

| Account | Rule | 2025 |
| --- | --- | --- |
| **Children (minors)** | in the mother's household (`kids`), raise its needs (kids tilt on necessities, saving −1 pt each) | 727 child lines (72.7M) |
| **Child cost** (display) | the household's spending × 0.3 k / (1 + 0.5 [spouse] + 0.3 k) (OECD-modified scale), k = kids ≤ 4: an accounting split, not new money | per player in the listing; the people part's 7.1 stem pulses use it (renamed from "allowance") |
| **Adult children at home** (dependents) | single, no child, no home, under 25 (`OwnHouseholds`) | 295 lines [proto] |
| **Allowance** (new, 2.4) | parents' household → dependent's household, real money | **$379B**, 175 of 295 dependents receive [proto C#] |
| **Estates** | `Bequeath` (exists) + `EstateLog` (people part 3.2) | the people part's 7.3 |
| **Inherited share, parent's rank** | `InheritedFromParentsBy`, `PersonTraits.ParentRank` | in MEMORY |

### 2.4 The allowance (the one model change)

At the end of `Incomes` (after taxes, before the saving rate), two passes over the heads in index order:

```
pass 1 (reads only): for each dependent household d with a living parent's household P = ParentHead(d), P ≠ d, Yd_P > 0:
    eq_P   = 1 + 0.5 (adults_P − 1) + 0.3 min(4, kids_P)          // OECD-modified equivalent adults of the parents
    need_d = 0.5 Yd_P / eq_P                                       // the family's standard for one more adult (weight 0.5)
    A_d    = φ · clamp(need_d − Yd_d, 0, 0.25 Yd_P)                // φ = 0.5: parents fund half the gap, at most 1/8 of Yd
pass 2 (writes, index order): Yd_d += A_d; Yd_P −= A_d; allowOut_P += A_d
```

Everything after it (saving rate, spending, balance sheets) runs as today on the new disposable incomes. Income
percentiles (from market income + SS) and taxes are computed before and do not move. National totals do not move (it
is a transfer inside the household sector); the year's saving calibration absorbs the redistribution.

* **Basis.** φ = 0.5 and the 0.25 cap are **[design]**. Anchors **[recalled]**: parents spend about $500B a year on
  adult children aged 18-34 (Merrill Lynch & Age Wave 2018); 59% of parents of 18-34-year-olds helped financially in
  the past year (Pew Research Center, January 2024). The model's dependents are the under-25s living at home, so its
  total should fall below the 18-34 figure: 2018 ≈ $260B.
* **Measured [proto C#]** (the step implemented in the private copy, `$SP/redesign2/mind/allowance_proto.diff`):

  | | 1972 | 2000 | 2018 | 2025 |
  | --- | --- | --- | --- | --- |
  | total | $20B | $155B | ≈ $260B | **$379B** (1.7% of DPI) |
  | receivers / dependents | 104 / 153 | 143 / 216 | | **175 / 295** (59%) |
  | per receiver mean / median | $2.0K / $1.4K | $10.8K / $7.0K | | **$21.7K / $14.7K** |
  | paying households | 162 | 206 | | 263 |

  2025 by group: **the PMC pays $312B (82%)**, frontline $30B, business owners $16B, office $10B; **students receive
  $128B, the working poor $148B**, gig $35B, public $25B, frontline $23B. The largest payers are PMC players
  (`pmc|tier:tech|M` $30.6B, `pmc|manufacturing|I` $27.1B); the largest receivers `students|schools/genz|I` ($67.7B of
  its $143B disposable).
* **Side effects [proto C#], the lives' report vs HEAD:** totals by source unchanged to the cent; 2025 category factors
  move ≤ 0.02; the in-control cut 0.843 → 0.845 (share recalibrated: 12.0%); net worth 2025 $152.11T → $151.10T (the
  parents' saving is spent by their children); top 1% share 31.4% → 31.1%; homeownership 2025 65.3 → 65.5%; one notable
  changes (indebted #1160 → #1276); players 2025 116 → 118. The landcheck expectations move with them (8.3).

### 2.5 The seven neurochemicals (a derived index)

For each adult-year, from the household's drives (trace) and the spending's fear share:

```
a_k   = (1 − fearShare) · desire_k        (k = food, collective, law, shelter, sex)
a_5+k = fearShare · fear_k                (k = starvation, isolation, murder, exposure, childless)
chem_c = Σ_k a_k · M[k, c]                M[k, c] = 1 / |chemicals(k)| when c ∈ psyche.json drive k's "chemicals"
chem_cortisol   += 0.25 · Stress          (chronic: the reason logit's stress term)
chem_adrenaline += 0.25 · [Loss > 0 or Discharged]   (acute: a large bill, a default)
Index_c(y) = chem_c / (mean over adults of 2025 of chem_c)      → 1.00 = the average adult of 2025
```

The memberships are psyche.json's own (food: dopamine, endorphins; collective: oxytocin, endorphins, dopamine; law:
serotonin, oxytocin; shelter: serotonin, endorphins; sex: dopamine, testosterone, oxytocin, endorphins; starvation:
cortisol; isolation: cortisol, oxytocin; murder: adrenaline, cortisol, testosterone; exposure: cortisol, adrenaline;
childless: cortisol, oxytocin). The two state gains (0.25) are **[design]**. The view says "an index of what drives the
spending, not a measurement" (psyche.json's caveats on each chemical are the hover text).

[proto] 2025, 118 players: cortisol 0.60-1.52 (the 1% 0.60-0.70; `out_of_work|safety_net/genx|I` 1.47); testosterone
0.71-1.33 (students 1.22-1.33); serotonin 0.77-1.29 (`pmc|health|I` 1.21); oxytocin 0.80-1.39; dopamine 0.84-1.23;
adrenaline 0.68-1.49; endorphins 0.79-1.17. 1972 vs 2025 (adult means): dopamine 1.12, serotonin 1.13, endorphins
1.13, oxytocin 0.99, testosterone 1.02, **cortisol 0.88, adrenaline 0.90** (debt and thin buffers raised stress since).

### 2.6 The five modes (+ daydream): a derived, calibrated time budget

psyche.json gives population shares (converse 0.14, learn 0.05, task 0.34, deep thought 0.05, rumination 0.12, and the
residual daydream 0.30). Per adult-year:

```
m_k = share_k × mult_k, with z-scores over the year's adults:
  converse      exp(0.30 E) × (married ? 1.10 : 0.95)
  learn         exp(0.30 O) × (age < 25 ? 2.0 : age < 35 ? 1.2 : 1.0)
  task          (employed ? 1.20 : 0.70) × (kids > 0 ? 1.10 : 1.0)
  deep thought  exp(0.60 reasonZ)
  rumination    exp(0.40 stressZ − 0.30 reasonZ + 0.20 N)
  daydream      exp(−0.20 reasonZ)
then 30 rounds of iterative proportional fitting: each adult's row sums to 1, each mode's adult mean = its share.
```

Coefficients **[design]**, signs from psyche.json's own notes (rumination: cortisol, default OS; deep thought: higher OS;
learn: openness, students; task: work hours, ATUS). Modes do not feed back into money (stated in the view). [proto]
2025 players: task 0.22-0.43 (out of work 0.23, PMC 0.40), rumination 0.04-0.27 (the 1% 0.04-0.07, out of work 0.26),
deep thought 0.02-0.14 (the 1% 0.10-0.13), learn 0.03-0.09 (students 0.08-0.09), converse 0.10-0.23, daydream 0.25-0.35.

### 2.7 The signals (the notebook's relief, satisfaction, pain) as life events

Per adult-year, a member counts once per signal:

| Signal | Event (any of) | 2025 [proto] | 1972 |
| --- | --- | --- | --- |
| **pain** | a large uninsured bill (`Loss > 0`); consumer debt discharged; lost the job (employed last year, not this year, age 22-61); widowed (a spouse's estate received, `EstateLog` kind 0) | 7.0% of adults | 8.1% |
| **relief** | consumer debt paid down to 0 from > 0; back to work (not employed last year, employed now, 22-61) | 4.8% | 7.6% |
| **satisfaction** | saved ≥ 10% of disposable income three years running (the agency rule's saver); bought a home this year; a child born this year (`SmvPerson.Birth` in the year, mother or father) | 14.3% | 37.2% |

(The 2025 and 1972 shares were measured without widowhood and births; with them each rises by ≤ 2 points.) The season
of tit for tat keeps its tie events (5.5 of the bowl spec: a tie breaks, a feud ends, a tie holds) with their looks; **in
the legend they are renamed "tie breaks / mends / holds"** so the notebook's three words mean one thing: what a person
feels in a year.

---

## 3. The player's program

### 3.1 Aggregation (`Land/ProgramModel.cs`, new; pure, any thread)

`ProgramModel.Build(model, players, season, year)` returns one `ProgramYear` per player, one for the nation, and, on
demand, one for a person's household. Rules, over the player's adult members (children add only counts and child cost):

| Quantity | Rule |
| --- | --- |
| dollars (income, taxes, spending, categories, fear and fantasy dollars, saving, balance sheet, flows, allowance) | sums of the members' records (records are per person, a couple's money split evenly), × 100,000 / 1e9 → $B |
| drives, stress, reason, future, agency, chemicals, modes, β, λ, comparison | adult means (unweighted: one person, one mind), with p10 and p90 |
| fear share, fantasy share | spending-weighted means |
| saving-rate parts | disposable-weighted means of the households' trace rows |
| tilt parts (6 × 5) | spending-weighted means |
| equity share (risk) | weighted by financial assets |
| signals, higher-OS share, in control, married, with children, inherited | shares of adults |
| PLAY | the season's per-player values: opening Σ_q E0(p,q) c(p,q); forgiveness g_p; tribal memory μ_p; cooperation at round 96; standing; coalition at the last detection |

A household split across two players (spouses in different groups) contributes each adult's half to each player.

### 3.2 The contract (`Land/LandTypes.cs`, added)

```csharp
public enum MindScope : byte { Player = 0, Person = 1, Nation = 2 }

/// One player's (a person's household's, or the nation's) program in one year: what each stage took in and gave out.
public sealed class ProgramYear
{
    public int Year; public MindScope Scope; public int Player = -1, Person = -1; public string Key;
    public int Adults, Children, Households, Dependents;                                  // lines
    // WORLD ($B)
    public double Wages, Business, Capital, Transfers, SocialSecurity, AllowanceIn, Estates, Borrowed, Taxes, Disposable;
    public readonly double[] WagesBy = new double[25];
    public float EmploymentRate, MortgageRate, CardApr, EquityReturn, HomePriceGrowth, Trust;       // the era (world dials)
    // MEMORY (start of year, $B; per-adult values are these / Adults × 1e4)
    public double Financial0, Home0, Business0, Mortgage0, Consumer0;
    public float Runway0, DebtService0, Buffer, Inherited, ParentRank, SavingRate1, SavingRate2;
    // FEEL
    public readonly float[] Desire = new float[5], Fear = new float[5];                  // psyche.json order
    public float FearShare, Stress, StressDebt, StressJobless, StressBuffer;
    public readonly float[] Chemicals = new float[7];   // dopamine, serotonin, oxytocin, endorphins, cortisol, adrenaline, testosterone
    public readonly float[] Modes = new float[6];       // converse, learn, task, deep thought, rumination, daydream
    // OS
    public float Reason, ReasonP10, ReasonP90, HigherOs, Future, Beta, Lambda, Comparison;
    public float Married, WithKids;                                                      // joint goals
    // DECIDE ($B)
    public double Spending, Saving, AllowanceOut, ChildCost;
    public float SavingRate, SaveBase, SaveTraits, SaveAge, SaveKids, SaveShift;
    public readonly double[] Category = new double[6], CategoryFear = new double[6], CategoryFantasy = new double[6];
    public readonly float[] Tilt = new float[30];       // [category × 5]: trait, drive, children, age, interest
    // ACT ($B)
    public double Financial1, Home1, Business1, Mortgage1, Consumer1, Repaid, Gains, Discharged, Closures;
    public float EquityShare, HomeBuys, HomeSales, Defaults;
    public double WealthChange, FamilyResidual;          // the score (4.4)
    // PLAY
    public float Opening, Forgiveness, TribalMemory, Cooperation; public byte Standing; public int Coalition = -1;
    public readonly float[] Strategy = new float[7];
    // OUTCOME
    public float Agency, InControl, Fantasy, Pain, Relief, Satisfaction;
    // mind space (6.8) and the listing
    public Vector3 MindPos; public string Listing;        // MindPos: (fear, reason, future) mapped
}
// LandSnapshot (added): public ProgramYear[] Programs; public ProgramYear Nation; public string ProgramLog;
// Player (added, $B): public double AllowanceIn, AllowanceOut, Borrowed, Repaid, Estates, ChildCost; public int ProgramIndex;
```

### 3.3 The listing

`ProgramModel.Listing(ProgramYear)` writes the 9 lines of section 0 from the numbers (number formats from `LandFacts`;
drives listed in descending weight, the top 3 tilt parts of each category with |part| ≥ 0.05 named: "necessities 42%
(children +0.10)"; comparisons with the nation use `LandFacts`' ±10% "about the same" band). Five 2025 listings
[proto C#] for the view's examples:

| Player | Stress → cortisol | Reason (higher OS) | Rumination / task | Save | Agency / in control | Allowance |
| --- | --- | --- | --- | --- | --- | --- |
| `frontline|trade|I` (the default, 6.8M adults) | 0.64 → 1.08 | 0.36 (24%) | 13% / 36% | 2.1% | 0.39 / 1% | in $7B, out $2B |
| `pmc|health|I` (4.2M) | 0.33 → 0.82 | 0.47 (40%) | 8% / 40% | 6.7% | 0.56 / 7% | out $25B |
| `out_of_work|safety_net/genx|I` (4.1M) | 1.28 → 1.47 | 0.26 (10%) | 26% / 23% | −8.6% | 0.35 / 5% | – |
| `top1|capital|D` (0.5M) | 0.01 → 0.62 | 0.58 (60%) | 7% / 30% | 26.9% | 0.86 / 60% | – |
| `students|schools/genz|I` (5.4M) | 0.61 → 1.08 | 0.30 (13%) | 14% / 25% | −3.5% | 0.28 / 0% | in $68B |

### 3.4 Following people across years

Players are rebuilt every year; a selection follows its **people**. When the year changes, the selected player becomes
the player of the new year holding the most of the old selection's adult members alive in both years (ties: more
people, then lower index); none alive → the selection clears. `EconomyState.SelectedKey` keeps the key for display; the
log prints "[Why] Selection 2024 frontline|trade|I → 2025 frontline|trade|I (61 of 66 members)". The same mapping
(`ProgramModel.Related(prevSet, nextSet)`) gives the tween partners of 4.3 and the mind trails of 6.8.

---

## 4. The game: the landscape plays the years

### 4.1 The clock (`Land/LandPlayback.cs`, new; main thread)

| | |
| --- | --- |
| Range | 1950 (`EconomyState.MinYear`) → 2025; 2026 (an H1 estimate) only by `.`, marked "estimate" |
| Controls | `Space` play / pause; `,` `.` step (existing); speed 0.75 / 1.5 / 3 s a year (chip buttons; default 1.5) |
| Step | at the end of a year's dwell: `LandService.Request(Y+1)`; `LandService.Prefetch(Y+2)` builds into the cache without publishing (`CacheYears` 4 → 6) |
| Never skips | if Y+1 is not built when its turn comes, the clock waits (logged: "[Why] Playback waited n ms at 1987") |
| On publish | land layers rebuild for Y+1; `LandTween` runs 0.8 s (ease in-out cubic); the signals of Y+1 fire; the mind (if shown) morphs (6.9) |
| Stop | at 2025, or any preset change, pick or drag |
| Harness | `WHY_ECON_PLAY=1972:2` starts playback at 1972 with blocking builds (deterministic: the step lands at fixed frames) for 2 steps |

### 4.2 What moves (the other parts draw it; this part sets the contract)

| Thing | Keyed by | Tween |
| --- | --- | --- |
| player discs | `Player.Key`; a key without a partner year uses `Related` (3.4): a new player grows from the position of the player its members came from, a vanished one shrinks into the player its members went to | `Pos`, `Radius` lerp; alpha 0 ↔ 1 for the unpartnered |
| member dots | person index | from `Stand.DotWorld(p, Y)` to `(p, Y+1)` (people part); births appear under the parent (alpha 0 → 1), deaths fade (ghosts: people part) |
| crowns | industry | center, y, radius; jewels cross-fade |
| clouds | key, else `Related` | center, radius |
| terrain, rivers, roots | – | 0.25 s alpha cross-fade (the terrain part may morph heights instead: its choice) |
| ties, territories | pair / coalition | cross-fade 0.6 s (people part's 8.1) |

**The tween contract** (`Land/LandTween.cs`, new): `static LandSnapshot Prev, Next; static float T; static bool Active;
static int[] RelatedPrev, RelatedNext` (player → partner index or −1). A layer implementing `ILandTweenable` builds its
morph targets from both snapshots with **identical topology** (every element of the union of keys emitted on both sides;
an absent side collapses to its related partner at zero size) and lerps vertices per frame, as `SectionLayer`'s morph
does today (16,446 vertices at 0.75-0.88 ms a frame [code log]).

### 4.3 Signals on the land (`Layers/SignalsLayer.cs`, new)

When a year lands, every player with a signal share ≥ 3% flashes at its head height (`Player.Pos + 0.32 up`, the
people part's glyph): **pain** an ice ring expanding 0 → 0.25 u over 0.6 s (alpha 0.7 × share/0.2, capped 0.7);
**relief** a white pulse rising 0.3 u; **satisfaction** a rose-gold ring holding 0.6 s. Players are flashed in index
order with a 15 ms stagger (≤ 1.8 s for 118 players), drawn as one mesh rebuilt per frame (≤ 3K vertices). In
2008-2010 pain fires across the floors; in 1950-1972 satisfaction dominates.

### 4.4 The score

Per player and year, over the members adult in both Y−1 and Y:

```
ΔW = Saving + Gains + Estates (from parents and spouses) + DischargeAmount (debt written off raises net worth)
     − ClosureLoss + FamilyResidual
```

`FamilyResidual` is what pooling and splitting balance sheets moved between people (marriage, divorce, a spouse joining
another player); it is printed, never hidden. The panel shows the score as a stacked bar ("2008 · frontline · trade ·
I: −$41B: saving +$6B, gains −$58B, estates +$9B, written off +$3B, closures −$1B"). The nation's sum equals the lives'
money check of the year (`MoneyNetWorth` difference) within $1B, minus arrivals and estates leaving (checked, 8.3).

### 4.5 Why the season keeps no memory across years

The season (96 rounds) stays one year's game, solved per year from GSS trust: chaining seasons would make a 2025 preset
depend on 75 earlier seasons (≈ 5.6 s blocking at 69 ms each) and the GSS calibration would erase most of the carried
memory anyway. The memory that carries the game from year to year is the program's: balance sheets, buffers, saving
history, estates and allowances. Stated in the docs.

---

## 5. Interaction

| Input | Where | Does |
| --- | --- | --- |
| click a player | land | selects it (existing `SetSelection`); its flows brighten (flows part's highlight ids), others to 0.25 |
| `M` or double-click a player | land | flies (1.2 s) to the `mind` view of the selection (default: 6.9's default player) |
| `M` | mind | flies back to the last land view |
| `Tab` / `Shift+Tab` | mind | next / previous player by index |
| `P` | mind | player ↔ population mode |
| click a particle | population mode | selects that player, switches to player mode |
| `G` | mind | runs the shown year as the 12 s staged sequence (6.9); `G` again jumps to the settled state |
| `Space`, `,` `.` | anywhere | plays / steps the years; the mind follows its selection (3.4) |
| "Mind" button / `M` in the person inspector | person inspector | person mode: the household of that lifeline |
| hover | mind | any part's card: its numbers and the rule that made them (generated; e.g. a conduit: "Status · $52B · 25% fear · 43% fantasy · pulled by drive pull +0.12, comparison −0.06, children −0.03 · biases: social comparison, hedonic adaptation, anchoring, payment decoupling") |

`EconomyState` gains: `MindScope Mind`, `string SelectedKey`, `int MindPerson`, `bool Playing`, `float Speed`,
`float MindPhase` (0..1; 1 = settled). Harness overrides: `WHY_ECON_PLAYER=<key>`, `WHY_ECON_MIND=player|nation|<person
index>`, `WHY_ECON_MIND_PHASE=<0..1>`, `WHY_ECON_PLAY=<year>:<steps>`.

**`UI/MindPanel.cs`** (new; right column in landscape, 360 reference px; bottom sheet in portrait): the listing (3.3),
sparklines 1950-Y of the followed members' income, spending, saving rate, net worth and debt per adult (in 2025
dollars), the score bar (4.4), and the buttons Run year (G), Population (P), Back to land (M). The harness does not draw
uGUI, so the listing is also logged (8.3).

---

## 6. The mind view (3D)

### 6.1 Placement (`Land/MindFrame.cs`, new)

Mind-local frame: x right, y up, z front (= the terrain's `Front`, toward the default camera). Origin: land-local
`(Bounds.xMin − 6.5, 0, Bounds.center.z)` (6.5 u left of the land's left edge, on the floor), same yaw as the land.
Everything is world-space raw geometry (`GraphMaterials.Raw`, `Why/Line`, `Why/Surface`), as the land is. Extent:
x −3.2..3.2, y 0..4.2, z −4.8..4.9.

### 6.2 The shell (the brain)

Ellipsoid, center **C = (0, 2.1, 0)**, semi-axes **a = (2.2, 1.5, 2.8)**; two hemispheres, each pushed 0.08 off the
midline (a 0.16 fissure): **left = FEAR (ice), right = DESIRE (rose)**.

* **Latitude rings**: v ∈ {−0.75, −0.5, −0.25, 0, 0.25, 0.5, 0.75, 0.92}; per hemisphere a half ring
  `(s(0.08 + 2.2ρ cos θ), 2.1 + 1.5v, 2.8ρ sin θ)`, ρ = √(1 − v²), θ ∈ [−90°, 90°], 48 segments.
* **Meridians**: planes x = s(0.08 + u · 2.2), u ∈ {0.15, 0.40, 0.65, 0.85}: the ellipse `(2.1 + 1.5ρ sin φ, 2.8ρ cos φ)`,
  ρ = √(1 − u²), 96 segments, segments with v < −0.8 left out (the brainstem's opening).
* **Style**: 1.0 px lines; hemisphere intensity `0.5 + 1.5 × pole share` (fear share left, 1 − fear share right).
* **The reason waterline** (the OS): the plane `y_r = 3.6 − 3.0 × Reason` (the lit top fraction of the shell's height =
  the share of decisions made by reason). Segments above y_r: color 0.7 white + 0.3 tint, alpha 0.55, intensity × 1.3
  (**the higher OS, the lit cortex**); below: the tint, alpha 0.35, × 0.8 (**the default OS**). The waterline ring itself:
  white, 2.2 px, intensity 2.4, label "reason 0.36 · 24% of adults on the higher OS".
* **OS dials** at the waterline's right end (x 2.6): three 240° arcs r 0.14 with a needle: present bias β (0.5-1.05),
  loss aversion λ (1-3.5), social comparison (−1.5..1.5 sd); hover: the biases of psyche.json tied to each.
* **Joint goals** (the notebook's "Joint", across both hemispheres): three arches over the fissure at y 3.55, z −1.0 /
  0 / +1.0, half circles r 0.35 in the x-y plane, white, intensity `0.6 + 2 × share`: partnership (married share),
  purpose (future orientation), parenting (with-children share).

### 6.3 The drives (the deep floor)

Ten nodes at **y = 1.25**. Pole s = −1 fear, +1 desire; node k = 0..4 in notebook order (food / starvation, collective
/ isolation, law / murder, shelter / exposure, sex / childless: each desire faces its fear across the fissure):
`ψ_k = −60° + 30°k; P = (s(0.45 + 1.15 cos ψ_k), 1.25, 1.15 sin ψ_k)`. Glyph: three orthogonal circles (24 segments) of
radius **ρ = 0.06 + 0.30 w_k** (w = the pole-normalized weight; 0.20 → 0.12 u) plus a horizontal veil disc (alpha 0.15
× activation); **activation = pole share × w_k × 5**, intensity `0.6 + 2.0 × activation`. Label "shelter 0.32".

### 6.4 The chemicals (beneath)

Seven vials hanging under the brain, **x = −1.8 + 0.6c**, z = 0.9, c = 0..6 in the order cortisol, adrenaline,
testosterone, dopamine, endorphins, oxytocin, serotonin (fear's chemicals on fear's side). A vial: a 3 px vertical line
from y 0.10 to **y 0.10 + 0.42 × min(2.2, index)** (index 1.0 = 0.42 u) topped by a circle r 0.05 + 0.04 × index;
color by psyche.json pole (cortisol, adrenaline ice; testosterone half ice half rose (its notebook side is fear, its pole
desire); the others rose); intensity `0.8 + 0.8 × index`. **Links**: from each drive node to the top of each of its
chemicals' vials (22 links), alpha `min(0.6, 0.1 + 2.5 × a_k / |chemicals(k)|)`, 0.8 px, the drive's pole color.
Label under each vial "cortisol 1.08".

### 6.5 The modes (the orbit)

A stepped ring around the shell: ellipse semi-axes **(2.55, 3.15)** in x-z; arcs in the order task, converse, learn,
deep thought, daydream, rumination, clockwise seen from above from the front (+z); each arc's angle ∝ its time share;
height **2.1 + 0.30 × os** (os +1 learn, deep thought; 0 task, converse; −1 daydream, rumination), arcs joined by
vertical steps. 1.6 px, white (rumination ice-tinted 50%), intensity `0.6 + 4 × share`. A **clock hand** (a bright 0.08
u dot) runs once around the orbit per shown year (6.9). Label per arc "task 36%".

### 6.6 Memory and world (behind)

* **MEMORY shelf** at z = −4.4, baseline y 1.4 (a gold hairline x −2.0..2.0): five boxes (w 0.36, d 0.24) at x −1.6,
  −0.8, 0, 0.8, 1.6: financial, home, business **rising** (solid gold), mortgage, consumer debt **hanging** (gold at
  0.7, dashed edges). Height per adult: **h = min(1.6, 0.45 log10(1 + $ per adult / $10K))** ($10K 0.14, $100K 0.47,
  $1M 0.90, $10M 1.35). Start-of-year values; they morph to the end of the year in the ACT stage (6.9). Above the shelf:
  a Big Five pentagon (r 0.35, z-scores mapped −1.5..1.5 sd to 0..r), "inherited from parents 7% · parents' rank 0.55",
  and three saving-history ticks. Five gold hairlines (alpha 0.15) run from the box tops to the shell's back pole
  (0, 2.1, −2.8): what is remembered enters the brain from behind.
* **WORLD intake**: income pipes on the floor (y 0.05) from z −4.3 to the **brainstem B = (0, 0.6, −1.0)** (cubic
  Bézier, 24 points), side by side over **0.9 u in total, widths ∝ dollars**: wages (`LandStyle.Wages` light blue),
  business and capital (gold), transfers (steel), allowance in (people blue), estates (gold, only in a year with one),
  borrowing (gold dashed). Flow pulses run toward B. **Taxes leave before any decision**: a steel pipe from B down to a
  sink disc on the floor at (−0.9, 0, −0.4) labeled "taxes $70B". **World dials** on the floor at x −2.8, z −4..−2: six
  240° arcs (employment rate, mortgage rate, card APR, stock return, home prices, GSS trust) with values.

### 6.7 Strategy (in front): the decisions

Each category's conduit **leaves the shell where the data puts its decision**: spending.json's `mind` coordinates
(x: −desire … +fear, y: reason) map to the exit `x_e = −1.6 × mind.x`, `y_e = 0.9 + 2.4 × mind.y`, on the front
surface `z_e = 2.8 √(1 − (x_e/2.2)² − ((y_e − 2.1)/1.5)²) + 0.05`: jeopardy leaves low on the fear side, escapism low on
the desire side, saving (gold) and growth high (reason 0.80, 0.85). Each conduit is a cubic Bézier `(P0, P0 + 1.2 z,
P3 + (0, 0.6, −1.5), P3)` to the **gate** at z = 4.3, y = 0.6, where conduits lie side by side **ordered by fear share,
fear side first**: jeopardy (0.85), saving (0.60), collective (0.55), necessities (0.50), growth (0.35), status (0.25),
escapism (0.15), then **allowance out** (people blue, from B). Total gate width **3.0 u ∝ spending + positive saving +
allowance out** (a player's composition, not its size: the size is in the labels). A category conduit is **two lanes**,
ice (its fear dollars) and rose (its desire dollars), never blended (the rivers' rule); **fantasy glitter**: sparkles
along the lane, count = `min(30, 400 × fantasy $ / total)`, positions `LandMath.Hash01(category, k)`. Dissaving: the
saving conduit is absent and a dashed gold **borrowing** pipe enters at the intake. Labels at the gate: "status $52B ·
25% fear · 43% fantasy". **Agency**: a gold ring around the prefrontal pole (0, 2.1, 2.92), r 0.30, facing front, the
arc filled to `agency × 360°`, label "agency 0.39 · 1% in control". **PLAY**: three concentric horizontal rings (r 0.18,
0.26, 0.34) at (0, 3.75, 2.3) filled to opening, forgiveness × 3 (g = h/3, so a full ring = everyone on the higher OS),
cooperation at round 96; label "tit for tat · cooperation 0.72 · beta · coalition 1".

### 6.8 Population mode (`P`)

The shell, drives, chemicals and modes show the **nation's** program (dimmed to 0.45 so the particles read; the
conduits and the gate show the nation's spending). Every player is a **particle** at

```
x = −1.9 · clamp((fear − 0.53) / 0.13, −1, 1)          (fear left, desire right; 2025 players span 0.47-0.63)
y = 0.75 + 2.7 · clamp((reason − 0.10) / 0.70, 0, 1)    (the higher OS above)
z = −2.4 + 4.8 · clamp((future − 0.30) / 0.70, 0, 1)    (the present-bound back, the planners front)
```

ranges fixed for all years (player-years 1950-2025 [proto]: fear p1-p99 0.437-0.608, reason 0.211-0.686, future
0.429-0.876). A particle: two rings (horizontal and vertical, 16 segments) of radius `0.03 √(people / 1M)`; color from the
people part's zone (floor people blue, slope blue with a gold rim, crown gold, cloud pale gold-white). **Trails**: where
the player's present members' minds were in each of the last 10 years (their mean (fear, reason, future) then; members
alive, adults), a polyline alpha 0.35 → 0 backward. Group labels at the people-weighted centroids. [proto, group means
1950 → 2025]: the 1% rise to future 0.82 and reason 0.57 with fear 0.52; out of work sink to reason 0.26, fear 0.58;
Social Security retirees move to the fear side (0.53 → 0.61); stress rises in every group but the 1% (0.15 → 0.04).

### 6.9 One year, staged (`G`), and the years (`Space`)

`MindPhase` p runs 0 → 1 over **12 s**; each stage lights in turn, earlier stages stay lit at 0.6:

| p (s) | Stage | What moves |
| --- | --- | --- |
| 0.00-0.125 (0-1.5) | MEMORY | shelf at start-of-year values; hairlines pulse into the back pole |
| 0.125-0.29 (1.5-3.5) | WORLD | intake pipes pulse toward the brainstem; taxes drop to the sink; dials swing |
| 0.29-0.46 (3.5-5.5) | FEEL | drive nodes brighten 0.2 → activation; links pulse; vials fill 0 → index |
| 0.46-0.58 (5.5-7.0) | OS | the waterline rises from the shell's floor to y_r; the cortex above it whitens; the clock hand runs once round the orbit |
| 0.58-0.79 (7.0-9.5) | DECIDE | conduits fill from their exits to the gate (a moving front), glitter appears |
| 0.79-0.92 (9.5-11.0) | ACT | memory boxes morph to end-of-year values; borrowing / repayment pulses; the agency arc fills |
| 0.92-1.00 (11.0-12.0) | PLAY + signals | play rings fill; pain (ice ripples down the lower shell), relief (a white pulse brainstem → crown), satisfaction (the waterline glows rose-gold), each with the year's shares |

**While the years play** (`Space`), the mind does not run stages: on each published year it **morphs** (0.8 s, with the
land's tween) from last year's geometry to this year's (fixed topology: every element always emitted, zero-sized when
absent, so two years' meshes lerp vertex by vertex) and the clock hand turns once per year. Labels swap at t = 0.5.

### 6.10 The combined view (`mind-land`) and person mode

* **mind-land**: camera between the mind and the land; the selected player's **tether**: its inbound pipes leave the
  player's `Pos` on the land (people part) and arc (apex max(y) + 2.5) into the intake; its gate conduits arc from the
  gate to the player's outflow anchor (flows part) and continue as its real rivulets there. Widths as in the mind; the
  land at 0.35 except the selected player's flows (1.0). Landscape only (portrait omits the view; the `people` view
  shows a 0.6 u stub "→ mind" over the selected player instead).
* **person mode**: the same view for one household (the inspected lifeline's); MEMORY shows that person's own traits
  (pentagon), the spouse's dot beside, and the children; the PLAY node shows the person's own strategy.

### 6.11 Meshes, renderers, budget

Eight renderers (one per stage: Memory, World, Feel, Chemicals, Os, Modes, Decide, Play) so the staged sequence only sets
`_Alpha` / intensity per renderer; the moving fronts (waterline, conduit fill, vial fill, clock hand, signal ripples) are
one small mesh rebuilt per frame (≤ 4K vertices). Static geometry per year: shell 1.6K points, drives 0.75K, chemicals
and links 0.25K, modes 0.4K, memory 0.2K, intake 0.2K, conduits 0.45K + ≤ 300 sparkles, rings and arches 0.4K: **≈ 4.6K
points ≈ 19K vertices** (budget 26K). Population particles: 118 × 34 points + trails 118 × 11 ≈ 5.3K points.

---

## 7. Interfaces with the other parts

### 7.1 Needs

| From | What |
| --- | --- |
| **terrain** | `LandFrame`, `ILandTerrain.Bounds` and `Front` (the mind's placement, 6.1); under playback either a 0.25 s cross-fade or a height morph within `LandTween.T` (its choice, 4.2) |
| **people & wealth** | `Player.Pos`, `Radius`, `Zone`, `Crown`/cloud index (tether ends, particle colors, signal positions); `Stand.DotWorld(person, year)` for the dot tween; their census in `Hills` mode (the program aggregates any `PlayerSet`); the `ILandTweenable` implementation for discs, dots, crowns, clouds (4.2) using `LandTween.Related` |
| **flows** | per-player highlight ids for its flows (selection emphasis) and a per-player **outflow anchor** (where its rivulets start) for the tether; the inflow sources (wage hill, crown / cloud) if the tether should start there instead of at `Pos` |
| **couples & births (SMV)** | births with mother and father (exist in `SmvSimulation`): the satisfaction signal; a selected couple or birth sets `EconomyState.SetPerson` → person mode works as is |

### 7.2 Offers

| To | What |
| --- | --- |
| **people & wealth** | WP-M1 writes all the record fields they asked for (`Financial`, `BusinessEquity`, `Dependent`, `EstateLog`) plus `Home`; the **real allowance** (`AllowanceIn`/`AllowanceOut`, `Player.AllowanceIn/Out`): their dependents' home threads carry its dollars (pulse width at `WidthPerB`); their minors' imputation stays, renamed **child cost**, and is `ProgramYear.ChildCost`; the signals per player (`Pain`, `Relief`, `Satisfaction`) and the rename of the season's tie events (2.7); `LandTween` / `Related` |
| **flows** | `ProgramYear` per player = the flows' inputs (categories with fear and fantasy dollars, saving, taxes, borrowing, repayment, allowance, estates); the identities they check stay the lives' totals (unchanged by the allowance, 2.4) |
| **terrain** | nothing it must read; the playback clock and `LandTween.T` |
| **couples & births** | per person-year program state (`MindModel`: chemicals, modes, signals) if lifelines are to be styled by it; person mode for a selected lifeline |

---

## 8. Engineering

### 8.1 Files

| File | Status | What |
| --- | --- | --- |
| `Model/LivesSimulation.cs` | changed | the allowance step (2.4); the trace writes (2.2); the record fields (2.2); `EstateLog` (people part 3.2) |
| `Model/EconomicLives.cs` | changed | `PersonYear` fields; `TraceOf(person, year)`; `CalibrationOf(year)`; `AllowanceTotal(year)`; `EstateLog` |
| `Model/ProgramTrace.cs` | new | the household-year trace store, quantization, row index |
| `Model/MindModel.cs` | new | per adult-year chemicals (2.5), modes (2.6), signals (2.7); per-year lazy, LRU 16 years |
| `Model/LivesReport.cs` | changed | the allowance line and the trace checks in the lives report |
| `Land/ProgramModel.cs` | new | `ProgramYear` aggregation (3.1), listing (3.3), `Related` (3.4), the score (4.4) |
| `Land/LandTypes.cs` | changed | `ProgramYear`, `MindScope`, `LandSnapshot.Programs/Nation/ProgramLog`, `Player` money fields |
| `Land/LandService.cs` | changed | programs in `Build`; `Prefetch`; `CacheYears` 6 |
| `Land/LandPlayback.cs`, `Land/LandTween.cs` | new | the clock (4.1), the tween contract (4.2) |
| `Land/MindFrame.cs`, `Land/MindGeometry.cs` | new | placement; pure geometry builders (points, widths, colors) of 6.2-6.8, unit-testable |
| `Layers/MindLayer.cs` | new | the mind's renderers, morph, staged animation, population particles, tether |
| `Layers/SignalsLayer.cs` | new | signal flashes on the land (4.3) |
| `UI/MindPanel.cs` | new | listing, sparklines, score, buttons |
| `UI/EconomyControls.cs` | changed | `Space`, `G`, `P`, `M`, `Tab` |
| `EconomyState.cs` | changed | 5's fields and harness overrides |
| `EconomyPresets.cs`, `EconomyViews.cs` | changed | `mind` (key 7, replacing the bowl's mind view), `mind-pop`, `mind-land`, `mind1972`; emphasis rows |
| `Assets/Resources/Data/economy/psyche.json` | data | a `program` block: chemical state gains, mode multipliers and fit rounds, allowance constants (φ, weights, cap) with the recalled anchors, signal definitions, mind-space ranges; each with `basis: "design"` and a note |
| `Docs/ECONOMY.md` | docs | "The program and the mind" |

### 8.2 Performance (worker unless said)

| Step | Budget |
| --- | --- |
| lives: allowance (≤ 2 ms over 81 years) + trace writes (≤ 15 ms) + record fields (≤ 10 ms) | lives 607-740 ms → ≤ 790 ms; memory +17 MB (trace 10.7 MB, record fields 6.6 MB) |
| `MindModel` one year (2,742 adults, IPF 30 × 6) | ≤ 3 ms |
| `ProgramModel` one year (118 players, listings) | ≤ 6 ms |
| mind static meshes, one year | ≤ 6 ms (≈ 19K vertices) |
| per frame (main): staged fronts ≤ 4K vertices; year morph lerp of ≈ 19K vertices | ≤ 1 ms; ≤ 0.8 ms |
| population particles + trails (10 earlier years of `MindModel`, members' means) | ≤ 12 ms |
| a playback step (snapshot ≈ 150 ms today + the other parts' budgets + programs 9 ms + layer rebuilds) | well under the 1.5 s dwell; prefetch keeps one year ahead |

### 8.3 Determinism

No random numbers in this part. Quantization is fixed-point; every loop runs in index order; IPF has a fixed round
count; sorts end on the index; glitter by `LandMath.Hash01`; animation time comes from the frame clock (pinned by the
harness through `WHY_ECON_MIND_PHASE` and `WHY_ECON_PLAY`). Checksums: the trace (Σ over rows of the quantized fields ×
row index), the programs (Σ_p index × (Spending + 3 Reason + 7 Chemicals[4])), the mind mesh (Σ vertex x).

---

## 9. Verifying with the headless harness

`$SP/agent-tools.sh <name> <tree>`; `export WHY_NOW=2026-10-01T12:00:00Z`;
`WHY_REPO=<tree> $SP/run-<name>/run.sh --scene economy --render mind,mind-pop,mind-land,mind1972 --out <dir>` (and
`--size 1080x1920` for `mind`, `mind-pop`).

### 9.1 Presets

| Preset | Camera (mind-local target; yaw relative to the land's) | Shows |
| --- | --- | --- |
| `mind` (key 7) | target (0, 1.6, 0.2), yaw −40°, pitch 16°, distance 15; portrait pitch 24°, distance 21, width 9.5 | player mode, the default player: the largest employee player by adult lines (ties: lower index): 2025 `frontline|trade|I` (68 lines), 1972 `frontline|manufacturing|D` (54) [proto C#]; settled (phase 1) |
| `mind-pop` | target (0, 1.9, 0), yaw −40°, pitch 16°, distance 13.5 | population mode, trails |
| `mind-land` | target midway between the mind's and the land's centers at y 1.5, yaw 0, pitch 24°, distance 34 | the tether; landscape only |
| `mind1972` | as `mind`, year 1972 | 1972's default player |

Extra runs: `WHY_ECON_MIND_PHASE=0.40` (FEEL lit, OS not yet), `WHY_ECON_PLAYER=top1|capital|D`,
`WHY_ECON_MIND=nation`, `WHY_ECON_PLAY=1972:2` with `--render people`.

### 9.2 Log lines and expected values

```
[Why] economic lives: … (allowance φ 0.5)                                   the 27-line report (WP-M1 alone: 0 lines
                                                                            differ from HEAD; with WP-M2: the diffs of 2.4)
[Why] Lives allowance: 1972 $20B (104 of 153 dependents) … 2025 $379B (175 of 295; 1.7% of DPI; 263 paying
      households; PMC pays 82%); 2018 ≈ $260B vs ~$500B for 18-34 (Merrill Lynch 2018, recalled); checks 2/2 PASS
      (Σ in = Σ out every year; national disposable income unchanged to $0.01B)
[Why] Program trace: 1946-2026 ≈110,000 household-years, 10.7 MB; reconstruction: shares from base × exp(Σ parts) ×
      factors vs records max |Δ| ≤ 0.001; saving rates from parts + shift vs records max |Δ| ≤ 0.0005 (unbound rows);
      Wealth = Financial + Home + Business − Debt max |Δ| ≤ $1; checks 3/3 PASS; checksum …
[Why] Mind 2025: chemicals (adult means) 1.000 ×7; 1972 dopamine 1.12 serotonin 1.13 endorphins 1.13 oxytocin 0.99
      testosterone 1.02 cortisol 0.88 adrenaline 0.90 (±0.02); modes = psyche shares ±0.001 every year; signals pain
      7.0% relief 4.8% satisfaction 14.3% (±2 pt with widowhood and births); checks 2/2 PASS
[Why] Programs 2025: 118 players (116-120); Σ players = lives: wages $15.73T spending $21.63T saving $1.23T taxes
      $5.25T (0.000%); allowance $379B in = out; default frontline|trade|I; ranges cortisol 0.60-1.52 rumination
      0.04-0.27 reason 0.19-0.77; checks 4/4 PASS; checksum …
[Why] Program listing 2025 frontline|trade|I: <the 9 lines of section 0, tab-separated>
[Why] Score 2025: Σ players ΔW + arrivals − estates leaving = lives' net-worth change within $1B; family residual
      |Σ| ≤ $5B; checks 1/1 PASS
[Why] MindLayer 2025 player frontline|trade|I: ≈19K vertices (shell 6.4K …), build n ms; reason line y 2.52; gate $397B
[Why] Playback 1972 → 1974: 2 steps, 2 tweens, max frame n ms, waited 0 ms (blocking builds)
```

`$SP/redesign/checks/landcheck.py` gets parsers for these lines and the expectations above; the existing 2025 land lines
move by the allowance's side effects (players 116 → 118; Money and Society bodies re-measured once and frozen).

### 9.3 Image checks (python on the PNGs and `labels.json`)

1. **Poles**: in `mind.base.png`, inside the shell's projected box (labels `mind:shell:left` / `mind:shell:right`
   anchors), ice pixels (hue 185-205°, s ≥ 0.30) left of the fissure vs rose pixels (320-350°) right: their ratio within
   ±30% of `(0.5 + 1.5 f) / (0.5 + 1.5 (1 − f))` (f = 0.547 → 1.11).
2. **Reason**: the screen y of the `mind:reason` label anchor lies between the `mind:top` and `mind:bottom` anchors at
   the fraction `Reason` ± 0.05 from the top (0.36 for the default player; 0.58 for `top1|capital|D`).
3. **Decisions**: the gate labels list 8 conduits for the default player (7 categories incl. saving + allowance out),
   their dollars summing to spending + saving + allowance out ± $1B; the jeopardy label left of the escapism label.
4. **Chemicals**: the 7 vial labels in the cortisol → serotonin order left to right; the cortisol label's vial is
   taller in `out_of_work|safety_net/genx|I` (1.47) than in `top1|capital|D` (0.62) by the label anchors' pixel heights.
5. **Population**: `mind-pop` has 118 particle anchors; the `top1` group label sits above (smaller screen y than) the
   `out_of_work` label; ≥ 90% of particles inside the frame.
6. **Phase**: with `WHY_ECON_MIND_PHASE=0.40`, the Decide renderer's lit pixels are < 5% of the settled render's, the
   drive nodes' ≥ 80%.
7. **Determinism**: two runs give byte-identical `.base.png` and identical log lines.
8. **Portrait**: `mind` at 1080x1920 keeps the shell, the gate labels and the memory shelf inside the frame.

---

## 10. Build plan

| WP | After | Content | Done when |
| --- | --- | --- | --- |
| **M1** record and trace | – | `PersonYear` fields (incl. the people part's), `ProgramTrace`, `EstateLog`, accessors | lives report 0 lines differ from HEAD; trace checks PASS |
| **M2** allowance | M1 | 2.4 + psyche.json `program.allowance` + report line | the measured diffs of 2.4 (±1 in the last digit); landcheck re-frozen |
| **M3** mind model and programs | M1 (M2 for the numbers) | `MindModel`, `ProgramModel`, contract, listing, `Related`, score | the Mind, Programs, Score lines PASS |
| **M4** the mind view | M3 | `MindFrame`, `MindGeometry`, `MindLayer`, presets, panel, keys | 9.3 checks 1-8 |
| **M5** playback | M3, the people part's placement | `LandPlayback`, `LandTween`, `SignalsLayer`, prefetch, the tween in the people part's layers | the Playback line; a 1950-2025 filmstrip (every 5th year) with the players' positions changing |

M1 and M2 touch `LivesSimulation` only and go first (every other part's census and money depend on the record).

---

## 11. Limits (said in the view and the docs)

* The program is the lives' program, **calibrated every year** to the national accounts (earnings, transfers, taxes,
  saving rate, PCE mix, reason's mean): it explains **who** spends, saves and fears what, not **how much** the nation
  does. A "what if" lever (more reason, no inheritance) would be absorbed by the calibration; none is offered.
* Chemicals and modes are derived indices with design coefficients; they do not feed back into money.
* The allowance's φ and cap are judgments; its anchors are recalled; adult children 25-34 who receive help are not
  modeled (only dependents under 25 living at home).
* Party is drawn independently of class in the lives (bowl spec 3.6); PLAY's tribal memory inherits that.
* One line is 100,000 people: a player's signals are lumpy (a 5-line player's pain share moves in steps of 20%).

---

## Appendix. Prototypes (`$SP/redesign2/mind/`)

| File | What |
| --- | --- |
| `repo/` | private copy of HEAD 16262b8 with `Scratch/MindDumpLayer.cs` (person-years with player keys, drives, traits), `PersonYear.Head/Dependent/AllowIn/AllowOut` and the allowance step (`MIND_ALLOW=φ`) |
| `allowance_proto.diff` | the allowance step and record fields as implemented in the copy (vs HEAD's `LivesSimulation.cs`) |
| `run_base.log`, `run_a0.log`, `run_a0.5.log`; `lb.txt`, `l0.txt`, `l5.txt` | harness logs and the extracted lives reports: HEAD, instrumented with φ 0 (identical), φ 0.5 |
| `py.csv`, `py_a0.5.csv` | the dumps (1950-2026, adults and children) |
| `proto.py` → `proto_out.txt`, `proto_a05_out.txt` | chemicals, modes, stress, signals, player aggregates, mind-space ranges, group trajectories |
| `listing.py` | the program listings of 3.3 |
| `mock.py` → `mock-player-frontline-trade.png`, `mock-player-top1.png`, `mock-population.png` | the 3D mind at the spec's geometry with 2025 numbers |

# Part: where people and wealth sit

The hill landscape's inhabitants: where every player stands, so that **control and wealth read from position**, how
children, dependents and inheritance show, and how the season of tit for tat is drawn on the terrain. One design;
numbers, not adjectives.

* `$SP` = `/tmp/claude-0/-home-user-why-2/dc9bf553-91de-54d2-9f1f-9831e1f24cfa/scratchpad`. Paths are under
  `Assets/Scripts/Economy/` unless they start with `Assets/` or `Docs/`. The repository was not edited.
* **[proto]** numbers come from `$SP/redesign2/people/proto_v3.py`, `crowns.py` and `proto3.py` run on the lives'
  person-year dump `$SP/redesign/groups/out/personyears.csv` (seed 2026, an earlier lives run: its census gives 119
  players where HEAD gives 116, so the build re-measures every count; outputs `proto_v3_out.txt`, `crowns_out.txt`).
  **[data]** = `Assets/Resources/Data/economy/*.json`. **[recalled]** = values I recalled that must be checked against
  the named source before they enter a data file. **[code]** = checked in the repository.

---

## 0. The grammar on one page

**The kind of standing is control; the place within it is wealth.** Five zones, from the ground up:

| Zone | Who | Where | Size of the mark | Wealth inside the zone reads as |
| --- | --- | --- | --- | --- |
| **Floor** | everyone who earns from a job (employees of the 7 employee groups) and those who live on transfers or are between jobs | on the valley floor, in **bands around the foot of the hill** that pays them (the government's hills for Social Security and the safety net, education's for students, the former industry's for comfortable retirees) | disc ∝ √people (as now) | **distance from the foot**: band 0 at the foot (PMC) … band 3 farthest out (working poor, out of work) |
| **Slope** | owners of private businesses (business owners) and the self-employed (gig & freelance) | on **ledges cut into the slope** of their own industry's hill | disc ∝ √people, on a gold-rimmed ledge | **elevation on the slope**: gig at 10% of the hill's height, owners at 32 / 50 / 68% by wealth |
| **Crown** | **controllers**: the 1% lines whose wealth is a controlling stake (they run a business, or founded and ran one, or inherited a controller's estate), and, measured, the named companies' controlling holders | a **gold crown floating over the summit** of the hill they control | crown radius ∝ √(market value controlled) | crown size; jewels = named companies controlled |
| **Cloud** | the **diversified 1%**: every other 1% line (owns stakes in many industries, controls none) | **pools at cloud level between the hills**, each at the dollar-weighted centroid of the hills it owns | cloud radius ∝ √(wealth) | cloud size; drift toward the hills it owns most |
| (beneath) | **children** and **dependents** | minors **directly beneath their parent's dot**, linked by a stem; adult children living at home linked to their parents' player by a home thread | 1.6 px dots | – |

No mark says "in control". The gold-dot-means-in-control encoding of the bowl is dropped (dots are people blue;
agency stays in the inspectors). Gold means capital, as in the scene's palette: crowns, the owners' ledges, clouds' rims.

**What a viewer sees in 2025 [proto]:** about 99 players on the valley floors (2,019 of 2,742 adult lines, 74%), 14 on
the slopes (190 lines, 7%), 2 controller players in the crowns (6 lines) under **18 crowns** (internet's the largest:
$6.07T controlled, Alphabet and Meta), 5 clouds (21 lines, $35T) drifting between the hills, 727 children beneath their
parents, 294 adult children living at home linked to their parents, and 158 estates (2021-2025, $5.6T) falling from
the departed to their heirs. 86% of the cloud lines and 67% of the crown lines inherited from a parent, against 26% on
the floor and 18% on the slopes.

---

## 1. Measured and modeled

| Measured (source) | Modeled (rule, this part) |
| --- | --- |
| Group shares, the census of 12 groups (unchanged, `groups.json`) | Which zone each group stands in (2.1) |
| Top 1%'s share of private business equity 0.529 and of corporate equity 0.501, its net worth $55.0T (DFA, `circuit.json groups`) [data] | The **control quota** of the 1%: 20.2% of its wealth (3.1) |
| Private business equity $16.0T (DFA 2026Q1; cited in `LivesSimulation.BusinessMultiple`) [code] | Which 1% lines are controllers: the **control ledger** (3.1) |
| Named companies' market value (`circuit.json capture`) [data] | – |
| Named companies' insider / founder stakes, economic and voting (DEF 14A beneficial-ownership tables) [recalled, data addition 11.3] | Control class by the 20% voting threshold (La Porta, Lopez-de-Silanes & Shleifer 1999) (5.1) |
| Proprietors' income by industry (BEA NIPA 6.12D) [data addition] | The crowns' private part split over hills by it (5.2) |
| The 1%'s capital mix: dividends / rent / interest (`CapitalSources.Mix`) [code] | Each cloud's holdings: that mix + 15% own-industry bias (6.1) |
| Parents, spouses, births, deaths (`SmvSimulation`) | Children beneath the mother's dot (7.1) |
| Estates to spouse / children / kin (`LivesSimulation.Bequeath`) [code] | Ghosts: where the departed stood (7.3) |
| Dependents: singles under 25 without children live with family (`LivesSimulation.OwnHouseholds`, a model rule) [code] | Home threads (7.2) |
| – | Children's allowance = OECD-modified equivalence share (0.3 per child) of the household's spending (7.2) |
| – | Positions: bands, rows, packing, cloud centroids, territories (4, 6, 8) |

The model's own gap, said in the log and the docs: **in the lives the top 1% receive 13% of proprietors' income ($282B
of $2.11T), the DFA says 53%** [proto]: the lives put self-employment mostly in the 50-99% band. The control ledger
does not change the lives (no recalibration); it decides which of the 1%'s existing wealth is a controlling stake by
the DFA quota and each line's own business history. Fixing self-employment at the top is a lives task (12).

---

## 2. Who stands where

### 2.1 The standing rule (per player, from its group and the ledger)

| Group (rung) | Zone | Hill (the hill it stands at) | Band / row |
| --- | --- | --- | --- |
| The 1%, controller lines (3.1) | Crown | the industry of the business they run; else the industry of their career self-employment (most years); else their last industry | – |
| The 1%, the other lines | Cloud | none (between hills); cell by **home tier** (tier of their current or last industry) | – |
| Business owners (4) | Slope | their industry (spouse's for a non-employed spouse, rule 5) | row by wealth: 1, 2 or 3 |
| Gig & freelance (2) | Slope | their industry | row 0 |
| PMC (4) | Floor | their industry | band 0 |
| Public servants (3), office (3) | Floor | their industry (federal or state & local for public servants) | band 1 |
| Comfortable retirees (3) | Floor | **their former industry** (last industry worked) | band 1 |
| Frontline & trades (2) | Floor | their industry | band 2 |
| Social Security retirees (2) | Floor | **federal** (the government's ground: Social Security) | band 2 |
| Working poor (1) | Floor | their industry | band 3 |
| Students & young adults (1) | Floor | **education** | band 3 |
| Out of work (0) | Floor | **state & local** (the safety net) | band 3 |

Non-employed spouses keep the dominance rule (they stand with their spouse's group and industry). Tier, rest and mixed
cells take the hill of their members' plurality industry by wages + business income (ties: lower industry index).
"Former industry" = the `Industry` of the person's last record with `Employed`, scanning back at most 45 years; no
record → education (never worked: 2 of 301 comfortable-retiree lines in 2025 [proto]).

### 2.2 Census changes (`PlayerCensus.Build(..., CensusMode.Hills)`)

The census rule is unchanged except the anchors of three groups. `CensusMode.Bowl` keeps today's anchors exactly so the
retired bowl still builds as it did.

| Group | Anchor today | Anchor in `Hills` mode | Key example |
| --- | --- | --- | --- |
| The 1% (controllers) | industry, or `capital` | `crown:<industry>` → tier → rest | `top1|crown:internet|M` |
| The 1% (the rest) | industry, or `capital` | `cloud:<home tier>` (`cloud:none` if never worked) → rest | `top1|cloud:services|D` |
| Comfortable retirees | `pensions/<generation>` | `former:<industry>` → `tier:<tier>` → rest | `retired_savings|former:health|R` |

Merging (place → tier → rest → largest), the size test (≥ 10 lines, or ≥ 3 lines with ≥ 1% of national net worth) and
the party split are the existing code. Expected 2025 **[proto]**: 126 players on the proto's lives (+7 on its 119), so
**about 123 at HEAD (the log states it; 118-128 passes)**: clouds 5 (home gov I, make M, services D, services I, tech
M; 3-7 lines each), controllers 2 (R and M, 3 lines each, cell `rest` because no industry holds 3 controller lines),
slope 14, floor 99 (21 of them comfortable retirees by former industry, up from 6), 1972: 84 players (clouds 3,
controllers 1, slope 9).

**The season keeps its exposure rule** (`SocialSeason` reads `Player.Anchor`): controllers' `Anchor` = their hill's
industry; clouds and comfortable retirees `Anchor = −1` (as `capital` and `pensions` are today), so retirees' dealings
still follow group and generation, not their old workplace. Placement reads the new `Player.Hill` instead.

---

## 3. Control vs diversified ownership

### 3.1 The control ledger (`Land/ControlLedger.cs`, new; built once per load for every year, 1946-2026)

**Quota.** The share of the 1%'s wealth that is a controlling stake:

```
f* = (top1.businessShare × PBE + Insiders) / top1.netWorth
   = (0.529 × $16.0T + $2.76T) / $55.0T = 0.204        (2025; Insiders = the named controlling holders' own stakes, 5.1;
                                                         the prototype ran with 0.202, from an earlier insider sum)
```

held at the 2025 value for every year (no series before the DFA's 1989; the data addition 11.3 adds `top1.businessShare`
history 1989-2026, and the ledger then uses the year's quota, 1989's before 1989). The spec constant
`groups.json zones.controlQuota` = 0.204 until then.

**Eligibility** (each line of the 1% in year Y, `WealthGroup == 3`), as household units (a couple both in the 1% is one
unit; its wealth is the sum):

1. **runs a business**: `SelfEmployed && Business > 0` this year;
2. **heir**: received an estate this year or in an earlier year from a parent who was a controller in the parent's
   last year, and has been a controller every year since (succession, 7.4);
3. **founder**: at least 5 years of self-employment in the person's records up to Y (`careerSe ≥ 5`);
4. **fill** (only while the eligible units' wealth is below the quota, which happens before 1960 when the record is too
   short to show careers): the remaining units by `PersonTraits.Enterprise`, descending.

**Order and fill.** Sort eligible units by (runs a business desc, heir desc, career self-employment years desc,
Enterprise desc, lowest person index); take units in order until their wealth reaches `f* × W1(Y)` (W1 = the 1%'s
wealth). The last unit taken may overshoot (units are atomic). No random numbers.

**What it gives [proto, career-SE proxy for Enterprise]:**

| Year | 1% lines | controllers | of them running a business | control wealth / the 1%'s |
| --- | --- | --- | --- | --- |
| 1972 | 14 | 5 | 1 | 18.9% |
| 2000 | 21 | 5 | 3 | 21.1% |
| 2015 | 25 | 4 | 2 | 26.0% |
| 2025 | 27 | 6 | 2 | 25.8% |

15 of the 27 lines of 2025 had ≥ 5 years of self-employment (55% of the 1%'s wealth): the founder signal is common at
the top, the business still running is rare (2 lines). 2025's controllers: a 34-year-old running a software business
($25.0M), a 70-year-old running a professional-services firm ($15.1M), two couples whose founder spouse ran a business
for 48 and 38 years (now 66-70, retired; $25.3M and $16.3M each).

**Diversified** = every other 1% line. Their stakes are their capital income's mix (6.1). A 1% line who works for a
wage (7 of 2025's 21 cloud lines receive more in wages than in capital income) is still a cloud: it owns stakes in many
industries and controls none; its wages arrive from its hill as a light-blue arc rising to the cloud (flows part).

### 3.2 New per-person record fields (record writer only; the lives' calibration table must stay byte-identical)

`PersonYear` gains three floats, written in `LivesSimulation.RecordHousehold` from state the simulation already has:

| Field | Value | Used for |
| --- | --- | --- |
| `BusinessEquity` | `biz[i]` (going-concern value of the household's business share, $) | crown value of a controller's own business; inspector |
| `Financial` | `fin[i]` ($) | clouds' holdings; inspector |
| `Dependent` (bool) | `dependent[head[i]]` (a single under 25 without children, living with family) | home threads (7.2) |

And one log, appended in `Bequeath` (sequential in the year loop): `EstateLog: List<EstateEvent>` with
`struct EstateEvent { short Year; int From, To; float Amount; byte Kind; }` (Kind 0 spouse, 1 child, 2 grandchild, 3
parent, 4 sibling), one entry per heir. 2025 [proto]: 202 receipts 2021-2025 ($9.25T nominal), 158 of them from a parent
($5.61T). Memory: 2,789 parent receipts 1950-2026 + spouses ≈ 6,000 events × 16 B.

`careerSe` (years self-employed up to Y) and `FormerIndustry` are derived in the census from the records (no lives
change).

---

## 4. Positions on the terrain (`Land/StandPlacement.cs`, new; `PlayerPlacement.cs` stays for the bowl)

### 4.1 What this part needs from the terrain (the interface, `ILandTerrain`, built by the terrain part per year)

```csharp
public interface ILandTerrain
{
    int HillCount { get; }                                  // 25: every industry has a hill record, the government's
                                                            // two included (a plateau, H = its height, may be ~0)
    Vector2 Center(int k);                                  // summit, land-local x z
    float Height(int k);                                    // summit height above the floor baseline (world u)
    float SummitY(int k);                                   // absolute y of the summit
    Vector3[] Contour(int k, float e);                      // closed CCW polyline (≥ 64 points) at elevation fraction e
                                                            // of the hill (e = 0: the foot, y on the floor); monotone:
                                                            // Contour(k, e2) lies inside Contour(k, e1) for e2 > e1
    float GroundY(float x, float z);                        // the surface (floor, slopes) at x z
    float CloudY { get; }                                   // cloud level (suggested: 1.25 × the tallest summit + 0.30)
    Rect Bounds { get; }                                    // the land's x z extent
    Vector2 Front { get; }                                  // unit x z vector toward the default camera (the road side)
    bool Free(float x, float z, float clearance);           // not inside any hill's foot contour + clearance, not in a
                                                            // river bed or pool (flows part supplies the beds), not
                                                            // under a tower footprint, inside Bounds
}
```

If the terrain makes the government a plain rather than hills, it still gives federal and state & local a `Center`
and a foot `Contour` (their ground); everything below works unchanged.

### 4.2 Floor bands

Each hill k has four **band curves**: the foot contour `Contour(k, 0)` offset outward (vertex normals, then Chaikin once)
by `d_b`:

| Band | d_b (from the foot) | Who |
| --- | --- | --- |
| 0 | 0.08 | PMC |
| 1 | 0.26 | public servants, office, comfortable retirees |
| 2 | 0.44 | frontline & trades, Social Security retirees |
| 3 | 0.62 | working poor, students, out of work |

A disc's center lies on its band curve. Bands are 0.18 apart, so two neighbouring bands' discs clear each other when
both radii are ≤ 0.075 (0.075 + 0.075 + 0.03 gap = 0.18); **a larger disc (r > 0.075: about 12M people and up, 9
players in 2025, mostly frontline and retirees) sits on its band curve offset outward by r − 0.075**, then packs with
its neighbours along the band as usual.

**Packing along a band** (exact, reuses `LandMath.PackRow`): parametrize the band curve by arclength s ∈ [0, L), map
s → angle 360 s / L on a circle of radius L / 2π, and call `PackRow(desired, extent, L/2π, PackGap 0.03, rank)`.

* **Desired position**: s* = the curve point maximizing `dot(p − Center(k), Front)` (the hill's front, facing the
  camera), the same for all players of the band; `rank` = (group order, key, index), so a band reads as contiguous
  groups (PMC D, PMC R, …).
* **Blocked arcs**: sample the curve every 0.02 u; a sample is blocked when `!Free(x, z, 0.02)`. Pack inside the free
  interval containing (or nearest to) s*: run `PackRow` unbounded, then clamp the packed run into the interval (isotonic
  regression with uniform box bounds = the clamped unconstrained solution). If the run is longer than the interval, the
  players at the run's far end (by rank) **spill to the next band outward** (d + 0.18, up to 3 spill bands; the log
  counts spills; 0 expected on a terrain with ≥ 0.9 u of free floor per hill front).
* Position: `(x, GroundY(x, z), z)`.

### 4.3 Slope rows (owners and the self-employed)

Elevation fraction by the player's mean adult wealth w (the year's own scale, so 1972 reads like 2025):

```
t = clamp( ln(w / w_lo) / ln(w_hi / w_lo), 0, 1 )
w_lo = 2 × the median adult wealth of the year         (2025: $264K [proto])
w_hi = the wealth of the poorest 1% line of the year   (2025: $8.1M [proto]; DFA's household threshold is ~$13M)
row  = gig & freelance → 0;  owners → 1 (t < 1/3), 2 (t < 2/3), 3
e    = { 0.10, 0.32, 0.50, 0.68 }[row]
```

2025 [proto]: owners' players hold $0.77-1.71M per adult → t 0.32-0.56 → rows 1-2. Each row is the contour
`Contour(k, e)`, packed exactly like a band (s* at the hill's front, blocked arcs where rivers climb or towers stand,
spill to the next row up, then down). **A ledge**: each slope player stands on a flat gold-rimmed veil (alpha 0.12, rim
1.2 px gold, intensity 1.4) of radius r + 0.04 at `y = GroundY(center)`: "they stand on their own business". No slope
player stands above e = 0.68: the summit belongs to the hill's towers and its crown.

### 4.4 Crowns

One crown per hill with controlled value V_k ≥ $100B (2025$, scaled by the year's GDP over 2025's) (5.2):

```
r_k     = 0.75 × sqrt(V_k / GDP_Y)                          (the land's stock scale: r 0.1 = $0.55T in 2025)
y_k     = SummitY(k) + 0.20 + 0.8 × r_k                      (bigger crowns float higher)
center  = Center(k)
```

Crowns whose rings intersect in x z (distance < r_a + r_b + 0.05) are separated in height: the smaller rises in
steps of 0.15 until its band clears the larger's (it never passes `CloudY − 0.20`; if it would, it moves outward along
the line between centers instead). 2025: 18 crowns (5.2 table).

**Controller players** ride their crown: the crown of their hill; a `tier`/`rest` cell takes the crown of its members'
hill with the largest `BusinessEquity + Wealth` (2025: both players → the crown of the hill of their largest member).
Their dots sit evenly around the crown's band at mid-height (gold, 2.6 px, highlight id = the lifeline's).

### 4.5 Clouds

A cloud is a player of the diversified 1% (6). Center = its holdings centroid, at `y = CloudY`; radius
`R = 0.75 sqrt(W / GDP_Y)` (W = the members' net worth, $; 2025 [proto]: $15.2T → 0.53 u, $3.0T → 0.24 u). Then a
deterministic 2D separation (6.2) keeps clouds 0.05 u apart and clear of every crown's ring by 0.10 u in x z.

### 4.6 Constants (`LandStyle`, new block `Stand`)

| Constant | Value |
| --- | --- |
| `FloorBands` | 0.08, 0.26, 0.44, 0.62 (u from the foot); spill step 0.18; max 3 spills |
| `SlopeRows` | 0.10, 0.32, 0.50, 0.68 (elevation fractions) |
| `LedgePad`, `PackGap` | 0.04, 0.03 (u) |
| `StockK` (crowns and clouds) | 0.75 (r = StockK √($/GDP)) |
| `CrownMin` | $100B (2025$) |
| `CrownLift`, `CrownStep` | 0.20 + 0.8 r, 0.15 |
| `CloudGap`, `CloudCrownGap` | 0.05, 0.10 |
| `HomeBias` | 0.15 |
| `ControlQuota` | 0.204 |
| `FounderYears` | 5 |
| `ChildWeight`, `SpouseWeight` | 0.3, 0.5 (OECD-modified scale) |
| `EstateWindow` | 5 years |
| `TerritorySigma`, `TerritoryGrid`, `TerritoryTau` | 0.40, 0.04, 0.35 |

All lengths are for a land about 16 u across; if the terrain part picks another scale, multiply lengths by its
`Scale` and keep the fractions.

---

## 5. The crowns: what is controlled (`Land/CrownModel.cs`, new)

### 5.1 The named companies (from 2024, when the land has the 25 companies)

Data addition to `circuit.json capture[]`: `insiderEconomic`, `insiderVoting`, `holder`, `holderSource`. Control class:

* **controlled**: the insider / founder / family bloc holds ≥ 20% of the votes (La Porta et al. 1999): the crown counts
  the company's **whole market value** (control steers all of it);
* **founder-led**: the bloc holds 1-20%: the crown counts **the stake's value**;
* **widely held** (< 1%): no jewel; the company's owners are the clouds, the pensions and abroad.

Recalled values (to verify against each company's latest DEF 14A / 10-K beneficial-ownership table before entering
the data; ±2 points):

| Company | Bloc (economic / voting) | Class | Counts toward its hill's crown |
| --- | --- | --- | --- |
| Alphabet | Page, Brin 6% / 51% (class B) | controlled | $4,130B (internet) |
| SpaceX | Musk 42% / 79% | controlled | $1,954B (manufacturing) |
| Meta | Zuckerberg 13% / 61% (class B) | controlled | $1,700B (internet) |
| Berkshire Hathaway | Buffett 14% / 30% (class A) | controlled | $1,091B (insurance) |
| Walmart | Walton family 45% / 45% | controlled | $878B (trade) |
| Palantir | Karp, Cohen, Thiel 7% / 50% (class F) | controlled | $456B (software) |
| Oracle | Ellison 41% / 41% | controlled | $427B (software) |
| Amazon | Bezos 8.3% | founder-led | $220B (trade) |
| NVIDIA | Huang 3.5% | founder-led | $192B (hardware) |
| Tesla | Musk 13% | founder-led | $189B (manufacturing) |
| Eli Lilly | Lilly Endowment 10% | founder-led (founding family's foundation) | $108B (manufacturing) |
| Netflix | Hastings 1.5% | founder-led | $6B (media & telecom) |
| Apple, Microsoft, Broadcom, AMD, JPMorgan, Visa, Exxon, J&J, Mastercard, BofA, Costco, Caterpillar, Chevron | < 1% | widely held | – |

2025 [proto]: controlled $10.64T, founder stakes $0.72T; the holders' own stakes (economic) $2.76T, the quota's
`Insiders`. These holders are a few dozen people: **0.0003 of one line**, so they are not players. The crown shows them
as **jewels** (measured); the model's controller lines (modeled) ride the band. The hover card says both: "Internet:
$6.07T controlled · Alphabet (Page, Brin, 51% of votes) and Meta (Zuckerberg, 61%) · the top 1%'s private firms here
$0.24T · in this model: no controller line".

### 5.2 The private part: the top 1%'s own firms (every year)

```
PBE_Y     = DFA private business equity (2026Q1 $16.0T); other years: 7.6 × proprietors' income_Y
            (circuit.json incomeHistory "proprietors"; 7.6 = $16.0T / $2.11T, the DFA ratio of 2025)
Private_k = top1.businessShare × PBE_Y × p_k,Y
p_k,Y     = proprietors' income of industry k (BEA NIPA 6.12D, data addition) / its total;
            until that is added: the lives' business income by industry of year Y (Σ Business over self-employed
            records with Industry k), which the log marks "(model mix)"
V_k       = Private_k + Σ_{controlled j in k} MarketCap_j + Σ_{founder-led j in k} insiderEconomic_j × MarketCap_j
```

Before 2024 there are no company figures: crowns are the private part only (as the towers, which begin in 2024); the
legend says so ("1972: crowns show the top 1%'s own firms; company control from 2024"). 2025 [proto, private split by
the owners' mix as a stand-in; the 6.12D split will move the private parts]:

| Hill | V | named | private | r (u) | jewels |
| --- | --- | --- | --- | --- | --- |
| internet | $6.07T | $5.83T | $0.24T | 0.333 | Alphabet, Meta (controlled) |
| manufacturing | $3.41T | $2.25T | $1.16T | 0.250 | SpaceX (controlled); Tesla, Lilly (founder) |
| trade | $2.65T | $1.10T | $1.56T | 0.220 | Walmart (controlled); Amazon (founder) |
| insurance | $1.56T | $1.09T | $0.47T | 0.168 | Berkshire (controlled) |
| software | $1.18T | $0.88T | $0.29T | 0.147 | Palantir, Oracle (controlled) |
| banking … mining | $0.07-0.80T | 0 | | 0.035-0.121 | – |
| total | $19.8T | $11.4T | $8.46T | | 18 crowns ≥ $100B |

Hardware's crown is small ($0.23T: NVIDIA's founder stake) under the tallest towers: Apple, Broadcom and AMD are widely
held, so hardware's capital rises to the clouds. That contrast is the point of the crowns.

### 5.3 Drawing (`CrownsLayer`, new, order 51, emphasis group `Crowns`)

| Part | Geometry | Style |
| --- | --- | --- |
| band | two horizontal circles (48 segments) at y_k and y_k + 0.25 r_k, a veil strip between | gold `LandStyle.Capital`, lines 1.6 px intensity 1.8, veil alpha 0.18 |
| points | 8 triangles rising 0.45 r_k from the band's top, the first centered on `Front` | gold lines 1.2 px |
| jewels | per named company, on the band's front half, ordered by value: controlled = a filled diamond of half-size 0.25 √(MarketCap/GDP) (min 0.02); founder-led = a hollow diamond of the stake's size | gold, intensity 2.4 (controlled) / 1.4 |
| tethers | one hairline per jewel from the diamond down to its company's tower top (terrain / capture part); without towers, to the summit | gold, 1 px, alpha 0.25 |
| controller dots | the controller players' lines, evenly around the band's mid-height | gold 2.6 px; children beneath (7.1) |
| shadow | the crown's ring projected on the summit (`GroundY`), 1 px alpha 0.12 | gold |

Vertices: 18 crowns × ~400 = 7.2K. Crowns are static (no spin: renders stay deterministic).

---

## 6. The clouds: what the diversified own (`Land/CloudModel.cs`, new)

### 6.1 Holdings

For each cloud member (a 1% line not controlling), its capital is spread over the industries by the 1%'s mix (the
existing `CapitalSources.Mix(data, Y)[3]`: dividends over private industries by owners' dollars, rent to real estate,
interest to banking), tilted toward the industry it knows:

```
m_i,k = (1 − h) × Mix[3]_k + h × [k == home(i)]       h = 0.15 when home(i) ≥ 0 (current or last industry), else 0
Holdings_p,k = Σ_i∈p Financial_i × people per line × m_i,k                ($B; Financial from 3.2)
```

`h = 0.15` is a design parameter, informed by investors' own-industry overweighting (Döskeland & Hvide 2011, J. Finance,
Norwegian investors; employer-stock holdings in 401(k)s, Benartzi 2001) [recalled]; the log prints it. The holdings
also feed the flows part: each hill's payouts reach the clouds in proportion to `Holdings_p,k` (10.2).

### 6.2 Centroid and separation

```
c_p      = Σ_k Holdings_p,k × Center(k) / Σ_k Holdings_p,k                (x z)
repeat 60 times (fixed order: clouds by key, then crowns by industry):
    for each cloud pair (a, b) closer than R_a + R_b + CloudGap: push both apart along a→b by half the deficit each
        (equal positions: a's push goes along +x, b's along −x, by index)
    for each cloud a and crown k closer than R_a + r_k + CloudCrownGap: push a away from the crown by the deficit
    clamp into Bounds shrunk by R_a
```

The market centroid (Mix[3] alone) is the clouds' common origin; home bias moves each 15% of the way toward its home
hill; separation does the rest. The log prints each cloud's shift from its centroid (expected ≤ 0.6 u) and the minimum
gaps (≥ 0.05 and ≥ 0.10 after the loop, or FAIL).

### 6.3 The cloud glyph (`InhabitantsLayer`)

A cloud is the player's disc at cloud level: a horizontal **lens** of radius R (veil, pale gold-white `#E8DDC0`, alpha
0.10 at the center falling to 0.03 at the rim, 32 × 4 triangles), its rim in gold at 1.2 px with the tribe's dash
pattern; its adult dots sunflower-packed over the lens at `y + 0.02 + 0.10 × reason` (2.6 px, people blue); children
beneath (7.1). Its wealth label appears on hover only: "Diversified 1% · home: services · R · 4 lines (0.4M adults) ·
$6.7T · owns: trade 17%, manufacturing 13%, banking 12%, … · controls nothing".

---

## 7. Children, dependents, generational wealth

### 7.1 Minors beneath their parents

Every child line (`!Adult`) is a **1.6 px dot directly beneath its mother's dot** (else its father's) at the player's
ground height (`disc y + 0.005`; for crowns, the band's bottom − 0.10; for clouds, the lens − 0.10), offset 0.012 u per
sibling around the parent's x z (golden angle), with a **stem**: a vertical hairline from the child's dot up to the
parent's dot (people blue, 1 px, alpha 0.30, highlight id = the child's line). Children whose parents are in different
players hang under the mother's (the census rule). A child without a parent alive in the record (35 lines) hangs under
the player's center with no stem.

This uses no footprint (packing unchanged) and reads as hierarchy in profile: the adults float at the height of their
reason (0.03-0.28 above the disc), their children sit on the ground under them. 2025 [proto]: 727 minors under 91
players (max 37 children lines in one player); by parents' zone: floor 588, slope 65, ground (government hills) 38,
cloud 1, crown 0.

**Allowance (minors).** Children have no money of their own in the lives; what the household spends on them is inside
its spending. It is shown as the stem's pulse (`AddFlowPath`, parent → child) and in the inspector as
`Σ household spending × 0.3 / (1 + 0.5 [married] + 0.3 × kids)` per child (the OECD-modified equivalence scale):
2025 [proto] **$1.53T, $22.0K a child** (USDA's 2017 estimate, about $17.5K, is ~$23K in 2025 dollars [recalled]).
If the mind-program part adds a per-player `Allowance` account (10.3), its value replaces this imputation and the log
drops "(imputed)".

### 7.2 Adult children who live at home (dependents)

A dependent (`PersonYear.Dependent`, 3.2) stands in its own player (its group: 99 students, 100 working poor, 41
frontline, … in 2025 [proto]) and is linked to its parents by a **home thread**: an arc from the dependent's dot to
its mother's (else father's) dot, apex `max(y0, y1) + 0.15 + 0.05 d`, people blue, 1 px, alpha 0.18, a pulse toward the
dependent (the family's support: board and lodging). 2025 [proto]: 294 dependents, 259 with a parent alive in a player
(225 distinct player pairs, nearly all of one line: threads are per person, not aggregated).

Drawn **only** in the `families` view and, in every view, for the selected or hovered player (its dependents' threads
and its own members' parents' threads). The students' threads show where students come from: the hover sums them
("Students · schools (Gen Z) · 32 lines · parents: 9 on slopes, 1 in a cloud, 22 on the floor").

The lives say "singles under 25 without children live with family"; 294 of 314 adults under 25 are dependents (94%;
the Census counts about 55-60% of 18-24s living with parents) [proto, recalled]: the inspector says it is the model's
rule.

### 7.3 Estates: generational wealth falling to the next generation

From `EstateLog` (3.2), the events with `Year` in the window [Y − 4, Y] and Kind ≠ spouse (spouses stay in the same
player; their estates show only in the inspector).

* **Ghost**: the departed are not players in Y. A ghost stands where people like them stand: the departed's own group
  rule applied to its last record (`OwnGroup`, the anchor rules of 2.2) gives a key prefix `group|anchor`; the ghost
  sits **0.35 above the Y player with that prefix** (largest by adults if several; else the group's largest player).
  Several ghosts over one player stack 0.06 apart. A ghost is a hollow grey ring (r 0.03, 1 px, alpha 0.4).
* **Estate stream**: an arc from the ghost to the heir's dot (apex `max + 0.20 + 0.06 d`), gold (capital), width =
  the estate's dollars at the flows width scale (`WidthPerB`), alpha `0.6 × 0.7^(Y − Year)` (older estates fade), pulse
  toward the heir. Aggregated per (ghost, heir player) pair when more than one.
* 2025 [proto]: 158 parent estates 2021-2025, $5.61T nominal (≈ $1.1T a year; in 2025 alone 35, $1.86T); 1 reached
  the 1%. Lumpy by design (one line = 100,000 people dying the same death).

**Inheritance on the glyph**: an inner ring on each disc (radius 0.6 r, on the ground) with a gold arc of `share of the
members who have inherited from a parent` (`EconomicLives.InheritedFromParentsBy(person, Y)`). 2025 [proto]: clouds 0.86,
crowns 0.67, ground (retirees) 0.50, floor 0.26, slopes 0.18. Generational wealth reads as height: the inherited sit
higher.

### 7.4 Crowns passed down

The ledger's heir rule (3.1, eligibility 2) makes a controller's heir a controller when the heir is in the 1%. The
lives send a business to a surviving spouse (`fin[sp] += fin[i] + biz[i]`) and to children as financial wealth (the
business is not run by the heirs), so **in this model control usually becomes diversified wealth: the crown's estate
becomes a cloud**. [proto]: no succession 1950-2025 with this ledger (2-3 with the earlier variants that kept incumbents);
the estates from controllers reach heirs outside the 1% (1968-1972: 1 such estate). When a succession happens in the window, its stream runs from the ghost (placed at
its crown) to the heir's dot in the same crown and the crown's hover says "passed to an heir in <year>". The log counts
successions and estates from controllers by where the heir stands (crown / cloud / slope / floor).

---

## 8. The season on the terrain (`SocialLayer`, changed; `SocialSeason`, `Coalitions` unchanged)

### 8.1 Coalitions as territories (replacing the rim bands)

Coalitions become **territories on the ground**: who organizes with whom reads as a map.

```
F_c(x, z) = Σ_{p in coalition c, zone floor or slope} exp(−|x − x_p|² / (2 σ²)),   σ = 0.40, cutoff 3σ
grid: h = 0.04 over Bounds (≈ 400 × 300); cell owner = argmax_c F_c when max F_c ≥ τ = 0.35, else none
boundary: marching squares on each coalition's indicator → polylines → Chaikin × 2 → drape at GroundY + 0.012
```

Every player weighs 1 (presence, not size: a 12M-person player does not swallow its neighbours). Drawn: a boundary
line (1.4 px, the coalition's color from `LandStyle.CoalitionColors`, alpha 0.55) and a fill veil (alpha 0.05, the
grid cells, merged into runs per row). Crown and cloud players carry a ring in their coalition's color at r + 0.03 (they
have no ground). One label per coalition at the area centroid of its largest component (the existing text: "Coalition 3
· 28 players · trade, manufacturing · 77% inside · cooperation 0.73"). Territories are recomputed at each detection
round (0, 24, 48, 72, 96) and cross-fade over 0.6 s during playback.

### 8.2 Ties

The same pair selection as today for the betrayal view (each player's strongest tie and the ties among both players'
three strongest, ≈ 120 of ~990 pairs with ~123 players); the **society view draws less**: each player's strongest tie
(≈ 100 pairs) and the 12 strongest ties across coalitions. Arcs between the players' tie anchors (floor and slope: the
disc center + 0.30; crown: the band's center; cloud: the lens' underside):

| Tie | Apex | Style |
| --- | --- | --- |
| within a coalition | `max(y0, y1) + 0.10 + 0.05 d` (hugs the land) | blue, width and brightness by mutual cooperation (as now) |
| across coalitions | `max(y0, y1) + 0.30 + 0.10 d` (a bridge over the valleys) | violet |
| feud (both < 0.3) | as its kind | grey, broken (as now) |

Standing marks (alpha ring, omega dimmed head ring, anti-alpha tick) and signals (pain, relief, satisfaction pulses)
are unchanged and drawn at the new positions. The betrayal incident's tie is the brightest arc of its view.

### 8.3 Clutter budget

In the society view the land's other groups ease to: terrain 0.35, flows 0.15, crowns 0.4, clouds 0.5, children
stems 0, home threads 0, estates 0. Never more than ~110 tie arcs, 4-6 territories, 6 labels.

---

## 9. The player glyph (what changes from the bowl's 3.4)

| Part | Change |
| --- | --- |
| disc, tribe dash, dots (height = reason), spouse links, head ring, mirage, standing | kept; placed at the new position (floor, ledge, crown band, cloud lens) |
| gold dots "in control" | **removed** (dots people blue everywhere; a crown's dots are gold because they are in a crown) |
| plinth | replaced by the slope **ledge** (4.3) |
| children ring (r + 0.02) | replaced by **children beneath their parents** (7.1) |
| inheritance arc | **new** inner ring (7.3) |
| coalition ring | **new**, crown and cloud players only (8.1) |

If the mind-program part replaces the glyph with its own figure, it draws it at `Player.Pos` with
`Player.Radius`, and keeps the dot slots this part uses for stems and threads (`Stand.DotWorld(person)`).

---

## 10. Interfaces with the other parts

### 10.1 Needs from the terrain part

`ILandTerrain` (4.1): 25 hill records (government included), `Contour(k, e)`, `GroundY`, `CloudY`, `Bounds`, `Front`,
`Free`. Hill summits must leave room for a crown (`CloudY − SummitY(k) ≥ 0.25 + 1.8 r_k`, i.e. ≥ 0.85 u over internet in
2025), and every hill must have ≥ 0.9 u of free floor on its front quarter (else players spill, logged). Tower top
positions per company (for crown tethers), if the towers are kept.

### 10.2 Needs from / offers to the flows part

* **Offers**: every player's stand point `Pos` and its zone; floor players' hills (wage arcs descend from a hill to its
  floor bands); slope players' ledges (business income from their own hill's slope); crowns (center, y, r, jewels:
  payouts of controlled companies rise into their crown); clouds (center, y, R, `Holdings_p,k`: payouts of hill k reach
  cloud p in proportion; investment can fall from the clouds as the source of the rivers that flow up the hills);
  `Free` must exclude the ledges and floor discs if rivers are routed after placement, or placement reads the river beds
  if rivers are routed first (decided by the pipeline: **rivers first**, placement second, both from the same terrain).
* **Needs**: river beds and pools as polylines with half-widths, before placement, to block them.

### 10.3 Needs from / offers to the mind-program part (the per-player program)

* **Needs**: per player, the program's accounts if it adds them: assets, debt, income by source, spending (already in
  `Player`), and `Allowance` (paid to children / dependents, $B; else 7.1's imputation). The record fields of 3.2
  (`BusinessEquity`, `Financial`, `Dependent`, `EstateLog`), whichever part edits `LivesSimulation`.
* **Offers**: the zone and the ledger (controller / diversified / owner / employee / dependent) as inputs of the program
  (a controller's program steers a firm; a cloud's program allocates a portfolio); the family links per player
  (children, dependents, parents' players, estates) for its generational accounts.

### 10.4 Needs from / offers to the couples-and-births part (the SMV graph)

* **Needs**: nothing new (mother, father, `MarriageLog` exist).
* **Offers**: the same highlight ids (a child's dot = its lifeline; a couple's two dots = their lines), so selecting a
  couple or a birth on the population graph lights the parents' dots and the child's stem on the land, and selecting a
  stem lights the birth point at the mid-plane.

### 10.5 The section (the cut's dots flying to the land)

`SectionLayer`'s unfold targets each lifeline dot at `Stand.DotWorld(person)` (adults: the glyph slot at the new
position; children: beneath their parent) instead of `PlayerPlacement.SlotOf`.

---

## 11. Engineering

### 11.1 Files

| File | Status | What |
| --- | --- | --- |
| `Land/ControlLedger.cs` | new | 3.1: controllers per year (all years at load), successions, quota fills; log body |
| `Land/StandPlacement.cs` | new | 4: zones, bands, rows, crown seats, clouds; `DotWorld`; log body |
| `Land/CrownModel.cs` | new | 5: crown values, jewels, radii, heights |
| `Land/CloudModel.cs` | new | 6: holdings, centroids, separation |
| `Land/FamilyModel.cs` | new | 7: child stems, dependents' threads, estates in the window, ghosts, inheritance shares |
| `Land/Territories.cs` | new | 8.1: kernel field, marching squares, Chaikin |
| `Land/PlayerCensus.cs` | changed | `CensusMode` (Bowl / Hills); the three anchors of 2.2; `careerSe`, `FormerIndustry` |
| `Land/LandTypes.cs` | changed | 11.2 |
| `Land/PlayerPlacement.cs` | kept | the bowl's (disabled scene) |
| `Layers/InhabitantsLayer.cs` | changed | glyphs at `Pos`; ledges; cloud lenses; inheritance arcs; dots blue |
| `Layers/CrownsLayer.cs` | new | 5.3 |
| `Layers/FamiliesLayer.cs` | new | stems, home threads, ghosts, estate streams (order 55) |
| `Layers/SocialLayer.cs` | changed | territories; tie anchors and apexes |
| `Model/EconomicLives.cs`, `Model/LivesSimulation.cs` | changed (record only) | 3.2 fields and `EstateLog` |
| `Assets/Resources/Data/economy/circuit.json` | data | `capture[].insiderEconomic/insiderVoting/holder/holderSource`; `privateBusinessEquity` 16.0 |
| `Assets/Resources/Data/economy/industries.json` | data | `proprietors2024` per industry (BEA NIPA 6.12D) |
| `Assets/Resources/Data/economy/groups.json` | data | `zones` block: the constants of 4.6 |

### 11.2 Contract additions (`LandTypes.cs`)

```csharp
public enum Zone : byte { Floor = 0, Slope = 1, Crown = 2, Cloud = 3 }

// Player (added)
public Zone Zone; public int Hill = -1;            // the hill it stands at; -1 for a cloud
public int Band = -1;                              // floor band 0-3 (+ spills) or slope row 0-3
public float Elevation;                            // slope: e; floor: 0
public Vector3 Pos;                                // land-local stand point (disc center; crown band center; lens center)
public int Crown = -1;                             // index in PlayerSet.Crowns for controller players
public double ControlWealth, BusinessEquity, Financial;   // $B
public readonly double[] Holdings = new double[25];       // clouds: $B by industry
public float Inherited;                            // share of adults who have inherited from a parent
public int[] Dependents = System.Array.Empty<int>();       // member adults living with family
public double ChildAllowance;                      // $B (imputed or the program's)

public sealed class CrownGeom
{
    public int Industry; public double Value, Named, Private;          // $B, the year's
    public (int company, byte kind, double value)[] Jewels;           // kind 0 controlled, 1 founder-led
    public Vector3 Center; public float Radius, BandHeight;
    public int[] Players;                                             // controller players seated in it
}

public struct EstateStream { public int Year, From, Heir, GhostPlayer, HeirPlayer; public double Dollars; public bool Succession; }

// PlayerSet (added)
public CrownGeom[] Crowns; public List<EstateStream> Estates; public ControlStats Control; public string StandLog, FamilyLog;
```

### 11.3 The pipeline (in `LandService`'s snapshot build)

1. Terrain (terrain part) → rivers and pools (flows part) → `Free`.
2. Census (`Hills` mode) with the ledger's controllers of Y (the ledger is built once after the lives, before the first
   snapshot).
3. `CrownModel` → `StandPlacement` (floor, slopes, crowns, then clouds with `CloudModel`) → `FamilyModel`.
4. Money (flows part) reads the positions; the season runs on the census (positions are not inputs to it);
   `Territories` after the season (per detection round).

### 11.4 Performance budget (worker thread, per year, 2025 sizes)

| Step | Budget |
| --- | --- |
| Control ledger, all 81 years (once per load: ~5,000 people × 81 years of record reads, ~30 1% lines a year) | 15 ms |
| Census extras (career SE, former industry: one backward scan per adult, ≤ 45 records) | 10 ms |
| Crowns | < 1 ms |
| Placement: band curves (25 hills × 4 bands × ~200 samples, `Free` checks) + packing (≤ 130 players) | 8 ms |
| Clouds: 60 iterations × (25 pairs + 90 crown checks) | < 1 ms |
| Families: child stems 727, threads 259, estates in window ~160 + ghosts | 3 ms |
| Territories: 5 detections × (120 players × ~2,800 grid cells + marching squares on 120K cells) | 25 ms |
| Meshes: crowns 7.2K verts, glyph additions 6K, families ~25K, territories ~20K | 15 ms |
| **Total added to a year change** | **< 80 ms** (the 200 ms budget holds with the terrain and flows parts' shares) |

### 11.5 Determinism

No random numbers anywhere in this part. Every sort has a final index tie-break (person index, player index, industry
index, company index); the ledger takes units in a total order; separation loops run a fixed number of iterations in a
fixed order; sums run in index order; float → string with `LandFacts.Ci`. Each builder adds a checksum to its log line:
ledger Σ (year × controller person index), placement Σ (index × (x + 2y + 3z)), crowns Σ value, families Σ estate
dollars × heir index, territories Σ boundary vertex x. Two runs must print identical lines and identical `.base.png`.

---

## 12. Verifying with the headless harness

Private copies: `$SP/agent-tools.sh <name> /home/user/why-2`, `export WHY_NOW=2026-10-01T12:00:00Z`,
`WHY_REPO=<tree> $SP/run-<name>/run.sh --scene economy --render people,crowns,families,society,betrayal,y1972 --out <dir>`
(and `--size 1080x1920` for portrait).

### 12.1 Presets (this part's; the camera framing follows the terrain part's land frame)

| Preset | Camera | Shows at full strength |
| --- | --- | --- |
| `people` (key 5) | oblique from `Front`, 28° elevation, whole land | all zones, glyphs, crowns, clouds; income arcs (flows part) |
| `crowns` (bar) | from `Front`, 8° elevation, target the land center at half `CloudY` | the vertical stack: floor → slopes → crowns → clouds; crown jewels labeled |
| `families` (bar) | 35° elevation, 3 u from the hill with the most child lines (2025: trade [proto]) | stems, home threads, ghosts, estate streams |
| `society` (key 8) | 60° elevation, whole land | territories, ties, standing |

### 12.2 Log lines and expected values (2025 unless said; ranges for counts that move with HEAD's lives)

```
[Why] Players  … 123 players (118-128) …; zones floor 99±5, slope 14±2, crown 2±1, cloud 5±1; spills 0;
               max band shift ≤ 0.8 u; checks 4/4 PASS
[Why] Control  2025: 1% 27 lines; controllers 6 lines (4-8) in 2 players (2 running a business, founders 4, heirs 0);
               control wealth 25.8% of the 1%'s (quota 20.4%, ≥ quota, ≤ quota + one unit); successions 1950-2026 ≤ 3;
               1972: 5 controllers, 18.9%; checks 3/3 PASS; checksum …
[Why] Crowns   18 crowns ≥ $100B; controlled named $10.64T + founder stakes $0.72T + top-1% private $8.46T = $19.82T;
               largest internet $6.07T r 0.333 (Alphabet, Meta); private split: proprietors 2024 | (model mix);
               lifts n; checks 3/3 PASS (Σ named = data; Σ private = 0.529 × PBE; every crown below CloudY − 0.2)
[Why] Clouds   5 clouds, 21 lines, $35T; home tilt 0.15; shift from centroid max ≤ 0.6 u; min gap ≥ 0.05, to crowns
               ≥ 0.10; checks 2/2 PASS
[Why] Families minors 727 under 91 players (stems 727, 35 without a parent); dependents 294 (259 threads);
               allowance $1.53T (imputed, $22.0K a child); estates 2021-2025 158 from a parent ($5.61T, 35 in 2025);
               to the 1% 1; ghosts n; successions 0; inherited share cloud 0.86 crown 0.67 floor 0.26 slope 0.18;
               checks 3/3 PASS
[Why] Society  … territories 4 (5 detections), largest component share ≥ 0.8 of each coalition's players; arcs ≤ 112
```

`$SP/redesign/checks/landcheck.py` gets the new lines' parsers and these expectations.

### 12.3 Geometric checks (in the builders, counted in the `checks` above)

1. Every player placed; no two discs of a band or row closer than `PackGap` (along the curve).
2. Floor players: `|y − GroundY(x, z)| < 0.005`, distance to the foot contour within the band's ±(0.09 + spill).
3. Slope players: `GroundY` at the ledge center within ±0.02 of `Contour(k, e)`'s height; within each hill, elevation is
   non-decreasing in mean wealth.
4. Crowns: band bottom > `SummitY + 0.15`, top < `CloudY − 0.20`; clouds at `CloudY` exactly.
5. No cloud overlaps another cloud or a crown in x z (gaps as 6.2).
6. Every child dot's y < its parent dot's y; every stem vertical (|Δx|, |Δz| ≤ 0.012 × siblings).
7. Σ estate streams' dollars = Σ `EstateLog` parent events in the window.

### 12.4 Image checks (python on the harness PNGs and `labels.json`)

* `crowns.base.png`: in each of the 5 largest crowns' projected boxes (the label anchor of `land:crown:<industry>` from
  `labels.json` ± the projected radius), ≥ 40 gold pixels (hue 38-52°, saturation ≥ 0.45, value ≥ 0.55); the clouds'
  pale pixels (saturation ≤ 0.25, value 0.25-0.6) lie above (smaller screen y than) every crown's anchor.
* `people.base.png`: the median screen y of floor players' anchors > slope players' > crowns' > clouds' (the stack reads
  top-down on screen), from a new optional log dump (`WHY_DUMP_LAND=<path>`: per player key, zone, Pos, and its screen
  position per rendered preset).
* `families.base.png`: ≥ 95% of the child dots in frame project below their parent dots.
* Determinism: render twice; the `.base.png` files are byte-identical and the log lines above identical.
* Portrait (`1080x1920`): `crowns` keeps the five largest crowns and at least four clouds inside the frame.

---

## 13. Open points and limits (said in the docs)

* **Model gap**: the lives' top 1% gets 13% of proprietors' income (DFA 53%); controllers are chosen by the quota and
  the career business signal rather than produced by the lives. Lives task: raise the self-employment propensity at the
  top earnings ranks (`SelfEmploymentRank`) until the 1%'s business share approaches the DFA, then retire the ledger's
  "fill" step.
* **Insider data is recalled** until the DEF 14A figures are entered; Tesla (13%) and Amazon (8.3%) fall under the 20%
  voting line and count as founder-led stakes, not control: a threshold of 10% (La Porta's alternative) would move
  Tesla's $1.45T into manufacturing's crown. The threshold is a data parameter (`zones.controlVotes`).
* The named controllers are a few dozen people, far below one line: crowns are measured objects that model players
  sit in, not players themselves.
* Clouds share one capital mix (the DFA's for the 1%); only the 15% home tilt separates them. A per-person portfolio
  (stocks vs deposits from `Financial` and the lives' equity share) is the next refinement, once `Financial` is in
  the record.
* Before 2024 crowns are the private part only (no company data): the 1972 land has small crowns everywhere and no
  jewels; the legend says so.
* With 100,000 people per line, estates are lumpy (35 parent estates in 2025 worth $1.86T, against ~$1.1T a year on
  average); the 5-year window smooths the picture, the inspector gives the year's own.

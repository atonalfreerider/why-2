# The economy as a hill: the theory the scene draws

The economy scene is the end of the causality graph: after matter and life, the newest layer of the same chain of cause
and effect. This note states the theory the scene draws, what each part rests on (measured, judged by Jev, or assumed),
what the October 2026 round added and found, and where the model should go next. The implementation log is
[IMPLEMENTATION.md](IMPLEMENTATION.md); the data and their sources are in `Docs/DATA.md`, `Tools/corporate`,
`Tools/history` and `Tools/social`.

## 1. The claim

An economy is a food chain made of money. Every actor - a household, a company, the state - runs one program:

> money comes in → a mind weighs desire against fear (and reason) → money goes out → the next balance sheet.

Where an actor stands on the hill is what it lives on: wages low on the slope, the claims of owners under the summit,
diversified capital in the clouds, attention above it all. How an actor moves on the hill is the running sum of its
program: what it saves and owns lifts it, what it owes and what it is sold out of fear or manufactured desire pulls it
down, and events (a lost job, a divorce, an inheritance, a war, a rate rise) shift the terms.

## 2. What stands on what

| Layer | Drawn as | Rests on |
| --- | --- | --- |
| The state | the grey bedrock under the whole wave, as deep as federal outlays over GDP, in strata of social programs, military, interest and the rest | OMB historical tables (measured) |
| Its levers on the market | taxes in, transfers and purchases out, regulation up into every hill and capture back down, **the price of money** and **the edge of trade** (the `money` view) | OMB, FRED (fed funds, mortgage, Treasury), BEA customs duties over goods imports (measured); regulation and capture (Jev) |
| The production tiers | raw materials → manufacturing and infrastructure → services → technology, platforms and AI on top; each industry a ridge as wide as its value added | BEA value added 1947-2026 (measured); the tier order and heights are a conceptual hierarchy of dependence |
| Companies | 75 lifelines born, rising and dying on their ridges; 25 stored firms with towers as tall as their market value | filings and market values (measured); how each sells (Jev) |
| People | 116 cohorts on the slope of what pays them, rung by household income rank; the diversified 1% in the clouds | the simulated lives, calibrated to national totals (modeled) |
| Attention | the most followed Americans above capital, laid out by who follows whom | the social graph (measured follows), Wikidata, Jev for identity |

Two honest qualifications. The IO table does not obey the tier order. Of the $15.85T industries paid other industries in
2024 (money runs from the buyer to the supplier), 37% went up to suppliers in a higher tier, 22% went down and 41%
stayed within a tier, counting the state as the bottom tier ($2.1T of the "up" is the state buying supplies, about two
thirds of all it buys from industries). Among the market tiers alone, 28% went up, 25% down and 47% stayed within.
Supplies come down; the money goes up. Tech buys $0.89T from the tiers below and sells them $0.80T, plus $0.29T to the
state. So "standing on" is the dependence of the top on the base, not a one-way flow. And the hill's height is
conceptual: what is measured at the top is the price of claims on the future. In 2025 tech was 9.4% of private value
added and about 9.6% of gross operating surplus, what is left after wages and production taxes (estimated from 2025
value added at each industry's 2024 split). Yet its 10 stored firms alone hold 31.7% of US listed value ($23.9T of
$75.3T, September 2026): the summit stands out in market value, not in output. Its share of domestic corporate profits
is higher, 16.5% in 2025 (BEA's corporate profits by industry, its lines mapped to the tiers by assumption), because the
surplus also covers the wear of buildings and machines, interest, rent and the earnings of the self-employed, which take
a larger part of the other tiers' surplus.

**What if: another government (new, `regimes`).** The bedrock has a measured shape, and the same hills can stand on other ones.
General government spends 37.6% of GDP in the United States (IMF, 2024; the federal block of the other views is its federal
part), 49.2% in Sweden, 33.0% in China and 29.2% in Saudi Arabia; the Soviet state budget spent 47.6% of GDP in 1980. What a
state lives on differs more than how much: income taxes and contributions carry the US (personal income taxes are 35% of
its revenue) and Sweden (social contributions, with its payroll tax, 30%); China's taxes fall mostly on spending and wages
(taxes on goods and services 28% of revenue, social contributions 25%, personal income tax 4%); Saudi Arabia's state lives on
oil (60% of its 2024 revenue); the Soviet budget lived on the turnover tax (31%) and the profits of the enterprises it owned
(30%). Ownership of the summits separates them most. Taking each industry's largest firm on the Fortune Global 500 2026 (for
Sweden and Saudi Arabia, the largest of the firms compared in national sources): every US summit is private; in China the
state controls the largest firm of 10 of the 14 industries the list covers - banking, finance, insurance, telecom, oil,
coal, the power grid, construction, real estate and the post - while private firms lead hardware (Huawei), the internet
(Tencent), manufacturing (Hengli) and retail (JD.com). China's health and agriculture summits are not ranked: Fortune
files their largest firms under wholesalers, and the research's comparison with their largest private firms was lost in
the data loss; the only firms named there, Sinopharm and COFCO, are state-controlled, so the view draws those two
summits as steel dotted "not ranked" rings. Saudi Arabia's state controls oil, chemicals, telecom and power and
holds 37% of the largest bank; Sweden's controls power (Vattenfall) and holds 41% of Telia, while private firms lead its
other summits; in the Soviet Union every summit was the state's by law. The public sector holds 3% of the value of listed
firms in the US, 5% in Sweden, 33% in China and 79% in Saudi Arabia (OECD 2025). The top 1%'s share of pre-tax income runs
from 4% (Russia, 1980) to 21% (the US, 2023), and the electoral democracy index from 0.89 in Sweden and 0.74 in the US
(2025, down from 0.84 in 2024) to 0.07 in China and 0.02 in Saudi Arabia (V-Dem). Each foundation is drawn under the US
hills as a model change - what the same economy would look like standing on that state - not as a picture of those
countries' industries.

## 3. The flows: cause and effect in money

**Households.** Spending splits into desire and fear in two different ways, which should not be confused. The population
model gives each kind of spending a fixed fear share and fantasy share (the author's category weights in `spending.json`),
applied to each simulated household's spending: that is the year readout's "≈ 54% of spending driven by fear, 19% on
fantasy" in 2025 (52.9% and 18.9% at the national accounts' spending weights), a model figure, and the readout shows it
apart from the measured GDP and GSS trust. Which markets are paid out of fear is judged item by item: Jev's judgments of
119 purchase items, shrunk toward each category's stored fear share, split each market's river and give the `you` view
its "sold by fear"; they do not feed the readout. At each market a dollar splits
through the Leontief inverse of the BEA use table into wages (back down the slope), production taxes (to the bedrock),
upkeep, and the owners' surplus (up to the halos and on to whoever holds the claims): 24-31 cents of a household dollar,
by category. **New: which way it flows.** For a median household about 85% of all simulated wages and ownership income
is received by people ranked above it, weighted by its dollar's split between wages and owners (about 42 and 22 cents).
No purchase is traced: the population's distribution stands in for where the household's own dollars end up, on the
assumption that its spending pays people the way everyone's does. The share falls as a household climbs, so near the
top much of it goes to people ranked below. That one number gives the user's "rivers flow uphill or downhill to
corporate entities" a model reading per person (the `you` view), not a measured trace.

**Companies (new, `strategy`).** Firms run the same program at their own scale, and their uses of money have directions:

| Use of money | Direction on the landscape | The 25 largest firms, latest fiscal year |
| --- | --- | --- |
| running the business | out to suppliers (down the hill for the ten tech firms; the other fifteen buy mostly from their own tier or above) and to their own workers (split by the industry's accounts) | $3.32T |
| investment and research | forward over the crest into the future, split into fear (spending so as not to fall behind) and desire (spending for new demand) by Jev | $662B + $287B |
| advertising | up to the attention platforms | $63B disclosed |
| acquisitions and stakes | across, to other firms | $91B |
| buybacks and dividends | up to the owners | $546B |
| cash taxes and lobbying | into the state | $211B; lobbying $191M |

Jev judged which way each firm's money moves it in the hierarchy (a probability over five directions): the data-center
builders **reach down** into their own supply (Alphabet 97%, Oracle 97%, Microsoft 95%, Meta 83%); NVIDIA, Tesla, Amazon
and SpaceX **climb** into AI (70-79%); Visa, Mastercard and Broadcom **harvest** cash for their owners (81-93%); Palantir
**leans on the state** (55%); the oil majors, drug makers and retailers **spread across** their own layer. Investment is
judged most defensive at Walmart, JPMorgan and Bank of America (0.80-0.84) and most expansive at SpaceX, J&J and Costco
(0.20-0.27). The dominant current fact: the five hyperscalers spent **$449B** on capital in 2025 and guide **$805B** for
2026 - the summit is pouring money down the hill into computers and chips and into data-center buildings (85% / 15% by
the 2025 US totals). The top of the hierarchy is building its own bedrock: power, buildings, chips. Lobbying is the
smallest stream and the largest lever ($191M against $211B of taxes), which is why the scene keeps capture as a separate
strand.

**Attention (new, in `social`).** Desire is manufactured in a measurable loop: companies pay the platforms ($295B of
US digital advertising in 2025, $118B of it social), the platforms and brands pay the creators who hold attention
(about $37B), people give about two hours a day and take in wants, and spend - the money returns to the companies that
paid for the wants. The social graph adds a second measure beside reach: how many of the other most-followed accounts
follow a person (Obama 70, Snoop Dogg 69, Rihanna 67, LeBron James and Dwayne Johnson 66, Taylor Swift 61, Elon Musk 55):
influence among the influential.

**The state's levers (new, `money`).** The price of money runs through every river of debt: 16.4% in 1981, under 0.5%
from 2009 to 2016 (when what people own rose with it), 4.2% in 2025. The edge of trade: the effective tariff rate fell
from 7.0% in 1947 to 1.4-1.7% in the 2010s and tripled in 2025 (2.6% → 8.0%; 2026's first half 8.8%, gross of the IEEPA
refunds). A federal democracy with free trade is drawn by these settings, and other regimes by others (the `regimes`
view, section 2).

## 4. Moving up or down the hill (new, `mobility`)

**Forces at each rung** (2025, on the like-for-like ladder the mobility view draws: adults 25 and older in 2015,
followed to 2025 and ranked into fifths of household income among themselves): saving runs from -12% of income at the
bottom to +17% at the top; income from what people own from 13% to 32%; debt service falls from 22% to 9%; the financial
runway goes from 1.1 to 3.4 years; the share in control of their path from 4% to 27%. The lifting forces compound where
they are already strong, and the pulling forces bite where they are already heavy.

**The decade's ladder** (2015-2025). The view draws it like for like with the US Treasury's closed panel of taxpayers
25+ (1996-2005): the model's bottom fifth keeps 58% (Treasury 55%) and its top fifth 66% (Treasury 61%), with 3% going
from bottom to top (3.6%). Counting every adult alive at both ends instead (the view's All adults setting), 33% climbed
a fifth or more, 48% stayed and 20% fell, and the top fifth kept 71% of its members; the bottom looks more fluid (34%
stay) because young adults rise as they leave home and start work.

**What comes with a move**, among adults 25-54 (each event's share of the movers and its lift over everyone's rate):
falls came with a lost job (59%, 1.6x), a divorce or separation (22%, 2.3x) and discharged debt (9%, 2.2x); climbs with
finding work (49%), a home (41%), a child (37%; households forming) and marriage (28%, 2.0x). The event studies agree in direction: displaced workers lose about a quarter of a year's earnings in an expansion;
a late-life divorce cuts women's household income 41%; a hospital stay cuts earnings about 20%. These are associations
in a calibrated simulation, not causal estimates.

**The individual (new, `you`).** The model becomes a mirror: a viewer's household is placed among the year's simulated
adults (income and wealth percentiles, rung on their hill), their dollar is split, the share of all wages and owners'
income above their rank read, their cushion (net worth, home included) and the years to living on what they own
computed, and their odds read off the decade's ladder for their fifth and age.

## 5. Measured, judged, assumed

| Measured | Judged by Jev (model evidence, not measurement) | Assumed |
| --- | --- | --- |
| value added, the use table, national and personal accounts, OMB budgets, filings of 25 firms, US totals (BEA, Census, NSF, IAB, OpenSecrets, S&P), rates, tariffs, trade shares, the social graph's follows, published mobility matrices; for the other regimes: general government spending and revenue (IMF), tax structures (OECD) and budget lines (Saudi MoF, the Soviet budget), the state's share of listed firms (OECD) and of named firms (filings, registers), trade (World Bank), the top 1%'s observed shares (WID), the electoral democracy index (V-Dem) | how purchases and companies sell; regulation and capture, for the US and for each other regime; which way each firm's money moves it, how defensive its investment is, its AI share; people's identities and roles in the social graph | tier order and heights; class as altitude; drive weights and behavioral parameters of the lives; the 4% yield and three-year runway; a firm's supplier/worker split by its industry; the AI build-out's split by US totals; another regime's foundation under the US hills, a summit as its industry's largest named firm, and the state's share of spending drawn through every river alike |

## 6. Where the model should go next

1. **Other governments as model changes** (first version done: the `regimes` view, section 2). Next: each regime on its
   own hills (its industries' value added) rather than the US's; regimes through time, so a foundation moves along the wave
   (the Soviet budget into the 1990s, China's state share since 2000, Sweden since its 1990s crisis); per-industry state
   shares of sales from the OECD Global SOE Dataset in place of the largest-named-firm rule; and more foundations (a
   developmental state such as South Korea, a European social market economy such as Germany). The civilization layer
   already carries 40 states' fiscal series.
2. **Height from capitalization.** Measure the hill's height as the market value of each industry's claims per dollar of
   value added (S&P sector weights since 1990, Fed Z.1 equities), so "the top of the hill" is a number.
3. **Companies through time.** The 25 firms' filings for 2015-2025 would show the defensive share and direction moving
   through the cycle (the AI race since 2023), and let the scenario program step firms as it steps households.
4. **The wave in motion.** Replay the crest from 1947 to now with the levers (rates, tariffs) and the ladder updating, so
   cause and effect plays out rather than being read off.
5. **Voice the five new stops** (`Tools/synthesize-narration-elevenlabs.py --only economy_narration`) and check the
   focused views in the headset (label sizes; the steppers by pinch).

# Build-out: who owns the data centres, and who pays

Cluster `buildout`, as of 7 Nov 2026. Every number below is a fact that passed the truth gate (59 of 59). Figures from
aggregators that quote filings are flagged in `buildout.json`.

## The shape of the build

The owners of data-centre power fall into four kinds, and each pays for it differently.

**Cash-rich clouds that still return money.** Microsoft added 31 data centres and 1 GW in the quarter to June
[buildout-microsoft-1gw-quarter], after 2 GW in 2025 [buildout-microsoft-2gw-fy2025]. Its property and equipment is
$346.8bn [buildout-microsoft-ppe], it added $39.4bn in cash in one quarter [buildout-microsoft-additions], and it still
paid $4.4bn of buybacks and $6.8bn of dividends [buildout-microsoft-buybacks] [buildout-microsoft-dividends]. Its debt
is small beside its assets. This is the advantage of the fortress: the share price and the cash flow pay for the build.

**Companies that have started to borrow.** Amazon's long-term debt rose from $65.6bn to $173.1bn in nine months
[buildout-amazon-debt], including $48.1bn raised in Q3 alone [buildout-amazon-q3-debt-proceeds]. Meta's property and
equipment grew from $176.4bn to $253.2bn [buildout-meta-ppe] with $80.9bn of debt [buildout-meta-debt]. Meta made no
buyback in the first nine months of 2026, against $26.2bn a year earlier [buildout-meta-no-buybacks]: the stock is no
longer the weapon, the balance sheet is. Alphabet shows a $561bn Google Cloud backlog [buildout-google-cloud-backlog];
its own property figure sits in a 10-Q the script could not read.

**Companies that build with other people's balance sheets.** Meta's Hyperion campus in Louisiana is a joint venture in
which Blue Owl funds own 80% and Meta 20% [buildout-hyperion-jv-split], on about $27bn of development cost
[buildout-hyperion-devcost]. Blue Owl put in about $7bn and Meta took out about $3bn [buildout-hyperion-blueowl-cash].
The venture's vehicle, Beignet Investor, priced $27.3bn of 6.581% bonds due 2049 [buildout-hyperion-bond]. Meta leases
the campus and guarantees its value for 16 years [buildout-hyperion-rvg]. Hyperion is planned to grow from 2 GW to 5 GW
with more than $50bn of investment [buildout-meta-hyperion-5gw]. In July 2026 Meta repeated the structure in El Paso
with BlackRock-managed funds at 80% [buildout-elpaso-80pct], 1 GW [buildout-meta-elpaso-1gw] and about $14bn of cost
[buildout-elpaso-devcost]. The debt lives in the vehicle; the obligation lives with Meta. A strategist should count the
guarantee as a contingent liability.

**Companies that build for one customer, or for rent.**
- Oracle's property and equipment is $127.8bn [buildout-oracle-ppe] and its non-current borrowings $117.7bn
  [buildout-oracle-debt], and it says it has leased another 4.5 GW for OpenAI [buildout-oracle-45gw-leasing]. A
  record $38bn construction loan to Vantage, split $23.25bn Texas and $14.75bn Wisconsin, funds campuses Oracle leases
  [buildout-oracle-vantage-loan] [buildout-oracle-vantage-split]. Oracle's rating sits one notch above junk after a July
  downgrade [buildout-oracle-credit-rating]. At Abilene the discussed lift from 1.2 GW to 2.0 GW was not pursued
  [buildout-stargate-abilene-12gw].
- CoreWeave has 1.5 GW active [buildout-coreweave-active-power] and 4.2 GW contracted [buildout-coreweave-contracted-power],
  property of $46.7bn [buildout-coreweave-ppe], and funds it with a $3.1bn syndicated term loan
  [buildout-coreweave-term-loan], over $10bn of unsecured and convertible debt [buildout-coreweave-unsecured] and a $1bn
  stake from Jane Street [buildout-coreweave-jane-street]. Short contracts price at about $40m per MW of annual revenue
  [buildout-coreweave-price-per-mw].
- SpaceX (with xAI) is the newest owner. SemiAnalysis counts 1.7 GW operating and 3.6 GW under construction
  [buildout-spacex-operating-17gw], aiming for 13 GW in 2027 [buildout-spacex-13gw-target]. Imagery in January showed
  cooling for 350 MW at Colossus 2 [buildout-spacex-colossus2-cooling], well below the claimed gigawatt. It financed the
  build with an IPO and a debut bond of at least $20bn [buildout-spacex-bond], after $12bn of debt talks in 2025
  [buildout-xai-colossus2-debt-2025]. Its tenants: Reflection at $150m a month, $6.3bn in all
  [buildout-reflection-spacex-lease], and Anthropic and Google at about $26bn a year combined, cancellable in 90 days
  [buildout-spacex-annual-contract-value].

## The dependency chain's money

Labs pay clouds; clouds and neoclouds pay NVIDIA; lenders and owners fill the gap between the two. The labs-to-cloud
contracts are in the `labs` cluster (OpenAI-Oracle, OpenAI-AWS, Anthropic-Azure). Two things are new here.
- NVIDIA's concentration: four direct customers are 61% of revenue and the largest 22% [buildout-nvidia-customer-concentration];
  its disclosed Blackwell and Rubin backlog is $1 trillion through 2027 [buildout-nvidia-backlog]. NVIDIA is the single point
  of supply; its demand is in a handful of buyers' hands.
- The financing layer: bond investors (Pimco was the anchor of the Hyperion bond), syndicated banks (JPMorgan and
  MUFG on the Vantage loan) and Blue Owl and BlackRock as equity owners of campuses. These actors turn data-centre rent
  into investment-grade paper. The flows in `buildout.json` mark one-off raises as such.

## The individual end of the rivers

- Employers paid $16,488bn a year in compensation in Q3 2026 [buildout-us-compensation-q3]; wages and salaries ran at
  $13,621bn in September [buildout-us-wages-sep]; households saved $911bn a year, 3.8% of disposable income
  [buildout-us-personal-saving-sep]. Corporate profits after tax ran at $4,280bn [buildout-us-corporate-profits-q2].
- S&P 500 companies paid out a record $1,020bn in buybacks and $664.9bn in dividends in the 12 months to September 2025
  [buildout-sp500-buybacks-12m] [buildout-sp500-dividends-12m], a record $1,685bn in all [buildout-sp500-total-return-12m]. The
  top 20 firms did 49.5% of the buybacks [buildout-sp500-buyback-concentration], and Apple, NVIDIA, Alphabet and Meta
  alone $55.2bn in a quarter [buildout-sp500-top4-buybacks].
- Who gets it: the top 1% of households held 50.9% of corporate equities and fund shares in Q2 2026
  [buildout-top1-equity-share], up from 42.8% in 1989 [buildout-top1-equity-share-1989]. The buyback and dividend rivers
  therefore end mostly in the top percentile's accounts and in funds, not in wages.

## Choke points and flashpoints

- **NVIDIA's customer base** and **grid power** (Amazon's CEO: maybe the bottleneck is power [buildout-jassy-power-bottleneck]).
  SpaceX's answer is on-site power; Meta's is Entergy's gas plants.
- Flashpoints in the file: Meta's off-balance-sheet Hyperion (`fp-hyperion-spv`), SpaceX's listing as a war chest
  (`fp-spacex-warchest`), Oracle borrowing for one customer (`fp-oracle-leverage`) and CoreWeave's debt-funded power
  (`fp-neocloud-leverage`).

## What a strategist would predict

The build splits the field by how it is paid for. Microsoft, and Alphabet with its cash, can slow or accelerate by choice.
Meta and Amazon have moved from buybacks to borrowing and joint ventures: they keep control but carry guarantees. Oracle,
CoreWeave and SpaceX depend on the continued willingness of lenders and a few tenants: Oracle on OpenAI, CoreWeave on
short contracts at high prices, SpaceX on 90-day cancellable leases. If model demand disappoints, the stress arrives in
this order: neocloud refinancing, Oracle's rating, vehicle bonds with residual guarantees, and last the cash-rich clouds.
If it does not, NVIDIA's four buyers keep absorbing its supply and the owners' share of the equity, already half in the
top 1%, grows with it.

## Sources

Meta Q3 2026 release and the Hyperion and El Paso announcements; Microsoft FY27 Q1 release; Oracle Q1 FY27 release;
CoreWeave Q2 2026 release and September release; DatacenterDynamics; Tom's Hardware; IFR; AI Weekly (Bloomberg,
SemiAnalysis); The Next Web; Fourweekmba; 24/7 Wall St; Daloopa; BEA and the Federal Reserve through FRED; BEA
Personal Income and Outlays; S&P Dow Jones Indices (via press.spglobal.com).

Verified: 59 of 59.

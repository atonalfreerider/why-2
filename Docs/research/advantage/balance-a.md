# Balance sheets and P/E of fifteen companies (balance-a)

Written 7 Nov 2026. Each company is shown as a balance sheet at its latest reported quarter, plus the market's
trailing P/E. Money is USD billions. Dates differ by fiscal calendar: NVIDIA 26 Jul, Broadcom 2 Aug, Oracle 31 Aug,
CoreWeave 30 Jun, the rest 26-30 Sep 2026. Fact ids follow the pattern `balancea-<actor>-<item>`, for example
[balancea-nvidia-totalAssets].

## What the sheets say

**Who is built of property (the physical build-out).** Amazon's property and equipment is 605.8 [balancea-amazon-ppe],
Alphabet's 393.1 [balancea-google-ppe], Microsoft's 346.8 [balancea-microsoft-ppe], Meta's 279.8 [balancea-meta-ppe],
Oracle's 161.8 [balancea-oracle-ppe], SpaceX's 90.5 [balancea-spacex-ppe] and CoreWeave's 63.3
[balancea-coreweave-ppe]. NVIDIA holds only 14.3 [balancea-nvidia-ppe]: it designs the chips and leaves the
factories and the data centres to others. That is the asset-light undergirder the user described.

**Who is built of cash.** Alphabet holds 265.1 [balancea-google-cash], NVIDIA 99.4 [balancea-nvidia-cash] (a third of it
in marketable equity securities, 42.8), SpaceX 89.9 [balancea-spacex-cash], Amazon 114.2 [balancea-amazon-cash],
Meta 82.4 [balancea-meta-cash]. CoreWeave holds 5.5 [balancea-coreweave-cash] against 51.6 of debt.

**Who is built of debt.** Amazon's total debt (including leases, on this source's measure) is 304.8
[balancea-amazon-debt] and its lease liabilities 98.8 [balancea-amazon-leases]. Oracle's debt of 169.1
[balancea-oracle-debt] sits against equity of only 61.8 [balancea-oracle-equity]: debt is 2.7 times equity. CoreWeave's
51.6 [balancea-coreweave-debt] against equity of 5.0 [balancea-coreweave-equity] is about ten times. Meta's is 114.8
[balancea-meta-debt], Alphabet's 150.3 [balancea-google-debt]. Microsoft carries only 35.5 [balancea-microsoft-debt] and
NVIDIA 33.4 [balancea-nvidia-debt]; Palantir almost none (0.2 [balancea-palantir-debt]).

**Who is built of what it bought.** Broadcom's goodwill is 97.8 and its intangibles 26.3 of 188.1 of assets
[balancea-broadcom-goodwill] [balancea-broadcom-intangibles], two thirds of the sheet. Microsoft's goodwill is 119.6
[balancea-microsoft-goodwill]; SpaceX's rose to 48.4 [balancea-spacex-goodwill] after the xAI merger (it was 11.6 the
quarter before, on the same page). AMD carries 25.5 plus 15.1 [balancea-amd-goodwill] [balancea-amd-intangibles] against
equity of 68.4.

**Stakes in other companies.** NVIDIA's non-marketable securities are 51.2, up from 22.3 in January
[balancea-nvidia-investments]: it is now a large investor in its own customers. Alphabet's long-term investments are
135.2 [balancea-google-investments], Amazon's 148.4 [balancea-amazon-investments], Microsoft's equity and other
investments 37.8 [balancea-microsoft-investments].

## The speculative shell: P/E

The market's trailing P/E (companiesmarketcap.com, November 2026): Alphabet 14.7 [balancea-google-pe-ratio], Oracle 17.8
[balancea-oracle-pe-ratio], Netflix 18.1 [balancea-netflix-pe-ratio], Meta 20.8 [balancea-meta-pe-ratio], Amazon 20.9
[balancea-amazon-pe-ratio], NVIDIA 23.6 [balancea-nvidia-pe-ratio], Microsoft 27.8 [balancea-microsoft-pe-ratio], Broadcom
36.3 [balancea-broadcom-pe-ratio], Apple 39.4 [balancea-apple-pe-ratio], AMD 112.9 [balancea-amd-pe-ratio], Palantir
127.3 [balancea-palantir-pe-ratio], Tesla 363.2 [balancea-tesla-pe-ratio]. Intel (-23.9), CoreWeave (-12.5) and SpaceX
(-579.4) have trailing losses, so their P/E is negative: their shells are pure expectation
[balancea-intel-pe-ratio] [balancea-coreweave-pe-ratio] [balancea-spacex-pe-ratio]. A strategist reads the spread as
the size of the premium that has to be earned: NVIDIA at 23.6 has a modest shell for a company whose quarterly revenue doubled; Tesla, Palantir and AMD carry shells that need years of growth to justify; CoreWeave and SpaceX
have a shell, a large asset base and no earnings.

## Commitments: the weight that does not show on the sheet

NVIDIA's 10-Q lists supply and capacity commitments of 279, up from 119 a quarter earlier, and 366 in all (with cloud
service agreements 29, data-centre leases not yet commenced 25, equity-investment commitments 25, capital expenditures
8) [balancea-nvidia-commit-supply] [balancea-nvidia-commit-supply-jump] [balancea-nvidia-commit-total]. NVIDIA is the
buyer of memory and packaging capacity for the whole stack, and it has committed to buy back compute and to fund
stakes. Backlogs are the other side: Microsoft's commercial remaining performance obligation is 677
[balancea-microsoft-commit-rpo], Oracle's 664 [balancea-oracle-commit-rpo], CoreWeave's revenue backlog 104
[balancea-coreweave-commit-backlog]. Oracle's backlog is more than twice its total assets of 303.3
[balancea-oracle-totalAssets]; CoreWeave's 104 is larger than its 77.1 of total assets [balancea-coreweave-totalAssets].
The dependency chain is a chain of promises: labs promise to buy compute from Oracle and CoreWeave, they borrow
against those promises to buy NVIDIA's chips, and NVIDIA commits to buy supply for the next several years.

## What a strategist would predict

- **Survivors with a fortress sheet:** Alphabet (cash 265.1, equity 659.5, P/E 14.7), Microsoft (equity 470.5, debt
  35.5), NVIDIA (cash 99.4, debt 33.4), Apple (cash 56.7 plus 80.2 of long-term securities).
- **Fragile, leveraged builders:** CoreWeave (equity 5.0 against debt 51.6 and leases 15.9), Oracle (debt 169.1 against
  equity 61.8, free cash flow negative 5 billion in its latest quarter per its release). A fall in AI contract
  renewals or an interest-rate rise breaks these first; their backlogs are only as good as their customers.
- **Expectation shells:** Tesla (363 times), Palantir (127), AMD (113), SpaceX and CoreWeave (loss-making). These
  can inflate or burst; their balance sheets show little to absorb a repricing, except SpaceX's 89.9 of cash and
  Palantir's 10.6.
- **Asset-light versus asset-heavy:** if demand for compute stalls, NVIDIA's inventory and commitments hurt, but the
  property-heavy hyperscalers (Amazon 605.8, Alphabet 393.1, Microsoft 346.8, Meta 279.8) carry fixed costs that do not
  shrink.

## Caveats

Balance sheets for 13 of 15 companies come from stockanalysis.com (an aggregator of filings, USD millions, newest column)
because sec.gov refuses scripts; NVIDIA's and Microsoft's are from their own releases. On that source "Total Debt"
includes lease liabilities, and PP&E may include finance-lease assets (Apple 63.7 there against 52.1 in Apple's own
release). Equity is "Total Common Equity" or "Shareholders' Equity", as the page labels it. CoreWeave's latest sheet is
30 Jun 2026. NVIDIA's commitments are read from a secondary article that quotes the 10-Q. Lease commitments not yet
commenced for Oracle, Microsoft and Meta, and Meta's, Alphabet's and Amazon's purchase commitments, were not found as
fetchable text.

## Sources

NVIDIA Q2 FY2027 release (nvidianews.nvidia.com); Microsoft Q1 FY2027 release (microsoft.com/investor); Oracle Q1 FY27
release (oracle.com); CoreWeave Q2 2026 release (investors.coreweave.com); stockanalysis.com quarterly balance sheets;
companiesmarketcap.com P/E pages; fourweekmba.com on NVIDIA's 10-Q.

**Verified facts: 161 of 161** (gate run 7 Nov 2026).

# Competitive advantage, round two: balance sheets, owners and the stock as a weapon

Written 7 Nov 2026 for the second round of research. Read `brief.md` first: its rules, tools, schema and canonical
actor ids all hold. This file adds the user's new direction, the new fact kinds and the new clusters. Today is
**7 November 2026**. Every 2026 number must come from a page you fetch today.

## The user's new direction (7 Nov 2026, verbatim)

> I don't like the conic graphics or the flat hillside. We want to have a much more intricate graphic visualization.
> Nodes that are connected together, if it's useful, a force-directed, three-dimensional layout. The speculative size
> of companies is something that needs to be visualized graphically. We have the price-to-earnings ratio that is a
> useful metric for this. A high share price is also a competitive advantage for a company, and this is a common tactic
> that large tech companies use, is a very large share price allows for a higher capital expenditures, acquisitions of
> competitors, artificially lowering prices so that market share can be accumulated. I think that we want to be showing
> all of these competitive dynamics. In the year 2026, there is an absolute ownership and dominance of the tech market
> by a select few companies, and they use their size and their monopolization of talent and capital to prevent other
> competitors from entering the market. And often they will take stock price funded losses on products to acquire the
> market, brand recognition, and other trades so that future value can be assured. This is all subject to risk and
> eventualities where markets collapse and companies fail if these high-risk strategies are followed. Try to break
> apart each company into effectively how a balance sheet would be broken down, but in a three-dimensional
> force-directed graph model, and show the interconnections between other companies that are dependencies. So again,
> in the example of AI companies, they have a capital dependency on companies like NVIDIA. NVIDIA is an undergirder of
> these AI frontier companies. NVIDIA wouldn't be as large as it is if there wasn't demand upstream of the GPU
> infrastructure. There's also all of the companies that do build outs and own the infrastructure of the data
> centers. This would be companies like Microsoft and SpaceX. We need to break these into smaller pieces, but still
> show a collective whole and do network graphs, and then also show the rivers of capital, both at the corporate level
> and the individual level, and then still maintain the ownership class, the individuals that actually own shares of
> the companies as the golden circles. These are not just the corporate managers. They're also private equity, hedge
> funds, sovereign funds, and any other significant owners of corporate shares. We want to show a true Darwinian
> survival of the fittest modern corporate simulation and use high ground and low ground as a way to visualize the
> relative position in the market.

## What the research is for

The 2026 contest becomes a 3D force-directed network:

- **Companies as organisms.** Each company is a cluster of balance-sheet parts around a core:
  - cash and securities;
  - property and equipment (data centres, fabs);
  - goodwill and intangibles (what it bought);
  - stakes in other companies;
  - debt, leases and purchase commitments.
- **Speculative size.** A shell as large as the market value surrounds each cluster, so the gap between the shell and
  the book value is the speculative premium. The price-to-earnings ratio shows how inflated that shell is.
- **Edges and rivers.** Edges join companies that depend on each other: supply, compute, capital and commitments.
  Rivers of capital run along them, between companies and down to people: wages, spending, dividends, buybacks and
  savings.
- **Owners above.** The owners float above as golden circles: index managers, sovereign funds, founders, private
  equity, venture capital, hedge funds, corporate holders, and the households that own shares through them.
- **High and low ground.** Altitude is relative market position.
- **A Darwinian simulation.** The view runs 2026 to 2030 under scenarios. Shells inflate or burst, cash runs down, debt
  weighs, and companies survive, are absorbed or fail.

## New fact kinds

Use them with the brief's schema. Money is in `"unit": "USD bn"`, unless the source states another currency, and
`period` names the balance-sheet date or the period covered.

- **Balance sheet** (latest reported quarter):
  - `totalAssets`;
  - `cash` (cash, equivalents and marketable securities, as the company groups them);
  - `ppe` (property and equipment, net; say in notes whether finance-lease assets are included);
  - `goodwill`, `intangibles`;
  - `investments` (non-marketable and equity-method stakes, strategic investments);
  - `totalLiabilities`;
  - `debt` (short- and long-term borrowings);
  - `leases` (operating-lease liabilities);
  - `equity` (total shareholders' equity);
  - `commitments` (purchase obligations or other off-balance-sheet commitments, with what they are for in notes).
- **Valuation:** `peRatio` (unit "x", trailing twelve months, with its source's date), `netIncomeTTM`, `revenueTTM`.
- **Stock as a weapon:**
  - `buybacks` and `dividends` (paid in a stated period);
  - `sbc` (stock-based compensation expense in a period);
  - `acquisition`, with `from` the buyer, `to` the target, `value` in USD bn and how it was paid in notes (stock,
    cash, a licence-and-hire deal);
  - `segmentLoss` (the operating loss of a subsidised unit);
  - `burn` (a private company's loss or cash burn);
  - `pricing` (evidence of pricing below cost to win share; `value` is the year);
  - `talent` (pay packages, hiring raids; `value` the amount or the year).
- **Ownership:** `stake`, with `from` the owner and `to` the company, `value` the percentage (unit "%") or the dollar
  value (unit "USD bn"). Say in `period` the date of the holding, and in notes the other measure if the source gives
  both.
- **Build-out:** `capacity` (GW or MW of data-centre power) and `financing` (debt, special-purpose vehicles, leases
  raised for data centres; `from` the lender, `to` the borrower).
- **Individual level:** `flow`, between `households` (or a wealth group) and companies or funds: wages, dividends,
  buybacks, retirement savings into funds.

## New canonical actor ids (owners)

| Group | Ids |
| --- | --- |
| Index and asset managers | `vanguard`, `blackrock`, `state-street`, `fidelity`, `capital-group` |
| Sovereign | `nbim` (Norway), `pif` (Saudi Arabia), `qia` (Qatar), `adia`, `mubadala` / `mgx` (Abu Dhabi), `gic`, `temasek` (Singapore), `cic` (China) |
| Private equity, venture capital, hedge funds | `blackstone`, `kkr`, `apollo`, `blue-owl`, `thrive-capital`, `a16z`, `sequoia`, `lightspeed`, `citadel`, `bridgewater`, `renaissance` |
| Corporate holders | `softbank`, `microsoft`, `amazon`, `google`, `nvidia`, `berkshire`, `openai-foundation` |
| Founders and other people | `elon-musk`, `mark-zuckerberg`, `jeff-bezos`, `larry-ellison`, `larry-page`, `sergey-brin`, `jensen-huang`, `steve-ballmer`, `michael-dell`, `bill-gates`, `warren-buffett`, `sam-altman` |
| Households | `households`, or the wealth groups `top1`, `next9`, `next40`, `bottom50` (the app already holds the Fed's Distributional Financial Accounts for these: research them only for new 2026 figures) |

Use `kind` `fund`, `sovereign`, `pe`, `vc`, `hedge`, `corporate`, `person` or `households`, and `layer: "owners"`.

## The clusters

| Cluster | Scope |
| --- | --- |
| `balance-a` | Balance sheets and P/E for NVIDIA, Microsoft, Apple, Alphabet, Amazon, Meta, Broadcom, Tesla, Oracle, AMD, Intel, CoreWeave, Palantir, Netflix and SpaceX, which includes xAI since their merger and has been listed since June 2026. Use each company's latest earnings release with a condensed balance sheet (investor-relations pages, or press-release copies on prnewswire or businesswire). Use companiesmarketcap.com's `/pe-ratio/` page for the P/E, and its `/total-assets/`, `/total-debt/`, `/cash-on-hand/` and `/net-assets/` pages where a release will not fetch. Record purchase commitments where the release or a filing excerpt gives them: NVIDIA's supply commitments, and Oracle's and Microsoft's remaining performance obligations and lease commitments. |
| `balance-b` | The same for TSMC, ASML, SK Hynix, Samsung Electronics, Micron, Arm, Dell, Arista, GE Vernova, Constellation Energy, Berkshire Hathaway, JPMorgan, Eli Lilly, Walmart and Tencent. Give foreign currencies as reported, and add a USD conversion fact from one fetchable exchange-rate source dated today, or record the source's own USD figure. For OpenAI and Anthropic, which are private, record capital raised to date, the latest valuation (already in the labs cluster; do not duplicate), losses or burn, and compute commitments. |
| `owners` | Who owns the companies above, as golden circles. Record, for each company: the top institutional holders (Vanguard, BlackRock, State Street) with their percentages; founders' and insiders' stakes (Musk in Tesla and SpaceX, Zuckerberg's stake and voting control in Meta, Bezos in Amazon, Ellison in Oracle, Page and Brin in Alphabet, Huang in NVIDIA, Dell in Dell); sovereign holders (Norway's fund's stakes, which it publishes, and PIF, QIA, the Abu Dhabi funds, GIC); corporate cross-holdings (Microsoft in OpenAI, SoftBank in OpenAI and Arm, Amazon and Google in Anthropic, Google in SpaceX, NVIDIA's stakes, Berkshire in Apple); the private labs' main holders (venture capital and private equity); and hedge funds where a company's top-holder list names them. Also the 2026 total of US equities held by households directly and through funds, by foreigners and by sovereign funds, if a fetchable source gives it. |
| `weapons` | The share price as a weapon, and its risk. Record buybacks, dividends and stock-based compensation (2025 or the latest four quarters) for the giants, and the notable acquisitions and licence-and-hire deals of 2012-2026 with values and how they were paid (Instagram, WhatsApp, LinkedIn, Activision, Mellanox, Wiz, Inflection, Character.AI, Windsurf, Scale AI's 49% to Meta and others). Record losses funded by other profits or by the share price: Meta's Reality Labs, Alphabet's Other Bets, Amazon's Kuiper, the labs' burn. Record evidence of pricing below cost to win share, the talent contest (pay packages, hiring raids), and barriers to entry (capex scale, exclusive compute). Then the risk: when this strategy failed (Cisco's 2000 valuation and its fall, Meta's 2022 drawdown, WeWork, FTX, SVB) and what markets did when the cycle turned. |
| `buildout` | Who builds and owns the data centres, and how it is paid for. Record the owners and their capacity (GW) and data-centre property and equipment: Microsoft, Amazon, Google, Meta, Oracle, CoreWeave, xAI within SpaceX (Colossus), and Stargate (OpenAI, SoftBank, Oracle, MGX). Record the financing (Meta's Hyperion special-purpose vehicle with Blue Owl, Oracle's and CoreWeave's debt, xAI's debt, lease structures) and the dependency chain's money (labs to clouds to NVIDIA, and NVIDIA's customer concentration). Then the individual level: 2025-2026 totals of US corporate buybacks and dividends, compensation of employees (BEA), flows into passive funds or 401(k) plans, and the share of US stocks held by the top 1% (Fed DFA, latest quarter). |

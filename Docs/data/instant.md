# `Data/economy/instant.json`: the US economy at one instant

`instant.json` feeds the view "One year, all at once" (M1; [MODALITIES.md](../hills-design/MODALITIES.md) section 5). It holds the
measured stocks and flows the view draws:

- households ranked by income in the Fed's six DFA income groups: their balance sheets, and the year's income, taxes,
  spending and saving;
- the state's accounts;
- corporate profits by tier;
- listed value;
- federal debt by holder;
- one reconciled figure for the hyperscalers' capital spending.

Work package WP02 in [MODALITIES-PLAN.md](../hills-design/MODALITIES-PLAN.md). `InstantModel` (WP10) reads the file.

- **Builder:** `Tools/instant/build.py`, in the pattern of `Tools/regimes/build.py`.
- **Configuration and hand-read slots:** `Tools/instant/sources.json`. It says which rows and columns to read, holds the
  crosswalks to tiers, and has empty slots for the values a person must read by hand.
- **Cache:** `Tools/instant/.cache` (gitignored). Every download sits there with a `.http.json` sidecar recording the URL,
  the retrieval date and the server's Last-Modified date.

```
python3 -I Tools/instant/build.py            # fetch what is not cached, write the file, print the checks
python3 -I Tools/instant/build.py --offline  # rebuild from the cache only: byte-identical output
```

The build prints one `CHECK` line per test, then:

- `SOURCE FAILED` lines, each with the fallback taken;
- `ASSUMED ≈` lines for every modelled or assumed value.

It exits with status 1 if any check fails, but writes the file either way. Two options exist for testing:

- `--out PATH` writes the file somewhere else.
- `--simulate-missing KEY[,KEY]` treats sources as unreachable:
  - `dfa-income`: the income-group file, everywhere. This runs the spec's fallback: `classBasis = "wealth"` with the
    four DFA wealth groups.
  - `dfa-wealth`: the wealth-group file, everywhere.
  - `dfa-mirror`: both files on the mirror.
  - `dfa-zip`: the Fed's download.

**Where the DFA groups come from**, in order:

1. The income-group file on the mirror pinned to circuit.json's release.
2. If the mirror cannot be read, the same file from the Fed's current download. That is a later release; the log says
   so and `classes.dfa.sameReleaseAsCircuit` is false.
3. Only if no income-group file can be read at all, the four wealth groups (`classBasis = "wealth"`):
   - from the mirror;
   - else from the Fed's download;
   - else from circuit.json `groups` (net worth and households only: no instruments, no annual history).

Every source that fails prints a `SOURCE FAILED` line with the fallback taken.

## Conventions

- Money is in billions of current dollars (`$B`) unless a block says otherwise. Shares run from 0 to 1. Households are in
  millions.
- A single value is an object `{value, unit, year, source, url, quote | field, note}`. `field` names the API field, CSV
  column or flat-file series. `quote` is text copied from the document `url` names (whitespace-normalised).
  - The build checks every quote against that document: a repository file, or the cached download.
  - Where a value is read from a repository file that cites a filing (the hyperscaler firms), `url` is the repository
    file and `filingUrl` the filing.
- A table states its provenance once, at the top: `source`, `url` and `field` (or `computed`). Its arrays follow the
  order of `groups`. Where an array is indexed by year, `values[i]` is year `first + i`.
- The build checks that every number in the file has provenance on its own object or on an enclosing block. Dates and
  indices are exempt.
- `"approx": true`, plus a note beginning with `≈`, marks a modelled split. These are never measurements.
- `"assumed": true`, or the label `assumed: mapping`, marks a drawing choice: the crosswalks to the scene's tiers.
- The JSON keeps its own build report: `checks`, `failedSources`, `assumptions`, and `sources` (every download with its
  retrieval and Last-Modified dates).

## What the file holds, and where M1 uses it

| Block | Contents | Used in M1 for |
| --- | --- | --- |
| `classes.groups` | bottom 20%, 20-40, 40-60, 60-80, 80-99, top 1% (`classBasis: "income"`) | the class tower's six floors; every key says "ranked by household income" |
| `classes.dfa` | DFA balance sheets by income group: 12 instruments (real estate, company shares and funds, private businesses, pensions, deposits, money market funds, bonds, durables, other; mortgages, consumer credit, other debts) with the totals and household counts. It has the latest quarter (2026:Q1), annual means for 1989-2026, the Fed's newer release (2026:Q2) beside them, the DFA's own definitions, and `householdDebt` with its `comparisons` (all liabilities, the newer release, Z.1 ≈) | the balance-sheet rows (assets outward, debts inward, hatched), "net worth $x T (24.5% of all) · DFA 2026 Q1", the people state's household counts |
| `classes.flows` | income, taxes, spending and saving by group for 2025 (≈): nine income components with their shares, the derived totals (personal income, gross income, disposable income, PCE, saving, other outlays), a per-group `statement` (its provenance is `classes.flows.source` and `.computed`), the top 1%'s split steps, and shares for every BEA year (income 2000-2024, spending 2000-2023) | floor heights (gross income × 5 units per $T), the source mosaic (pay · owning · state), the use mosaic (taxes · spending · saving or borrowing), the saving and borrowing stubs, class splits in other years |
| `classes.flows.circuitVintage` | the same group flows on circuit.json's pre-update 2025 totals, the vintage MODALITIES.md and WP10 quote (class gross income $28.12T, PCE $20.95T). Saving is the residual here (see below) | M1, if the spec owner keeps that vintage (open question) |
| `classes.spendingMix` | BLS CE 2024 and 2025: 14 spending categories by income fifth, as dollars per consumer unit and as shares. `sixGroups` maps the five fifths onto the six groups, from CE 2024, the year the spec names and spending.json holds | splitting each group's spending by kind (WP10 applies the item judgments to it) |
| `nipa.facts` | named 2025 values, each traced to a table line | captions and ribbon totals: PCE, exports and imports, the gap, investment, taxes, benefits, public pay, interest, net lending, income abroad |
| `nipa.tables` | NIPA Tables 1.1.5, 2.1, 3.1, 3.2 (selected lines), 3.10.5 (selected), 4.1, 6.16D and the government lines of 6.2D, 6.4D and 6.5D (6.5D: general government and the enterprises per level, from 1998), annual from 1947 (or each line's first year) to 2025 | the year chip: NIPA flows for 1950-2025 |
| `profitsByTier` | 6.16D corporate profits by industry in 2025, mapped to raw / make / services / tech, with the crosswalk and its impurities | the comparison strip's PROFITS column |
| `listedValue` | sector-weight slot (not read yet), GICS-to-tier crosswalk, the Wilshire 5000 total, and the spec's fallback from the 25 stored firms | the MARKET VALUE column |
| `publicDebt` | federal debt held by the public at the end of August 2026, split into the Federal Reserve, foreign holders (by country) and other US holders (a residual), plus interest by holder in 2025 | "THE STATE OWES $x T" with its holder segments; "federal interest split by holder" |
| `hyperscalerCapex` | one calendar-2025 value for the five hyperscalers, with its definition, the five firms' figures and the alternative definitions | the frontier's panel footnote |
| `vintageComparison` | circuit.json's 2025 figures against this file's, each with its circuit.json field and NIPA line: personal-income items, PCE, saving, GDP, profits, production and import taxes, corporate income taxes, class gross income | reconciling the two vintages (see below) |

## Series and sources

| Series | Source and URL | Vintage | Stored in | Use |
| --- | --- | --- | --- | --- |
| Household balance sheets by **income** group, by instrument, quarterly 1989:Q3-2026:Q1 | Federal Reserve, Distributional Financial Accounts, `dfa-income-levels-detail.csv`, read from the [bdecon/US-chartbook mirror pinned at commit 2f97c5b](https://raw.githubusercontent.com/bdecon/US-chartbook/2f97c5ba33b0ae07dfaee49af4d2e5479c780cb2/chartbook/data/dfa/dfa-income-levels-detail.csv) | the June 2026 release (data through 2026:Q1), the same release circuit.json used | `classes.dfa.latest`, `.annual` | balance sheet, net-worth shares, household counts |
| The same by **wealth** group | the same mirror and commit, `dfa-networth-levels-detail.csv` | same release | (check only; the fallback) | "groups sum to DFA totals" |
| The Fed's current DFA download | [federalreserve.gov dfa.zip](https://www.federalreserve.gov/releases/z1/dataviz/download/zips/dfa.zip) | files dated 2026-09-15, published 2026-09-18 (data through 2026:Q2) | `classes.dfa.newerRelease`, `.definitions` | the "as of" stamp and a later update; the DFA's definitions |
| Personal income components by decile, plus the top 1%'s share of personal and disposable income, 2000-2024 | BEA, [Distribution of Personal Income, national summary file](https://apps.bea.gov/data/special-topics/distribution-of-personal-income/national/summary-file.csv) ([readme](https://apps.bea.gov/data/special-topics/distribution-of-personal-income/national/readme-for-summary-file.pdf)) | released 2026-06-16; 2024 provisional | `classes.flows.components`, `.byYear.income` | income by group |
| Disposable income, PCE and saving by decile and top 1%, 2000-2023 | BEA and BLS, [joint distribution summary](https://www.bea.gov/sites/default/files/2026-05/joint_dist_summary.xlsx) ([technical document](https://www.bea.gov/sites/default/files/2026-04/technical_document_personal_saving.pdf)) | published 2026-05-20 | `classes.flows.derived.pce`, `.saving`, `.byYear.spending` | spending and saving by group |
| Spending by category, income fifths, 2024 and 2025 | BLS Consumer Expenditure Surveys (Table 1101 series) through the [BLS Public Data API v1](https://api.bls.gov/publicAPI/v1/timeseries/data/), series `CXU{item}LB01{group}M` | annual, 2024 and 2025; `sixGroups` uses 2024 | `classes.spendingMix` | the spending mix by class |
| NIPA Tables 1.1.5, 2.1, 3.1, 3.2, 3.10.5, 4.1, 6.16D, 6.2D, 6.4D, 6.5D | BEA [flat files](https://apps.bea.gov/national/Release/TXT/FlatFiles.zip) (`NipaDataA.txt`, `SeriesRegister.txt`, `TablesRegister.txt`) | files dated 2026-10-28, after the annual update of 30 September 2026 | `nipa.tables`, `nipa.facts` | every NIPA total; scaling the group shares |
| Corporate profits by industry, 2025 | NIPA Table 6.16D (lines 9-32, with inventory valuation adjustment) | as above | `profitsByTier` | PROFITS column |
| Wilshire 5000 total market value ($75,320.3B, 16 Sep 2026) and the 25 stored firms | `circuit.json` `marketCapTotal` and `capture` (their sources are listed in circuit.json) | Sept 2026 | `listedValue.total`, `.fallback` | MARKET VALUE column |
| GICS sector weights | S&P Dow Jones Indices factsheet, **not read** (see below) | none | slot `listedValue.sectorWeights` in sources.json | MARKET VALUE, once read |
| Federal debt held by the public, 31 Aug 2026 | US Treasury Fiscal Data, [Debt to the Penny API](https://api.fiscaldata.treasury.gov/services/api/fiscal_service/v2/accounting/od/debt_to_penny?filter=record_date:eq:2026-08-31) (`debt_held_public_amt`) | daily | `publicDebt.heldByPublic` | the state's debt |
| Treasury securities held by the Federal Reserve, Wednesday 2 Sep 2026 | Federal Reserve [H.4.1 of 3 Sep 2026](https://www.federalreserve.gov/releases/h41/20260903/), Table 1, "U.S. Treasury securities" (quoted) | weekly | `publicDebt.federalReserve` | holder segment |
| Foreign holdings of Treasury securities, Aug 2026 (a 2026 fact, fetched and quoted) | US Treasury, [TIC Table 5](https://ticdata.treasury.gov/resource-center/data-chart-center/tic/Documents/slt_table5.txt) | published 2026-10-16 | `publicDebt.foreign`, `.foreignOfficial`, `.foreignByCountry` | holder segment ("abroad $9.33T") |
| Hyperscaler capital spending, calendar 2025 | `corporate-programs.json` `economy.hyperscalerCapex` (company filings and calls) | filings through 2026 | `hyperscalerCapex` | frontier footnote |
| circuit.json's 2025 accounts and net-worth shares | `circuit.json` `personal`, `income`, `groupNotes.incomeWealthModel` | before the September 2026 annual update | `vintageComparison`, `classes.flows.circuitVintage`; the shares check | cross-checks; the earlier vintage's group flows |
| Fed Z.1 debt of households and nonprofits, 2025 (≈) | `history.json` `series.householdDebtToIncome` (90.5% of DPI in 2025) × `circuit.json` `personal.disposableIncome` | pre-update | `classes.dfa.householdDebt.comparisons` | explaining the spec's $20.6T (never drawn) |

## Modelled (≈) and assumed

1. **Group flows.** Every 2025 flow by group uses BEA's 2023 shares, carried to 2025 and scaled to the NIPA 2025 Table
   2.1 totals.
   - Why 2023: it is the latest year for which BEA publishes income components by decile, a point estimate for the top
     1% (2024 is provisional and gives the top 1% as an interval), and the distribution of consumption and saving.
   - Coverage: BEA distributes the household part of personal income (Table 2.9). The scaling totals here also include
     nonprofit institutions.
   - The components reproduce BEA's published quintile shares of personal income within 0.45 points. The top 1% comes
     out at 15.7% of personal income, against BEA's published 16.0%.
2. **Rankings.**
   - Income components use BEA's ranking by size-adjusted personal income (before taxes). This is the closest to the
     DFA's ranking by household income before taxes.
   - PCE and saving exist only ranked by size-adjusted disposable income. So the spending-side groups are ranked a little
     differently: at the fifths, the two rankings give personal-income shares within 0.1 point of each other (2023), and
     identical shares for 2024.
3. **The top 1%'s split** of each flow. BEA publishes only the top 1%'s share of total income, and CBO could not be
   fetched. The split is therefore built this way:
   - the top 1%'s personal income and disposable income are BEA's published top-1% shares;
   - its owners' income is the top fifth's, divided between 80-99% and the top 1% in proportion to the matching DFA
     holdings of the same income groups:
     - proprietors' income ↔ unincorporated business equity;
     - rental income ↔ home equity;
     - dividends ↔ directly held shares and funds;
     - interest ↔ deposits, bonds, loans held, and pension and insurance reserves;
   - its benefits and business transfers are the top fifth's, per household;
   - its taxes are its personal income less its disposable income;
   - its pay is what remains, with social-insurance contributions at the top fifth's ratio to pay.

   Every step and every key is stored in `classes.flows.top1.steps`.

   **The taxes mix two rankings.** BEA publishes the top 1%'s share of personal income (0.160 in 2023) with households
   ranked by personal income. It publishes the top 1%'s share of disposable income (0.139) only with households ranked by
   disposable income.
   - The taxes subtract the second from the first: $889.5B in 2023, $1,048.1B once scaled to 2025.
   - Ranking by disposable income picks the households with the most disposable income. The subtracted share is
     therefore likely at least that of the income-ranked top 1%, so these taxes are likely **understated**.
   - Size: each 0.1 point of disposable-income share moves them by $20.7B in 2023 (2.3%), or $24.4B in 2025.
   - At the top decile, where both rankings are published, the disposable-income shares differ by 0.1 point (0.357 vs
     0.358; BEA rounds to 0.1 point). That is a guide, not a bound: the gap at the top 1% is not published.
   - All of this is stored in `classes.flows.top1.steps.rankingMix` and `classes.flows.top1.taxesSensitivity`.
4. **The other-outlays residual** (interest and transfers paid) is disposable income less PCE less saving, group by group.
   It is non-negative in every group.
5. **Spending mix of 80-99% and the top 1%.** Both take the CE top fifth's proportions (CE 2024, as in `sixGroups`). The
   CE ranks consumer units in fifths and barely samples the top 1%.
6. **Provisional years.** For 2024 in `byYear`, BEA's top-1% intervals are replaced by their midpoints.
7. **Profits crosswalk** (`assumed: mapping`):

   | Tier | 6.16D lines |
   | --- | --- |
   | raw | utilities + petroleum and coal products |
   | make | manufacturing less computer and electronics and less petroleum and coal; wholesale; retail; transportation and warehousing |
   | services | financial (incl. Federal Reserve banks) + other nonfinancial |
   | tech | computer and electronic products + information |

   BEA does not split "other nonfinancial". It also holds agriculture and mining (raw in the scene), construction (make)
   and computer systems design (tech). Primary metals sit inside "other durable goods". The rest of the world is left
   out.
8. **GICS crosswalk** (`assumed: mapping`), waiting for the weights. The build log prints it under the sector-weights
   check:

   | Tier | GICS sectors |
   | --- | --- |
   | raw | Energy, Materials, Utilities |
   | make | Industrials, Consumer Discretionary, Consumer Staples, Health Care |
   | services | Financials, Real Estate |
   | tech | Information Technology, Communication Services |

   Health Care goes to make because the drug makers dominate it, and circuit.json files Eli Lilly and Johnson & Johnson
   as manufacturing.
9. **Other US holders of federal debt** are a residual: held by the public, less the Fed, less foreign holders. The inputs
   differ in date (31 Aug, 2 Sep, end of August) and in valuation.
10. **The earlier vintage's group flows** (`classes.flows.circuitVintage`) use the same BEA shares on circuit.json's
    totals, with one difference: saving is the residual there.
    - circuit.json's 2025 saving ($1,046.8B) is 16% below the current vintage's ($1,241.9B).
    - BEA's saving shares on that total would leave the bottom fifth and the 20-40% group with negative interest and
      transfers paid (−$101.2B and −$33.7B).
    - So interest and transfers paid are split as in the main block, and saving = disposable income − PCE − those. The
      resulting saving rates stay within about 1.3 points of the main block's in every group (bottom fifth −77.3% vs
      −76.2%).
    - `savingFromBeaShares` keeps BEA's shares on that total for comparison.

## Sources that refused automated requests, and the fallbacks

The builder never gets around bot checks. It uses only the project User-Agent `why-2-instant/0.1 (educational economic
visualization)`, which carries no address.

- **CBO** (household income by source, for the top 1%): www.cbo.gov answers with HTTP 403 and a DataDome CAPTCHA.
  - Fallback: the split in item 3 above.
  - To use CBO instead: read the top 1%'s share of the top fifth for each component into `sources.json` →
    `top1.cbo.shareOfTopFifth`, with the year and a quote. The builder then uses those shares.
- **S&P Dow Jones Indices** (sector weights): www.spglobal.com answers with HTTP 403, "Security Controls Triggered".
  - Fallback, as MODALITIES.md section 5.2 prescribes: the 25 stored firms over the Wilshire total. Tech holds "at
    least 31.7%", and $37.3T (49.5%) is "not split by industry".
  - To finish: read the factsheet's GICS weights into `sources.json` → `listedValue.sectorWeights` (index, as-of date,
    page, weights, quotes). The builder then computes value by tier and checks that the weights sum to 100 ± 0.5.
- **www.bls.gov tables**: "Access Denied" to automated requests.
  - The CE figures come instead from the BLS Public Data API, the sanctioned automated route. No data are lost.
- **The Fed's DFA download** works, but it now serves the September 2026 release.
  - The spec asks for circuit.json's release, so the mirror is pinned to the commit that holds it. The newer release's
    2026:Q2 is stored beside it.
  - If the mirror disappears, the Fed's own file stands in; the order is under "Where the DFA groups come from" above.
    Tried offline with `--simulate-missing`:
    - `dfa-mirror`: income groups from the Fed's 2026:Q2. The net-worth-share check then fails by 0.120 pt on the
      revised 2026:Q1.
    - `dfa-income`: the four wealth groups from the mirror.
    - `dfa-mirror,dfa-zip`: circuit.json's four wealth groups.

## Checks (build of 2026-11-03, sha256 `bb025a14…`)

Two `--offline` runs wrote the same file (sha256 `bb025a14ec3fca63172f48f504fb0dd18e3c9cc66771b8fa86e8f64b1a5e70df`), exit status 0: 18 checks passed, none failed, one skipped (sector weights, below). The online build reads the same cache.

```
CHECK PASS    NIPA 2025 total receipts - total expenditures = net lending within $1B: 8,854.237 - 11,105.044 = -2,250.807 vs Table 3.1 line 43 -2,250.807 (gap $0.000B); current receipts 8,797.0, current expenditures 10,786.2
CHECK PASS    NIPA 2025 identities hold: personal income = its components, current account = receipts - payments, net exports = exports - imports, 6.16D industries = domestic total; largest gap $0.001B
CHECK PASS    general government full-time equivalent jobs 2025 (6.5D lines 88 + 93, the base's jobs): federal 3.439M + state and local 16.363M = 19.802M; with the government enterprises (lines 91 + 96: 0.577M + 1.210M, counted with the market) = 21.589M vs line 86 21.590M within 0.005M (gap 0.001M)
CHECK PASS    DFA instruments add up to each group's assets, liabilities and net worth (2026:Q1): largest gap $0.002B
CHECK PASS    DFA by income: 6 groups (Bottom 20%, 20-40%, 40-60%, 60-80%, 80-99%, Top 1%); 2026:Q1 levels sum to the DFA totals (the wealth-group partition of the same release) within 0.5%: largest gap 0.0027% over 28 columns (all 147 quarters, net worth, assets, liabilities, households: 0.0000%)
CHECK PASS    net-worth shares 2026:Q1 [2.894, 4.175, 7.820, 13.333, 47.305, 24.473] within 0.1 pt of circuit.json groupNotes [2.89, 4.17, 7.82, 13.33, 47.31, 24.47]: largest gap 0.005 pt
CHECK PASS    household liabilities (all DFA instruments) 2026:Q1 $20.00T vs the spec's $20.0T +- 3%: -0.0% (home mortgages + consumer credit $18.89T; the newer release's 2026:Q2: $19.10T; nearest in the repository: Fed Z.1 households and nonprofits 2025, history.json's ratio x circuit.json DPI ≈ $20.69T)
CHECK PASS    flows by group sum to NIPA 2025 totals within 0.5%: 9 income components and personal income, gross income, disposable income, PCE, saving, interest and transfers paid; largest gap 0.0002%
CHECK PASS    interest and transfers paid by group (disposable income less PCE less saving) are not negative: 81.0, 105.5, 188.4, 216.4, 270.4, 12.2 ($B)
CHECK PASS    the same group flows on circuit.json's 2025 totals (the pre-update vintage the spec quotes) sum to them within 0.5%: largest gap 0.0191%; class gross income $28.12T (current vintage $28.31T), PCE $20.95T; saving is the residual [-1,085.8, -766.1, -631.2, -235.3, 1,100.0, 2,665.0] ($B; BEA's saving shares on this total would leave Bottom 20% -101.2; 20-40% -33.7 of interest and transfers paid)
CHECK PASS    CE categories add up to total expenditures in every fifth, 2024-2025: largest gap 0.005%
CHECK PASS    sixGroups uses CE 2024, the year MODALITIES.md names and spending.json byQuintile holds: income before taxes by fifth [16,658, 42,925, 74,474, 121,548, 264,510] equals spending.json byQuintile [16,658, 42,925, 74,474, 121,548, 264,510]; CE 2025 stored beside it
CHECK PASS    6.16D profits 2025 mapped to 4 tiers (assumed: mapping; crosswalk below): raw $67.6B (1.9%) · make $1,123.4B (31.2%) · services $1,814.3B (50.4%) · tech $592.0B (16.5%) = domestic industries $3,597.3B
        raw = utilities (14); petroleum and coal products (25)
        make = manufacturing (15) less computer and electronic products (19) and petroleum and coal products (25); wholesale trade (28); retail trade (29); transportation and warehousing (30)
        services = financial incl. Federal Reserve banks (10); other nonfinancial (32)
        tech = computer and electronic products (19); information (31)
        note: Other nonfinancial (line 32) also holds agriculture and mining (raw in the scene), construction (manufacturing & infrastructure) and computer systems design (tech: the scene's software industry includes NAICS 5415); BEA does not split it.
        note: Other durable goods (inside manufacturing, line 22) holds primary metals, which the scene counts as raw.
        note: The rest of the world (line 33) is not a US industry and is left out of the tiers.
CHECK SKIPPED sector weights sum to 100 within 0.5: not run, the weights are not read (www.spglobal.com answered every automated request on 2026-11-03 with HTTP 403 'Security Controls Triggered'); listed value falls back to the 25 stored firms: tech at least 31.7%, $37.3T (49.5%) not split by industry; this criterion stays open until a person reads the factsheet (GICS crosswalk below, waiting for the weights)
        GICS sector -> tier crosswalk (assumed: mapping):
        raw = Energy, Materials, Utilities
        make = Industrials, Consumer Discretionary, Consumer Staples, Health Care
        services = Financials, Real Estate
        tech = Information Technology, Communication Services
        note: Materials holds chemicals and packaging (manufacturing in the scene) beside metals and mining.
        note: Health Care holds drug makers (manufacturing in the scene, as circuit.json files Eli Lilly and Johnson & Johnson) beside hospitals and insurers (services).
        note: Consumer Discretionary holds hotels, restaurants and education services (services in the scene) beside carmakers and retailers.
        note: Communication Services holds Alphabet, Meta, Netflix and the telecom carriers: the scene's internet and media & telecom industries.
CHECK PASS    Debt to the Penny 2026-08-31: held by the public $32,415.0B + intragovernmental = total within $0.01B (gap $0.0000B)
CHECK PASS    federal debt held by the public 2026-08-31 $32,415.0B: Federal Reserve $4,552.3B (14.0%), foreign $9,331.7B (28.8%, TIC 2026-08), other US holders $18,530.9B (57.2%, residual)
CHECK PASS    one hyperscaler capex value with its definition: $411.5B in calendar 2025 for the five firms, capital expenditures as each firm's cash purchases of property and equipment in its cash-flow statement, finance leases out, calendar 2025 (Oracle, whose fiscal year ends in May: its four quarters from December 2024 to November 2025) (each firm's own headline measure sums to $449.0B; the four with calendar quarters, $376.1B)
CHECK PASS    every value has a source and a url with a quote, an API field or a computation: 126 values, 35 tables and blocks; every one of the file's 20,359 numbers (dates and indices aside) has that provenance on itself or an enclosing block
CHECK PASS    every quote is found verbatim (whitespace-normalised) in the document its url names: 11 verified against repository files and cached downloads
               fallback: the top 1%'s total income and disposable income are BEA's published top-1% shares; its owners' income is the top fifth's, split between 80-99% and the top 1% in proportion to the matching DFA holdings of the same income groups; its benefits and business transfers are the top fifth's per household; its taxes are BEA's top-1% personal income (households ranked by personal income) less its top-1% disposable income (households ranked by disposable income, the only ranking under which BEA publishes it), a mix of two rankings that likely understates the income-ranked top 1%'s taxes (the size per 0.1 pt of disposable-income share is in steps.rankingMix and taxesSensitivity); its pay is what is left of its personal income, with social-insurance contributions at the top fifth's ratio to pay
               fallback: the 25 stored firms over the Wilshire total (MODALITIES.md section 5.2): tech 'at least', the rest 'not split by industry'
checks: 18 passed, 0 failed, 1 skipped
```

## Before using the numbers

- **Two vintages of the 2025 accounts.** The 2025 accounts in circuit.json, and the figures MODALITIES.md quotes, predate
  BEA's annual update of 30 September 2026. This file holds the current flat files. `vintageComparison` lists the
  differences:

  | Figure | circuit.json and MODALITIES.md | This file | Change |
  | --- | --- | --- | --- |
  | PCE | $20,954.9B | $20,870.2B | −0.4% |
  | Personal income | $26,099.9B | $26,327.6B | +0.9% |
  | Class gross income (personal income + contributions) | $28,119.0B | $28,307.7B | +0.7% |
  | Personal taxes | $3,233.8B | $3,341.6B | +3.3% |
  | Contributions | $2,019.1B | $1,980.1B | −1.9% |
  | Government social benefits | $4,845.1B | $4,824.5B | −0.4% |
  | Personal saving | $1,046.8B | $1,241.9B | +18.6% |
  | Taxes on production and imports (Table 3.1 line 4) | $2,129.6B | $2,233.1B | +4.9% |
  | Taxes on corporate income (Table 3.1 line 5) | $695.2B | $623.3B | −10.3% |

  - GDP ($30,861.3B) and corporate profits ($4,025.0B) already match circuit.json's annual-update fields.
  - circuit.json derives its corporate taxes from FRED profits before and after tax, so part of that −10.3% may be
    definition, not revision.
  - **M1 uses one vintage throughout: circuit.json's** (MODALITIES-PLAN decision 30). WP10 reads
    `classes.flows.circuitVintage`, which sums to $28.12T and PCE $20.95T, and takes the state's figures from
    circuit.json, so M1 and the hills agree. The current vintage ($28.31T, PCE $20.87T, personal taxes $3.34T,
    contributions $1.98T, production taxes $2.23T, corporate taxes $0.62T) stays in the main block for the day
    circuit.json is rebuilt. Mixing them would break WP10's "class total = personal income + contributions within 0.5%"
    (0.67% apart).
- **Household debt is every DFA liability: $20.00T** at 2026:Q1 (MODALITIES-PLAN decision 29), so assets less debt is
  net worth in each group. The spec first said "about $20.6T", which no DFA measure matches.
  - DFA home mortgages plus consumer credit: $18.89T (2026:Q1), most of it.
  - The Fed's newer release, 2026:Q2: mortgages plus consumer credit $19.10T.
  - The nearest measure in the repository is wider: the Fed Z.1 debt of households **and nonprofit organizations**,
    all loans and debt securities. history.json gives it as 90.5% of 2025 disposable income; times circuit.json's
    $22,866.2B that is ≈ $20.69T (+0.5%). It is a derived figure, kept in
    `classes.dfa.householdDebt.comparisons` and never drawn.
  - That wider figure is probably where the spec's first $20.6T came from; it counts nonprofits too.
- **Public pay** has two definitions, both stored; M1's base pays the first (MODALITIES-PLAN decision 32):
  - general government (Table 3.10.5 line 4): $2,455.7B;
  - all government including government enterprises (Table 6.2D line 86): $2,679.4B, with 21.59M full-time
    equivalent jobs (6.5D line 86). This is the spec's first "$2.70T, 21.6M jobs". Enterprises' pay and jobs count
    with the market's.
  - general government's jobs: 19.80M full-time equivalents in 2025 (6.5D lines 88 + 93, federal 3.44M and state and
    local 16.36M; `nipa.facts.publicJobsGeneralGovernment`, from 1998 in `nipa.tables.T60500D`), the base's jobs. The
    build checks that they and the enterprises' (lines 91 + 96) add up to line 86.
- **Interest by holder.** NIPA splits all government interest into persons and business together ($1,178.2B) and
  nonresidents ($316.0B). Federal interest is split into residents other than general government ($905.5B) and
  nonresidents ($312.2B). No table separates persons from businesses, so WP10 (InstantModel) takes the state's interest
  to persons to be federal interest to residents other than general government (Table 3.2 line 34, $905.5B in 2025),
  marked as an estimate (≈): the spec's "government interest to persons ≈ $0.9T". It never exceeds the interest the
  class statements receive or all interest to persons and business; the rest of that interest ($272.7B in 2025) goes
  to business, among the state's other outlays.
- **The state's account, 2025 (Table 3.1):**

  | Basis | Receipts | Expenditures | Borrowing |
  | --- | --- | --- | --- |
  | Current | $8,797.0B | $10,786.2B | $1,989.2B (net saving −$1,989.2B) |
  | Total, incl. investment | $8,854.2B | $11,105.0B | $2,250.8B (net borrowing) |

  The spec's "$9.0T in, $10.8T out, $1.9T borrowed" came from circuit.json's fourth-quarter annual rates.
- **Hyperscaler capital spending: $411.5B.** This is each firm's cash purchases of property and equipment in calendar
  2025, finance leases out; for Oracle it is the four quarters from December 2024 to November 2025. Only this definition
  is available for all five firms in the stored filings.
  - $449.0B (THEORY.md) is each firm's own headline measure. Microsoft's ($118.0B) and Meta's ($72.2B) include finance
    leases.
  - The four firms with calendar quarters spent $376.1B.
  - industries.json's AI overlay holds $410B and $443B, with Microsoft at $95B. Their definition is not recorded there.
- **CE 2025 is out.** The spec names CE 2024, and spending.json `byQuintile` is CE 2024 (the build checks that its
  income before taxes per fifth matches). So `sixGroups` uses 2024, and CE 2025 is stored in `byQuintile` beside it.
  To move to 2025, change `ce.sixGroupsYear` in sources.json, spending.json and the spec together.
- **Class splits by year.** `classes.flows.byYear` starts in 2000 for income and stops in 2023 for spending. Before 2000,
  M1 shows one undivided block (spec section 5.9).

# World data: `global.json`

`Assets/Resources/Data/economy/global.json` holds the world economy around the United States for the economy scene's world
view, M2 "One economy among many" (MODALITIES.md section 6, work package WP04). Its readers are WorldModel and WorldLayout
(WP11), the world layers (WP21 row, WP27 lanes, WP28 conflict, WP29 dependence, WP30 history), WorldPanel (WP35) and tour
stops 14-17.

It covers 13 economies (USA, CHN, EU27, JPN, IND, GBR, RUS, CAN, MEX, KOR, TWN, SAU, BRA), Germany (DEU) drawn inside the
EU27 and never added to it, the rest of the world (ROW, the world less the 13) and the world (WLD). 2024 is the base year,
2025 is an overlay, and 2026 facts appear only once the 2026 gate has verified them.

## Building it

```
python3 -I Tools/global/build.py            # fetch what is not cached, run the gate, write global.json
python3 -I Tools/global/build.py --offline  # the same from the cache only: writes the same bytes
python3 -I Tools/global/verify.py [--offline]   # re-run the gate alone and list verdicts that changed (writes nothing)
```

- **Inputs.** Hand-held inputs are in `Tools/global/sources.json`: the economies' codes in each source, the EU27 members,
  hand-read values with their verbatim quotes, the 2026 values to gate, and the dated events with their quotes, values and
  claims. Seeds and cross-checks come from `Tools/global/research` (read only, never edited).
- **Cache.** Downloads are cached under `Tools/global/.cache`, which is gitignored. Each file has a `<name>.meta.json`
  beside it recording the URL, fetch date, HTTP status, size and sha256. A failed fetch records its error, so `--offline`
  replays failures too.
  - Derived text (pdftotext output) is cached next to its PDF.
  - Jev answers are cached by the sha256 of the exact request.
  - The as-of date is recorded in `.cache/asof.txt`. The gate's item specifications are in `.cache/gate-items.json`, for
    `verify.py`.
- **Requests.** The User-Agent is `why-2-global/0.1 (https://github.com/atonalfreerider/why-2)`. A browser-shaped variant
  that still names the project is available for hosts that refuse it. No key is sent except Jev's, which `Tools/jev.py`
  finds and never prints.
- **Federal Register.** HTML pages answer automated requests with an access form, so Federal Register documents are read
  through the FR API's raw text (`api/v1/documents/<number>.json` → `raw_text_url`). Bot-check pages are refused, never
  cached as documents.
- **As-of date.** It is the build date online (at least `readOn` in sources.json) and the recorded date offline. A rebuild
  on a later day re-reads every 2026 source (`refresh_before`) and re-runs the gate.
- **Exit status.** The build exits 1 if any check fails. Unverified gate items do not fail the build: they are stored with
  `drawn: false`.

## Value format

Every stored value is `{value, year, unit, source, url, field | quote, note}`:

- `field` names the API cell, for example `NGDPD.USA.2024` or `imts:CHN.XG_FOB_USD.USA.A 2024`.
- `quote` is the verbatim text read by hand.
- `note: "computed"` (with the arithmetic in `field`) marks a value computed here.
- Series (history, bilateral, the trade matrix) carry one provenance block for the whole array.

## What is in it

| Key | What | Source (year) | Notes |
| --- | --- | --- | --- |
| `economies.<id>.gdp`, `gdpPpp`, `shareWorld`, `sharePpp` | size | IMF WEO Oct 2026, DataMapper `NGDPD`, `PPPGDP`, `PPPSH` (2024) | one source for every economy (WDI's GDP is printed for comparison only); ROW = WLD less the 13 |
| `population`, `sharePeople`, `labourForce` | people | WDI `SP.POP.TOTL`, `SL.TLF.TOTL.IN` (2024); Taiwan: IMF `LP` | Taiwan's labour force is null (fallback) |
| `govSpending`, `govRevenue` | the state, % GDP | IMF Fiscal Monitor `G_X_G01_GDP_PT`, `GGR_G01_GDP_PT` (2024); EU27: Eurostat `gov_10a_main` TE/TR | base depth = spending × 0.4 (WorldLayout) |
| `valueAdded` (+ `strata`) | agriculture, industry, manufacturing, services, % GDP; strata normalised to 100 | WDI `NV.*` (2024; Canada 2022); US: OECD table 1 (B1G/B1GQ); Taiwan: DGBAS production account | strata order: agriculture, industry less manufacturing, manufacturing, services |
| `exportsPct`, `importsPct` | trade in goods and services, % GDP | WDI; EU27: Eurostat `bop_eu6_q` extra-EU over `nama_10_gdp` | Taiwan null (fallback) |
| `energyNetImports` | net energy imports, % of use | WDI `EG.IMP.CONS.ZS` (EU27 2023, Eurostat's 2024 figure beside it); Taiwan: MOEAEA API | negative = net exporter |
| `crudeShare` | % of world crude output | EIA international bulk file, `INTL.57-1-<ISO>-TBPD.A` (2024) | computed |
| `military` | US$ bn, % GDP, % of world | SIPRI v1.2 (2024) | EU27 = sum of its 27 members (checked against WDI's EU aggregate) |
| `currentAccount` | US$ bn, % GDP | WEO `BCA`, `BCA_NGDPD`; EU27: Eurostat extra-EU | |
| `niip` | net IIP, US$ bn, % GDP | IMF IIP (end-2024); EU27: Eurostat `bop_iip6_q` (excluding reserves); Taiwan: CBC | ratio in national currency: the stock at the year-end rate (IMF ER; euro: ECB rates via Eurostat; Taiwan: DGBAS) over WEO's GDP in national currency (`NGDP`, IMF SDMX `IMF.RES,WEO`, the same vintage as `NGDPD`; EU27: Eurostat). No rate on the GDP side, so China is not converted at a rate the WEO does not use: China +17.2% |
| `treasuries` | US Treasuries held, end-Aug 2026 | TIC Table 3 | a 2026 value: gated (`status`, `verifiedOn`, `drawn`); custodial |
| `democracy` | V-Dem v16 electoral democracy index and Regimes of the World, 2025 | V-Dem via Our World in Data | EU27: population-weighted mean (WDI population), plus population shares by regime class |
| `centroid`, `order` | west-to-east order | Natural Earth 10m populated places, `pop_max` | population-weighted centroid on the sphere; ROW closes the row |
| `totals` | the totals line | computed | "$111.9T · US 26% of output, 4% of people, 37% of military · these 13: 82%, 55%" |
| `economies.ROW.economies` | how many economies the rest of the world gathers | computed from WEO `NGDPD` | 156: countries with a 2024 GDP outside the 13 (the EU as its 27 members); the block's label reads "the rest of the world · 156 economies together, not one" |

**`trade`.** Goods trade.

- **Matrix.** A 13×13 goods matrix for 2024, exporter's record (`matrix`), with the importer's record (`mirror`). Each cell
  names its fields (`cells`). `toROW` and `total` are per row.
- **Sources.**
  - IMF IMTS (EU27 = `G998`, extra-EU). Its cells are trade with non-members. Its total is G998's exports to the world
    less its intra-EU exports: 7,085.5 − 4,338.6 = 2,746.9, of which 1,115.5 goes to the rest of the world.
    `euExtraExports` holds the same 2,746.9. `euWorldExports` (7,085.5, intra-EU trade included) is kept for the record
    and is never drawn or used as a row total.
  - US exports and imports: Census country pages.
  - Taiwan: MOF CSV (EU27 = the sum of the 27 members' columns).
  - Russia's total: Bank of Russia balance of payments; its cells are partner-built, so draw them in the ghost style.
- **Flags.** `mirrorGapsFlagged` lists every cell where the two records, as stored (US$ bn to 2 decimals), differ by more
  than 15%: 80 of the 156 cells, 6 of them under $1bn. There is no size floor (`mirrorGapRule`), so a gap between cells
  under $0.1bn is coarse at this precision. The build recomputes the set from `matrix` and `mirror` and fails if the two
  differ.
- **Other blocks.**
  - `germany` (inside the EU, never an arc).
  - `intraEU` (a label on the EU block).
  - `worldGoodsExports`.
  - `lanes` and `defaultSet`: the 12 US pairs plus the 6 largest other pairs, carrying 32.1% of world goods exports outside
    intra-EU trade; all 156 arcs carry 43.6%.
  - `usServices` (BEA Tables 7-8; the total is computed as the countries plus "All other countries").
  - `overlay2025` (US customs imports 2024 → 2025 by partner, with the exporter's 2025 record).
  - `bilateral` (2015-2025 series: Census US pairs, Eurostat EU pairs in EUR, IMTS for the rerouting of Russia's trade).
  - `chipTools` (US exports to China of HS 8486, 2021 and 2025, UN Comtrade preview).

**`tariffs`.**

- `series` is PIIE's US-China chart workbook ('Panel a'), every action with its date and four trade-weighted averages:
  China on the rest of the world, China on the US, US on China, US on the rest of the world. Every 2026 row is gated and
  carries `status` and `drawn`.
- Caveat: statutory rates on fixed 2017 trade, not duties collected.
- `section301_2026` holds the forced-labor Section 301 rate by partner (Federal Register 2026-15274).

**`events`.** 68 dated events: the research's 76, with duplicates merged.

- Each has `id`, `name`, `kind`, `parties`, `start`, `end` / `endText`, `expires`, `form`, `url`, `quote`, `claim`, the
  gate's `status`, `verifiedOn`, `supportedJ`, `reason`, and the flags `ended`, `expiresWithin30Days`, `listed` and `drawn`.
- `evidence` lists further sources an event cites, each gated like the event itself (`role`: `start`, `scope`,
  `expires` or `status`). An event is `verified` only when its own item and every further source pass. Examples:
  - Mexico's 2025 IEEPA duty: Executive Order 14198, which paused it to 4 Mar 2025, is its `start` source.
  - China's suspensions: MOFCOM's announcements are the `scope` (No. 57 of 2025; No. 46 of 2024, clause 2) and
    `expires` (No. 70 to 10 Nov 2026; No. 72 to 27 Nov 2026) sources.
  - The Iran war: EIA's October 2026 outlook is its `status` source.
- `form` says how WP28 may draw it: `wall`, `cut`, `valve`, `war`, `strait`, `tick` (event strip only) or `none`
  (blocs and pledges, listed only).
- Outcomes and expiry:
  - An event whose stated end date has passed is drawn only with a recorded `outcome` that passes the gate itself: the
    verbatim quote, its values in the quote, Jev at 0.6 or more, and, for a 2026 event, the re-read on the as-of date.
    The outcome carries its own `claim`, `status`, `verifiedOn`, `supportedJ` and `recorded`.
  - An event past its `expires` date (the truce and the suspensions) needs the same.
  - Without one it stays in the file with `drawn: false`.
  - An expiry date counts as stated only when a verified quote gives the full date. If none does, the event carries
    `expiresNote` (≈) and the build's assumption line names it. `expiresStated` records which case applies.
- `dataset` maps an event to the research dataset's id; `research` names the research rows it merges.

**`values2026`.** Every gated 2026 value with its verdict:

- OHCHR's Ukraine toll (to 30 Sep 2026);
- Euroclear's sanctioned Russian assets (end-June 2026);
- the 2026 Brent prices (EIA daily and monthly series). The daily table is read cell by cell, so an empty cell shows:
  the 2026 high, $138.21, was on Tuesday 7 April (no Monday price that week). The build checks every daily value against
  its date and the high against all 2026 cells;
- the 2026 Gulf shut-ins (EIA STEO);
- the NATO 2026 edition (unverifiable: image-only tables);
- TIC holdings (end-Aug 2026);
- PIIE's 2026 rows;
- COFER's dollar share (2026-Q2).

**`chokepoints`.** Keystone shares, each with holders, scope, year, source, URL and the verbatim quote it was read from,
checked against the cached document on every build:

- logic below 10 nm (SIA/BCG 2022, read with the bar colours of a render);
- magnet rare-earth refining (IEA annex);
- rare-earth mining, gallium and cobalt mining (USGS MCS 2026 tables);
- lithium refining (IEA);
- LNG exports (GIIGNL);
- data-centre electricity (IEA);
- crude output (EIA, computed);
- AI compute (Epoch, computed by the rule stored with it).

**`straits`.** Three values: Hormuz 2024 flow, Bab el-Mandeb 2023 → Jan-Aug 2024, and Suez tonnage. Each has its
EIA or UNCTAD quote.

**`history`.** The history wall.

- `ppp`: shares of world GDP at PPP for 1950-2024, 14 bands.
  - 1950-1979 from Maddison 2023: GDP per capita × population. The world is the sum of all units, with the USSR,
    Czechoslovakia and Yugoslavia counted as units and their successors left out.
  - 1980-2024 from WEO `PPPSH`. EU27 is the sum of its members; ROW is the sum of every other country.
- `market`: WEO `NGDPD` shares for 1980-2024. There is no market-rate series before 1980.
- `splice`: records the step at 1980. The two sources use different PPP benchmarks, and the step is not smoothed: China
  5.8 → 1.9, EU27 22.3 → 25.1, US 21.6 → 19.8.
- `ussrSeam`: the Russia band is the USSR until 1991.

**`gate`.** The rule, the as-of date, the threshold, the Jev question, the verified and unverified counts, the `pending`
list (WorldPanel's "pending verification") and every item's verdict.

**`checks`, `assumptions`, `fallbacks`, `failedSources`, `sources`.** What the build printed, and the 28 sources with
their retrieval dates.

## The 2026 gate (`Tools/global/verify.py`)

For every value or event dated 2026, and by the same rule every dated event the view can draw, each further source an
event cites and every recorded outcome, the gate runs four steps:

1. **Fetch.** The source is fetched into the cache. A 2026 item's source is re-read when its copy predates the as-of
   date.
2. **Quote.** Each recorded quote must be found verbatim in the fetched text, and each of the item's values must appear
   inside the quotes as a whole token.
   - The only normalisation: Unicode compatibility forms (ligatures, non-breaking spaces), straight quotes, soft hyphens
     dropped, whitespace collapsed.
   - PDFs are read in both pdftotext reading orders.
   - HTML inline tags are removed without a gap.
3. **Jev.** Jev (TypeSafe System One, a Noul) judges whether the text around the quotes supports the item's `claim`. The
   score is stored as `supportedJ`. Data tables send their headings and the quoted row (`tight`).
   - Pass: 0.6 or more.
   - Without the Jev credential, an answer that is not already cached fails closed.
4. **Verdict.** The gate writes `status` (verified | unverified), `verifiedOn` (the fetch date), `quoteFound`,
   `valuesFound`, `supportedJ` and `reason`.

**Pass rule.** An item passes only with the verbatim quote and the score: Jev's probability is model evidence, not proof.
A 2026 item also needs `verifiedOn` on or after the as-of date. The build fails if a drawn 2026 item lacks that.

**Status on 3 Nov 2026.** 116 of 124 gated items verified: 36 of 38 values, 62 of 68 events, all 8 further sources and all
10 recorded outcomes. The 8 unverified:

| Item | Why unverified |
| --- | --- |
| NATO 2026 edition | image-only tables |
| PIIE row of 31 Jul 2026 | Jev 0.51 |
| US-China "30 for 30" | the cited PIIE page lacks the quote |
| India interim deal | the cited April 2025 annex lacks the quote |
| US-Korea and US-Taiwan deals | the cited July 2025 order lacks the quote |
| Mexico's non-FTA tariffs | secondary sources only |
| China-Russia "no limits" | statement not fetched |
| EU single market | an API cell, not a quote |

**Ended or expiring events.**

- Not drawn, because their end date has passed and no outcome was refetched: Airbus-Boeing, the Japanese seafood ban,
  Galwan, the 2020 Saudi-Russia price war.
- Drawn after their end date on a verified outcome (10): the Phase One purchases, the 2025 US-China escalation, Section
  122, the IEEPA duties on Canada, Mexico and Brazil (Executive Order 14389), the Canada-China canola deal, the Greenland
  threat (the President's 21 Jan 2026 post, quoted by CBS News: "I will not be imposing the Tariffs"), the India penalty
  and Japan-Korea.
- Expiring within 30 days, each date stated in full in a verified quote:
  - the US-China truce and the November 2025 deal (10 Nov 2026; their own quotes);
  - China's rare-earth suspension (10 Nov 2026; MOFCOM-GAC announcement No. 70 of 2025);
  - China's gallium and germanium ban suspension (27 Nov 2026; MOFCOM announcement No. 72 of 2025, which suspends
    clause 2 of announcement No. 46 of 2024).
- A rebuild after those dates drops them unless an `outcome` that passes the gate is added to sources.json.

**Changed in the 3 Nov review.**

- The trade matrix's EU27 total is extra-EU (2,746.9, not IMTS's 7,085.5 to the world).
- Mirror gaps are flagged without a size floor.
- China's net IIP ratio divides by WEO's yuan GDP (17.2%, was 17.0%).
- The Brent high is dated 7 Apr 2026.
- Mexico's IEEPA duty starts on 4 Mar 2025 (the end of the pause), like Canada's.
- Outcomes are gated like every other item.
- China's suspension dates come from MOFCOM.
- The Iran war is named for the war alone. The strait's state belongs to `hormuz_disruption` (October 2026: "shipments
  through the Strait of Hormuz increased slightly in September"), so the war event does not draw the strait closed.

**Re-verified since the research.** Several items failed the research's check on 3 Nov 11:13 and now pass. Their claims
were restated to what the quoted passages say, and dated quotes from the same documents were added:

- the Iran war and the Hormuz disruption;
- the East-West pipeline;
- Section 338 on Canada, and the 2025 IEEPA tariffs on Canada (Canada's pages now fetch);
- the EU's EV duties;
- the December 2024 chip package;
- the CHIPS Act;
- the gallium and germanium ban;
- the Greenland threat;
- Learning Resources v. Trump;
- the gas cut-off;
- China's pressure on Taiwan. It is verified but has `form: none`: MODALITIES.md 6.4 says it is not drawn.

The "trade before and after" row is now the measured `trade.bilateral` series.

## Checks the build prints

- One GDP source per economy (WEO).
- Shares at most 100: the 13 + ROW sum to 100.
- EU27 military = the sum of its members, checked against WDI's EU aggregate.
- Net IIP ratios computed in national currency. WEO's `NGDP` must be the same vintage as the DataMapper's `NGDPD`.
- Partners + ROW = row totals.
- EU27 row is extra-EU. Its total equals G998's world exports less the 27 members' own records of their intra-EU
  exports, and matches the research's 2,746.9. No ROW cell is negative, and the summed ROW column matches the research
  within 1%. None of this depends on the residual.
- Mirror gaps over 15% flagged. The set is recomputed from `matrix` and `mirror`, and the check fails if it differs.
- DEU never added to EU27.
- History shares sum to 100 within 0.5 every year. The WEO country shares sum to 100, and Maddison's summed world is
  checked against its own regional totals.
- Hand-read values quoted verbatim.
- Brent daily values on their dates. The daily table is read cell by cell. Each 2026 daily value must sit on its stated
  date, and the stated high must be the largest 2026 price.
- Section 301 partner list quoted.
- Every drawn 2026 item verified on or after the as-of date.

It also prints:

- world GDP and the 13's share;
- the computed west-to-east order against the expected one;
- the default-lane share;
- the 2025 overlay;
- the Epoch shares;
- the size of the 1980 splice;
- the refetched values against the research's seeds;
- the gate report;
- the expiry dates stated in verified quotes, the event outcomes and the expiries within 30 days;
- WEO's own exchange-rate conversion against the IMF's average rates (China 7.122 against 7.197; India's fiscal year).

## Assumptions (≈) and gaps

- ≈ **PPP wall, 1980-1991.** The IMF's world has no USSR before 1992. The USSR's share there is Maddison's (8.5% in
  1980, 6.9% in 1991), and the IMF shares are scaled to make room.
- **Taiwan.** Its labour force and its exports and imports as % of GDP are null; the DGBAS manpower and expenditure
  tables were not read. Neither is drawn.
- **Canada's value added** is 2022, WDI's latest year.
- **Not carried.**
  - Direct-investment positions (BEA), US long-term securities (TIC Table 1), the crude-export value shares (Comtrade),
    wafer capacity by region and COFER's other currencies. None is drawn by the spec.
  - Russia's services trade: BEA shows no Russia column.
- ≈ **Event dates.** Event start and end dates come from the research's event table. The gate verifies each event's
  quoted substance, and a date only where a verified quote states it.
  - Some start dates are not inside a quote, for example the EU EV duties' entry into force and Brazil's IEEPA tariff
    date. The event notes say so where it matters.
  - Every expiry date is stated in full in a verified quote. One that is not would carry `expiresNote` (≈) and be named
    in the assumption line.

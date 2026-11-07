# Collective action: nine real cases for the holistic scene

Research for the holistic collective-action scene (role research:collective, holistic views workflow, 5 Nov 2026). On 4 Nov the
user asked for "holistic view states to show economic mobility and collective action game theory with real world examples" and for
images of individuals and collectives. This file gives nine dated, sourced cases that span the main games, one or two measured
series for each, and the actors with their Wikidata items. The machine-readable twin is [`collective.json`](collective.json): its
`facts` carry every quote, URL and access date, its `series` carry every data point with the row it was read from, and its `cases`
carry the actors, timelines and verdicts. Fact ids are given in brackets below.

**How it was checked** (details in the last sections):

- **Fresh copies.** Every source was downloaded on 4 or 5 Nov 2026 into the role's cache, with its URL, date, size and sha256.
- **Verbatim quotes.** All 116 facts' quotes were found word for word in those copies (whitespace, typographic quotes and soft
  hyphens normalised); quotes joined by " … " are separate passages of one source.
- **Values read by code.** Series values come from the cached tables, CSVs and feeds; every point keeps its source row, and all
  675 rows were found again in the cached text.
- **The 2026 gate.** 16 facts (and the 2025-26 points of four series) are dated 2026. They may be drawn only when Jev, through
  `Tools/jev.py` with the project's gate question, supports them at 0.6 or more: 16 of 16 facts pass (section 11).
- **Jev on every claim.** Besides the gate, each of the 116 claims (this research's paraphrase of its quote) was put to Jev with the
  same question and excerpt rule, to catch paraphrases that say more than the source: all are supported at 0.75 or more.
- **Actors.** Every QID was read from the Wikidata API on 5 Nov 2026; one search hit was rejected because it is a different person.

The repository already holds two of these games as data: the PIIE US-China tariff series and the gated world events in
`Assets/Resources/Data/economy/global.json` (OPEC's 2020 breakdown, the UAE leaving OPEC, the Russia sanctions coalition), and
`games.json` holds the theory (Axelrod's payoffs and tournaments, lab benchmarks). This file adds seven cases and links the two.

## The nine cases at a glance

| # | Case | Game | Cooperating means | Defecting means | What changed the payoffs | Verdict |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | The ozone layer and the Montreal Protocol | public good | stop making and using CFCs and halons and pay for substitutes | keep the cheap chemicals and let everyone else's cuts protect the shared layer | a trade ban on non-parties, payments for developing countries' costs, a ten-year grace period | solved |
| 2 | Northern cod: the Grand Banks collapse of 1992 | tragedy of the commons (lost) | keep total catch at a level the stock can replace | take more now, including the young fish, because what one fleet leaves in the sea another may catch | nothing in time; a moratorium after the collapse | failed; rebuilt after 32 years |
| 3 | Maine lobster: harbor gangs, the V-notch and zone councils | tragedy of the commons (solved) | notch the tail of an egg-bearing female and throw it back; respect gauges, trap limits and territories | keep the egged female or the short lobster and sell it; fish another harbor's ground | closed harbor gangs with territories, the V-notch law, elected zone councils | solved, now under strain |
| 4 | OPEC+: the March 2020 breakdown and the April 2020 cut | cartel | hold output at the agreed quota so the price stays high | produce above quota and sell more barrels at the price the others' restraint holds up | a month of price war, then a deal with equal baselines for the leaders and monitoring | restraint restored after a punishing price war; frayed again by 2025-26 |
| 5 | Silicon Valley Bank, March 2023 | bank run | leave the money in and let the bank ride out its securities losses | wire the money out before others do | a guarantee of all deposits and central-bank lending against securities at par | the run won at SVB; the guarantee stopped the panic spreading |
| 6 | The UAW Stand Up strike, September-October 2023 | strike (war of attrition) | for workers: stay out together and keep the line; for the companies: settle | for a worker: cross the line or vote down the strike; for a company: wait the union out, or split the union by settling separately | targeted strikes widened in steps; each company could stop its losses by settling | won by the union |
| 7 | The US debt ceiling standoffs of 2011 and 2023 | standoff (chicken) | raise or suspend the limit early, on terms both can accept | hold out for concessions as the X-date approaches | nothing structural; a last-minute law each time | someone always swerved, late and at a cost |
| 8 | The global minimum tax, 2021 and after | race to the bottom | tax large multinationals at 15% or more | undercut to attract profits and headquarters | a 15% floor enforced by top-up taxes, so undercutting no longer keeps the revenue | the race halted; the floor is partly built |
| 9 | The US-China tariff war, 2018-2026 | tariff war (iterated prisoner's dilemma) | keep tariffs low; honour purchase and tariff commitments | raise tariffs or controls on the other | truces with suspensions and deadlines; a court ruling removed one tool | ongoing: two truces, no settlement |

The cases pair up: a public good solved by a treaty (ozone) against a commons lost (cod) and one kept (lobster); a cartel that
punished its own defectors (OPEC+); a run, where fear rather than greed drives defection (SVB); a strike and a standoff, both wars of
nerves (UAW, debt ceiling); and two games between states, a race to the bottom halted by a floor (minimum tax) and an iterated
prisoner's dilemma still being played (US-China tariffs).

## 1. The ozone layer and the Montreal Protocol

*Game: public good.* Every country gains from an intact ozone layer whether or not it pays to protect it; a treaty with a trade ban, payments and a grace period made joining the better move, and nearly all of the banned chemicals are gone.

**The game in plain words.** Players: the 198 parties and the chemical industries they regulate. Cooperating: stop making and using CFCs and halons and pay for substitutes. Defecting: keep the cheap chemicals and let everyone else's cuts protect the shared layer. The temptation: the layer is non-excludable: a country that keeps using CFCs still gets the benefit of others' cuts. Why it was hard: the damage was invisible, decades-long (CFCs last 40-150 years) and fell on everyone, while the cost of switching fell on each user. What changed the payoffs: Article 4 banned trade in the chemicals with non-parties, so staying out cost market access; Article 10 and the Multilateral Fund paid developing countries' incremental costs, so joining cost them little; Article 5 gave them ten more years; scientific assessments allowed the rules to be tightened.

**As a game** (n-player prisoner's dilemma (public good); the ordering is the standard theory, not a measurement): For each country: others cut and I do not (temptation) > all cut (reward) > nobody cuts (punishment) > I cut alone (sucker). The trade ban lowers the temptation and the Fund's payments raise the sucker's payoff, until joining is the best reply.

**Actors.**

| Actor | Wikidata | Role | Image on the item |
| --- | --- | --- | --- |
| Montreal Protocol | [Q219438](https://www.wikidata.org/wiki/Q219438) | the treaty: signed 16 September 1987, in force 1 January 1989 | photo |
| Kigali Accord | [Q28499112](https://www.wikidata.org/wiki/Q28499112) | the 2016 amendment that added the HFC phase-down | – |
| United Nations Environment Programme | [Q182016](https://www.wikidata.org/wiki/Q182016) | partner in the 2023 assessment release (WMO and UNEP) | logo |
| Multilateral Fund for the Implementation of the Montreal Protocol | none found | pays developing countries' agreed incremental costs (interim 1991, permanent 1994) | – |
| Mario Molina | [Q19045](https://www.wikidata.org/wiki/Q19045) | warned in 1974 that CFCs destroy stratospheric ozone (with Rowland); Nobel 1995 | photo |
| F. Sherwood Rowland | [Q111190](https://www.wikidata.org/wiki/Q111190) | co-author of the 1974 warning; Nobel 1995 | photo |
| Paul J. Crutzen | [Q135139](https://www.wikidata.org/wiki/Q135139) | Nobel 1995 for the chemistry of ozone formation and decomposition | photo |
| Joe Farman | [Q6209774](https://www.wikidata.org/wiki/Q6209774) | first author of the 1985 report of large Antarctic ozone losses | – |
| British Antarctic Survey | [Q918757](https://www.wikidata.org/wiki/Q918757) | the authors' institution: the 1985 losses were measured by its Antarctic stations | photo |
| United States | [Q30](https://www.wikidata.org/wiki/Q30) | signed 16 September 1987, ratified 21 April 1988 | photo, flag |
| ozone layer | [Q79995](https://www.wikidata.org/wiki/Q79995) | the shared resource | photo |

**Timeline.** (The 'coded as' column is this research's reading of each move, for the scene; it is not in the sources.)

| Date | Move | Coded as | Evidence |
| --- | --- | --- | --- |
| 1974-06-28 | Molina and Rowland publish the warning that CFCs destroy ozone | signal | `ozone-molina-rowland-1974` |
| 1985-05-01 | Farman, Gardiner and Shanklin report large ozone losses over Antarctica | signal | `ozone-farman-1985` |
| 1987-09-16 | Montreal Protocol adopted; the US signs the same day | cooperate | `ozone-protocol-dates`, `ozone-usa-ratified` |
| 1988-04-21 | US ratifies | cooperate | `ozone-usa-ratified` |
| 1989-01-01 | Protocol enters into force | institution | `ozone-protocol-dates` |
| 1990-01-01 | Parties must ban imports of CFCs and halons from non-parties | punish defectors | `ozone-trade-ban` |
| 1990 | London Amendment: total phase-out of CFCs and halons, financial mechanism for developing countries | institution | `ozone-london-1990` |
| 1991-01-01 | Interim Multilateral Fund begins | side payment | `ozone-london-1990` |
| 1994 | Multilateral Fund made permanent | side payment | `ozone-london-1990` |
| 1995-10-11 | Nobel Prize in Chemistry to Crutzen, Molina and Rowland | signal | `ozone-nobel-1995` |
| 2016 | Kigali Amendment adds the HFC phase-down | cooperate | `ozone-kigali`, `ozone-kigali-warming` |
| 2023-01-09 | Assessment: nearly 99% of banned substances phased out; recovery to 1980 values by about 2040-2066 | outcome | `ozone-99pct`, `ozone-recovery-dates` |
| 2023 | Record US$965 million replenishment of the Fund for 2024-2026 | side payment | `ozone-mlf-2023` |
| 2026-11-04 | 198 parties (dated 2026) | outcome | `ozone-parties-198` |

**Series `ozone-cfc-consumption`: Consumption of CFCs (Annex A/I) and halons (A/II), all parties** (ODP tonnes (negative = exports and destruction exceed production plus imports); [Ozone Secretariat, UNEP](https://ozone.unep.org/countries/data-table?report_type=0&party_grouping=total&group_by=group&output_type=odp-CO2e-tonnes&period_start=1986&period_end=2025&baseline=1&group%5B%5D=1&group%5B%5D=2&group%5B%5D=3&group%5B%5D=4&group%5B%5D=5&group%5B%5D=6&group%5B%5D=7&group%5B%5D=8&group%5B%5D=9&form_id=ozone_country_data_form__report_table_form&op=GENERATE+REPORT), read 2026-11-04). Read from the data-centre table (Annex group totals, all parties). 1987-1988 are not in the table. The 2025 values were reported in 2026 (dated 2026 for the gate) and may be revised.

| x | CFCs | halons |
| --- | --- | --- |
| 1986 | 1,084,638.5 | 217,893.8 |
| 1989 | 1,041,673.2 | 186,793.5 |
| 1990 | 732,449.2 | 162,598.5 |
| 1995 | 285,589.8 | 46,065.1 |
| 2000 | 146,951.6 | 25,512.7 |
| 2005 | 44,256.1 | 4,326.9 |
| 2009 | 2,200.0 | 1,932.0 |
| 2010 | -208.9 | -160.8 |
| 2024 | -1,005.5 | -404.7 |
| 2025 (pub. 2026, gate: verified) | -836.1 | -110.2 |

**Series `ozone-hole-area`: Antarctic ozone hole: mean area 7 Sep-13 Oct and mean minimum ozone 21 Sep-16 Oct** (million km2; Dobson units; [NASA Goddard Space Flight Center](https://ozonewatch.gsfc.nasa.gov/meteorology/annual_data.html), read 2026-11-04). No data for 1995. Year-to-year swings are mostly weather (WMO 2023); the trend is the signal.

| x | hole area | min ozone (DU) |
| --- | --- | --- |
| 1979 | 0.1 | 225 |
| 1985 | 14.2 | 146.5 |
| 1987 | 19.3 | 123 |
| 1992 | 22.3 | 114.3 |
| 2000 | 24.8 | 98.7 |
| 2006 | 26.6 | 98.4 |
| 2015 | 25.6 | 117.2 |
| 2019 | 9.3 | 167 |
| 2023 | 23.1 | 115.2 |
| 2024 | 19.6 | 121 |
| 2025 | 18.7 | 138.3 |

**Series `ozone-odgi`: NOAA Ozone-Depleting Gas Index, Antarctic and mid-latitudes (new method)** (index: 100 = peak halogen, 0 = 1980 benchmark; EESC-A in ppt; [NOAA Global Monitoring Laboratory](https://gml.noaa.gov/odgi/), read 2026-11-05). Antarctic peak 100 in 2001-2002; mid-latitude peak 100 in 1997.

| x | ODGI Antarctic | ODGI mid-lat | EESC-A (ppt) |
| --- | --- | --- | --- |
| 1992 | 69.6 | 82 | 3544 |
| 1997 | 94.5 | 100 | 4041 |
| 2001 | 100 | 94.2 | 4151 |
| 2002 | 100 | 91.1 | 4151 |
| 2010 | 90.6 | 70.3 | 3963 |
| 2020 | 76.7 | 51.8 | 3686 |
| 2024 | 71.6 | 45.3 | 3583 |

**Verdict: solved.** Consumption of CFCs fell from about 1.08 million ODP tonnes (1986) to 2,200 (2009) and below zero net from 2010; nearly 99% of the banned substances are phased out; halogens over Antarctica have fallen 28% of the way back to the 1980 level and over mid-latitudes 55%; the layer is projected to recover around 2040-2066. *Why (this research's reading of the sources):* Joining was made individually rational: a trade ban on non-parties raised the cost of defection, the Multilateral Fund paid developing countries' incremental costs, a ten-year grace period eased the start, and periodic science assessments let the parties tighten the rules.
Caveats: The ozone hole's yearly size swings with weather; the projected dates assume current policies hold.

**Numbers and their quotes.**

- [`ozone-molina-rowland-1974`] In June 1974 Molina and Rowland warned in Nature that chlorofluoromethanes, chemically inert and lasting 40-150 years in the atmosphere, would destroy stratospheric ozone through chlorine atoms. — "Published: 28 June 1974 Stratospheric sink for chlorofluoromethanes: chlorine atom-catalysed destruction of ozone … These compounds are chemically inert and may remain in the atmosphere for 40–150 years, and concentrations can be expected to reach 10 to 30 times present levels" — [Nature](https://www.nature.com/articles/249810a0), read 2026-11-05.
- [`ozone-farman-1985`] In May 1985 the British Antarctic Survey team (Farman, Gardiner and Shanklin) reported large losses of total ozone over Antarctica in Nature. — "Published: 01 May 1985 Large losses of total ozone in Antarctica reveal seasonal ClOx/NOx interaction … Authors and Affiliations British Antarctic Survey, Natural Environment Research Council, High Cross, Madingley Road, Cambridge, CB3 0ET, UK J. C. Farman, B. G. Gardiner & J. D. Shanklin" — [Nature](https://www.nature.com/articles/315207a0), read 2026-11-05.
- [`ozone-protocol-signed`] The Montreal Protocol was signed in 1987 and entered into force in 1989. — "The landmark agreement was signed in 1987 and entered into force in 1989." — [Ozone Secretariat, UNEP](https://ozone.unep.org/treaties/montreal-protocol), read 2026-11-04.
- [`ozone-protocol-dates`] The Montreal Protocol was adopted at Montreal on 16 September 1987 and entered into force on 1 January 1989. — "Montreal Protocol on Substances that Deplete the Ozone Layer Montreal, 16 September 1987 Entry into force : 1 January 1989, in accordance with article 16(1)." — [United Nations Treaty Collection](https://treaties.un.org/Pages/ViewDetails.aspx?src=TREATY&mtdsg_no=XXVII-2-a&chapter=27&clang=_en), read 2026-11-04.
- [`ozone-parties-198`] As of 4 November 2026 the Montreal Protocol has 198 parties and 46 signatories. **[2026 gate: verified, Jev 0.98]** — "STATUS AS AT : 04-11-2026 … Status : Signatories : 46. Parties : 198." — [United Nations Treaty Collection](https://treaties.un.org/Pages/ViewDetails.aspx?src=TREATY&mtdsg_no=XXVII-2-a&chapter=27&clang=_en), read 2026-11-04. (A status page dated 2026. Whether 198 is every state is not stated on this page and is not claimed.)
- [`ozone-usa-ratified`] The United States signed the Montreal Protocol on 16 September 1987 and ratified it on 21 April 1988. — "United States of America 16 Sep 1987 21 Apr 1988" — [United Nations Treaty Collection](https://treaties.un.org/Pages/ViewDetails.aspx?src=TREATY&mtdsg_no=XXVII-2-a&chapter=27&clang=_en), read 2026-11-04.
- [`ozone-trade-ban`] From 1 January 1990 every party must ban imports of the Annex A substances (CFCs and halons) from any state that has not joined the Protocol: free riders lose access to the controlled chemicals' trade. — "As of 1 January 1990, each party shall ban the import of the controlled substances in Annex A from any State not party to this Protocol." — [Ozone Secretariat, UNEP](https://ozone.unep.org/treaties/montreal-protocol/articles/article-4-control-trade-non-parties), read 2026-11-05.
- [`ozone-grace-period`] Developing countries consuming less than 0.3 kg of Annex A substances per head may delay compliance with the control measures by ten years. — "Any Party that is a developing country and whose annual calculated level of consumption of the controlled substances in Annex A is less than 0.3 kilograms per capita on the date of the entry into force of the Protocol for it, or any time thereafter until 1 January 1999, shall, in order to meet its basic domestic needs, be entitled to delay for ten years its compliance with the control measures set out in Articles 2A to 2E" — [Ozone Secretariat, UNEP](https://ozone.unep.org/treaties/montreal-protocol/articles/article-5-special-situation-developing-countries), read 2026-11-05.
- [`ozone-incremental-costs`] The Protocol's financial mechanism, which includes a Multilateral Fund, pays all agreed incremental costs of developing-country parties' compliance, on top of other aid. — "The mechanism, contributions to which shall be additional to other financial transfers to Parties operating under that paragraph, shall meet all agreed incremental costs of such Parties in order to enable their compliance with the control measures of the Protocol. … The mechanism established under paragraph 1 shall include a Multilateral Fund." — [Ozone Secretariat, UNEP](https://ozone.unep.org/treaties/montreal-protocol/articles/article-10-financial-mechanism), read 2026-11-05.
- [`ozone-london-1990`] In 1990 (London Amendment) the parties agreed to phase CFCs and halons out completely and adopted a financial mechanism for developing (Article 5) countries; the interim Multilateral Fund began on 1 January 1991 and became permanent in 1994. — "1990 London Amendment advances Montreal Protocol Parties agree to the total phase-out of chlorofluorocarbons (CFCs) and halons in addition to the transfer of ozone-friendly technologies and adopted a financial mechanism for Article 5 countries. 1991 Establishment of the interim Multilateral Fund Interim Multilateral Fund was founded on 1 January to aid developing countries in ozone layer protection efforts. … 1994 Multilateral Fund permanently established The Multilateral Fund gains permanent status, becoming the first financial mechanism born from an international treaty." — [Multilateral Fund Secretariat](https://www.multilateralfund.org/about/history), read 2026-11-05.
- [`ozone-mlf-2023`] In 2023 the parties agreed a record replenishment of the Multilateral Fund of US$965 million for 2024-2026. — "2023 Historic replenishment of the Fund A record replenishment of US $965 million is agreed upon, setting a new benchmark for the 2024-2026 triennium." — [Multilateral Fund Secretariat](https://www.multilateralfund.org/about/history), read 2026-11-05.
- [`ozone-mlf-totals`] The Multilateral Fund reports US$4.3 billion in grants for 10,198 approved projects in 144 developing countries. **[2026 gate: verified, Jev 0.96]** — "$4.3 BN In grant funding 10,198 Approved projects 144 Developing countries assisted" — [Multilateral Fund Secretariat](https://www.multilateralfund.org/), read 2026-11-04. (A running total on the Fund's home page as read on 4 Nov 2026; the page gives no as-of date, so it is treated as dated 2026.)
- [`ozone-kigali`] The Kigali Amendment (2016) added a phase-down of HFCs, the ozone-safe substitutes that are strong greenhouse gases. — "The most recent amendment, the Kigali Amendment, called for the phase-down of hydrofluorocarbons (HFCs) in 2016." — [Ozone Secretariat, UNEP](https://ozone.unep.org/treaties/montreal-protocol), read 2026-11-04.
- [`ozone-99pct`] The 2022 quadrennial assessment (presented 9 January 2023) found that nearly 99% of banned ozone-depleting substances had been phased out. — "confirms the phase out of nearly 99% of banned ozone-depleting substances has succeeded in safeguarding the ozone layer" — [World Meteorological Organization / UNEP](https://wmo.int/news/media-centre/ozone-layer-recovery-track-helping-avoid-global-warming-05degc), read 2026-11-04.
- [`ozone-recovery-dates`] If current policies hold, the ozone layer returns to 1980 values around 2066 over the Antarctic, 2045 over the Arctic and 2040 elsewhere. — "If current policies remain in place, the ozone layer is expected to recover to 1980 values (before the appearance of the ozone hole) by around 2066 over the Antarctic, by 2045 over the Arctic and by 2040 for the rest of the world." — [World Meteorological Organization / UNEP](https://wmo.int/news/media-centre/ozone-layer-recovery-track-helping-avoid-global-warming-05degc), read 2026-11-04.
- [`ozone-kigali-warming`] The Kigali Amendment is estimated to avoid 0.3-0.5 °C of warming by 2100. — "The Scientific Assessment Panel said this amendment is estimated to avoid 0.3–0.5°C of warming by 2100 (this does not include contributions from HFC-23 emissions)." — [World Meteorological Organization / UNEP](https://wmo.int/news/media-centre/ozone-layer-recovery-track-helping-avoid-global-warming-05degc), read 2026-11-04.
- [`ozone-odgi-a-2024`] At the beginning of 2024 NOAA's Antarctic ozone-depleting gas index was 72 (71.6): reactive halogens had decreased 28% from their peak relative to the 1980 benchmark. — "On the ODGI scale, the value of ODGI-A at the beginning of 2024 was 72 (71.6) i.e., by that time reactive halogen concentrations in the Antarctic springtime stratosphere had decreased 28% (i.e., 100-71.6) from their peak relative to the 1980 benchmark halogen value" — [NOAA Global Monitoring Laboratory](https://gml.noaa.gov/odgi/), read 2026-11-05.
- [`ozone-odgi-ml-2024`] NOAA's mid-latitude ozone-depleting gas index was 45 (45.3) in 2024: reactive halogens had decreased 55% from their peak relative to the 1980 benchmark. — "On this scale, the 2024 value of the ODGI-ML was 45, i.e., by that time reactive halogen concentrations in the mid-latitude stratosphere had decreased 55% (i.e. 100-45.3) from their peak relative to the 1980 benchmark value" — [NOAA Global Monitoring Laboratory](https://gml.noaa.gov/odgi/), read 2026-11-05.
- [`ozone-odgi-a-2070`] Antarctic springtime halogens are projected to return to the 1980 level around 2070. — "Reactive halogen concentrations in the Antarctic springtime stratosphere are projected to decline to that 1980 value sometime around 2070" — [NOAA Global Monitoring Laboratory](https://gml.noaa.gov/odgi/), read 2026-11-05.
- [`ozone-nobel-1995`] Crutzen, Molina and Rowland received the 1995 Nobel Prize in Chemistry for their work on the formation and decomposition of ozone. — "11 October 1995 … for their work in atmospheric chemistry, particularly concerning the formation and decomposition of ozone" — [Royal Swedish Academy of Sciences](https://www.nobelprize.org/prizes/chemistry/1995/press-release/), read 2026-11-05.

## 2. Northern cod: the Grand Banks collapse of 1992

*Game: tragedy of the commons (lost).* Canadian inshore and offshore fleets and foreign trawlers all fished one stock; each gained the whole value of its catch and bore only a share of the depletion, and in 1992 the spawning stock was about 10% of its long-term average.

**The game in plain words.** Players: the Canadian inshore fishery, Canadian offshore trawlers, foreign fleets fishing outside the 200-mile limit, and the regulator that set the catch. Cooperating: keep total catch at a level the stock can replace. Defecting: take more now, including the young fish, because what one fleet leaves in the sea another may catch. The temptation: a fleet keeps the full value of each extra tonne while the cost, a smaller stock, is shared by all. Why it was hard: the stock straddled the 200-mile line, harvesting and processing capacity was out of balance with the resource (the 1992 release itself calls for that balance), and stock estimates were uncertain. What changed the payoffs: nothing in time: the 1992 moratorium closed the fishery after the collapse; a 1998-2002 inshore fishery had to be closed again in 2003.

**As a game** (n-player prisoner's dilemma (common-pool resource); the ordering is the standard theory, not a measurement): For each fleet: others restrain and I fish hard > all restrain > all fish hard > I restrain alone. Without enforceable limits, fishing hard is the best reply whatever the others do, and the stock is the shared loss.

**Actors.**

| Actor | Wikidata | Role | Image on the item |
| --- | --- | --- | --- |
| Fisheries and Oceans Canada | [Q349666](https://www.wikidata.org/wiki/Q349666) | federal regulator that set the catch and closed the fishery | photo, logo |
| John Crosbie | [Q1699688](https://www.wikidata.org/wiki/Q1699688) | Minister of Fisheries and Oceans who announced the moratorium on 2 July 1992 | photo |
| Northwest Atlantic Fisheries Organization | [Q580933](https://www.wikidata.org/wiki/Q580933) | international body managing stocks outside Canada's 200-mile limit; its scientific council confirmed the 1992 assessment | – |
| Canada | [Q16](https://www.wikidata.org/wiki/Q16) | coastal state | photo, flag |
| Newfoundland and Labrador | [Q2003](https://www.wikidata.org/wiki/Q2003) | the province whose fishing communities lost the fishery | photo, flag |
| Fish, Food and Allied Workers Union | [Q5454457](https://www.wikidata.org/wiki/Q5454457) | fishers' union; its head chaired the 1992 Task Force on Incomes and Adjustment (stated in the 1992 release) | – |
| Diane Lebouthillier | [Q21336098](https://www.wikidata.org/wiki/Q21336098) | minister who ended the moratorium on 26 June 2024 | photo |
| Joanne Thompson | [Q108687503](https://www.wikidata.org/wiki/Q108687503) | minister who raised the TAC to 59,000 t in June 2026 | photo |
| Gadus morhua | [Q199788](https://www.wikidata.org/wiki/Q199788) | the shared resource (Atlantic cod) | photo |
| Grand Banks of Newfoundland | [Q913793](https://www.wikidata.org/wiki/Q913793) | the fishing grounds | – |
| collapse of the Atlantic northwest cod fishery | [Q4462461](https://www.wikidata.org/wiki/Q4462461) | Wikidata's item for the collapse | – |

**Timeline.** (The 'coded as' column is this research's reading of each move, for the scene; it is not in the sources.)

| Date | Move | Coded as | Evidence |
| --- | --- | --- | --- |
| 1968 | Record landings of 810,014 t, 686,694 t of it by non-Canadian fleets | defect | series `cod-landings` |
| 1977 | Landings 172,720 t, of which 93,159 t by other countries (686,694 t in 1968) | context | series `cod-landings` |
| 1988 | TAC 266 kt; landings 268,677 t | defect | series `cod-landings` |
| 1992-07-02 | Two-year moratorium; spawners about 10% of the long-term average | outcome | `cod-moratorium-1992`, `cod-1992-spawners`, `cod-1992-foreign` |
| 1995 | Model spawning stock biomass at 10 kt (903 kt in 1985) | outcome | series `cod-ssb` |
| 1998 | Limited inshore fishery reopens | defect | `cod-2003-reclosed` |
| 2003 | Fishery closed again | punish defectors | `cod-2003-reclosed` |
| 2005-11-25 | Commons committee: overfishing from foreign and domestic mismanagement caused the collapse | signal | `cod-2005-overfishing` |
| 2024-06-26 | Moratorium ends; Canadian TAC 18,000 t | cooperate | `cod-2024-reopen`, `cod-2024-cautious` |
| 2025-06-18 | TAC raised to 38,000 t | cooperate | `cod-2025-tac` |
| 2025-09 | Assessment: SSB 524 kt, 2.0 times the limit reference point | outcome | `cod-2025-ssb` |
| 2026-06-12 | Healthy zone for the first time since 1992; TAC 59,000 t (dated 2026) | outcome | `cod-2026-healthy` |

**Series `cod-landings`: Northern cod (NAFO 2J3KL) reported landings, Canada and other countries, and TAC** (tonnes; TAC in thousand tonnes; [Fisheries and Oceans Canada, Canadian Science Advisory Se…](https://publications.gc.ca/collections/collection_2018/mpo-dfo/fs70-5/Fs70-5-2018-018-eng.pdf), read 2026-11-05). Table 3 of DFO Res. Doc. 2018/018. 'Other' = foreign fleets (1991-92 include Canadian surveillance estimates). The table prints a TAC of 0 from 1992 except the 1998-2002 index fisheries. Some printed cells have typos in the source (1983 '3,7276', 1991 '22,14'), which do not affect the totals used here.

| x | total t | Canada t | other t | TAC kt |
| --- | --- | --- | --- | --- |
| 1959 | 359,572 | 164,007 | 195,565 | – |
| 1965 | 545,035 | 117,901 | 427,134 | – |
| 1968 | 810,014 | 123,320 | 686,694 | – |
| 1975 | 287,508 | 42,482 | 245,026 | 554 |
| 1977 | 172,720 | 79,561 | 93,159 | 160 |
| 1982 | 229,774 | 207,506 | 22,268 | 230 |
| 1988 | 268,677 | 241,870 | 26,807 | 266 |
| 1990 | 219,452 | 193,277 | 26,175 | 199 |
| 1991 | 172,012 | 121,959 | 50,053 | 190 |
| 1992 | 40,956 | 26,073 | 14,883 | 0 |
| 1993 | 11,392 | 8,967 | 2,425 | 0 |
| 1995 | 413 | 413 | 0 | 0 |
| 1999 | 8,526 | 8,525 | 1 | 9 |
| 2003 | 994 | 971 | 23 | 0 |
| 2015 | 4,436 | 4,369 | 67 | 0 |

**Series `cod-ssb`: Northern cod spawning stock biomass (NCAM model, 2018 assessment)** (thousand tonnes; ratio to Blim; [Fisheries and Oceans Canada, Canadian Science Advisory Se…](https://publications.gc.ca/collections/collection_2018/mpo-dfo/fs70-5/Fs70-5-2018-018-eng.pdf), read 2026-11-05). Model estimates with 95% intervals; the 2025 assessment (524 kt, cod-2025-ssb) uses a newer model and reference point and is not on the same scale.

| x | SSB kt | low | high | SSB/Blim |
| --- | --- | --- | --- | --- |
| 1985 | 903 | 728 | 1120 | 1.02 |
| 1990 | 862 | 673 | 1104 | 0.97 |
| 1991 | 735 | 577 | 934 | 0.83 |
| 1992 | 382 | 289 | 505 | 0.43 |
| 1993 | 101 | 71 | 145 | 0.11 |
| 1994 | 31 | 21 | 44 | 0.04 |
| 1995 | 10 | 8 | 12 | 0.01 |
| 2005 | 25 | 21 | 30 | 0.03 |
| 2010 | 97 | 79 | 119 | 0.11 |
| 2015 | 299 | 246 | 362 | 0.34 |

**Verdict: failed; rebuilt after 32 years.** Landings fell from 810,014 t (1968) to 413 t (1995) and the modelled spawning stock from 903 kt (1985) to 10 kt (1995). The fishery stayed closed, apart from 1998-2002, until 2024. *Why (this research's reading of the sources):* Catch limits stayed above what the stock could bear and fleets inside and outside the 200-mile zone kept fishing; the 2005 parliamentary committee called it overfishing caused by foreign and domestic mismanagement. The 1992 government release had blamed mainly ecological factors.
Caveats: Model estimates (SSB) differ between assessment vintages; the 2018 and 2025 models are not on one scale. Cold water in 1991 and seals are named in the sources as contributing factors.

**Numbers and their quotes.**

- [`cod-moratorium-1992`] On 2 July 1992 Fisheries Minister John Crosbie closed the northern cod fishery with a two-year moratorium. — "FOR IMMEDIATE RELEASE July 2, 1992 Crosbie Announces First steps in Northern Cod Recovery Plan A two-year moratorium on the northern cod fishery was announced on today by John C. Crosbie, Minister of Fisheries and Oceans and Minister for the Atlantic Canada Opportunities Agency." — [Fisheries and Oceans Canada (transcribed by the Centre fo…](https://www.cdli.ca/cod/announce.htm), read 2026-11-05.
- [`cod-1992-assistance`] Fishermen and plant workers were offered emergency payments of $225 a week for 10 weeks. — "emergency assistance payments of $225 a week will be made for the next 10 weeks to fishermen and plant workers affected by the moratorium" — [Fisheries and Oceans Canada (transcribed by the Centre fo…](https://www.cdli.ca/cod/announce.htm), read 2026-11-05.
- [`cod-1992-capacity`] The 1992 release called for a fishery whose harvesting and processing capacity is in balance with what the stock can yield. — "take the decisions necessary to create a sustainable northern cod fishery built on a full-time professional work force, as well as harvesting and processing capacity that is in balance with the potential of the resource" — [Fisheries and Oceans Canada (transcribed by the Centre fo…](https://www.cdli.ca/cod/announce.htm), read 2026-11-05.
- [`cod-1992-spawners`] In 1992 the spawning cod (age 7+) were estimated at 50,000-110,000 tonnes, about 10% of the long-term average. — "The critical estimate of cod seven years old and older, an indicator of spawning potential, is between 50,000 and 110,000 tonnes, among the lowest ever observed and about 10 percent of the long-term average." — [Fisheries and Oceans Canada (transcribed by the Centre fo…](https://www.cdli.ca/cod/announce.htm), read 2026-11-05.
- [`cod-1992-official-cause`] The July 1992 federal release attributed the decline since 1990 primarily to ecological factors: it had halved the biomass and cut the spawning biomass by three-quarters. — "Since 1990, a devastating decline in the stock of northern cod off the east coast of Newfoundland and Labrador, due primarily to ecological factors, has reduced the biomass by half and the spawning biomass by three-quarters." — [Fisheries and Oceans Canada (transcribed by the Centre fo…](https://www.cdli.ca/cod/announce.htm), read 2026-11-05. *Disputed:* The government's 1992 attribution to ecology is disputed by the 2005 committee report (cod-2005-overfishing).
- [`cod-1992-foreign`] Of 1992's 50,000-tonne reference catch, offshore vessels had already taken 15,000 t, foreign vessels outside the 200-mile limit 10,000 t, and 10,000 t was set aside for by-catch, leaving 15,000 t for the inshore. — "Already in 1992, 15,000 tonnes have been taken by offshore vessels, approximately 10,000 tonnes have been set aside for by-catches, and another 10,000 have been taken by foreign vessels outside the 200-mile limit, leaving only 15,000 of the 50,000 tonnes for harvest." — [Fisheries and Oceans Canada (transcribed by the Centre fo…](https://www.cdli.ca/cod/announce.htm), read 2026-11-05.
- [`cod-2005-overfishing`] The Commons fisheries committee (2005, unanimous) concluded that overfishing, the result of foreign and domestic mismanagement, caused the collapse. — "That we conclude that overfishing was the cause of the collapse of the northern cod stock should not surprise anyone. However, in our view, it is clear that overfishing is a consequence of mismanagement, both foreign and domestic" — [House of Commons of Canada, Standing Committee on Fisheri…](https://www.ourcommons.ca/DocumentViewer/en/38-1/FOPO/news-release/2142311), read 2026-11-05.
- [`cod-2003-reclosed`] A small inshore fishery reopened in 1998-2002 and was closed again in 2003 because the harvest hit the stock harder than expected. — "There was a limited index commercial fishery restricted to the inshore between 1998 and 2002, but the fishery was closed again in 2003 following the realization that the harvest had had a more severe and rapid impact on the resource than expected." — [House of Commons of Canada, Standing Committee on Fisheri…](https://www.ourcommons.ca/DocumentViewer/en/38-1/FOPO/news-release/2142311), read 2026-11-05.
- [`cod-2024-reopen`] On 26 June 2024 Minister Diane Lebouthillier ended the moratorium, reopening a commercial northern cod fishery with a Canadian TAC of 18,000 t. — "June 26, 2024 … Today, the Honourable Diane Lebouthillier, Minister of Fisheries, Oceans, and the Canadian Coast Guard announced the end of the Northern cod moratorium off the north and east coasts of Newfoundland and Labrador. This historic decision re-establishes a commercial Northern cod fishery in NAFO Divisions 2J3KL with a Canadian Total Allowable Catch (TAC) of 18,000 tonnes for the 2024 season." — [Fisheries and Oceans Canada](https://www.canada.ca/en/fisheries-oceans/news/2024/06/the-government-of-canada-announces-the-historic-return-of-the-commercial-northern-cod-fishery-in-newfoundland-and-labrador.html), read 2026-11-05.
- [`cod-2024-cautious`] The stock had been in the Cautious zone since 2016; NAFO's other parties keep 5% of the overall TAC. — "The 2J3KL Northern cod stock has been in the Cautious zone since 2016. … The Canadian TAC of 18,000t is considered 95 per cent of the overall TAC." — [Fisheries and Oceans Canada](https://www.canada.ca/en/fisheries-oceans/news/2024/06/the-government-of-canada-announces-the-historic-return-of-the-commercial-northern-cod-fishery-in-newfoundland-and-labrador.html), read 2026-11-05.
- [`cod-2025-tac`] In June 2025 the TAC rose from 18,000 t to 38,000 t. — "allowing for a responsible total allowable catch (TAC) increase from 18,000 tonnes (t) to 38,000 t." — [Fisheries and Oceans Canada](https://www.canada.ca/en/fisheries-oceans/news/2025/06/contributing-to-a-stronger-economy-for-newfoundland-and-labrador-canada-announces-a-sustainable-increase-in-northern-cod-tac.html), read 2026-11-05.
- [`cod-2025-ssb`] DFO's 2025 assessment put spawning stock biomass at 524 kt (95% CI 404-678), 2.0 times the limit reference point, with recruitment about 90% of pre-collapse levels and fishing mortality 0.020 in 2024. — "The 2025 Spawning Stock Biomass (SSB) is 2.0 (95% Confidence Interval [CI] = 1.2–3.3) times the Limit Reference Point (LRP). … SSB in 2025 is estimated at 524 Kt (95% CI = 404–678 Kt). … corresponding to about 90% of pre-collapse (1954–90) levels. … Fishing mortality (F, ages 5+) has been below 0.05 since 2004 and in 2024 was 0.020 (95% CI = 0.016–0.026)." — [Fisheries and Oceans Canada, Canadian Science Advisory Se…](https://publications.gc.ca/collections/collection_2025/mpo-dfo/fs70-6/Fs70-6-2025-043-eng.pdf), read 2026-11-05. (The 2025 model and its LRP (40% of BMSY) differ from the 2018 model's Blim, so SSB/LRP 2.0 is not on the same scale as the 2018 table's SSB/Blim.)
- [`cod-2026-healthy`] On 12 June 2026 DFO said the stock was in the healthy zone for the first time since the 1992 moratorium and raised the TAC from 38,000 t to 59,000 t. **[2026 gate: verified, Jev 0.97]** — "June 12, 2026 … For the first time since the 1992 moratorium, the latest Northern Cod science indicates the stock is in the healthy zone. … Northern cod: TAC increase from 38,000 tonnes to 59,000 tonnes." — [Fisheries and Oceans Canada](https://www.canada.ca/en/fisheries-oceans/news/2026/06/northern-cod-quota-to-increase-55-per-cent-minister-thompson-announces-2026-management-decisions-for-northern-cod-northern-gulf-cod-and-northeast-a.html), read 2026-11-05.

## 3. Maine lobster: harbor gangs, the V-notch and zone councils

*Game: tragedy of the commons (solved).* Each lobsterman gains by keeping an egg-bearing female that would breed for everyone; territorial harbor gangs, a voluntary V-notch custom backed by law, and elected zone councils made restraint pay; with warmer water helping, landings rose from 5.5 million pounds in 1924 to 132.6 million in 2016.

**The game in plain words.** Players: between about 5,000 and 7,500 license holders since 1988, fishing from harbors along the Maine coast; the state and the interstate commission. Cooperating: notch the tail of an egg-bearing female and throw it back; respect gauges, trap limits and territories. Defecting: keep the egged female or the short lobster and sell it; fish another harbor's ground. The temptation: the egged female is worth money today, while its eggs benefit every fisherman on the coast. Why it was hard: no one can see what happens on another boat; in the 1920s and 1930s laws were widely broken and 40% of fishermen went broke between 1928 and 1930. What changed the payoffs: small, closed harbor gangs that know each other and defend territories (boundaries and sanctions); the V-notch law that makes a notched lobster untouchable forever; a belief that conservation works, a long horizon and reputation; from 1995 zone councils elected by license holders set trap limits and entry rules.

**As a game** (prisoner's dilemma repeated in small groups; the ordering is the standard theory, not a measurement): The same ordering as the cod, but the players meet daily for decades in closed harbor groups: the shadow of the future (games.json: w at least 1/2 for tit for tat) and reputation make restraint the better long-run strategy, which is Acheson and Gardner's explanation.

**Actors.**

| Actor | Wikidata | Role | Image on the item |
| --- | --- | --- | --- |
| Maine Department of Marine Resources | [Q43895377](https://www.wikidata.org/wiki/Q43895377) | state regulator; publishes the landings series | – |
| Atlantic States Marine Fisheries Commission | [Q4816546](https://www.wikidata.org/wiki/Q4816546) | interstate commission that assesses the stock and sets coastwide rules | logo |
| Elinor Ostrom | [Q153761](https://www.wikidata.org/wiki/Q153761) | economist of the commons; Nobel 2009; design principles | photo |
| James M. Acheson | [Q113222971](https://www.wikidata.org/wiki/Q113222971) | anthropologist of the Maine lobster industry; modelled the V-notch as a prisoner's dilemma (with Gardner) | – |
| Roy Gardner (Indiana University economist) | none found | co-author of the V-notch model | – |
| Maine | [Q724](https://www.wikidata.org/wiki/Q724) | the state whose legislature passed the V-notch (1917, 1948) and zone (1995) laws | flag |
| American lobster | [Q37668](https://www.wikidata.org/wiki/Q37668) | the shared resource (American lobster) | photo |
| harbor gangs | none found | the informal groups that admit fishermen and defend each harbor's territory | – |

**Timeline.** (The 'coded as' column is this research's reading of each move, for the scene; it is not in the sources.)

| Date | Move | Coded as | Evidence |
| --- | --- | --- | --- |
| 1917 | State begins buying egged lobsters from pounds and marking them; marked lobsters may not be taken | institution | `lob-1917` |
| 1928-1930 | Bust: 40% of fishermen go out of business; conservation laws widely violated | defect | `lob-bust` |
| 1948 | The mark becomes a V-shaped notch | institution | `lob-1917` |
| 1995 | Zone Management Law: seven zones run by elected councils with their own trap limits | institution | `lob-zones-1995` |
| 2016 | Record landings, 132.6 million pounds | outcome | series `lobster-landings` |
| 2023 | Southern New England's fishery at a record low, 1.7 million pounds | outcome | `lob-sne` |
| 2025-05 | Gauge changes to protect spawners repealed after industry objections | defect | `lob-gauge-repeal` |
| 2025-10 | Assessment: not depleted, but overfishing occurring; abundance below the industry target | outcome | `lob-asmfc-2025` |

**Series `lobster-landings`: Maine lobster landings (and, from 1988, license holders and traps)** (million pounds; US$ (nominal ex-vessel); license holders; million traps; °C at Boothbay Harbor; [Maine Department of Marine Resources](https://www.maine.gov/dmr/sites/maine.gov.dmr/files/inline-files/lobster_table.pdf), read 2026-11-05). 2025 is preliminary (last updated 9 Feb 2026) and so dated 2026 for the gate. Years with no landings figure are skipped. 1981-1995 traps are estimates; 1996-2021 from trap tags sold.

| x | million lb | metric t | value | licenses | traps (m) | water °C | prelim. |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1880 | 14.2351 | – | $300,000 | – | – | – |  |
| 1889 | 24.4512 | 11,091 | $500,000 | – | – | – |  |
| 1919 | 5.7937 | 2,628 | $1,400,000 | – | – | – |  |
| 1924 | 5.5137 | 2,501 | $1,800,000 | – | – | – |  |
| 1934 | 5.377 | 2,439 | $900,000 | – | – | – |  |
| 1950 | 18.3526 | 8,325 | $6,412,311 | 5,152 | 0.43 | 9.9 |  |
| 1970 | 18.1723 | 8,243 | $17,201,501 | 6,316 | 1.18 | 8.9 |  |
| 1988 | 21.7391 | 9,861 | $60,742,438 | 5,627 | 2.053 | 8.5 |  |
| 1990 | 28.0682 | 12,732 | $61,627,973 | 6,617 | 2.13 | 9.2 |  |
| 2000 | 57.2154 | 25,953 | $187,714,817 | 6,873 | 2.776 | 9.9 |  |
| 2010 | 96.2088 | 43,640 | $318,052,441 | 5,913 | 3.031 | 10.4 |  |
| 2016 | 132.605 | 60,149 | $540,671,929 | 6,009 | 2.874 | 10.4 |  |
| 2021 | 110.704 | 50,215 | $742,989,021 | 5,763 | 2.861 | 11.3 |  |
| 2024 | 87.2795 | 39,589 | $536,466,702 | 5,218 | 2.547 | 10.6 |  |
| 2025 (pub. 2026, gate: verified) | 78.8267 | 35,755 | $461,384,405 | 5,060 | 2.631 | 9.3 | yes |

**Verdict: solved, now under strain.** Landings rose from about 5.5 million pounds (1924) to 132.6 million (2016); since then they fell to 87.3 million (2024) and a preliminary 78.8 million (2025), and the 2025 assessment finds overfishing occurring with abundance below the industry's own target. *Why (this research's reading of the sources):* Ostrom's conditions hold: clear boundaries (harbor gangs, zones), users making their own rules (zone councils), monitoring and sanctions among people who meet again and again, rights recognised by the state. Acheson and Gardner add belief, a long horizon and reputation. Warm water and few predators helped the boom (Acheson and Gardner); ASMFC says temperature changes have likely played a role in recruitment, and the southern fishery is at a record low.
Caveats: How much of the boom the rules caused, and how much the ocean did, is not settled (the authors say both). Territorial defence includes illegal gear cutting.

**Numbers and their quotes.**

- [`lob-pd`] Acheson and Gardner (2011) describe V-notching in Maine as a prisoner's dilemma that gives each fisherman an incentive to avoid the practice. — "The genesis of this practice is not easily explained, because V-notching poses a prisoner's dilemma problem that gives fishermen an incentive to avoid the practice." — [Ecology and Society (Resilience Alliance)](https://ecologyandsociety.org/vol16/iss1/art41/main.html), read 2026-11-05.
- [`lob-unobservable`] For most of the 20th century V-notching was voluntary, and many fishermen did it although no one off a boat can see whether its crew notches. — "For most of the 20th century, the practice of V-notching was voluntary. … However, many fishermen did so voluntarily even though it is impossible to be certain who was V-notching because what happens on a boat cannot be monitored by anyone who is not on the boat." — [Ecology and Society (Resilience Alliance)](https://ecologyandsociety.org/vol16/iss1/art41/main.html), read 2026-11-05.
- [`lob-why`] Three things explain why cooperation took hold: belief that V-notching works, a low discount rate (the long-run gain beats the one-time gain from defecting), and reputation. — "An unusual combination of factors explains the V-notch program: (1) a strong belief among those in the industry that the V-notch is effective in conserving the lobster stock; (2) a low discount rate because the long-term gains from V-notching are higher than the one-time gain from defection; (3) a gain in reputation for those who V-notch." — [Ecology and Society (Resilience Alliance)](https://ecologyandsociety.org/vol16/iss1/art41/main.html), read 2026-11-05.
- [`lob-harbor-gangs`] Fishing rights are local: a newcomer must be accepted by a harbor gang and fishes only its territory (about 200 km2 on average), defended sometimes by cutting intruders' gear. — "To catch lobster, a person must gain acceptance by a group of people fishing from one harbor, called a "harbor gang" (Acheson 1988). Once admission is gained, the fisherman can fish only in this group's territory; territories average about 200 km2. They are defended in some cases by the surreptitious destruction of lobster gear (Acheson 1988, 2003)." — [Ecology and Society (Resilience Alliance)](https://ecologyandsociety.org/vol16/iss1/art41/main.html), read 2026-11-05.
- [`lob-1917`] The V-notch program began in 1917, when wardens were allowed to buy egg-bearing lobsters to stop egg-scrubbing; from 1948 the mark was a V-shaped notch. — "The V-notch program began in 1917 when the Maine legislature passed a law allowing the wardens to purchase lobsters that had extruded eggs in pounds to stop the pound owners from scrubbing the eggs off and selling them. … In 1948, the law was changed so that lobsters were marked by a V-shaped notch in the tail rather than a round hole (Laws of Maine 1947)." — [Ecology and Society (Resilience Alliance)](https://ecologyandsociety.org/vol16/iss1/art41/main.html), read 2026-11-05.
- [`lob-bust`] In the bust of the 1920s and 1930s, 40% of lobster fishermen went out of business between 1928 and 1930; conservation laws were widely violated. — "Forty percent of lobster fishermen went out of business between 1928 and 1930 … The low stock sizes were produced, in some part, by the widespread violation of the conservation laws." — [Ecology and Society (Resilience Alliance)](https://ecologyandsociety.org/vol16/iss1/art41/main.html), read 2026-11-05.
- [`lob-zones-1995`] The 1995 Zone Management Law split the coast into seven zones run by councils elected by license holders, each with its own trap limits and entry rules. — "(5) in 1995, the Zone Management Law, which changed many aspects of lobstering. The Zone Management Law divides the coast into seven zones run by zone councils elected by lobster license holders. This law established an apprenticeship program for the state. The zones have different trap limits and limited-entry rules." — [Ecology and Society (Resilience Alliance)](https://ecologyandsociety.org/vol16/iss1/art41/main.html), read 2026-11-05.
- [`lob-boom`] Catches hovered near 7.5 million kg a year from the Second World War to 1989 and exceeded 22.7 million kg in every year after 2000, a boom the authors credit to warm water and few predators working with the conservation rules. — "From World War II to 1989, catches hovered around 7.5 million kg per year (Acheson 2003). From 1989 to the present, catches have been at record-high levels despite decades of intense exploitation. In every year after 2000, catches have exceeded 22.7 million kg. This boom is due to environmental factors, e.g., favorable water temperature and low predation by large finfish, working in tandem with conservation laws" — [Ecology and Society (Resilience Alliance)](https://ecologyandsociety.org/vol16/iss1/art41/main.html), read 2026-11-05.
- [`lob-solved`] Unlike most fisheries, the Maine lobster fishery has repeatedly solved its collective action dilemmas. — "The lobster fishery is different in that it has repeatedly solved its collective action dilemmas (Acheson 2003)." — [Ecology and Society (Resilience Alliance)](https://ecologyandsociety.org/vol16/iss1/art41/main.html), read 2026-11-05.
- [`lob-asmfc-2025`] ASMFC's 2025 assessment: the Gulf of Maine/Georges Bank stock is not depleted but overfishing is occurring; 2021-2023 abundance (202 million) fell below the industry target (229 million) and stays above the limit (143 million). — "Based on these reference points, the GOM/GBK stock is not depleted and overfishing is occurring … The average abundance from 2021-2023 (202 million lobsters) has declined below the fishery/industry target (229 million lobsters) but remains above the abundance limit (143 million lobsters) and abundance threshold (89 million lobsters)." — [Atlantic States Marine Fisheries Commission](https://asmfc.org/wp-content/uploads/2025/11/AmericanLobsterStockAssmtOverview_Oct2025.pdf), read 2026-11-05.
- [`lob-sne`] Southern New England's lobster landings fell to a record low of 1.7 million pounds in 2023, 1% of US landings. — "SNE landings have continuously declined to the lowest on record in 2023 (1.7 million pounds), now accounting for only 1% of US landings." — [Atlantic States Marine Fisheries Commission](https://asmfc.org/wp-content/uploads/2025/11/AmericanLobsterStockAssmtOverview_Oct2025.pdf), read 2026-11-05.
- [`lob-temperature`] ASMFC: lobsters do best at 12-18 °C and are stressed above 20 °C, and temperature changes have likely played a role in the population changes seen in the assessment, such as recruitment. — "indicating an optimal range of 12° – 18° C and a threshold for physiological stress at 20°C for adult lobsters. Temperature changes through time have likely played a role in population dynamics observed during the assessment such as changes in recruitment." — [Atlantic States Marine Fisheries Commission](https://asmfc.org/wp-content/uploads/2025/11/AmericanLobsterStockAssmtOverview_Oct2025.pdf), read 2026-11-05.
- [`lob-gauge-repeal`] Gauge and vent changes triggered in October 2023 to protect spawners were repealed in May 2025 (Addendum XXXII) after industry objections. — "Changes were triggered in October 2023 based on observed declines in recruit abundance indices, but these changes were ultimately repealed with the subsequent approval of Addendum XXXII in May 2025 due to lobster industry concern regarding the potential economic impacts of increasing the minimum gauge size" — [Atlantic States Marine Fisheries Commission](https://asmfc.org/wp-content/uploads/2025/11/AmericanLobsterStockAssmtOverview_Oct2025.pdf), read 2026-11-05.
- [`lob-ostrom-nobel`] Elinor Ostrom shared the 2009 economics prize for her analysis of economic governance, especially the commons. — "12 October 2009 … "for her analysis of economic governance, especially the commons"" — [Royal Swedish Academy of Sciences](https://www.nobelprize.org/prizes/economic-sciences/2009/press-release/), read 2026-11-05.
- [`lob-ostrom-principles`] Ostrom's design principles for long-lived commons include clear user and resource boundaries, recognised rights of users to make their own rules, and nested layers of governance. — "1A. User Boundaries: Clear and locally understood boundaries between legitimate users and nonusers are present. … 7. Minimal Recognition of Rights: The rights of local users to make their own rules are recognized by the government. … 8. Nested Enterprises: When a common-pool resource is closely connected to a larger social-ecological system, governance activities are organized in multiple nested layers." — [The Nobel Foundation](https://www.nobelprize.org/uploads/2018/06/ostrom_lecture.pdf), read 2026-11-05.
- [`lob-2025-prelim`] Maine's 2025 landings figures are preliminary, last updated 9 February 2026. **[2026 gate: verified, Jev 0.98]** — "2025 are preliminary and were last updated 2/9/26." — [Maine Department of Marine Resources](https://www.maine.gov/dmr/fisheries/commercial/landings-program/historical-data), read 2026-11-05. (The landings table PDF itself is footed 'Last Updated 2/6/2026'; the page says 2/9/26.)

## 4. OPEC+: the March 2020 breakdown and the April 2020 cut

*Game: cartel.* Each producer gains by pumping above its quota while the others hold back; when OPEC+ failed to agree on 6 March 2020, Saudi Arabia ramped output toward full capacity, Brent averaged $18.38 in April, and on 12 April the group agreed a 9.7 mb/d cut.

**The game in plain words.** Players: OPEC's members and 10 partner countries (OPEC+), led by Saudi Arabia and Russia. Cooperating: hold output at the agreed quota so the price stays high. Defecting: produce above quota and sell more barrels at the price the others' restraint holds up. The temptation: a member that cheats gains volume while the cost, a lower price, is shared by all. Why it was hard: output is hard to observe directly and demand collapsed with the pandemic; Iran, Libya and Venezuela were exempt because of sanctions or instability. What changed the payoffs: a punishment phase (a month of price war that hurt every producer), then a deal with a common baseline for the two leaders, a monitoring committee using secondary-source production estimates, and a tapering schedule.

**As a game** (repeated prisoner's dilemma among producers (cartel); the ordering is the standard theory, not a measurement): For each member: others hold quota and I cheat > all hold quota > all pump > I hold quota alone. Monthly meetings make it repeated; March-April 2020 was a punishment phase that restored cooperation.

**Actors.**

| Actor | Wikidata | Role | Image on the item |
| --- | --- | --- | --- |
| Organization of the Petroleum Exporting Countries | [Q7795](https://www.wikidata.org/wiki/Q7795) | the cartel's core organisation | photo, logo, flag |
| OPEC+ | [Q121826282](https://www.wikidata.org/wiki/Q121826282) | OPEC plus partner producers (the Declaration of Cooperation) | – |
| Saudi Arabia | [Q851](https://www.wikidata.org/wiki/Q851) | leader; ramped production after the March 2020 breakdown; baseline 11.0 mb/d | photo, flag |
| Russia | [Q159](https://www.wikidata.org/wiki/Q159) | co-leader; same 11.0 mb/d baseline as Saudi Arabia | photo, flag |
| Abdulaziz bin Salman | [Q4665821](https://www.wikidata.org/wiki/Q4665821) | Saudi energy minister; chaired the 12 April 2020 meeting (named in the release) | photo |
| Alexander Novak | [Q4321847](https://www.wikidata.org/wiki/Q4321847) | Russian energy minister; co-chaired the 12 April 2020 meeting (named in the release) | photo |
| United Arab Emirates | [Q878](https://www.wikidata.org/wiki/Q878) | one of the eight voluntary cutters of 2023-26; left OPEC on 1 May 2026 | photo, flag |
| Iraq | [Q796](https://www.wikidata.org/wiki/Q796) | one of the eight voluntary cutters (named in the March 2025 release) | photo, flag |
| Kuwait | [Q817](https://www.wikidata.org/wiki/Q817) | one of the eight | flag |
| Kazakhstan | [Q232](https://www.wikidata.org/wiki/Q232) | one of the eight | flag |
| Algeria | [Q262](https://www.wikidata.org/wiki/Q262) | one of the eight | photo, flag |
| Oman | [Q842](https://www.wikidata.org/wiki/Q842) | one of the eight | photo, flag |
| Energy Information Administration | [Q1133499](https://www.wikidata.org/wiki/Q1133499) | US statistics agency whose data and outlooks document the episode | logo |
| 2020 Russia–Saudi Arabia oil price war | [Q87496123](https://www.wikidata.org/wiki/Q87496123) | Wikidata's item for the price war | – |

**Timeline.** (The 'coded as' column is this research's reading of each move, for the scene; it is not in the sources.)

| Date | Move | Coded as | Evidence |
| --- | --- | --- | --- |
| 2020-03-06 | OPEC+ meeting ends without agreement; the cuts expiring 31 March are not renewed | defect | `opec-2020-03-06`, `opec-market-share` |
| 2020-03-09 | Brent futures fall 24% in a day, below $35 | outcome | `opec-2020-03-06` |
| 2020-Q2 | Saudi Arabia ramps toward full capacity (price war) | punish defectors | `opec-saudi-ramp` |
| 2020-04-12 | Deal: cut 9.7 mb/d from 1 May, tapering to 7.7 and 5.8 mb/d; monitoring committee | cooperate | `opec-2020-04-12`, `opec-monitoring` |
| 2020-04-20 | WTI spot closes at -$36.98 as futures holders cannot find buyers and storage at Cushing is scarce | outcome | `opec-wti-negative`, `opec-negative-why` |
| 2020-05 | OPEC output falls 6.0 mb/d in a month, the largest drop since 1993 | cooperate | `opec-may-2020-cut` |
| 2022-10-05 | Cut of 2 mb/d from November 2022 | cooperate | `opec-2022-cut` |
| 2025-04-01 | Eight members begin returning 2.2 mb/d of voluntary cuts | defect | `opec-2025-unwind` |
| 2026-03-01 | The eight resume unwinding the 1.65 mb/d cut (+206 kb/d in April 2026) (dated 2026) | defect | `opec-2026-unwind` |
| 2026-05-01 | The UAE leaves OPEC (dated 2026) | exit | `opec-uae-exit` |

**Series `opec-brent-monthly`: Brent spot price, monthly average** (US$ per barrel; [U.S. Energy Information Administration](https://www.eia.gov/dnav/pet/hist/LeafHandler.ashx?n=PET&s=RBRTE&f=M), read 2026-11-05). March-April 2020: the price war (6 March) on top of the pandemic demand collapse; the OPEC+ cut began 1 May 2020.

| x | Brent $/b |
| --- | --- |
| 2020-01 | 63.65 |
| 2020-02 | 55.66 |
| 2020-03 | 32.01 |
| 2020-04 | 18.38 |
| 2020-05 | 29.38 |
| 2020-06 | 40.27 |
| 2020-12 | 49.99 |
| 2021-12 | 74.17 |
| 2022-06 | 122.71 |

**Series `opec-wti-daily`: WTI spot price at Cushing, daily, 24 February - 29 May 2020** (US$ per barrel; [U.S. Energy Information Administration](https://www.eia.gov/dnav/pet/hist/RWTCD.htm), read 2026-11-05). Weekday columns of EIA's daily table (blank cells are market holidays: 10 April and 25 May 2020). EIA attributes the -$36.98 of 20 April to low liquidity and scarce storage at Cushing near the May contract's expiry (opec-negative-why).

| x | WTI $/b |
| --- | --- |
| 2020-02-24 | 51.36 |
| 2020-03-06 | 41.14 |
| 2020-03-09 | 31.05 |
| 2020-03-18 | 20.48 |
| 2020-03-30 | 14.1 |
| 2020-04-02 | 25.18 |
| 2020-04-17 | 18.31 |
| 2020-04-20 | -36.98 |
| 2020-04-21 | 8.91 |
| 2020-05-01 | 19.72 |
| 2020-05-29 | 35.57 |

**Verdict: restraint restored after a punishing price war; frayed again by 2025-26.** Brent's monthly average fell from $63.65 (January 2020) to $18.38 (April 2020) and recovered to $49.99 by December 2020 under the cut; OPEC output fell 6.0 mb/d in May 2020. *Why (this research's reading of the sources):* The price war showed every member what defection by all costs (a punishment phase, after which restraint resumed); the April deal gave Saudi Arabia and Russia equal baselines and set up monitoring. Pandemic demand, not only the cartel, drove the price.
Caveats: Which side caused the March breakdown is not stated in the cached primary sources. EIA attributes the negative WTI price of 20 April to low liquidity and scarce storage at Cushing as contracts expired (opec-negative-why), not to the price war alone.

In the repository: global.json events: saudi_russia_price_war (verified 2026-11-03, not drawn: no recorded outcome), uae_leaves_opec (verified, drawn).

**Numbers and their quotes.**

- [`opec-2020-03-06`] The OPEC+ meeting of 6 March 2020 ended without agreement on production; on 9 March Brent futures fell below $35, a 24% one-day drop. — "On March 9, Brent crude oil front-month futures prices fell below $35/b, a 24% daily decline and the second largest daily price decline on record. Prices fell following the March 6 meeting between members of the Organization of the Petroleum Exporting Countries (OPEC) and its partner countries, which ended without an agreement on production levels" — [U.S. Energy Information Administration](https://www.eia.gov/outlooks/steo/archives/mar20.pdf), read 2026-11-05.
- [`opec-market-share`] After 6 March EIA assumed OPEC would target market share instead of a balanced market: a defection from restraint. — "As a result of the outcome of the March 6 OPEC meeting, EIA's forecast assumes that OPEC will target market share instead of a balanced global oil market." — [U.S. Energy Information Administration](https://www.eia.gov/outlooks/steo/archives/mar20.pdf), read 2026-11-05.
- [`opec-saudi-ramp`] Saudi Arabia ramped production to near full capacity in the second quarter of 2020 after the OPEC+ cuts were suspended in March. — "This is a result of Saudi Arabia ramping up its production to near full capacity during the second quarter of 2020, following the suspension of OPEC+ agreed-upon production cuts in March 2020." — [U.S. Energy Information Administration](https://www.eia.gov/outlooks/steo/archives/apr20.pdf), read 2026-11-05. (Already in the repository as global.json event saudi_russia_price_war (verified 2026-11-03).)
- [`opec-2020-04-12`] On 12 April 2020 OPEC+ agreed to cut 9.7 mb/d from 1 May, tapering to 7.7 mb/d (July-December 2020) and 5.8 mb/d (to April 2022), with Saudi Arabia and Russia each measured from 11.0 mb/d. — "Adjust downwards their overall crude oil production by 9.7 mb/d, starting on 1 May 2020, for an initial period of two months that concludes on 30 June 2020. For the subsequent period of 6 months, from 1 July 2020 to 31 December 2020, the total adjustment agreed will be 7.7 mb/d. It will be followed by a 5.8 mb/d adjustment for a period of 16 months, from 1 January 2021 to 30 April 2022. The baseline for the calculation of the adjustments is the oil production of October 2018, except for the Kingdom of Saudi Arabia and The Russian Federation, both with the same baseline level of 11.0 mb/d." — [OPEC](https://www.opec.org/pr-detail/310-12-apr-2020.html), read 2026-11-05.
- [`opec-monitoring`] Compliance is watched: a ministerial committee reviews conformity, measured from secondary-source production estimates. — "Reaffirm and extend the mandate of the Joint Ministerial Monitoring Committee and its membership, to closely review general market conditions, oil production levels and the level of conformity with the DoC and this Statement … Reaffirm that the DoC conformity is to be monitored considering crude oil production, based on the information from secondary sources" — [OPEC](https://www.opec.org/pr-detail/310-12-apr-2020.html), read 2026-11-05.
- [`opec-plus-members`] OPEC+ is OPEC's members plus 10 non-OPEC partner countries; in 2020 they agreed to cut output as inventories rose quickly in the first quarter. — "members of the Organization of the Petroleum Exporting Countries (OPEC) and 10 non-OPEC partner countries, collectively known as OPEC+, agreed to reduce crude oil production in response to rapidly increasing global oil inventories in the first quarter of 2020." — [U.S. Energy Information Administration](https://www.eia.gov/todayinenergy/detail.php?id=45236), read 2026-11-05.
- [`opec-may-2020-cut`] OPEC crude output fell 6.0 million b/d from April to May 2020, the largest monthly decline since 1993; Iran, Libya and Venezuela were exempt. — "The U.S. Energy Information Administration's (EIA) monthly data show that OPEC total crude oil production decreased by 6.0 million b/d from April to May, which was the largest monthly production decline since 1993. … OPEC members Iran, Libya, and Venezuela were exempt from the production cut agreement because of economic sanctions or domestic political instability." — [U.S. Energy Information Administration](https://www.eia.gov/todayinenergy/detail.php?id=45236), read 2026-11-05.
- [`opec-wti-negative`] On Monday 20 April 2020 the Cushing, OK WTI spot price was -$36.98 a barrel. — "Cushing, OK WTI Spot Price FOB (Dollars per Barrel) Week Of Mon Tue Wed Thu Fri … 2020 Apr-20 to Apr-24 -36.98 8.91 13.64 15.06 15.99" — [U.S. Energy Information Administration](https://www.eia.gov/dnav/pet/hist/RWTCD.htm), read 2026-11-05. (EIA daily table row: the week of 20-24 April 2020, Monday first. EIA explains the negative price by low liquidity and scarce storage at Cushing near expiry (opec-negative-why), not by the price war alone.)
- [`opec-negative-why`] EIA: WTI futures went below zero on 20 April 2020 for the first time since trading began in 1983, because holders could not find buyers before expiry and storage at Cushing was scarce and costly. — "On Monday, April 20, 2020, West Texas Intermediate (WTI) crude oil front-month futures traded on the New York Mercantile Exchange (NYMEX) were priced in negative dollars per barrel (b) for the first time since trading began in 1983. … The extreme market events of April 20 and April 21 were driven by several factors, including the inability of traders who had purchased futures to find other market participants to sell futures contracts to. In addition, in this case, the scarcity and high cost of available crude oil storage meant several market participants were heavily incentivized to close their positions before having to physically settle their contracts" — [U.S. Energy Information Administration](https://www.eia.gov/todayinenergy/detail.php?id=43495), read 2026-11-05.
- [`opec-2022-cut`] In October 2022 OPEC+ cut 2 mb/d from November. — "Adjust downward the overall production by 2 mb/d from the August 2022 required production levels, starting November 2022" — [OPEC](https://www.opec.org/pr-detail/73-05-oct-2022.html), read 2026-11-05.
- [`opec-2025-unwind`] Eight OPEC+ members began returning their 2.2 mb/d of voluntary cuts from 1 April 2025. — "they re-affirmed their decision agreed upon on December 5, 2024, to proceed with a gradual and flexible return of the 2.2 mbd voluntary adjustments starting on 1st April, 2025" — [OPEC](https://www.opec.org/pr-detail/518-03-march-2025.html), read 2026-11-05.
- [`opec-2026-unwind`] On 1 March 2026 the eight resumed unwinding the 1.65 mb/d cut of April 2023, adding 206 thousand b/d in April 2026. **[2026 gate: verified, Jev 0.94]** — "the eight participating countries decided to resume the unwinding of the 1.65 million barrels per day of additional voluntary adjustments announced in April 2023 and agreed on a production adjustment of 206 thousand barrels per day. This adjustment will be implemented in April 2026" — [OPEC](https://www.opec.org/pr-detail/593-1-march-2026.html), read 2026-11-05.
- [`opec-uae-exit`] The UAE announced on 28 April 2026 that it was leaving OPEC from 1 May; without it OPEC's share of world crude output in 2025 would have been 31% instead of 35%. **[2026 gate: verified, Jev 0.97]** — "On April 28, 2026, the United Arab Emirates (UAE) announced that it was leaving OPEC, effective on May 1. … Without the UAE's contribution, the group's share of world total crude oil production would have been 31% in 2025." — [U.S. Energy Information Administration](https://www.eia.gov/todayinenergy/detail.php?id=67804), read 2026-11-05. (Already in the repository as global.json event uae_leaves_opec (verified 2026-11-03, Jev 0.98).)

## 5. Silicon Valley Bank, March 2023

*Game: bank run.* If no one withdraws, every depositor is safe; if others run, the first to leave gets paid. With 94% of deposits uninsured, $40 billion left SVB on 9 March 2023 and the bank was closed the next day; the government then protected all of SVB's depositors, insured or not, to stop the run spreading.

**The game in plain words.** Players: SVB's depositors, mostly businesses with balances above the insurance limit (94% of deposits were uninsured), the bank, and the agencies that can guarantee deposits or lend. Cooperating: leave the money in and let the bank ride out its securities losses. Defecting: wire the money out before others do. The temptation: not a temptation to cheat but fear: a depositor who stays while others run can lose everything above the insured limit (a coordination game, Diamond-Dybvig). Why it was hard: deposits were concentrated among a small number of large depositors and most of the money left by wire within the first few days (FDIC, 2026), and the 8 March announcement of a $1.8 billion loss was a public signal. What changed the payoffs: deposit insurance and a lender of last resort make staying safe; on 12 March the systemic risk exception covered all SVB deposits and the Fed's BTFP lent against securities at par.

**As a game** (coordination game (stag hunt; Diamond-Dybvig); the ordering is the standard theory, not a measurement): For a depositor: everyone stays > I run while the others stay (I lose a little) and I run when the others run (I save part) > I stay while the others run (I lose what is uninsured). Two equilibria, all stay or all run; a guarantee removes the bad one.

**Actors.**

| Actor | Wikidata | Role | Image on the item |
| --- | --- | --- | --- |
| Silicon Valley Bank | [Q17112054](https://www.wikidata.org/wiki/Q17112054) | the bank; closed 10 March 2023 | photo, logo |
| SVB Financial Group | [Q111839764](https://www.wikidata.org/wiki/Q111839764) | its holding company, which announced the 8 March restructuring | photo, logo |
| collapse of Silicon Valley Bank | [Q117053334](https://www.wikidata.org/wiki/Q117053334) | Wikidata's item for the collapse | photo |
| California Department of Financial Protection and Innovation | [Q16850544](https://www.wikidata.org/wiki/Q16850544) | California regulator that closed the bank | photo, logo |
| Federal Deposit Insurance Corporation | [Q1345065](https://www.wikidata.org/wiki/Q1345065) | receiver; ran the bridge bank and sold it to First Citizens; estimated a $20 billion cost | logo |
| Federal Reserve System | [Q53536](https://www.wikidata.org/wiki/Q53536) | supervisor of SVB; lender of last resort (discount window, BTFP) | flag |
| United States Department of the Treasury | [Q648666](https://www.wikidata.org/wiki/Q648666) | approved the systemic risk exception (Secretary Yellen) | photo, flag |
| Janet Yellen | [Q263725](https://www.wikidata.org/wiki/Q263725) | Treasury Secretary who approved full protection of depositors | photo |
| Michael Barr | [Q6828448](https://www.wikidata.org/wiki/Q6828448) | Michael S. Barr, who signed the Fed's April 2023 review (the item's description says 'American legal academic') | photo |
| First Citizens BancShares | [Q5452733](https://www.wikidata.org/wiki/Q5452733) | bought the bridge bank's deposits and loans (First-Citizens Bank & Trust) | – |
| Signature Bank | [Q24204571](https://www.wikidata.org/wiki/Q24204571) | failed two days later; covered by the same exception | photo |
| First Republic Bank | [Q5453752](https://www.wikidata.org/wiki/Q5453752) | the third bank that failed after a run in spring 2023 | – |
| Douglas Diamond | [Q185871](https://www.wikidata.org/wiki/Q185871) | Diamond-Dybvig model of runs; Nobel 2022 | photo |
| Philip H. Dybvig | [Q24565948](https://www.wikidata.org/wiki/Q24565948) | Diamond-Dybvig model of runs; Nobel 2022 | photo |
| Ben Bernanke | [Q201795](https://www.wikidata.org/wiki/Q201795) | Nobel 2022 (banks in crises) | photo |

**Timeline.** (The 'coded as' column is this research's reading of each move, for the scene; it is not in the sources.)

| Date | Move | Coded as | Evidence |
| --- | --- | --- | --- |
| 2019-2021 | Assets grow from $71 billion to over $211 billion | context | `svb-growth` |
| 2022-12-31 | About 94% of deposits uninsured | context | `svb-uninsured` |
| 2023-03-08 | SVB sells $21 billion of securities at a $1.8 billion loss and seeks capital | signal | `svb-mar8` |
| 2023-03-09 | Over $40 billion withdrawn; $100 billion more expected | defect | `svb-run` |
| 2023-03-10 | California closes the bank; the FDIC is receiver and moves all deposits to a bridge bank | outcome | `svb-run`, `svb-closed` |
| 2023-03-12 | Systemic risk exception: all depositors protected; BTFP created; losses to be recovered from banks | guarantee | `svb-guarantee`, `svb-assessment`, `svb-btfp` |
| 2023-03-15 | Discount window borrowing jumps from $4.6 billion to $152.9 billion; deposits at small banks fall from $5,097.4 billion to $4,948.1 billion in the week (-$149 billion) while large banks' rise | outcome | series `svb-discount-window` |
| 2023-03-26 | First-Citizens buys the bridge bank; FDIC cost estimate about $20 billion | outcome | `svb-first-citizens`, `svb-dif-cost` |
| 2023-04-28 | Fed review: standards too low, supervision lacked force and urgency | signal | `svb-verdict` |
| 2026-05 | FDIC study: 74% of top depositors ran; insured retail depositors generally did not (dated 2026) | outcome | `svb-who-ran`, `svb-concentration`, `svb-insured-stayed` |

**Series `svb-discount-window`: Federal Reserve primary credit (discount window) outstanding, Wednesdays** (US$ million; [Board of Governors of the Federal Reserve System, via FRE…](https://fred.stlouisfed.org/graph/fredgraph.csv?id=WLCFLPCL), read 2026-11-05). Wednesday levels; 15 March 2023 is the first Wednesday after the run. The BTFP and the loans to the FDIC's bridge banks are separate lines of the H.4.1 and not included.

| x | primary credit $m |
| --- | --- |
| 2023-03-01 | 4,362 |
| 2023-03-08 | 4,581 |
| 2023-03-15 | 152,853 |
| 2023-03-22 | 110,248 |
| 2023-03-29 | 88,157 |
| 2023-04-26 | 73,855 |
| 2023-05-03 | 5,345 |

**Series `svb-deposit-flight`: Deposits at large vs small domestically chartered commercial banks, weekly** (US$ billion, seasonally adjusted; [Board of Governors of the Federal Reserve System, via FRED](https://fred.stlouisfed.org/graph/fredgraph.csv?id=DPSLCBW027SBOG), read 2026-11-05). H.8 weekly estimates as revised to November 2026 (FRED vintage read 2026-11-05); the small-bank column is checked against DPSSCBW027SBOG. Week ending 15 March 2023: small banks -$149 billion, large banks +$54 billion.

| x | large banks $bn | small banks $bn |
| --- | --- | --- |
| 2023-03-01 | 11,208.3 | 5,117.5 |
| 2023-03-08 | 11,115.2 | 5,097.4 |
| 2023-03-15 | 11,168.7 | 4,948.1 |
| 2023-03-22 | 11,101.9 | 4,913.8 |
| 2023-03-29 | 11,063.1 | 4,907.1 |

**Verdict: the run won at SVB; the guarantee stopped the panic spreading.** SVB failed within two days of its announcement; two more banks failed after runs. The guarantee and new Fed lending contained it, at an estimated $20 billion cost to the insurance fund, to be recovered from banks by a special assessment. *Why (this research's reading of the sources):* Uninsured depositors were in a coordination game with no floor; only a promise that staying is safe (insurance, a lender of last resort) removes the reason to run, as Diamond and Dybvig showed. Insured retail depositors, who had that floor, stayed.
Caveats: Covering uninsured deposits ex post raises moral hazard; the FDIC figures of 2026 pass the gate only if Jev supports them.

**Numbers and their quotes.**

- [`svb-growth`] SVB grew from $71 billion to over $211 billion in assets in 2019-2021 without heightened standards. — "While the firm was growing rapidly from $71 billion to over $211 billion in assets from 2019 to 2021, it was not subject to heightened supervisory or regulatory standards." — [Board of Governors of the Federal Reserve System](https://www.federalreserve.gov/publications/files/svb-review-20230428.pdf), read 2026-11-05.
- [`svb-uninsured`] About 94% of SVB's deposits were uninsured at the end of 2022. — "As of year-end 2022, approximately 94 percent of SVBFG's total deposits were uninsured." — [Board of Governors of the Federal Reserve System](https://www.federalreserve.gov/publications/files/svb-review-20230428.pdf), read 2026-11-05.
- [`svb-mar8`] On 8 March 2023 SVB announced it had sold $21 billion of securities at a $1.8 billion after-tax loss and sought $2.25 billion of new capital. — "SVBFG had sold $21 billion in available-for-sale (AFS) securities, was booking a $1.8 billion after-tax loss, was planning to increase term borrowings by $15 billion to $30 billion, and was seeking to raise $2.25 billion in capital." — [Board of Governors of the Federal Reserve System](https://www.federalreserve.gov/publications/files/svb-review-20230428.pdf), read 2026-11-05.
- [`svb-run`] Depositors pulled over $40 billion on 9 March 2023 and $100 billion more was expected the next day; California closed the bank on 10 March. — "the bank faced a run by depositors on March 9. Deposit outflows were over $40 billion on March 9, and management expected $100 billion more the next day. This unprecedented outflow led the California Department of Financial Protection and Innovation (CDFPI) to close the bank on March 10." — [Board of Governors of the Federal Reserve System](https://www.federalreserve.gov/publications/files/svb-review-20230428.pdf), read 2026-11-05.
- [`svb-closed`] On Friday 10 March 2023 California's financial regulator closed Silicon Valley Bank and named the FDIC receiver; the FDIC moved all deposits to a bridge bank it ran. — "On Friday, March 10, 2023, Silicon Valley Bank, Santa Clara, CA was closed by the California Department of Financial Protection & Innovation and the Federal Deposit Insurance Corporation (FDIC) was named Receiver. … To protect depositors, the FDIC transferred all the deposits and substantially all of the assets of Silicon Valley Bank to Silicon Valley Bridge Bank, National Association (N.A.), a full-service bank that was operated by the FDIC." — [Federal Deposit Insurance Corporation](https://www.fdic.gov/resources/resolutions/bank-failures/failed-bank-list/silicon-valley.html), read 2026-11-05.
- [`svb-verdict`] The Fed's own review: standards were too low and supervision lacked force and urgency. — "Regulatory standards for SVB were too low, the supervision of SVB did not work with sufficient force and urgency" — [Board of Governors of the Federal Reserve System](https://www.federalreserve.gov/publications/files/svb-review-20230428.pdf), read 2026-11-05.
- [`svb-guarantee`] On 12 March 2023 Treasury, the Fed and the FDIC invoked a systemic risk exception: all SVB depositors could reach all their money from 13 March; Signature Bank, closed that day, got the same exception. — "Secretary Yellen approved actions enabling the FDIC to complete its resolution of Silicon Valley Bank, Santa Clara, California, in a manner that fully protects all depositors. Depositors will have access to all of their money starting Monday, March 13. No losses associated with the resolution of Silicon Valley Bank will be borne by the taxpayer. We are also announcing a similar systemic risk exception for Signature Bank, New York, New York, which was closed today by its state chartering authority." — [Board of Governors of the Federal Reserve System](https://www.federalreserve.gov/newsevents/pressreleases/monetary20230312b.htm), read 2026-11-05.
- [`svb-assessment`] Losses from covering uninsured depositors were to be recovered by a special assessment on banks. — "Any losses to the Deposit Insurance Fund to support uninsured depositors will be recovered by a special assessment on banks, as required by law." — [Board of Governors of the Federal Reserve System](https://www.federalreserve.gov/newsevents/pressreleases/monetary20230312b.htm), read 2026-11-05.
- [`svb-btfp`] The Fed created the Bank Term Funding Program: loans of up to a year against Treasuries and agency securities valued at par. — "The additional funding will be made available through the creation of a new Bank Term Funding Program (BTFP), offering loans of up to one year in length to banks, savings associations, credit unions, and other eligible depository institutions pledging U.S. Treasuries, agency debt and mortgage-backed securities, and other qualifying assets as collateral. These assets will be valued at par." — [Board of Governors of the Federal Reserve System](https://www.federalreserve.gov/newsevents/pressreleases/monetary20230312a.htm), read 2026-11-05.
- [`svb-first-citizens`] First-Citizens bought about $72 billion of the bridge bank's assets at a $16.5 billion discount (26 March 2023). — "Today's transaction included the purchase of about $72 billion of Silicon Valley Bridge Bank, National Association's assets at a discount of $16.5 billion." — [Federal Deposit Insurance Corporation](https://www.fdic.gov/news/press-releases/2023/pr23023.html), read 2026-11-05.
- [`svb-dif-cost`] The FDIC estimated the failure's cost to the Deposit Insurance Fund at about $20 billion. — "The FDIC estimates the cost of the failure of Silicon Valley Bank to its Deposit Insurance Fund (DIF) to be approximately $20 billion." — [Federal Deposit Insurance Corporation](https://www.fdic.gov/news/press-releases/2023/pr23023.html), read 2026-11-05.
- [`svb-diamond-dybvig`] The 2022 economics prize (Bernanke, Diamond, Dybvig) explained runs as self-fulfilling rumours that deposit insurance and a lender of last resort can prevent. — "If a large number of savers simultaneously run to the bank to withdraw their money, the rumour may become a self-fulfilling prophecy – a bank run occurs and the bank collapses. These dangerous dynamics can be prevented through the government providing deposit insurance and acting as a lender of last resort to banks." — [Royal Swedish Academy of Sciences](https://www.nobelprize.org/prizes/economic-sciences/2022/press-release/), read 2026-11-05.
- [`svb-who-ran`] FDIC staff (May 2026): 74% of SVB's top depositors and 39% of all its depositors ran; SVB lost half its 6 March domestic deposits between 9 and 14 March. **[2026 gate: verified, Jev 0.87]** — "Roughly two-thirds or more of each bank's top depositors ran (74 percent at SVB, 65 percent at SBNY, and 74 percent at FRB). … 39 percent of SVB's total number of depositors ran … Between March 9 and March 14, SVB lost 50 percent of its March 6 domestic deposits." — [Federal Deposit Insurance Corporation](https://www.fdic.gov/center-financial-research/staff-studies-may-2026.pdf), read 2026-11-05.
- [`svb-concentration`] FDIC staff (May 2026): deposits at all three failed banks were concentrated among a small number of large depositors, and most of the money ran by wire in the first few days. **[2026 gate: verified, Jev 0.88]** — "However, all three banks exhibited a concentration of deposits among a small number of large depositors. Consistent with public reports, we find that the runs were unprecedented in their size and speed. Most funds ran via wire transfer in the first few days." — [Federal Deposit Insurance Corporation](https://www.fdic.gov/center-financial-research/staff-studies-may-2026.pdf), read 2026-11-05.
- [`svb-insured-stayed`] FDIC staff (May 2026): fully insured retail depositors generally did not run. **[2026 gate: verified, Jev 0.83]** — "Retail depositors that were fully insured generally did not run and, at certain times during the runs, had net inflows of deposits." — [Federal Deposit Insurance Corporation](https://www.fdic.gov/center-financial-research/staff-studies-may-2026.pdf), read 2026-11-05.

## 6. The UAW Stand Up strike, September-October 2023

*Game: strike (war of attrition).* Each side loses every day the plants are idle and wins by holding out longer; the UAW struck all three Detroit automakers at once but only at chosen plants, widened it six times, from 12,700 to 49,800 workers, and settled by 30 October with 25% base raises.

**The game in plain words.** Players: the United Auto Workers and its members; General Motors, Ford and Stellantis. Cooperating: for workers: stay out together and keep the line; for the companies: settle. Defecting: for a worker: cross the line or vote down the strike; for a company: wait the union out, or split the union by settling separately. The temptation: each worker gains from a contract whether or not they bore the strike (a collective-action problem inside the union), and each company hopes the others settle first. Why it was hard: a long strike costs members their wages and tests their solidarity; the union's public case was that the companies' record profits could pay ('record profits mean record contracts'). What changed the payoffs: targeted, escalating strikes: idle a few plants, add more in steps, and reward a company that moves (Ford put 50% more on the table); record profits as the public argument.

**As a game** (war of attrition (bargaining with costly delay); the ordering is the standard theory, not a measurement): Both sides lose every day of the strike; the side that holds out longer wins the larger share. Escalation in steps raises the companies' daily cost faster than the union's.

**Actors.**

| Actor | Wikidata | Role | Image on the item |
| --- | --- | --- | --- |
| United Auto Workers | [Q1814260](https://www.wikidata.org/wiki/Q1814260) | the union | photo |
| Shawn Fain | [Q117716066](https://www.wikidata.org/wiki/Q117716066) | UAW President during the strike | photo |
| General Motors | [Q81965](https://www.wikidata.org/wiki/Q81965) | struck employer; deal on 30 October | photo, logo |
| Ford Motor Company | [Q44294](https://www.wikidata.org/wiki/Q44294) | struck employer; first deal on 25 October; $1.7 billion strike cost | photo, logo |
| Stellantis | [Q97439162](https://www.wikidata.org/wiki/Q97439162) | struck employer (parent); deal on 28 October | photo, logo |
| Stellantis North America | [Q181114](https://www.wikidata.org/wiki/Q181114) | Stellantis's US arm (the struck plants) | photo, logo |
| Mary Barra | [Q6779013](https://www.wikidata.org/wiki/Q6779013) | GM's chief executive (office from Wikidata; not named in the cached sources) | photo |
| Jim Farley | [Q18921712](https://www.wikidata.org/wiki/Q18921712) | Ford's chief executive (office from Wikidata; not named in the cached sources) | – |
| Carlos Tavares | [Q2939466](https://www.wikidata.org/wiki/Q2939466) | Stellantis's chief executive in 2023 (Wikidata's description says only 'Portuguese businessman'; not named in the cached sources) | photo |
| Bureau of Labor Statistics | [Q2928428](https://www.wikidata.org/wiki/Q2928428) | counts the strike and its idle days | logo |
| 2023 United Auto Workers strike | [Q122599696](https://www.wikidata.org/wiki/Q122599696) | Wikidata's item for the strike | photo |

**Timeline.** (The 'coded as' column is this research's reading of each move, for the scene; it is not in the sources.)

| Date | Move | Coded as | Evidence |
| --- | --- | --- | --- |
| 2023-09-15 | Strike begins with 12,700 workers at three plants | defect | `uaw-escalation`, `uaw-pressure` |
| 2023-09-22 | +5,600 workers | escalate | `uaw-escalation` |
| 2023-09-29 | +7,000 workers | escalate | `uaw-escalation` |
| 2023-10-11 | +8,700 workers | escalate | `uaw-escalation` |
| 2023-10-23 | +6,800 workers | escalate | `uaw-escalation` |
| 2023-10-24 | +5,000 workers | escalate | `uaw-escalation` |
| 2023-10-25 | Ford deal: 25% base raises through April 2028 | cooperate | `uaw-ford-ta` |
| 2023-10-28 | Stellantis deal | cooperate | `uaw-escalation` |
| 2023-10-29 | +4,000 workers | escalate | `uaw-escalation` |
| 2023-10-30 | GM deal; strike suspended | cooperate | `uaw-escalation` |
| 2023-11-20 | Contracts ratified with 64% of votes | outcome | `uaw-ratified` |
| 2023-11-30 | Ford: $1.7 billion of lost profit, about 100,000 vehicles | outcome | `uaw-ford-cost` |

**Series `uaw-stoppages`: US major work stoppages (1,000+ workers): number beginning in the year, workers involved, days idle** (stoppages; thousand workers; thousand days idle; [U.S. Bureau of Labor Statistics](https://web.archive.org/web/20260227115109id_/https://www.bls.gov/web/wkstp/annual-listing.htm), read 2026-11-05). 2025 row published 20 Feb 2026 (dated 2026 for the gate). The 2023 days idle include the UAW, SAG-AFTRA and WGA strikes.

| x | stoppages began | workers (k) | days idle (k) |
| --- | --- | --- | --- |
| 1952 | 470 | 2,746.0 | 48,820.0 |
| 1959 | 245 | 1,381.0 | 60,850.0 |
| 1970 | 381 | 2,468.0 | 52,761.0 |
| 1980 | 187 | 795.0 | 20,844.0 |
| 1990 | 44 | 184.9 | 5,926.1 |
| 2000 | 39 | 393.7 | 20,419.4 |
| 2009 | 5 | 12.5 | 124.1 |
| 2018 | 20 | 485.2 | 2,815.4 |
| 2019 | 25 | 425.5 | 3,244.3 |
| 2022 | 23 | 120.6 | 2,194.5 |
| 2023 | 33 | 458.9 | 16,673.0 |
| 2024 | 31 | 271.5 | 3,364.1 |
| 2025 (pub. 2026, gate: verified) | 30 | 306.8 | 1,991.2 |

**Series `uaw-auto-jobs`: Employment in motor vehicles and parts manufacturing, 2023** (thousand jobs, seasonally adjusted; [U.S. Bureau of Labor Statistics](https://api.bls.gov/publicAPI/v1/timeseries/data/), read 2026-11-05). Current vintage (read 2026-11-05): September 1,028.1, October 999.0, November 1,026.3. The October dip is the strike; the first print (3 Nov 2023) said strike activity lowered manufacturing employment.

| x | jobs (k) |
| --- | --- |
| 2023-08 | 1,022.8 |
| 2023-09 | 1,028.1 |
| 2023-10 | 999.0 |
| 2023-11 | 1,026.3 |
| 2023-12 | 1,027.6 |

**Verdict: won by the union.** 25% base raises over the contract (raises of at least 33% after COLA), the end of wage tiers (the union's account), ratified by 64% of voters; Ford alone lost $1.7 billion of profit. 2023 had 33 major stoppages, the most since 2000. *Why (this research's reading of the sources):* Escalating, targeted strikes kept the union's cost low and the companies' uncertainty high, and kept members' solidarity cheap; each company could end its losses by settling first.
Caveats: The union's description of its gains is the union's; the BLS counts and Ford's cost are independent.

**Numbers and their quotes.**

- [`uaw-escalation`] The UAW's Stand Up strike began with 12,700 workers on 15 September 2023 and escalated in steps to 49,800; it ended on 30 October after deals with Ford (25 Oct), Stellantis (28 Oct) and GM (30 Oct). — "9/15/23 10/30/23 49,800 869,200 Work stoppage began with 12,700 UAW workers on 9/15; UAW added 5,600 workers on 9/22; 7,000 workers on 9/29; 8,700 workers at 6:00 pm E.T. on 10/11; 6,800 workers on 10/23; 5,000 workers on 10/24; and 4,000 workers at 8:00 pm E.T. on 10/29/2023. UAW and Ford announced a tentative agreement on 10/25; a tentative agreement with Stellantis on 10/28; and a tentative agreement with GM on 10/30. On 10/30, UAW announced a suspension of the strike and a return to work for all workers." — [U.S. Bureau of Labor Statistics](https://web.archive.org/web/20240304212659id_/https://www.bls.gov/web/wkstp/monthly-listing.htm), read 2026-11-05.
- [`uaw-ford-ta`] The Ford deal (25 October 2023, after 41 days) gave 25% base raises through April 2028, lifting the top wage over 30% to more than $40 an hour. — "The UAW has announced a tentative agreement with Ford Motor Company after 41 days on strike at the Big Three. … The agreement grants 25% in base wage increases through April 2028, and will cumulatively raise the top wage by over 30% to more than $40 an hour, and raise the starting wage by 68%, to over $28 an hour. … The agreement reinstates major benefits lost during the Great Recession, including Cost-of-Living Allowances and a three-year Wage Progression, as well as killing divisive wage tiers in the union." — [UAW](https://uaw.org/uaw-reaches-tentative-agreement-on-record-contract-with-ford-motor-company/), read 2026-11-05.
- [`uaw-pressure`] The union's case: record profits mean record contracts; Ford put 50% more on the table during the strike. — "For months we've said that record profits mean record contracts. … Since the strike began, Ford put 50% more on the table than when we walked out." — [UAW](https://uaw.org/uaw-reaches-tentative-agreement-on-record-contract-with-ford-motor-company/), read 2026-11-05.
- [`uaw-ratified`] 64% of voting members at the three companies ratified the contracts; raises run from at least 33% to over 160% over the agreement. — "Across the three companies, 64% of voting members voted in favor of the agreements. … Over the life of the agreement, members will see raises of at least 33%, after COLA and compounded wage increases, ranging up to over 160% for some of the lowest-paid workers." — [UAW](https://uaw.org/uaw-members-ratify-historic-contracts-at-ford-gm-and-stellantis/), read 2026-11-05.
- [`uaw-ford-cost`] Ford put the strike's cost at $1.7 billion in lost profit and about 100,000 fewer vehicles wholesaled. — "That would include $1.7 billion in strike-related lost profits – $1.6 billion of that from the fourth quarter – owing to interruptions in production of high-margin trucks and SUVs and, in turn, vehicle wholesales about 100,000 units lower than planned." — [Ford Motor Company](https://www.fromtheroad.ford.com/us/en/articles/2023/ford-provides-guidance-for-full-year-2023-operating-results--cfo), read 2026-11-05.
- [`uaw-bls-2023`] 33 major work stoppages began in 2023, the most since 39 in 2000. — "In 2023, there were 33 major work stoppages beginning in the year, the U.S. Bureau of Labor Statistics reported today. This is the largest number of major work stoppages since there were 39 in 2000." — [U.S. Bureau of Labor Statistics](https://web.archive.org/web/20240721103904id_/https://www.bls.gov/news.release/archives/wkstp_02212024.htm), read 2026-11-05.
- [`uaw-jobs-report`] October 2023 manufacturing employment fell because of the strike. — "Employment in manufacturing declined due to strike activity." — [U.S. Bureau of Labor Statistics](https://web.archive.org/web/20231105024849id_/https://www.bls.gov/news.release/archives/empsit_11032023.htm), read 2026-11-05.
- [`uaw-bls-2025`] 30 major work stoppages began in 2025, involving 306,800 workers. **[2026 gate: verified, Jev 0.99]** — "In 2025, there were 30 major work stoppages beginning in the year, the U.S. Bureau of Labor Statistics reported today. … There were 306,800 workers involved in major work stoppages that began in 2025." — [U.S. Bureau of Labor Statistics](https://web.archive.org/web/20260301012641id_/https://www.bls.gov/news.release/wkstp.nr0.htm), read 2026-11-05.

## 7. The US debt ceiling standoffs of 2011 and 2023

*Game: standoff (chicken).* Each side gains if the other swerves first, and a default would hurt both; in 2011 the deal came on 2 August and S&P downgraded the US three days later, and in 2023 the House passed the deal on 31 May, it became law on 3 June, and Fitch downgraded in August.

**The game in plain words.** Players: the House majority and the President, with the Senate, Treasury and bond investors. Cooperating: raise or suspend the limit early, on terms both can accept. Defecting: hold out for concessions as the X-date approaches. The temptation: the side that holds out longer wins more of what it wants, as long as the other swerves. Why it was hard: the cost of crashing (a default) is so large that each side expects the other to give way, and the X-date can only be estimated, so last-minute deals risk default (GAO). What changed the payoffs: nothing structural: each time a last-minute law raised or suspended the limit; GAO recommends replacing the process.

**As a game** (chicken; the ordering is the standard theory, not a measurement): For each side: the other swerves > both swerve (an early deal) > I swerve > nobody swerves (default, the worst for both). Each waits for the other at the brink; a default has never happened.

**Actors.**

| Actor | Wikidata | Role | Image on the item |
| --- | --- | --- | --- |
| United States Congress | [Q11268](https://www.wikidata.org/wiki/Q11268) | sets the debt limit by law | photo, logo |
| United States House of Representatives | [Q11701](https://www.wikidata.org/wiki/Q11701) | passed the 2011 (269-161) and 2023 (314-117) deals | photo, logo, flag |
| United States Senate | [Q66096](https://www.wikidata.org/wiki/Q66096) | the other chamber | photo, logo, flag |
| Barack Obama | [Q76](https://www.wikidata.org/wiki/Q76) | President in 2011 (office from Wikidata) | photo |
| John Boehner | [Q11702](https://www.wikidata.org/wiki/Q11702) | Speaker of the House 2011-2015 (office from Wikidata); voted aye on 1 August 2011 | photo |
| Joe Biden | [Q6279](https://www.wikidata.org/wiki/Q6279) | President in 2023 (office from Wikidata) | photo |
| Kevin McCarthy | [Q766866](https://www.wikidata.org/wiki/Q766866) | Speaker of the House January-October 2023 (office from Wikidata) | photo |
| United States Department of the Treasury | [Q648666](https://www.wikidata.org/wiki/Q648666) | manages cash to the X-date | photo, flag |
| Janet Yellen | [Q263725](https://www.wikidata.org/wiki/Q263725) | Treasury Secretary in 2023; disputed the Fitch downgrade | photo |
| S&P Global Ratings | [Q106158](https://www.wikidata.org/wiki/Q106158) | downgraded the US to AA+ on 5 August 2011 | photo, logo |
| Fitch Group | [Q871325](https://www.wikidata.org/wiki/Q871325) | downgraded the US to AA+ on 1 August 2023 (Fitch Ratings; the item is the Fitch Group) | logo |
| United States Government Accountability Office | [Q749118](https://www.wikidata.org/wiki/Q749118) | measured the costs of the impasses | photo, logo, flag |
| Budget Control Act of 2011 | [Q4985020](https://www.wikidata.org/wiki/Q4985020) | the 2011 deal | – |
| Fiscal Responsibility Act of 2023 | [Q118982787](https://www.wikidata.org/wiki/Q118982787) | the 2023 deal | – |
| United States debt-ceiling crisis of 2011 | [Q1591212](https://www.wikidata.org/wiki/Q1591212) | Wikidata's item for the 2011 crisis | – |

**Timeline.** (The 'coded as' column is this research's reading of each move, for the scene; it is not in the sources.)

| Date | Move | Coded as | Evidence |
| --- | --- | --- | --- |
| 2011-08-01 | House passes the Budget Control Act 269-161 | swerve | `debt-2011-vote` |
| 2011-08-02 | Law signed | swerve | `debt-2011-law` |
| 2011-08-05 | S&P cuts the US from AAA to AA+ | outcome | `debt-2011-sp` |
| 2012-07 | GAO: the 2011 delay cost about $1.3 billion in FY2011 borrowing costs | outcome | `debt-2011-cost` |
| 2023-05 | 4-week bill yields rise to 5.90% as the X-date nears, above the 8-week | outcome | series `debt-bills-2023` |
| 2023-05-31 | House passes the Fiscal Responsibility Act 314-117 | swerve | `debt-2023-vote` |
| 2023-06-03 | Law signed; limit suspended to 1 January 2025 | swerve | `debt-2023-law` |
| 2023-08-01 | Fitch cuts the US to AA+, citing repeated standoffs; Treasury disagrees | outcome | `debt-2023-fitch`, `debt-2023-yellen` |
| 2024-12 | GAO: 12 impasses 2011-2023, six resolved within days of the X-date | signal | `debt-12-impasses` |
| 2026-03-25 | GAO: $107-161 million of extra immediate borrowing costs, 2011-2023 (dated 2026) | outcome | `debt-gao26-cost` |

**Series `debt-bills-2023`: Treasury bill yields (coupon equivalent), 4-, 8- and 13-week, April-June 2023** (% a year; [U.S. Department of the Treasury](https://home.treasury.gov/resource-center/data-chart-center/interest-rates/daily-treasury-rates.csv/2023/all?type=daily_treasury_bill_rates&field_tdr_date_value=2023&page&_format=csv), read 2026-11-05). Bills maturing near the projected X-date (early June) paid more than longer ones: on 26 May the 4-week yield was 5.90% against 5.43% for 8 weeks. Fed policy (a 3 May rate rise) moves all maturities; the inversion is the standoff's mark (GAO-26-107872 documents investors avoiding X-date maturities).

| x | 4-week % | 8-week % | 13-week % |
| --- | --- | --- | --- |
| 2023-04-03 | 4.65 | 4.75 | 4.91 |
| 2023-04-21 | 3.35 | 4.91 | 5.12 |
| 2023-05-03 | 4.53 | 5.28 | 5.24 |
| 2023-05-04 | 5.66 | 5.21 | 5.25 |
| 2023-05-15 | 5.61 | 4.92 | 5.21 |
| 2023-05-24 | 5.74 | 5.18 | 5.37 |
| 2023-05-25 | 5.83 | 5.4 | 5.37 |
| 2023-05-26 | 5.9 | 5.43 | 5.31 |
| 2023-05-31 | 5.18 | 5.3 | 5.42 |
| 2023-06-02 | 5.23 | 5.34 | 5.39 |
| 2023-06-05 | 5.2 | 5.29 | 5.36 |
| 2023-06-30 | 5.18 | 5.32 | 5.32 |

**Verdict: someone always swerved, late and at a cost.** No default; the limit has been raised, extended or redefined 78 times since 1960. The 2011 delay cost about $1.3 billion in FY2011; two downgrades followed the 2011 and 2023 standoffs; bills maturing near the X-date were shunned. *Why (this research's reading of the sources):* Chicken has no stable cooperative outcome without commitment: each side waits for the other at the brink. The game recurs because the limit is set separately from the spending and tax laws that require the borrowing.
Caveats: The 2023 bill-yield inversion also reflects Fed policy; GAO's 2012 and 2026 estimates use different methods and are not additive.

**Numbers and their quotes.**

- [`debt-2011-vote`] On 1 August 2011 the House passed S. 365 by 269 votes to 161: Republicans 174-66, Democrats 95-95. — "690 S 365 On Passage RECORDED VOTE Passed 1-Aug-2011 … Republican 174 66 0 0 Democratic 95 95 0 3 Independent 0 0 0 0 Totals 269 161 0 3" — [Clerk of the U.S. House of Representatives](https://clerk.house.gov/evs/2011/roll690.xml), read 2026-11-05. (S. 365 became the Budget Control Act of 2011 (debt-2011-law); the roll call still shows the bill's original title.)
- [`debt-2011-law`] The Budget Control Act of 2011 became law on 2 August 2011. — "<<NOTE: Aug. 2, 2011 - [S. 365]>> … Act may be cited as the ``Budget Control Act of 2011''." — [U.S. Government Publishing Office](https://www.govinfo.gov/content/pkg/PLAW-112publ25/html/PLAW-112publ25.htm), read 2026-11-05.
- [`debt-2011-sp`] On 5 August 2011 S&P lowered the US government's long-term rating from AAA to AA+. — "Earlier today, Standard & Poor's rating agency lowered the long-term rating of the U.S. government and federal agencies from AAA to AA+." — [Board of Governors of the Federal Reserve System](https://www.federalreserve.gov/newsevents/pressreleases/bcreg20110805a.htm), read 2026-11-05.
- [`debt-2011-cost`] GAO estimated the 2011 delay raised Treasury's borrowing costs by about $1.3 billion in fiscal 2011. — "GAO estimated that delays in raising the debt limit in 2011 led to an increase in Treasury's borrowing costs of about $1.3 billion in fiscal year 2011." — [U.S. Government Accountability Office](https://web.archive.org/web/20260926000433id_/https://www.gao.gov/products/gao-12-701), read 2026-11-05.
- [`debt-2023-vote`] The House passed the Fiscal Responsibility Act on 31 May 2023, 314-117. — "Passed 31-May-2023 9:21 PM Fiscal Responsibility Act Party Ayes Noes Answered "Present" Not Voting Republican 149 71 0 2 Democratic 165 46 0 2 Independent 0 0 0 0 Totals 314 117 0 4" — [Clerk of the U.S. House of Representatives](https://clerk.house.gov/evs/2023/roll243.xml), read 2026-11-05.
- [`debt-2023-law`] The Fiscal Responsibility Act (3 June 2023) suspended the debt limit until 1 January 2025. — "<<NOTE: June 3, 2023 - [H.R. 3746]>> … Section 3101(b) of title 31, United States Code, shall not apply for the period beginning on the date of the enactment of this Act and ending on January 1, 2025." — [U.S. Government Publishing Office](https://www.govinfo.gov/content/pkg/PLAW-118publ5/html/PLAW-118publ5.htm), read 2026-11-05.
- [`debt-2023-fitch`] On 1 August 2023 Fitch cut the US to AA+ from AAA, citing repeated debt-limit standoffs. — "Fitch Ratings cut the United States' long-term foreign currency issuer default rating to AA+ from AAA. … The repeated debt-limit political standoffs and last-minute resolutions have eroded confidence in fiscal management," — [CNBC](https://www.cnbc.com/2023/08/01/fitch-downgrades-us-long-term-ratings-to-aa-from-aaa.html), read 2026-11-05.
- [`debt-2023-yellen`] Treasury Secretary Yellen called Fitch's decision arbitrary and said the debt-limit law included over $1 trillion of deficit reduction. — "I strongly disagree with Fitch Ratings' decision. The change by Fitch Ratings announced today is arbitrary and based on outdated data. … The most recent debt limit legislation included over $1 trillion in deficit reduction and improved our fiscal trajectory." — [U.S. Department of the Treasury (via FRASER, Federal Rese…](https://fraser.stlouisfed.org/files/docs/publications/treaspr/2023/2023-08-01_jy1665.pdf), read 2026-11-05.
- [`debt-78-times`] Since 1960 Congress has raised, extended or redefined the debt limit 78 times (49 under Republican presidents, 29 under Democratic). — "Since 1960, Congress has acted 78 separate times to permanently raise, temporarily extend, or revise the definition of the debt limit – 49 times under Republican presidents and 29 times under Democratic presidents." — [U.S. Department of the Treasury](https://home.treasury.gov/policy-issues/financial-markets-financial-institutions-and-fiscal-service/debt-limit), read 2026-11-05.
- [`debt-12-impasses`] There were 12 debt-limit impasses from 2011 to 2023; in six the limit was raised or suspended within days of the projected X-date. — "From 2011 through 2023, there were 12 debt limit impasses, which were resolved by increases or temporary suspensions of the debt limit. … in six of the 12 debt limit impasses between 2011 and 2023, the debt limit was raised or suspended within days before the projected X-date." — [U.S. Government Accountability Office](https://files.gao.gov/reports/GAO-25-107089/index.html), read 2026-11-05.
- [`debt-x-date`] GAO: projections of the X-date are inherently imprecise, so last-minute negotiations raise the risk of default. — "Predictions about when the government will no longer be able to pay all of its obligations—the "X-date"—are inherently imprecise due to the unpredictable size and timing of federal cash flows. Consequently, last-minute negotiations on the debt limit can increase the risk of a default." — [U.S. Government Accountability Office](https://files.gao.gov/reports/GAO-25-107089/index.html), read 2026-11-05.
- [`debt-gao26-cost`] GAO (March 2026) estimates the impasses of 2011-2023 cost roughly $107-161 million in extra immediate borrowing costs (2024 dollars), and investors shunned bills maturing near the X-date. **[2026 gate: verified, Jev 0.94]** — "Published: Mar 25, 2026. … GAO estimates that Treasury securities issued during periods of acute market concern over impasses between 2011 and 2023—the most recent impasses with complete data available at the time of GAO's analysis—incurred a total of roughly $107 million to $161 million in increased immediate borrowing costs (in 2024 dollars), depending on the measure used to estimate market concern. … Market participants avoided securities maturing near a projected X-date, as those maturing after this date would be the first to default if the impasse were not resolved in time." — [U.S. Government Accountability Office](https://files.gao.gov/reports/GAO-26-107872/index.html), read 2026-11-05. (GAO-12-701's $1.3 billion (FY2011, multiyear effects excluded) and this estimate use different methods and are not additive.)

## 8. The global minimum tax, 2021 and after

*Game: race to the bottom.* Each country gains investment and booked profits by taxing companies a little less than its neighbours, and all lose revenue when everyone does; the average statutory rate fell from 28.0% in 2000 to 21.7% in 2019, and in 2021 136 jurisdictions agreed a 15% floor that takes the gain out of undercutting.

**The game in plain words.** Players: about 140 governments competing for multinational companies' profits and investment. Cooperating: tax large multinationals at 15% or more. Defecting: undercut to attract profits and headquarters. The temptation: the undercutter gains the tax base others lose. Why it was hard: profits can be booked where taxes are low with little real activity there (profit-shifting indicators remain far higher in investment hubs). What changed the payoffs: the 15% floor is enforced by top-up taxes: if a low-tax country does not tax its multinationals up to 15%, the parent or other countries may, so undercutting no longer moves the money; a domestic top-up tax (QDMTT) lets the low-tax country collect it first.

**As a game** (n-player prisoner's dilemma (race to the bottom); the ordering is the standard theory, not a measurement): For each country: others keep rates and I undercut > all keep rates > all undercut > I keep my rate while others undercut. The top-up tax removes the gain from undercutting below 15%.

**Actors.**

| Actor | Wikidata | Role | Image on the item |
| --- | --- | --- | --- |
| Organization for Economic Cooperation and Development | [Q41550](https://www.wikidata.org/wiki/Q41550) | secretariat of the negotiation | photo, logo |
| OECD/G20 Inclusive Framework | [Q133865145](https://www.wikidata.org/wiki/Q133865145) | the 140-member forum that agreed the deal (Wikidata labels the item a plan) | – |
| G20 | [Q19771](https://www.wikidata.org/wiki/Q19771) | the finance ministers who received the deal (13 October 2021) | photo |
| Mathias Cormann | [Q1813692](https://www.wikidata.org/wiki/Q1813692) | OECD Secretary-General in 2021 | photo |
| Ireland | [Q27](https://www.wikidata.org/wiki/Q27) | low-tax holdout that joined with Estonia (7 October 2021) and Hungary (8 October) | photo, flag |
| Paschal Donohoe | [Q1271675](https://www.wikidata.org/wiki/Q1271675) | Ireland's finance minister, who welcomed the agreement | photo |
| Estonia | [Q191](https://www.wikidata.org/wiki/Q191) | joined on 7 October 2021 | photo, flag |
| Hungary | [Q28](https://www.wikidata.org/wiki/Q28) | joined on 8 October 2021 | photo, flag |
| Kenya | [Q114](https://www.wikidata.org/wiki/Q114) | stayed out | photo, flag |
| Nigeria | [Q1033](https://www.wikidata.org/wiki/Q1033) | stayed out | photo, flag |
| Pakistan | [Q843](https://www.wikidata.org/wiki/Q843) | stayed out | photo, flag |
| Sri Lanka | [Q854](https://www.wikidata.org/wiki/Q854) | stayed out | photo, flag |
| United States | [Q30](https://www.wikidata.org/wiki/Q30) | backed the 2021 deal; declared it of no effect in January 2025 | photo, flag |
| Donald Trump | [Q22686](https://www.wikidata.org/wiki/Q22686) | signed the 20 January 2025 memorandum | photo |
| global minimum corporate tax rate | [Q106610285](https://www.wikidata.org/wiki/Q106610285) | the policy | photo |

**Timeline.** (The 'coded as' column is this research's reading of each move, for the scene; it is not in the sources.)

| Date | Move | Coded as | Evidence |
| --- | --- | --- | --- |
| 2000 | Average statutory corporate rate 28.0% | context | `tax-cit-decline` |
| 2019 | Average down to 21.7% | defect | `tax-cit-decline` |
| 2021-10-07 | Ireland and Estonia join | cooperate | `tax-holdouts` |
| 2021-10-08 | 136 jurisdictions agree a 15% minimum; Kenya, Nigeria, Pakistan and Sri Lanka stay out; no new digital services taxes | cooperate | `tax-2021-136`, `tax-2021-15pct`, `tax-holdouts`, `tax-2021-dst` |
| 2023-06-09 | 139 jurisdictions | cooperate | `tax-2023-139` |
| 2025-01-20 | US declares the deal of no force or effect | defect | `tax-us-2025` |
| 2025-11-25 | Average rate 21.2% in 2025, levelling off | outcome | `tax-cit-decline` |
| 2026-01-05 | Side-by-Side package adopted by consensus (dated 2026) | cooperate | `tax-sbs-2026` |
| 2026-04 | More than 65 jurisdictions implementing (dated 2026) | cooperate | `tax-65` |
| 2026-07-15 | Impact estimate: +US$91-155 billion a year; profit shifting cut by 22.6-44.6% (dated 2026) | outcome | `tax-eia-2026` |

**Series `tax-cit-average`: Average combined statutory corporate income tax rate, Inclusive Framework jurisdictions (the points the OECD states)** (% (simple average); [OECD](https://web.archive.org/web/20261016110240id_/https://www.oecd.org/en/about/news/press-releases/2025/11/corporate-tax-revenues-rise-as-global-corporate-tax-rates-continue-to-stabilise-new-oecd-data-show.html), read 2026-11-05). Only the four years the 25 November 2025 press release states; the full annual series is in OECD Corporate Tax Statistics (not fetched). The 2025 value was published in November 2025.

| x | average CIT % |
| --- | --- |
| 2000 | 28 |
| 2019 | 21.7 |
| 2024 | 21.1 |
| 2025 | 21.2 |

**Verdict: the race halted; the floor is partly built.** The average statutory rate stopped falling after 2019 (21.2% in 2025); corporate tax rose to 17.8% of tax revenue in 2022. The US withdrew its support in January 2025; the 2026 Side-by-Side package accommodates comparable minimum-tax systems. *Why (this research's reading of the sources):* The top-up tax changes the payoffs: undercutting below 15% no longer keeps the revenue away from other countries, so the best reply is to collect it at home. Holdouts lose little by staying out, which is why participation, not the rate, is the weak point.
Caveats: Whether the halt is the deal's effect or began before it (rates were stable from 2019) is not settled by these sources; the 2026 facts pass only through the gate.

**Numbers and their quotes.**

- [`tax-cit-decline`] The average combined statutory corporate tax rate across Inclusive Framework jurisdictions fell from 28.0% (2000) to 21.7% (2019), then levelled off: 21.1% in 2024, 21.2% in 2025. — "The average combined statutory corporate income tax rate (CIT) across Inclusive Framework jurisdictions declined from 28.0% in 2000 to 21.7% in 2019 and has since remained relatively stable. The average rate stood at 21.2% in 2025, a slight increase from 21.1% in 2024, confirming that the long-term downward trend is levelling off." — [OECD](https://web.archive.org/web/20261016110240id_/https://www.oecd.org/en/about/news/press-releases/2025/11/corporate-tax-revenues-rise-as-global-corporate-tax-rates-continue-to-stabilise-new-oecd-data-show.html), read 2026-11-05.
- [`tax-cit-revenue`] Corporate tax revenues rose from 15.9% to 17.8% of total tax revenue in 2022 (average of 131 jurisdictions). — "the share of corporate tax revenues in total tax revenues increased by nearly two percentage points in 2022 - from 15.9% to 17.8% on average across 131 jurisdictions for which data is available." — [OECD](https://web.archive.org/web/20261016110240id_/https://www.oecd.org/en/about/news/press-releases/2025/11/corporate-tax-revenues-rise-as-global-corporate-tax-rates-continue-to-stabilise-new-oecd-data-show.html), read 2026-11-05.
- [`tax-beps-hubs`] OECD: profit-shifting indicators remain much higher in investment hubs than elsewhere, a sign that profits are still booked apart from real activity. — "BEPS indicators also remain significantly higher in investment hubs than in other jurisdictions, suggesting continued misalignment of profit and economic substance" — [OECD](https://web.archive.org/web/20261016110240id_/https://www.oecd.org/en/about/news/press-releases/2025/11/corporate-tax-revenues-rise-as-global-corporate-tax-rates-continue-to-stabilise-new-oecd-data-show.html), read 2026-11-05.
- [`tax-2021-136`] On 8 October 2021, 136 Inclusive Framework members agreed the two-pillar statement. — "136 member jurisdictions have agreed to it as of 8 October 2021." — [OECD/G20 Inclusive Framework on BEPS](https://web.archive.org/web/20211011225942id_/https://www.oecd.org/tax/beps/statement-on-a-two-pillar-solution-to-address-the-tax-challenges-arising-from-the-digitalisation-of-the-economy-october-2021.pdf), read 2026-11-05.
- [`tax-2021-15pct`] The global minimum rate is 15%. — "The minimum tax rate used for purposes of the IIR and UTPR will be 15%." — [OECD/G20 Inclusive Framework on BEPS](https://web.archive.org/web/20211011225942id_/https://www.oecd.org/tax/beps/statement-on-a-two-pillar-solution-to-address-the-tax-challenges-arising-from-the-digitalisation-of-the-economy-october-2021.pdf), read 2026-11-05.
- [`tax-2021-dst`] Members agreed to impose no new digital services taxes from 8 October 2021 until the end of 2023 or the multilateral convention. — "No newly enacted Digital Services Taxes or other relevant similar measures will be imposed on any company from 8 October 2021 and until the earlier of 31 December 2023 or the coming into force of the MLC." — [OECD/G20 Inclusive Framework on BEPS](https://web.archive.org/web/20211011225942id_/https://www.oecd.org/tax/beps/statement-on-a-two-pillar-solution-to-address-the-tax-challenges-arising-from-the-digitalisation-of-the-economy-october-2021.pdf), read 2026-11-05.
- [`tax-2023-139`] 139 members had joined by 9 June 2023. — "139 member jurisdictions have agreed to it as of 9 June 2023." — [OECD/G20 Inclusive Framework on BEPS](https://www.oecd.org/content/dam/oecd/en/topics/policy-issues/beps/statement-on-a-two-pillar-solution-to-address-the-tax-challenges-arising-from-the-digitalisation-of-the-economy-october-2021.pdf), read 2026-11-05.
- [`tax-holdouts`] Ireland, Estonia and Hungary joined in the last two days; Kenya, Nigeria, Pakistan and Sri Lanka stayed out. — "Ireland was joined by Estonia last night and today by Hungary which means all EU member states are part of the deal. Of the 140 countries which engaged in the OECD process, Kenya, Nigeria, Pakistan and Sri Lanka have opted to remain outside the agreement." — [RTÉ News](https://www.rte.ie/news/2021/1008/1252439-corporate-tax-reform/), read 2026-11-05.
- [`tax-150bn`] The deal was expected to raise about US$150 billion a year and applies to groups with sales above €750 million. — "It also confirms that it will apply to companies with sales above €750 million a year. This is expected to generate an extra US$150 billion in global tax revenues annually." — [RTÉ News](https://www.rte.ie/news/2021/1008/1252439-corporate-tax-reform/), read 2026-11-05.
- [`tax-us-2025`] In a memorandum of 20 January 2025 President Trump declared that the OECD Global Tax Deal has no force or effect in the United States unless Congress adopts it. — "Administration of Donald J. Trump, 2025 Memorandum on the Organization for Economic Co-operation and Development (OECD) Global Tax Deal (Global Tax Deal) January 20, 2025 … This memorandum recaptures our Nation's sovereignty and economic competitiveness by clarifying that the Global Tax Deal has no force or effect in the United States. … any commitments made by the prior administration on behalf of the United States with respect to the Global Tax Deal have no force or effect within the United States absent an act by the Congress adopting the relevant provisions of the Global Tax Deal." — [Compilation of Presidential Documents, U.S. Government Pu…](https://www.govinfo.gov/content/pkg/DCPD-202500150/html/DCPD-202500150.htm), read 2026-11-05.
- [`tax-sbs-2026`] The Inclusive Framework adopted the Side-by-Side package by consensus on 5 January 2026, accommodating comparable minimum-tax systems while keeping domestic top-up taxes (QDMTTs) for all groups. **[2026 gate: verified, Jev 0.88]** — "The Side-by-Side package,5 released on 5 January 2026, marks a significant technical and political achievement for the Inclusive Framework following months of complex negotiations. Its adoption by consensus underscores the strong and continuing commitment of Inclusive Framework members to international tax co-operation. … Crucially, the agreement reaffirms the primacy of QDMTTs, which continue to apply to all MNE Groups without exception" — [OECD](https://www.oecd.org/content/dam/oecd/en/publications/reports/2026/04/oecd-secretary-general-tax-report-to-g20-finance-ministers-and-central-bank-governors-g20-united-states-april-2026_c41601f7/02d05307-en.pdf), read 2026-11-05.
- [`tax-65`] More than 65 jurisdictions have implemented or are implementing the GloBE rules or a domestic minimum top-up tax (April 2026). **[2026 gate: verified, Jev 0.78]** — "To date, more than 65 jurisdictions have already implemented or are in the process of implementing the GloBE rules or a Qualified Domestic Minimum Top-up Tax (QDMTT)." — [OECD](https://www.oecd.org/content/dam/oecd/en/publications/reports/2026/04/oecd-secretary-general-tax-report-to-g20-finance-ministers-and-central-bank-governors-g20-united-states-april-2026_c41601f7/02d05307-en.pdf), read 2026-11-05.
- [`tax-eia-2026`] OECD's July 2026 update: the minimum tax raises global tax revenue by 3.2-5.4% (US$91-155 billion a year) and cuts profit shifting by 22.6-44.6%. **[2026 gate: verified, Jev 0.93]** — "15 July 2026 … The GMT is estimated to reduce profit-shifting substantially with an estimated reduction of between 22.6 – 44.6% Global tax revenues are estimated to rise by 3.2 – 5.4% or USD 91 – 155 bn per year" — [OECD](https://www.oecd.org/content/dam/oecd/en/events/2026/07/economic-impact-assessment-2026/eia-2026-update-webinar-presentation-en.pdf), read 2026-11-05.

## 9. The US-China tariff war, 2018-2026

*Game: tariff war (iterated prisoner's dilemma).* Each side gains by protecting its producers and pressing the other, and both lose trade when both do; the two answered each other's main tariff rounds, made truces in 2020 and 2025, and in spring 2025 the averages reached 134.3% (US on China, one day in April; 126.1% by early May) and 147.6% (China on the US, mid-April).

**The game in plain words.** Players: the United States and China. Cooperating: keep tariffs low; honour purchase and tariff commitments. Defecting: raise tariffs or controls on the other. The temptation: a tariff can win concessions or protect an industry if the other does not respond. Why it was hard: each side retaliated (tit for tat), and truces were hard to verify or enforce (China bought 58% of what Phase One promised). What changed the payoffs: truces with suspensions and expiry dates (Phase One 2020; Geneva, May 2025; Kuala Lumpur, November 2025); a US court ruling in February 2026 removed one legal tool.

**As a game** (iterated prisoner's dilemma between two players; the ordering is the standard theory, not a measurement): For each side: I tariff and the other does not > neither does > both do > the other tariffs and I do not. Their moves look like tit for tat, the strategy that won Axelrod's tournaments (games.json): escalations and de-escalations were both answered in kind.

**Actors.**

| Actor | Wikidata | Role | Image on the item |
| --- | --- | --- | --- |
| United States | [Q30](https://www.wikidata.org/wiki/Q30) | imposed the Section 301, IEEPA and later tariffs | photo, flag |
| People's Republic of China | [Q148](https://www.wikidata.org/wiki/Q148) | retaliated round for round | photo, flag |
| Donald Trump | [Q22686](https://www.wikidata.org/wiki/Q22686) | US President in both escalations (signed Phase One) | photo |
| Xi Jinping | [Q15031](https://www.wikidata.org/wiki/Q15031) | China's leader | photo |
| Robert Lighthizer | [Q28113710](https://www.wikidata.org/wiki/Q28113710) | US Trade Representative 2017-2021 (office from Wikidata) | photo |
| Liu He | [Q8294074](https://www.wikidata.org/wiki/Q8294074) | Vice Premier, China's Phase One negotiator (from Wikidata's 'economist and politician'; role not in the cached sources) | photo |
| Scott Bessent | [Q7435987](https://www.wikidata.org/wiki/Q7435987) | US Treasury Secretary, a 2025 negotiator (office from Wikidata; negotiating role not in the cached sources) | photo |
| He Lifeng | [Q8291839](https://www.wikidata.org/wiki/Q8291839) | Vice Premier of China, a 2025 negotiator (office from Wikidata; negotiating role not in the cached sources) | photo |
| Office of the United States Trade Representative | [Q1574977](https://www.wikidata.org/wiki/Q1574977) | runs Section 301 actions | flag |
| Ministry of Commerce of the People's Republic of China | [Q1064994](https://www.wikidata.org/wiki/Q1064994) | announces China's countermeasures | photo, logo |
| Supreme Court of the United States | [Q11201](https://www.wikidata.org/wiki/Q11201) | held on 20 February 2026 that IEEPA does not authorize tariffs | photo, logo |
| Learning Resources v. Trump | [Q136260440](https://www.wikidata.org/wiki/Q136260440) | the case | – |
| Peterson Institute for International Economics | [Q2981447](https://www.wikidata.org/wiki/Q2981447) | keeps the tariff series | photo |
| Chad P. Bown | [Q41799800](https://www.wikidata.org/wiki/Q41799800) | author of the series and the Phase One audit | photo |
| China–United States trade war | [Q51557538](https://www.wikidata.org/wiki/Q51557538) | Wikidata's item for the trade war | photo |

**Timeline.** (The 'coded as' column is this research's reading of each move, for the scene; it is not in the sources.)

| Date | Move | Coded as | Evidence |
| --- | --- | --- | --- |
| 2018-07-06 | First Section 301 round; China retaliates | defect | global.json us_china_trade_war |
| 2020-01-15 | Phase One signed: China to buy $200 billion more | cooperate | `tariff-phase1` |
| 2020-02 | US average 19.3% on 66.6% of imports from China; China's 21% | outcome | `tariff-2020` |
| 2021-12-31 | Purchase period ends at 58% of the commitment | defect | `tariff-phase1` |
| 2025-02-04 | New US tariffs on China; China retaliates in tranches | defect | `tariff-2025-peak` |
| 2025-04 | US average on China 134.3% on 10 April, 126.1% by early May; China's average on the US peaks at 147.6% in mid-April | defect | `tariff-2025-peak`, global.json tariffs.series rows 2025-04-10, 2025-05-03 |
| 2025-05-14 | Geneva truce: US average to 51.7%, China's to 31.9% | cooperate | `tariff-geneva` |
| 2025-11-10 | Kuala Lumpur/Korea arrangement: further cuts; suspension to 10 November 2026 | cooperate | global.json us_china_truce, us_china_deal_nov2025 |
| 2026-02-20 | Supreme Court strikes down the IEEPA tariffs; Section 122, then Section 301 tariffs (dated 2026) | referee | `tariff-ieepa` |
| 2026-08-22 | US 35.2% on China, China 31.9% on the US; US 16.3% on the rest of the world (dated 2026) | outcome | `tariff-now` |

**Series `tariff-trade`: US goods trade with China: exports, imports, balance (2026 = January-September)** (US$ million, nominal, not seasonally adjusted; [U.S. Census Bureau](https://www.census.gov/foreign-trade/balance/c5700.html), read 2026-11-05). 2025 totals were completed in February 2026 and 2026 is partial (to September), so both are dated 2026 for the gate. Part of the fall is rerouting through third countries (not measured here).

| x | exports $m | imports $m | balance $m |
| --- | --- | --- | --- |
| 2010 | 91,911.1 | 364,952.6 | -273,041.6 |
| 2015 | 115,873.4 | 483,201.7 | -367,328.3 |
| 2017 | 129,997.2 | 505,165.1 | -375,167.9 |
| 2018 | 120,281.2 | 538,514.2 | -418,232.9 |
| 2019 | 106,481.2 | 449,110.7 | -342,629.5 |
| 2020 | 124,581.5 | 432,548.0 | -307,966.5 |
| 2022 | 153,987.4 | 536,268.7 | -382,281.3 |
| 2023 | 147,626.5 | 427,234.3 | -279,607.8 |
| 2024 | 143,271.7 | 440,319.4 | -297,047.7 |
| 2025 (pub. 2026, gate: verified) | 105,977.8 | 308,651.9 | -202,674.1 |
| 2026 (pub. 2026, gate: verified) | 83,516.8 | 215,008.6 | -131,491.8 |

**Series in the repository: `global.json` `tariffs.series`** (PIIE, Chad P. Bown, US-China Trade War Tariffs: An Up-to-Date Chart (workbook, sheet 'Panel a'); 91 rows; % (trade-weighted average); caveat: statutory rates on fixed 2017 trade, not duties collected). Key rows:

| Date | Action (PIIE) | US on China | China on US | US on the rest | Gate |
| --- | --- | --- | --- | --- | --- |
| 2018-01-01 | January 1, 2018 | 3.09 | 7.99 | 2.17 | not dated 2026 |
| 2018-07-06 | US Section 301 tariffs of 25% ($34 billion, List 1) and China’s retaliation ($34 billion) | 6.75 | 10.12 | 2.9 | not dated 2026 |
| 2018-09-24 | US Section 301 tariffs of 10% ($200 billion, List 3) and China’s retaliation ($60 billion) | 12.06 | 18.33 | 2.92 | not dated 2026 |
| 2019-09-01 | US Section 301 tariffs of 15% (subset of $300 billion, List 4A) and China’s retaliation on some US products (s | 21.04 | 21.84 | 2.89 | not dated 2026 |
| 2020-02-14 | US Section 301 tariffs of 15% imposed on September 1, 2019 (List 4A) cut to 7.5%, and China’s retaliatory tari | 19.29 | 21.25 | 3.04 | not dated 2026 |
| 2025-02-04 | US tariffs of 10 percent on all imports from China under International Emergency Economic Powers Act (IEEPA) | 30.74 | 21.17 | 3.03 | not dated 2026 |
| 2025-04-10 | US additional tariffs on trade surplus countries under IEEPA reversed except for China; US additional tariffs  | 134.27 | 106.6 | 11.11 | not dated 2026 |
| 2025-05-14 | US reduces to 10 percent from 125 percent the cumulative tariffs under IEEPA imposed on certain imports from C | 51.66 | 32.6 | 12.46 | not dated 2026 |
| 2025-11-10 | US reduces to 10 percent from 20 percent the tariffs on all imports from China under IEEPA; China suspends the | 47.44 | 31.88 | 19.39 | not dated 2026 |
| 2026-02-24 | US Section 122 tariffs of 10 percent imposed on nearly all countries | 36.85 | 31.91 | 16.92 | verified |
| 2026-07-24 | US 10% tariffs under Section 122 expire | 27.28 | 31.91 | 9.35 | verified |
| 2026-07-24 | US Section 122 tariffs of 10 percent expire; US imposes 10 to 12.5 percent Section 301 tariffs on 60 economies | 34.85 | 31.91 | 15.85 | verified |

**Verdict: ongoing: two truces, no settlement.** US imports from China fell from $505.2 billion (2017) to $308.7 billion (2025); average tariffs stood at 35.2% (US on China) and 31.9% (China on the US) on 22 August 2026, against 3.1% and 8.0% in January 2018 (PIIE, in the repository's series). *Why (this research's reading of the sources):* Both sides played something like tit for tat: the main escalations were answered, and so were the de-escalations (at Geneva both averages were cut at once). Truces held only with deadlines and suspensions; Phase One's purchase promise was, in PIIE's words, problematic if not unrealistic from the start, and was not met.
Caveats: PIIE's averages are statutory rates on fixed 2017 trade, not duties paid; part of the fall in direct imports reflects rerouting through third countries. The series' one-day US high (134.27%, 10 April 2025) is above the 126.1% PIIE's text gives for early May.

In the repository: global.json tariffs.series (PIIE, 91 dated rows); global.json events us_china_trade_war, us_china_phase_one, us_china_2025, us_china_truce, us_china_deal_nov2025, ieepa_ruling, section122, us_301_forced_labour.

**Numbers and their quotes.**

- [`tariff-2020`] By February 2020 US tariffs on Chinese goods averaged 19.3% (six times January 2018) on 66.6% of imports from China; China's on US goods averaged 21%, up from 8.0%. — "US tariffs on Chinese exports in February 2020 averaged 19.3 percent, more than six times higher than in January 2018. Those tariffs covered 66.6 percent of US imports from China, or roughly $335 billion of trade (measured in terms of 2017 import levels). … Average Chinese tariffs on US exports starting in February 2020 were also at elevated levels of 21 percent, up from 8.0 percent prior to the trade war." — [Peterson Institute for International Economics](https://www.piie.com/research/piie-charts/2019/us-china-trade-war-tariffs-date-chart), read 2026-11-05. (Same passage as global.json event us_china_trade_war (verified 2026-11-03).)
- [`tariff-phase1`] Under Phase One China bought only 58% of the US exports it had committed to, none of the extra $200 billion; the deal did halt the escalation, though PIIE calls it problematic, if not unrealistic, from the start. — "In the end, China bought only 58 percent of the US exports it had committed to purchase under the agreement, not even enough to reach its import levels from before the trade war.[1] Put differently, China bought none of the additional $200 billion of exports Trump's deal had promised. … The deal did halt his spiraling trade war. … However, signing something that was problematic, if not unrealistic, from the start, shows some degree of bad faith on both sides." — [Peterson Institute for International Economics](https://www.piie.com/blogs/realtime-economics/2022/china-bought-none-extra-200-billion-us-exports-trumps-trade-deal), read 2026-11-05.
- [`tariff-2025-peak`] Average US tariffs on Chinese goods had risen to 126.1% by early May 2025; China's average on US goods peaked at 147.6% in mid-April 2025. — "Average US tariffs on imports from China had increased to 126.1 percent in early May 2025, before later being reduced. … With its retaliation in 2025, average Chinese tariffs on imports from the US peaked at 147.6 percent in mid-April before later being reduced." — [Peterson Institute for International Economics](https://www.piie.com/research/piie-charts/2019/us-china-trade-war-tariffs-date-chart), read 2026-11-05. (PIIE's workbook (global.json tariffs.series) has the US average at 134.27% for one day, 10 April 2025, before the 11 April semiconductor exclusion (123.67%); 126.13% is its 3 May 2025 row.)
- [`tariff-geneva`] At Geneva (effective 14 May 2025) both sides converted April's 125-point increases to 10%: the US average fell from 126.1% to 51.7%, China's from 147.6% to 31.9%. — "both sides agreed to convert their cumulative bilateral tariff increases taking place in April to 10 percent (instead of 125 percent), effective as of May 14. As a result, the average US tariff on imports from China fell from 126.1 percent to 51.7 percent. … the average Chinese tariff on imports from the United States fell from 147.6 percent to 31.9 percent, where it largely remains today." — [Peterson Institute for International Economics](https://www.piie.com/research/piie-charts/2019/us-china-trade-war-tariffs-date-chart), read 2026-11-05.
- [`tariff-now`] As of 22 August 2026, US tariffs on Chinese goods average 35.2% (89% of goods) and China's on US goods 31.9% (all goods); the US average on the rest of the world rose from 3.0% to 16.3% since 20 January 2025. **[2026 gate: verified, Jev 0.90]** — "As of August 22, 2026, average US tariffs on Chinese exports stand at 35.2 percent and cover 89 percent of all goods. China's average tariffs on US exports are at 31.9 percent and cover 100 percent of all goods. … the average US tariff on all goods imports from the rest of the world increased from 3.0 percent to 16.3 percent between January 20, 2025, and August 22, 2026." — [Peterson Institute for International Economics](https://www.piie.com/research/piie-charts/2019/us-china-trade-war-tariffs-date-chart), read 2026-11-05. (The repository's PIIE series (global.json tariffs.series) carries the same values row by row; its 2026-07-31 row is unverified there (Jev 0.51).)
- [`tariff-ieepa`] After the Supreme Court struck down the IEEPA tariffs on 20 February 2026, the US imposed Section 122 tariffs, replaced by Section 301 tariffs from 24 July 2026. **[2026 gate: verified, Jev 0.96]** — "Immediately following the US Supreme Court decision of February 20, 2026, that struck down the 2025 tariffs imposed on China and other trading partners under IEEPA, the Trump administration imposed new tariffs under Section 122 of the Trade Act of 1974, which it then replaced with tariffs under Section 301 of the Trade Act of 1974 beginning on July 24, 2026." — [Peterson Institute for International Economics](https://www.piie.com/research/piie-charts/2019/us-china-trade-war-tariffs-date-chart), read 2026-11-05. (Also in global.json as events ieepa_ruling, section122 and us_301_forced_labour (verified 2026-11-03).)

## 10. Linking the cases to the repository

- **global.json events.** `saudi_russia_price_war` (OPEC+ breakdown) is verified but not drawn because its end date has passed with no
  recorded outcome; the OPEC case's April 2020 deal (`opec-2020-04-12`, OPEC's own release) is that outcome. `uae_leaves_opec`,
  `us_china_trade_war`, `us_china_phase_one`, `us_china_2025`, `us_china_truce`, `us_china_deal_nov2025`, `ieepa_ruling` and `section122` are
  the tariff case's moves; this file quotes the same PIIE passages and adds Census trade values. The Russia sanctions coalition
  (`russia_sanctions`, `russia_price_cap`) is a further collective-action case already in the data (a coalition holding a price cap), not
  researched again here.
- **games.json.** Axelrod's payoffs (T 5, R 3, P 1, S 0) and the shadow-of-the-future threshold (w at least 1/2) give the scene its
  theory: the tariff war and OPEC+ are repeated games with a long shadow (the same players meet every month), the run and the
  standoff are coordination and chicken games where the threshold logic does not apply, and the lobster harbor gangs are the
  small, repeated groups in which reciprocity works.
- **map/views.md.** No collective-action view is readable today and the planned tour has no collective-action stop; these nine cases are
  candidates for that stop, each with actors to show as images and one series to show as an object (a stock, a price, a crowd).

## 11. The 2026 gate

Facts and points dated 2026 (published in 2026, or describing 2026) may be drawn only when the quote was found on the source fetched
on or after 4 Nov 2026 and Jev supports the claim at 0.6 or more with the project's gate question (Tools/global/verify.py
`JEV_QUESTION`, copied verbatim; excerpt 1,500 characters before and 2,500 after a single quote, 600/900 around each of several).

| Fact or point | Claim | Quote found | Jev | Status |
| --- | --- | --- | --- | --- |
| `ozone-parties-198` | As of 4 November 2026 the Montreal Protocol has 198 parties and 46 signatories. | yes | 0.98 | verified |
| `ozone-mlf-totals` | The Multilateral Fund reports US$4.3 billion in grants for 10,198 approved projects in 144 developing countries. | yes | 0.96 | verified |
| `cod-2026-healthy` | On 12 June 2026 DFO said the stock was in the healthy zone for the first time since the 1992 moratorium and raised the TAC from 38,000 t to 59,000 t. | yes | 0.97 | verified |
| `lob-2025-prelim` | Maine's 2025 landings figures are preliminary, last updated 9 February 2026. | yes | 0.98 | verified |
| `opec-2026-unwind` | On 1 March 2026 the eight resumed unwinding the 1.65 mb/d cut of April 2023, adding 206 thousand b/d in April 2026. | yes | 0.94 | verified |
| `opec-uae-exit` | The UAE announced on 28 April 2026 that it was leaving OPEC from 1 May; without it OPEC's share of world crude output in 2025 would have been 31% instead of 35%. | yes | 0.97 | verified |
| `svb-who-ran` | FDIC staff (May 2026): 74% of SVB's top depositors and 39% of all its depositors ran; SVB lost half its 6 March domestic deposits between 9 and 14 March. | yes | 0.87 | verified |
| `svb-concentration` | FDIC staff (May 2026): deposits at all three failed banks were concentrated among a small number of large depositors, and most of the money ran by wire in the first few days. | yes | 0.88 | verified |
| `svb-insured-stayed` | FDIC staff (May 2026): fully insured retail depositors generally did not run. | yes | 0.83 | verified |
| `uaw-bls-2025` | 30 major work stoppages began in 2025, involving 306,800 workers. | yes | 0.99 | verified |
| `debt-gao26-cost` | GAO (March 2026) estimates the impasses of 2011-2023 cost roughly $107-161 million in extra immediate borrowing costs (2024 dollars), and investors shunned bills maturing near the X-date. | yes | 0.94 | verified |
| `tax-sbs-2026` | The Inclusive Framework adopted the Side-by-Side package by consensus on 5 January 2026, accommodating comparable minimum-tax systems while keeping domestic top-up taxes (QDMTTs) for all groups. | yes | 0.88 | verified |
| `tax-65` | More than 65 jurisdictions have implemented or are implementing the GloBE rules or a domestic minimum top-up tax (April 2026). | yes | 0.78 | verified |
| `tax-eia-2026` | OECD's July 2026 update: the minimum tax raises global tax revenue by 3.2-5.4% (US$91-155 billion a year) and cuts profit shifting by 22.6-44.6%. | yes | 0.93 | verified |
| `tariff-now` | As of 22 August 2026, US tariffs on Chinese goods average 35.2% (89% of goods) and China's on US goods 31.9% (all goods); the US average on the rest of the world rose from 3.0% to 16.3% since 20 January 2025. | yes | 0.90 | verified |
| `tariff-ieepa` | After the Supreme Court struck down the IEEPA tariffs on 20 February 2026, the US imposed Section 122 tariffs, replaced by Section 301 tariffs from 24 July 2026. | yes | 0.96 | verified |
| `ozone-cfc-consumption` 2025 | In 2025, consumption of CFCs (Annex group A/I) by all Parties to the Montreal Protocol was -836.1 ODP tonnes and consumption of halons (A/II) was -110.2 ODP tonnes. | yes | 0.97 | verified |
| `lobster-landings` 2025 | Maine's preliminary 2025 lobster landings were 78,826,683 pounds (35,755 metric tons), with an ex-vessel value of $461,384,405. | yes | 0.96 | verified |
| `uaw-stoppages` 2025 | In 2025, 30 work stoppages involving 1,000 or more workers began in the United States (32 were in effect), 306.8 thousand workers were involved in those beginning in the year, and there were 1,991.2 thousand days of idleness. | yes | 0.93 | verified |
| `tariff-trade` 2025 | In 2025 US goods exports to China totalled $105,977.8 million and US imports from China $308,651.9 million, a balance of -$202,674.1 million. | yes | 0.98 | verified |
| `tariff-trade` 2026 | The Census table of US trade in goods with China for 2026 lists the months January to September 2026, with a 2026 total of $83,516.8 million in exports, $215,008.6 million in imports and a balance of -$131,491.8 million. | yes | 0.94 | verified |

## 12. How this was checked

- Sources were fetched with `curl` (User-Agent `why-2-holistic/0.1 (https://github.com/atonalfreerider/why-2)`; a browser-like agent
  naming the project for two hosts that refuse scripts), PDFs converted with `pdftotext` (plain and `-layout`), HTML stripped of scripts
  and styles. Every fetch has a `.meta.json` with URL, final URL, HTTP status, date, bytes and sha256 (listed in `collective.json`).
- Quotes were checked with the same normalisation as `bin/src.py find`: NFKC, typographic quotes to straight ones, soft hyphens removed,
  whitespace collapsed. A quote passes only if every " … " part is found in the plain or the layout text.
- Table values were parsed by code: HTML tables (UNEP data centre, NASA, NOAA, BLS, Census), PDF layout text (DFO Table 3 and A2-1,
  Maine DMR), CSV (FRED, Treasury) and JSON (BLS API). Each point stores the row it came from, and each row was found again.
- Jev was called only through `Tools/jev.py` (`call()`), which finds the key itself; no key was printed or stored. Answers are cached
  by the sha256 of the exact request in the role's `jev/cache/`.
- Wikidata: `wbsearchentities` for each name, then `wbgetentities` for labels, descriptions, P31, P18, P154 and P41 (cached by URL).
  `Q131537827` ("Roy Gardner", employer Psycle Interactive) was rejected as a different person from the economist co-author.
- Secondary sources are marked as such: RTÉ for the October 2021 holdouts and the $150 billion estimate, CNBC for Fitch's 2023
  wording (Fitch's own page returned no text to a script), and the cdli.ca transcription of the 1992 federal release.

## 13. Sources

| Id | Title | Publisher | Published | Read | Standing |
| --- | --- | --- | --- | --- | --- |
| `unep-mp` | [The Montreal Protocol on Substances that Deplete the Ozone Layer (treaty page)](https://ozone.unep.org/treaties/montreal-protocol) | Ozone Secretariat, UNEP | undated page | 2026-11-04 | primary |
| `untc-mp` | [Status of treaties, chapter XXVII 2.a: Montreal Protocol on Substances that Deplete the Ozone Layer (status as at 04-11-2026)](https://treaties.un.org/Pages/ViewDetails.aspx?src=TREATY&mtdsg_no=XXVII-2-a&chapter=27&clang=_en) | United Nations Treaty Collection | 2026-11-04 (status page) | 2026-11-04 | primary |
| `mp-art4` | [Montreal Protocol, Article 4: Control of trade with non-Parties (Handbook)](https://ozone.unep.org/treaties/montreal-protocol/articles/article-4-control-trade-non-parties) | Ozone Secretariat, UNEP | treaty text as amended | 2026-11-05 | primary |
| `mp-art5` | [Montreal Protocol, Article 5: Special situation of developing countries (Handbook)](https://ozone.unep.org/treaties/montreal-protocol/articles/article-5-special-situation-developing-countries) | Ozone Secretariat, UNEP | treaty text as amended | 2026-11-05 | primary |
| `mp-art10` | [Montreal Protocol, Article 10: Financial mechanism (Handbook)](https://ozone.unep.org/treaties/montreal-protocol/articles/article-10-financial-mechanism) | Ozone Secretariat, UNEP | treaty text as amended | 2026-11-05 | primary |
| `mlf-home` | [Multilateral Fund for the Implementation of the Montreal Protocol (home page)](https://www.multilateralfund.org/) | Multilateral Fund Secretariat | page read 2026-11-04 | 2026-11-04 | primary |
| `mlf-history` | [Multilateral Fund: History](https://www.multilateralfund.org/about/history) | Multilateral Fund Secretariat | undated page | 2026-11-05 | primary |
| `ozone-data` | [Data centre: consumption of controlled substances, all Parties, by Annex group, 1986-2025 (ODP tonnes; database last updated 2 Nov 2026)](https://ozone.unep.org/countries/data-table?report_type=0&party_grouping=total&group_by=group&output_type=odp-CO2e-tonnes&period_start=1986&period_end=2025&baseline=1&group%5B%5D=1&group%5B%5D=2&group%5B%5D=3&group%5B%5D=4&group%5B%5D=5&group%5B%5D=6&group%5B%5D=7&group%5B%5D=8&group%5B%5D=9&form_id=ozone_country_data_form__report_table_form&op=GENERATE+REPORT) | Ozone Secretariat, UNEP | 2026-11-02 (database update) | 2026-11-04 | primary |
| `nasa-ozone` | [NASA Ozone Watch: annual records (Southern Hemisphere ozone hole area and minimum ozone)](https://ozonewatch.gsfc.nasa.gov/meteorology/annual_data.html) | NASA Goddard Space Flight Center | last updated 2026-11-03 | 2026-11-04 | primary |
| `noaa-odgi` | [The NOAA Ozone Depleting Gas Index (ODGI), 2024 update](https://gml.noaa.gov/odgi/) | NOAA Global Monitoring Laboratory | 2024 update (page read 2026-11-05) | 2026-11-05 | primary |
| `wmo-2023` | [Ozone layer recovery is on track, helping avoid global warming by 0.5°C (press release, 9 January 2023)](https://wmo.int/news/media-centre/ozone-layer-recovery-track-helping-avoid-global-warming-05degc) | World Meteorological Organization / UNEP | 2023-01-09 | 2026-11-04 | primary |
| `molina-1974` | [Molina & Rowland, Stratospheric sink for chlorofluoromethanes: chlorine atom-catalysed destruction of ozone, Nature 249:810-812](https://www.nature.com/articles/249810a0) | Nature | 1974-06-28 | 2026-11-05 | primary |
| `farman-1985` | [Farman, Gardiner & Shanklin, Large losses of total ozone in Antarctica reveal seasonal ClOx/NOx interaction, Nature 315:207-210](https://www.nature.com/articles/315207a0) | Nature | 1985-05-01 | 2026-11-05 | primary |
| `nobel-1995` | [The Nobel Prize in Chemistry 1995: press release](https://www.nobelprize.org/prizes/chemistry/1995/press-release/) | Royal Swedish Academy of Sciences | 1995-10-11 | 2026-11-05 | primary |
| `dfo-1992` | [Crosbie announces first steps in Northern Cod Recovery Plan (press release, 2 July 1992; transcription)](https://www.cdli.ca/cod/announce.htm) | Fisheries and Oceans Canada (transcribed by the Centre for Distance Learning and Innovation, cdli.ca) | 1992-07-02 | 2026-11-05 | primary (transcribed) |
| `fopo-2005` | [Northern Cod: A Failure of Canadian Fisheries Management (committee news release, 25 November 2005)](https://www.ourcommons.ca/DocumentViewer/en/38-1/FOPO/news-release/2142311) | House of Commons of Canada, Standing Committee on Fisheries and Oceans | 2005-11-25 | 2026-11-05 | primary |
| `dfo-resdoc-2018` | [Brattey et al., Assessment of the Northern Cod (Gadus morhua) stock in NAFO Divisions 2J3KL in 2016, CSAS Research Document 2018/018](https://publications.gc.ca/collections/collection_2018/mpo-dfo/fs70-5/Fs70-5-2018-018-eng.pdf) | Fisheries and Oceans Canada, Canadian Science Advisory Secretariat | 2018-03 | 2026-11-05 | primary |
| `dfo-sar-2025` | [NAFO Divisions 2J3KL Northern cod (Gadus morhua) stock assessment to 2025, CSAS Science Advisory Report 2025/043](https://publications.gc.ca/collections/collection_2025/mpo-dfo/fs70-6/Fs70-6-2025-043-eng.pdf) | Fisheries and Oceans Canada, Canadian Science Advisory Secretariat | 2025-09 | 2026-11-05 | primary |
| `dfo-2024` | [The Government of Canada announces the historic return of the commercial Northern cod fishery (news release, 26 June 2024)](https://www.canada.ca/en/fisheries-oceans/news/2024/06/the-government-of-canada-announces-the-historic-return-of-the-commercial-northern-cod-fishery-in-newfoundland-and-labrador.html) | Fisheries and Oceans Canada | 2024-06-26 | 2026-11-05 | primary |
| `dfo-2025` | [Canada announces a sustainable increase in Northern cod TAC (news release, 18 June 2025)](https://www.canada.ca/en/fisheries-oceans/news/2025/06/contributing-to-a-stronger-economy-for-newfoundland-and-labrador-canada-announces-a-sustainable-increase-in-northern-cod-tac.html) | Fisheries and Oceans Canada | 2025-06-18 | 2026-11-05 | primary |
| `dfo-2026` | [Northern cod quota to increase 55 per cent: Minister Thompson announces 2026 management decisions (news release, 12 June 2026)](https://www.canada.ca/en/fisheries-oceans/news/2026/06/northern-cod-quota-to-increase-55-per-cent-minister-thompson-announces-2026-management-decisions-for-northern-cod-northern-gulf-cod-and-northeast-a.html) | Fisheries and Oceans Canada | 2026-06-12 | 2026-11-05 | primary |
| `acheson-2011` | [Acheson & Gardner, The evolution of the Maine lobster V-notch practice: cooperation in a prisoner's dilemma game, Ecology and Society 16(1):41](https://ecologyandsociety.org/vol16/iss1/art41/main.html) | Ecology and Society (Resilience Alliance) | 2011 | 2026-11-05 | primary |
| `dmr-landings` | [Historical Maine lobster landings, 1880-2025 (table; 2025 preliminary)](https://www.maine.gov/dmr/sites/maine.gov.dmr/files/inline-files/lobster_table.pdf) | Maine Department of Marine Resources | 2026-02-09 (last update of the 2025 values) | 2026-11-05 | primary |
| `dmr-page` | [Historical Maine Fisheries Landings Data (page)](https://www.maine.gov/dmr/fisheries/commercial/landings-program/historical-data) | Maine Department of Marine Resources | page read 2026-11-05 | 2026-11-05 | primary |
| `asmfc-2025` | [American Lobster Stock Assessment Overview (October 2025)](https://asmfc.org/wp-content/uploads/2025/11/AmericanLobsterStockAssmtOverview_Oct2025.pdf) | Atlantic States Marine Fisheries Commission | 2025-10 | 2026-11-05 | primary |
| `nobel-2009` | [The Prize in Economic Sciences 2009: press release](https://www.nobelprize.org/prizes/economic-sciences/2009/press-release/) | Royal Swedish Academy of Sciences | 2009-10-12 | 2026-11-05 | primary |
| `ostrom-2009` | [Elinor Ostrom, Beyond Markets and States: Polycentric Governance of Complex Economic Systems (Prize Lecture, 8 December 2009)](https://www.nobelprize.org/uploads/2018/06/ostrom_lecture.pdf) | The Nobel Foundation | 2009-12-08 | 2026-11-05 | primary |
| `eia-steo-2020-03` | [Short-Term Energy Outlook, March 2020](https://www.eia.gov/outlooks/steo/archives/mar20.pdf) | U.S. Energy Information Administration | 2020-03-11 | 2026-11-05 | primary |
| `eia-steo-2020-04` | [Short-Term Energy Outlook, April 2020](https://www.eia.gov/outlooks/steo/archives/apr20.pdf) | U.S. Energy Information Administration | 2020-04-07 | 2026-11-05 | primary |
| `opec-2020-04-12` | [The 10th (Extraordinary) OPEC and non-OPEC Ministerial Meeting concludes (press release, 12 April 2020)](https://www.opec.org/pr-detail/310-12-apr-2020.html) | OPEC | 2020-04-12 | 2026-11-05 | primary |
| `eia-tie-45236` | [OPEC+ agreement to reduce production contributes to global oil market rebalancing (Today in Energy)](https://www.eia.gov/todayinenergy/detail.php?id=45236) | U.S. Energy Information Administration | 2020-10 | 2026-11-05 | primary |
| `opec-2022-10-05` | [33rd OPEC and non-OPEC Ministerial Meeting (press release, 5 October 2022)](https://www.opec.org/pr-detail/73-05-oct-2022.html) | OPEC | 2022-10-05 | 2026-11-05 | primary |
| `opec-2025-03-03` | [Eight OPEC+ countries confirm the gradual return of the 2.2 mb/d voluntary adjustments (press release, 3 March 2025)](https://www.opec.org/pr-detail/518-03-march-2025.html) | OPEC | 2025-03-03 | 2026-11-05 | primary |
| `opec-2026-03-01` | [Eight OPEC+ countries resume the unwinding of the 1.65 mb/d voluntary adjustments (press release, 1 March 2026)](https://www.opec.org/pr-detail/593-1-march-2026.html) | OPEC | 2026-03-01 | 2026-11-05 | primary |
| `eia-tie-67804` | [The UAE leaves OPEC (Today in Energy, id 67804)](https://www.eia.gov/todayinenergy/detail.php?id=67804) | U.S. Energy Information Administration | 2026-05 | 2026-11-05 | primary |
| `eia-tie-43495` | [Low liquidity and limited available storage pushed WTI crude oil futures prices below zero (Today in Energy, 27 April 2020)](https://www.eia.gov/todayinenergy/detail.php?id=43495) | U.S. Energy Information Administration | 2020-04-27 | 2026-11-05 | primary |
| `eia-brent-m` | [Europe Brent spot price FOB, monthly (dollars per barrel)](https://www.eia.gov/dnav/pet/hist/LeafHandler.ashx?n=PET&s=RBRTE&f=M) | U.S. Energy Information Administration | release of 2026-11-04 | 2026-11-05 | primary |
| `eia-wti-d` | [Cushing, OK WTI spot price FOB, daily (dollars per barrel)](https://www.eia.gov/dnav/pet/hist/RWTCD.htm) | U.S. Energy Information Administration | page read 2026-11-05 | 2026-11-05 | primary |
| `fed-svb-review` | [Review of the Federal Reserve's Supervision and Regulation of Silicon Valley Bank (Barr review, 28 April 2023)](https://www.federalreserve.gov/publications/files/svb-review-20230428.pdf) | Board of Governors of the Federal Reserve System | 2023-04-28 | 2026-11-05 | primary |
| `joint-2023-03-12` | [Joint statement by Treasury, Federal Reserve, and FDIC (12 March 2023)](https://www.federalreserve.gov/newsevents/pressreleases/monetary20230312b.htm) | Board of Governors of the Federal Reserve System | 2023-03-12 | 2026-11-05 | primary |
| `fed-btfp` | [Federal Reserve Board announces it will make available additional funding to eligible depository institutions (12 March 2023)](https://www.federalreserve.gov/newsevents/pressreleases/monetary20230312a.htm) | Board of Governors of the Federal Reserve System | 2023-03-12 | 2026-11-05 | primary |
| `fdic-2023-03-26` | [First-Citizens Bank & Trust Company, Raleigh, NC, to assume all deposits and loans of Silicon Valley Bridge Bank (PR-23-2023, 26 March 2023)](https://www.fdic.gov/news/press-releases/2023/pr23023.html) | Federal Deposit Insurance Corporation | 2023-03-26 | 2026-11-05 | primary |
| `fdic-failed-svb` | [Failed bank information: Silicon Valley Bank, Santa Clara, CA](https://www.fdic.gov/resources/resolutions/bank-failures/failed-bank-list/silicon-valley.html) | Federal Deposit Insurance Corporation | page read 2026-11-05 | 2026-11-05 | primary |
| `fdic-flight-2026` | [Chu et al., Dissecting Depositor Flight: An Analysis of the Spring 2023 Bank Failures (FDIC Staff Studies, May 2026)](https://www.fdic.gov/center-financial-research/staff-studies-may-2026.pdf) | Federal Deposit Insurance Corporation | 2026-05 | 2026-11-05 | primary |
| `nobel-2022` | [The Prize in Economic Sciences 2022: press release](https://www.nobelprize.org/prizes/economic-sciences/2022/press-release/) | Royal Swedish Academy of Sciences | 2022-10-10 | 2026-11-05 | primary |
| `fred-wlcflpcl` | [Assets: Liquidity and Credit Facilities: Loans: Primary Credit: Wednesday Level (WLCFLPCL), millions of dollars, weekly (H.4.1 via FRED)](https://fred.stlouisfed.org/graph/fredgraph.csv?id=WLCFLPCL) | Board of Governors of the Federal Reserve System, via FRED (Federal Reserve Bank of St. Louis) | updated 2026-10-29 | 2026-11-05 | primary |
| `fred-small` | [Deposits, Small Domestically Chartered Commercial Banks (DPSSCBW027SBOG), billions of dollars, SA, weekly (H.8 via FRED)](https://fred.stlouisfed.org/graph/fredgraph.csv?id=DPSSCBW027SBOG) | Board of Governors of the Federal Reserve System, via FRED | updated 2026-10-30 | 2026-11-05 | primary |
| `fred-large` | [Deposits, Large Domestically Chartered Commercial Banks (DPSLCBW027SBOG), billions of dollars, SA, weekly (H.8 via FRED)](https://fred.stlouisfed.org/graph/fredgraph.csv?id=DPSLCBW027SBOG) | Board of Governors of the Federal Reserve System, via FRED | read 2026-11-05 | 2026-11-05 | primary |
| `bls-monthly-2023` | [Work stoppages: detailed monthly listing (archived 2024-03-04)](https://web.archive.org/web/20240304212659id_/https://www.bls.gov/web/wkstp/monthly-listing.htm) | U.S. Bureau of Labor Statistics | 2024-03 | 2026-11-05 | primary |
| `bls-annual` | [Annual work stoppages involving 1,000 or more workers, 1947-2025 (table; archived 2026-02-27)](https://web.archive.org/web/20260227115109id_/https://www.bls.gov/web/wkstp/annual-listing.htm) | U.S. Bureau of Labor Statistics | 2026-02 (2025 row) | 2026-11-05 | primary |
| `bls-wkstp-2024` | [Major Work Stoppages in 2023 (USDL-24-0348, 21 February 2024)](https://web.archive.org/web/20240721103904id_/https://www.bls.gov/news.release/archives/wkstp_02212024.htm) | U.S. Bureau of Labor Statistics | 2024-02-21 | 2026-11-05 | primary |
| `bls-wkstp-2026` | [Major Work Stoppages in 2025 (USDL-26-0232, 20 February 2026)](https://web.archive.org/web/20260301012641id_/https://www.bls.gov/news.release/wkstp.nr0.htm) | U.S. Bureau of Labor Statistics | 2026-02-20 | 2026-11-05 | primary |
| `bls-empsit-2023-10` | [The Employment Situation, October 2023 (archived release of 3 November 2023)](https://web.archive.org/web/20231105024849id_/https://www.bls.gov/news.release/archives/empsit_11032023.htm) | U.S. Bureau of Labor Statistics | 2023-11-03 | 2026-11-05 | primary |
| `bls-ces-autos` | [CES3133600101: All employees, motor vehicles and parts, thousands, SA (BLS public API v1)](https://api.bls.gov/publicAPI/v1/timeseries/data/) | U.S. Bureau of Labor Statistics | read 2026-11-05 | 2026-11-05 | primary |
| `uaw-ford-ta` | [UAW reaches tentative agreement on record contract with Ford Motor Company (25 October 2023)](https://uaw.org/uaw-reaches-tentative-agreement-on-record-contract-with-ford-motor-company/) | UAW | 2023-10-25 | 2026-11-05 | primary |
| `uaw-ratify` | [UAW members ratify historic contracts at Ford, GM and Stellantis (20 November 2023)](https://uaw.org/uaw-members-ratify-historic-contracts-at-ford-gm-and-stellantis/) | UAW | 2023-11-20 | 2026-11-05 | primary |
| `ford-2023-11-30` | [Ford provides guidance for full-year 2023 operating results (30 November 2023)](https://www.fromtheroad.ford.com/us/en/articles/2023/ford-provides-guidance-for-full-year-2023-operating-results--cfo) | Ford Motor Company | 2023-11-30 | 2026-11-05 | primary |
| `house-2011-690` | [Roll call 690, S. 365 (Budget Control Act of 2011), 1 August 2011](https://clerk.house.gov/evs/2011/roll690.xml) | Clerk of the U.S. House of Representatives | 2011-08-01 | 2026-11-05 | primary |
| `plaw-112-25` | [Public Law 112-25, Budget Control Act of 2011](https://www.govinfo.gov/content/pkg/PLAW-112publ25/html/PLAW-112publ25.htm) | U.S. Government Publishing Office | 2011-08-02 | 2026-11-05 | primary |
| `fed-2011-08-05` | [Agencies issue guidance on federal debt (joint press release, 5 August 2011)](https://www.federalreserve.gov/newsevents/pressreleases/bcreg20110805a.htm) | Board of Governors of the Federal Reserve System | 2011-08-05 | 2026-11-05 | primary |
| `gao-12-701` | [GAO-12-701, Debt Limit: Analysis of 2011-2012 Actions Taken and Effect of Delayed Increase on Borrowing Costs (July 2012; report page)](https://web.archive.org/web/20260926000433id_/https://www.gao.gov/products/gao-12-701) | U.S. Government Accountability Office | 2012-07 | 2026-11-05 | primary |
| `house-2023-243` | [Roll call 243, H.R. 3746 (Fiscal Responsibility Act), 31 May 2023](https://clerk.house.gov/evs/2023/roll243.xml) | Clerk of the U.S. House of Representatives | 2023-05-31 | 2026-11-05 | primary |
| `plaw-118-5` | [Public Law 118-5, Fiscal Responsibility Act of 2023](https://www.govinfo.gov/content/pkg/PLAW-118publ5/html/PLAW-118publ5.htm) | U.S. Government Publishing Office | 2023-06-03 | 2026-11-05 | primary |
| `treasury-bills-2023` | [Daily Treasury bill rates, 2023 (CSV)](https://home.treasury.gov/resource-center/data-chart-center/interest-rates/daily-treasury-rates.csv/2023/all?type=daily_treasury_bill_rates&field_tdr_date_value=2023&page&_format=csv) | U.S. Department of the Treasury | read 2026-11-05 | 2026-11-05 | primary |
| `treasury-jy1665` | [Statement by Secretary of the Treasury Janet L. Yellen (1 August 2023, JY-1665)](https://fraser.stlouisfed.org/files/docs/publications/treaspr/2023/2023-08-01_jy1665.pdf) | U.S. Department of the Treasury (via FRASER, Federal Reserve Bank of St. Louis) | 2023-08-01 | 2026-11-05 | primary |
| `cnbc-fitch-2023` | [Fitch downgrades U.S. long-term rating to AA+ from AAA (1 August 2023)](https://www.cnbc.com/2023/08/01/fitch-downgrades-us-long-term-ratings-to-aa-from-aaa.html) | CNBC | 2023-08-01 | 2026-11-05 | secondary |
| `treasury-debt-limit` | [Debt limit (policy page)](https://home.treasury.gov/policy-issues/financial-markets-financial-institutions-and-fiscal-service/debt-limit) | U.S. Department of the Treasury | page read 2026-11-05 | 2026-11-05 | primary |
| `gao-25-107089` | [GAO-25-107089, Debt Limit: Statutory Changes Could Avert the Risk of a Government Default (December 2024)](https://files.gao.gov/reports/GAO-25-107089/index.html) | U.S. Government Accountability Office | 2024-12-11 | 2026-11-05 | primary |
| `gao-26-107872` | [GAO-26-107872, Debt Limit: Prolonged Negotiations Increase Taxpayer Costs and Disrupt Financial Markets (25 March 2026)](https://files.gao.gov/reports/GAO-26-107872/index.html) | U.S. Government Accountability Office | 2026-03-25 | 2026-11-05 | primary |
| `oecd-2021-orig` | [Statement on a Two-Pillar Solution to Address the Tax Challenges Arising from the Digitalisation of the Economy, 8 October 2021 (copy archived 11 October 2021)](https://web.archive.org/web/20211011225942id_/https://www.oecd.org/tax/beps/statement-on-a-two-pillar-solution-to-address-the-tax-challenges-arising-from-the-digitalisation-of-the-economy-october-2021.pdf) | OECD/G20 Inclusive Framework on BEPS | 2021-10-08 | 2026-11-05 | primary |
| `oecd-2021-now` | [Statement on a Two-Pillar Solution (8 October 2021; current copy, signatories as of 9 June 2023)](https://www.oecd.org/content/dam/oecd/en/topics/policy-issues/beps/statement-on-a-two-pillar-solution-to-address-the-tax-challenges-arising-from-the-digitalisation-of-the-economy-october-2021.pdf) | OECD/G20 Inclusive Framework on BEPS | 2021-10-08 (signatory note 2023-06-09) | 2026-11-05 | primary |
| `rte-2021-10-08` | [136 out of 140 countries join OECD global tax deal (8 October 2021)](https://www.rte.ie/news/2021/1008/1252439-corporate-tax-reform/) | RTÉ News | 2021-10-08 | 2026-11-05 | secondary |
| `wh-2025-01-20` | [Memorandum on the Organization for Economic Co-operation and Development (OECD) Global Tax Deal (20 January 2025), DCPD-202500150](https://www.govinfo.gov/content/pkg/DCPD-202500150/html/DCPD-202500150.htm) | Compilation of Presidential Documents, U.S. Government Publishing Office | 2025-01-20 | 2026-11-05 | primary |
| `oecd-cts-2025` | [Corporate tax revenues rise as global corporate tax rates continue to stabilise, new OECD data show (press release, 25 November 2025; archived copy)](https://web.archive.org/web/20261016110240id_/https://www.oecd.org/en/about/news/press-releases/2025/11/corporate-tax-revenues-rise-as-global-corporate-tax-rates-continue-to-stabilise-new-oecd-data-show.html) | OECD | 2025-11-25 | 2026-11-05 | primary |
| `oecd-sg-2026-04` | [OECD Secretary-General Tax Report to G20 Finance Ministers and Central Bank Governors (April 2026)](https://www.oecd.org/content/dam/oecd/en/publications/reports/2026/04/oecd-secretary-general-tax-report-to-g20-finance-ministers-and-central-bank-governors-g20-united-states-april-2026_c41601f7/02d05307-en.pdf) | OECD | 2026-04 | 2026-11-05 | primary |
| `oecd-eia-2026` | [Economic Impact Assessment of the Global Minimum Tax: 2026 update (webinar slides, 15 July 2026)](https://www.oecd.org/content/dam/oecd/en/events/2026/07/economic-impact-assessment-2026/eia-2026-update-webinar-presentation-en.pdf) | OECD | 2026-07-15 | 2026-11-05 | primary |
| `piie-chart` | [Chad P. Bown, US-China Trade War Tariffs: An Up-to-Date Chart (description as of 22 August 2026)](https://www.piie.com/research/piie-charts/2019/us-china-trade-war-tariffs-date-chart) | Peterson Institute for International Economics | updated to 2026-08-22 | 2026-11-05 | primary |
| `piie-phase1` | [Chad P. Bown, China bought none of the extra $200 billion of US exports in Trump's trade deal (19 July 2022)](https://www.piie.com/blogs/realtime-economics/2022/china-bought-none-extra-200-billion-us-exports-trumps-trade-deal) | Peterson Institute for International Economics | 2022-07-19 | 2026-11-05 | primary |
| `census-china` | [Trade in goods with China (c5700), 1985-2026](https://www.census.gov/foreign-trade/balance/c5700.html) | U.S. Census Bureau | read 2026-11-05 | 2026-11-05 | primary |

## 14. Open questions for the design

- Nine cases are more than one scene can hold. A first cut of five covers the main games: ozone (public good, solved), cod against lobster (commons lost and kept, drawn side by side), OPEC+ 2020 (cartel and punishment), SVB (run) and the US-China tariffs (iterated dilemma, already in the data). Which does the design keep?
- The timeline's 'coded as' labels (cooperate, defect, punish, swerve, escalate) are this research's reading, not the sources'. May the scene colour moves by them, with that stated?
- The cause of the cod collapse is disputed: the 1992 federal release says 'primarily' ecological factors, the 2005 Commons committee says overfishing from mismanagement. Show both, or only the committee's later finding?
- Who broke OPEC+ on 6 March 2020 is not stated in the cached primary sources (EIA says only that the meeting ended without agreement). Leave it unattributed, or fetch a primary source that names a side?
- The CEOs of GM, Ford and Stellantis, the Presidents and Speakers of 2011 and 2023, and the 2025 tariff negotiators are public figures with Wikidata items, but the cached sources do not name their roles; their offices come from Wikidata descriptions. Draw them, or only the named actors (Fain, Yellen, Crosbie, Abdulaziz bin Salman, Novak, Trump, Xi, Cormann, Donohoe)?
- Fitch's 2023 wording comes from CNBC because Fitch's page returned no text to a script. Is a secondary source acceptable here, or should that fact stay off screen?
- The Russia sanctions coalition and the oil price cap (global.json) are a tenth collective case already gated in the repository. Add it to the scene's set?
- PIIE's tariff averages are statutory rates on fixed 2017 trade, not duties paid, and Census trade values do not capture rerouting. Is a caption enough?

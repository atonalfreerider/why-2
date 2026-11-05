# Mobility evidence for the holistic scene

Research for the holistic mobility scene (role research:mobility, holistic views workflow, 4-5 Nov 2026). On 4 Nov the
user asked for "holistic view states to show economic mobility ... with real world examples" and for images of
individuals and collectives. This file collects the real-world evidence such a scene can draw on. Each number has its
source, the exact passage that carries it, and the date it was read. The same 37 facts, machine-readable, are in
[`mobility.json`](mobility.json); fact ids are given in brackets below. Each fact has `id`, `claim`, `value`, `unit`,
`scope`, `quote`, `url`, `accessed`, `notes`, `source`, `page` and `check`, and public figures also carry `wikidata`,
`trajectory` and `approval`.

**What is approved.** Oprah Winfrey is the approved real life. Every other person below is a proposal for the author. Each
is a public figure with a Wikidata item, and every QID was read from the Wikidata API on 5 Nov 2026.

**How it was checked** (details in section 8):

- **Fresh copies.** Every source was downloaded on 5 Nov 2026. The exception is the Forbes list feeds, which the lives
  build cached on 3 Nov.
- **Verbatim quotes.** Every quote was found word for word in that copy.
- **Values checked in code.** Every value read from a table or feed was compared exactly with the file.
- **Jev checks.** Every claim taken from prose was also put to Jev with the project's 2026-gate question. All 31 such
  claims are supported at 0.78 or more.

Nothing here is dated 2026 except two Forbes 2026 list values, flagged `gate2026`.

## 1. Across generations in the United States

Chetty, Hendren, Kline and Saez (2014, *Quarterly Journal of Economics*) link 9.9 million children born in 1980-82 to their
parents through tax records. Parent income is averaged over 1996-2000 and the child's over 2011-12, at about age 30.

- **7.5% of children raised in the bottom fifth reach the top fifth** [`chk2014-us-q1-to-q5`]:
  "This probability is 7.5% in the U.S., compared with 11.7% in Denmark (Boserup, Kopczuk and Kreiner 2013) and 13.4% in
  Canada (Corak and Heisz 1999)." If parents' income did not matter, every cell of the matrix below would be 20%.
- **The national transition matrix**, Table II [`chk2014-transition-matrix`]. Rows are the parents' fifth and columns the
  child's fifth; the printed table is the transpose.

  | Parents in | child bottom | 2nd | middle | 4th | child top |
  | --- | --- | --- | --- | --- | --- |
  | bottom fifth | **33.7** | 28.0 | 18.4 | 12.3 | **7.5** |
  | 2nd | 24.2 | 24.2 | 21.7 | 17.6 | 12.3 |
  | middle | 17.8 | 19.8 | 22.1 | 22.0 | 18.3 |
  | 4th | 13.4 | 16.0 | 20.9 | 24.4 | 25.4 |
  | top fifth | **10.9** | 11.9 | 17.0 | 23.6 | **36.5** |

- **The rank-rank slope is 0.341** [`chk2014-rank-rank-slope`]: "a one percentage point (pp) increase in parent rank is
  associated with a 0.341 pp increase in the child's mean rank". In Denmark the slope is 0.180 and in Canada 0.174,
  "nearly half that in the U.S." [`chk2014-rank-rank-slope-denmark-canada`].
- **Place matters** [`chk2014-charlotte-sanjose`, `chk2014-cz-table`]: "the probability that a child reaches the top
  quintile of the national income distribution starting from a family in the bottom quintile is 4.4% in Charlotte but
  12.9% in San Jose." The figures below are from Table III, which uses children born 1980-85, for some of the 50 largest
  commuting zones:

  | Place | bottom fifth to top fifth | | Place | bottom fifth to top fifth |
  | --- | --- | --- | --- | --- |
  | San Jose | 12.9% | | Houston | 9.3% |
  | San Francisco | 12.2% | | Chicago | 6.5% |
  | Washington DC | 11.0% | | Detroit | 5.5% |
  | Seattle | 10.9% | | Milwaukee | 4.5% |
  | Salt Lake City | 10.8% | | Atlanta | 4.5% |
  | Boston, New York | 10.5% | | Charlotte | 4.4% |
  | Los Angeles | 9.6% | | | |

- **Where a low-income child ends up** [`chk2014-absolute-upward-range`]. Take children whose parents were at the 25th
  percentile ($28,800). Their mean adult rank "ranges from 35.8 in Charlotte to 46.2 in Salt Lake City among the 50
  largest CZs".

## 2. Absolute mobility: earning more than your parents

Chetty, Grusky, Hell, Hendren, Manduca and Narang (2017, *Science*; NBER WP 22910) compare each child's real household
income at 30 with their parents' income at the same age.

- **92% of children born in 1940 out-earned their parents, but only 50% of those born in 1984**
  [`cghhmn2017-absolute-1940-1984`]: "the fraction of children earning more than their parents fell from 92% in the 1940
  birth cohort to 50% in the 1984 birth cohort." The abstract rounds the 1940 figure to "approximately 90%".
- **Every cohort** [`cghhmn2017-absolute-by-cohort`], from the authors' Online Data Table 1 (`cohort_mean`, every cohort
  1940-1984 is in the file):

  | Born | 1940 | 1945 | 1950 | 1955 | 1960 | 1965 | 1970 | 1975 | 1980 | 1984 |
  | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
  | Out-earned parents | 91.5% | 86.4% | 78.5% | 69.6% | 62.3% | 59.3% | 61.0% | 58.6% | 50.0% | 50.3% |

- **The middle fell most** [`cghhmn2017-absolute-middle`]. "Rates of absolute mobility fell the most for children with
  parents in the middle class." With parents at the 50th percentile, the share fell from 92.6% (1940) to 46.1% (1984).
  At the 10th percentile it fell from 93.5% to 69.6%.

## 3. Race

Chetty, Hendren, Jones and Porter (2018 working paper; *QJE* 2020) follow 20 million children born in 1978-83. Their
household income is measured in 2014-15, at ages 31-37.

- **From the bottom fifth to the top fifth: 10.6% of white children, 2.5% of Black children** [`chjp2018-q1-to-q5-by-race`]:
  "among children with parents in the bottom quintile, 10.6% of white children rise up to the top quintile, but only 2.5%
  of black children do".
- **For Black children, the top fifth does not hold** [`chjp2018-q5-persistence-by-race`]: "41.1% of white children remain
  in the top quintile, compared with 18.0% of black children ... black children starting from families in the top quintile
  have nearly the same chances of falling to the bottom income quintile (16.7%) as they do of staying in the top
  quintile."
- **Even in the same neighbourhood** [`chjp2018-tracts`]: "Controlling for parental income, black boys have lower incomes in
  adulthood than white boys in 99% of Census tracts." The gap is driven by men. Black and white women raised at the same
  parental income have similar individual incomes.
- **Since then, the race gap has narrowed while class gaps grew** [`cdgpy2024-class-and-race-trends`]. Chetty, Dobbie,
  Goldman, Porter and Yang (NBER WP 32697, July 2024, revised December 2025) write: "Between the 1978 and 1992 birth
  cohorts, earnings increased for white children from high-income families relative to white children from low-income
  families, increasing earnings gaps by parental income (“class”) by 30%. Earnings increased for Black children at all
  parental income levels, reducing white-Black earnings gaps for children from low-income families by 30%." It is a
  working paper, not yet peer reviewed.

## 4. Across countries

- **Generations to reach the mean.** OECD (2018), *A Broken Social Elevator?* [`oecd2018-generations-average`]: "in an
  “average OECD country” it would take around four to five generations for children from the bottom earnings decile to
  attain the level of mean earnings." The country values come from the data file behind Figure 1.5 (StatLink
  10.1787/888933761910) [`oecd2018-generations-by-country`]:

  | Generations | Countries |
  | --- | --- |
  | 2 | Denmark |
  | 3 | Norway, Finland, Sweden |
  | 4 | Spain, New Zealand, Canada, Greece, Belgium, Australia, Japan, Netherlands |
  | 4.5 | OECD24 average |
  | 5 | Portugal, Ireland, Korea, **United States**, United Kingdom, Italy, Switzerland, Austria |
  | 6 | France, Germany, Chile, Argentina |
  | 7 | Hungary, India, China |
  | 9 | Brazil, South Africa |
  | 11 | Colombia |

  The OECD calls these estimates "simulation-based and intended to be illustrative": they assume that father-son earnings
  elasticities stay constant. **One OECD sentence must not be quoted.** The overview's prose (printed p.26) says the Nordic
  countries take "at least four generations". That contradicts the figure and the report's own executive summary, which
  says "only about two generations".
- **The Great Gatsby Curve.** Alan Krueger, chairman of the Council of Economic Advisers, named it on 12 January 2012
  [`krueger2012-great-gatsby-curve`]: "I call this the “Great Gatsby Curve.”" He also projected that "The IGE for the U.S.
  is predicted to rise from .47 to .56." This is a rough forecast, not a measurement. Later work found the U.S. rank-rank
  slope roughly flat for cohorts born 1971-1993 (`chettyEtAl2014Trend` in `Tools/history/sources.json`).
- **Both axes from one source** [`oecd2018-gatsby-data`]. The data behind OECD Figure 1.13 (StatLink
  10.1787/888933762062) gives, for 30 countries and the OECD24 average, the earnings mobility (1 minus the father-son
  elasticity) and the Gini of the mid-1980s to early 1990s. For example:

  | Country | Mobility | Gini |
  | --- | --- | --- |
  | Denmark | 0.879 | 0.222 |
  | United States | 0.590 | 0.350 |
  | Brazil | 0.340 | 0.599 |
  | South Africa | 0.324 | 0.605 |
  | Colombia | 0.240 | 0.553 |

  The OECD finds "no countries which combine high inequality with high mobility".
- **Corak's elasticities**, the data Krueger used [`corak-ige-by-country`]. They run from Denmark .15, Norway .17, Finland
  .18 and Canada .19, through Germany .32, Japan .34, France .41 and **United States .47**, to United Kingdom .50, Italy .50,
  Chile .52, Brazil .58, China .60 and Peru .67. In plain words [`corak2013-fifth-vs-half`]: in Finland, Norway and Denmark
  "less than one-fifth of any economic advantage or disadvantage that a father may have had in his time is passed on to a
  son", and in Italy, the United Kingdom and the United States "roughly 50 percent". The OECD's U.S. elasticity (0.41)
  differs from Corak's 0.47, because the two draw on different studies. Never mix the two series in one picture.

## 5. Within one working life: a decade

Four sources span about ten years. Their income definitions and samples differ, so compare like with like.

| Source | Years | Data | Bottom fifth stays | Bottom fifth reaches top | Top fifth stays |
| --- | --- | --- | --- | --- | --- |
| Hungerford, CRS RL34434 [`hungerford2008-psid-1990-1999`] | 1990-1999 | PSID, after-tax family income, size-adjusted | 53.2% | 2.8% | 53.8% |
| U.S. Treasury [`treasury2007-table1`] | 1996-2005 | tax returns, filers aged 25+ | 42.4% | 5.3% | 69.4% |
| Bradbury, Boston Fed [`bradbury2011-psid-1995-2005`] | 1995-2005 | PSID, after-tax, size-adjusted | 58.1% (41.9% moved up) | n/a | 55.0% (45.0% moved down) |
| Carroll and Chen, Cleveland Fed [`carrollchen2016-psid-2003-2013`] | 2003-2013 | PSID, male-headed households, heads aged 18-65 | 64% | 1% | 72% |

- **Treasury, 1996-2005** [`treasury2007-bottom-moved-up`]: "About half (58 percent by one measure and 45 percent by
  another measure) of those in the bottom income quintile in 1996 moved to a higher income group by 2005." The very top
  churns [`treasury2007-top-persistence`]: "Only about 25 percent of the individuals in the top 1/100th percent in 1996
  remained in the top 1/100th percent in 2005."
- **Cleveland Fed, 2003-2013**: "about 64 percent of households that were in the bottom quintile in 2003 were there in
  2013. Meanwhile, 1 percent of them had moved up to the top quintile." This is the most recent decade here, and it spans
  the Great Recession.

The economy view's mobility panel already checks its simulated population against the Treasury panel.
`Tools/history/sources.json` holds the full matrices for Treasury, Auten-Gee, Hungerford 1980s and Census SIPP
2009-2012, and the base rates of the events behind ordinary falls (`events`).

## 6. Public figures: rose, held, fell

Forbes values come from Forbes's own list data, in the feeds cached by the lives build (`Tools/lives/.cache/forbes/`).
Each value is quoted verbatim from the feed. Forbes figures are estimates.

| Person (Wikidata) | Trajectory | Evidence (fact ids) | Status |
| --- | --- | --- | --- |
| Oprah Winfrey (Q55800) | rose, then held | Born 29 Jan 1954 in Kosciusko, Mississippi. Her mother moved to Milwaukee "in search of better economic opportunities" (Mississippi Encyclopedia) [`figure-winfrey-origin`]. Forbes: $1.3B (2005), $3.0B (2025), $3.2B (2026, gated) [`figure-winfrey-forbes`] | **approved** |
| Jay-Z, Shawn Carter (Q62766) | rose | Forbes, 3 Jun 2019: his fortune "conservatively totals $1 billion ... the first hip-hop artist to do so", and its "start: Brooklyn’s notorious Marcy housing projects" [`figure-jayz-origin`]. World's Billionaires: $1.0B (2020), $2.5B (2025), $2.8B (2026, gated) [`figure-jayz-forbes`] | proposal |
| Andrew Carnegie (Q484265), 1835-1919 | rose | Bobbin boy "at one dollar and twenty cents per week" (*Autobiography*, 1920) [`figure-carnegie-start`]. "sold his steel company to J. P. Morgan for $480 million in 1901" (Andrew Carnegie Foundation) [`figure-carnegie-sale`] | proposal |
| Warren Buffett (Q47213) | held | Ranked between 1st and 9th on all 42 Forbes lists, 2005-2025 [`figure-buffett-held`] | proposal |
| William C. Durant (Q381381), 1861-1947 | fell | Founder of General Motors. In February 1936 he "invoked the shelter of the bankrupt court ... and put down as his assets $250 in clothes" (*Washington Daily News*, 15 Feb 1936) [`figure-durant-fell`] | proposal |
| Elizabeth Holmes (Q17503525) | fell | Forbes, 1 Jun 2016: "Today, FORBES is lowering our estimate of her net worth to nothing", down from $4.5B (Forbes 400, 2014) [`figure-holmes-fell`] | proposal; convicted of fraud |
| Sam Bankman-Fried (Q106543540) | fell | Forbes: $24.0B (April 2022). In November 2022 "estimated fortune went from $17 billion to less than $1 billion in days. Forbes figures now it might be closer to zero." (Forbes Australia, 19 Nov 2022) [`figure-bankman-fried-fell`] | proposal; convicted of fraud |

Cautions:

- **Holmes and Bankman-Fried fell through fraud, not misfortune.** Shown alone, they would suggest that falling means
  crime. Durant fell through the stock market, and the 1936 papers say so. Ordinary falls (job loss, illness, divorce)
  are better told by the typical lives and the base rates in `Tools/history/sources.json` (`events`).
- **Durant's assets are disputed.** The *Washington Daily News* says $250 in clothes. The Hendersonville *Times-News*
  (14 Feb 1936) says "$200 worth of wearing apparel" and debts of "about a million". Caption the figure as disputed, or
  as "a few hundred dollars of clothing".
- **No "first" caption for Winfrey.** The Mississippi Encyclopedia calls her "the first black billionaire in the United
  States", but Robert L. Johnson is usually given that place (2001).
- **The 2026 values are gated.** The Winfrey and Jay-Z 2026 values are verbatim in Forbes's feed, and Jev supports both
  (0.91). The gate still asks for the feed to be re-read on the build's as-of date.
- **Old dollars are nominal.** Carnegie's and Durant's dollar figures are nominal 19th- and 20th-century dollars.
- **Images.** Wikidata lists an image (P18) for every person above. The images agent must check the licence of each.
  A photo may be used only for that real person, never for a typical life.

## 7. What a scene can show

These are suggestions for the designers. Every number below is in `mobility.json`.

- **A hundred children from the bottom fifth.** About 34 stay at the bottom and 7 or 8 reach the top nationally
  [`chk2014-transition-matrix`]. In Charlotte about 4 reach the top, in San Jose about 13, in Denmark about 12 and in
  Canada about 13.
- **Born in 1940 vs born in 1984.** A cohort crowd where 9 in 10 climb above their parents' line, against one where only
  1 in 2 do [`cghhmn2017-absolute-by-cohort`].
- **A staircase of generations.** Denmark reaches the mean in 2 steps, the United States in 5 and Colombia in 11
  [`oecd2018-generations-by-country`]. The Gatsby data [`oecd2018-gatsby-data`] can place each country by inequality
  and mobility together.
- **A decade within one life.** The Cleveland Fed matrix [`carrollchen2016-psid-2003-2013`] gives the shares that stay,
  climb or fall between 2003 and 2013. The Treasury's top 0.01% shows that even the very top turns over
  [`treasury2007-top-persistence`].
- **Real lives as dated waypoints.** Winfrey (Kosciusko 1954, then the Forbes lists from 2005), Carnegie ($1.20 a week,
  then $480 million in 1901), Durant (founder of GM, bankrupt in 1936) and Buffett (held at the top). Each needs its own
  image, and every one except Winfrey needs the author's approval.

## 8. How the facts were checked

- **Copies.** Every source was downloaded into `.why-2-build/work/holistic/research-mobility/dl/`, which lies outside the
  repository; `dl/manifest.json` records each URL, retrieval time and sha256. The papers an earlier pass fetched on 4 Nov
  were fetched again on 5 Nov and are byte-identical. Some sources refused the scripted client:
  - oecd.org returns 410 at the report's original address and 403 at its landing page.
  - forbes.com returns 403 to the client.

  Those sources were read in the Internet Archive's copies from the day of publication (OECD: 23 Jul 2018; Forbes:
  1 Jun 2016 and 3 Jun 2019). The OECD figure data comes from the OECD's own StatLink server, reached through the DOIs
  printed under the figures.
- **Quotes.** `scripts/build_facts.py` finds every quote, and every part of a quote joined by ` [...] `, verbatim in the
  text of its copy. It normalizes only whitespace, line-break hyphens, ligatures and typographic quotes. PDF text comes from
  pdftotext. The two-column race summary is checked against its right column, extracted on its own. The 1936 newspapers
  are checked against the Library of Congress OCR.
- **Code checks.** The builder compares exactly:
  - every value read from the Opportunity Insights and OECD spreadsheets, and every bar label of Corak's figure;
  - Buffett's 42 ranks, and the absence of Holmes after March 2016 and Bankman-Fried after September 2022 from every
    later list;
  - every hand-typed matrix against the numbers of its own quote, in order.

  The builder exits without writing if any check fails.
- **Jev.** For each claim from prose, Jev (TypeSafe System One, model `jev-1.14.0`) was asked the project's 2026-gate
  question from `Tools/global/verify.py`: does the fetched text around the quote state or directly imply the claim,
  including its numbers and dates? It was not asked about values already compared in code, following the repository rule
  that exact lookups belong in code. The excerpts follow the gate's rules: 1500 characters before and 2500 after a quote.
  There were 34 requests in all: one per claim, plus three asked again with dated context. The 31 final answers used
  43,687 tokens and are cached by request hash.
  - Every claim is supported at 0.78 or more (stored as `check.jevSupported`). The lowest are Durant 0.78, the
    Denmark/Canada slopes 0.80 and Bankman-Fried 0.84.
  - Three claims first scored below 0.6 (Krueger 0.23, Durant 0.33, Jay-Z 0.61) because the excerpt did not carry the
    date stated in the claim. Each now includes the dated line of its own source, verified verbatim: the speech header,
    the newspaper's running head, and the article's publication metadata.
  - Jev's probability is model evidence, not proof.

## 9. Sources

All accessed 5 Nov 2026 unless noted. Full titles, hashes and notes are in `mobility.json` (`sources`).

- Chetty, Hendren, Kline, Saez (2014), QJE 129(4). Authors' version:
  https://opportunityinsights.org/wp-content/uploads/2018/03/mobility_geo.pdf
- Chetty, Grusky, Hell, Hendren, Manduca, Narang (2017), Science 356(6336); NBER WP 22910:
  https://opportunityinsights.org/wp-content/uploads/2018/03/abs_mobility_paper.pdf. Online Data Table 1:
  https://opportunityinsights.org/wp-content/uploads/2018/04/table1_national_absmob_by_cohort_parpctile.xlsx
- Chetty, Hendren, Jones, Porter (2018 WP; QJE 2020), December 2019 version:
  https://opportunityinsights.org/wp-content/uploads/2018/04/race_paper.pdf. Summary:
  https://opportunityinsights.org/wp-content/uploads/2018/04/race_summary.pdf
- Chetty, Dobbie, Goldman, Porter, Yang, NBER WP 32697 (2024, revised Dec 2025):
  https://www.nber.org/system/files/working_papers/w32697/w32697.pdf
- OECD (2018), *A Broken Social Elevator? How to Promote Social Mobility*, Overview and Main Findings, read in the
  Internet Archive's capture of 23 Jul 2018:
  https://web.archive.org/web/20180723065332/http://www.oecd.org/social/soc/Social-mobility-2018-Overview-MainFindings.pdf.
  Figure 1.5 data: https://statlinks.oecdcode.org/812018151P1G005.XLSX. Figure 1.13 data:
  https://statlinks.oecdcode.org/812018151P1G013.XLSX
- Corak, IZA DP 9929, the published version of the 2013 book chapter: https://docs.iza.org/dp9929.pdf. Corak (2013),
  IZA DP 7520, the *JEP* article's working-paper version: https://docs.iza.org/dp7520.pdf
- Krueger (12 Jan 2012): https://obamawhitehouse.archives.gov/sites/default/files/krueger_cap_speech_final_remarks.pdf
- U.S. Treasury (2007, revised 2008): https://home.treasury.gov/system/files/131/Report-Income-Mobility-2008.pdf
- Hungerford (2008), CRS RL34434:
  https://www.everycrsreport.com/files/20080404_RL34434_be64d1aab381920155131bf341304806355ba3b7.pdf
- Bradbury (2011), Boston Fed WP 11-10:
  https://www.bostonfed.org/-/media/Documents/Workingpapers/PDF/economic/wp/wp2011/wp1110.pdf
- Carroll and Chen (20 Jun 2016), Cleveland Fed Economic Commentary 2016-06:
  https://www.clevelandfed.org/publications/economic-commentary/2016/ec-201606-income-inequality-and-mobility
- Mississippi Encyclopedia, "Winfrey, Oprah": https://mississippiencyclopedia.org/entries/oprah-winfrey/
- Forbes, 3 Jun 2019 (Jay-Z):
  https://web.archive.org/web/20190603173402/https://www.forbes.com/sites/zackomalleygreenburg/2019/06/03/jay-z-billionaire-worth/
- Forbes, 1 Jun 2016 (Holmes):
  https://web.archive.org/web/20160601180515/http://www.forbes.com/sites/matthewherper/2016/06/01/from-4-5-billion-to-nothing-forbes-revises-estimated-net-worth-of-theranos-founder-elizabeth-holmes/
- Forbes Australia, 19 Nov 2022 (Bankman-Fried):
  https://www.forbes.com.au/news/billionaires/sam-bankman-fried-elizabeth-holmes-and-9-other-epic-billionaire-blowups/
- Carnegie, *Autobiography* (1920), Project Gutenberg #17976: https://www.gutenberg.org/cache/epub/17976/pg17976.txt.
  Andrew Carnegie Foundation: https://carnegie.org/about/andrew-carnegie-story/
- *The Washington Daily News*, 15 Feb 1936, p.13: https://www.loc.gov/resource/sn82016181/1936-02-15/ed-1/?sp=13.
  *The Times-News* (Hendersonville), 14 Feb 1936, p.2: https://www.loc.gov/resource/sn86063811/1936-02-14/ed-1/?sp=2
- Forbes list data, cached 3 Nov 2026 by `Tools/lives/build.py`:
  `https://www.forbes.com/forbesapi/person/<list>/<year>/position/true.json`
- Wikidata items (wbgetentities, 5 Nov 2026): Q55800, Q62766, Q484265, Q47213, Q381381, Q17503525, Q106543540

## 10. Gaps and open questions

- **No official 2010s decade matrix.** Census P70-142 covers only 2009-2012. The most recent decade here is the Cleveland
  Fed's 2003-2013, and its sample is male-headed households only.
- **Only two of the fallers are ordinary.** Durant is historical, and Holmes and Bankman-Fried fell through fraud. No
  living U.S. public figure was found whose fall was ordinary (job loss, illness) and documented in a primary source; the
  typical lives cover that ground.
- **Two more rise candidates were not researched.** Howard Schultz (Q472330; Forbes lists 2006-2026 are in the cache) and
  Ursula Burns (Q465663) are other possible "rose" lives. Neither origin story has yet been quoted from a primary source.
- **Some sources refused the client.** SEC EDGAR and pubs.aeaweb.org returned 403, so Xerox's 2009 filing and the *JEP*
  version of Corak (2013) were not read; the IZA version was used.

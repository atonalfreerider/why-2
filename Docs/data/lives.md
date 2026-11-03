# Lives data: real figures' money and visibility (`Data/social/visibility.json`)

The lives view (M3, "Lives that rise, stay or fall"; [MODALITIES.md](../hills-design/MODALITIES.md) section 7.3, work
package WP03 in [MODALITIES-PLAN.md](../hills-design/MODALITIES-PLAN.md)) draws three real public figures above the
synthetic lives: their fortune as a gold line in the magnified top 1% (log scale, "Forbes estimate"), their visibility as a
white line of monthly Wikipedia page views in the attention band (log scale, from July 2015), and their 2025 follower
snapshot as a separate star that is never joined to the page-view line. This file holds that data for every candidate,
and the people the spec's printed rules choose among them.

| | |
| --- | --- |
| Built by | `Tools/lives/build.py` (`python3 -I Tools/lives/build.py`; `--offline` rebuilds byte-identically from the cache; `--refresh` re-downloads the 2026 Forbes feeds first) |
| Hand-read research | `Tools/lives/sources.json` (Forbes values with URL and verbatim quote, rule wording, the revision check's index queries and one override, the author's approval) |
| Cache | `Tools/lives/.cache/` (gitignored): Forbes list feeds, Wikidata, page views, the Wayback Machine's index and archived copies (`wayback/`, gzip), and `manifest.json` with each download's URL, retrieval date and sha256 |
| Read by | `Model/LivesModel.cs` (WP12: applies the rules, the 2016 = 1 indices, "together in k of m years"), `Layers/LivesVisibility.cs` (WP31), `UI/LivesStrip.cs` (WP32) |
| Keys, credentials | none: every source is public and keyless; the User-Agent is `why-2-lives/0.1 (educational economic visualization)` |

## Series

| Series | Source | How it is stored | Use on screen |
| --- | --- | --- | --- |
| Net worth, World's Billionaires (each spring) and Forbes 400 (each fall), 2005-2026 | forbes.com's own list data: the JSON feed its list pages load (`forbesapi/person/<list>/<year>/position/true.json`, fields named in the URL), one per list and year | each value in billions of US dollars (`finalWorth` / 1000) with list, year, month, `listDate`, `entryDate`, rank, URL, the entry's verbatim text (`quote`) and the field; read into `sources.json` and checked on every build against the cached copy of the feed (quote, value, rank, month, entry date) | the gold line, labelled "Forbes estimate"; the World's Billionaires value of each year is the annual series the rules use |
| Forbes's own revisions | the Wayback Machine: its index of captures of Forbes's list data at both addresses forbes.com has used, the older `ajax/list/data` and the newer `forbesapi` feed, and the archived copies themselves | `build.py` compares every candidate's value (and rank, where the copy keeps the list's rank) with today's: in every copy at the older address, and the earliest and latest readable whole-list copy at the newer one. An archived value that differs would be kept as its own value with `current: false`; `visibility.json` `forbesRevisions` lists every copy compared | the panel could show an earlier estimate as a ghost; none was found (below) |
| Monthly page views, Jul 2015 - Oct 2026 | Wikimedia REST API, pageviews per article: en.wikipedia, all-access, user agent, monthly | one array of 136 monthly counts per person (`pageViews.value`, from `start`); a month the source has no row for would be `null` and listed in `missing`, never filled | the white line in the attention band; the calendar-year sums feed the rules and the 2016 = 1 index |
| English Wikipedia article | Wikidata, the item's `enwiki` sitelink (`wbgetentities`) | `wikipedia.value` (title), `url` (the Wikidata item) and `article` | which article's views are counted |
| Followers, 2025 snapshot | `people.json` (`fol`): followers summed across Instagram, TikTok, YouTube and X | `followers.value`, labelled "followers summed across Instagram, TikTok, YouTube and X, 2025 snapshot" | the star at the person's cloud altitude, joined only by a dotted leader ("a different measure") |
| Names for the Forbes search | `people.json` names, Wikidata English labels and aliases, and `forbesNames` in `sources.json` | not written; each name match is confirmed by birth year | finds every US person of `people.json` on any list |

Page views count the article under its current title; views that arrive through a redirect are counted by the source under
the redirect's own title and are not included. No Instagram-only follower list is used anywhere: the only follower figure
is `people.json`'s four-platform 2025 snapshot.

**Dates.** `entryDate` is the UTC date of the entry's own `date` field in Forbes's data. Those stamps fall at several hours
(00:00, 03:00, 04:00, 05:00, 05:01, 09:01 and 23:00 UTC), and the entries of one list can carry dates up to a month apart:
on the World's Billionaires 2013, Bill Gates, Jeff Bezos, Mark Zuckerberg and Elon Musk are dated March 26 and Donald
Trump and Oprah Winfrey March 1, while most of the list is dated March 4; in 2009 most entries are dated March 1 and a few
March 11 or March 30. So an entry's date is not the list's. `listDate` is computed instead: the most common UTC date among
all of the list's entries (`forbesLists` gives how many entries carry it). Before 2013 that is the first day of the list's
month for most entries, so it marks the month rather than a day of publication; neither date is the day the fortunes
were measured. Values dated 2026 also carry `asOf` (the list date), as MODALITIES.md rule 10 asks.

**Verification.** Every Forbes value carries `verifiedOn`, the day the cached copy holding its quote was fetched. A build
checks every value against that cached copy, online or offline; it does not download a feed again by itself.
`python3 -I Tools/lives/build.py --refresh` re-downloads the 2026 feeds first (and fails if Forbes's data no longer
matches `sources.json`); deleting `Tools/lives/.cache` re-fetches everything.

**Revisions.** On 2026-11-03 the build read the Wayback Machine's index, which lists 2,298 status-200 captures of the two
lists' data in 2005-2026 at both addresses, and compared the six candidates' values with today's in 71 archived copies
covering 33 list-years: all 31 copies at the older address, and the earliest and latest readable whole-list copy at the
newer address for each list and year (40). Of 388 candidate values compared none differs, and of 319 ranks compared
(where a copy keeps the list's rank) none differs. The Forbes 400 2014 copy keeps real-time figures in `worth` and
`rank`, so its list value is read from `archivedWorth` and its rank is not compared; `sources.json` records why, with
quotes the build finds in the copy. So every value in the file is Forbes's current estimate and no earlier estimate is
kept. How far that reaches:

- the earliest copy is within 31 days of the list date, so it shows the values as first published or nearly, for the
  World's Billionaires 2015, 2018, 2020, 2021 and 2023-2026, and the Forbes 400 2018-2026;
- only later copies exist for the World's Billionaires 2005, 2007, 2009-2014, 2016, 2017, 2019 and 2022, and the
  Forbes 400 2014-2017 (2005-2013 copies of the World's Billionaires were all made in March 2026): they show the values
  as of the day they were made, so they would miss a revision made before then;
- no archived copy of the list data exists at either address for the World's Billionaires 2006 and 2008 and the Forbes
  400 2005-2013.

Some copies hold only the list's first page (the newer address often served 50 rows at a time); candidates beyond that
page are reported, not compared. forbes.com's HTML list pages, which held the lists before the list data existed, were
not searched. At least some are archived (for example the Forbes 400 2008 ranking page, with captures from 2008-09-20
on), so some of the early years above could still be checked from them.

## Who is a candidate

Candidates are the US people of `people.json` who are public adults and have at least 8 annual values on the World's
Billionaires list in the years compared (2016-2025). The builder searches every list feed from 2005 to 2026 for every US
person of `people.json` and prints what it finds, so nobody is chosen by hand.

**Public adults only**, checked in `build.py` from `people.json` before anything is written:

- `kind` is `"p"` (a person, not a group);
- the person has a Wikidata item and an English Wikipedia article (a public figure, not a private individual);
- the birth year is known and not estimated (`est` false);
- the person was 18 or older on 1 January of the first year of any data written for them (born at least 19 years before
  it, whatever the birthday). The page views start in 2015, so someone born in 1997 or later is excluded even if they are
  an adult today.

Anyone who fails the rule is counted and never named or identified, in the file (`excludedByPublicAdultRule` holds only a
count and the reasons) and in the build log. Before writing, the build checks the rule again for everyone the file names:
the candidates and the public adults listed in `notCandidates`.

As built on 2026-11-03 the search finds 15 US people of `people.json` on a list. Six are candidates: Bill Gates, Donald
Trump, Elon Musk, Jeff Bezos, Mark Zuckerberg and Oprah Winfrey (each on the World's Billionaires list every year
2016-2025). Eight are public adults with too few years (Tim Cook and Kim Kardashian 5, Jay-Z 6, LeBron James 3, Taylor
Swift and Magic Johnson 2, Arnold Schwarzenegger 1, Beyoncé 0 before 2026). One fails the public-adult rule (under 18 in
2015, the first year of data) and is counted only. One more `people.json` account matches a Forbes name but not its birth
year; it also fails the rule (no Wikidata item, estimated birth year), so it is left out and not named.

## The three rules

The rules are the spec's (MODALITIES.md 7.3); `sources.json` holds their exact wording and `visibility.json` the full
rankings. Each rule picks among the candidates the earlier rules did not pick (together, then apart, then little
attention).

- **Together**: the highest share of years in which log net worth and log page views moved the same way (both rose or
  both fell). The years are 2016-2025; net worth is the World's Billionaires estimate of each year and page views are the
  calendar year's, so there are nine year-on-year moves. A year in which either series did not move is a year they did
  not move the same way: it stays among the nine and is not counted as together, so captions read "together in k of 9
  years". Ties: more years, then the id.
- **Apart**: the lowest share, counted the same way.
- **Little attention**: the richest Forbes-400 American not among the most-followed accounts, with the fewest page views.
  Among the candidates on the Forbes 400 of 2025 that the earlier rules did not pick, those outside the attention cloud
  (fewer than 10 million followers in the 2025 snapshot, the cloud of MODALITIES.md 5.2) are ranked by fortune. If none
  is outside the cloud, the rule picks nobody: the candidate with the fewest page views in 2025 is written as a fallback
  proposal (`rules.littleAttention.fallback`), not as the pick, until the author accepts it.

As built on 2026-11-03, author approval pending (`author_approved: false`):

| Rule | Pick | Score |
| --- | --- | --- |
| together | Elon Musk | together in 8 of 9 years |
| apart | Donald Trump | together in 2 of 9 years (one year when his fortune did not move counts as not together); tied with Oprah Winfrey at 2 of 9, broken by the id |
| little attention | none | every candidate on the 2025 Forbes 400 has 10M followers or more; fallback proposal: Bill Gates, 2.87M page views in 2025 (the fewest of the five), $107B, 77.0M followers |

Full ranking: Elon Musk 8 of 9, Jeff Bezos 6 of 9, Mark Zuckerberg 5 of 9, Bill Gates 3 of 9, Donald Trump 2 of 9 (one
flat year), Oprah Winfrey 2 of 9 (two flat years).

**How stable the picks are.** The direction of a year-on-year change is a fragile statistic for these six people. Apart is
an exact tie between Donald Trump and Oprah Winfrey, settled only by the alphabetical tie-break. The build also prints an
alignment check that pairs each spring list with the page views of the 12 months before it instead of the calendar year
(2017-2026): together would then be Jeff Bezos (8 of 9) and apart Elon Musk (3 of 9). Neither alignment is wrong; the
calendar year is the spec's ("indexed 2016 = 1", "k of 9 years"). The author should approve the shortlist knowing this.

**The literal reading of little attention.** Read literally, "not among the most-followed accounts" points outside
`people.json` altogether; the build prints who that would be (the richest on the 2025 Forbes 400 with no account in
`people.json`: Larry Ellison, $276B) but does not build it, because candidates are limited to `people.json`, whose kind
and birth year enforce the public-adult rule.

## Approving the shortlist

`sources.json` `shortlist.author_approved` is `false`. To approve, set it to `true` and `approved` to
`{"together": "<id>", "apart": "<id>", "littleAttention": "<id>" or null}` (ids are Forbes's, as in `visibility.json`).
While no candidate meets the little-attention condition, naming the fallback proposal's id accepts it as the pick
(`rules.littleAttention.fallback.accepted` becomes true) and `null` leaves the rule without a pick. The build then fails
if new data ever makes a rule pick someone else.

## What the build prints

Every run prints `CHECK` lines for: `people.json` (with its sha256) and the Forbes feeds read; the candidates; the
public-adult check; how many hand-read quotes were found verbatim in the cached copy they cite; the revision check (the
archive's captures, the copies and values compared, the differences) and its coverage (copies near the list date, only
later copies, no copy); the together and apart picks with their scores and any tie; the little-attention pick or, when
its condition is unmet, the fallback proposal; the shortlist and the approval flag; and, for each chosen person and the
fallback proposal, the page-view months and gaps, the Forbes values (each with URL and quote) and the quotes found.
Between them it prints each public adult found on a list with their years and why they are or are not a candidate (those
who fail the rule only as a count), each candidate's year-on-year moves, one line per list and year of the revision check,
the full rankings (`RULE`), the alignment check and the literal reading of little attention, and finally the sha256 of
the file written. Any failed check is printed as `PROBLEM` and the build exits 1 without writing. If the Wayback Machine
cannot be reached, the revision check is reported as not run and the rest of the file is still built.

## Layout of `visibility.json`

```json
{ "note", "built", "window": { "pageViews": {"from", "to"}, "annual": {"from", "to"}, "forbes": {"from", "to"} },
  "measures", "followersLabel",
  "rules": { "minAnnualPoints", "netWorthList",
             "together": { "rule", "pick", "tiedWith", "ranking": [{ "id", "k", "m", "flat", "share" }] },
             "apart": { "rule", "pick", "tiedWith", "ranking" },
             "littleAttention": { "rule", "fallbackRule", "mostFollowedFrom", "year", "conditionMet", "pick",
                                  "fallback": { "id", "name", "netWorth", "followers", "pageViews", "accepted" },
                                  "basis", "ranking": [{ "id", "netWorth", "followers", "pageViews" }] },
             "distinct" },
  "shortlist": { "authorApproved", "picks": [{ "rule", "id" (null: no pick), "name", ... }], "note" },
  "forbesLists": { "note", "byList": { "<list>": { "<year>": { "listDate", "entries", "onListDate" } } } },
  "forbesRevisions": { "kept", "method", "status", "nearListDays", "index": [{ "url", "retrieved" }],
                       "capturesInWindow", "copiesCompared", "valuesCompared", "differences", "ranksCompared",
                       "rankDifferences", "nearList", "onlyLaterCopies", "noReadableCopy", "noArchivedCopy",
                       "listYears": [{ "list", "year", "listDate", "copies",
                                       "compared": [{ "url", "capturedOn", "daysAfterList", "address", "field",
                                                     "ranksCompared", "firstPageOnly", "candidates", "same", "differ",
                                                     "beyondThisPage" }],
                                       "skipped": [{ "url", "why" }] }],
                       "note" },
  "people": [{ "id", "name", "handle", "wikidata", "born", "publicAdult",
               "wikipedia": { "value", "url" (the Wikidata item), "article", "source", "field", "retrieved" },
               "followers": { "value", "year", "label", "source", "url", "field", "note" },
               "forbes": [{ "list", "listName", "year", "month", "listDate", "entryDate", "value", "unit", "rank",
                            "source", "url", "quote", "field", "current", "verifiedOn", "asOf", "note", "revision" }],
               "pageViews": { "value": [136 monthly counts], "start", "end", "unit", "source", "url", "field",
                              "retrieved", "missing", "note" },
               "annual": { "years", "netWorth", "netWorthList", "pageViews", "netWorthIndex", "pageViewsIndex",
                           "moves", "computed" },
               "together": { "k", "m", "flat", "share", "caption" } }],
  "notCandidates": [{ "name", "handle", "wikidata", "forbes", "lists", "reason" }],
  "excludedByPublicAdultRule": { "count", "reasons", "note" } }
```

`id` is the person's Forbes id (`elon-musk`). `annual.moves` maps each year to two signs, net worth then page views
(`+` rose, `-` fell, `=` unchanged). `together.m` counts every year compared, `flat` the years in which a series did not
move (among `m`, never among `k`). Indices are each series over its 2016 value. `built` is the latest retrieval date of the
downloads used, so an offline rebuild writes the same bytes.

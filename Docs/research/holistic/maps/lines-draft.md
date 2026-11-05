# Map: line clutter in the economy, view by view (draft)

A checkpoint of the line audit's draft (role map:lines), left by an interrupted run of the holistic views' design
workflow on 4 Nov 2026 (23:49). It predates the final emphasis table of commit 1c7b850 in places; the workflow's
map:lines step checks it against `Assets/Scripts/Economy/Emphasis/EmphasisTable.cs` and the g3b2 frames, finishes it and
writes `work/holistic/map/lines.md`, which supersedes this file. Scratch evidence (crops `viz-*.png`, `rows.json`, the
scripts) stays in `/home/john/Desktop/Unity/.why-2-build/work/holistic/map/lines-scratch/`.

Each part is a key of the emphasis table (layer.part): what it draws in that view, its alpha there, roughly how bright it
stands on screen, its blend, whether it makes the view's point (its title and subtitle) or is context, and the proposal:
keep (at most two line families a view), go, or carried by an object instead.

## Counts per view

| View | Line parts drawn | at >= .05 | at >= .2 | of them added | Object parts drawn |
| --- | --- | --- | --- | --- | --- |
| `overview` | 38 | 35 | 18 | 12 | 9 |
| `section` | 11 | 9 | 6 | 1 | 1 |
| `landscape` | 38 | 32 | 20 | 12 | 9 |
| `capture` | 32 | 26 | 22 | 9 | 5 |
| `people` | 39 | 36 | 24 | 12 | 9 |
| `rivers` | 35 | 33 | 23 | 10 | 5 |
| `mind` | 7 | 2 | 2 | 1 | 0 |
| `society` | 34 | 18 | 10 | 6 | 6 |
| `foundation` | 33 | 28 | 17 | 10 | 6 |
| `companies` | 34 | 28 | 18 | 10 | 6 |
| `social` | 39 | 33 | 21 | 12 | 8 |
| `roots` | 33 | 29 | 17 | 10 | 5 |
| `betrayal` | 34 | 18 | 10 | 6 | 6 |
| `y1972` | 35 | 29 | 18 | 10 | 6 |
| `strategy` | 45 | 25 | 18 | 9 | 8 |
| `money` | 16 | 6 | 1 | 1 | 6 |
| `regimes` | 35 | 18 | 14 | 4 | 5 |
| `mobility` | 38 | 16 | 9 | 5 | 6 |
| `atom` | 39 | 17 | 9 | 7 | 6 |
| `dilemma` | 40 | 18 | 10 | 7 | 8 |
| `ladder` | 38 | 17 | 11 | 7 | 6 |
| `you` | 41 | 19 | 12 | 5 | 7 |

## How much of the land lines cover (from the frames)

```
economy-view-atom.png                      land   56% of frame | ink 15.3%  cover 55.6%  solid 29.3%  lit 46.5%
    line ink by hue: gold 43%  neutral 30%  rose 13%  ice 9%  blue 5%  violet 0%
economy-view-betrayal.png                  land   65% of frame | ink 21.3%  cover 59.7%  solid 66.0%  lit 87.7%
    line ink by hue: neutral 73%  gold 19%  blue 3%  rose 3%  ice 1%  green 0%  violet 0%
economy-view-capture.png                   land   63% of frame | ink 33.2%  cover 76.1%  solid 48.3%  lit 81.9%
    line ink by hue: gold 50%  neutral 36%  ice 8%  rose 4%  blue 2%  green 0%  violet 0%
economy-view-companies.png                 land   39% of frame | ink 25.3%  cover 63.5%  solid 53.6%  lit 79.4%
    line ink by hue: neutral 51%  gold 36%  rose 7%  blue 3%  ice 2%  green 1%
economy-view-dilemma.png                   land   69% of frame | ink 17.5%  cover 61.4%  solid 27.0%  lit 45.5%
    line ink by hue: gold 47%  neutral 32%  rose 12%  ice 5%  blue 4%  violet 0%  green 0%
economy-view-foundation.png                land   53% of frame | ink 28.3%  cover 69.9%  solid 59.7%  lit 88.2%
    line ink by hue: neutral 63%  gold 28%  ice 6%  blue 2%  rose 1%  green 0%  violet 0%
economy-view-ladder.png                    land   49% of frame | ink 26.1%  cover 80.0%  solid 44.4%  lit 72.2%
    line ink by hue: neutral 58%  gold 33%  ice 3%  rose 3%  blue 2%  violet 0%  green 0%
economy-view-landscape.png                 land   54% of frame | ink 27.2%  cover 68.4%  solid 54.2%  lit 81.8%
    line ink by hue: neutral 52%  gold 35%  ice 6%  rose 4%  blue 2%  green 0%  violet 0%
economy-view-mind.png                      land   15% of frame | ink 21.0%  cover 62.3%  solid  1.4%  lit 22.5%
    line ink by hue: neutral 52%  rose 18%  blue 15%  gold 8%  ice 6%  violet 0%
economy-view-mobility.png                  land   27% of frame | ink 22.0%  cover 65.1%  solid 21.2%  lit 43.2%
    line ink by hue: neutral 69%  gold 18%  rose 13%  ice 0%  blue 0%
economy-view-money.png                     land   69% of frame | ink  4.8%  cover 16.4%  solid 93.7%  lit 98.5%
    line ink by hue: neutral 60%  gold 20%  ice 12%  rose 8%  violet 0%
economy-view-overview.png                  land   43% of frame | ink 28.8%  cover 72.4%  solid 54.0%  lit 83.2%
    line ink by hue: neutral 56%  gold 33%  rose 6%  blue 2%  ice 2%  green 0%  violet 0%
economy-view-people.png                    land   70% of frame | ink 28.3%  cover 70.8%  solid 58.7%  lit 87.4%
    line ink by hue: neutral 48%  gold 31%  blue 11%  ice 7%  rose 4%  green 0%
economy-view-regimes-cn.png                land   31% of frame | ink 24.6%  cover 70.2%  solid 40.2%  lit 67.4%
    line ink by hue: neutral 71%  gold 25%  blue 2%  ice 1%  rose 1%  violet 0%  green 0%
economy-view-regimes.png                   land   31% of frame | ink 24.2%  cover 68.4%  solid 43.2%  lit 70.1%
    line ink by hue: neutral 59%  gold 37%  blue 2%  ice 1%  rose 1%  violet 0%  green 0%
economy-view-regimes-sa.png                land   31% of frame | ink 23.1%  cover 67.2%  solid 38.4%  lit 64.3%
    line ink by hue: neutral 66%  gold 30%  blue 2%  ice 1%  rose 1%  violet 0%  green 0%
economy-view-regimes-se.png                land   33% of frame | ink 23.4%  cover 66.0%  solid 44.6%  lit 70.5%
    line ink by hue: neutral 64%  gold 32%  blue 2%  ice 1%  rose 1%  violet 0%  green 0%
economy-view-regimes-su.png                land   33% of frame | ink 22.1%  cover 64.4%  solid 46.4%  lit 70.9%
    line ink by hue: neutral 76%  gold 21%  blue 2%  rose 1%  ice 1%  violet 0%  green 0%
economy-view-regimes-us.png                land   31% of frame | ink 24.2%  cover 68.4%  solid 43.2%  lit 70.1%
    line ink by hue: neutral 59%  gold 37%  blue 2%  ice 1%  rose 1%  violet 0%  green 0%
economy-view-rivers.png                    land   61% of frame | ink 29.1%  cover 75.4%  solid 56.5%  lit 85.9%
    line ink by hue: neutral 61%  gold 25%  rose 6%  ice 5%  blue 4%  green 0%  violet 0%
economy-view-roots.png                     land   57% of frame | ink 24.4%  cover 68.0%  solid 60.9%  lit 85.8%
    line ink by hue: neutral 56%  gold 30%  ice 6%  rose 4%  blue 3%  green 0%  violet 0%
economy-view-section.png                   land   40% of frame | ink 24.1%  cover 68.4%  solid 42.5%  lit 67.3%
    line ink by hue: neutral 55%  blue 23%  gold 17%  rose 3%  green 0%  ice 0%  violet 0%
economy-view-social.png                    land   45% of frame | ink 28.9%  cover 76.8%  solid 50.5%  lit 80.5%
    line ink by hue: neutral 56%  gold 32%  rose 6%  ice 4%  blue 2%  green 0%  violet 0%
economy-view-society.png                   land   65% of frame | ink 21.1%  cover 59.5%  solid 66.1%  lit 87.7%
    line ink by hue: neutral 72%  gold 20%  blue 4%  rose 3%  ice 1%  green 0%  violet 0%
economy-view-strategy.png                  land   53% of frame | ink 23.9%  cover 67.2%  solid 56.3%  lit 81.9%
    line ink by hue: neutral 47%  gold 38%  rose 9%  ice 6%  blue 0%  violet 0%  green 0%
economy-view-y1972.png                     land   27% of frame | ink 25.1%  cover 72.2%  solid 41.4%  lit 67.0%
    line ink by hue: neutral 45%  gold 36%  blue 8%  ice 6%  rose 5%  green 0%  violet 0%
economy-view-you.png                       land   66% of frame | ink 19.0%  cover 58.0%  solid 58.6%  lit 79.2%
    line ink by hue: neutral 41%  gold 36%  ice 15%  rose 6%  blue 2%  violet 0%  green 0%
```

## Every view

#### `overview`

| Part | What it draws here | Alpha | ≈ on screen | Blend | Point or context | Proposal |
| --- | --- | --- | --- | --- | --- | --- |
| `land.road` | road of lives: ~3,500 lifelines (1 line = 100,000 people) and the time axis | 1 | – | added | context | → object: one population band (its width the people), lifelines only close up |
| `wave.crest` | the crest line, white, 2.4 px, pulsing | 1 | – | blended | **point** | **keep: subject (the year shown)** |
| `activity.sparks` | moving sparks along the trunks | 1 | .65 | added | context | go |
| `activity.focus` | one gold ring round the summit most is spent at | 1 | – | added | context | go |
| `capture.halo` | ~23 industries' owners' halos: 3 circles, stem, rose/ice arcs, white ticks | 1 | – | blended | context | → object: owners' gold discs on the summits |
| `social.anchors` | stars at a company or office seat, gold ropes to their other firms | 1 | .8 | added | context | go |
| `activity.halos` | 25 firms' profit halos, 3 gold rings each | .95 | – | blended | context | → object: firm tokens at the crest (disc or logo tile) |
| `wave.arrow` | arrow of time at the crest's far end | .85 | .68 | blended | context | go |
| `wave.spray` | ~65 spray arcs breaking forward from the crest | .8 | .24 | blended | context | go |
| `capture.climb` | ~23 gold rivers climbing each hill to its owners' halo (lines added, ribbon blended) | .8 | – | added | context | go |
| `wave.lives` | 75 company lifelines (gold need, rose want, ice fear) with birth rings, × and ○ | .75 | – | blended | context | go |
| `activity.circles` | 25 owners' inner circles: 2 gold rings with 5-22 owner pins | .75 | – | blended | context | go (owners shown by the discs) |
| `wave.ridges` | 23 industry ridges along time + 4 tier steps | .7 | .39 | blended | context | → object: the wake as a tier-tinted terrain surface |
| `wave.ribs` | 79 ribs, one a year 1947-2025 (decades .55, fifth years .26, the rest .10 of their own) | .55 | .08 | blended | **point** | **keep: support .45, decade ribs only** |
| `foundation.strata` | strata boundaries along the wake and the receipts line | .45 | .38 | added | context | go |
| `activity.beams` | 25 market-value beams with revenue ring, margin arc and $1T ticks | .22 | – | added | context | go |
| `activity.trunks` | 23 trunks: each market's household spending as a rose/ice ribbon | .2 | – | blended | context | go |
| `capture.ledger` | 128 cohorts' ledger rings (gold collects, rose pays) | .2 | .17 | blended | context | go |
| `capture.wages` | <=175 blue paths from each market to the cohorts it pays (straight up to the cloud's) | .16 | .11 | blended | context | go |
| `social.rings` | category clouds' rings | .15 | .07 | blended | context | go |
| `wave.fan` | ~125 threads from the road out to every cohort | .14 | .07 | blended | context | go |
| `wave.future` | debt streams arriving from the future, one per indebted cohort | .12 | .07 | blended | context | go |
| `wave.saving` | gold saving streams leaving over the crest, one per saving cohort | .12 | .07 | blended | context | go |
| `hills.crown` | 2 owners' crowns (3 gold rings each), the 1%'s cloud outline and gold threads from summits to it (.08) | .1 | .05 | blended | context | → object: the 1%'s cloud as one solid |
| `capture.taxes` | ~23 steel drops from the markets to the bedrock, upkeep rings | .1 | .06 | blended | context | go |
| `foundation.pillars` | 6 program pillars: spines with rings | .1 | .08 | added | context | go |
| `activity.threads` | 25 gold threads from the inner circles up to the 1%'s cloud | .08 | .04 | added | context | go |
| `social.threads` | ropes from a company or seat up to the person's place in the cloud | .08 | .03 | added | context | go |
| `activity.rivalry` | ~20 rivalry arcs between firms of one sector (rose) | .07 | – | added | context | go |
| `capture.ascent` | <=230 gold arcs from the halos to the cohorts that hold the claims, and abroad | .07 | .04 | blended | context | go |
| `foundation.benefits` | ~250 arcs from the pillars' tops to the households served | .06 | .04 | blended | context | go |
| `hills.roots` | 39 underground purchase lines (BEA IO, at least $100B), blue | .055 | .02 | blended | context | go |
| `social.elite` | rings: followed by the most-followed | .05 | .03 | blended | context | go |
| `foundation.taxes` | ~140 drops: every cohort's taxes and each industry's corporate tax | .05 | .03 | added | context | go |
| `foundation.regulation` | ~45 strands: grey rules up into each hill, gold capture back down (Jev) | .05 | .04 | blended | context | go |
| traces | capture.roots .04, activity.trips .035, activity.supply .018 | < .05 | | | context | go |
| `wave.plate` *(object)* | dark plates behind the wave's names | 1 | | blended | context | keep |
| `foundation.seats` *(object (strokes))* | officeholders: stick figures with head and base rings | 1 | | added | context | keep as context |
| `wave.face` *(object)* | crest's cut face: 5 tier fills (.06-.22) with steel outlines, gold ridge fill | .8 | | blended | **point** | **carry**: the tiers, extended over the wake as terrain |
| `capture.glow` *(object)* | gold glow discs under those halos | .8 | | blended | context | → the owners' discs |
| `social.bright` *(object (strokes))* | the 16 most-followed stars | .7 | | blended | context | **carry** (as above) |
| `activity.markers` *(object (strokes))* | 640 moving markers, 5 per cohort: stick, head ring (asset and debt rings close up) | .6 | | blended | context | **carry**: cohorts as figure tokens sized by people |
| `foundation.block` *(object)* | the federal block: strata faces under the wave (its edges and decade ticks added) | .55 | | blended | context | **carry** (the bedrock) |
| `social.glow` *(object)* | the attention cloud's glow | .4 | | added | context | keep |
| `social.stars` *(object (strokes))* | ~340 star glyphs (3 strokes and a ring), sized by followers | .25 | | blended | context | **carry**: portrait thumbnails for the named, stars for the rest |
| *new* | the wake filled as terrain, tier by tier (today only ribs and ridge lines draw the land behind the crest) | | | | | **object carries the view** |

#### `section`

| Part | What it draws here | Alpha | ≈ on screen | Blend | Point or context | Proposal |
| --- | --- | --- | --- | --- | --- | --- |
| `wave.crest` | the crest line, white, 2.4 px, pulsing | 1 | – | blended | context | fade to ≤ .12 |
| `wave.spray` | ~65 spray arcs breaking forward from the crest | .5 | .15 | blended | context | go |
| `wave.fan` | ~125 threads from the road out to every cohort | .35 | .17 | blended | context | fade to ≤ .12 |
| `land.road` | road of lives: ~3,500 lifelines (1 line = 100,000 people) and the time axis | .3 | – | added | **point** | **keep: subject, blended at intensity ≤ 1 (today .3 and added)** |
| `wave.ribs` | 79 ribs, one a year 1947-2025 (decades .55, fifth years .26, the rest .10 of their own) | .3 | .04 | blended | context | go |
| `wave.ridges` | 23 industry ridges along time + 4 tier steps | .2 | .11 | blended | context | go |
| `wave.lives` | 75 company lifelines (gold need, rose want, ice fear) with birth rings, × and ○ | .1 | – | blended | context | go |
| `hills.crown` | 2 owners' crowns (3 gold rings each), the 1%'s cloud outline and gold threads from summits to it (.08) | .1 | .05 | blended | context | go |
| `hills.roots` | 39 underground purchase lines (BEA IO, at least $100B), blue | .055 | .02 | blended | context | go |
| traces | wave.future .04, wave.saving .04 | < .05 | | | context | go |
| `wave.face` *(object)* | crest's cut face: 5 tier fills (.06-.22) with steel outlines, gold ridge fill | .4 | | blended | context | keep as context |
| *new* | a marriage and a birth marker on the road's junctions (two icons, named once) | | | | | **object carries the view** |

#### `landscape`

| Part | What it draws here | Alpha | ≈ on screen | Blend | Point or context | Proposal |
| --- | --- | --- | --- | --- | --- | --- |
| `wave.ridges` | 23 industry ridges along time + 4 tier steps | 1 | .55 | blended | **point** | **keep: subject: one ridge per industry, tinted by tier and named** |
| `wave.crest` | the crest line, white, 2.4 px, pulsing | 1 | – | blended | **point** | **keep: support .45** |
| `activity.sparks` | moving sparks along the trunks | 1 | .65 | added | context | go |
| `activity.focus` | one gold ring round the summit most is spent at | 1 | – | added | context | go |
| `capture.halo` | ~23 industries' owners' halos: 3 circles, stem, rose/ice arcs, white ticks | 1 | – | blended | context | → object: owners' discs |
| `social.anchors` | stars at a company or office seat, gold ropes to their other firms | 1 | .8 | added | context | go |
| `activity.halos` | 25 firms' profit halos, 3 gold rings each | .95 | – | blended | context | → object: firm tokens at the crest |
| `wave.ribs` | 79 ribs, one a year 1947-2025 (decades .55, fifth years .26, the rest .10 of their own) | .85 | .13 | blended | context | fade to ≤ .12 |
| `wave.arrow` | arrow of time at the crest's far end | .85 | .68 | blended | context | go |
| `wave.spray` | ~65 spray arcs breaking forward from the crest | .8 | .24 | blended | context | go |
| `capture.climb` | ~23 gold rivers climbing each hill to its owners' halo (lines added, ribbon blended) | .8 | – | added | context | go |
| `wave.lives` | 75 company lifelines (gold need, rose want, ice fear) with birth rings, × and ○ | .75 | – | blended | context | go |
| `activity.circles` | 25 owners' inner circles: 2 gold rings with 5-22 owner pins | .75 | – | blended | context | go |
| `capture.wages` | <=175 blue paths from each market to the cohorts it pays (straight up to the cloud's) | .55 | .39 | blended | context | go |
| `foundation.strata` | strata boundaries along the wake and the receipts line | .45 | .38 | added | context | go |
| `land.road` | road of lives: ~3,500 lifelines (1 line = 100,000 people) and the time axis | .35 | – | added | context | → object: one population band |
| `capture.taxes` | ~23 steel drops from the markets to the bedrock, upkeep rings | .35 | .21 | blended | context | go |
| `activity.beams` | 25 market-value beams with revenue ring, margin arc and $1T ticks | .22 | – | added | context | go |
| `activity.trunks` | 23 trunks: each market's household spending as a rose/ice ribbon | .2 | – | blended | context | go |
| `capture.ledger` | 128 cohorts' ledger rings (gold collects, rose pays) | .2 | .17 | blended | context | go |
| `social.rings` | category clouds' rings | .15 | .07 | blended | context | go |
| `hills.crown` | 2 owners' crowns (3 gold rings each), the 1%'s cloud outline and gold threads from summits to it (.08) | .1 | .05 | blended | context | → object: the 1%'s cloud as one solid |
| `foundation.pillars` | 6 program pillars: spines with rings | .1 | .08 | added | context | go |
| `activity.threads` | 25 gold threads from the inner circles up to the 1%'s cloud | .08 | .04 | added | context | go |
| `social.threads` | ropes from a company or seat up to the person's place in the cloud | .08 | .03 | added | context | go |
| `activity.rivalry` | ~20 rivalry arcs between firms of one sector (rose) | .07 | – | added | context | go |
| `capture.ascent` | <=230 gold arcs from the halos to the cohorts that hold the claims, and abroad | .07 | .04 | blended | context | go |
| `foundation.benefits` | ~250 arcs from the pillars' tops to the households served | .06 | .04 | blended | context | go |
| `hills.roots` | 39 underground purchase lines (BEA IO, at least $100B), blue | .055 | .02 | blended | context | go |
| `social.elite` | rings: followed by the most-followed | .05 | .03 | blended | context | go |
| `foundation.taxes` | ~140 drops: every cohort's taxes and each industry's corporate tax | .05 | .03 | added | context | go |
| `foundation.regulation` | ~45 strands: grey rules up into each hill, gold capture back down (Jev) | .05 | .04 | blended | context | go |
| traces | wave.fan .04, wave.future .04, wave.saving .04, capture.roots .04, activity.trips .035, activity.supply .018 | < .05 | | | context | go |
| `wave.face` *(object)* | crest's cut face: 5 tier fills (.06-.22) with steel outlines, gold ridge fill | 1 | | blended | **point** | **carry**: four distinct tier fills at .3-.45 with names and shares |
| `wave.plate` *(object)* | dark plates behind the wave's names | 1 | | blended | context | keep as context |
| `foundation.seats` *(object (strokes))* | officeholders: stick figures with head and base rings | 1 | | added | context | keep as context |
| `capture.glow` *(object)* | gold glow discs under those halos | .8 | | blended | context | keep as context |
| `social.bright` *(object (strokes))* | the 16 most-followed stars | .7 | | blended | context | keep as context (tokens) |
| `activity.markers` *(object (strokes))* | 640 moving markers, 5 per cohort: stick, head ring (asset and debt rings close up) | .6 | | blended | context | keep as context (small) |
| `foundation.block` *(object)* | the federal block: strata faces under the wave (its edges and decade ticks added) | .55 | | blended | context | **carry** (the bedrock the tiers stand on) |
| `social.glow` *(object)* | the attention cloud's glow | .4 | | added | context | keep as context |
| `social.stars` *(object (strokes))* | ~340 star glyphs (3 strokes and a ring), sized by followers | .25 | | blended | context | keep as context (tokens) |
| *new* | each industry a filled ridge band whose width is its share (an object per industry, named) | | | | | **object carries the view** |

#### `capture`

| Part | What it draws here | Alpha | ≈ on screen | Blend | Point or context | Proposal |
| --- | --- | --- | --- | --- | --- | --- |
| `wave.crest` | the crest line, white, 2.4 px, pulsing | 1 | – | blended | context | go |
| `hills.crown` | 2 owners' crowns (3 gold rings each), the 1%'s cloud outline and gold threads from summits to it (.08) | 1 | .5 | blended | context | → object: the 1%'s cloud as one solid, where the owners' share lands |
| `activity.circles` | 25 owners' inner circles: 2 gold rings with 5-22 owner pins | 1 | – | blended | context | go (the discs say who owns) |
| `activity.sparks` | moving sparks along the trunks | 1 | .65 | added | context | go |
| `activity.focus` | one gold ring round the summit most is spent at | 1 | – | added | context | go |
| `capture.climb` | ~23 gold rivers climbing each hill to its owners' halo (lines added, ribbon blended) | 1 | – | added | **point** | **keep: subject: owners' share up (blended, not added)** |
| `capture.halo` | ~23 industries' owners' halos: 3 circles, stem, rose/ice arcs, white ticks | 1 | – | blended | **point** | gold discs on the summits sized by the owners' take |
| `capture.wages` | <=175 blue paths from each market to the cohorts it pays (straight up to the cloud's) | 1 | .7 | blended | **point** | **keep: subject: wages back down, bundled per market** |
| `capture.ledger` | 128 cohorts' ledger rings (gold collects, rose pays) | 1 | .85 | blended | context | → object: the tint of each cohort's token (gold collects, rose pays) |
| `capture.taxes` | ~23 steel drops from the markets to the bedrock, upkeep rings | .8 | .48 | blended | **point** | → object: the steel part of each market's split token |
| `foundation.strata` | strata boundaries along the wake and the receipts line | .7 | .59 | added | context | go |
| `activity.beams` | 25 market-value beams with revenue ring, margin arc and $1T ticks | .55 | – | added | context | go |
| `activity.rivalry` | ~20 rivalry arcs between firms of one sector (rose) | .5 | – | added | context | go |
| `capture.ascent` | <=230 gold arcs from the halos to the cohorts that hold the claims, and abroad | .4 | .22 | blended | context | → object: the holders as objects (the 1%'s cloud, owners' tokens); no arcs |
| `land.road` | road of lives: ~3,500 lifelines (1 line = 100,000 people) and the time axis | .35 | – | added | context | go |
| `capture.roots` | underground amber purchase lines set off by household spending (>= $25B) | .35 | .17 | blended | context | go |
| `wave.lives` | 75 company lifelines (gold need, rose want, ice fear) with birth rings, × and ○ | .3 | – | blended | context | go |
| `wave.ribs` | 79 ribs, one a year 1947-2025 (decades .55, fifth years .26, the rest .10 of their own) | .22 | .03 | blended | context | go |
| `wave.ridges` | 23 industry ridges along time + 4 tier steps | .2 | .11 | blended | context | go |
| `hills.pool` | the pool's gold ring at the 1%'s cloud | .2 | .07 | blended | context | go |
| `activity.trunks` | 23 trunks: each market's household spending as a rose/ice ribbon | .2 | – | blended | context | go |
| `foundation.pillars` | 6 program pillars: spines with rings | .2 | .16 | added | context | go |
| `wave.spray` | ~65 spray arcs breaking forward from the crest | .12 | .04 | blended | context | go |
| `foundation.benefits` | ~250 arcs from the pillars' tops to the households served | .06 | .04 | blended | context | go |
| `hills.roots` | 39 underground purchase lines (BEA IO, at least $100B), blue | .055 | .02 | blended | context | go |
| `foundation.taxes` | ~140 drops: every cohort's taxes and each industry's corporate tax | .05 | .03 | added | context | go |
| traces | wave.fan .04, wave.future .04, wave.saving .04, activity.trips .035, foundation.regulation .03, activity.supply .018 | < .05 | | | context | go |
| `capture.glow` *(object)* | gold glow discs under those halos | 1 | | blended | context | → part of the owners' disc |
| `foundation.seats` *(object (strokes))* | officeholders: stick figures with head and base rings | 1 | | added | context | keep as context |
| `activity.markers` *(object (strokes))* | 640 moving markers, 5 per cohort: stick, head ring (asset and debt rings close up) | .6 | | blended | context | **carry**: the cohorts that are paid |
| `foundation.block` *(object)* | the federal block: strata faces under the wave (its edges and decade ticks added) | .55 | | blended | context | **carry**: where the taxes land |
| `wave.face` *(object)* | crest's cut face: 5 tier fills (.06-.22) with steel outlines, gold ridge fill | .4 | | blended | context | keep as context |
| *new* | at each market the dollar's split as one object (a five-part ring or bar: wages, owners, upkeep, imports, taxes); the caption bar already draws it for the whole year | | | | | **object carries the view** |

#### `people`

| Part | What it draws here | Alpha | ≈ on screen | Blend | Point or context | Proposal |
| --- | --- | --- | --- | --- | --- | --- |
| `wave.crest` | the crest line, white, 2.4 px, pulsing | 1 | – | blended | context | go |
| `activity.sparks` | moving sparks along the trunks | 1 | .65 | added | context | go |
| `activity.focus` | one gold ring round the summit most is spent at | 1 | – | added | context | go |
| `capture.halo` | ~23 industries' owners' halos: 3 circles, stem, rose/ice arcs, white ticks | 1 | – | blended | context | → object: owners' discs (small) |
| `capture.ledger` | 128 cohorts' ledger rings (gold collects, rose pays) | 1 | .85 | blended | context | → object: the tint of the cohort tokens |
| `social.anchors` | stars at a company or office seat, gold ropes to their other firms | 1 | .8 | added | context | go |
| `activity.halos` | 25 firms' profit halos, 3 gold rings each | .95 | – | blended | context | → object: firm tokens (small) |
| `hills.rungs` | 6 rung arcs up the most populated hill (the class legend) | .9 | .5 | blended | **point** | **keep: subject: the class ruler on one hill** |
| `capture.wages` | <=175 blue paths from each market to the cohorts it pays (straight up to the cloud's) | .9 | .63 | blended | context | go |
| `mobility.drift` | 128 drift arrows (gold climbed, rose fell, faint held) | .9 | .63 | blended | **point** | **keep: support: ten-year moves as vertical stems (spec S05)** |
| `capture.climb` | ~23 gold rivers climbing each hill to its owners' halo (lines added, ribbon blended) | .8 | – | added | context | go |
| `activity.circles` | 25 owners' inner circles: 2 gold rings with 5-22 owner pins | .75 | – | blended | context | go |
| `wave.fan` | ~125 threads from the road out to every cohort | .7 | .35 | blended | context | go |
| `foundation.strata` | strata boundaries along the wake and the receipts line | .7 | .59 | added | context | go |
| `land.road` | road of lives: ~3,500 lifelines (1 line = 100,000 people) and the time axis | .35 | – | added | context | go |
| `wave.future` | debt streams arriving from the future, one per indebted cohort | .35 | .19 | blended | context | go |
| `wave.saving` | gold saving streams leaving over the crest, one per saving cohort | .35 | .19 | blended | context | go |
| `hills.crown` | 2 owners' crowns (3 gold rings each), the 1%'s cloud outline and gold threads from summits to it (.08) | .35 | .17 | blended | context | go |
| `foundation.pillars` | 6 program pillars: spines with rings | .35 | .28 | added | context | go |
| `activity.threads` | 25 gold threads from the inner circles up to the 1%'s cloud | .3 | .17 | added | context | go |
| `wave.ribs` | 79 ribs, one a year 1947-2025 (decades .55, fifth years .26, the rest .10 of their own) | .22 | .03 | blended | context | go |
| `activity.beams` | 25 market-value beams with revenue ring, margin arc and $1T ticks | .22 | – | added | context | go |
| `wave.ridges` | 23 industry ridges along time + 4 tier steps | .2 | .11 | blended | context | go |
| `activity.trunks` | 23 trunks: each market's household spending as a rose/ice ribbon | .2 | – | blended | context | go |
| `capture.ascent` | <=230 gold arcs from the halos to the cohorts that hold the claims, and abroad | .15 | .08 | blended | context | go |
| `social.rings` | category clouds' rings | .15 | .07 | blended | context | go |
| `wave.spray` | ~65 spray arcs breaking forward from the crest | .12 | .04 | blended | context | go |
| `foundation.benefits` | ~250 arcs from the pillars' tops to the households served | .12 | .08 | blended | context | go |
| `wave.lives` | 75 company lifelines (gold need, rose want, ice fear) with birth rings, × and ○ | .1 | – | blended | context | go |
| `capture.taxes` | ~23 steel drops from the markets to the bedrock, upkeep rings | .1 | .06 | blended | context | go |
| `social.threads` | ropes from a company or seat up to the person's place in the cloud | .08 | .03 | added | context | go |
| `activity.rivalry` | ~20 rivalry arcs between firms of one sector (rose) | .07 | – | added | context | go |
| `hills.roots` | 39 underground purchase lines (BEA IO, at least $100B), blue | .055 | .02 | blended | context | go |
| `social.elite` | rings: followed by the most-followed | .05 | .03 | blended | context | go |
| `foundation.taxes` | ~140 drops: every cohort's taxes and each industry's corporate tax | .05 | .03 | added | context | go |
| `foundation.regulation` | ~45 strands: grey rules up into each hill, gold capture back down (Jev) | .05 | .04 | blended | context | go |
| traces | capture.roots .04, activity.trips .035, activity.supply .018 | < .05 | | | context | go |
| `wave.plate` *(object)* | dark plates behind the wave's names | 1 | | blended | context | keep as context |
| `foundation.seats` *(object (strokes))* | officeholders: stick figures with head and base rings | 1 | | added | context | keep as context |
| `capture.glow` *(object)* | gold glow discs under those halos | .8 | | blended | context | keep as context |
| `social.bright` *(object (strokes))* | the 16 most-followed stars | .7 | | blended | context | keep as context |
| `activity.markers` *(object (strokes))* | 640 moving markers, 5 per cohort: stick, head ring (asset and debt rings close up) | .6 | | blended | **point** | **carry**: cohort figures sized by people, at their rung |
| `foundation.block` *(object)* | the federal block: strata faces under the wave (its edges and decade ticks added) | .55 | | blended | context | keep as context |
| `wave.face` *(object)* | crest's cut face: 5 tier fills (.06-.22) with steel outlines, gold ridge fill | .4 | | blended | context | keep as context |
| `social.glow` *(object)* | the attention cloud's glow | .4 | | added | context | keep as context |
| `social.stars` *(object (strokes))* | ~340 star glyphs (3 strokes and a ring), sized by followers | .25 | | blended | context | keep as context |
| *new* | the top 1% as figures in their cloud, in frame | | | | | **object carries the view** |

#### `rivers`

| Part | What it draws here | Alpha | ≈ on screen | Blend | Point or context | Proposal |
| --- | --- | --- | --- | --- | --- | --- |
| `activity.sparks` | moving sparks along the trunks | 1 | .65 | added | **point** | **keep: the rivers' own motion, blended (today added)** |
| `activity.focus` | one gold ring round the summit most is spent at | 1 | – | added | context | go |
| `activity.halos` | 25 firms' profit halos, 3 gold rings each | .95 | – | blended | context | go |
| `activity.trunks` | 23 trunks: each market's household spending as a rose/ice ribbon | .8 | – | blended | **point** | **keep: subject: 23 trunks, rose and ice lanes** |
| `foundation.strata` | strata boundaries along the wake and the receipts line | .7 | .59 | added | context | go |
| `activity.trips` | household trips to the markets, rose/ice ribbons (1 subgroup in 15; 1 in 3 in rivers) | .5 | – | blended | **point** | **keep: subject (same family): the trips that feed them** |
| `activity.circles` | 25 owners' inner circles: 2 gold rings with 5-22 owner pins | .45 | – | blended | context | go |
| `land.road` | road of lives: ~3,500 lifelines (1 line = 100,000 people) and the time axis | .35 | – | added | context | go |
| `foundation.pillars` | 6 program pillars: spines with rings | .35 | .28 | added | context | go |
| `wave.crest` | the crest line, white, 2.4 px, pulsing | .3 | – | blended | context | go |
| `wave.future` | debt streams arriving from the future, one per indebted cohort | .3 | .17 | blended | context | go |
| `wave.saving` | gold saving streams leaving over the crest, one per saving cohort | .3 | .17 | blended | context | go |
| `capture.climb` | ~23 gold rivers climbing each hill to its owners' halo (lines added, ribbon blended) | .3 | – | added | context | go |
| `foundation.taxes` | ~140 drops: every cohort's taxes and each industry's corporate tax | .3 | .17 | added | context | go |
| `capture.halo` | ~23 industries' owners' halos: 3 circles, stem, rose/ice arcs, white ticks | .25 | – | blended | context | → object: owners' discs (context) |
| `capture.wages` | <=175 blue paths from each market to the cohorts it pays (straight up to the cloud's) | .25 | .17 | blended | context | go |
| `capture.taxes` | ~23 steel drops from the markets to the bedrock, upkeep rings | .25 | .15 | blended | context | go |
| `wave.ribs` | 79 ribs, one a year 1947-2025 (decades .55, fifth years .26, the rest .10 of their own) | .22 | .03 | blended | context | go |
| `activity.beams` | 25 market-value beams with revenue ring, margin arc and $1T ticks | .22 | – | added | context | go |
| `wave.ridges` | 23 industry ridges along time + 4 tier steps | .2 | .11 | blended | context | go |
| `hills.investment` | ~23 gold arcs from the 1%'s pool down to each hill's halo | .2 | .1 | blended | context | go |
| `hills.pool` | the pool's gold ring at the 1%'s cloud | .2 | .07 | blended | context | go |
| `capture.ledger` | 128 cohorts' ledger rings (gold collects, rose pays) | .2 | .17 | blended | context | go |
| `wave.spray` | ~65 spray arcs breaking forward from the crest | .12 | .04 | blended | context | go |
| `capture.ascent` | <=230 gold arcs from the halos to the cohorts that hold the claims, and abroad | .12 | .07 | blended | context | go |
| `wave.lives` | 75 company lifelines (gold need, rose want, ice fear) with birth rings, × and ○ | .1 | – | blended | context | go |
| `hills.crown` | 2 owners' crowns (3 gold rings each), the 1%'s cloud outline and gold threads from summits to it (.08) | .1 | .05 | blended | context | go |
| `capture.roots` | underground amber purchase lines set off by household spending (>= $25B) | .1 | .05 | blended | context | go |
| `activity.threads` | 25 gold threads from the inner circles up to the 1%'s cloud | .08 | .04 | added | context | go |
| `activity.rivalry` | ~20 rivalry arcs between firms of one sector (rose) | .07 | – | added | context | go |
| `foundation.benefits` | ~250 arcs from the pillars' tops to the households served | .06 | .04 | blended | context | go |
| `hills.roots` | 39 underground purchase lines (BEA IO, at least $100B), blue | .055 | .02 | blended | context | go |
| `foundation.regulation` | ~45 strands: grey rules up into each hill, gold capture back down (Jev) | .05 | .04 | blended | context | go |
| traces | wave.fan .04, activity.supply .018 | < .05 | | | context | go |
| `foundation.seats` *(object (strokes))* | officeholders: stick figures with head and base rings | 1 | | added | context | keep as context |
| `activity.markers` *(object (strokes))* | 640 moving markers, 5 per cohort: stick, head ring (asset and debt rings close up) | .6 | | blended | context | **carry**: the spenders, at the rivers' sources |
| `foundation.block` *(object)* | the federal block: strata faces under the wave (its edges and decade ticks added) | .55 | | blended | context | keep as context |
| `wave.face` *(object)* | crest's cut face: 5 tier fills (.06-.22) with steel outlines, gold ridge fill | .25 | | blended | context | keep as context |
| `capture.glow` *(object)* | gold glow discs under those halos | .25 | | blended | context | keep as context |
| *new* | each market as a pool or basin sized by the spending it receives (the bowl's pools, land.pools) | | | | | **object carries the view** |

#### `mind`

| Part | What it draws here | Alpha | ≈ on screen | Blend | Point or context | Proposal |
| --- | --- | --- | --- | --- | --- | --- |
| `hills.mind` | the mind: 10 drive spheres (wireframes), threads to the gate, choice and memory rings, 7 skull rings | 1 | – | blended | **point** | **keep: subject: drive threads to the gate and the choice threads; the 7 skull rings go** |
| `land.road` | road of lives: ~3,500 lifelines (1 line = 100,000 people) and the time axis | .35 | – | added | context | go |
| traces | hills.roots .035, hills.rungs .035, hills.crown .035, hills.investment .035, hills.pool .035 | < .05 | | | context | go |
| *new* | the ten drives as solid spheres sized by share (today wireframes of three rings) | | | | | **object carries the view** |
| *new* | memory as a stack: owned (gold) over owed (hatched) | | | | | **object carries the view** |

#### `society`

| Part | What it draws here | Alpha | ≈ on screen | Blend | Point or context | Proposal |
| --- | --- | --- | --- | --- | --- | --- |
| `hills.ties` | ~1,000 season ties between cohorts (rose cheat to blue cooperate), one arrowhead each | 1 | .4 | blended | **point** | **keep: subject: each cohort's 3 strongest ties, lifted by span; feuds grey and broken** |
| `activity.sparks` | moving sparks along the trunks | 1 | .65 | added | context | go |
| `activity.focus` | one gold ring round the summit most is spent at | 1 | – | added | context | go |
| `activity.halos` | 25 firms' profit halos, 3 gold rings each | .95 | – | blended | context | go |
| `activity.circles` | 25 owners' inner circles: 2 gold rings with 5-22 owner pins | .45 | – | blended | context | go |
| `land.road` | road of lives: ~3,500 lifelines (1 line = 100,000 people) and the time axis | .35 | – | added | context | go |
| `wave.crest` | the crest line, white, 2.4 px, pulsing | .3 | – | blended | context | go |
| `activity.beams` | 25 market-value beams with revenue ring, margin arc and $1T ticks | .22 | – | added | context | go |
| `wave.ridges` | 23 industry ridges along time + 4 tier steps | .2 | .11 | blended | context | go |
| `activity.trunks` | 23 trunks: each market's household spending as a rose/ice ribbon | .2 | – | blended | context | go |
| `wave.spray` | ~65 spray arcs breaking forward from the crest | .12 | .04 | blended | context | go |
| `capture.halo` | ~23 industries' owners' halos: 3 circles, stem, rose/ice arcs, white ticks | .12 | – | blended | context | go |
| `wave.lives` | 75 company lifelines (gold need, rose want, ice fear) with birth rings, × and ○ | .1 | – | blended | context | go |
| `hills.crown` | 2 owners' crowns (3 gold rings each), the 1%'s cloud outline and gold threads from summits to it (.08) | .1 | .05 | blended | context | go |
| `wave.ribs` | 79 ribs, one a year 1947-2025 (decades .55, fifth years .26, the rest .10 of their own) | .08 | .01 | blended | context | go |
| `activity.threads` | 25 gold threads from the inner circles up to the 1%'s cloud | .08 | .04 | added | context | go |
| `activity.rivalry` | ~20 rivalry arcs between firms of one sector (rose) | .07 | – | added | context | go |
| `hills.roots` | 39 underground purchase lines (BEA IO, at least $100B), blue | .055 | .02 | blended | context | go |
| traces | wave.fan .04, wave.future .04, wave.saving .04, activity.trips .035, capture.climb .02 (added), capture.ascent .02, capture.wages .02, capture.taxes .02, capture.ledger .02, capture.roots .02, foundation.strata .02 (added), foundation.pillars .02 (added), foundation.benefits .02, foundation.taxes .02 (added), foundation.regulation .02, activity.supply .018 | < .05 | | | context | go |
| `wave.plate` *(object)* | dark plates behind the wave's names | 1 | | blended | context | keep as context |
| `activity.markers` *(object (strokes))* | 640 moving markers, 5 per cohort: stick, head ring (asset and debt rings close up) | .6 | | blended | **point** | **carry**: cohorts as figures tinted by party, grouped by coalition |
| `foundation.block` *(object)* | the federal block: strata faces under the wave (its edges and decade ticks added) | .25 | | blended | context | keep as context |
| `wave.face` *(object)* | crest's cut face: 5 tier fills (.06-.22) with steel outlines, gold ridge fill | .15 | | blended | context | keep as context |
| `capture.glow` *(object)* | gold glow discs under those halos | .12 | | blended | context | keep as context |
| `foundation.seats` *(object (strokes))* | officeholders: stick figures with head and base rings | .02 | | added | context | go |
| *new* | the 4 coalitions as ground patches or hulls (computed by Coalitions, never drawn) | | | | | **object carries the view** |

#### `foundation`

| Part | What it draws here | Alpha | ≈ on screen | Blend | Point or context | Proposal |
| --- | --- | --- | --- | --- | --- | --- |
| `wave.crest` | the crest line, white, 2.4 px, pulsing | 1 | – | blended | context | go |
| `activity.sparks` | moving sparks along the trunks | 1 | .65 | added | context | go |
| `activity.focus` | one gold ring round the summit most is spent at | 1 | – | added | context | go |
| `capture.taxes` | ~23 steel drops from the markets to the bedrock, upkeep rings | 1 | .6 | blended | context | go |
| `foundation.strata` | strata boundaries along the wake and the receipts line | 1 | .85 | added | **point** | → object: the block's filled layers, named |
| `foundation.pillars` | 6 program pillars: spines with rings | 1 | .8 | added | **point** | → object: solid columns sized by outlays |
| `foundation.taxes` | ~140 drops: every cohort's taxes and each industry's corporate tax | 1 | .55 | added | **point** | **keep: subject: taxes in, bundled by source** |
| `foundation.regulation` | ~45 strands: grey rules up into each hill, gold capture back down (Jev) | 1 | .75 | blended | context | go (a variant of its own) |
| `activity.halos` | 25 firms' profit halos, 3 gold rings each | .95 | – | blended | context | → object: firm tokens (context) |
| `activity.circles` | 25 owners' inner circles: 2 gold rings with 5-22 owner pins | .45 | – | blended | context | go |
| `foundation.benefits` | ~250 arcs from the pillars' tops to the households served | .4 | .28 | blended | **point** | **keep: subject: programs out, bundled per pillar** |
| `land.road` | road of lives: ~3,500 lifelines (1 line = 100,000 people) and the time axis | .35 | – | added | context | go |
| `capture.halo` | ~23 industries' owners' halos: 3 circles, stem, rose/ice arcs, white ticks | .3 | – | blended | context | → object: owners' discs (context) |
| `wave.ribs` | 79 ribs, one a year 1947-2025 (decades .55, fifth years .26, the rest .10 of their own) | .22 | .03 | blended | context | go |
| `activity.beams` | 25 market-value beams with revenue ring, margin arc and $1T ticks | .22 | – | added | context | go |
| `wave.ridges` | 23 industry ridges along time + 4 tier steps | .2 | .11 | blended | context | go |
| `activity.trunks` | 23 trunks: each market's household spending as a rose/ice ribbon | .2 | – | blended | context | go |
| `wave.spray` | ~65 spray arcs breaking forward from the crest | .12 | .04 | blended | context | go |
| `wave.lives` | 75 company lifelines (gold need, rose want, ice fear) with birth rings, × and ○ | .1 | – | blended | context | go |
| `hills.crown` | 2 owners' crowns (3 gold rings each), the 1%'s cloud outline and gold threads from summits to it (.08) | .1 | .05 | blended | context | go |
| `activity.threads` | 25 gold threads from the inner circles up to the 1%'s cloud | .08 | .04 | added | context | go |
| `capture.climb` | ~23 gold rivers climbing each hill to its owners' halo (lines added, ribbon blended) | .08 | – | added | context | go |
| `capture.ascent` | <=230 gold arcs from the halos to the cohorts that hold the claims, and abroad | .08 | .04 | blended | context | go |
| `capture.wages` | <=175 blue paths from each market to the cohorts it pays (straight up to the cloud's) | .08 | .06 | blended | context | go |
| `capture.ledger` | 128 cohorts' ledger rings (gold collects, rose pays) | .08 | .07 | blended | context | go |
| `capture.roots` | underground amber purchase lines set off by household spending (>= $25B) | .08 | .04 | blended | context | go |
| `activity.rivalry` | ~20 rivalry arcs between firms of one sector (rose) | .07 | – | added | context | go |
| `hills.roots` | 39 underground purchase lines (BEA IO, at least $100B), blue | .055 | .02 | blended | context | go |
| traces | wave.fan .04, wave.future .04, wave.saving .04, activity.trips .035, activity.supply .018 | < .05 | | | context | go |
| `wave.face` *(object)* | crest's cut face: 5 tier fills (.06-.22) with steel outlines, gold ridge fill | 1 | | blended | context | keep as context |
| `wave.plate` *(object)* | dark plates behind the wave's names | 1 | | blended | context | keep |
| `foundation.seats` *(object (strokes))* | officeholders: stick figures with head and base rings | 1 | | added | context | **carry**: officeholders as portrait tokens (public figures) |
| `foundation.block` *(object)* | the federal block: strata faces under the wave (its edges and decade ticks added) | .8 | | blended | **point** | **carry**: the bedrock and its strata |
| `activity.markers` *(object (strokes))* | 640 moving markers, 5 per cohort: stick, head ring (asset and debt rings close up) | .6 | | blended | context | **carry**: the households taxed and served |
| `capture.glow` *(object)* | gold glow discs under those halos | .3 | | blended | context | keep as context |
| *new* | the 6 pillars as solid columns | | | | | **object carries the view** |

#### `companies`

| Part | What it draws here | Alpha | ≈ on screen | Blend | Point or context | Proposal |
| --- | --- | --- | --- | --- | --- | --- |
| `land.road` | road of lives: ~3,500 lifelines (1 line = 100,000 people) and the time axis | 1 | – | added | context | go here (it is the white beam across the frame) |
| `wave.crest` | the crest line, white, 2.4 px, pulsing | 1 | – | blended | context | go |
| `wave.lives` | 75 company lifelines (gold need, rose want, ice fear) with birth rings, × and ○ | 1 | – | blended | **point** | **keep: subject: lifelines with × and ○** |
| `activity.circles` | 25 owners' inner circles: 2 gold rings with 5-22 owner pins | 1 | – | blended | context | go |
| `activity.sparks` | moving sparks along the trunks | 1 | .65 | added | context | go |
| `activity.focus` | one gold ring round the summit most is spent at | 1 | – | added | context | go |
| `capture.halo` | ~23 industries' owners' halos: 3 circles, stem, rose/ice arcs, white ticks | 1 | – | blended | context | go |
| `activity.halos` | 25 firms' profit halos, 3 gold rings each | .95 | – | blended | context | → object: firm tokens at the line ends |
| `wave.arrow` | arrow of time at the crest's far end | .85 | .68 | blended | context | go |
| `wave.spray` | ~65 spray arcs breaking forward from the crest | .8 | .24 | blended | context | go |
| `capture.climb` | ~23 gold rivers climbing each hill to its owners' halo (lines added, ribbon blended) | .8 | – | added | context | go |
| `foundation.strata` | strata boundaries along the wake and the receipts line | .7 | .59 | added | context | go |
| `activity.threads` | 25 gold threads from the inner circles up to the 1%'s cloud | .6 | .33 | added | context | go |
| `wave.ribs` | 79 ribs, one a year 1947-2025 (decades .55, fifth years .26, the rest .10 of their own) | .55 | .08 | blended | context | fade to ≤ .12 |
| `wave.ridges` | 23 industry ridges along time + 4 tier steps | .45 | .25 | blended | **point** | **keep: support .45: the industries' ground they run on** |
| `activity.beams` | 25 market-value beams with revenue ring, margin arc and $1T ticks | .22 | – | added | context | go |
| `activity.trunks` | 23 trunks: each market's household spending as a rose/ice ribbon | .2 | – | blended | context | go |
| `capture.ledger` | 128 cohorts' ledger rings (gold collects, rose pays) | .2 | .17 | blended | context | go |
| `capture.wages` | <=175 blue paths from each market to the cohorts it pays (straight up to the cloud's) | .16 | .11 | blended | context | go |
| `hills.crown` | 2 owners' crowns (3 gold rings each), the 1%'s cloud outline and gold threads from summits to it (.08) | .1 | .05 | blended | context | go |
| `capture.taxes` | ~23 steel drops from the markets to the bedrock, upkeep rings | .1 | .06 | blended | context | go |
| `foundation.pillars` | 6 program pillars: spines with rings | .1 | .08 | added | context | go |
| `activity.rivalry` | ~20 rivalry arcs between firms of one sector (rose) | .07 | – | added | context | go |
| `capture.ascent` | <=230 gold arcs from the halos to the cohorts that hold the claims, and abroad | .07 | .04 | blended | context | go |
| `foundation.benefits` | ~250 arcs from the pillars' tops to the households served | .06 | .04 | blended | context | go |
| `hills.roots` | 39 underground purchase lines (BEA IO, at least $100B), blue | .055 | .02 | blended | context | go |
| `foundation.taxes` | ~140 drops: every cohort's taxes and each industry's corporate tax | .05 | .03 | added | context | go |
| `foundation.regulation` | ~45 strands: grey rules up into each hill, gold capture back down (Jev) | .05 | .04 | blended | context | go |
| traces | wave.fan .04, wave.future .04, wave.saving .04, capture.roots .04, activity.trips .035, activity.supply .018 | < .05 | | | context | go |
| `wave.plate` *(object)* | dark plates behind the wave's names | 1 | | blended | context | keep |
| `foundation.seats` *(object (strokes))* | officeholders: stick figures with head and base rings | 1 | | added | context | keep as context |
| `capture.glow` *(object)* | gold glow discs under those halos | .8 | | blended | context | keep as context |
| `activity.markers` *(object (strokes))* | 640 moving markers, 5 per cohort: stick, head ring (asset and debt rings close up) | .6 | | blended | context | keep as context |
| `foundation.block` *(object)* | the federal block: strata faces under the wave (its edges and decade ticks added) | .55 | | blended | context | keep as context |
| `wave.face` *(object)* | crest's cut face: 5 tier fills (.06-.22) with steel outlines, gold ridge fill | .4 | | blended | context | keep as context |
| *new* | a logo or monogram tile at each survivor's line end and at each failure (free logos only) | | | | | **object carries the view** |

#### `social`

| Part | What it draws here | Alpha | ≈ on screen | Blend | Point or context | Proposal |
| --- | --- | --- | --- | --- | --- | --- |
| `land.road` | road of lives: ~3,500 lifelines (1 line = 100,000 people) and the time axis | 1 | – | added | context | go |
| `wave.crest` | the crest line, white, 2.4 px, pulsing | 1 | – | blended | context | go |
| `activity.sparks` | moving sparks along the trunks | 1 | .65 | added | context | go |
| `activity.focus` | one gold ring round the summit most is spent at | 1 | – | added | context | go |
| `capture.halo` | ~23 industries' owners' halos: 3 circles, stem, rose/ice arcs, white ticks | 1 | – | blended | context | → object: owners' discs (context) |
| `social.rings` | category clouds' rings | 1 | .5 | blended | context | → object: category clouds as soft hulls behind the tokens |
| `social.anchors` | stars at a company or office seat, gold ropes to their other firms | 1 | .8 | added | context | → object: the person's token stands at the company or seat; the ropes go |
| `social.loop` | the attention loop: 4 thick arcs (ads, creators, attention, spending) | 1 | .85 | blended | **point** | **keep: subject: the loop's 4 arcs, with arrowheads** |
| `activity.halos` | 25 firms' profit halos, 3 gold rings each | .95 | – | blended | context | → object: firm tokens (context) |
| `social.ties` | mutual follows among the most-followed (white segments) | .9 | .18 | blended | **point** | **keep: support: who follows whom, faint** |
| `social.elite` | rings: followed by the most-followed | .9 | .5 | blended | context | → object: a halo size on the token |
| `capture.climb` | ~23 gold rivers climbing each hill to its owners' halo (lines added, ribbon blended) | .8 | – | added | context | go |
| `foundation.strata` | strata boundaries along the wake and the receipts line | .7 | .59 | added | context | go |
| `social.threads` | ropes from a company or seat up to the person's place in the cloud | .5 | .17 | added | context | go |
| `activity.circles` | 25 owners' inner circles: 2 gold rings with 5-22 owner pins | .45 | – | blended | context | go |
| `foundation.pillars` | 6 program pillars: spines with rings | .35 | .28 | added | context | go |
| `wave.ribs` | 79 ribs, one a year 1947-2025 (decades .55, fifth years .26, the rest .10 of their own) | .22 | .03 | blended | context | go |
| `activity.beams` | 25 market-value beams with revenue ring, margin arc and $1T ticks | .22 | – | added | context | go |
| `wave.ridges` | 23 industry ridges along time + 4 tier steps | .2 | .11 | blended | context | go |
| `activity.trunks` | 23 trunks: each market's household spending as a rose/ice ribbon | .2 | – | blended | context | go |
| `capture.ledger` | 128 cohorts' ledger rings (gold collects, rose pays) | .2 | .17 | blended | context | go |
| `capture.wages` | <=175 blue paths from each market to the cohorts it pays (straight up to the cloud's) | .16 | .11 | blended | context | go |
| `wave.spray` | ~65 spray arcs breaking forward from the crest | .12 | .04 | blended | context | go |
| `wave.lives` | 75 company lifelines (gold need, rose want, ice fear) with birth rings, × and ○ | .1 | – | blended | context | go |
| `hills.crown` | 2 owners' crowns (3 gold rings each), the 1%'s cloud outline and gold threads from summits to it (.08) | .1 | .05 | blended | context | go |
| `capture.taxes` | ~23 steel drops from the markets to the bedrock, upkeep rings | .1 | .06 | blended | context | go |
| `activity.threads` | 25 gold threads from the inner circles up to the 1%'s cloud | .08 | .04 | added | context | go |
| `activity.rivalry` | ~20 rivalry arcs between firms of one sector (rose) | .07 | – | added | context | go |
| `capture.ascent` | <=230 gold arcs from the halos to the cohorts that hold the claims, and abroad | .07 | .04 | blended | context | go |
| `foundation.benefits` | ~250 arcs from the pillars' tops to the households served | .06 | .04 | blended | context | go |
| `hills.roots` | 39 underground purchase lines (BEA IO, at least $100B), blue | .055 | .02 | blended | context | go |
| `foundation.taxes` | ~140 drops: every cohort's taxes and each industry's corporate tax | .05 | .03 | added | context | go |
| `foundation.regulation` | ~45 strands: grey rules up into each hill, gold capture back down (Jev) | .05 | .04 | blended | context | go |
| traces | wave.fan .04, wave.future .04, wave.saving .04, capture.roots .04, activity.trips .035, activity.supply .018 | < .05 | | | context | go |
| `foundation.seats` *(object (strokes))* | officeholders: stick figures with head and base rings | 1 | | added | context | keep as context |
| `social.glow` *(object)* | the attention cloud's glow | 1 | | added | context | keep |
| `social.stars` *(object (strokes))* | ~340 star glyphs (3 strokes and a ring), sized by followers | 1 | | blended | **point** | **carry**: portrait thumbnails (the 60 named), stars for the rest |
| `social.bright` *(object (strokes))* | the 16 most-followed stars | 1 | | blended | **point** | **carry** (as above) |
| `capture.glow` *(object)* | gold glow discs under those halos | .8 | | blended | context | keep as context |
| `activity.markers` *(object (strokes))* | 640 moving markers, 5 per cohort: stick, head ring (asset and debt rings close up) | .6 | | blended | context | keep as context |
| `foundation.block` *(object)* | the federal block: strata faces under the wave (its edges and decade ticks added) | .55 | | blended | context | keep as context |
| `wave.face` *(object)* | crest's cut face: 5 tier fills (.06-.22) with steel outlines, gold ridge fill | .4 | | blended | context | keep as context |
| *new* | company logos at the anchors (free logos or monogram tiles) | | | | | **object carries the view** |

#### `roots`

| Part | What it draws here | Alpha | ≈ on screen | Blend | Point or context | Proposal |
| --- | --- | --- | --- | --- | --- | --- |
| `wave.crest` | the crest line, white, 2.4 px, pulsing | 1 | – | blended | context | go |
| `activity.sparks` | moving sparks along the trunks | 1 | .65 | added | context | go |
| `activity.focus` | one gold ring round the summit most is spent at | 1 | – | added | context | go |
| `capture.roots` | underground amber purchase lines set off by household spending (>= $25B) | 1 | .5 | blended | **point** | merge into the one network |
| `activity.halos` | 25 firms' profit halos, 3 gold rings each | .95 | – | blended | context | go |
| `hills.roots` | 39 underground purchase lines (BEA IO, at least $100B), blue | .75 | .24 | blended | **point** | **keep: subject: one network in the money's direction, with arrowheads** |
| `foundation.strata` | strata boundaries along the wake and the receipts line | .7 | .59 | added | context | go |
| `activity.circles` | 25 owners' inner circles: 2 gold rings with 5-22 owner pins | .45 | – | blended | context | go |
| `wave.ridges` | 23 industry ridges along time + 4 tier steps | .4 | .22 | blended | context | go |
| `land.road` | road of lives: ~3,500 lifelines (1 line = 100,000 people) and the time axis | .35 | – | added | context | go |
| `activity.supply` | ~40 inferred supply tunnels from firms to their suppliers' hills (ice) | .35 | .12 | blended | **point** | go (inferred, not evidence) |
| `capture.halo` | ~23 industries' owners' halos: 3 circles, stem, rose/ice arcs, white ticks | .35 | – | blended | context | go |
| `foundation.pillars` | 6 program pillars: spines with rings | .35 | .28 | added | context | go |
| `wave.lives` | 75 company lifelines (gold need, rose want, ice fear) with birth rings, × and ○ | .25 | – | blended | context | go |
| `wave.ribs` | 79 ribs, one a year 1947-2025 (decades .55, fifth years .26, the rest .10 of their own) | .22 | .03 | blended | context | go |
| `activity.beams` | 25 market-value beams with revenue ring, margin arc and $1T ticks | .22 | – | added | context | go |
| `activity.trunks` | 23 trunks: each market's household spending as a rose/ice ribbon | .2 | – | blended | context | go |
| `wave.spray` | ~65 spray arcs breaking forward from the crest | .12 | .04 | blended | context | go |
| `hills.crown` | 2 owners' crowns (3 gold rings each), the 1%'s cloud outline and gold threads from summits to it (.08) | .1 | .05 | blended | context | go |
| `activity.threads` | 25 gold threads from the inner circles up to the 1%'s cloud | .08 | .04 | added | context | go |
| `activity.rivalry` | ~20 rivalry arcs between firms of one sector (rose) | .07 | – | added | context | go |
| `capture.climb` | ~23 gold rivers climbing each hill to its owners' halo (lines added, ribbon blended) | .06 | – | added | context | go |
| `capture.ascent` | <=230 gold arcs from the halos to the cohorts that hold the claims, and abroad | .06 | .03 | blended | context | go |
| `capture.wages` | <=175 blue paths from each market to the cohorts it pays (straight up to the cloud's) | .06 | .04 | blended | context | go |
| `capture.taxes` | ~23 steel drops from the markets to the bedrock, upkeep rings | .06 | .04 | blended | context | go |
| `capture.ledger` | 128 cohorts' ledger rings (gold collects, rose pays) | .06 | .05 | blended | context | go |
| `foundation.benefits` | ~250 arcs from the pillars' tops to the households served | .06 | .04 | blended | context | go |
| `foundation.taxes` | ~140 drops: every cohort's taxes and each industry's corporate tax | .05 | .03 | added | context | go |
| `foundation.regulation` | ~45 strands: grey rules up into each hill, gold capture back down (Jev) | .05 | .04 | blended | context | go |
| traces | wave.fan .04, wave.future .04, wave.saving .04, activity.trips .035 | < .05 | | | context | go |
| `foundation.seats` *(object (strokes))* | officeholders: stick figures with head and base rings | 1 | | added | context | keep as context |
| `activity.markers` *(object (strokes))* | 640 moving markers, 5 per cohort: stick, head ring (asset and debt rings close up) | .6 | | blended | context | keep as context |
| `foundation.block` *(object)* | the federal block: strata faces under the wave (its edges and decade ticks added) | .55 | | blended | context | keep as context |
| `wave.face` *(object)* | crest's cut face: 5 tier fills (.06-.22) with steel outlines, gold ridge fill | .4 | | blended | context | keep as context |
| `capture.glow` *(object)* | gold glow discs under those halos | .35 | | blended | context | keep as context |
| *new* | industries as solid named nodes (their summits) for the network to join | | | | | **object carries the view** |

#### `betrayal`

| Part | What it draws here | Alpha | ≈ on screen | Blend | Point or context | Proposal |
| --- | --- | --- | --- | --- | --- | --- |
| `hills.ties` | ~1,000 season ties between cohorts (rose cheat to blue cooperate), one arrowhead each | 1 | .4 | blended | **point** | **keep: subject: the incident tie and the 5 hit cohorts' ties only** |
| `activity.sparks` | moving sparks along the trunks | 1 | .65 | added | context | go |
| `activity.focus` | one gold ring round the summit most is spent at | 1 | – | added | context | go |
| `activity.halos` | 25 firms' profit halos, 3 gold rings each | .95 | – | blended | context | go |
| `activity.circles` | 25 owners' inner circles: 2 gold rings with 5-22 owner pins | .45 | – | blended | context | go |
| `land.road` | road of lives: ~3,500 lifelines (1 line = 100,000 people) and the time axis | .35 | – | added | context | go |
| `wave.crest` | the crest line, white, 2.4 px, pulsing | .3 | – | blended | context | go |
| `activity.beams` | 25 market-value beams with revenue ring, margin arc and $1T ticks | .22 | – | added | context | go |
| `wave.ridges` | 23 industry ridges along time + 4 tier steps | .2 | .11 | blended | context | go |
| `activity.trunks` | 23 trunks: each market's household spending as a rose/ice ribbon | .2 | – | blended | context | go |
| `wave.spray` | ~65 spray arcs breaking forward from the crest | .12 | .04 | blended | context | go |
| `capture.halo` | ~23 industries' owners' halos: 3 circles, stem, rose/ice arcs, white ticks | .12 | – | blended | context | go |
| `wave.lives` | 75 company lifelines (gold need, rose want, ice fear) with birth rings, × and ○ | .1 | – | blended | context | go |
| `hills.crown` | 2 owners' crowns (3 gold rings each), the 1%'s cloud outline and gold threads from summits to it (.08) | .1 | .05 | blended | context | go |
| `wave.ribs` | 79 ribs, one a year 1947-2025 (decades .55, fifth years .26, the rest .10 of their own) | .08 | .01 | blended | context | go |
| `activity.threads` | 25 gold threads from the inner circles up to the 1%'s cloud | .08 | .04 | added | context | go |
| `activity.rivalry` | ~20 rivalry arcs between firms of one sector (rose) | .07 | – | added | context | go |
| `hills.roots` | 39 underground purchase lines (BEA IO, at least $100B), blue | .055 | .02 | blended | context | go |
| traces | wave.fan .04, wave.future .04, wave.saving .04, activity.trips .035, capture.climb .02 (added), capture.ascent .02, capture.wages .02, capture.taxes .02, capture.ledger .02, capture.roots .02, foundation.strata .02 (added), foundation.pillars .02 (added), foundation.benefits .02, foundation.taxes .02 (added), foundation.regulation .02, activity.supply .018 | < .05 | | | context | go |
| `wave.plate` *(object)* | dark plates behind the wave's names | 1 | | blended | context | keep as context |
| `activity.markers` *(object (strokes))* | 640 moving markers, 5 per cohort: stick, head ring (asset and debt rings close up) | .6 | | blended | context | **carry**: the betrayer, the betrayed and the 5 hit cohorts |
| `foundation.block` *(object)* | the federal block: strata faces under the wave (its edges and decade ticks added) | .25 | | blended | context | keep as context |
| `wave.face` *(object)* | crest's cut face: 5 tier fills (.06-.22) with steel outlines, gold ridge fill | .15 | | blended | context | keep as context |
| `capture.glow` *(object)* | gold glow discs under those halos | .12 | | blended | context | keep as context |
| `foundation.seats` *(object (strokes))* | officeholders: stick figures with head and base rings | .02 | | added | context | go |
| *new* | cooperation over the rounds as a sparkline in the scene | | | | | **object carries the view** |

#### `y1972`

| Part | What it draws here | Alpha | ≈ on screen | Blend | Point or context | Proposal |
| --- | --- | --- | --- | --- | --- | --- |
| `wave.crest` | the crest line, white, 2.4 px, pulsing | 1 | – | blended | **point** | **keep: subject (1972)** |
| `activity.sparks` | moving sparks along the trunks | 1 | .65 | added | context | go |
| `activity.focus` | one gold ring round the summit most is spent at | 1 | – | added | context | go |
| `capture.halo` | ~23 industries' owners' halos: 3 circles, stem, rose/ice arcs, white ticks | 1 | – | blended | context | → object: owners' discs (context) |
| `activity.halos` | 25 firms' profit halos, 3 gold rings each | .95 | – | blended | context | go (no towers in 1972) |
| `wave.arrow` | arrow of time at the crest's far end | .85 | .68 | blended | context | → object: part of the ghost's label |
| `capture.climb` | ~23 gold rivers climbing each hill to its owners' halo (lines added, ribbon blended) | .8 | – | added | context | go |
| `foundation.strata` | strata boundaries along the wake and the receipts line | .7 | .59 | added | context | go |
| `wave.ghost` | today's crest outlined over the earlier one | .6 | .42 | blended | **point** | **keep: subject (2025, for scale)** |
| `wave.spray` | ~65 spray arcs breaking forward from the crest | .5 | .15 | blended | context | go |
| `activity.circles` | 25 owners' inner circles: 2 gold rings with 5-22 owner pins | .45 | – | blended | context | go |
| `land.road` | road of lives: ~3,500 lifelines (1 line = 100,000 people) and the time axis | .35 | – | added | context | go |
| `foundation.pillars` | 6 program pillars: spines with rings | .35 | .28 | added | context | go |
| `wave.ribs` | 79 ribs, one a year 1947-2025 (decades .55, fifth years .26, the rest .10 of their own) | .22 | .03 | blended | context | fade to ≤ .12 |
| `activity.beams` | 25 market-value beams with revenue ring, margin arc and $1T ticks | .22 | – | added | context | go |
| `wave.ridges` | 23 industry ridges along time + 4 tier steps | .2 | .11 | blended | context | go |
| `activity.trunks` | 23 trunks: each market's household spending as a rose/ice ribbon | .2 | – | blended | context | go |
| `capture.ledger` | 128 cohorts' ledger rings (gold collects, rose pays) | .2 | .17 | blended | context | go |
| `capture.wages` | <=175 blue paths from each market to the cohorts it pays (straight up to the cloud's) | .16 | .11 | blended | context | go |
| `wave.lives` | 75 company lifelines (gold need, rose want, ice fear) with birth rings, × and ○ | .1 | – | blended | context | go |
| `hills.crown` | 2 owners' crowns (3 gold rings each), the 1%'s cloud outline and gold threads from summits to it (.08) | .1 | .05 | blended | context | go |
| `capture.taxes` | ~23 steel drops from the markets to the bedrock, upkeep rings | .1 | .06 | blended | context | go |
| `activity.threads` | 25 gold threads from the inner circles up to the 1%'s cloud | .08 | .04 | added | context | go |
| `activity.rivalry` | ~20 rivalry arcs between firms of one sector (rose) | .07 | – | added | context | go |
| `capture.ascent` | <=230 gold arcs from the halos to the cohorts that hold the claims, and abroad | .07 | .04 | blended | context | go |
| `foundation.benefits` | ~250 arcs from the pillars' tops to the households served | .06 | .04 | blended | context | go |
| `hills.roots` | 39 underground purchase lines (BEA IO, at least $100B), blue | .055 | .02 | blended | context | go |
| `foundation.taxes` | ~140 drops: every cohort's taxes and each industry's corporate tax | .05 | .03 | added | context | go |
| `foundation.regulation` | ~45 strands: grey rules up into each hill, gold capture back down (Jev) | .05 | .04 | blended | context | go |
| traces | wave.fan .04, wave.future .04, wave.saving .04, capture.roots .04, activity.trips .035, activity.supply .018 | < .05 | | | context | go |
| `wave.plate` *(object)* | dark plates behind the wave's names | 1 | | blended | context | keep |
| `foundation.seats` *(object (strokes))* | officeholders: stick figures with head and base rings | 1 | | added | context | keep as context |
| `capture.glow` *(object)* | gold glow discs under those halos | .8 | | blended | context | keep as context |
| `activity.markers` *(object (strokes))* | 640 moving markers, 5 per cohort: stick, head ring (asset and debt rings close up) | .6 | | blended | context | keep as context |
| `foundation.block` *(object)* | the federal block: strata faces under the wave (its edges and decade ticks added) | .55 | | blended | context | keep as context |
| `wave.face` *(object)* | crest's cut face: 5 tier fills (.06-.22) with steel outlines, gold ridge fill | .4 | | blended | **point** | **carry**: tier fills with their then and now shares |

#### `strategy`

| Part | What it draws here | Alpha | ≈ on screen | Blend | Point or context | Proposal |
| --- | --- | --- | --- | --- | --- | --- |
| `activity.circles` | 25 owners' inner circles: 2 gold rings with 5-22 owner pins | 1 | – | blended | context | go |
| `activity.sparks` | moving sparks along the trunks | 1 | .65 | added | context | go |
| `activity.focus` | one gold ring round the summit most is spent at | 1 | – | added | context | go |
| `social.anchors` | stars at a company or office seat, gold ropes to their other firms | 1 | .8 | added | context | go |
| `strategy.streams` | every firm's streams, faint (fills and lines) | 1 | .3 | blended | context | go (the other 24 firms) |
| `strategy.focus` | the focused firm's streams in full (lines added, fills blended) | 1 | – | added | **point** | **keep: subject: the one firm's streams, blended (today added)** |
| `strategy.arrows` | white arrows: which way the money moves the firm (Jev) | 1 | – | blended | **point** | **keep: support: its direction arrows** |
| `strategy.cascade` | the AI cascade from the hyperscalers into chips and data centres | 1 | .8 | blended | context | a variant of its own |
| `strategy.trail` | every firm's 2015-2025 years along its lifeline (lanes, gold stalks, chevrons) | 1 | .7 | blended | context | a variant of its own |
| `activity.halos` | 25 firms' profit halos, 3 gold rings each | .95 | – | blended | context | → object: firm tokens (context) |
| `activity.threads` | 25 gold threads from the inner circles up to the 1%'s cloud | .6 | .33 | added | context | go |
| `wave.crest` | the crest line, white, 2.4 px, pulsing | .45 | – | blended | context | go |
| `capture.halo` | ~23 industries' owners' halos: 3 circles, stem, rose/ice arcs, white ticks | .45 | – | blended | context | → object: owners' discs (context) |
| `wave.lives` | 75 company lifelines (gold need, rose want, ice fear) with birth rings, × and ○ | .4 | – | blended | context | go |
| `wave.ridges` | 23 industry ridges along time + 4 tier steps | .28 | .15 | blended | context | go |
| `activity.beams` | 25 market-value beams with revenue ring, margin arc and $1T ticks | .22 | – | added | context | go |
| `wave.ribs` | 79 ribs, one a year 1947-2025 (decades .55, fifth years .26, the rest .10 of their own) | .2 | .03 | blended | context | go |
| `activity.trunks` | 23 trunks: each market's household spending as a rose/ice ribbon | .2 | – | blended | context | go |
| `social.rings` | category clouds' rings | .15 | .07 | blended | context | go |
| `land.road` | road of lives: ~3,500 lifelines (1 line = 100,000 people) and the time axis | .12 | – | added | context | go |
| `hills.crown` | 2 owners' crowns (3 gold rings each), the 1%'s cloud outline and gold threads from summits to it (.08) | .08 | .04 | blended | context | go |
| `social.threads` | ropes from a company or seat up to the person's place in the cloud | .08 | .03 | added | context | go |
| `activity.rivalry` | ~20 rivalry arcs between firms of one sector (rose) | .07 | – | added | context | go |
| `foundation.regulation` | ~45 strands: grey rules up into each hill, gold capture back down (Jev) | .06 | .04 | blended | context | go |
| `social.elite` | rings: followed by the most-followed | .05 | .03 | blended | context | go |
| traces | activity.trips .035, wave.fan .03, wave.future .03, wave.saving .03, hills.roots .03, hills.rungs .03, hills.investment .03, hills.pool .03, capture.climb .03 (added), capture.ascent .03, capture.wages .03, capture.taxes .03, capture.ledger .03, capture.roots .03, foundation.strata .03 (added), foundation.pillars .03 (added), foundation.benefits .03, foundation.taxes .03 (added), wave.spray .02, activity.supply .018 | < .05 | | | context | go |
| `social.bright` *(object (strokes))* | the 16 most-followed stars | .7 | | blended | context | go |
| `foundation.seats` *(object (strokes))* | officeholders: stick figures with head and base rings | .6 | | added | context | portrait tokens where lobbying lands |
| `capture.glow` *(object)* | gold glow discs under those halos | .45 | | blended | context | keep as context |
| `social.glow` *(object)* | the attention cloud's glow | .4 | | added | context | keep as context |
| `foundation.block` *(object)* | the federal block: strata faces under the wave (its edges and decade ticks added) | .3 | | blended | context | keep as context |
| `social.stars` *(object (strokes))* | ~340 star glyphs (3 strokes and a ring), sized by followers | .25 | | blended | context | go |
| `activity.markers` *(object (strokes))* | 640 moving markers, 5 per cohort: stick, head ring (asset and debt rings close up) | .18 | | blended | context | keep as context |
| `wave.face` *(object)* | crest's cut face: 5 tier fills (.06-.22) with steel outlines, gold ridge fill | .14 | | blended | context | keep as context |
| *new* | the firm as an object (logo tile on a tower sized by its value) | | | | | **object carries the view** |
| *new* | its money's destinations as objects: suppliers' hills, its workers, the owners' cloud, the bedrock | | | | | **object carries the view** |

#### `money`

| Part | What it draws here | Alpha | ≈ on screen | Blend | Point or context | Proposal |
| --- | --- | --- | --- | --- | --- | --- |
| `money.chart` | 4 rate series, the tariff, rules, axis and moment leaders | 1 | – | blended | **point** | **keep: subject: a chart, whose series are the point** |
| `wave.ribs` | 79 ribs, one a year 1947-2025 (decades .55, fifth years .26, the rest .10 of their own) | .1 | .01 | blended | context | go |
| `hills.crown` | 2 owners' crowns (3 gold rings each), the 1%'s cloud outline and gold threads from summits to it (.08) | .08 | .04 | blended | context | go |
| `activity.rivalry` | ~20 rivalry arcs between firms of one sector (rose) | .07 | – | added | context | go |
| `wave.ridges` | 23 industry ridges along time + 4 tier steps | .06 | .03 | blended | context | go |
| `wave.crest` | the crest line, white, 2.4 px, pulsing | .06 | – | blended | context | go |
| traces | hills.roots .03, hills.rungs .03, hills.investment .03, hills.pool .03, foundation.strata .02 (added), foundation.pillars .02 (added), foundation.benefits .02, foundation.taxes .02 (added), foundation.regulation .02, activity.supply .018 | < .05 | | | context | go |
| `money.fill` *(object)* | the trade strip (area) | 1 | | blended | **point** | **carry** (trade strip) |
| `money.backing` *(object)* | the dark panel behind the chart | 1 | | blended | context | keep |
| `activity.markers` *(object (strokes))* | 640 moving markers, 5 per cohort: stick, head ring (asset and debt rings close up) | .18 | | blended | context | keep as context |
| `foundation.block` *(object)* | the federal block: strata faces under the wave (its edges and decade ticks added) | .18 | | blended | context | keep as context |
| `wave.face` *(object)* | crest's cut face: 5 tier fills (.06-.22) with steel outlines, gold ridge fill | .05 | | blended | context | keep as context |
| `foundation.seats` *(object (strokes))* | officeholders: stick figures with head and base rings | .02 | | added | context | go |

#### `regimes`

| Part | What it draws here | Alpha | ≈ on screen | Blend | Point or context | Proposal |
| --- | --- | --- | --- | --- | --- | --- |
| `activity.sparks` | moving sparks along the trunks | 1 | .65 | added | context | go |
| `activity.focus` | one gold ring round the summit most is spent at | 1 | – | added | context | go |
| `regime.strata` | strata lines, revenue (white) and spending (steel) lines, the US depth line | 1 | .8 | blended | context | → object: the bedrock's filled layers |
| `regime.summits` | summit rings split steel/gold, dotted where unranked, leaders to the names | 1 | – | blended | **point** | → object: summit caps (solid) split steel and gold |
| `regime.beams` | steel beams from the bedrock into the summits the state owns | 1 | .8 | blended | **point** | **keep: subject where the state owns summits** |
| `regime.strands` | regulation (grey up) and capture (gold down) strands (Jev) | 1 | .75 | blended | context | go (Jev) |
| `regime.taxes` | ~35 drops: what the state lives on, falling in from where it comes | 1 | .5 | blended | context | go: the strata already show what the state lives on |
| `regime.votes` | 9 white threads from the people into the state (V-Dem) | 1 | .6 | blended | **point** | **keep: support: 9 threads as wide as the democracy index** |
| `regime.trade` | the trade gauge across the far border | 1 | .6 | blended | context | → object: a gauge bar |
| `regime.cloud` | the top 1%'s gold ring (wealth) and white ring (income) | 1 | – | blended | context | → object: a gold disc (wealth) with a white core (income) |
| `wave.crest` | the crest line, white, 2.4 px, pulsing | .45 | – | blended | context | go |
| `wave.ridges` | 23 industry ridges along time + 4 tier steps | .28 | .15 | blended | context | go |
| `wave.ribs` | 79 ribs, one a year 1947-2025 (decades .55, fifth years .26, the rest .10 of their own) | .2 | .03 | blended | context | go |
| `activity.trunks` | 23 trunks: each market's household spending as a rose/ice ribbon | .2 | – | blended | context | go |
| `land.road` | road of lives: ~3,500 lifelines (1 line = 100,000 people) and the time axis | .12 | – | added | context | go |
| `hills.crown` | 2 owners' crowns (3 gold rings each), the 1%'s cloud outline and gold threads from summits to it (.08) | .08 | .04 | blended | context | go |
| `activity.rivalry` | ~20 rivalry arcs between firms of one sector (rose) | .07 | – | added | context | go |
| `wave.lives` | 75 company lifelines (gold need, rose want, ice fear) with birth rings, × and ○ | .05 | – | blended | context | go |
| traces | activity.trips .035, wave.fan .03, wave.future .03, wave.saving .03, hills.roots .03, hills.rungs .03, hills.investment .03, hills.pool .03, wave.spray .02, capture.climb .02 (added), capture.halo .02, capture.ascent .02, capture.wages .02, capture.taxes .02, capture.ledger .02, capture.roots .02, activity.supply .018 | < .05 | | | context | go |
| `regime.bedrock` *(object)* | the regime's bedrock: faces by revenue part, edges | 1 | | blended | **point** | **carry**: depth and strata |
| `activity.markers` *(object (strokes))* | 640 moving markers, 5 per cohort: stick, head ring (asset and debt rings close up) | .18 | | blended | context | keep as context |
| `wave.face` *(object)* | crest's cut face: 5 tier fills (.06-.22) with steel outlines, gold ridge fill | .14 | | blended | context | keep as context |
| `foundation.block` *(object)* | the federal block: strata faces under the wave (its edges and decade ticks added) | .05 | | blended | context | keep as context |
| `capture.glow` *(object)* | gold glow discs under those halos | .02 | | blended | context | go |
| *new* | the state's flag or coat of arms beside its name (public domain) | | | | | **object carries the view** |

#### `mobility`

| Part | What it draws here | Alpha | ≈ on screen | Blend | Point or context | Proposal |
| --- | --- | --- | --- | --- | --- | --- |
| `activity.sparks` | moving sparks along the trunks | 1 | .65 | added | context | go |
| `activity.focus` | one gold ring round the summit most is spent at | 1 | – | added | context | go |
| `mobility.drift` | 128 drift arrows (gold climbed, rose fell, faint held) | 1 | .7 | blended | context | go |
| `mobility.ribbons` | Sankey streams between the fifths (bands, edges and cores) | .9 | .45 | blended | **point** | **keep: subject: a chart, whose streams are the point** |
| `wave.crest` | the crest line, white, 2.4 px, pulsing | .45 | – | blended | context | go |
| `wave.ridges` | 23 industry ridges along time + 4 tier steps | .28 | .15 | blended | context | go |
| `activity.halos` | 25 firms' profit halos, 3 gold rings each | .24 | – | blended | context | go |
| `wave.ribs` | 79 ribs, one a year 1947-2025 (decades .55, fifth years .26, the rest .10 of their own) | .2 | .03 | blended | context | go |
| `activity.trunks` | 23 trunks: each market's household spending as a rose/ice ribbon | .2 | – | blended | context | go |
| `activity.circles` | 25 owners' inner circles: 2 gold rings with 5-22 owner pins | .15 | – | blended | context | go |
| `land.road` | road of lives: ~3,500 lifelines (1 line = 100,000 people) and the time axis | .12 | – | added | context | go |
| `capture.halo` | ~23 industries' owners' halos: 3 circles, stem, rose/ice arcs, white ticks | .1 | – | blended | context | go |
| `hills.crown` | 2 owners' crowns (3 gold rings each), the 1%'s cloud outline and gold threads from summits to it (.08) | .08 | .04 | blended | context | go |
| `activity.rivalry` | ~20 rivalry arcs between firms of one sector (rose) | .07 | – | added | context | go |
| `wave.lives` | 75 company lifelines (gold need, rose want, ice fear) with birth rings, × and ○ | .05 | – | blended | context | go |
| `activity.beams` | 25 market-value beams with revenue ring, margin arc and $1T ticks | .05 | – | added | context | go |
| traces | activity.trips .035, wave.fan .03, wave.future .03, wave.saving .03, hills.roots .03, hills.rungs .03, hills.investment .03, hills.pool .03, wave.spray .02, activity.threads .02 (added), capture.climb .02 (added), capture.ascent .02, capture.wages .02, capture.taxes .02, capture.ledger .02, capture.roots .02, foundation.strata .02 (added), foundation.pillars .02 (added), foundation.benefits .02, foundation.taxes .02 (added), foundation.regulation .02, activity.supply .018 | < .05 | | | context | go |
| `mobility.ladder` *(object (strokes))* | the ladder: rung bars at both ends, lift/pull arrows, event bars, dot plots | 1 | | blended | **point** | **carry**: rung bars, event bars, dot plots |
| `activity.markers` *(object (strokes))* | 640 moving markers, 5 per cohort: stick, head ring (asset and debt rings close up) | .18 | | blended | context | keep as context |
| `foundation.block` *(object)* | the federal block: strata faces under the wave (its edges and decade ticks added) | .18 | | blended | context | keep as context |
| `wave.face` *(object)* | crest's cut face: 5 tier fills (.06-.22) with steel outlines, gold ridge fill | .14 | | blended | context | keep as context |
| `capture.glow` *(object)* | gold glow discs under those halos | .1 | | blended | context | keep as context |
| `foundation.seats` *(object (strokes))* | officeholders: stick figures with head and base rings | .02 | | added | context | go |

#### `atom`

| Part | What it draws here | Alpha | ≈ on screen | Blend | Point or context | Proposal |
| --- | --- | --- | --- | --- | --- | --- |
| `activity.sparks` | moving sparks along the trunks | 1 | .65 | added | context | go |
| `activity.focus` | one gold ring round the summit most is spent at | 1 | – | added | context | go |
| `person.pulls` | pulls: rose arcs to markets, ice arcs from threats (× marks, push arrows), income arcs | 1 | – | blended | **point** | **keep: subject: desire and fear pulls; income arcs as support** |
| `activity.orgs.employer` | the followed person's employer's org chart (figures and reporting lines) | .9 | – | added | context | → object: the employer as a logo tile |
| `wave.ridges` | 23 industry ridges along time + 4 tier steps | .25 | .14 | blended | context | go |
| `capture.halo` | ~23 industries' owners' halos: 3 circles, stem, rose/ice arcs, white ticks | .25 | – | blended | context | go |
| `wave.crest` | the crest line, white, 2.4 px, pulsing | .22 | – | blended | context | go |
| `land.road` | road of lives: ~3,500 lifelines (1 line = 100,000 people) and the time axis | .2 | – | added | context | go |
| `activity.trunks` | 23 trunks: each market's household spending as a rose/ice ribbon | .2 | – | blended | context | go |
| `person.ladder` | the life's path across the wake, the parents' ghost, event stalks and spheres, the walker | .18 | – | blended | context | go |
| `activity.circles` | 25 owners' inner circles: 2 gold rings with 5-22 owner pins | .15 | – | blended | context | go |
| `activity.halos` | 25 firms' profit halos, 3 gold rings each | .14 | – | blended | context | go |
| `wave.ribs` | 79 ribs, one a year 1947-2025 (decades .55, fifth years .26, the rest .10 of their own) | .14 | .02 | blended | context | go |
| `hills.crown` | 2 owners' crowns (3 gold rings each), the 1%'s cloud outline and gold threads from summits to it (.08) | .12 | .06 | blended | context | go |
| `foundation.pillars` | 6 program pillars: spines with rings | .12 | .1 | added | context | go |
| `activity.rivalry` | ~20 rivalry arcs between firms of one sector (rose) | .07 | – | added | context | go |
| `activity.beams` | 25 market-value beams with revenue ring, margin arc and $1T ticks | .05 | – | added | context | go |
| traces | wave.spray .04, wave.lives .04, wave.fan .04, wave.future .04, wave.saving .04, activity.trips .035, hills.roots .03, hills.rungs .03, hills.investment .03, hills.pool .03, activity.threads .02 (added), capture.climb .02 (added), capture.ascent .02, capture.wages .02, capture.taxes .02, capture.ledger .02, capture.roots .02, foundation.strata .02 (added), foundation.benefits .02, foundation.taxes .02 (added), foundation.regulation .02, activity.supply .018 | < .05 | | | context | go |
| `person.atom` *(object (strokes))* | the person's figure and rings, the mind above (wireframe spheres, threads), the spine | 1 | | blended | **point** | **carry**: the person as a large generic figure icon; the mind's drives as solid spheres |
| `capture.glow` *(object)* | gold glow discs under those halos | .25 | | blended | context | keep as context |
| `activity.markers` *(object (strokes))* | 640 moving markers, 5 per cohort: stick, head ring (asset and debt rings close up) | .15 | | blended | context | go |
| `wave.face` *(object)* | crest's cut face: 5 tier fills (.06-.22) with steel outlines, gold ridge fill | .12 | | blended | context | keep as context |
| `foundation.block` *(object)* | the federal block: strata faces under the wave (its edges and decade ticks added) | .12 | | blended | context | keep as context |
| `foundation.seats` *(object (strokes))* | officeholders: stick figures with head and base rings | .02 | | added | context | go |
| *new* | markets and threats as icon tokens where the pulls end (food, home, insurance; job loss, illness) | | | | | **object carries the view** |

#### `dilemma`

| Part | What it draws here | Alpha | ≈ on screen | Blend | Point or context | Proposal |
| --- | --- | --- | --- | --- | --- | --- |
| `activity.sparks` | moving sparks along the trunks | 1 | .65 | added | context | go |
| `activity.focus` | one gold ring round the summit most is spent at | 1 | – | added | context | go |
| `dilemma.moves` | strips of moves, payoff bars, tribe rings and their bridge | 1 | – | blended | **point** | **keep: support: the strips and the tribes' bridge** |
| `dilemma.pulses` | pulses along the links (blue cooperate, red defect) | 1 | – | blended | **point** | **keep: subject: the moves along the links** |
| `activity.orgs.employer` | the followed person's employer's org chart (figures and reporting lines) | .9 | – | added | context | go |
| `wave.ridges` | 23 industry ridges along time + 4 tier steps | .25 | .14 | blended | context | go |
| `capture.halo` | ~23 industries' owners' halos: 3 circles, stem, rose/ice arcs, white ticks | .25 | – | blended | context | go |
| `wave.crest` | the crest line, white, 2.4 px, pulsing | .22 | – | blended | context | go |
| `land.road` | road of lives: ~3,500 lifelines (1 line = 100,000 people) and the time axis | .2 | – | added | context | go |
| `activity.trunks` | 23 trunks: each market's household spending as a rose/ice ribbon | .2 | – | blended | context | go |
| `activity.circles` | 25 owners' inner circles: 2 gold rings with 5-22 owner pins | .15 | – | blended | context | go |
| `activity.halos` | 25 firms' profit halos, 3 gold rings each | .14 | – | blended | context | go |
| `wave.ribs` | 79 ribs, one a year 1947-2025 (decades .55, fifth years .26, the rest .10 of their own) | .14 | .02 | blended | context | go |
| `hills.crown` | 2 owners' crowns (3 gold rings each), the 1%'s cloud outline and gold threads from summits to it (.08) | .12 | .06 | blended | context | go |
| `foundation.pillars` | 6 program pillars: spines with rings | .12 | .1 | added | context | go |
| `activity.rivalry` | ~20 rivalry arcs between firms of one sector (rose) | .07 | – | added | context | go |
| `person.pulls` | pulls: rose arcs to markets, ice arcs from threats (× marks, push arrows), income arcs | .06 | – | blended | context | go |
| `activity.beams` | 25 market-value beams with revenue ring, margin arc and $1T ticks | .05 | – | added | context | go |
| traces | wave.spray .04, wave.lives .04, wave.fan .04, wave.future .04, wave.saving .04, activity.trips .035, hills.roots .03, hills.rungs .03, hills.investment .03, hills.pool .03, activity.threads .02 (added), capture.climb .02 (added), capture.ascent .02, capture.wages .02, capture.taxes .02, capture.ledger .02, capture.roots .02, foundation.strata .02 (added), foundation.benefits .02, foundation.taxes .02 (added), foundation.regulation .02, activity.supply .018 | < .05 | | | context | go |
| `dilemma.arena` *(object (strokes))* | arena: 6 figures, links to the seats, floor rings, threads to the cohorts' places | 1 | | blended | **point** | **carry**: the six figures (a generic icon per role) |
| `dilemma.floor` *(object)* | the arena's floor disc | 1 | | blended | context | keep |
| `capture.glow` *(object)* | gold glow discs under those halos | .25 | | blended | context | keep as context |
| `person.atom` *(object (strokes))* | the person's figure and rings, the mind above (wireframe spheres, threads), the spine | .22 | | blended | context | go here |
| `activity.markers` *(object (strokes))* | 640 moving markers, 5 per cohort: stick, head ring (asset and debt rings close up) | .15 | | blended | context | go |
| `wave.face` *(object)* | crest's cut face: 5 tier fills (.06-.22) with steel outlines, gold ridge fill | .12 | | blended | context | keep as context |
| `foundation.block` *(object)* | the federal block: strata faces under the wave (its edges and decade ticks added) | .12 | | blended | context | keep as context |
| `foundation.seats` *(object (strokes))* | officeholders: stick figures with head and base rings | .02 | | added | context | go |

#### `ladder`

| Part | What it draws here | Alpha | ≈ on screen | Blend | Point or context | Proposal |
| --- | --- | --- | --- | --- | --- | --- |
| `activity.sparks` | moving sparks along the trunks | 1 | .65 | added | context | go |
| `activity.focus` | one gold ring round the summit most is spent at | 1 | – | added | context | go |
| `person.ladder` | the life's path across the wake, the parents' ghost, event stalks and spheres, the walker | 1 | – | blended | **point** | **keep: subject: the path and the parents' ghost** |
| `activity.orgs.employer` | the followed person's employer's org chart (figures and reporting lines) | .9 | – | added | context | go |
| `wave.crest` | the crest line, white, 2.4 px, pulsing | .6 | – | blended | context | go |
| `wave.ridges` | 23 industry ridges along time + 4 tier steps | .55 | .3 | blended | context | go |
| `wave.ribs` | 79 ribs, one a year 1947-2025 (decades .55, fifth years .26, the rest .10 of their own) | .5 | .07 | blended | context | fade to ≤ .12 |
| `land.road` | road of lives: ~3,500 lifelines (1 line = 100,000 people) and the time axis | .4 | – | added | context | go |
| `hills.rungs` | 6 rung arcs up the most populated hill (the class legend) | .35 | .19 | blended | context | fade to ≤ .12 |
| `capture.halo` | ~23 industries' owners' halos: 3 circles, stem, rose/ice arcs, white ticks | .25 | – | blended | context | go |
| `activity.trunks` | 23 trunks: each market's household spending as a rose/ice ribbon | .2 | – | blended | context | go |
| `activity.circles` | 25 owners' inner circles: 2 gold rings with 5-22 owner pins | .15 | – | blended | context | go |
| `activity.halos` | 25 firms' profit halos, 3 gold rings each | .14 | – | blended | context | go |
| `hills.crown` | 2 owners' crowns (3 gold rings each), the 1%'s cloud outline and gold threads from summits to it (.08) | .12 | .06 | blended | context | go |
| `foundation.pillars` | 6 program pillars: spines with rings | .12 | .1 | added | context | go |
| `activity.rivalry` | ~20 rivalry arcs between firms of one sector (rose) | .07 | – | added | context | go |
| `activity.beams` | 25 market-value beams with revenue ring, margin arc and $1T ticks | .05 | – | added | context | go |
| traces | wave.spray .04, wave.lives .04, wave.fan .04, wave.future .04, wave.saving .04, activity.trips .035, hills.roots .03, hills.investment .03, hills.pool .03, activity.threads .02 (added), capture.climb .02 (added), capture.ascent .02, capture.wages .02, capture.taxes .02, capture.ledger .02, capture.roots .02, foundation.strata .02 (added), foundation.benefits .02, foundation.taxes .02 (added), foundation.regulation .02, activity.supply .018 | < .05 | | | context | go |
| `wave.face` *(object)* | crest's cut face: 5 tier fills (.06-.22) with steel outlines, gold ridge fill | .3 | | blended | context | keep as context |
| `capture.glow` *(object)* | gold glow discs under those halos | .25 | | blended | context | keep as context |
| `activity.markers` *(object (strokes))* | 640 moving markers, 5 per cohort: stick, head ring (asset and debt rings close up) | .15 | | blended | context | go |
| `foundation.block` *(object)* | the federal block: strata faces under the wave (its edges and decade ticks added) | .12 | | blended | context | keep as context |
| `person.atom` *(object (strokes))* | the person's figure and rings, the mind above (wireframe spheres, threads), the spine | .12 | | blended | context | go here |
| `foundation.seats` *(object (strokes))* | officeholders: stick figures with head and base rings | .02 | | added | context | go |
| *new* | event icons instead of stalks and spheres (job lost, child, home) | | | | | **object carries the view** |

#### `you`

| Part | What it draws here | Alpha | ≈ on screen | Blend | Point or context | Proposal |
| --- | --- | --- | --- | --- | --- | --- |
| `activity.sparks` | moving sparks along the trunks | 1 | .65 | added | context | go |
| `activity.focus` | one gold ring round the summit most is spent at | 1 | – | added | context | go |
| `you.rivers` | your spending's rivers to the markets (rose/ice lanes) | 1 | .8 | blended | **point** | **keep: support: your spending's two lanes** |
| `you.flow` | gold up, blue down: income going to those ranked above and below | 1 | .8 | blended | **point** | → object: a gauge beside the figure ("86% goes to those ranked above") |
| `you.futures` | the ruler of fifths and the white ribbons to each | 1 | .8 | blended | **point** | **keep: subject: the 5 ribbons to the fifths** |
| `wave.crest` | the crest line, white, 2.4 px, pulsing | .45 | – | blended | context | go |
| `capture.halo` | ~23 industries' owners' halos: 3 circles, stem, rose/ice arcs, white ticks | .35 | – | blended | context | go |
| `wave.ridges` | 23 industry ridges along time + 4 tier steps | .28 | .15 | blended | context | go |
| `mobility.drift` | 128 drift arrows (gold climbed, rose fell, faint held) | .25 | .17 | blended | context | go |
| `activity.halos` | 25 firms' profit halos, 3 gold rings each | .24 | – | blended | context | go |
| `wave.ribs` | 79 ribs, one a year 1947-2025 (decades .55, fifth years .26, the rest .10 of their own) | .2 | .03 | blended | context | go |
| `activity.trunks` | 23 trunks: each market's household spending as a rose/ice ribbon | .2 | – | blended | context | go |
| `activity.circles` | 25 owners' inner circles: 2 gold rings with 5-22 owner pins | .15 | – | blended | context | go |
| `land.road` | road of lives: ~3,500 lifelines (1 line = 100,000 people) and the time axis | .12 | – | added | context | go |
| `mobility.ribbons` | Sankey streams between the fifths (bands, edges and cores) | .12 | .06 | blended | context | go |
| `hills.crown` | 2 owners' crowns (3 gold rings each), the 1%'s cloud outline and gold threads from summits to it (.08) | .08 | .04 | blended | context | go |
| `activity.rivalry` | ~20 rivalry arcs between firms of one sector (rose) | .07 | – | added | context | go |
| `wave.lives` | 75 company lifelines (gold need, rose want, ice fear) with birth rings, × and ○ | .05 | – | blended | context | go |
| `activity.beams` | 25 market-value beams with revenue ring, margin arc and $1T ticks | .05 | – | added | context | go |
| traces | activity.trips .035, wave.fan .03, wave.future .03, wave.saving .03, hills.roots .03, hills.rungs .03, hills.investment .03, hills.pool .03, capture.climb .03 (added), capture.ascent .03, capture.wages .03, capture.taxes .03, capture.ledger .03, capture.roots .03, wave.spray .02, activity.threads .02 (added), foundation.strata .02 (added), foundation.pillars .02 (added), foundation.benefits .02, foundation.taxes .02 (added), foundation.regulation .02, activity.supply .018 | < .05 | | | context | go |
| `you.figure` *(object (strokes))* | your figure and the cushion ring at its feet | 1 | | blended | **point** | **carry**: a generic figure icon at your rung; the cushion as a filled disc |
| `capture.glow` *(object)* | gold glow discs under those halos | .35 | | blended | context | keep as context |
| `activity.markers` *(object (strokes))* | 640 moving markers, 5 per cohort: stick, head ring (asset and debt rings close up) | .18 | | blended | context | keep as context |
| `foundation.block` *(object)* | the federal block: strata faces under the wave (its edges and decade ticks added) | .18 | | blended | context | keep as context |
| `wave.face` *(object)* | crest's cut face: 5 tier fills (.06-.22) with steel outlines, gold ridge fill | .14 | | blended | context | keep as context |
| `mobility.ladder` *(object (strokes))* | the ladder: rung bars at both ends, lift/pull arrows, event bars, dot plots | .12 | | blended | context | keep as context |
| `foundation.seats` *(object (strokes))* | officeholders: stick figures with head and base rings | .02 | | added | context | go |

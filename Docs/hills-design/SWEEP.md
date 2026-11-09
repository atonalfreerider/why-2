# The sweep through time, as built (8 Nov 2026)

The user's eighth direction is verbatim in `Docs/research/advantage/brief-v6.md`: redraw "the economy sweep through time" (the
Wave view, preset `overview`, key 1) in the point-cloud language of the advantage, history and politics views (bodies with
outlines and occlusion), and track every major player in every industry, not a hand-picked or tech-only set. No narration or
video is re-recorded until the user approves the redesign. This file is the design as built; the judged build spec it follows
(three concepts, a judge, the "strata" concept with grafts) is the workflow's, and where the build departs from it the reason
is under "Departures from the spec".

**Code.** `Assets/Scripts/Economy/Layers/HillSweepLayer.cs` (the view, Order 69), `Assets/Scripts/Economy/Land/SweepLayout.cs`
(the per-year layout, pure), `Assets/Scripts/Economy/Model/Companies.cs` (the roster). **A/B switch:** `-classicwave` on the
command line brings back the old wave (ridges, ribs, gold lifelines) in the Wave view, for comparison frames before approval;
every hook below reads `HillSweepLayer.On`.

## What is drawn

Seen straight from the future (yaw 180) and 8 degrees from above (18 on a portrait screen), the year shown is a cross-section
square to the camera, and the past slants back to the left, up and deeper:

- **Industries.** The 25 industries of `industries.json` are point-cloud bodies (`Organism`, one lobe, detail 3, three skin
  points under each surface point), each as large as its share of that year's GDP, in its tier's warm stone lightened toward
  the ink (the government's slate). An industry with no value added (software before 1969) draws no body and is named
  "none yet". Each body's silhouette is a solid outline, dotted where the year's value added is an estimated split
  (`EstimatedSplits`: the government's split before 1963; banking, finance, insurance, other services, internet, media and
  telecom split by 1963 shares before 1963; legal and professional services before 1963; software and media and telecom before
  2017 and in 2025, which keeps 2024's share of the publishing line; every industry in 2026).
- **Companies.** The roster's companies (`Companies`: Jev's major players and the 75 curated lives) stand around their industry
  as gold bodies, as large as their year-end market value as a share of that year's GDP, packed so a cluster grows sideways
  (below). The 60 largest of a year, each industry's largest and the selected one are full bodies (outlined); the others of
  3 units or more are small bodies; smaller ones are dots of twelve points. A company that existed that year with no value
  draws nothing: its industry's name and the title count it ("+24 not measured"; the hover card "(+24 that existed, not
  measured)"). Companies stand 6 units nearer the camera than their industry, so their occluders hide its rim: a cluster is
  grouped by occlusion, not by a line.
- **Outlines and occlusion.** Every body is three renderers: its cloud, its outline (baked into a small mesh from the view's
  direction, solid or dotted) and its occluder (`PointBodies.Occluder`, depth only at queue 3001), so streams, packets and
  bodies behind it are hidden.
- **Strata.** Top to bottom by the households' share of each stratum's sales, measured at load from `circuit.json`'s 2024 Use
  table (personal consumption over intermediate sales plus every final use): services .502, manufacturing and
  infrastructure .331, tech .223, raw materials .218, and the government .027, always last as the base. Within a stratum the
  most household-facing industry stands at the screen's right. Services' eleven industries stand in two rows (one on a
  portrait screen, below). Each row keeps the room its labels take at the fitted scale (below: "The stack"), so a row's names
  never stand in the next row, and the strata stand further apart than any two rows of one stratum.
- **Time.** One stream per stratum: a ring of dots a year from 1947 to the year before the one shown, as wide as 0.3 of a body
  of the stratum's value added that year, on a slant from the stratum's departure: L = 18 min(Δ, 25) + 8 max(0, Δ - 25) units
  to the left, 0.28 L higher and 4 Δ deeper for a year Δ back, fading to a quarter over 25 years and to 0.6 of that over 50
  more (politics' `Past` and `Age`). The rise is the same for every stream and every year, a way into the depth and not a
  value: it carries the far years up into the screen's top left, which stood empty, and the government's stream clear of the
  legend. A ring that would stand under one of the HUD's blocks at the solved pose is not drawn (`UnderHud`; none in the
  port's frames). The 2026 estimate's rings would be faint and half-dotted. The years 1950, 1975 and 2000 are named on the
  top stream (never within eight years of the year shown).
- **Peaks.** An industry whose largest measured share of GDP since 1947 is at least 1.4 times its share now has a faint dotted
  ring at that peak's size around its body (at most six, the largest ratios first); the one that fell furthest in points of
  GDP is named on a landscape screen ("Manufacturing: 23.4% of GDP in 1953"). In 2025: agriculture (1948), mining and metals (1951), manufacturing
  (1953), oil and gas (1981), the federal government (1970) and computers and chips (1997). Estimated years are never peaks.
- **Tails.** The hovered (else the selected) industry or company shows its own rings back through its years, 0.3 of each
  year's radius, on the same slant from where it stands now; dotted and faint where that year's value is approximate, nothing
  where it has none. One tail at a time.
- **Fates.** When the year passes a company's end: a bankrupt one falls 30 units and fades (0.9 s), an acquired or merged one
  slides into its successor (or its industry's centre) and shrinks (0.6 s), a broken-up one fades in place while its successors
  grow. For the end year and the two after, a grey hairline ghost stands at its last place in its cluster (beside its industry
  where it stands that year) and its size, the largest two named on a landscape screen ("Enron · bankrupt 2001").
- **The column right of the stack.** The owners' four dotted gold circles (top 1%, next 9%, next 40%, bottom 50%), each
  22 √(its share of household wealth) across (`circuit.json` groupHistory: the Fed's DFA from 1989, recalled Saez-Zucman shares
  1950-1988 drawn fainter with "≈", 1950's held before), and the households, a fixed blue node (a node, not a measure).
  Their names stand to their right: "Owners' income $6.53T / top 1% hold 31% of wealth / circles: top 1%, next 9%, 40%, 50%"
  and "Households · spending $21.0T / pay $13.3T" (portrait: "Owners' income / $6.53T / top 1% hold 31%", "Households /
  spend $21.0T"); the government's name carries the year's benefits ("the base · benefits $4.85T").
- **Motion at a frozen year: the measured circuit.** Eight packet lanes on the flow clock, from `InstantModel.Year(y)` (the
  national accounts of that year; 2026 shows 2025's, fainter and labelled "(2025)"): spending (households to the stack's
  services edge; its split by motive is judged and is not drawn), pay (the stack to the households), owners' income (the stack
  to the owners), taxes from industries and the state's purchases (the tech edge and the government, a pair), benefits, taxes
  from households and public pay (the government and the households). Each lane is an arc bowed out to the right and toward
  the camera, as wide as 0.22 ∛ of its dollars at 2025's scale, its packets 3-5 px at 0.5-2.5 a second (rate by log size,
  never the amount), 12 units a second. A lane whose total is not measured would draw a dotted band and "≈". Nothing moves
  between companies or industries: no such flow is measured year by year.

## The scale: one rule for every year

Every size is a share of that year's GDP drawn at 2025's scale: X = value / GDP(year) × GDP(2025) ($30,762bn). No deflator is
needed; every label gives that year's own nominal dollars.

- **Industry:** R = 2.4 ∛X (ContestNetwork.Radius). 2025: real estate 38.8, trade 37.5, professional services 34.4, state and
  local government 31.9, federal 24.8; mining and metals the smallest, 13.1. 1950: manufacturing 45.7.
- **Company:** r = 1.512 ∛X: a quarter of an industry's volume per dollar (1.512 = 2.4 ∛¼), because market value is a stock and
  value added a year's flow; the key says so. NVIDIA at $5.49T: 26.7; $100bn: 7.0; $10bn: 3.3; $2bn: 1.9.
- **Streams:** ρ = 0.3 × 2.4 ∛X(stratum's value added): services 13.7 in 1950, 17.7 in 2025.

## The layout (`SweepLayout`, every year 1947-2026 solved at load, landscape and portrait)

- **Packing.** Each industry's companies are packed in a fixed order, the largest share of GDP they ever reached first (so a
  company keeps its place from year to year), each on a flank by its rank (even left, odd right). Its candidate spots are 20
  rings out from the industry (the first overlapping the industry's rim by a quarter of the smaller radius, then steps of 0.8
  of its own radius) at every 12 degrees up to 84 either side of its flank; it takes the free spot nearest by an ellipse 2.5
  times as tall as wide, keeping 1.5 units plus 6% of the two radii between companies. A company with no free spot goes to
  further rings all round (none in the test roster). A portrait screen's clusters flatten less (1.8: its rows are one a
  stratum, so its width binds).
- **Dots** stand 2-6 units outside the bodies' box at angles hashed from their ids. Places are kept relative to the industry
  (`Placed.Rel`); the stack moves them into place.
- **Rows** (`Arrange`, at load): at most six industries a row on a landscape screen (services' eleven in two), one row a
  stratum on a portrait one; each row as tall as its tallest cluster.
- **The stack** (`Stack`, on the main thread at every rebuild, for a `Room`: the labels' room in units at the fitted scale):
  across, each cluster in a slot widened on both sides to its name's width where the row has room (at most the widest row
  the free rect gives at that scale), the largest industries first, flush to x = 0 and 10 units apart; the eight largest
  companies' names (four on a portrait screen) a tier over their bodies, a second tier 28 px higher where two names of a row
  would meet; down, under each row its industries' names (40 px: two lines and 4 clear; portrait one line, 24, and only under
  rows that name one), then 4 units within a stratum, or between strata 12 units and a named company's room (22 px) whether or
  not the next row names one, so a gap between strata is always at least 22 px and 8 units wider than any within one. A
  landscape stratum too short for its name over its stream is padded above and below. On a portrait screen each stratum's
  heading stands over its first row (24 px), and between strata 2 units.
- **The column** stands at x = +75: the owners' circles from the top row's top down, the households at the first
  manufacturing row's height.
- **The streams** depart 8 units left of the stack's widest row: one left edge. On a landscape screen each stratum's name
  ("SERVICES / 48.3% of GDP · households buy 50% of its sales (2024)"; a click opens its close-up) stands right-aligned at that
  edge over its stream, the pair centred on the stratum's rows, so a name stands level with its own rows. On a portrait
  screen the name is a heading over the stratum's first row ("SERVICES · 48.3% of GDP"), right-aligned at the stack's right
  edge, and the stream departs at the stratum's middle.
- **The title** stands over the top row's named companies' room, centred on the stack.

## The camera and the stack's fit (`FitStack`)

A label's pixels are fixed and the units it takes depend on the fitted scale, which depends on the stack, so every rebuild
(before any body is sent to its place) stands the stack for a scale (the last fit's, or a guess: 0.85 px a unit, portrait
0.75), fits the essentials with politics' fit (`Solve`, `Measure`, `LargestFree`, `MeasureHud`), and stands it again for the
fitted scale until the two agree within 1.5% (at most ten passes; the free rect of the first pass serves the passes after it).
Then the industries' names' slots are given at that scale and the stack fitted again until it agrees. The labels' widths come
from their texts (`TextPx`: about TextMeshPro's widths, half an em a letter, capitals, digits and narrow marks their own). The
essentials: every row's box with its labels' room, each stratum's name with its width to the left (portrait: its heading), the
streams' last 10 years (portrait 6; the rest run on to the left), the owners' circles and the households with their names to
their right, the circuit's arcs and the title with its width. A year's change refits (0.8 s) only when the solved distance
moves 8% or the essentials' box 40 px, so the replay does not sway the frame. `FlyToState` (a focus, a state, the HUD's blocks
moving) rebuilds and fits again. A stratum's close-up keeps the view's stack and fits its own camera to its rows. The outlines
are baked from the view's direction at load and baked again, ten meshes a frame on the main thread, once the camera settles more
than a degree from where they were baked (an orbit, a portrait screen).

## Data

- **`companies.json`** (Tools/companies/build.py, the data track): `{asOf, note, industries, companies: [{id, name, ticker,
  industry, judged {industry, confidence, major, majorConfidence} or null, major, curated, founded, ended, fate, successor,
  sells, value: [[year, usdBn, "m" or "a", fact or null], ...]}]}`. "m" values are gated facts (CompaniesMarketCap's year-end
  history, 1996 on); "a" are company-lives.json's recalled waypoints, kept outside the measured span. A company is drawn when it
  is major or curated and its industry is one of the 25.
- **The fallback** (before companies.json lands): company-lives.json's 75 lives, every value "a" in nominal dollars, curated,
  not major, not judged. The title then says "N companies, approximate" and the coverage note "Every market value here is
  recalled (≈ ±30-50%): the measured roster is not built yet".
- **`industries.json`**: GDP and value added by industry, 1947-2026 (2026 an estimate). **`circuit.json`**: the 2024 Use table
  (the strata's order) and the wealth shares (the owners). **`InstantModel`**: the year's national accounts (the circuit).
- **Jev's judgments** (which industry earned most of a company's revenue; how major a player it was) appear only on the hover
  card, in italics with "J 0.82", "Jev was unsure" under 0.5, both industries when the drawn one is not Jev's. They place a
  company only through the file's industry field and size nothing. The build makes no Jev calls.

## Honesty rules (`Companies.ValueAt`, year-end, nominal $bn)

| Case | Kind | Drawn | Label |
|---|---|---|---|
| an "m" entry for the year | Measured | solid gold, solid outline | "NVIDIA $5.49T" |
| an "a" entry for the year | Approx | alpha × 0.55, points 0.3 toward the no-data grey, dotted outline | "≈ $95bn"; card "recalled, ±30-50%" |
| between two entries at most 15 years apart (geometric) | Interpolated | as approximate, whatever its ends | "≈" |
| a year after the last entry | Held | as that entry was known | "(end of 2025)" |
| before the first entry, across a longer gap, after it ended | None | not drawn | while it existed, counted in its industry's name and the title, "+N not measured" |

The smoke test fails a body drawn as measured without an "m" entry that year.

## Interaction

Hover shows a card (`HistoryPopups`: the actors' atlas logo or a monogram; the industry, the value with its kind and year and
its share of GDP, its founding, fate and successor, what it sells, Jev's judgments). A click selects (its tail, the rest at
0.35) or clears on empty ground. A stratum's name opens its close-up (its rows fitted, every company of $10bn or more at 2025's
scale named, at most 60; the other strata at 0.15); Escape or the title returns. States, for cues and the smoke test:
`sweep:<year>[:in=<stratum>][:sel=<id>]` (the year pinned until the chip moves).

## Labels (HillLabels; at most 46 at 1080p, 30 on a portrait screen or in a headset; each at its thing or culled)

No label is sent into another row: the industries' names, the strata's, the peak and the fates have no fallbacks (the label
system's own four candidates, 1.2 em away, remain), a company's name has one, its second tier. An industry's name that would
meet a larger industry's name in its row at the camera's scale is not drawn at all (`Unmet`): the label system would move it
1.2 em aside or up, and up is onto its own cluster (a portrait row, whose slots seldom widen to the names, had one to three a
frame). On a portrait screen a name hangs 10 px under its cluster and a heading stands 10 px over its point (12 and 12 left
1-5 px less than the label system's 8 px between two labels, and the names under a row's right end were moved up onto their
clusters), and the owners' name stands at their top circle's top (at the circles' middle it met the top row's last name).
Placed by role and rank:

- **Title** (500): "The US economy in 2025 · GDP $30.8T · 25 industries · 162 companies (155 major) · +1 not measured" ("N major
  companies" when every company drawn is a major player, the fallback's "N companies, approximate"), with the coverage note
  under it in the dim ink before the first measured year or on the fallback ("Market values measured from 1996; earlier, 67
  companies' values are recalled (≈)"). Portrait: "The US economy in 2025 · GDP $30.8T" / "25 industries · 162 companies (155
  major) · +1 not measured" (before 1996: "26 companies, values recalled (≈) · +134 not measured").
- **Strata** (450): landscape "SERVICES" / "48.3% of GDP · households buy 50% of its sales (2024)" (the government's "the base ·
  benefits $4.85T"); portrait "SERVICES · 48.3% of GDP".
- **Industries** (400 + share), under their clusters' middles: landscape two short lines, the name and the value and share
  ("Real estate" / "$4.24T · 13.8%"; "≈" where the split is estimated; "none yet"; "· +24 not measured" where the roster has
  companies that existed with no value), every industry; portrait one line ("Real estate · 13.8%"), the ten largest. The labels
  give the shorter names (`ShortNames`: "Real estate", "Health care", "Wholesale & retail", ...); the card gives the full ones.
  Company counts are the title's and the card's.
- **Companies** (300 + r): the eight largest drawn as bodies (portrait four) and the selected one; a close-up names every
  company of its stratum from $10bn at 2025's scale (at most 60).
- **Fates** (250, landscape, two), **owners and households** (220, right of their bodies), **years** (190, three),
  **the peak** (180, landscape, one).

Past the budget the least important go first: the peak, the fates, the years, the column's names, then the smallest companies
and industries.

## Hooks outside the layer

- `EconomyViews.Focused("overview")` and `EconomyViews.Get("overview")` (a spec of its own: every land group 0, no land
  labels, the road's labels aside, the land folded; the cached overview spec other views copy is untouched).
- The emphasis row `RegisterView("overview", subject sweep.industries/companies, support sweep.streams/circuit, context
  sweep.owners/households/ghosts/marks, synthesis)`: every land, wave, hill, activity, social and capture part is absent there.
- Hidden in the Wave view: `HillWaveLayer`, `HillSocialLayer`, `HillReplayLayer`'s tags, `HillExplorer` ("Explore the hill"),
  `LandPicker.YearClicks` (section only).
- `HillPresets`: the overview's title, subtitle and key ("Not measured: grey ring" gone with the rings), and `SweepPose`
  (portrait width 0). Its catalog pose still asks distance 1150; the layer fits on focus (2025 stands about 1,650 away).
- `PointBodies.Silhouette(..., dashEvery)`: 2 draws every other segment (the dotted outline); 1, the default, as before.
- `EconomyEmphasis.HighlightCheck` (`-smokeextra emphasis`) lights the wave's crest in the landscape view, since the overview
  no longer draws it.

## Performance

At load (worker): the roster's parse (the contract asks under 20 ms for 304 companies; logged), 160 layouts, 25 industry and
72 company shapes. At load (main): their meshes, occluders and outlines. A year's change (one `HillRebuilds` slot, timed as
"sweep: year"): the stack's fit (two passes from the last year's scale, up to ten from a guess: each a stack of the year's
rows, its essentials and a solve), about 350 transforms retargeted and shapes swapped by reference (no body mesh is built), the
dots, streams (5 × up to 79 rings × up to 40 dots), peaks, ghosts, tail and circuit meshes into kept builders, at most 46 labels.
Each rebuild is logged in the smoke test; over 20 ms warns, over 50 ms fails the smoke sequence. The accounts of the year and
the next three are built on a worker ahead of the replay. A frame eases the bodies in flight; packets move in the shader. Not
done: merging each cluster's small bodies into one mesh a year for the headset.

## Smoke

- **ViewVariants** (captured after the overview's own frame in every run): `sweep:1950`, `sweep:1985`, `sweep:1995`,
  `sweep:1996`, `sweep:2008`, `sweep:2025`, `sweep:2025:sel=nvidia`, `sweep:2025:in=services`.
- **`-smokeextra sweep`**: those states, each logged (industry and company bodies, dots, not measured (counted, not drawn),
  labels and the budget, rebuild ms, the worst overlap, the fit box, the distance and its share of the limit, px a unit and
  the scale the stack was stood for with its passes, the rows' gaps in px (within a stratum and between), the largest industry
  in px) and captured (`economy-sweep-2025-sel-nvidia` and so on), failing a state with no industry or no company bodies, an
  industry with value added and no body, two company bodies overlapping by more than 0.5 units, essentials off the screen, a
  body drawn as measured without a measured value that year, a rebuild over 50 ms, a fit at its distance limit (4000; over 90%
  warns), a camera more than 9.5% off the scale the stack was stood for (not in a close-up), labels over the budget, or on a
  portrait screen a largest industry under 25 px; the never-still check at 2025; then a replay pass 1995-2005 at 0.35 s a
  year, each rebuild timed.
- **The comparison frames:** a second run with `-classicwave -smokeonly overview` captures the old wave.
- `SmokeTest.NeverStillViews` already lists the overview: its eight lanes keep it moving at any year.

## The layout's check without Unity

A port of `SweepLayout`, `FitStack`, the fit and the labels (scratch: `sweep-port/mirror.py` and `render.py` in the session's
scratchpad; `variants.py` and `streamtest.py` compare arrangements and slants), with the HUD's blocks of the 8 Nov frames
(portrait: the free rect is 1,048 x 726 px under the title, the measured block, the legend and the bar), the label scale (1.5 on
a portrait screen), companies.json's 199 companies, and a simulation of `LabelSystem.Place` (own place, fallbacks, the four
candidates at 1.2 em; boxes padded 4 px, on screen, off the blocks; widths as `TextPx`). Second pass, 8 Nov 2026:

| | px a unit (distance) | largest / smallest industry | largest / smallest company body | labels placed of specs | over another row's bodies |
|---|---|---|---|---|---|
| 1950, 1920 x 1080 | 0.94 (1392) | manufacturing 87 px / internet 15 | AT&T 24 / U.S. Steel 17 | 27 of 36 (industries 16 of 25) | 0 |
| 1985, 1920 x 1080 | 0.87 (1498) | manufacturing 71 / internet 19 | IBM 23 / Pan Am 3 | 36 of 45 (industries 17, companies 8 of 8) | 0 |
| 2025, 1920 x 1080 | 0.79 (1660) | real estate 63 / mining and metals 20 | NVIDIA 39 / Cal-Maine 4 | 40 of 46 (industries 22, companies 6 of 8) | 0 |
| 1950, 1080 x 1920 | 0.76 (3035) | manufacturing 71 / internet 13 | AT&T 20 / U.S. Steel 13 | 17 of 21 | 0 |
| 1985, 1080 x 1920 | 0.90 (2586) | manufacturing 73 / internet 19 | IBM 24 / Pan Am 3 | 18 of 24 | 0 |
| 2025, 1080 x 1920 | 0.73 (3172) | real estate 59 / mining and metals 19 | NVIDIA 37 / Cal-Maine 3 | 18 of 25 | 0 |

Gaps (landscape 2025): 43 px within services, 71-85 px between strata; portrait 46-82 px. A company's name stands at most 28 px
off its body's edge (Apple's second tier over NVIDIA's name, 2025; Sears', 1985). No two company bodies overlap on
screen, no ring stands under a HUD block, every fit stands well inside its limit (4000: portrait at most 3172). The first
frames' landscape scale was 1.14 px a unit at 2025 with 22-unit gaps (25 px) and two-line names (34 px) in them: the room the
names now keep costs about 30% of the scale. Names culled are the smallest industries' where a row's names would meet (2025:
legal, education, arts; 1950: construction, transport and the small services, whose second line carries "+N not measured").

Review of the second pass (the same port, every year 1947-2026, with the review's changes: `Unmet`, the portrait drop and
lift of 10, the owners' name at the top circle): industry names standing on their own clusters, portrait 230 over the 80
years before, 34 after (landscape 0 both); labels over another row's bodies, portrait 5 years before, 0 after (landscape 0);
the owners' name placed in 80 of 80 portrait years (64 before); labels placed 2,709 to 2,678 landscape and 1,476 to 1,425
portrait (the names that would have been moved aside); scales and distances unchanged (portrait 2025 3178, 2008 3561 the
largest, 89% of the limit). 2025: landscape 41 labels placed (40 before), portrait 17 (18; construction's name, which stood
on its body, is not drawn).

## Departures from the spec, and why

- **Packing by an ellipse, 20 rings** (spec: the first free spot ring by ring, 12 rings). Ring by ring, 2025's tallest clusters
  (manufacturing's 45 companies, computers and chips' 26) stood 190 units high and the six rows fitted at 0.91 px a unit; taking
  the free spot nearest by an ellipse 2.5 times as tall as wide grows clusters sideways and fits at 1.08.
- **Clearance 1.5 units + 6% of the two radii** (spec: 1.5): the lumpy bodies reach about 5% past their volume radius.
- **Row gaps: the labels' room at the fitted scale, then 4 and 12 units** (spec: 26 and 34; first build 22 and 30). A fixed gap
  in units held the names only at one scale: at 2025's 1.14 px a unit the first frames' 22 units were 25 px under 34 px of
  names, so a row's names stood in the next row (Legal services' and Insurance's level with MANUFACTURING &
  INFRASTRUCTURE). The stack and the fit now agree on the scale (`FitStack`).
- **One departure for every stream, at the stack's widest row** (spec: each stratum's own widest row): raw materials' and the
  government's narrow rows sent their streams out from the middle of the screen, under the wider rows above, and in
  perspective the government's crossed raw materials'.
- **Rings on one straight slant from the year shown's departure** (spec: each year's ring at that year's own departure): a
  year's rows narrow where values are missing (most before 1996), and the streams would kink at that seam, an artefact of the
  layout drawn as if it were the economy's.
- **Portrait: one row a stratum, headings over the rows** (spec: wrapped at 300; first build at 450, nine rows, which ran
  the fit to its 4000 limit in the real frames: the free rect under the portrait HUD is 1,048 x 726 px, wider than tall, and
  its labels are 1.5 times larger, so the height binds and every row costs a band of names). Compared in the port (2025):
  wrapped at 450 or 550, nine or six rows, 0.58 px a unit at the limit; six a row, six rows, 0.585 at 3961; four a row,
  seven rows, at the limit; one row a stratum, 0.73 at 3172 (real estate 59 px across). Its clusters flatten 1.8, not 2.5
  (the services row 956 units wide, not 1,075), and only its ten largest industries are named.
- **No "not measured" rings** (first build: a grey dotted ring for every company that existed without a value). In 1950 they
  were 98 rings beside 3 bodies, in 1985 134 beside 26: a ring's place was hashed and its size fixed, so it said nothing but
  its count while reading as an object; the count now says exactly that, in the industry's name and the title, and nothing
  unmeasured is drawn at all.
- **Slant 18 and 8 a year, rising 0.28** (first build 9 and 4; the lead's 14 and 6 after the first frames): the labels' room
  brought 2025 from 1.14 to 0.79 px a unit, and 18 and 8 keep the streams as long on screen; the rise fills the empty top left
  and keeps the government's stream above the legend (with 22 and 10 a year and a 0.22 rise, a few of its rings would stand
  under the legend's corner).
- **The overview's spec folds the land** (morph target 0): the land is hidden there, and a bowl left unfolded would still keep
  the HUD's year readout off a part of the screen nothing is drawn on. Entering a land view from the Wave view now unfolds the
  land, as entering it from the families view always has.
- **Industry outlines as their own small meshes** (spec: two whole meshes an industry, solid and dotted): the same picture at
  half the memory.
- **No rotation of shared shapes about the view axis**: twelve seeded variants give the variety, and an unrotated shape's
  baked silhouette stays right after the camera orbits and the outlines are baked again.
- **Shapes uploaded and every body's renderers made at load**, not twenty meshes a frame afterwards: the overview is the
  first view shown, so a progressive upload would show it filling in; at load the cost sits behind the loading screen, and the
  first year shown makes no GameObject or material.
- **Peaks from measured years only**, and the one named by the points of GDP lost, not the ratio.

## What is left

1. The lead's frame checks of the second pass: no row's names in another row (2025: Legal services' and Insurance's under
   services' second row, MANUFACTURING & INFRASTRUCTURE level with its own row); Microsoft's and NVIDIA's names at their
   bodies in the tech row (a second tier where two meet, never another row); the portrait frames fitted well inside the limit
   (the smoke log's "distance ... of at most 4000"), the strata's headings over their rows, about 18 labels; the streams' far
   years in the top left and no ring under a HUD block; 1950 and 1985 without rings ("+24 not measured" in manufacturing's name,
   "+98 not measured" in the title); about 40 labels in landscape. Which industries' names are culled in the early years
   (the small services', construction's and transport's in 1950), and whether the 30% smaller scale reads; the knobs are the
   names' second line ("+N not measured" widens it), the eight company names (their second tier costs a band over the tech row)
   and StratumGap.
2. companies.json from the data track: 199 companies, 185 major, 4,570 measured values (the review rebuilt it at Jev's major
   score 2.0; 2.5 left out PepsiCo, Merck, Target, ConocoPhillips, Morgan Stanley, Schwab, Capital One, American Express, Duke,
   Prologis, Adobe, Salesforce, Qualcomm and every professional-services firm). The fallback runs only when the file is missing
   or unreadable.
3. The tour still shows the new picture under the old narration at `hills_wave` and `hills_return`; nothing is re-recorded
   until the user approves.
4. The headset's merged small bodies; the cue grammar has no tour stop yet.
5. Found in review, not changed (each costs scale):
   - **The portrait fit has two basins.** A portrait row keeps room under it only when one of the ten largest industries
     stands in it, and tech's or raw materials' row often names none; with no names under it the height binds 36 px less and
     the scale is about 11% larger (1964 2,676, 1965 3,032, 1966 2,664 away). A replay in portrait refits 16 times from
     1947 to 2026 past the 8% hysteresis (landscape 6), zooming in and out by about 11%. Naming every row's largest industry
     (then the next largest to ten) steadies it to one refit, but the mean distance grows from 2,880 to 3,190 and the largest
     from 3,560 to 3,800.
   - **The labels' room ignores the pitch.** A unit of height on screen is the fitted scale times cos(pitch), less again for
     the rows below the camera's target (farther away): on a portrait screen (18 degrees) the bottom rows' room is about 9%
     short in pixels (2008: 75 px between raw materials and the government against 82 stacked), landscape about 4%. Scaling
     the room by the pitch and the depth at each row clears the rest of the portrait names on their clusters (34 to 0) but
     takes 2008 to 3,730 (93% of the limit; with every row named, to the limit).
   - **Streams on a portrait screen are mostly off its left edge**: the rows take the width, and only each stream's last 6
     years are in the fit (339 of 390 rings off screen at 2025; 1950, 1975 and 2000 are never named). The far years cannot be
     read there; 10 fitted years still leaves 318 off and costs 6% of the scale.
   - **A gap within a stratum can be wider than one between strata** where a row's named companies take their second tier
     (landscape 2014: 93 px within services against 71-77 between; 2004, 2017-2019 too); the names fill it, the stratum's
     name beside its stream marks the band.
   - **A replay holds the camera within the hysteresis while the rows are stood for the new fit**: in the port's replay the
     camera stood up to 7.7% farther (landscape) than the scale the room was stood for, so the room was that much short.

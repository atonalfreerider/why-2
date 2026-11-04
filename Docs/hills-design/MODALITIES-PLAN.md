# The new display modalities: work packages and decisions

*Companion to [MODALITIES.md](MODALITIES.md), the build specification (lead design, 3 Nov 2026). Five proposals (clarity, story, fidelity, spatial, engineering) were written independently from [EVALUATION.md](EVALUATION.md) and the global data brief, scored by three judges (audience, truth, build), and merged into the spec. This file lists the 39 work packages in build order and the design decisions with their reasons. Packages in one group share no files and do not depend on each other.*

## Work packages

| Package | Group | Effort | Depends on | Title |
| --- | --- | --- | --- | --- |
| [WP01](#wp01) | G1 | small | none (start now) | Persist the world research in the repository |
| [WP02](#wp02) | G1 | large | none (start now; no Unity code) | Instant data: US accounts by income group, the state, market value, public debt (instant.json) |
| [WP03](#wp03) | G1 | medium | none (start now) | Lives data: real figures' visibility and net worth (visibility.json) |
| [WP04](#wp04) | G2 | large | WP01 | World data and the 2026 verification gate (global.json) |
| [WP05](#wp05) | G2 | small | Quick fixes Q01-Q27 merged (THEORY.md is being edited by them now) | Correct the record: direction between tiers and where tech dominates |
| [WP06](#wp06) | G2 | medium | Quick fixes Q01-Q27 merged (Q07 harness) | Smoke-check registry, extra sequences and model facts |
| [WP07](#wp07) | G3 | large | WP06 | S01a: one emphasis table, literal transcription, part ids (no visual change) |
| [WP08](#wp08) | G3 | large | WP06 | S02 + S09: one palette, the mark code, the shared ribbon, shader parameters, runtime palette check |
| [WP09](#wp09) | G3 | large | WP06 | S04: extend LabelSystem for the hills (groups, part alpha, placement, halo, degrees, clicks) |
| [WP10](#wp10) | G3 | large | WP02, WP06 | InstantModel: the US economy in one year (pure model) |
| [WP11](#wp11) | G3 | medium | WP04, WP06 | WorldModel and WorldLayout |
| [WP12](#wp12) | G3 | medium | WP03, WP06 | LivesModel: three typical lives, base rates, real figures |
| [WP13](#wp13) | G4 | medium | WP07, WP08, WP09 | S03: a legend that belongs to the view, with a live scale bar; wire label part alpha |
| [WP14](#wp14) | G4 | medium | WP07 | S01b: retune each view, S06 emphasis values, tour emphasis, pose overrides and fact placeholders |
| [WP15](#wp15) | G5 | large | WP07, WP08, WP09, WP13 | Migrate layer group A to tokens, splits, labels and legend entries: wave, landscape, replay |
| [WP16](#wp16) | G5 | medium | WP07, WP08, WP09, WP13 | Migrate layer group B: foundation, regimes, money, mobility |
| [WP17](#wp17) | G5 | large | WP07, WP08, WP09, WP13 | Migrate layer group C: activity, capture, strategy, social |
| [WP18](#wp18) | G5 | medium | WP07, WP08, WP09, WP13 | Migrate layer group D: person, dilemma, you, mind |
| [WP19](#wp19) | G5 | large | WP07, WP09, WP13 | The stage and three empty views (I, G, J), navigation, Help 2x2, XR views page and kept canvases |
| [WP20](#wp20) | G6 | large | WP08, WP09, WP10, WP13, WP19 | M1 structure step and the states framework |
| [WP21](#wp21) | G6 | medium | WP08, WP09, WP11, WP13, WP19 | M2 the world row: blocks, strata, bases, rule and totals |
| [WP22](#wp22) | G6 | medium | WP08, WP09, WP12, WP13, WP19 | M3 the lives corridor and three synthetic lives |
| [WP23](#wp23) | G6 | medium | WP14, WP15, WP16, WP17, WP18, WP19 | S06: the overview as establishing shot and hub; retire the motive blend |
| [WP24](#wp24) | G7 | large | WP20 | M1 income statement: the year's money in steps |
| [WP25](#wp25) | G7 | medium | WP20 | M1 between industries, frontier and attention |
| [WP26](#wp26) | G7 | medium | WP20 | M1 balance sheet and people states |
| [WP27](#wp27) | G7 | medium | WP21 | M2 trade lanes and the 2025 overlay |
| [WP28](#wp28) | G7 | medium | WP21 | M2 conflict marks and the dated event strip (verified only) |
| [WP29](#wp29) | G7 | medium | WP21 | M2 dependence layers: money and debt, chokepoints, foundations |
| [WP30](#wp30) | G7 | medium | WP21 | M2 since 1950: the history wall at one basis |
| [WP31](#wp31) | G7 | medium | WP22 | M3 real lives: money and visibility |
| [WP32](#wp32) | G7 | medium | WP22 | M3 LivesStrip, LivesPanel and your path |
| [WP33](#wp33) | G7 | medium | WP20, WP23 | Stop time and resume time: the wave and M1 choreography |
| [WP34](#wp34) | G8 | medium | WP24, WP25, WP26 | M1 panel: states, steps, the year in five lines, follow a dollar, hover and select |
| [WP35](#wp35) | G8 | medium | WP20, WP27, WP28, WP29, WP30 | M2 panel, layer chips, zoom between M1 and M2, the universe link |
| [WP36](#wp36) | G9 | large | WP14, WP23, WP31, WP32, WP33, WP34, WP35 | The guided tour: six acts, 21 stops, numbers from the models |
| [WP37](#wp37) | G9 | medium | WP32, WP34, WP35 | Portrait and headset framing, budgets and frame time |
| [WP38](#wp38) | G10 | large | WP15, WP16, WP17, WP18, WP23, WP36, WP37 | S07: time left to right in the wave (the mirror behind a flag, shipped last) |
| [WP39](#wp39) | G11 | medium | WP38 | Verification sweep, first-look test and documentation |

### WP01

**Persist the world research in the repository** · group G1 · effort small · depends on none (start now)

**Files.** create: Tools/global/research/BRIEF.md; Tools/global/research/dataset.json (from dataset.json.txt); Tools/global/research/domains.json; Tools/global/research/verify-events.json; Tools/global/research/README.md

Copy the four research files from /home/john/Desktop/Unity/.why-2-build/research/global (the untracked mirror, already lost once in a crash) into Tools/global/research. README.md records the source paths, copy date, sha256 of each file, and that verify-events.json is the starting state of the 2026 gate (pass list and failures as of 3 Nov 2026 11:13), not a substitute for re-verification. No transformation of contents.

**Data.** Reads the mirror's BRIEF.md, dataset.json.txt, domains.json, verify-events.json; writes their tracked copies.

**Acceptance.** All four files are present and byte-identical to the mirror (sha256 printed in README.md and re-checked by a one-line command recorded there). verify-events.json parses and reports 54 passed and 22 failed rows. git status shows only these new files.

### WP02

**Instant data: US accounts by income group, the state, market value, public debt (instant.json)** · group G1 · effort large · depends on none (start now; no Unity code)

**Files.** create: Tools/instant/build.py; Tools/instant/sources.json; Assets/Resources/Data/economy/instant.json (+ .meta); Docs/data/instant.md

A builder in the regimes pattern (python3 -I, cached downloads, --offline, User-Agent why-2-instant/0.1, each value {value, year, source, url, quote|field, note}). It collects these datasets:

(1) Fed DFA levels by INCOME group: bottom 20, 20-40, 40-60, 60-80, 80-99, top 1. Assets by instrument (real estate, corporate equities and mutual funds, private businesses, pension entitlements, deposits, debt securities, other) and liabilities (home mortgages, consumer credit, other), quarterly 1989-2026. Annual means, plus the latest quarter labelled.

(2) Personal income components by income group from BEA's Distribution of Personal Income (latest release). The top 1% split comes from CBO's household income distribution where BEA does not publish it. Groups are scaled to NIPA 2025 totals and marked ≈ with the source year.

(3) Spending mix by income quintile from BLS CE 2024. The top quintile is split into 80-99 and top 1 by an assumption, marked ≈.

(4) NIPA 2025 tables:
- 1.1.5: exports and imports;
- 3.1: receipts, expenditures, interest, net lending;
- 3.10.5: public pay;
- 4.1: income receipts from and payments to the rest of the world;
- 6.16D: corporate profits by industry, with a printed crosswalk to the four tiers. Tech = computer and electronic products + information, marked 'assumed: mapping'.

(5) Listed value by sector: S&P Dow Jones Indices sector weights (hand-read, page and quote) times the Wilshire 5000 total ($75.3T in circuit.json), with a printed GICS-to-tier crosswalk marked assumed.

(6) Federal debt held by the public by holder: Treasury Debt to the Penny or MSPD API, Fed H.4.1 SOMA, and TIC Table 5. The TIC Aug 2026 figure is a 2026 fact: fetch it and quote it.

(7) One reconciled calendar-2025 hyperscaler capital-spending series from corporate-programs.json hyperscalerCapex, with its definition (finance leases in or out). This resolves the $410B/$443B/$449B disagreement (S12).

Docs/data/instant.md lists every series, its source and its use.

**Data.** Writes instant.json. Reads public sources: Fed DFA download (dfa-income-levels-detail.csv, same release as circuit.json), BEA DPI tables, CBO, BLS CE, BEA NipaDataA.txt, S&P DJI factsheet, Treasury FiscalData, Fed H.4.1, TIC. Also reads circuit.json and corporate-programs.json for cross-checks.

**Acceptance.** python3 -I Tools/instant/build.py --offline rebuilds instant.json byte-identically. The build log prints:
- DFA by income: 6 groups, levels summing to DFA totals within 0.5%;
- net-worth shares within 0.1 pt of circuit.json groupNotes [2.89, 4.17, 7.82, 13.33, 47.31, 24.47];
- household liabilities (all DFA instruments) $20.0T within 3%;
- NIPA 2025 receipts minus expenditures equal to net lending within $1B;
- flows by group summing to NIPA totals within 0.5%;
- 6.16D profits mapped to 4 tiers with the crosswalk printed;
- sector weights summing to 100 within 0.5;
- one hyperscaler capex value with its definition.
Every value has a source and either a url+quote or an API field. If the DFA income-group file cannot be fetched, the log says so, writes classBasis = 'wealth' and keeps the four DFA wealth groups (fallback).

### WP03

**Lives data: real figures' visibility and net worth (visibility.json)** · group G1 · effort medium · depends on none (start now)

**Files.** create: Tools/lives/build.py; Tools/lives/sources.json; Assets/Resources/Data/social/visibility.json (+ .meta); Docs/data/lives.md

For US public adults in Assets/Resources/Data/social/people.json:
- monthly English-Wikipedia page views, Jul 2015 to Oct 2026 (Wikimedia REST per-article, user agent, no key, cached);
- Forbes net worth by year (Forbes 400 and World's Billionaires; each value hand-read into sources.json with URL and quote, keeping Forbes's own revisions);
- the person's 2025 follower snapshot copied from people.json, labelled 'followers summed across Instagram, TikTok, YouTube and X, 2025 snapshot'.
The selection rules are computed and printed: 'together' (highest share of years where log net worth and log page views move the same way, at least 8 annual points), 'apart' (lowest share) and 'little attention' (richest Forbes-400 American absent from the most-followed accounts, fewest page views), plus an optional author's pick (decision 34). sources.json records author_approved: true/false for the shortlist. Instagram-only list revisions from Wikipedia are not used.

**Data.** Writes visibility.json. Reads people.json, the Wikimedia pageviews API and hand-read Forbes pages.

**Acceptance.** The --offline rebuild is byte-identical. The log prints, for each chosen person: monthly series with no gaps beyond the source's; at least 8 Forbes values, each with URL and quote; the three rule picks with their scores ('together in k of m years'); and the approval flag. No minors and no private individuals (a check in build.py on the people.json kind and birth year).

### WP04

**World data and the 2026 verification gate (global.json)** · group G2 · effort large · depends on WP01

**Files.** create: Tools/global/build.py; Tools/global/verify.py; Tools/global/sources.json; Assets/Resources/Data/economy/global.json (+ .meta); Docs/data/global.md

Implement the brief's recipes R1-R9 for the 13 economies, Germany inside EU27, ROW and WLD, with 2024 as base, the 2025 US import overlay and dated events. Add:
- general government revenue % GDP (IMF GGR_G01_GDP_PT);
- annual bilateral series 2015-2025 for the conflict pairs (Census, Eurostat);
- Maddison 2023 shares 1950-1979 and WEO PPPSH 1980-2024 (USSR seamed into Russia; EU27 as the sum of its members);
- WEO NGDPD 1980-2024;
- population-weighted centroid longitudes from Natural Earth populated places (pop_max), with the west-to-east order;
- V-Dem v16 EDI and Regimes of the World 2025 via OWID (EU27 population-weighted, labelled).
verify.py implements the gate for every 2026-dated value or event:
- fetch and cache with the fetch date;
- find the quote verbatim and check that the value is present;
- Jev support of at least 0.6 when the key is available, else fail closed;
- write status, verifiedOn, quote and supportedJ.
Unverified items stay in global.json with status 'unverified' and drawn=false.

**Data.** Seeded from Tools/global/research/dataset.json and verify-events.json; refetched per BRIEF section 7; writes global.json.

**Acceptance.** The --offline rebuild is byte-identical. The log shows these brief checks passing:
- partners + ROW = row totals;
- DEU never added to EU27;
- mirror gaps over 15% flagged;
- one GDP source per economy (WEO);
- shares at most 100;
- EU27 military = sum of members;
- net IIP ratios in national currency;
- history shares summing to 100 within 0.5 per year.
The gate report lists every 2026 item as verified or unverified with its date and quote status. Items that failed on 3 Nov 2026 (Iran war and Hormuz closure, East-West pipeline, Section 338 on Canada, India interim deal, Taiwan pressure and others listed in spec section 2.6) appear as unverified unless the re-fetch now verifies them. The computed west-to-east order is printed (expected MEX USA CAN BRA GBR EU27 SAU/RUS IND CHN TWN KOR JPN). World GDP = $111.9T and the 13 = 82.2% are printed.

### WP05

**Correct the record: direction between tiers and where tech dominates** · group G2 · effort small · depends on Quick fixes Q01-Q27 merged (THEORY.md is being edited by them now)

**Files.** change: Docs/hills-design/THEORY.md; Docs/hills-design/EVALUATION.md

THEORY section 2 (lines 31-32 today) is replaced. New text: 'Of the $15.85T industries paid other industries in 2024, 37% went up to a higher tier's suppliers, 22% down and 41% stayed within a tier, counting the state as the bottom tier (it buys $2.1T); among market tiers alone 28% up, 25% down, 47% within: supplies come down, the money goes up; tech buys $0.89T from the tiers below and sells them $0.80T, plus $0.29T to the state.'

The dominance sentence is restated as market value: tech is 9.4% of private value added and 9.6% of gross operating surplus, and holds at least 31.7% of listed value. Profit is shown from BEA corporate profits by industry once measured.

In EVALUATION:
- 5.1 #5 and S11 are corrected;
- 5.1's acceptance #4 is reworded to 'a larger share of market value than of output';
- a dated 'Corrections (3 Nov 2026 design pass)' note is added listing the measured totals: PCE $20.95T against the simulation's $21.63T, household debt $20.0T (all DFA liabilities, 2026:Q1) against $14.35T, $185.8T as 2026:Q2, the current account against the trade gap, and 55% of world population in the 13.

**Data.** Numbers recomputed from circuit.json io and industries.json (spec section 2).

**Acceptance.** grep finds no '37%' paired with 'down' or 'comes down' in THEORY.md or EVALUATION.md. THEORY section 2 and EVALUATION 5.1 show the corrected sentences. Acceptance #4 reads 'market value'. The corrections note exists with its date.

### WP06

**Smoke-check registry, extra sequences and model facts** · group G2 · effort medium · depends on Quick fixes Q01-Q27 merged (Q07 harness)

**Files.** change: Assets/Scripts/Core/SmokeTest.cs; create: Assets/Scripts/Core/SmokeChecks.cs (+ .meta); Assets/Scripts/Core/ModelFacts.cs (+ .meta); Tools/export-facts.py

SmokeChecks provides three hooks:
- Register(name, Func<ViewPreset, IEnumerable<string>>): SmokeTest calls it after every capture, view variant and tour stop, and logs each returned string as an error;
- RegisterSequence(name, IEnumerator): sequences run with -smokeextra a,b, for packages' own captures (palette card, label stress, transitions, budgets);
- a generic -smokecheckfail flag that registers a failing check to prove the exit code.
ModelFacts.Set(key, value, unit, source, basis) is thread-safe. -smokefacts logs '[Why] fact <key> = <value> <unit> | <source> | <basis>' at the end of the run. Tools/export-facts.py parses a player log into Tools/model-facts.json (sorted, stable). After this package, no other package edits SmokeTest.cs.

**Data.** None (harness).

**Acceptance.** The log shows '[Why] smoke checks: N registered' and per-capture results. A run with -smokecheckfail exits 1 and names the failing check. -smokeextra with an unknown name logs an error. -smokefacts with one test fact prints the fact line; export-facts.py turns that log into JSON with the fact. The full smoke run on the merged quick-fix state still has 0 errors.

### WP07

**S01a: one emphasis table, literal transcription, part ids (no visual change)** · group G3 · effort large · depends on WP06

**Files.** create: Assets/Scripts/Economy/Emphasis/EconomyEmphasis.cs, Assets/Scripts/Economy/Emphasis/EmphasisTable.cs (+ folder and .meta); change: Assets/Scripts/Economy/EconomyViews.cs; Assets/Scripts/Economy/Layers/LandViewLayer.cs; the Emphasis() bodies, inline view-dependent alpha and mesh ids in Assets/Scripts/Economy/Layers/HillWaveLayer.cs, HillLandscapeLayer.cs, HillReplayLayer.cs, HillFoundationLayer.cs, HillRegimeLayer.cs, HillMoneyLayer.cs, HillMobilityLayer.cs, HillActivityLayer.cs, HillCaptureLayer.cs, HillStrategyLayer.cs, HillSocialLayer.cs, HillPersonLayer.cs, HillDilemmaLayer.cs, HillYouLayer.cs

Spec section 3.1.

The key registry: layer.part strings with int handles; EmphasisIds.Part(layer, part) id ranges below 2^24, clear of EconomyIds. Rows hold four levels: Subject 1, Support .45, Context .12, Absent 0 (renderer deactivated).

The API has five calls:
- RegisterView;
- Contribute(view:variant);
- Multiplier(key, name, Func<float>);
- tour override push/pop;
- Problems().
LandViewLayer eases all keys once per frame. EconomyViews.Table's land groups fold in.

Transcribe, for every key and catalog view, the value today's code returns. This includes the state-dependent alpha outside the Emphasis() switches, as named multipliers:
- HillActivityLayer: level of detail, selection, EvidenceOnly;
- HillCaptureLayer and HillWaveLayer: state references.
Replace each switch with lookups, and build hill meshes with part ids. Change no values.

Register the SmokeChecks rules:
- every read key is registered;
- every view has a row;
- at most 3 subjects (synthesis exempt);
- no data mark above intensity 1;
- no key at 0.5 or more in more than 6 views (warning only until WP14 retunes).

**Acceptance.** Full smoke run with all views, regimes variants and both tours before and after. Every economy-view-*.png and economy-tour-NN-*.png differs from its merged-quick-fix baseline in under 0.5% of pixels (ffmpeg blend=difference, threshold 8/255; script recorded). The log shows '[Why] Emphasis: K keys, V views, 0 problems'. grep finds no 'float Emphasis(' under Assets/Scripts/Economy/Layers. A smoke sequence highlighting wave.crest brightens only pixels inside the crest's projected box (diff mask).

### WP08

**S02 + S09: one palette, the mark code, the shared ribbon, shader parameters, runtime palette check** · group G3 · effort large · depends on WP06

**Files.** change: Assets/Scripts/Economy/EconomyStyle.cs; Assets/Resources/Shaders/WhyLine.shader; Assets/Resources/Shaders/WhySurface.shader; Assets/Scripts/Core/GraphLayer.cs; create: Assets/Scripts/Economy/Draw/MarkCode.cs, Assets/Scripts/Economy/Draw/FlowRibbon.cs, Assets/Scripts/Economy/Draw/PaletteCheck.cs (+ folder and .meta); Tools/check-palette.py

Spec sections 3.2 and 3.3.

EconomyStyle gains the role tokens (sRGB targets converted to linear):
- Owners #EFB146, People #6796EE, Desire #EF5B98, Fear #79E4F0;
- State #546577 with base #3A4654, Attention #F5F5F5, Commerce #C0B5AA, Conflict #864AD2;
- Matter, Life, the tier ramp, the class ramp, YearShown, Ink, Missing and the chart series.
Add MotiveSplit(width, fear). Mark Motive() and Fantasy [Obsolete]; they are removed in WP23.

Shaders:
- Why/Surface gains _Hatch (screen-space 45-degree stripes: period, duty, strength);
- Why/Line gains _Dash (alpha gaps along the path phase written by AddFlowPath: period, duty);
- GraphMaterials gets setters for both.

MarkCode builds the forms: solid, judged outline (fill alpha 0.5 x confidence; nothing below 0.5), modelled dotted edge, owed hatch, no-data dashed outline, ghost hairline, assumed chip text, and the not-money dotted strand.

FlowRibbon: bands in a plane, Hermite-routed between ports, with arrowheads; width = dollars x scale; a 1.5 px hairline with a mark below that; split lanes. LDR, alpha-blended.

PaletteCheck registers a smoke-only 'palette' preset and sequence. It draws every role as layers draw it, samples rendered pixels after post-processing, converts them to OKLab, applies Machado 2009 deutan and protan, and logs pair separations. The run fails on any touching pair under 8, or desire-fear under 15. Known weak pairs (attention-fear protan 7.9; matter-life 4.0) are listed with their placement rule.

check-palette.py lists new Color( literals under Assets/Scripts/Economy/Layers.

**Acceptance.** The palette capture (economy-view-palette.png via -smokeextra palette) shows every role swatch, a test strip of hatch and dash, and FlowRibbon test bands. The log lists measured separations for the pairs in spec 3.2: desire-fear, people-fear, desire-people, owners-desire, owners-people, state-people, desire-state, owners-commerce, people-conflict, desire-conflict, state-conflict, commerce-fear, each at least 8 under deutan and protan, and desire-fear at least 15 under deutan. The FlowRibbon test logs width = $ x scale within 2% and the hairline rule at 1.5 px. check-palette.py prints the remaining layer files with local colours: only files owned by WP15-WP18. The full smoke run has 0 errors.

### WP09

**S04: extend LabelSystem for the hills (groups, part alpha, placement, halo, degrees, clicks)** · group G3 · effort large · depends on WP06

**Files.** change: Assets/Scripts/Core/LabelSystem.cs; Assets/Scripts/Economy/UI/HudBlocks.cs; create: Assets/Scripts/Economy/HillLabels.cs (+ .meta)

Spec section 3.5. LabelSystem gains:
- owner groups with Replace(owner, specs) and Clear(owner), thread-safe;
- LabelSpec.Part with a PartAlpha provider delegate (hidden below 0.3);
- size roles Title 18, Name 15, Value 13, Note 12 px at 1080p, clamped 12-22, with priorities;
- four candidate offsets with 1 px leaders;
- HUD exclusion rectangles supplied by HudBlocks (title, legend, panels, tour card, bar, year chip);
- a TMP underlay halo, LDR colours, in the base camera (no overlay camera, no second canvas);
- in XR, sizes of at least 1 degree (titles 1.2) from the eye camera's pixels per degree;
- LabelSpec.OnClick;
- per-view metrics registered as a SmokeChecks rule (overlaps 0, minimum 12 px or 1 degree, 0 under HUD).
HillLabels is the adapter layers call: Set(owner, labels) with stage or land positions, role, part, provenance and click. The economy wiring of PartAlpha happens in WP13. A 'labels-stress' sequence places 300 labels over the overview.

**Acceptance.** -smokeextra labels-stress logs 'overlaps 0, min >= 12.0 px, max <= 22 px, under HUD 0'. Replacing a group 100 times keeps the spec count constant (logged). In the stress capture, label pixels never exceed their LDR colour by more than 5% (no bloom halo). A -xr run logs a minimum label angle of at least 1.0 degree. The universe scene's labels look unchanged (smoke captures of the Why scene differ by under 0.5% of pixels).

### WP10

**InstantModel: the US economy in one year (pure model)** · group G3 · effort large · depends on WP02, WP06

**Files.** create: Assets/Scripts/Economy/Model/InstantModel.cs (+ .meta)

Spec sections 5.4, 5.5 and 5.10. The model is pure C#, built per year on a worker and cached. It loads instant.json itself (Resources) and reads LandSnapshot.Circuit, circuit.json, industries.json, ValueCapture (Leontief split; item judgments shrunk to priors, with confidence), the 25 firms, corporate-programs.json, people.json and EconomicLives (role counts only).

Outputs:
- nodes: 6 income groups or the fallback, 4 tiers, the state, abroad, stubs;
- every flow in spec 5.4 with $, colour role, provenance and per-group sub-amounts;
- the household dollar split;
- the 5x5 tier matrix in the money direction with up, down and within shares (state as tier 0 and market-only), the boundary flows, and tech buys and sells;
- the comparison strip shares;
- balance-sheet stocks by group and instrument;
- threads allocation;
- role counts with n;
- face-mosaic shares;
- coverage flags by year.

Problems(), logged as errors:
- node balances within 1% or a labelled 'other';
- class total = personal income + contributions within 0.5%;
- tiers = value added;
- PCE = NIPA PCE;
- direction facts recomputed from io;
- every fact sourced.
It publishes ModelFacts under instant.*.

**Data.** instant.json, circuit.json, industries.json, spending.json, purchase-judgments.json, corporate-programs.json, people.json, the simulation.

**Acceptance.** The smoke log prints '[Why] Instant 2025:' with:
- class gross income $28.1T in 6 groups;
- tiers 1.37/8.53/14.85/2.57;
- base outlays;
- PCE 20.95;
- fork 41/27/15/10/7 summing to 100;
- io up/down/within 37.2/21.9/41.0 and market 27.7/24.9/47.4;
- boundaries 1.15/0.60, 1.76/2.88, 0.90/0.80;
- tech buys 0.89, sells 0.80 + 0.29;
- listed value tech at least 31.7%.
InstantModel.Problems() is empty for 2025, 1990 and 1972; the earlier years log their coverage flags. Two runs give identical outputs. The worker build takes under 40 ms (logged). -smokefacts lists the instant.* facts used by the tour.

### WP11

**WorldModel and WorldLayout** · group G3 · effort medium · depends on WP04, WP06

**Files.** create: Assets/Scripts/Economy/Model/WorldModel.cs; Assets/Scripts/Economy/Land/WorldLayout.cs (+ .meta)

Spec sections 6.1-6.7. Parse global.json.

Layout:
- widths at 5 units per 1% (100% = 500 units), with no gaps and separator lines;
- order from the computed centroid longitudes;
- the US centred at stage u 0;
- the ROW block at the east end.

Lanes:
- arch paths in the plane: apex 0.22 x span + 10; two concentric lanes, outer eastbound;
- the default set (12 US pairs, 6 largest other pairs) and the 'All flows' set;
- exporter-record widths at 1 unit per $100B, with mirrors.

Also computed:
- wall, valve and cut anchors on lanes;
- dependence-layer geometry inputs;
- the history series on both bases;
- the drawn-lane share of world goods exports outside intra-EU trade.

Problems(): shares sum to 100; DEU never added; every drawn 2026 item verified (verifiedOn at or after asOf); no drawn event past its end date without an outcome; lane widths within 2%. It publishes ModelFacts under world.*.

**Data.** global.json.

**Acceptance.** The log prints '[Why] World 2024: 14 blocks, shares 100.0, row 500.0 units, order <computed order>'. It also prints the default lanes (24 US + 12 others) with the drawn share of about 32.1% and the All-flows share of 43.6%. WorldModel.Problems() is empty, and the log lists every 2026 item drawn or excluded. -smokefacts lists world.* facts.

### WP12

**LivesModel: three typical lives, base rates, real figures** · group G3 · effort medium · depends on WP03, WP06

**Files.** create: Assets/Scripts/Economy/Model/LivesModel.cs (+ .meta)

Spec sections 7.2-7.4.

Synthetic lives:
- from EconomicLives: adults aged 30-39 in the middle income fifth at year-20 (widen to the 2nd-4th fifths if fewer than 60 records, and say so);
- outcomes by fifth at the year;
- the medoid rank path per outcome (L1; present every year; fixed seed);
- events from PersonLife with what changed (rank and net worth over two years); parents' rank from PersonTraits;
- base rates with n and 90% Wilson intervals;
- the viewer's ten-year odds by fifth and age band.

Real figures: load visibility.json, apply the printed rules and compute the 2016 = 1 indices and 'together in k of m years'.

Problems(): outcomes sum to 1; each pick lies in the start group and within its group's interquartile range of distance to the median path; public adults only. It publishes ModelFacts under lives.*.

**Data.** The simulation, PersonLife, PersonTraits, Mobility, visibility.json, people.json.

**Acceptance.** The log prints '[Why] Lives 2005-2025: middle fifth aged 30-39, n = N; climbed a% (lo-hi), stayed b%, fell c%; typical #i/#j/#k' and the real-figure picks with their k of m. Two runs give identical ids. LivesModel.Problems() is empty. A scripted default viewer logs odds summing to 100 plus or minus 1, with n.

### WP13

**S03: a legend that belongs to the view, with a live scale bar; wire label part alpha** · group G4 · effort medium · depends on WP07, WP08, WP09

**Files.** change: Assets/Scripts/Core/ViewPresets.cs; Assets/Scripts/UI/HudLegend.cs; Assets/Scripts/UI/Hud.cs; Assets/Scripts/Director/TourPanel.cs; Assets/Scripts/Economy/HillLabels.cs; create: Assets/Scripts/UI/HudScaleBar.cs; Assets/Scripts/Economy/ViewKeys.cs (+ .meta)

Spec section 3.4. LegendSpec has:
- entries: glyph, role, meaning of at most 6 words, provenance;
- a Grammar line;
- a scale bar or rulers (at most 3);
- a provenance line;
- a direction line.
IViewKeySource lets layers supply entries, filtered by eased emphasis of at least 0.5. HillPresets.ViewKey adapts as the fallback until a view's layers supply entries.

HudLegend rebuilds on FocusChanged and on a variant or layer change, and stays visible in the tour; TourPanel shows the key line. HudScaleBar measures PixelsPerUnit at the subject plane every frame.

Wire HillLabels.PartAlpha to EconomyEmphasis.Get.

**Acceptance.** Every economy-view-*.png shows its own legend. The log prints '[Why] Legend <view>: entries [...]', and the entries match the view's lit parts (registered SmokeChecks rule). The legend is visible in every economy-tour frame. The scale bar's pixel length is within 3% of the projected world length (logged). An -xr capture shows the legend on the HUD plane. Portrait wraps to at most 2 rows. Labels of a part eased to 0 disappear (logged count).

### WP14

**S01b: retune each view, S06 emphasis values, tour emphasis, pose overrides and fact placeholders** · group G4 · effort medium · depends on WP07

**Files.** change: Assets/Scripts/Economy/Emphasis/EmphasisTable.cs; Assets/Scripts/Director/TourData.cs; Assets/Scripts/Director/DirectorMode.cs

Retune every view's row so the subject is at 1 and the rest at 0.12 or less or absent, with off-topic layers removed by rows.

The overview row follows S06:
- lives .2 with the 5-8 largest living firms bright;
- spray 0;
- halos .4;
- the cloud only in 2025.

TourStep gains:
- 'emphasis' (key to level or value, '*' for the rest);
- 'lit';
- 'pose' and 'portraitPose' (target, yaw, pitch, distance);
- '{key}' placeholders in title, text, reading and footer lines, resolved from ModelFacts at play time; an unresolved placeholder logs an error.
DirectorMode pushes and pops the overrides per step and per cue, and applies the poses.

The existing tour stays as it is in content; WP36 rewrites it.

**Data.** ModelFacts (WP06).

**Acceptance.** The landscape, people, rivers and roots captures differ pairwise by at least 60/255 mean over the land box (they differ by 31-34 today). In rivers, rose plus ice is at least 30% of saturated pixels; in capture, blue is at least 25% (geq masks). A test stop with an override logs its lit keys at 0.9 or more and every other key at 0.12 or less, and its pose matches the override. A card with {instant.pce}-style test text resolves, and a missing key logs an error. The existing tour runs with 0 errors.

### WP15

**Migrate layer group A to tokens, splits, labels and legend entries: wave, landscape, replay** · group G5 · effort large · depends on WP07, WP08, WP09, WP13

**Files.** change: Assets/Scripts/Economy/Layers/HillWaveLayer.cs; Assets/Scripts/Economy/Layers/HillLandscapeLayer.cs; Assets/Scripts/Economy/Layers/HillReplayLayer.cs

Replace layer-local colours with role tokens. The crest becomes YearShown with its tag. People are blue and fear is ice. Use MotiveSplit, never a blend. Replace every world-space TextMeshPro and Label() with HillLabels: the focus card, decade labels, tier labels and replay tags. Supply IViewKeySource entries. Geometry is unchanged apart from colour and labels.

**Acceptance.** check-palette.py reports 0 local colours in these files. For the overview, landscape, companies, y1972, section and replay frames: overlaps 0, minimum 12 px, 0 under HUD. No warm-white or violet motive-blend pixels (hue masks). grep finds no 'new GameObject' with TextMeshPro in these layers. The smoke run has 0 errors.

### WP16

**Migrate layer group B: foundation, regimes, money, mobility** · group G5 · effort medium · depends on WP07, WP08, WP09, WP13

**Files.** change: Assets/Scripts/Economy/Layers/HillFoundationLayer.cs; Assets/Scripts/Economy/Layers/HillRegimeLayer.cs; Assets/Scripts/Economy/Layers/HillMoneyLayer.cs; Assets/Scripts/Economy/Layers/HillMobilityLayer.cs

Same as WP15. Self-contained charts (the money chart, mobility streams, regime strata) use the chart series with direct labels. State marks become slate tokens; military takes the state colour, never conflict violet. Labels go through HillLabels, and the layers supply legend entries.

**Acceptance.** check-palette.py reports 0 local colours in these files. For foundation, regimes (5 variants), money and mobility: label overlaps 0, minimum 12 px. The money chart and ladder show chart-series hues with direct labels (hue mask finds no owners gold or desire rose in the chart lines). The smoke run has 0 errors.

### WP17

**Migrate layer group C: activity, capture, strategy, social** · group G5 · effort large · depends on WP07, WP08, WP09, WP13

**Files.** change: Assets/Scripts/Economy/Layers/HillActivityLayer.cs; Assets/Scripts/Economy/Layers/HillCaptureLayer.cs; Assets/Scripts/Economy/Layers/HillStrategyLayer.cs; Assets/Scripts/Economy/Layers/HillSocialLayer.cs

Same as WP15:
- rivers drawn as split lanes;
- in strategy, advertising becomes commerce, workers blue and investment split lanes;
- Jev direction arrows in the judged form (outline plus J and confidence; nothing below 0.5);
- the social cloud white only;
- labels through HillLabels;
- legend entries supplied.

**Data.** purchase-judgments.json and corporate-programs.json confidences (for the judged form).

**Acceptance.** check-palette.py reports 0 local colours in these files. For capture, rivers, people, society, betrayal, strategy and social: label overlaps 0, minimum 12 px. Rivers: rose plus ice is at least 30% of saturated pixels. Strategy: Jev arrows render as outlines with J tags, and none is drawn below confidence 0.5 (logged count). The smoke run has 0 errors.

### WP18

**Migrate layer group D: person, dilemma, you, mind** · group G5 · effort medium · depends on WP07, WP08, WP09, WP13

**Files.** change: Assets/Scripts/Economy/Layers/HillPersonLayer.cs; Assets/Scripts/Economy/Layers/HillDilemmaLayer.cs; Assets/Scripts/Economy/Layers/HillYouLayer.cs; Assets/Scripts/Economy/Layers/MindDiagram.cs

Same as WP15:
- the mind's drives as split columns;
- dilemma cooperation in blue and defection grey and broken;
- the 'you' rivers as splits;
- firm readouts quieted in person views through emphasis rows;
- labels through HillLabels;
- legend entries supplied.

**Acceptance.** check-palette.py reports 0 local colours in these files, and the total for Economy/Layers is 0 after WP15-WP18. For atom, dilemma, ladder, mind and you: label overlaps 0, minimum 12 px; the mind's drive labels no longer overprint. The smoke run has 0 errors.

### WP19

**The stage and three empty views (I, G, J), navigation, Help 2x2, XR views page and kept canvases** · group G5 · effort large · depends on WP07, WP09, WP13

**Files.** create: Assets/Scripts/Economy/Stage/StageFrame.cs, Assets/Scripts/Economy/Stage/StageViews.cs, Assets/Scripts/Economy/Stage/StagePanel.cs, Assets/Scripts/Economy/Stage/StageTransitions.cs (+ folder and .meta); change: Assets/Scripts/Economy/HillPresets.cs; Assets/Scripts/Economy/EconomyLayouts.cs; Assets/Scripts/Economy/UI/HillExplorer.cs; Assets/Scripts/UI/HudPresetBar.cs; Assets/Scripts/UI/HudHelp.cs; Assets/Scripts/Xr/XrSession.cs; Assets/Scripts/Xr/XrMenu.cs

Spec sections 4, 8.1 (navigation part), 9 and 10 (hand menu).

Stage frame and views:
- StageFrame: O = Land().World(120, 0, 240), R = LookRotation(-F, up), World(u, v, d), Fit(box, freeRect), StagePick.
- StageViews: catalog registration hooks (presets, titles and subtitles, short titles, chapter, variants), its own I/G/J key handling (I cycles M1 states), emphasis rows that set every wave, land, road and lifeline key absent, and HillExplorer hidden on the stage.
- StageTransitions: a framework for timed choreographies (flight plus eased roots and quads).
- StagePanel: the shared base (buttons, HudBlocks registration, portrait bottom sheet, XR keep).
- Each empty view draws a faint ground grid and its title only.

Navigation and headset:
- HillPresets calls StageViews.AddTo(list) once, and its chapters become Over time / One year / The world / Money / People / One life;
- HudPresetBar shows chapter dividers;
- HudHelp gains the 2x2 and the keys I, G, J;
- XrSession gets Keep(name) to extend the kept set;
- XrMenu gets a Views page (Wave, One year, World, Lives).

EconomyLayouts gains MirrorLand (default false).

**Acceptance.** economy-view-instant.png, economy-view-world.png and economy-view-lives.png show the grid and the title only. The log shows '[Why] stage <view>: 0 hill renderers active' for each. I, G and J fly to the views; I pressed again cycles states (logged); the keys are ignored while a text field has focus. Bar captions and the Help 2x2 are at least 12 px. In -xr, the hand menu's Views page reaches all three, and the kept-canvas log includes the stage panels. MirrorLand exists and is false. The full smoke run has 0 errors.

### WP20

**M1 structure step and the states framework** · group G6 · effort large · depends on WP08, WP09, WP10, WP13, WP19

**Files.** create: Assets/Scripts/Economy/Layers/HillInstantLayer.cs; Assets/Scripts/Economy/Land/InstantLayout.cs (+ .meta)

Spec sections 5.1-5.3 and 5.9. InstantLayout is pure math: s = 5 units per $1T a year, stage u-ranges and manifolds, floor stacking, mosaic segments, comparison strip, 100-cent bar, gate, frontier and cloud positions (low and high).

HillInstantLayer draws:
- the base with strata and receipts line;
- borrowing hatched;
- class and tier towers with face mosaics;
- the comparison strip (PROFITS column dashed 'no data yet' until 6.16D is in instant.json; MARKET VALUE with tech 'at least 31.7%' and the dashed unsplit segment);
- the 100-cent bar, the gate, the frontier plume and the cloud (2025 only; otherwise the note);
- labels.
It registers the instant ViewVariants: structure, spending, pay, state, abroad, industries, attention, all, balance, people. A states/steps API (InstantState, InstantStep) lets later layers (WP24-WP26) contribute rows. It also registers the year coverage rules, logs projected floor heights in px, and adds anchors instant:base, instant:tiers and instant:classes.

**Data.** InstantModel; people.json for the cloud.

**Acceptance.** economy-view-instant.png (structure) at 1920x1080 shows the base, both towers with mosaics, the strip, the 100-cent bar, the gate and the cloud with its 'four platforms, 2025 snapshot' label. The log shows:
- projected raw at least 20 px, tech at least 35 px, top 1% at least 40 px, bottom fifth at least 20 px;
- floor heights = $ x 5 within 1%;
- 0 hill renderers;
- labels: overlaps 0, minimum 12 px.
The strip prints tech '9.4% · 4.9% · <profits or no data> · at least 31.7%'. economy-view-instant-1972 (set by a smoke sequence) shows an undivided class block with its note and no cloud. A year step rebuild uses one HillRebuilds slot with no frame over 50 ms.

### WP21

**M2 the world row: blocks, strata, bases, rule and totals** · group G6 · effort medium · depends on WP08, WP09, WP11, WP13, WP19

**Files.** create: Assets/Scripts/Economy/Layers/HillWorldLayer.cs (+ .meta)

Spec sections 6.1, 6.2 and 6.7 (drawing). The layer draws:
- the continuous row (100% = 500 units, separators), with blocks 32 units tall in normalised value-added strata;
- bases (context in the default; full in Foundations) with depth = government spending % x 0.4 and a revenue line;
- the ROW block;
- Germany outlined inside EU27;
- the 0-100% rule, the totals line and the 'west to east by where people live' note;
- labels with leaders for narrow blocks;
- a PPP toggle that resizes widths.
It registers world layers as ViewVariants and the anchor world:usa. The default camera is from WorldLayout (target u 176, distance 435).

**Data.** WorldModel.

**Acceptance.** economy-view-world.png shows 14 labelled blocks in the computed order, the rule and the totals line ('US: 26% of output, 4% of people, 37% of military; these 13: 82% of output, 55% of people'). The log shows widths summing to 500.0 units, 3.0 or more px per unit at 1080p, and Taiwan's label placed with a leader. economy-view-world-ppp.png shows China wider than the US. Labels: overlaps 0. 0 hill renderers.

### WP22

**M3 the lives corridor and three synthetic lives** · group G6 · effort medium · depends on WP08, WP09, WP12, WP13, WP19

**Files.** create: Assets/Scripts/Economy/Layers/HillLivesLayer.cs (+ .meta)

Spec sections 7.1 and 7.2. The layer draws:
- the stage-plane corridor u -350 to -200 (year-20 to year at 7.5 units a year);
- the dashed 'next ten years' zone u -200 to -125;
- the class block at rank heights (u -125 to -55);
- the income-rank band (linear), the magnified top-1% band with funnel and 'magnified, log scale', and the attention band with 'log scale';
- three medoid lives in blue styles with end labels 'synthetic person #N';
- event ticks with what changed, and the parents' dashed rank;
- the base-rate bar with n and ranges;
- the 'visibility not modelled' stub.
It registers the 'Turn to lives' transition with StageTransitions and the anchor lives:three.

**Data.** LivesModel.

**Acceptance.** economy-view-lives.png shows years increasing left to right and three paths labelled 'synthetic person #N · climbed/stayed/fell', with at least 3 named events each. The base-rate bar shows n and 90% ranges, and the 'magnified · log scale' and 'log scale' rulers are printed. Labels: overlaps 0, minimum 12 px. A transition capture from instant at 0.8 s shows the tower morphing to rank heights.

### WP23

**S06: the overview as establishing shot and hub; retire the motive blend** · group G6 · effort medium · depends on WP14, WP15, WP16, WP17, WP18, WP19

**Files.** change: Assets/Scripts/Economy/Layers/HillWaveLayer.cs; Assets/Scripts/Economy/Layers/HillLandscapeLayer.cs; Assets/Scripts/Economy/Layers/HillFoundationLayer.cs; Assets/Scripts/Economy/HillPresets.cs; Assets/Scripts/Economy/EconomyStyle.cs

Spec section 8.1. The overview gains:
- tier fills at .3-.45 in the tier ramp with separators;
- in-layer tier names with value-added shares;
- the tech ridges named (tier 4 exempt from the threshold);
- GOVERNMENT on the bedrock's front face below RAW MATERIALS, with 'federal (OMB)';
- the crest tag; the CAPITAL pool label; the ATTENTION label (2025 only);
- framing widened to include the clouds;
- the three door labels (OnClick to I, G, J; pinchable).
Also remove EconomyStyle.Motive and Fantasy; no caller remains after WP15-WP18.

**Data.** industries.json.

**Acceptance.** economy-view-overview.png shows four distinguishable tier fills with names and shares, four named tech ridges, GOVERNMENT below RAW MATERIALS, the crest tag and the three doors. All labels are at least 12 px with overlaps 0. Smoke steps clicking each door fly to instant, world and lives (logged). grep finds no Motive( or Fantasy in Assets/Scripts. economy-view-y1972.png has no cloud.

### WP24

**M1 income statement: the year's money in steps** · group G7 · effort large · depends on WP20

**Files.** create: Assets/Scripts/Economy/Layers/InstantFlows.cs; Assets/Scripts/Economy/Land/InstantRoutes.cs (+ .meta)

Spec sections 5.3 and 5.4. InstantRoutes stacks manifold ports, routes ribbons and counts crossings. InstantFlows draws with FlowRibbon on the flow scale:
- the spending trunk (rose and ice lanes, judged seam with J, PCE total), the fork, and the imports branch into the base-front conduit to the gate;
- the arch (pay inner, owners outer) with the cloud's high position, and owners abroad;
- the reinvested stub loop and the investment stub;
- tax downpipes, the state-to-households riser, the purchases riser, the exports riser;
- saving and borrowing stubs and the other-payments stub;
- per-group highlight sub-bands for selection;
- trunk labels of at most 12 words.
It contributes the rows for spending, pay, state, abroad and all, and adds anchors instant:trunk, instant:fork, instant:arch and instant:state.

**Data.** InstantModel.

**Acceptance.** Captures economy-view-instant-spending, -pay, -state, -abroad and -all. In each, the step's family is at full strength and every other mark is at 0.12 or less (logged eased values; hue coverage over the stage box). The log shows:
- ribbon widths = $ x 5 within 2%;
- every node balanced within 1% from drawn widths;
- InstantRoutes.Crossings() = 0 outside the fork;
- the trunk label reads $20.95T with 'J';
- the 100-cent bar reads 41/27/15/10/7.
The cloud is at its high position in pay and all. A year-step frame stays at or under 50 ms.

### WP25

**M1 between industries, frontier and attention** · group G7 · effort medium · depends on WP20

**Files.** create: Assets/Scripts/Economy/Layers/InstantSummit.cs; Assets/Scripts/Economy/Layers/AttentionCloud.cs (+ .meta)

Spec sections 5.5 and 5.6. InstantSummit draws:
- the boundary arrows (money direction, flow scale) with $ labels and the totals label;
- the strip at subject level in the industries step;
- the frontier at subject level.
AttentionCloud is the cloud builder: log shelves 10M, 30M, 100M, 300M, the top 12 named, the five gold-ringed capital holders with dotted J tethers to the top-1% floor and their tiers, and the four-node attention loop (ads and creators on the flow scale; not-money strands).
It contributes the rows for industries and attention, and adds anchors instant:between and instant:cloud.

**Data.** InstantModel; people.json; corporate-programs.json economy (ads, minutes).

**Acceptance.** economy-view-instant-industries.png shows arrows labelled 1.15/0.60, 1.76/2.88 and 0.90/0.80 ($T, 2024), the totals '37% up · 22% down · 41% within', and the frontier label '$594B a year... (10 largest tech firms)'. economy-view-instant-attention.png shows the shelves with 'log scale', 12 names without overlap, 5 tethers with J tags, labelled loop legs ($294.6B; creators), and 'not money' strands. A 2024 capture shows no cloud and its note.

### WP26

**M1 balance sheet and people states** · group G7 · effort medium · depends on WP20

**Files.** create: Assets/Scripts/Economy/Layers/InstantStocks.cs (+ .meta)

Spec sections 5.7 and 5.8.

Balance: morph to equal rows with group sizes printed, at 1.5 units per $1T owned.
- class assets outward by type, with gold ownership;
- debts inward, hatched;
- tier listed value outward with named firm segments, and a dashed row 'not split by industry: $37.3T';
- threads at 1 per $1T from a gold manifold: 18.4% to ABROAD, the rest to class ownership segments, tagged assumed;
- the state's debt by holder, hatched;
- the net IIP, hatched;
- the stock-flow note and the bridge line on wealth ranking.

People: floors at 1 unit per 1M households or jobs, role dots at 1 per 1M (simulated, n printed), the top-1% label 'magnified label', and the base at 21.6M public jobs.

It contributes the rows for balance and people, and adds the anchor instant:balance.

**Data.** InstantModel (DFA by income, listed value, TIC, IIP, roles).

**Acceptance.** economy-view-instant-balance.png shows six equal rows labelled with households and net-worth shares (top 1% near 24.5%), debts hatched, household debt total near $20.0T, firm-named tech segments summing to at least 31.7% of $75.3T, and the dashed $37.3T row. The thread count equals round(listed $T) (logged), with 14 to ABROAD. The scale bar reads '$10T owned' and no flow pixels remain. economy-view-instant-people.png shows the top 1% holding about one dot, the bottom fifth about 27, the base 21.6 units and a roles legend marked ≈. The 1.2 s morph has no frame over 50 ms.

### WP27

**M2 trade lanes and the 2025 overlay** · group G7 · effort medium · depends on WP21

**Files.** create: Assets/Scripts/Economy/Layers/WorldTrade.cs (+ .meta)

Spec section 6.3. Two concentric lanes per pair (outer eastbound), arrowheads at the destination, 1 unit per $100B, the exporter's record with the mirror on hover.
- the default set: 12 US pairs at subject, 6 others at support;
- 'All flows' and '+ services' variants;
- Russia's partner-built lanes in the ghost style;
- the Taiwan via-Hong-Kong stub;
- the intra-EU label;
- the 2025 variant: US customs imports with 2024 ghosts.
The key line reads 'arrows: where the goods go'. It adds the anchor world:lane:usa-chn.

**Data.** WorldModel.

**Acceptance.** economy-view-world-trade.png shows 24 US lanes and 12 others. The China-to-US lane is 3.7 times the US-to-China lane within 3% (logged widths, 525.2 against 143.3). The five widest lanes carry both records in their labels. The share label is computed (near 32%). economy-view-world-2025.png shows the China -29.9% and Taiwan +73.2% notches labelled 'US customs records'. Labels: overlaps 0.

### WP28

**M2 conflict marks and the dated event strip (verified only)** · group G7 · effort medium · depends on WP21

**Files.** create: Assets/Scripts/Economy/Layers/WorldConflict.cs; Assets/Scripts/Economy/UI/ConflictStrip.cs (+ .meta)

Spec section 6.4. Draw only events and values whose global.json status is verified:
- tariff walls (1 unit per 2 points, with ghosts of earlier rates);
- sanction cuts with 2021 ghosts and rerouting labels;
- export-control valves;
- war zones and strait glyphs.
The Hormuz glyph shows its 2024 flow only unless the closure is verified. There is no war mark for the Galwan clash and no mark for pressure on Taiwan; ended episodes appear only as strip ticks. The HUD strip runs from 2018 to as-of, with ticks, selection lighting an event's marks (emphasis override), the 'as of' stamp and expiry flags within 30 days. The PIIE caveat sits in the key. It adds the anchor world:conflict.

**Data.** WorldModel (events with status), PIIE tariffs, bilateral series.

**Acceptance.** economy-view-world-conflict.png shows walls only for verified items: the log lists drawn and excluded items, and every excluded item appears in the panel's pending list. Wall heights = rate x 0.5 within 2% (logged). The Russia lanes show 2021 ghosts. No war mark appears for Galwan or Taiwan. The strip reads 2018 to as-of left to right with 'as of <date>'. A palette sample of the frame finds no conflict mark in red, rose or people blue.

### WP29

**M2 dependence layers: money and debt, chokepoints, foundations** · group G7 · effort medium · depends on WP21

**Files.** create: Assets/Scripts/Economy/Layers/WorldDependence.cs (+ .meta)

Spec section 6.5. Three variants, one at a time.

Money and debt:
- Treasury threads at 1 per $100B into the US base, with the custodial note;
- net IIP bars at 1 unit per $500B (creditors gold above, debtors hatched below);
- current accounts in the labels.

Chokepoints: keystone 100% bars with holder segments, leader spokes, scope and year; straits.

Foundations:
- bases at full strength with revenue lines;
- military strata in the state colour;
- V-Dem gauges and Regimes-of-the-World class;
- energy glyphs;
- the 'what if' link to regimes.

It adds the anchor world:keystones.

**Data.** WorldModel (TIC, IIP, current account, chokepoints, IMF FM, SIPRI, V-Dem, WDI energy).

**Acceptance.** economy-view-world-money.png shows thread counts per holder equal to round(holdings/100) (logged), 'custodial', and the US net IIP bar at -$22.1T hatched. economy-view-world-chokepoints.png shows 'Taiwan 69 · Korea 31 (SIA/BCG 2022)', 'China 91 (magnet refining, IEA 2024)' and 'AI compute: US 77% (Epoch, end-2024)'. economy-view-world-foundations.png shows base depths ordered EU > UK > US > China > Taiwan and military strata in the state hue. Each capture has exactly one dependence layer drawn (logged).

### WP30

**M2 since 1950: the history wall at one basis** · group G7 · effort medium · depends on WP21

**Files.** create: Assets/Scripts/Economy/Layers/WorldHistory.cs (+ .meta)

Spec section 6.6 (wall). A 100% stacked area at PPP, 1950-2024, left to right at 6.75 units a year, in the stage plane at v -230 to -110.
- the splice tick at 1980 labelled 'Maddison to IMF (both at purchasing power)';
- the USSR-to-Russia seam;
- EU27 as the sum of its members;
- bands west to east from bottom to top, in chart greys with direct labels and the US outlined;
- a market-rate toggle for 1980-2024, with earlier years dashed 'no market-rate series here';
- the row blocks morph to the wall's basis while the wall shows.
The junction caption is computed.

**Data.** WorldModel history (Maddison, WEO PPPSH, WEO NGDPD).

**Acceptance.** economy-view-world-history.png runs 1950 to 2024 left to right, with bands summing to 100% each year (logged), the 1980 splice tick, the USSR seam, 'purchasing power' printed, and the row blocks on PPP (logged basis). economy-view-world-history-market.png starts at 1980.

### WP31

**M3 real lives: money and visibility** · group G7 · effort medium · depends on WP22

**Files.** create: Assets/Scripts/Economy/Layers/LivesVisibility.cs (+ .meta)

Spec section 7.3. For the three rule-chosen public figures:
- a gold net-worth line in the magnified band (log, 'Forbes estimate');
- a white page-view line in the attention band (log, from Jul 2015);
- the 2025 follower snapshot as a separate star at the person's cloud altitude, joined only by a dotted leader labelled 'a different measure', never by the line;
- computed captions ('together in k of m years').
A ViewVariant 'real'. It adds the anchor lives:real.

**Data.** LivesModel, visibility.json.

**Acceptance.** economy-view-lives-real.png shows three labelled real figures with 'Forbes estimate', 'log scale', the star and leader, and computed captions. No page-view vertex lies before Jul 2015 (logged). No line joins a page-view track to a star (geometry check logged). Labels: overlaps 0.

### WP32

**M3 LivesStrip, LivesPanel and your path** · group G7 · effort medium · depends on WP22

**Files.** create: Assets/Scripts/Economy/UI/LivesStrip.cs; Assets/Scripts/Economy/UI/LivesPanel.cs (+ .meta); change: Assets/Scripts/Economy/Model/YouModel.cs

Spec sections 7.4, 7.5 and 10.

LivesStrip: a uGUI mesh graphic on the HUD plane with tracks (income percentile linear, net worth log, page views log), events, parents' rank, the base-rate box and the money-and-visibility chart indexed 2016 = 1 on one log axis. It is the default in portrait and XR, and its canvas is kept.

LivesPanel: the start chooser, lives, events, base rates, 'Add me' and 'Follow on the wave'.

YouModel: odds by income fifth and age band with n and 90% Wilson ranges. The you futures are drawn as three dotted paths in the ten-year zone (variant 'you').

**Data.** LivesModel; YouState.

**Acceptance.** The strip renders in 1920x1080, 1080x1920 and -xr captures with text of at least 12 px or 1 degree. Chooser buttons are at least 40 canvas units and switch lives by click and pinch (smoke step). economy-view-lives-you.png shows three dotted futures with odds summing to 100 plus or minus 1, n and ranges. 'Follow on the wave' opens the ladder view on that person (smoke step).

### WP33

**Stop time and resume time: the wave and M1 choreography** · group G7 · effort medium · depends on WP20, WP23

**Files.** create: Assets/Scripts/Economy/Stage/StopTime.cs (+ .meta)

Spec section 9.2. Overview to instant over 2.0 s:
- the flight from the overview pose to the instant's structure pose;
- wave keys eased to absent over 0.6 s;
- M1 assembling bottom-up.
With EconomyLayouts.MirrorLand true, quads cut from the crest's four tier bands fly into the tier floors, the bedrock face into the base, and the road's cross-section into the class column; with it false, a crossfade. Instant to overview reverses it. I pressed during the replay pauses and opens that year. It registers a 'transitions' smoke sequence. In the headset, content moves and the viewer does not.

**Acceptance.** -smokeextra transitions captures stop time and resume time at 0, 0.6, 1.2 and 2.0 s with no frame over 50 ms. Final frames match the static instant and overview captures (mean diff under 3/255). The log states 'crossfade' with MirrorLand off and 'morph' with it on (forced in the sequence).

### WP34

**M1 panel: states, steps, the year in five lines, follow a dollar, hover and select** · group G8 · effort medium · depends on WP24, WP25, WP26

**Files.** create: Assets/Scripts/Economy/UI/InstantPanel.cs; Assets/Scripts/Economy/Layers/FollowDollar.cs (+ .meta)

Spec section 10 (InstantPanel). The panel is a StagePanel with:
- state buttons and step chips;
- the year;
- 'The year in five lines', each line a button to its step, with computed text;
- the 100-cent bar and the 5x5 matrix;
- 'Follow a dollar' (the FollowDollar token animation in the pay/all frame, ending on the lit 100-cent bar);
- the MEASURED / JUDGED BY JEV / MODELLED / THE DRAWING'S CHOICES / NO DATA sections.
Hover or tap on any M1 mark shows value, unit, year, source and basis. Class and tier selection lights sub-bands and lists the statement. The gate opens the world.

**Data.** InstantModel facts.

**Acceptance.** Panel captures in landscape, portrait (bottom sheet) and -xr. The layout check logs no overlap between the panel and the fitted structure. A smoke step clicks each of the five lines and logs the step shown. The follow-a-dollar capture ends with the 100-cent bar lit. A scripted hover over each floor logs a card with no empty field. A scripted class selection logs sub-band widths that sum to the group's flows within 1%.

### WP35

**M2 panel, layer chips, zoom between M1 and M2, the universe link** · group G8 · effort medium · depends on WP20, WP27, WP28, WP29, WP30

**Files.** create: Assets/Scripts/Economy/UI/WorldPanel.cs; Assets/Scripts/Economy/Stage/ZoomOut.cs (+ .meta); change: Assets/Resources/Data/Walkthrough/tour.json; Assets/Scripts/Core/GraphRoot.cs

Spec sections 6.6 (hand-off), 9.2 (zoom) and 10 (WorldPanel). The panel has:
- a chips row (Trade · 2025 · Conflict · Money & debt · Chokepoints · Foundations · Since 1950 · PPP · All flows);
- an economy card on selection, with the camera reframing;
- the 13x14 matrix with mirror-gap flags;
- the pending-verification list and the as-of stamp.
ZoomOut is the instant-to-world choreography (M1 root scaled toward the US block, crossfading between 40% and 80%, blocks rising west to east, the caption) and its reverse. The universe's 'Power shifts again' card gets a 'Today's world economy' link. GraphRoot gains OpenOtherScene(viewId, variant), which opens the economy scene on world:history.

**Data.** WorldModel.

**Acceptance.** Frames at 0, 1.2 and 2.4 s of the zoom show M1 shrinking into the US block with the classification caption, with no frame over 50 ms. Selecting China lights its lanes; the others are at 0.12 or less (logged). Matrix rows sum to totals. The pending list equals global.json's unverified set. A smoke step from the universe link opens the economy on world:history, and the economy capture is taken. Panel captures exist in landscape, portrait and -xr.

### WP36

**The guided tour: six acts, 21 stops, numbers from the models** · group G9 · effort large · depends on WP14, WP23, WP31, WP32, WP33, WP34, WP35

**Files.** change: Assets/Resources/Data/Walkthrough/economy_tour.json; Assets/Resources/Data/Walkthrough/economy_narration.json; Tools/narration-facts.json; Tools/synthesize-narration-elevenlabs.py (a --facts option to voice rendered templates); create: Tools/fill-narration.py; Tools/check-facts.py

Spec section 11. Write the 21 stops. Each has:
- a card of at most 45 words with {placeholders};
- a key line, a caveat footer, an anchor and lit emphasis;
- pose and portraitPose where needed;
- cues for variants; the you stop waits.
Old per-view stops leave the main tour.

The narration uses the same templates. fill-narration.py renders them from Tools/model-facts.json, exported by a -smokefacts run. check-facts.py fails on any number in cards or narration not traceable to model-facts.json. narration-facts.json holds the per-stop facts for check-narration.py.

Check the Jev key exists without printing it. Run Tools/check-narration.py per stop. Ask the user before voicing with ElevenLabs (credits); until then stops pace by reading time.

**Data.** Tools/model-facts.json (from the player).

**Acceptance.** -smoketour captures economy-tour-NN-<id>.png for all 21 stops. Each has its anchor in frame and glowing, lit keys at 0.9 or more and the rest at 0.12 or less (logged), the legend visible, a card of at most 45 words with its footer, and no unresolved placeholder. Stops 4-13 (M1) precede 14-17 (world) and 18-19 (lives). check-facts.py passes, and check-narration.py passes each stop (contradiction 0.15 or less) or is logged as skipped because Jev is unavailable. The total unvoiced time is 6-8 minutes.

### WP37

**Portrait and headset framing, budgets and frame time** · group G9 · effort medium · depends on WP32, WP34, WP35

**Files.** create: Assets/Scripts/Economy/Stage/StagePortrait.cs; Assets/Scripts/Economy/Stage/StageBudgets.cs (+ .meta)

Spec sections 9.1 (portrait column) and 14.

StagePortrait provides the portrait framings:
- M1: towers only, with the strip and bar in the sheet and numbered callouts;
- M2: halves plus a HUD thumbnail with two buttons;
- M3: the strip default.

StageBudgets registers a 'budgets' smoke sequence that logs vertices, materials and draw calls per stage view in -xr (M1 at most 12, M2 at most 20, M3 at most 6) and runs -smokereplay with M1 on screen (no frame over 50 ms).

**Acceptance.** 1080x1920 captures economy-view-instant-portrait, economy-view-world-portrait-west, economy-view-world-portrait-east and economy-view-lives-portrait show labels of at least 12 px, none under a panel, and M1 raw at least 20 px (logged). The -xr budgets log shows labels at 1 degree or more and draw calls within the caps. The replay with M1 has no frame over 50 ms (LayerClock line).

### WP38

**S07: time left to right in the wave (the mirror behind a flag, shipped last)** · group G10 · effort large · depends on WP15, WP16, WP17, WP18, WP23, WP36, WP37

**Files.** change: Assets/Scripts/Economy/EconomyLayouts.cs; Assets/Scripts/Economy/EconomyStage.cs; Assets/Scripts/Economy/Land/LandFrame.cs; Assets/Scripts/Economy/Land/LandPick.cs; Assets/Scripts/Economy/Land/HillLandscape.cs; Assets/Scripts/Economy/HillPresets.cs; Assets/Scripts/Economy/Layers/HillMoneyLayer.cs; Assets/Scripts/Economy/Layers/HillMobilityLayer.cs; Assets/Scripts/Economy/Layers/HillLandscapeLayer.cs; Assets/Scripts/Economy/Layers/HillWaveLayer.cs; Assets/Scripts/Director/DirectorMode.cs

Spec section 8.2.

The mirror:
- LandFrame reflects land-local x in World, Local, Place (localScale.x -1), Right and a new InverseRotation;
- LandPick.cs:890 uses frame.Local instead of Quaternion.Inverse(frame.Rotation); audit any other direct rotation math;
- wave presets take yaw' = 360 - yaw, and the section offset is mirrored;
- the money chart moves to the road side and the mobility ladder and mind are re-posed, so time stays left to right;
- the year rail replaces the FUTURE arrow;
- the bridge pose in DirectorMode is checked.

Turn MirrorLand on only after a full clean run. If regressions cannot be closed in bounded time, apply fallback (b) and record it.

**Acceptance.** With MirrorLand on:
- the full smoke run plus both tours and -smokereplay have 0 errors;
- in every wave view the decade labels' screen x increases with the year (logged);
- the companies view reads 1935 to 2026 left to right;
- a scripted hover picks the same company with the flag on and off;
- the money chart and ladder read left to right;
- stage-view captures are identical with the flag on and off (mean diff under 1/255);
- no mirrored glyph appears, since all text is billboarded.
The flag's default is recorded with the run log.

### WP39

**Verification sweep, first-look test and documentation** · group G11 · effort medium · depends on WP38

**Files.** change: Docs/hills-design/IMPLEMENTATION.md; Docs/DATA.md; Docs/ECONOMY.md; create: Docs/hills-design/FIRSTLOOK.md; Tools/caption-audit.py

Run the full sweep:
- the smoke run with all flags (-smokereplay, -smoketour, -smokeextra palette,labels-stress,transitions,budgets);
- portrait and -xr runs;
- all SmokeChecks.

Run the five-person first-look test per new view on still captures. At least 4 of 5 must answer each section 1 question within 30 s without narration, and the answers are recorded in FIRSTLOOK.md. A failure goes back to the owning package.

Tools/caption-audit.py (Jev, supporting evidence only) scores the new titles, subtitles, keys and cards.

Documentation:
- IMPLEMENTATION.md gains a section with measured timings and budgets;
- DATA.md links Docs/data/instant.md, global.md and lives.md;
- ECONOMY.md states the four views, the 2x2, time direction and keys.

**Data.** All built data and captures.

**Acceptance.** The sweep logs 0 errors. FIRSTLOOK.md shows at least 4 of 5 correct answers per question for M1, M2 and M3, with the frames used. The caption audit's mean clarity for new strings is at least 0.6, or each string below 0.5 is listed with its rewrite. The docs contain the sections named. The 2026 gate report is re-run and dated.

## Decisions

1. **Which proposal is the base design?** Clarity ('Stop time, zoom out, follow a life'), with grafts from story, engineering, fidelity and spatial.  
   *Why:* It has the highest total score (23 against 20, 19, 19 and 15) and was best for the clarity judge. Its views are planar and frontal, like the only views that read well today (money, mobility, regimes panel), and its palette was the one whose colour-blind numbers reproduced.

2. **Where do the new views stand, and do they wait for S07?** On one stage built from world vectors: origin Land().World(120, 0, 240), rotation LookRotation(-F, up), never mirrored. The position follows the land and the wave is absent there. M1-M3 never depend on S07.  
   *Why:* All three judges flagged that gating the first new view on the riskiest change (the mirror) was wrong. A world-vector stage keeps text and left/right composition identical with the mirror on or off, while the stop-time move still pulls the year out in front of the crest.

3. **How is the direction of time settled (S07)?** Option (a), a frame-level reflection, ships last behind EconomyLayouts.MirrorLand, after S04 has removed all world-space hill text, with LandPick.cs:890 audited. Fallback (b): keep depth as time, print 'the past recedes behind the crest' and add a year rail.  
   *Why:* The universe hands over in left-to-right time and 7 of 12 review groups flagged the reversal. With labels billboarded and the stage independent, the mirror's risk is contained to the wave.

4. **Which class scale do M1, M3, mobility, you and the ladder share?** Household income rank in the Fed DFA's six income groups (bottom 20, 20-40, 40-60, 60-80, 80-99, top 1). Wealth is shown as the balance-sheet stock. The DFA net-worth shares by income group are the bridge line. Fallback, if the DFA income-group levels cannot be fetched: the four DFA wealth groups, with the scale printed in every key.  
   *Why:* Judges asked for one scale. Mobility, you and the ladder already use income fifths, and EVALUATION 5.1 asks for 'five fifths and the top 1%'. Flows (pay, taxes, benefits) are measured by income group, so no copula is needed. Class totals come from measured distributions, not the roughly 26 simulated top-1% records the engineering judge warned about.

5. **How is M1 composed so it does not become a hairball?** Two towers on the state's base. Who gets and spends what is read from area-true face mosaics. About 15 aggregate ribbons attach to manifolds with stacked ports. The only crossing allowed is the imports branch at the fork, and InstantRoutes logs crossings.  
   *Why:* This is the story and engineering graft ('who in bars, how much in about 15 aggregate ribbons'). Fanning every class into every tier was the default-overload failure all judges flagged.

6. **What scales does M1 use?** Income statement: 5 units = $1T a year for both floors and ribbons. Balance sheet: 1.5 units = $1T owned. People: 1 unit = 1M households or jobs. Each state prints exactly one money or people scale, plus the cloud's log ruler.  
   *Why:* The pixel budget, computed from FOV 45 degrees and the HUD's free rectangle, gives raw at about 21 px, tech about 40 px and the top 1% and bottom fifth at least 25 px in the default frame. Clarity's 1 unit per $1T gave raw 10 px and tech 19 px. Stocks are never drawn on the flow scale.

7. **What does M1 open on?** The structure step: base, both towers with mosaics, the comparison strip, the 100-cent bar, the gate, and the cloud (low). Flows light one family per step (spending, pay, state, abroad, industries, attention), and 'all' is the synthesis.  
   *Why:* Judges rejected defaults with every flow lit. The income arch adds 19.5 $T of height, so raw cannot reach 20 px with the arch in frame. The structure default meets the legibility rule and still answers the five questions through the mosaics, strip, bar and the 'year in five lines' buttons.

8. **Where is the attention cloud, and when is it shown?** Above the towers at two altitudes: low when the arch is not lit, high above the arch when it is (1 s ease). It is shown only in 2025, the snapshot year, labelled 'followers summed across Instagram, TikTok, YouTube and X (2025 snapshot), not money'. Altitude is log followers on printed shelves.  
   *Why:* This keeps the cloud in the default frame (a weakness flagged for story) without overlapping the arch. people.json's own note says the counts are a multi-platform snapshot, which also explains MrBeast above @instagram. Judges require that no snapshot is drawn as history in any other year.

9. **Which palette, and does it pass colour-blind checks?** Clarity's verified set (owners #EFB146, people #6796EE, desire #EF5B98, fear #79E4F0, state #546577) plus commerce #C0B5AA and a new conflict #864AD2. A runtime check on rendered pixels requires at least 8 under deutan and protan for every touching pair, and at least 15 for desire-fear.  
   *Why:* Computed in OKLab with Machado 2009: desire-fear 17.5 deutan (teal gives about 7-9); people-conflict 13.6 deutan and 17.2 protan for #864AD2, against 6.1 deutan for #A35DE4. Judges required people-conflict of at least 8 under both and verification on rendered pixels, not constants.

10. **How are measured, judged, modelled, owed and missing told apart?** One form, one meaning. Solid = measured. Outline with fill alpha proportional to confidence plus 'J' = judged by Jev (not drawn below 0.5). Dotted edge plus '≈' = modelled. Hatch = owed (debts, borrowing, net liabilities). Dashed empty = no data. Thin grey with a year = earlier value. 'assumed:' chips = drawing choices.  
   *Why:* The proposals used hatching for judged marks, debts and missing splits at once. A viewer can only learn a code whose forms each mean one thing. Debts keep the hatch the grafts asked for, and the judged form keeps Jev visibly separate from filed dollars.

11. **Is fear given a dash cue?** No. Fear is separated from desire by lightness (ice L .86 against rose L .68). Dashes are reserved for 'no data'.  
   *Why:* Judges allowed either a lightness-separated set or a dash on every fear mark. Lightness passes (17.5 deutan), and dashes would otherwise carry three meanings.

12. **How are labels rendered?** Extend the existing LabelSystem with owner groups (Replace/Clear), part-bound alpha, four candidate offsets with leaders, HUD exclusion, an underlay halo, degree sizing in XR and clicks. Labels are LDR in the base camera under the hills' 1.1 bloom threshold. No overlay camera and no second canvas.  
   *Why:* This is the engineering judge's must-fix: one backend for desktop, portrait and Quest. LabelSystem cannot remove specs today, and the hills rebuild every year.

13. **Which keys open the new views?** I (one instant; pressing again cycles Income, Balance, People), G (globe: the world, economy scene only) and J (journeys: lives). Steps use panel chips, tour cues and the hand menu; no digits.  
   *Why:* W, A, S, D, Q and E move the camera (W cancels flights), digits are preset keys, and R, N, T, H, V, P, M, L, U, F, Tab, comma, period and the brackets are bound. I, G and J are free in the economy scene and mnemonic.

14. **How do trade lanes read?** Arrows point where the goods go (the key says so). Lane width follows one rule, the exporter's record, with the importer's record in the label and hover. US pairs follow the same rule; the 2025 overlay is labelled 'US customs records'.  
   *Why:* Judges required one convention and rejected buyer-to-seller arrows as inverting the usual reading. The brief recommends exporter-side values with mirrors in labels.

15. **How is M2 laid out?** A continuous west-to-east row of blocks in the stage plane: width = share of world output at market rates (100% = 500 units, so the row is a 100% bar), fixed height = 100% of value added, bases hanging below by the state's spending share. The rest of the world is a visible block. Order is by population-weighted longitude, computed and printed. The camera gives 3.0 px per unit.  
   *Why:* Judges praised clarity's planar row and rejected tilted area maps and the ring (far half right to left). The rest of the world must be visible at 17.8%, and the engineering judge required at least 3 px per unit. Each size channel carries one quantity.

16. **What does M2 show by default?** The 12 US pairs (24 lanes) at subject and the 6 largest other pairs at support. 'All flows', '+ services', 2025, conflict, money and debt, chokepoints, foundations and history are variants, with one dependence layer at a time. The drawn share (about 32%) is computed for what is drawn.  
   *Why:* Judges flagged 66 lanes by default as overload, and a '43.6%' label that is true only when all 156 arcs are drawn.

17. **How are 2026 facts handled?** A gate re-fetches each 2026 item's source. It requires the quote verbatim and the value present, plus Jev support of at least 0.6 when available, failing closed otherwise. Unverified items are not drawn, not even dashed, and are listed as pending. The build fails if a drawn 2026 value lacks a verified quote on or after the as-of date, or if an event's end date has passed without an outcome. Acceptance tests never require items that failed on 3 Nov 2026.  
   *Why:* This is a must-fix from all judges. verify-events.json already failed the Iran war and Hormuz closure, Section 338 on Canada, the India deal, Taiwan pressure and others. The truce and rare-earth suspensions expire on 10 and 27 Nov 2026.

18. **On what basis is world-share history drawn?** One basis per frame: the history wall at purchasing power (Maddison 1950-1979 spliced to WEO PPPSH 1980-2024, with the splice marked). A market-rate variant covers 1980-2024 only, and the row morphs to the wall's basis while the wall shows. The universe link explains 14.8% at PPP against 26.3% at market rates.  
   *Why:* Judges required one basis or a labelled break. PPP continues the universe's streams. Market rates are what trade and debts are paid in, so the floor defaults to them.

19. **How are conflict and military drawn?** Violet marks only for conflict, always in a conflict form: wall, cut, valve, war zone or strait. Coercion and ended episodes (Galwan, Japan-Korea, the India penalty) appear only as strip ticks, and China's pressure on Taiwan is not drawn. Military spending takes the state's colour.  
   *Why:* This is a must-fix: no war scar for coercion or the 2020 Galwan clash, and military in the state colour, not conflict violet.

20. **How is tech's dominance shown?** A linked OUTPUT, JOBS, PROFITS, MARKET VALUE strip. Profits come from BEA 6.16D mapped to tiers by a printed crosswalk and are shown whichever way they fall, with 'no data yet' until acquired, never the 25 firms. Market value shows tech 'at least 31.7%' with the $37.3T unsplit remainder dashed until sector weights arrive. No 'dominates' mark is drawn.  
   *Why:* Tech is 9.4% of value added and 9.6% of gross operating surplus, so a profit-from-surplus claim would be false. Judges required market value, a measured lower bound, no 'about 40%', and acceptance passable on measured data.

21. **Which way does money between industries run on screen?** In the money direction, buyer to supplier: 37.2% up, 21.9% down, 41.0% within with the state as tier 0, and 27.7, 24.9 and 47.4 among market tiers ('supplies come down, the money goes up'). InstantModel asserts it, and WP05 corrects THEORY and EVALUATION before any caption cites the figures.  
   *Why:* Recomputed from circuit.json io in this design pass. The docs and the spatial proposal had it reversed, which would have put a false headline on the author's symbiosis.

22. **Which totals are drawn, and how are the state's accounts kept consistent?** Measured totals only: PCE $20.95T and DFA household debt of about $20.0T (decision 29). The simulation supplies splits, marked ≈. The state buys about $3.16T from industries, market pay is the rest of compensation (about $13.3T, government enterprises included), public pay ($2.46T, decision 32) comes from the base, tariffs stay inside production taxes, and interest is split by holder.  
   *Why:* This is a must-fix. The simulated $21.63T and $14.35T, government consumption of $5.29T, and double-counted pay were errors the judges found in the proposals.

23. **How do lives and visibility work in M3?** A corridor in the stage plane, left of the class tower, with time left to right and no orbit. Income rank is linear, with a magnified top 1% (log net worth) and an attention band (log Wikipedia page views). Three medoid lives start in the middle fifth at year-20, aged 30-39. Real figures are chosen by printed rules. The 2025 follower snapshot is a separate dated star, never joined to the page-view line. Money and visibility are compared on one log index (2016 = 1).  
   *Why:* This grafts the 'paths through the class tower into the cloud' idea while honouring the must-fix that Instagram-only history must never be spliced onto a multi-platform snapshot, and that no snapshot is drawn as history. Frontal layout keeps it readable in the headset.

24. **What does the guided tour do?** Six acts in 21 stops: the wave, then stop time assembling M1 one element per stop to the all-lit synthesis, then the world, lives, you, and resume time. Cards are at most 45 words, with caveat footers and numbers filled from model facts. Old per-view stops leave the main tour. Voicing happens only after the user approves ElevenLabs credits.  
   *Why:* This is the story graft. Judges required that the instant be assembled before the world or lives appear, and that every number come from the built models.

25. **How are parallel packages kept from colliding?** Registration hooks: SmokeChecks and sequences, EconomyEmphasis RegisterView, Contribute and Multiplier, StageViews.Add, IViewKeySource, XrSession.Keep, and the ModelFacts API. Each content layer lives in its own file, and only the hook's owner edits the shared file.  
   *Why:* The quick fixes are editing the same hot files now (HillPresets, DirectorMode, layers), and judges asked that foundation work start from the merged state with packages split by file groups.

26. **What gates a package, and what gates a release?** Packages are gated on screenshots and logged metrics that run in the player smoke test as errors. Each new view's release is gated on a five-person first-look test (at least 4 of 5 per question). The Jev caption audit is supporting evidence only.  
   *Why:* Judges required checks that run in the mirror's player build, not editor-only HillValidation, and said human tests supplement but do not replace screenshot acceptance.

27. **What is out of scope for this build?** Per-economy class glyphs (WID), a judged 'supports or dominates' overlay, cloud and app-store fees, the AI share of the frontier, attention outside the US, and S05, S10, S13, S14 and S17-S20 beyond what the new views absorb.  
   *Why:* WID cells are mostly modelled or missing, firms' AI and defensive confidences are mostly under 0.5, and there is no attention data for other economies. Each would add a scale or a judged mark against the default-overload and honesty rules, without answering an acceptance question.

28. **How do the new views behave on portrait and in the headset?** Portrait: M1 shows the towers only, with the strip and bar in a bottom sheet; M2 shows halves plus a thumbnail; M3 uses the LivesStrip. Headset: dioramas with labels of at least 1 degree, kept canvases, pinch buttons of at least 40 units, draw calls M1 at most 12, M2 at most 20, M3 at most 6, and content that moves while the viewer stays still.  
   *Why:* These are the grafts from clarity and fidelity. Economy panels are hidden in XR unless kept, and the floors must stay at least 20 px in portrait too.

29. **Which household-debt total is drawn?** Every household liability in the Fed DFA: $20.0T at 2026:Q1, of which home mortgages and consumer credit are $18.9T, so assets less debt is net worth in each income group. WP02's check compares that total with $20.0T ± 3%.  
   *Why:* The first figure, $20.6T, matches no DFA measure; it is near the Fed Z.1 debt of households and nonprofits together (about $20.7T), which counts nonprofits too. Corrected 3 Nov 2026 after WP02.

30. **Which national-accounts vintage does M1 draw?** circuit.json's (BEA before its September 2026 update: PCE $20.95T), stored in instant.json as `classes.flows.circuitVintage`, so M1 and the hills agree to the dollar. The current vintage (PCE $20.87T, total $28.31T) is stored beside it for the day circuit.json is rebuilt; then both move together.  
   *Why:* The two vintages are 0.67% apart, more than WP10's 0.5% class-total check allows, and the hills are calibrated on circuit.json.

31. **Which hyperscaler capital spending is drawn?** $411.5B for calendar 2025: each firm's cash purchases of property and equipment, finance leases out (Oracle's four quarters to November 2025), the one definition all five firms report. Each firm's own headline measure sums to $449B; wherever that figure appears it says so.  
   *Why:* Only one definition can be added across the five firms.

32. **Which public pay comes from the base?** General government compensation, $2.46T (NIPA 2025, Table 3.10.5), with general government jobs. Government enterprises (the Postal Service and the like) pay $0.22T more out of their own sales: their pay and jobs count with the market's, so market pay is total compensation less general government's (about $13.3T). instant.json stores the all-government figures too ($2.68T, 21.59M full-time equivalent jobs); WP10 adds general government's jobs from NIPA Table 6.5D to Tools/instant/build.py.  
   *Why:* The base is built from the state's current accounts (NIPA Table 3.1), which cover general government and count enterprises only by their net surplus; paying enterprise wages from the base would need their sales on its other side. Corrects decision 22's first $2.70T.

33. **What stands in for sources that refuse automated access?** CBO's top-1% income shares and S&P Dow Jones Indices' sector weights answered every automated request with a block page (3 Nov 2026); the builds never get around a bot check. The top 1%'s flows come from BEA's distribution with DFA holdings (marked ≈), and listed value by sector falls back to the 25 stored firms: tech at least 31.7%, $37.3T dashed as not split. A person may read either page by hand into sources.json; the builders pick the values up and run their checks.  
   *Why:* Fail closed and say so on screen, rather than estimate a number the sources did not give.

34. **Which real lives does M3 follow?** Elon Musk (together: fortune and readership moved the same way in 8 of 9 years), Donald Trump (apart: 2 of 9) and Oprah Winfrey, the author's pick: her fortune held steady ($2.5B to $3.0B) while her readership fell to 0.63 of 2016. Approved by the author on 3 Nov 2026 (Tools/lives/sources.json). The little-attention rule stays computed and printed without a pick; its fallback proposal (Bill Gates) was declined.  
   *Why:* Nobody in people.json meets the little-attention condition: every candidate on the 2025 Forbes 400 has 10M followers or more. The author chose a third life whose pattern differs from the other two, and her caption states that pattern from the data instead of a rule she does not meet.

# One year, one world, many lives: build specification for the economy's new display modalities

*Lead design, 3 Nov 2026. Base design: **clarity** ("Stop time, zoom out, follow a life"), the highest total judge score (23, against story 20, engineering 19, fidelity 19 and spatial 15). Ideas from story, engineering, fidelity and spatial are grafted onto it, and everything all three judges flagged is avoided. This spec was written against commit cc1950e plus the quick fixes in the working tree (Q01-Q27, not yet merged). Every number below was recomputed from the repository's data files, the global data brief (`.why-2-build/research/global`) or `verify-events.json`. ⟨angle brackets⟩ mark values the build computes and prints; a builder never types them by hand.*

---

## 0. How to use this spec

**Scope.** The spec covers:
- the four shared foundations (S01 emphasis, S02 palette, S03 legend, S04 labels), with the S09 mark code they carry;
- the three new modalities: M1 "One year, all at once", M2 "One economy among many", M3 "Lives that rise, stay or fall";
- how the modalities connect to the wave: the overview's role (S06), the direction of time (S07), the year chip and the replay;
- camera presets and transitions, panels, the guided tour, the data pipeline, provenance on screen, and desktop, portrait and headset.

Other structural items (S05, S10, S13, S14, S17-S20) are out of scope, except where this spec absorbs their substance:
- S11 (dependence between tiers) becomes M1's "between industries" step;
- S12 (frontier and dominance) becomes M1's frontier and comparison strip;
- S15 (status cloud) becomes M1's cloud;
- S16 (one circuit) becomes M1's income statement;
- the parts of S21 the new views need.

**Prerequisite.** Quick fixes Q01-Q27 must be merged first. Every code package starts from that merged state, and every pixel baseline is re-captured then. Three things can start immediately because they touch no code: the data builders (WP01-WP03), the copy of the research into the repository, and the documentation corrections once THEORY.md is free.

**Conventions.**

| Term | Meaning |
| --- | --- |
| Land-local | `EconomyStage.Land()` coordinates, as today: x across the wave (road at 0, hills toward +x, FootX 18), y up (bedrock −18), z along time (`HillLandscape.Z(year)`, present ≈ 0). |
| Stage coordinates (u, v, d) | Used by the three new views (§4): u is screen-right for the stage camera, v is up, d points toward the camera. One stage unit is one world unit. |
| $T | Trillions of US dollars. A flow is $ a year; a stock is $ at a date. |

**Rules every package obeys.** These digest the judges' must-fix lists; WP acceptance criteria test them.

1. **Money direction between tiers.**
   - Of the $15.85T that industries paid to other industries in 2024, 37.2% went **up** to a supplier in a higher tier, 21.9% **down**, and 41.0% stayed **within** a tier. The state counts as the bottom tier, and $2.09T of the "up" is the state buying.
   - Among market tiers alone, the shares are 27.7% up, 24.9% down and 47.4% within.
   - In words: "supplies come down, the money goes up". No caption, test or narration may say "37% down".
2. **Tech's dominance is shown in market value, not profit.**
   - Tech is 9.4% of private value added, 9.6% of gross operating surplus, 7.6% of net operating surplus, and *at least* 31.7% of US listed value (10 stored firms, $23.9T of $75.3T).
   - No "about 40%". No "dominates" mark.
   - Profits come from BEA corporate profits by industry once acquired, and are shown whichever way the data fall.
3. **Measured totals; the simulation supplies splits only, marked ≈.**
   - Household spending is BEA PCE, $20.95T (never the simulation's $21.63T).
   - Household debt is the DFA's, $20.0T at 2026:Q1: every liability, of which home mortgages and consumer credit are $18.9T (never the simulation's $14.35T).
4. **Consistent state accounts.**
   - The state buys ≈$3.16T from industries: $2.09T intermediate plus $1.07T final (2024 table). This is not the $5.29T of government consumption and investment.
   - Market-tier pay is ≈$13.1T; public pay is $2.70T from the base, counted once.
   - Tariffs are already inside production and import taxes.
   - Federal interest is split by holder.
5. **Colour.** In rendered pixels, desire–fear, people–conflict and people–commerce (and every other pair that can touch) separate by ΔE ≥ 8 (OKLab ×100) under both deuteranopia and protanopia.
6. **No compared quantity in perspective.**
   - Every width or length a viewer compares lies in a plane facing the camera.
   - Nothing lies flat on the ground; no box face needs a turned camera; there is no ring.
7. **No default overload.**
   - M1 opens on the structure, and families of flows light one at a time.
   - Labels sit on structures and trunks, at 12 words or fewer.
   - M2 shows the US lanes plus six others by default.
   - A view prints at most 3 scales.
8. **Floors encode dollars and are legible.**
   - In M1's default frame at 1080p, the raw, tech, top-1% and bottom-fifth floors are each ≥ 20 px tall.
   - Nothing is rescaled silently; a magnified inset says "magnified".
9. **One class scale:** household income rank, in the Fed DFA's six income groups (§2).
10. **2026 facts.** No 2026-dated fact is drawn until it is re-verified against its source (§6.8), and every 2026 value is stamped "as of".
11. **Attention data** are one 2025 snapshot of followers summed across Instagram, TikTok, YouTube and X.
    - They are never drawn as history and never spliced onto Instagram-only series.
    - The cloud is hidden in every other year.
12. **Tour order.** The instant is assembled before the world or the lives are shown.
13. **Stage independence.** The new views never depend on S07: the stage is built from world vectors and never mirrored. No hill renderer is active in a stage view.
14. **Checks run in the player.** Every check that gates a package runs in the player's smoke test and logs an error on failure. Editor-only validation is optional.
15. **Keys.**
    - Never W, A, S, D, Q, E (camera), digits (presets), R, N, T, H, V, P, M, L, U, F, Tab, comma, period or the brackets.
    - New keys are **I** (one instant), **G** (globe: the world, economy scene only) and **J** (journeys: lives).

---

## 1. The picture in one page

| View | Question it answers | Camera | Key | Time on screen |
| --- | --- | --- | --- | --- |
| **The wave** (start; exists; S06 + S07 applied) | How has the US economy grown and changed since 1947? | side and oblique | 1 | past left → future right (after S07) |
| **M1 One year, all at once** | How does the economy work at one instant: who earns, spends, owns, owes and pays? | frontal, on the stage | I (pressing again cycles Income → Balance → People) | none (the frame states its year) |
| **M2 One economy among many** | Where does the US stand among the economies it trades and fights with? | frontal, on the stage | G | none (2024); history wall left → right |
| **M3 Lives that rise, stay or fall** | What happens to people in money and in visibility? | frontal, on the stage | J | past left → now → next ten years |

**The 2×2.** This grid is shown in Help, on the chapter bar and on the first-run card:

| | Through time | At one instant |
| --- | --- | --- |
| **United States** | the wave (1) | One year (I) |
| **The world** | Since 1950 (G, then "Since 1950") | One economy among many (G) |

Lives (J) cross the United States row: people through time.

**What a first-time viewer should be able to say after each view, without narration.**

- **M1** (EVALUATION 5.1, with acceptance #4 reworded):
  - which tier produces the most and which is valued the most;
  - how a household dollar splits;
  - which classes own the capital and which earn the pay;
  - that the top tier buys from the tiers beneath it and holds a larger share of market value than of output;
  - where the state takes money in and pays it out.
- **M2** (5.2):
  - which economies are largest, and how that changed since 1950;
  - who sells to whom, and in which direction;
  - whether the US buys more than it sells, and from whom;
  - when conflict cut trade;
  - the US share of the world.
- **M3** (5.3):
  - what happened to each of three lives, and when;
  - how rare each path is;
  - whether visibility followed money.

---

## 2. Corrections to the record

These are findings of this design pass. WP05 corrects the documents before any caption or narration cites them.

1. **Direction between tiers.**
   - Recomputed from `circuit.json` io: rows are suppliers, columns are buyers, money runs buyer → supplier, own-industry purchases are excluded.
   - With the state as tier 0: up 37.2%, down 21.9%, within 41.0%.
   - Market tiers only: 27.7 / 24.9 / 47.4.
   - Money crossing each tier boundary (market tiers):

     | Boundary | Down | Up |
     | --- | --- | --- |
     | raw / make | $1.15T | $0.60T |
     | make / services | $1.76T | $2.88T |
     | services / tech | $0.90T | $0.80T |

   - Tech buys $0.89T from the tiers below (raw 0.02, make 0.17, services 0.71). It sells them $0.80T (make 0.30, services 0.49, raw 0.01), plus $0.29T to the state.
   - THEORY §2 lines 31-32, EVALUATION 5.1 #5 and S11 state the direction backwards.
2. **Dominance.**
   - 2025 tier shares:

     | Measure | Raw | Make | Services | Tech |
     | --- | --- | --- | --- | --- |
     | Value added | 5.0% | 31.2% | 54.4% | 9.4% |
     | Gross operating surplus | 7.2% | 28.1% | 55.2% | 9.6% |
     | Market jobs | 2.2% | 35.8% | 57.1% | 4.9% |

   - Listed value (25 firms as a share of $75.3T): raw 1.4%, make 12.6%, services 4.8%, tech 31.7%, and 49.5% not split by industry.
   - EVALUATION 5.1's acceptance "takes a larger share of profit than of output" becomes "of market value".
3. **Totals.**
   - PCE is $20.95T and household debt is DFA ≈ $20.0T (2026:Q1, all liabilities; corrected from $20.6T, which no DFA measure matches). The simulation's $21.63T and $14.35T are not totals.
   - $185.8T is DFA 2026:Q2. The Q1 groups sum to $174.0T.
   - "The US buys $1.2T more than it sells" is the 2024 current account. The 2025 goods-and-services gap is computed from NIPA 1.1.5 and labelled as such.
4. **People.** The 13 economies hold 55.1% of the world's people, not 45%.
5. **Attention data.**
   - `people.json` counts are one snapshot of followers summed across Instagram, TikTok, YouTube and X. That is why MrBeast (778.5M) exceeds @instagram (686.4M).
   - The anomaly is explained, not an error. Names may be shown with that label.
6. **2026 verification status** (`verify-events.json`, 3 Nov 2026 11:13). These rows failed and are not drawn until re-verified:
   - the Iran war and Hormuz closure (supported 0.58 and 0.37);
   - the attacks on the East-West pipeline (quote not found);
   - Section 338 on Canada, and the 2025 IEEPA tariffs on Canada (both fetch failed);
   - the India interim deal (0.03);
   - the US–Korea and US–Taiwan deals;
   - China's pressure on Taiwan, the EU EV duties and Mexico's non-FTA tariffs (quotes not found);
   - the Greenland threat, the China–Russia "no limits" partnership and the single-market row;
   - the Dec 2024 chip package, the CHIPS Act and the gallium/germanium ban on the US;
   - the US–China "30 for 30" framework;
   - the "Learning Resources v. Trump" row (the separate "Supreme Court ends IEEPA tariffs; Section 122" row passed);
   - the gas cut-off and "trade before and after" rows (their magnitudes come from re-fetched trade data instead).

   The acceptance tests below never require any of these.
7. **Class scale.**
   - Today the scene uses income fifths (mobility, you, the ladder) and the copula-moved wealth groups in `circuit.json`.
   - The new views use **household income rank**, in the DFA's six income groups: bottom 20%, 20-40, 40-60, 60-80, 80-99, top 1%. These are "five fifths and the top 1%", as EVALUATION 5.1 #3 asks.
   - The DFA net-worth shares by income group, already in `circuit.json` groupNotes (2.89, 4.17, 7.82, 13.33, 47.31, 24.47%), are the measured bridge to the wealth ranking. Every key says "ranked by household income".
   - Fallback if the DFA income-group levels cannot be fetched (WP02): the four DFA wealth groups, with the scale printed in every key and the bridge line.

---

## 3. Shared foundations

### 3.1 S01: one emphasis table every layer reads

**Files.**
- `Economy/Emphasis/EconomyEmphasis.cs` and `EmphasisTable.cs` (new).
- `EconomyViews.Table`, the land groups, folds into the same table.

**Keys.**
- Every drawable part is a key `layer.part`, for example:
  - `wave.ribs`, `wave.crest`, `wave.face`, `wave.lives`, `wave.spray`;
  - `capture.owners`, `activity.rivers`, `social.stars`, `land.road`;
  - `instant.trunk`, `instant.arch`, `world.lanes.us`, `lives.paths`.
- A layer holds an int handle, so a lookup is an array read.
- Hill meshes are built with the part's id range (`EmphasisIds.Part(layer, part)`), so `Highlighter` can light a hill part. Today hill geometry carries id 0.

**Levels.**

| Level | Alpha | Use |
| --- | --- | --- |
| Subject | 1.0 | at most 3 parts per view or variant; views flagged *Synthesis* are exempt |
| Support | 0.45 | |
| Context | 0.12 | |
| Absent | 0 | the renderer is deactivated: absent, not faint |

**Row semantics.**
- A view's row lists subject, support and context parts.
- Unlisted parts are absent. Wildcards are allowed (`social.*`).
- Variants are rows keyed `view:variant`.
- Precedence: tour override > variant > view.

**State multipliers.** View-dependent alpha that lives outside today's `Emphasis()` switches registers as named multipliers on keys: HillActivityLayer's level of detail, selection and evidence-only conditions, and the state references in HillCaptureLayer and HillWaveLayer. The rule is `Get(key) = row × Π multipliers`.

**Easing and drawing.**
- LandViewLayer (Order 44, before every hill layer) eases all keys toward the active row at `LandStyle.EmphasisRate` once per frame.
- Layers read `EconomyEmphasis.Get(key)` and call `GraphMaterials.SetAlpha`.
- Meaning-carrying marks are LDR (intensity ≤ 1) and alpha-blended, never additive. HDR (`GraphStyle.HighlightGlow`) is for hover, selection and the tour anchor only.

**Registration hooks.**
- `RegisterView(id, subject, support, context)` lets new views add rows from their own code.
- `Contribute(view:variant, keys, level)` lets several layers light parts of one variant.
- `Multiplier(key, name, Func<float>)` registers a state multiplier.

**Tour overrides.**
- A TourStep gains `emphasis` (key → level or value, `"*"` for the rest) and `lit` (keys at 1, everything else ≤ 0.12).
- DirectorMode pushes the override when a step starts and pops it when the step ends. Cues may push their own.

**Migration.**
- WP07 transcribes today's values for every key and view. The gate is under 0.5% of pixels changed against the merged quick-fix baseline.
- Only then does WP14 retune views, from S06 and EVALUATION 3.4.
- Retuned targets: the landscape, people, rivers and roots frames differ pairwise by ≥ 60/255 over the land (31-34 today).

**Checks** (SmokeChecks; logged as errors):
- every key read is registered;
- every catalog view has a row;
- at most 3 subject parts per row (synthesis views exempt);
- no data mark with intensity > 1;
- no key at ≥ 0.5 in more than 6 views ("every view looks alike");
- in stage views, 0 hill renderers active.

### 3.2 S02: one semantic palette; motive is a split

`EconomyStyle` becomes the only source of economy colour. Values are sRGB targets converted to linear in code.

| Role | sRGB | OKLCH (L C h) | Means, and only this |
| --- | --- | --- | --- |
| Owners | #EFB146 | .80 .14 78 | owners' income and claims: profit, dividends, rent, interest received, market value, ownership threads, net creditors |
| People and pay | #6796EE | .68 .14 262 | people, households, jobs, wages and salaries, synthetic lives |
| Desire | #EF5B98 | .68 .19 358 | money spent toward a want; always beside fear |
| Fear | #79E4F0 | .86 .10 205 | money spent away from a fear; always beside desire. Separated from desire by lightness, so no dash cue is needed |
| State | #546577 for flows and lines; #3A4654 for the base fill | .50 .04 250 / .39 | every government mark: taxes, benefits, public pay, purchases, the base, public debt, military spending |
| Attention | #F5F5F5 | .97 0 – | the most-followed Americans, visibility, attention given |
| Commerce | #C0B5AA | .78 .02 68 | money between businesses, at home or across a border: purchases between industries, advertising, imports, exports, trade lanes |
| Conflict | **#864AD2** | .55 .20 300 | tariffs, sanctions, export controls, war; always in a conflict form (wall, cut, valve, war zone) |
| Matter | #CA4B20 | .58 .17 38 | raw matter (M2 energy exporters only; the universe scene) |
| Life | #33903C | .58 .15 145 | the universe scene only (never touches Matter in the new views) |
| Tier ramp (warm stone, lighter higher) | #534C44 · #69625A · #817970 · #999188 | L .42/.50/.58/.66 | raw · manufacturing & infrastructure · services · tech (structure, not data) |
| Class ramp (cool grey, lighter higher) | #3F4953 · #4F5964 · #606A75 · #717C87 · #828E99 · #94A0AC | L .40-.70 | bottom 20% … top 1% |
| Year shown | #C9CED6 | neutral | the crest, tagged "the year shown" |
| Ink | text #E8ECF2 · dim #A3ACBA · rule #5A6372 · backing #0A0E14 at .82 | | labels and frames |
| Missing | #6B7280 | | no data |
| Chart series | #F08A4B · #E6CF6A · #4FB3E8 · #B0B7C3 | | self-contained charts only (money chart, mobility streams, regime strata, history wall, life strip), with direct labels |

**Pair separations** (computed for this spec on the targets; the runtime check repeats them on rendered pixels). Values are normal / deutan / protan:

| Pair | Normal | Deutan | Protan |
| --- | --- | --- | --- |
| desire–fear | 33.5 | 17.5 | 29.9 |
| people–fear | 21.7 | 21.2 | 22.0 |
| desire–people | 24.8 | 15.7 | 13.8 |
| owners–desire | 24.6 | 17.8 | 24.6 |
| owners–people | 30.5 | 31.8 | 27.6 |
| state–people | 20.8 | 20.1 | 21.9 |
| desire–state | 27.2 | 19.7 | 9.0 |
| owners–commerce | 12.2 | 12.4 | 12.3 |
| people–conflict | 18.0 | 13.6 | 17.2 |
| desire–conflict | 22.9 | 24.2 | 15.5 |
| state–conflict | 18.6 | 14.8 | 15.8 |
| commerce–fear | 14.1 | 10.0 | 13.1 |

The proposals' conflict #A35DE4 fails against people (6.1 deutan) and teal fear fails against rose (≈7-9 deutan), so neither is used.

**Known weak pairs**, kept apart by placement and stated in PaletteCheck's list:
- attention–fear: 7.9 protan. The cloud's points and the spending lanes never touch.
- matter–life: 4.0 deutan. The two never appear together in the new views.

**Rules.**
- `Motive(fear)`, the blend through warm white, is retired. `MotiveSplit(width, fear)` returns two adjacent lanes, and a small mark gets a two-part ring.
- Direction and sign are carried by arrowheads and position, never by hue:
  - climbed and fell are arrows up and down;
  - defection is grey and broken;
  - recessions are neutral bands.
- **PaletteCheck** runs in the player:
  - a smoke-only `palette` view draws every role as the layers draw it (alpha-blended surfaces and lines through the same post-processing);
  - the check samples the rendered pixels, converts them to OKLab, simulates deuteranopia and protanopia (Machado 2009, severity 1), and fails on any listed touching pair under 8, or desire–fear under 15.
- `Tools/check-palette.py` lists every `new Color(` under `Economy/Layers`. The four migration packages bring that count to zero.

### 3.3 The mark code (S09): one form, one meaning

| Form | Meaning | Text |
| --- | --- | --- |
| Solid fill or line | measured | regular type; source and year on hover and in the panel |
| Outline, fill at alpha 0.5 × confidence | judged by Jev, an AI model | italic, "J 0.74" tag. **Below 0.5 it is not drawn**; the panel lists it as "not drawn: too uncertain" |
| Dotted edge | modelled or simulated | "≈" before the number, n printed for simulations |
| Hatch (screen-space 45° stripes, `_Hatch` on Why/Surface) | **owed**: debts, the state's borrowing, net liabilities, deficits | "owes", "borrowed" |
| Dashed empty outline (`_Dash` on Why/Line, built with path phase via `AddFlowPath`) | **no data**: a total not split, a year not covered | "no data" or "not split by industry in the data" |
| Thin grey hairline outline with a year | an earlier value (ghost) | "2021", "2024" |
| Chip "assumed:" | a drawing choice | e.g. "assumed: order of dependence", "assumed: every household holds the market's mix" |
| 1.5 px dotted strand, no width | something that is not money | "not money" (attention given, wants taken in) |

A ribbon whose total is measured but whose split is judged draws both lanes solid. The seam is a 1 px outline, and the label carries "J ⟨confidence⟩", which is the spending-weighted confidence of the item judgments.

`Draw/MarkCode.cs` provides one builder per form. `Draw/FlowRibbon.cs` is the shared ribbon:
- Hermite-routed bands in a plane, with ports and arrowheads;
- width = dollars × scale (checked within 2%);
- below 1.5 px it is drawn as a 1.5 px hairline carrying a "‹" mark and its label.

### 3.4 S03: a legend that belongs to the view

`HillPresets.ViewKey` (quick fix) grows into a `LegendSpec`:
- **entries:** glyph (swatch, lane pair, line, ribbon, bar, thread, ring, arrow, wall, cut, valve, dot, star, hatch, dashed, dotted, ghost), role, a meaning of six words or fewer, and a provenance tag;
- **Grammar line:** what height, width and position mean, with units;
- **Scale bar:** a world length drawn at its projected pixel length, measured each frame at the subject plane (`CameraRig.PixelsPerUnit`);
- **Provenance line:** "Measured: … · J judged by Jev, an AI model: … · ≈ modelled: … · assumed: …";
- **Direction line:** "past ← → future" in side views, "west ← → east" in M2.

Layers supply entries through `IViewKeySource`, filtered to parts eased ≥ 0.5, so a key cannot drift from its picture. HudLegend rebuilds on FocusChanged and on a variant or layer change. It stays visible in the tour (TourPanel shows the key line) and in the headset (HudOverlay is a kept canvas).

At most 3 scale bars or rulers print per view.

### 3.5 S04: labels through the existing LabelSystem (one backend)

LabelSystem stays the only label backend. There is no overlay camera and no second canvas. It gains:

1. **Owner groups.** `Replace(owner, specs)` and `Clear(owner)`, thread-safe. Hill layers rebuild every year, and today specs only grow.
2. **Part binding.** `LabelSpec.Part` binds a label's alpha to its emphasis key through a provider delegate (`HillLabels.PartAlpha`, wired to EconomyEmphasis in WP13). A label is hidden below 0.3.
3. **Size roles.**

   | Role | Height at 1080p (`UiScale`) | Headset |
   | --- | --- | --- |
   | Title | 18 px | 1.2° |
   | Name | 15 px | ≥ 1.0° |
   | Value | 13 px | ≥ 1.0° |
   | Note | 12 px | ≥ 1.0° |

   Heights are clamped to 12-22 px. Portrait uses `PortraitLabelZoom`.
4. **Placement.** Greedy, by priority: Title 10, Name 8, Value 6, Note 4, plus 3 for a subject's labels. Each label tries four candidate offsets at 1.2 em (right, left, above, below), with a 1 px rule-coloured leader when it moves more than 8 px. HUD blocks (title, legend, side panels, tour card, bar, year chip; from HudBlocks) are exclusion rectangles.
5. **Clicks.** `LabelSpec.OnClick` makes the overview's doors clickable and pinchable.
6. **Rendering.**
   - LDR text with a TextMeshPro underlay halo (black, alpha 0.7, about 1.5 px), in the base camera. Bloom's threshold is 1.1 in the hills, so labels never bloom.
   - Labels are billboards, so S07's mirror can never flip text.
   - World-space TextMeshPro under hill layers is removed.
7. **Metrics, per captured view** (SmokeChecks): `[Why] labels <view>: placed N, culled M, overlaps 0, min ≥ 12.0 px (≥ 1.0° in XR), under HUD 0`.
8. **Budgets.** At most 140 labels on desktop and 60 in the headset.

### 3.6 Harness: smoke checks and model facts (WP06)

**`Core/SmokeChecks.cs`.** Packages register:
- per-view checks, `Func<ViewPreset, IEnumerable<string>>`, returning error strings;
- named extra sequences, run with `-smokeextra a,b`: `palette`, `labels-stress`, `transitions`, `budgets`.

SmokeTest calls the checks after every capture and logs failures as errors, so the run exits 1. Only WP06 edits SmokeTest.cs.

**`Core/ModelFacts.cs`.**
- Models publish facts with `ModelFacts.Set(key, value, unit, source, basis)`.
- `-smokefacts` logs `[Why] fact <key> = <value> <unit> | <source> | <basis>`.
- `Tools/export-facts.py` turns a player log into `Tools/model-facts.json`.
- Tour cards resolve `{key}` placeholders from ModelFacts at play time (WP14). An unresolved placeholder is an error.
- The narration is rendered from templates with `Tools/fill-narration.py` (WP36).

---

## 4. The stage (shared by M1, M2, M3)

**StageFrame** (`Economy/Stage/StageFrame.cs`):
- **Origin:** `O = EconomyStage.Land().World(120, 0, 240)`. That is land-local x 120 (the crest's middle) and z +240 (240 units ahead of the present, where nothing of the wave is drawn).
- **Rotation:** `R = Quaternion.LookRotation(−F, Vector3.up)`, where F is the land's forward (toward the future) as a world vector.
- **Mapping:** `World(u, v, d) = O + R·(u, v, −d)`.

The position follows the land, so after S07 the stage still sits behind the crest. The rotation is computed from world vectors and is **never mirrored**, so M1-M3 read the same with the S07 flag off or on.

The overview camera sits at land z ≈ +137. The stage cameras sit at z ≈ +570 to +660, looking back toward the past. Stop time is therefore a pull-back out of the wave.

**Stage views are absent-only.**
- Every wave, land, road and lifeline key is 0 in their rows, and HillExplorer is hidden.
- SmokeChecks asserts 0 active hill renderers in `instant`, `world` and `lives`.
- Stage views never show the wave, so nothing occludes.

**Registration** (`StageViews.cs`):
- `StageViews.Add(id, title, subtitle, key, shortTitle, chapter, poseFn, variants)` adds presets to the catalog, so the content packages never edit HillPresets.
- StageViews is a GraphModule that reads I, G and J itself. It ignores them while a text field has focus and honours `GraphRoot.AllowPresetKeys`.

**Framing.** `StageFrame.Fit(box, freeRect, margin 3%)` returns target and distance so a variant's lit bounding box fills HudBlocks' free rectangle. Camera FOV is 45° vertical, so pixels per unit ≈ 1303.7 / distance at 1080p.

**Picking.** `StagePick` intersects the pointer ray with the stage plane and tests rectangles and ribbon polygons. It uses no physics.

**Panels.**
- `StagePanel` is the shared base of InstantPanel, WorldPanel and LivesPanel. Panels are built from buttons, so they work by pinch.
- Panels register with HudBlocks and are kept in XR through `XrSession.Keep(name)`; WP19 makes the kept set extensible.

---

## 5. M1: One year, all at once (view `instant`, key I)

**Title:** "One year, all at once". **Short title:** "One year".

**Subtitle** (≤15 words): "One year of the US economy: households left, industries right, the state beneath, attention above."

**Design.**
- Two towers stand on the state's base: households ranked by income on the left, industries stacked raw → tech on the right.
- *Who* gets and spends what is read on the floors' faces (area-true mosaics).
- *How much moves* is about 15 aggregate ribbons in the stage plane, one family lit per step.
- The same frame has three **states**: Income statement (default; flows), Balance sheet (stocks), People (head counts). Each state prints one money or people scale.

### 5.1 Scales

| State | Scale | Printed bar |
| --- | --- | --- |
| Income statement | **5 units = $1T a year** (1 unit = $200B), for floor heights and ribbon widths alike | "▬ $1T a year" |
| Balance sheet | **1.5 units = $1T owned**, for bar lengths | "▬ $10T owned" |
| People | **1 unit = 1M households** (classes) or 1M jobs (tiers, base); 1 dot = 1M | "● 1M households" |
| Attention cloud (all states) | log₁₀ followers: 24 units from 10M to 1B (12 units a decade); shelves at 10M, 30M, 100M, 300M | "log scale" |

**Pixel budget** (why 5 units per $T):
- In the structure step the composition runs from the base's bottom (−54) to the cloud's top (+186). At 1080p with the HUD it fits 775 px, so about 3.1 px per unit.
- Raw ($1.37T, 6.85 units) is then ≈ 21 px, tech ≈ 40 px, and the top 1% and bottom fifth ≥ 25 px.
- With the income arch lit, 19.5 $T of height is added. Raw drops to ≈ 15 px there, where floors are support. That is why the default step is the structure (§5.3).

### 5.2 Geometry (stage u, v; nominal 2025)

Positions come from `InstantLayout` rules. The u-ranges are fixed; heights follow the data.

| Element | Geometry | Encoding and data | Label (screen-space) |
| --- | --- | --- | --- |
| **The state (base)** | u −175…267; v −(outlays × 5)…0 (2025 ≈ −54); depth d −10…+10 | Slate fill. Front-face strata, top to bottom: the state's own work (schools, police, courts, defense: public value added $3.45T), benefits, purchases from industries, interest, other. A light line at receipts; the band below it, the borrowing, is **hatched**. Data: NIPA Table 3.1 annual (WP02); until then circuit.json Q4 annual rate. | "THE STATE · all levels of government (NIPA ⟨year⟩) · spends $⟨10.8⟩T · takes in $⟨9.0⟩T · borrows $⟨1.9⟩T" |
| **Class tower** | 3-unit manifolds at u −125…−122 (in) and −58…−55 (out); floors u −122…−58 (64 wide); six floors stacked from v 2 with 2-unit gaps: bottom 20%, 20-40, 40-60, 60-80, 80-99, top 1% | Floor height = the group's gross income a year (personal income + social-insurance contributions) × 5. Total $28.12T, 140.6 units. Class ramp fill. **Face mosaic** (area-true 100% bar across the floor): pay (blue) · from owning (gold) · from the state (slate) · borrowed (hatched). In the spending step it switches to uses: toward a want (rose) · away from a fear (ice) · taxes (slate) · saving (gold) or borrowing. Data: instant.json (BEA distribution of personal income, CBO top 1%, CE, DFA by income); group splits ≈ (source year carried to ⟨year⟩). | Inside each floor: "TOP 1% · 1.35M households · $⟨x⟩T a year". Before the first split year, one undivided block: "households · not split by income before ⟨year⟩" |
| **Tier tower** | manifolds u 55…58 (in) and 122…125 (out); floors u 58…122; four floors from v 2, gaps 2 | Floor height = value added × 5 (2025: raw 6.85, make 42.65, services 74.25, tech 12.85 units). Tier ramp fill (same names, order and stone as the crest face). **Face mosaic:** pay (blue) · production taxes (slate) · upkeep (stone, ≈ estimated depreciation shares) · owners (gold). Data: industries.json (BEA 2025; compShare, taxShare, depShare). | "RAW MATERIALS · $1.37T · 2.8M jobs"; "MANUFACTURING & INFRASTRUCTURE · $8.53T · 45.0M"; "SERVICES · $14.85T · 71.8M"; "TECH · $2.57T · 6.1M · platforms (social media, cloud, AI), software, chips, media & telecom" (two lines where needed; leader when the floor is under 2 lines) |
| **Comparison strip** (structure and industries steps only) | right of the tier tower: link band 8, then JOBS, PROFITS and MARKET VALUE columns, each 10 units wide with 8-unit link bands (u 133…187); each column is 100% of the tower's height, tiers in the same order, linked at alpha .35 | OUTPUT is the tower itself (5.0 / 31.2 / 54.4 / 9.4%). JOBS: market jobs (2.2 / 35.8 / 57.1 / 4.9%). PROFITS: BEA NIPA 6.16D corporate profits by industry mapped to tiers (crosswalk printed, "assumed: mapping"); until WP02 lands, a dashed "no data yet" column, **never the 25 firms**. MARKET VALUE: listed value by tier. Until sector weights are sourced: the 25 stored firms over $75.3T, with tech "at least 31.7%" and a dashed segment "not split by industry: $37.3T (49.5%)". | Column heads "OUTPUT · JOBS · PROFITS · MARKET VALUE"; tech row printed: "⟨9.4⟩% · ⟨4.9⟩% · ⟨…⟩ · at least ⟨31.7⟩%" |
| **One household dollar** | 100% bar, 40 × 6 units, in the channel at the tier tower's foot (u 12…52, v 24…30); at the fork in the spending step | Shares, not dollars (printed "100¢"): pay 41 · owners 27 · upkeep 15 · imports 10 · taxes on production 7. Data: ValueCapture 2025 ledger (Leontief split of household purchases; ≈ 2024 input-output ratios). | "ONE HOUSEHOLD DOLLAR at the market (⟨year⟩, before income taxes)" |
| **ABROAD gate** | doorway outline u 251…267, v 0…40 | Commerce sand; clickable door to M2 | "ABROAD · the world (G)" |
| **Frontier** | rises 20 units from the tech floor's top at u 90; width = $ × 5 | Gold, alpha fading upward. Measured: the 10 tech-tier firms' capital spending ($353.7B) + R&D ($240.5B) = $594B (filings, latest fiscal year). No AI or fear/desire split: most confidences are under 0.5. | "FRONTIER · $594B a year into what comes next (10 largest tech firms) · all US private investment $5.55T (BEA 2025)"; panel footnote: hyperscalers' capital spending, calendar 2025, one reconciled figure (WP02) |
| **Attention cloud** | u −125…125; **low** v 162…186 when the income arch is not lit; **high** v 268…292 when it is (eases 1 s; labels replaced when the ease ends) | White points of one size; altitude = log₁₀ followers; x from the social graph's layout. The 248 US people with ≥ 10M followers. Shown **only when the year is 2025**; otherwise a faint outline "attention: one 2025 snapshot". | "ATTENTION · the most-followed Americans · followers summed across Instagram, TikTok, YouTube and X (2025 snapshot) · not money"; top 12 named with leaders (MrBeast 779M … Jennifer Lopez 312M) |

### 5.3 Steps of the income statement

Each step is a ViewVariant, captured by the smoke test as `economy-view-instant-<name>.png`. Its subject is at 1 and everything else at ≤ 0.12 or absent. Captions are computed.

| Step | Lit (subject) | Caption template |
| --- | --- | --- |
| `structure` (**default**) | base, both towers with mosaics, comparison strip, 100¢ bar; cloud and frontier at support | "Households (left) and industries (right) stand on the state; each floor is as tall as its dollars a year." |
| `spending` | trunk, imports branch, fork with 100¢ bar, class use-mosaics | "Households spent $⟨20.95⟩T: ⟨d⟩% toward a want, ⟨f⟩% away from a fear (J ⟨c⟩); ⟨m⟩¢ of each dollar bought imports." |
| `pay` | arch (pay and owners' income), class source-mosaics, owners abroad | "Industries paid $⟨13.1⟩T in pay to every floor and $⟨6.5⟩T to owners, mostly the top floors; $⟨a⟩T to owners abroad." |
| `state` | base strata, both tax downpipes, state-to-households riser, purchases riser, borrowing hatch | "The state took in $⟨r⟩T and paid out $⟨x⟩T: benefits $⟨b⟩T, pay $⟨p⟩T, purchases $⟨q⟩T, interest $⟨i⟩T; it borrowed $⟨d⟩T." |
| `abroad` | imports conduit, exports riser, owners and interest abroad, gate | "Paid for imports $⟨m⟩T; earned from exports $⟨x⟩T; the gap $⟨g⟩T (goods and services, ⟨year⟩). Press G for the world." |
| `industries` | boundary arrows, comparison strip, frontier | "Supplies come down, the money goes up: ⟨37⟩% of the money between industries to a higher tier, ⟨22⟩% down, ⟨41⟩% within (2024)." |
| `attention` | cloud, five tethers, attention loop | "Companies paid platforms $⟨295⟩B to reach people (IAB 2025); people gave about ⟨114⟩ minutes a day." |
| `all` (synthesis) | trunk, arch, state flows, abroad | "One year's circuit: spending in, pay and profits back, taxes down, benefits and purchases up." |

**States** (also variants): `balance` and `people`.

**Panel steps.** Chips: Structure · Spending · Pay & profits · The state · Abroad · Between industries · Attention · All. State buttons: Income statement | Balance sheet | People. Pressing I again cycles the states. There are no digit keys.

### 5.4 The year's money: ribbons, ports and routing

Aggregate ribbons attach to tower **manifolds**, not to individual floors. Per-floor shares are read on the faces.

| Ribbon | Route | $ (2025) | Colour and provenance |
| --- | --- | --- | --- |
| Household spending (2 lanes) | class out-manifold, top part → across the channel → fork at u ≈ 20 → value-added part into the tier in-manifold's top part | $20.95T (PCE); value added ≈ 90%, imports ≈ 10% | rose and ice lanes; total measured; split judged ("J ⟨conf⟩") |
| Imports | peels down from the trunk at the fork (u 20…28) → into a conduit on the base's front face (v −4…−10) → east to the gate | ≈ $⟨2.1⟩T of household spending; the conduit carries all imports $⟨4.1⟩T (NIPA) | commerce; arrowheads east |
| Pay (private) | tier out-manifold, top part → right leg (u 133…231) → over the top (inner edge v ≈ 160) → left leg (u −275…−177) → class in-manifold, top part | ≈ $13.1T (market compensation) | blue, inner strand |
| Owners' income to persons | same arch, outer strand | proprietors' $2.11T + rental $1.11T + dividends $2.25T + private interest (personal interest minus government interest to persons) ≈ $⟨6.5⟩T | gold |
| Owners abroad | tier out-manifold, middle → right, under the arch's leg → gate | NIPA 4.1 income payments ≈ $⟨0.5⟩T | gold |
| Kept by firms and upkeep | stub loop on the tier out-manifold → "reinvested" | upkeep ≈ $⟨4.2⟩T (≈ estimated depreciation shares) + retained $1.12T | stone and gold stub |
| Investment | stub into the tier in-manifold | private fixed investment $5.55T (value-added part) | gold stub |
| Taxes from industries | tier out-manifold, bottom → downpipe → base | production and import taxes $2.13T (include customs duties) + corporate $0.70T | slate |
| Taxes from households | class out-manifold, bottom → downpipe → base | personal taxes $3.23T + contributions $2.02T | slate |
| The state to households | base → riser (u −169…−127) → class in-manifold, bottom part | public pay $2.70T (21.6M jobs) + benefits $4.85T + government interest to persons ≈ $⟨0.9⟩T | slate (pay blue-edged) |
| The state buys | base → riser beside the tier tower → tier in-manifold | ≈ $3.16T ($2.09T intermediate + $1.07T final; 2024 table scaled ≈) | slate |
| Exports | gate → base-front conduit west → riser into the tier in-manifold's bottom | $⟨3.2⟩T (NIPA) | commerce |
| Saving and borrowing | class stubs | upper groups save (gold); lower groups borrow (hatched) | from the group statements ≈ |
| Interest and other payments | class stub | interest paid $0.58T + transfers paid $0.29T | stone stub |

**Routing rules** (in `InstantRoutes`):
- Within each manifold, ports are stacked so that no two ribbons cross. Inputs from above enter the upper part; inputs from the base enter the lower part.
- The only crossing allowed is the imports branch leaving the trunk at the fork.
- `InstantRoutes.Crossings()` is logged and must be 0 outside the fork.
- Every node balances (in = out) within 1%, measured from drawn widths. A residual over 1% is drawn as a labelled grey "other" stub, never forced.
- Labels go on trunks and structures only, at 12 words or fewer. Detail goes to hover and the panel.

**Selection.** Clicking a class or tier floor lights its share of every ribbon as a brighter sub-band along the ribbon's edge (computed per group in InstantModel). The panel shows that floor's own statement, line by line, with its basis.

### 5.5 Between industries (S11 + S12 within M1)

**Boundary arrows.** At each tier boundary, in the channel beside the tier tower, two vertical arrows cross the boundary line. They point where the money goes, and their widths are on the flow scale:

| Boundary | Down | Up |
| --- | --- | --- |
| raw / make | $1.15T | $0.60T |
| make / services | $1.76T | $2.88T |
| services / tech | $0.90T | $0.80T |

**Labels.**
- On the arrows: "money between industries, 2024: ⟨37⟩% up · ⟨22⟩% down · ⟨41⟩% within".
- Panel: the market-tier shares 28/25/47 ("they feed each other almost equally"), and "tech buys $⟨0.89⟩T from the tiers below and sells them $⟨0.80⟩T, plus $⟨0.29⟩T to the state".
- Panel: a 5×5 who-buys-from-whom matrix ordered by tier, with the state as row and column 0.

**Assertion.** InstantModel computes the direction from io and fails Problems() if any caption fact disagrees.

**Dominance** is read off the comparison strip. No "supports" or "dominates" mark is drawn in this build.

### 5.6 Attention (S15 within M1)

**Tethers.** The cloud stays above the money, apart from the top-1% floor. Five gold-ringed points mark the capital holders in the cloud, whose firms Jev matched (J):
- Elon Musk, 242.5M (Tesla, SpaceX: make);
- Bill Gates, 77.0M (Microsoft: tech);
- Mark Zuckerberg, 26.9M (Meta: tech);
- Tim Cook, 19.8M (Apple: tech);
- Jeff Bezos, 15.0M (Amazon: make).

They drop dotted tethers to the top-1% floor and to their tier: "where attention and capital meet".

**Attention loop** (attention step):
- Four labelled nodes: COMPANIES (tier tower), PLATFORMS (tech floor), CREATORS (cloud rim), HOUSEHOLDS (class tower).
- Money legs on the flow scale in commerce sand: digital ads $294.6B (IAB/PwC 2025, ≥ 1.5 px), and creators ≈ $⟨37⟩B paid by brands.
- Attention given: a dotted white strand ("about ⟨114⟩ minutes a day", eMarketer, "not money").
- Wants taken in: a dotted rose strand ("not money").

### 5.7 Balance sheet (state `balance`)

The flows fade (0.5 s) and the floors morph to equal-height rows (1.2 s), with each group's size printed: "TOP 1% · 1.35M households". Each row is a bar chart with a common baseline at its block:

- **Class rows.**
  - Assets run outward (leftward from u −125) by type: company shares and businesses (gold), homes, pensions, deposits and bonds (stone and grey tints).
  - Debts run inward (rightward from u −58), **hatched**: mortgages, consumer credit.
  - Row label: "net worth $⟨x⟩T (⟨24.5⟩% of all) · DFA ⟨2026 Q1⟩".
  - Bridge line: "ranked by wealth instead, the top 1% hold 31.6%".
- **Tier rows.**
  - Listed market value runs outward (rightward from u 125), in segments named by firm (NVIDIA 5.49, Apple 4.84, Alphabet 4.13, Microsoft 3.78, Meta 1.70 …).
  - A fifth row reads "not split by industry in the data: $37.3T", as a dashed empty bar.
  - Value added stays as a thin tick.
- **Threads:** **1 gold thread = $1T of claims on listed US companies** (75 threads).
  - They come from a gold manifold beside the tier rows: 18.4% (14 threads) to ABROAD (Fed Z.1); the rest fan to the class rows' ownership segments in proportion to DFA corporate equities and mutual funds by income group, plus the equity part of pensions.
  - Tags: "≈ assumed: every household holds the market's mix" and "≈ pensions 45% equity".
- **The state:** a hatched bar "THE STATE OWES $⟨debt⟩T". It is segmented by holder (Federal Reserve, US holders, abroad $⟨9.33⟩T, TIC Aug 2026 after re-verification) and labelled with the definition (federal debt held by the public; state and local separate).
- **ABROAD:** a hatched bar, "the rest of the world owns $22.1T more of the US than the US owns abroad (end-2024)".
- **Cloud and scale.** The cloud stays, since followers are a stock. Scale bar "▬ $10T owned".
- **Stock-flow note:** "Each year's saving and borrowing change these stocks; so do prices, which the income statement does not show."

### 5.8 People (state `people`)

- **Class floors:** height ∝ households, 1 unit = 1M. Each fifth is about 27 units, 80-99 is 25.7, and the top 1% is 1.35, labelled "magnified label: 1.35M households".
- **Dots:** 1 dot = 1M households, coloured by main income source, from the simulation (≈, n printed): pay (blue), owning (gold), the state (slate: public payroll, Social Security, safety net), none (grey: students, homemakers). The top 1% holds about one dot.
- **Tier floors and base:** tier floors ∝ jobs (raw 2.8, make 45.0, services 71.8, tech 6.1); the base ∝ 21.6M public jobs.
- There are no ribbons in this state. The key says "the pay arch in the income statement joins them".

### 5.9 Years and the replay

M1 follows the year chip:

| Data | Coverage |
| --- | --- |
| Value added | 1947-2026 (2026 ≈ estimate) |
| NIPA flows | 1950-2025; selecting 2026 shows 2025, with a note |
| Class splits | from the first year of BEA's distribution of personal income; earlier, one undivided block, with a note |
| Balance sheet | from 1989 (DFA); earlier rows say "no data" |
| Input-output ratios | the 2024 table is "≈ assumed for every year" |
| Cloud | 2025 only |

In M1, R plays the replay as a flip-book of instants. Rebuilds go through `HillRebuilds.Take()` from a model computed and cached by year on a worker. No frame may exceed 50 ms.

### 5.10 InstantModel: identities and checks (`Model/InstantModel.cs`)

The model is pure C#, built on a worker per year, and reads:
- `LandSnapshot.Circuit` (MoneyCircuit), circuit.json, industries.json;
- ValueCapture, with its Leontief split and the Jev item judgments shrunk toward category priors (confidence carried);
- instant.json, the 25 firms (circuit.json capture), corporate-programs.json, people.json;
- EconomicLives, for role counts only (≈).

**Problems()** runs at load and logs errors:
- each class node: in = out within 1%; the class total equals personal income + contributions within 0.5%;
- each tier node: in = value added = out within 1%;
- the state: in = out within 1%, the residual drawn as "other";
- the fork sums to 100;
- the direction shares recomputed from io match the facts;
- PCE equals NIPA PCE;
- every label fact has a source and a year.

It publishes ModelFacts, for example `instant.pce`, `instant.fork.wages`, `instant.io.up`, `instant.tech.mvShare`.

---

## 6. M2: One economy among many (view `world`, key G)

**Title:** "One economy among many". **Short title:** "World".

**Subtitle:** "The world economy in 2024: each block is as wide as its share of world output; lanes carry goods."

### 6.1 The row (west to east)

The 13 economies and the rest of the world stand as one continuous row of blocks in the stage plane. **100% of world output = 500 units** (1% = 5 units). The blocks touch, separated by dark 0.6-unit lines, so the row *is* a 100% bar, and a rule along its base reads "0 ─── 100% of world output, $111.9T (IMF WEO Oct 2026, 2024, market exchange rates)".

**Order.** West to east by the longitude of each economy's population-weighted centroid. WP04 computes the longitudes from Natural Earth populated places (pop_max), and the rule is printed on the floor: "west → east by where people live".

**Expected order:** Mexico, United States, Canada, Brazil, United Kingdom, EU27 (Germany drawn inside, never added), Saudi Arabia / Russia (as computed), India, China, Taiwan, Korea, Japan, then the rest of the world.

**Geometry.** The US block is centred at u 0 (−65.6…65.6); the row spans about u −74…426.

| Economy | Width (units) | Share | GDP 2024 |
| --- | --- | --- | --- |
| Mexico | 8.2 | 1.64% | $1.83T |
| United States | 131.3 | 26.25% | $29.38T |
| Canada | 10.2 | 2.03% | $2.27T |
| Brazil | 9.8 | 1.95% | $2.19T |
| United Kingdom | 16.6 | 3.32% | $3.71T |
| EU27 | 87.6 | 17.51% | $19.59T |
| Saudi Arabia | 5.6 | 1.12% | $1.25T |
| Russia | 9.8 | 1.95% | $2.19T |
| India | 16.8 | 3.36% | $3.76T |
| China | 84.6 | 16.92% | $18.93T |
| Taiwan | 3.6 | 0.72% | $0.80T |
| Korea | 8.4 | 1.68% | $1.88T |
| Japan | 18.8 | 3.76% | $4.21T |
| Rest of the world | 89.0 | 17.79% | $19.91T |

The rest of the world is a grey block labelled "the rest of the world · 176 economies together, not one".

**Camera.** Target (u 176, v 40), pitch 8°, distance 435, giving **3.0 px per unit** at 1080p. The default has no side panel, so the 500-unit row fits the 1880 px width. Taiwan is about 11 px wide, with a leader label.

**Totals line:** "The world economy, 2024: $111.9T · the US: 26% of output, 4% of people, 37% of military spending · these 13: 82% of output, 55% of people".

### 6.2 Blocks

| Part | Encoding | Data |
| --- | --- | --- |
| Width | share of world GDP (one quantity) | IMF WEO Oct 2026 (2024); PPP toggle |
| Block height | fixed 32 units = 100% of the economy's value added | — |
| Strata, bottom to top | agriculture / industry excluding manufacturing / manufacturing / services, normalised to 100% ("shares of value added at basic prices, normalised") | WDI 2024 (OECD for the US, labelled; DGBAS for Taiwan; Canada 2022, marked) |
| Base (Foundations layer at full strength; context otherwise) | hangs below the block; depth = general government spending % of GDP × 0.4 units (US 15.0, EU 19.6, Taiwan 6.3); a revenue line; so area ∝ the state's dollars | IMF Fiscal Monitor 2024 (EU27: Eurostat) |
| Label | under each block | "CHINA · $18.9T · 16.9% of world output" |

### 6.3 Trade lanes (default layer)

- **Shape.** Each pair is one arch in the stage plane above the row: apex = 0.22 × span + 10.
- **Lanes.** Two concentric lanes per pair. The outer lane carries eastbound goods, the inner lane westbound.
- **Arrows point where the goods go**, and the key says so: "arrows: where the goods go; the money goes the other way".
- **Width.** 1 unit = $100B a year. A $100B bar is printed.
- **Record.** One rule: **the exporter's record**, with the importer's record in the label and hover. Example: "China → US $525B (China's record; US customs $440B) · US → China $143B".
- **Data:** IMF IMTS 2024, Census for US exports, Taiwan MOF.
- **Special cases:**
  - Russia's cells are partner-built: drawn at the ghost style's alpha with "partner records".
  - Taiwan → China carries a "via Hong Kong" stub.
  - Intra-EU trade ($4.34T) is a label on the EU block, never an arc.
- **Default set:**
  - all 12 US pairs (24 lanes) at subject;
  - the 6 largest other pairs at support (China–EU 747.6, EU–UK 594.3, China–Korea 280.3, China–Japan 276.7, China–Russia 227.7, China–Taiwan 172.3).
  - The drawn lanes carry ⟨32.1⟩% of world goods exports outside intra-EU trade, computed for the drawn set. The 43.6% applies only to the "All flows" variant (156 lanes).
- **US services.** "+ services" adds hollow US lanes (BEA 2024). Other pairs are "not researched".
- **2025 variant.** US imports by partner (Census), with 2024 as a ghost hairline and 2025 solid: China −29.9%, Taiwan +73.2%. Labelled "US customs records, before and after the 2025 tariff shock".

### 6.4 Conflict (layer; violet, always a conflict form; verified events only)

| Form | Mark | Examples (drawn only if verified in global.json) |
| --- | --- | --- |
| Tariff wall | translucent violet slab across the lane at the importer's end; height 1 unit per 2 percentage points of the average rate; earlier rate as a ghost | US on Chinese goods ⟨35.2⟩% and China on US goods ⟨31.9⟩% (PIIE, ⟨22 Aug 2026⟩; "statutory rates on fixed 2017 trade, not duties collected"); Section 301 rates of 10-12.5% on other US import lanes (verified row) |
| Sanction cut | the lane at its 2024 width, its 2021 width as a ghost, a break mark | US imports from Russia $29.6B (2021) → $3.0B (2024); EU, UK, Japan, Korea and Canada likewise; rerouting: India 8.7 → 65.7, China 78.4 → 128.3 |
| Export-control valve | violet diamond on the lane | US chip-making tools to China $6.81B (2021) → $2.28B (2025); China's rare-earth controls, with the suspension's expiry ("to 10 Nov 2026") |
| War | a violet zone with an X at the block's edge, with dates | Russia's war on Ukraine (from 24 Feb 2022; the civilian toll printed only if the OHCHR 2026 figure is re-verified). The Iran war and Hormuz are **pending**: listed, not drawn |
| Strait | a narrow gate glyph on the lanes it touches | Bab el-Mandeb 8.7 → 4.0 mb/d (2023 → Jan-Aug 2024), Suez tonnage −70%; Hormuz drawn with its 2024 flow (20 mb/d) only, the closure pending |

**Rules.**
- Conflict marks are for conflict only. Coercion, and ended episodes (Japan–Korea 2019-23, the India penalty, the Galwan clash), appear as dated ticks in the event strip only. China's pressure on Taiwan is not drawn.
- Military spending takes the state's colour.
- **Event strip** (HUD, `ConflictStrip`): 2018 at the left, as-of date at the right. Each tick is a drawn event; selecting one lights its marks through an emphasis override. The strip shows "as of ⟨date⟩" and expiry flags for events ending within 30 days (the truce on 10 Nov 2026; the rare-earth suspensions on 10 and 27 Nov 2026).

### 6.5 Dependence layers (one at a time)

- **Money & debt.**
  - Gold threads from holders into the US base, **1 thread = $100B of US Treasuries** (TIC, end-Aug 2026, re-verified): EU 2,145, Japan 1,068, UK 1,010, China 591, Canada 465, Taiwan 298 …, with the note "custodial: TIC records the custodian's country".
  - Net international investment positions, 1 unit = $500B: creditors as gold bars above the block, debtors as hatched bars below the base (US −$22.1T).
  - Current accounts in the labels (US −$1.2T, 2024: "the current account").
- **Chokepoints.** Keystone 100% bars float above the main holder, segmented by holder, with leader spokes; scope and year are printed:

  | Keystone | Holders | Source |
  | --- | --- | --- |
  | logic below 10 nm | Taiwan 69, Korea 31 | 2022 |
  | magnet rare-earth refining | China 91 | 2024 |
  | rare-earth mining | China 71, US 12 | |
  | gallium | China 98.9 | |
  | lithium refining | China 70 | |
  | cobalt mining | DR Congo 74.8, in the rest of the world | |
  | crude output | US 16.2, Russia 12.1, Saudi Arabia 11.2 | |
  | LNG exports | US 21.0 | |
  | data-centre electricity | US 45 | |
  | AI compute | US 77, China 9, EU 5 | Epoch, end-2024 |

  Straits as in §6.4.
- **Foundations.**
  - Bases at full strength with their revenue lines.
  - A military stratum in the state's colour (% of GDP on the same 0.4 scale), with "$1.0T · 3.4% of GDP · 37% of world".
  - A V-Dem 2025 electoral democracy gauge (0-1) and the Regimes-of-the-World class on the base's face; EU27 is the population-weighted mean, labelled.
  - An energy glyph: ▲ net exporter (matter red) or ▼ net importer, with % (WDI 2024).
  - The regimes view stays, labelled as a thought experiment. A block's card links "what if: the US hills on this state".

### 6.6 Since 1950 (variant `history`) and the universe hand-off

**The history wall** (`WorldHistory`) stands in the stage plane below the row: u −74…426, v −230…−110.
- It is a 100% stacked area chart, 1950 at the left and 2024 at the right (6.75 units a year).
- Bands are stacked west → east from bottom to top, with the rest of the world on top, in chart greys with direct labels at the right end. The US band has a white outline.
- **One basis per frame: purchasing power (PPP)**, the universe streams' measure.
  - Maddison Project 2023 covers 1950-1979 and IMF WEO PPPSH covers 1980-2024. A vertical tick at 1980 reads "source changes: Maddison → IMF (both at purchasing power)".
  - The USSR (1950-1991) seams into Russia.
  - EU27 is the sum of its members.
- **Market-rate toggle:** WEO NGDPD, 1980-2024. Before 1980 it is a dashed "no market-rate series here".
- When the wall shows, the row's blocks morph to the same basis.
- Caption at the junction: "At purchasing power, as in the universe's streams, the US is 14.8% and China 19.3%; trade and debts are paid at market rates, where the US is 26.3% and China 16.9% (2024)."

**Universe hand-off.** The universe's "Power shifts again" card gains a link, "Today's world economy ▸". It opens the economy scene on `world:history`, then lowers to the row. The universe tour itself continues unchanged.

### 6.7 WorldModel (`Model/WorldModel.cs`, `Land/WorldLayout.cs`)

The model parses global.json, lays out widths and the order, routes lanes (arch paths shared with the conflict walls) and selects the default lane set. It publishes ModelFacts.

**Problems()** checks:
- shares sum to 100 ± 0.1;
- Germany is never added to EU27;
- every drawn 2026 value is verified with verifiedOn ≥ asOf;
- no drawn event is past its stated end date without a recorded outcome;
- every lane's width equals $ × scale within 2%.

### 6.8 The 2026 gate (`Tools/global/verify.py`)

For every value or event dated 2026 (events, TIC Aug 2026, PIIE Aug 2026 averages, OHCHR Sep 2026, NATO 2026, Euroclear Jun 2026, 2026 Brent prices, COFER 2026):
1. Fetch the source URL (cached, with the fetch date).
2. Find the quoted passage **verbatim** (whitespace-normalised) and check that the value appears.
3. If the Jev credential is available, ask Jev whether the page supports the claim (≥ 0.6). This is model evidence, stored as `supportedJ`. If Jev is unavailable, the gate fails closed.
4. Write `status` (verified or unverified), `verifiedOn`, `quote` and `supportedJ` into global.json.

Unverified items are **not drawn, not even dashed**. WorldPanel lists them under "pending verification". Every 2026 label carries "as of ⟨verifiedOn⟩". The build fails if a drawn 2026 value lacks a verified quote on or after the as-of date. Because the truce and the rare-earth suspensions expire on 10, 10 and 27 Nov 2026, a rebuild after those dates must record the outcome or the events drop out.

---

## 7. M3: Lives that rise, stay or fall (view `lives`, key J)

**Title:** "Lives that rise, stay or fall". **Short title:** "Lives".

**Subtitle:** "Three simulated Americans from the same start, by income rank; real public figures' money and visibility above."

### 7.1 Layout (stage plane, left of the class tower, time left → right)

| Element | Geometry | Scale |
| --- | --- | --- |
| Past corridor | u −350…−200: ⟨year−20⟩ → ⟨year⟩ at 7.5 units a year | — |
| "The next ten years" zone | u −200…−125, an empty dashed outline: "not yet lived" (the viewer's futures appear here) | — |
| Class block, rank-scaled ("now", the same column as M1's class tower) | u −125…−55 | — |
| Income rank band | v 2…102: fifths 20 units each; 80-99 at 19 units; the top 1% a 1-unit sliver | **1 unit = 1 percentile point (linear)** |
| Magnified top 1% | v 108…132, joined by a funnel from the sliver | **net worth, log scale** ($10M → $1T, 4.8 units a decade), printed "magnified · log scale" |
| Attention band | v 140…176 | **monthly English-Wikipedia page views, log scale** (1K → 100M), printed |

**Camera:** target (u −250, v 92), pitch 4°, distance 330 (≈ 4 px per unit). There is no orbit.

**"Turn to lives" transition** (1.6 s): the class tower morphs from dollar heights to rank heights, the tier side and flows ease to 0.12 then absent, the bands extend left and the camera pans left.

### 7.2 Three synthetic lives (`Model/LivesModel.cs`)

- **Start group:** simulated adults aged 30-39 in the **middle income fifth** (40th-60th percentile) in ⟨year−20⟩. If fewer than 60 records qualify, widen to the 2nd-4th fifths and say so.
- **Outcomes at ⟨year⟩:** climbed (one or more fifths up), stayed, or fell (one or more fifths down).
- **Each life drawn** is the **medoid** of its outcome group's rank paths (L1 distance), among records present every year. The seed is fixed.
- **Paths:** people-blue polylines told apart by style: climber solid, stayer long dash, faller short dash.
  - Each is named at its right end: "synthetic person #⟨N⟩ · climbed: middle → fourth fifth".
  - The parents' rank is a grey dashed reference line.
  - The selected life's wealth rank is a thin gold line.
- **Events:** 3-6 named ticks per life from `PersonLife` (first job, change of industry, job lost, marriage, divorce, child, home, business, inheritance, debt discharged, retirement). Each says what changed: "2009 · lost a job · income rank 62 → 38".
- **Base rate bar:** "Of ⟨n⟩ simulated records like them (1 record = 100,000 people): climbed ⟨a⟩% (90% range ⟨lo-hi⟩), stayed ⟨b⟩%, fell ⟨c⟩%" (Wilson intervals).
- **Footer:** "events are associations in the simulation, not causes".
- In the attention band, synthetic lives show a grey stub: "visibility not modelled for synthetic people".

### 7.3 Real lives: money and visibility (`Layers/LivesVisibility.cs`)

- **Who.** Public adults only, chosen by printed rules from the US people in people.json with ≥ 8 annual Forbes estimates. The rules are computed in `Tools/lives/build.py`, and the author approves the shortlist (recorded in sources.json):
  - **together:** the highest share of years in which log net worth and log page views moved the same way;
  - **apart:** the lowest share;
  - **little attention:** the richest Forbes-400 American not among the most-followed accounts, with the fewest page views. Nobody in people.json meets it (every candidate on the 2025 Forbes 400 has 10M followers or more), so the rule is printed without a pick;
  - **the author's pick** (approved 3 Nov 2026): Oprah Winfrey, a candidate no rule picked, shown with her own computed caption: "fortune held steady ($2.5B to $3.0B) while readership fell to 0.63 of 2016; together in 2 of 9 years".
  - The approved three: Elon Musk (together, 8 of 9), Donald Trump (apart, 2 of 9) and Oprah Winfrey (the author's pick).
- **Money.** A gold line in the magnified band: Forbes estimates by year, labelled "Forbes estimate", each value with a URL and quote.
- **Visibility.** A white line of monthly page views (from Jul 2015) that rises through the attention band, the cloud's region.
  - The **2025 follower snapshot** is drawn once as a separate star at the person's cloud altitude. It is joined only by a dotted leader labelled "followers, 2025 snapshot (four platforms): a different measure".
  - The line never continues into the star. Instagram-only list revisions are never used.
- **Money and visibility chart** (LivesPanel and strip): both series indexed **2016 = 1** on **one** log axis, never two axes. The caption is computed: "together in ⟨7⟩ of ⟨9⟩ years".

### 7.4 You

From the "Where do you stand?" panel (YouModel), the viewer is a dot at their income rank at "now". Three dotted futures fan into the ten-year zone: climb, stay, fall, from simulated people like them (same fifth and age band), with n and 90% ranges. Label: "your odds, from people like you; not a forecast". The existing `you` view stays as the deep dive.

### 7.5 LivesStrip (`UI/LivesStrip.cs`)

A uGUI mesh graphic on the HUD plane, with one row per life:
- tracks: income percentile (linear); net worth (log, real people); page views (log, real people); event ticks; the parents' dashed rank; the base-rate box;
- chooser buttons of at least 40 canvas units: Climber · Stayer · Faller · You · Together · Apart · Steady (the author's pick).

It is the **default rendering of M3 in portrait and in the headset**, and its canvas is kept in XR.

---

## 8. Connections: the wave (S06), the direction of time (S07), the year

### 8.1 S06: the overview stays the starting view

The overview stays the starting view and becomes the establishing shot *and* the hub (WP23).

**The establishing shot:**
- tier fills at .3-.45 in the tier stone ramp, with dark separators;
- tier names inside their layers, with value-added shares;
- the four tech ridges named (tier 4 is exempt from the 3.5% threshold): Internet platforms (social media, cloud, AI), Software, Chips, Media & telecom;
- GOVERNMENT on the bedrock's front face, *below* RAW MATERIALS, with "federal (OMB)" in its label;
- the crest tagged "⟨year⟩ · the year shown";
- the 1% pool at .5, labelled "CAPITAL · the top 1%";
- the cloud labelled as in §5.2, and hidden except in 2025;
- lifelines at .2 with the 5-8 largest living firms bright;
- the spray absent.

The emphasis values for these are set in WP14.

**Doors.** Three clickable and pinchable labels:
- "■ STOP TIME · one year as one structure (I)" in front of the crest;
- "◐ THE WORLD (G)" beyond the hills, where money already leaves for ABROAD;
- "⤴ LIVES (J)" at the road's present.

The bar's chapters are Over time · One year · The world · Money · People · One life, and the 2×2 appears in Help and on the first-run card.

**Visual rhyme.** M1 keeps the wave's names, order and colours:

| Wave | M1 |
| --- | --- |
| crest-face tiers | tier tower |
| bedrock | base |
| the road's cross-section at the crest | class column |
| the cloud | the cloud |
| ABROAD | the gate |

### 8.2 S07: settled as option (a), shipped last behind a flag

`EconomyLayouts.MirrorLand` is added in WP19, default off. WP38 implements the mirror:
- `LandFrame` reflects land-local x in `World`, `Local`, `Place` (localScale.x −1), `Right` and a new `InverseRotation`.
- `LandPick.cs:890` uses `frame.Local()` instead of `Quaternion.Inverse(frame.Rotation)`.
- Wave presets take yaw' = 360 − yaw.
- The money chart moves to the road's side, and the mobility ladder and the mind are re-posed, so their time still reads left to right.
- The "FUTURE →" arrow becomes a year rail at the crest: "1950 … ⟨year⟩ → not yet made".
- The universe bridge's pose is checked.

**Preconditions:** S04 has removed every world-space hill text (WP15-WP18), and the full smoke test plus both tours pass with the flag on. Then the flag defaults on.

**Fallback (b), if the mirrored run cannot be cleaned in bounded time:** keep depth as time, print "the past recedes behind the crest" and keep the year rail.

The stage views do not change either way (§4). With the mirror on, stop time can morph the crest into the towers (§9.2); with it off, it crossfades.

### 8.3 The year chip and the replay

| View | Year chip | Replay (R) |
| --- | --- | --- |
| Wave | as today | as today |
| M1 | the instant's year (§5.9) | a flip-book of instants |
| M2 | pinned at 2024 ("world data 2024 · events to ⟨as-of⟩"), plus the 2025 overlay | the history wall only |
| M3 | the window ends at the shown year | moves the window |

Pressing I during the wave's replay pauses it and opens that year's instant.

---

## 9. Camera presets and transitions

### 9.1 Presets

Poses are in stage coordinates. The yaw is always the land yaw + 180 (looking along −F) and never mirrored. Each variant's distance comes from `StageFrame.Fit` (lit bounding box + 3% into the free rectangle); the nominal 1080p values are listed.

| View · variant | Target (u, v) | Pitch | Distance | ≈ px/unit | Portrait |
| --- | --- | --- | --- | --- | --- |
| instant · structure, spending, state, abroad, industries, people | (70, 68) | 4° | ≈ 425 | 3.1 | towers and channel only (u −130…130, ≈ 3.8 px/unit); strip and 100¢ bar move to the bottom sheet; gate as an edge marker |
| instant · pay, all | (0, 120) | 4° | ≈ 605 | 2.15 | as left, with callouts |
| instant · balance | (−15, 50) | 4° | ≈ 470 | 2.8 | rows only; threads summarised in the sheet |
| instant · attention | (0, 150) | 4° | ≈ 330 | 4.0 | cloud plus the top floors |
| world (all layers) | (176, 40) | 8° | 435 | 3.0 | one half of the row (Americas & Europe, or Europe & Asia) with a HUD thumbnail of the whole row and two buttons |
| world · history | (176, −170) | 0° | 435 | 3.0 | wall from 1980 with a "1950" button |
| lives | (−250, 92) | 4° | 330 | 4.0 | LivesStrip is primary; the 3D frames the last 15 years |

### 9.2 Transitions (`StageTransitions`, `StopTime`, `ZoomOut`)

These move quads and stage roots only; no mesh is rebuilt mid-flight.

| From → to | Time | What moves |
| --- | --- | --- |
| overview → instant ("Stop time") | 2.0 s | `Rig.FlyTo` pulls back to the stage. Wave keys ease to absent over 0.6 s. M1 assembles bottom-up: base, floors rising in stacking order, labels. With MirrorLand on, four quads cut from the crest face fly into the tier floors, the bedrock face into the base, and the road's cross-section into the class column; with it off, a crossfade. |
| instant → overview ("Resume time") | 2.0 s | the reverse |
| instant ↔ world ("Zoom out / in") | 2.4 s | M1's root scales toward the US block (to its width) while the camera pulls back. M1 crossfades into the block between 40% and 80% of the move; the other blocks rise west → east; lanes draw in over the last 0.6 s. Caption for 4 s: "Zoomed out: every economy in the same terms (World Bank sectors, IMF state spending); BEA tiers and income groups are inside the US block (I)." |
| instant → lives ("Turn to lives") | 1.6 s | §7.1 |
| world → world · history | 1.2 s | pan down; blocks morph to PPP |
| any other | 1.6 s | fly |

In the headset, content moves and the viewer stays still; pans become 0.4 s fades.

---

## 10. Panels and interactions

- **InstantPanel** (right, 380 px; bottom sheet in portrait; kept in XR):
  - state buttons; step chips; the year;
  - **"The year in five lines":** five computed lines that answer §1's questions. Each line is a button that jumps to the step that shows it.
  - the household dollar's 100¢ bar; the who-buys-from-whom matrix;
  - "Follow a dollar ▸": one token from the 40-60 group's spending, through the fork, splitting into five coins that travel to pay, owners, upkeep, ABROAD and the base; it ends with the 100¢ bar lit;
  - sections: MEASURED · JUDGED BY JEV (AN AI MODEL) · MODELLED (≈) · THE DRAWING'S CHOICES · NO DATA.
  - **Hover or tap** any floor, row, ribbon, thread or bar for its value, unit, year, source and basis.
  - **Clicks:** a class floor shows its statement and lights its sub-bands; a tier floor shows its industries and the largest stored firms ("go deeper: Companies, Firm spending"); the gate opens the world.
- **WorldPanel:**
  - a chips row, always visible at bottom centre: Trade · 2025 · Conflict · Money & debt · Chokepoints · Foundations · Since 1950 · PPP · All flows;
  - a side card that opens when an economy is selected, and the camera reframes: GDP, people, the state's spending and revenue, military, trade with the US both ways with both records, tariffs, events with dates, and "what if: the US on this state";
  - the 13×14 trade matrix, exporter rows, with mirror gaps over 15% flagged;
  - the pending-verification list and the "as of" stamp.
  - Tapping a lane shows both records, the 2025 change and its sources.
- **LivesPanel:** start chooser (start year 1990/1995/2000/2005; fifth; age band), the three lives' events, base rates, the money-and-visibility chart, "Add me" (from You) and "Follow on the wave" (opens the ladder view on that person).
- **Hand menu:** a **Views** page (Wave · One year · World · Lives) and, per view, the state, step or layer buttons. Every control is a button at least 40 canvas units wide.

---

## 11. The guided tour: six acts, 21 stops

Each stop has:
- a ≤ 45-word card with `{placeholders}` from ModelFacts;
- a key line from the legend;
- a caveat footer (measured / judged / assumed);
- an anchor (Fixed, with its emphasis id range so it glows);
- `lit` emphasis, and optionally `pose` and `portraitPose` overrides.

Variants are reached through the existing `cues` ("show"). The old per-view stops leave the main tour, and those views stay one click away (bar, doors and "go deeper"). Voicing (ElevenLabs JohnV4) happens **only after the user approves** the credits. Until then, stops pace by reading time.

| # | Id | Act · view (variant) | Card (template) | Footer |
| --- | --- | --- | --- | --- |
| 1 | hills_bridge | I · section pose | From lives to an economy. The newest link in the chain is an economy. These are the lives of the United States since 1950, running into now. Behind them rises what those lives make, buy and own. | road: 1 line = 1M people at this zoom |
| 2 | hills_wave | I · overview | Each rib is one year's economy, from 1947 to {year}; the bright crest is the year shown. The wave widens as the country makes more: {growth} times as much in {year} as in 1947, after inflation. | width: square root of real output, so growth looks smaller than it is |
| 3 | hills_land | I · landscape | Read the crest's face from the bottom: the government's bedrock, raw materials, manufacturing and infrastructure, services, and tech on top. Each layer is as wide as its share of the year's output. | the order of the layers is an assumed order of dependence |
| 4 | instant_stop | II · instant (structure; lit base, towers) | Stop time. Pull {year} out of the wave and stand it up as one structure: households on the left, ranked by income; industries on the right, raw materials to tech; the state beneath both. | one year, no time axis · floor height = dollars a year |
| 5 | instant_industries | II · instant (structure; lit tiers, strip) | Services make the most: ${servicesVA}T of ${privateVA}T a year. Tech, on top, makes {techVA}% of the output with {techJobs}% of the jobs, yet holds at least {techMV}% of all US stock-market value. | market value: the 25 largest firms; ${unsplit}T not split by industry in the data (Sept 2026) |
| 6 | instant_households | II · instant (structure; lit classes) | Every household, ranked by income: the poorest fifth at the bottom, the top 1% at the top. Each floor is as tall as the group's income in a year; its face shows where that income comes from. | ≈ group splits from BEA's distribution of personal income ({dpiYear}), scaled to {year} |
| 7 | instant_spending | II · instant (spending) | Households spend ${pce}T a year at the industries. Every dollar runs in one of two lanes: rose, toward something wanted; ice, away from something feared. {fear}% of it moves away from a fear. | J which purchases are made from fear: judged item by item by Jev, an AI model (confidence {fearConf}); the total is measured |
| 8 | instant_dollar | II · instant (pay; lit fork, arch) | At the market each household dollar splits: {wages} cents become pay, running back to every floor; {owners} cents go to owners, mostly the top floors; {upkeep} replace what wears out; {imports} buy imports; {taxes} are taxes. | {year}, before income taxes · ≈ input-output ratios of 2024 |
| 9 | instant_state | II · instant (state) | All levels of government take in ${receipts}T a year and pay out ${outlays}T: benefits ${benefits}T, pay for {publicJobs} million employees ${publicPay}T, ${purchases}T bought from industries, ${interest}T of interest. They borrow the other ${deficit}T. | NIPA {year}, all levels of government |
| 10 | instant_between | II · instant (industries) | Industries pay each other ${io}T a year. Supplies come down and the money goes up: {up}% of it to a higher tier, {down}% to a lower one, {within}% within. Tech buys ${techBuys}T from the tiers below and sells them ${techSells}T. | BEA use table 2024 · arrows point where the money goes |
| 11 | instant_balance | II · instant (balance) | Now what has piled up. Ranked the same way, the top 1% hold {top1NW}% of all household wealth and the poorest fifth {bottomNW}%. Each gold thread is a trillion dollars of claims on US companies. | Fed DFA {dfaQuarter} · ≈ every household holds the market's mix |
| 12 | instant_attention | II · instant (attention) | Above the money float the most-followed Americans, higher the more followers they have; five of them also own or run giant companies. Companies pay platforms ${ads}B a year to reach people, who give about {minutes} minutes a day. | followers summed across four platforms, one 2025 snapshot · J firms matched by Jev |
| 13 | instant_all | II · instant (all; cues light each family 3 s) | Households spend; industries make, pay their workers and their owners; the state taxes, pays and borrows; attention steers the wants. Every dollar of {year} ran this circuit, and each was someone's choice. | — |
| 14 | world_floor | III · world (lanes at context) | Pull back: the United States is one block in a row of economies, west to east, each as wide as its share of the world's ${worldGDP}T of output. It makes {usShare}% of it with {usPop}% of the people. | IMF WEO Oct 2026, 2024, market exchange rates · order by where people live |
| 15 | world_trade | III · world (trade) | Each lane carries a year's goods and points where they go. The widest run into the United States: from Europe ${euUs}B, China ${cnUs}B, Mexico ${mxUs}B. It sells China far less: ${usCn}B. | goods, 2024, the exporter's record; the other record on hover |
| 16 | world_chokepoints | III · world (chokepoints) | Some links have one main holder. Taiwan made {twnChips}% of the most advanced chips; China mines {chnMine}% of rare earths and refines {chnMagnets}% of those in magnets; the United States holds {usAi}% of AI computing power. | SIA/BCG 2022; USGS, IEA 2024; Epoch end-2024 |
| 17 | world_conflict | III · world (conflict) | Tariff walls, sanction cuts and export controls narrow these lanes. US imports from Russia fell from ${ruUs2021}B in 2021 to ${ruUs2024}B in 2024; {tariffSentence} | drawn only after each 2026 fact is re-verified · as of {asOf} |
| 18 | lives_three | IV · lives | Turn the household tower on its side: behind it run the lives that fill it. Three synthetic Americans were in the middle fifth in {start}, aged 30 to 39. One climbed, one stayed, one fell; {climbed}% of those like them climbed. | simulated, n = {n} records of 100,000 people · associations, not causes |
| 19 | lives_visible | IV · lives (real) | Real public figures, by published estimates. {togetherName}'s fortune and readership moved together in {togetherYears} of {years} years; {apartName}'s moved apart; {pickName}'s {pickCaption}. | Forbes estimates; Wikipedia page views; chosen by the printed rule |
| 20 | hills_you | V · you (wait) | Tell the model about your household. It places you among simulated Americans by income, splits your dollar the way everyone's splits, and shows where people like you were ten years later. Take your time; the tour waits. | not advice: simulated odds with n and ranges |
| 21 | hills_return | VI · instant → overview (resume time) | Put the year back into the wave. Every rib behind it is a year like this one, and every dollar in each was someone's choice, made in fear or desire. | — |

`{tariffSentence}` is filled only from verified facts, for example "the average US tariff on Chinese goods was {usCnTariff}% on {usCnTariffDate}, against 3% in January 2018". Otherwise the clause is omitted.

**Checks:**
- `Tools/check-facts.py`: every number in a card or the narration traces to model-facts.json.
- `Tools/check-narration.py`: contradiction ≤ 0.15 per stop.
- Total length unvoiced: 6-8 minutes.

---

## 12. Data pipeline

All builders follow the regimes pattern:
- `python3 -I`, a project User-Agent, downloads cached in `.cache`, and `--offline` to rebuild byte-identically;
- every value stored as `{value, year, source, url, quote | field, note}`;
- the build log prints its checks.

New JSON under Assets needs a .meta file: let the editor create it, or write one with a random GUID while the editor is closed.

| Builder | Writes | Sources | Checks |
| --- | --- | --- | --- |
| WP01 copy | `Tools/global/research/{BRIEF.md, dataset.json, domains.json, verify-events.json, README.md}` | the mirror's research folder (it was lost once) | sha256 recorded |
| `Tools/instant/build.py` (WP02) | `Assets/Resources/Data/economy/instant.json` | DFA levels **by income group** and instrument (`dfa-income-levels-detail.csv`, same release as circuit.json; 1989-2026); BEA Distribution of Personal Income (latest; quintiles, deciles, top shares) and CBO household income (top 1%) for flows by group; BLS CE 2024 by income quintile (spending mix), with the top quintile split by assumption (≈); NIPA 2025 Tables 1.1.5 (exports, imports), 3.1 (receipts, expenditures, interest), 3.10.5 (public pay), 4.1 (income to and from abroad), 6.16D (corporate profits by industry, with a printed crosswalk to tiers, "assumed: mapping"); listed value by sector: S&P Dow Jones Indices sector weights (hand-read with page and quote) × Wilshire 5000 $75.3T, GICS → tier crosswalk printed ("assumed"); federal debt held by the public by holder (Treasury MSPD / Debt to the Penny API, Fed H.4.1, TIC Table 5, re-verified); hyperscaler capital spending reconciled into one calendar-2025 series with its definition (finance leases in or out), from corporate-programs.json | groups sum to DFA totals within 0.5%; net-worth shares within 0.1 pt of groupNotes; household liabilities (all DFA instruments) ≈ $20.0T ± 3%; NIPA receipts − expenditures = net lending ± $1B; flows by group sum to NIPA totals; sector weights sum to 100 ± 0.5 |
| `Tools/global/build.py` + `verify.py` (WP04) | `Assets/Resources/Data/economy/global.json` | brief recipes R1-R9 (IMF WEO/DataMapper incl. revenue `GGR_G01_GDP_PT`, WDI, OECD, Eurostat, SIPRI, Taiwan DGBAS/MOF, IMF IMTS, Census, Bank of Russia, EIA, GIIGNL, USGS, IEA, SIA/BCG, Epoch, TIC, IMF IIP, BEA, PIIE, Federal Register, OHCHR, UNCTAD, Euroclear); annual bilateral series for the conflict pairs (Census, Eurostat 2015-2025); Maddison 2023 (1950-1979) + WEO PPPSH (1980-2024); WEO NGDPD history; Natural Earth populated places (centroid longitudes); V-Dem v16 via OWID (EDI and Regimes of the World, 2025) | the brief's checks (partners + ROW = totals; DEU never summed with EU27; mirror gaps over 15% flagged; one GDP source per economy; shares ≤ 100; EU27 military = sum of members; net IIP in national currency); history sums to 100 per year; the 2026 gate (§6.8) |
| `Tools/lives/build.py` (WP03) | `Assets/Resources/Data/social/visibility.json` | Wikimedia REST page views (en.wikipedia, user agent, monthly, 2015-07 → 2026-10; no key); Forbes 400 / World's Billionaires estimates by year (hand-read into sources.json with URL and quote; Forbes's revisions kept); people.json snapshot | the selection rules computed and printed; shortlist approval recorded; public adults only |
| Existing, unchanged | circuit.json, industries.json, spending.json, purchase-judgments.json (Jev), corporate-programs.json, people.json, groups.json, mobility-benchmarks.json, regimes.json, civilizations.json | — | — |
| Model facts (WP06, WP36) | `Tools/model-facts.json`, `Tools/narration-facts.json` | the player's `-smokefacts` log | every card or narration number traced |

**Jev**, only for judgments, always labelled as judgments:
1. the existing item judgments behind the spending split (J + confidence);
2. the existing capital-holder ↔ firm matches (J);
3. the 2026 gate's support score, stored as `supportedJ` and never drawn;
4. the narration check and the caption audit (tooling).

There are no new Jev marks. The caller is `Tools/jev.py`. A builder checks that the key exists without printing it; if it is unavailable, it reports that live Jev calls are unavailable, the gate fails closed, and audits are skipped and logged.

---

## 13. On screen: measured, judged, modelled, assumed

| View | Measured (solid) | Judged by Jev (outline + J) | Modelled ≈ (dotted) | Assumed (chip) | No data (dashed) |
| --- | --- | --- | --- | --- | --- |
| M1 income | BEA 2025 accounts, value added, jobs, the 2024 use table, PCE, NIPA government, imports and exports, filings, IAB | the want/fear split; capital holders' firms | group splits of every flow (DPI/CBO/CE, carried to the year); role counts (simulation); 2024 input-output ratios in other years; upkeep/owners split (estimated depreciation) | tier order; the profits crosswalk | profits until 6.16D lands; class splits before the first DPI year |
| M1 balance | DFA by income group; Wilshire total; 25 firms' values; TIC; BEA net IIP | — | pension equity part; thread allocation | every household holds the market's mix | value not split by industry ($37.3T) |
| M2 | IMF, WDI, SIPRI, trade (one record per lane), PIIE, TIC, IIP, chokepoint shares, history; 2026 items only once verified | — | ROW as a residual | west-to-east order rule; one record per lane | Russia's own records; non-US services; market-rate history before 1980 |
| M3 | Forbes estimates (labelled), page views, the follower snapshot | — | synthetic lives, base rates, your odds | the selection rules | visibility for synthetic people |

Each panel repeats these lists in words. Each legend carries the provenance line.

---

## 14. Desktop, portrait, headset

- **Desktop 1080p.**
  - Labels 12-18 px, collision-free, never under panels.
  - Side panels are registered with HudBlocks, and cameras fit into the free rectangle.
  - M1 structure: raw, tech, top-1% and bottom-fifth floors ≥ 20 px. M2 at ≥ 3 px per unit.
- **Portrait 1080×1920** (player `-screen-width 1080 -screen-height 1920`):
  - M1 frames the towers only, with the strip and 100¢ bar in the bottom sheet and outside labels collapsed into numbered callouts.
  - M2 frames halves, with a thumbnail of the row.
  - M3 shows the LivesStrip.
  - The tour uses `portraitPose`.
- **Quest (hands only).**
  - The three views are dioramas (`CameraRig.XrScale`).
  - Labels ≥ 1° (Titles 1.2°); panels and the LivesStrip on the HUD plane as kept canvases; every control is a pinch button ≥ 40 canvas units.
  - Views are reachable from the hand menu.
  - Draw-call caps: M1 ≤ 12, M2 ≤ 20, M3 ≤ 6 (meshes merged per mark).
  - Transitions move content, not the viewer.
  - `-smokereplay` with M1 on screen: no frame over 50 ms.

**Budgets.** M1 under 30k vertices; M2 under 40k; M3 under 20k. Models are built on a worker and cached by year; layers draw in one HillRebuilds slot, and only while their view is on screen or easing out.

---

## 15. Verification

- **Per package:** compile check, then the Linux player's smoke test in the mirror (`~/Desktop/Unity/.why-2-build`), with `-smokeonly <views>` and `-screenshots`.
  - Captures are compared with ffmpeg `blend=difference` and `signalstats`; hue coverage uses `geq` masks (there is no PIL or numpy on the machine).
  - Metrics are read from the log: labels, emphasis, palette, crossings, balances, projected floor heights, budgets.
  - One Unity process at a time; screenshots go in `.why-2-build/Shots` and are deleted after viewing.
- **Capture names:**
  - `economy-view-instant[-<variant>].png`
  - `economy-view-world[-<layer>].png`
  - `economy-view-lives[-real|-you].png`
  - `economy-tour-NN-<id>.png`
  - portrait and `-xr` runs of the same.
- **Release gate per view (WP39):** a five-person first-look test on still captures. At least 4 of 5 must answer each of §1's questions within 30 s without narration. The Jev caption audit is supporting evidence only. A failure returns to the owning package.

## 16. Work order (detail in the work packages)

| Group | Work |
| --- | --- |
| G1 | data builders and the research copy (now) |
| G2 | world data with the 2026 gate; docs corrections; smoke-check and facts harness (after the quick fixes merge) |
| G3 | S01a emphasis, S02 palette and mark code, S04 labels, and the three pure models |
| G4 | S03 legend; S01b retune and tour overrides |
| G5 | four layer-migration packages and the stage with three empty views |
| G6 | M1 structure, M2 row, M3 corridor, S06 overview hub |
| G7 | M1 flows, summit and attention, balance and people; M2 trade, conflict, dependence, history; M3 real lives, strip and you; stop time |
| G8 | M1 and M2 panels |
| G9 | the tour; portrait and headset |
| G10 | S07 mirror |
| G11 | verification and documentation |

Packages in one group share no files and do not depend on each other.

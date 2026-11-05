# Map: how mobility and collective action are shown today (map:views)

Restored from the map agent's returned summary (its 497-line file was lost when the session's /tmp scratchpad was wiped).
Read the code it names for detail. Frames: `/home/john/Desktop/Unity/.why-2-build/Shots/base1/`.

## Mobility

- Only one mobility picture is readable: the `mobility` view (tour 13). It is a Sankey chart of five income fifths,
  2015 to 2025, floating in black with no wave or people in frame (scored 3 of 5 for a first-time viewer). It counts
  2,250 adult lines (225M people), plus a like-for-like panel of 194M adults aged 25+, checked against the Treasury
  1996-2005 panel (bottom fifth stays 55%, top 61%, bottom to top 4%), PSID surveys and the Chetty study.
- On the hills it gets lost:
  - In `people` (tour 11), the per-cohort drift arrows sit among about 46 lit hill parts, and climbers' arrows point
    down the screen.
  - `ladder` (tour 18) is one pale line, and its default person, the striver #3505, is the population's most extreme
    climber (income rank 4% in 2018, 98% in 2022).
- M3 is computed but not drawn. Its start group is 78 records: 49% climbed (90% range 40-58), 32% stayed, 19% fell,
  from three typical lives. The typical climber lost a job in 2021 and again in 2025. The three approved real lives are
  Musk, Trump and Winfrey.

## Collective action

- No collective-action view is readable. `society` is about 1,000 pale tie lines. `betrayal` is the same picture
  (28 dB PSNR between the two frames); only the panel text differs.
- Season results that are computed but never drawn:
  - 4 coalitions of 45, 39, 32 and 12 cohorts, which follow industry (NMI 0.56), not party (0.01);
  - standing and signals;
  - the betrayal's reach: 5 players hit, calm after 8 rounds;
  - the forgiveness what-ifs: 0.426 if nobody forgives, 0.868 if everyone does, against 0.733.
- `dilemma` (tours 16-17) uses six synthetic stick figures, with the story in captions. It has three defects:
  - the caption "Tit for tat did not retaliate" is shown in round 9, before the reply is played (HillDilemmaLayer.cs ~199);
  - both tribe rings draw the same number (~228);
  - an independent is drawn as a Democrat.

## Reusable

- Pure, deterministic models: Mobility, LivesModel, PersonLife, YouModel, MindState/MindDiagram, PrisonersDilemma,
  DilemmaArena, SocialSeason (111 ms a season) and Coalitions.
- Real-world data already in the repository, unused by these views:
  - the PIIE US-China tariff series: 91 dated moves with named retaliations, a peak of 134% (US on China) and 148%
    (China on US) in April 2025, and the May 2025 truce;
  - 68 gated world events in global.json, including the Russia sanctions coalition, the oil price cap, OPEC's 2020
    breakdown and the UAE leaving OPEC;
  - in games.json: Axelrod's tournaments, lab meta-analyses and GSS trust (47% in 1972 to 25% in 2024);
  - unshipped mobility research in Tools/history/sources.json: Charlotte 4.4% vs San Jose 12.9% bottom to top,
    Denmark 11.7%, Canada 13.4%, absolute mobility 92% for the 1940 cohort vs 50% for 1984.

## Images

- None of these views has an image. Only people.json (1,003 accounts) and visibility.json carry Wikidata ids; firms,
  states and the US actors in government.json do not.

## Plan conflict

- The planned 21-stop tour has no collective-action stop, and it specifies M3 as line paths, so the new direction
  amends the plan, not just the views.

## Open questions the map raised

- Should the holistic scenes live on the planned stage (WP19, not built) or replace the mobility, society, betrayal,
  dilemma and ladder views in place? The plan's tour (decision 24, spec section 11) and M3 (decision 23) change either way.
- Is the PIIE US-China tariff series acceptable as the anchor real iterated game, given that it measures statutory
  rates on fixed 2017 trade rather than duties paid, and that its 2026 rows depend on the gate?
- May SHED's 2025 survey value (fielded October 2025, published May 2026) be drawn without the 2026 gate?
- Do the approved real lives (Musk, Trump, Winfrey), billionaires throughout the window, belong in a mobility scene, or
  only in M3's money-and-visibility comparison?
- Should the three dilemma defects be fixed in the existing view now, or only avoided in the new scene?

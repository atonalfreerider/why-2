# The present as a contest: the competitive-advantage design

Written 7 Nov 2026 for the user's request of the same day (verbatim in `Docs/research/advantage/brief.md`). The causality
timeline stays as it is. Key I still opens the 2026 cross-section into a 3D hillside, but the hillside is rebuilt
around competitive advantage. The industry terraces of the previous present stay reachable from the view bar.

## 1. The idea

The biosphere chapter already shows a trophic pyramid. Prey turn energy into calories, and predators at the top live on
that expenditure. The 2026 economy is drawn the same way:

- **Money climbs to advantage.** Buyers spend at the foot of the range. Rivers of money run back through the supply
  chain, and at each step part of the river stays with the actor that holds the advantage.
- **Altitude is dominance.** An actor stands as high as its competitive advantage. Altitude is judged from sourced
  evidence with Helmer's 7 Powers, and its gold summit shows the profit it keeps above normal.
- **Choke points are where rivers must converge.** Many rivers enter one actor that has no substitute (CUDA, CoWoS,
  EUV), and a thinner river leaves it.
- **Flashpoints are the contests.** A spark between two actors marks where an advantage is deciding who wins, coloured
  by the user's triad: information, execution, environment.
- **Scenarios change the environment.** Friendly or unfriendly conditions move every actor up or down. The view then
  predicts who rises and who falls, and says why.
- **The recursion is visible.** One bright thread runs back along the wave from NVIDIA's summit to the transistor of
  1947. History's flashpoints stand on the wave at their years.

Actors, companies and the people in them want to win and fear extinction. The rivers carry that motive: rose where
spending is driven by desire, ice blue where it is driven by fear. These are the economy's existing colours, kept in
two lanes and never blended.

## 2. What a first-time viewer should understand in ten seconds

1. Who dominates in 2026? The highest summits.
2. Where does the money go? The widest rivers, and the summits they climb to.
3. Who depends on whom? The order of the bands, from energy and tools at the back to buyers at the front.
4. Where is the fight? The sparks.
5. What if the world changes? The scenario buttons move the landscape.

## 3. Layout

All positions are land-local, in the frame of the present (`LandFrame`: +z runs along time, +x across, +y up). The
arena stands past the crest, where the previous present stood: z from CrestZ + 50 to CrestZ + 330, x from about 40 to
540.

- **Depth: the supply chain.** Bands run from the crest outward:
  - *Energy* (power, grid) lies nearest the crest, the past;
  - then *Tools & IP* (ASML, EDA, Arm), *Fabs & memory* (TSMC, Samsung, Intel, SK Hynix, Micron), *Chips & systems*
    (NVIDIA, AMD, Broadcom, Huawei, servers), *Clouds* (Microsoft, Amazon, Google, Oracle, CoreWeave, Meta, xAI),
    *Models* (OpenAI, Anthropic, DeepSeek and the open-weight labs), *Apps & platforms*;
  - *Buyers* (consumers, enterprises, governments) lie nearest the camera, in the future.

  So the foundations of the AI economy face the wave they came from, and the frontier faces the future.
- **Across: rivals side by side.** In each band the largest actor stands at the centre and the rest alternate outward
  by size. A rival is never above or below another in the same band except by altitude.
- **Flanks.**
  - *Left:* capital (SoftBank, MGX, Berkshire, JPMorgan, BlackRock, venture capital, public markets). Its rivers cross
    to the labs and clouds.
  - *Right:* the rest of the economy, by sector (health, retail, payments, autos, energy majors, media), each sector
    a cluster in the band nearest its role.
- **Under everything: the state.** A bedrock slab bears the US government's name and its roles: the IP system, export
  controls, the rule of law. Small tiles carry the other states (China, Taiwan, the EU, the Netherlands, South Korea,
  Japan, the Gulf), under the actors that stand on them.

## 4. Encodings

| Mark | Means | Source |
| --- | --- | --- |
| A peak's footprint | market cap (valuation for private firms): area proportional to value, radius = k · sqrt(USD bn) | a verified fact (2026 gate) |
| A peak's altitude | competitive advantage: the strongest power plus half the second plus a quarter of the third (Jev scores 0-4, so 0-7) | Jev judgments over the verified facts |
| Bands of colour up the peak | which powers hold it up: one stratum per power, as thick as its share of the altitude | Jev |
| The gold summit | operating margin above normal profit (10%): no gold at or below normal; brighter and wider with the excess | a verified fact |
| A ring at the foot | a choke point; brighter the less substitutable its supply | the research's choke points; Jev's substitutability |
| A river | money a year from payer to payee, width proportional to the amount, pulses running toward the payee | a verified fact per river |
| A river's lanes | desire (rose) and fear (ice) shares of the payer's motive | Jev, from the payer's own words on record |
| A spark between peaks | a flashpoint: holder and rival, at the rival's height; cyan for information, amber for execution, green for environment, mixed by Jev's triad scores | the research's flashpoints; Jev |
| A ghost outline above or below a summit | the 2030 projection (most likely course) or a scenario's effect | Jev durability / scenario scores, composed in code |

Labels say which marks are measured and which are judged. Judged marks are italic with "J", and the legend says the
altitude is a judgment made by an AI model from sourced facts.

## 5. The model: exact, in code

Every composition below is done in `Tools/advantage/build.py`, which writes `Assets/Resources/Data/economy/advantage.json`.
The app moves things but does no inference.

- **Advantage.** Sort the seven Jev power scores, so s1 ≥ s2 ≥ s3. Then A = s1 + s2/2 + s3/4, from 0 to 7. Helmer's
  rule is that one power is enough, and a second and third reinforce it.
- **Scenario effects.** Jev answers "how would this actor's position change" on a 0-4 Score. The step is
  e = score - 2, from -2 to +2. Then A_s = clamp(A + 0.9 · e, 0, 7). The strategist sets each scenario's river
  multipliers, for example Taiwan: flows into TSMC × 0.2. The multipliers are data, with reasons in the analysis.
- **The 2030 projection.** Jev's durability Score gives d from -2 to +2. The 2030 altitude is A + 0.9 · d.
- **Motive.** The fear share of a flow is Jev's motive score divided by 4.
- **Choke strength.** Strength = 1 - substitutable / 4.
- **Truth.**
  - A number on screen comes only from a fact the gate verified, and 2026 numbers come through the 2026 gate.
  - A judged value is drawn as judged.
  - An actor without a verified size is drawn as an outline at a minimum size and labelled "size unverified".

## 6. Interaction

- **Key I, or the bar's "Advantage" button.**
  1. The crest's cut face glows.
  2. A floor slides out of it and spreads into the arena.
  3. Peaks rise band by band, from energy to buyers.
  4. The rivers start to run, and the sparks light. The camera rides out to the catalog pose.

  I again replays the choreography. Shift + I returns to the overview.
- **Hover** over a summit shows a card with the actor's name, role, market cap, margin, primary power, triad, desire
  and fear, each number with its source.
- **Click** selects an actor. Its rivers and sparks stay lit and the rest dims, and the panel shows the actor in full.
- **The panel**, on the right, has the scenario buttons:
  - *Now (2026)* and *2030: most likely*;
  - then the strategist's scenarios: compounding demand, capex digestion, the power wall, a Taiwan disruption, models
    commoditised, a regulatory clampdown, China decoupling.

  Choosing one moves the peaks and swells or dries the rivers over two seconds. The panel then says what was assumed,
  who wins, who loses and why.
- **History.** Flashpoints from 1947 to 2025 stand on the wave at their years, between the two companies' lifelines
  where both have one. The archetypes are listed in the panel's "Patterns" section with their mechanism and the
  balance that restored itself: the Mongols, Alexander, Napoleon, the two world wars, and the biosphere's pyramid and
  extinctions. They lie outside the economy's time axis.

## 7. Objects first

The peaks carry the argument. By default only these lines are drawn:

- the 12 largest rivers;
- the choke points' converging rivers;
- the 6 present flashpoints with the most evidence.

Selection lights the rest of an actor's lines. Labels name about 14 actors: the largest by market cap, every choke
point, and a scenario's winners and losers. The others show on hover.

## 8. Data: `advantage.json`

The runtime file, built from the research by `Tools/advantage/build.py`:

- `bands` and `flanks`, as names and order.
- `actors`. Each one has:
  - identity: id, name, short name, kind, country, band, sector and lifeline id;
  - size: `{usdBn, label, fact}`;
  - margin: `{pct, fact}`;
  - judged values: `powers{7}`, `primary`, `triad{3}`, advantage A, a2030 and `scenario{id: A_s}`;
  - text: role, advantage, desire, fear and a few source lines.
- `flows`: from, to, usdBn, what, kind, basis, fear share, fact, and per-scenario multipliers.
- `chokepoints`: actor, controls, strength.
- `flashpoints`: holder, rivals, year, era, title, advantage, mechanism, outcome, triad{3} and power.
- `lineage`: the thread's steps.
- `scenarios`: id, title, friendly, premise, winners, losers, and a reading.
- `sources`: fact id to `{claim, url}` for the cards.

## 9. Build plan

| Step | What | Acceptance (smoke `-smokeextra advantage`) |
| --- | --- | --- |
| A1 | The research gated, merged, judged and built into `advantage.json` | Every size and margin points to a verified fact. Every present actor has its judgments. |
| A2 | `HillAdvantageLayer`: the floor, bands, bedrock and peaks with strata and gold; labels; the view `advantage` on key I with its choreography | Actors do not overlap within a band. Renderers stay at 8 or fewer. 0 errors. |
| A3 | Rivers with motive lanes and pulses; choke rings | Each river's ends are at actors. |
| A4 | Sparks; the panel with scenarios and actor detail; hover and click | A scenario changes at least five altitudes. Frames stay under 50 ms. |
| A5 | History on the wave and the lineage thread; the patterns list | Each flashpoint before 2026 is placed at its year. |
| A6 | The tour stop text, unvoiced, and the handoff | |

## 10. Open questions for the user

1. **People.** The research names people where they are the mechanism: Shockley, the eight who left him, Noyce,
   Moore, Grove, Huang, Altman, Sutskever, Amodei. The app shows them in text only. Approval would be needed to show
   any of them as an image.
2. **Altitude as a judgment.** It reads advantage, not price. A firm can be large and low, as Walmart is, or small
   and high, as ASML is. A strategist reads it that way, but it is not a market statistic. The legend says so.
3. **Which scenarios matter most to the user?** The strategist's seven are a start.

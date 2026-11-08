# The present as a contest: the corporate biosphere

Rewritten 7 Nov 2026, from the user's second direction of the same day (verbatim in
`Docs/research/advantage/brief-v2.md`). The first version, cones on a flat arena, is replaced. Key I still opens the
2026 crest of the causality timeline, but into a three-dimensional, force-directed network of companies. Each company
is broken into its balance sheet and still reads as one whole. They hover over a fitness landscape that their own
positions raise. The history camera, the lineage's gold lane and the flashpoints carry over from the first version.

## 0. The fifth direction: the hill cut open (7 Nov 2026)

The user's fifth direction is verbatim in `Docs/research/advantage/brief-v3.md`. It replaces sections 3, 6 and 7 below:
altitude is no longer market position.

- **Strata.** The supply chain stands in strata under the hill's 2026 crest (`ContestNetwork.StratumOrder`):
  1. consumer products and platforms, on top;
  2. AI models;
  3. cloud and data centres;
  4. chips, memory and networking;
  5. fabs and packaging;
  6. equipment, EDA and IP;
  7. energy and power;
  8. raw materials;
  9. government, the floor.

  Each stratum is a wide, shallow band (`StratumY`, from y −101 to −742). Its gap from the next leaves room for both
  strata's largest bodies. The hill stays on top: its people, the top 1% and the attention cloud above it stay
  visible (the view's emphasis row). The owners' golden circles float above them.
- **Home stratum.** Each company stands where most of its revenue is earned (`build.py`, `HOME_STRATUM`, stated rules).
- **Integration.**
  - *Vertical.* A company's presence in another stratum (research cluster `integration`) is a satellite in its
    column, at the same x and z, joined by a dotted vein. Alphabet's column runs from consumer down to chips.
  - *Horizontal.* A company's families of products in one stratum are named in the panel.
- **Advantage boundaries.** Research cluster `moats` gives each dominant company's sources of advantage, with
  evidence:
  - technology;
  - supply;
  - patents;
  - share price and cash;
  - talent;
  - scale;
  - distribution;
  - regulation.

  The boundary is a ring on the holder's stratum, as wide as its shell and its share of the market. Rivals are held
  outside it. It is walled by one arc per source, each as tall as Jev judges that source keeps rivals and entrants
  out (`judge.py`, section `moats`, 0-4), in the source's colour and named with its evidence. NVIDIA's walls are
  always labelled; a stratum's close-up labels all of its own.
- **The wall against entrants.** Where Jev judges talent and the share price to be about half or more of what keeps
  entrants out (`judge.py`, section `entry`), a rose wall rings the stratum's incumbents. It is as tall as Jev's
  entry difficulty. The entrants stand outside it (research cluster `entry`), each with its fate. The taken ones are
  joined by a dotted line to whoever took them.
- **Bodies.**
  - *Points.* Point clouds with a boundary outline, no wireframe: the surface's points and a skin beneath them,
    coloured by the parts at each place (the three heat maps kept).
  - *Outlines.* They face the camera: the body's silhouette, the market value as a dotted circle in the P/E's heat,
    and what the earnings justify as a faint dotted circle.
  - *Occlusion.* Each body's hidden solid (depth only, drawn after the labels and before every mark) hides what
    stands behind it. Labels opt in to being drawn over depth (`LabelSpec.OnTop`).
- **Camera.**
  - *Overview.* The whole stack under the hill is the view's own pose (fitted, `Frame`).
  - *Close-ups.* The panel's strata buttons fly to one stratum at a time, a cutaway: the strata above vanish and
    those below fade, so the camera looks down onto its territories. "The hill" frames the hill, its people, the
    cloud and the consumer stratum.
- **Explanation.** The panel reads NVIDIA's walls, tallest first, with their evidence and Jev's heights, and how
  entry is closed in each stratum: where talent and the share price are most of the wall, the evidence and the
  entrants' fates; elsewhere, the tallest wall.

## 1. The idea in one paragraph

Evolutionary biology draws a population on a **fitness landscape**: peaks where an organism thrives, valleys where it
dies out. The 2026 economy is drawn the same way. Each company is an **organism** made of its balance sheet:

- what it owns, above its core, and what it owes, below;
- a translucent **shell** as large as its market value;
- the gap between the shell and the solid book value inside it, which is the **speculative premium**, inflated by its
  price-to-earnings ratio. That premium is a weapon: it pays for capital spending, acquisitions and losses that buy a
  market.

Companies are joined by the **dependencies** that feed them: revenue, compute, capital and contracted commitments.
**Rivers of capital** run along those links and between companies and people. The **owners float above** as golden
circles. The **workers and consumers stand on the low ground** beneath. Each company hovers at an **altitude**, its
market position, and the landscape under the network rises into high ground beneath the dominant and falls into low
ground beneath the dependent. A **Darwinian simulation** runs the population from 2026 to 2030 under each scenario:

- shells inflate or burst;
- cash runs down and debt weighs;
- each company holds its ground, is absorbed, or goes extinct.

## 2. What a first-time viewer should understand in ten seconds

1. A few giants hold the high ground, and their shells are mostly air: speculative value above their books.
2. Everything depends on a few undergirders. The rivers converge on NVIDIA, and from it on TSMC and ASML.
3. The owners sit above them all: index funds, sovereign funds, founders, private equity, the richest 1%.
4. Someone is betting the company. Plumes mark losses paid for by the share price; hollow spheres mark promises not yet
   paid.
5. The future is a contest. Press play, or choose a world, and watch who survives.

## 3. The frame

Land-local, past the crest of 2026: z from CrestZ + 60 to about CrestZ + 420, centred on x = 290. There is no floor.
Altitude runs from 0, the low ground, to 150, the high ground:

| Stratum | Altitude | What stands there |
| --- | --- | --- |
| The ownership class | 208-240 | Golden circles: households by wealth (the top 1%, next 9%, next 40%, bottom 50%) and named founders, above the institutions (index managers, sovereign funds, private equity, venture capital, hedge funds, corporate holders) |
| Companies | 10-150 | Their market position: high ground for the dominant |
| Workers and consumers | -10 to 0 | The low ground: wages flow down to it, spending flows up from it |
| The state | -25 | The bedrock under everything: rule of law, property and IP, export controls |

A thin altitude ruler at the left edge names the ground: "high ground: dominant", "low ground: dependent".

## 4. The organism: one organic body made of a balance sheet

The user's third direction (7 Nov 2026), verbatim:

> the geometric shapes are completely irrelevant to the concepts they are representing. there needs to be a coherence
> and organic shape to the groups that represent a company. point cloud meshes with variable wireframe opacity and
> muted color punctuated by intense bloom for attention to important advantage frontiers. do a redesign

So a company is not a set of solids but **one body** (`Land/Organism.cs`):

- **Lobes, not parts.** Each part of the balance sheet is a lobe of a single implicit surface: a Gaussian as wide as
  k · ∛($bn) of the part, so volume tracks dollars. The lobes overlap and fuse.
  - Equity is the core.
  - Cash, what it built (property and equipment), what it bought (goodwill and intangibles) and its stakes swell above.
  - Debt and leases sag below.
  - A company without a researched balance sheet is one faint lobe a twelfth of its value, labelled as such.
- **Points and wire.** The surface is sampled along a geodesic sphere: 162 vertices, or 642 for the giants. Each vertex
  is a point, and each geodesic edge is a wire, so the points and the mesh share one topology. A seeded noise roughens
  the surface so no two bodies are alike.
- **Colour by region, with three heat maps.** A point takes the colour of the lobes that make the surface there, so the
  balance sheet reads as regions of one whole. The user's fourth direction (7 Nov 2026): "they should still have color
  coded heatmaps for the core equity value, debt, and speculation asset swell". So three regions are heat maps, each in
  its own hue family and kept under the bloom threshold:
  - the equity core, by book value on a log scale ($10bn navy to $3tn white; negative equity violet);
  - debt, by leverage, debt over equity (0.05 dark red to 5 and beyond yellow-hot);
  - the speculative dust and the market-value membrane, by P/E (8 violet to 150 pink-white; a loss carried by its value
    hottest).

  Cash (sage), what it built (slate), what it bought (mauve), stakes (ochre) and leases (rust) keep muted tones.
- **Variable wireframe opacity.** A wire is as opaque as the parts there are tangible. What it built and what it owes
  read solid. Cash and leases read firm. Equity sits between. What it bought and its stakes read ghostly.
- **Speculation as dust.** A dust of points fills the space between what earnings justify and the market value. What
  earnings justify is net income at a reference P/E of 15 (assumed), drawn as a faint dotted inner membrane. The dust
  holds as many points as the cube root of the premium allows. The market value itself is a dotted membrane, tinted by
  the P/E. So the P/E the user named is visible as size: a high P/E is a body swimming in dust.
- **Promises** (purchase and compute commitments) are a ghost lobe of points beneath the body, on a dotted stalk.
- **Burn.** A loss funded by others' money is a comet of points, its head hot, trailing from the body toward the
  market it buys.
- **Intense bloom only at the advantage frontiers.** A body glows only on the face that presses on others:
  - toward the dollar-weighted middle of those who must pay it at a choke point, as hard as the choke is hard to
    replace (Jev) times its judged advantage;
  - toward each of the two rivals it is beating in a present contest.

  Everything else stays muted. So the eye goes first to NVIDIA's face toward the clouds, ASML's toward the fabs, and
  TSMC's toward its customers.

Each body is its own mesh, built in its own frame and moved by its transform. The force layout moves the population
every frame without rebuilding; a body is rebuilt, four a frame, only when its world, year or frontiers change.

## 5. The network

- **Dependencies, as rivers.** Each verified flow of money a year is a tube between two cores, as wide as √($bn)
  allows, its packets moving toward the payee. Spending carries the payer's motive in two lanes: rose for desire, ice for
  fear, judged by Jev. Investments and capital raised run white-gold. Contracted commitments are dashed: promises.
- **Motion** (the seventh direction, `Docs/research/advantage/brief-v5.md`). Packets on the flow clock
  (`PacketMeshBuilder`, `FlowClock`) replace the pulses of the rivers and the threads, so nothing stands still at a frozen
  year, a paused play or a settling world. Every living river carries them from payer to payee, the story's hops lit
  and the rest faint but moving: dots of money in the lane's rose or ice; gold ingots of capital and of the payouts to
  the wealth groups; and, before a contract's start year, the marching dashes of its promise over a ghost of its river.
  A one-off amount crosses as one larger packet every three seconds. Gold ingots rise up an owner's thread only from a
  company with buybacks or dividends on record. The dollars stay the width: a yearly flow's 0.5 to 2 packets a second follow
  its measured dollars gently, all at one speed, and its rate and seed are fixed by its flow, so no year, world or
  rebuild makes a pattern jump.
- **Ownership.** Gold threads run down from each owner's circle to the organisms it holds, as wide as the stake's
  value. Buybacks and dividends rise as gold ingots up those threads.
- **Talent.** One shared node, the people who build frontier AI, takes the companies' stock-based pay as rivers. The
  widest rivers show who is buying the talent.
- **People.**
  - Wages run down from the companies to the workers.
  - Spending runs up from consumers to the companies that sell to them.
  - Retirement savings run up from households to the index managers.
- **Contests.** Sparks between rival shells, as before, coloured by the triad: information, execution, environment.

## 6. High ground and low ground

A company's altitude is its market position:

    altitude = 150 · (0.55 · A / 7 + 0.45 · log10(V / 10) / log10(5000 / 10)), clamped to 10..150

- A is the judged advantage: Jev's seven powers, composed in code as before.
- V is the market value in $bn.

Both halves are the user's: durable advantage, and command of capital. A high share price is itself an advantage. A
world's judged advantage arrives over three years, as the simulation ramps it: none of the change in 2026, all of it from
2029. So 2026 is the same network in every world.

In a simulated year, each company that moved more than 4 units carries a **trail** from its 2026 ground: a ring where it
stood, and a line to its body, green where it rose and red where it fell. A faint stem drops from every body to the
landscape under it, so altitude reads against the ground rather than depth. The fallen keep their names in the 2026 top
22, with their fate and its year: "† extinct 2028" or "→ absorbed by Microsoft, 2028". Companies whose inputs would all
be assumed are not simulated: they stay at their verified 2026 values, marked "(2026)".

The **fitness landscape** is a surface under the network:

- its height at a point is the weighted mean of nearby companies' altitudes (a Gaussian kernel as wide as their
  shells);
- it is drawn only as glowing contour lines every 10 units, as a topographic map, never as a solid floor;
- it rises beneath the dominant and sinks between them, and it reshapes as the simulation moves them.

## 7. The force-directed layout

- **Who moves.** Companies, owners and the shared nodes are bodies. Each organism moves as one rigid body as wide as
  its shell; its parts are placed inside it by rule, not by force.
- **Forces,** iterated about 600 times at build and then live:
  - a strong spring holds each body at its target altitude;
  - springs pull along every dependency, stake and commitment, with rest length the two shells plus a gap;
  - every pair of organisms repels, with a hard collision at the shells;
  - members of the same supply-chain layer attract weakly, so rivals sit beside each other;
  - a weak centring pulls toward x = 290.
- **Owners** sit at their stratum's altitude, near the weighted centre of what they hold, and repel one another.
- **Determinism.** Seeded starting positions, so smoke frames repeat.
- **Live.** When the year or the world changes, targets move and the layout keeps iterating, a few steps a frame, so
  the population visibly rearranges.

## 8. The Darwinian simulation (`Tools/advantage/simulate.py`, precomputed)

For each scenario (now, the most likely course, and the strategist's seven) and each year from 2026 to 2030, every
company carries:

- its revenue, net income, cash, debt and book equity;
- its market value and P/E;
- its altitude;
- its status: alive, absorbed (and by whom) or extinct.

The rules are plain, in code, and documented in the file's header:

1. **Revenue** grows at a base rate from the verified recent growth, moved by Jev's scenario judgment, plus the
   scenario's shocks.
2. **Net income** is revenue times the margin, with the scenario's margin shocks.
3. **Market value** is earnings times the P/E, times the scenario's multiple factor for the year. Loss-makers are valued
   on revenue times a multiple.
4. **Cash** changes by net income, minus capital spending and commitments due, plus capital raised.
5. **Raising capital.** Equity can be raised while the market is open and the shell is large: that is the share price
   as a weapon. Debt can be raised while leverage allows.
6. **Extinction and absorption.** A company whose cash runs out with no capital to raise dies, unless a partner it
   depends on, or that holds a stake in it, can afford it. Then it is absorbed.

Every assumption is labelled as assumed, and the panel says: a simulation, not a forecast.

## 9. Interaction

- **Key I** plays the opening:
  1. The crest's cut face glows.
  2. The organisms condense out of it and rise to their ground.
  3. The landscape draws in under them.
  4. The rivers start.
  5. The owners' circles light above.
- **The panel:**
  - the worlds (now, the most likely course, and the strategist's scenarios): choosing one plays it from the measured
    2026, a year every 2.6 s, so the contest is seen to unfold;
  - the years from 2026 to 2030, with play (from now it steps onto the most likely course);
  - the selected organism's balance sheet as a bar of assets against liabilities and equity, beside its market value;
    with its P/E, owners, dependencies, burn, strategy and risk;
  - the history button.
- **Hover** shows a company's card. **Click** lights its rivers, threads and contests and dims the rest.
- **Truth on screen.** Every 2026 number is a verified fact shown with its period (a quarter's buybacks say so; a burn
  says its period and what it comes to a year at that rate; a river is "/yr" only when it is yearly, and capital raised is
  a dated round, not a river a year). Simulated numbers carry ≈, with the assumed inputs listed for the company and all
  of the simulation's assumptions in the method.
- **The owners' circles** face the camera, filled with dots, as wide as √ of what they hold, and turn with it as it
  orbits. The largest owner of each kind is always named.

## 10. Objects first

- **What carries the argument.** The organisms, the circles and the landscape.
- **Lines drawn by default:**
  - the 14 largest rivers;
  - the owners' threads to the 12 largest companies;
  - the commitments over $100bn;
  - the present contests.
- **Selection** lights the rest. The landscape's contours are context: faint, never brighter than a river.

## 11. Data: `advantage.json`, version 2

`build.py` adds to the first version:

- per company: `balance{totalAssets, cash, ppe, goodwill, intangibles, investments, totalLiabilities, debt, leases,
  equity, commitments, date, facts}`, `pe`, `buybacks`, `dividends`, `sbc`, `burn`, `strategy` and `risk` (Jev);
- `owners`, with kind, holdings and stakes;
- the shared nodes: talent, workers and consumers, and households by wealth;
- `simulation{scenario: {year: {company: {v, eq, cash, debt, rev, ni, pe, alt, status, by}}}}`.

## 12. Build plan

| Step | What | Acceptance (`-smokeextra advantage`) |
| --- | --- | --- |
| B1 | Research round two: balance sheets, owners, weapons, build-out | Every drawn number points to a verified fact |
| B2 | `simulate.py`, and `build.py` version 2 | Deterministic. Every scenario changes at least five fates or altitudes. Assumptions listed |
| B3 | `ContestNetwork`: force layout, organism placement, the landscape and its contours | No two shells interpenetrate. Altitude error under 2 units. Converges in 600 steps |
| B4 | The layer: organisms, rivers, threads, circles, people, landscape, sparks, labels, opening, hover and click | Renderers within budget. Frames under 50 ms while moving |
| B5 | The panel: worlds, year slider and play, balance-sheet bar, owners, history | |
| B6 | Critic review, frames, handoff | |

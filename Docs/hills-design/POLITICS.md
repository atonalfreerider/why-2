# Politics: government in the point-cloud language (design, 7 Nov 2026)

The user's seventh direction is verbatim in `Docs/research/advantage/brief-v5.md`. This file holds the plan and the choices made
so far; the maps of what exists are in `Docs/research/politics/maps/` (key `government`, `flows`, `power`).

## What exists

- The regimes view (`HillRegimeLayer`) compares five states in one year, with no parties, officials, time series or military.
- The foundation view seats presidents and a few officeholders as line glyphs ranked head of state, vice president, national
  executive, legislature, state and local (`HillFoundationLayer.SeatActors`): the nearest thing to the cascade.
- The history view (`HillHistoryLayer`) has the machinery to reuse: lanes of tubes through time, point-cloud bodies opened at a
  year in a contest layout, advantage rings walled by their sources, portrait cards.
- The world model (`global.json`, `WorldModel`, `WorldLayout`) was built but is drawn nowhere.
- Data gaps: no party entities, seats, votes or officials beyond presidents; no naval, nuclear or alliance data after 1815; trade
  between countries for 2024 only (US pairs 2015-2025 in a cache); no clock that keeps flows moving when time is frozen.

## Choices (defaults until the user says otherwise)

1. **Scope.** The United States, 1945-2026, first; then the 13 economies at their latest year; then the five regimes.
2. **The view.** A new focused view, `politics`, beside the regimes view. Seen from the future at the shown year:
   - x: parties left to right, by their place on the spectrum;
   - y: power, the drawing's stated rule: holding the presidency counts one half, each chamber's seat share one quarter;
   - width: the population a party represents, its national popular vote (presidential years; the House vote in midterms);
   - z: time, the streams receding into the past.
3. **Colours.** The parties' conventional colours (Democratic blue, Republican red), added as two party roles to
   `EconomyStyle.Roles` and checked for contrast with `Tools/check-palette.py`, distinct from the people and desire roles.
4. **Opened at a year.** The executive is a point-cloud body at the centre top; the vice president, cabinet, congressional
   leaders, governors and military commanders cascade below by rank (the foundation view's ranks, plus a military rank on the
   army's side). Party bodies stand left and right, raised by power, with lobes for seats and votes.
5. **The standing army** stands on the country's far edge as its relative power, not a head count: a body sized by its share of the
   world's military spending, ringed by its advantage, with walls for sea control, nuclear weapons, alliances and spending (Jev's
   judgments, 0-4). For the United States and NATO it is linked to open markets and freedom of navigation.
6. **Trade between countries.** Lanes from the country's edge to its partners, exports and imports as two lanes, the deficit as
   their hatched gap (the mark code's owed hatch).
7. **No frozen moment.** A flow clock that never stops (`FlowClock`, anchored at a capture's first frame so recordings repeat)
   and one packet primitive for everything that moves: money, goods, capital, people, promises (contracts as marching dashes).
8. **Key moments.** Laws, regulations, trade actions, monetary turns and conflicts are sparks on the streams at their dates,
   coloured by kind, and each lands on the economy as topology (regulation strands, tariff walls) under the industries it touched.
9. **Portraits.** Approved by the user on 7 Nov 2026 ("yes, use public domain portraits", `brief-v5.md`): public-domain
   official portraits of the executive cascade (presidents, vice presidents, cabinet, congressional leaders, the Joint Chiefs),
   the approval recorded per person in `Tools/images/fetch.py`'s manifest.

## Plan

1. Research (`Docs/research/politics/`, gated with `python3 -I Tools/advantage/verify.py --dir Docs/research/politics --cluster
   <c>`): us-control, us-votes, us-officials, military-power, trade-balances, key-moments; a skeptic per pair of clusters.
2. `Tools/politics/build.py` writes `Assets/Resources/Data/economy/politics.json`; `Model/Politics.cs` reads it.
3. The flow clock and packets (Core and shaders), then the advantage view's rivers and the money chart's trade balance.
4. `Layers/HillPoliticsLayer.cs` and `UI/PoliticsPanel.cs`, the preset and view variants, smoke states.
5. Review, frames, and the user's look before more countries.

## Implementation (v1)

`Layers/HillPoliticsLayer.cs` (view `politics`, Order 67) draws the United States from Truman's accession to the year shown,
all from `Model/Politics.cs` (`politics.json`). Land-local frame; the cross-section stands at `HillLandscape.Z(year)` and the
camera looks from the future (yaw 180, pitch 6), fitted to the cross-section, the rivals and the partners between the
portrait cards and the panel's column.

**The year.** The view follows the year chip, at the middle of the chip's year (where the data's annual measures stand),
rebuilding under `HillRebuilds` and flying along as the crest moves. A tour cue or the panel pins a year:
`politics:us:<year>[:sel=<id>]`, clamped to the data's span; a whole year (`1965`) pins its middle, as the chip and the
panel's steps do, and a decimal year (`2025.06`) pins that date; `ShowState(null)` follows the chip again. The panel's
contract is `Year`, `Pinned`, `Selected`, `Changed`, `Pin` and `Select`.

**Encodings and scales.**

- **Parties.** Streams of dot rings from 1945 to the year, a ring about every 1.3 units of time. The Democrats stand at
  x + 34 (the screen's left from the future) and the Republicans at x - 34. Height is 60 units times power by the stated rule,
  above a floor at y 60. A ring's radius is the votes the party represents over 11.4 million, so 80 million votes draw 14
  units across. Every change of power or votes eases over 0.1 year, with extra rings through the jump so it reads as a step.
  Before the first election measured (November 1948) a stream is a faint 0.45-unit thread with no width implied.
- **People.** Soft People packets drift along each stream toward the present (1.2 a second), from the first measured election.
- **Key moments.** Sparks on the president's party's stream at each date, each with a strand straight down to the floor and
  a foot where it lands. Colours by kind, from palette roles:
  - conflict: Conflict;
  - trade: Commerce;
  - monetary: Owners;
  - law: the state's slate, lightened halfway to the ink;
  - regulation: the slate, lightened a quarter;
  - treaty: Conflict, lightened halfway.

  Moments of the last three years spark larger and are listed under the president's party's label, newest first, six at most.
- **The cascade.** The president's body (radius 4.6) stands at the top centre, coloured by party. Four lines step down evenly
  to the Chairman of the Joint Chiefs, who stands on the shell:
  1. the vice president;
  2. the Speaker and the Senate's leader;
  3. State, the Treasury and Defense (War and the Navy, four on the line, before 1947);
  4. the Chairman of the Joint Chiefs.

  Each holder is a small body (radius 2.3) in its party's colour, with a thin line up to the nearest holder on the line
  above. An appointee whose party the research infers from the appointing president takes the administration's colour and
  reads "in the Democratic administration". The military are the state's slate. A vacancy is an empty dotted outline
  labelled "vacant".
- **Party heads.** Point-cloud bodies at the streams' ends, the core as wide as the last ring (the votes). House and Senate
  lobes are 5.2 × ∛(seat share), so their volumes are the shares; the body's id is chosen so the lobes stand left (House) and
  right (Senate) of the camera. Each is labelled with its power ("82% of power") and its votes with their election year.
- **The army.** A shell of dotted circles (every 1.2 units out, a dot every 1.6 around) centred at y 84, inner radius 64. It is
  26 units thick for the whole of the world's military spending, so the 37% of 2024 draws 9.6 units. While NATO stands, its
  share beyond the US's is a fainter layer outside. Rivals stand beyond the shell on the screen's right (±24°, 116 units
  out) as shells on the same scale around an empty core: the Soviet Union until the dissolution moment (with the Warsaw
  Pact's layer while it stands), then Russia, and China.
- **Walls.** From `WallsAt` (Jev's judgments at the nearest benchmark, within two and a half years; alliances only while
  NATO stands), as arcs outside the shell:
  - nuclear weapons face the rivals (-60° to 40°);
  - military spending stands above right (46° to 86°);
  - alliances stand above left (94° to 130°);
  - command of the sea spans the trade lanes (136° to 262°), the visible link between sea control and open markets.

  Each arc is 1.2 + 2.4 × the wall thick and dashed under a confidence of 0.15. The colours are the palette's chart series,
  and each wall is labelled with Jev's reading and the benchmark's year. The key names the sea wall over the trade lanes
  as freedom of navigation.
- **Trade.** Partners measured within three years of the year (their nearest benchmark) stand at fixed angles, 150° to
  255° and 112 units out. Each lane has two bands, goods out and goods in, each a unit wide per $100bn a year, carrying
  Goods packets as wide as the band at 1.5 a second. The dollars for the deficit go out as a thin Money lane in the Owners
  colour (0.8 a second, inward for a surplus). The deficit is the hatched `Mark.Owed` gap beside the exports band where the
  lane crosses the US shell, as wide as imports beyond exports. Before any partner is measured, one lane runs to the world
  (goods and services, `TradeAt`), labelled with the first partner year; before 1960, a faint thread reads "Trade: not
  measured before 1960".
- **Command.** A Promise lane of marching dashes (1.6 a second) runs from the president down beside the cascade to the shell.

**Packets.** All packets share one speed (9 units a second), one mesh and one material, and are built once per rebuild. No
lane exceeds 4 packets a second; a flow's size is its lane's width.

**Labels and cards.** There are about 20 to 32 labels a year, every number from `Politics` with the year it was measured.
The portrait cards (`HistoryPopups`, ids from `Politics.ImageId`) show the president and the three most senior holders
below, the selected office first.

**Not measured.** These draw faint and say so:

- votes before November 1948;
- military shares before 1955 and across gaps of more than six years;
- NATO's share before 1955;
- trade before 1960.

Before 1983, when the nearest partner benchmark is 1985, the lane to the world says so ("by partner from 1985"). Not drawn,
with nothing in their place: the EU before 1995 (its first benchmark is 1997), and a wall Jev skipped (spending at the
1945 and 1950 benchmarks, so none from 1945 to 1952) or the alliances wall before NATO; the panel lists the walls judged.

**Palette and registration.**

- Two new roles, Democratic `#3A4E98` and Republican `#DC2626`. They are in `Tools/check-palette.py` and `PaletteCheck`
  with twelve touching pairs (each other, People, Desire, Conflict, Commerce, State, Owners), all above the floor.
- The four military sources are in `HillAdvantageLayer.SourceOrder`, `SourceName` and `SourceColor`. `HillHistoryLayer.
  SourceWord` now gives the advantage view's name for alliances: "Alliances", not "Allies".
- The preset is "Who holds power" ("Power" in the bar), in the wave chapter beside the regimes view, with portrait width
  330, the legend key and `EconomyViews.Focused`.
- The view is drawn alone: it is hidden from the wave, the attention cloud and the land's labels. Its emphasis row holds
  seven parts as a synthesis view.
- Its states are `politics:us:1965`, `politics:us:2025` and any walkthrough cue. The smoke sequence `politics` captures
  1950, 1965, 1985, 2001 and 2025, each at the middle of its year.

**Left for later.**

- No run in the player yet. The frames, label crowding, camera fit and frame time need
  `-smokeonly politics -smokeextra politics`.
- The moments land on an abstract floor, not on the industries they touched (the data has no affected industries).
- Not yet drawn: the warhead and fleet series (they reach the view only through Jev's walls) and the governors.
- Not yet built: the other countries, a tour stop and its narration, and a portrait-screen layout of the cross-section.

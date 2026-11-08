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

## Implementation (v2)

`Layers/HillPoliticsLayer.cs` (view `politics`, Order 67) draws the United States from Truman's accession to the year shown,
all from `Model/Politics.cs` (`politics.json`). Land-local frame; the cross-section stands at `HillLandscape.Z(year)`.

v2 (7 Nov 2026) answers the review of v1's frames (`Shots/pol1l`, `pol1p`): time did not show (the camera looked straight
down the time axis), the view filled the middle third of the screen, the party heads stood inside the cascade, every moment
sat on the president's party's stream, the Democratic blue read fainter than the red, the trade bands were wide slabs, and in
portrait four tall cards and the panel's sheet covered the view.

**The year.** The view follows the year chip, at the middle of the chip's year (where the data's annual measures stand),
rebuilding under `HillRebuilds` and flying along as the crest moves. A tour cue or the panel pins a year:
`politics:us:<year>[:sel=<id>]`, clamped to the data's span; a whole year (`1965`) pins its middle, as the chip and the
panel's steps do, and a decimal year (`2025.06`) pins that date; `ShowState(null)` follows the chip again. The panel's
contract is `Year`, `Pinned`, `Selected`, `Changed`, `Pin` and `Select`.

**The pose and the time axis.** (v3, from the v2 frames)

- The camera looks straight from the future (yaw 180) and 15 degrees from above, so the cross-section faces it square and
  both parties stand at one depth. v2 turned the camera 30 degrees toward the Democrats' side (yaw 210); on its frames the
  nearer side drew a tenth to a sixth larger (measured on a mock-up of its fit), enough to reverse 2024's close popular vote on screen (3% fewer Democratic votes
  drew 7% wider), and the fit swung 150 pixels when the European Union's lane appeared.
- The past is set back along a slant instead: a time a unit back from the shown year's plane stands `Shear` 0.6 units toward
  +x as well (`Past(x, y, z, t)`), so it recedes to the screen's left and up behind the cross-section and each stream reads
  as a chart, time running toward the present on the right; 0.6 opens the decades as far as the 30-degree turn did.
- The view's own time axis: a year back stands `TimeScale` 1.5 times as deep as on the land (`Back(z, t) = z + 1.5 (Z(t) -
  z)`), the cross-section staying at the shown year's plane. At the land's own depth the decade marks stood atop one another.
- The past fades: a stream's rings, the presidency's track and the sparks draw at `PastAlpha` 0.25 of their own alpha from
  `FadeYears` 20 years back (eased from the shown year's whole; the track and the sparks keep at least half). The slant sets
  the Republicans' recent past behind the cascade's names, and the fade keeps it from crowding them.
- The parties' seat lobes are turned to the camera's left and right (`FacingId`: the twist nearest `180 - Yaw` degrees, 0).

**The fit.** `FlyToState` puts the essentials as large as they go into the free screen.

- The essentials (`Essentials`, built with the labels): the shell's reach (the walls' outer edge, 24 points round), each
  party's name with 210 pixels of room outward and 19 up and down, the title above, the past's far end (each stream's first
  ring, and the presidency track's first point with a decade mark's room), and every partner's seat with its name's room (125
  pixels toward the screen's left, 16 up and down) and the world's (125, 24 up and down: its label is broken into three lines
  as a partner's is, where on one line it took 205 pixels and set the portrait view's width). Every seat is held whichever
  partners stand at the year, so the frame keeps still from year to year. The nodes stand at the essentials' lower left;
  fitted without their names (a first v2), the names ran onto the legend, where the labels drop them. The rivals and the
  walls' names may stand near the free screen's edges.
- The free screen (`LargestFree`): of the rects whose edges are the screen's 16-pixel margins or the obstacles' edges (a
  12-pixel gap away), the one where the essentials stand largest, their names' pixels taken off its width and height before
  their world box is set against them; the larger on ties. The obstacles are the HUD's blocks as the labels keep out of them
  (`LabelSystem.HudRects`: the title, the buttons, the year chip and its readout, the legend, the preset bar, the economy's
  panels, `PoliticsPanel` among them through `HudBlocks.Register`, and the tour's card) and, in landscape, the portrait cards'
  column (`HistoryPopups`' layout: left 24, top 176, 330 wide, four cards of 96 with gaps of 10, canvas units). The rect lies
  below every block standing in the screen's top 8% (the title, the chip) and above those in its bottom 8% (the preset bar),
  so the view never climbs beside the HUD's title.
- The solve (`Solve`): six passes, each scaling the distance by how far the essentials' world box misses the rect (their
  names' pixels kept as they are), then centring the box; within a pixel, perspective included. Distance 110 to 3000. The
  pose's `PortraitWidth` is 0, so the fitted distance stands in portrait too.
- When it fits: on focus, a pin and a year change (as v1), and when the HUD's blocks change (the panel shows a frame after
  the view, a portrait sheet opens, a tour's card comes, the screen turns) and then hold still for 8 frames; that refit flies
  only if the pose moved, and waits while a year change waits for its turn to rebuild (`HillRebuilds`), whose rebuild fits
  the camera itself. Flights take 0 s under `-smoketest`.
- On the v3 frames: in landscape 1920x1080 the essentials stand in the rect right of the cards and above the legend for
  every smoke year, x 419-1381 to 439-1361 by y 133-798, distance 387 (1950) to 422 (2001), the shell about 560 pixels
  across; in portrait 1080x1920 they take the width between the What-if button and the panel's header row, distance 797 to
  838. v1's shell was about 340 pixels across in landscape.

**Encodings and scales.**

- **Parties.** Streams of dot rings from 1945 to the year, a ring every 2 units of the view's time axis (two thirds of a
  year). The Democrats stand at x + 50 (the screen's left from the future) and the Republicans at x - 50 (v1: 34), outside
  the cascade's widest line. Height is 60 units times power by the stated rule, above a floor at y 60. A ring's radius is the
  votes the party represents over 11.4 million, so 80 million votes draw 14 units across. Every change of power or votes
  eases over 0.1 year, with extra rings through the jump so it reads as a step. Before the first election measured (November
  1948) a stream is a faint 0.45-unit thread with no width implied.
- **People.** Soft People packets drift along each stream toward the present (1.2 a second), from the first measured election.
- **The presidency.** A thin track (2.6 pixels) through time at x 0 of the cross-section and the executive's height (y 138),
  term by term in the president's party colour (Truman blue, Eisenhower red ...), with a short tick where each term begins,
  so a successor of the same party reads as a new term. Decade marks ("1950", dim ink, the lowest rank) stand above it, none
  within four years of the year shown, and every second decade over a span longer than 40 years (they stood on one another
  in the distance).
- **Key moments.** Sparks on the presidency's track at each date, whichever party held it (v1 put them on the president's
  party's stream, which read as if that party had done foreign events). Those of the last eight years (`StrandYears`) have
  a strand straight down to the floor (alpha 0.28, the named ones 0.5) and a foot where it lands; v2 drew all 56 by 2025,
  a curtain over the streams. Colours by kind, from palette roles:
  - conflict: Conflict;
  - trade: Commerce;
  - monetary: Owners;
  - law: the state's slate, lightened halfway to the ink;
  - regulation: the slate, lightened a quarter;
  - treaty: Conflict, lightened halfway.

  Moments of the last three years spark larger and are listed beside the president on the screen's right, newest first, six
  at most.
- **The cascade.** The president's body (radius 4.6) stands at the top centre, coloured by party. Four lines step down evenly
  (31.3 units) to the Chairman of the Joint Chiefs, who stands on the shell:
  1. the vice president;
  2. the Speaker and the Senate's leader;
  3. State, the Treasury and Defense (War and the Navy, four on the line, before 1947);
  4. the Chairman of the Joint Chiefs.

  Each holder is a small body (radius 2.3) in its party's colour, with a thin line up to the nearest holder on the line
  above. An appointee whose party the research infers from the appointing president takes the administration's colour and
  reads "in the Democratic administration"; a chamber leader's inferred party reads "of the Democratic majority". The
  military are the state's slate. A vacancy is an empty dotted outline labelled "vacant".
- **Party heads.** Point-cloud bodies at the streams' ends, the core as wide as the last ring (the votes). House and Senate
  lobes are 5.2 × ∛(seat share), so their volumes are the shares, the House lobe on the screen's left and the Senate's on its
  right. Each is labelled outside its head, away from the middle, with its power ("82% of power") and its votes with their
  election year.
- **The army.** A shell of dotted circles (every 1.2 units out, a dot every 1.6 around) centred at y 84, inner radius 74 (v1:
  64), enclosing the heads with their lobes at the most power a party has held (84%). It is 26 units thick for the whole of the
  world's military spending, so the 37% of 2024 draws 9.6 units. While NATO stands, its share beyond the US's is a fainter
  layer outside. Rivals stand beyond the shell on the screen's right (±24°, 132 units out, beyond the Republicans' name) as
  shells on the same scale around an empty core: the Soviet Union until the dissolution moment (with the Warsaw Pact's layer
  while it stands), then Russia, and China.
- **Walls.** From `WallsAt` (Jev's judgments at the nearest benchmark, within two and a half years; alliances only while
  NATO stands), as arcs outside the shell:
  - nuclear weapons face the rivals (-60° to 40°);
  - military spending stands above right (46° to 86°);
  - alliances stand above left (94° to 130°);
  - command of the sea spans the trade lanes (136° to 262°), the visible link between sea control and open markets.

  Each arc is 1.2 + 2.4 × the wall thick and dashed under a confidence of 0.15. The colours are the palette's chart series,
  and each wall is labelled with Jev's reading and the benchmark's year. The key names the sea wall over the trade lanes
  as freedom of navigation.
- **Trade.** Partners measured within three years of the year (their nearest benchmark) stand on the lower left at fixed
  angles, 192° to 232° (8° apart; v1 150° to 255°), 122 units out; the lane to the world stands at 212°. Each lane runs from
  8 units inside the shell's inner edge (the country's edge; v1 started 10 units from the middle and crossed the left half)
  to its node. Its two bands, goods out and goods in, stand either side of the path, 1.2 units apart, each 0.3 units wide per
  $100bn a year (v1: 1), so the data's widest flow, the EU's $633bn of goods in 2024, draws 1.9 units, with a 1-pixel
  hairline so a small flow still shows its lane; each carries Goods packets 2.5 pixels plus 0.85 of the band at 1.5 a
  second. The dollars for the deficit go out between them as a thin Money lane in the Owners colour (0.8 a second, inward
  for a surplus). The deficit is the hatched `Mark.Owed` gap beside the exports band where the lane crosses the US shell, as
  wide as imports beyond exports (at the bands' scale, so the EU's $220bn of 2025 is 0.66 units). Before any partner is
  measured, one lane runs to the world (goods and services, `TradeAt`), labelled with the first partner year; before 1960, a
  faint thread reads "Trade: not measured before 1960".
- **Command.** A Promise lane of marching dashes (1.6 a second) runs from the president down beside the cascade to the shell.

**Packets.** All packets share one speed (9 units a second), one mesh and one material, and are built once per rebuild. No
lane exceeds 4 packets a second; a flow's size is its lane's width.

**Labels and cards.** About 23 to 37 labels a year (the decade marks the lowest rank, culled first), every number from
`Politics` with the year it was measured. The portrait cards (`HistoryPopups`, ids from `Politics.ImageId`) show the
president and the three most senior holders below, the selected office first, on a landscape screen; on a portrait one at
most the president's card, and only where the fitted essentials leave its place free (at 9:16 with today's HUD they fill the
width, so none shows).

**Not measured.** These draw faint and say so:

- votes before November 1948;
- military shares before 1955 and across gaps of more than six years;
- NATO's share before 1955;
- trade before 1960.

Before 1983, when the nearest partner benchmark is 1985, the lane to the world says so ("by partner from 1985"). Not drawn,
with nothing in their place: the EU before 1995 (its first benchmark is 1997), and a wall Jev skipped (spending at the
1945 and 1950 benchmarks, so none from 1945 to 1952) or the alliances wall before NATO; the panel lists the walls judged.

**Palette and registration.**

- Two party roles, Democratic `#4F7BB4` (v1 `#3A4E98`) and Republican `#DC2626`, at one OKLab lightness (0.576 and 0.577;
  v1's blue was 0.448 and made the Democrats read fainter). Both are in `Tools/check-palette.py` and `PaletteCheck` with twelve
  touching pairs (each other, People, Desire, Conflict, Commerce, State, Owners), all above the floor of 8; the least is
  democratic-conflict, 8.4 under deuteranopia (8.7 rendered without a tonemapper, as the hills are). `--pairs` also fails when
  the two parties' targets differ in lightness by more than 0.02.
- The four military sources are in `HillAdvantageLayer.SourceOrder`, `SourceName` and `SourceColor`. `HillHistoryLayer.
  SourceWord` gives the advantage view's name for alliances: "Alliances", not "Allies".
- The preset is "Who holds power" ("Power" in the bar), in the wave chapter beside the regimes view, with the legend key and
  `EconomyViews.Focused`; its catalog pose (yaw 180, pitch 15) only starts the layer's flight.
- The view is drawn alone: it is hidden from the wave, the attention cloud and the land's labels. Its emphasis row holds
  seven parts as a synthesis view (the presidency's track and the decade marks with the streams, the sparks with the marks).
- Its states are `politics:us:1965`, `politics:us:2025` and any walkthrough cue. The smoke sequence `politics` captures
  1950, 1965, 1985, 2001 and 2025, each at the middle of its year, and fails a year that draws no executive, no streams or no
  presidency's track, or whose fitted essentials run off the screen; each logs its fit (the box, the distance, the cards).

**Moments as topology.** (8 Nov 2026, choice 8's second half) `Layers/HillMomentsLayer.cs` (Order 68) lands the key moments
on the economy, in the landscape view ("Layers of the economy") only.

- **Data.** Each moment's `touches` in `politics.json` (`Politics.Moment.Touches`): Jev's (TypeSafe's model, jev-1.14.0)
  score 0-4 of how directly the moment changed the rules, costs, demand or competition each of the 25 industries of
  `industries.json` faced in the five years after it, with its confidence. Judged over the gated claims by
  `Tools/politics/touches.py` (17 anchor expectations met; answers in `Docs/research/politics/touches.json`), written by
  `Tools/politics/build.py`. Judgments, not measurements.
- **Fences.** Each moment up to the year shown stands at its date (`HillLandscape.Z`; never past the crest, which stands at
  the year's start: the moments of the year shown and the year before are drawn in, in their order, to stand behind it) as a thin translucent fence across the ridge of each industry it touched with a score of 2 or more:
  across the ridge where its own relief stands above the strata at that date (`Bump`), its foot on the surface the ribs draw
  (`Ground`), 2.2 units tall per point (a 4 stands 9, a little over the median ridge's own relief; at 1.5 the first frames showed only faint strokes), in the moment's kind colour
  (`KindColor`): a faint fill, pickets every 1.5 units and a rail, dashed where Jev's confidence is under 0.15. The past fades
  as this view's does (to 0.25 at 20 years). No packet lane: a fence is a standing rule, not a flow.
- **Left out.** The government's own industries (federal; state and local), which lie in the bedrock with no ridge (21
  touches by 2025; the panel lists them), and Bretton Woods (1944), before the land's first rib.
- **Names.** Moments of the last three years only (as the politics view names its latest): "2022 · CHIPS and Science Act"
  over the tallest fence, role labels ranked by score and age, placed or culled by the label system. The first frames named
  every moment with a touch of 3.5 or more too, 20 names over 2025's land whose plates hid the wave's company names; older
  moments are named in the politics view and its panel.
- **Expected.** 2025: 124 fences on 20 ridges for 42 moments, 24 names; 1985: 39 fences on 16 ridges for 14 moments, 8 names.
- **Registration.** Emphasis rows `moments.fences` and `moments.rails` (the landscape only). The landscape's legend key is the
  wave's own with a line on the fences. The panel's Moments tab lists under each moment the industries Jev scored 2 or
  more, by name with the score (as many as fit one line, the rest counted).
- **Smoke.** The check `moments` after every capture of the landscape logs what it drew and fails a year with touches to
  fence but no fences; `-smokeextra moments` captures the landscape at 1985 and 2025.

**Left for later.**

- The open portrait sheet has no smoke state, so no frame shows it yet.
- In portrait, at labels 1.5 times larger, the cascade's lower names (the Speaker, Defense) and some decade marks are culled.
- Jev's touches to doubt (review of 8 Nov 2026; the questions, not the answers, are what would change): parts of laws not in
  their claims are missed (TARP's carmakers and AIG, the CARES Act's airline grants, the American Rescue Plan's restaurant
  fund, the One Big Beautiful Bill Act, which touches no industry; Section 230 of the Telecommunications Act, internet 1.8);
  monetary turns put banking at about 3.5, which rounds to "written for" it; the five years after a moment of 2022-2026
  have not all passed, so those touches are partly forecasts; defense drawdowns and procurement cuts (the Soviet
  dissolution, the Budget Control Act) do not reach Manufacturing.
- Not yet drawn: the warhead and fleet series (they reach the view only through Jev's walls) and the governors.
- Not yet built: the other countries, a tour stop and its narration.

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

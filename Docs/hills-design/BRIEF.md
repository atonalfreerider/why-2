# Economy scene, second redesign: the hill landscape game

`SP=/tmp/claude-0/-home-user-why-2/dc9bf553-91de-54d2-9f1f-9831e1f24cfa/scratchpad`. Read first:
`$SP/review/BRIEF.md` (the original request and the user's notebook), `$SP/redesign/BRIEF.md` (the first redesign's
direction, verbatim), and this file.

## The user's new direction (verbatim, 2026-10-02, after seeing the stepped-bowl landscape)

"this is good progress - you can save the circular torus economy game, but we need to expand this out into more of a
landscape with hills representing the hierarchies. I would like to see golden crowns of owners over certain sectors to
show who is in control of those companies, and then have pools of ultra wealthy floating at cloud level between the
hills to show when people own stakes in many different industries but don't control them.

the less wealthy should live on the valley floors - again if they earn from a job in a sector, they should be close to
that hill. any capital that flows to a sector should be a river flowing up the hill

I didn't mean for the "control, not in control" to be taken literally. it moreso should be apparent from where a person
sits in the landscape

for the desire/fear brain diagram - I would like a separate 3d visualization of that, but it should also be the
fundamental program that each player runs in the landscape economy simulation. tracking assets, debt, spending
behavior, income per player (player represents group of people). generational wealth, family inheritance and allowance
is useful data - children living with or depending on parents should be shown in the hierarchy

on the smv graph, we need to show relationships between men and women, and children are born from two adults
intersecting in the mid-plane of the graph"

And then: "keep but disable the circular torus economy layout and replace with landscape game".

## Read as requirements

1. **Keep but disable the bowl** ("circular torus"): its code stays in the repo, compiling, but is not created in the
   scene (the `[GraphScenes]` retirement pattern already used for the old stations: a scene id that is never loaded).
   Reuse its models wherever they still fit (census of players, money routing, the season of tit for tat, the lives).
2. **A landscape with hills for the hierarchy.** Industries are hills; the hierarchy (the notebook's gov / raw / make /
   services / tech strata, and value created) is expressed by the terrain: height, position, which hill stands on or
   behind which. Dependencies (the IO table) should still read (the first redesign's "dependencies feed upward from
   underneath").
3. **Golden crowns of owners over sectors**: the controllers of companies (founders, controlling owners, the named
   companies' insiders; and owners of private businesses) float as golden crowns over the hills / sectors they control.
4. **Pools of the ultra wealthy at cloud level between the hills**: people who own stakes in many industries without
   controlling them (diversified capital owners: the top of the wealth distribution living on capital income) float as
   pools / clouds at cloud height between the hills.
5. **The less wealthy live on the valley floors**; a person who earns from a job in a sector lives close to that hill.
6. **Capital flowing to a sector is a river flowing UP the hill.**
7. **Control is shown by where a person sits**, never by a literal "in control" label or gold dots meaning "in control".
   (The agency / in-control measure can stay in the model and the inspector, but the landscape's geometry carries it:
   valley floor -> slopes -> crowns -> clouds.)
8. **The desire / fear brain as a separate 3D visualization** AND as **the fundamental program every player runs** in
   the landscape economy simulation: per player (a player is a group of people) it tracks assets, debt, spending
   behavior and income; it carries generational wealth, family inheritance and allowance; children living with or
   depending on their parents are shown in the hierarchy (where they sit relative to the parents).
9. **The SMV graph** (the population lifelines on the road: women on the inner side, men on the outer side, the
   mid-plane between them): show the relationships between men and women (couples), and children are born from two
   adults whose lines intersect in the mid-plane (the child's line starts where its mother's and father's lines meet
   at the mid-plane at its birth).
10. Everything else the user asked for in the first redesign still holds: a cross-section of the temporal graph that
    expands into the 3D landscape; spending that pools together; groups interacting with in-group / out-group
    affinity in a basic tit-for-tat simulation; players grouped by 2026 socioeconomic groups; topology, not geography,
    as the organizing structure; the visual language (glowing lines and veils on black; gold = capital, blue = people,
    rose = desire, ice = fear, steel = government).

## What exists now (branch `claude/economy-visualization-2026-n8j6kd`, HEAD 16262b8)

- The first redesign's spec: `$SP/redesign/SPEC.md` (the stepped bowl), its build notes `$SP/redesign/build/FOLLOWUPS.md`,
  the groups research `$SP/redesign/groups.md`. The economy guide `Docs/ECONOMY.md` describes the current scene.
- Code: `Assets/Scripts/Economy/` — `Land/` (contracts `LandTypes.cs`, `LandStyle.cs`, `LandFrame.cs`, `LandLayout.cs`
  (bowl geometry), `PlayerCensus.cs` + `PlayerPlacement.cs` (players: 12 groups x anchor industry x party, 116 in 2025),
  `MoneyRouting.cs` + `SellerMatrix.cs` + `CanalRouting.cs` + `CapitalSources.cs` (money per player and flows),
  `SocialSeason.cs` + `Coalitions.cs` (the tit-for-tat season), `RootsModel.cs`, `LandService.cs` (snapshots, year
  changes), `LandView.cs` (view state), `LandPick.cs`), `Layers/` (`LandModelLayer`, `LandViewLayer`, `LandscapeLayer`,
  `InhabitantsLayer`, `FlowsLayer`, `SocialLayer`, `SectionLayer` (the cut and the unfold morph), `IndustryWallLayer`,
  `EconomyLoaderLayer`), `UI/` (pickers, player and person inspectors, legend, year scrubber, social panel),
  `Model/` (the economic lives: `EconomicLives`, `LivesSimulation` (per person per year: income by source, taxes,
  transfers, spending by category with fear / desire / fantasy, saving, borrowing, wealth, debt, homes, inheritance,
  households, children), `TraitSampler` (Big Five, drives, OS), `LivesReport`, `MoneyCircuit`, `PrisonersDilemma`,
  `GamesSetup`), `EconomyViews.cs` / `EconomyPresets.cs` (the 11 views), `EconomyState.cs`.
- The population graph: `Assets/Scripts/Humans/Smv/` (`SmvSimulation` with `MarriageLog`, mothers / fathers, births;
  `SmvGeometry` draws the lifelines (women inner, men outer; height = social market value); `SmvLayer`; the economy
  restyles lines through `ISmvLineStyle`). It is SHARED with the causality scene (`Why.unity`), which must stay
  unchanged unless a change is explicitly scoped to the economy scene.
- Data: `Assets/Resources/Data/economy/*.json` (industries with value added 1947-2026 and IO table, circuit with
  companies (capture), spending categories with motives, psyche (drives, chemicals, modes, OS, traits, agency),
  games (payoffs, strategies, social season block), groups.json, history).
- Renders of the current state: `$SP/redesign/build/lead-fix6/land/*.png` (16:9) and `.../portrait/*.png` (9:16).
- Engine constraints (unchanged): Unity 6 URP, everything built at runtime in code; `LineMeshBuilder` /
  `SurfaceMeshBuilder` with `Why/Line` and `Why/Surface` (unlit, translucent, ZWrite off, bloom; shading must be baked
  into vertex colors); world-space ("raw") placement; layers (`Prepare` on workers, `Upload` / `Tick` on the main
  thread); labels / anchors / highlight ids; view presets; the headless harness (`$SP/run/README.md`; private copies
  via `$SP/agent-tools.sh <name> <tree>`; `WHY_NOW=2026-10-01T12:00:00Z` pins the clock) renders presets to PNG with
  the full log; `$SP/whycheck.sh` checks the causality scene pixel for pixel; `$SP/redesign/checks/landcheck.py` checks
  the current land's log lines.

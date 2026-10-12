# Review: the causality graph and the economy wave (October 2026)

A full review of both scenes against the model's intent, the design principles adopted to make the graph immersive,
expansive and easy to read, and what changed. Findings are specific (file and behavior); "done" items are in `main`.

## The model this review holds the code to

A causality graph: by understanding cause and effect, a working model of the universe can be built from the Big Bang to
this moment. Its architecture is not negotiable:

1. **Spiral time.** A super-logarithmic clock of deep time that straightens into the near-linear road of human history.
2. **Three levels**, each standing on the one beneath it:
   * **red - the physical universe**, governed by the Standard Model and our cosmological models inside general and
     special relativity;
   * **green - the biosphere**, ordered by trophic level (the food chain) and by phylogenetic history; higher organisms
     use the organisms beneath them as calorie inputs, for efficiency;
   * **blue - human civilization**: tribes and their social market hierarchies, with lifespans and relative power over
     time, oriented west (inside) to east (outside).
3. **The economy** (second scene, the United States by default): the present cross-section of the blue level opens into
   a wave moving forward through time. Like the food chain, top companies run by golden circles of elite owners build
   on other businesses, on infrastructure, on subsistence infrastructure, all supported by a foundational government
   (here a constitutional federal democracy). The population inhabits the wave as a topographic map, positioned as close
   as possible to its source of value (employer, government, family wealth); rivers of capital and debt flow in two
   chromatic spectrums, desire (rose) and fear (ice), which govern behavior. Individuals and groups decide by tit for tat
   (iterated prisoner's dilemmas) and can move along the economic ladder drawn by the landscape.

## Design principles adopted

These now guide every view; each is tied to the change that implements it.

| Principle | What it means here | Where |
| --- | --- | --- |
| **One grammar, everywhere** | Hue = level only (red, green, blue; gold = capital, rose = desire, ice = fear in the economy). Height = hierarchy. Radius = relevance to us. Never reuse a channel for something else. | existing core; economy review fixes below |
| **The diorama** | Any part of the graph can be brought to arm's length at any scale and walked around. The world scales to the subject, not the subject to the screen. | `Xr/XrRig`, `CameraRig.XrOrigin` |
| **Grab the universe** | Direct manipulation beats menus: drag the world with a hand, resize it with two. Unlimited 3D space in every dimension, but the spiral, the levels and the hierarchy never move relative to each other. | `XrRig.Locomotion` |
| **Every structure is real 3D** | Diagrams that explain (the mind, the dilemma arena, the ladder, the calorie flow) are built in the world, not on the screen, so they read from any angle and in stereo. | economy atomistic views; trophic flow |
| **Zoom has meaning** | Each step in scale reveals the next level of cause: universe → level → clade or civilization → one life → one mind → one decision. | presets and the walkthrough's order |
| **A voice that knows why** | The walkthrough is narrated by the graph's author, top to bottom, and every claim matches what is on screen. | `Data/Walkthrough`, Jev check |
| **Point, don't explain** | The narration always has a target in the world (arrow on screens, beacon in a headset). | `DirectorMode`, `Xr/XrBeacon` |
| **Honest models** | Where the economy is a model (psychological weights, terrain height, synthetic portfolios), it says so once, plainly. | narration, README |

## Causality scene (`Assets/Scenes/Why.unity`)

**Strengths.** The data-space + GPU warp core is the right foundation: re-scaling is animating a handful of shader
globals with zero mesh rebuilds, so the spiral can unroll anywhere and the levels stay locked together. Loading is
asynchronous and fast. The color grammar is disciplined. West-to-east ordering of civilizations (inside to outside) is
implemented in `HumanWorld`/`CivBandsLayer`, and the biosphere is ordered by food chain with our lineage innermost.

**Findings.**

1. **The full tour and its narration had silently left the project.** `tour.json` and `tour_narration.json` were
   untracked (`.gitignore`), so every fresh checkout fell back to an 8-step built-in tour with no audio. *Done:* the
   63-stop tour is restored as a tracked walkthrough with a new opening stop, voiced by John; drafts still override it
   locally.
2. **The calorie relationship of the green level was implied, never shown.** Branches are ordered by food chain, but
   the energy flowing up it (the reason for the order) was not drawn. *Done:* the calorie flow layer (below).
3. **Screen-only interaction.** Picking, tooltips, the tour's arrow and every canvas depended on a mouse and a flat
   screen (ScreenSpaceOverlay canvases do not render in a headset). *Done:* one `PointerInput` (mouse or controller
   ray) for labels and clicks; world-space panels in a headset; a 3D beacon for the tour target.
4. **The two scenes did not connect.** The universe's blue level ends where the economy begins, but nothing led from
   one to the other. *Done:* the universe tour ends by continuing into the economy tour; Tab switches scenes.
5. **Documentation drift.** `ARCHITECTURE.md` gave branch constants (`L = 6`, `CH = 3000`) that the code had changed
   (`8`, `400`). *Done.*
6. **Open: constant-pixel lines.** `Why/Line` widths are pixels per eye: crisp on monitors, but in stereo a line has no
   true thickness. A world-width mode for the headset would read better up close. Not changed (needs device testing).
7. **Open: no culling.** All meshes have infinite bounds (the vertex shader moves them), so everything is drawn every
   frame. Fine on desktop GPUs; must be profiled on Quest (lifeline level of detail is the first lever).
8. **Open: only labels are pickable.** Geometry (a band, a branch) cannot be pointed at directly; in a headset this
   matters more. A CPU inverse of the warp exists (`GraphWarp.Inverse`) and could back geometry picking.

## Economy scene (`Assets/Scenes/Economy.unity`)

**Strengths.** One time axis shared by the road of lives and the wave is the right idea; the crest-as-present and ribs
as past years read well. Tiers stand on a government bedrock; companies live and die like people; capture (wages
down, taxes to the bedrock, surplus up) is explicit at every market; the lives model has real per-person desire and
fear drives, strategies and a prisoner's dilemma engine.

**Findings.**

1. **Government-funded people were drawn inside the bedrock** (y ≈ -18), contradicting the "valley floor" legend, and
   validation skipped them. *Done:* public servants, Social Security retirees and the out of work stand on the valley
   floor near the crest; their markets, heights and the legend follow, and `HillValidation` checks them.
2. **Golden circles existed for two owners only** (`hill-owners.json`); elsewhere owners floated in a detached cloud.
   *Done:* a gold inner circle around each of the 25 tower firms, sized by the top 1%'s share of corporate equity
   times the firm's US-held market value (a proxy, not a list of owners), threaded to the 1% cloud.
3. **River color meant the spender, not the purchase.** Lanes split by the spender's average fear, so a health or
   insurance river was not ice because of what it buys. *Done:* lanes split by the fear share of what is bought
   (Jev's purchase judgments blended with each category's stored fear share): health 0.85 and insurance 0.70 read as
   ice, entertainment and restaurants 0.21 as rose.
4. **The mind was static and fear never evolved.** `PlayerMindProgram.Step` computed scarcity/pressure fear locally
   and discarded it. *Done:* fear and reason drift a third of the way per year toward a target set by disposition,
   stress and seller pressure, and the Reason/Fear controls move the disposition. The six-node diagram is replaced by
   one person's mind (view `atom`, and key 7): five desire drives (rose) and five fear drives (ice) from the lives
   model, a reason gate, a choice (spend, save, borrow) and memory; rose threads reach toward the markets that serve
   each desire, ice threads come from each threat, and income threads arrive from the person's source of value.
5. **Game theory never touched an individual, or money.** Tit for tat ran as a mean field between groups; there was no
   pairwise memory and no payoff consequence. *Done (view `dilemma`):* a live iterated prisoner's dilemma with
   pairwise memory between the person and five real counterparts of the same year (spouse or parent, coworker,
   neighbor, boss or firm, someone from the other tribe), each with the strategy the lives model gave them; pulses per
   round, a 12-round memory strip with tit for tat's copy marked, running payoffs, one deliberate defection in round 9
   to show retaliation and forgiveness, and the group level (two tribes' cooperation) above. The payoff ledger is
   illustrative and changes no recorded data.
6. **No one could be followed up the ladder.** Cohorts are re-cut each year; no life's path across hills and rungs was
   drawn. *Done (view `ladder`):* the followed person's adult life across the wave, each year on the household's hill
   at its income rung, blue to gold as agency rises, with labelled events, a ghost line at the parents' rank and a
   walker looping along it. The followed person is the one inspected, else the "striver" notable.
7. **Open: tier order versus the thesis.** The stack is sector type (raw, make, services, tech) rather than dependence
   (subsistence infrastructure, infrastructure, businesses, top firms); top companies are beams on their industry's
   ridge, not a tier. The golden circles make the summit legible, but a dependence-ordered stack is the deeper fix.
8. **Open: placement is by industry, not employer.** People never connect to the 75 companies or 25 towers that employ
   them; family wealth has no place on the map.
9. **Open: legacy weight.** The retired bowl pipeline (`LandLayout`, `MoneyRouting`, `CanalRouting`, `SocialSeason`)
   still runs for every snapshot; `Docs/ECONOMY.md` describes the bowl; view emphasis is spread over five layers'
   string switches.

## Delivered in this pass

* **Narrated walkthrough in John's voice**, 78 stops, about 34 minutes, universe then economy; scripts tracked and
  checked against the screen with Jev; ElevenLabs JohnV4 synthesis tool.
* **Meta Quest build** (OpenXR, Vulkan, IL2CPP ARM64): the diorama rig, controllers, world-space HUD, hand menu, tour
  beacon, scene switching; `Tools/build-quest.sh`.
* **Atomistic economy views** (`atom`, `dilemma`, `ladder`) and the four economy fixes above.
* **Calorie flow** in the biosphere (`TrophicFlowLayer`): at three time slices (Devonian seas, Jurassic, Miocene
  grasslands) arcs carry energy up a representative food chain, each step about ten times thinner (roughly 10%
  ecological efficiency), pulses running from prey to predator; a narrated stop, "Calories flow upward".
* **Smoke test** (`-smoketest`): every view and tour stop of both scenes in a player, failing on any error.
* **Tooling:** a cross-platform compile check, a Jev caller, stereo-ready shaders.

## Next

1. Profile on a Quest 3: frame time per scene, lifeline level of detail, bloom cost; world-width lines for the headset.
2. A dependence-ordered economy stack with top firms as the summit tier; employer-level placement.
3. Retire the bowl pipeline from the economy's per-snapshot build; rewrite `Docs/ECONOMY.md` for the wave.
4. Geometry picking from `GraphWarp.Inverse`, so any band or branch can be pointed at in a headset.

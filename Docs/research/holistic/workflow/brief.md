# Holistic views brief: objects first, mobility and collective action as scenes, images of the actors

You are one agent of a design workflow on the why-2 Unity project (repository root `/home/john/Desktop/Unity/why-2`,
branch main, Unity 6000.6.5f1, URP, Mono player, also built for Quest). The project is a 3D visualization: the
universe from the Big Bang to now (the "why" scene), and as its newest link the US economy drawn as a landscape of
hills (the "economy" scene: industries as hills along tiers from raw materials to tech, a wave of the economy's past
running back from today's crest, firms' halos and lifelines, households, flows of money, an attention cloud, the state
as bedrock). Read `README.md`, `Docs/ECONOMY.md` and `Docs/hills-design/README.md` first for orientation.

## What the user asked for (4 Nov 2026, verbatim)

> there is too much line clutter in the economy view. we should have object emphasis and only use line emphasis when
> illustrating a point. faded and bloomed lines can still be present, but used sparingly
>
> I still want you to break from traditional graph views and create holistic view states to show economic mobility and
> collective action game theory with real world examples
>
> overall we need more image data to illustrate individuals, collectives (companies, governments, groups). pull these
> thumbnail images from open sources

Earlier (3 Nov) the user asked for a top-to-bottom evaluation of how understandable the economy's graphics are and for
new display modalities: the wave west to east; foundational governing structures and the economies on them; the value
chain with tech at the summit; owners, employees and spenders (desire vs fear); the attention ("Instagram status")
cloud; individual storylines rising or falling; an instantaneous, balance-sheet-like view; the global level
(adjacent economies, trade, war and conflict). That work produced `Docs/hills-design/EVALUATION.md` (the audit),
`Docs/hills-design/MODALITIES.md` (the build spec: modalities M1 the instant, M2 the world, M3 the lives) and
`Docs/hills-design/MODALITIES-PLAN.md` (39 work packages in groups G1-G11 and decisions 1-36, and a section "Carried
into the packages" listing the label problems the polish rounds left). G1-G3a are built; G3b (WP07, a literal
emphasis table for every view; WP09, the label system extended for the hills, with candidate places, halos and
plates) is built and being reviewed now in other files. The new direction amends that spec: it does not throw it away.

## The current state you will design from

- Frames of every view and tour stop, from the current build with no pointer (the default state a first-time viewer
  sees): `/home/john/Desktop/Unity/.why-2-build/Shots/base1/<name>.png` (economy-view-*, economy-tour-NN-*,
  economy-replay-*, why-*). The same build with a parked pointer (hover cards):
  `/home/john/Desktop/Unity/.why-2-build/Shots/p4h/`. Look at them with the Read tool; crop and zoom with ffmpeg
  (`ffmpeg -loglevel error -y -i IN -vf "crop=W:H:X:Y,scale=iw*2:-1" OUT`); no PIL or numpy.
- The views and their layers: `Assets/Scripts/Economy/HillPresets.cs` (titles, subtitles, poses), `EconomyViews.cs`,
  the layers in `Assets/Scripts/Economy/Layers/`, the new emphasis table `Assets/Scripts/Economy/Emphasis/EmphasisTable.cs`
  (every view's alpha per part, transcribed from the layers' old Emphasis() switches), the tour in
  `Assets/Resources/Data/Walkthrough/economy_tour.json`.
- Mobility today: `HillMobilityLayer.cs` (drift arrows on the hills in the people view; the mobility view, a ladder of
  five income fifths at the window's start and at the crest joined by streams: a Sankey chart), the person views
  (`HillPersonLayer.cs`: atom, ladder; `MindDiagram.cs`), the lives model (`Assets/Scripts/Economy/Model/LivesModel*.cs`,
  `Docs/data/lives.md`: the 2005-2025 middle fifth's typical climber, stayer and faller, base rates, the approved real
  lives Musk, Trump and Oprah Winfrey) and spec M3 (the lives corridor). `Tools/history/sources.json` holds unshipped
  mobility research (Chetty's Charlotte and San Jose, Denmark, Canada, absolute mobility by cohort).
- Collective action today: `HillDilemmaLayer.cs` (an iterated prisoner's dilemma arena: YOU against SPOUSE, COWORKER,
  OTHER TRIBE, a payoff ledger, tit for tat), the encounters views society and betrayal (households' groups meeting
  again and again; `EconomyViews.cs`, LandGroup.Coalitions, the cooperation and betrayal models under
  `Assets/Scripts/Economy/`), `Docs/hills-design/THEORY.md`, and real dated data already in the repository (the PIIE
  US-China tariff series, the gated world events in global.json, games.json).
- Images today: none in the economy. The universe has procedural icons (`Assets/Scripts/Core/Icons.cs`, IconRaster,
  `LabelSpec.Icon` drawn as SpriteRenderers on Sprites/Default), and `Tools/social/wikidata.py` already maps the
  attention cloud's accounts to Wikidata (cached HTTP, a User-Agent with the repository URL, Jev choosing between
  candidates).
- Jev (TypeSafe System One) is available through `Tools/jev.py` for semantic judgments (choose among candidates,
  verify that a quote supports a claim); read `.agents/skills/typesafe-ai/SKILL.md` and `Docs/jev-setup.md` before
  using it. Its key is found by the tool and must never be printed, copied or written to a file.

## Constraints every design must keep

- Truth: every number and claim on screen comes from data with a source. Anything dated 2026 is drawn only when its
  quote was found on the fetched source and Jev supports it (the project's 2026 gate: plan decision 17, spec 6.8,
  `Tools/global/verify.py`). Real-world examples carry their source and date; disputed numbers are marked as such.
- Images: free licences only (CC0, public domain, CC BY, CC BY-SA), each with author, licence, licence URL and source
  URL in a manifest, and attribution reachable in the app. Real individuals only when they are public figures with a
  Wikidata entry (politicians, executives, celebrities, the approved real lives); never a private person; never a
  photo of a real person standing in for a synthetic or typical life (those get generic figures, e.g. OpenMoji or
  Twemoji people, whose licences are free). Logos only when free (e.g. PD-textlogo on Commons); otherwise another free
  image of the company or a monogram tile. Flags and coats of arms are usually public domain.
- Performance: the Quest build too. Thumbnails small (128-256 px), compressed, few draw calls (an atlas), nothing
  allocated per frame, the replay's year swap under 50 ms.
- The universe scene stays as it is. No narration audio is produced (the user has not approved voicing).
- The plan's packages and decisions stand unless your design says exactly what it changes and why.

## Rules for agents

- Do not run Unity, a build or the player, and run no git command that changes anything. Design agents edit no file in
  the repository; research agents write only the files their prompt names. Scratch files go under
  `/home/john/Desktop/Unity/.why-2-build/work/holistic/<your role>/` (durable; the session's /tmp scratchpad was wiped
  once).
- Web research: prefer primary sources (the study, the agency, the treaty text); record the URL, the exact quote that
  carries each number and the access date; never invent a number. Use WebSearch and WebFetch (load them with
  ToolSearch), or python3 urllib with the User-Agent `why-2-holistic/0.1 (https://github.com/atonalfreerider/why-2)`.
  Send no personal data to any service.

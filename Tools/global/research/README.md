# World economy research (copied 3 Nov 2026)

The research behind the world view (Docs/hills-design/MODALITIES.md §6, work package WP04), copied unchanged from the
build mirror's untracked folder `~/Desktop/Unity/.why-2-build/research/global`, which a crash had already emptied once.
Nothing in these files is drawn directly: `Tools/global/build.py` (WP04) refetches the sources and writes
`Assets/Resources/Data/economy/global.json`.

| File | What it is | sha256 |
| --- | --- | --- |
| BRIEF.md | The data brief: the 13 economies, recipes R1-R9 for each measure, sources, caveats | 68667fc4e421edce2862aade76c86053c41639f677065292a9d32ccc97f07129 |
| dataset.json | The brief's compact dataset (2024 base): measures, trade matrix and mirrors, US imports, tariffs, 26 events, straits, 41 sources (copied from `dataset.json.txt`) | 245c1f94260c4935043eeda7a871c50f34651fc27c4c506c13ea5e79b5559fb0 |
| domains.json | The five research domains' full findings (size and structure, trade, financial ties, chokepoints, conflict and coercion), each with its sources | d156ae0ce00134b2100b48c7d2c785ad3121464485df1e02db6ceacb46428829 |
| verify-events.json | The citation check of the 76 dated events (3 Nov 2026 11:13): 54 pass, 22 failed | 7a300b548afcf26d0d3a5852925b157ad8301c7e466f1a301308413c0f7486c0 |
| verify-events.py | The script that wrote verify-events.json | |

**The check's rule.** An event passes only if a quoted passage is found verbatim on the fetched source and Jev (an AI
model) judges that the page supports the claim (0.6 or more). Jev's score is evidence, not proof. verify-events.json is
the starting state of the 2026 gate (MODALITIES.md §6.8), not a substitute for it: every 2026-dated value is re-verified
by `Tools/global/verify.py` before it is drawn.

Check the copies against these sums:

    cd Tools/global/research && sha256sum BRIEF.md dataset.json domains.json verify-events.json

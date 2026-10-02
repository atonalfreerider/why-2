# The hill landscape game: design and work in progress

**Current implementation:** see [IMPLEMENTATION.md](IMPLEMENTATION.md). The October 2 user review replaces the
stepped benches and literal crowns below with connected terrain, substantial vertical scale, and luminous ownership
rings. The landscape is now active. Do not apply `wp0-wip.patch` on top of it; that patch is retained as an archive.

This folder holds the design of the economy scene's second redesign and the work done so far. In that redesign, the
circular bowl is kept but disabled and replaced by a landscape of hills.

* `BRIEF.md`: the request, verbatim, and the requirements read from it.
* `SPEC2.md`: the specification the build follows: terrain, people and wealth, flows, the season, the mind program,
  the 3D mind view, couples and births on the population graph, the cut, views, engineering contracts, the build plan
  (section 12) and acceptance (section 13).
* `parts/`: the four part designs SPEC2 was merged from. They are background only; SPEC2 wins where they differ.
* `prototypes/`: the Python reference prototypes whose numbers SPEC2 quotes.
* `BUILD2.md`, `hills-wave.js`: the build rules and the workflow for the parallel work packages.
* `checks/`: the start of the harness checker (`hillcheck.py`). It is incomplete.
* `wp0-wip.patch`: work package WP0 in progress, against commit `16262b8`, made with `git format-patch`. It retires
  the bowl to `Assets/Scripts/Economy/Bowl/` under the scene id `economy-bowl` and adds the first contracts. It
  compiles, but on its own it leaves the economy scene's land empty, so it is not applied on this branch. Apply it
  with `git am Docs/hills-design/wp0-wip.patch`.

The paths in these files refer to `$SP`, the scratch folder of the session that wrote them. Read `$SP/redesign2/...`
as this folder.

**Status:** the design is done. WP0 is partly built: the bowl is retired and the first contracts are in. Still to
build in WP0: the pipeline, the model stubs, the 18 views and the checks. WP1-WP6 have not started.

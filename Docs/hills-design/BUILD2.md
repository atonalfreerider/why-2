# Building the hill landscape: rules for every work package

`SP=/tmp/claude-0/-home-user-why-2/dc9bf553-91de-54d2-9f1f-9831e1f24cfa/scratchpad`

## What to read

1. `$SP/redesign2/SPEC2.md`: the specification and the authority. Section 12 names your package, the files you own,
   its content and its "Done when"; 11.1 lists every file and its owner; 11.2 holds the contracts (frozen once WP0
   merges); 11.3 the pipeline; 11.6 the budgets; 11.7 determinism; 11.8 the harness commands, the exact log lines with
   their tolerances and the checks (`hillcheck.py` and its modules); 10 the views, poses, labels and legibility rules;
   13 the acceptance list. The sections your package "builds" are the design you implement. Appendix A holds the
   decisions between the part designs; Appendix B the prototypes (`$SP/redesign2/synth/*.py`, `$SP/redesign2/*/proto*.py`)
   whose numbers the C# must reproduce within the tolerances of 11.8.
2. `$SP/redesign2/BRIEF.md`: the user's words (verbatim) and the requirements read from them. When a choice is not
   settled by the spec, pick what the user's words ask for.
3. The part designs (`$SP/redesign2/part-*.md`) only as background for derivations; SPEC2 wins where they differ.
4. The code you touch and its neighbors. Write code that reads like the surrounding code: its doc-comment density
   (a `<summary>` on every type and non-trivial member, saying what and why), naming, C# 9, no LINQ in per-frame or
   per-vertex paths, `CultureInfo.InvariantCulture` for numbers in text, the existing builders (`LineMeshBuilder`,
   `SurfaceMeshBuilder`), materials (`GraphMaterials`, `.Raw`), labels / anchors / highlight-id conventions. Code under
   `Economy/Land` and `Economy/Layers` never references `Economy/UI`.

## Where you work

- Only in your worktree (given in your task), on its branch. Never edit `/home/user/why-2` itself or another
  package's worktree. Commit your work in your worktree (as many commits as you like); never push, never merge,
  never rebase onto anything (the lead merges and rebases).
- Every commit message ends with exactly these two lines (and contains no model name):

  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01VM9nbYe1AHvdH9DXoTJnbj
  ```
- New `.cs` / `.json` files and folders need `.meta` files: `python3 $SP/gen-metas.py <worktree>`. Moved files keep
  their `.meta` (`git mv` both).
- Touch only the files your package owns (SPEC2 11.1 and 12). A frozen contract (11.2) may not change signature. If you
  truly must change a file you do not own or a contract, make the smallest additive change and list it under
  `deviations` in your result with the reason (the lead carries it into the integration branch).
- **The causality scene (`Why.unity`) must not change**: no file under `Assets/Scripts/Core/`, `Axis/`, `UI/`, `Lens/`,
  `Director/` (but WP6's `TourData.EconomyBuiltIn`), `Matter/`, `Life/`, `Domination/`, `Figures/`, `Assets/Scenes/`;
  the four SMV files only additively and only behind the economy's option (8.2). `whycheck` proves it.
- Every line printed by a stub carries "(demo)". A check whose expected value comes from another package is INFO until
  that package is in `--expect` (11.8).

## Tools (private copies: never use `$SP/cc` or `$SP/run` directly)

```
export WHY_NOW=2026-10-01T12:00:00Z          # ALWAYS: pins the harness clock, so renders are reproducible
$SP/agent-tools.sh <name> <worktree>         # once: makes $SP/cc-<name> and $SP/run-<name>
$SP/cc-<name>/check.sh <worktree>            # COMPILE OK (all of Assets/Scripts, Economy/UI included)
$SP/datacheck/check.sh <worktree>            # economy data loader warnings (no new ones)
$SP/whycheck.sh <worktree> <name>            # the causality scene, pixel for pixel: must print PASS
# the harness runs of SPEC2 11.8 (land, portrait, stairs, rise, phase, mind-ow, play, lives0, nobonds), e.g.
OUT=$SP/redesign2/build/<name>; mkdir -p $OUT
WHY_ANCHORS_OUT=$OUT/anchors.json WHY_DUMP_LAND=1 WHY_REPO=<worktree> $SP/run-<name>/run.sh --scene economy --render all --out $OUT/land > $OUT/land.log 2>&1
WHY_REPO=<worktree> $SP/run-<name>/run.sh --scene economy --size 1080x1920 --render all --out $OUT/portrait > $OUT/portrait.log 2>&1
python3 $SP/redesign2/checks/hillcheck.py $OUT/land.log $OUT --expect <packages merged + yours>
```

The harness (`$SP/run/README.md`) compiles the repo's Core, Humans, Axis and Economy (except `Economy/UI`) into a
console app with a managed UnityEngine shim, builds the scene's layers like `GraphRoot`, ticks presets and renders
PNGs (`<preset>.png` with labels, `.base.png` without, `.labels.json`) plus the full log. Read the PNGs (you can see
images) to judge what you built against the user's words. Scratch files go under `$SP/redesign2/build/<name>/`.
The machine has 4 CPUs and another agent may be building at the same time: run one harness at a time, never in
parallel with yourself.

## Done means

- `COMPILE OK`; the economy harness renders every preset at 16:9 and 9:16 with no exception or warning you caused;
  `whycheck` PASS; no new datacheck warnings; your package's "Done when" (SPEC2 12) and its log lines within the
  tolerances of 11.8 (`hillcheck.py --expect <...>`); your renders showing what the spec asks.
- Everything committed in your worktree.
- Your result lists: the commit, what you built, each check with its actual output, every deviation from the spec
  with the reason, and open issues for the integrator. Be exact and honest: a failing check is reported as failing.

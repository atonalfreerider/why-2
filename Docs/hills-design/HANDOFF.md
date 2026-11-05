# Handoff: objects first, holistic views and images of the actors

Written 5 Nov 2026 so that this session, or a new one, can carry the work on after a usage limit. Read it with
`MODALITIES.md` (the spec), `MODALITIES-PLAN.md` (39 work packages, decisions 1-36, the section "Carried into the
packages") and `AGENTS.md`.

## Where things stand

- **main is checkpointed and pushed.** Built and verified in the mirror:
  - G1-G3 (WP01-WP12), the polish rounds 1-3b, and G3b: the emphasis table and the label system for the hills
    (1c7b850; plan update 124c310).
  - The dilemma fix (39e68f2): the slip's verdict waits for the reply, each party's ring shows its own cooperation, and an
    independent is no longer drawn as a Democrat. The season model's numbers and checksums are unchanged.
- **No unmerged work in git.** Every local and remote branch is merged into main. The three worktrees under
  `.claude/worktrees/` are merged too.
- **The research so far is committed** in `Docs/research/holistic/`:
  - `mobility.md` and `mobility.json`: 37 sourced facts, each with its exact quote and URL. Quotes were checked word for
    word against fetched copies; Jev supports all 31 claims taken from prose.
  - `maps/views.md`: how mobility and collective action are shown today.
  - `maps/objects.md`: what the economy can draw, the identifiers held, an image probe (60 most-followed people, 14 presidents, 6 lives candidates, 16 flags, 25 firms), the Quest budget.
  - `maps/lines-draft.md`: the line-clutter audit, view by view, as a draft.
  - `workflow/`: the design brief and the workflow script.
  - `Tools/images/fetch.py` is the draft image fetcher. The map found three gaps in it, and the workflow's
    research:images step replaces it.
- **Nothing of the new direction is built yet.** The amendment to the spec and plan is being designed (below).

## The user's direction (4 Nov 2026, verbatim)

> there is too much line clutter in the economy view. we should have object emphasis and only use line emphasis when
> illustrating a point. faded and bloomed lines can still be present, but used sparingly
>
> I still want you to break from traditional graph views and create holistic view states to show economic mobility and
> collective action game theory with real world examples
>
> overall we need more image data to illustrate individuals, collectives (companies, governments, groups). pull these
> thumbnail images from open sources

Standing instructions and approvals:

- **Git.** Full git control of main: commit and push as the work lands.
- **Real lives.** Elon Musk, Donald Trump and Oprah Winfrey are approved (3 Nov). Any other real person is a proposal
  until the user approves it.
- **Narration.** Not approved: produce no narration audio and make no ElevenLabs call.
- **Workflows.** The user opted into multi-agent workflows (ultracode) for this work.

## In flight: the design workflow

One agent at a time, stopping at the first failure so that a resume continues from it. Each agent takes 30-70 minutes,
so the remaining steps take 5-9 hours, and a usage limit will probably stop the run again.

| Step | What it does | Output | State on 5 Nov, 09:35 |
| --- | --- | --- | --- |
| research:mobility | mobility evidence and public figures | `Docs/research/holistic/mobility.md`, `.json` | done |
| research:collective | 8 real collective-action cases (ozone, cod and lobsters, OPEC+, SVB, UAW, debt ceiling, global minimum tax, US-China tariffs) | `Docs/research/holistic/collective.md`, `.json` | running, resumed: the first attempt died at the limit after caching its sources (`work/holistic/research-collective/cache/`) |
| research:images | the open-image pipeline, prototype run on 12 actors | `Tools/images/fetch.py`, `Docs/research/holistic/images.md`, samples in `work/holistic/images-sample/` | to do |
| map:lines | finish the line audit against the emphasis table | `work/holistic/map/lines.md` | to do (the draft is 22 of 22 views) |
| design:objects | objects-first emphasis, two variants, one recommended | `work/holistic/designs/objects.md` | to do |
| design:mobility | the holistic mobility view state | `work/holistic/designs/mobility.md` | to do |
| design:collective | the holistic collective-action view states with the real cases | `work/holistic/designs/collective.md` | to do |
| synthesize, critic, revise | the amendment | `work/holistic/amendment/{spec,plan,decisions,summary}.md` | to do |

`work/` is `/home/john/Desktop/Unity/.why-2-build/work/`. It is durable, unlike the session's /tmp scratchpad, which
has been wiped twice.

**To continue in the same session** (after a usage limit), resume the run. Finished agents replay from the cache; the
one that died restarts and reuses what it downloaded.

    Workflow({scriptPath: "/home/john/.claude/projects/-home-john-Desktop-Unity-why-2/15d4882d-8d0a-4d8f-9b16-59ae77aa5db4/workflows/scripts/holistic-views-design-lean-wf_27fe0109-5db.js",
              resumeFromRunId: "wf_27fe0109-5db"})

**To continue from a new session**, the run cannot be resumed, so start the committed copy and skip the finished steps:

    Workflow({scriptPath: "/home/john/Desktop/Unity/why-2/Docs/research/holistic/workflow/design-workflow.js",
              args: {skip: ["research:mobility", ...every other finished step]}})

- **Which steps finished.** Check the outputs in the table, or the run's journal: one result line per finished agent
  in `/home/john/.claude/projects/-home-john-Desktop-Unity-why-2/15d4882d-8d0a-4d8f-9b16-59ae77aa5db4/subagents/workflows/wf_27fe0109-5db/journal.jsonl`.
- **A half-written output.** A step whose output exists but stopped short (no summary section, no sources) runs again.
  Its prompt tells it to reuse what is there.

## After the design: the lead's steps

1. **Check the amendment.** Read `amendment/summary.md`, the critic's gaps and the revision. Check every number in the
   amendment against the research, by its quote.
2. **Merge it.** Put the new sections into `MODALITIES.md`, and the new and changed packages and decisions (numbered
   from 37) into `MODALITIES-PLAN.md`. Copy the final maps, designs and research into `Docs/research/holistic/`, with
   `lines.md` replacing `maps/lines-draft.md`. Commit and push.
3. **Show the user.** Send `summary.md` to the user (SendUserFile) and ask the open questions below.
4. **Build in this order**, unless the amendment orders it otherwise. Agents may edit files; only the lead builds and runs
   the gates below.
   1. *The image pipeline* (it can start at once). Thumbnails and their manifest under Assets, an atlas, tokens with round
      or rounded-square masks, and attribution reachable in the app.
   2. *Objects first.* The line budget through the emphasis table (WP14's retune), measured by the smoke test.
   3. *The holistic mobility scene*, then *the collective-action scenes* with the real cases.
   4. *G4* (WP13's legend and scale bar), adapted to the new direction.

## Open questions for the user

These come from the research and the map. Where a default is given, the lead can proceed with it and say so.

1. **More real public figures?** The mobility research proposes:
   - rose: Jay-Z (Q62766), Andrew Carnegie (Q484265);
   - held: Warren Buffett (Q47213);
   - fell: William C. Durant (Q381381), who lost his fortune in the market, and Elizabeth Holmes (Q17503525) and Sam
     Bankman-Fried (Q106543540), who fell through fraud.

   Shown alone, the fraud cases would suggest that falling means crime, so Durant is the safer "fell". Without approval,
   use only the three approved lives and generic figures.
2. **Holistic scenes: on the planned stage (WP19) or in place?** Either they live on the stage, or they replace the
   mobility, society, betrayal, dilemma and ladder views where they stand. The designs recommend one; the user may decide.
3. **The real lives in the mobility scene?** Musk, Trump and Winfrey were billionaires throughout the window. They could
   stand in the mobility scene, or only in M3's money-and-visibility comparison.
4. **Data choices (defaults given):**
   - *The Great Gatsby curve:* one source for both axes, the OECD's (it gives inequality too); never mix it with Corak's.
   - *The decade's mobility:* the Treasury's 1996-2005 tax panel (all taxpayers, already used to check the mobility
     view), not the Cleveland Fed's male-headed households.
   - *Durant's 1936 assets:* "a few hundred dollars of clothing", with the two newspapers' dispute in the source note.
   - *The 2026 Forbes values:* re-read on the build's as-of date through the 2026 gate, falling back to 2025.
   - *SHED's 2025 survey:* published in May 2026, so it goes through the gate.
   - *The PIIE US-China tariff series as the anchor iterated game:* it measures statutory rates on fixed 2017 trade, not
     duties paid, and the scene says so. Its 2026 rows go through the gate.

## Building and verifying

- **Where to build.** Only in the mirror `~/Desktop/Unity/.why-2-build`, never in the main checkout, where the user's
  editor may be open.
  - *Compile check first* (3 s): `python3 Tools/compile-check.py --refs-from ~/Desktop/Unity/.why-2-build`.
  - *Build:* `work/mirror-build.sh [--dry-run]`. It rsyncs Assets, Packages, ProjectSettings, Tools and Docs by content,
    keeps the mirror's three reserialized assets and skips StreamingAssets, then builds the Linux player (about a minute
    incremental). Judge success by the line `[Why] desktop build Succeeded`, because Unity can segfault on exit.
- **Smoke test.** `EXTRA="-smoketour -smokereplay" work/shots.sh TAG [VIEWS|all] [WxH]` writes frames to `Shots/TAG`,
  and the run must end with 0 errors. No pointer is present by default; `-smokepointer X,Y` parks one.
- **Pixel gate.**
  - *Tool:* `work/pixdiff.py A B` (ffmpeg difference, 8/255 threshold).
  - *Rule:* a frame fails when its change exceeds the noise between two baseline runs plus 0.5%. Judge it by the smaller
    of its differences from the two baselines. Replay frames carry sub-pixel timing noise, up to about 5.8%.
  - *Baselines:* `Shots/base1` and `base2` predate G3b. Make two fresh baselines at the current HEAD before the next gate.
- **Resources.**
  - One Unity process at a time across all sessions, and only with `free -g` showing at least 4 GB available. Detect one
    with `pgrep -x 'Unity Main Thre'`, never with a plain `pgrep -f`, which matches its own shell.
  - Keep screenshots out of /tmp, which has a quota, and delete them after viewing.
  - New .cs and .json files under Assets need .meta files.

## Rules that hold throughout

- **Credentials.**
  - Jev only through `Tools/jev.py` (or `Tools/jev.ps1`). Check that the key is available without printing it.
  - No credential in any file, console output or commit. The user's email address goes to no service.
- **Truth.** Every number on screen has a source. Anything dated 2026 is drawn only through the 2026 gate: the quote is
  found on the fetched source and Jev supports it (decision 17, spec 6.8, `Tools/global/verify.py`).
- **Images.**
  - *Licences:* CC0, public domain, CC BY or CC BY-SA, each in a manifest with author, licence, licence URL and source,
    and attribution reachable in the app.
  - *People:* real individuals only when they are public figures with a Wikidata entry. Synthetic and typical lives get
    generic figures (OpenMoji, Twemoji).
  - *Logos:* only free ones, else a monogram tile.
- **Unchanged.** The universe scene stays as it is. Assets/StreamingAssets (the 152 MB social graph DB) is never committed.
- **Workflow agents** run no Unity, no build and no git command that changes anything.
- **Commits** end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Also pending

- **The plan's carried items.** The section "Carried into the packages" holds the label problems left to the layer
  migrations (WP15-WP18, WP23).
- **WP39.** The first-look test with five people, approximated with agents, comes last.
- **Narration voicing** waits for the user's approval.

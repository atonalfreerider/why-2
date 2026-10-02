export const meta = {
  name: 'economy-hills-wave',
  description: 'Build hill-landscape work packages in their own worktrees, independently reviewed against SPEC2, fixed, re-reviewed',
  phases: [
    { title: 'Build', detail: 'one builder per work package in its own git worktree' },
    { title: 'Review', detail: 'an independent reviewer re-runs every check and reads the diff against SPEC2' },
    { title: 'Fix', detail: 'the builder fixes what the review found, then a second review' },
  ],
}

const SP = '/tmp/claude-0/-home-user-why-2/dc9bf553-91de-54d2-9f1f-9831e1f24cfa/scratchpad'
const RULES = `${SP}/redesign2/BUILD2.md`
const SPEC = `${SP}/redesign2/SPEC2.md`

const RESULT = {
  type: 'object',
  properties: {
    commit: { type: 'string' },
    built: { type: 'string', description: 'what was built, per spec item' },
    checks: { type: 'array', items: { type: 'object', properties: { name: { type: 'string' }, pass: { type: 'boolean' }, output: { type: 'string' } }, required: ['name', 'pass', 'output'] } },
    deviations: { type: 'array', items: { type: 'string' } },
    openIssues: { type: 'array', items: { type: 'string' } },
  },
  required: ['commit', 'built', 'checks', 'deviations', 'openIssues'],
}

const REVIEW = {
  type: 'object',
  properties: {
    pass: { type: 'boolean', description: 'true only if the package is complete, correct and every check passes' },
    issues: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          severity: { type: 'string', enum: ['blocker', 'major', 'minor'] },
          file: { type: 'string' }, line: { type: 'integer' },
          problem: { type: 'string' }, evidence: { type: 'string' }, fix: { type: 'string' },
        },
        required: ['severity', 'problem', 'evidence', 'fix'],
      },
    },
    summary: { type: 'string' },
  },
  required: ['pass', 'issues', 'summary'],
}

const packages = args.packages

function resume(p) {
  return `An earlier attempt at this package may have been interrupted: run git -C ${p.worktree} status and git -C ${p.worktree} log ` +
    `--oneline ${p.base}..HEAD first. If the worktree already holds work (committed or not), read it, keep what is right and continue ` +
    `from it instead of starting over.`
}

function buildPrompt(p) {
  const name = p.key.toLowerCase()
  return `You are the engineer of work package ${p.key} of the economy scene's second redesign, the hill landscape game.\n` +
    `Read ${RULES} first and follow it exactly, then ${SPEC}: sections 0 and 1, section 12 "${p.key}" (what you own, your content, ` +
    `your Done when), 11 (files, the contracts you build on, the pipeline, budgets, determinism, the harness and your log lines and ` +
    `checks in 11.8), 10 (views, poses, labels, legibility) and every section your package builds; then the code. WP0 (the contracts, ` +
    `the stubs, the pipeline, the 18 presets, the checks in ${SP}/redesign2/checks/) is merged into your base.\n\n` +
    `Worktree: ${p.worktree} (branch ${p.branch}, base ${p.base}). Tools name: ${name} (run ${SP}/agent-tools.sh ${name} ${p.worktree} ` +
    `first). Harness output: ${SP}/redesign2/build/${name}/. Your checks: hillcheck.py --expect ${p.expect}.\n\n${p.scope}\n\n` +
    `${resume(p)}\n\nImplement the whole package to the spec, replacing WP0's stubs you own with the real thing; verify it with the ` +
    `tools; LOOK at your renders at 16:9 and 9:16 and judge them against the user's words in ${SP}/redesign2/BRIEF.md; fix what is ` +
    `wrong; commit. Return the result: commit hash, what you built, every check with its real output, deviations, open issues.`
}

function reviewPrompt(p, built, round) {
  const name = `rv${p.key.toLowerCase()}${round}`
  return `You are an independent reviewer of work package ${p.key} of the economy scene's second redesign, the hill landscape game ` +
    `(review round ${round}). Read ${RULES} and ${SPEC} (sections 0, 1, 12 "${p.key}", 11, 10, 13 and every section the package ` +
    `builds) and the user's words in ${SP}/redesign2/BRIEF.md.\n` +
    `The package is in worktree ${p.worktree} (branch ${p.branch}); its diff: git -C ${p.worktree} diff ${p.base}..HEAD. ` +
    `Do NOT edit its files or commit. Make your own private tools: ${SP}/agent-tools.sh ${name} ${p.worktree}, and write scratch ` +
    `output under ${SP}/redesign2/build/review-${name}/.\n\n${p.scope}\n\n` +
    `The builder reported (JSON):\n${JSON.stringify(built, null, 2)}\n\n` +
    `Verify independently: re-run the compile check, the harness runs of 11.8 the package needs (16:9 and 9:16, WHY_NOW set), ` +
    `hillcheck --expect ${p.expect}, whycheck, datacheck; compare every log line with the spec's expected values and tolerances; ` +
    `look at the renders and judge them against the user's words; read the whole diff against the spec (missing items, wrong ` +
    `algorithms or constants, contract signatures changed, files touched outside the package's ownership (11.1), thread-safety in ` +
    `Prepare, per-frame allocations, determinism, code that does not read like the codebase, missing doc comments, missing .meta ` +
    `files). Report every real problem with evidence and a concrete fix. pass = true only if nothing of severity blocker or major remains.`
}

function fixPrompt(p, review) {
  return buildPrompt(p) + `\n\nA review of your committed work found the problems below. Fix every blocker and major issue, and the ` +
    `minor ones that are cheap; re-run the checks; commit; return the updated result.\n\nReview (JSON):\n` + JSON.stringify(review, null, 2)
}

const results = await pipeline(
  packages,
  p => agent(buildPrompt(p), { label: `build:${p.key}`, phase: 'Build', schema: RESULT, effort: 'high' }),
  (built, p) => agent(reviewPrompt(p, built, 1), { label: `review:${p.key}`, phase: 'Review', schema: REVIEW, effort: 'high' })
    .then(review => ({ built, review })),
  async (state, p) => {
    if (!state.review || state.review.pass) return { key: p.key, ...state, final: state.review }
    const fixed = await agent(fixPrompt(p, state.review), { label: `fix:${p.key}`, phase: 'Fix', schema: RESULT, effort: 'high' })
    const review2 = await agent(reviewPrompt(p, fixed, 2), { label: `review2:${p.key}`, phase: 'Fix', schema: REVIEW, effort: 'high' })
    return { key: p.key, built: state.built, review: state.review, fixed, final: review2 }
  },
)

for (const r of results.filter(Boolean)) {
  log(`${r.key}: ${r.final ? (r.final.pass ? 'PASS' : `${r.final.issues.length} issues remain`) : 'no review'}`)
}
return results

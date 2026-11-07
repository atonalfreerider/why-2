export const meta = {
  name: 'advantage-research',
  description: 'Competitive-advantage research: six sourced clusters (2026 compute, labs, economy; lineage; century; archetypes) and a code map',
  phases: [
    { title: 'Map', detail: 'read-only map of the present view code' },
    { title: 'Research', detail: 'six clusters, each gated by Tools/advantage/verify.py' },
  ],
}

const REPO = '/home/john/Desktop/Unity/why-2'
const RES = `${REPO}/Docs/research/advantage`
const OUT = {
  type: 'object',
  properties: {
    files: { type: 'array', items: { type: 'string' } },
    verified: { type: 'integer' },
    total: { type: 'integer' },
    summary: { type: 'string' },
    open_questions: { type: 'array', items: { type: 'string' } },
  },
  required: ['files', 'verified', 'total', 'summary', 'open_questions'],
}
const skip = new Set((args && args.skip) || [])

const MAP = `You map the code that the competitive-advantage redesign of why-2's economy view will change. Work read-only: do not edit any file except your map, and do not run Unity.

Read ${RES}/brief.md (the section "What the research is for" says what will be built), then ${REPO}/Docs/hills-design/HANDOFF.md (its sections on the exploded present and PresentPeaks). Then read the code under ${REPO}/Assets/Scripts/Economy: Land/PresentLayout.cs, Land/PresentPeaks.cs, Layers/HillPresentLayer.cs, HillPresets.cs (find it), Emphasis/EmphasisTable.cs; the loaders in Data/ and Model/ that read Resources/Data/economy/*.json (industries.json, company-lives.json); the class that draws the rivers of desire and fear; how the wave (the causality timeline) draws company lifelines from company-lives.json and how to get a company's position at a given year; the label system; the hover card and panel UI of the present view (UI/); the key binding of the present view (key I); the smoke-test hooks (-smokeextra present, Assets/Scripts/Core/SmokeTest.cs); HillRebuilds and LayerClock; any Quest performance budget notes.

Write ${RES}/maps/code.md, concrete and short (300 to 500 lines), with file:line references:
1. How the present view is built, step by step: entry point, data, layout, meshes, materials, labels, camera poses.
2. The coordinate frames and units of the present cross-section: where the 2026 crest is, what x, y and z mean, the scale, how the camera reaches it.
3. Reusable pieces for: objects sized by a value (tokens, peaks), ribbons or rivers with animated flow, glowing sparks and bloom, labels and hover cards, a toggle UI for scenarios, and a marker on the wave at a year between two company lifelines.
4. Recommended hook points for a new "advantage" layer that replaces or sits beside PresentPeaks without breaking the other views; how a new data file under Resources/Data/economy is loaded; what the smoke test checks for the present view.
5. Risks: performance (meshes, draw calls, the Quest), label collisions, the emphasis rules (objects first: lines only to make a point).
Write the file as soon as you have a first version, then complete it.`

const research = (cluster, extra) => `You are a researcher for why-2's competitive-advantage redesign. Your cluster is \`${cluster}\`.

Read ${RES}/brief.md in full first: it holds the user's request verbatim, how to think, the rules, the tools, the output schema, the canonical actor ids and your cluster's scope (the table at the end). Work from the repository root ${REPO}. Write ${RES}/${cluster}.json and ${RES}/${cluster}.md, as early as you can and then extend them.
${extra}
Run the gate on your cluster (python3 -I Tools/advantage/verify.py --cluster ${cluster}) after your first ten facts and again at the end; fix or remove what fails. Only verified facts may carry numbers into actors, flows and flashpoints; every flow and flashpoint lists the fact ids it rests on. Return the files you wrote, the final verified and total fact counts from the gate, a summary of your findings (the main advantages, choke points or patterns, and what a strategist would predict), and open questions.`

const STEPS = [
  { key: 'map:code', phase: 'Map', prompt: MAP },
  { key: 'research:compute', phase: 'Research', prompt: research('compute', 'Today is 7 November 2026. Fetch the latest reported quarters (NVIDIA, AMD, Broadcom, TSMC, ASML, SK Hynix, Micron) and today\'s market caps. Give special care to the choke points: CUDA and NVIDIA\'s share of AI accelerators, TSMC\'s share of leading-edge foundry and CoWoS capacity, ASML\'s EUV monopoly, HBM shares; and to the flows: hyperscaler spending into NVIDIA (customer concentration), NVIDIA into TSMC and the HBM makers.') },
  { key: 'research:labs', phase: 'Research', prompt: research('labs', 'Today is 7 November 2026. Get the latest valuations and revenue run-rates of OpenAI, Anthropic and xAI, the 2026 capex guidance of Microsoft, Alphabet, Amazon, Meta and Oracle, and the large compute contracts and investments between them (OpenAI with Microsoft, Oracle, CoreWeave, AMD, NVIDIA and Broadcom; Anthropic with Amazon, Google, Microsoft and NVIDIA; Stargate, SoftBank). Record the executives\' own words on fear and desire (for example on the risk of underinvesting against overinvesting) as statement facts. Talent: pay packages, defections, the founding of Anthropic by people from OpenAI.') },
  { key: 'research:economy', phase: 'Research', prompt: research('economy', 'Today is 7 November 2026. Build the scoreboard of the 25 largest companies by market cap today from one fetchable source, then the advantages, fears and flashpoints of the most important leaders outside the AI chain, and the 2026 environment. Say how each connects to the AI chain.') },
  { key: 'research:lineage', phase: 'Research', prompt: research('lineage', 'This is history (dated2026 false except NVIDIA\'s present position). Make the lineage array the spine of your file: every link from the transistor to the 2026 AI economy, each with year, how the advantage passed, and facts. Prefer primary or near-primary sources (the Computer History Museum, Nobel Prize pages, NVIDIA\'s own history, the original papers on arXiv) and Wikipedia sentences for the rest.') },
  { key: 'research:century', phase: 'Research', prompt: research('century', 'This is history (dated2026 false). Walk back from the 2026 winners: for each, the predecessor it displaced and the advantage that did it; then the great 20th-century winners and how each lost its lead. Include numbers where sources give them (market share, revenue, rank, years). Extract the repeating patterns of decline as a short list in your Markdown.') },
  { key: 'research:archetypes', phase: 'Research', prompt: research('archetypes', 'This is history and biology (dated2026 false). Prefer encyclopaedic and academic sources (Britannica, Wikipedia sentences, university and museum pages, papers). Record the user\'s premises accurately and gently correct them where the sources disagree. Give each archetype its balance-restoring counterforce (the coalition, the extinction, the predator-prey cycle).') },
]

const results = await parallel(STEPS.filter(s => !skip.has(s.key)).map(s => async () => {
  const r = await agent(s.prompt, { label: s.key, phase: s.phase, schema: OUT, model: 'sonnet' })
  if (r) log(`${s.key}: ${r.verified}/${r.total} verified; ${r.files.join(', ')}`)
  else log(`${s.key}: no result`)
  return r ? { key: s.key, ...r } : null
}))
return results.filter(Boolean)

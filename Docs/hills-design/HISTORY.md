# Competitive advantage through history: timelines that explode into cross-sections

The user's sixth direction (7 Nov 2026) is verbatim in `Docs/research/advantage/brief-v4.md`. This design carries the
advantage view's language back through history, for states, armies, navies, populations, faiths, alliances and
companies alike:

- point-cloud bodies;
- a boundary outline;
- occlusion;
- advantage walls built from their sources.

## 1. The axis of time

The AI story (1945-2026) runs on the economy's own time axis, `HillLandscape.Z(year)`, so its lanes float above the
wave's ribs of the same years (the slope of 1940-41 before the table starts). The age of revolutions (1775-1815) lies
before the wave on a stretch of its own: 7 units a year, ending 140 units before the wave's first year
(`HillHistoryLayer.T`). At the wave's two units a year its forty years would be a sliver beside a stack of 23 lanes.
The gap between the stories is not drawn.

## 2. Two views of every shape

- **Compressed: a timeline.** Each actor is a tube along its lane, a ring of points every 1.3 units of time, as thick
  as the cube root of what it measured then (`TubeR`). Where nothing was measured the tube is thin and faint.
  - **What it measured** (`History.Actor.MainPartAt`):
    - people for a state or a population;
    - troops for an army, or its force at a battle within a quarter-year before;
    - ships of the line for a navy;
    - members for a church;
    - market value, revenue or capital for a company;
    - valuation, users or (in its tube only) parameters for a lab.
  - **Per-actor exceptions.** The crown of France is drawn by its money, its people being the nation's own actor
    (`history.py` `ORDERS`). A state whose own forces stand as their own actors is not drawn by what they keep
    (`FORCES`): its standing army keeps its soldiers, its standing navy its ships and sailors, and nothing else (while
    only the Republic's navy stands, the levy of 300,000 is still the Republic's). A product's property (a model's
    compute, a chip's transistors) never sizes a body.
  - **When a value stands** (`History.Actor.SizeAt`, `Standing`). A value counts from its own date:
    - `history.py` keeps months and days; rows whose facts say "at the end of 1999" or "in November 2026" are dated so;
    - a year-only value counts for its whole calendar year if it is a standing quantity, and from the year's end if it
      is an event.
  - **How long it holds:** half a year for an event (a battle's force or fleet, a loss, the Terror's sentences, a year's
    spending), three years for an army or a navy, five for money, eight for a people's count or a debt.
  - **Before its date:** only a state's or a people's population or GDP may stand, up to three years before it was
    counted, and never before its own scope began: a row may say from when it holds (Prussia's 1816 count, of the
    kingdom in the borders drawn on 9 June 1815, stands from that day, not at Leipzig).
  - **A row's own hold:** a row may hold for less than its part (Britain's 1793 debt for three years, before the war's
    loans more than doubled it).
  - **Never between two:** no line is drawn between measurements. Most series are snapshots of different things
    (Austerlitz's army and Jena's), not one quantity sampled.
- **Lanes.** Groups top to bottom: polities, alliances, forces, peoples and churches, companies, labs.
  - A polity, an alliance or a force continues in its successor in one lane: the Kingdom of France, then the
    Republic, the Consulate and the Empire. The predecessor ends where the successor begins.
  - The coalitions follow one another on a lane where they do not overlap.
  - A company's heir is another company, so it gets its own lane, right below its parent, with a dotted arc from the
    parent where it began: Shockley's lab, Fairchild, Intel.
- **Moments** are sparks on the holder's lane, coloured by what decided them (information, execution, environment),
  with dashed ties to the rivals' lanes and gold ties to the allies'. Each is titled under its spark.
- **Periods** are dashed brackets above the lanes, each on the lowest level free over its years.
- **The year** is a gold cursor through the lanes, named under them.
- **Exploded: a cross-section at an instant.** At a year the tubes' slices swell into occluding point-cloud bodies
  (`PointBodies`, as in the 2026 strata). The tubes give way to the bodies, and nothing else is drawn: the wave, the
  hill's marks and the attention cloud all step out of the history view.
  - **The contest.** There is a column for the holder of the moment within a year and a half and its allies, one for
    the others, and one for its rivals, each headed. Rows run polities and peoples, then forces, then enterprises, at
    most three bodies to a line.
    - Each body is spaced by its advantage ring's reach.
    - Neighbours' names alternate above and below their bodies, and in the top row the middle column's stand half a
      name further out, so three columns' names at one height do not meet. A name whose place is taken steps further
      out, 18 pixels at a time and four times at most: a name that would wander onto another body is left to its
      card. A name over 32 letters takes two lines. The columns' heads and the moment's title are placed after the names: a head steps down past a name in its
      column's last line, the title up past the top row's names.
    - The camera fits the bodies to three quarters of the screen left free by the panel and the cards, so the names
      that reach past them stay clear of both. A column holds one to three bodies to a line, whichever count lets the
      camera come nearest: a wide contest stacks into the screen's height.
    - A long note of a unit goes on its own line under the number, so a name stays narrow. A note that only restates the
      value ("US$1.145 trillion" beside "1,145 USD bn") is dropped by `history.py`.
    - An advantage stands only while its holder's lane does. A side of the moment whose lane has just ended stands a
      quarter-year more (the Republic at Brumaire).
    - Of two moments on the same date, the selected actor's sets the sides.
    - The participants of that moment, and every holder of an advantage documented for the year, stand even when
      unmeasured: small, grey, "not measured".
  - **Lobes.** The core is the measure the body is drawn by; lobes are its other measures then, each as wide as the cube
    root of its size. What it fields stands above; what it owes, spends away or loses hangs below. The fused body is
    scaled so the volume it encloses is the lobes' total (`Organism`, `trueVolume`).
  - **Advantage rings.** One ring per actor: its latest documented advantage, walled by one arc per source. Each wall is
    as thick as Jev judged it keeps rivals out (`judge.py --only history`, levels in the era's own terms). A source not
    judged, or one Jev was unsure of (confidence under 0.15), is dashed and marked "uncertain". The sources' names
    show for the moment's holder and the selection; the panel lists every advantage.
  - The moment's bursts point from the holder to its rivals.
- **Labels and cards** give the fact's own number and unit with every digit it has, the measure's name where the unit
  does not say it (market value, revenue, GDP, debt), and the value's year when it is not the year shown. An actor's
  portrait card says what it is and what it measured then, from the verified series, not the research's prose.
- **Sizes.** One unit of size draws a lobe 2.2 times its cube root (radius 1.6 to 42; history.py's method):
  - a thousand people under arms, at work or in orders;
  - a hundred thousand civilians;
  - a ship;
  - a billion dollars in the AI story, or a million pounds of the time in 1775-1815 (£1 = 24 livres, assumed);
  - a hundred million parameters;
  - a thousand transistors.

  A soldier therefore draws as a hundred civilians. Sizes compare within a kind of measure; across kinds they are a
  drawing convention, stated on every stop's card.

## 3. The stories

Two narrated walkthroughs, started from the history panel ("Play the story"). Each lives in
`Assets/Resources/Data/Walkthrough/story_ai_*` or `story_revolutions_*` and is generated by
`Tools/advantage/story_scripts.py`.

- **What a stop does.** Its cues set the view through `ViewVariants` names of the form
  `history:<story>:<year>[:x][:sel=<id>][:pop=<id,id>]`. These say the story, the year, whether the cross-section is
  open, the selected actor and the portrait cards. A cue is timed to the words that name its picture, at the voice's
  measured 17 characters a second.
- **Facts.** Every number spoken or shown is a verified fact (`Tools/advantage/verify.py`). The generator stops on a
  missing or unverified one. Each stop's facts go to `Tools/narration-facts.json`, and `Tools/check-narration.py ai` (or
  `revolutions`) has Jev check the narration against its card and facts. All 33 stops pass, twice, since Jev's scores
  move by about 0.1 between runs. Interpretive asides that Jev read as unsupported claims were cut rather than argued
  for.
- **The words** live in `Tools/advantage/story_scripts.json`, edited as data. An independent review checked every card,
  reading and intro against its facts and against what the picture draws: a port of the drawing rules, run over every
  cue, showed what each opened year draws. Where two facts disagree (France's war spending, the Allied army at
  Austerlitz, the invasion of Russia), the card names both counts.
- **Voice.** JohnV4 (ElevenLabs), approved by the user for these stories. The clips go to `Audio/EconomyTour` beside the
  economy tour's (ids prefixed `ai_` and `rev_`).
- **Cards.** A story has its own title card (`intro`) and end card (`end`). The title card already shows the first
  stop's state.

The stories:

1. **The AI revolution** (16 stops): the American century, the transistor, the traitorous eight, the integrated
   circuit, the microprocessor, the PC and the x86, the foundry, the GPU, CUDA, AlexNet, "Attention Is All You Need",
   scaling, ChatGPT, DeepSeek's test, five trillion dollars, the present.
2. **The age of revolutions** (17 stops): the thirteen colonies, Saratoga, the French alliance, the Chesapeake and
   Yorktown, the debt, the Estates-General, the Bastille, the king's execution, the levée en masse, the Terror,
   18 Brumaire, the emperor crowning himself, Trafalgar, Austerlitz and Jena, Europe out of balance and Russia, the
   alliance against France, the Congress of Vienna.

**Portraits and logos.**
- 75 of 81 images are free-licensed, from Wikidata and Commons through `Tools/images/fetch.py`, with the licence
  manifest. They are packed at 256 px into `Resources/Data/economy/images`, and every card carries its credit.
  `Docs/research/advantage/image-credits.md` has the full attributions.
- People get circles, marks get tiles, and a wordmark wider than its tile is shown two cells wide. The people shown were
  approved by the user's message of 7 Nov 2026.
- The crop review of the first contact sheet re-cropped Robert Noyce. Marks of another era gave way: the British Army's
  modern flag, the Armée de Terre logo and the tricolour for the navy of 1781 became monograms, and Spain's flag of 1873
  was replaced by the 1785 one. The coalitions' battle paintings became numerals.
- Three AI researchers (Krizhevsky, Vaswani, Shazeer) have no Wikidata portrait and show monograms.

**Without a tour.** The history view (in the view bar, chapter "History") has a panel with:
- the two stories;
- the previous and next moment;
- open or close the cross-section;
- play the story.

It also shows the reading of the moment at the year: who held the advantage over whom and with whom, how, and what
came of it; the advantages held then with their sources and walls; and the periods the year falls in.

## 4. The people on the hill

The hill's households are a point cloud, one point per 100,000 people. Each group's points stand on the hill that pays
it, from rose at the lowest income rung to gold at the highest; the top 1% are in the capital cloud above
(`HillAdvantageLayer.People`).

## 5. Flows

Time frozen at an instant must still show money and capital moving (the user's seventh direction,
`Docs/research/advantage/brief-v5.md`; the flows map's proposal 6). The history view does this with its flows
(`HillHistoryLayer.Flows`).

**What is drawn.**
- **Compressed.** Each flow is a few rungs between the payer's lane and the payee's: one every four years of its span,
  one for a single year.
  - Each rung stands near the middle of its share of the years, moved by up to a quarter of it on the flow's own hash,
    so Britain's two subsidies of 1813 do not lie on each other.
  - A rung bows a little toward the future (a twelfth of its height, at most 3 units), so it never reads as the year's
    cursor or a moment's tie.
  - A flow from outside the story rises into the payee's lane from below and before. One to outside (the Marshall
    Plan's 17 European countries) rises out of the payer's lane above and after. Both are named where there is room.
  - A flow just past an actor's lane is drawn at the lane's end (Google bought DNNresearch in March 2013; the AlexNet
    team's lane ends in December 2012).
- **Opened at a year.** A river runs between the two bodies for each flow running that year; two rivers between the same
  pair bow to opposite sides.
  - An end with no body that year (outside the story, or not drawn) makes a short river from beside the body that has
    one, pointing away from the picture's middle past its advantage ring, and named ("to Habsburg Austria").
  - A flow neither of whose ends has a body draws nothing.
- **Packets** move from payer to payee on the flow clock, so they keep moving while the year stands still or a tour
  holds. Money paid (subsidies, aid, grants, payments, purchases, indemnities) moves as dots in commerce's colour;
  capital (loans, investments, acquisitions, confiscations) as ingots in the owners' gold.
- **Labels.** The five largest flows by a year's amount show their kind, the fact's own number, unit and years, with
  "in total" for a total over years or "a year" for a rate: "Loan · 18 million livres in total, 1778-1782". Compressed they
  stand at the rung nearest the year, opened beside the river's bend. They rank below every other label of the view.
- **Emphasis.** The flows take the part of what they join (the tubes compressed, the bodies opened), so the emphasis
  table needs no row for them. A flow neither of whose ends is the selection dims with the rest.

**The data.** `Tools/advantage/history.py` (`build_flows`) builds each story's `flows` in `stories.json` from the
cluster's flow facts: facts of kind "flow", and investments or contracts with an end.
- **Drawn only when** the gate verified the fact and its `check` (the skeptic's record in the research file) does not
  stop it. `ok: false` rejects it (merge.py marks it "rejected"); `flow: false` keeps the claim but draws no flow (a
  pledge, a commitment with no schedule, a sum spent at home, a "value" that is only a year).
- **Ends** go through the story's aliases. An actor that had ended before the flow began is its successor (Britain's
  1809 subsidy is the United Kingdom's). `PAYERS` names the payer of record where the research left it outside the
  story: the Dutch loan of 1781 is the French crown's, raised in its name and under its guarantee.
- **Fields:** the fact's own value and unit; the amount in the story's money (billions of pounds of the time at £1 = 24
  livres, or of dollars) and in its unit of size; the years, both included; the kind (`FLOW_KINDS` for facts researched
  before flows had kinds); whether the amount is a total over the years (yes when the fact does not say); and gaps.
- **Overlaps.** A total that holds flows drawn on their own is not drawn in their years (`WITHIN`): Britain's £46.3
  million of cash subsidies (1793-1814) skips 1809, 1813 and 1814. France's 1.3 billion livres spent supporting the
  Americans is not a flow: it went mostly on France's own forces, and the gifts and loans that reached Congress are
  drawn on their own.
- **The build's log** lists every flow fact left out and why. On 8 Nov 2026 the AI story has 14 flows (Taiwan's 48.3%
  of TSMC's founding capital is a share: kept, drawn as nothing) and the age of revolutions 12; the skeptics rejected 11
  facts, and 6 more stand as claims but are no flows.

**The rules.**
- **Width, not rate, is the amount:** 0.8 times the cube root of a year's amount in the story's unit of size (a million
  pounds, a billion dollars), at most 6 units, a total over years spread evenly over them. The Church's lands (3 billion
  livres, £125 million, 1789) draw 4 units wide; Britain's £5 million of 1815, 1.4; Microsoft's billion into OpenAI,
  0.8; Busicom's $60,000, a hairline.
- **Rate:** one packet a second for one unit of size a year, plus 0.35 for each tenfold, from 0.5 to 2.5 (never above
  four a lane). **Speed:** one for the view, 12 units a second.
- An unmeasured amount draws nothing.
- The flows are built with the view when its story, year, selection or cross-section changes; the shader moves the
  packets. The smoke test's never-still check reads the view's default, the AI story compressed.

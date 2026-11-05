namespace Why.Economy.Emphasis
{
    /// <summary>
    /// The emphasis table's first form (spec 3.1, WP07): for every part a hill layer draws and every view of the economy's
    /// catalog, the alpha the layer gave it before the table existed, transcribed literally from its <c>Emphasis()</c> switch
    /// and its inline view-dependent alpha (and, for the land's groups, from <c>EconomyViews</c>' table and the copies its
    /// <c>Get</c> made for the views without a column). The values are not levels yet: WP14 retunes each view to the spec's
    /// four levels (<see cref="Level"/>). Nothing on screen changed with the move (the smoke frames are compared with the
    /// base commit's, Tools/emphasis-diff.py).
    /// <para>A row is a part's value at its fullest: nothing selected or hovered, no evidence-only filter, after Instagram,
    /// and close up where a layer's level of detail decides. The state's own alpha is a named multiplier on the key (the
    /// rules below, registered by the layers that know the state), each at most 1: <c>Get(key) = row × Π multipliers</c>.
    /// A view whose layer hides its content there (the capture layer in the money chart's view, the mobility layer outside
    /// its three views) is transcribed as absent: nothing of it is drawn there, now or before. The column "*" is what the
    /// layers drew in a view no switch names (the palette check's card, a view added without a row of its own).</para>
    /// <para>Pure data and arithmetic (no Unity types): <see cref="EconomyEmphasis"/> loads it, and a test outside the
    /// editor compares it with the switches it replaces.</para>
    /// </summary>
    public static class EmphasisTable
    {
        /// <summary>The columns of <see cref="Rows"/>: the economy's catalog in its order (HillPresets), then any other view.</summary>
        public static readonly string[] Views =
        {
            "overview", "section", "landscape", "capture", "people", "rivers", "mind", "society", "foundation", "companies", "social",
            "roots", "betrayal", "y1972", "strategy", "money", "regimes", "mobility", "atom", "dilemma", "ladder", "you", Other
        };

        /// <summary>The column of the views no row names.</summary>
        public const string Other = "*";

        /// <summary>
        /// Every key and its value in each of <see cref="Views"/>. A part whose layer applies it as its markers' or halos'
        /// intensity (activity.markers, activity.halos) or bakes it into its geometry (activity.trips, activity.trunks)
        /// keeps doing so; the rest are each renderer's alpha. Keys with a third segment are the states one part's items
        /// take (a firm's beam at rest, near the camera or chosen): the layer picks the state, the row gives its value.
        /// </summary>
        public static readonly (string Key, float[] Values)[] Rows =
        {
            //                             ovrvw, sectn, lands, captr, peopl, rivrs,  mind, socty, found, comps, socal, roots, betry, y1972, strat, money, regms, mobil,  atom, dilem, laddr,   you,     *

            // the land's groups: EconomyViews' table (its 12 columns; WP6's legibility: each view's own story at full strength, the
            // rest near-invisible) and the copies its Get made of the overview's for the other views: one person in front and the
            // population behind, so the person views' road recedes to .2 (the ladder's to .4: the time axis its life runs along); a
            // focused view's structure stands in front of the road, a trace there (.12, the cut .2); the money chart's low pose
            // sees the road edge-on, where its thousands of faint lines added up to a bright bar across the rates (0). LandView
            // eases them for the road of lives (TimeAxisLayer, SmvLayer)
            Row("land.road",                   1,   .3f,  .35f,  .35f,  .35f,  .35f,  .35f,  .35f,  .35f,     1,     1,  .35f,  .35f,  .35f,  .12f,     0,  .12f,  .12f,   .2f,   .2f,   .4f,  .12f,     1),
            Row("land.cut",                    1,     1,   .6f,   .6f,   .6f,   .6f,   .6f,   .6f,   .6f,     1,     1,   .6f,   .6f,   .6f,   .2f,   .2f,   .2f,   .2f,     1,     1,     1,   .2f,     1),
            Row("land.terraces",             .8f,     0,     1,  .32f,  .35f,   .4f,  .25f,   .2f,   .8f,   .8f,   .8f,  .35f,   .2f,     1,   .8f,   .8f,   .8f,   .8f,   .8f,   .8f,   .8f,   .8f,   .8f),
            Row("land.sectors",              .8f,     0,     1,     1,  .35f,  .25f,   .2f,  .12f,   .8f,   .8f,   .8f,  .45f,  .12f,     1,   .8f,   .8f,   .8f,   .8f,   .8f,   .8f,   .8f,   .8f,   .8f),
            Row("land.pools",                .5f,     0,  .35f,   .1f,   .1f,     1,   .2f,  .05f,   .5f,   .5f,   .5f,  .05f,  .05f,  .35f,   .5f,   .5f,   .5f,   .5f,   .5f,   .5f,   .5f,   .5f,   .5f),
            Row("land.roots",               .15f,     0,   .3f,  .05f,  .03f,  .03f,  .03f,  .02f,  .15f,  .15f,  .15f,     1,  .02f,   .3f,  .15f,  .15f,  .15f,  .15f,  .15f,  .15f,  .15f,  .15f,  .15f),
            Row("land.towers",               .8f,     0,  .45f,     1,  .35f,  .08f,   .1f,   .1f,   .8f,   .8f,   .8f,   .1f,   .1f,  .45f,   .8f,   .8f,   .8f,   .8f,   .8f,   .8f,   .8f,   .8f,   .8f),
            Row("land.crown",                .8f,     0,   .3f,     1,   .6f,   .1f,   .2f,   .1f,   .8f,   .8f,   .8f,  .05f,   .1f,   .3f,   .8f,   .8f,   .8f,   .8f,   .8f,   .8f,   .8f,   .8f,   .8f),
            Row("land.overlays",             .5f,     0,   .4f,     1,     0,     0,     0,     0,   .5f,   .5f,   .5f,     0,     0,   .4f,   .5f,   .5f,   .5f,   .5f,   .5f,   .5f,   .5f,   .5f,   .5f),
            Row("land.glyphs",               .7f,     0,  .35f,  .35f,     1,  .35f,     1,   .8f,   .7f,   .7f,   .7f,   .1f,   .8f,  .35f,   .7f,   .7f,   .7f,   .7f,   .7f,   .7f,   .7f,   .7f,   .7f),
            Row("land.dots",                 .3f,     0,   .2f,  .25f,     1,  .15f,     1,   .3f,   .3f,   .3f,   .3f,  .05f,   .3f,   .2f,   .3f,   .3f,   .3f,   .3f,   .3f,   .3f,   .3f,   .3f,   .3f),
            Row("land.mirages",             .15f,     0,  .05f,  .05f,   .3f,  .05f,     1,  .03f,  .15f,  .15f,  .15f,     0,  .03f,  .05f,  .15f,  .15f,  .15f,  .15f,  .15f,  .15f,  .15f,  .15f,  .15f),
            Row("land.income",               .1f,     0,  .05f,   .1f,     1,  .04f,  .05f,  .02f,   .1f,   .1f,   .1f,  .03f,  .02f,  .05f,   .1f,   .1f,   .1f,   .1f,   .1f,   .1f,   .1f,   .1f,   .1f),
            Row("land.capitalflows",        .15f,     0,  .05f,   .6f,   .4f,  .04f,  .05f,  .02f,  .15f,  .15f,  .15f,  .03f,  .02f,  .05f,  .15f,  .15f,  .15f,  .15f,  .15f,  .15f,  .15f,  .15f,  .15f),
            Row("land.rivers",               .4f,     0,  .15f,   .1f,  .15f,     1,   .5f,  .06f,   .4f,   .4f,   .4f,  .05f,  .06f,  .15f,   .4f,   .4f,   .4f,   .4f,   .4f,   .4f,   .4f,   .4f,   .4f),
            Row("land.glitter",              .2f,     0,  .05f,  .05f,  .05f,     1,   .8f,  .03f,   .2f,   .2f,   .2f,     0,  .03f,  .05f,   .2f,   .2f,   .2f,   .2f,   .2f,   .2f,   .2f,   .2f,   .2f),
            Row("land.taxes",                .2f,     0,   .1f,  .05f,  .15f,     1,   .1f,  .03f,   .2f,   .2f,   .2f,  .05f,  .03f,   .1f,   .2f,   .2f,   .2f,   .2f,   .2f,   .2f,   .2f,   .2f,   .2f),
            Row("land.ties",                   0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0,     0,     1,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0),
            Row("land.coalitions",             0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0,     0,   .6f,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0),

            // HillWaveLayer.Emphasis (its content hidden in the mind view). Today's crest outlined over an earlier one (ghost)
            // belongs to 1972's view; the plates behind the names the lines cross to the views of the wave and the encounters'; the
            // arrow of time to the views of the whole wave (from the foundation's low pose it fell in the corner under the title).
            // The ladder runs across the wake: its ribs and ridges are the ground the life climbs over. The money chart stands in
            // front of the wave on the same time axis: a bright crest would climb past its 20% rule like a fifth series, so only
            // the ribs remain, the ground of the years. The wave is the faint ground of a focused view; the companies' spending
            // view keeps their lives. The rivers tell desire and fear: the gold face and the crest step back below them, and so do
            // the saving (gold) and the debts arriving from the future (most households' mix of motives, near white). The front
            // breaking forward (spray) is the wave's own image: full in its views, a trace where another story leads. The families'
            // view names the fan and steps it back so the family links read. State: WaveSelection
            Row("wave.ribs",                .55f,   .3f,  .85f,  .22f,  .22f,  .22f,     0,  .08f,  .22f,  .55f,  .22f,  .22f,  .08f,  .22f,   .2f,   .1f,   .2f,   .2f,  .14f,  .14f,   .5f,   .2f,  .22f),
            Row("wave.ridges",               .7f,   .2f,     1,   .2f,   .2f,   .2f,     0,   .2f,   .2f,  .45f,   .2f,   .4f,   .2f,   .2f,  .28f,  .06f,  .28f,  .28f,  .25f,  .25f,  .55f,  .28f,   .2f),
            Row("wave.face",                 .8f,   .4f,     1,   .4f,   .4f,  .25f,     0,  .15f,     1,   .4f,   .4f,   .4f,  .15f,   .4f,  .14f,  .05f,  .14f,  .14f,  .12f,  .12f,   .3f,  .14f,   .4f),
            Row("wave.crest",                  1,     1,     1,     1,     1,   .3f,     0,   .3f,     1,     1,     1,     1,   .3f,     1,  .45f,  .06f,  .45f,  .45f,  .22f,  .22f,   .6f,  .45f,     1),
            Row("wave.spray",                .8f,   .5f,   .8f,  .12f,  .12f,  .12f,     0,  .12f,  .12f,   .8f,  .12f,  .12f,  .12f,   .5f,  .02f,     0,  .02f,  .02f,  .04f,  .04f,  .04f,  .02f,  .12f),
            Row("wave.lives",               .75f,   .1f,  .75f,   .3f,   .1f,   .1f,     0,   .1f,   .1f,     1,   .1f,  .25f,   .1f,   .1f,   .4f,     0,  .05f,  .05f,  .04f,  .04f,  .04f,  .05f,   .1f),
            Row("wave.focus",                  1,     1,     1,     1,     1,     1,     0,     1,     1,     1,     1,     1,     1,     1,     1,     0,     1,     1,     1,     1,     1,     1,     1),
            Row("wave.fan",                 .14f,  .35f,  .04f,  .04f,   .7f,  .04f,     0,  .04f,  .04f,  .04f,  .04f,  .04f,  .04f,  .04f,  .03f,     0,  .03f,  .03f,  .04f,  .04f,  .04f,  .03f,  .04f),
            Row("wave.future",              .12f,  .04f,  .04f,  .04f,  .35f,   .3f,     0,  .04f,  .04f,  .04f,  .04f,  .04f,  .04f,  .04f,  .03f,     0,  .03f,  .03f,  .04f,  .04f,  .04f,  .03f,  .04f),
            Row("wave.saving",              .12f,  .04f,  .04f,  .04f,  .35f,   .3f,     0,  .04f,  .04f,  .04f,  .04f,  .04f,  .04f,  .04f,  .03f,     0,  .03f,  .03f,  .04f,  .04f,  .04f,  .03f,  .04f),
            Row("wave.arrow",               .85f,     0,  .85f,     0,     0,     0,     0,     0,     0,  .85f,     0,     0,     0,  .85f,     0,     0,     0,     0,     0,     0,     0,     0,     0),
            Row("wave.ghost",                  0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,   .6f,     0,     0,     0,     0,     0,     0,     0,     0,     0),
            Row("wave.plate",                  1,     0,     1,     0,     1,     0,     0,     1,     1,     1,     0,     0,     1,     1,     0,     0,     0,     0,     0,     0,     0,     0,     0),

            // HillLandscapeLayer.Emphasis by group, with its Tick's rule for the mind view (.035, its own mind 1) and the season's
            // ties (1 in the encounters' views): roots Roots, rungs Glyphs (the class rungs' legend), towers Towers (its builder is
            // empty today), crown Crown (the owners' crowns and the top 1%'s clouds), family Dots (a selected household's family),
            // investment CapitalFlows, pool Pools, mind Mirages, ties the season's renderer. The followed person's views keep the
            // hills' names and the clouds faintly; the rest steps back. The owners' investment and their pool are a trace under the
            // rivers of desire and fear; in who keeps the money the pool stays (the owners' surplus rises into it) and the
            // investment falling back out of it does not
            Row("hills.roots",             .055f, .055f, .055f, .055f, .055f, .055f, .035f, .055f, .055f, .055f, .055f,  .75f, .055f, .055f,  .03f,  .03f,  .03f,  .03f,  .03f,  .03f,  .03f,  .03f, .055f),
            Row("hills.rungs",                 0,     0,     0,     0,   .9f,     0, .035f,     0,     0,     0,     0,     0,     0,     0,  .03f,  .03f,  .03f,  .03f,  .03f,  .03f,  .35f,  .03f,     0),
            Row("hills.towers",             .12f,  .12f,  .12f,  .55f,  .12f,  .12f, .035f,  .12f,  .12f,  .12f,  .12f,  .12f,  .12f,  .12f,  .03f,  .03f,  .03f,  .03f,  .03f,  .03f,  .03f,  .03f,  .12f),
            Row("hills.crown",               .1f,   .1f,   .1f,     1,  .35f,   .1f, .035f,   .1f,   .1f,   .1f,   .1f,   .1f,   .1f,   .1f,  .08f,  .08f,  .08f,  .08f,  .12f,  .12f,  .12f,  .08f,   .1f),
            Row("hills.family",                1,     1,     1,     1,     1,     1, .035f,     1,     1,     1,     1,     1,     1,     1,  .03f,  .03f,  .03f,  .03f,  .03f,  .03f,  .03f,  .03f,     1),
            Row("hills.investment",            0,     0,     0,     0,     0,   .2f, .035f,     0,     0,     0,     0,     0,     0,     0,  .03f,  .03f,  .03f,  .03f,  .03f,  .03f,  .03f,  .03f,     0),
            Row("hills.pool",                  0,     0,     0,   .2f,     0,   .2f, .035f,     0,     0,     0,     0,     0,     0,     0,  .03f,  .03f,  .03f,  .03f,  .03f,  .03f,  .03f,  .03f,     0),
            Row("hills.mind",                  0,     0,     0,     0,     0,     0,     1,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0),
            Row("hills.ties",                  0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0,     0,     1,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0),

            // HillActivityLayer's inline alpha (its content hidden in the mind and section views, its rivers, their sparks and the
            // summit's ring in the money chart's too: seen edge-on behind the chart's panel they added up to the series' own
            // brightness): markers and halos are the markers' and the firms' halos' intensity (a halo .lit when chosen or of the
            // focused sector: HDR only for those; who keeps the money draws the industries' owners' halos instead of the firms'; in
            // a focused view other than the companies' own the firms' halos step back to landmarks, in a person's further). A
            // firm's beam and organization take the largest of the states it is in (.near: lod >= 1 and near or of the focused
            // sector for a beam, lod >= 2 and near for an organization; .related: of the focused sector, the selected or hovered
            // household's or the explored or hovered industry, at lod >= 1; .chosen: the explored firm; .employer: the followed
            // person's). trips and trunks are the rivers' water, baked into their lanes. The regimes view draws its own summits
            // (steel where the state owns them), so none of the firms' halos, beams, organizations and owner rings: under the
            // Soviet state their gold read as private ones. State: EvidenceOnly
            Row("activity.markers",            1,     0,     1,     1,     1,     1,     0,     1,     1,     1,     1,     1,     1,     1,   .3f,   .3f,   .3f,   .3f,  .25f,  .25f,  .25f,   .3f,     1),
            Row("activity.halos",              1,     0,     1,     0,     1,     1,     0,     1,     1,     1,     1,     1,     1,     1,     1,     0,     0,  .25f,  .15f,  .15f,  .15f,  .25f,     1),
            Row("activity.halos.lit",          1,     0,     1,     1,     1,     1,     0,     1,     1,     1,     1,     1,     1,     1,     1,     0,     0,  .25f,  .15f,  .15f,  .15f,  .25f,     1),
            Row("activity.beams",           .22f,     0,  .22f,  .55f,  .22f,  .22f,     0,  .22f,  .22f,  .22f,  .22f,  .22f,  .22f,  .22f,  .22f,     0,     0,  .05f,  .05f,  .05f,  .05f,  .05f,  .22f),
            Row("activity.beams.near",      .95f,     0,  .95f,  .95f,  .95f,  .95f,     0,  .95f,  .95f,  .95f,  .95f,  .95f,  .95f,  .95f,  .95f,     0,     0,     0,     0,     0,     0,     0,  .95f),
            Row("activity.beams.chosen",    .95f,     0,  .95f,  .95f,  .95f,  .95f,     0,  .95f,  .95f,  .95f,  .95f,  .95f,  .95f,  .95f,  .95f,     0,     0,  .95f,  .95f,  .95f,  .95f,  .95f,  .95f),
            Row("activity.orgs",               0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0),
            Row("activity.orgs.near",        .9f,     0,   .9f,   .9f,   .9f,   .9f,     0,   .9f,   .9f,   .9f,   .9f,   .9f,   .9f,   .9f,     0,     0,     0,     0,     0,     0,     0,     0,   .9f),
            Row("activity.orgs.related",     .3f,     0,   .3f,   .3f,   .3f,   .3f,     0,   .3f,   .3f,   .3f,   .3f,   .3f,   .3f,   .3f,     0,     0,     0,     0,     0,     0,     0,     0,   .3f),
            Row("activity.orgs.chosen",      .9f,     0,   .9f,   .9f,   .9f,   .9f,     0,   .9f,   .9f,   .9f,   .9f,   .9f,   .9f,   .9f,   .9f,     0,     0,   .9f,   .9f,   .9f,   .9f,   .9f,   .9f),
            Row("activity.orgs.employer",      0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,   .9f,   .9f,   .9f,     0,     0),
            Row("activity.supply",         .018f,     0, .018f, .018f, .018f, .018f,     0, .018f, .018f, .018f, .018f,  .35f, .018f, .018f, .018f, .018f, .018f, .018f, .018f, .018f, .018f, .018f, .018f),
            Row("activity.rivalry",         .07f,     0,  .07f,   .5f,  .07f,  .07f,     0,  .07f,  .07f,  .07f,  .07f,  .07f,  .07f,  .07f,  .07f,  .07f,  .07f,  .07f,  .07f,  .07f,  .07f,  .07f,  .07f),
            Row("activity.circles",         .75f,     0,  .75f,     1,  .75f,  .45f,     0,  .45f,  .45f,     1,  .45f,  .45f,  .45f,  .45f,     1,     0,     0,  .15f,  .15f,  .15f,  .15f,  .15f,  .45f),
            Row("activity.threads",         .08f,     0,  .08f,     0,   .3f,  .08f,     0,  .08f,  .08f,   .6f,  .08f,  .08f,  .08f,  .08f,   .6f,     0,     0,  .02f,  .02f,  .02f,  .02f,  .02f,  .08f),
            Row("activity.trips",          .035f,     0, .035f, .035f, .035f,   .5f,     0, .035f, .035f, .035f, .035f, .035f, .035f, .035f, .035f,     0, .035f, .035f, .035f, .035f, .035f, .035f, .035f),
            Row("activity.trunks",           .2f,     0,   .2f,   .2f,   .2f,   .8f,     0,   .2f,   .2f,   .2f,   .2f,   .2f,   .2f,   .2f,   .2f,     0,   .2f,   .2f,   .2f,   .2f,   .2f,   .2f,   .2f),
            Row("activity.rivers",             1,     0,     1,     1,     1,     1,     0,     1,     1,     1,     1,     1,     1,     1,     1,     0,     1,     1,     1,     1,     1,     1,     1),
            Row("activity.sparks",             1,     0,     1,     1,     1,     1,     0,     1,     1,     1,     1,     1,     1,     1,     1,     0,     1,     1,     1,     1,     1,     1,     1),
            Row("activity.focus",              1,     0,     1,     1,     1,     1,     0,     1,     1,     1,     1,     1,     1,     1,     1,     0,     1,     1,     1,     1,     1,     1,     1),

            // HillCaptureLayer.Emphasis (its content hidden in the mind, section and money chart's views). Who keeps the money
            // draws every part of the dollar at its weight: wages (41¢ in 2025) as strong as the owners' climb (27¢), production
            // taxes (7¢) near it, the owners' ascent to their holders no stronger. In the focused views the halos stay as landmarks
            // (brighter where the story is the money's destination); the regimes view draws its own summits (steel where the state
            // owns them); in the rivers view, whose story is desire and fear, where their money goes next steps back under them.
            // State: CaptureSelection, CaptureDetail
            Row("capture.climb",             .8f,     0,   .8f,     1,   .8f,   .3f,     0,  .02f,  .08f,   .8f,   .8f,  .06f,  .02f,   .8f,  .03f,     0,  .02f,  .02f,  .02f,  .02f,  .02f,  .03f,   .8f),
            Row("capture.halo",                1,     0,     1,     1,     1,  .25f,     0,  .12f,   .3f,     1,     1,  .35f,  .12f,     1,  .45f,     0,  .02f,   .1f,  .25f,  .25f,  .25f,  .35f,     1),
            Row("capture.glow",              .8f,     0,   .8f,     1,   .8f,  .25f,     0,  .12f,   .3f,   .8f,   .8f,  .35f,  .12f,   .8f,  .45f,     0,  .02f,   .1f,  .25f,  .25f,  .25f,  .35f,   .8f),
            Row("capture.ascent",           .07f,     0,  .07f,   .4f,  .15f,  .12f,     0,  .02f,  .08f,  .07f,  .07f,  .06f,  .02f,  .07f,  .03f,     0,  .02f,  .02f,  .02f,  .02f,  .02f,  .03f,  .07f),
            Row("capture.wages",            .16f,     0,  .55f,     1,   .9f,  .25f,     0,  .02f,  .08f,  .16f,  .16f,  .06f,  .02f,  .16f,  .03f,     0,  .02f,  .02f,  .02f,  .02f,  .02f,  .03f,  .16f),
            Row("capture.taxes",             .1f,     0,  .35f,   .8f,   .1f,  .25f,     0,  .02f,     1,   .1f,   .1f,  .06f,  .02f,   .1f,  .03f,     0,  .02f,  .02f,  .02f,  .02f,  .02f,  .03f,   .1f),
            Row("capture.ledger",            .7f,     0,   .7f,     1,     1,   .2f,     0,  .02f,  .08f,   .7f,   .7f,  .06f,  .02f,   .7f,  .03f,     0,  .02f,  .02f,  .02f,  .02f,  .02f,  .03f,   .7f),
            Row("capture.roots",            .04f,     0,  .04f,  .35f,  .04f,   .1f,     0,  .02f,  .08f,  .04f,  .04f,     1,  .02f,  .04f,  .03f,     0,  .02f,  .02f,  .02f,  .02f,  .02f,  .03f,  .04f),
            Row("capture.selected",            1,     0,     1,     1,     1,     1,     0,  .02f,     1,     1,     1,  .06f,  .02f,     1,  .03f,     0,  .02f,  .02f,  .02f,  .02f,  .02f,     1,     1),

            // HillFoundationLayer.Emphasis (its content hidden in the mind and section views). The benefits fan out to every
            // household: in the bedrock's own view a weave under the block, not its brightest mark. The regimes view draws the
            // foundation of the regime it shows: the US block (and its seats) steps back to a trace. In the focused views the
            // bedrock stays as the ground; in the companies' spending view their lobbying meets the seats and the rule-making. The
            // regulation and capture strands run from the bedrock up into every hill: the bedrock's story and close up; elsewhere,
            // who keeps the money included, a trace, or two strands per industry curtain the whole crest face. State:
            // FoundationDetail
            Row("foundation.block",         .55f,     0,  .55f,  .55f,  .55f,  .55f,     0,  .25f,   .8f,  .55f,  .55f,  .55f,  .25f,  .55f,   .3f,  .18f,  .05f,  .18f,  .12f,  .12f,  .12f,  .18f,  .55f),
            Row("foundation.strata",        .45f,     0,  .45f,   .7f,   .7f,   .7f,     0,  .02f,     1,   .7f,   .7f,   .7f,  .02f,   .7f,  .03f,  .02f,     0,  .02f,  .02f,  .02f,  .02f,  .02f,   .7f),
            Row("foundation.pillars",        .1f,     0,   .1f,   .2f,  .35f,  .35f,     0,  .02f,     1,   .1f,  .35f,  .35f,  .02f,  .35f,  .03f,  .02f,     0,  .02f,  .12f,  .12f,  .12f,  .02f,  .35f),
            Row("foundation.benefits",       .5f,     0,   .5f,   .5f,  .12f,   .5f,     0,  .02f,   .4f,   .5f,   .5f,   .5f,  .02f,   .5f,  .03f,  .02f,     0,  .02f,  .02f,  .02f,  .02f,  .02f,   .5f),
            Row("foundation.taxes",         .45f,     0,  .45f,  .45f,  .45f,  .45f,     0,  .02f,     1,  .45f,  .45f,  .45f,  .02f,  .45f,  .03f,  .02f,     0,  .02f,  .02f,  .02f,  .02f,  .02f,  .45f),
            Row("foundation.regulation",    .45f,     0,  .45f,  .03f,  .45f,  .45f,     0,  .02f,     1,  .45f,  .45f,  .45f,  .02f,  .45f,  .06f,  .02f,     0,  .02f,  .02f,  .02f,  .02f,  .02f,  .45f),
            Row("foundation.seats",            1,     0,     1,     1,     1,     1,     0,  .02f,     1,     1,     1,     1,  .02f,     1,   .6f,  .02f,     0,  .02f,  .02f,  .02f,  .02f,  .02f,     1),
            Row("foundation.selected",         1,     0,     1,     1,     1,     1,     0,  .02f,     1,     1,     1,     1,  .02f,     1,  .03f,  .02f,     0,  .02f,  .02f,  .02f,  .02f,  .02f,     1),

            // HillSocialLayer.Emphasis (its content hidden in the views at 0 below, and before Instagram outside its own view):
            // outside its own view the cloud is context, named but no longer the brightest thing on screen. State: SocialDetail,
            // SocialEarly
            Row("social.glow",               .4f,     0,   .4f,     0,   .4f,     0,     0,     0,     0,     0,     1,     0,     0,   .4f,   .4f,     0,     0,     0,     0,     0,     0,     0,   .4f),
            Row("social.stars",                1,     0,     1,     0,     1,     0,     0,     0,     0,     0,     1,     0,     0,     1,     1,     0,     0,     0,     0,     0,     0,     0,     1),
            Row("social.bright",             .7f,     0,   .7f,     0,   .7f,     0,     0,     0,     0,     0,     1,     0,     0,   .7f,   .7f,     0,     0,     0,     0,     0,     0,     0,   .7f),
            Row("social.ties",               .6f,     0,   .6f,     0,   .6f,     0,     0,     0,     0,     0,   .9f,     0,     0,   .6f,   .6f,     0,     0,     0,     0,     0,     0,     0,   .6f),
            Row("social.rings",              .6f,     0,   .6f,     0,   .6f,     0,     0,     0,     0,     0,     1,     0,     0,   .6f,   .6f,     0,     0,     0,     0,     0,     0,     0,   .6f),
            Row("social.anchors",              1,     0,     1,     0,     1,     0,     0,     0,     0,     0,     1,     0,     0,     1,     1,     0,     0,     0,     0,     0,     0,     0,     1),
            Row("social.threads",           .25f,     0,  .25f,     0,  .25f,     0,     0,     0,     0,     0,   .5f,     0,     0,  .25f,  .25f,     0,     0,     0,     0,     0,     0,     0,  .25f),
            Row("social.elite",              .3f,     0,   .3f,     0,   .3f,     0,     0,     0,     0,     0,   .9f,     0,     0,   .3f,   .3f,     0,     0,     0,     0,     0,     0,     0,   .3f),
            Row("social.loop",                 0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0),

            // HillMobilityLayer.Emphasis (its content drawn in the mobility, people and you views only): the people view (class is
            // altitude) carries the cohorts' drift on the hills; the ladder is the mobility view's
            Row("mobility.drift",              0,     0,     0,     0,   .9f,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0,  .25f,     0),
            Row("mobility.ladder",             0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0,  .12f,     0),
            Row("mobility.ribbons",            0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,   .9f,     0,     0,     0,  .12f,     0),

            // HillPersonLayer's atom, pulls and ladder alphas (its content drawn where one of them is above 0)
            Row("person.atom",                 0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,  .22f,  .12f,     0,     0),
            Row("person.pulls",                0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,  .06f,     0,     0,     0),
            Row("person.ladder",               0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,  .18f,     0,     1,     0,     0),

            // HillDilemmaLayer (drawn in its own view only, at full alpha)
            Row("dilemma.arena",               0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0),
            Row("dilemma.floor",               0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0),
            Row("dilemma.moves",               0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0),
            Row("dilemma.pulses",              0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0),

            // HillYouLayer (drawn in its own view only, at full alpha)
            Row("you.figure",                  0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0),
            Row("you.rivers",                  0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0),
            Row("you.flow",                    0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0),
            Row("you.futures",                 0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0),

            // HillStrategyLayer (drawn in its own view only, at full alpha)
            Row("strategy.streams",            0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0,     0,     0,     0,     0,     0),
            Row("strategy.focus",              0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0,     0,     0,     0,     0,     0),
            Row("strategy.arrows",             0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0,     0,     0,     0,     0,     0),
            Row("strategy.cascade",            0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0,     0,     0,     0,     0,     0),
            Row("strategy.trail",              0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0,     0,     0,     0,     0,     0),

            // HillMoneyLayer (drawn in its own view only, at full alpha; the backing also fades as the camera turns edge-on to it,
            // its multiplier "seen from its side")
            Row("money.chart",                 0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0,     0,     0,     0,     0),
            Row("money.fill",                  0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0,     0,     0,     0,     0),
            Row("money.backing",               0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0,     0,     0,     0,     0),

            // HillRegimeLayer (drawn in its own view only, at full alpha). State: RegimeTourCard
            Row("regime.bedrock",              0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0,     0,     0,     0),
            Row("regime.strata",               0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0,     0,     0,     0),
            Row("regime.summits",              0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0,     0,     0,     0),
            Row("regime.beams",                0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0,     0,     0,     0),
            Row("regime.strands",              0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0,     0,     0,     0),
            Row("regime.taxes",                0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0,     0,     0,     0),
            Row("regime.votes",                0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0,     0,     0,     0),
            Row("regime.trade",                0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0,     0,     0,     0),
            Row("regime.cloud",                0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0,     0,     0,     0),

            // HillReplayLayer (drawn while a replay runs, in the views of the whole wave; the held year's rib pulses, its
            // multiplier "the held year's pulse")
            Row("replay.ribs",                 1,     0,     1,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0),
            Row("replay.now",                  1,     0,     1,     0,     0,     0,     0,     0,     0,     1,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0),
            //                             ovrvw, sectn, lands, captr, peopl, rivrs,  mind, socty, found, comps, socal, roots, betry, y1972, strat, money, regms, mobil,  atom, dilem, laddr,   you,     *
        };

        static (string, float[]) Row(string key, params float[] values) => (key, values);

        // ------------------------------------------------------------------ the views the switches named together

        /// <summary>The followed person's views (PersonFocus.IsView at the transcription).</summary>
        public static bool Person(string view) => view == "atom" || view == "dilemma" || view == "ladder";

        /// <summary>The views that tell one story on a structure of their own (EconomyViews.Focused at the transcription).</summary>
        public static bool Focused(string view) =>
            view == "mobility" || view == "strategy" || view == "you" || view == "money" || view == "regimes";

        /// <summary>The views the capture layer's switch gave rules of their own; the selection's dimming and the ledger's
        /// level of detail belong to the rest (its last branch) and, for the dimming, to the rivers view.</summary>
        static bool CaptureOwnRule(string view) =>
            view == "society" || view == "betrayal" || Person(view) || view == "roots" || view == "foundation" || view == "mobility" ||
            view == "regimes" || view == "strategy" || view == "you";

        /// <summary>The views the government layer's switch draws by its last branch, where its level of detail counts.</summary>
        static bool FoundationLastBranch(string view) =>
            view != "foundation" && view != "society" && view != "betrayal" && !Person(view) && view != "regimes" && !Focused(view);

        // ------------------------------------------------------------------ the state multipliers (each at most 1)

        /// <summary>
        /// "a household selected" on wave.lives (HillWaveLayer): while a household is selected (EconomyState.SelectedPlayer)
        /// the companies' lives step back to .4 of their row in the views its switch's last branch draws; the person views,
        /// the focused views and the money chart's keep theirs.
        /// </summary>
        public static float WaveSelection(string view, bool selected) => selected && !Person(view) && !Focused(view) ? .4f : 1;

        /// <summary>
        /// "a household selected" on capture.climb, .ascent, .wages and .taxes (HillCaptureLayer): while a household is
        /// selected the dollar's other flows dim to .35 of their rows, so its own arcs (capture.selected) read: in the views
        /// of the switch's last branch (the owners' climb not in the capture view, where it is the story) and in the rivers
        /// view, not in the views with rules of their own.
        /// </summary>
        public static float CaptureSelection(string key, string view, bool selected)
        {
            if (!selected || CaptureOwnRule(view)) return 1;
            switch (key)
            {
                case "capture.climb": return view == "capture" ? 1 : .35f;
                case "capture.ascent":
                case "capture.wages":
                case "capture.taxes": return .35f;
                default: return 1;
            }
        }

        /// <summary>
        /// "level of detail" on capture.ledger (HillCaptureLayer's lod: 2 within 35 units of the camera's target, 1 within
        /// 100, 0 beyond): in the views of the switch's last branch but the capture and people views, the households' ledger
        /// rings stand at .7 from mid distance and .2 beyond.
        /// </summary>
        public static float CaptureDetail(string key, string view, int lod)
        {
            if (key != "capture.ledger" || lod >= 1 || CaptureOwnRule(view) || view == "rivers" || view == "capture" || view == "people") return 1;
            return .2f / .7f;
        }

        /// <summary>
        /// "level of detail" on foundation.benefits, .taxes and .regulation (HillFoundationLayer's lod: 2 in its own view and
        /// within 35 units of the camera's target, 1 within 100, 0 beyond): in the views of the switch's last branch, short
        /// of close up the benefits fall from .5 to .06 (not in the people view, where they stay at .12), the households'
        /// taxes from .45 to .05 (.3 in the rivers view) and the regulation strands from .45 to .05 (not in the capture view,
        /// where they stay at .03).
        /// </summary>
        public static float FoundationDetail(string key, string view, int lod)
        {
            if (lod >= 2 || !FoundationLastBranch(view)) return 1;
            switch (key)
            {
                case "foundation.benefits": return view == "people" ? 1 : .06f / .5f;
                case "foundation.taxes": return (view == "rivers" ? .3f : .05f) / .45f;
                case "foundation.regulation": return view == "capture" ? 1 : .05f / .45f;
                default: return 1;
            }
        }

        /// <summary>
        /// "level of detail" on social.stars, .ties, .rings, .threads and .elite (HillSocialLayer's lod: the camera within
        /// 45 units of the cloud's centre 2, within 140 1, 0 beyond): outside the attention view, far off the stars fall to
        /// .25, the category clouds' rings to .15, the threads to the cloud to .08 and the rings of those the most followed
        /// follow (elite) to .05, and the mutual follows (ties) show only close up.
        /// </summary>
        public static float SocialDetail(string key, string view, int lod)
        {
            if (view == "social") return 1;
            switch (key)
            {
                case "social.stars": return lod >= 1 ? 1 : .25f;
                case "social.ties": return lod >= 2 ? 1 : 0;
                case "social.rings": return lod >= 1 ? 1 : .15f / .6f;
                case "social.threads": return lod >= 1 ? 1 : .08f / .25f;
                case "social.elite": return lod >= 1 ? 1 : .05f / .3f;
                default: return 1;
            }
        }

        /// <summary>"before Instagram" on every social key: before the platform whose follows place the stars (2010), the
        /// cloud is a faint ghost (.3) in its own view (and not drawn elsewhere).</summary>
        public static float SocialEarly(bool early) => early ? .3f : 1;

        /// <summary>"evidence only" on activity.supply, .rivalry and every activity.orgs state (HillExplorer.EvidenceOnly):
        /// the inferred supply allocations, the scenario's rivalry and the schematic organizations are not evidence.</summary>
        public static float EvidenceOnly(bool evidenceOnly) => evidenceOnly ? 0 : 1;

        /// <summary>"the tour's card" on regime.trade (HillRegimeLayer): the tour docks its card over the far border, where
        /// the trade gauge's words would read as the card's own, so the gauge steps aside while the tour runs.</summary>
        public static float RegimeTourCard(bool touring) => touring ? 0 : 1;
    }
}

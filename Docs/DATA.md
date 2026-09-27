# why-2 data

All runtime data lives in `Assets/Resources` and is loaded as `TextAsset`s by the layers. Every file
below was either part of the original projects (why-2 and smv) or authored for this version from
mainstream sources and then checked by an independent reviewer pass. Values marked approximate are
modeled, not measured; the graph is an education tool about cause and effect, not a primary source.

## Matter (red) - `Data/matter.json`

```json
{ "ageOfUniverseYears": 13.787e9,
  "items":  [{ "id", "name", "startYa", "endYa", "massKg", "rank", "parent", "inPath", "blurb", "source",
               "sizeM", "bound", "icon" }],
  "epochs": [{ "id", "name", "ya", "afterBigBangYears", "tier", "blurb", "source" }] }
```

* `items` are the bands of the red layer. `rank` orders them radially (0 = innermost = Earth). Bands with
  `inPath` form our lineage, nested through `parent`: universe -> Laniakea Supercluster -> Virgo
  Supercluster -> Local Group -> Milky Way -> solar nebula -> Sun -> Earth.
* `sizeM` (diameter, metres) with `massKg` gives each band's density. A band is shaded by the density of
  its *own* region (its matter minus what formed out of it, over its volume minus theirs), from about
  3x10^30 atoms/m^3 for Earth to 2x10^5 between the stars and ~0.3 between the galaxies, so the gaps
  between the contours read as orders of magnitude. The fan labels quote these densities.
* `bound: false` marks structures gravity does not hold together (superclusters, the universe): they
  thin out and dim as space expands.
* `icon` names the glyph drawn beside the item's labels (`Core/Icons.cs`: universe, cosmic_web,
  first_stars, laniakea, supercluster, galaxy_group, galaxy, nebula, sun, planets, earth, moon).
* The red envelope follows the real expansion of space: a flat Lambda-CDM scale factor
  (Omega_Lambda 0.69, 1/H0 = 14.4 Gyr), so most of the fan opens in the first few billion years.
  Groups that form inside an older body (the solar system inside the Milky Way) open gradually over a
  long arc, so there is no step in scale anywhere.
* `epochs` are cosmic and geologic markers (Big Bang ... Chicxulub impact).
* Sources: Planck 2018; Tully et al. 2014 (*The Laniakea supercluster of galaxies*, Nature);
  Wikipedia *Chronology of the universe*, *Observable universe*, *Virgo Supercluster*, *Local Group*,
  *Milky Way*, *Formation and evolution of the Solar System*; Immerman, *The universe*
  (people.cs.umass.edu/~immerman/stanford/universe.html). Replaces the original `energy.json`.

## Life (green)

* `TimetreeOfLife2009.txt` - the TimeTree of Life (Hedges & Kumar 2009) family-level Newick tree:
  1610 leaves, ultrametric, root at 4200 Ma. Unchanged from the original project.
* `Data/life_traits.json` - `{ "<leaf label>": { "t": 1.0-5.0, "role": "...", "common": "..." } }`, the
  trophic level (food-chain position), ecological role and a lay common name for every leaf. Used to
  order branches with top predators on the inside track. Hominidae is fixed at 5.0 (the reference point).
* `Data/life_clades.json` -
  `{ "clades": [{ "id", "name", "mrca": [leafA, leafB], "treeAgeMya", "leafCount", "tier", "blurb" }],
     "events": [{ "id", "name", "mya", "tier", "blurb" }] }`. A clade is the most recent common
  ancestor of two leaves; events are dated milestones (Great Oxidation, Cambrian explosion, the Big Five
  extinctions, first hominins, agriculture...).

## Humans (blue)

* `powerByYearsAgo.json` - digitized from John B. Sparks' *Histomap* (1931): for every 50 years from
  4000 to 100 years before 2024, each stream's left position and width in Histomap units
  (1699 units = 100% of world power). The digitization is partial (not every stream of the chart).
* `Data/civilizations.json` -
  `{ "epochYear": 2024, "totalWidth": 1699, "civs": [{ "id", "key", "name", "region", "startYear",
     "endYear", "parents", "blurb", "source" }], "extension": { "<yearsAgo>": { "<key>": { "Item1", "Item2" } } } }`.
  Metadata for every stream, causal `parents`, and an extension that (a) adds streams missing from the
  digitization (United States, Britain, Russia, Japan, Ottomans, Mongols) and (b) continues all streams
  from 1924 to 2024 using approximate shares of world GDP (Maddison Project) as the power proxy.
* `Data/demography.json` - world population (HYDE 3.2 / Our World in Data / UN WPP), crude birth rates,
  era life tables (survival by age, female advantage), sex ratio at birth, major wars with approximate
  male excess mortality ("the gender difference, especially with war"), and US generations.
* `Data/figures.json` - famous historical figures `{ "id", "name", "born", "died", "gender", "civ",
  "prominence", "tier", "role", "blurb" }` drawn as highlighted lifelines in their civilization.
* `Data/smv/*.csv` - from the smv project: UN World Population Prospects single-age population of the
  United States by sex, 1950-2022 (thousands); US marriages and divorces per 1000 (Our World in Data /
  CDC NCHS); single-parent homes (US Census); number of sexual partners by age and sex (bedbible.com,
  CDC NSFG key statistics).

### Modeling notes

* Civilization population is approximated as world population x relative power share. Relative power is
  not population, but it is the only per-civilization series available across 4000 years.
* Lifelines are statistical: each line represents N people (shown on screen), born in proportion to the
  stream's births, living a lifespan drawn from the era's life table, shortened for men during wars.
* "Social market value" follows the storyline in the smv README (see `Assets/Scripts/Humans/Shared/SmvModel.cs`
  for every parameter). It is a model of an idea, drawn so it can be examined, not a measurement.
* The United States 1950-now is simulated in detail from the UN age pyramids with marriage, divorce,
  births and partner counts; children's lines start from their parents.

## The hierarchy of human domination

* `Data/domestication.json` - life under human control, drawn in green directly beneath the
  civilizations: global cropland and pasture (HYDE 3.2/3.3), livestock biomass by type in megatonnes of
  carbon (Bar-On et al. 2018, Greenspoon et al. 2023, FAO), crop, human and wild-mammal biomass, and
  35 domestications (dog, sheep, cattle, wheat, rice, maize...) each tied to its wild family in the tree
  of life (`wildFamily` = exact leaf label), so the thread rises out of that family at the moment of
  domestication. Livestock is drawn on the same mass scale as the human layer.
* `Data/extraction.json` - matter under human control, drawn in red beneath the farm layer: annual global
  extraction of fossil fuels, metal ores and non-metallic minerals (UN IRP Global Material Flows,
  Krausmann et al. 2009/2018; rough historical estimates before 1900), 16 materials with first use
  (each rises out of Earth's matter band), and 30 extraction milestones.
* `Data/figures.json` also carries `influencedBy` (figure ids) and `influenceNote` - well-established
  direct influences (teacher and student, succession, acknowledged intellectual debt) drawn as threads
  between lifelines.

## Director - `Data/tour.json`

`{ "title", "steps": [{ "id", "title", "text", "focus", "anchor", "highlight", "hold" }] }` - `focus` is a
view preset id, `anchor`/`highlight` are anchor keys (`matter:`, `epoch:`, `clade:`, `lifeevent:`,
`leaf:`, `civ:`, `figure:`, `war:`, `gen:`, `smv:us`, `farm:`, `domestication:`, `extraction:`,
`extractionevent:`, `resource:`, `time:<yearsAgo>`, `now`).

# Impacts in the universe scene: Theia, the Late Heavy Bombardment, Chicxulub

Research for drawing impactor paths on the red matter layer (request of 6 Nov 2026). The layer's radius is the
real distance from our lineage (`Assets/Scripts/Matter/MatterScale.cs`, log10 metres; the inner track is Earth).
Each impactor gets a path of distance from Earth against time, from where and when it formed to the moment it
struck. The data, with every quote, URL and Jev check, is in [`impacts.json`](impacts.json). All sources were
accessed 6 Nov 2026. Each quote below was found verbatim in the fetched text: the page itself, the Europe PMC
abstract or open-access full text, or text extracted from the PDF.

Jev (jev-1.14.0) scored each claim against its quote ("supported", probability). All but two of the 61 facts score 0.6 or
higher. The two lower scores (0.52, `mars_radius` and `earth_semimajor_axis`) are rows of NASA fact-sheet tables
that Jev reads poorly; the numbers match the tables.
Jev also checked each prose paragraph below against the full set of quotes. The paragraphs it flagged fall into
three groups:

- Paragraphs describing how the drawing is built: the schematic distances, the start dates and the LHB size.
- The LHB-debate and Baptistina bullets, which quote sources that disagree with each other, as intended.
- Two paragraphs that use details from pages not quoted in the JSON: the Apollo samples (MPS release), and the
  L4/L5 point lying 1 AU from Earth, which follows from geometry.

Treat these flags as cues for a reviewer, not as errors.

**Was Chicxulub a comet? No.** The best evidence says it was an asteroid. Ruthenium isotopes in the boundary
layer match carbonaceous chondrites: "the Chicxulub impactor was a carbonaceous-type asteroid, which had formed
beyond the orbit of Jupiter" (Fischer-Gödde et al. 2024, *Science*). The lead author calls a comet "unlikely".
The 2021 long-period-comet proposal (Siraj & Loeb) was criticised in print for needing extreme assumptions. So it
formed in the outer Solar System, beyond Jupiter, but as a carbonaceous asteroid, not an icy comet. It most likely
reached Earth from the main asteroid belt (section 3).

## How to read the paths

- `log10DistanceM` is the distance from Earth in metres. Before Earth existed, it is the distance from the 1 AU
  region where Earth formed. 1 AU = 149.598 million km (log10 11.175).
- **measured**: the point's date (and, for impacts, its place) rests on a cited measurement.
  **schematic**: a defensible illustration. Real orbits swing in and out every year or so, and some stages
  (when a body left the belt, for instance) are unknown. Distances are orbit-averaged.
- Impact points use `log10DistanceM = 0`: the path joins the inner track, which is drawn at human scale by
  convention (`MatterScale.InnerMetres = 1 m`). Each impactor also has `contactLog10M`, the centre-to-centre
  distance at contact (Earth's radius is 6371 km, log10 6.80).

| Impactor | Formed | Struck | Size | Path (ya → log10 m) |
| --- | --- | --- | --- | --- |
| Theia | ~4.565 Ga, inner Solar System (schematic) | 4.51 Ga (range 4.51-4.35) | Mars-sized, ~6,780 km | 4.565 Ga 11.18 → 4.53 Ga 11.18 → 4.5105 Ga 10.0 → 4.51 Ga impact |
| LHB, belt asteroid | ~4.565 Ga, asteroid belt | ~3.9 Ga (Imbrium) | >10 km (schematic) | 11.48 → stays until ~3.95 Ga → 11.18 at 3.92 Ga → impact 3.9 Ga |
| LHB, leftover planetesimal | ~4.565 Ga, 0.5-1.5 AU | ~3.9 Ga | >10 km (schematic) | 10.87 → 10.87 at 4.2 Ga → impact 3.9 Ga |
| Chicxulub | ~4.5648 Ga, beyond Jupiter | 66.038 Ma | ~10-14 km | 12.08 → 11.65 at 4.562 Ga → 11.65 to 67.2 Ma → 11.18 at 66.6 Ma → impact |

## 1. Theia and the Moon-forming impact

**Size.** NASA describes the impactor as "perhaps a single object the size of Mars" striking "about 4.5 billion
years ago" ([NASA](https://science.nasa.gov/moon/formation/)). Mars's mean radius is 3389.5 km
([NASA fact sheet](https://nssdc.gsfc.nasa.gov/planetary/factsheet/marsfact.html)), so the drawing uses a
diameter of 6.78e6 m. This size is not settled. Canup (2012) "simulated impacts involving larger impactors than
previously considered" to match the Moon's Earth-like composition
([Science](https://doi.org/10.1126/science.1226073)).

**Where it formed.** Hopp et al. (*Science*, 20 Nov 2025) measured iron isotopes in Apollo samples, Earth rocks
and meteorites: "all of Theia and most of Earth's other constituent materials originated from the inner Solar
System. Our calculations suggest that Theia might have formed closer to the Sun than Earth did"
([doi](https://doi.org/10.1126/science.ado0623)). The lead author's summary: "Earth and Theia are likely to have
been neighbors" ([MPS](https://www.mpg.de/25743917/theia-and-earth-were-neighbors)). A distinct, older hypothesis
puts Theia at Earth's L4 or L5 Lagrange point, which is always 1 AU from Earth. It would then have been knocked
onto a slow, "zero-energy parabolic trajectory" into Earth
([Belbruno & Gott 2005](https://arxiv.org/abs/astro-ph/0405372)). Either way, its pre-impact distance from Earth
is of order 1 AU. The path draws ~1 AU (log10 11.18) and marks it schematic.

**When.** No study found here dates Theia's own formation. The path assumes it grew within a few million years of
the first solids, which formed 4567.30 ± 0.16 Ma ([Connelly et al. 2012](https://doi.org/10.1126/science.1226919)),
and starts at ~4.565 Ga. This start point is a schematic assumption, not sourced. Estimates of the impact date span "between 4.35 and 4.51 billion years
ago" ([Nimmo et al. 2024](https://doi.org/10.1038/s41586-024-08231-0)):

- 4.51 Ga from Apollo 14 zircons, i.e. "within the first ~60 million years"
  ([Barboni et al. 2017](https://doi.org/10.1126/sciadv.1602365)). The path uses this date, which is also
  matter.json's `moon_impact`.
- "approximately 50 Myr after the solar system" (~4.52 Ga) from Hf-W
  ([Thiemens et al. 2019](https://doi.org/10.1038/s41561-019-0398-3)).
- 4.425 ± 0.025 Ga from a long-lived magma ocean model ([Maurice et al. 2020](https://doi.org/10.1126/sciadv.aba8949)).
- Nimmo et al. (2024) read the frequent ~4.35 Ga ages as "a remelting event driven by the Moon's orbital evolution
  rather than the original crystallization" of the magma ocean, which "permits formation of the Moon within a
  few tens of million years of Solar System formation".

## 2. The Late Heavy Bombardment

**The proposal.** The lunar record suggested "a cataclysmic spike in the cratering rate occurred approximately
700 million years after the planets formed" ([Gomes et al. 2005](https://doi.org/10.1038/nature03676)), "a
hypothesized impact spike at ∼3.9 Ga" ([Boehnke & Harrison 2016](https://doi.org/10.1073/pnas.1611535113)).
(Separately, the project's matter.json places the LHB epoch marker at 4.1 Ga, with a blurb span of 4.1-3.8 Ga.)

**Sources of the impactors.**

- *Giant-planet migration (Nice model).* "the LHB was triggered by the rapid migration of the giant planets". The
  planetesimal disk beyond the planets was destabilised, and "The asteroid belt was also strongly perturbed, with
  these objects supplying a significant fraction of the LHB impactors" (Gomes et al. 2005).
- *Leftovers of planet building.* A 2023 model finds "The contribution of cometary impactors is found to be
  insignificant" and "The great majority of early impactors were rocky planetesimals left behind at ∼0.5–1.5 au"
  ([Nesvorný et al. 2023](https://arxiv.org/abs/2303.17736)).

**The debate.** Was there a spike at all?

- Argon-argon age histograms may mislead: "monotonically declining impact histories yield apparent age peaks that
  could be misinterpreted as LHB-type events" (Boehnke & Harrison 2016).
- A review concludes "most evidence supports a prolonged lunar (and thus, terrestrial) bombardment from ~4.2 to 3.4
  Ga and not a cataclysmic spike at ~3.9 Ga" ([Zellner 2017](https://arxiv.org/abs/1704.06694)).
- Morbidelli et al. (2018) show that the same data fit both scenarios. Under the traditional assumption, "The
  cataclysm should have started ~3.95Gy ago". If the lunar magma ocean crystallised late, the accretion tail works
  ([arXiv](https://arxiv.org/abs/1801.03756)).
- One firm date: the Imbrium basin formed "some ≃3.9 Ga" (Nesvorný et al. 2023).

**For the drawing.** The LHB was a population of impactors, not one body. Two representative paths are given:

- a belt asteroid shaken loose at ~3.95 Ga, the classic picture;
- a leftover planetesimal from near Earth's orbit, the accretion-tail picture.

Both strike at 3.9 Ga (Imbrium). No sourced size was found for a single LHB impactor. The 1e4 m size reflects the
"d>10 km" population that Nesvorný counts, and is schematic: basin-forming bodies were probably larger (not sourced here).

## 3. The Chicxulub impactor

**What it was.** A carbonaceous-type asteroid that formed beyond Jupiter
([Fischer-Gödde et al. 2024](https://doi.org/10.1126/science.adk4868), verified in the Science abstract). "It is
therefore 'unlikely' the impactor in question was a comet". The lead author also said "We cannot be really sure
where the asteroid was kind of hiding just before it impacted on Earth" and that it "may have made a stopover in
the asteroid belt" ([phys.org/AFP](https://phys.org/news/2024-08-scientists-dino-asteroid-jupiter.html)). The comet
idea came from [Siraj & Loeb 2021](https://doi.org/10.1038/s41598-021-82320-2). Its critics wrote that it
"requires extreme assumptions about how comets fragment ... and a blind eye to the evidence of the iridium layer"
([Desch et al. 2021](https://arxiv.org/abs/2105.08768)).

**Size, speed, angle, energy.**

- **Size:** "∼10 km in diameter", leaving "a crater ∼180 to 200 km in diameter"
  ([Chiarenza et al. 2020](https://doi.org/10.1073/pnas.2006087117)). Crater models give a little more: "For an
  asteroid speed of around 18 km s–1 and impact angles of between 45° and 60°, impactor diameters of around
  12.2–14.4 km are required" ([Morgan et al. 2022](https://par.nsf.gov/servlets/purl/10349743), *Nat. Rev. Earth
  Environ.*). The JSON uses 1.2e4 m.
- **Speed:** ~20 km/s, "approximately the average speed that asteroids encounter Earth" and "more representative of
  the likely impact speed" ([Collins et al. 2020](https://doi.org/10.1038/s41467-020-15269-x)). Morgan et al. use
  ~18 km/s. The true speed is not measured.
- **Angle and direction:** "steeply-inclined (45–60° to horizontal) impact from the northeast; several lines of
  evidence rule out a low angle (<30°)" (Collins et al. 2020).
- **Energy:** "about 10²³ joules" (Morgan et al. 2022).

**Date.** "The new date for the impact – 66,038,000 years ago – is the same within error limits as the date of the
extinction" ([Berkeley, on Renne et al. 2013](https://news.berkeley.edu/2013/02/07/new-evidence-comet-or-asteroid-impact-was-last-straw-for-dinosaurs/)).
The impact and the extinction are synchronous "to within 32,000 years"
([Renne et al. 2013](https://doi.org/10.1126/science.1230492)). matter.json rounds this to 66.0 Ma.

**When such bodies formed.** "chondrite parent bodies ... accreted 2-3 Myr after the Solar System onset"
([2025 model paper](https://arxiv.org/abs/2510.19994)). The carbonaceous reservoir beyond Jupiter stayed separate
from the inner one "between ∼1 My and ∼3-4 My after Solar System formation"
([Kruijer et al. 2017](https://doi.org/10.1073/pnas.1704461114)). The path starts at ~4.5648 Ga (CAIs plus ~2.5 Myr).

**How it reached the belt.** Gas accretion by Jupiter and Saturn scattered planetesimals inward, "populating the
outer main belt with C-type asteroids that originated from a broad (5-20 AU-wide) region"
([Raymond & Izidoro 2017](https://arxiv.org/abs/1707.01234)). This is a model, but it fits a carbonaceous body
formed beyond Jupiter that then sat in the belt. A dynamical model of large near-Earth asteroids concludes the K/Pg
impactor "was a main belt asteroid that quite likely (≃60% probability) originated beyond 2.5 au"
([Nesvorný et al. 2021](https://arxiv.org/abs/2107.03458)).

**When and how it left the belt: unknown.**

- *Baptistina hypothesis.* [Bottke et al. 2007](https://doi.org/10.1038/nature06070) proposed that the Baptistina
  parent "broke up 160(-20)+30Myr ago in the inner main asteroid belt" and was the "most likely source (>90 per
  cent probability)".
- *Its refutation.* WISE found it "broke up closer to 80 million years ago, half as long as originally proposed",
  so a fragment would have had to reach Earth "in just about 15 million years". Delivery through a resonance "is
  thought to normally take many tens of millions of years". "The asteroid family that produced the
  dinosaur-killing asteroid remains at large" ([NASA/JPL release, 2011, via Astronomy](https://www.astronomy.com/science/nasas-wise-raises-doubt-about-asteroid-family-believed-responsible-for-dinosaur-extinction/)).
  Spectra show (298) Baptistina "is similar to LL-type ordinary chondrites rather than CM2 carbonaceous chondrites"
  ([Reddy et al. 2011](https://arxiv.org/abs/1110.3414)). A later WISE age is 140-320 Myr
  ([Masiero et al. 2012](https://arxiv.org/abs/1209.1430)). The decisive point is the 2024 result: a
  carbonaceous impactor does not match an LL-type family.
- *What is known about the last stage.* Near-Earth objects have "a relatively short life cycle (∼ 0.1-100 Myr)",
  and the mean dynamical lifetime of near-Earth asteroids larger than 5 km is ~1.2 Myr (Nesvorný et al. 2021).
  The path therefore leaves the belt at 67.2 Ma, about a Myr before impact. This is typical, not known.

**From impact to extinction.**

1. *The impact.* The asteroid struck carbonate- and sulfate-rich rock, releasing "hundreds of Gigatonnes (Gt) of
   sulfates" with "3 to 16 y of subfreezing temperatures" (Chiarenza et al. 2020). Soot from the oil-rich target
   "was spread globally" ([Kaiho et al. 2016](https://doi.org/10.1038/srep28427)).
2. *Darkness and cold.* Ejecta and wildfire soot "caused the Earth to become dark and cold for about a decade"
   (Morgan et al. 2022). The review adds that "it remains uncertain whether this impact winter lasted for many
   months or for more than a decade".
3. *Habitat loss.* "Asteroid impact models generate a prolonged cold winter that suppresses potential global
   dinosaur habitats", and the extinction "included the demise of non-avian dinosaurs" (Chiarenza et al. 2020).
4. *Scale of the loss.* "around three-quarters of all living species" (Morgan et al. 2022); "around 75% of species
   in the fossil record" ([Nature, 2026](https://doi.org/10.1038/s41586-026-10541-4)). Kaiho et al. (2016) note:
   "Only 12% of the land-dwelling forms survived, but 90% of species in the freshwater assemblage survived".
5. *Cause, not coincidence.* Schulte et al. (2010) conclude "the Chicxulub impact triggered the mass extinction"
   ([Science](https://doi.org/10.1126/science.1177265)). Deccan outgassing began and ended "distinctly before the
   impact, with only the impact coinciding with mass extinction"
   ([Hull et al. 2020](https://doi.org/10.1126/science.aay5055)).

## Open questions

- **Impact speed and size:** the speed is not measured (crater models assume 12-20 km/s), so the size is
  model-dependent: ~10 km (common) or 12.2-14.4 km (Morgan et al. 2022).
- **Leaving the belt:** which family, and when, is unknown. The Nesvorný 2021 model gives ~60% odds for a source
  beyond 2.5 AU, from the outer-belt resonances.
- **Theia:** its size (Mars-sized or larger), its formation time, and whether it was co-orbital (L4/L5) or on a
  neighbouring orbit inside Earth's are all open. The Moon's age is still debated, from 4.51 to 4.35 Ga.
- **The LHB:** spike versus tail is still debated. The giant-planet instability's date is a separate debate not
  sourced here. If the lead wants a single LHB line, the accretion-tail planetesimal is the safer default given
  2017-2023 work.
- The Fischer-Gödde paper's full text was not accessible (paywalled). The comet statement rests on the lead
  author's quoted remarks (AFP via phys.org) and on the abstract's word "asteroid".

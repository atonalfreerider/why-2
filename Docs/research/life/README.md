# Life research: the mass extinctions

Facts behind the tree of life's collapses and rebounds (`Assets/Resources/Data/life_clades.json`, events with `loss` and
`recoveryMyr`) and the present-day collapse of wild biomass (the farm layer's wild mammals stream).

- `extinctions-big-five.json`: the end-Ordovician, Late Devonian, end-Permian, end-Triassic and end-Cretaceous extinctions: dates,
  shares of species and genera lost, recovery times by measure, and evidence of collapsed productivity or biomass.
- `extinctions-present.json`: the present-day (sixth) extinction: wild mammal and plant biomass against pre-human baselines
  (Bar-On et al. 2018; Erb et al. 2018), extinction rates and threatened shares (IPBES 2019), the Living Planet Index (a change in
  population size, not biomass), and the dating of the late Pleistocene megafaunal extinctions.

Every fact passed the truth gate on 7 Nov 2026: its quote found verbatim on the cited page, its values inside the quote, and Jev
(TypeSafe System One) judging that the text around the quote supports the claim (0.6 or more). Re-run it with

    python3 -I Tools/advantage/verify.py --dir Docs/research/life --cluster extinctions-big-five

A second, skeptical reader then checked each fact's measure (marine species against genera or all species; biomass against
population size; a recovery time against the event's duration) and its span. Its verdict is the fact's `check`; a fact with
`check.ok` false is kept for the record but never used.

Deep-time biomass is not measured. The tree's dip depth is therefore the share of species lost, and its rebound the recovery time a
source gives for the stated measure; the labels say both. Only the present-day collapse is measured biomass.

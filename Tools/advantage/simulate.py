#!/usr/bin/env python3
"""The Darwinian simulation of the 2026 corporate biosphere (Docs/hills-design/ADVANTAGE.md, section 8).

A simulation, not a forecast. Every rule is plain code below; every number in it is an assumption, listed with its
reason in P, LAYERS and WORLDS and copied into the output's "assumptions". No randomness: the same input gives the same
output. Python 3, standard library only.

Usage: python3 -I Tools/advantage/simulate.py INPUT.json OUTPUT.json      (build.py calls simulate(data) directly)

The input lists companies (market value, P/E, revenue, net income, growth, cash, debt, equity, capex, commitments, burn,
Jev's advantage A with its durability and scenario scores, dependencies, owners) and the strategist's scenarios. A
missing number gets a fallback by layer, named per company in "fallbacks" as "field: what was used". The output holds
one world per course: "now" (2026), "2030" (the most likely course, moved by each company's durability score) and each
scenario (moved by its Jev scores and its parameters in WORLDS). Each year from 2027, for every company alive:

 1 Advantage  A_t = clamp(A + 0.9 (score - 2) f, 0, 7), f = 0, 1/3, 2/3, 1, 1 for 2026..2030.
 2 Revenue    base x shock x lost. The base grows at the recent growth (capped) fading toward 5%, plus 3 points a year
              per step of the score, times f. The shock is the scenario's at the year's intensity: flowScale where it
              names the company or the rivers into it (a river by its own key, else its payee's, else its payer's),
              else the premise's own shock for the company. Lost is the rivers once paid by customers now dead.
 3 Earnings   The margin before interest fades toward the layer's norm, keeping 0.6 + 0.05 A_t of the gap a year (a
              power is a margin rivals cannot compete away; a loss narrows the same way), plus the scenario's margin
              shock. A revenue shortfall hits it through fixed costs, m - (1 - m) phi (1/shock - 1), and commitments
              due are fixed costs. Net income = revenue x margin - interest.
 4 Value      The 2026 price is the market's forecast: enterprise value (stakes held aside) grows at 5% a year, times
              earnings power against the path the market expected (growth fading, margins reverting, A unchanged),
              times (A_t + 1)/(A + 1), times M_t^s. Earnings power is half this year's and half last year's, grown;
              for a 2026 loss-maker, or a firm priced over 60x earnings, it is revenue x its layer's long-run margin.
              M_t is the world's multiple factor; s = the 2026 EV/earnings multiple / 25, within 0.5..2.5, so the
              most speculative shell bursts hardest. Market value = EV - debt + cash + the stakes held, at value.
 5 Cash       + net income + depreciation - capex. Above 35% of revenue in cash, 80% of earnings goes to owners.
 6 Capital    Below a buffer (10% of revenue plus a year of burn) a company raises equity while markets are open and
              its shell is at least 1.5 times book (first, if over 5 times: the share price as a weapon), and debt while
              lenders accept it: 3.5 x EBITDA, or its 2026 debt times its earnings power against the expected path
              times the world's credit, if more. An eighth of debt matures a year; what lenders will not roll is paid.
 7 Death      Still short after cutting capex to depreciation, it is absorbed by its largest owner or partner that can
              pay its EV (cash above its buffer, then debt, then stock), which keeps 80% of its revenue at its own
              layer's long-run margin and honours half its commitments; with no such partner it goes extinct. Its
              customers lose the rivers it paid (half, if absorbed); holders get their share of the price, or nothing.
"""
import json
import math
import re
import sys

P = {  # name: (value, why)
    "scoreStep": (0.9, "altitude points per step of Jev's 0-4 answer, as build.py composes the scenarios"),
    "rampYears": (3, "a judged change arrives over three years: f is 0 in 2026 and 1 from 2029"),
    "growthCap": (0.6, "recent growth above 60% a year (NVIDIA +106%, Micron +379%) is a spike, not a rate to project"),
    "growthFloor": (-0.3, "nor is a fall steeper than 30% a year"),
    "growthLongRun": (0.05, "the nominal growth every company fades toward, and the drift of an expected value"),
    "growthFade": (0.55, "growth above 5% keeps 55% of its excess a year: a boom halves in about a year"),
    "growthPerStep": (0.03, "each step of Jev's answer adds or takes 3 points of growth a year: the lasting change in "
                      "position, while the event itself is the scenario's shock"),
    "marginKeep": ((0.6, 0.05), "a margin keeps 0.6 + 0.05 A of its gap to the norm a year (A 7 keeps 95%, A 2 70%): "
                   "a power is a margin rivals cannot compete away (Helmer); a loss narrows the same way"),
    "fixedMax": (0.9, "at most 90% of costs are fixed, however large the commitments"),
    "marginRange": ((-3.0, 0.75), "a loss beyond three times revenue, or a margin above 75%, is not modelled"),
    "lookThrough": (0.5, "markets weigh this year's earnings and last year's, grown, equally: a one-year shock is "
                    "half priced"),
    "senseRef": (25, "an EV of 25 times earnings moves one for one with the market factor"),
    "senseRange": ((0.5, 2.5), "a cheap shell (memory near 5x) moves half as much, a speculative one 2.5 times"),
    "capitalisedMin": (0.02, "a loss-maker is never valued on a long-run margin below 2%"),
    "speculative": (60, "a company priced at over 60 times its earnings (Tesla, Palantir, Arm, AMD) is valued like a "
                    "loss-maker, on what it may earn: the market is not pricing what it earns now"),
    "depShare": (0.5, "depreciation is half of a reported capex for a growing asset base; the other half is growth"),
    "buffer": (0.1, "a company keeps cash of 10% of revenue, plus a year of any burn"),
    "payout": ((0.35, 0.8), "above 35% of revenue in cash, 80% of the year's earnings goes back to owners"),
    "raise": ((0.10, 0.15), "a listed company can sell a tenth of itself a year, a private lab a seventh in a round"),
    "shellLarge": (1.5, "equity is sold only from a shell at least 1.5 times book: the premium makes it cheap money"),
    "equityFirst": (5.0, "a shell over five times book raises equity before debt, as Alphabet (5.3x) did in 2026"),
    "leverage": (3.5, "lenders always roll 3.5 x EBITDA, and keep their 2026 lending while earnings power meets the "
                 "expected path, times the world's credit (how contract-backed builders borrow beyond 3.5x)"),
    "repay": (0.125, "an eighth of debt matures a year; what lenders will not roll must be paid"),
    "spread": ((0.01, 0.045), "debt costs the world's rate plus 1 point, up to 4.5 points at 6x EBITDA or a loss"),
    "absorbKeep": (0.8, "an absorber keeps 80% of its target's revenue, run at the absorber's long-run margin"),
    "riverKeep": (0.5, "an absorbed company's purchases and commitments continue at half; an extinct one's stop"),
    "riverMax": (0.5, "no one customer's river is more than half a company's revenue in the contagion: contracted "
                 "rivers are partly revenue still to come (OpenAI's $60bn a year to Oracle starts in 2027)"),
    "burnCap": (1.5, "a burn above 1.5 times revenue is a multi-year or non-cash figure, so it is capped"),
    "defaultA": (3.5, "an unjudged company stands mid-scale"),
    "durabilityInScenarios": (False, "as specified, a scenario moves A by its own score only, so the drift Jev expects "
                              "anyway (durability) shows only in 2030; True adds it, making each scenario a deviation "
                              "from the most likely course"),
    "commitYears": (4, "commitments without a term run four years"),
    "burst": (0.3, "a fall of 30% or more of market value in a year is a burst shell"),
    "eventMin": (1.0, "raises under $1bn are not listed as events"),
}
# price/sales, margin now, long-run margin, growth, cash (share of revenue; of valuation for labs), debt/revenue,
# price/book, fixed share of costs, depreciation/revenue
LAYER_KEYS = ("ps", "margin", "norm", "growth", "cash", "debt", "pb", "fixed", "dep")
LAYERS = {
    "energy": ((3.0, 0.12, 0.10, 0.08, 0.15, 0.30, 4.0, 0.35, 0.08), "utilities and turbine makers: heavy assets"),
    "tools": ((9.0, 0.28, 0.24, 0.10, 0.30, 0.15, 10.0, 0.30, 0.04), "toolmakers and design IP: high margins"),
    "fabs": ((5.0, 0.30, 0.20, 0.10, 0.30, 0.15, 3.5, 0.45, 0.15), "foundries and memory: depreciation fixes costs; "
             "margins revert (build.py's band also holds pharma and carmakers)"),
    "memory": ((3.5, 0.30, 0.15, 0.08, 0.25, 0.10, 3.0, 0.45, 0.15), "memory alone, if a layer says so"),
    "chips": ((12.0, 0.30, 0.22, 0.20, 0.25, 0.15, 12.0, 0.25, 0.03), "chip designers: fabless, costs flex"),
    "systems": ((1.5, 0.06, 0.06, 0.08, 0.10, 0.30, 8.0, 0.15, 0.02), "server and network assemblers: thin margins"),
    "clouds": ((8.0, 0.25, 0.22, 0.15, 0.25, 0.20, 7.0, 0.30, 0.12), "clouds and platforms (build.py's band also "
               "holds banks and payments)"),
    "models": ((18.0, -0.60, 0.20, 0.60, 0.12, 0.0, 6.0, 0.25, 0.05), "labs lose about 60 cents a dollar (OpenAI "
               "burned $3.7bn on $5.7bn in Q1 2026), aim at 20%, and hold about 12% of their valuation in cash"),
    "apps": ((8.0, 0.20, 0.18, 0.10, 0.25, 0.20, 10.0, 0.30, 0.05), "consumer platforms and apps"),
    "capital": ((3.0, 0.25, 0.20, 0.05, 0.50, 0.30, 1.6, 0.30, 0.02), "banks, holding companies and investors"),
    "economy": ((3.5, 0.13, 0.12, 0.06, 0.12, 0.30, 5.0, 0.30, 0.05), "the rest, and any layer not named"),
}
BUILDERS = ({"clouds", "models"}, "their capex follows the world's AI build-out; everyone else's follows its revenue")
INVESTORS = ({"venture-capital", "softbank", "households", "us-gov", "gic", "mgx", "blue-owl", "coatue", "magnetar",
              "thrive-capital", "a16z", "sequoia", "kkr", "blackstone", "apollo", "pif", "qia", "temasek"},
             "a river from an investor or a shareholder is capital, whatever kind its edge carries")
ALIASES = {"vc": "venture-capital", "gev": "ge-vernova", "aws": "amazon", "lilly": "eli-lilly", "aramco": "saudi-aramco"}
WORLDS = {  # field: (value by year 2026..2030, or by company; why)
    "2030": {"M": ([1] * 5, "multiples hold: the most likely course is the one the market already prices"),
             "rate": ([.055] * 5, "the Fed holds at 3.75-4% (Oct 2026): investment-grade debt costs about 5.5%"),
             "open": ([1, .9, .9, .9, .9], "equity markets stay open"), "credit": ([1] * 5, "lenders keep 2026 terms"),
             "capex": ([1, 1.25, 1.35, 1.4, 1.45], "Alphabet signalled 2027 capex over $300bn, Microsoft $170bn for FY27"),
             "p": ([0] * 5, "no shock: each company moves by Jev's durability answer alone"),
             "shocks": ({}, "none"), "margins": ({}, "none")},
    "compounding": {
        "M": ([1, 1.08, 1.12, 1.08, 1.04], "AI revenue keeps pace with spending: multiples expand, then normalise"),
        "rate": ([.055, .055, .06, .06, .06], "the boom keeps inflation up; the Fed leans toward a rise"),
        "open": ([1] * 5, "equity markets wide open to AI"), "credit": ([1, 1.2, 1.2, 1.1, 1], "lenders chase it"),
        "capex": ([1, 1.35, 1.6, 1.75, 1.85], "capex passes $1tn in 2027 (from $725-828bn) and rises through 2028"),
        "p": ([0, .6, 1, 1, 1], "demand compounds from 2027"),
        "shocks": ({"micron": 1.4, "samsung": 1.25, "amd": 1.35, "broadcom": 1.35, "marvell": 1.3, "arista": 1.3,
                    "dell": 1.25, "foxconn": 1.15, "vertiv": 1.3, "constellation": 1.25, "siemens-energy": 1.2,
                    "applied-materials": 1.2, "lam-research": 1.2, "kla": 1.2, "tokyo-electron": 1.2, "intel": 1.1,
                    "xai": 1.4}, "beyond flowScale, the friendly list: chips, memory, packaging and power stay sold "
                   "out, like NVIDIA (1.4) and SK Hynix (1.5)"),
        "margins": ({"micron": .05, "samsung": .03}, "shortage pricing in memory")},
    "digestion": {
        "M": ([1, .85, .85, .92, .97], "a sector bust: the market falls 15%; the AI chain falls further through "
              "its earnings and its judged advantage"),
        "rate": ([.055, .065, .065, .06, .06], "the Fed stays near 4% and spreads widen"),
        "open": ([1, .2, .3, .6, .8], "markets close to new AI money for 18 months (venture flows at 0.3-0.4)"),
        "credit": ([1, .55, .6, .8, .9], "credit for data-centre builders tightens (the premise)"),
        "capex": ([1, .65, .62, .8, .9], "the hyperscalers cut capex 30-40% and hold it for at least 18 months"),
        "p": ([0, 1, 1, .6, .35], "the cut holds through 2028, then eases"),
        "shocks": ({"micron": .6, "samsung": .8, "cxmt": .8, "amd": .7, "broadcom": .75, "marvell": .7, "arista": .75,
                    "dell": .7, "foxconn": .8, "vertiv": .7, "siemens-energy": .85, "constellation": .95,
                    "applied-materials": .8, "lam-research": .8, "kla": .85, "tokyo-electron": .8, "smic": .9,
                    "intel": .9, "google": .97, "meta": .97, "alibaba": .9, "xai": .7, "deepseek": .85, "spacex": .9,
                    "palantir": .9, "softbank": .7}, "a capex cut is the suppliers' revenue: flowScale's NVIDIA 0.6 "
                   "and SK Hynix 0.5 carried along the chain, by how much of each one's sales is the build-out"),
        "margins": ({"micron": -.15, "sk-hynix": -.15, "samsung": -.08, "cxmt": -.1}, "memory prices fall faster than "
                    "volumes (SK Hynix's shares already fell 18.8% on the fear)")},
    "powerwall": {
        "M": ([1, .97, .95, .97, .99], "slower growth and dearer energy trim multiples"),
        "rate": ([.055, .06, .065, .065, .06], "power prices and Hormuz keep inflation up"),
        "open": ([1, .8, .75, .8, .9], "markets open, less eagerly"), "credit": ([1, .9, .85, .9, .95], "lenders "
                                                                                 "wait for grid connections"),
        "capex": ([1, .8, .7, .8, .9], "capacity comes online 30-50% slower: capex cannot be spent"),
        "p": ([0, .7, 1, 1, .8], "the wall binds through 2029 and eases as turbines arrive"),
        "shocks": ({"constellation": 1.35, "nextera": 1.2, "siemens-energy": 1.3, "vertiv": 1.15, "exxon-mobil": 1.1,
                    "saudi-aramco": 1.1, "xai": 1.2, "amd": .85, "broadcom": .85, "micron": .85, "samsung": .9,
                    "marvell": .85, "arista": .85, "dell": .8, "foxconn": .85}, "advantage moves from chips to watts: "
                   "power owners and turbine makers gain (flowScale's GE Vernova 1.5); the chain sells fewer racks"),
        "margins": ({"constellation": .05, "nextera": .03, "microsoft": -.02, "amazon": -.02, "google": -.02,
                     "meta": -.02, "oracle": -.03, "coreweave": -.05}, "dear power lifts the generators' margins and "
                    "the data centres' costs")},
    "taiwan": {
        "M": ([1, .7, .78, .88, .93], "a blockade is a war-risk shock for every market; it recovers as fabs move"),
        "rate": ([.055, .065, .065, .06, .06], "a supply shock is inflationary"),
        "open": ([1, .3, .5, .7, .85], "markets close in the crisis"), "credit": ([1, .6, .7, .85, .9], "so does credit"),
        "capex": ([1, .55, .7, .85, .95], "there are no chips to install: capex falls with TSMC's output"),
        "p": ([0, 1, .6, .35, .2], "TSMC's Taiwan output halts in 2027 and recovers over years (Arizona, Japan)"),
        "shocks": ({"amd": .4, "broadcom": .45, "marvell": .4, "arm": .7, "foxconn": .5, "dell": .6, "arista": .6,
                    "micron": .7, "applied-materials": .75, "lam-research": .75, "kla": .75, "tokyo-electron": .75,
                    "synopsys": .9, "cadence": .9, "intel": 1.3, "samsung": 1.1, "smic": 1.25, "huawei": 1.15,
                    "cxmt": 1.1, "coreweave": .8, "oracle": .85, "alibaba": .9, "tesla": .85, "spacex": .9,
                    "xai": .8}, "everyone above TSMC loses with it (flowScale: TSMC 0.2, NVIDIA 0.35, Apple 0.6); "
                   "Intel, Samsung and SMIC gain as the only leading-edge fabs left"),
        "margins": ({"intel": .05, "samsung": .03}, "scarce foundry capacity prices up")},
    "commodity": {
        "M": ([1, .95, .93, .95, .97], "the AI premium deflates where the model was the toll-gate"),
        "rate": ([.055] * 5, "no monetary shock"),
        "open": ([1, .85, .8, .8, .85], "public markets stay open; the labs' private money is cut by flowScale"),
        "credit": ([1, .9, .9, .9, .95], "lenders to GPU landlords grow wary"),
        "capex": ([1, .95, .9, .92, .95], "use keeps growing, but cheap models need less frontier compute"),
        "p": ([0, .7, 1, 1, 1], "open weights match each frontier model within months from 2027"),
        "shocks": ({"deepseek": 1.6, "alibaba": 1.1, "tencent": 1.05, "meta": 1.05, "walmart": 1.02, "softbank": .8},
                   "the friendly list: open-model makers and the firms that use AI gain; SoftBank loses with OpenAI"),
        "margins": ({"openai": -.12, "anthropic": -.12, "xai": -.1, "deepseek": -.05}, "prices per token fall 70-90% "
                    "a year: the closed labs' long-run margin falls from 20% toward 8%")},
    "clampdown": {
        "M": ([1, .92, .9, .92, .94], "a regulatory discount on the platforms and the frontier"),
        "rate": ([.055] * 5, "no monetary shock"),
        "open": ([1, .6, .6, .7, .75], "investors wait for licensing and liability to settle"),
        "credit": ([1, .85, .85, .9, .9], "lenders price liability and data-centre moratoria"),
        "capex": ([1, .85, .8, .85, .9], "power-use limits and a slower frontier cut the build-out"),
        "p": ([0, .6, 1, 1, 1], "the rules bind from 2027"),
        "shocks": ({"palantir": 1.15, "deepseek": 1.25, "alibaba": 1.05, "tencent": 1.05, "microsoft": .95,
                    "amazon": .97, "xai": .85, "amd": .9, "broadcom": .92, "micron": .9, "samsung": .95,
                    "softbank": .85}, "Western rules slow the Western frontier and the chain that sells to it; labs "
                   "outside the rules, and Palantir, built for compliance, gain"),
        "margins": ({"google": -.04, "meta": -.04, "apple": -.03, "microsoft": -.02, "amazon": -.02, "openai": -.05,
                     "anthropic": -.05, "xai": -.05}, "compliance costs billions a year and remedies cut margins")},
    "decoupling": {
        "M": ([1, .92, .92, .95, .97], "tariffs and a split technology market lower multiples"),
        "rate": ([.055, .06, .06, .06, .06], "broad tariffs on Chinese goods lift inflation"),
        "open": ([1, .8, .8, .85, .9], "markets stay open"), "credit": ([1, .9, .9, .95, .95], "credit a little dearer"),
        "capex": ([1, .95, .95, 1, 1], "the build-out barely changes"),
        "p": ([0, 1, 1, 1, 1], "the cut-off holds for at least three years from 2027"),
        "shocks": ({"cxmt": .6, "smic": .65, "tencent": .85, "alibaba": .85, "foxconn": .75, "byd": .8, "tesla": .92,
                    "applied-materials": .72, "lam-research": .72, "kla": .78, "tokyo-electron": .75, "synopsys": .9,
                    "cadence": .9, "intel": 1.1, "micron": .95, "amd": .95}, "no tools, servicing or design software "
                   "cross the line: China's fabs and the toolmakers' China sales fall (23% of ASML's Q3); Intel gains"),
        "margins": ({"apple": -.02, "tesla": -.02, "walmart": -.01}, "tariffs and rare-earth limits raise costs")},
    "turn": {
        "M": ([1, .78, .7, .8, .88], "as in 2000-2002: the richest multiples halve over 2027-28 (0.7 to the 2.5th "
              "power is 0.41), a 25x one falls 30%, then all recover part of it"),
        "rate": ([.055, .065, .065, .06, .055], "credit spreads widen (the premise)"),
        "open": ([1, .1, .25, .7, .9], "the equity window for loss-makers closes for about 18 months"),
        "credit": ([1, .7, .7, .85, .95], "lenders cut what they accept from leveraged builders"),
        "capex": ([1, 1.1, 1.15, 1.25, 1.35], "AI demand still grows 20-30% a year, so capex grows, more slowly"),
        "p": ([0, 1, 1, .6, .3], "the turn runs through 2027 and 2028, then eases; the buyback halt is carried by "
              "M (market value counts the cash kept, so a halt would read as a gain)"),
        "shocks": ({"amd": .85, "broadcom": .9, "marvell": .85, "micron": .85, "sk-hynix": .85, "samsung": .92,
                    "arm": .9, "arista": .9, "dell": .85, "tesla": .9, "spacex": .95, "xai": .85, "palantir": .9,
                    "softbank": .7}, "the unfriendly list beyond flowScale: AI demand grows more slowly than in the "
                   "base course, so the chain sells less; SoftBank's holdings fall with the labs"),
        "margins": ({}, "none")},
    "*": {"M": ([1, .95, .95, .97, 1], "a scenario without its own parameters: mild caution"),
          "rate": ([.055] * 5, "unchanged"), "open": ([1, .8, .8, .85, .9], "a little shut"),
          "credit": ([1, .9, .9, .95, 1], "a little tighter"), "capex": ([1] * 5, "flat"),
          "p": ([0, 1, 1, 1, 1], "its flowScale from 2027"), "shocks": ({}, "flowScale only"), "margins": ({}, "none")},
}


def v(name):
    return P[name][0]


def num(x):
    return float(x) if isinstance(x, (int, float)) and not isinstance(x, bool) and math.isfinite(x) else None


def clamp(x, lo, hi):
    return max(lo, min(hi, x))


def alnum(s):
    return re.sub(r"[^a-z0-9]", "", str(s).lower())


def at(seq, t):
    return seq[min(t, len(seq) - 1)]


def spread_of(debt, ebitda):
    lo, hi = v("spread")
    return lo if debt <= 0 else hi if ebitda <= 0 else lo + (hi - lo) * clamp((debt / ebitda - 2) / 4, 0, 1)


def altitude(A, V):
    """ADVANTAGE.md, section 6: judged advantage and command of capital, 10..150."""
    return 10.0 if V <= 0 else round(clamp(150 * (0.55 * A / 7 + 0.45 * math.log10(V / 10) / math.log10(500)), 10, 150), 1)


def fade(m, norm, A):
    """A margin above its norm, or a loss, keeps 0.6 + 0.05 A of its gap to the norm a year."""
    return norm + (m - norm) * (v("marginKeep")[0] + v("marginKeep")[1] * A) if m > norm or m < 0 else m


def prepare(c, rate0):
    """A company's 2026 state, with a named fallback for every missing number."""
    layer = c.get("layer") if c.get("layer") in LAYERS else "economy"
    L, notes = dict(zip(LAYER_KEYS, LAYERS[layer][0])), []

    def fb(field, value, how):
        notes.append(f"{field}: {how}")
        return value
    V, R, NI, pe = (num(c.get(k)) for k in ("marketCap", "revenue", "netIncome", "pe"))
    pe, R = (pe if pe and pe > 0 else None), (R if R and R > 0 else None)
    if not V or V <= 0:
        if not R:
            return None, ["not simulated: neither a market value nor revenue"]
        V = fb("marketCap", R * L["ps"], f"revenue x {L['ps']:g}, the {layer} price-to-sales")
    if pe and (NI is None or NI <= 0 or not 0.4 < V / NI / pe < 2.5):
        NI = fb("netIncome", V / pe, "market value / P/E" + ("" if NI is None else f" (the given {NI:g} disagrees)"))
    burn = num(c.get("burn"))
    if NI is None and burn and burn > 0:
        if R and burn > v("burnCap") * R:
            burn = fb("burn", v("burnCap") * R, f"{burn:g} capped at {v('burnCap'):g} x revenue")
        NI = fb("netIncome", -burn, "minus the burn")
    if not R:
        R = V / L["ps"]
        if NI and NI > 0:
            R = fb("revenue", math.sqrt(R * NI / L["margin"]) if L["margin"] > 0 else R,
                   f"log-mean of value / {L['ps']:g} and net income / {L['margin']:g} ({layer})")
        else:
            R = fb("revenue", R, f"market value / {L['ps']:g}, the {layer} price-to-sales")
    debt, cash, eq = num(c.get("debt")), num(c.get("cash")), num(c.get("equity"))
    if debt is None:
        debt = fb("debt", L["debt"] * R, f"revenue x {L['debt']:g} ({layer})")
    if cash is None:
        lab = layer == "models"
        cash = fb("cash", L["cash"] * (V if lab else R), f"{'valuation' if lab else 'revenue'} x {L['cash']:g} ({layer})")
    capex = num(c.get("capex"))
    if capex is None or capex < 0:
        capex = dep = fb("capex", L["dep"] * R, f"unknown, taken as depreciation, revenue x {L['dep']:g} ({layer})")
    else:
        dep = v("depShare") * capex
    guess, sp = NI is None, v("spread")[0]
    for _ in range(2):   # the 2026 spread depends on EBITDA, which depends on the interest: two passes settle it
        interest = (rate0 + sp) * debt
        sp = spread_of(debt, (R * L["margin"] if guess else NI + interest) + dep)
    interest = (rate0 + sp) * debt
    if guess:
        NI = fb("netIncome", R * L["margin"] - interest, f"revenue x {L['margin']:g} ({layer}), less interest")
    OE = NI + interest
    if eq is None:
        eq = fb("equity", V / L["pb"], f"market value / {L['pb']:g}, the {layer} price-to-book")
    g0 = num(c.get("growth"))
    if g0 is None:
        g0 = fb("growth", L["growth"], f"{L['growth']:g} a year ({layer})")
    commit, years = max(num(c.get("commitments")) or 0.0, 0.0), num(c.get("commitmentYears"))
    if commit and not years:
        years = fb("commitmentYears", v("commitYears"), f"{v('commitYears')} years")
    A, dur = num(c.get("A")), num(c.get("durability"))
    if A is None:
        A = fb("A", v("defaultA"), f"{v('defaultA')}, mid-scale")
    if dur is None:
        dur = fb("durability", 2.0, "2 (holds)")
    basis = "earnings" if NI > 0 and V + debt - cash < v("speculative") * OE else "revenue"
    OEs = OE if basis == "earnings" else R * max(L["norm"], v("capitalisedMin"))
    mb = clamp(OE / R, *v("marginRange"))
    return {"id": c["id"], "layer": layer, "V0": V, "V": V, "R0": R, "R": R, "Rb": R, "Rx": R, "NI": NI, "OE0": OEs,
            "eq": eq, "cash": cash, "debt": debt, "capex0": capex, "dep0": dep, "mb": mb, "mx": mb, "norm": L["norm"],
            "fixed": L["fixed"], "g0": clamp(g0, v("growthFloor"), v("growthCap")), "left": commit,
            "due": commit / years if commit else 0.0, "A": clamp(A, 0, 7), "A_t": clamp(A, 0, 7), "dur": dur,
            "scen": {k: num(x) for k, x in (c.get("scenario") or {}).items() if num(x) is not None}, "basis": basis,
            "OEs": OEs, "OExs": OEs, "debt0": debt, "spread": sp,
            "pe_k": pe / (V / NI) if pe and NI > 0 else 1.0, "rho": v("raise")[bool(c.get("private"))],
            "builder": layer in BUILDERS[0], "status": "alive", "by": None, "died": None, "lost": 1.0, "raw": c}, notes


def link(S):
    """Edges (capital told apart from revenue), the stakes each company holds, its partners, and its own EV."""
    rank = {"capital": 0, "commitment": 1, "customer": 2, "supplier": 3}
    for c in S.values():
        c["owners"] = {o["id"]: num(o.get("pct")) / 100 for o in c["raw"].get("owners") or []
                       if o.get("id") and num(o.get("pct"))}
        c["edges"], c["holds"] = [], {}
        for e in c["raw"].get("dependsOn") or []:
            usd, kind = num(e.get("usdBn")), e.get("kind") or "customer"
            if e.get("id") and usd and usd > 0:
                cap = kind == "capital" or (kind != "supplier" and (e["id"] in INVESTORS[0] or e["id"] in c["owners"]))
                c["edges"].append({"id": e["id"], "kind": "capital" if cap else kind, "usd": usd})
        c["revSum"] = sum(e["usd"] for e in c["edges"] if e["kind"] in ("customer", "commitment"))
        c["capSum"] = sum(e["usd"] for e in c["edges"] if e["kind"] == "capital")
    pays = {e["id"] for c in S.values() for e in c["edges"] if e["kind"] == "capital"}
    for c in S.values():
        c["pays"] = c["id"] in pays or any(e["kind"] == "supplier" for e in c["edges"])
        for h, pct in c["owners"].items():
            if h in S and h != c["id"]:
                S[h]["holds"][c["id"]] = pct
        for e in c["edges"]:
            if e["kind"] == "capital" and e["id"] in S and e["id"] != c["id"]:
                S[e["id"]]["holds"].setdefault(c["id"], min(0.5, e["usd"] / c["V0"]))
        order = [h for h, _ in sorted(c["owners"].items(), key=lambda x: -x[1])]
        order += [e["id"] for e in sorted(c["edges"], key=lambda e: (rank.get(e["kind"], 4), -e["usd"]))]
        c["partners"] = [p for i, p in enumerate(order) if p in S and p != c["id"] and p not in order[:i]]
    for c in S.values():
        c["stakePrev"] = sum(pct * S[x]["V0"] for x, pct in c["holds"].items())
        c["EV0"] = max(c["V0"] + c["debt"] - c["cash"] - c["stakePrev"], 0.05 * c["V0"])   # its own business
        c["s"] = clamp(c["EV0"] / c["OE0"] / v("senseRef"), *v("senseRange"))


def river_pairs(fs):
    """flowScale's river keys as (payer, payee) pairs: 'str-flow-enterprises-openai' -> (enterprises, openai)."""
    pairs = {}
    for key, m in fs.items():
        parts = key.split("-flow-", 1)[1].split("-") if "-flow-" in key else []
        for i in range(1, len(parts)):
            a, b = "-".join(parts[:i]), "-".join(parts[i:])
            pairs[(alnum(ALIASES.get(a, a)), alnum(ALIASES.get(b, b)))] = m
    return pairs


def exposure(c, fs, pairs, own):
    """A company's full scenario shock to revenue, and to the capital it can raise. An actor's key scales the rivers
    into it, or, if it has none, the rivers out of it (flowScale's rule); otherwise the premise's own shock applies."""
    me = alnum(c["id"])

    def river(payer):
        m = pairs.get((alnum(payer), me))
        return m if m is not None else fs.get(c["id"], fs.get(payer))
    seen, investors = {alnum(e["id"]) for e in c["edges"]}, {alnum(i) for i in INVESTORS[0]}
    loose = [(a in investors, m) for (a, b), m in pairs.items() if b == me and a not in seen]
    rev = [(e["usd"], river(e["id"])) for e in c["edges"] if e["kind"] in ("customer", "commitment")]
    cap = [(e["usd"], river(e["id"])) for e in c["edges"] if e["kind"] == "capital"]
    loose_rev, loose_cap = [m for inv, m in loose if not inv], [m for inv, m in loose if inv]
    if c["id"] in fs and (c["revSum"] or not c["pays"]):
        shock = fs[c["id"]]
    elif loose_rev or any(m is not None for _, m in rev):
        total, rest = max(c["R0"], c["revSum"]), (sum(loose_rev) / len(loose_rev) if loose_rev else 1.0)
        shock = (sum(u * (1.0 if m is None else m) for u, m in rev) + (total - c["revSum"]) * rest) / total
    else:
        shock = own.get(c["id"], 1.0)
    if c["capSum"]:
        scale = sum(u * (1.0 if m is None else m) for u, m in cap) / c["capSum"]
    else:
        scale = sum(loose_cap) / len(loose_cap) if loose_cap else 1.0
    return shock, scale


def ev(c, M, t):
    """Enterprise value: the 2026 EV grown at 5%, times earnings power against the expected path, advantage and M^s."""
    return (c["EV0"] * (1 + v("growthLongRun")) ** t * c["OEs"] / c["OExs"] * (c["A_t"] + 1) / (c["A"] + 1)
            * M ** c["s"])


def record(c, S, y):
    if c["status"] != "alive" and c["died"] != y:
        h = S.get(c["by"]) if c["by"] else None
        alt = altitude(h["A_t"], h["V"]) if h and h["status"] == "alive" else 10.0
        return {"v": 0, "eq": 0, "cash": 0, "debt": 0, "rev": 0, "ni": 0, "pe": None, "alt": alt,
                "status": c["status"], "by": c["by"]}
    return {"v": round(c["V"], 2), "eq": round(c["eq"], 2), "cash": round(max(c["cash"], 0.0), 2),
            "debt": round(c["debt"], 2), "rev": round(c["R"], 2), "ni": round(c["NI"], 2),
            "pe": round(c["V"] / c["NI"] * c["pe_k"], 1) if c["NI"] > 0 else None, "alt": altitude(c["A_t"], c["V"]),
            "status": c["status"], "by": c["by"]}


def step(c, wid, t, W, events, y):
    """One year of a living company: advantage, revenue, earnings, cash, value, and the capital it raises."""
    f, p, lt = min(1.0, t / v("rampYears")), at(W["p"], t), v("lookThrough")
    M, rate, opn, credit, K = (at(W[k], t) for k in ("M", "rate", "open", "credit", "capex"))
    score = c["dur"] if wid == "2030" else c["scen"].get(wid, 2.0) + (c["dur"] - 2) * v("durabilityInScenarios")
    c["A_t"] = clamp(c["A"] + v("scoreStep") * (score - 2) * f, 0, 7)
    gx = v("growthLongRun") + (c["g0"] - v("growthLongRun")) * v("growthFade") ** t
    grow = max(0.5, 1 + gx + v("growthPerStep") * (score - 2) * f)
    c["Rx"], c["mx"], c["Rb"] = c["Rx"] * (1 + gx), fade(c["mx"], c["norm"], c["A"]), c["Rb"] * grow   # expected, base
    c["mb"] = fade(c["mb"], c["norm"], c["A_t"])
    sh, dm = max(0.05, (1 + (c["shock"] - 1) * p) * c["lost"]), c["dm"] * p
    due = min(c["left"], c["due"])
    c["left"] -= due
    phi = min(v("fixedMax"), c["fixed"] + due / ((1 - c["mb"]) * c["Rb"]))
    m = clamp(c["mb"] + dm - (1 - c["mb"]) * phi * (1 / sh - 1), *v("marginRange"))
    R = c["Rb"] * sh
    OE, capex = R * m, c["capex0"] * (K if c["builder"] else R / c["R0"])
    D = c["dep0"] * capex / c["capex0"] if c["capex0"] else 0.0
    NI = OE - (rate + c["spread"]) * c["debt"]
    ebitda, flow = OE + D, NI + D - capex
    c["spread"] = spread_of(c["debt"], ebitda)
    c["cash"], c["eq"] = c["cash"] + flow, c["eq"] + NI
    if c["basis"] == "earnings":
        c["OEs"] = lt * OE + (1 - lt) * c["OEs"] * grow
        c["OExs"] = lt * c["Rx"] * c["mx"] + (1 - lt) * c["OExs"] * (1 + gx)
    else:
        c["OEs"], c["OExs"] = R * max(c["norm"] + dm, v("capitalisedMin")), c["Rx"] * max(c["norm"], v("capitalisedMin"))
    c["EV"] = ev(c, M, t)
    V = c["EV"] - c["debt"] + c["cash"] + c["stakePrev"]
    limit = max(v("leverage") * max(ebitda, 0.0), credit * c["debt0"] * clamp(c["OEs"] / c["OExs"], 0, 2))
    if c["debt"] > limit:   # maturities that lenders will not roll
        due = min(v("repay") * c["debt"], c["debt"] - limit)
        c["debt"], c["cash"] = c["debt"] - due, c["cash"] - due
    c["buf"] = v("buffer") * R + max(0.0, -flow)
    c["eqcap"] = opn * c["capScale"] * c["rho"] * V if V > 0 and V >= v("shellLarge") * max(c["eq"], 0) else 0.0
    c["dcap"] = max(0.0, limit - c["debt"])
    for kind in (("equity", "debt") if V >= v("equityFirst") * max(c["eq"], 1e-9) else ("debt", "equity")):
        x = min(c["buf"] - c["cash"], c["eqcap"] if kind == "equity" else c["dcap"])
        if x > 0:
            c["cash"] += x
            if kind == "equity":
                c["eq"], c["eqcap"] = c["eq"] + x, c["eqcap"] - x
            else:
                c["debt"], c["dcap"] = c["debt"] + x, c["dcap"] - x
            if x >= v("eventMin"):
                events.append({"year": y, "company": c["id"], "what": f"raised ${x:.0f} bn {kind}"})
    if c["cash"] < 0:   # cut capex to depreciation before anything else
        c["cash"] += min(-c["cash"], capex - D)
    if c["cash"] >= 0 and NI > 0 and c["cash"] > v("payout")[0] * R:
        pay = min(v("payout")[1] * NI, c["cash"] - v("payout")[0] * R)
        c["cash"], c["eq"] = c["cash"] - pay, c["eq"] - pay
    c.update(R=R, NI=NI)


def die(c, S, dying, events, y, M, t):
    """Absorbed by the first owner or partner that can pay its EV; otherwise extinct. Then the contagion."""
    cost, h = max(c["EV"], 0.0), None
    for pid in c["partners"]:
        q = S[pid]
        if q["status"] == "alive" and pid not in dying and max(0.0, q["cash"] - q["buf"]) + q["dcap"] + q["eqcap"] >= cost:
            h = q
            break
    c["status"], c["by"], c["died"] = ("absorbed" if h else "extinct"), (h["id"] if h else None), y
    events.append({"year": y, "company": c["id"], "what": f"absorbed by {h['id']}" if h else "extinct"})
    if h:
        cash = min(cost, max(0.0, h["cash"] - h["buf"]))
        debt = min(cost - cash, h["dcap"])
        h["cash"], h["debt"], h["dcap"] = h["cash"] - cash, h["debt"] + debt, h["dcap"] - debt
        h["eq"], h["eqcap"] = h["eq"] + cost - cash - debt, h["eqcap"] - (cost - cash - debt)   # the rest in stock
        k, mx = v("absorbKeep"), max(h["norm"], v("capitalisedMin"))
        h["mb"] = (h["mb"] * h["Rb"] + mx * c["Rb"] * k) / (h["Rb"] + c["Rb"] * k)
        h["Rb"] += c["Rb"] * k
        h["OEs"] += c["R"] * k * mx
        h["due"], h["left"] = h["due"] + c["due"] * v("riverKeep"), h["left"] + c["left"] * v("riverKeep")
        h["EV"] = ev(h, M, t)
    keep = v("riverKeep") if h else 0.0
    for d in S.values():
        for e in (d["edges"] if d["status"] == "alive" else []):
            if e["id"] == c["id"] and e["kind"] in ("customer", "commitment"):
                d["lost"] *= 1 - (1 - keep) * min(v("riverMax"), e["usd"] / max(d["R0"], d["revSum"]))
            elif e["id"] == c["id"] and e["kind"] == "capital":
                d["capScale"] *= 1 - (1 - keep) * e["usd"] / d["capSum"]


def run(base, wid, title, sc, years):
    W = {k: x[0] for k, x in (WORLDS.get(wid) or WORLDS["*"]).items()}
    S = {i: dict(s, holds=dict(s["holds"])) for i, s in base.items()}
    fs = {k: num(x) for k, x in ((sc or {}).get("flowScale") or {}).items() if num(x) is not None}
    pairs = river_pairs(fs)
    for c in S.values():
        c["shock"], c["capScale"] = exposure(c, fs, pairs, W["shocks"])
        c["dm"] = W["margins"].get(c["id"], 0.0)
    out, events = {str(years[0]): {i: record(c, S, years[0]) for i, c in S.items()}}, []
    for y in years[1:]:
        t, alive = y - years[0], [c for c in S.values() if c["status"] == "alive"]
        for c in alive:
            c["prevV"] = c["V"]
            step(c, wid, t, W, events, y)
        dying = [c for c in alive if c["cash"] < 0]
        for c in sorted(dying, key=lambda c: (-c["EV"], c["id"])):
            die(c, S, {d["id"] for d in dying}, events, y, at(W["M"], t), t)
        own = {c["id"]: c["EV"] - c["debt"] + c["cash"] for c in alive}
        for c in alive:
            stake = 0.0
            for x, pct in list(c["holds"].items()):
                if S[x]["status"] == "alive":
                    stake += pct * max(own[x], 0.0)
                elif c["status"] == "alive":   # the stake turns into its share of the price paid, or into nothing
                    got = pct * max(own[x], 0.0) if S[x]["status"] == "absorbed" else 0.0
                    c["cash"], c["eq"] = c["cash"] + got, c["eq"] + got
                    del c["holds"][x]
            c["eq"] += stake - c["stakePrev"]
            c["stakePrev"] = stake
            c["V"] = max(own[c["id"]] + stake, 0.02 * max(c["EV"], 0.0), 0.5)
            if c["status"] == "alive" and c["V"] < (1 - v("burst")) * c["prevV"]:
                events.append({"year": y, "company": c["id"],
                               "what": f"shell burst (value fell {100 * (1 - c['V'] / c['prevV']):.0f}%)"})
        out[str(y)] = {i: record(c, S, y) for i, c in S.items()}
    return {"title": title, "years": out, "events": events}


def assumptions():
    out = [{"name": k, "value": x[0], "why": x[1]} for k, x in P.items()]
    out += [{"name": "layer." + k, "value": dict(zip(LAYER_KEYS, x[0])), "why": x[1]} for k, x in LAYERS.items()]
    out += [{"name": "builders", "value": sorted(BUILDERS[0]), "why": BUILDERS[1]},
            {"name": "investors", "value": sorted(INVESTORS[0]), "why": INVESTORS[1]}]
    for w, fields in WORLDS.items():
        out += [{"name": f"{w}.{k}", "value": x[0], "why": x[1]} for k, x in fields.items()]
    return out


def simulate(data):
    """The simulation: input as in the docstring, output {assumptions, fallbacks, worlds}."""
    years = [int(y) for y in data.get("years") or range(2026, 2031)]
    base, fallbacks = {}, {}
    for c in sorted(data.get("companies") or [], key=lambda c: c["id"]):
        st, notes = prepare(c, WORLDS["2030"]["rate"][0][0])
        if notes:
            fallbacks[c["id"]] = notes
        if st:
            base[c["id"]] = st
    link(base)
    worlds = {"now": {"title": "2026, as it stands", "events": [],
                      "years": {str(years[0]): {i: record(c, base, years[0]) for i, c in base.items()}}},
              "2030": run(base, "2030", "The most likely course", None, years)}
    for sc in data.get("scenarios") or []:
        worlds[sc["id"]] = run(base, sc["id"], sc.get("title") or sc["id"], sc, years)
    return {"assumptions": assumptions(), "fallbacks": fallbacks, "worlds": worlds}


def main():
    if len(sys.argv) != 3:
        sys.exit("usage: python3 -I Tools/advantage/simulate.py INPUT.json OUTPUT.json")
    with open(sys.argv[1], encoding="utf-8") as f:
        out = simulate(json.load(f))
    with open(sys.argv[2], "w", encoding="utf-8") as f:
        json.dump(out, f, indent=1)
    for wid, w in out["worlds"].items():
        fates = [f"{e['company']} {e['what']} ({e['year']})" for e in w["events"]
                 if e["what"] == "extinct" or e["what"].startswith("absorbed")]
        print(f"{wid:12s} {'; '.join(fates) or 'no deaths'}")


if __name__ == "__main__":
    main()

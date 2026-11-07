#!/usr/bin/env python3
"""Jev judgments for the competitive-advantage model (Docs/research/advantage/model.json -> judgments.json).

Facts come from the research and its truth gate (Tools/advantage/verify.py). What a fact cannot give, a strategist's
reading of those facts, is asked of Jev (TypeSafe System One, through Tools/jev.py) as typed judgments, so code can
compose them and the app can show them as judgments, never as facts:

  actor        one request per actor, all questions over the same state (asked together, answered independently):
               Helmer's 7 Powers (Score 0-4 each), the primary power (Choice), the user's triad (information,
               execution, environment: Score 0-3 each), the durability of its position to 2030 (Score 0-4), and one
               speculative question per scenario (Score 0-4, "strongly weakened" .. "strongly strengthened"), each
               scenario's premise stated in its own question so no answer depends on another.
  flow         the payer's motive for one flow of money: desire (to win) .. fear (not to be displaced) (Score 0-4).
  chokepoint   how substitutable the choke point's supply is (Score 0-4).
  flashpoint   how much the holder's win came from information, execution and environment (Score 0-3 each), and,
               for contests between firms, the power that decided it (Choice).

State holds only task-relevant inputs: the item, the actors it names (name, role, layer) and the claims of its verified
facts. Answers are cached by the sha256 of the exact request in Tools/advantage/.cache/judge/, so a rerun only asks
what changed. The credential is found by Tools/jev.py and never printed or stored.

Usage (from the repository root):
    python3 -I Tools/advantage/judge.py                    # every judgment
    python3 -I Tools/advantage/judge.py --only actors      # actors | strategy | flows | chokepoints | flashpoints
    python3 -I Tools/advantage/judge.py --dry              # count the requests, call nothing
    python3 -I Tools/advantage/judge.py --offline          # cached answers only
    python3 -I Tools/advantage/judge.py --model M --out J  # another model file (a test)
"""
import concurrent.futures
import hashlib
import json
import os
import sys
import threading
import time

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
RESEARCH = os.path.join(ROOT, "Docs", "research", "advantage")
MODEL = os.path.join(RESEARCH, "model.json")
VERIFY = os.path.join(RESEARCH, "verification.json")
OUT = os.path.join(RESEARCH, "judgments.json")
CACHE = os.path.join(HERE, ".cache", "judge")
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.join(ROOT, "Tools"))
import jev  # noqa: E402  (Tools/jev.py: credential discovery; the key is never printed)

# ------------------------------------------------------------------------------------------------------ the questions

POWERS = {
    "scale": ("scale economies",
              "per-unit cost falls as the actor's volume grows, so a smaller rival cannot match its cost or its "
              "spending on research and capacity without first winning share it cannot win",
              ["No scale advantage: rivals of any size have the same unit costs.",
               "Slight scale advantage: larger volume lowers its costs a little, and mid-sized rivals stay close.",
               "Real scale advantage: its volume gives it clearly lower unit costs or larger budgets, though a few "
               "large rivals operate at similar scale.",
               "Strong scale advantage: its volume is several times any rival's, and the cost or spending gap would "
               "take rivals many years and great investment to close.",
               "Overwhelming scale advantage: no rival comes near its volume; its costs or budgets are out of reach "
               "and decide its market."]),
    "network": ("network economies",
                "each customer gains value as more customers, developers or partners use the same product or platform, "
                "so the leader's lead feeds itself",
                ["No network effect: a customer gets the same value whether few or many others use it.",
                 "Weak network effect: some value from a shared user base, but small rivals offer nearly as much.",
                 "Moderate network effect: a large community, ecosystem or marketplace makes it more useful than "
                 "smaller rivals, and that gap persists.",
                 "Strong network effect: its users, developers or partners form an ecosystem that rivals cannot "
                 "match; most new customers choose it because others already have.",
                 "Decisive network effect: the market has tipped to it; its ecosystem is the standard, and rivals "
                 "survive only in niches."]),
    "counter": ("counter-positioning",
                "it uses a new, better business model that incumbents cannot copy without damaging their own existing "
                "business",
                ["No counter-positioning: its business model is the same as its rivals'.",
                 "Slight counter-positioning: a somewhat different model that incumbents could copy at little cost.",
                 "Real counter-positioning: incumbents hesitate to copy its model because copying would hurt some of "
                 "their existing revenue.",
                 "Strong counter-positioning: incumbents would have to cannibalise a large existing business to copy "
                 "it, and are visibly holding back.",
                 "Decisive counter-positioning: its model is destroying incumbents who cannot respond without "
                 "destroying themselves."]),
    "switching": ("switching costs",
                  "customers would lose money, time, data or capability by moving to a rival",
                  ["No switching costs: customers could move to a rival at almost no cost or loss.",
                   "Low switching costs: moving takes some effort or retraining, and customers do it routinely.",
                   "Significant switching costs: moving costs customers real money, time or capability, so many stay, "
                   "though a determined rival can win them.",
                   "High switching costs: moving means rebuilding systems, code, workflows or skills over years; most "
                   "customers stay even when rivals are cheaper.",
                   "Lock-in: customers' products, code, data or operations are built on it, and for most of them no "
                   "realistic alternative exists."]),
    "brand": ("branding",
              "customers pay more for, or trust more, what it sells than an objectively similar rival offering, "
              "because of its history and reputation",
              ["No brand power: customers choose on price and specification alone.",
               "Weak brand power: a known name, but customers switch readily for a better price.",
               "Moderate brand power: its reputation earns some price premium or loyalty.",
               "Strong brand power: its name lets it charge clearly more, or win trust rivals cannot, for similar "
               "products.",
               "Iconic brand power: the brand itself is a large part of what customers buy, and decades of history "
               "make it impossible for rivals to replicate."]),
    "cornered": ("cornered resource",
                 "it has preferential access on attractive terms to a coveted asset rivals cannot get: patents, unique "
                 "technology or tools, key talent, data, licences, land, energy, capital or a government's favour",
                 ["No cornered resource: everything it uses is available to rivals on similar terms.",
                  "Minor cornered resource: some advantaged access (a patent, a contract, some people), easily "
                  "matched or soon expiring.",
                  "Valuable cornered resource: access to an asset that rivals want and find hard to get, giving a "
                  "clear but contestable edge.",
                  "Strong cornered resource: it controls an asset that is critical to its market and that rivals "
                  "cannot obtain for years.",
                  "Sole control: it alone holds an asset its whole market depends on, with no alternative source."]),
    "process": ("process power",
                "its embedded organisation and ways of working give it lower costs or better products, and rivals "
                "could match them only after years of sustained effort (like Toyota's production system or TSMC's "
                "yields)",
                ["No process power: its operations are like its rivals'.",
                 "Slight process power: some operational excellence that rivals can copy within a year or two.",
                 "Real process power: a clearly better way of making or delivering things that rivals have been "
                 "trying, with partial success, to copy.",
                 "Strong process power: an accumulated know-how that rivals with large budgets have failed to match "
                 "for years.",
                 "Unmatched process power: decades of embedded capability that no rival can reproduce; its "
                 "execution defines the frontier of its industry."]),
}

TRIAD = {
    "information": "information: knowing more, or knowing sooner, than its rivals (research, proprietary data, "
                   "talent and insight, intelligence about customers, technology and rivals)",
    "execution": "execution: doing things better and faster than its rivals (organisation, process, tactics, speed, "
                 "discipline, the ability to build and deliver at scale)",
    "environment": "environment: being in a favourable position that it did not entirely create (geography, "
                   "regulation and government support, access to capital, timing, a place in a supply chain, the "
                   "condition of its rivals)",
}
TRIAD_LEVELS = ["Not at all: this plays no real part in its position.",
                "Somewhat: it helps, but its position would hold without it.",
                "Substantially: it is one of the main supports of its position.",
                "Decisively: without it, its position would collapse."]

ACTOR_SCOPE = ("`actor` is a company, lab, state, investor or other actor in the economy as of `asOf`. `actor.role`, "
               "`actor.layer` and `actor.relations` place it in the supply chain; `evidence` lists claims verified "
               "against sources. Judge from the evidence and from well-established knowledge about the actor up to "
               "`asOf`.")


def actor_questions(scenarios):
    q = {}
    for key, (name, mech, levels) in POWERS.items():
        q["power_" + key] = {"type": "score",
                             "instructions": f"{ACTOR_SCOPE} How strong is the actor's {name}: {mech}?",
                             "criteria": levels}
    q["primary"] = {"type": "choice",
                    "instructions": f"{ACTOR_SCOPE} Which single source of durable competitive advantage matters most "
                                    "to the actor's position?",
                    "criteria": dict({key: f"{name}: {mech}" for key, (name, mech, _) in POWERS.items()},
                                     none="none: it has no durable advantage; its position rests on temporary "
                                          "factors such as a boom, a subsidy or a head start")}
    for key, text in TRIAD.items():
        q["triad_" + key] = {"type": "score",
                             "instructions": f"{ACTOR_SCOPE} How much does the actor's competitive position rest on "
                                             f"{text}?",
                             "criteria": TRIAD_LEVELS}
    q["durability"] = {"type": "score",
                       "instructions": f"{ACTOR_SCOPE} Looking ahead from `asOf` to 2030, in the most likely course of "
                                       "events, what happens to the actor's competitive position?",
                       "criteria": ["It fails or is displaced: it loses most of its position, is absorbed or goes "
                                    "under.",
                                    "It declines: rivals or substitutes take a substantial part of its position.",
                                    "It holds: its position in 2030 is about what it is now.",
                                    "It strengthens: it gains share, pricing power or reach at rivals' expense.",
                                    "It dominates: it ends the decade far ahead, with rivals marginalised."]}
    for s in scenarios:
        q["scenario_" + s["id"]] = {
            "type": "score",
            "instructions": f"{ACTOR_SCOPE} Suppose this happens after `asOf`: {s['premise']} Over the following two "
                            "to three years, how would the actor's competitive position change because of it?",
            "criteria": ["Strongly weakened: it loses much of its position, or its survival is in danger.",
                         "Weakened: it loses some position, revenue or pricing power.",
                         "Little changed: the event barely touches its position.",
                         "Strengthened: it gains some position, revenue or pricing power.",
                         "Strongly strengthened: it gains a great deal while rivals fall back."]}
    return q


STRATEGY_Q = {
    "strategy": {
        "type": "choice",
        "instructions": "`company` is a company in the 2026 economy, `evidence` lists verified claims about it (its balance sheet, its "
                        "valuation, its spending, its contracts, its owners). Which strategy best describes how it competes now?",
        "criteria": {
            "toll": "toll collector: sells what everyone in its chain must buy, at margins rivals cannot match",
            "landgrab": "land grab on the share price: uses a high valuation or outside capital to spend far ahead of revenue, take "
                        "losses or price below cost to win a market before rivals",
            "fortress": "cash fortress: funds its growth and defends its markets from its own profits, returning the rest to owners",
            "builder": "debt-financed builder: borrows or leases to build capacity ahead of demand that is contracted but unproven",
            "consolidator": "consolidator: buys rivals, talent and technology with its cash or its shares to keep its position",
            "challenger": "challenger: attacks an incumbent with a cheaper, faster or different model",
            "utility": "utility or commodity supplier: earns ordinary returns on physical assets or cyclical products",
        }},
    "risk": {
        "type": "score",
        "instructions": "`company` is a company in the 2026 economy, `evidence` lists verified claims about it. How exposed is it to a "
                        "collapse in its own share price or in its access to outside capital (equity or debt) over the next three years?",
        "criteria": ["Barely exposed: it funds itself from profits and holds more cash than debt.",
                     "Lightly exposed: a collapse would slow it but not threaten any commitment.",
                     "Moderately exposed: it needs capital markets for its growth, but not to survive.",
                     "Heavily exposed: it depends on new capital or a high valuation to meet its commitments.",
                     "Existentially exposed: without continued outside capital it would fail or have to sell itself."]}}

FLOW_Q = {"motive": {
    "type": "score",
    "instructions": "`flow` is money the payer (`flow.payer`) pays the payee (`flow.payee`) for `flow.what`, as of "
                    "`asOf`. `statements` are the payer's own words on the record, and `evidence` lists verified "
                    "claims. What drives the payer to spend this money: the desire to win and gain new ground, or "
                    "the fear of falling behind, losing position or being displaced?",
    "criteria": ["Pure desire: it spends to seize an opportunity; nothing threatens it if it does not.",
                 "Mostly desire: an ambition to grow, with some concern about rivals.",
                 "Mixed: ambition and the fear of being left behind weigh about equally.",
                 "Mostly fear: it spends mainly so as not to fall behind rivals or lose what it has.",
                 "Pure fear: it spends because not spending could end its position or its existence."]}}

CHOKE_Q = {"substitutable": {
    "type": "score",
    "instructions": "`chokepoint` describes what one actor (`chokepoint.actor`) supplies to the economy as of `asOf`, "
                    "and `evidence` lists verified claims. If this actor stopped supplying `chokepoint.controls` "
                    "tomorrow, how quickly could its customers obtain an equivalent elsewhere?",
    "criteria": ["Not for years at any price: no alternative exists.",
                 "Only after years and great cost: alternatives exist only in early or inferior forms.",
                 "Within one to two years, at significant cost and with lower performance.",
                 "Within months, with some loss of performance or price.",
                 "At once: many suppliers offer an equivalent."]}}


WALL_LEVELS = ["No barrier: a rival or a new entrant could match it within a year with ordinary effort.",
               "A low barrier: a well-funded rival could match it in one or two years.",
               "A real barrier: matching it takes several years and tens of billions of dollars, and rivals are trying.",
               "A high barrier: only a few of the world's largest companies or states could match it, over many years.",
               "Near-insurmountable: no rival can match it this decade; the market is built on it."]


def moat_questions(sources):
    """One Score per source of a moat (the fifth direction's advantage boundaries): how much that source alone keeps
    rivals and entrants out of the company's market. The source is named in each question (ids are not sent)."""
    return {"wall_" + src: {
        "type": "score",
        "instructions": "`moat` says where `moat.company` holds an advantage as of `asOf`: its market (`moat.market`) and its "
                        "share there (`moat.share`). `moat.sources." + src + "` is one source of that advantage, with what it "
                        "is and the verified evidence for it. Judge that source alone: how much does it keep rivals and new "
                        "entrants from taking this market from the company?",
        "criteria": WALL_LEVELS} for src in sources}


ENTRY_Q = {
    "hard": {
        "type": "score",
        "instructions": "`stratum` is one layer of the 2026 AI supply chain as of `asOf`, with its largest incumbents, the "
                        "verified barriers to entry, and new entrants with their fates. How hard is it for a new company, "
                        "starting now, to win a meaningful share (about 5%) of this layer's market by 2030?",
        "criteria": ["Easy: many entrants win share; incumbents hold little that an entrant cannot buy or build.",
                     "Hard but common: well-funded entrants win share within a few years.",
                     "Very hard: a few entrants with exceptional backing have won share; most are absorbed or stall.",
                     "Nearly closed: entrants are bought, licensed-and-hired or starved before they win share.",
                     "Closed: no entrant can win share this decade; only states or the incumbents themselves can."]},
    "talentCapital": {
        "type": "score",
        "instructions": "`stratum` is one layer of the 2026 AI supply chain as of `asOf`, with its largest incumbents, the "
                        "verified barriers to entry, and new entrants with their fates. Of what keeps new entrants out of "
                        "this layer, how much is the incumbents' hold on talent and the buying power of their share price "
                        "(stock-based pay, stock- and cash-funded deals, investments in would-be rivals, capex funded by "
                        "market value), as opposed to other barriers (technology, scarce supply, distribution, regulation)?",
        "criteria": ["None of it: other barriers keep entrants out.",
                     "A little: talent and the share price matter at the margin.",
                     "About half: talent and the share price weigh as much as the other barriers.",
                     "Most of it: talent and the share price are the main wall.",
                     "Almost all of it: without their talent and share price the incumbents would face entrants."]},
}


def moat_state(model, actors, m):
    return {"asOf": model.get("asOf"),
            "moat": {"company": actor_brief(actors, m.get("actor")), "market": m.get("market"), "share": m.get("share"),
                     "sources": {x["source"]: {"what": x.get("what"), "evidence": verified_claims(model, x.get("facts"), 10)}
                                 for x in m.get("sources") or [] if x.get("source")}}}


def entry_state(model, actors, stratum, name):
    present = [a for a in model.get("actors", []) if a.get("era") == "present" and not a.get("stub")]
    incumbents = [actor_brief(actors, m["actor"])["name"] for m in model.get("moats", []) if m.get("stratum") == stratum]
    return {"asOf": model.get("asOf"),
            "stratum": {"name": name, "incumbents": incumbents,
                        "barriers": [{"source": b.get("source"), "what": b.get("what"), "evidence": verified_claims(model, b.get("facts"), 6)}
                                     for b in model.get("barriers", []) if b.get("stratum") == stratum],
                        "entrants": [{"name": e.get("name"), "what": e.get("what"), "fate": e.get("fate")}
                                     for e in model.get("entrants", []) if e.get("stratum") == stratum]}}


STRATA_NAMES = {"consumer": "consumer products and platforms", "models": "AI models", "clouds": "cloud and data centres",
                "chips": "chips, memory and networking", "fabs": "fabs and packaging", "tools": "equipment, EDA and IP",
                "energy": "energy and power", "materials": "raw materials"}


def flashpoint_questions(firms):
    scope = ("`flashpoint` is a contest in which `flashpoint.holder` held a competitive advantage over "
             "`flashpoint.rivals`: `flashpoint.mechanism` says how, and `flashpoint.outcome` what came of it. "
             "`evidence` lists verified claims.")
    q = {}
    for key, text in TRIAD.items():
        q["triad_" + key] = {"type": "score",
                             "instructions": f"{scope} How much did the holder's advantage in this contest come from "
                                             f"{text}?",
                             "criteria": TRIAD_LEVELS}
    if firms:
        q["power"] = {"type": "choice",
                      "instructions": f"{scope} Which source of competitive advantage decided this contest?",
                      "criteria": dict({key: f"{name}: {mech}" for key, (name, mech, _) in POWERS.items()},
                                       none="none of these: luck, a rival's blunder or force decided it")}
    return q

# ------------------------------------------------------------------------------------------------------------- state


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def verified_claims(model, ids, limit=24):
    facts = model.get("facts", {})
    out = []
    for fid in ids or []:
        f = facts.get(fid)
        if f and f.get("status") == "verified" and f.get("claim") not in out:
            out.append(f["claim"])
    return out[:limit]


def actor_brief(actors, aid):
    a = actors.get(aid)
    if not a:
        return {"id": aid}
    return {"name": a.get("name", aid), "role": a.get("role", ""), "layer": a.get("layer", "")}


def actor_state(model, actors, a):
    facts = list(a.get("facts") or [])
    for f in model.get("flows", []):   # an actor's money in and out is evidence about it too
        if a["id"] in (f.get("from"), f.get("to")) and f.get("fact"):
            facts.append(f["fact"])
    rel = {k: [actor_brief(actors, x)["name"] if x in actors else x for x in a.get(k) or []]
           for k in ("standsOn", "competitors", "customers")}
    return {"asOf": model.get("asOf"),
            "actor": {"name": a.get("name"), "kind": a.get("kind"), "country": a.get("country"),
                      "layer": a.get("layer"), "sector": a.get("sector"), "role": a.get("role"),
                      "advantage": a.get("advantage"), "relations": rel},
            "evidence": verified_claims(model, facts)}


def flow_state(model, actors, f):
    payer = actors.get(f.get("from"), {})
    statements = [c for c in verified_claims(model, payer.get("facts"), 60)
                  if any(w in c.lower() for w in ("said", "says", "wrote", "told", "according to"))][:8]
    return {"asOf": model.get("asOf"),
            "flow": {"payer": actor_brief(actors, f.get("from")), "payee": actor_brief(actors, f.get("to")),
                     "what": f.get("what"), "usdBnPerYear": f.get("usdBnPerYear"), "kind": f.get("kind"),
                     "period": f.get("period")},
            "statements": statements,
            "evidence": verified_claims(model, [f.get("fact")] + list(f.get("facts") or []))}


def choke_state(model, actors, c):
    return {"asOf": model.get("asOf"),
            "chokepoint": {"actor": actor_brief(actors, c.get("actor")), "controls": c.get("controls"),
                           "share": c.get("share"), "alternatives": c.get("alternatives")},
            "evidence": verified_claims(model, c.get("facts"))}


def flash_state(model, actors, p):
    return {"flashpoint": {"title": p.get("title"), "year": p.get("year"), "era": p.get("era"),
                           "holder": actor_brief(actors, p.get("holder")),
                           "rivals": [actor_brief(actors, r) for r in p.get("rivals") or []],
                           "advantage": p.get("advantage"), "mechanism": p.get("mechanism"),
                           "outcome": p.get("outcome")},
            "evidence": verified_claims(model, p.get("facts"))}

# --------------------------------------------------------------------------------------------------------------- run

STATS = {"requests": 0, "cached": 0, "errors": 0, "tokens": 0, "model": None}
LOCK = threading.Lock()


def ask(state, questions, offline):
    blob = json.dumps({"state": state, "questions": questions}, sort_keys=True, ensure_ascii=False)
    path = os.path.join(CACHE, hashlib.sha256(blob.encode("utf-8")).hexdigest() + ".json")
    if os.path.exists(path):
        with LOCK:
            STATS["cached"] += 1
        return load(path)
    if offline:
        return None
    last = None
    for attempt in range(3):
        try:
            result = jev.call(state, questions, timeout=150)
            break
        except RuntimeError as e:   # jev.call never puts the key or the state in its message
            last = str(e)
            time.sleep(4 * (attempt + 1))
    else:
        with LOCK:
            STATS["errors"] += 1
        print(f"  Jev error: {last}", file=sys.stderr)
        return None
    os.makedirs(CACHE, exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(result, f, sort_keys=True)
    with LOCK:
        STATS["requests"] += 1
        STATS["tokens"] += sum(v for v in (result.get("usage") or {}).values() if isinstance(v, (int, float)))
        STATS["model"] = result.get("model") or STATS["model"]
    return result


def compact(answer):
    """The parts of an answer the app and the analysis use."""
    if answer.get("type") == "score":
        return {"score": round(answer["score"], 3), "confidence": round(answer.get("confidence", 0), 3),
                "p": {k: round(v, 3) for k, v in (answer.get("probabilities") or {}).items()}}
    if answer.get("type") == "choice":
        return {"choice": answer["choice"], "confidence": round(answer.get("confidence", 0), 3),
                "p": {k: round(v, 3) for k, v in sorted((answer.get("probabilities") or {}).items(),
                                                        key=lambda kv: -kv[1])}}
    return {"p": answer.get("noul")}


def main():
    args = sys.argv[1:]
    only = args[args.index("--only") + 1] if "--only" in args else None
    dry, offline = "--dry" in args, "--offline" in args
    global OUT
    model = load(args[args.index("--model") + 1] if "--model" in args else MODEL)
    OUT = args[args.index("--out") + 1] if "--out" in args else OUT
    actors = {a["id"]: a for a in model.get("actors", [])}
    scenarios = model.get("scenarios", [])
    jobs = []   # (section, id, state, questions)
    strategist = load(os.path.join(RESEARCH, "strategist.json")) if os.path.exists(os.path.join(RESEARCH, "strategist.json")) else {}
    arena = {a["id"] for a in strategist.get("actors", []) if a.get("layer") not in (None, "drop", "history", "owners")}

    def in_arena(a):
        nums = a.get("numbers") or {}
        return a.get("era", "present") == "present" and a.get("layer") not in (None, "drop", "history", "owners") and \
            (a["id"] in arena or nums.get("marketCap") or nums.get("valuation"))
    if only in (None, "actors"):
        aq = actor_questions(scenarios)
        for a in model.get("actors", []):
            if in_arena(a):
                jobs.append(("actors", a["id"], actor_state(model, actors, a), aq))
    if only in (None, "strategy"):
        for a in model.get("actors", []):
            if not in_arena(a) or a.get("layer") in ("state", "buyers"):
                continue
            st = actor_state(model, actors, a)
            jobs.append(("strategy", a["id"], {"asOf": st["asOf"], "company": st["actor"], "evidence": verified_claims(model, a.get("facts"), 40)}, STRATEGY_Q))
    if only in (None, "flows"):
        for f in model.get("flows", []):
            jobs.append(("flows", f["id"], flow_state(model, actors, f), FLOW_Q))
    if only in (None, "chokepoints"):
        for c in model.get("chokepoints", []):
            jobs.append(("chokepoints", c.get("id") or c["actor"], choke_state(model, actors, c), CHOKE_Q))
    if only in (None, "moats"):
        for m in model.get("moats", []):
            srcs = [x["source"] for x in m.get("sources") or [] if x.get("source")]
            if srcs:
                jobs.append(("moats", m["id"], moat_state(model, actors, m), moat_questions(srcs)))
    if only in (None, "entry"):
        for sid, nm in STRATA_NAMES.items():
            st = entry_state(model, actors, sid, nm)
            if st["stratum"]["barriers"] or st["stratum"]["entrants"]:
                jobs.append(("entry", sid, st, ENTRY_Q))
    if only in (None, "flashpoints"):
        for p in model.get("flashpoints", []):
            firms = all(actors.get(x, {}).get("kind") in ("company", "lab")
                        for x in [p.get("holder")] + list(p.get("rivals") or []))
            jobs.append(("flashpoints", p["id"], flash_state(model, actors, p), flashpoint_questions(firms)))
    print(f"{len(jobs)} requests ({', '.join(f'{s} {sum(1 for j in jobs if j[0] == s)}' for s in dict.fromkeys(j[0] for j in jobs))})")
    if dry:
        return
    if not offline and not jev.available():
        sys.exit("Jev credential unavailable: no judgments made")
    out = load(OUT) if os.path.exists(OUT) else {}
    for section in ("actors", "strategy", "flows", "chokepoints", "flashpoints", "moats", "entry"):
        out.setdefault(section, {})
    done = [0]

    def run(job):
        section, jid, state, questions = job
        result = ask(state, questions, offline)
        with LOCK:
            done[0] += 1
            if done[0] % 20 == 0:
                print(f"  {done[0]}/{len(jobs)}")
        if result is None:
            return section, jid, None
        return section, jid, {k: compact(v) for k, v in result.get("answers", {}).items()}

    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        for section, jid, answers in pool.map(run, jobs):
            if answers is not None:
                out[section][jid] = answers
    out.update(asOf=model.get("asOf"), model=STATS["model"] or out.get("model"),
               note="Jev (TypeSafe System One) judgments over the verified facts: model evidence, not facts.")
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=1, ensure_ascii=False, sort_keys=True)
        f.write("\n")
    print(f"Jev: {STATS['requests']} asked, {STATS['cached']} cached, {STATS['errors']} errors, "
          f"{STATS['tokens']} tokens, model {STATS['model']}; wrote {os.path.relpath(OUT, ROOT)}")


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""
Jev's touches (Docs/hills-design/POLITICS.md choice 8: each key moment lands on the economy as topology under the industries it
touched): for each key moment and each of the 25 industries the landscape view draws as ridges (Assets/Resources/Data/economy/
industries.json), how directly the moment changed the rules, costs, demand or competition that industry faced in the United
States in the five years after it, as a TypeSafe Score on five levels (0-4) from Jev (through Tools/jev.py). Writes
Docs/research/politics/touches.json; Tools/politics/build.py merges it into politics.json as each moment's "touches" (the
industries scored 1 or more, highest first), which Assets/Scripts/Economy/Model/Politics.cs reads as Moment.Touches. These are
Jev's judgments over the moment's gated claim, not measurements.

The moments are the usable facts of the key-moments cluster (gate-verified and not rejected by a skeptic: build.py's load()).
One request per moment carries 25 questions, one Score per industry, each naming its industry alone, asked together over the
same state: the moment as build.moment_state() tells it (its title, its date as the research gives it, its kind and the claim of
its gated fact) and the industries (name, tier, NAICS coverage and blurb from industries.json), nothing else. The questions judge
the five years after the moment and leave out what came later unless the claim states it; a measure that reached every
industry through the economy as a whole (a broad tax cut, the central bank's rates) counts only for an industry it bore on more
than on most. Each judged moment records the digest of what Jev was told about it (build.told()), so the build can tell when a
moment's words have changed since.

Answers are cached by the sha256 of the exact request in Tools/politics/.cache/touches/, so a rerun asks only what changed and
reproduces the rest; at most four requests are in flight. The credential is found by Tools/jev.py and never printed or stored.

    python3 -I Tools/politics/touches.py                         # judge every moment and write touches.json
    python3 -I Tools/politics/touches.py --moment pol-2010-aca   # some moments (comma-separated): their rows replace theirs
    python3 -I Tools/politics/touches.py --anchors               # the anchor moments against what they must show; writes nothing
    python3 -I Tools/politics/touches.py --dry                   # each request's size and whether it is cached; calls nothing
    python3 -I Tools/politics/touches.py --show pol-2010-aca     # print that moment's request; calls nothing
    python3 -I Tools/politics/touches.py --offline               # cached answers only
"""
import concurrent.futures
import datetime
import hashlib
import json
import os
import sys
import threading
import time

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(ROOT, "Docs", "research", "politics", "touches.json")
INDUSTRIES = os.path.join(ROOT, "Assets", "Resources", "Data", "economy", "industries.json")
CACHE = os.path.join(HERE, ".cache", "touches")
sys.dont_write_bytecode = True
sys.path[0:0] = [HERE, os.path.join(ROOT, "Tools")]
import build  # noqa: E402  (Tools/politics/build.py: load() keeps the usable facts; moment_state() is what Jev is told)
import jev  # noqa: E402  (Tools/jev.py: credential discovery; the key is never printed)

WORKERS = 4   # requests in flight at once

# ------------------------------------------------------------------------------------------------------- the questions

# One dimension: how directly the moment changed the conditions the industry operated under in the United States. Each
# question names its industry alone ({name}, and {id} for its entry in the state); the levels name it too, so each stands on
# its own, as an object (the situation, and what it includes or excludes) where a boundary needed drawing. No moment is used
# as an example anywhere in them. Tried on the anchors (below) and on economy-wide probes (Volcker's tightening, the 1981 tax
# cut, the Fed's zero rates of 2008, the Korean War), on 8 Nov 2026:
# - "Indirectly ... through prices, demand or another industry (dearer inputs ...)" drew about half of every unrelated
#   industry's probability (airline deregulation put banking at 1.0) and read an oil shock as a dearer input of transport,
#   so level 1 is the price of a minor input or business brought by a change in another industry, and level 3 includes a
#   sharp change in the price or supply of what an industry mainly sells or of one of its main inputs;
# - "... though it was aimed elsewhere" let every industry a broad measure reached into level 2 (the 1981 tax cut split each
#   between levels 0 and 2), so level 2 is an effect larger than most industries felt, and level 3 an industry among those
#   the moment changed most;
# - level 0's "whatever reached it through growth, inflation, interest rates or taxes" pulled the industries that bear the
#   rates most toward 0 (Volcker's real estate split 0.37 on level 0 against 0.43 on level 3), and "the moment did nothing in
#   particular to the industry" let a foreign act (China's accession, the embargo) read as touching no US industry (US
#   manufacturing 0.2 on level 0), so level 0 is said from the industry's side: its conditions owed nothing to the moment;
# - "leave out what came later" could be read as later than the moment; the window's end is meant, as judge.py's "knowledge of
#   the period up to `year`" (it moved no answer much). A clause judging a law of many parts by the part that bore most on the
#   industry only lengthened the tails of broad laws (the 1981 tax cut from 6 touches to 11) and was dropped; the parts it was
#   meant for (the CARES Act's airline grants, TARP's rescue of the carmakers) are not in their claims, and Jev did not supply
#   them.
INSTRUCTIONS = ("`moment` is a key moment for the economy of the United States (a law, a regulation, a trade action, a monetary "
                "turn, a treaty or a conflict), and `industries` lists the industries of the US economy, each with its name, "
                "tier, NAICS coverage and description. How directly did `moment` change the rules, costs, demand or "
                "competition that the {name} industry (`industries.{id}`) faced in the United States in the five years after "
                "`moment.date`? Judge from `moment.claim` and well-established knowledge of the moment and of those five "
                "years; leave out anything after those five years unless `moment.claim` states it. A measure that reached "
                "every industry through the economy as a whole, such as a broad tax cut, a change in the central bank's "
                "interest rates or the general demand of a war, counts for the {name} industry only as far as it bore on it "
                "more than on most industries.")

LEVELS = [
    {"situation": "Not at all, or only through the economy as a whole: in those five years the rules, costs, demand and "
                  "competition of the {name} industry in the United States owed nothing in particular to the moment; whatever "
                  "it felt of the moment, most industries felt alike.",
     "includes": "an industry tied to the moment only by the buying and selling that link every industry to every other"},
    {"situation": "Indirectly: the moment reached the {name} industry only at one remove, through the price of a minor input or "
                  "through more or less business brought by a change in another industry, while its own rules, main costs and "
                  "competition stayed as they were.",
     "excludes": "a tie only through the buying and selling that link every industry to every other"},
    {"situation": "A clear but secondary effect: the moment itself changed the rules, costs, demand or competition of the {name} "
                  "industry in the United States, more than it changed most industries', though the industry was not among "
                  "those the moment changed most."},
    {"situation": "A direct effect on a large part of the {name} industry: the industry was among those the moment changed "
                  "most, in the rules, main costs, demand or competition that much of it faced in the United States.",
     "includes": "a sharp change in the price or supply of what the industry mainly sells or of one of its main inputs"},
    {"situation": "Written for, or aimed squarely at, the {name} industry: changing its rules, structure, costs or competition, "
                  "or the supply of what it sells, was the moment's main purpose."},
]

# The anchors (--anchors): moments whose touches are well known, and the least level each named industry must reach ("high"
# is 3). An expectation is met when Jev puts at least half its probability on that level or above (the Score docs: a decision
# that depends on one level reads that level's probability, since the score can hide a split); the score is shown beside it.
# They check the questions; when one fails, the questions are what changes, never the answers.
ANCHORS = {
    "pol-1978-airline-deregulation": {"transport": 3},
    "pol-1984-bell-breakup": {"media_telecom": 3},
    "pol-1999-gramm-leach-bliley": {"banking": 3, "finance": 3, "insurance": 3},
    "pol-2010-aca": {"health": 3, "insurance": 3},
    "pol-2022-chips-act": {"hardware": 3},
    "pol-1973-oil-embargo": {"oil_gas": 3, "transport": 3, "utilities": 3},
    "pol-1970-epa": {"utilities": 2, "manufacturing": 2, "oil_gas": 2, "mining_metals": 2},
    "pol-1956-highway-act": {"construction": 3, "transport": 3},
}

SCOPE = ("`moment` holds the key moment's title, its date as the research gives it, its kind (law, regulation, trade, "
         "monetary, treaty or conflict) and the claim of its gate-verified fact (Docs/research/politics/key-moments.json); "
         "`industries` the 25 industries of Assets/Resources/Data/economy/industries.json by id, each with its name, its tier's "
         "name, its NAICS coverage and its blurb (as `description`). Nothing else.")

NOTE = ("These are Jev's judgments, not measurements: for each key moment and industry, Jev (TypeSafe System One) placed how "
        "directly the moment changed the rules, costs, demand or competition the industry faced in the United States in the "
        "five years after it on the five levels below, judging from the moment's gated claim and its own knowledge of the "
        "period. `score` is the probability-weighted level (0-4), `confidence` how concentrated the probabilities are (1 all "
        "on one level, 0 an even spread or worse), `p` the probability of each level, 0 to 4. Built by "
        "Tools/politics/touches.py; Tools/politics/build.py merges the industries scored 1 or more into politics.json.")


def industries():
    """The landscape's industries in industries.json's order, as (id, name, the state's entry for it)."""
    data = load(INDUSTRIES)
    tiers = {t["id"]: t["name"] for t in data["tiers"]}
    return [(i["id"], i["name"], {"name": i["name"], "tier": tiers[i["tier"]], "naics": i["naics"], "description": i["blurb"]})
            for i in data["industries"]]


def fill(text, **names):
    """A question's text with its industry filled in, through any structure (a level may be an object)."""
    if isinstance(text, str):
        return text.format(**names)
    if isinstance(text, dict):
        return {k: fill(v, **names) for k, v in text.items()}
    return [fill(v, **names) for v in text]


def questions(inds):
    """One Score per industry, named in its own question (question ids are not sent to the model)."""
    return {"touch_" + iid: {"type": "score", "instructions": fill(INSTRUCTIONS, name=name, id=iid),
                             "criteria": fill(LEVELS, name=name, id=iid)} for iid, name, _ in inds}


def request(fact, inds, qs):
    return {"moment": build.moment_state(fact), "industries": {iid: entry for iid, _, entry in inds}}, qs

# --------------------------------------------------------------------------------------------------------------- run


STATS = {"requests": 0, "cached": 0, "errors": 0, "tokens": 0, "model": None}
LOCK = threading.Lock()


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def key(state, qs):
    blob = json.dumps({"state": state, "questions": qs}, sort_keys=True, ensure_ascii=False)
    return hashlib.sha256(blob.encode("utf-8")).hexdigest()


def ask(state, qs, offline):
    """Jev's answers for one request, from the cache when this exact request was asked before (judge.py's), with the date it
    was asked."""
    path = os.path.join(CACHE, key(state, qs) + ".json")
    if os.path.exists(path):
        result = load(path)
        with LOCK:
            STATS["cached"] += 1
            STATS["model"] = STATS["model"] or result.get("model")
        return result
    if offline:
        return None
    last = None
    for attempt in range(3):
        try:
            result = jev.call(state, qs, timeout=150)
            break
        except (RuntimeError, OSError, ValueError) as e:   # HTTP, network or timeout, a body that is not JSON: never the key
            last = f"{type(e).__name__}: {e}"
            time.sleep(4 * (attempt + 1))
    else:
        with LOCK:
            STATS["errors"] += 1
        print(f"  Jev error: {last}", file=sys.stderr)
        return None
    result["asked"] = datetime.date.today().isoformat()
    os.makedirs(CACHE, exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(result, f, sort_keys=True)
    with LOCK:
        STATS["requests"] += 1
        STATS["tokens"] += sum(v for v in (result.get("usage") or {}).values() if isinstance(v, (int, float)))
        STATS["model"] = result.get("model") or STATS["model"]
    return result


def scores(answers, inds):
    """The moment's 25 rows in industries.json's order, or None when an answer is missing or not a Score."""
    rows = []
    for iid, _, _ in inds:
        a = answers.get("touch_" + iid)
        if not isinstance(a, dict) or a.get("type") != "score" or not isinstance(a.get("score"), (int, float)):
            return None
        p = a.get("probabilities") or {}
        rows.append({"industry": iid, "score": round(a["score"], 2), "confidence": round(a.get("confidence", 0), 2),
                     "p": [round(p.get(str(level), 0), 3) for level in range(len(LEVELS))]})
    return rows


def at_least(s, level):
    """The probability Jev put on `level` or above."""
    return sum(s["p"][level:])


def line(row, names, expect=None, floor=1.0):
    """The moment's touches as one line, highest first: those scored `floor` or more, and any it was expected to show (with
    the probability of the level wanted or above)."""
    expect = expect or {}
    shown = sorted((s for s in row["scores"] if s["score"] >= floor or s["industry"] in expect),
                   key=lambda s: -s["score"])
    parts = []
    for s in shown:
        mark = ""
        if s["industry"] in expect:
            want = expect[s["industry"]]
            mark = f" [p(>={want}) {at_least(s, want):.2f} " + ("ok]" if at_least(s, want) >= 0.5 else "FAIL]")
        parts.append(f"{names[s['industry']]} {s['score']:.2f} ({s['confidence']:.2f}){mark}")
    return f"{row['id']} [{row['kind']}]: " + ("; ".join(parts) or "none")


def dumps(out):
    """touches.json with each industry's row on one line."""
    rows = {}

    def mark(obj):
        token = f"@@row{len(rows)}@@"
        rows[json.dumps(token)] = json.dumps(obj, ensure_ascii=False)
        return token

    shaped = dict(out, moments=[dict(m, scores=[mark(s) for s in m["scores"]]) for m in out["moments"]])
    text = json.dumps(shaped, indent=1, ensure_ascii=False)
    for token, value in rows.items():
        text = text.replace(token, value)
    return text + "\n"


def main():
    args = sys.argv[1:]
    dry, offline, anchors = "--dry" in args, "--offline" in args, "--anchors" in args
    show = args[args.index("--show") + 1] if "--show" in args else None
    only = args[args.index("--moment") + 1].split(",") if "--moment" in args else None
    facts = build.load("key-moments")
    rows = build.moments(facts)                     # in date order, with the kind and decimal date the build draws
    inds = industries()
    names = {iid: name for iid, name, _ in inds}
    qs = questions(inds)
    reqs = {r["id"]: request(facts[r["id"]], inds, qs) for r in rows}
    asked = list(ANCHORS) if anchors else only
    for fid in (asked or []) + ([show] if show else []):
        if fid not in reqs:
            sys.exit(f"{fid} is not a usable key moment: " + ", ".join(reqs))
    if show is not None:
        state, q = reqs[show]
        print(json.dumps({"state": state, "questions": q}, indent=1, ensure_ascii=False))
        return
    ids = [r["id"] for r in rows if asked is None or r["id"] in asked]
    print(f"{len(ids)} requests, {len(ids) * len(inds)} touches ({len(inds)} industries a moment)")
    if dry:
        for fid in ids:
            state, q = reqs[fid]
            size = len(json.dumps({"state": state, "questions": q}, ensure_ascii=False))
            cached = os.path.exists(os.path.join(CACHE, key(state, q) + ".json"))
            print(f"  {fid}: {size} characters{', cached' if cached else ''}")
        return
    if not offline and not jev.available():
        sys.exit("Jev credential unavailable: no judgments made")

    def run(fid):
        state, q = reqs[fid]
        return fid, ask(state, q, offline)

    by_id = {r["id"]: r for r in rows}
    judged = {}
    with concurrent.futures.ThreadPoolExecutor(max_workers=WORKERS) as pool:
        for fid, result in pool.map(run, ids):
            got = scores(result.get("answers") or {}, inds) if result else None
            if got is None:
                print(f"  no judgment of {fid}: {'no answer' if result is None else 'an answer missing or not a Score'}")
                continue
            r = by_id[fid]
            judged[fid] = {"id": fid, "title": r["title"], "kind": r["kind"], "date": r["date"],
                           "model": result.get("model"), "asked": result.get("asked") or datetime.date.today().isoformat(),
                           "told": build.told(facts[fid]), "scores": got}
    for fid in ids:
        if fid in judged:
            print("  " + line(judged[fid], names, ANCHORS.get(fid) if anchors else None))
    print(f"Jev: {STATS['requests']} asked, {STATS['cached']} cached, {STATS['errors']} errors, {STATS['tokens']} tokens, "
          f"model {STATS['model']}")
    if anchors:
        rows_of = {fid: {s["industry"]: s for s in judged[fid]["scores"]} for fid in judged}
        wanted = [(fid, iid, least) for fid, want in ANCHORS.items() for iid, least in want.items() if fid in judged]
        failed = [(fid, iid) for fid, iid, least in wanted if at_least(rows_of[fid][iid], least) < 0.5]
        below = [(fid, iid) for fid, iid, least in wanted if rows_of[fid][iid]["score"] < least]
        missing = [fid for fid in ANCHORS if fid not in judged]
        print(f"anchors: {len(wanted) - len(failed)} of {len(wanted)} expectations met (half the probability or more on the "
              f"level wanted or above)" + (f"; failed: {failed}" if failed else "")
              + (f"; scored below the level wanted: {below}" if below else "")
              + (f"; not judged: {missing}" if missing else ""))
        return

    old = load(OUT) if os.path.exists(OUT) else {}
    kept = []
    for m in old.get("moments", []):
        if m["id"] in judged:
            continue
        if m["id"] not in reqs:
            print(f"  dropped the touches of {m['id']}: no longer a usable key moment")
        elif m["id"] in ids:
            print(f"  kept the earlier judgment of {m['id']}: no answer this run")
            kept.append(m)
        else:
            kept.append(m)   # not asked this run
    order = {r["id"]: i for i, r in enumerate(rows)}
    out_rows = sorted(kept + list(judged.values()), key=lambda m: order[m["id"]])
    models = sorted({m.get("model") for m in out_rows if m.get("model")})
    out = {"asOf": build.AS_OF, "judged": datetime.date.today().isoformat(),
           "model": models[0] if len(models) == 1 else models, "note": NOTE, "state": SCOPE,
           "instructions": INSTRUCTIONS, "levels": LEVELS,
           "placeholders": "{name} is the industry's name and {id} its id in `industries`, in the instructions and each level.",
           "industries": [{"id": iid, "name": name} for iid, name, _ in inds], "moments": out_rows}
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(dumps(out))
    unjudged = [r["id"] for r in rows if r["id"] not in {m["id"] for m in out_rows}]
    print(f"wrote {os.path.relpath(OUT, ROOT)} ({len(out_rows)} of {len(rows)} moments judged"
          + (f"; not judged: {', '.join(unjudged)}" if unjudged else "") + ")")


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""
Jev's judgments of the candidates for the roster of major US companies (Assets/Resources/Data/economy/companies.json; the
eighth direction, Docs/research/advantage/brief-v6.md): for each candidate of Tools/companies/candidates.json, one request
asks together, over the same state (the company's name, ticker, years and the opening of its English Wikipedia article as
Tools/companies/fetch.py fetched it; and the 23 market industries of industries.json by id, name and NAICS coverage):
  industry  a Choice of the industry that earned most of its revenue, among the 23 market industries of industries.json
            (each option the industry's name, NAICS coverage and blurb), and
  major     a Score on five levels (0-4) of how major a player it was in that industry in the United States at its peak:
            0 a minor firm, 1 mid-sized, 2 among the larger dozen, 3 among the largest five, 4 the leader or co-leader.
Writes Docs/research/companies/judgments.json; Tools/companies/build.py reads it: the roster is the companies scoring 2.0 or
more, plus the 75 curated ones, each in the industry Jev chose at confidence 0.6 or more (else the authored one, as
Assets/Scripts/Economy/Model/CompanyLives.cs does with HillConfidence). These are Jev's judgments, not measurements.

The questions were tried on the anchors (--anchors: ten well-known companies and what they must show) before the whole run, and
only the questions were changed, never the answers. Answers are cached by the sha256 of the exact request in
Tools/companies/.cache/judge/, so a rerun asks only what changed and reproduces the rest; at most four requests are in flight.
The credential is found by Tools/jev.py and never printed or stored.

    python3 -I Tools/companies/judge.py                     # judge every candidate and write judgments.json
    python3 -I Tools/companies/judge.py --anchors           # the anchors against what they must show; writes nothing
    python3 -I Tools/companies/judge.py --only a,b          # some candidates; their rows replace theirs
    python3 -I Tools/companies/judge.py --dry               # each request's size and whether it is cached; calls nothing
    python3 -I Tools/companies/judge.py --show exxon        # print that company's request; calls nothing
    python3 -I Tools/companies/judge.py --offline           # cached answers only

A candidate without a market cap page (candidates.json "cmc": false) that is not curated has nothing to draw, so it is not
asked about; nor is one whose description could not be fetched (Ensign Group has no English Wikipedia article).
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
OUT = os.path.join(ROOT, "Docs", "research", "companies", "judgments.json")
INDUSTRIES = os.path.join(ROOT, "Assets", "Resources", "Data", "economy", "industries.json")
CACHE = os.path.join(HERE, ".cache", "judge")
sys.dont_write_bytecode = True
sys.path[0:0] = [HERE, os.path.join(ROOT, "Tools")]
import fetch  # noqa: E402  (Tools/companies/fetch.py: the candidates and their fetched descriptions)
import jev  # noqa: E402  (Tools/jev.py: credential discovery; the key is never printed)

WORKERS = 4          # requests in flight at once
MAJOR = 2.0          # the roster's threshold on the major score (build.py; 2.5 at the run of 8 Nov 2026)
CONFIDENT = 0.6      # Jev's industry replaces the authored one at this confidence or more (build.py)

# ------------------------------------------------------------------------------------------------------- the questions

# Tried on the anchors (below) on 8 Nov 2026, jev-1.14.0, before the whole run:
# - first wording ("beside all of the industry's US companies, public and private"): 9 of 10 anchors met; Grand Canyon
#   Education read as a minor education company (0.29; 0.84 on level 0) and its industry at 0.57 (its article's opening
#   says it "provides services to universities": education 0.57, professional services the rest);
# - the comparison set was then said to be the industry's businesses, listed or privately held, not nonprofits or
#   government bodies (a nonprofit university or hospital system is not a company the roster could hold): HCA, Goldman Sachs
#   and Union Pacific stayed near 2.5-2.6 (the top-five level with probability spread below it), Grand Canyon Education
#   stayed minor (0.33) and its industry rose to 0.62. The answers were not changed; the expectation that it is among the
#   larger dozen was the questionable one (about $1B of revenue beside Kaplan, Apollo, Bright Horizons and others), so the
#   second adjustment allowed was not spent. The whole run used this wording.
# Scores come out compressed: a clear leader scores about 3.4-3.9 (Walmart 3.85, JPMorgan 3.56, Exxon 3.39), a clear
# top-five player about 2.5-3.0, so a 2.5 threshold kept only the companies Jev was fairly sure were top five at their peak
# and missed obvious majors (PepsiCo 2.48, Target 2.46, Morgan Stanley 2.45, Merck 2.18): build.py uses 2.0;
# `p` keeps the whole distribution for another rule (build.py --major).
INDUSTRY = ("`company` is a US company: its name, ticker, the years it existed and the opening of its English Wikipedia "
            "article (`company.description`). Which of these industries of the US economy earned most of `company.name`'s "
            "revenue, judging by its main business over its history and weighing most the years when it was largest? Pick "
            "the industry of what it sells, as the US national accounts class a business by its main activity, not the "
            "industry of its suppliers or of its customers.")

MAJOR_Q = ("`company` is a US company: its name, ticker, the years it existed and the opening of its English Wikipedia "
           "article (`company.description`). Its industry is the one of the 23 broad industries of the US economy listed in "
           "`industries` that earned most of its revenue. At its peak, at any time from 1947 to 2025, how large a player "
           "was `company.name` in that whole industry in the United States, beside all of the industry's US businesses, "
           "listed or privately held (not nonprofits or government bodies), and not only those of its own niche, by "
           "revenue, assets or market value?")

LEVELS = [
    "A minor company of its industry: even at its peak it was small beside the industry's leading US companies, far below "
    "the largest dozen.",
    "A mid-sized company of its industry: well known, but even at its peak it stayed below the twelve or so largest US "
    "companies of its industry.",
    "At its peak, one of the larger dozen US companies of its industry, though not one of the five largest.",
    "At its peak, one of the five largest US companies of its industry, though not its leader.",
    "At its peak, the leader or co-leader of its industry in the United States: the industry's largest company, or one of "
    "its two largest.",
]

# The anchors (--anchors): companies whose industry and standing are well known, and what Jev must show for them: the industry
# chosen at confidence 0.6 or more, and the least (or most) level of the major score, met when Jev puts at least half its
# probability on that level or above (or at or below), as the Score docs advise for a decision that depends on one level.
# When one fails, the questions are what changes, never the answers.
ANCHORS = {
    "exxon": ("oil_gas", ">=", 4),
    "walmart": ("trade", ">=", 4),
    "jpmorgan": ("banking", ">=", 4),
    "goldman": ("finance", ">=", 3),
    "hca": ("health", ">=", 3),
    "waste_management": ("other_services", ">=", 3),
    "union_pacific": ("transport", ">=", 3),
    "grand_canyon": ("education", ">=", 2),
    "deere": ("manufacturing", None, None),
    "tootsie_roll": ("manufacturing", "<=", 1),
}

NOTE = ("These are Jev's judgments, not measurements: for each candidate, Jev (TypeSafe System One) chose the industry of the "
        "US economy that earned most of its revenue (`industry`: the choice, its confidence and the three likeliest with "
        "their probabilities) and placed how major a player it was in that industry in the United States at its peak on the "
        "five levels below (`major`: the probability-weighted level 0-4, its confidence, and `p`, the probability of each "
        "level), reading the opening of the company's English Wikipedia article. The roster (Tools/companies/build.py) is "
        "the companies scoring 2.0 or more plus the 75 curated ones, each in Jev's industry at confidence 0.6 or more and "
        "otherwise in the authored one.")

# ---------------------------------------------------------------------------------------------------------- the state


def industries():
    """The 23 market industries (the government's two left out) in industries.json's order: (id, name, naics, covers)."""
    data = fetch.load(INDUSTRIES)
    return [(i["id"], i["name"], i["naics"], i["blurb"].split(":")[0].strip()) for i in data["industries"]
            if i["tier"] != "gov"]


def questions(inds):
    return {
        "industry": {"type": "choice", "instructions": INDUSTRY,
                     "criteria": {iid: {"industry": name, "naics": naics, "covers": covers}
                                  for iid, name, naics, covers in inds}},
        "major": {"type": "score", "instructions": MAJOR_Q, "criteria": LEVELS},
    }


def years(c):
    start = f"founded {c['founded']}" if c.get("founded") else "founding year not given"
    if c.get("ended"):
        return f"{start}; ended {c['ended']} ({c.get('fate')})"
    return f"{start}; still operating" if c.get("fate", "alive") == "alive" else start


def state(c, told, inds):
    d = told.get(c["id"])
    company = {"name": c["name"], "ticker": c.get("ticker") or None, "years": years(c),
               "description": d["extract"] if d else None}
    return {"company": company, "industries": {iid: f"{name} (NAICS {naics})" for iid, name, naics, _ in inds}}


def digest(st):
    """What Jev was told about the company, as a short digest (build.py can tell when a description has changed since)."""
    return hashlib.sha256(json.dumps(st["company"], sort_keys=True, ensure_ascii=False).encode()).hexdigest()[:16]

# --------------------------------------------------------------------------------------------------------------- run


STATS = {"requests": 0, "cached": 0, "errors": 0, "tokens": 0, "model": None}
LOCK = threading.Lock()


def key(st, qs):
    blob = json.dumps({"state": st, "questions": qs}, sort_keys=True, ensure_ascii=False)
    return hashlib.sha256(blob.encode("utf-8")).hexdigest()


def ask(st, qs, offline):
    """Jev's answers for one request, from the cache when this exact request was asked before, with the date it was asked."""
    path = os.path.join(CACHE, key(st, qs) + ".json")
    if os.path.exists(path):
        result = fetch.load(path)
        with LOCK:
            STATS["cached"] += 1
            STATS["model"] = STATS["model"] or result.get("model")
        return result
    if offline:
        return None
    last = None
    for attempt in range(3):
        try:
            result = jev.call(st, qs, timeout=150)
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


def row(c, result, st):
    """The company's judgment row, or None when an answer is missing or malformed."""
    a = (result or {}).get("answers") or {}
    ind, maj = a.get("industry"), a.get("major")
    if not isinstance(ind, dict) or ind.get("type") != "choice" or not isinstance(maj, dict) or maj.get("type") != "score":
        return None
    probs = sorted((ind.get("probabilities") or {}).items(), key=lambda kv: -kv[1])
    p = maj.get("probabilities") or {}
    return {"id": c["id"], "name": c["name"], "authored": c["industry"], "curated": bool(c.get("curated")),
            "model": result.get("model"), "asked": result.get("asked"), "told": digest(st),
            "industry": {"choice": ind.get("choice"), "confidence": round(ind.get("confidence", 0), 3),
                         "top": [[k, round(v, 3)] for k, v in probs[:3]]},
            "major": {"score": round(maj.get("score", 0), 3), "confidence": round(maj.get("confidence", 0), 3),
                      "p": [round(p.get(str(level), 0), 3) for level in range(len(LEVELS))]}}


def at_least(r, level):
    return sum(r["major"]["p"][level:])


def at_most(r, level):
    return sum(r["major"]["p"][:level + 1])


def anchor_line(r, want):
    ind, op, level = want
    ok_ind = r["industry"]["choice"] == ind and r["industry"]["confidence"] >= CONFIDENT
    parts = [f"{r['id']}: industry {r['industry']['choice']} ({r['industry']['confidence']:.2f})"
             + ("" if ok_ind else f" [wanted {ind} at >= {CONFIDENT}: FAIL]")]
    ok_major = True
    if op == ">=":
        q = at_least(r, level)
        ok_major = q >= 0.5
        parts.append(f"major {r['major']['score']:.2f} p(>={level}) {q:.2f}" + ("" if ok_major else " [FAIL]"))
    elif op == "<=":
        q = at_most(r, level)
        ok_major = q >= 0.5
        parts.append(f"major {r['major']['score']:.2f} p(<={level}) {q:.2f}" + ("" if ok_major else " [FAIL]"))
    else:
        parts.append(f"major {r['major']['score']:.2f}")
    parts.append("p " + " ".join(f"{x:.2f}" for x in r["major"]["p"]))
    return ok_ind and ok_major, "; ".join(parts)


def dumps(out):
    """judgments.json with each company's row on one line."""
    rows = {}

    def mark(obj):
        token = f"@@row{len(rows)}@@"
        rows[json.dumps(token)] = json.dumps(obj, ensure_ascii=False)
        return token

    shaped = dict(out, companies=[mark(r) for r in out["companies"]])
    text = json.dumps(shaped, indent=1, ensure_ascii=False)
    for token, value in rows.items():
        text = text.replace(token, value)
    return text + "\n"


def main():
    args = sys.argv[1:]
    dry, offline, anchors = "--dry" in args, "--offline" in args, "--anchors" in args
    show = args[args.index("--show") + 1] if "--show" in args else None
    only = args[args.index("--only") + 1].split(",") if "--only" in args else None
    inds = industries()
    qs = questions(inds)
    cands = {c["id"]: c for c in fetch.candidates(anchors=True)}
    cache = fetch.Polite(True)   # the descriptions fetch.py cached; judging never fetches
    told = {}
    for cid, c in cands.items():
        d = fetch.wiki(cache, c["wiki"]) if c.get("wiki") else None
        if d:
            told[cid] = d
    asked = list(ANCHORS) if anchors else only
    for cid in (asked or []) + ([show] if show else []):
        if cid not in cands:
            sys.exit(f"{cid} is not a candidate")
    if show is not None:
        st = state(cands[show], told, inds)
        print(json.dumps({"state": st, "questions": qs}, indent=1, ensure_ascii=False))
        return
    ids = [cid for cid, c in cands.items() if (asked is None and not c.get("anchorOnly")) or (asked and cid in asked)]
    valueless = [cid for cid in ids if cands[cid].get("cmc") is False and not cands[cid].get("curated")]
    if valueless and asked is None:   # no page, no measured value, no approximate one: nothing to draw, so nothing to ask
        print("not judged (no market value to draw): " + ", ".join(valueless))
        ids = [cid for cid in ids if cid not in valueless]
    untold = [cid for cid in ids if cid not in told]
    if untold:
        print("no description fetched (run Tools/companies/fetch.py), so not judged: " + ", ".join(untold))
        ids = [cid for cid in ids if cid in told]
    reqs = {cid: state(cands[cid], told, inds) for cid in ids}
    print(f"{len(ids)} requests (an industry Choice of {len(inds)} options and a major Score a request)")
    if dry:
        for cid in ids:
            size = len(json.dumps({"state": reqs[cid], "questions": qs}, ensure_ascii=False))
            cached = os.path.exists(os.path.join(CACHE, key(reqs[cid], qs) + ".json"))
            print(f"  {cid}: {size} characters{', cached' if cached else ''}")
        return
    if not offline and not jev.available():
        sys.exit("Jev credential unavailable: no judgments made")

    def run(cid):
        return cid, ask(reqs[cid], qs, offline)

    judged = {}
    with concurrent.futures.ThreadPoolExecutor(max_workers=WORKERS) as pool:
        for cid, result in pool.map(run, ids):
            r = row(cands[cid], result, reqs[cid])
            if r is None:
                print(f"  no judgment of {cid}: {'no answer' if result is None else 'an answer missing or malformed'}")
                continue
            judged[cid] = r
    print(f"Jev: {STATS['requests']} asked, {STATS['cached']} cached, {STATS['errors']} errors, {STATS['tokens']} tokens, "
          f"model {STATS['model']}")
    if anchors:
        met = 0
        for cid, want in ANCHORS.items():
            if cid not in judged:
                print(f"  {cid}: not judged")
                continue
            ok, text = anchor_line(judged[cid], want)
            met += ok
            print("  " + text)
        print(f"anchors: {met} of {len(ANCHORS)} met (industry at confidence >= {CONFIDENT}; half the probability or more "
              "on the major level wanted)")
        return
    old = fetch.load(OUT) if os.path.exists(OUT) else {}
    kept = [r for r in old.get("companies", []) if r["id"] not in judged and r["id"] in cands
            and not cands[r["id"]].get("anchorOnly")]
    order = {cid: i for i, cid in enumerate(cands)}
    rows = sorted(kept + list(judged.values()), key=lambda r: order[r["id"]])
    models = sorted({r.get("model") for r in rows if r.get("model")})
    out = {"judged": datetime.date.today().isoformat(), "model": models[0] if len(models) == 1 else models, "note": NOTE,
           "thresholds": {"major": MAJOR, "industryConfidence": CONFIDENT},
           "state": ("`company`: name, ticker, years (authored in Tools/companies/candidates.json or company-lives.json) and "
                     "description (the opening of its English Wikipedia article, the Wikimedia REST API's page summary, "
                     "fetched by Tools/companies/fetch.py); `industries`: the 23 market industries of industries.json by "
                     "id, name and NAICS coverage. Nothing else."),
           "questions": qs, "companies": rows}
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(dumps(out))
    names = {iid: name for iid, name, _, _ in inds}
    moved = [r for r in rows if r["industry"]["choice"] != r["authored"] and r["industry"]["confidence"] >= CONFIDENT]
    unsure = [r for r in rows if r["industry"]["confidence"] < CONFIDENT]
    major = [r for r in rows if r["major"]["score"] >= MAJOR]
    print(f"wrote {os.path.relpath(OUT, ROOT)}: {len(rows)} companies; {len(major)} score {MAJOR} or more; "
          f"{len(moved)} moved to Jev's industry; {len(unsure)} keep the authored one (confidence below {CONFIDENT})")
    for r in moved:
        print(f"  moved {r['id']}: {names.get(r['authored'])} -> {names.get(r['industry']['choice'])} "
              f"({r['industry']['confidence']:.2f})")


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""
Builds Assets/Resources/Data/economy/companies.json, the roster of major US companies for the economy views (the eighth
direction, Docs/research/advantage/brief-v6.md), from
  - the candidates (Tools/companies/candidates.json, the 75 curated ones with their fields from company-lives.json),
  - Jev's judgments (Docs/research/companies/judgments.json, Tools/companies/judge.py): the industry that earned most of each
    company's revenue and how major a player it was in it at its peak,
  - the gated market values (Docs/research/companies/<cluster>.json, Tools/companies/fetch.py; the gate's records in
    Docs/research/companies/gate/<cluster>.json, Tools/advantage/verify.py --dir Docs/research/companies --cluster <cluster>),
  - company-lives.json's approximate waypoints for the curated companies.

The roster is the candidates Jev scored 2.0 or more as a major player ("major": true) and the 75 curated companies
("curated": true), each in the industry Jev chose at confidence 0.6 or more, else the authored one. Its values:
  "m"  a year-end market value from a fact the gate verified (verbatim quote, values inside it, Jev's support): the fact's id
       rides with it; only years within the company's life (none after the year it ended);
  "a"  an approximate recalled waypoint of company-lives.json (+-30-50%; private companies' funding valuations), kept only
       outside the span of the measured values (before the first or after the last), never presented as measured.
A company with no value at all is left out (the file says which). Nothing is interpolated here: the C# model
(Assets/Scripts/Economy/Model/Companies.cs, the build track's) decides how to draw between the points.

    python3 -I Tools/companies/build.py              # writes companies.json (and its .meta when missing), prints the roster
    python3 -I Tools/companies/build.py --check      # validates the written file against the contract; writes nothing
    python3 -I Tools/companies/build.py --major 2.0  # another threshold on Jev's major score (2.0 is the default, 2.5 the first run's); no
                                                     # Jev call is needed, the judgments are read from judgments.json
"""
import collections
import datetime
import json
import os
import sys
import uuid

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
DATA = os.path.join(ROOT, "Assets", "Resources", "Data", "economy")
OUT = os.path.join(DATA, "companies.json")
META_TEMPLATE = os.path.join(DATA, "politics.json.meta")   # a JSON TextAsset's .meta, for the format
INDUSTRIES = os.path.join(DATA, "industries.json")
RESEARCH = os.path.join(ROOT, "Docs", "research", "companies")
JUDGMENTS = os.path.join(RESEARCH, "judgments.json")
sys.dont_write_bytecode = True
sys.path[0:0] = [HERE]
import fetch  # noqa: E402  (Tools/companies/fetch.py: the candidates, the clusters)

AS_OF = "2026-11-08"
MAJOR = 2.0          # Jev's major score at which a candidate joins the roster. 2.5 was tried first: Jev's scores are
                     # compressed (a clear top-five player scores 2.5-3.0), so 2.5 left out PepsiCo, Merck, Target,
                     # ConocoPhillips, Morgan Stanley, Schwab, Capital One, American Express, Duke, Prologis, Adobe,
                     # Salesforce, Qualcomm and every professional-services firm, against the user's "every major player
                     # in every industry" (8 Nov 2026). 2.0 is "among the larger dozen" at its peak; 199 companies
CONFIDENT = 0.6      # Jev's industry replaces the authored one at this confidence or more
FATES = ("alive", "acquired", "merged", "bankrupt", "breakup")

NOTE = ("The roster of major US companies across the 23 market industries of industries.json, 1947-2025. Who is in it: the "
        "companies Jev (TypeSafe System One) scored 2.0 or more on a five-level judgment of how major a player each was in "
        "its US industry at its peak (0 a minor firm, 2 among the larger dozen, 3 among the largest five, 4 the leader or "
        "co-leader; \"major\": true), and the 75 hand-picked companies of company-lives.json (\"curated\": true), chosen "
        "for famous rises and falls. Each stands in the industry Jev chose as the one earning most of its revenue at "
        "confidence 0.6 or more, else in the authored one (\"judged\" keeps Jev's answers: they are judgments, not "
        "measurements; Docs/research/companies/judgments.json has the full distributions). \"value\" is the year-end market "
        "value in nominal billions of dollars as [year, usdBn, kind, fact]: \"m\" a measured value, CompaniesMarketCap's "
        "end-of-year market capitalization (its history begins in 1996 or at listing; the current year and a delisted "
        "company's last, not-year-end value left out), each from a research fact the truth gate verified (the fact id, in "
        "Docs/research/companies/<cluster>.json); \"a\" an approximate recalled waypoint from company-lives.json (+-30-50%; "
        "for private companies a funding valuation), kept only before or after the measured span and never to be drawn as "
        "measured. Candidates were today's largest US companies of each industry and the historical giants that still "
        "traded after 1996; giants that left the market before (Gulf Oil, RCA, Pan Am's peers) have no measured value and "
        "appear only if curated. Built by Tools/companies/build.py.")

# ------------------------------------------------------------------------------------------------------------- inputs


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def industry_ids():
    return [i["id"] for i in load(INDUSTRIES)["industries"]]


def gated_facts():
    """{fact id: (fact, gate record)} of every research fact whose cluster's gate record says verified, and the reasons of
    those that were not ({fact id: reason})."""
    ok, failed = {}, {}
    for cluster in fetch.CLUSTERS:
        path = os.path.join(RESEARCH, cluster + ".json")
        if not os.path.exists(path):
            continue
        facts = load(path).get("facts") or []
        gate_path = os.path.join(RESEARCH, "gate", cluster + ".json")
        records = load(gate_path).get("facts", {}) if os.path.exists(gate_path) else {}
        for f in facts:
            rec = records.get(f["id"])
            if rec and rec.get("status") == "verified":
                ok[f["id"]] = (f, rec)
            else:
                failed[f["id"]] = (rec or {}).get("reason", "not gated")
    return ok, failed


def judgments():
    if not os.path.exists(JUDGMENTS):
        return {}
    return {r["id"]: r for r in load(JUDGMENTS).get("companies", [])}

# -------------------------------------------------------------------------------------------------------------- build


def values(c, measured, lives):
    """[[year, usdBn, kind, fact id]] sorted by year: the gated measured values within the company's life, and the curated
    approximate waypoints outside their span."""
    rows = {}
    for fid, f in measured:
        for year, usd in f["rows"]:
            if c.get("ended") and year > c["ended"]:
                continue
            if c.get("founded") and year < c["founded"]:
                continue
            if usd <= 0:
                continue
            rows[year] = [year, round(usd, 4), "m", fid]
    if rows:
        lo, hi = min(rows), max(rows)
    else:
        lo = hi = None
    for year, usd in (lives.get(c["id"], {}).get("value") or []) if c.get("curated") else []:
        if lo is not None and lo <= year <= hi:
            continue
        if c.get("ended") and year > c["ended"]:
            continue
        rows.setdefault(year, [year, round(float(usd), 4), "a", None])
    return [rows[y] for y in sorted(rows)]


def build(threshold=MAJOR):
    inds = industry_ids()
    market = set(inds) - {"federal", "state_local"}
    cands = fetch.candidates()
    lives = {c["id"]: c for c in load(fetch.LIVES)["companies"]}
    judged = judgments()
    gated, failed = gated_facts()
    by_actor = collections.defaultdict(list)
    for fid, (f, _) in sorted(gated.items()):
        by_actor[f["actor"]].append((fid, f))
    out, left = [], collections.OrderedDict()
    for c in cands:
        j = judged.get(c["id"])
        major = bool(j and j["major"]["score"] >= threshold)
        curated = bool(c.get("curated"))
        if not (major or curated):
            left[c["id"]] = "not major" if j else "not judged"
            continue
        industry = c["industry"]
        if j and j["industry"]["choice"] in market and j["industry"]["confidence"] >= CONFIDENT:
            industry = j["industry"]["choice"]
        vals = values(c, by_actor.get(c["id"], []), lives)
        if not vals:
            left[c["id"]] = "no value"
            continue
        out.append({
            "id": c["id"], "name": c["name"], "ticker": c.get("ticker") or None, "industry": industry,
            "judged": ({"industry": j["industry"]["choice"], "confidence": j["industry"]["confidence"],
                        "major": j["major"]["score"], "majorConfidence": j["major"]["confidence"]} if j else None),
            "major": major, "curated": curated,
            "founded": c.get("founded"), "ended": c.get("ended"), "fate": c.get("fate") or "alive",
            "successor": c.get("successor"), "sells": c.get("sells"),
            "value": vals,
        })
    ids = {c["id"] for c in out}
    for c in out:   # a successor that is not drawn is no line to merge into
        if c["successor"] not in ids:
            c["successor"] = None
    doc = {"asOf": AS_OF, "note": NOTE, "industries": inds, "companies": out}
    return doc, left, failed, judged

# --------------------------------------------------------------------------------------------------------------- check


def check(doc, threshold=MAJOR):
    """The contract's rules; returns the list of problems (empty when the file is valid)."""
    problems = []
    inds = industry_ids()
    if doc.get("industries") != inds:
        problems.append("industries is not the 25 ids of industries.json in order")
    gated, _ = gated_facts()
    seen = set()
    for c in doc.get("companies", []):
        cid = c.get("id")
        where = f"{cid}:"
        if not cid or cid in seen:
            problems.append(f"{where} id missing or repeated")
        seen.add(cid)
        for k in ("id", "name", "ticker", "industry", "judged", "major", "curated", "founded", "ended", "fate", "successor",
                  "sells", "value"):
            if k not in c:
                problems.append(f"{where} no {k}")
        if c.get("industry") not in inds or c.get("industry") in ("federal", "state_local"):
            problems.append(f"{where} industry {c.get('industry')} is not a market industry of industries.json")
        j = c.get("judged")
        if j is not None and (j.get("industry") not in inds or not 0 <= j.get("major", -1) <= 4
                              or not 0 <= j.get("confidence", -1) <= 1 or not 0 <= j.get("majorConfidence", -1) <= 1):
            problems.append(f"{where} judged is malformed")
        if not (c.get("major") or c.get("curated")):
            problems.append(f"{where} neither major nor curated")
        if c.get("major") and (j is None or j["major"] < threshold):
            problems.append(f"{where} major without a judged score of {threshold} or more")
        if c.get("fate") not in FATES:
            problems.append(f"{where} fate {c.get('fate')} unknown")
        if c.get("fate") == "alive" and c.get("ended") is not None:
            problems.append(f"{where} alive but ended")
        vals = c.get("value") or []
        if not vals:
            problems.append(f"{where} no value")
        years = [v[0] for v in vals]
        if years != sorted(years) or len(set(years)) != len(years):
            problems.append(f"{where} values not sorted by year or a year repeated")
        for v in vals:
            if len(v) != 4 or not isinstance(v[0], int) or not isinstance(v[1], (int, float)) or v[1] <= 0:
                problems.append(f"{where} value {v} malformed")
                continue
            if v[2] == "m":
                rec = gated.get(v[3])
                if rec is None:
                    problems.append(f"{where} measured value {v[0]} cites {v[3]}, not a verified fact")
                elif not any(y == v[0] and abs(u - v[1]) < 1e-3 for y, u in rec[0]["rows"]):
                    problems.append(f"{where} measured value {v[0]} is not a row of {v[3]}")
                elif rec[0]["actor"] != cid:
                    problems.append(f"{where} measured value {v[0]} cites another company's fact {v[3]}")
            elif v[2] == "a":
                if v[3] is not None:
                    problems.append(f"{where} approximate value {v[0]} cites a fact")
                if not c.get("curated"):
                    problems.append(f"{where} approximate value {v[0]} on a company that is not curated")
            else:
                problems.append(f"{where} value kind {v[2]} unknown")
        if c.get("ended") is not None and years and max(years) > c["ended"]:
            problems.append(f"{where} a value after the year it ended")
        if c.get("founded") is not None and years and min(years) < c["founded"]:
            problems.append(f"{where} a value before the year it was founded")
    for c in doc.get("companies", []):
        if c.get("successor") is not None and c["successor"] not in seen:
            problems.append(f"{c['id']}: successor {c['successor']} is not in the file")
    return problems

# ---------------------------------------------------------------------------------------------------------------- main


def write_meta(path):
    meta = path + ".meta"
    if os.path.exists(meta):
        return False
    with open(META_TEMPLATE, encoding="utf-8") as f:
        lines = f.read().split("\n")
    lines = [f"guid: {uuid.uuid4().hex}" if ln.startswith("guid: ") else ln for ln in lines]
    with open(meta, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines))
    return True


def dumps(doc):
    """companies.json with each company on a few lines: its fields, then its values one row a line."""
    parts = []
    for c in doc["companies"]:
        head = json.dumps({k: v for k, v in c.items() if k != "value"}, ensure_ascii=False)[:-1]
        rows = ",\n    ".join(json.dumps(v, ensure_ascii=False) for v in c["value"])
        parts.append(f"  {head}, \"value\": [\n    {rows}]}}")
    top = {k: v for k, v in doc.items() if k != "companies"}
    text = json.dumps(top, indent=1, ensure_ascii=False)[:-2]
    return text + ",\n \"companies\": [\n" + ",\n".join(parts) + "\n ]\n}\n"


def main():
    args = sys.argv[1:]
    threshold = float(args[args.index("--major") + 1]) if "--major" in args else MAJOR
    if "--check" in args:
        if not os.path.exists(OUT):
            sys.exit(f"{os.path.relpath(OUT, ROOT)} not written yet")
        doc = load(OUT)
        problems = check(doc, threshold)   # a file built with --major X is checked with the same --major X
        for p in problems:
            print("  " + p)
        n = len(doc.get("companies", []))
        print(f"CHECK {'OK' if not problems else 'FAILED'}: {n} companies, {len(problems)} problems")
        sys.exit(1 if problems else 0)
    doc, left, failed, judged = build(threshold)
    problems = check(doc, threshold)
    if problems:
        for p in problems:
            print("  " + p)
        sys.exit(f"not written: {len(problems)} problems")
    text = dumps(doc)
    json.loads(text)
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    made = write_meta(OUT)
    names = {i["id"]: i["name"] for i in load(INDUSTRIES)["industries"]}
    per = collections.Counter(c["industry"] for c in doc["companies"])
    kinds = collections.Counter(v[2] for c in doc["companies"] for v in c["value"])
    print(f"wrote {os.path.relpath(OUT, ROOT)}{' and its .meta' if made else ''}: {len(doc['companies'])} companies "
          f"({sum(c['major'] for c in doc['companies'])} major, {sum(c['curated'] for c in doc['companies'])} curated), "
          f"{kinds['m']} measured values, {kinds['a']} approximate")
    for iid in doc["industries"]:
        if iid in ("federal", "state_local"):
            continue
        members = [c["id"] for c in doc["companies"] if c["industry"] == iid]
        print(f"  {names[iid]:<34} {per[iid]:>3}  {', '.join(members)}")
    why = collections.Counter(left.values())
    print("left out: " + ", ".join(f"{n} {w}" for w, n in why.items()))
    for cid, w in left.items():
        if w != "not major":
            print(f"  {cid}: {w}")
    if failed:
        print(f"facts not verified by the gate ({len(failed)}): " + "; ".join(f"{k} ({v})" for k, v in sorted(failed.items())))


if __name__ == "__main__":
    main()

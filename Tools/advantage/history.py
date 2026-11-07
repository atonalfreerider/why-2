#!/usr/bin/env python3
"""Builds Assets/Resources/Data/economy/stories.json: the history stories (Docs/hills-design/HISTORY.md) from the verified
research of round four (Docs/research/advantage/story-*.json, merged into model.json by merge.py) and Jev's judgments
(judgments.json, section "history").

Each story keeps only what rests on verified facts:
  actors   id, name, kind, qid, years, successor, role, and a series of parts by year, each value normalised to its base
           (people, troops, ships, or money in billions of its currency) with the unit as the fact wrote it
  moments, periods, moats (with Jev's wall heights), people (with their Wikidata ids, for the portraits)
  sources  the cited facts' claims and URLs

Sizes compare money at fixed rates (assumed, stated in the file's method): £1 = 24 livres = 24 francs of the 1790s;
dollars are taken as written. Labels always show the fact's own number and unit.

    python3 -I Tools/advantage/history.py
"""
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
RESEARCH = os.path.join(ROOT, "Docs", "research", "advantage")
OUT = os.path.join(ROOT, "Assets", "Resources", "Data", "economy", "stories.json")

STORIES = [
    {"id": "ai", "cluster": "story-ai", "title": "The AI revolution", "from": 1945, "to": 2026},
    {"id": "rev", "cluster": "story-revolutions", "title": "The age of revolutions", "from": 1775, "to": 1815},
]

MONEY_PARTS = {"gdp", "revenue", "debt", "marketValue", "valuation", "capex", "spending", "subsidy", "cost", "capital"}
COUNT_PARTS = {"population", "army", "troops", "navy", "ships", "adherents", "users", "employees", "people", "parameters",
               "transistors", "compute", "casualties", "executions"}
# for sizes only: money in pounds sterling of the time (1775-1815) or in dollars (the AI story)
TO_POUNDS = {"livre": 1 / 24, "franc": 1 / 24, "pound": 1.0, "sterling": 1.0, "£": 1.0, "dollar": 1 / 4.5, "$": 1 / 4.5}
SCALE_WORDS = [("trillion", 1e12), ("billion", 1e9), ("million", 1e6), ("thousand", 1e3), (r"\bbn\b", 1e9), (r"\bm\b", 1e6),
               (r"\btn\b", 1e12), (r"\bk\b", 1e3)]


def load(path, default=None):
    if not os.path.exists(path):
        return default
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def year_of(v):
    if v is None or v == "":
        return None
    if isinstance(v, (int, float)):
        return float(v)
    m = re.match(r"\s*(-?\d{1,4})(?:-(\d{1,2}))?", str(v))
    if not m:
        return None
    y = float(m.group(1))
    if m.group(2):
        y += (int(m.group(2)) - .5) / 12
    return round(y, 3)


def scale_of(unit):
    """The multiplier the unit's first scale word gives ("USD bn (US$1.2 trillion)" is in billions: the first word counts)."""
    u = (unit or "").lower()
    best = None
    for pat, mul in SCALE_WORDS:
        m = re.search(pat, u)
        if m and (best is None or m.start() < best[0]):
            best = (m.start(), mul)
    return best[1] if best else 1.0


def base_value(part, value, unit):
    """The value in its base: a count, or money in billions of its currency; None for shares and ratios."""
    u = (unit or "").lower()
    if "%" in u or "percent" in u or "share" in u or part in ("gdpShare", "share"):
        return None
    v = float(value) * scale_of(unit)
    if is_money(part, unit):
        return v / 1e9
    return v


def is_money(part, unit):
    """Whether a row counts money: never a count's part (a "Franco-Spanish fleet" is ships, not francs)."""
    u = (unit or "").lower()
    if part in COUNT_PARTS:
        return False
    return part in MONEY_PARTS or bool(re.search(r"\b(dollars?|livres?|francs?|pounds?|usd)\b|[£$]", u))


# The size of a row for drawing (a body's lobe is 2.2 times its cube root, a tube's .42 times): what one unit of size is
# for each kind of measure. People under arms, at work or in orders count in thousands, civilians in hundred thousands
# (a soldier draws as a hundred civilians); ships one by one; money in billions of dollars (the AI story) or millions of
# pounds of the time (the age of revolutions); a model's parameters in hundred millions; a chip's transistors in thousands.
PERSONNEL = re.compile(r"troop|soldier|sailor|\bmen\b|conscript|clergy|employee|founder|engineer|inventor|author|people who|staff|executions?|death|casualt|lost")


def size_of(story, part, base, unit):
    if base is None:
        return None
    u = (unit or "").lower()
    # not a size at its year: a quarter's flow beside years', a total over years, a year's interest beside a debt
    if re.search(r"quarter|cumulative|interest", u):
        return None
    if is_money(part, unit):
        return base / (0.001 if story == "rev" else 1.0)
    if "ship" in u:
        return base
    if part == "parameters":
        return base / 1e8
    if part == "transistors":
        return base / 1e3
    if part in ("population", "adherents", "users") and not PERSONNEL.search(u):
        return base / 1e5
    return base / 1e3


def pounds(part, value, unit):
    """Money in billions of pounds of the time (sizes only); None when the currency is unknown."""
    u = (unit or "").lower()
    for w, rate in TO_POUNDS.items():
        if w in u:
            return value * rate
    return None


def main():
    model = load(os.path.join(RESEARCH, "model.json"))
    if not model:
        sys.exit("model.json missing: run Tools/advantage/merge.py first")
    judg = load(os.path.join(RESEARCH, "judgments.json"), {}) or {}
    facts = model.get("facts", {})
    used = set()

    def ok(fid):
        return facts.get(fid, {}).get("status") == "verified"

    def cite(ids):
        out = [x for x in ids or [] if ok(x)]
        used.update(out)
        return out

    actors_by_id = {a["id"]: a for a in model.get("actors", [])}
    stories = []
    for st in STORIES:
        cluster = st["cluster"]
        data = load(os.path.join(RESEARCH, cluster + ".json"), {}) or {}
        prefix = st["id"]
        series = {}
        spans = {a.get("id"): (year_of(a.get("from")), year_of(a.get("to")), a.get("successor")) for a in data.get("actors", [])}
        moved = 0
        for row in data.get("series", []):
            if not ok(row.get("fact")):
                continue
            if "both sides" in (row.get("unit") or "").lower():
                continue    # a battle's total is not one side's size (the fact stays on its moment)
            try:
                v = base_value(row.get("part"), row.get("value"), row.get("unit"))
            except (TypeError, ValueError):
                continue
            entry = {"year": year_of(row.get("year")), "part": row.get("part"), "unit": row.get("unit"), "fact": row["fact"],
                     "raw": row.get("value"), "value": v}
            if entry["year"] is None:
                continue
            actor = row.get("actor")
            # a state measured after it ended is its successor (Great Britain's 1805 population is the United Kingdom's)
            span = spans.get(actor)
            if span and span[1] is not None and span[2] and span[2] in spans and entry["year"] > span[1] + .5:
                actor = span[2]
                moved += 1
            series.setdefault(actor, []).append(entry)
            used.add(row["fact"])
        if moved:
            print(f"{prefix}: {moved} values moved to the successor of an actor that had ended")
        actors = []
        for a in data.get("actors", []):
            aid = a.get("id")
            rows = sorted(series.get(aid, []), key=lambda r: r["year"])
            # a value for sizes: counts as they are, money in billions (pounds of the time for the age of revolutions)
            for r in rows:
                if r["value"] is not None and prefix == "rev" and is_money(r["part"], r["unit"]):
                    p = pounds(r["part"], r["value"], r["unit"])
                    r["value"] = p if p is not None else r["value"]
                r["size"] = size_of(prefix, r["part"], r["value"], r["unit"])
            actors.append({"id": aid, "name": a.get("name") or aid, "kind": a.get("kind"), "qid": a.get("qid"),
                           "from": year_of(a.get("from")), "to": year_of(a.get("to")), "successor": a.get("successor"),
                           "role": a.get("role"), "image": aid, "facts": cite(a.get("facts")),
                           "series": [{"year": r["year"], "part": r["part"], "unit": r["unit"], "fact": r["fact"], "value": r["value"], "raw": r["raw"],
                                       "size": None if r["size"] is None else round(r["size"], 6)}
                                      for r in rows if r["value"] is not None or r["part"] in ("gdpShare", "share")]})
        moments = []
        for m in data.get("moments", []):
            fl = cite(m.get("facts"))
            if not fl:
                continue
            moments.append({"id": m["id"], "year": year_of(m.get("date")) or year_of(m.get("year")), "date": m.get("date"), "title": m.get("title"),
                            "holder": m.get("holder"), "rivals": m.get("rivals") or [], "allies": m.get("allies") or [],
                            "advantage": m.get("advantage"), "power": m.get("power"), "triad": m.get("triad"),
                            "mechanism": m.get("mechanism"), "outcome": m.get("outcome"), "facts": fl})
        periods = [{"id": p["id"], "from": year_of(p.get("from")), "to": year_of(p.get("to")), "title": p.get("title"), "what": p.get("what"),
                    "actors": p.get("actors") or [], "facts": cite(p.get("facts"))} for p in data.get("periods", []) if cite(p.get("facts"))]
        jm = (judg.get("history") or {})
        moats = []
        for m in data.get("moats", []):
            mid = f"hmoat-{prefix}-{m.get('actor')}-{m.get('from')}"
            js = jm.get(mid) or {}
            srcs = []
            for x in m.get("sources") or []:
                fl = cite(x.get("facts"))
                if not fl:
                    continue
                w = js.get("wall_" + (x.get("source") or ""))
                srcs.append({"source": x.get("source"), "what": x.get("what"), "facts": fl,
                             "wall": round(w["score"], 2) if w else None})
            if srcs:
                moats.append({"id": mid, "actor": m.get("actor"), "from": year_of(m.get("from")), "to": year_of(m.get("to")),
                              "market": m.get("market"), "sources": srcs, "rivals": m.get("rivals") or [], "facts": cite(m.get("facts"))})
        people = [{"id": p["id"], "name": p.get("name"), "qid": p.get("qid"), "born": p.get("born"), "died": p.get("died"), "role": p.get("role"),
                   "image": p["id"], "actors": p.get("actors") or [], "facts": cite(p.get("facts"))} for p in data.get("people", [])]
        stories.append({"id": prefix, "title": st["title"], "from": st["from"], "to": st["to"], "actors": actors, "moments": moments,
                        "periods": periods, "moats": moats, "people": people})
        print(f"{prefix}: {len(actors)} actors ({sum(len(a['series']) for a in actors)} values), {len(moments)} moments, "
              f"{len(periods)} periods, {len(moats)} moats, {len(people)} people")
    # an actor in two stories (the United States of 1781 and of 1945) has an image for each: its flag then
    count = {}
    for st in stories:
        for a in st["actors"]:
            count[a["id"]] = count.get(a["id"], 0) + 1
    for st in stories:
        for a in st["actors"]:
            if count[a["id"]] > 1:
                a["image"] = f"{a['id']}-{st['id']}"
    out = {"asOf": model.get("asOf"),
           "method": "Every number is a verified fact (Tools/advantage/verify.py). Sizes compare money at fixed rates (assumed): "
                     "£1 = 24 livres or francs of the 1790s; dollars as written. A unit of size (a lobe 2.2 times its cube root) is a "
                     "thousand people under arms, at work or in orders; a hundred thousand civilians; a ship; a billion dollars (AI) "
                     "or a million pounds (1775-1815); a hundred million parameters; a thousand transistors. Walls are Jev's "
                     "judgments (0-4).",
           "stories": stories,
           "sources": {fid: {"claim": facts[fid].get("claim"), "url": facts[fid].get("url")} for fid in sorted(used) if fid in facts}}
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, separators=(",", ":"))
        f.write("\n")
    print(f"stories.json: {len(stories)} stories, {len(out['sources'])} sources; {os.path.getsize(OUT) // 1024} KB")


if __name__ == "__main__":
    sys.dont_write_bytecode = True
    main()

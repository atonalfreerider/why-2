#!/usr/bin/env python3
"""Builds Assets/Resources/Data/economy/stories.json: the history stories (Docs/hills-design/HISTORY.md) from the verified
research of round four (Docs/research/advantage/story-*.json, merged into model.json by merge.py) and Jev's judgments
(judgments.json, section "history").

Each story keeps only what rests on verified facts:
  actors   id, name, kind, qid, years, successor, role, and a series of parts by year, each value normalised to its base
           (people, troops, ships, or money in billions of its currency) with the unit as the fact wrote it
  moments, periods, moats (with Jev's wall heights), people (with their Wikidata ids, for the portraits)
  flows    money paid and capital lent or invested from one actor to another over the years a fact gives (Britain's
           subsidies to the coalitions, France's loans to the Americans, the rounds into Intel and Anthropic): only facts the
           gate verified that no skeptic rejected or found to be no flow (their "check"), each with its amount as the fact
           gives it and normalised as the series' money is (see build_flows); what is left out is printed with why
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

# One polity under two ids in the research: the monarchy as the Americans' ally (1778-83) is the Kingdom of France, whose
# debt the war raised (folded in, so the debt stands on the crown at the fiscal collapse).
ALIASES = {"rev": {"france": "kingdom-of-france"}}
# Alliance rows whose figure is one member's (the First Coalition's 800,000 are the Holy Roman Empire's).
MEMBER_SIZED = {("first-coalition", "story-revolutions-first-coalition-strength")}
# The crown is drawn by its money (income, debt), its army as a lobe; its people are the nation's own actor.
ORDERS = {"kingdom-of-france": ["revenue", "debt", "army", "population"]}
# A state's own forces, drawn as their own actors: the state's body leaves out their soldiers and ships when they stand.
FORCES = {"united-states": ["continental-army"], "kingdom-of-great-britain": ["british-army", "royal-navy"],
          "united-kingdom": ["british-army", "royal-navy"], "kingdom-of-france": ["french-army", "french-navy"],
          "french-republic": ["french-army", "french-navy"], "french-consulate": ["french-army", "french-navy", "grande-armee"],
          "first-french-empire": ["grande-armee", "french-army", "french-navy"], "prussia": ["prussian-army"],
          "russian-empire": ["russian-army"]}

# Flows (the flows map's proposal 6, Docs/research/politics/maps/government-flows-power-2026-11-07.json; HISTORY.md section 5).
# A flow fact is a fact of kind "flow", or an investment or contract with an end (from, to, fromName or toName). It is
# drawn when the gate verified it (model.json; a skeptic's "check": {"ok": false} makes it "rejected" there) and its check
# does not say "flow": false (a claim that stands but moved no money between its ends then: a pledge, a commitment with no
# schedule, a cost spent at home).
FLOW_FACT_KINDS = ("investment", "contract")
# The kind of flow of the facts researched before flows had kinds, as the skeptics read them (8 Nov 2026).
FLOW_KINDS = {
    "story-ai-marshall-plan": "aid",
    "story-ai-microsoft-openai-2019": "investment",
    "story-ai-nvidia-founding-capital": "investment",
    "story-ai-fairchild-raised-1957": "loan",           # agreed in August 1957 as a loan (Wikipedia, Traitorous eight)
    "story-ai-intel-debentures-1968": "loan",           # convertible debentures
    "story-revolutions-austria-subsidy-1809": "subsidy",
    "story-revolutions-britain-subsidies-total": "subsidy",
}
# The payer of record where the research left the payer outside the story (the skeptic, 8 Nov 2026): the Dutch loan of 1781
# was raised in the French King's name and under his guarantee, counted among "the preceding aids granted by the King" (the
# contract of 1783) and repaid to the royal treasury, so it is drawn from the crown, the Dutch lenders behind it.
PAYERS = {"story-revolutions-flow-dutch-loan-1781": "kingdom-of-france"}
# A total over years that holds flows drawn on their own (the skeptic, 8 Nov 2026: "draw the parts, and the total at most as
# what's left over"): the total is not drawn in the years its parts run (its "gaps"), so no pound is drawn twice. Britain's
# £46.3 million of cash subsidies (1793-1814) holds Austria's £250,000 of 1809 and the subsidies of Reichenbach (1813) and
# Chaumont (1814). Spread over its 22 years, the rest of it draws at most what is left over.
WITHIN = {
    "story-revolutions-austria-subsidy-1809": "story-revolutions-britain-subsidies-total",
    "story-revolutions-flow-britain-prussia-1813": "story-revolutions-britain-subsidies-total",
    "story-revolutions-flow-britain-russia-1813": "story-revolutions-britain-subsidies-total",
    "story-revolutions-flow-britain-allies-1814": "story-revolutions-britain-subsidies-total",
}

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
    """A date as a fractional year: a month at its middle, a day at its own place in the month (Waterloo, 18 June 1815, and
    the Congress of Vienna's Final Act, 9 June, stay apart)."""
    if v is None or v == "":
        return None
    if isinstance(v, (int, float)):
        return float(v)
    m = re.match(r"\s*(-?\d{1,4})(?:-(\d{1,2})(?:-(\d{1,2}))?)?", str(v))
    if not m:
        return None
    y = float(m.group(1))
    if m.group(2):
        month = int(m.group(2)) - 1
        y += (month + ((int(m.group(3)) - .5) / 31 if m.group(3) else .5)) / 12
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


SHARE_PARTS = {"gdpShare", "share", "industrialShare", "debtService"}


def is_share(part, unit):
    """A share or a ratio, not an amount: its part says so, or its unit starts with one ("% of global industrial output").
    A note that mentions a percent ("thousand troops (70% of the Allied 85,000)") does not make an amount a share."""
    return part in SHARE_PARTS or bool(re.match(r"\s*(%|percent|per\s*cent|share)", (unit or "").lower()))


def base_value(part, value, unit):
    """The value in its base: a count, or money in billions of its currency; None for shares and ratios."""
    if is_share(part, unit):
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
    if part == "compute":
        return None    # a model's training compute is a property of a product, not a size of the lab
    if part == "transistors":
        return base / 1e3
    if part in ("population", "adherents", "users") and not PERSONNEL.search(u):
        return base / 1e5
    return base / 1e3


# When a person may stand for an actor while browsing a story (its tours choose their portraits by hand): from age 16 to
# the end of the year of death, unless stated here. Selection only: these dates are never shown.
ADULT = 16
ACTIVE = {
    "george-iii": (None, 1811.09),   # the Regency from 5 February 1811
    "pitt": (None, 1806.06),         # died on 23 January 1806
}


def active_of(p):
    born, died = p.get("born"), p.get("died")
    lo = born + ADULT if isinstance(born, (int, float)) else None
    hi = died + 0.999 if isinstance(died, (int, float)) else None
    over = ACTIVE.get(p.get("id"))
    if over:
        lo = over[0] if over[0] is not None else lo
        hi = over[1] if over[1] is not None else hi
    return [lo, hi]


def plain_unit(unit, value):
    """The unit without a note that only restates the value in another form ('1,145 USD bn (US$1.145 trillion)'): the same
    number to the last digit, and a label short enough to stand beside its neighbours."""
    if not unit or not isinstance(value, (int, float)):
        return unit

    def same(m):
        return "" if abs(float(m.group(1)) * 1000 - value) < 0.5 else m.group(0)
    return re.sub(r"\s*\(US\$([0-9.]+) trillion\)", same, unit)


def pounds(part, value, unit):
    """Money in billions of pounds of the time (sizes only); None when the currency is unknown."""
    u = (unit or "").lower()
    for w, rate in TO_POUNDS.items():
        if w in u:
            return value * rate
    return None


def apply_aliases(data, mapping):
    """Rewrites every reference to an aliased id to its polity's id and drops the aliased actor (its facts join the
    polity's)."""
    if not mapping:
        return data
    def m(x):
        return mapping.get(x, x)
    keep = []
    by_id = {a.get("id"): a for a in data.get("actors", [])}
    for a in data.get("actors", []):
        if a.get("id") in mapping:
            target = by_id.get(mapping[a["id"]])
            if target is not None:
                target["facts"] = list(dict.fromkeys((target.get("facts") or []) + (a.get("facts") or [])))
            continue
        keep.append(a)
    data["actors"] = keep
    for r in data.get("series", []):
        r["actor"] = m(r.get("actor"))
    for mo in data.get("moments", []):
        mo["holder"] = m(mo.get("holder"))
        mo["rivals"] = [m(x) for x in mo.get("rivals") or []]
        mo["allies"] = [m(x) for x in mo.get("allies") or []]
    for p in data.get("periods", []):
        p["actors"] = list(dict.fromkeys(m(x) for x in p.get("actors") or []))
    for mt in data.get("moats", []):
        mt["actor"] = m(mt.get("actor"))
        mt["rivals"] = [m(x) for x in mt.get("rivals") or []]
    for pe in data.get("people", []):
        acts = pe.get("actors") or []
        pe["actors"] = list(dict.fromkeys(m(x) for x in (acts if isinstance(acts, list) else [acts])))
    for f in data.get("flows", []):
        for k in ("from", "to"):
            if k in f:
                f[k] = m(f[k])
    return data


def years_of(period):
    """A period's first and last calendar years ("1778-1782", "1813", "December 2015"); (None, None) when it names none."""
    ys = [int(y) for y in re.findall(r"(?<!\d)(1[5-9]\d\d|20\d\d)(?!\d)", str(period or ""))]
    return (min(ys), max(ys)) if ys else (None, None)


def flow_unit(unit):
    """A flow's unit for its label: the fact's own, without a note that it is a sum over the years (the label says "in total"
    and gives them: "million pounds (cumulative 1793-1814)" is "million pounds") or the source's abbreviation beside the word
    it abbreviates (Hansard's "l.": "pounds (l.)" is "pounds")."""
    plain = re.sub(r"\s*\(cumulative[^)]*\)", "", unit or "").strip()
    plain = re.sub(r"\bpounds\s*\(l\.\)", "pounds", plain).strip()
    return plain or unit


def flow_money(prefix, value, unit):
    """A flow's amount in the story's money: billions of pounds of the time for the age of revolutions (£1 = 24 livres or
    francs, as the series' money), billions of dollars as written for the AI story; None for a share, a ratio, a count or a
    currency the story cannot compare."""
    if not isinstance(value, (int, float)) or is_share(None, unit) or not is_money(None, unit):
        return None
    v = base_value(None, value, unit)
    if v is None:
        return None
    if prefix == "rev":
        return pounds(None, v, unit)
    return v if re.search(r"\b(usd|dollars?)\b|\$", (unit or "").lower()) else None


def short(reason, n=110):
    """A reason's first sentence, at most about n characters, for the build's log."""
    s = re.split(r"(?<=\.)\s", str(reason or "").strip(), 1)[0]
    return s if len(s) <= n else s[:n - 1].rstrip() + "…"


def build_flows(prefix, cluster, data, facts, mapping, story_actors, model_actors, cite):
    """The story's flows, from its cluster's flow facts (see FLOW_FACT_KINDS), and the build's notes: what is left out and
    why, and what was read in. Each flow:
      id, fact             "hflow-<story>-<fact id without its cluster>", and the fact it rests on
      from, to             the payer and the payee among the story's actors, through the story's aliases, the payer of record
                           (PAYERS) and a successor for an actor that had ended before the flow began; null for an end
                           outside the story, then named by fromName or toName (the research's words, or the actor's name)
      value, unit          the fact's own number and unit, for labels (a "(cumulative ...)" note dropped: "in total" says it)
      money, size          the amount in the story's money (billions of pounds of the time, or of dollars) and in its unit of
                           size (a million pounds, a billion dollars), as the series' money is; null when the fact gives no
                           money (a share of a company's capital), which draws nothing
      fromYear, toYear     the calendar years it ran, both included
      flowKind, total      what moved (subsidy, aid, grant, payment, purchase, indemnity; loan, investment, acquisition,
                           confiscation) and whether the amount is a sum over the years (else a year's rate)
      gaps                 years it is not drawn in, because flows inside it are drawn there on their own (WITHIN)"""
    spans = {a.get("id"): (year_of(a.get("from")), year_of(a.get("to")), a.get("successor")) for a in story_actors}
    names = {a.get("id"): a.get("name") or a.get("id") for a in model_actors}
    names.update({a.get("id"): a.get("name") or a.get("id") for a in story_actors})
    out, notes, kept = [], [], {}

    def leave(fid, why):
        notes.append(f"  left out {fid}: {why}")

    def alias(x):
        return mapping.get(x, x) if x else None

    def standing(aid, year):
        """The actor an id stands for in a year: itself, or its successor once it had ended (Great Britain after 1800)."""
        for _ in range(8):
            span = spans.get(aid)
            if not span or span[1] is None or not span[2] or span[2] not in spans or year <= span[1] + .5:
                return aid
            aid = span[2]
        return aid

    for f in data.get("facts", []):
        fid = f.get("id")
        ends = any(f.get(k) for k in ("from", "to", "fromName", "toName"))
        if not fid or not (f.get("kind") == "flow" or (f.get("kind") in FLOW_FACT_KINDS and ends)):
            continue
        status = facts.get(fid, {}).get("status")
        if status != "verified":
            leave(fid, short(facts.get(fid, {}).get("reason")) if status == "rejected" else f"not verified ({status})")
            continue
        check = f.get("check") if isinstance(f.get("check"), dict) else {}
        if check.get("flow") is False:
            leave(fid, "no flow, by its check: " + short(check.get("reason")))
            continue
        y0, y1 = years_of(f.get("period"))
        if y0 is None:
            leave(fid, f"no years in its period ({f.get('period')!r})")
            continue
        if (f.get("unit") or "").strip().lower() == "year":
            leave(fid, "no amount: its value is a year")
            continue
        payer, payee = alias(PAYERS.get(fid) or f.get("from")), alias(f.get("to"))
        if fid in PAYERS:
            notes.append(f"  {fid}: drawn from {payer}, the payer of record (PAYERS), not from {f.get('fromName') or f.get('from')}")
        from_name, to_name = (f.get("fromName") if payer is None else None), (f.get("toName") if payee is None else None)
        # an end the story does not draw is outside it, named
        if payer is not None and payer not in spans:
            notes.append(f"  {fid}: its payer {payer} is no actor of the story: drawn from outside it")
            payer, from_name = None, names.get(payer, payer)
        if payee is not None and payee not in spans:
            notes.append(f"  {fid}: its payee {payee} is no actor of the story: drawn to outside it")
            payee, to_name = None, names.get(payee, payee)
        if payer is None and payee is None:
            leave(fid, "neither end is an actor of the story")
            continue
        # an actor that had ended when the flow began is its successor (Britain's subsidy of 1809 is the United Kingdom's)
        for end in ("payer", "payee"):
            aid = payer if end == "payer" else payee
            moved = standing(aid, y0) if aid else None
            if moved != aid:
                notes.append(f"  {fid}: {end} {aid} had ended by {y0}: drawn from its successor {moved}" if end == "payer"
                             else f"  {fid}: {end} {aid} had ended by {y0}: drawn to its successor {moved}")
                if end == "payer":
                    payer = moved
                else:
                    payee = moved
        money = flow_money(prefix, f.get("value"), f.get("unit"))
        if money is None:
            notes.append(f"  {fid}: no money amount ({f.get('value')} {f.get('unit')}): kept unsized, drawn as nothing")
        total = f.get("total") if isinstance(f.get("total"), bool) else True
        if not isinstance(f.get("total"), bool):
            notes.append(f"  {fid}: no total given: read as the sum over {y0 if y0 == y1 else f'{y0}-{y1}'}")
        kind = f.get("flowKind") or FLOW_KINDS.get(fid)
        if not kind:
            kind = "investment" if f.get("kind") == "investment" else "payment"
            notes.append(f"  {fid}: no kind of flow given: read as {kind}")
        rest = fid[len(cluster) + 1:] if fid.startswith(cluster + "-") else fid
        rest = rest[5:] if rest.startswith("flow-") else rest
        entry = {"id": f"hflow-{prefix}-{rest}", "from": payer, "to": payee}
        if payer is None:
            entry["fromName"] = from_name
        if payee is None:
            entry["toName"] = to_name
        money = None if money is None else float(f"{money:.10g}")   # 18 million livres are 0.00075 billion pounds, not 0.000749999...
        entry.update({"value": f.get("value"), "unit": flow_unit(f.get("unit")), "money": money,
                      "size": None if money is None else round(money / (0.001 if prefix == "rev" else 1.0), 6),
                      "fromYear": y0, "toYear": y1, "flowKind": kind, "total": total, "fact": fid})
        out.append(entry)
        kept[fid] = entry
        cite(fid)

    # a total is not drawn in the years its parts run on their own; a part left out leaves its years to the total
    for part, whole in WITHIN.items():
        if whole in kept and part in kept:
            p = kept[part]
            gaps = set(kept[whole].get("gaps") or [])
            gaps.update(range(p["fromYear"], p["toYear"] + 1))
            kept[whole]["gaps"] = sorted(y for y in gaps if kept[whole]["fromYear"] <= y <= kept[whole]["toYear"])
    for whole in {w for w in WITHIN.values() if w in kept and kept[w].get("gaps")}:
        notes.append(f"  {whole}: not drawn in {', '.join(map(str, kept[whole]['gaps']))}, where flows inside it are (WITHIN)")
    out.sort(key=lambda x: (x["fromYear"], x["id"]))
    return out, notes


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
        data = apply_aliases(load(os.path.join(RESEARCH, cluster + ".json"), {}) or {}, ALIASES.get(st["id"], {}))
        prefix = st["id"]
        series = {}
        spans = {a.get("id"): (year_of(a.get("from")), year_of(a.get("to")), a.get("successor")) for a in data.get("actors", [])}
        kinds = {a.get("id"): a.get("kind") for a in data.get("actors", [])}
        # a fact can size both sides (250,000 French against 850,000 allies): only the same figure under the same fact is a member's
        member_facts = {(r.get("fact"), r.get("value")) for r in data.get("series", []) if kinds.get(r.get("actor")) != "alliance"}
        moved = 0
        for row in data.get("series", []):
            if not ok(row.get("fact")):
                continue
            if "both sides" in (row.get("unit") or "").lower():
                continue    # a battle's total is not one side's size (the fact stays on its moment)
            if kinds.get(row.get("actor")) == "alliance" and ((row.get("fact"), row.get("value")) in member_facts or (row.get("actor"), row.get("fact")) in MEMBER_SIZED):
                continue    # an alliance sized by one member's force is that member's, drawn once
            try:
                v = base_value(row.get("part"), row.get("value"), row.get("unit"))
            except (TypeError, ValueError):
                continue
            entry = {"year": year_of(row.get("year")), "part": row.get("part"), "unit": plain_unit(row.get("unit"), row.get("value")),
                     "fact": row["fact"], "raw": row.get("value"), "value": v}
            # a row's own limits: the earliest date it may stand (its scope began then) and a hold shorter than its part's
            if row.get("from") is not None:
                entry["from"] = year_of(row.get("from"))
            if row.get("hold") is not None:
                entry["hold"] = float(row.get("hold"))
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
            extra = {}
            if aid in ORDERS:
                extra["order"] = ORDERS[aid]
            if aid in FORCES:
                extra["forces"] = FORCES[aid]
            actors.append({**extra, "id": aid, "name": a.get("name") or aid, "kind": a.get("kind"), "qid": a.get("qid"),
                           "from": year_of(a.get("from")), "to": year_of(a.get("to")), "successor": a.get("successor"),
                           "role": a.get("role"), "image": aid, "facts": cite(a.get("facts")),
                           "series": [{"year": r["year"], "part": r["part"], "unit": r["unit"], "fact": r["fact"], "value": r["value"], "raw": r["raw"],
                                       "size": None if r["size"] is None else round(r["size"], 6),
                                       **{k: r[k] for k in ("from", "hold") if r.get(k) is not None}}
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
                             "wall": round(w["score"], 2) if w else None,
                             "confidence": round(w.get("confidence", 0), 2) if w else None})
            if srcs:
                moats.append({"id": mid, "actor": m.get("actor"), "from": year_of(m.get("from")), "to": year_of(m.get("to")),
                              "market": m.get("market"), "sources": srcs, "rivals": m.get("rivals") or [], "facts": cite(m.get("facts"))})
        people = [{"id": p["id"], "name": p.get("name"), "qid": p.get("qid"), "born": p.get("born"), "died": p.get("died"), "role": p.get("role"),
                   "image": p["id"], "actors": p.get("actors") or [], "facts": cite(p.get("facts")), "active": active_of(p)}
                  for p in data.get("people", [])]
        flows, flow_notes = build_flows(prefix, cluster, data, facts, ALIASES.get(st["id"], {}), data.get("actors", []),
                                        model.get("actors", []), used.add)
        stories.append({"id": prefix, "title": st["title"], "from": st["from"], "to": st["to"], "actors": actors, "moments": moments,
                        "periods": periods, "moats": moats, "people": people, "flows": flows})
        print(f"{prefix}: {len(actors)} actors ({sum(len(a['series']) for a in actors)} values), {len(moments)} moments, "
              f"{len(periods)} periods, {len(moats)} moats, {len(people)} people")
        print(f"{prefix}: {len(flows)} flows ({sum(1 for f in flows if f['money'] is not None)} with an amount)")
        for line in flow_notes:
            print(line)
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

#!/usr/bin/env python3
"""Merges the competitive-advantage research (Docs/research/advantage/<cluster>.json) and its truth gate into one model
(Docs/research/advantage/model.json), mechanically: no judgment is made here.

- facts: every cluster's facts with the gate's verdict (gate/<cluster>.json, else verification.json); only verified
  facts may carry a number into the model.
- actors: merged by id. Text fields come from the first cluster that has them, in the order of CLUSTERS (the
  strategist's corrections first); lists and fact ids are unions. Each present actor gets its numbers (market cap or
  valuation, revenue, margins, assets) from its own verified facts, preferring facts dated 2026 and the latest year;
  `pick` in an actor overrides a choice ({"marketCap": "<fact id>"}).
- flows, chokepoints, flashpoints, lineage: concatenated, deduplicated by id (first cluster wins); a flow whose amount
  rests on no verified fact is dropped and reported.
- scenarios: Docs/research/advantage/scenarios.json (the strategist's), if present.
- an item (flow, choke point, flashpoint, lineage link) repeated by id with "drop": true in the strategist's file is
  removed; an actor with "layer": "drop" is left out of the app.
A reference to an actor no cluster describes becomes a stub actor, reported, so nothing dangles.

Usage (from the repository root): python3 -I Tools/advantage/merge.py
"""
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
RESEARCH = os.path.join(ROOT, "Docs", "research", "advantage")
OUT = os.path.join(RESEARCH, "model.json")
CLUSTERS = ["strategist", "compute", "labs", "economy", "lineage", "century", "archetypes"]
SPECIAL = {"verification.json", "model.json", "judgments.json", "scenarios.json", "review.json"}
NUMBER_KINDS = ("marketCap", "valuation", "revenue", "runRate", "netIncome", "operatingIncome", "operatingMarginPct",
                "grossMarginPct", "assets", "capex", "headcount",
                # round two: the balance sheet, valuation, the share price's uses, losses
                "totalAssets", "cash", "ppe", "goodwill", "intangibles", "investments", "totalLiabilities", "debt", "leases",
                "equity", "peRatio", "netIncomeTTM", "revenueTTM", "buybacks", "dividends", "sbc", "burn", "segmentLoss", "commitments")
PLAIN_UNITS = ("operatingMarginPct", "grossMarginPct", "headcount", "peRatio")
TEXT = ("name", "kind", "country", "layer", "sector", "role", "advantage", "desire", "fear")
LISTS = ("powers", "standsOn", "competitors", "customers", "facts")


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def clusters():
    names = [n[:-5] for n in os.listdir(RESEARCH) if n.endswith(".json") and n not in SPECIAL]
    return sorted(names, key=lambda n: (CLUSTERS.index(n) if n in CLUSTERS else len(CLUSTERS), n))


def gate_records():
    rec = {}
    whole = os.path.join(RESEARCH, "verification.json")
    if os.path.exists(whole):
        rec.update(load(whole).get("facts", {}))
    gdir = os.path.join(RESEARCH, "gate")
    if os.path.isdir(gdir):
        for n in sorted(os.listdir(gdir)):
            if n.endswith(".json"):
                rec.update(load(os.path.join(gdir, n)).get("facts", {}))   # a cluster's own, newer run wins
    return rec


def number(v):
    if isinstance(v, (int, float)):
        return float(v)
    try:
        return float(str(v).replace(",", "").replace("$", "").strip())
    except ValueError:
        return None


def usd_bn(value, unit):
    """A money value in USD billions, from the unit the researcher wrote; None if the unit is not money in USD."""
    v = number(value)
    u = (unit or "").lower()
    if v is None or ("usd" not in u and "$" not in u and "dollar" not in u):
        return None
    if "trillion" in u or re.search(r"\btn\b|\bt\b", u):
        return v * 1000
    if "million" in u or re.search(r"\bm\b|\bmn\b", u):
        return v / 1000
    return v


def latest_year(period):
    years = [int(y) for y in re.findall(r"(?:19|20)\d\d", period or "")]
    return max(years) if years else 0


def main():
    gate = gate_records()
    report = {"dropped flows": [], "stub actors": [], "actors without size": [], "duplicates": []}
    facts, actors, order = {}, {}, []
    lists = {"flows": [], "chokepoints": [], "flashpoints": [], "lineage": []}
    seen = {k: set() for k in lists}
    names = clusters()
    for name in names:
        data = load(os.path.join(RESEARCH, name + ".json"))
        for f in data.get("facts", []):
            fid = f.get("id")
            if not fid or fid in facts:
                if fid:
                    report["duplicates"].append(fid)
                continue
            g = gate.get(fid, {})
            facts[fid] = {"claim": f.get("claim"), "actor": f.get("actor"), "kind": f.get("kind"), "value": f.get("value"),
                          "unit": f.get("unit"), "period": f.get("period"), "scope": f.get("scope"), "from": f.get("from"),
                          "to": f.get("to"), "url": f.get("url"), "quote": (f.get("quotes") or [""])[0],
                          "dated2026": bool(f.get("dated2026")), "cluster": name,
                          "status": g.get("status", "unverified"), "supportedJ": g.get("supportedJ"),
                          "verifiedOn": g.get("verifiedOn"), "reason": g.get("reason")}
        for a in data.get("actors", []):
            aid = a.get("id")
            if not aid:
                continue
            m = actors.get(aid)
            if m is None:
                actors[aid] = m = {"id": aid, "clusters": []}
                order.append(aid)
            m["clusters"].append(name)
            for k in TEXT:
                if a.get(k) and not m.get(k):
                    m[k] = a[k]
            if isinstance(a.get("triad"), dict) and not m.get("triad"):
                m["triad"] = a["triad"]
            for k in ("founded", "ended"):
                if a.get(k) and not m.get(k):
                    m[k] = a[k]
            for k in LISTS:
                for x in a.get(k) or []:
                    m.setdefault(k, [])
                    if x not in m[k]:
                        m[k].append(x)
            if isinstance(a.get("pick"), dict):
                m.setdefault("pick", {}).update({k: v for k, v in a["pick"].items() if k not in m.get("pick", {})})
        for key in lists:
            for item in data.get(key, []):
                if item.get("id"):
                    iid = item["id"]
                elif key == "chokepoints":
                    iid = "choke-" + item.get("actor", "") + "-" + re.sub(r"[^a-z0-9]+", "-", (item.get("controls") or "").lower())[:32].strip("-")
                else:
                    iid = f"{key}:{item.get('from', '')}>{item.get('to', '')}:{item.get('year', '')}"
                if iid in seen[key]:
                    report["duplicates"].append(iid)
                    continue
                seen[key].add(iid)
                lists[key].append(dict(item, id=iid, cluster=name))

    # the strategist removes an item by repeating its id with "drop": true (its file is merged first, so it wins)
    for key in lists:
        lists[key] = [x for x in lists[key] if not x.get("drop")]

    def ok(fid):
        return facts.get(fid, {}).get("status") == "verified"

    # flows rest on a verified amount
    flows = []
    for f in lists["flows"]:
        amount = number(f.get("usdBnPerYear"))
        if not ok(f.get("fact")) or amount is None or amount <= 0:
            report["dropped flows"].append(f"{f['id']} ({'no verified fact' if not ok(f.get('fact')) else 'no amount'})")
            continue
        flows.append(dict(f, usdBnPerYear=amount))
    lists["flows"] = flows

    # every referenced actor exists
    def need(aid, where):
        if aid and aid not in actors:
            actors[aid] = {"id": aid, "name": aid.replace("-", " ").title(), "stub": True, "clusters": []}
            order.append(aid)
            report["stub actors"].append(f"{aid} ({where})")
    for f in lists["flows"]:
        need(f.get("from"), f["id"]); need(f.get("to"), f["id"])
    for c in lists["chokepoints"]:
        need(c.get("actor"), c["id"])
    for p in lists["flashpoints"]:
        need(p.get("holder"), p["id"])
        for r in p.get("rivals") or []:
            need(r, p["id"])
    for l in lists["lineage"]:
        need(l.get("from"), l["id"]); need(l.get("to"), l["id"])

    # each actor's numbers, from its own verified facts
    by_actor = {}
    for fid, f in facts.items():
        if f["status"] == "verified" and f.get("actor") and f.get("kind") in NUMBER_KINDS:
            by_actor.setdefault(f["actor"], []).append(fid)
    for aid in order:
        a = actors[aid]
        a["era"] = "history" if a.get("layer") == "history" else "present"
        nums = {}
        for kind in NUMBER_KINDS:
            cands = [fid for fid in by_actor.get(aid, []) if facts[fid]["kind"] == kind]
            pick = (a.get("pick") or {}).get(kind)
            if pick and ok(pick):
                cands = [pick]
            if not cands:
                continue
            fid = sorted(cands, key=lambda x: (not facts[x]["dated2026"], -latest_year(facts[x]["period"])))[0]
            f = facts[fid]
            value = usd_bn(f["value"], f["unit"]) if kind not in PLAIN_UNITS else number(f["value"])
            if value is None:
                continue
            nums[kind] = {"value": value, "unit": "USD bn" if kind not in PLAIN_UNITS else f["unit"], "period": f["period"], "fact": fid}
        if "operatingMarginPct" not in nums and "operatingIncome" in nums and "revenue" in nums \
                and latest_year(nums["operatingIncome"]["period"]) == latest_year(nums["revenue"]["period"]) \
                and nums["revenue"]["value"] > 0:
            nums["operatingMarginPct"] = {"value": round(100 * nums["operatingIncome"]["value"] / nums["revenue"]["value"], 1),
                                          "unit": "%", "period": nums["revenue"]["period"],
                                          "fact": [nums["operatingIncome"]["fact"], nums["revenue"]["fact"]], "derived": True}
        a["numbers"] = nums
        a["candidates"] = {kind: [fid for fid in by_actor.get(aid, []) if facts[fid]["kind"] == kind]
                           for kind in ("commitments", "buybacks", "dividends", "sbc", "burn", "segmentLoss")
                           if any(facts[fid]["kind"] == kind for fid in by_actor.get(aid, []))}
        if a["era"] == "present" and not a.get("stub") and "marketCap" not in nums and "valuation" not in nums:
            report["actors without size"].append(aid)

    scen_path = os.path.join(RESEARCH, "scenarios.json")
    scenarios = load(scen_path).get("scenarios", []) if os.path.exists(scen_path) else []
    model = {"asOf": "2026-11-07", "clusters": names,
             "counts": {"facts": len(facts), "verified": sum(1 for f in facts.values() if f["status"] == "verified"),
                        "actors": len(order), "present": sum(1 for a in actors.values() if a.get("era") == "present"),
                        **{k: len(v) for k, v in lists.items()}, "scenarios": len(scenarios)},
             "actors": [actors[a] for a in order], **lists, "scenarios": scenarios, "facts": facts, "report": report}
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(model, f, indent=1, ensure_ascii=False)
        f.write("\n")
    print(json.dumps(model["counts"]))
    for k, v in report.items():
        if v:
            print(f"{k} ({len(v)}): {', '.join(v[:40])}{' ...' if len(v) > 40 else ''}")


if __name__ == "__main__":
    sys.dont_write_bytecode = True
    main()

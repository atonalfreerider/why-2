#!/usr/bin/env python3
"""Builds the app's competitive-advantage data (Assets/Resources/Data/economy/advantage.json) from the merged research
(Docs/research/advantage/model.json), Jev's judgments (judgments.json) and the strategist's review (review.json).

Every composition is here, in code, so the app only draws (Docs/hills-design/ADVANTAGE.md, section 5):
  advantage   A = s1 + s2/2 + s3/4 over Jev's seven power scores sorted (0-4 each), so 0..7: one power is enough
              (Helmer), a second and third reinforce it. A review override replaces a power's score before sorting.
  scenario    A_s = clamp(A + 0.9 * (score - 2), 0, 7), score Jev's 0-4 answer to "how would this actor's position
              change" under the scenario's premise; the 2030 projection is the same with Jev's durability score.
  motive      a flow's fear share = Jev's motive score / 4 (0 pure desire .. 1 pure fear).
  choke       strength = 1 - substitutable / 4.
  triad       a flashpoint's or actor's information, execution and environment scores (0-3), as given.
Numbers come only from facts the gate verified (sizes and margins dated 2026 through the 2026 gate); an actor without a
verified size gets none, and the app draws it as unverified. Judgments are labelled as judgments.

Usage (from the repository root): python3 -I Tools/advantage/build.py [--judgments PATH]
"""
import json
import math
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
RESEARCH = os.path.join(ROOT, "Docs", "research", "advantage")
OUT = os.path.join(ROOT, "Assets", "Resources", "Data", "economy", "advantage.json")
LIVES = os.path.join(ROOT, "Assets", "Resources", "Data", "economy", "company-lives.json")
sys.dont_write_bytecode = True

POWER_KEYS = ["scale", "network", "counter", "switching", "brand", "cornered", "process"]
POWER_NAMES = {"scale": "scale economies", "network": "network economies", "counter": "counter-positioning",
               "switching": "switching costs", "brand": "branding", "cornered": "cornered resource",
               "process": "process power", "none": "no durable power"}
SCENARIO_STEP, MAX_A = 0.9, 7.0

# the supply chain from the crest outward (the past) to the buyers (the future); the flanks beside it
BANDS = [("energy", "Energy"), ("tools", "Tools & IP"), ("fabs", "Fabs & memory"), ("chips", "Chips & systems"),
         ("clouds", "Clouds"), ("models", "Models"), ("apps", "Apps & platforms"), ("buyers", "Buyers")]
LAYER_BAND = {"energy": "energy", "tools": "tools", "fabs": "fabs", "memory": "fabs", "chips": "chips",
              "systems": "chips", "clouds": "clouds", "models": "models", "apps": "apps", "buyers": "buyers"}
# the rest of the economy stands in the band nearest its role
SECTOR_BAND = [("oil|gas|energy|utilit|power|mining|material", "energy"),
               ("auto|vehicle|car|industrial|aerospace|defen|manufactur|pharma|drug|semicond", "fabs"),
               ("telecom|network|logistic|transport", "clouds"),
               ("", "apps")]
ALIASES = {"google": "alphabet", "eli-lilly": "lilly", "exxon-mobil": "exxon", "johnson-and-johnson": "jnj",
           "bank-of-america": "bofa", "coca-cola": "coca_cola", "general-electric": "ge", "us-steel": "ussteel",
           "general-motors": "gm", "home-depot": "homedepot", "bear-stearns": "bearstearns", "at-t": "att",
           "washington-mutual": "wamu", "pan-am": "panam", "toys-r-us": "toysrus", "procter-gamble": "pg",
           "berkshire-hathaway": "berkshire", "jp-morgan": "jpmorgan"}


def load(path, default=None):
    if not os.path.exists(path):
        return default
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def alnum(s):
    return re.sub(r"[^a-z0-9]", "", (s or "").lower())


def money(v):
    return f"${v / 1000:.1f}T".replace(".0T", "T") if v >= 1000 else f"${v:.0f}B" if v >= 10 else f"${v:.1f}B"


def advantage(powers):
    s = sorted(powers.values(), reverse=True) + [0, 0, 0]
    return round(s[0] + s[1] / 2 + s[2] / 4, 3)


def band_of(a):
    layer = a.get("layer") or ""
    if layer in LAYER_BAND:
        return LAYER_BAND[layer], "center"
    if layer == "capital":
        return "clouds", "capital"
    if layer == "state":
        return None, "state"
    sector = (a.get("sector") or "").lower()
    if any(w in sector for w in ("bank", "financ", "insur", "invest", "asset", "capital")):
        return "clouds", "capital"
    for pattern, band in SECTOR_BAND:
        if re.search(pattern, sector):
            return band, "economy"
    return "apps", "economy"


def main():
    args = sys.argv[1:]
    model = load(os.path.join(RESEARCH, "model.json"))
    judg = load(args[args.index("--judgments") + 1] if "--judgments" in args else os.path.join(RESEARCH, "judgments.json"), {})
    review = load(os.path.join(RESEARCH, "review.json"), {})
    lives = load(LIVES, {}).get("companies", [])
    facts = model["facts"]
    overrides = review.get("overrides", {})
    jact, jflow = judg.get("actors", {}), judg.get("flows", {})
    jchoke, jflash = judg.get("chokepoints", {}), judg.get("flashpoints", {})
    scen_ids = [s["id"] for s in model.get("scenarios", [])]
    used = set()

    def ok(fid):
        return isinstance(fid, str) and facts.get(fid, {}).get("status") == "verified"

    def cite(fid):
        if ok(fid):
            used.add(fid)
            return fid
        return None

    life_by = {}
    for life in lives:
        life_by[life["id"]] = life["id"]
        life_by[alnum(life["id"])] = life["id"]
        life_by["name:" + alnum(life.get("name"))] = life["id"]

    def lifeline(a):
        for key in (ALIASES.get(a["id"]), a["id"], alnum(a["id"]), "name:" + alnum(a.get("name"))):
            if key and key in life_by:
                return life_by[key]
        return None

    actors, problems = [], []
    for a in model["actors"]:
        if a.get("era") != "present" or a.get("stub") or a.get("layer") in (None, "drop", "history"):
            continue
        band, flank = band_of(a)
        nums = a.get("numbers", {})
        size = None
        for kind in ("marketCap", "valuation"):
            n = nums.get(kind)
            if n and cite(n["fact"]) and n["value"] > 0:
                size = {"usdBn": round(n["value"], 1), "label": money(n["value"]), "kind": kind, "period": n.get("period"),
                        "fact": n["fact"]}
                break
        margin = None
        n = nums.get("operatingMarginPct")
        if n:
            fids = n["fact"] if isinstance(n["fact"], list) else [n["fact"]]
            if all(cite(f) for f in fids):
                margin = {"pct": round(n["value"], 1), "period": n.get("period"), "facts": fids, "derived": bool(n.get("derived"))}
        j = jact.get(a["id"], {})
        ov = overrides.get(a["id"], {})
        powers = {k: round(j["power_" + k]["score"], 2) for k in POWER_KEYS if "power_" + k in j}
        for k, v in (ov.get("powers") or {}).items():
            if k in POWER_KEYS:
                powers[k] = float(v)
        judged = len(powers) == len(POWER_KEYS)
        A = advantage(powers) if judged else None
        rec = {"id": a["id"], "name": a.get("name") or a["id"], "kind": a.get("kind"), "country": a.get("country"),
               "band": band, "flank": flank, "sector": a.get("sector"), "lifeline": lifeline(a),
               "size": size, "margin": margin, "judged": judged,
               "role": a.get("role"), "advantage": a.get("advantage"), "desire": a.get("desire"), "fear": a.get("fear"),
               "sources": [f for f in (a.get("facts") or []) if cite(f)][:8]}
        if judged:
            rec["powers"] = powers
            rec["A"] = A
            prim = ov.get("primary") or (j.get("primary") or {}).get("choice")
            rec["primary"] = prim
            rec["primaryConfidence"] = (j.get("primary") or {}).get("confidence")
            rec["triad"] = {k: round(j["triad_" + k]["score"], 2) for k in ("information", "execution", "environment") if "triad_" + k in j}
            d = (j.get("durability") or {}).get("score")
            if d is not None:
                rec["durability"] = round(d, 2)
                rec["a2030"] = round(min(MAX_A, max(0, A + SCENARIO_STEP * (d - 2))), 3)
            rec["scenario"] = {}
            for sid in scen_ids:
                q = j.get("scenario_" + sid)
                if q:
                    rec["scenario"][sid] = round(min(MAX_A, max(0, A + SCENARIO_STEP * (q["score"] - 2))), 3)
            if ov.get("reason"):
                rec["override"] = ov["reason"]
        else:
            problems.append(f"{a['id']}: no Jev judgment")
        if size is None and flank != "state" and band != "buyers":
            problems.append(f"{a['id']}: no verified size")
        actors.append(rec)
    ids = {a["id"] for a in actors}

    flows = []
    for f in model.get("flows", []):
        if f.get("from") not in ids or f.get("to") not in ids or not cite(f.get("fact")):
            continue
        m = (jflow.get(f["id"]) or {}).get("motive")
        flows.append({"id": f["id"], "from": f["from"], "to": f["to"], "usdBn": round(f["usdBnPerYear"], 2),
                      "what": f.get("what"), "kind": f.get("kind"), "basis": f.get("basis"), "period": f.get("period"),
                      "fear": round(m["score"] / 4, 3) if m else None, "fact": f["fact"]})
    scale = {s["id"]: s.get("flowScale") or {} for s in model.get("scenarios", [])}
    for fl in flows:
        fl["scale"] = {}
        for sid, table in scale.items():
            x = table.get(fl["id"], table.get(fl["to"], table.get(fl["from"])))
            if x is not None:
                fl["scale"][sid] = float(x)

    chokes = []
    for c in model.get("chokepoints", []):
        if c.get("actor") not in ids:
            continue
        sub = (jchoke.get(c["id"]) or {}).get("substitutable")
        chokes.append({"id": c["id"], "actor": c["actor"], "controls": c.get("controls"), "share": c.get("share"),
                       "alternatives": c.get("alternatives"), "strength": round(1 - sub["score"] / 4, 3) if sub else None,
                       "sources": [f for f in c.get("facts") or [] if cite(f)]})

    names = {a["id"]: a.get("name") or a["id"] for a in model["actors"]}
    flashes = []
    for p in model.get("flashpoints", []):
        j = jflash.get(p["id"], {})
        year = p.get("year")
        try:
            year = int(str(year)[:4]) if year not in (None, "") else None
        except ValueError:
            year = None
        flashes.append({"id": p["id"], "year": year, "era": p.get("era"), "holder": p.get("holder"),
                        "holderName": names.get(p.get("holder"), p.get("holder")),
                        "rivals": p.get("rivals") or [], "rivalNames": [names.get(r, r) for r in p.get("rivals") or []],
                        "title": p.get("title"), "advantage": p.get("advantage"), "mechanism": p.get("mechanism"),
                        "outcome": p.get("outcome"),
                        "triad": {k: round(j["triad_" + k]["score"], 2) for k in ("information", "execution", "environment") if "triad_" + k in j},
                        "power": (j.get("power") or {}).get("choice") or p.get("power"),
                        "inArena": p.get("holder") in ids and all(r in ids for r in p.get("rivals") or []),
                        "sources": [f for f in p.get("facts") or [] if cite(f)]})
    flashes.sort(key=lambda x: (x["year"] is None, x["year"] or 0))

    lineage = []
    for l in model.get("lineage", []):
        y = l.get("year")
        try:
            y = int(str(y)[:4])
        except (TypeError, ValueError):
            continue
        lineage.append({"from": l.get("from"), "to": l.get("to"), "fromName": names.get(l.get("from"), l.get("from")),
                        "toName": names.get(l.get("to"), l.get("to")), "year": y, "how": l.get("how"),
                        "sources": [f for f in l.get("facts") or [] if cite(f)]})
    lineage.sort(key=lambda x: x["year"])

    scenarios = [{"id": s["id"], "title": s.get("title"), "premise": s.get("premise"), "reading": s.get("reading"),
                  "friendly": s.get("friendly") or [], "unfriendly": s.get("unfriendly") or [],
                  "signposts": s.get("signposts") or []} for s in model.get("scenarios", [])]
    sources = {fid: {"claim": facts[fid]["claim"], "url": facts[fid]["url"]} for fid in sorted(used)}
    out = {"asOf": model.get("asOf"), "judge": judg.get("model"),
           "method": "Altitude: A = s1 + s2/2 + s3/4 of Jev's seven power scores (0-4), 0-7; scenarios and 2030 move it by "
                     "0.9 per step of Jev's 0-4 answer. Sizes and margins: verified facts only.",
           "bands": [{"id": b, "name": n} for b, n in BANDS],
           "powers": POWER_NAMES, "actors": actors, "flows": flows, "chokepoints": chokes, "flashpoints": flashes,
           "lineage": lineage, "scenarios": scenarios, "sources": sources}
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, separators=(",", ":"))
        f.write("\n")
    judged = sum(1 for a in actors if a["judged"])
    print(f"advantage.json: {len(actors)} actors ({judged} judged, {sum(1 for a in actors if a['size'])} sized), "
          f"{len(flows)} flows, {len(chokes)} choke points, {len(flashes)} flashpoints "
          f"({sum(1 for p in flashes if p['inArena'])} in the arena), {len(lineage)} lineage steps, {len(scenarios)} scenarios, "
          f"{len(sources)} sources; {os.path.getsize(OUT) // 1024} KB")
    for p in problems[:40]:
        print("  -", p)
    if judged:
        top = sorted((a for a in actors if a["judged"]), key=lambda a: -a["A"])[:12]
        print("  top by A:", ", ".join(f"{a['id']} {a['A']:.2f} ({a.get('primary')})" for a in top))


if __name__ == "__main__":
    main()

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

Usage (from the repository root): python3 -I Tools/advantage/build.py [--model PATH] [--judgments PATH]
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
STRATEGY_NAMES = {"toll": "toll collector", "landgrab": "land grab on the share price", "fortress": "cash fortress",
                  "builder": "debt-financed builder", "consolidator": "consolidator", "challenger": "challenger",
                  "utility": "utility or commodity supplier"}

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


def number(v):
    if isinstance(v, (int, float)):
        return float(v)
    try:
        return float(str(v).replace(",", "").replace("$", "").strip())
    except ValueError:
        return None


def usd_bn(value, unit):
    """A money value in USD billions from the unit the researcher wrote; None if it is not money in USD."""
    v = number(value)
    u = (unit or "").lower()
    if v is None or ("usd" not in u and "$" not in u and "dollar" not in u):
        return None
    if "trillion" in u or re.search(r"\btn\b|\bt\b", u):
        return v * 1000
    if "million" in u or re.search(r"\bm\b|\bmn\b", u):
        return v / 1000
    return v


def year_of(period):
    years = [int(y) for y in re.findall(r"(?:19|20)\d\d", period or "")]
    return max(years) if years else 0


def per_year(value, period, kind=""):
    """A flow or income figure as a year's: a quarter x 4, nine months x 4/3, a half x 2; TTM and run-rates as they are."""
    if value is None:
        return None
    per = (period or "").lower()
    if kind in ("revenueTTM", "netIncomeTTM", "runRate") or re.search(r"ttm|trailing|twelve months|12 months|run-rate|run rate|annuali", per):
        return value
    if re.search(r"\b(f?q[1-4]|quarter)\b", per):
        return value * 4
    if re.search(r"nine months|9 months|first three quarters", per):
        return value * 4 / 3
    if re.search(r"\bh[12]\b|\b[12]h\b|first half|six months|6 months", per):
        return value * 2
    return value


NOT_YEARLY = (r"cumulative|accumulated|forecast|\b(19|20)\d\d\s*(-|–|to)\s*(19|20)?\d\d\b|over (five|four|three|\d+) years|since|"
              r"\b(19|20)\d\d\b[^()]*\bto\b[^()]*\b(19|20)\d\d\b")


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


def simulation(actors, flows, owners, scenarios, problems):
    """Runs Tools/advantage/simulate.py (the Darwinian simulation) on the companies, if the module exists."""
    path = os.path.join(HERE, "simulate.py")
    if not os.path.exists(path):
        return None
    import importlib.util
    spec = importlib.util.spec_from_file_location("simulate", path)
    sim = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(sim)
    research = load(os.path.join(RESEARCH, "model.json"), {})
    rnums = {a["id"]: a.get("numbers", {}) for a in research.get("actors", [])}
    companies = []
    for a in actors:
        if a["flank"] == "state" or a["band"] == "buyers" or not a.get("size"):
            continue
        n = rnums.get(a["id"], {})
        sheet = a.get("balance") or {}

        def val(kind):
            x = n.get(kind)
            return per_year(x["value"], x.get("period"), kind) if x else None
        # revenue: the latest-dated of the trailing year, the latest reported period (as a year's) and the run-rate
        cands = [(year_of((n.get(k) or {}).get("period")), i, val(k)) for i, k in enumerate(("revenueTTM", "runRate", "revenue")) if n.get(k)]
        revenue = max(cands)[2] if cands else None
        margin = (a.get("margin") or {}).get("pct")
        net = val("netIncomeTTM") or val("netIncome")
        if net is None and a.get("pe"):
            net = a["size"]["usdBn"] / a["pe"]   # a negative P/E keeps a loss a loss
        companies.append({
            "id": a["id"], "name": a["name"], "layer": a["band"], "private": a.get("private", False), "marketCap": a["size"]["usdBn"],
            "pe": a.get("pe") if (a.get("pe") or 0) > 0 else None, "revenue": revenue, "netIncome": net,
            "growth": None, "cash": sheet.get("cash"), "debt": sheet.get("debt"), "equity": sheet.get("equity"),
            "capex": val("capex"), "commitments": sheet.get("commitments"), "commitmentYears": 4 if sheet.get("commitments") else None,
            "burn": a.get("burn"), "A": a.get("A") if a.get("judged") else None,
            "durability": a.get("durability") if a.get("durability") is not None else 2,
            "scenario": {sid: round(2 + (A_s - a["A"]) / SCENARIO_STEP, 3) for sid, A_s in (a.get("scenario") or {}).items()} if a.get("judged") else {},
            "dependsOn": [{"id": f["to"] if f["from"] == a["id"] else f["from"], "kind": "supplier" if f["from"] == a["id"] else "customer", "usdBn": f["usdBn"]}
                          for f in flows if a["id"] in (f["from"], f["to"])],
            "owners": [{"id": o["id"], "pct": st.get("pct")} for o in owners for st in o["stakes"] if st["company"] == a["id"] and st.get("pct")],
            "operatingMarginPct": margin,
        })
    data = {"asOf": "2026-11-07", "years": [2026, 2027, 2028, 2029, 2030], "companies": companies, "scenarios": scenarios}
    with open(os.path.join(HERE, ".cache", "sim-input.json") if os.path.isdir(os.path.join(HERE, ".cache")) else os.devnull, "w") as f:
        json.dump(data, f, indent=1)
    try:
        run = getattr(sim, "simulate", None) or getattr(sim, "run")
        out = run(data)
    except Exception as e:   # the app keeps working without its simulation
        problems.append(f"simulation failed: {e}")
        return None
    # a company whose revenue, cash and debt are all assumed is not simulated: its fate would be the fallbacks' fate, not
    # its own. It holds its 2026 state in every world, and the panel says so.
    blind = sorted(cid for cid, notes in (out.get("fallbacks") or {}).items()
                   if sum(1 for n in notes if n.split(":")[0] in ("revenue", "cash", "debt")) >= 3)
    now = ((out.get("worlds") or {}).get("now") or {}).get("years") or {}
    first = now.get(min(now, key=int)) if now else {}
    for w in (out.get("worlds") or {}).values():
        for states in (w.get("years") or {}).values():
            for cid in blind:
                if cid in states and cid in first:
                    states[cid] = dict(first[cid])
        w["events"] = [e for e in w.get("events") or [] if e.get("company") not in blind]
    out["notSimulated"] = blind
    return out


def main():
    args = sys.argv[1:]
    model = load(args[args.index("--model") + 1] if "--model" in args else os.path.join(RESEARCH, "model.json"))
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

    strategist = load(os.path.join(RESEARCH, "strategist.json"), {})
    arena_ids = {a["id"] for a in strategist.get("actors", []) if a.get("layer") not in (None, "drop", "history", "owners")}
    actors, problems = [], []
    for a in model["actors"]:
        if a.get("era") != "present" or a.get("stub") or a.get("layer") in (None, "drop", "history", "owners"):
            continue
        nums0 = a.get("numbers", {})
        if a["id"] not in arena_ids and not (nums0.get("marketCap") or nums0.get("valuation")):
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
        sheet, sheet_facts, date = {}, [], None
        anchor = nums.get("equity") or nums.get("totalAssets")
        sheet_year = year_of(anchor.get("period")) if anchor else 0
        for kind in ("totalAssets", "cash", "ppe", "goodwill", "intangibles", "investments", "totalLiabilities", "debt", "leases", "equity"):
            n = nums.get(kind)
            if n and ok(n["fact"]):
                if sheet_year and year_of(n.get("period")) and year_of(n.get("period")) != sheet_year:
                    problems.append(f"{a['id']}: {kind} dated {n.get('period')} left out of a {sheet_year} balance sheet")
                    continue
                cite(n["fact"])
                sheet[kind] = round(n["value"], 2)
                sheet_facts.append(n["fact"])
                date = date or n.get("period")
        # an aggregator's "total debt" includes lease liabilities: borrowings only, where both are known
        dn = nums.get("debt")
        if dn and "leases" in sheet and "stockanalysis" in (facts.get(dn["fact"], {}).get("url") or ""):
            sheet["debt"] = round(max(0.0, sheet["debt"] - sheet["leases"]), 2)
        # commitments: the company's own promises (purchase, supply, capacity), never its customers' backlog
        promises = []
        for fid in (a.get("candidates") or {}).get("commitments", []):
            f = facts[fid]
            text = ((f.get("claim") or "") + " " + fid).lower()
            if not ok(fid) or re.search(r"performance obligation|\brpo\b|backlog|remaining performance", text):
                continue
            v = usd_bn(f["value"], f["unit"])
            if v:
                promises.append((v, fid, f.get("claim") or ""))
        if promises:
            v, fid, claim = max(promises)
            sheet["commitments"] = round(v, 1)
            sheet["commitmentsWhat"] = claim[:140]
            sheet_facts.append(cite(fid))
        if sheet:
            sheet["date"] = date
            sheet["facts"] = sheet_facts
            rec["balance"] = sheet
        pe = nums.get("peRatio")
        if pe and cite(pe["fact"]):
            rec["pe"] = round(pe["value"], 1)
            if pe.get("period"):
                rec["pePeriod"] = pe["period"]
        for kind in ("buybacks", "dividends", "sbc"):
            n = nums.get(kind)
            if n and cite(n["fact"]):
                rec[kind] = round(n["value"], 2)
                rec[kind + "Period"] = n.get("period")
                if re.search(r"returned|return of capital|buybacks and dividends", (facts[n["fact"]].get("claim") or "").lower()):
                    rec[kind + "Returned"] = True   # a shareholder-return total, not buybacks alone
        losses = [fid for k in ("burn", "segmentLoss") for fid in (a.get("candidates") or {}).get(k, []) if ok(fid)]
        losses = [fid for fid in losses if not re.search(NOT_YEARLY, (facts[fid].get("period") or "").lower())]
        if losses:
            fid = max(losses, key=lambda x: year_of(facts[x].get("period")))
            f = facts[fid]
            v = usd_bn(f["value"], f["unit"])
            if v:
                rec["burn"] = round(abs(v), 2)
                rec["burnPeriod"] = f.get("period")
                rec["burnYearly"] = round(abs(per_year(v, f.get("period"))), 2)
                rec["burnWhat"] = (f.get("claim") or "")[:120]
                cite(fid)
        rec["private"] = bool(size and size["kind"] == "valuation")
        # what it bought (the share price and the cash at work) and the data-centre power it builds, each with its period
        bought = sorted(((fid, f) for fid, f in facts.items() if f.get("kind") == "acquisition" and f.get("from") == a["id"] and f.get("status") == "verified"
                         and not re.search(r"combined|merged entity|entity valued", (f.get("claim") or "").lower())),
                        key=lambda x: -(number(x[1].get("value")) or 0))[:5]
        if bought:
            rec["acquisitions"] = [{"target": ("licence + hire: " if re.search(r"licen[cs]|hire", (f.get("claim") or "").lower()) else "") +
                                              next((x.get("name") for x in model["actors"] if x["id"] == f.get("to")), f.get("to")),
                                    "usdBn": number(f.get("value")), "period": f.get("period"), "claim": (f.get("claim") or "")[:140], "fact": cite(fid)}
                                   for fid, f in bought]
        power = [(fid, f) for fid, f in facts.items() if f.get("kind") == "capacity" and f.get("actor") == a["id"] and f.get("status") == "verified"]
        if power:
            rec["capacity"] = [{"value": number(f.get("value")), "unit": f.get("unit"), "period": f.get("period"), "fact": cite(fid)} for fid, f in power[:4]]
        js = (judg.get("strategy") or {}).get(a["id"]) or {}
        if js.get("strategy"):
            rec["strategy"] = STRATEGY_NAMES.get(js["strategy"]["choice"], js["strategy"]["choice"])
        if js.get("risk"):
            rec["risk"] = round(js["risk"]["score"], 2)
        actors.append(rec)
    merged = {}
    if any(a["id"] == "xai" for a in actors) and any(a["id"] == "spacex" for a in actors):
        merged["xai"] = "spacex"
        actors = [a for a in actors if a["id"] != "xai"]
    # venture capital is an owner (a golden circle), not a company
    vc = next((a for a in actors if a["id"] == "venture-capital"), None)
    if vc:
        actors = [a for a in actors if a["id"] != "venture-capital"]
    ids = {a["id"] for a in actors}

    owner_ids = {a["id"] for a in model["actors"] if a.get("layer") == "owners"} | ({"venture-capital"} if vc else set())
    flows = []
    raw_flows = list(model.get("flows", []))
    # a verified contract with a yearly amount is a river too (contracted), e.g. compute bought from a builder
    for fid, f in facts.items():
        if f.get("kind") == "contract" and f.get("status") == "verified" and f.get("from") and f.get("to") \
                and re.search(r"a year|per year|annual|/yr|each year", (f.get("claim") or "").lower()) and usd_bn(f["value"], f["unit"]):
            raw_flows.append({"id": "contract-" + fid, "from": f["from"], "to": f["to"], "usdBnPerYear": usd_bn(f["value"], f["unit"]),
                              "what": (f.get("claim") or "")[:90], "kind": "contract", "basis": "contracted", "period": f.get("period"), "fact": fid})
    for f0 in raw_flows:
        # a river is money a year: a one-off deal, a valuation, a cumulative total or an "up to" is not one
        text = " ".join(str(f0.get(k) or "") for k in ("what", "notes", "basis", "period")).lower()
        if re.search(r"one-off|one off|merged entity|valued at|acquisition price|\ba total\b|cumulative|up to|tranche", text):
            continue
        # wages and savings belong to people, who stand on the low ground as workers and consumers; xAI is SpaceX's
        f = dict(f0, **{k: "consumers" for k in ("from", "to") if f0.get(k) in ("households", "workers")})
        f = dict(f, **{k: merged[f[k]] for k in ("from", "to") if f.get(k) in merged})
        if f.get("from") == f.get("to"):
            continue
        ends = ids | owner_ids
        if f.get("from") not in ends or f.get("to") not in ends or (f.get("from") not in ids and f.get("to") not in ids) or not cite(f.get("fact")):
            continue
        m = (jflow.get(f["id"]) or {}).get("motive")
        kind = f.get("kind") or ""
        # yearly: spending, revenue, capex, wages, contracts at a yearly rate; a funding round is capital raised once
        yearly = kind not in ("investment", "financing", "equity") and not re.search(r"\bround\b|series [a-z]\b", text)
        flows.append({"id": f["id"], "from": f["from"], "to": f["to"], "usdBn": round(float(f["usdBnPerYear"]), 2),
                      "what": f.get("what"), "kind": kind, "basis": f.get("basis"), "period": f.get("period"), "yearly": yearly,
                      "fear": round(m["score"] / 4, 3) if m else None, "fact": f["fact"]})
    # one river per payer, payee and kind: the newest fact wins (rounds and restated contracts must not count twice)
    best = {}
    for fl in flows:
        key = (fl["from"], fl["to"], "capital" if not fl["yearly"] else "yearly")
        if key not in best or year_of(fl.get("period")) > year_of(best[key].get("period")):
            best[key] = fl
    dropped = [fl["id"] for fl in flows if best.get((fl["from"], fl["to"], "capital" if not fl["yearly"] else "yearly")) is not fl]
    if dropped:
        problems.append("duplicate rivers left out: " + ", ".join(dropped))
    flows = list(best.values())
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

    company_ids = {a["id"] for a in actors if a["flank"] != "state" and a["band"] != "buyers"}
    value = {a["id"]: (a["size"] or {}).get("usdBn") for a in actors}
    owners = {}
    for a in model["actors"]:
        if a.get("layer") == "owners" and a["id"] not in ids and a["id"] not in ("households", "top1", "next9", "next40", "bottom50", "foreign-holders"):
            kind = a.get("kind") or "fund"
            if re.search(r"blue owl|blackstone|\bkkr\b|apollo global|carlyle|brookfield|\btpg\b|silver lake", (a.get("name") or a["id"]).lower()):
                kind = "pe"   # private capital: private equity and private credit firms, whatever the cluster called them
            owners[a["id"]] = {"id": a["id"], "name": a.get("name") or a["id"], "kind": kind, "stakes": []}
    if vc:
        owners["venture-capital"] = {"id": "venture-capital", "name": "Venture capital funds", "kind": "vc", "stakes": []}
    stakes_seen = set()
    for fid, f in facts.items():
        if f.get("kind") not in ("stake", "investment") or f.get("status") != "verified" or "voting" in (f.get("unit") or ""):
            continue
        if re.search(r"\bcap(ped)?\b|up to|at most|maximum", (f.get("claim") or "").lower()):
            continue   # a ceiling on a stake is not a holding
        if f.get("kind") == "investment" and f.get("from") not in owners and f.get("from") != "venture-capital":
            continue   # a company's investment in another is a river or a stake fact of its own
        f = dict(f, **{k: merged[f[k]] for k in ("from", "to") if f.get(k) in merged})
        if re.search(r"joint venture|\bjv\b|vehicle|campus|data cent|hyperion|el paso", (f.get("claim") or "").lower()):
            continue   # a stake in a joint venture or a financing vehicle, not in the company itself
        holder, company = f.get("from"), f.get("to")
        if company not in company_ids or (holder not in owners and holder not in company_ids) or (holder, company) in stakes_seen:
            continue
        stakes_seen.add((holder, company))
        unit = (f.get("unit") or "").lower()
        st = {"company": company, "fact": cite(fid), "period": f.get("period")}
        if "%" in unit:
            st["pct"] = number(f["value"])
        else:
            st["usdBn"] = usd_bn(f["value"], f["unit"])
        if holder not in owners:     # a company holding a stake in another: it owns as an organism, not as a circle
            owners[holder] = {"id": holder, "name": next((x["name"] for x in actors if x["id"] == holder), holder), "kind": "corporate", "stakes": []}
        owners[holder]["stakes"].append(st)
    # private capital financing the build-out through a venture with a company (Blue Owl's cash into Meta's Hyperion
    # campus): a holding in the venture, drawn as a thread to the company and labelled as the venture, never as its shares
    for fid, f in facts.items():
        if f.get("kind") != "financing" or f.get("status") != "verified" or "usd" not in (f.get("unit") or "").lower():
            continue
        holder, company = f.get("from"), merged.get(f.get("to"), f.get("to"))
        if holder not in owners or company not in company_ids or (holder, company) in stakes_seen:
            continue
        m = re.search(r"\b([A-Z][\w-]+) (?:joint )?venture", f.get("claim") or "")
        what = (m.group(1) + " venture" if m else "a venture") + " with " + next((x["name"] for x in actors if x["id"] == company), company)
        stakes_seen.add((holder, company))
        owners[holder]["stakes"].append({"company": company, "fact": cite(fid), "usdBn": usd_bn(f["value"], f["unit"]),
                                         "period": f"{f.get('period')}; in the {what}, not its shares", "venture": True})
    for o in owners.values():
        held = 0.0
        for st in o["stakes"]:
            if st.get("pct") and value.get(st["company"]):
                held += value[st["company"]] * st["pct"] / 100
            elif st.get("usdBn"):
                held += st["usdBn"]
        o["holdingsUsdBn"] = round(held, 1)
    # the households that own the shares, by wealth (Fed Distributional Financial Accounts, already in the app's
    # circuit.json: each group's share of households' corporate equities and mutual fund shares, and their total), and abroad
    circuit = load(os.path.join(ROOT, "Assets", "Resources", "Data", "economy", "circuit.json"), {})
    hist = circuit.get("groupEquityHistory") or []
    note = circuit.get("equityHoldersNote") or ""
    m = re.search(r"directly held equities & mutual funds \(\$([0-9.]+)T\)", note)
    if hist and m:
        year, shares = hist[-1]
        total = float(m.group(1)) * 1000
        names = {"top1": "Top 1%", "next9": "Next 9%", "next40": "Next 40%", "bottom50": "Bottom 50%"}
        for gid, gname in names.items():
            if gid in shares:
                owners["wealth-" + gid] = {"id": "wealth-" + gid, "name": gname, "kind": "households", "stakes": [],
                                           "holdingsUsdBn": round(shares[gid] * total, 0), "share": shares[gid], "year": year,
                                           "source": "Fed DFA via circuit.json (households' corporate equities and mutual fund shares)"}
    if circuit.get("foreignEquityShare") and circuit.get("corporateEquityTotal"):
        owners["abroad"] = {"id": "abroad", "name": "Abroad", "kind": "abroad", "stakes": [],
                            "holdingsUsdBn": round(circuit["foreignEquityShare"] * circuit["corporateEquityTotal"], 0),
                            "source": "Fed Z.1 L.224 via circuit.json (rest of world's US corporate equities)"}
    owner_list = [o for o in owners.values() if (o["stakes"] or o["kind"] in ("households", "abroad")) and o.get("holdingsUsdBn", 0) > 0]

    # the individual level: what US companies pay their owners (buybacks and dividends, S&P 500, verified) rising to the
    # golden circles by the Fed's shares of who owns the stocks (an estimate, labelled so); wages already run to the people
    payouts = [nums_by[k] for k in ("buybacks", "dividends") for nums_by in [next((x.get("numbers") or {} for x in model["actors"] if x["id"] == "enterprises"), {})] if nums_by.get(k)]
    total = sum(n["value"] for n in payouts if ok(n["fact"]))
    if total > 0 and "enterprises" in ids:
        abroad_share = circuit.get("foreignEquityShare") or 0
        for o in owner_list:
            share = (1 - abroad_share) * o.get("share", 0) if o["id"].startswith("wealth-") else abroad_share if o["id"] == "abroad" else 0
            if share > 0:
                flows.append({"id": "payout-" + o["id"], "from": "enterprises", "to": o["id"], "usdBn": round(total * share, 1),
                              "what": "buybacks and dividends paid by S&P 500 companies, by the Fed's shares of who owns US stocks",
                              "kind": "payout", "basis": "estimate", "period": payouts[0].get("period"), "yearly": True, "fear": None,
                              "fact": next(n["fact"] for n in payouts if ok(n["fact"]))})

    scenarios = [{"id": s["id"], "title": s.get("title"), "premise": s.get("premise"), "reading": s.get("reading"),
                  "friendly": s.get("friendly") or [], "unfriendly": s.get("unfriendly") or [],
                  "signposts": s.get("signposts") or []} for s in model.get("scenarios", [])]
    sources = {fid: {"claim": facts[fid]["claim"], "url": facts[fid]["url"]} for fid in sorted(used)}
    sim = simulation(actors, flows, owner_list, model.get("scenarios", []), problems)
    out = {"asOf": model.get("asOf"), "judge": judg.get("model"),
           "method": "Altitude: A = s1 + s2/2 + s3/4 of Jev's seven power scores (0-4), 0-7; scenarios and 2030 move it by "
                     "0.9 per step of Jev's 0-4 answer. Sizes and margins: verified facts only.",
           "bands": [{"id": b, "name": n} for b, n in BANDS],
           "powers": POWER_NAMES, "actors": actors, "flows": flows, "chokepoints": chokes, "flashpoints": flashes,
           "lineage": lineage, "scenarios": scenarios, "owners": owner_list, "simulation": sim, "sources": sources}
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, separators=(",", ":"))
        f.write("\n")
    judged = sum(1 for a in actors if a["judged"])
    print(f"owners: {len(owner_list)} golden circles, {sum(len(o['stakes']) for o in owner_list)} stakes; "
          f"balance sheets: {sum(1 for a in actors if a.get('balance'))}; P/E: {sum(1 for a in actors if 'pe' in a)}; "
          f"simulation: {len((sim or {}).get('worlds', {}))} worlds")
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

#!/usr/bin/env python3
"""Builds Assets/Resources/Data/government.json: the grey government block of the Histomap and the US economy scene.

What it makes (all shares are fractions of GDP / output; years are integers, negative = BCE):
  us          Federal receipts and outlays by source/function 1947-2026 (OMB Historical Tables, FY2027 budget) and
              state & local consumption expenditures and gross investment (BEA via FRED SLCE / GDP).
  civs        For every civilization in civilizations.json, a series of {revenue, military, social, other} over its
              whole span. Modern countries use published data (IMF Public Finances in Modern History, Global Military
              Spending Dataset / SIPRI via Our World in Data, Lindert / OECD social spending via OWID); everything
              else is Jev (TypeSafe System One) judgment mapped to shares by an explicit calibration fitted on the
              (civ, year) points where data exist plus cited premodern anchors (see CALIBRATION_ANCHORS).
  industries  Jev Scores for how heavily regulation binds each US industry, and how captured its regulators are.
  actors      Political figures (figures.json rulers, commanders, reformers; offices and years from Wikidata) and
              every US president 1945-2026 (Wikidata P39 = President of the United States).

Usage (from the repository root):
    python Tools/government/build.py            # downloads into Tools/government/.cache, then writes the JSON
    python Tools/government/build.py --offline  # fail instead of calling Jev for anything not yet cached

Every download and every Jev answer is cached under Tools/government/.cache (gitignored), so reruns are free and
deterministic. The Jev credential is found by Tools/jev.py and never printed.
"""
import datetime
import hashlib
import json
import math
import os
import re
import secrets
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import zipfile
import xml.etree.ElementTree as ET
from concurrent.futures import ThreadPoolExecutor

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
DATA = os.path.join(ROOT, "Assets", "Resources", "Data")
OUT = os.path.join(DATA, "government.json")
CACHE = os.path.join(HERE, ".cache")
sys.path.insert(0, os.path.join(ROOT, "Tools"))
import jev  # noqa: E402  (Tools/jev.py: credential discovery and request validation)

AGENT = "why-2-government-layer/0.1 (https://github.com/atonalfreerider/why-2)"
OFFLINE = "--offline" in sys.argv
THIS_YEAR = 2026
CAP = 0.8

OMB = "https://www.whitehouse.gov/wp-content/uploads/2026/04/{}_fy2027.xlsx"
FRED = "https://fred.stlouisfed.org/graph/fredgraph.csv?id={}"
OWID = "https://ourworldindata.org/grapher/{}.csv?v=1&csvType=full&useColumnShortNames=false"
IMF = "https://www.imf.org/external/datamapper/api/v1/{}"
WD_API = "https://www.wikidata.org/w/api.php"
WD_SPARQL = "https://query.wikidata.org/sparql"

SOURCES = [
    "OMB Historical Tables, FY2027 Budget (April 2026): Table 1.2 receipts/outlays % of GDP, Table 2.3 receipts by source % of GDP, Table 3.1 outlays by function ($ millions, divided by Table 1.2 fiscal-year GDP; FY2026 GDP from Table 10.1) - https://www.whitehouse.gov/omb/budget/historical-tables/",
    "BEA state & local government consumption expenditures and gross investment (SLCE) and GDP, quarterly SAAR, via FRED - https://fred.stlouisfed.org/series/SLCE , https://fred.stlouisfed.org/series/GDP",
    "IMF Public Finances in Modern History database (Dec 2025): government revenue (rev) and expenditure (exp), % of GDP, 1800-2024 - https://www.imf.org/external/datamapper/datasets/FPP",
    "Barnum et al., Global Military Spending Dataset v9 (1816-2019), via Our World in Data - https://ourworldindata.org/grapher/military-spending-as-a-share-of-gdp-gmsd",
    "SIPRI Military Expenditure Database (1949-2025), via Our World in Data - https://ourworldindata.org/grapher/military-spending-as-a-share-of-gdp-sipri",
    "Lindert (2004) Growing Public, OECD (1985) and OECD SOCX: public social expenditure % of GDP 1880-2024, via Our World in Data - https://ourworldindata.org/grapher/social-spending-oecd-longrun",
    "Scheidel & Friesen (2009), The Size of the Economy and the Distribution of Income in the Roman Empire, JRS 99: state expenditure ~HS900m on GDP ~HS17-19bn, ~5% of GDP, 'the same as for France in 1700' (as quoted at https://marginalrevolution.com/marginalrevolution/2011/12/roman-empire-more-equal-than-the-united-states.html) - https://doi.org/10.3815/007543509789745223",
    "Potter (2015), Measuring the power of the Roman Empire (Brill, CC BY-NC): army pay ~58% of the Augustan budget, ~71% with retirement bonuses; Elizabethan England 73-80% military; Goldsmith's Roman revenue estimate 3% of GDP - https://brewminate.com/manpower-and-revenue-measuring-the-power-of-the-roman-empire/",
    "Pomeranz (2025), The Puzzle of the Qing, New York Review of Books: Qing revenue c.1780 'perhaps 3-4 percent' of GDP, France ~9%, Britain 12% (18% in the Napoleonic Wars) - https://www.nybooks.com/articles/2025/06/26/the-puzzle-of-the-qing-dynasty-china/",
    "Broadberry, Guan & Li (2021), China, Europe, and the Great Divergence: A Restatement, JEH 81(3): government services (soldiers and civil servants) peak at 8.6% of GDP at the start of the Ming - https://doi.org/10.1017/S0022050721000292",
    "Wallis (2000), American government finance in the long run: 1790 to 1990 (national, state and local revenue, % of national income), via Our World in Data - https://ourworldindata.org/grapher/government-revenues-national-income",
    "Wikidata (P39 position held with P580/P582 qualifiers; Q11696 President of the United States) - https://www.wikidata.org",
    "Jev (TypeSafe System One) judgments, https://docs.typesafe.ai - questions recorded under 'judgments'",
]

# Modern countries that have data. window = first year the country's data may describe the civ (spec: China = PRC
# from 1949, India from 1947, Germany from the 1871 Reich); imf = IMF PFMH code; owid = OWID entity name.
DATA_CIVS = {
    "united_states": {"imf": "USA", "owid": "United States", "window": 1816},
    "britain": {"imf": "GBR", "owid": "United Kingdom", "window": 1816},
    "france": {"imf": "FRA", "owid": "France", "window": 1816},
    "holy_roman_empire": {"imf": "DEU", "owid": "Germany", "window": 1871},
    "spain": {"imf": "ESP", "owid": "Spain", "window": 1816},
    "denmark": {"imf": "DNK", "owid": "Denmark", "window": 1816},
    "russia": {"imf": "RUS", "owid": "Russia", "window": 1816},
    "japan": {"imf": "JPN", "owid": "Japan", "window": 1860},
    "chinese": {"imf": "CHN", "owid": "China", "window": 1949},
    "indians": {"imf": "IND", "owid": "India", "window": 1947},
}

# Cited premodern anchors: (civ, year, measure, value, source). Each is paired with the Jev judgment of that civ in
# that year and enters the calibration fit beside the data points (with ANCHOR_WEIGHT).
CALIBRATION_ANCHORS = [
    ("romans", 150, "revenue", 0.05, "Scheidel & Friesen 2009: Roman state spending ~5% of GDP"),
    ("france", 1700, "revenue", 0.05, "Scheidel & Friesen 2009: ~5%, 'the same as for France in 1700'"),
    ("france", 1780, "revenue", 0.09, "Pomeranz 2025: France c.1780 ~9% of GDP"),
    ("britain", 1780, "revenue", 0.12, "Pomeranz 2025: Britain c.1780 taxes 12% of GDP"),
    ("britain", 1810, "revenue", 0.18, "Pomeranz 2025: Britain 18% in the Napoleonic Wars"),
    ("chinese", 1780, "revenue", 0.035, "Pomeranz 2025: Qing c.1780 3-4% of GDP"),
    ("chinese", 1400, "revenue", 0.086, "Broadberry, Guan & Li 2021: government services 8.6% of GDP, early Ming peak"),
    ("romans", 150, "military", 0.65, "Potter 2015: army ~58-71% of the Roman budget"),
    ("britain", 1590, "military", 0.765, "Potter 2015: Elizabethan England 73-80% military"),
]
# Coverage breaks in the IMF series, found as one-year jumps in revenue AND expenditure that persist (central
# government before, general government after). The earlier years are scaled up by the break ratio (mean of the 3
# years after / 3 years before), fading back to 1 over SPLICE_FADE years; spliced values are marked 'blend'.
IMF_SPLICES = {"FRA": 1978, "DEU": 1970, "DNK": 1971, "ESP": 1995, "IND": 1988}
SPLICE_FADE = 30
IMF_OUTLIERS = {"DEU": [1913]}  # 37.7% between 17% (1912) and the 1925 series: a lone different coverage
# US revenue before 1960 is federal only in the IMF series; Wallis (2000) gives national + state + local revenue
# (% of national income, benchmark years 1902-1992), rescaled to the IMF level over their 1962-1992 overlap.
US_WALLIS_BEFORE = 1960
ANCHOR_WEIGHT = 10.0  # each cited anchor summarizes an era-wide literature: worth ten data-years
CALIBRATION_YEARS = [1850, 1880, 1913, 1938, 1960, 1990, 2020]

# ----------------------------------------------------------------------------------------------------------- questions

CIV_QUESTIONS = {
    "reach": {
        "type": "score",
        "instructions": "In `year`, how much of the economy's output did the government of `civilization.name` "
                        "(in `civilization.region`) take in and command through taxes, tribute, rents, monopolies and "
                        "labor dues? Judge the ruling state or states of this people in that year, using "
                        "`civilization.about` and `peopleAliveThen`.",
        "criteria": [
            "No regular taxation: a chieftain or war leader lives on tribute, plunder, gifts and personal dues from followers",
            "Rulers live mainly from their own estates, booty and occasional tribute from subject peoples, with little regular tax",
            "A palace, temple or loose kingdom takes a customary share of some harvests and labor service, but much of the land and trade lies beyond its reach",
            "An agrarian kingdom or empire with a regular land or poll tax, but collection is thin, farmed out or skimmed by local lords, so little reaches the center",
            "A strong centralized pre-industrial state: salaried officials collect land, poll and trade taxes across its territory and pay a standing army",
            "An early modern fiscal-military state with permanent excise and customs duties, public debt and a large standing army or navy",
            "An industrializing nation-state with income or property taxes and a professional civil service, but few social programs outside wartime",
            "A modern bureaucratic state taxing incomes, payrolls and consumption, collecting a third or more of output and paying for pensions and health care",
            "A total-war or central-planning state that commands most of the nation's output directly",
        ],
    },
    "military": {
        "type": "score",
        "instructions": "In `year`, how much of what the government of `civilization.name` spent went to armies, fleets, "
                        "fortifications and war, compared with its court, administration, debt, public works, "
                        "religion and welfare?",
        "criteria": [
            "Almost none: the state keeps no forces of its own beyond a few guards; its spending goes to the court, temples or civilian services",
            "A small slice: large civilian budgets for pensions, health, schools and administration dwarf defense",
            "Defense is one major item among several, alongside civil administration, debt service, public works or welfare",
            "Defense is the largest single item, but the court, administration, temples or public building take much of the rest",
            "Armies and war take most of what the state collects; the court, administration and public works share what is left",
            "Nearly everything collected goes to warriors and war: the state is a war machine or fighting a total war",
        ],
    },
    "social": {
        "type": "score",
        "instructions": "In `year`, what did the government of `civilization.name` provide for ordinary people's "
                        "livelihoods: relief for the poor, the old, the sick and the hungry?",
        "criteria": [
            "None beyond the ruler's household and retainers; the poor depend on kin and private charity",
            "Occasional famine relief and alms given by rulers, temples or churches, with no regular public program",
            "Regular public granaries, grain doles or temple redistribution for a capital city or for famine years",
            "Organized poor relief funded by local taxes or church tithes in many localities",
            "First social insurance laws: accident, sickness or old-age insurance for some workers, and public schooling",
            "A broad welfare state: pensions, public schooling and health insurance cover most people",
            "Universal pensions, health care and unemployment insurance, with cash transfers the largest part of state spending",
        ],
    },
    "regulation": {
        "type": "score",
        "instructions": "In `year`, how much did the government of `civilization.name` regulate economic life: prices, "
                        "wages, trades, markets, money and who may produce what?",
        "criteria": [
            "Economic life is governed by custom and kin; rulers set no prices, wages, standards or trades",
            "Rulers fix weights, measures, coinage and some tolls, but otherwise leave farming and trade alone",
            "Law codes regulate contracts, debts, wages or prices in some trades, and the state monopolizes a few goods such as salt or metals",
            "Guilds, licences, state monopolies and price controls govern many trades, enforced unevenly",
            "Extensive mercantilist or bureaucratic control: state workshops, trade monopolies, export bans and detailed rules on crafts and commerce",
            "A modern regulatory state: licensing, safety, labor, environmental and financial rules across most industries",
            "Central planning: the state owns most enterprises and sets output, prices and wages",
        ],
    },
}

INDUSTRY_QUESTIONS = {
    "regulation": {
        "type": "score",
        "instructions": "Today, how heavily do federal and state rules constrain how the `industry.name` industry in the "
                        "United States operates (licensing, safety, environmental, financial, pricing and conduct rules)? "
                        "`industry.about` describes it.",
        "criteria": [
            "Almost no industry-specific rules: anyone can start, and only general business law applies",
            "Light rules: registration, consumer-protection and workplace-safety law, but no licence is needed to operate",
            "Moderate: specific safety, labeling or environmental standards with periodic inspections, but entry is open",
            "Heavy: operating licences or permits, frequent inspections and detailed product, environmental or conduct rules shape daily operations",
            "Very heavy: entry needs government approval, prices or capital are regulated, and dedicated agencies supervise firms continuously",
            "Run by government itself: budgets, pay, prices and operations are set by statute, legislatures and agencies",
        ],
    },
    "capture": {
        "type": "score",
        "instructions": "How much does the `industry.name` industry in the United States shape the rules that bind it, "
                        "through lobbying, campaign money, revolving doors between its firms or unions and the regulators, "
                        "and rules that protect incumbents from new competitors? `industry.about` describes it.",
        "criteria": [
            "It has no organized influence; the rules are written without its input and it hardly lobbies",
            "It lobbies through trade associations like most industries, but regulators and courts routinely overrule it",
            "Significant lobbying and some revolving-door hiring win it narrow carve-outs and delays, but not the main rules",
            "Heavy lobbying and campaign giving; its former executives staff the regulators and it largely writes the technical rules that govern it",
            "The rules mainly serve incumbents: licensing, subsidies or tax breaks it secured keep out competitors, and its regulators rarely act against it",
        ],
    },
}

# A priori reading of each level (documented prior; the fit pulls these toward the data and anchors).
PRIOR = {
    # revenue as share of GDP
    "reach": [0.01, 0.02, 0.035, 0.05, 0.08, 0.10, 0.13, 0.38, 0.55],
    # military as share of state outlays
    "military": [0.05, 0.10, 0.25, 0.45, 0.65, 0.85],
    # social spending as share of GDP
    "social": [0.0005, 0.002, 0.005, 0.01, 0.03, 0.15, 0.24],
}
PRIOR_WEIGHT = 1.0      # ridge weight per level, in units of one observation
SMOOTH_WEIGHT = 0.5     # second-difference penalty between neighbouring levels
TRANSFORMS = {
    "reach": (lambda v: math.log(v), lambda u: math.exp(u)),
    "military": (lambda v: math.log(v / (1 - v)), lambda u: 1 / (1 + math.exp(-u))),
    "social": (lambda v: math.log(v + 0.002), lambda u: max(math.exp(u) - 0.002, 0.0)),
}

# ----------------------------------------------------------------------------------------------------------- downloads


def fetch(url, name, binary=False, headers=None):
    """Download once into .cache/<name>; later runs read the file."""
    path = os.path.join(CACHE, name)
    if not os.path.exists(path):
        if OFFLINE:
            raise SystemExit(f"--offline: {name} is not cached")
        os.makedirs(os.path.dirname(path), exist_ok=True)
        h = {"User-Agent": AGENT}
        h.update(headers or {})
        last = None
        for attempt in range(4):
            try:
                with urllib.request.urlopen(urllib.request.Request(url, headers=h), timeout=120) as r:
                    body = r.read()
                with open(path, "wb") as f:
                    f.write(body)
                break
            except Exception as e:  # noqa: BLE001
                last = e
                time.sleep(2 * (attempt + 1))
        else:
            raise RuntimeError(f"download failed: {url} ({type(last).__name__})")
    mode = "rb" if binary else "r"
    with open(path, mode, **({} if binary else {"encoding": "utf-8"})) as f:
        return f.read()


def read_xlsx(path):
    """First worksheet of an .xlsx as a list of {column letter: text} rows (stdlib only)."""
    ns = "{http://schemas.openxmlformats.org/spreadsheetml/2006/main}"
    z = zipfile.ZipFile(path)
    shared = []
    if "xl/sharedStrings.xml" in z.namelist():
        for si in ET.fromstring(z.read("xl/sharedStrings.xml")).findall(ns + "si"):
            shared.append("".join(t.text or "" for t in si.iter(ns + "t")))
    rows = []
    for row in ET.fromstring(z.read("xl/worksheets/sheet1.xml")).iter(ns + "row"):
        cells = {}
        for c in row.findall(ns + "c"):
            col = re.match(r"[A-Z]+", c.get("r")).group()
            v, t = c.find(ns + "v"), c.get("t")
            if t == "inlineStr":
                cells[col] = "".join(x.text or "" for x in c.iter(ns + "t"))
            elif v is not None:
                cells[col] = shared[int(v.text)] if t == "s" else v.text
        rows.append(cells)
    return rows


def num(s):
    if s is None:
        return None
    s = s.strip()
    if s in ("*", "-*"):
        return 0.0
    try:
        return float(s)
    except ValueError:
        return None


def omb(table):
    fetch(OMB.format(table), f"{table}_fy2027.xlsx", binary=True)
    return read_xlsx(os.path.join(CACHE, f"{table}_fy2027.xlsx"))


def fred_annual(series):
    text = fetch(FRED.format(series), f"fred_{series}.csv")
    by_year = {}
    for line in text.strip().splitlines()[1:]:
        d, v = line.split(",")
        if v and v != ".":
            by_year.setdefault(int(d[:4]), []).append(float(v))
    return by_year


def owid(slug):
    text = fetch(OWID.format(slug), f"owid_{slug}.csv")
    import csv
    import io
    rows = list(csv.reader(io.StringIO(text)))
    out = {}
    for r in rows[1:]:
        if len(r) > 3 and r[3] != "":
            out.setdefault(r[0], {})[int(r[2])] = float(r[3]) / 100.0
    return out


def owid_multi(slug):
    """Entity -> year -> {column: share} for multi-column grapher CSVs."""
    import csv
    import io
    text = fetch(OWID.format(slug), f"owid_{slug}.csv")
    rows = list(csv.reader(io.StringIO(text)))
    head, out = rows[0], {}
    for r in rows[1:]:
        for i in range(3, len(head)):
            if i < len(r) and r[i] != "":
                out.setdefault(r[0], {}).setdefault(int(r[2]), {})[head[i]] = float(r[i]) / 100.0
    return out


def imf(indicator):
    data = json.loads(fetch(IMF.format(indicator), f"imf_{indicator}.json"))
    return {c: {int(y): v / 100.0 for y, v in s.items() if v is not None} for c, s in data["values"][indicator].items()}

# ------------------------------------------------------------------------------------------------------------- US block


def build_us(circuit):
    t12 = omb("hist01z2")
    t23 = omb("hist02z3")
    t31 = omb("hist03z1")
    t101 = omb("hist10z1")

    def year_of(label):
        m = re.match(r"^(\d{4})( estimate)?$", (label or "").strip())
        return int(m.group(1)) if m else None

    gdp = {}
    for r in t12:
        y = year_of(r.get("A"))
        if y and num(r.get("B")):
            gdp[y] = num(r["B"]) * 1000.0  # $ billions -> $ millions
    # Table 10.1: GDP for fiscal years including estimates (column B, $ billions)
    gdp_est = {}
    for r in t101:
        y = year_of(r.get("A"))
        if y and num(r.get("B")):
            gdp_est[y] = num(r["B"]) * 1000.0
    for y, v in gdp_est.items():
        gdp.setdefault(y, v)

    rec = {}
    for r in t23:
        y = year_of(r.get("A"))
        if y:
            rec[y] = {k: num(r.get(c)) for k, c in
                      [("individual", "B"), ("corporate", "C"), ("payroll", "D"), ("excise", "G"), ("other", "H"), ("total", "I")]}

    header = next(r for r in t31 if r.get("A", "").startswith("Superfunction"))
    col_year = {c: year_of(v) for c, v in header.items() if c != "A" and year_of(v)}
    fn = {}
    in_dollars = False
    for r in t31:
        a = (r.get("A") or "").strip()
        if a.startswith("In millions"):
            in_dollars = True
            continue
        if a.startswith("As percentages"):
            break
        if in_dollars and a and a not in fn:  # first occurrence (the (On-budget) sub-rows repeat)
            fn[a] = {y: num(r.get(c)) for c, y in col_year.items()}

    def f(name, y):
        v = fn[name].get(y)
        return (v or 0.0) / gdp[y]

    rows = []
    for y in range(1947, THIS_YEAR + 1):
        if y not in rec or y not in gdp or y not in fn["Total, Federal outlays"]:
            raise SystemExit(f"US year {y} missing in OMB tables")
        rc = rec[y]
        total_out = f("Total, Federal outlays", y)
        row = {
            "year": y,
            "receipts": rc["total"] / 100,
            "individual": rc["individual"] / 100,
            "payroll": rc["payroll"] / 100,
            "corporate": rc["corporate"] / 100,
            "otherReceipts": (rc["excise"] + rc["other"]) / 100,
            "defense": f("National Defense", y),
            "socialSecurity": f("Social Security", y),
            "health": f("Health", y) + (f("Medicare", y) if fn["Medicare"].get(y) else 0.0),
            "incomeSecurity": f("Income Security", y),
            "netInterest": f("Net interest", y),
        }
        row["otherOutlays"] = total_out - sum(row[k] for k in ("defense", "socialSecurity", "health", "incomeSecurity", "netInterest"))
        rows.append(row)

    slce, gdpq = fred_annual("SLCE"), fred_annual("GDP")
    for row in rows:
        y = row["year"]
        s, g = slce.get(y), gdpq.get(y)
        n = min(len(s or []), len(g or []))
        row["stateLocal"] = (sum(s[:n]) / n) / (sum(g[:n]) / n) if n else None
    for row in rows:
        for k, v in list(row.items()):
            if k != "year":
                row[k] = round(v, 5)

    gc, gdp25 = circuit["government"], circuit["income"]["gdp"]
    check = {
        "receipts": (gc["receipts"] / gdp25, rows[-2]["receipts"]),
        "defense": (gc["defense"] / gdp25, rows[-2]["defense"]),
        "socialSecurity": (gc["socialSecurity"] / gdp25, rows[-2]["socialSecurity"]),
        "netInterest": (gc["netInterest"] / gdp25, rows[-2]["netInterest"]),
        "stateLocal": (gc["stateLocalPurchases"] / gdp25, rows[-2]["stateLocal"]),
    }
    quarters26 = min(len(slce.get(THIS_YEAR, [])), len(gdpq.get(THIS_YEAR, [])))
    return rows, check, quarters26

# ----------------------------------------------------------------------------------------------------------------- Jev

TOKENS = {"run": 0, "cached": 0, "requests": 0, "cache_hits": 0}
MODEL = {"id": None}


def _post(state, questions):
    jev.validate({"state": state, "questions": questions})
    key, direct = jev._key()
    if key is None:
        raise RuntimeError("No Jev credential available (see Tools/jev.py)")
    body = {"model": "jev-latest" if direct else "typesafe-ai/jev", "state": state, "questions": questions}
    req = urllib.request.Request(jev.DIRECT_URL if direct else jev.GATEWAY_URL, data=json.dumps(body).encode("utf-8"),
                                 headers={"Authorization": f"Bearer {key}", "Content-Type": "application/json"},
                                 method="POST")
    with urllib.request.urlopen(req, timeout=120) as resp:
        return json.load(resp)


def ask(state, questions):
    """One Jev request, cached on disk by its exact content. Returns the full response (answers, model, usage)."""
    blob = json.dumps({"state": state, "questions": questions}, sort_keys=True, ensure_ascii=False)
    path = os.path.join(CACHE, "jev", hashlib.sha256(blob.encode("utf-8")).hexdigest() + ".json")
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            result = json.load(f)
        TOKENS["cache_hits"] += 1
        TOKENS["cached"] += sum((result.get("usage") or {}).values())
        return result
    if OFFLINE:
        raise SystemExit("--offline: a Jev answer is not cached")
    last = None
    for attempt in range(3):
        try:
            result = _post(state, questions)
            if result.get("answers"):
                os.makedirs(os.path.dirname(path), exist_ok=True)
                with open(path, "w", encoding="utf-8") as f:
                    json.dump(result, f)
                TOKENS["requests"] += 1
                TOKENS["run"] += sum((result.get("usage") or {}).values())
                return result
        except urllib.error.HTTPError as e:  # never print headers, the key or the server body
            last = f"HTTP {e.code}"
        except (urllib.error.URLError, TimeoutError, OSError) as e:
            last = type(e).__name__
        time.sleep(3 * (attempt + 1))
    raise RuntimeError(f"Jev request failed after 3 tries ({last})")


def ask_all(jobs):
    """jobs: list of (state, questions) -> list of responses, six at a time."""
    with ThreadPoolExecutor(max_workers=6) as pool:
        results = list(pool.map(lambda j: ask(*j), jobs))
    for r in results:
        if r.get("model"):
            MODEL["id"] = r["model"]
    return results


def usage_of(result):
    return sum((result.get("usage") or {}).values())

# --------------------------------------------------------------------------------------------------------- civ series


def year_text(y):
    return f"{-y} BCE" if y < 0 else (f"{y} CE" if y < 1000 else str(y))


def sample_years(start, end):
    """Every 25 years before 1800, every 10 to 1950, every 5 after; plus the civ's own first and last year."""
    ys = {start, end}
    for y in range(start, end + 1):
        if (y < 1800 and y % 25 == 0) or (1800 <= y < 1950 and y % 10 == 0) or (y >= 1950 and y % 5 == 0):
            ys.add(y)
    return sorted(ys)


def judged_years(civ_id, start, end, data_start):
    """Years Jev judges: each century mark, the ends, 25-year steps 1800-1950 and 10-year steps after 1950 where the
    civ has no data yet, calibration years where it has data, and the years of any anchor."""
    ys = {start, end}
    for y in range(start + 1, end):
        if y % 100 == 0:
            ys.add(y)
        if y >= 1800 and (data_start is None or y < data_start):
            if (y < 1950 and y % 25 == 0) or (y >= 1950 and y % 10 == 0):
                ys.add(y)
    if civ_id in DATA_CIVS:
        ys.update(y for y in CALIBRATION_YEARS if start <= y <= end and y >= DATA_CIVS[civ_id]["window"])
    ys.update(a[1] for a in CALIBRATION_ANCHORS if a[0] == civ_id)
    return sorted(ys)


def first_sentence(text, limit=150):
    s = re.split(r"(?<=[.!?])\s", text.strip())[0]
    return s if len(s) <= limit else s[:limit - 1].rsplit(" ", 1)[0] + "..."


def civ_state(civ, year, figures):
    alive = [f for f in figures if f["civ"] == civ["id"] and f["born"] <= year <= (f["died"] if f["died"] is not None else THIS_YEAR)]
    alive.sort(key=lambda f: (-(f.get("prominence") or 0), f["born"]))
    end = civ["endYear"]
    return {
        "civilization": {
            "name": civ["name"],
            "region": civ["region"],
            "about": civ["blurb"],
            "span": f"{year_text(civ['startYear'])} to {year_text(end) if end is not None else 'today'}",
        },
        "year": year_text(year),
        "peopleAliveThen": [f"{f['name']} ({f['role']}): {first_sentence(f['blurb'])}" for f in alive[:6]] or ["none recorded"],
    }


def solve(a, b):
    """Gaussian elimination with partial pivoting."""
    n = len(b)
    m = [row[:] + [b[i]] for i, row in enumerate(a)]
    for c in range(n):
        p = max(range(c, n), key=lambda r: abs(m[r][c]))
        m[c], m[p] = m[p], m[c]
        for r in range(n):
            if r != c and m[r][c]:
                k = m[r][c] / m[c][c]
                for j in range(c, n + 1):
                    m[r][j] -= k * m[c][j]
    return [m[i][n] / m[i][i] for i in range(n)]


def pav(values, weights):
    """Pool adjacent violators: the closest non-decreasing sequence."""
    blocks = [[v, w, 1] for v, w in zip(values, weights)]
    i = 0
    while i < len(blocks) - 1:
        if blocks[i][0] > blocks[i + 1][0]:
            v1, w1, n1 = blocks[i]
            v2, w2, n2 = blocks[i + 1]
            blocks[i] = [(v1 * w1 + v2 * w2) / (w1 + w2), w1 + w2, n1 + n2]
            del blocks[i + 1]
            i = max(i - 1, 0)
        else:
            i += 1
    out = []
    for v, _, n in blocks:
        out += [v] * n
    return out


def fit_levels(question, observations):
    """Level values for one Score question. observations: [(probabilities list, target value, weight)].
    Minimizes sum w (p.u - T(target))^2 + PRIOR_WEIGHT |u - T(prior)|^2 + SMOOTH_WEIGHT |second differences of u|^2
    in the transformed space T (log for revenue and social, logit for the military fraction), then forces the level
    values to rise with the level (pool adjacent violators)."""
    fwd, inv = TRANSFORMS[question]
    prior = [fwd(v) for v in PRIOR[question]]
    n = len(prior)
    a = [[0.0] * n for _ in range(n)]
    b = [0.0] * n
    for p, target, w in observations:
        t = fwd(target)
        for i in range(n):
            b[i] += w * p[i] * t
            for j in range(n):
                a[i][j] += w * p[i] * p[j]
    for i in range(n):
        a[i][i] += PRIOR_WEIGHT
        b[i] += PRIOR_WEIGHT * prior[i]
    for k in range(1, n - 1):
        d = {k - 1: 1.0, k: -2.0, k + 1: 1.0}
        for i, di in d.items():
            for j, dj in d.items():
                a[i][j] += SMOOTH_WEIGHT * di * dj
    u = solve(a, b)
    mass = [PRIOR_WEIGHT + sum(w * p[i] for p, _, w in observations) for i in range(n)]
    u = pav(u, mass)
    resid = [p_dot(p, u) - fwd(t) for p, t, _ in observations]
    rmse = math.sqrt(sum(r * r for r in resid) / len(resid)) if resid else 0.0
    return u, [inv(x) for x in u], rmse


def p_dot(p, u):
    return sum(pi * ui for pi, ui in zip(p, u))


def probs(answer, levels):
    pr = answer.get("probabilities") or {}
    v = [float(pr.get(str(k), 0.0)) for k in range(levels)]
    s = sum(v)
    if s <= 0:  # fall back to the score itself
        sc = float(answer.get("score", 0.0))
        v = [max(0.0, 1 - abs(sc - k)) for k in range(levels)]
        s = sum(v)
    return [x / s for x in v]


def interp(points, y):
    """Linear interpolation over sorted (year, value) points, flat beyond the ends."""
    if y <= points[0][0]:
        return points[0][1]
    if y >= points[-1][0]:
        return points[-1][1]
    for (y0, v0), (y1, v1) in zip(points, points[1:]):
        if y0 <= y <= y1:
            return v0 + (v1 - v0) * (y - y0) / (y1 - y0) if y1 > y0 else v1
    return points[-1][1]


def data_series(civ_id, imf_rev, imf_exp, gmsd, sipri, social, wallis, end):
    """Year -> measure -> value from the published series, inside the civ's data window. Gaps of up to 12 years
    (social: 30, it is published by decade before 1960) are interpolated; IMF segments contradicted by the military
    series (expenditure below military spending in a quarter or more of its years) are dropped."""
    spec = DATA_CIVS.get(civ_id)
    if not spec:
        return {}, [], set()
    lo, hi = spec["window"], end
    code, name = spec["imf"], spec["owid"]
    mil = {}
    for y, v in gmsd.get(name, {}).items():
        mil[y] = v
    for y, v in sipri.get(name, {}).items():  # SIPRI from 1949 where present (it continues to 2025)
        if y >= 1949:
            mil[y] = v
    rev, exp = dict(imf_rev.get(code, {})), dict(imf_exp.get(code, {}))
    dropped = []
    # contiguous IMF segments
    years = sorted(set(rev) & set(exp))
    segs, cur = [], []
    for y in years:
        if cur and y != cur[-1] + 1:
            segs.append(cur)
            cur = []
        cur.append(y)
    if cur:
        segs.append(cur)
    for seg in segs:
        bad = sum(1 for y in seg if y in mil and exp[y] < mil[y])
        if bad >= 0.25 * len(seg):
            dropped.append((seg[0], seg[-1]))
            for y in seg:
                rev.pop(y, None)
                exp.pop(y, None)
    adjusted = set()
    for y in IMF_OUTLIERS.get(code, []):
        rev.pop(y, None)
        exp.pop(y, None)
    if code in IMF_SPLICES:
        b = IMF_SPLICES[code]
        for series, measure in ((rev, "revenue"), (exp, "outlays")):
            after = [series[y] for y in range(b, b + 3) if y in series]
            before = [series[y] for y in range(b - 3, b) if y in series]
            ratio = (sum(after) / len(after)) / (sum(before) / len(before))
            for y in range(b - SPLICE_FADE, b):
                if y in series:
                    series[y] *= 1 + (ratio - 1) * (1 - (b - 1 - y) / SPLICE_FADE)
                    adjusted.add((y, measure))
    if code == "USA" and wallis:
        pts = sorted(wallis.items())
        overlap = [(y, v) for y, v in pts if y >= 1962 and y in rev]
        k = sum(rev[y] for y, _ in overlap) / sum(v for _, v in overlap)
        for y in range(pts[0][0], US_WALLIS_BEFORE):
            rev[y] = interp(pts, y) * k
            if y not in exp:
                adjusted.add((y, "revenue"))
    soc = social.get(name, {})

    def filled(series, max_gap):
        ys = sorted(y for y in series if lo <= y <= hi)
        out = {y: series[y] for y in ys}
        for y0, y1 in zip(ys, ys[1:]):
            if 1 < y1 - y0 <= max_gap:
                for y in range(y0 + 1, y1):
                    out[y] = series[y0] + (series[y1] - series[y0]) * (y - y0) / (y1 - y0)
        return out

    out = {}
    for measure, series, gap in (("revenue", rev, 12), ("outlays", exp, 12), ("military", mil, 12), ("social", soc, 30)):
        for y, v in filled(series, gap).items():
            out.setdefault(y, {})[measure] = v
    return out, dropped, adjusted


def build_civs(civs, figures, imf_rev, imf_exp, gmsd, sipri, social, wallis):
    # 1. data
    end_of = {c["id"]: (c["endYear"] if c["endYear"] is not None else THIS_YEAR) for c in civs}
    data, dropped, adjusted = {}, {}, {}
    for c in civs:
        data[c["id"]], dropped[c["id"]], adjusted[c["id"]] = data_series(
            c["id"], imf_rev, imf_exp, gmsd, sipri, social, wallis, end_of[c["id"]])

    # 2. judgments
    plan = []
    for c in civs:
        have = [y for y, d in data[c["id"]].items() if "revenue" in d]
        for y in judged_years(c["id"], c["startYear"], end_of[c["id"]], min(have) if have else None):
            plan.append((c, y))
    print(f"civ judgments: {len(plan)} (civ, year) requests x {len(CIV_QUESTIONS)} questions", flush=True)
    results = ask_all([(civ_state(c, y, figures), CIV_QUESTIONS) for c, y in plan])
    judged = {}
    for (c, y), r in zip(plan, results):
        judged.setdefault(c["id"], {})[y] = r["answers"]
    tokens = sum(usage_of(r) for r in results)

    # 3. calibration: pair judgments with data / anchors at the same (civ, year)
    levels = {q: len(CIV_QUESTIONS[q]["criteria"]) for q in CIV_QUESTIONS}
    obs = {"reach": [], "military": [], "social": []}
    for cid, by_year in judged.items():
        for y, ans in by_year.items():
            d = dict(data.get(cid, {}).get(y) or {})
            for m in list(d):
                if (y, m) in adjusted.get(cid, ()):
                    del d[m]  # spliced values are not used to calibrate
            if not d:
                continue
            if "revenue" in d:
                obs["reach"].append((probs(ans["reach"], levels["reach"]), max(d["revenue"], 0.005), 1.0))
            if "military" in d and "outlays" in d and d["outlays"] > d["military"]:
                obs["military"].append((probs(ans["military"], levels["military"]), min(max(d["military"] / d["outlays"], 0.01), 0.97), 1.0))
            if "social" in d:
                obs["social"].append((probs(ans["social"], levels["social"]), max(d["social"], 0.0), 1.0))
    n_data_obs = {q: len(v) for q, v in obs.items()}
    for cid, y, measure, value, _ in CALIBRATION_ANCHORS:
        ans = judged[cid][y]
        q = "reach" if measure == "revenue" else measure
        obs[q].append((probs(ans[q], levels[q]), value, ANCHOR_WEIGHT))
    calib = {}
    for q in obs:
        u, values, rmse = fit_levels(q, obs[q])
        calib[q] = {"u": u, "levels": values, "rmse": rmse, "dataPoints": n_data_obs[q],
                    "anchors": sum(1 for a in CALIBRATION_ANCHORS if (a[2] == "revenue" and q == "reach") or a[2] == q)}

    def mapped(ans, q):
        fwd, inv = TRANSFORMS[q]
        return inv(p_dot(probs(ans[q], levels[q]), calib[q]["u"]))

    # 4. per-civ judged curves (value at each judged year, interpolated between them)
    out, counts = {}, {"data": 0, "judgment": 0, "blend": 0}
    for c in civs:
        cid, end = c["id"], end_of[c["id"]]
        jy = sorted(judged[cid])
        jr = [(y, mapped(judged[cid][y], "reach")) for y in jy]
        jm = [(y, mapped(judged[cid][y], "military")) for y in jy]
        js = [(y, mapped(judged[cid][y], "social")) for y in jy]
        jreg = [(y, judged[cid][y]["regulation"]["score"] / (levels["regulation"] - 1)) for y in jy]
        jconf = [(y, sum(judged[cid][y][q].get("confidence", 0.0) for q in ("reach", "military", "social")) / 3) for y in jy]
        d = data.get(cid, {})
        adj = adjusted.get(cid, set())

        def judged_at(y):
            rev = interp(jr, y)
            mf = interp(jm, y)
            return {"revenue": rev, "outlays": rev, "military": mf * rev, "social": interp(js, y)}

        def blended(measure, y):
            """(value, from_data, bridged). Off the data, the judged value is scaled by the data/judgment ratio at
            the nearest data years: interpolated between two, fading out over 50 years beyond one."""
            jv = judged_at(y)[measure]
            if y in d and measure in d[y]:
                return d[y][measure], (y, measure) not in adj, (y, measure) in adj
            ys = sorted(yy for yy in d if measure in d[yy])
            if not ys:
                return jv, False, False
            before = [yy for yy in ys if yy < y]
            after = [yy for yy in ys if yy > y]

            def log_ratio(yy):
                return math.log(max(d[yy][measure], 1e-4) / max(judged_at(yy)[measure], 1e-4))
            if before and after and after[0] - before[-1] <= 50:  # short gap: interpolate the ratio
                y0, y1 = before[-1], after[0]
                r = log_ratio(y0) + (log_ratio(y1) - log_ratio(y0)) * (y - y0) / (y1 - y0)
                return jv * math.exp(r), False, True
            # beyond the data, or inside a long gap: each side's ratio fades out over 50 years
            parts = [(log_ratio(yy), 1 - abs(y - yy) / 50.0) for yy in (before[-1:] + after[:1])]
            parts = [(r, a) for r, a in parts if a > 0]
            if not parts:
                return jv, False, False
            r = sum(r * a for r, a in parts) / sum(a for _, a in parts) * max(a for _, a in parts)
            return jv * math.exp(r), False, True

        rows = []
        for y in sample_years(c["startYear"], end):
            vals, kinds = {}, []
            for m in ("revenue", "outlays", "military", "social"):
                v, is_data, bridged = blended(m, y)
                vals[m] = v
                kinds.append("data" if is_data else ("blend" if bridged else "judgment"))
            rev, outl, mil, soc = vals["revenue"], vals["outlays"], vals["military"], vals["social"]
            if kinds[1] != "data" and kinds[0] == "data":
                outl = rev  # outlays judged: follow the measured revenue
            if kinds[3] != "data":
                soc = min(soc, 0.9 * max(outl - mil, 0.0))  # judged social provision fits inside the civil budget
            other = outl - mil - soc
            if other < 0.03 * outl:
                # military + social exceed the outlays (central-government-only series, total war, or a judged
                # budget below measured military spending): raise outlays so administration keeps a 3% sliver
                outl = (mil + soc) / 0.97
                other = outl - mil - soc
            if all(k == "data" for k in kinds):
                basis = "data"
            elif all(k == "judgment" for k in kinds):
                basis = "judgment"
            else:
                basis = "blend"
            counts[basis] += 1
            jc = interp(jconf, y)
            share_data = sum(1 for k in kinds if k == "data") / 4
            conf = 0.95 * share_data + jc * (1 - share_data)
            row = {"year": y, "revenue": rev, "military": mil, "social": soc, "other": other,
                   "basis": basis, "confidence": conf, "regulation": interp(jreg, y)}
            for k in ("revenue", "military", "social", "other"):
                row[k] = round(min(max(row[k], 0.0), CAP), 5)
            row["confidence"] = round(row["confidence"], 3)
            row["regulation"] = round(row["regulation"], 3)
            rows.append(row)
        out[cid] = rows
    return out, counts, calib, tokens, dropped, len(plan)

# ---------------------------------------------------------------------------------------------------------- industries


def build_industries(industries):
    jobs = [({"industry": {"name": i["name"], "tier": i["tier"], "about": i["blurb"], "country": "United States"}},
             INDUSTRY_QUESTIONS) for i in industries]
    results = ask_all(jobs)
    out = {}
    for i, r in zip(industries, results):
        a = r["answers"]
        row = {}
        for q in ("regulation", "capture"):
            n = len(INDUSTRY_QUESTIONS[q]["criteria"])
            row[q] = round(a[q]["score"] / (n - 1), 4)
            row[q + "Confidence"] = round(a[q].get("confidence", 0.0), 3)
        out[i["id"]] = row
    return out, sum(usage_of(r) for r in results)

# --------------------------------------------------------------------------------------------------------------- actors


def wd_get(params):
    url = WD_API + "?" + urllib.parse.urlencode(params)
    name = "wikidata/" + hashlib.sha256(url.encode()).hexdigest() + ".json"
    return json.loads(fetch(url, name, headers={"Accept": "application/json"}))


def wd_sparql(query):
    url = WD_SPARQL + "?format=json&query=" + urllib.parse.quote(query)
    name = "wikidata/" + hashlib.sha256(url.encode()).hexdigest() + ".json"
    return json.loads(fetch(url, name, headers={"Accept": "application/sparql-results+json"}))


def wd_year(t):
    try:
        return int(t[:5]) if t[0] in "+-" else int(t[:4])
    except (ValueError, TypeError, IndexError):
        return None


def wd_entities(qids, props="labels|claims"):
    out = {}
    qids = sorted(set(qids))
    for i in range(0, len(qids), 50):
        out.update(wd_get({"action": "wbgetentities", "ids": "|".join(qids[i:i + 50]), "props": props,
                           "languages": "en", "format": "json"}).get("entities", {}))
    return out


def claim_year(e, prop):
    for c in e.get("claims", {}).get(prop, []):
        v = c["mainsnak"].get("datavalue", {}).get("value")
        if isinstance(v, dict) and "time" in v:
            return wd_year(v["time"])
    return None


OFFICE_KEYS = [
    ("head of government", r"prime minister|chancellor|premier|head of government|grand vizier|vizier"),
    ("party leader", r"general secretary|first secretary|chairman of the (central committee|communist)|party"),
    ("judiciary", r"justice|judge|court"),
    ("legislature", r"member of|senator|representative|deputy|parliament|congress|assembly|duma|bundestag|lok sabha"),
    ("military command", r"general|commander|marshal|admiral|chief of staff|army|generalissimo"),
    ("head of state", r"king|queen|emperor|empress|pharaoh|sultan|caliph|khan|khagan|tsar|czar|president|monarch|"
                      r"ruler|shah|regent|consul|dictator|shogun|chairman|pope|head of state|lord protector|duke|"
                      r"prince|chanyu|basileus|tyrant|archon|strategos|kaiser|f.hrer|augusta|augustus"),
]
ROLE_PREFERENCE = {
    "ruler": ["head of state", "head of government", "party leader", "military command", "legislature"],
    "military": ["military command", "head of state", "head of government"],
    "reformer": ["legislature", "judiciary", "head of government", "head of state", "party leader"],
}
ROLE_DEFAULT = {"ruler": "head of state", "military": "military command", "reformer": "reform movement"}


IGNORED_POSITIONS = (r"european|andorra|-elect|acting |crown prince|warden|leader of the (house|opposition)|exchequer|duchy|"
                     r"university|cis heads")


def office_of(label):
    low = (label or "").lower()
    if re.search(IGNORED_POSITIONS, low):
        return None
    for office, pattern in OFFICE_KEYS:
        if re.search(pattern, low):
            return office
    return None


def build_actors(figures):
    political = [f for f in figures if f["role"] in ("ruler", "military", "reformer")]
    # resolve each figure to a Wikidata human whose birth year matches
    cands = {}
    for f in political:
        res = wd_get({"action": "wbsearchentities", "search": f["name"].split(" (")[0], "language": "en", "uselang": "en",
                      "type": "item", "limit": 7, "format": "json"})
        cands[f["id"]] = [r["id"] for r in res.get("search", [])]
    ents = wd_entities([q for v in cands.values() for q in v])
    qid_of = {}
    for f in political:
        best = None
        for q in cands[f["id"]]:
            e = ents.get(q, {})
            kinds = [c["mainsnak"].get("datavalue", {}).get("value", {}).get("id") for c in e.get("claims", {}).get("P31", [])]
            if "Q5" not in kinds:
                continue
            b = claim_year(e, "P569")
            if b is None or abs(b - f["born"]) <= 12:
                best = q
                break
        qid_of[f["id"]] = best
    pos_qids = set()
    for q in qid_of.values():
        if q:
            for c in ents[q].get("claims", {}).get("P39", []):
                v = c["mainsnak"].get("datavalue", {}).get("value")
                if isinstance(v, dict):
                    pos_qids.add(v["id"])
    pos_labels = {k: e.get("labels", {}).get("en", {}).get("value", k) for k, e in wd_entities(pos_qids, "labels").items()}

    def qual_year(c, p):
        for qv in c.get("qualifiers", {}).get(p, []):
            v = qv.get("datavalue", {}).get("value")
            if isinstance(v, dict) and "time" in v:
                return wd_year(v["time"])
        return None

    actors, matched = [], 0
    for f in political:
        q = qid_of[f["id"]]
        positions = []
        if q:
            for c in ents[q].get("claims", {}).get("P39", []):
                v = c["mainsnak"].get("datavalue", {}).get("value")
                if not isinstance(v, dict):
                    continue
                start, stop = qual_year(c, "P580"), qual_year(c, "P582")
                label = pos_labels.get(v["id"], "")
                if start is None:
                    continue
                if f["born"] is not None and not (f["born"] + 5 <= start <= (f["died"] or THIS_YEAR) + 1):
                    continue
                positions.append((office_of(label), start, stop, label))
        chosen = None
        for office in ROLE_PREFERENCE[f["role"]]:
            hits = [p for p in positions if p[0] == office]
            if hits:
                chosen = (office, min(p[1] for p in hits),
                          None if any(p[2] is None for p in hits) and f["died"] is None else
                          max((p[2] if p[2] is not None else (f["died"] or p[1])) for p in hits),
                          "; ".join(sorted({p[3] for p in hits})))
                break
        if chosen:
            matched += 1
            office, a, b, label = chosen
            source = "wikidata"
        else:
            a = f["born"] + 20 if f["died"] is None else min(f["born"] + 20, f["died"])
            office, b, label = ROLE_DEFAULT[f["role"]], f["died"], None
            source = "lifespan"
        actors.append({"name": f["name"], "civ": f["civ"], "office": office, "from": a, "to": b, "figure": f["id"],
                       "position": label, "years": source, "_qid": q})

    # US presidents whose term touches 1945-2026
    res = wd_sparql("""SELECT ?p ?pLabel ?start ?end WHERE {
      ?p wdt:P31 wd:Q5 ; p:P39 ?st . ?st ps:P39 wd:Q11696 ; pq:P580 ?start . OPTIONAL { ?st pq:P582 ?end }
      SERVICE wikibase:label { bd:serviceParam wikibase:language "en". } }""")
    terms = {}
    for b in res["results"]["bindings"]:
        qid = b["p"]["value"].rsplit("/", 1)[-1]
        start = int(b["start"]["value"][:4])
        stop = int(b["end"]["value"][:4]) if "end" in b else None
        if stop is not None and stop < 1945 or start > THIS_YEAR:
            continue
        key = (qid, start)
        if key not in terms or (terms[key]["to"] is not None and stop is None):
            terms[key] = {"name": b["pLabel"]["value"], "qid": qid, "from": start, "to": stop}
    fig_by_qid = {a["_qid"]: a["figure"] for a in actors if a["_qid"]}
    president_qids = {t["qid"] for t in terms.values()}
    actors = [a for a in actors if a["_qid"] not in president_qids]
    for t in sorted(terms.values(), key=lambda t: t["from"]):
        actors.append({"name": t["name"], "civ": "united_states", "office": "head of state", "from": t["from"],
                       "to": t["to"], "figure": fig_by_qid.get(t["qid"]), "position": "President of the United States",
                       "years": "wikidata"})
    for a in actors:
        a.pop("_qid", None)
    actors.sort(key=lambda a: (a["from"], a["name"]))
    return actors, matched, len(political), len(terms)

# ----------------------------------------------------------------------------------------------------------------- main


def write_meta(path):
    meta = path + ".meta"
    if os.path.exists(meta):
        return
    with open(meta, "w", encoding="utf-8", newline="\n") as f:
        f.write("fileFormatVersion: 2\nguid: " + secrets.token_hex(16) + "\nTextScriptImporter:\n  externalObjects: {}\n"
                "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def main():
    os.makedirs(CACHE, exist_ok=True)
    if not jev.available() and not OFFLINE:
        print("Jev credential unavailable: only cached judgments can be used.", file=sys.stderr)
    load = lambda p: json.load(open(os.path.join(DATA, p), encoding="utf-8"))  # noqa: E731
    civs = load("civilizations.json")["civs"]
    figures = load("figures.json")["figures"]
    industries = load("economy/industries.json")["industries"]
    circuit = load("economy/circuit.json")

    us, check, q26 = build_us(circuit)
    imf_rev, imf_exp = imf("rev"), imf("exp")
    gmsd = owid("military-spending-as-a-share-of-gdp-gmsd")
    sipri = owid("military-spending-as-a-share-of-gdp-sipri")
    social = owid("social-spending-oecd-longrun")
    wallis_raw = owid_multi("government-revenues-national-income").get("United States", {})
    wallis = {y: sum(v.values()) for y, v in wallis_raw.items() if len(v) == 3}
    civ_rows, counts, calib, civ_tokens, dropped, n_civ_req = build_civs(civs, figures, imf_rev, imf_exp, gmsd, sipri,
                                                                         social, wallis)
    ind_rows, ind_tokens = build_industries(industries)
    actors, matched, n_political, n_terms = build_actors(figures)

    calib_note = "; ".join(
        f"{q}: levels -> " + ", ".join(f"{v:.3f}" for v in c["levels"]) +
        f" (fit on {c['dataPoints']} data points + {c['anchors']} anchors, rmse {c['rmse']:.2f} in "
        f"{'log' if q != 'military' else 'logit'} units)" for q, c in calib.items())
    dropped_note = "; ".join(f"{k} {a}-{b}" for k, v in dropped.items() for a, b in v)
    note = (
        "Fiscal size of the state as fractions of GDP/output. US block: OMB Historical Tables (FY2027 budget), federal "
        "FISCAL years; 1947-2025 actual, 2026 = OMB estimate published in the FY2027 budget. 'health' = Health + "
        "Medicare functions, 'incomeSecurity' = Income Security function, 'otherOutlays' = total outlays minus the "
        "named functions (incl. veterans, education, transport, offsetting receipts); 'otherReceipts' = excise + "
        "customs/other. 'stateLocal' = BEA state & local consumption expenditures and gross investment / GDP by "
        f"CALENDAR year (2026 = mean of the {q26} quarters published). "
        "CIV series: revenue = what the state collected; military + social + other = what it spent (outlays). Deficits "
        "ARE modeled where data exist (outlays may exceed revenue, e.g. world wars); judged samples assume a balanced "
        "budget (outlays = revenue). basis 'data' = IMF Public Finances in Modern History revenue/expenditure, GMSD "
        "(1816-1948) / SIPRI (1949-) military, Lindert/OECD social spending (decadal points before 1960 interpolated); "
        "'other' = expenditure - military - social. Data windows: US/Britain/France/Spain/Denmark/Russia (incl. USSR) "
        "from 1816, Germany (holy_roman_empire) from 1871, Japan from 1860, China = PRC from 1949, India from 1947. "
        "Where a component has no data (e.g. revenue before the IMF series begins, social spending for Russia/China/"
        "India) the Jev-judged value is scaled to meet the data (ratio interpolated across gaps, fading out over 50 "
        "years before the data begin): basis 'blend'. 'judgment' = Jev Scores (fiscal reach, military share of "
        "spending, social provision, regulation) judged per civ at century marks, ends, anchor years and denser steps "
        "after 1800, interpolated to the sample grid and mapped to shares by a calibration fitted in code: per-level "
        "values start from a documented prior reading of each level and are pulled by ridge least squares toward the "
        "(civ, year) points where data exist and toward cited premodern anchors (Rome ~5% of GDP with the army 58-71% "
        "of spending; France 1700 ~5%, 1780 ~9%; Britain 1780 12%, 1810 18%, Elizabethan military 73-80%; Qing 1780 "
        "3-4%; early Ming government services 8.6%), then forced monotone. " + calib_note + ". 'regulation' (0..1, "
        "extra field) is the judged regulation of economic life. IMF segments contradicted by the military series were "
        f"dropped as implausible: {dropped_note or 'none'}; Germany 1913 dropped as a lone coverage outlier. IMF "
        "coverage breaks (central -> general government: France 1978, Germany 1970, Denmark 1971, Spain 1995, India "
        "1988) are spliced by scaling the earlier 30 years by the break ratio, fading to 1 (basis 'blend'). US revenue "
        "1902-1959 = Wallis (2000) national + state + local revenue rescaled to the IMF level over 1962-1992 (the "
        "IMF US revenue before 1960 is federal only). Industries: Jev Scores, "
        "probability-weighted level / (levels - 1). Actors: figures.json rulers/commanders/reformers with Wikidata "
        "P39 offices and years where found (else born+20 to death; 'years' says which), plus every US president "
        "1945-2026 from Wikidata. Judgments are model evidence, not measurements; confidence = Jev confidence "
        "blended with 0.95 for data components.")

    model = MODEL["id"] or "jev-latest"
    total_tokens = TOKENS["run"] + TOKENS["cached"]
    out = {
        "note": note,
        "sources": SOURCES,
        "calibrationAnchors": [{"civ": a[0], "year": a[1], "measure": a[2], "value": a[3], "source": a[4]} for a in CALIBRATION_ANCHORS],
        "calibration": {q: [round(v, 4) for v in c["levels"]] for q, c in calib.items()},
        "judgments": {"model": model, "judged": datetime.date.today().isoformat(), "tokens": total_tokens,
                      "questions": {"civ": CIV_QUESTIONS, "industry": INDUSTRY_QUESTIONS}},
        "civs": civ_rows,
        "us": us,
        "industries": ind_rows,
        "actors": actors,
    }

    # ---- validation
    problems = []
    ids = {c["id"] for c in civs}
    for c in civs:
        rows = civ_rows.get(c["id"])
        end = c["endYear"] if c["endYear"] is not None else THIS_YEAR
        if not rows or rows[0]["year"] != c["startYear"] or rows[-1]["year"] != end:
            problems.append(f"civ {c['id']} does not cover {c['startYear']}..{end}")
        for r in rows or []:
            for k in ("revenue", "military", "social", "other"):
                if not 0 <= r[k] <= CAP:
                    problems.append(f"{c['id']} {r['year']} {k}={r[k]}")
    if len(us) != 80 or [r["year"] for r in us] != list(range(1947, 2027)):
        problems.append("us series is not 1947..2026 without gaps")
    for r in us:
        for k, v in r.items():
            if k != "year" and (v is None or not (-0.05 <= v <= CAP)):
                problems.append(f"us {r['year']} {k}={v}")
    for a in actors:
        if a["civ"] not in ids:
            problems.append(f"actor {a['name']} civ {a['civ']} unknown")
    if problems:
        print("VALIDATION FAILED:\n  " + "\n  ".join(problems[:40]), file=sys.stderr)
        sys.exit(1)

    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        json.dump(out, f, ensure_ascii=False, separators=(",", ":"))
    write_meta(OUT)

    # ---- summary
    by = {r["year"]: r for r in us}
    print(f"wrote {os.path.relpath(OUT, ROOT)} ({os.path.getsize(OUT) // 1024} KB)")
    print(f"us: {len(us)} rows; civs: {len(civ_rows)} series, {sum(len(v) for v in civ_rows.values())} samples "
          f"(data {counts['data']}, blend {counts['blend']}, judgment {counts['judgment']}); industries: {len(ind_rows)}; "
          f"actors: {len(actors)} ({matched}/{n_political} figures with Wikidata office years, {n_terms} presidential terms)")
    print(f"Jev: {n_civ_req} civ requests + {len(ind_rows)} industry requests; model {model}; tokens {total_tokens} "
          f"(this run {TOKENS['run']} over {TOKENS['requests']} new requests, {TOKENS['cache_hits']} from cache)")
    for q, c in calib.items():
        print(f"calibration {q}: " + ", ".join(f"{v:.3f}" for v in c["levels"]) + f"  rmse {c['rmse']:.2f}, {c['dataPoints']} data pts")
    for y in (1950, 1953, 1970, 2000, 2025, 2026):
        r = by[y]
        print(f"US {y}: receipts {r['receipts']:.3f}  defense {r['defense']:.3f}  SS {r['socialSecurity']:.3f}  "
              f"health {r['health']:.3f}  interest {r['netInterest']:.3f}  state&local {r['stateLocal']:.3f}")
    print("FY2025 vs circuit.json (OMB FY share / circuit $ over calendar GDP): " +
          ", ".join(f"{k} {b:.3f}/{a:.3f}" for k, (a, b) in check.items()))
    for cid in ("romans", "chinese", "britain", "united_states", "russia"):
        rows = civ_rows[cid]
        pick = [r for r in rows if r["year"] in (150, 1100, 1700, 1800, 1913, 1950, 1980, 2026)]
        print(f"  {cid}: " + "  ".join(f"{r['year']}:{r['revenue']:.2f}/{r['military']:.2f}/{r['social']:.2f}/{r['other']:.2f}({r['basis'][0]})" for r in pick))


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Builds Assets/Resources/Data/economy/regimes.json: other governments as model changes of the economy scene.

The economy scene stands the United States' hills on a grey government bedrock. The "regimes" view stands the same hills
on another state's foundation, drawn from that state's measured structure:

  spending, revenue  general government expenditure and revenue, % of GDP: IMF Fiscal Monitor (DataMapper API) for the
                     modern regimes; the Soviet state budget from the sourced research for the USSR
  revenueMix         what the state lives on, as shares of its revenue: the OECD Global Revenue Statistics Database's
                     categories (US, Sweden, China; fetched) or the budget's own lines (Saudi Arabia, the USSR), computed
                     here from the sourced inputs
  stateFirms         the public sector's share of the market value of listed companies (OECD Corporate Governance
                     Factbook 2025; the USSR's largest firms were the state's by law), and the state-controlled share of
                     the country's firms in the Fortune Global 500 (fortune)
  ownership          the state's holdings among the country's largest firms and the largest private firms beside them,
                     each mapped to one of the scene's industries with owner, share, revenue and sources; the largest
                     firm named on an industry (by revenue, computed here) is its summit, steel by the state's share
  trade              exports + imports, % of GDP: World Bank WDI (NE.TRD.GNFS.ZS)
  top1Wealth/Income  the top 1%'s shares of net personal wealth and pre-tax national income: World Inequality Database
  democracy          the electoral democracy index: V-Dem (via Our World in Data)
  industries         how heavily the state's rules bind each industry and how much the industry shapes them (Jev Scores:
                     model judgments, not measurement; the US keeps the government file's own judgments)

Tools/regimes/sources.json holds the regimes and the research read by hand (every value with its source URL and the text
it was read from; null where nothing defensible was found). The API series (IMF, World Bank, OECD, OWID) and Jev answers
are cached under
Tools/regimes/.cache (gitignored), so reruns are free and deterministic. The Jev credential is found by Tools/jev.py and
never printed.

Usage (from the repository root):
    python3 Tools/regimes/build.py            # fetch what is not cached, ask Jev what is not cached, write the JSON
    python3 Tools/regimes/build.py --offline  # use only the cache
    python3 Tools/regimes/build.py --no-jev   # skip the industry judgments (regimes other than the US get none)
"""
import csv
import datetime
import hashlib
import io
import json
import os
import re
import sys
import time
import urllib.error
import urllib.request
from concurrent.futures import ThreadPoolExecutor

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
DATA = os.path.join(ROOT, "Assets", "Resources", "Data")
OUT = os.path.join(DATA, "economy", "regimes.json")
SRC = os.path.join(HERE, "sources.json")
CACHE = os.path.join(HERE, ".cache")
sys.path.insert(0, os.path.join(ROOT, "Tools"))
import jev  # noqa: E402  (Tools/jev.py: credential discovery and request validation)

OFFLINE = "--offline" in sys.argv
NO_JEV = "--no-jev" in sys.argv
AGENT = "why-2-regimes/0.1 (https://github.com/atonalfreerider/why-2)"

IMF = "https://www.imf.org/external/datamapper/api/v1/{}"
IMF_PAGE = "https://www.imf.org/external/datamapper/{}@FM/{}"
WB = "https://api.worldbank.org/v2/country/{}/indicator/NE.TRD.GNFS.ZS?format=json&date=1960:2026&per_page=1000"
WB_PAGE = "https://data.worldbank.org/indicator/NE.TRD.GNFS.ZS?locations={}"
OWID = "https://ourworldindata.org/grapher/{}.csv?v=1&csvType=full&useColumnShortNames=true"
OWID_META = "https://ourworldindata.org/grapher/{}.metadata.json?v=1&csvType=full&useColumnShortNames=true"
VDEM_SLUG = "electoral-democracy-index"
OECD_REV = ("https://sdmx.oecd.org/public/rest/data/OECD.CTP.TPS,DSD_REV_COMP_GLOBAL@DF_RSGLOBAL,2.1/"
            "{iso3}.TAX_REV.S13._T+T_1100+T_1200+T_1300+T_2000+T_3000+T_4000+T_5000+T_6000._T.XDC+PT_B1GQ.A"
            "?startPeriod={year}&endPeriod={year}&format=csv")
BROWSER = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130 Safari/537.36"
GOVERNMENT_OWNED = "Government owned 50% or more"
# OECD revenue categories -> the parts of a state's revenue (unallocable income tax 1300 and other taxes 6000 stay "other")
OECD_PARTS = {"T_1100": "personal", "T_1200": "corporate", "T_2000": "social", "T_3000": "social", "T_4000": "property",
              "T_5000": "goods", "T_1300": "other", "T_6000": "other"}

# The parts of a state's revenue, in drawing order (top of the bedrock down).
PARTS = ["personal", "corporate", "social", "goods", "property", "resources", "enterprises", "other"]

# ----------------------------------------------------------------------------------------------------------- downloads


def fetch(url, name, ua=AGENT):
    """Download once into .cache/<name>; later runs read the file."""
    path = os.path.join(CACHE, name)
    if not os.path.exists(path):
        if OFFLINE:
            raise SystemExit(f"--offline: {name} is not cached")
        os.makedirs(os.path.dirname(path), exist_ok=True)
        last = None
        for attempt in range(4):
            try:
                with urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": ua}), timeout=120) as r:
                    body = r.read()
                with open(path, "wb") as f:
                    f.write(body)
                break
            except Exception as e:  # noqa: BLE001
                last = e
                time.sleep(2 * (attempt + 1))
        else:
            raise RuntimeError(f"download failed: {url} ({type(last).__name__})")
    with open(path, encoding="utf-8") as f:
        return f.read()


def retrieved(name):
    """The date a cached download was made (the file's modification day)."""
    path = os.path.join(CACHE, name)
    return datetime.date.fromtimestamp(os.path.getmtime(path)).isoformat() if os.path.exists(path) else None


def imf_series(indicator):
    """{iso3: {year: percent}} of an IMF DataMapper indicator (the API returns every country)."""
    d = json.loads(fetch(IMF.format(indicator), f"imf_{indicator}.json"))
    return {c: {int(y): v for y, v in s.items() if v is not None} for c, s in d.get("values", {}).get(indicator, {}).items()}


def wb_trade(iso3s):
    """({iso3: {year: percent}}, {iso3: iso2}, last updated) of the World Bank's trade share of GDP."""
    d = json.loads(fetch(WB.format(";".join(iso3s)), "wb_trade_" + "_".join(iso3s) + ".json"))
    out, iso2 = {}, {}
    for row in d[1] or []:
        iso2[row["countryiso3code"]] = row["country"]["id"]
        if row.get("value") is not None:
            out.setdefault(row["countryiso3code"], {})[int(row["date"])] = row["value"]
    return out, iso2, d[0].get("lastupdated")


def oecd_revenue(iso3, year):
    """({OECD category: amount in national currency}, tax revenue / GDP, url) of general government (S13), from the OECD
    Global Revenue Statistics Database."""
    url = OECD_REV.format(iso3=iso3, year=year)
    rows = list(csv.DictReader(io.StringIO(fetch(url, f"oecd_rev_{iso3}_{year}.csv"))))
    amounts, tax_gdp = {}, None
    for row in rows:
        if row["UNIT_MEASURE"] == "XDC":
            amounts[row["STANDARD_REVENUE"]] = float(row["OBS_VALUE"])
        elif row["UNIT_MEASURE"] == "PT_B1GQ" and row["STANDARD_REVENUE"] == "_T":
            tax_gdp = float(row["OBS_VALUE"]) / 100
    return amounts, tax_gdp, url


def fortune_list(url):
    """The Fortune Global 500 rows (rank, name, profile slug, country, sector, industry, revenue in US$ millions) from the
    list page's own data; only the extract is cached."""
    path = os.path.join(CACHE, "fortune_global500.json")
    if not os.path.exists(path):
        if OFFLINE:
            raise SystemExit("--offline: the Fortune list is not cached")
        with urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": BROWSER}), timeout=120) as r:
            html = r.read().decode("utf-8", "replace")
        m = re.search(r'<script id="__NEXT_DATA__" type="application/json">(.*?)</script>', html, re.S)
        items = json.loads(m.group(1))["props"]["pageProps"]["franchiseSearch"]["items"]
        rows = [{"rank": i["rank"], "name": i["name"], "slug": i["slug"], "country": i["data"]["Country / Territory"],
                 "sector": i["data"]["Sector"], "industry": i["data"]["Industry"],
                 "revenue": float(i["data"]["Revenues ($M)"].replace("$", "").replace(",", ""))} for i in items]
        os.makedirs(CACHE, exist_ok=True)
        with open(path, "w", encoding="utf-8") as f:
            json.dump(rows, f)
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def fortune_owned(slug):
    """Whether a firm's Fortune Global 500 profile carries the footnote 'Government owned 50% or more.' (only that is
    cached), with the profile's URL and the footnote."""
    url = "https://fortune.com" + slug + "global500/"
    path = os.path.join(CACHE, "fortune", slug.strip("/").replace("/", "_") + ".json")
    if not os.path.exists(path):
        if OFFLINE:
            raise SystemExit(f"--offline: the Fortune profile {slug} is not cached")
        with urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": BROWSER}), timeout=120) as r:
            html = r.read().decode("utf-8", "replace")
        m = re.search(r"(Government owned 50% or more\.[^\"<]{0,160})", html)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8") as f:
            json.dump({"url": url, "governmentOwned": GOVERNMENT_OWNED in html, "footnote": m.group(1).strip() if m else None,
                       "profileFound": len(html) > 50000}, f)
        time.sleep(1)
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def owid(slug):
    text = fetch(OWID.format(slug), f"owid_{slug}.csv")
    meta = json.loads(fetch(OWID_META.format(slug), f"owid_{slug}.meta.json"))
    rows = list(csv.DictReader(io.StringIO(text)))
    return rows, meta

# ------------------------------------------------------------------------------------------------------------ helpers


def measure(value, year, source, url, note=None, **extra):
    m = {"value": None if value is None else round(float(value), 4), "year": year, "source": source, "url": url}
    if note:
        m["note"] = note
    m.update({k: v for k, v in extra.items() if v is not None})
    return m


def curated(entry):
    """A measure read by hand (sources.json): value, year, source, url, quote, note; None stays None. A measure given as
    two amounts read in their sources ("numerator" and "denominator", each {value, unit, source, url, quote}) gets its
    value computed here as their ratio, so no share is typed by hand."""
    if not entry:
        return None
    if entry.get("value") is None and entry.get("numerator") and entry.get("denominator"):
        n, d = entry["numerator"], entry["denominator"]
        if n.get("value") is not None and d.get("value"):
            entry = dict(entry, value=n["value"] / d["value"],
                         computed=f"{n['value']:,} / {d['value']:,} {d.get('unit', '')}".strip())
    keep = {k: entry.get(k) for k in ("value", "year", "unit", "definition", "source", "url", "quote", "note", "previous", "count", "of", "list",
                                      "modelled", "basis", "numerator", "denominator", "computed")}
    if isinstance(keep.get("value"), float):
        keep["value"] = round(keep["value"], 4)
    return {k: v for k, v in keep.items() if v is not None}


def revenue_mix(mix, iso3, revenue_gdp_of_year):
    """Shares of the state's whole revenue (summing to one) from the sourced inputs, and notes on how they were computed.

    basis "oecd": the OECD's general government tax revenue by category (fetched), as shares of tax revenue, scaled by tax
    revenue over total revenue (the OECD tax-to-GDP ratio over the IMF revenue-to-GDP ratio of the same year); named
    non-tax parts (resources, enterprises: shares of total revenue) are added, and the remainder is other revenue (fees,
    property income, grants and other non-tax revenue).
    basis "amounts": the budget's own lines in currency (each mapped to a part) over its reported total.
    basis "revenue": parts already given as shares of total revenue; the remainder is "other"."""
    if not mix:
        return None, ["no revenue mix"], None
    notes, inputs = [], None
    parts = {p: 0.0 for p in PARTS}
    if mix["basis"] == "oecd":
        amounts, tax_gdp, url = oecd_revenue(iso3, mix["year"])
        total = amounts.get("_T")
        rev = revenue_gdp_of_year(mix["year"])
        if not total or tax_gdp is None or rev is None:
            return None, ["OECD cells or the IMF revenue of the same year missing"], None
        tax_share = min(1.0, tax_gdp / rev)
        for code, part in OECD_PARTS.items():
            parts[part] += amounts.get(code, 0.0) / total * tax_share
        inputs = {"oecdCells": amounts, "taxGdp": round(tax_gdp, 5), "imfRevenueGdp": round(rev, 5), "url": url}
        notes.append(f"taxes are {tax_share:.1%} of revenue ({tax_gdp:.1%} of GDP, OECD, over revenue {rev:.1%} of GDP, IMF, {mix['year']}); "
                     "the rest is other revenue")
        for k, v in (mix.get("nonTax") or {}).items():
            if isinstance(v, (int, float)):
                parts[k] += v
    elif mix["basis"] == "amounts":
        total = mix["total"]
        summed = sum(line["amount"] for line in mix["lines"])
        if mix.get("remainder"):
            # the source prints some lines and the total; what they leave is the named remainder part
            if summed > total * 1.002:
                raise SystemExit(f"budget lines sum to {summed}, above the reported total {total}")
            parts[mix["remainder"]] += (total - summed) / total
            notes.append(f"{mix['remainder']} is the total less the printed lines ({total - summed:,.1f} {mix.get('unit', '')})")
        elif abs(summed - total) > 0.002 * total:
            raise SystemExit(f"budget lines sum to {summed}, not the reported total {total}")
        for line in mix["lines"]:
            if line["part"] not in PARTS:
                raise SystemExit(f"unknown revenue part {line['part']}")
            parts[line["part"]] += line["amount"] / total
        notes.insert(0, f"the budget's lines over its reported total ({total:,} {mix.get('unit', '')}, {mix['year']})")
    elif mix["basis"] == "revenue":
        for k, v in mix["parts"].items():
            if v is not None:
                if k not in PARTS:
                    raise SystemExit(f"unknown revenue part {k}")
                parts[k] = v
    else:
        raise SystemExit(f"unknown mix basis {mix['basis']}")
    rest = 1 - sum(parts.values())
    if rest < -0.02:
        raise SystemExit(f"revenue mix exceeds the whole revenue by {-rest:.3f}")
    parts["other"] += max(0.0, rest)
    s = sum(parts.values())
    return {k: round(v / s, 4) for k, v in parts.items()}, notes, inputs

# ----------------------------------------------------------------------------------------------------------------- Jev

TOKENS = {"run": 0, "cached": 0, "requests": 0, "answers": 0}
MODEL = {"id": None}

QUESTIONS = {
    "regulation": {
        "type": "score",
        "instructions": "`country.when`, how heavily did the state's rules constrain how the `industry.name` industry operated in "
                        "`country.name` (`country.form`): licensing, safety, environmental, financial, pricing, planning and conduct "
                        "rules? `industry.scope` says what the industry covers.",
        "criteria": [
            "Almost no industry-specific rules: anyone can start, and only general business law applies",
            "Light rules: registration, consumer-protection and workplace-safety law, but no licence is needed to operate",
            "Moderate: specific safety, labeling or environmental standards with periodic inspections, but entry is open",
            "Heavy: operating licences or permits, frequent inspections and detailed product, environmental or conduct rules shape daily operations",
            "Very heavy: entry needs government approval, prices or capital are regulated, and dedicated agencies supervise firms continuously",
            "Run by government itself: budgets, pay, prices and operations are set by statute, plans, ministries and agencies",
        ],
    },
    "capture": {
        "type": "score",
        "instructions": "`country.when`, how much did the `industry.name` industry in `country.name` (`country.form`) shape the rules "
                        "that bound it, through lobbying, campaign money, party, ministry or family connections, revolving doors "
                        "between its firms or unions and the regulators or planners, and rules that protect incumbents from new "
                        "competitors? `industry.scope` says what the industry covers.",
        "criteria": [
            "It has no organized influence; the rules are written without its input and it hardly lobbies",
            "It lobbies through trade associations like most industries, but regulators and courts routinely overrule it",
            "Significant lobbying and some revolving-door hiring win it narrow carve-outs and delays, but not the main rules",
            "Heavy lobbying and campaign giving; its former executives staff the regulators and it largely writes the technical rules that govern it",
            "The rules mainly serve incumbents: licensing, subsidies or tax breaks it secured keep out competitors, and its regulators rarely act against it",
        ],
    },
}


def ask(state, questions):
    """One Jev request, cached on disk by its exact content. Returns the full response (answers, model, usage)."""
    blob = json.dumps({"state": state, "questions": questions}, sort_keys=True, ensure_ascii=False)
    path = os.path.join(CACHE, "jev", hashlib.sha256(blob.encode("utf-8")).hexdigest() + ".json")
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            result = json.load(f)
        TOKENS["cached"] += sum((result.get("usage") or {}).values())
        TOKENS["answers"] += 1
        return result
    if OFFLINE:
        raise SystemExit("--offline: a Jev answer is not cached")
    last = None
    for attempt in range(3):
        try:
            result = jev.call(state, questions, timeout=120)
            os.makedirs(os.path.dirname(path), exist_ok=True)
            with open(path, "w", encoding="utf-8") as f:
                json.dump(result, f)
            TOKENS["requests"] += 1
            TOKENS["answers"] += 1
            TOKENS["run"] += sum((result.get("usage") or {}).values())
            return result
        except RuntimeError as e:  # jev.call never puts the key or the state in its message
            last = str(e)
        time.sleep(3 * (attempt + 1))
    raise RuntimeError(f"Jev request failed after 3 tries ({last})")


def judge(regime, industries):
    """{industry id: {regulation, capture, ...Confidence}} for one regime: two Scores per production industry."""
    jobs = []
    for i in industries:
        state = {"country": {"name": regime["country"], "form": regime["form"], "when": regime["when"]},
                 "industry": {"name": i["name"], "scope": i["scope"]}}
        jobs.append(state)
    with ThreadPoolExecutor(max_workers=6) as pool:
        results = list(pool.map(lambda s: ask(s, QUESTIONS), jobs))
    out = {}
    for i, r in zip(industries, results):
        if r.get("model"):
            MODEL["id"] = r["model"]
        a = r["answers"]
        row = {}
        for q in ("regulation", "capture"):
            n = len(QUESTIONS[q]["criteria"])
            row[q] = round(a[q]["score"] / (n - 1), 4)
            row[q + "Confidence"] = round(a[q].get("confidence", 0.0), 3)
        out[i["id"]] = row
    return out

# --------------------------------------------------------------------------------------------------------------- build


def main():
    src = json.load(open(SRC, encoding="utf-8"))
    industries_file = json.load(open(os.path.join(DATA, "economy", "industries.json"), encoding="utf-8"))
    ids = {i["id"] for i in industries_file["industries"]}
    production = [{"id": i["id"], "name": i["name"], "scope": i["naics"].split("(", 1)[-1].rstrip(")") if "(" in i["naics"] else i["name"]}
                  for i in industries_file["industries"] if i["tier"] != "gov"]
    government = json.load(open(os.path.join(DATA, "government.json"), encoding="utf-8"))

    spending, revenue = imf_series("G_X_G01_GDP_PT"), imf_series("GGR_G01_GDP_PT")
    iso3s = [r["iso3"] for r in src["regimes"] if r.get("iso3")]
    trade, iso2, trade_updated = wb_trade(iso3s)
    vdem_rows, vdem_meta = owid(VDEM_SLUG)
    vdem_col = next(k for k in vdem_meta["columns"] if k.startswith("electdem"))
    vdem_info = vdem_meta["columns"][vdem_col]
    vdem = {}
    for row in vdem_rows:
        if row.get(vdem_col):
            vdem.setdefault(row["entity"], {})[int(row["year"])] = float(row[vdem_col])
    imf_day = retrieved("imf_G_X_G01_GDP_PT.json")
    fortune = fortune_list(src["fortune"]["url"]) if src.get("fortune") else []

    problems = []
    regimes = []
    for r in src["regimes"]:
        iso3, year = r.get("iso3"), r["year"]
        out = {k: r[k] for k in ("id", "name", "short", "form", "country", "era") if k in r}
        out["year"] = year
        if r.get("baseline"):
            out["baseline"] = True

        def imf(series, label, code):
            s = series.get(iso3, {})
            if year not in s:
                return None
            return measure(s[year] / 100, year, f"IMF Fiscal Monitor (October 2026), general government {label}, % of GDP, via the "
                                                f"DataMapper API ({code}; retrieved {imf_day})", IMF_PAGE.format(code, iso3))
        if iso3:
            out["spending"] = imf(spending, "total expenditure", "G_X_G01_GDP_PT")
            out["revenue"] = imf(revenue, "revenue", "GGR_G01_GDP_PT")
        else:
            out["spending"] = curated(r.get("spending"))
            out["revenue"] = curated(r.get("revenue"))

        def revenue_of(y):
            if iso3:
                v = revenue.get(iso3, {}).get(y)
                return None if v is None else v / 100
            return out["revenue"]["value"] if out.get("revenue") and out["revenue"].get("year") == y else None

        mix, notes, fetched = revenue_mix(r.get("revenueMix"), iso3, revenue_of)
        if mix:
            m = r["revenueMix"]
            out["revenueMix"] = {"parts": mix, "year": m["year"], "basis": m["basis"], "source": m.get("source"), "url": m.get("url") or (fetched or {}).get("url"),
                                 "inputs": fetched or {k: v for k, v in m.items() if k not in ("source", "url")}, "computed": notes}
            if m.get("note"):
                out["revenueMix"]["note"] = m["note"]
        else:
            out["revenueMix"] = None
            problems.append(f"{r['id']}: {notes[0]}")

        if iso3:
            s = trade.get(iso3, {})
            ty = max((y for y in s if y <= year), default=None)
            out["trade"] = measure(s[ty] / 100, ty, f"World Bank, World Development Indicators, Trade (% of GDP), NE.TRD.GNFS.ZS "
                                                    f"(last updated {trade_updated})", WB_PAGE.format(iso2.get(iso3, iso3))) if ty else None
        else:
            out["trade"] = curated(r.get("trade"))

        entity = r.get("vdemEntity", r["name"])
        s = vdem.get(entity, {})
        vy = r.get("vdemYear") or (max(s) if s else None)
        if vy in s:
            out["democracy"] = measure(s[vy], vy, f"V-Dem electoral democracy index (v2x_polyarchy), {vdem_info.get('citationShort', 'V-Dem')}, "
                                                  f"via Our World in Data", f"https://ourworldindata.org/grapher/{VDEM_SLUG}?country=~{iso3 or 'RUS'}",
                                       r.get("vdemNote"), previous={"year": vy - 1, "value": round(s[vy - 1], 4)} if vy - 1 in s else None)
        else:
            out["democracy"] = None

        for key in ("top1Wealth", "top1Income", "stateFirms"):
            out[key] = curated(r.get(key))
        # the Fortune Global 500 tally: its share is computed here from the counts read in the sources
        f = r.get("fortune")
        if f and f.get("stateControlled") is not None and f.get("companies"):
            out["fortune"] = {"value": round(f["stateControlled"] / f["companies"], 4), "count": f"{f['stateControlled']} of {f['companies']}",
                              **{k: f[k] for k in ("year", "source", "url", "quote", "note") if f.get(k) is not None}}
        else:
            out["fortune"] = None

        own = r.get("ownership") or {}
        firms = []
        # the summits: each industry's largest firm of the country on the Fortune Global 500 (steel when Fortune says the
        # government owns half or more), else the national leader the research established (Sweden, Saudi Arabia)
        fort = src.get("fortune") or {}
        summits = {}
        if r.get("fortuneCountry"):
            candidates = {}
            for row in fortune:
                if row["country"] != r["fortuneCountry"]:
                    continue
                if row["name"] in fort["firms"]:
                    industry = fort["firms"][row["name"]]
                else:
                    industry = fort["industries"].get(row["sector"] + "|" + row["industry"], fort["industries"].get(row["industry"]))
                    if industry is None:
                        problems.append(f"{r['id']}: no scene industry for Fortune's {row['sector']} | {row['industry']} ({row['name']})")
                if industry is None:
                    continue
                candidates.setdefault(industry, []).append(row)
            for industry, rows in sorted(candidates.items()):
                rows.sort(key=lambda x: -x["revenue"])
                lead = rows[0]
                flag = fortune_owned(lead["slug"])
                if not flag.get("profileFound"):
                    problems.append(f"{r['id']}: Fortune profile of {lead['name']} not found")
                rest = "; ".join(f"{x['name']} ${x['revenue'] / 1000:,.1f}B" for x in rows[1:3])
                summits[industry] = {
                    "name": lead["name"], "industry": industry, "leads": True, "controlled": bool(flag["governmentOwned"]),
                    "holder": "Government owned 50% or more (Fortune)" if flag["governmentOwned"] else None,
                    "basis": "the country's largest firm in this industry on the Fortune Global 500 2026",
                    "revenue": {"value": lead["revenue"], "currency": "USD", "unit": "million", "year": "Fortune Global 500 2026",
                                "rank": lead["rank"], "url": fort["url"]},
                    "url": flag["url"], "quote": flag["footnote"] or "(no 'Government owned' footnote on the profile)",
                    "note": ("next: " + rest if rest else "the country's only firm of this industry on the list") +
                            (f"; {fort['firmNotes'][lead['name']]}" if lead["name"] in fort.get("firmNotes", {}) else "")}
        # the national tier: the firms the research compared on an industry, the largest by revenue (one currency) leads
        for industry, block in sorted((r.get("nationalLeaders") or {}).items()):
            if industry in summits:
                problems.append(f"{r['id']}: {industry} has both a Fortune and a national leader")
                continue
            compared = block["firms"]
            currencies = {f["revenue"].get("currency") for f in compared}
            if any(f["revenue"].get("value") is None for f in compared) or len(currencies) != 1:
                problems.append(f"{r['id']}: {industry} cannot be ranked (a revenue is missing or currencies differ)")
                continue
            compared = sorted(compared, key=lambda f: -f["revenue"]["value"])
            lead = compared[0]
            unit = f" {lead['revenue'].get('currency')}"
            rest = "; ".join(f"{f['name']} {f['revenue']['value'] / 1e9:,.1f}bn{unit}" for f in compared[1:4])
            summits[industry] = {
                "name": lead["name"], "industry": industry, "leads": True, "controlled": bool(lead.get("controlled")),
                "stateShare": lead.get("stateShare"), "holder": lead.get("holder"),
                "basis": "the largest by revenue of the firms the research compared in the country",
                "revenue": lead["revenue"], "url": lead.get("url"), "quote": lead.get("quote"),
                "note": "; ".join(x for x in (("next: " + rest) if rest else "the only firm compared", block.get("note"), lead.get("note")) if x)}
        # the state's holdings named in the ownership research: listed in the panel; one that is a summit lends it its share
        aliases = own.get("aliases", {})
        for f in own.get("firms", []):
            if f.get("industry") not in ids:
                problems.append(f"{r['id']}: {f.get('name')} stands on unknown industry {f.get('industry')}")
                continue
            rec = {k: f[k] for k in ("name", "industry", "stateShare", "controlled", "holder", "asOf", "url", "quote", "note") if k in f}
            summit = summits.get(f["industry"])
            if summit and summit["name"] == aliases.get(f["name"], f["name"]):
                if f.get("stateShare") is not None:
                    summit["stateShare"] = f["stateShare"]
                summit["holder"] = f.get("holder") or summit.get("holder")
                summit["ownershipUrl"] = f.get("url")
                continue
            rec["leads"] = False
            firms.append(rec)
        firms = list(summits.values()) + firms
        out["ownership"] = {"default": own.get("default", 0), "defaultNote": own.get("defaultNote"), "firms": firms}

        if r.get("baseline"):
            out["industries"] = {k: {"regulation": v["regulation"], "capture": v["capture"]} for k, v in government["industries"].items()
                                 if k in ids and k not in ("federal", "state_local")}
            out["industriesJudged"] = "Jev, the government file's own judgments for US industries (Tools/government/build.py)"
        elif not NO_JEV and (jev.available() or OFFLINE or os.path.isdir(os.path.join(CACHE, "jev"))):
            out["industries"] = judge({"country": r["country"], "form": r["form"], "when": r["when"]}, production)
            out["industriesJudged"] = f"Jev ({MODEL['id'] or 'TypeSafe System One'}), two Scores per industry; model judgments, not measurement"
        else:
            out["industries"] = None
            out["industriesJudged"] = None
        out["notes"] = r.get("notes")
        regimes.append(out)

    # checks
    for r in regimes:
        for key in ("spending", "revenue", "trade", "democracy", "top1Wealth", "top1Income", "stateFirms", "fortune"):
            m = r.get(key)
            if m and m.get("value") is not None and not (0 <= m["value"] <= 2.5):
                problems.append(f"{r['id']}: {key} {m['value']} out of range")
        if r.get("revenueMix"):
            s = sum(r["revenueMix"]["parts"].values())
            if abs(s - 1) > 1e-3:
                problems.append(f"{r['id']}: revenue mix sums to {s}")
        for f in r["ownership"]["firms"]:
            if f.get("stateShare") is not None and not (0 <= f["stateShare"] <= 1):
                problems.append(f"{r['id']}: {f['name']} state share {f['stateShare']} out of range")
    for p in problems:
        print("PROBLEM:", p, file=sys.stderr)

    doc = {
        "note": "Other governments as model changes of the economy scene (the 'regimes' view): the same US hills on another state's "
                "foundation, drawn from the state's measured structure. Every measure carries its year, source and URL; null where no "
                "defensible source was found. Built by Tools/regimes/build.py from Tools/regimes/sources.json (research read by hand, "
                "with quotes) and the IMF, World Bank, OECD and V-Dem series fetched through their APIs. The industries' regulation and "
                "capture are Jev judgments (model evidence, not measurement).",
        "built": datetime.date.today().isoformat(),
        "parts": PARTS,
        "partNames": src.get("partNames"),
        "regimes": regimes,
        "jev": {"model": MODEL["id"], "requests": TOKENS["answers"], "tokens": TOKENS["run"] + TOKENS["cached"]},
    }
    with open(OUT, "w", encoding="utf-8") as fh:
        json.dump(doc, fh, indent=1, ensure_ascii=False)
        fh.write("\n")
    print(f"wrote {os.path.relpath(OUT, ROOT)}: {len(regimes)} regimes; Jev {TOKENS['requests']} new requests, "
          f"{TOKENS['run'] + TOKENS['cached']} tokens in all")
    for r in regimes:
        def v(key):
            m = r.get(key)
            return "-" if not m or m.get("value") is None else f"{m['value']:.3f} ({m.get('year')})"
        print(f"  {r['id']:<3} spending {v('spending')}  revenue {v('revenue')}  trade {v('trade')}  democracy {v('democracy')}  "
              f"top1 wealth {v('top1Wealth')}  income {v('top1Income')}  state firms {v('stateFirms')}  "
              f"{len(r['ownership']['firms'])} firms  {'judged' if r.get('industries') else 'no judgments'}")
    if problems:
        sys.exit(1)


if __name__ == "__main__":
    main()

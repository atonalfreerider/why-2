#!/usr/bin/env python3
"""Builds Assets/Resources/Data/economy/global.json: the world economy around the United States (MODALITIES.md section 6,
view M2 "One economy among many"), from the data brief's recipes R1-R9 (Tools/global/research/BRIEF.md section 7).

The 13 economies (USA, CHN, EU27, JPN, IND, GBR, RUS, CAN, MEX, KOR, TWN, SAU, BRA), Germany drawn inside the EU27 and
never added to it, the rest of the world (ROW, the world less the 13) and the world (WLD). 2024 is the base year.

  size        GDP and GDP at PPP: IMF World Economic Outlook (October 2026) via the DataMapper API, one source for every
              economy; population (World Bank WDI; Taiwan from the IMF), labour force (WDI, the ILO's model)
  state       general government expenditure and revenue, % of GDP: IMF Fiscal Monitor (EU27: Eurostat gov_10a_main)
  strata      value added by activity, % of GDP: WDI (US: OECD table 1; Taiwan: DGBAS; Canada's latest is 2022),
              normalised to 100 for the block's strata
  armour      military spending: SIPRI (EU27 = the sum of its 27 members)
  ground      net energy imports (WDI; Taiwan: MOEAEA), crude output (EIA international), LNG exports (GIIGNL)
  clouds      current account (WEO; EU27: Eurostat), net IIP (IMF IIP; EU27: Eurostat; Taiwan: CBC) with its ratio to GDP
              computed in national currency (the stock at the IMF's year-end rate over WEO's GDP in national currency, NGDP
              via the IMF SDMX API), US Treasuries held (TIC, end-Aug 2026: gated)
  trade       goods exports 2024, the exporter's record (IMF IMTS; US exports: Census; Taiwan: MOF), the importer's record
              beside it (IMTS; US imports: Census), mirror gaps over 15% flagged; US services (BEA); the 2025 overlay
              (Census); annual series 2015-2025 for the conflict pairs (Census, Eurostat, IMTS)
  conflict    PIIE's US-China average tariffs, dated events with their sources (and any further source for a date, a scope or
              a later status), recorded outcomes of ended events, the 2026 gate (verify.py) over all of them
  keystones   chokepoint shares (USGS, IEA, SIA/BCG, EIA, GIIGNL, Epoch), straits (EIA, UNCTAD)
  democracy   V-Dem v16 electoral democracy index and Regimes of the World, 2025, via Our World in Data
  order       west to east by the population-weighted centroid of each economy's populated places (Natural Earth)
  history     shares of world GDP at PPP since 1950: Maddison Project 2023 (1950-1979) and WEO PPPSH (1980-2024);
              market-rate shares from WEO NGDPD (1980-2024)

Hand-read values and every event's quotes are in Tools/global/sources.json. Downloads are cached under Tools/global/.cache
(gitignored) with their fetch dates; --offline rebuilds from the cache alone and writes the same bytes. Every stored value
carries {value, year, source, url, field | quote, note}; computed values say how. The 2026 gate (Tools/global/verify.py)
runs last: an unverified item stays in the file with drawn = false. The Jev credential is found by Tools/jev.py and is
never printed.

Usage (from the repository root):
    python3 -I Tools/global/build.py            # fetch what is not cached, run the gate, write global.json
    python3 -I Tools/global/build.py --offline  # the same from the cache only
"""
import csv
import datetime
import io
import json
import math
import os
import re
import struct
import sys
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(ROOT, "Assets", "Resources", "Data", "economy", "global.json")
SRC = os.path.join(HERE, "sources.json")
RESEARCH = os.path.join(HERE, "research", "dataset.json")
sys.dont_write_bytecode = True   # leave no __pycache__ beside the tools
sys.path.insert(0, HERE)
import verify  # noqa: E402  (the shared cache and the 2026 gate)
from verify import norm, sheet_rows, xlsx_sheets  # noqa: E402

OFFLINE = "--offline" in sys.argv
CACHE = verify.Cache(OFFLINE)
SDMX = {"Accept": "application/vnd.sdmx.data+csv;version=1.0.0"}
BASE = 2024

CHECKS, ASSUMED, FALLBACKS, PROBLEMS, SOURCES = [], [], [], [], {}


def check(name, ok, detail):
    CHECKS.append({"check": name, "ok": bool(ok), "detail": detail})
    print(f"CHECK {'ok  ' if ok else 'FAIL'} {name}: {detail}")
    if not ok:
        PROBLEMS.append(f"{name}: {detail}")


def assume(text):
    ASSUMED.append(text)
    print("ASSUMED ≈ " + text)


def fallback(text):
    FALLBACKS.append(text)
    print("FALLBACK " + text)


def rnd(x, d=4):
    if x is None:
        return None
    if isinstance(x, float):
        r = round(x, d)
        return int(r) if r == int(r) and abs(r) < 1e15 and d <= 0 else r
    return x


def val(value, year, source, url, field=None, quote=None, note=None, unit=None, d=4, **extra):
    """One stored value: {value, year, unit, source, url, field | quote, note, ...}."""
    v = {"value": rnd(value, d), "year": year}
    if unit:
        v["unit"] = unit
    v["source"] = source
    v["url"] = url
    if field:
        v["field"] = field
    if quote:
        v["quote"] = quote
    if note:
        v["note"] = note
    v.update({k: x for k, x in extra.items() if x is not None})
    return v


def source(key, name, url, cache_name=None):
    SOURCES[key] = {"name": name, "url": url}
    if cache_name:
        SOURCES[key]["retrieved"] = CACHE.fetched(cache_name)
    return name


def need(body, meta, what):
    if body is None:
        raise SystemExit(f"cannot build without {what}: {meta.get('error')} ({meta.get('url')})")
    return body

# ------------------------------------------------------------------------------------------------------------ sources

IMF_API = "https://www.imf.org/external/datamapper/api/v1/{}"
IMF_PAGE = "https://www.imf.org/external/datamapper/{}@{}/{}"
WDI_API = "https://api.worldbank.org/v2/country/{}/indicator/{}?format=json&date=2010:2025&per_page=1000"
WDI_PAGE = "https://data.worldbank.org/indicator/{}"
ESTAT = "https://ec.europa.eu/eurostat/api/dissemination/statistics/1.0/data/"


def imf(indicator):
    body, meta = CACHE.get(IMF_API.format(indicator), f"imf/{indicator}.json")
    d = json.loads(need(body, meta, f"IMF {indicator}"))
    return {c: {int(y): v for y, v in s.items() if v is not None} for c, s in d.get("values", {}).get(indicator, {}).items()}


def imf_vintage(indicator):
    body, meta = CACHE.get(IMF_API.format("indicators"), "imf/indicators.json")
    if body is None:
        return "IMF"
    info = json.loads(body).get("indicators", {}).get(indicator) or {}
    return info.get("source") or "IMF"


def wdi(code, countries):
    url = WDI_API.format(";".join(countries), code)
    name = f"wdi/{code}.json" if countries == WDI13 else f"wdi/{code}-{len(countries)}.json"
    body, meta = CACHE.get(url, name, timeout=150)
    d = json.loads(need(body, meta, f"WDI {code}"))
    out = {}
    for r in d[1] or []:
        if r.get("value") is not None:
            out.setdefault(r["countryiso3code"], {})[int(r["date"])] = r["value"]
    return out, d[0].get("lastupdated"), url


def eurostat(name, query):
    url = ESTAT + query
    body, meta = CACHE.get(url, f"eurostat/{name}.json")
    d = json.loads(need(body, meta, f"Eurostat {name}"))
    ids, sizes = d["id"], d["size"]
    cats = []
    for i in ids:
        idx = d["dimension"][i]["category"]["index"]
        cats.append({v: k for k, v in idx.items()} if isinstance(idx, dict) else dict(enumerate(idx)))
    out = {}
    for k, v in d["value"].items():
        k, key = int(k), []
        for s, inv in zip(reversed(sizes), reversed(cats)):
            key.append(inv[k % s])
            k //= s
        out[tuple(reversed(key))] = v
    return ids, out, url, d.get("updated")


def sdmx_csv(url, name):
    body, meta = CACHE.get(url, name, headers=SDMX, timeout=300)
    return list(csv.DictReader(io.StringIO(need(body, meta, name).decode("utf-8-sig")))), meta


def census(code):
    """{year: (exports, imports)} in US$ million from a Census country page (the 'TOTAL yyyy' rows)."""
    url = f"https://www.census.gov/foreign-trade/balance/c{code}.html"
    body, meta = CACHE.get(url, f"census/c{code}.html", headers={"Accept": "text/html"})
    t = re.sub(r"\s+", " ", verify.html_text(need(body, meta, f"Census c{code}")))
    out = {}
    for y, e, i, _ in re.findall(r"TOTAL (\d{4}) ([\d,.\-]+) ([\d,.\-]+) ([\d,.\-]+)", t):
        out[int(y)] = (float(e.replace(",", "")), float(i.replace(",", "")))
    return out, url


def latest(series, year=BASE, back=4):
    """(year, value) of the base year, else the latest year before it within `back` years."""
    for y in range(year, year - back - 1, -1):
        if series and y in series:
            return y, series[y]
    return None, None

# ------------------------------------------------------------------------------------------------------------- setup

S = json.load(open(SRC, encoding="utf-8"))
ECON = S["economies"]
IDS13 = [e["id"] for e in ECON if e["id"] != "DEU"]
ALL = [e["id"] for e in ECON] + ["ROW", "WLD"]
BY = {e["id"]: e for e in ECON}
EU = [m["iso3"] for m in S["eu27"]]
WDI13 = [e["wdi"] for e in ECON if e["wdi"]] + ["WLD"]
RESEARCH_DATA = json.load(open(RESEARCH, encoding="utf-8"))


def research_value(measure, eid):
    """The research's own value of a measure (dataset.json), for printing beside the refetched one."""
    m = RESEARCH_DATA["measures"].get(measure)
    if not m or eid not in RESEARCH_DATA["order"]:
        return None
    return m["v"][RESEARCH_DATA["order"].index(eid)]


def asof():
    """The build's as-of date: today online (recorded in the cache), the recorded date offline."""
    path = CACHE.path("asof.txt")
    if OFFLINE:
        if not os.path.exists(path):
            raise SystemExit("--offline: no recorded as-of date (run online first)")
        return open(path, encoding="utf-8").read().strip()
    day = max(datetime.date.today().isoformat(), S["readOn"])
    with open(path, "w", encoding="utf-8") as f:
        f.write(day + "\n")
    return day

# ---------------------------------------------------------------------------------------------------------- economies


def build_size(E):
    gdp, ppp, pppsh, lp = imf("NGDPD"), imf("PPPGDP"), imf("PPPSH"), imf("LP")
    weo = imf_vintage("NGDPD")
    src = source("weo", f"IMF, {weo}, via the DataMapper API", IMF_API.format("NGDPD"), "imf/NGDPD.json")
    world = gdp["WEOWORLD"][BASE]
    world_ppp = ppp["WEOWORLD"][BASE]
    for eid in ALL[:-2]:
        code = BY[eid]["imf"]
        E[eid]["gdp"] = val(gdp[code][BASE], BASE, src, IMF_PAGE.format("NGDPD", "WEO", code), field=f"NGDPD.{code}.{BASE}",
                            unit="US$ bn, market exchange rates", d=3)
        E[eid]["gdpPpp"] = val(ppp[code][BASE], BASE, src, IMF_PAGE.format("PPPGDP", "WEO", code), field=f"PPPGDP.{code}.{BASE}",
                               unit="international $ bn (PPP)", d=3)
        E[eid]["shareWorld"] = val(gdp[code][BASE] / world * 100, BASE, src, IMF_PAGE.format("NGDPD", "WEO", code),
                                   field=f"NGDPD.{code}.{BASE} / NGDPD.WEOWORLD.{BASE}", unit="% of world GDP", note="computed")
        E[eid]["sharePpp"] = val(pppsh[code][BASE], BASE, src, IMF_PAGE.format("PPPSH", "WEO", code), field=f"PPPSH.{code}.{BASE}",
                                 unit="% of world GDP at PPP")
    s13 = sum(gdp[BY[i]["imf"]][BASE] for i in IDS13)
    s13p = sum(ppp[BY[i]["imf"]][BASE] for i in IDS13)
    E["WLD"]["gdp"] = val(world, BASE, src, IMF_PAGE.format("NGDPD", "WEO", "WEOWORLD"), field=f"NGDPD.WEOWORLD.{BASE}",
                          unit="US$ bn, market exchange rates", d=3)
    E["WLD"]["gdpPpp"] = val(world_ppp, BASE, src, IMF_PAGE.format("PPPGDP", "WEO", "WEOWORLD"), field=f"PPPGDP.WEOWORLD.{BASE}",
                             unit="international $ bn (PPP)", d=3)
    E["ROW"]["gdp"] = val(world - s13, BASE, src, IMF_PAGE.format("NGDPD", "WEO", "WEOWORLD"),
                          field="NGDPD.WEOWORLD less the 13", unit="US$ bn", d=3, note="computed: the world less the 13 (a residual)")
    E["ROW"]["gdpPpp"] = val(world_ppp - s13p, BASE, src, IMF_PAGE.format("PPPGDP", "WEO", "WEOWORLD"),
                             field="PPPGDP.WEOWORLD less the 13", unit="international $ bn", d=3, note="computed: a residual")
    E["ROW"]["shareWorld"] = val((world - s13) / world * 100, BASE, src, None, field="1 - the 13's shares", unit="% of world GDP",
                                 note="computed")
    # how many economies the rest of the world gathers: WEO countries with a 2024 GDP outside the 13 (the EU as its 27 members)
    groups = {"ADVEC", "AFQ", "SSQ", "AS5", "APQ", "AZQ", "CBQ", "CMQ", "CAQ", "EAQ", "EEQ", "DA", "EDE", "OEMDC", "EUQ", "EU", "WE", "MAE",
              "MEQ", "MECA", "NAQ", "NMQ", "OAE", "PIQ", "SMQ", "SAQ", "SEQ", "SSA", "WEQ", "WHQ", "WEOWORLD", "EURO"}
    inside = set(EU) | {BY[i]["imf"] for i in IDS13 if i != "EU27"}
    rest = sorted(c for c, s in gdp.items() if c not in groups and BASE in s and c not in inside)
    E["ROW"]["economies"] = val(len(rest), BASE, src, IMF_API.format("NGDPD"), field="countries with NGDPD 2024 outside the 13 and the EU's 27",
                                unit="economies", note="computed")
    E["ROW"]["note"] = f"the rest of the world · {len(rest)} economies together, not one"
    print(f"ROW: {len(rest)} economies with a 2024 WEO GDP outside the 13 (the EU counted as its 27 members)")
    E["ROW"]["sharePpp"] = val(100 - sum(pppsh[BY[i]["imf"]][BASE] for i in IDS13), BASE, src, None, field="100 - PPPSH of the 13",
                               unit="% of world GDP at PPP", note="computed")
    E["WLD"]["shareWorld"] = val(100.0, BASE, src, None, unit="%")
    shares = [E[i]["shareWorld"]["value"] for i in IDS13 + ["ROW"]]
    check("one GDP source per economy (WEO)", all(E[i]["gdp"]["field"].startswith("NGDPD.") for i in IDS13 + ["DEU", "WLD"]),
          "every GDP is IMF WEO NGDPD (October 2026); WDI GDP is not used for size, never mixed for China")
    check("shares at most 100", all(0 <= s <= 100 for s in shares) and abs(sum(shares) - 100) < 1e-6,
          f"13 economies + ROW = {sum(shares):.4f}% of world GDP; largest {max(shares):.2f}%")
    print(f"WORLD GDP 2024 = ${world / 1000:,.1f}T (WEO); the 13 = ${s13 / 1000:,.1f}T = {s13 / world * 100:.1f}% of world output; "
          f"at PPP {s13p / world_ppp * 100:.1f}%; Germany {gdp['DEU'][BASE] / world * 100:.1f}% (inside the EU27's "
          f"{gdp['EU'][BASE] / world * 100:.1f}%, never added)")
    # WDI's GDP, for the record only: the brief's 'use WEO for size' rule
    w, upd, url = wdi("NY.GDP.MKTP.CD", WDI13)
    diffs = []
    for eid in ("USA", "CHN", "DEU", "GBR"):
        if BY[eid]["wdi"] in w and BASE in w[BY[eid]["wdi"]]:
            diffs.append(f"{eid} {(w[BY[eid]['wdi']][BASE] / 1e9 / gdp[BY[eid]['imf']][BASE] - 1) * 100:+.2f}%")
    print("  WDI GDP against WEO (not used): " + ", ".join(diffs))
    # population and labour force
    pop, upd, url = wdi("SP.POP.TOTL", WDI13)
    lab, upd2, url2 = wdi("SL.TLF.TOTL.IN", WDI13)
    wsrc = source("wdi", f"World Bank, World Development Indicators (last updated {upd})", "https://api.worldbank.org/v2/", "wdi/SP.POP.TOTL.json")
    for eid in IDS13 + ["DEU"]:
        code = BY[eid]["wdi"]
        if code:
            y, v = latest(pop.get(code))
            E[eid]["population"] = val(v / 1e6, y, wsrc, WDI_PAGE.format("SP.POP.TOTL"), field=f"SP.POP.TOTL.{code}.{y}", unit="million", d=3)
            y, v = latest(lab.get(code))
            E[eid]["labourForce"] = val(v / 1e6, y, wsrc + ", ILO modelled estimate", WDI_PAGE.format("SL.TLF.TOTL.IN"),
                                        field=f"SL.TLF.TOTL.IN.{code}.{y}", unit="million", d=3)
        else:
            E[eid]["population"] = val(lp["TWN"][BASE], BASE, src, IMF_PAGE.format("LP", "WEO", "TWN"), field=f"LP.TWN.{BASE}",
                                       unit="million", d=3, note="WDI has no Taiwan; IMF WEO population")
            E[eid]["labourForce"] = None
            fallback("Taiwan's labour force: WDI has no Taiwan and the DGBAS manpower table was not read; stored as null "
                     "(labour force is not drawn)")
    wp = pop["WLD"][BASE] / 1e6
    E["WLD"]["population"] = val(wp, BASE, wsrc, WDI_PAGE.format("SP.POP.TOTL"), field=f"SP.POP.TOTL.WLD.{BASE}", unit="million", d=3)
    p13 = sum(E[i]["population"]["value"] for i in IDS13)
    E["ROW"]["population"] = val(wp - p13, BASE, wsrc, None, field="WLD less the 13", unit="million", d=3,
                                 note="computed; Taiwan's IMF population is inside WDI's world total")
    for eid in IDS13 + ["ROW"]:
        E[eid]["sharePeople"] = val(E[eid]["population"]["value"] / wp * 100, BASE, wsrc, None, unit="% of world population", note="computed")
    print(f"PEOPLE 2024: the 13 hold {p13 / wp * 100:.1f}% of the world's {wp:,.0f} million people; the US {E['USA']['population']['value'] / wp * 100:.1f}%")
    return world, s13


def build_state(E):
    fm_x, fm_r = imf("G_X_G01_GDP_PT"), imf("GGR_G01_GDP_PT")
    fm = imf_vintage("G_X_G01_GDP_PT")
    src = source("fm", f"IMF, {fm}, via the DataMapper API", IMF_API.format("G_X_G01_GDP_PT"), "imf/G_X_G01_GDP_PT.json")
    ids, gov, url, upd = eurostat("gov_10a_main", "gov_10a_main?format=JSON&lang=EN&geo=EU27_2020&na_item=TE&na_item=TR&sector=S13&unit=PC_GDP&sinceTimePeriod=2022")
    esrc = source("estat_gov", f"Eurostat gov_10a_main (updated {upd})", url, "eurostat/gov_10a_main.json")
    for eid in IDS13 + ["DEU"]:
        code = BY[eid]["imf"]
        if eid == "EU27":
            te = gov[("A", "PC_GDP", "S13", "TE", "EU27_2020", str(BASE))]
            tr = gov[("A", "PC_GDP", "S13", "TR", "EU27_2020", str(BASE))]
            E[eid]["govSpending"] = val(te, BASE, esrc, url, field=f"gov_10a_main TE S13 PC_GDP EU27_2020 {BASE}", unit="% of GDP",
                                        note="the Fiscal Monitor has no EU27")
            E[eid]["govRevenue"] = val(tr, BASE, esrc, url, field=f"gov_10a_main TR S13 PC_GDP EU27_2020 {BASE}", unit="% of GDP")
        else:
            E[eid]["govSpending"] = val(fm_x[code][BASE], BASE, src, IMF_PAGE.format("G_X_G01_GDP_PT", "FM", code),
                                        field=f"G_X_G01_GDP_PT.{code}.{BASE}", unit="% of GDP", d=2)
            E[eid]["govRevenue"] = val(fm_r[code][BASE], BASE, src, IMF_PAGE.format("GGR_G01_GDP_PT", "FM", code),
                                       field=f"GGR_G01_GDP_PT.{code}.{BASE}", unit="% of GDP", d=2)


def build_strata(E):
    codes = {"agri": "NV.AGR.TOTL.ZS", "industry": "NV.IND.TOTL.ZS", "manuf": "NV.IND.MANF.ZS", "services": "NV.SRV.TOTL.ZS"}
    series = {k: wdi(c, WDI13) for k, c in codes.items()}
    wsrc = SOURCES["wdi"]["name"]
    # US: OECD table 1 (output approach), B1G by activity over B1GQ
    url = "https://sdmx.oecd.org/public/rest/data/OECD.SDD.NAD,DSD_NAMAIN10@DF_TABLE1_OUTPUT,/A.USA........V..?startPeriod=2021&format=csvfilewithlabels"
    body, meta = CACHE.get(url, "oecd/usa_table1_output.csv")
    rows = list(csv.DictReader(io.StringIO(need(body, meta, "OECD table 1").decode("utf-8-sig"))))
    cell = {}
    for r in rows:
        if r["TIME_PERIOD"] == str(BASE) and r["UNIT_MEASURE"] == "XDC" and r["PRICE_BASE"] == "V":
            cell[(r["TRANSACTION"], r["ACTIVITY"])] = float(r["OBS_VALUE"])
    osrc = source("oecd", "OECD, Annual GDP and components, output approach (DSD_NAMAIN10@DF_TABLE1_OUTPUT)", url, "oecd/usa_table1_output.csv")
    gdp = cell[("B1GQ", "_Z")]
    us = {"agri": cell[("B1G", "A")] / gdp * 100, "industry": (cell[("B1G", "BTE")] + cell[("B1G", "F")]) / gdp * 100,
          "manuf": cell[("B1G", "C")] / gdp * 100,
          "services": sum(cell[("B1G", a)] for a in ("GTI", "J", "K", "L", "M_N", "OTQ", "RTU")) / gdp * 100}
    # Taiwan: DGBAS production account (annual, current prices) over GDP from the principal figures
    tw = taiwan_strata()
    for eid in IDS13 + ["DEU"]:
        out = {}
        if eid == "USA":
            for k, v in us.items():
                out[k] = val(v, BASE, osrc, url, field={"agri": "B1G A / B1GQ", "industry": "B1G (BTE+F) / B1GQ", "manuf": "B1G C / B1GQ",
                                                         "services": "B1G (GTI+J+K+L+M_N+OTQ+RTU) / B1GQ"}[k], unit="% of GDP")
            basis = "OECD table 1 (the BEA vintage before the Sept 2026 update), value added at basic prices over GDP"
        elif eid == "TWN":
            out = tw["values"]
            basis = tw["basis"]
        else:
            code = BY[eid]["wdi"]
            y0 = None
            for k, c in codes.items():
                y, v = latest(series[k][0].get(code))
                y0 = y0 or y
                out[k] = val(v, y, wsrc, WDI_PAGE.format(c), field=f"{c}.{code}.{y}", unit="% of GDP")
            basis = "WDI, value added at basic prices over GDP at market prices" + (f"; latest year {y0}" if y0 != BASE else "")
            if y0 != BASE:
                fallback(f"{eid} value added: WDI's latest year is {y0} (not {BASE}); marked on the block")
        a, ind, m, s = (out[k]["value"] for k in ("agri", "industry", "manuf", "services"))
        tot = a + ind + s
        E[eid]["valueAdded"] = dict(out, basis=basis, strata={
            "order": ["agriculture", "industryExManufacturing", "manufacturing", "services"],
            "shares": [rnd(a / tot * 100, 3), rnd((ind - m) / tot * 100, 3), rnd(m / tot * 100, 3), rnd(s / tot * 100, 3)],
            "note": "shares of value added at basic prices, normalised to 100 (agriculture + industry + services); industry "
                    "excluding manufacturing = industry less manufacturing (mining, utilities, construction)"})


def taiwan_strata():
    page = "https://eng.stat.gov.tw/cp.aspx?n=2334"
    body, meta = CACHE.get(page, "taiwan/dgbas_2334.html")
    links = re.findall(r'href="([^"]+\.xlsx)"', need(body, meta, "DGBAS page").decode("utf-8", "replace"))
    prod = next(u for u in links if "生產帳_年" in u or "%E7%94%9F%E7%94%A2%E5%B8%B3_%E5%B9%B4" in u)
    princ = next(u for u in links if "table_eng" in u)
    from urllib.parse import quote
    pu, tu = quote(prod, safe=":/()"), quote(princ, safe=":/()")
    b1, m1 = CACHE.get(pu, "taiwan/dgbas_production_annual.xlsx")
    b2, m2 = CACHE.get(tu, "taiwan/dgbas_table_eng.xlsx")
    rows = sheet_rows(xlsx_sheets(need(b1, m1, "DGBAS production account"))["Gross Domestic Product "])
    col = rows[3].index(float(BASE))
    va = {}
    for r in rows[4:]:
        if isinstance(r[0], str) and re.match(r"\s*[A-S]\. ", r[0]):
            va[r[0].strip()[0]] = r[col]
    prin = sheet_rows(xlsx_sheets(need(b2, m2, "DGBAS principal figures"))["Principal_Figures"])
    gdp = next(r[4] for r in prin if r[0] == float(BASE))
    src = source("dgbas", "DGBAS (Taiwan), national accounts: production account by kind of activity (annual) and principal figures", page,
                 "taiwan/dgbas_production_annual.xlsx")
    ind = sum(va[k] for k in "BCDEF")
    serv = sum(va[k] for k in "GHIJKLMNOPQRS" if k in va)
    vals = {"agri": val(va["A"] / gdp * 100, BASE, src, pu, field=f"'Gross Domestic Product ' sheet, A. / nominal GDP {gdp:,.0f} million NT$", unit="% of GDP"),
            "industry": val(ind / gdp * 100, BASE, src, pu, field="sections B-F / nominal GDP", unit="% of GDP"),
            "manuf": val(va["C"] / gdp * 100, BASE, src, pu, field="section C / nominal GDP", unit="% of GDP"),
            "services": val(serv / gdp * 100, BASE, src, pu, field="sections G-S / nominal GDP", unit="% of GDP")}
    return {"values": vals, "basis": "DGBAS production account (current prices) over nominal GDP; services = sections G-S"}


def build_trade_shares(E):
    ex, upd, _ = wdi("NE.EXP.GNFS.ZS", WDI13)
    im, _, _ = wdi("NE.IMP.GNFS.ZS", WDI13)
    en, _, _ = wdi("EG.IMP.CONS.ZS", WDI13)
    wsrc = SOURCES["wdi"]["name"]
    ids, bop, burl, bupd = eurostat("bop_gs", "bop_eu6_q?format=JSON&lang=EN&geo=EU27_2020&partner=EXT_EU27_2020&bop_item=GS&bop_item=CA&currency=MIO_EUR&sector10=S1&sectpart=S1&stk_flow=CRE&stk_flow=DEB&stk_flow=BAL&s_adj=NSA&freq=A&sinceTimePeriod=2023")
    _, gdpd, gurl, _ = eurostat("gdp", "nama_10_gdp?format=JSON&lang=EN&geo=EU27_2020&na_item=B1GQ&unit=CP_MEUR&sinceTimePeriod=2022")
    eu_gdp = gdpd[("A", "CP_MEUR", "B1GQ", "EU27_2020", str(BASE))]
    bsrc = source("estat_bop", f"Eurostat bop_eu6_q, extra-EU (updated {bupd}), over nama_10_gdp", burl, "eurostat/bop_gs.json")
    nrg_ids, nrg, nurl, nupd = eurostat("nrg", "nrg_ind_id?format=JSON&lang=EN&geo=EU27_2020&siec=TOTAL&unit=PC&sinceTimePeriod=2022")
    for eid in IDS13 + ["DEU"]:
        code = BY[eid]["wdi"]
        if eid == "EU27":
            cre = bop[("A", "MIO_EUR", "GS", "S1", "S1", "NSA", "CRE", "EXT_EU27_2020", "EU27_2020", str(BASE))]
            deb = bop[("A", "MIO_EUR", "GS", "S1", "S1", "NSA", "DEB", "EXT_EU27_2020", "EU27_2020", str(BASE))]
            E[eid]["exportsPct"] = val(cre / eu_gdp * 100, BASE, bsrc, burl, field=f"GS CRE {cre:,.1f} / B1GQ {eu_gdp:,.1f} MIO_EUR", unit="% of GDP",
                                       note="extra-EU exports of goods and services")
            E[eid]["importsPct"] = val(deb / eu_gdp * 100, BASE, bsrc, burl, field=f"GS DEB {deb:,.1f} / B1GQ {eu_gdp:,.1f} MIO_EUR", unit="% of GDP",
                                       note="extra-EU imports of goods and services")
        elif code:
            y, v = latest(ex.get(code))
            E[eid]["exportsPct"] = val(v, y, wsrc, WDI_PAGE.format("NE.EXP.GNFS.ZS"), field=f"NE.EXP.GNFS.ZS.{code}.{y}", unit="% of GDP")
            y, v = latest(im.get(code))
            E[eid]["importsPct"] = val(v, y, wsrc, WDI_PAGE.format("NE.IMP.GNFS.ZS"), field=f"NE.IMP.GNFS.ZS.{code}.{y}", unit="% of GDP")
        else:
            E[eid]["exportsPct"] = E[eid]["importsPct"] = None
        if code:
            y, v = latest(en.get(code))
            E[eid]["energyNetImports"] = val(v, y, wsrc, WDI_PAGE.format("EG.IMP.CONS.ZS"), field=f"EG.IMP.CONS.ZS.{code}.{y}",
                                             unit="% of energy use (negative = net exporter)",
                                             note=(f"latest WDI year {y}" if y != BASE else None))
            if eid == "EU27":
                E[eid]["energyNetImports"]["alternative"] = val(nrg[("A", "TOTAL", "PC", "EU27_2020", str(BASE))], BASE,
                                                                f"Eurostat nrg_ind_id (updated {nupd}), energy import dependency on Eurostat's definition",
                                                                nurl, field="nrg_ind_id TOTAL PC EU27_2020", unit="%")
        else:
            E[eid]["energyNetImports"] = taiwan_energy()
    E["TWN"]["exportsPct"] = E["TWN"]["importsPct"] = None
    fallback("Taiwan exports and imports as % of GDP: the DGBAS expenditure table was not read; stored as null (not drawn)")


def taiwan_energy():
    url = "https://ea01.moeaea.gov.tw/a0303/02/api/v1/zone/annually/1/1"
    body, meta = CACHE.get(url, "taiwan/moeaea_1_1.json")
    d = json.loads(need(body, meta, "MOEAEA energy indicators"))
    rows = d["人均&依存度&能源進口"]
    row = next(r for r in rows if r.get("1-01.能源指標") == BASE)
    src = source("moeaea", "Energy Administration, Ministry of Economic Affairs (Taiwan), energy indicators open API", url, "taiwan/moeaea_1_1.json")
    return val(row["Column5"], BASE, src, url, field="'人均&依存度&能源進口' Column5 (Dependence on Imported Energy)",
               unit="% of energy supply imported", note="Taiwan's official definition counts nuclear fuel as imported")


def build_military(E):
    body, meta = CACHE.get("https://www.sipri.org/sites/default/files/SIPRI-Milex-data-1949-2025_v1.2.xlsx",
                           "sipri/SIPRI-Milex-data-1949-2025_v1.2.xlsx")
    sh = xlsx_sheets(need(body, meta, "SIPRI workbook"))
    src = source("sipri", "SIPRI Military Expenditure Database (v1.2, 2026)", "https://www.sipri.org/databases/milex",
                 "sipri/SIPRI-Milex-data-1949-2025_v1.2.xlsx")
    url = "https://www.sipri.org/sites/default/files/SIPRI-Milex-data-1949-2025_v1.2.xlsx"

    def table(name):
        rows = sheet_rows(sh[name])
        h = next(i for i, r in enumerate(rows) if r and r[0] == "Country")
        col = rows[h].index(float(BASE))
        out = {}
        for r in rows[h + 1:]:
            if r and isinstance(r[0], str) and isinstance(r[col], float):
                out[r[0].strip()] = r[col]
        return out
    usd, share = table("Current US$"), table("Share of GDP")
    reg = sheet_rows(sh["Regional totals"])
    h = next(i for i, r in enumerate(reg) if r and r[0] in ("Region", "Country"))
    col = reg[h].index(float(BASE))
    world = next(r[col] for r in reg[h + 1:] if r and isinstance(r[0], str) and r[0].strip() == "World")
    for eid in IDS13 + ["DEU"]:
        if eid == "EU27":
            continue
        n = BY[eid]["sipri"]
        E[eid]["military"] = {"usd": val(usd[n] / 1000, BASE, src, url, field=f"'Current US$' {n} {BASE}", unit="US$ bn", d=2),
                              "pctGdp": val(share[n] * 100, BASE, src, url, field=f"'Share of GDP' {n} {BASE}", unit="% of GDP", d=3),
                              "shareWorld": val(usd[n] / 1000 / world * 100, BASE, src, url, field=f"/ World {world:,.1f} ('Regional totals')",
                                                unit="% of world military spending", note="computed", d=2)}
    members = [m["sipri"] for m in S["eu27"]]
    missing = [m for m in members if m not in usd or m not in share]
    total = sum(usd[m] for m in members if m in usd)
    implied_gdp = sum(usd[m] / share[m] for m in members if m in usd and m in share and share[m])
    E["EU27"]["military"] = {"usd": val(total / 1000, BASE, src, url, field="sum of the 27 members' 'Current US$'", unit="US$ bn", d=2,
                                        note="EU27 = the sum of its 27 members"),
                             "pctGdp": val(total / implied_gdp * 100, BASE, src, url, field="sum of spending / sum of spending ÷ share of GDP",
                                           unit="% of GDP", d=3, note="computed from SIPRI's own GDP denominators"),
                             "shareWorld": val(total / 1000 / world * 100, BASE, src, url, unit="% of world military spending", note="computed", d=2)}
    E["WLD"]["military"] = {"usd": val(world, BASE, src, url, field=f"'Regional totals' World {BASE} (constant 2024 = current {BASE} US$)", unit="US$ bn", d=1)}
    w, upd, wurl = wdi("MS.MIL.XPND.CD", WDI13)
    eu_wdi = w.get("EUU", {}).get(BASE)
    check("EU27 military = sum of members", not missing and eu_wdi is not None and abs(total * 1e6 / eu_wdi - 1) < 0.01,
          f"SIPRI 27 members ${total / 1000:,.1f}bn; WDI's EU aggregate (MS.MIL.XPND.CD.EUU) ${(eu_wdi or 0) / 1e9:,.1f}bn"
          + (f"; missing {missing}" if missing else ""))
    s13 = sum(E[i]["military"]["usd"]["value"] for i in IDS13)
    E["ROW"]["military"] = {"usd": val(world - s13, BASE, src, url, field="World less the 13", unit="US$ bn", d=2, note="computed")}
    print(f"MILITARY 2024: US ${E['USA']['military']['usd']['value']:,.1f}bn = {E['USA']['military']['pctGdp']['value']:.2f}% of GDP = "
          f"{E['USA']['military']['shareWorld']['value']:.1f}% of world ${world:,.1f}bn; the 13 = {s13 / world * 100:.1f}%")


def build_external(E):
    bca, bcap = imf("BCA"), imf("BCA_NGDPD")
    weo = SOURCES["weo"]["name"]
    gdp = imf("NGDPD")
    ids, bop, burl, _ = eurostat("bop_gs", "bop_eu6_q?format=JSON&lang=EN&geo=EU27_2020&partner=EXT_EU27_2020&bop_item=GS&bop_item=CA&currency=MIO_EUR&sector10=S1&sectpart=S1&stk_flow=CRE&stk_flow=DEB&stk_flow=BAL&s_adj=NSA&freq=A&sinceTimePeriod=2023")
    _, gdpd, _, _ = eurostat("gdp", "nama_10_gdp?format=JSON&lang=EN&geo=EU27_2020&na_item=B1GQ&unit=CP_MEUR&sinceTimePeriod=2022")
    _, ert, eurl, _ = eurostat("ert", "ert_bil_eur_a?format=JSON&lang=EN&currency=USD&statinfo=AVG&statinfo=END&sinceTimePeriod=2022")
    avg, end = ert[("A", "AVG", "NAC", "USD", str(BASE))], ert[("A", "END", "NAC", "USD", str(BASE))]
    eu_gdp = gdpd[("A", "CP_MEUR", "B1GQ", "EU27_2020", str(BASE))]
    ca_eu = bop[("A", "MIO_EUR", "CA", "S1", "S1", "NSA", "BAL", "EXT_EU27_2020", "EU27_2020", str(BASE))]
    esrc = SOURCES["estat_bop"]["name"]
    for eid in IDS13 + ["DEU"]:
        code = BY[eid]["imf"]
        if eid == "EU27":
            E[eid]["currentAccount"] = {
                "usd": val(ca_eu * avg / 1000, BASE, esrc, burl, field=f"CA BAL {ca_eu:,.1f} MIO_EUR × {avg} USD/EUR (ert_bil_eur_a AVG)",
                           unit="US$ bn", d=2, note="extra-EU current account"),
                "pctGdp": val(ca_eu / eu_gdp * 100, BASE, esrc, burl, field="CA BAL / B1GQ (both EUR)", unit="% of GDP", d=3)}
        else:
            E[eid]["currentAccount"] = {"usd": val(bca[code][BASE], BASE, weo, IMF_PAGE.format("BCA", "WEO", code), field=f"BCA.{code}.{BASE}", unit="US$ bn", d=3),
                                        "pctGdp": val(bcap[code][BASE], BASE, weo, IMF_PAGE.format("BCA_NGDPD", "WEO", code),
                                                      field=f"BCA_NGDPD.{code}.{BASE}", unit="% of GDP", d=2)}
    # net international investment positions, end-2024, with ratios in national currency
    rows, _ = sdmx_csv("https://api.imf.org/external/sdmx/2.1/data/IMF.STA,IIP/USA+CHN+DEU+JPN+IND+GBR+RUS+CAN+MEX+KOR+SAU+BRA+G163.NETAL_P.NIIP.USD.A+Q?startPeriod=2023",
                       "imf/iip_usd.csv")
    iip = {(r["COUNTRY"], r["TIME_PERIOD"]): float(r["OBS_VALUE"]) for r in rows if r["OBS_VALUE"] and r["FREQUENCY"] == "A"}
    isrc = source("iip", "IMF, International Investment Position (IMF.STA,IIP), net, US$", "https://api.imf.org/external/sdmx/2.1/data/IMF.STA,IIP/", "imf/iip_usd.csv")
    ers, _ = sdmx_csv("https://api.imf.org/external/sdmx/2.1/data/IMF.STA,ER/JPN+DEU+KOR+BRA+RUS+MEX+CAN+CHN+IND+GBR+SAU+USA...A?startPeriod=2023&detail=dataonly",
                      "imf/er.csv")
    er = {(r["COUNTRY"], r["INDICATOR"], r["TYPE_OF_TRANSFORMATION"], r["TIME_PERIOD"]): float(r["OBS_VALUE"]) for r in ers if r["OBS_VALUE"]}
    rsrc = source("er", "IMF, Exchange Rates (IMF.STA,ER): national currency per US$, end of period and period average",
                  "https://api.imf.org/external/sdmx/2.1/data/IMF.STA,ER/", "imf/er.csv")
    # GDP in national currency from the WEO itself (the same vintage as NGDPD): no exchange rate on the GDP side, so China is
    # not converted at a rate the WEO does not use (BRIEF 1a: WEO's yuan rate is about 7.122, the market average 7.1975)
    wurl = ("https://api.imf.org/external/sdmx/2.1/data/IMF.RES,WEO/USA+CHN+DEU+JPN+IND+GBR+RUS+CAN+MEX+KOR+TWN+SAU+BRA."
            "NGDP+NGDPD.A?startPeriod=2023&endPeriod=2025&detail=dataonly")
    wrows, _ = sdmx_csv(wurl, "imf/weo_ngdp.csv")
    weo = {(r["COUNTRY"], r["INDICATOR"], int(r["TIME_PERIOD"])): float(r["OBS_VALUE"]) / 1e9 for r in wrows if r["OBS_VALUE"]}
    nsrc = source("weo_ngdp", "IMF, World Economic Outlook database (IMF.RES,WEO) via the IMF SDMX API: GDP in national currency (NGDP)",
                  wurl, "imf/weo_ngdp.csv")
    weo_ids = [i for i in IDS13 + ["DEU"] if i != "EU27"]
    vintage = max(abs(weo[(BY[i]["imf"], "NGDPD", BASE)] / gdp[BY[i]["imf"]][BASE] - 1) for i in weo_ids)
    implied = []
    for i in weo_ids:
        c = BY[i]["imf"]
        rate = weo[(c, "NGDP", BASE)] / weo[(c, "NGDPD", BASE)]
        pa = er.get((c, "XDC_USD", "PA_RT", str(BASE))) or ({"DEU": 1 / avg, "USA": 1.0}.get(i))
        if pa and abs(rate / pa - 1) > 0.002:
            implied.append(f"{i} {rate:.4g} vs {pa:.4g} ({(rate / pa - 1) * 100:+.2f}%)")
    print(f"  WEO NGDP (SDMX) against the DataMapper's NGDPD: same vintage, largest difference {vintage * 100:.5f}%; WEO's own conversion "
          f"(NGDP / NGDPD) differs from the IMF's average market rate by more than 0.2% for: {', '.join(implied) or 'none'}")
    lines = []
    for eid in IDS13 + ["DEU"]:
        code = BY[eid]["imf"]
        g = gdp[code][BASE]
        ngdp = weo.get((code, "NGDP", BASE))
        if eid == "EU27":
            _, iipd, iurl, iupd = eurostat("iip", "bop_iip6_q?format=JSON&lang=EN&geo=EU27_2020&partner=EXT_EU27_2020&bop_item=FA__TXR__F&sector10=S1&sectpart=S1&stk_flow=N_LE&currency=MIO_EUR&freq=A&sinceTimePeriod=2022")
            n = iipd[("A", "MIO_EUR", "FA__TXR__F", "S1", "S1", "N_LE", "EXT_EU27_2020", "EU27_2020", str(BASE))]
            usd = n * end / 1000
            pct = n / eu_gdp * 100
            s, u, f = f"Eurostat bop_iip6_q (updated {iupd}), excluding reserve assets", iurl, f"FA__TXR__F N_LE {n:,.1f} MIO_EUR × {end} (END)"
            how = f"N_LE {n:,.1f} / B1GQ {eu_gdp:,.1f} (both MIO_EUR)"
        elif eid == "TWN":
            usd, pct, s, u, f, how = taiwan_iip(ngdp)
        else:
            usd = iip[(code, str(BASE))] / 1e9
            s, u, f = isrc, "https://api.imf.org/external/sdmx/2.1/data/IMF.STA,IIP/", f"{code}.NETAL_P.NIIP.USD.A {BASE}"
            if eid == "USA":
                pct, how = usd / ngdp * 100, f"US$ / WEO NGDP {ngdp:,.1f} bn US$"
            elif eid == "DEU":   # the IMF's exchange-rate table has no euro-area members: ECB reference rates via Eurostat
                pct = usd / end / ngdp * 100
                how = f"÷ {end} US$ per EUR (end of {BASE}, Eurostat ert_bil_eur_a) / WEO NGDP {ngdp:,.1f} bn EUR"
            else:
                eop = er[(code, "XDC_USD", "EOP_RT", str(BASE))]
                pct = usd * eop / ngdp * 100
                how = f"× {eop:.6g} per US$ (end of {BASE}, IMF ER) / WEO NGDP {ngdp:,.1f} bn national currency"
        rates = {"USA": "none needed", "EU27": "Eurostat ert_bil_eur_a (ECB reference rates, for the US$ stock only)",
                 "DEU": "Eurostat ert_bil_eur_a (ECB reference rates)", "TWN": "DGBAS"}.get(eid, rsrc)
        gsrc = "Eurostat nama_10_gdp" if eid == "EU27" else nsrc
        E[eid]["niip"] = {"usd": val(usd, BASE, s, u, field=f, unit="US$ bn, end of year", d=1),
                          "pctGdp": val(pct, BASE, f"{s}; GDP: {gsrc}; exchange rates: {rates}", u, field=how, unit="% of GDP", d=2,
                                        note="computed in national currency: the year-end stock at the year-end rate over GDP in national currency",
                                        gdpUrl=None if eid == "EU27" else wurl)}
        reported = usd / g * 100
        lines.append(f"{eid} {pct:+.1f}% (US$ ratio {reported:+.1f})")
    check("net IIP ratios in national currency", all(E[i]["niip"]["pctGdp"]["value"] is not None for i in IDS13) and vintage < 1e-4,
          "over GDP in national currency (WEO NGDP; EU27 Eurostat): " + "; ".join(lines))


def taiwan_iip(ngdp_twd):
    """Taiwan's net IIP (CBC, US$) and its ratio to GDP in Taiwan dollars (WEO NGDP), the stock at DGBAS's year-end rate."""
    page = "https://www.cbc.gov.tw/en/cp-513-26934-DD294-2.html"
    body, meta = CACHE.get(page, "taiwan/cbc_iip_page.html")
    link = re.search(r'href="(https://www\.cbc\.gov\.tw/en/dl-[^"]+)"[^>]*>\s*PDF', need(body, meta, "CBC page").decode("utf-8", "replace"))
    url = link.group(1)
    b, m = CACHE.get(url, "taiwan/cbc_iip.pdf")
    need(b, m, "CBC IIP PDF")
    lines = verify.pdf_text(CACHE, "taiwan/cbc_iip.pdf", layout=True).splitlines()
    years = next(l.split() for l in lines if re.match(r"\s*2000\s+2001", l))
    row = next(l for l in lines if l.strip().startswith("Net international investment position"))
    nums = re.findall(r"-?[\d,]+", row.split("position", 1)[1])
    i = years.index(str(BASE))
    usd = float(nums[i].replace(",", "")) / 1000
    # TWD per US$ at the end of the year: the DGBAS key indicators
    b3, m3 = CACHE.get("https://ws.dgbas.gov.tw/001/Upload/464/relfile/10320/1812/index-eng.xlsx", "taiwan/dgbas_index_eng.xlsx")
    rows = sheet_rows(xlsx_sheets(need(b3, m3, "DGBAS key indicators"))["Key Economic & Social Indicator"])
    hdr = next(r for r in rows if float(BASE) in r)
    fx = next(r for r in rows if any(isinstance(x, str) and "Foreign exchange (end of period)" in x for x in r))
    end = fx[hdr.index(float(BASE))]
    src = source("cbc", "Central Bank of the Republic of China (Taiwan), International Investment Position (US$ million)", page, "taiwan/cbc_iip.pdf")
    pct = usd * end / ngdp_twd * 100
    return usd, pct, src, url, f"'Net international investment position' {BASE} column: {nums[i]}", \
        f"× {end} TWD per US$ (end of {BASE}, DGBAS key indicators) / WEO NGDP {ngdp_twd:,.1f} bn TWD"


def build_energy(E):
    body, meta = CACHE.get("https://www.eia.gov/opendata/bulk/INTL.zip", "eia/INTL.zip", timeout=600)
    z = zipfile.ZipFile(io.BytesIO(need(body, meta, "EIA international bulk file")))
    want = {f"INTL.57-1-{BY[i]['eia']}-TBPD.A": i for i in IDS13 + ["DEU"]}
    want["INTL.57-1-WORL-TBPD.A"] = "WLD"
    found = {}
    with z.open(z.namelist()[0]) as f:
        for line in io.TextIOWrapper(f, encoding="utf-8"):
            if '"INTL.57-1-' not in line[:40]:
                continue
            d = json.loads(line)
            if d.get("series_id") in want:
                found[want[d["series_id"]]] = {int(y): v for y, v in d["data"] if isinstance(v, (int, float))}
    src = source("eia_intl", "EIA, International Energy Statistics bulk file (INTL.zip): crude oil including lease condensate production",
                 "https://www.eia.gov/opendata/bulk/INTL.zip", "eia/INTL.zip")
    world = found["WLD"][BASE]
    for eid in IDS13 + ["DEU"]:
        v = found.get(eid, {}).get(BASE)
        E[eid]["crudeShare"] = val(None if v is None else v / world * 100, BASE, src, "https://www.eia.gov/international/data/world",
                                   field=f"INTL.57-1-{BY[eid]['eia']}-TBPD.A {BASE} / INTL.57-1-WORL-TBPD.A ({world:,.1f} kb/d)",
                                   unit="% of world crude output", d=2, note="computed")
    return world

# -------------------------------------------------------------------------------------------------------------- trade


def build_trade(E):
    order = IDS13
    url = (f"https://api.imf.org/external/sdmx/2.1/data/IMF.STA,IMTS/USA+CHN+G998+DEU+JPN+IND+GBR+RUS+CAN+MEX+KOR+TWN+SAU+BRA."
           f"XG_FOB_USD+MG_CIF_USD+MG_FOB_USD.USA+CHN+G998+DEU+JPN+IND+GBR+RUS+CAN+MEX+KOR+TWN+SAU+BRA+G001.A?startPeriod=2015&endPeriod=2025&detail=dataonly")
    rows, meta = sdmx_csv(url, "imf/imts_matrix_2015_2025.csv")
    imts = {}
    for r in rows:
        if r["OBS_VALUE"]:
            imts[(r["COUNTRY"], r["INDICATOR"], r["COUNTERPART_COUNTRY"], int(r["TIME_PERIOD"]))] = float(r["OBS_VALUE"]) / 1e9
    isrc = source("imts", "IMF, International Merchandise Trade Statistics (IMF.STA,IMTS), annual, US$", url, "imf/imts_matrix_2015_2025.csv")
    # US: Census country pages (exports and general imports, customs basis)
    cen = {i: census(BY[i]["census"]) for i in order if BY[i]["census"]}
    csrc = source("census", "US Census Bureau, Trade in Goods by country (country pages, 'TOTAL yyyy' rows, US$ million)",
                  "https://www.census.gov/foreign-trade/balance/", None)
    # Taiwan: MOF CSV (Big5, US$ thousand), EU27 = sum of the 27 members' columns
    tw = taiwan_trade()
    # Russia's goods totals: Bank of Russia balance of payments
    ru = russia_totals()

    def code(i):
        return BY[i]["imts"]
    matrix, mirror, cellsrc = [], [], []
    toROW, total, totsrc = [], [], []
    for a in order:
        row, mrow, srow = [], [], []
        for b in order:
            if a == b:
                row.append(None), mrow.append(None), srow.append(None)
                continue
            if a == "USA":
                v = cen[b][0][BASE][0] / 1000
                s = f"census:c{BY[b]['census']} exports {BASE}"
            elif a == "TWN":
                v = tw["exports"][b]
                s = f"mof:u2010ex {BASE}"
            else:
                v = imts.get((code(a), "XG_FOB_USD", code(b), BASE))
                s = f"imts:{code(a)}.XG_FOB_USD.{code(b)}.A {BASE}"
            # the importer's record of the same flow
            if b == "USA":
                mv = cen[a][0][BASE][1] / 1000
                ms = f"census:c{BY[a]['census']} imports {BASE}"
            elif b == "TWN":
                mv = tw["imports"][a]
                ms = f"mof:u2010im {BASE}"
            else:
                mv = imts.get((code(b), "MG_CIF_USD", code(a), BASE))
                ms = f"imts:{code(b)}.MG_CIF_USD.{code(a)}.A {BASE} (c.i.f.)"
            row.append(rnd(v, 2))
            mrow.append(None if mv is None else rnd(mv, 2))
            srow.append({"record": s, "mirror": ms})
        matrix.append(row), mirror.append(mrow), cellsrc.append(srow)
        if a == "USA":
            t = imts[("USA", "XG_FOB_USD", "G001", BASE)]
            ts = f"imts:USA.XG_FOB_USD.G001.A {BASE}"
        elif a == "TWN":
            t, ts = tw["exports"]["_total"], f"mof:u2010ex 總計 {BASE}"
        elif a == "RUS":
            t, ts = ru["exports"], f"cbr:bal_of_payments_standart 'Годы' row 13, {BASE}"
        elif a == "EU27":   # IMTS's G998 exports to the world include the members' trade with each other: take it out
            t = imts[("G998", "XG_FOB_USD", "G001", BASE)] - imts[("G998", "XG_FOB_USD", "G998", BASE)]
            ts = f"imts:G998.XG_FOB_USD.G001.A {BASE} less G998.XG_FOB_USD.G998.A {BASE} (extra-EU: exports to the world less intra-EU exports)"
        else:
            t = imts[(code(a), "XG_FOB_USD", "G001", BASE)]
            ts = f"imts:{code(a)}.XG_FOB_USD.G001.A {BASE}"
        partners = sum(x for x in row if x)
        total.append(rnd(t, 2)), toROW.append(rnd(t - partners, 2)), totsrc.append(ts)
    # mirror gaps: every cell whose two records, as stored (US$ bn to 2 decimals), differ by more than 15%; no size floor
    flagged = []
    for i, a in enumerate(order):
        for j, b in enumerate(order):
            v, mv = matrix[i][j], mirror[i][j]
            if i != j and v and mv and abs(mv / v - 1) > 0.15:
                p = 1 if min(v, mv) >= 1 else 2   # both records at the same precision; two decimals when one is under $1bn
                flagged.append(f"{a}>{b} {v:,.{p}f} vs {mv:,.{p}f} ({(mv / v - 1) * 100:+.0f}%)")
    # checks
    ok = all(abs((sum(x for x in r if x) + toROW[i]) - total[i]) < 0.05 and toROW[i] >= 0 for i, r in enumerate(matrix))
    check("partners + ROW = row totals", ok, "; ".join(f"{a} {sum(x for x in r if x):,.1f}+{toROW[i]:,.1f}={total[i]:,.1f}"
                                                     for i, (a, r) in enumerate(zip(order, matrix)) if a in ("USA", "CHN", "EU27", "RUS", "TWN"))
          + " … (all 13 rows)")
    # the EU27 row against figures the residual cannot absorb: the members' own records of their intra-EU exports (a separate
    # download), the research's EU27 total and its summed ROW column
    e = order.index("EU27")
    intra, intra_raw = intra_eu_exports()
    eu_world, eu_intra = imts[("G998", "XG_FOB_USD", "G001", BASE)], imts[("G998", "XG_FOB_USD", "G998", BASE)]
    r_tot = RESEARCH_DATA["tradeMatrix"]["total"][RESEARCH_DATA["tradeMatrix"]["order"].index("EU27")]
    r_row = sum(RESEARCH_DATA["tradeMatrix"]["toROW"])
    check("EU27 row is extra-EU", abs(total[e] / (eu_world - intra_raw) - 1) < 0.005 and abs(eu_intra / intra_raw - 1) < 0.005
          and abs(total[e] / r_tot - 1) < 0.005 and min(toROW) >= 0 and abs(sum(toROW) / r_row - 1) < 0.01,
          f"EU27 total {total[e]:,.1f} = {eu_world:,.1f} to the world less {eu_intra:,.1f} intra-EU (the 27 members' own records "
          f"{intra_raw:,.1f}); research {r_tot:,.1f}; EU27 to ROW {toROW[e]:,.1f}; every ROW cell >= 0 (smallest {min(toROW):,.1f}); "
          f"summed ROW column {sum(toROW):,.1f} (research rows {r_row:,.1f}, brief 2b 4,580.5)")
    recomputed = {f"{order[i]}>{order[j]}" for i in range(len(order)) for j in range(len(order))
                  if i != j and matrix[i][j] and mirror[i][j] and abs(mirror[i][j] - matrix[i][j]) > 0.15 * matrix[i][j]}
    listed = {s.split(" ", 1)[0] for s in flagged}
    one_sided = [f"{order[i]}>{order[j]}" for i in range(len(order)) for j in range(len(order))
                 if i != j and bool(matrix[i][j]) != bool(mirror[i][j])]
    small = [s for s in flagged if max(float(x.replace(",", "")) for x in s.split(" ")[1:4:2]) < 1]
    check("mirror gaps over 15% flagged", recomputed == listed and not one_sided,
          f"{len(flagged)} of 156 cells differ by more than 15% between the two records (recomputed from matrix and mirror: "
          f"{len(recomputed)}, the same set{'' if recomputed == listed else ': DIFFERS ' + str(sorted(recomputed ^ listed))}; no size floor, "
          f"{len(small)} of them under $1bn); one-sided cells: {len(one_sided)}; e.g. " + "; ".join(flagged[:6]))
    check("DEU never added to EU27", "DEU" not in order and all("DEU" not in json.dumps(s) for s in cellsrc),
          "Germany is not a row or column of the 13×13 matrix; its own trade is kept apart (germany block) and the EU27 row is extra-EU (IMTS G998)")
    # Germany inside the EU27: exports to and imports from the US, China, the UK, and intra-EU
    deu = {}
    for p in ("USA", "CHN", "GBR"):
        deu[p] = {"exports": rnd(imts.get(("DEU", "XG_FOB_USD", code(p), BASE)), 2), "imports": rnd(imts.get(("DEU", "MG_CIF_USD", code(p), BASE)), 2)}
    deu["EU27"] = {"exports": rnd(imts.get(("DEU", "XG_FOB_USD", "G998", BASE)), 2), "imports": rnd(imts.get(("DEU", "MG_CIF_USD", "G998", BASE)), 2)}
    # world goods exports and intra-EU trade
    wm, upd, wurl = wdi("TX.VAL.MRCH.CD.WT", WDI13)
    world_x = wm["WLD"][BASE] / 1e9
    arcs = sum(x for r in matrix for x in r if x)
    ex_intra = world_x - intra["value"]
    # drawn lanes: all 12 US pairs and the 6 largest other two-way pairs
    pairs = {}
    for i, a in enumerate(order):
        for j, b in enumerate(order):
            if i < j:
                pairs[(a, b)] = (matrix[i][j] or 0) + (matrix[j][i] or 0)
    us_pairs = [p for p in pairs if "USA" in p]
    others = sorted((p for p in pairs if "USA" not in p), key=lambda p: -pairs[p])[:6]
    drawn = sum(pairs[p] for p in us_pairs + others)
    lanes = [{"pair": f"{a}-{b}", "twoWay": rnd(pairs[(a, b)], 2), "set": "default" if (a, b) in us_pairs or (a, b) in others else "all",
              "role": "subject" if "USA" in (a, b) else ("support" if (a, b) in others else "context")}
             for (a, b) in sorted(pairs, key=lambda p: -pairs[p])]
    print(f"TRADE 2024: 156 arcs carry ${arcs:,.1f}bn = {arcs / ex_intra * 100:.1f}% of world goods exports outside intra-EU trade "
          f"(${world_x:,.1f}bn less ${intra['value']:,.1f}bn); default lanes (12 US pairs + 6 others: "
          + ", ".join(f"{a}-{b} {pairs[(a, b)]:,.1f}" for a, b in others) + f") carry {drawn / ex_intra * 100:.1f}%")
    research_arcs = RESEARCH_DATA["tradeMatrix"]["arcsTotal"]
    print(f"  research arcs total {research_arcs:,.1f} (refetched {arcs:,.1f}); CHN>USA {matrix[1][0]} / mirror {mirror[1][0]}")
    # US services by partner (BEA geo time series, Tables 7 and 8)
    services = us_services()
    # 2025 overlay: US imports by partner (Census), exporter-side 2025 (IMTS; Taiwan: MOF)
    overlay = {"source": csrc, "unit": "US$ bn", "note": "US customs records (general imports), before and after the 2025 tariff shock", "partners": {}}
    for b in order:
        if b == "USA":
            continue
        i24, i25 = cen[b][0][BASE][1] / 1000, cen[b][0][2025][1] / 1000
        exp25 = tw["exports2025"]["USA"] if b == "TWN" else imts.get((code(b), "XG_FOB_USD", "USA", 2025))
        overlay["partners"][b] = {"imports2024": rnd(i24, 2), "imports2025": rnd(i25, 2), "changePct": rnd((i25 / i24 - 1) * 100, 1),
                                  "exporterRecord2025": rnd(exp25, 2),
                                  "url": f"https://www.census.gov/foreign-trade/balance/c{BY[b]['census']}.html",
                                  "field": f"c{BY[b]['census']} TOTAL 2024 / TOTAL 2025 imports"}
    print("  2025 overlay (US imports, Census): CHN {changePct:+.1f}%".format(**overlay["partners"]["CHN"])
          + ", TWN {changePct:+.1f}%".format(**overlay["partners"]["TWN"]))
    trade = {
        "year": BASE, "unit": "US$ bn", "basis": "goods exports as the exporter records them (f.o.b.); the importer's record (mostly c.i.f.) as the mirror",
        "rule": "one record per lane: the exporter's; the importer's record goes in the label and on hover",
        "order": order,
        "matrix": matrix, "mirror": mirror, "cells": cellsrc, "toROW": toROW, "total": total, "totalSource": totsrc,
        "sources": {"imts": isrc, "census": csrc, "mof": tw["source"], "cbr": ru["source"]},
        "notes": ["EU27 trade is extra-EU (IMF code G998): its cells are trade with non-members, and its total is G998's exports to the world "
                  "less its intra-EU exports; intra-EU trade is a label on the EU block, never an arc",
                  "Russia has published no partner data since 2022: its cells are the IMF's estimates built from partner records "
                  "(draw them in the ghost style, 'partner records'); its total is the Bank of Russia's balance of payments",
                  "US exports and imports are Census country-page totals (customs basis)",
                  "Taiwan's cells are Taiwan MOF customs data (EU27 = the sum of the 27 members' columns); Taiwan reports nothing to the IMF",
                  "Taiwan → China: China records far more than Taiwan does (Hong Kong re-exports); draw a 'via Hong Kong' stub"],
        "taiwanViaHongKong": {"exports2024": rnd(tw["exports"]["_HKG"], 2), "field": f"mof:u2010ex 香港 {BASE}"},
        "mirrorGapsFlagged": flagged,
        "germany": dict(deu, note="Germany inside the EU27: drawn inside the EU block, never added to it", source=isrc),
        "worldGoodsExports": val(world_x, BASE, SOURCES["wdi"]["name"], WDI_PAGE.format("TX.VAL.MRCH.CD.WT"), field=f"TX.VAL.MRCH.CD.WT.WLD.{BASE}", unit="US$ bn", d=1),
        "intraEU": intra,
        "euExtraExports": val(eu_world - eu_intra, BASE, isrc, url, field=f"G998.XG_FOB_USD.G001 {BASE} less G998.XG_FOB_USD.G998 {BASE}", unit="US$ bn", d=1,
                              note="computed: the EU27's goods exports to the world less its intra-EU exports (the EU27 row's total)"),
        "euWorldExports": val(eu_world, BASE, isrc, url, field=f"G998.XG_FOB_USD.G001 {BASE}", unit="US$ bn", d=1,
                              note="IMTS's EU27 exports to the world, intra-EU trade included: never drawn, never a row total"),
        "mirrorGapRule": "every cell whose importer's record differs from the exporter's by more than 15%, both as stored (US$ bn to 2 decimals); "
                         "no size floor, so a gap between cells under $0.1bn is coarse at this precision",
        "arcsTotal": rnd(arcs, 1),
        "arcsShareWorldExIntraEU": rnd(arcs / ex_intra * 100, 2),
        "lanes": lanes,
        "defaultSet": {"usPairs": [f"{a}-{b}" for a, b in us_pairs], "otherPairs": [f"{a}-{b}" for a, b in others],
                       "shareWorldExIntraEU": rnd(drawn / ex_intra * 100, 2), "twoWayTotal": rnd(drawn, 1),
                       "note": "computed for the drawn set: all 12 US pairs both ways plus the six largest other two-way pairs"},
        "usServices": services,
        "overlay2025": overlay,
    }
    return trade, imts, cen, tw


def taiwan_trade():
    out = {}
    for flow, fname in (("exports", "u2010ex"), ("imports", "u2010im")):
        url = f"https://service.mof.gov.tw/public/data/statistic/trade/{fname}.csv"
        body, meta = CACHE.get(url, f"taiwan/mof_{fname}.csv")
        rows = list(csv.reader(io.StringIO(need(body, meta, f"MOF {fname}").decode("big5", "replace"))))
        head = [h.replace("(千元)", "").strip() for h in rows[0]]
        for y in (BASE, 2025):
            r = next(r for r in rows if r and r[0].strip() == f"{y}年")
            cell = {h: float(v.replace(",", "")) / 1e6 for h, v in zip(head, r) if v.strip() and re.match(r"^-?[\d,.]+$", v.strip())}
            d = {"_total": cell["總計"], "_HKG": cell.get("香港")}
            for e in ECON:
                if e["mof"]:
                    d[e["id"]] = cell[e["mof"]]
            d["EU27"] = sum(cell[m["mof"]] for m in S["eu27"])
            out[flow if y == BASE else flow + "2025"] = d
    out["source"] = source("mof", "Ministry of Finance (Taiwan), trade statistics by country, annual (US$ thousand)",
                           "https://service.mof.gov.tw/public/data/statistic/trade/u2010ex.csv", "taiwan/mof_u2010ex.csv")
    return out


def russia_totals():
    url = "https://www.cbr.ru/vfs/statistics/credit_statistics/bop/bal_of_payments_standart.xlsx"
    body, meta = CACHE.get(url, "russia/bal_of_payments_standart.xlsx")
    rows = sheet_rows(xlsx_sheets(need(body, meta, "Bank of Russia BOP"))["Годы"])
    years = rows[4]
    col = next(i for i, y in enumerate(years) if (isinstance(y, float) and int(y) == BASE) or (isinstance(y, str) and y.strip().startswith(str(BASE))))
    src = source("cbr", "Bank of Russia, balance of payments of the Russian Federation, standard components (annual, US$ million)", url,
                 "russia/bal_of_payments_standart.xlsx")
    return {"exports": rows[12][col] / 1000, "imports": rows[13][col] / 1000, "source": src,
            "labels": [str(rows[12][0]).strip(), str(rows[13][0]).strip()]}


def intra_eu_exports():
    """Intra-EU goods exports, 2024: the 27 members' exports to the EU (IMTS, counterpart G998), summed: (stored value, raw sum)."""
    members = "+".join(EU)
    url = f"https://api.imf.org/external/sdmx/2.1/data/IMF.STA,IMTS/{members}.XG_FOB_USD.G998.A?startPeriod={BASE}&endPeriod={BASE}&detail=dataonly"
    rows, meta = sdmx_csv(url, "imf/imts_intra_eu.csv")
    vals = {r["COUNTRY"]: float(r["OBS_VALUE"]) / 1e9 for r in rows if r["OBS_VALUE"]}
    missing = [m for m in EU if m not in vals]
    if missing:
        fallback(f"intra-EU exports: no IMTS row for {missing}; the sum leaves them out")
    return val(sum(vals.values()), BASE, SOURCES["imts"]["name"], url, field=f"sum over the 27 members of XG_FOB_USD to G998, {BASE}",
               unit="US$ bn", d=1, note="computed; the EU's internal trade, a label on the EU block"), sum(vals.values())


def us_services():
    page = "https://www.bea.gov/data/intl-trade-investment/international-trade-goods-and-services"
    body, meta = CACHE.get(page, "bea/trade_page.html")
    link = re.search(r'href="(/sites/default/files/[^"]+geo-time-series\.xlsx)"', need(body, meta, "BEA trade page").decode("utf-8", "replace"))
    url = "https://www.bea.gov" + link.group(1)
    b, m = CACHE.get(url, "bea/trad-geo-time-series.xlsx")
    sh = xlsx_sheets(need(b, m, "BEA geographic time series"))
    src = source("bea_services", "BEA, U.S. International Trade in Goods and Services by selected countries and areas, Tables 7-8 (services, US$ million)", url,
                 "bea/trad-geo-time-series.xlsx")
    out = {"source": src, "url": url, "year": BASE, "unit": "US$ bn", "partners": {},
           "note": "US services trade by partner (hollow lanes, '+ services'); services between other pairs were not researched"}
    for tname, key in (("Table 7", "exports"), ("Table 8", "imports")):
        rows = sheet_rows(sh[tname])
        h = next(i for i, r in enumerate(rows) if r and r[0] == "Period")
        head = [str(x).strip() if x is not None else "" for x in rows[h]]
        row = next(r for r in rows[h:] if r and r[0] == float(BASE))
        for e in ECON:
            if e["bea"]:
                c = head.index(e["bea"]) if e["bea"] in head else None
                v = row[c] if c is not None and isinstance(row[c], float) else None
                out["partners"].setdefault(e["id"], {})[key] = rnd(None if v is None else v / 1000, 2)
        other = head.index("All other countries")
        tot = sum(row[i] for i in range(1, other + 1) if isinstance(row[i], float))
        out[key + "Total"] = rnd(tot / 1000, 2)
        out[key + "TotalField"] = f"{tname}: the {other - 1} countries plus 'All other countries' (the area columns after it are memo aggregates), {BASE}"
    out["partners"]["RUS"] = {"exports": None, "imports": None, "note": "BEA does not show Russia separately"}
    return out


def bilateral(imts, cen):
    """Annual series 2015-2025 for the conflict pairs: Census (US pairs), Eurostat (EU pairs, EUR), IMTS (rerouting)."""
    years = list(range(2015, 2026))
    out = {"years": years, "unit": "US$ bn unless stated", "pairs": {}}
    for p in ("CHN", "RUS", "CAN", "MEX", "IND", "BRA", "EU27", "JPN", "KOR", "TWN"):
        c = cen[p][0]
        out["pairs"][f"USA-{p}"] = {"usExports": [rnd(c[y][0] / 1000, 2) if y in c else None for y in years],
                                    "usImports": [rnd(c[y][1] / 1000, 2) if y in c else None for y in years],
                                    "source": SOURCES["census"]["name"], "url": f"https://www.census.gov/foreign-trade/balance/c{BY[p]['census']}.html",
                                    "field": "TOTAL yyyy rows (exports, general imports; US$ million)"}
    c, curl = census("5070")
    out["pairs"]["USA-IRN"] = {"usExports": [rnd(c[y][0] / 1000, 3) if y in c else None for y in years],
                               "usImports": [rnd(c[y][1] / 1000, 3) if y in c else None for y in years],
                               "source": SOURCES["census"]["name"], "url": curl, "field": "TOTAL yyyy rows"}
    # EU pairs from Eurostat (monthly, summed to years), EUR million
    q = ("ext_st_eu27_2020sitc?format=JSON&lang=EN&freq=M&indic_et=TRD_VAL&sitc06=TOTAL&geo=EU27_2020&partner=RU&partner=CN_X_HK"
         "&partner=US&partner=IR&stk_flow=EXP&stk_flow=IMP&sinceTimePeriod=2015-01&untilTimePeriod=2025-12")
    ids, d, url, upd = eurostat("ext_st_eu27_2020sitc", q)
    ip = ids.index("partner")
    iflow = ids.index("stk_flow")
    it = ids.index("time")
    sums, months = {}, {}
    for k, v in d.items():
        y = int(k[it][:4])
        key = (k[ip], k[iflow], y)
        sums[key] = sums.get(key, 0.0) + v
        months[key] = months.get(key, 0) + 1
    esrc = source("estat_trade", f"Eurostat ext_st_eu27_2020sitc, EU27 trade by partner, monthly summed to years (updated {upd})", url,
                  "eurostat/ext_st_eu27_2020sitc.json")
    for code, pid in (("RU", "RUS"), ("CN_X_HK", "CHN"), ("US", "USA"), ("IR", "IRN")):
        series = {}
        for flow, key in (("EXP", "euExports"), ("IMP", "euImports")):
            series[key] = [rnd(sums[(code, flow, y)] / 1000, 2) if months.get((code, flow, y)) == 12 else None for y in years]
        out["pairs"][f"EU27-{pid}"] = dict(series, unit="EUR bn", source=esrc, url=url, field=f"partner={code}, SITC total, 12 months summed")
    # rerouting of Russia's trade (importers' records, IMTS c.i.f.)
    for rep in ("IND", "CHN", "JPN", "KOR", "GBR", "CAN"):
        out["pairs"][f"{rep}-RUS"] = {"importsFromRussia": [rnd(imts.get((BY[rep]["imts"], "MG_CIF_USD", "RUS", y)), 2) for y in years],
                                      "source": SOURCES["imts"]["name"], "field": f"{BY[rep]['imts']}.MG_CIF_USD.RUS.A"}
    out["pairs"]["EU27-RUS"]["importsUsd"] = [rnd(imts.get(("G998", "MG_CIF_USD", "RUS", y)), 2) for y in years]
    return out


def chip_tools():
    """US exports to China of chip-making machines (HS 8486), 2021 and 2025: UN Comtrade public preview."""
    out = {}
    for y in (2021, 2025):
        url = f"https://comtradeapi.un.org/public/v1/preview/C/A/HS?reporterCode=842&period={y}&partnerCode=156&cmdCode=8486&flowCode=X"
        body, meta = CACHE.get(url, f"comtrade/usa_chn_8486_{y}.json")
        if body is None:
            fallback(f"Comtrade HS 8486 {y}: {meta.get('error')}")
            continue
        d = json.loads(body)
        rows = [r for r in d.get("data", []) if r.get("motCode") in (0, None) and r.get("partner2Code") in (0, None)
                and r.get("customsCode") in ("C00", None)]
        if rows:
            out[y] = val(rows[0]["primaryValue"] / 1e9, y, "UN Comtrade public preview (US-reported exports, HS 8486)", url,
                         field="primaryValue (US$)", unit="US$ bn", d=2)
    return out

# ----------------------------------------------------------------------------------------------------------- conflict


def piie():
    url = "https://www.piie.com/sites/default/files/2023-04/us-china-trade-war-tariffs.xlsx"
    body, meta = CACHE.get(url, "piie/us-china-trade-war-tariffs.xlsx")
    sh = xlsx_sheets(need(body, meta, "PIIE workbook"))
    rows = sheet_rows(sh["Panel a"])
    h = next(i for i, r in enumerate(rows) if r and r[0] == "Tariff action")
    src = source("piie", "PIIE, Chad P. Bown, US-China Trade War Tariffs: An Up-to-Date Chart (workbook, sheet 'Panel a')", url,
                 "piie/us-china-trade-war-tariffs.xlsx")
    series = []
    lines = verify.piie_text(sh).splitlines()     # the gate reads the workbook in this same rendering
    for r in rows[h + 1:]:
        if len(r) > 5 and isinstance(r[1], float):
            d = (datetime.date(1899, 12, 30) + datetime.timedelta(days=int(r[1]))).isoformat()
            text = [verify.cell_text(x) for x in r[:6]]
            line = next(l for l in lines if l.startswith(text[0] + " | " + d + " |"))
            n = sum(1 for s in series if s["date"] == d)
            series.append({"date": d, "action": r[0], "chinaOnRow": rnd(r[2], 2), "chinaOnUs": rnd(r[3], 2),
                           "usOnChina": rnd(r[4], 2), "usOnRow": rnd(r[5], 2), "_row": line, "_text": text,
                           "_id": f"piie_{d}" + (f"_{n + 1}" if n else "")})
    return {"source": src, "url": url, "chart": "https://www.piie.com/research/piie-charts/us-china-trade-war-tariffs-date-chart",
            "unit": "% (trade-weighted average)", "caveat": "statutory rates on fixed 2017 trade, not duties collected",
            "columns": ["chinaOnRow", "chinaOnUs", "usOnChina", "usOnRow"], "series": series}

# ---------------------------------------------------------------------------------------------------------- keystones


def computed_share(c, holder, numerator):
    return {"id": holder, "value": rnd(numerator / c["computed"]["denominator"] * 100, 2),
            "computed": f"{numerator:,} / {c['computed']['denominator']:,} {c['computed']['unit']}"}


def build_chokepoints(E, quote_checks):
    out = []
    for c in S["chokepoints"]:
        holders = []
        if c.get("holders"):
            holders = [dict(h) for h in c["holders"]]
        if c.get("computed"):
            comp = c["computed"]
            for h in comp.get("holders") or [{"id": comp["holder"], "numerator": comp["numerator"]}]:
                holders.append(computed_share(c, h["id"], h["numerator"]))
        values = [h.get("valueText") for h in holders if h.get("valueText")]
        if c.get("computed"):
            comp = c["computed"]
            # the numbers read in the document: the denominator and each numerator (or, for a sum, its parts)
            nums = [comp["denominator"]]
            for h in comp.get("holders") or [{"numerator": comp.get("numerator")}]:
                nums += h.get("parts") or [h.get("numerator")]
            values += [f"{n:,}" if isinstance(n, int) else str(n) for n in nums if n is not None]
        rec = quote_check(c["url"], c["quotes"], values, cache_name=c.get("cache"))
        quote_checks.append((c["id"], rec))
        out.append({"id": c["id"], "keystone": c["keystone"], "scope": c["scope"], "year": c["year"], "holders": holders,
                    "source": c["source"], "url": c["url"], "quote": " … ".join(c["quotes"]), "note": c.get("note"),
                    "quoteCheck": rec})
    # crude output: EIA (computed) and AI compute: Epoch (computed)
    crude = sorted(((i, E[i]["crudeShare"]["value"]) for i in IDS13 if E[i]["crudeShare"]["value"]), key=lambda x: -x[1])[:3]
    out.append({"id": "crude_output", "keystone": "crude output", "scope": "share of world crude oil production (incl. lease condensate)",
                "year": BASE, "holders": [{"id": i, "value": v} for i, v in crude], "source": SOURCES["eia_intl"]["name"],
                "url": "https://www.eia.gov/opendata/bulk/INTL.zip", "field": "INTL.57-1-<ISO3>-TBPD.A / INTL.57-1-WORL-TBPD.A"})
    out.append(epoch())
    return out


EPOCH_NAMES = {"United States of America": "USA", "China": "CHN", "Japan": "JPN", "India": "IND", "United Kingdom": "GBR",
               "Russia": "RUS", "Canada": "CAN", "Mexico": "MEX", "South Korea": "KOR", "Taiwan": "TWN", "Saudi Arabia": "SAU",
               "Brazil": "BRA"}
EU_NAMES = {"Austria", "Belgium", "Bulgaria", "Croatia", "Cyprus", "Czechia", "Czech Republic", "Denmark", "Estonia", "Finland",
            "France", "Germany", "Greece", "Hungary", "Ireland", "Italy", "Latvia", "Lithuania", "Luxembourg", "Malta", "Netherlands",
            "Poland", "Portugal", "Romania", "Slovakia", "Slovenia", "Spain", "Sweden"}


def epoch():
    e = S["epoch"]
    body, meta = CACHE.get(e["url"], "epoch/gpu_clusters.csv")
    rows = list(csv.DictReader(io.StringIO(need(body, meta, "Epoch GPU clusters").decode("utf-8-sig"))))
    cut = e["cutoff"]
    op = [r for r in rows if r["First Operational Date"] and r["First Operational Date"][:10] <= cut]   # names repeat ('Anonymized Chinese System')
    names = {r["Name"] for r in op}
    tot, by, dropped = 0.0, {}, 0
    for r in op:
        sup = (r.get("Superseded by") or "").strip()
        if sup and sup in names:
            dropped += 1
            continue
        dec = (r.get("Decommissioned Date (if applicable)") or "").strip()
        if dec and dec[:10] <= cut:
            continue
        try:
            h = float(r["H100 equivalents"])
        except ValueError:
            continue
        tot += h
        c = r["Country"].strip()
        key = EPOCH_NAMES.get(c) or ("EU27" if c in EU_NAMES else None)
        if key:
            by[key] = by.get(key, 0.0) + h
    src = source("epoch", "Epoch AI, GPU clusters dataset (CSV)", e["url"], "epoch/gpu_clusters.csv")
    holders = [{"id": k, "value": rnd(v / tot * 100, 2)} for k, v in sorted(by.items(), key=lambda x: -x[1])[:5]]
    print(f"EPOCH end-2024: {len(op)} clusters operational, {dropped} superseded; " + ", ".join(f"{h['id']} {h['value']:.2f}%" for h in holders))
    return {"id": "ai_compute", "keystone": "AI compute", "scope": "share of H100-equivalents in known GPU clusters operational by the end of 2024",
            "year": 2024, "holders": holders, "source": src, "url": e["url"], "rule": e["rule"],
            "field": f"sum of 'H100 equivalents' by 'Country' ({tot:,.0f} H100e in all, clusters with no country included in the total)"}


def quote_check(url, quotes, values, cache_name=None, item=None):
    """A hand-read value's own check: its quotes are verbatim in the cached document and its values appear in them."""
    it = dict(item or {}, url=url)
    if cache_name:
        it["cacheName"] = cache_name
    texts, meta, name = verify.source_text(CACHE, it)
    found = [any(norm(q) in t for t in texts) for q in quotes]
    joined = " … ".join(norm(q) for q in quotes)
    missing = [v for v in values if not verify.contains_value(joined, v)]
    return {"quotesFound": f"{sum(found)}/{len(found)}", "ok": bool(texts) and all(found) and not missing,
            "fetched": meta.get("fetched"), **({"valuesMissing": missing} if missing else {})}

# --------------------------------------------------------------------------------------------------------- democracy


def build_democracy(E):
    out = {}
    for slug, col in (("electoral-democracy-index", "electdem_vdem__estimate_best"), ("political-regime", "regime_row_owid")):
        url = f"https://ourworldindata.org/grapher/{slug}.csv?v=1&csvType=full&useColumnShortNames=true"
        body, meta = CACHE.get(url, f"owid/{slug}.csv")
        mb, mm = CACHE.get(f"https://ourworldindata.org/grapher/{slug}.metadata.json?v=1&csvType=full&useColumnShortNames=true", f"owid/{slug}.meta.json")
        md = json.loads(need(mb, mm, f"OWID {slug} metadata"))["columns"][col]
        series = {}
        for r in csv.DictReader(io.StringIO(need(body, meta, f"OWID {slug}").decode("utf-8-sig"))):
            if r.get(col) not in (None, ""):
                series.setdefault(r["code"], {})[int(r["year"])] = float(r[col])
        out[slug] = (series, md, url)
    edi, emd, eurl = out["electoral-democracy-index"]
    row, rmd, rurl = out["political-regime"]
    yr = 2025
    pop, upd, _ = wdi("SP.POP.TOTL", EU)
    src_e = source("vdem", f"{emd.get('citationShort', 'V-Dem')}: V-Dem v16 electoral democracy index (v2x_polyarchy)", eurl, "owid/electoral-democracy-index.csv")
    src_r = source("row", f"{rmd.get('citationShort', 'V-Dem')}: Regimes of the World (Lührmann et al. 2018), 0 closed autocracy - 3 liberal democracy", rurl, "owid/political-regime.csv")
    classes = ["closed autocracy", "electoral autocracy", "electoral democracy", "liberal democracy"]
    for eid in IDS13 + ["DEU"]:
        if eid == "EU27":
            w = {m: latest(pop.get(m), yr, 1)[1] for m in EU}
            tw = sum(v for v in w.values() if v)
            mean = sum(edi[m][yr] * w[m] for m in EU if m in edi and yr in edi[m] and w[m]) / tw
            dist = {}
            for m in EU:
                if m in row and yr in row[m] and w[m]:
                    k = classes[int(row[m][yr])]
                    dist[k] = dist.get(k, 0.0) + w[m] / tw * 100
            E[eid]["democracy"] = {"edi": val(mean, yr, src_e, eurl, field=f"population-weighted mean of the 27 members' {yr} index (WDI population)",
                                              unit="0-1", d=3, note="EU27: population-weighted mean of its members, labelled"),
                                   "regime": {"value": None, "year": yr, "source": src_r, "url": rurl,
                                              "populationShares": {k: rnd(v, 1) for k, v in sorted(dist.items())},
                                              "note": "EU27: the share of its population living under each class"}}
            continue
        code = BY[eid]["owid"]
        e = edi.get(code, {}).get(yr)
        r = row.get(code, {}).get(yr)
        E[eid]["democracy"] = {"edi": val(e, yr, src_e, f"https://ourworldindata.org/grapher/electoral-democracy-index?country={code}",
                                          field=f"electdem_vdem__estimate_best {code} {yr}", unit="0-1", d=3),
                               "regime": val(None if r is None else int(r), yr, src_r, f"https://ourworldindata.org/grapher/political-regime?country={code}",
                                             field=f"regime_row_owid {code} {yr}", label=None if r is None else classes[int(r)])}

# -------------------------------------------------------------------------------------------------------------- order


def build_order(E):
    url = "https://naciscdn.org/naturalearth/10m/cultural/ne_10m_populated_places_simple.zip"
    body, meta = CACHE.get(url, "naturalearth/ne_10m_populated_places_simple.zip")
    z = zipfile.ZipFile(io.BytesIO(need(body, meta, "Natural Earth populated places")))
    dbf = z.read(next(n for n in z.namelist() if n.endswith(".dbf")))
    n, hlen, rlen = struct.unpack("<IHH", dbf[4:12])
    fields, pos = [], 32
    while dbf[pos] != 0x0D:
        fields.append((dbf[pos:pos + 11].split(b"\0")[0].decode(), dbf[pos + 16]))
        pos += 32
    places = []
    for i in range(n):
        r = dbf[hlen + i * rlen: hlen + (i + 1) * rlen]
        if r[:1] == b"*":
            continue
        off, rec = 1, {}
        for name, ln in fields:
            rec[name] = r[off:off + ln].decode("utf-8", "replace").strip()
            off += ln
        places.append(rec)
    src = source("naturalearth", "Natural Earth 1:10m populated places (simple), pop_max", url, "naturalearth/ne_10m_populated_places_simple.zip")

    def centroid(codes):
        x = y = zz = w = 0.0
        k = 0
        for p in places:
            if p["adm0_a3"] in codes:
                pm = float(p["pop_max"] or 0)
                if pm <= 0:
                    continue
                la, lo = math.radians(float(p["latitude"])), math.radians(float(p["longitude"]))
                x += pm * math.cos(la) * math.cos(lo)
                y += pm * math.cos(la) * math.sin(lo)
                zz += pm * math.sin(la)
                w += pm
                k += 1
        return math.degrees(math.atan2(y, x)), math.degrees(math.atan2(zz, math.hypot(x, y))), k, w
    lon = {}
    for eid in IDS13 + ["DEU"]:
        codes = set(EU) if eid == "EU27" else set(BY[eid]["ne"])
        lo, la, k, w = centroid(codes)
        lon[eid] = lo
        E[eid]["centroid"] = val(lo, 2022, src, url, field=f"pop_max-weighted centroid of {k} places (adm0_a3 in {', '.join(sorted(codes)) if len(codes) < 4 else 'the 27 members'})",
                                 unit="degrees east", d=3, latitude=rnd(la, 3), places=k,
                                 note="the population-weighted centroid on the sphere (unit vectors), so the antimeridian does not distort it")
    order = sorted(IDS13, key=lambda i: lon[i]) + ["ROW"]
    print("ORDER west to east (population-weighted centroids of Natural Earth populated places): " + " ".join(order[:-1])
          + "  [" + ", ".join(f"{i} {lon[i]:.1f}" for i in order[:-1]) + "]")
    expected = ["MEX", "USA", "CAN", "BRA", "GBR", "EU27", "SAU|RUS", "SAU|RUS", "IND", "CHN", "TWN", "KOR", "JPN"]
    same = all(i in e.split("|") for i, e in zip(order, expected))
    print(f"  expected MEX USA CAN BRA GBR EU27 SAU/RUS IND CHN TWN KOR JPN: {'matches' if same else 'DIFFERS (the computed order is used)'}")
    return {"westToEast": order, "rule": "west → east by where people live: the population-weighted centroid longitude of each economy's "
                                        "populated places (Natural Earth, pop_max); the rest of the world closes the row at the east end",
            "lon": {i: rnd(lon[i], 3) for i in order[:-1]}, "source": src, "url": url}

# ------------------------------------------------------------------------------------------------------------ history


def build_history(E):
    body, meta = CACHE.get("https://dataverse.nl/api/access/datafile/421302", "maddison/mpd2023_web.xlsx")
    sh = xlsx_sheets(need(body, meta, "Maddison Project 2023"))
    gdp = {}
    for r in sheet_rows(sh["Full data"])[1:]:
        if r[3] is not None and 1950 <= r[3] <= 1991 and r[4] is not None and r[5] is not None:
            gdp.setdefault(int(r[3]), {})[r[0]] = r[4] * r[5]     # GDP per capita (2011 int'l $) × population (thousands)
    reg = sheet_rows(sh["Regional data"])
    mad_url = "https://dataverse.nl/api/access/datafile/421302"
    msrc = source("maddison", "Maddison Project Database 2023 (Bolt and van Zanden 2024), GDP per capita × population, 2011 international $",
                  "https://doi.org/10.34894/INZBF2", "maddison/mpd2023_web.xlsx")
    unions = S["maddisonUnions"]
    successors = {c for k in ("SUN", "CSK", "YUG") for c in unions[k]}
    pppsh, ngdpd = imf("PPPSH"), imf("NGDPD")
    groups = {"ADVEC", "AFQ", "SSQ", "AS5", "APQ", "AZQ", "CBQ", "CMQ", "CAQ", "EAQ", "EEQ", "DA", "EDE", "OEMDC", "EUQ", "EU", "WE", "MAE",
              "MEQ", "MECA", "NAQ", "NMQ", "OAE", "PIQ", "SMQ", "SAQ", "SEQ", "SSA", "WEQ", "WHQ", "WEOWORLD", "EURO"}
    bands = IDS13 + ["ROW"]
    years = list(range(1950, BASE + 1))
    ppp = {b: [] for b in bands}
    maxdev, wsum_dev, ussr = 0.0, [], {}
    bench = []
    for y in years:
        if y < 1980:
            g = gdp[y]
            world = sum(v for c, v in g.items() if c not in successors)
            sh_ = {}
            for b in IDS13:
                if b == "EU27":
                    s = sum(g.get(m, 0) for m in EU if m not in successors) + g.get("CSK", 0)
                elif b == "RUS":
                    s = g.get("SUN", 0)
                else:
                    s = g.get(BY[b]["maddison"][0], 0)
                sh_[b] = s / world * 100
            sh_["ROW"] = 100 - sum(sh_[b] for b in IDS13)
            ussr[y] = sh_["RUS"]
            r = next((x for x in reg if x[0] == float(y)), None)
            if r and r[9] and r[19]:
                bench.append((y, world / (r[9] * r[19]) - 1))
        else:
            ys = str(y)
            countries = {c: s[y] for c, s in pppsh.items() if c not in groups and y in s}
            total = sum(countries.values())
            wsum_dev.append(abs(total - 100))
            sh_ = {}
            for b in IDS13:
                if b == "EU27":
                    sh_[b] = sum(countries.get(m, 0) for m in EU)
                else:
                    sh_[b] = countries.get(BY[b]["imf"], 0)
            sh_["ROW"] = sum(v for c, v in countries.items() if c not in {BY[b]["imf"] for b in IDS13 if b != "EU27"} and c not in EU)
            if y <= 1991:     # the IMF's world has no USSR before 1992: add Maddison's USSR share, rescale the rest
                g = gdp[y]
                world = sum(v for c, v in g.items() if c not in successors)
                u = g["SUN"] / world * 100
                ussr[y] = u
                scale = (100 - u) / total
                for b in bands:
                    sh_[b] = sh_[b] * scale
                sh_["RUS"] = u
        for b in bands:
            ppp[b].append(rnd(sh_[b], 3))
        maxdev = max(maxdev, abs(sum(sh_[b] for b in bands) - 100))
    # market exchange rates: WEO NGDPD shares of WEOWORLD, 1980-2024
    myears = list(range(1980, BASE + 1))
    mkt = {b: [] for b in bands}
    mdev = 0.0
    for y in myears:
        w = ngdpd["WEOWORLD"][y]
        sh_ = {}
        for b in IDS13:
            if b == "EU27":
                sh_[b] = sum(ngdpd.get(m, {}).get(y, 0) for m in EU) / w * 100
            else:
                sh_[b] = ngdpd.get(BY[b]["imf"], {}).get(y, 0) / w * 100
        sh_["ROW"] = 100 - sum(sh_.values())
        for b in bands:
            mkt[b].append(rnd(sh_[b], 3))
        mdev = max(mdev, abs(sum(sh_.values()) - 100))
    eu_weo = pppsh["EU"][BASE]
    eu_sum = ppp["EU27"][-1]
    check("history shares sum to 100 (within 0.5 per year)", maxdev <= 0.5 and max(wsum_dev) <= 0.5,
          f"PPP wall 1950-{BASE}: bands sum to 100 within {maxdev:.4f} every year; WEO's country PPP shares sum to 100 within "
          f"{max(wsum_dev):.3f} (1980-{BASE}); Maddison's summed world against its own regional totals at benchmark years: "
          + ", ".join(f"{y} {d * 100:+.1f}%" for y, d in bench[:4]) + f"; EU27 as the sum of members {eu_sum:.3f} vs WEO's EU aggregate {eu_weo:.3f} ({BASE})")
    assume(f"the PPP wall 1980-1991: the IMF's world has no USSR before 1992, so the USSR's share is Maddison's (same year, "
           f"{ussr[1980]:.1f}% in 1980, {ussr[1991]:.1f}% in 1991) and every IMF share is scaled by (100 - USSR) / (sum of IMF shares)")
    i79, i80 = years.index(1979), years.index(1980)
    jumps = sorted(((b, ppp[b][i80] - ppp[b][i79]) for b in bands), key=lambda x: -abs(x[1]))[:4]
    print("SPLICE at 1980 (Maddison 1979 → IMF 1980, both PPP; not smoothed): " + ", ".join(f"{b} {ppp[b][i79]:.1f} → {ppp[b][i80]:.1f}" for b, _ in jumps))
    print(f"HISTORY at PPP: US {ppp['USA'][0]:.1f}% (1950) → {ppp['USA'][-1]:.1f}% ({BASE}); China {ppp['CHN'][0]:.1f}% → {ppp['CHN'][-1]:.1f}%; "
          f"USSR/Russia {ppp['RUS'][0]:.1f}% → {ppp['RUS'][-1]:.1f}%; market rates {BASE}: US {mkt['USA'][-1]:.1f}%, China {mkt['CHN'][-1]:.1f}%")
    return {
        "bands": bands, "stackOrder": "west to east from the bottom, the rest of the world on top (WorldLayout applies the computed order)",
        "ppp": {"years": years, "shares": ppp, "unit": "% of world GDP at purchasing power parity",
                "splice": {"year": 1980, "label": "source changes: Maddison → IMF (both at purchasing power)",
                           "jump": {b: [ppp[b][i79], ppp[b][i80]] for b in bands},
                           "note": "the two sources use different purchasing-power benchmarks (Maddison: 2011 international $; IMF: the latest ICP round), so bands step at 1980; not smoothed"},
                "sources": [{"years": "1950-1979", "source": msrc, "url": mad_url,
                             "field": "'Full data' gdppc × pop; world = the sum of every unit (USSR, Czechoslovakia and Yugoslavia as units, their successors left out)"},
                            {"years": f"1980-{BASE}", "source": SOURCES["weo"]["name"], "url": IMF_API.format("PPPSH"),
                             "field": "PPPSH by country; EU27 = the sum of its members; ROW = the sum of every other country"}],
                "ussrSeam": {"band": "RUS", "label": "USSR until 1991, Russia from 1992",
                             "modelled": {"years": "1980-1991", "note": ASSUMED[-1]}},
                "euNote": "EU27 = the sum of the members with a series that year: Czechoslovakia counts in full before 1980; the Baltic states are inside the USSR "
                          "and Croatia and Slovenia inside Yugoslavia (rest of the world) until the IMF gives them their own series"},
        "market": {"years": myears, "shares": mkt, "unit": "% of world GDP at market exchange rates",
                   "source": SOURCES["weo"]["name"], "url": IMF_API.format("NGDPD"), "field": "NGDPD by country / NGDPD.WEOWORLD; ROW a residual",
                   "before1980": "no market-rate series here (dashed)",
                   "note": "the IMF's world has no USSR before 1992 at market rates either; the Russia band starts in 1992"},
        "caption": {"pppUS": E["USA"]["sharePpp"]["value"], "pppCHN": E["CHN"]["sharePpp"]["value"],
                    "marketUS": E["USA"]["shareWorld"]["value"], "marketCHN": E["CHN"]["shareWorld"]["value"], "year": BASE}}

# --------------------------------------------------------------------------------------------------------------- gate


def gate_items(fin, tariffs, asof_day):
    """Every item the gate checks: the 2026 values (sources.json, TIC, PIIE, COFER) and the dated events."""
    items = []
    for v in S["values2026"]:
        items.append(dict(v, kind="value", dated2026=True, date=v.get("date")))
    # TIC, end-Aug 2026: one item per holder, the quote being the holder's own row in Table 3
    for t in fin["ticRows"]:
        items.append(t)
    # PIIE 2026 rows
    for r in tariffs["series"]:
        if r["date"].startswith("2026"):
            row, tx = r["_row"], r["_text"]
            items.append({"id": r["_id"],
                          "kind": "value", "group": "tariffs", "dated2026": True, "date": r["date"],
                          "label": f"PIIE average tariffs on {r['date']}: US on China {r['usOnChina']}%, US on the rest {r['usOnRow']}%, China on the US {r['chinaOnUs']}%",
                          "claim": f"PIIE's US-China tariff data give, for the row dated {r['date']}: US tariffs on Chinese exports {tx[4]}, "
                                   f"US tariffs on rest-of-world exports {tx[5]}, Chinese tariffs on US exports {tx[3]} (trade-weighted averages, percent).",
                          "url": tariffs["url"], "cacheName": "piie/us-china-trade-war-tariffs.xlsx", "textKind": "piie", "tight": True,
                          "context": ["Tariff action | Date | Chinese tariffs on ROW exports | Chinese tariffs on US exports | US tariffs on Chinese exports | US tariffs on ROW exports"],
                          "quotes": [row], "values": [tx[4], tx[5], tx[3]],
                          "value": {"usOnChina": r["usOnChina"], "usOnRow": r["usOnRow"], "chinaOnUs": r["chinaOnUs"]}})
    items.extend(fin["coferItems"])
    for e in S["events"]:
        it = {k: v for k, v in e.items() if k not in ("evidence", "outcome")}
        it.update(kind="event", label=e["name"])
        dates = [e.get("start"), e.get("end"), e.get("expires")] + [d for d in e.get("dates2026", [])]
        it["dated2026"] = any(isinstance(d, str) and d.startswith("2026") for d in dates) or bool(e.get("dated2026"))
        it["date"] = e.get("start")
        items.append(it)
        # the event's further sources (a start or expiry date, a scope, a later status) and its recorded outcome: gated the
        # same way, re-read with the event (a 2026 event's sources on the as-of date)
        for ev in e.get("evidence") or []:
            items.append(dict(ev, id=f"{e['id']}.{ev['role']}", kind="evidence", parent=e["id"], dated2026=it["dated2026"],
                              date=ev.get("date") or e.get("start"), label=f"{e['name']}: {ev['role']}" + (f" ({ev['label']})" if ev.get("label") else "")))
        if e.get("outcome"):
            oc = e["outcome"]
            items.append(dict({k: v for k, v in oc.items() if k != "text"}, id=f"{e['id']}.outcome", kind="outcome", parent=e["id"],
                              dated2026=it["dated2026"], date=oc.get("date") or e.get("end") or e.get("expires"),
                              label=f"{e['name']}: outcome ({oc['text'][:60]})"))
    return items


MONTHS = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"]
WEEKDAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"]


def brent_daily_check(items):
    """EIA's daily Brent table read cell by cell (the gate's own copy): every daily 2026 value sits on its stated date, and the
    stated 2026 high is the largest 2026 price in the table. The weekday comes from the cell's column, not from Jev."""
    daily = [i for i in items if i.get("group") == "oil" and (i.get("url") or "").endswith("RBRTED.htm")]
    if not daily:
        return
    body, meta, _ = verify.fetch_document(CACHE, dict(daily[0], refreshBefore=None))
    if body is None:
        check("Brent daily values on their dates", False, f"the EIA daily table is not cached ({meta.get('error')})")
        return
    mon = {m[:3]: k + 1 for k, m in enumerate(MONTHS)}
    days = {}
    # each week's row: its label, then exactly five cells, Monday to Friday (an empty cell: no price that day)
    for m in re.finditer(r"(\d{4}) ([A-Z][a-z]{2})- ?(\d{1,2}) to [A-Z][a-z]{2}- ?\d{1,2} \|((?: [^|]*\|){5})", norm(verify.html_cells(body))):
        start = datetime.date(int(m.group(1)), mon[m.group(2)], int(m.group(3)))
        for k, c in enumerate(x.strip() for x in m.group(4).split("|")[:5]):
            if c:
                days[(start + datetime.timedelta(days=k)).isoformat()] = float(c)
    y26 = {d: v for d, v in days.items() if d.startswith("2026")}
    top = max(y26, key=lambda d: (y26[d], d))
    ok, parts = bool(y26), []
    for it in daily:
        v = days.get(it["date"])
        ok = ok and v is not None and abs(v - it["value"]) < 1e-9
        parts.append(f"{it['date']} {WEEKDAYS[datetime.date.fromisoformat(it['date']).weekday()]} {v}")
    high = next((it for it in daily if it["id"] == "brent_2026_high"), None)
    ok = ok and high is not None and high["date"] == top and abs(high["value"] - y26[top]) < 1e-9
    check("Brent daily values on their dates", ok, "EIA's daily table read cell by cell: " + "; ".join(parts)
          + f"; the 2026 high is {y26[top]} on {top} ({WEEKDAYS[datetime.date.fromisoformat(top).weekday()]}), the largest of "
            f"{len(y26)} daily prices from {min(y26)} to {max(y26)}")


def date_stated(iso, quotes):
    """True when one of the quotes states the full date (day, month and year) in an English, ISO or Chinese form."""
    y, m, d = (int(x) for x in iso.split("-"))
    mon = MONTHS[m - 1]
    forms = [f"{mon} {d}, {y}", f"{d} {mon} {y}", f"{mon[:3]}. {d}, {y}", f"{mon[:3]} {d}, {y}", f"{d} {mon[:3]} {y}", iso, f"{y}年{m}月{d}日"]
    return any(re.search(r"(?<![\d])" + re.escape(norm(f)) + r"(?![\d])", norm(q)) for f in forms for q in quotes)


def build_finance(E, asof_day):
    url3 = "https://ticdata.treasury.gov/resource-center/data-chart-center/tic/Documents/slt_table3.txt"
    body, meta = CACHE.get(url3, "tic/slt_table3.txt", refresh_before=asof_day)
    lines = need(body, meta, "TIC Table 3").decode("utf-8", "replace").splitlines()
    src = source("tic", "US Treasury, TIC, Table 3: U.S. Treasury Securities Held by Foreign Residents (US$ million)", url3, "tic/slt_table3.txt")
    rows = {}
    for l in lines:
        p = l.split("\t")
        if len(p) > 3:
            rows[(p[0].strip(), p[2].strip())] = (l, p)
    month, prev = "2026-08", "2025-08"
    items, tic = [], {}
    head = [lines[0], "Millions of dollars", "Country Country Code Date Holdings Net U.S. Sales"]
    for eid in IDS13 + ["DEU"]:
        name = BY[eid]["tic"]
        if not name:
            continue
        line, p = rows[(name, month)]
        v = float(p[3]) / 1000
        pv = float(rows[(name, prev)][1][3]) / 1000 if (name, prev) in rows else None
        tic[eid] = {"value": rnd(v, 1), "aug2025": rnd(pv, 1), "row": line}
        items.append({"id": f"tic_{eid}", "kind": "value", "group": "treasuries", "dated2026": True, "date": "2026-08-31",
                      "label": f"US Treasuries held: {BY[eid]['name']}, end-Aug 2026", "url": url3, "cacheName": "tic/slt_table3.txt",
                      "textKind": "text", "tight": True, "context": head,
                      "claim": f"TIC Table 3 records US Treasury securities held by foreign residents in '{name}' of {int(p[3]):,} million dollars at the end of August 2026 (date 2026-08).",
                      "quotes": [line], "values": [p[3]], "value": rnd(v, 1)})
    for name, key in (("Grand Total", "total"), ("Of Which: Foreign Official", "official")):
        line, p = rows[(name, month)]
        tic[key] = {"value": rnd(float(p[3]) / 1000, 1), "aug2025": rnd(float(rows[(name, prev)][1][3]) / 1000, 1), "row": line}
        items.append({"id": f"tic_{key}", "kind": "value", "group": "treasuries", "dated2026": True, "date": "2026-08-31",
                      "label": f"US Treasuries held by all foreign residents ({name}), end-Aug 2026", "url": url3, "cacheName": "tic/slt_table3.txt",
                      "textKind": "text", "tight": True, "context": head,
                      "claim": f"TIC Table 3 records US Treasury securities held by foreign residents, '{name}', of {int(p[3]):,} million dollars at the end of August 2026.",
                      "quotes": [line], "values": [p[3]], "value": tic[key]["value"]})
    # COFER: the dollar's share of allocated reserves
    curl = "https://api.imf.org/external/sdmx/2.1/data/IMF.STA,COFER/G001....Q?startPeriod=2024-Q1"
    crows, cmeta = sdmx_csv(curl, "imf/cofer.csv")
    cof = {r["TIME_PERIOD"]: r for r in crows if r["INDICATOR"] == "AFXRA" and r["FXR_CURRENCY"] == "CI_USD" and r["TYPE_OF_TRANSFORMATION"] == "SHRO_PT"}
    cols = ["COUNTRY", "INDICATOR", "FXR_CURRENCY", "TYPE_OF_TRANSFORMATION", "FREQUENCY", "TIME_PERIOD", "OBS_VALUE"]
    csrc = source("cofer", "IMF, Currency Composition of Official Foreign Exchange Reserves (IMF.STA,COFER)", curl, "imf/cofer.csv")
    q26 = max(k for k in cof if k.startswith("2026"))
    r26 = cof[q26]
    cofer = {"usdShare2024Q4": val(float(cof["2024-Q4"]["OBS_VALUE"]), "2024-Q4", csrc, curl, field="G001.AFXRA.CI_USD.SHRO_PT.Q 2024-Q4", unit="% of allocated reserves", d=2),
             "usdShareLatest": val(float(r26["OBS_VALUE"]), q26, csrc, curl, field=f"G001.AFXRA.CI_USD.SHRO_PT.Q {q26}", unit="% of allocated reserves", d=2,
                                   gateId="cofer_2026"),
             "note": "the IMF changed its method from the 2025-Q3 release (it now imputes unreported reserves); every quarter here is on the current basis"}
    citems = [{"id": "cofer_2026", "kind": "value", "group": "reserves", "dated2026": True, "date": q26,
               "label": f"US dollar's share of allocated FX reserves, {q26} (IMF COFER)", "url": curl, "cacheName": "imf/cofer.csv",
               "headers": SDMX, "textKind": "sdmx-csv", "columns": cols, "tight": True, "context": [" ".join(cols)],
               "claim": f"In the IMF's COFER data, the US dollar's share (CI_USD, percent, SHRO_PT) of the world's allocated foreign exchange reserves (AFXRA, G001) was {r26['OBS_VALUE']} in {q26}.",
               "quotes": [" ".join(r26.get(c, "") for c in cols)], "values": [r26["OBS_VALUE"]], "value": rnd(float(r26["OBS_VALUE"]), 2)}]
    return {"tic": tic, "ticRows": items, "ticSource": src, "ticUrl": url3, "cofer": cofer, "coferItems": citems}

# --------------------------------------------------------------------------------------------------------------- main


def main():
    day = asof()
    print(f"global.json build, as of {day} ({'offline, from the cache' if OFFLINE else 'online'}); sources read on {S['readOn']}")
    E = {i: {"id": i} for i in ALL}
    for e in ECON:
        E[e["id"]].update({k: e[k] for k in ("name", "short", "label") if k in e})
        if e.get("inside"):
            E[e["id"]]["inside"] = e["inside"]
    E["ROW"].update(name="Rest of the world", short="Rest of the world", label="THE REST OF THE WORLD")
    E["WLD"].update(name="World", short="World", label="WORLD")
    world, s13 = build_size(E)
    build_state(E)
    build_strata(E)
    build_trade_shares(E)
    build_military(E)
    build_external(E)
    build_energy(E)
    quote_checks = []
    trade, imts, cen, tw = build_trade(E)
    trade["bilateral"] = bilateral(imts, cen)
    trade["chipTools"] = chip_tools()
    tariffs = piie()
    chokepoints = build_chokepoints(E, quote_checks)
    straits = []
    for s in S["straits"]:
        rec = quote_check(s["url"], s["quotes"], [x for x in (s.get("valueText"), s.get("beforeText")) if x])
        quote_checks.append((s["id"], rec))
        straits.append({k: s[k] for k in s if k not in ("quotes", "valueText", "beforeText")} | {"quote": " … ".join(s["quotes"]), "quoteCheck": rec})
    build_democracy(E)
    order = build_order(E)
    for i, eid in enumerate(order["westToEast"]):
        E[eid]["order"] = i
    # the research's seed values (dataset.json) beside the refetched ones, for the record
    pairs = {"gdp": lambda i: E[i]["gdp"]["value"], "gdp_ppp": lambda i: E[i]["gdpPpp"]["value"],
             "population": lambda i: E[i]["population"]["value"], "milex_usd": lambda i: E[i]["military"]["usd"]["value"],
             "niip_usd": lambda i: E[i]["niip"]["usd"]["value"], "ca_usd": lambda i: E[i]["currentAccount"]["usd"]["value"]}
    seeds = []
    for m, get in pairs.items():
        d = [abs(get(i) / research_value(m, i) - 1) * 100 for i in IDS13 + ["DEU"] if research_value(m, i) and get(i)]
        seeds.append(f"{m} max {max(d):.2f}%")
    print("SEEDS: refetched values against the research's dataset.json (largest relative difference): " + ", ".join(seeds))
    history = build_history(E)
    fin = build_finance(E, day)
    for eid, t in fin["tic"].items():
        if eid in E:
            E[eid]["treasuries"] = val(t["value"], "2026-08", fin["ticSource"], fin["ticUrl"], field=f"Table 3 '{BY[eid]['tic']}' 2026-08 for_treas_pos",
                                       unit="US$ bn, end of month", d=1, aug2025=t["aug2025"], gateId=f"tic_{eid}",
                                       note="custodial: TIC records the custodian's country")
    for eid in ("USA",):
        E[eid]["treasuries"] = None
    # quote self-checks of hand-read values
    bad = [(k, r) for k, r in quote_checks if not r["ok"]]
    check("hand-read values quoted verbatim", not bad,
          f"{len(quote_checks) - len(bad)} of {len(quote_checks)} hand-read values have their quotes verbatim in the fetched document"
          + ("; failing: " + ", ".join(f"{k} ({r['quotesFound']}{', missing ' + ','.join(r['valuesMissing']) if r.get('valuesMissing') else ''})" for k, r in bad) if bad else ""))
    # the gate
    items = gate_items(fin, tariffs, day)
    if not OFFLINE:   # the item specs, so verify.py can re-run the gate on its own
        with open(CACHE.path("gate-items.json"), "w", encoding="utf-8") as f:
            json.dump(items, f, indent=1, ensure_ascii=False, sort_keys=True)
    results = verify.run_gate(CACHE, items, day)
    verify.report(items, results, day)
    brent_daily_check(items)
    stored = []
    for it in items:
        r = results[it["id"]]
        rec = {k: it[k] for k in ("id", "kind", "parent", "role", "group", "label", "date", "dated2026", "claim", "url", "value") if k in it}
        if it.get("fetch", {}).get("fr"):
            rec["fetchedVia"] = f"Federal Register API, document {it['fetch']['fr']} (raw text)"
        rec["quote"] = " … ".join(it.get("quotes") or []) or None
        rec.update({k: r.get(k) for k in ("status", "verifiedOn", "quoteFound", "valuesFound", "supportedJ", "reason", "fetched")})
        rec["drawn"] = r["status"] == "verified"
        stored.append(rec)
    by_id = {s["id"]: s for s in stored}
    # events: verified only when the event's own item and every further source it cites pass; an ended event (or one past
    # its expiry) is drawn only with a recorded outcome that passed the same gate (verbatim quote, Jev >= 0.6, the as-of re-read)
    events, expiry_assumed, expiry_stated = [], [], []
    for e in S["events"]:
        g = by_id[e["id"]]
        ev = {k: e[k] for k in e if k not in ("quotes", "values", "claim", "fetch", "context", "tight", "dated2026", "dates2026",
                                             "textKind", "browserAgent", "cacheName", "outcome", "evidence")}
        ev["quote"] = g["quote"]
        ev["claim"] = e.get("claim")
        if e.get("fetch", {}).get("fr"):
            ev["fetchedVia"] = g.get("fetchedVia")
        for k in ("status", "verifiedOn", "quoteFound", "supportedJ", "reason"):
            ev[k] = g.get(k)
        ev["dated2026"] = bool(g.get("dated2026"))
        sources = e.get("evidence") or []
        parts = [by_id[f"{e['id']}.{x['role']}"] for x in sources]
        if parts:
            ev["evidence"] = [dict({"role": x["role"], "url": x["url"], "quote": p["quote"], "claim": x["claim"]},
                                   **{k: p.get(k) for k in ("fetchedVia", "status", "verifiedOn", "supportedJ", "reason") if p.get(k) is not None},
                                   **({"note": x["note"]} if x.get("note") else {}))
                              for x, p in zip(sources, parts)]
            failed = [p for p in parts if p["status"] != "verified"]
            if failed:
                ev["status"] = "unverified"
                ev["reason"] = "; ".join(([ev["reason"]] if ev.get("reason") else [])
                                         + [f"its {p['id'].split('.', 1)[1]} source is unverified ({p.get('reason')})" for p in failed])
            elif ev["status"] == "verified":
                ev["verifiedOn"] = min([g["verifiedOn"]] + [p["verifiedOn"] for p in parts])
        oc, outcome_ok = e.get("outcome"), False
        if oc:
            p = by_id[f"{e['id']}.outcome"]
            outcome_ok = p["status"] == "verified"
            ev["outcome"] = dict({"text": oc["text"], "url": oc["url"], "quote": p["quote"], "claim": oc["claim"]},
                                 **{k: p.get(k) for k in ("fetchedVia", "status", "verifiedOn", "supportedJ", "reason") if p.get(k) is not None},
                                 recorded=outcome_ok)
        # an expiry date counts as stated only when a verified quote of the event (its own or a further source's) gives it in full
        if e.get("expires"):
            where = [("its own quote" if x is e else f"its {x['role']} source") for x, st in [(e, g)] + list(zip(sources, parts))
                     if st["status"] == "verified" and date_stated(e["expires"], x.get("quotes") or [])]
            ev["expiresStated"] = bool(where)
            if where:
                expiry_stated.append(f"{e['id']} {e['expires']} ({where[0]})")
            else:
                expiry_assumed.append(e["id"])
                ev["expiresNote"] = "≈ the expiry date is not stated in full in a verified quote: it is the research's date"
        ended = isinstance(e.get("end"), str) and re.match(r"\d{4}-\d{2}-\d{2}$", e["end"]) and e["end"] < day
        ev["ended"] = bool(ended)
        drawn = ev["status"] == "verified"
        why = []
        unrec = "" if not oc else f" (the recorded outcome is unverified: {ev['outcome'].get('reason')})"
        if ended and not outcome_ok:
            drawn = False
            why.append(f"its stated end date {e['end']} has passed and no verified outcome is recorded{unrec}")
        if e.get("expires") and e["expires"] < day and not outcome_ok:
            drawn = False
            why.append(f"its suspension or truce expired on {e['expires']} and no verified outcome is recorded{unrec}")
        if why:
            ev["reason"] = "; ".join(([ev["reason"]] if ev.get("reason") else []) + why)
        ev["expiresWithin30Days"] = bool(e.get("expires") and day <= e["expires"] <= (datetime.date.fromisoformat(day) + datetime.timedelta(days=30)).isoformat())
        ev["drawn"] = drawn and e.get("form") not in (None, "none")
        ev["listed"] = drawn
        events.append(ev)
        g["drawn"] = ev["drawn"]
        for p in parts + ([by_id[f"{e['id']}.outcome"]] if oc else []):
            p["drawn"] = ev["drawn"] and p["status"] == "verified"
    assume("event start and end dates are the research's event table (Tools/global/research); the gate verifies each event's quoted "
           "substance, and a date only where a verified quote states it (startNote/note say so where it matters)"
           + (f"; expiry dates not stated in full in a verified quote (marked ≈ on the event): {', '.join(expiry_assumed)}" if expiry_assumed
              else "; every expiry date is stated in full in a verified quote"))
    print("EXPIRIES stated in full in a verified quote: " + ("; ".join(expiry_stated) or "none"))
    ocs = [by_id[f"{e['id']}.outcome"] for e in S["events"] if e.get("outcome")]
    bad_oc = [p["id"] for p in ocs if p["status"] != "verified"]
    print(f"OUTCOMES: {len(ocs) - len(bad_oc)} of {len(ocs)} recorded outcomes verified (verbatim quote and Jev >= {verify.THRESHOLD})"
          + (f"; unverified: {', '.join(bad_oc)}" if bad_oc else ""))
    unverified = [s for s in stored if s["status"] != "verified"]
    expired = [e["id"] for e in events if e["ended"] and not (e.get("outcome") or {}).get("recorded")]
    forms = {}
    for e in events:
        if e["drawn"]:
            forms[e["form"]] = forms.get(e["form"], 0) + 1
    print(f"EVENTS: {len(events)} dated events; {sum(1 for e in events if e['status'] == 'verified')} verified; "
          f"{sum(1 for e in events if e['listed'])} listed (verified and not past an end or expiry date without a verified outcome); "
          f"{sum(1 for e in events if e['drawn'])} drawn ("
          + ", ".join(f"{k} {v}" for k, v in sorted(forms.items())) + "; 'tick' = the event strip only); "
          f"ended without a recorded outcome (not drawn): {', '.join(expired) if expired else 'none'}")
    for e in events:
        if e["expiresWithin30Days"]:
            print(f"  expires within 30 days: {e['id']} on {e['expires']} ({'drawn' if e['drawn'] else 'not drawn'})")
    # a drawn 2026 value must carry a verified quote on or after the as-of date
    bad26 = [s["id"] for s in stored if s.get("dated2026") and s["drawn"] and (s["status"] != "verified" or (s.get("verifiedOn") or "") < day)]
    check("every drawn 2026 item verified on or after the as-of date", not bad26,
          f"{sum(1 for s in stored if s.get('dated2026') and s['drawn'])} drawn 2026 items, all verified on {day}" if not bad26 else f"failing: {bad26}")
    # totals line
    us = E["USA"]
    totals = {"worldGdp": E["WLD"]["gdp"]["value"], "worldGdpT": rnd(E["WLD"]["gdp"]["value"] / 1000, 1),
              "the13Gdp": rnd(s13, 1), "the13ShareGdp": rnd(s13 / world * 100, 2),
              "the13SharePeople": rnd(sum(E[i]["population"]["value"] for i in IDS13) / E["WLD"]["population"]["value"] * 100, 2),
              "usShareGdp": us["shareWorld"]["value"], "usSharePeople": us["sharePeople"]["value"],
              "usShareMilitary": us["military"]["shareWorld"]["value"], "year": BASE,
              "line": f"The world economy, {BASE}: ${E['WLD']['gdp']['value'] / 1000:.1f}T · the US: {us['shareWorld']['value']:.0f}% of output, "
                      f"{us['sharePeople']['value']:.0f}% of people, {us['military']['shareWorld']['value']:.0f}% of military spending · these 13: "
                      f"{s13 / world * 100:.0f}% of output, {sum(E[i]['population']['value'] for i in IDS13) / E['WLD']['population']['value'] * 100:.0f}% of people"}
    print("TOTALS: " + totals["line"])
    finance = {"cofer": fin["cofer"], "treasuriesTotal": val(fin["tic"]["total"]["value"], "2026-08", fin["ticSource"], fin["ticUrl"],
                                                              field="Table 3 'Grand Total' 2026-08", unit="US$ bn", d=1, aug2025=fin["tic"]["total"]["aug2025"], gateId="tic_total"),
               "treasuriesOfficial": val(fin["tic"]["official"]["value"], "2026-08", fin["ticSource"], fin["ticUrl"],
                                         field="Table 3 'Of Which: Foreign Official' 2026-08", unit="US$ bn", d=1, aug2025=fin["tic"]["official"]["aug2025"], gateId="tic_official"),
               "threadsPer": 100, "note": "1 thread = $100B of US Treasuries; custodial: TIC records the custodian's country (Belgium, Luxembourg, the Cayman Islands)"}
    s301 = S.get("section301_2026") or {}
    if s301:
        rec = quote_check(s301["url"], s301["listQuotes"], [], item={"fetch": {"fr": "2026-15274"}})
        s301 = dict(s301, listCheck=rec, status=by_id["us_301_forced_labour"]["status"], drawn=by_id["us_301_forced_labour"]["status"] == "verified")
        quote_checks.append(("section301_2026.list", rec))
        check("Section 301 partner list quoted", rec["ok"], f"China, Brazil, Saudi Arabia and Russia found among the investigated economies of FR 2026-15274 ({rec['quotesFound']})")
    for r in tariffs["series"]:
        r.pop("_row", None)
        r.pop("_text", None)
        g = by_id.get(r.pop("_id"))
        if r["date"].startswith("2026"):
            r["gateId"] = g["id"] if g else None
            r["dated2026"] = True
            r["status"] = g["status"] if g else "unverified"
            r["drawn"] = bool(g and g["status"] == "verified")
    tariffs["section301_2026"] = s301

    def stamp(v, gid):
        """Carries the gate's verdict onto a stored 2026 value, so the model can check it where it is drawn."""
        g = by_id.get(gid)
        if v and g:
            v.update(status=g["status"], verifiedOn=g.get("verifiedOn"), drawn=g["status"] == "verified")
    for eid in IDS13 + ["DEU"]:
        if E[eid].get("treasuries"):
            stamp(E[eid]["treasuries"], E[eid]["treasuries"]["gateId"])
    stamp(finance["treasuriesTotal"], "tic_total")
    stamp(finance["treasuriesOfficial"], "tic_official")
    stamp(finance["cofer"]["usdShareLatest"], "cofer_2026")
    doc = {
        "note": "The world economy around the United States for the economy scene's world view (M2, 'One economy among many'): 13 economies, "
                "Germany inside the EU27 (never added to it), the rest of the world and the world, 2024 base year. Built by Tools/global/build.py "
                "from the IMF, World Bank, OECD, Eurostat, SIPRI, Taiwan DGBAS/MOF/MOEAEA/CBC, IMF IMTS, US Census, Bank of Russia, EIA, BEA, TIC, "
                "PIIE, USGS, IEA, SIA/BCG, GIIGNL, Epoch, V-Dem via OWID, Natural Earth and the Maddison Project; hand-read values and event quotes "
                "in Tools/global/sources.json. Every value carries its year, source and URL with an API field or a verbatim quote. Every 2026-dated "
                "value and every event went through the 2026 gate (Tools/global/verify.py): drawn only with a verbatim quote AND Jev's support "
                "score of 0.6 or more (model evidence, not proof); unverified items stay here with drawn = false. See Docs/data/global.md.",
        "asOf": day,
        "baseYear": BASE,
        "economyIds": ALL,
        "row13": IDS13,
        "order": order,
        "economies": E,
        "totals": totals,
        "trade": trade,
        "tariffs": tariffs,
        "events": events,
        "values2026": [s for s in stored if s["kind"] == "value"],
        "chokepoints": chokepoints,
        "straits": straits,
        "finance": finance,
        "history": history,
        "gate": {"rule": "drawn only if the quoted passage is found verbatim (whitespace-normalised) on the source fetched on or after the as-of date, "
                         "every value appears in the quote, and Jev (TypeSafe System One, a Noul) judges the page supports the claim at 0.6 or more; "
                         "if Jev is unavailable the gate fails closed. An event passes only when its own item and every further source it cites "
                         "(kind 'evidence': a date, a scope, a later status) pass; an event past its stated end or expiry date is drawn only with "
                         "a recorded outcome (kind 'outcome') that passes the same gate",
                 "asOf": day, "threshold": verify.THRESHOLD, "jev": {"model": verify.JEV["model"], "question": verify.JEV_QUESTION["supported"]["instructions"]},
                 "verified": sum(1 for s in stored if s["status"] == "verified"), "unverified": len(unverified),
                 "pending": [{"id": s["id"], "label": s.get("label") or by_id[s["id"]].get("label"), "reason": s.get("reason")} for s in unverified],
                 "items": [{k: s.get(k) for k in ("id", "kind", "parent", "dated2026", "date", "status", "verifiedOn", "quoteFound", "supportedJ", "drawn", "reason")
                            if k != "parent" or s.get("parent")} for s in stored]},
        "checks": CHECKS,
        "assumptions": ASSUMED,
        "fallbacks": FALLBACKS,
        "failedSources": [{"name": k, "url": u, "error": err} for k, (u, err) in sorted(CACHE.failed.items())],
        "sources": SOURCES,
    }
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(doc, f, indent=1, ensure_ascii=False, sort_keys=False)
        f.write("\n")
    print(f"wrote {os.path.relpath(OUT, ROOT)}: {len(ALL)} economies, {len(events)} events, {len(stored)} gated items "
          f"({doc['gate']['verified']} verified, {doc['gate']['unverified']} unverified); Jev {verify.JEV['requests']} new requests, "
          f"{verify.JEV['cached']} cached, {verify.JEV['errors']} errors; jev credential {'available' if verify.jev.available() else 'UNAVAILABLE (gate fails closed)'}")
    if CACHE.failed:
        for k, (u, err) in sorted(CACHE.failed.items()):
            print(f"FAILED SOURCE {k}: {err} ({u})")
    if PROBLEMS:
        for p in PROBLEMS:
            print("PROBLEM: " + p, file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()

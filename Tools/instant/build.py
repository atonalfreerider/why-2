#!/usr/bin/env python3
"""Builds Assets/Resources/Data/economy/instant.json: the US economy at one instant, for the view "One year, all at once"
(MODALITIES.md sections 5, 12 and 13; MODALITIES-PLAN.md WP02).

What it collects:
  classes.dfa        household balance sheets by household INCOME group (bottom 20%, 20-40, 40-60, 60-80, 80-99, top 1%):
                     the Federal Reserve's Distributional Financial Accounts, the release circuit.json used (data through
                     2026:Q1; the bdecon/US-chartbook mirror of dfa-income-levels-detail.csv pinned to one commit), annual
                     means 1989-2026 and the latest quarter by instrument, and the Fed's newer release beside it. If the
                     mirror cannot be read, the Fed's current file stands in (a later release, said so); only if no
                     income-group file can be read does classBasis become 'wealth' (the mirror's wealth groups, else the
                     Fed's, else circuit.json groups)
  classes.flows      income, taxes, spending and saving by the same six groups: shares from BEA's Distribution of Personal
                     Income (components by decile, the top 1%'s share) and BEA/BLS's joint distribution of income,
                     consumption and saving, scaled to NIPA 2025 totals (approximate: the source year carried to 2025), and
                     the same shares on circuit.json's pre-update 2025 totals (circuitVintage, the vintage the spec quotes).
                     The top 1%'s part of the top fifth comes from CBO when read by hand into sources.json; otherwise from
                     BEA's top-1% shares and the DFA holdings of the same income groups (the steps are stored and printed)
  classes.spendingMix  the spending mix by income fifth, BLS Consumer Expenditure Survey 2024 and 2025 (BLS Public Data API);
                     sixGroups uses 2024 (the spec's year and spending.json's); the top fifth's mix stands for 80-99% and
                     the top 1% (assumed)
  nipa               NIPA tables 1.1.5, 2.1, 3.1, 3.2 (federal interest), 3.10.5, 4.1, 6.16D and the government lines of
                     6.2D, 6.4D and 6.5D, annual 1947-2025, from BEA's flat files (the current vintage), with named 2025 facts
  profitsByTier      NIPA 6.16D corporate profits by industry mapped to the scene's four tiers (crosswalk printed, assumed)
  listedValue        listed market value by tier: S&P Dow Jones Indices GICS sector weights read by hand into sources.json,
                     times the Wilshire 5000 total in circuit.json (crosswalk printed, assumed); until they are read, the
                     spec's fallback, the 25 stored firms
  publicDebt         federal debt held by the public by holder, end of August 2026: Treasury Debt to the Penny (FiscalData
                     API), the Federal Reserve's holdings (H.4.1) and foreign holders (TIC Table 5)
  hyperscalerCapex   one calendar-2025 capital-spending figure for the five hyperscalers, reconciled from
                     corporate-programs.json, with its definition (finance leases out)

Every stored value is {value, unit, year, source, url, quote | field, note}; a table carries that provenance once, for all
of its numbers. "approx": true (and a note starting with "≈") marks a modelled split, not a measurement.

Downloads are cached under Tools/instant/.cache (gitignored), each with a small <name>.http.json beside it (URL, retrieval
date, Last-Modified), so --offline rebuilds the same file byte for byte. Sources that refuse automated requests (CBO, S&P Dow
Jones Indices, the www.bls.gov tables) are never fetched around; sources.json records them and the fallback taken.

Usage (from the repository root):
    python3 -I Tools/instant/build.py            # fetch what is not cached, write the JSON, print the checks
    python3 -I Tools/instant/build.py --offline  # use only the cache
Test options: --out PATH writes elsewhere; --simulate-missing KEY[,KEY] treats sources as unreachable: dfa-income (the
income-group file everywhere: the wealth-group fallback), dfa-wealth (the wealth-group file everywhere), dfa-mirror (both
mirror files), dfa-zip (the Fed's download).
The exit status is 1 when a check fails (the file is written either way).
"""
import csv
import datetime
import hashlib
import html
import io
import json
import math
import os
import re
import sys
import time
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
ECON = os.path.join(ROOT, "Assets", "Resources", "Data", "economy")
SRC = os.path.join(HERE, "sources.json")
CACHE = os.path.join(HERE, ".cache")


def _arg(name):
    if name in sys.argv:
        i = sys.argv.index(name)
        if i + 1 < len(sys.argv):
            return sys.argv[i + 1]
    return None


OFFLINE = "--offline" in sys.argv
OUT = os.path.abspath(_arg("--out") or os.path.join(ECON, "instant.json"))
SIMULATE_MISSING = set(filter(None, (_arg("--simulate-missing") or "").split(",")))
AGENT = "why-2-instant/0.1 (educational economic visualization)"
TODAY = datetime.date.today().isoformat()  # only ever written into a cache sidecar at download time

# Spec targets the build log tests (MODALITIES.md section 12, MODALITIES-PLAN.md WP02 acceptance).
TARGET_DEBT_T = 20.0        # "household liabilities (all DFA instruments) $20.0T within 3%" (MODALITIES.md 2.3, corrected 3 Nov 2026)
TARGET_DEBT_TOL = 0.03
TOTALS_TOL = 0.005          # DFA groups vs DFA totals; flows by group vs NIPA totals
SHARES_TOL_PT = 0.1         # net-worth shares vs circuit.json groupNotes, percentage points
NET_LENDING_TOL_B = 1.0     # receipts - expenditures vs net lending, $B
WEIGHTS_TOL = 0.5           # sector weights sum to 100 +- 0.5

DECILES = ["0-10%", "10-20%", "20-30%", "30-40%", "40-50%", "50-60%", "60-70%", "70-80%", "80-90%", "90-100%"]

CHECKS = []        # {id, status, line}
FAILED = []        # sources that could not be read, with the fallback taken
ASSUMED = []       # modelled or assumed values (marked ≈ in the file)
PROBLEMS = []      # data problems found while building
FETCHED = {}       # cache name -> sidecar


class Unavailable(Exception):
    pass


# ----------------------------------------------------------------------------------------------------------- downloads

def fetch(url, name, key=None, data=None, content_type=None, check=None):
    """The body of url, downloaded once into .cache/<name> with .cache/<name>.http.json beside it; later runs read the
    cache. check(body) must return True for a download to be kept (an HTML refusal page is never cached). key is one
    --simulate-missing name or a tuple of them; any of them makes the source unavailable."""
    keys = (key,) if isinstance(key, str) else tuple(key or ())
    hit = [k for k in keys if k in SIMULATE_MISSING]
    if hit:
        raise Unavailable(f"{name}: simulated as missing (--simulate-missing {hit[0]})")
    path = os.path.join(CACHE, name)
    side = path + ".http.json"
    if not (os.path.exists(path) and os.path.exists(side)):
        if OFFLINE:
            raise Unavailable(f"--offline: {name} is not cached")
        headers = {"User-Agent": AGENT}
        if content_type:
            headers["Content-Type"] = content_type
        body, info, last = None, None, None
        for attempt in range(3):
            try:
                req = urllib.request.Request(url, data=data, headers=headers)
                with urllib.request.urlopen(req, timeout=240) as r:
                    body = r.read()
                    info = {"url": url, "status": r.status, "retrieved": TODAY,
                            "lastModified": r.headers.get("Last-Modified"), "contentType": r.headers.get("Content-Type")}
                break
            except urllib.error.HTTPError as e:
                last = f"HTTP {e.code}"
                if e.code in (401, 403, 404, 410):
                    break  # a refusal or a wrong address does not heal by retrying
            except Exception as e:  # noqa: BLE001
                last = type(e).__name__
            time.sleep(3 * (attempt + 1))
        if body is None:
            raise Unavailable(f"{url}: {last}")
        if check and not check(body):
            raise Unavailable(f"{url}: the response is not the expected file ({len(body)} bytes)")
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "wb") as f:
            f.write(body)
        if data is not None:
            info["request"] = data.decode("utf-8")
        with open(side, "w", encoding="utf-8") as f:
            json.dump(info, f, indent=1, sort_keys=True)
    with open(path, "rb") as f:
        body = f.read()
    with open(side, encoding="utf-8") as f:
        info = json.load(f)
    FETCHED[name] = info
    return body, info


def load_json(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def http_date(s):
    """'Tue, 16 Jun 2026 14:00:04 GMT' -> '2026-06-16' (None stays None)."""
    if not s:
        return None
    try:
        return datetime.datetime.strptime(s, "%a, %d %b %Y %H:%M:%S %Z").date().isoformat()
    except ValueError:
        return s


def xlsx_sheets(body):
    """{sheet name: rows (lists of cell strings)} of an .xlsx file, read with the standard library."""
    ns = {"m": "http://schemas.openxmlformats.org/spreadsheetml/2006/main",
          "r": "http://schemas.openxmlformats.org/officeDocument/2006/relationships"}
    z = zipfile.ZipFile(io.BytesIO(body))
    shared = []
    if "xl/sharedStrings.xml" in z.namelist():
        for si in ET.fromstring(z.read("xl/sharedStrings.xml")).findall("m:si", ns):
            shared.append("".join(t.text or "" for t in si.iter("{%s}t" % ns["m"])))
    rels = {r.get("Id"): r.get("Target") for r in ET.fromstring(z.read("xl/_rels/workbook.xml.rels"))}
    out = {}
    for s in ET.fromstring(z.read("xl/workbook.xml")).find("m:sheets", ns):
        target = rels[s.get("{%s}id" % ns["r"])].lstrip("/")
        target = target if target.startswith("xl/") else "xl/" + target
        rows = []
        for row in ET.fromstring(z.read(target)).iter("{%s}row" % ns["m"]):
            cells = {}
            for c in row.findall("m:c", ns):
                col = 0
                for ch in re.match(r"[A-Z]+", c.get("r")).group(0):
                    col = col * 26 + ord(ch) - 64
                v = c.find("m:v", ns)
                if c.get("t") == "inlineStr":
                    cells[col - 1] = "".join(x.text or "" for x in c.iter("{%s}t" % ns["m"]))
                elif v is not None:
                    cells[col - 1] = shared[int(v.text)] if c.get("t") == "s" else v.text
            if cells:
                rows.append([cells.get(i, "") for i in range(max(cells) + 1)])
        out[s.get("name")] = rows
    return out


def num(s):
    """A cell as (value, interval): '0.016' -> (0.016, None); '[0.159-0.165]' -> (0.162, (0.159, 0.165)); else (None, None)."""
    if s is None:
        return None, None
    s = str(s).strip()
    m = re.fullmatch(r"\[\s*(-?[0-9.]+)\s*-\s*(-?[0-9.]+)\s*\]", s)
    if m:
        lo, hi = float(m.group(1)), float(m.group(2))
        return (lo + hi) / 2, (lo, hi)
    try:
        return float(s.replace(",", "")), None
    except ValueError:
        return None, None


def r3(x):
    return None if x is None else round(x, 3)


def r6(x):
    return None if x is None else round(x, 6)


def check(cid, ok, line, status=None):
    status = status or ("PASS" if ok else "FAIL")
    CHECKS.append({"id": cid, "status": status, "line": line})


def money(x, unit="B"):
    return f"${x:,.1f}{unit}"


# ---------------------------------------------------------------------------------------------------------------- NIPA

def load_nipa(cfg):
    body, info = fetch(cfg["url"], "bea/FlatFiles.zip", check=lambda b: b[:2] == b"PK")
    z = zipfile.ZipFile(io.BytesIO(body))
    names = {n.lower(): n for n in z.namelist()}

    def text(n):
        return z.read(names[n.lower()]).decode("utf-8-sig")

    stamp = datetime.date(*z.getinfo(names["nipadataa.txt"]).date_time[:3]).isoformat()
    values = {}
    rows = csv.reader(io.StringIO(text("NipaDataA.txt")))
    next(rows)
    for code, period, value in rows:
        v, _ = num(value)
        if v is not None:
            values.setdefault(code, {})[int(period)] = v
    reg = {}
    rows = csv.reader(io.StringIO(text("SeriesRegister.txt")))
    next(rows)
    for row in rows:
        code, label, metric, calc, scale, placed = row[0], row[1], row[2], row[3], int(row[4]), row[5]
        for t in placed.split("|"):
            if ":" in t:
                tid, line = t.split(":")
                reg.setdefault(tid, {})[int(line)] = (code, label, metric, calc, scale)
    titles = {}
    for row in csv.reader(io.StringIO(text("TablesRegister.txt"))):
        if row and row[0] != "TableId":
            titles[row[0]] = row[1]
    return {"values": values, "reg": reg, "titles": titles, "stamp": stamp, "info": info, "url": cfg["url"], "cfg": cfg}


def nipa_unit(metric, scale):
    """(unit, factor) turning a flat-file value into the stored unit."""
    if metric == "Current Dollars" and scale == -6:
        return "$B", 1 / 1000
    if metric == "Persons" and scale == -3:
        return "million persons", 1 / 1000
    if metric == "Persons" and scale == 0:
        return "persons", 1
    return metric.lower() + (f" (x10^{scale})" if scale else ""), 1


def nipa_line(N, ref):
    tid, ln = ref.split(":")
    ln = int(ln)
    code, label, metric, calc, scale = N["reg"][tid][ln]
    label = N["cfg"]["tables"].get(tid, {}).get("stubs", {}).get(str(ln), label)
    unit, f = nipa_unit(metric, scale)
    return tid, ln, code, label, unit, f


def nipa_value(N, ref, year):
    tid, ln, code, label, unit, f = nipa_line(N, ref)
    v = N["values"].get(code, {}).get(year)
    return None if v is None else v * f


def nipa_fact(N, ref, year):
    tid, ln, code, label, unit, f = nipa_line(N, ref)
    v = N["values"].get(code, {}).get(year)
    title = N["titles"].get(tid, tid)
    return {"value": r3(v * f) if v is not None else None, "unit": unit, "year": year,
            "source": f"BEA NIPA {title}, line {ln} '{label}'", "url": N["url"],
            "field": f"NipaDataA.txt series {code}, {year} (BEA flat files dated {N['stamp']})"}


def nipa_table(N, tid, tcfg, first, last):
    lines = N["reg"].get(tid, {})
    pick = sorted(lines) if tcfg["lines"] == "all" else tcfg["lines"]
    out = {}
    for ln in pick:
        if ln not in lines:
            PROBLEMS.append(f"NIPA {tid} has no line {ln}")
            continue
        code, label, metric, calc, scale = lines[ln]
        if calc != "Level":
            continue
        unit, f = nipa_unit(metric, scale)
        series = N["values"].get(code, {})
        y0 = min((y for y in series if first <= y <= last), default=None)
        if y0 is None:
            PROBLEMS.append(f"NIPA {tid} line {ln} ({code}) has no values {first}-{last}")
            continue
        out[str(ln)] = {"label": tcfg.get("stubs", {}).get(str(ln), label), "series": code, "unit": unit, "first": y0,
                        "values": [r3(series[y] * f) if y in series else None for y in range(y0, last + 1)]}
    block = {"title": N["titles"].get(tid, tid), "source": f"BEA NIPA {N['titles'].get(tid, tid)}", "url": N["url"],
             "field": f"NipaDataA.txt values by the series codes SeriesRegister.txt places on this table's lines (BEA flat files "
                      f"dated {N['stamp']}); a line's values[i] is year first + i",
             "years": [first, last], "lines": out}
    if tcfg.get("note"):
        block["note"] = tcfg["note"]
    return block


# ----------------------------------------------------------------------------------------------------------------- DFA

def quarter_key(q):
    y, n = q.split(":Q")
    return int(y), int(n)


def dfa_parse(text):
    """{quarter: {category: {column: value}}}, columns, quarters in order."""
    rows = list(csv.DictReader(io.StringIO(text)))
    data = {}
    for r in rows:
        vals = {}
        for k, v in r.items():
            if k in ("Date", "Category"):
                continue
            x, _ = num(v)
            if x is not None:
                vals[k] = x
        data.setdefault(r["Date"], {})[r["Category"]] = vals
    return data, [c for c in rows[0].keys() if c not in ("Date", "Category")], sorted(data, key=quarter_key)


def col_sum(row, cols):
    s = 0.0
    for c in cols:
        neg = c.startswith("-")
        v = row.get(c[1:] if neg else c, 0.0)
        s += -v if neg else v
    return s


def merge_categories(row_by_cat, cats):
    out = {}
    for c in cats:
        for k, v in row_by_cat.get(c, {}).items():
            out[k] = out.get(k, 0.0) + v
    return out


def group_record(row, cfg):
    """One group's quarter (or annual mean) in $B by instrument, with totals, households in millions."""
    rec = {}
    for inst in cfg["instruments"]:
        rec[inst["id"]] = r3(col_sum(row, inst["columns"]) / 1000)
    t = cfg["totals"]
    rec["assets"] = r3(row.get(t["assets"], 0.0) / 1000)
    rec["liabilities"] = r3(row.get(t["liabilities"], 0.0) / 1000)
    rec["netWorth"] = r3(row.get(t["netWorth"], 0.0) / 1000)
    rec["households"] = round(row.get(t["households"], 0.0) / 1e6, 4)
    return rec


def annual_rows(data, quarters, cats_by_group):
    """{year: {group id: {column: mean over the year's quarters}}}, {year: number of quarters}."""
    by_year, count = {}, {}
    for q in quarters:
        y = quarter_key(q)[0]
        count[y] = count.get(y, 0) + 1
        for gid, cats in cats_by_group.items():
            row = merge_categories(data[q], cats)
            acc = by_year.setdefault(y, {}).setdefault(gid, {})
            for k, v in row.items():
                acc[k] = acc.get(k, 0.0) + v
    for y, groups in by_year.items():
        for gid, acc in groups.items():
            for k in acc:
                acc[k] /= count[y]
    return by_year, count


def quarter_in(text):
    """The first 'YYYY:Qn' in a text, or None."""
    m = re.search(r"(\d{4}:Q\d)", text or "")
    return m.group(1) if m else None


def debt_comparison_z1(src, circuit):
    """The nearest measure to the spec's first figure, $20.6T, in the repository: the Fed Z.1 debt of households and nonprofit
    organizations (history.json's debt-to-income ratio) times circuit.json's disposable income of the same year (≈)."""
    cfg = src["dfa"].get("debtComparisonZ1")
    if not cfg:
        return None
    hist = load_json(os.path.join(ROOT, cfg["file"]))
    series = hist["series"][cfg["series"]]
    ratio = dict((int(y), v) for y, v in series["points"]).get(cfg["year"])
    dpi = circuit["personal"].get(cfg["dpiKey"])
    if ratio is None or dpi is None:
        return None
    return {"what": cfg["what"], "value": r3(ratio / 100 * dpi), "unit": "$B", "year": cfg["year"], "approx": True,
            "source": f"{series['source']} (history.json); circuit.json personal.{cfg['dpiKey']}",
            "url": cfg["file"],
            "computed": f"history.json series.{cfg['series']} {cfg['year']} ({ratio:g}% of disposable income) x circuit.json "
                        f"personal.{cfg['dpiKey']} (${dpi:,.1f}B, the pre-update {cfg['year']} vintage)",
            "note": "≈ " + cfg["note"]}


def circuit_wealth(circuit, wgroups):
    """The last fallback: circuit.json's four wealth groups (the same DFA release), net worth and households only."""
    quarter = quarter_in(circuit["groupNotes"]["source"])
    byid = {g["id"]: g for g in circuit["groups"]}
    latest = {g["id"]: {"netWorth": r3(byid[g["id"]]["netWorth"] * 1000), "households": byid[g["id"]]["households"],
                        "netWorthShare": byid[g["id"]]["netWorthShare"]} for g in wgroups}
    check("dfa-instruments", False, "DFA instruments: not available (circuit.json groups hold net worth, not instruments)",
          status="SKIPPED")
    check("dfa-totals", False, "DFA by income: no income-group file could be read; classBasis = 'wealth' from circuit.json "
                               "groups (the last fallback)")
    check("net-worth-shares", False, "net-worth shares by income group not built (wealth-group fallback)", status="SKIPPED")
    check("household-debt", False, "household debt: not computed (circuit.json groups hold debt shares, not levels)",
          status="SKIPPED")
    return {"source": "circuit.json groups: " + circuit["groupNotes"]["source"],
            "url": "Assets/Resources/Data/economy/circuit.json",
            "field": "circuit.json groups[].netWorth ($T x 1,000 = $B), .households (millions), .netWorthShare",
            "classBasis": "wealth", "sameReleaseAsCircuit": True, "unit": "$B; households in millions",
            "groups": [{"id": g["id"], "name": g["name"], "circuitGroup": g["id"]} for g in wgroups],
            "latest": {"quarter": quarter, "groups": latest},
            "annual": None,
            "note": "the last fallback: no DFA file could be read, so the four wealth groups are circuit.json's (the same "
                    "release), with net worth and household counts only: no instruments and no annual history"}


def build_dfa(src, circuit):
    """Household balance sheets by group. The DFA files come from the mirror pinned to circuit.json's release; when the
    mirror cannot be read, from the Fed's current download (a later release). Only when no income-group file can be
    read at all does the build fall back to the four DFA wealth groups (classBasis 'wealth', MODALITIES.md section 2.7):
    from the mirror, else the Fed's download, else circuit.json groups (net worth and households only)."""
    cfg = src["dfa"]
    groups = src["classes"]["groups"]
    wgroups = src["classes"]["wealthFallback"]
    mirror = cfg["mirror"]
    murl = lambda f: mirror["url"].format(commit=mirror["commit"], file=f)  # noqa: E731
    csv_ok = lambda b: b[:40].lstrip(b"\xef\xbb\xbf").startswith(b"Date,Category")  # noqa: E731
    INC, NW = "dfa-income-levels-detail.csv", "dfa-networth-levels-detail.csv"
    why = {}

    def from_mirror(file, keys):
        try:
            body, info = fetch(murl(file), "dfa/mirror-" + file, key=keys, check=csv_ok)
            return body.decode("utf-8-sig"), info
        except Unavailable as e:
            why[("mirror", file)] = str(e)
            return None

    m_inc = from_mirror(INC, ("dfa-income", "dfa-mirror"))
    m_nw = from_mirror(NW, ("dfa-wealth", "dfa-mirror"))
    official, zinfo = None, None
    try:
        zbody, zinfo = fetch(cfg["official"]["url"], "dfa/fed-dfa.zip", key="dfa-zip", check=lambda b: b[:2] == b"PK")
        official = zipfile.ZipFile(io.BytesIO(zbody))
    except Unavailable as e:
        why[("zip", None)] = str(e)
    znames = {n.lower(): n for n in official.namelist()} if official else {}

    def from_zip(file, key):
        if official is None:
            why[("zip", file)] = why[("zip", None)]
            return None
        if key in SIMULATE_MISSING:
            why[("zip", file)] = f"{file} in dfa.zip: simulated as missing (--simulate-missing {key})"
            return None
        n = znames.get(file)
        if not n:
            why[("zip", file)] = f"dfa.zip holds no {file}"
            return None
        return official.read(n).decode("utf-8-sig"), datetime.date(*official.getinfo(n).date_time[:3]).isoformat()

    z_inc = from_zip(INC, "dfa-income")
    z_nw = from_zip(NW, "dfa-wealth")

    # which file the classes come from
    if m_inc:
        basis, origin = "income", "mirror"
    elif z_inc:
        basis, origin = "income", "zip"
    else:
        basis, origin = "wealth", ("mirror" if m_nw else "zip" if z_nw else "circuit")
    where = {"mirror": "the mirror's dfa-networth-levels-detail.csv (the same release as circuit.json)",
             "zip": "the Fed's current download (a later release than circuit.json's)",
             "circuit": "circuit.json groups (the same release; net worth and households only: no instruments, no annual "
                        "history)"}
    if not m_inc:
        if basis == "income":
            fb = (f"the same file from {where['zip']}; the net-worth shares are compared for the quarter circuit.json's "
                  f"groupNotes name, as that release revised it")
        else:
            fb = (f"no usable {INC} in the Fed's download either ({why[('zip', INC)]}); classBasis = 'wealth': the four DFA "
                  f"wealth groups from {where[origin]}, the scale printed in every key with the bridge line (MODALITIES.md "
                  f"section 2.7); flows by group are left to circuit.json's copula-moved shares")
        FAILED.append({"source": f"Fed DFA levels by income group, the release circuit.json used ({mirror['repo']} mirror, "
                                 f"{INC})", "why": why[("mirror", INC)], "fallback": fb})
    if not m_nw:
        if basis == "income" and origin == "mirror":
            fb = "the totals check compares net worth and household counts with circuit.json groups (the same release)"
        elif basis == "income":
            fb = (f"the totals check uses {NW} in the same dfa.zip (the release of the income file used)" if z_nw else
                  "the totals check is not run")
        else:
            fb = "the four wealth groups come from " + where[origin]
            if origin == "circuit":
                fb += f" (the Fed's download: {why[('zip', NW)]})"
        FAILED.append({"source": f"Fed DFA levels by wealth group, the release circuit.json used ({mirror['repo']} mirror, "
                                 f"{NW})", "why": why[("mirror", NW)], "fallback": fb})
    if official is None:
        FAILED.append({"source": "Fed DFA current download (dfa.zip)", "why": why[("zip", None)],
                       "fallback": "no newer-release quarter; the DFA definitions are not quoted"})
    if origin == "circuit":
        return circuit_wealth(circuit, wgroups), basis, None

    if basis == "income":
        text, file, kind = (m_inc if origin == "mirror" else z_inc)[0], INC, cfg["source"]
        cats = {g["id"]: [g["dfa"]] for g in groups}
        gdefs = [{"id": g["id"], "name": g["name"], "short": g["short"], "dfaCategory": g["dfa"]} for g in groups]
    else:
        text, file, kind = (m_nw if origin == "mirror" else z_nw)[0], NW, cfg["sourceWealth"]
        cats = {g["id"]: g["dfa"] for g in wgroups}
        gdefs = [{"id": g["id"], "name": g["name"], "dfaCategories": g["dfa"]} for g in wgroups]
    data, cols, quarters = dfa_parse(text)
    latest = quarters[-1]
    if origin == "mirror":
        info = (m_inc if basis == "income" else m_nw)[1]
        source, url = f"{kind}; {mirror['release']}", murl(file)
        fetched = f"{mirror['repo']} at commit {mirror['commit'][:7]} ({mirror['committed']}); retrieved {info['retrieved']}"
        whyfile = mirror["why"]
    else:
        dated = (z_inc if basis == "income" else z_nw)[1]
        source = (f"{kind}; the Fed's current download, files dated {dated}, published {http_date(zinfo.get('lastModified'))} "
                  f"(data through {latest}), a later release than circuit.json's")
        url = cfg["official"]["url"]
        fetched = f"{file} inside dfa.zip; retrieved {zinfo['retrieved']}"
        whyfile = ("the mirror pinned to circuit.json's release could not be read (failedSources), so the Fed's own current "
                   "file stands in")
    by_year, count = annual_rows(data, quarters, cats)
    years = sorted(by_year)

    def groups_at(q):
        recs = {gid: group_record(merge_categories(data[q], cs), cfg) for gid, cs in cats.items()}
        tot = sum(g["netWorth"] for g in recs.values())
        for g in recs.values():
            g["netWorthShare"] = r6(g["netWorth"] / tot)
        return recs

    latest_groups = groups_at(latest)

    # the instruments add up to the DFA's own totals in every group
    worst = 0.0
    for gid, g in latest_groups.items():
        a = sum(g[i["id"]] for i in cfg["instruments"] if i["side"] == "asset")
        l = sum(g[i["id"]] for i in cfg["instruments"] if i["side"] == "liability")
        worst = max(worst, abs(a - g["assets"]), abs(l - g["liabilities"]), abs(g["assets"] - g["liabilities"] - g["netWorth"]))
    check("dfa-instruments", worst < 0.01, f"DFA instruments add up to each group's assets, liabilities and net worth ({latest}): "
                                           f"largest gap ${worst:.3f}B")

    annual = {"years": [years[0], years[-1]], "quarters": [count[y] for y in years], "groups": {}}
    inst_ids = [i["id"] for i in cfg["instruments"]] + ["assets", "liabilities", "netWorth", "households"]
    for gid in cats:
        recs = [group_record(by_year[y][gid], cfg) for y in years]
        annual["groups"][gid] = {k: [rec[k] for rec in recs] for k in inst_ids}

    block = {
        "source": source,
        "url": url,
        "officialUrl": cfg["official"]["url"],
        "page": cfg["page"],
        "field": f"columns of {file} (millions of dollars; 'Household count' in households), summed per instrument as "
                 "'instruments' lists, divided by 1,000 ($B) and 1,000,000 (households, millions)",
        "fetched": fetched,
        "why": whyfile,
        "classBasis": basis,
        "sameReleaseAsCircuit": origin == "mirror",
        "unit": "$B; households in millions",
        "instruments": [{"id": i["id"], "name": i["name"], "side": i["side"], "columns": i["columns"]} for i in cfg["instruments"]],
        "groups": gdefs,
        "latest": {"quarter": latest, "groups": latest_groups},
        "annual": dict(annual, note="annual means of the quarters each year holds ('quarters'); 1989 holds Q3-Q4 and the "
                                    f"last year {years[-1]} holds {count[years[-1]]} quarter(s); values[i] is year "
                                    f"{years[0]} + i"),
    }

    if basis == "income":
        names = ", ".join(g["name"] for g in gdefs)
        head = f"DFA by income: {len(latest_groups)} groups ({names}); "
        partner = None
        if origin == "mirror" and m_nw:
            partner = (m_nw[0], murl(NW), "the same release's dfa-networth-levels-detail.csv (wealth groups)")
        elif origin == "zip" and z_nw:
            partner = (z_nw[0], cfg["official"]["url"], "dfa-networth-levels-detail.csv in the same dfa.zip (wealth groups)")
        cq = quarter_in(circuit["groupNotes"]["source"])
        if partner:
            # the groups add up to the DFA totals: the income partition against the wealth partition of the same release
            nw_data, nw_cols, nw_quarters = dfa_parse(partner[0])
            gaps, gaps_all = [], []
            for col in cols:
                if "Cutoff" in col:
                    continue
                a = sum(data[latest][c].get(col, 0.0) for c in data[latest])
                b = sum(nw_data[latest][c].get(col, 0.0) for c in nw_data[latest])
                if b:
                    gaps.append(abs(a - b) / abs(b))
            for q in quarters:
                if q not in nw_data:
                    continue
                for col in ("Net worth", "Assets", "Liabilities", "Household count"):
                    a = sum(data[q][c].get(col, 0.0) for c in data[q])
                    b = sum(nw_data[q][c].get(col, 0.0) for c in nw_data[q])
                    if b:
                        gaps_all.append(abs(a - b) / abs(b))
            ok = len(latest_groups) == 6 and max(gaps) <= TOTALS_TOL
            check("dfa-totals", ok, head + f"{latest} levels sum to the DFA totals (the wealth-group partition of the same "
                                           f"release) within {TOTALS_TOL:.1%}: largest gap {max(gaps):.4%} over {len(gaps)} "
                                           f"columns (all {len(quarters)} quarters, net worth, assets, liabilities, "
                                           f"households: {max(gaps_all):.4%})")
            block["totalsCheck"] = {"value": r6(max(gaps)), "unit": "largest relative gap", "year": latest,
                                    "source": partner[2], "url": partner[1],
                                    "computed": "|sum over income groups - sum over wealth groups| / sum over wealth groups, "
                                                "every column, latest quarter"}
        elif origin == "mirror" and cq in data:
            # the mirror's wealth file is gone: circuit.json groups hold the same release's net worth and households
            a_nw = sum(data[cq][c].get("Net worth", 0.0) for c in data[cq]) / 1e6
            b_nw = sum(g["netWorth"] for g in circuit["groups"])
            a_hh = sum(data[cq][c].get("Household count", 0.0) for c in data[cq]) / 1e6
            b_hh = sum(g["households"] for g in circuit["groups"])
            gap = max(abs(a_nw - b_nw) / b_nw, abs(a_hh - b_hh) / b_hh)
            check("dfa-totals", len(latest_groups) == 6 and gap <= TOTALS_TOL,
                  head + f"{cq} net worth and households sum to circuit.json groups' totals (the same release; the mirror's "
                         f"wealth-group file could not be read) within {TOTALS_TOL:.1%}: ${a_nw:,.3f}T vs ${b_nw:,.3f}T, "
                         f"{a_hh:,.3f}M vs {b_hh:,.3f}M households, largest gap {gap:.4%}")
            block["totalsCheck"] = {"value": r6(gap), "unit": "largest relative gap", "year": cq,
                                    "source": "circuit.json groups (the same DFA release, wealth groups)",
                                    "url": "Assets/Resources/Data/economy/circuit.json",
                                    "computed": "|sum over income groups - sum of circuit.json groups| / the latter, net worth "
                                                "and household count"}
        else:
            check("dfa-totals", False, head + "no wealth-group partition of the same release to compare with",
                  status="SKIPPED")
        # net-worth shares against circuit.json groupNotes (read from its text, not typed here), in the quarter they name
        model = circuit["groupNotes"]["incomeWealthModel"]
        m = re.search(r"DFA \[([0-9., ]+)\]", model)
        notes = [float(x) for x in m.group(1).split(",")] if m else None
        nq = re.search(r"DFA (\d{4}:Q\d) net-worth shares", model)
        sq = nq.group(1) if nq and nq.group(1) in data else latest
        at = latest_groups if sq == latest else groups_at(sq)
        mine = [at[g["id"]]["netWorthShare"] * 100 for g in groups]
        label = "" if origin == "mirror" else " (the Fed's later release, as revised)"
        if notes and len(notes) == 6:
            gap = max(abs(a - b) for a, b in zip(mine, notes))
            check("net-worth-shares", gap <= SHARES_TOL_PT,
                  f"net-worth shares {sq}{label} [{', '.join(f'{x:.3f}' for x in mine)}] within {SHARES_TOL_PT} pt of "
                  f"circuit.json groupNotes [{', '.join(f'{x:g}' for x in notes)}]: largest gap {gap:.3f} pt")
        else:
            check("net-worth-shares", False, "circuit.json groupNotes.incomeWealthModel holds no 'DFA [...]' shares to compare")
    else:
        check("dfa-totals", False, f"DFA by income: no income-group file could be read; classBasis = 'wealth' from "
                                   f"{where[origin]} (fallback)")
        check("net-worth-shares", False, "net-worth shares by income group not built (wealth-group fallback)", status="SKIPPED")

    # household debt: every DFA liability (so assets less debt is net worth in each group), against the spec's $20.0T;
    # home mortgages and consumer credit are most of it
    debt = sum(g["homeMortgages"] + g["consumerCredit"] for g in latest_groups.values()) / 1000
    liab = sum(g["liabilities"] for g in latest_groups.values()) / 1000
    gap = liab / TARGET_DEBT_T - 1
    comparisons = [{"what": "home mortgages + consumer credit, the same release and quarter", "value": r3(debt * 1000), "unit": "$B",
                    "year": latest, "source": source, "url": url, "field": "sum over the groups of 'Home mortgages' + 'Consumer credit'"}]
    newer_note = ""

    # the Fed's newer release, for its latest quarter (only when the classes come from circuit.json's release)
    if origin == "mirror" and basis == "income" and z_inc:
        odata, ocols, oquarters = dfa_parse(z_inc[0])
        oq = oquarters[-1]
        dated = z_inc[1]
        ogroups = {gid: group_record(merge_categories(odata[oq], cs), cfg) for gid, cs in cats.items()}
        otot = sum(g["netWorth"] for g in ogroups.values())
        for g in ogroups.values():
            g["netWorthShare"] = r6(g["netWorth"] / otot)
        revised = {}
        if latest in odata:
            rg = {gid: group_record(merge_categories(odata[latest], cs), cfg) for gid, cs in cats.items()}
            revised = {gid: {"netWorth": rg[gid]["netWorth"], "liabilities": rg[gid]["liabilities"]} for gid in rg}
        odebt = sum(g["homeMortgages"] + g["consumerCredit"] for g in ogroups.values()) / 1000
        newer_note = f"; the newer release's {oq}: ${odebt:.2f}T"
        newer_src = (f"{cfg['source']}; the Fed's current download, files dated {dated}, published "
                     f"{http_date(zinfo.get('lastModified'))} (data through {oq})")
        block["newerRelease"] = {
            "source": newer_src,
            "url": cfg["official"]["url"],
            "field": f"columns of dfa-income-levels-detail.csv inside dfa.zip, {oq}, summed per instrument as above",
            "retrieved": zinfo["retrieved"],
            "quarter": oq,
            "groups": ogroups,
            "revisedLatest": {"quarter": latest, "groups": revised,
                              "note": f"the same quarter as 'latest' as this newer release revised it (net worth and "
                                      f"liabilities, $B)"},
            "note": "Not the release circuit.json used: shown for the 'as of' stamp and a later update. MODALITIES.md "
                    "quotes this release's headline as '$185.8T is DFA 2026:Q2'.",
        }
        comparisons.append({"what": f"home mortgages + consumer credit in the Fed's newer release, {oq}",
                            "value": r3(odebt * 1000), "unit": "$B", "year": oq, "source": newer_src,
                            "url": cfg["official"]["url"],
                            "field": f"sum over the groups of 'Home mortgages' + 'Consumer credit', {oq}"})
    if official is not None:
        dn = znames.get("dfa-data-definitions.txt")
        if dn:
            dtext = official.read(dn).decode("utf-8", "replace")
            defs = {}
            for line in dtext.splitlines():
                if ":" in line:
                    k, v = line.split(":", 1)
                    defs[k.strip()] = re.sub(r"\s+", " ", v).strip()
            block["definitions"] = {"source": "Federal Reserve, dfa-data-definitions.txt inside dfa.zip", "url": cfg["official"]["url"],
                                    "quote": {k: defs[k] for k in ("Unincorporated Business", "Defined contribution pension entitlements",
                                                                    "Corporate equities and mutual fund shares", "Real estate",
                                                                    "Home mortgages", "Consumer credit") if k in defs}}
    z1 = debt_comparison_z1(src, circuit)
    z1_note = ""
    if z1:
        comparisons.append(z1)
        z1_note = (f"; nearest in the repository: Fed Z.1 households and nonprofits {z1['year']}, history.json's ratio x "
                   f"circuit.json DPI ≈ ${z1['value'] / 1000:.2f}T ({z1['value'] / 1000 / TARGET_DEBT_T - 1:+.1%})")
        ASSUMED.append("household-debt comparison: Fed Z.1 households-and-nonprofits debt ≈ history.json's debt-to-income "
                       "ratio x circuit.json's disposable income (≈; stored beside the check, never drawn)")
    check("household-debt", abs(gap) <= TARGET_DEBT_TOL,
          f"household liabilities (all DFA instruments) {latest} ${liab:.2f}T vs the spec's ${TARGET_DEBT_T}T +- "
          f"{TARGET_DEBT_TOL:.0%}: {gap:+.1%} (home mortgages + consumer credit ${debt:.2f}T{newer_note}{z1_note})")
    block["householdDebt"] = {"value": r3(liab * 1000), "unit": "$B", "year": latest,
                              "source": source, "url": url,
                              "field": "sum over the groups of 'Liabilities'",
                              "note": f"Every household liability in the DFA, so assets less debt is net worth in each group "
                                      f"(MODALITIES.md 2.3, corrected 3 Nov 2026 from a first $20.6T that no DFA measure "
                                      f"matches). Home mortgages and consumer credit are ${debt:.2f}T of the ${liab:.2f}T.",
                              "comparisons": comparisons}
    raw_cols = {"data": data, "cats": cats, "by_year": by_year, "count": count}
    return block, basis, raw_cols


# ----------------------------------------------------------------------------------------------------------------- BEA

def load_dpi(cfg):
    body, info = fetch(cfg["url"], "bea/dpi-summary-file.csv",
                       check=lambda b: b[:40].lstrip(b"\xef\xbb\xbf").startswith(b"Year,Ranking"))
    idx = {}
    for r in csv.DictReader(io.StringIO(body.decode("utf-8-sig"))):
        idx[(int(r["Year"]), r["Ranking"], r["Income Concept"], r["Quantile or Summary Metric"])] = r["Value"]
    return idx, info


def load_joint(cfg):
    body, info = fetch(cfg["url"], "bea/joint_dist_summary.xlsx", check=lambda b: b[:2] == b"PK")
    rows = xlsx_sheets(body)[cfg["sheet"]]
    idx = {}
    for r in rows[1:]:
        if len(r) < 6:
            continue
        y, _ = num(r[0])
        v, _ = num(r[5])
        if y is not None and v is not None:
            idx[(int(y), r[1], r[2], r[4])] = v
    return idx, info


def quintiles(get):
    """Five quintile shares from ten decile shares (normalised to sum to one), and the raw decile sum."""
    d = [get(q) for q in DECILES]
    if any(x is None for x in d):
        return None, None
    raw = sum(d)
    n = [x / raw for x in d]
    return [n[0] + n[1], n[2] + n[3], n[4] + n[5], n[6] + n[7], n[8] + n[9]], raw


def income_shares(year, dpi, dfa_raw, src):
    """Shares of each income component by the six groups for one BEA year, and the top 1%'s steps."""
    bcfg = src["bea"]["dpi"]
    rk = bcfg["ranking"]
    comps = bcfg["components"]
    t1cfg = src["top1"]
    q, raw, tot = {}, {}, {}
    for c in comps:
        q[c["id"]], raw[c["id"]] = quintiles(lambda qq: num(dpi.get((year, rk, c["concept"], qq)))[0])
        tot[c["id"]] = num(dpi.get((year, "", c["concept"], "Total ($Billions)")))[0]
        if q[c["id"]] is None or tot[c["id"]] is None:
            return None
    pi_total = num(dpi.get((year, "", bcfg["personalIncome"]["concept"], "Total ($Billions)")))[0]
    dpi_total = num(dpi.get((year, "", bcfg["disposableIncome"]["concept"], "Total ($Billions)")))[0]
    s_pi, pi_iv = num(dpi.get((year, rk, bcfg["personalIncome"]["concept"], "Top 1%")))
    s_dpi, dpi_iv = num(dpi.get((year, bcfg["dpiRanking"], bcfg["disposableIncome"]["concept"], "Top 1%")))
    if None in (pi_total, dpi_total, s_pi, s_dpi):
        return None
    q5 = {cid: q[cid][4] * tot[cid] for cid in q}
    top1 = {}
    cbo = t1cfg["cbo"].get("shareOfTopFifth") or {}
    steps = {"year": year, "personalIncomeTop1Share": r6(s_pi), "personalIncomeTop1Ranking": rk,
             "disposableIncomeTop1Share": r6(s_dpi), "disposableIncomeTop1Ranking": bcfg["dpiRanking"]}
    if pi_iv:
        steps["personalIncomeTop1Interval"] = list(pi_iv)
    if dpi_iv:
        steps["disposableIncomeTop1Interval"] = list(dpi_iv)
    if all(cbo.get(c["id"]) is not None for c in comps):
        method = "cbo"
        for cid in q:
            top1[cid] = q5[cid] * cbo[cid]
        steps["shareOfTopFifth"] = {cid: r6(cbo[cid]) for cid in q}
    else:
        method = "fallback"
        fb = t1cfg["fallback"]
        annual = dfa_raw["by_year"].get(year)
        if annual is None or "top1" not in annual:
            return None
        a1, a2 = annual["top1"], annual["p80_99"]
        steps["keys"] = {}
        for cid, cols in fb["capitalKeys"].items():
            k1, k2 = col_sum(a1, cols), col_sum(a2, cols)
            frac = k1 / (k1 + k2)
            top1[cid] = q5[cid] * frac
            steps["keys"][cid] = {"columns": cols, "top1": r3(k1 / 1000), "p80_99": r3(k2 / 1000), "top1ShareOfTopFifth": r6(frac)}
        h1, h2 = a1.get("Household count", 0.0), a2.get("Household count", 0.0)
        hfrac = h1 / (h1 + h2)
        for cid in fb["perHousehold"]:
            top1[cid] = q5[cid] * hfrac
        steps["householdsTop1ShareOfTopFifth"] = r6(hfrac)
        pi1, dpi1 = s_pi * pi_total, s_dpi * dpi_total
        top1["taxes"] = pi1 - dpi1
        ratio = q5["contributions"] / q5["compensation"]
        rest = pi1 - sum(top1[c] for c in fb["capitalKeys"]) - sum(top1[c] for c in fb["perHousehold"])
        top1["compensation"] = rest / (1 - ratio)
        top1["contributions"] = ratio * top1["compensation"]
        steps.update({"personalIncomeTop1": r3(pi1), "disposableIncomeTop1": r3(dpi1), "taxesTop1": r3(top1["taxes"]),
                      "contributionsToPay": r6(ratio), "payTop1": r3(top1["compensation"]),
                      "unit": "$B of the BEA year (household totals of NIPA Table 2.9)"})
        # the taxes mix two rankings: BEA gives the top 1%'s disposable income only with households ranked by
        # disposable income. The top deciles, published under both rankings, show how far the rankings move a share.
        d_top10 = {r: num(dpi.get((year, r, bcfg["disposableIncome"]["concept"], "90-100%")))[0] for r in (rk, bcfg["dpiRanking"])}
        per_tenth = 0.001 * dpi_total
        steps["rankingMix"] = {
            "taxesTop1": "top-1% personal income with households ranked by personal income, less top-1% disposable income "
                         "with households ranked by disposable income (BEA publishes no top-1% disposable income under the "
                         "first ranking)",
            "direction": "ranking by disposable income picks the households with the most disposable income, so the "
                         "subtracted share is likely at least that of the income-ranked top 1%, and taxesTop1 is likely an "
                         "understatement of the income-ranked top 1%'s taxes (a lower estimate)",
            "topDecileDisposableIncomeShare": {r: r6(v) if v is not None else None for r, v in d_top10.items()},
            "taxesPerTenthPointOfDisposableIncomeShare": r3(per_tenth),
            "guide": "at the top decile the two rankings' disposable-income shares differ by "
                     + (f"{abs(d_top10[rk] - d_top10[bcfg['dpiRanking']]) * 100:.1f} pt (BEA rounds the shares to 0.1 pt)"
                        if None not in d_top10.values() else "an unknown amount")
                     + f"; each 0.1 pt of disposable-income share moves taxesTop1 by ${per_tenth:,.1f}B "
                       f"({per_tenth / top1['taxes']:.1%} of it). A guide, not a bound: the gap at the top 1% is not published."}
    shares, clamped = {}, []
    for cid in q:
        t = top1[cid] / tot[cid]
        if t < 0 or t > q[cid][4]:
            clamped.append(f"{cid} {t:.4f} outside 0..{q[cid][4]:.4f}")
            t = min(max(t, 0.0), q[cid][4])
        qq = q[cid]
        shares[cid] = [qq[0], qq[1], qq[2], qq[3], qq[4] - t, t]
    if clamped:
        PROBLEMS.append(f"top 1% split {year}: clamped " + "; ".join(clamped))
        steps["clamped"] = clamped
    steps["method"] = method
    return {"shares": shares, "raw": raw, "steps": steps, "q5": {cid: q[cid][4] for cid in q}}


def spending_shares(year, joint, src):
    jcfg = src["bea"]["joint"]
    rk = jcfg["ranking"]
    out, raw = {}, {}
    for s in jcfg["series"]:
        qq, rw = quintiles(lambda d: joint.get((year, rk, s["series"], d)))
        t = joint.get((year, rk, s["series"], "Top 1%"))
        if qq is None or t is None:
            return None
        out[s["id"]] = [qq[0], qq[1], qq[2], qq[3], qq[4] - t, t]
        raw[s["id"]] = rw
    return {"shares": out, "raw": raw}


STATEMENT_COMPUTED = ("statement[], per group from the same group's byGroup values: grossIncome = derived.grossIncome "
                      "(personal income + contributions); sources.pay = components.compensation; sources.owning = "
                      "components.proprietors + rental + interest + dividends; sources.state = components.benefits; "
                      "sources.other = components.fromBusiness; uses.taxes = components.taxes; uses.contributions = "
                      "components.contributions; uses.spending = derived.pce; uses.otherOutlays = derived.otherOutlays; "
                      "uses.saving = derived.saving")


def group_statements(groups, comps, values, pce, sav):
    """Personal income, gross income, disposable income and other outlays by group, and the per-group statements, from
    the income components by group (values: id -> six values) and PCE and saving by group."""
    n = len(groups)
    contrib = values["contributions"]
    pi = [sum(values[c["id"]][i] for c in comps if c["role"] not in ("deducted", "tax")) - contrib[i] for i in range(n)]
    gross = [pi[i] + contrib[i] for i in range(n)]
    dpi_g = [pi[i] - values["taxes"][i] for i in range(n)]
    other = [dpi_g[i] - pce[i] - sav[i] for i in range(n)]
    statement = []
    for i, g in enumerate(groups):
        owning = sum(values[c][i] for c in ("proprietors", "rental", "interest", "dividends"))
        statement.append({"id": g["id"], "approx": True, "grossIncome": r3(gross[i]),
                          "sources": {"pay": r3(values["compensation"][i]), "owning": r3(owning),
                                      "state": r3(values["benefits"][i]), "other": r3(values["fromBusiness"][i])},
                          "uses": {"taxes": r3(values["taxes"][i]), "contributions": r3(contrib[i]), "spending": r3(pce[i]),
                                   "otherOutlays": r3(other[i]), "saving": r3(sav[i])}})
    return pi, gross, dpi_g, other, statement


def build_flows(src, N, dfa_raw, groups, circuit):
    bcfg = src["bea"]
    dpi, dinfo = load_dpi(bcfg["dpi"])
    joint, jinfo = load_joint(bcfg["joint"])
    y0, year = bcfg["sourceYear"], src["nipa"]["year"]
    inc = income_shares(y0, dpi, dfa_raw, src)
    spend = spending_shares(y0, joint, src)
    if inc is None or spend is None:
        raise SystemExit(f"BEA distribution data for {y0} are incomplete")
    gids = [g["id"] for g in groups]
    dpi_src = f"{bcfg['dpi']['source']}, released {http_date(dinfo.get('lastModified'))}; households ranked by {bcfg['dpi']['ranking'].lower()}"
    joint_src = f"{bcfg['joint']['source']}, released {http_date(jinfo.get('lastModified'))}; households ranked by {bcfg['joint']['ranking'].lower()}"
    approx_note = (f"≈ shares of {y0} (BEA) carried to {year} and scaled to NIPA {year} totals; BEA distributes the household "
                   f"part of personal income (NIPA Table 2.9), the totals here are Table 2.1's (persons, incl. nonprofit "
                   f"institutions)")
    components = {}
    values = {}
    worst = 0.0
    for c in bcfg["dpi"]["components"]:
        cid = c["id"]
        total = nipa_fact(N, c["nipa"], year)
        vals = [s * total["value"] for s in inc["shares"][cid]]
        values[cid] = vals
        worst = max(worst, abs(sum(vals) - total["value"]) / abs(total["value"]))
        components[cid] = {
            "concept": c["concept"], "role": c["role"], "approx": True,
            "source": dpi_src, "url": bcfg["dpi"]["url"],
            "field": f"Year {y0}, Ranking '{bcfg['dpi']['ranking']}', Income Concept '{c['concept']}', deciles 0-10% .. 90-100% "
                     f"(normalised to sum to one; raw sum {inc['raw'][cid]:.3f}); the top 1% as 'top1'",
            "shares": [r6(s) for s in inc["shares"][cid]],
            "byGroup": [r3(v) for v in vals],
            "nipaTotal": total,
            "note": approx_note,
        }
    jvals = {}
    for s in bcfg["joint"]["series"]:
        total = nipa_fact(N, s["nipa"], year)
        jvals[s["id"]] = (total, [x * total["value"] for x in spend["shares"][s["id"]]])
    pce_t, pce = jvals["pce"]
    sav_t, sav = jvals["saving"]
    pi, gross, dpi_g, other, statement = group_statements(groups, bcfg["dpi"]["components"], values, pce, sav)
    pi_t = nipa_fact(N, bcfg["dpi"]["personalIncome"]["nipa"], year)
    dpi_t = nipa_fact(N, bcfg["dpi"]["disposableIncome"]["nipa"], year)
    paid = nipa_value(N, "T20100:30", year) + nipa_value(N, "T20100:31", year)
    sums = [("personal income", sum(pi), pi_t["value"]), ("gross income", sum(gross), pi_t["value"] + nipa_value(N, "T20100:25", year)),
            ("disposable income", sum(dpi_g), dpi_t["value"]), ("PCE", sum(pce), pce_t["value"]),
            ("saving", sum(sav), sav_t["value"]), ("interest and transfers paid", sum(other), paid)]
    for name, a, b in sums:
        worst = max(worst, abs(a - b) / abs(b))
    check("flows-totals", worst <= TOTALS_TOL,
          f"flows by group sum to NIPA {year} totals within {TOTALS_TOL:.1%}: {len(components)} income components and "
          f"{', '.join(n for n, _, _ in sums)}; largest gap {worst:.4%}")
    negative = [f"{groups[i]['name']} {other[i]:.1f}" for i in range(len(gids)) if other[i] < 0]
    check("flows-outlays", not negative,
          "interest and transfers paid by group (disposable income less PCE less saving) are not negative" +
          (f": negative for {'; '.join(negative)}" if negative else
           f": {', '.join(f'{v:,.1f}' for v in other)} ($B)"))
    # BEA's own quintile shares of personal income, against the shares the components give
    pub = [num(dpi.get((y0, bcfg["dpi"]["ranking"], "Personal income", q)))[0] for q in ("0-20%", "20-40%", "40-60%", "60-80%", "80-100%")]
    mine = [pi[0], pi[1], pi[2], pi[3], pi[4] + pi[5]]
    mine = [x / sum(pi) for x in mine]
    pub_gap = max(abs(a - b) for a, b in zip(mine, pub)) if None not in pub else None
    top1_pub = inc["steps"]["personalIncomeTop1Share"]
    derived = {
        "personalIncome": {"byGroup": [r3(v) for v in pi], "nipaTotal": pi_t, "approx": True,
                           "computed": "income components less contributions for government social insurance",
                           "source": dpi_src, "url": bcfg["dpi"]["url"], "note": approx_note,
                           "publishedQuintileShares": {"value": [r6(x) for x in pub] if None not in pub else None,
                                                       "unit": "share of personal income, fifths", "year": y0,
                                                       "source": dpi_src, "url": bcfg["dpi"]["url"],
                                                       "field": f"Year {y0}, '{bcfg['dpi']['ranking']}', 'Personal income', 0-20% .. 80-100%",
                                                       "note": f"the components give [{', '.join(f'{x:.3f}' for x in mine)}]; "
                                                               f"top 1% published {top1_pub:.3f}"}},
        "grossIncome": {"byGroup": [r3(v) for v in gross], "approx": True,
                        "computed": "personal income + contributions for government social insurance (the floor height in M1)",
                        "source": dpi_src, "url": bcfg["dpi"]["url"], "note": approx_note},
        "disposableIncome": {"byGroup": [r3(v) for v in dpi_g], "nipaTotal": dpi_t, "approx": True,
                             "computed": "personal income less personal current taxes", "source": dpi_src,
                             "url": bcfg["dpi"]["url"], "note": approx_note},
    }
    for s in bcfg["joint"]["series"]:
        if s["id"] == "disposableIncome":
            continue
        total, vals = jvals[s["id"]]
        derived[s["id"]] = {"byGroup": [r3(v) for v in vals], "nipaTotal": total, "approx": True,
                            "shares": [r6(x) for x in spend["shares"][s["id"]]],
                            "source": joint_src, "url": bcfg["joint"]["url"],
                            "field": f"sheet '{bcfg['joint']['sheet']}', Year {y0}, Series '{s['series']}', deciles (normalised; raw "
                                     f"sum {spend['raw'][s['id']]:.3f}) and 'Top 1%'",
                            "note": f"≈ shares of {y0} carried to {year}; {bcfg['joint']['rankingNote']}, so these groups "
                                    f"are ranked a little differently from the income components"}
    derived["otherOutlays"] = {"byGroup": [r3(v) for v in other], "approx": True,
                               "computed": "disposable income less PCE less saving: personal interest payments and transfer payments",
                               "nipaTotal": {"value": r3(paid), "unit": "$B", "year": year,
                                             "source": "BEA NIPA Table 2.1, lines 30 + 31 (personal interest payments + personal current transfer payments)",
                                             "url": N["url"], "field": f"NipaDataA.txt B069RC + W211RC, {year}"},
                               "source": f"{dpi_src}; {joint_src}", "url": bcfg["joint"]["url"], "note": "≈ a residual of the group statements"}
    ASSUMED.append(f"group flows: BEA {y0} shares (income components; consumption and saving) carried to {year} and scaled to "
                   f"NIPA {year} totals (≈)")

    # the same shares on circuit.json's totals (the vintage before BEA's annual update, which the spec quotes)
    circuit_vintage = None
    cv = bcfg.get("circuitVintage")
    if cv:
        per = circuit["personal"]
        comps = bcfg["dpi"]["components"]
        tot_c = {c["id"]: per[cv["components"][c["id"]]] for c in comps}
        values_c = {cid: [s * tot_c[cid] for s in inc["shares"][cid]] for cid in tot_c}
        pce_c = [s * per[cv["pce"]] for s in spend["shares"]["pce"]]
        paid_c = sum(per[k] for k in cv["otherOutlays"])
        # saving is the residual here: BEA's saving shares do not survive the revision of saving between the vintages
        # (scaled to circuit.json's total they leave the bottom groups negative interest and transfers paid), so interest
        # and transfers paid are split as in the main block and saving = disposable income - PCE - those
        okey = [x / sum(other) for x in other]
        dpi_pre = [sum(values_c[c["id"]][i] for c in comps if c["role"] not in ("deducted", "tax"))
                   - values_c["contributions"][i] - values_c["taxes"][i] for i in range(len(gids))]
        sav_c = [dpi_pre[i] - pce_c[i] - okey[i] * paid_c for i in range(len(gids))]
        sav_shares_scaled = [s * per[cv["saving"]] for s in spend["shares"]["saving"]]
        pi_c, gross_c, dpi_c, other_c, stmt_c = group_statements(groups, comps, values_c, pce_c, sav_c)
        sums_c = [("personal income", sum(pi_c), per[cv["personalIncome"]]),
                  ("gross income", sum(gross_c), per[cv["personalIncome"]] + per[cv["components"]["contributions"]]),
                  ("disposable income", sum(dpi_c), per[cv["disposableIncome"]]), ("PCE", sum(pce_c), per[cv["pce"]]),
                  ("saving", sum(sav_c), per[cv["saving"]]), ("interest and transfers paid", sum(other_c), paid_c)]
        worst_c = max([abs(sum(values_c[cid]) - tot_c[cid]) / abs(tot_c[cid]) for cid in tot_c] +
                      [abs(a - b) / abs(b) for _, a, b in sums_c])
        neg_c = [f"{groups[i]['name']} {other_c[i]:.1f}" for i in range(len(gids)) if other_c[i] < 0]
        neg_naive = [f"{groups[i]['name']} {dpi_c[i] - pce_c[i] - sav_shares_scaled[i]:.1f}" for i in range(len(gids))
                     if dpi_c[i] - pce_c[i] - sav_shares_scaled[i] < 0]
        check("flows-circuit-vintage", worst_c <= TOTALS_TOL and not neg_c,
              f"the same group flows on circuit.json's {year} totals (the pre-update vintage the spec quotes) sum to them "
              f"within {TOTALS_TOL:.1%}: largest gap {worst_c:.4%}; class gross income ${sum(gross_c) / 1000:,.2f}T "
              f"(current vintage ${sum(gross) / 1000:,.2f}T), PCE ${sum(pce_c) / 1000:,.2f}T; saving is the residual "
              f"[{', '.join(f'{x:,.1f}' for x in sav_c)}] ($B; BEA's saving shares on this total would leave "
              + (f"{'; '.join(neg_naive)} of interest and transfers paid" if neg_naive else "no negative outlays") + ")"
              + (f"; NEGATIVE interest and transfers paid: {'; '.join(neg_c)}" if neg_c else ""))
        keys = [cv["components"][c["id"]] for c in comps] + [cv["pce"], cv["saving"], cv["personalIncome"],
                                                             cv["disposableIncome"]] + list(cv["otherOutlays"])
        circuit_vintage = {
            "approx": True,
            "source": f"the group shares of 'components' and 'derived' ({dpi_src}; {joint_src}) x circuit.json's {year} "
                      f"personal block ({circuit['personalNotes']['basis']})",
            "url": cv["file"],
            "field": "circuit.json personal." + ", personal.".join(keys),
            "computed": "income components and PCE: each group's share (classes.flows.components[].shares, "
                        "derived.pce.shares) x the circuit.json total; other outlays (interest and transfers paid): the "
                        "circuit.json total split as the main block's derived.otherOutlays; saving: the residual, disposable "
                        "income - PCE - other outlays; the statement is assembled from these as the main block's is (see "
                        "classes.flows.computed)",
            "savingMethod": "residual here, unlike the main block (BEA's saving shares x NIPA saving, other outlays the "
                            f"residual): circuit.json's saving (${per[cv['saving']]:,.1f}B) is "
                            f"{1 - per[cv['saving']] / sav_t['value']:.0%} below the current vintage's "
                            f"(${sav_t['value']:,.1f}B), and BEA's saving shares on it would leave "
                            + ("; ".join(neg_naive) if neg_naive else "no group") + " of interest and transfers paid ($B)",
            "savingFromBeaShares": [r3(v) for v in sav_shares_scaled],
            "why": cv["why"],
            "totals": {**{cid: tot_c[cid] for cid in tot_c}, "personalIncome": per[cv["personalIncome"]],
                       "disposableIncome": per[cv["disposableIncome"]], "pce": per[cv["pce"]], "saving": per[cv["saving"]],
                       "otherOutlays": r3(paid_c)},
            "components": {cid: [r3(v) for v in values_c[cid]] for cid in tot_c},
            "derived": {"personalIncome": [r3(v) for v in pi_c], "grossIncome": [r3(v) for v in gross_c],
                        "disposableIncome": [r3(v) for v in dpi_c], "pce": [r3(v) for v in pce_c],
                        "saving": [r3(v) for v in sav_c], "otherOutlays": [r3(v) for v in other_c]},
            "statement": stmt_c,
            "note": f"≈ the same splits as the main block (BEA {y0} shares carried to {year}) on circuit.json's totals, with "
                    f"saving as the residual (savingMethod; savingFromBeaShares shows BEA's shares on this total for "
                    f"comparison). The main block (components, derived, statement) uses the current NIPA flat files; M1 "
                    f"should draw one vintage throughout, and the spec owner picks which.",
        }
        ASSUMED.append(f"group flows on circuit.json's {year} totals (classes.flows.circuitVintage): the same BEA {y0} shares; "
                       f"interest and transfers paid split as in the main block, saving the residual (≈)")
    # every BEA year, for the year chip: income shares 2000-latest, consumption and saving 2000-latest
    years_inc = sorted({k[0] for k in dpi})
    years_sp = sorted({k[0] for k in joint})
    by_inc, notes = {}, {}
    for y in years_inc:
        r = income_shares(y, dpi, dfa_raw, src)
        if r is None:
            continue
        by_inc[y] = r
        if r["steps"].get("personalIncomeTop1Interval"):
            notes[str(y)] = (f"BEA gives the top 1% as an interval for {y} (provisional): personal income "
                             f"{r['steps']['personalIncomeTop1Interval']}, disposable income "
                             f"{r['steps'].get('disposableIncomeTop1Interval')}; the midpoints are used (≈)")
    by_sp = {y: spending_shares(y, joint, src) for y in years_sp}
    by_sp = {y: v for y, v in by_sp.items() if v}
    yi, ys = sorted(by_inc), sorted(by_sp)
    by_year = {
        "approx": True,
        "source": f"{dpi_src}; {joint_src}", "url": bcfg["dpi"]["url"],
        "field": "the same rows as 'components' and 'derived', every year the files hold; values[i] is year first + i, each a list "
                 "of six group shares in 'groups' order",
        "income": {"years": [yi[0], yi[-1]],
                   "components": {c["id"]: [[r6(x) for x in by_inc[y]["shares"][c["id"]]] for y in yi] for c in bcfg["dpi"]["components"]}},
        "spending": {"years": [ys[0], ys[-1]],
                     "series": {s["id"]: [[r6(x) for x in by_sp[y]["shares"][s["id"]]] for y in ys] for s in bcfg["joint"]["series"]}},
        "top1Method": sorted({by_inc[y]["steps"]["method"] for y in yi}),
        "notes": notes,
    }
    if notes:
        ASSUMED.append("BEA's provisional-year top-1% intervals: midpoints used (≈) in classes.flows.byYear")
    steps = dict(inc["steps"])
    t1 = src["top1"]
    sensitivity = None
    if steps["method"] == "fallback":
        FAILED.append({"source": "CBO, The Distribution of Household Income (top 1% by income source)", "why": t1["cbo"]["why"],
                       "fallback": t1["fallback"]["summary"]})
        tax_concept = next(c["concept"] for c in bcfg["dpi"]["components"] if c["id"] == "taxes")
        bea_tax = num(dpi.get((y0, "", tax_concept, "Total ($Billions)")))[0]
        per_2025 = steps["rankingMix"]["taxesPerTenthPointOfDisposableIncomeShare"] / bea_tax * components["taxes"]["nipaTotal"]["value"]
        sensitivity = {"value": r3(per_2025), "unit": "$B a year", "year": year, "approx": True, "source": dpi_src,
                       "url": bcfg["dpi"]["url"],
                       "computed": f"steps.rankingMix.taxesPerTenthPointOfDisposableIncomeShare / BEA {y0} '{tax_concept}' total "
                                   f"(${bea_tax:,.0f}B) x NIPA {year} personal current taxes",
                       "note": f"≈ how much the top 1%'s {year} taxes (components.taxes.byGroup[5]) move per 0.1 pt of its "
                               f"disposable-income share; the mixed rankings (steps.rankingMix) likely understate them"}
        ASSUMED.append("top 1%'s part of the top fifth: owners' income in proportion to DFA holdings, benefits per household, "
                       "taxes = BEA top-1% personal income (households ranked by personal income) less top-1% disposable "
                       "income (ranked by disposable income: two rankings mixed, which likely understates the taxes; "
                       f"${per_2025:,.1f}B per 0.1 pt of disposable-income share in {year}), pay the remainder (≈)")
    top1 = {"method": steps["method"], "steps": steps, "approx": True,
            "source": f"{dpi_src}; {src['dfa']['source']}", "url": bcfg["dpi"]["url"],
            "computed": t1["fallback"]["summary"] if steps["method"] == "fallback" else "CBO's top-1% shares of the top fifth, read by hand",
            "keyNotes": t1["fallback"]["keyNotes"] if steps["method"] == "fallback" else None,
            "taxesSensitivity": sensitivity,
            "note": "≈ the top 1%'s split of each flow; BEA publishes only its share of total income"}
    return {
        "sourceYear": y0, "year": year, "approx": True,
        "source": f"{dpi_src}; {joint_src}", "url": bcfg["dpi"]["url"],
        "computed": STATEMENT_COMPUTED,
        "why": bcfg["sourceYearWhy"],
        "ranking": {"income": bcfg["dpi"]["ranking"], "incomeWhy": bcfg["dpi"]["rankingWhy"],
                    "spending": bcfg["joint"]["ranking"], "spendingWhy": bcfg["joint"]["rankingNote"]},
        "leftOut": bcfg["dpi"]["leftOut"],
        "components": components,
        "derived": derived,
        "statement": statement,
        "statementNote": "per group, $B a year: gross income = pay + owning + state + other = taxes + contributions + spending + "
                         "other outlays + saving (saving < 0 is borrowing or drawing down assets); ≈ throughout",
        "circuitVintage": circuit_vintage,
        "top1": {k: v for k, v in top1.items() if v is not None},
        "byYear": by_year,
    }, {"pub_gap": pub_gap, "dpi_info": dinfo, "joint_info": jinfo, "inc": inc, "spend": spend}


# ----------------------------------------------------------------------------------------------------------------- BLS

def bls_query(api, series, start, end):
    payload = json.dumps({"seriesid": series, "startyear": str(start), "endyear": str(end)}, sort_keys=True).encode("utf-8")
    name = "bls/ce_" + hashlib.sha256(payload).hexdigest()[:16] + ".json"

    def ok(b):
        try:
            d = json.loads(b)
            return d.get("status") == "REQUEST_SUCCEEDED" and d.get("Results", {}).get("series")
        except ValueError:
            return False

    body, info = fetch(api, name, data=payload, content_type="application/json", check=ok)
    out = {}
    for s in json.loads(body)["Results"]["series"]:
        for d in s.get("data", []):
            if d.get("period") == "A01":
                v, _ = num(d.get("value"))
                if v is not None:
                    out.setdefault(s["seriesID"], {})[int(d["year"])] = v
    return out, info, name


def build_ce(src, groups):
    cfg = src["ce"]
    codes = [cfg["total"]["code"], cfg["income"]["code"]] + [i["code"] for i in cfg["items"]]
    chars = {"all": cfg["all"], **cfg["quintiles"]}
    series = [cfg["pattern"].format(item=c, group=g) for c in codes for g in chars.values()]
    values, names = {}, []
    y0, y1 = min(cfg["years"]), max(cfg["years"])
    for i in range(0, len(series), 25):
        part, info, name = bls_query(cfg["api"], series[i:i + 25], y0, y1)
        values.update(part)
        names.append(name)
    by_year, worst = {}, 0.0
    for y in cfg["years"]:
        yr = {}
        for gk, ch in chars.items():
            sid = lambda c: cfg["pattern"].format(item=c, group=ch)  # noqa: E731
            total = values.get(sid(cfg["total"]["code"]), {}).get(y)
            items = {it["code"]: values.get(sid(it["code"]), {}).get(y) for it in cfg["items"]}
            if total is None or any(v is None for v in items.values()):
                raise Unavailable(f"BLS CE {y} {gk}: a series is missing")
            worst = max(worst, abs(sum(items.values()) - total) / total)
            yr[gk] = {"total": total, "income": values.get(sid(cfg["income"]["code"]), {}).get(y), "items": items,
                      "shares": {k: r6(v / total) for k, v in items.items()}}
        by_year[str(y)] = yr
    check("ce-items", worst <= TOTALS_TOL, f"CE categories add up to total expenditures in every fifth, {y0}-{y1}: largest gap {worst:.3%}")
    latest = str(y1)
    sy = str(cfg["sixGroupsYear"])
    keys = {"02": "bottom20", "03": "p20_40", "04": "p40_60", "05": "p60_80", "06": "top20"}
    six = {g["id"]: by_year[sy][keys[g["ce"]]]["shares"] for g in groups}
    # the same CE vintage as spending.json byQuintile, which M1 also reads: income before taxes per fifth must agree
    sp = load_json(os.path.join(ECON, "spending.json"))
    theirs = [q.get("incomeBeforeTaxes") for q in sp.get("byQuintile", [])]
    mine = [by_year[sy][k]["income"] for k in ("bottom20", "p20_40", "p40_60", "p60_80", "top20")]
    same = len(theirs) == 5 and all(a is not None and b is not None and abs(a - b) < 0.5 for a, b in zip(mine, theirs))
    check("ce-vintage", same, f"sixGroups uses CE {sy}, the year MODALITIES.md names and spending.json byQuintile holds: "
                              f"income before taxes by fifth [{', '.join(f'{x:,.0f}' for x in mine)}] "
                              + ("equals" if same else "differs from") + " spending.json byQuintile "
                              f"[{', '.join(f'{x:,.0f}' if x is not None else 'none' for x in theirs)}]; CE {latest} stored beside it")
    ASSUMED.append(f"spending mix of 80-99% and the top 1%: {cfg['top1Assumption']} (≈)")
    return {
        "source": cfg["source"], "url": cfg["api"], "page": cfg["page"],
        "field": f"BLS Public Data API v1, series {cfg['pattern']} ({cfg['patternNote']}), annual (period A01), {y0}-{y1}",
        "why": cfg["why"],
        "retrieved": FETCHED[names[0]]["retrieved"],
        "unit": "$ per consumer unit a year (means); shares of total expenditures",
        "items": cfg["items"],
        "notPurchases": cfg["notPurchasesNote"],
        "byQuintile": by_year,
        "latestYear": int(latest),
        "sixGroups": {"year": int(sy), "approx": True, "shares": six, "note": "≈ " + cfg["top1Assumption"]},
        "specNote": cfg["sixGroupsYearWhy"],
    }


# ------------------------------------------------------------------------------------------------------ profits by tier

def profits_by_tier(src, N, industries):
    cfg = src["profitsCrosswalk"]
    year = src["nipa"]["year"]
    tid = cfg["table"]
    v = lambda ln: nipa_value(N, f"{tid}:{ln}", year)  # noqa: E731
    total = v(cfg["total"])
    tiers, lines_out = [], []
    for t in cfg["tiers"]:
        val = sum(v(ln) for ln in t["plus"]) - sum(v(ln) for ln in t["minus"])
        tiers.append({"tier": t["tier"], "value": r3(val), "unit": "$B", "year": year, "share": r6(val / total),
                      "lines": {"plus": t["plus"], "minus": t["minus"]}, "names": t["names"],
                      "source": f"BEA NIPA {N['titles'].get(tid, tid)}", "url": N["url"],
                      "computed": " + ".join(f"line {ln}" for ln in t["plus"]) + "".join(f" - line {ln}" for ln in t["minus"]),
                      "note": "assumed: mapping"})
        lines_out.append(f"{t['tier']} = {t['names']}")
    s = sum(t["value"] for t in tiers)
    ok = len(tiers) == 4 and abs(s - total) < 0.01
    shares = " · ".join(f"{t['tier']} {money(t['value'])} ({t['share']:.1%})" for t in tiers)
    check("profits-tiers", ok, f"6.16D profits {year} mapped to {len(tiers)} tiers (assumed: mapping; crosswalk below): {shares} "
                               f"= domestic industries {money(total)}", )
    CHECKS[-1]["detail"] = lines_out + [f"note: {x}" for x in cfg["impurities"]]
    ASSUMED.append("profits by tier: the 6.16D-to-tier crosswalk (assumed: mapping)")
    return {"year": year, "assumed": True, "label": cfg["label"], "basis": cfg["basis"],
            "total": nipa_fact(N, f"{tid}:{cfg['total']}", year), "tiers": tiers,
            "crosswalk": cfg["tiers"], "impurities": cfg["impurities"], "tech": cfg["tech"],
            "source": f"BEA NIPA {N['titles'].get(tid, tid)}", "url": N["url"],
            "field": f"NipaDataA.txt, the series on {tid}'s lines listed per tier, {year}"}


# --------------------------------------------------------------------------------------------------------- listed value

def listed_value(src, circuit, industries):
    cfg = src["listedValue"]
    tier_of = {i["id"]: i["tier"] for i in industries["industries"]}
    total = circuit["marketCapTotal"]
    wil = next((s for s in circuit["sources"] if "Wilshire" in s), None)
    wil_src = {"value": total, "unit": "$B", "year": "2026-09-16",
               "source": "Wilshire 5000 total market capitalization, as stored in circuit.json (its source: " + (wil or "circuit.json") + ")",
               "url": "Assets/Resources/Data/economy/circuit.json", "field": "circuit.json marketCapTotal"}
    firms = {}
    for f in circuit["capture"]:
        t = tier_of.get(f["industry"])
        firms.setdefault(t, []).append((f["name"], f["marketCap"]))
    fb_src = {"source": "circuit.json capture (the stored firms' market values, Sept 2026) and marketCapTotal; tiers from industries.json",
              "url": "Assets/Resources/Data/economy/circuit.json"}
    fb_tiers = []
    for t in ("raw", "make", "services", "tech"):
        v = sum(m for _, m in firms.get(t, []))
        fb_tiers.append({"tier": t, "value": r3(v), "unit": "$B", "share": r6(v / total), "firms": [n for n, _ in firms.get(t, [])],
                         **fb_src, "computed": f"sum of the stored firms' marketCap in the {t} tier"})
    split = sum(t["value"] for t in fb_tiers)
    fallback = {"tiers": fb_tiers,
                "notSplit": {"value": r3(total - split), "unit": "$B", "share": r6((total - split) / total), **fb_src,
                             "computed": "marketCapTotal less the stored firms' market values"},
                "count": sum(len(v) for v in firms.values()),
                "source": "circuit.json capture (the 25 stored firms' market values, Sept 2026) over the Wilshire 5000 total; tiers from industries.json",
                "url": "Assets/Resources/Data/economy/circuit.json", "field": "circuit.json capture[].marketCap by industries.json tier",
                "computed": "sum of the stored firms' market values per tier / marketCapTotal",
                "note": "MODALITIES.md section 5.2: until sector weights are sourced, tech is 'at least' its share and the rest is "
                        "drawn dashed as 'not split by industry'"}
    sw = cfg["sectorWeights"]
    gx = cfg["gicsCrosswalk"]
    by_tier_sectors = {t: [s for s, tt in gx["map"].items() if tt == t] for t in ("raw", "make", "services", "tech")}
    gics_detail = ([f"GICS sector -> tier crosswalk ({gx['label']}):"] +
                   [f"{t} = {', '.join(ss)}" for t, ss in by_tier_sectors.items()] +
                   [f"note: {n}" for n in gx["notes"]])
    block = {"total": wil_src, "gicsCrosswalk": cfg["gicsCrosswalk"], "fallback": fallback,
             "source": "S&P Dow Jones Indices GICS sector weights (hand-read) x the Wilshire 5000 total; fallback the 25 stored firms",
             "url": sw["url"], "field": "sources.json listedValue.sectorWeights (hand-read); circuit.json"}
    tech = next(t for t in fb_tiers if t["tier"] == "tech")
    if sw.get("weights"):
        w = sw["weights"]
        s = sum(w.values())
        unknown = [k for k in w if k not in cfg["gicsCrosswalk"]["map"]]
        by_tier = {}
        for k, x in w.items():
            t = cfg["gicsCrosswalk"]["map"].get(k)
            if t:
                by_tier[t] = by_tier.get(t, 0.0) + x
        block["bySector"] = {"value": w, "unit": "% of index market value", "year": sw.get("asOf"), "source": sw.get("index"),
                             "url": sw["url"], "quote": sw.get("quotes"), "page": sw.get("page")}
        block["byTier"] = [{"tier": t, "value": r3(total * by_tier.get(t, 0.0) / s), "unit": "$B", "share": r6(by_tier.get(t, 0.0) / s),
                            "source": f"{sw.get('index')} sector weights (hand-read) x the Wilshire 5000 total", "url": sw["url"],
                            "computed": "sector weights mapped to tiers (assumed: mapping) x the Wilshire total"}
                           for t in ("raw", "make", "services", "tech")]
        check("sector-weights", abs(s - 100) <= WEIGHTS_TOL and not unknown,
              f"sector weights sum to 100 within {WEIGHTS_TOL}: {s:.2f}" + (f"; unmapped {unknown}" if unknown else "") +
              " (assumed: mapping; GICS crosswalk below)")
        CHECKS[-1]["detail"] = gics_detail
        block["status"] = "read"
    else:
        block["status"] = "fallback"
        block["statusWhy"] = sw["why"]
        check("sector-weights", False,
              f"sector weights sum to 100 within {WEIGHTS_TOL}: not run, the weights are not read ({sw['why'].split(';')[0]}); "
              f"listed value falls back to the {fallback['count']} stored firms: tech at least {tech['share']:.1%}, "
              f"{money(fallback['notSplit']['value'] / 1000, 'T')} ({fallback['notSplit']['share']:.1%}) not split by industry; "
              f"this criterion stays open until a person reads the factsheet (GICS crosswalk below, waiting for the weights)",
              status="SKIPPED")
        CHECKS[-1]["detail"] = gics_detail
        FAILED.append({"source": "S&P Dow Jones Indices sector weights (factsheet, to be hand-read)", "why": sw["why"],
                       "fallback": "the 25 stored firms over the Wilshire total (MODALITIES.md section 5.2): tech 'at least', the "
                                   "rest 'not split by industry'"})
    return block


# ---------------------------------------------------------------------------------------------------------- public debt

def tic_table(text, month):
    rows = [ln.split("\t") for ln in text.splitlines()]
    head = next(r for r in rows if r and r[0].strip() == "Country")
    col = [c.strip() for c in head].index(month)
    out, quote = {}, {}
    for r in rows:
        if len(r) > col and r[0].strip() and r[0].strip() != "Country":
            v, _ = num(r[col])
            if v is not None:
                out[r[0].strip()] = v
                quote[r[0].strip()] = " ".join(x.strip() for x in r if x.strip())
    return out, quote


def h41_treasuries(html_text):
    t = re.sub(r"<script.*?</script>|<style.*?</style>", "", html_text, flags=re.S)
    t = re.sub(r"</t[dh]>", " | ", t)
    t = html.unescape(re.sub(r"<[^>]+>", " ", t))
    t = re.sub(r"\s+", " ", t)
    i = t.find("Factors Affecting Reserve Balances of Depository Institutions")
    part = t[i:] if i >= 0 else t
    wed = re.search(r"Wednesday\s*([A-Z][a-z]{2} \d{1,2}, \d{4})", part)
    m = re.search(r"U\.S\. Treasury securities\s*\|\s*([\d,]+)\s*\|\s*([+-]\s*[\d,]+|0)\s*\|\s*([+-]\s*[\d,]+|0)\s*\|\s*([\d,]+)", part)
    if not (wed and m):
        return None
    return {"wednesday": wed.group(1), "weekAverage": float(m.group(1).replace(",", "")),
            "wednesdayLevel": float(m.group(4).replace(",", "")), "quote": re.sub(r"\s*\|\s*", " ", m.group(0)).strip()}


def public_debt(src, N):
    cfg = src["publicDebt"]
    out = {"asOf": cfg["asOf"], "definition": cfg["definition"]}
    d = cfg["debtToThePenny"]

    def penny_ok(b):
        try:
            rows = json.loads(b)["data"]
            return rows and rows[0]["record_date"] == cfg["asOf"]
        except (ValueError, KeyError):
            return False

    body, info = fetch(d["url"], f"treasury/debt_to_penny_{cfg['asOf']}.json", check=penny_ok)
    row = json.loads(body)["data"][0]
    dhp = float(row["debt_held_public_amt"]) / 1e9
    out["heldByPublic"] = {"value": r3(dhp), "unit": "$B", "year": row["record_date"], "source": d["source"], "url": d["url"],
                           "field": f"debt_held_public_amt = {row['debt_held_public_amt']}", "retrieved": info["retrieved"]}
    out["intragovernmental"] = {"value": r3(float(row["intragov_hold_amt"]) / 1e9), "unit": "$B", "year": row["record_date"],
                                "source": d["source"], "url": d["url"], "field": f"intragov_hold_amt = {row['intragov_hold_amt']}"}
    out["total"] = {"value": r3(float(row["tot_pub_debt_out_amt"]) / 1e9), "unit": "$B", "year": row["record_date"],
                    "source": d["source"], "url": d["url"], "field": f"tot_pub_debt_out_amt = {row['tot_pub_debt_out_amt']}"}
    gap = abs(out["heldByPublic"]["value"] + out["intragovernmental"]["value"] - out["total"]["value"])
    check("debt-penny", gap < 0.01, f"Debt to the Penny {row['record_date']}: held by the public {money(dhp)} + intragovernmental = "
                                     f"total within $0.01B (gap ${gap:.4f}B)")
    h = cfg["h41"]
    body, info = fetch(h["url"], "fed/h41_20260903.htm", check=lambda b: b"U.S. Treasury securities" in b)
    fed = h41_treasuries(body.decode("utf-8", "replace"))
    if fed is None:
        raise SystemExit("H.4.1: the Treasury securities row was not found")
    out["federalReserve"] = {"value": r3(fed["wednesdayLevel"] / 1000), "unit": "$B", "year": fed["wednesday"],
                             "source": h["source"], "url": h["url"], "quote": fed["quote"], "retrieved": info["retrieved"],
                             "note": h["note"] + "; the row's cells: average of the week ended " + fed["wednesday"] + ", change from "
                                     "the week before, change from a year earlier, the Wednesday level (millions of dollars)"}
    t = cfg["tic"]
    body, info = fetch(t["url"], "treasury/slt_table5.txt", check=lambda b: b"Grand Total" in b)
    rows, quotes = tic_table(body.decode("utf-8", "replace"), t["month"])
    foreign = rows["Grand Total"]
    out["foreign"] = {"value": foreign, "unit": "$B", "year": t["month"], "source": t["source"], "url": t["url"],
                      "quote": quotes["Grand Total"], "retrieved": info["retrieved"],
                      "published": http_date(info.get("lastModified")),
                      "note": "a 2026 fact, fetched and quoted (whitespace-normalised row of the table); TIC records the "
                              "custodian's country, so holdings in overseas custody may not be attributed to their owners"}
    official = next((k for k in rows if k.startswith("Of Which: Foreign Official") and "Bills" not in k and "Bonds" not in k), None)
    if official:
        out["foreignOfficial"] = {"value": rows[official], "unit": "$B", "year": t["month"], "source": t["source"], "url": t["url"],
                                  "quote": quotes[official]}
    out["foreignByCountry"] = {"value": {k: v for k, v in rows.items() if not k.startswith("Of Which") and k != "Grand Total"},
                               "unit": "$B", "year": t["month"], "source": t["source"], "url": t["url"],
                               "field": f"the {t['month']} column of every country row"}
    others = dhp - out["federalReserve"]["value"] - foreign
    out["otherUsHolders"] = {"value": r3(others), "unit": "$B", "year": cfg["asOf"], "approx": True,
                             "source": "held by the public less the Federal Reserve less foreign holders", "url": d["url"],
                             "computed": "heldByPublic - federalReserve - foreign",
                             "note": "≈ a residual: banks, funds, pensions, insurers, households, state and local governments; the "
                                     "three inputs differ in date (31 Aug, 2 Sep, end-Aug) and valuation (TIC builds on "
                                     "reported fair values)"}
    shares = {k: r6(out[k]["value"] / dhp) for k in ("federalReserve", "foreign", "otherUsHolders")}
    out["shares"] = {"value": shares, "unit": "share of debt held by the public", "year": cfg["asOf"], "source": "computed from the three holders",
                     "url": d["url"], "computed": "holder / heldByPublic"}
    ASSUMED.append("public debt: other US holders = held by the public - Fed - foreign (≈ residual)")
    out["interestByHolder"] = {
        "federal": nipa_fact(N, "T30200:33", src["nipa"]["year"]),
        "federalToResidents": nipa_fact(N, "T30200:34", src["nipa"]["year"]),
        "federalToNonresidents": nipa_fact(N, "T30200:35", src["nipa"]["year"]),
        "allGovernmentToPersonsAndBusiness": nipa_fact(N, "T30100:28", src["nipa"]["year"]),
        "allGovernmentToNonresidents": nipa_fact(N, "T30100:29", src["nipa"]["year"]),
    }
    check("debt-holders", others > 0, f"federal debt held by the public {cfg['asOf']} {money(dhp)}: Federal Reserve "
                                       f"{money(out['federalReserve']['value'])} ({shares['federalReserve']:.1%}), foreign "
                                       f"{money(foreign)} ({shares['foreign']:.1%}, TIC {t['month']}), other US holders "
                                       f"{money(others)} ({shares['otherUsHolders']:.1%}, residual)")
    return out


# ------------------------------------------------------------------------------------------------------- hyperscalers

def hyperscaler_capex(src, corp):
    cfg = src["hyperscalers"]
    data = corp["economy"]["hyperscalerCapex"]
    comps, headline = [], 0.0
    for f in cfg["firms"]:
        e = data[f["id"]][f["entry"]]
        field = f"corporate-programs.json economy.hyperscalerCapex.{f['id']}.{f['entry']}"
        # url is the file the value and quote are read from; the filing it cites is kept apart (filingUrl), because the
        # quoted text is corporate-programs.json's own note, not necessarily the filing's wording
        rec = {"id": f["id"], "name": f["name"], "unit": "$B", "year": e["year"],
               "source": f"{f['name']} filings, as stored in corporate-programs.json", "url": cfg["file"],
               "filingUrl": e["source"]}
        if f["cash"]["from"] == "value":
            rec["value"] = e["value"]
            rec["field"] = field + ".value"
        else:
            m = re.search(f["cash"]["pattern"], e["note"])
            if not m:
                raise SystemExit(f"hyperscalers: the cash figure of {f['name']} was not found in its note")
            if f["cash"].get("stated"):
                parts = [int(x.replace(",", "")) for x in m.groups()[:f["cash"]["stated"] - 1]]
                stated = float(m.group(f["cash"]["stated"]))
                rec["value"] = round(sum(parts) / 1000, 3)
                if abs(rec["value"] - stated) > 0.05:
                    PROBLEMS.append(f"{f['name']}: quarters sum to {rec['value']} but the note states {stated}")
            else:
                rec["value"] = round(int(m.group(1).replace(",", "")) / 1000, 3)
            rec["quote"] = m.group(0)
            rec["field"] = field + ".note"
        rec["headline"] = {"value": e["value"], "unit": "$B", "source": rec["source"], "url": cfg["file"],
                           "filingUrl": e["source"], "field": field + ".value", "note": f["headlineNote"]}
        headline += e["value"]
        comps.append(rec)
    total = sum(c["value"] for c in comps)
    four = sum(c["value"] for c in comps if c["id"] != "oracle")
    check("hyperscaler-capex", len(comps) == 5,
          f"one hyperscaler capex value with its definition: {money(total)} in calendar 2025 for the five firms, {cfg['definition']} "
          f"(each firm's own headline measure sums to {money(headline)}; the four with calendar quarters, {money(four)})")
    return {"value": r3(total), "unit": "$B", "year": 2025, "definition": cfg["definition"], "why": cfg["why"],
            "source": "Microsoft, Alphabet, Amazon, Meta and Oracle filings as stored in corporate-programs.json",
            "url": "Assets/Resources/Data/economy/corporate-programs.json", "field": f"{cfg['path']} (the entries per firm below)",
            "computed": "sum of the five firms' cash purchases of property and equipment",
            "firms": comps,
            "alternatives": [
                {"value": r3(headline), "unit": "$B", "definition": "each firm's own headline measure (Microsoft's and Meta's include finance leases)",
                 "source": "corporate-programs.json", "url": "Assets/Resources/Data/economy/corporate-programs.json",
                 "computed": "sum of the five firms' headline values", "note": "the $449B in THEORY.md"},
                {"value": r3(four), "unit": "$B", "definition": "the four firms with calendar fiscal quarters, cash purchases (finance leases out)",
                 "source": "corporate-programs.json", "url": "Assets/Resources/Data/economy/corporate-programs.json",
                 "computed": "the reconciled value less Oracle"},
            ],
            "otherFigures": cfg["otherFigures"]}


# ---------------------------------------------------------------------------------------------------------------- build

META_KEYS = {"year", "years", "first", "sourceYear", "latestYear"}  # numbers that date or index values, not values


def has_provenance(x):
    return isinstance(x, dict) and bool(x.get("source") and x.get("url") and (x.get("quote") or x.get("field") or x.get("computed")))


def validate(doc):
    """Every value object has a source and a url with a quote, an API field or a computation; so does every listed table
    and block; and every number in the file (other than dates and indices) has that provenance on its own object or on
    an enclosing one."""
    count, bad, leaves, orphans = [0], [], [0], []

    def walk(x, path, covered, key=None):
        if isinstance(x, dict):
            if "value" in x and not isinstance(x.get("value"), list):
                count[0] += 1
                if not has_provenance(x):
                    bad.append(path)
            here = covered or has_provenance(x)
            for k, v in x.items():
                walk(v, f"{path}.{k}", here, k)
        elif isinstance(x, list):
            for i, v in enumerate(x):
                walk(v, f"{path}[{i}]", covered, key)
        elif isinstance(x, (int, float)) and not isinstance(x, bool) and key not in META_KEYS:
            leaves[0] += 1
            if not covered:
                orphans.append(path)

    walk(doc, "$", False)
    blocks = [("nipa.tables." + k, v) for k, v in doc.get("nipa", {}).get("tables", {}).items()]
    cls = doc.get("classes", {})
    blocks += [("classes.dfa", cls.get("dfa"))]
    if cls.get("flows"):
        fl = cls["flows"]
        blocks += [("classes.flows (its statement)", fl)]
        blocks += [("classes.flows.components." + k, v) for k, v in fl["components"].items()]
        blocks += [("classes.flows.derived." + k, v) for k, v in fl["derived"].items()]
        blocks += [("classes.flows.byYear", fl["byYear"]), ("classes.flows.top1", fl["top1"])]
        if fl.get("circuitVintage"):
            blocks += [("classes.flows.circuitVintage", fl["circuitVintage"])]
    if cls.get("spendingMix"):
        blocks += [("classes.spendingMix", cls["spendingMix"])]
    blocks += [("profitsByTier", doc.get("profitsByTier")), ("listedValue", doc.get("listedValue")),
               ("hyperscalerCapex", doc.get("hyperscalerCapex")), ("vintageComparison", doc.get("vintageComparison"))]
    for path, b in blocks:
        if not has_provenance(b):
            bad.append(path)
    return {"values": count[0], "blocks": len(blocks), "bad": bad, "numbers": leaves[0], "orphans": orphans}


def _norm(s):
    return re.sub(r"\s+", " ", s).strip()


def check_quotes(doc):
    """Every quote must appear verbatim (whitespace-normalised) in the document its url names: a repository file (its
    strings), or a cached download (HTML without tags; a zip by its text files). Returns (verified, problems, unchecked)."""
    by_url = {}
    for name, info in FETCHED.items():
        by_url.setdefault(info["url"], name)
    texts = {}

    def text_of(url):
        if url in texts:
            return texts[url]
        t = None
        if not re.match(r"https?://", url):
            path = os.path.join(ROOT, url)
            if os.path.exists(path):
                with open(path, encoding="utf-8") as f:
                    raw = f.read()
                strings = []

                def strs(x):
                    if isinstance(x, str):
                        strings.append(x)
                    elif isinstance(x, dict):
                        for v in x.values():
                            strs(v)
                    elif isinstance(x, list):
                        for v in x:
                            strs(v)
                try:
                    strs(json.loads(raw))
                    t = "\n".join(strings)
                except ValueError:
                    t = raw
        elif url in by_url:
            with open(os.path.join(CACHE, by_url[url]), "rb") as f:
                body = f.read()
            if body[:2] == b"PK":
                z = zipfile.ZipFile(io.BytesIO(body))
                t = "\n".join(z.read(n).decode("utf-8", "replace") for n in z.namelist() if n.lower().endswith(".txt"))
            else:
                t = body.decode("utf-8", "replace")
                if "<html" in t[:2000].lower() or url.endswith((".htm", ".html", "/")):
                    t = re.sub(r"<script.*?</script>|<style.*?</style>", "", t, flags=re.S)
                    t = html.unescape(re.sub(r"<[^>]+>", " ", t))
        texts[url] = None if t is None else _norm(t)
        return texts[url]

    verified, problems, unchecked = [0], [], []

    def walk(x, path):
        if isinstance(x, dict):
            q = x.get("quote")
            if q and x.get("url"):
                quotes = [q] if isinstance(q, str) else [v for v in q.values() if isinstance(v, str)] if isinstance(q, dict) else []
                t = text_of(x["url"])
                if t is None:
                    unchecked.append(path)
                else:
                    for one in quotes:
                        if _norm(one) in t:
                            verified[0] += 1
                        else:
                            problems.append(f"{path}: {one[:60]!r} not in {x['url']}")
            for k, v in x.items():
                if k != "quote":
                    walk(v, f"{path}.{k}")
        elif isinstance(x, list):
            for i, v in enumerate(x):
                walk(v, f"{path}[{i}]")

    walk(doc, "$")
    return verified[0], problems, unchecked


def dumps(x, ind=0):
    """JSON with one key per line and lists of numbers on one line (deterministic, diff-friendly)."""
    pad = " " * ind
    if isinstance(x, dict):
        if not x:
            return "{}"
        inner = ",\n".join(f"{pad} {json.dumps(k, ensure_ascii=False)}: {dumps(v, ind + 1)}" for k, v in x.items())
        return "{\n" + inner + "\n" + pad + "}"
    if isinstance(x, list):
        if not x:
            return "[]"
        if all(not isinstance(v, (dict, list)) for v in x):
            return "[" + ", ".join(dumps(v) for v in x) + "]"
        inner = ",\n".join(f"{pad} {dumps(v, ind + 1)}" for v in x)
        return "[\n" + inner + "\n" + pad + "]"
    if isinstance(x, float):
        if math.isnan(x) or math.isinf(x):
            raise ValueError("NaN or infinity in the output")
        return json.dumps(round(x, 6))
    return json.dumps(x, ensure_ascii=False)


def vintage_comparison(N, circuit, year):
    pairs = [("pce", "personal", "pce", "T20100:29"), ("personalIncome", "personal", "personalIncome", "T20100:1"),
             ("personalTaxes", "personal", "personalTaxes", "T20100:26"),
             ("socialInsuranceContributions", "personal", "socialInsuranceContributions", "T20100:25"),
             ("governmentSocialBenefits", "personal", "governmentSocialBenefits", "T20100:17"),
             ("compensation", "personal", "compensation", "T20100:2"), ("disposableIncome", "personal", "disposableIncome", "T20100:27"),
             ("personalSaving", "personal", "saving", "T20100:34"), ("gdp", "income", "gdpAnnualUpdate", "T10105:1"),
             ("corporateProfits", "income", "corporateProfitsAnnualUpdate", "T61600D:1"),
             ("productionAndImportTaxes", "income", "productionTaxes", "T30100:4"),
             ("corporateIncomeTaxes", "income", "corporateTaxes", "T30100:5")]
    notes = {"corporateIncomeTaxes": "circuit.json derives corporateTaxes from FRED profits before and after tax (incomeNotes); "
                                     "the NIPA figure is Table 3.1 line 5, taxes on corporate income received by US governments"}
    out = {}
    for name, block, key, ref in pairs:
        c = circuit.get(block, {}).get(key)
        n = nipa_value(N, ref, year)
        if c is None or n is None:
            continue
        out[name] = {"circuit": c, "circuitField": f"{block}.{key}", "nipa": r3(n), "nipaLine": ref, "change": r6(n / c - 1)}
        if name in notes:
            out[name]["note"] = notes[name]
    p = circuit.get("personal", {})
    if p.get("personalIncome") is not None and p.get("socialInsuranceContributions") is not None:
        c = p["personalIncome"] + p["socialInsuranceContributions"]
        n = nipa_value(N, "T20100:1", year) + nipa_value(N, "T20100:25", year)
        out["classGrossIncome"] = {"circuit": r3(c), "circuitField": "personal.personalIncome + personal.socialInsuranceContributions",
                                   "nipa": r3(n), "nipaLine": "T20100:1 + T20100:25", "change": r6(n / c - 1),
                                   "note": "the class tower's total (spec: $28.12T); classes.flows.circuitVintage holds the "
                                           "group flows on circuit.json's totals"}
    return {"note": f"circuit.json holds the {year} accounts before BEA's annual update of 30 September 2026 (its 'vintage'); this file "
                    f"holds the flat files of {N['stamp']}, after it. MODALITIES.md and WP10's acceptance quote the earlier "
                    f"vintage (e.g. PCE $20.95T, class gross income $28.12T). M1 should use one vintage throughout: the spec "
                    f"owner picks; classes.flows.circuitVintage serves the earlier one.",
            "source": "circuit.json personal and income blocks; BEA NIPA flat files", "url": N["url"],
            "field": "circuit.json personal.*, income.gdpAnnualUpdate, .corporateProfitsAnnualUpdate, .productionTaxes, "
                     ".corporateTaxes; NipaDataA.txt (the lines in nipaLine)",
            "items": out}


def main():
    src = load_json(SRC)
    circuit = load_json(os.path.join(ECON, "circuit.json"))
    industries = load_json(os.path.join(ECON, "industries.json"))
    corp = load_json(os.path.join(ECON, "corporate-programs.json"))
    groups = src["classes"]["groups"]
    year = src["nipa"]["year"]

    N = load_nipa(src["nipa"])
    first = src["nipa"]["firstYear"]
    tables = {tid: nipa_table(N, tid, tc, first, year) for tid, tc in src["nipa"]["tables"].items()}
    facts = {k: nipa_fact(N, ref, year) for k, ref in src["nipa"]["facts"].items()}

    # NIPA identities
    rec, exp_, lend = (nipa_value(N, f"T30100:{n}", year) for n in (34, 37, 43))
    check("net-lending", abs(rec - exp_ - lend) <= NET_LENDING_TOL_B,
          f"NIPA {year} total receipts - total expenditures = net lending within ${NET_LENDING_TOL_B:g}B: {rec:,.3f} - {exp_:,.3f} "
          f"= {rec - exp_:,.3f} vs Table 3.1 line 43 {lend:,.3f} (gap ${abs(rec - exp_ - lend):.3f}B); current receipts "
          f"{nipa_value(N, 'T30100:1', year):,.1f}, current expenditures {nipa_value(N, 'T30100:20', year):,.1f}")
    pi_parts = sum(nipa_value(N, f"T20100:{n}", year) for n in (2, 9, 12, 14, 15, 17, 24)) - nipa_value(N, "T20100:25", year)
    ca = nipa_value(N, "T40100:1", year) - nipa_value(N, "T40100:17", year)
    ind = sum(nipa_value(N, f"T61600D:{n}", year) for n in (10, 14, 15, 28, 29, 30, 31, 32))
    gaps = [abs(pi_parts - nipa_value(N, "T20100:1", year)), abs(ca - nipa_value(N, "T40100:33", year)),
            abs(nipa_value(N, "T10105:16", year) - nipa_value(N, "T10105:19", year) - nipa_value(N, "T10105:15", year)),
            abs(ind - nipa_value(N, "T61600D:9", year))]
    check("nipa-identities", max(gaps) <= 0.01 * 10,
          f"NIPA {year} identities hold: personal income = its components, current account = receipts - payments, net exports = "
          f"exports - imports, 6.16D industries = domestic total; largest gap ${max(gaps):.3f}B")

    dfa_block, basis, dfa_raw = build_dfa(src, circuit)
    classes = {"basis": src["classes"]["basis"] if basis == "income" else "household wealth rank, the DFA's four wealth groups (fallback)",
               "classBasis": basis,
               "groups": [{"id": g["id"], "name": g["name"], "short": g["short"]} for g in groups] if basis == "income"
               else [{"id": g["id"], "name": g["name"]} for g in src["classes"]["wealthFallback"]],
               "dfa": dfa_block}
    pub_gap = None
    if basis == "income":
        flows, extra = build_flows(src, N, dfa_raw, groups, circuit)
        classes["flows"] = flows
        pub_gap = extra["pub_gap"]
        try:
            classes["spendingMix"] = build_ce(src, groups)
        except Unavailable as e:
            FAILED.append({"source": "BLS Consumer Expenditure Survey via the BLS Public Data API", "why": str(e),
                           "fallback": "no spending mix in this file; spending.json byQuintile (CE 2024) holds the scene's seven categories"})
            classes["spendingMix"] = None
            check("ce-items", False, "CE spending mix not built (see the failed sources)", status="SKIPPED")
    else:
        classes["flows"] = None
        classes["flowsNote"] = "flows by wealth group: use circuit.json groups (copula-moved shares); BEA measures flows by income group"
        check("flows-totals", False, "flows by group not built (wealth-group fallback)", status="SKIPPED")

    profits = profits_by_tier(src, N, industries)
    listed = listed_value(src, circuit, industries)
    debt = public_debt(src, N)
    capex = hyperscaler_capex(src, corp)

    built = max(info["retrieved"] for info in FETCHED.values())
    doc = {
        "note": "The US economy at one instant, for the view 'One year, all at once' (MODALITIES.md section 5): the classes ranked by "
                "household income (Fed DFA's six income groups) with their balance sheets and their year's income, taxes, spending "
                "and saving; the state's accounts; corporate profits by tier; listed value; federal debt by holder; and one "
                "reconciled figure for the hyperscalers' capital spending. Built by Tools/instant/build.py from Tools/instant/"
                "sources.json and the sources fetched there; Docs/data/instant.md lists every series. Every value carries its "
                "source and a quote or API field; 'approx': true and a note starting with '≈' mark modelled splits, never "
                "measurements; 'assumed' marks drawing choices (the crosswalks to tiers).",
        "built": built,
        "year": year,
        "classBasis": basis,
        "units": "$B (billions of current US dollars) unless a block says otherwise; shares 0..1; households in millions",
        "classes": classes,
        "nipa": {"vintage": f"BEA NIPA flat files dated {N['stamp']} (after the annual update of 30 September 2026)",
                 "source": src["nipa"]["source"], "url": N["url"], "retrieved": N["info"]["retrieved"],
                 "facts": facts, "tables": tables},
        "profitsByTier": profits,
        "listedValue": listed,
        "publicDebt": debt,
        "hyperscalerCapex": capex,
        "vintageComparison": vintage_comparison(N, circuit, year),
    }
    v = validate(doc)
    check("provenance", not v["bad"] and not v["orphans"],
          f"every value has a source and a url with a quote, an API field or a computation: {v['values']} values, "
          f"{v['blocks']} tables and blocks; every one of the file's {v['numbers']:,} numbers (dates and indices aside) has "
          f"that provenance on itself or an enclosing block" + (f"; missing at {v['bad'][:5]}" if v["bad"] else "") +
          (f"; {len(v['orphans'])} numbers without, e.g. {v['orphans'][:5]}" if v["orphans"] else ""))
    nq, qprob, qskip = check_quotes(doc)
    check("quotes", not qprob,
          f"every quote is found verbatim (whitespace-normalised) in the document its url names: {nq} verified against "
          f"repository files and cached downloads" + (f"; {len(qskip)} not checkable ({', '.join(qskip[:3])})" if qskip else "") +
          (f"; NOT FOUND: {'; '.join(qprob[:3])}" if qprob else ""))
    doc["sources"] = [{"name": k, "url": v["url"], "retrieved": v["retrieved"], "lastModified": http_date(v.get("lastModified"))}
                      for k, v in sorted(FETCHED.items())]
    doc["failedSources"] = FAILED
    doc["assumptions"] = ASSUMED
    doc["checks"] = [{k: c[k] for k in ("id", "status", "line")} for c in CHECKS]
    if pub_gap is not None:
        doc["classes"]["flows"]["derived"]["personalIncome"]["publishedQuintileShares"]["largestGap"] = r6(pub_gap)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    text = dumps(doc) + "\n"
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(text)

    print(f"wrote {os.path.relpath(OUT, ROOT)}: {len(text.encode('utf-8')):,} bytes, sha256 "
          f"{hashlib.sha256(text.encode('utf-8')).hexdigest()}{' (offline)' if OFFLINE else ''}")
    print(f"classes: {basis} basis, DFA {dfa_block['latest']['quarter']}; NIPA {N['stamp']}; built {built}")
    for c in CHECKS:
        print(f"CHECK {c['status']:<7} {c['line']}")
        for d in c.get("detail", []):
            print(f"        {d}")
    for f in FAILED:
        print(f"SOURCE FAILED  {f['source']}: {f['why']}")
        print(f"               fallback: {f['fallback']}")
    for a in ASSUMED:
        print(f"ASSUMED ≈  {a}")
    for p in PROBLEMS:
        print(f"PROBLEM  {p}")
    failed = [c for c in CHECKS if c["status"] == "FAIL"]
    print(f"checks: {sum(c['status'] == 'PASS' for c in CHECKS)} passed, {len(failed)} failed, "
          f"{sum(c['status'] == 'SKIPPED' for c in CHECKS)} skipped" + (f" ({', '.join(c['id'] for c in failed)})" if failed else ""))
    if failed or PROBLEMS:
        sys.exit(1)


if __name__ == "__main__":
    try:
        main()
    except Unavailable as e:
        raise SystemExit(f"a required source is unavailable: {e}")

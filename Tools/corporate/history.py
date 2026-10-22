#!/usr/bin/env python3
"""Collects Tools/corporate/history.json: the uses of money of the 25 stored firms in every fiscal year that falls mostly
in calendar 2015-2025, so the strategy view can follow each firm's money through the cycle (filings.json keeps the latest
year's researched record, which the crest draws; this is the trail behind it).

Source: the XBRL financial data of each firm's audited annual reports (Form 10-K) on SEC EDGAR, through the SEC's company
facts (https://data.sec.gov/api/xbrl/companyfacts/CIK##########.json): every value a 10-K reported, with its US-GAAP
concept, period, form and accession number. For each firm, year and field the first concept of a list that the firm
reported for that period is taken (CONCEPTS; some firms have their own, FIRM_CONCEPTS), from the latest 10-K that reports
the period, so earlier years read as later filings restated them. Company facts carry standard concepts only: a value a
firm tagged with its own extension concept (EXTENSIONS) is read from the XBRL instance of the latest 10-K that reports
the period. SpaceX filed no annual XBRL: its years come from the audited statements of its 2026 IPO prospectus, entered
by hand with each line's name (SPACEX). Every number keeps its source: the concept and the filing's accession, whose
folder is https://www.sec.gov/Archives/edgar/data/<cik>/<accession without dashes>/ (listed per firm under "filings").

Fields (USD billions): revenue, costOfRevenue, rd (research and development, excluding acquired in-process R&D where the
firm separates it), sga (selling, general and administrative, or its parts summed), capex (cash purchases of property
and equipment), financeLeases (assets taken on under finance leases, for the four firms whose crest counts them),
buybacks, dividends (to common holders where the firm separates them), acquisitions (businesses, net of cash acquired,
as the firm's cash-flow line has them), incomeTaxesPaid (cash), netIncome, stockComp, noninterestExpense (banks). A field
a firm's statements do not have (a bank's capex, Amazon's R&D) is left out; a buyback, dividend or acquisition line a
10-K does not have is a zero; anything else missing, and every year a firm did not file, is named under "gaps".

Downloads are cached under Tools/corporate/.cache/edgar (gitignored). Requests carry a generic User-Agent (no personal
address: the SEC's archive refuses agents without a contact, so it names the reserved example.org domain) and stay well
under the SEC's ten per second.

Usage (from the repository root):
    python3 Tools/corporate/history.py            # fetches what is not cached, writes history.json, reports gaps
    python3 Tools/corporate/history.py --offline  # cache only
"""
import datetime
import json
import os
import re
import sys
import time
import urllib.request
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
FILINGS = os.path.join(HERE, "filings.json")
OUT = os.path.join(HERE, "history.json")
CACHE = os.path.join(HERE, ".cache", "edgar")
USER_AGENT = "why-2 economy research admin@example.org"
OFFLINE = "--offline" in sys.argv
FIRST, LAST = 2015, 2025

# Registrants before the current one (Broadcom Inc. since FY2018: Broadcom Limited for FY2016-17, Avago Technologies
# before); the current registrant's filings win where both report a period.
PREDECESSORS = {"AVGO": [1649338, 1441634]}

# Concepts per field, first match wins (us-gaap). A tuple sums its parts (each must be present).
CONCEPTS = {
    "revenue": ["Revenues", "RevenueFromContractWithCustomerExcludingAssessedTax", "RevenueFromContractWithCustomerIncludingAssessedTax",
                "SalesRevenueNet", "SalesRevenueGoodsNet"],
    "costOfRevenue": ["CostOfRevenue", "CostOfGoodsAndServicesSold", "CostOfGoodsSold"],
    "rd": ["ResearchAndDevelopmentExpenseExcludingAcquiredInProcessCost", "ResearchAndDevelopmentExpense"],
    "sga": ["SellingGeneralAndAdministrativeExpense", ("SellingAndMarketingExpense", "GeneralAndAdministrativeExpense"),
            ("MarketingExpense", "GeneralAndAdministrativeExpense"), ("MarketingAndAdvertisingExpense", "GeneralAndAdministrativeExpense")],
    "capex": ["PaymentsToAcquirePropertyPlantAndEquipment", "PaymentsToAcquireProductiveAssets", "PaymentsToAcquireOtherPropertyPlantAndEquipment"],
    "financeLeases": None,
    "buybacks": ["PaymentsForRepurchaseOfCommonStock"],
    "dividends": ["PaymentsOfDividendsCommonStock", "PaymentsOfDividends", "PaymentsOfOrdinaryDividends"],
    "acquisitions": ["PaymentsToAcquireBusinessesNetOfCashAcquired", "PaymentsToAcquireBusinessesAndInterestInAffiliates",
                     "OtherPaymentsToAcquireBusinesses"],
    "incomeTaxesPaid": ["IncomeTaxesPaidNet", "IncomeTaxesPaid"],
    "netIncome": ["NetIncomeLoss", "ProfitLoss"],
    "stockComp": ["ShareBasedCompensation", "AllocatedShareBasedCompensationExpense"],
    "noninterestExpense": None,
}
FIELDS = list(CONCEPTS)
# Cash a firm either paid or did not: a 10-K without the line paid none that year.
ZERO_IF_ABSENT = {"buybacks", "dividends", "acquisitions"}
LEASES = ["RightOfUseAssetObtainedInExchangeForFinanceLeaseLiability", "CapitalLeaseObligationsIncurred"]
BANK = {"costOfRevenue": None, "rd": None, "sga": None, "capex": None, "noninterestExpense": ["NoninterestExpense"]}

# Each firm's own lists, matching the definitions of its researched record (filings.json); None: the field does not
# apply to its statements.
FIRM_CONCEPTS = {
    "AMZN": {"rd": None, "capex": ["PaymentsToAcquireProductiveAssets", "PaymentsToAcquirePropertyPlantAndEquipment"], "financeLeases": LEASES},
    "MSFT": {"financeLeases": LEASES},
    "GOOGL": {"financeLeases": LEASES},
    "META": {"financeLeases": LEASES},
    "NVDA": {"acquisitions": [("PaymentsToAcquireBusinessesNetOfCashAcquired", "PaymentsToAcquireBusinessTwoNetOfCashAcquired"),
                              "PaymentsToAcquireBusinessesNetOfCashAcquired"]},
    "JPM": dict(BANK, dividends=["PaymentsOfDividends"]),
    "BAC": dict(BANK, dividends=["DividendsCommonStockCash"]),
    "BRK": {"rd": None, "costOfRevenue": None, "sga": None, "stockComp": None},
    "XOM": {"costOfRevenue": None, "stockComp": None},
    "CVX": {"revenue": ["RevenueFromContractWithCustomerExcludingAssessedTax", "RevenueFromContractWithCustomerIncludingAssessedTax", "Revenues"],
            "costOfRevenue": ["CostOfGoodsAndServicesSold", "CostOfPurchasedOilAndGas"], "stockComp": None,
            "capex": ["PaymentsToAcquireProductiveAssets", "PaymentsToAcquireOilAndGasPropertyAndEquipment"]},
    "ORCL": {"costOfRevenue": None},
    "WMT": {"rd": None},
    "COST": {"rd": None},
    "V": {"costOfRevenue": None, "rd": None,
          "buybacks": ["PaymentsForRepurchaseOfCommonStock", "StockRepurchasedAndRetiredDuringPeriodValue"]},
    "MA": {"costOfRevenue": None, "rd": None},
    "CAT": {"capex": [("PaymentsToAcquirePropertyPlantAndEquipment", "PaymentsToAcquireEquipmentOnLease"),
                      ("PaymentsToAcquireProductiveAssets", "PaymentsToAcquireEquipmentOnLease")]},
}

# Values a firm tagged with its own extension concept (local names, read from its 10-K's XBRL instance), tried after
# the standard concepts.
EXTENSIONS = {
    "NVDA": {"capex": ["PurchasesOfPropertyAndEquipmentAndIntangibleAssets"]},
    "MSFT": {"acquisitions": ["AcquisitionsNetOfCashAcquiredAndPurchasesOfIntangibleAndOtherAssets"]},
    "GOOGL": {"acquisitions": ["AcquisitionsNetOfCashAcquiredAndPurchasesOfIntangibleAndOtherAssets"]},
    "META": {"acquisitions": ["PaymentsToAcquireBusinessesNetOfCashAcquiredAndPurchasesOfIntangibleAndOtherAssets"]},
    "AMZN": {"acquisitions": ["PaymentsToAcquireBusinessesNetOfCashAcquiredAndPaymentsToAcquireNonmarketableSecuritiesAndOther"]},
    "AMD": {"acquisitions": ["AcquisitionsNetOfCashAcquiredAllOthers"]},
}

# SpaceX: the audited consolidated statements (years ended December 31; USD millions) of its IPO prospectus (Form 424B4,
# filed 2026-06-12), recast for the common-control mergers with xAI (acquired 2026-02-02) and X (acquired by xAI
# 2025-03-28), so every year includes them. Earlier years have no public audited statements.
SPACEX_SOURCE = "https://www.sec.gov/Archives/edgar/data/1181412/000162828026042639/spaceexplorationtechnologi.htm"
SPACEX_LINES = {
    "revenue": "Revenue", "costOfRevenue": "Cost of revenue", "rd": "Research and development", "sga": "Selling, general, and administrative",
    "netIncome": "Net income (loss)", "stockComp": "Share-based compensation (cash flows)",
    "capex": "Purchases of property, plant, and equipment", "buybacks": "Payments for repurchase of common and redeemable convertible preferred stock",
    "dividends": "none paid", "acquisitions": "no acquisitions line", "incomeTaxesPaid": "Cash paid for income taxes, net",
}
SPACEX = {
    2023: {"revenue": 10387, "costOfRevenue": 6110, "rd": 2105, "sga": 1665, "netIncome": -4628, "stockComp": 679, "capex": 4415,
           "buybacks": 170, "dividends": 0, "acquisitions": 0, "incomeTaxesPaid": 45},
    2024: {"revenue": 14015, "costOfRevenue": 7996, "rd": 3464, "sga": 1813, "netIncome": 791, "stockComp": 784, "capex": 11163,
           "buybacks": 1021, "dividends": 0, "acquisitions": 0, "incomeTaxesPaid": 134},
    2025: {"revenue": 18674, "costOfRevenue": 9451, "rd": 8643, "sga": 2644, "netIncome": -4937, "stockComp": 1947, "capex": 20737,
           "buybacks": 1125, "dividends": 0, "acquisitions": 0, "incomeTaxesPaid": 154},
}
NOTES = {
    "SPCX": "Private until its June 2026 IPO: audited statements exist only for 2023-2025, in the prospectus, recast to include "
            "xAI and X (common control), so these years are SpaceX, xAI and X together; 2015-2022 are not public.",
    "PLTR": "Listed in September 2020; its first 10-K (2020) reports 2018-2020, so 2015-2017 are not public.",
    "AMZN": "No R&D line (technology and infrastructure is separate and not counted as research, as on the crest); acquisitions "
            "is the line 'Acquisitions, net of cash acquired, non-marketable investments, and other' (stakes included).",
    "MSFT": "Fiscal years end June 30 (FY2026 is mostly calendar 2025); acquisitions include purchases of intangible and other assets.",
    "GOOGL": "Acquisitions include purchases of intangible assets; finance-leased assets are disclosed from 2022.",
    "META": "Acquisitions include intangible assets; finance-leased assets are disclosed from 2019.",
    "NVDA": "Fiscal years end in late January (FY2026 is mostly calendar 2025); capex includes intangible assets (to FY2021 under "
            "NVIDIA's own concept); FY2026 acquisitions include the $13.0B Groq license and hiring.",
    "JPM": "A bank: no cost of revenue, R&D, SG&A or capex lines; dividends are the cash-flow 'Dividends paid', preferred included "
           "(the crest counts common dividends declared).",
    "BAC": "A bank: no cost of revenue, R&D, SG&A or capex lines; dividends are cash dividends on common stock.",
    "BRK": "No single cost-of-revenue or SG&A line for the whole conglomerate in its XBRL, no R&D, no dividends; buybacks from 2018.",
    "XOM": "Acquisitions sit inside capex (the 10-K's Cash Capex); no single cost-of-revenue line.",
    "CVX": "Revenue is sales and other operating revenues; cost of revenue is purchased crude oil and products.",
    "V": "Fiscal years end September 30; FY2016 buybacks are the equity statement's shares repurchased and retired (the cash-flow line is not tagged that year).",
    "CAT": "Capex includes equipment leased to others.",
    "AVGO": "Fiscal years end around November 1; FY2015 is Avago Technologies and FY2016-17 Broadcom Limited (predecessor registrants).",
    "WMT": "Fiscal years end January 31 (FY2026 is mostly calendar 2025).",
    "ORCL": "Fiscal years end May 31 (FY2026 is mostly calendar 2025); no single cost-of-revenue line.",
    "AAPL": "Fiscal years end in late September; no acquisitions line after FY2022.",
    "COST": "Fiscal years end around August 31; dividends include special dividends (2015, 2017, 2021, 2024).",
    "LLY": "Acquisitions are businesses only; purchases of in-process R&D (about $3-4B a year since 2023) are separate and not counted, as on the crest.",
}


def get(url, path):
    """The cached download of url (fetched once, politely)."""
    if not os.path.exists(path):
        if OFFLINE:
            raise RuntimeError("--offline: not cached: " + url)
        req = urllib.request.Request(url, headers={"User-Agent": USER_AGENT, "Accept-Encoding": "identity"})
        with urllib.request.urlopen(req, timeout=120) as r:
            data = r.read()
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "wb") as f:
            f.write(data)
        time.sleep(.2)
    with open(path, "rb") as f:
        return f.read()


def companyfacts(cik):
    return json.loads(get(f"https://data.sec.gov/api/xbrl/companyfacts/CIK{cik:010d}.json", os.path.join(CACHE, f"CIK{cik:010d}.json")))


def folder_of(cik, accn):
    return f"https://www.sec.gov/Archives/edgar/data/{cik}/{accn.replace('-', '')}/"


def days(a, b):
    return (datetime.date.fromisoformat(b) - datetime.date.fromisoformat(a)).days


def annual(start, end):
    return 340 <= days(start, end) <= 380


def calendar_year(start, end):
    """The calendar year holding most of a fiscal year's days (ties: the later)."""
    a, b = datetime.date.fromisoformat(start), datetime.date.fromisoformat(end)
    best = None
    for y in range(a.year, b.year + 1):
        n = (min(b, datetime.date(y, 12, 31)) - max(a, datetime.date(y, 1, 1))).days + 1
        if best is None or n >= best[1]:
            best = (y, n)
    return best[0]


def fiscal_label(end):
    """The firm's own name for a fiscal year: the year it ends in (a year ending in the first week of January is the one before)."""
    e = datetime.date.fromisoformat(end)
    return f"FY{e.year - 1 if e.month == 1 and e.day <= 7 else e.year}"


def annual_facts(facts_by_cik):
    """concept -> end date -> the annual USD fact from the latest 10-K reporting it (the current registrant first)."""
    out = {}
    for rank, (cik, facts) in enumerate(facts_by_cik):
        for name, c in facts.get("facts", {}).get("us-gaap", {}).items():
            for f in c.get("units", {}).get("USD", []):
                if "start" not in f or not f.get("form", "").startswith("10-K") or not annual(f["start"], f["end"]):
                    continue
                by_end = out.setdefault(name, {})
                prev = by_end.get(f["end"])
                if prev is None or (-rank, f["filed"]) > (-prev["rank"], prev["filed"]):
                    by_end[f["end"]] = dict(f, cik=cik, rank=rank)
    return out


def tenks(facts_by_cik):
    """Every 10-K a firm's company facts cite: (filed, accession, cik), newest first."""
    seen = {}
    for cik, facts in facts_by_cik:
        for c in facts.get("facts", {}).get("us-gaap", {}).values():
            for rows in c.get("units", {}).values():
                for f in rows:
                    if f.get("form", "").startswith("10-K"):
                        seen[f["accn"]] = (f["filed"], f["accn"], cik)
    return sorted(seen.values(), reverse=True)


_instances = {}


def instance(cik, accn):
    """A 10-K's XBRL instance: ({local name: {(start, end): value}} over its non-dimensional USD duration facts in the
    firm's own (extension) concepts, {local name: namespace prefix})."""
    if accn in _instances:
        return _instances[accn]
    folder = folder_of(cik, accn)
    index = json.loads(get(folder + "index.json", os.path.join(CACHE, "filings", accn, "index.json")))
    names = [i["name"] for i in index["directory"]["item"]]
    pick = [n for n in names if n.endswith("_htm.xml")] or \
           [n for n in names if n.endswith(".xml") and not re.search(r"_(cal|def|lab|pre)\.xml$|FilingSummary", n)]
    facts, prefix = {}, {}
    if pick:
        raw = get(folder + pick[0], os.path.join(CACHE, "filings", accn, pick[0]))
        prefixes = {uri.decode(): p.decode() for p, uri in re.findall(rb'xmlns:([\w-]+)="([^"]+)"', raw)}
        root = ET.fromstring(raw)
        x = "{http://www.xbrl.org/2003/instance}"
        periods, usd = {}, set()
        for c in root.iter(x + "context"):
            p, entity = c.find(x + "period"), c.find(x + "entity")
            s, e = p.find(x + "startDate"), p.find(x + "endDate")
            if s is None or e is None or entity.find(x + "segment") is not None or c.find(x + "scenario") is not None:
                continue
            periods[c.get("id")] = (s.text.strip(), e.text.strip())
        for u in root.iter(x + "unit"):
            m = u.find(x + "measure")
            if m is not None and (m.text or "").strip().split(":")[-1] == "USD":
                usd.add(u.get("id"))
        for el in root:
            if not el.tag.startswith("{") or el.get("contextRef") not in periods or el.get("unitRef") not in usd or not el.text:
                continue
            uri, local = el.tag[1:].split("}")
            if "xbrl.org" in uri or "fasb.org" in uri:
                continue
            try:
                value = float(el.text)
            except ValueError:
                continue
            facts.setdefault(local, {})[periods[el.get("contextRef")]] = value
            prefix[local] = prefixes.get(uri, "ext")
    _instances[accn] = facts, prefix
    return facts, prefix


def from_extension(filed_list, names, start, end):
    """(USD billions, source, (accn, filed, cik)) of the first extension concept reported for the period in the latest
    10-K filed within three years of its end."""
    for filed, accn, cik in filed_list:
        if not (end < filed <= (datetime.date.fromisoformat(end) + datetime.timedelta(days=3 * 366 + 60)).isoformat()):
            continue
        facts, prefix = instance(cik, accn)
        for name in names:
            for (s, e), v in facts.get(name, {}).items():
                if e == end and annual(s, e):
                    return round(v / 1e9, 3), f"{prefix.get(name, 'ext')}:{name} @ {accn}", (accn, filed, cik)
    return None, None, None


def pick_standard(table, concepts, end):
    """(USD billions, source, fact) of the first concept (or sum of parts) reported for the period ending at end."""
    for c in concepts:
        parts = c if isinstance(c, tuple) else (c,)
        found = [table.get(p, {}).get(end) for p in parts]
        if all(found):
            return round(sum(f["val"] for f in found) / 1e9, 3), " + ".join(f"us-gaap:{p}" for p in parts) + " @ " + found[0]["accn"], found[0]
    return None, None, None


def spacex(record):
    years = []
    for y, row in sorted(SPACEX.items()):
        entry = {"year": y, "fiscalYear": f"FY{y}", "start": f"{y}-01-01", "end": f"{y}-12-31"}
        for field, v in row.items():
            entry[field] = round(v / 1000, 3)
        entry["sources"] = {field: f"prospectus: {SPACEX_LINES[field]} @ 0001628280-26-042639" for field in row}
        years.append(entry)
    return {"ticker": "SPCX", "name": record["name"], "cik": 1181412, "notes": NOTES["SPCX"], "years": years,
            "filings": {"0001628280-26-042639": {"form": "424B4", "filed": "2026-06-12", "url": SPACEX_SOURCE}}}


def firm(record, gaps):
    ticker = record["ticker"]
    cik = int(re.search(r"/edgar/data/(\d+)/", record["sources"]["financials"]).group(1))
    facts = [(c, companyfacts(c)) for c in [cik] + PREDECESSORS.get(ticker, [])]
    table, filed_list = annual_facts(facts), tenks(facts)
    concepts = dict(CONCEPTS)
    concepts.update(FIRM_CONCEPTS.get(ticker, {}))
    extensions = EXTENSIONS.get(ticker, {})
    # The fiscal years: every annual period the firm reported revenue, net income or (a bank) noninterest expense for.
    periods = {}
    for field in ("revenue", "netIncome", "noninterestExpense"):
        for c in concepts.get(field) or []:
            for end, f in table.get(c, {}).items():
                y = calendar_year(f["start"], end)
                if FIRST <= y <= LAST and (y not in periods or end > periods[y][1]):
                    periods[y] = (f["start"], end)
    years, filings = [], {}
    for y in range(FIRST, LAST + 1):
        if y not in periods:
            gaps.append(f"{ticker} {y}: no annual report")
            continue
        start, end = periods[y]
        row, sources = {"year": y, "fiscalYear": fiscal_label(end), "start": start, "end": end}, {}
        for field in FIELDS:
            if not concepts.get(field):
                continue
            v, src, fact = pick_standard(table, concepts[field], end)
            if fact:
                filings[fact["accn"]] = (fact["form"], fact["filed"], fact["cik"])
            if v is None and field in extensions:
                v, src, hit = from_extension(filed_list, extensions[field], start, end)
                if hit:
                    filings[hit[0]] = ("10-K", hit[1], hit[2])
            if v is None and field in ZERO_IF_ABSENT:
                v, src = 0.0, "no such line in the year's 10-K cash-flow statement"
            if v is None:
                gaps.append(f"{ticker} {y} {field}")
                continue
            row[field], sources[field] = v, src
        row["sources"] = sources
        years.append(row)
    return {"ticker": ticker, "name": record["name"], "cik": cik, "notes": NOTES.get(ticker, ""), "years": years,
            "filings": {a: {"form": f, "filed": d, "url": folder_of(c, a)} for a, (f, d, c) in sorted(filings.items())}}


def main():
    filings = json.load(open(FILINGS, encoding="utf-8"))["firms"]
    firms, gaps = [], []
    for record in filings:
        firms.append(spacex(record) if record["ticker"] == "SPCX" else firm(record, gaps))
    out = {
        "note": "Uses of money of the 25 stored firms in each fiscal year that falls mostly in calendar 2015-2025 (year), from the "
                "XBRL data of their audited 10-Ks on SEC EDGAR (SpaceX: its 2026 IPO prospectus). USD billions. Each value's "
                "source is its concept and the accession of the filing it was read from (the latest 10-K reporting the period, "
                "so earlier years are as later filings restated them); 'filings' gives each accession's folder. Collected by "
                "Tools/corporate/history.py.",
        "collected": datetime.date.today().isoformat(),
        "years": [FIRST, LAST],
        "fields": FIELDS,
        "gaps": gaps,
        "firms": firms,
    }
    with open(OUT, "w", encoding="utf-8") as fh:
        json.dump(out, fh, indent=1)
    print(f"wrote {os.path.relpath(OUT)}: {len(firms)} firms, {sum(len(f['years']) for f in firms)} firm-years; {len(gaps)} gaps")
    for g in gaps:
        print("  gap:", g)
    # The latest year against the researched record (filings.json), for the fields the trail draws and a few more.
    for record in filings:
        h = next(f for f in firms if f["ticker"] == record["ticker"])
        last = h["years"][-1] if h["years"] else {}
        for field in FIELDS:
            a, b = last.get(field), record.get(field)
            if isinstance(a, (int, float)) and isinstance(b, (int, float)) and abs(a - b) > max(.02 * abs(b), .015):
                print(f"  differs from filings.json: {record['ticker']} {last['fiscalYear']} {field} {a} vs {b} ({last['sources'][field]})")


if __name__ == "__main__":
    main()

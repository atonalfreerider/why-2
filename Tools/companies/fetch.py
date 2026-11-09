#!/usr/bin/env python3
"""
The measured market values of the major US companies (Assets/Resources/Data/economy/companies.json, the roster of the eighth
direction: Docs/research/advantage/brief-v6.md), and the descriptions Jev reads to judge them (Tools/companies/judge.py).

For every candidate of Tools/companies/candidates.json (the 75 companies of company-lives.json, and the largest US companies of
each of the 23 market industries of industries.json, today's and the historical giants'):
  1. its description: the opening of its English Wikipedia article (the Wikimedia REST API's page summary), which judge.py
     passes to Jev as state;
  2. its market value: CompaniesMarketCap's page of the company's market cap history (companiesmarketcap.com/<slug>/marketcap/),
     whose table gives the market capitalization at the end of each year since 1996, or since the company listed (a delisted
     company keeps its page, its ticker marked ".defunct"). The rows up to 2025 become one research fact per company in
     Docs/research/companies/<cluster>.json (the fields of Docs/research/advantage/story-ai.json's facts): its quote the
     table's rows verbatim, its values each row's year and amount as written, its context the table's heading, for the gate
         python3 -I Tools/advantage/verify.py --dir Docs/research/companies --cluster <cluster>
     The current year's row is left out (it is the market cap today, not at a year's end), and so is the last row of a
     delisted company unless it stopped trading in December (it is its last known value, on its last day of trading).
  3. the page of each company: candidates.json's "cmc" (a slug; false for none); else the page of its ticker in
     CompaniesMarketCap's ranking of US companies (pages of 100, fetched once); else the site's own search (the POST its
     search box makes), taking a result whose ticker is the candidate's, live or ".defunct", and whose name shares the
     candidate's first word. --resolve prints these choices for review; a wrong one is fixed by writing "cmc" in candidates.json.

Polite: one request at a time, a pause after each download, a generic User-Agent naming the repository (no person). Every
download is cached under Tools/companies/.cache/ with its URL and fetch date (Tools/global/verify.py's Cache), so a rerun asks
only for what is missing and --offline replays the cache. Each market cap page is also placed in the gate's cache under the
fact's cacheName (Tools/advantage/.cache/companies/<slug>.html) when the gate has no copy, so the gate reads the very page the
rows were parsed from, and the site is asked once.

    python3 -I Tools/companies/fetch.py                 # fetch what is missing and write the research facts
    python3 -I Tools/companies/fetch.py --offline       # from the cache only
    python3 -I Tools/companies/fetch.py --resolve       # print each candidate's page (and how it was found); writes nothing
    python3 -I Tools/companies/fetch.py --ranking       # print the US ranking (rank, slug, ticker, market cap, name)
    python3 -I Tools/companies/fetch.py --only a,b      # print what is parsed for some candidates; writes only the cache

The descriptions are read back from the cache by judge.py (Tools/companies/.cache/wiki/; descriptions.json lists them for a
reader). A candidate with no English Wikipedia article (Ensign Group) has no description and is not judged.
"""
import datetime
import hashlib
import json
import os
import re
import shutil
import sys
import time
import urllib.parse

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
DATA = os.path.join(ROOT, "Assets", "Resources", "Data", "economy")
CANDIDATES = os.path.join(HERE, "candidates.json")
LIVES = os.path.join(DATA, "company-lives.json")
RESEARCH = os.path.join(ROOT, "Docs", "research", "companies")
CACHE = os.path.join(HERE, ".cache")
DESCRIPTIONS = os.path.join(CACHE, "descriptions.json")
GATE_CACHE = os.path.join(ROOT, "Tools", "advantage", ".cache")   # Tools/advantage/verify.py's cache (its G.CACHE)
sys.dont_write_bytecode = True
sys.path[0:0] = [HERE, os.path.join(ROOT, "Tools", "global")]
import verify as G  # noqa: E402  (Tools/global/verify.py: the download cache, html_text, norm)

G.CACHE = CACHE
AGENT = "why-2-companies/0.1 (https://github.com/atonalfreerider/why-2)"
PAUSE = 2.0          # seconds after each download
LAST_YEAR = 2025     # the last year-end a value is kept for
RANKING = "https://companiesmarketcap.com/usa/largest-companies-in-the-usa-by-market-cap/?page={}"
RANKING_PAGES = 12   # 1,200 US companies
CMC = "https://companiesmarketcap.com/{}/marketcap/"
SEARCH = "https://companiesmarketcap.com/search.do"
WIKI = "https://en.wikipedia.org/api/rest_v1/page/summary/{}"

# The research clusters, by the authored industry (the gate runs one cluster at a time).
CLUSTERS = {
    "raw": ("agriculture", "oil_gas", "mining_metals", "utilities"),
    "make": ("manufacturing",),
    "build-move-sell": ("construction", "transport", "trade"),
    "money": ("banking", "finance", "insurance", "real_estate"),
    "services": ("health", "legal", "professional", "education", "entertainment", "hospitality", "other_services"),
    "tech": ("hardware", "software", "internet", "media_telecom"),
}
CLUSTER_OF = {ind: c for c, inds in CLUSTERS.items() for ind in inds}

# ------------------------------------------------------------------------------------------------------------- inputs


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def candidates(anchors=False):
    """The candidates in candidates.json's order, a curated one with company-lives.json's fields (candidates.json names only
    its id and how to find its pages); with anchors=True, the anchor-only companies of judge.py's anchors after them."""
    lives = {c["id"]: c for c in load(LIVES)["companies"]}
    doc = load(CANDIDATES)
    out = []
    for c in doc["companies"]:
        if c.get("curated"):
            out.append(dict(lives[c["id"]], **c))
        else:
            out.append(dict(c))
    if anchors:
        out += [dict(a) for a in doc.get("anchors", [])]
    return out

# ------------------------------------------------------------------------------------------------------------ fetching


class Polite(G.Cache):
    """The 2026 gate's download cache, with this tool's User-Agent and a pause after each download (a cached file costs
    nothing)."""

    def get(self, url, name, **kw):
        before = self.meta(name)
        fresh = before is None or "error" in before
        body, meta = super().get(url, name, agent=AGENT, **kw)
        if fresh and not self.offline:
            time.sleep(PAUSE)
        return body, meta


def ranking(cache):
    """[{rank, slug, name, ticker, marketCap (USD bn, today)}] of CompaniesMarketCap's US ranking."""
    rows = []
    for page in range(1, RANKING_PAGES + 1):
        body, meta = cache.get(RANKING.format(page), f"cmc/ranking-usa-{page}.html", min_bytes=2000)
        if body is None:
            print(f"  ranking page {page}: {meta.get('error')}", file=sys.stderr)
            continue
        html = body.decode("utf-8", "replace")
        for tr in re.findall(r"(?s)<tr>(.*?)</tr>", html):
            m = re.search(r'(?s)href="/([^"/]+)/marketcap/"><div class="company-name">(.*?)</div><div class="company-code">'
                          r'<span class="rank d-none"></span>(.*?)</div>', tr)
            if not m:
                continue
            rank = re.search(r'class="rank-td td-right" data-sort="(\d+)"', tr)
            cap = re.search(r'class="td-right" data-sort="(\d+)"', tr)
            rows.append({"rank": int(rank.group(1)) if rank else None, "slug": m.group(1),
                         "name": G.html.unescape(m.group(2)).strip(), "ticker": G.html.unescape(m.group(3)).strip(),
                         "marketCap": round(int(cap.group(1)) / 1e9, 2) if cap else None})
    return rows


def search(cache, query):
    """The site's search results for a name: [{identifier, name, url (the slug)}]."""
    name = "cmc/search-" + hashlib.sha1(query.lower().encode()).hexdigest()[:12] + ".json"
    body, meta = cache.get(SEARCH, name, data=("query=" + urllib.parse.quote(query)).encode(),
                           headers={"Content-Type": "application/x-www-form-urlencoded"})
    if body is None:
        return []
    try:
        return json.loads(body)
    except ValueError:
        return []


def words(text):
    return [w for w in re.findall(r"[a-z0-9]+", text.lower()) if w not in ("the", "inc", "corporation", "company", "co")]


def resolve(cache, c, by_ticker):
    """(slug or None, how it was found) for a candidate's market cap page."""
    if c.get("cmc") is False:
        return None, "none (candidates.json)"
    if c.get("cmc"):
        return c["cmc"], "candidates.json"
    t = c.get("ticker")
    if t and t in by_ticker:
        return by_ticker[t]["slug"], f"US ranking #{by_ticker[t]['rank']}"
    query = re.sub(r"\s*\(.*?\)", "", c["name"]).strip()
    results = search(cache, query)
    first = (words(query) or [""])[0]
    for r in results:
        ident = (r.get("identifier") or "").upper()
        base = ident.split(".DEFUNCT")[0]
        if t and base == t.upper() and first in words(r.get("name") or ""):
            return r["url"], f"search: {r.get('name')} ({r.get('identifier')})"
    for r in results:
        if ".DEFUNCT" in (r.get("identifier") or "").upper() and first in words(r.get("name") or ""):
            return None, f"unsure: search found {r.get('name')} ({r.get('identifier')}, {r.get('url')}); not taken"
    return None, "not found" + (": " + "; ".join(f"{r.get('name')} ({r.get('identifier')})" for r in results[:4])
                                if results else "")


def wiki(cache, title):
    """{title, description, extract, qid, url} of an English Wikipedia article's summary, or None."""
    name = "wiki/" + re.sub(r"[^A-Za-z0-9_.-]", "_", title) + ".json"
    body, meta = cache.get(WIKI.format(urllib.parse.quote(title.replace(" ", "_"), safe="")), name, min_bytes=50)
    if body is None:
        return None
    j = json.loads(body)
    if j.get("type") == "disambiguation" or not j.get("extract"):
        return None
    return {"title": j.get("title"), "description": j.get("description"), "extract": j["extract"].strip(),
            "qid": j.get("wikibase_item"), "url": ((j.get("content_urls") or {}).get("desktop") or {}).get("page")}


UNITS = {"T": 1000.0, "B": 1.0, "M": 0.001, "K": 0.000001}
ROW = re.compile(r"(?<!\d)((?:19|20)\d\d) \$(\d[\d,]*(?:\.\d+)?) ([TBMK])(?: (-?[\d,]*\.?\d+%))?")
MONTH = {m: i for i, m in enumerate(["January", "February", "March", "April", "May", "June", "July", "August",
                                      "September", "October", "November", "December"], 1)}
TABLE_ENDS = (" End of Day market cap", " Market capitalization for similar companies", " What is the market capitalization")


def cmc_page(cache, slug):
    """(normalised text of the company's market cap page, meta, cache name), or (None, meta, name)."""
    name = f"cmc/{slug}.html"
    body, meta = cache.get(CMC.format(slug), name, min_bytes=2000)
    if body is None:
        return None, meta, name
    return G.norm(G.html_text(body)), meta, name


def history(text):
    """The page's year-end market caps: {heading, title, table, rows: [(year, usd bn, row text, start, end)], quote,
    lastKnown}: the rows kept are those up to LAST_YEAR, without a delisted company's last row (its last known value) unless
    the page dates that value in December; lastKnown is (the date of that value or None when the page gives no day, its
    year) for a delisted company, else None. None when the page has no table."""
    m = re.search(r"Market cap history of (.+?) from (\d{4}) to (\d{4}) End of year Market Cap Year Marketcap Change ", text)
    if not m:
        return None
    ends = [i for i in (text.find(e, m.end()) for e in TABLE_ENDS) if i > 0]
    table = text[m.end():min(ends) if ends else len(text)]
    rows = [(int(r.group(1)), float(r.group(2).replace(",", "")) * UNITS[r.group(3)], r.group(0), r.start(), r.end())
            for r in ROW.finditer(table)]
    last_known = None
    if "Last known market cap:" in text and rows:
        d = re.search(r"Last known market cap: .*? On (\w+) (\d{1,2}), (\d{4}) ", text)
        day = (datetime.date(int(d.group(3)), MONTH[d.group(1)], int(d.group(2)))
               if d and d.group(1) in MONTH else None)
        if day:
            last_known = (day, {day.year})
        else:   # no day given: the newest rows that repeat the last known value carry it, not a year-end value
            v = re.search(r"Last known market cap: \$(\d[\d,]*(?:\.\d+)?) (Trillion|Billion|Million) USD", text)
            amount = v and float(v.group(1).replace(",", "")) * {"Trillion": 1000.0, "Billion": 1.0, "Million": 0.001}[v.group(2)]
            repeat = set()
            for r in rows:   # newest first
                if amount is None or abs(r[1] - amount) > 0.006 * max(amount, 0.001):
                    break
                repeat.add(r[0])
            last_known = (None, repeat or {max(r[0] for r in rows)})

    def partial(year):   # the year of a delisted company's last known value, unless it is dated in December
        return bool(last_known) and year in last_known[1] and not (last_known[0] and last_known[0].month == 12)

    keep = [r for r in rows if r[0] <= LAST_YEAR and not partial(r[0])]
    years = [r[0] for r in keep]
    if years != sorted(years, reverse=True) or len(set(years)) != len(years):
        raise ValueError(f"rows out of order or repeated: {years}")
    return window({"heading": m.group(0).strip(), "title": m.group(1), "table": table, "rows": keep, "quote": None,
                   "lastKnown": last_known}, None, None)


def window(h, lo, hi):
    """The history with only the rows from year lo to year hi (None: open), its quote the table's text from the first of
    them to the last (the rows run newest first and contiguous, so the quote stays verbatim)."""
    rows = [r for r in h["rows"] if (lo is None or r[0] >= lo) and (hi is None or r[0] <= hi)]
    out = dict(h, rows=rows, quote=h["table"][rows[0][3]:rows[-1][4]] if rows else None)
    return out


def pages(c):
    """The candidate's market cap pages as [(slug or None, from year, to year)]: candidates.json's "cmc" may be one slug or
    a list of {slug, from, to} when a company's history is split over two pages of the same legal entity's lineage."""
    if isinstance(c.get("cmc"), list):
        return [(p["slug"], p.get("from"), p.get("to")) for p in c["cmc"]]
    return None


def seed_gate(cache, name, slug):
    """Copies a cached market cap page into the gate's cache under the fact's cacheName, when the gate has no copy."""
    gate = os.path.join(GATE_CACHE, "companies", slug + ".html")
    if os.path.exists(gate) and os.path.exists(gate + ".meta.json"):
        return
    os.makedirs(os.path.dirname(gate), exist_ok=True)
    shutil.copyfile(cache.path(name), gate)
    shutil.copyfile(cache.path(name) + ".meta.json", gate + ".meta.json")

# --------------------------------------------------------------------------------------------------------------- facts


def value_text(row_text):
    """'2012 $101.94 B -17.44%' -> '2012 $101.94 B' (the year and the amount, as written)."""
    m = ROW.match(row_text)
    return f"{m.group(1)} ${m.group(2)} {m.group(3)}"


def spoken(row_text):
    """'2012 $101.94 B -17.44%' -> '$101.94 billion at the end of 2012'."""
    m = ROW.match(row_text)
    unit = {"T": "trillion", "B": "billion", "M": "million", "K": "thousand"}[m.group(3)]
    return f"${m.group(2)} {unit} at the end of {m.group(1)}"


def fact(c, slug, h, fetched, fid, span=(None, None)):
    """The research fact of one company's year-end market caps on one page (its quote the table's rows)."""
    rows = sorted(r[:3] for r in h["rows"])
    note = (f"CompaniesMarketCap's end-of-year market capitalization of {h['title']}, nominal US dollars; the {LAST_YEAR + 1} "
            "row (today's value) is left out")
    if h["lastKnown"]:
        day, year = h["lastKnown"]
        note += (f"; delisted, its last known value {'on ' + day.isoformat() if day else 'in ' + str(min(year))}"
                 + ("" if day and day.month == 12 else " (that row is left out: not a year-end value)"))
    if span != (None, None):
        note += f"; this page's rows {span[0] or ''}-{span[1] or ''} only (the company's history spans more than one page)"
    return {
        "id": fid,
        "claim": f"CompaniesMarketCap's market cap history of {h['title']} gives its market capitalization as "
                 + "; ".join(spoken(t) for _, _, t in rows) + ".",
        "url": CMC.format(slug),
        "quotes": [h["quote"]],
        "values": [value_text(t) for _, _, t in sorted((r[:3] for r in h["rows"]), reverse=True)],
        "context": [h["heading"]],
        "tight": True,
        "cacheName": f"companies/{slug}.html",
        "dated2026": False,
        "actor": c["id"],
        "kind": "marketCap",
        "unit": "USD bn, end of year (nominal)",
        "rows": [[y, round(v, 4)] for y, v, _ in rows],
        "period": f"{rows[0][0]}-{rows[-1][0]}",
        "from": rows[0][0],
        "to": rows[-1][0],
        "accessed": fetched,
        "notes": note,
    }


def research(cluster, facts, fetched):
    return {
        "cluster": cluster,
        "asOf": fetched,
        "scope": ("Year-end market capitalizations of the major US companies of the industries "
                  + ", ".join(CLUSTERS[cluster]) + " (by the industry authored in Tools/companies/candidates.json), from "
                  "CompaniesMarketCap's market cap history pages, 1996-2025: one fact per company, its quote the table's "
                  "rows. Written by Tools/companies/fetch.py."),
        "notes": ("CompaniesMarketCap's history begins in 1996 or 1997 (or at listing). The current year's row (today's "
                  "value) and a delisted company's last row (its last known value, unless it stopped trading in December) "
                  "are left out. Values are nominal US dollars as the table writes them."),
        "facts": facts,
    }

# ---------------------------------------------------------------------------------------------------------------- main


def main():
    args = sys.argv[1:]
    offline = "--offline" in args
    only = set(args[args.index("--only") + 1].split(",")) if "--only" in args else None
    cache = Polite(offline)
    ranked = ranking(cache)
    if "--ranking" in args:
        for r in ranked:
            print(f"{r['rank']:>5} {r['slug']:<40} {r['ticker']:<8} {r['marketCap'] or 0:>10.2f}  {r['name']}")
        return
    by_ticker = {}
    for r in ranked:
        by_ticker.setdefault(r["ticker"], r)
    cands = candidates(anchors=True)
    if only:
        missing = only - {c["id"] for c in cands}
        if missing:
            sys.exit("not candidates: " + ", ".join(sorted(missing)))
    if "--resolve" in args:
        for c in cands:
            if c.get("anchorOnly") or (only and c["id"] not in only):
                continue
            slug, how = resolve(cache, c, by_ticker)
            print(f"{c['id']:<22} {c.get('ticker') or '-':<7} {slug or '-':<36} {how}")
        return
    facts = {c: [] for c in CLUSTERS}
    told = load(DESCRIPTIONS) if os.path.exists(DESCRIPTIONS) else {}
    gaps = []
    fetched = datetime.date.today().isoformat()
    for c in cands:
        if only and c["id"] not in only:
            continue
        if c.get("wiki"):
            w = wiki(cache, c["wiki"])
            if w is None:
                gaps.append(f"{c['id']}: no Wikipedia summary for {c['wiki']}")
            else:
                told[c["id"]] = w
        if c.get("anchorOnly"):
            continue
        spans = pages(c)
        if spans is None:
            slug, how = resolve(cache, c, by_ticker)
            if not slug:
                gaps.append(f"{c['id']}: no market cap page ({how})")
                continue
            spans = [(slug, None, None)]
        else:
            how = "candidates.json"
        for n, (slug, lo, hi) in enumerate(spans, 1):
            text, meta, name = cmc_page(cache, slug)
            if text is None:
                gaps.append(f"{c['id']}: {CMC.format(slug)}: {meta.get('error')}")
                continue
            h = history(text)
            h = window(h, lo, hi) if h else None
            if h is None or not h["rows"]:
                gaps.append(f"{c['id']}: {slug} has no year-end row up to {LAST_YEAR}" + (f" in {lo}-{hi}" if lo or hi else ""))
                continue
            seed_gate(cache, name, slug)
            fid = f"co-{c['id']}-mcap" + (f"-{n}" if len(spans) > 1 else "")
            f = fact(c, slug, h, meta.get("fetched") or fetched, fid, (lo, hi))
            if only:
                print(f"{c['id']}: {slug} ({how}) | {h['heading']} | {len(h['rows'])} rows {f['period']}"
                      + (f" | last known {h['lastKnown'][0] or sorted(h['lastKnown'][1])}" if h["lastKnown"] else "")
                      + f" | {h['rows'][0][2]} ... {h['rows'][-1][2]}")
                continue
            facts[CLUSTER_OF[c["industry"]]].append(f)
    for g in gaps:
        print("  " + g)
    os.makedirs(CACHE, exist_ok=True)
    with open(DESCRIPTIONS, "w", encoding="utf-8") as f:
        json.dump(told, f, indent=1, ensure_ascii=False, sort_keys=True)
    if only:
        return
    os.makedirs(RESEARCH, exist_ok=True)
    for cluster, fs in facts.items():
        path = os.path.join(RESEARCH, cluster + ".json")
        with open(path, "w", encoding="utf-8") as f:
            json.dump(research(cluster, fs, fetched), f, indent=1, ensure_ascii=False)
            f.write("\n")
        print(f"wrote {os.path.relpath(path, ROOT)}: {len(fs)} facts")
    print(f"{len(told)} descriptions, {sum(len(v) for v in facts.values())} market value facts, {len(gaps)} gaps")


if __name__ == "__main__":
    main()

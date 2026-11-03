#!/usr/bin/env python3
"""Builds Assets/Resources/Data/social/visibility.json: real public figures' money and visibility for the lives view.

The lives view (M3, "Lives that rise, stay or fall"; Docs/hills-design/MODALITIES.md section 7.3) draws three real public
figures above the synthetic lives: their fortune (Forbes estimates, a gold line in the magnified top 1%) and their
visibility (monthly English-Wikipedia page views, a white line in the attention band), with the 2025 follower snapshot as
a separate star. This builder writes, for every candidate:

  forbes      net worth on Forbes's World's Billionaires list (spring) and Forbes 400 (fall), 2005-2026, read by hand
              into Tools/lives/sources.json, each with its URL and the verbatim text of the entry; Forbes's own revisions
              (an archived copy that differs from today's) are kept as separate values
  pageViews   monthly English-Wikipedia page views, Jul 2015 - Oct 2026: Wikimedia REST API, per article, all access,
              user agent (no key); the article is the person's English Wikipedia sitelink on Wikidata
  followers   the 2025 follower snapshot copied from people.json: followers summed across Instagram, TikTok, YouTube and X
  annual      the years compared (2016-2025): the World's Billionaires estimate of each year and the calendar year's page
              views, both also indexed 2016 = 1

Candidates are the US people of people.json who are public adults and have at least 8 annual Forbes values in the years
compared. Public adult, enforced here from people.json: kind "p" (a person, not a group), a Wikidata item and an English
Wikipedia article (a public figure, not a private individual), a birth year that is known and not estimated, and 18 or
older from the first year of any data written for them (born at least 19 years before it, whatever the birthday). Anyone
who fails the rule is counted, never named, in the file and in the log.

Forbes's list feeds (the forbesapi data forbes.com's list pages load, one per list and year) are fetched to find every US
person of people.json on any list (name or alias on Wikidata, and birth year) and to re-verify each hand-read quote,
value, rank, month and entry date. Forbes's own revisions are looked for in the Wayback Machine: its index of captures of
the list data at both addresses forbes.com has used (ajax/list/data, then forbesapi) is read, and every candidate's value
is compared with today's in every copy at the older address and in the earliest and latest readable whole-list copy at the
newer one. The log prints which list-years have a copy from near the list's date, only later copies, or none.

The three selection rules of the spec are computed and printed: together (the highest share of years in which log net
worth and log page views moved the same way), apart (the lowest share) and little attention (the richest Forbes-400
American not among the most-followed accounts, with the fewest page views). When no candidate meets the little-attention
condition, that rule picks nobody and its fallback is written only as a proposal. The author approves the shortlist in
sources.json (author_approved); until then the picks are written with authorApproved false.

Every download is cached under Tools/lives/.cache (gitignored) with a manifest of URLs and retrieval dates, so reruns are
free and --offline rebuilds the file byte-identically. A build verifies against the cached copies; --refresh re-downloads
the 2026 list feeds first (MODALITIES.md rule 10), and deleting the cache re-fetches everything. Nothing here needs a key;
no credential is read.

Usage (from the repository root):
    python3 -I Tools/lives/build.py            # fetch what is not cached, write the JSON
    python3 -I Tools/lives/build.py --offline  # use only the cache
    python3 -I Tools/lives/build.py --refresh  # re-download the 2026 Forbes list feeds, then build
"""
import collections
import datetime
import fractions
import gzip
import hashlib
import json
import math
import os
import re
import sys
import time
import unicodedata
import urllib.parse
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
DATA = os.path.join(ROOT, "Assets", "Resources", "Data")
PEOPLE = os.path.join(DATA, "social", "people.json")
OUT = os.path.join(DATA, "social", "visibility.json")
SRC = os.path.join(HERE, "sources.json")
CACHE = os.path.join(HERE, ".cache")
MANIFEST = os.path.join(CACHE, "manifest.json")

OFFLINE = "--offline" in sys.argv
REFRESH = "--refresh" in sys.argv
AGENT = "why-2-lives/0.1 (educational economic visualization)"

FORBES_FIELDS = "uri,personName,finalWorth,rank,year,month,date,countryOfCitizenship,birthDate"
FORBES_FEED = ("https://www.forbes.com/forbesapi/person/{list}/{year}/position/true.json?fields=" + FORBES_FIELDS +
               "&limit=5000")
WIKIDATA = ("https://www.wikidata.org/w/api.php?action=wbgetentities&ids={ids}&props=labels%7Caliases%7Csitelinks"
            "&languages=en&sitefilter=enwiki&format=json")
PAGEVIEWS = ("https://wikimedia.org/api/rest_v1/metrics/pageviews/per-article/en.wikipedia/all-access/user/{title}/monthly/"
             "{start}0100/{end}00")
WIKI_PAGE = "https://en.wikipedia.org/wiki/{}"
WAYBACK = "https://web.archive.org/web/{ts}/{url}"         # a capture as the Wayback Machine shows it (the URL written)
WAYBACK_RAW = "https://web.archive.org/web/{ts}id_/{url}"  # the same capture as it was archived (what is read)
LISTS = {"billionaires": "The World's Billionaires", "forbes-400": "The Forbes 400"}
AS_OF_FROM = 2026  # values of this year and later are stamped "as of" (MODALITIES.md rule 10); --refresh re-downloads them
WHOLE_SHARE = 0.9  # an archived copy holding fewer list rows than this share of today's list is a first page
# Forbes stores each entry's date as epoch milliseconds. The stamps fall at several hours (00:00, 03:00, 04:00, 05:00,
# 05:01, 09:01 and 23:00 UTC) and entries of one list can carry dates up to a month apart (The World's Billionaires 2009:
# March 1 for most, March 11 and March 30 for a few), so an entry's date is not the list's. entryDate is the UTC date of
# the entry's own stamp; listDate is the list's most common UTC date across all of its entries.
UTC = datetime.timezone.utc

# ----------------------------------------------------------------------------------------------------------- downloads

_manifest = None
USED = set()  # cache files read by this run: the latest retrieval date among them is the file's "built"


def manifest():
    global _manifest
    if _manifest is None:
        try:
            with open(MANIFEST, encoding="utf-8") as f:
                _manifest = json.load(f)
        except (OSError, ValueError):
            _manifest = {}
    return _manifest


def fetch(url, name, required=True, check=None, refresh=False, gz=False, pause=0.3, attempts=4, backoff=3):
    """Download once into .cache/<name> (recorded in the manifest with its URL, retrieval date and the body's sha256);
    later runs read the file. check(text) may reject a body (an error page served with status 200); a rejected body is
    not cached. refresh downloads again although the file is cached, and replaces it only when the download succeeds. gz
    stores the body gzip-compressed. A failed attempt waits backoff, 2 x backoff, ... seconds before the next. Returns the
    text, or None when the file is not cached and cannot be fetched and required is False."""
    path = os.path.join(CACHE, name)
    if refresh or not os.path.exists(path):
        if OFFLINE:
            if required:
                raise SystemExit(f"--offline: {name} is not cached")
            return None
        os.makedirs(os.path.dirname(path), exist_ok=True)
        last = None
        for attempt in range(attempts):
            try:
                req = urllib.request.Request(url, headers={"User-Agent": AGENT, "Accept": "application/json"})
                with urllib.request.urlopen(req, timeout=180) as r:
                    body = r.read()
                if body[:2] == b"\x1f\x8b":  # the Wayback Machine serves an archived gzip body as it was stored
                    body = gzip.decompress(body)
                text = body.decode("utf-8")
                if check and not check(text):
                    raise ValueError("unexpected body")
                tmp = path + ".part"
                with open(tmp, "wb") as f:
                    f.write(gzip.compress(body, mtime=0) if gz else body)
                os.replace(tmp, path)
                m = manifest()
                m[name] = {"url": url, "retrieved": datetime.date.today().isoformat(),
                           "sha256": hashlib.sha256(body).hexdigest()}
                with open(MANIFEST, "w", encoding="utf-8") as f:
                    json.dump(m, f, indent=1, sort_keys=True)
                    f.write("\n")
                break
            except Exception as e:  # noqa: BLE001
                last = e
                time.sleep(backoff * (attempt + 1))
        else:
            if refresh and os.path.exists(path):
                raise SystemExit(f"--refresh: {url} could not be downloaded ({type(last).__name__}: {last}); the cached "
                                 f"copy of {retrieved(name)} is unchanged")
            if required:
                raise RuntimeError(f"download failed: {url} ({type(last).__name__}: {last})")
            print(f"FETCH FAILED (not required): {url} ({type(last).__name__})", file=sys.stderr)
            return None
        time.sleep(pause)
    USED.add(name)
    with open(path, "rb") as f:
        body = f.read()
    return (gzip.decompress(body) if gz else body).decode("utf-8")


def retrieved(name):
    """The day a cached file was downloaded (manifest; the file's modification day for a file without an entry)."""
    e = manifest().get(name)
    if e:
        return e["retrieved"]
    path = os.path.join(CACHE, name)
    return datetime.date.fromtimestamp(os.path.getmtime(path)).isoformat() if os.path.exists(path) else None


def is_json(text):
    try:
        json.loads(text)
        return True
    except ValueError:
        return False


def forbes_feed(lst, year, refresh=False):
    """(rows, url, cache name, text) of a Forbes list and year: forbes.com's own list data."""
    url = FORBES_FEED.format(list=lst, year=year)
    name = f"forbes/{lst}_{year}.json"
    text = fetch(url, name, check=lambda t: is_json(t) and "personList" in t, refresh=refresh)
    d = json.loads(text)["personList"]
    rows = d["personsLists"]
    if d.get("count") is not None and d["count"] != len(rows):
        raise SystemExit(f"Forbes {lst} {year}: {len(rows)} rows of {d['count']} (raise the limit)")
    return rows, url, name, text


def wikidata(qids):
    """({QID: entity}, cache names): English label, aliases and the English Wikipedia sitelink, 50 per request."""
    out, names = {}, []
    qids = sorted(set(qids))
    for i in range(0, len(qids), 50):
        batch = qids[i:i + 50]
        key = hashlib.sha256("|".join(batch).encode("utf-8")).hexdigest()[:16]
        names.append(f"wikidata/{key}.json")
        text = fetch(WIKIDATA.format(ids="%7C".join(batch)), names[-1], check=lambda t: is_json(t) and "entities" in t)
        out.update(json.loads(text)["entities"])
    return out, names


def page_views(title, start, end):
    """({'YYYY-MM': views}, url, cache name): monthly page views of an English Wikipedia article."""
    t = urllib.parse.quote(title.replace(" ", "_"), safe="")
    url = PAGEVIEWS.format(title=t, start=start.replace("-", ""), end=last_day(end))
    name = "pageviews/" + re.sub(r"[^A-Za-z0-9_.-]", "_", title.replace(" ", "_")) + ".json"
    text = fetch(url, name, check=lambda x: is_json(x) and "items" in x)
    items = json.loads(text)["items"]
    out = {}
    for it in items:
        if it.get("granularity") != "monthly" or it.get("agent") != "user" or it.get("access") != "all-access":
            raise SystemExit(f"page views of {title}: unexpected item {it}")
        ts = it["timestamp"]
        out[f"{ts[:4]}-{ts[4:6]}"] = int(it["views"])
    return out, url, name


def last_day(month):
    y, m = int(month[:4]), int(month[5:7])
    nxt = datetime.date(y + (m == 12), m % 12 + 1, 1)
    return (nxt - datetime.timedelta(days=1)).strftime("%Y%m%d")

# ------------------------------------------------------------------------------------- archived copies (Wayback Machine)


API_RE = re.compile(r"^https?://(?:www\.)?forbes\.com(?::\d+)?/forbesapi/person/([a-z0-9-]+)/(\d{4})/([^?]*)(?:\?(.*))?$")
AJAX_RE = re.compile(r"^https?://(?:www\.)?forbes\.com(?::\d+)?/ajax/list/data\?(.*)$")
ROW_FILTERS = ("multiSelectFilters", "query", "searchColumns")


def classify(original):
    """(address, list, year, whole) of an archived URL of Forbes's person-list data, or None. whole: the list itself
    (newer address: in rank order from its first row, with no row filter or search, so a first page counts)."""
    m = API_RE.match(original or "")
    if m:
        lst, year, path, query = m.groups()
        if lst not in LISTS:
            return None
        params = dict(urllib.parse.parse_qsl(query or "", keep_blank_values=True))
        whole = (path in ("position/true.json", "rank/true.json") and not any(k in params for k in ROW_FILTERS)
                 and params.get("start", "0") == "0")
        return "forbesapi", lst, int(year), whole
    m = AJAX_RE.match(original or "")
    if m:
        params = dict(urllib.parse.parse_qsl(m.group(1).replace("&amp;", "&"), keep_blank_values=True))
        if (params.get("type") == "person" and params.get("uri") in LISTS
                and re.fullmatch(r"\d{4}", params.get("year") or "")):
            return "ajax", params["uri"], int(params["year"]), set(params) == {"year", "uri", "type"}
    return None


def capture_parts(url):
    """(timestamp, original URL) of a capture URL https://web.archive.org/web/<timestamp>/<original>."""
    m = re.match(r"https://web\.archive\.org/web/(\d{14})/(https?://.+)$", url or "")
    return (m.group(1), m.group(2)) if m else (None, None)


def capture(ts, original):
    """(text or None, the capture URL as written, cache name) of an archived copy, read as it was archived."""
    raw = WAYBACK_RAW.format(ts=ts, url=urllib.parse.quote(original, safe=":/?&=%,;+@!$'()*~#[]"))
    name = "wayback/" + hashlib.sha256(raw.encode("utf-8")).hexdigest()[:16] + ".json.gz"
    text = fetch(raw, name, required=False, check=is_json, gz=True, pause=1.5, attempts=6, backoff=10)
    return text, WAYBACK.format(ts=ts, url=original), name


def capture_rows(text, address):
    """{uri: row} of the person rows an archived copy holds, or None when it holds none (an empty list, an error body)."""
    if text is None:
        return None
    try:
        d = json.loads(text)
    except ValueError:
        return None
    if address == "forbesapi":
        rows = (d.get("personList") or {}).get("personsLists") if isinstance(d, dict) else None
    else:
        rows = d if isinstance(d, list) else None
    out = {r["uri"]: r for r in rows or () if isinstance(r, dict) and r.get("uri")}
    return out or None


def capture_field(address, rows, override):
    """(the field holding the list's value in US$ millions, whether the list's rank is kept) for an archived copy, or
    (None, False) when the copy's layout needs an override in sources.json."""
    if override:
        return override["field"], bool(override.get("rank"))
    if address == "forbesapi":
        return "finalWorth", True
    # the older list data: worth and rank are the list's when the copy also carries real-time fields; a copy whose rows
    # carry archivedWorth and no real-time fields keeps real-time figures in worth and rank (the Forbes 400 2014 copy)
    n_arch = sum(1 for r in rows.values() if r.get("archivedWorth") is not None)
    n_rt = sum(1 for r in rows.values() if r.get("realTimeWorth") is not None)
    if n_arch > len(rows) / 2 and n_rt == 0:
        return None, False
    return "worth", True


def archive_index(rc):
    """([(address, list, year, whole, timestamp, original)] of status-200 captures, cache names), or (None, names)
    when an index query is not available."""
    caps, names = set(), []
    for url in rc["index"]:
        name = "wayback/index-" + hashlib.sha256(url.encode("utf-8")).hexdigest()[:16] + ".json.gz"
        text = fetch(url, name, required=False, check=lambda t: is_json(t) and t.lstrip()[:1] == "[", gz=True, pause=2,
                     attempts=6, backoff=15)
        if text is None:
            return None, names
        names.append(name)
        rows = json.loads(text)
        limit = int(urllib.parse.parse_qs(urllib.parse.urlsplit(url).query)["limit"][0])
        if len(rows) - 1 >= limit:
            raise SystemExit(f"archive index {url}: {len(rows) - 1} rows, its limit (raise it)")
        head = rows[0] if rows else []
        for r in rows[1:]:
            rec = dict(zip(head, r))
            if rec.get("statuscode") != "200":
                continue
            c = classify(rec.get("original"))
            if c:
                caps.add(c + (rec["timestamp"], rec["original"]))
    return sorted(caps), names


def ts_date(ts):
    return f"{ts[:4]}-{ts[4:6]}-{ts[6:8]}"


def days_between(a, b):
    return (datetime.date.fromisoformat(b) - datetime.date.fromisoformat(a)).days


def revision_check(rc, feeds, cands, f0, f1):
    """Compares each candidate's value (and rank, where kept) on today's lists with archived copies of the list data.
    cands: {uri: name}. Returns (summary for the file, value differences, rank differences, problems, log lines)."""
    caps, names = archive_index(rc)
    near = rc["nearListDays"]
    if caps is None:
        return ({"status": "not run: the Wayback Machine's index could not be read", "index": rc["index"]},
                [], [], [], ["CHECK revisions: not run: the Wayback Machine's index could not be read"])
    overrides = {o["url"]: o for o in rc.get("overrides") or []}
    used_overrides = set()
    problems, lines, diffs, rank_diffs = [], [], [], []
    by = collections.defaultdict(list)
    n_window = collections.Counter()
    for address, lst, year, whole, ts, orig in caps:
        if f0 <= year <= f1:
            by[(lst, year)].append((address, whole, ts, orig))
            n_window[address] += 1
    list_years = []
    cover = {k: {lst: [] for lst in LISTS} for k in ("nearList", "onlyLaterCopies", "noReadableCopy", "noArchivedCopy")}
    n_caps = collections.Counter()
    n_values = n_ranks = 0
    for lst in LISTS:
        for year in range(f0, f1 + 1):
            fe = feeds[(lst, year)]
            today = {u: fe["rows"][u] for u in sorted(cands) if u in fe["rows"]}
            allc = sorted(by.get((lst, year), []), key=lambda c: (c[2], c[3]))
            if not allc:
                cover["noArchivedCopy"][lst].append(year)
                continue
            skipped, chosen = [], []

            def use(c):
                text, shown, _ = capture(c[2], c[3])
                rows = capture_rows(text, c[0])
                if rows is None:
                    skipped.append({"url": shown, "why": "not read: the download failed" if text is None
                                    else "holds no rows of the list"})
                    return None
                return c, shown, rows, text

            for c in (c for c in allc if c[0] == "ajax" and c[1]):  # every copy at the older address
                got = use(c)
                if got:
                    chosen.append(got)
            api = [c for c in allc if c[0] == "forbesapi" and c[1]]
            first = None
            for c in api:  # the earliest readable whole-list copy at the newer address
                first = use(c)
                if first:
                    chosen.append(first)
                    break
            if first:
                for c in reversed(api):  # and the latest
                    if (c[2], c[3]) <= (first[0][2], first[0][3]):
                        break
                    got = use(c)
                    if got:
                        chosen.append(got)
                        break
            compared = []
            for (address, whole, ts, orig), shown, rows, text in sorted(chosen, key=lambda g: (g[0][2], g[0][3])):
                ov = overrides.get(shown)
                if ov:
                    used_overrides.add(shown)
                    for q in ov.get("quotes") or []:
                        if q not in text:
                            problems.append(f"revisionCheck override for {shown}: quote not found verbatim: {q}")
                field, rank_kept = capture_field(address, rows, ov)
                if field is None:
                    problems.append(f"{shown}: its rows carry archivedWorth and no real-time fields; say in sources.json "
                                    f"revisionCheck.overrides which field holds the list's value")
                    continue
                n_list = sum(1 for r in rows.values() if r.get(field) is not None)
                partial = n_list < WHOLE_SHARE * len(fe["rows"])
                same = differ = beyond = ranks = 0
                for uri, cur in today.items():
                    row = rows.get(uri)
                    if row is None or row.get(field) is None:
                        if partial:
                            beyond += 1
                        else:
                            differ += 1
                            diffs.append((uri, lst, year, shown, None, cur["finalWorth"] / 1000))
                        continue
                    n_values += 1
                    if abs(row[field] - cur["finalWorth"]) > 1e-6:
                        differ += 1
                        diffs.append((uri, lst, year, shown, row[field] / 1000, cur["finalWorth"] / 1000))
                    else:
                        same += 1
                    if rank_kept and row.get("rank") is not None:
                        ranks += 1
                        n_ranks += 1
                        if row["rank"] != cur.get("rank"):
                            rank_diffs.append((uri, lst, year, shown, row["rank"], cur.get("rank")))
                for uri in sorted(set(cands) - set(today)):  # on the archived list but not on today's: dropped later
                    row = rows.get(uri)
                    if row is not None and row.get(field) is not None:
                        differ += 1
                        diffs.append((uri, lst, year, shown, row[field] / 1000, None))
                n_caps[address] += 1
                item = {"url": shown, "capturedOn": ts_date(ts), "daysAfterList": days_between(fe["listDate"], ts_date(ts)),
                        "address": address, "field": field, "ranksCompared": ranks, "firstPageOnly": partial,
                        "candidates": len(today) - beyond, "same": same, "differ": differ}
                if beyond:
                    item["beyondThisPage"] = beyond
                compared.append(item)
            if not compared:
                cover["noReadableCopy"][lst].append(year)
            elif compared[0]["daysAfterList"] <= near:
                cover["nearList"][lst].append(year)
            else:
                cover["onlyLaterCopies"][lst].append(year)
            entry = {"list": lst, "year": year, "listDate": fe["listDate"], "copies": len(allc), "compared": compared}
            if skipped:
                entry["skipped"] = skipped
            list_years.append(entry)
            parts = []
            for it in compared:
                parts.append(f"{it['capturedOn']} ({it['daysAfterList']:+d} d, {it['address']}, {it['field']}"
                             f"{', first page' if it['firstPageOnly'] else ''}): {it['same']} of {it['candidates']} same"
                             f"{', ' + str(it['differ']) + ' differ' if it['differ'] else ''}"
                             f"{', ' + str(it.get('beyondThisPage')) + ' beyond the page' if it.get('beyondThisPage') else ''}"
                             f"{', ranks ' + str(it['ranksCompared']) if it['ranksCompared'] else ', ranks not kept'}")
            lines.append(f"    {LISTS[lst]} {year} (list date {fe['listDate']}): {len(allc)} archived "
                         f"cop{'y' if len(allc) == 1 else 'ies'}; compared " + ("; ".join(parts) or "none") +
                         (f"; skipped {len(skipped)} ({', '.join(sorted(set(s['why'] for s in skipped)))})" if skipped else ""))
    for url in overrides:
        if url not in used_overrides:
            problems.append(f"revisionCheck override for {url}: no such copy was compared")
    summary = {
        "status": "run",
        "nearListDays": near,
        "index": [{"url": u, "retrieved": retrieved(n)} for u, n in zip(rc["index"], names)],
        "capturesInWindow": dict(sorted(n_window.items())),
        "copiesCompared": dict(sorted(n_caps.items())),
        "valuesCompared": n_values,
        "differences": len(diffs),
        "ranksCompared": n_ranks,
        "rankDifferences": len(rank_diffs),
        **{k: {lst: ys for lst, ys in v.items()} for k, v in cover.items()},
        "listYears": list_years,
    }
    return summary, diffs, rank_diffs, problems, lines

# ------------------------------------------------------------------------------------------------------------ helpers


def norm(s):
    """A name for matching: accents and case folded, a trailing '& family' dropped, letters and digits only."""
    s = unicodedata.normalize("NFKD", s or "")
    s = "".join(c for c in s if not unicodedata.combining(c)).casefold()
    s = re.sub(r"\s*&\s*family$", "", s)
    return re.sub(r"[^a-z0-9]", "", s)


def birth_year(ms):
    """The year of a Forbes birthDate (epoch milliseconds), or None."""
    if ms is None:
        return None
    return datetime.datetime.fromtimestamp(ms / 1000, UTC).year


def list_date(ms):
    """The UTC date of a Forbes date field (epoch milliseconds)."""
    return datetime.datetime.fromtimestamp(ms / 1000, UTC).date().isoformat() if ms is not None else None


def list_date_of(rows):
    """(date, entries on it, entries): the most common UTC date of a list's entries (ties: the earlier date)."""
    c = collections.Counter(list_date(r["date"]) for r in rows if r.get("date") is not None)
    if not c:
        return None, 0, len(rows)
    d, n = sorted(c.items(), key=lambda kv: (-kv[1], kv[0]))[0]
    return d, n, len(rows)


def months(start, end):
    """['YYYY-MM', ...] from start to end inclusive."""
    y, m = int(start[:4]), int(start[5:7])
    out = []
    while f"{y}-{m:02d}" <= end:
        out.append(f"{y}-{m:02d}")
        y, m = (y + 1, 1) if m == 12 else (y, m + 1)
    return out


def months_before(year, month, n):
    """The n months before the given one, latest first: months_before(2017, 3, 2) = ['2017-02', '2017-01']."""
    out = []
    y, m = year, month
    for _ in range(n):
        y, m = (y - 1, 12) if m == 1 else (y, m - 1)
        out.append(f"{y}-{m:02d}")
    return out


def years_text(years):
    """'2016-2025', '2025' or '2005-2020, 2022' for a sorted list of years."""
    runs = []
    for y in years:
        if runs and y == runs[-1][1] + 1:
            runs[-1][1] = y
        else:
            runs.append([y, y])
    return ", ".join(str(a) if a == b else f"{a}-{b}" for a, b in runs)


def by_list_text(d):
    """"The World's Billionaires 2006, 2008; The Forbes 400 2005-2013" for {list: [years]}, or "none"."""
    return "; ".join(f"{LISTS[lst]} {years_text(ys)}" for lst, ys in d.items() if ys) or "none"


def fmt_views(v):
    return f"{v / 1e6:.2f}M" if v >= 1e6 else f"{v / 1e3:.0f}K"


def share(k, m):
    return round(k / m, 4) if m else None

# ------------------------------------------------------------------------------------------------------------ the rules


def moves(nw, pv, years):
    """Year-on-year directions of log net worth and log page views over consecutive years with both values. Returns
    (k years both moved the same way, m years compared, flat years in which a series did not move (counted in m, not in
    k), [(year, nw sign, pv sign)])."""
    k = m = flat = 0
    rows = []
    for y in years[1:]:
        if None in (nw.get(y), nw.get(y - 1), pv.get(y), pv.get(y - 1)):
            continue
        a = math.log(nw[y]) - math.log(nw[y - 1])
        b = math.log(pv[y]) - math.log(pv[y - 1])
        sa = (a > 0) - (a < 0)
        sb = (b > 0) - (b < 0)
        rows.append((y, sa, sb))
        m += 1
        if sa == 0 or sb == 0:
            flat += 1
        elif sa == sb:
            k += 1
    return k, m, flat, rows


def arrow(s):
    return "+" if s > 0 else "-" if s < 0 else "="


def pick_rule(stats, highest, exclude):
    """(the candidate with the highest or lowest share k/m, the others tied with it on the share); ties are broken by
    more years, then the id."""
    pool = [s for s in stats if s["id"] not in exclude and s["m"]]
    if not pool:
        return None, []
    frac = lambda s: fractions.Fraction(s["k"], s["m"])  # noqa: E731
    key = (lambda s: (-frac(s), -s["m"], s["id"])) if highest else (lambda s: (frac(s), -s["m"], s["id"]))
    ranked = sorted(pool, key=key)
    return ranked[0], [s for s in ranked[1:] if frac(s) == frac(ranked[0])]

# --------------------------------------------------------------------------------------------------------------- build


def main():
    if OFFLINE and REFRESH:
        raise SystemExit("--refresh downloads; it cannot be combined with --offline")
    src = json.load(open(SRC, encoding="utf-8"))
    rules = src["rules"]
    win = src["window"]
    pv_start, pv_end = win["pageViews"]["from"], win["pageViews"]["to"]
    y0, y1 = win["annual"]["from"], win["annual"]["to"]
    f0, f1 = win["forbes"]["from"], win["forbes"]["to"]
    annual_years = list(range(y0, y1 + 1))
    series_list = rules["netWorthList"]
    problems = []

    def check(line):
        print("CHECK " + line)

    # people.json: the US people, and who among them is a public adult
    with open(PEOPLE, "rb") as f:
        people_bytes = f.read()
    people_doc = json.loads(people_bytes.decode("utf-8"))
    us = [p for p in people_doc["people"] if p.get("region") == "us"]
    us_people = [p for p in us if p.get("kind") == "p"]
    ents, wd_names = wikidata([p["q"] for p in us_people if p.get("q")])

    def enwiki(p):
        e = ents.get(p.get("q") or "") or {}
        return ((e.get("sitelinks") or {}).get("enwiki") or {}).get("title")

    def public_adult(p, first_year):
        """(ok, reasons): kind p, a Wikidata item and an English Wikipedia article, a known and unestimated birth year,
        and 18 or older on 1 January of the first year of data whatever the birthday (born at least 19 years before).
        The reasons name no birth year, so they can be printed for someone who fails."""
        why = []
        if p.get("kind") != "p":
            why.append("not a person")
        if not p.get("q"):
            why.append("no Wikidata item")
        elif not enwiki(p):
            why.append("no English Wikipedia article")
        if p.get("born") is None:
            why.append("birth year unknown")
        elif p.get("est"):
            why.append("birth year estimated")
        elif p["born"] > first_year - 19:
            why.append("under 18 for some or all of the first year of data")
        return not why, why

    # names to match on Forbes's lists: people.json's name, the Wikidata English label and aliases, and the extra
    # Forbes spellings in sources.json (forbesNames: handle -> Forbes name)
    extra = {h: n for h, n in (src.get("forbesNames") or {}).items()}
    by_name = {}
    for i, p in enumerate(us_people):
        keys = {norm(p["name"])}
        e = ents.get(p.get("q") or "") or {}
        if (e.get("labels") or {}).get("en"):
            keys.add(norm(e["labels"]["en"]["value"]))
        for a in (e.get("aliases") or {}).get("en", []):
            keys.add(norm(a["value"]))
        if p.get("handle") in extra:
            keys.add(norm(extra[p["handle"]]))
        for k in keys:
            if len(k) >= 4:
                by_name.setdefault(k, set()).add(i)

    # Forbes's list feeds: who of people.json is on which list, by Forbes uri (a name match confirmed by birth year
    # names the uri; every entry of that uri then belongs to the person, also those without a birth date)
    feeds = {}
    uri_of = {}
    unconfirmed = set()
    for lst in LISTS:
        for year in range(f0, f1 + 1):
            rows, url, name, text = forbes_feed(lst, year, refresh=REFRESH and year >= AS_OF_FROM)
            ld, on_ld, n_rows = list_date_of(rows)
            feeds[(lst, year)] = {"rows": {r["uri"]: r for r in rows}, "url": url, "name": name, "text": text,
                                  "listDate": ld, "onListDate": on_ld, "entries": n_rows}
            for r in rows:
                for i in by_name.get(norm(r.get("personName")), ()):
                    by = birth_year(r.get("birthDate"))
                    born = us_people[i].get("born")
                    if by is not None and born is not None and abs(by - born) <= 1:
                        uri_of.setdefault(i, set()).add(r["uri"])
                    elif by is not None:
                        unconfirmed.add((i, r["uri"], by, year))
    for i, uris in uri_of.items():
        if len(uris) > 1:
            problems.append(f"{us_people[i]['name']} matches several Forbes entries: {sorted(uris)}")
    matched = {i: sorted(u)[0] for i, u in uri_of.items()}
    unnamed = set()
    for i, uri, by, year in sorted(unconfirmed, key=lambda t: (us_people[t[0]]["name"], t[1], t[3])):
        if uri in matched.values():
            continue
        p = us_people[i]
        if public_adult(p, min(year, int(pv_start[:4])))[0]:
            print(f"  name match not confirmed by birth year: {p['name']} (born {p['born']}) vs Forbes {uri} (born {by})")
        else:
            unnamed.add(i)
    if unnamed:
        print(f"  name match not confirmed by birth year: {len(unnamed)} people.json account(s) that fail the public-adult "
              f"rule (not named)")
    if REFRESH:
        print(f"  --refresh: re-downloaded the {AS_OF_FROM}+ list feeds: " +
              ", ".join(f"{LISTS[lst]} {y} ({retrieved(feeds[(lst, y)]['name'])})" for (lst, y) in sorted(feeds) if y >= AS_OF_FROM))

    def entries(uri):
        return {(lst, y): feeds[(lst, y)]["rows"][uri] for (lst, y) in feeds if uri in feeds[(lst, y)]["rows"]}

    check(f"people.json (sha256 {hashlib.sha256(people_bytes).hexdigest()[:16]}): {len(us)} US accounts, {len(us_people)} people; "
          f"Forbes feeds read: {len(feeds)} "
          f"({', '.join(LISTS[lst] for lst in LISTS)} {f0}-{f1}); US people of people.json on any list: {len(matched)}")

    # candidates: public adults with at least minAnnualPoints values of the series list in the years compared. Someone
    # who fails the public-adult rule is counted, with the rule's reasons, and never named or identified.
    candidates, not_candidates = [], []
    excluded = collections.Counter()
    n_excluded = 0
    for i, uri in sorted(matched.items(), key=lambda kv: us_people[kv[0]]["name"]):
        p = us_people[i]
        ent = entries(uri)
        first = min([y for (_, y) in ent] + [int(pv_start[:4])])
        ok, why = public_adult(p, first)
        if not ok:
            n_excluded += 1
            excluded.update(why)
            continue
        n_series = sum(1 for y in annual_years if (series_list, y) in ent)
        by_list = {lst: sorted(y for (l2, y) in ent if l2 == lst) for lst in LISTS}
        span = "; ".join(f"{LISTS[lst]} {len(v)} ({years_text(v)})" if v else f"{LISTS[lst]} 0" for lst, v in by_list.items())
        if n_series < rules["minAnnualPoints"]:
            yrs = [y for y in annual_years if (series_list, y) in ent]
            reason = (f"{n_series} annual value{'s' if n_series != 1 else ''} of {LISTS[series_list]} in {y0}-{y1}"
                      f"{' (' + years_text(yrs) + ')' if yrs else ''}; needs {rules['minAnnualPoints']}")
        else:
            reason = None
        print(f"  {p['name']:<24} Forbes {uri:<24} {span}; in {y0}-{y1}: {n_series} -> "
              f"{'candidate' if reason is None else 'not a candidate (' + reason + ')'}")
        if reason is None:
            candidates.append((p, uri, ent))
        else:
            not_candidates.append({"name": p["name"], "handle": p.get("handle"), "wikidata": p.get("q"), "forbes": uri,
                                   "lists": {lst: v for lst, v in by_list.items()}, "reason": reason})
    if n_excluded:
        print(f"  [not named] {n_excluded} person(s) on a Forbes list fail the public-adult rule ("
              + ", ".join(f"{w}: {n}" for w, n in sorted(excluded.items())) + "); counted, not written")

    # the hand-read values: one record per candidate in sources.json, keyed by Forbes uri
    hand = {r["id"]: r for r in src["people"]}
    cand_ids = [uri for _, uri, _ in candidates]
    for uri in cand_ids:
        if uri not in hand:
            problems.append(f"candidate {uri} has no hand-read Forbes values in sources.json")
    for uri in hand:
        if uri not in cand_ids:
            problems.append(f"sources.json reads {uri}, who is not a candidate")
    check(f"candidates: {len(candidates)} public adults with >= {rules['minAnnualPoints']} annual {LISTS[series_list]} values in "
          f"{y0}-{y1}: {', '.join(p['name'] for p, _, _ in candidates)}; not candidates: {len(not_candidates)} public adults "
          f"with fewer values, and {n_excluded} person(s) who fail the public-adult rule (counted, not named)")

    # page views and the Forbes values of each candidate, with their checks
    pv_months = months(pv_start, pv_end)
    out_people = []
    quotes_total = quotes_ok = 0
    revisions = []
    person_quotes = {}  # id -> [found, total]
    for p, uri, ent in candidates:
        rec = hand.get(uri)
        if rec is None:
            continue
        title = enwiki(p)
        first = min([y for (_, y) in ent] + [int(pv_start[:4])])
        if rec.get("wikidata") != p.get("q") or rec.get("handle") != p.get("handle"):
            problems.append(f"{uri}: sources.json names {rec.get('handle')}/{rec.get('wikidata')}, people.json {p.get('handle')}/{p.get('q')}")

        # monthly page views
        views, pv_url, pv_name = page_views(title, pv_start, pv_end)
        missing = [m for m in pv_months if m not in views]
        extra_months = sorted(m for m in views if m not in pv_months)
        if extra_months:
            problems.append(f"{p['name']}: page views outside the window: {extra_months[:3]}")
        series = [views.get(m) for m in pv_months]
        pv_year = {}
        for y in range(int(pv_start[:4]), int(pv_end[:4]) + 1):
            ms = [m for m in pv_months if m.startswith(f"{y}-")]
            if len(ms) == 12 and all(views.get(m) is not None for m in ms):
                pv_year[y] = sum(views[m] for m in ms)

        # Forbes values: verify each hand-read entry against the copy it cites, and require one per feed entry
        values = []
        seen = set()
        for v in rec["forbes"]:
            key = (v["list"], v["year"])
            fe = feeds.get(key)
            quotes_total += 1
            person_quotes.setdefault(uri, [0, 0])[1] += 1
            if not (isinstance(v.get("value"), (int, float)) and v["value"] > 0):
                problems.append(f"{p['name']} {key}: value {v.get('value')!r} is not a positive number")
                continue
            if v.get("archived"):
                # an earlier copy kept as a Forbes revision: verified against the archived copy it cites
                ts, orig = capture_parts(v["url"])
                c = classify(orig)
                text, shown, _ = capture(ts, orig) if ts and c else (None, v["url"], None)
                rows = capture_rows(text, c[0]) if c else None
                found = text is not None and v["quote"] in text
                quotes_ok += found
                person_quotes[uri][0] += found
                if text is None:
                    problems.append(f"{p['name']} {key}: the archived copy it cites could not be read ({v['url']})")
                elif not found:
                    problems.append(f"{p['name']} {key}: archived quote not found verbatim in {v['url']}")
                row = (rows or {}).get(uri) or {}
                fld = v.get("valueField")
                if row.get(fld) is None or abs(row[fld] / 1000 - v["value"]) > 1e-9:
                    problems.append(f"{p['name']} {key}: archived value {v['value']} is not the copy's {fld} / 1000")
                ov = {o["url"]: o for o in src["revisionCheck"].get("overrides") or []}.get(shown)
                rank_kept = (ov is None or bool(ov.get("rank"))) and row.get("rank") is not None
                if v.get("rank") is not None and (not rank_kept or row["rank"] != v["rank"]):
                    problems.append(f"{p['name']} {key}: archived rank {v['rank']} is not the copy's rank "
                                    f"({row['rank'] if rank_kept else 'the copy keeps no list rank'})")
                cur = (fe or {}).get("rows", {}).get(uri)
                if cur is not None and abs(cur["finalWorth"] / 1000 - v["value"]) <= 1e-9:
                    problems.append(f"{p['name']} {key}: the archived value equals today's, so it is not a revision")
                revisions.append((uri, p["name"], v, cur["finalWorth"] / 1000 if cur else None))
                values.append(v)
                continue
            if key in seen:
                problems.append(f"{p['name']}: two current values for {key}")
            seen.add(key)
            if fe is None:
                problems.append(f"{p['name']} {key}: no feed read for this list and year")
                continue
            if v["url"] != fe["url"]:
                problems.append(f"{p['name']} {key}: cites {v['url']}, not the list's feed")
            row = fe["rows"].get(uri)
            found = v["quote"] in fe["text"]
            quotes_ok += found
            person_quotes[uri][0] += found
            if not found:
                problems.append(f"{p['name']} {key}: quote not found verbatim in {fe['url']}")
            m = re.search(r'"finalWorth":([0-9.]+)', v["quote"])
            if not m or abs(float(m.group(1)) / 1000 - v["value"]) > 1e-9:
                problems.append(f"{p['name']} {key}: value {v['value']} is not the quote's finalWorth / 1000")
            mr = re.search(r'"rank":(\d+)', v["quote"])
            if not mr or int(mr.group(1)) != v.get("rank"):
                problems.append(f"{p['name']} {key}: rank {v.get('rank')} is not the quote's rank")
            if row is None:
                problems.append(f"{p['name']} {key}: not on today's list (a revision would be marked archived)")
            else:
                if abs(row["finalWorth"] / 1000 - v["value"]) > 1e-9:
                    problems.append(f"{p['name']} {key}: today's feed says {row['finalWorth'] / 1000}, sources.json {v['value']}")
                if row.get("rank") != v.get("rank"):
                    problems.append(f"{p['name']} {key}: today's feed ranks it {row.get('rank')}, sources.json {v.get('rank')}")
                if row.get("month") != v.get("month"):
                    problems.append(f"{p['name']} {key}: today's feed gives month {row.get('month')}, sources.json {v.get('month')}")
                if list_date(row.get("date")) != v.get("entryDate"):
                    problems.append(f"{p['name']} {key}: today's feed dates the entry {list_date(row.get('date'))}, "
                                    f"sources.json {v.get('entryDate')}")
            values.append(v)
        for key in sorted(ent):
            if key not in seen:
                problems.append(f"{p['name']}: Forbes {key[0]} {key[1]} is on the list but not read into sources.json")

        current = {(v["list"], v["year"]): v for v in values if not v.get("archived")}
        nw_year = {y: current[(series_list, y)]["value"] for y in annual_years if (series_list, y) in current}
        k, mm, flat, rows = moves(nw_year, pv_year, annual_years)
        base_nw, base_pv = nw_year.get(y0), pv_year.get(y0)
        out_forbes = []
        for v in sorted(values, key=lambda v: (v["year"], list(LISTS).index(v["list"]), bool(not v.get("archived")))):
            fe = feeds.get((v["list"], v["year"])) or {}
            e = {"list": v["list"], "listName": LISTS[v["list"]], "year": v["year"], "month": v.get("month"),
                 "listDate": fe.get("listDate"), "entryDate": v.get("entryDate"), "value": v["value"],
                 "unit": "billion US dollars", "rank": v.get("rank"), "source": v["source"], "url": v["url"],
                 "quote": v["quote"], "field": v.get("field")}
            if v.get("archived"):
                e["revision"] = v.get("note")
                e["archivedOn"] = v.get("archivedOn")
                e["current"] = False
                ts, orig = capture_parts(v["url"])
                e["verifiedOn"] = retrieved(capture(ts, orig)[2]) if ts else None
            else:
                e["current"] = True
                e["verifiedOn"] = retrieved(fe["name"])
            if v["year"] >= AS_OF_FROM:
                e["asOf"] = fe.get("listDate")
            if v.get("note") and not v.get("archived"):
                e["note"] = v["note"]
            out_forbes.append({kk: vv for kk, vv in e.items() if vv is not None})
        n_cur = len(current)
        n_series = len(nw_year)
        gaps = ("no gaps (the source has a row for every month)" if not missing else
                f"{len(missing)} month(s) the source has no row for, kept null and not filled: {', '.join(missing)}")
        print(f"  {p['name']}: page views {sum(1 for x in series if x is not None)} of {len(pv_months)} months "
              f"{pv_start}..{pv_end}, {gaps}; "
              f"Forbes {n_cur} values ({sum(1 for kk in current if kk[0] == 'billionaires')} Billionaires, "
              f"{sum(1 for kk in current if kk[0] == 'forbes-400')} Forbes 400) each with URL and quote"
              f"{', plus ' + str(len(values) - n_cur) + ' earlier revision(s)' if len(values) > n_cur else ''}; "
              f"{LISTS[series_list]} {y0}-{y1}: {n_series} annual points")
        if n_series < rules["minAnnualPoints"]:
            problems.append(f"{p['name']}: {n_series} annual points, fewer than {rules['minAnnualPoints']}")
        out_people.append({
            "id": uri,
            "name": p["name"],
            "handle": p.get("handle"),
            "wikidata": p.get("q"),
            "born": p.get("born"),
            "publicAdult": {"kind": p.get("kind"), "born": p["born"], "bornEstimated": bool(p.get("est")),
                            "wikipedia": True, "adultFrom": p["born"] + 18, "firstDataYear": first,
                            "source": "people.json (kind, born, est, q) and the Wikidata sitelink below",
                            "rule": "a person with a Wikidata item and an English Wikipedia article, birth year known and not "
                                    "estimated, 18 or older on 1 January of the first year of data"},
            "wikipedia": {"value": title, "url": f"https://www.wikidata.org/wiki/{p['q']}",
                          "article": WIKI_PAGE.format(urllib.parse.quote(title.replace(" ", "_"))),
                          "source": "Wikidata, the item's English Wikipedia sitelink (read through the wbgetentities API)",
                          "field": f"entities.{p['q']}.sitelinks.enwiki.title", "retrieved": max(retrieved(n) for n in wd_names)},
            "followers": {"value": p.get("fol"), "year": 2025, "label": src["followersLabel"],
                          "source": "people.json, the social graph's follower snapshot (see the file's note)",
                          "url": "Assets/Resources/Data/social/people.json", "field": f"people[handle={p.get('handle')}].fol",
                          "note": "a different measure from page views: drawn once, as a star, never joined to the page-view line"},
            "forbes": out_forbes,
            "pageViews": {"value": series, "start": pv_start, "end": pv_end, "unit": "views a month",
                          "source": "Wikimedia REST API, pageviews per article: en.wikipedia, all-access, user agent, monthly",
                          "url": pv_url, "field": "items[].views (one per month; a month the source has no row for is null)",
                          "retrieved": retrieved(pv_name), "missing": missing,
                          "note": "views of the article under its current title; views that arrive through redirects are counted by the source under the redirect's own title and are not included"},
            "annual": {"years": annual_years,
                       "netWorth": [nw_year.get(y) for y in annual_years],
                       "netWorthList": series_list,
                       "pageViews": [pv_year.get(y) for y in annual_years],
                       "netWorthIndex": [round(nw_year[y] / base_nw, 4) if base_nw and y in nw_year else None for y in annual_years],
                       "pageViewsIndex": [round(pv_year[y] / base_pv, 4) if base_pv and y in pv_year else None for y in annual_years],
                       "moves": {str(y): arrow(a) + arrow(b) for y, a, b in rows},
                       "computed": f"netWorth: {LISTS[series_list]} estimate of each year (billion US dollars); pageViews: the "
                                   f"calendar year's monthly views summed; indices: each over its {y0} value; moves: the "
                                   f"signs of the year-on-year change of log net worth and log page views ('=' unchanged: "
                                   f"such a year counts as one in which they did not move the same way)"},
            "together": {"k": k, "m": mm, "flat": flat, "share": share(k, mm), "caption": f"together in {k} of {mm} years"},
        })
        print(f"    {y0}-{y1}: " + " ".join(f"{y}:{arrow(a)}{arrow(b)}" for y, a, b in rows) +
              f"  together {k} of {mm}" + (f" ({flat} with a series that did not move)" if flat else ""))

    # everyone named in the file passes the public-adult rule (candidates, and the public adults with too few values)
    named = [(r["handle"], r["id"]) for r in out_people] + [(r["handle"], r["forbes"]) for r in not_candidates]
    by_handle = {p.get("handle"): p for p in us_people}
    for handle, uri in named:
        p = by_handle.get(handle)
        ent = entries(uri)
        if p is None or not public_adult(p, min([y for (_, y) in ent] + [int(pv_start[:4])]))[0]:
            problems.append(f"a person written into the file fails the public-adult rule ({uri})")
    check(f"public adults only: everyone named in the file passes the rule ({len(out_people)} candidates and "
          f"{len(not_candidates)} others on Forbes lists; kind p, Wikidata item and English Wikipedia article, birth year "
          f"known and not estimated, 18+ from the first year of their data); {n_excluded} person(s) on a Forbes list fail "
          f"it and are written only as a count, with no name or id")
    check(f"Forbes values: {quotes_ok} of {quotes_total} hand-read quotes found verbatim in the cached copy they cite; value, "
          f"rank, month and entry date checked against it; {len(revisions)} earlier revision(s) kept as separate values")
    for _, name, v, cur in revisions:
        print(f"    revision: {name} {LISTS[v['list']]} {v['year']}: ${v['value']:g}B in the archived copy "
              f"({v.get('archivedOn')}), ${cur:g}B today" if cur is not None else f"    revision: {name} {v}")

    # Forbes's own revisions: archived copies of the list data compared with today's
    rc = src["revisionCheck"]
    rev, diffs, rank_diffs, rprob, rlines = revision_check(rc, feeds, {r["id"]: r["name"] for r in out_people}, f0, f1)
    problems += rprob
    names_of = {r["id"]: r["name"] for r in out_people}
    if rev["status"] == "run":
        for line in rlines:
            print(line)
        kept = {(u, v["list"], v["year"], v["url"]): v["value"] for u, _, v, _ in revisions}
        for uri, lst, year, shown, a, t in diffs:
            if a is None:
                problems.append(f"{names_of[uri]} {LISTS[lst]} {year}: on today's list but not in the whole archived copy "
                                f"{shown}")
            elif abs(kept.get((uri, lst, year, shown), -1) - a) > 1e-9:
                now = f"${t:g}B today" if t is not None else "not on today's list"
                problems.append(f"{names_of[uri]} {LISTS[lst]} {year}: ${a:g}B in {shown}, {now}; keep the archived "
                                f"value in sources.json (archived: true) or explain it")
        for uri, lst, year, shown, a, t in rank_diffs:
            print(f"    rank differs: {names_of[uri]} {LISTS[lst]} {year}: rank {a} in {shown}, {t} today")
        n_copies = sum(rev["copiesCompared"].values())
        check(f"revisions: the Wayback Machine's index lists {sum(rev['capturesInWindow'].values())} status-200 captures "
              f"of the two lists' data in {f0}-{f1} ({', '.join(f'{k} {n}' for k, n in rev['capturesInWindow'].items())}); "
              f"{n_copies} copies compared ({', '.join(f'{k} {n}' for k, n in rev['copiesCompared'].items())}) for "
              f"{len(rev['listYears'])} list-years; {rev['valuesCompared']} candidate values compared, "
              f"{rev['differences']} differ; {rev['ranksCompared']} ranks compared, {rev['rankDifferences']} differ; "
              f"earlier estimates kept as separate values: {len(revisions)}")
        check(f"revision coverage: earliest copy within {rev['nearListDays']} days of the list date (values as first "
              f"published, or nearly): {by_list_text(rev['nearList'])}; only later copies (a revision made before the "
              f"copy would not show): {by_list_text(rev['onlyLaterCopies'])}; no archived copy of the list data at either "
              f"address: {by_list_text(rev['noArchivedCopy'])}"
              + (f"; captures listed, but no readable copy of the whole list among them: {by_list_text(rev['noReadableCopy'])}"
                 if any(rev["noReadableCopy"].values()) else ""))
    else:
        check(rlines[0][len("CHECK "):])

    # the rules
    stats = [{"id": r["id"], "name": r["name"], **{k: r["together"][k] for k in ("k", "m", "flat", "share")}}
             for r in out_people]
    frac = lambda s: fractions.Fraction(s["k"], s["m"]) if s["m"] else fractions.Fraction(-1)  # noqa: E731
    ranked_high = sorted(stats, key=lambda s: (-frac(s), -s["m"], s["id"]))
    picks, ties = {}, {}
    together, ties["together"] = pick_rule(stats, True, set())
    if together:
        picks["together"] = together
    apart, ties["apart"] = pick_rule(stats, False, {together["id"]} if together else set())
    if apart:
        picks["apart"] = apart
    print("RULE together (highest share of years moving the same way): " +
          ", ".join(f"{s['name']} {s['k']}/{s['m']}" + (f" ({s['flat']} flat)" if s["flat"] else "") for s in ranked_high))
    for rule in ("together", "apart"):
        s = picks.get(rule)
        if s:
            t = ties[rule]
            tie = ""
            if t:
                by_years = any(x["m"] != s["m"] for x in t)
                tie = (f"; tied on the share with {', '.join(x['name'] + ' (' + str(x['k']) + ' of ' + str(x['m']) + ')' for x in t)}, "
                       f"broken by {'more years' if by_years else 'the id (alphabetical)'}")
            check(f"{rule}: {s['name']}, together in {s['k']} of {s['m']} years (share {s['share']:.3f}"
                  f"{'; ' + str(s['flat']) + ' year(s) in which a series did not move, counted as not together' if s['flat'] else ''})"
                  f"{tie}")
        else:
            problems.append(f"no pick for {rule}")

    # little attention: the richest Forbes-400 American not among the most-followed accounts, with the fewest page views
    la = rules["littleAttention"]
    approval = src["shortlist"]
    approved = bool(approval.get("author_approved"))
    want = (approval.get("approved") or {}) if approved else {}
    f400 = []
    for r in out_people:
        v = next((x for x in r["forbes"] if x["list"] == "forbes-400" and x["year"] == y1 and x["current"]), None)
        if v is None:
            continue
        views_y1 = r["annual"]["pageViews"][annual_years.index(y1)]
        if views_y1 is None:
            problems.append(f"{r['name']}: no complete page views for {y1}")
            continue
        f400.append({"id": r["id"], "name": r["name"], "netWorth": v["value"], "followers": r["followers"]["value"],
                     "pageViews": views_y1})
    taken = {s["id"] for s in picks.values()}
    pool = [x for x in f400 if x["id"] not in taken]
    cloud = la["mostFollowedFrom"]
    quiet = [x for x in pool if x["followers"] < cloud]
    proposal = None
    if quiet:
        chosen = sorted(quiet, key=lambda x: (-x["netWorth"], x["pageViews"], x["id"]))[0]
        condition = True
        basis = (f"the richest on the Forbes 400 {y1} with fewer than {cloud / 1e6:g}M followers (outside the attention "
                 f"cloud), among the candidates the earlier rules did not pick")
    elif pool:
        condition = False
        proposal = sorted(pool, key=lambda x: (x["pageViews"], -x["netWorth"], x["id"]))[0]
        accepted = approved and want.get("littleAttention") == proposal["id"]
        chosen = proposal if accepted else None
        basis = (f"no candidate on the Forbes 400 {y1} is outside the attention cloud ({cloud / 1e6:g}M followers or more: "
                 f"all {len(f400)}), so the rule picks nobody; fallback proposal: the fewest page views in {y1} among the "
                 f"candidates the earlier rules did not pick" +
                 ("; the author accepted it" if accepted else "; not the pick until the author accepts it"))
    else:
        chosen, condition, basis = None, None, f"no candidate on the Forbes 400 {y1}"
    print("RULE little attention, Forbes 400 " + str(y1) + ": " +
          ", ".join(f"{x['name']} ${x['netWorth']:g}B, {x['followers'] / 1e6:.1f}M followers, {fmt_views(x['pageViews'])} views"
                    for x in sorted(f400, key=lambda x: -x["netWorth"])) +
          (f"; already picked: {', '.join(sorted(taken))}" if taken else ""))
    if condition:
        check(f"little attention: {chosen['name']} (Forbes 400 {y1}: ${chosen['netWorth']:g}B; {fmt_views(chosen['pageViews'])} "
              f"page views in {y1}; {chosen['followers'] / 1e6:.1f}M followers); {basis}")
    elif proposal:
        check(f"little attention: {'pick ' + chosen['name'] + ' (accepted fallback)' if chosen else 'no pick'}: {basis}: "
              f"{proposal['name']} ({fmt_views(proposal['pageViews'])} page views in {y1}; Forbes 400 {y1} "
              f"${proposal['netWorth']:g}B; {proposal['followers'] / 1e6:.1f}M followers, inside the cloud)")
        overall = sorted(f400, key=lambda x: (x["pageViews"], -x["netWorth"], x["id"]))[0]
        if overall["id"] == proposal["id"]:
            print("  (the earlier picks do not change the proposal: it has the fewest page views of all the Forbes-400 candidates)")
    else:
        problems.append("little attention: no candidate on the Forbes 400")
    # the literal reading would look outside people.json: printed for the author, not built (candidates are people.json's)
    inside = set(matched.values())
    outside = sorted((r for u, r in feeds[("forbes-400", y1)]["rows"].items() if u not in inside),
                     key=lambda r: (-r["finalWorth"], r["uri"]))
    if outside:
        r = outside[0]
        print(f"  literal reading, not built: the richest on the Forbes 400 {y1} with no account in people.json is "
              f"{r['personName']} (${r['finalWorth'] / 1000:g}B, rank {r.get('rank')}); candidates are limited to people.json")

    # the same rules with page views aligned to each spring list (the 12 months before its month): a check of how much
    # the picks depend on the calendar-year alignment; printed only, not written
    alt = []
    for r in out_people:
        views = dict(zip(pv_months, r["pageViews"]["value"]))
        cur = {v["year"]: v for v in r["forbes"] if v["list"] == series_list and v["current"]}
        nw, pv = {}, {}
        for y in range(y0, f1 + 1):
            v = cur.get(y)
            if not v:
                continue
            win12 = months_before(y, v["month"], 12)
            if all(views.get(w) is not None for w in win12):
                nw[y], pv[y] = v["value"], sum(views[w] for w in win12)
        k, mm, flat, _ = moves(nw, pv, list(range(y0, f1 + 1)))
        alt.append({"id": r["id"], "name": r["name"], "k": k, "m": mm, "flat": flat, "share": share(k, mm),
                    "years": f"{min(nw)}-{max(nw)}" if nw else "-"})
    at, _ = pick_rule(alt, True, set())
    ap, _ = pick_rule(alt, False, {at["id"]} if at else set())
    print("ALIGNMENT CHECK (page views of the 12 months before each spring list's month instead of the calendar year): " +
          ", ".join(f"{s['name']} {s['k']}/{s['m']} ({s['years']})" for s in alt) +
          (f"; together would be {at['name']} ({at['k']} of {at['m']}), apart {ap['name']} ({ap['k']} of {ap['m']})" if at and ap else ""))

    # the shortlist and its approval
    shortlist = []
    for rule in ("together", "apart"):
        s = picks.get(rule)
        if s:
            shortlist.append({"rule": rule, "id": s["id"], "name": s["name"], "k": s["k"], "m": s["m"], "share": s["share"],
                              "caption": f"together in {s['k']} of {s['m']} years"})
    la_item = {"rule": "littleAttention", "id": chosen["id"] if chosen else None, "name": chosen["name"] if chosen else None,
               "conditionMet": condition, "basis": basis}
    if chosen:
        la_item.update({"netWorth": chosen["netWorth"], "pageViews": chosen["pageViews"], "followers": chosen["followers"]})
    if proposal:
        la_item["fallback"] = {**proposal, "accepted": bool(chosen)}
    shortlist.append(la_item)
    if approved:
        for item in shortlist:
            w = want.get(item["rule"])
            if item["rule"] == "littleAttention" and not condition and proposal:
                if w not in (None, proposal["id"]):
                    problems.append(f"approved shortlist names {w} for littleAttention; the rule picks nobody and proposes "
                                    f"{proposal['id']}")
            elif w != item["id"]:
                problems.append(f"approved shortlist names {w} for {item['rule']}; the rules now pick {item['id']}")
    check(f"shortlist: {', '.join((i['name'] or 'no pick') + ' (' + i['rule'] + ')' for i in shortlist)}"
          f"{'; little attention: fallback proposal ' + proposal['name'] + (' accepted' if chosen else ' awaiting the author') if proposal else ''}"
          f"; author_approved: {str(approved).lower()} (sources.json shortlist.author_approved)")
    by_id = {r["id"]: r for r in out_people}
    shown_people = [(i["rule"], i["id"], "chosen") for i in shortlist if i["id"]]
    if proposal and not chosen:
        shown_people.append(("littleAttention", proposal["id"], "fallback proposal"))
    for rule, pid, role in shown_people:
        r = by_id[pid]
        pv = r["pageViews"]
        cur = [v for v in r["forbes"] if v["current"]]
        fq = person_quotes.get(pid, [0, 0])
        if rule == "littleAttention":
            x = next(x for x in f400 if x["id"] == pid)
            score = (f"{fmt_views(x['pageViews'])} page views in {y1}, Forbes 400 {y1} ${x['netWorth']:g}B"
                     f"{'' if condition else ' (fallback)'}")
        else:
            score = f"together in {r['together']['k']} of {r['together']['m']} years"
        check(f"{role}, {rule}: {r['name']}: {score}; page views {sum(1 for x in pv['value'] if x is not None)} of "
              f"{len(pv['value'])} months {pv['start']}..{pv['end']}, "
              f"{'no gaps' if not pv['missing'] else str(len(pv['missing'])) + ' month(s) without a row in the source (null)'}; "
              f"Forbes {len(cur)} values ({sum(1 for v in cur if v['list'] == series_list and y0 <= v['year'] <= y1)} annual "
              f"{LISTS[series_list]} points in {y0}-{y1}), each with URL and quote, {fq[0]} of {fq[1]} quotes found verbatim")

    built = max(retrieved(n) for n in sorted(USED) if retrieved(n))
    ranking = lambda key: [{"id": s["id"], "k": s["k"], "m": s["m"], "flat": s["flat"], "share": s["share"]}  # noqa: E731
                           for s in sorted(stats, key=key)]
    doc = {
        "note": src["note"],
        "built": built,
        "window": {"pageViews": {"from": pv_start, "to": pv_end}, "annual": {"from": y0, "to": y1}, "forbes": {"from": f0, "to": f1}},
        "measures": src["measures"],
        "followersLabel": src["followersLabel"],
        "rules": {
            "minAnnualPoints": rules["minAnnualPoints"],
            "netWorthList": series_list,
            "together": {"rule": rules["together"], "pick": picks["together"]["id"] if picks.get("together") else None,
                         "tiedWith": [s["id"] for s in ties["together"]],
                         "ranking": ranking(lambda s: (-frac(s), -s["m"], s["id"]))},
            "apart": {"rule": rules["apart"], "pick": picks["apart"]["id"] if picks.get("apart") else None,
                      "tiedWith": [s["id"] for s in ties["apart"]],
                      "ranking": ranking(lambda s: (frac(s) if s["m"] else fractions.Fraction(2), -s["m"], s["id"]))},
            "littleAttention": {"rule": la["rule"], "fallbackRule": la["fallback"], "mostFollowedFrom": cloud, "year": y1,
                                "conditionMet": condition, "pick": chosen["id"] if chosen else None,
                                "fallback": ({**proposal, "accepted": bool(chosen)} if proposal else None), "basis": basis,
                                "ranking": [{"id": x["id"], "netWorth": x["netWorth"], "followers": x["followers"],
                                             "pageViews": x["pageViews"]} for x in sorted(f400, key=lambda x: (x["pageViews"], x["id"]))]},
            "distinct": "each rule picks among the candidates the earlier rules did not pick (order: together, apart, little attention)",
        },
        "shortlist": {"authorApproved": approved, "picks": shortlist, "note": approval.get("note")},
        "forbesLists": {
            "note": ("listDate: the most common UTC date of the list's entries in today's list data (entries: rows on the "
                     "list; onListDate: rows with that date). Before 2013 it is the first day of the list's month for most "
                     "entries, so it marks the month rather than a day of publication or of measurement; an entry's own "
                     "date is its entryDate"),
            "byList": {lst: {str(y): {"listDate": feeds[(lst, y)]["listDate"], "entries": feeds[(lst, y)]["entries"],
                                      "onListDate": feeds[(lst, y)]["onListDate"]}
                             for y in range(f0, f1 + 1)} for lst in LISTS}},
        "forbesRevisions": {"kept": len(revisions), "method": rc["note"], **{k: v for k, v in rev.items()},
                            "note": "an archived value that differs from today's is kept as its own value with current "
                                    "false; listYears gives every copy compared, its capture date, days after the list "
                                    "date, the field read and how many candidates matched"},
        "people": out_people,
        "notCandidates": not_candidates,
        "excludedByPublicAdultRule": {"count": n_excluded, "reasons": dict(sorted(excluded.items())),
                                      "note": "people of people.json on a Forbes list who fail the public-adult rule are "
                                              "counted here and never named or identified"},
    }
    for p in problems:
        print("PROBLEM:", p, file=sys.stderr)
    if problems:
        sys.exit(1)
    text = json.dumps(doc, indent=1, ensure_ascii=False, allow_nan=False) + "\n"
    with open(OUT, "w", encoding="utf-8") as fh:
        fh.write(text)
    print(f"wrote {os.path.relpath(OUT, ROOT)}: {len(out_people)} candidates, {len(not_candidates)} others on Forbes lists, "
          f"{n_excluded} counted only; sha256 {hashlib.sha256(text.encode('utf-8')).hexdigest()}")


if __name__ == "__main__":
    main()

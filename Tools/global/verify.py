#!/usr/bin/env python3
"""The 2026 verification gate for Assets/Resources/Data/economy/global.json (MODALITIES.md section 6.8), and the
download cache that Tools/global/build.py shares with it.

The gate. For every value or event dated 2026 (and, by the same rule, every dated event the world view could draw, each
further source an event cites for a date or a scope, and every recorded outcome of an ended event):
  1. fetch the source URL into Tools/global/.cache, recording the fetch date beside the download;
  2. find the quoted passage verbatim in the fetched text (whitespace collapsed, curly quotes straightened, Unicode
     compatibility forms folded: nothing else) and check that each of the item's values appears inside the quote;
  3. ask Jev (TypeSafe System One, through Tools/jev.py) whether the fetched text around the quote supports the claim:
     a Noul, stored as supportedJ; the item passes at 0.6 or more. If the Jev credential is unavailable, the gate fails
     closed (every gated item is unverified);
  4. write status (verified | unverified), verifiedOn (the fetch date), quote, quoteFound, valuesFound and supportedJ.
An item passes only with the verbatim quote AND the score: Jev's probability is model evidence, not proof. Unverified
items stay in global.json with drawn = false. An event is verified only when its own item and every further source it
cites pass. An event whose stated end date is before the build's as-of date needs a recorded outcome that passes the
same gate (verbatim quote and Jev's support), or it is not drawn either.

Usage (from the repository root):
    python3 -I Tools/global/verify.py            # re-run the gate on global.json (refetching sources older than today)
    python3 -I Tools/global/verify.py --offline  # re-run it from the cache only
build.py runs the same gate as its last step, so a normal build needs no separate call.

The cache. Every download is stored once under Tools/global/.cache/<name> with <name>.meta.json beside it (URL, fetch
date, HTTP status, content type, size, sha256; or the error of a failed fetch, so --offline reproduces failures too).
Jev answers are cached by the sha256 of the exact request. The Jev credential is found by Tools/jev.py and never
printed or stored.
"""
import datetime
import hashlib
import html
import io
import json
import os
import re
import subprocess
import sys
import time
import unicodedata
import zipfile
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
CACHE = os.path.join(HERE, ".cache")
OUT = os.path.join(ROOT, "Assets", "Resources", "Data", "economy", "global.json")
sys.dont_write_bytecode = True   # leave no __pycache__ beside the tools
sys.path.insert(0, os.path.join(ROOT, "Tools"))
import jev  # noqa: E402  (Tools/jev.py: credential discovery; the key is never printed)

AGENT = "why-2-global/0.1 (https://github.com/atonalfreerider/why-2)"
# for the few hosts that refuse a non-browser agent; it still names the project and carries no personal data
BROWSER_AGENT = "Mozilla/5.0 (X11; Linux x86_64) why-2-global/0.1 (+https://github.com/atonalfreerider/why-2)"
THRESHOLD = 0.6
JEV_QUESTION = {
    "supported": {
        "type": "noul",
        "instructions": "`evidence.excerpt` is text fetched from `evidence.url` (a web page, PDF or data table). Does it state "
                        "`claim.statement`, or directly imply that it is true, including its numbers and dates? Answer yes "
                        "only if the excerpt itself supports the whole claim; a claim the excerpt does not address, or "
                        "contradicts, is a no.",
    }
}

# ------------------------------------------------------------------------------------------------------------- cache


class Cache:
    """Downloads into .cache/<name> once; later runs read the file. Online runs retry earlier failures; --offline never
    touches the network and replays the recorded outcome (body or failure) of every download."""

    def __init__(self, offline, log=print):
        self.offline = offline
        self.log = log
        self.failed = {}       # name -> (url, error) of downloads that failed in this build
        self.used = {}         # name -> meta of every download read in this build
        os.makedirs(CACHE, exist_ok=True)

    def path(self, name):
        return os.path.join(CACHE, name)

    def meta(self, name):
        p = self.path(name) + ".meta.json"
        if os.path.exists(p):
            with open(p, encoding="utf-8") as f:
                return json.load(f)
        return None

    def get(self, url, name, headers=None, agent=AGENT, refresh_before=None, data=None, timeout=150, tries=3, min_bytes=1):
        """(body bytes or None, meta). refresh_before (an ISO date): online, refetch a copy fetched before that day."""
        path, meta = self.path(name), self.meta(name)
        stale = meta is None or "error" in meta or (refresh_before and meta.get("fetched", "") < refresh_before)
        if stale and not self.offline:
            meta = self._download(url, path, headers or {}, agent, data, timeout, tries, min_bytes)
            with open(path + ".meta.json", "w", encoding="utf-8") as f:
                json.dump(meta, f, indent=1, sort_keys=True)
        if meta is None:
            meta = {"url": url, "error": "--offline: not cached"}
        self.used[name] = meta
        if "error" in meta or not os.path.exists(path):
            self.failed[name] = (url, meta.get("error", "missing file"))
            return None, meta
        with open(path, "rb") as f:
            return f.read(), meta

    def _download(self, url, path, headers, agent, data, timeout, tries, min_bytes):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        tmp = path + ".part"
        last = "no attempt"
        compressed = True
        for attempt in range(tries):
            cmd = ["curl", "-sSL"] + (["--compressed"] if compressed else []) + ["--max-time", str(timeout), "-A", agent, "-o", tmp,
                                                                              "-w", "%{http_code}\t%{content_type}\t%{url_effective}"]
            for k, v in headers.items():
                cmd += ["-H", f"{k}: {v}"]
            if data is not None:
                cmd += ["--data-binary", "@-"]
            cmd.append(url)
            r = subprocess.run(cmd, input=data, capture_output=True, timeout=timeout + 30)
            out = r.stdout.decode("utf-8", "replace").split("\t")
            status = out[0] if out else ""
            final = out[2] if len(out) > 2 else url
            if r.returncode == 0 and status.startswith("2") and os.path.exists(tmp) and blocked(tmp, final):
                last = f"HTTP {status}, but a bot-check page ({final[:60]})"
                os.remove(tmp)
                break
            if r.returncode == 0 and status.startswith("2") and os.path.exists(tmp) and os.path.getsize(tmp) >= min_bytes:
                os.replace(tmp, path)
                with open(path, "rb") as f:
                    body = f.read()
                return {"url": url, "fetched": datetime.date.today().isoformat(), "status": int(status),
                        "contentType": out[1] if len(out) > 1 else "", "finalUrl": out[2] if len(out) > 2 else url,
                        "bytes": len(body), "sha256": hashlib.sha256(body).hexdigest()}
            last = f"HTTP {status or '-'} (curl exit {r.returncode})" if r.returncode or not status.startswith("2") else \
                f"HTTP {status}, body under {min_bytes} bytes"
            if os.path.exists(tmp):
                os.remove(tmp)
            if r.returncode == 56:   # some servers end the TLS stream early when asked for a compressed body
                compressed = False
            if status in ("403", "404", "410"):
                break
            time.sleep(2 * (attempt + 1))
        if os.path.exists(path):
            os.remove(path)
        return {"url": url, "fetched": datetime.date.today().isoformat(), "error": last}

    def drop(self, name):
        """Forget a cached download (used once to clear bot-check pages cached before blocked() existed)."""
        for p in (self.path(name), self.path(name) + ".meta.json"):
            if os.path.exists(p):
                os.remove(p)

    def text(self, url, name, **kw):
        body, meta = self.get(url, name, **kw)
        return (None if body is None else body.decode("utf-8", "replace")), meta

    def derived(self, name, suffix, make):
        """A derived file (e.g. pdftotext output) cached beside its source, so --offline does not depend on the tool."""
        path = self.path(name) + suffix
        if not os.path.exists(path):
            if self.offline:
                raise SystemExit(f"--offline: derived {name}{suffix} is not cached")
            content = make()
            with open(path, "w", encoding="utf-8") as f:
                f.write(content)
        with open(path, encoding="utf-8") as f:
            return f.read()

    def fetched(self, name):
        m = self.used.get(name) or self.meta(name) or {}
        return m.get("fetched")

BLOCK_MARKERS = (b"<title>Just a moment", b"Attention Required! | Cloudflare", b"Request Access</title>",
                 b"cf-browser-verification", b"<title>Access Denied</title>", b"captcha-delivery", b"Please enable JS and disable")


def blocked(path, final_url):
    """True for a bot-check or access-request page served with HTTP 200 instead of the document."""
    if "unblock." in final_url or "/captcha" in final_url:
        return True
    with open(path, "rb") as f:
        head = f.read(6000)
    return any(m in head for m in BLOCK_MARKERS)

# --------------------------------------------------------------------------------------------------------------- text


def pdf_text(cache, name, layout=True):
    """pdftotext output of a cached PDF (-layout keeps table rows; the default reading order keeps sentences)."""
    path = cache.path(name)

    def make():
        args = ["pdftotext"] + (["-layout"] if layout else []) + [path, "-"]
        return subprocess.run(args, capture_output=True, timeout=300).stdout.decode("utf-8", "replace")
    return cache.derived(name, ".layout.txt" if layout else ".txt", make)


INLINE_TAGS = r"a|span|em|strong|b|i|u|sup|sub|abbr|font|small|mark|cite|q|code|time|dfn|bdi|bdo|s|ins|del"


def html_text(raw):
    """Visible text of an HTML page: scripts and styles dropped, inline tags removed without a gap (so 'pipeline</a>,'
    reads 'pipeline,'), block tags turned into line breaks or spaces, entities unescaped."""
    s = raw.decode("utf-8", "replace") if isinstance(raw, bytes) else raw
    s = re.sub(r"(?is)<(script|style|noscript|svg|template)[^>]*>.*?</\1>", " ", s)
    s = re.sub(r"(?is)<!--.*?-->", " ", s)
    s = re.sub(r"(?i)<br\s*/?>|</(p|div|li|tr|h\d|td|th|table|section|article|header|footer|ul|ol|dt|dd)>", "\n", s)
    s = re.sub(r"(?is)</?(%s)(\s[^>]*)?>" % INLINE_TAGS, "", s)
    return html.unescape(re.sub(r"(?s)<[^>]+>", " ", s))


def html_cells(raw):
    """Like html_text, but a table keeps its cells: each cell ends with ' |' and each row with a line break, so an empty
    cell stays visible ('2026 Apr- 6 to Apr-10 | | 138.21 | ...': no Monday price)."""
    s = raw.decode("utf-8", "replace") if isinstance(raw, bytes) else raw
    s = re.sub(r"(?i)</t[dh]>", " |", s)
    s = re.sub(r"(?i)</tr>", "\n", s)
    return html_text(s)


QUOTES =str.maketrans({"‘": "'", "’": "'", "‚": "'", "‛": "'", "′": "'",
                        "“": '"', "”": '"', "„": '"', "″": '"', "­": None})


def norm(s):
    """The gate's only normalisation: Unicode compatibility forms (ligatures, non-breaking spaces), curly quotes made
    straight, soft hyphens dropped, whitespace collapsed."""
    return re.sub(r"\s+", " ", unicodedata.normalize("NFKC", s or "").translate(QUOTES)).strip()

# --------------------------------------------------------------------------------------------------------------- xlsx


def xlsx_sheets(body):
    """{sheet name: {(row, col): value}} of an .xlsx file, read with the standard library (numbers as floats)."""
    z = zipfile.ZipFile(io.BytesIO(body))
    ns = {"m": "http://schemas.openxmlformats.org/spreadsheetml/2006/main",
          "r": "http://schemas.openxmlformats.org/officeDocument/2006/relationships"}
    shared = []
    if "xl/sharedStrings.xml" in z.namelist():
        for si in ET.fromstring(z.read("xl/sharedStrings.xml")).findall("m:si", ns):
            shared.append("".join(t.text or "" for t in si.iter("{%s}t" % ns["m"])))
    wb = ET.fromstring(z.read("xl/workbook.xml"))
    rels = ET.fromstring(z.read("xl/_rels/workbook.xml.rels"))
    target = {r.get("Id"): r.get("Target") for r in rels}
    out = {}
    for sh in wb.find("m:sheets", ns):
        rid = sh.get("{%s}id" % ns["r"])
        t = target[rid].lstrip("/")
        t = t if t.startswith("xl/") else "xl/" + t
        cells = {}
        root = ET.fromstring(z.read(t))
        for c in root.iter("{%s}c" % ns["m"]):
            ref = c.get("r")
            m = re.match(r"([A-Z]+)(\d+)", ref)
            col = 0
            for ch in m.group(1):
                col = col * 26 + ord(ch) - 64
            row = int(m.group(2))
            typ = c.get("t")
            v = c.find("m:v", ns)
            if typ == "s" and v is not None:
                val = shared[int(v.text)]
            elif typ == "inlineStr":
                val = "".join(x.text or "" for x in c.iter("{%s}t" % ns["m"]))
            elif typ in ("str", "e") and v is not None:
                val = v.text
            elif typ == "b" and v is not None:
                val = v.text == "1"
            elif v is not None and v.text is not None:
                try:
                    val = float(v.text)
                except ValueError:
                    val = v.text
            else:
                continue
            cells[(row, col)] = val
        out[sh.get("name")] = cells
    return out


def sheet_rows(cells):
    """[[values...]] by row (1-based rows and columns; missing cells None)."""
    if not cells:
        return []
    nr = max(r for r, _ in cells)
    nc = max(c for _, c in cells)
    return [[cells.get((r, c)) for c in range(1, nc + 1)] for r in range(1, nr + 1)]

# ---------------------------------------------------------------------------------------------------------------- Jev

JEV = {"requests": 0, "cached": 0, "tokens": 0, "model": None, "errors": 0}


def ask_jev(cache, state):
    """One Jev Noul (JEV_QUESTION), cached by the request's exact content. Returns (probability or None, why)."""
    blob = json.dumps({"state": state, "questions": JEV_QUESTION}, sort_keys=True, ensure_ascii=False)
    path = os.path.join(CACHE, "jev", hashlib.sha256(blob.encode("utf-8")).hexdigest() + ".json")
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            result = json.load(f)
        JEV["cached"] += 1
    else:
        if cache.offline:
            return None, "Jev answer not cached (--offline)"
        if not jev.available():
            return None, "Jev credential unavailable"
        result, last = None, None
        for attempt in range(3):
            try:
                result = jev.call(state, JEV_QUESTION, timeout=120)
                break
            except RuntimeError as e:  # jev.call never puts the key or the state in its message
                last = str(e)
                time.sleep(3 * (attempt + 1))
        if result is None:
            JEV["errors"] += 1
            return None, f"Jev error ({last})"
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8") as f:
            json.dump(result, f, sort_keys=True)
        JEV["requests"] += 1
    JEV["tokens"] += sum(v for v in (result.get("usage") or {}).values() if isinstance(v, (int, float)))
    if result.get("model"):
        JEV["model"] = result["model"]
    try:
        return round(float(result["answers"]["supported"]["noul"]), 3), "ok"
    except (KeyError, TypeError, ValueError):
        return None, "Jev answer malformed"

# --------------------------------------------------------------------------------------------------------------- gate


def number_forms(text):
    """The value strings to look for: as written, and with or without thousands separators."""
    t = str(text)
    forms = {t}
    if re.fullmatch(r"-?[\d,]+(\.\d+)?", t):
        forms.add(t.replace(",", ""))
        whole, _, frac = t.replace(",", "").partition(".")
        sign = "-" if whole.startswith("-") else ""
        whole = whole.lstrip("-")
        grouped = f"{int(whole):,}" if whole.isdigit() else whole
        forms.add(sign + grouped + ("." + frac if frac else ""))
    return forms


def contains_value(quote_n, value_text):
    """True when the value appears in the (normalised) quote as a whole token, not inside a longer number."""
    for form in number_forms(value_text):
        f = norm(form)
        for m in re.finditer(re.escape(f), quote_n):
            before = quote_n[m.start() - 1] if m.start() > 0 else " "
            after = quote_n[m.end()] if m.end() < len(quote_n) else " "
            if f[0].isdigit() and (before.isdigit() or (before in ".," and m.start() > 1 and quote_n[m.start() - 2].isdigit())):
                continue
            if f[-1].isdigit() and (after.isdigit() or (after in ".," and m.end() + 1 < len(quote_n) and quote_n[m.end() + 1].isdigit())):
                continue
            return True
    return False


def excerpt_around(text_n, quote_n, before=1500, after=2500):
    at = text_n.find(quote_n)
    if at < 0:
        return ""
    return text_n[max(0, at - before): at + len(quote_n) + after]


FR_API = "https://www.federalregister.gov/api/v1/documents/{}.json"


def gate_name(url):
    return "gate/" + hashlib.sha1(url.encode()).hexdigest()[:16]


def fetch_document(cache, item):
    """(body, meta, cache name) of the document an item cites. A Federal Register document is read through the FR API's
    raw text (its HTML pages answer automated requests with an access form); everything else from its URL."""
    rb = item.get("refreshBefore")
    fr = (item.get("fetch") or {}).get("fr")
    if fr:
        api = f"fr/{fr}.json"
        body, meta = cache.get(FR_API.format(fr), api, refresh_before=rb)
        if body is None:
            return None, meta, api
        raw = json.loads(body).get("raw_text_url")
        if not raw:
            return None, dict(meta, error="the FR API gave no raw_text_url"), api
        name = f"fr/{fr}.txt"
        body, meta = cache.get(raw, name, refresh_before=rb, min_bytes=200)
        return body, meta, name
    url = (item.get("fetch") or {}).get("url") or item["url"]
    name = item.get("cacheName") or gate_name(url)
    body, meta = cache.get(url, name, headers=item.get("headers"), agent=BROWSER_AGENT if item.get("browserAgent") else AGENT,
                           refresh_before=rb, min_bytes=200)
    return body, meta, name


def cell_text(v):
    return "" if v is None else (f"{v:g}" if isinstance(v, float) else str(v))


def piie_text(sheets):
    """PIIE's tariff workbook, sheet 'Panel a', one line per tariff action with its Excel date written as an ISO date."""
    lines = []
    for row in sheet_rows(sheets.get("Panel a", {})):
        cells = [cell_text(v) for v in row]
        if len(row) > 1 and isinstance(row[1], float) and 30000 < row[1] < 80000:
            cells[1] = (datetime.date(1899, 12, 30) + datetime.timedelta(days=int(row[1]))).isoformat()
        lines.append(" | ".join(cells))
    return "\n".join(lines)


def sdmx_text(raw, columns):
    """An SDMX-CSV file reduced to the named columns (the rest is per-row metadata)."""
    import csv
    rows = list(csv.DictReader(io.StringIO(raw)))
    cols = columns or (list(rows[0].keys()) if rows else [])
    return "\n".join([" ".join(cols)] + [" ".join(r.get(c, "") for c in cols) for r in rows])


def document_texts(cache, item, body, name):
    """The texts of a fetched document (raw, before normalisation). PDFs are read in both reading orders."""
    kind = item.get("textKind")
    if body[:4] == b"%PDF":
        return [pdf_text(cache, name, layout=False), pdf_text(cache, name, layout=True)]
    if body[:2] == b"PK":
        sheets = xlsx_sheets(body)
        if kind == "piie":
            return [piie_text(sheets)]
        return ["\n".join("\n".join(" | ".join(cell_text(v) for v in row) for row in sheet_rows(c)) for c in sheets.values())]
    raw = body.decode("utf-8-sig", "replace")
    if kind == "ec-json":   # the European Commission press corner API: the release's title and HTML body
        j = json.loads(raw)
        d = j.get("docuLanguageResource") or {}
        return [(d.get("title") or "") + "\neventDate: " + str(j.get("eventDate") or "") + "\n" + html_text(d.get("htmlContent") or "")]
    if kind == "sdmx-csv":
        return [sdmx_text(raw, item.get("columns"))]
    if kind == "text":
        return [raw]
    if kind == "html-cells":   # a data table read cell by cell (empty cells kept)
        return [html_cells(raw)]
    return [html_text(raw) if "<" in raw[:3000] else raw]


def source_text(cache, item):
    """The fetched text an item is checked against: (normalised texts, meta, cache name)."""
    body, meta, name = fetch_document(cache, item)
    if body is None:
        return [], meta, name
    return [norm(t) for t in document_texts(cache, item, body, name)], meta, name


def check_item(cache, item, as_of):
    """Runs the gate on one item: {id, claim, url, quotes[], values[], dated2026, ...}. Returns the gate record."""
    rec = {"status": "unverified", "verifiedOn": None, "quoteFound": False, "valuesFound": False, "supportedJ": None}
    if "://" not in (item.get("url") or ""):
        rec["reason"] = "no source URL"
        return rec
    texts, meta, name = source_text(cache, item)
    rec["fetched"] = meta.get("fetched")
    if not texts:
        rec["reason"] = f"fetch failed ({meta.get('error', 'empty')})"
        return rec
    quotes = [q for q in item.get("quotes") or [] if q]
    if not quotes:
        rec["reason"] = "no quote recorded"
        return rec
    found, where = [], None
    for q in quotes:
        qn = norm(q)
        hit = next((t for t in texts if qn and qn in t), None)
        found.append(hit is not None)
        if hit is not None and where is None:
            where = (hit, qn)
    rec["quotesFound"] = f"{sum(found)}/{len(found)}"
    rec["quoteFound"] = all(found)
    vals = [str(v) for v in item.get("values") or []]
    joined = " … ".join(norm(q) for q, ok in zip(quotes, found) if ok)
    missing = [v for v in vals if not contains_value(joined, v)]
    rec["valuesFound"] = bool(vals) and not missing and rec["quoteFound"]
    if not rec["quoteFound"]:
        rec["reason"] = "quote not found verbatim on the fetched source"
        return rec
    if missing:
        rec["reason"] = "value not in the quote: " + ", ".join(missing)
        return rec
    if not vals:
        rec["reason"] = "no value recorded to check"
        return rec
    excerpt = excerpt_around(*where)
    if len(quotes) > 1:   # all quotes found: give Jev each quote's surroundings, trimmed
        parts = []
        for q in quotes:
            qn = norm(q)
            t = next(t for t in texts if qn in t)
            parts.append(excerpt_around(t, qn, 600, 900))
        excerpt = " [...] ".join(parts)
    if item.get("tight"):   # a data table: its headings, then the quoted rows themselves
        excerpt = " [...] ".join(norm(q) for q in quotes)
    heads = [norm(h) for h in item.get("context") or [] if any(norm(h) in t for t in texts)]
    if heads:
        excerpt = " [...] ".join(heads) + " [...] " + excerpt
    state = {"claim": {"statement": item["claim"]}, "evidence": {"url": item["url"], "excerpt": excerpt[:6000]}}
    p, why = ask_jev(cache, state)
    rec["supportedJ"] = p
    if p is None:
        rec["reason"] = f"fails closed: {why}"
        return rec
    if p < THRESHOLD:
        rec["reason"] = f"Jev support {p:.2f} below {THRESHOLD}"
        return rec
    if item.get("dated2026") and (rec["fetched"] is None or rec["fetched"] < as_of):   # a 2026 fact is re-read on the as-of date
        rec["reason"] = f"source fetched {rec['fetched']}, before the as-of date {as_of}"
        return rec
    rec.update(status="verified", verifiedOn=rec["fetched"])
    rec.pop("reason", None)
    return rec


def run_gate(cache, items, as_of, log=print):
    """Gates every item (in order, deterministically) and returns {id: record}."""
    out = {}
    for it in items:
        it = dict(it, refreshBefore=as_of if it.get("dated2026") else None)
        out[it["id"]] = check_item(cache, it, as_of)
    return out


def report(items, results, as_of, log=print):
    """Prints the gate report: every gated item with its status, date and quote status."""
    g26 = [i for i in items if i.get("dated2026")]
    other = [i for i in items if not i.get("dated2026")]
    for title, group in (("2026 gate", g26), ("citation check, events before 2026", other)):
        ok = sum(1 for i in group if results[i["id"]]["status"] == "verified")
        log(f"GATE {title}: {ok} verified, {len(group) - ok} unverified (as of {as_of}; Jev >= {THRESHOLD} and a verbatim quote)")
        for i in group:
            r = results[i["id"]]
            j = "-" if r.get("supportedJ") is None else f"{r['supportedJ']:.2f}"
            log(f"  {r['status']:<10} {str(i.get('date') or '')[:10]:<10} quote {'found' if r.get('quoteFound') else 'MISSING':<7} "
                f"{r.get('quotesFound', '-'):<4} J {j:<4} {i['id']:<28} {i.get('label', '')[:60]}"
                + (f"  ({r['reason']})" if r.get("reason") else ""))


def main():
    """Standalone: re-run the gate on the items of the last build (Tools/global/.cache/gate-items.json, written by build.py),
    print the report and every item whose verdict differs from global.json. It changes no file but the download cache;
    rebuild with build.py to carry new verdicts into global.json."""
    offline = "--offline" in sys.argv
    with open(OUT, encoding="utf-8") as f:
        doc = json.load(f)
    path = os.path.join(CACHE, "gate-items.json")
    if not os.path.exists(path):
        raise SystemExit("no gate items recorded: run Tools/global/build.py online first")
    with open(path, encoding="utf-8") as f:
        items = json.load(f)
    cache = Cache(offline)
    as_of = doc["asOf"] if offline else max(doc["asOf"], datetime.date.today().isoformat())
    print(f"re-running the gate on {len(items)} items as of {as_of} ({'offline' if offline else 'online'}); "
          f"Jev credential {'available' if jev.available() else 'UNAVAILABLE: the gate fails closed'}")
    results = run_gate(cache, items, as_of)
    report(items, results, as_of)
    before = {i["id"]: i for i in doc["gate"]["items"]}
    changed = [i for i in items if before.get(i["id"], {}).get("status") != results[i["id"]]["status"]]
    for i in changed:
        print(f"CHANGED {i['id']}: {before.get(i['id'], {}).get('status')} -> {results[i['id']]['status']} ({results[i['id']].get('reason') or 'ok'})")
    print(f"{len(changed)} verdicts differ from global.json (as of {doc['asOf']})" + ("; rebuild with Tools/global/build.py" if changed else ""))


if __name__ == "__main__":
    main()

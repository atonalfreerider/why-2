#!/usr/bin/env python3
"""Square thumbnails of the economy's actors from open sources, a licence manifest, and an atlas packer.

Real people, companies, governments, groups and places get their images from their Wikidata items through Wikimedia
Commons. Synthetic or typical people get generic figures from OpenMoji or Twemoji, never a photo of a real person.
The policy, the sources it rests on and a sample run are in Docs/research/holistic/images.md.

    python3 -I -B Tools/images/fetch.py fetch ITEMS.json --out DIR [--cache DIR] [--size 256] [--jev]
                                               [--offline] [--refresh] [--maxlag 5] [--patience 600]
    python3 -I -B Tools/images/fetch.py pack DIR [--atlas DIR/atlas] [--cell 128] [--page 2048] [--allow-unapproved]

fetch reads ITEMS.json, a list of actors:
    {"id": "elon-musk", "kind": "person", "qid": "Q317521"}
    {"id": "walmart", "kind": "company", "qid": "Q483551", "ticker": "WMT"}      the ticker cross-checks the QID
    {"id": "sweden", "kind": "government", "iso3": "SWE"}                         resolved through P298
    {"id": "uaw", "kind": "group", "search": "United Auto Workers", "hint": "American labor union"}
    {"id": "ussr", "kind": "government", "qid": "Q15180", "at": 1980}            the flag in use in 1980
    {"id": "opec", "kind": "group", "qid": "Q7795", "file": "OPEC Logo.svg", "as": "P154"}   a reviewed pin
    {"id": "coworker", "kind": "figure", "emoji": "1F9D1-200D-1F4BC", "set": "openmoji", "variant": "mask"}
Kinds: person (a public figure: a human with an English Wikipedia article, 18 or older), company, government, group,
place, figure. Identity: qid, iso3 (P298), ticker (P414 with qualifier P249) or search (a name, with an optional hint;
with --jev, a Jev Choice among the candidates and a companion Noul on whether any is the actor; without Jev a name
resolves only when one candidate of the right kind is left and its label is the name exactly). When several are given
they must agree. Options: label; at (a year, YYYY-MM or YYYY-MM-DD: the image in use then; default today, or the day
before a dissolved item's P576); prefer (a property order); file and as (a reviewed Commons file and the property it
stands for); crop (center, portrait, contain); box ([x, y, w, h] as fractions of the original, from a crop review);
monogram (the text of the fallback tile); approved (for a real person: when and where the user approved showing them;
pack leaves out a person without it). Figures: emoji (a hex code), set (openmoji or twemoji), variant (color, or
mask: OpenMoji's black line glyph turned white so the app can tint it).

It writes DIR/<id>.jpg or .png, size x size (photos center-cropped, logos, flags, arms and seals contained whole on
transparency, never cropped), and DIR/manifest.json: per actor its id, kind, label, QID and how it was resolved, file,
source page, author, licence, licence URL, attribution text, short credit, fetched date, sha256, restrictions and the
rulings on them, review notes and every rejected candidate with its reason; and a list of failures. An actor without
an acceptable image gets "file": null and a monogram.

pack reads DIR/manifest.json and the thumbnails (no network) and writes DIR/atlas/: actors-<n>.png pages of
cell x cell RGBA cells, each cell's shape baked into alpha (a circle for a person, a light rounded tile for a logo, the
flag at its own aspect with rounded corners and a dark rule, arms and seals as they are, a monogram tile when there is
no image or the logo is a wordmark wider than 2.5:1, whose wordmark also gets a two-cell wide cell); index.json (per id
the page, cell, UV rect with the origin at the bottom left as Unity uses, shape and attribution); contact.png (every
cell with its id, for the crop review); and CREDITS.md. Placement is append-only: an id keeps its cell across repacks
and a removed id leaves its cell empty until a deliberate repack (delete index.json).

Policy (the reasons are in images.md):
  - licences: public domain (including the PD mark), CC0, CC BY and CC BY-SA of any version, ported or IGO; anything
    else is rejected (GFDL only, FAL, OGL, custom attribution licences, non-free files, files nominated for deletion);
  - restrictions (Commons' restriction-* classes): trademarked, insignia, communist and rusmilitarism are kept for the
    actor's own image, a mark shown unaltered and only to identify it; personality only for a person's own portrait;
    noresize is a layout class that six templates share, so the template on the file page decides (Shahada,
    Palestinian insignia and SouthVietnam kept as identity; Apartheid symbol, Anti-LGBT symbol and Vietnamese
    extremist symbol rejected); nazi, ihl, currency, design, costume, fan-art, 2257, ita-mibac, aus-reserve and any
    unknown class are rejected;
  - a person's image is only their own P18 portrait, and only for a public adult;
  - a collective's photo is the last resort and is rejected when its description, categories or depicted subjects
    suggest people in it (with --jev also when Jev gives 0.3 or more that private individuals are shown);
  - a pinned file must be the actor's own image: with --jev it is rejected unless Jev gives 0.5 or more that it refers
    to the actor and under 0.5 that something ties it to another owner (a file found by name can be a namesake's:
    Commons' "UAW logo.jpg" is the Union of Australian Women's). Commons is never searched by file name automatically.

Wikimedia etiquette: a User-Agent with the repository URL; one request at a time and at least 0.35 s between requests
(under the 200 a minute allowed to an identified, unauthenticated client); batched titles and ids; gzip for API calls;
maxlag=5 on api.php reads, waiting at least 5 s and the Retry-After after a lag error, up to --patience seconds in all
(Wikidata counts its query service's update lag in maxlag, which can refuse reads for hours; a run someone is waiting
on may pass --maxlag 0, as Manual:Maxlag_parameter allows for interactive tasks); 429 and 503 honour Retry-After
(else back off from 5 s); thumbnails only at the standard widths. Every response is
cached under --cache (the sha256 of the URL, as Tools/social/wikidata.py does) with a manifest.json of downloads (URL,
retrieval date, sha256, as Tools/lives/build.py does), so reruns are free and --offline rebuilds every output
byte for byte. Images are processed with ffmpeg and ffprobe alone (no PIL).
"""
import argparse
import datetime
import gzip
import hashlib
import html
import http.client as httpclient
import json
import os
import re
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from email.utils import parsedate_to_datetime

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
VERSION = "0.2"
AGENT = "why-2-images-bot/%s (https://github.com/atonalfreerider/why-2) Python-urllib/%d.%d" % (
    (VERSION,) + sys.version_info[:2])
WIKIDATA_API = "https://www.wikidata.org/w/api.php"
COMMONS_API = "https://commons.wikimedia.org/w/api.php"
WDQS = "https://query.wikidata.org/sparql"
# https://www.mediawiki.org/wiki/Common_thumbnail_sizes: the imageinfo API rounds other widths up, and direct
# requests for other widths are refused (T414805).
STANDARD_WIDTHS = (20, 40, 60, 120, 250, 330, 500, 960, 1280, 1920, 3840)
FONT_BOLD = "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf"
FONT = "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf"

OPENMOJI_VERSION, TWEMOJI_VERSION = "17.0.0", "17.0.3"
OPENMOJI_SVG = "https://cdn.jsdelivr.net/npm/openmoji@%s/{}/svg/{}.svg" % OPENMOJI_VERSION
OPENMOJI_DATA = "https://cdn.jsdelivr.net/npm/openmoji@%s/data/openmoji.json" % OPENMOJI_VERSION
OPENMOJI_PAGE = "https://openmoji.org/library/emoji-{}/"
TWEMOJI_SVG = "https://cdn.jsdelivr.net/gh/jdecked/twemoji@v%s/assets/svg/{}.svg" % TWEMOJI_VERSION
TWEMOJI_PAGE = "https://github.com/jdecked/twemoji/blob/v%s/assets/svg/{}.svg" % TWEMOJI_VERSION
FIGURE_SETS = {
    "openmoji": {"name": "OpenMoji", "version": OPENMOJI_VERSION, "licence": "CC BY-SA 4.0", "code": "cc-by-sa-4.0",
                 "url": "https://creativecommons.org/licenses/by-sa/4.0/",
                 "credit": "OpenMoji, the open-source emoji and icon project (https://openmoji.org/)"},
    "twemoji": {"name": "Twemoji", "version": TWEMOJI_VERSION, "licence": "CC BY 4.0", "code": "cc-by-4.0",
                "url": "https://creativecommons.org/licenses/by/4.0/",
                "credit": "Twemoji, Copyright 2014-2021 Twitter, Inc and 2022-present Jason Sofonia & Justine De "
                          "Caires and other contributors (https://github.com/jdecked/twemoji)"},
}

PROPS = {"P18": "image", "P154": "logo", "P8972": "small logo or icon", "P2910": "icon", "P41": "flag",
         "P94": "coat of arms", "P158": "seal"}
ORDER = {
    "person": ["P18"],
    "company": ["P8972", "P154", "P2910", "P18"],
    "government": ["P41", "P94", "P158", "P8972", "P154", "P2910", "P18"],
    "place": ["P41", "P94", "P158", "P154", "P8972", "P18"],
    "group": ["P8972", "P154", "P41", "P2910", "P94", "P158", "P18"],
}
MARKS = {"P154", "P8972", "P2910", "P41", "P94", "P158"}
KINDS = set(ORDER) | {"figure"}
NOUN = {"P18": "Portrait", "P154": "Logo", "P8972": "Logo", "P2910": "Icon", "P41": "Flag", "P94": "Arms", "P158": "Seal"}
KIND_NOUN = {"person": "a person", "company": "a company", "government": "a state or government",
             "group": "a group, union, alliance or organization", "place": "a city, region or other place"}
HUMAN = "Q5"
LIGHT_ON_DARK = "Q6545942"  # P8798 "for color scheme": light-on-dark; tiles are light, so it goes last
NOT_ACTORS = {"Q4167410": "disambiguation page", "Q4167836": "Wikimedia category", "Q13406463": "Wikimedia list",
              "Q11266439": "Wikimedia template", "Q14204246": "Wikimedia project page", "Q101352": "family name",
              "Q202444": "given name", "Q12308941": "male given name", "Q11879590": "female given name",
              "Q13442814": "scholarly article"}
IDENTITY_MIN = 0.5  # Jev's identity choice is accepted at this probability (as Tools/social/resolve.py)
IDENTITY_ANY = 0.7  # ... and only when its companion Noul (is any candidate the actor?) is a clear yes
PRIVATE_PEOPLE_MAX = 0.3  # a collective's photo is rejected when Jev gives this or more for private individuals
OWN_IMAGE_MIN = 0.5  # a pinned file is kept only when Jev gives this or more that it refers to the actor
OTHER_OWNER_MAX = 0.5  # ... and under this that something ties it to another owner
WORDMARK = 2.5  # a logo wider than this (w:h) gets a monogram token and a wide cell

# Commons' non-copyright restriction classes (extmetadata "Restrictions", without the "restriction-" prefix) and the
# rulings on them. identity: kept for the actor's own image (a mark shown unaltered, only to identify the actor);
# portrait: kept only for a person's own portrait; reject: never used. An unknown class is rejected until ruled on.
CLASS_RULING = {
    "trademarked": "identity", "insignia": "identity", "communist": "identity", "rusmilitarism": "identity",
    "personality": "portrait",
    "nazi": "reject", "ihl": "reject", "currency": "reject", "design": "reject", "costume": "reject",
    "fan-art": "reject", "2257": "reject", "ita-mibac": "reject", "aus-reserve": "reject",
}
# "restriction-noresize" is a layout class (the disclaimer box's style) that six templates share; the template on the
# file page decides.
SHARED_CLASSES = {"noresize"}
TEMPLATE_RULING = {
    "Shahada": "identity", "Palestinian insignia": "identity", "SouthVietnam": "identity",
    "Apartheid symbol": "reject", "Anti-LGBT symbol": "reject", "Vietnamese extremist symbol": "reject",
}
NOTICE = {
    "trademarked": "The mark may be a trademark of {label}; it is shown only to identify {label}.",
    "insignia": "Official insignia, shown unaltered only to identify {label}.",
    "communist": "Contains a communist symbol, whose display some countries restrict; shown only to identify {label}.",
    "rusmilitarism": "Commons marks this symbol as one whose use some countries restrict; shown only to identify "
                     "{label}.",
    "Shahada": "Contains the shahada, the Islamic declaration of faith; shown unaltered only to identify {label}.",
    "Palestinian insignia": "Palestinian insignia, shown unaltered only to identify {label}.",
    "SouthVietnam": "A symbol of South Vietnam, restricted in Vietnam; shown only to identify {label}.",
}
LICENCE_RE = re.compile(r"^cc-by(-sa)?-(\d(?:\.\d)?)(?:-([a-z]{2,4}))?$")  # cc-by-sa-4.0, cc-by-2.5-se, cc-by-3.0-igo
LICENCE_CATEGORY_RE = re.compile(r"^CC-(BY(?:-SA)?)-(\d\.\d)(?:-([A-Za-z]{2,4}))?$")
PEOPLE_RE = re.compile(
    r"\b(people|persons?|workers?|employees?|staff|crowds?|members?|protest\w*|rall(y|ies)|marche(s|rs)|strik(e|es|ers|ing)"
    r"|demonstrat\w*|celebrat\w*|audiences?|attendees?|students?|children|kids|famil(y|ies)|delegates?|supporters?"
    r"|fans|volunteers?|customers?|shoppers?|passengers?|visitors?|tourists?|picket\w*|group photo|selfie|portrait"
    r"|voters?|voting|vote|men|women|man|woman|boys?|girls?)\b", re.I)
EXTMETADATA = ("ImageDescription|ObjectName|Artist|Credit|Attribution|AttributionRequired|LicenseShortName|License|"
               "LicenseUrl|UsageTerms|Copyrighted|Restrictions|NonFree|DeletionReason|DateTimeOriginal|Categories")


class OfflineMiss(RuntimeError):
    pass


# ------------------------------------------------------------------------------------------------------------- http

def _retry_after(headers, default):
    value = (headers or {}).get("Retry-After")
    if not value:
        return default
    try:
        return max(1.0, float(value))
    except ValueError:
        try:
            when = parsedate_to_datetime(value)
            return max(1.0, (when - datetime.datetime.now(datetime.timezone.utc)).total_seconds())
        except (TypeError, ValueError):
            return default


class Http:
    """Cached, polite HTTP: one request at a time, a gap between requests, retries that honour Retry-After."""

    def __init__(self, cache, offline=False, refresh=False, maxlag=5, patience=600.0, gap=0.35):
        self.cache, self.offline, self.refresh, self.maxlag = cache, offline, refresh, maxlag
        self.patience, self.gap = patience, gap
        self.stats = {"network": 0, "cached": 0, "waited_s": 0.0}
        self._last = 0.0
        self.manifest_path = os.path.join(cache, "manifest.json")
        try:
            with open(self.manifest_path, encoding="utf-8") as f:
                self.downloads = json.load(f)
        except (OSError, ValueError):
            self.downloads = {}

    def _path(self, ns, key, ext):
        return os.path.join(self.cache, ns, hashlib.sha256(key.encode("utf-8")).hexdigest() + ext)

    def _record(self, path, url, body):
        rel = os.path.relpath(path, self.cache)
        self.downloads[rel] = {"url": url, "retrieved": datetime.date.today().isoformat(),
                               "sha256": hashlib.sha256(body).hexdigest(), "bytes": len(body)}
        os.makedirs(self.cache, exist_ok=True)
        tmp = self.manifest_path + ".part"
        with open(tmp, "w", encoding="utf-8") as f:
            json.dump(self.downloads, f, indent=1, sort_keys=True)
            f.write("\n")
        os.replace(tmp, self.manifest_path)

    def retrieved(self, path):
        e = self.downloads.get(os.path.relpath(path, self.cache))
        if e:
            return e["retrieved"]
        return datetime.date.fromtimestamp(os.path.getmtime(path)).isoformat()

    @staticmethod
    def _write(path, body):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        tmp = path + ".part"
        with open(tmp, "wb") as f:
            f.write(body)
        os.replace(tmp, path)

    def _download(self, url, api, check=None, accept=None):
        headers = {"User-Agent": AGENT}
        if api:
            headers["Accept-Encoding"] = "gzip"  # the robot policy asks for it, except on media files
        if accept:
            headers["Accept"] = accept
        start, attempt = time.monotonic(), 0
        while True:
            wait = time.monotonic() - self._last
            if wait < self.gap:
                time.sleep(self.gap - wait)
            why = None
            try:
                req = urllib.request.Request(url, headers=headers)
                with urllib.request.urlopen(req, timeout=90) as r:
                    body = r.read()
                    if r.headers.get("Content-Encoding") == "gzip":
                        body = gzip.decompress(body)
                    response_headers = r.headers
                self._last = time.monotonic()
                self.stats["network"] += 1
                pause = check(body, response_headers, attempt) if check else None
                if pause is None:
                    return body
                why = "maxlag: " + pause[1]
                pause = pause[0]
            except urllib.error.HTTPError as e:
                self._last = time.monotonic()
                self.stats["network"] += 1
                if e.code not in (429, 500, 502, 503, 504):
                    raise
                pause, why = _retry_after(e.headers, 5.0 * 2 ** min(attempt, 4)), "HTTP %d" % e.code
            except (urllib.error.URLError, TimeoutError, ConnectionError, httpclient.HTTPException) as e:
                self._last = time.monotonic()
                pause, why = 5.0 * 2 ** min(attempt, 4), "%s: %s" % (type(e).__name__, e)
            attempt += 1
            if time.monotonic() - start + pause > self.patience:
                raise RuntimeError("gave up after %d attempts and %.0f s (%s): %s"
                                   % (attempt, time.monotonic() - start, why, url[:160]))
            print("  waiting %.0f s (%s)" % (pause, why), file=sys.stderr, flush=True)
            self.stats["waited_s"] += pause
            time.sleep(pause)

    def _cached_json(self, path):
        if os.path.exists(path) and not self.refresh:
            self.stats["cached"] += 1
            with open(path, encoding="utf-8") as f:
                return json.load(f)
        return None

    def api(self, base, params, ns):
        """A MediaWiki Action API GET (JSON), cached by its URL; maxlag is sent but is not part of the key."""
        params = dict(params, format="json")
        url = base + "?" + urllib.parse.urlencode(sorted(params.items()))
        path = self._path(ns, url, ".json")
        hit = self._cached_json(path)
        if hit is not None:
            return hit
        if self.offline:
            raise OfflineMiss("not cached: " + url[:160])
        send = url + ("&maxlag=%d" % self.maxlag if self.maxlag else "")

        def check(body, headers, attempt):
            data = json.loads(body)
            err = data.get("error") if isinstance(data, dict) else None
            if not err:
                return None
            if err.get("code") == "maxlag":
                # Manual:Maxlag_parameter: wait at least 5 s; on Wikidata the lag includes the query service's
                # update lag, which can hold for hours, hence the growing pause and the overall patience.
                lag = float(err.get("lag") or 0)
                kind = err.get("type", "db")
                why = "%s, %.0f s lagged (%s)" % (err.get("host", "?"), lag, kind)
                if kind == "wikibase-queryservice":
                    why += ("; Wikidata counts the query service's update lag (%.0f s behind) in maxlag, which can "
                            "hold for hours; for a run someone is waiting on, --maxlag 0 omits it (Manual:Maxlag "
                            "parameter)" % float(err.get("queryserviceLag") or lag * 60))
                return max(5.0, _retry_after(headers, 5.0)) * min(2 ** attempt, 12), why
            raise RuntimeError("API error %s: %s" % (err.get("code"), str(err.get("info", ""))[:200]))

        body = self._download(send, True, check)
        self._write(path, body)
        self._record(path, url, body)
        return json.loads(body)

    def sparql(self, query):
        """A Wikidata Query Service query (JSON results), cached by its URL."""
        url = WDQS + "?" + urllib.parse.urlencode({"format": "json", "query": query})
        path = self._path("wdqs", url, ".json")
        hit = self._cached_json(path)
        if hit is not None:
            return hit
        if self.offline:
            raise OfflineMiss("not cached: " + url[:160])
        body = self._download(url, True, accept="application/sparql-results+json")
        self._write(path, body)
        self._record(path, url, body)
        return json.loads(body)

    def media(self, url, key, ext):
        """A media file (thumbnail, SVG, data file), cached by key (for Commons the URL plus the file's sha1, so a
        re-upload is fetched again)."""
        path = self._path("media", key, ext)
        if os.path.exists(path):
            self.stats["cached"] += 1
            return path
        if self.offline:
            raise OfflineMiss("not cached: " + url[:160])
        body = self._download(url, False)
        self._write(path, body)
        self._record(path, url, body)
        return path


# --------------------------------------------------------------------------------------------------------- wikidata

def wd_entities(http, qids, props="labels|descriptions|claims|sitelinks"):
    """QID -> entity. Labels ask for en and mul with fallback: since 2024 many items keep their name only in "mul"."""
    out, qids = {}, sorted(set(q for q in qids if q))
    for i in range(0, len(qids), 50):
        data = http.api(WIKIDATA_API, {"action": "wbgetentities", "ids": "|".join(qids[i:i + 50]), "props": props,
                                       "languages": "en|mul", "languagefallback": "1", "sitefilter": "enwiki"},
                        "wikidata")
        out.update(data.get("entities", {}))
    return out


def wd_search(http, text, limit=7):
    data = http.api(WIKIDATA_API, {"action": "wbsearchentities", "search": text, "language": "en", "uselang": "en",
                                   "type": "item", "limit": str(limit)}, "wikidata")
    return [r["id"] for r in data.get("search", [])]


def wd_statement(http, prop, value):
    """Items with a main statement prop=value (CirrusSearch haswbstatement), for exact identifiers such as P298."""
    data = http.api(WIKIDATA_API, {"action": "query", "list": "search", "srnamespace": "0", "srlimit": "10",
                                   "srsearch": "haswbstatement:%s=%s" % (prop, value)}, "wikidata")
    return [r["title"] for r in data.get("query", {}).get("search", [])]


def wd_tickers(http, tickers):
    """Ticker -> [QIDs] listed with it on an exchange (P414 with qualifier P249), through the query service."""
    out = {t: [] for t in tickers}
    for i in range(0, len(tickers), 40):
        values = " ".join(json.dumps(t) for t in tickers[i:i + 40])
        q = "SELECT ?t ?item WHERE { VALUES ?t { %s } ?item p:P414 ?s . ?s pq:P249 ?t . }" % values
        for b in http.sparql(q).get("results", {}).get("bindings", []):
            qid = b["item"]["value"].rsplit("/", 1)[-1]
            if qid not in out[b["t"]["value"]]:
                out[b["t"]["value"]].append(qid)
    return out


def label(e, default=None):
    labels = (e or {}).get("labels", {})
    for lang in ("en", "mul"):
        if lang in labels:
            return labels[lang]["value"]
    return default or (e or {}).get("id")


def description(e):
    return ((e or {}).get("descriptions", {}).get("en") or {}).get("value", "")


def values(e, prop):
    return [c["mainsnak"]["datavalue"]["value"] for c in (e or {}).get("claims", {}).get(prop, [])
            if c.get("mainsnak", {}).get("snaktype") == "value" and c.get("rank") != "deprecated"]


def instance_of(e):
    return [v.get("id") for v in values(e, "P31") if isinstance(v, dict)]


def _wd_date(snak, end):
    """A Wikidata time snak as a date: the first day of its precision period for a start, the last for an end."""
    if snak.get("snaktype") == "novalue":
        return None, "none"
    if snak.get("snaktype") != "value":
        return None, "unknown"
    v = snak["datavalue"]["value"]
    m = re.match(r"^([+-])(\d+)-(\d\d)-(\d\d)", v["time"])
    if not m or m.group(1) == "-":
        return (datetime.date.min if not end else datetime.date.min), "value"
    y, mo, d = int(m.group(2)), int(m.group(3)), int(m.group(4))
    y = min(max(y, 1), 9999)
    p = v.get("precision", 11)
    if p <= 9 or mo == 0:
        return (datetime.date(y, 12, 31) if end else datetime.date(y, 1, 1)), "value"
    if p == 10 or d == 0:
        last = (datetime.date(y + (mo == 12), mo % 12 + 1, 1) - datetime.timedelta(days=1)) if y < 9999 else \
            datetime.date(y, mo, 28)
        return (last if end else datetime.date(y, mo, 1)), "value"
    return datetime.date(y, mo, d), "value"


def claim_period(claim):
    """(start, end) from qualifiers P580 and P582 (None when open); an end of unknown value counts as ended at the
    start."""
    q = claim.get("qualifiers", {})
    start = end = None
    for s in q.get("P580", []):
        start, _ = _wd_date(s, False)
        break
    for s in q.get("P582", []):
        end, how = _wd_date(s, True)
        if how == "unknown":
            end = start or datetime.date.min
        break
    return start, end


def parse_at(at):
    """A year, YYYY-MM or YYYY-MM-DD -> (first day, last day)."""
    s = str(at)
    m = re.fullmatch(r"(\d{4})(?:-(\d\d)(?:-(\d\d))?)?", s)
    if not m:
        raise ValueError("at must be a year, YYYY-MM or YYYY-MM-DD: %r" % at)
    y = int(m.group(1))
    if m.group(3):
        d = datetime.date(y, int(m.group(2)), int(m.group(3)))
        return d, d
    if m.group(2):
        mo = int(m.group(2))
        return datetime.date(y, mo, 1), datetime.date(y + (mo == 12), mo % 12 + 1, 1) - datetime.timedelta(days=1)
    return datetime.date(y, 1, 1), datetime.date(y, 12, 31)


def default_at(e):
    """Today, or the day before the item's dissolution (P576) when it no longer exists."""
    for c in (e or {}).get("claims", {}).get("P576", []):
        d, how = _wd_date(c.get("mainsnak", {}), False)
        if how == "value" and d and d > datetime.date.min:
            d = d - datetime.timedelta(days=1)
            return (d, d), "the day before its dissolution (P576)"
    t = datetime.date.today()
    return (t, t), "today"


def candidates(e, order, window, current):
    """The item's image claims in the order to try. Claims whose period (P580-P582) overlaps the window come first;
    then property order; within a property the larger overlap, the nearer in time, preferred rank (when the window is
    today), the claim in force longer (a later end), dark-on-light before light-on-dark (P8798), and Wikidata's own
    order. Deprecated claims are dropped. Claims without dates count as in force at any time."""
    lo, hi = window
    span = (hi - lo).days + 1
    rows = []
    for pi, prop in enumerate(order):
        for i, c in enumerate((e or {}).get("claims", {}).get(prop, [])):
            ms = c.get("mainsnak", {})
            if c.get("rank") == "deprecated" or ms.get("snaktype") != "value":
                continue
            start, end = claim_period(c)
            a, b = max(lo, start or datetime.date.min), min(hi, end or datetime.date.max)
            overlap = max(0, (b - a).days + 1) / span
            distance = (lo - end).days if end and end < lo else (start - hi).days if start and start > hi else 0
            dark = any(s.get("datavalue", {}).get("value", {}).get("id") == LIGHT_ON_DARK
                       for s in c.get("qualifiers", {}).get("P8798", []))
            key = (0 if overlap else 1, pi, -round(overlap, 4), distance,
                   (0 if c.get("rank") == "preferred" else 1) if current else 0,
                   -(end or datetime.date.max).toordinal(), int(dark), i)
            rows.append((key, {"property": prop, "file": ms["datavalue"]["value"], "rank": c.get("rank"),
                               "start": start.isoformat() if start else None, "end": end.isoformat() if end else None,
                               "in_force": bool(overlap), "light_on_dark": dark}))
    out, seen = [], set()
    for _, r in sorted(rows, key=lambda r: r[0]):
        if r["file"] not in seen:  # one file can stand in several claims (Russia's flag 1896-1918 and since 1993)
            seen.add(r["file"])
            out.append(r)
    return out


def public_adult(e):
    """(ok, reason): a human with an English Wikipedia article who is 18 or older."""
    if HUMAN not in instance_of(e):
        return False, "not a human (P31 lacks Q5)"
    if "enwiki" not in e.get("sitelinks", {}):
        return False, "no English Wikipedia article (not treated as a public figure)"
    born = None
    for c in e.get("claims", {}).get("P569", []):
        d, how = _wd_date(c.get("mainsnak", {}), True)  # the latest possible birth day: the strictest test
        if how == "value":
            born = d
            break
    if born is None:
        return False, "no birth date (adulthood cannot be checked)"
    today = datetime.date.today()
    if (today.year - born.year) - ((today.month, today.day) < (born.month, born.day)) < 18:
        return False, "under 18"
    return True, "human, enwiki article, adult"


def ticker_of(e):
    """The item's ticker (P249 on its current P414 listing, preferred first), for monograms."""
    best = None
    for c in (e or {}).get("claims", {}).get("P414", []):
        if c.get("rank") == "deprecated" or "P582" in c.get("qualifiers", {}):
            continue
        for s in c.get("qualifiers", {}).get("P249", []):
            t = s.get("datavalue", {}).get("value")
            if isinstance(t, str) and re.fullmatch(r"[A-Z][A-Z.]{0,5}", t):
                rank = 0 if c.get("rank") == "preferred" else 1
                if best is None or rank < best[0]:
                    best = (rank, t)
    return best[1] if best else None


def monogram(it, e, fallback_label):
    """The item's own monogram, else its ticker, else an English acronym among its short names (P1813: USSR, OPEC),
    else a state's ISO 3166-1 alpha-3 code (P298), else another English short name of 2-6 letters, else initials."""
    if it.get("monogram"):
        return it["monogram"]
    if it.get("ticker"):
        return it["ticker"].upper()
    t = ticker_of(e)
    if t:
        return t
    short = []
    for c in (e or {}).get("claims", {}).get("P1813", []):
        v = c.get("mainsnak", {}).get("datavalue", {}).get("value", {})
        text = v.get("text", "") if isinstance(v, dict) else ""
        if v and v.get("language") in ("en", "mul") and re.fullmatch(r"[A-Za-z][A-Za-z&.+-]{1,5}", text) \
                and text.lower() not in ("n/a", "na", "none"):
            short.append(text)
    acronyms = [s for s in short if re.fullmatch(r"[A-Z][A-Z&]{1,5}", s)]
    if acronyms:
        return acronyms[0]
    if it.get("iso3"):
        return it["iso3"].upper()
    for v in values(e, "P298"):
        if isinstance(v, str) and re.fullmatch(r"[A-Z]{3}", v):
            return v
    if short:
        return short[0]
    words = [w for w in re.split(r"[\s\-]+", fallback_label or "")
             if w and w.lower() not in ("of", "the", "and", "for", "&", "de", "la", "le")]
    return "".join(w[0].upper() for w in words[:4]) or "?"


# ---------------------------------------------------------------------------------------------------------- commons

def strip_html(s):
    s = re.sub(r"<[^>]+>", " ", s or "")
    return re.sub(r"\s+", " ", html.unescape(s)).strip()


def clean_author(s):
    """Plain author text from Commons' Artist HTML: no space before punctuation, a doubled phrase once (the HTML often
    repeats a name in a hidden span), and None for an unknown author."""
    s = re.sub(r"\s+([,.;:)])", r"\1", strip_html(s))
    s = re.sub(r"\(\s+", "(", s).strip()
    half = len(s) // 2
    if len(s) % 2 == 1 and s[:half] == s[half + 1:] and s[half] == " ":
        s = s[:half]
    if re.fullmatch(r"(unknown|anonymous)( author)?\.?", s, re.I):
        return None
    return s or None


def first_link(s):
    m = re.search(r'href="([^"]+)"', s or "")
    if not m:
        return None
    url = html.unescape(m.group(1))
    return "https:" + url if url.startswith("//") else url


def commons_info(http, files, width=None):
    """File name -> imageinfo (with extmetadata, or only the thumbnail URL when width is given) plus pageid."""
    out, files = {}, sorted(set(files))
    for i in range(0, len(files), 50):
        batch = files[i:i + 50]
        params = {"action": "query", "formatversion": "2", "prop": "imageinfo", "redirects": "1",
                  "titles": "|".join("File:" + f for f in batch)}
        if width:
            params.update({"iiprop": "url", "iiurlwidth": str(width)})
        else:
            params.update({"iiprop": "url|size|mime|sha1|timestamp|extmetadata", "iiextmetadatalanguage": "en",
                           "iiextmetadatafilter": EXTMETADATA})
        data = http.api(COMMONS_API, params, "commons")
        q = data.get("query", {})
        alias = {}
        for kind in ("normalized", "redirects"):
            for n in q.get(kind, []):
                alias[n["to"]] = alias.get(n["from"], n["from"])
        for page in q.get("pages", []):
            title = alias.get(page["title"], page["title"])
            name = title[5:] if title.startswith("File:") else title
            if page.get("missing") or not page.get("imageinfo"):
                out[name] = {"missing": True}
            else:
                out[name] = dict(page["imageinfo"][0], pageid=page.get("pageid"), title=page["title"])
    for f in files:  # titles whose normalized form differs only by underscores
        if f not in out:
            out[f] = out.get(f.replace("_", " "), {"missing": True})
    return out


def commons_templates(http, files, templates):
    """File name -> the given templates that the file page uses (to resolve a class several templates share)."""
    out, files = {f: [] for f in files}, sorted(set(files))
    names = "|".join("Template:" + t for t in sorted(templates))
    for i in range(0, len(files), 50):
        batch = files[i:i + 50]
        data = http.api(COMMONS_API, {"action": "query", "formatversion": "2", "prop": "templates", "tllimit": "max",
                                      "tlnamespace": "10", "tltemplates": names,
                                      "titles": "|".join("File:" + f for f in batch)}, "commons")
        q = data.get("query", {})
        alias = {n["to"]: n["from"] for n in q.get("normalized", [])}
        for page in q.get("pages", []):
            name = alias.get(page["title"], page["title"])[5:]
            out[name] = sorted(t["title"][9:] for t in page.get("templates", []))
    return out


def commons_depicts(http, pageids):
    """pageid -> [(QID, region)] from structured data on Commons (MediaInfo M<pageid>, P180 depicts, with the
    qualifier P2677 relative position within image where given)."""
    out, mids = {}, sorted({"M%d" % p for p in pageids if p})
    for i in range(0, len(mids), 50):
        data = http.api(COMMONS_API, {"action": "wbgetentities", "ids": "|".join(mids[i:i + 50]), "props": "claims"},
                        "commons")
        for mid, e in data.get("entities", {}).items():
            st = e.get("statements") or e.get("claims") or {}
            if isinstance(st, list):  # an entity without statements serializes them as []
                st = {}
            rows = []
            for c in st.get("P180", []):
                if c.get("mainsnak", {}).get("snaktype") != "value":
                    continue
                region = None
                for s in c.get("qualifiers", {}).get("P2677", []):
                    region = s.get("datavalue", {}).get("value")
                    break
                rows.append((c["mainsnak"]["datavalue"]["value"]["id"], region))
            out[int(mid[1:])] = rows
    return out


def meta(info, key):
    return (info.get("extmetadata", {}).get(key) or {}).get("value", "")


def licence_check(info):
    """(ok, reason, licence dict) under the whitelist: public domain, CC0, CC BY, CC BY-SA, any version."""
    if info.get("missing"):
        return False, "file not found on Commons", None
    if str(meta(info, "NonFree")).lower() == "true":
        return False, "non-free file", None
    if meta(info, "DeletionReason"):
        return False, "nominated for deletion: " + strip_html(meta(info, "DeletionReason"))[:120], None
    code = strip_html(meta(info, "License")).lower()
    short = strip_html(meta(info, "LicenseShortName"))
    url = strip_html(meta(info, "LicenseUrl")) or None
    lic = {"licence": short or code, "licence_code": code or None, "licence_url": url,
           "attribution_required": str(meta(info, "AttributionRequired")).lower() != "false",
           "share_alike": "-sa" in code or "BY-SA" in short.upper()}
    if code in ("pd", "pdm") or short.lower().startswith(("public domain", "pdm")):
        lic.update(licence="Public domain", licence_url=url if code == "pdm" else None,
                   attribution_required=False, share_alike=False)
        return True, "public domain", lic
    if code == "cc0" or short.upper().startswith("CC0"):
        lic.update(licence="CC0 1.0", licence_url=url or "https://creativecommons.org/publicdomain/zero/1.0/",
                   attribution_required=False, share_alike=False)
        return True, "CC0", lic
    m = LICENCE_RE.match(code)
    if not m:
        # Multi-licensed files: the extmetadata licence is the first one only; a CC licence category is another offer.
        cats = [c.strip() for c in strip_html(meta(info, "Categories")).split("|")]
        for c in cats:
            mc = LICENCE_CATEGORY_RE.match(c)
            if mc:
                code = "cc-%s-%s%s" % (mc.group(1).lower(), mc.group(2), "-" + mc.group(3).lower() if mc.group(3) else "")
                m = LICENCE_RE.match(code)
                short = "CC %s %s%s" % (mc.group(1), mc.group(2), " " + mc.group(3) if mc.group(3) else "")
                lic.update(licence=short, licence_code=code, licence_url=None, share_alike="SA" in mc.group(1),
                           multi_licensed=True)
                url = None
                break
    if m:
        if not url:
            url = "https://creativecommons.org/licenses/%s/%s/%s" % (
                "by-sa" if m.group(1) else "by", m.group(2), (m.group(3) + "/") if m.group(3) else "")
            lic["licence_url_derived"] = True
        lic["licence_url"] = url
        return True, lic["licence"], lic
    return False, "licence not on the whitelist: %s" % (short or code or "none given"), lic


def restriction_check(info, templates, kind, prop):
    """(ok, reason, classes, rulings). rulings: [(class or template, ruling)]."""
    classes = [r for r in strip_html(meta(info, "Restrictions")).split("|") if r]
    rulings = []
    for r in classes:
        if r in SHARED_CLASSES:
            found = [t for t in templates if t in TEMPLATE_RULING]
            if not found:
                return False, "restriction class '%s' with no known template on the file page" % r, classes, rulings
            for t in found:
                rulings.append((t, TEMPLATE_RULING[t]))
            continue
        rulings.append((r, CLASS_RULING.get(r, "unknown")))
    for what, ruling in rulings:
        if ruling == "identity":
            continue  # the actor's own mark, flag or photo, shown unaltered (marks) and only to identify it
        if ruling == "portrait" and kind == "person" and prop == "P18":
            continue  # a public figure's own portrait, used to inform, never to endorse
        if ruling == "unknown":
            return False, "unknown restriction class '%s' (rule on it in images.md first)" % what, classes, rulings
        return False, "restriction '%s' (%s) is not allowed for %s's %s" % (
            what, ruling, KIND_NOUN.get(kind, kind), PROPS.get(prop, prop)), classes, rulings
    return True, "", classes, rulings


def people_signals(info, depicted_humans):
    """Words in the description, title or categories, and human subjects in 'depicts', that suggest people in a photo."""
    text = " ".join([strip_html(meta(info, "ImageDescription")), strip_html(meta(info, "ObjectName")),
                     strip_html(meta(info, "Categories")).replace("|", " ; ")])
    words = sorted({m.group(0).lower() for m in PEOPLE_RE.finditer(text)})
    return words + ["depicts " + h for h in depicted_humans]


def parse_region(region, w, h):
    """P2677 'pct:x,y,w,h' or 'x,y,w,h' (pixels) -> [x, y, w, h] fractions."""
    if not isinstance(region, str):
        return None
    m = re.fullmatch(r"(pct:)?\s*([\d.]+),([\d.]+),([\d.]+),([\d.]+)", region.strip())
    if not m:
        return None
    x, y, bw, bh = (float(m.group(i)) for i in range(2, 6))
    if m.group(1):
        return [x / 100, y / 100, bw / 100, bh / 100]
    return [x / w, y / h, bw / w, bh / h] if w and h else None


def square_box(box, w, h, margin=0.0):
    """A box (fractions) grown by margin and made square in pixels, kept inside the image; fractions out."""
    x, y, bw, bh = box
    cx, cy = (x + bw / 2) * w, (y + bh / 2) * h
    side = min(max(bw * w, bh * h) * (1 + margin), w, h)
    x0 = min(max(cx - side / 2, 0), w - side)
    y0 = min(max(cy - side / 2, 0), h - side)
    return [x0 / w, y0 / h, side / w, side / h]


def standard_width(w, h, size, crop, box=None):
    """The smallest standard width whose thumbnail gives the crop at least size px."""
    w, h = max(1, w or size), max(1, h or size)
    if box:
        need = size / max(box[2], 1e-6)
    elif crop == "contain":
        need = size if w >= h else size * w / h
    else:
        need = size * w / min(w, h)
    for s in STANDARD_WIDTHS:
        if s >= need:
            return s
    return STANDARD_WIDTHS[-1]


# ------------------------------------------------------------------------------------------------------------ jev

def jev_ask(http, state, questions):
    """Jev answers, cached by the request's sha256. Tools/jev.py finds the key itself and never prints it."""
    key = hashlib.sha256(json.dumps({"state": state, "questions": questions}, sort_keys=True).encode("utf-8"))
    path = os.path.join(http.cache, "jev", key.hexdigest() + ".json")
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            return json.load(f)
    if http.offline:
        raise OfflineMiss("Jev answers not cached")
    sys.path.insert(0, TOOLS)
    import jev
    if not jev.available():
        raise RuntimeError("--jev: no Jev credential available (live Jev calls are unavailable)")
    answers = jev.call_jev(state, questions)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(answers, f, indent=1, sort_keys=True)
    return answers


def jev_identity(http, it, cands):
    """(qid or None, choice probability, choice, companion noul): a Choice among the candidates and, as TypeSafe's
    companion-Noul pattern advises instead of a "none" option, a Noul on whether any candidate is the actor."""
    state = {"actor": {"name": it.get("search") or it.get("label"), "about": it.get("hint", ""),
                       "kind": KIND_NOUN.get(it["kind"], it["kind"])},
             "candidates": cands}
    crit = {"c%d" % i: "%s %s: %s" % (c["qid"], c["label"], c["description"] or "no description")
            for i, c in enumerate(cands)}
    a = jev_ask(http, state, {
        "identity": {"type": "choice", "criteria": crit,
                     "instructions": "Which Wikidata entity in `candidates` is the actor `actor`? Match on the name, "
                                     "the description and the kind of actor."},
        "any": {"type": "noul",
                "instructions": "Is one of the Wikidata entities in `candidates` the actor `actor` itself?",
                "criteria": {"true": "One candidate is that very actor, not an event, a branch, a local, a document "
                                     "or another entity that only shares its name.",
                             "false": "No candidate is that actor."}}})
    choice = a["identity"].get("choice")
    p = float(a["identity"].get("probabilities", {}).get(choice, 0))
    anyp = float(a["any"]["noul"])
    if choice in crit and p >= IDENTITY_MIN and anyp >= IDENTITY_ANY:
        return cands[int(choice[1:])]["qid"], p, choice, anyp
    return None, p, choice, anyp


def jev_own_image(http, pins):
    """Per pinned file, (refers, other): does the file refer to the actor, and does anything tie it to a different
    owner? Two snap judgments combined in code (a file found by name can be a namesake's: the Commons file
    "UAW logo.jpg" is the Union of Australian Women's, and its title alone matches the UAW)."""
    if not pins:
        return {}
    questions = {}
    for i, _ in enumerate(pins):
        questions["r%d" % i] = {
            "type": "noul",
            "instructions": "Do the title, description or categories in `pins[%d]` refer to `pins[%d].actor`, by its "
                            "name or its initials?" % (i, i)}
        questions["o%d" % i] = {
            "type": "noul",
            "instructions": "Do the author, credit, description or categories in `pins[%d]` tie the image to an "
                            "organization, person or place other than `pins[%d].actor` (`pins[%d].actor_description`)?"
                            % (i, i, i),
            "criteria": {
                "true": "They name another organization or place as the image's owner, source or subject, for example "
                        "a namesake with the same initials in another country.",
                "false": "They name only this actor, a government or archive that published its image, or the person "
                         "who drew or photographed it."}}
    answers = jev_ask(http, {"pins": pins}, questions)
    return {i: (float(answers["r%d" % i]["noul"]), float(answers["o%d" % i]["noul"])) for i in range(len(pins))}


def jev_private_people(http, photos):
    """Noul per photo: would it show identifiable private individuals?"""
    if not photos:
        return {}
    questions = {}
    for i, _ in enumerate(photos):
        questions["p%d" % i] = {
            "type": "noul",
            "instructions": "Would the photo described in `photos[%d]` show identifiable private individuals if it "
                            "were used as the picture of `photos[%d].organization`?" % (i, i),
            "criteria": {
                "true": "The description, title, categories or depicted subjects say or imply that people who are not "
                        "public figures are shown recognizably: workers, members, staff, customers, protesters, "
                        "voters, attendees, a crowd or a group photo.",
                "false": "The photo shows a building, a sign, a landscape, a map, a product, an emblem or an object, "
                         "with no people or only incidental distant figures, or it shows only public figures."}}
    answers = jev_ask(http, {"photos": photos}, questions)
    return {i: float(answers["p%d" % i]["noul"]) for i in range(len(photos))}


# ---------------------------------------------------------------------------------------------------------- resolve

def kind_ok(kind, e):
    """(ok, reason) that an entity can stand for an actor of this kind."""
    if not e or "missing" in e:
        return False, "no such item"
    kinds = instance_of(e)
    bad = [NOT_ACTORS[k] for k in kinds if k in NOT_ACTORS]
    if bad:
        return False, bad[0]
    if kind == "person":
        return (HUMAN in kinds), "not a human"
    return (HUMAN not in kinds), "a human, not %s" % KIND_NOUN.get(kind, kind)


def resolve(http, items, use_jev):
    """id -> {"qid", "how": [...], "problems": [...]}, from the item's qid, iso3, ticker or search."""
    tickers = sorted({it["ticker"].upper() for it in items if it.get("ticker")})
    by_ticker = wd_tickers(http, tickers) if tickers else {}
    keys, pool = {}, set()
    for it in items:
        if it["kind"] == "figure":
            continue
        ks = []
        if it.get("iso3"):
            ks.append(("iso3", it["iso3"].upper(), wd_statement(http, "P298", it["iso3"].upper())))
        if it.get("ticker"):
            ks.append(("ticker", it["ticker"].upper(), by_ticker.get(it["ticker"].upper(), [])))
        if it.get("search"):
            ks.append(("search", it["search"], wd_search(http, it["search"])))
        keys[it["id"]] = ks
        pool.update(q for _, _, qs in ks for q in qs)
        if it.get("qid"):
            pool.add(it["qid"])
    ents = wd_entities(http, pool)
    out = {}
    for it in items:
        if it["kind"] == "figure":
            continue
        how, problems, chosen = [], [], it.get("qid")
        if chosen:
            how.append({"method": "qid", "value": chosen})
        for method, value, qs in keys[it["id"]]:
            fit, dropped = [], {}
            for q in qs:
                ok, why = kind_ok(it["kind"], ents.get(q))
                if ok:
                    fit.append(q)
                else:
                    dropped[q] = why
            step = {"method": method, "value": value, "candidates": qs[:7], "kept": fit[:7]}
            if dropped:
                step["dropped"] = dropped
            if chosen:
                if chosen in qs:
                    step["agrees"] = True
                else:
                    step["agrees"] = False
                    problems.append("%s %s gives %s, not %s" % (method, value, ", ".join(qs[:5]) or "nothing", chosen))
                how.append(step)
                continue
            if method in ("iso3", "ticker") and len(fit) == 1:
                chosen, step["result"] = fit[0], "the only match"
            elif fit and use_jev:
                cands = [{"qid": q, "label": label(ents.get(q), q), "description": description(ents.get(q))}
                         for q in fit[:6]]
                q, p, choice, anyp = jev_identity(http, it, cands)
                step.update(jev_choice=choice, jev_probability=round(p, 3), jev_any=round(anyp, 3))
                if q:
                    chosen, step["result"] = q, "chosen by Jev"
                else:
                    problems.append("%s %r: Jev chose %s at %.2f, any candidate at %.2f (needs %.2f and %.2f)"
                                    % (method, value, choice, p, anyp, IDENTITY_MIN, IDENTITY_ANY))
            elif fit:
                # Without Jev a name resolves only when one candidate of the right kind is left and its label is the
                # name exactly: a unique exact label among several can still be the wrong entity (Apple, the fruit).
                want = (it.get("label") or value).casefold()
                if len(fit) == 1 and label(ents.get(fit[0]), "").casefold() == want:
                    chosen, step["result"] = fit[0], "the only candidate, with the exact label"
                else:
                    problems.append("%s %r: %d candidates of the right kind; a name is a judgment, so give a qid or "
                                    "use --jev" % (method, value, len(fit)))
            else:
                problems.append("%s %r: no candidate of the right kind" % (method, value))
            how.append(step)
        out[it["id"]] = {"qid": chosen, "how": how, "problems": problems}
    return out


# ----------------------------------------------------------------------------------------------------------- images

def probe(path):
    r = subprocess.run(["ffprobe", "-v", "error", "-select_streams", "v:0", "-show_entries",
                        "stream=width,height,pix_fmt", "-of", "json", path], capture_output=True, text=True, check=True)
    s = json.loads(r.stdout)["streams"][0]
    return int(s["width"]), int(s["height"]), s.get("pix_fmt", "")


def ffmpeg(args):
    subprocess.run(["ffmpeg", "-nostdin", "-hide_banner", "-loglevel", "error", "-y"] + args, check=True)


ENCODE_PNG = ["-fflags", "+bitexact", "-flags:v", "+bitexact", "-pix_fmt", "rgba", "-compression_level", "9"]
ENCODE_JPG = ["-fflags", "+bitexact", "-flags:v", "+bitexact", "-pix_fmt", "yuvj444p", "-q:v", "2"]
WHITE_MASK = "geq=r=255:g=255:b=255:a='alpha(X,Y)'"


def render(src, dst_stem, size, crop, box=None, svg=False, mask=False):
    """Square size x size. center and portrait crop a square (portrait keeps the upper part, where faces are), box
    crops a reviewed square, and each is scaled; contain fits the whole image on transparency. JPEG for opaque crops,
    PNG otherwise. Returns (path, (source w, h), upscaled, content rect [x, y, w, h] in output pixels)."""
    pre = ["-width", str(size), "-height", str(size), "-keep_ar", "1"] if svg else []
    w, h, pix = (size, size, "rgba") if svg else probe(src)
    alpha = svg or any(a in pix for a in ("rgba", "argb", "bgra", "abgr", "ya", "pal8", "gbrap", "yuva"))
    if crop == "contain":
        sw, sh = (size, max(1, round(size * h / w))) if w >= h else (max(1, round(size * w / h)), size)
        x0, y0 = (size - sw) // 2, (size - sh) // 2
        vf = "format=rgba,scale=%d:%d:flags=lanczos,pad=%d:%d:%d:%d:color=black@0" % (sw, sh, size, size, x0, y0)
        content, upscaled = [x0, y0, sw, sh], max(w, h) < size
        ext, enc = ".png", ENCODE_PNG
    else:
        if box:
            bx, by, bw, _ = box
            side = max(1, round(bw * w))
            x, y = round(bx * w), round(by * h)
            side = min(side, w - x, h - y)
        else:
            side = min(w, h)
            x, y = (w - side) // 2, round((h - side) * (0.12 if crop == "portrait" else 0.5))
        vf = "crop=%d:%d:%d:%d:exact=1,scale=%d:%d:flags=lanczos" % (side, side, x, y, size, size)
        content, upscaled = [0, 0, size, size], side < size
        if alpha or mask:
            vf = "format=rgba," + vf
            ext, enc = ".png", ENCODE_PNG
        else:
            ext, enc = ".jpg", ENCODE_JPG
    if mask:
        vf += "," + WHITE_MASK
    dst = dst_stem + ext
    for stale in (".jpg", ".png"):
        if stale != ext and os.path.exists(dst_stem + stale):
            os.remove(dst_stem + stale)
    ffmpeg(pre + ["-i", src, "-vf", vf, "-frames:v", "1", "-map_metadata", "-1"] + enc + [dst])
    return dst, (w, h), upscaled, content


def sha256_file(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def attribution(title, author, lic, source_page, modified, notices=()):
    """TASL: title, author, source and licence, the changes made, and the restriction notices."""
    by = (' by ' + author) if author else ""
    t = '"%s"%s' % (title, by) if title else (author or "Unknown author")
    if lic["licence"] == "Public domain":
        text = "%s, public domain, via Wikimedia Commons (%s)." % (t, source_page)
    else:
        text = "%s, %s (%s), via Wikimedia Commons (%s)." % (t, lic["licence"], lic["licence_url"], source_page)
    text += " %s%s." % (modified[0].upper(), modified[1:])
    if lic.get("share_alike"):
        text += " This thumbnail is shared under %s." % lic["licence"]
    for n in notices:
        text += " " + n
    return text


def credit_short(noun, author, lic):
    who = author if author and len(author) <= 40 else (author[:37].rstrip() + "..." if author else "")
    if lic["licence"] == "Public domain":
        # a portrait (a photograph or a painting) names its maker even in the public domain
        return "%s: %spublic domain" % (noun, who + ", " if who and noun == "Portrait" else "")
    return "%s: %s · %s" % (noun, who, lic["licence"]) if who else "%s: %s" % (noun, lic["licence"])


# ------------------------------------------------------------------------------------------------------------ fetch

def slug_ok(s):
    return isinstance(s, str) and re.fullmatch(r"[a-z0-9][a-z0-9_-]{0,63}", s) is not None


def load_items(path):
    with open(path, encoding="utf-8") as f:
        items = json.load(f)
    seen = set()
    for it in items:
        if not slug_ok(it.get("id")) or it["id"] in seen:
            sys.exit("bad or duplicate id: %r (lower case, digits, - and _)" % it.get("id"))
        seen.add(it["id"])
        if it.get("kind") not in KINDS:
            sys.exit("%s: kind must be one of %s" % (it["id"], sorted(KINDS)))
        if it["kind"] == "figure":
            if not re.fullmatch(r"[0-9A-Fa-f]{2,6}(-[0-9A-Fa-f]{2,6})*", it.get("emoji", "")):
                sys.exit("%s: a figure needs an emoji hex code, e.g. 1F9D1-200D-1F3ED" % it["id"])
            continue
        if it["kind"] == "person" and not (it.get("qid") or it.get("search")):
            sys.exit("%s: a real person needs a Wikidata QID (or a search to resolve one)" % it["id"])
        if not any(it.get(k) for k in ("qid", "iso3", "ticker", "search", "file")):
            sys.exit("%s: needs a qid, iso3, ticker, search or a Commons file" % it["id"])
        if it.get("qid") and not re.fullmatch(r"Q\d+", it["qid"]):
            sys.exit("%s: bad qid %r" % (it["id"], it["qid"]))
        if it.get("at") is not None:
            try:
                parse_at(it["at"])
            except ValueError as e:
                sys.exit("%s: %s" % (it["id"], e))
        if it.get("box") is not None and not (isinstance(it["box"], list) and len(it["box"]) == 4
                                              and all(0 <= v <= 1 for v in it["box"])):
            sys.exit("%s: box must be [x, y, w, h] as fractions" % it["id"])
        if it.get("crop") not in (None, "center", "portrait", "contain"):
            sys.exit("%s: crop must be center, portrait or contain" % it["id"])
    return items


def cmd_fetch(args):
    if args.offline and args.refresh:
        sys.exit("--refresh downloads; it cannot be combined with --offline")
    if not 32 <= args.size <= 512:
        sys.exit("--size must be 32-512 (the app uses 128-256)")
    items = load_items(args.items)
    os.makedirs(args.out, exist_ok=True)
    http = Http(args.cache, args.offline, args.refresh, args.maxlag, args.patience)
    S = args.size
    records, failures = [], []

    # 1. Identity: the QID of each actor, and how it was found.
    res = resolve(http, items, args.jev)
    for iid, r in res.items():
        for p in r["problems"]:
            failures.append({"id": iid, "stage": "resolve", "reason": p})

    # 2. Wikidata: the items and their image claims, chosen for the date (or a pinned file).
    ents = wd_entities(http, [r["qid"] for r in res.values() if r["qid"]])
    plan = {}
    for it in items:
        if it["kind"] == "figure":
            continue
        qid = res[it["id"]]["qid"]
        e = ents.get(qid, {}) if qid else {}
        if qid and (not e or "missing" in e):
            failures.append({"id": it["id"], "stage": "resolve", "reason": "Wikidata item %s not found" % qid})
            e = {}
        if not qid and not it.get("file"):
            records.append({"id": it["id"], "kind": it["kind"], "label": it.get("label") or it.get("search"),
                            "qid": None, "resolved": res[it["id"]]["how"], "file": None, "fallback": "monogram",
                            "monogram": monogram(it, None, it.get("label") or it.get("search")),
                            "reason": "no Wikidata item resolved", "rejected": [], "candidates": 0})
            continue
        if it.get("at") is not None:
            window, why = parse_at(it["at"]), "at %s (given)" % it["at"]
        else:
            window, why = default_at(e)
        current = it.get("at") is None and why == "today"
        order = list(it.get("prefer") or []) + [p for p in ORDER[it["kind"]] if p not in (it.get("prefer") or [])]
        if it["kind"] == "person":
            order = ["P18"]  # a person's image is only their own portrait
        cands = candidates(e, order, window, current) if e else []
        if it.get("file"):
            pinned = {"property": "pinned", "file": it["file"], "rank": "pinned", "start": None, "end": None,
                      "in_force": True, "light_on_dark": False}
            cands = [pinned] + [c for c in cands if c["file"] != it["file"]]
        plan[it["id"]] = (it, e, cands, {"window": [window[0].isoformat(), window[1].isoformat()], "basis": why})

    # 3. Commons: metadata of every candidate, the templates behind shared restriction classes, depicts of photos.
    files = sorted({c["file"] for _, _, cs, _ in plan.values() for c in cs})
    info = commons_info(http, files) if files else {}
    shared = [f for f in files if set(strip_html(meta(info.get(f, {}), "Restrictions")).split("|")) & SHARED_CLASSES]
    templates = commons_templates(http, shared, TEMPLATE_RULING) if shared else {}

    def as_prop(it, c, fi):
        if c["property"] != "pinned":
            return c["property"]
        if it.get("as"):
            return it["as"]
        if it["kind"] == "person" or fi.get("mime", "").startswith(("image/jpeg", "image/tiff", "image/webp")):
            return "P18"
        return ORDER[it["kind"]][0]

    photo_pages = {info[c["file"]].get("pageid") for it, _, cs, _ in plan.values() for c in cs
                   if not info.get(c["file"], {}).get("missing") and as_prop(it, c, info[c["file"]]) == "P18"}
    depicts = commons_depicts(http, photo_pages) if photo_pages else {}
    dq = sorted({q for rows in depicts.values() for q, _ in rows})
    dents = wd_entities(http, dq, props="labels|claims") if dq else {}
    is_human = {q: HUMAN in instance_of(dents.get(q, {})) for q in dq}

    # 4. Verdicts in order; Jev (optional) on the collectives' photos that pass everything else.
    verdicts, jev_queue, pin_queue = {}, [], []
    for iid, (it, e, cands, _) in plan.items():
        for c in cands:
            name = c["file"]
            fi = info.get(name, {"missing": True})
            prop = as_prop(it, c, fi)
            ok, why, lic = licence_check(fi)
            v = dict(c, prop=prop, ok=ok, reason="" if ok else why, lic=lic, photo=prop == "P18")
            if c["property"] == "pinned" and ok and args.jev:
                pin_queue.append(((iid, name), {
                    "actor": label(e, it.get("label")) if e else it.get("label", iid),
                    "actor_description": description(e), "file": name,
                    "title": strip_html(meta(fi, "ObjectName")),
                    "description": strip_html(meta(fi, "ImageDescription"))[:600],
                    "author": clean_author(meta(fi, "Artist")) or "", "credit": strip_html(meta(fi, "Credit"))[:200],
                    "categories": strip_html(meta(fi, "Categories")).split(" | ")[:20]}))
            if ok:
                rok, rwhy, classes, rulings = restriction_check(fi, templates.get(name, []), it["kind"], prop)
                v.update(restrictions=classes, rulings=[{"restriction": a, "ruling": b} for a, b in rulings])
                if not rok:
                    v.update(ok=False, reason=rwhy)
            dep = depicts.get(fi.get("pageid"), []) if v["photo"] else []
            if it["kind"] == "person":
                v["depicts_subject"] = any(q == e.get("id") for q, _ in dep) if dep else None
                v["region"] = next((r for q, r in dep if q == e.get("id") and r), None)
                others = [label(dents.get(q, {}), q) for q, _ in dep if is_human.get(q) and q != e.get("id")]
                if others:
                    v["other_people"] = others
            elif v["photo"] and v["ok"]:
                sig = people_signals(fi, [label(dents.get(q, {}), q) for q, _ in dep if is_human.get(q)])
                v["people_signals"] = sig
                if sig:
                    v.update(ok=False, reason="a collective's photo that may show people: " + ", ".join(sig[:6]))
                v["jev_state"] = {
                    "organization": label(e, it.get("label")) if e else it.get("label", iid),
                    "organization_description": description(e),
                    "file": name, "description": strip_html(meta(fi, "ImageDescription"))[:600],
                    "categories": strip_html(meta(fi, "Categories")).split(" | ")[:20],
                    "depicts": [label(dents.get(q, {}), q) for q, _ in dep]}
            verdicts[(iid, name)] = v
    if pin_queue:  # first, so that a rejected pin lets the photos behind it reach the next check
        p = jev_own_image(http, [s for _, s in pin_queue])
        for i, (k, _) in enumerate(pin_queue):
            refers, other = p[i]
            verdicts[k]["jev_own_image"] = {"refers": round(refers, 3), "other_owner": round(other, 3)}
            if (refers < OWN_IMAGE_MIN or other >= OTHER_OWNER_MAX) and verdicts[k]["ok"]:
                verdicts[k].update(ok=False, reason="Jev: the pinned file may not be this actor's own image (refers "
                                                    "%.2f, another owner %.2f)" % (refers, other))
    if args.jev:
        # Jev sees only the photos that could be used: those before the first accepted logo, flag, arms or seal (P18
        # comes last in every order, so a collective with an accepted mark sends nothing). A photo the words already
        # rejected is still sent, so the two checks can be compared.
        for iid, (it, e, cands, _) in plan.items():
            for c in cands:
                v = verdicts[(iid, c["file"])]
                if "jev_state" in v:
                    jev_queue.append(((iid, c["file"]), v["jev_state"]))
                if v["ok"] and not v["photo"]:
                    break
    if jev_queue:
        p = jev_private_people(http, [s for _, s in jev_queue])
        for i, (k, _) in enumerate(jev_queue):
            verdicts[k]["jev_private_people"] = round(p[i], 3)
            if p[i] >= PRIVATE_PEOPLE_MAX and verdicts[k]["ok"]:
                verdicts[k].update(ok=False, reason="Jev: private individuals likely shown (noul %.2f)" % p[i])

    # 5. Choose, then fetch the thumbnails at standard widths.
    chosen = {}
    for iid, (it, e, cands, _) in plan.items():
        if it["kind"] == "person":
            ok, why = public_adult(e)
            if not ok:
                failures.append({"id": iid, "stage": "policy", "reason": "not shown as a real person: " + why})
                chosen[iid] = (None, [], why)
                continue
        tried, pick = [], None
        for c in cands:
            v = verdicts[(iid, c["file"])]
            if v["ok"]:
                pick = v
                break
            tried.append(v)
        chosen[iid] = (pick, tried, None)
    by_width = {}
    for iid, (pick, _, _) in chosen.items():
        if not pick:
            continue
        it = plan[iid][0]
        fi = info[pick["file"]]
        crop = it.get("crop") or ("contain" if pick["prop"] in MARKS else "center")
        box, basis = None, None
        if crop != "contain" and fi.get("width") and fi.get("height"):
            if it.get("box"):
                box, basis = square_box(it["box"], fi["width"], fi["height"]), "a reviewed box"
            elif pick.get("region") and not it.get("crop"):
                region = parse_region(pick["region"], fi["width"], fi["height"])
                if region:
                    box = square_box(region, fi["width"], fi["height"], margin=0.6)
                    basis = "the subject's region on Commons (P2677), grown 60% and squared"
        pick.update(crop="box" if box else crop, box=[round(b, 4) for b in box] if box else None, crop_basis=basis)
        by_width.setdefault(standard_width(fi.get("width"), fi.get("height"), S, crop, box), []).append(pick["file"])
    thumbs = {}
    for width, names in sorted(by_width.items()):
        for name, ti in commons_info(http, names, width).items():
            thumbs[name] = ti

    for iid, (it, e, cands, era) in plan.items():
        pick, tried, person_reason = chosen[iid]
        lab = it.get("label") or (label(e) if e else None) or it.get("search") or iid
        rejected = [{"property": t["prop"], "file": t["file"], "reason": t["reason"],
                     **{k: t[k] for k in ("jev_private_people", "jev_own_image") if k in t}}
                    for t in tried]
        base = {"id": iid, "kind": it["kind"], "label": lab, "qid": e.get("id") if e else None,
                "description": description(e) or None, "approved": it.get("approved"), "resolved": res[iid]["how"],
                "era": era}
        if not pick:
            reason = person_reason or ("no image claim on the Wikidata item" if not cands
                                       else "every candidate was rejected")
            records.append(dict(base, file=None, fallback="monogram", monogram=monogram(it, e, lab),
                                reason=reason, rejected=rejected, candidates=len(cands)))
            if not person_reason:
                failures.append({"id": iid, "stage": "image", "reason": reason + "; monogram fallback"})
            continue
        fi, ti = info[pick["file"]], thumbs.get(pick["file"], {})
        try:
            turl = ti.get("thumburl") or ti.get("url") or fi.get("url")
            src = http.media(turl, turl.split("?")[0] + "#" + (fi.get("sha1") or ""),
                             os.path.splitext(urllib.parse.urlparse(turl).path)[1] or ".img")
            # The box is in fractions of the original, which the thumbnail shares (same aspect).
            out, (tw, th), upscaled, content = render(src, os.path.join(args.out, iid), S,
                                                      "center" if pick["crop"] == "box" else pick["crop"],
                                                      pick.get("box"))
        except (RuntimeError, OfflineMiss, subprocess.CalledProcessError, urllib.error.HTTPError) as ex:
            failures.append({"id": iid, "stage": "thumbnail", "reason": "%s: %s" % (type(ex).__name__, str(ex)[:200])})
            records.append(dict(base, file=None, fallback="monogram", monogram=monogram(it, e, lab),
                                reason="thumbnail failed", rejected=rejected, candidates=len(cands)))
            continue
        lic = pick["lic"]
        att = clean_author(meta(fi, "Attribution"))
        artist_html = meta(fi, "Artist")
        author = att or clean_author(artist_html)
        prop = pick["prop"]
        noun = NOUN.get(prop, "Image")
        if prop == "P18" and it.get("kind") != "person":
            noun = "Image"    # a company's or a group's own picture (a building, a product, a painting): not a portrait
        title = strip_html(meta(fi, "ObjectName")) or os.path.splitext(pick["file"])[0]
        modified = {"center": "cropped to a centred square and scaled to %d px",
                    "portrait": "cropped to a square (upper part) and scaled to %d px",
                    "box": "cropped to a square around the subject and scaled to %d px",
                    "contain": "scaled to fit a %d px square on transparency, uncropped"}[pick["crop"]] % S
        rs = pick.get("restrictions", [])
        notices = []
        for r in pick.get("rulings", []):
            n = NOTICE.get(r["restriction"])
            if n and r["ruling"] == "identity":
                notices.append(n.format(label=lab))
        review = []
        if it["kind"] == "person":
            review.append("portrait: confirm the crop shows the subject alone and recognizably")
            if pick.get("depicts_subject") is False:
                review.append("structured data does not list the subject as depicted")
            if pick.get("other_people"):
                review.append("structured data lists other people: " + ", ".join(pick["other_people"]))
        if pick["photo"] and it["kind"] != "person":
            review.append("a collective's photo: confirm no private individuals are recognizable")
        aspect = (fi["width"] / fi["height"]) if fi.get("width") and fi.get("height") else None
        if pick["crop"] == "contain" and aspect and aspect >= WORDMARK and prop in ("P154", "P8972", "P2910", "pinned"):
            review.append("wordmark (%.1f:1): unreadable in a square token; the packer gives it a monogram token and "
                          "a wide cell" % aspect)
        if upscaled:
            review.append("upscaled from %dx%d" % (tw, th))
        if pick["property"] == "pinned":
            first = next((x for x in cands if x["property"] != "pinned"), None)
            if first and first["file"] != pick["file"]:
                review.append("pinned file differs from the first candidate (%s %s)" % (first["property"], first["file"]))
        if not pick["in_force"]:
            review.append("no claim covers %s; nearest in time used" % era["basis"])
        page = fi.get("descriptionurl")
        date = re.search(r"\d{4}(-\d\d(-\d\d)?)?", strip_html(meta(fi, "DateTimeOriginal")))
        records.append(dict(
            base, file=os.path.basename(out), property=prop, property_label=PROPS.get(prop, prop),
            rank=pick["rank"], period=[pick["start"], pick["end"]],
            source="Wikimedia Commons", commons_file=pick["file"], source_page=page, title=title,
            original={"width": fi.get("width"), "height": fi.get("height"), "mime": fi.get("mime"),
                      "sha1": fi.get("sha1"), "uploaded": fi.get("timestamp"),
                      "url": (fi.get("url") or "").split("?")[0]},
            thumbnail_url=turl.split("?")[0], fetched_from={"width": tw, "height": th},
            author=author, author_url=first_link(artist_html) if not att else None,
            credit=strip_html(meta(fi, "Credit")) or None, date=date.group(0) if date else None,
            licence=lic["licence"], licence_code=lic["licence_code"], licence_url=lic["licence_url"],
            attribution_required=lic["attribution_required"], share_alike=lic["share_alike"],
            restrictions=rs, rulings=pick.get("rulings", []), crop=pick["crop"], box=pick.get("box"),
            crop_basis=pick.get("crop_basis"), content=content, aspect=round(aspect, 4) if aspect else None,
            monogram=monogram(it, e, lab), modified=modified,
            attribution=attribution(title, author, lic, page, modified, notices),
            credit_short=credit_short(noun, author, lic),
            fetched=http.retrieved(src), size=S, bytes=os.path.getsize(out), sha256=sha256_file(out),
            checks={k: pick[k] for k in ("depicts_subject", "other_people", "people_signals", "jev_private_people",
                                         "jev_own_image") if k in pick},
            review=review, rejected=rejected, candidates=len(cands)))

    # 6. Generic figures for synthetic people.
    figures = [it for it in items if it["kind"] == "figure"]
    om = None
    for it in figures:
        iid, code = it["id"], it["emoji"].upper()
        fs, variant = it.get("set", "openmoji"), it.get("variant", "color")
        try:
            if fs not in FIGURE_SETS:
                raise ValueError("unknown figure set %r" % fs)
            if variant not in ("color", "mask") or (variant == "mask" and fs != "openmoji"):
                raise ValueError("variant must be color, or mask (OpenMoji's black glyphs only)")
            if om is None:
                with open(http.media(OPENMOJI_DATA, OPENMOJI_DATA, ".json"), encoding="utf-8") as f:
                    om = {g["hexcode"]: g for g in json.load(f)}
            g = om.get(code)
            if g and (g.get("subgroups") == "brand" or g.get("group") == "flags"):
                raise ValueError("a %s glyph: not used as a figure" % ("brand" if g.get("subgroups") == "brand"
                                                                        else "flag"))
            if fs == "openmoji":
                if not g:
                    raise ValueError("OpenMoji %s has no %s" % (OPENMOJI_VERSION, code))
                url = OPENMOJI_SVG.format("black" if variant == "mask" else "color", code)
                page = OPENMOJI_PAGE.format(code)
            else:
                parts = code.lower().split("-")
                tw_code = "-".join(parts if "200d" in parts else [p for p in parts if p != "fe0f"])
                url, page = TWEMOJI_SVG.format(tw_code), TWEMOJI_PAGE.format(tw_code)
            src = http.media(url, url, ".svg")
            out, _, _, content = render(src, os.path.join(args.out, iid), S, "contain", svg=True,
                                        mask=variant == "mask")
        except (ValueError, RuntimeError, OfflineMiss, subprocess.CalledProcessError, urllib.error.HTTPError) as ex:
            failures.append({"id": iid, "stage": "figure", "reason": "%s %s: %s" % (fs, code, str(ex)[:160])})
            records.append({"id": iid, "kind": "figure", "label": it.get("label"), "file": None, "fallback": "none",
                            "reason": str(ex)[:160]})
            continue
        fset = FIGURE_SETS[fs]
        name = it.get("label") or (g["annotation"] if g else code)
        glyph = g["annotation"] if g else code
        modified = "rendered from SVG at %d px%s" % (S, ", black lines turned white for tinting" if variant == "mask"
                                                     else "")
        if fs == "openmoji":
            author = "%s for OpenMoji" % g["openmoji_author"] if g.get("openmoji_author") else "OpenMoji"
            text = '"%s" by %s, %s' % (glyph, author, fset["credit"])
        else:
            author = fset["credit"]
            text = '"%s", %s' % (glyph, author)
        text += ", %s (%s), %s. %s%s." % (fset["licence"], fset["url"], page, modified[0].upper(), modified[1:])
        if fs == "openmoji":
            text += " This thumbnail is shared under CC BY-SA 4.0."
        records.append({
            "id": iid, "kind": "figure", "label": name, "glyph": glyph, "emoji": code, "variant": variant,
            "file": os.path.basename(out), "source": "%s %s" % (fset["name"], fset["version"]),
            "source_page": page, "source_url": url, "author": author, "licence": fset["licence"],
            "licence_code": fset["code"], "licence_url": fset["url"], "attribution_required": True,
            "share_alike": fs == "openmoji", "restrictions": [], "crop": "contain", "content": content,
            "modified": modified, "attribution": text,
            "credit_short": "Figure: %s · %s" % (fset["name"], fset["licence"]),
            "fetched": http.retrieved(src), "size": S, "bytes": os.path.getsize(out), "sha256": sha256_file(out),
            "review": [], "rejected": []})

    order = {it["id"]: i for i, it in enumerate(items)}
    records.sort(key=lambda r: order[r["id"]])
    manifest = {
        "tool": "Tools/images/fetch.py", "version": VERSION, "size": S, "user_agent": AGENT,
        "built": max([r["fetched"] for r in records if r.get("fetched")] or [datetime.date.today().isoformat()]),
        "policy": {"licences": ["Public domain", "CC0", "CC BY (any version)", "CC BY-SA (any version)"],
                   "restriction_rulings": CLASS_RULING, "shared_classes": sorted(SHARED_CLASSES),
                   "template_rulings": TEMPLATE_RULING,
                   "person": "a public adult's own P18 portrait only",
                   "collective_photos": "only without people (words, depicts, and Jev with --jev)",
                   "doc": "Docs/research/holistic/images.md"},
        "images": records, "failures": failures}
    with open(os.path.join(args.out, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=1, ensure_ascii=False)
        f.write("\n")

    for r in records:
        if r.get("file"):
            print("ok    %-22s %-7s %-14s %-58s -> %s (%d KB)" % (
                r["id"], r.get("property", "figure"), r["licence"][:14], (r.get("commons_file") or r.get("emoji"))[:58],
                r["file"], r["bytes"] // 1024))
        else:
            print("none  %-22s %s (fallback %s %s)" % (r["id"], r.get("reason", "")[:100], r.get("fallback"),
                                                       r.get("monogram", "")))
        for t in r.get("rejected", []):
            print("        rejected %s %s: %s" % (t["property"], t["file"][:60], t["reason"][:110]))
    for fl in failures:
        print("FAIL  %-22s [%s] %s" % (fl["id"], fl["stage"], fl["reason"][:140]))
    print("%d of %d with an image; %d failures; %d network requests, %d from the cache, %.0f s waited" % (
        sum(1 for r in records if r.get("file")), len(records), len(failures), http.stats["network"],
        http.stats["cached"], http.stats["waited_s"]))


# ------------------------------------------------------------------------------------------------------------- pack

TILE_LIGHT, TILE_DARK, RULE, TEXT_LIGHT, SHEET_BG = "0xE8ECF2", "0x2A3140", "0x1B2230", "0xE8ECF2", "0x10141C"


def _rounded(w, h, r):
    """geq alpha for a rounded rectangle w x h with radius r, anti-aliased, times the existing alpha."""
    d = ("(hypot(max(abs(X+0.5-%g)-%g,0),max(abs(Y+0.5-%g)-%g,0))-%g)" % (w / 2, w / 2 - r, h / 2, h / 2 - r, r))
    return "geq=r='r(X,Y)':g='g(X,Y)':b='b(X,Y)':a='alpha(X,Y)*clip(0.5-%s,0,1)'" % d


def _circle(s):
    d = "(hypot(X+0.5-%g,Y+0.5-%g)-%g)" % (s / 2, s / 2, s / 2 - 0.5)
    return "geq=r='r(X,Y)':g='g(X,Y)':b='b(X,Y)':a='alpha(X,Y)*clip(0.5-%s,0,1)'" % d


def _content_crop(r):
    x, y, w, h = r.get("content") or [0, 0, r["size"], r["size"]]
    return "crop=%d:%d:%d:%d" % (w, h, x, y)


def cell_plan(r, cell):
    """[(key, span, shape)] for one manifest record."""
    if not r.get("file"):
        return [(r["id"], 1, "monogram")] if r.get("monogram") else []
    if r["kind"] == "figure":
        return [(r["id"], 1, "glyph")]
    if r["kind"] == "person":
        return [(r["id"], 1, "circle")]
    prop = r.get("property")
    if prop in ("P41",):
        return [(r["id"], 1, "flag")]
    if prop in ("P94", "P158"):
        return [(r["id"], 1, "emblem")]
    if prop == "P18":
        return [(r["id"], 1, "photo")]
    if (r.get("aspect") or 1) >= WORDMARK:
        return [(r["id"], 1, "monogram"), (r["id"] + "@wide", 2, "wordmark")]
    return [(r["id"], 1, "tile")]


def render_cell(r, shape, span, cell, src_dir, dst, tmp):
    """One cell (span cells wide) as an RGBA PNG with its shape in alpha; returns the content rect in the cell."""
    inset = max(2, cell // 32)
    inner = cell - 2 * inset
    src = os.path.join(src_dir, r["file"]) if r.get("file") else None
    if shape == "circle":
        vf = "format=rgba,scale=%d:%d:flags=lanczos,%s,pad=%d:%d:%d:%d:color=black@0" % (
            inner, inner, _circle(inner), cell, cell, inset, inset)
        ffmpeg(["-i", src, "-vf", vf, "-frames:v", "1"] + ENCODE_PNG + [dst])
        return [inset, inset, inner, inner]
    if shape == "photo":
        vf = "format=rgba,scale=%d:%d:flags=lanczos,%s,pad=%d:%d:%d:%d:color=black@0" % (
            inner, inner, _rounded(inner, inner, cell / 8), cell, cell, inset, inset)
        ffmpeg(["-i", src, "-vf", vf, "-frames:v", "1"] + ENCODE_PNG + [dst])
        return [inset, inset, inner, inner]
    if shape in ("glyph", "emblem"):
        vf = ("format=rgba,%s,scale=%d:%d:force_original_aspect_ratio=decrease:flags=lanczos,"
              "pad=%d:%d:(ow-iw)/2:(oh-ih)/2:color=black@0" % (_content_crop(r), inner, inner, cell, cell))
        ffmpeg(["-i", src, "-vf", vf, "-frames:v", "1"] + ENCODE_PNG + [dst])
        return [inset, inset, inner, inner]
    if shape == "flag":
        a = r.get("aspect") or 1.5
        fw, fh = (inner, max(8, round(inner / a))) if a >= 1 else (max(8, round(inner * a)), inner)
        vf = ("format=rgba,%s,scale=%d:%d:flags=lanczos,pad=%d:%d:1:1:color=%s,%s,"
              "pad=%d:%d:(ow-iw)/2:(oh-ih)/2:color=black@0"
              % (_content_crop(r), fw - 2, fh - 2, fw, fh, RULE, _rounded(fw, fh, max(2, cell / 21)), cell, cell))
        ffmpeg(["-i", src, "-vf", vf, "-frames:v", "1"] + ENCODE_PNG + [dst])
        return [(cell - fw) // 2, (cell - fh) // 2, fw, fh]
    if shape in ("tile", "wordmark"):
        tw, th = span * cell - 2 * inset, inner
        lw, lh = round(tw - 2 * cell * 0.12), round(th - 2 * cell * 0.12)
        fc = ("color=c=%s:s=%dx%d,format=rgba[bg];[0:v]format=rgba,%s,scale=%d:%d:force_original_aspect_ratio=decrease:"
              "flags=lanczos[lg];[bg][lg]overlay=(W-w)/2:(H-h)/2:format=auto,format=rgba,%s,pad=%d:%d:%d:%d:color=black@0"
              % (TILE_LIGHT, tw, th, _content_crop(r), lw, lh, _rounded(tw, th, cell / 8), span * cell, cell, inset,
                 inset))
        ffmpeg(["-i", src, "-filter_complex", fc, "-frames:v", "1"] + ENCODE_PNG + [dst])
        return [inset, inset, tw, th]
    if shape == "monogram":
        text = r.get("monogram") or "?"
        txt = os.path.join(tmp, "mono-%s.txt" % hashlib.sha256(text.encode("utf-8")).hexdigest()[:12])
        with open(txt, "w", encoding="utf-8") as f:
            f.write(text)
        # Liberation Sans Bold capitals advance up to about 0.8 em (W is 0.94, most are 0.67-0.78): fit the text in
        # 78% of the tile, and no taller than half of it.
        fs = round(min(0.5 * inner, 0.78 * inner / (0.8 * max(1, len(text)))))
        vf = ("color=c=%s:s=%dx%d,format=rgba,drawtext=fontfile=%s:textfile=%s:fontsize=%d:fontcolor=%s:"
              "x=(w-text_w)/2:y=(h-text_h)/2,%s,pad=%d:%d:%d:%d:color=black@0"
              % (TILE_DARK, inner, inner, FONT_BOLD, txt, fs, TEXT_LIGHT, _rounded(inner, inner, cell / 8), cell, cell,
                 inset, inset))
        ffmpeg(["-f", "lavfi", "-i", vf, "-frames:v", "1"] + ENCODE_PNG + [dst])
        return [inset, inset, inner, inner]
    raise ValueError(shape)


def cmd_pack(args):
    src_dir = args.dir
    if not os.path.exists(os.path.join(src_dir, "manifest.json")):
        sys.exit("error: no manifest.json in %s (run fetch first)" % src_dir)
    with open(os.path.join(src_dir, "manifest.json"), encoding="utf-8") as f:
        man = json.load(f)
    atlas = args.atlas or os.path.join(src_dir, "atlas")
    cell, page = args.cell, args.page
    if page % cell or page // cell < 2:
        sys.exit("--page must be a multiple of --cell")
    n = page // cell
    per = n * n
    tmp = os.path.join(atlas, ".cells")
    if os.path.isdir(tmp):
        shutil.rmtree(tmp)
    os.makedirs(tmp)
    entries, excluded = [], {}
    for r in man["images"]:
        if r["kind"] == "person" and r.get("file") and not r.get("approved") and not args.allow_unapproved:
            excluded[r["id"]] = "a real person not yet approved by the user (no 'approved' in the item)"
            print("left out %s: %s" % (r["id"], excluded[r["id"]]))
            continue
        for key, span, shape in cell_plan(r, cell):
            entries.append((key, span, shape, r))
    # Append-only placement: ids keep their slot from the previous index; retired ids keep theirs, empty.
    index_path = os.path.join(atlas, "index.json")
    prev = {}
    if os.path.exists(index_path):
        with open(index_path, encoding="utf-8") as f:
            old = json.load(f)
        if old.get("cell") == cell and old.get("page") == page:
            prev = {k: v for k, v in old.get("cells", {}).items()}
            prev.update(old.get("retired", {}))
    taken, place = {}, {}
    for key, span, shape, r in entries:
        p = prev.get(key)
        if p and p.get("span") == span and all(p["g"] + k not in taken for k in range(span)):
            place[key] = p["g"]
            for k in range(span):
                taken[p["g"] + k] = key
    retired = {k: v for k, v in prev.items() if k not in {e[0] for e in entries}}
    for k, v in retired.items():
        for j in range(v.get("span", 1)):
            taken.setdefault(v["g"] + j, None)
    g = 0
    for key, span, shape, r in entries:
        if key in place:
            continue
        while True:
            col = g % n
            if col + span <= n and all(g + k not in taken for k in range(span)):
                break
            g += 1
        place[key] = g
        for k in range(span):
            taken[g + k] = key
        g += span
    pages = max(1, -(-max([v + 1 for v in taken] + [1]) // per))
    blank = os.path.join(tmp, "blank.png")
    ffmpeg(["-f", "lavfi", "-i", "color=c=black@0:s=%dx%d,format=rgba" % (cell, cell), "-frames:v", "1"]
           + ENCODE_PNG + [blank])
    slot_files, cells = {}, {}
    for key, span, shape, r in entries:
        g0 = place[key]
        dst = os.path.join(tmp, "e-%04d.png" % g0)
        content = render_cell(r, shape, span, cell, src_dir, dst, tmp)
        for k in range(span):
            part = dst if span == 1 else os.path.join(tmp, "e-%04d-%d.png" % (g0, k))
            if span > 1:
                ffmpeg(["-i", dst, "-vf", "crop=%d:%d:%d:0" % (cell, cell, k * cell), "-frames:v", "1"]
                       + ENCODE_PNG + [part])
            slot_files[g0 + k] = part
        pg, slot = divmod(g0, per)
        row, col = divmod(slot, n)
        u0, u1 = col / n, (col + span) / n
        v0, v1 = 1 - (row + 1) / n, 1 - row / n
        rec = {"g": g0, "page": pg, "slot": slot, "col": col, "row": row, "span": span, "shape": shape,
               "uv": [round(u0, 6), round(v0, 6), round(u1, 6), round(v1, 6)], "content": content,
               "kind": r["kind"], "label": r.get("label"), "qid": r.get("qid")}
        if r.get("approved"):
            rec["approved"] = r["approved"]
        if shape == "monogram":
            rec.update(text=r.get("monogram"), credit_short=None,
                       attribution="A monogram of %s's ticker or short name in Liberation Sans Bold; no third-party "
                                   "image." % r.get("label"))
            if r.get("file"):
                rec["wide"] = r["id"] + "@wide"
        else:
            shape_note = {"circle": "masked to a circle", "photo": "masked to a rounded square",
                          "glyph": "fitted to the cell", "emblem": "fitted to the cell",
                          "flag": "fitted at its own aspect with rounded corners and a 1 px rule",
                          "tile": "fitted on a light rounded tile", "wordmark": "fitted on a light rounded tile"}[shape]
            modified = "%s; packed into a %s atlas cell, %s" % (
                r["modified"], "%d px" % cell if span == 1 else "%d x %d px" % (cell * span, cell), shape_note)
            rec.update({k: r.get(k) for k in ("author", "licence", "licence_url", "source_page", "commons_file",
                                              "title", "credit_short", "share_alike", "restrictions", "sha256")})
            rec["modified"] = modified
            rec["attribution"] = r["attribution"].replace(r["modified"][0].upper() + r["modified"][1:],
                                                          modified[0].upper() + modified[1:])
        cells[key] = rec
    os.makedirs(atlas, exist_ok=True)
    names = []
    for pg in range(pages):
        seq = os.path.join(tmp, "p%d" % pg)
        os.makedirs(seq)
        for s in range(per):
            gidx = pg * per + s
            shutil.copyfile(slot_files.get(gidx, blank), os.path.join(seq, "%04d.png" % s))
        name = "actors-%d.png" % pg
        ffmpeg(["-framerate", "1", "-start_number", "0", "-i", os.path.join(seq, "%04d.png"), "-vf",
                "tile=%dx%d" % (n, n), "-frames:v", "1"] + ENCODE_PNG + [os.path.join(atlas, name)])
        names.append(name)
    index = {"tool": "Tools/images/fetch.py pack", "version": VERSION, "cell": cell, "page": page, "grid": n,
             "uv_origin": "bottom-left (Unity); row 0 is the top of the PNG", "pages": names,
             "manifest_sha256": sha256_file(os.path.join(src_dir, "manifest.json")),
             "cells": cells, "retired": retired, "excluded": excluded}
    with open(index_path, "w", encoding="utf-8") as f:
        json.dump(index, f, indent=1, ensure_ascii=False)
        f.write("\n")
    # Contact sheet: every entry on the scene's dark ground with its id, for the crop review.
    pw, ph = 2 * cell + 16, cell + 30
    panels = []
    for i, (key, span, shape, r) in enumerate(entries):
        txt = os.path.join(tmp, "id-%03d.txt" % i)
        with open(txt, "w", encoding="utf-8") as f:
            f.write(key)
        dst = os.path.join(tmp, "panel-%03d.png" % i)
        g0 = place[key]
        src = os.path.join(tmp, "e-%04d.png" % g0)
        fc = ("color=c=%s:s=%dx%d,format=rgba[bg];[bg][0:v]overlay=8:6:format=auto,drawtext=fontfile=%s:textfile=%s:"
              "fontsize=12:fontcolor=0xC8D0DC:x=8:y=h-20" % (SHEET_BG, pw, ph, FONT, txt))
        ffmpeg(["-i", src, "-filter_complex", fc, "-frames:v", "1"] + ENCODE_PNG + [dst])
        panels.append(dst)
    cols = 6
    rows = max(1, -(-len(panels) // cols))
    seq = os.path.join(tmp, "sheet")
    os.makedirs(seq)
    filler = os.path.join(tmp, "panel-blank.png")
    ffmpeg(["-f", "lavfi", "-i", "color=c=%s:s=%dx%d,format=rgba" % (SHEET_BG, pw, ph), "-frames:v", "1"]
           + ENCODE_PNG + [filler])
    for i in range(rows * cols):
        shutil.copyfile(panels[i] if i < len(panels) else filler, os.path.join(seq, "%04d.png" % i))
    ffmpeg(["-framerate", "1", "-start_number", "0", "-i", os.path.join(seq, "%04d.png"), "-vf",
            "tile=%dx%d" % (cols, rows), "-frames:v", "1"] + ENCODE_PNG + [os.path.join(atlas, "contact.png")])
    # CREDITS.md, grouped by kind.
    groups = [("person", "People"), ("company", "Companies"), ("government", "States and governments"),
              ("group", "Groups and institutions"), ("place", "Places"), ("figure", "Generic figures")]
    lines = ["# Image credits", "",
             "Generated by `Tools/images/fetch.py pack` from the fetch manifest. Every image is used under its licence "
             "as given; changes are noted. CC BY-SA images are adapted (cropped, scaled, masked) and shared under the "
             "same licence. Public-domain images carry no legal attribution requirement and are credited as a "
             "courtesy.", ""]
    for kind, title in groups:
        rows_ = [(k, c) for k, c in cells.items() if c["kind"] == kind and c.get("licence")]
        if not rows_:
            continue
        lines += ["## " + title, ""]
        for k, c in rows_:
            lines.append("- **%s** (`%s`): %s" % (c["label"], k, c["attribution"]))
        lines.append("")
    with open(os.path.join(atlas, "CREDITS.md"), "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    shutil.rmtree(tmp)
    used = sum(c["span"] for c in cells.values())
    print("%d cells (%d slots) on %d page(s) of %dx%d at %d px: %s" % (
        len(cells), used, pages, n, n, cell, ", ".join(os.path.join(atlas, x) for x in names)))
    print("index %s; contact sheet %s; credits %s" % (index_path, os.path.join(atlas, "contact.png"),
                                                       os.path.join(atlas, "CREDITS.md")))


# ------------------------------------------------------------------------------------------------------------- main

def main():
    argv = sys.argv[1:]
    if argv and argv[0] not in ("fetch", "pack", "-h", "--help"):
        argv = ["fetch"] + argv  # the draft's interface: fetch.py ITEMS.json --out DIR
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    sub = ap.add_subparsers(dest="cmd", required=True)
    f = sub.add_parser("fetch", help="fetch, check and crop thumbnails; write the manifest")
    f.add_argument("items")
    f.add_argument("--out", required=True)
    f.add_argument("--cache", default=os.path.join(HERE, ".cache"))
    f.add_argument("--size", type=int, default=256)
    f.add_argument("--offline", action="store_true", help="use only the cache (rebuilds byte for byte)")
    f.add_argument("--refresh", action="store_true", help="refetch API answers (media are keyed by their sha1)")
    f.add_argument("--jev", action="store_true", help="Jev chooses among name-search candidates and checks "
                                                       "collectives' photos for private individuals")
    f.add_argument("--maxlag", type=int, default=5, help="seconds; 0 omits it (only for a run someone waits on)")
    f.add_argument("--patience", type=float, default=600.0, help="seconds to wait in all for lag or rate limits")
    p = sub.add_parser("pack", help="pack the thumbnails of a fetch into atlas pages, an index and credits")
    p.add_argument("dir")
    p.add_argument("--atlas")
    p.add_argument("--cell", type=int, default=128)
    p.add_argument("--page", type=int, default=2048)
    p.add_argument("--allow-unapproved", action="store_true",
                   help="pack real people whose item has no 'approved' (for a review sheet only, never for the app)")
    args = ap.parse_args(argv)
    try:
        if args.cmd == "fetch":
            cmd_fetch(args)
        else:
            cmd_pack(args)
    except (RuntimeError, OfflineMiss) as e:
        sys.exit("error: %s" % e)


if __name__ == "__main__":
    main()

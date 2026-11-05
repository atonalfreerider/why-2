#!/usr/bin/env python3
"""Square thumbnails of the economy's actors from open sources, with a licence manifest (draft).

Real people, companies, governments and groups come from Wikidata's image properties through Wikimedia Commons.
Synthetic or typical people get generic figures from OpenMoji or Twemoji, never a photo of a real person.

    python3 -I -B Tools/images/fetch.py ITEMS.json --out DIR [--cache DIR] [--size 256] [--offline] [--refresh]
                                       [--jev] [--maxlag 5]

ITEMS.json is a list of objects:
    {"id": "elon-musk", "kind": "person", "qid": "Q317521"}
    {"id": "walmart", "kind": "company", "qid": "Q483551"}
    {"id": "sweden", "kind": "government", "qid": "Q34", "prefer": ["P94"]}
    {"id": "opec", "kind": "group", "qid": "Q7795", "file": "Flag of OPEC.svg"}   (a reviewed Commons file pins it)
    {"id": "factory-worker", "kind": "figure", "emoji": "1F9D1-200D-1F3ED", "set": "openmoji"}
kind: person (a public figure: a human with a Wikidata item and an English Wikipedia article, an adult), company,
government, group, figure. Optional: "label", "crop" (center, portrait, contain), "prefer" (a property order).

Writes DIR/<id>.jpg or .png (size x size) and DIR/manifest.json: for each item its id, kind, label, file, source page,
author, licence, licence URL, attribution text, fetched date and sha256, the checks made, and every candidate that
was rejected and why. An item with no acceptable image gets "file": null and a monogram the app draws itself.

Policy (Docs/research/holistic/images.md):
  - licences: public domain, CC0, CC BY and CC BY-SA of any version (ported and IGO included); anything else is
    rejected (GFDL-only, FAL, OGL, custom attribution licences, non-free files, files nominated for deletion);
  - non-copyright restrictions (Commons' restriction-* classes): trademarked, insignia and communist are kept for an
    item's own logo, flag, arms or seal (shown unaltered, only to identify it); personality only for a person's own
    portrait; everything else (nazi, ihl, currency, design, costume, fan-art, 2257, ...) is rejected;
  - a collective's photo (P18) is a last resort and is rejected when its description, categories or depicted
    subjects suggest people in it (and, with --jev, when Jev gives 0.3 or more that private individuals are shown);
  - a person's image is only their own P18 portrait; a person who is not a public adult gets no image.

Wikimedia etiquette: a User-Agent with the repository URL, one request at a time with a pause (Action API under
5 requests a second, media concurrency 1), batched titles, gzip for API calls, maxlag=5 with its Retry-After, 429
Retry-After honoured, and thumbnails only at Wikimedia's standard widths. Every response is cached under --cache
(sha256 of the URL, as Tools/social/wikidata.py does; a manifest.json of downloads with URL, retrieval date and
sha256, as Tools/lives/build.py does), so reruns are free and --offline rebuilds the outputs byte-identically.
Images are processed with ffmpeg and ffprobe (no PIL).
"""
import argparse
import datetime
import gzip
import hashlib
import html
import json
import os
import re
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from email.utils import parsedate_to_datetime

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
AGENT = "why-2-images/0.1 (https://github.com/atonalfreerider/why-2) python-urllib/%d.%d" % sys.version_info[:2]
WIKIDATA_API = "https://www.wikidata.org/w/api.php"
COMMONS_API = "https://commons.wikimedia.org/w/api.php"
# https://www.mediawiki.org/wiki/Common_thumbnail_sizes: other widths are rounded up by the API and refused when
# requested directly.
STANDARD_WIDTHS = (20, 40, 60, 120, 250, 330, 500, 960, 1280, 1920, 3840)

OPENMOJI_VERSION, TWEMOJI_VERSION = "17.0.0", "17.0.3"
OPENMOJI_SVG = "https://cdn.jsdelivr.net/npm/openmoji@%s/color/svg/{}.svg" % OPENMOJI_VERSION
OPENMOJI_DATA = "https://cdn.jsdelivr.net/npm/openmoji@%s/data/openmoji.json" % OPENMOJI_VERSION
OPENMOJI_PAGE = "https://openmoji.org/library/emoji-{}/"
TWEMOJI_SVG = "https://cdn.jsdelivr.net/gh/jdecked/twemoji@%s/assets/svg/{}.svg" % TWEMOJI_VERSION
TWEMOJI_PAGE = "https://github.com/jdecked/twemoji/blob/v%s/assets/svg/{}.svg" % TWEMOJI_VERSION
FIGURE_SETS = {
    "openmoji": {"name": "OpenMoji", "licence": "CC BY-SA 4.0", "code": "cc-by-sa-4.0",
                 "url": "https://creativecommons.org/licenses/by-sa/4.0/",
                 "credit": "OpenMoji, the open-source emoji and icon project (openmoji.org)"},
    "twemoji": {"name": "Twemoji", "licence": "CC BY 4.0", "code": "cc-by-4.0",
                "url": "https://creativecommons.org/licenses/by/4.0/",
                "credit": "Twemoji, Copyright 2019 Twitter, Inc and other contributors"},
}

PROPS = {"P18": "image", "P154": "logo", "P8972": "small logo or icon", "P2910": "icon", "P41": "flag",
         "P94": "coat of arms", "P158": "seal"}
ORDER = {
    "person": ["P18"],
    "company": ["P8972", "P154", "P2910", "P18"],
    "government": ["P41", "P94", "P158", "P8972", "P154", "P2910", "P18"],
    "group": ["P8972", "P154", "P41", "P2910", "P94", "P158", "P18"],
}
KINDS = set(ORDER) | {"figure"}
NOUN = {"P18": "Photo", "P154": "Logo", "P8972": "Logo", "P2910": "Icon", "P41": "Flag", "P94": "Arms", "P158": "Seal"}
HUMAN = "Q5"
LIGHT_ON_DARK = "Q6545942"  # P8798 "for color scheme": light-on-dark (the tiles are light, so it goes last)
IDENTITY_RESTRICTIONS = {"trademarked", "insignia", "communist"}  # kept for an item's own mark, never altered
LICENCE_RE = re.compile(r"^cc-by(-sa)?-\d(\.\d)?(-[a-z]{2,4})?$")  # any version; ported (-de) and -igo included
PEOPLE_RE = re.compile(
    r"\b(people|persons?|workers?|employees?|staff|crowds?|members?|protest\w*|rall(y|ies)|marche(s|rs)|strik(e|es|ers|ing)"
    r"|demonstrat\w*|celebrat\w*|audiences?|attendees?|students?|children|kids|famil(y|ies)|delegates?|supporters?"
    r"|fans|volunteers?|customers?|shoppers?|passengers?|visitors?|tourists?|picket\w*|group photo|selfie|portrait"
    r"|men|women|man|woman|boys?|girls?)\b", re.I)
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
            return max(1.0, (parsedate_to_datetime(value) - datetime.datetime.now(datetime.timezone.utc)).total_seconds())
        except (TypeError, ValueError):
            return default


class Http:
    """Cached, polite HTTP: one request at a time, a pause after each, retries that honour Retry-After."""

    def __init__(self, cache, offline=False, refresh=False, maxlag=5, pause_api=0.3, pause_media=0.2):
        self.cache, self.offline, self.refresh, self.maxlag = cache, offline, refresh, maxlag
        self.pause_api, self.pause_media = pause_api, pause_media
        self.requests = 0
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

    def _write(self, path, body):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        tmp = path + ".part"
        with open(tmp, "wb") as f:
            f.write(body)
        os.replace(tmp, path)

    def _download(self, url, api, check=None, attempts=6):
        headers = {"User-Agent": AGENT}
        if api:
            headers["Accept-Encoding"] = "gzip"  # the robot policy asks for it, except on media files
        last = None
        for attempt in range(attempts):
            try:
                req = urllib.request.Request(url, headers=headers)
                with urllib.request.urlopen(req, timeout=90) as r:
                    body = r.read()
                    if r.headers.get("Content-Encoding") == "gzip":
                        body = gzip.decompress(body)
                    response_headers = r.headers
                self.requests += 1
                wait = check(body, response_headers) if check else None
                if wait is not None:  # maxlag: the API answered 200 with an error and a Retry-After
                    last = RuntimeError("maxlag")
                    time.sleep(wait)
                    continue
                time.sleep(self.pause_api if api else self.pause_media)
                return body
            except urllib.error.HTTPError as e:
                self.requests += 1
                last = e
                if e.code == 404:
                    raise
                if e.code in (429, 502, 503, 504):
                    time.sleep(min(120.0, _retry_after(e.headers, 5.0 * (attempt + 1))))
                    continue
                time.sleep(2 * (attempt + 1))
            except (urllib.error.URLError, TimeoutError, ConnectionError, OSError) as e:
                last = e
                time.sleep(2 * (attempt + 1))
        raise RuntimeError(f"request failed after {attempts} attempts: {url[:160]} ({type(last).__name__}: {last})")

    def api(self, base, params, ns):
        """A MediaWiki Action API GET, cached by its URL (maxlag is sent but not part of the key)."""
        params = dict(params, format="json")
        url = base + "?" + urllib.parse.urlencode(sorted(params.items()))
        path = self._path(ns, url, ".json")
        if os.path.exists(path) and not self.refresh:
            with open(path, encoding="utf-8") as f:
                return json.load(f)
        if self.offline:
            raise OfflineMiss("not cached: " + url[:160])
        send = url + ("&maxlag=%d" % self.maxlag if self.maxlag else "")

        def check(body, headers):
            err = json.loads(body).get("error")
            if not err:
                return None
            if err.get("code") == "maxlag":
                return max(5.0, _retry_after(headers, 5.0))
            raise RuntimeError(f"API error {err.get('code')}: {err.get('info', '')[:200]}")

        body = self._download(send, True, check)
        self._write(path, body)
        self._record(path, url, body)
        return json.loads(body)

    def media(self, url, key, ext):
        """A media file (thumbnail, SVG), cached by key (the URL plus the file's Commons sha1, so a re-upload refetches)."""
        path = self._path("media", key, ext)
        if os.path.exists(path):
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
    out, qids = {}, sorted(set(qids))
    for i in range(0, len(qids), 50):
        data = http.api(WIKIDATA_API, {"action": "wbgetentities", "ids": "|".join(qids[i:i + 50]), "props": props,
                                       "languages": "en|mul", "languagefallback": "1", "sitefilter": "enwiki"},
                        "wikidata")
        out.update(data.get("entities", {}))
    return out


def label(e, default=None):
    labels = e.get("labels", {})
    for lang in ("en", "mul"):
        if lang in labels:
            return labels[lang]["value"]
    return default or e.get("id")


def values(e, prop):
    return [c["mainsnak"]["datavalue"]["value"] for c in e.get("claims", {}).get(prop, [])
            if c.get("mainsnak", {}).get("snaktype") == "value" and c.get("rank") != "deprecated"]


def _time(q, prop):
    for s in q.get(prop, []):
        try:
            t = s["datavalue"]["value"]["time"]
            y, m, d = t[1:].split("T")[0].split("-") if t[0] in "+-" else t.split("T")[0].split("-")
            return (int(y), max(1, int(m)), max(1, int(d)))
        except (KeyError, ValueError, TypeError):
            continue
    return None


def candidates(e, order):
    """[(prop, file, rank, start, end, light_on_dark)] in the order to try: by property, then preferred rank, current
    before ended (P582 in the past), dark-on-light before light-on-dark, then Wikidata's own order. Deprecated claims
    are dropped."""
    today = datetime.date.today().timetuple()[:3]
    out = []
    for prop in order:
        rows = []
        for i, c in enumerate(e.get("claims", {}).get(prop, [])):
            ms = c.get("mainsnak", {})
            if c.get("rank") == "deprecated" or ms.get("snaktype") != "value":
                continue
            q = c.get("qualifiers", {})
            start, end = _time(q, "P580"), _time(q, "P582")
            dark = any(s.get("datavalue", {}).get("value", {}).get("id") == LIGHT_ON_DARK for s in q.get("P8798", []))
            ended = end is not None and end <= today
            rows.append(((0 if c.get("rank") == "preferred" else 1, int(ended), int(dark), i),
                         (prop, ms["datavalue"]["value"], c.get("rank"), start, end, dark)))
        out += [r for _, r in sorted(rows)]
    return out


def public_adult(e):
    """(ok, reason): a human with an English Wikipedia article who is 18 or older."""
    kinds = {v.get("id") for v in values(e, "P31") if isinstance(v, dict)}
    if HUMAN not in kinds:
        return False, "not a human (P31 lacks Q5)"
    if "enwiki" not in e.get("sitelinks", {}):
        return False, "no English Wikipedia article (not treated as a public figure)"
    born = None
    for c in e.get("claims", {}).get("P569", []):
        try:
            v = c["mainsnak"]["datavalue"]["value"]
            t = v["time"]
            y = int(t[1:5]) if t[0] in "+-" else int(t[:4])
            born = (y, int(t[6:8]) or 12, int(t[9:11]) or 31) if v.get("precision", 9) >= 11 else (y + 1, 12, 31)
            break
        except (KeyError, ValueError, TypeError):
            continue
    if born is None:
        return False, "no birth date (adulthood cannot be checked)"
    today = datetime.date.today()
    if (today.year - born[0], today.month, today.day) < (18, born[1], born[2]):
        return False, "under 18"
    return True, "human, enwiki, adult"


def monogram(e, fallback_label):
    """P1813 short name in English, else the initials of the label's words."""
    for c in e.get("claims", {}).get("P1813", []) if e else []:
        v = c.get("mainsnak", {}).get("datavalue", {}).get("value", {})
        if isinstance(v, dict) and v.get("language") in ("en", "mul") and len(v.get("text", "")) <= 6:
            return v["text"]
    words = [w for w in re.split(r"[\s\-]+", fallback_label or "") if w and w.lower() not in ("of", "the", "and", "for", "&")]
    return "".join(w[0].upper() for w in words[:4]) or "?"


# ---------------------------------------------------------------------------------------------------------- commons

def strip_html(s):
    s = re.sub(r"<[^>]+>", " ", s or "")
    return re.sub(r"\s+", " ", html.unescape(s)).strip()


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


def commons_depicts(http, pageids):
    """pageid -> [QIDs] from structured data on Commons (MediaInfo M<pageid>, P180 depicts)."""
    out, mids = {}, sorted({"M%d" % p for p in pageids if p})
    for i in range(0, len(mids), 50):
        data = http.api(COMMONS_API, {"action": "wbgetentities", "ids": "|".join(mids[i:i + 50]), "props": "claims"},
                        "commons")
        for mid, e in data.get("entities", {}).items():
            st = e.get("statements") or e.get("claims") or {}
            if isinstance(st, list):  # an entity without statements serializes them as []
                st = {}
            out[int(mid[1:])] = [c["mainsnak"]["datavalue"]["value"]["id"] for c in st.get("P180", [])
                                 if c.get("mainsnak", {}).get("snaktype") == "value"]
    return out


def meta(info, key):
    return (info.get("extmetadata", {}).get(key) or {}).get("value", "")


def licence_check(info):
    """(ok, reason, licence dict) under the whitelist: PD, CC0, CC BY, CC BY-SA of any version."""
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
    if code in ("pd", "pdm") or short.lower().startswith("public domain"):
        lic.update(licence="Public domain", licence_url=None, attribution_required=False, share_alike=False)
        return True, "public domain", lic
    if code == "cc0" or short.upper().startswith("CC0"):
        lic.update(licence="CC0 1.0", licence_url=url or "https://creativecommons.org/publicdomain/zero/1.0/",
                   attribution_required=False, share_alike=False)
        return True, "CC0", lic
    if LICENCE_RE.match(code) or re.match(r"^CC BY(-SA)? \d\.\d( [A-Za-z]{2,4})?$", short):
        if not url:
            m = re.match(r"^cc-(by(?:-sa)?)-(\d\.\d)(?:-([a-z]{2,4}))?$", code)
            if m:
                url = "https://creativecommons.org/licenses/%s/%s/%s" % (m.group(1), m.group(2),
                                                                            (m.group(3) + "/") if m.group(3) else "")
                lic["licence_url_derived"] = True
            lic["licence_url"] = url
        if not lic["licence_url"]:
            return False, f"{short or code}: no licence URL", lic
        return True, short or code, lic
    return False, f"licence not on the whitelist: {short or code or 'none given'}", lic


def restriction_check(info, kind, prop):
    """(ok, reason, restrictions)."""
    rs = [r for r in strip_html(meta(info, "Restrictions")).split("|") if r]
    for r in rs:
        if r in IDENTITY_RESTRICTIONS:
            continue  # the item's own mark or insignia, or a photo with one in it; shown unaltered, to identify it
        if r == "personality" and kind == "person" and prop == "P18":
            continue  # a public figure's own portrait, used to inform, never to endorse
        return False, f"restriction '{r}' is not allowed for a {kind}'s {PROPS.get(prop, prop)}", rs
    return True, "", rs


def people_signals(info, depicted_humans):
    """Words in the description, title or categories, and human subjects in 'depicts', that suggest people in a photo."""
    text = " ".join([strip_html(meta(info, "ImageDescription")), strip_html(meta(info, "ObjectName")),
                     strip_html(meta(info, "Categories")).replace("|", " ; ")])
    words = sorted({m.group(0).lower() for m in PEOPLE_RE.finditer(text)})
    return words + ["depicts " + h for h in depicted_humans]


def standard_width(w, h, size, crop):
    """The smallest standard width whose thumbnail covers (cover crops) or fits (contain) a size x size tile."""
    w, h = max(1, w or size), max(1, h or size)
    need = size * max(1.0, w / h) if crop != "contain" else (size if w >= h else size * w / h)
    for s in STANDARD_WIDTHS:
        if s >= need:
            return s
    return STANDARD_WIDTHS[-1]


# ------------------------------------------------------------------------------------------------------------ jev

def jev_private_people(http, photos):
    """Noul per photo: would it show identifiable private individuals? Cached by the request's sha256."""
    if not photos:
        return {}
    sys.path.insert(0, TOOLS)
    import jev  # Tools/jev.py: reads the key itself and never prints it
    state = {"photos": photos}
    questions = {}
    for i, _ in enumerate(photos):
        questions["p%d" % i] = {
            "type": "noul",
            "instructions": f"Would the photo described in `photos[{i}]` show identifiable private individuals if it "
                            f"were used as the picture of `photos[{i}].organization`?",
            "criteria": {
                "true": "The description, title, categories or depicted subjects say or imply that people who are not "
                        "public figures are shown recognizably: workers, members, staff, customers, protesters, "
                        "attendees, a crowd or a group photo.",
                "false": "The photo shows a building, a sign, a landscape, a map, a product, an emblem or an object, "
                         "with no people or only incidental distant figures, or it shows only public figures."}}
    key = hashlib.sha256(json.dumps({"state": state, "questions": questions}, sort_keys=True).encode("utf-8")).hexdigest()
    path = os.path.join(http.cache, "jev", key + ".json")
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            answers = json.load(f)
    else:
        if http.offline:
            raise OfflineMiss("Jev answers not cached")
        if not jev.available():
            raise RuntimeError("--jev: no Jev credential available (live Jev calls unavailable)")
        answers = jev.call_jev(state, questions)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8") as f:
            json.dump(answers, f, indent=1, sort_keys=True)
    return {i: float(answers["p%d" % i]["noul"]) for i in range(len(photos))}


# ----------------------------------------------------------------------------------------------------------- images

def probe(path):
    r = subprocess.run(["ffprobe", "-v", "error", "-select_streams", "v:0", "-show_entries",
                        "stream=width,height,pix_fmt", "-of", "json", path], capture_output=True, text=True, check=True)
    s = json.loads(r.stdout)["streams"][0]
    return int(s["width"]), int(s["height"]), s.get("pix_fmt", "")


def render(src, dst_stem, size, crop, svg=False):
    """Square size x size: 'center' and 'portrait' crop a square (portrait keeps the upper part, where faces are) and
    scale it; 'contain' fits the whole image and pads it with transparency. JPEG for opaque crops, PNG otherwise."""
    pre = ["-width", str(size), "-height", str(size), "-keep_ar", "1"] if svg else []
    w, h, pix = (size, size, "rgba") if svg else probe(src)
    alpha = svg or any(a in pix for a in ("rgba", "argb", "bgra", "abgr", "ya", "pal8", "gbrap", "yuva"))
    if crop == "contain":
        sw, sh = (size, max(1, round(size * h / w))) if w >= h else (max(1, round(size * w / h)), size)
        vf = (f"format=rgba,scale={sw}:{sh}:flags=lanczos,"
              f"pad={size}:{size}:{(size - sw) // 2}:{(size - sh) // 2}:color=black@0")
        ext, enc = ".png", ["-pix_fmt", "rgba", "-compression_level", "9"]
    else:
        side = min(w, h)
        x = (w - side) // 2
        y = round((h - side) * (0.12 if crop == "portrait" else 0.5))
        vf = f"crop={side}:{side}:{x}:{y}:exact=1,scale={size}:{size}:flags=lanczos"
        if alpha:
            ext, enc = ".png", ["-pix_fmt", "rgba", "-compression_level", "9"]
        else:
            ext, enc = ".jpg", ["-pix_fmt", "yuvj420p", "-q:v", "3"]
    dst = dst_stem + ext
    for stale in (".jpg", ".png"):
        if stale != ext and os.path.exists(dst_stem + stale):
            os.remove(dst_stem + stale)
    subprocess.run(["ffmpeg", "-nostdin", "-loglevel", "error", "-y"] + pre +
                   ["-i", src, "-vf", vf, "-frames:v", "1", "-map_metadata", "-1"] + enc + [dst], check=True)
    return dst, (w, h), min(w, h) < size if crop != "contain" else max(w, h) < size


def sha256_file(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def attribution(author, lic, source_page, modified, extra=""):
    by = author or "Unknown author"
    if lic["licence"] == "Public domain":
        text = f"{by}, public domain, via Wikimedia Commons ({source_page})."
    else:
        text = f"{by}, {lic['licence']} ({lic['licence_url']}), via Wikimedia Commons ({source_page})."
    text += f" {modified[0].upper()}{modified[1:]}."
    if lic.get("share_alike"):
        text += f" This thumbnail is shared under {lic['licence']}."
    return (text + (" " + extra if extra else "")).strip()


def credit_short(noun, author, lic):
    who = author if author and len(author) <= 40 else (author[:37] + "..." if author else "")
    if lic["licence"] == "Public domain":
        return f"{noun}: {who + ', ' if who and noun == 'Photo' else ''}public domain"
    return f"{noun}: {who} · {lic['licence']}" if who else f"{noun}: {lic['licence']}"


# ------------------------------------------------------------------------------------------------------------- main

def slug_ok(s):
    return isinstance(s, str) and re.fullmatch(r"[a-z0-9][a-z0-9_-]{0,63}", s) is not None


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("items")
    ap.add_argument("--out", required=True)
    ap.add_argument("--cache", default=os.path.join(HERE, ".cache"))
    ap.add_argument("--size", type=int, default=256)
    ap.add_argument("--offline", action="store_true", help="use only the cache")
    ap.add_argument("--refresh", action="store_true", help="refetch API answers (media are keyed by their sha1)")
    ap.add_argument("--jev", action="store_true", help="ask Jev whether a collective's photo shows private people")
    ap.add_argument("--maxlag", type=int, default=5, help="seconds; 0 omits it (interactive use only)")
    args = ap.parse_args()
    if args.offline and args.refresh:
        sys.exit("--refresh downloads; it cannot be combined with --offline")
    if not 32 <= args.size <= 512:
        sys.exit("--size must be 32-512 (the app uses 128-256)")
    with open(args.items, encoding="utf-8") as f:
        items = json.load(f)
    seen = set()
    for it in items:
        if not slug_ok(it.get("id")) or it["id"] in seen:
            sys.exit(f"bad or duplicate id: {it.get('id')!r} (lower case, digits, - and _)")
        seen.add(it["id"])
        if it.get("kind") not in KINDS:
            sys.exit(f"{it['id']}: kind must be one of {sorted(KINDS)}")
        if it["kind"] == "figure" and not it.get("emoji"):
            sys.exit(f"{it['id']}: a figure needs an emoji code (e.g. 1F9D1-200D-1F3ED)")
        if it["kind"] == "person" and not it.get("qid"):
            sys.exit(f"{it['id']}: a real person needs a Wikidata QID")
        if it["kind"] != "figure" and not (it.get("qid") or it.get("file")):
            sys.exit(f"{it['id']}: needs a qid or a Commons file")
    os.makedirs(args.out, exist_ok=True)
    http = Http(args.cache, args.offline, args.refresh, args.maxlag)
    S = args.size
    records, failures = [], []

    # 1. Wikidata: the items, their image claims (or a pinned file).
    ents = wd_entities(http, [it["qid"] for it in items if it.get("qid")])
    plan = {}
    for it in items:
        if it["kind"] == "figure":
            continue
        e = ents.get(it.get("qid"), {}) if it.get("qid") else {}
        if it.get("qid") and (not e or "missing" in e):
            failures.append({"id": it["id"], "reason": f"Wikidata item {it['qid']} not found"})
            continue
        order = list(it.get("prefer") or []) + [p for p in ORDER[it["kind"]] if p not in (it.get("prefer") or [])]
        if it["kind"] == "person":
            order = ["P18"]  # a person's image is only their own portrait
        cands = candidates(e, order) if e else []
        if it.get("file"):
            cands = [("pinned", it["file"], "pinned", None, None, False)] + [c for c in cands if c[1] != it["file"]]
        plan[it["id"]] = (it, e, cands)

    # 2. Commons: metadata of every candidate, depicts of the photos, the depicted items' kinds.
    files = sorted({c[1] for _, _, cs in plan.values() for c in cs})
    info = commons_info(http, files) if files else {}
    photo_pages = {info[c[1]].get("pageid") for it, _, cs in plan.values() for c in cs
                   if c[0] in ("P18", "pinned") and not info.get(c[1], {}).get("missing")}
    depicts = commons_depicts(http, photo_pages) if photo_pages else {}
    dq = sorted({q for qs in depicts.values() for q in qs})
    dents = wd_entities(http, dq, props="labels|claims") if dq else {}
    is_human = {q: HUMAN in {v.get("id") for v in values(dents.get(q, {}), "P31") if isinstance(v, dict)} for q in dq}

    # 3. Verdicts in order; Jev (optional) on the collectives' photos that pass everything else.
    verdicts = {}  # (id, file) -> dict
    jev_queue = []
    for iid, (it, e, cands) in plan.items():
        for c in cands:
            prop, name = c[0], c[1]
            fi = info.get(name, {"missing": True})
            ok, why, lic = licence_check(fi)
            v = {"property": prop, "file": name, "rank": c[2], "ok": ok, "reason": why if not ok else "", "lic": lic}
            if ok:
                rok, rwhy, rs = restriction_check(fi, it["kind"], "P18" if prop == "pinned" else prop)
                v.update(restrictions=rs)
                if not rok:
                    v.update(ok=False, reason=rwhy)
            photo = prop == "P18" or (prop == "pinned" and it.get("crop") != "contain" and
                                      fi.get("mime", "").startswith(("image/jpeg", "image/tiff", "image/webp")))
            dep = depicts.get(fi.get("pageid"), []) if photo else []
            v["photo"] = photo
            if it["kind"] == "person":
                v["depicts_subject"] = (it.get("qid") in dep) if dep else None
                others = [label(dents.get(q, {}), q) for q in dep if is_human.get(q) and q != it.get("qid")]
                if others:
                    v["other_people"] = others
            elif photo and v["ok"]:
                sig = people_signals(fi, [label(dents.get(q, {}), q) for q in dep if is_human.get(q)])
                v["people_signals"] = sig
                if sig:
                    v.update(ok=False, reason="a collective's photo that may show people: " + ", ".join(sig[:6]))
                if args.jev:
                    jev_queue.append(((iid, name), {
                        "organization": label(e, it.get("label")) if e else it.get("label", iid),
                        "organization_description": (e.get("descriptions", {}).get("en", {}) or {}).get("value", ""),
                        "file": name, "description": strip_html(meta(fi, "ImageDescription"))[:600],
                        "categories": strip_html(meta(fi, "Categories")).split(" | ")[:20],
                        "depicts": [label(dents.get(q, {}), q) for q in dep]}))
            verdicts[(iid, name)] = v
    if jev_queue:
        p = jev_private_people(http, [s for _, s in jev_queue])
        for i, (k, _) in enumerate(jev_queue):
            verdicts[k]["jev_private_people"] = round(p[i], 3)
            if p[i] >= 0.3 and verdicts[k]["ok"]:
                verdicts[k].update(ok=False, reason=f"Jev: private individuals likely shown (noul {p[i]:.2f})")

    # 4. Choose, then fetch the thumbnails at standard widths.
    chosen = {}
    for iid, (it, e, cands) in plan.items():
        if it["kind"] == "person":
            ok, why = public_adult(e)
            if not ok:
                failures.append({"id": iid, "reason": "not shown as a real person: " + why})
                chosen[iid] = (None, [], why)
                continue
        tried = []
        pick = None
        for c in cands:
            v = verdicts[(iid, c[1])]
            if v["ok"]:
                pick = (c, v)
                break
            tried.append(v)
        chosen[iid] = (pick, tried, None)
    by_width = {}
    for iid, (pick, _, _) in chosen.items():
        if not pick:
            continue
        it = plan[iid][0]
        c, v = pick
        fi = info[c[1]]
        crop = it.get("crop") or ("portrait" if it["kind"] == "person" else "center" if v["photo"] else "contain")
        v["crop"] = crop
        by_width.setdefault(standard_width(fi.get("width"), fi.get("height"), S, crop), []).append(c[1])
    thumbs = {}
    for width, names in sorted(by_width.items()):
        for name, ti in commons_info(http, names, width).items():
            thumbs[name] = ti
    today_line = datetime.date.today().isoformat()

    for iid, (it, e, cands) in plan.items():
        pick, tried, person_reason = chosen[iid]
        lab = it.get("label") or (label(e) if e else None)
        rejected = [{"property": t["property"], "file": t["file"], "reason": t["reason"],
                     **({"jev_private_people": t["jev_private_people"]} if "jev_private_people" in t else {})}
                    for t in tried]
        base = {"id": iid, "kind": it["kind"], "label": lab, "qid": it.get("qid")}
        if not pick:
            reason = person_reason or ("no candidate image on Wikidata" if not cands else "every candidate was rejected")
            records.append(dict(base, file=None, fallback="monogram", monogram=monogram(e, lab),
                                reason=reason, rejected=rejected, candidates=len(cands)))
            if not person_reason:
                failures.append({"id": iid, "reason": reason + "; monogram fallback"})
            continue
        c, v = pick
        fi, ti = info[c[1]], thumbs.get(c[1], {})
        try:
            turl = ti.get("thumburl") or ti.get("url") or fi.get("url")
            src = http.media(turl, turl.split("?")[0] + "#" + (fi.get("sha1") or ""),
                             os.path.splitext(urllib.parse.urlparse(turl).path)[1] or ".img")
            out, (tw, th), upscaled = render(src, os.path.join(args.out, iid), S, v["crop"])
        except (RuntimeError, OfflineMiss, subprocess.CalledProcessError, urllib.error.HTTPError) as ex:
            failures.append({"id": iid, "reason": f"thumbnail failed: {type(ex).__name__}: {str(ex)[:200]}"})
            records.append(dict(base, file=None, fallback="monogram", monogram=monogram(e, lab),
                                reason="thumbnail failed", rejected=rejected, candidates=len(cands)))
            continue
        lic = v["lic"]
        att = strip_html(meta(fi, "Attribution"))
        author = att or strip_html(meta(fi, "Artist")) or None
        prop = "P18" if c[0] == "pinned" and v["photo"] else c[0]
        noun = NOUN.get(prop, "Image")
        modified = ({"portrait": "cropped to a square (upper part) and scaled to %d px",
                     "center": "cropped to a centred square and scaled to %d px",
                     "contain": "scaled to fit a %d px square with transparent padding"}[v["crop"]]) % S
        rs = v.get("restrictions", [])
        extra = []
        if "trademarked" in rs:
            extra.append(f"The mark may be a trademark of {lab}; it is shown only to identify {lab}.")
        if "insignia" in rs:
            extra.append("Official insignia, shown unaltered only to identify the state or organization.")
        if "communist" in rs:
            extra.append("Contains a communist symbol, whose display some jurisdictions restrict.")
        review = []
        if it["kind"] == "person":
            review.append("portrait: confirm the crop shows the subject alone and recognizably")
            if v.get("depicts_subject") is False:
                review.append("structured data does not list the subject as depicted")
            if v.get("other_people"):
                review.append("structured data lists other people: " + ", ".join(v["other_people"]))
        if v["photo"] and it["kind"] != "person":
            review.append("a collective's photo: confirm no private individuals are recognizable")
        if v["crop"] == "contain" and fi.get("width") and fi.get("height") and fi["width"] / fi["height"] >= 2.5:
            review.append("wordmark (%.1f:1): small in a square tile; prefer a monogram or a wide plate"
                          % (fi["width"] / fi["height"]))
        if upscaled:
            review.append(f"upscaled from {tw}x{th}")
        if c[0] == "pinned":
            first = next((x for x in cands if x[0] != "pinned"), None)
            if first and first[1] != c[1]:
                review.append(f"pinned file differs from Wikidata's first candidate ({first[0]} {first[1]})")
        page = fi.get("descriptionurl")
        date = re.search(r"\d{4}(-\d\d(-\d\d)?)?", strip_html(meta(fi, "DateTimeOriginal")))
        records.append(dict(
            base, file=os.path.basename(out), property=prop, property_label=PROPS.get(prop, prop), rank=v["rank"],
            source="Wikimedia Commons", commons_file=c[1], source_page=page,
            original={"width": fi.get("width"), "height": fi.get("height"), "mime": fi.get("mime"),
                      "sha1": fi.get("sha1"), "uploaded": fi.get("timestamp"),
                      "url": (fi.get("url") or "").split("?")[0]},
            thumbnail_url=turl.split("?")[0], fetched_from={"width": tw, "height": th},
            author=author, credit=strip_html(meta(fi, "Credit")) or None, date=date.group(0) if date else None,
            licence=lic["licence"], licence_code=lic["licence_code"], licence_url=lic["licence_url"],
            attribution_required=lic["attribution_required"], share_alike=lic["share_alike"], restrictions=rs,
            crop=v["crop"], modified=modified,
            attribution=attribution(author, lic, page, modified, " ".join(extra)),
            credit_short=credit_short(noun, author, lic),
            fetched=http.retrieved(src), size=S, bytes=os.path.getsize(out), sha256=sha256_file(out),
            checks={k: v[k] for k in ("depicts_subject", "other_people", "people_signals", "jev_private_people")
                    if k in v},
            review=review, rejected=rejected, candidates=len(cands)))

    # 5. Generic figures for synthetic people.
    figures = [it for it in items if it["kind"] == "figure"]
    om = None
    for it in figures:
        iid, code = it["id"], it["emoji"].upper()
        fs = it.get("set", "openmoji")
        if fs not in FIGURE_SETS:
            failures.append({"id": iid, "reason": f"unknown figure set {fs!r}"})
            continue
        try:
            if om is None:
                om_path = http.media(OPENMOJI_DATA, OPENMOJI_DATA, ".json")
                with open(om_path, encoding="utf-8") as f:
                    om = {g["hexcode"]: g for g in json.load(f)}
            g = om.get(code)
            if g and g.get("subgroups") == "brand":
                raise ValueError("a brand glyph (a trademark drawn by OpenMoji): not used")
            if fs == "openmoji":
                if not g:
                    raise ValueError(f"OpenMoji {OPENMOJI_VERSION} has no {code}")
                url, page = OPENMOJI_SVG.format(code), OPENMOJI_PAGE.format(code)
            else:
                tw_code = code.lower()
                url, page = TWEMOJI_SVG.format(tw_code), TWEMOJI_PAGE.format(tw_code)
            src = http.media(url, url, ".svg")
            out, _, _ = render(src, os.path.join(args.out, iid), S, "contain", svg=True)
        except (ValueError, RuntimeError, OfflineMiss, subprocess.CalledProcessError, urllib.error.HTTPError) as ex:
            failures.append({"id": iid, "reason": f"figure {fs} {code}: {type(ex).__name__}: {str(ex)[:160]}"})
            records.append({"id": iid, "kind": "figure", "label": it.get("label"), "file": None, "fallback": "none",
                            "reason": str(ex)[:160]})
            continue
        meta_set = FIGURE_SETS[fs]
        name = it.get("label") or (g["annotation"] if g else code)
        modified = "rendered from SVG at %d px" % S
        if fs == "openmoji":
            author = f"{g['openmoji_author']} for OpenMoji" if g.get("openmoji_author") else "OpenMoji"
            text = f"\"{name}\" by {author}, {meta_set['credit']}"
        else:
            author = meta_set["credit"]
            text = f"\"{name}\", {author}"
        text += f", {meta_set['licence']} ({meta_set['url']}), {page}. {modified[0].upper()}{modified[1:]}."
        records.append({
            "id": iid, "kind": "figure", "label": name, "emoji": code, "file": os.path.basename(out),
            "source": meta_set["name"] + " " + (OPENMOJI_VERSION if fs == "openmoji" else TWEMOJI_VERSION),
            "source_page": page, "source_url": url, "author": author, "licence": meta_set["licence"],
            "licence_code": meta_set["code"], "licence_url": meta_set["url"], "attribution_required": True,
            "share_alike": fs == "openmoji", "restrictions": [], "crop": "contain", "modified": modified,
            "attribution": text,
            "credit_short": f"Figure: {meta_set['name']} · {meta_set['licence']}",
            "fetched": http.retrieved(src), "size": S, "bytes": os.path.getsize(out), "sha256": sha256_file(out),
            "review": [], "rejected": []})

    order = {it["id"]: i for i, it in enumerate(items)}
    records.sort(key=lambda r: order[r["id"]])
    manifest = {
        "tool": "Tools/images/fetch.py", "size": S, "user_agent": AGENT,
        "built": max([r["fetched"] for r in records if r.get("fetched")] or [today_line]),
        "policy": {"licences": ["Public domain", "CC0", "CC BY (any version)", "CC BY-SA (any version)"],
                   "restrictions_kept_for_identification": sorted(IDENTITY_RESTRICTIONS),
                   "personality": "a person's own portrait only", "collective_photos": "only without people"},
        "images": records, "failures": failures}
    with open(os.path.join(args.out, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=1, ensure_ascii=False)
        f.write("\n")

    for r in records:
        if r.get("file"):
            print(f"ok    {r['id']:<22} {r.get('property', 'figure'):<6} {r['licence']:<14} "
                  f"{(r.get('commons_file') or r.get('emoji'))[:58]:<58} -> {r['file']} ({r['bytes'] // 1024} KB)")
        else:
            print(f"none  {r['id']:<22} {r.get('reason', '')[:100]} (fallback {r.get('fallback')} "
                  f"{r.get('monogram', '')})")
        for t in r.get("rejected", []):
            print(f"        rejected {t['property']} {t['file'][:60]}: {t['reason'][:110]}")
    print(f"{sum(1 for r in records if r.get('file'))} of {len(records)} with an image; {len(failures)} failures; "
          f"{http.requests} network requests")


if __name__ == "__main__":
    main()

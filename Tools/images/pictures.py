#!/usr/bin/env python3
"""Picture cards for the narrated walkthrough: Wikimedia Commons files, licence-checked and cut to 16:9 at 960 x 540.

The picture plan names one to three pictures for each stop of a tour. This tool fetches the ones sourced from
Wikimedia Commons; Tools/images/generate.py makes the ones no free image shows. Both write into the same folder and
share its manifest.json. The user's request and the plan are in Docs/research/pictures/README.md.

    python3 -I -B Tools/images/pictures.py fetch PLAN.json --out DIR [--only IDS] [--cache DIR] [--offline]
                                                [--refresh] [--maxlag 5] [--patience 600] [--tour TOUR.json]
    python3 -I -B Tools/images/pictures.py check PLAN.json [--only IDS] [--cache DIR] [...]

check reads only Commons' metadata (no image is downloaded) and prints each Commons picture's verdict, licence, size
and review notes, and the tour's stops without a picture; it exits with 1 when any picture fails. fetch also
downloads, crops and records them.

PLAN.json lists the pictures stop by stop, in any of these shapes:
    {"stops": [{"stop": "big_bang", "pictures": [PICTURE, ...]}, ...]}      (the stop may be given as "id")
    [{"stop": "big_bang", "pictures": [PICTURE, ...]}, ...]
    {"pictures": [PICTURE, ...]}  or  [PICTURE, ...]                        (each PICTURE names its stop)
    {"big_bang": [PICTURE, ...], ...}                                       (stop id -> pictures)
A PICTURE, most informative first within its stop:
    {"id": "big_bang-1", "stop": "big_bang", "source": "commons", "file": "File:Hubble ultra deep field.jpg",
     "caption": "The Hubble Ultra Deep Field ...", "fit": "cover", "focus": [0.5, 0.4]}
    {"id": "first_stars-1", "stop": "first_stars", "source": "generate", "prompt": "...", "caption": "...",
     "why": "no camera saw the first stars, and no free illustration shows them"}
  id      lower case, digits, - and _ (default <stop>-<n>, the picture's place in its stop, from 1); keep it stable,
          it names the file and the cache entry
  source  commons, or generate (made by generate.py; this tool leaves those alone). Without a source, a picture with
          a file is commons and one with only a prompt is generate.
  file    a Commons file name, its File: title, or its commons.wikimedia.org or upload.wikimedia.org URL
  caption the card's caption (the narration's subject); label: a short name for restriction notices
  fit     cover (default: the largest 16:9 crop, centred on focus) or contain (the whole picture on black, for a map
          or diagram)
  focus   [x, y] as fractions of the picture, or center, top, bottom, left, right, portrait (0.5, 0.3): the point a
          cover crop centres on, as far as the picture allows
  box     [x, y, w, h] as fractions: a cover crop of the 16:9 frame around this region (from a crop review)
  matte   the colour behind transparent pixels: white (default, so dark lines on a transparent diagram stay
          legible), black, or #RRGGBB
  person  the name or QID of the person a portrait shows (only a person the narration names); approved: the user's
          approval to show them, recorded in the manifest
  licence, author  what the planner read on the file page, cross-checked against Commons (a difference is a review
          note); credit_author: a shorter author for the card's credit line (the full author stays in the manifest)
  allow_small  true accepts a file under 800 px wide

Policy: Tools/images/fetch.py's own, imported from it, so there is one policy (the reasons are in
Docs/research/holistic/images.md):
  - licences: public domain (including the PD mark), CC0, CC BY and CC BY-SA of any version, ported or IGO; anything
    else is rejected (GFDL only, FAL, OGL, non-free files, files nominated for deletion) - fetch.licence_check;
  - Commons' restriction classes ruled as fetch.restriction_check rules them: personality rights only for the portrait
    of the person a picture names (person); trademarks and insignia shown unaltered with fetch.py's notice; nazi,
    ihl, currency, design, costume, fan-art and any unknown class rejected;
  - at least 800 px wide (vector files exempt) unless allow_small;
  - a photo that may show people (words in its description, title or categories, and humans in its depicts
    statements on Commons, with their Wikidata death dates) gets a review note: a living private person must not be
    a picture's subject, and that is for the reviewer to confirm, never passed silently.

It writes DIR/<id>.jpg, 960 x 540 (cover: cropped to 16:9 and scaled; contain: scaled to fit on black, uncropped;
transparent pixels on the matte), and merges DIR/manifest.json: per picture its id, stop, caption, Commons file,
source page, title, author, licence, licence URL, the card's short credit ("Image: NASA, public domain"), the full
attribution (title, author, source, licence and changes made), restrictions and notices, the original and the
thumbnail used, the crop, fetched date, sha256 and review notes; and the failures. A picture this run processed is
replaced; generate.py's pictures and pictures outside --only are kept while the plan still names them with the same
source. A stale <id>.png from an earlier generated picture of the same id is removed (with its Unity .meta). It also
writes DIR/index.json, what the app reads (Assets/Scripts/UI/TourPictures.cs, from Resources/Data/pictures/):
{"pictures": {"<id>": {"caption", "credit", "generated", "fit"}}}. Temporary files start with a dot, which Unity
ignores, so DIR can be Assets/Resources/Data/pictures while the editor is open.

Wikimedia etiquette is fetch.Http's: a User-Agent naming the project and its repository, one request at a time with
a gap between requests, maxlag=5 on API reads (waiting as Manual:Maxlag_parameter asks), Retry-After honoured on 429
and 503, batched titles, gzip for API calls, and thumbnails only at the standard widths. Every response and image is
cached under --cache (default Tools/images/.cache, shared with fetch.py), so reruns are free and --offline rebuilds
every output byte for byte. Images are processed with ffmpeg and ffprobe alone.
"""
import argparse
import datetime
import json
import os
import re
import subprocess
import sys
import urllib.error
import urllib.parse

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
import fetch as F  # noqa: E402  (one policy: the licence and restriction checks are fetch.py's own)

VERSION = "0.1"
AGENT = ("why-2-pictures/%s (https://github.com/atonalfreerider/why-2; picture cards for the narrated walkthrough) "
         "Python-urllib/%d.%d" % ((VERSION,) + sys.version_info[:2]))
F.AGENT = AGENT  # fetch.Http sends its module's User-Agent; this tool names itself
CARD_W, CARD_H = 960, 540
MIN_WIDTH = 800
UPSCALE_NOTE = 1.1  # a card scaled up more than this from its source gets a review note
CREDIT_AUTHOR_MAX = 44
WIKIDATA_PATIENCE = 120.0  # the depicts check is advisory: it never holds a run for long
DEFAULT_CACHE = os.path.join(HERE, ".cache")
TOUR = os.path.join(ROOT, "Assets", "Resources", "Data", "Walkthrough", "tour.json")
ABOUT = ("Picture cards for the narrated walkthrough, 960 x 540: Wikimedia Commons files (Tools/images/pictures.py) "
         "and AI-generated pictures (Tools/images/generate.py). Docs/research/pictures/README.md.")
SOURCES = {"commons": "commons", "wikimedia": "commons", "wikimedia commons": "commons",
           "generate": "generate", "generated": "generate", "openai": "generate", "ai": "generate"}
FILE_KEYS = ("file", "commons_file", "commons", "filename", "title", "source_page", "url", "page")
PROMPT_KEYS = ("prompt", "generation_prompt", "image_prompt")
FOCUS = {"center": (0.5, 0.5), "centre": (0.5, 0.5), "middle": (0.5, 0.5), "top": (0.5, 0.0),
         "bottom": (0.5, 1.0), "left": (0.0, 0.5), "right": (1.0, 0.5), "portrait": (0.5, 0.3)}
MATTES = {"white": "white", "black": "black"}
ALPHA_FORMATS = ("rgba", "argb", "bgra", "abgr", "ya", "pal8", "gbrap", "yuva")  # as fetch.render reads ffprobe
BITEXACT = ["-fflags", "+bitexact", "-flags:v", "+bitexact"]
ENCODE = {".jpg": BITEXACT + ["-pix_fmt", "yuvj444p", "-q:v", "3"],
          ".png": BITEXACT + ["-pix_fmt", "rgb24", "-compression_level", "9", "-pred", "mixed"]}


def rel(path):
    a = os.path.abspath(path)
    return os.path.relpath(a, ROOT) if a.startswith(ROOT + os.sep) else a


# ------------------------------------------------------------------------------------------------------------- plan

def commons_name(value):
    """A Commons file name (no File: prefix, spaces for underscores) from a name, a File: title or a Commons URL;
    None for anything else (a local Wikipedia upload is not on Commons)."""
    if not isinstance(value, str) or not value.strip():
        return None
    s = value.strip()
    if re.match(r"https?://", s, re.I):
        u = urllib.parse.urlparse(s)
        host, path = u.netloc.lower(), urllib.parse.unquote(u.path)
        parts = [x for x in path.split("/") if x]
        if host in ("upload.wikimedia.org", "thumb.wikimedia.org"):
            # /wikipedia/commons/a/ab/Name.jpg and /wikipedia/commons/thumb/a/ab/Name.jpg/960px-Name.jpg
            if len(parts) < 3 or parts[:2] != ["wikipedia", "commons"]:
                return None
            if parts[2] == "thumb":
                s = parts[5] if len(parts) > 5 else ""
            else:
                s = parts[-1]
        elif host.endswith((".wikimedia.org", ".wikipedia.org")):
            q = urllib.parse.parse_qs(u.query)
            if "title" in q:
                s = q["title"][0]
            elif "/wiki/" in path:
                s = path.split("/wiki/", 1)[1]
            else:
                return None
            if not re.match(r"(File|Image)\s*:", s, re.I):
                return None
        else:
            return None
    s = re.sub(r"^(File|Image)\s*:\s*", "", s, flags=re.I).replace("_", " ").strip()
    return s or None


def file_ref(p):
    """The plan's reference to a Commons file: file, commons_file, commons or filename as given; title, source_page,
    url or page only when it is a File: title or a URL (a picture's title may be just its name)."""
    for k in FILE_KEYS:
        v = p.get(k)
        if isinstance(v, str) and v.strip():
            if k in ("title", "source_page", "url", "page") and not re.match(r"(File|Image)\s*:|https?://", v.strip(),
                                                                           re.I):
                continue
            return v
    return None


def _fractions(v, n):
    if isinstance(v, dict):
        v = [v.get(k) for k in ("x", "y", "w", "h")[:n]]
    if not (isinstance(v, (list, tuple)) and len(v) == n and all(isinstance(x, (int, float)) and not isinstance(x, bool)
                                                                 and 0 <= x <= 1 for x in v)):
        return None
    return [float(x) for x in v]


def load_plan(path):
    """The plan's pictures in order, normalized. A picture the plan gets wrong carries "error" (it fails alone);
    a plan whose shape is not one of the documented ones stops the run."""
    try:
        with open(path, encoding="utf-8") as f:
            data = json.load(f)
    except (OSError, ValueError) as e:
        sys.exit("cannot read the plan %s: %s" % (path, e))
    rows = []

    def take(stop, pics, where):
        if not isinstance(pics, list):
            sys.exit("plan: %s: pictures must be a list" % where)
        for p in pics:
            if not isinstance(p, dict):
                sys.exit("plan: %s: each picture must be an object" % where)
            rows.append((p.get("stop", stop), p))

    def stops(seq):
        for i, s in enumerate(seq):
            if not isinstance(s, dict):
                sys.exit("plan: stop #%d must be an object" % (i + 1))
            take(s.get("stop") or s.get("id"), s.get("pictures") or [], "stop %s" % (s.get("stop") or s.get("id")))

    if isinstance(data, dict) and isinstance(data.get("stops"), list):
        stops(data["stops"])
    elif isinstance(data, dict) and isinstance(data.get("stops"), dict):
        for k, v in data["stops"].items():
            take(k, (v.get("pictures") or []) if isinstance(v, dict) else v, k)
    elif isinstance(data, dict) and isinstance(data.get("pictures"), list):
        take(None, data["pictures"], "pictures")
    elif isinstance(data, list) and data and all(isinstance(s, dict) and isinstance(s.get("pictures"), list)
                                                 for s in data):
        stops(data)
    elif isinstance(data, list):
        take(None, data, "the list")
    elif isinstance(data, dict):
        for k, v in data.items():
            if isinstance(v, list) and all(isinstance(x, dict) for x in v):
                take(k, v, k)
    else:
        sys.exit("plan: expected a list or an object (see the docstring)")
    if not rows:
        sys.exit("plan %s names no pictures in any of the shapes this tool reads (see its docstring)" % path)

    pics, seen, count = [], set(), {}
    for order, (stop, p) in enumerate(rows):
        errs = []
        stop = stop.strip() if isinstance(stop, str) and stop.strip() else None
        if not stop:
            errs.append("no stop")
        count[stop] = count.get(stop, 0) + 1
        pid = p.get("id") if p.get("id") is not None else ("%s-%d" % (stop, count[stop]) if stop else None)
        if not F.slug_ok(pid):
            errs.append("bad id %r (lower case, digits, - and _, up to 64)" % (pid,))
            pid = None
        elif pid in seen:
            errs.append("duplicate id %r" % pid)
        else:
            seen.add(pid)
        raw = file_ref(p)
        has_file = raw is not None
        prompt = next((p[k] for k in PROMPT_KEYS if p.get(k)), None)
        given = p.get("source")
        source = SOURCES.get(str(given).strip().lower()) if given is not None else (
            "commons" if has_file and not prompt else "generate" if prompt and not has_file else None)
        if source is None:
            errs.append("source must be commons or generate (got %r)" % (given,))
        pic = {"id": pid, "stop": stop, "source": source, "order": order, "n": count[stop],
               "caption": str(p.get("caption") or "").strip(), "label": p.get("label") or p.get("subject"),
               "person": p.get("person") or None, "approved": p.get("approved"),
               "allow_small": p.get("allow_small") is True, "plan_licence": p.get("licence") or p.get("license"),
               "plan_author": p.get("author"), "credit_author": p.get("credit_author")}
        if source == "commons":
            pic["file"] = commons_name(raw)
            if not pic["file"]:
                errs.append("no Wikimedia Commons file (file: a name, a File: title or its Commons URL; got %r)"
                            % (raw,))
        elif source == "generate":
            if not isinstance(prompt, str) or not prompt.strip():
                errs.append("no prompt")
            elif len(prompt) > 30000:
                errs.append("prompt over 30000 characters")
            pic["prompt"] = prompt.strip() if isinstance(prompt, str) else None
            pic["why"] = (p.get("why") or p.get("reason") or p.get("why_generate") or p.get("why_no_free_image")
                          or None)
            variant = p.get("variant", 1)
            if not (isinstance(variant, int) and not isinstance(variant, bool) and variant >= 1):
                errs.append("variant must be a whole number from 1")
                variant = 1
            pic["variant"] = variant
        fit = p.get("fit") or "cover"
        if fit not in ("cover", "contain"):
            errs.append("fit must be cover or contain")
        pic["fit"] = fit
        focus = p.get("focus")
        if isinstance(focus, str):
            pic["focus"] = list(FOCUS[focus.lower()]) if focus.lower() in FOCUS else None
        else:
            pic["focus"] = _fractions(focus, 2) if focus is not None else None
        if focus is not None and pic["focus"] is None:
            errs.append("focus must be [x, y] fractions or one of %s" % ", ".join(sorted(FOCUS)))
        box = _fractions(p.get("box"), 4) if p.get("box") is not None else None
        if p.get("box") is not None and (box is None or box[2] <= 0 or box[3] <= 0
                                         or box[0] + box[2] > 1.0001 or box[1] + box[3] > 1.0001):
            errs.append("box must be [x, y, w, h] fractions inside the picture")
            box = None
        pic["box"] = box
        matte = str(p.get("matte") or "white").strip().lower()
        if matte in MATTES:
            pic["matte"] = MATTES[matte]
        elif re.fullmatch(r"(#|0x)[0-9a-f]{6}", matte):
            pic["matte"] = "0x" + matte[-6:].upper()
        else:
            errs.append("matte must be white, black or #RRGGBB")
            pic["matte"] = "white"
        if errs:
            pic["error"] = "; ".join(errs)
        pics.append(pic)
    return pics


def guard_plan(pics, path):
    """Before a run writes: a plan in which no picture is valid is the wrong file or the wrong shape, and writing
    from it would empty the manifest and the index (the plan decides what they hold)."""
    if not any(not p.get("error") for p in pics):
        sys.exit("plan %s: none of its %d pictures is valid (first problem: %s); nothing was written"
                 % (path, len(pics), pics[0].get("error") if pics else "none"))


def select(pics, only):
    """The pictures named by --only (picture ids or stop ids, comma- or space-separated; repeatable)."""
    if not only:
        return pics
    want = {w for o in only for w in re.split(r"[,\s]+", o) if w}
    unknown = want - {p["id"] for p in pics} - {p["stop"] for p in pics}
    if unknown:
        print("warning: --only names nothing in the plan: %s" % ", ".join(sorted(unknown)), file=sys.stderr)
    return [p for p in pics if p["id"] in want or p["stop"] in want]


def tour_stops(path):
    if not path:
        return None
    try:
        with open(path, encoding="utf-8") as f:
            return [s["id"] for s in json.load(f).get("steps", [])]
    except (OSError, ValueError, KeyError, AttributeError, TypeError):
        print("warning: cannot read the tour %s; stops not checked" % path, file=sys.stderr)
        return None


def warn_stops(pics, tour):
    stops = tour_stops(tour)
    if stops is not None:
        unknown = sorted({p["stop"] for p in pics if p["stop"] and p["stop"] not in stops})
        if unknown:
            print("warning: stops not in %s: %s" % (rel(tour), ", ".join(unknown)), file=sys.stderr)
    return stops


def failure(p, stage, reason):
    return {"id": p["id"] or "#%d" % (p["order"] + 1), "stop": p["stop"], "source": p["source"] or "?",
            "stage": stage, "reason": reason}


# --------------------------------------------------------------------------------------------------------- geometry

def cover_crop(w, h, focus=None, box=None):
    """The 16:9 crop [x, y, w, h] as fractions of a w x h picture: the frame around box, else the largest frame
    centred on focus (default the middle), moved inside the picture where it would cross an edge."""
    ar = CARD_W / CARD_H
    if box:
        bx, by, bw, bh = box[0] * w, box[1] * h, box[2] * w, box[3] * h
        cx, cy, cw = bx + bw / 2, by + bh / 2, max(bw, bh * ar)
    else:
        fx, fy = focus or (0.5, 0.5)
        cx, cy, cw = fx * w, fy * h, w
    cw = min(cw, w, h * ar)
    ch = cw / ar
    x = min(max(cx - cw / 2, 0.0), w - cw)
    y = min(max(cy - ch / 2, 0.0), h - ch)
    return [x / w, y / h, cw / w, ch / h]


def plan_geometry(p, w, h):
    """(crop fractions or None for contain, the source width that fills the card without scaling up)."""
    w, h = max(1, w or CARD_W), max(1, h or CARD_H)
    if p["fit"] == "contain":
        return None, min(CARD_W, CARD_H * w / h)
    crop = cover_crop(w, h, p.get("focus"), p.get("box"))
    return crop, CARD_W / crop[2]


def thumb_width(need):
    """The smallest standard thumbnail width that gives need px (Wikimedia serves only these; T414805)."""
    for s in F.STANDARD_WIDTHS:
        if s >= need:
            return s
    return F.STANDARD_WIDTHS[-1]


def render_card(src, dst, fit, crop, matte):
    """Writes dst (.jpg or .png), CARD_W x CARD_H: cover crops the fractions crop and scales it; contain scales the
    whole picture to fit and centres it on black. Transparent pixels are laid on the matte first. Returns the
    source size, whether it had alpha, the picture's rectangle on the card, the crop in source pixels and the scale."""
    w, h, pix = F.probe(src)
    alpha = any(a in pix for a in ALPHA_FORMATS)
    if fit == "contain":
        s = min(CARD_W / w, CARD_H / h)
        sw, sh = max(1, min(CARD_W, round(w * s))), max(1, min(CARD_H, round(h * s)))
        x0, y0 = (CARD_W - sw) // 2, (CARD_H - sh) // 2
        cut, scale, crop_px = "", s, None
    else:
        pw, ph = max(1, min(w, round(crop[2] * w))), max(1, min(h, round(crop[3] * h)))
        px, py = min(max(0, round(crop[0] * w)), w - pw), min(max(0, round(crop[1] * h)), h - ph)
        cut = "crop=%d:%d:%d:%d:exact=1," % (pw, ph, px, py)
        sw, sh, x0, y0 = CARD_W, CARD_H, 0, 0
        scale, crop_px = CARD_W / pw, [px, py, pw, ph]
    ext = os.path.splitext(dst)[1].lower()
    graph = ("[0:v]format=rgba,%sscale=%d:%d:flags=lanczos[fg];color=c=%s:s=%dx%d:r=1,format=rgba[bg];"
             "[bg][fg]overlay=shortest=1:format=rgb,pad=%d:%d:%d:%d:color=black[card]"
             % (cut, sw, sh, matte, sw, sh, CARD_W, CARD_H, x0, y0))
    stem = os.path.splitext(dst)[0]
    for stale in (".jpg", ".png"):  # one card per id: Unity loads Resources by name, without the extension
        for path in (stem + stale, stem + stale + ".meta"):
            if stale != ext and os.path.exists(path):
                os.remove(path)
    tmp = os.path.join(os.path.dirname(dst), "." + os.path.basename(stem) + ".part" + ext)  # Unity skips dot files
    try:
        F.ffmpeg(["-i", src, "-filter_complex", graph, "-map", "[card]", "-frames:v", "1", "-map_metadata", "-1"]
                 + ENCODE[ext] + ["-update", "1", tmp])
        os.replace(tmp, dst)
    finally:
        if os.path.exists(tmp):
            os.remove(tmp)
    return {"source": [w, h], "alpha": alpha, "content": [x0, y0, sw, sh], "crop_px": crop_px,
            "scale": round(scale, 4)}


def modification(fit, box, geo, matte):
    if fit == "contain":
        text = "scaled to fit %d x %d on black, uncropped" % (CARD_W, CARD_H)
    else:
        text = "cropped to 16:9%s and scaled to %d x %d" % (" around a reviewed region" if box else "", CARD_W, CARD_H)
    if geo["alpha"]:
        text += ", transparent areas on %s" % {"white": "white", "black": "black"}.get(matte, matte)
    return text


# --------------------------------------------------------------------------------------------------------- manifest

def manifest_path(out):
    return os.path.join(out, "manifest.json")


def write_json(path, data):
    """Atomically, through a dot file (Unity ignores dot files, so an output folder under Assets sees no temp file)."""
    os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
    tmp = os.path.join(os.path.dirname(path), "." + os.path.basename(path) + ".part")
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1, ensure_ascii=False)
        f.write("\n")
    os.replace(tmp, path)


def index_of(manifest):
    """DIR/index.json, what the app reads (Assets/Scripts/UI/TourPictures.cs): per id the caption, the credit, whether
    the picture is generated, and its fit. The licences and prompts stay in manifest.json."""
    return {"about": "Picture cards by id (tour.json's stops name them); licences, sources and prompts are in "
                     "manifest.json beside this file.",
            "pictures": {r["id"]: {"caption": r.get("caption") or "", "credit": r.get("credit") or "",
                                   "generated": r.get("source") == "generate", "fit": r.get("fit") or "cover"}
                         for r in manifest.get("pictures", [])}}


def read_manifest(out):
    try:
        with open(manifest_path(out), encoding="utf-8") as f:
            m = json.load(f)
    except FileNotFoundError:
        return {}
    except ValueError as e:
        sys.exit("%s is not valid JSON (%s); move it aside first" % (manifest_path(out), e))
    return m if isinstance(m, dict) else {}


def update_manifest(out, plan_path, pics, processed, records, failures, section, info):
    """Merges this run into DIR/manifest.json and returns it. The pictures in processed are replaced by records (a
    processed picture without a record has none); the others are kept while the plan still names them with the same
    source (their stop and caption refreshed from the plan). Failures likewise. Plan order."""
    m = read_manifest(out)
    plan = {}
    for p in pics:
        if p["id"] and p["id"] not in plan:
            plan[p["id"]] = p
    new = {r["id"]: r for r in records}
    kept = {}
    for r in m.get("pictures", []):
        p = plan.get(r.get("id"))
        if p and r["id"] not in processed and r.get("source") == p["source"]:
            kept[r["id"]] = dict(r, stop=p["stop"], caption=p["caption"])
    merged = []
    for pid, p in plan.items():
        r = new.get(pid) or kept.get(pid)
        if r:
            merged.append(r)
    rank = {pid: i for i, pid in enumerate(plan)}
    fails = [f for f in m.get("failures", []) if f.get("id") in plan and f["id"] not in processed
             and f.get("source") == plan[f["id"]]["source"]] + list(failures)
    fails.sort(key=lambda f: (rank.get(f["id"], len(rank)), f.get("stage", "")))
    dates = [str(r.get("fetched") or r.get("created") or "")[:10] for r in merged]
    sections = {"commons": m.get("commons"), "generated": m.get("generated")}
    sections[section] = info
    manifest = {"about": ABOUT, "plan": rel(plan_path), "card": {"width": CARD_W, "height": CARD_H},
                "updated": max([d for d in dates if d] or [datetime.date.today().isoformat()]),
                "commons": sections["commons"], "generated": sections["generated"],
                "pictures": merged, "failures": fails}
    write_json(manifest_path(out), manifest)
    write_json(os.path.join(out, "index.json"), index_of(manifest))
    return manifest


def orphans(out, manifest):
    """Card images in DIR that the manifest does not list (left in place; Unity would still import them)."""
    files = {r.get("file") for r in manifest.get("pictures", [])}
    return sorted(f for f in os.listdir(out) if f.lower().endswith((".jpg", ".png")) and f not in files)


# ---------------------------------------------------------------------------------------------------------- commons

def licence_key(s):
    s = re.sub(r"[^a-z0-9]", "", str(s).lower())
    if s.startswith("pd") or "publicdomain" in s:
        return "pd"
    return "cc0" if s.startswith("cc0") else s


def short_author(author):
    """The author for a card's credit line: without parentheses when too long, then cut at a separator (or with
    "...") to CREDIT_AUTHOR_MAX characters; no final full stop. The full author stays in the manifest."""
    who = re.sub(r"\s+", " ", author or "").strip()
    if len(who) > CREDIT_AUTHOR_MAX:
        who = re.sub(r"\s*\([^()]*\)", "", who).strip()
    if len(who) > CREDIT_AUTHOR_MAX:
        cut = max(who.rfind(sep, 0, CREDIT_AUTHOR_MAX) for sep in (", ", "; ", " and ", " / "))
        who = who[:cut] if cut > 10 else who[:CREDIT_AUTHOR_MAX - 3].rstrip(" ,;:") + "..."
    return who if who.endswith("...") else who.rstrip(" .,;:")


def card_credit(author, lic):
    """The card's credit line: "Image: NASA, public domain", "Image: ESO/M. Kornmesser, CC BY 4.0"."""
    who = short_author(author)
    what = "public domain" if lic["licence"] == "Public domain" else lic["licence"]
    return "Image: %s, %s" % (who, what) if who else "Image: %s" % what


def depicted_humans(http, infos):
    """pageid -> the humans in the file's depicts statements on Commons (P180), each with whether Wikidata gives a
    date of death; and a note when the check could not be made. Advisory: it waits for lag or rate limits for
    WIKIDATA_PATIENCE seconds at most."""
    pageids = sorted({fi.get("pageid") for fi in infos if fi.get("pageid")})
    if not pageids:
        return {}, None
    saved = http.patience
    http.patience = min(saved, WIKIDATA_PATIENCE)
    try:
        dep = F.commons_depicts(http, pageids)
        qids = sorted({q for rows in dep.values() for q, _ in rows if re.fullmatch(r"Q\d+", q or "")})
        people = {}
        for i in range(0, len(qids), 100):
            q = ("SELECT ?item ?itemLabel (SAMPLE(?d) AS ?died) WHERE { VALUES ?item { %s } ?item wdt:P31 wd:Q5 . "
                 "OPTIONAL { ?item wdt:P570 ?d } SERVICE wikibase:label { bd:serviceParam wikibase:language "
                 "\"en,mul\". } } GROUP BY ?item ?itemLabel" % " ".join("wd:" + x for x in qids[i:i + 100]))
            for b in http.sparql(q).get("results", {}).get("bindings", []):
                qid = b["item"]["value"].rsplit("/", 1)[-1]
                people[qid] = {"qid": qid, "label": b.get("itemLabel", {}).get("value", qid),
                               "alive": "died" not in b}
    except (RuntimeError, F.OfflineMiss, urllib.error.HTTPError, ValueError, KeyError) as e:
        return {}, "Commons' depicts statements were not checked (%s)" % str(e)[:120]
    finally:
        http.patience = saved
    return {pid: [people[q] for q, _ in rows if q in people] for pid, rows in dep.items()}, None


def check_commons(http, pics):
    """id -> verdict: ok, stage and reason of a rejection, the file's imageinfo, the licence, restrictions, notices,
    checks and review notes."""
    names = sorted({p["file"] for p in pics})
    info = F.commons_info(http, names) if names else {}
    shared = [n for n in names
              if set(F.strip_html(F.meta(info.get(n, {}), "Restrictions")).split("|")) & F.SHARED_CLASSES]
    templates = F.commons_templates(http, shared, F.TEMPLATE_RULING) if shared else {}
    found = [info[n] for n in names if not info.get(n, {}).get("missing")]
    humans, depicts_note = depicted_humans(http, found) if found else ({}, None)
    verdicts = {}
    for p in pics:
        fi = info.get(p["file"]) or {"missing": True}
        title = fi.get("title") or ""
        name = title[5:] if title.startswith("File:") else p["file"]
        v = verdicts[p["id"]] = {"ok": True, "stage": None, "reason": "", "fi": fi, "name": name, "lic": None,
                                 "restrictions": [], "rulings": [], "notices": [], "review": [], "checks": {}}
        ok, why, lic = F.licence_check(fi)
        v["lic"] = lic
        if not ok:
            v.update(ok=False, stage="missing" if fi.get("missing") else "licence", reason=why)
            continue
        portrait = bool(p.get("person"))
        rok, rwhy, classes, rulings = F.restriction_check(fi, templates.get(p["file"], []),
                                                          "person" if portrait else "picture card",
                                                          "P18" if portrait else "image")
        v["restrictions"] = classes
        v["rulings"] = [{"restriction": a, "ruling": b} for a, b in rulings]
        if not rok:
            if any(b == "portrait" for _, b in rulings) and not portrait:
                rwhy += ("; a file with personality rights is shown only as the portrait of a person the narration "
                         "names (the plan's person)")
            v.update(ok=False, stage="restriction", reason=rwhy)
            continue
        mime = fi.get("mime") or ""
        if mime != "image/svg+xml" and (fi.get("width") or 0) < MIN_WIDTH and not p.get("allow_small"):
            v.update(ok=False, stage="size", reason="%sx%s, under %d px wide (allow_small accepts it)"
                     % (fi.get("width"), fi.get("height"), MIN_WIDTH))
            continue
        label = p.get("label") or F.strip_html(F.meta(fi, "ObjectName")) or os.path.splitext(name)[0]
        for r in v["rulings"]:
            n = F.NOTICE.get(r["restriction"])
            if n and r["ruling"] == "identity":
                v["notices"].append(n.format(label=label))
        if classes:
            v["review"].append("Commons restrictions (%s): shown unaltered, only for what the narration names"
                               % ", ".join(classes))
        people = humans.get(fi.get("pageid"), [])
        sig = F.people_signals(fi, [x["label"] for x in people])
        if portrait:
            v["review"].append("portrait of %s: confirm it shows them and that the narration names them" % p["person"])
            if not p.get("approved"):
                v["review"].append("no approval recorded for showing %s (the plan's approved)" % p["person"])
        elif sig:
            v["review"].append("may show people (%s): confirm no living private person is its subject"
                               % ", ".join(sig[:6]))
        for x in people:
            if x["alive"] and not (portrait and str(p["person"]) in (x["qid"], x["label"])):
                v["review"].append("depicts %s (%s), who has no date of death on Wikidata" % (x["label"], x["qid"]))
        if depicts_note:
            v["review"].append(depicts_note)
        if sig or people:
            v["checks"] = {"people_signals": sig, "depicts_humans": people}
        if p.get("plan_licence") and licence_key(p["plan_licence"]) != licence_key(lic["licence"]):
            v["review"].append("the plan gives the licence as %r; Commons gives %s" % (p["plan_licence"],
                                                                                       lic["licence"]))
        if lic.get("multi_licensed"):
            v["review"].append("multi-licensed: %s taken from its licence categories" % lic["licence"])
        if not p["caption"]:
            v["review"].append("no caption in the plan")
    return verdicts


def tidy(s):
    """An author as Commons' HTML reads once the links are gone: "ESO/ S. Brunier" -> "ESO/S. Brunier"."""
    return re.sub(r"(?<=\S)/ (?=\S)", "/", s) if s else s


def make_record(p, v, turl, src, dst, geo, http):
    fi, lic = v["fi"], v["lic"]
    att = tidy(F.clean_author(F.meta(fi, "Attribution")))
    artist = F.meta(fi, "Artist")
    author = att or tidy(F.clean_author(artist))
    title = F.strip_html(F.meta(fi, "ObjectName")) or os.path.splitext(v["name"])[0]
    page = fi.get("descriptionurl")
    modified = modification(p["fit"], p.get("box"), geo, p["matte"])
    attribution = F.attribution(title, author, lic, page, modified, v["notices"]).replace(
        "This thumbnail is shared under", "This card is shared under")
    date = re.search(r"\d{4}(-\d\d(-\d\d)?)?", F.strip_html(F.meta(fi, "DateTimeOriginal")))
    review = list(v["review"])
    if geo["scale"] > UPSCALE_NOTE:
        cw, ch = (geo["crop_px"] or [0, 0] + geo["source"])[2:]
        review.append("scaled up x%.2f from %dx%d source pixels" % (geo["scale"], cw, ch))
    ow, oh = fi.get("width"), fi.get("height")
    sw, sh = geo["source"]
    if ow and oh and abs((sw / sh) / (ow / oh) - 1) > 0.03:
        review.append("the downloaded image is %dx%d but the original is %dx%d (EXIF rotation?): check the crop"
                      % (sw, sh, ow, oh))
    credit_author = p.get("credit_author") or author
    if not p.get("credit_author") and author and (short_author(author) != author.strip().rstrip(" .")
                                                  or ":" in author):
        review.append("the credit line reads %r%s: set the plan's credit_author if it reads badly" % (
            card_credit(author, lic), "; the full author is required by the licence, so the credits page must carry "
            "it" if lic["attribution_required"] else ""))
    crop = v.get("crop")
    painter = tidy(F.clean_author(artist))
    return {
        "id": p["id"], "stop": p["stop"], "caption": p["caption"], "source": "commons", "file": os.path.basename(dst),
        "commons_file": v["name"], "source_page": page, "title": title,
        "author": author, "artist": painter if att and painter and painter != att else None,
        "author_url": F.first_link(artist) if not att else None,
        "commons_credit": F.strip_html(F.meta(fi, "Credit")) or None, "date": date.group(0) if date else None,
        "licence": lic["licence"], "licence_code": lic["licence_code"], "licence_url": lic["licence_url"],
        "attribution_required": lic["attribution_required"], "share_alike": lic["share_alike"],
        "credit": card_credit(credit_author, lic), "attribution": attribution,
        "restrictions": v["restrictions"], "rulings": v["rulings"], "notices": v["notices"],
        "person": p.get("person"), "approved": p.get("approved"),
        "original": {"width": ow, "height": oh, "mime": fi.get("mime"), "sha1": fi.get("sha1"),
                     "uploaded": fi.get("timestamp"), "url": (fi.get("url") or "").split("?")[0]},
        "thumbnail_url": turl.split("?")[0], "fetched_from": {"width": sw, "height": sh},
        "fit": p["fit"], "focus": p.get("focus"), "box": p.get("box"),
        "crop": [round(c, 4) for c in crop] if crop else None, "content": geo["content"],
        "matte": p["matte"] if geo["alpha"] else None, "modified": modified,
        "width": CARD_W, "height": CARD_H, "bytes": os.path.getsize(dst), "sha256": F.sha256_file(dst),
        "fetched": http.retrieved(src), "checks": v["checks"], "review": review}


def policy():
    return {"from": "Tools/images/fetch.py (imported: licence_check, restriction_check, people_signals)",
            "licences": ["Public domain", "CC0", "CC BY (any version)", "CC BY-SA (any version)"],
            "restriction_rulings": F.CLASS_RULING, "shared_classes": sorted(F.SHARED_CLASSES),
            "template_rulings": F.TEMPLATE_RULING, "min_width": MIN_WIDTH,
            "people": "personality rights only for the portrait of a person the narration names (person); photos "
                      "that may show people, and depicted people without a date of death, get review notes",
            "doc": "Docs/research/holistic/images.md"}


# ------------------------------------------------------------------------------------------------------------- run

def run(args, download):
    if args.offline and args.refresh:
        sys.exit("--refresh downloads; it cannot be combined with --offline")
    pics = load_plan(args.plan)
    if download:
        guard_plan(pics, args.plan)
    stops = warn_stops(pics, args.tour)
    sel = select(pics, args.only)
    mine = [p for p in sel if p["source"] != "generate"]  # commons, and pictures whose source is unknown
    commons = [p for p in mine if p["source"] == "commons" and not p.get("error")]
    failures = [failure(p, "plan", p["error"]) for p in mine if p.get("error")]
    n_generate = sum(1 for p in sel if p["source"] == "generate")
    http = F.Http(args.cache, args.offline, args.refresh, args.maxlag, args.patience)
    try:
        verdicts = check_commons(http, commons)
    except (RuntimeError, F.OfflineMiss) as e:
        sys.exit("error: %s" % e)
    for p in commons:
        v = verdicts[p["id"]]
        if not v["ok"]:
            failures.append(failure(p, v["stage"], v["reason"]))
            continue
        fi = v["fi"]
        v["crop"], need = plan_geometry(p, fi.get("width"), fi.get("height"))
        v["width"] = thumb_width(need)
        ow = fi.get("width") or 0
        if fi.get("mime") != "image/svg+xml" and ow and need > ow * UPSCALE_NOTE:
            v["review"].append("the original is %d px wide; the card needs %d (scaled up)" % (ow, round(need)))
    ok = [p for p in commons if verdicts[p["id"]]["ok"]]

    if not download:
        for p in commons:
            v = verdicts[p["id"]]
            fi, lic = v["fi"], v["lic"]
            size = "%sx%s" % (fi.get("width"), fi.get("height")) if not fi.get("missing") else "-"
            if v["ok"]:
                author = tidy(F.clean_author(F.meta(fi, "Attribution")) or F.clean_author(F.meta(fi, "Artist"))) or "?"
                print("ok    %-24s %-16s %-11s %-5s %s  [%s]" % (p["id"], lic["licence"][:16], size, p["fit"][:5],
                                                                 v["name"][:70], author[:60]))
            else:
                print("FAIL  %-24s [%s] %s  (%s)" % (p["id"], v["stage"], v["reason"][:140], p["file"][:70]))
            for note in v["review"]:
                print("        review: %s" % note[:160])
        for fl in failures:
            if fl["stage"] == "plan":
                print("FAIL  %-24s [plan] %s" % (fl["id"], fl["reason"][:160]))
        if stops is not None:
            have = {p["stop"] for p in pics if not p.get("error")}
            missing = [s for s in stops if s not in have]
            print("the plan has pictures for %d of the tour's %d stops%s" % (
                len(stops) - len(missing), len(stops), "; none for: " + ", ".join(missing) if missing else ""))
        print("%d Commons pictures checked: %d ok, %d failed; %d for generate.py; %d network requests, %d from the "
              "cache" % (len(commons) + sum(1 for p in mine if p.get("error")), len(ok), len(failures), n_generate,
                         http.stats["network"], http.stats["cached"]))
        return 1 if failures else 0

    by_width = {}
    for p in ok:
        by_width.setdefault(verdicts[p["id"]]["width"], set()).add(p["file"])
    thumbs = {}
    try:
        for width, names in sorted(by_width.items()):
            for name, ti in F.commons_info(http, sorted(names), width).items():
                thumbs[(name, width)] = ti
    except (RuntimeError, F.OfflineMiss) as e:
        sys.exit("error: %s" % e)
    os.makedirs(args.out, exist_ok=True)
    old = {r.get("id"): r for r in read_manifest(args.out).get("pictures", [])}
    records = []
    for p in ok:
        v = verdicts[p["id"]]
        fi = v["fi"]
        ti = thumbs.get((p["file"], v["width"]), {})
        turl = ti.get("thumburl") or ti.get("url") or fi.get("url")
        dst = os.path.join(args.out, p["id"] + ".jpg")
        stage = "download"
        try:
            if not turl:
                raise RuntimeError("Commons gave no image URL")
            src = http.media(turl, turl.split("?")[0] + "#" + (fi.get("sha1") or ""),
                             os.path.splitext(urllib.parse.urlparse(turl).path)[1] or ".img")
            stage = "render"
            geo = render_card(src, dst, p["fit"], v["crop"], p["matte"])
        except (RuntimeError, F.OfflineMiss, subprocess.CalledProcessError, urllib.error.URLError, OSError,
                ValueError, KeyError, IndexError) as ex:
            failures.append(failure(p, stage, "%s: %s" % (type(ex).__name__, str(ex)[:200])))
            prev = old.get(p["id"])
            if prev and prev.get("source") == "commons" and prev.get("commons_file") == v["name"] \
                    and os.path.exists(os.path.join(args.out, prev.get("file") or "")):
                # the last good card of the same file stays until it can be fetched again
                records.append(dict(prev, stop=p["stop"], caption=p["caption"]))
            continue
        records.append(make_record(p, v, turl, src, dst, geo, http))

    info = {"tool": "Tools/images/pictures.py", "version": VERSION, "user_agent": AGENT, "policy": policy()}
    manifest = update_manifest(args.out, args.plan, pics, {p["id"] for p in mine if p["id"]}, records, failures,
                               "commons", info)
    for r in records:
        print("ok    %-24s %-16s %-60s -> %s (%d KB)" % (r["id"], r["licence"][:16], r["commons_file"][:60], r["file"],
                                                         r["bytes"] // 1024))
        for note in r["review"]:
            print("        review: %s" % note[:160])
    for fl in failures:
        print("FAIL  %-24s [%s] %s" % (fl["id"], fl["stage"], fl["reason"][:160]))
    extra = orphans(args.out, manifest)
    if extra:
        print("not in the manifest (left in place): %s" % ", ".join(extra))
    print("%d of %d Commons pictures written to %s; %d failures; %d for generate.py; %d network requests, %d from the "
          "cache, %.0f s waited" % (len(records), len(commons) + sum(1 for p in mine if p.get("error")),
                                    rel(args.out), len(failures), n_generate, http.stats["network"],
                                    http.stats["cached"], http.stats["waited_s"]))
    return 0


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    sub = ap.add_subparsers(dest="cmd", required=True)
    for name, helptext in (("fetch", "download, check and crop the Commons pictures; merge the manifest"),
                           ("check", "check the Commons pictures' metadata only (no image downloads)")):
        c = sub.add_parser(name, help=helptext)
        c.add_argument("plan")
        if name == "fetch":
            c.add_argument("--out", required=True)
        c.add_argument("--only", action="append", help="picture or stop ids, comma-separated (repeatable)")
        c.add_argument("--cache", default=DEFAULT_CACHE)
        c.add_argument("--tour", default=TOUR, help="the tour whose stops the plan names ('' to skip the check)")
        c.add_argument("--offline", action="store_true", help="use only the cache (rebuilds byte for byte)")
        c.add_argument("--refresh", action="store_true", help="re-read Commons' metadata (media are keyed by sha1)")
        c.add_argument("--maxlag", type=int, default=5, help="seconds; 0 omits it (only for a run someone waits on)")
        c.add_argument("--patience", type=float, default=600.0, help="seconds to wait in all for lag or rate limits")
    args = ap.parse_args()
    sys.exit(run(args, download=args.cmd == "fetch"))


if __name__ == "__main__":
    main()

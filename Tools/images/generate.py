#!/usr/bin/env python3
"""Generated picture cards: OpenAI's Images API for the pictures that no free image shows.

    python3 -I -B Tools/images/generate.py PLAN.json --out DIR [--model chatgpt-image-latest] [--only IDS] [--dry-run]
                                           [--quality high] [--size 1536x1024] [--max-calls 40]
                                           [--key-file ~/Desktop/openai-key.txt] [--cache DIR] [--tour TOUR.json]

It reads the plan Tools/images/pictures.py reads (its docstring gives the format) and makes each picture whose source
is "generate": one image per picture from POST https://api.openai.com/v1/images/generations (n = 1, a landscape size,
1536 x 1024 by default), cut to 16:9 and written as DIR/<id>.png at 960 x 540 (the plan's fit, focus, box and matte
apply as for Commons pictures). Its entries are merged into DIR/manifest.json beside pictures.py's: id, stop, caption,
the model as the API returns it (the model asked for when the response names none), the plan's prompt, the prompt
sent (the plan's prompt and the house style below), the revised prompt if the API gives one, the reason no free
image fits (the plan's why), created date, sha256, and the credit "AI-generated image (OpenAI <model>)"; DIR/index.json
(what the app reads) gets its caption, that credit, "generated": true and its fit. A generated picture is never
presented as a photograph, a specimen or a historical record (Docs/research/pictures/README.md).

Every request is cached under --cache/openai/ by the sha256 of what is sent (model, prompt, size and quality) and the
plan's variant (a whole number; a new variant asks for a new image of the same prompt), so a picture already
generated is never paid for twice: a cached image is only cut again, and a picture whose manifest entry has the same
key and whose card exists is kept even without the cache. The run stops before it would make more than --max-calls
API calls (default 40). --dry-run sends nothing and does not read the key: it prints what would be generated, the
prompts, and the count.

The key is read at run time from --key-file (default ~/Desktop/openai-key.txt) only when an API call is needed. It
goes into the Authorization header of the API request and nowhere else: never printed, logged, cached, put in a URL
or on a command line; error text from the API is shown with anything that looks like a key redacted. A rejected key
or model, a missing quota or an unverified organization stops the run; a refused prompt (moderation) fails only its
picture. Rate limits and server errors are retried with backoff; a request that times out after it was sent is not
retried (it may have been billed).
"""
import argparse
import base64
import binascii
import datetime
import hashlib
import http.client as httpclient
import json
import os
import re
import subprocess
import sys
import textwrap
import time
import urllib.error
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pictures as P  # noqa: E402  (the plan, the card cut and the shared manifest)

F = P.F
VERSION = "0.1"
API_URL = "https://api.openai.com/v1/images/generations"
DEFAULT_MODEL = "chatgpt-image-latest"
DEFAULT_SIZE = "1536x1024"  # the landscape size GPT image models accept (the others: 1024x1024, 1024x1536, auto)
QUALITIES = ("low", "medium", "high", "auto")
DEFAULT_KEY_FILE = "~/Desktop/openai-key.txt"
TIMEOUT = 300  # seconds; a high-quality image can take minutes
ATTEMPTS = 4
AGENT = "why-2-pictures-generate/%s (https://github.com/atonalfreerider/why-2)" % VERSION
HOUSE_STYLE = ("Style: a clear, uncluttered scientific illustration that reads as an illustration, not a photograph. "
               "Wide landscape composition with the subject well inside the frame, clear of the top and bottom "
               "edges. No text, letters, numbers, labels, captions, watermarks, signatures or logos.")
NOTE = "An AI-generated illustration: not a photograph, a specimen or a historical record."
KEY_RE = re.compile(r"\bsk-[A-Za-z0-9_\-]{16,}")
REDACT_RE = re.compile(r"sk-[A-Za-z0-9_\-*.]{3,}")
STOP_CODES = ("insufficient_quota", "billing_hard_limit_reached", "billing_not_active", "invalid_api_key",
              "model_not_found", "organization_verification_required")


class Fatal(Exception):
    """Every further call would fail the same way: stop the run."""


class PictureError(Exception):
    """This picture failed; go on with the next."""


def redact(text):
    return REDACT_RE.sub("sk-[redacted]", str(text))


def read_key(path):
    """The API key from the key file. Never printed, logged, cached or passed on a command line."""
    path = os.path.expanduser(path)
    try:
        with open(path, encoding="utf-8") as f:
            text = f.read()
    except OSError as e:
        sys.exit("no OpenAI key: cannot read %s (%s)" % (path, e.strerror or type(e).__name__))
    except ValueError:
        sys.exit("no OpenAI key: %s is not UTF-8 text" % path)
    m = KEY_RE.search(text)
    if m:
        return m.group(0)
    lines = [ln.strip() for ln in text.splitlines() if ln.strip() and not ln.strip().startswith("#")]
    if len(lines) == 1:
        v = lines[0].split("=", 1)[-1].strip().strip("'\"")
        if re.fullmatch(r"[A-Za-z0-9_\-.]{20,}", v):
            return v
    sys.exit("no OpenAI key found in %s (expected the key, sk-..., on a line of its own)" % path)


def request_body(p, model, size, quality):
    return {"model": model, "prompt": p["prompt"].rstrip() + "\n\n" + HOUSE_STYLE, "n": 1, "size": size,
            "quality": quality}


def cache_key(body, variant):
    blob = json.dumps({"request": body, "variant": variant}, sort_keys=True, ensure_ascii=False)
    return hashlib.sha256(blob.encode("utf-8")).hexdigest()


def cache_dir(cache):
    return os.path.join(cache, "openai")


def cached(cache, key):
    """(meta, image path) of a cached generation, or None. The meta file is written last, so it implies the image."""
    meta_path = os.path.join(cache_dir(cache), key + ".json")
    try:
        with open(meta_path, encoding="utf-8") as f:
            meta = json.load(f)
    except (OSError, ValueError):
        return None
    img = os.path.join(cache_dir(cache), meta.get("image") or "")
    return (meta, img) if meta.get("image") and os.path.exists(img) else None


def _write(path, blob):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    tmp = path + ".part"
    with open(tmp, "wb") as f:
        f.write(blob)
    os.replace(tmp, path)


def sniff(blob):
    if blob.startswith(b"\x89PNG\r\n\x1a\n"):
        return ".png"
    if blob.startswith(b"\xff\xd8\xff"):
        return ".jpg"
    if blob[:4] == b"RIFF" and blob[8:12] == b"WEBP":
        return ".webp"
    return None


def api_error(e):
    try:
        err = json.loads(e.read().decode("utf-8", "replace")).get("error") or {}
        return err if isinstance(err, dict) else {"message": str(err)}
    except (ValueError, AttributeError, OSError):
        return {}


def call_api(key, body):
    """(response JSON, request id). Raises Fatal or PictureError."""
    data = json.dumps(body).encode("utf-8")
    for attempt in range(ATTEMPTS):
        req = urllib.request.Request(API_URL, data=data, method="POST", headers={
            "Authorization": "Bearer " + key, "Content-Type": "application/json", "Accept": "application/json",
            "User-Agent": AGENT})
        try:
            with urllib.request.urlopen(req, timeout=TIMEOUT) as r:
                raw, rid = r.read(), r.headers.get("x-request-id")
            try:
                return json.loads(raw.decode("utf-8")), rid
            except ValueError:
                raise PictureError("the API answered with something that is not JSON") from None
        except urllib.error.HTTPError as e:
            err = api_error(e)
            code = err.get("code") or err.get("type") or ""
            what = redact("HTTP %d%s: %s" % (e.code, " " + code if code else "", err.get("message") or e.reason))[:400]
            if code in STOP_CODES or e.code in (401, 403, 404) \
                    or (e.code == 400 and err.get("param") in ("model", "size", "quality", "n")):
                raise Fatal(what) from None
            if e.code in (429, 500, 502, 503, 504) and attempt + 1 < ATTEMPTS:
                pause = F._retry_after(e.headers, 15.0 * 2 ** attempt)
                print("  waiting %.0f s (%s)" % (pause, what[:160]), file=sys.stderr, flush=True)
                time.sleep(pause)
                continue
            if e.code == 429:
                raise Fatal(what) from None
            raise PictureError(what) from None
        except urllib.error.URLError as e:  # no connection: nothing was sent
            if attempt + 1 < ATTEMPTS:
                time.sleep(10.0 * 2 ** attempt)
                continue
            raise Fatal("cannot reach the API: %s" % redact(e.reason)[:200]) from None
        except (OSError, httpclient.HTTPException) as e:  # a timeout or a dropped connection after sending
            raise PictureError("%s after the request was sent: it may have been billed, and a rerun sends it again"
                               % type(e).__name__) from None
    raise PictureError("no answer after %d attempts" % ATTEMPTS)


def fetch_url(url):
    """An image the API gives as a URL (signed, on another host): fetched without the key."""
    if not re.match(r"https://", url or ""):
        raise PictureError("the API gave no usable image URL")
    req = urllib.request.Request(url, headers={"User-Agent": AGENT})
    try:
        with urllib.request.urlopen(req, timeout=120) as r:
            return r.read(64 * 1024 * 1024 + 1)
    except (OSError, httpclient.HTTPException) as e:
        raise PictureError("cannot fetch the image URL: %s" % type(e).__name__) from None


def save_original(cache, key, body, resp, rid):
    """Writes the image and its metadata (the response without the image, the request without the key) to the cache
    before anything else is done with them, so a paid image survives a crash."""
    items = resp.get("data") or []
    if not items or not isinstance(items[0], dict):
        raise PictureError("the response has no image")
    item = items[0]
    if item.get("b64_json"):
        try:
            blob = base64.b64decode(item["b64_json"], validate=True)
        except (binascii.Error, ValueError):
            raise PictureError("the image in the response is not valid base64") from None
    else:
        blob = fetch_url(item.get("url"))
    ext = sniff(blob)
    if not ext:
        raise PictureError("the response's image is not a PNG, JPEG or WebP file")
    img = os.path.join(cache_dir(cache), key + ext)
    _write(img, blob)
    meta = {k: v for k, v in resp.items() if k != "data"}
    meta.update(data=[{k: v for k, v in item.items() if k != "b64_json"}], request=body, request_id=rid,
                received=datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
                image=os.path.basename(img), sha256=hashlib.sha256(blob).hexdigest(), bytes=len(blob))
    _write(os.path.join(cache_dir(cache), key + ".json"),
           (json.dumps(meta, indent=1, ensure_ascii=False) + "\n").encode("utf-8"))
    return meta, img


def created_at(meta):
    c = meta.get("created")
    if isinstance(c, (int, float)) and not isinstance(c, bool):
        return datetime.datetime.fromtimestamp(c, datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    return meta.get("received")


def make_record(p, key, meta, img, dst, geo):
    req = meta.get("request") or {}
    model = meta.get("model") or req.get("model")
    item = (meta.get("data") or [{}])[0]
    review = ["AI-generated: confirm it shows what the caption says, has no lettering, and could not pass as a "
              "photograph, a specimen or a historical record"]
    if not p.get("why"):
        review.append("the plan gives no reason why no free image fits (why)")
    if not p["caption"]:
        review.append("no caption in the plan")
    if geo["scale"] > P.UPSCALE_NOTE:
        review.append("scaled up x%.2f" % geo["scale"])
    return {
        "id": p["id"], "stop": p["stop"], "caption": p["caption"], "source": "generate", "file": os.path.basename(dst),
        "generated": True, "credit": "AI-generated image (OpenAI %s)" % model, "note": NOTE,
        "model": model, "model_requested": req.get("model"), "model_named_by_api": "model" in meta,
        "prompt": p["prompt"], "prompt_sent": req.get("prompt"), "revised_prompt": item.get("revised_prompt"),
        "why": p.get("why"), "variant": p["variant"], "size_requested": req.get("size"),
        "quality": meta.get("quality") or req.get("quality"),
        "original": {"width": geo["source"][0], "height": geo["source"][1],
                     "format": os.path.splitext(img)[1].lstrip("."), "sha256": meta.get("sha256")},
        "created": created_at(meta), "usage": meta.get("usage"), "request_id": meta.get("request_id"),
        "cache_key": key, "fit": p["fit"], "focus": p.get("focus"), "box": p.get("box"),
        "crop": [round(c, 4) for c in geo["crop"]] if geo.get("crop") else None, "content": geo["content"],
        "matte": p["matte"] if geo["alpha"] else None,
        "modified": P.modification(p["fit"], p.get("box"), geo, p["matte"]),
        "width": P.CARD_W, "height": P.CARD_H, "bytes": os.path.getsize(dst), "sha256": F.sha256_file(dst),
        "review": review}


def cut(p, key, meta, img, out):
    """The card from a cached original. Raises PictureError."""
    dst = os.path.join(out, p["id"] + ".png")
    try:
        w, h, _ = F.probe(img)
        crop = P.cover_crop(w, h, p.get("focus"), p.get("box")) if p["fit"] == "cover" else None
        geo = P.render_card(img, dst, p["fit"], crop, p["matte"])
    except (subprocess.CalledProcessError, OSError, ValueError, KeyError, IndexError) as e:
        raise PictureError("cutting the card failed: %s: %s" % (type(e).__name__, str(e)[:160])) from None
    geo["crop"] = crop
    return make_record(p, key, meta, img, dst, geo)


def info():
    return {"tool": "Tools/images/generate.py", "version": VERSION, "endpoint": API_URL, "house_style": HOUSE_STYLE,
            "cache": "sha256 of the request (model, prompt, size, quality) and the plan's variant"}


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("plan")
    ap.add_argument("--out", required=True)
    ap.add_argument("--model", default=DEFAULT_MODEL)
    ap.add_argument("--only", action="append", help="picture or stop ids, comma-separated (repeatable)")
    ap.add_argument("--dry-run", action="store_true", help="print what would be generated and the count; send nothing")
    ap.add_argument("--size", default=DEFAULT_SIZE, help="a landscape size the model accepts (default 1536x1024)")
    ap.add_argument("--quality", default="high", choices=QUALITIES)
    ap.add_argument("--max-calls", type=int, default=40, help="stop before making more API calls than this")
    ap.add_argument("--key-file", default=DEFAULT_KEY_FILE)
    ap.add_argument("--cache", default=P.DEFAULT_CACHE)
    ap.add_argument("--tour", default=P.TOUR, help="the tour whose stops the plan names ('' to skip the check)")
    args = ap.parse_args()
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._:-]{1,80}", args.model):
        sys.exit("--model: not a model id: %r" % args.model)
    m = re.fullmatch(r"(\d{3,4})x(\d{3,4})", args.size)
    if not m or int(m.group(1)) <= int(m.group(2)):
        sys.exit("--size must be a landscape WxH the model accepts, such as 1536x1024")

    pics = P.load_plan(args.plan)
    if not args.dry_run:
        P.guard_plan(pics, args.plan)
    P.warn_stops(pics, args.tour)
    sel = P.select(pics, args.only)
    gens = [p for p in sel if p["source"] == "generate"]
    n_other = sum(1 for p in sel if p["source"] == "commons")
    old = {r.get("id"): r for r in P.read_manifest(args.out).get("pictures", []) if r.get("source") == "generate"}
    jobs, failures = [], []
    for p in gens:
        if p.get("error"):
            failures.append(P.failure(p, "plan", p["error"]))
            continue
        body = request_body(p, args.model, args.size, args.quality)
        key = cache_key(body, p["variant"])
        hit = cached(args.cache, key)
        prev = old.get(p["id"])
        keep = (not hit and prev and prev.get("cache_key") == key
                and os.path.exists(os.path.join(args.out, prev.get("file") or "")))
        jobs.append({"p": p, "body": body, "key": key, "hit": hit, "keep": prev if keep else None})
    calls = [j for j in jobs if not j["hit"] and not j["keep"]]

    if args.dry_run:
        print("dry run: nothing is sent and the key is not read. Plan %s; model %s, size %s, quality %s, n=1 per "
              "picture." % (P.rel(args.plan), args.model, args.size, args.quality))
        print("every prompt ends with the house style:\n%s\n" % textwrap.fill(HOUSE_STYLE, 116, initial_indent="    ",
                                                                             subsequent_indent="    "))
        for j in jobs:
            p = j["p"]
            state = "generate" if j in calls else "cached" if j["hit"] else "kept"
            print("%-9s %-26s stop %-18s variant %d  -> %s.png" % (state, p["id"], p["stop"], p["variant"], p["id"]))
            if state == "generate":
                print(textwrap.fill(p["prompt"], 116, initial_indent="    prompt: ", subsequent_indent="            "))
                if p.get("why"):
                    print(textwrap.fill(str(p["why"]), 116, initial_indent="    why:    ",
                                        subsequent_indent="            "))
                print("    caption: %s" % (p["caption"] or "(none)"))
            elif state == "cached":
                print("    the image is in the cache: cut again, no API call")
            else:
                print("    already generated (%s): kept, no API call" % j["keep"].get("file"))
        for fl in failures:
            print("FAIL      %-26s [plan] %s" % (fl["id"], fl["reason"][:150]))
        key_file = os.path.expanduser(args.key_file)
        print("\n%d to generate (%d API call%s, one image each, %s %s %s), %d cached, %d kept, %d plan problem%s; "
              "%d Commons pictures are pictures.py's" % (
                  len(calls), len(calls), "" if len(calls) == 1 else "s", args.model, args.size, args.quality,
                  sum(1 for j in jobs if j["hit"]), sum(1 for j in jobs if j["keep"]), len(failures),
                  "" if len(failures) == 1 else "s", n_other))
        if len(calls) > args.max_calls:
            print("a real run would stop: %d calls is over --max-calls %d" % (len(calls), args.max_calls))
        print("key file %s: %s" % (args.key_file, "present (not read in a dry run)" if os.path.isfile(key_file)
                                   else "MISSING: a real run could not call the API"))
        return 0

    if len(calls) > args.max_calls:
        sys.exit("%d API calls needed, over --max-calls %d; pass a higher --max-calls to allow them (see --dry-run)"
                 % (len(calls), args.max_calls))
    api_key = read_key(args.key_file) if calls else None
    os.makedirs(args.out, exist_ok=True)
    records, done, stopped, made = [], set(), None, 0
    for j in jobs:
        p = j["p"]
        done.add(p["id"])
        if j["keep"]:
            records.append(dict(j["keep"], stop=p["stop"], caption=p["caption"], why=p.get("why")))
            print("kept      %-26s %s (already generated)" % (p["id"], j["keep"].get("file")))
            if any(j["keep"].get(k) != p.get(k) for k in ("fit", "focus", "box")):
                print("          the plan's fit, focus or box changed, but the original is not in the cache: the card "
                      "keeps its old cut (a new cut needs a new image)", file=sys.stderr)
            continue
        try:
            if j["hit"]:
                meta, img = j["hit"]
                how = "cut again from the cache"
            else:
                if stopped:
                    raise PictureError("not attempted: the run stopped (%s)" % stopped[:160])
                print("generate  %-26s ..." % p["id"], flush=True)
                t0 = time.monotonic()
                resp, rid = call_api(api_key, j["body"])
                meta, img = save_original(args.cache, j["key"], j["body"], resp, rid)
                made += 1
                how = "generated in %.0f s" % (time.monotonic() - t0)
            rec = cut(p, j["key"], meta, img, args.out)
        except Fatal as e:
            stopped = str(e)
            failures.append(P.failure(p, "generate", stopped))
            print("STOP      %-26s %s" % (p["id"], stopped[:200]), file=sys.stderr)
            continue
        except PictureError as e:
            failures.append(P.failure(p, "generate", str(e)))
            print("FAIL      %-26s %s" % (p["id"], str(e)[:200]), file=sys.stderr)
            continue
        records.append(rec)
        print("ok        %-26s %s -> %s (%d KB; %s)" % (p["id"], rec["model"], rec["file"], rec["bytes"] // 1024, how))
        if not j["hit"]:  # a paid image goes into the manifest at once
            P.update_manifest(args.out, args.plan, pics, set(done), records, failures, "generated", info())
    manifest = P.update_manifest(args.out, args.plan, pics, {p["id"] for p in gens if p["id"]}, records, failures,
                                 "generated", info())
    extra = P.orphans(args.out, manifest)
    if extra:
        print("not in the manifest (left in place): %s" % ", ".join(extra))
    print("%d of %d generated pictures written to %s (%d API calls made); %d failures%s" % (
        len(records), len(gens), P.rel(args.out), made, len(failures), "; stopped: " + stopped if stopped else ""))
    return 1 if stopped else 0


if __name__ == "__main__":
    sys.exit(main())

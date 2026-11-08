#!/usr/bin/env python3
"""Public-domain official portraits of the politics view's executive cascade, 1945-2026 (Docs/hills-design/POLITICS.md
choice 9; the code side is Docs/research/politics/maps/layer-api-2026-11-07.md section 8).

The user approved them on 7 Nov 2026, verbatim: "yes, use public domain portraits". Docs/research/advantage/brief-v5.md
first glossed that as official US government portraits only; the user's words allow any public-domain portrait, so a US
government work is kept and so is a portrait whose Commons page shows it in the public domain for another reason (copyright
not renewed, published without notice: Truman's among them). It asks for the approval to be recorded per person in
Tools/images/fetch.py's manifest, which each item's "approved" does.

The people are every president and every holder of the offices in the politics data (Tools/politics/build.py's presidents
and officials, from gated facts only through its load()). Each person's atlas id is Politics.ImageId(name)
(Assets/Scripts/Economy/Model/Politics.cs): "pol-" and the name in lower case without accents, each run of other characters
one dash, so the view finds a portrait by the name it shows. A person's Wikidata QID is looked up by name (CirrusSearch among
the holders of the office, haswbstatement:P39, then the entity search) and confirmed by a position held (P39): the office
itself overlapping the tenure, a related position held when it began (a senator, a representative, a deputy secretary,
the vice chairman) or any position whose dates are the tenure's. A person with no confirmed QID, or with two, is listed
and left out, never guessed.

    python3 -I Tools/politics/portraits.py items [--maxlag 0]       people -> QIDs -> Tools/images/politics.json
    python3 -I Tools/politics/portraits.py merge W                  W/items.json: the story items, then the politics items
    python3 -I -B Tools/images/fetch.py fetch W/items.json --out W --cache C --jev --maxlag 0
    python3 -I Tools/politics/portraits.py licences W [--maxlag 0]  keeps public-domain portraits only
    python3 -I -B Tools/images/fetch.py pack W --cell 256
    python3 -I Tools/politics/portraits.py install W [--check]      checks; pages (new ones with metas), index, credits

W is the work directory the story images were fetched into (the mirror's work/stories-images) and C the cache they were
fetched with (the mirror's work/holistic/research-images/cache): fetch rewrites W/manifest.json with only the items it is
given and pack retires every id missing from it, so the politics portraits are fetched and packed with the stories in one
list, and every story image keeps its cell. licences drops from W/manifest.json each politics record that is not in the
public domain (its licence and a PD template on its Commons page; a PD-USGov template is recorded as us_government, any
other PD template as public_domain), and each without an image (the app draws
a monogram for an id it does not find), and lists them in the manifest's "politics_dropped". install checks that every
story cell of the app's index is unchanged in the new one (placement and pixels) and that every politics cell is an
approved person, copies the pages and the index into Assets/Resources/Data/economy/images (a new page gets a .meta copied
from actors-1.png.meta with a fresh GUID), and writes the politics section of Docs/research/advantage/image-credits.md.

Lookups (Wikidata and Commons) are cached under Tools/politics/.cache, with the polite client of Tools/images/fetch.py
(its User-Agent names the repository, no person).
"""
import argparse
import datetime
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import unicodedata
import uuid

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
ROOT = os.path.dirname(TOOLS)
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(TOOLS, "images"))
import build  # noqa: E402  Tools/politics/build.py: load() keeps gate-verified facts no skeptic rejected
import fetch  # noqa: E402  Tools/images/fetch.py: the cached, polite Wikimedia client and its helpers

CACHE = os.path.join(HERE, ".cache")
ITEMS = os.path.join(TOOLS, "images", "politics.json")
STORY_ITEMS = os.path.join(TOOLS, "images", "stories.json")
ATLAS = os.path.join(ROOT, "Assets", "Resources", "Data", "economy", "images")
CREDITS = os.path.join(ROOT, "Docs", "research", "advantage", "image-credits.md")
PREFIX = "pol-"
SINCE = 1945.0

APPROVED = ('2026-11-07, the user\'s seventh direction (Docs/research/advantage/brief-v5.md), verbatim: "yes, use public '
            'domain portraits": public-domain portraits of the executive cascade')

# The offices as the politics data titles them -> their Wikidata items, and the related positions whose holding when a
# tenure began confirms the person (a chamber's leader sits in it; an acting secretary is the deputy; the acting chairman
# is the vice chairman). Wikidata files the Chief of Staff to the Commander in Chief (Leahy, 1942-1949) under the
# chairmanship. Every item's English label (LABELS) is checked at run time, so a changed item confirms no one.
OFFICES = {
    "President": ["Q11696"],
    "Vice President": ["Q11699"],
    "Speaker of the House": ["Q912994"],
    "Senate Majority Leader": ["Q28530268"],
    "Secretary of State": ["Q14213"],
    "Secretary of the Treasury": ["Q4215834"],
    "Secretary of Defense": ["Q735015"],
    "Secretary of War": ["Q2419102"],
    "Secretary of the Navy": ["Q940135"],
    "Chairman of the Joint Chiefs of Staff": ["Q1763459"],
    "Chief of Staff to the Commander in Chief": ["Q1763459"],
}
RELATED = {
    "Speaker of the House": ["Q13218630", "Q139796629"],
    "Senate Majority Leader": ["Q4416090"],
    "Secretary of State": ["Q2301213"],
    "Secretary of the Treasury": ["Q11285124"],
    "Secretary of Defense": ["Q2297543"],
    "Chairman of the Joint Chiefs of Staff": ["Q2916001"],
}
LABELS = {
    "Q11696": "President of the United States", "Q11699": "Vice President of the United States",
    "Q912994": "Speaker of the United States House of Representatives",
    "Q28530268": "Majority Leader of the United States Senate", "Q14213": "United States Secretary of State",
    "Q4215834": "United States Secretary of the Treasury", "Q735015": "United States Secretary of Defense",
    "Q2419102": "United States Secretary of War", "Q940135": "United States Secretary of the Navy",
    "Q1763459": "Chairman of the Joint Chiefs of Staff", "Q13218630": "United States representative",
    "Q139796629": "member of the United States House of Representatives", "Q4416090": "United States senator",
    "Q2301213": "United States Deputy Secretary of State", "Q11285124": "United States Deputy Secretary of the Treasury",
    "Q2297543": "United States Deputy Secretary of Defense", "Q2916001": "Vice Chairman of the Joint Chiefs of Staff",
}
# From the crop review of 7 Nov 2026 (the contact sheets of a trial fetch): official portraits are mostly tall
# head-and-shoulders or three-quarter figures, so every portrait is cropped to its upper square ("portrait"), which keeps the
# head inside the card's circle (a centred square cut Johnson's and Mansfield's heads and put most faces at the top edge);
# a photograph that shows someone else beside the person gets a reviewed box (fractions of the original) around them alone.
CROP = "portrait"
BOXES = {
    # "M Peter McPherson at lectern in 1981.jpg": President Reagan stands behind him; the box keeps McPherson alone
    "pol-m-peter-mcpherson": [0.1818, 0.0218, 0.3247, 0.3636],
}
RANKS = re.compile(r"^(?:General of the (?:Army|Air Force)|Fleet Admiral|General|Admiral)\s+")
SUFFIXES = {"jr", "sr", "ii", "iii", "iv"}
TOLERANCE = datetime.timedelta(days=60)  # between the research's dates and Wikidata's
ADULT = datetime.timedelta(days=round(18 * 365.25))


# ---------------------------------------------------------------------------------------------------------- the people

def image_id(name):
    """Politics.ImageId: "pol-", the name decomposed (NFD) without its combining marks, lower case, each run of characters
    other than a-z and 0-9 one dash between words ("Harry S. Truman" -> pol-harry-s-truman); None without a letter."""
    out, dash = [], False
    for ch in unicodedata.normalize("NFD", name or ""):
        if unicodedata.category(ch) == "Mn":
            continue
        c = ch.lower()
        if len(c) == 1 and ("a" <= c <= "z" or "0" <= c <= "9"):
            if dash and out:
                out.append("-")
            out.append(c)
            dash = False
        else:
            dash = True
    return PREFIX + "".join(out) if out else None


def to_date(x):
    """A decimal year of build.py (four places) -> the date it stands for."""
    y = int(x)
    days = (datetime.date(y + 1, 1, 1) - datetime.date(y, 1, 1)).days
    return datetime.date(y, 1, 1) + datetime.timedelta(days=round((x - y) * days))


def people():
    """Every president and every named officeholder of the politics data whose tenure reaches 1945, in the view's order
    (the presidents, then the offices by rank and start): [{"name", "id", "tenures": [{title, from, to, acting}]}]."""
    control = build.load("us-control")
    presidents = build.presidents(control)
    chambers = build.chambers(control)  # only for the officials' parties; a missing House row is not about people
    officials, _ = build.officials(build.load("us-officials"), chambers, presidents)
    now = build.decimal(datetime.date.fromisoformat(build.AS_OF))
    out = {}
    rows = [dict(r, title="President", acting=False) for r in presidents] + [r for r in officials if r.get("name")]
    for r in rows:
        if r["to"] is not None and r["to"] < SINCE:
            continue
        p = out.setdefault(r["name"], {"name": r["name"], "id": image_id(r["name"]), "tenures": []})
        # a tenure that runs to the research's date (or has no end) is still open
        is_open = r["to"] is None or r["to"] >= now - 0.001
        p["tenures"].append({"title": r["title"], "from": to_date(r["from"]), "to": to_date(min(r["to"] or now, now)),
                             "open": is_open, "acting": bool(r.get("acting")), "fact": r["fact"]})
    return list(out.values())


# ---------------------------------------------------------------------------------------------------------- wikidata

def tokens(s):
    """Lower-case words without accents, apostrophes or periods (O'Neill -> oneill, J.D. -> jd)."""
    s = "".join(ch for ch in unicodedata.normalize("NFD", s or "") if unicodedata.category(ch) != "Mn").lower()
    return re.findall(r"[a-z0-9]+", re.sub(r"['’.]", "", s))


def core(s):
    return [t for t in tokens(s) if t not in SUFFIXES]


def search_name(name):
    """The name without a military rank ("General Colin Powell" -> "Colin Powell")."""
    return RANKS.sub("", name).strip()


def surname(name):
    """The last word that is not a suffix, as written (with its apostrophe), for the full-text search."""
    words = [w for w in search_name(name).split() if core(w)]
    return words[-1].strip(",") if words else name


def variants(name):
    """The name searched by label: as written without the rank, without a suffix, and first and last name only."""
    s = search_name(name)
    words = [w for w in s.split() if core(w)]  # without a suffix (Jr., IV) or a lone comma
    out = [s, " ".join(words).rstrip(","), (words[0] + " " + words[-1]) if len(words) > 2 else None]
    seen, res = set(), []
    for v in out:
        if v and v.lower() not in seen:
            seen.add(v.lower())
            res.append(v)
    return res


def names_of(e):
    labels = [fetch.label(e, "")]
    for lang in ("en", "mul"):
        labels += [a["value"] for a in (e.get("aliases", {}).get(lang) or [])]
    return [n for n in labels if n]


def name_score(e, name):
    """2 when a label or alias is the name (without rank and suffix), 1 when it has the surname and the same first name or
    initial, 0 when it has only the surname, -1 without the surname."""
    want = core(search_name(name))
    if not want:
        return -1
    first, last = want[0], want[-1]
    best = -1
    for n in names_of(e):
        c = core(n)
        if last not in c:
            continue
        if c == want:
            return 2
        f = c[0]
        same = f == first or (len(first) == 1 and f.startswith(first)) or (len(f) == 1 and first.startswith(f))
        best = max(best, 1 if same else 0)
    return best


def claim_date(e, prop):
    for c in (e or {}).get("claims", {}).get(prop, []):
        d, how = fetch._wd_date(c.get("mainsnak", {}), prop == "P569")  # the latest possible birth: the strictest test
        if how == "value" and d and d > datetime.date.min:
            return d
    return None


def positions(e):
    """[(position QID, start, end)] of the item's non-deprecated P39 claims."""
    out = []
    for c in (e or {}).get("claims", {}).get("P39", []):
        ms = c.get("mainsnak", {})
        if c.get("rank") == "deprecated" or ms.get("snaktype") != "value":
            continue
        start, end = fetch.claim_period(c)
        out.append((ms["datavalue"]["value"].get("id"), start, end))
    return out


def span(s, e):
    return "%s to %s" % (s.isoformat() if s else "?", e.isoformat() if e else "open")


def confirm(e, person):
    """(strength, note) that the item is the person: 3 the office held over the tenure, 2 a related position held
    when it began or a position with the tenure's dates, 1 the office held without dates; 0 otherwise."""
    if fetch.HUMAN not in fetch.instance_of(e):
        return 0, "not a human"
    first = min(t["from"] for t in person["tenures"])
    born, died = claim_date(e, "P569"), claim_date(e, "P570")
    if born and born > first - ADULT:
        return 0, "born %s, too late for a tenure from %s" % (born, first)
    if died and died < first:
        return 0, "died %s, before a tenure from %s" % (died, first)
    best = (0, "no position held (P39) matches")
    for t in person["tenures"]:
        lo, hi = t["from"], t["to"]
        for q, s, en in positions(e):
            if q in OFFICES[t["title"]]:
                if s is None and en is None:
                    cand = (1, "P39 %s (%s), undated" % (q, LABELS[q]))
                elif (s or datetime.date.min) <= hi + TOLERANCE and (en or datetime.date.max) >= lo - TOLERANCE:
                    cand = (3, "P39 %s (%s) %s" % (q, LABELS[q], span(s, en)))
                else:
                    continue
            elif q in RELATED.get(t["title"], ()) and s and s <= lo + TOLERANCE and (en is None or en >= lo):
                cand = (2, "P39 %s (%s) %s, held when the tenure as %s began" % (q, LABELS[q], span(s, en), t["title"]))
            elif s and abs(s - lo) <= TOLERANCE and ((en and abs(en - hi) <= TOLERANCE) or (en is None and t["open"])):
                cand = (2, "P39 %s %s: the dates of the tenure as %s" % (q, span(s, en), t["title"]))
            else:
                continue
            if cand[0] > best[0]:
                best = cand
    return best


def cirrus(http, text):
    data = http.api(fetch.WIKIDATA_API, {"action": "query", "list": "search", "srnamespace": "0", "srlimit": "20",
                                         "srsearch": text}, "wikidata")
    return [r["title"] for r in data.get("query", {}).get("search", [])]


def entities(http, qids):
    out = {}
    qids = sorted(set(qids), key=lambda q: int(q[1:]))
    for i in range(0, len(qids), 25):
        data = http.api(fetch.WIKIDATA_API, {"action": "wbgetentities", "ids": "|".join(qids[i:i + 25]),
                                             "props": "labels|aliases|descriptions|claims", "languages": "en|mul",
                                             "languagefallback": "1"}, "wikidata")
        out.update(data.get("entities", {}))
    return out


def check_offices(http):
    ents = entities(http, LABELS)
    wrong = ["%s is %r, not %r" % (q, fetch.label(ents.get(q), None), want) for q, want in LABELS.items()
             if fetch.label(ents.get(q), "") != want]
    if wrong:
        sys.exit("office items changed on Wikidata (fix OFFICES first): " + "; ".join(wrong))


def judge(http, person, qids):
    """[(strength, name score, qid, label, note)] for the candidates that are this person by name and position."""
    rows = []
    for q, e in entities(http, qids).items():
        if not e or "missing" in e:
            continue
        ns = name_score(e, person["name"])
        if ns < 0:
            continue
        strength, note = confirm(e, person)
        if strength:
            rows.append((strength, ns, q, fetch.label(e, q), note))
    return sorted(rows, key=lambda r: (-r[0], -r[1], int(r[2][1:])))


def decide(rows):
    """The one best candidate, or None when there is none or two are equally good."""
    if not rows:
        return None
    if len(rows) > 1 and rows[1][:2] == rows[0][:2]:
        return None
    return rows[0]


def lookup(http, person):
    """{"qid", "label", "confirmed", "how"} or {"qid": None, "reason", "candidates"}."""
    sur = surname(person["name"])
    asked, qids = [], []
    for title in dict.fromkeys(t["title"] for t in person["tenures"]):
        for q in OFFICES[title]:
            text = '%s haswbstatement:P39=%s' % (sur, q)
            hits = cirrus(http, text)
            asked.append({"search": text, "found": hits[:10]})
            qids += hits
    rows = judge(http, person, qids) if qids else []
    pick = decide(rows)
    if not (pick and pick[0] == 3 and pick[1] >= 1):
        for v in variants(person["name"]):
            hits = fetch.wd_search(http, v, limit=10)
            asked.append({"search": v, "found": hits})
            qids += hits
        rows = judge(http, person, qids)
        pick = decide(rows)
    cands = [{"qid": r[2], "label": r[3], "strength": r[0], "name": r[1], "note": r[4]} for r in rows[:5]]
    if not pick:
        why = "two candidates equally confirmed" if rows else "no candidate holds the office or a position of its dates"
        return {"qid": None, "reason": why, "candidates": cands, "how": asked}
    return {"qid": pick[2], "label": pick[3], "confirmed": pick[4], "how": asked, "candidates": cands}


def cmd_items(args):
    http = fetch.Http(CACHE, args.offline, False, args.maxlag, args.patience)
    check_offices(http)
    folks = people()
    ids = {}
    for p in folks:
        if not fetch.slug_ok(p["id"]):
            sys.exit("%r gives the id %r, which fetch.py refuses" % (p["name"], p["id"]))
        ids.setdefault(p["id"], []).append(p["name"])
    clash = {k: v for k, v in ids.items() if len(v) > 1}
    if clash:
        sys.exit("two names give one id: %s" % clash)
    items, missing, log = [], [], []
    by_qid = {}
    for p in folks:
        r = lookup(http, p)
        offices = "; ".join("%s %s to %s%s" % (t["title"], t["from"], "now" if t["open"] else t["to"],
                                               " (acting)" if t["acting"] else "") for t in p["tenures"])
        log.append({"id": p["id"], "name": p["name"], "offices": offices, **r})
        if not r["qid"]:
            missing.append((p, r))
            continue
        by_qid.setdefault(r["qid"], []).append(p["name"])
        it = {"id": p["id"], "kind": "person", "qid": r["qid"], "approved": APPROVED, "name": p["name"],
              "label": search_name(p["name"]), "hint": offices[:160], "confirmed": r["confirmed"]}
        it.update({"box": BOXES[p["id"]]} if p["id"] in BOXES else {"crop": CROP})
        items.append(it)
    os.makedirs(CACHE, exist_ok=True)
    with open(os.path.join(CACHE, "lookup.json"), "w", encoding="utf-8") as f:
        json.dump(log, f, indent=1, ensure_ascii=False, default=str)
        f.write("\n")
    with open(ITEMS, "w", encoding="utf-8") as f:
        json.dump(items, f, indent=1, ensure_ascii=False)
        f.write("\n")
    for it in items:
        print("ok    %-40s %-10s %s" % (it["id"], it["qid"], it["confirmed"][:110]))
    for p, r in missing:
        print("none  %-40s %s: %s" % (p["id"], p["name"], r["reason"]))
        for c in r["candidates"]:
            print("        candidate %s %s (strength %d, name %d): %s" % (c["qid"], c["label"], c["strength"], c["name"],
                                                                          c["note"][:100]))
    for q, names in by_qid.items():
        if len(names) > 1:
            print("note  %s is named %s in the data: one item (and one atlas cell) per name" % (q, " and ".join(names)))
    print("%d people; %d with a confirmed QID; %d without (listed above, left out) -> %s" % (
        len(folks), len(items), len(missing), os.path.relpath(ITEMS, ROOT)))


# ---------------------------------------------------------------------------------------------------------- the run

def politics_items():
    with open(ITEMS, encoding="utf-8") as f:
        return json.load(f)


def cmd_merge(args):
    with open(STORY_ITEMS, encoding="utf-8") as f:
        stories = json.load(f)
    pol = politics_items()
    clash = {it["id"] for it in stories} & {it["id"] for it in pol}
    if clash or any(it["id"].startswith(PREFIX) for it in stories):
        sys.exit("story and politics ids collide: %s" % sorted(clash))
    out = os.path.join(args.dir, "items.json")
    with open(out, "w", encoding="utf-8") as f:
        json.dump(stories + pol, f, indent=1, ensure_ascii=False)
        f.write("\n")
    print("%d story items and %d politics items -> %s" % (len(stories), len(pol), out))


def file_templates(http, files):
    """Commons file name -> the templates its page uses (namespace 10, every one, following continuations)."""
    out = {}
    files = sorted(set(files))
    for i in range(0, len(files), 10):
        batch = files[i:i + 10]
        cont = {}
        while True:
            params = {"action": "query", "formatversion": "2", "prop": "templates", "tlnamespace": "10",
                      "tllimit": "max", "redirects": "1", "titles": "|".join("File:" + f for f in batch)}
            params.update(cont)
            data = http.api(fetch.COMMONS_API, params, "commons")
            q = data.get("query", {})
            alias = {}
            for kind in ("normalized", "redirects"):
                for n in q.get(kind, []):
                    alias[n["to"]] = alias.get(n["from"], n["from"])
            for page in q.get("pages", []):
                title = alias.get(page["title"], page["title"])
                name = title[5:] if title.startswith("File:") else title
                have = out.setdefault(name, [])
                have += [t["title"][9:] for t in page.get("templates", []) if t["title"][9:] not in have]
            if "continue" not in data:
                break
            cont = dict(data["continue"])
    for f in files:
        if f not in out:
            out[f] = out.get(f.replace("_", " "), [])
    return out


def us_government(templates):
    return sorted(t for t in templates if t.startswith("PD-USGov"))


def public_domain(templates):
    """The public-domain templates on a Commons page other than the US government's (PD-US-not-renewed, PD-US-no-notice,
    PD-old-...), which say why a portrait that no government made is free to use."""
    # the page-layout helper and the templates' language subpages are not licence tags
    return sorted(t for t in templates if t.startswith("PD") and not t.startswith("PD-USGov") and t != "PD-Layout" and "/" not in t)


def cmd_licences(args):
    path = os.path.join(args.dir, "manifest.json")
    with open(path, encoding="utf-8") as f:
        man = json.load(f)
    http = fetch.Http(CACHE, args.offline, False, args.maxlag, args.patience)
    pol = [r for r in man["images"] if r["id"].startswith(PREFIX)]
    tpl = file_templates(http, [r["commons_file"] for r in pol if r.get("commons_file")])
    keep, dropped = [], {d["id"]: d for d in man.get("politics_dropped", [])}
    for r in man["images"]:
        if not r["id"].startswith(PREFIX):
            keep.append(r)
            continue
        why = None
        if r.get("approved") != APPROVED:
            why = "no approval recorded"
        elif not r.get("file"):
            why = "no image: %s" % r.get("reason", "none fetched")
        elif r.get("licence") != "Public domain":
            why = "licence %s, not public domain" % r.get("licence")
        else:
            templates = tpl.get(r["commons_file"], [])
            gov, other = us_government(templates), public_domain(templates)
            if not gov and not other:
                why = "no public-domain template on its Commons page"
            else:
                r["us_government"] = gov
                r["public_domain"] = other
        if why:
            dropped[r["id"]] = {"id": r["id"], "label": r.get("label"), "qid": r.get("qid"), "reason": why,
                                "licence": r.get("licence"), "commons_file": r.get("commons_file"),
                                "source_page": r.get("source_page")}
            continue
        dropped.pop(r["id"], None)
        keep.append(r)
    man["images"] = keep
    man["politics_dropped"] = list(dropped.values())
    man["politics_policy"] = ("politics portraits: public domain only (the licence and a PD template on the Commons page: "
                              "a US government work, or free for another reason the page states), "
                              "Docs/research/advantage/brief-v5.md")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(man, f, indent=1, ensure_ascii=False)
        f.write("\n")
    kept = [r for r in keep if r["id"].startswith(PREFIX)]
    for r in kept:
        print("keep  %-40s %-10s %s" % (r["id"], r["qid"], ", ".join(r["us_government"] + r["public_domain"])))
    for d in man["politics_dropped"]:
        print("drop  %-40s %-10s %s" % (d["id"], d.get("qid"), d["reason"][:120]))
    print("%d politics records: %d kept (public domain), %d dropped -> %s" % (
        len(pol), len(kept), len(man["politics_dropped"]), path))


# ---------------------------------------------------------------------------------------------------------- install

def rgba(path, size):
    r = subprocess.run(["ffmpeg", "-nostdin", "-hide_banner", "-loglevel", "error", "-i", path, "-f", "rawvideo",
                        "-pix_fmt", "rgba", "-"], capture_output=True, check=True)
    if len(r.stdout) != size * size * 4:
        sys.exit("%s is not a %d px RGBA page" % (path, size))
    return r.stdout


def cell_pixels(page, size, cell, col, row, span_):
    rows = []
    for y in range(row * cell, (row + 1) * cell):
        a = (y * size + col * cell) * 4
        rows.append(page[a:a + span_ * cell * 4])
    return b"".join(rows)


def sha256(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def credits_section(index, ids):
    lines = ["## Politics: the executive cascade, 1945-2026", "",
             "Written by `Tools/politics/portraits.py install` from the atlas index. Public-domain portraits of the "
             "presidents and officeholders of the politics view, approved by the user on 7 Nov 2026 "
             "(Docs/research/advantage/brief-v5.md): most are works of the US government; the rest are in the public "
             "domain for the reason their Commons pages state. The public-domain status is Wikimedia Commons' tagging.", ""]
    for k in ids:
        c = index["cells"][k]
        lines.append("- **%s** (`%s`): %s" % (c["label"], k, c["attribution"]))
    return lines + [""]


def cmd_install(args):
    new_dir = os.path.join(args.dir, "atlas")
    with open(os.path.join(new_dir, "index.json"), encoding="utf-8") as f:
        new = json.load(f)
    with open(os.path.join(ATLAS, "index.json"), encoding="utf-8") as f:
        old = json.load(f)
    with open(os.path.join(args.dir, "manifest.json"), encoding="utf-8") as f:
        man = json.load(f)
    problems = []
    if (new["cell"], new["page"]) != (old["cell"], old["page"]):
        sys.exit("the new atlas has %d px cells on %d px pages, the app's %d on %d" % (
            new["cell"], new["page"], old["cell"], old["page"]))
    cell, size, n = new["cell"], new["page"], new["grid"]
    place = ("g", "page", "slot", "col", "row", "span", "uv", "shape")
    story = [k for k in old["cells"] if not k.startswith(PREFIX)]
    moved = [k for k in story if k not in new["cells"] or any(new["cells"][k].get(p) != old["cells"][k].get(p)
                                                               for p in place)]
    problems += ["story cell %s moved or missing" % k for k in moved]
    for k, v in old.get("retired", {}).items():
        if not k.startswith(PREFIX) and new.get("retired", {}).get(k, {}).get("g") != v.get("g"):
            problems.append("retired cell %s not kept" % k)
    credit_changed = [k for k in story if k in new["cells"] and any(
        new["cells"][k].get(p) != old["cells"][k].get(p) for p in ("sha256", "attribution", "credit_short", "licence"))]
    # pixels of every story cell, old page against new
    pages_old, pages_new, repainted = {}, {}, []
    for k in story:
        if k in moved:
            continue
        c = old["cells"][k]
        pg = c["page"]
        if pg not in pages_old:
            pages_old[pg] = rgba(os.path.join(ATLAS, old["pages"][pg]), size)
            pages_new[pg] = rgba(os.path.join(new_dir, new["pages"][pg]), size)
        if cell_pixels(pages_old[pg], size, cell, c["col"], c["row"], c["span"]) != \
                cell_pixels(pages_new[pg], size, cell, c["col"], c["row"], c["span"]):
            repainted.append(k)
    items = {it["id"]: it for it in politics_items()}
    pol = [k for k in new["cells"] if k.startswith(PREFIX)]
    for k in pol:
        c = new["cells"][k]
        if k not in items:
            problems.append("%s is not a politics item" % k)
        if c.get("kind") != "person" or c.get("approved") != APPROVED:
            problems.append("%s is not an approved person (kind %s)" % (k, c.get("kind")))
        if c.get("licence") != "Public domain" or c.get("shape") != "circle":
            problems.append("%s: licence %s, shape %s" % (k, c.get("licence"), c.get("shape")))
    rec = {r["id"]: r for r in man["images"]}
    problems += ["%s: the manifest has no public-domain template for it" % k for k in pol
                 if not (rec.get(k, {}).get("us_government") or rec.get(k, {}).get("public_domain"))]
    if problems:
        for p in problems:
            print("PROBLEM", p)
        sys.exit("not installed: %d problems" % len(problems))
    if args.check:
        print("check only: story cells %d of %d in place, %d repainted %s, %d with a changed credit %s; %d politics "
              "cells, approved people in the public domain; pages %s" % (
                  len(story) - len(moved), len(story), len(repainted), repainted, len(credit_changed), credit_changed,
                  len(pol), ", ".join(new["pages"])))
        return
    # copy the pages and the index; a new page gets the import settings of actors-1.png with a fresh GUID
    with open(os.path.join(ATLAS, "actors-1.png.meta"), encoding="utf-8") as f:
        template = f.read()
    guids = set()
    for name in os.listdir(ATLAS):
        if name.endswith(".meta"):
            with open(os.path.join(ATLAS, name), encoding="utf-8") as f:
                m = re.search(r"^guid: ([0-9a-f]{32})$", f.read(), re.M)
            if m:
                guids.add(m.group(1))
    written = []
    for name in new["pages"]:
        shutil.copyfile(os.path.join(new_dir, name), os.path.join(ATLAS, name))
        meta = os.path.join(ATLAS, name + ".meta")
        if not os.path.exists(meta):
            guid = uuid.uuid4().hex
            while guid in guids:
                guid = uuid.uuid4().hex
            guids.add(guid)
            with open(meta, "w", encoding="utf-8") as f:
                f.write(re.sub(r"^guid: [0-9a-f]{32}$", "guid: " + guid, template, count=1, flags=re.M))
            written.append(name)
    shutil.copyfile(os.path.join(new_dir, "index.json"), os.path.join(ATLAS, "index.json"))
    # check what landed
    with open(os.path.join(ATLAS, "index.json"), encoding="utf-8") as f:
        landed = json.load(f)
    for name in landed["pages"]:
        p, m = os.path.join(ATLAS, name), os.path.join(ATLAS, name + ".meta")
        if not os.path.exists(p) or not os.path.exists(m) or sha256(p) != sha256(os.path.join(new_dir, name)):
            sys.exit("page %s or its meta did not land" % name)
    # credits: the stories' part as packed, then the politics section
    with open(os.path.join(new_dir, "CREDITS.md"), encoding="utf-8") as f:
        packed = f.read().split("\n")
    stories_part = [line for line in packed if "(`%s" % PREFIX not in line]
    while stories_part and stories_part[-1] == "":
        stories_part.pop()
    with open(CREDITS, encoding="utf-8") as f:
        before = f.read()
    cut = before.find("\n## Politics")
    before_stories = (before[:cut] if cut >= 0 else before).rstrip("\n")
    text = "\n".join(stories_part) + "\n\n" + "\n".join(credits_section(landed, pol))
    with open(CREDITS, "w", encoding="utf-8") as f:
        f.write(text)
    print("story cells: %d of %d in the same place%s; %d repainted %s; %d with a changed credit %s" % (
        len(story) - len(moved), len(story), "" if not moved else " (moved: %s)" % moved, len(repainted), repainted,
        len(credit_changed), credit_changed))
    print("politics cells: %d, all approved people in the public domain" % len(pol))
    print("pages: %s; new metas: %s" % (", ".join(landed["pages"]), ", ".join(written) or "none"))
    print("credits: %s (%d politics lines; the stories' part %s)" % (
        os.path.relpath(CREDITS, ROOT), len(pol),
        "unchanged" if "\n".join(stories_part) == before_stories else "updated from the pack"))


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    sub = ap.add_subparsers(dest="cmd")
    for name in ("items", "licences"):
        p = sub.add_parser(name)
        if name == "licences":
            p.add_argument("dir")
        p.add_argument("--maxlag", type=int, default=5, help="seconds; 0 omits it (only for a run someone waits on)")
        p.add_argument("--patience", type=float, default=600.0)
        p.add_argument("--offline", action="store_true", help="use only the cache")
    sub.add_parser("merge").add_argument("dir")
    p = sub.add_parser("install")
    p.add_argument("dir")
    p.add_argument("--check", action="store_true", help="run the checks and write nothing")
    args = ap.parse_args(sys.argv[1:] or ["items"])
    try:
        {"items": cmd_items, "merge": cmd_merge, "licences": cmd_licences, "install": cmd_install}[args.cmd](args)
    except (RuntimeError, fetch.OfflineMiss) as e:
        sys.exit("error: %s" % e)


if __name__ == "__main__":
    main()

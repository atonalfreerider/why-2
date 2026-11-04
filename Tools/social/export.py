"""Stage 3: assemble Assets/Resources/Data/social/people.json for both scenes.

Keeps people and groups (organizations such as brands, outlets and clubs are left out), with their lifespans, region,
category cloud, political role and offices, the economy companies they control, their follow ties, and a layout per
region: the GPU-FDG positions of the region's members, centered and scaled so the region's cloud has unit radius
(90th percentile). Links people who are also in figures.json.

Then it finishes the file (finish()), which needs neither the Wikidata nor the Jev cache and can run alone:
  names      display names without emoji and stylized letters (Unicode NFKC, symbols dropped); a name the scenes'
             font cannot draw (another script, or nothing left) takes the Wikidata English label of an entity whose
             Instagram username is the account's, else the drawable part of the name, else the handle; so does a name
             spelled out letter by letter with no wider gap between its words ("S O P H I E T U R N E R"), which closed
             up would run its words together (the label, else the handle)
  offices    current terms of the members of Congress in the file, from the unitedstates/congress-legislators
             dataset by Wikidata id, where Wikidata's own were missing or cut short; then a check of the year's
             officeholders
  followers  a check that flags accounts above @instagram, the most followed Instagram account: the graph's counts
             sum each person's Instagram, TikTok, YouTube and X followers, so a few exceed it

    python Tools/social/export.py            # assemble from the caches, finish, write
    python Tools/social/export.py --finish   # finish the shipped people.json in place
"""
import datetime
import json
import math
import os
import re
import sqlite3
import sys
import unicodedata
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
import resolve  # noqa: E402
import wikidata  # noqa: E402
from facts import DEFAULT_DB  # noqa: E402

FACTS = os.path.join(HERE, ".cache", "facts.json")
JUDGED = os.path.join(HERE, ".cache", "judgments.json")
LEGISLATORS = "https://unitedstates.github.io/congress-legislators/legislators-current.json"
LEGISLATORS_CACHE = os.path.join(HERE, ".cache", "legislators-current.json")
# The year the scenes call now: the officeholders check asks who holds office then.
SNAPSHOT_YEAR = 2025
FIGURES = os.path.join(ROOT, "Assets", "Resources", "Data", "figures.json")
OUT_DIR = os.path.join(ROOT, "Assets", "Resources", "Data", "social")
OUT = os.path.join(OUT_DIR, "people.json")
REGIONS = [
    {"id": "us", "name": "United States", "civs": ["united_states"]},
    {"id": "east_asia", "name": "East Asia", "civs": ["chinese", "japan"]},
    {"id": "south_asia", "name": "South Asia", "civs": ["indians"]},
    {"id": "europe", "name": "Europe", "civs": ["britain", "france", "holy_roman_empire", "spain", "denmark", "russia"]},
    {"id": "row", "name": "Rest of the world", "civs": []},
]


# Regional industries make stars of their own region whatever their passport (LISA of BLACKPINK is Thai).
CLOUD_REGION = {"k-pop": "east_asia", "bollywood": "south_asia"}
NOT_GOVERNMENT = ("elect", "candidate", "ambassador", "first lady", "order of the", "champion")
EXECUTIVE = ("president of", "prime minister", "chief minister", "governor", "mayor", "minister", "crown prince",
             "advisor to the president", "attorney general", "solicitor general", "district attorney")
LEGISLATIVE = ("senator", "representative", "lok sabha", "rajya sabha", "legislative assembly", "state senate",
               "parliament", "council member", "leader of the house")
NATIONAL = ("united states", "of india", "lok sabha", "rajya sabha", "prime minister", "of indonesia", "minister of",
            "deputy prime minister", "attorney general of the united", "associate deputy attorney general")


def government(office):
    """[label, from, to, branch e|l, level n|s] for a government office, else None (company and honorary titles)."""
    label, start, end = office
    low = (label or "").lower()
    if not low or any(w in low for w in NOT_GOVERNMENT):
        return None
    branch = "l" if any(w in low for w in LEGISLATIVE) else "e" if any(w in low for w in EXECUTIVE) else None
    if branch is None:
        return None
    level = "n" if any(w in low for w in NATIONAL) and "state senate" not in low and "chief minister" not in low else "s"
    return [label, start, end, branch, level]


def norm(t):
    return "".join(ch for ch in unicodedata.normalize("NFKD", (t or "").lower()) if ch.isalnum())


# What Liberation Sans, the font both scenes draw names in, covers: Latin, Greek, Cyrillic and Hebrew with their marks,
# and common punctuation. Anything else (emoji, other scripts) renders as an empty box.
DRAWABLE = ((0x20, 0x7E), (0xA0, 0x36F), (0x370, 0x3FF), (0x400, 0x51D), (0x591, 0x5F4), (0x1D00, 0x1EF9),
            (0x1F00, 0x1FFE), (0x2010, 0x2022), (0x2026, 0x2026), (0x2030, 0x2044))
# Emoji, symbols and the invisible characters that build them (joiners, variation selectors, skin tones, keycaps).
DROPPED = {"So", "Sk", "Cf", "Co", "Cs", "Cn", "Me"}


def drawable(ch):
    return any(a <= ord(ch) <= b for a, b in DRAWABLE)


def plain(name):
    """NFKC turns mathematical, fullwidth and script letters into plain ones and composes accents; symbols, joiners and
    variation selectors become spaces."""
    t = unicodedata.normalize("NFKC", name or "")
    return "".join(" " if unicodedata.category(ch) in DROPPED or 0xFE00 <= ord(ch) <= 0xFE0F else ch for ch in t)


def clean_name(name):
    """A display name without emoji or stylized letters (plain()), closed up and with spaces collapsed (tidy())."""
    return tidy(plain(name))


def run_together(name, letters=8):
    """Whether a name is spelled out letter by letter with no wider gap between its words ("S O P H I E T U R N E R"),
    at least `letters` long: closed up it would be one long word, and where its words break cannot be told. Shorter
    ones are taken for one word ("C A M I L O"); wider gaps set the words apart ("N I C O L E   A M A D O")."""
    t = plain(name).strip()
    parts = t.split()
    return len(parts) >= letters and all(len(c) == 1 for c in parts) and not re.search(r"\s{2,}", t)


def tidy(t):
    """Closes up letter-spaced words ("N I C O L E   A M A D O": words set apart by wider gaps), drops what removed
    characters leave behind (empty brackets, loose dots and separators) and collapses spaces."""
    words = [w.split() for w in re.split(r"\s{2,}", t.strip())]
    t = " ".join("".join(w) if len(w) > 1 and all(len(c) == 1 for c in w) else " ".join(w) for w in words)
    t = re.sub(r"[(\[]\s*[)\]]", "", t)
    t = re.sub(r"\s+\.+$", "", t)
    return re.sub(r"\s+", " ", t).strip(" -~|/•·_:;*=")


def legible(name, letters=2):
    """Whether the font draws every character and enough letters are left (one is a stage name: "V")."""
    return all(drawable(ch) for ch in name) and sum(ch.isalpha() for ch in name) >= letters


def finish_names(people):
    """Names the scenes can draw: cleaned of emoji and stylized letters; where that is not enough (another script, or
    nothing left), the Wikidata English label of an entity whose Instagram username is the account's, else the
    drawable part of the name, else the handle. A name that would run its words together (run_together()) takes the
    same label, else the handle. Returns the renamed (old, new) pairs."""
    cleaned = {id(p): clean_name(p["name"]) for p in people}
    together = {id(p) for p in people if run_together(p["name"])}
    unsure = [p for p in people if not legible(cleaned[id(p)]) or id(p) in together]
    labels = {}
    qids = sorted({p["q"] for p in unsure if p.get("q")})
    if qids:
        for q, e in wikidata.entities(qids).items():
            f = wikidata.facts(e) if "missing" not in e else None
            if f and f.get("label"):
                labels[q] = (f["label"], {h.lower() for h in f.get("instagram", [])})
    renamed = []
    for p in people:
        name = cleaned[id(p)]
        if id(p) in together or not legible(name):
            label, handles = labels.get(p.get("q"), (None, set()))
            label = clean_name(label) if label and p["handle"].lower() in handles else None
            # a name that runs its words together has no part to fall back on: where its words break cannot be told
            part = "" if id(p) in together else tidy("".join(ch if drawable(ch) else " " for ch in name))
            name = label if label and legible(label, 1) else part if legible(part, 1) else p["handle"]
        if name != p["name"]:
            renamed.append((p["name"], name))
            p["name"] = name
    return renamed


def legislators():
    """The members of Congress now in office (unitedstates/congress-legislators), downloaded once into the cache."""
    if not os.path.exists(LEGISLATORS_CACHE):
        os.makedirs(os.path.dirname(LEGISLATORS_CACHE), exist_ok=True)
        req = urllib.request.Request(LEGISLATORS, headers={"User-Agent": wikidata.AGENT})
        with urllib.request.urlopen(req, timeout=120) as r:
            body = r.read()
        with open(LEGISLATORS_CACHE, "wb") as f:
            f.write(body)
    with open(LEGISLATORS_CACHE, encoding="utf-8") as f:
        return json.load(f)


def holds(gov, year):
    return any(isinstance(g[1], int) and g[1] <= year <= (g[2] if isinstance(g[2], int) else 9999) for g in gov)


def finish_offices(people, today=None):
    """Fills the current members of Congress's terms (matched by Wikidata id) that Wikidata's offices lack or that the
    resolver cut short, so sitting legislators sit with the state rather than float among the famous. A term already
    covered year by year by an office of the same name is left alone; one still running ends null. Returns the
    (name, office, from, to) added and the problems the check finds: a member of Congress, or a US person whose
    description names a sitting office, with no office in SNAPSHOT_YEAR."""
    today = today or datetime.date.today().isoformat()
    by_q = {x["id"]["wikidata"]: x for x in legislators() if x.get("id", {}).get("wikidata")}
    added, problems = [], []
    for p in people:
        x = by_q.get(p.get("q"))
        if not x:
            continue
        for t in x["terms"]:
            office = "United States senator" if t["type"] == "sen" else "United States representative"
            start, end = int(t["start"][:4]), int(t["end"][:4])
            same = [g for g in p["gov"] if g[0] == office]
            if all(holds(same, y) for y in range(start, end)):
                continue
            term = [office, start, end if t["end"] <= today else None, "l", "n"]
            p["gov"].append(term)
            added.append((p["name"], office, term[1], term[2]))
        if not holds(p["gov"], SNAPSHOT_YEAR):
            problems.append(f"{p['name']} sits in Congress but holds no office in {SNAPSHOT_YEAR}")
    sitting = re.compile(r"(senator|representative|governor|mayor|president|vice president) (of|from)\b", re.I)
    for p in people:
        if p["region"] == "us" and p.get("desc") and sitting.search(p["desc"]) and not re.search(r"\bformer\b|\(\d{4}[–-]\d{4}\)|from \d{4} to \d{4}", p["desc"]) \
                and not holds(p["gov"], SNAPSHOT_YEAR) and p.get("q") not in by_q:
            problems.append(f"{p['name']} ('{p['desc']}') holds no office in {SNAPSHOT_YEAR}")
    return added, problems


def flag_above_instagram(people, db=DEFAULT_DB):
    """The accounts with more followers than @instagram, the most followed Instagram account, with what the graph's
    snapshot sums for them per platform (followers summed across Instagram, TikTok, YouTube and X)."""
    platform = next((p for p in people if p["handle"] == "instagram"), None)
    if platform is None:
        return ["@instagram is not in the file: nothing to check the follower counts against"]
    flagged, counts = [], {}
    if os.path.exists(db):
        c = sqlite3.connect("file:" + db + "?mode=ro", uri=True)
        for h, ig, tt, yt, x in c.execute("select lower(n.instagram_handle), p.instagram_followers, p.tiktok_followers, p.youtube_followers, p.x_followers "
                                          "from unity_graph_nodes n join person_popularity_snapshot p on p.person_id = n.person_id"):
            counts[h] = (ig, tt, yt, x)
    for p in people:
        if p is not platform and p["fol"] > platform["fol"]:
            parts = counts.get(p["handle"].lower())
            how = ", ".join(f"{k} {v / 1e6:.0f}M" for k, v in zip(("Instagram", "TikTok", "YouTube", "X"), parts or ()) if v) or "no per-platform counts"
            flagged.append(f"{p['name']} {p['fol'] / 1e6:.0f}M followers, above @instagram's {platform['fol'] / 1e6:.0f}M: the sum of {how}")
    return flagged


FOLLOWERS_NOTE = ("Follower counts are one snapshot of each account's followers summed across Instagram, TikTok, YouTube and X "
                  "(so a few, such as MrBeast's, exceed the @instagram account's own); they size people in every year shown.")
NAMES_NOTE = ("Names are drawable: emoji and stylized letters are dropped (NFKC); a name in a script the scenes' font cannot draw, "
              "or spelled out letter by letter with no wider gap between its words, takes the Wikidata English label of the "
              "entity with the account's Instagram username.")


def finish(out):
    """Finishes an assembled (or the shipped) people.json in place: names, offices, the follower check, the notes."""
    people = out["people"]
    renamed = finish_names(people)
    added, problems = finish_offices(people)
    flagged = flag_above_instagram(people)
    note = out.get("note", "")
    note = note.replace("Popular accounts from the social graph (Instagram follower counts and follow ties, GPU-FDG layout)",
                        "Popular accounts from the social graph (follower counts and Instagram follow ties, GPU-FDG layout)")
    note = note.replace("Follower counts are a 2025 snapshot; they size people in every year shown.", FOLLOWERS_NOTE)
    note = note.replace("Names are drawable: emoji and stylized letters are dropped (NFKC); a name in a script the scenes' font cannot "
                        "draw takes the Wikidata English label of the entity with the account's Instagram username.", NAMES_NOTE)
    for extra in (NAMES_NOTE, "Members of Congress have their current terms from the unitedstates/congress-legislators dataset."):
        if extra not in note:
            note += " " + extra
    out["note"] = note
    if "Congress terms: unitedstates/congress-legislators (CC0), legislators-current" not in out.get("sources", []):
        out.setdefault("sources", []).append("Congress terms: unitedstates/congress-legislators (CC0), legislators-current")
    print(f"names: {len(renamed)} cleaned or replaced")
    for old, new in renamed:
        if not legible(clean_name(old)) or run_together(old):
            print(f"  {old!r} -> {new!r}")
    print(f"offices: {len(added)} congressional terms added")
    for a in added:
        print("  ", *a)
    for p in problems:
        print("CHECK officeholders:", p, file=sys.stderr)
    for f in flagged:
        print("CHECK followers:", f, file=sys.stderr)
    return problems


def main():
    if "--finish" in sys.argv:
        out = json.load(open(OUT, encoding="utf-8"))
        finish(out)
        with open(OUT, "w", encoding="utf-8") as f:
            json.dump(out, f, ensure_ascii=False, separators=(",", ":"))
        print(f"finished {OUT}")
        return
    data = json.load(open(FACTS, encoding="utf-8"))
    judged = json.load(open(JUDGED, encoding="utf-8"))
    figures = {norm(f["name"]): f["id"] for f in json.load(open(FIGURES, encoding="utf-8"))["figures"]}
    facts, labels, clouds = data["facts"], data["labels"], data["clouds"]
    cloud_index = {c["id"]: k for k, c in enumerate(clouds)}
    people, index, dropped = [], {}, {"organization": 0}
    for n in data["nodes"]:
        a = judged.get(str(n["id"]), {})
        r = resolve.resolve(n, facts, labels, a, judged.get(f"{n['id']}:decade", {}).get("decade"), judged.get(f"{n['id']}:firm", {}).get("firm"))
        if r["kind"] == "organization":
            dropped["organization"] += 1
            continue
        name = n["name"]
        fig = figures.get(norm(name))
        region, basis = r["region"], r["regionBasis"]
        if n["cloud"] in CLOUD_REGION and region != CLOUD_REGION[n["cloud"]]:
            region, basis = CLOUD_REGION[n["cloud"]], "cloud"
        offices = [[o["office"], o["from"], o["to"]] for o in r["offices"]]
        gov = [g for g in (government(o) for o in offices) if g]
        index[n["id"]] = len(people)
        people.append({
            "name": name, "handle": n["handle"], "kind": r["kind"][0], "region": region, "regionBasis": basis,
            "born": r["born"], "died": r["died"], "est": r["bornEstimated"], "g": r["gender"], "fol": n["followers"],
            "cloud": cloud_index.get(n["cloud"], -1), "lead": n["leader"], "pol": r["political"] if r["political"] != "none" else "",
            "gov": gov, "firms": r["firms"], "fig": fig,
            "q": r["qid"], "desc": r["description"], "pos": n["pos"],
        })
    # One layout per region: the global FDG positions of its members, centered and scaled to unit radius.
    for reg in REGIONS:
        members = [p for p in people if p["region"] == reg["id"]]
        if not members:
            continue
        cx = [sum(p["pos"][k] for p in members) / len(members) for k in range(3)]
        radii = sorted(math.dist(p["pos"], cx) for p in members)
        scale = radii[int(.9 * (len(radii) - 1))] or 1
        for p in members:
            p["pos"] = [round((p["pos"][k] - cx[k]) / scale, 4) for k in range(3)]
        reg["count"] = len(members)
    edges = [[index[a], index[b], m] for a, b, m, _ in data["edges"] if a in index and b in index and a < b]
    os.makedirs(OUT_DIR, exist_ok=True)
    out = {
        "note": "Popular accounts from the social graph (follower counts and Instagram follow ties, GPU-FDG layout), "
                "resolved to Wikidata for lifespans, sex, citizenship and offices. Jev (TypeSafe) judged identity where the "
                "handle did not match, person / group / organization, region when citizenship did not settle it, political "
                "role, birth decade when Wikidata had none (est = true), and which economy company a capital holder runs. "
                + FOLLOWERS_NOTE + " Brands and outlets are left out. "
                "gov lists government offices only ([office, from, to, e(xecutive)|l(egislative), n(ational)|s(tate/local)]); "
                "company, honorary and goodwill-ambassador titles are dropped. K-pop and Bollywood stars belong to East and "
                "South Asia whatever their citizenship.",
        "sources": ["Social graph: SocialGraph/global_social_graph.db (social-graph + GPU-FDG pipeline)",
                    "Wikidata (CC0): P2003 Instagram username, P31, P569, P570, P571, P576, P21, P27, P106, P39 with P580/P582",
                    "Jev judgments: Tools/social/judge.py"],
        "built": datetime.date.today().isoformat(),
        "regions": REGIONS, "clouds": clouds, "people": people, "edges": edges,
    }
    finish(out)
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, separators=(",", ":"))
    by = {r["id"]: r.get("count", 0) for r in REGIONS}
    print(f"wrote {OUT}: {len(people)} people and groups ({dropped['organization']} organizations left out), {len(edges)} ties; {by}")


if __name__ == "__main__":
    main()

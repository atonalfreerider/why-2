"""Stage 3: assemble Assets/Resources/Data/social/people.json for both scenes.

Keeps people and groups (organizations such as brands, outlets and clubs are left out), with their lifespans, region,
category cloud, political role and offices, the economy companies they control, their follow ties, and a layout per
region: the GPU-FDG positions of the region's members, centered and scaled so the region's cloud has unit radius
(90th percentile). Links people who are also in figures.json.

    python Tools/social/export.py
"""
import datetime
import json
import math
import os
import sys
import unicodedata

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
import resolve  # noqa: E402

FACTS = os.path.join(HERE, ".cache", "facts.json")
JUDGED = os.path.join(HERE, ".cache", "judgments.json")
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


def main():
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
        "note": "Popular accounts from the social graph (Instagram follower counts and follow ties, GPU-FDG layout), "
                "resolved to Wikidata for lifespans, sex, citizenship and offices. Jev (TypeSafe) judged identity where the "
                "handle did not match, person / group / organization, region when citizenship did not settle it, political "
                "role, birth decade when Wikidata had none (est = true), and which economy company a capital holder runs. "
                "Follower counts are a 2025 snapshot; they size people in every year shown. Brands and outlets are left out. "
                "gov lists government offices only ([office, from, to, e(xecutive)|l(egislative), n(ational)|s(tate/local)]); "
                "company, honorary and goodwill-ambassador titles are dropped. K-pop and Bollywood stars belong to East and "
                "South Asia whatever their citizenship.",
        "sources": ["Social graph: SocialGraph/global_social_graph.db (social-graph + GPU-FDG pipeline)",
                    "Wikidata (CC0): P2003 Instagram username, P31, P569, P570, P571, P576, P21, P27, P106, P39 with P580/P582",
                    "Jev judgments: Tools/social/judge.py"],
        "built": datetime.date.today().isoformat(),
        "regions": REGIONS, "clouds": clouds, "people": people, "edges": edges,
    }
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, separators=(",", ":"))
    by = {r["id"]: r.get("count", 0) for r in REGIONS}
    print(f"wrote {OUT}: {len(people)} people and groups ({dropped['organization']} organizations left out), {len(edges)} ties; {by}")


if __name__ == "__main__":
    main()

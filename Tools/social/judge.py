"""Stage 2: Jev judgments that code cannot make from the graph and Wikidata alone.

One request per account, holding only the questions its facts leave open (independent questions over the same state):
  identity  Choice  which Wikidata candidate (if any) owns the account; skipped for exact Instagram-handle matches
  kind      Choice  person, group (band), or organization (brand, outlet, club, fan page); skipped when Wikidata knows
  region    Choice  which public made them famous (US, East Asia, South Asia, Europe, rest of world); skipped when
                    every citizenship points to one region
  political Choice  role in politics; only for accounts the graph or Wikidata marks as political
  firm:*    Noul    founds, runs or controls each living company of the economy scene; only for business people
A second pass asks the birth decade (Choice) for people Wikidata gives no birth year. Answers are cached per account in
Tools/social/.cache/judgments.json; reruns only ask what is missing.

    python Tools/social/judge.py            # needs a Jev credential (see Tools/jev.py)
"""
import json
import os
import sys
import threading
from concurrent.futures import ThreadPoolExecutor

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, os.path.join(ROOT, "Tools"))
sys.path.insert(0, HERE)
from jev import call_jev  # noqa: E402
import regions  # noqa: E402

FACTS = os.path.join(HERE, ".cache", "facts.json")
CACHE = os.path.join(HERE, ".cache", "judgments.json")
COMPANIES = os.path.join(ROOT, "Assets", "Resources", "Data", "economy", "company-lives.json")
LOCK = threading.Lock()
BUSINESS = ("business", "entrepreneur", "investor", "executive", "founder", "industrialist", "engineer", "magnate", "financier")
POLITICAL = ("politician", "political", "activist", "commentator", "lawyer", "diplomat", "statesperson", "head of state")

KIND = {
    "type": "choice",
    "instructions": "Who or what runs the social-media account `account`: an individual person, a group of people who "
                    "perform or create together under one name, or an organization?",
    "criteria": {
        "person": "One individual human being (a celebrity, athlete, creator, politician or business person), even if a team posts for them",
        "group": "A band, duo, idol group, comedy troupe or creator collective known under a shared name",
        "organization": "A brand, company, product, media outlet, sports club or league, charity, or a fan, meme or aggregator page",
    },
}
REGION = {
    "type": "choice",
    "instructions": "Which region's public made the owner of `account` famous and follows them most? Use their "
                    "citizenship, career and audience in `wikidata` and `account.bio`; a foreign-born star of US film or "
                    "music counts as the United States, a footballer counts where their national team and home audience are.",
    "criteria": {
        "us": "The United States",
        "east_asia": "East Asia: China, Japan, South Korea, Taiwan, Hong Kong or Mongolia",
        "south_asia": "South Asia: India, Pakistan, Bangladesh, Sri Lanka or Nepal",
        "europe": "Europe, including the United Kingdom and Russia",
        "row": "Anywhere else: Latin America, Canada, Africa, the Middle East, Southeast Asia or Oceania",
    },
}
POLITICS = {
    "type": "choice",
    "instructions": "What is the political role of the owner of `account` today, given `wikidata` and `account.bio`?",
    "criteria": {
        "head": "Holds or has held the top office of a country: president, prime minister or monarch with power",
        "elected": "Holds or has held another elected office: legislator, governor, mayor or vice president",
        "appointed": "Holds or has held an appointed government post: cabinet minister, judge, agency head or diplomat",
        "party": "A party leader, candidate or organizer who has not held office",
        "commentator": "A political commentator, podcaster, writer or activist without a government role",
        "none": "No political role; politics is not what they are known for",
    },
}
DECADE = {
    "type": "choice",
    "instructions": "In which decade was the person who owns `account` born?",
    "criteria": {f"d{y}": f"The {y}s" for y in range(1920, 2020, 10)},
}


def identity_question(candidates):
    crit = {f"c{k}": c for k, c in enumerate(candidates)}
    crit["none"] = "None of these entities owns the account, or the account belongs to a brand, outlet, club or fan page"
    return {"type": "choice", "instructions": "Which entity in `wikidata` is the owner of the social-media account "
                                              "`account`? Match on name, description, occupation and the account's bio and audience.",
            "criteria": crit}


CAPITAL = {"type": "noul", "instructions": "Does the owner of `account` found, lead as chief executive or chair, or hold a "
                                            "controlling or major ownership stake in a large company, beyond their own "
                                            "personal brand, merchandise line or endorsement deals?"}


def firm_question(companies):
    crit = {c["id"]: f"{c['name']}: {c['sells']}" for c in companies}
    crit["none"] = "None of these companies: their company is not listed, or they only endorse, advertise or hold small stakes"
    return {"type": "choice", "instructions": "Which of these companies does the owner of `account` found, lead as chief "
                                              "executive or chair, or control as a major owner? If several, pick the one "
                                              "they are most identified with.", "criteria": crit}


def trusted_exact(node, entity):
    """Wikidata's handle claims are sometimes wrong: keep an exact match only when its name agrees with the account."""
    import difflib
    import unicodedata
    norm = lambda t: "".join(ch for ch in unicodedata.normalize("NFKD", (t or "").lower()) if ch.isalnum() or ch == " ")
    a, b = norm(node["name"]), norm(entity.get("label") or "")
    if not b:
        return True
    if difflib.SequenceMatcher(None, a, b).ratio() >= 0.6:
        return True
    ta, tb = {w for w in a.split() if len(w) >= 3}, {w for w in b.split() if len(w) >= 3}
    return bool(ta & tb)


def describe(f, labels):
    if not f:
        return None
    lab = lambda xs: [labels.get(x, x) for x in xs][:4]
    out = {"name": f["label"], "description": f["description"], "is_human": f["human"], "is_group": f["group"]}
    for k in ("born", "died", "founded"):
        if f.get(k):
            out[k] = f[k]
    if f["citizenship"]:
        out["citizenship"] = lab(f["citizenship"])
    if f["occupation"]:
        out["occupations"] = lab(f["occupation"])
    if f["positions"]:
        out["positions_held"] = [labels.get(p["position"], p["position"]) for p in f["positions"]][:5]
    return out


def plan(node, facts, labels, companies):
    """(state, questions) for one account; empty questions when code already knows everything."""
    exact = facts.get(node["exact"]) if node.get("exact") else None
    if exact is not None and not trusted_exact(node, exact):
        node["candidates"] = [node["exact"]] + [q for q in node["candidates"] if q != node["exact"]]
        node["exact"], exact = None, None
    cands = [] if exact else [facts[q] for q in node["candidates"] if q in facts][:5]
    pool = [exact] if exact else cands
    account = {"name": node["name"], "instagram": "@" + node["handle"], "followers": node["followers"],
               "bio": node["bio"], "graph_category": node["category"], "graph_cloud": node["cloud"]}
    state = {"account": account, "wikidata": [describe(c, labels) for c in pool]}
    q = {}
    if cands:
        q["identity"] = identity_question([f"{c['label']} — {c['description'] or 'no description'}" + (f" (born {c['born']})" if c.get("born") else "")
                                           for c in cands])
    if not exact or not (exact["human"] or exact["group"]):
        q["kind"] = KIND
    regs = set()
    for c in pool:
        regs |= {regions.of(labels.get(x, "")) for x in c["citizenship"] or c["country"]}
    if len(regs - {None}) != 1:
        q["region"] = REGION
    occ = " ".join(labels.get(x, "") for c in pool for x in c["occupation"]).lower()
    political = node["category"] in ("politician", "political_influencer") or node["cloud"] in ("maga", "liberal") \
        or any(c["positions"] for c in pool) or any(w in occ for w in POLITICAL)
    if political:
        q["political"] = POLITICS
    business = node["cloud"] == "tech" or any(w in occ for w in BUSINESS) or any(c["owner_of"] for c in pool)
    if business:
        q["capital"] = CAPITAL
    return state, q


def main():
    with open(FACTS, encoding="utf-8") as f:
        data = json.load(f)
    facts, labels = data["facts"], data["labels"]
    companies = [c for c in json.load(open(COMPANIES, encoding="utf-8"))["companies"] if c.get("fate") == "alive"]
    cache = json.load(open(CACHE, encoding="utf-8")) if os.path.exists(CACHE) else {}
    usage = {"requests": 0}

    def run(node, extra=None):
        key = str(node["id"]) + (":" + extra if extra else "")
        if key in cache:
            return
        if extra == "decade":
            state, q = {"account": {"name": node["name"], "instagram": "@" + node["handle"], "bio": node["bio"]}}, {"decade": DECADE}
        elif extra == "firm":
            state, _ = plan(node, facts, labels, companies)
            q = {"firm": firm_question(companies)}
        else:
            state, q = plan(node, facts, labels, companies)
        answers = {}
        if q:
            for attempt in range(3):
                try:
                    answers = call_jev(state, q)
                    break
                except Exception as e:  # noqa: BLE001
                    if attempt == 2:
                        print(f"  {node['name']}: {e}", file=sys.stderr)
                        return
        with LOCK:
            cache[key] = answers
            usage["requests"] += 1 if q else 0
            if len(cache) % 50 == 0:
                json.dump(cache, open(CACHE, "w", encoding="utf-8"))
                print(f"  {len(cache)} accounts judged", flush=True)

    nodes = data["nodes"]
    with ThreadPoolExecutor(max_workers=6) as pool:
        list(pool.map(run, nodes))
    json.dump(cache, open(CACHE, "w", encoding="utf-8"))
    # Second pass: birth decade for people still without a birth year; which listed company the capital holders run.
    import resolve  # noqa: E402
    missing = [n for n in nodes if (r := resolve.resolve(n, facts, labels, cache.get(str(n["id"]), {})))["kind"] == "person" and not r["born"]]
    owners = [n for n in nodes if cache.get(str(n["id"]), {}).get("capital", {}).get("noul", 0) >= 0.5]
    print(f"birth decade needed for {len(missing)} people; company choice for {len(owners)} capital holders", flush=True)
    with ThreadPoolExecutor(max_workers=6) as pool:
        list(pool.map(lambda n: run(n, "decade"), missing))
        list(pool.map(lambda n: run(n, "firm"), owners))
    json.dump(cache, open(CACHE, "w", encoding="utf-8"))
    print(f"done: {usage['requests']} Jev requests this run, {len(cache)} cached answers")


if __name__ == "__main__":
    main()

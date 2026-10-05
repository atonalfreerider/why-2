"""Judges every company in company-lives.json with Jev and stores the judgments for the economy wave.

The scene never calls a model: it reads Assets/Resources/Data/economy/company-judgments.json, written here. Each
company gets one request with five independent questions over the same state (what it sells, when it lived, how it
ended): which industry hill it stands on (a Choice over the 23 non-government industries, with a no-match option), and
four Scores on how its revenue is won, matching the household purchase judgments (Tools/judge-purchases.py):
manufactured want, fear sold, captive buyer and habit loop. Code colors each company's lifeline by these judgments
and keeps the authored industry when Jev's hill choice is unsure. They are model evidence about selling practices,
not measurements.

Usage (from the repository root; the key stays in this process and is never printed):
    export TYPESAFE_API_KEY=...        # a direct TypeSafe key, or AI_GATEWAY_API_KEY for Vercel AI Gateway
    python3 Tools/judge-companies.py   # --sample judges six contrasting companies into the temp folder
"""
import datetime
import json
import os
import sys
import tempfile
import time
import urllib.error
import urllib.request
from concurrent.futures import ThreadPoolExecutor

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "Assets", "Resources", "Data", "economy")
COMPANIES = os.path.join(DATA, "company-lives.json")
INDUSTRIES = os.path.join(DATA, "industries.json")
OUT = os.path.join(DATA, "company-judgments.json")

SCORES = {
    "manufactured": {
        "type": "score",
        "instructions": "Over `company.name`'s life as described, how much of its revenue came from wants that it or its "
                        "industry created or inflated (advertising, branding, status, novelty, planned obsolescence), "
                        "rather than from needs its buyers would have had without any marketing?",
        "criteria": [
            "It sold what buyers needed anyway (fuel, steel, basic food, utilities); marketing only decided which seller got the money",
            "Mostly need-driven, but it upsold size, frequency or premium versions",
            "About half need and half a want shaped by advertising or social comparison",
            "Mostly a want sustained by its marketing, brand image or status; buyers could forgo most of it without harm",
            "Demand existed chiefly because it created it: image, novelty or social pressure was the product",
        ],
    },
    "fear_sold": {
        "type": "score",
        "instructions": "How much did `company.name` win its revenue by selling protection from fear or anxiety (illness, "
                        "injury, loss, danger, ruin, falling behind) that it presented or amplified to close the sale?",
        "criteria": [
            "Fear played no part; customers bought for enjoyment, convenience, production or routine",
            "A background worry existed but it rarely used it to sell",
            "It regularly invoked safety, risk or worry alongside other appeals",
            "Its sales were mainly made by pointing at a threat customers wanted to avoid",
            "Customers bought under acute threat or dread, so fear decided the purchase and its price",
        ],
    },
    "captive": {
        "type": "score",
        "instructions": "How little real freedom did `company.name`'s typical customer have to refuse, delay or switch to "
                        "another seller: how much pricing power did it hold over them?",
        "criteria": [
            "Easily skipped or substituted; many competitors kept its prices in check",
            "Hard to skip, but strong competition kept prices in check",
            "Switching was costly or confusing (contracts, ecosystems, opaque pricing, few local options)",
            "Customers depended on it or a few rivals, faced lock-in, or could not compare prices before paying",
            "Customers could not refuse: a monopoly, a legal requirement or an emergency set the terms",
        ],
    },
    "habit": {
        "type": "score",
        "instructions": "How much of `company.name`'s repeat revenue was sustained by habit-forming or compulsive design "
                        "(addictive substances, variable rewards, auto-renewal, engagement loops) rather than a fresh "
                        "decision each time?",
        "criteria": [
            "Each purchase was a deliberate, occasional decision",
            "Routine repeat buying out of convenience, easily stopped",
            "Subscriptions, auto-renewal or brand habit kept the spending going by default",
            "Its products were designed to be compulsive and many customers spent more than they intended",
            "Chemically or psychologically addictive; much of its revenue came from dependent heavy users",
        ],
    },
}


def hill_question(industries):
    criteria = {}
    for ind in industries:
        if ind["tier"] == "gov":
            continue
        blurb = ind.get("blurb", "").split(":")[0]
        criteria[ind["id"]] = f"{ind['name']}: {blurb}"
    criteria["none"] = "None of these: a government agency, a nonprofit, or a business that fits no listed industry"
    return {
        "type": "choice",
        "instructions": "Which industry earned most of `company.name`'s revenue, judging by what it sells "
                        "(`company.sells`) over its life? Pick the industry of its main revenue, not of its suppliers "
                        "or customers.",
        "criteria": criteria,
    }


def call_jev(state, questions):
    key = os.environ.get("TYPESAFE_API_KEY") or os.environ.get("AI_GATEWAY_API_KEY")
    if not key:
        raise SystemExit("Set TYPESAFE_API_KEY (or AI_GATEWAY_API_KEY) in this process before calling Jev.")
    direct = bool(os.environ.get("TYPESAFE_API_KEY"))
    url = "https://api.typesafe.ai/v1/systemone" if direct else "https://ai-gateway.vercel.sh/typesafe/v1/systemone"
    body = json.dumps({"model": "jev-latest" if direct else "typesafe-ai/jev", "state": state, "questions": questions}).encode()
    request = urllib.request.Request(url, data=body, headers={"Authorization": "Bearer " + key, "Content-Type": "application/json"})
    for attempt in range(3):
        try:
            with urllib.request.urlopen(request, timeout=90) as response:
                result = json.load(response)
            if result.get("answers"):
                return result
        except urllib.error.HTTPError as e:  # never print headers, the key or the raw server error
            print(f"{state['company']['name']}: HTTP {e.code}", file=sys.stderr)
        except (urllib.error.URLError, TimeoutError) as e:
            print(f"{state['company']['name']}: {type(e).__name__}", file=sys.stderr)
        time.sleep(2 * (attempt + 1))
    return None


def main():
    companies = json.load(open(COMPANIES, encoding="utf-8"))["companies"]
    industries = json.load(open(INDUSTRIES, encoding="utf-8"))["industries"]
    names = {i["id"]: i["name"] for i in industries}
    questions = dict(SCORES)
    questions["hill"] = hill_question(industries)
    out_path = OUT
    if "--sample" in sys.argv:
        pick = {"altria", "enron", "unitedhealth", "nvidia", "blockbuster", "meta"}
        companies = [c for c in companies if c["id"] in pick]
        out_path = os.path.join(tempfile.gettempdir(), "company-judgments-sample.json")

    def state_of(c):
        life = f"founded {c['founded']}"
        life += f", ended {c['ended']} ({c['fate']})" if c.get("ended") else ", still operating"
        if c.get("event"):
            life += f"; {c['event']}"
        return {"company": {"name": c["name"], "sells": c["sells"], "life": life, "country": "United States"}}

    jobs = [(c, state_of(c)) for c in companies]
    print(f"{len(jobs)} companies x {len(questions)} questions", flush=True)
    with ThreadPoolExecutor(max_workers=6) as pool:
        results = list(pool.map(lambda job: call_jev(job[1], questions), jobs))
    failed = [c["id"] for (c, _), r in zip(jobs, results) if r is None]
    if failed:
        print("failed: " + ", ".join(failed), file=sys.stderr)
        sys.exit(1)
    rows, model, tokens, disagree = {}, None, 0, []
    for (c, _), answer in zip(jobs, results):
        model = answer.get("model", model)
        tokens += sum(answer.get("usage", {}).values())
        a = answer["answers"]
        row = {"name": c["name"]}
        for q, spec in SCORES.items():
            row[q] = round(a[q]["score"] / (len(spec["criteria"]) - 1), 4)  # zero-based level index -> 0..1
            row[q + "Confidence"] = round(a[q].get("confidence", 0), 3)
        probs = sorted(a["hill"]["probabilities"].items(), key=lambda kv: -kv[1])
        row["hill"] = a["hill"]["choice"]
        row["hillConfidence"] = round(a["hill"].get("confidence", 0), 3)
        row["hillTop"] = {k: round(v, 3) for k, v in probs[:3]}
        if row["hill"] != c["industry"]:
            disagree.append(f"  {c['name']}: authored {names.get(c['industry'])}, Jev {names.get(row['hill'], row['hill'])} ({row['hillConfidence']:.2f})")
        rows[c["id"]] = row
    out = {
        "note": "Jev (TypeSafe System One) judgments of each company in company-lives.json, one request per company from "
                "Tools/judge-companies.py. Scores are 0..1 (probability-weighted level / 4). The hill is Jev's choice of "
                "the industry earning most of the company's revenue; the scene uses it only when its confidence is at "
                "least 0.6 and otherwise keeps the authored industry. These are model evidence about how each company "
                "sells, not measurements.",
        "model": model,
        "judged": datetime.date.today().isoformat(),
        "tokens": tokens,
        "questions": questions,
        "companies": rows,
    }
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=1, ensure_ascii=False)
    print(f"wrote {out_path}: {len(rows)} companies, model {model}, {tokens} tokens")
    if disagree:
        print("hill choices that differ from the authored industry:")
        print("\n".join(disagree))


if __name__ == "__main__":
    main()

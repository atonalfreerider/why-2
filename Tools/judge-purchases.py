"""Scores every household purchase item in spending.json with Jev and stores the judgments for the economy scene.

The scene never calls a model: it reads Assets/Resources/Data/economy/purchase-judgments.json, written here. Each item
is judged on four independent dimensions of how the sale is made (manufactured want, fear sold, captive buyer, habit
loop) and one primary motive. Code combines these judgments with the measured input-output capture; the judgments are
model evidence about how purchases are sold, not measurements of anyone's mind.

Usage (PowerShell, from the repository root; the key stays in this process):
    $env:TYPESAFE_API_KEY = (Get-Content <key file> -Raw).Trim()   # or AI_GATEWAY_API_KEY
    python Tools/judge-purchases.py
    Remove-Item Env:TYPESAFE_API_KEY
"""
import datetime
import json
import os
import subprocess
import sys
import tempfile
from concurrent.futures import ThreadPoolExecutor

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SPENDING = os.path.join(ROOT, "Assets", "Resources", "Data", "economy", "spending.json")
INDUSTRIES = os.path.join(ROOT, "Assets", "Resources", "Data", "economy", "industries.json")
OUT = os.path.join(ROOT, "Assets", "Resources", "Data", "economy", "purchase-judgments.json")

QUESTIONS = {
    "manufactured": {
        "type": "score",
        "instructions": "How much of US households' spending on `item.name` exists because sellers create or inflate "
                        "the want (advertising, branding, status signaling, upselling, planned obsolescence), rather "
                        "than because of a need buyers would have without any marketing?",
        "criteria": [
            "Bought to meet a basic physical need; marketing only changes which seller gets the money, not how much is spent",
            "Mostly need-driven, but sellers upsell size, frequency or premium tiers",
            "About half need and half a want shaped by advertising or social comparison",
            "Mostly a want sustained by marketing, branding or status; buyers could forgo most of it without harm",
            "Demand exists chiefly because sellers create it: image, novelty or social pressure is the product",
        ],
    },
    "fear_sold": {
        "type": "score",
        "instructions": "How much is spending on `item.name` driven by fear or anxiety (illness, injury, loss, "
                        "danger, ruin, being left behind) that sellers present or amplify to close the sale?",
        "criteria": [
            "Fear plays no part; people buy it for enjoyment, convenience or routine",
            "A background worry exists but sellers rarely use it",
            "Sellers regularly invoke safety, risk or worry alongside other appeals",
            "The sale is mainly made by pointing at a threat the buyer wants to avoid",
            "Bought under acute threat or dread, where fear decides the purchase and its price",
        ],
    },
    "captive": {
        "type": "score",
        "instructions": "How little real freedom does a typical buyer have to refuse, delay, or switch sellers for "
                        "`item.name`: how much pricing power do sellers hold over this purchase?",
        "criteria": [
            "Easily skipped or substituted; many sellers compete on price",
            "Hard to skip, but many competing sellers keep prices in check",
            "Switching is costly or confusing (contracts, opaque pricing, few local options)",
            "Buyers depend on few sellers, face lock-in, or cannot compare prices before paying",
            "Buyers cannot refuse: emergencies, legal requirements, or a local monopoly set the terms",
        ],
    },
    "habit": {
        "type": "score",
        "instructions": "How much is repeat spending on `item.name` sustained by habit-forming or compulsive design "
                        "(addictive substances, variable rewards, auto-renewal, engagement loops) rather than a "
                        "fresh decision each time?",
        "criteria": [
            "Each purchase is a deliberate, occasional decision",
            "Routine repeat buying out of convenience, easily stopped",
            "Subscriptions, auto-renewal or brand habit keep the spending going by default",
            "Products are designed to be compulsive and many buyers spend more than they intend",
            "Chemically or psychologically addictive; a large share of revenue comes from dependent heavy users",
        ],
    },
    "motive": {
        "type": "choice",
        "instructions": "Which motive most often decides a US household's spending on `item.name`?",
        "criteria": {
            "need": "Meeting a basic physical need: food, shelter, warmth, getting to work",
            "comfort": "Convenience or comfort beyond the need",
            "belonging": "Belonging, care for others, or a group the buyer is part of",
            "status": "Rank, attractiveness or being seen to keep up",
            "escape": "Feeling good now, relief from stress, boredom or loneliness",
            "safety": "Protection from illness, loss, danger or ruin",
            "future": "Building skills, assets or income for later",
        },
    },
}


def main():
    spending = json.load(open(SPENDING, encoding="utf-8"))
    names = {i["id"]: i["name"] for i in json.load(open(INDUSTRIES, encoding="utf-8"))["industries"]}
    jobs = []
    for category in spending["categories"]:
        rows = list(category.get("items", []))
        for extra in category.get("outsidePce", []) or []:
            rows.append({"id": extra["id"], "name": extra["name"], "pce": extra.get("usd"), "industries": extra.get("industries", {})})
        for item in rows:
            sellers = sorted(item.get("industries", {}).items(), key=lambda kv: -kv[1])[:3]
            state = {
                "item": {
                    "name": item["name"],
                    "annual_us_household_spending_billions": item.get("pce"),
                    "category": category["name"],
                    "category_description": category.get("blurb", ""),
                    "main_selling_industries": [names.get(k, k) for k, v in sellers if v > 0.02],
                }
            }
            jobs.append((category["id"], item["id"], item["name"], state))
    out_path = OUT
    if "--sample" in sys.argv:  # a quick look at a few contrasting items, written beside the system temp folder
        pick = {"rent_basic", "tobacco", "health_insurance", "jewelry_watches", "casinos", "college", "video_games_software", "food_fruit_veg"}
        jobs = [j for j in jobs if j[1] in pick] or jobs[:6]
        out_path = os.path.join(tempfile.gettempdir(), "purchase-judgments-sample.json")
    print(f"{len(jobs)} items x {len(QUESTIONS)} questions", flush=True)
    with ThreadPoolExecutor(max_workers=6) as pool:
        results = list(pool.map(judge, jobs))
    failed = [j[1] for j, r in zip(jobs, results) if r is None]
    if failed:
        print("failed: " + ", ".join(failed), file=sys.stderr)
        sys.exit(1)
    items, model, tokens = {}, None, 0
    for (cat, iid, name, _), answer in zip(jobs, results):
        model = answer.get("model", model)
        tokens += sum(answer.get("usage", {}).values())
        a = answer["answers"]
        row = {"category": cat, "name": name}
        for q in ("manufactured", "fear_sold", "captive", "habit"):
            row[q] = round(a[q]["score"] / 4.0, 4)
            row[q + "Confidence"] = round(a[q].get("confidence", 0), 3)
        row["motive"] = a["motive"]["choice"]
        row["motiveProbabilities"] = {k: round(v, 3) for k, v in a["motive"]["probabilities"].items()}
        row["motiveConfidence"] = round(a["motive"].get("confidence", 0), 3)
        items[iid] = row
    out = {
        "note": "Jev (TypeSafe System One) judgments of how each household purchase is sold, one request per item from "
                "Tools/judge-purchases.py. Scores are 0..1 (probability-weighted level / 4). They are model evidence "
                "about selling practices, not measurements of anyone's motives; the scene combines them with the "
                "measured input-output capture in code and labels them as judgments.",
        "model": model,
        "judged": datetime.date.today().isoformat(),
        "tokens": tokens,
        "questions": QUESTIONS,
        "items": items,
    }
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=1, ensure_ascii=False)
    print(f"wrote {out_path}: {len(items)} items, model {model}, {tokens} tokens")


def judge(job):
    _, iid, _, state = job
    request = {"state": state, "questions": QUESTIONS}
    fd, path = tempfile.mkstemp(suffix=".json", prefix="jev-")
    with os.fdopen(fd, "w", encoding="utf-8") as f:
        json.dump(request, f)
    try:
        for _attempt in range(3):
            run = subprocess.run(["powershell", "-NoProfile", "-File", os.path.join(ROOT, "Tools", "jev.ps1"),
                                  "-RequestPath", path], capture_output=True, text=True, encoding="utf-8")
            if run.returncode == 0:
                return json.loads(run.stdout)
        print(f"{iid}: {run.stderr.strip().splitlines()[0] if run.stderr.strip() else 'failed'}", file=sys.stderr)
        return None
    finally:
        os.remove(path)


if __name__ == "__main__":
    main()


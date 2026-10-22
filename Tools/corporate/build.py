#!/usr/bin/env python3
"""Builds Assets/Resources/Data/economy/corporate-programs.json: what the economy's largest companies spend, which way
the money pushes them, and the economy-wide totals the scene sets them against.

Inputs (tracked, with a source for every number):
  Tools/corporate/filings.json   one record per stored firm (circuit.json capture): its fiscal year's uses of money from
                                 the audited 10-K / annual report / IPO prospectus on SEC EDGAR (cost of revenue, R&D,
                                 SG&A, advertising where disclosed, capex and finance leases, buybacks, dividends,
                                 acquisitions, stakes in other companies, cash taxes, stock pay, headcount), its 2025
                                 federal lobbying (Senate LDA filings), and a paraphrase of where it says the money goes.
  Tools/corporate/economy.json   US totals for 2025: advertising (WPP Media, IAB/PwC), private fixed investment by type
                                 (BEA NIPA 5.3.5), business R&D (NSF), payouts (S&P DJI), lobbying (OpenSecrets), M&A,
                                 hyperscaler capex and guidance, data-center construction (Census).

Jev (TypeSafe System One) answers three questions per firm over the same state (its numbers and its own words):
  direction  a Choice: which way its money moves it in the hierarchy of layers (climb into software / platforms / AI,
             reach down into its own supply, spread across its own layer, lean on the state, or harvest cash for owners);
             the probability spread is kept, so a firm that does two things shows both
  defensive  a Score: how much its investment is driven by fear of losing what it has rather than by desire for new demand
  ai         a Score: how much of its investment goes to artificial intelligence
Code keeps the arithmetic (totals, shares, the split of a firm's costs between suppliers and workers by its industry's
accounts happens in the scene). The judgments are model evidence about strategy, not measurements.

Usage (from the repository root):
    python3 Tools/corporate/build.py            # calls Jev for anything not cached, then writes the JSON
    python3 Tools/corporate/build.py --offline  # fail instead of calling Jev
Jev answers are cached under Tools/corporate/.cache (gitignored); the credential is found by Tools/jev.py and never printed.
"""
import datetime
import hashlib
import json
import os
import sys
from concurrent.futures import ThreadPoolExecutor

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
FILINGS = os.path.join(HERE, "filings.json")
ECONOMY = os.path.join(HERE, "economy.json")
CIRCUIT = os.path.join(ROOT, "Assets", "Resources", "Data", "economy", "circuit.json")
INDUSTRIES = os.path.join(ROOT, "Assets", "Resources", "Data", "economy", "industries.json")
OUT = os.path.join(ROOT, "Assets", "Resources", "Data", "economy", "corporate-programs.json")
CACHE = os.path.join(HERE, ".cache", "judgments.json")
sys.path.insert(0, os.path.join(ROOT, "Tools"))
import jev  # noqa: E402

OFFLINE = "--offline" in sys.argv

DIRECTION = {
    "type": "choice",
    "instructions": "The economy can be read as layers standing on one another: raw materials and energy at the bottom, then "
                    "manufacturing, construction and logistics, then services, with software, internet platforms, artificial "
                    "intelligence and attention on top. Judging by where `company.name` put its money in `company.fiscal_period` "
                    "(`company.uses_of_money_billion_usd`) and by what it says it is doing (`company.what_it_says`), which way is "
                    "its money moving it?",
    "criteria": {
        "climb": {
            "description": "Moving up: its money builds a higher layer than the one it mainly earns from today, so it will sell "
                           "software, platforms, data, AI models or access to attention rather than goods, labor or capacity",
            "examples": ["a retailer building a cloud, advertising and AI business", "a carmaker presenting itself as an AI and robotics company",
                         "a chipmaker funding AI model makers and software"],
        },
        "integrate_down": {
            "description": "Reaching down: its money buys or builds the supply beneath it, such as chips, data centers, power, "
                           "factories, materials or logistics, so it depends less on suppliers",
            "examples": ["a software company building its own data centers and power contracts", "a phone maker designing its own chips and servers"],
        },
        "expand_across": {
            "description": "Spreading across its own layer: more customers, products or territory in the business it already has, "
                           "or buying rivals, with no move up or down the layers",
            "examples": ["an oil company buying another oil producer", "a bank opening branches and widening lending", "a drug maker launching new medicines"],
        },
        "lean_on_state": {
            "description": "Leaning on the state: its growth rests on government contracts, subsidies, tariffs, licenses or rules it "
                           "helps shape",
            "examples": ["a defense or government-software contractor", "a launch company living on public contracts"],
        },
        "harvest": {
            "description": "Harvesting: it returns most of its cash to its owners through buybacks and dividends instead of "
                           "reinvesting it in the business",
            "examples": ["a mature firm spending far more on buybacks and dividends than on investment"],
        },
    },
}

DEFENSIVE = {
    "type": "score",
    "instructions": "What drives `company.name`'s investment in `company.fiscal_period` (capital expenditures, research, acquisitions "
                    "and stakes in other companies, in `company.uses_of_money_billion_usd`), judging by what it says "
                    "(`company.what_it_says`): pursuing demand that does not exist yet, or fear of losing the position it has?",
    "criteria": [
        "It invests to create or capture new demand: products or markets it expects to open, with no rival forcing its hand",
        "Mostly to grow into new demand; keeping pace with rivals is a secondary reason",
        "As much to seize new demand as to keep pace with rivals spending on the same thing",
        "Mostly to keep up: rivals' spending or a shift in technology would erode its business if it held back",
        "To defend what it has: it spends because falling behind in a race for capacity, technology or talent would cost it its existing business",
    ],
}

AI = {
    "type": "score",
    "instructions": "How much of `company.name`'s investment in `company.fiscal_period` (capital expenditures, research, acquisitions and "
                    "stakes) goes to artificial intelligence: AI chips, data centers for AI, AI models and AI products, judging by "
                    "`company.what_it_says` and `company.uses_of_money_billion_usd`?",
    "criteria": [
        "None of it goes to AI",
        "A small part: AI is one project among many",
        "A substantial part, about a third",
        "Most of it goes to AI",
        "Nearly all of it goes to AI",
    ],
}

QUESTIONS = {"direction": DIRECTION, "defensive": DEFENSIVE, "ai": AI}

# Fields copied from a filing record into the scene file (USD billions unless named otherwise).
FIELDS = ["revenue", "costOfRevenue", "rd", "sga", "salesMarketing", "generalAdmin", "advertising", "capex", "financeLeases",
          "buybacks", "dividends", "acquisitions", "incomeTaxesPaid", "incomeTaxProvision", "stockComp", "netIncome",
          "compensation", "noninterestExpense", "contentSpend", "technologyAndInfrastructure", "fulfillment"]
# Corrections to a filing record, each with its reason (kept in the output's notes).
ADJUST = {
    "XOM": {"acquisitions": (0.0, "ExxonMobil's $2.6B of acquisitions sit inside its Cash Capex (10-K), so they are not added again")},
}
# Lines of a conglomerate's or an oil major's expense breakdown that are not operating costs (interest, taxes other than
# on income go to the state stream elsewhere or are financing).
NOT_OPERATING = ("interest", "incometaxprovision", "othertaxesandduties", "salesandotheroperatingrevenue")
# A filing's "other notable outflows" that are stakes in other companies (not acquisitions of whole businesses).
STAKE_WORDS = ("non-marketable", "stake", "private compan", "equity invest", "equity securities", "venture", "anthropic", "openai")


def stakes_of(f):
    total, picked = 0.0, []
    for o in f.get("otherNotableOutflows") or []:
        label = (o.get("label") or "").lower()
        if any(w in label for w in STAKE_WORDS) and isinstance(o.get("value"), (int, float)):
            total += o["value"]; picked.append(o["label"])
    return round(total, 2), picked


def operating_costs(f):
    """Total operating costs from a filing's own expense breakdown when it reports a total (conglomerates and oil majors,
    whose cost of revenue is only one of several cost lines), less interest and non-income taxes; None otherwise."""
    b = f.get("expenseBreakdown") or {}
    total = next((v for k, v in b.items() if k.lower().startswith("totalcosts") and isinstance(v, (int, float))), None)
    if total is None:
        return None
    less = sum(v for k, v in b.items() if isinstance(v, (int, float)) and not k.lower().startswith("total")
               and any(w in k.lower() for w in NOT_OPERATING) and "provision" not in k.lower() and "revenue" not in k.lower())
    return round(total - less, 2)


def uses_of(f, stakes):
    named = [("cost of revenue", "costOfRevenue"), ("research and development", "rd"), ("selling, general and administrative", "sga"),
             ("advertising", "advertising"), ("capital expenditures", "capex"), ("finance-leased assets", "financeLeases"),
             ("buybacks", "buybacks"), ("dividends", "dividends"), ("acquisitions", "acquisitions"), ("cash income taxes", "incomeTaxesPaid"),
             ("compensation and benefits", "compensation"), ("content", "contentSpend")]
    uses = {label: f[key] for label, key in named if isinstance(f.get(key), (int, float))}
    if stakes > 0:
        uses["stakes in other companies"] = stakes
    if isinstance(f.get("lobbying2025"), (int, float)):
        uses["federal lobbying 2025 (million USD)"] = f["lobbying2025"]
    return uses


def state_of(f, industry, stakes):
    return {"company": {
        "name": f["display"], "industry": industry, "fiscal_period": f.get("fiscalPeriod", ""),
        "revenue_billion_usd": f.get("revenue"), "net_income_billion_usd": f.get("netIncome"),
        "uses_of_money_billion_usd": uses_of(f, stakes), "what_it_says": f.get("strategy", ""), "country": "United States"}}


def key_of(state):
    return hashlib.sha256(json.dumps([state, QUESTIONS], sort_keys=True).encode()).hexdigest()[:20]


def main():
    filings = json.load(open(FILINGS, encoding="utf-8"))["firms"]
    economy = json.load(open(ECONOMY, encoding="utf-8"))
    capture = json.load(open(CIRCUIT, encoding="utf-8"))["capture"]
    names = {i["id"]: i["name"] for i in json.load(open(INDUSTRIES, encoding="utf-8"))["industries"]}
    cache = json.load(open(CACHE, encoding="utf-8")) if os.path.exists(CACHE) else {}

    # Pair every stored firm (the towers) with its filing: by ticker, else by name.
    by_ticker = {f["ticker"].upper(): f for f in filings if f.get("ticker")}
    rows, jobs = [], []
    for c in capture:
        f = by_ticker.get((c.get("ticker") or "").upper())
        if f is None:
            f = next((g for g in filings if g["name"].lower().startswith(c["name"].lower()) or c["name"].lower() in g["name"].lower()), None)
        if f is None:
            print(f"no filing for {c['name']}", file=sys.stderr)
            continue
        f["display"] = c["name"]
        for k, (v, why) in ADJUST.get((f.get("ticker") or "").upper(), {}).items():
            f[k] = v; f["notes"] = (f.get("notes") or "") + " Adjusted: " + why + "."
        stakes, picked = stakes_of(f)
        state = state_of(f, names.get(c["industry"], c["industry"]), stakes)
        rows.append((c, f, stakes, picked, state))
        k = key_of(state)
        if k not in cache:
            jobs.append((k, state))
    if jobs and OFFLINE:
        sys.exit(f"--offline: {len(jobs)} firms have no cached judgment")
    if jobs:
        print(f"asking Jev about {len(jobs)} firms x {len(QUESTIONS)} questions", flush=True)

        def ask(job):
            k, state = job
            try:
                return k, jev.call(state, QUESTIONS)
            except RuntimeError as e:
                print(f"{state['company']['name']}: {e}", file=sys.stderr)
                return k, None
        with ThreadPoolExecutor(max_workers=5) as pool:
            for k, answers in pool.map(ask, jobs):
                if answers is not None:
                    cache[k] = answers
        os.makedirs(os.path.dirname(CACHE), exist_ok=True)
        with open(CACHE, "w", encoding="utf-8") as fh:
            json.dump(cache, fh, indent=1)

    firms, missing = [], []
    tokens = 0
    for c, f, stakes, picked, state in rows:
        result = cache.get(key_of(state))
        a = result["answers"] if result else None
        tokens += sum((result or {}).get("usage", {}).values())
        row = {"ticker": f.get("ticker"), "name": c["name"], "industry": c["industry"], "period": f.get("fiscalPeriod"),
               "basis": f.get("basis"), "employees": f.get("employees"), "lobbying": f.get("lobbying2025"), "stakes": stakes,
               "strategy": f.get("strategy"), "sources": f.get("sources"), "notes": f.get("notes")}
        for k in FIELDS:
            if isinstance(f.get(k), (int, float)):
                row[k] = f[k]
        oc = operating_costs(f)
        if oc is not None:
            row["operatingCosts"] = oc
        if picked:
            row["stakesFrom"] = picked
        if a:
            d = a["direction"]
            row["direction"] = {k: round(v, 4) for k, v in sorted(d["probabilities"].items())}
            row["directionChoice"] = d["choice"]
            row["directionConfidence"] = round(d.get("confidence", 0), 3)
            row["defensive"] = round(a["defensive"]["score"] / (len(DEFENSIVE["criteria"]) - 1), 4)
            row["defensiveConfidence"] = round(a["defensive"].get("confidence", 0), 3)
            row["ai"] = round(a["ai"]["score"] / (len(AI["criteria"]) - 1), 4)
            row["aiConfidence"] = round(a["ai"].get("confidence", 0), 3)
        else:
            missing.append(c["name"])
        firms.append(row)

    out = {
        "note": "What the stored firms (circuit.json capture) spent in their latest fiscal year, from their audited filings, and "
                "which way Jev (TypeSafe System One) judged their money moves them. Money in USD billions; lobbying in USD "
                "millions (calendar 2025, Senate LDA filings); employees in thousands. direction holds the probability Jev put on "
                "each way (climb, integrate_down, expand_across, lean_on_state, harvest); defensive is 0 (investing for new "
                "demand) .. 1 (investing for fear of losing position); ai is the share of investment judged to go to AI (0..1). "
                "Judgments are model evidence, not measurements. economy holds the US totals the firms are set against.",
        "built": datetime.date.today().isoformat(),
        "model": next((r.get("model") for r in cache.values() if isinstance(r, dict) and r.get("model")), None),
        "tokens": tokens,
        "questions": QUESTIONS,
        "firms": firms,
        "economy": economy,
    }
    with open(OUT, "w", encoding="utf-8") as fh:
        json.dump(out, fh, indent=1, ensure_ascii=False)
    print(f"wrote {os.path.relpath(OUT, ROOT)}: {len(firms)} firms" + (f"; no judgment for {', '.join(missing)}" if missing else ""))
    for r in firms:
        top = sorted((r.get("direction") or {}).items(), key=lambda kv: -kv[1])[:2]
        print(f"  {r['name']:<24} {' / '.join(f'{k} {v:.2f}' for k, v in top):<44} defensive {r.get('defensive', float('nan')):.2f}  ai {r.get('ai', float('nan')):.2f}"
              + (f"  stakes {r['stakes']} ({'; '.join(r.get('stakesFrom', []))[:60]})" if r.get("stakes") else ""))


if __name__ == "__main__":
    main()

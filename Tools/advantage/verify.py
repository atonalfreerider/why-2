#!/usr/bin/env python3
"""The truth gate for the competitive-advantage research (Docs/research/advantage/*.json): the same test as the 2026
gate of Tools/global/verify.py (MODALITIES.md section 6.8), applied to every fact of every research file.

For each fact {id, claim, url, quotes[], values[], dated2026, ...}:
  1. the source is fetched (or read from Tools/advantage/.cache, with its fetch date);
  2. every quote must occur verbatim in the fetched text (whitespace collapsed, curly quotes straightened, Unicode
     compatibility forms folded: nothing else), and every value must appear inside a quote;
  3. Jev (TypeSafe System One, through Tools/jev.py) judges whether the text around the quote supports the claim; the
     fact passes at 0.6 or more. Without a Jev credential the gate fails closed;
  4. a fact dated 2026 must also have been read on the as-of date (today), so a 2026 number is never stale.
Jev's probability is model evidence, not proof: a fact needs the verbatim quote AND the score.

Usage (from the repository root):
    python3 -I Tools/advantage/verify.py                 # gate every research file
    python3 -I Tools/advantage/verify.py --cluster compute   # one file (Docs/research/advantage/compute.json)
    python3 -I Tools/advantage/verify.py --offline       # from the cache only
    python3 -I Tools/advantage/verify.py --dir Docs/research/life --cluster extinctions   # another research folder
A whole run writes Docs/research/advantage/verification.json ({fact id: record}); a --cluster run writes
Docs/research/advantage/gate/<cluster>.json instead, so several researchers can run the gate at once. Both print, per
cluster, the verified count and every failure with its reason.
"""
import datetime
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
RESEARCH = os.path.join(ROOT, "Docs", "research", "advantage")
OUT = os.path.join(RESEARCH, "verification.json")
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.join(ROOT, "Tools", "global"))
import verify as G  # noqa: E402  (the 2026 gate: cache, text extraction, quote and value checks, Jev)

G.CACHE = os.path.join(HERE, ".cache")   # this research keeps its own downloads and Jev answers
NOT_RESEARCH = {"verification.json", "model.json", "judgments.json"}
REQUIRED = ("id", "claim", "url", "quotes", "values")


def research_files(cluster=None, folder=RESEARCH):
    out = []
    for name in sorted(os.listdir(folder)):
        if not name.endswith(".json") or name in NOT_RESEARCH:
            continue
        if cluster and name != cluster + ".json":
            continue
        out.append(os.path.join(folder, name))
    return out


def load_facts(path):
    with open(path, encoding="utf-8") as f:
        data = json.load(f)
    facts = data.get("facts") if isinstance(data, dict) else None
    return facts if isinstance(facts, list) else []


def malformed(fact):
    missing = [k for k in REQUIRED if not fact.get(k)]
    if missing:
        return "missing " + ", ".join(missing)
    if not isinstance(fact["quotes"], list) or not isinstance(fact["values"], list):
        return "quotes and values must be lists"
    return None


def main():
    args = sys.argv[1:]
    offline = "--offline" in args
    cluster = args[args.index("--cluster") + 1] if "--cluster" in args else None
    # another research folder (e.g. Docs/research/life) gated the same way, its records in <dir>/gate/
    folder = os.path.abspath(args[args.index("--dir") + 1]) if "--dir" in args else RESEARCH
    as_of = datetime.date.today().isoformat()
    cache = G.Cache(offline)
    out = (os.path.join(folder, "gate", cluster + ".json") if cluster
           else OUT if folder == RESEARCH else os.path.join(folder, "verification.json"))
    results = {}
    seen = set()
    for path in research_files(cluster, folder):
        name = os.path.splitext(os.path.basename(path))[0]
        facts = load_facts(path)
        ok, failures = 0, []
        for fact in facts:
            fid = fact.get("id") or "?"
            if fid in seen:
                failures.append((fid, "duplicate id across the research files"))
                continue
            seen.add(fid)
            bad = malformed(fact)
            if bad:
                rec = {"status": "unverified", "reason": bad}
            else:
                item = dict(fact, values=[str(v) for v in fact["values"]],
                            refreshBefore=as_of if fact.get("dated2026") else None)
                rec = G.check_item(cache, item, as_of)
            rec["cluster"] = name
            results[fid] = rec
            if rec.get("status") == "verified":
                ok += 1
            else:
                failures.append((fid, rec.get("reason", "unverified")))
        print(f"GATE {name}: {ok} of {len(facts)} verified (as of {as_of}; verbatim quote, values in it, Jev >= {G.THRESHOLD})")
        for fid, why in failures:
            print(f"  - {fid}: {why}")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "w", encoding="utf-8") as f:
        json.dump({"asOf": as_of, "threshold": G.THRESHOLD, "jev": G.JEV, "facts": dict(sorted(results.items()))},
                  f, indent=1, ensure_ascii=False)
        f.write("\n")


if __name__ == "__main__":
    main()

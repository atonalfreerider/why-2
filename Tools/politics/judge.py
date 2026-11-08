#!/usr/bin/env python3
"""
Jev's walls for the standing army's advantage (Docs/hills-design/POLITICS.md choice 5): for each benchmark year 1945, 1950, ...,
2025 and each source of the army's advantage, in Politics.WallSources order (seaControl, nuclear, alliances, spending), how far
that source alone kept the strongest rival of the time from matching the United States, as a TypeSafe Score on five levels (0-4)
from Jev (through Tools/jev.py). Writes Docs/research/politics/walls.json; Tools/politics/build.py merges it into politics.json
as military.walls, which Assets/Scripts/Economy/Model/Politics.cs reads as its Walls.

The state holds only usable facts (gate-verified and not rejected by a skeptic: build.py's load()) about the United States and its
rivals of the time: the Soviet Union until 1991, then Russia and China; for alliances NATO against the Warsaw Pact while it stood
(1955-1991). For each measure it takes the measurement nearest the year, within three years, as {id, claim, value, unit, when};
a membership in force at the year (NATO's founding and accessions, the Warsaw Pact's) holds until it ends; events and statements
within three years set the context. Nothing in a year's state was reported more than a year after it (LEAD): a measurement taken
later (FY1948's demobilised fleet for 1945, the Defense Department's 2023-24 count for 2020) or a span that closed later (the
monopoly's '1945-1949' tells 1945 that it ended in 1949, the Warsaw Pact's '1955-1991' tells 1955 when it fell) would hand Jev
the hindsight its questions rule out. A rival without a measurement says so in words, never silently. The spending shares are the
ones the build draws (build.military()), so the SIPRI 1972 yearbook's shares it leaves out (their world totals mix price bases)
are left out here too, and projections are not measurements. A (year, source) with no US measurement is skipped and logged.

One request per year carries that year's questions (one Score per source, each naming its source alone, asked together over
the same state). The answers are not independent of the rest of the state: removing two Warsaw Pact facts from 1985's alliances
moved its command of the sea from 2.05 to 2.43, and the same request asked twice gave 2.43 and 2.36 (confidence 0.17 and 0.11),
so a wall near the 0.15 line can read "uncertain" or not from one run to the next. Answers are cached by the sha256 of the exact
request in Tools/politics/.cache/judge/, so a rerun asks only what changed and reproduces the rest; the credential is found by
Tools/jev.py and never printed or stored.

    python3 -I Tools/politics/judge.py               # judge every benchmark year and write walls.json
    python3 -I Tools/politics/judge.py --year 1980   # one year (its rows replace that year's in walls.json)
    python3 -I Tools/politics/judge.py --dry         # what each year's state holds and what is skipped; calls nothing
    python3 -I Tools/politics/judge.py --show 1980   # print that year's request; calls nothing
    python3 -I Tools/politics/judge.py --offline     # cached answers only
"""
import concurrent.futures
import hashlib
import json
import os
import re
import sys
import threading
import time

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(ROOT, "Docs", "research", "politics", "walls.json")
CACHE = os.path.join(HERE, ".cache", "judge")
sys.dont_write_bytecode = True
sys.path[0:0] = [HERE, os.path.join(ROOT, "Tools")]
import build  # noqa: E402  (Tools/politics/build.py: load() keeps the usable facts; military() the series the view draws)
import jev  # noqa: E402  (Tools/jev.py: credential discovery; the key is never printed)

YEARS = list(range(1945, 2026, 5))
WINDOW = 3          # a measurement stands for a benchmark year within three years of it
LEAD = 1            # but nothing reported more than a year after the benchmark year (the last benchmark stands for the as-of date)
UNCERTAIN = 0.15    # HillHistoryLayer.Uncertain: a wall judged with less confidence reads "uncertain"
AS_YEAR = int(build.AS_OF[:4])
US, CONTEXT = "unitedStates", "context"

# Politics.WallSources, in the order the army's ring draws them
SOURCES = ("seaControl", "nuclear", "alliances", "spending")
NAMES = {"seaControl": "Command of the sea", "nuclear": "Nuclear weapons", "alliances": "Alliances",
         "spending": "Military spending"}
# each question names its source alone and says what it compares
WHAT = {"seaControl": "command of the sea alone (the fleet of the United States and the sea lanes it could hold or deny, against "
                      "its rivals' fleets)",
        "nuclear": "nuclear weapons alone (the nuclear arsenal of the United States against its rivals' arsenals)",
        "alliances": "alliances alone (the armed forces, bases and economies that the allies of the United States brought to its "
                     "side, NATO above all, against those its rivals' allies brought to theirs)",
        "spending": "military spending alone (the United States' share of the world's military spending against its rivals' "
                    "shares)"}

# ------------------------------------------------------------------------------------------------------- the questions

# HISTORY_WALL_LEVELS (Tools/advantage/judge.py) rewritten for a state's military advantage over its rivals: one dimension (the
# United States' lead in the source, by what it would take the strongest rival of the time to make it up), in the terms of any
# year from 1945 (no dollar amounts, no "this decade"), each level a situation that stands on its own
LEVELS = ["No barrier: in it the strongest rival of the time already matched or outdid the United States, or could have within "
          "a year or two.",
          "A low barrier: the United States led in it by a margin its strongest rival could have made up within a few years.",
          "A real barrier: the United States led in it by a margin that would have taken its strongest rival many years and "
          "great resources to make up.",
          "A high barrier: the United States led in it by a margin that only the greatest powers of the time could have made "
          "up, over a decade or more.",
          "Near-insurmountable: no rival of the time could have come close to it; the world order of the period was built on "
          "it."]

SCOPE = ("`year` is a benchmark year and `rivalsOfTheTime` the strongest rivals of the United States in that year. `sources` "
         "holds, for each source of the United States' military advantage that year, the verified facts nearest the year: "
         "`unitedStates` for the United States (for alliances, NATO), `rivals` for each rival (a rival the research holds no "
         "measurement for says so), `context` for facts about the contest as a whole, and `notes` worked out from the facts.")


def questions(sources):
    """One Score per source, named in its own question (question ids are not sent to the model)."""
    return {"wall_" + src: {
        "type": "score",
        "instructions": f"{SCOPE} `sources.{src}` holds the facts on {NAMES[src].lower()}. In `year`, how much did {WHAT[src]} "
                        f"keep the strongest rival of the time from matching the United States? Where the facts are silent, "
                        f"judge from well-established knowledge of the period up to `year`.",
        "criteria": LEVELS} for src in sources}

# --------------------------------------------------------------------------------------------------------------- state


OPEN = re.compile(r"present|\d{4}-(\s|$)")   # a span still running when it was read: '1979-present', '2024- (after ...)'


def span(fact):
    """The years a fact speaks for, (first, last), from its `when`: '1980', 'FY1980', '1945-1949', '1979-present',
    '2024- (after ...)', '1955-05-14 to 1991-07-01', '18 February 1952'; a 'current' statement speaks for its reading."""
    when = fact.get("when") or ""
    years = [int(y) for y in re.findall(r"(?<!\d)(1[89]\d\d|20\d\d)(?!\d)", when)]
    if not years:
        return AS_YEAR, AS_YEAR
    last = AS_YEAR if OPEN.search(when) else max(years)
    return min(years), last


def known(fact, kind):
    """The year by which all that a fact reports was known: a measurement's first year (when it was taken), and a statement's
    that still holds ('1979-present'); an event's or a closed span's last ('2015-2020', '1955-05-14 to 1991-07-01')."""
    first, last = span(fact)
    return first if kind == "nearest" or OPEN.search(fact.get("when") or "") else last


def start(fact):
    """When a membership took effect, as a decimal year."""
    when = fact.get("when") or ""
    d = build.parse_date(when)
    if d:
        return build.decimal(d)
    m = re.search(r"(\d{4})-(\d{2})(?!\d)", when)
    return int(m.group(1)) + (int(m.group(2)) - 1) / 12 if m else span(fact)[0]


def gap(fact, year):
    first, last = span(fact)
    return max(0, first - year, year - last)


def rivals(year, source=None):
    """The rivals of the time: the Soviet Union until 1991, then Russia and China; for alliances the Warsaw Pact while it stood."""
    if year < 1991:
        if source == "alliances" and PACT and PACT["from"] <= year + .5 < PACT["to"]:
            return ["Warsaw Pact"]
        return ["Soviet Union"]
    return ["Russia", "China"]


PACT = None   # the Warsaw Pact's span, from build.military()
POSSESSIVE = {"Soviet Union": "the Soviet Union's", "Russia": "Russia's", "China": "China's", "Warsaw Pact": "the Warsaw Pact's"}
MISSING = {"seaControl": "count of {p} fleet (its ships or aircraft carriers)", "nuclear": "count of {p} nuclear warheads",
           "alliances": "measure of {p} alliances", "spending": "measure of {p} share of world military spending"}


def measures(facts, series):
    """Every measure the walls read: (source, side, kind, fact ids, until). side is US, a rival's name or CONTEXT; kind is
    "nearest" (the measurement nearest the year, within WINDOW years), "each" (every event or statement within WINDOW years) or
    "held" (every membership in force at the year, from when it took effect until `until`)."""
    def ids(pattern):
        return [f for f in facts if re.fullmatch(pattern, f) and "projection" not in (facts[f].get("when") or "")]

    def share(who):   # the shares the build draws, one a year
        return [p["fact"] for p in series["share"].get(who, [])]

    arsenal = ids(r"pol-mil-nukes-russia-\d{4}")   # the FAS series lists the Soviet arsenal under Russia before 1992
    pact_end = PACT["to"] if PACT else None
    return [
        # command of the sea
        ("seaControl", US, "nearest", ids(r"pol-mil-navy-ships-(fy\d{4}|\d{4}-\d\d-\d\d)"), None),
        ("seaControl", US, "nearest", ids(r"pol-mil-navy-carriers-\d{4}|pol-mil-navy-us-carriers-\d{4}"), None),
        ("seaControl", US, "nearest", ids(r"pol-mil-navy-carriers-us-\d{4}"), None),
        ("seaControl", US, "nearest", ids(r"pol-mil-navy-us-displacement-\d{4}"), None),
        ("seaControl", "Russia", "nearest", ids(r"pol-mil-navy-carriers-russia-\d{4}"), None),
        ("seaControl", "China", "nearest", ids(r"pol-mil-navy-china-\d{4}"), None),
        ("seaControl", "China", "nearest", ids(r"pol-mil-navy-china-oni-\d{4}"), None),
        ("seaControl", "China", "nearest", ids(r"pol-mil-navy-china-dod-\d+"), None),
        ("seaControl", "China", "nearest", ids(r"pol-mil-navy-carriers-china-\d{4}"), None),
        ("seaControl", "China", "each", ids(r"pol-mil-navy-china-overtakes"), None),
        ("seaControl", CONTEXT, "each", ids(r"pol-mil-sea-(usn-supplants-rn|leyte-1944|fon-program-1979|fon-challenges|"
                                            r"fonop-subi-2015|china-challenge|nato-hormuz-2026)|pol-mil-navy-composition"), None),
        ("seaControl", CONTEXT, "nearest", ids(r"pol-sea-share-[\w-]+|pol-mil-sea-maritime-trade-80"), None),
        # nuclear weapons
        ("nuclear", US, "nearest", ids(r"pol-mil-nukes-us-\d{4}"), None),
        ("nuclear", US, "nearest", ids(r"pol-mil-nukes-us-\d{4}-fas-inventory"), None),
        ("nuclear", US, "each", ids(r"pol-mil-nukes-us-monopoly-\d{4}"), None),
        ("nuclear", "Soviet Union", "nearest", arsenal, None),
        ("nuclear", "Soviet Union", "each", ids(r"pol-mil-nukes-ussr-first-test-\d{4}"), None),
        ("nuclear", "Russia", "nearest", arsenal, None),
        ("nuclear", "Russia", "nearest", ids(r"pol-mil-nukes-russia-\d{4}-fas-inventory"), None),
        ("nuclear", "China", "nearest", ids(r"pol-mil-nukes-china-\d{4}"), None),
        ("nuclear", "China", "nearest", ids(r"pol-mil-nukes-china-\d{4}-fas-inventory"), None),
        ("nuclear", CONTEXT, "each", ids(r"pol-mil-nukes-us-russia-share-\d{4}"), None),
        # alliances
        ("alliances", US, "held", [r["fact"] for r in series["alliances"]["nato"]], None),
        ("alliances", US, "nearest", ids(r"pol-mil-nato-members-\d+"), None),
        ("alliances", US, "nearest", share("nato"), None),
        ("alliances", US, "nearest", ids(r"pol-mil-nato-spending-over-half"), None),
        ("alliances", "Warsaw Pact", "held", ids(r"pol-mil-warsaw-pact-(founding-1955|albania-1968)"), pact_end),
        ("alliances", "Warsaw Pact", "each", ids(r"pol-mil-warsaw-pact-(dates|counterweight|response-1955|end-1991)"), None),
        ("alliances", "Warsaw Pact", "nearest", share("wto"), None),
        ("alliances", CONTEXT, "each", ids(r"pol-mil-sea-nato-hormuz-2026"), None),
        # military spending
        ("spending", US, "nearest", share("usa"), None),
        ("spending", "Soviet Union", "nearest", share("ussr"), None),
        ("spending", "Russia", "nearest", share("russia"), None),
        ("spending", "China", "nearest", share("china"), None),
    ]


def pick(facts, kind, fids, until, year):
    """The facts of one measure that speak for the year."""
    if kind == "held":
        return [f for f in fids if start(facts[f]) <= year + .5 and (until is None or year + .5 < until)]
    near = [f for f in fids if gap(facts[f], year) <= WINDOW and known(facts[f], kind) <= year + LEAD]
    if kind == "each":
        return near
    # the nearest; between two as near, the one already measured by the year
    return sorted(near, key=lambda f: (gap(facts[f], year), span(facts[f])[1] > year,
                                       abs(sum(span(facts[f])) / 2 - year), f))[:1]


def item(fact):
    return {"id": fact["id"], "claim": fact["claim"], "value": fact.get("value"), "unit": fact.get("unit"),
            "when": fact.get("when")}


def request(year, facts, ms, series):
    """The year's state, its questions, the fact ids each source's state holds, and the sources skipped (no US measurement)."""
    sections, cited, skipped = {}, {}, []
    for src in SOURCES:
        got = {}
        for source, side, kind, fids, until in ms:
            if source == src:
                for f in pick(facts, kind, fids, until, year):
                    if f not in got.setdefault(side, []):
                        got[side].append(f)
        if not got.get(US):
            skipped.append((src, f"no US measurement from {year - WINDOW} to {year + LEAD}"))
            continue
        sec = {"unitedStates": [item(facts[f]) for f in got[US]], "rivals": {}}
        for r in rivals(year, src):
            if got.get(r):
                sec["rivals"][r] = [item(facts[f]) for f in got[r]]
            else:
                # said in words: the research holds none near the year, or none at all (an alliance counts the pact's facts)
                sides = {r, "Warsaw Pact"} if src == "alliances" and r == "Soviet Union" else {r}
                ever = [f for source, side, _, fids, _ in ms if source == src and side in sides for f in fids]
                # a measurement near the year that was left out was taken more than LEAD years after it
                scope = "for any year" if not ever else f"from {year - WINDOW} to {year + LEAD}" if any(
                    gap(facts[f], year) <= WINDOW for f in ever) else f"within {WINDOW} years of {year}"
                sec["rivals"][r] = f"No measurement: the research holds no {MISSING[src].format(p=POSSESSIVE[r])} {scope}."
        if got.get(CONTEXT):
            sec["context"] = [item(facts[f]) for f in got[CONTEXT]]
        notes = []
        if src == "alliances":
            members = [r["members"] for r in series["alliances"]["nato"] if r["date"] <= year + .5]
            if members:
                notes.append(f"NATO had {members[-1]} member states in mid-{year}, counted from the facts above.")
        if src == "nuclear" and "Soviet Union" in sec["rivals"] and isinstance(sec["rivals"]["Soviet Union"], list):
            notes.append("The warhead series lists the Soviet Union's arsenal under Russia for the years before 1992.")
        if notes:
            sec["notes"] = notes
        sections[src] = sec
        cited[src] = [f for side in [US] + rivals(year, src) + [CONTEXT] for f in got.get(side, [])]
    state = {"year": year, "rivalsOfTheTime": rivals(year), "sources": sections}
    return state, questions(list(sections)), cited, skipped

# --------------------------------------------------------------------------------------------------------------- run


STATS = {"requests": 0, "cached": 0, "errors": 0, "tokens": 0, "model": None}
LOCK = threading.Lock()


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def ask(state, qs, offline):
    """Jev's answers for one request, from the cache when this exact request was asked before (Tools/advantage/judge.py's)."""
    blob = json.dumps({"state": state, "questions": qs}, sort_keys=True, ensure_ascii=False)
    path = os.path.join(CACHE, hashlib.sha256(blob.encode("utf-8")).hexdigest() + ".json")
    if os.path.exists(path):
        with LOCK:
            STATS["cached"] += 1
            STATS["model"] = STATS["model"] or load(path).get("model")
        return load(path)
    if offline:
        return None
    last = None
    for attempt in range(3):
        try:
            result = jev.call(state, qs, timeout=150)
            break
        except (RuntimeError, OSError, ValueError) as e:   # HTTP, network or timeout, a body that is not JSON: never the key
            last = f"{type(e).__name__}: {e}"
            time.sleep(4 * (attempt + 1))
    else:
        with LOCK:
            STATS["errors"] += 1
        print(f"  Jev error: {last}", file=sys.stderr)
        return None
    os.makedirs(CACHE, exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(result, f, sort_keys=True)
    with LOCK:
        STATS["requests"] += 1
        STATS["tokens"] += sum(v for v in (result.get("usage") or {}).values() if isinstance(v, (int, float)))
        STATS["model"] = result.get("model") or STATS["model"]
    return result


def reading(src, wall, confidence):
    return f"{NAMES[src]}: wall {wall:.1f} of 4" + (", uncertain" if confidence < UNCERTAIN else "")


METHOD = ("For each benchmark year (1945 to 2025, every five years, standing at mid-year) and each source of the standing army's "
          "advantage, Jev (TypeSafe System One) judged how far that source alone kept the strongest rival of the time (the Soviet "
          "Union until 1991, then Russia and China; for alliances, the Warsaw Pact from 1955 to 1991) from matching the United "
          "States, as a Score on the five levels below. Its state held only usable facts (gate-verified, not rejected by a "
          "skeptic): for each measure the measurement nearest the year within three years, the memberships in force, and the "
          "events and statements within three years, for the United States and each rival, saying so in words where the research "
          "holds no measurement for a rival; nothing reported more than a year after the year (no later measurement, and no span "
          "that closed later, so no hindsight). The spending shares are those the build draws (the SIPRI 1972 yearbook's are left "
          "out: their world totals mix price bases); projections are not measurements. A year and source with no US measurement "
          "is skipped. One request per year asked the sources' questions together; each names its source alone. `wall` is the "
          "probability-weighted level (0-4), `confidence` how concentrated the probabilities are (under 0.15 reads as "
          "uncertain), `p` the probability of each level, `facts` the facts its state held. Model judgments over the facts, not "
          "facts. Built by Tools/politics/judge.py.")


def main():
    global PACT
    args = sys.argv[1:]
    dry, offline = "--dry" in args, "--offline" in args
    show = int(args[args.index("--show") + 1]) if "--show" in args else None
    only = int(args[args.index("--year") + 1]) if "--year" in args else None
    if only is not None and only not in YEARS:
        sys.exit(f"{only} is not a benchmark year ({YEARS[0]}, {YEARS[1]}, ..., {YEARS[-1]})")
    mil = build.load("military-power")
    facts = dict(mil)
    facts.update(build.load("trade-balances"))
    series = build.military(mil)
    PACT = series["alliances"].get("warsawPact")
    ms = measures(facts, series)

    reqs = {y: request(y, facts, ms, series) for y in YEARS}
    skipped = [{"t": y + .5, "source": src, "why": why} for y in YEARS for src, why in reqs[y][3]]
    if show is not None:
        state, qs, _, _ = reqs[show]
        print(json.dumps({"state": state, "questions": qs}, indent=1, ensure_ascii=False))
        return
    for s in skipped:
        print(f"skipped {int(s['t'])} {s['source']}: {s['why']}")
    years = [y for y in YEARS if only in (None, y) and reqs[y][1]]
    print(f"{len(years)} requests, {sum(len(reqs[y][1]) for y in years)} walls")
    if dry:
        for y in years:
            state, _, cited, _ = reqs[y]
            print(f"  {y}: " + ", ".join(f"{src} {len(cited[src])} facts" + "".join(
                f" ({r}: none)" for r, v in state["sources"][src]["rivals"].items() if isinstance(v, str))
                for src in state["sources"]))
        return
    if not offline and not jev.available():
        sys.exit("Jev credential unavailable: no judgments made")

    def run(y):
        state, qs, _, _ = reqs[y]
        return y, ask(state, qs, offline)

    answered = {}
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        for y, result in pool.map(run, years):
            if result is not None:
                answered[y] = result.get("answers", {})

    old = load(OUT) if os.path.exists(OUT) else {}
    earlier = {(j["t"], j["source"]): j for j in old.get("judgments", [])}
    rows = [j for j in old.get("judgments", []) if round(j["t"] - .5) not in years]   # years not asked this run stand
    for y in years:
        _, _, cited, _ = reqs[y]
        for src in cited:
            a = answered.get(y, {}).get("wall_" + src)
            if a is None or a.get("type") != "score":
                if (y + .5, src) in earlier:
                    print(f"  kept the earlier judgment of {src} in {y}: no answer this run")
                    rows.append(earlier[(y + .5, src)])
                else:
                    print(f"  no judgment of {src} in {y}: no answer")
                continue
            wall, conf = round(a["score"], 2), round(a.get("confidence", 0), 2)
            rows.append({"t": y + .5, "source": src, "wall": wall, "confidence": conf,
                         "p": {k: round(v, 3) for k, v in (a.get("probabilities") or {}).items()},
                         "facts": cited[src], "reading": reading(src, a["score"], a.get("confidence", 0))})
    rows.sort(key=lambda j: (j["t"], SOURCES.index(j["source"])))
    out = {"asOf": build.AS_OF, "model": STATS["model"] or old.get("model"), "levels": LEVELS, "method": METHOD,
           "skipped": skipped, "judgments": rows}
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=1, ensure_ascii=False)
        f.write("\n")
    print(f"Jev: {STATS['requests']} asked, {STATS['cached']} cached, {STATS['errors']} errors, {STATS['tokens']} tokens, "
          f"model {STATS['model']}; wrote {os.path.relpath(OUT, ROOT)} ({len(rows)} walls)")


if __name__ == "__main__":
    main()

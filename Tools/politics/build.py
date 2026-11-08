#!/usr/bin/env python3
"""
Builds Assets/Resources/Data/economy/politics.json, the politics view's data (Docs/hills-design/POLITICS.md; the user's
seventh direction is Docs/research/advantage/brief-v5.md), from the gated research in Docs/research/politics.

Only facts that passed the truth gate (gate/<cluster>.json status "verified") and that no skeptic rejected (check.ok false)
are used; a replacement fact ("replaces": "<old id>") takes the place of the one it corrects. Every number in the output
names the fact it came from, so the view's panel can cite it. Nothing is interpolated or invented here: series are the
measured points; the C# model (Assets/Scripts/Economy/Model/Politics.cs) decides how to draw between them. The standing
army's walls are Jev's judgments over these facts (Tools/politics/judge.py writes Docs/research/politics/walls.json), merged
in when judged as military.walls; a wall that cites a fact that is not usable fails the build. So are the industries each key
moment touched (Tools/politics/touches.py writes Docs/research/politics/touches.json): each moment Jev judged gets "touches",
the landscape industries it scored 1 or more, highest first; a moment it was not asked about, or was asked about in other
words than its gated fact's now, gets none, and the build says so.

    python3 -I Tools/politics/build.py            # writes politics.json and prints what it left out
    python3 -I Tools/politics/build.py --check    # builds in memory and fails on a broken series, writing nothing

Times are decimal years (2021-01-20 is 2021.0521); an annual measure stands at the middle of its year, a fiscal-year count
at the end of its fiscal year (FY N ends in N: on 30 June through FY1976, on 30 September from FY1977), and a seat count
from the date its row starts (the opening of a Congress, 3 January, unless the fact gives a later date) until the next row
of its chamber. A projection is not a measured point, so no series draws one.
"""

import datetime
import hashlib
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
RESEARCH = os.path.join(ROOT, "Docs", "research", "politics")
OUT = os.path.join(ROOT, "Assets", "Resources", "Data", "economy", "politics.json")

AS_OF = "2026-11-07"
CLUSTERS = ("us-control", "us-votes", "us-officials", "military-power", "trade-balances", "key-moments")

# the stated rule for the y-axis (POLITICS.md choice 2): the presidency counts one half, each chamber's seat share a quarter
POWER = {"presidency": 0.5, "house": 0.25, "senate": 0.25,
         "rule": "Power: holding the presidency counts one half, and each chamber's share of seats one quarter."}

PARTIES = [
    {"id": "democratic", "name": "Democratic Party", "short": "Democrats", "side": -1},
    {"id": "republican", "name": "Republican Party", "short": "Republicans", "side": 1},
]

# the executive cascade, top down (POLITICS.md choice 4): rank 0 is the president, then the offices below by seniority;
# the military's ranks stand on the army's side of the view
OFFICES = [
    # (measure prefix, office id, title, rank, military)
    ("start of term as Vice President of the United States", "vice-president", "Vice President", 1, False),
    ("start of a vacancy in the office of Vice President", "vice-president", "Vice President", 1, False),
    ("start of tenure as Speaker of the US House of Representatives", "speaker", "Speaker of the House", 2, False),
    ("start of tenure as Senate Majority Leader", "senate-leader", "Senate Majority Leader", 3, False),
    ("start of term as US Secretary of State", "state", "Secretary of State", 4, False),
    ("start of term as US Secretary of the Treasury", "treasury", "Secretary of the Treasury", 5, False),
    ("start of term as US Secretary of Defense", "defense", "Secretary of Defense", 6, False),
    ("start of term as US Secretary of War", "defense", "Secretary of War", 6, False),
    ("start of term as US Secretary of the Navy", "navy", "Secretary of the Navy", 6, False),
    ("start of tenure as Chairman of the Joint Chiefs of Staff", "joint-chiefs", "Chairman of the Joint Chiefs of Staff", 7, True),
    ("start of tenure as Chief of Staff to the Commander in Chief", "joint-chiefs", "Chief of Staff to the Commander in Chief", 7, True),
]

# who holds a share of the world's military spending, as the research names them
SPENDERS = {"usa": "United States", "ussr": "Soviet Union", "russia": "Russia", "china": "China", "nato": "NATO",
            "wto": "Warsaw Pact"}
WARHEADS = {"us": "usa", "russia": "russia", "china": "china"}
PARTNERS = ("china", "mexico", "canada", "japan", "germany", "eu")

MONTHS = {m: i + 1 for i, m in enumerate(["january", "february", "march", "april", "may", "june", "july", "august",
                                          "september", "october", "november", "december"])}

problems = []   # fail the build
left_out = []   # printed, not fatal
touch_log = []  # moments without Jev's touches, and why: printed, not fatal


# ---------------------------------------------------------------------------------------------------- loading

def load(cluster):
    """The cluster's usable facts by id, in file order: gate-verified and not rejected by a skeptic."""
    with open(os.path.join(RESEARCH, cluster + ".json"), encoding="utf-8") as f:
        facts = json.load(f)["facts"]
    with open(os.path.join(RESEARCH, "gate", cluster + ".json"), encoding="utf-8") as f:
        gate = json.load(f)["facts"]
    usable = {}
    for fact in facts:
        fid = fact["id"]
        record = gate.get(fid)
        check = fact.get("check")
        if not record or record.get("status") != "verified":
            left_out.append((fid, "not verified by the gate" + (f" ({record.get('reason')})" if record else " (not run)")))
        elif isinstance(check, dict) and check.get("ok") is False:
            left_out.append((fid, "rejected by the skeptic"))
        else:
            usable[fid] = fact
    return usable


# ---------------------------------------------------------------------------------------------------- dates

def decimal(d):
    """A date as a decimal year, to four places."""
    start = datetime.date(d.year, 1, 1)
    days = (datetime.date(d.year + 1, 1, 1) - start).days
    return round(d.year + (d - start).days / days, 4)


def parse_date(text):
    """The first full date in a text: ISO (2021-01-20), 'January 20, 2021' or '20 January 2021'; None without one."""
    if not text:
        return None
    m = re.search(r"(\d{4})-(\d{2})-(\d{2})", text)
    if m:
        return datetime.date(int(m.group(1)), int(m.group(2)), int(m.group(3)))
    m = re.search(r"([A-Za-z]+)\s+(\d{1,2}),\s*(\d{4})", text)
    if m and m.group(1).lower() in MONTHS:
        return datetime.date(int(m.group(3)), MONTHS[m.group(1).lower()], int(m.group(2)))
    m = re.search(r"(\d{1,2})\s+([A-Za-z]+)\s+(\d{4})", text)
    if m and m.group(2).lower() in MONTHS:
        return datetime.date(int(m.group(3)), MONTHS[m.group(2).lower()], int(m.group(1)))
    return None


def year_of(text):
    m = re.search(r"\b(1[89]\d\d|20\d\d)\b", text or "")
    return int(m.group(1)) if m else None


def congress_start(n):
    """The opening of the nth Congress: 3 January of its first year (from the 74th, 1935)."""
    return datetime.date(1789 + 2 * (n - 1), 1, 3)


def number(text):
    """A number as the research writes it ('24,178,347', '17 733', '-1,385', '49.55')."""
    return float(re.sub(r"[ , ]", "", text))


PARTY_NAMES = {"democratic": "democratic", "democrat": "democratic", "democrats": "democratic",
               "republican": "republican", "republicans": "republican"}


def party_id(text):
    return PARTY_NAMES.get((text or "").strip().lower())


# ---------------------------------------------------------------------------------------------------- control

def presidents(control):
    rows = []
    for fact in control.values():
        if not fact.get("office"):
            continue
        start, end = parse_date(fact.get("termStart")), parse_date(fact.get("termEnd"))
        party = party_id(fact.get("party"))
        name = re.sub(r",\s*\d+\w*\s+President of the United States$", "", fact["subject"]).strip()
        if not start or not party:
            problems.append(f"{fact['id']}: president without a start or a party")
            continue
        rows.append({"name": name, "party": party, "from": decimal(start), "to": decimal(end) if end else None,
                     "fact": fact["id"]})
    rows.sort(key=lambda r: r["from"])
    for a, b in zip(rows, rows[1:]):
        if a["to"] is None or abs(a["to"] - b["from"]) > 0.003:
            problems.append(f"presidents: {a['name']} to {a['to']} does not meet {b['name']} from {b['from']}")
    return rows


def chambers(control):
    """House and Senate rows: seats by party from a date until the next row of the chamber."""
    out = {"house": [], "senate": []}
    for fact in control.values():
        chamber, seats = (fact.get("chamber") or "").lower(), fact.get("seats")
        if chamber not in out or not isinstance(seats, dict):
            continue
        n = int(fact["congress"])
        when = fact.get("when") or ""
        d = parse_date(when)
        opening = congress_start(n)
        # a row from a later date in the Congress (a mid-Congress change); a date before the opening is the election
        start = d if d and d > opening else opening
        others = sum(int(v) for k, v in seats.items() if k in ("others",)) or 0
        row = {
            "congress": n,
            "from": decimal(start),
            "seats": {"democratic": int(seats.get("democrats", 0)), "republican": int(seats.get("republicans", 0)),
                      "other": others, "vacant": int(seats.get("vacant", 0) or 0)},
            "total": int(seats.get("total")),
            "majority": party_id(seats.get("majority")),
            "fact": fact["id"],
        }
        if fact.get("notes"):
            row["note"] = fact["notes"] if isinstance(fact["notes"], str) else " ".join(fact["notes"])
        filled = sum(row["seats"].values())
        if filled > row["total"]:
            problems.append(f"{fact['id']}: {filled} seats filed of {row['total']}")
        out[chamber].append(row)

    for chamber, rows in out.items():
        rows.sort(key=lambda r: (r["from"], r["fact"]))
        dedup = []
        for r in rows:
            if dedup and abs(dedup[-1]["from"] - r["from"]) < 1e-4:
                left_out.append((dedup[-1]["fact"], f"{chamber} row superseded by {r['fact']} on the same date"))
                dedup[-1] = r
            else:
                dedup.append(r)
        out[chamber] = dedup
        have = {r["congress"] for r in dedup}
        missing = [n for n in range(79, 120) if n not in have]
        if missing:
            problems.append(f"{chamber}: no verified row for the Congresses {missing}")
    return out


# ---------------------------------------------------------------------------------------------------- votes

def votes(vote_facts):
    rows, third, population = [], [], []
    for fact in vote_facts.values():
        measure = fact["measure"]
        d = parse_date(fact.get("when"))
        if measure.startswith("national presidential popular vote: Democratic nominee") or \
                measure.startswith("national House popular vote: Democratic candidates"):
            v = fact["values"]
            kind = "president" if "presidential" in measure else "house"
            rows.append({"kind": kind, "date": decimal(d),
                         "votes": {"democratic": int(number(v[0])), "republican": int(number(v[2]))},
                         "share": {"democratic": number(v[1]), "republican": number(v[3])},
                         "fact": fact["id"]})
        elif measure.startswith("national presidential popular vote of a third-party"):
            third.append({"date": decimal(d) if d else year_of(fact.get("when")), "name": fact["subject"],
                          "votes": int(fact["value"]), "claim": fact["claim"], "fact": fact["id"]})
        elif measure.startswith("US resident population") or measure.startswith("US population estimate"):
            y = year_of(fact.get("when")) or year_of(fact["id"])
            population.append({"t": y + 0.5 if "July" in (fact.get("when") or "") + measure else y + 0.25,
                               "v": int(fact["value"]), "fact": fact["id"]})
    rows.sort(key=lambda r: (r["date"], r["kind"]))
    for a, b in zip(rows, rows[1:]):
        if abs(a["date"] - b["date"]) < 1e-4 and a["kind"] == b["kind"]:
            problems.append(f"votes: {a['fact']} and {b['fact']} are the same election")
    return rows, sorted(third, key=lambda r: r["date"]), sorted(population, key=lambda r: r["t"])


# ---------------------------------------------------------------------------------------------------- officials

def officials(official_facts, chamber_rows, president_rows):
    rows, notes = [], []
    for fact in official_facts.values():
        office = next((o for o in OFFICES if fact["measure"].startswith(o[0])), None)
        if office is None:
            notes.append({"date": decimal(parse_date(fact.get("when")) or datetime.date(year_of(fact.get("when")), 7, 1)),
                          "claim": fact["claim"], "fact": fact["id"]})
            continue
        _, oid, title, rank, military = office
        when = fact.get("when") or ""
        parts = when.split("/")
        start = parse_date(parts[0])
        end = parse_date(parts[1]) if len(parts) > 1 else None
        vacancy = fact["measure"].startswith("start of a vacancy")
        if not start:
            problems.append(f"{fact['id']}: no start date in '{when}'")
            continue
        row = {"office": oid, "title": title, "rank": rank, "military": military,
               "name": None if vacancy else fact["subject"], "from": decimal(start), "to": decimal(end) if end else None,
               "acting": "(acting)" in fact["measure"], "fact": fact["id"]}
        if not vacancy and not military:
            stated = re.search(r"party: (\w+)", fact["claim"])
            party = party_id(stated.group(1)) if stated else next(
                (party_id(v) for v in fact["values"] if party_id(v)), None)
            basis = "stated"
            if party is None and oid in ("speaker", "senate-leader"):
                # the leader of a chamber's majority belongs to the majority party
                chamber = chamber_rows["house" if oid == "speaker" else "senate"]
                at = [r for r in chamber if r["from"] <= row["from"] + 0.01]
                party, basis = (at[-1]["majority"] if at else None), "majority of the chamber"
            elif party is None:
                # a cabinet secretary serves the president who appointed them
                at = [p for p in president_rows if p["from"] <= row["from"] + 0.003]
                party, basis = (at[-1]["party"] if at else None), "appointing president"
            row["party"], row["partyBasis"] = party, basis
        rows.append(row)
    rows.sort(key=lambda r: (r["rank"], r["from"]))
    return rows, sorted(notes, key=lambda r: r["date"])


# ---------------------------------------------------------------------------------------------------- military

def military(mil):
    share = {k: [] for k in SPENDERS}
    seen = {}
    for fid, fact in mil.items():
        m = re.fullmatch(r"pol-mil-milex-(\w+)-(\d{4})(?:-yb(\d{4}))?", fid)
        if not m or m.group(1) not in SPENDERS:
            continue
        who, year, edition = m.group(1), int(m.group(2)), m.group(3)
        if "Yearbook%201972" in fact["url"]:
            # the skeptics showed that the 1972 yearbook's world totals put the Warsaw Pact at current prices beside
            # everyone else's constant prices (pol-mil-milex-usa-1950's check), so no share computed from them is used;
            # the 1976 yearbook's own percentages give 1955
            left_out.append((fid, "computed from SIPRI 1972's world total, which mixes price bases"))
            continue
        point = {"t": year + 0.5, "v": float(fact["value"]), "basis": fact["unit"], "fact": fid}
        key = (who, year)
        # one point a year: the series' own fact, else a yearbook's (the 1976 edition fills the years whose own failed)
        if key in seen and (edition or not seen[key][1]):
            left_out.append((fid, f"another {who} share for {year} is used ({seen[key][0]['fact']})"))
            continue
        if key in seen:
            share[who].remove(seen[key][0])
        seen[key] = (point, edition)
        share[who].append(point)
    for who in share:
        share[who].sort(key=lambda p: p["t"])

    warheads = {v: [] for v in WARHEADS.values()}
    firsts = []
    for fid, fact in mil.items():
        m = re.fullmatch(r"pol-mil-nukes-(us|russia|china)-(\d{4})", fid)
        if m:
            warheads[WARHEADS[m.group(1)]].append({"t": int(m.group(2)) + 0.5, "v": int(fact["value"]), "fact": fid})
        elif fid.startswith(("pol-mil-nukes-us-monopoly", "pol-mil-nukes-ussr-first-test", "pol-mil-nukes-china-first-test")):
            d = parse_date(fact.get("when"))
            firsts.append({"date": decimal(d) if d else year_of(fact.get("when")) + 0.5, "claim": fact["claim"], "fact": fid})
    for who in warheads:
        warheads[who].sort(key=lambda p: p["t"])

    navy = {"usShips": [], "usCarriers": [], "chinaShips": [], "chinaBattleForce": []}
    for fid, fact in mil.items():
        if fid.startswith("pol-mil-navy-") and "projection" in (fact.get("when") or ""):
            left_out.append((fid, "a projection, not a measurement"))
            continue
        m = re.fullmatch(r"pol-mil-navy-ships-fy(\d{4})", fid)
        if m:
            # CRS counts stand at the end of the fiscal year, which ends in its own year: FY1948 on 30 June 1948 (until
            # FY1976), FY2020 on 30 September 2020 (from FY1977)
            fy = int(m.group(1))
            navy["usShips"].append({"t": decimal(datetime.date(fy, 6, 30) if fy <= 1976 else datetime.date(fy, 9, 30)),
                                    "v": int(fact["value"]), "basis": fact["unit"], "fact": fid})
            continue
        if re.fullmatch(r"pol-mil-navy-ships-\d{4}-\d\d-\d\d", fid) and parse_date(fact.get("when")):
            # an actual count on a stated day: CRS's 293 of 1 October 2025 replaces FY2025's projected 288 (rejected)
            navy["usShips"].append({"t": decimal(parse_date(fact["when"])), "v": int(fact["value"]), "basis": fact["unit"],
                                    "fact": fid})
            continue
        if re.fullmatch(r"pol-mil-navy-carriers-\d{4}", fid):
            d = parse_date_us(fact.get("when"))
            navy["usCarriers"].append({"t": decimal(d) if d else year_of(fact["when"]) + 0.5, "v": int(fact["value"]),
                                       "basis": fact["unit"], "fact": fid})
            continue
        m = re.fullmatch(r"pol-mil-navy-china-(\d{4})", fid)
        if m:
            navy["chinaShips"].append({"t": int(m.group(1)) + 0.5, "v": int(fact["value"]), "basis": fact["unit"],
                                       "fact": fid})
            continue
        m = re.fullmatch(r"pol-mil-navy-china-oni-(\d{4})", fid)
        if m:
            navy["chinaBattleForce"].append({"t": int(m.group(1)) + 0.5, "v": int(fact["value"]), "basis": fact["unit"],
                                             "fact": fid})
    for k in navy:
        navy[k].sort(key=lambda p: p["t"])

    alliances = {"nato": [], "warsawPact": None}
    founding = mil.get("pol-mil-nato-founding-1949")
    if founding:
        alliances["nato"].append({"date": decimal(datetime.date(1949, 4, 4)), "joined": int(founding["value"]),
                                  "claim": founding["claim"], "fact": founding["id"]})
    for fid, fact in mil.items():
        if not re.fullmatch(r"pol-mil-nato-accession-\d{4}", fid):
            continue
        d = parse_date(fact.get("when"))
        # 'Greece and Turkey joined NATO on ...': the countries named before 'joined'
        named = re.split(r",\s*|\s+and\s+", fact["claim"].split(" joined NATO")[0])
        alliances["nato"].append({"date": decimal(d), "joined": len([n for n in named if n.strip()]), "claim": fact["claim"],
                                  "fact": fid})
    alliances["nato"].sort(key=lambda r: r["date"])
    members = 0
    for row in alliances["nato"]:
        members += row["joined"]
        row["members"] = members
    total = mil.get("pol-mil-nato-members-32")
    if total and members != int(total["value"]):
        problems.append(f"NATO: the accessions add up to {members} members, the research says {total['value']}")
    pact = mil.get("pol-mil-warsaw-pact-dates")
    if pact:
        alliances["warsawPact"] = {"from": decimal(datetime.date(1955, 5, 14)), "to": decimal(datetime.date(1991, 7, 1)),
                                   "claim": pact["claim"], "fact": pact["id"]}

    sea = {}
    for fid in ("pol-mil-sea-control", "pol-mil-freedom-of-navigation"):
        if fid in mil:
            sea[fid] = mil[fid]["claim"]
    return {"share": share, "warheads": warheads, "firstTests": sorted(firsts, key=lambda r: r["date"]), "navy": navy,
            "alliances": alliances}


def parse_date_us(text):
    """'1945 (as of 8/14/45, V-J Day)' or '2016 (as of 9/30/16)': the US month/day/two-digit-year date in it."""
    m = re.search(r"(\d{1,2})/(\d{1,2})/(\d{2})\b", text or "")
    if not m:
        return parse_date(text)
    y = int(m.group(3))
    y += 1900 if y >= 40 else 2000
    return datetime.date(y, int(m.group(1)), int(m.group(2)))


# ---------------------------------------------------------------------------------------------------- trade

def trade(tr):
    balance, partners, china = [], {p: [] for p in PARTNERS}, []
    notes = []
    for fid, fact in tr.items():
        m = re.fullmatch(r"pol-us-gs-balance-(\d{4})", fid)
        if m:
            v = [number(x) for x in fact["values"]]
            balance.append({"t": int(m.group(1)) + 0.5, "balance": v[0], "goods": v[1], "services": v[2],
                            "exports": v[3], "imports": v[4], "fact": fid})
            continue
        m = re.fullmatch(r"pol-us-goods-balance-(\w+)-(\d{4})", fid)
        if m and m.group(1) in partners:
            v = [number(x) for x in fact["values"]]
            partners[m.group(1)].append({"t": int(m.group(2)) + 0.5, "exports": v[0], "imports": v[1], "balance": v[2],
                                         "fact": fid})
            continue
        m = re.fullmatch(r"pol-china-gs-balance-(\d{4})", fid)
        if m:
            china.append({"t": int(m.group(1)) + 0.5, "v": float(fact["value"]), "fact": fid})
            continue
        notes.append({"claim": fact["claim"], "when": fact.get("when"), "fact": fid})
    for a in partners.values():
        a.sort(key=lambda p: p["t"])
    unit = "US$ million, current"
    for row in balance:
        if abs(row["exports"] - row["imports"] - row["balance"]) > max(5, abs(row["balance"]) * 0.002):
            problems.append(f"{row['fact']}: exports minus imports is not the balance")
    return {"unit": unit, "balance": sorted(balance, key=lambda p: p["t"]), "partners": partners,
            "chinaBalance": sorted(china, key=lambda p: p["t"]), "notes": notes}


# ---------------------------------------------------------------------------------------------------- moments

KINDS = ("law", "regulation", "trade", "monetary", "treaty", "conflict")


def kind_of(fact):
    """A moment's kind, the word before the colon of its measure ('law: signed into law (year)' is a law)."""
    return fact["measure"].split(":")[0].strip().lower()


def moment_state(fact):
    """What Jev is told about a key moment when it judges the industries the moment touched (Tools/politics/touches.py): its
    title, its date as the research gives it, its kind and the claim of its gated fact. Touches judged on other words than
    these are stale (touches())."""
    return {"title": fact["subject"], "date": fact.get("when"), "kind": kind_of(fact), "claim": fact["claim"]}


def told(fact):
    """The digest of moment_state(fact), which touches.json records for each moment it judged."""
    blob = json.dumps(moment_state(fact), sort_keys=True, ensure_ascii=False)
    return hashlib.sha256(blob.encode("utf-8")).hexdigest()[:16]


def moments(km):
    rows = []
    for fid, fact in km.items():
        kind = kind_of(fact)
        if kind not in KINDS:
            problems.append(f"{fid}: unknown kind '{kind}'")
            continue
        d = parse_date(fact.get("when"))
        y = year_of(fact.get("when"))
        rows.append({"id": fid, "date": decimal(d) if d else y + 0.5, "kind": kind, "title": fact["subject"],
                     "claim": fact["claim"], "url": fact["url"], "fact": fid})
    rows.sort(key=lambda r: r["date"])
    return rows


# ---------------------------------------------------------------------------------------------------- touches

TOUCHES = os.path.join(RESEARCH, "touches.json")
INDUSTRIES = os.path.join(ROOT, "Assets", "Resources", "Data", "economy", "industries.json")
TOUCH_FLOOR = 1   # a moment touches an industry Jev scored 1 ("indirectly") or more


def touches(rows, km):
    """
    Jev's touches (POLITICS.md choice 8; Tools/politics/touches.py writes touches.json): each moment it judged gets "touches",
    the landscape's industries it scored 1 or more, highest first (industries.json's order between equal scores), as
    {"industry", "score", "confidence"}. A moment Jev was not asked about, or was asked about in other words than its gated
    fact's now (told()), gets no "touches" and is logged, and so is an industry a moment was not judged for; a malformed or
    repeated row, or an industry the landscape does not have, is a problem.
    """
    if not os.path.exists(TOUCHES):
        touch_log.append(f"no touches for any moment: no {os.path.relpath(TOUCHES, ROOT)} (run Tools/politics/touches.py)")
        return
    with open(INDUSTRIES, encoding="utf-8") as f:
        order = [i["id"] for i in json.load(f)["industries"]]
    with open(TOUCHES, encoding="utf-8") as f:
        judged = json.load(f).get("moments") or []

    def within(v, top):
        return isinstance(v, (int, float)) and not isinstance(v, bool) and 0 <= v <= top

    by_id = {}
    for m in judged:
        fid = m.get("id")
        if not isinstance(fid, str) or fid in by_id:
            problems.append(f"touches: {fid!r} is " + ("judged twice" if fid in by_id else "not a moment id"))
            continue
        by_id[fid] = m
        if fid not in km:
            touch_log.append(f"touches of {fid} not merged: it is not a usable key moment")
    for row in rows:
        fid = row["id"]
        m = by_id.get(fid)
        if m is None:
            touch_log.append(f"no touches for {fid}: Jev was not asked about it "
                             f"(python3 -I Tools/politics/touches.py --moment {fid})")
            continue
        if m.get("told") != told(km[fid]):
            touch_log.append(f"no touches for {fid}: judged on other words than its gated fact's now "
                             f"(python3 -I Tools/politics/touches.py --moment {fid})")
            continue
        scored = {}
        for s in m.get("scores") or []:
            iid = s.get("industry") if isinstance(s, dict) else None
            if iid not in order or iid in scored or not within(s.get("score"), 4) or not within(s.get("confidence"), 1):
                problems.append(f"touches: {fid} has a row that is not an industry's score (a landscape industry judged once, a "
                                f"score 0-4 and a confidence 0-1): {s!r}")
                continue
            scored[iid] = s
        missing = [iid for iid in order if iid not in scored]
        if missing:
            touch_log.append(f"touches of {fid}: not judged for {', '.join(missing)}")
        row["touches"] = [{"industry": iid, "score": scored[iid]["score"], "confidence": scored[iid]["confidence"]}
                          for iid in sorted(scored, key=lambda i: (-scored[i]["score"], order.index(i)))
                          if scored[iid]["score"] >= TOUCH_FLOOR]


# ---------------------------------------------------------------------------------------------------- walls

WALLS = os.path.join(RESEARCH, "walls.json")
WALL_SOURCES = ("seaControl", "nuclear", "alliances", "spending")   # Politics.WallSources, in the order its ring draws them


def walls(usable):
    """The standing army's walls (POLITICS.md choice 5) from walls.json: at each benchmark time, how far each source of its
    advantage kept the strongest rival of the time from matching the United States, Jev's Score 0-4 with its confidence, and
    the facts its state held. A malformed or repeated row, or a cited fact that is not usable, is a problem."""
    with open(WALLS, encoding="utf-8") as f:
        judged = json.load(f).get("judgments") or []

    def within(v, top):
        return isinstance(v, (int, float)) and not isinstance(v, bool) and 0 <= v <= top

    rows, seen = [], set()
    for j in judged:
        name = f"walls: {j.get('source')} at {j.get('t')}"
        if j.get("source") not in WALL_SOURCES or not within(j.get("t"), 3000) or not within(j.get("wall"), 4) or \
                not within(j.get("confidence"), 1):
            problems.append(f"{name} is not a wall (a source, a time, a wall 0-4 and a confidence 0-1)")
            continue
        if (j["t"], j["source"]) in seen:
            problems.append(f"{name} is judged twice")
            continue
        seen.add((j["t"], j["source"]))
        facts = list(j.get("facts") or [])
        unusable = [fid for fid in facts if fid not in usable]
        if not facts:
            problems.append(f"{name} cites no fact")
        elif unusable:
            problems.append(f"{name} cites facts that are not usable: {', '.join(unusable)}")
        rows.append({"t": j["t"], "source": j["source"], "wall": j["wall"], "confidence": j["confidence"],
                     "reading": j.get("reading"), "facts": facts})
    rows.sort(key=lambda r: (r["t"], WALL_SOURCES.index(r["source"])))
    return rows


# ---------------------------------------------------------------------------------------------------- main

def build():
    facts = {c: load(c) for c in CLUSTERS}
    pres = presidents(facts["us-control"])
    ch = chambers(facts["us-control"])
    vote_rows, third, population = votes(facts["us-votes"])
    office_rows, office_notes = officials(facts["us-officials"], ch, pres)
    governors = []
    for fact in facts["us-control"].values():
        if isinstance(fact.get("governors"), dict):
            g = fact["governors"]
            row = {"date": decimal(parse_date(fact["when"]) or datetime.date(year_of(fact["when"]), 11, 15)),
                   "fact": fact["id"]}
            for side in ("before", "after", "inOffice"):
                counts = g.get(side) or {}
                if any(not re.fullmatch(r"\d+", str(v)) for v in counts.values()):
                    # results still being counted (a range such as 28-30): told in words, not drawn as a count
                    row[side + "Pending"] = ", ".join(f"{v} {k}" for k, v in counts.items())
                elif counts:
                    row[side] = {party_id(k): int(v) for k, v in counts.items() if party_id(k)}
            governors.append(row)
    out = {
        "asOf": AS_OF,
        "country": "united-states",
        "name": "United States",
        # the research's presidents begin with Truman's accession (12 April 1945): the span starts where every measure has one
        "from": pres[0]["from"] if pres else decimal(datetime.date(1945, 4, 12)),
        "to": decimal(datetime.date.fromisoformat(AS_OF)),
        "power": POWER,
        "parties": PARTIES,
        "presidents": pres,
        "house": ch["house"],
        "senate": ch["senate"],
        "votes": vote_rows,
        "thirdParties": third,
        "population": population,
        "governors": sorted(governors, key=lambda r: r["date"]),
        "officials": office_rows,
        "officeNotes": office_notes,
        "military": military(facts["military-power"]),
        "trade": trade(facts["trade-balances"]),
        "moments": moments(facts["key-moments"]),
    }
    touches(out["moments"], facts["key-moments"])
    if os.path.exists(WALLS):
        out["military"]["walls"] = walls({fid: fact for cluster in facts.values() for fid, fact in cluster.items()})
    return out


def main():
    out = build()
    for fid, why in left_out:
        print(f"left out {fid}: {why}")
    if problems:
        for p in problems:
            print("PROBLEM", p)
        sys.exit(1)
    for line in touch_log:
        print(line)
    counts = {k: len(v) if isinstance(v, list) else None for k, v in out.items()}
    print("politics:", ", ".join(f"{k} {n}" for k, n in counts.items() if n is not None))
    touched = [m for m in out["moments"] if "touches" in m]
    if touched:
        scores = [t["score"] for m in touched for t in m["touches"]]
        print(f"touches: {len(touched)} of {len(out['moments'])} moments judged, {len(scores)} touches scored {TOUCH_FLOOR} or "
              f"more ({sum(s >= 2 for s in scores)} 2 or more, {sum(s >= 3 for s in scores)} 3 or more), from "
              f"{os.path.relpath(TOUCHES, ROOT)}")
    if "walls" in out["military"]:
        print(f"walls: {len(out['military']['walls'])} judged, from {os.path.relpath(WALLS, ROOT)}")
    if "--check" in sys.argv:
        return
    text = json.dumps(out, ensure_ascii=False, separators=(",", ":"))
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(text)
    print(f"wrote {os.path.relpath(OUT, ROOT)} ({len(text) // 1024} KB)")


if __name__ == "__main__":
    main()

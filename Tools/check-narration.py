#!/usr/bin/env python3
"""Checks spoken narration against its on-screen text with Jev before it is voiced.

For each stop: does the narration contradict the on-screen text, does it claim anything the text (and the facts given
for the stop) do not support, and how faithfully does it carry the text? Prints one line per stop; a stop fails when
contradiction or an unsupported claim is more likely than not, or faithfulness is low.

Usage (from the repository root; the Jev key is found by Tools/jev.py and never printed):
    python3 Tools/check-narration.py economy hills_money hills_strategy      # the economy tour's named stops
    python3 Tools/check-narration.py tour                                     # every stop of the universe tour
    python3 Tools/check-narration.py ai                                       # the history stories (revolutions too)
Facts for a stop (numbers the narration may cite beyond its on-screen text) can be given in Tools/narration-facts.json:
    {"hills_money": ["fed funds 1981: 16.4%", ...]}
"""
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "Tools"))
import jev  # noqa: E402

WALK = os.path.join(ROOT, "Assets", "Resources", "Data", "Walkthrough")
SCRIPTS = {"economy": ("economy_tour.json", "economy_narration.json"), "tour": ("tour.json", "tour_narration.json"),
           "ai": ("story_ai_tour.json", "story_ai_narration.json"), "revolutions": ("story_revolutions_tour.json", "story_revolutions_narration.json")}
FACTS = os.path.join(ROOT, "Tools", "narration-facts.json")

QUESTIONS = {
    "contradiction": {"type": "noul", "instructions": "Does `stop.narration` contradict anything stated in `stop.on_screen` or in `stop.facts`?"},
    "unsupported": {"type": "noul", "instructions": "Does `stop.narration` state a specific fact, number or causal claim that neither `stop.on_screen` "
                                                    "nor `stop.facts` supports? Ordinary framing, metaphor and invitations to look do not count."},
    "faithful": {"type": "score", "instructions": "How faithfully does `stop.narration` carry the content of `stop.on_screen` to a listener?",
                 "criteria": ["It is about something else or leaves out the main point",
                              "It keeps the topic but drops or bends most of the specific content",
                              "It carries the main point and some of the specifics",
                              "It carries the main point and most specifics, with harmless framing added",
                              "It carries everything the text says, faithfully, in spoken form"]},
}


def main():
    if len(sys.argv) < 2 or sys.argv[1] not in SCRIPTS:
        sys.exit(__doc__)
    tour_file, narration_file = SCRIPTS[sys.argv[1]]
    tour = {s["id"]: s for s in json.load(open(os.path.join(WALK, tour_file), encoding="utf-8"))["steps"]}
    narration = {s["id"]: s["narration"] for s in json.load(open(os.path.join(WALK, narration_file), encoding="utf-8"))["steps"]}
    facts = json.load(open(FACTS, encoding="utf-8")) if os.path.exists(FACTS) else {}
    ids = sys.argv[2:] or [i for i in tour if i in narration]
    failed = 0
    for sid in ids:
        if sid not in tour or sid not in narration:
            print(f"{sid}: missing"); failed += 1; continue
        state = {"stop": {"title": tour[sid].get("title", ""), "on_screen": tour[sid].get("text", ""), "narration": narration[sid], "facts": facts.get(sid, [])}}
        a = jev.call_jev(state, QUESTIONS)
        c, u, f = a["contradiction"]["noul"], a["unsupported"]["noul"], a["faithful"]["score"] / 4
        bad = c > .5 or u > .5 or f < .5
        failed += bad
        print(f"{'FAIL' if bad else 'ok  '} {sid:<18} contradiction {c:.2f}  unsupported {u:.2f}  faithful {f:.2f}")
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()

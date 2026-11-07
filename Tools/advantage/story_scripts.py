#!/usr/bin/env python3
"""Writes the history stories' walkthroughs (the user, 7 Nov 2026: "write a new script and animation walkthrough", verbatim
in Docs/research/advantage/brief-v4.md): for each story a tour (Assets/Resources/Data/Walkthrough/story_<id>_tour.json:
the cards, each stop's view states as cues timed to the narration) and its narration (story_<id>_narration.json, voiced
as JohnV4 by Tools/synthesize-narration-elevenlabs.py), and each stop's facts for Tools/check-narration.py
(Tools/narration-facts.json, keyed by stop id).

Every number spoken or shown is a verified fact (Tools/advantage/verify.py): each stop lists the facts it rests on, and
the script stops if one is missing or unverified. The register is the economy tour's: impersonal and second person,
never "I". A stop's narration is a list of {show: view state, words}: each state is shown when the voice reaches its
words (timed from the voiced clip, or at 17 characters a second before it is voiced), so the picture changes with the
sentence that names it. The words live in story_scripts.json.

    python3 -I Tools/advantage/story_scripts.py
"""
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
WALK = os.path.join(ROOT, "Assets", "Resources", "Data", "Walkthrough")
MODEL = os.path.join(ROOT, "Docs", "research", "advantage", "model.json")
FACTS_OUT = os.path.join(ROOT, "Tools", "narration-facts.json")
PACE = 17.0    # characters a second (JohnV4, measured on the economy tour's clips), until a stop's own clip exists
AUDIO = os.path.join(ROOT, "Assets", "Resources", "Audio", "EconomyTour")


def clip_seconds(sid):
    """The voiced clip's length (Audio/EconomyTour/<id>.ogg), or None before it is voiced: its cues are then timed by its
    own pace, not the estimate."""
    path = os.path.join(AUDIO, sid + ".ogg")
    if not os.path.exists(path):
        return None
    try:
        out = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", path],
                             capture_output=True, text=True, timeout=30).stdout.strip()
        return float(out) if out else None
    except (OSError, ValueError, subprocess.SubprocessError):
        return None

# The stories' words: the cards, the readings, the narration by view state, and each stop's facts
# (Tools/advantage/story_scripts.json, edited as data).
SCRIPT = os.path.join(HERE, "story_scripts.json")


def main():
    script = json.load(open(SCRIPT, encoding="utf-8"))
    AI_MEASURED, JUDGED, ASSUMED = script["measured"], script["judged"], script["assumed"]
    model = json.load(open(MODEL, encoding="utf-8"))
    facts = model.get("facts", {})
    narration_facts = json.load(open(FACTS_OUT, encoding="utf-8")) if os.path.exists(FACTS_OUT) else {}
    missing = []
    for story in script["stories"]:
        steps, spoken = [], []
        for st in story["stops"]:
            for fid in st["facts"]:
                f = facts.get(fid)
                if not f or f.get("status") != "verified":
                    missing.append(f"{st['id']}: {fid}")
            narration = " ".join(x["words"] for x in st["say"])
            seconds = clip_seconds(st["id"])
            pace = len(narration) / seconds if seconds else PACE
            cues, at = [], 0.0
            for show, text in ((x["show"], x["words"]) for x in st["say"]):
                # a state shows half a second before the words that name it
                cues.append({"at": round(max(0.0, at - (.5 if at > 0 else 0)), 1), "show": show})
                at += (len(text) + 1) / pace
            steps.append({
                "id": st["id"], "title": st["title"], "text": st["text"], "reading": st["reading"],
                "measured": AI_MEASURED, "judged": JUDGED, "assumed": ASSUMED,
                "focus": "history", "anchor": "", "side": "right", "highlight": [],
                "hold": round((seconds or len(narration) / PACE) + 2), "cues": cues,
            })
            spoken.append({"id": st["id"], "narration": narration})
            narration_facts[st["id"]] = [f"{facts[fid]['claim']} ({fid})" for fid in st["facts"] if fid in facts]
        tour = {"title": story["title"], "intro": story["intro"], "end": story["end"], "steps": steps}
        with open(os.path.join(WALK, f"story_{story['id']}_tour.json"), "w", encoding="utf-8") as f:
            json.dump(tour, f, ensure_ascii=False, indent=2)
            f.write("\n")
        with open(os.path.join(WALK, f"story_{story['id']}_narration.json"), "w", encoding="utf-8") as f:
            json.dump({"voice": "JohnV4", "generated": "2026-11-07", "steps": spoken}, f, ensure_ascii=False, indent=2)
            f.write("\n")
        chars = sum(len(s["narration"]) for s in spoken)
        print(f"{story['id']}: {len(steps)} stops, {chars} characters of narration (about {chars / PACE / 60:.1f} minutes)")
    if missing:
        sys.exit("facts missing or unverified:\n  " + "\n  ".join(missing))
    with open(FACTS_OUT, "w", encoding="utf-8") as f:
        json.dump(narration_facts, f, ensure_ascii=False, indent=1)
        f.write("\n")


if __name__ == "__main__":
    sys.dont_write_bytecode = True
    main()

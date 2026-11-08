#!/usr/bin/env python3
"""Writes Tools/images/stories.json, the image list of the history stories (Docs/hills-design/HISTORY.md), from
Assets/Resources/Data/economy/stories.json: every person (a portrait, approved by the user on 7 Nov 2026 for these stories:
Docs/research/advantage/brief-v4.md) and every actor (a company's or lab's logo in use at its moment, a state's flag or
arms then, an army's or navy's flag or arms, a monogram for an alliance or a people). Ids are the stories' image ids, so
the app finds each image by the id it draws (History.Actor.Image, History.Person.Image).

    python3 -I Tools/advantage/story_images.py
    python3 -I Tools/politics/portraits.py merge W        W/items.json: these items, then Tools/images/politics.json
    python3 -I -B Tools/images/fetch.py fetch W/items.json --out W --cache C --jev --maxlag 0
    python3 -I Tools/politics/portraits.py licences W
    python3 -I -B Tools/images/fetch.py pack W --cell 256

The atlas also holds the politics view's portraits (Tools/politics/portraits.py): fetch rewrites W/manifest.json with only
the items it is given and pack retires every id missing from it, so never fetch this list alone.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
STORIES = os.path.join(ROOT, "Assets", "Resources", "Data", "economy", "stories.json")
OUT = os.path.join(ROOT, "Tools", "images", "stories.json")

APPROVED = ("2026-11-07, the user's sixth direction (Docs/research/advantage/brief-v4.md): biographical picture popups "
            "for the AI-revolution and age-of-revolutions stories")
KIND = {"state": "government", "company": "company", "lab": "company", "army": "group", "navy": "group", "alliance": "group",
        "religion": "group", "population": "group", "movement": "group"}
ORDINALS = {"first": "I", "second": "II", "third": "III", "fourth": "IV", "fifth": "V", "sixth": "VI", "seventh": "VII"}

# From the crop review of 7 Nov 2026 (the contact sheet of the first run):
#   a portrait whose face fell outside the centre square gets a box (fractions of the original);
#   a mark of another era (the British Army's modern flag, the Armee de Terre logo, the tricolour for the navy of 1781,
#   Spain's flag of 1873) gives way to a pinned mark of the time, or to the actor's monogram; the coalitions' battle
#   paintings show no mark of the coalition, so the coalitions keep their numerals; Compaq's only image was a laptop of
#   1999, so it keeps its monogram for 1986.
OVERRIDES = {
    "robert-noyce": {"box": [0.528, 0.0, 0.359, 0.639]},
    "llion-jones": {"box": [0.255, 0.039, 0.487, 0.326]},
    "spain": {"file": "Flag of Spain (1785–1873, 1875–1931).svg", "as": "P41"},
}
MONOGRAM_ONLY = {"compaq", "british-army", "french-army", "french-navy", "first-coalition", "second-coalition", "third-coalition",
                 "fourth-coalition", "fifth-coalition", "sixth-coalition", "seventh-coalition", "coalitions"}


def first_year(story, actor_id, fallback):
    years = [m["year"] for m in story["moments"] if m.get("year") is not None and
             (m.get("holder") == actor_id or actor_id in (m.get("rivals") or []) or actor_id in (m.get("allies") or []))]
    return int(min(years)) if years else int(fallback)


def main():
    with open(STORIES, encoding="utf-8") as f:
        data = json.load(f)
    items, seen = [], set()
    person_qids = {p.get("qid") for st in data["stories"] for p in st["people"] if p.get("qid")}
    for st in data["stories"]:
        for p in st["people"]:
            iid = p.get("image") or p["id"]
            if iid in seen:
                continue
            seen.add(iid)
            it = {"id": iid, "kind": "person", "search": p.get("name") or p["id"], "approved": APPROVED}
            if p.get("qid"):
                it["qid"] = p["qid"]
            if p.get("role"):
                it["hint"] = p["role"][:160]
            # paintings of the eighteenth century: the face is in the upper square
            if st["id"] == "rev":
                it["crop"] = "portrait"
            it.update(OVERRIDES.get(iid, {}))
            if "box" in it:
                it.pop("crop", None)
            items.append(it)
        for a in st["actors"]:
            iid = a.get("image") or a["id"]
            if iid in seen or iid in MONOGRAM_ONLY:
                continue
            seen.add(iid)
            kind = a.get("kind") or "company"
            start = a.get("from") if a.get("from") is not None else st["from"]
            at = max(int(st["from"]), first_year(st, a["id"], max(start, st["from"])))
            it = {"id": iid, "kind": KIND.get(kind, "group"), "label": a.get("name") or a["id"], "at": at}
            qid = a.get("qid") if a.get("qid") not in person_qids else None    # a team keyed to one of its people is not that person
            if qid:
                it["qid"] = qid
            elif kind in ("alliance", "population", "movement", "lab") or a.get("qid"):
                continue    # no mark of its own: the app draws its monogram
            else:
                it["search"] = a.get("name") or a["id"]
                if a.get("role"):
                    it["hint"] = a["role"][:160]
            if kind in ("company", "lab"):
                it["prefer"] = ["P154"]
            elif kind in ("army", "navy"):
                it["prefer"] = ["P41", "P94"]
            if kind in ("alliance", "population", "movement"):
                word = (a.get("name") or a["id"]).split()[0].lower()
                it["monogram"] = ORDINALS.get(word, "".join(w[0] for w in (a.get("name") or a["id"]).split() if w[:1].isupper())[:3] or a["id"][:2].upper())
            it.update(OVERRIDES.get(iid, {}))
            items.append(it)
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(items, f, ensure_ascii=False, indent=1)
        f.write("\n")
    print(f"{len(items)} items ({sum(1 for i in items if i['kind'] == 'person')} people) -> {os.path.relpath(OUT, ROOT)}")


if __name__ == "__main__":
    sys.dont_write_bytecode = True
    main()

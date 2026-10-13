"""Combines one account's graph record, Wikidata facts and Jev answers into the record the scenes draw.

Policy lives here, in code: Jev's identity choice is accepted at >= 0.5 probability (else the account stays
unresolved); a lone citizenship region beats Jev's region; Wikidata birth years beat Jev's decade (which is marked as an
estimate); a company is tied when the company choice gives it at least 0.2 probability (or picks it).
"""
import regions

FIRM_SHARE = 0.2
IDENTITY_MIN = 0.5
FEMALE, MALE = "Q6581072", "Q6581097"


def resolve(node, facts, labels, answers, decade=None, firm=None):
    exact = facts.get(node["exact"]) if node.get("exact") else None
    entity, identity_p = exact, 1.0 if exact else 0.0
    if not exact and "identity" in answers:
        a = answers["identity"]
        choice = a.get("choice")
        p = a.get("probabilities", {}).get(choice, 0)
        if choice and choice != "none" and p >= IDENTITY_MIN:
            k = int(choice[1:])
            cands = [q for q in node["candidates"] if q in facts][:5]
            if k < len(cands):
                entity, identity_p = facts[cands[k]], p
    if "kind" in answers:
        kind = answers["kind"].get("choice", "organization")
    elif entity is not None:
        kind = "person" if entity["human"] else "group" if entity["group"] else "organization"
    else:
        kind = "organization"
    # A human Wikidata entity settles it even when Jev hesitated.
    if entity is not None and entity["human"]:
        kind = "person"
    cit = (entity or {}).get("citizenship") or (entity or {}).get("country") or []
    regs = {regions.of(labels.get(x, "")) for x in cit} - {None}
    if len(regs) == 1:
        region, region_basis = regs.pop(), "citizenship"
    elif "region" in answers:
        region, region_basis = answers["region"].get("choice", "row"), "jev"
    else:
        region, region_basis = "row", "default"
    born = (entity or {}).get("born") if kind == "person" else (entity or {}).get("founded")
    died = (entity or {}).get("died") if kind == "person" else (entity or {}).get("dissolved")
    born_estimated = False
    if not born and decade and decade.get("choice"):
        born, born_estimated = int(decade["choice"][1:]) + 5, True
    sex = (entity or {}).get("sex") or []
    gender = "f" if FEMALE in sex else "m" if MALE in sex else "x"
    political = answers.get("political", {}).get("choice", "none") if "political" in answers else "none"
    offices = []
    for p in (entity or {}).get("positions", []):
        offices.append({"office": labels.get(p["position"], p["position"]), "from": p["from"], "to": p["to"]})
    # The company choice's distribution names every company it gives real weight to (Musk: Tesla and SpaceX).
    firms = []
    if firm and firm.get("choice") not in (None, "none"):
        probs = firm.get("probabilities", {})
        firms = sorted([k for k, v in probs.items() if k != "none" and (v >= FIRM_SHARE or k == firm["choice"])], key=lambda k: -probs[k])
    return {
        "kind": kind, "qid": (entity or {}).get("qid"), "identity": round(identity_p, 3),
        "region": region, "regionBasis": region_basis,
        "born": born, "died": died, "bornEstimated": born_estimated, "gender": gender,
        "political": political, "offices": offices[:8], "firms": firms,
        "description": (entity or {}).get("description"),
    }

"""Wikidata facts for the social layer: who each account is, when they lived, where they are from, what they do.

Exact matches come first: Wikidata's Instagram-username property (P2003) against the account's handle. Accounts
without one get name-search candidates, which Jev later chooses between (or rejects). Every HTTP response is cached
under Tools/social/.cache so reruns are free and deterministic.
"""
import hashlib
import json
import os
import time
import urllib.parse
import urllib.request

CACHE = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".cache", "wikidata")
AGENT = "why-2-social-layer/0.2 (https://github.com/atonalfreerider/why-2)"
SPARQL = "https://query.wikidata.org/sparql"
API = "https://www.wikidata.org/w/api.php"
HUMAN, GROUPS = "Q5", {"Q215380", "Q2088357", "Q5741069", "Q9212979", "Q281643", "Q216337", "Q476028", "Q847017"}


def _get(url, timeout=90):
    os.makedirs(CACHE, exist_ok=True)
    path = os.path.join(CACHE, hashlib.sha256(url.encode("utf-8")).hexdigest() + ".json")
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            return json.load(f)
    for attempt in range(5):
        try:
            req = urllib.request.Request(url, headers={"User-Agent": AGENT, "Accept": "application/json"})
            with urllib.request.urlopen(req, timeout=timeout) as r:
                data = json.load(r)
            with open(path, "w", encoding="utf-8") as f:
                json.dump(data, f)
            time.sleep(0.2)
            return data
        except Exception:
            time.sleep(2 * (attempt + 1))
    raise RuntimeError("Wikidata request failed: " + url[:160])


def by_instagram(handles):
    """Handle (lower case) -> QID, from P2003. Several items may claim a handle; the first human wins."""
    found = {}
    handles = sorted({h.lower() for h in handles if h})
    for i in range(0, len(handles), 120):
        batch = handles[i:i + 120]
        values = " ".join(json.dumps(h) for h in batch)
        q = ("SELECT ?item ?ig ?human WHERE { VALUES ?ig { %s } ?item wdt:P2003 ?ig . "
             "BIND(EXISTS{?item wdt:P31 wd:Q5} AS ?human) }") % values
        data = _get(SPARQL + "?format=json&query=" + urllib.parse.quote(q))
        for b in data["results"]["bindings"]:
            ig, qid = b["ig"]["value"].lower(), b["item"]["value"].rsplit("/", 1)[-1]
            human = b.get("human", {}).get("value") == "true"
            if ig not in found or human and not found[ig][1]:
                found[ig] = (qid, human)
    return {k: v[0] for k, v in found.items()}


def search(name, limit=5):
    """Name-search candidates (QIDs), most relevant first."""
    url = API + "?" + urllib.parse.urlencode({"action": "wbsearchentities", "search": name, "language": "en",
                                               "uselang": "en", "type": "item", "limit": limit, "format": "json"})
    return [r["id"] for r in _get(url).get("search", [])]


def entities(qids):
    """QID -> raw entity (claims, labels, descriptions), fetched 50 at a time."""
    out, qids = {}, sorted(set(qids))
    for i in range(0, len(qids), 50):
        url = API + "?" + urllib.parse.urlencode({"action": "wbgetentities", "ids": "|".join(qids[i:i + 50]),
                                                   "props": "labels|descriptions|claims", "languages": "en", "format": "json"})
        out.update(_get(url).get("entities", {}))
    return out


def labels(qids):
    out, qids = {}, sorted(set(qids))
    for i in range(0, len(qids), 50):
        url = API + "?" + urllib.parse.urlencode({"action": "wbgetentities", "ids": "|".join(qids[i:i + 50]),
                                                   "props": "labels", "languages": "en", "format": "json"})
        for k, e in _get(url).get("entities", {}).items():
            out[k] = e.get("labels", {}).get("en", {}).get("value", k)
    return out


def _values(e, prop):
    return [c["mainsnak"].get("datavalue", {}).get("value") for c in e.get("claims", {}).get(prop, [])
            if c["mainsnak"].get("snaktype") == "value"]


def _year(e, prop):
    for v in _values(e, prop):
        try:
            t = v["time"]
            return int(t[:5]) if t[0] in "+-" else int(t[:4])
        except Exception:
            continue
    return None


def _qualifier_year(claim, prop):
    for q in claim.get("qualifiers", {}).get(prop, []):
        try:
            t = q["datavalue"]["value"]["time"]
            return int(t[:5])
        except Exception:
            continue
    return None


def facts(e):
    """The facts the layer uses, with QIDs left for labelling."""
    kinds = [v["id"] for v in _values(e, "P31") if isinstance(v, dict)]
    positions = []
    for c in e.get("claims", {}).get("P39", []):
        v = c["mainsnak"].get("datavalue", {}).get("value")
        if isinstance(v, dict):
            positions.append({"position": v["id"], "from": _qualifier_year(c, "P580"), "to": _qualifier_year(c, "P582")})
    return {
        "qid": e.get("id"),
        "label": e.get("labels", {}).get("en", {}).get("value"),
        "description": e.get("descriptions", {}).get("en", {}).get("value"),
        "human": HUMAN in kinds,
        "group": any(k in GROUPS for k in kinds),
        "kinds": kinds,
        "born": _year(e, "P569"), "died": _year(e, "P570"),
        "founded": _year(e, "P571"), "dissolved": _year(e, "P576"),
        "sex": [v["id"] for v in _values(e, "P21") if isinstance(v, dict)],
        "citizenship": [v["id"] for v in _values(e, "P27") if isinstance(v, dict)],
        "country": [v["id"] for v in _values(e, "P17") + _values(e, "P495") if isinstance(v, dict)],
        "occupation": [v["id"] for v in _values(e, "P106") if isinstance(v, dict)],
        "positions": positions,
        "owner_of": [v["id"] for v in _values(e, "P1830") if isinstance(v, dict)],
        "founded_orgs": [],
        "instagram": [v for v in _values(e, "P2003") if isinstance(v, str)],
    }

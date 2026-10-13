"""Stage 1: read the social graph (GPU-FDG output) and gather Wikidata facts for every account.

Writes Tools/social/.cache/facts.json: one record per graph node with the graph's own fields (name, handle,
followers, category cloud, FDG position, bio) plus Wikidata candidates and facts. Nothing is judged here.

    python Tools/social/facts.py [path to global_social_graph.db]
"""
import json
import os
import sqlite3
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import wikidata  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_DB = os.path.join(os.path.expanduser("~"), "Desktop", "SocialGraph", "global_social_graph.db")
OUT = os.path.join(HERE, ".cache", "facts.json")


def read_graph(db):
    c = sqlite3.connect("file:" + db + "?mode=ro", uri=True)
    c.row_factory = sqlite3.Row
    bios = {r["account_id"]: r["bio"] for r in c.execute(
        "select account_id, bio from profile_snapshot p where observed_at=(select max(observed_at) from profile_snapshot q where q.account_id=p.account_id)")}
    totals = {r["person_id"]: r["total_followers"] for r in c.execute("select person_id, total_followers from person_popularity_snapshot")}
    nodes = []
    for r in c.execute("select * from unity_graph_nodes order by id"):
        followers = max(r["follower_count"] or 0, totals.get(r["person_id"]) or 0)
        nodes.append({"id": r["id"], "name": r["display_name"], "handle": (r["instagram_handle"] or "").lower(),
                      "followers": followers, "category": r["base_category"], "cloud": r["cloud_category_id"],
                      "leader": bool(r["is_category_leader"]), "photo": r["profile_photo_path"],
                      "pos": [r["position_x"], r["position_y"], r["position_z"]], "bio": (bios.get(r["account_id"]) or "")[:240]})
    edges = [[r["node1"], r["node2"], int(r["mutual_follow"] or 0), int((r["a_mentions_b_count"] or 0) + (r["b_mentions_a_count"] or 0))]
             for r in c.execute("select node1, node2, mutual_follow, a_mentions_b_count, b_mentions_a_count from unity_edge_metadata")]
    clouds = [{"id": r["category_id"], "label": r["display_name"], "color": r["color_hex"]} for r in c.execute("select * from social_category order by priority")]
    return nodes, edges, clouds


def main():
    db = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_DB
    nodes, edges, clouds = read_graph(db)
    print(f"{len(nodes)} accounts, {len(edges)} relationships, {len(clouds)} clouds", flush=True)
    exact = wikidata.by_instagram([n["handle"] for n in nodes])
    print(f"{len(exact)} exact Instagram matches on Wikidata", flush=True)
    candidates = set(exact.values())
    for k, n in enumerate(nodes):
        n["exact"] = exact.get(n["handle"])
        n["candidates"] = [] if n["exact"] else wikidata.search(n["name"])
        candidates.update(n["candidates"])
        if k % 100 == 0:
            print(f"  searched {k}/{len(nodes)}", flush=True)
    ents = wikidata.entities(candidates)
    facts = {q: wikidata.facts(e) for q, e in ents.items() if "missing" not in e}
    refs = set()
    for f in facts.values():
        refs.update(f["citizenship"] + f["country"] + f["occupation"] + f["sex"] + f["owner_of"] + [p["position"] for p in f["positions"]])
    names = wikidata.labels(refs)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump({"source": os.path.basename(db), "nodes": nodes, "edges": edges, "clouds": clouds, "facts": facts, "labels": names},
                  f, ensure_ascii=False)
    print(f"wrote {OUT}: {len(facts)} entities, {len(names)} labels")


if __name__ == "__main__":
    main()

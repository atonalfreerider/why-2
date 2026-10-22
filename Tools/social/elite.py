"""Adds influence among the influential to Assets/Resources/Data/social/people.json, from the social graph itself.

For every person in people.json (matched to the graph's nodes by Instagram handle) it writes "elite": how many of the
graph's other accounts (the 1,139 most-followed, social-graph + GPU-FDG export) follow them, from the graph's directed
follow edges, and adds the count's definition to the file's notes. Followers measure reach; this measures who the
influential themselves listen to. Nothing else in the file changes, so the Wikidata / Jev pipeline (facts.py, judge.py,
export.py) need not be rerun.

    python Tools/social/elite.py [path to global_social_graph.db]
"""
import json
import os
import sqlite3
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
from facts import DEFAULT_DB  # noqa: E402

PEOPLE = os.path.join(ROOT, "Assets", "Resources", "Data", "social", "people.json")
NOTE = ("elite = how many of the graph's other accounts (the 1,139 most-followed) follow this person, from the graph's "
        "directed follow edges (Tools/social/elite.py): influence among the influential, as followers are reach.")


def main():
    db = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_DB
    c = sqlite3.connect("file:" + db + "?mode=ro", uri=True)
    indegree = {}
    for handle, n in c.execute("select lower(m.instagram_handle), count(*) from directed_edges d join node_metadata m on m.node_id=d.followed_node "
                               "where d.follower_node <> d.followed_node group by d.followed_node"):
        indegree[handle] = n
    data = json.load(open(PEOPLE, encoding="utf-8"))
    matched = 0
    for p in data["people"]:
        n = indegree.get((p.get("handle") or "").lower())
        if n is not None:
            p["elite"] = n; matched += 1
        else:
            p["elite"] = 0
    if NOTE not in data.get("note", ""):
        data["note"] = data.get("note", "") + " " + NOTE
    with open(PEOPLE, "w", encoding="utf-8") as fh:
        json.dump(data, fh, ensure_ascii=False, separators=(",", ":"))
    top = sorted(data["people"], key=lambda p: -p["elite"])[:12]
    print(f"{matched} of {len(data['people'])} people matched; most followed by the most-followed:")
    for p in top:
        print(f"  {p['name']:<24} {p['elite']:>3}  ({p['region']}, {p['fol'] / 1e6:.0f}M followers)")


if __name__ == "__main__":
    main()

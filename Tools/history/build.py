#!/usr/bin/env python3
"""Writes the small data files the economy scene reads from Tools/history/sources.json (the sourced research):

  Assets/Resources/Data/economy/mobility-benchmarks.json   published decade income-mobility cells and event studies, so
                                                           the decade's ladder shows the model beside the data
  Assets/Resources/Data/economy/money-and-trade.json       the price of money (fed funds, 30-year mortgage, 10-year
                                                           Treasury) and the effective tariff rate and trade shares, by year

Usage (from the repository root): python3 Tools/history/build.py
"""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
SRC = os.path.join(HERE, "sources.json")
OUT = os.path.join(ROOT, "Assets", "Resources", "Data", "economy")


def cells(matrix):
    """bottom stays, top stays, bottom to top (percent) of a quintile matrix (rows = origin, bottom first)."""
    return {"bottomStay": matrix[0][0], "topStay": matrix[4][4], "bottomToTop": matrix[0][4]}


def short(source):
    return source.split(",")[0].split("(")[0].strip() if source else ""


def main():
    d = json.load(open(SRC, encoding="utf-8"))
    m = d["mobility"]
    intra = m["intragenerational"]
    rows = []
    for key, label, kind in [("treasury2007Table2", "US Treasury, taxpayers 25+, 1996-2005, fifths ranked within the panel", "closed panel"),
                             ("treasury2007Table1", "US Treasury, taxpayers 25+, 1996-2005, fifths of all taxpayers", "open"),
                             ("autenGee2009", "Auten & Gee, tax returns 25+, 1996-2005, size-adjusted", "open"),
                             ("psidHungerford1990s", "PSID, after-tax family income, 1990-1999", "survey"),
                             ("psidHungerford1980s", "PSID, after-tax family income, 1980-1989", "survey")]:
        e = intra[key]
        rows.append({"id": key, "label": label, "kind": kind, "years": e.get("years"), **cells(e["matrix"]),
                     "matrix": e["matrix"], "source": e.get("source")})
    b = intra["psidBradbury2011"]["value"]["1995-05"]
    rows.append({"id": "psidBradbury2011", "label": "PSID family heads and spouses, 1995-2005 (Bradbury 2011)", "kind": "survey", "years": "1995-2005",
                 "bottomStay": round(100 - b["bottomMovedUp"], 1), "topStay": round(100 - b["topMovedDown"], 1), "bottomToTop": None,
                 "source": intra["psidBradbury2011"].get("source")})
    inter = m["intergenerational"]
    ch = inter["chetty2014TransitionMatrix"]
    ev = m["events"]
    out = {
        "note": "Published US income-mobility benchmarks for the decade's ladder (percent of a starting fifth). The model's own "
                "panel is synthetic and ranks households of every age; the closest analogue is the Treasury's closed panel of "
                "taxpayers 25+ with fifths ranked within the panel. From Tools/history/sources.json (research collected "
                "2026-10-22, each number read in its cited source).",
        "decade": rows,
        "generations": {"label": "Chetty, Hendren, Kline & Saez 2014: children born 1980-82 at ~30 against their parents", **cells(ch["matrix"]),
                        "rankRankSlope": inter["chetty2014RankRankSlope"]["value"], "source": ch.get("source")},
        "events": {
            "jobLoss": {"label": "displaced workers lose about 25% of earnings the first year in an expansion, 39% in a recession (Davis & von Wachter 2011)",
                        "source": ev["jobDisplacement"].get("source")},
            "divorce": {"label": "household income falls 41% for women and 23% for men after a late-life divorce (GAO 2012)", "source": ev["divorce"].get("source")},
            "health": {"label": "earnings fall about 20% over three years after a hospital admission (Dobkin et al. 2018)", "source": ev["healthShock"].get("source")},
            "poverty": {"label": "37% of poverty spells begin when the head's earnings fall; about 11% with a move to a female-headed family (Bane & Ellwood)",
                        "source": ev["povertyEntriesBaneEllwood"].get("source")},
        },
    }
    with open(os.path.join(OUT, "mobility-benchmarks.json"), "w", encoding="utf-8") as fh:
        json.dump(out, fh, indent=1, ensure_ascii=False)
    money = {"note": "The price of money and trade by year, from Tools/history/sources.json (FRED / BEA / Freddie Mac / Federal Reserve). "
                     "Rates are annual averages (2026: months available); the tariff rate is customs duties over goods imports (NIPA, "
                     "calendar years, gross of the 2026 IEEPA refunds).",
             "fedFunds": d["fedFunds"]["values"], "mortgage30": d["mortgage30"]["values"], "treasury10": d["treasury10"]["values"],
             "tariffRate": d["tariffs"]["values"], "customsDuties": d["tariffs"]["customsDuties"], "goodsImports": d["tariffs"]["goodsImports"],
             "tradeShare": d["tradeShare"]["values"],
             "sources": {k: d[k].get("source") for k in ("fedFunds", "mortgage30", "treasury10", "tariffs", "tradeShare")},
             "tariffNote": d["tariffs"].get("note")}
    with open(os.path.join(OUT, "money-and-trade.json"), "w", encoding="utf-8") as fh:
        json.dump(money, fh, indent=1, ensure_ascii=False)
    print("wrote mobility-benchmarks.json and money-and-trade.json")
    for r in rows:
        print(f"  {r['id']:<22} bottom stays {r['bottomStay']}  top stays {r['topStay']}  bottom->top {r['bottomToTop']}")


if __name__ == "__main__":
    main()

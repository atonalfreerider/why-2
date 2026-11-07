#!/usr/bin/env python3
"""A port of History.cs and HillHistoryLayer's drawing rules, run over every cue of the two stories: which bodies each opened
picture draws, by what measure and value, with which lobes, on which side, inside which rings; and what a compressed cue's
selected tube shows. For writing the stops' readings so they describe the picture exactly (Docs/hills-design/HISTORY.md).
Keep it in step with History.Actor (SizeAt, Standing, MainPartAt) and HillHistoryLayer (PartsAt, Bodies, ActiveMoats).

    python3 -I Tools/advantage/story_cues.py [stop ids]
"""
import json, math, os, sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
H = json.load(open(f'{ROOT}/Assets/Resources/Data/economy/stories.json'))

EVENT = {"troops", "ships", "casualties", "executions", "spending", "deficit", "defections"}
LOBED_NOT = {"transistors", "parameters", "compute", "people", "defections", "gdpShare", "industrialShare", "share", "conscripted"}
ORDER = {
    "state": ["population", "gdp", "army", "revenue", "debt"], "population": ["population", "gdp", "army", "revenue", "debt"],
    "army": ["army", "troops"], "navy": ["navy", "ships", "sailors"], "religion": ["adherents", "revenue"],
    "company": ["marketValue", "revenue", "capital", "employees"],
    "lab": ["valuation", "users", "parameters", "revenue", "capital", "employees"],
    "alliance": ["army", "troops", "population"],
}


def year_only(y): return abs(y - round(y)) < 1e-3


def effective(v):
    y = v['year']
    if not year_only(y): return y
    return round(y) + .99 if v['part'] in EVENT else round(y)


def happened(v, year):
    lim = math.floor(year) if (year_only(v['year']) and v['part'] not in EVENT) else year
    return effective(v) <= lim + 1e-3


def slow_part(a, part):
    return a['kind'] in ("state", "population", "religion") and part in ("population", "gdp", "adherents")


def hold_of(a, part):
    if part in EVENT: return .5
    if part in ("army", "navy", "sailors", "subsidisedTroops"): return 3
    return 8 if slow_part(a, part) or part == "debt" else 5


def standing(a, part, year, hold=None, ahead=3):
    if hold is None: hold = hold_of(a, part)
    if part in EVENT: ahead = 0
    elif not slow_part(a, part): ahead = 0
    lo = hi = None
    for v in a['series']:
        if v['part'] != part or v.get('size') is None: continue
        if happened(v, year):
            if lo is None or v['year'] > lo['year']: lo = v
        elif hi is None or v['year'] < hi['year']: hi = v
    if lo is not None and year - effective(lo) <= hold + 1e-3: return lo
    if hi is not None and hi['year'] - year <= ahead: return hi
    return None


def order(a): return a.get('order') or ORDER.get(a['kind'], ["population", "army", "marketValue", "valuation", "adherents"])


FORCE_PARTS = {"army", "navy", "troops", "ships", "sailors"}


def main_part(a, year, body=False, skip=()):
    if a['kind'] in ("army", "alliance", "navy"):
        battle = "ships" if a['kind'] == "navy" else "troops"
        if any(v['part'] == battle and v.get('size') is not None and happened(v, year) and year - effective(v) <= .25 for v in a['series']):
            return battle
    for p in order(a):
        if body and p == "parameters": continue
        if p in skip: continue
        if standing(a, p, year): return p
    return None


def fmt(v):
    x = v['raw'] if v.get('raw') is not None else v['value']
    return f"{x:,}" + " " + (v.get('unit') or '') + f" [{v['year']}]"


def nearest(s, year, within, sel=None):
    ms = [m for m in s['moments'] if m.get('year') is not None and abs(m['year'] - year) <= within]
    ms.sort(key=lambda m: (round(abs(m['year'] - year) * 100), 0 if sel and (m['holder'] == sel or sel in m['allies'] or sel in m['rivals']) else 1))
    return ms[0] if ms else None


def active_moats(s, year):
    out = {}
    for m in s['moats']:
        to = m['to'] + 1 if m['to'] is not None and abs(m['to'] - round(m['to'])) < 1e-6 else m['to']
        if m['from'] is not None and to is not None and m['from'] - .01 <= year <= to + .01:
            if m['actor'] not in out or m['from'] > out[m['actor']]['from']: out[m['actor']] = m
    return out


def spans(s):
    # the lane spans as Lay draws them (the end of an integer-year actor extended by a year; successors cut predecessors)
    end = s['to'] + 1
    sp = {}
    for a in s['actors']:
        f = max(s['from'], a['from'] if a['from'] is not None else s['from'])
        t = end if a['to'] is None else min(end, a['to'] + 1 if abs(a['to'] - round(a['to'])) < 1e-6 else a['to'])
        if t <= f: t = min(end, f + 1)
        sp[a['id']] = [f, t]
    for a in s['actors']:
        suc = a.get('successor')
        if suc and suc in sp and a['kind'] in ("state", "alliance", "army", "navy", "population", "religion", "movement"):
            sp[a['id']][1] = min(sp[a['id']][1], sp[suc][0])
    return sp


def show(story, state):
    s = next(x for x in H['stories'] if x['id'] == story)
    parts = state.split(':')
    year = float(parts[2]); opened = 'x' in parts
    sel = next((p[4:] for p in parts if p.startswith('sel=')), None)
    actors = {a['id']: a for a in s['actors']}
    sp = spans(s)
    print(f"--- {state}")
    if not opened:
        for aid in ([sel] if sel else []):
            a = actors.get(aid)
            if not a: print('   SELECTED MISSING', aid); continue
            p = main_part(a, year)
            alive = sp[aid][0] - .01 <= year <= sp[aid][1] + .01
            print(f"   tube {aid}: {'alive' if alive else 'NOT ALIVE'} {sp[aid]}, drawn by {p}: {fmt(standing(a, p, year)) if p else 'not measured (faint)'}")
        return
    moment = nearest(s, year, 1.5, sel)
    side = {}
    if moment:
        side[moment['holder']] = -1
        for x in moment['allies']: side[x] = -1
        for x in moment['rivals']: side[x] = 1
    near = nearest(s, year, 1.5, sel)
    part_ids = set()
    if near:
        for m in s['moments']:
            if m.get('year') is not None and abs(m['year'] - near['year']) < .05:
                part_ids |= {m['holder'], *m['allies'], *m['rivals']}
    moats = active_moats(s, year)
    part_ids |= set(moats)
    forces = {a['id'] for a in s['actors'] if a['kind'] in ('army', 'navy') and main_part(a, year, True)}
    print(f"   sides from: {moment['id'] if moment else None}; title: {(nearest(s, year, .75, sel) or {}).get('title')}")
    for a in s['actors']:
        f, t = sp[a['id']]
        alive = f - .01 <= year <= t + .01
        own = any(x in forces for x in a.get('forces') or [])
        p = main_part(a, year, True, FORCE_PARTS if own else ())
        if p is None and not (a['id'] in part_ids and (alive or abs(year - f) < 1.5)): continue
        if p is not None and not alive: continue
        lobes = []
        if p:
            for q in dict.fromkeys(v['part'] for v in a['series']):
                if q == p or q in LOBED_NOT: continue
                if own and q in ('army', 'navy', 'troops'): continue
                if a.get('order') and (q not in a['order'] or q == 'population'): continue
                v = standing(a, q, year)
                if v: lobes.append(f"{q}={fmt(v)}")
        ring = moats.get(a['id'])
        sd = {-1: 'HOLDER-SIDE', 1: 'RIVAL', 0: 'other'}[side.get(a['id'], 0)]
        print(f"   {sd:<11} {a['id']}: {p + ' ' + fmt(standing(a, p, year)) if p else 'NOT MEASURED (grey)'}"
              + (f" | lobes: {'; '.join(lobes)}" if lobes else '')
              + (f" | RING {[(x['source'], x['wall']) for x in ring['sources']]}" if ring else ''))
    for sid in ([sel] if sel else []):
        if sid not in actors: print('   SELECTED MISSING', sid)


for f, story in (('story_ai_tour.json', 'ai'), ('story_revolutions_tour.json', 'rev')):
    t = json.load(open(f'{ROOT}/Assets/Resources/Data/Walkthrough/{f}'))
    for st in t['steps']:
        if len(sys.argv) > 1 and st['id'] not in sys.argv[1:]: continue
        print(f"\n===== {st['id']}: {st['reading']}")
        for c in st['cues']:
            show(story, c['show'])

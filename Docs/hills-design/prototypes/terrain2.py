#!/usr/bin/env python3
"""SPEC2 merged terrain: the terrain part's prototype (terrain/terrain.py) with the merged constants of SPEC2 section 2
(treads 1.20 deep, risers 0.70, the gov plain's front 1.20), plus gorges, lakes on the valley rivers and the reference
log lines. Usage: python3 terrain2.py [year ...]   (default 2025 1972 1950). Writes hills-<year>.json for stand.py.
Deterministic: no random numbers.
"""
import json, math, os, sys, io, contextlib
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'terrain'))
with contextlib.redirect_stdout(io.StringIO()):
    import terrain as T

# ---------------------------------------------------------------- the merged constants (SPEC2 2.1)
T.FRONT = 1.20                          # the gov plain in front of the mesas = a tread
T.VALLEY = [0.00, 1.20, 1.20, 1.20, 1.20]
T.RISER = 0.70
ROAD_V = 0.60                           # the valley river's centre, from the tread's lip
GORGES, GORGE_SEP, GORGE_HALF = 3, 2.0, 0.15
LAKE_AREA_GDP = T.AREA_GDP / 10         # 4.6 u2 per GDP: a lake a tenth of its hill when households pay its whole VA
LAKE_B_MAX = 0.16


def tread(t):
    """(lip d, hills d) of a tread: gov's is the front plain [0, FRONT]."""
    if t == 0:
        return 0.0, T.FRONT
    return T.Z_VALLEY[t], T.Z_HILLS[t]


def gorges(L, t):
    """Up to 3 gorge x positions through the range in front of riser t (range t-1): local minima of its crest."""
    X, Z = L['X'], L['Z']
    own = sum(o[0] for o in L['own'])
    lo = T.Z_HILLS[t - 1]
    hi = T.Z_HILLS[t - 1] + 2 * T.ZB[t - 1] + 0.35 if t - 1 > 0 else T.FRONT + 1.0
    band = (Z >= lo - 0.05) & (Z <= hi)
    crest = np.where(band, own, 0).max(axis=0)
    gx = L['gx']
    cand = []
    for k in range(1, len(gx) - 1):
        if abs(gx[k]) > T.LAND_W / 2 - 0.45: continue
        if crest[k] <= crest[k - 1] and crest[k] <= crest[k + 1]:
            cand.append((round(float(crest[k]), 4), float(gx[k])))
    cand.sort()
    out = []
    for c, x in cand:
        if all(abs(x - y) >= GORGE_SEP for y in out):
            out.append(x)
        if len(out) == GORGES: break
    return sorted(out)


def report2(L):
    y = L['year']; g = T.GDP[y]
    lines = []
    d = {t: tread(t) for t in range(5)}
    lines.append(f"[Hills {y}] GDP ${g/1000:.2f}T; {T.AREA_GDP:.0f} u2 per GDP (1 u2 = ${g/T.AREA_GDP/1000:.2f}T); land "
                 f"{T.LAND_W:.1f} x {T.Z_BACK:.2f}; treads (lip..hills) " +
                 ' '.join(f"{T.TIERS[t]} {d[t][0]:.2f}-{d[t][1]:.2f}" for t in range(5)) +
                 "; zones " + ' '.join(f"{2*T.ZB[t]:.2f}" for t in range(1, 5)))
    S = L['S']
    rng = []
    for t in range(5):
        v = [S[i] for i in T.ROWS[t]]
        rng.append(f"{T.TIERS[t]} {min(v):.2f}-{max(v):.2f}")
    ok = all(min(S[i] for i in T.ROWS[t]) > max(S[i] for i in T.ROWS[t - 1]) for t in range(1, 5))
    worst = 0.0
    for i in range(T.N):
        m = L['own'][i][1] < 1
        if m.any(): worst = max(worst, float(np.max(L['slope'][m])))
    lines.append(f"  summits {'; '.join(rng)}; strictly ordered {ok}; steepest hill {math.degrees(math.atan(worst)):.0f} deg; "
                 f"risers {math.degrees(math.atan(0.70 * 1.5 / T.RISER)):.0f} deg; cloud level {T.BENCH_Y[4] + T.OWN_MAX + T.CLOUD_GAP:.2f}")
    for t in range(5):
        lines.append(f"  {T.TIERS[t]:9s}: " + ', '.join(
            f"{T.IDS[i]} x{L['x'][i]:+.2f} d{L['z'][i]:.2f} {2*L['ab'][i][0]:.2f}x{2*L['ab'][i][1]:.2f} S{S[i]:.2f}" for i in T.ROWS[t]))
    gz = {t: gorges(L, t) for t in range(1, 5)}
    lines.append("  gorges " + '; '.join(f"{T.TIERS[t-1]}->{T.TIERS[t]} " + ' '.join(f"{x:+.2f}" for x in gz[t]) for t in range(1, 5)))
    return lines, gz


def export(L, gz, path):
    hills = []
    for i in range(T.N):
        t = int(T.TIER[i])
        w, u, o = T.shares(i, L['year'])
        hills.append(dict(id=T.IDS[i], tier=t, x=float(L['x'][i]), d=float(L['z'][i]), a=float(L['ab'][i][0]),
                          b=float(L['ab'][i][1]), h=float(L['h'][i]), S=float(L['S'][i]), base=T.BENCH_Y[t],
                          wages=w, upkeep=u, owners=o, va=T.va(i, L['year'])))
    treads = [dict(tier=t, lip=tread(t)[0], hills=tread(t)[1], y=T.BENCH_Y[t]) for t in range(5)]
    json.dump(dict(year=L['year'], gdp=T.GDP[L['year']], depth=T.Z_BACK, width=T.LAND_W,
                   cloudY=T.BENCH_Y[4] + T.OWN_MAX + T.CLOUD_GAP, roadV=ROAD_V, hills=hills, treads=treads,
                   gorges={str(t): v for t, v in gz.items()}, order=[[T.IDS[i] for i in T.ROWS[t]] for t in range(5)]),
              open(path, 'w'), indent=1)


if __name__ == '__main__':
    years = [int(a) for a in sys.argv[1:]] or [2025, 1972, 1950]
    print('order (left -> right): ' + ' | '.join(T.TIERS[t] + ': ' + ' '.join(T.IDS[i] for i in T.ROWS[t]) for t in range(5)))
    for y in years:
        L = T.layout(y)
        lines, gz = report2(L)
        for ln in lines: print(ln)
        export(L, gz, os.path.join(HERE, f'hills-{y}.json'))
        T.render_top(L, os.path.join(HERE, f'top-{y}.png'))

#!/usr/bin/env python3
"""SPEC2 merged placement prototype (sections 3 and 2.5); stand3.py = stand.py + the rev-2 fixes (crown seats, the crown band, clouds by overweights, rows by wealth checks): who stands where on the merged terrain (terrain2.py).

Inputs: hills-<year>.json (terrain2.py), the person-year dump of HEAD + the allowance (mind/py_a0.5.csv, bowl player keys),
the bowl's 2025 pools (lead-fix6 log), the people part's crown values. Approximates the Hills census (1% split into
controllers / diversified by the ledger rule; comfortable retirees by former industry; tier / rest cells to their
members' plurality industry), then places: floor rows on the treads (SPEC2 3.3), slope ledges (3.4), lakes on the valley
rivers (2.5), crowns (3.5), clouds (3.6). Prints feasibility numbers. Deterministic.
"""
import csv, json, math, os, sys, collections

HERE = os.path.dirname(os.path.abspath(__file__))
SP = '/tmp/claude-0/-home-user-why-2/dc9bf553-91de-54d2-9f1f-9831e1f24cfa/scratchpad'
YEAR = int(sys.argv[1]) if len(sys.argv) > 1 else 2025
DISC_K = float(os.environ.get('DISC_K', '0.030'))
H = json.load(open(f'{HERE}/hills-{YEAR}.json'))
IND = json.load(open('/home/user/why-2/Assets/Resources/Data/economy/industries.json'))['industries']
CIR = json.load(open('/home/user/why-2/Assets/Resources/Data/economy/circuit.json'))
IDS = [i['id'] for i in IND]; IDX = {k: i for i, k in enumerate(IDS)}
HILL = {h['id']: h for h in H['hills']}
DEPTH = H['depth']
Z0 = DEPTH / 2                      # land-local z = d - DEPTH / 2
GDP = H['gdp']

# ---------------------------------------------------------------- the dump: this year's records and every adult's history
rec, hist = {}, collections.defaultdict(list)
with open(f'{SP}/redesign2/mind/py_a0.5.csv') as f:
    for r in csv.DictReader(f):
        y = int(r['year'])
        if y > YEAR: continue
        p = int(r['person'])
        if r['adult'] == '1':
            hist[p].append((y, int(r['employed']), int(r['se']), int(r['industry'])))
        if y == YEAR: rec[p] = r
adults = [p for p, r in rec.items() if r['adult'] == '1']
kids = [p for p, r in rec.items() if r['adult'] != '1']


def former(p):
    """Last industry while employed, scanning back at most 45 years; -1 when none."""
    for y, emp, se, ind in sorted(hist[p], reverse=True):
        if y < YEAR - 45: break
        if emp and ind >= 0: return ind
    return -1


def career_se(p):
    return sum(1 for y, emp, se, ind in hist[p] if emp and se)


# ---------------------------------------------------------------- the control ledger (SPEC2 3.2), approximated
top = [p for p in adults if rec[p]['wg'] == '3']
w1 = sum(max(0.0, float(rec[p]['wealth'])) for p in top)
QUOTA = 0.204


def ledger_key(p):
    r = rec[p]
    runs = int(r['se']) == 1 and float(r['business']) > 0
    return (-int(runs), -career_se(p), -float(rec[p]['wealth']), p)


eligible = [p for p in top if (int(rec[p]['se']) == 1 and float(rec[p]['business']) > 0) or career_se(p) >= 5]
eligible.sort(key=ledger_key)
controllers, acc = set(), 0.0
for p in eligible:
    if acc >= QUOTA * w1: break
    controllers.add(p); acc += max(0.0, float(rec[p]['wealth']))

# ---------------------------------------------------------------- the Hills census, approximated on the bowl's keys
TIER_OF = {i['id']: ['gov', 'raw', 'make', 'services', 'tech'].index(i['tier']) for i in IND}


def plurality_industry(members):
    w = collections.Counter()
    for p in members:
        r = rec[p]
        ind = int(r['industry'])
        if ind < 0: ind = former(p)
        if ind >= 0: w[ind] += float(r['wages']) + float(r['business']) + 1e-6
    if not w: return IDX['state_local']
    return sorted(w.items(), key=lambda kv: (-kv[1], kv[0]))[0][0]


cells = collections.defaultdict(list)
for p in adults:
    key = rec[p]['key']; g, anchor, tribe = key.split('|')
    if g == 'top1':
        if p in controllers:
            ind = int(rec[p]['industry']);
            ind = ind if ind >= 0 else former(p)
            cells[('top1', 'crown', 'X')].append(p)
        else:
            ind = int(rec[p]['industry']); ind = ind if ind >= 0 else former(p)
            home = ['gov', 'raw', 'make', 'services', 'tech'][TIER_OF[IDS[ind]]] if ind >= 0 else 'none'
            cells[('top1', 'cloud:' + home, 'X')].append(p)
    elif g == 'retired_savings':
        ind = former(p)
        cells[(g, 'former:' + (IDS[ind] if ind >= 0 else 'education'), tribe)].append(p)
    else:
        cells[(g, anchor, tribe)].append(p)

# merge small cells: retirees' former cells < 10 lines -> tier, then rest; clouds < 3 lines -> cloud:rest
merged = collections.defaultdict(list)
for (g, a, t), m in cells.items():
    if g == 'retired_savings' and len(m) < 10:
        ind = IDX.get(a.split(':')[1], -1)
        a = 'tier:' + ['gov', 'raw', 'make', 'services', 'tech'][TIER_OF[IDS[ind]]] if ind >= 0 else 'rest'
        t = 'M'
    merged[(g, a, t)].extend(m)
final = collections.defaultdict(list)
for (g, a, t), m in merged.items():
    if g == 'retired_savings' and a.startswith('tier:') and len(m) < 10: a = 'rest'
    if g == 'top1' and a.startswith('cloud:') and len(m) < 3: a = 'cloud:rest'
    final[(g, a, t)].extend(m)
# children: the mother's player, else the father's
player_of = {}
for k, m in final.items():
    for p in m: player_of[p] = k
kids_of = collections.Counter()
for c in kids:
    mo, fa = int(rec[c]['mother']), int(rec[c]['father'])
    k = player_of.get(mo) or player_of.get(fa)
    if k: kids_of[k] += 1

GROUP_ZONE = {'top1': None, 'owners': 'slope', 'gig': 'slope', 'pmc': 'floor', 'public': 'floor', 'office': 'floor',
              'frontline': 'floor', 'working_poor': 'floor', 'retired_savings': 'floor', 'retired_ss': 'floor',
              'students': 'floor', 'out_of_work': 'floor'}
BAND = {'pmc': 0, 'public': 1, 'office': 1, 'retired_savings': 1, 'frontline': 2, 'retired_ss': 2,
        'working_poor': 3, 'students': 3, 'out_of_work': 3}
PSEUDO = {'social_security': 'federal', 'schools': 'education', 'safety_net': 'state_local'}

players = []
for (g, a, t), m in sorted(final.items()):
    people = (len(m) + kids_of[(g, a, t)]) * 1e5
    pl = dict(key=f"{g}|{a}|{t}", group=g, members=m, people=people, r=DISC_K * math.sqrt(people / 1e6))
    if g == 'top1':
        pl['zone'] = 'crown' if a == 'crown' else 'cloud'
    else:
        pl['zone'] = GROUP_ZONE[g]
    base = a.split('/')[0]
    if a.startswith('former:'): hill = a.split(':')[1]
    elif base in PSEUDO: hill = PSEUDO[base]
    elif a in IDX: hill = a
    elif pl['zone'] in ('floor', 'slope') or a == 'crown': hill = IDS[plurality_industry(m)]
    else: hill = None
    pl['hill'] = hill
    pl['wealth'] = sum(float(rec[p]['wealth']) for p in m) / len(m)
    players.append(pl)

zones = collections.Counter(p['zone'] for p in players)
print(f"[Census {YEAR}] {len(players)} players; zones " + ', '.join(f"{z} {zones[z]}" for z in ('floor', 'slope', 'crown', 'cloud')) +
      f"; 1% lines {len(top)}, controllers {len(controllers)} ({100*acc/max(w1,1):.1f}% of the 1%'s wealth); disc K {DISC_K}; "
      f"largest disc r {max(p['r'] for p in players):.3f} ({max(p['people'] for p in players)/1e6:.1f}M people)")


# ---------------------------------------------------------------- PackRow (LandMath.PackRow's linear twin)
def pack(desired, half, lo, hi, gap=0.03):
    n = len(desired)
    if n == 0: return [], True
    off = [0.0] * n
    for k in range(1, n): off[k] = off[k - 1] + half[k - 1] + half[k] + gap
    tgt = [desired[k] - off[k] for k in range(n)]
    blocks = []
    for v in tgt:
        blocks.append([v, 1])
        while len(blocks) > 1 and blocks[-2][0] / blocks[-2][1] > blocks[-1][0] / blocks[-1][1]:
            s, c = blocks.pop(); blocks[-1][0] += s; blocks[-1][1] += c
    fit = []
    for s, c in blocks: fit += [s / c] * c
    Lb, Ub = lo + half[0], hi - (off[-1] + half[-1])
    ok = Lb <= Ub
    fit = [min(Ub, max(Lb, f)) for f in fit] if ok else [(Lb + Ub) / 2] * n
    return [fit[k] + off[k] for k in range(n)], ok


# ---------------------------------------------------------------- treads, rows, lakes (SPEC2 2.5, 3.3)
ROAD_V = H['roadV']
ROWS_V = {0: 1.07, 1: 0.87, 2: 0.33, 3: 0.13}
EDGE = 6.7
POOLS = {'health': 3750, 'trade': 3210, 'real_estate': 3180, 'manufacturing': 2100, 'hospitality': 1370, 'other_services': 952,
         'finance': 611, 'transport': 545, 'insurance': 506, 'education': 464, 'media_telecom': 424, 'entertainment': 406,
         'banking': 405, 'utilities': 353, 'professional': 160, 'software': 120, 'internet': 150, 'legal': 120, 'hardware': 90,
         'agriculture': 150, 'oil_gas': 120, 'mining_metals': 20}
LAKE_AREA_GDP, LAKE_B = 46 / 10, 0.16
lakes = {}
for t in range(5):
    hs = [h for h in H['hills'] if h['tier'] == t and POOLS.get(h['id'], 0) >= 5]
    hs.sort(key=lambda h: (h['x'], h['id']))
    half, des = [], []
    for h in hs:
        area = LAKE_AREA_GDP * POOLS[h['id']] / (GDP)
        b = min(LAKE_B, math.sqrt(area / math.pi)); a = area / (math.pi * b)
        lakes[h['id']] = dict(a=a, b=b); half.append(a); des.append(h['x'])
    xs, ok = pack(des, half, -EDGE, EDGE, 0.10)
    for h, x in zip(hs, xs): lakes[h['id']]['x'] = x
if YEAR == 2025:
    print("[Lakes 2025] " + ', '.join(f"{k} {2*v['a']:.2f}x{2*v['b']:.2f} at x{v['x']:+.2f} (hill x{HILL[k]['x']:+.2f})"
                                      for k, v in sorted(lakes.items(), key=lambda kv: -POOLS[kv[0]])[:8]))

frontage = {}
for t in range(5):
    hs = sorted([h for h in H['hills'] if h['tier'] == t], key=lambda h: (h['x'], h['id']))
    for k, h in enumerate(hs):
        lo = -7.0 if k == 0 else 0.5 * (hs[k - 1]['x'] + h['x'])
        hi = 7.0 if k == len(hs) - 1 else 0.5 * (h['x'] + hs[k + 1]['x'])
        frontage[h['id']] = (lo, hi)

gz = H['gorges']
spills, outside, worst, rows_report = 0, [], 0.0, []
GORDER = ['pmc', 'public', 'office', 'retired_savings', 'frontline', 'retired_ss', 'working_poor', 'students', 'out_of_work']
for t in range(5):
    tr = H['treads'][t]
    for band in (0, 1, 2, 3):
        ps = [p for p in players if p['zone'] == 'floor' and HILL[p['hill']]['tier'] == t and BAND[p['group']] == band]
        if not ps: continue
        # gorge blocks on this row: riser t+1's gorges cross the back rows (0, 1); riser t's the front rows (2, 3)
        blocks = []
        if band in (0, 1) and str(t + 1) in gz: blocks = gz[str(t + 1)]
        if band in (2, 3) and str(t) in gz: blocks = gz[str(t)]
        cuts = [-EDGE] + sorted(x for b in blocks for x in (b - 0.15, b + 0.15)) + [EDGE]
        intervals = [(cuts[k], cuts[k + 1]) for k in range(0, len(cuts), 2)]
        ps.sort(key=lambda p: (HILL[p['hill']]['x'], GORDER.index(p['group']), p['key']))
        groups = collections.defaultdict(list)
        for p in ps:
            x = HILL[p['hill']]['x']
            best = min(range(len(intervals)), key=lambda k: (0 if intervals[k][0] <= x <= intervals[k][1] else
                                                           min(abs(x - intervals[k][0]), abs(x - intervals[k][1])), k))
            groups[best].append(p)
        used, avail = 0.0, 0.0
        for k, (lo, hi) in enumerate(intervals):
            avail += hi - lo
            m = groups.get(k, [])
            if not m: continue
            xs, ok = pack([HILL[p['hill']]['x'] for p in m], [p['r'] for p in m], lo, hi)
            if not ok: spills += 1
            used += sum(2 * p['r'] + 0.03 for p in m)
            for p, x in zip(m, xs):
                p['x'] = x; p['v'] = ROWS_V[band]
                p['z'] = tr['lip'] + ROWS_V[band] - Z0
                d = abs(x - HILL[p['hill']]['x']); worst = max(worst, d)
                lo_f, hi_f = frontage[p['hill']]
                if not (lo_f <= x <= hi_f): outside.append((p['key'], round(x - HILL[p['hill']]['x'], 2)))
        rows_report.append(f"{['gov','raw','make','services','tech'][t]}/b{band}: {len(ps)} players, fill {used/avail:.2f}")
print(f"[Floor {YEAR}] " + '; '.join(rows_report))
print(f"  spills {spills}; max |x - hill x| {worst:.2f} u; outside their hill's frontage {len(outside)}: {outside[:12]}")

# ---------------------------------------------------------------- slope ledges (SPEC2 3.4)
w_lo = 2 * sorted(float(rec[p]['wealth']) for p in adults)[len(adults) // 2]
w_hi = min(float(rec[p]['wealth']) for p in top) if top else 1
E = {0: 0.10, 1: 0.32, 2: 0.50, 3: 0.68}
slope_rep = collections.Counter(); over = []
for hid in IDS:
    ps = [p for p in players if p['zone'] == 'slope' and p['hill'] == hid]
    if not ps: continue
    h = HILL[hid]
    byrow = collections.defaultdict(list)
    for p in ps:
        if p['group'] == 'gig': row = 0
        else:
            tt = min(1, max(0, math.log(max(p['wealth'], 1) / w_lo) / math.log(w_hi / w_lo)))
            row = 1 if tt < 1 / 3 else 2 if tt < 2 / 3 else 3
        byrow[row].append(p)
    for row, m in byrow.items():
        rho = math.sqrt(max(0.0, 1 - math.sqrt(E[row]))) if h['tier'] > 0 else 0.5 + 0.5 * (1 - E[row])
        a, b = h['a'] * rho, h['b'] * rho
        # the front half arc (facing -z) minus the capital river's block (0.25)
        arc = math.pi * (3 * (a + b) - math.sqrt((3 * a + b) * (a + 3 * b))) / 2 - 0.25
        need = sum(2 * p['r'] + 0.03 for p in m)
        slope_rep[row] += len(m)
        if need > arc: over.append((hid, row, round(need, 2), round(arc, 2)))
        for p in m: p['row'] = row
print(f"[Slopes {YEAR}] players by row {dict(sorted(slope_rep.items()))}; w_lo ${w_lo/1e3:.0f}K w_hi ${w_hi/1e6:.1f}M; rows too short: {over}")

# ---------------------------------------------------------------- crowns (SPEC2 3.5)
CROWN = {'internet': 6069, 'manufacturing': 3409, 'trade': 2654, 'insurance': 1557, 'software': 1177, 'banking': 800,
         'professional': 762, 'construction': 662, 'media_telecom': 365, 'health': 332, 'transport': 274, 'hospitality': 272,
         'legal': 271, 'oil_gas': 238, 'hardware': 234, 'agriculture': 177, 'utilities': 171, 'other_services': 157,
         'entertainment': 136, 'mining_metals': 67, 'finance': 22} if YEAR == 2025 else {}
CLOUD_Y = H['cloudY']
crowns = []
for hid, V in CROWN.items():
    if V < 100: continue
    h = HILL[hid]; r = 0.75 * math.sqrt(V / GDP)
    y = h['S'] + 0.35 + 0.8 * r
    crowns.append(dict(id=hid, V=V, r=r, x=h['x'], z=h['d'] - Z0, y=y, top=y + 0.70 * r))
# separate intersecting crowns in height (the smaller rises 0.15 at a time)
crowns.sort(key=lambda c: (-c['V'], c['id']))
lifts = 0
for k, c in enumerate(crowns):
    for o in crowns[:k]:
        while math.hypot(c['x'] - o['x'], c['z'] - o['z']) < c['r'] + o['r'] + 0.05 and abs(c['y'] - o['y']) < 0.25 * max(c['r'], o['r']) + 0.10:
            c['y'] += 0.15; c['top'] += 0.15; lifts += 1
if crowns:
    hi_c = max(crowns, key=lambda c: c['top'])
    print(f"[Crowns {YEAR}] {len(crowns)} crowns >= $100B; largest internet r {crowns[0]['r']:.3f} at y {crowns[0]['y']:.2f}; "
          f"highest top {hi_c['id']} {hi_c['top']:.2f} (cloud level {CLOUD_Y:.2f}, limit {CLOUD_Y-0.30:.2f}); lifts {lifts}")

# ---------------------------------------------------------------- clouds (SPEC2 3.6)
grp = CIR['groups'] if isinstance(CIR.get('groups'), list) else CIR.get('groups', {}).get('list', [])
top1 = [g for g in grp if g.get('id') == 'top1'][0] if grp else {}
inc = {i['id']: i['value'] if 'value' in i else i.get('amount', 0) for i in CIR.get('income', [])} if isinstance(CIR.get('income'), list) else {}
owners = {}
for i in IND:
    if i['tier'] == 'gov' or i['id'] == 'real_estate': continue
    va = dict(i['valueAdded'])[YEAR]
    owners[i['id']] = va * max(0, 1 - i['compShare'] - i['taxShare'] - i['depShare'])
INC = CIR['income']
eq = top1.get('equityShare', 0.5) * INC['dividends']; re_ = top1.get('realEstateShare', 0.1) * INC['rental']; it = top1.get('interestShare', 0.2) * INC['netInterest']
tot_o = sum(owners.values())
mix = {k: eq * v / tot_o for k, v in owners.items()}
mix['real_estate'] = mix.get('real_estate', 0) + re_; mix['banking'] = mix.get('banking', 0) + it
s = sum(mix.values()); mix = {k: v / s for k, v in mix.items()}
print('[Mix top1] ' + ', '.join(f'{k} {v:.2f}' for k, v in sorted(mix.items(), key=lambda kv: -kv[1])[:8]))
clouds = []
for p in [p for p in players if p['zone'] == 'cloud']:
    home = p['key'].split('|')[1].split(':')[1]
    hold = collections.Counter()
    for q in p['members']:
        ind = int(rec[q]['industry']); ind = ind if ind >= 0 else former(q)
        h = 0.15 if ind >= 0 else 0
        for k, v in mix.items(): hold[k] += float(rec[q]['wealth']) * (1 - h) * v
        if ind >= 0: hold[IDS[ind]] += float(rec[q]['wealth']) * h
    W = sum(hold.values())
    cx = sum(v * HILL[k]['x'] for k, v in hold.items()) / W
    cz = sum(v * (HILL[k]['d'] - Z0) for k, v in hold.items()) / W
    R = 0.75 * math.sqrt(W * 1e5 / 1e9 / GDP)
    clouds.append(dict(key=p['key'], x=cx, z=cz, x0=cx, z0=cz, R=R, W=W * 1e5 / 1e9))
for it_ in range(60):
    for a in range(len(clouds)):
        for b in range(a + 1, len(clouds)):
            A, B = clouds[a], clouds[b]
            dx, dz = B['x'] - A['x'], B['z'] - A['z']; d = math.hypot(dx, dz)
            need = A['R'] + B['R'] + 0.05
            if d < need:
                ux, uz = (dx / d, dz / d) if d > 1e-9 else (1.0, 0.0)
                sh = (need - d) / 2
                A['x'] -= ux * sh; A['z'] -= uz * sh; B['x'] += ux * sh; B['z'] += uz * sh
        for c in crowns:
            A = clouds[a]
            dx, dz = A['x'] - c['x'], A['z'] - c['z']; d = math.hypot(dx, dz)
            need = A['R'] + c['r'] + 0.10
            if d < need:
                ux, uz = (dx / d, dz / d) if d > 1e-9 else (1.0, 0.0)
                A['x'] += ux * (need - d); A['z'] += uz * (need - d)
        A = clouds[a]
        A['x'] = min(7 - A['R'], max(-7 + A['R'], A['x'])); A['z'] = min(Z0 - A['R'], max(-Z0 + A['R'], A['z']))
mind = min((math.hypot(a['x'] - b['x'], a['z'] - b['z']) - a['R'] - b['R'] for i, a in enumerate(clouds) for b in clouds[i + 1:]), default=9)
print(f"[Clouds {YEAR}] " + '; '.join(f"{c['key']} ${c['W']/1000:.1f}T R {c['R']:.2f} at ({c['x']:+.2f}, {c['z']:+.2f}) "
                                       f"shift {math.hypot(c['x']-c['x0'], c['z']-c['z0']):.2f}" for c in clouds) + f"; min gap {mind:.2f}")
json.dump(dict(players=[{k: v for k, v in p.items() if k != 'members'} | dict(n=len(p['members'])) for p in players],
               lakes=lakes, crowns=crowns, clouds=clouds), open(f'{HERE}/stand-{YEAR}.json', 'w'), indent=1)

# ================================================================ SPEC2 rev 2 additions (the critique's fixes)
print("\n---- rev 2 ----")
# controller lines and their own industries
ctrl = sorted(controllers, key=lambda p: (-float(rec[p]['wealth']), p))
own_ind = {}
for p in ctrl:
    r = rec[p]; ind = int(r['industry']); ind = ind if ind >= 0 else former(p)
    own_ind[p] = IDS[ind] if ind >= 0 else None
    print(f"  controller {p}: wealth ${float(r['wealth'])/1e6:.0f}M se {r['se']} business {float(r['business'])/1e3:.0f}K own industry {own_ind[p]} careerSe {career_se(p)}")

# ---- crown seats: Hamilton apportionment of the controller lines over the crowns by V_k; own industry, then own tier
TIER = {i['id']: ['gov', 'raw', 'make', 'services', 'tech'].index(i['tier']) for i in IND}
cr = [c for c in crowns]
Vtot = sum(c['V'] for c in cr); n = len(ctrl)
quota = {c['id']: n * c['V'] / Vtot for c in cr}
seats = {k: int(math.floor(q)) for k, q in quota.items()}
left = n - sum(seats.values())
for k in sorted(quota, key=lambda k: (-(quota[k] - seats[k]), -CROWN[k], k))[:left]: seats[k] += 1
print("[Seats] " + ', '.join(f"{k} {v}" for k, v in sorted(seats.items(), key=lambda kv: -CROWN[kv[0]]) if v))
open_ = []  # list of open seat industries, by V desc (repeated)
for k in sorted(seats, key=lambda k: (-CROWN[k], k)): open_ += [k] * seats[k]
seat_of = {}
for p in ctrl:  # pass 1 own industry
    if own_ind[p] in open_: seat_of[p] = own_ind[p]; open_.remove(own_ind[p])
for p in ctrl:  # pass 2 same tier
    if p in seat_of or own_ind[p] is None: continue
    c = [k for k in open_ if TIER[k] == TIER[own_ind[p]]]
    if c: seat_of[p] = c[0]; open_.remove(c[0])
for p in ctrl:  # pass 3 the rest
    if p not in seat_of: seat_of[p] = open_.pop(0)
own_match = sum(1 for p in ctrl if seat_of[p] == own_ind[p]); tier_match = sum(1 for p in ctrl if own_ind[p] and TIER[seat_of[p]] == TIER[own_ind[p]])
print("[Seated] " + '; '.join(f"{p} ({own_ind[p]}) -> {seat_of[p]}" for p in ctrl) + f"; own industry {own_match}/{n}, own tier {tier_match}/{n}")
print(f"[Crown players] {len(set(seat_of.values()))} (one per seated crown; the size test does not apply to crown cells)")

# ---- the controlled share of each crowned hill's payouts (pass-through)
mcap, pbe = CIR['marketCapTotal'], 16000.0
own_v = {h['id']: h['owners'] * h['va'] for h in H['hills'] if h['tier'] > 0}
so = sum(own_v.values())
named = collections.Counter()
for x_ in CIR['capture']: named[x_['industry']] += x_['marketCap']
E_k = {k: max(named[k], mcap * own_v[k] / so) + pbe * own_v[k] / so for k in own_v}   # the hill's equity value (estimate)
cshare = {c['id']: min(1.0, c['V'] / E_k[c['id']]) for c in cr}
print("[Controlled share c_k] " + ', '.join(f"{k} {v:.2f}" for k, v in sorted(cshare.items(), key=lambda kv: -CROWN[kv[0]])))

# ---- the common crown band
BASE = float(os.environ.get('CROWN_BASE', '3.55')); TOPMAX = 4.20
maxS = max(h['S'] for h in H['hills'])
cb = []
for c in sorted(cr, key=lambda c: (-c['V'], c['id'])):
    y = BASE + 0.8 * c['r']
    cb.append(dict(c, y=y, top=y + 0.70 * c['r']))
lifts = 0; moved = 0
for k, c in enumerate(cb):
    for it in range(40):
        clash = [o for o in cb[:k] if math.hypot(c['x'] - o['x'], c['z'] - o['z']) < c['r'] + o['r'] + 0.05 and not (c['y'] >= o['top'] + 0.03 or o['y'] >= c['top'] + 0.03)]
        if not clash: break
        if c['top'] + 0.15 <= TOPMAX: c['y'] += 0.15; c['top'] += 0.15; lifts += 1
        else:
            o = clash[0]; dx, dz = c['x'] - o['x'], c['z'] - o['z']; d = math.hypot(dx, dz) or 1
            need = c['r'] + o['r'] + 0.05 - d; c['x'] += dx / d * need; c['z'] += dz / d * need; moved += 1
print(f"[Crown band] base {BASE:.2f} (highest summit {maxS:.2f}); bottoms {min(c['y'] for c in cb):.2f}-{max(c['y'] for c in cb):.2f}, tops max {max(c['top'] for c in cb):.2f}; lifts {lifts}, moved outward {moved}; stems: longest {max(c['y']-HILL[c['id']]['S'] for c in cb):.2f} ({max(cb, key=lambda c: c['y']-HILL[c['id']]['S'])['id']})")
for c in cb[:6]: print(f"   {c['id']}: r {c['r']:.3f} y {c['y']:.2f} top {c['top']:.2f} at ({c['x']:+.2f}, {c['z']:+.2f})")
json.dump(dict(crowns=cb, seats=seat_of and {str(p): k for p, k in seat_of.items()}, cshare=cshare), open(f'{HERE}/crowns3-{YEAR}.json', 'w'), indent=1)

# ---- clouds: the centre of each cloud's overweights (holdings minus the market mix, positive part), between the hills
def rho(h, x, z):
    return math.hypot((x - h['x']) / h['a'], (z - (h['d'] - Z0)) / h['b'])
def between(x, z, lim=0.8):
    return all(rho(h, x, z) >= lim for h in H['hills'])
cl3 = []
for p in [p for p in players if p['zone'] == 'cloud']:
    hold = collections.Counter()
    for q in p['members']:
        ind = int(rec[q]['industry']); ind = ind if ind >= 0 else former(q)
        h = 0.15 if ind >= 0 else 0
        for k, v in mix.items(): hold[k] += float(rec[q]['wealth']) * (1 - h) * v
        if ind >= 0: hold[IDS[ind]] += float(rec[q]['wealth']) * h
    W = sum(hold.values())
    ow = {k: max(0.0, hold[k] / W - mix.get(k, 0)) for k in IDS}
    so_ = sum(ow.values())
    if so_ > 1e-6:
        cx = sum(v * HILL[k]['x'] for k, v in ow.items()) / so_; cz = sum(v * (HILL[k]['d'] - Z0) for k, v in ow.items()) / so_
        top3 = sorted(ow.items(), key=lambda kv: -kv[1])[:3]
    else:
        cx = sum(v * HILL[k]['x'] for k, v in hold.items()) / W; cz = sum(v * (HILL[k]['d'] - Z0) for k, v in hold.items()) / W; top3 = []
    R = 0.75 * math.sqrt(W * 1e5 / 1e9 / GDP)
    cl3.append(dict(key=p['key'], x=cx, z=cz, x0=cx, z0=cz, R=R, W=W * 1e5 / 1e9, over=[(k, round(v, 3)) for k, v in top3]))
# between the hills: nearest raster point (0.05) within 2.5 u where every hill's rho >= 0.8
for c in cl3:
    if between(c['x'], c['z']): continue
    best = None
    for i in range(-50, 51):
        for j in range(-50, 51):
            x, z = c['x'] + 0.05 * i, c['z'] + 0.05 * j
            if abs(x) > 7 - c['R'] or abs(z) > Z0 - c['R'] or not between(x, z): continue
            d = math.hypot(x - c['x'], z - c['z'])
            if best is None or d < best[0]: best = (d, x, z)
    if best: c['x'], c['z'] = best[1], best[2]
for it_ in range(60):
    for a in range(len(cl3)):
        for c in cb:
            A = cl3[a]
            dx, dz = A['x'] - c['x'], A['z'] - c['z']; d = math.hypot(dx, dz)
            need = A['R'] + c['r'] + 0.10
            if d < need:
                ux, uz = (dx / d, dz / d) if d > 1e-9 else (1.0, 0.0)
                A['x'] += ux * (need - d); A['z'] += uz * (need - d)
        for b in range(a + 1, len(cl3)):
            A, B = cl3[a], cl3[b]
            dx, dz = B['x'] - A['x'], B['z'] - A['z']; d = math.hypot(dx, dz)
            need = A['R'] + B['R'] + 0.05
            if d < need:
                ux, uz = (dx / d, dz / d) if d > 1e-9 else (1.0, 0.0)
                sh = (need - d) / 2
                A['x'] -= ux * sh; A['z'] -= uz * sh; B['x'] += ux * sh; B['z'] += uz * sh
def gap_of(c):
    """the gap a cloud sits in: the two nearest hills by rho (sorted ids)"""
    hs = sorted(H['hills'], key=lambda h: (rho(h, c['x'], c['z']), h['id']))[:2]
    return tuple(sorted(h['id'] for h in hs))
xs = [c['x'] for c in cl3]; zs = [c['z'] for c in cl3]
print(f"[Clouds rev2] " + '; '.join(f"{c['key']} ${c['W']/1000:.1f}T R {c['R']:.2f} centroid ({c['x0']:+.2f}, {c['z0']:+.2f}) -> ({c['x']:+.2f}, {c['z']:+.2f}) over {c['over']} gap {gap_of(c)}" for c in cl3))
mcr = min(math.hypot(a['x']-c['x'], a['z']-c['z']) - a['R'] - c['r'] for a in cl3 for c in cb)
print(f"   min clearance to a crown ring {mcr:.2f}; shift from the overweight centre max {max(math.hypot(c['x']-c['x0'], c['z']-c['z0']) for c in cl3):.2f}")
print(f"   span x {max(xs)-min(xs):.2f} u, z {max(zs)-min(zs):.2f} u; distinct gaps {len(set(gap_of(c) for c in cl3))}; all between hills: {all(between(c['x'], c['z']) for c in cl3)}")

# ---- floor rows vs wealth
fl = [p for p in players if p['zone'] == 'floor']
byg = collections.defaultdict(list)
for p in fl: byg[p['group']].append(p)
print("[Floor wealth by group] " + ', '.join(f"{g} ${sum(q['wealth']*len(q['members']) for q in v)/sum(len(q['members']) for q in v)/1e3:.0f}K" for g, v in sorted(byg.items(), key=lambda kv: BAND[kv[0]])))
viol = 0; pairs = 0
for hid in IDS:
    ps = [p for p in fl if p['hill'] == hid]
    for a in ps:
        for b in ps:
            if BAND[a['group']] < BAND[b['group']]:
                pairs += 1
                if a['wealth'] < b['wealth']: viol += 1
print(f"   within a hill's players: band order vs mean wealth inversions {viol} of {pairs} pairs")
# wealth group 2 (top 10%) share by group
wg = collections.defaultdict(lambda: [0, 0])
for p in fl:
    for q in p['members']:
        wg[p['group']][0] += 1; wg[p['group']][1] += rec[q]['wg'] == '2'
print("   top-10% (wg 2) share of members: " + ', '.join(f"{g} {v[1]/v[0]:.0%}" for g, v in sorted(wg.items(), key=lambda kv: BAND[kv[0]])))
# dependents
dep = [p for p in adults if rec[p]['dep'] == '1']
alive_par = [p for p in dep if (int(rec[p]['mother']) in rec or int(rec[p]['father']) in rec)]
same = sum(1 for p in alive_par if player_of.get(p) == (player_of.get(int(rec[p]['mother'])) or player_of.get(int(rec[p]['father']))))
print(f"[Dependents] {len(dep)}; with a living parent {len(alive_par)}; in the same player as the parent {same}")

#!/usr/bin/env python3
"""Top view of the SPEC2 merged layout (2025): hills (cap, upkeep, wage zones), treads with the four floor rows, the valley
rivers with their lakes, gorges, floor discs, slope ledges, crowns (rings) and clouds (lenses). Illustrative only."""
import json, math
import matplotlib; matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import Ellipse, Circle, Rectangle
H = json.load(open('hills-2025.json')); S = json.load(open('stand-2025.json'))
z0 = H['depth'] / 2
fig, ax = plt.subplots(figsize=(10, 11), dpi=90); fig.patch.set_facecolor('black'); ax.set_facecolor('black')
TINT = ['#2b3140', '#3a2826', '#35322b', '#29313a', '#322c3d']
for t, tr in enumerate(H['treads']):
    ax.add_patch(Rectangle((-7, tr['lip'] - z0), 14, 1.2, color=TINT[t], alpha=0.9, lw=0))
    ax.plot([-6.7, 6.7], [tr['lip'] + 0.6 - z0] * 2, color='#d9a0c8', lw=1.2, alpha=0.6)
    for v in (1.07, 0.87, 0.33, 0.13): ax.plot([-6.7, 6.7], [tr['lip'] + v - z0] * 2, color='#5a7fb8', lw=0.4, ls=':')
for h in H['hills']:
    z = h['d'] - z0
    for rho, col in ((1.0, '#6699ff'), (math.sqrt(h['owners'] + h['upkeep']), '#9aa3b5'), (math.sqrt(h['owners']), '#ffc44d')):
        ax.add_patch(Ellipse((h['x'], z), 2 * h['a'] * rho, 2 * h['b'] * rho, color=col, alpha=0.55, lw=0))
    ax.text(h['x'], z, h['id'].replace('_', ' '), color='white', fontsize=6, ha='center', va='center')
for k, l in S['lakes'].items():
    t = [h for h in H['hills'] if h['id'] == k][0]['tier']
    ax.add_patch(Ellipse((l['x'], H['treads'][t]['lip'] + 0.6 - z0), 2 * l['a'], 2 * l['b'], fc='#cfe0ff', alpha=0.35, ec='#ff8fc8', lw=0.8))
for t, xs in H['gorges'].items():
    for x in xs:
        z1 = H['treads'][int(t) - 1]['lip'] + 0.6 - z0; z2 = H['treads'][int(t)]['lip'] + 0.6 - z0
        ax.plot([x, x], [z1, z2], color='#d9a0c8', lw=1.0, alpha=0.5, ls='--')
for p in S['players']:
    if 'x' in p and 'z' in p:
        ax.add_patch(Circle((p['x'], p['z']), p['r'], fc='#4f7ddb', alpha=0.7, ec='#9fc0ff', lw=0.4))
for c in S['crowns']:
    ax.add_patch(Circle((c['x'], c['z']), c['r'], fill=False, ec='#ffcf40', lw=1.2))
for c in S['clouds']:
    ax.add_patch(Circle((c['x'], c['z']), c['R'], fc='#e8ddc0', alpha=0.18, ec='#ffcf40', lw=0.8, ls='--'))
ax.set_xlim(-7.2, 7.2); ax.set_ylim(-z0 - 0.2, z0 + 0.2); ax.set_aspect('equal'); ax.tick_params(colors='grey')
ax.set_title('SPEC2 merged layout 2025 (top; +z away from the road)', color='white')
fig.savefig('sketch-top-2025.png', facecolor='black', bbox_inches='tight')

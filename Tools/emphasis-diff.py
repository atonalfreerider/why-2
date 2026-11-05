#!/usr/bin/env python3
"""The emphasis table's pixel gate (WP07, spec 3.1): every smoke frame of a run against the base commit's, beside the
run-to-run noise.

The emphasis table moved every hill layer's alpha into one table without changing a value, so a run of the new build
must draw what the base commit drew, up to the noise two runs of the same build differ by (flow pulses and glints caught
at another phase). For every frame in both folders (economy-*.png by default) this measures the share of pixels where any
channel differs by more than 8/255 (ffmpeg blend=difference, then that threshold: the measure the noise file was made
with), prints it beside the frame's noise, and fails the frame when its change exceeds its noise by 0.5 percentage points
or more. A frame missing from the new run fails too; a frame only the new run has (the emphasis sequence's) is listed.

    python3 Tools/emphasis-diff.py BASE NEW --noise NOISE.json [--glob 'economy-*.png'] [--gate 0.5] [--out report.json]

    BASE    the base commit's frames (Shots/base1)
    NEW     the new build's frames, from the same smoke command
    NOISE   per-frame noise: {"frames": {"economy-view-overview.png": 0.4087, ...}} in percent, as the share of pixels two
            runs of the base build differ in (base1 against base2: this script with --noise-of base2 writes one)

The crest check of -smokeextra emphasis (a part lit by its ids brightens only its own pixels):

    python3 Tools/emphasis-diff.py --crest PLAYER.LOG NEW

    reads the box the player logged ("[Why] Emphasis highlight wave.crest ...: ... box (x X0-X1, y Y0-Y1, ...") and counts
    the pixels brighter by more than 8/255 in economy-emphasis-crest-on.png than in -off.png (ffmpeg blend=subtract)
    inside and outside it: it fails when fewer than 200 brightened inside or more than 0.01% of the frame outside.

Exit code 0 when every frame passes, 1 otherwise. Needs ffmpeg on the PATH; nothing else.
"""
import argparse
import fnmatch
import json
import os
import re
import subprocess
import sys

THRESH = 8


def lut(t):
    return f"if(gt(val\\,{t})\\,255\\,0)"


def changed(a, b, thresh=THRESH):
    """Percent of pixels where any channel of a and b differs by more than thresh/255 (None when ffmpeg fails)."""
    l = lut(thresh)
    cmd = ['ffmpeg', '-loglevel', 'error', '-i', a, '-i', b, '-filter_complex',
           f"[0]format=rgb24[x];[1]format=rgb24[y];[x][y]blend=all_mode=difference,lutrgb=r='{l}':g='{l}':b='{l}',format=gray",
           '-f', 'rawvideo', '-']
    p = subprocess.run(cmd, capture_output=True)
    if p.returncode != 0 or not p.stdout:
        print(f"ffmpeg failed on {os.path.basename(a)}: {p.stderr.decode(errors='replace')[:200]}", file=sys.stderr)
        return None
    data = p.stdout
    lit = len(data) - data.count(0)
    return 100.0 * lit / len(data)


def frames(folder, pattern):
    return sorted(n for n in os.listdir(folder) if n.endswith('.png') and fnmatch.fnmatch(n, pattern))


def gate(args):
    noise = {}
    if args.noise:
        noise = json.load(open(args.noise)).get('frames', {})
    base, new = frames(args.base, args.glob), set(frames(args.new, args.glob))
    rows, failed, missing = [], [], []
    for n in base:
        if n not in new:
            missing.append(n)
            continue
        c = changed(os.path.join(args.base, n), os.path.join(args.new, n), args.thresh)
        if c is None:
            failed.append(n)
            rows.append((n, None, noise.get(n), None, 'ERROR'))
            continue
        z = noise.get(n)
        excess = c - (z if z is not None else 0.0)
        verdict = 'FAIL' if excess >= args.gate else 'ok'
        if verdict == 'FAIL':
            failed.append(n)
        rows.append((n, c, z, excess, verdict + ('' if z is not None else ' (no noise entry)')))
    rows.sort(key=lambda r: -(r[3] if r[3] is not None else 1e9))
    print(f"{'change':>8} {'noise':>8} {'excess':>8}  verdict  frame   (pixels with a channel more than {args.thresh}/255 apart; gate: excess >= {args.gate})")
    for n, c, z, e, v in rows:
        cs = f"{c:7.3f}%" if c is not None else "     n/a"
        zs = f"{z:7.3f}%" if z is not None else "     n/a"
        es = f"{e:+7.3f}" if e is not None else "     n/a"
        print(f"{cs} {zs} {es}   {v:<5}  {n}")
    for n in missing:
        print(f"     n/a      n/a      n/a   FAIL   {n} (missing from {args.new})")
    extra = sorted(new - set(base))
    for n in extra:
        print(f"     n/a      n/a      n/a   new    {n} (only in {args.new}, not gated)")
    worst = max((r[3] for r in rows if r[3] is not None), default=0.0)
    print(f"frames {len(rows)}, failed {len(failed) + len(missing)} (change - noise >= {args.gate}: {len(failed)}, missing: {len(missing)}), "
          f"largest excess {worst:+.3f} points, new frames {len(extra)}")
    if args.out:
        json.dump({'base': args.base, 'new': args.new, 'thresh': args.thresh, 'gate': args.gate,
                   'frames': {n: {'change': None if c is None else round(c, 4), 'noise': z, 'excess': None if e is None else round(e, 4), 'verdict': v}
                              for n, c, z, e, v in rows},
                   'missing': missing, 'new': extra}, open(args.out, 'w'), indent=1)
    return 0 if not failed and not missing else 1


def noise_of(args):
    """Writes the noise file: every frame of BASE against the same frame of a second run of the same build."""
    other = args.noise_of
    out = {}
    for n in frames(args.base, args.glob):
        p = os.path.join(other, n)
        if os.path.exists(p):
            c = changed(os.path.join(args.base, n), p, args.thresh)
            if c is not None:
                out[n] = round(c, 4)
    json.dump({'a': args.base, 'b': other, 'thresh': args.thresh, 'frames': dict(sorted(out.items(), key=lambda kv: -kv[1]))},
              open(args.out or 'noise.json', 'w'), indent=1)
    print(f"{len(out)} frames written to {args.out or 'noise.json'}")
    return 0


BOX = re.compile(r"\[Why\] Emphasis highlight wave\.crest .*?box \(x (\d+)-(\d+), y (\d+)-(\d+), grown \d+ px; (\d+)x(\d+)\)")


def crest(args):
    log, folder = args.crest
    text = open(log, errors='replace').read()
    m = None
    for m in BOX.finditer(text):
        pass
    if m is None:
        print(f"no '[Why] Emphasis highlight wave.crest ... box (...)' line in {log}: run with -smokeextra emphasis", file=sys.stderr)
        return 1
    x0, x1, y0, y1, w, h = map(int, m.groups())
    off, on = os.path.join(folder, 'economy-emphasis-crest-off.png'), os.path.join(folder, 'economy-emphasis-crest-on.png')
    l = lut(args.thresh)
    cmd = ['ffmpeg', '-loglevel', 'error', '-i', on, '-i', off, '-filter_complex',
           f"[0]format=rgb24[x];[1]format=rgb24[y];[x][y]blend=all_mode=subtract,lutrgb=r='{l}':g='{l}':b='{l}',format=gray",
           '-f', 'rawvideo', '-']
    p = subprocess.run(cmd, capture_output=True)
    if p.returncode != 0 or len(p.stdout) != w * h:
        print(f"ffmpeg failed or the frames are not {w}x{h}: {p.stderr.decode(errors='replace')[:200]}", file=sys.stderr)
        return 1
    data = p.stdout
    inside = outside = 0
    for row in range(h):
        line = data[row * w:(row + 1) * w]
        lit = w - line.count(0)
        if y0 <= row < y1:
            seg = line[x0:x1]
            a = len(seg) - seg.count(0)
            inside += a
            outside += lit - a
        else:
            outside += lit
    limit = 1e-4 * w * h
    ok = inside >= 200 and outside <= limit
    print(f"wave.crest lit by its ids: {inside} px brighter by more than {args.thresh}/255 inside its box (x {x0}-{x1}, y {y0}-{y1} from the "
          f"top left), {outside} outside (at most {limit:.0f}): {'PASS' if ok else 'FAIL'}")
    return 0 if ok else 1


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('base', nargs='?', help='the base commit\'s frames')
    ap.add_argument('new', nargs='?', help='the new build\'s frames')
    ap.add_argument('--noise', help='per-frame noise (percent) from two runs of the base build')
    ap.add_argument('--glob', default='economy-*.png')
    ap.add_argument('--thresh', type=int, default=THRESH, help='a channel counts as changed past this many of 255')
    ap.add_argument('--gate', type=float, default=0.5, help='a frame fails when its change exceeds its noise by this many points')
    ap.add_argument('--out', help='write the per-frame results (or, with --noise-of, the noise) as JSON')
    ap.add_argument('--noise-of', help='write the noise file instead: BASE against this second run of the same build')
    ap.add_argument('--crest', nargs=2, metavar=('LOG', 'FRAMES'), help='the -smokeextra emphasis check instead')
    args = ap.parse_args()
    if args.crest:
        return crest(args)
    if not args.base:
        ap.error('BASE is required')
    if args.noise_of:
        return noise_of(args)
    if not args.new:
        ap.error('NEW is required')
    return gate(args)


if __name__ == '__main__':
    sys.exit(main())

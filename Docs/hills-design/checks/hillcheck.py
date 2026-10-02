#!/usr/bin/env python3
"""hillcheck.py: the hill landscape's checks (SPEC2 11.8) on a harness log and its output folder.

usage:
  hillcheck.py <land.log> <OUT> [--expect wp0[,wp1,...]|all] [--log2 <second run's land.log>] [--only a,b.c]
               [--repo <worktree>] [-v]
  hillcheck.py --list

  <land.log>  stdout of the main economy run (WHY_ANCHORS_OUT=$OUT/anchors.json WHY_DUMP_LAND=1 ... --render all
              --out $OUT/land > $OUT/land.log)
  <OUT>       the output folder of 11.8: land/ portrait/ stairs/ rise/ phase/ mind-ow/ play/ lives0/ nobonds/ with their
              .log files next to them, anchors.json; optional: phi.log (WHY_ECON_PLAYFEED=0, the phi-only lives run),
              whycheck.log (stdout of whycheck.sh), why.log (a --scene why run without --quiet), panels.log (the
              scratch-harness run with Economy/UI), restore.log + restore/ (restore-bowl.sh at integration)
  --expect    the packages whose lines and checks must be real: a '(demo)' line of a listed package FAILs, of an unlisted
              one is INFO; an expected value tagged "from": "<wp>" in a module's JSON is checked only when <wp> is listed
              (INFO otherwise); a check owned by an unlisted package is INFO. 'all' = wp0..wp6 and the integration
              checks ('int'). wp0 is always included.
  --log2      a second run's main log (its render folders beside it): all.determinism
  --only      run only these checks: module names (hc_terrain, terrain), module.check (hc_people.stack), or check names
  --repo      the worktree (bowl files, tour.json); default: the log's '[harness] repo <path>' line

Prints 'hillcheck <module>.<check>: PASS|FAIL|INFO|SKIP (details)' per check; exit status 1 on any FAIL. A check whose
input file is missing is SKIP, unless its owner is in --expect and its Done-when needs the file (then FAIL).
"""
import argparse, importlib, json, os, re, sys, traceback

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from hc_common import (ALL_WP, Ctx, Missing, Res, SP, check_line)  # noqa: E402

MODULES = ['hc_terrain', 'hc_people', 'hc_mind', 'hc_smv', 'hc_views', 'hc_tour']


# --------------------------------------------------------------------------------------------- core (WP0) and all

def core_lines(ctx, res):
    """Every log line of 11.8 prints (stubs with '(demo)'): WP0's Done-when."""
    e = ctx.exp('hillcheck')
    missing, demo = [], []
    for item in e['lines']:
        files = item.get('files', ['land'])
        found, absent = None, []
        for fname in files:
            try:
                log = ctx.log(fname)
            except Missing:
                absent.append(fname + '.log')
                continue
            ln, _ = log.find(item['re'])
            if ln:
                found = ln
                break
        if found is None:
            missing.append(item['name'])
            res.bad(f"{item['name']} not printed" + (f" ({', '.join(absent)} missing)" if absent else ''))
        elif found.demo:
            demo.append(item['name'])
    res.note(f"{len(e['lines']) - len(missing)}/{len(e['lines'])} lines print, {len(demo)} still (demo)")
    if demo and ctx.verbose:
        res.note('demo: ' + ', '.join(demo))


def core_log(ctx, res):
    """WP0's own lines: Land checks (every stage n/n, PASS), Facts (unchanged in form), Society (checks 2/2)."""
    e = ctx.exp('hillcheck')
    ln, _ = check_line(ctx, res, e['landChecks'], 'Land checks')
    if ln is not None and not ln.demo:
        body = ln.body_after()
        got = dict((k, (int(a), int(b))) for k, a, b in re.findall(r"(\w+) (\d+)/(\d+)", body))
        for g in e['landChecks']['groups']:
            v = got.get(g['name'])
            if v is None:
                res.bad(f"Land checks: {g['name']} missing")
                continue
            res.ok(f"Land checks: {g['name']} {v[0]}/{v[1]} not all passing", v[0] == v[1] and v[1] > 0)
            res.ok(f"Land checks: {g['name']} {v[0]}/{v[1]} (want {g['want']})", f"{v[0]}/{v[1]}" == g['want'], '', g['from'])
        res.ok("Land checks: ends PASS", body.rstrip().endswith('PASS'), body[-40:])
    check_line(ctx, res, e['facts'], 'Facts')
    vals = {}
    ln, vals = check_line(ctx, res, e['society'], 'Society', vals)
    if ln is not None and not ln.demo:
        from hc_common import players_count
        n = players_count(ctx)
        if n is not None and 'players' in vals:
            res.eq('Society: players = the census', vals['players'], n, 'wp2')


def core_render(ctx, res):
    """All 18 presets render at 16:9 (land/) and 9:16 (portrait/) without an exception."""
    e = ctx.exp('hillcheck')
    for run in ('land', 'portrait'):
        log = ctx.log(run)
        exc = log.exceptions()
        res.ok(f"{run}.log: no exception", not exc, '; '.join(x.strip()[:100] for x in exc[:3]))
        missing = [p for p in e['presets'] if not all(ctx.has(os.path.join(run, p + s))
                                                      for s in ('.png', '.base.png', '.labels.json'))]
        res.ok(f"{run}/: every preset rendered", not missing, 'missing ' + ', '.join(missing[:8]) + (' ...' if len(missing) > 8 else ''))
        m = log.harness(r"done in [\d.]+ s \((\d+) warnings, (\d+) errors logged\)")
        if m:
            res.note(f"{run}: {m.group(1)} warnings, {m.group(2)} errors logged")


def core_dump(ctx, res):
    """WHY_DUMP_LAND: the Frame lines (WP0's wiring) at every preset's OnFocus; the Stand dumps (WP2)."""
    e = ctx.exp('hillcheck')
    log = ctx.log('land')
    presets = [p for p in e['presets'] if p in log.frames]
    res.ok("Frame lines printed", bool(log.frames), 'no "[Why] Frame <preset>: <key> in|out x0 y0 x1 y1" line (WHY_DUMP_LAND=1)')
    res.note(f"Frame lines for {len(presets)}/{len(e['presets'])} presets, "
             f"{sum(len(v) for v in log.frames.values())} items")
    for p in e['landViews']:
        res.ok(f"Stand screen {p} printed", p in log.stand, '', 'wp2')
    res.ok("Stand crowns crowns printed", 'crowns' in log.crowns, '', 'wp2')


def core_budget(ctx, res):
    """The scene's load (harness: first Prepare to last Upload) <= 2.9 s (11.6)."""
    e = ctx.exp('hillcheck')
    m = ctx.log('land').harness(r"build total (\d+) ms")
    if not m:
        res.bad("'[harness] ... build total <n> ms' not printed")
        return
    res.within('load ms', int(m.group(1)), None, e['loadMsMax'])


def all_why(ctx, res):
    """whycheck.sh: 14 presets, 0 differ; the Why log's SmvLayer.Prepare line = HEAD's and no SmvBonds line."""
    e = ctx.exp('hillcheck')['why']
    wc, wl = os.path.join(ctx.outdir, 'whycheck.log'), os.path.join(ctx.outdir, 'why.log')
    if not os.path.exists(wc) and not os.path.exists(wl):
        raise Missing('whycheck.log (stdout of whycheck.sh) or why.log', done=False)
    if os.path.exists(wc):
        txt = open(wc, errors='replace').read()
        m = re.search(r"\[whycheck\] (\d+) presets, (\d+) differ: (PASS|FAIL)", txt)
        res.ok('whycheck "14 presets, 0 differ: PASS"', m is not None and m.groups() == ('14', '0', 'PASS'),
               m.group(0) if m else 'no [whycheck] summary')
    else:
        res.note('no whycheck.log')
    if os.path.exists(wl):
        from hc_common import Log
        log = Log(wl)
        ln, m = log.find(r"^SmvLayer\.Prepare\b")
        res.ok('why.log: SmvLayer.Prepare printed', ln is not None)
        if ln:
            res.eq('why.log: SmvLayer.Prepare', ln.body_after(m.end()), e['smvBody'])
        res.ok('why.log: no SmvBonds line', log.find(r"^SmvBonds\b")[0] is None)
    else:
        res.note('no why.log (a --scene why run without --quiet)')


def _norm(s):
    s = re.sub(r"-?\d[\d.,]*\s?ms\b", "<ms>", s)
    s = re.sub(r"\(\d+(\.\d+)? s\)", "(<s>)", s)
    return s


def _det_lines(log):
    out = [_norm(ln.text) for ln in log.lines if not ln.text.startswith('economic lives:')]
    rep = log.lives_report()
    if rep:
        out += rep[1:]
    return out


def all_determinism(ctx, res):
    """--log2: every checksum and log line equal (timings aside); the .base.png files byte-identical."""
    if not ctx.log2:
        raise Missing('--log2 <second run log>', done=False)
    from hc_common import Log
    a, b = ctx.log('land'), Log(ctx.log2)
    la, lb = _det_lines(a), _det_lines(b)
    ca = re.findall(r"checksum (-?[\d.]+)", '\n'.join(la))
    cb = re.findall(r"checksum (-?[\d.]+)", '\n'.join(lb))
    res.ok(f"checksums equal ({len(ca)} vs {len(cb)})", ca == cb)
    if la != lb:
        sa, sb = set(la), set(lb)
        only_a = [x for x in la if x not in sb][:3]
        only_b = [x for x in lb if x not in sa][:3]
        res.bad(f"log lines differ: {len(la)} vs {len(lb)} lines; first only in 1: {[x[:80] for x in only_a]}; "
                f"only in 2: {[x[:80] for x in only_b]}")
    res.note(f"{len(la)} lines, {len(ca)} checksums")
    d1 = os.path.join(os.path.dirname(os.path.abspath(ctx.main_log)), os.path.splitext(os.path.basename(ctx.main_log))[0])
    d2 = os.path.join(os.path.dirname(os.path.abspath(ctx.log2)), os.path.splitext(os.path.basename(ctx.log2))[0])
    if os.path.isdir(d1) and os.path.isdir(d2):
        names = sorted(f for f in os.listdir(d1) if f.endswith('.base.png'))
        diff = [n for n in names if not os.path.exists(os.path.join(d2, n))
                or open(os.path.join(d1, n), 'rb').read() != open(os.path.join(d2, n), 'rb').read()]
        res.ok(f"{len(names)} .base.png byte-identical", not diff and names, ', '.join(diff[:6]) or 'no renders')
    else:
        res.note(f"render folders not both present ({d1}, {d2}); .base.png not compared")


CORE = [('core', 'lines', 'wp0', core_lines), ('core', 'log', 'wp0', core_log), ('core', 'render', 'wp0', core_render),
        ('core', 'dump', 'wp0', core_dump), ('core', 'budget', 'wp0', core_budget),
        ('all', 'why', 'any', all_why), ('all', 'determinism', 'any', all_determinism)]


def registry():
    """[(module, check, owner, fn)] of the driver and every module, in order."""
    out = list(CORE[:5])
    for name in MODULES:
        mod = importlib.import_module(name)
        for chk, owner, fn in mod.CHECKS:
            out.append((name, chk, owner, fn))
    out += CORE[5:]
    return out


def selected(only, module, check):
    if not only:
        return True
    short = module[3:] if module.startswith('hc_') else module
    for o in only:
        if o in (module, short, check, f"{module}.{check}", f"{short}.{check}"):
            return True
    return False


def run_one(ctx, module, check, owner, fn):
    res = Res(owner)
    status, detail = None, ''
    try:
        fn(ctx, res)
    except Missing as e:
        status = 'FAIL' if (e.done and ctx.real(owner)) else 'SKIP'
        detail = f"missing {e.path}" + (" (its Done-when needs it)" if status == 'FAIL' else '')
    except Exception as e:  # a check never kills the driver
        tb = traceback.extract_tb(sys.exc_info()[2])[-1]
        res.bad(f"crashed: {type(e).__name__}: {e} at {os.path.basename(tb.filename)}:{tb.lineno}")
    if status is None:
        req = [t for t, f in res.items if ctx.real(f or owner)]
        info = [t for t, f in res.items if not ctx.real(f or owner)]
        if not ctx.real(owner):
            status, req, info = 'INFO', [], [t for t, _ in res.items]
        else:
            status = 'FAIL' if req else 'PASS'
        parts = list(res.notes)
        if req:
            parts.append('; '.join(req[:8]) + (f" ... (+{len(req) - 8})" if len(req) > 8 else ''))
        if info:
            n = 4 if status != 'INFO' else 8
            parts.append(('not yet required: ' if status != 'INFO' else '') + '; '.join(info[:n])
                         + (f" ... (+{len(info) - n})" if len(info) > n else ''))
        if not parts and status == 'PASS':
            parts.append('ok')
        detail = '; '.join(parts)
    print(f"hillcheck {module}.{check}: {status}" + (f" ({detail})" if detail else ''))
    sys.stdout.flush()
    return status


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('log', nargs='?')
    ap.add_argument('outdir', nargs='?')
    ap.add_argument('--expect', default='wp0')
    ap.add_argument('--log2', default='')
    ap.add_argument('--only', default='')
    ap.add_argument('--repo', default='')
    ap.add_argument('--list', action='store_true')
    ap.add_argument('-v', '--verbose', action='store_true')
    args = ap.parse_args()
    reg = registry()
    if args.list:
        for module, check, owner, fn in reg:
            doc = (fn.__doc__ or '').strip().split('\n')[0]
            print(f"{module + '.' + check:28s} {owner:4s}  {doc}")
        return 0
    if not args.log or not args.outdir:
        ap.error('<land.log> and <OUT> are required (or --list)')
    if args.expect.strip() == 'all':
        expect = ALL_WP + ['int']
    else:
        expect = [x.strip() for x in args.expect.split(',') if x.strip()]
        bad = [x for x in expect if x not in ALL_WP + ['int']]
        if bad:
            ap.error(f"unknown packages in --expect: {bad} (wp0..wp6, int, all)")
    if 'wp0' not in expect:
        expect = ['wp0'] + expect
    ctx = Ctx(args.log, os.path.abspath(args.outdir), expect, repo=args.repo or None,
              log2=args.log2 or None, verbose=args.verbose)
    only = [x.strip() for x in args.only.split(',') if x.strip()]
    counts = {'PASS': 0, 'FAIL': 0, 'INFO': 0, 'SKIP': 0}
    for module, check, owner, fn in reg:
        if selected(only, module, check):
            counts[run_one(ctx, module, check, owner, fn)] += 1
    print(f"hillcheck: {sum(counts.values())} checks: {counts['PASS']} PASS, {counts['FAIL']} FAIL, {counts['INFO']} INFO, "
          f"{counts['SKIP']} SKIP (expect {','.join(expect)})")
    return 1 if counts['FAIL'] else 0


if __name__ == '__main__':
    sys.exit(main())

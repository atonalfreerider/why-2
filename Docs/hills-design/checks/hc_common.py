"""hc_common.py: what hillcheck.py and its modules share (SPEC2 11.8).

The context (the logs of the harness output folder, its renders, labels and dump lines), the result of one check (its
problems, each tagged with the work package its expected value comes from) and the helpers: log line lookup, the generic
JSON field engine, money parsing, PNG / HSV, labels.json, Frame / Stand dump parsing.
"""
import json, os, re

SP = '/tmp/claude-0/-home-user-why-2/dc9bf553-91de-54d2-9f1f-9831e1f24cfa/scratchpad'
HERE = os.path.dirname(os.path.abspath(__file__))
ALL_WP = ['wp0', 'wp1', 'wp2', 'wp3', 'wp4', 'wp5', 'wp6']
# the harness runs of 11.8: <name>.log next to the folder <name>/ in $OUT ('land' is the main log given on the command line)
RUNS = ['land', 'portrait', 'stairs', 'rise', 'phase', 'mind-ow', 'play', 'lives0', 'nobonds']


class Missing(Exception):
    """An input file is missing. done = the check owner's Done-when needs it (then FAIL when the owner is expected)."""

    def __init__(self, path, done=True):
        super().__init__(path)
        self.path, self.done = path, done


# ------------------------------------------------------------------------------------------------------------ numbers

def money_b(text):
    """'$15.73T' / '$928B' / '$22.0K' / '$1.2M' / '15.73T' -> dollars in $B (float), None when unparsable."""
    if text is None:
        return None
    m = re.match(r"^\s*[≈~]?\s*-?\$?\s*(-?[\d,]*\.?\d+)\s*([TBMK]?)\s*$", text.replace('−', '-'))
    if not m:
        return None
    v = float(m.group(1).replace(',', ''))
    return v * {'T': 1000.0, 'B': 1.0, 'M': 1e-3, 'K': 1e-6, '': 1.0}[m.group(2)]


def to_num(text):
    if text is None:
        return None
    t = text.replace(',', '').replace('−', '-').replace('+', '').strip()
    try:
        return float(t)
    except ValueError:
        return None


def fmt(v):
    if isinstance(v, float):
        return f"{v:g}"
    return str(v)


# ------------------------------------------------------------------------------------------------------------ logs

class Line:
    """One '[Why] ...' line: text = everything after '[Why] ', demo = carries '(demo)'."""

    def __init__(self, text, index):
        self.text, self.index = text, index
        self.demo = '(demo)' in text

    def body_after(self, m_end=0):
        """The text after the first ': ' at or after m_end (the line's payload), or the whole rest."""
        k = self.text.find(': ', max(0, m_end - 1))
        return self.text[k + 2:] if k >= 0 else self.text[m_end:]


FRAME = re.compile(r"^Frame (\S+?)(?: \(demo\))?: (\S+) (in|out) (-?[\d.]+) (-?[\d.]+) (-?[\d.]+) (-?[\d.]+)"
                   r"(?: wy (-?[\d.]+) (-?[\d.]+))?")


class Frame:
    def __init__(self, key, inside, x0, y0, x1, y1, wy0=None, wy1=None):
        self.key, self.inside = key, inside
        self.x0, self.y0, self.x1, self.y1 = min(x0, x1), min(y0, y1), max(x0, x1), max(y0, y1)
        self.wy0, self.wy1 = wy0, wy1

    @property
    def cx(self):
        return (self.x0 + self.x1) / 2

    @property
    def cy(self):
        return (self.y0 + self.y1) / 2

    def area(self):
        return max(0.0, self.x1 - self.x0) * max(0.0, self.y1 - self.y0)


class Log:
    """A harness log: its [Why] lines, the dump lines of WHY_DUMP_LAND, the lives' report, exceptions."""

    def __init__(self, path):
        self.path = path
        with open(path, errors='replace') as f:
            self.raw = [r.rstrip('\n') for r in f]
        self.lines = []
        for i, r in enumerate(self.raw):
            k = r.find('[Why] ')
            if k >= 0:
                self.lines.append(Line(r[k + 6:], i))
        self.frames, self.stand, self.crowns, self.children = {}, {}, {}, {}
        for ln in self.lines:
            t = ln.text
            if t.startswith('Frame '):
                m = FRAME.match(t)
                if m:
                    g = m.groups()
                    fr = Frame(g[1], g[2] == 'in', *[float(x) for x in g[3:7]],
                               wy0=float(g[7]) if g[7] else None, wy1=float(g[8]) if g[8] else None)
                    self.frames.setdefault(g[0], {})[g[1]] = fr
            elif t.startswith('Stand screen '):
                m = re.match(r"^Stand screen (\S+?)(?: \(demo\))?: (.*)$", t)
                if m:
                    lst = self.stand.setdefault(m.group(1), [])
                    for e in m.group(2).split(';'):
                        p = e.split()
                        if len(p) >= 5:
                            try:
                                lst.append((' '.join(p[:-4]), p[-4], float(p[-3]), float(p[-2]), float(p[-1])))
                            except ValueError:
                                pass
            elif t.startswith('Stand crowns '):
                m = re.match(r"^Stand crowns (\S+?)(?: \(demo\))?: (.*)$", t)
                if m:
                    lst = self.crowns.setdefault(m.group(1), [])
                    for e in m.group(2).split(';'):
                        p = e.split()
                        if len(p) >= 5:
                            try:
                                lst.append((' '.join(p[:-4]), *[float(x) for x in p[-4:]]))
                            except ValueError:
                                pass
            elif t.startswith('Stand children '):
                # proposed format (open issue): "<child> <x> <y> <parent> <px> <py>; …" in image pixels
                m = re.match(r"^Stand children (\S+?)(?: \(demo\))?: (.*)$", t)
                if m:
                    lst = self.children.setdefault(m.group(1), [])
                    for e in m.group(2).split(';'):
                        p = e.split()
                        if len(p) == 6:
                            try:
                                lst.append((p[0], float(p[1]), float(p[2]), p[3], float(p[4]), float(p[5])))
                            except ValueError:
                                pass

    def find(self, regex):
        """The first [Why] line whose text matches regex (re.search), with the match: (Line, match) or (None, None)."""
        r = re.compile(regex)
        for ln in self.lines:
            m = r.search(ln.text)
            if m:
                return ln, m
        return None, None

    def find_all(self, regex):
        r = re.compile(regex)
        return [(ln, r.search(ln.text)) for ln in self.lines if r.search(ln.text)]

    def harness(self, regex):
        r = re.compile(regex)
        for raw in self.raw:
            if raw.startswith('[harness]'):
                m = r.search(raw)
                if m:
                    return m
        return None

    def repo(self):
        m = self.harness(r"^\[harness\] repo (.+?), scene ")
        return m.group(1) if m else None

    def exceptions(self):
        return [r for r in self.raw if re.search(r"Exception\b", r) and not r.startswith('   at ')]

    def lives_report(self, n=27):
        """The lives' report: the 'economic lives:' line and the n - 1 lines after it (raw lines, the '[Log] ' prefix
        stripped from the first). None when absent."""
        for i, r in enumerate(self.raw):
            k = r.find('[Why] economic lives:')
            if k >= 0:
                return [r[k:]] + self.raw[i + 1:i + n]
        return None

    def prepare_vertices(self, layer):
        """Vertices printed after '<layer>.Prepare' (the first '<n> vertices' after it on its line), or None."""
        r = re.compile(r"(?<![A-Za-z])" + re.escape(layer) + r"\.Prepare")
        for ln in self.lines:
            m = r.search(ln.text)
            if m:
                v = re.search(r"≈?([\d.,]+)(K?) vertices", ln.text[m.end():])
                if v:
                    return float(v.group(1).replace(',', '')) * (1000 if v.group(2) == 'K' else 1), ln
                return None, ln
        return None, None


# ------------------------------------------------------------------------------------------------------------ images

def load_rgb(path):
    from PIL import Image
    import numpy as np
    return np.asarray(Image.open(path).convert('RGB')).astype('float32') / 255.0


def hsv(rgb):
    import numpy as np
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx, mn = rgb.max(-1), rgb.min(-1)
    d = mx - mn
    h = np.zeros_like(mx)
    m = d > 1e-6
    rr = m & (mx == r)
    gg = m & (mx == g) & ~rr
    bb = m & ~rr & ~gg
    h[rr] = (60 * ((g - b)[rr] / d[rr]) + 360) % 360
    h[gg] = 60 * ((b - r)[gg] / d[gg]) + 120
    h[bb] = 60 * ((r - g)[bb] / d[bb]) + 240
    s = np.where(mx > 1e-6, d / np.maximum(mx, 1e-6), 0)
    return h, s, mx


def hue_mask(hsv_t, h0, h1, smin=0.0, smax=1.0, vmin=0.0, vmax=1.0):
    h, s, v = hsv_t
    hm = (h >= h0) & (h <= h1) if h0 <= h1 else (h >= h0) | (h <= h1)
    return hm & (s >= smin) & (s <= smax) & (v >= vmin) & (v <= vmax)


def box_slice(shape, x0, y0, x1, y1, pad=0):
    H, W = shape[0], shape[1]
    xa, xb = max(0, int(round(x0 - pad))), min(W, int(round(x1 + pad)) + 1)
    ya, yb = max(0, int(round(y0 - pad))), min(H, int(round(y1 + pad)) + 1)
    return slice(ya, max(ya, yb)), slice(xa, max(xa, xb))


# ------------------------------------------------------------------------------------------------------------ results

class Res:
    """The outcome of one check: problems tagged with the package their expected value comes from (None: the check's
    owner), notes for the details."""

    def __init__(self, owner):
        self.owner = owner
        self.items = []   # (text, from)
        self.notes = []

    def bad(self, text, frm=None):
        self.items.append((text, frm))

    def note(self, text):
        self.notes.append(text)

    def ok(self, what, cond, detail='', frm=None):
        if not cond:
            self.bad(what + (f" ({detail})" if detail else ''), frm)
        return cond

    def near(self, what, got, want, tol=0.0, rel=None, frm=None):
        if got is None:
            self.bad(f"{what} missing", frm)
            return False
        t = max(tol or 0.0, abs(want) * rel if rel else 0.0)
        if abs(got - want) > t + 1e-9:
            self.bad(f"{what} {fmt(got)} (want {fmt(want)} ± {fmt(round(t, 6))})", frm)
            return False
        return True

    def within(self, what, got, lo=None, hi=None, frm=None):
        if got is None:
            self.bad(f"{what} missing", frm)
            return False
        if (lo is not None and got < lo - 1e-9) or (hi is not None and got > hi + 1e-9):
            rng = f"{fmt(lo) if lo is not None else ''}..{fmt(hi) if hi is not None else ''}"
            self.bad(f"{what} {fmt(got)} (want {rng})", frm)
            return False
        return True

    def eq(self, what, got, want, frm=None):
        if got != want:
            self.bad(f"{what} {got!r} (want {want!r})", frm)
            return False
        return True


# ------------------------------------------------------------------------------------------------------------ fields

def parse_group(text, typ):
    if text is None:
        return None
    if typ == 'money':
        return money_b(text)
    if typ == 'str':
        return text
    if typ == 'int':
        v = to_num(text)
        return None if v is None else int(round(v))
    return to_num(text)


def field_values(f, text):
    """The values a field spec reads from a line's text: a list (one per group; a 'range' field gives (a, b or a))."""
    m = re.search(f['re'], text)
    if not m:
        return None
    typ = f.get('type', 'num')
    groups = list(m.groups()) or [m.group(0)]
    if typ == 'range':
        a = to_num(groups[0])
        b = to_num(groups[1]) if len(groups) > 1 and groups[1] is not None else a
        return [a, b]
    return [parse_group(g, typ) for g in groups]


def check_fields(res, fields, text, vals=None, line_from=None):
    """Applies the JSON field specs to a line's text. Each spec: name, re (groups), type (num|int|money|str|range),
    and any of want (+ tol | rel), eq, min, max, startswith, contains, endswith; 'from' = the package the expected value
    comes from. Values read are stored in vals[name] (a scalar for one group, else the list)."""
    for f in fields:
        frm = f.get('from', line_from)
        name = f['name']
        if 'startswith' in f or 'contains' in f or 'endswith' in f or 'absent' in f:
            if 'startswith' in f:
                res.ok(f"{name}: starts with {f['startswith']!r}", text.startswith(f['startswith']), text[:60], frm)
            if 'contains' in f:
                res.ok(f"{name}: contains {f['contains']!r}", f['contains'] in text, '', frm)
            if 'endswith' in f:
                res.ok(f"{name}: ends with {f['endswith']!r}", text.rstrip().endswith(f['endswith']), text[-60:], frm)
            if 'absent' in f:
                res.ok(f"{name}: no {f['absent']!r}", f['absent'] not in text, '', frm)
            continue
        got = field_values(f, text)
        if got is None:
            res.bad(f"{name} not printed (/{f['re']}/)", frm)
            continue
        if vals is not None:
            vals[name] = got[0] if len(got) == 1 else got
        if 'eq' in f:
            want = f['eq'] if isinstance(f['eq'], list) else [f['eq']]
            res.eq(name, got if len(want) > 1 else got[0], want if len(want) > 1 else want[0], frm)
        if 'want' in f:
            want = f['want'] if isinstance(f['want'], list) else [f['want']]
            for k, (g, w) in enumerate(zip(got, want)):
                if w is None:
                    continue
                label = name if len(want) == 1 else f"{name}[{k}]"
                res.near(label, g, w, f.get('tol', 0.0), f.get('rel'), frm)
        if 'min' in f or 'max' in f:
            for k, g in enumerate(got):
                res.within(name if len(got) == 1 else f"{name}[{k}]", g, f.get('min'), f.get('max'), frm)
    return vals


def check_line(ctx, res, spec, name, vals=None):
    """Looks up one line spec of a module's JSON ({file, re, owner, fields, checks, checksum}) and checks it.
    Returns (Line, vals) or (None, vals). A missing log raises Missing; a missing or '(demo)' line is a problem tagged
    with the line's owner; a demo line's values are not checked."""
    vals = {} if vals is None else vals
    log = ctx.log(spec.get('file', 'land'), done=spec.get('done', True))
    ln, m = log.find(spec['re'])
    owner = spec.get('owner')
    if ln is None:
        res.bad(f"{name}: line missing", owner)
        return None, vals
    if ln.demo:
        res.bad(f"{name}: still (demo)", owner)
        res.note(f"{name} demo")
        return ln, vals
    text = ln.text
    sub = Res(res.owner)
    check_fields(sub, spec.get('fields', []), text, vals, owner)
    if 'checks' in spec:
        c = re.search(r"checks (\d+)/(\d+) (PASS|FAIL)", text)
        if c is None:
            sub.bad("checks n/m PASS not printed", owner)
        else:
            sub.ok(f"checks {c.group(1)}/{c.group(2)} {c.group(3)} (want {spec['checks']} PASS)",
                   f"{c.group(1)}/{c.group(2)}" == spec['checks'] and c.group(3) == 'PASS', '', owner)
    if spec.get('checksum'):
        sub.ok("checksum printed", re.search(r"checksum -?[\d.]+", text) is not None, '', owner)
    for t, frm in sub.items:
        res.bad(f"{name}: {t}", frm)
    return ln, vals


# ------------------------------------------------------------------------------------------------------------ context

class Ctx:
    """What every check reads: the command line, the expected packages, the output folder's logs and renders."""

    def __init__(self, log, outdir, expect, repo=None, log2=None, verbose=False):
        self.main_log, self.outdir, self.expect = log, outdir, set(expect)
        self.log2, self.verbose = log2, verbose
        self._logs, self._json = {}, {}
        self._repo = repo

    @property
    def repo(self):
        if self._repo:
            return self._repo
        try:
            r = self.log('land').repo()
        except Missing:
            r = None
        return r or '/home/user/why-2'

    def real(self, wp):
        return wp in self.expect or wp == 'any'

    def log_path(self, name):
        if name == 'land':
            return self.main_log
        return os.path.join(self.outdir, name + '.log')

    def log(self, name='land', done=True):
        p = self.log_path(name)
        if name not in self._logs:
            if not os.path.exists(p):
                raise Missing(os.path.relpath(p, self.outdir) if p.startswith(self.outdir) else p, done)
            self._logs[name] = Log(p)
        return self._logs[name]

    def has(self, rel):
        return os.path.exists(os.path.join(self.outdir, rel))

    def need(self, rel, done=True):
        p = rel if os.path.isabs(rel) else os.path.join(self.outdir, rel)
        if not os.path.exists(p):
            raise Missing(rel, done)
        return p

    def rgb(self, folder, preset, done=True, base=True):
        return load_rgb(self.need(os.path.join(folder, preset + ('.base.png' if base else '.png')), done))

    def labels(self, folder, preset, done=True):
        with open(self.need(os.path.join(folder, preset + '.labels.json'), done)) as f:
            return json.load(f)

    def frames(self, run, preset, done=True):
        """The Frame boxes of a preset in a run's log ({key: Frame}); {} when the log has none for it."""
        return self.log(run, done).frames.get(preset, {})

    def exp(self, module):
        if module not in self._json:
            with open(os.path.join(HERE, module + '.json')) as f:
                self._json[module] = json.load(f)
        return self._json[module]


def label_by_anchor(labels, prefix):
    return [lab for lab in (labels or {}).get('labels', []) if str(lab.get('anchor') or '').startswith(prefix)]


def glob_frames(frames, pattern):
    """Frames whose key matches a glob-ish pattern ('land:hill:*' = prefix; else exact)."""
    if pattern.endswith('*'):
        p = pattern[:-1]
        return [fr for k, fr in frames.items() if k.startswith(p)]
    return [frames[pattern]] if pattern in frames else []


def players_count(ctx):
    """The Players line's player count (WP2's census), or None."""
    try:
        ln, m = ctx.log('land').find(r"^Players 2025(?: \(demo\))?: (\d+) players")
    except Missing:
        return None
    return int(m.group(1)) if ln and not ln.demo else None

#!/usr/bin/env python3
"""Lists the economy layers' local colours, and computes the palette's pair separations (spec 3.2, S02).

EconomyStyle.Roles (Assets/Scripts/Economy/EconomyStyle.cs) is the only source of economy colour. Every `new Color(` or
`new Color32(` literal under Assets/Scripts/Economy/Layers is a colour a layer still keeps for itself; the four migration
packages (WP15-WP18) bring the count to zero, each in the files it owns. The listing counts them per file with the
package that owns the file. Layers of the retired bowl ([GraphScenes(EconomyLayouts.BowlScene)]: never created, never
drawn) are listed apart and not counted. A file with local colours that no migration package owns (a new layer, say) is
an error: new layers take their colours from EconomyStyle.Roles.

--pairs computes, from the role targets read back from EconomyStyle.cs, the separation of every pair that can touch, as
spec 3.2 tabulates them: OKLab distance x 100 under normal vision, deuteranopia and protanopia (Machado, Oliveira and
Fernandes 2009, severity 1, on linear RGB, clipped to the display's range). The player's palette check
(Assets/Scripts/Economy/Draw/PaletteCheck.cs, -smoketest -smokeextra palette) measures the same pairs on rendered
pixels; --tonemap neutral predicts what it reads when the frame goes through URP's Neutral tonemapper (GraphRoot's
post-processing) instead of leaving LDR colour as it is. --vignette also predicts what it reads at its spots toward the
frame's edges, through URP's vignette at the Vignette volume's intensity and smoothness, as the palette check logs them
(GraphRoot.SetupPostProcessing sets them; 0.32,0.45 when this was written).

Usage (from the repository root):
    python3 Tools/check-palette.py                      # the layers' local colours, by file and owner
    python3 Tools/check-palette.py --strict             # ... and exit 1 while any remain (after WP15-WP18)
    python3 Tools/check-palette.py --pairs [--tonemap none|neutral [--vignette INTENSITY,SMOOTHNESS]]
Exits 1 when a file with local colours has no migration package (or, with --strict, when any remain), or when a pair
is under its floor (8; 15 for desire and fear) with --pairs.
"""
import argparse
import math
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LAYERS = os.path.join(ROOT, "Assets", "Scripts", "Economy", "Layers")
STYLE = os.path.join(ROOT, "Assets", "Scripts", "Economy", "EconomyStyle.cs")

# the migration packages (MODALITIES-PLAN.md) and the layer files each owns
OWNERS = {
    "WP15": ["HillWaveLayer.cs", "HillLandscapeLayer.cs", "HillReplayLayer.cs"],
    "WP16": ["HillFoundationLayer.cs", "HillRegimeLayer.cs", "HillMoneyLayer.cs", "HillMobilityLayer.cs"],
    "WP17": ["HillActivityLayer.cs", "HillCaptureLayer.cs", "HillStrategyLayer.cs", "HillSocialLayer.cs"],
    "WP18": ["HillPersonLayer.cs", "HillDilemmaLayer.cs", "HillYouLayer.cs", "MindDiagram.cs"],
}
OWNER = {f: wp for wp, files in OWNERS.items() for f in files}

LITERAL = re.compile(r"\bnew\s+Color(?:32)?\s*\(")
RETIRED = re.compile(r"\[GraphScenes\(\s*EconomyLayouts\.BowlScene\s*\)\]")

# the roles PaletteCheck measures, as EconomyStyle.Roles names them, and the pairs of spec 3.2 (people-commerce: spec 0, rule 5)
ROLES = ["Owners", "People", "Desire", "Fear", "State", "Attention", "Commerce", "Conflict", "Matter", "Life"]
TOUCHING = [("Desire", "Fear"), ("People", "Fear"), ("Desire", "People"), ("Owners", "Desire"), ("Owners", "People"),
            ("State", "People"), ("Desire", "State"), ("Owners", "Commerce"), ("People", "Conflict"),
            ("Desire", "Conflict"), ("State", "Conflict"), ("Commerce", "Fear"), ("People", "Commerce")]
WEAK = [("Attention", "Fear", "the cloud's points and the spending lanes never touch"),
        ("Matter", "Life", "the two never appear together in the new views")]
PAIR_FLOOR, DESIRE_FEAR_FLOOR = 8.0, 15.0

# PaletteCheck.Spots: where it reads each role, as shares of the frame's width and height from its centre
SPOTS = [(0.0, 0.0), (0.42, 0.0), (0.3, -0.3)]

DEUTAN = [[0.367322, 0.860646, -0.227968], [0.280085, 0.672501, 0.047413], [-0.011820, 0.042940, 0.968881]]
PROTAN = [[0.152286, 1.052583, -0.204868], [0.114503, 0.786281, 0.099216], [-0.003882, -0.048116, 1.051998]]


def code(line):
    """A line without its // comment (a // inside a string, after an odd number of quotes, is kept)."""
    i = line.find("//")
    while i >= 0:
        if line[:i].count('"') % 2 == 0:
            return line[:i]
        i = line.find("//", i + 2)
    return line


def local_colours():
    """(file, owner or None, [line numbers]) for each layer file with literals; and the retired files with theirs."""
    found, retired = [], []
    for name in sorted(os.listdir(LAYERS)):
        if not name.endswith(".cs"):
            continue
        with open(os.path.join(LAYERS, name), encoding="utf-8") as f:
            text = f.read()
        lines = [n for n, line in enumerate(text.splitlines(), 1) for _ in LITERAL.finditer(code(line))]
        if not lines:
            continue
        (retired if RETIRED.search(text) else found).append((name, OWNER.get(name), lines))
    return found, retired


def listing(strict):
    found, retired = local_colours()
    total = sum(len(lines) for _, _, lines in found)
    print("Local colours under Assets/Scripts/Economy/Layers (new Color( and new Color32( literals):")
    for name, owner, lines in sorted(found, key=lambda x: (x[1] or "~", x[0])):
        shown = ", ".join(str(n) for n in lines[:12]) + (", ..." if len(lines) > 12 else "")
        print(f"  {owner or 'NONE':5} {name:26} {len(lines):3}   lines {shown}")
    print(f"  total: {total} in {len(found)} files")
    if retired:
        print("Retired bowl layers (never drawn), not counted: " +
              ", ".join(f"{name} {len(lines)}" for name, _, lines in retired))
    unowned = [name for name, owner, _ in found if owner is None]
    if unowned:
        print("ERROR: local colours in files no migration package owns (take them from EconomyStyle.Roles): " + ", ".join(unowned))
        return 1
    if found:
        print("Every file with local colours is owned by a migration package (WP15-WP18).")
    if strict and total:
        print(f"ERROR: {total} local colours remain (--strict)")
        return 1
    return 0


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def linear_to_srgb(c):
    c = min(1.0, max(0.0, c))
    return c * 12.92 if c <= 0.0031308 else 1.055 * c ** (1 / 2.4) - 0.055


def oklab(rgb):
    r, g, b = rgb
    cbrt = lambda x: math.copysign(abs(x) ** (1 / 3), x)
    l = cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b)
    m = cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b)
    s = cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b)
    return (0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s)


def simulate(matrix, rgb):
    return [min(1.0, max(0.0, sum(matrix[i][j] * rgb[j] for j in range(3)))) for i in range(3)]


def separations(a, b):
    """OKLab distance x 100 under normal vision, deuteranopia and protanopia."""
    d = lambda x, y: 100 * math.dist(oklab(x), oklab(y))
    return d(a, b), d(simulate(DEUTAN, a), simulate(DEUTAN, b)), d(simulate(PROTAN, a), simulate(PROTAN, b))


def neutral(x):
    """URP's Neutral tonemapper (com.unity.render-pipelines.core Color.hlsl, NeutralTonemap) on a linear value."""
    a, b, c, d, e, f = 0.2, 0.29, 0.24, 0.272, 0.02, 0.3
    curve = lambda v: ((v * (a * v + c * b) + d * e) / (v * (a * v + b) + d * f)) - e / f
    white = 1 / curve(5.3)
    return curve(max(0.0, x) * white) * white


def vignette_factor(spot, intensity, smoothness):
    """What URP's vignette (UberPost ApplyVignette: black, not rounded, centred) leaves of linear light at a spot of the
    frame; URP passes the volume's intensity times 3 and its smoothness times 5 to the shader."""
    i, s = 3 * intensity, 5 * smoothness
    dx, dy = spot
    return max(0.0, 1 - i * i * (dx * dx + dy * dy)) ** s


def rendered(rgb, tonemap, light=1.0):
    """The colour as the frame holds it: an 8-bit vertex colour, the vignette's share of light (applied before colour
    grading), the tonemapper (or the clamp of LDR grading), an 8-bit sRGB back buffer."""
    vertex = [round(min(1.0, max(0.0, c)) * 255) / 255 * light for c in rgb]
    hdr = [neutral(c) for c in vertex] if tonemap == "neutral" else vertex
    return [srgb_to_linear(round(linear_to_srgb(c) * 255) / 255) for c in hdr]


def roles():
    """The role targets in EconomyStyle.cs (Roles' fields written as Srgb(0xRRGGBB)), as linear colours."""
    with open(STYLE, encoding="utf-8") as f:
        text = f.read()
    found = {}
    for name, hexa in re.findall(r"public static readonly Color (\w+) = Srgb\(0x([0-9A-Fa-f]{6})", text):
        found[name] = (hexa.upper(), [srgb_to_linear(int(hexa[i:i + 2], 16) / 255) for i in (0, 2, 4)])
    missing = [r for r in ROLES if r not in found]
    if missing:
        sys.exit("EconomyStyle.cs has no Srgb(0x...) target for " + ", ".join(missing))
    return found


def pairs(tonemap, vignette):
    found = roles()
    what = {None: "on the sRGB targets", "none": "rendered with no tonemapper (8-bit vertex colours and back buffer)",
            "neutral": "rendered through URP's Neutral tonemapper"}[tonemap]
    # with a vignette, each of PaletteCheck's spots in turn; without one, the frame is the same everywhere
    spots = SPOTS if vignette else SPOTS[:1]
    below = set()
    for spot in spots:
        light = vignette_factor(spot, *vignette) if vignette else 1.0
        colour = {name: rendered(rgb, tonemap, light) if tonemap else rgb for name, (_, rgb) in found.items()}
        place = "at the frame's centre" if spot == SPOTS[0] else f"at ({spot[0]:g}, {spot[1]:g}) of the frame from its centre"
        where = (f", {place}, through the vignette (intensity {vignette[0]:g}, smoothness {vignette[1]:g}: "
                 f"{light:.3f} of the light)") if vignette else ""
        print(f"Pair separations, OKLab x100, normal / deutan / protan, {what}{where}:")
        if spot == spots[0]:
            print("  " + ", ".join(f"{r.lower()} #{found[r][0]}" for r in ROLES))
        for a, b in TOUCHING:
            n, d, p = separations(colour[a], colour[b])
            floor = DESIRE_FEAR_FLOOR if (a, b) == ("Desire", "Fear") else PAIR_FLOOR
            ok = min(n, d, p) >= floor
            if not ok:
                below.add((a, b))
            print(f"  {a.lower() + '-' + b.lower():18} {n:5.1f} {d:5.1f} {p:5.1f}   floor {floor:.0f}: {'ok' if ok else 'BELOW'}")
        for a, b, rule in WEAK:
            n, d, p = separations(colour[a], colour[b])
            print(f"  {a.lower() + '-' + b.lower():18} {n:5.1f} {d:5.1f} {p:5.1f}   known weak: {rule}")
    print(f"  {len(TOUCHING)} touching pairs, {len(below)} below their floor" + (f" at {len(spots)} spots" if vignette else ""))
    return 1 if below else 0


def vignette_arg(text):
    """INTENSITY,SMOOTHNESS as the Vignette volume takes them (0..1 each)."""
    try:
        values = [float(v) for v in text.split(",")]
    except ValueError:
        values = []
    if len(values) != 2 or not all(0 <= v <= 1 for v in values):
        raise argparse.ArgumentTypeError("give the Vignette volume's intensity and smoothness, e.g. 0.32,0.45")
    return tuple(values)


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--strict", action="store_true", help="exit 1 while any local colour remains")
    ap.add_argument("--pairs", action="store_true", help="the pair separations of the role targets")
    ap.add_argument("--tonemap", choices=["none", "neutral"], help="with --pairs: model the rendered frame")
    ap.add_argument("--vignette", type=vignette_arg, metavar="INTENSITY,SMOOTHNESS",
                    help="with --tonemap: also at PaletteCheck's spots toward the frame's edges, through URP's vignette")
    args = ap.parse_args()
    if args.tonemap and not args.pairs:
        ap.error("--tonemap goes with --pairs")
    if args.vignette and not args.tonemap:
        ap.error("--vignette goes with --pairs and --tonemap (it models the rendered frame)")
    sys.exit(pairs(args.tonemap, args.vignette) if args.pairs else listing(args.strict))


if __name__ == "__main__":
    main()

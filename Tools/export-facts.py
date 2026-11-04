#!/usr/bin/env python3
"""Turns the model facts in a player log into Tools/model-facts.json.

A player started with -smoketest -smokefacts logs every fact the models published (Assets/Scripts/Core/ModelFacts.cs)
at the end of the run, one line each:

    [Why] fact <key> = <value> <unit> | <source> | <basis>

The key is letters, digits, '.', '_' and '-'; the value is a number, or a text in double quotes with JSON escapes; the
unit may be empty. Only lines of this form count. Other lines are ignored, even when they mention facts
("[Why] Facts 2025: ...", "[Why] Replay facts ...", "[Why] facts: 12 published"); a line that starts like a fact
("[Why] fact <key> = ") but does not parse is reported and skipped. When a key appears twice with different contents
(two runs in one log), the later line wins and the change is reported.

The JSON lists the facts sorted by key, each as {"value", "unit", "source", "basis"}, written the same way every time:
the same log gives the same file, byte for byte.

Usage (from the repository root):
    python3 Tools/export-facts.py PLAYER_LOG              # writes Tools/model-facts.json
    python3 Tools/export-facts.py PLAYER_LOG --out -      # prints the JSON instead
Exits 1, writing nothing, when the log has no fact lines (was the player started with -smokefacts?).
"""
import argparse
import json
import math
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Tools", "model-facts.json")

# "[Why] fact <key> = <rest>"; the rest is "<value> <unit> | <source> | <basis>"
FACT = re.compile(r"\[Why\] fact ([A-Za-z0-9._-]+) = (.*)")
# after the value: one space, the unit (maybe empty), " | ", the source, " | ", the basis
FIELDS = re.compile(r" ?(.*?) ?\| (.*?) \| (.*)")
DECODER = json.JSONDecoder()


def parse(rest):
    """The fields after "<key> = " on a fact line, or a ValueError saying what is wrong with them."""
    try:
        value, end = DECODER.raw_decode(rest)
    except json.JSONDecodeError as e:
        raise ValueError(f"the value is neither a number nor a quoted text ({e.msg})") from None
    if isinstance(value, bool) or not isinstance(value, (int, float, str)):
        raise ValueError("the value is neither a number nor a quoted text")
    if isinstance(value, float) and not math.isfinite(value):
        raise ValueError(f"the value {rest[:end]} is not a finite number")
    m = FIELDS.fullmatch(rest[end:])
    if not m:
        raise ValueError("expected '<unit> | <source> | <basis>' after the value")
    unit, source, basis = (s.strip() for s in m.groups())
    return {"value": value, "unit": unit, "source": source, "basis": basis}


def read(path):
    """The facts in a player log, by key, and the number of lines that started like a fact but were skipped."""
    facts, skipped = {}, 0
    with open(path, encoding="utf-8", errors="replace") as f:
        for n, line in enumerate(f, 1):
            m = FACT.fullmatch(line.strip())
            if not m:
                continue
            key, rest = m.groups()
            try:
                fact = parse(rest)
            except ValueError as e:
                skipped += 1
                print(f"{path}:{n}: skipped a fact line: {e}: {line.strip()[:200]}", file=sys.stderr)
                continue
            if key in facts and facts[key] != fact:
                print(f"{path}:{n}: '{key}' again with other contents; this later line wins", file=sys.stderr)
            facts[key] = fact
    return facts, skipped


def main():
    ap = argparse.ArgumentParser(description="Turns the model facts in a player log into Tools/model-facts.json.")
    ap.add_argument("log", help="the player's log (Player.log, or the -logFile of a -smoketest -smokefacts run)")
    ap.add_argument("--out", default=OUT, help="where to write the JSON ('-': print it); default Tools/model-facts.json")
    args = ap.parse_args()

    try:
        facts, skipped = read(args.log)
    except OSError as e:
        sys.exit(f"cannot read {args.log}: {e.strerror}")
    if not facts:
        sys.exit(f"no '[Why] fact' lines in {args.log}: was the player started with -smoketest -smokefacts? "
                 f"Nothing written.")

    text = json.dumps({key: facts[key] for key in sorted(facts)}, indent=2, ensure_ascii=False) + "\n"
    if args.out == "-":
        sys.stdout.write(text)
        return
    old = None
    if os.path.exists(args.out):
        with open(args.out, encoding="utf-8", newline="") as f:
            old = f.read()
    if old != text:
        with open(args.out, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
    shown = os.path.relpath(args.out, ROOT) if os.path.abspath(args.out).startswith(ROOT + os.sep) else args.out
    print(f"{shown}: {len(facts)} facts from {args.log}{' (unchanged)' if old == text else ''}"
          f"{f'; {skipped} fact lines skipped' if skipped else ''}")


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Compiles the C# scripts under Assets/ against Unity's assemblies, outside the Editor (twin of compile-check.ps1).

Usage (from anywhere):
    python3 Tools/compile-check.py [--editor] [--project PATH]

Runtime check (default): every script under Assets except Editor folders and TextMesh Pro.
--editor: Editor scripts together with the runtime scripts, against the editor references.
--project: check another checkout (e.g. a worktree) while borrowing references from this one when it has no
           Library of its own.

References come from the Unity-generated Assembly-CSharp(-Editor).csproj (HintPaths) plus every package assembly
in Library/ScriptAssemblies, so packages added since the project files were generated still resolve. Uses the
Roslyn compiler bundled with the Editor; nothing is written into the project.
"""
import argparse
import glob
import os
import re
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def find_editor():
    for base in [os.path.expanduser("~/Unity/Hub/Editor"), "/opt/unity/Editor"]:
        for d in sorted(glob.glob(os.path.join(base, "*")), reverse=True):
            data = os.path.join(d, "Editor", "Data")
            if os.path.isdir(data):
                return data
    sys.exit("Unity Editor not found under ~/Unity/Hub/Editor")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--editor", action="store_true")
    ap.add_argument("--project", default=HERE)
    args = ap.parse_args()
    project = os.path.abspath(args.project)

    ref_root = project if os.path.isdir(os.path.join(project, "Library", "ScriptAssemblies")) else HERE
    csproj = os.path.join(ref_root, "Assembly-CSharp-Editor.csproj" if args.editor else "Assembly-CSharp.csproj")
    if not os.path.exists(csproj):
        sys.exit(f"Missing {csproj}: open the project in Unity once so it generates project files.")
    xml = open(csproj, encoding="utf-8").read()
    refs = set(re.findall(r"<HintPath>([^<]+)</HintPath>", xml))
    defines = re.search(r"<DefineConstants>([^<]*)</DefineConstants>", xml).group(1)
    lang = (re.search(r"<LangVersion>([^<]*)</LangVersion>", xml) or [None, "9.0"])[1]
    if not args.editor:
        defines = ";".join(d for d in defines.split(";") if d and d != "UNITY_EDITOR" and not d.startswith("UNITY_EDITOR_"))

    lib = os.path.join(ref_root, "Library", "ScriptAssemblies")
    for dll in glob.glob(os.path.join(lib, "*.dll")):
        name = os.path.basename(dll)
        if name.startswith("Assembly-CSharp") or ".Tests" in name or "TestFramework" in name:
            continue
        if not args.editor and (".Editor" in name or "Editor." in name or name.endswith("Editor.dll")):
            continue
        refs.add(dll)
    # drop references that are no longer there (Library is rebuilt by the Editor)
    refs = sorted(r for r in refs if os.path.exists(r) and not os.path.basename(r).startswith("Assembly-CSharp"))
    # de-duplicate by assembly name: prefer the ScriptAssemblies copy (newest package)
    by_name = {}
    for r in refs:
        n = os.path.basename(r).lower()
        if n not in by_name or "ScriptAssemblies" in r:
            by_name[n] = r
    refs = sorted(by_name.values())

    sources = []
    assets = os.path.join(project, "Assets")
    for dirpath, dirnames, filenames in os.walk(assets):
        rel = os.path.relpath(dirpath, assets)
        if rel.startswith("TextMesh Pro"):
            continue
        in_editor = "Editor" in rel.split(os.sep)
        if in_editor and not args.editor:
            continue
        sources += [os.path.join(dirpath, f) for f in filenames if f.endswith(".cs")]
    if not sources:
        sys.exit("no sources")

    data = find_editor()
    dotnet = os.path.join(data, "DotNetSdk", "dotnet")
    csc = sorted(glob.glob(os.path.join(data, "DotNetSdk", "sdk", "*", "Roslyn", "bincore", "csc.dll")))[-1]
    with tempfile.TemporaryDirectory() as tmp:
        rsp = os.path.join(tmp, "args.rsp")
        with open(rsp, "w", encoding="utf-8") as f:
            f.write("-nologo -noconfig -nostdlib+ -target:library -unsafe+ -deterministic -nowarn:0169,0414,0649,8618,1701,1702\n")
            f.write(f"-langversion:{lang}\n-define:{defines}\n-out:{os.path.join(tmp, 'check.dll')}\n")
            for r in refs:
                f.write(f'-r:"{r}"\n')
            for s in sources:
                f.write(f'"{s}"\n')
        p = subprocess.run([dotnet, csc, "@" + rsp], capture_output=True, text=True)
    out = (p.stdout + p.stderr).splitlines()
    errors = sorted({l.replace(project + os.sep, "") for l in out if ": error " in l})
    warnings = sorted({l.replace(project + os.sep, "") for l in out if ": warning CS" in l})
    for w in warnings[:20]:
        print(w)
    if errors:
        for e in errors:
            print(e)
        print(f"COMPILE FAILED ({len(errors)} errors, {len(sources)} files)")
        sys.exit(1)
    print(f"COMPILE OK ({len(sources)} files{', editor' if args.editor else ''})")


if __name__ == "__main__":
    main()

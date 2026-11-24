# Refactor & Optimization — Work In Progress (Pending Start)

- **Status:** Pending (not yet refactored; ready to start)
- **Date:** 2026-11-24
- **WIP Branch:** `wip/refactor-optimize`
- **Base Branch / Commit:** `main` @ `a21bb4d`
- **Stack:** Unity 6000.6.10f1 (C#) + Python Tools

## Current State Notes
Pulled latest 82 upstream commits from `origin/main` (earlier Oct-13 local WIP preserved on branch `backup/pre-pull-2026-10-13`). Linux `Tools/judge-purchases.py` and `.gitignore` applied on `main` (`a21bb4d`). Not yet re-opened in Unity `6000.6.10f1` since pull or refactored.

## Planned Work
- [ ] Re-open in Unity `6000.6.10f1` to import the 82 pulled upstream commits and verify clean baseline build.
- [ ] Refactor & optimize Unity C# scripts (`Assets/Scripts/`, preserving all MonoBehaviour/ScriptableObject names, namespaces, `[SerializeField]` fields, and `.meta` GUIDs) and `Tools/*.py`.
- [ ] Run Unity EditMode tests (if present), rebuild Linux64 player, GPU smoke-run, and verify `Tools/*.py`.
- [ ] Remove `REFACTOR_WIP.md` and merge `wip/refactor-optimize` into `main`.

## Verification Command
```bash
python3 /home/john/Tools/project-migration/unity_build_and_run.py /home/john/Projects/why-2 why-2
```

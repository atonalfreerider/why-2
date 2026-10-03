@echo off
cd /d "%~dp0"
codex app "%~dp0."
if errorlevel 1 pause

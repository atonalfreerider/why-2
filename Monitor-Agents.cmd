@echo off
cd /d "%~dp0"
codex agents -C "%~dp0." --no-alt-screen
if errorlevel 1 pause

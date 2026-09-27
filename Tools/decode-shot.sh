#!/usr/bin/env bash
# Extracts the first "base64" PNG payload from a saved MCP tool result and writes it as a PNG.
# Usage: Tools/decode-shot.sh <tool-result.txt> <out.png>
set -e
grep -o '"base64": *"[A-Za-z0-9+/=]*"' "$1" | head -1 | sed 's/.*"base64": *"//; s/"$//' | base64 -d > "$2"
echo "$2 $(stat -c %s "$2") bytes"

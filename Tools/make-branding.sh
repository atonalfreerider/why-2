#!/usr/bin/env bash
# Renders the app's icon and splash from the graph itself.
#
# Usage: Tools/make-branding.sh PLAYER [SHOTS_DIR]
#   PLAYER: a desktop player of the project (e.g. ../.why-2-build/Builds/Linux/why.x86_64, built with
#           Why.EditorTools.DesktopBuild.BuildLinuxFromCommandLine)
# Writes Assets/Branding/WhyIcon.png (1024 x 1024: the launch icon) and Assets/Branding/WhySplash.png
# (2048 x 2048: the headset's system splash), both from a 2048 x 2048 render of the overview with no panels or labels
# (the player's -iconshot mode). Needs ffmpeg.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PLAYER="${1:?usage: Tools/make-branding.sh PLAYER [SHOTS_DIR]}"
SHOTS="${2:-$(mktemp -d)}"
OUT="$ROOT/Assets/Branding"
FONT="$(fc-match -f '%{file}' 'Noto Sans Display:style=Regular' 2>/dev/null || true)"
mkdir -p "$OUT" "$SHOTS"

"$PLAYER" -screen-fullscreen 0 -screen-width 1024 -screen-height 1024 -iconshot "$SHOTS" -logFile "$SHOTS/iconshot.log" >/dev/null 2>&1 || true
SRC="$SHOTS/graph-overview.png"
[ -f "$SRC" ] || { echo "no render in $SHOTS (see iconshot.log)" >&2; exit 1; }

# icon: the graph, a little closer, saturated, with a soft vignette that keeps the edges dark on any launcher
ffmpeg -v error -y -i "$SRC" -vf "crop=1760:1760:60:40,scale=1024:1024:flags=lanczos,eq=saturation=1.18:contrast=1.06:brightness=0.015,vignette=PI/5" \
  "$OUT/WhyIcon.png"

# splash: the whole graph and the wordmark in the dark under the human branch
ffmpeg -v error -y -i "$SRC" -vf "eq=saturation=1.12:contrast=1.05,vignette=PI/4.5,\
drawtext=fontfile='$FONT':text='W H Y':fontcolor=white@0.94:fontsize=150:x=(w-text_w)/2:y=h*0.835,\
drawtext=fontfile='$FONT':text='from the Big Bang to this moment':fontcolor=0xC9CED8@0.82:fontsize=56:x=(w-text_w)/2:y=h*0.835+200" \
  "$OUT/WhySplash.png"
echo "wrote $OUT/WhyIcon.png and $OUT/WhySplash.png"

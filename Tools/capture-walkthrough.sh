#!/usr/bin/env bash
# Records the narrated walkthrough's opening with the Unity Recorder, landscape 1920x1080 and portrait 1080x1920,
# and writes H.264 MP4s to Captures/ (the Recorder writes VP8 WebM on Linux; ffmpeg transcodes).
#
# Usage: Tools/capture-walkthrough.sh [SECONDS] [--copy]
#   --copy  record from the mirror (../.why-2-build) so it works while the Editor has this project open
# Opens a Unity Editor window for each orientation (the Recorder needs the Game view; -batchmode cannot record).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SECONDS_TO_RECORD=110
PROJECT="$ROOT"
for a in "$@"; do
  case "$a" in
    --copy) PROJECT="$(dirname "$ROOT")/.why-2-build" ;;
    *) SECONDS_TO_RECORD="$a" ;;
  esac
done
VERSION="$(sed -n 's/^m_EditorVersion: //p' "$ROOT/ProjectSettings/ProjectVersion.txt")"
UNITY="$HOME/Unity/Hub/Editor/$VERSION/Editor/Unity"
if [ "$PROJECT" != "$ROOT" ]; then
  rsync -a --delete --exclude Library --exclude Temp --exclude Logs --exclude UserSettings --exclude Builds \
    --exclude Captures --exclude .git --exclude .claude "$ROOT/" "$PROJECT/"
fi
mkdir -p "$PROJECT/Captures" "$ROOT/Captures"
for o in landscape portrait; do
  rm -f "$PROJECT/Captures/why-$o".*
  "$UNITY" -projectPath "$PROJECT" -buildTarget Linux64 -executeMethod Why.EditorTools.WalkthroughCapture.FromCommandLine \
    -captureOrientation "$o" -captureSeconds "$SECONDS_TO_RECORD" -captureOut "Captures/why-$o" \
    -logFile "$PROJECT/Logs/capture-$o.log" || true
  src="$(ls "$PROJECT/Captures/why-$o".webm "$PROJECT/Captures/why-$o".mp4 2>/dev/null | head -1 || true)"
  [ -n "$src" ] || { echo "no recording for $o (see $PROJECT/Logs/capture-$o.log)" >&2; exit 1; }
  # the narration, laid in from its clips at the times the capture noted (each clip runs until the next stop starts)
  python3 - "$src" "$PROJECT/Captures/why-$o.cues.json" "$ROOT" "$ROOT/Captures/why-$o.mp4" <<'PY'
import json, subprocess, sys
src, cues_path, root, out = sys.argv[1:5]
cues = json.load(open(cues_path))
seconds = float(cues["seconds"])
items = [(c["id"], float(c["t"])) for c in cues["cues"] if float(c["t"]) < seconds]
args = ["ffmpeg", "-v", "error", "-y", "-i", src]
filters, labels = [], []
for k, (cid, t) in enumerate(items):
    clip = f"{root}/Assets/Resources/Audio/Tour/{cid}.ogg"
    args += ["-i", clip]
    end = items[k + 1][1] if k + 1 < len(items) else seconds
    ms = int(t * 1000)
    filters.append(f"[{k + 1}:a]atrim=0:{max(end - t, 0.01):.3f},adelay={ms}|{ms},aformat=channel_layouts=stereo[a{k}]")
    labels.append(f"[a{k}]")
if items:
    filters.append("".join(labels) + f"amix=inputs={len(items)}:normalize=0,atrim=0:{seconds:.3f}[aout]")
    args += ["-filter_complex", ";".join(filters), "-map", "0:v", "-map", "[aout]"]
else:
    args += ["-map", "0:v"]
args += ["-c:v", "libx264", "-preset", "slow", "-crf", "18", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k",
         "-movflags", "+faststart", out]
subprocess.run(args, check=True)
print(f"narration: {len(items)} clips: " + ", ".join(f"{i}@{t:.1f}s" for i, t in items))
PY
  echo "wrote $ROOT/Captures/why-$o.mp4"
done

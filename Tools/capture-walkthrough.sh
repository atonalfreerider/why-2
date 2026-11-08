#!/usr/bin/env bash
# Records the narrated walkthrough of the universe with the Unity Recorder, landscape 1920x1080 and portrait 1080x1920,
# and writes H.264 MP4s with the voice and the tour's chapters to Captures/. By default the whole tour (66 stops, about
# 33 minutes of video with the clips voiced on 2026-10-12), until 10 s into its end card.
#
# Usage: Tools/capture-walkthrough.sh [SECONDS | --opening] [--copy [--as-is]] [--only landscape|portrait] [--mix-only] [--force]
#   SECONDS     a video of that many seconds, why-<o>-<SECONDS>s.mp4 (0, the default: the whole tour, why-<o>.mp4)
#   --opening   the tour's first three stops, ending on the frame before read_clock appears (about 114.5 s):
#               why-<o>-opening.mp4
#   --copy      record in the mirror (../.why-2-build), synced first as work/mirror-build.sh syncs it; the Editor on this
#               project must still be closed (one Unity process at a time)
#   --as-is     with --copy: record the mirror as it stands, without syncing it first (when this working tree holds
#               unfinished changes the recording must not take; the mirror keeps what its last sync or build gave it)
#   --only o    one orientation, landscape or portrait
#   --mix-only  make the MP4s again from the last recording's picture and cue log, without Unity, with this repository's
#               narration clips (it syncs nothing, so a mirror's clips may be older)
#   --force     make the MP4 even when the picture's length and the cue log's differ by more than half a second
# An MP4 already there is kept as <name>-<date it was made>.mp4, with its cue log; the new one replaces it only once it
# is complete. The picture is cut to the frames the cue log gives (the Recorder has also taken the frame the opening
# ends before, read_clock's first); a picture whose length disagrees with its cue log makes no MP4 unless --force.
# Each orientation opens a Unity Editor window (the Recorder needs the Game view; -batchmode cannot record): leave it
# visible. The mouse and keyboard are out of the game while it records. Recording runs slower than real time (October's
# opening ran at about 0.6x), then ffmpeg transcodes; the Editor log (Logs/capture-<o><tag>-<date>.log in the project
# that records, one per recording) shows the progress each minute of video. Needs no other Unity process, 4 GB of
# memory available and 20 GB of disk free.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SECONDS_TO_RECORD=0
PROJECT="$ROOT"
ORIENTATIONS="landscape portrait"
MIX_ONLY=0
FORCE=0
AS_IS=0
while [ $# -gt 0 ]; do
  case "$1" in
    --copy) PROJECT="$(dirname "$ROOT")/.why-2-build" ;;
    --opening) SECONDS_TO_RECORD=opening ;;
    --only)
      case "${2:-}" in
        landscape|portrait) ORIENTATIONS="$2" ;;
        *) echo "--only takes landscape or portrait" >&2; exit 2 ;;
      esac
      shift ;;
    --mix-only) MIX_ONLY=1 ;;
    --force) FORCE=1 ;;
    --as-is) AS_IS=1 ;;
    -h|--help) awk 'NR > 1 && /^#/ { sub(/^# ?/, ""); print; next } NR > 1 { exit }' "$0"; exit 0 ;;
    *)
      [[ "$1" =~ ^[0-9]+([.][0-9]+)?$ ]] || { echo "unknown argument $1 (see --help)" >&2; exit 2; }
      SECONDS_TO_RECORD="$1" ;;
  esac
  shift
done

# outputs are named for what they hold, so a test or the opening never replaces the whole tour
if [ "$SECONDS_TO_RECORD" = opening ]; then
  TAG="-opening"
elif awk -v s="$SECONDS_TO_RECORD" 'BEGIN { exit !(s > 0) }'; then
  TAG="-${SECONDS_TO_RECORD}s"
else
  TAG=""
  SECONDS_TO_RECORD=0
fi

VERSION="$(sed -n 's/^m_EditorVersion: //p' "$ROOT/ProjectSettings/ProjectVersion.txt")"
UNITY="$HOME/Unity/Hub/Editor/$VERSION/Editor/Unity"
# the narration laid in: the clips in the project that records the tour (--copy syncs the mirror's from this repository
# first, so they are the ones the tour is timed with); --mix-only syncs nothing, so it takes this repository's own clips,
# never a mirror's that may be older
if [ "$MIX_ONLY" = 1 ]; then CLIPS="$ROOT/Assets/Resources/Audio/Tour"; else CLIPS="$PROJECT/Assets/Resources/Audio/Tour"; fi

refuse() { echo "REFUSED: $*" >&2; exit 2; }

unity_running() {
  pgrep -x 'Unity Main Thre' >/dev/null || pgrep -f '[E]ditor/Unity -batchmode' >/dev/null || pgrep -f '[.]/why[.]x86_64' >/dev/null
}

# The machine's rules (work/mirror-build.sh): one Unity process at a time (an Editor, a batch build or the player), and
# only with 4 GB of memory available; and room on the disk for the Recorder's picture and the MP4s.
guard() {
  if unity_running; then refuse "another Unity process is running (close the Editor, or wait for the build or the player)"; fi
  local memory disk
  memory=$(free -g | awk '/^Mem:/ { print $7 }')
  [ "$memory" -ge 4 ] || refuse "only $memory GB of memory available (4 needed)"
  disk=$(df -Pk "$PROJECT" | awk 'NR == 2 { print int($4 / 1048576) }')
  [ "$disk" -ge 20 ] || refuse "only $disk GB free on the disk (20 needed for the picture and the MP4s)"
}

# Syncs the source trees into the mirror as work/mirror-build.sh does, without building. Never the repository's root:
# that would delete the mirror's own work/, Shots/ and research/. Each tree by content (-c, no times: copied times make
# Unity reimport untouched assets) with --delete inside it, keeping the three assets the mirror's Unity reserialized and
# leaving out the 152 MB social graph DB (StreamingAssets), which the app does not read at runtime.
sync_mirror() {
  local d
  for d in Assets Packages ProjectSettings Tools Docs; do
    rsync -rlpDc --delete \
      --exclude '/StreamingAssets/' --exclude '/StreamingAssets.meta' \
      --exclude '/DefaultVolumeProfile.asset' --exclude '/UniversalRenderPipelineGlobalSettings.asset' --exclude '/Settings/WhyURP.asset' \
      --exclude '.cache/' --exclude '__pycache__/' \
      "$ROOT/$d/" "$PROJECT/$d/" || { echo "SYNC FAILED: $d" >&2; exit 1; }
  done
  echo "synced the source trees into $PROJECT"
}

# Moves an earlier MP4 and its cue log aside as <name>-<date the MP4 was made>, so a new capture never destroys one.
keep() {
  local out="$1" made stamp dest n=1
  [ -e "$out.mp4" ] || [ -e "$out.cues.json" ] || return 0
  made="$out.mp4"
  [ -e "$made" ] || made="$out.cues.json"
  stamp="$(date -r "$made" +%Y%m%d-%H%M)"
  dest="$out-$stamp"
  while [ -e "$dest.mp4" ] || [ -e "$dest.cues.json" ]; do n=$((n + 1)); dest="$out-$stamp-$n"; done
  # a move that fails stops the script (set -e) before the new MP4 could take the earlier one's name
  if [ -e "$out.mp4" ]; then
    mv "$out.mp4" "$dest.mp4"
    echo "kept the earlier $(basename "$out").mp4 as $(basename "$dest").mp4"
  fi
  if [ -e "$out.cues.json" ]; then mv "$out.cues.json" "$dest.cues.json"; fi
  return 0
}

# The newest Editor log of a capture in the project (each recording writes its own, capture-<o><tag>-<date>.log; before
# November 2026 they were capture-<o><tag>.log), or nothing: latest_log <o><tag>
latest_log() {
  local f newest=""
  for f in "$PROJECT/Logs/capture-$1"-[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9][0-9][0-9].log \
           "$PROJECT/Logs/capture-$1.log"; do
    [ -e "$f" ] || continue
    if [ -z "$newest" ] || [ "$f" -nt "$newest" ]; then newest="$f"; fi
  done
  echo "$newest"
}

# Lays the narration into the picture from its clips at the times the capture noted (each clip runs until the next stop
# starts), marks the tour's chapters (a view's title where the tour first reaches it) and transcodes to H.264 MP4, the
# picture cut to the frames the cue log gives. Fails, writing nothing, when the picture's length and the cue log's
# differ by more than two frames and half a second, unless FORCE is 1:
# mix PICTURE CUE_LOG CLIPS_DIR OUT.mp4 FORCE
mix() {
  python3 -u - "$@" <<'PY'
import json, os, subprocess, sys

src, cues_path, clips, out = sys.argv[1:5]
force = sys.argv[5:6] == ["1"]
log = json.load(open(cues_path, encoding="utf-8"))
if not log.get("complete", True):
    sys.exit(f"{cues_path}: the recording is incomplete: {log.get('problem') or 'see the Editor log'}")
fps = float(log.get("fps", 30))
if "frames" in log:
    frames = int(log["frames"])
    seconds = frames / fps
else:
    seconds = float(log["seconds"])
    frames = round(seconds * fps)
cues = [c for c in log["cues"] if float(c["t"]) < seconds]

# the picture's own length against the frames the capture counted: the picture is cut to those frames (the Recorder
# may hold one more: a capture of the first stops ends on the frame before the next stop's, which it has taken), but a
# picture longer or shorter by more than that is not the one the cue log describes
probe = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", src],
                       capture_output=True, text=True)
try:
    picture = float(probe.stdout.strip())
except ValueError:
    picture = None
    print(f"picture: {src}, length unreadable: {probe.stderr.strip()}", file=sys.stderr)
if picture is not None:
    note = "" if abs(picture - seconds) <= 2 / fps else f" (the picture runs {picture:.3f} s)"
    print(f"picture: {src}, {seconds:.3f} s ({frames} frames) by the cue log{note}")
    if abs(picture - seconds) > max(2 / fps, 0.5):
        problem = (f"the picture runs {picture:.3f} s but its cue log {cues_path} gives {seconds:.3f} s (not the picture "
                   "the cue log was written with? its narration and chapters may land at the wrong times)")
        if not force:
            sys.exit(f"{problem}; no MP4 made (--force makes it anyway, cut to the cue log's frames)")
        print(f"picture: {problem}; --force: made anyway, cut to the cue log's {frames} frames", file=sys.stderr)

args = ["ffmpeg", "-v", "error", "-stats", "-y", "-i", src]
filters, labels, laid, silent = [], [], [], []
for k, c in enumerate(cues):
    t = float(c["t"])
    clip = os.path.join(clips, c["id"] + ".ogg")
    if not os.path.exists(clip):
        silent.append(c["id"])
        continue
    end = float(cues[k + 1]["t"]) if k + 1 < len(cues) else seconds
    n = len(labels) + 1
    args += ["-i", clip]
    ms = int(t * 1000)
    filters.append(f"[{n}:a]atrim=0:{max(end - t, 0.01):.3f},adelay={ms}|{ms},aformat=channel_layouts=stereo[a{n}]")
    labels.append(f"[a{n}]")
    laid.append(f"{c['id']}@{t:.1f}s")
print(f"narration: {len(laid)} clips from {clips}: " + ", ".join(laid))
if silent:
    print("narration: no clip for " + ", ".join(silent) + "; silent there", file=sys.stderr)

# chapters as ffmpeg metadata, and as the timestamps a video description lists
chapters = [(float(c["t"]), c["chapter"]) for c in cues if c.get("chapter")]
meta = out + ".chapters" if chapters else None
if meta:
    with open(meta, "w", encoding="utf-8") as f:
        f.write(";FFMETADATA1\n")
        for i, (t, title) in enumerate(chapters):
            end = chapters[i + 1][0] if i + 1 < len(chapters) else seconds
            title = "".join("\\" + ch if ch in "=;#\\\n" else ch for ch in title)
            f.write(f"[CHAPTER]\nTIMEBASE=1/1000\nSTART={round(t * 1000)}\nEND={round(end * 1000)}\ntitle={title}\n")
    args += ["-f", "ffmetadata", "-i", meta]
    print("chapters:")
    for t, title in chapters:
        s = int(t)
        print(f"  {s // 3600}:{s // 60 % 60:02d}:{s % 60:02d} {title}" if s >= 3600 else f"  {s // 60}:{s % 60:02d} {title}")

if labels:
    # silence after the last clip, so the sound runs as long as the picture (through the end card)
    filters.append("".join(labels) + f"amix=inputs={len(labels)}:normalize=0,apad,atrim=0:{seconds:.3f}[aout]")
    args += ["-filter_complex", ";".join(filters), "-map", "0:v", "-map", "[aout]"]
else:
    args += ["-map", "0:v"]
if meta:
    args += ["-map_chapters", str(len(labels) + 1)]
args += ["-frames:v", str(frames),
         "-c:v", "libx264", "-preset", "slow", "-crf", "18", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k",
         "-movflags", "+faststart", out]
try:
    subprocess.run(args, check=True)
finally:
    if meta and os.path.exists(meta):
        os.remove(meta)
PY
}

command -v ffmpeg >/dev/null && command -v ffprobe >/dev/null || { echo "ffmpeg and ffprobe are needed" >&2; exit 1; }
if [ "$MIX_ONLY" = 0 ]; then
  [ -x "$UNITY" ] || { echo "Unity $VERSION not found at $UNITY" >&2; exit 1; }
  [ -n "${DISPLAY:-}${WAYLAND_DISPLAY:-}" ] || refuse "no desktop session for the Editor's window (DISPLAY is not set)"
  [ -f "$PROJECT/ProjectSettings/ProjectVersion.txt" ] || refuse "no Unity project at $PROJECT to record in"
  guard
  if [ "$PROJECT" != "$ROOT" ] && [ "$AS_IS" = 0 ]; then sync_mirror; fi
  if [ "$AS_IS" = 1 ]; then
    [ "$PROJECT" != "$ROOT" ] || { echo "--as-is records the mirror: give --copy too" >&2; exit 2; }
    echo "recording the mirror as it stands (not synced)"
  fi
  # scripts that do not compile would hold the Editor at its Safe Mode dialog until the time limit: check them first
  # (Tools/compile-check.py: a few seconds, nothing written; this project borrows the mirror's package assemblies, as
  # its Library may lag behind a package added since the Editor last opened it)
  refs=()
  mirror="$(dirname "$ROOT")/.why-2-build"
  if [ "$PROJECT" = "$ROOT" ] && [ -d "$mirror/Library/ScriptAssemblies" ]; then refs=(--refs-from "$mirror"); fi
  if ! check=$(python3 "$ROOT/Tools/compile-check.py" --editor --project "$PROJECT" "${refs[@]}" 2>&1); then
    if grep -q "error CS" <<<"$check"; then
      grep "error CS" <<<"$check" | head -10 >&2
      refuse "the scripts in $PROJECT do not compile"
    fi
    echo "compile check not run: $(tail -1 <<<"$check")" >&2
  fi
fi

# an Editor that never finishes (a dialog, a hang) must not hold the machine: four times the video's length and 20
# minutes more, the whole tour counted as 40 minutes
LIMIT=$(awk -v s="$SECONDS_TO_RECORD" 'BEGIN { v = s == "opening" ? 120 : s > 0 ? s : 2400; printf "%d", 4 * v + 1200 }')
mkdir -p "$PROJECT/Captures" "$PROJECT/Logs" "$ROOT/Captures"
for o in $ORIENTATIONS; do
  NAME="why-$o$TAG"
  RAW="$PROJECT/Captures/$NAME-raw"
  if [ "$MIX_ONLY" = 0 ]; then
    # a log of its own for each recording, dated as work/mirror-build.sh dates its builds', so a new capture never
    # overwrites an earlier one's (October's whole-tour logs are capture-<o>.log)
    LOG="$PROJECT/Logs/capture-$o$TAG-$(date +%Y%m%d-%H%M%S).log"
    # the Editor before this one may still be closing
    for _ in $(seq 60); do unity_running || break; sleep 1; done
    guard
    rm -f "$RAW.webm" "$RAW.mp4" "$RAW.cues.json"
    echo "recording $o in $PROJECT (log: $LOG); leave the Editor's window visible"
    start=$(date +%s)
    status=0
    # -nointro: the startup opening stays off (the tour's first stop plays it); --foreground: Ctrl+C still reaches the Editor
    timeout --foreground -k 120 "$LIMIT" "$UNITY" -projectPath "$PROJECT" -buildTarget Linux64 -nointro \
      -executeMethod Why.EditorTools.WalkthroughCapture.FromCommandLine \
      -captureOrientation "$o" -captureSeconds "$SECONDS_TO_RECORD" -captureOut "Captures/$NAME-raw" \
      -logFile "$LOG" || status=$?
    echo "the Editor ran $(( $(date +%s) - start )) s (exit status $status); its log: $LOG"
  else
    # the log of the recording being mixed again
    LOG="$(latest_log "$o$TAG")"
    echo "mixing $o again from $RAW (its Editor log: ${LOG:-none in $PROJECT/Logs})"
  fi

  pic="$RAW.webm"
  [ -s "$pic" ] || pic="$RAW.mp4"
  if [ ! -s "$pic" ] || [ ! -s "$RAW.cues.json" ]; then
    echo "no recording for $o in $PROJECT/Captures (see ${LOG:-the Editor log}):" >&2
    if [ -n "$LOG" ]; then grep -E "\[Why\] capture|error CS[0-9]+" "$LOG" 2>/dev/null | tail -8 >&2 || true; fi
    exit 1
  fi

  OUT="$ROOT/Captures/$NAME"
  rm -f "$OUT.new.mp4"
  if ! mix "$pic" "$RAW.cues.json" "$CLIPS" "$OUT.new.mp4" "$FORCE"; then
    rm -f "$OUT.new.mp4"
    echo "no MP4 made for $o; $NAME.mp4 stays as it was" >&2
    exit 1
  fi
  keep "$OUT"
  mv "$OUT.new.mp4" "$OUT.mp4"
  cp "$RAW.cues.json" "$OUT.cues.json"
  echo "wrote $OUT.mp4 ($(ffprobe -v error -show_entries format=duration -of csv=p=0 "$OUT.mp4") s) and its cue log"
done

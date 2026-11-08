#!/usr/bin/env bash
# Builds the Meta Quest APK (both scenes, OpenXR) in batch mode, and optionally installs it on a connected headset.
#
# Usage: Tools/build-quest.sh [--install] [--copy] [--unity PATH]
#   --copy     build from a mirror of the project (../.why-2-build) so it works while the Editor has this project
#              open, synced first as work/mirror-build.sh and Tools/capture-walkthrough.sh sync it (the source trees
#              only); the mirror keeps its own Library, so later builds are incremental
#   --install  adb install the APK onto the headset connected by USB (developer mode on, USB debugging allowed)
# Output: Builds/Quest/why.apk (in the project, or copied back from the mirror).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY=""
INSTALL=0
COPY=0
while [ $# -gt 0 ]; do
  case "$1" in
    --install) INSTALL=1 ;;
    --copy) COPY=1 ;;
    --unity) UNITY="$2"; shift ;;
    *) echo "unknown option $1" >&2; exit 2 ;;
  esac
  shift
done

VERSION="$(sed -n 's/^m_EditorVersion: //p' "$ROOT/ProjectSettings/ProjectVersion.txt")"
if [ -z "$UNITY" ]; then
  for c in "$HOME/Unity/Hub/Editor/$VERSION/Editor/Unity" "/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity"; do
    [ -x "$c" ] && UNITY="$c" && break
  done
fi
[ -x "$UNITY" ] || { echo "Unity $VERSION not found; pass --unity PATH" >&2; exit 1; }

PROJECT="$ROOT"
if [ "$COPY" = 1 ]; then
  PROJECT="$(dirname "$ROOT")/.why-2-build"
  mkdir -p "$PROJECT"
  # The source trees only, as Tools/capture-walkthrough.sh syncs them. Never the repository's root: its --delete would
  # remove the mirror's own work/, Shots/ and research/. Each tree by content (-c, no times: copied times make Unity
  # reimport untouched assets) with --delete inside it, keeping the three assets the mirror's Unity reserialized and
  # leaving out the 152 MB social graph DB (StreamingAssets), which the app does not read at runtime.
  for d in Assets Packages ProjectSettings Tools Docs; do
    rsync -rlpDc --delete \
      --exclude '/StreamingAssets/' --exclude '/StreamingAssets.meta' \
      --exclude '/DefaultVolumeProfile.asset' --exclude '/UniversalRenderPipelineGlobalSettings.asset' --exclude '/Settings/WhyURP.asset' \
      --exclude '.cache/' --exclude '__pycache__/' \
      "$ROOT/$d/" "$PROJECT/$d/" || { echo "SYNC FAILED: $d" >&2; exit 1; }
  done
  # a new mirror has none of those three assets yet: they start as this repository's
  for f in Assets/DefaultVolumeProfile.asset Assets/UniversalRenderPipelineGlobalSettings.asset Assets/Settings/WhyURP.asset; do
    [ -e "$PROJECT/$f" ] || [ ! -e "$ROOT/$f" ] || cp "$ROOT/$f" "$PROJECT/$f"
  done
  echo "synced the source trees into $PROJECT"
elif [ -e "$ROOT/Temp/UnityLockfile" ] && pgrep -f "projectpath $ROOT" >/dev/null 2>&1; then
  echo "The Editor has this project open: close it, or run with --copy" >&2
  exit 1
fi

LOG="$PROJECT/Logs/quest-build.log"
mkdir -p "$PROJECT/Logs"
echo "Building the Quest APK from $PROJECT (log: $LOG) ..."
# Unity sometimes crashes while shutting down after a finished build: judge by the log, not the exit code
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" -buildTarget Android \
  -executeMethod Why.EditorTools.QuestBuild.BuildFromCommandLine -logFile "$LOG" || true
if ! grep -q "\[Why\] Quest build Succeeded" "$LOG"; then
  grep -E "error CS|\[Why\]|BuildFailedException|What went wrong" -A3 "$LOG" | head -40 >&2
  echo "Build failed; see $LOG" >&2
  exit 1
fi
grep "\[Why\] Quest build" "$LOG" | tail -1
APK="$PROJECT/Builds/Quest/why.apk"
if [ "$PROJECT" != "$ROOT" ]; then
  mkdir -p "$ROOT/Builds/Quest"
  cp "$APK" "$ROOT/Builds/Quest/why.apk"
  APK="$ROOT/Builds/Quest/why.apk"
fi
echo "APK: $APK"

if [ "$INSTALL" = 1 ]; then
  ADB="$(command -v adb || true)"
  [ -n "$ADB" ] || ADB="$(dirname "$UNITY")/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb"
  "$ADB" devices
  "$ADB" install -r "$APK"
  echo "Installed: on the headset open Library > Unknown Sources > Why"
fi

#!/usr/bin/env python3
"""Voices narration scripts with the ElevenLabs text-to-speech API (voice: JohnV4).

Usage:
    python3 Tools/synthesize-narration-elevenlabs.py [--key-file PATH] [--voice ID] [--model ID]
                                                    [--only SCRIPT] [--force] [--dry-run]

Reads every narration script listed in SCRIPTS below (JSON: {"steps":[{"id":..., "narration":...}]}),
writes one mono Ogg Vorbis clip per step to the script's audio folder under Assets/Resources/Audio
(Unity loads them with Resources.Load<AudioClip>). Existing clips are kept unless --force, or unless
the step's text changed since it was voiced (a small manifest next to the clips remembers the text hash).

The key is read from $ELEVENLABS_API_KEY or from --key-file (default: ~/Desktop/eleven-key.txt).
It is never printed and never written into the project. ffmpeg converts the MP3 from the API to Ogg.
Neighbouring steps are sent as previous_text / next_text so the delivery flows from clip to clip.
"""
import argparse
import hashlib
import json
import os
import shutil
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# script (relative to ROOT) -> audio folder (relative to ROOT)
SCRIPTS = {
    "Assets/Resources/Data/Narration/origin.json": "Assets/Resources/Audio/Origin",
    "Assets/Resources/Data/tour_narration.json": "Assets/Resources/Audio/Tour",
    "Assets/Resources/Data/Narration/economy.json": "Assets/Resources/Audio/EconomyTour",
    "Assets/Resources/Data/Narration/mind.json": "Assets/Resources/Audio/Mind",
}

DEFAULT_VOICE = "7NoxJCAEPTXbnfIvyaF6"  # JohnV4 (cloned)
DEFAULT_MODEL = "eleven_multilingual_v2"
API = "https://api.elevenlabs.io/v1/text-to-speech/{voice}?output_format=mp3_44100_128"


def read_key(path):
    key = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if key:
        return key
    path = os.path.expanduser(path)
    if not os.path.exists(path):
        sys.exit(f"No ElevenLabs key: set ELEVENLABS_API_KEY or put it in {path}")
    with open(path, encoding="utf-8") as f:
        return f.readline().strip()


def synthesize(key, voice, model, text, prev_text, next_text, out_mp3):
    body = {
        "text": text,
        "model_id": model,
        "voice_settings": {"stability": 0.5, "similarity_boost": 0.8, "style": 0.15, "use_speaker_boost": True},
    }
    if prev_text:
        body["previous_text"] = prev_text
    if next_text:
        body["next_text"] = next_text
    req = urllib.request.Request(
        API.format(voice=voice),
        data=json.dumps(body).encode("utf-8"),
        headers={"xi-api-key": key, "Content-Type": "application/json", "Accept": "audio/mpeg"},
        method="POST",
    )
    for attempt in range(5):
        try:
            with urllib.request.urlopen(req, timeout=180) as resp, open(out_mp3, "wb") as f:
                shutil.copyfileobj(resp, f)
            return
        except urllib.error.HTTPError as e:
            detail = e.read().decode("utf-8", "replace")[:300]
            if e.code in (429, 500, 502, 503, 504) and attempt < 4:
                time.sleep(2 ** attempt * 3)
                continue
            raise RuntimeError(f"HTTP {e.code}: {detail}") from None
        except urllib.error.URLError:
            if attempt < 4:
                time.sleep(2 ** attempt * 3)
                continue
            raise


def duration(path):
    try:
        out = subprocess.run(
            ["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", path],
            capture_output=True, text=True, check=True).stdout.strip()
        return float(out)
    except Exception:
        return 0.0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--key-file", default="~/Desktop/eleven-key.txt")
    ap.add_argument("--voice", default=DEFAULT_VOICE)
    ap.add_argument("--model", default=DEFAULT_MODEL)
    ap.add_argument("--only", help="substring of a script path to limit the run")
    ap.add_argument("--force", action="store_true")
    ap.add_argument("--dry-run", action="store_true", help="list what would be voiced, no API calls")
    args = ap.parse_args()

    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        sys.exit("ffmpeg is required (it converts the API's MP3 to Ogg Vorbis for Unity)")
    key = None if args.dry_run else read_key(args.key_file)

    grand_total, grand_made, grand_chars = 0.0, 0, 0
    for script, audio_dir in SCRIPTS.items():
        if args.only and args.only not in script:
            continue
        spath = os.path.join(ROOT, script)
        if not os.path.exists(spath):
            print(f"skip {script} (missing)")
            continue
        with open(spath, encoding="utf-8") as f:
            steps = json.load(f)["steps"]
        out_dir = os.path.join(ROOT, audio_dir)
        os.makedirs(out_dir, exist_ok=True)
        manifest_path = os.path.join(out_dir, "voiced.manifest")  # not a Unity-importable extension
        manifest = {}
        if os.path.exists(manifest_path):
            with open(manifest_path, encoding="utf-8") as f:
                manifest = json.load(f)
        print(f"{script}: {len(steps)} steps -> {audio_dir}")
        for i, s in enumerate(steps):
            sid, text = s["id"], s["narration"].strip()
            digest = hashlib.sha1(f"{args.voice}|{args.model}|{text}".encode("utf-8")).hexdigest()
            ogg = os.path.join(out_dir, sid + ".ogg")
            if os.path.exists(ogg) and not args.force and manifest.get(sid) == digest:
                grand_total += duration(ogg)
                continue
            if args.dry_run:
                print(f"  would voice {sid} ({len(text)} chars)")
                grand_chars += len(text)
                continue
            prev_text = steps[i - 1]["narration"] if i > 0 else None
            next_text = steps[i + 1]["narration"] if i + 1 < len(steps) else None
            with tempfile.TemporaryDirectory() as tmp:
                mp3 = os.path.join(tmp, "clip.mp3")
                synthesize(key, args.voice, args.model, text, prev_text, next_text, mp3)
                subprocess.run([ffmpeg, "-v", "error", "-y", "-i", mp3, "-c:a", "libvorbis", "-q:a", "4",
                                "-ac", "1", ogg], check=True)
            manifest[sid] = digest
            with open(manifest_path, "w", encoding="utf-8") as f:
                json.dump(manifest, f, indent=1)
            secs = duration(ogg)
            grand_total += secs
            grand_made += 1
            grand_chars += len(text)
            print(f"  {sid:<24} {secs:6.1f} s")
    if args.dry_run:
        print(f"Dry run: {grand_chars} characters to voice")
    else:
        print(f"Done: {grand_made} new clips, {grand_chars} characters; {grand_total / 60:.1f} minutes of narration in all")


if __name__ == "__main__":
    main()

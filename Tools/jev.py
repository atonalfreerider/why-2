#!/usr/bin/env python3
"""Calls Jev (TypeSafe System One) with a request file; the cross-platform twin of Tools/jev.ps1.

Usage (from the repository root):
    python3 Tools/jev.py REQUEST.json [--validate-only]

REQUEST.json holds {"state": ..., "questions": {...}} (see Tools/jev-smoke.json). Prints the answers as JSON.

The key comes from $TYPESAFE_API_KEY (a direct key, "apikey_..." -> api.typesafe.ai), $AI_GATEWAY_API_KEY
(Vercel AI Gateway), or the first line of ~/Desktop/jev-key.txt (routed by its prefix). It is never printed.
Other tools import call_jev() from here.
"""
import json
import os
import sys
import urllib.error
import urllib.request

KEY_FILE = os.path.expanduser("~/Desktop/jev-key.txt")
DIRECT_URL = "https://api.typesafe.ai/v1/systemone"
GATEWAY_URL = "https://ai-gateway.vercel.sh/typesafe/v1/systemone"


def _key():
    """(key, direct) or (None, False) when no credential is available."""
    k = os.environ.get("TYPESAFE_API_KEY", "").strip()
    if k:
        return k, True
    k = os.environ.get("AI_GATEWAY_API_KEY", "").strip()
    if k:
        return k, False
    if os.path.exists(KEY_FILE):
        with open(KEY_FILE, encoding="utf-8") as f:
            k = f.readline().strip()
        if k:
            return k, k.startswith("apikey_")
    return None, False


def available():
    return _key()[0] is not None


def validate(request):
    if not isinstance(request, dict) or "state" not in request or not request.get("questions"):
        raise ValueError("Request must contain state and questions. See Tools/jev-smoke.json.")
    for name, q in request["questions"].items():
        if q.get("type") not in ("noul", "choice", "score") or "instructions" not in q:
            raise ValueError(f"Question '{name}' needs a supported type and instructions.")
        if q["type"] in ("choice", "score") and "criteria" not in q:
            raise ValueError(f"Question '{name}' needs criteria.")


def call_jev(state, questions, timeout=90):
    """Returns the response's answers dict. Raises RuntimeError without leaking the key or the state."""
    validate({"state": state, "questions": questions})
    key, direct = _key()
    if key is None:
        raise RuntimeError("No Jev credential: set TYPESAFE_API_KEY / AI_GATEWAY_API_KEY or create ~/Desktop/jev-key.txt")
    body = {"model": "jev-latest" if direct else "typesafe-ai/jev", "state": state, "questions": questions}
    req = urllib.request.Request(DIRECT_URL if direct else GATEWAY_URL, data=json.dumps(body).encode("utf-8"),
                                 headers={"Authorization": f"Bearer {key}", "Content-Type": "application/json"},
                                 method="POST")
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            result = json.load(resp)
    except urllib.error.HTTPError as e:
        raise RuntimeError(f"Jev request failed (HTTP {e.code}). Check the key, balance and provider availability.") from None
    if "answers" not in result:
        raise RuntimeError("Jev returned no answers.")
    return result["answers"]


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    with open(sys.argv[1], encoding="utf-8") as f:
        request = json.load(f)
    validate(request)
    if "--validate-only" in sys.argv:
        print("Valid request structure. No network request made.")
        return
    print(json.dumps(call_jev(request["state"], request["questions"]), indent=2))


if __name__ == "__main__":
    main()

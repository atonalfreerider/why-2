#!/usr/bin/env python3
"""Re-fetches the source of every conflict/coercion event the world-economy research found and asks Jev whether the
page supports the claim (a citation check), alongside a plain count of the claim's numbers found on the page.
Usage: verify-events.py OUT_JSON. Downloads go to research/global/verify/. The Jev key is read by Tools/jev.py."""
import hashlib, html, json, os, re, subprocess, sys
from concurrent.futures import ThreadPoolExecutor
sys.path.insert(0, "/home/john/Desktop/Unity/why-2/Tools")
import jev  # noqa: E402
RES = "/home/john/Desktop/Unity/.why-2-build/research/global"
DL = os.path.join(RES, "verify"); os.makedirs(DL, exist_ok=True)
doms = json.load(open(os.path.join(RES, "domains.json"), encoding="utf-8"))
events = [dict(e, domain=d["key"]) for d in doms for e in d["research"]["events"]]

def fetch(url):
    h = hashlib.sha1(url.encode()).hexdigest()[:16]; path = os.path.join(DL, h)
    if not os.path.exists(path):
        r = subprocess.run(["curl", "-sL", "--max-time", "45", "-A", "Mozilla/5.0 (X11; Linux x86_64) why-2 source check", "-o", path, "-w", "%{http_code} %{content_type}", url], capture_output=True, text=True)
        meta = r.stdout.strip()
        open(path + ".meta", "w").write(meta)
    meta = open(path + ".meta").read() if os.path.exists(path + ".meta") else ""
    if not os.path.exists(path) or os.path.getsize(path) < 200: return meta, ""
    raw = open(path, "rb").read()
    if raw[:4] == b"%PDF":
        t = subprocess.run(["pdftotext", "-layout", path, "-"], capture_output=True, text=True).stdout
    else:
        s = raw.decode("utf-8", "replace")
        s = re.sub(r"(?is)<(script|style|noscript)[^>]*>.*?</\1>", " ", s)
        t = html.unescape(re.sub(r"(?s)<[^>]+>", " ", s))
    return meta, re.sub(r"\s+", " ", t)

def numbers(s):
    return [n for n in re.findall(r"\d[\d,]*\.?\d*", s) if len(n.replace(",", "").replace(".", "")) >= 2]

def window(text, keys, size=3500):
    if not text: return ""
    best, at = -1, 0
    for i in range(0, max(1, len(text) - size), 700):
        w = text[i:i + size]; score = sum(1 for k in keys if k in w)
        if score > best: best, at = score, i
    return text[at:at + size]

Q = {
  "supported": {"type": "noul", "instructions": "Does `evidence.excerpt` (text fetched from `evidence.url`) say what `claim.passage` quotes or paraphrases from it, so that the page is a correct source for that passage?"},
}
def norm(s): return re.sub(r"\s+", " ", re.sub(r"[‘’“”]", "'", s)).strip().lower()
def segments(passage):
    body = re.sub(r"^[^:']{0,80}:\s*", "", passage)   # drop a leading "EO 14066 Sec. 1(a):" style label
    parts = re.split(r"\s*\.\.\.\s*|'\s*;\s*'|\s*\|\s*", body)
    return [norm(x.strip(" '\"")) for x in parts if len(x.strip(" '\"")) >= 30]
def check(e):
    url = e.get("url", "")
    if "://" not in url: return dict(name=e["name"], url=url, status="no url")
    meta, text = fetch(url)
    t = norm(text); segs = segments(e.get("passage", ""))
    hits = [x for x in segs if x[:60] in t]
    nums = numbers(e.get("magnitude", "")); nfound = [n for n in nums if n in text]
    row = dict(name=e["name"], kind=e.get("kind"), start=e.get("start"), domain=e["domain"], url=url, http=meta, page_chars=len(text),
               quotes=len(segs), quotes_found=len(hits), magnitude_numbers=len(nums), magnitude_numbers_found=len(nfound))
    if not text:
        row["status"] = "fetch failed"; return row
    at = t.find(hits[0][:60]) if hits else -1
    ex = t[max(0, at - 1500): at + 2500] if at >= 0 else window(t, [norm(n) for n in nfound])
    try:
        a = jev.call_jev({"claim": {"name": e["name"], "passage": e.get("passage")}, "evidence": {"url": url, "excerpt": ex}}, Q)
        row.update(status="checked", supported=round(a["supported"]["noul"], 3))
    except Exception as x:  # noqa: BLE001
        row.update(status="jev error", error=str(x)[:160])
    return row
with ThreadPoolExecutor(6) as ex:
    rows = list(ex.map(check, events))
json.dump(rows, open(sys.argv[1], "w", encoding="utf-8"), indent=1, ensure_ascii=False)
ok = [r for r in rows if r.get("status") == "checked"]
print(f"{len(rows)} events: {len(ok)} checked, {sum(1 for r in rows if r.get('status') == 'fetch failed')} fetch failed, {sum(1 for r in rows if r.get('status') == 'jev error')} Jev errors")
print(f"supported >= .7: {sum(1 for r in ok if r['supported'] >= .7)}; .3-.7: {sum(1 for r in ok if .3 <= r['supported'] < .7)}; < .3: {sum(1 for r in ok if r['supported'] < .3)}")
print("quotes found verbatim:", sum(1 for r in ok if r["quotes_found"] > 0), "of", sum(1 for r in ok if r["quotes"] > 0), "events with quotes")
for r in sorted(ok, key=lambda r: (r["supported"], r["quotes_found"]))[:24]:
    print(f"  {r['supported']:.2f} quotes {r['quotes_found']}/{r['quotes']} magnitude nums {r['magnitude_numbers_found']}/{r['magnitude_numbers']} {str(r['start'])[:10]} {r['name'][:64]}")

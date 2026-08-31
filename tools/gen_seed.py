import argparse
import json
import os
import ssl
import sys
import time
import urllib.request
import urllib.error

TENRAI = "https://api.tenrai.org/v1"
TENRAI_UA = "MALPlus/2.0 (seed generator)"
ANIME_TYPES = {"TV", "Movie", "OVA", "ONA", "Special", "Music", "CM", "PV", "TV Special"}
ANIME_FIELDS = [
    "mal_id", "title", "title_english", "title_japanese", "title_synonyms",
    "type", "status", "episodes", "score", "rank", "popularity", "members",
    "favorites", "season", "year", "synopsis", "duration", "rating", "source",
]

REQ_INTERVAL = 1.0
retry_after = 0.0
last_429 = 0
last_request = 0.0


def throttle():
    global last_request
    now = time.time()
    wait = last_request + REQ_INTERVAL - now
    if wait > 0:
        time.sleep(wait)
    last_request = time.time()


def http_get(path, retries=4):
    global retry_after, last_429
    context = ssl._create_unverified_context()
    url = TENRAI + path
    for attempt in range(retries):
        if retry_after > 0:
            time.sleep(retry_after)
            retry_after = 0.0
        throttle()
        try:
            req = urllib.request.Request(url, headers={"User-Agent": TENRAI_UA})
            with urllib.request.urlopen(req, timeout=45, context=context) as resp:
                return resp.read()
        except urllib.error.HTTPError as e:
            if e.code == 429:
                last_429 += 1
                ra = e.headers.get("Retry-After")
                retry_after = min((float(ra) if ra else 0) or (1.5 * (last_429 // 4 + 1)), 45.0)
                if last_429 % 8 == 0:
                    print(f"   ~{last_429} throttled so far (+{retry_after:.1f}s)", file=sys.stderr)
                if attempt >= 2:
                    return None
                continue
            if e.code == 504:
                if attempt + 1 >= retries:
                    return None
                print(f"   504 on {url} ({attempt + 1}/{retries}), retry in 3s", file=sys.stderr)
                time.sleep(3)
                continue
            if e.code in (404, 400, 405):
                if e.code == 404:
                    print(f"   skip 404 {url}", file=sys.stderr)
                return None
            if e.code == 403:
                print(f"   403 on {url}, sleeping 20s", file=sys.stderr)
                time.sleep(20)
                continue
            return None
        except ssl.SSLError as e:
            print(f"   SSL on {url}: {e}", file=sys.stderr)
            return None
        except Exception as e:
            print(f"   conn error on {url}: {e} ({attempt + 1}/{retries})", file=sys.stderr)
            time.sleep(4)
    return None


def tget(path, retries=4):
    data = http_get(path, retries)
    return json.loads(data.decode("utf-8")) if data else None


def pool(endpoint, cap):
    ids = []
    page = 1
    while True:
        data = tget(f"{endpoint}&page={page}&limit=25")
        if not data or not data.get("data"):
            break
        for e in data["data"]:
            mid = e.get("mal_id")
            if mid and e.get("type") in ANIME_TYPES and mid not in ids:
                ids.append(mid)
                if len(ids) >= cap:
                    return ids
        if not data.get("pagination", {}).get("has_next_page", False):
            break
        page += 1
        if page > 40:
            break
    return ids


def whitelist_details(raw):
    out = {}
    for k in ANIME_FIELDS:
        if k in raw:
            v = raw[k]
            if v is None:
                continue
            if k == "title_synonyms":
                out[k] = [s for s in v if isinstance(s, str)]
                continue
            out[k] = v
    for nested in ("aired", "broadcast", "trailer"):
        if nested in raw and isinstance(raw[nested], dict):
            on = {}
            for nk in ("from", "to", "string", "embed_url", "url", "youtube_id"):
                if nk in raw[nested] and raw[nested][nk] is not None:
                    on[nk] = raw[nested][nk]
            if on:
                out[nested] = on
    if "images" in raw and isinstance(raw.get("images"), dict):
        jpg = raw["images"].get("jpg") or {}
        if jpg.get("image_url"):
            out["images"] = {"jpg": {"image_url": jpg["image_url"]}}
    for coll in ("studios", "genres", "themes"):
        if coll in raw and isinstance(raw[coll], list):
            out[coll] = [{"name": e["name"]} for e in raw[coll] if isinstance(e, dict) and e.get("name")]
    return out


def fetch_episodes(mal_id):
    eps = []
    page = 1
    while True:
        data = tget(f"/anime/{mal_id}/episodes?page={page}", retries=2)
        if not data or not data.get("data") or not data["data"]:
            break
        for e in data["data"]:
            eps.append({
                "mal_id": e.get("mal_id"),
                "title": e.get("title") or "",
                "aired": e.get("aired") or None,
                "filler": bool(e.get("filler")),
                "recap": bool(e.get("recap")),
            })
        if not data.get("pagination", {}).get("has_next_page", False):
            break
        page += 1
        if page > 60:
            break
    return eps


def state_path(out):
    return os.path.splitext(out)[0] + ".state.json"


def save_state(out, items, origin, scores):
    with open(state_path(out), "w", encoding="utf-8") as f:
        json.dump({"origin": origin, "scores": scores, "items": items}, f, ensure_ascii=False, separators=(",", ":"))


def load_state(out):
    if not os.path.exists(state_path(out)):
        return None
    with open(state_path(out), encoding="utf-8") as f:
        st = json.load(f)
    origin = {int(k): v for k, v in (st.get("origin") or {}).items()}
    scores = {int(k): v for k, v in (st.get("scores") or {}).items()}
    return origin, scores, st.get("items", [])


def main():
    ap = argparse.ArgumentParser(description="Generate MAL+ seed bundle from Tenrai (dev-only)")
    ap.add_argument("--top", type=int, default=200)
    ap.add_argument("--yearcap", type=int, default=200)
    ap.add_argument("--out", default=r"D:\Descargas\MALPlus\MALClient.Android\Assets\seed_bundle_v1.json")
    ap.add_argument("--resume", action="store_true", help="skip fetching, use existing .state.json checkpoint")
    args = ap.parse_args()

    origin = {}
    scores = {}
    items = []

    resumed = False
    if args.resume:
        state = load_state(args.out)
        if state:
            origin, scores, items = state
            resumed = True
            print(f"[resume] {len(items)} items from {state_path(args.out)}", file=sys.stderr)

    if not resumed:
        print(f"[1/4] Tenrai top by popularity (target {args.top})...", file=sys.stderr)
        pop = pool("/anime?order_by=popularity&sort=asc", args.top)
        print(f"     got {len(pop)}", file=sys.stderr)

        print(f"[2/4] Tenrai top by score (target {args.top})...", file=sys.stderr)
        score_ids = pool("/anime?order_by=score&sort=desc", args.top)
        print(f"     got {len(score_ids)}", file=sys.stderr)

        season_ids = []
        seasons = [(2026, "winter"), (2026, "spring"), (2026, "summer"),
                   (2025, "winter"), (2025, "spring"), (2025, "summer"), (2025, "fall")]
        for year, season in seasons:
            got = pool(f"/seasons/{year}/{season}?", args.yearcap // len(seasons) + 10)
            season_ids = [i for i in season_ids if i not in got] + got
            print(f"[3/4] season {year}-{season}: {len(got)}", file=sys.stderr)
        season_ids = season_ids[: args.yearcap]

        for i in pop:
            origin[i] = "top-pop"
        for i in score_ids:
            origin.setdefault(i, "top-score")
        for i in season_ids:
            origin.setdefault(i, "season")

        tids = sorted(origin)
        print(f"POOL: {len(pop)} top-pop, {len(score_ids)} top-score, {len(season_ids)} season -> {len(tids)} total",
              file=sys.stderr)
        print(f"[4/4] fetching details+episodes for {len(tids)} titles...", file=sys.stderr)

        for n, mid in enumerate(tids, 1):
            raw = tget(f"/anime/{mid}/full")
            if not raw or not raw.get("data"):
                continue
            d = raw["data"]
            if d.get("type") not in ANIME_TYPES:
                continue
            scores[mid] = d.get("score") or 0
            item = {"id": mid, "status": d.get("status") or "", "details": whitelist_details(d)}
            eps = fetch_episodes(mid)
            if eps:
                item["episodes"] = eps
            items.append(item)
            if n % 25 == 0 or n == len(tids):
                print(f"   {n}/{len(tids)} (ok {len(items)})", file=sys.stderr)

        save_state(args.out, items, origin, scores)
        print(f"CHECKPOINT saved: {len(items)} items", file=sys.stderr)

    pop_items = [it for it in items if origin[it["id"]] == "top-pop"][: args.top]
    score_items = [it for it in items if origin[it["id"]] == "top-score"][: args.top]
    season_items = [it for it in items if origin[it["id"]] == "season"]
    season_items.sort(key=lambda it: -(scores.get(it["id"], 0)))
    season_items = season_items[: args.yearcap]

    ordered = list(pop_items)
    for it in score_items:
        if it not in ordered:
            ordered.append(it)
    for it in season_items:
        if it not in ordered:
            ordered.append(it)

    bundle = {"version": 1, "generated": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()), "count": len(ordered),
              "items": ordered}
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(bundle, f, ensure_ascii=False, separators=(",", ":"))
    size = os.path.getsize(args.out)
    print(f"DONE: {len(ordered)} items ({len(pop_items)} top-pop, {len(score_items)} top-score, "
          f"{len(season_items)} season), {size / 1024 / 1024:.2f} MB -> {args.out}")


if __name__ == "__main__":
    main()
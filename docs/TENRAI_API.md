# Tenrai API — Referencia de uso en MAL+

Tenrai (`https://api.tenrai.org/v1`) is a free **Unofficial** REST API for the full MyAnimeList public catalogue.
It implements the **Jikan v4 schema**, so it can replace Jikan with only a base-URL swap.

This file documents which endpoints MAL+ uses today, which structured endpoints exist that could replace
fragile HTML scraping / third-party feeds, and the exact fields we verified live (2026-09-01).

## Auth & Rate limits (verified from docs)
- Public requests need **no credentials**.
- `X-Server-Key` raises limits but is for server-side only — **MUST NOT be embedded in the mobile client**.
- Public limits: **120 RPM / 4 RPS / 40,000 RPD** per IP.
- On `429` honor `Retry-After`. `TenraiClient` already spaces requests 500ms and retries 4x.

## JSON conventions
- Single-value unknown field → `null`; empty list/nested object → `[]`/`{}`.
- Score unknown → `0`. Dates/timestamps → ISO8601 **UTC**.
- Error envelope: `{ status, type, message, error, path }`.

## SFW filtering
- `?sfw` filters out `R+`/`Rx`/Hentai/Erotica. `?sfw-strict` also filters Ecchi.

---

## Endpoints used by MAL+ today (mapped to source files)

### Catalogue / details
| Endpoint | Field | Use in MAL+ |
|---|---|---|
| `GET /anime?q=&sfw&genres=&order_by=` | search | `AnimeSearchQuery` (`GetPaginatedAsync("anime?...")`) |
| `GET /manga?q=&sfw` | manga search | `MangaSearchQuery` |
| `GET /anime/{id}` | anime details (36 keys, **no** relations/theme/external) | `AnimeDetailsMalQuery.FetchFromTenraiAsync` |
| `GET /manga/{id}` | manga details | `AnimeDetailsMalQuery` (manga) |
| `GET /anime/{id}/full` | full details (**41 keys**, incl. relations/theme/external/streaming/moreinfo) | `AnimeGeneralDetailsQuery.BuildFromFullData` (currently only ~15 fields read) |
| `GET /manga/{id}/full` | manga full details | `AnimeGeneralDetailsQuery` |
| `GET /anime/{id}/themes` | OP/ED strings | `AnimeDetailsMalQuery` (separate 2nd call) |
| `GET /anime/{id}/episodes` | episode list: `mal_id,url,title,title_japanese,title_romanji,aired(ISO UTC),filler,recap,forum_url,score` | `AnimeEpisodesQuery.GetEpisodes` / `GetLastEpisodesAsync` |
| `GET /anime/{id}/characters` | characters (with `voice_actors`) | `AnimeCharactersStaffQuery` |
| `GET /manga/{id}/characters` | manga characters | `AnimeCharactersStaffQuery` |
| `GET /anime/{id}/staff` | staff (with `positions`) | `AnimeCharactersStaffQuery` |
| `GET /anime/{id}/reviews` | reviews | `AnimeReviewsQuery.FetchReviewsFromTenraiAsync` |

### Discovery
| Endpoint | Use in MAL+ |
|---|---|
| `GET /seasons/{year}/{season}` (or `/seasons/now`) | `AnimeSeasonalQuery` |
| `GET /seasons` | `AnimeListViewModel` (available seasons list) |
| `GET /anime?producers=&order_by=score` / `?genres=` | `AnimeGenreStudioQuery` |

---

## Endpoints NOT used today (structured replacements available)

These structured endpoints would eliminate fragile HTML scraping or third-party feeds:

| Endpoint | Replaces | Notes (verified) |
|---|---|---|
| **`GET /schedules`** | `mylovelyvps.xyz airing.json` + AniList `AnimeAniListScheduleQuery` | Returns **~297 airing series** with full object: `mal_id,title,images,status="Currently Airing",airing=true,broadcast{day,time,timezone,string},aired{from,to,string},genres,score`. Today the app used a 129-item feed lacking titles + an AniList GraphQL fallback. |
| **`GET /anime/{id}/full` `.relations` / `.theme` / `.background` / `.external` / `.streaming` / `.moreinfo` / `.explicit_genres` / `.demographics`** | `AnimeRelatedQuery` (HTML scraper) + separate `/themes` call + missing detail fields | Single call already returns all of these; `BuildFromFullData` ignores them. |
| **`GET /top/anime`** | `AnimeTopQuery` HTML scraper (`topanime.php`) | Full object per entry (`mal_id,title,images,type,episodes,score,rank,members,genres,status`). `filter=airing|upcoming|bypopularity|favorite`, `type=tv|movie|ova|special|ona|music|cm|pv|tv_special`. |
| **`GET /top/manga`** | `AnimeTopQuery` HTML scraper (`topmanga.php`) | `filter=publishing|upcoming|bypopularity|favorite`, `type=manga|novel|lightnovel|oneshot|doujin|manhwa|manhua`. NOTE: singular type values (scraper used plural). |
| `GET /anime/{id}/relations` | (or use `.relations` from `/full`) | Available standalone too. |
| `GET /anime/{id}/statistics` | — | Not used. |
| `GET /anime/{id}/pictures` | — | Not used. |
| `GET /anime/{id}/news` / `articles` | MAL articles scrape | Available. |
| `GET /anime/{id}/recommendations` | `AnimeDirectRecommendationsQuery`/`AnimePersonalizedRecommendationsQuery` (scrapers) | Available structured. |
| `GET /genres/anime` / `/genres/manga`, `/magazines` | — | Available. |

---

## Verified JSON shapes (live, 2026-09-01)

### `GET /anime/{id}/full` (41 keys)
```
mal_id, url, images{jpg,webp}, trailer, approved, titles[], title, title_english,
title_japanese, title_synonyms, type, source, episodes, status, airing, aired{from,to,prop,string},
duration, rating, score, scored_by, rank, popularity, members, favorites, synopsis, background,
season, year, broadcast{day,time,timezone,string}, producers[], licensors[], studios[],
genres[], explicit_genres[], themes[], demographics[], relations[{relation,entry[]}],
theme{openings[],endings[]}, external[{name,url}], streaming[], moreinfo
```
`/anime/{id}` (simple) has the same minus `relations, theme, external, streaming, moreinfo`.

Relations entry objects: `{ mal_id, type: "anime"|"manga", name, url, media_type, images{jpg} }`.

### `GET /schedules` (paginated; `filter=monday|...|sunday`, `sfw`)
Each item is the full anime object incl. `broadcast.day` (`"Mondays"`), `broadcast.time` (`"21:00"`),
`broadcast.timezone` (`"Asia/Tokyo"`), `broadcast.string` (`"Mondays at 21:00 (JST)"`), and
`aired.from`/`aired.to` ISO4217 datetimes.

### `GET /anime/{id}/episodes`
```
data: [ { mal_id, url, title, title_japanese, title_romanji, duration, aired: "2026-07-06T00:00:00+00:00",
         score, filler, recap, synopsis, replies, forum_url, images } ]
pagination: { last_visible_page, has_next_page }
```
`aired` is a **UTC ISO datetime** — ideal for exact next-episode countdown.

### `GET /top/anime` / `GET /top/manga`
Pagination `{ last_visible_page, has_next_page, current_page, items{...} }`; each entry is the full
catalogue object (same keys as `/anime/{id}`).

---

## Rate-limit budget notes
- `/schedules` is cached 8h in `AiringInfoProvider` → ~1 request per 8h. Cheap.
- Switching `AnimeDetailsMalQuery` from 2 calls (`/anime/{id}` + `/themes`) to 1 (`/full`) reduces load.
- All other calls run through `TenraiClient`'s 500ms spacing + 4x retry (within 4 RPS / 120 RPM).

Refer also to the refactor summary in `AGENTS.md` and the cache-version bump rule (`mal_details_v3_*` →
`v4` when fields added; no in-place migration).

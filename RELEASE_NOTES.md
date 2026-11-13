## What's new in 1.2.2 (Final Release)

### ⚠️ Deprecation Notice — MALClient has moved to MAL+
- **MALClient is now deprecated and will not receive further updates.**
- Active development continues on our new official modern app, **[MAL+](https://github.com/idarkalex/MALPlus)**, rebuilt from scratch with .NET MAUI, full offline sync, refreshed design, and active maintenance.
- Download MAL+ from the [MAL+ GitHub Releases](https://github.com/idarkalex/MALPlus/releases/latest).
- Added an in-app migration notice with a direct link to MAL+ and a "Don't show again" option.

## What's new in 1.2.1

### Bug fixes
- Fix: **Top novels** and **Top light novels** under Top manga showed the wrong ranking (the whole top list) because the MAL filter used the singular `type=novel`/`type=lightnovel`. Correct filters `type=novels`/`type=lightnovels` applied, and stale cached rankings are invalidated.

## What's new in 1.2.0

### Manga browsing improvements
- New "Adapted to anime" section in the side menu with three views: **All**, **Airing Now** (default) and **Upcoming Anime**, scraped directly from MyAnimeList.
- Real MyAnimeList categories for **Top manga** (All, Manga, Novels, Light Novels, One Shots, Doujinshi, Manhwa, Manhua, Popular, Favourited), selectable from the ⋮ overflow menu or the top status bar.
- Fix: manga covers (e.g. Bleach) are now loaded correctly in the adapted-to-anime lists.
- Fix: adapted-to-anime results no longer come back empty, and empty results are never cached.

### Top anime / top manga polish
- The top status bar on Top anime / Top manga now opens a dropdown to switch categories directly.
- Alternate titles, About and the changelog sections reworked.

### Release workflow
- Nightly APK is now published as a direct download (GitHub Release) instead of a zip artifact.

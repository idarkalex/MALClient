<h1 align="center">MALClient</h1>

<p align="center">
  <img src="images/app-logo.png" width="150px">
  <br><br>
  Robust MyAnimeList client application interfacing with the official MAL API, available on Android.
</p>

> This is a community-maintained fork of the [original MALClient by Drutol](https://github.com/Drutol/MALClient).

> **Important note:** most metadata (details, themes, episodes, seasonal, studios, genres, search, favourites) is served by the **Tenrai API** (**`https://api.tenrai.org/v1`**). Top anime/top manga and the "Adapted to anime" section are scraped directly from MyAnimeList. The official MAL API is used for the anime/manga list, search and scores.

### Download

Get the latest signed APK from [Releases](https://github.com/idarkalex/MALClient/releases).

### Screenshots

#### Android
<p align="center">
  <img src="images/android-preview.png">
</p>

### Features
* Anime and manga list updates.
  * Score, Status, Episodes, Volumes
  * Tags
  * Favourites
  * Start/End date
  * Rewatching
* Anime list with sorting, filters.
  * Grid view
  * Compact view
  * Detailed grid view
* Anime info.
  * Genres
  * Episodes
  * Reviews
  * Recommendations
  * Personalized anime/manga suggestions.
  * Related
  * Characters & Staff
  * Mal statistics
  * Promotional videos
* Top anime/manga with real MyAnimeList categories (top manga: All, Manga, Novels, Light Novels, One Shots, Doujinshi, Manhwa, Manhua, Popular, Favourited).
  * Category switcher in the top status bar and the ⋮ overflow menu.
* "Adapted to anime" manga section (All / Airing Now / Upcoming Anime).
* Seasonal anime
  * With multiple season selection (ordered by date, current season marked, default sort by MAL score)
* Anime by studio and genre
* Global anime & manga recommendations
* Calendar
  * With countdowns to next episode
* Mal articles
  * Mal news
* Mal messaging
* Tons of settings
* Mal profile
  * With navigation across other's profiles
  * Profile comments, you can add new ones too!
  * Profile comment converstion
* Forums
  * As native as it's possible, not wrapped website.
* System toasts/notifications and notification hub!
* Friends feeds parsed from rss channels.
* History.
* And much more!

### Compilation
No local build required: the signed APK is built automatically by GitHub Actions
(`.github/workflows/build-android.yml`) on every push to `main` and on pull requests,
and every `v*` tag additionally publishes a GitHub Release with the APK attached.
Grab the latest build from [Releases](https://github.com/idarkalex/MALClient/releases).

### "Protocol"

If you'd like for some reason to launch my app externally you can do so by using this protocol:
```
malclient://<your everyday MAL link>
```
List of all accepted urls can be found [here](MALClient.XShared/Utils/MalLinkParser.cs)

### Icon

Icon was donated by @richardbmx! Great thanks!

### Donations

This PayPal belongs to the author of the **original** MALClient project
([Drutol](https://github.com/Drutol)), not to the maintainer of this fork. If you'd like
to support the original author you won't be stopped:

[![paypal](https://www.paypalobjects.com/webstatic/mktg/merchant_portal/button/donate.en.png)](https://www.paypal.me/drutol)

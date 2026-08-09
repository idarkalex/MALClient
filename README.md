<h1 align="center">MALClient</h1>

<p align="center">
  <img src="images/app-logo.png" width="150px">
  <br><br>
  Robust MyAnimeList client application interfacing with the official MAL API, available on Android.
</p>

> **Important note (v1.0.0):** the app loads seasonal, top, studios, genres, details and other metadata exclusively from the stable **Tenrai API** (**`https://api.tenrai.org/v1`**). The official MAL API is still used for the anime/manga list, search and scores.

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
* Top anime/manga.
  * With multiple categories
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
You should be able to compile this thing out of the box, you may have to generate certificate for Android though.
There's also "Secrets.cs" file with some configs... you will have to make it yourself.
### Code
Spaghetti landfill.
Well... there's a metric ton of legacy thingies especially in navigation and pages that were made in the beggining like anime list or anime details. I'm not proud of these but I'm not planning to rewrite them. Stuff that has been added later on is nicer and somewhat decently organised. I started this app when I knew nothing so yeah, works but code is smelly.
### "Protocol"

If you'd like for some reason to launch my app externally you can do so by using this protocol:
```
malclient://<your everyday MAL link>
```
List of all accepted urls can be found [here](https://github.com/Drutol/MALClient/blob/714a73a3f4389a3212843fda243c1034c7347144/MALClient.XShared/Utils/MalLinkParser.cs)

### Icon

Icon was donated by @richardbmx! Great thanks!

### Donations

Well, if you really like my app I won't stop you:

[![paypal](https://www.paypalobjects.com/webstatic/mktg/merchant_portal/button/donate.en.png)](https://www.paypal.me/drutol)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MALClient.XShared.Utils
{
    public static class AnimeThemesHelper
    {
        private static readonly HttpClient Client = new HttpClient();

        static AnimeThemesHelper()
        {
            Client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        private static DateTime? _rateLimitedUntil;

        private static async Task<string> GetStringWithBackoffAsync(string url)
        {
            if (_rateLimitedUntil.HasValue && DateTime.UtcNow < _rateLimitedUntil.Value)
                return null;

            try
            {
                return await Client.GetStringAsync(url);
            }
            catch (HttpRequestException ex)
            {
                if (ex.Message.Contains("429") || ex.Message.Contains("Too Many Requests"))
                {
                    _rateLimitedUntil = DateTime.UtcNow.AddMinutes(1);
                    DiagnosticsReporter.Warn("AnimeThemes", $"rate limited (429), backoff 1min: {ex.Message}");
                    return null;
                }
                throw;
            }
        }

        public class ThemeVideo
        {
            public string Type { get; set; }
            public int Sequence { get; set; }
            public string Url { get; set; }
            public string AnimeSlug { get; set; }
            public string SongTitle { get; set; }
        }

        private static string NormalizeSong(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            var sb = new System.Text.StringBuilder();
            foreach (var c in s.ToLowerInvariant())
                if (char.IsLetterOrDigit(c))
                    sb.Append(c);
            return sb.ToString();
        }

        /// <summary>
        /// Fuzzy score (0..1) of how much of the query words appear in the theme title.
        /// Extra words in the title never penalize, so a high but not perfect coincidence
        /// still matches (user requirement: no 100% needed).
        /// </summary>
        private static double SongMatchScore(string query, string songTitle)
        {
            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(songTitle))
                return 0.0;
            var title = NormalizeSong(songTitle);
            if (title.Length == 0)
                return 0.0;
            var tokens = Regex.Matches(query, @"[A-Za-z0-9]{2,}")
                .Cast<Match>()
                .Select(m => m.Value.ToLowerInvariant())
                .Distinct()
                .ToList();
            if (tokens.Count == 0)
                return 0.0;
            if (tokens.All(t => title.Contains(t)))
                return 1.0;
            return tokens.Count(t => title.Contains(t)) / (double)tokens.Count;
        }

        private static string Slugify(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            var sb = new System.Text.StringBuilder();
            foreach (var c in s.ToLowerInvariant())
                if (char.IsLetterOrDigit(c))
                    sb.Append(c);
            return sb.ToString();
        }

        private static string CacheFileName(string key)
        {
            return Regex.Replace(key ?? "", @"[^a-z0-9]", "_");
        }

        /// <summary>
        /// Rejects fuzzy false positives: "Enen no Shouboutai..." must never match
        /// "ShingekiNoKyojinS3Part2" just because the search API returned it.
        /// </summary>
        private static bool SlugMatches(string videoSlug, string querySlug)
        {
            if (string.IsNullOrEmpty(videoSlug) || string.IsNullOrEmpty(querySlug))
                return true;
            if (videoSlug.Contains(querySlug) || querySlug.Contains(videoSlug))
                return true;
            var prefix = 0;
            var max = Math.Min(videoSlug.Length, querySlug.Length);
            while (prefix < max && videoSlug[prefix] == querySlug[prefix])
                prefix++;
            return prefix >= 6;
        }

        private static readonly Dictionary<string, List<ThemeVideo>> Cache =
            new Dictionary<string, List<ThemeVideo>>();

        private static readonly object DebugFileLock = new object();
        private static string _debugFilePath;

        private static void AppendThemeDebug(string line)
        {
            try
            {
                lock (DebugFileLock)
                {
                    if (_debugFilePath == null)
                    {
                        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        var dir = System.IO.Path.Combine(baseDir, "ThemeDebug");
                        System.IO.Directory.CreateDirectory(dir);
                        _debugFilePath = System.IO.Path.Combine(dir, "themes_debug.txt");
                    }
                    System.IO.File.AppendAllText(_debugFilePath,
                        $"[{DateTime.Now:HH:mm:ss.fff}] {line}{Environment.NewLine}");
                }
            }
            catch (Exception)
            {
            }
        }

        public static async Task<List<ThemeVideo>> SearchAsync(string animeTitle, string englishTitle = null)
        {
            if (string.IsNullOrEmpty(animeTitle) && string.IsNullOrEmpty(englishTitle))
                return new List<ThemeVideo>();

            // Build search variants: English title FIRST (matches AnimeThemes DB), then romaji
            var variants = new List<string>();
            if (!string.IsNullOrEmpty(englishTitle))
            {
                variants.Add(englishTitle.Trim());
                var colonIdx = englishTitle.IndexOf(':');
                if (colonIdx > 0)
                    variants.Add(englishTitle.Substring(0, colonIdx).Trim());
            }
            if (!string.IsNullOrEmpty(animeTitle))
            {
                variants.Add(animeTitle.Trim());
                var colonIdx = animeTitle.IndexOf(':');
                if (colonIdx > 0)
                    variants.Add(animeTitle.Substring(0, colonIdx).Trim());
                var partMatch = Regex.Match(animeTitle, @"^(.+?)(?:\s+(?:Part|Season|S\d|2nd|3rd|4th).*)?$", RegexOptions.IgnoreCase);
                if (partMatch.Success && partMatch.Groups[1].Value.Trim() != animeTitle.Trim())
                    variants.Add(partMatch.Groups[1].Value.Trim());
            }

            // PHASE 1 â€” structured /anime resolution for EVERY variant. The legacy search
            // is season-ambiguous (e.g. "KonoSuba" returns Season 1 videos), so a structured
            // hit from a later variant (romaji "Kono Subarashii Sekai ni Shukufuku wo! 3")
            // must never be masked by legacy garbage cached from an earlier one. Only cache
            // structured results.
            foreach (var variant in variants)
            {
                var cacheKey = variant.ToLowerInvariant();
                AppendThemeDebug($"PHASE1 variant='{variant}'");
                if (Cache.TryGetValue(cacheKey, out var cached) && cached.Count > 0)
                    return cached;

                // Persisted cache: the AnimeThemes server intermittently serves EMPTY theme
                // relations (Konosuba S3 is a real case) for minutes at a time â€” an in-memory
                // cache cannot survive that. Once a structured resolve succeeds, store it so
                // future sessions/taps stay immune to the flap.
                try
                {
                    var diskList = await DataCache.RetrieveData<List<ThemeVideo>>(
                        "animethemes_" + CacheFileName(cacheKey), "AnimeThemes", 7);
                    AppendThemeDebug($"  disk cache key='animethemes_{CacheFileName(cacheKey)}' hit={diskList?.Count ?? 0}");
                    if (diskList != null && diskList.Count > 0)
                    {
                        foreach (var v in variants)
                            Cache[v.ToLowerInvariant()] = diskList;
                        return diskList;
                    }
                }
                catch (Exception)
                {
                }

                try
                {
                    var videos = await SearchViaAnimeApi(variant,
                        string.Equals(variant, animeTitle?.Trim(), StringComparison.OrdinalIgnoreCase));
                    DiagnosticsReporter.Info("AnimeThemes", $"SearchViaAnimeApi '{variant}': {videos?.Count ?? 0} videos");
                    AppendThemeDebug($"  SearchViaAnimeApi='{variant}' result={videos?.Count ?? 0}");
                    if (videos.Count > 0)
                    {
                        foreach (var v in variants)
                        {
                            var vKey = v.ToLowerInvariant();
                            Cache[vKey] = videos;
                            try
                            {
                                await DataCache.SaveData(videos, "animethemes_" + CacheFileName(vKey), "AnimeThemes");
                            }
                            catch (Exception)
                            {
                            }
                        }
                        return videos;
                    }
                }
                catch (Exception ex)
                {
                    if (ex is OperationCanceledException)
                    {
                        DiagnosticsReporter.Warn("AnimeThemes", $"SearchViaAnimeApi cancelled for '{variant}'");
                    }
                    else
                        DiagnosticsReporter.Error("AnimeThemes", $"SearchViaAnimeApi failed for '{variant}'", ex);
                }
            }

            // PHASE 2 â€” legacy fuzzy endpoint, only when no variant resolved a structured anime
            foreach (var variant in variants)
            {
                var cacheKey = variant.ToLowerInvariant();
                AppendThemeDebug($"PHASE2 variant='{variant}'");
                if (Cache.TryGetValue(cacheKey, out var cached) && cached.Count > 0)
                    return cached;
                try
                {
                    var videos = await SearchViaLegacySearch(variant);
                    DiagnosticsReporter.Info("AnimeThemes", $"SearchViaLegacySearch '{variant}': {videos?.Count ?? 0} videos");
                    AppendThemeDebug($"  SearchViaLegacySearch='{variant}' result={videos?.Count ?? 0}");
                    if (videos.Count > 0)
                    {
                        // NEVER cache legacy results: they are season-ambiguous (e.g. "KonoSuba"
                        // returns Season 1 videos) and a transient API hiccup would poison every
                        // subsequent tap within the session via the phase-1 cache lookup.
                        return videos;
                    }
                }
                catch (Exception ex)
                {
                    if (ex is OperationCanceledException)
                    {
                        DiagnosticsReporter.Warn("AnimeThemes", $"SearchViaLegacySearch cancelled for '{variant}'");
                        return new List<ThemeVideo>();
                    }
                    DiagnosticsReporter.Error("AnimeThemes", $"SearchViaLegacySearch failed for '{variant}'", ex);
                }
            }

            return new List<ThemeVideo>();
        }

        private static async Task<List<ThemeVideo>> SearchViaLegacySearch(string variant)
        {
            var query = Uri.EscapeDataString(variant);
            var url = $"https://api.animethemes.moe/search?q={query}";
            AppendThemeDebug($"GET {url}");
            var json = await GetStringWithBackoffAsync(url);
            AppendThemeDebug($"  resp len={json?.Length ?? 0} head={(json?.Length > 200 ? json.Substring(0, 200) : json)}");
            var response = JsonConvert.DeserializeObject<SearchResponse>(json);

            var videos = new List<ThemeVideo>();
            if (response?.search?.videos == null)
                return videos;
            var querySlug = Slugify(variant);
            foreach (var video in response.search.videos)
            {
                var match = Regex.Match(video.basename ?? "",
                    @"-((OP|ED)(\d*))\.webm$", RegexOptions.IgnoreCase);
                if (!match.Success)
                    continue;
                var animeSlug = video.basename.Substring(0, match.Index);
                if (!SlugMatches(Slugify(animeSlug), querySlug))
                    continue;
                var type = match.Groups[2].Value.ToUpper();
                var seqStr = match.Groups[3].Value;
                var seq = string.IsNullOrEmpty(seqStr) ? 1 : int.Parse(seqStr);
                videos.Add(new ThemeVideo
                {
                    Type = type,
                    Sequence = seq,
                    Url = video.link,
                    AnimeSlug = animeSlug
                });
            }
            return videos;
        }

        /// <summary>
        /// Structured /anime API: resolves the exact series (sequels included) and
        /// returns every theme with its real sequence number. The /anime endpoint returns
        /// a FLAT document under the top-level "anime" key (anime â†’ animethemes â†’
        /// song / animethemeentries â†’ videos), NOT a JSON:API sideloaded "included" array.
        /// </summary>
        private static async Task<List<ThemeVideo>> SearchViaAnimeApi(string variant, bool guessKeyedSlug = false)
        {
            // The CDN/query cache for the /anime LIST route is poisoned for stable periods
            // (Konosuba S3: filter[name]= returns empty/anime=[] or even cross-aliased bodies
            // from /search). The KEYED route /anime/{slug} is a DIFFERENT path with its own
            // cache key and stays healthy. Kageetai slugs are just the lowercased title with
            // spacesâ†’underscores, so we can GUESS the slug and hit the healthy route first
            // for the romaji title.
            if (guessKeyedSlug)
            {
                var slugVideos = await TryKeyedSlugGuess(variant);
                if (slugVideos.Count > 0)
                    return slugVideos;
            }

            // The AnimeThemes CDN edge caches EMPTY theme relations (or empty results) per
            // IP/POP for a long time â€” KonoSuba S3 is a textbook case: healthy from a PC on
            // another edge, permanently EMPTY from the phone's edge. A random query param
            // busts the cache key and forces a fresh origin fetch.
            var videos = await FetchByVariant(variant, null);
            if (videos.Count > 0)
                return videos;

            DiagnosticsReporter.Info("AnimeThemes", $"cache-busting pass for '{variant}'");
            videos = await FetchByVariant(variant, Guid.NewGuid().ToString("N"));
            if (videos.Count > 0)
                return videos;

            DiagnosticsReporter.Info("AnimeThemes", $"cache-busting pass 2 for '{variant}'");
            return await FetchByVariant(variant, Guid.NewGuid().ToString("N"));
        }

        private static async Task<List<ThemeVideo>> TryKeyedSlugGuess(string variant)
        {
            var guessSlug = GuessSlug(variant);
            if (string.IsNullOrEmpty(guessSlug))
                return new List<ThemeVideo>();
            var keyedUrl =
                $"https://api.animethemes.moe/anime/{guessSlug}?include=animethemes.animethemeentries.videos,animethemes.song";
            try
            {
                AppendThemeDebug($"GET {keyedUrl}");
                var keyedJson = await GetStringWithBackoffAsync(keyedUrl);
                AppendThemeDebug($"  resp len={keyedJson?.Length ?? 0} head={(keyedJson?.Length > 160 ? keyedJson.Substring(0, 160) : keyedJson)}");
                var keyed = JsonConvert.DeserializeObject<AnimeKeyedApiResponse>(keyedJson);
                var videos = BuildVideos(keyed?.anime);
                AppendThemeDebug($"  keyed guess slug='{guessSlug}' result={videos.Count} themes={keyed?.anime?.animethemes?.Count}");
                if (videos.Count > 0)
                    DiagnosticsReporter.Info("AnimeThemes", $"keyed slug guess '{guessSlug}': {videos.Count} videos");
                return videos;
            }
            catch (Exception ex)
            {
                AppendThemeDebug($"  keyed guess slug='{guessSlug}' EXCEPTION {ex.Message}");
                DiagnosticsReporter.Error("AnimeThemes", $"keyed slug guess '{guessSlug}' failed", ex);
                return new List<ThemeVideo>();
            }
        }

        private static string GuessSlug(string title)
        {
            if (string.IsNullOrEmpty(title)) return "";
            var cleaned = new List<string>();
            foreach (var word in title.ToLowerInvariant().Split(' '))
            {
                var sb = new System.Text.StringBuilder();
                foreach (var c in word)
                    if (char.IsLetterOrDigit(c))
                        sb.Append(c);
                if (sb.Length > 0)
                    cleaned.Add(sb.ToString());
            }
            return string.Join("_", cleaned);
        }

        private static async Task<List<ThemeVideo>> FetchByVariant(string variant, string cacheBuster)
        {
            var query = Uri.EscapeDataString(variant);
            var buster = string.IsNullOrEmpty(cacheBuster) ? "" : "&cb=" + cacheBuster;
            var url = $"https://api.animethemes.moe/anime?filter[name]={query}&include=animethemes.animethemeentries.videos,animethemes.song&page[size]=8{buster}";
            AppendThemeDebug($"GET {url}");
            var json = await GetStringWithBackoffAsync(url);
            AppendThemeDebug($"  resp len={json?.Length ?? 0} head={(json?.Length > 160 ? json.Substring(0, 160) : json)}");
            var response = JsonConvert.DeserializeObject<AnimeApiResponse>(json);
            AppendThemeDebug($"  parse anime={response?.anime?.Count ?? 0} exactThemes={(response?.anime?.FirstOrDefault()?.animethemes?.Count) ?? 0}");
            if (response?.anime == null || response.anime.Count == 0)
                return new List<ThemeVideo>();

            var exact = response.anime.FirstOrDefault(a =>
                            string.Equals(a.name?.Trim(), variant.Trim(), StringComparison.OrdinalIgnoreCase))
                     ?? response.anime.FirstOrDefault(a =>
                            a.name?.IndexOf(variant.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                     ?? response.anime.FirstOrDefault();
            AppendThemeDebug($"  anime count={response.anime.Count} exact='{exact?.name}' slug='{exact?.slug}' themes={exact?.animethemes?.Count}");

            var videos = BuildVideos(exact);
            if (videos.Count > 0 || string.IsNullOrEmpty(exact?.slug))
                return videos;

            // The /anime LIST endpoint (filter[name]=) may ship an empty animetheme relation for
            // valid records (Konosuba S3) while the themes exist. The KEYED route /anime/{slug}
            // is more reliable â€” rescue with it, retrying with a fresh cache-buster each attempt.
            var keyedBase =
                $"https://api.animethemes.moe/anime/{Uri.EscapeDataString(exact.slug)}?include=animethemes.animethemeentries.videos,animethemes.song";
            for (var attempt = 0; attempt < 2 && videos.Count == 0; attempt++)
            {
                if (attempt > 0)
                    await Task.Delay(500);
                try
                {
                    var keyedUrl = keyedBase + "&cb=" + Guid.NewGuid().ToString("N");
                    AppendThemeDebug($"GET {keyedUrl}");
                    var keyedJson = await GetStringWithBackoffAsync(keyedUrl);
                    AppendThemeDebug($"  resp len={keyedJson?.Length ?? 0} head={(keyedJson?.Length > 160 ? keyedJson.Substring(0, 160) : keyedJson)}");
                    var keyed = JsonConvert.DeserializeObject<AnimeKeyedApiResponse>(keyedJson);
                    videos = BuildVideos(keyed?.anime);
                    AppendThemeDebug($"  keyed attempt={attempt + 1} themes={keyed?.anime?.animethemes?.Count} result={videos.Count}");
                    DiagnosticsReporter.Info("AnimeThemes", $"slug rescue '{exact.slug}' attempt {attempt + 1}: {videos.Count} videos");
                }
                catch (Exception ex)
                {
                    AppendThemeDebug($"  keyed attempt={attempt + 1} EXCEPTION {ex.Message}");
                    DiagnosticsReporter.Error("AnimeThemes", $"slug rescue failed for '{variant}' ({exact.slug}) attempt {attempt + 1}", ex);
                }
            }
            return videos;
        }

        private static List<ThemeVideo> BuildVideos(InlineAnime anime)
        {
            var videos = new List<ThemeVideo>();
            if (anime?.animethemes == null)
                return videos;
            foreach (var theme in anime.animethemes)
            {
                foreach (var entry in theme.animethemeentries ?? new List<InlineThemeEntry>())
                {
                    foreach (var video in entry.videos ?? new List<InlineVideo>())
                    {
                        if (string.IsNullOrEmpty(video.link))
                            continue;
                        if (videos.Any(v => v.Url == video.link))
                            continue;
                        videos.Add(new ThemeVideo
                        {
                            Type = theme.type,
                            Sequence = theme.sequence ?? 1,
                            Url = video.link,
                            AnimeSlug = Slugify(anime.name),
                            SongTitle = theme.song?.title
                        });
                    }
                }
            }
            return videos;
        }

        private class AnimeApiResponse
        {
            [JsonProperty("anime")] public List<InlineAnime> anime { get; set; }
        }

        private class AnimeKeyedApiResponse
        {
            [JsonProperty("anime")] public InlineAnime anime { get; set; }
        }

        private class InlineAnime
        {
            [JsonProperty("slug")] public string slug { get; set; }
            [JsonProperty("name")] public string name { get; set; }
            [JsonProperty("animethemes")] public List<InlineTheme> animethemes { get; set; }
        }

        private class InlineTheme
        {
            [JsonProperty("type")] public string type { get; set; }
            [JsonProperty("sequence")] public int? sequence { get; set; }
            [JsonProperty("song")] public InlineSong song { get; set; }
            [JsonProperty("animethemeentries")] public List<InlineThemeEntry> animethemeentries { get; set; }
        }

        private class InlineSong
        {
            [JsonProperty("title")] public string title { get; set; }
        }

        private class InlineThemeEntry
        {
            [JsonProperty("videos")] public List<InlineVideo> videos { get; set; }
        }

        private class InlineVideo
        {
            [JsonProperty("link")] public string link { get; set; }
        }

        /// <summary>
        /// Picks the theme whose song best matches artist+song (high-percentage fuzzy).
        /// Deliberately ignores OP/ED type and sequence (user rule: "no te ofusques en ED/OP").
        /// Returns null when nothing clears the threshold so the caller falls back to YouTube.
        /// </summary>
        public static ThemeVideo FindMatch(List<ThemeVideo> videos, bool isOp, int sequence, string songQuery = null, string songName = null)
        {
            if (videos == null || videos.Count == 0)
                return null;

            ThemeVideo best = null;
            var bestScore = 0.0;
            foreach (var v in videos)
            {
                // Prefer the bare song name (artist tokens the title lacks would unfairly
                // drag the score down); fall back to the full artist+song otherwise.
                var score = string.IsNullOrEmpty(songName)
                    ? SongMatchScore(songQuery, v.SongTitle)
                    : Math.Max(SongMatchScore(songName, v.SongTitle), SongMatchScore(songQuery, v.SongTitle));
                if (score > bestScore)
                {
                    bestScore = score;
                    best = v;
                }
            }
            return bestScore >= 0.7 ? best : null;
        }

        public static int ParseSequence(string opEdText)
        {
            if (string.IsNullOrEmpty(opEdText)) return 1;
            var match = Regex.Match(opEdText, @"^(\d+):");
            return match.Success ? int.Parse(match.Groups[1].Value) : 1;
        }

        private class SearchResponse
        {
            [JsonProperty("search")]
            public SearchData search { get; set; }
        }

        private class SearchData
        {
            [JsonProperty("videos")]
            public List<VideoItem> videos { get; set; }
        }

        private class VideoItem
        {
            [JsonProperty("link")]
            public string link { get; set; }
            [JsonProperty("basename")]
            public string basename { get; set; }
        }
    }
}

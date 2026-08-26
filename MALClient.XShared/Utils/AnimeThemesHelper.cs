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

        private static bool SongTitlesMatch(string normalizedQuery, string songTitle)
        {
            if (string.IsNullOrEmpty(normalizedQuery) || string.IsNullOrEmpty(songTitle))
                return false;
            var title = NormalizeSong(songTitle);
            if (title.Length == 0 || normalizedQuery.Length == 0)
                return false;
            // Exact match
            if (title == normalizedQuery)
                return true;
            // One is a prefix of the other, require significant overlap (>= 60% of shorter)
            var shorter = title.Length < normalizedQuery.Length ? title : normalizedQuery;
            var longer = title.Length < normalizedQuery.Length ? normalizedQuery : title;
            if (longer.StartsWith(shorter) && shorter.Length >= Math.Max(4, (int)(longer.Length * 0.6)))
                return true;
            // Prefix shared chars >= 8
            var max = Math.Min(title.Length, normalizedQuery.Length);
            var prefix = 0;
            while (prefix < max && title[prefix] == normalizedQuery[prefix])
                prefix++;
            return prefix >= 8;
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

            foreach (var variant in variants)
            {
                var cacheKey = variant.ToLowerInvariant();
                if (Cache.TryGetValue(cacheKey, out var cached) && cached.Count > 0)
                    return cached;

                try
                {
                    // PRIMARY: structured API with exact anime-name match (resolves
                    // sequels like "Gintama°" vs "Gintama" and full OP/ED sequences)
                    List<ThemeVideo> videos = null;
                    try
                    {
                        videos = await SearchViaAnimeApi(variant);
                    }
                    catch (Exception)
                    {
                    }

                    // SECONDARY: legacy fuzzy search endpoint
                    if (videos == null || videos.Count == 0)
                        videos = await SearchViaLegacySearch(variant);

                    if (videos.Count > 0)
                    {
                        foreach (var v in variants)
                            Cache[v.ToLowerInvariant()] = videos;
                        return videos;
                    }
                }
                catch (Exception)
                {
                }
            }

            return new List<ThemeVideo>();
        }

        private static async Task<List<ThemeVideo>> SearchViaLegacySearch(string variant)
        {
            var query = Uri.EscapeDataString(variant);
            var json = await Client.GetStringAsync(
                $"https://api.animethemes.moe/search?q={query}");
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
        /// returns every theme with its real sequence number.
        /// </summary>
        private static async Task<List<ThemeVideo>> SearchViaAnimeApi(string variant)
        {
            var query = Uri.EscapeDataString(variant);
            var json = await Client.GetStringAsync(
                $"https://api.animethemes.moe/anime?filter[name]={query}&include=animethemes.animethemeentries.videos,animethemes.song&page[size]=8");
            var response = JsonConvert.DeserializeObject<AnimeApiResponse>(json);
            if (response?.data == null || response.data.Count == 0)
                return new List<ThemeVideo>();

            var exact = response.data.FirstOrDefault(a =>
                            string.Equals(a.attributes?.name?.Trim(), variant.Trim(), StringComparison.OrdinalIgnoreCase))
                     ?? response.data.FirstOrDefault(a =>
                            a.attributes?.name?.IndexOf(variant.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                     ?? response.data.FirstOrDefault();
            if (exact?.relationships?.animethemes?.data == null)
                return new List<ThemeVideo>();

            var themeIds = exact.relationships.animethemes.data.Select(d => d.id).ToList();
            var included = response.included ?? new List<IncludedItem>();
            var videos = new List<ThemeVideo>();
            foreach (var theme in included.Where(i => i.type == "animetheme" && themeIds.Contains(i.id)))
            {
                var slugMatch = Regex.Match(theme.attributes?.slug ?? "", @"(OP|ED)\s*(\d*)", RegexOptions.IgnoreCase);
                if (!slugMatch.Success)
                    continue;
                var type = slugMatch.Groups[1].Value.ToUpper();
                var seq = string.IsNullOrEmpty(slugMatch.Groups[2].Value) ? 1 : int.Parse(slugMatch.Groups[2].Value);
                var songTitle = theme.relationships?.song?.data == null
                    ? null
                    : included.FirstOrDefault(i => i.type == "song" && i.id == theme.relationships.song.data.id)
                        ?.attributes?.title;
                var entryIds = theme.relationships?.animethemeentries?.data?.Select(d => d.id).ToList() ?? new List<string>();
                foreach (var entry in included.Where(i => i.type == "animethemeentry" && entryIds.Contains(i.id)))
                {
                    var videoIds = entry.relationships?.videos?.data?.Select(d => d.id).ToList() ?? new List<string>();
                    foreach (var video in included.Where(i => i.type == "video" && videoIds.Contains(i.id)))
                    {
                        if (string.IsNullOrEmpty(video.attributes?.link))
                            continue;
                        if (videos.Any(v => v.Url == video.attributes.link))
                            continue;
                        videos.Add(new ThemeVideo
                        {
                            Type = type,
                            Sequence = seq,
                            Url = video.attributes.link,
                            AnimeSlug = Slugify(exact.attributes?.name),
                            SongTitle = songTitle
                        });
                    }
                }
            }
            return videos;
        }

        private class AnimeApiResponse
        {
            [JsonProperty("data")] public List<AnimeData> data { get; set; }
            [JsonProperty("included")] public List<IncludedItem> included { get; set; }
        }

        private class AnimeData
        {
            [JsonProperty("attributes")] public NameAttributes attributes { get; set; }
            [JsonProperty("relationships")] public Relations relationships { get; set; }
        }

        private class NameAttributes
        {
            [JsonProperty("name")] public string name { get; set; }
        }

        private class Relations
        {
            [JsonProperty("animethemes")] public IdList animethemes { get; set; }
            [JsonProperty("animethemeentries")] public IdList animethemeentries { get; set; }
            [JsonProperty("videos")] public IdList videos { get; set; }
            [JsonProperty("song")] public SongRef song { get; set; }
        }

        private class SongRef
        {
            [JsonProperty("data")] public IdRef data { get; set; }
        }

        private class IdList
        {
            [JsonProperty("data")] public List<IdRef> data { get; set; }
        }

        private class IdRef
        {
            [JsonProperty("id")] public string id { get; set; }
        }

        private class IncludedItem
        {
            [JsonProperty("id")] public string id { get; set; }
            [JsonProperty("type")] public string type { get; set; }
            [JsonProperty("attributes")] public ThemeAttributes attributes { get; set; }
            [JsonProperty("relationships")] public Relations relationships { get; set; }
        }

        private class ThemeAttributes
        {
            [JsonProperty("slug")] public string slug { get; set; }
            [JsonProperty("link")] public string link { get; set; }
            [JsonProperty("basename")] public string basename { get; set; }
            [JsonProperty("title")] public string title { get; set; }
        }

        public static ThemeVideo FindMatch(List<ThemeVideo> videos, bool isOp, int sequence, string songQuery = null)
        {
            var type = isOp ? "OP" : "ED";
            var exact = videos.FirstOrDefault(v => v.Type == type && v.Sequence == sequence);

            // If exact sequence found and song matches (or no query), use it
            if (exact != null && (string.IsNullOrEmpty(songQuery) ||
                                  SongTitlesMatch(NormalizeSong(songQuery), exact.SongTitle)))
                return exact;

            // Search by song title — prefer same type, then cross-type
            if (!string.IsNullOrEmpty(songQuery))
            {
                var q = NormalizeSong(songQuery);
                var bySongSameType = videos.FirstOrDefault(v => v.Type == type && SongTitlesMatch(q, v.SongTitle));
                if (bySongSameType != null)
                    return bySongSameType;
                var bySongAny = videos.FirstOrDefault(v => SongTitlesMatch(q, v.SongTitle));
                if (bySongAny != null)
                    return bySongAny;
            }

            // No good match — return null (don't play a random video)
            return null;
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

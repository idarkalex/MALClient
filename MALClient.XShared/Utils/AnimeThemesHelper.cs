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
                    var query = Uri.EscapeDataString(variant);
                    var json = await Client.GetStringAsync(
                        $"https://api.animethemes.moe/search?q={query}");
                    var response = JsonConvert.DeserializeObject<SearchResponse>(json);

                    var videos = new List<ThemeVideo>();
                    if (response?.search?.videos != null)
                    {
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
                    }

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

        public static ThemeVideo FindMatch(List<ThemeVideo> videos, bool isOp, int sequence)
        {
            var type = isOp ? "OP" : "ED";
            return videos.FirstOrDefault(v => v.Type == type && v.Sequence == sequence)
                   ?? videos.FirstOrDefault(v => v.Type == type && v.Sequence == 1)
                   ?? videos.FirstOrDefault(v => v.Type == type);
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

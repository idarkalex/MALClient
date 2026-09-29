using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MALClient.XShared.Comm.Discovery
{
    public class DiscoveryItem
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string ImgUrl { get; set; }
        public string Type { get; set; }
        public string Subtitle { get; set; }
        public bool IsManga { get; set; }
        public int PopularityRank { get; set; }
        /// <summary>Direct media url, only set for the OP/ED discovery entries.</summary>
        public string ThemeUrl { get; set; }
    }

    public class RandomFilter
    {
        public List<int> GenreIds { get; set; } = new List<int>();
        public string Format { get; set; } = "";
        public int MinYear { get; set; }
        public int MaxYear { get; set; }
        public double MinScore { get; set; } = 0;
    }

    /// <summary>
    /// Trending for the Discover hub. Tenrai has no trending at all (/trending 404,
    /// order_by=trending 400) and MAL v2 has no popularity sort, so this reads AniList's
    /// TRENDING_DESC and keeps only the entries AniList can map to a MAL id, which is what
    /// lets the row open our own details page.
    /// </summary>
    public static class AniListTrendingQuery
    {
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        private const string ChromeUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

        /// <summary>Last failure, surfaced so the caller can log why the list came back empty.</summary>
        public static string LastError { get; private set; } = "";

        private const string Endpoint = "https://graphql.anilist.co";
        private const string Body =
            @"{""query"":""query{Page{media(type:ANIME,sort:TRENDING_DESC,isAdult:false){id idMal title{romaji}coverImage{large}popularity averageScore seasonYear}}}""}";

        public static async Task<List<DiscoveryItem>> GetAsync(int limit = 20)
        {
            var output = new List<DiscoveryItem>();
            LastError = "";
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
                {
                    Content = new StringContent(Body, Encoding.UTF8, "application/json")
                };
                // AniList rejects requests without a browser User-Agent outright.
                request.Headers.TryAddWithoutValidation("User-Agent", ChromeUserAgent);
                request.Headers.TryAddWithoutValidation("Accept", "application/json");
                using var response = await Client.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    LastError = "HTTP " + (int)response.StatusCode;
                    return output;
                }
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("data", out var data) ||
                    !data.TryGetProperty("Page", out var page) ||
                    !page.TryGetProperty("media", out var media) ||
                    media.ValueKind != JsonValueKind.Array)
                {
                    LastError = json.Length > 200 ? json.Substring(0, 200) : json;
                    return output;
                }

                foreach (var entry in media.EnumerateArray())
                {
                    if (output.Count >= limit)
                        break;
                    if (!entry.TryGetProperty("idMal", out var idMal) ||
                        idMal.ValueKind != JsonValueKind.Number)
                        continue; // AniList original with no MAL equivalent
                    var title = entry.TryGetProperty("title", out var t) && t.TryGetProperty("romaji", out var r)
                        ? r.GetString()
                        : null;
                    string cover = null;
                    if (entry.TryGetProperty("coverImage", out var coverImage) &&
                        coverImage.TryGetProperty("large", out var large) && large.ValueKind == JsonValueKind.String)
                        cover = large.GetString();
                    output.Add(new DiscoveryItem
                    {
                        Id = idMal.GetInt32(),
                        Title = title,
                        ImgUrl = cover,
                        Subtitle = FormatTrendMeta(entry)
                    });
                }
            }
            catch
            {
                LastError = "exception";
            }
            return output;
        }

        private static string FormatTrendMeta(JsonElement entry)
        {
            try
            {
                var score = entry.TryGetProperty("averageScore", out var s) && s.ValueKind == JsonValueKind.Number
                    ? (s.GetDouble() / 10.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                    : "";
                return string.IsNullOrEmpty(score) ? "Trending" : $"Trending · {score}";
            }
            catch
            {
                return "Trending";
            }
        }
    }

    /// <summary>
    /// "Random with filters" for the Discover hub. Tenrai's /random endpoints ignore every
    /// filter except sfw (verified: min_score=9 came back with a 7.48), so we run the
    /// filtered listing and jump to a random page inside it instead.
    /// </summary>
    public static class DiscoveryRandomQuery
    {
        private static readonly Random Rng = new Random();

        /// <summary>
        /// What the community is watching right now, without AniList: currently airing,
        /// most favourited first. Used as the OP/ED source and as the trending fallback.
        /// </summary>
        public static async Task<List<DiscoveryItem>> GetTopAiringAsync(int limit = 10)
        {
            var output = new List<DiscoveryItem>();
            try
            {
                var raw = await TenraiClient.GetRawJsonAsync(
                    "anime?status=airing&sfw&order_by=favorites&sort=desc&page=1&limit=" + limit);
                if (string.IsNullOrEmpty(raw))
                    return output;
                using var doc = JsonDocument.Parse(raw);
                if (!doc.RootElement.TryGetProperty("data", out var data) ||
                    data.ValueKind != JsonValueKind.Array)
                    return output;
                foreach (var entry in data.EnumerateArray())
                {
                    var item = ParseCatalogEntry(entry, false);
                    if (item != null)
                        output.Add(item);
                }
            }
            catch
            {
            }
            return output;
        }

        //Tenrai recomputes pagination from the page size: anime?sfw reports 27341 pages with
        //limit=1 but only ~1094 with limit=25, and asking for a page past the real last one is a
        //400 rather than an empty page. The page count therefore has to be resolved with the very
        //same limit the fetch uses, or the random page is out of range almost every time.
        private const int PageSize = 25;

        //last_visible_page is also not a usable bound on its own. manga?sfw advertises 2369 pages
        //of 25, yet page 1000 (offset 25,000) is served and page 2000 (offset 50,000) is a 400:
        //there is a hard offset cap that the pagination metadata does not mention. The random pick
        //is confined to an offset known to be served, which is still a 20k entry pool.
        private const int MaxOffset = 20000;

        public static async Task<List<DiscoveryItem>> GetAsync(bool manga, RandomFilter filter,
            int minScore, int maxPopularityRank, int limit = 20)
        {
            var output = new List<DiscoveryItem>();
            try
            {
                var media = manga ? "manga" : "anime";
                var effectiveMinScore = minScore > 0 ? minScore : filter?.MinScore ?? 0;
                var parts = new List<string> { "sfw" };
                if (filter != null)
                {
                    if (filter.GenreIds.Count > 0)
                        parts.Add("genres=" + string.Join(",", filter.GenreIds));
                    if (!string.IsNullOrEmpty(filter.Format))
                        parts.Add("type=" + filter.Format);
                }
                if (effectiveMinScore > 0)
                    parts.Add("min_score=" + effectiveMinScore.ToString("0.0",
                        System.Globalization.CultureInfo.InvariantCulture));
                var baseQuery = media + "?" + string.Join("&", parts);

                var lastPage = await ResolveLastPageAsync(media, baseQuery, PageSize);
                if (lastPage < 1)
                    return output;

                var maxPage = Math.Max(1, Math.Min(lastPage, MaxOffset / PageSize));
                var page = maxPage == 1 ? 1 : Rng.Next(1, maxPage + 1);
                var raw = await TenraiClient.GetRawJsonAsync($"{baseQuery}&page={page}&limit={PageSize}");
                if (string.IsNullOrEmpty(raw))
                    return output;

                using var doc = JsonDocument.Parse(raw);
                if (!doc.RootElement.TryGetProperty("data", out var data) ||
                    data.ValueKind != JsonValueKind.Array)
                    return output;

                foreach (var entry in data.EnumerateArray())
                {
                    if (output.Count >= limit)
                        break;
                    var item = ParseCatalogEntry(entry, manga);
                    if (item == null)
                        continue;
                    // Popularity ceilings cannot be filtered server side (Tenrai ignores
                    // min_members/max_members), so they are applied here.
                    if (maxPopularityRank > 0 && item.PopularityRank > 0 && item.PopularityRank < maxPopularityRank)
                        continue;
                    output.Add(item);
                }
            }
            catch
            {
            }
            return output;
        }

        private static async Task<int> ResolveLastPageAsync(string media, string baseQuery, int pageSize)
        {
            try
            {
                var raw = await TenraiClient.GetRawJsonAsync($"{baseQuery}&page=1&limit={pageSize}");
                if (string.IsNullOrEmpty(raw))
                    return 0;
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.TryGetProperty("pagination", out var pagination) &&
                    pagination.TryGetProperty("last_visible_page", out var last) &&
                    last.ValueKind == JsonValueKind.Number)
                    return last.GetInt32();
            }
            catch
            {
            }
            return 0;
        }

        internal static DiscoveryItem ParseCatalogEntry(JsonElement entry, bool manga)
        {
            try
            {
                if (!entry.TryGetProperty("mal_id", out var idProp) || idProp.ValueKind != JsonValueKind.Number)
                    return null;
                var id = idProp.GetInt32();
                if (id <= 0)
                    return null;
                string title = entry.TryGetProperty("title", out var t) ? t.GetString() : null;
                string type = entry.TryGetProperty("type", out var ty) ? ty.GetString() : null;
                string img = null;
                if (entry.TryGetProperty("images", out var images) &&
                    images.TryGetProperty("jpg", out var jpg) &&
                    jpg.TryGetProperty("image_url", out var imgProp) && imgProp.ValueKind == JsonValueKind.String)
                    img = imgProp.GetString();
                int popularity = 0;
                if (entry.TryGetProperty("popularity", out var pop) && pop.ValueKind == JsonValueKind.Number)
                    popularity = pop.GetInt32();
                double score = 0;
                if (entry.TryGetProperty("score", out var sc) && sc.ValueKind == JsonValueKind.Number)
                    score = sc.GetDouble();

                return new DiscoveryItem
                {
                    Id = id,
                    Title = title,
                    ImgUrl = img,
                    Type = type,
                    IsManga = manga,
                    PopularityRank = popularity,
                    Subtitle = BuildSubtitle(type, score, popularity)
                };
            }
            catch
            {
                return null;
            }
        }

        private static string BuildSubtitle(string type, double score, int popularity)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(type))
                parts.Add(type);
            if (score > 0)
                parts.Add("★ " + score.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            if (popularity > 0)
                parts.Add($"#{popularity}");
            return string.Join(" · ", parts);
        }
    }
}

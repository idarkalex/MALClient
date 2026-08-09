using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Android.Runtime;
using HtmlAgilityPack;
using MALClient.Models.Models.AnimeScrapped;
using MALClient.XShared.Utils;

namespace MALClient.XShared.Comm.Anime
{
    public class AnimeReviewsQuery : Query
    {
        private readonly bool _anime;
        private readonly int _targetId;

        public AnimeReviewsQuery(int id, bool anime = true)
        {
            Request =
                WebRequest.Create(
                    Uri.EscapeUriString($"https://myanimelist.net/{(anime ? "anime" : "manga")}/{id}/whatever/reviews"));
            Request.ContentType = "application/x-www-form-urlencoded";
            Request.Method = "GET";
            _targetId = id;
            _anime = anime;
        }

        private const int MaxMalPages = 10;

        public async Task<List<AnimeReviewData>> GetAnimeReviews(bool force = false)
        {
            var output = force
                ? new List<AnimeReviewData>()
                : await DataCache.RetrieveReviewsData(_targetId, _anime) ?? new List<AnimeReviewData>();
            if (output.Count != 0) return output;

            try
            {
                output = await FetchReviewsFromMalAsync();
                if (output.Count == 0)
                    output = await FetchReviewsFromTenraiAsync();

                if (output.Count != 0)
                    DataCache.SaveAnimeReviews(_targetId, output, _anime);
            }
            catch (Exception)
            {
            }

            return output;
        }

        private async Task<List<AnimeReviewData>> FetchReviewsFromMalAsync()
        {
            var output = new List<AnimeReviewData>();
            var url = $"https://myanimelist.net/{(_anime ? "anime" : "manga")}/{_targetId}/whatever/reviews";

            for (var page = 0; page < MaxMalPages; page++)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("User-Agent", "MALClient/3.0");

                string html;
                using (var response = await _client.SendAsync(request))
                {
                    if (!response.IsSuccessStatusCode)
                        break;
                    html = await response.Content.ReadAsStringAsync();
                }

                var parsed = ParseMalReviewsPage(html);
                if (parsed.Reviews.Count == 0)
                    break;

                output.AddRange(parsed.Reviews);
                if (string.IsNullOrEmpty(parsed.MoreReviewsUrl))
                    break;

                url = new Uri(new Uri("https://myanimelist.net/"), parsed.MoreReviewsUrl).AbsoluteUri;
            }

            return output;
        }

        private (List<AnimeReviewData> Reviews, string MoreReviewsUrl) ParseMalReviewsPage(string html)
        {
            var reviews = new List<AnimeReviewData>();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var reviewNodes = doc.DocumentNode.SelectNodes(
                "//div[contains(@class, 'review-element') and contains(@class, 'js-review-element')]");

            if (reviewNodes != null)
            {
                foreach (var node in reviewNodes)
                {
                    try
                    {
                        var authorNode = node.SelectSingleNode(".//div[contains(@class, 'username')]//a");
                        var avatarNode = node.SelectSingleNode(".//div[contains(@class, 'thumb')]//img");
                        var dateNode = node.SelectSingleNode(".//div[contains(@class, 'update_at')]");
                        var tagNode =
                            node.SelectSingleNode(
                                ".//div[contains(@class, 'tag ') and contains(@class, 'btn-label')]");
                        var textNode = node.SelectSingleNode(".//div[contains(@class, 'text')]");
                        var idNode = node.SelectSingleNode(".//div[contains(@class, 'icon-reaction')]");
                        var reactionsAttr = node.Attributes["data-reactions"]?.Value;

                        var tagText = tagNode?.InnerText?.Trim() ?? string.Empty;
                        ParseReactions(reactionsAttr, out var overall, out var nice, out var loveIt,
                            out var funny, out var confusing, out var informative, out var wellWritten);

                        reviews.Add(new AnimeReviewData
                        {
                            Id = idNode?.Attributes["data-id"]?.Value ?? string.Empty,
                            Author = authorNode?.InnerText?.Trim() ?? "N/A",
                            AuthorAvatar = avatarNode?.Attributes["src"]?.Value ??
                                           avatarNode?.Attributes["data-src"]?.Value ?? string.Empty,
                            Date = dateNode?.InnerText?.Trim() ?? "N/A",
                            OverallRating = string.IsNullOrEmpty(tagText) ? "N/A" : tagText,
                            EpisodesSeen = "N/A",
                            HelpfulCount = overall.ToString(),
                            HasSpoilers = false,
                            IsPreliminary =
                                tagText.IndexOf("Preliminary", StringComparison.OrdinalIgnoreCase) >= 0,
                            Review = ParseMalReviewText(textNode),
                            Score = new List<ReviewScore>
                            {
                                new ReviewScore {Field = "Informative", Score = informative.ToString()},
                                new ReviewScore {Field = "Confusing", Score = confusing.ToString()},
                                new ReviewScore {Field = "Creative", Score = "N/A"},
                                new ReviewScore {Field = "Funny", Score = funny.ToString()},
                                new ReviewScore {Field = "Love It", Score = loveIt.ToString()},
                                new ReviewScore {Field = "Well Written", Score = wellWritten.ToString()}
                            }
                        });
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            var moreReviewsNode =
                doc.DocumentNode.SelectSingleNode("//a[@data-ga-click-type='review-more-reviews']");
            var moreReviewsUrl = moreReviewsNode?.GetAttributeValue("href", null);
            return (reviews, moreReviewsUrl);
        }

        private static string ParseMalReviewText(HtmlNode textNode)
        {
            if (textNode == null)
                return string.Empty;

            textNode.SelectSingleNode(".//span[contains(@class, 'js-visible')]")?.Remove();
            var html = textNode.InnerHtml;
            html = Regex.Replace(html, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, "<[^>]+>", string.Empty);
            var text = WebUtility.HtmlDecode(html);
            return Regex.Replace(text, @"[ \t]*\n[ \t]*", "\n").Trim();
        }

        private static void ParseReactions(string json, out int overall, out int nice, out int loveIt,
            out int funny, out int confusing, out int informative, out int wellWritten)
        {
            overall = nice = loveIt = funny = confusing = informative = wellWritten = 0;
            if (string.IsNullOrEmpty(json))
                return;

            try
            {
                using (var doc = JsonDocument.Parse(json))
                {
                    if (!doc.RootElement.TryGetProperty("count", out var count) ||
                        count.ValueKind != JsonValueKind.Array)
                        return;

                    var values = new int[7];
                    var i = 0;
                    foreach (var item in count.EnumerateArray())
                    {
                        if (i >= 7)
                            break;
                        values[i++] = int.TryParse(item.GetString(), out var value) ? value : 0;
                    }

                    overall = values[0];
                    nice = values[1];
                    loveIt = values[2];
                    funny = values[3];
                    confusing = values[4];
                    informative = values[5];
                    wellWritten = values[6];
                }
            }
            catch (Exception)
            {
            }
        }

        private async Task<List<AnimeReviewData>> FetchReviewsFromTenraiAsync()
        {
            var output = new List<AnimeReviewData>();
            var data = new List<Datum>();
            var page = 1;
            while (page <= 3)
            {
                var json = await TenraiClient.GetRawJsonAsync(
                    $"{(_anime ? "anime" : "manga")}/{_targetId}/reviews?page={page}");
                var reviews = JsonSerializer.Deserialize<Root>(json);
                if (reviews?.Data == null || reviews.Data.Count == 0)
                    break;

                data.AddRange(reviews.Data);
                if (reviews.Pagination == null || !reviews.Pagination.HasNextPage)
                    break;

                page++;
            }

            foreach (var review in data.OrderByDescending(r => r.Reactions?.Overall ?? 0))
            {
                output.Add(new AnimeReviewData
                {
                    AuthorAvatar = review.User.Images.Jpg.ImageUrl ?? review.User.Images.Webp.ImageUrl,
                    Author = review.User.Username,
                    Date = review.Date?.ToString("d") ?? "N/A",
                    EpisodesSeen = review.EpisodesWatched?.ToString() ?? "N/A",
                    HelpfulCount = review.Reactions?.Overall?.ToString() ?? "N/A",
                    Id = review.MalId.ToString(),
                    OverallRating = review.Score?.ToString() ?? "N/A",
                    Review = review.Review,
                    HasSpoilers = review.IsSpoiler,
                    IsPreliminary = review.IsPreliminary,
                    Score = new List<ReviewScore>
                    {
                        new ReviewScore
                        {
                            Field = "Informative",
                            Score = review.Reactions?.Informative?.ToString() ?? "N/A"
                        },
                        new ReviewScore
                        {
                            Field = "Confusing",
                            Score = review.Reactions?.Confusing?.ToString() ?? "N/A"
                        },
                        new ReviewScore
                        {
                            Field = "Creative",
                            Score = review.Reactions?.Creative?.ToString() ?? "N/A"
                        },
                        new ReviewScore
                        {
                            Field = "Funny",
                            Score = review.Reactions?.Funny?.ToString() ?? "N/A"
                        },
                        new ReviewScore
                        {
                            Field = "Love It",
                            Score = review.Reactions?.LoveIt?.ToString() ?? "N/A"
                        },
                        new ReviewScore
                        {
                            Field = "Well Written",
                            Score = review.Reactions?.WellWritten?.ToString() ?? "N/A"
                        }
                    }
                });
            }

            return output;
        }
    }

    [Preserve(AllMembers = true)]
    public class Datum
    {
        [JsonPropertyName("mal_id")]
        public int MalId { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("reactions")]
        public Reactions Reactions { get; set; }

        [JsonPropertyName("date")]
        public DateTime? Date { get; set; }

        [JsonPropertyName("review")]
        public string Review { get; set; }

        [JsonPropertyName("score")]
        public int? Score { get; set; }

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; }

        [JsonPropertyName("is_spoiler")]
        public bool IsSpoiler { get; set; }

        [JsonPropertyName("is_preliminary")]
        public bool IsPreliminary { get; set; }

        [JsonPropertyName("episodes_watched")]
        public int? EpisodesWatched { get; set; }

        [JsonPropertyName("user")]
        public User User { get; set; }
    }
    [Preserve(AllMembers = true)]
    public class Images
    {
        [JsonPropertyName("jpg")]
        public Jpg Jpg { get; set; }

        [JsonPropertyName("webp")]
        public Webp Webp { get; set; }
    }
    [Preserve(AllMembers = true)]
    public class Jpg
    {
        [JsonPropertyName("image_url")]
        public string ImageUrl { get; set; }
    }
    [Preserve(AllMembers = true)]
    public class Pagination
    {
        [JsonPropertyName("last_visible_page")]
        public int LastVisiblePage { get; set; }

        [JsonPropertyName("has_next_page")]
        public bool HasNextPage { get; set; }
    }
    [Preserve(AllMembers = true)]
    public class Reactions
    {
        [JsonPropertyName("overall")]
        public int? Overall { get; set; }

        [JsonPropertyName("nice")]
        public int? Nice { get; set; }

        [JsonPropertyName("love_it")]
        public int? LoveIt { get; set; }

        [JsonPropertyName("funny")]
        public int? Funny { get; set; }

        [JsonPropertyName("confusing")]
        public int? Confusing { get; set; }

        [JsonPropertyName("informative")]
        public int? Informative { get; set; }

        [JsonPropertyName("well_written")]
        public int? WellWritten { get; set; }

        [JsonPropertyName("creative")]
        public int? Creative { get; set; }
    }
    [Preserve(AllMembers = true)]
    public class Root
    {
        [JsonPropertyName("pagination")]
        public Pagination Pagination { get; set; }

        [JsonPropertyName("data")]
        public List<Datum> Data { get; set; }
    }
    [Preserve(AllMembers = true)]
    public class User
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("images")]
        public Images Images { get; set; }
    }
    [Preserve(AllMembers = true)]
    public class Webp
    {
        [JsonPropertyName("image_url")]
        public string ImageUrl { get; set; }
    }
}
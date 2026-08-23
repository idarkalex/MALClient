using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MALClient.Models.Enums;
using MALClient.Models.Models.MalSpecific;
using MALClient.XShared.Utils;

namespace MALClient.XShared.Comm.Articles
{
    public class AnnNewsQuery : Query
    {
        private const string RssUrl = "https://www.animenewsnetwork.com/all/rss.xml?ann-edition=us";

        public AnnNewsQuery()
        {
            Request = new Uri(RssUrl);
        }

        private static readonly System.Net.Http.HttpClient AnnClient = new System.Net.Http.HttpClient();

        static AnnNewsQuery()
        {
            AnnClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        public async Task<List<MalNewsUnitModel>> GetAnnNewsIndex(bool force = false)
        {
            var cached = force
                ? null
                : await DataCache.RetrieveData<List<MalNewsUnitModel>>("ann_news_index.json", "Articles", 1);
            if (cached != null && cached.Count > 0)
                return cached;

            string raw = null;
            for (var attempt = 1; attempt <= 3 && raw == null; attempt++)
            {
                try
                {
                    raw = await AnnClient.GetStringAsync(RssUrl);
                    if (string.IsNullOrWhiteSpace(raw))
                        raw = null;
                }
                catch (Exception)
                {
                    if (attempt < 3)
                        await Task.Delay(TimeSpan.FromSeconds(attempt));
                }
            }
            if (string.IsNullOrEmpty(raw))
                return new List<MalNewsUnitModel>();

            var output = ParseRss(raw);
            if (output.Count > 0)
                DataCache.SaveData(output, "ann_news_index.json", "Articles");
            return output;
        }

        public static async Task<string> GetAnnArticleHtml(string url, string id)
        {
            var cached = await DataCache.RetrieveArticleContentData($"ann_{id}", MalNewsType.News);
            if (cached != null)
                return cached;

            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                    var html = await client.GetStringAsync(url);

                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);

                    var body = doc.DocumentNode.Descendants("div")
                        .FirstOrDefault(node => node.Attributes["class"]?.Value?.Contains("KonaBody") ?? false);
                    if (body == null)
                        return null;

                    foreach (var script in body.Descendants("script").ToList())
                        script.Remove();

                    DataCache.SaveArticleContentData($"ann_{id}", body.InnerHtml, MalNewsType.News);
                    return body.InnerHtml;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static List<MalNewsUnitModel> ParseRss(string rss)
        {
            var output = new List<MalNewsUnitModel>();
            var items = Regex.Matches(rss, "<item>(?s)(.*?)</item>");
            foreach (Match item in items)
            {
                try
                {
                    var block = item.Groups[1].Value;
                    var title = StripHtml(WebUtility.HtmlDecode(ExtractTag(block, "title"))).TrimStart('>').Trim();
                    var link = ExtractTag(block, "link");
                    var rawDescription = ExtractRawTag(block, "description");
                    var description = StripHtml(WebUtility.HtmlDecode(rawDescription)).TrimStart('>').Trim();
                    var pubDate = ExtractTag(block, "pubDate");
                    var category = StripHtml(WebUtility.HtmlDecode(ExtractTag(block, "category"))).TrimStart('>').Trim();
                    var guid = ExtractTag(block, "guid");

                    if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(link))
                        continue;

                    DateTime? published = null;
                    if (!string.IsNullOrEmpty(pubDate) &&
                        DateTimeOffset.TryParse(pubDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
                        published = dto.UtcDateTime;

                    var imgUrl = ExtractFirstImage(rawDescription) ?? "";

                    output.Add(new MalNewsUnitModel
                    {
                        Title = title,
                        Url = link,
                        Highlight = description,
                        Tags = string.IsNullOrEmpty(category) ? "News" : category,
                        Author = "ANN",
                        Views = "",
                        Type = MalNewsType.News,
                        Source = "ANN",
                        PublishedAt = published,
                        ImgUrl = imgUrl,
                        Id = ExtractAnnId(link) ?? ExtractAnnId(guid) ?? Guid.NewGuid().ToString("N"),
                    });
                }
                catch (Exception)
                {
                    //
                }
            }

            return output;
        }

        private static string ExtractTag(string block, string tag)
        {
            var match = Regex.Match(block, $"<{tag}(?s)(.*?)</{tag}>|<{tag}[^>]*>([^<]*)");
            if (!match.Success)
                return "";
            var value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            value = Regex.Replace(value, "^\\s*<!\\[CDATA\\[", "").Replace("\\]\\]>\\s*$", "").Trim();
            return value;
        }

        private static string ExtractRawTag(string block, string tag)
        {
            var match = Regex.Match(block, $"<{tag}(?s)(.*?)</{tag}>");
            if (!match.Success)
                return "";
            return match.Groups[1].Value.Trim();
        }

        private static string ExtractFirstImage(string html)
        {
            if (string.IsNullOrEmpty(html))
                return null;
            var match = Regex.Match(html, "<img[^>]+src=\"([^\"]+)\"");
            if (!match.Success)
                return null;
            var src = WebUtility.HtmlDecode(match.Groups[1].Value);
            if (src.StartsWith("//"))
                src = "https:" + src;
            return src;
        }

        private static string StripHtml(string input) =>
            string.IsNullOrEmpty(input) ? input : Regex.Replace(input, "<.*?>", "").Trim();

        private static string ExtractAnnId(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;
            var match = Regex.Match(url, @"\.(\d+)");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}

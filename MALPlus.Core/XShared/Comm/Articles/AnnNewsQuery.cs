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
                : await DataCache.RetrieveData<List<MalNewsUnitModel>>("ann_news_index_v6.json", "Articles", 1);
            if (cached != null && cached.Count > 0)
            {
                DiagnosticsReporter.Info("ANN", $"cache hit: {cached.Count} articles");
                return cached;
            }

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
            {
                DiagnosticsReporter.Warn("ANN", "RSS fetch returned null/empty after 3 attempts");
                return new List<MalNewsUnitModel>();
            }

            var output = ParseRss(raw);
            if (output.Count > 0)
            {
                var thumbs = await FetchThumbMap();
                var filled = 0;
                foreach (var entry in output)
                {
                    if (string.IsNullOrEmpty(entry.ImgUrl) && thumbs.TryGetValue(entry.Id, out var thumb))
                    {
                        entry.ImgUrl = thumb;
                        filled++;
                    }
                }
                DiagnosticsReporter.Info("ANN", $"thumb map filled {filled}/{output.Count - output.Count(o => !string.IsNullOrEmpty(o.ImgUrl))} entries from listing scrape");
                DataCache.SaveData(output, "ann_news_index_v6.json", "Articles");
            }
            return output;
        }

        public static async Task<string> GetAnnArticleHtml(string url, string id)
        {
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var requestUri) ||
                (requestUri.Scheme != Uri.UriSchemeHttp && requestUri.Scheme != Uri.UriSchemeHttps))
            {
                DiagnosticsReporter.Error("ANN", $"article url invalid: \"{url}\" (id={id})");
                return null;
            }

            var cached = await DataCache.RetrieveArticleContentData($"ann_v4_{id}", MalNewsType.News);
            if (cached != null)
                return cached;

            string html = null;
            for (var attempt = 1; attempt <= 3 && html == null; attempt++)
            {
                try
                {
                    using (var request = new System.Net.Http.HttpRequestMessage(
                        System.Net.Http.HttpMethod.Get, requestUri))
                    {
                        request.Headers.Add("User-Agent",
                            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                        using (var response = await AnnClient.SendAsync(request))
                        {
                            DiagnosticsReporter.Info("ANN", $"article fetch attempt {attempt}: {url} -> {response.StatusCode}");
                            if (response.IsSuccessStatusCode)
                                html = await response.Content.ReadAsStringAsync();
                        }
                    }
                    if (html == null && attempt < 3)
                        await Task.Delay(TimeSpan.FromSeconds(attempt));
                }
                catch (Exception ex)
                {
                    DiagnosticsReporter.Error("ANN", $"article fetch attempt {attempt} failed for \"{url}\"", ex);
                    if (attempt < 3)
                        await Task.Delay(TimeSpan.FromSeconds(attempt));
                }
            }
            if (string.IsNullOrEmpty(html))
                return null;

            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var body = doc.DocumentNode.Descendants("div")
                    .FirstOrDefault(node => node.Attributes["class"]?.Value?.Contains("KonaBody") ?? false);
                if (body == null)
                    body = doc.DocumentNode.SelectSingleNode("//*[@itemprop='articleBody']");
                if (body == null)
                    body = doc.DocumentNode.Descendants("div")
                        .FirstOrDefault(node => node.Attributes["class"]?.Value?.Contains("text-zone") ?? false);
                if (body == null)
                    body = doc.DocumentNode.SelectSingleNode("//article")
                        ?? doc.DocumentNode.SelectSingleNode("//main");
                if (body == null)
                    body = doc.DocumentNode.SelectSingleNode("//body");
                if (body == null)
                    return null;

                foreach (var script in body.Descendants("script").ToList())
                    script.Remove();
                foreach (var style in body.Descendants("style").ToList())
                    style.Remove();

                // Normalize images: absolute https src, no lazy attrs, no srcset overflow
                var imgCount = 0;
                foreach (var img in body.Descendants("img").ToList())
                {
                    imgCount++;
                    var srcAttr = img.Attributes["src"];
                    var src = srcAttr?.Value ?? "";
                    if (string.IsNullOrEmpty(src))
                    {
                        var lazy = img.Attributes["data-src"]?.Value ?? img.Attributes["data-lazy-src"]?.Value;
                        if (string.IsNullOrEmpty(lazy))
                        {
                            // <picture><source srcset="..."> pattern: fall back to first srcset URL
                            var picture = img.ParentNode != null && img.ParentNode.Name == "picture"
                                ? img.ParentNode.SelectSingleNode("source[@srcset]")
                                : null;
                            if (picture != null)
                                lazy = picture.Attributes["srcset"]?.Value?.Split(',')[0]?.Trim().Split(' ')[0];
                        }
                        if (!string.IsNullOrEmpty(lazy))
                        {
                            src = lazy;
                            img.SetAttributeValue("src", src);
                        }
                    }
                    if (src.StartsWith("//"))
                    {
                        src = "https:" + src;
                        img.SetAttributeValue("src", src);
                    }
                    else if (!string.IsNullOrEmpty(src) && !src.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            img.SetAttributeValue("src", new Uri(requestUri, src).AbsoluteUri);
                        }
                        catch
                        {
                        }
                    }
                    foreach (var attr in img.Attributes.Where(a =>
                                 a.Name == "srcset" || a.Name == "sizes" || a.Name.StartsWith("data-")).ToList())
                        attr.Remove();
                    var finalSrc = img.Attributes["src"]?.Value ?? "";
                    var isPlaceholder = string.IsNullOrEmpty(finalSrc) || finalSrc.Contains("data:image") ||
                        Regex.IsMatch(finalSrc, @"(spacer|1x1|blank|pixel|lazy)", RegexOptions.IgnoreCase);
                    if (isPlaceholder)
                    {
                        img.Remove();
                        continue;
                    }
                    // Broken images must not leave giant empty blocks in the reader
                    img.SetAttributeValue("onerror", "this.style.display='none'");
                    img.SetAttributeValue("style", "max-width:100%;height:auto;");
                }
                DiagnosticsReporter.Info("ANN", $"imgs found: {imgCount} in {url}");

                // ANN keeps the hero image outside the body container: pull og:image
                var og = Regex.Match(html, "property=\"og:image\"[^>]*content=\"([^\"]+)\"", RegexOptions.IgnoreCase);
                if (!og.Success)
                    og = Regex.Match(html, "content=\"([^\"]+)\"[^>]*property=\"og:image\"", RegexOptions.IgnoreCase);
                var inner = body.InnerHtml;
                if (og.Success)
                {
                    var ogUrl = SanitizeUrl(og.Groups[1].Value);
                    // Only real content images; ANN's generic logos live elsewhere
                    var isContent = ogUrl.Contains("/cms/") || ogUrl.Contains("/thumbnails/");
                    if (isContent && !string.IsNullOrEmpty(ogUrl) && !inner.Contains(ogUrl))
                        inner = "<img src=\"" + ogUrl + "\" style=\"max-width:100%;height:auto;\" />" + inner;
                }

                DiagnosticsReporter.Success("ANN", $"article extracted: {inner.Length} chars from {url}");
                DataCache.SaveArticleContentData($"ann_v4_{id}", inner, MalNewsType.News);
                return inner;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string SanitizeUrl(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "";
            var lastGt = raw.LastIndexOf('>');
            if (lastGt >= 0 && lastGt < raw.Length - 1)
                raw = raw.Substring(lastGt + 1);
            var match = Regex.Match(raw, @"https?://[^\s<>""']+", RegexOptions.IgnoreCase);
            if (!match.Success)
                return "";
            var url = match.Value;
            if (url.StartsWith("//"))
                url = "https:" + url;
            return url;
        }

        /// <summary>
        /// ANN feeds carry no images; scrape the /news/ listing once and map
        /// article id -> thumbnail (data-src="/thumbnails/.../{id}/x.jpg").
        /// </summary>
public static async Task<Dictionary<string, string>> FetchThumbMap()
        {
            try
            {
                using (var request = new System.Net.Http.HttpRequestMessage(
                    System.Net.Http.HttpMethod.Get, "https://www.animenewsnetwork.com/news/"))
                {
                    request.Headers.Add("User-Agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    using (var response = await AnnClient.SendAsync(request))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            DiagnosticsReporter.Warn("ANN", $"thumb map fetch failed: {response.StatusCode}");
                            return new Dictionary<string, string>();
                        }
                        var html = await response.Content.ReadAsStringAsync();
                        var doc = new HtmlDocument();
                        doc.LoadHtml(html);
                        var map = new Dictionary<string, string>();
                        // Find all elements with data-src attribute (img or div)
                        foreach (var node in doc.DocumentNode.Descendants().Where(n => n.Attributes["data-src"] != null))
                        {
                            var dataSrc = node.Attributes["data-src"].Value;
                            var thumbUrl = SanitizeUrl(dataSrc);
                            if (string.IsNullOrEmpty(thumbUrl) || !thumbUrl.Contains("/thumbnails/"))
                                continue;
                            // Find the nearest parent <a> with href containing article ID
                            var parentLink = node.Ancestors("a").FirstOrDefault(a => a.Attributes["href"] != null);
                            if (parentLink == null) continue;
                            var href = parentLink.Attributes["href"].Value;
                            var idMatch = Regex.Match(href, @"(\d+)(?=/|$|\.html|\?)");
                            if (!idMatch.Success) continue;
                            var id = idMatch.Groups[1].Value;
                            if (!map.ContainsKey(id))
                                map[id] = thumbUrl;
                        }
                        DiagnosticsReporter.Info("ANN", $"thumb map: {map.Count} entries from listing scrape");
                        return map;
                    }
                }
            }
            catch (Exception ex)
            {
                DiagnosticsReporter.Error("ANN", "FetchThumbMap exception", ex);
                return new Dictionary<string, string>();
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
                    var title = StripHtml(WebUtility.HtmlDecode(ExtractTag(block, "title"))).Trim().TrimStart('>').Trim();
                    var link = SanitizeUrl(ExtractTag(block, "link"));
                    var rawDescription = ExtractRawTag(block, "description");
                    var description = StripHtml(WebUtility.HtmlDecode(rawDescription)).Trim().TrimStart('>').Trim();
                    var pubDate = ExtractTag(block, "pubDate");
                    var categories = new List<string>();
                    foreach (Match catMatch in Regex.Matches(block, "<category>(.*?)</category>"))
                        categories.Add(WebUtility.HtmlDecode(catMatch.Groups[1].Value.Trim()));
                    // Only content relevant to the app: anime and manga
                    var relevant = categories.Any(c =>
                        c.Equals("Anime", StringComparison.OrdinalIgnoreCase) ||
                        c.Equals("Manga", StringComparison.OrdinalIgnoreCase));
                    if (!relevant)
                        continue;
                    var category = string.Join(", ", categories.Where(c => !string.IsNullOrEmpty(c)));
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
                catch (Exception ex)
                {
                    DiagnosticsReporter.Error("ANN", $"ParseRss item parse failed: {ex.Message}");
                }
            }

            var withImages = output.Count(o => !string.IsNullOrEmpty(o.ImgUrl));
            DiagnosticsReporter.Info("ANN", $"RSS parsed: {output.Count} entries, {withImages} with images, {output.Count - withImages} without");
            return output;
        }

        private static string ExtractTag(string block, string tag)
        {
            var match = Regex.Match(block, $"<{tag}(?s)(.*?)</{tag}>|<{tag}[^>]*>([^<]*)");
            if (!match.Success)
                return "";
            var value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            value = Regex.Replace(value, "^\\s*<!\\[CDATA\\[", "");
            value = Regex.Replace(value, "\\]\\]>\\s*$", "").Trim();
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
            var match = Regex.Match(html, "<img[^>]+(?:src|data-src)=[\"']([^\"']+)\"");
            if (!match.Success)
            {
                // Try data-src without src
                match = Regex.Match(html, "data-src=[\"']([^\"']+)\"");
            }
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
            // ANN article URLs end with /.{id} (e.g. /news/.../.241011)
            // Capture digits after the final dot at end of path
            var match = Regex.Match(url, @"\.(\d+)(?=/|$|\.html|\?)");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}





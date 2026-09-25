using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MALClient.Models.Enums;
using MALClient.Models.Models.AnimeScrapped;
using MALClient.XShared.Utils;
using MALClient.XShared.Comm.Anime;

namespace MALClient.XShared.Comm.Anime
{
    public class AnimeRelatedQuery : Query
    {
        private readonly int _animeId;
        private readonly bool _animeMode;

        public AnimeRelatedQuery(int id, bool anime = true)
        {
            Request =
                new Uri(Uri.EscapeUriString($"https://myanimelist.net/{(anime ? "anime" : "manga")}/{id}/"));
            _animeId = id;
            _animeMode = anime;
        }

        public async Task<List<RelatedAnimeData>> GetRelatedAnime(bool force = false)
        {
            var output = force
                ? new List<RelatedAnimeData>()
                : await DataCache.RetrieveRelatedAnimeData(_animeId, _animeMode) ?? new List<RelatedAnimeData>();
            if (output.Count != 0) return output;

            output = await FetchFromTenraiAsync();
            if (output != null && output.Count > 0)
            {
                DataCache.SaveRelatedAnimeData(_animeId, output, _animeMode);
                return output;
            }

            return await FetchFromHtmlScraperAsync();
        }

        private async Task<List<RelatedAnimeData>> FetchFromTenraiAsync()
        {
            try
            {
                var endpoint = _animeMode ? $"anime/{_animeId}/full" : $"manga/{_animeId}/full";
                var data = await TenraiClient.GetDataAsync(endpoint);
                if (!data.TryGetProperty("relations", out var relations) || relations.ValueKind != JsonValueKind.Array)
                    return null;

                var output = new List<RelatedAnimeData>();
                foreach (var rel in relations.EnumerateArray())
                {
                    try
                    {
                        if (rel.ValueKind != JsonValueKind.Object)
                            continue;
                        if (!rel.TryGetProperty("relation", out var relationProp) || relationProp.ValueKind != JsonValueKind.String)
                            continue;
                        if (!rel.TryGetProperty("entry", out var entry))
                            continue;

                        var relation = WebUtility.HtmlDecode(relationProp.GetString());
                        IEnumerable<JsonElement> entries;
                        if (entry.ValueKind == JsonValueKind.Array)
                            entries = entry.EnumerateArray().ToList();
                        else if (entry.ValueKind == JsonValueKind.Object)
                            entries = new[] { entry };
                        else
                            continue;

                        foreach (var ent in entries)
                        {
                            try
                            {
                                if (ent.ValueKind != JsonValueKind.Object)
                                    continue;
                                var malId = ent.TryGetProperty("mal_id", out var idProp) && idProp.ValueKind == JsonValueKind.Number
                                    ? idProp.GetInt32()
                                    : 0;
                                if (malId <= 0)
                                    continue;

                                var typeStr = ent.TryGetProperty("type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String
                                    ? typeProp.GetString()
                                    : null;

                                var current = new RelatedAnimeData();
                                current.WholeRelation = relation;
                                current.Type = typeStr == "anime"
                                    ? RelatedItemType.Anime
                                    : typeStr == "manga" ? RelatedItemType.Manga : RelatedItemType.Unknown;
                                current.Id = malId;
                                current.Title = WebUtility.HtmlDecode(
                                    ent.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String
                                        ? nameProp.GetString()
                                        : "");

                                var imgUrl = GetNestedImageUrl(ent);
                                if (!string.IsNullOrEmpty(imgUrl))
                                    current.ImgUrl = NormalizeImageUrl(imgUrl);

                                output.Add(current);
                            }
                            catch (Exception)
                            {
                                // skip malformed relation entry
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // skip malformed relation
                    }
                }

                return output.Count > 0 ? output : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private async Task<List<RelatedAnimeData>> FetchFromHtmlScraperAsync()
        {
            var output = new List<RelatedAnimeData>();

            var raw = await GetRequestResponse();
            if (string.IsNullOrEmpty(raw))
                return null;

            var doc = new HtmlDocument();
            doc.LoadHtml(raw);
            try
            {
                var relationsNode = doc.DocumentNode.Descendants("div")
                    .First(
                        node =>
                            node.Attributes.Contains("class") &&
                            node.Attributes["class"].Value ==
                            "related-entries");


                try
                {
                    var tile = relationsNode.Descendants("div")
                        .First(
                            node =>
                                node.Attributes.Contains("class") &&
                                node.Attributes["class"].Value ==
                                "entries-tile");
                    var tileContents = tile.Descendants("div")
                        .Where(
                            node =>
                                node.Attributes.Contains("class") &&
                                node.Attributes["class"].Value ==
                                "content").ToList();

                    foreach (var content in tileContents)
                    {
                        var relationDiv = content.Descendants("div")
                        .First(
                            node =>
                                node.Attributes.Contains("class") &&
                                node.Attributes["class"].Value ==
                                "relation");

                        var relation = WebUtility.HtmlDecode(relationDiv.InnerText.Trim());
                        relation = Regex.Replace(relation.Trim(), @"\t|\n|\r|  ", "");

                        var titleDiv = content.Descendants("div")
                        .First(
                            node =>
                                node.Attributes.Contains("class") &&
                                node.Attributes["class"].Value ==
                                "title");

                        var linkNode = titleDiv.Descendants("a").First();

                        var current = new RelatedAnimeData();
                        current.WholeRelation = relation;
                        var link = linkNode.Attributes["href"].Value.Split('/');
                        current.Type = link[3] == "anime"
                            ? RelatedItemType.Anime
                            : link[3] == "manga" ? RelatedItemType.Manga : RelatedItemType.Unknown;
                        current.Id = Convert.ToInt32(link[4]);
                        current.Title = WebUtility.HtmlDecode(linkNode.InnerText.Trim().Trim('\n'));

                        // The entry's real poster lives in the sibling div.image of
                        // this entry (not the whole tile) - scope by the current entry
                        // so each related title gets its own poster.
                        var entry = content.ParentNode;
                        var imgNode = entry.Descendants("img").FirstOrDefault();
                        var imgSrc = imgNode?.Attributes["data-src"]?.Value ?? imgNode?.Attributes["src"]?.Value;
                        if (!string.IsNullOrEmpty(imgSrc))
                        {
                            if (imgSrc.StartsWith("//"))
                                imgSrc = "https:" + imgSrc;
                            current.ImgUrl = NormalizeImageUrl(imgSrc);
                        }

                        output.Add(current);
                    }
                }
                catch (Exception)
                {
                    //mystery
                }

                try
                {
                    var table = relationsNode.Descendants("table").First();
                    var trs = table.Descendants("tr").ToList();

                    foreach (var t in trs)
                    {
                        var tds = t.Descendants("td").ToList();
                        var relation = WebUtility.HtmlDecode(tds[0].InnerText.Trim());
                        foreach (var linkNode in tds[1].Descendants("a"))
                        {
                            var current = new RelatedAnimeData();
                            current.WholeRelation = relation;
                            var link = linkNode.Attributes["href"].Value.Split('/');
                            current.Type = link[3] == "anime"
                                ? RelatedItemType.Anime
                                : link[3] == "manga" ? RelatedItemType.Manga : RelatedItemType.Unknown;
                            current.Id = Convert.ToInt32(link[4]);
                            current.Title = WebUtility.HtmlDecode(linkNode.InnerText.Trim());
                            output.Add(current);
                        }
                    }
                }
                catch (Exception)
                {
                    //mystery
                }

            }
            catch (Exception ex)
            {
                DiagnosticsReporter.Info("Related", $"parse failed for anime {_animeId}: {ex.Message}");
            }

            if (output.Count > 0)
                DataCache.SaveRelatedAnimeData(_animeId, output, _animeMode);
            else
                DiagnosticsReporter.Warn("Related", $"no related entries found for anime {_animeId}");

            return output;
        }

        private static string GetNestedImageUrl(JsonElement entry)
        {
            if (!entry.TryGetProperty("images", out var images) || images.ValueKind != JsonValueKind.Object)
                return null;
            if (!images.TryGetProperty("jpg", out var jpg) || jpg.ValueKind != JsonValueKind.Object)
                return null;
            if (!jpg.TryGetProperty("image_url", out var url) || url.ValueKind != JsonValueKind.String)
                return null;
            return url.GetString();
        }

        private static string NormalizeImageUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            url = Regex.Replace(url, @"\/r\/\d+x\d+\/", "/");
            var qPos = url.IndexOf('?');
            if (qPos > 0) url = url.Substring(0, qPos);
            var dotPos = url.LastIndexOf('.');
            if (dotPos > 0)
            {
                var beforeDot = url.Substring(0, dotPos);
                var lastChar = beforeDot[beforeDot.Length - 1];
                if (lastChar != 'l' && lastChar != 'm' && lastChar != 's')
                    url = beforeDot + "l" + url.Substring(dotPos);
            }
            return url;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MALClient.Models.Enums;
using MALClient.Models.Models.AnimeScrapped;
using MALClient.XShared.Utils;

namespace MALClient.XShared.Comm.Anime
{
    public class AnimeDirectRecommendationsQuery : Query
    {
        private readonly int _animeId;
        private readonly bool _animeMode;

        public AnimeDirectRecommendationsQuery(int id, bool anime = true)
        {
            Request =
                new Uri(
                    Uri.EscapeUriString($"https://myanimelist.net/{(anime ? "anime" : "manga")}/{id}/whatever/userrecs"));
            _animeId = id;
            _animeMode = anime;
        }

        public async Task<List<DirectRecommendationData>> GetDirectRecommendations(bool force = false)
        {
            var output = force
                ? new List<DirectRecommendationData>()
                : await DataCache.RetrieveDirectRecommendationData(_animeId, _animeMode) ??
                  new List<DirectRecommendationData>();
            if (output.Count != 0) return output;

            output = await FetchFromTenraiAsync();
            if (output != null && output.Count > 0)
            {
                EnrichWithDescriptionsFromMal(output);
                DataCache.SaveDirectRecommendationsData(_animeId, output, _animeMode);
                return output;
            }

            output = await FetchDescriptionsFromMalAsync();
            if (output != null && output.Count > 0)
                DataCache.SaveDirectRecommendationsData(_animeId, output, _animeMode);
            return output ?? new List<DirectRecommendationData>();
        }

        private async Task<List<DirectRecommendationData>> FetchFromTenraiAsync()
        {
            try
            {
                var endpoint = _animeMode ? $"anime/{_animeId}/recommendations" : $"manga/{_animeId}/recommendations";
                var data = await TenraiClient.GetDataAsync(endpoint);
                if (data.ValueKind != JsonValueKind.Array)
                    return null;

                var output = new List<DirectRecommendationData>();
                foreach (var item in data.EnumerateArray())
                {
                    try
                    {
                        var current = new DirectRecommendationData();
                        if (!item.TryGetProperty("entry", out var entry) || entry.ValueKind != JsonValueKind.Object)
                            continue;

                        var malId = entry.TryGetProperty("mal_id", out var idProp) && idProp.ValueKind == JsonValueKind.Number
                            ? idProp.GetInt32()
                            : 0;
                        if (malId <= 0)
                            continue;
                        current.Id = malId;
                        current.Title = WebUtility.HtmlDecode(
                            entry.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String
                                ? titleProp.GetString()
                                : "");

                        var entryUrl = entry.TryGetProperty("url", out var urlProp) && urlProp.ValueKind == JsonValueKind.String
                            ? urlProp.GetString()
                            : "";
                        if (entryUrl.Contains("/manga/"))
                            current.Type = RelatedItemType.Manga;
                        else if (entryUrl.Contains("/anime/"))
                            current.Type = RelatedItemType.Anime;
                        else
                            current.Type = RelatedItemType.Unknown;

                        if (entry.TryGetProperty("images", out var images) && images.ValueKind == JsonValueKind.Object &&
                            images.TryGetProperty("jpg", out var jpg) && jpg.ValueKind == JsonValueKind.Object &&
                            jpg.TryGetProperty("image_url", out var imgUrl) && imgUrl.ValueKind == JsonValueKind.String)
                            current.ImageUrl = NormalizeImageUrl(imgUrl.GetString());

                        output.Add(current);
                    }
                    catch (Exception)
                    {
                        // skip malformed recommendation
                    }
                }
                return output.Count > 0 ? output : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void EnrichWithDescriptionsFromMal(List<DirectRecommendationData> output)
        {
            if (output == null || output.Count == 0)
                return;
            try
            {
                var scraped = FetchDescriptionsFromMalAsync().GetAwaiter().GetResult();
                if (scraped == null || scraped.Count == 0)
                    return;
                var byId = scraped.Where(r => r.Id > 0).ToDictionary(r => r.Id, r => r.Description);
                foreach (var item in output)
                {
                    if (byId.TryGetValue(item.Id, out var desc) && !string.IsNullOrEmpty(desc))
                        item.Description = desc;
                }
            }
            catch (Exception)
            {
                // enrichment is best-effort
            }
        }

        private async Task<List<DirectRecommendationData>> FetchDescriptionsFromMalAsync()
        {
            var output = new List<DirectRecommendationData>();
            var raw = await GetRequestResponse();
            if (string.IsNullOrEmpty(raw))
                return output;

            var doc = new HtmlDocument();
            doc.LoadHtml(raw);
            try
            {
                var recommNodes = doc.DocumentNode.Descendants("div")
                    .Where(
                        node =>
                            node.Attributes.Contains("class") &&
                            node.Attributes["class"].Value ==
                            "borderClass").Take(Settings.RecommsToPull);

                foreach (var recommNode in recommNodes)
                {
                    try
                    {
                        var current = new DirectRecommendationData();

                        var tds = recommNode.Descendants("td").Take(2).ToList();
                         var img = tds[0].Descendants("img").First().Attributes["data-src"].Value;
                         if (!img.Contains("questionmark"))
                         {
                             img = Regex.Replace(img, @"\/r\/\d+x\d+", "");
                             var qPos = img.IndexOf('?');
                             if (qPos > 0) img = img.Substring(0, qPos);
                             current.ImageUrl = img;
                         }
                        current.Description = WebUtility.HtmlDecode(tds[1].Descendants("div").First(
                            node =>
                                node.Attributes.Contains("class") &&
                                node.Attributes["class"].Value ==
                                "borderClass bgColor1")
                            .Descendants("div")
                            .First().InnerText.Trim().Replace("&nbsp", "").Replace("read more", ""));
                        current.Description = current.Description.Substring(0, current.Description.Length - 1);
                        var titleNode = tds[1].ChildNodes[3].Descendants("a").First();
                        current.Title = titleNode.Descendants("strong").First().InnerText.Trim();
                        var link = titleNode.Attributes["href"].Value.Split('/');
                        current.Id = Convert.ToInt32(link[4]);
                        current.Type = link[3] == "anime"
                            ? RelatedItemType.Anime
                            : link[3] == "manga" ? RelatedItemType.Manga : RelatedItemType.Unknown;
                        output.Add(current);
                    }
                    catch (Exception)
                    {
                        //who knows...raw html is scary
                    }
                }
            }
            catch (Exception)
            {
                //something we wrong
            }

            return output;
        }

        private static string NormalizeImageUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            url = Regex.Replace(url, @"\/r\/\d+x\d+", "");
            var qPos = url.IndexOf('?');
            return qPos > 0 ? url.Substring(0, qPos) : url;
        }
    }
}

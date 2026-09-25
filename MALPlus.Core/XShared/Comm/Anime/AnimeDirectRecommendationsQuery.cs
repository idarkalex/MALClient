using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MALClient.Models.Enums;
using MALClient.Models.Models.AnimeScrapped;
using MALClient.XShared.Utils;

namespace MALClient.XShared.Comm.Anime
{
    public class AnimeDirectRecommendationsQuery : Query
    {
        private static readonly SemaphoreSlim MediaTypeEnrichmentGate = new SemaphoreSlim(4);
        private static readonly TimeSpan EnrichmentTimeout = TimeSpan.FromSeconds(8);
        private static readonly TimeSpan MediaTypeRequestTimeout = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan DescriptionRequestTimeout = TimeSpan.FromSeconds(4);
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
                try
                {
                    await EnrichRecommendationsAsync(output);
                }
                catch (Exception)
                {
                }
                await DataCache.SaveDirectRecommendationsData(_animeId, output, _animeMode);
                return output;
            }

            output = await FetchDescriptionsFromMalAsync(CancellationToken.None);
            if (output != null && output.Count > 0)
            {
                using var cancellation = new CancellationTokenSource(EnrichmentTimeout);
                try
                {
                    await PopulateMediaTypesAsync(output, cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception)
                {
                }
                await DataCache.SaveDirectRecommendationsData(_animeId, output, _animeMode);
            }
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
                        current.MediaType = NormalizeMediaType(GetString(entry, "media_type"));
                        if (string.IsNullOrEmpty(current.MediaType))
                        {
                            var entryType = GetString(entry, "type");
                            if (!string.Equals(entryType, "anime", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(entryType, "manga", StringComparison.OrdinalIgnoreCase))
                                current.MediaType = NormalizeMediaType(entryType);
                        }

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

        private async Task EnrichRecommendationsAsync(List<DirectRecommendationData> output)
        {
            if (output == null || output.Count == 0)
                return;

            using var cancellation = new CancellationTokenSource(EnrichmentTimeout);
            try
            {
                var work = Task.WhenAll(
                    EnrichDescriptionsAsync(output, cancellation.Token),
                    PopulateMediaTypesAsync(output, cancellation.Token));
                var completed = await Task.WhenAny(work, Task.Delay(EnrichmentTimeout));
                if (completed == work)
                    await work;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
            }
        }

        private async Task EnrichDescriptionsAsync(List<DirectRecommendationData> output,
            CancellationToken cancellationToken)
        {
            try
            {
                var scraped = await FetchDescriptionsFromMalAsync(cancellationToken);
                if (scraped == null || scraped.Count == 0)
                    return;
                cancellationToken.ThrowIfCancellationRequested();
                var byKey = scraped.Where(r => r.Id > 0)
                    .GroupBy(r => (r.Type, r.Id))
                    .ToDictionary(g => g.Key, g => g.First().Description);
                var byId = scraped.Where(r => r.Id > 0)
                    .GroupBy(r => r.Id)
                    .ToDictionary(g => g.Key, g => g.First().Description);
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var item in output)
                {
                    if (!byKey.TryGetValue((item.Type, item.Id), out var description))
                        byId.TryGetValue(item.Id, out description);
                    if (!string.IsNullOrEmpty(description))
                        item.Description = description;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
            }
        }

        private async Task PopulateMediaTypesAsync(List<DirectRecommendationData> output,
            CancellationToken cancellationToken)
        {
            var tasks = output
                .Where(item => item.Id > 0 && string.IsNullOrEmpty(item.MediaType))
                .Select(item => PopulateMediaTypeAsync(item, cancellationToken))
                .ToArray();
            await Task.WhenAll(tasks);
        }

        private async Task PopulateMediaTypeAsync(DirectRecommendationData item,
            CancellationToken cancellationToken)
        {
            await MediaTypeEnrichmentGate.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var animeMode = item.Type == RelatedItemType.Manga ? false : _animeMode;
                var cached = await DataCache.RetrieveAnimeSearchResultsData(item.Id.ToString(), animeMode);
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.IsNullOrEmpty(cached?.Type))
                {
                    item.MediaType = NormalizeMediaType(cached.Type);
                    return;
                }
                var endpoint = animeMode ? $"anime/{item.Id}/full" : $"manga/{item.Id}/full";
                var data = await TenraiClient.GetDataAsync(endpoint, MediaTypeRequestTimeout);
                cancellationToken.ThrowIfCancellationRequested();
                if (data.ValueKind == JsonValueKind.Object)
                    item.MediaType = NormalizeMediaType(GetString(data, "type"));
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
            }
            finally
            {
                MediaTypeEnrichmentGate.Release();
            }
        }

        private async Task<List<DirectRecommendationData>> FetchDescriptionsFromMalAsync(
            CancellationToken cancellationToken)
        {
            var output = new List<DirectRecommendationData>();
            string raw;
            try
            {
                using var timeout = new CancellationTokenSource(DescriptionRequestTimeout);
                using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, timeout.Token);
                using var response = await _client.GetAsync(Request, cancellation.Token);
                if (!response.IsSuccessStatusCode)
                    return output;
                raw = await response.Content.ReadAsStringAsync(cancellation.Token);
            }
            catch (Exception)
            {
                return output;
            }

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

        private static string GetString(JsonElement element, string property)
        {
            return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : "";
        }

        private static string NormalizeMediaType(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
                return type;
            return type switch
            {
                "tv" => "TV",
                "tv_special" => "TV Special",
                "movie" => "Movie",
                "ova" => "OVA",
                "ona" => "ONA",
                "special" => "Special",
                "music" => "Music",
                "cm" => "Commercial",
                "pv" => "PV",
                "manga" => "Manga",
                "novel" => "Novel",
                "light_novel" => "Light Novel",
                "one_shot" => "One-shot",
                "oneshot" => "One-shot",
                "doujinshi" => "Doujinshi",
                "doujin" => "Doujinshi",
                "manhwa" => "Manhwa",
                "manhua" => "Manhua",
                _ => type
            };
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

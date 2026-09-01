using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MALClient.Models.Models.AnimeScrapped;
using MALClient.XShared.Utils;

namespace MALClient.XShared.Comm.Anime
{
    public class AnimeVideosQuery : Query
    {
        private readonly int _id;

        public AnimeVideosQuery(int id)
        {
            _id = id;
            Request =
                new Uri(
                    Uri.EscapeUriString(
                        $"https://myanimelist.net/anime/{id}/whatever/video"));
        }

        public async Task<List<AnimeVideoData>> GetVideos(bool force)
        {
            var output = force
                ? new List<AnimeVideoData>()
                : await DataCache.RetrieveData<List<AnimeVideoData>>($"videos_{_id}", "AnimeDetails", 7) ??
                  new List<AnimeVideoData>();

            if (output.Any())
                return output;

            output = await FetchFromTenraiAsync();
            if (output != null && output.Count > 0)
            {
                DataCache.SaveData(output, $"videos_{_id}", "AnimeDetails");
                return output;
            }

            output = await FetchFromHtmlAsync();
            if (output.Count > 0)
                DataCache.SaveData(output, $"videos_{_id}", "AnimeDetails");
            return output;
        }

        private async Task<List<AnimeVideoData>> FetchFromTenraiAsync()
        {
            try
            {
                var data = await TenraiClient.GetDataAsync($"anime/{_id}/videos");
                if (data.ValueKind != JsonValueKind.Object)
                    return null;
                if (!data.TryGetProperty("promo", out var promo) || promo.ValueKind != JsonValueKind.Array)
                    return null;

                var output = new List<AnimeVideoData>();
                foreach (var p in promo.EnumerateArray())
                {
                    try
                    {
                        if (p.ValueKind != JsonValueKind.Object)
                            continue;
                        if (!p.TryGetProperty("trailer", out var trailer) || trailer.ValueKind != JsonValueKind.Object)
                            continue;

                        var ytId = trailer.TryGetProperty("youtube_id", out var ytProp) && ytProp.ValueKind == JsonValueKind.String
                            ? ytProp.GetString()
                            : null;
                        if (string.IsNullOrEmpty(ytId))
                            continue;

                        var current = new AnimeVideoData();

                        var title = trailer.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String
                            ? titleProp.GetString()
                            : p.TryGetProperty("title", out var promoTitle) && promoTitle.ValueKind == JsonValueKind.String
                                ? promoTitle.GetString()
                                : null;
                        current.Name = title ?? $"Promotional video {output.Count + 1}";
                        current.YtLink = $"https://www.youtube.com/watch?v={ytId}";

                        if (trailer.TryGetProperty("images", out var images) && images.ValueKind == JsonValueKind.Object)
                        {
                            var thumb = GetThumb(images);
                            if (!string.IsNullOrEmpty(thumb))
                                current.Thumb = thumb;
                        }

                        current.AnimeId = _id;
                        output.Add(current);
                    }
                    catch (Exception)
                    {
                        // skip malformed promo
                    }
                }
                return output.Count > 0 ? output : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string GetThumb(JsonElement images)
        {
            if (images.TryGetProperty("maximum_image_url", out var max) && max.ValueKind == JsonValueKind.String)
                return max.GetString();
            if (images.TryGetProperty("large_image_url", out var large) && large.ValueKind == JsonValueKind.String)
                return large.GetString();
            if (images.TryGetProperty("image_url", out var img) && img.ValueKind == JsonValueKind.String)
                return img.GetString();
            return null;
        }

        private async Task<List<AnimeVideoData>> FetchFromHtmlAsync()
        {
            var output = new List<AnimeVideoData>();
            var raw = await GetRequestResponse();
            if (string.IsNullOrEmpty(raw))
                return output;

            var doc = new HtmlDocument();
            doc.LoadHtml(raw);

            try
            {
                foreach (
                    var video in
                    doc.FirstOfDescendantsWithClass("div", "video-block promotional-video mt16")
                        .WhereOfDescendantsWithClass("div", "video-list-outer po-r pv"))
                {
                    try
                    {
                        var current = new AnimeVideoData();
                        var img = video.Descendants("img").First();
                        current.Thumb = img.Attributes["data-src"].Value;
                        if (current.Thumb.Contains("banned"))
                            continue;
                        var href = video.Descendants("a").First().Attributes["href"].Value;
                        var pos = href.IndexOf('?');
                        href = href.Substring(0, pos);
                        current.YtLink = $"https://www.youtube.com/watch?v={href.Split('/').Last()}";

                        current.Name = WebUtility.HtmlDecode(img.Attributes["data-title"].Value);

                        output.Add(current);
                    }
                    catch (Exception)
                    {
                        //html
                    }

                }
            }
            catch (Exception)
            {
                //no videos
            }

            return output;
        }
    }
}

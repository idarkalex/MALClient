using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MALClient.Models.Models.AnimeScrapped;
using MALClient.XShared.Utils;

namespace MALClient.XShared.Comm.Anime
{
    public enum MangaAdaptedType
    {
        All,
        AiringNow,
        UpcomingAnime
    }

    public class AnimeAdaptedToAnimeQuery : Query
    {
        private static Dictionary<MangaAdaptedType, List<TopAnimeData>> _prevQueriesCache = new Dictionary<MangaAdaptedType, List<TopAnimeData>>();
        private readonly MangaAdaptedType _type;

        public AnimeAdaptedToAnimeQuery(MangaAdaptedType type)
        {
            Request = WebRequest.Create(Uri.EscapeUriString(GetEndpoint(type)));
            Request.ContentType = "application/x-www-form-urlencoded";
            Request.Method = "GET";
            _type = type;
        }

        public static string ToDisplayName(MangaAdaptedType type)
        {
            switch (type)
            {
                case MangaAdaptedType.All:
                    return "All";
                case MangaAdaptedType.AiringNow:
                    return "Airing Now";
                case MangaAdaptedType.UpcomingAnime:
                    return "Upcoming Anime";
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        private string GetEndpoint(MangaAdaptedType type)
        {
            switch (type)
            {
                case MangaAdaptedType.AiringNow:
                    return "https://myanimelist.net/manga/adapted";
                case MangaAdaptedType.All:
                    return "https://myanimelist.net/manga/adapted?type=all";
                case MangaAdaptedType.UpcomingAnime:
                    return "https://myanimelist.net/manga/adapted?type=upcoming";
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public async Task<List<TopAnimeData>> GetAdaptedToAnimeData(bool force = false)
        {
            if (!force)
                if (_prevQueriesCache.ContainsKey(_type))
                    return _prevQueriesCache[_type];

            var output = force ? new List<TopAnimeData>() : (await DataCache.RetrieveAdaptedToAnimeData(_type) ?? new List<TopAnimeData>());
            if (output.Count > 0)
            {
                _prevQueriesCache[_type] = output;
                return output;
            }

            var raw = await GetRequestResponse();
            if (string.IsNullOrEmpty(raw))
                return new List<TopAnimeData>();

            var doc = new HtmlDocument();
            doc.LoadHtml(raw);
            var items = doc.DocumentNode.Descendants("div")
                .Where(node => node.Attributes.Contains("class") && node.Attributes["class"].Value.Contains("js-seasonal-anime"))
                .ToList();

            var index = 0;
            foreach (var item in items)
            {
                try
                {
                    var current = new TopAnimeData();
                    var titleNode = item.Descendants("a").FirstOrDefault(node =>
                        node.Attributes.Contains("class") && node.Attributes["class"].Value == "link-title");
                    if (titleNode == null)
                        continue;
                    current.Title = WebUtility.HtmlDecode(titleNode.InnerText).Trim();
                    current.Id = Convert.ToInt32(titleNode.Attributes["href"].Value.Substring("/manga/".Length).Split('/')[0]);

                    var img = item.Descendants("img").FirstOrDefault();
                    if (img != null)
                    {
                        var src = img.Attributes.Contains("data-src")
                            ? img.Attributes["data-src"].Value
                            : img.Attributes.Contains("src") ? img.Attributes["src"].Value : null;
                        if (!string.IsNullOrEmpty(src))
                        {
                            var pos = src.IndexOf('?');
                            if (pos != -1)
                                src = src.Substring(0, pos);
                            current.ImgUrl = src;
                        }
                    }

                    var scoreNode = item.Descendants("div").FirstOrDefault(node =>
                        node.Attributes.Contains("class") && node.Attributes["class"].Value.Contains("scormem-item score"));
                    if (scoreNode != null)
                    {
                        var scoreText = scoreNode.InnerText.Replace("\u2605", "").Trim();
                        float.TryParse(scoreText, NumberStyles.Float, CultureInfo.InvariantCulture, out var score);
                        current.Score = score;
                    }

                    current.Index = ++index;
                    output.Add(current);
                }
                catch (Exception)
                {
                    //skip malformed entries
                }
            }

            DataCache.SaveAdaptedToAnimeData(output, _type);
            _prevQueriesCache[_type] = output;
            return output;
        }
    }
}

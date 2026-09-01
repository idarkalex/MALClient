using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MALClient.Models.Models.AnimeScrapped;
using MALClient.XShared.Utils;

namespace MALClient.XShared.Comm.Anime
{
    public enum TopAnimeType
    {
        General,
        Airing,
        Upcoming,
        Tv,
        Movies,
        Ovas,
        Popular,
        Favourited,
        Manga
    }

    public enum MangaTopType
    {
        All,
        Manga,
        Novels,
        LightNovels,
        OneShots,
        Doujinshi,
        Manhwa,
        Manhua,
        Popular,
        Favourited
    }


    public class AnimeTopQuery : Query
    {
        private static Dictionary<TopAnimeType,List<TopAnimeData>> _prevQueriesCache = new Dictionary<TopAnimeType, List<TopAnimeData>>();
        private static Dictionary<MangaTopType, List<TopAnimeData>> _prevMangaQueriesCache = new Dictionary<MangaTopType, List<TopAnimeData>>();
        private TopAnimeType _type;
        private MangaTopType _mangaType;
        private bool _isManga;
        private int _page;
        public AnimeTopQuery(TopAnimeType topType,int page = 0)
        {
            Request =
                new Uri(
                    Uri.EscapeUriString($"https://myanimelist.net/{GetEndpoint(topType,page)}"));
            _page = page;
            _type = topType;
            _isManga = false;
        }

        public AnimeTopQuery(MangaTopType topType, int page = 0)
        {
            Request =
                new Uri(
                    Uri.EscapeUriString($"https://myanimelist.net/{GetMangaEndpoint(topType, page)}"));
            _page = page;
            _mangaType = topType;
            _isManga = true;
        }

        private string GetEndpoint(TopAnimeType type,int page)
        { 
            switch (type)
            {
                case TopAnimeType.General:
                    return $"topanime.php?limit={page*50}";
                case TopAnimeType.Airing:
                    return $"topanime.php?type=airing&limit={page*50}";
                case TopAnimeType.Upcoming:
                    return $"topanime.php?type=upcoming&limit={page*50}";
                case TopAnimeType.Tv:
                    return $"topanime.php?type=tv&limit={page*50}";
                case TopAnimeType.Movies:
                    return $"topanime.php?type=movie&limit={page*50}";
                case TopAnimeType.Ovas:
                    return $"topanime.php?type=ova&limit={page*50}";
                case TopAnimeType.Popular:
                    return $"topanime.php?type=bypopularity&limit={page*50}";
                case TopAnimeType.Favourited:
                    return $"topanime.php?type=favorite&limit={page*50}";
                case TopAnimeType.Manga:
                    return $"topmanga.php?limit={page*50}";
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        private string GetMangaEndpoint(MangaTopType type, int page)
        {
            switch (type)
            {
                case MangaTopType.All:
                    return $"topmanga.php?limit={page*50}";
                case MangaTopType.Manga:
                    return $"topmanga.php?type=manga&limit={page*50}";
                case MangaTopType.Novels:
                    return $"topmanga.php?type=novels&limit={page*50}";
                case MangaTopType.LightNovels:
                    return $"topmanga.php?type=lightnovels&limit={page*50}";
                case MangaTopType.OneShots:
                    return $"topmanga.php?type=oneshots&limit={page*50}";
                case MangaTopType.Doujinshi:
                    return $"topmanga.php?type=doujin&limit={page*50}";
                case MangaTopType.Manhwa:
                    return $"topmanga.php?type=manhwa&limit={page*50}";
                case MangaTopType.Manhua:
                    return $"topmanga.php?type=manhua&limit={page*50}";
                case MangaTopType.Popular:
                    return $"topmanga.php?type=bypopularity&limit={page*50}";
                case MangaTopType.Favourited:
                    return $"topmanga.php?type=favorite&limit={page*50}";
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public async Task<List<TopAnimeData>> GetTopAnimeData(bool force = false)
        {
            if (!force)
            {
                if (_isManga)
                {
                    if (_prevMangaQueriesCache.ContainsKey(_mangaType))
                        return _prevMangaQueriesCache[_mangaType];
                }
                else if (_prevQueriesCache.ContainsKey(_type))
                    return _prevQueriesCache[_type];
            }

            var output = force
                ? new List<TopAnimeData>()
                : _isManga
                    ? (await DataCache.RetrieveTopMangaData(_mangaType) ?? new List<TopAnimeData>())
                    : (await DataCache.RetrieveTopAnimeData(_type) ?? new List<TopAnimeData>());
            if (output.Count > 0)
            {
                if (_isManga)
                    _prevMangaQueriesCache[_mangaType] = output;
                else
                    _prevQueriesCache[_type] = output;
                return output;
            }

            output = await FetchFromTenraiAsync();
            if (output != null && output.Count > 0)
            {
                MergeWithPreviousPage(output);
                if (_isManga)
                {
                    DataCache.SaveTopMangaData(output, _mangaType);
                    _prevMangaQueriesCache[_mangaType] = output;
                }
                else
                {
                    DataCache.SaveTopAnimeData(output, _type);
                    _prevQueriesCache[_type] = output;
                }
                return output;
            }

            var raw = await GetRequestResponse();
            if (string.IsNullOrEmpty(raw))
                return new List<TopAnimeData>();

            var doc = new HtmlDocument();
            doc.LoadHtml(raw);
            var topNodes = doc.DocumentNode.Descendants("table").FirstOrDefault(node =>
                node.Attributes.Contains("class") && node.Attributes["class"].Value == "top-ranking-table");

            if(topNodes == null)
                return new List<TopAnimeData>();

            var i = 50*_page;
            string imgUrlType = _isManga ? "manga/" : "anime/";
            foreach (var item in topNodes.Descendants("tr").Where(node => node.Attributes.Contains("class") && node.Attributes["class"].Value == "ranking-list"))
            {
                try
                {
                    var current = new TopAnimeData();
                    var epsText = item.Descendants("div").First(node => node.Attributes.Contains("class") && node.Attributes["class"].Value == "information di-ib mt4").ChildNodes[0].InnerText;
                    epsText = epsText.Substring(epsText.IndexOf('(') + 1);
                    epsText = epsText.Substring(0, epsText.IndexOf(' '));
                    current.Episodes = epsText;
                    //var img = item.Descendants("img").First().Attributes["data-src"].Value.Split('/');
                    var img = item.Descendants("img").First().Attributes["data-srcset"].Value;
                    img = img.Split(',').Last();
                    img = img.Substring(0, img.Length - 3);
                    var imgParts = img.Split('/');
                    int imgCount = imgParts.Length;
                    var imgurl = imgParts[imgCount - 2] + "/" + imgParts[imgCount - 1];
                    var pos = imgurl.IndexOf('?');
                    if (pos != -1)
                        imgurl = imgurl.Substring(0, pos);
                    current.ImgUrl = "https://cdn.myanimelist.net/images/" + imgUrlType + imgurl;
                    var titleNode = item.Descendants("h3").First().Descendants("a").First();
                        //.First(node => node.Attributes.Contains("class") && node.Attributes["class"].Value == (_type != TopAnimeType.Manga  ? "hoverinfo_trigger fl-l fs14 fw-b" : "hoverinfo_trigger fs14 fw-b"));
                    current.Title = WebUtility.HtmlDecode(titleNode.InnerText).Trim();
                    current.Id = Convert.ToInt32(titleNode.Attributes["href"].Value.Substring(8).Split('/')[2]);
                    try
                    {
                        current.Score = float.Parse(item.Descendants("span").First(node => node.Attributes.Contains("class") && node.Attributes["class"].Value == "text on").InnerText.Trim());
                    }
                    catch (Exception)
                    {
                        current.Score = 0; //sometimes score in unavailable -> upcoming for example
                    }
                    
                    current.Index = ++i;


                    output.Add(current);
                }
                catch (Exception)
                {
                    //
                }
            }
            if (_page != 0)
                output = (_isManga ? _prevMangaQueriesCache[_mangaType] : _prevQueriesCache[_type])
                    .Concat(output)
                    .GroupBy(item => item.Id)
                    .Select(group => group.First())
                    .ToList();

            if (_isManga)
            {
                DataCache.SaveTopMangaData(output, _mangaType);
                _prevMangaQueriesCache[_mangaType] = output;
            }
            else
            {
                DataCache.SaveTopAnimeData(output, _type);
                _prevQueriesCache[_type] = output;
            }
            return output;
        }

        private async Task<List<TopAnimeData>> FetchFromTenraiAsync()
        {
            try
            {
                var api = _isManga ? "top/manga" : "top/anime";
                var query = _isManga ? GetMangaApiQuery(_mangaType) : GetAnimeApiQuery(_type);
                if (string.IsNullOrEmpty(query))
                    return null;

                var endpoint = $"{api}?{query}&page={Math.Max(_page + 1, 1)}";
                var data = await TenraiClient.GetDataAsync(endpoint);
                if (!data.TryGetProperty("data", out var items) || items.ValueKind != JsonValueKind.Array)
                    return null;

                var output = new List<TopAnimeData>();
                var topIndex = 50 * _page;
                foreach (var item in items.EnumerateArray())
                {
                    try
                    {
                        if (item.ValueKind != JsonValueKind.Object)
                            continue;
                        var malId = item.TryGetProperty("mal_id", out var idProp) && idProp.ValueKind == JsonValueKind.Number
                            ? idProp.GetInt32()
                            : 0;
                        if (malId <= 0 && !item.TryGetProperty("title", out _))
                            continue;

                        var current = new TopAnimeData();
                        current.Id = malId;
                        current.Title = WebUtility.HtmlDecode(
                            item.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String
                                ? titleProp.GetString()
                                : "");

                        if (item.TryGetProperty("images", out var images) && images.ValueKind == JsonValueKind.Object &&
                            images.TryGetProperty("jpg", out var jpg) && jpg.ValueKind == JsonValueKind.Object &&
                            jpg.TryGetProperty("image_url", out var imgProp) && imgProp.ValueKind == JsonValueKind.String)
                        {
                            current.ImgUrl = NormalizeImageUrl(imgProp.GetString());
                        }

                        if (item.TryGetProperty("score", out var scoreProp) && scoreProp.ValueKind == JsonValueKind.Number)
                            current.Score = (float)scoreProp.GetDouble();
                        else
                            current.Score = 0;

                        if (item.TryGetProperty("episodes", out var epsProp) && epsProp.ValueKind == JsonValueKind.Number)
                            current.Episodes = epsProp.GetInt32().ToString();

                        current.Index = ++topIndex;
                        output.Add(current);
                    }
                    catch (Exception)
                    {
                        // skip malformed entry
                    }
                }

                return output.Count > 0 ? output : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void MergeWithPreviousPage(List<TopAnimeData> output)
        {
            if (_page == 0)
                return;
            var previous = _isManga
                ? (_prevMangaQueriesCache.ContainsKey(_mangaType) ? _prevMangaQueriesCache[_mangaType] : null)
                : (_prevQueriesCache.ContainsKey(_type) ? _prevQueriesCache[_type] : null);
            if (previous == null || previous.Count == 0)
                return;
            output.InsertRange(0, previous);
            var deduped = output
                .GroupBy(item => item.Id)
                .Select(group => group.First())
                .ToList();
            output.Clear();
            output.AddRange(deduped);
        }

        private static string GetAnimeApiQuery(TopAnimeType type)
        {
            switch (type)
            {
                case TopAnimeType.General:
                    return "sfw";
                case TopAnimeType.Airing:
                    return "filter=airing&sfw";
                case TopAnimeType.Upcoming:
                    return "filter=upcoming&sfw";
                case TopAnimeType.Tv:
                    return "type=tv&sfw";
                case TopAnimeType.Movies:
                    return "type=movie&sfw";
                case TopAnimeType.Ovas:
                    return "type=ova&sfw";
                case TopAnimeType.Popular:
                    return "filter=bypopularity&sfw";
                case TopAnimeType.Favourited:
                    return "filter=favorite&sfw";
                default:
                    return null;
            }
        }

        private static string GetMangaApiQuery(MangaTopType type)
        {
            switch (type)
            {
                case MangaTopType.All:
                    return "sfw";
                case MangaTopType.Manga:
                    return "type=manga&sfw";
                case MangaTopType.Novels:
                    return "type=novel&sfw";
                case MangaTopType.LightNovels:
                    return "type=lightnovel&sfw";
                case MangaTopType.OneShots:
                    return "type=oneshot&sfw";
                case MangaTopType.Doujinshi:
                    return "type=doujin&sfw";
                case MangaTopType.Manhwa:
                    return "type=manhwa&sfw";
                case MangaTopType.Manhua:
                    return "type=manhua&sfw";
                case MangaTopType.Popular:
                    return "filter=bypopularity&sfw";
                case MangaTopType.Favourited:
                    return "filter=favorite&sfw";
                default:
                    return null;
            }
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
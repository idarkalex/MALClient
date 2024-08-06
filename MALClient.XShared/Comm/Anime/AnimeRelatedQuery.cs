using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MALClient.Models.Enums;
using MALClient.Models.Models.AnimeScrapped;
using MALClient.XShared.Utils;

namespace MALClient.XShared.Comm.Anime
{
    public class AnimeRelatedQuery : Query
    {
        private readonly int _animeId;
        private readonly bool _animeMode;

        public AnimeRelatedQuery(int id, bool anime = true)
        {
            Request =
                WebRequest.Create(Uri.EscapeUriString($"https://myanimelist.net/{(anime ? "anime" : "manga")}/{id}/"));
            Request.ContentType = "application/x-www-form-urlencoded";
            Request.Method = "GET";
            _animeId = id;
            _animeMode = anime;
        }

        public async Task<List<RelatedAnimeData>> GetRelatedAnime(bool force = false)
        {
            var output = force
                ? new List<RelatedAnimeData>()
                : await DataCache.RetrieveRelatedAnimeData(_animeId, _animeMode) ?? new List<RelatedAnimeData>();
            if (output.Count != 0) return output;

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
            catch (Exception)
            {
                //no recom
            }
            DataCache.SaveRelatedAnimeData(_animeId, output, _animeMode);

            return output;
        }
    }
}
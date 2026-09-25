using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MALClient.Models.Enums;
using MALClient.Models.Models.Forums;
using MALClient.XShared.Utils;

namespace MALClient.XShared.Comm.Forums
{
    public class ForumBoardTopicsQuery : Query
    {
        private static readonly Dictionary<ForumBoards, Dictionary<int, ForumBoardContent>> _boardCache =
            new Dictionary<ForumBoards, Dictionary<int, ForumBoardContent>>();

        private static readonly Dictionary<int, Dictionary<int, ForumBoardContent>> _animeBoardCache =
            new Dictionary<int, Dictionary<int, ForumBoardContent>>();

        private static readonly Dictionary<string, Dictionary<int, ForumBoardContent>> _clubBoardCache =
            new Dictionary<string, Dictionary<int, ForumBoardContent>>();

        private ForumBoards _board;
        private int _animeId;
        private readonly string _clubId;
        private int _page;

        /// <summary>
        ///
        /// </summary>
        /// <param name="board"></param>
        /// <param name="page">From 0</param>
        public ForumBoardTopicsQuery(ForumBoards board,int page)
        {
            Request =
                new Uri(Uri.EscapeUriString($"https://myanimelist.net/forum/{GetEndpoint(board)}&show={page*50}"));
            _board = board;
            _page = page;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="animeId"></param>
        /// <param name="page">From 0</param>
        public ForumBoardTopicsQuery(int animeId,int page,bool anime)
        {
            Request =
                new Uri(Uri.EscapeUriString($"https://myanimelist.net/forum/?{(anime ? "anime" : "manga")}id={animeId}&show={page*50}"));
            _animeId = animeId;
            _page = page;
        }

        public ForumBoardTopicsQuery(string clubId,int page)
        {
            Request =
                new Uri(Uri.EscapeUriString($"https://myanimelist.net/forum/?clubid={clubId}&show={page*50}"));
            _clubId = clubId;
            _page = page;
        }



        public async Task<ForumBoardContent> GetTopicPosts(int? lastPage,bool force = false)
        {
            if(!force)
                try
                {
                    if (_clubId == null)
                    {
                        if (_animeId == 0)
                        {
                            if (_boardCache.ContainsKey(_board) && _boardCache[_board].ContainsKey(_page))
                                return _boardCache[_board][_page];
                        }
                        else
                        {
                            if (_animeBoardCache.ContainsKey(_animeId) && _animeBoardCache[_animeId].ContainsKey(_page))
                                return _animeBoardCache[_animeId][_page];
                        }
                    }
                    else
                    {
                        if (_clubBoardCache.ContainsKey(_clubId) && _clubBoardCache[_clubId].ContainsKey(_page))
                            return _clubBoardCache[_clubId][_page];
                    }

                }
                catch (Exception e)
                {
                    //
                }
            else //clear all pages
            {
                if (_clubId == null)
                {
                    if (_animeId == 0)
                    {
                        if (_boardCache.ContainsKey(_board))
                            _boardCache[_board] = new Dictionary<int, ForumBoardContent>();
                    }
                    else
                    {
                        if (_animeBoardCache.ContainsKey(_animeId))
                            _animeBoardCache[_animeId] = new Dictionary<int, ForumBoardContent>();
                    }
                }
                else
                {
                    if (_clubBoardCache.ContainsKey(_clubId))
                        _clubBoardCache[_clubId] = new Dictionary<int, ForumBoardContent>();
                }

            }



            var output = new ForumBoardContent();
            var raw = await GetAnonymousRequestResponse(Request);
            if (string.IsNullOrEmpty(raw))
            {
                global::System.Diagnostics.Debug.WriteLine(
                    $"MALPLUS forum board EMPTY: url={Request} board={_board} animeId={_animeId} club={_clubId} page={_page}");
                return new ForumBoardContent();
            }
            var doc = new HtmlDocument();
            doc.LoadHtml(raw);

            var topicRows = doc.DocumentNode.Descendants("tr")
                .Where(node => node.Attributes.Contains("data-topic-id"))
                .ToList();
            if (topicRows.Count == 0)
            {
                var topicContainer = doc.DocumentNode.Descendants("table")
                    .FirstOrDefault(node => node.Attributes.Contains("id") &&
                                             node.Attributes["id"].Value == "forumTopics");
                if (topicContainer != null)
                    topicRows = topicContainer.Descendants("tr").Skip(1).ToList();
            }

            global::System.Diagnostics.Debug.WriteLine(
                $"MALPLUS forum: url={Request} len={raw.Length} tables={doc.DocumentNode.Descendants("table").Count()} rows={topicRows.Count} hasForumTopics={raw.Contains("forumTopics")}");

            try
            {
                output.Pages = lastPage ?? ReadPageCount(doc);

                var failed = 0;
                foreach (var topicRow in topicRows)
                {
                    try
                    {
                        output.ForumTopicEntries.Add(ParseHtmlToTopic(topicRow));
                    }
                    catch (Exception ex)
                    {
                        // One malformed row used to vanish silently and, with every row
                        // failing, the board rendered as "No topics" with no clue why.
                        failed++;
                        if (failed <= 2)
                            global::System.Diagnostics.Debug.WriteLine(
                                $"MALPLUS forum row parse failed: {ex.GetType().Name} {ex.Message}");
                    }

                }
                global::System.Diagnostics.Debug.WriteLine(
                    $"MALPLUS forum parsed {output.ForumTopicEntries.Count}/{topicRows.Count} pages={output.Pages} (failed {failed})");
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine(
                    $"MALPLUS forum parse failed: url={Request} {ex.GetType().Name} {ex.Message}");
            }

            if (output.ForumTopicEntries.Count == 0)
                return output; // never cache an empty result

            CacheOutput(output);
            return output;
        }

        /// <summary>
        ///     Reads the page count from the board pager. The pager is the row of
        ///     "show=" links; the first span.di-ib in the document is the breadcrumb,
        ///     which is why this used to always yield 0 pages.
        /// </summary>
        private static int ReadPageCount(HtmlDocument doc)
        {
            var shows = doc.DocumentNode.Descendants("a")
                .Select(a => a.Attributes["href"]?.Value)
                .Where(h => !string.IsNullOrEmpty(h) && h.Contains("show="))
                .Select(h =>
                {
                    var idx = h.IndexOf("show=", StringComparison.Ordinal);
                    return int.TryParse(h.Substring(idx + 5).Split('&')[0], out var v) ? (int?)v : null;
                })
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();
            if (shows.Count == 0)
                return 0;
            var max = shows.Max();
            return max / 50;
        }

        private void CacheOutput(ForumBoardContent output)
        {
            if (_clubId != null)
            {
                if (!_clubBoardCache.ContainsKey(_clubId))
                    _clubBoardCache[_clubId] = new Dictionary<int, ForumBoardContent>();
                _clubBoardCache[_clubId][_page] = output;
            }
            else if (_animeId == 0)
            {
                if (!_boardCache.ContainsKey(_board))
                    _boardCache[_board] = new Dictionary<int, ForumBoardContent>();
                _boardCache[_board][_page] = output;
            }
            else
            {
                if (!_animeBoardCache.ContainsKey(_animeId))
                    _animeBoardCache[_animeId] = new Dictionary<int, ForumBoardContent>();
                _animeBoardCache[_animeId][_page] = output;
            }
        }

        public static ForumTopicEntry ParseHtmlToTopic(HtmlNode topicRow,int tdOffset = 0)
        {
            var current = new ForumTopicEntry();
            var tds = topicRow.Descendants("td").ToList();
            // Guard the shape instead of trusting it: a short row used to throw and
            // take the whole board down to "No topics".
            if (tds.Count < 4 + tdOffset)
                throw new ArgumentOutOfRangeException(nameof(tds), $"row has {tds.Count} cells");

            current.Type = tds[1].ChildNodes.Count > 0 ? tds[1].ChildNodes[0].InnerText : string.Empty;

            var titleLinks = tds[1].Descendants("a")
                .Where(a => !string.IsNullOrEmpty(a.InnerText))
                .ToList();
            if (titleLinks.Count == 0)
                throw new InvalidOperationException("row has no title link");
            var titleLink = titleLinks[0];

            current.Title = WebUtility.HtmlDecode(titleLink.InnerText);
            var link = titleLink.Attributes["href"]?.Value;
            if (string.IsNullOrEmpty(link))
                throw new InvalidOperationException("title link has no href");
            if (link.Contains("&goto="))
            {
                var pos = link.IndexOf("&goto=");
                link = link.Substring(0, pos);
            }

            current.Id = link.Split('=').Last();


            var spans = tds[1].Descendants("span").Where(node => !string.IsNullOrEmpty(node.InnerText)).ToList();
            current.Op = spans.Count > 0 ? spans[0].InnerText : string.Empty;
            current.Created = spans.Count > 1 ? spans[1].InnerText : string.Empty;

            current.Replies = tds[2 + tdOffset].InnerText;

            var lastCell = tds[3 + tdOffset];
            var lastLinks = lastCell.Descendants("a").Where(a => !string.IsNullOrEmpty(a.InnerText)).ToList();
            current.LastPoster = lastLinks.Count > 0 ? lastLinks[0].InnerText : string.Empty;
            current.LastPostDate = lastCell.ChildNodes.Count > 0
                ? lastCell.ChildNodes.Last().InnerText
                : string.Empty;

            return current;
        }

        private static string GetEndpoint(ForumBoards board)
        {
            if (board == ForumBoards.AnimeSeriesDisc || board == ForumBoards.MangaSeriesDisc)
                return $"?subboard={(int) board - 100}"; //100 is offset to differentiate from other boards
            return $"?board={(int) board}";
        }
    }
}

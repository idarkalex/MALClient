using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;

namespace MALClient.XShared.Comm.Discovery
{
    /// <summary>
    /// "Because you liked X": pick a finished, well scored title from the user's own list,
    /// ask Tenrai what people recommended alongside it, and drop anything the user already
    /// has. Two calls at most, both cached by TenraiClient.
    /// </summary>
    public static class BecauseYouLikedQuery
    {
        public const int MinAnchorScore = 8;

        public static async Task<Tuple<string, List<DiscoveryItem>>> GetAsync(
            IReadOnlyList<Tuple<int, string, int>> completedAnime, HashSet<int> alreadyInLibrary)
        {
            var empty = Tuple.Create("", new List<DiscoveryItem>());
            if (completedAnime == null || completedAnime.Count == 0)
                return empty;

            // Rotate deterministically per call so the anchor changes between openings
            // instead of pinning the first qualifying title forever.
            var index = Math.Abs(Environment.TickCount ^ completedAnime.Count.GetHashCode()) % completedAnime.Count;
            var anchor = completedAnime[index];
            if (anchor == null)
                return empty;

            var output = new List<DiscoveryItem>();
            try
            {
                var raw = await TenraiClient.GetRawJsonAsync($"anime/{anchor.Item1}/recommendations");
                if (string.IsNullOrEmpty(raw))
                    return empty;
                using var doc = JsonDocument.Parse(raw);
                if (!doc.RootElement.TryGetProperty("data", out var data) ||
                    data.ValueKind != JsonValueKind.Array)
                    return empty;

                var scored = new List<Tuple<int, DiscoveryItem>>();
                foreach (var entry in data.EnumerateArray())
                {
                    try
                    {
                        if (!entry.TryGetProperty("entry", out var inner))
                            continue;
                        var item = DiscoveryRandomQuery.ParseCatalogEntry(inner, false);
                        if (item == null)
                            continue;
                        if (alreadyInLibrary != null && alreadyInLibrary.Contains(item.Id))
                            continue;
                        if (string.IsNullOrEmpty(item.ImgUrl) || string.IsNullOrEmpty(item.Title))
                            continue;
                        var votes = entry.TryGetProperty("votes", out var v) && v.ValueKind == JsonValueKind.Number
                            ? v.GetInt32()
                            : 0;
                        scored.Add(Tuple.Create(votes, item));
                    }
                    catch
                    {
                    }
                }

                output = scored
                    .OrderByDescending(entry => entry.Item1)
                    .Select(entry => entry.Item2)
                    .Take(20)
                    .ToList();
            }
            catch
            {
            }

            if (output.Count == 0)
                return empty;
            return Tuple.Create(anchor.Item2, output);
        }
    }

    /// <summary>
    /// OP/ED of what people are watching right now: the top airing titles, then their themes
    /// through AnimeThemes. Tenrai has no season-wide themes endpoint, so this is one extra
    /// request per title and everything lands in the normal AnimeThemes cache.
    /// </summary>
    public static class SeasonThemesQuery
    {
        public static async Task<List<DiscoveryItem>> GetAsync(int titlesToScan = 10, int themesPerTitle = 2)
        {
            var output = new List<DiscoveryItem>();
            try
            {
                var airing = await DiscoveryRandomQuery.GetTopAiringAsync(titlesToScan);
                if (airing.Count == 0)
                    return output;

                var englishTitles = ResourceLocator.EnglishTitlesProvider;
                var tasks = airing.Select(async entry =>
                {
                    try
                    {
                        englishTitles.TryGetEnglishTitleForSeries(entry.Id, true, out var english);
                        var themes = await AnimeThemesHelper.SearchAsync(entry.Title, english);
                        if (themes == null || themes.Count == 0)
                            return;
                        var picked = themes
                            .Where(t => !string.IsNullOrEmpty(t?.Url))
                            .OrderBy(t => t.Sequence)
                            .ThenBy(t => t.Type)
                            .Take(themesPerTitle)
                            .ToList();
                        foreach (var theme in picked)
                            output.Add(new DiscoveryItem
                            {
                                Id = entry.Id,
                                Title = entry.Title,
                                ImgUrl = entry.ImgUrl,
                                Subtitle = theme.Type + " " + theme.Sequence +
                                           (string.IsNullOrEmpty(theme.SongTitle) ? "" : " · " + theme.SongTitle),
                                ThemeUrl = theme.Url
                            });
                    }
                    catch
                    {
                    }
                }).ToArray();

                await Task.WhenAll(tasks);
            }
            catch
            {
            }
            return output;
        }
    }
}

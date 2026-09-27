using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MALClient.Models.Models.Anime;
using MALClient.XShared.Utils;

namespace MALClient.XShared.Comm.Anime
{
    /// <summary>
    /// MAL only creates an episode page once the episode is well past, so an airing cour can
    /// be missing its last one or two rows even though they already aired and already have a
    /// discussion thread. This reads the per-anime forum index and maps "Episode N
    /// Discussion" back to its topic id, so those rows still open the real thread.
    /// </summary>
    public class AnimeEpisodeForumQuery : Query
    {
        private readonly int _animeId;
        private readonly bool _animeMode;

        public AnimeEpisodeForumQuery(int animeId, bool animeMode)
        {
            _animeId = animeId;
            _animeMode = animeMode;
            Request = new Uri($"https://myanimelist.net/{(animeMode ? "anime" : "manga")}/{animeId}/forum");
        }

        private static readonly Regex TopicPattern = new Regex(
            "topicid=(\\d+)[^>]*>([^<]{0,140}?Episode\\s*(\\d+)[^<]{0,60}?)<",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public async Task<Dictionary<int, string>> GetEpisodeTopicsAsync()
        {
            var result = new Dictionary<int, string>();
            if (_animeId <= 0)
                return result;
            try
            {
                var html = await GetRequestResponse();
                if (string.IsNullOrEmpty(html))
                    return result;
                foreach (Match match in TopicPattern.Matches(html))
                {
                    if (!int.TryParse(match.Groups[1].Value, out var topicId) || topicId <= 0)
                        continue;
                    if (!int.TryParse(match.Groups[3].Value, out var episodeNumber) || episodeNumber <= 0)
                        continue;
                    if (!result.ContainsKey(episodeNumber))
                        result[episodeNumber] = $"https://myanimelist.net/forum/?topicid={topicId}";
                }
            }
            catch
            {
                // A missing forum index only costs us the thread link, never the row.
            }
            return result;
        }
    }

    /// <summary>
    /// Projects the episode rows MAL has not created yet. Uses the cadence of the real rows
    /// (weekly for a TV cour) to work out how many slots have already passed, and never goes
    /// past the announced total.
    /// </summary>
    public static class AnimeEpisodeBackfill
    {
        public static List<AnimeEpisode> Build(IList<AnimeEpisode> existing, int announcedTotal,
            DateTime now, int provenHighestEpisode = 0)
        {
            var output = new List<AnimeEpisode>();
            if (existing == null || existing.Count == 0)
                return output;

            var dated = existing.Where(ep => ep.AiredDate.HasValue).OrderBy(ep => ep.EpisodeId).ToList();
            if (dated.Count < 2)
                return output;

            var cadence = MedianGapDays(dated);
            if (cadence <= 0)
                return output;

            var last = dated[dated.Count - 1];
            var nextDate = last.AiredDate.Value + TimeSpan.FromDays(cadence);
            var nextNumber = last.EpisodeId + 1;
            // Only the announced total bounds this: a cour that MAL says is 10 long must not
            // grow rows past 10 just because the date arithmetic says so.
            var ceiling = announcedTotal > 0 ? announcedTotal : long.MaxValue;
            // A discussion thread is proof the episode is real even when MAL has not created
            // its page. A cour that paused for a week breaks the cadence, so when the forum
            // tells us where the show actually is we trust it over the arithmetic.
            if (provenHighestEpisode > 0 && provenHighestEpisode < ceiling)
                ceiling = provenHighestEpisode;

            while (nextDate <= now && nextNumber <= ceiling)
            {
                output.Add(new AnimeEpisode
                {
                    EpisodeId = nextNumber,
                    AiredDate = nextDate,
                    Title = "",
                    IsEstimated = true
                });
                nextDate = nextDate.AddDays(cadence);
                nextNumber++;
            }
            return output;
        }

        private static double MedianGapDays(List<AnimeEpisode> dated)
        {
            var gaps = new List<double>();
            for (var i = 1; i < dated.Count; i++)
            {
                var gap = (dated[i].AiredDate.Value - dated[i - 1].AiredDate.Value).TotalDays;
                if (gap > 0 && gap < 400)
                    gaps.Add(gap);
            }
            if (gaps.Count == 0)
                return 7;
            gaps.Sort();
            return gaps[gaps.Count / 2];
        }
    }
}

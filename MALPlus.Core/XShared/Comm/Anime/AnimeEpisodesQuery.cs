using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AnimeEpisode = MALClient.Models.Models.Anime.AnimeEpisode;

namespace MALClient.XShared.Comm.Anime
{
    public class AnimeEpisodesQuery : Query
    {
        private readonly Dictionary<int, (List<AnimeEpisode> data, DateTime fetchedAt, int lastPage)> _cache = new Dictionary<int, (List<AnimeEpisode>, DateTime, int)>();
        private static bool IsAiringForTtl(int animeId)
        {
            if (MALClient.XShared.Utils.DataCache.TryRetrieveDataForId(animeId, out var vd) && !string.IsNullOrEmpty(vd.LastKnownStatus))
                return MALClient.XShared.Utils.AirTimeUtils.IsCurrentlyAiringStatus(vd.LastKnownStatus);
            return false;
        }

        /// <param name="onFirstBatch">
        ///     Invoked with the first page as soon as it lands, while the remaining pages are
        ///     still being fetched. A long-runner has a dozen pages, so waiting for all of them
        ///     meant the tab stayed empty for seconds; this lets the caller show the newest
        ///     episodes immediately and fill the rest in as it arrives.
        /// </param>
        public async Task<List<AnimeEpisode>> GetEpisodes(int animeId, bool force = false,
            CancellationToken cancellationToken = default, Action<List<AnimeEpisode>> onFirstBatch = null)
        {
            if (!force && _cache.TryGetValue(animeId, out var cachedFull))
            {
                var ttl = IsAiringForTtl(animeId) ? TimeSpan.FromHours(1) : TimeSpan.FromDays(7);
                if (DateTime.UtcNow - cachedFull.fetchedAt < ttl)
                    return cachedFull.data;
            }

            try
            {
                var result = new List<AnimeEpisode>();
                int page = 1;
                while (true)
                {
                    var attempt = 0;
                    try
                    {
                        var (items, hasNext) = await TenraiClient.GetPaginatedAsync($"anime/{animeId}/episodes?page={page}", cancellationToken);
                        foreach (var ep in items)
                        {
                            result.Add(new AnimeEpisode
                            {
                                EpisodeId = GetInt(ep, "mal_id"),
                                Filler = GetBool(ep, "filler"),
                                ForumUrl = GetString(ep, "forum_url"),
                                Recap = GetBool(ep, "recap"),
                                Title = GetString(ep, "title"),
                                TitleJapanese = GetString(ep, "title_japanese"),
                                TitleRomanji = GetString(ep, "title_romanji"),
                                VideoUrl = GetString(ep, "url"),
                                AiredDate = GetDateTime(ep, "aired"),
                            });
                        }

                        if (!hasNext)
                            break;

                        if (onFirstBatch != null && result.Count > 0)
                        {
                            try { onFirstBatch(result.ToList()); }
                            catch { }
                            onFirstBatch = null;
                        }

                        page++;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                        attempt++;
                        if (attempt >= 3)
                            return result;
                        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    }
                }

                _cache[animeId] = (result, DateTime.UtcNow, 1);
                NotifyOverrideIfResumed(animeId, result);
                return result;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Efficiently fetches only the most recent page of episodes (the ones with the
        /// latest airdates) so airing cadence/staleness can be computed without pulling
        /// the whole episode list for long-running series.
        /// </summary>
        public async Task<List<AnimeEpisode>> GetLastEpisodesAsync(int animeId)
        {
            if (_cache.TryGetValue(animeId, out var cached) && cached.data != null)
            {
                    var ttl = IsAiringForTtl(animeId) ? TimeSpan.FromHours(1) : TimeSpan.FromDays(7);
                if (DateTime.UtcNow - cached.fetchedAt < ttl)
                    return cached.data;
            }

            try
            {
                int lastPage = 1;
                List<JsonElement> items = null;
                var json = await TenraiClient.GetRawJsonAsync($"anime/{animeId}/episodes?page=1");
                using (var doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("data", out var data))
                    {
                        items = new List<JsonElement>();
                        foreach (var it in data.EnumerateArray())
                            items.Add(it.Clone());
                    }
                    if (root.TryGetProperty("pagination", out var pag) &&
                        pag.TryGetProperty("last_visible_page", out var lvp) &&
                        lvp.ValueKind == JsonValueKind.Number)
                        lastPage = lvp.GetInt32();
                }

                if (lastPage > 1)
                {
                    var (lastItems, _) = await TenraiClient.GetPaginatedAsync($"anime/{animeId}/episodes?page={lastPage}");
                    items = lastItems;
                }

                var result = new List<AnimeEpisode>();
                if (items != null)
                {
                    foreach (var ep in items)
                    {
                        result.Add(new AnimeEpisode
                        {
                            EpisodeId = GetInt(ep, "mal_id"),
                            Filler = GetBool(ep, "filler"),
                            ForumUrl = GetString(ep, "forum_url"),
                            Recap = GetBool(ep, "recap"),
                            Title = GetString(ep, "title"),
                            TitleJapanese = GetString(ep, "title_japanese"),
                            TitleRomanji = GetString(ep, "title_romanji"),
                            VideoUrl = GetString(ep, "url"),
                            AiredDate = GetDateTime(ep, "aired"),
                        });
                    }
                }

                _cache[animeId] = (result, DateTime.UtcNow, lastPage);
                NotifyOverrideIfResumed(animeId, result);
                return result;
            }
            catch
            {
                return null;
            }
        }

        //Every real episode fetch funnels through here, so a series that was flagged as
        //not airing by hand gets its flag dropped the moment a newer episode shows up.
        //No-op for every anime that has no override.
        private static void NotifyOverrideIfResumed(int animeId, List<AnimeEpisode> episodes)
        {
            try
            {
                if (episodes == null || episodes.Count == 0) return;
                if (!MALClient.XShared.Utils.AiringOverrides.IsOverriddenAsNotAiring(animeId)) return;
                var latest = episodes.Where(ep => ep.AiredDate.HasValue)
                    .Select(ep => ep.AiredDate.Value)
                    .OrderByDescending(d => d)
                    .FirstOrDefault();
                if (latest == default) return;
                MALClient.XShared.Utils.AiringOverrides.ResumeIfAiring(animeId, latest);
            }
            catch { }
        }

        private static string GetString(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : "";

        private static int GetInt(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : 0;

        private static bool GetBool(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.True;

        private static DateTime? GetDateTime(JsonElement el, string prop)
        {
            if (el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String)
            {
                if (DateTime.TryParse(p.GetString(), out var result))
                    return result;
            }
            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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

        public async Task<List<AnimeEpisode>> GetEpisodes(int animeId, bool force = false)
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
                    try
                    {
                        var (items, hasNext) = await TenraiClient.GetPaginatedAsync($"anime/{animeId}/episodes?page={page}");
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

                        page++;
                    }
                    catch
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1));
                    }
                }

                _cache[animeId] = (result, DateTime.UtcNow, 1);
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
                var isExpired = DateTime.UtcNow - cached.fetchedAt >= ttl;
                if (!isExpired)
                {
                    try
                    {
                        var probeJson = await TenraiClient.GetRawJsonAsync($"anime/{animeId}/episodes?page=1");
                        using var probeDoc = JsonDocument.Parse(probeJson);
                        var probeRoot = probeDoc.RootElement;
                        int probeLastPage = 1;
                        int probeCount = 0;
                        if (probeRoot.TryGetProperty("pagination", out var pp) && pp.TryGetProperty("last_visible_page", out var pl) && pl.ValueKind == JsonValueKind.Number)
                            probeLastPage = pl.GetInt32();
                        if (probeRoot.TryGetProperty("data", out var pdata) && pdata.ValueKind == JsonValueKind.Array)
                            probeCount = pdata.GetArrayLength();
                        if (probeLastPage == cached.lastPage && probeCount == cached.data.Count)
                            return cached.data;
                    }
                    catch { return cached.data; }
                }
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
                return result;
            }
            catch
            {
                return null;
            }
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

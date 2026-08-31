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
        private readonly Dictionary<int, List<AnimeEpisode>> _cache = new Dictionary<int, List<AnimeEpisode>>();

        public async Task<List<AnimeEpisode>> GetEpisodes(int animeId, bool force = false)
        {
            if (_cache.ContainsKey(animeId) && !force)
                return _cache[animeId];

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

                _cache[animeId] = result;
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
            if (_cache.ContainsKey(animeId))
                return _cache[animeId];

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

                _cache[animeId] = result;
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

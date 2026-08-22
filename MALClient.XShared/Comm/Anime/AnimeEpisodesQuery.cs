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
                            });
                        }

                        if (!hasNext)
                            break;

                        page++;
                        if (page > 10)
                            break;
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

        private static string GetString(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : "";

        private static int GetInt(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : 0;

        private static bool GetBool(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.True;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using MALClient.Models.Enums;
using MALClient.Models.Models.AnimeScrapped;
using MALClient.XShared.Utils;

namespace MALClient.XShared.Comm.Anime
{
    public class AnimeGenreStudioQuery : Query
    {
        private readonly AnimeStudios _studio;
        private readonly int _page;
        private readonly AnimeGenreSearch _genre;
        private readonly bool _genreMode;

        public AnimeGenreStudioQuery(AnimeGenreSearch genre, int page = 1)
        {
            _genre = genre;
            _page = page;
            _genreMode = true;
        }

        public AnimeGenreStudioQuery(AnimeStudios studio, int page = 1)
        {
            _studio = studio;
            _page = page;
            _genreMode = false;
        }

        public async Task<List<SeasonalAnimeData>> GetAnime()
        {
            var cacheKey = _genreMode ? $"genre_{_genre}_{_page}" : $"studio_{_studio}_{_page}";
            var cacheRegion = _genreMode ? "AnimesByGenre" : "AnimesByStudio";
            var output = await DataCache.RetrieveData<List<SeasonalAnimeData>>(cacheKey, cacheRegion, 1)
                         ?? new List<SeasonalAnimeData>();
            if (output.Count > 0)
                return output;

            try
            {
                var endpoint = _genreMode
                    ? $"anime?genres={(int)_genre}&page={_page}&order_by=score&sort=desc&sfw"
                    : $"anime?producers={(int)_studio}&page={_page}&order_by=score&sort=desc&sfw";

                var (items, _) = await TenraiClient.GetPaginatedAsync(endpoint);

                int index = (_page - 1) * 25 + 1;
                foreach (var entry in items)
                {
                    output.Add(new SeasonalAnimeData
                    {
                        Title = GetString(entry, "title"),
                        Id = GetInt(entry, "mal_id"),
                        ImgUrl = GetNestedString(entry, "images", "jpg", "image_url"),
                        Episodes = GetInt(entry, "episodes").ToString(),
                        Score = (float)GetDouble(entry, "score"),
                        Genres = GetGenreNames(entry),
                        Index = index++
                    });
                }
            }
            catch
            {
                return output;
            }

            if (output.Count > 0)
                DataCache.SaveData(output, cacheKey, cacheRegion);
            else if (!_genreMode)
            {
                // Studio produced no results via producers filter - try text search as fallback
                try
                {
                    var studioName = _studio.GetDescription();
                    var fallbackEndpoint = $"anime?q={Uri.EscapeDataString(studioName)}&order_by=score&sort=desc&sfw";
                    var (fbItems, _) = await TenraiClient.GetPaginatedAsync(fallbackEndpoint);
                    int fbIndex = 1;
                    foreach (var entry in fbItems.Take(25))
                    {
                        output.Add(new SeasonalAnimeData
                        {
                            Title = GetString(entry, "title"),
                            Id = GetInt(entry, "mal_id"),
                            ImgUrl = GetNestedString(entry, "images", "jpg", "image_url"),
                            Episodes = GetInt(entry, "episodes").ToString(),
                            Score = (float)GetDouble(entry, "score"),
                            Genres = GetGenreNames(entry),
                            Index = fbIndex++
                        });
                    }
                } catch { }
            }
            return output;
        }

        private static string GetString(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : "";

        private static int GetInt(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : 0;

        private static double GetDouble(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDouble() : 0;

        private static string GetNestedString(JsonElement el, params string[] props)
        {
            foreach (var prop in props.Take(props.Length - 1))
                if (!el.TryGetProperty(prop, out el)) return "";
            return GetString(el, props.Last());
        }

        private static List<string> GetGenreNames(JsonElement el)
        {
            if (!el.TryGetProperty("genres", out var genres) || genres.ValueKind != JsonValueKind.Array)
                return new List<string>();
            var list = new List<string>();
            foreach (var g in genres.EnumerateArray())
            {
                if (g.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                    list.Add(name.GetString());
            }
            return list;
        }
    }
}

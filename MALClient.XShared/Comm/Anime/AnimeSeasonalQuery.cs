using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MALClient.Models.Models.Anime;
using MALClient.Models.Models.AnimeScrapped;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;

namespace MALClient.XShared.Comm.Anime
{
    public class AnimeSeasonalQuery : Query
    {
        private readonly AnimeSeason _season;

        public AnimeSeasonalQuery(AnimeSeason season)
        {
            _season = season;
        }

        public async Task<List<SeasonalAnimeData>> GetSeasonalAnime(bool force = false)
        {
            var output = force
                ? new List<SeasonalAnimeData>()
                : await DataCache.RetrieveSeasonalData(_season.Name) ?? new List<SeasonalAnimeData>();

            if (output.Count != 0) return output;

            const int maxAttempts = 3;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var result = new List<SeasonalAnimeData>();
                try
                {
                    int currentPage = 1;
                    while (true)
                    {
                        var requestedYear = _season.Year != 0 ? _season.Year : DateTime.UtcNow.Year;
                        var requestedSeason = _season.Year != 0 ? SeasonEnumToString(_season.Season) : GetCurrentSeason();

                        var (items, hasNext) = await JikanClient.GetPaginatedAsync(
                            $"seasons/{requestedYear}/{requestedSeason}?page={currentPage}");

                        foreach (var entry in items)
                        {
                            var airDay = -1;
                            var airStartDate = "";
                            if (entry.TryGetProperty("broadcast", out var broadcast) &&
                                broadcast.TryGetProperty("day", out var dayProp) &&
                                dayProp.ValueKind == JsonValueKind.String)
                            {
                                airDay = DayOfWeekStringToInt(dayProp.GetString());
                            }
                            if (entry.TryGetProperty("aired", out var aired) &&
                                aired.TryGetProperty("from", out var fromProp) &&
                                fromProp.ValueKind == JsonValueKind.String)
                            {
                                var fromStr = fromProp.GetString();
                                if (DateTime.TryParse(fromStr, out var dt))
                                    airStartDate = dt.ToString("yyyy-MM-dd");
                            }

                            result.Add(new SeasonalAnimeData
                            {
                                Title = GetString(entry, "title"),
                                Id = GetInt(entry, "mal_id"),
                                ImgUrl = GetNestedString(entry, "images", "jpg", "image_url"),
                                Episodes = GetInt(entry, "episodes").ToString(),
                                Score = (float)GetDouble(entry, "score"),
                                Genres = GetGenreNames(entry),
                                Index = result.Count + 1,
                                AirDay = airDay,
                                AirStartDate = airStartDate,
                            });
                        }

                        if (!hasNext)
                            break;

                        await Task.Delay(TimeSpan.FromMilliseconds(500));
                        currentPage++;
                    }

                    DataCache.SaveSeasonalData(result, _season.Name);
                    return result;
                }
                catch (Exception e)
                {
                    if (attempt == maxAttempts)
                    {
                        ResourceLocator.ClipboardProvider.SetText($"[Seasonal] {_season.Name}\n{e}");
                        return result;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
                }
            }

            return output;
        }

        private static string GetCurrentSeason()
        {
            return DateTime.UtcNow.Month switch
            {
                <= 3 => "winter",
                <= 6 => "spring",
                <= 9 => "summer",
                _ => "fall"
            };
        }

        private static string SeasonEnumToString(JikanDotNet.Season season)
        {
            return season switch
            {
                JikanDotNet.Season.Winter => "winter",
                JikanDotNet.Season.Spring => "spring",
                JikanDotNet.Season.Summer => "summer",
                JikanDotNet.Season.Fall => "fall",
                _ => "fall"
            };
        }

        private static int DayOfWeekStringToInt(string day)
        {
            return day?.ToLowerInvariant() switch
            {
                "mondays" => 1,
                "tuesdays" => 2,
                "wednesdays" => 3,
                "thursdays" => 4,
                "fridays" => 5,
                "saturdays" => 6,
                "sundays" => 7,
                _ => -1
            };
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
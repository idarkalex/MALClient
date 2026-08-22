using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
            var output = new List<SeasonalAnimeData>();
            if (!force)
            {
                var cached = await DataCache.RetrieveSeasonalData(_season.Name);
                if (cached != null && cached.All(i => !string.IsNullOrEmpty(i.Title) && i.Id != 0))
                    output = cached;
            }

            if (output.Count != 0) return output;

            var requestedYear = _season.Year != 0 ? _season.Year : DateTime.UtcNow.Year;
            var requestedSeason = _season.Year != 0 ? SeasonEnumToString(_season.Season) : GetCurrentSeason();

            try
            {
                var official = await GetSeasonalFromOfficialMalApi(requestedYear, requestedSeason);
                if (official.Count != 0)
                {
                    DataCache.SaveSeasonalData(official, _season.Name);
                    return official;
                }
            }
            catch (Exception)
            {
            }

            const int maxAttempts = 3;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var result = new List<SeasonalAnimeData>();
                try
                {
                    int currentPage = 1;
                    while (true)
                    {
                        var (items, hasNext) = await TenraiClient.GetPaginatedAsync(
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

                        currentPage++;
                    }

                    DataCache.SaveSeasonalData(result, _season.Name);
                    return result;
                }
                catch (Exception)
                {
                    if (attempt == maxAttempts)
                        return result;

                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
                }
            }

            return output;
        }

        private static async Task<List<SeasonalAnimeData>> GetSeasonalFromOfficialMalApi(int year, string season)
        {
            var baseUrl =
                $"https://api.myanimelist.net/v2/anime/season/{year}/{season}" +
                "?sort=anime_num_list_users&limit=100&fields=id,title,main_picture,mean,media_type,num_episodes,genres,broadcast,start_date";

            var clients = new List<HttpClient>();
            if (!string.IsNullOrEmpty(Settings.RefreshToken))
            {
                try
                {
                    clients.Add(await ResourceLocator.MalHttpContextProvider.GetApiHttpContextAsync());
                }
                catch (Exception)
                {
                }
            }
            var anonClient = new HttpClient();
            anonClient.DefaultRequestHeaders.Add("X-MAL-CLIENT-ID", "183063f74126e7551b00c3b4de66986c");
            clients.Add(anonClient);

            foreach (var client in clients)
            {
                var result = new List<SeasonalAnimeData>();
                var offset = 0;
                while (true)
                {
                    try
                    {
                        using var response = await client.GetAsync($"{baseUrl}&offset={offset}");
                        if (!response.IsSuccessStatusCode)
                            break;

                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("data", out var dataArr) &&
                            dataArr.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in dataArr.EnumerateArray())
                            {
                                var entry = item;
                                if (item.TryGetProperty("node", out var node) &&
                                    node.ValueKind == JsonValueKind.Object)
                                    entry = node;
                                result.Add(ParseSeasonalEntry(entry, result.Count + 1));
                            }
                        }

                        var hasNext = root.TryGetProperty("paging", out var paging) &&
                                      paging.ValueKind == JsonValueKind.Object &&
                                      paging.TryGetProperty("next", out var next) &&
                                      next.ValueKind == JsonValueKind.String &&
                                      !string.IsNullOrEmpty(next.GetString());
                        if (!hasNext)
                            break;

                        offset += 100;
                    }
                    catch (Exception)
                    {
                        break;
                    }
                }

                if (result.Count != 0)
                    return result;
            }

            return new List<SeasonalAnimeData>();
        }

        private static SeasonalAnimeData ParseSeasonalEntry(JsonElement entry, int index)
        {
            var airDay = -1;
            var airStartDate = "";
            if (entry.TryGetProperty("broadcast", out var broadcast) &&
                broadcast.TryGetProperty("day_of_the_week", out var dayProp) &&
                dayProp.ValueKind == JsonValueKind.String)
            {
                airDay = dayProp.GetString()?.ToLowerInvariant() switch
                {
                    "monday" => 1,
                    "tuesday" => 2,
                    "wednesday" => 3,
                    "thursday" => 4,
                    "friday" => 5,
                    "saturday" => 6,
                    "sunday" => 7,
                    _ => -1
                };
            }
            if (entry.TryGetProperty("start_date", out var startProp) &&
                startProp.ValueKind == JsonValueKind.String)
            {
                var startStr = startProp.GetString();
                if (DateTime.TryParse(startStr, out var dt))
                    airStartDate = dt.ToString("yyyy-MM-dd");
            }

            var imgUrl = GetNestedString(entry, "main_picture", "large");
            if (string.IsNullOrEmpty(imgUrl))
                imgUrl = GetNestedString(entry, "main_picture", "medium");

            return new SeasonalAnimeData
            {
                Title = GetString(entry, "title"),
                Id = GetInt(entry, "id"),
                ImgUrl = imgUrl,
                Episodes = GetInt(entry, "num_episodes").ToString(),
                Score = (float)GetDouble(entry, "mean"),
                Genres = GetGenreNames(entry),
                Index = index,
                AirDay = airDay,
                AirStartDate = airStartDate,
            };
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

        private static string SeasonEnumToString(Season season)
        {
            return season switch
            {
                Season.Winter => "winter",
                Season.Spring => "spring",
                Season.Summer => "summer",
                Season.Fall => "fall",
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
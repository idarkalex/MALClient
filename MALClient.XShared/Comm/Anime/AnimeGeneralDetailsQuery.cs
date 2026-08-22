using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MALClient.Models.Enums;
using MALClient.Models.Models.Anime;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;

namespace MALClient.XShared.Comm.Anime
{
    public class AnimeGeneralDetailsQuery : Query
    {
        public async Task<AnimeGeneralDetailsData> GetAnimeDetails(bool force, string id, string title, bool animeMode,
            ApiType? apiOverride = null)
        {
            var output = force ? null : await DataCache.RetrieveAnimeSearchResultsData(id, animeMode);
            if (output != null)
                return output;

            try
            {
                output = await TryGetDetailsFromOfficialMalApi(id, animeMode);
                if (output != null)
                {
                    DataCache.SaveAnimeSearchResultsData(id, output, animeMode);
                    return output;
                }
            }
            catch (Exception)
            {
            }

            try
            {
                var data = await TenraiClient.GetDataAsync($"{(animeMode ? "anime" : "manga")}/{id}");

                if (animeMode)
                {
                    output = new AnimeGeneralDetailsData
                    {
                        AllEpisodes = GetInt(data, "episodes"),
                        Status = GetString(data, "status"),
                        Type = GetString(data, "type"),
                        AlternateTitle = GetString(data, "title_japanese"),
                        StartDate = ParseDateFromIso(GetNestedString(data, "aired", "from")),
                        EndDate = ParseDateFromIso(GetNestedString(data, "aired", "to")),
                        ImgUrl = GetNestedString(data, "images", "jpg", "image_url"),
                        GlobalScore = (float)GetDouble(data, "score"),
                        Id = GetInt(data, "mal_id"),
                        MalId = GetInt(data, "mal_id"),
                        Rank = GetInt(data, "rank"),
                        Popularity = GetInt(data, "popularity"),
                        Studios = GetNameList(data, "studios"),
                        Synopsis = WebUtility.HtmlDecode(GetString(data, "synopsis")),
                        Title = WebUtility.HtmlDecode(GetString(data, "title")),
                        Synonyms = GetStringList(data, "title_synonyms"),
                    };

                    if ((output.Type == "Movie" || output.AllEpisodes == 1) && output.EndDate == "N/A" &&
                        output.Status == "Finished Airing")
                    {
                        output.EndDate = output.StartDate;
                    }

                    ResourceLocator.EnglishTitlesProvider.AddOrUpdate(int.Parse(id), true,
                        GetString(data, "title_english"));
                }
                else
                {
                    output = new AnimeGeneralDetailsData
                    {
                        AllEpisodes = GetInt(data, "chapters"),
                        AllVolumes = GetInt(data, "volumes"),
                        Status = GetString(data, "status"),
                        Type = GetString(data, "type"),
                        AlternateTitle = GetString(data, "title_japanese"),
                        StartDate = ParseDateFromIso(GetNestedString(data, "published", "from")),
                        EndDate = ParseDateFromIso(GetNestedString(data, "published", "to")),
                        ImgUrl = GetNestedString(data, "images", "jpg", "image_url"),
                        GlobalScore = (float)GetDouble(data, "score"),
                        Id = GetInt(data, "mal_id"),
                        MalId = GetInt(data, "mal_id"),
                        Rank = GetInt(data, "rank"),
                        Popularity = GetInt(data, "popularity"),
                        Studios = GetNameList(data, "serializations"),
                        Synopsis = WebUtility.HtmlDecode(GetString(data, "synopsis")),
                        Title = WebUtility.HtmlDecode(GetString(data, "title")),
                        Synonyms = GetStringList(data, "title_synonyms"),
                    };

                    ResourceLocator.EnglishTitlesProvider.AddOrUpdate(int.Parse(id), false,
                        GetString(data, "title_english"));
                }

                DataCache.SaveAnimeSearchResultsData(id, output, animeMode);
            }
            catch (Exception)
            {
            }

            return output;
        }

        private static async Task<AnimeGeneralDetailsData> TryGetDetailsFromOfficialMalApi(string id, bool animeMode)
        {
            var endpoint = animeMode ? "anime" : "manga";
            var fields = animeMode
                ? "id,title,main_picture,alternative_titles,start_date,end_date,synopsis,mean,media_type,status,num_episodes,rank,popularity,num_list_users,num_favorites,start_season,studios{name}"
                : "id,title,main_picture,alternative_titles,start_date,end_date,synopsis,mean,media_type,status,num_chapters,num_volumes,rank,popularity,num_list_users,num_favorites,serializations{name}";
            var url = $"https://api.myanimelist.net/v2/{endpoint}/{id}?fields={fields}";

            try
            {
                var client = await ResourceLocator.MalHttpContextProvider.GetApiHttpContextAsync();
                using var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                    return ParseOfficialDetails(await response.Content.ReadAsStringAsync(), id, animeMode);
            }
            catch (Exception)
            {
            }

            try
            {
                using var anonClient = new HttpClient();
                anonClient.DefaultRequestHeaders.Add("X-MAL-CLIENT-ID", "183063f74126e7551b00c3b4de66986c");
                using var response = await anonClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                    return ParseOfficialDetails(await response.Content.ReadAsStringAsync(), id, animeMode);
            }
            catch (Exception)
            {
            }

            return null;
        }

        private static AnimeGeneralDetailsData ParseOfficialDetails(string json, string id, bool animeMode)
        {
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement;

            var imgUrl = GetNestedString(data, "main_picture", "large");
            if (string.IsNullOrEmpty(imgUrl))
                imgUrl = GetNestedString(data, "main_picture", "medium");

            var output = new AnimeGeneralDetailsData
            {
                AllEpisodes = animeMode ? GetInt(data, "num_episodes") : GetInt(data, "num_chapters"),
                AllVolumes = animeMode ? 0 : GetInt(data, "num_volumes"),
                Status = MapStatus(GetString(data, "status")),
                Type = MapMediaType(GetString(data, "media_type")),
                AlternateTitle = GetNestedString(data, "alternative_titles", "ja"),
                StartDate = ParseDateFromIso(GetString(data, "start_date")),
                EndDate = ParseDateFromIso(GetString(data, "end_date")),
                ImgUrl = imgUrl,
                GlobalScore = (float)GetDouble(data, "mean"),
                Id = GetInt(data, "id"),
                MalId = GetInt(data, "id"),
                Rank = GetInt(data, "rank"),
                Popularity = GetInt(data, "popularity"),
                Studios = GetNameList(data, animeMode ? "studios" : "serializations"),
                Synopsis = WebUtility.HtmlDecode(GetString(data, "synopsis")),
                Title = WebUtility.HtmlDecode(GetString(data, "title")),
                Synonyms = GetNestedStringList(data, "alternative_titles", "synonyms"),
            };

            var englishTitle = GetNestedString(data, "alternative_titles", "en");
            if (!string.IsNullOrEmpty(englishTitle))
                ResourceLocator.EnglishTitlesProvider.AddOrUpdate(int.Parse(id), animeMode, englishTitle);

            return output;
        }

        private static string MapStatus(string status) =>
            status switch
            {
                "currently_airing" => "Currently Airing",
                "finished_airing" => "Finished Airing",
                "not_yet_aired" => "Not yet aired",
                _ => status
            };

        private static string MapMediaType(string type) =>
            type switch
            {
                "tv" => "TV",
                "movie" => "Movie",
                "ova" => "OVA",
                "ona" => "ONA",
                "special" => "Special",
                "music" => "Music",
                "manga" => "Manga",
                "novel" => "Novel",
                "light_novel" => "Light Novel",
                "one_shot" => "One-shot",
                "doujinshi" => "Doujinshi",
                "manhwa" => "Manhwa",
                "manhua" => "Manhua",
                _ => type
            };

        private static List<string> GetNameList(JsonElement el, string prop)
        {
            if (!el.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return new List<string>();
            var list = new List<string>();
            foreach (var item in arr.EnumerateArray())
            {
                var name = GetString(item, "name");
                if (!string.IsNullOrEmpty(name))
                    list.Add(name);
            }
            return list;
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
            {
                if (!el.TryGetProperty(prop, out el)) return "";
            }
            return GetString(el, props.Last());
        }

        private static List<string> GetStringList(JsonElement el, string prop)
        {
            if (!el.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return new List<string>();
            var list = new List<string>();
            foreach (var item in arr.EnumerateArray())
                list.Add(item.GetString() ?? "");
            return list;
        }

        private static List<string> GetNestedStringList(JsonElement el, params string[] props)
        {
            foreach (var prop in props.Take(props.Length - 1))
            {
                if (!el.TryGetProperty(prop, out el)) return new List<string>();
            }
            return GetStringList(el, props.Last());
        }

        private static string ParseDateFromIso(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return "N/A";
            if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return "N/A";
        }
    }
}
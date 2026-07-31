using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
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
                var data = await JikanClient.GetDataAsync($"{(animeMode ? "anime" : "manga")}/{id}");

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
                        Synopsis = WebUtility.HtmlDecode(GetString(data, "synopsis")),
                        Title = WebUtility.HtmlDecode(GetString(data, "title")),
                        Synonyms = GetStringList(data, "title_synonyms"),
                    };

                    ResourceLocator.EnglishTitlesProvider.AddOrUpdate(int.Parse(id), false,
                        GetString(data, "title_english"));
                }

                DataCache.SaveAnimeSearchResultsData(id, output, animeMode);
            }
            catch (Exception e)
            {
                // ResourceLocator.ClipboardProvider.SetText($"{e}\n{response}");
                // ResourceLocator.SnackbarProvider.ShowText("Error copied to clipboard.");
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

        private static string ParseDateFromIso(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return "N/A";
            if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return "N/A";
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using MALClient.Models.Enums;
using MALClient.Models.Models.Anime;
using MALClient.XShared.Utils;

namespace MALClient.XShared.Comm.Anime
{
    public class AnimeSearchQuery : Query
    {
        private readonly string _query;

        public AnimeSearchQuery(string query, ApiType? apiOverride = null)
        {
            _query = query;
        }

        public async Task<List<AnimeGeneralDetailsData>> GetSearchResults()
        {
            var output = new List<AnimeGeneralDetailsData>();

            try
            {
                var (items, _) = await JikanClient.GetPaginatedAsync($"anime?q={Uri.EscapeDataString(_query)}&sfw");

                foreach (var result in items)
                {
                    output.Add(new AnimeGeneralDetailsData
                    {
                        Id = GetInt(result, "mal_id"),
                        AllEpisodes = GetInt(result, "episodes"),
                        Title = GetString(result, "title"),
                        ImgUrl = GetNestedString(result, "images", "jpg", "image_url"),
                        Type = GetString(result, "type"),
                        Synopsis = GetString(result, "synopsis"),
                        MalId = GetInt(result, "mal_id"),
                        GlobalScore = (float)GetDouble(result, "score"),
                        Status = GetBool(result, "airing") ? "Currently Airing" : "Unknown"
                    });
                }
            }
            catch
            {
                // fallthrough
            }

            return output;
        }

        private static string GetString(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : "";

        private static int GetInt(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : 0;

        private static double GetDouble(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDouble() : 0;

        private static bool GetBool(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.True;

        private static string GetNestedString(JsonElement el, params string[] props)
        {
            foreach (var prop in props.Take(props.Length - 1))
                if (!el.TryGetProperty(prop, out el)) return "";
            return GetString(el, props.Last());
        }
    }
}
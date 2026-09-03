using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using MALClient.Models.Models.Anime;
using MALClient.XShared.Utils;

namespace MALClient.XShared.Comm.Manga
{
    public class MangaSearchQuery : Query
    {
        private readonly string _query;

        public MangaSearchQuery(string query)
        {
            _query = query;
        }

        public async Task<List<AnimeGeneralDetailsData>> GetSearchResults()
        {
            var output = new List<AnimeGeneralDetailsData>();

            try
            {
                var (items, _) = await TenraiClient.GetPaginatedAsync($"manga?q={Uri.EscapeDataString(Utilities.CleanAnimeTitle(_query))}&sfw&order_by=popularity&sort=asc");

                foreach (var result in items)
                {
                    output.Add(new AnimeGeneralDetailsData
                    {
                        Id = GetInt(result, "mal_id"),
                        AllVolumes = GetInt(result, "volumes"),
                        Title = GetString(result, "title"),
                        ImgUrl = GetNestedString(result, "images", "jpg", "image_url"),
                        Type = GetString(result, "type"),
                        Synopsis = GetString(result, "synopsis"),
                        MalId = GetInt(result, "mal_id"),
                        GlobalScore = (float)GetDouble(result, "score"),
                        Status = "Unknown"
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

        private static string GetNestedString(JsonElement el, params string[] props)
        {
            foreach (var prop in props.Take(props.Length - 1))
                if (!el.TryGetProperty(prop, out el)) return "";
            return GetString(el, props.Last());
        }
    }
}
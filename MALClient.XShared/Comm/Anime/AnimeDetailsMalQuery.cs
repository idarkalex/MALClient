using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using MALClient.Models.Models.AnimeScrapped;
using MALClient.XShared.Utils;

namespace MALClient.XShared.Comm.Anime
{
    public class AnimeDetailsMalQuery : Query
    {
        private readonly int _id;
        private readonly bool _anime;

        public AnimeDetailsMalQuery(int id, bool anime)
        {
            _id = id;
            _anime = anime;
        }

        public async Task<AnimeScrappedDetails> GetDetails(bool force)
        {
            var possibleData = force ? null :
                await DataCache.RetrieveData<AnimeScrappedDetails>(_id.ToString(), "anime_details_scrapped", 14);
            if (possibleData != null)
                return possibleData;

            var output = new AnimeScrappedDetails { Id = _id };

            try
            {
                if (!_anime)
                {
                    DataCache.SaveData(output, _id.ToString(), "anime_details_scrapped");
                    return output;
                }

                var data = await TenraiClient.GetDataAsync($"anime/{_id}");

                var type = GetString(data, "type");
                var episodes = GetString(data, "episodes");
                var status = GetString(data, "status");
                var aired = "";
                if (data.TryGetProperty("aired", out var airedObj))
                {
                    var from = SanitizeDate(GetString(airedObj, "from"));
                    var to = SanitizeDate(GetString(airedObj, "to"));
                    if (!string.IsNullOrEmpty(from))
                    {
                        aired = from;
                        if (!string.IsNullOrEmpty(to))
                            aired += " to " + to;
                    }
                }
                var premiered = CapitalizeSeason(GetString(data, "season") ?? "");
                if (data.TryGetProperty("year", out var yearProp) && yearProp.ValueKind == JsonValueKind.Number)
                    premiered = $"{premiered} {yearProp.GetInt32()}".Trim();
                var broadcast = "";
                if (data.TryGetProperty("broadcast", out var bc))
                {
                    var day = GetString(bc, "day");
                    var time = GetString(bc, "time");
                    if (!string.IsNullOrEmpty(day))
                        broadcast = $"{day} at {time} (JST)";
                }
                var producers = GetNameList(data, "producers");
                var licensors = GetNameList(data, "licensors");
                var studios = GetNameList(data, "studios");
                var source = GetString(data, "source");
                var genres = GetNameList(data, "genres");
                var themes = GetNameList(data, "themes");
                var demographics = GetNameList(data, "demographics");
                var duration = GetString(data, "duration");
                var rating = GetString(data, "rating");
                var popularity = GetInt(data, "popularity");
                var scoredBy = GetInt(data, "scored_by");
                var rank = GetInt(data, "rank");
                var members = GetInt(data, "members");
                var favorites = GetInt(data, "favorites");

                var info = new List<string>();
                if (!string.IsNullOrEmpty(type)) info.Add($"Type: {type}");
                if (!string.IsNullOrEmpty(episodes) && episodes != "0") info.Add($"Episodes: {episodes}");
                if (!string.IsNullOrEmpty(status)) info.Add($"Status: {status}");
                if (!string.IsNullOrEmpty(aired)) info.Add($"Aired: {aired}");
                if (!string.IsNullOrEmpty(premiered)) info.Add($"Premiered: {premiered}");
                if (!string.IsNullOrEmpty(broadcast)) info.Add($"Broadcast: {broadcast}");
                if (producers.Count > 0) info.Add($"Producers: {string.Join(", ", producers)}");
                if (licensors.Count > 0) info.Add($"Licensors: {string.Join(", ", licensors)}");
                if (studios.Count > 0) info.Add($"Studios: {string.Join(", ", studios)}");
                if (!string.IsNullOrEmpty(source)) info.Add($"Source: {source}");
                if (genres.Count > 0) info.Add($"Genres: {string.Join(", ", genres)}");
                if (themes.Count > 0) info.Add($"Themes: {string.Join(", ", themes)}");
                if (demographics.Count > 0) info.Add($"Demographics: {string.Join(", ", demographics)}");
                if (!string.IsNullOrEmpty(duration)) info.Add($"Duration: {duration}");
                if (!string.IsNullOrEmpty(rating)) info.Add($"Rating: {rating}");
                output.Information.AddRange(info);

                var stats = new List<string>();
                var score = GetDouble(data, "score");
                if (score > 0) stats.Add($"Score: {score:N2} (scored by {scoredBy:N0} users)");
                if (rank > 0) stats.Add($"Rank: #{rank}");
                if (popularity > 0) stats.Add($"Popularity: #{popularity}");
                if (members > 0) stats.Add($"Members: {members:N0}");
                if (favorites > 0) stats.Add($"Favorites: {favorites:N0}");
                output.Statistics.AddRange(stats);

                output.AlternativeTitles.Add(GetString(data, "title_japanese"));
                var english = GetString(data, "title_english");
                if (!string.IsNullOrEmpty(english))
                    output.AlternativeTitles.Add(english);

                try
                {
                    var themesData = await TenraiClient.GetDataAsync($"anime/{_id}/themes");
                    if (themesData.TryGetProperty("openings", out var ops))
                    {
                        foreach (var op in ops.EnumerateArray())
                            output.Openings.Add(op.GetString() ?? "");
                    }
                    if (themesData.TryGetProperty("endings", out var eds))
                    {
                        foreach (var ed in eds.EnumerateArray())
                            output.Endings.Add(ed.GetString() ?? "");
                    }
                }
                catch
                {
                    // themes may not be available
                }
            }
            catch (Exception)
            {
                // fallthrough
            }

            if (output.Information.Count > 0 || output.Statistics.Count > 0)
                DataCache.SaveData(output, _id.ToString(), "anime_details_scrapped");
            return output;
        }

        private static string GetString(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : "";

        private static string SanitizeDate(string date)
        {
            if (string.IsNullOrEmpty(date))
                return date;
            var tIndex = date.IndexOf('T');
            return tIndex > 0 ? date.Substring(0, tIndex) : date;
        }

        private static string CapitalizeSeason(string season)
        {
            if (string.IsNullOrEmpty(season))
                return season;
            return char.ToUpperInvariant(season[0]) + season.Substring(1);
        }

        private static int GetInt(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : 0;

        private static double GetDouble(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDouble() : 0;

        private static List<string> GetNameList(JsonElement el, string prop)
        {
            if (!el.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return new List<string>();
            var list = new List<string>();
            foreach (var item in arr.EnumerateArray())
            {
                if (item.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                    list.Add(name.GetString());
            }
            return list;
        }
    }
}

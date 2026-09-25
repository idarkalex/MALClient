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

        public async Task<AnimeScrappedDetails> GetDetails(bool force, bool airing = true)
        {
            var possibleData = force ? null : await DataCache.RetrieveAnimeDetailsScrapped(_id, airing);
            // empty canned/manga records (pre-manga-fill era) fall through so the live path re-populates them
            if (possibleData != null && HasContent(possibleData))
                return possibleData;

            if (!_anime)
                return await FetchMangaFromTenraiAsync(force, airing);

            var output = new AnimeScrappedDetails { Id = _id };

            if (!airing)
            {
                // Finished entries: bounded live fetch first, so the first open gets the FULL
                // Details tab (incl. OP/ED) whenever the network answers. Only fall back to a
                // synthesized/seed-only view when that fetch cannot complete; the fallback is
                // SERVED but never persisted (persisting a partial/empty synth would poison the
                // permanent cache and hide OP/ED + rows forever until a manual refresh).
                // Pull-to-refresh (force) keeps the full retry path and skips the fallbacks.
                var live = await FetchFromTenraiAsync(!force);
                if (live != null && HasContent(live))
                {
                    DataCache.SaveAnimeDetailsScrappedByStatus(_id, live, false);
                    return live;
                }

                if (!force)
                {
                    var synthesized = await BuildFromGeneralDetails();
                    if (synthesized != null)
                        return synthesized;

                    var stale = await DataCache.RetrieveAnimeDetailsScrappedStale(_id);
                    if (stale != null && HasContent(stale))
                        return stale;
                }
                return null;
            }

            var fetched = await FetchFromTenraiAsync(false);
            if (fetched != null && (fetched.Information.Count > 0 || fetched.Statistics.Count > 0))
            {
                DataCache.SaveAnimeDetailsScrappedByStatus(_id, fetched, true);
                return fetched;
            }

            // airing, offline past TTL: serve the expired cache rather than a blank tab
            var airingStale = await DataCache.RetrieveAnimeDetailsScrappedStale(_id);
            if (airingStale != null && (airingStale.Information.Count > 0 || airingStale.Statistics.Count > 0))
                return airingStale;
            return null;
        }

        private async Task<AnimeScrappedDetails> FetchMangaFromTenraiAsync(bool force, bool airing)
        {
            if (!force && airing)
            {
                var stale = await DataCache.RetrieveAnimeDetailsScrappedStale(_id);
                if (stale != null && HasContent(stale))
                    return stale;
            }

            var live = await FetchFromTenraiAsync(!force);
            if (live != null && HasContent(live))
            {
                DataCache.SaveAnimeDetailsScrappedByStatus(_id, live, airing);
                return live;
            }

            if (!force)
            {
                var stale = await DataCache.RetrieveAnimeDetailsScrappedStale(_id);
                if (stale != null && HasContent(stale))
                    return stale;
            }
            return null;
        }

        private static bool HasContent(AnimeScrappedDetails details)
        {
            return details.Information.Count > 0 || details.Statistics.Count > 0 ||
                   details.Openings.Count > 0 || details.Endings.Count > 0;
        }

        private async Task<AnimeScrappedDetails> FetchFromTenraiAsync(bool bounded)
        {
            var output = new AnimeScrappedDetails { Id = _id };

            try
            {
                var timeout = bounded ? (TimeSpan?)TimeSpan.FromSeconds(7) : null;
                var endpoint = _anime ? $"anime/{_id}/full" : $"manga/{_id}/full";
                var data = timeout.HasValue
                    ? await TenraiClient.GetDataAsync(endpoint, timeout.Value)
                    : await TenraiClient.GetDataAsync(endpoint);

                var type = GetString(data, "type");
                var status = GetString(data, "status");

                var info = new List<string>();
                if (!string.IsNullOrEmpty(type)) info.Add($"Type: {type}");
                if (!string.IsNullOrEmpty(status)) info.Add($"Status: {status}");

                if (_anime)
                {
                    var episodes = GetString(data, "episodes");
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
                    var duration = GetString(data, "duration");
                    var rating = GetString(data, "rating");

                    if (!string.IsNullOrEmpty(episodes) && episodes != "0") info.Add($"Episodes: {episodes}");
                    if (!string.IsNullOrEmpty(aired)) info.Add($"Aired: {aired}");
                    if (!string.IsNullOrEmpty(premiered)) info.Add($"Premiered: {premiered}");
                    if (!string.IsNullOrEmpty(broadcast)) info.Add($"Broadcast: {broadcast}");
                    if (producers.Count > 0) info.Add($"Producers: {string.Join(", ", producers)}");
                    if (licensors.Count > 0) info.Add($"Licensors: {string.Join(", ", licensors)}");
                    if (studios.Count > 0) info.Add($"Studios: {string.Join(", ", studios)}");
                    if (!string.IsNullOrEmpty(source)) info.Add($"Source: {source}");
                    if (!string.IsNullOrEmpty(duration)) info.Add($"Duration: {duration}");
                    if (!string.IsNullOrEmpty(rating)) info.Add($"Rating: {rating}");
                }
                else
                {
                    var volumes = GetInt(data, "volumes");
                    var chapters = GetInt(data, "chapters");
                    var published = "";
                    if (data.TryGetProperty("published", out var publishedObj))
                    {
                        var from = SanitizeDate(GetString(publishedObj, "from"));
                        var to = SanitizeDate(GetString(publishedObj, "to"));
                        if (!string.IsNullOrEmpty(from))
                        {
                            published = from;
                            if (!string.IsNullOrEmpty(to))
                                published += " to " + to;
                        }
                    }
                    var serializations = GetNameList(data, "serializations");
                    var authors = GetNameList(data, "authors");

                    if (volumes > 0) info.Add($"Volumes: {volumes}");
                    if (chapters > 0) info.Add($"Chapters: {chapters}");
                    if (!string.IsNullOrEmpty(published)) info.Add($"Published: {published}");
                    if (serializations.Count > 0) info.Add($"Studios: {string.Join(", ", serializations)}");
                    if (authors.Count > 0) info.Add($"Authors: {string.Join(", ", authors)}");
                }

                var genres = GetNameList(data, "genres");
                var themes = GetNameList(data, "themes");
                var demographics = GetNameList(data, "demographics");
                if (genres.Count > 0) info.Add($"Genres: {string.Join(", ", genres)}");
                if (themes.Count > 0) info.Add($"Themes: {string.Join(", ", themes)}");
                if (demographics.Count > 0) info.Add($"Demographics: {string.Join(", ", demographics)}");
                output.Information.AddRange(info);

                var stats = new List<string>();
                var score = GetDouble(data, "score");
                var scoredBy = GetInt(data, "scored_by");
                var rank = GetInt(data, "rank");
                var popularity = GetInt(data, "popularity");
                var members = GetInt(data, "members");
                var favorites = GetInt(data, "favorites");
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

                if (_anime)
                {
                    try
                    {
                        if (data.TryGetProperty("theme", out var themeObj) && themeObj.ValueKind == JsonValueKind.Object)
                        {
                            if (themeObj.TryGetProperty("openings", out var ops))
                            {
                                foreach (var op in ops.EnumerateArray())
                                    output.Openings.Add(op.GetString() ?? "");
                            }
                            if (themeObj.TryGetProperty("endings", out var eds))
                            {
                                foreach (var ed in eds.EnumerateArray())
                                    output.Endings.Add(ed.GetString() ?? "");
                            }
                        }
                    }
                    catch
                    {
                        // themes may not be available
                    }
                }
            }
            catch (Exception)
            {
                // fallthrough
            }

            return output;
        }

        private async Task<AnimeScrappedDetails> BuildFromGeneralDetails()
        {
            try
            {
                var data = await DataCache.RetrieveAnimeSearchResultsData(_id.ToString(), true);
                if (data == null || !string.Equals(data.Status, "Finished Airing", StringComparison.CurrentCultureIgnoreCase))
                    return null;

                var output = new AnimeScrappedDetails { Id = _id };

                var info = new List<string>();
                if (!string.IsNullOrEmpty(data.Type)) info.Add($"Type: {data.Type}");
                if (data.AllEpisodes > 0) info.Add($"Episodes: {data.AllEpisodes}");
                if (!string.IsNullOrEmpty(data.Status)) info.Add($"Status: {data.Status}");
                var aired = "";
                var start = FormatAiredPart(data.StartDate);
                var end = FormatAiredPart(data.EndDate);
                if (!string.IsNullOrEmpty(start))
                {
                    aired = start;
                    if (!string.IsNullOrEmpty(end))
                        aired += " to " + end;
                }
                if (!string.IsNullOrEmpty(aired)) info.Add($"Aired: {aired}");
                if (!string.IsNullOrEmpty(data.Season)) info.Add($"Premiered: {data.Season}");
                if (!string.IsNullOrEmpty(data.Broadcast)) info.Add($"Broadcast: {data.Broadcast}");
                if (data.Studios != null && data.Studios.Count > 0) info.Add($"Studios: {string.Join(", ", data.Studios)}");
                if (data.Genres != null && data.Genres.Count > 0) info.Add($"Genres: {string.Join(", ", data.Genres)}");
                if (data.Themes != null && data.Themes.Count > 0) info.Add($"Themes: {string.Join(", ", data.Themes)}");
                output.Information.AddRange(info);

                var stats = new List<string>();
                if (data.GlobalScore > 0) stats.Add($"Score: {data.GlobalScore:N2}");
                if (data.Rank > 0) stats.Add($"Rank: #{data.Rank:N0}");
                if (data.Popularity > 0) stats.Add($"Popularity: #{data.Popularity:N0}");
                if (data.MembersCount > 0) stats.Add($"Members: {data.MembersCount:N0}");
                if (data.FavoritesCount > 0) stats.Add($"Favorites: {data.FavoritesCount:N0}");
                output.Statistics.AddRange(stats);

                if (!string.IsNullOrEmpty(data.AlternateTitle))
                    output.AlternativeTitles.Add(data.AlternateTitle);
                if (!string.IsNullOrEmpty(data.Title) && !output.AlternativeTitles.Contains(data.Title))
                    output.AlternativeTitles.Add(data.Title);

                return output.Information.Count > 0 || output.Statistics.Count > 0 ? output : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string FormatAiredPart(string date)
        {
            if (string.IsNullOrEmpty(date) || string.Equals(date, "N/A", StringComparison.Ordinal))
                return "";
            return date;
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Text.Json;
using HtmlAgilityPack;
using MALClient.Models.Models.AnimeScrapped;
using MALClient.Models.Models.Favourites;
using MALClient.XShared.Utils;

namespace MALClient.XShared.Comm.Anime
{
    public class AnimeStaffData
    {
        public List<AnimeCharacterStaffModel> AnimeCharacterPairs { get; set; } = new List<AnimeCharacterStaffModel>();
        public List<AnimeStaffPerson> AnimeStaff { get; set; } = new List<AnimeStaffPerson>();
    }

    public class AnimeCharactersStaffQuery : Query
    {
        private readonly int _animeId;
        private readonly bool _animeMode;

        public AnimeCharactersStaffQuery(int id, bool anime = true)
        {
            Request =
                new Uri(
                    Uri.EscapeUriString(
                        $"https://myanimelist.net/{(anime ? "anime" : "manga")}/{id}/whatever/characters"));
            _animeId = id;
            _animeMode = anime;
        }

        public async Task<AnimeStaffData> GetCharStaffData(bool force = false)
        {
            if (!_animeMode)
                throw new InvalidOperationException("Umm you said it's going to be manga...");
            var output = force
                ? new AnimeStaffData()
                : await DataCache.RetrieveData<AnimeStaffData>($"staff_v2_{_animeId}", "AnimeDetails", 7) ??
                  new AnimeStaffData();
            if (HasData(output) && !force) return output;

            try
            {
                var structured = await GetCharStaffDataStructuredAsync();
                if (HasData(structured))
                {
                    DiagnosticsReporter.Info("Characters", $"structured API: {structured.AnimeCharacterPairs.Count} pairs, {structured.AnimeStaff.Count} staff for anime {_animeId}");
                    await DataCache.SaveData(structured, $"staff_v2_{_animeId}", "AnimeDetails");
                    return structured;
                }
                DiagnosticsReporter.Warn("Characters", $"structured API returned empty for anime {_animeId}, falling back to HTML");
            }
            catch (Exception ex)
            {
                DiagnosticsReporter.Info("Characters", $"structured API failed for anime {_animeId}, falling back to HTML: {ex.Message}");
            }

            var htmlResult = await GetCharStaffDataHtml(output);
            DiagnosticsReporter.Info("Characters", $"HTML scrape: {htmlResult.AnimeCharacterPairs.Count} pairs, {htmlResult.AnimeStaff.Count} staff for anime {_animeId}");
            if (HasData(htmlResult))
                await DataCache.SaveData(htmlResult, $"staff_v2_{_animeId}", "AnimeDetails");
            return htmlResult;
        }

        private async Task<AnimeStaffData> GetCharStaffDataStructuredAsync()
        {
            var output = new AnimeStaffData();

            var charsData = await TenraiClient.GetDataAsync($"anime/{_animeId}/characters");
            if (charsData.ValueKind != JsonValueKind.Array)
                return null;
            foreach (var entry in EnumerateArray(charsData))
            {
                try
                {
                    var pair = new AnimeCharacterStaffModel();

                    var charImg = CleanImage(GetNestedString(entry, "character", "images", "jpg", "image_url"));
                    var charObj = pair.AnimeCharacter;
                    charObj.Id = GetNestedString(entry, "character", "mal_id");
                    charObj.Name = WebUtility.HtmlDecode(GetNestedString(entry, "character", "name").Replace(",", ""));
                    if (!string.IsNullOrEmpty(charImg))
                        charObj.ImgUrl = charImg;
                    charObj.FromAnime = true;
                    charObj.ShowId = _animeId.ToString();
                    charObj.Notes = BuildRoleNotes(GetString(entry, "role"), GetInt(entry, "favorites"));

                    var voiceActors = GetVoiceActors(entry);
                    if (voiceActors.Count > 0)
                    {
                        pair.AnimeStaffPerson = voiceActors[0];
                        pair.VoiceActors.AddRange(voiceActors);
                    }
                    else
                    {
                        pair.AnimeStaffPerson.Name = "Unknown";
                        pair.AnimeStaffPerson.IsUnknown = true;
                    }

                    output.AnimeCharacterPairs.Add(pair);
                }
                catch (Exception)
                {
                }
            }

            var staffData = await TenraiClient.GetDataAsync($"anime/{_animeId}/staff");
            if (staffData.ValueKind != JsonValueKind.Array)
                return null;
            foreach (var entry in EnumerateArray(staffData))
            {
                try
                {
                    var person = new AnimeStaffPerson();
                    person.Id = GetNestedString(entry, "person", "mal_id");
                    person.Name = WebUtility.HtmlDecode(GetNestedString(entry, "person", "name").Replace(",", ""));

                    var img = CleanImage(GetNestedString(entry, "person", "images", "jpg", "image_url"));
                    if (!string.IsNullOrEmpty(img))
                        person.ImgUrl = img;

                    var positions = new List<string>();
                    if (entry.TryGetProperty("positions", out var posArr) && posArr.ValueKind == JsonValueKind.Array)
                        foreach (var pos in posArr.EnumerateArray())
                            positions.Add(pos.GetString() ?? "");
                    person.Notes = string.Join(", ", positions);

                    if (!string.IsNullOrEmpty(person.Name))
                        output.AnimeStaff.Add(person);
                }
                catch (Exception)
                {
                }
            }

            return HasData(output) ? output : null;
        }

        public async Task<AnimeStaffData> GetMangaCharStaffData(bool force = false)
        {
            if (_animeMode)
                throw new InvalidOperationException("You fed constructor with anime, remember?");

            var cached = force
                ? null
                : await DataCache.RetrieveData<AnimeStaffData>($"staff_v2_{_animeId}", "MangaDetails", 7);
            if (cached != null && (cached.AnimeCharacterPairs?.Count ?? 0) > 0)
                return cached;

            var output = new AnimeStaffData();
            try
            {
                var charsData = await TenraiClient.GetDataAsync($"manga/{_animeId}/characters");
                if (charsData.ValueKind != JsonValueKind.Array)
                    return output;
                foreach (var entry in EnumerateArray(charsData))
                {
                    try
                    {
                        var pair = new AnimeCharacterStaffModel();
                        var img = CleanImage(GetNestedString(entry, "character", "images", "jpg", "image_url"));
                        var charObj = pair.AnimeCharacter;
                        charObj.Id = GetNestedString(entry, "character", "mal_id");
                        charObj.Name = WebUtility.HtmlDecode(GetNestedString(entry, "character", "name").Replace(",", ""));
                        if (!string.IsNullOrEmpty(img))
                            charObj.ImgUrl = img;
                        charObj.FromAnime = false;
                        charObj.ShowId = _animeId.ToString();
                        charObj.Notes = BuildRoleNotes(GetString(entry, "role"), GetInt(entry, "favorites"));

                        pair.AnimeStaffPerson.Name = "Unknown";
                        pair.AnimeStaffPerson.IsUnknown = true;

                        output.AnimeCharacterPairs.Add(pair);
                    }
                    catch (Exception)
                    {
                        //
                    }
                }
            }
            catch (Exception)
            {
                //
            }

            if (output.AnimeCharacterPairs.Count > 0)
                await DataCache.SaveData(output, $"staff_v2_{_animeId}", "MangaDetails");
            return output;
        }

        private static string BuildRoleNotes(string role, int favorites)
        {
            var notes = role ?? "";
            if (favorites > 0)
                notes = string.IsNullOrEmpty(notes) ? $"{favorites:N0} favorites" : $"{notes} � {favorites:N0} favorites";
            return notes;
        }

        private static List<AnimeStaffPerson> GetVoiceActors(JsonElement entry)
        {
            var output = new List<AnimeStaffPerson>();
            if (!entry.TryGetProperty("voice_actors", out var voices) || voices.ValueKind != JsonValueKind.Array)
                return output;

            foreach (var voice in voices.EnumerateArray())
            {
                try
                {
                    var actor = new AnimeStaffPerson
                    {
                        Id = GetNestedString(voice, "person", "mal_id"),
                        Name = WebUtility.HtmlDecode(GetNestedString(voice, "person", "name").Replace(",", "")),
                        ImgUrl = CleanImage(GetNestedString(voice, "person", "images", "jpg", "image_url")),
                        Notes = GetString(voice, "language")
                    };
                    if (!string.IsNullOrEmpty(actor.Name))
                        output.Add(actor);
                }
                catch (Exception)
                {
                }
            }

            return output
                .Where(actor => string.Equals(actor.Notes, "Japanese", StringComparison.OrdinalIgnoreCase))
                .Concat(output.Where(actor =>
                    !string.Equals(actor.Notes, "Japanese", StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        private static bool HasData(AnimeStaffData data)
        {
            return data != null &&
                   ((data.AnimeCharacterPairs?.Count ?? 0) > 0 || (data.AnimeStaff?.Count ?? 0) > 0);
        }

        private static IEnumerable<JsonElement> EnumerateArray(JsonElement el)
        {
            if (el.ValueKind != JsonValueKind.Array)
                yield break;
            foreach (var item in el.EnumerateArray())
                yield return item.Clone();
        }

        private static string CleanImage(string url)
        {
            if (string.IsNullOrEmpty(url))
                return url;
            url = Regex.Replace(url, @"\/r\/\d+x\d+", "");
            var queryIndex = url.IndexOf('?');
            return queryIndex > 0 ? url.Substring(0, queryIndex) : url;
        }

        private static string GetString(JsonElement el, string prop)
        {
            if (!el.TryGetProperty(prop, out var p))
                return "";
            if (p.ValueKind == JsonValueKind.String)
                return p.GetString();
            if (p.ValueKind == JsonValueKind.Number)
                return p.GetRawText();
            return "";
        }

        private static int GetInt(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : 0;

        private static string GetNestedString(JsonElement el, params string[] props)
        {
            foreach (var prop in props.Take(props.Length - 1))
                if (!el.TryGetProperty(prop, out el))
                    return "";
            return GetString(el, props.Last());
        }

        private async Task<AnimeStaffData> GetCharStaffDataHtml(AnimeStaffData output)
        {
            output ??= new AnimeStaffData();
            output.AnimeCharacterPairs ??= new List<AnimeCharacterStaffModel>();
            output.AnimeStaff ??= new List<AnimeStaffPerson>();
            var raw = await GetRequestResponse();
            if (string.IsNullOrEmpty(raw))
                return output;

            var doc = new HtmlDocument();
            doc.LoadHtml(raw);

            try
            {
                var mainContainer = doc.FirstOfDescendantsWithClassContaining("div", "js-scrollfix-bottom-rel");
                List<HtmlNode> charTables = new List<HtmlNode>();
                List<HtmlNode> staffTables = new List<HtmlNode>();
                bool nowStaff = false;
                int headerCount = 0;
                foreach (var node in mainContainer.Descendants("table"))
                {
                    try
                    {
                        if (node.Name == "table")
                        {
                            var tdCount = node.Descendants("td").Count();
                            if (tdCount >= 3)
                            {
                                charTables.Add(node);
                            }
                            else if (tdCount == 2)
                            {
                                staffTables.Add(node);
                            }

                        }
                    }
                    catch (Exception e)
                    {

                    }

                }
                foreach (var table in charTables)
                {
                    try
                    {
                        if (table.Attributes["class"].Value == "js-anime-character-va")
                            continue;

                        var current = new AnimeCharacterStaffModel();

                        var imgs = table.Descendants("img").ToList();
                        var infos = table.Descendants("td").ToList(); //2nd is character 4th is person

                        //character
                        var img = imgs[0].Attributes["data-src"].Value;
                        if (!img.Contains("questionmark"))
                        {
                            img = Regex.Replace(img,@"\/r\/\d+x\d+", "");
                            current.AnimeCharacter.ImgUrl = CleanImage(img);
                        }

                        current.AnimeCharacter.FromAnime = _animeMode;
                        current.AnimeCharacter.ShowId = _animeId.ToString();
                        current.AnimeCharacter.Name =
                            WebUtility.HtmlDecode(imgs[0].Attributes["alt"].Value.Replace(",", ""));

                        current.AnimeCharacter.Id = infos[1].Descendants("a").First().Attributes["href"].Value.Split('/')[4];

                        var pads = infos[1].WhereOfDescendantsWithClass("div", "spaceit_pad").ToList();


                        if(pads.Count > 1)
                            current.AnimeCharacter.Notes = WebUtility.HtmlDecode(pads[1].InnerText.Replace("\n","").Trim());
                        //if (pads.Count > 2)
                        //    current.AnimeCharacter.Notes += "," + WebUtility.HtmlDecode(pads[2].InnerText.Replace("\n", "").Trim());

                        //voiceactor
                        try
                        {
                            img = imgs[1].Attributes["data-src"].Value;
                            if (!img.Contains("questionmark"))
                            {
                                img = Regex.Replace(img, @"\/r\/\d+x\d+", "");
                                current.AnimeStaffPerson.ImgUrl = CleanImage(img);
                            }
                            current.AnimeStaffPerson.Name = WebUtility.HtmlDecode(imgs[1].Attributes["alt"].Value.Replace(",", ""));

                            var padss = infos[3].WhereOfDescendantsWithClass("div", "spaceit_pad").ToList();

                            current.AnimeStaffPerson.Id = padss[0].ChildNodes[1].Attributes["href"].Value.Split('/')[4];
                            if(padss.Count > 1)
                                current.AnimeStaffPerson.Notes = WebUtility.HtmlDecode(padss[1].InnerText.Replace("\n", "").Trim());

                        }
                        catch (Exception e)
                        {
                            //no voice actor
                            current.AnimeStaffPerson.Name = "Unknown";
                            current.AnimeStaffPerson.IsUnknown = true;
                        }
                        if (!current.AnimeStaffPerson.IsUnknown && !string.IsNullOrEmpty(current.AnimeStaffPerson.Name))
                            current.VoiceActors.Add(current.AnimeStaffPerson);
                        output.AnimeCharacterPairs.Add(current);
                    }
                    catch (Exception e)
                    {
                        //oddities
                    }

                }
                foreach (var staffRow in staffTables)
                {
                    try
                    {
                        var current = new AnimeStaffPerson();
                        var imgs = staffRow.Descendants("img").ToList();
                        var info = staffRow.Descendants("td").Last(); //we want last

                        var img = imgs[0].Attributes["data-src"].Value;
                        if (!img.Contains("questionmark"))
                        {
                            img = Regex.Replace(img, @"\/r\/\d+x\d+", "");
                            current.ImgUrl = CleanImage(img);
                        }
                        var link = info.Descendants("a").First();
                        current.Name = WebUtility.HtmlDecode(link.InnerText.Trim().Replace(",", ""));
                        current.Id = link.Attributes["href"].Value.Split('/')[4];
                        current.Notes = staffRow.FirstOfDescendantsWithClass("div", "spaceit_pad").InnerText.Trim();

                        if(string.IsNullOrEmpty(current.Name))
                            continue;

                        output.AnimeStaff.Add(current);
                    }
                    catch (Exception e)
                    {
                        //what can I say?
                    }
                }
            }
            catch (Exception e)
            {
                //mysteries of html
            }

            return output;
        }

    }
}





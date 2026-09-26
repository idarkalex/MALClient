using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MALClient.Models.Models.Anime;
using MALClient.Models.Models.Favourites;
using MALClient.Models.Models.ScrappedDetails;
using MALClient.XShared.Utils;

namespace MALClient.XShared.Comm.Details
{
    public class StaffDetailsQuery : Query
    {

        private readonly int _id;

        public StaffDetailsQuery(int id)
        {
            _id = id;
            Request =
                new Uri(
                    Uri.EscapeUriString($"https://myanimelist.net/people/{id}"));
        }

        public async Task<StaffDetailsData> GetStaffDetails(bool force)
        {
            var possibleData = force
                ? null
                : await DataCache.RetrieveData<StaffDetailsData>($"staff_details_v5_{_id}.json",
                    "staff_details", 30);
            if (IsStructuredDataValid(possibleData))
                return possibleData;

            var output = await FetchFromTenraiAsync();
            if (IsStructuredDataValid(output))
            {
                await DataCache.SaveData(output, $"staff_details_v5_{_id}.json", "staff_details");
                return output;
            }

            return await FetchFromHtmlAsync();
        }

        private async Task<StaffDetailsData> FetchFromTenraiAsync()
        {
            try
            {
                var data = await TenraiClient.GetDataAsync($"people/{_id}/full");
                if (data.ValueKind != JsonValueKind.Object)
                    return null;

                var output = new StaffDetailsData { Id = _id };
                var givenName = GetString(data, "given_name");
                var familyName = GetString(data, "family_name");
                output.Name = GetString(data, "name");
                if (string.IsNullOrWhiteSpace(output.Name))
                    output.Name = $"{givenName} {familyName}".Trim();
                if (string.IsNullOrWhiteSpace(output.Name))
                    return null;
                output.ImgUrl = GetNestedImageUrl(data);

                if (!string.IsNullOrWhiteSpace(givenName))
                    output.Details.Add("Given name: " + givenName);
                if (!string.IsNullOrWhiteSpace(familyName))
                    output.Details.Add("Family name: " + familyName);

                var nativeName = GetString(data, "native_name");
                if (string.IsNullOrEmpty(nativeName))
                    nativeName = GetString(data, "name_native");
                if (!string.IsNullOrWhiteSpace(nativeName) &&
                    !string.Equals(nativeName, output.Name, StringComparison.OrdinalIgnoreCase))
                    output.Details.Add("Native name: " + nativeName);

                var alternateNames = GetStringArray(data, "alternate_names")
                    .Where(name => !string.IsNullOrWhiteSpace(name) &&
                                    !string.Equals(name, output.Name, StringComparison.OrdinalIgnoreCase) &&
                                    !string.Equals(name, nativeName, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (alternateNames.Count > 0)
                    output.Details.Add("Alternate names: " + string.Join(", ", alternateNames));

            var birthday = GetString(data, "birthday");
            if (!string.IsNullOrWhiteSpace(birthday))
                output.Details.Add("Birthday: " + NormalizeDetailDate(birthday));
                if (data.TryGetProperty("birthdays", out var birthdays) && birthdays.ValueKind == JsonValueKind.Array)
                {
                    foreach (var birthdayEntry in birthdays.EnumerateArray())
                    {
                        try
                        {
                            var value = GetString(birthdayEntry, "birthday");
                            value = NormalizeDetailDate(value);
                            if (string.IsNullOrWhiteSpace(value) ||
                                output.Details.Any(detail => detail.EndsWith(": " + value,
                                    StringComparison.OrdinalIgnoreCase)))
                                continue;
                            var type = GetString(birthdayEntry, "type");
                            output.Details.Add((string.IsNullOrWhiteSpace(type) ? "Birthday" : type) + ": " + value);
                        }
                        catch (Exception)
                        {
                        }
                    }
                }

                var about = GetString(data, "about");
                if (!string.IsNullOrEmpty(about))
                {
                    about = WebUtility.HtmlDecode(about);
                    var bracketPos = about.IndexOf("(Source:");
                    if (bracketPos > 0)
                        about = about.Substring(0, bracketPos).Trim();
                    about = Regex.Replace(about, "\r?\n+", "\n").Trim();
                    if (!string.IsNullOrEmpty(about))
                        output.Details.Add(about);
                }

                ParseVoiceRoles(data, output);
                ParsePositions(data, "anime", output, true);
                ParsePositions(data, "manga", output, false);

                return output;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private bool IsStructuredDataValid(StaffDetailsData data)
        {
            return data != null && data.Id == _id && !string.IsNullOrWhiteSpace(data.Name) &&
                   ((data.Details?.Count ?? 0) > 0 || (data.ShowCharacterPairs?.Count ?? 0) > 0 ||
                    (data.StaffPositions?.Count ?? 0) > 0);
        }

        private static string NormalizeDetailDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;
            var trimmed = value.Trim();
            if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out var parsed))
                return parsed.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);
            return trimmed;
        }

        private static void ParseVoiceRoles(JsonElement data, StaffDetailsData output)
        {
            if (!data.TryGetProperty("voices", out var voices) || voices.ValueKind != JsonValueKind.Array)
                return;
            foreach (var voice in voices.EnumerateArray())
            {
                try
                {
                    if (voice.ValueKind != JsonValueKind.Object)
                        continue;
                    var pair = new ShowCharacterPair();

                    if (voice.TryGetProperty("anime", out var animeEl) && animeEl.ValueKind == JsonValueKind.Object)
                    {
                        var show = new AnimeLightEntry { IsAnime = true };
                        show.Id = GetInt(animeEl, "mal_id");
                        show.Title = WebUtility.HtmlDecode(GetString(animeEl, "title"));
                        show.ImgUrl = GetNestedImageUrl(animeEl);
                        var mediaType = NormalizeMediaType(GetString(animeEl, "media_type"));
                        if (string.IsNullOrEmpty(mediaType))
                        {
                            var entryType = GetString(animeEl, "type");
                            if (!string.Equals(entryType, "anime", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(entryType, "manga", StringComparison.OrdinalIgnoreCase))
                                mediaType = NormalizeMediaType(entryType);
                        }
                        show.Notes = BuildNotes(mediaType, GetString(voice, "role"));
                        if (show.Id > 0 && show.Title != null)
                            pair.AnimeLightEntry = show;
                    }

                    if (voice.TryGetProperty("character", out var charEl) && charEl.ValueKind == JsonValueKind.Object)
                    {
                        var character = new AnimeCharacter { FromAnime = true };
                        character.Id = GetIntString(charEl, "mal_id");
                        character.Name = WebUtility.HtmlDecode(GetString(charEl, "name"));
                        character.ImgUrl = GetNestedImageUrl(charEl);
                        character.Notes = GetString(voice, "role");
                        character.ShowId = pair.AnimeLightEntry?.Id.ToString() ?? "";
                        if (character.Name != null)
                            pair.AnimeCharacter = character;
                    }

                    if (pair.AnimeLightEntry != null && pair.AnimeCharacter != null)
                        output.ShowCharacterPairs.Add(pair);
                }
                catch (Exception)
                {
                    // skip malformed voice role
                }
            }
        }

        private static void ParsePositions(JsonElement data, string prop, StaffDetailsData output, bool isAnime)
        {
            if (!data.TryGetProperty(prop, out var positions) || positions.ValueKind != JsonValueKind.Array)
                return;
            foreach (var position in positions.EnumerateArray())
            {
                try
                {
                    if (position.ValueKind != JsonValueKind.Object)
                        continue;
                    if (!position.TryGetProperty(prop, out var entry) || entry.ValueKind != JsonValueKind.Object)
                    {
                        if (position.TryGetProperty(prop == "anime" ? "anime" : "manga", out var fallback))
                            entry = fallback;
                    }
                    if (entry.ValueKind != JsonValueKind.Object)
                        continue;

                    var show = new AnimeLightEntry { IsAnime = isAnime };
                    show.Id = GetInt(entry, "mal_id");
                    show.Title = WebUtility.HtmlDecode(GetString(entry, "title"));
                    show.ImgUrl = GetNestedImageUrl(entry);
                    var role = GetString(position, "position");
                    if (string.IsNullOrEmpty(role))
                        role = WebUtility.HtmlDecode(GetString(position, "role"));
                    var mediaType = NormalizeMediaType(GetString(entry, "media_type"));
                    if (string.IsNullOrEmpty(mediaType))
                    {
                        var entryType = GetString(entry, "type");
                        if (!string.Equals(entryType, "anime", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(entryType, "manga", StringComparison.OrdinalIgnoreCase))
                            mediaType = NormalizeMediaType(entryType);
                    }
                    show.Notes = BuildNotes(role, mediaType);
                    if (show.Id > 0 && show.Title != null)
                        output.StaffPositions.Add(show);
                }
                catch (Exception)
                {
                    // skip malformed position
                }
            }
        }

        private static string BuildNotes(string role, string mediaType)
        {
            return string.Join(" | ", new[] {role, mediaType}.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static string NormalizeMediaType(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
                return type;
            return type switch
            {
                "tv" => "TV",
                "tv_special" => "TV Special",
                "movie" => "Movie",
                "ova" => "OVA",
                "ona" => "ONA",
                "special" => "Special",
                "music" => "Music",
                "cm" => "Commercial",
                "pv" => "PV",
                "manga" => "Manga",
                "novel" => "Novel",
                "light_novel" => "Light Novel",
                "one_shot" => "One-shot",
                "oneshot" => "One-shot",
                "doujinshi" => "Doujinshi",
                "doujin" => "Doujinshi",
                "manhwa" => "Manhwa",
                "manhua" => "Manhua",
                _ => type
            };
        }

        private static List<string> GetStringArray(JsonElement el, string prop)
        {
            var output = new List<string>();
            if (!el.TryGetProperty(prop, out var p))
                return output;
            if (p.ValueKind == JsonValueKind.String)
            {
                if (!string.IsNullOrWhiteSpace(p.GetString()))
                    output.Add(p.GetString());
                return output;
            }
            if (p.ValueKind != JsonValueKind.Array)
                return output;
            foreach (var item in p.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                    output.Add(item.GetString());
            return output;
        }

        private static int GetInt(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : 0;

        private static string GetIntString(JsonElement el, string prop)
        {
            if (el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number)
                return p.GetInt32().ToString();
            if (el.TryGetProperty(prop, out var ps) && ps.ValueKind == JsonValueKind.String)
                return ps.GetString();
            return null;
        }

        private static string GetString(JsonElement el, string prop)
        {
            if (el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String)
                return p.GetString();
            return null;
        }

        private static string GetNestedImageUrl(JsonElement entry)
        {
            if (entry.TryGetProperty("images", out var images) && images.ValueKind == JsonValueKind.Object)
            {
                if (images.TryGetProperty("webp", out var webp) && webp.ValueKind == JsonValueKind.Object &&
                    webp.TryGetProperty("image_url", out var webpFull) && webpFull.ValueKind == JsonValueKind.String)
                    return webpFull.GetString();
                if (images.TryGetProperty("jpg", out var jpg) && jpg.ValueKind == JsonValueKind.Object)
                {
                    if (jpg.TryGetProperty("image_url", out var full) && full.ValueKind == JsonValueKind.String)
                        return full.GetString();
                    if (jpg.TryGetProperty("small_image_url", out var small) && small.ValueKind == JsonValueKind.String)
                        return small.GetString();
                }
            }

            if (entry.TryGetProperty("image", out var direct) && direct.ValueKind == JsonValueKind.String)
                return direct.GetString();
            return null;
        }

        private async Task<StaffDetailsData> FetchFromHtmlAsync()
        {
            var output = new StaffDetailsData();
            var raw = await GetRequestResponse();
            if (string.IsNullOrEmpty(raw))
                return output;
            var doc = new HtmlDocument();
            doc.LoadHtml(raw);

            output.Id = _id;
            try
            {
                var columns =
                    doc.DocumentNode.Descendants("table").First().ChildNodes[0].ChildNodes.Where(
                        node => node.Name == "td").ToList();
                var leftColumn = columns[0];
                var image = leftColumn.Descendants("img").FirstOrDefault();
                if (image != null && image.Attributes.Contains("alt"))
                    output.ImgUrl = image.Attributes["data-src"].Value;

                output.Name = WebUtility.HtmlDecode(doc.DocumentNode.Descendants("h1").First().InnerText.Trim());
                output.Name = output.Name?.Split('\n')[0];
                bool recording = false;
                var currentString = "";
                int i = 0;
                foreach (var child in leftColumn.ChildNodes)
                {
                    if (!recording)
                    {
                        if (child.Attributes.Contains("class") &&
                            child.Attributes["class"].Value.Trim() == "js-sns-icon-container icon-block")
                            recording = true;
                        else
                            continue;
                    }

                    if (child.Attributes.Contains("class") &&
                        child.Attributes["class"].Value == "spaceit_pad")
                    {
                        output.Details.Add(WebUtility.HtmlDecode(child.InnerText.Trim()));
                        currentString = "";
                        i = 0;
                    }
                    else if (!string.IsNullOrWhiteSpace(child.InnerText))
                    {
                        currentString += WebUtility.HtmlDecode(child.InnerText.Trim()) + " ";
                        i++;
                        if (i == 2)
                        {
                            output.Details.Add(currentString);
                            currentString = "";
                            i = 0;
                        }
                    }

                    if (child.Name == "div" && !child.Attributes.Contains("class"))
                        break;
                }

                var more = doc.FirstOrDefaultOfDescendantsWithClass("div",
                    "people-informantion-more js-people-informantion-more");
                if (more != null)
                {
                    output.Details.Add(WebUtility.HtmlEncode(more.InnerText.Trim()));
                }

                foreach (var table in columns[1].Descendants("table").Take(2))
                    try
                    {
                        foreach (var row in table.Descendants("tr"))
                        {

                            var tds = row.Descendants("td").ToList();
                            if (tds.Count == 4)
                            {
                                var current = new ShowCharacterPair();
                                var show = new AnimeLightEntry();
                                var img = tds[0].Descendants("img").First().Attributes["data-src"].Value;
                                if (!img.Contains("questionmark"))
                                {
                                    img = Regex.Replace(img, @"\/r\/\d+x\d+", "");
                                    show.ImgUrl = img.Substring(0, img.IndexOf('?'));
                                }
                                var link = tds[1].Descendants("a").First();
                                show.IsAnime = true;
                                show.Id = int.Parse(link.Attributes["href"].Value.Split('/')[4]);
                                show.Title = WebUtility.HtmlDecode(link.InnerText.Trim());
                                current.AnimeLightEntry = show;

                                var character = new AnimeCharacter();
                                character.FromAnime = true;
                                character.ShowId = show.Id.ToString();
                                link = tds[2].Descendants("a").First();
                                character.Id = link.Attributes["href"].Value.Split('/')[4];
                                character.Name = WebUtility.HtmlDecode(link.InnerText.Trim());
                                character.Notes = WebUtility.HtmlDecode(tds[2].Descendants("div").Last().InnerText);

                                img = tds[3].Descendants("img").First().Attributes["data-src"].Value;
                                if (!img.Contains("questionmark"))
                                {
                                    img = Regex.Replace(img, @"\/r\/\d+x\d+", "");
                                    character.ImgUrl = img.Substring(0, img.IndexOf('?'));
                                }

                                current.AnimeCharacter = character;
                                output.ShowCharacterPairs.Add(current);
                            }
                            else
                            {
                                var show = new AnimeLightEntry();
                                var img = tds[0].Descendants("img").First().Attributes["data-src"].Value;
                                if (!img.Contains("questionmark"))
                                {
                                    img = Regex.Replace(img, @"\/r\/\d+x\d+", "");
                                    show.ImgUrl = img.Substring(0, img.IndexOf('?'));
                                }
                                var link = tds[1].Descendants("a").First();
                                show.IsAnime = !link.Attributes["href"].Value.Contains("/manga/");
                                show.Id = int.Parse(link.Attributes["href"].Value.Split('/')[4]);
                                show.Title = WebUtility.HtmlDecode(link.InnerText.Trim());
                                show.Notes =
                                    WebUtility.HtmlDecode(
                                        tds[1].Descendants("div").Last().InnerText.Replace("add", "").Trim());

                                output.StaffPositions.Add(show);

                            }
                        }
                    }
                    catch
                        (Exception e)
                    {
                        //htaml
                    }
            }
            catch (Exception)
            {
                //sorcery
            }

            return output;
        }
    }
}
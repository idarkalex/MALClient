using System;
using System.Collections.Generic;
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
            var possibleData = force ? null : await DataCache.RetrieveData<StaffDetailsData>(_id.ToString(), "staff_details", 30);
            if (possibleData != null)
                return possibleData;

            var output = await FetchFromTenraiAsync();
            if (output != null && output.Name != null)
            {
                DataCache.SaveData(output, _id.ToString(), "staff_details");
                return output;
            }

            output = await FetchFromHtmlAsync();
            DataCache.SaveData(output, _id.ToString(), "staff_details");
            return output;
        }

        private async Task<StaffDetailsData> FetchFromTenraiAsync()
        {
            try
            {
                var data = await TenraiClient.GetDataAsync($"people/{_id}");
                if (data.ValueKind != JsonValueKind.Object)
                    return null;

                var output = new StaffDetailsData { Id = _id };
                output.Name = GetString(data, "name");
                if (string.IsNullOrEmpty(output.Name))
                    return null;
                output.ImgUrl = GetNestedImageUrl(data);

                var alternateNames = GetStringArray(data, "alternate_names");
                if (alternateNames.Count > 0)
                    output.Details.Add("Alternate names: " + string.Join(", ", alternateNames));

                if (data.TryGetProperty("birthdays", out var birthdays) && birthdays.ValueKind == JsonValueKind.Array)
                {
                    foreach (var birthday in birthdays.EnumerateArray())
                    {
                        try
                        {
                            var bday = GetString(birthday, "birthday");
                            if (!string.IsNullOrEmpty(bday))
                            {
                                output.Details.Add((GetString(birthday, "type") ?? "Birthday") + ": " + bday);
                            }
                        }
                        catch (Exception)
                        {
                            // skip malformed birthday
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
                    show.Notes = !string.IsNullOrEmpty(role) ? role : WebUtility.HtmlDecode(GetString(position, "role"));
                    if (show.Id > 0 && show.Title != null)
                        output.StaffPositions.Add(show);
                }
                catch (Exception)
                {
                    // skip malformed position
                }
            }
        }

        private static List<string> GetStringArray(JsonElement el, string prop)
        {
            var output = new List<string>();
            if (el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in p.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                        output.Add(item.GetString());
                }
            }
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
            if (!entry.TryGetProperty("images", out var images) || images.ValueKind != JsonValueKind.Object)
                return null;
            if (!images.TryGetProperty("jpg", out var jpg) || jpg.ValueKind != JsonValueKind.Object)
                return null;
            if (jpg.TryGetProperty("large_image_url", out var large) && large.ValueKind == JsonValueKind.String)
                return large.GetString();
            if (jpg.TryGetProperty("image_url", out var img) && img.ValueKind == JsonValueKind.String)
                return img.GetString();
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
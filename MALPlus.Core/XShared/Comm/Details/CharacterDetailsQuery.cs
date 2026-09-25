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
    public class CharacterDetailsQuery : Query
    {
        private readonly int _id;

        public CharacterDetailsQuery(int id)
        {
            _id = id;
            Request =
                new Uri(
                    Uri.EscapeUriString($"https://myanimelist.net/character/{id}"));
        }

        public async Task<CharacterDetailsData> GetCharacterDetails(bool force = false)
        {
            var possibleData = force
                ? null
                : await DataCache.RetrieveData<CharacterDetailsData>($"character_details_v2_{_id}.json",
                    "character_details", 30);
            if (possibleData != null && possibleData.Id == _id && !string.IsNullOrWhiteSpace(possibleData.Name))
                return possibleData;

            var output = await FetchFromTenraiAsync();
            if (!string.IsNullOrWhiteSpace(output?.Name))
            {
                await DataCache.SaveData(output, $"character_details_v2_{_id}.json", "character_details");
                return output;
            }

            output = await FetchFromHtmlAsync();
            if (!string.IsNullOrWhiteSpace(output?.Name))
                await DataCache.SaveData(output, $"character_details_v2_{_id}.json", "character_details");
            return output ?? new CharacterDetailsData();
        }

        private async Task<CharacterDetailsData> FetchFromTenraiAsync()
        {
            try
            {
                var data = await TenraiClient.GetDataAsync($"characters/{_id}/full");
                if (data.ValueKind != JsonValueKind.Object)
                    return null;

                var output = new CharacterDetailsData { Id = _id };
                output.Name = GetString(data, "name");
                if (string.IsNullOrWhiteSpace(output.Name))
                    return null;

                output.ImgUrl = GetNestedImageUrl(data);

                var favorites = GetInt(data, "favorites");
                if (favorites > 0)
                    output.TotalFavs = favorites.ToString("N0");

                var content = new List<string>();
                var nativeName = GetString(data, "name_kanji");
                if (string.IsNullOrEmpty(nativeName))
                    nativeName = GetString(data, "native_name");
                if (!string.IsNullOrEmpty(nativeName) &&
                    !string.Equals(nativeName, output.Name, StringComparison.OrdinalIgnoreCase))
                    content.Add("Native name: " + nativeName);

                var alternateNames = GetStringList(data, "alternate_names")
                    .Where(name => !string.IsNullOrWhiteSpace(name) &&
                                    !string.Equals(name, output.Name, StringComparison.OrdinalIgnoreCase) &&
                                    !string.Equals(name, nativeName, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (alternateNames.Count > 0)
                    content.Add("Alternate names: " + string.Join(", ", alternateNames));

                var nicknames = GetStringList(data, "nicknames")
                    .Where(nickname => !string.IsNullOrWhiteSpace(nickname))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (nicknames.Count > 0)
                    content.Add("Nicknames: " + string.Join(", ", nicknames));

                var about = GetString(data, "about");
                if (!string.IsNullOrEmpty(about))
                {
                    about = WebUtility.HtmlDecode(about);
                    var bracketPos = about.IndexOf("(Source:");
                    if (bracketPos > 0)
                        about = about.Substring(0, bracketPos).Trim();
                    about = Regex.Replace(about, "\r?\n+", "\n").Trim();
                    if (!string.IsNullOrEmpty(about))
                        content.Add(about);
                }
                output.Content = string.Join("\n\n", content);
                output.SpoilerContent = "";

                ParseAnimeography(data, "anime", output.Animeography, true);
                ParseAnimeography(data, "manga", output.Mangaography, false);

                if (data.TryGetProperty("voices", out var voices) && voices.ValueKind == JsonValueKind.Array)
                {
                    foreach (var voice in voices.EnumerateArray())
                    {
                        try
                        {
                            if (voice.ValueKind != JsonValueKind.Object)
                                continue;
                            if (!voice.TryGetProperty("person", out var person) || person.ValueKind != JsonValueKind.Object)
                                continue;
                            var current = new AnimeStaffPerson();
                            current.Id = GetIntString(person, "mal_id");
                            current.Name = GetString(person, "name");
                            current.ImgUrl = GetNestedImageUrl(person);
                            current.Notes = GetString(voice, "language");
                            if (current.Name != null)
                                output.VoiceActors.Add(current);
                        }
                        catch (Exception)
                        {
                            // skip malformed voice
                        }
                    }
                }

                return output;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void ParseAnimeography(JsonElement data, string prop, List<AnimeLightEntry> collection, bool isAnime)
        {
            if (!data.TryGetProperty(prop, out var entries) || entries.ValueKind != JsonValueKind.Array)
                return;
            foreach (var wrapper in entries.EnumerateArray())
            {
                try
                {
                    if (wrapper.ValueKind != JsonValueKind.Object)
                        continue;
                    var entry = wrapper;
                    if (!wrapper.TryGetProperty(prop, out var nested) || nested.ValueKind != JsonValueKind.Object)
                        nested = wrapper;
                    entry = nested;

                    var current = new AnimeLightEntry { IsAnime = isAnime };
                    current.Id = GetInt(entry, "mal_id");
                    current.Title = WebUtility.HtmlDecode(GetString(entry, "title"));
                    current.ImgUrl = GetNestedImageUrl(entry);
                    var mediaType = NormalizeMediaType(GetString(entry, "media_type"));
                    if (string.IsNullOrEmpty(mediaType))
                    {
                        var entryType = GetString(entry, "type");
                        if (!string.Equals(entryType, "anime", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(entryType, "manga", StringComparison.OrdinalIgnoreCase))
                            mediaType = NormalizeMediaType(entryType);
                    }
                    current.Notes = BuildNotes(GetString(wrapper, "role"), mediaType);
                    if (current.Id > 0 && !string.IsNullOrEmpty(current.Title))
                        collection.Add(current);
                }
                catch (Exception)
                {
                }
            }
        }

        private static int GetInt(JsonElement el, string prop)
        {
            if (!el.TryGetProperty(prop, out var p))
                return 0;
            if (p.ValueKind == JsonValueKind.Number)
                return p.GetInt32();
            return p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var value) ? value : 0;
        }

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

        private static List<string> GetStringList(JsonElement element, string property)
        {
            var output = new List<string>();
            if (!element.TryGetProperty(property, out var values))
                return output;
            if (values.ValueKind == JsonValueKind.String)
            {
                if (!string.IsNullOrWhiteSpace(values.GetString()))
                    output.Add(values.GetString());
                return output;
            }
            if (values.ValueKind != JsonValueKind.Array)
                return output;
            foreach (var value in values.EnumerateArray())
                if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                    output.Add(value.GetString());
            return output;
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

        private async Task<CharacterDetailsData> FetchFromHtmlAsync()
        {
            var output = new CharacterDetailsData();
            var raw = await GetRequestResponse();
            if (string.IsNullOrEmpty(raw))
                return output;
            var doc = new HtmlDocument();
            doc.LoadHtml(raw);

            output.Id = _id;
            try
            {
                var columns = doc.DocumentNode.Descendants("table").First().ChildNodes[1].ChildNodes.Where(node => node.Name == "td").ToList();
                var leftColumn = columns[0];
                var tables = leftColumn.Descendants("table");
                foreach (var table in tables)
                {
                    foreach (var descendant in table.Descendants("tr"))
                    {
                        var links = descendant.Descendants("a").ToList();
                        var img = links[0].Descendants("img").First().Attributes["data-src"].Value;
                        var imageUrl = "";
                        if (!img.Contains("questionmark"))
                        {
                            img = Regex.Replace(img, @"\/r\/\d+x\d+", "");
                            imageUrl = img.Substring(0, img.IndexOf('?'));
                        }
                        if (links[0].Attributes["href"].Value.Contains("/anime/"))
                        {
                            var curr = new AnimeLightEntry { IsAnime = true };
                            curr.Id = int.Parse(links[0].Attributes["href"].Value.Split('/')[4]);
                            curr.ImgUrl = imageUrl;
                            curr.Title = WebUtility.HtmlDecode(links[1].InnerText.Trim());
                            output.Animeography.Add(curr);
                        }
                        else
                        {
                            var curr = new AnimeLightEntry { IsAnime = false };
                            curr.Id = int.Parse(links[0].Attributes["href"].Value.Split('/')[4]);
                            curr.ImgUrl = imageUrl;
                            curr.Title = WebUtility.HtmlDecode(links[1].InnerText.Trim());
                            output.Mangaography.Add(curr);
                        }
                    }
                }
                var image = leftColumn.Descendants("img").First();
                if (image.Attributes.Contains("alt"))
                {
                    output.ImgUrl = image.Attributes["data-src"].Value;
                }

                output.Name = WebUtility.HtmlDecode(doc.DocumentNode.Descendants("h1").First().InnerText).Trim().Replace("  ", " ");
                output.Name = output.Name?.Split('\n')[0];
                output.Content = output.SpoilerContent = "";
                output.Content += WebUtility.HtmlDecode(leftColumn.LastChild.InnerText.Trim()) + "\n\n";
                foreach (var node in columns[1].ChildNodes)
                {
                    if (node.Name == "#text")
                        output.Content += WebUtility.HtmlDecode(node.InnerText.Trim());
                    else if (node.Name == "br" && !output.Content.EndsWith("\n\n"))
                        output.Content += "\n";
                    else if (node.Name == "div" && node.Attributes.Contains("class") && node.Attributes["class"].Value == "spoiler")
                        output.SpoilerContent += WebUtility.HtmlDecode(node.InnerText.Trim()) + "\n\n";
                    else if (node.Name == "table")
                    {
                        foreach (var descendant in node.Descendants("tr"))
                        {
                            var current = new AnimeStaffPerson();
                            var img2 = descendant.Descendants("img").First();
                            var imgUrl = img2.Attributes["data-src"].Value;
                            current.ImgUrl = imgUrl;
                            var info = descendant.Descendants("td").Last();
                            current.Id = info.ChildNodes[0].Attributes["href"].Value.Split('/')[4];
                            current.Name = WebUtility.HtmlDecode(info.ChildNodes[0].InnerText.Trim());
                            current.Notes = info.ChildNodes[2].InnerText;
                            output.VoiceActors.Add(current);
                        }
                    }
                }
                output.Content = output.Content.Trim();
                output.SpoilerContent = output.SpoilerContent.Trim();

                output.Content =
                    output.Content.Replace(
                        "No voice actors have been added to this character. Help improve our database by searching for a voice actor, and adding this character to their roles.",
                        "");
            }
            catch (Exception e)
            {
                //html
            }

            return output;
        }
    }
}
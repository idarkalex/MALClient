using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MALClient.Models.Models.Favourites;
using MALClient.XShared.Utils;

namespace MALClient.XShared.Comm.Search
{
    public class CharactersSearchQuery : Query
    {
        private readonly string _query;

        public CharactersSearchQuery(string query)
        {
            _query = query;
            Request =
                new Uri(
                    Uri.EscapeUriString($"https://myanimelist.net/character.php?q={query}"));
        }

        public async Task<List<AnimeCharacter>> GetSearchResults()
        {
            var output = await FetchFromTenraiAsync();
            if (output?.Count > 0)
                return output;

            output = new List<AnimeCharacter>();
            var raw = await GetRequestResponse();
            if (string.IsNullOrEmpty(raw))
                return null;
            var doc = new HtmlDocument();
            doc.LoadHtml(raw);

            try
            {
                foreach (var row in doc.DocumentNode.Descendants("table").FirstOrDefault()?.Descendants("tr").Skip(1) ?? Enumerable.Empty<HtmlNode>())
                {
                    try
                    {
                        var character = new AnimeCharacter();
                        var tds = row.Descendants("td").ToList();
                        var link = tds[1].Descendants("a").First();
                        character.Id = link.Attributes["href"].Value.Split('/')[4];
                        character.Name = WebUtility.HtmlDecode(link.InnerText.Trim());
                        var smalls = tds[1].Descendants("small");
                        if (smalls.Any())
                            character.Notes = WebUtility.HtmlDecode(smalls.Last().InnerText);

                        var img = tds[0].Descendants("img").First().Attributes["data-src"].Value;
                        if (!img.Contains("questionmark"))
                        {
                            img = Regex.Replace(img, @"\/r\/\d+x\d+", "");
                            character.ImgUrl = img.Substring(0, img.IndexOf('?'));
                        }

                        output.Add(character);
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

            return output;
        }

        private async Task<List<AnimeCharacter>> FetchFromTenraiAsync()
        {
            try
            {
                var data = await TenraiClient.GetDataAsync($"characters?q={Uri.EscapeDataString(_query)}");
                if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("data", out var results) && results.ValueKind == JsonValueKind.Array)
                {
                    var output = new List<AnimeCharacter>();
                    foreach (var item in results.EnumerateArray())
                    {
                        try
                        {
                            var character = new AnimeCharacter();
                            character.Id = GetIntString(item, "mal_id");
                            character.Name = WebUtility.HtmlDecode(GetString(item, "name"));
                            character.ImgUrl = GetNestedImageUrl(item);
                            var nicknames = GetStringArray(item, "nicknames");
                            var about = GetString(item, "about");
                            character.Notes = nicknames.Count > 0
                                ? string.Join(", ", nicknames)
                                : NormalizeAbout(about);
                            output.Add(character);
                        }
                        catch (Exception)
                        {
                            //
                        }
                    }
                    return output;
                }
            }
            catch (Exception)
            {
                //
            }
            return null;
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

        private static string NormalizeAbout(string about)
        {
            if (string.IsNullOrEmpty(about)) return null;
            var decoded = WebUtility.HtmlDecode(about);
            var bracketPos = decoded.IndexOf("(Source:");
            if (bracketPos > 0)
                decoded = decoded.Substring(0, bracketPos);
            decoded = Regex.Replace(decoded, "\r?\n+", " ").Trim();
            return decoded.Length > 160 ? decoded.Substring(0, 157) + "..." : decoded;
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
    }
}
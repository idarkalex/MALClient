using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MALClient.Models.Models.ApiResponses;
using MALClient.XShared.Comm;
using Newtonsoft.Json;

namespace MALClient.XShared.Comm.Search
{
    public class EverywhereSearchQuery : Query
    {
        public async Task<SearchEverywhereResponse> GetResult(string query)
        {
            try
            {
                var tenraiTasks = new List<Task<List<Item>>>();
                var categories = new List<Category>();

                try
                {
                    var animeTask = SearchTenraiCategory("anime", query, "anime");
                    var mangaTask = SearchTenraiCategory("manga", query, "manga");
                    var charTask = SearchTenraiCategory("characters", query, "character");
                    await Task.WhenAll(animeTask, mangaTask, charTask);
                    if (animeTask.Result.Count > 0) categories.Add(new Category { Type = "anime", Items = animeTask.Result });
                    if (mangaTask.Result.Count > 0) categories.Add(new Category { Type = "manga", Items = mangaTask.Result });
                    if (charTask.Result.Count > 0) categories.Add(new Category { Type = "character", Items = charTask.Result });
                }
                catch { }

                try
                {
                    var jsonUser = await _client.GetStringAsync(
                        $"https://myanimelist.net/search/prefix.json?type=user&keyword={query}&v=1");
                    var userResponse = JsonConvert.DeserializeObject<SearchEverywhereResponse>(jsonUser);
                    if (userResponse?.Categories?.Any() == true)
                    {
                        userResponse.Categories[0].Items =
                            userResponse.Categories[0].Items.OrderByDescending(item => item.EsScore).Take(5).ToList();
                        categories.Add(userResponse.Categories.First());
                    }
                }
                catch { }

                if (categories.Count > 0)
                    return new SearchEverywhereResponse { Categories = categories };

                var json = await _client.GetStringAsync(
                    $"https://myanimelist.net/search/prefix.json?type=all&keyword={query}&v=1");
                return JsonConvert.DeserializeObject<SearchEverywhereResponse>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private async Task<List<Item>> SearchTenraiCategory(string endpoint, string query, string type)
        {
            try
            {
                var pop = endpoint == "characters" ? "" : "&order_by=popularity&sort=asc";
                var (items, _) = await TenraiClient.GetPaginatedAsync($"{endpoint}?q={Uri.EscapeDataString(query)}&sfw{pop}");
                return items.Take(5).Select(el =>
                {
                    var id = el.TryGetProperty("mal_id", out var p) && p.ValueKind == System.Text.Json.JsonValueKind.Number ? p.GetInt32() : 0;
                    var title = el.TryGetProperty("title", out var tp) && tp.ValueKind == System.Text.Json.JsonValueKind.String ? tp.GetString() : "";
                    var img = "";
                    if (el.TryGetProperty("images", out var imgs) && imgs.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        if (imgs.TryGetProperty("jpg", out var jpg) && jpg.ValueKind == System.Text.Json.JsonValueKind.Object)
                            if (jpg.TryGetProperty("image_url", out var url) && url.ValueKind == System.Text.Json.JsonValueKind.String)
                                img = url.GetString();
                    }
                    var urlStr = $"https://myanimelist.net/{type}/{id}";
                    return new Item { Id = id, Type = type, Name = title, Url = urlStr, ImageUrl = img, ThumbnailUrl = img, EsScore = 1.0, Payload = new Payload() };
                }).Where(i => i.Id > 0 && !string.IsNullOrEmpty(i.Name)).ToList();
            }
            catch { return new List<Item>(); }
        }
    }
}

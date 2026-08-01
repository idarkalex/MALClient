using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MALClient.XShared.Comm
{
    public static class JikanClient
    {
        private static readonly HttpClient Client = new HttpClient();
        private static readonly SemaphoreSlim RateLimiter = new SemaphoreSlim(1, 1);
        private static DateTime _lastRequest = DateTime.MinValue;

        private const int RequestSpacingMs = 500;
        private const int MaxAttempts = 4;

        private static readonly string[] BaseUrls =
        {
            "https://api.tenrai.org/v1"
        };

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        static JikanClient()
        {
            Client.DefaultRequestHeaders.Add("User-Agent", "MALClient/3.0");
        }

        private static async Task<string> GetStringAsync(string endpoint)
        {
            Exception lastError = null;
            foreach (var baseUrl in BaseUrls)
            {
                try
                {
                    return await GetStringCoreAsync($"{baseUrl}/{endpoint}");
                }
                catch (HttpRequestException e)
                {
                    lastError = e;
                }
                catch (TaskCanceledException e)
                {
                    lastError = e;
                }
            }

            throw lastError ?? new HttpRequestException("All Jikan mirrors failed.");
        }

        private static async Task<string> GetStringCoreAsync(string url)
        {
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                HttpResponseMessage response;
                await RateLimiter.WaitAsync();
                try
                {
                    var sinceLast = DateTime.UtcNow - _lastRequest;
                    if (sinceLast.TotalMilliseconds < RequestSpacingMs)
                        await Task.Delay(RequestSpacingMs - (int)sinceLast.TotalMilliseconds);

                    _lastRequest = DateTime.UtcNow;
                    response = await Client.GetAsync(url);
                }
                finally
                {
                    RateLimiter.Release();
                }

                using (response)
                {
                    var status = (int)response.StatusCode;

                    if (status == 429)
                    {
                        var retryAfterSeconds = response.Headers.RetryAfter?.Delta?.TotalSeconds;
                        var delay = retryAfterSeconds ?? 2.0 * attempt;
                        if (attempt < MaxAttempts)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(Math.Min(delay, 15)));
                            continue;
                        }
                        throw new HttpRequestException("Jikan rate limit exceeded (429).");
                    }

                    if (status >= 500)
                    {
                        if (attempt < MaxAttempts)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
                            continue;
                        }
                        throw new HttpRequestException($"Jikan server error {(int)response.StatusCode}.");
                    }

                    if (!response.IsSuccessStatusCode)
                        throw new HttpRequestException($"Jikan request failed: {(int)response.StatusCode}");

                    return await response.Content.ReadAsStringAsync();
                }
            }

            throw new HttpRequestException("Unexpected retry exhaustion.");
        }

        public static async Task<string> GetRawJsonAsync(string endpoint)
        {
            return await GetStringAsync(endpoint);
        }

        public static async Task<JsonElement> GetDataAsync(string endpoint)
        {
            var json = await GetStringAsync(endpoint);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("data").Clone();
        }

        public static async Task<(List<JsonElement> Items, bool HasNextPage)> GetPaginatedAsync(string endpoint)
        {
            var json = await GetStringAsync(endpoint);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var data = root.GetProperty("data");
            var items = new List<JsonElement>();
            foreach (var item in data.EnumerateArray())
                items.Add(item.Clone());

            var hasNext = false;
            if (root.TryGetProperty("pagination", out var pagination))
                hasNext = pagination.GetProperty("has_next_page").GetBoolean();

            return (items, hasNext);
        }

        public static async Task<List<JsonElement>> GetAllPagesAsync(Func<int, string> endpointForPage, int maxPages = 10)
        {
            var allItems = new List<JsonElement>();
            for (int page = 1; page <= maxPages; page++)
            {
                var (items, hasNext) = await GetPaginatedAsync(endpointForPage(page));
                allItems.AddRange(items);
                if (!hasNext) break;
            }
            return allItems;
        }
    }
}

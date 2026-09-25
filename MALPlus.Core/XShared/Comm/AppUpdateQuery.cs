using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MALClient.XShared.ViewModels;

namespace MALClient.XShared.Comm
{
    public class AppUpdateInfo
    {
        public string Version { get; set; }
        public string DownloadUrl { get; set; }
        public string ReleaseName { get; set; }
        public string ReleaseNotes { get; set; }
    }

    public class AppUpdateQuery
    {
        private readonly HttpClient _httpClient = new HttpClient(ResourceLocator.MalHttpContextProvider.GetHandler());

        public AppUpdateQuery()
        {
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "MALClient-Android/1.0");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        }

        public async Task<AppUpdateInfo> GetUpdateInfo()
        {
            var json = await _httpClient.GetStringAsync("https://api.github.com/repos/idarkalex/MALClient/releases/latest");
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.TryGetProperty("tag_name", out var tag) && tag.ValueKind == JsonValueKind.String
                ? tag.GetString()
                : null;
            if (string.IsNullOrEmpty(tagName))
                return null;

            var info = new AppUpdateInfo
            {
                Version = tagName.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? tagName.Substring(1) : tagName,
                ReleaseName = root.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
                    ? name.GetString()
                    : tagName,
                ReleaseNotes = root.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String
                    ? body.GetString()
                    : ""
            };

            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var assetName = asset.TryGetProperty("name", out var an) && an.ValueKind == JsonValueKind.String
                        ? an.GetString()
                        : "";
                    if (!string.IsNullOrEmpty(assetName) && assetName.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                    {
                        info.DownloadUrl = asset.TryGetProperty("browser_download_url", out var url) && url.ValueKind == JsonValueKind.String
                            ? url.GetString()
                            : null;
                        break;
                    }
                }
            }

            return string.IsNullOrEmpty(info.DownloadUrl) ? null : info;
        }
    }
}

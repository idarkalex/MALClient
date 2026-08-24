using System;
using System.Text.RegularExpressions;
using Android.Webkit;

namespace MALClient.Android.Web
{
    public class InlineVideoWebViewClient : WebViewClient
    {
        private readonly WebView _webView;

        public InlineVideoWebViewClient(WebView webView)
        {
            _webView = webView;
        }

        public override bool ShouldOverrideUrlLoading(WebView view, string url)
        {
            if (string.IsNullOrEmpty(url))
                return true;

            if (url.StartsWith("http://") || url.StartsWith("https://"))
                return false;

            var videoId = ExtractYouTubeId(url);
            if (!string.IsNullOrEmpty(videoId))
            {
                _webView.LoadUrl($"https://www.youtube.com/embed/{videoId}");
                return true;
            }

            return true;
        }

        public static string ExtractYouTubeId(string url)
        {
            var match = Regex.Match(url, @"(?:vnd\.youtube:|v=|/embed/|youtu\.be/)([A-Za-z0-9_\-]{6,})");
            return match.Success ? match.Groups[1].Value : null;
        }

        public static string BuildVideoHtml(string url)
        {
            string embedUrl;
            var videoId = ExtractYouTubeId(url);
            if (!string.IsNullOrEmpty(videoId) && !url.Contains("listType=search"))
                embedUrl = $"https://www.youtube.com/embed/{videoId}?autoplay=1";
            else if (url.Contains("listType=search") || url.Contains("/embed"))
                embedUrl = url;
            else
                embedUrl = url;

            return "<html><head><meta name='viewport' content='width=device-width,initial-scale=1'/>" +
                   "<style>body{margin:0;padding:0;background:#000;overflow:hidden}" +
                   "iframe{position:absolute;top:0;left:0;width:100%;height:100%;border:none}</style></head>" +
                   "<body><iframe src='" + embedUrl + "' allow='autoplay;encrypted-media;fullscreen' allowfullscreen></iframe></body></html>";
        }

        public static async System.Threading.Tasks.Task<string> SearchYouTubeVideoId(string encodedQuery)
        {
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    var html = await client.GetStringAsync(
                        $"https://www.youtube.com/results?search_query={encodedQuery}");
                    var match = Regex.Match(html, @"""videoId"":""([A-Za-z0-9_\-]{11})""");
                    return match.Success ? match.Groups[1].Value : null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

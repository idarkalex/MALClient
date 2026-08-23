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
    }
}

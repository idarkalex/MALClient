using System.Text.RegularExpressions;
using Android.Webkit;

namespace MALPlus.Services;

public static class VideoWebViewHelper
{
    // Never youtube.com: YouTube refuses to configure a player whose own origin is
    // youtube.com (error 152/153, onReady then onError in the same millisecond).
    // myanimelist.net is the v2 base and is known to work.
    private const string PlaybackBaseUrl = "https://myanimelist.net";

    private const string ChromeUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private static readonly HttpClient SearchClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    /// <summary>
    ///     Set by an open video overlay so the Android back gesture closes the player instead of
    ///     navigating away from the page underneath. Returns true when it consumed the back.
    ///     MAUI 7 has no Page.BackButtonPressed and a SwipeGestureRecognizer never sees the drag
    ///     because the WebView child consumes it, so MainActivity.OnBackPressed routes here.
    /// </summary>
    public static Func<bool> BackHandler { get; set; }

    public static void ConfigurePlatformView(global::Android.Views.View platformView)
    {
        if (platformView is not global::Android.Webkit.WebView wv) return;
        try
        {
            wv.Settings.JavaScriptEnabled = true;
            wv.Settings.DomStorageEnabled = true;
            wv.Settings.MediaPlaybackRequiresUserGesture = false;
            // YouTube needs third-party cookies to authenticate video streams (blocked by default on Android 13+)
            global::Android.Webkit.CookieManager.Instance.SetAcceptThirdPartyCookies(wv, true);
            wv.SetWebChromeClient(new LoggingWebChromeClient());
            wv.SetWebViewClient(new InlineVideoWebViewClient());
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("MALPlus Video", "ConfigurePlatformView: " + ex.GetType().Name);
        }
    }

    /// <summary>
    ///     Must be called AFTER assigning <c>Source</c>. MAUI's WebView handler re-applies the
    ///     platform settings on every source update and puts
    ///     <c>MediaPlaybackRequiresUserGesture</c> back to true, which blocked the
    ///     <c>&lt;video&gt;</c> autoplay and left Chromium's grey placeholder up forever
    ///     (readyState 0, 0:00 / 0:00, no frames).
    /// </summary>
    public static void ApplyMediaSettings(global::Microsoft.Maui.Controls.WebView webView)
    {
        if (webView?.Handler?.PlatformView is not global::Android.Webkit.WebView wv) return;
        try
        {
            wv.Settings.MediaPlaybackRequiresUserGesture = false;
            wv.Settings.AllowFileAccessFromFileURLs = true;
            wv.Settings.AllowContentAccess = true;
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("MALPlus Video", "ApplyMediaSettings: " + ex.GetType().Name);
        }
    }

    public static void Resume(global::Android.Views.View platformView)
    {
        if (platformView is not global::Android.Webkit.WebView wv) return;
        try { wv.OnResume(); } catch { }
    }

    public static string ExtractYouTubeId(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        try
        {
            var match = Regex.Match(url, @"(?:vnd\.youtube:|v=|/embed/|youtu\.be/)([A-Za-z0-9_\-]{6,})");
            return match.Success ? match.Groups[1].Value : null;
        }
        catch { return null; }
    }

    private static bool IsSearchUrl(string url) =>
        !string.IsNullOrEmpty(url) && url.Contains("search_query=", StringComparison.OrdinalIgnoreCase);

    private static bool IsDirectMedia(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var lower = url.Split('?')[0].ToLowerInvariant();
        return lower.EndsWith(".webm") || lower.EndsWith(".mp4") || lower.EndsWith(".m4v")
               || lower.EndsWith(".ogg") || lower.EndsWith(".mp3") || lower.EndsWith(".m4a");
    }

    public static string ToEmbedUrl(string url, bool autoplay)
    {
        if (string.IsNullOrWhiteSpace(url)) return url;
        try
        {
            var id = ExtractYouTubeId(url);
            if (!string.IsNullOrEmpty(id))
                return $"https://www.youtube.com/embed/{id}" + (autoplay ? "?autoplay=1" : "");
            return url;
        }
        catch { return url; }
    }

    /// <summary>
    /// Single entry point for every video overlay in the app. Returns the source to hand to
    /// a WebView, already routed to the right player (YouTube iframe or direct media element).
    /// </summary>
    public static HtmlWebViewSource BuildSource(string url, bool autoplay = true)
    {
        return new HtmlWebViewSource
        {
            Html = BuildPlayerHtml(url, autoplay),
            BaseUrl = PlaybackBaseUrl
        };
    }

    public static string BuildPlayerHtml(string url, bool autoplay = true)
    {
        if (string.IsNullOrWhiteSpace(url))
            return BuildPlayerShell("<p style='color:#d4e4f7;font-family:sans-serif;padding:24px'>No video</p>");

        if (IsDirectMedia(url))
            return BuildDirectMediaHtml(url, autoplay);

        if (IsSearchUrl(url))
        {
            // A search page is not a player. Show the results so the user can pick one
            // instead of handing the iframe a URL the YouTube player cannot boot.
            var match = Regex.Match(url, @"search_query=([^&]+)");
            var results = match.Success
                ? "https://www.youtube.com/results?search_query=" + match.Groups[1].Value
                : url;
            return BuildEmbedHtml(results);
        }

        return BuildEmbedHtml(ToEmbedUrl(url, autoplay));
    }

    public static string BuildEmbedHtml(string embedUrl)
    {
        if (!embedUrl.Contains("?")) embedUrl += "?enablejsapi=1";
        else embedUrl += "&enablejsapi=1";
        return BuildPlayerShell(
            "<iframe src='" + embedUrl + "' allow='autoplay;encrypted-media;fullscreen' allowfullscreen></iframe>",
            "<script>window.addEventListener('message',function(e){try{var d=e.data||{};if(typeof d==='string'){console.log('YTRAW '+d);}else if(d.event){console.log('YTEVT '+d.event+(d.info?JSON.stringify(d.info):''));}}catch(x){}});</script>");
    }

    private static string BuildDirectMediaHtml(string url, bool autoplay)
    {
        var autoplayAttr = autoplay ? " autoplay" : "";
        return BuildPlayerShell(
            "<div id='splash'></div>" +
            "<video id='video' src='" + url + "'" + autoplayAttr + " preload='auto' playsinline webkit-playsinline></video>" +
            "<div id='controls'>" +
            "<button id='playBtn'>&#9208;</button>" +
            "<div id='progressContainer'><div id='progressFill'></div></div>" +
            "<span id='timeDisplay'>0:00 / 0:00</span></div>",
            "<script>" +
            "var v=document.getElementById('video'),splash=document.getElementById('splash')," +
            "btn=document.getElementById('playBtn'),fill=document.getElementById('progressFill')," +
            "container=document.getElementById('progressContainer'),time=document.getElementById('timeDisplay');" +
            "function fmt(s){s=Math.floor(s||0);return Math.floor(s/60)+':'+('0'+s%60).slice(-2)}" +
            "btn.onclick=function(e){e.stopPropagation();v.paused?v.play():v.pause()};" +
            "v.onplaying=function(){splash.style.opacity='0';};" +
            "v.onplay=function(){btn.innerHTML='&#9208;';};" +
            "v.onpause=function(){btn.innerHTML='&#9654;';};" +
            "v.onerror=function(){splash.style.opacity='0';console.error('video-error code='+(v.error?v.error.code:-1)+' net='+v.networkState);};" +
            "v.ontimeupdate=function(){if(v.duration){fill.style.width=((v.currentTime/v.duration)*100)+'%';time.textContent=fmt(v.currentTime)+' / '+fmt(v.duration)}};" +
            "container.onclick=function(e){e.stopPropagation();var r=container.getBoundingClientRect();v.currentTime=((e.clientX-r.left)/r.width)*v.duration;};" +
            "setTimeout(function(){if(splash.style.opacity!=='0'){splash.style.opacity='0';console.log('DBG: splash timeout');}},5000);" +
            // Chromium will not start a media element on an attribute alone when the page was not
            // opened by a gesture, and a rejected play() leaves the grey placeholder up forever.
            // Kick the load and retry a few times so a slow CDN does not read as "broken".
            "function kick(){v.load();var p=v.play();if(p&&p.catch){p.catch(function(e){console.log('play-rejected '+e.name);setTimeout(function(){v.play().catch(function(){});},900);});}}" +
            "v.addEventListener('canplay',function(){kick();},{once:true});" +
            "v.addEventListener('loadedmetadata',function(){kick();},{once:true});" +
            "v.addEventListener('error',function(){console.error('video-error code='+(v.error?v.error.code:-1)+' net='+v.networkState);});" +
            "kick();" +
            "</script>",
            directMedia: true);
    }

    /// <summary>Resolution spinner shown while we look for the OP/ED source.</summary>
    public static string BuildLoadingHtml()
    {
        return "<html><head><meta name='viewport' content='width=device-width,initial-scale=1'/><style>" +
               "@keyframes spin{to{transform:rotate(360deg)}}" +
               "body{margin:0;padding:0;background:#000;height:100%;overflow:hidden}" +
               "#s{position:absolute;top:50%;left:50%;width:44px;height:44px;margin:-22px 0 0 -22px;" +
               "border:4px solid rgba(255,255,255,0.2);border-top-color:#0066FF;border-radius:50%;animation:spin 1s linear infinite}" +
               "</style></head><body><div id='s'></div></body></html>";
    }

    private static string BuildPlayerShell(string body, string script = null, bool directMedia = false)
    {
        var videoStyle = directMedia
            ? "#video{position:absolute;top:0;left:0;width:100%;height:calc(100% - 48px);object-fit:contain;background:#000}" +
              "#splash{position:absolute;top:0;left:0;width:100%;height:100%;background:#000;z-index:20;transition:opacity 0.3s;pointer-events:none}" +
              "#controls{position:absolute;bottom:0;left:0;right:0;height:48px;background:rgba(5,21,34,0.92);display:flex;align-items:center;padding:0 12px;z-index:10}" +
              "#playBtn{background:none;border:none;color:#fff;font-size:20px;cursor:pointer;margin-right:10px;padding:4px}" +
              "#progressContainer{flex:1;height:4px;background:rgba(255,255,255,0.15);border-radius:2px;cursor:pointer;position:relative}" +
              "#progressFill{height:100%;background:#0066FF;border-radius:2px;width:0%;pointer-events:none}" +
              "#timeDisplay{color:#d4e4f7;font-size:11px;margin-left:10px;white-space:nowrap}"
            : "iframe{position:absolute;top:0;left:0;width:100%;height:100%;border:none}";

        return "<html><head><meta name='viewport' content='width=device-width,initial-scale=1'/>" +
               "<style>*{box-sizing:border-box}" +
               "body{margin:0;padding:0;background:#000;overflow:hidden;font-family:sans-serif;user-select:none}" +
               videoStyle + "</style></head><body>" + body +
               (string.IsNullOrEmpty(script) ? "" : script) +
               "</body></html>";
    }

    /// <summary>
    /// Scrapes the first video id out of a YouTube search results page. Used as the
    /// secondary source when AnimeThemes has no usable match.
    /// </summary>
    public static async Task<string> SearchYouTubeVideoId(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        try
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                "https://www.youtube.com/results?search_query=" + System.Net.WebUtility.UrlEncode(query));
            request.Headers.TryAddWithoutValidation("User-Agent", ChromeUserAgent);
            using var response = await SearchClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;
            var html = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(html)) return null;
            var match = Regex.Match(html, "\"videoId\":\"([A-Za-z0-9_\\-]{11})\"");
            return match.Success ? match.Groups[1].Value : null;
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("MALPlus Video", "YouTube search failed: " + ex.GetType().Name);
            return null;
        }
    }

    /// <summary>
    /// Keeps http(s) inside the overlay and rewrites the YouTube app hand-off schemes
    /// (vnd.youtube:, intent://) back into the embed player. Without this the WebView
    /// escapes to the YouTube app instead of playing in place.
    /// </summary>
    private sealed class InlineVideoWebViewClient : WebViewClient
    {
        public override bool ShouldOverrideUrlLoading(global::Android.Webkit.WebView view, IWebResourceRequest request)
        {
            var url = request?.Url?.ToString();
            if (string.IsNullOrEmpty(url)) return false;
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return false;
            var id = ExtractYouTubeId(url);
            if (!string.IsNullOrEmpty(id))
            {
                view.LoadUrl($"https://www.youtube.com/embed/{id}?autoplay=1");
                return true;
            }
            return true;
        }
    }

    private sealed class LoggingWebChromeClient : WebChromeClient
    {
        public override bool OnConsoleMessage(ConsoleMessage consoleMessage)
        {
            var msg = consoleMessage?.Message();
            if (!string.IsNullOrWhiteSpace(msg))
                global::Android.Util.Log.Info("MALPlus VideoOverlay", msg);
            return base.OnConsoleMessage(consoleMessage);
        }
    }
}

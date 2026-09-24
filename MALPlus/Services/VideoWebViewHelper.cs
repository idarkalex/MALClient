using System.Text.RegularExpressions;
using Android.Webkit;

namespace MALPlus.Services;

public static class VideoWebViewHelper
{
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
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS ConfigurePlatformView: " + ex.GetType().Name);
        }
    }

    public static void Resume(global::Android.Views.View platformView)
    {
        if (platformView is not global::Android.Webkit.WebView wv) return;
        try { wv.OnResume(); } catch { }
    }

    public static string ToEmbedUrl(string url, bool autoplay)
    {
        try
        {
            var m = Regex.Match(url, @"youtube\.com/watch\?v=([\w\-]+)");
            if (m.Success) return $"https://www.youtube.com/embed/{m.Groups[1].Value}" + (autoplay ? "?autoplay=1" : "");
            m = Regex.Match(url, @"youtu\.be/([\w\-]+)");
            if (m.Success) return $"https://www.youtube.com/embed/{m.Groups[1].Value}" + (autoplay ? "?autoplay=1" : "");
            return url;
        }
        catch { return url; }
    }

    public static string BuildEmbedHtml(string embedUrl)
    {
        embedUrl = embedUrl.Contains("?") ? embedUrl + "&enablejsapi=1" : embedUrl + "?enablejsapi=1";
        return "<html><head><meta name='viewport' content='width=device-width,initial-scale=1'/>" +
               "<style>body{margin:0;padding:0;background:#000;overflow:hidden}" +
               "iframe{position:absolute;top:0;left:0;width:100%;height:100%;border:none}</style></head>" +
               "<body><iframe src='" + embedUrl + "' allow='autoplay;encrypted-media;fullscreen' allowfullscreen></iframe>" +
               "<script>window.addEventListener('message',function(e){try{var d=e.data||{};if(typeof d==='string'){console.log('YTRAW '+d);}else if(d.event){console.log('YTEVT '+d.event+(d.info?JSON.stringify(d.info):''));}}catch(x){}});</script>" +
               "</body></html>";
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
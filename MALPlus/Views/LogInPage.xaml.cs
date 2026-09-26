using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALPlus.Views;

public partial class LogInPage : ContentPage
{
    private bool _initialized;
    private bool _autoMode;
    private bool _autoSubmitted;
    private bool _loginPageReady;
    private string _autoUser = string.Empty;
    private string _autoPass = string.Empty;
    private CancellationTokenSource _autoCts;

    private LogInViewModel Vm => (LogInViewModel)BindingContext;

    public LogInPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.LogIn;
#if ANDROID
        AuthWebView.HandlerChanged += OnAuthWebViewHandlerChanged;
#endif
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized)
            return;
        _initialized = true;
        try
        {
            Vm.Init();
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS login Init failed: " + ex.Message);
        }
        try
        {
            if (!Credentials.Authenticated)
            {
#if ANDROID
                Android.Webkit.CookieManager.Instance.SetAcceptCookie(true);
#endif
                AuthWebView.Source = "https://myanimelist.net/login.php";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS login prewarm failed: " + ex.Message);
        }
    }

    private void OnAutoSignInClicked(object sender, EventArgs e)
    {
        try
        {
            var user = (UserEntry.Text ?? string.Empty).Trim();
            var pass = PassEntry.Text ?? string.Empty;
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                ShowStatus("Enter your MyAnimeList username and password.");
                return;
            }
            _autoUser = user;
            _autoPass = pass;
            _autoMode = true;
            _autoSubmitted = false;
            LoginForm.IsEnabled = false;
            ShowBusy(true, "Connecting to MyAnimeList…");
#if ANDROID
            Android.Webkit.CookieManager.Instance.SetAcceptCookie(true);
            ForgetMalSession();
#endif
            _autoCts?.Cancel();
            _autoCts = new CancellationTokenSource();
            _ = AutoTimeoutAsync(_autoCts.Token);
            if (_loginPageReady && AuthWebView.Source is UrlWebViewSource { Url: string readyUrl }
                && readyUrl.Contains("login.php"))
            {
                _ = AutoFillAndSubmitAsync();
            }
            else
            {
                _loginPageReady = false;
                _ = LoadLoginFormAsync(_autoCts.Token);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS auto signin failed: " + ex.Message);
            ResetToForm("Could not start login. Try again.");
        }
    }

    private async Task LoadLoginFormAsync(CancellationToken token)
    {
        try
        {
            // RemoveAllCookies is asynchronous on the platform side, so the cookie jar is
            // not necessarily empty yet when we ask for the login form. Without this pause
            // the still-authenticated redirect to the consent page can win the race.
            await Task.Delay(400, token);
            AuthWebView.Source = "https://myanimelist.net/login.php";
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS login form load failed: " + ex.Message);
        }
    }

    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

    private void OnAuthWebViewHandlerChanged(object sender, EventArgs e)
    {
        try
        {
#if ANDROID
            // MyAnimeList serves "Google reCAPTCHA is currently blocked" inside the
            // default Android WebView, because the stock WebView user agent is what its
            // bot detection keys on. Presenting a desktop Chrome is what unblocks it.
            var platform = AuthWebView?.Handler?.PlatformView as global::Android.Webkit.WebView;
            if (platform == null)
                return;
            var settings = platform.Settings;
            if (settings != null && settings.UserAgentString != DesktopUserAgent)
                settings.UserAgentString = DesktopUserAgent;
            var cookies = global::Android.Webkit.CookieManager.Instance;
            cookies.SetAcceptCookie(true);
            cookies.SetAcceptThirdPartyCookies(platform, true);
#endif
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS auth webview setup failed: " + ex.Message);
        }
    }

    /// <summary>
    /// MAL+ signs in with the username and password the user typed, so the MAL session in
    /// the WebView has to be gone first. With a live session, login.php redirects straight
    /// to the OAuth consent page: the login form never renders, AutoFillAndSubmitAsync
    /// finds no fields, and the app asks for consent and hands over a token for the
    /// previously logged-in account no matter what was typed. The credentials were never
    /// checked, which is exactly the "it signs in with anything" behaviour.
    /// </summary>
    private static void ForgetMalSession()
    {
#if ANDROID
        try
        {
            var cookies = Android.Webkit.CookieManager.Instance;
            // Only the MAL session cookies. RemoveAllCookies also wiped the 13-month
            // privacy-consent cookie, so the consent wall came back on every single login.
            foreach (var name in new[] { "MAL_SESSION", "MAL_SESSIONSS", "MAL_LANG" })
            {
                cookies.SetCookie("https://myanimelist.net",
                    name + "=; Max-Age=0; Path=/; Domain=.myanimelist.net");
            }
            cookies.RemoveSessionCookies(null);
            cookies.Flush();
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS cookie wipe failed: " + ex.Message);
        }
#endif
    }

    private async Task AutoTimeoutAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            if (_autoMode)
                FallbackToManual("Taking too long. Continue here.");
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void OnAuthNavigated(object sender, WebNavigatedEventArgs e)
    {
        try
        {
            var url = e.Url ?? string.Empty;
            Probe("NAVIGATED " + url);
            if (url.Contains("login.php"))
                _loginPageReady = true;
            if (!_autoMode)
                return;
            // Hard guarantee: while we drive the sign-in ourselves, MyAnimeList's pages are
            // never on screen. Whatever it renders - the privacy wall, the consent screen,
            // a 400 - stays behind our own form.
            WebOverlay.IsVisible = false;
            if (!url.Contains("login.php"))
            {
                if (_autoSubmitted && url.Contains("login"))
                {
                    FailAuto("Wrong username or password.");
                    return;
                }
                _ = InspectAndFailIfErrorAsync();
                return;
            }
            if (_autoSubmitted)
            {
                _ = DiagnoseReturnToLoginAsync();
                return;
            }
            _ = AutoFillAndSubmitAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS auth navigated failed: " + ex.Message);
        }
    }

    /// <summary>
    /// MyAnimeList answers a rejected sign-in with its own error page ("400 Bad Request -
    /// please return to the previous screen"). Rendering it and then bouncing back to
    /// login.php is what made the user stare at MAL's site, so the page is inspected while
    /// it is still hidden and reported through our own message instead.
    /// </summary>
    private async Task InspectAndFailIfErrorAsync()
    {
        try
        {
            var js = "(function(){"
                + "var t=(document.title||'')+' '+(document.body?document.body.innerText:'');"
                + "if(/400\\s*bad request|bad request|403|forbidden|too many requests|access denied|error occurred/i.test(t))return 'error';"
                + "if(/we value your privacy/i.test(t))return 'wall';"
                + "return 'ok';})();";
            var result = await MainThread.InvokeOnMainThreadAsync(() => AuthWebView.EvaluateJavaScriptAsync(js));
            Probe("page check=" + result);
            if (result == null || !result.Contains("error"))
                return;
            if (!_autoMode)
                return;
            WebOverlay.IsVisible = false;
            FailAuto("Wrong username or password.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS page check failed: " + ex.Message);
        }
    }

    private async Task AutoFillAndSubmitAsync()
    {
        try
        {
            await AutoFillAndSubmitAsync(_autoCts?.Token ?? CancellationToken.None);
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS autofill failed: " + ex.Message);
            FallbackToManual("Could not fill the login form. Continue here.");
        }
    }

    private async Task AutoFillAndSubmitAsync(CancellationToken token)
    {
        try
        {
            var js = "(function(){"
                + "var wt=document.body&&/we value your privacy/i.test(document.body.innerText);"
                + "if(wt)return 'wall';"
                + "var u=document.querySelector('input[name=\"user_name\"]')||document.querySelector('input[type=\"text\"]');"
                + "var p=document.querySelector('input[name=\"password\"]')||document.querySelector('input[type=\"password\"]');"
                + "if(!u||!p)return 'nofields';"
                + "u.focus();u.value='" + JsEscape(_autoUser) + "';u.dispatchEvent(new Event('input',{bubbles:true}));"
                + "p.focus();p.value='" + JsEscape(_autoPass) + "';p.dispatchEvent(new Event('input',{bubbles:true}));"
                + "var f=u.form||p.form;"
                + "var b=document.querySelector('input[type=\"submit\"]')||document.querySelector('button[type=\"submit\"]')||document.querySelector('.btn-submit');"
                + "window.__malplusP=!!(f&&b);"
                + "if(b){b.click();}"
                + "return 'filled';})();";
            var result = await MainThread.InvokeOnMainThreadAsync(() => AuthWebView.EvaluateJavaScriptAsync(js));
            Probe("autofill result=" + result);
            if (result != null && result.Contains("wall"))
            {
                // MyAnimeList gates the login form behind its privacy notice. The user is
                // never supposed to see MAL's pages, so accept it ourselves and retry.
                if (await TryClickButtonAsync("privacy", "agree", "accept", "i agree", "allow", "accept all"))
                {
                    ShowBusy(true, "Accepting MyAnimeList's privacy notice…");
                    await Task.Delay(2500, token);
                    await AutoFillAndSubmitAsync(token);
                    return;
                }
                FallbackToManual("Could not pass MyAnimeList's privacy notice.");
            }
            else if (result != null && result.Contains("filled"))
            {
                // Submitting is the hard part. MyAnimeList's form needs its own submit
                // handler to run: a synthetic click is untrusted and gets ignored, and a
                // bare form.submit() skips the handler, so MAL answers the POST with a
                // 400 that has nothing to do with the password. Try all three, checking
                // after each whether the page actually moved on.
                _autoSubmitted = true;
                ShowBusy(true, "Verifying your credentials…");
                _autoCts?.Cancel();
                _autoCts = new CancellationTokenSource();
                _ = AutoTimeoutAsync(_autoCts.Token);
                // The token of the CTS we just cancelled is dead, so the submit loop has to
                // use the new one; reusing the old one aborted the attempt instantly and
                // reported "could not fill the login form" right after a successful submit.
                if (await SubmitWithFallbacksAsync(_autoCts.Token))
                    return;
                FailAuto("Could not submit the login form. Try again.");
            }
            else
            {
                FallbackToManual("Could not fill the login form.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS autofill failed: " + ex.Message);
            FallbackToManual("Could not fill the login form.");
        }
    }

    private async Task<bool> SubmitWithFallbacksAsync(CancellationToken token)
    {
        const string stillHere = "(function(){return document.querySelector('input[type=\"password\"]')?'here':'gone';})();";
        var attempts = new (string Name, string Js)[]
        {
            ("submit-event",
                "(function(){var f=document.querySelector('form');if(!f)return 'noform';"
                + "return f.dispatchEvent(new Event('submit',{bubbles:true,cancelable:true}))?'dispatched':'blocked';})();"),
            ("prototype-submit",
                "(function(){var f=document.querySelector('form');if(!f)return 'noform';"
                + "HTMLFormElement.prototype.submit.call(f);return 'submitted';})();"),
        };
        foreach (var attempt in attempts)
        {
            try
            {
                var result = await MainThread.InvokeOnMainThreadAsync(() => AuthWebView.EvaluateJavaScriptAsync(attempt.Js));
                Probe("submit " + attempt.Name + "=" + result);
            }
            catch (Exception ex)
            {
                Console.WriteLine("MALPLUS submit " + attempt.Name + " failed: " + ex.Message);
            }
            for (var waited = 0; waited < 12; waited++)
            {
                try
                {
                    await Task.Delay(500, token);
                }
                catch (TaskCanceledException)
                {
                    return false;
                }
                var here = await MainThread.InvokeOnMainThreadAsync(() => AuthWebView.EvaluateJavaScriptAsync(stillHere));
                if (here != null && here.Contains("gone"))
                    return true;
            }
        }
        return false;
    }

    private async Task DiagnoseReturnToLoginAsync()
    {
        try
        {
            var js = "(function(){"
                // MyAnimeList loads an invisible reCAPTCHA on every login page, so a
                // captcha element existing means nothing. Only a rendered one counts.
                + "var els=document.querySelectorAll('iframe[src*=\"recaptcha\"],iframe[title*=\"challenge\"],div.g-recaptcha,#captcha,div[class*=\"captcha\"]');"
                + "for(var i=0;i<els.length;i++){"
                + "var r=els[i].getBoundingClientRect();"
                + "var st=window.getComputedStyle(els[i]);"
                + "if(r.width>40&&r.height>20&&st.display!=='none'&&st.visibility!=='hidden')return 'challenge';}"
                + "var t=document.body?document.body.innerText:'';"
                // Always report what the page actually says. Deciding from keywords alone
                // is how a rejected password and a malformed automated submit got mixed up.
                + "var e=document.querySelector('.error,.error-message,#error,.form-error,div[class*=\"error\"],div[class*=\"Error\"]');"
                + "var msg=(e?((e.innerText||e.value||'')+''):'')||((t.match(/[^\\n]*(incorrect|invalid|wrong|password|error|denied|400)[^\\n]*/i)||[''])[0]);"
                + "var head=(document.title||'')+' >> '+msg.trim().slice(0,160);"
                + "if(/400|bad request/i.test(t))return 'denied|'+head;"
                + "if(/incorrect|invalid|wrong|failed|error|denied/i.test(t))return 'denied|'+head;"
                + "if(/not a robot|verification required/i.test(t))return 'challenge|'+head;"
                + "return 'unknown|'+head;})();";
            var result = await MainThread.InvokeOnMainThreadAsync(() => AuthWebView.EvaluateJavaScriptAsync(js));
            Probe("diagnose result=" + result);
            if (result != null && result.Contains("challenge"))
                FallbackToManual("MyAnimeList asks for verification. Complete it here.");
            else
                FailAuto("Wrong username or password.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS diagnose failed: " + ex.Message);
            FailAuto("Wrong username or password.");
        }
    }

    private async Task AcceptConsentOrFallBackAsync()
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            if (!_autoMode)
                return;
            await Task.Delay(1200);
            if (await TryClickButtonAsync("consent", "allow", "authorize", "accept", "continue"))
                return;
        }
        if (!_autoMode)
            return;
        _autoCts?.Cancel();
        OverlayHint.Text = "Review and tap Allow to connect MAL+ to your account";
        OverlayHint.IsVisible = true;
        WebOverlay.IsVisible = true;
    }

    /// <summary>
    /// Clicks a button on the MyAnimeList page by its label. The whole point of the
    /// WebView here is to be invisible: the privacy notice and the OAuth consent screen
    /// both need a click, and the user must never be the one making it.
    /// </summary>
    private async Task<bool> TryClickButtonAsync(string what, params string[] labels)
    {
        try
        {
            var wanted = string.Join(",", labels.Select(l => "'" + JsEscape(l.ToLowerInvariant()) + "'"));
            var js = "(function(){"
                + "var want=[" + wanted + "];"
                + "var nodes=document.querySelectorAll('button,input[type=submit],input[type=button],a.btn,div.btn');"
                + "for(var i=0;i<nodes.length;i++){"
                + "var n=nodes[i];"
                + "if(!n||n.disabled)continue;"
                + "var t=((n.value||n.innerText||n.textContent||'')+'').trim().toLowerCase();"
                + "if(!t)continue;"
                + "for(var j=0;j<want.length;j++){"
                + "if(t===want[j]||t.indexOf(want[j])>=0){n.click();return 'clicked:'+t;}}}"
                + "return 'notfound';})();";
            var result = await MainThread.InvokeOnMainThreadAsync(() => AuthWebView.EvaluateJavaScriptAsync(js));
            Probe(what + " click=" + result);
            return result != null && result.StartsWith("clicked", StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS " + what + " click failed: " + ex.Message);
            return false;
        }
    }

    private static string JsEscape(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", string.Empty).Replace("\r", string.Empty);
    }

    private void OnWebCloseClicked(object sender, EventArgs e)
    {
        ResetToForm(null);
    }

    private void ShowBusy(bool busy, string status)
    {
        BusySpinner.IsRunning = busy;
        BusySpinner.IsVisible = busy;
        ShowStatus(status);
    }

    private void ShowStatus(string status)
    {
        if (string.IsNullOrEmpty(status))
        {
            StatusLabel.IsVisible = false;
            return;
        }
        StatusLabel.Text = status;
        StatusLabel.IsVisible = true;
    }

    private void FailAuto(string message)
    {
        ResetToForm(message);
    }

    /// <summary>
    /// The automatic sign-in gave up. MyAnimeList's page is NOT shown: the app says what
    /// happened on our own screen and only reveals the WebView if the user explicitly asks
    /// for it. This used to flip the overlay on by itself, which is how MAL kept ending up
    /// in the foreground.
    /// </summary>
    private void FallbackToManual(string hint)
    {
        try
        {
            Probe("fallback: " + hint);
            _autoMode = false;
            _autoSubmitted = false;
            _autoCts?.Cancel();
            ShowBusy(false, null);
            LoginForm.IsEnabled = true;
            WebOverlay.IsVisible = false;
            ManualLoginButton.IsVisible = true;
            ShowStatus(string.IsNullOrEmpty(hint) ? "Sign-in needs one more step." : hint);
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS fallback failed: " + ex.Message);
        }
    }

    private void OnManualLoginClicked(object sender, EventArgs e)
    {
        try
        {
            ManualLoginButton.IsVisible = false;
            OverlayHint.Text = "Sign in with MyAnimeList, then tap Close";
            OverlayHint.IsVisible = true;
            if (AuthWebView.Source is not UrlWebViewSource { Url: string u }
                || (!u.Contains("login.php") && !u.Contains("oauth2/authorize") && !u.Contains("dialog/")))
            {
                AuthWebView.Source = "https://myanimelist.net/login.php";
            }
            WebOverlay.IsVisible = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS manual login failed: " + ex.Message);
        }
    }

    private void ResetToForm(string message)
    {
        try
        {
            _autoCts?.Cancel();
            _autoMode = false;
            _autoSubmitted = false;
            _autoUser = string.Empty;
            _autoPass = string.Empty;
            try
            {
                Vm.PasswordInput = string.Empty;
                PassEntry.Text = string.Empty;
            }
            catch
            {
            }
            WebOverlay.IsVisible = false;
            OverlayHint.IsVisible = false;
            LoginForm.IsVisible = true;
            LoginForm.IsEnabled = true;
            ShowBusy(false, message);
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS reset form failed: " + ex.Message);
        }
    }

    private static void Probe(string text)
    {
        try
        {
            File.AppendAllText(Path.Combine(FileSystem.CacheDirectory, "maui_login_probe.txt"),
                DateTime.UtcNow.ToString("HH:mm:ss") + " " + text + "\n");
        }
        catch
        {
        }
#if ANDROID
        try
        {
            // The probe file lives in the app's cache, which cannot be read without a
            // debuggable build, and a failing sign-in is exactly when the navigation
            // history matters. Logcat is readable from adb.
            Android.Util.Log.Info("MALPlusAuth", text);
        }
        catch
        {
        }
#endif
    }

    private string _cookies;

    private void OnAuthNavigating(object sender, WebNavigatingEventArgs e)
    {
        try
        {
            var url = e.Url ?? string.Empty;
            Probe("NAVIGATING " + url);
            if (url.Contains("maloauth?state=signin&error="))
            {
                e.Cancel = true;
                var denied = _autoMode;
                ResetToForm(denied ? "Connection cancelled. Tap Sign in to retry." : null);
                if (!denied)
                    MainThread.BeginInvokeOnMainThread(() => WebOverlay.IsVisible = false);
                Vm.FailedSignIn();
                return;
            }
            if (url.Contains("maloauth"))
            {
                var match = System.Text.RegularExpressions.Regex.Matches(url, ".*maloauth\\?code=(.*)\\&.*");
                var hadAuto = _autoMode;
                _autoCts?.Cancel();
                _autoMode = false;
                _autoSubmitted = false;
                _autoUser = string.Empty;
                _autoPass = string.Empty;
                try
                {
                    Vm.PasswordInput = string.Empty;
                    PassEntry.Text = string.Empty;
                }
                catch
                {
                }
                MainThread.BeginInvokeOnMainThread(() => WebOverlay.IsVisible = false);
                if (match.Count > 0)
                {
                    e.Cancel = true;
                    Probe("oauth code captured");
                    ShowBusy(true, "Signing you in…");
                    Vm.SignIn(_cookies ?? string.Empty, match[0].Groups[1].Value);
                }
                else
                {
                    if (hadAuto)
                        ResetToForm("Could not complete the connection.");
                    else
                        ResetToForm(null);
                    Vm.FailedSignIn();
                }
                return;
            }
            if (url == "https://myanimelist.net/" || url == "https://myanimelist.net/#"
                || url.StartsWith("https://myanimelist.net/#"))
            {
                e.Cancel = true;
#if ANDROID
                try
                {
                    _cookies = Android.Webkit.CookieManager.Instance.GetCookie("https://myanimelist.net");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("MALPLUS cookie read failed: " + ex.Message);
                }
#endif
                AuthWebView.Source = "https://myanimelist.net/v1/oauth2/authorize?response_type=code&"
                    + "client_id=030f8e30cb57bce625dda6ca8637b75e&"
                    + "state=signin&"
                    + $"code_challenge={Vm.PkceChallenge}&"
                    + "code_challenge_method=plain";
                return;
            }
            if (url.StartsWith("https://myanimelist.net/v1/oauth2/authorize")
                || url.StartsWith("https://myanimelist.net/dialog/"))
            {
                if (_autoMode)
                {
                    // The credentials were accepted, so MyAnimeList is only asking to let
                    // MAL+ use the account. The user never sees this page: accept for them
                    // and keep our own login UI on screen. If the click does not land we
                    // fall back to showing MAL's page rather than hanging forever.
                    ShowBusy(true, "Connecting your account…");
                    _ = AcceptConsentOrFallBackAsync();
                }
                return;
            }
            if (url == "https://myanimelist.net/register.php"
                || url.Contains("consent") || url.Contains("quantcast") || url.Contains("sourcepoint")
                || url.Contains("privacy-mgmt") || url.Contains("trustarc") || url.Contains("cookielaw")
                || url.Contains("google") || url.Contains("facebook") || url.Contains("apple")
                || url.StartsWith("https://api.twitter") || url.StartsWith("https://myanimelist.net/sns/")
                || url.StartsWith("https://accounts.google") || url.StartsWith("https://accounts.youtube")
                || url.StartsWith("https://myanimelist.net/login.php"))
            {
                return;
            }
            e.Cancel = true;
            AuthWebView.Source = "https://myanimelist.net/login.php";
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS auth nav failed: " + ex.Message);
        }
    }
}

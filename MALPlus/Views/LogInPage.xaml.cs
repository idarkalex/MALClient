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
                AuthWebView.Source = "https://myanimelist.net/login.php";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS auto signin failed: " + ex.Message);
            ResetToForm("Could not start login. Try again.");
        }
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
            if (url.Contains("login.php"))
                _loginPageReady = true;
            if (!_autoMode || WebOverlay.IsVisible)
                return;
            if (!url.Contains("login.php"))
            {
                if (_autoSubmitted && url.Contains("login"))
                    FailAuto("Wrong username or password.");
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

    private async Task AutoFillAndSubmitAsync()
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
                + "var b=document.querySelector('input[type=\"submit\"]')||document.querySelector('button[type=\"submit\"]');"
                + "if(b){b.click();return 'submitted';}"
                + "var f=u.form||p.form;if(f){f.submit();return 'submitted';}"
                + "return 'nosubmit';})();";
            var result = await MainThread.InvokeOnMainThreadAsync(() => AuthWebView.EvaluateJavaScriptAsync(js));
            Probe("autofill result=" + result);
            if (result != null && result.Contains("wall"))
            {
                FallbackToManual("Tap AGREE on the privacy notice, then Close and retry.");
            }
            else if (result != null && result.Contains("submitted"))
            {
                _autoSubmitted = true;
                ShowBusy(true, "Verifying your credentials…");
                _autoCts?.Cancel();
                _autoCts = new CancellationTokenSource();
                _ = AutoTimeoutAsync(_autoCts.Token);
            }
            else
            {
                FallbackToManual("Could not fill the login form. Continue here.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS autofill failed: " + ex.Message);
            FallbackToManual("Could not fill the login form. Continue here.");
        }
    }

    private async Task DiagnoseReturnToLoginAsync()
    {
        try
        {
            var js = "(function(){var t=document.body?document.body.innerText:'';"
                + "if(/captcha|recaptcha|not a robot|verification required/i.test(t))return 'challenge';"
                + "if(/incorrect|invalid|wrong|failed|error/i.test(t))return 'denied';"
                + "return 'unknown';})();";
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
            OverlayHint.Text = hint;
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
            Console.WriteLine("MALPLUS fallback failed: " + ex.Message);
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
    }

    private string _cookies;

    private void OnAuthNavigating(object sender, WebNavigatingEventArgs e)
    {
        try
        {
            var url = e.Url ?? string.Empty;
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
                    _autoCts?.Cancel();
                    OverlayHint.Text = "Review and tap Allow to connect MAL+ to your account";
                    OverlayHint.IsVisible = true;
                    WebOverlay.IsVisible = true;
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

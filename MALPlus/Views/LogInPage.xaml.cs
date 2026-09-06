using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALPlus.Views;

public partial class LogInPage : ContentPage
{
    private bool _initialized;

    private LogInViewModel Vm => (LogInViewModel)BindingContext;

    public LogInPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.LogIn;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        DebugLabel.Text = "authed=" + Credentials.Authenticated + " user=" + Credentials.UserName;
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
    }

    private string _cookies;

    private void OnWebSignInClicked(object sender, EventArgs e)
    {
        try
        {
#if ANDROID
            Android.Webkit.CookieManager.Instance.SetAcceptCookie(true);
#endif
            WebOverlay.IsVisible = true;
            AuthWebView.Source = "https://myanimelist.net/login.php";
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS web signin failed: " + ex.Message);
        }
    }

    private void OnWebCloseClicked(object sender, EventArgs e)
    {
        WebOverlay.IsVisible = false;
    }

    private void OnAuthNavigating(object sender, WebNavigatingEventArgs e)
    {
        try
        {
            var url = e.Url ?? string.Empty;
            if (url.Contains("maloauth?state=signin&error="))
            {
                e.Cancel = true;
                MainThread.BeginInvokeOnMainThread(() => WebOverlay.IsVisible = false);
                Vm.FailedSignIn();
                return;
            }
            if (url.Contains("maloauth"))
            {
                var match = System.Text.RegularExpressions.Regex.Matches(url, ".*maloauth\\?code=(.*)\\&.*");
                MainThread.BeginInvokeOnMainThread(() => WebOverlay.IsVisible = false);
                if (match.Count > 0)
                {
                    e.Cancel = true;
                    Vm.SignIn(_cookies ?? string.Empty, match[0].Groups[1].Value);
                }
                else
                {
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
                    + "client_id=183063f74126e7551b00c3b4de66986c&"
                    + "state=signin&"
                    + $"code_challenge={Vm.PkceChallenge}&"
                    + "code_challenge_method=plain";
                return;
            }
            if (url == "https://myanimelist.net/register.php"
                || url.Contains("google") || url.Contains("facebook") || url.Contains("apple")
                || url.StartsWith("https://api.twitter") || url.StartsWith("https://myanimelist.net/sns/")
                || url.StartsWith("https://accounts.google") || url.StartsWith("https://accounts.youtube")
                || url.StartsWith("https://myanimelist.net/login.php"))
                return;
            e.Cancel = true;
            AuthWebView.Source = "https://myanimelist.net/login.php";
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS auth nav failed: " + ex.Message);
        }
    }
}

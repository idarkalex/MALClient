using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Microsoft.Maui;

namespace MALPlus;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, Exported = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter(new[] { Microsoft.Maui.ApplicationModel.Platform.Intent.ActionAppAction },
    Categories = new[] { Android.Content.Intent.CategoryDefault })]
[IntentFilter(new[] { Android.Content.Intent.ActionView },
    Categories = new[] { Android.Content.Intent.CategoryDefault, Android.Content.Intent.CategoryBrowsable },
    DataScheme = "malplus")]
public class MainActivity : MauiAppCompatActivity
{
    public static Intent DeepLinkIntent { get; private set; }

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        if (Intent?.Data != null && Intent.Data.ToString().StartsWith("malplus://"))
        {
            DeepLinkIntent = Intent;
            HandleDeepLinkNavigation(Intent.Data.ToString());
        }
    }

    protected override void OnNewIntent(Intent intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;
        if (intent?.Data != null && intent.Data.ToString().StartsWith("malplus://"))
        {
            DeepLinkIntent = intent;
            HandleDeepLinkNavigation(intent.Data.ToString());
        }
    }

    private void HandleDeepLinkNavigation(string uri)
    {
        if (uri.StartsWith("malplus://"))
        {
            var path = uri.Replace("malplus://", "");
            System.Diagnostics.Debug.WriteLine($"MALPLUS Deep link path: {path}");
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"MALPLUS GoToAsync: {path}");
                    if (Shell.Current != null)
                    {
                        await Shell.Current.GoToAsync(path);
                        System.Diagnostics.Debug.WriteLine($"MALPLUS GoToAsync success");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("MALPLUS Shell.Current is null!");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Deep link failed: {ex.Message} | Stack: {ex.StackTrace}");
                }
            });
        }
    }
}

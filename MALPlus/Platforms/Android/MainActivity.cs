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
    protected override void OnNewIntent(Intent intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;
        System.Diagnostics.Debug.WriteLine($"MALPLUS OnNewIntent: {intent?.Data}");
        HandleDeepLink(intent);
    }

    private void HandleDeepLink(Intent intent)
    {
        if (intent?.Data != null)
        {
            var uri = intent.Data.ToString();
            System.Diagnostics.Debug.WriteLine($"MALPLUS HandleDeepLink: {uri}");
            if (uri.StartsWith("malplus://"))
            {
                var path = uri.Replace("malplus://", "");
                System.Diagnostics.Debug.WriteLine($"MALPLUS Deep link path: {path}");
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"MALPLUS GoToAsync: {path}");
                        await Shell.Current.GoToAsync(path);
                        System.Diagnostics.Debug.WriteLine($"MALPLUS GoToAsync success");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Deep link failed: {ex}");
                    }
                });
            }
        }
    }
}

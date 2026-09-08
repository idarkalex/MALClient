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
    public static string PendingDeepLinkPath { get; private set; }

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Android.Util.Log.Info("MALPLUS", $"OnCreate intent data: {Intent?.Data}");
        if (Intent?.Data != null && Intent.Data.ToString().StartsWith("malplus://"))
        {
            DeepLinkIntent = Intent;
            PendingDeepLinkPath = Intent.Data.ToString().Replace("malplus://", "");
            Android.Util.Log.Info("MALPLUS", $"Deep link pending (cold start): {PendingDeepLinkPath}");
        }
    }

    protected override void OnNewIntent(Intent intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;
        Android.Util.Log.Info("MALPLUS", $"OnNewIntent data: {intent?.Data}");
        if (intent?.Data != null && intent.Data.ToString().StartsWith("malplus://"))
        {
            DeepLinkIntent = intent;
            HandleDeepLinkNavigation(intent.Data.ToString());
        }
    }

    public static async Task ApplyPendingDeepLinkAsync()
    {
        var path = PendingDeepLinkPath;
        if (string.IsNullOrWhiteSpace(path))
            return;
        PendingDeepLinkPath = null;
        Android.Util.Log.Info("MALPLUS", $"Applying pending deep link: {path}");
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                await Shell.Current.GoToAsync(path);
                Android.Util.Log.Info("MALPLUS", "Pending deep link success");
            }
            catch (Exception ex)
            {
                Android.Util.Log.Info("MALPLUS", $"Pending deep link failed: {ex.Message} | Stack: {ex.StackTrace}");
            }
        });
    }

    private void HandleDeepLinkNavigation(string uri)
    {
        if (uri.StartsWith("malplus://"))
        {
            var path = uri.Replace("malplus://", "");
            Android.Util.Log.Info("MALPLUS", $"Deep link path: {path}");
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    if (Shell.Current != null)
                    {
                        try
                        {
                            Android.Util.Log.Info("MALPLUS", $"GoToAsync: {path}");
                            await Shell.Current.GoToAsync(path);
                            Android.Util.Log.Info("MALPLUS", "GoToAsync success");
                            return;
                        }
                        catch (Exception ex)
                        {
                            Android.Util.Log.Info("MALPLUS", $"Deep link failed: {ex.Message} | Stack: {ex.StackTrace}");
                            return;
                        }
                    }
                    Android.Util.Log.Info("MALPLUS", $"Shell.Current null, retry {attempt + 1}/20");
                    await System.Threading.Tasks.Task.Delay(250);
                }
                Android.Util.Log.Info("MALPLUS", "Shell.Current never became ready (20 retries)");
            });
        }
    }
}

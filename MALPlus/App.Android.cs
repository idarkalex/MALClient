using Android.App;
using Android.Content;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace MALPlus;

public partial class App
{
    private void HandleDeepLinkFromIntent()
    {
        System.Diagnostics.Debug.WriteLine("=== MALPLUS HandleDeepLinkFromIntent CALLED ===");
        try
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            System.Diagnostics.Debug.WriteLine($"MALPLUS CurrentActivity: {activity != null}");
            if (activity != null)
            {
                System.Diagnostics.Debug.WriteLine($"MALPLUS Activity Intent: {activity.Intent != null}");
                System.Diagnostics.Debug.WriteLine($"MALPLUS Activity Intent Data: {activity.Intent?.Data}");
            }
            if (activity?.Intent?.Data != null)
            {
                var uri = activity.Intent.Data.ToString();
                System.Diagnostics.Debug.WriteLine("=== MALPLUS OnStart DeepLink ===");
                System.Diagnostics.Debug.WriteLine($"MALPLUS Intent Data: {uri}");
                if (uri.StartsWith("malplus://"))
                {
                    var path = uri.Replace("malplus://", "");
                    System.Diagnostics.Debug.WriteLine($"MALPLUS Deep link path: {path}");
                    HandleDeepLinkNavigation(path);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MALPLUS HandleDeepLinkFromIntent error: {ex.Message}");
        }
    }

    private async void HandleDeepLinkNavigation(string path)
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
    }
}
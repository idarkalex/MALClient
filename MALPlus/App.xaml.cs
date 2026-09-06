using MALClient.XShared.BL;
using MALClient.XShared.Utils;
using MALPlus.Services;

namespace MALPlus;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new AppShell();
    }

    protected override void OnStart()
    {
        base.OnStart();

        System.Diagnostics.Debug.WriteLine("=== MALPLUS App.OnStart ===");

        Task.Run(async () =>
        {
            try
            {
                await Task.WhenAny(InitializationRoutines.InitApp(), Task.Delay(TimeSpan.FromSeconds(30)));
            }
            catch
            {
            }
            finally
            {
                InitializationRoutines.AwaitableCompletion.TrySetResult(true);
            }
            try
            {
                if (!Credentials.Authenticated)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                        Shell.Current?.GoToAsync("login"));
                }
                else
                {
                    try
                    {
                        var client = await MALClient.XShared.ViewModels.ResourceLocator.MalHttpContextProvider.GetApiHttpContextAsync();
                        Console.WriteLine("MALPLUS boot api client ok=" + (client != null));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("MALPLUS boot api client FAILED " + ex.GetType().Name + " " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("MALPLUS login nav failed: " + ex.Message);
            }
        });
        MauiBootDiagnostics.Run();
    }

    protected override void OnAppLinkRequestReceived(Uri uri)
    {
        base.OnAppLinkRequestReceived(uri);
        System.Diagnostics.Debug.WriteLine("=== MALPLUS OnAppLinkRequestReceived ===");
        System.Diagnostics.Debug.WriteLine($"MALPLUS App Link: {uri}");
        if (uri.Scheme == "malplus")
        {
            var path = uri.AbsolutePath;
            if (!string.IsNullOrEmpty(uri.Query))
                path += uri.Query;
            System.Diagnostics.Debug.WriteLine($"MALPLUS App Link path: {path}");
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
                    System.Diagnostics.Debug.WriteLine($"App Link failed: {ex.Message} | Stack: {ex.StackTrace}");
                }
            });
        }
    }
}
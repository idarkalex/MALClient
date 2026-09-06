using MALClient.XShared.BL;
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
        });
        MauiBootDiagnostics.Run();
    }
}

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
        MauiBootDiagnostics.Run();
    }
}

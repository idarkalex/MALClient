using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALPlus.Views;

public partial class NotificationsHubPage : ContentPage
{
    private bool _initialized;
    private NotificationsHubViewModel Vm => (NotificationsHubViewModel)BindingContext;

    public NotificationsHubPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.NotificationsHub;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;
        try
        {
            Vm.Init(false);
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(200);
                if (!Vm.Loading) break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS NotificationsHubPage Init failed: " + ex.Message);
        }
    }

    private void OnRefreshing(object sender, EventArgs e)
    {
        try { Vm.Init(true); } catch { }
    }
}

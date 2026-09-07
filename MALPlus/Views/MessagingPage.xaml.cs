using MALClient.Models.Models.MalSpecific;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALPlus.Views;

public partial class MessagingPage : ContentPage
{
    private bool _initialized;
    private MalMessagingViewModel Vm => (MalMessagingViewModel)BindingContext;

    public MessagingPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.MalMessaging;
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
                if (!Vm.LoadingVisibility) break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS MessagingPage Init failed: " + ex.Message);
        }
    }

    private void OnRefreshing(object sender, EventArgs e)
    {
        try { Vm.Init(true); } catch { }
    }

    private async void OnMessageTapped(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is MalMessageModel msg)
            {
                ((CollectionView)sender).SelectedItem = null;
                await Shell.Current.GoToAsync($"messagedetails?id={msg.ThreadId}&subject={Uri.EscapeDataString(msg.Subject ?? "")}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS message nav failed: " + ex.Message);
        }
    }
}

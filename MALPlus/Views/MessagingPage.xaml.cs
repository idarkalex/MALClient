using MALClient.Models.Models.MalSpecific;
using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;
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
            if (Vm == null)
            {
                Console.WriteLine("MALPLUS MessagingPage: VM is null!");
                return;
            }
            Vm.Init(false);
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(200);
                if (Vm.LoadingVisibility == false) break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS MessagingPage Init failed: " + ex);
        }
    }

    private void OnRefreshing(object sender, EventArgs e)
    {
        try { Vm.Init(true); } catch { }
    }

    private async void OnComposeClicked(object sender, EventArgs e)
    {
        try
        {
            // Compose a new message. The user enters the recipient in the details page
            // (or we could prompt here — keeping simple: open a blank compose).
            await Shell.Current.GoToAsync("messagedetails?id=0&subject=");
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS compose failed: " + ex.Message);
        }
    }

    private async void OnMessageTapped(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is MalMessageModel msg)
            {
                ((CollectionView)sender).SelectedItem = null;
                await Shell.Current.GoToAsync(
                    $"messagedetails?id={Uri.EscapeDataString(msg.ThreadId ?? msg.Id)}&subject={Uri.EscapeDataString(msg.Subject ?? "")}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS message nav failed: " + ex.Message);
        }
    }
}

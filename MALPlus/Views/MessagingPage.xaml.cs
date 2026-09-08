using MALClient.Models.Models.MalSpecific;
using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALPlus.Views;

public partial class MessagingPage : ContentPage
{
    private bool _initialized;
    private int _messagingTabIndex = 0;
    private MalMessagingViewModel Vm => (MalMessagingViewModel)BindingContext;

    public int MessagingTabIndex
    {
        get => _messagingTabIndex;
        set { _messagingTabIndex = value; OnPropertyChanged(); }
    }

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
        BuildTabs();
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
            HighlightTab(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS MessagingPage Init failed: " + ex);
        }
    }

    private void BuildTabs()
    {
        TabStrip.Children.Clear();
        var tabs = new[] { ("Inbox", 0), ("Sent", 1) };
        for (int i = 0; i < tabs.Length; i++)
        {
            var (label, index) = tabs[i];
            var button = new Button
            {
                Text = label,
                FontFamily = "InterSemiBold",
                FontSize = 12,
                Padding = new Thickness(12, 6),
                HeightRequest = 32,
                CornerRadius = 16,
                BorderColor = Color.FromArgb("#29FFFFFF"),
                BorderWidth = 1,
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#FFFFFF")
            };
            int captured = index;
            button.Clicked += (s, e) => OnTabClicked(captured);
            TabStrip.Children.Add(button);
        }
    }

    private void HighlightTab(int activeIndex)
    {
        for (int i = 0; i < TabStrip.Children.Count; i++)
        {
            if (TabStrip.Children[i] is Button b)
            {
                b.BackgroundColor = i == activeIndex ? Color.FromArgb("#0066FF") : Colors.Transparent;
                b.TextColor = i == activeIndex ? Colors.White : Color.FromArgb("#FFFFFF");
            }
        }
    }

    private void OnTabClicked(int index)
    {
        _messagingTabIndex = index;
        OnPropertyChanged(nameof(MessagingTabIndex));
        HighlightTab(index);
        // Toggle DisplaySentMessages in VM
        Vm.DisplaySentMessages = index == 1;
    }

    private void OnRefreshing(object sender, EventArgs e)
    {
        try { Vm.Init(true); } catch { }
    }

    private async void OnComposeClicked(object sender, EventArgs e)
    {
        try
        {
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

using MALClient.Models.Models.Notifications;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALPlus.Views;

public partial class NotificationsHubPage : ContentPage
{
    private bool _initialized;
    private int _activeTab;
    private NotificationsHubViewModel Vm => (NotificationsHubViewModel)BindingContext;

    // MAL notification type tabs (mirrors v2 "All / Airing / Replies / Generic")
    private static readonly (string Label, MALClient.Models.Enums.MalNotificationsTypes Type)[] Tabs = new (string, MALClient.Models.Enums.MalNotificationsTypes)[]
    {
        ("All", MALClient.Models.Enums.MalNotificationsTypes.Generic),
        ("Generic", MALClient.Models.Enums.MalNotificationsTypes.Generic),
    };

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
        BuildTabs();
        try
        {
            Vm.Init(false);
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(200);
                if (!Vm.Loading) break;
            }
            HighlightTab(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS NotificationsHubPage Init failed: " + ex.Message);
        }
    }

    private void BuildTabs()
    {
        TypeTabsStrip.Children.Clear();
        for (int i = 0; i < Tabs.Length; i++)
        {
            var (label, type) = Tabs[i];
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
            int captured = i;
            var capturedType = type;
            button.Clicked += (s, e) => OnTypeTabClicked(captured, capturedType);
            TypeTabsStrip.Children.Add(button);
        }
    }

    private void HighlightTab(int activeIndex)
    {
        _activeTab = activeIndex;
        for (int i = 0; i < TypeTabsStrip.Children.Count; i++)
        {
            if (TypeTabsStrip.Children[i] is Button b)
            {
                b.BackgroundColor = i == activeIndex ? Color.FromArgb("#0066FF") : Colors.Transparent;
                b.TextColor = i == activeIndex ? Colors.White : Color.FromArgb("#FFFFFF");
            }
        }
    }

    private void OnTypeTabClicked(int index, MALClient.Models.Enums.MalNotificationsTypes type)
    {
        HighlightTab(index);
        try
        {
            // index 0 == All → null (show everything)
            Vm.CurrentNotificationType = index == 0 ? null : (MALClient.Models.Enums.MalNotificationsTypes?)type;
        }
        catch { }
    }

    private void OnRefreshing(object sender, EventArgs e)
    {
        try { Vm.Init(true); } catch { }
    }

    private async void OnNotificationTapped(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is MalNotification notif)
            {
                NotificationList.SelectedItem = null;
                // Mark as read first.
                try { Vm.MarkAsReadComand?.Execute(notif); } catch { }
                // Then navigate via the VM command (parses SanitizedLaunchArgs to
                // anime/manga/forum/messages routes).
                try { Vm.NavigateNotificationCommand?.Execute(notif); } catch { }
                await Task.Delay(200);
                // The VM command uses GeneralMain.Navigate synchronously; nothing to await,
                // but if the VM navigation requires the page to load before the next tap,
                // we wait a moment.
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS notification tap failed: " + ex.Message);
        }
    }
}

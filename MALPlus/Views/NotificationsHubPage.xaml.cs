using MALClient.Models.Enums;
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

    // MAL notification type tabs: All, Airing, Replies, Messages, Others
    private static readonly (string Label, MalNotificationsTypes? Type)[] Tabs = new (string, MalNotificationsTypes?)[]
    {
        ("All", null),
        ("Airing", MalNotificationsTypes.NowAiring),
        ("Replies", MalNotificationsTypes.ForumQuoute | MalNotificationsTypes.UserMentions | MalNotificationsTypes.WatchedTopics | MalNotificationsTypes.WatchedTopic),
        ("Messages", MalNotificationsTypes.Messages),
        ("Others", MalNotificationsTypes.Generic | MalNotificationsTypes.FriendRequest | MalNotificationsTypes.FriendRequestAcceptDeny | MalNotificationsTypes.ProfileComment | MalNotificationsTypes.BlogComment | MalNotificationsTypes.ClubMessages | MalNotificationsTypes.NewRelatedAnime | MalNotificationsTypes.Payment),
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

    private void OnTypeTabClicked(int index, MalNotificationsTypes? type)
    {
        HighlightTab(index);
        try
        {
            Vm.CurrentNotificationType = type;
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
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS notification tap failed: " + ex.Message);
        }
    }
}

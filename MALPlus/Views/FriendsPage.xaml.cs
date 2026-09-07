using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;
using MALClient.XShared.Utils;

namespace MALPlus.Views;

public partial class FriendsPage : ContentPage
{
    private int _tabIndex;
    private bool _initialized;

    public int FriendsTabIndex
    {
        get => _tabIndex;
        set { _tabIndex = value; OnPropertyChanged(); }
    }

    private FriendsPageViewModel Vm => (FriendsPageViewModel)BindingContext;

    public FriendsPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.Friends;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;
        try
        {
            Vm.NavigatedTo(new FriendsPageNavArgs { TargetUser = new MALClient.Models.Models.MalUser { Name = Credentials.UserName } });
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(200);
                if (!Vm.Loading) break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS FriendsPage Init failed: " + ex.Message);
        }
    }

    private void OnTabTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is string s && int.TryParse(s, out int idx))
        {
            FriendsTabIndex = idx;
            // recolor the two tab labels manually
            FriendsTab.TextColor = idx == 0 ? Color.FromArgb("#FF6B00") : Colors.White;
            RequestsTab.TextColor = idx == 1 ? Color.FromArgb("#FF6B00") : Colors.White;
        }
    }

    private void OnRefreshFriends(object sender, EventArgs e)
    {
        try { Vm.RefreshFriendsCommand?.Execute(null); } catch { }
    }

    private void OnRefreshRequests(object sender, EventArgs e)
    {
        try { Vm.RefreshPendingCommand?.Execute(null); } catch { }
    }

    private async void OnFriendTapped(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is MALClient.Models.Models.MalSpecific.MalFriend f)
            {
                ((CollectionView)sender).SelectedItem = null;
                await Shell.Current.GoToAsync($"profile?user={Uri.EscapeDataString(f.User.Name)}");
            }
        }
        catch { }
    }

    private void OnAcceptClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Button b && b.CommandParameter is object req)
            {
                Vm.AcceptFriendRequestCommand?.Execute(req);
            }
        }
        catch { }
    }

    private void OnDenyClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Button b && b.CommandParameter is object req)
            {
                Vm.DenyFriendRequestCommand?.Execute(req);
            }
        }
        catch { }
    }
}

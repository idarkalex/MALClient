using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;
using MALClient.XShared.ViewModels.Items;

namespace MALPlus.Views;

[QueryProperty(nameof(TargetUser), "user")]
public partial class ProfilePage : ContentPage
{
    private bool _initialized;

    public string TargetUser { get; set; }

    private ProfilePageViewModel Vm => (ProfilePageViewModel)BindingContext;

    public ProfilePage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.ProfilePage;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        System.Diagnostics.Debug.WriteLine("MALPLUS ProfilePage OnAppearing");
        if (_initialized)
            return;
        _initialized = true;

        var args = new ProfilePageNavigationArgs
        {
            TargetUser = TargetUser ?? Credentials.UserName
        };
        System.Diagnostics.Debug.WriteLine("MALPLUS ProfilePage calling LoadProfileData");
        try
        {
            await Vm.LoadProfileData(args);
            System.Diagnostics.Debug.WriteLine("MALPLUS ProfilePage LoadProfileData completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("MALPLUS Profile Init failed: " + ex);
        }
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        try
        {
            if (Vm == null) return;
            await Vm.LoadProfileData(new ProfilePageNavigationArgs
            {
                TargetUser = TargetUser ?? Credentials.UserName
            }, force: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("MALPLUS Profile refresh failed: " + ex.Message);
        }
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        try
        {
            if (Vm == null) return;
            await Vm.LoadProfileData(new ProfilePageNavigationArgs
            {
                TargetUser = TargetUser ?? Credentials.UserName
            }, force: true);
        }
        catch (Exception ex) { Console.WriteLine("MALPLUS refresh btn failed: " + ex.Message); }
    }

    private async void OnCompareClicked(object sender, EventArgs e)
    {
        try
        {
            var user = TargetUser ?? Credentials.UserName;
            if (string.IsNullOrEmpty(user)) return;
            await Shell.Current.GoToAsync($"listcomparison?user={Uri.EscapeDataString(user)}");
        }
        catch (Exception ex) { Console.WriteLine("MALPLUS compare failed: " + ex.Message); }
    }

    private async void OnHistoryClicked(object sender, EventArgs e)
    {
        try
        {
            var user = TargetUser ?? Credentials.UserName;
            if (string.IsNullOrEmpty(user)) return;
            await Shell.Current.GoToAsync($"history?user={Uri.EscapeDataString(user)}");
        }
        catch (Exception ex) { Console.WriteLine("MALPLUS history failed: " + ex.Message); }
    }

    private async void OnFavAnimeTapped(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is AnimeItemViewModel item)
        {
            ((CollectionView)sender).SelectedItem = null;
            await Shell.Current.GoToAsync(
                $"animedetails?id={item.Id}&title={Uri.EscapeDataString(item.Title ?? "")}");
        }
    }

    private async void OnFavMangaTapped(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is AnimeItemViewModel item)
        {
            ((CollectionView)sender).SelectedItem = null;
            await Shell.Current.GoToAsync(
                $"animedetails?id={item.Id}&title={Uri.EscapeDataString(item.Title ?? "")}&manga=true");
        }
    }

    private async void OnFavCharacterTapped(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is FavouriteViewModel item &&
            int.TryParse(item.Data.Id, out int id))
        {
            ((CollectionView)sender).SelectedItem = null;
            await Shell.Current.GoToAsync($"character?id={id}");
        }
    }

    private async void OnFavStaffTapped(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is FavouriteViewModel item &&
            int.TryParse(item.Data.Id, out int id))
        {
            ((CollectionView)sender).SelectedItem = null;
            await Shell.Current.GoToAsync($"staff?id={id}");
        }
    }

    private async void OnRecentAnimeTapped(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is AnimeItemViewModel item)
        {
            ((CollectionView)sender).SelectedItem = null;
            await Shell.Current.GoToAsync(
                $"animedetails?id={item.Id}&title={Uri.EscapeDataString(item.Title ?? "")}");
        }
    }

    private async void OnRecentMangaTapped(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is AnimeItemViewModel item)
        {
            ((CollectionView)sender).SelectedItem = null;
            await Shell.Current.GoToAsync(
                $"animedetails?id={item.Id}&title={Uri.EscapeDataString(item.Title ?? "")}&manga=true");
        }
    }

    private async void OnFriendTapped(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is MALClient.Models.Models.MalUser user)
        {
            ((CollectionView)sender).SelectedItem = null;
            await Shell.Current.GoToAsync($"profile?user={Uri.EscapeDataString(user.Name)}");
        }
    }
}

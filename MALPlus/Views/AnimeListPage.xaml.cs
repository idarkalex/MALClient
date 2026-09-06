using MALClient.Models.Enums;
using MALClient.XShared.BL;
using MALClient.XShared.Comm.Anime;
using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALPlus.Views;

public partial class AnimeListPage : ContentPage
{
    private bool _authedAtLoad;
    private string _userAtLoad;
    private bool _refreshArmed;

    private AnimeListViewModel Vm => (AnimeListViewModel)BindingContext;

    public AnimeListPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.AnimeList;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var authed = Credentials.Authenticated;
        var user = Credentials.UserName;
        if (!InitializationRoutines.AwaitableCompletion.Task.IsCompleted)
        {
            await Task.WhenAny(InitializationRoutines.AwaitableCompletion.Task, Task.Delay(TimeSpan.FromSeconds(30)));
            authed = Credentials.Authenticated;
            user = Credentials.UserName;
        }
        if (_authedAtLoad == authed && _userAtLoad == user && (Vm.AnimeItems?.Count ?? 0) > 0)
            return;
        _authedAtLoad = authed;
        _userAtLoad = user;
        try
        {
            if (authed && !string.IsNullOrWhiteSpace(user))
            {
                var args = new AnimeListPageNavigationArgs(0, AnimeListWorkModes.Anime) { ListSource = user };
                await Vm.Init(args);
            }
            else
            {
                await Vm.Init(AnimeListPageNavigationArgs.TopAnime(TopAnimeType.General));
            }
            _refreshArmed = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS AnimeListPage Init failed: " + ex);
        }
    }

    private void OnRefreshing(object sender, EventArgs e)
    {
        try
        {
            if (!_refreshArmed || Vm == null || Vm.Initializing)
            {
                ListRefresh.IsRefreshing = false;
                return;
            }
            Vm.RefreshCommand?.Execute(null);
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS refresh failed: " + ex.Message);
            ListRefresh.IsRefreshing = false;
        }
    }

    private async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is AnimeItemViewModel item)
            {
                AnimeGrid.SelectedItem = null;
                await Shell.Current.GoToAsync($"animedetails?id={item.Id}&title={Uri.EscapeDataString(item.Title ?? string.Empty)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS selection nav failed: " + ex.Message);
        }
    }
}

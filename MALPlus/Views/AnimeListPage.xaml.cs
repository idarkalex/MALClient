using MALClient.Models.Enums;
using MALClient.XShared.BL;
using MALClient.XShared.Comm.Anime;
using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALPlus.Views;

[QueryProperty(nameof(Mode), "mode")]
[QueryProperty(nameof(Status), "status")]
public partial class AnimeListPage : ContentPage
{
    private bool _authedAtLoad;
    private string _userAtLoad;
    private bool _refreshArmed;
    private bool _modeApplied;
    private int _lastMode = -1;

    public string Mode { get; set; }
    public string Status { get; set; }

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

        // Resolve work mode: query string first, then fall back to Shell tab title.
        int mode = 0;
        if (!int.TryParse(Mode, out mode) || mode == 0)
        {
            // No query string → infer from current Shell tab
            try
            {
                var title = Shell.Current?.CurrentItem?.CurrentItem?.Title;
                if (title == "Manga") mode = 2; // AnimeListWorkModes.Manga
            }
            catch { }
        }

        // Same source check but include work mode so re-navigation re-inits
        if (_authedAtLoad == authed && _userAtLoad == user && _modeApplied && mode == _lastMode
            && (Vm.AnimeItems?.Count ?? 0) > 0)
            return;
        _authedAtLoad = authed;
        _userAtLoad = user;
        _modeApplied = true;
        _lastMode = mode;

        try
        {
            int statusIdx = 0;
            int.TryParse(Status, out statusIdx);
            AnimeListPageNavigationArgs args;
            switch (mode)
            {
                case 1: // SeasonalAnime
                    args = AnimeListPageNavigationArgs.Seasonal;
                    break;
                case 2: // Manga
                    args = statusIdx > 0
                        ? new AnimeListPageNavigationArgs(statusIdx, AnimeListWorkModes.Manga)
                        : new AnimeListPageNavigationArgs(0, AnimeListWorkModes.Manga);
                    break;
                case 3: // TopAnime
                    args = AnimeListPageNavigationArgs.TopAnime(TopAnimeType.General);
                    break;
                case 4: // TopManga
                    args = AnimeListPageNavigationArgs.TopMangaCategory(MangaTopType.All);
                    break;
                case 8: // MangaAdapted
                    args = AnimeListPageNavigationArgs.MangaAdapted(MangaAdaptedType.All);
                    break;
                case 0: // Anime (user list)
                default:
                    if (authed && !string.IsNullOrWhiteSpace(user))
                    {
                        args = statusIdx > 0
                            ? new AnimeListPageNavigationArgs(statusIdx, AnimeListWorkModes.Anime) { ListSource = user }
                            : new AnimeListPageNavigationArgs(0, AnimeListWorkModes.Anime) { ListSource = user };
                    }
                    else
                    {
                        args = AnimeListPageNavigationArgs.TopAnime(TopAnimeType.General);
                    }
                    break;
            }
            await Vm.Init(args);
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

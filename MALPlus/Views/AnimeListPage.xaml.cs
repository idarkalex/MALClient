using MALClient.Models.Enums;
using MALClient.XShared.BL;
using MALClient.XShared.Comm.Anime;
using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;
using MALPlus.Services;

namespace MALPlus.Views;

[QueryProperty(nameof(Mode), "mode")]
[QueryProperty(nameof(Status), "status")]
[QueryProperty(nameof(Type), "type")]
public partial class AnimeListPage : ContentPage
{
    private bool _authedAtLoad;
    private string _userAtLoad;
    private bool _refreshArmed;
    private bool _modeApplied;
    private int _lastMode = -1;
    private int _lastStatus = -1;
    private TopAnimeType _topType = TopAnimeType.General;
    private MangaTopType _mangaTopType = MangaTopType.All;
    private MangaAdaptedType _adaptedType = MangaAdaptedType.AiringNow;

    public string Mode { get; set; }
    public string Status { get; set; }
    public string Type { get; set; }

    private AnimeListViewModel Vm => (AnimeListViewModel)BindingContext;

    // Tabs in display order; the value is the StatusSelectorSelectedIndex the shared VM expects:
    // 0=Watching, 1=Completed, 2=OnHold, 3=Dropped, 4=PlanToWatch, 5=All.
    private static readonly (string Label, int StatusSelectorIndex)[] AnimeStatusTabs = new[]
    {
        ("All", 5),
        ("Watching", 0),
        ("Completed", 1),
        ("On Hold", 2),
        ("Dropped", 3),
        ("Planned", 4),
    };

    private static readonly (string Label, int StatusSelectorIndex)[] MangaStatusTabs = new[]
    {
        ("All", 5),
        ("Reading", 0),
        ("Completed", 1),
        ("On Hold", 2),
        ("Dropped", 3),
        ("Planned", 4),
    };

    public AnimeListPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.AnimeList;
        // MAUI's CollectionView virtualizes rendering, so we load the ENTIRE user list
        // into AnimeItems (negative dimensions => GetGridItemsToLoad returns int.MaxValue).
        // Otherwise UpdatePageSetup only ever exposes ~27 items and never drains the rest.
        Vm.DimensionsProvider = new MauiDimensionsProvider();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var authed = Credentials.Authenticated;
        var user = Credentials.UserName;
        if (!InitializationRoutines.AwaitableCompletion.Task.IsCompleted)
        {
            // Initialise synchronously; the user is still on the loading screen.
        }
        else if (!authed)
        {
            // not yet authenticated (e.g. login still pending)
        }

        // Resolve work mode: query string first, then fall back to Shell tab title.
        int mode = 0;
        int.TryParse(Mode, out mode);
        if (mode == 0)
        {
            try
            {
                var title = Shell.Current?.CurrentItem?.CurrentItem?.Title;
                if (title == "Manga") mode = (int)AnimeListWorkModes.Manga;
            }
            catch { }
        }

        // Resolve the specific top-type / adapted-type from the query param (e.g. TopAnimeType.Airing).
        ParseTypeQuery(mode, Type);

        // Re-initialise when mode changes (e.g. switching Anime/Manga tabs)
        if (_modeApplied && _lastMode == mode && (Vm.AnimeItems?.Count ?? 0) > 0)
            return;
        _modeApplied = true;
        _lastMode = mode;

        BuildStatusStrip(mode);

        try
        {
            // Default status tab: All (StatusSelector index 5). 'status' query carries the
            // StatusSelectorSelectedIndex when routing in.
            int statusIdx = 5;
            int.TryParse(Status, out statusIdx);
            if (statusIdx < 0 || statusIdx > 5)
                statusIdx = 5;
            _lastStatus = statusIdx;
            HighlightStatus(_lastStatus, mode);
            AnimeListPageNavigationArgs args = BuildArgs(mode, _lastStatus);
            Vm.Init(args);
            _refreshArmed = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS AnimeListPage Init failed: " + ex);
        }
    }

    private void ParseTypeQuery(int mode, string type)
    {
        _topType = TopAnimeType.General;
        _mangaTopType = MangaTopType.All;
        _adaptedType = MangaAdaptedType.AiringNow;
        if (string.IsNullOrWhiteSpace(type))
            return;
        try
        {
            switch ((AnimeListWorkModes)mode)
            {
                case AnimeListWorkModes.TopAnime:
                    _topType = (TopAnimeType)System.Enum.Parse(typeof(TopAnimeType), type);
                    break;
                case AnimeListWorkModes.TopManga:
                    _mangaTopType = (MangaTopType)System.Enum.Parse(typeof(MangaTopType), type);
                    break;
                case AnimeListWorkModes.MangaAdapted:
                    _adaptedType = (MangaAdaptedType)System.Enum.Parse(typeof(MangaAdaptedType), type);
                    break;
            }
        }
        catch
        {
            // fall back to defaults
        }
    }

    private void BuildStatusStrip(int mode)
    {
        StatusTabsStrip.Children.Clear();
        var tabs = mode == (int)AnimeListWorkModes.Manga ? MangaStatusTabs : AnimeStatusTabs;
        for (int i = 0; i < tabs.Length; i++)
        {
            var (label, statusIdx) = tabs[i];
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
            int capturedStatus = statusIdx;
            button.Clicked += (s, e) => OnStatusTabClicked(capturedStatus);
            StatusTabsStrip.Children.Add(button);
        }
    }

    private void HighlightStatus(int statusSelectorIndex, int mode)
    {
        var activeColor = Color.FromArgb("#0066FF");
        var inactiveColor = Colors.Transparent;
        var activeText = Colors.White;
        var inactiveText = Color.FromArgb("#FFFFFF");
        var tabs = mode == (int)AnimeListWorkModes.Manga ? MangaStatusTabs : AnimeStatusTabs;
        for (int i = 0; i < StatusTabsStrip.Children.Count && i < tabs.Length; i++)
        {
            if (StatusTabsStrip.Children[i] is Button b)
            {
                var isActive = tabs[i].StatusSelectorIndex == statusSelectorIndex;
                b.BackgroundColor = isActive ? activeColor : inactiveColor;
                b.TextColor = isActive ? activeText : inactiveText;
            }
        }
    }

    private void OnStatusTabClicked(int statusSelectorIndex)
    {
        try
        {
            _lastStatus = statusSelectorIndex;
            HighlightStatus(statusSelectorIndex, _lastMode);
            var args = BuildArgs(_lastMode, statusSelectorIndex);
            Vm.Init(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS status tab failed: " + ex.Message);
        }
    }

    private AnimeListPageNavigationArgs BuildArgs(int mode, int statusSelectorIndex)
    {
        var workMode = (AnimeListWorkModes)mode;
        switch (workMode)
        {
            case AnimeListWorkModes.Manga:
                return new AnimeListPageNavigationArgs(statusSelectorIndex, AnimeListWorkModes.Manga);
            case AnimeListWorkModes.Anime:
                if (Credentials.Authenticated && !string.IsNullOrWhiteSpace(Credentials.UserName))
                    return new AnimeListPageNavigationArgs(statusSelectorIndex, AnimeListWorkModes.Anime)
                    {
                        ListSource = Credentials.UserName
                    };
                // Unauthenticated: fall back to the Top anime list.
                return AnimeListPageNavigationArgs.TopAnime(_topType);
            case AnimeListWorkModes.SeasonalAnime:
                return AnimeListPageNavigationArgs.Seasonal;
            case AnimeListWorkModes.TopAnime:
                return AnimeListPageNavigationArgs.TopAnime(_topType);
            case AnimeListWorkModes.TopManga:
                return AnimeListPageNavigationArgs.TopMangaCategory(_mangaTopType);
            case AnimeListWorkModes.MangaAdapted:
                return AnimeListPageNavigationArgs.MangaAdapted(_adaptedType);
            default:
                return AnimeListPageNavigationArgs.TopAnime(_topType);
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

    private bool _navigatingToDetails;

    private async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is AnimeItemViewModel item)
            {
                if (_navigatingToDetails) return;
                _navigatingToDetails = true;
                AnimeGrid.SelectedItem = null;
                var wm = (AnimeListWorkModes)_lastMode;
                var manga = wm == AnimeListWorkModes.Manga || wm == AnimeListWorkModes.TopManga || wm == AnimeListWorkModes.MangaAdapted;
                var titleQs = Uri.EscapeDataString(item.Title ?? string.Empty);
                var source = wm == AnimeListWorkModes.Manga
                    ? PageIndex.PageMangaList
                    : PageIndex.PageAnimeList;
                var detailsArgs = new AnimeDetailsPageNavigationArgs(item.Id, item.Title, null, item,
                    BuildArgs(_lastMode, _lastStatus))
                {
                    Source = source,
                    AnimeMode = !manga
                };
                MauiDetailsNavigationHandoff.Set(detailsArgs);
                var route = manga
                    ? $"animedetails?id={item.Id}&title={titleQs}&manga=true"
                    : $"animedetails?id={item.Id}&title={titleQs}";
                await Shell.Current.GoToAsync(route);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS selection nav failed: " + ex.Message);
        }
        finally
        {
            _navigatingToDetails = false;
        }
    }

    private sealed class MauiDimensionsProvider : MALClient.XShared.ViewModels.Interfaces.IDimensionsProvider
    {
        public double ActualWidth => DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
        public double ActualHeight => DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density;
    }
}

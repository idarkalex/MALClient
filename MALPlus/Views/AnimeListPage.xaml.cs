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
[QueryProperty(nameof(Menu), "menu")]
[QueryProperty(nameof(Reset), "reset")]
public partial class AnimeListPage : ContentPage
{
    private bool _authedAtLoad;
    private string _userAtLoad;
    private bool _refreshArmed;
    private bool _applied;
    private int _appliedMode = -1;
    private int _appliedStatus = -1;
    private string _appliedType;
    private int _lastMode = -1;
    private int _lastStatus = -1;
    private bool _dotsConnected;
    private TopAnimeType _topType = TopAnimeType.General;
    private MangaTopType _mangaTopType = MangaTopType.All;
    private MangaAdaptedType _adaptedType = MangaAdaptedType.AiringNow;

    private const int DefaultStatusIndex = 0;
    private const int MaxStatusIndex = 5;

    public string Mode { get; set; }
    public string Status { get; set; }
    public string Type { get; set; }
    public string Menu { get; set; }
    public string Reset { get; set; }

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
        if (!_dotsConnected)
        {
            BottomNav.DotsClicked += OnBottomNavDotsClicked;
            BottomNav.SectionActivated += OnSectionActivated;
            _dotsConnected = true;
        }
        ApplyRoute(null);
    }

    private void OnSectionActivated(string route)
    {
        ApplyRoute(route);
    }

    private void ApplyRoute(string route)
    {
        try
        {
            var query = ParseQuery(route);
            var mode = ResolveMode(query, out var modeFromQuery);
            var status = ResolveStatus(query, modeFromQuery);
            var type = ResolveType(query, modeFromQuery);
            ParseTypeQuery(mode, type);

            var openStatusMenu = string.Equals(QueryValue(query, "menu", Menu), "status", StringComparison.OrdinalIgnoreCase);
            var forceReset = string.Equals(QueryValue(query, "reset", Reset), "1", StringComparison.Ordinal);
            Menu = null;
            Reset = null;

            var alreadyShowing =
                !forceReset &&
                _applied &&
                _appliedMode == mode &&
                _appliedStatus == status &&
                string.Equals(_appliedType, type, StringComparison.Ordinal) &&
                (Vm.AnimeItems?.Count ?? 0) > 0;
            Android.Util.Log.Info("MALPLUS",
                $"AnimeList apply route='{route ?? "(properties)"}' -> mode={mode} status={status} type={type} reset={forceReset} skip={alreadyShowing} applied=({_appliedMode},{_appliedStatus},{_appliedType})");
            if (alreadyShowing)
            {
                if (openStatusMenu)
                    OpenStatusMenu(mode);
                return;
            }

            _applied = true;
            _appliedMode = mode;
            _appliedStatus = status;
            _appliedType = type;
            _lastMode = mode;
            _lastStatus = status;

            _ = InitAndObserveAsync(mode, status, openStatusMenu);
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS AnimeListPage ApplyRoute failed: " + ex);
        }
    }

    private static Dictionary<string, string> ParseQuery(string route)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(route))
            return result;
        var parts = route.Split(new[] { '?' }, 2);
        if (parts.Length < 2)
            return result;
        foreach (var pair in parts[1].Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split(new[] { '=' }, 2);
            var key = Uri.UnescapeDataString(kv[0]);
            if (result.ContainsKey(key))
                continue;
            result[key] = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
        }
        return result;
    }

    private static string QueryValue(Dictionary<string, string> query, string key, string fallback)
        => query.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private async Task InitAndObserveAsync(int mode, int status, bool openStatusMenu)
    {
        try
        {
            var args = BuildArgs(mode, status);
            await Vm.Init(args);
            _refreshArmed = true;
        }
        catch (Exception ex)
        {
            _applied = false;
            Console.WriteLine("MALPLUS AnimeListPage Init failed: " + ex);
        }
        finally
        {
            if (openStatusMenu)
                MainThread.BeginInvokeOnMainThread(() => OpenStatusMenu(mode));
        }
    }

    private int ResolveMode(Dictionary<string, string> query, out bool fromQuery)
    {
        fromQuery = false;
        if (query.TryGetValue("mode", out var raw) &&
            int.TryParse(raw, out var parsed) &&
            System.Enum.IsDefined(typeof(AnimeListWorkModes), parsed))
        {
            fromQuery = true;
            return parsed;
        }
        if (!string.IsNullOrWhiteSpace(Mode) &&
            int.TryParse(Mode, out var fromProperty) &&
            System.Enum.IsDefined(typeof(AnimeListWorkModes), fromProperty))
        {
            fromQuery = true;
            return fromProperty;
        }
        return IsMangaLocation() ? (int) AnimeListWorkModes.Manga : (int) AnimeListWorkModes.Anime;
    }

    private static bool IsMangaLocation()
    {
        var location = Shell.Current?.CurrentState?.Location?.OriginalString ?? string.Empty;
        var path = location.Split(new[] { '?' }, 2)[0];
        foreach (var segment in path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(segment, "manga", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private int ResolveStatus(Dictionary<string, string> query, bool fromQuery)
    {
        if (fromQuery && query.TryGetValue("status", out var raw) &&
            int.TryParse(raw, out var parsed) && parsed >= 0 && parsed <= MaxStatusIndex)
            return parsed;
        return ResolveStatus();
    }

    private int ResolveStatus()
    {
        if (string.IsNullOrWhiteSpace(Status))
            return DefaultStatusIndex;
        if (int.TryParse(Status, out var parsed) && parsed >= 0 && parsed <= MaxStatusIndex)
            return parsed;
        return DefaultStatusIndex;
    }

    private string ResolveType(Dictionary<string, string> query, bool fromQuery)
    {
        if (fromQuery && query.TryGetValue("type", out var raw) && !string.IsNullOrWhiteSpace(raw))
            return raw.Trim();
        return ResolveType();
    }

    private string ResolveType()
        => string.IsNullOrWhiteSpace(Type) ? null : Type.Trim();

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

    private void OpenStatusMenu(int mode)
    {
        var tabs = mode == (int)AnimeListWorkModes.Manga ? MangaStatusTabs : AnimeStatusTabs;
        BottomNav.OpenStatusMenu(
            mode == (int)AnimeListWorkModes.Manga,
            tabs.Select(tab => (tab.Label, tab.StatusSelectorIndex)),
            OnStatusTabClicked);
    }

    private void OnBottomNavDotsClicked(string section)
    {
        OpenStatusMenu(section == "manga" ? (int)AnimeListWorkModes.Manga : (int)AnimeListWorkModes.Anime);
    }

    private async void OnStatusTabClicked(int statusSelectorIndex)
    {
        try
        {
            _lastStatus = statusSelectorIndex;
            var args = BuildArgs(_lastMode, statusSelectorIndex);
            await Vm.Init(args);
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
                // Never silently switch work mode when there is no session: let the
                // ViewModel surface its "log in or set it manually" notice instead.
                return new AnimeListPageNavigationArgs(statusSelectorIndex, AnimeListWorkModes.Anime)
                {
                    ListSource = Credentials.UserName
                };
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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_dotsConnected)
        {
            BottomNav.DotsClicked -= OnBottomNavDotsClicked;
            BottomNav.SectionActivated -= OnSectionActivated;
            _dotsConnected = false;
        }
        // MAUI re-applies query attributes on re-navigation but never clears a
        // [QueryProperty] that the new route omits, which would leak the previous
        // work mode into this page. Drop them so the next apply resolves fresh.
        Mode = null;
        Status = null;
        Type = null;
        Menu = null;
        Reset = null;
        _applied = false;
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

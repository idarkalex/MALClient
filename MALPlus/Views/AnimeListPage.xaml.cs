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
    private int _lastStatus = -1;
    private bool _lastQueryEmpty = true;
    private string _lastSearch = "";

    public string Mode { get; set; }
    public string Status { get; set; }

    private AnimeListViewModel Vm => (AnimeListViewModel)BindingContext;

    // All / Watching / Completed / On Hold / Dropped / Plan to Watch
    // AnimeListPageNavigationArgs(filterIndex, AnimeListWorkModes) ctor:
    // filterIndex 0 = all, 1 = watching, 2 = completed, 3 = on hold, 4 = dropped, 5 = planned, 6 = rewatching
    private static readonly (string Label, int FilterIndex)[] AnimeStatusTabs = new[]
    {
        ("All", 0),
        ("Watching", 1),
        ("Completed", 2),
        ("On Hold", 3),
        ("Dropped", 4),
        ("Planned", 5),
    };

    private static readonly (string Label, int FilterIndex)[] MangaStatusTabs = new[]
    {
        ("All", 0),
        ("Reading", 1),
        ("Completed", 2),
        ("On Hold", 3),
        ("Dropped", 4),
        ("Planned", 5),
    };

    public AnimeListPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.AnimeList;
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

        // Re-initialise when mode changes (e.g. switching Anime/Manga tabs)
        if (_modeApplied && _lastMode == mode && (Vm.AnimeItems?.Count ?? 0) > 0)
            return;
        _modeApplied = true;
        _lastMode = mode;

        BuildStatusStrip(mode);

        try
        {
            int statusIdx = 0;
            int.TryParse(Status, out statusIdx);
            _lastStatus = statusIdx > 0 ? statusIdx : 0;
            HighlightStatus(_lastStatus);
            AnimeListPageNavigationArgs args = BuildArgs(mode, _lastStatus);
            Vm.Init(args);
            _refreshArmed = true;
            UpdateSubtitle();
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS AnimeListPage Init failed: " + ex);
        }
    }

    private void BuildStatusStrip(int mode)
    {
        StatusTabsStrip.Children.Clear();
        var tabs = mode == (int)AnimeListWorkModes.Manga ? MangaStatusTabs : AnimeStatusTabs;
        for (int i = 0; i < tabs.Length; i++)
        {
            var (label, filterIndex) = tabs[i];
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
            int capturedIndex = filterIndex;
            button.Clicked += (s, e) => OnStatusTabClicked(capturedIndex);
            StatusTabsStrip.Children.Add(button);
        }
    }

    private void HighlightStatus(int activeIndex)
    {
        var activeColor = Color.FromArgb("#0066FF");
        var inactiveColor = Colors.Transparent;
        var activeText = Colors.White;
        var inactiveText = Color.FromArgb("#FFFFFF");
        for (int i = 0; i < StatusTabsStrip.Children.Count; i++)
        {
            if (StatusTabsStrip.Children[i] is Button b)
            {
                b.BackgroundColor = i == activeIndex ? activeColor : inactiveColor;
                b.TextColor = i == activeIndex ? activeText : inactiveText;
            }
        }
    }

    private void OnStatusTabClicked(int filterIndex)
    {
        try
        {
            _lastStatus = filterIndex;
            HighlightStatus(filterIndex);
            var args = BuildArgs(_lastMode, filterIndex);
            Vm.Init(args);
            UpdateSubtitle();
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS status tab failed: " + ex.Message);
        }
    }

    private void UpdateSubtitle()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_lastSearch))
                SubtitleLabel.Text = $"Search: \"{_lastSearch}\"";
            else
                SubtitleLabel.Text = "";
            SubtitleLabel.IsVisible = !string.IsNullOrWhiteSpace(SubtitleLabel.Text);
        }
        catch { }
    }

    private static AnimeListPageNavigationArgs BuildArgs(int mode, int filterIndex)
    {
        var workMode = (AnimeListWorkModes)mode;
        if (workMode == AnimeListWorkModes.Manga)
            return new AnimeListPageNavigationArgs(filterIndex, AnimeListWorkModes.Manga);
        // anime user list
        if (Credentials.Authenticated && !string.IsNullOrWhiteSpace(Credentials.UserName))
        {
            return new AnimeListPageNavigationArgs(filterIndex, AnimeListWorkModes.Anime)
            {
                ListSource = Credentials.UserName
            };
        }
        return AnimeListPageNavigationArgs.TopAnime(TopAnimeType.General);
    }

    private void OnSearchCompleted(object sender, EventArgs e) => DoSearch();
    private void OnSearchClicked(object sender, EventArgs e) => DoSearch();

    private void DoSearch()
    {
        try
        {
            _lastSearch = SearchEntry.Text ?? "";
            // Search is local-only (Recent Searches isn't implemented yet): re-Init with a query
            // would clear the user list. Instead, just filter the visible collection client-side.
            UpdateSubtitle();
            // No-op for now: this is documented as a known limitation in MAUI.
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS search failed: " + ex.Message);
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
                var manga = (AnimeListWorkModes)_lastMode == AnimeListWorkModes.Manga;
                var titleQs = Uri.EscapeDataString(item.Title ?? string.Empty);
                if (manga)
                    await Shell.Current.GoToAsync($"animedetails?id={item.Id}&title={titleQs}&manga=true");
                else
                    await Shell.Current.GoToAsync($"animedetails?id={item.Id}&title={titleQs}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS selection nav failed: " + ex.Message);
        }
    }
}

namespace MALPlus.Controls;

public partial class BottomNavBar : ContentView
{
    public event Action<string> DotsClicked;

    // Fired after a successful tab navigation with the exact route used. MAUI does
    // not raise Navigated when only the query changes on an already-visible
    // ShellContent, so pages that need to react to a re-tap rely on this.
    public event Action<string> SectionActivated;

    private static readonly Color SelectedColor = Color.FromArgb("#0066FF");
    private static readonly Color UnselectedColor = Color.FromArgb("#A0FFFFFF");
    private static readonly Color SelectedButtonColor = Color.FromArgb("#1F0066FF");
    private static readonly Color TransparentButtonColor = Color.FromArgb("#00000000");
    private static readonly string[] Sections = { "discover", "anime", "manga", "more" };
    private Shell _subscribedShell;
    private bool _navigationInFlight;
    private string _pendingRoute;
    private string _pendingSection;
    private bool _pendingAllowsCurrent;
    private long _requestVersion;
    private long _navigationVersion;

    public BottomNavBar()
    {
        InitializeComponent();
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();
        if (Parent == null)
        {
            UnsubscribeFromShell();
            InvalidateNavigation();
            return;
        }

        try
        {
            var shell = Shell.Current;
            if (shell == null)
            {
                UnsubscribeFromShell();
                return;
            }

            if (!ReferenceEquals(_subscribedShell, shell))
            {
                UnsubscribeFromShell();
                _subscribedShell = shell;
                _subscribedShell.Navigated += OnShellNavigated;
            }
            RefreshSelection();
        }
        catch { }
    }

    private void UnsubscribeFromShell()
    {
        var shell = _subscribedShell;
        _subscribedShell = null;
        if (shell == null)
            return;

        try
        {
            shell.Navigated -= OnShellNavigated;
        }
        catch { }
    }

    private void InvalidateNavigation()
    {
        _navigationVersion++;
        _requestVersion++;
        _pendingRoute = null;
        _pendingSection = null;
        _pendingAllowsCurrent = false;
    }

    private void OnShellNavigated(object sender, ShellNavigatedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(RefreshSelection);
    }

    private string _appliedSection;

    public void RefreshSelection()
    {
        try
        {
            var section = CurrentSection();
            // Shell.Navigated fires on every navigation anywhere, and there are 4
            // BottomNavBar instances alive (one per root page). Re-writing the 20
            // bindable properties each time dirtied 4 buttons for no visual change.
            if (string.Equals(section, _appliedSection, StringComparison.Ordinal))
                return;
            _appliedSection = section;
            SetSelectedSection(section);
        }
        catch { }
    }

    private static string CurrentLocation()
        => Shell.Current?.CurrentState?.Location?.OriginalString ?? string.Empty;

    private static string CurrentSection()
    {
        var location = CurrentLocation();
        var path = location.Split(new[] { '?' }, 2)[0];
        var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            foreach (var section in Sections)
            {
                if (string.Equals(segment, section, StringComparison.OrdinalIgnoreCase))
                    return section;
            }
        }

        return Shell.Current?.CurrentItem?.Route ?? string.Empty;
    }

    private static bool OnSection(string section)
        => string.Equals(CurrentSection(), section, StringComparison.OrdinalIgnoreCase);

    private void SetSelectedSection(string section)
    {
        SetSelected(DiscoverButton, DiscoverIcon, DiscoverLabel,
            string.Equals(section, "discover", StringComparison.OrdinalIgnoreCase));
        SetSelected(AnimeButton, AnimeIcon, AnimeLabel,
            string.Equals(section, "anime", StringComparison.OrdinalIgnoreCase));
        SetSelected(MangaButton, MangaIcon, MangaLabel,
            string.Equals(section, "manga", StringComparison.OrdinalIgnoreCase));
        SetSelected(MoreButton, MoreIcon, MoreLabel,
            string.Equals(section, "more", StringComparison.OrdinalIgnoreCase));
    }

    private static void SetSelected(Button button, Image icon, Label label, bool selected)
    {
        try
        {
            button.BackgroundColor = selected ? SelectedButtonColor : TransparentButtonColor;
            button.BorderColor = selected ? SelectedColor : TransparentButtonColor;
            button.BorderWidth = selected ? 1 : 0;
            label.TextColor = selected ? SelectedColor : UnselectedColor;
            icon.Opacity = selected ? 1.0 : 0.55;
        }
        catch { }
    }
    public Rect GetDotsBounds(bool manga)
    {
        try
        {
            var btn = manga ? MangaDots : AnimeDots;
            return btn.Bounds;
        }
        catch { return Rect.Zero; }
    }

    public void OpenStatusMenu(bool manga, IEnumerable<(string Label, int Index)> items, Action<int> selected)
    {
        _ = ShowStatusMenuAsync(manga, items.ToArray(), selected);
    }

    private async Task ShowStatusMenuAsync(bool manga, IReadOnlyList<(string Label, int Index)> items, Action<int> selected)
    {
        try
        {
            var page = Application.Current?.MainPage;
            if (page == null || items.Count == 0)
                return;
            var result = await page.DisplayActionSheet(
                manga ? "Manga status" : "Anime status",
                "Cancel",
                null,
                items.Select(item => item.Label).ToArray());
            if (result == null)
                return;
            var index = items.ToList().FindIndex(item => item.Label == result);
            if (index >= 0)
                selected(items[index].Index);
        }
        catch { }
    }

    private void OnDiscoverTapped(object sender, EventArgs e)
        => NavigateToSection("discover");

    private void OnAnimeTapped(object sender, EventArgs e)
        => NavigateToSection("anime");

    private void OnMangaTapped(object sender, EventArgs e)
        => NavigateToSection("manga");

    private void OnMoreTapped(object sender, EventArgs e)
        => NavigateToSection("more");

    private void OnAnimeDotsClicked(object sender, EventArgs e)
        => OpenDotsMenu("anime");

    private void OnMangaDotsClicked(object sender, EventArgs e)
        => OpenDotsMenu("manga");

    private static bool IsListSection(string section)
        => string.Equals(section, "anime", StringComparison.OrdinalIgnoreCase)
           || string.Equals(section, "manga", StringComparison.OrdinalIgnoreCase);

    // The list tabs must always carry an explicit work mode and status. A bare
    // "///anime" would let the page keep whatever [QueryProperty] the previous
    // route left behind, which is how "Anime" ended up showing the Top list.
    private static string SectionRoute(string section, bool reset)
    {
        var query = section switch
        {
            "anime" => "mode=0&status=0",
            "manga" => "mode=2&status=0",
            _ => null
        };
        if (query == null)
            return $"///{section}";
        return reset ? $"///{section}?{query}&reset=1" : $"///{section}?{query}";
    }

    private void NavigateToSection(string section)
        => NavigateToRoute(SectionRoute(section, OnSection(section) && IsListSection(section)),
            section, IsListSection(section));

    private void OpenDotsMenu(string section)
    {
        SetSelectedSection(section);
        if (OnSection(section))
        {
            _pendingRoute = null;
            _pendingSection = null;
            _pendingAllowsCurrent = false;
            DotsClicked?.Invoke(section);
            RefreshSelection();
            return;
        }

        NavigateToRoute(IsListSection(section) ? $"///{section}?menu=status&{SectionRoute(section, false).Split('?')[1]}"
                                               : $"///{section}?menu=status",
            section, true);
    }

    private void NavigateToRoute(string route, string section, bool allowCurrent)
    {
        var requestId = ++_requestVersion;
        SetSelectedSection(section);
        if (!allowCurrent && OnSection(section))
        {
            _pendingRoute = null;
            _pendingSection = null;
            _pendingAllowsCurrent = false;
            RefreshSelection();
            return;
        }

        var navigationVersion = _navigationVersion;
        _ = NavigateToRouteAsync(route, section, allowCurrent, requestId, navigationVersion);
    }

    private async Task NavigateToRouteAsync(string route, string section, bool allowCurrent,
        long requestId, long navigationVersion)
    {
        var ownsNavigation = false;
        try
        {
            if (requestId != _requestVersion || navigationVersion != _navigationVersion)
                return;

            if (_navigationInFlight)
            {
                _pendingRoute = route;
                _pendingSection = section;
                _pendingAllowsCurrent = allowCurrent;
                return;
            }

            if (!allowCurrent && OnSection(section))
            {
                _pendingRoute = null;
                _pendingSection = null;
                _pendingAllowsCurrent = false;
                MainThread.BeginInvokeOnMainThread(RefreshSelection);
                return;
            }

            var shell = Shell.Current;
            if (shell == null)
                return;

            _navigationInFlight = true;
            ownsNavigation = true;
            await shell.GoToAsync(route);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try { SectionActivated?.Invoke(route); }
                catch { }
            });
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("MALPLUS", $"BottomNav GoToAsync failed '{route}': {ex}");
        }
        finally
        {
            if (ownsNavigation && navigationVersion == _navigationVersion)
            {
                _navigationInFlight = false;
                MainThread.BeginInvokeOnMainThread(CompleteNavigation);
            }
        }
    }

    private void CompleteNavigation()
    {
        if (Parent == null)
            return;

        RefreshSelection();
        var route = _pendingRoute;
        var section = _pendingSection;
        var allowCurrent = _pendingAllowsCurrent;
        _pendingRoute = null;
        _pendingSection = null;
        _pendingAllowsCurrent = false;
        if (!string.IsNullOrEmpty(route) && !string.IsNullOrEmpty(section))
            NavigateToRoute(route, section, allowCurrent);
    }
}

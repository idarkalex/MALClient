namespace MALPlus.Controls;

public partial class BottomNavBar : ContentView
{
    public event Action<string> DotsClicked;

    private static readonly Color SelectedColor = Color.FromArgb("#0066FF");
    private static readonly Color UnselectedColor = Color.FromArgb("#A0FFFFFF");
    private Shell _subscribedShell;

    public BottomNavBar()
    {
        InitializeComponent();
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();
        try
        {
            if (Parent != null && Shell.Current != null && !ReferenceEquals(_subscribedShell, Shell.Current))
            {
                if (_subscribedShell != null)
                    _subscribedShell.Navigated -= OnShellNavigated;
                _subscribedShell = Shell.Current;
                _subscribedShell.Navigated += OnShellNavigated;
            }
            RefreshSelection();
        }
        catch { }
    }

    private void OnShellNavigated(object sender, ShellNavigatedEventArgs e)
    {
        try
        {
            Console.WriteLine($"MALPLUS shell-nav {e.Previous?.Location} -> {e.Current?.Location}");
            MainThread.BeginInvokeOnMainThread(RefreshSelection);
        }
        catch { }
    }

    public void RefreshSelection()
    {
        try
        {
            SetSelected(DiscoverIcon, DiscoverLabel, OnSection("discover"));
            SetSelected(AnimeIcon, AnimeLabel, OnSection("anime"));
            SetSelected(MangaIcon, MangaLabel, OnSection("manga"));
            SetSelected(MoreIcon, MoreLabel, OnSection("more"));
        }
        catch { }
    }

    private static string CurrentLocation()
        => Shell.Current?.CurrentState?.Location?.OriginalString ?? string.Empty;

    private static bool OnSection(string section)
        => CurrentLocation().IndexOf(section, StringComparison.OrdinalIgnoreCase) >= 0;

    private static void SetSelected(Image icon, Label label, bool selected)
    {
        try
        {
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
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS status menu failed: " + ex.Message);
        }
    }

    private void OnDiscoverTapped(object o, EventArgs e)
        => _ = Shell.Current?.GoToAsync("///discover");

    private void OnAnimeTapped(object o, EventArgs e)
    {
        Console.WriteLine($"MALPLUS tab anime tapped loc={CurrentLocation()}");
        _ = Shell.Current?.GoToAsync("///anime");
    }

    private void OnMangaTapped(object o, EventArgs e)
    {
        Console.WriteLine($"MALPLUS tab manga tapped loc={CurrentLocation()}");
        _ = Shell.Current?.GoToAsync("///manga");
    }

    private void OnMoreTapped(object o, EventArgs e)
        => _ = Shell.Current?.GoToAsync("///more");

    private void OnAnimeDotsClicked(object sender, EventArgs e)
    {
        Console.WriteLine($"MALPLUS dots anime loc={CurrentLocation()}");
        if (OnSection("anime"))
            DotsClicked?.Invoke("anime");
        else
            _ = Shell.Current?.GoToAsync("///anime?menu=status");
    }

    private void OnMangaDotsClicked(object sender, EventArgs e)
    {
        Console.WriteLine($"MALPLUS dots manga loc={CurrentLocation()}");
        if (OnSection("manga"))
            DotsClicked?.Invoke("manga");
        else
            _ = Shell.Current?.GoToAsync("///manga?menu=status");
    }
}
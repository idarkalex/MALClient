using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALPlus.Views;

[QueryProperty(nameof(InitialMode), "mode")]
public partial class CalendarPage : ContentPage
{
    private bool _initialized;
    private string _initialMode;
    private bool _subscribed;

    /// <summary>
    ///     "mine" or "global", set by the two Discover calendar tiles. Unlike v2, which just moved
    ///     the pager to a tab, this has to flip the data source: the calendar is built by weekday
    ///     and none of the headers are ever "my list" or "airing now", so the old header search
    ///     could never match and the tiles silently did nothing.
    /// </summary>
    public string InitialMode
    {
        get => _initialMode;
        set
        {
            _initialMode = value;
            if (string.IsNullOrEmpty(value))
                return;
            SetCalendarMode(string.Equals(value, "global", StringComparison.OrdinalIgnoreCase));
        }
    }

    private CalendarPageViewModel Vm => (CalendarPageViewModel)BindingContext;

    public CalendarPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.CalendarPage;
        UpdateFilterButtons();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        UpdateFilterButtons();
        // Re-subscribe on every appearance: the page instance outlives a navigation, and the day
        // strip has to be rebuilt whenever the view model produces a fresh CalendarData.
        BuildTabStrip();
        if (_initialized)
            return;
        _initialized = true;
        try
        {
            await Vm.Init(false);
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("MALPlus Calendar", "Init failed: " + ex.GetType().Name);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_subscribed)
        {
            Vm.PropertyChanged -= OnVmPropertyChanged;
            _subscribed = false;
        }
    }

    // ---------- mode switch ----------

    private void OnSeasonModeTapped(object sender, TappedEventArgs e) => SetCalendarMode(true);

    private void OnMyListModeTapped(object sender, TappedEventArgs e) => SetCalendarMode(false);

    /// <summary>
    ///     Port of v2's SetCalendarMode: persist the preference, repaint the indicator and rebuild.
    ///     The rebuild is a force, since the view model short-circuits when it is already initialised.
    /// </summary>
    private void SetCalendarMode(bool allAiring)
    {
        if (Settings.CalendarShowAllAiring == allAiring && _initialized)
            return;
        Settings.CalendarShowAllAiring = allAiring;
        UpdateFilterButtons();
        if (!_initialized)
            return;
        _ = RebuildAsync();
    }

    private async Task RebuildAsync()
    {
        try
        {
            await Vm.Init(true);
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("MALPlus Calendar", "mode rebuild failed: " + ex.GetType().Name);
        }
    }

    private void UpdateFilterButtons()
    {
        try
        {
            var allAiring = Settings.CalendarShowAllAiring;
            // Read from the app resources rather than hardcoding, so the indicator cannot drift
            // away from the palette the XAML uses.
            var accent = ResolveColor("EmAccent", "#0066FF");
            var text = ResolveColor("BrushText", "#FFFFFF");

            SeasonFilterLabel.TextColor = allAiring ? accent : text;
            SeasonFilterIndicator.BackgroundColor = allAiring ? accent : Colors.Transparent;
            MyListFilterLabel.TextColor = allAiring ? text : accent;
            MyListFilterIndicator.BackgroundColor = allAiring ? Colors.Transparent : accent;
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("MALPlus Calendar", "UpdateFilterButtons: " + ex.GetType().Name);
        }
    }

    private static Color ResolveColor(string key, string fallback)
    {
        try
        {
            if (Application.Current?.Resources?.TryGetValue(key, out var value) == true && value is Color color)
                return color;
        }
        catch { }
        return Color.FromArgb(fallback);
    }

    // ---------- tab strip ----------

    private void BuildTabStrip()
    {
        if (Vm == null)
            return;
        if (!_subscribed)
        {
            Vm.PropertyChanged += OnVmPropertyChanged;
            _subscribed = true;
        }
        // Back-nav re-entry: the data is already there, so redraw without rebuilding it.
        if (Vm.CalendarData != null && Vm.CalendarData.Count > 0)
            OnVmPropertyChanged(Vm, new System.ComponentModel.PropertyChangedEventArgs(
                nameof(CalendarPageViewModel.CalendarData)));
    }

    private void OnVmPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CalendarPageViewModel.CalendarPivotIndex))
        {
            MainThread.BeginInvokeOnMainThread(HighlightActiveTab);
            return;
        }
        if (e.PropertyName != nameof(CalendarPageViewModel.CalendarData)) return;
        if (Vm?.CalendarData == null || Vm.CalendarData.Count == 0) return;
        MainThread.BeginInvokeOnMainThread(BuildDayStrip);
    }

    /// <summary>
    ///     One equal-width cell per page, hairline separators in between. Seven days plus Summary
    ///     fit the width, so there is no scroller to swallow taps.
    /// </summary>
    private void BuildDayStrip()
    {
        try
        {
            var count = Vm.CalendarData.Count;
            TabStrip.Children.Clear();
            TabStrip.ColumnDefinitions.Clear();
            TabStrip.RowDefinitions.Clear();
            TabStrip.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                    TabStrip.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1)));
                TabStrip.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            }

            for (int i = 0; i < count; i++)
            {
                var page = Vm.CalendarData[i];
                var index = i;
                var column = i * 2;

                var label = new Label
                {
                    // "Summary" is far wider than a three letter day, and a star column treats the
                    // child's desired width as its minimum, which pushed the last cell off screen.
                    Text = page is CalendarSummaryPivotPage ? "All" : page.Header,
                    FontFamily = "InterBold",
                    FontSize = 13,
                    HorizontalTextAlignment = TextAlignment.Center,
                    LineBreakMode = LineBreakMode.TailTruncation,
                    MaxLines = 1
                };
                var sub = new Label
                {
                    Text = page.Sub,
                    FontFamily = "Inter",
                    FontSize = 10,
                    TextColor = Color.FromArgb("#8FA8C0"),
                    HorizontalTextAlignment = TextAlignment.Center
                };

                var cell = new Grid
                {
                    RowDefinitions =
                    {
                        new RowDefinition(GridLength.Auto),
                        new RowDefinition(GridLength.Auto),
                        new RowDefinition(GridLength.Auto)
                    },
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star) },
                    // A transparent background is what makes a layout hit-testable on Android.
                    BackgroundColor = Colors.Transparent,
                    Padding = new Thickness(1, 2)
                };
                cell.Add(label);
                cell.Add(sub, 0, 1);

                var indicator = new BoxView
                {
                    HeightRequest = 3,
                    BackgroundColor = Colors.Transparent,
                    CornerRadius = new CornerRadius(2)
                };
                cell.Add(indicator, 0, 2);

                Grid.SetColumn(cell, column);
                TabStrip.Children.Add(cell);

                var tap = new TapGestureRecognizer();
                tap.Tapped += (s, e) => Vm.CalendarPivotIndex = index;
                cell.GestureRecognizers.Add(tap);

                if (i < count - 1)
                {
                    var separator = new BoxView
                    {
                        WidthRequest = 1,
                        Color = Color.FromArgb("#26FFFFFF"),
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Fill
                    };
                    Grid.SetColumn(separator, column + 1);
                    TabStrip.Children.Add(separator);
                }
            }

            HighlightActiveTab();
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("MALPlus Calendar", "BuildDayStrip: " + ex.GetType().Name);
        }
    }

    private void HighlightActiveTab()
    {
        try
        {
            var pages = Vm?.CalendarData;
            if (pages == null) return;
            for (int i = 0; i < TabStrip.Children.Count; i++)
            {
                if (TabStrip.Children[i] is not Grid cell) continue;
                if (cell.Children.Count < 3) continue;
                if (cell.Children[0] is not Label label) continue;
                if (cell.Children[2] is not BoxView indicator) continue;
                // Children are added column by column, so the active one is picked by its column.
                var column = Grid.GetColumn(cell);
                var index = column / 2;
                var active = index == Vm.CalendarPivotIndex && index < pages.Count;
                label.TextColor = active ? Colors.White : Color.FromArgb("#8FA8C0");
                indicator.BackgroundColor = active ? Color.FromArgb("#0066FF") : Colors.Transparent;
            }
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("MALPlus Calendar", "HighlightActiveTab: " + ex.GetType().Name);
        }
    }

    // ---------- card navigation ----------

    /// <summary>
    ///     The calendar is a browsing surface: a tap opens the entry, like everywhere else in the
    ///     app. The per-row actions of the old list layout have no room on a poster card, and a
    ///     press-and-hold is not available on Android in MAUI 8, so "Add to List" and
    ///     "Export to calendar" both live on the details page instead.
    /// </summary>
    private async void OnPosterTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not AnimeItemViewModel item)
            return;
        try
        {
            HighlightActiveTab();
            await Shell.Current.GoToAsync(
                $"animedetails?id={item.Id}&title={Uri.EscapeDataString(item.Title ?? string.Empty)}");
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("MALPlus Calendar", "poster nav failed: " + ex.GetType().Name);
        }
    }
}
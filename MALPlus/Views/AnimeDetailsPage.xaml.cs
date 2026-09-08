using MALClient.Models.Enums;
using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Details;

namespace MALPlus.Views;

[QueryProperty(nameof(MalId), "id")]
[QueryProperty(nameof(AnimeTitle), "title")]
[QueryProperty(nameof(InitialIsManga), "manga")]
public partial class AnimeDetailsPage : ContentPage
{
    private bool _initialized;
    private bool _episodesLoaded;
    private bool _reviewsLoaded;
    private bool _charactersLoaded;
    private bool _recommendationsLoaded;
    private bool _relatedLoaded;

    public string MalId { get; set; }
    public string AnimeTitle { get; set; }
    public string InitialIsManga { get; set; }

    private AnimeDetailsPageViewModel Vm => (AnimeDetailsPageViewModel)BindingContext;

    public AnimeDetailsPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.AnimeDetails;
        if (BindingContext is AnimeDetailsPageViewModel vm)
        {
            vm.ShowMoreRequested += OnShowMoreRequested;
            // PropertyChanged is subscribed here only; OnBindingContextChanged does NOT
            // re-subscribe (the VM is a singleton accessor reused across page instances).
            vm.PropertyChanged -= OnVmPropertyChanged;
            vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private async void OnShowMoreRequested()
    {
        try
        {
            var choice = await DisplayActionSheet("More",
                "Cancel", null,
                "Open trailer", "Refresh", "Open in MAL");
            if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;
            if (choice == "Open trailer") ShowVideoOverlay(Vm.TrailerUrl);
            else if (choice == "Refresh")
            {
                try { Vm.RefreshData(); } catch (Exception ex) { Console.WriteLine("Refresh failed: " + ex.Message); }
            }
            else if (choice == "Open in MAL")
            {
                try
                {
                    var url = $"https://myanimelist.net/anime/{Vm.Id}";
                    Microsoft.Maui.ApplicationModel.Launcher.OpenAsync(new Uri(url));
                }
                catch (Exception ex) { Console.WriteLine("OpenInMal failed: " + ex.Message); }
            }
        }
        catch (Exception ex) { Console.WriteLine("MALPLUS OnShowMoreRequested failed: " + ex.GetType().Name); }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized)
            return;
        _initialized = true;
        Console.WriteLine("MALPLUS Details OnAppearing id=" + MalId);
        try
        {
            var isManga = string.Equals(InitialIsManga, "true", StringComparison.OrdinalIgnoreCase);
            var args = new AnimeDetailsPageNavigationArgs(int.Parse(MalId), AnimeTitle, null, null, null);
            if (isManga) args.AnimeMode = false;
            Vm.Init(args, fakeDelay: false);
            // wait for the initial General data to land so we can shape the tab strip
            // (anime vs manga vs movie determines which tabs are visible)
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(200);
                if (!Vm.LoadingGlobal && !string.IsNullOrEmpty(Vm.Title))
                    break;
            }
            ApplyTabStripVisibility();
            ApplyCharacterStaffBindings();
            System.Diagnostics.Debug.WriteLine("MALPLUS Details Init returned title=" + Vm.Title
                + " rank=" + Vm.GeneralRank + " pop=" + Vm.GeneralPopularity
                + " studios=" + Vm.GeneralStudios + " animemode=" + Vm.AnimeMode);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("MALPLUS Details Init failed: " + ex);
        }
    }

    private void ApplyTabStripVisibility()
    {
        // Index 0=General, 1=Details, 2=Episodes, 3=Reviews, 4=Recs, 5=Related, 6=Characters, 7=Staff
        if (Vm == null || Vm.AnimeMode)
        {
            if (Vm != null && Vm.Type?.Equals("Movie", StringComparison.OrdinalIgnoreCase) == true)
            {
                // Movie: hide Episodes only
                TabEpisodes.IsVisible = false;
            }
        }
        else
        {
            // Manga: hide Episodes, Reviews, Recs, Related, Staff
            TabEpisodes.IsVisible = false;
            TabReviews.IsVisible = false;
            TabRecs.IsVisible = false;
            TabRelated.IsVisible = false;
            TabStaff.IsVisible = false;
        }
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        // PropertyChanged is subscribed in the constructor; do NOT subscribe again here,
        // otherwise the singleton VM gets a second handler and every property change fires twice.
    }

    private void OnVmPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AnimeDetailsPageViewModel.DetailsPivotSelectedIndex))
        {
            LoadTabData(Vm.DetailsPivotSelectedIndex);
            return;
        }
        // Re-bind characters/staff when mode/data changes.
        if (e.PropertyName == nameof(AnimeDetailsPageViewModel.AnimeMode) ||
            e.PropertyName == nameof(AnimeDetailsPageViewModel.AnimeStaffData) ||
            e.PropertyName == nameof(AnimeDetailsPageViewModel.MangaCharacterData))
        {
            ApplyCharacterStaffBindings();
        }
    }

    private void ApplyCharacterStaffBindings()
    {
        try
        {
            if (Vm == null) return;
            if (Vm.AnimeMode)
            {
                CharactersList.ItemsSource = Vm.AnimeStaffData?.AnimeCharacterPairs;
                StaffList.ItemsSource = Vm.AnimeStaffData?.AnimeStaff;
            }
            else
            {
                CharactersList.ItemsSource = Vm.MangaCharacterData;
                StaffList.ItemsSource = null; // staff tab is hidden for manga anyway
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS char/staff binding failed: " + ex.Message);
        }
    }

    private async void LoadTabData(int tabIndex)
    {
        try
        {
            switch (tabIndex)
            {
                case 2: // Episodes
                    if (!_episodesLoaded)
                    {
                        _episodesLoaded = true;
                        await Vm.LoadEpisodes(false);
                    }
                    break;
                case 3: // Reviews
                    if (!_reviewsLoaded)
                    {
                        _reviewsLoaded = true;
                        await Vm.LoadReviews(false);
                    }
                    break;
                case 4: // Recommendations
                    if (!_recommendationsLoaded)
                    {
                        _recommendationsLoaded = true;
                        await Vm.LoadRecommendations(false);
                    }
                    break;
                case 5: // Related
                    if (!_relatedLoaded)
                    {
                        _relatedLoaded = true;
                        await Vm.LoadRelatedAnime(false);
                    }
                    break;
                case 6: // Characters
                case 7: // Staff
                    if (!_charactersLoaded)
                    {
                        _charactersLoaded = true;
                        await Vm.LoadCharacters(false);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("MALPLUS LoadTabData failed: " + ex);
        }
    }

    // v2 parity: status/score buttons open pickers (AnimeUpdateDialogBuilder),
    // the VM commands take the picked value as string and crash without one.
    private async void OnStatusClicked(object sender, EventArgs e)
    {
        try
        {
            var options = new[]
            {
                AnimeStatus.Watching, AnimeStatus.Completed, AnimeStatus.OnHold,
                AnimeStatus.Dropped, AnimeStatus.PlanToWatch
            };
            var labels = options.Select(s => Utilities.StatusToString((int)s, !Vm.AnimeMode, false)).ToArray();
            var choice = await DisplayActionSheet("Set status", "Cancel", null, labels);
            if (string.IsNullOrEmpty(choice) || choice == "Cancel")
                return;
            var index = Array.IndexOf(labels, choice);
            if (index >= 0)
                Vm.ChangeStatus(options[index]);
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS OnStatusClicked failed: " + ex.GetType().Name);
        }
    }

    private async void OnScoreClicked(object sender, EventArgs e)
    {
        try
        {
            var choice = await DisplayActionSheet("Set score", "Cancel", null,
                "10", "9", "8", "7", "6", "5", "4", "3", "2", "1", "0 (clear)");
            if (string.IsNullOrEmpty(choice) || choice == "Cancel")
                return;
            var num = choice.Split(' ')[0];
            if (int.TryParse(num, out var score))
                Vm.ChangeScoreCommand.Execute(score.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS OnScoreClicked failed: " + ex.GetType().Name);
        }
    }

    private void OnTrailerClicked(object sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(Vm.TrailerUrl)) return;
            ShowVideoOverlay(Vm.TrailerUrl);
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS OnTrailerClicked failed: " + ex.GetType().Name);
        }
    }

    private void OnOpEdTapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (e.Parameter is string text && !string.IsNullOrWhiteSpace(text))
            {
                // Build a YouTube search URL for the OP/ED song
                var q = Uri.EscapeDataString(text);
                ShowVideoOverlay($"https://www.youtube.com/results?search_query={q}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS OnOpEdTapped failed: " + ex.GetType().Name);
        }
    }

    private async void OnCharacterTapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (e.Parameter is string idStr && int.TryParse(idStr, out int id))
            {
                await Shell.Current.GoToAsync($"character?id={id}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS OnCharacterTapped failed: " + ex.GetType().Name);
        }
    }

    private async void OnStaffTapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (e.Parameter is string idStr && int.TryParse(idStr, out int id))
            {
                await Shell.Current.GoToAsync($"staff?id={id}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS OnStaffTapped failed: " + ex.GetType().Name);
        }
    }

    private void ShowVideoOverlay(string url)
    {
        try
        {
            if (VideoOverlay == null || VideoWebView == null) return;
            VideoOverlay.IsVisible = true;
            // Convert watch?v= / youtu.be / results?search_query= → /embed/
            var embed = BuildYouTubeEmbed(url);
            var html = $"<!DOCTYPE html><html><head><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"/>" +
                       $"<style>html,body{{margin:0;background:#000;height:100%}}iframe{{width:100%;height:100%;border:0}}</style>" +
                       $"</head><body><iframe src=\"{embed}\" allowfullscreen></iframe></body></html>";
            VideoWebView.Source = new HtmlWebViewSource { Html = html, BaseUrl = "https://www.youtube.com" };
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS ShowVideoOverlay failed: " + ex.GetType().Name);
        }
    }

    private void CloseVideoOverlay(object sender, EventArgs e)
    {
        try
        {
            if (VideoWebView != null) VideoWebView.Source = null;
            if (VideoOverlay != null) VideoOverlay.IsVisible = false;
        }
        catch { }
    }

    private static string BuildYouTubeEmbed(string url)
    {
        try
        {
            if (string.IsNullOrEmpty(url)) return url;
            // watch?v=ID
            var m = System.Text.RegularExpressions.Regex.Match(url, @"youtube\.com/watch\?v=([\w\-]+)");
            if (m.Success) return $"https://www.youtube.com/embed/{m.Groups[1].Value}";
            // youtu.be/ID
            m = System.Text.RegularExpressions.Regex.Match(url, @"youtu\.be/([\w\-]+)");
            if (m.Success) return $"https://www.youtube.com/embed/{m.Groups[1].Value}";
            // /embed/ID
            m = System.Text.RegularExpressions.Regex.Match(url, @"/embed/([\w\-]+)");
            if (m.Success) return url;
            // search → list=search query
            m = System.Text.RegularExpressions.Regex.Match(url, @"search_query=([^&]+)");
            if (m.Success) return $"https://www.youtube.com/embed/results?search_query={m.Groups[1].Value}";
            return url;
        }
        catch { return url; }
    }

    private void OnTabTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is string param && int.TryParse(param, out int tabIndex))
        {
            GoToTab(tabIndex);
        }
    }

    private void OnPagerSwiped(object sender, SwipedEventArgs e)
    {
        if (Vm == null) return;
        var current = Vm.DetailsPivotSelectedIndex;
        // Map index to next visible tab honoring manga/movie hide rules
        var max = GetMaxTabIndex();
        if (e.Direction == SwipeDirection.Left)
        {
            var next = current + 1;
            while (next <= max && !IsTabVisible(next)) next++;
            if (next <= max) GoToTab(next);
        }
        else if (e.Direction == SwipeDirection.Right)
        {
            var prev = current - 1;
            while (prev >= 0 && !IsTabVisible(prev)) prev--;
            if (prev >= 0) GoToTab(prev);
        }
    }

    private void GoToTab(int tabIndex)
    {
        Vm.DetailsPivotSelectedIndex = tabIndex;
        ResetHero();
        ScrollActiveTabToTop();
    }

    private int GetMaxTabIndex() => 7;

    private bool IsTabVisible(int index)
    {
        if (Vm == null) return true;
        if (!Vm.AnimeMode)
        {
            // manga: hide 2,3,4,5,7
            if (index == 2 || index == 3 || index == 4 || index == 5 || index == 7) return false;
        }
        else if (Vm.Type?.Equals("Movie", StringComparison.OrdinalIgnoreCase) == true)
        {
            // movie: hide 2 (Episodes)
            if (index == 2) return false;
        }
        return true;
    }

    // v2 hero mechanics (AnimeDetailsPageFragment AppBarOffsetListener):
    // ratio = scrollY / (460 - 210); poster scale = 1 + 0.35 * ratio; scrim alpha = ratio.
    private const double HeroExpandedHeight = 460;
    private const double HeroCollapsedHeight = 210;
    private const double HeroCollapseRange = HeroExpandedHeight - HeroCollapsedHeight;
    private double _lastCollapseRatio = -1;

    private void OnTabContentScrolled(object sender, ScrolledEventArgs e)
    {
        ApplyHeroCollapse(e.ScrollY);
    }

    private void OnTabItemsScrolled(object sender, ItemsViewScrolledEventArgs e)
    {
        ApplyHeroCollapse(e.VerticalOffset);
    }

    private void ApplyHeroCollapse(double scrollY)
    {
        var ratio = Math.Clamp(scrollY / HeroCollapseRange, 0, 1);
        if (Math.Abs(ratio - _lastCollapseRatio) < 0.01)
            return;
        _lastCollapseRatio = ratio;
        HeroContainer.HeightRequest = HeroExpandedHeight - HeroCollapseRange * ratio;
        var scale = 1 + 0.35 * ratio;
        PosterContainer.ScaleX = scale;
        PosterContainer.ScaleY = scale;
        HeroScrim.Opacity = ratio;
    }

    private void ResetHero()
    {
        _lastCollapseRatio = -1;
        ApplyHeroCollapse(0);
    }

    private void ScrollActiveTabToTop()
    {
        try
        {
            switch (Vm.DetailsPivotSelectedIndex)
            {
                case 0: _ = GeneralScroll.ScrollToAsync(0, 0, false); break;
                case 1: _ = DetailsScroll.ScrollToAsync(0, 0, false); break;
                case 2: EpisodesList.ScrollTo(0, animate: false); break;
                case 3: ReviewsList.ScrollTo(0, animate: false); break;
                case 4: RecsList.ScrollTo(0, animate: false); break;
                case 5: RelatedList.ScrollTo(0, animate: false); break;
                case 6: CharactersList.ScrollTo(0, animate: false); break;
                case 7: StaffList.ScrollTo(0, animate: false); break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("MALPLUS ScrollActiveTabToTop failed: " + ex.GetType().Name);
        }
    }
}
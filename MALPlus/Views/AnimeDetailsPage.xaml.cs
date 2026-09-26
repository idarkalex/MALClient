using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using MALClient.Models.Enums;
using MALClient.Models.Models.Anime;
using MALClient.Models.Models.AnimeScrapped;
using MALClient.Models.Models.Favourites;
using MALClient.Models.Interfaces;
using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Details;
using MALPlus.Services;

namespace MALPlus.Views;

[QueryProperty(nameof(MalId), "id")]
[QueryProperty(nameof(AnimeTitle), "title")]
[QueryProperty(nameof(InitialIsManga), "manga")]
[QueryProperty(nameof(InitialTabIndex), "tab")]
public partial class AnimeDetailsPage : ContentPage
{
    private const int ProgressiveChunkSize = 40;
    private const double HeroExpandedHeight = 460;
    private const double HeroCollapsedHeight = 210;
    private const double HeroCollapseRange = HeroExpandedHeight - HeroCollapsedHeight;
    private const uint TabTransitionLength = 160;

    private bool _initialized;
    private int _initializedId;
    private bool _initializedAnimeMode;
    private int _entryVersion;
    private bool _isAppearing;
    private bool _episodesLoaded;
    private bool _reviewsLoaded;
    private bool _charactersLoaded;
    private bool _recommendationsLoaded;
    private bool _relatedLoaded;
    private readonly HashSet<(int Tab, int Version)> _loadingTabs = new();
    private AnimeDetailsPageViewModel _subscribedVm;
    private int _headerSpacerRetry;
    private double _headerPanLastY;
    private bool _headerPanActive;
    private double _lastViewportWidth = -1;
    private double _lastViewportHeight = -1;
    private string _generalError = string.Empty;
    private string _detailsError = string.Empty;
    private string _episodesError = string.Empty;
    private string _reviewsError = string.Empty;
    private string _recommendationsError = string.Empty;
    private string _relatedError = string.Empty;
    private string _charactersError = string.Empty;
    private string _staffError = string.Empty;
#if ANDROID
    private bool _videoHandlerSubscribed;
#endif
    private WebView _videoWebView;

    public string MalId { get; set; }
    public string AnimeTitle { get; set; }
    public string InitialIsManga { get; set; }
    public int InitialTabIndex { get; set; }

    public ObservableCollection<AnimeEpisode> DisplayedEpisodes { get; } = new();
    public ObservableCollection<AnimeReviewData> DisplayedReviews { get; } = new();
    public ObservableCollection<AnimeCharacterCard> DisplayedCharacters { get; } = new();
    public ObservableCollection<FavouriteViewModel> DisplayedMangaCharacters { get; } = new();
    public ObservableCollection<FavouriteViewModel> DisplayedStaff { get; } = new();

    public string GeneralErrorText => EmptyStateText(_generalError, "Unable to load general details.");
    public bool GeneralErrorVisible => !string.IsNullOrWhiteSpace(_generalError);
    public bool GeneralLoadingVisible => Vm?.LoadingGlobal == true;
    public bool GeneralContentVisible => Vm != null && !Vm.LoadingGlobal && !GeneralErrorVisible;
    public bool GeneralSynopsisEmptyVisible => GeneralContentVisible && string.IsNullOrWhiteSpace(Vm.Synopsis);

    public string DetailsErrorText => EmptyStateText(_detailsError, "Unable to load details.");
    public bool DetailsErrorVisible => !string.IsNullOrWhiteSpace(_detailsError);
    public bool DetailsEmptyVisible => Vm != null && !Vm.LoadingDetails && !DetailsErrorVisible && !HasDetailsContent();
    public bool DetailsContentVisible => Vm != null && !Vm.LoadingDetails && !DetailsErrorVisible && HasDetailsContent();

    public string EpisodesErrorText => EmptyStateText(_episodesError, "Unable to load episodes.");
    public bool EpisodesErrorVisible => !string.IsNullOrWhiteSpace(_episodesError);
    public bool EpisodesEmptyVisible => Vm != null && !Vm.LoadingEpisodes && !EpisodesErrorVisible && Vm.Episodes.Count == 0;
    public bool EpisodesContentVisible => Vm != null && !Vm.LoadingEpisodes && !EpisodesErrorVisible && Vm.Episodes.Count > 0;

    public string ReviewsErrorText => EmptyStateText(_reviewsError, "Unable to load reviews.");
    public bool ReviewsErrorVisible => !string.IsNullOrWhiteSpace(_reviewsError);
    public bool ReviewsEmptyVisible => Vm != null && !Vm.LoadingReviews && !ReviewsErrorVisible && Vm.Reviews.Count == 0;
    public bool ReviewsContentVisible => Vm != null && !Vm.LoadingReviews && !ReviewsErrorVisible && Vm.Reviews.Count > 0;

    public string RecommendationsErrorText => EmptyStateText(_recommendationsError, "Unable to load recommendations.");
    public bool RecommendationsErrorVisible => !string.IsNullOrWhiteSpace(_recommendationsError);
    public bool RecommendationsEmptyVisible => Vm != null && !Vm.LoadingRecommendations && !RecommendationsErrorVisible && Vm.Recommendations.Count == 0;
    public bool RecommendationsContentVisible => Vm != null && !Vm.LoadingRecommendations && !RecommendationsErrorVisible && Vm.Recommendations.Count > 0;

    public string RelatedErrorText => EmptyStateText(_relatedError, "Unable to load related titles.");
    public bool RelatedErrorVisible => !string.IsNullOrWhiteSpace(_relatedError);
    public bool RelatedEmptyVisible => Vm != null && !Vm.LoadingRelated && !RelatedErrorVisible && Vm.RelatedAnime.Count == 0;
    public bool RelatedContentVisible => Vm != null && !Vm.LoadingRelated && !RelatedErrorVisible && Vm.RelatedAnime.Count > 0;

    public string CharactersErrorText => EmptyStateText(_charactersError, "Unable to load characters.");
    public bool CharactersErrorVisible => !string.IsNullOrWhiteSpace(_charactersError);
    public bool CharactersEmptyVisible => Vm != null && !Vm.LoadingCharactersVisibility && !CharactersErrorVisible &&
        (Vm.AnimeMode ? Vm.AnimeStaffData?.AnimeCharacterPairs?.Count > 0 : Vm.MangaCharacterData?.Count > 0) == false;
    public bool CharactersContentVisible => Vm != null && !Vm.LoadingCharactersVisibility && !CharactersErrorVisible &&
        (Vm.AnimeMode ? Vm.AnimeStaffData?.AnimeCharacterPairs?.Count > 0 : Vm.MangaCharacterData?.Count > 0) == true;

    public string StaffErrorText => EmptyStateText(_staffError, "Unable to load staff.");
    public bool StaffErrorVisible => !string.IsNullOrWhiteSpace(_staffError);
    public bool StaffEmptyVisible => Vm != null && !Vm.LoadingCharactersVisibility && !StaffErrorVisible && (Vm.AnimeStaffData?.AnimeStaff?.Count ?? 0) == 0;
    public bool StaffContentVisible => Vm != null && !Vm.LoadingCharactersVisibility && !StaffErrorVisible && (Vm.AnimeStaffData?.AnimeStaff?.Count ?? 0) > 0;

    private AnimeDetailsPageViewModel Vm => BindingContext as AnimeDetailsPageViewModel;

    public AnimeDetailsPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.AnimeDetails;
        ApplyTabActiveState();
    }

    private void AttachSubscriptions()
    {
        DetachVmSubscriptions();
        if (BindingContext is AnimeDetailsPageViewModel vm)
        {
            _subscribedVm = vm;
            vm.ShowMoreRequested += OnShowMoreRequested;
            vm.PropertyChanged += OnVmPropertyChanged;
            vm.Episodes.CollectionChanged += OnEpisodesSourceChanged;
            vm.Reviews.CollectionChanged += OnReviewsSourceChanged;
            vm.LeftGenres.CollectionChanged += OnDetailsSourceChanged;
            vm.RightGenres.CollectionChanged += OnDetailsSourceChanged;
            vm.Information.CollectionChanged += OnDetailsSourceChanged;
            vm.Stats.CollectionChanged += OnDetailsSourceChanged;
            vm.OPs.CollectionChanged += OnDetailsSourceChanged;
            vm.EDs.CollectionChanged += OnDetailsSourceChanged;
            vm.Recommendations.CollectionChanged += OnRecommendationsSourceChanged;
            vm.RelatedAnime.CollectionChanged += OnRelatedSourceChanged;
            RefreshDisplayedEpisodes(false);
            RefreshDisplayedReviews(false);
        }
#if ANDROID
        if (!_videoHandlerSubscribed)
        {
            _videoHandlerSubscribed = true;
        }
#endif
    }

    private void DetachSubscriptions()
    {
        DetachVmSubscriptions();
#if ANDROID
        if (_videoHandlerSubscribed)
        {
            if (VideoWebView != null)
                VideoWebView.HandlerChanged -= OnVideoWebViewHandlerChanged;
            _videoHandlerSubscribed = false;
        }
#endif
    }

    private void DetachVmSubscriptions()
    {
        if (_subscribedVm == null)
            return;
        _subscribedVm.ShowMoreRequested -= OnShowMoreRequested;
        _subscribedVm.PropertyChanged -= OnVmPropertyChanged;
        _subscribedVm.Episodes.CollectionChanged -= OnEpisodesSourceChanged;
        _subscribedVm.Reviews.CollectionChanged -= OnReviewsSourceChanged;
        _subscribedVm.LeftGenres.CollectionChanged -= OnDetailsSourceChanged;
        _subscribedVm.RightGenres.CollectionChanged -= OnDetailsSourceChanged;
        _subscribedVm.Information.CollectionChanged -= OnDetailsSourceChanged;
        _subscribedVm.Stats.CollectionChanged -= OnDetailsSourceChanged;
        _subscribedVm.OPs.CollectionChanged -= OnDetailsSourceChanged;
        _subscribedVm.EDs.CollectionChanged -= OnDetailsSourceChanged;
        _subscribedVm.Recommendations.CollectionChanged -= OnRecommendationsSourceChanged;
        _subscribedVm.RelatedAnime.CollectionChanged -= OnRelatedSourceChanged;
        _subscribedVm = null;
    }

#if ANDROID
    private void OnVideoWebViewHandlerChanged(object sender, EventArgs e)
    {
        try
        {
            var platformView = VideoWebView?.Handler?.PlatformView as global::Android.Views.View;
            if (platformView == null)
                return;
            VideoWebViewHelper.ConfigurePlatformView(platformView);
            VideoWebViewHelper.Resume(platformView);
        }
        catch { }
    }

    private void ResumeVideoWebView()
    {
        try
        {
            var platformView = VideoWebView?.Handler?.PlatformView as global::Android.Views.View;
            if (platformView != null)
                VideoWebViewHelper.Resume(platformView);
        }
        catch { }
    }

    private static void SetSystemBars(bool video)
    {
        try
        {
            var window = Platform.CurrentActivity?.Window;
            if (window == null)
                return;
            window.SetStatusBarColor(video
                ? global::Android.Graphics.Color.Black
                : global::Android.Graphics.Color.ParseColor("#051522"));
            window.SetNavigationBarColor(global::Android.Graphics.Color.Black);
        }
        catch { }
    }
#else
    private void ResumeVideoWebView() { }
    private static void SetSystemBars(bool video) { }
#endif

    private async void OnShowMoreRequested()
    {
        try
        {
            var choice = await DisplayActionSheet("More",
                "Cancel", null,
                "Open trailer", "Refresh", "Open in MAL");
            if (string.IsNullOrEmpty(choice) || choice == "Cancel")
                return;
            if (choice == "Open trailer")
                ShowVideoOverlay(Vm.TrailerUrl);
            else if (choice == "Refresh")
            {
                try { await Vm.RefreshDataAsync(); }
                catch (Exception ex) { Console.WriteLine("Refresh failed: " + ex.Message); }
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

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _isAppearing = true;
        AttachSubscriptions();
        try
        {
            if (!int.TryParse(MalId, out var id) || id <= 0)
                return;

            var isManga = string.Equals(InitialIsManga, "true", StringComparison.OrdinalIgnoreCase);
            var handoff = MauiDetailsNavigationHandoff.Take(id, !isManga);
            var args = handoff ?? new AnimeDetailsPageNavigationArgs(id, AnimeTitle, null, null, null)
            {
                SourceTabIndex = Math.Clamp(InitialTabIndex, 0, 7)
            };
            if (isManga)
                args.AnimeMode = false;
            if (handoff != null)
                args.AnimeMode = handoff.AnimeMode;

            var entryChanged = !_initialized || _initializedId != args.Id || _initializedAnimeMode != args.AnimeMode ||
                               Vm.Id != args.Id || Vm.AnimeMode != args.AnimeMode;
            if (!entryChanged)
                return;

            _initialized = true;
            _initializedId = args.Id;
            _initializedAnimeMode = args.AnimeMode;
            _entryVersion++;
            ResetTabLoadedFlags();
            ResetPageStateForEntry();
            _loadingTabs.Clear();
            ApplyTabStripVisibilityFromParams(args.AnimeMode);
            ApplyCharacterStaffBindings();
            _ = InitAsyncSafe(Vm, args);
        }
        catch (Exception ex)
        {
            _generalError = "Unable to open this title.";
            NotifyStateProperties();
            System.Diagnostics.Debug.WriteLine("MALPLUS Details Init failed: " + ex);
        }
    }

    private async Task InitAsyncSafe(AnimeDetailsPageViewModel vm, AnimeDetailsPageNavigationArgs args)
    {
        try
        {
            _generalError = string.Empty;
            NotifyStateProperties();
            await vm.InitAsync(args, fakeDelay: false);
            while (vm.LoadingGlobal)
                await Task.Delay(100);
            if (PageScroll != null)
                await PageScroll.ScrollToAsync(0, 0, false);
            ApplyHeroState(0);
            var initialTab = Math.Clamp(args.SourceTabIndex, 0, 7);
            vm.DetailsPivotSelectedIndex = IsTabVisible(initialTab) ? initialTab : 0;
            ApplyTabStripVisibility();
            ApplyTabActiveState();
            ApplyCharacterStaffBindings();
            Dispatcher.Dispatch(UpdateHeaderSpacer);
            NotifyStateProperties();
            System.Diagnostics.Debug.WriteLine("MALPLUS Details Init returned title=" + vm.Title
                + " rank=" + vm.GeneralRank + " pop=" + vm.GeneralPopularity
                + " studios=" + vm.GeneralStudios + " animemode=" + vm.AnimeMode);
        }
        catch (Exception ex)
        {
            _generalError = "Unable to load this title.";
            NotifyStateProperties();
            System.Diagnostics.Debug.WriteLine("MALPLUS Details Init failed: " + ex);
        }
    }

    private void ApplyTabStripVisibilityFromParams(bool isManga)
    {
        TabGeneral.IsVisible = true;
        TabDetails.IsVisible = true;
        TabEpisodes.IsVisible = !isManga;
        TabReviews.IsVisible = !isManga;
        TabRecs.IsVisible = !isManga;
        TabRelated.IsVisible = !isManga;
        TabCharacters.IsVisible = true;
        TabStaff.IsVisible = !isManga;
        ApplyTabActiveState();
    }

    private void ApplyTabStripVisibility()
    {
        TabGeneral.IsVisible = true;
        TabDetails.IsVisible = true;
        TabEpisodes.IsVisible = true;
        TabReviews.IsVisible = true;
        TabRecs.IsVisible = true;
        TabRelated.IsVisible = true;
        TabCharacters.IsVisible = true;
        TabStaff.IsVisible = true;
        if (Vm == null)
            return;
        if (!Vm.AnimeMode)
        {
            TabEpisodes.IsVisible = false;
            TabReviews.IsVisible = false;
            TabRecs.IsVisible = false;
            TabRelated.IsVisible = false;
            TabStaff.IsVisible = false;
        }
        else if (Vm.Type?.Equals("Movie", StringComparison.OrdinalIgnoreCase) == true)
        {
            TabEpisodes.IsVisible = false;
        }
        ApplyTabActiveState();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _isAppearing = false;
        DetachSubscriptions();
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        DetachVmSubscriptions();
        if (_isAppearing)
            AttachSubscriptions();
    }

    private void OnVmPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AnimeDetailsPageViewModel.DetailsPivotSelectedIndex))
        {
            ApplyTabActiveState();
            if (Vm.Id == _initializedId && Vm.AnimeMode == _initializedAnimeMode)
                LoadTabData(Vm.DetailsPivotSelectedIndex);
            return;
        }
        if (e.PropertyName == nameof(AnimeDetailsPageViewModel.AnimeMode) ||
            e.PropertyName == nameof(AnimeDetailsPageViewModel.AnimeStaffData) ||
            e.PropertyName == nameof(AnimeDetailsPageViewModel.MangaCharacterData))
        {
            ApplyTabStripVisibility();
            ApplyCharacterStaffBindings();
        }
        if (e.PropertyName == nameof(AnimeDetailsPageViewModel.Type))
        {
            ApplyTabStripVisibility();
            if (!IsTabVisible(Vm.DetailsPivotSelectedIndex))
                Vm.DetailsPivotSelectedIndex = FindAdjacentVisibleTab(Vm.DetailsPivotSelectedIndex, 1) ?? 0;
        }
        if (e.PropertyName == nameof(AnimeDetailsPageViewModel.LoadingGlobal) ||
            e.PropertyName == nameof(AnimeDetailsPageViewModel.LoadingDetails) ||
            e.PropertyName == nameof(AnimeDetailsPageViewModel.LoadingEpisodes) ||
            e.PropertyName == nameof(AnimeDetailsPageViewModel.LoadingReviews) ||
            e.PropertyName == nameof(AnimeDetailsPageViewModel.LoadingRecommendations) ||
            e.PropertyName == nameof(AnimeDetailsPageViewModel.LoadingRelated) ||
            e.PropertyName == nameof(AnimeDetailsPageViewModel.LoadingCharactersVisibility) ||
            e.PropertyName == nameof(AnimeDetailsPageViewModel.Synopsis) ||
            e.PropertyName == nameof(AnimeDetailsPageViewModel.DetailedDataVisibility))
        {
            NotifyStateProperties();
        }
    }

    private void ApplyCharacterStaffBindings()
    {
        try
        {
            if (Vm == null)
                return;
            var animeMode = Vm.AnimeMode;
            var pairs = animeMode ? Vm.AnimeStaffData?.AnimeCharacterPairs : null;
            var mangaCharacters = animeMode ? null : Vm.MangaCharacterData;
            var staff = animeMode ? Vm.AnimeStaffData?.AnimeStaff : null;
            DisplayedCharacters.Clear();
            DisplayedMangaCharacters.Clear();
            DisplayedStaff.Clear();
            foreach (var pair in pairs?.Take(ProgressiveChunkSize) ?? Enumerable.Empty<AnimeDetailsPageViewModel.AnimeStaffDataViewModels.AnimeCharacterStaffModelViewModel>())
                DisplayedCharacters.Add(new AnimeCharacterCard(pair));
            foreach (var character in mangaCharacters?.Take(ProgressiveChunkSize) ?? Enumerable.Empty<FavouriteViewModel>())
                DisplayedMangaCharacters.Add(character);
            foreach (var person in staff?.Take(ProgressiveChunkSize) ?? Enumerable.Empty<FavouriteViewModel>())
                DisplayedStaff.Add(person);
            NotifyStateProperties();
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS char/staff binding failed: " + ex.Message);
        }
    }

    private void ResetTabLoadedFlags()
    {
        _episodesLoaded = false;
        _reviewsLoaded = false;
        _charactersLoaded = false;
        _recommendationsLoaded = false;
        _relatedLoaded = false;
        ResetReviewExpansionState();
    }

    private void ResetPageStateForEntry()
    {
        _generalError = string.Empty;
        _detailsError = string.Empty;
        _episodesError = string.Empty;
        _reviewsError = string.Empty;
        _recommendationsError = string.Empty;
        _relatedError = string.Empty;
        _charactersError = string.Empty;
        _staffError = string.Empty;
        ResetProgressiveCollections();
        ResetHeaderLayoutState();
        NotifyStateProperties();
    }

    private void ResetProgressiveCollections()
    {
        DisplayedEpisodes.Clear();
        DisplayedReviews.Clear();
        DisplayedCharacters.Clear();
        DisplayedMangaCharacters.Clear();
        DisplayedStaff.Clear();
    }

    private async void LoadTabData(int tabIndex)
    {
        var entryVersion = _entryVersion;
        var loadingKey = (tabIndex, entryVersion);
        if (!_loadingTabs.Add(loadingKey))
            return;

        var entryId = _initializedId;
        var entryAnimeMode = _initializedAnimeMode;
        try
        {
            SetTabError(tabIndex, null);
            switch (tabIndex)
            {
                case 1:
                    await Vm.LoadDetails(false);
                    break;
                case 2:
                    if (!_episodesLoaded)
                    {
                        await Vm.LoadEpisodes(false);
                        if (entryId == _initializedId && entryAnimeMode == _initializedAnimeMode)
                        {
                            _episodesLoaded = Vm.EpisodesLoaded;
                            RefreshDisplayedEpisodes(false);
                        }
                    }
                    break;
                case 3:
                    if (!_reviewsLoaded)
                    {
                        await Vm.LoadReviews(false);
                        if (entryId == _initializedId && entryAnimeMode == _initializedAnimeMode)
                        {
                            _reviewsLoaded = Vm.ReviewsLoaded;
                            ResetReviewExpansionState();
                            RefreshDisplayedReviews(false);
                        }
                    }
                    break;
                case 4:
                    if (!_recommendationsLoaded)
                    {
                        await Vm.LoadRecommendations(false);
                        if (entryId == _initializedId && entryAnimeMode == _initializedAnimeMode)
                            _recommendationsLoaded = Vm.RecommendationsLoaded;
                    }
                    break;
                case 5:
                    if (!_relatedLoaded)
                    {
                        await Vm.LoadRelatedAnime(false);
                        if (entryId == _initializedId && entryAnimeMode == _initializedAnimeMode)
                            _relatedLoaded = Vm.RelatedLoaded;
                    }
                    break;
                case 6:
                case 7:
                    if (!_charactersLoaded)
                    {
                        await Vm.LoadCharacters(false);
                        if (entryId == _initializedId && entryAnimeMode == _initializedAnimeMode)
                        {
                            _charactersLoaded = Vm.CharactersLoaded;
                            ApplyCharacterStaffBindings();
                        }
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            if (entryId == _initializedId && entryAnimeMode == _initializedAnimeMode)
            {
                ResetTabLoadedFlags();
                SetTabError(tabIndex, ex);
            }
            System.Diagnostics.Debug.WriteLine("MALPLUS LoadTabData failed: " + ex);
        }
        finally
        {
            _loadingTabs.Remove(loadingKey);
        }
    }

    private void OnEpisodesSourceChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshDisplayedEpisodes(false);
        NotifyStateProperties();
    }

    private void OnReviewsSourceChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
            ResetReviewExpansionState();
        RefreshDisplayedReviews(false);
        NotifyStateProperties();
    }

    private void OnDetailsSourceChanged(object sender, NotifyCollectionChangedEventArgs e)
        => NotifyStateProperties();

    private void OnRecommendationsSourceChanged(object sender, NotifyCollectionChangedEventArgs e)
        => NotifyStateProperties();

    private void OnRelatedSourceChanged(object sender, NotifyCollectionChangedEventArgs e)
        => NotifyStateProperties();

    private void RefreshDisplayedEpisodes(bool reset)
    {
        var source = Vm?.Episodes;
        if (source == null)
            return;
        if (reset || source.Count < DisplayedEpisodes.Count)
            DisplayedEpisodes.Clear();
        var target = Math.Min(source.Count, Math.Max(ProgressiveChunkSize, DisplayedEpisodes.Count));
        for (var index = DisplayedEpisodes.Count; index < target; index++)
            DisplayedEpisodes.Add(source[index]);
    }

    private void RefreshDisplayedReviews(bool reset)
    {
        var source = Vm?.Reviews;
        if (source == null)
            return;
        if (reset || source.Count < DisplayedReviews.Count)
            DisplayedReviews.Clear();
        var target = Math.Min(source.Count, Math.Max(ProgressiveChunkSize, DisplayedReviews.Count));
        for (var index = DisplayedReviews.Count; index < target; index++)
            DisplayedReviews.Add(source[index]);
    }

    private void ResetReviewExpansionState()
    {
        if (Vm == null)
            return;
        foreach (var review in Vm.Reviews)
            review.IsExpanded = false;
    }

    private async void OnEpisodeTapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (e.Parameter is not AnimeEpisode episode || string.IsNullOrWhiteSpace(episode.ForumUrl))
                return;
            var match = System.Text.RegularExpressions.Regex.Match(
                episode.ForumUrl,
                @"(?:topicid|topic)=([0-9]+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success)
                return;
            await Shell.Current.GoToAsync($"forumtopic?id={match.Groups[1].Value}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS OnEpisodeTapped failed: " + ex.GetType().Name);
        }
    }

    private async void OnRecommendationTapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (e.Parameter is not IDetailsPageArgs item || item.Id <= 0)
                return;
            var animeMode = item.Type != RelatedItemType.Manga;
            var args = new AnimeDetailsPageNavigationArgs(item.Id, item.Title, null, null, null)
            {
                AnimeMode = animeMode,
                Source = PageIndex.PageAnimeDetails,
                SourceTabIndex = Vm.DetailsPivotSelectedIndex
            };
            MauiDetailsNavigationHandoff.Set(args);
            var title = Uri.EscapeDataString(item.Title ?? string.Empty);
            var route = animeMode
                ? $"animedetails?id={item.Id}&title={title}"
                : $"animedetails?id={item.Id}&title={title}&manga=true";
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS OnRecommendationTapped failed: " + ex.GetType().Name);
        }
    }

    private void OnReviewMoreClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is not Button button || button.CommandParameter is not AnimeReviewData review || Vm == null)
                return;
            var reviews = Vm.Reviews;
            var boundary = reviews.IndexOf(review);
            if (boundary < 0)
                return;
            var expanded = !review.IsExpanded;
            for (var index = 0; index < reviews.Count; index++)
                reviews[index].IsExpanded = expanded && index >= boundary;
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS OnReviewMoreClicked failed: " + ex.GetType().Name);
        }
    }

    private async void OnStatusClicked(object sender, EventArgs e)
    {
        try
        {
            var options = new[]
            {
                AnimeStatus.Watching, AnimeStatus.Completed, AnimeStatus.OnHold,
                AnimeStatus.Dropped, AnimeStatus.PlanToWatch
            };
            var labels = options.Select(status => MALClient.XShared.Utils.Utilities.StatusToString((int)status, !Vm.AnimeMode, false)).ToArray();
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
            var number = choice.Split(' ')[0];
            if (int.TryParse(number, out var score))
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
            if (string.IsNullOrEmpty(Vm.TrailerUrl))
                return;
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
            if (e.Parameter is not string text || string.IsNullOrWhiteSpace(text))
                return;
            var query = Uri.EscapeDataString(text);
            ShowVideoOverlay($"https://www.youtube.com/results?search_query={query}");
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
            if (TryGetNavigationId(e.Parameter, out var id))
                await Shell.Current.GoToAsync($"character?id={id}");
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
            if (TryGetNavigationId(e.Parameter, out var id))
                await Shell.Current.GoToAsync($"staff?id={id}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS OnStaffTapped failed: " + ex.GetType().Name);
        }
    }

    private static bool TryGetNavigationId(object value, out int id)
    {
        if (value is int numericId)
        {
            id = numericId;
            return id > 0;
        }
        return int.TryParse(value as string, out id) && id > 0;
    }

    private WebView VideoWebView => _videoWebView ??= EnsureVideoWebView();

    private WebView EnsureVideoWebView()
    {
        if (_videoWebView != null)
            return _videoWebView;

        _videoWebView = new WebView();
        Grid.SetRow(_videoWebView, 1);
        VideoOverlay.Add(_videoWebView);
#if ANDROID
        _videoWebView.HandlerChanged += OnVideoWebViewHandlerChanged;
#endif
        return _videoWebView;
    }

    private void ShowVideoOverlay(string url)
    {
        try
        {
            if (VideoOverlay == null)
                return;
            var webView = EnsureVideoWebView();
            VideoOverlay.IsVisible = true;
            VideoWebViewHelper.Resume(webView.Handler?.PlatformView as global::Android.Views.View);
            var embed = BuildYouTubeEmbed(url);
            webView.Source = new HtmlWebViewSource
            {
                Html = VideoWebViewHelper.BuildEmbedHtml(embed),
                BaseUrl = "https://myanimelist.net"
            };
            SetSystemBars(true);
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
            if (_videoWebView != null)
                _videoWebView.Source = null;
            if (VideoOverlay != null)
                VideoOverlay.IsVisible = false;
            SetSystemBars(false);
        }
        catch { }
    }

    private static string BuildYouTubeEmbed(string url)
    {
        try
        {
            if (string.IsNullOrEmpty(url))
                return url;
            var match = System.Text.RegularExpressions.Regex.Match(url, @"youtube\.com/watch\?v=([\w\-]+)");
            if (match.Success)
                return $"https://www.youtube.com/embed/{match.Groups[1].Value}";
            match = System.Text.RegularExpressions.Regex.Match(url, @"youtu\.be/([\w\-]+)");
            if (match.Success)
                return $"https://www.youtube.com/embed/{match.Groups[1].Value}";
            match = System.Text.RegularExpressions.Regex.Match(url, @"/embed/([\w\-]+)");
            if (match.Success)
                return url;
            match = System.Text.RegularExpressions.Regex.Match(url, @"search_query=([^&]+)");
            if (match.Success)
                return $"https://www.youtube.com/embed/results?search_query={match.Groups[1].Value}";
            return url;
        }
        catch
        {
            return url;
        }
    }

    private void OnTabTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not string parameter || !int.TryParse(parameter, out var tabIndex))
            return;
        var direction = Vm != null && tabIndex >= Vm.DetailsPivotSelectedIndex ? 1 : -1;
        _ = SelectTabAsync(tabIndex, direction);
    }

    private async Task SelectTabAsync(int tabIndex, int direction)
    {
        if (Vm == null || tabIndex < 0 || tabIndex > 7 || !IsTabVisible(tabIndex))
            return;
        var changed = Vm.DetailsPivotSelectedIndex != tabIndex;
        if (changed)
            Vm.DetailsPivotSelectedIndex = tabIndex;
        ApplyTabActiveState();
        if (changed)
            await AnimateTabTransitionAsync(direction);
    }

    private async Task AnimateTabTransitionAsync(int direction)
    {
        if (TabContentHost == null)
            return;
        var offset = direction < 0 ? -28 : 28;
        TabContentHost.Opacity = 0.35;
        TabContentHost.TranslationX = offset;
        await Task.WhenAll(
            TabContentHost.FadeTo(1, TabTransitionLength),
            TabContentHost.TranslateTo(0, 0, TabTransitionLength, Easing.CubicOut));
    }

    private int? FindAdjacentVisibleTab(int current, int direction)
    {
        var next = direction < 0 ? current - 1 : current + 1;
        while (next >= 0 && next <= 7)
        {
            if (IsTabVisible(next))
                return next;
            next += direction < 0 ? -1 : 1;
        }
        return null;
    }

    private bool IsTabVisible(int index)
    {
        if (Vm == null)
            return true;
        if (!Vm.AnimeMode)
            return index is not (2 or 3 or 4 or 5 or 7);
        if (Vm.Type?.Equals("Movie", StringComparison.OrdinalIgnoreCase) == true)
            return index != 2;
        return true;
    }

    private void ApplyTabActiveState()
    {
        if (TabGeneral == null || TabDetails == null || TabEpisodes == null || TabReviews == null ||
            TabRecs == null || TabRelated == null || TabCharacters == null || TabStaff == null)
            return;
        var selected = Vm?.DetailsPivotSelectedIndex ?? 0;
        var labels = new[] { TabGeneral, TabDetails, TabEpisodes, TabReviews, TabRecs, TabRelated, TabCharacters, TabStaff };
        for (var index = 0; index < labels.Length; index++)
            labels[index].TextColor = index == selected ? Color.FromArgb("#0066FF") : Color.FromArgb("#B3FFFFFF");
    }

    private void OnPageViewportSizeChanged(object sender, EventArgs e)
    {
        if (PageScroll == null)
            return;
        var width = PageScroll.Width;
        var height = PageScroll.Height;
        if (Math.Abs(width - _lastViewportWidth) < 0.5 && Math.Abs(height - _lastViewportHeight) < 0.5)
            return;
        _lastViewportWidth = width;
        _lastViewportHeight = height;
        ApplyHeroState(PageScroll.ScrollY);
        Dispatcher.Dispatch(UpdateHeaderSpacer);
    }

    private void OnHeaderPanUpdated(object sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _headerPanActive = true;
                _headerPanLastY = e.TotalY;
                break;
            case GestureStatus.Running:
                if (!_headerPanActive || PageScroll == null)
                    return;
                var currentY = e.TotalY;
                var deltaY = currentY - _headerPanLastY;
                _headerPanLastY = currentY;
                if (Math.Abs(deltaY) < 0.5 || Math.Abs(e.TotalX) > Math.Abs(deltaY) * 1.25)
                    return;
                var maxScroll = Math.Max(0, PageScroll.ContentSize.Height - PageScroll.Height);
                var target = Math.Clamp(PageScroll.ScrollY - deltaY, 0, maxScroll);
                _ = PageScroll.ScrollToAsync(0, target, false);
                break;
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _headerPanActive = false;
                _headerPanLastY = 0;
                break;
        }
    }

    private void OnPageScrolled(object sender, ScrolledEventArgs e)
    {
        ApplyHeroState(e.ScrollY);
        if (IsNearPageBottom(e.ScrollY))
            AppendNextProgressiveChunk();
    }

    private void ResetHeaderLayoutState()
    {
        _headerSpacerRetry = 0;
        if (PageScroll != null)
            _ = PageScroll.ScrollToAsync(0, 0, false);
        if (TabStripScroll != null)
            _ = TabStripScroll.ScrollToAsync(0, 0, false);
        if (HeaderFlow != null)
            HeaderFlow.TranslationY = 0;
        if (TabContentHost != null)
            TabContentHost.TranslationX = 0;
        if (TabContentHost != null)
            TabContentHost.TranslationY = 0;
        ApplyHeroState(0);
        Dispatcher.Dispatch(UpdateHeaderSpacer);
    }

    private void UpdateHeaderSpacer()
    {
        if (HeaderSpacer == null)
            return;
        var bar = (ActionBar?.Height ?? 0) + (TabStripScroll?.Height ?? 0);
        if (bar <= 0)
        {
            if (_headerSpacerRetry++ < 5)
                Dispatcher.Dispatch(UpdateHeaderSpacer);
            return;
        }
        _headerSpacerRetry = 0;
        var height = HeroExpandedHeight + bar;
        if (Math.Abs(HeaderSpacer.HeightRequest - height) > 0.5)
            HeaderSpacer.HeightRequest = height;
    }

    private static double GetDisplayDensity()
    {
#if ANDROID
        return global::Android.Content.Res.Resources.System.DisplayMetrics.Density;
#else
        return 1;
#endif
    }

    private void ApplyHeroState(double scrollY)
    {
        if (HeroContainer == null || HeroVisual == null || PosterContainer == null || HeroScrim == null)
            return;
        var progress = GetCollapseProgress(scrollY);
        var targetHeight = HeroExpandedHeight - HeroCollapseRange * progress;
        if (Math.Abs(HeroContainer.HeightRequest - targetHeight) > 0.5)
        {
            HeroContainer.HeightRequest = targetHeight;
            HeroVisual.HeightRequest = targetHeight;
        }
        var targetScale = GetPosterTargetScale();
        var scale = 1 + (targetScale - 1) * progress;
        PosterContainer.ScaleX = scale;
        PosterContainer.ScaleY = scale;
        HeroScrim.Opacity = progress;
    }

    private static double GetCollapseProgress(double scrollY)
    {
        if (HeroCollapseRange <= 0)
            return 1;
        return Math.Clamp(Math.Max(0, scrollY) / HeroCollapseRange, 0, 1);
    }

    private double GetPosterTargetScale()
    {
        var availableWidth = PageScroll?.Width ?? 0;
        var posterWidth = PosterContainer?.Width ?? 0;
        if (availableWidth <= 0 || posterWidth <= 0)
            return 1;
        return Math.Max(1, availableWidth / posterWidth);
    }

    private bool IsNearPageBottom(double scrollY)
    {
        if (PageScroll == null)
            return false;
        var contentHeight = PageScroll.ContentSize.Height;
        if (contentHeight <= 0 || double.IsNaN(contentHeight) || double.IsInfinity(contentHeight))
            return false;
        var remaining = contentHeight - scrollY - Math.Max(0, PageScroll.Height);
        return remaining <= Math.Max(600, PageScroll.Height * 0.75);
    }

    private void AppendNextProgressiveChunk()
    {
        var tab = Vm?.DetailsPivotSelectedIndex ?? -1;
        switch (tab)
        {
            case 2:
                if (Vm.Episodes.Count > DisplayedEpisodes.Count)
                {
                    var target = Math.Min(Vm.Episodes.Count, DisplayedEpisodes.Count + ProgressiveChunkSize);
                    for (var index = DisplayedEpisodes.Count; index < target; index++)
                        DisplayedEpisodes.Add(Vm.Episodes[index]);
                }
                break;
            case 3:
                if (Vm.Reviews.Count > DisplayedReviews.Count)
                {
                    var target = Math.Min(Vm.Reviews.Count, DisplayedReviews.Count + ProgressiveChunkSize);
                    for (var index = DisplayedReviews.Count; index < target; index++)
                        DisplayedReviews.Add(Vm.Reviews[index]);
                }
                break;
            case 6:
                if (Vm.AnimeMode)
                    AppendCharacters();
                else
                    AppendMangaCharacters();
                break;
            case 7:
                AppendStaff();
                break;
        }
    }

    private void AppendCharacters()
    {
        var source = Vm?.AnimeStaffData?.AnimeCharacterPairs;
        if (source == null || source.Count <= DisplayedCharacters.Count)
            return;
        var target = Math.Min(source.Count, DisplayedCharacters.Count + ProgressiveChunkSize);
        for (var index = DisplayedCharacters.Count; index < target; index++)
            DisplayedCharacters.Add(new AnimeCharacterCard(source[index]));
    }

    private void AppendMangaCharacters()
    {
        var source = Vm?.MangaCharacterData;
        if (source == null || source.Count <= DisplayedMangaCharacters.Count)
            return;
        var target = Math.Min(source.Count, DisplayedMangaCharacters.Count + ProgressiveChunkSize);
        for (var index = DisplayedMangaCharacters.Count; index < target; index++)
            DisplayedMangaCharacters.Add(source[index]);
    }

    private void AppendStaff()
    {
        var source = Vm?.AnimeStaffData?.AnimeStaff;
        if (source == null || source.Count <= DisplayedStaff.Count)
            return;
        var target = Math.Min(source.Count, DisplayedStaff.Count + ProgressiveChunkSize);
        for (var index = DisplayedStaff.Count; index < target; index++)
            DisplayedStaff.Add(source[index]);
    }

    private void SetTabError(int tabIndex, Exception exception)
    {
        var message = exception == null ? string.Empty : EmptyStateText(string.Empty, "Unable to load this section.");
        _detailsError = tabIndex == 1 ? message : _detailsError;
        _episodesError = tabIndex == 2 ? message : _episodesError;
        _reviewsError = tabIndex == 3 ? message : _reviewsError;
        _recommendationsError = tabIndex == 4 ? message : _recommendationsError;
        _relatedError = tabIndex == 5 ? message : _relatedError;
        _charactersError = tabIndex == 6 ? message : _charactersError;
        _staffError = tabIndex == 7 ? message : _staffError;
        NotifyStateProperties();
    }

    private bool HasDetailsContent()
    {
        return Vm.DetailedDataVisibility ||
               Vm.LeftGenres.Count > 0 ||
               Vm.RightGenres.Count > 0 ||
               Vm.Information.Count > 0 ||
               Vm.Stats.Count > 0 ||
               Vm.OPs.Count > 0 ||
               Vm.EDs.Count > 0;
    }

    private static string EmptyStateText(string value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

    private void NotifyStateProperties()
    {
        OnPropertyChanged(nameof(GeneralErrorText));
        OnPropertyChanged(nameof(GeneralErrorVisible));
        OnPropertyChanged(nameof(GeneralLoadingVisible));
        OnPropertyChanged(nameof(GeneralContentVisible));
        OnPropertyChanged(nameof(GeneralSynopsisEmptyVisible));
        OnPropertyChanged(nameof(DetailsErrorText));
        OnPropertyChanged(nameof(DetailsErrorVisible));
        OnPropertyChanged(nameof(DetailsEmptyVisible));
        OnPropertyChanged(nameof(DetailsContentVisible));
        OnPropertyChanged(nameof(EpisodesErrorText));
        OnPropertyChanged(nameof(EpisodesErrorVisible));
        OnPropertyChanged(nameof(EpisodesEmptyVisible));
        OnPropertyChanged(nameof(EpisodesContentVisible));
        OnPropertyChanged(nameof(ReviewsErrorText));
        OnPropertyChanged(nameof(ReviewsErrorVisible));
        OnPropertyChanged(nameof(ReviewsEmptyVisible));
        OnPropertyChanged(nameof(ReviewsContentVisible));
        OnPropertyChanged(nameof(RecommendationsErrorText));
        OnPropertyChanged(nameof(RecommendationsErrorVisible));
        OnPropertyChanged(nameof(RecommendationsEmptyVisible));
        OnPropertyChanged(nameof(RecommendationsContentVisible));
        OnPropertyChanged(nameof(RelatedErrorText));
        OnPropertyChanged(nameof(RelatedErrorVisible));
        OnPropertyChanged(nameof(RelatedEmptyVisible));
        OnPropertyChanged(nameof(RelatedContentVisible));
        OnPropertyChanged(nameof(CharactersErrorText));
        OnPropertyChanged(nameof(CharactersErrorVisible));
        OnPropertyChanged(nameof(CharactersEmptyVisible));
        OnPropertyChanged(nameof(CharactersContentVisible));
        OnPropertyChanged(nameof(StaffErrorText));
        OnPropertyChanged(nameof(StaffErrorVisible));
        OnPropertyChanged(nameof(StaffEmptyVisible));
        OnPropertyChanged(nameof(StaffContentVisible));
    }

    public sealed class AnimeCharacterCard
    {
        public AnimeCharacterCard(AnimeDetailsPageViewModel.AnimeStaffDataViewModels.AnimeCharacterStaffModelViewModel pair)
        {
            AnimeCharacter = pair?.AnimeCharacter;
            AnimeStaffPerson = pair?.AnimeStaffPerson;
            VoiceActors = (pair?.VoiceActors ?? new List<FavouriteViewModel>())
                .Where(actor => actor?.Data != null)
                .OrderBy(actor => string.Equals(actor.Data.Notes, "Japanese", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(actor => actor.Data.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public FavouriteViewModel AnimeCharacter { get; }
        public FavouriteViewModel AnimeStaffPerson { get; }
        public List<FavouriteViewModel> VoiceActors { get; }
        public bool HasVoiceActors => VoiceActors.Count > 0;
        public bool UseLegacyStaffBinding => VoiceActors.Count == 0 && AnimeStaffPerson?.Data != null;
    }
}

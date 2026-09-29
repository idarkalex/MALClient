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
    //First paint shows five rows; the rest is already in the VM and gets appended as the user
    //scrolls. The number is deliberately small: a long-runner has well over a thousand episodes
    //and painting forty of them on the tap is what made the tab feel like it hung.
    private const int ProgressiveChunkSize = 5;
    private const int RecommendationChunkSize = 8;
    //Characters, staff and related were the three tabs that still froze on first paint. Their
    //CollectionViews sit inside the page ScrollView with a HeightRequest sized to the whole list,
    //so the viewport is as tall as the data (sixty character rows is 6480px) and MAUI realises
    //every row in one layout pass: sixty portraits plus sixty seiyuu avatars, all decoded on the
    //UI thread. Measured 3000ms frames at p95 on a cold open. Growing them from the page scroll is
    //not an option, the scroll threshold says nothing about where these lists are, and an earlier
    //attempt at it appended forty rows per frame and was reverted. So the height is bounded and
    //the rest is reached with the Show more button instead.
    private const int ImageTabChunkSize = 12;
    private double _lastProgressiveAppendScrollY = double.NaN;
    private int _charactersShown = ImageTabChunkSize;
    private int _mangaCharactersShown = ImageTabChunkSize;
    private int _staffShown = ImageTabChunkSize;
    private int _relatedShown = ImageTabChunkSize;
    private const double HeroExpandedHeight = 460;
    private const double HeroCollapsedHeight = 210;
    private const double HeroCollapseRange = HeroExpandedHeight - HeroCollapsedHeight;
    private const uint TabTransitionLength = 160;

    /// <summary>
    /// Height of one character/staff row: PanelStyle padding 10 top and bottom, the 88px
    /// image, and the 4px margin above and below the 100px row. Every row is built to this
    /// height so the virtualised lists get an exact HeightRequest instead of a guess.
    /// </summary>
    private const double CharacterRowHeight = 108;

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
    public ObservableCollection<DirectRecommendationData> DisplayedRecommendations { get; } = new();
    public ObservableCollection<AnimeCharacterCard> DisplayedCharacters { get; } = new();
    public ObservableCollection<MangaCharacterCard> DisplayedMangaCharacters { get; } = new();
    public ObservableCollection<StaffCard> DisplayedStaff { get; } = new();
    public ObservableCollection<RelatedAnimeData> DisplayedRelated { get; } = new();

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
    public bool EpisodesContentVisible => Vm != null && !EpisodesErrorVisible && Vm.Episodes.Count > 0;

    public string ReviewsErrorText => EmptyStateText(_reviewsError, "Unable to load reviews.");
    public bool ReviewsErrorVisible => !string.IsNullOrWhiteSpace(_reviewsError);
    public bool ReviewsEmptyVisible => Vm != null && !Vm.LoadingReviews && !ReviewsErrorVisible && Vm.Reviews.Count == 0;
    public bool ReviewsContentVisible => Vm != null && !ReviewsErrorVisible && Vm.Reviews.Count > 0;

    public string RecommendationsErrorText => EmptyStateText(_recommendationsError, "Unable to load recommendations.");
    public bool RecommendationsErrorVisible => !string.IsNullOrWhiteSpace(_recommendationsError);
    public bool RecommendationsEmptyVisible => Vm != null && !Vm.LoadingRecommendations && !RecommendationsErrorVisible && Vm.Recommendations.Count == 0;
    public bool RecommendationsContentVisible => Vm != null && !RecommendationsErrorVisible && Vm.Recommendations.Count > 0;

    public string RelatedErrorText => EmptyStateText(_relatedError, "Unable to load related titles.");
    public bool RelatedErrorVisible => !string.IsNullOrWhiteSpace(_relatedError);
    public bool RelatedEmptyVisible => Vm != null && !Vm.LoadingRelated && !RelatedErrorVisible && Vm.RelatedAnime.Count == 0;
    public bool RelatedContentVisible => Vm != null && !RelatedErrorVisible && Vm.RelatedAnime.Count > 0;

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

    /// <summary>
    /// A CollectionView living inside the page's own ScrollView has to be told how tall it
    /// is or it collapses. The rows are a fixed height by design, so the total is exact.
    /// </summary>
    public double CharactersListHeight => CharacterRowHeight * DisplayedCharacters.Count;

    public double MangaCharactersListHeight => CharacterRowHeight * DisplayedMangaCharacters.Count;

    public double StaffListHeight => CharacterRowHeight * DisplayedStaff.Count;

    private double _tabListHeight = 520;
    private bool _tabListHeightSet;

    /// <summary>
    /// Bounded viewport for the character and staff lists. Sizing these to their content
    /// (row height times count) is what stopped them virtualising: the viewport then IS the
    /// list, so MAUI realises every row at once. A shorter box than the content is the whole
    /// trick - the CollectionView recycles, only the visible rows exist, and the memory stays
    /// flat no matter how far the user scrolls. It also means these two lists no longer grow
    /// by chunks, so the scroll driven append in AppendNextProgressiveChunk is not used for
    /// them; the list scrolls itself.
    /// </summary>
    public double TabListHeight
    {
        get => _tabListHeight;
        private set
        {
            if (value <= 0 || Math.Abs(value - _tabListHeight) < 0.5)
                return;
            _tabListHeight = value;
            OnPropertyChanged(nameof(TabListHeight));
        }
    }

    private void UpdateTabListHeight()
    {
        if (PageScroll == null)
            return;
        var viewport = PageScroll.Height;
        if (viewport <= 0 || double.IsNaN(viewport))
            return;
        // Big enough to be worth scrolling, small enough to leave the page something to scroll
        // for the hero. The floor keeps a short list from becoming a two row window.
        TabListHeight = Math.Clamp(viewport * 0.72, 320, viewport - 120);
        _tabListHeightSet = true;
    }

    private AnimeDetailsPageViewModel Vm => BindingContext as AnimeDetailsPageViewModel;

    public AnimeDetailsPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.AnimeDetails;
        if (RelatedGrid?.Inner != null)
            RelatedGrid.Inner.SelectionChanged += OnRelatedSelectionChanged;
        ApplyTabActiveState();
        UpdateHeroMetaLayout();
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
            vm.RequestVideoPlayback += ShowVideoOverlay;
            RefreshDisplayedEpisodes(false);
            RefreshDisplayedReviews(false);
            RefreshDisplayedRecommendations(false);
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
        _subscribedVm.RequestVideoPlayback -= ShowVideoOverlay;
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
        if (e.PropertyName == nameof(AnimeDetailsPageViewModel.LastAired) ||
            e.PropertyName == nameof(AnimeDetailsPageViewModel.Status) ||
            e.PropertyName == nameof(AnimeDetailsPageViewModel.GlobalScoreBind) ||
            e.PropertyName == nameof(AnimeDetailsPageViewModel.MyStatusBind))
        {
            UpdateHeroMetaLayout();
        }
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
            // Characters and staff are bounded boxes now (TabListHeight), so the CollectionView
            // recycles and every entry can go in at once: the height no longer tracks the count,
            // so a full list costs the same as a chunk. Related still grows in chunks, its grid
            // sizes itself to its content and cannot recycle.
            var cap = MALClient.XShared.Comm.Anime.AnimeCharactersStaffQuery.MaxCharacterPairs;
            var staffCap = MALClient.XShared.Comm.Anime.AnimeCharactersStaffQuery.MaxStaff;
            var charTarget = Math.Min(cap, pairs?.Count ?? 0);
            var mangaTarget = Math.Min(cap, mangaCharacters?.Count ?? 0);
            var staffTarget = Math.Min(staffCap, staff?.Count ?? 0);
            var relatedTarget = Math.Min(Vm.RelatedAnime.Count, _relatedShown);
            DisplayedCharacters.Clear();
            DisplayedMangaCharacters.Clear();
            DisplayedStaff.Clear();
            DisplayedRelated.Clear();
            foreach (var pair in pairs?.Take(charTarget) ?? Enumerable.Empty<AnimeDetailsPageViewModel.AnimeStaffDataViewModels.AnimeCharacterStaffModelViewModel>())
                DisplayedCharacters.Add(new AnimeCharacterCard(pair));
            foreach (var character in mangaCharacters?.Take(mangaTarget) ?? Enumerable.Empty<FavouriteViewModel>())
                DisplayedMangaCharacters.Add(new MangaCharacterCard(character));
            foreach (var person in staff?.Take(staffTarget) ?? Enumerable.Empty<FavouriteViewModel>())
                DisplayedStaff.Add(new StaffCard(person));
            foreach (var item in Vm.RelatedAnime.Take(relatedTarget))
                DisplayedRelated.Add(item);
            NotifyStateProperties();
            if (!_tabListHeightSet)
                UpdateTabListHeight();
            OnPropertyChanged(nameof(TabListHeight));
            OnPropertyChanged(nameof(CharactersListHeight));
            OnPropertyChanged(nameof(MangaCharactersListHeight));
            OnPropertyChanged(nameof(StaffListHeight));
            Console.WriteLine($"MALPlusTabs rows pairs={pairs?.Count ?? 0}/{DisplayedCharacters.Count} manga={mangaCharacters?.Count ?? 0}/{DisplayedMangaCharacters.Count} staff={staff?.Count ?? 0}/{DisplayedStaff.Count} related={Vm.RelatedAnime.Count}/{DisplayedRelated.Count} tabListH={TabListHeight}");
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
        // A new entry starts at the first chunk again; carrying the grown count over would
        // hand the next anime a list that is already past its own first paint.
        _charactersShown = ImageTabChunkSize;
        _mangaCharactersShown = ImageTabChunkSize;
        _staffShown = ImageTabChunkSize;
        _relatedShown = ImageTabChunkSize;
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
        DisplayedRecommendations.Clear();
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

        // Breadcrumb: "warm" means the background chain had already filled this tab before the
        // user touched it, "cold" means the tap is what triggered the fetch. This is how we tell
        // a preload that works from one that silently never ran.
        global::Android.Util.Log.Info("MALPlusTabs",
            $"tap tab={tabIndex} warm={IsTabWarm(tabIndex)} entry={_initializedId}");
        LogMemory($"tab{tabIndex}-tap");

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
                        {
                            _recommendationsLoaded = Vm.RecommendationsLoaded;
                            RefreshDisplayedRecommendations(false);
                        }
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
    {
        RefreshDisplayedRecommendations(false);
        NotifyStateProperties();
    }

    private void OnRelatedSourceChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        // The grid binds to the paged DisplayedRelated, not straight to the source, so it has to
        // be filled here. The load path clears the VM collection and then adds the entries one by
        // one, so this runs once per item and appends only what the current chunk size allows.
        if (e.Action == NotifyCollectionChangedAction.Reset)
            DisplayedRelated.Clear();
        var source = Vm?.RelatedAnime;
        if (source != null)
        {
            var target = Math.Min(source.Count, _relatedShown);
            for (var index = DisplayedRelated.Count; index < target; index++)
                DisplayedRelated.Add(source[index]);
        }
        NotifyStateProperties();
    }

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

    private void RefreshDisplayedRecommendations(bool reset)
    {
        var source = Vm?.Recommendations;
        if (source == null)
            return;
        if (reset || source.Count < DisplayedRecommendations.Count)
            DisplayedRecommendations.Clear();
        var target = Math.Min(source.Count, Math.Max(RecommendationChunkSize, DisplayedRecommendations.Count));
        for (var index = DisplayedRecommendations.Count; index < target; index++)
            DisplayedRecommendations.Add(source[index]);
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

    /// <summary>
    ///     Splits the recommendation blurb so it really wraps around the poster: the first slice sits
    ///     beside the artwork, whatever is left flows underneath it at full card width. The only honest
    ///     budget comes from the measured title height, so this runs on SizeChanged and memoizes its
    ///     inputs on the item.
    /// </summary>
    private void OnRecommendationTitleSized(object sender, EventArgs e)
    {
        if (sender is not Label title || title.BindingContext is not DirectRecommendationData item)
            return;

        var width = title.Width;
        var titleHeight = title.Height;
        if (width <= 1 || titleHeight <= 0)
            return;

        var unchanged = Math.Abs(item.SplitWidth - width) < 1 &&
                        Math.Abs(item.SplitTitleHeight - titleHeight) < 1 &&
                        string.Equals(item.DescriptionSplitSource, item.Description, StringComparison.Ordinal);
        if (unchanged)
            return;

        item.SplitWidth = width;
        item.SplitTitleHeight = titleHeight;

        var full = item.Description;
        if (string.IsNullOrWhiteSpace(full))
        {
            item.SetDescriptionSplit(null, null);
            return;
        }

        const double lineHeight = 13 * 1.35;
        const double rowSpacing = 8;
        const double charWidth = 6.6;
        const double posterHeight = 195;

        var available = Math.Max(0, posterHeight - titleHeight - rowSpacing);
        var lines = (int)Math.Floor(available / lineHeight);
        // Keep one line of slack: measured line height drifts from the nominal 13 * 1.35 and an
        // over-eager budget is what makes the blurb run over the artwork.
        var budget = (int)Math.Floor(width / charWidth) * Math.Max(0, lines - 1);
        if (budget <= 8)
        {
            // Nothing fits beside the poster, so the blurb flows underneath it at full width.
            item.SetDescriptionSplit(null, full);
            return;
        }

        if (full.Length <= budget)
        {
            item.SetDescriptionSplit(full, null);
            return;
        }

        var cut = full.LastIndexOf(' ', Math.Min(full.Length, budget), Math.Min(full.Length, budget));
        if (cut <= 0)
            cut = Math.Min(full.Length, budget);
        item.SetDescriptionSplit(full.Substring(0, cut).TrimEnd(), full.Substring(cut).TrimStart());
    }

    private async void OnRecommendationTapped(object sender, TappedEventArgs e)
        => await NavigateToRelatedItem(e.Parameter as IDetailsPageArgs);

    private async void OnRelatedSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (RelatedGrid?.Inner == null)
                return;
            // Clear it straight away, otherwise the same card stays selected and tapping it again
            // raises no SelectionChanged.
            RelatedGrid.Inner.SelectedItem = null;
            if (e.CurrentSelection.Count == 0)
                return;
            await NavigateToRelatedItem(e.CurrentSelection[0] as IDetailsPageArgs);
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS OnRelatedSelectionChanged failed: " + ex.GetType().Name);
        }
    }

    private async Task NavigateToRelatedItem(IDetailsPageArgs item)
    {
        try
        {
            if (item == null || item.Id <= 0)
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
            // CommandParameter already carries the exact review instance bound to the row,
            // so it is the only one that may flip. Walking the collection by index used to
            // expand every review from this one downwards.
            if (sender is Button button && button.CommandParameter is AnimeReviewData review)
                review.IsExpanded = !review.IsExpanded;
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

    /// <summary>
    /// The old client let you type the watched count straight into the flyout; the model still
    /// carries WatchedEpsInput and ChangeWatchedEps for it, only the input surface was missing.
    /// </summary>
    private async void OnEpisodesCounterTapped(object sender, TappedEventArgs e)
    {
        try
        {
            var vm = Vm;
            if (vm == null)
                return;

            int.TryParse(vm.MyEpisodesBind?.Split('/')[0], out var current);
            int.TryParse(vm.MyEpisodesBind?.Split('/').LastOrDefault(), out var max);

            var prompt = new NumberPromptPopup
            {
                PromptTitle = vm.AnimeMode ? "Episodes watched" : "Chapters read",
                InitialValue = current.ToString(),
                Hint = max > 0 ? $"Max {max}" : string.Empty
            };

            await Navigation.PushModalAsync(prompt, animated: true);
            var entered = await prompt.WaitForResultAsync();

            if (entered is int value)
            {
                vm.WatchedEpsInput = value.ToString();
                vm.ChangeWatchedCommand.Execute(null);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPlus OnEpisodesCounterTapped failed: " + ex.GetType().Name);
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

    private async void OnOpEdTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not string text || string.IsNullOrWhiteSpace(text) || Vm == null)
            return;
        try
        {
            // The OP and ED lists share one handler, so recover which list the row came from.
            var isOp = Vm.OPs.Contains(text);
            var sequence = AnimeThemesHelper.ParseSequence(text);
            var song = AnimeThemesHelper.ExtractOpEdSong(text);
            var query = AnimeThemesHelper.BuildOpEdSearchQuery(text);
            var entryId = Vm.Id;
            var entryTitle = Vm.Title;
            var animeMode = Vm.AnimeMode;

            ShowVideoLoading();

            var url = await Task.Run(async () =>
            {
                // 1) AnimeThemes: direct WebM, exact match, plays in the overlay.
                var themes = Vm.OpEdThemes;
                if (themes == null || themes.Count == 0)
                {
                    ResourceLocator.EnglishTitlesProvider.TryGetEnglishTitleForSeries(
                        entryId, animeMode, out var english);
                    themes = await AnimeThemesHelper.SearchAsync(entryTitle, english);
                }
                var match = AnimeThemesHelper.FindMatch(themes, isOp, sequence, query, song);
                if (!string.IsNullOrEmpty(match?.Url))
                    return match.Url;

                // 2) YouTube search scrape.
                var videoId = await VideoWebViewHelper.SearchYouTubeVideoId(query);
                if (!string.IsNullOrEmpty(videoId))
                    return $"https://www.youtube.com/watch?v={videoId}";

                return null;
            });

            if (!string.IsNullOrEmpty(url))
            {
                ShowVideoOverlay(url);
                return;
            }

            // 3) Nothing resolved: hand the search to the YouTube app.
            HideVideoLoading();
            await global::Microsoft.Maui.ApplicationModel.Launcher.Default.OpenAsync(
                new Uri("https://www.youtube.com/results?search_query=" +
                        System.Net.WebUtility.UrlEncode(query)));
        }
        catch (Exception ex)
        {
            HideVideoLoading();
            global::Android.Util.Log.Warn("MALPlus Video", "OnOpEdTapped: " + ex.GetType().Name);
        }
    }

    private void OnCharacterItemSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count == 0)
            return;
        var id = ResolveCharacterId(e.CurrentSelection[0]);
        ClearSenderSelection(sender);
        if (id > 0)
            NavigateToCharacter(id);
    }

    private void OnStaffItemSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count == 0)
            return;
        var id = ResolveStaffId(e.CurrentSelection[0]);
        ClearSenderSelection(sender);
        if (id > 0)
            NavigateToStaff(id);
    }

    private async void OnJapaneseVoiceClicked(object sender, EventArgs e)
    {
        try
        {
            if (TryGetNavigationId((sender as ImageButton)?.CommandParameter, out var id))
                await Shell.Current.GoToAsync($"staff?id={id}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPlus OnJapaneseVoiceClicked failed: " + ex.Message);
        }
    }

    private static int ResolveCharacterId(object item) => item switch
    {
        AnimeCharacterCard card => ResolveStringId(card.AnimeCharacter?.Data?.Id),
        MangaCharacterCard mangaCard => ResolveStringId(mangaCard.Id),
        _ => 0
    };

    private static int ResolveStaffId(object item) => item switch
    {
        StaffCard card => ResolveStringId(card.Id),
        _ => 0
    };

    private static int ResolveStringId(string value) => int.TryParse(value, out var id) ? id : 0;

    private static void ClearSenderSelection(object sender)
    {
        if (sender is CollectionView collectionView)
            collectionView.SelectedItem = null;
    }

    private static async Task NavigateToCharacter(int id)
    {
        try
        {
            await Shell.Current.GoToAsync($"character?id={id}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPlus character nav failed: " + ex.Message);
        }
    }

    private static async Task NavigateToStaff(int id)
    {
        try
        {
            await Shell.Current.GoToAsync($"staff?id={id}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPlus staff nav failed: " + ex.Message);
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
            if (VideoOverlay == null || string.IsNullOrWhiteSpace(url))
                return;
                var webView = EnsureVideoWebView();
                VideoOverlay.IsVisible = true;
                VideoWebViewHelper.BackHandler = OnVideoBackPressed;
                VideoWebViewHelper.Resume(webView.Handler?.PlatformView as global::Android.Views.View);
                // Single source of truth: routes YouTube to the iframe and direct media
                // (AnimeThemes WebM) to its own <video> player, with the working BaseUrl.
                webView.Source = VideoWebViewHelper.BuildSource(url);
                VideoWebViewHelper.ApplyMediaSettings(webView);
                SetSystemBars(true);
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("MALPlus Video", "ShowVideoOverlay: " + ex.GetType().Name);
        }
    }

    private void ShowVideoLoading()
    {
        try
        {
            if (VideoOverlay == null)
                return;
            var webView = EnsureVideoWebView();
            VideoOverlay.IsVisible = true;
            VideoWebViewHelper.Resume(webView.Handler?.PlatformView as global::Android.Views.View);
                webView.Source = new HtmlWebViewSource
                {
                    Html = VideoWebViewHelper.BuildLoadingHtml(),
                    BaseUrl = "https://myanimelist.net"
                };
                VideoWebViewHelper.ApplyMediaSettings(webView);
                SetSystemBars(true);
        }
        catch { }
    }

    private void HideVideoLoading()
        {
            try
            {
                if (VideoOverlay != null && VideoOverlay.IsVisible)
                    VideoWebViewHelper.BackHandler = null;
                if (_videoWebView != null)
                    _videoWebView.Source = null;
                if (VideoOverlay != null)
                    VideoOverlay.IsVisible = false;
                SetSystemBars(false);
            }
            catch { }
        }

    private void CloseVideoOverlay(object sender, EventArgs e)
    {
        HideVideoLoading();
    }

    /// <summary>
    ///     Back gesture / back button while the player is up closes it and cancels navigation, so
    ///     the swipe drops you back onto the details page instead of leaving the entry. Neither a
    ///     SwipeGestureRecognizer (the WebView child eats the drag) nor Page.BackButtonPressed
    ///     (not in MAUI 7) works: this is routed through MainActivity.OnBackPressed.
    /// </summary>
    private bool OnVideoBackPressed()
    {
        if (VideoOverlay == null || !VideoOverlay.IsVisible)
            return false;
        HideVideoLoading();
        return true;
    }

    private void OnTabTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not string parameter || !int.TryParse(parameter, out var tabIndex))
            return;
        var direction = Vm != null && tabIndex >= Vm.DetailsPivotSelectedIndex ? 1 : -1;
        _ = SelectTabAsync(tabIndex, direction);
    }

    private bool IsTabWarm(int tabIndex)
    {
        if (Vm == null) return false;
        switch (tabIndex)
        {
            case 1: return Vm.DetailsLoaded || Vm.DetailsRowsLoaded;
            case 2: return Vm.EpisodesLoaded;
            case 3: return Vm.ReviewsLoaded;
            case 4: return Vm.RecommendationsLoaded;
            case 5: return Vm.RelatedLoaded;
            case 6:
            case 7: return Vm.CharactersLoaded;
            default: return true;
        }
    }

    /// <summary>
    ///     Memory breadcrumb. The details page used to sit at ~900MB RSS on a long-runner and we
    ///     need to tell apart the three things that can dominate it: decoded artwork (Android
    ///     graphics), the managed heap (which lives in NATIVE memory under Mono/AOT, so it shows
    ///     up as Native Heap and not as a managed counter) and one Java peer object per view.
    /// </summary>
    public static void LogMemory(string label)
    {
        try
        {
            long managed = GC.GetTotalMemory(false);
            long committed = GC.GetGCMemoryInfo().TotalCommittedBytes;
            global::Android.Util.Log.Info("MALPlusMem",
                $"{label} gcHeap={managed / 1048576}MB committed={committed / 1048576}MB");
        }
        catch (Exception e)
        {
            global::Android.Util.Log.Warn("MALPlusMem", "LogMemory failed: " + e.GetType().Name);
        }
    }

    /// <summary>
    ///     Remembers the tab the user picked by hand. Only the START of the background warm-up
    ///     chain moves: the sequence itself is always General, Details, Episodes, Reviews,
    ///     Recommendations, Related, Characters, Staff.
    /// </summary>
    private void RememberPreferredTab(int tabIndex)
    {
        try { Settings.DetailsLastTab = tabIndex; }
        catch { }
    }

    private async Task SelectTabAsync(int tabIndex, int direction)
    {
        if (Vm == null || tabIndex < 0 || tabIndex > 7 || !IsTabVisible(tabIndex))
            return;
        var changed = Vm.DetailsPivotSelectedIndex != tabIndex;
        if (changed)
        {
            Vm.DetailsPivotSelectedIndex = tabIndex;
            RememberPreferredTab(tabIndex);
        }
        ApplyTabActiveState();
        if (changed)
            await AnimateTabTransitionAsync(direction);
        if (tabIndex >= 2)
            await TopUpTabUntilScrollable(tabIndex);
    }

    /// <summary>
    ///     A list tab opens with one chunk, and on a tall screen that chunk is shorter than the
    ///     viewport. A ScrollView with nothing to scroll raises no Scrolled event, so the scroll
    ///     driven growth in OnPageScrolled never fires and the rest of the list is unreachable.
    ///     This tops the list up in chunks, with a beat between them so each chunk paints on its
    ///     own, until it is tall enough to scroll; from there normal scrolling takes over and
    ///     keeps appending. Applies to every growing tab, not just the poster grids: they all
    ///     hang off the same page scroll and all of them used to stall at the first chunk.
    /// </summary>
    private async Task TopUpTabUntilScrollable(int tabIndex)
    {
        // Related is the one tab that cannot be left to the scroll. Its grid is a ContentView
        // wrapping a CollectionView inside the page ScrollView, and the inner list wins the
        // nested gesture arbitration: the page moves about forty pixels and then the drag is
        // gone, so anything past that point would be unreachable. It is also the shortest list
        // (thirty entries), so it is simply filled completely. The list tabs below are the ones
        // that keep loading as the user scrolls.
        var fillCompletely = tabIndex == 5;
        for (var pass = 0; pass < 12; pass++)
        {
            if (Vm == null || Vm.DetailsPivotSelectedIndex != tabIndex)
                return;
            if (!fillCompletely && IsTabContentScrollable())
            {
                Console.WriteLine($"MALPlusTabs topup tab={tabIndex} pass={pass} scrollable viewport={PageScroll?.Height} content={PageScroll?.ContentSize.Height}");
                return;
            }
            _lastProgressiveAppendScrollY = double.NaN;
            var before = DisplayedCountFor(tabIndex);
            AppendNextProgressiveChunk();
            if (fillCompletely && DisplayedCountFor(tabIndex) == before)
                return;
            // The HeightRequest is a binding, so the list has to remeasure before it can be
            // judged against the viewport.
            await Task.Delay(90);
        }
    }

    private int DisplayedCountFor(int tabIndex)
    {
        if (Vm == null)
            return 0;
        return tabIndex switch
        {
            2 => DisplayedEpisodes.Count,
            3 => DisplayedReviews.Count,
            4 => DisplayedRecommendations.Count,
            5 => DisplayedRelated.Count,
            6 => Vm.AnimeMode ? DisplayedCharacters.Count : DisplayedMangaCharacters.Count,
            7 => DisplayedStaff.Count,
            _ => 0
        };
    }

    private bool IsTabContentScrollable()
    {
        if (PageScroll == null)
            return false;
        var viewport = PageScroll.Height;
        if (viewport <= 0 || double.IsNaN(viewport))
            return false;
        var content = PageScroll.ContentSize.Height;
        if (content <= 0 || double.IsNaN(content))
            return false;
        // Two screens, not one. A single screen of content is technically scrollable, but the
        // Related grid's inner CollectionView wins the nested gesture arbitration and the page
        // stops after a few pixels, so on that tab the list has to already be long enough to
        // browse without the scroll carrying the load.
        return content > viewport * 2;
    }

    private async Task AnimateTabTransitionAsync(int direction)
    {
        if (TabContentHost == null)
            return;
        //Dimming the whole host before switching (Opacity 0.35 + a 28px slide) was the visible
        //half of "tapping a tab takes ages": the fade only starts once the new tab has already
        //been laid out, so the screen sat dimmed and offset for the whole layout pass. A short
        //slide alone reads as responsive and does not hide the content while it measures.
        var offset = direction < 0 ? -20 : 20;
        TabContentHost.TranslationX = offset;
        TabContentHost.Opacity = 1;
        await Task.WhenAll(
            TabContentHost.TranslateTo(0, 0, TabTransitionLength, Easing.CubicOut),
            TabContentHost.FadeTo(1, TabTransitionLength / 2));
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

    /// <summary>
    /// LAST AIRED only exists while something is actually airing, so the row can end up with a
    /// single field. The fields are packed to the left with a fixed gap so SCORE and STATUS stay
    /// together, and when only one is left it is stretched so the row keeps its shape.
    /// </summary>
    private void UpdateHeroMetaLayout()
    {
        if (HeroMetaLayout == null || ScoreMetaGroup == null || StatusMetaGroup == null ||
            LastAiredMetaGroup == null)
            return;

        var vm = Vm;
        bool showLastAired = vm != null && AirTimeUtils.IsCurrentlyAiringStatus(vm.Status) &&
                             !string.IsNullOrWhiteSpace(vm.LastAired);
        LastAiredMetaGroup.IsVisible = showLastAired;

        var groups = new[] {ScoreMetaGroup, StatusMetaGroup, LastAiredMetaGroup};
        int visible = 0;
        View sole = null;
        foreach (var group in groups)
        {
            if (!group.IsVisible)
                continue;
            visible++;
            sole = group;
        }

        foreach (var group in groups)
            group.HorizontalOptions = visible == 1 && ReferenceEquals(group, sole)
                ? LayoutOptions.Fill
                : LayoutOptions.Start;
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
        UpdateTabListHeight();
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
        var near = IsNearPageBottom(e.ScrollY);
        if (!near)
        {
            // Away from the tail, so the next approach to the bottom may append again.
            _lastProgressiveAppendScrollY = double.NaN;
            return;
        }
        // IsNearPageBottom is deliberately generous (three quarters of the viewport), so a single
        // fling fires it on many consecutive frames. Without this guard each of those frames
        // appended a chunk, which is what made this tab unusable when it was tried before: forty
        // rows per frame. One append per 120px of real scrolling is the same endless list with
        // none of the stampede.
        if (!double.IsNaN(_lastProgressiveAppendScrollY) &&
            e.ScrollY - _lastProgressiveAppendScrollY < 120)
            return;
        _lastProgressiveAppendScrollY = e.ScrollY;
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
        // Only the active tab grows, so scrolling a long Episodes list never quietly appends
        // rows to a list nobody is looking at. OnPageScrolled keeps one append per 120px of
        // scroll, which is what makes this safe to run for the three grid tabs as well: their
        // rows are a portrait each, so an unbounded append is a stutter, not a few lines of text.
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
            case 4:
                if (Vm.Recommendations.Count > DisplayedRecommendations.Count)
                {
                    var target = Math.Min(Vm.Recommendations.Count, DisplayedRecommendations.Count + RecommendationChunkSize);
                    for (var index = DisplayedRecommendations.Count; index < target; index++)
                        DisplayedRecommendations.Add(Vm.Recommendations[index]);
                }
                break;
            case 5:
                GrowImageTab(Vm.RelatedAnime, DisplayedRelated, ref _relatedShown, MakeRelatedCard);
                break;
        }
    }

    private delegate TCard TCardFactory<TSource, TCard>(TSource source);

    private static void GrowImageTab<TSource, TCard>(IReadOnlyList<TSource> source, ObservableCollection<TCard> target,
        ref int shown, TCardFactory<TSource, TCard> factory)
    {
        if (source == null || target == null)
            return;
        // One chunk per pass, and never past what the query already trimmed.
        var cap = MALClient.XShared.Comm.Anime.AnimeCharactersStaffQuery.MaxCharacterPairs;
        var next = Math.Min(source.Count, Math.Min(cap, shown + ImageTabChunkSize));
        if (next <= target.Count)
            return;
        for (var index = target.Count; index < next; index++)
            target.Add(factory(source[index]));
        shown = next;
        Console.WriteLine($"MALPlusTabs grow rows={target.Count} of {source.Count} (shown={shown})");
    }

    private static AnimeCharacterCard MakeCharacterCard(
        AnimeDetailsPageViewModel.AnimeStaffDataViewModels.AnimeCharacterStaffModelViewModel pair)
        => new AnimeCharacterCard(pair);

    private static MangaCharacterCard MakeMangaCharacterCard(FavouriteViewModel character)
        => new MangaCharacterCard(character);

    private static StaffCard MakeStaffCard(FavouriteViewModel person)
        => new StaffCard(person);

    private static RelatedAnimeData MakeRelatedCard(RelatedAnimeData item)
        => item;

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
            var character = AnimeCharacter?.Data as MALClient.Models.Models.Favourites.AnimeCharacter;
            Role = character?.Role ?? string.Empty;
            Favorites = character?.Favorites ?? 0;
            // Only the Japanese seiyuu. Tenrai hands over up to 32 voice actors per character
            // across ten languages, and the card has room for exactly one row.
            JapaneseVoice =
                (pair?.VoiceActors ?? new List<FavouriteViewModel>())
                .FirstOrDefault(actor => actor?.Data is MALClient.Models.Models.Favourites.AnimeStaffPerson va &&
                                        string.Equals(va.Language, "Japanese", StringComparison.OrdinalIgnoreCase))
                ?? (pair?.VoiceActors ?? new List<FavouriteViewModel>()).FirstOrDefault(actor => actor?.Data != null);
            VoiceFavorites = (JapaneseVoice?.Data as MALClient.Models.Models.Favourites.AnimeStaffPerson)?.Favorites ?? 0;
        }

        public FavouriteViewModel AnimeCharacter { get; }
        public FavouriteViewModel AnimeStaffPerson { get; }

        /// <summary>The Japanese voice actor, or null when the character has none.</summary>
        public FavouriteViewModel JapaneseVoice { get; }

        public bool HasJapaneseVoice => JapaneseVoice?.Data != null && !string.IsNullOrEmpty(JapaneseVoice.Data.Name);

        public string Role { get; }

        public bool HasRole => !string.IsNullOrEmpty(Role);

        public int Favorites { get; }

        public string FavoritesText => Favorites > 0 ? ShortNumber(Favorites) : string.Empty;

        public bool HasFavorites => Favorites > 0;

        public int VoiceFavorites { get; }

        public string VoiceFavoritesText => VoiceFavorites > 0 ? ShortNumber(VoiceFavorites) : string.Empty;

        public bool HasVoiceFavorites => VoiceFavorites > 0;

        internal static string ShortNumber(int value) =>
            value >= 1000 ? (value / 1000d).ToString("0.#") + "K" : value.ToString();
    }

    /// <summary>
    /// Staff and manga rows bind straight to FavouriteViewModel.Data, which is typed as the
    /// FavouriteBase and therefore has no Role/Favorites/PrimaryPosition. These wrappers
    /// unwrap the concrete type so the templates can bind real fields.
    /// </summary>
    public sealed class StaffCard
    {
        public StaffCard(FavouriteViewModel person)
        {
            Person = person;
            var staff = person?.Data as MALClient.Models.Models.Favourites.AnimeStaffPerson;
            PrimaryPosition = staff?.PrimaryPosition ?? string.Empty;
            ExtraPositions = staff?.ExtraPositions ?? 0;
            // Up to two lines of roles, the rest folded into a count so a six role person
            // does not push the row past its height. "+2" on its own said nothing; this says
            // what the roles are.
            var roles = staff?.Positions;
            if (roles != null && roles.Count > 0)
            {
                RoleLineOne = roles[0];
                if (roles.Count > 1)
                    RoleLineTwo = roles.Count > 2
                        ? $"{roles[1]}  +{roles.Count - 2} more"
                        : roles[1];
            }
        }

        public FavouriteViewModel Person { get; }
        public string Name => Person?.Data?.Name ?? string.Empty;
        public string ImgUrl => Person?.Data?.ImgUrl ?? string.Empty;
        public string Id => Person?.Data?.Id ?? string.Empty;
        public string PrimaryPosition { get; }
        public bool HasPrimaryPosition => !string.IsNullOrEmpty(PrimaryPosition);
        public int ExtraPositions { get; }
        public bool HasExtraPositions => ExtraPositions > 0;
        public string RoleLineOne { get; }
        public string RoleLineTwo { get; }
        public bool HasRoleLineOne => !string.IsNullOrEmpty(RoleLineOne);
        public bool HasRoleLineTwo => !string.IsNullOrEmpty(RoleLineTwo);
    }

    public sealed class MangaCharacterCard
    {
        public MangaCharacterCard(FavouriteViewModel character)
        {
            Character = character;
            var data = character?.Data as MALClient.Models.Models.Favourites.AnimeCharacter;
            Role = data?.Role ?? string.Empty;
            Favorites = data?.Favorites ?? 0;
        }

        public FavouriteViewModel Character { get; }
        public string Name => Character?.Data?.Name ?? string.Empty;
        public string ImgUrl => Character?.Data?.ImgUrl ?? string.Empty;
        public string Id => Character?.Data?.Id ?? string.Empty;
        public string Role { get; }
        public bool HasRole => !string.IsNullOrEmpty(Role);
        public int Favorites { get; }
        public string FavoritesText => Favorites > 0 ? AnimeCharacterCard.ShortNumber(Favorites) : string.Empty;
        public bool HasFavorites => Favorites > 0;
    }
}

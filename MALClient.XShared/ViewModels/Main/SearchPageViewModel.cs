using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using MALClient.Models.Enums;
using MALClient.Models.Models.Anime;
using MALClient.XShared.Comm.Anime;
using MALClient.XShared.Comm.Manga;
using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;

namespace MALClient.XShared.ViewModels.Main
{
    //this thing begs for refactor

    public class SearchPageViewModel : ViewModelBase
    {
        private readonly HashSet<string> _filters = new HashSet<string>();
        private bool _animeSearch; // default to anime
        private string _currrentFilter;
        private bool _directQueryInputVisibility;
        private bool _isFirstVisitGridVisible = true;
        private bool _queryHandler;
        private int _queryGeneration;
        private string _lastLoadedQuery;
        public SearchPageNavigationArgs PrevArgs;
        public string PrevQuery;

        public void Init(SearchPageNavigationArgs args)
        {
            PrevArgs = args;
            if (args.ByGenre || args.ByStudio)
            {
                PrevQuery = null;
                EmptyNoticeVisibility = false;
                IsFirstVisitGridVisible = false;
                GenreSelectionGridVisibility = true;
                DirectQueryInputVisibility = false;


                if (args.ByGenre)
                    AvailableSelectionChoices = Enum.GetValues(typeof(AnimeGenreSearch)).Cast<Enum>()
                        .OrderBy(val => val.ToString()).ToList();
                else
                    AvailableSelectionChoices = Enum.GetValues(typeof(AnimeStudios)).Cast<Enum>()
                        .OrderBy(val => val.ToString()).ToList();

                return;
            }

            GenreSelectionGridVisibility = false;

            if (_animeSearch != args.Anime || args.ForceQuery)
                PrevQuery = null;
            if (!_queryHandler)
                ViewModelLocator.GeneralMain.OnSearchQuerySubmitted += SubmitQuery;
            _queryHandler = true;
            _currrentFilter = null;
            _animeSearch = args.Anime;
            EmptyNoticeVisibility = false;
            IsFirstVisitGridVisible = true;
            if (args.DisplayMode == SearchPageDisplayModes.Off)
            {
                ViewModelLocator.NavMgr.ResetOffBackNav();
                DirectQueryInputVisibility = true;
                if (_queryHandler)
                {
                    ViewModelLocator.GeneralMain.OnSearchQuerySubmitted -= SubmitQuery;
                    _queryHandler = false;
                }
            }
            else
            {
                DirectQueryInputVisibility = false;
            }

            if (!string.IsNullOrWhiteSpace(args.Query) &&
                (args.DisplayMode == SearchPageDisplayModes.Main || args.ForceQuery))
            {
                if (args.ForceQuery)
                {
                    PrevQuery = null;
                    if (_lastLoadedQuery == args.Query && (_allAnimeSearchItemViewModels.Count > 0 || _allMangaSearchItemViewModels.Count > 0))
                    {
                        // both tabs already loaded for this query -> instant tab switch, no refetch but refresh current tab
                        IsFirstVisitGridVisible = false;
                        EmptyNoticeVisibility = false;
                        ViewModelLocator.GeneralMain.CurrentSearchQuery = args.Query;
                        InternalQuery = args.Query;
                        PopulateItems();
                        return;
                    }
                }
                ViewModelLocator.GeneralMain.PopulateSearchFilters(_filters);
                SubmitQuery(args.Query);
                if (args.ForceQuery)
                {
                    ViewModelLocator.GeneralMain.CurrentSearchQuery = args.Query;
                    InternalQuery = args.Query;
                }
            }
            else
            {
                _filters.Clear();
                CurrentSearchItems.Clear();
                IsFirstVisitGridVisible = true;
                ResetQuery();
            }
        }

        public int LastSearchPageIndex { get; set; } = -1;

        public bool? CatalogueIsGenre { get; set; }
        public string ActiveCatalogueTitle { get; set; }
        public int CatalogueScrollPosition { get; set; }
        public int CatalogueScrollOffset { get; set; }
        public string GenreFilterQuery { get; set; }
        public string StudioFilterQuery { get; set; }
        public int GenreListScrollPosition { get; set; }
        public int StudioListScrollPosition { get; set; }

        public void ClearCatalogueSession()
        {
            CatalogueIsGenre = null;
            ActiveCatalogueTitle = null;
            CatalogueScrollPosition = 0;
            CatalogueScrollOffset = 0;
        }

        private SearchPageNavigationArgs _catalogueArgs;
        private int _cataloguePage;
        private int _catalogueToken;
        public bool HasMoreCatalogue { get; private set; } = true;
        public bool IsLoadingMoreCatalogue { get; private set; }

        private AnimeGenreStudioQuery CreateCatalogueQuery(SearchPageNavigationArgs args, int page)
            => args.Studio.HasValue
                ? new AnimeGenreStudioQuery(args.Studio.Value, page)
                : new AnimeGenreStudioQuery(args.Genre.Value, page);

        public async Task LoadCatalogue(SearchPageNavigationArgs args)
        {
            var token = ++_catalogueToken;
            try
            {
                EmptyNoticeVisibility = false;
                IsFirstVisitGridVisible = false;
                GenreSelectionGridVisibility = false;
                Loading = true;
                IsLoadingMoreCatalogue = false;
                _catalogueArgs = args;
                CatalogueIsGenre = args.Studio.HasValue ? (bool?)false : (bool?)true;
                ActiveCatalogueTitle = args.CatalogueTitle;
                CatalogueScrollPosition = 0;
                CatalogueScrollOffset = 0;

                const int initialPages = 2;
                var allItems = new List<AnimeGeneralDetailsData>();
                bool hasMore = false;
                int pagesLoaded = 0;
                for (int page = 1; page <= initialPages; page++)
                {
                    var query = CreateCatalogueQuery(args, page);
                    var pageItems = await query.GetAnime();
                    hasMore = query.HasNextPage;
                    allItems.AddRange(pageItems);
                    pagesLoaded++;
                    if (!hasMore)
                        break;
                }
                if (token != _catalogueToken)
                    return;

                _cataloguePage = pagesLoaded;
                HasMoreCatalogue = hasMore;
                ResourceLocator.DispatcherAdapter.Run(() =>
                {
                    CatalogueResults.Clear();
                    var seen = new HashSet<int>();
                    foreach (var item in allItems)
                        if (seen.Add(item.Id))
                            CatalogueResults.Add(new AnimeSearchItemViewModel(item, ViewModelLocator.AnimeList));
                    EmptyNoticeVisibility = CatalogueResults.Count == 0;
                    IsFirstVisitGridVisible = false;
                });
            }
            finally
            {
                if (token == _catalogueToken)
                    ResourceLocator.DispatcherAdapter.Run(() => Loading = false);
            }
        }

        public async Task LoadMoreCatalogue()
        {
            if (IsLoadingMoreCatalogue || !HasMoreCatalogue || _catalogueArgs == null || _cataloguePage == 0)
                return;
            IsLoadingMoreCatalogue = true;
            var token = _catalogueToken;
            try
            {
                var args = _catalogueArgs;
                var nextPage = _cataloguePage + 1;
                var query = CreateCatalogueQuery(args, nextPage);
                var items = await query.GetAnime();
                if (token != _catalogueToken)
                    return;
                _cataloguePage = nextPage;
                HasMoreCatalogue = query.HasNextPage;
                var newItems = items.Select(item => new AnimeSearchItemViewModel(item, ViewModelLocator.AnimeList)).ToList();
                ResourceLocator.DispatcherAdapter.Run(() =>
                {
                    var seen = new HashSet<int>(CatalogueResults.Select(result => result.Id));
                    foreach (var vm in newItems)
                        if (seen.Add(vm.Id))
                            CatalogueResults.Add(vm);
                });
            }
            catch (Exception ex)
            {
                DiagnosticsReporter.Error("SearchPageCatalogue", $"load-more failed page {_cataloguePage + 1}: {ex.Message}", ex);
            }
            finally
            {
                if (token == _catalogueToken)
                    ResourceLocator.DispatcherAdapter.Run(() => IsLoadingMoreCatalogue = false);
            }
        }

        public void OnNavigatedFrom()
        {
            ViewModelLocator.GeneralMain.OnSearchQuerySubmitted -= SubmitQuery;
            _queryHandler = false;
        }

        public async void SubmitQuery(string query)
        {
            var generation = ++_queryGeneration;

            if (string.IsNullOrEmpty(query) || query == PrevQuery || query.Length < 2)
            {
                IsFirstVisitGridVisible = false;
                EmptyNoticeVisibility = false;
                return;
            }

            IsFirstVisitGridVisible = false;
            PrevQuery = query;
            Loading = true;
            EmptyNoticeVisibility = false;
            CurrentSearchItems.Clear();
            _filters.Clear();
            _allAnimeSearchItemViewModels = new List<AnimeSearchItemViewModel>();
            _allMangaSearchItemViewModels = new List<AnimeSearchItemViewModel>();

            var cleanQuery = Utilities.CleanAnimeTitle(query);
            var animeTask = Task.Run(async () => await new AnimeSearchQuery(cleanQuery).GetSearchResults());
            var mangaTask = Task.Run(async () => await new MangaSearchQuery(cleanQuery).GetSearchResults());

            var animeData = new List<AnimeGeneralDetailsData>();
            var mangaData = new List<AnimeGeneralDetailsData>();
            try { animeData = await animeTask; } catch (Exception) { }
            try { mangaData = await mangaTask; } catch (Exception) { }

            if (generation != _queryGeneration)
            {
                Loading = false;
                return;
            }

            foreach (var item in animeData)
            {
                _allAnimeSearchItemViewModels.Add(new AnimeSearchItemViewModel(item, ViewModelLocator.AnimeList));
                if (!_filters.Contains(item.Type))
                    _filters.Add(item.Type);
            }
            foreach (var item in mangaData)
            {
                _allMangaSearchItemViewModels.Add(new AnimeSearchItemViewModel(item, ViewModelLocator.AnimeList, false));
                if (!_filters.Contains(item.Type))
                    _filters.Add(item.Type);
            }

            _lastLoadedQuery = query;

            ViewModelLocator.GeneralMain.PopulateSearchFilters(_filters);
            PopulateItems();
            Loading = false;
        }

        private ObservableCollection<AnimeSearchItemViewModel> CurrentSearchItems =>
            _animeSearch ? AnimeSearchItemViewModels : MangaSearchItemViewModels;

        private void PopulateItems()
        {
            AnimeSearchItemViewModels.Clear();
            MangaSearchItemViewModels.Clear();
            foreach (
                var item in
                _allAnimeSearchItemViewModels.Where(
                    item =>
                        string.IsNullOrWhiteSpace(_currrentFilter) ||
                        string.Equals(_currrentFilter, item.Type, StringComparison.CurrentCultureIgnoreCase)))
                AnimeSearchItemViewModels.Add(item);
            foreach (
                var item in
                _allMangaSearchItemViewModels.Where(
                    item =>
                        string.IsNullOrWhiteSpace(_currrentFilter) ||
                        string.Equals(_currrentFilter, item.Type, StringComparison.CurrentCultureIgnoreCase)))
                MangaSearchItemViewModels.Add(item);
            EmptyNoticeVisibility = CurrentSearchItems.Count == 0;
        }

        private void ResetQuery()
        {
            PrevQuery = null;
        }

        public void SubmitFilter(string filter)
        {
            _currrentFilter = filter == "None" ? "" : filter;
            PopulateItems();
        }

        #region Properties

        private List<AnimeSearchItemViewModel> _allAnimeSearchItemViewModels;
        private List<AnimeSearchItemViewModel> _allMangaSearchItemViewModels = new List<AnimeSearchItemViewModel>();

        public ObservableCollection<AnimeSearchItemViewModel> AnimeSearchItemViewModels { get; } =
            new ObservableCollection<AnimeSearchItemViewModel>();

        public ObservableCollection<AnimeSearchItemViewModel> MangaSearchItemViewModels { get; } =
            new ObservableCollection<AnimeSearchItemViewModel>();

        public ObservableCollection<AnimeSearchItemViewModel> CatalogueResults { get; } =
            new ObservableCollection<AnimeSearchItemViewModel>();

        public AnimeSearchItemViewModel CurrentlySelectedItem
        {
            get => null;
//One way to VM
            set => value?.NavigateDetails();
        }

        private bool _loading;

        public bool Loading
        {
            get => _loading;
            set
            {
                _loading = value;
                RaisePropertyChanged(() => Loading);
            }
        }

        private bool _emptyNoticeVisibility;
        private bool _genreSelectionGridVisibility;
        private List<Enum> _availableSelectionChoices;
        private string _internalQuery;

        public bool EmptyNoticeVisibility
        {
            get => _emptyNoticeVisibility;
            set
            {
                _emptyNoticeVisibility = value;
                RaisePropertyChanged(() => EmptyNoticeVisibility);
            }
        }

        public bool IsFirstVisitGridVisible
        {
            get => _isFirstVisitGridVisible;
            private set
            {
                _isFirstVisitGridVisible = value;
                RaisePropertyChanged(() => IsFirstVisitGridVisible);
            }
        }

        public bool DirectQueryInputVisibility
        {
            get => _directQueryInputVisibility;
            set
            {
                _directQueryInputVisibility = value;
                RaisePropertyChanged(() => DirectQueryInputVisibility);
            }
        }

        public bool GenreSelectionGridVisibility
        {
            get => _genreSelectionGridVisibility;
            set
            {
                _genreSelectionGridVisibility = value;
                RaisePropertyChanged(() => GenreSelectionGridVisibility);
            }
        }

        public List<Enum> AvailableSelectionChoices
        {
            get => _availableSelectionChoices;
            set
            {
                _availableSelectionChoices = value;
                RaisePropertyChanged(() => AvailableSelectionChoices);
            }
        }

        //used to update searchbox in desktop earch page in off pane --- one way
        public string InternalQuery
        {
            get => _internalQuery;
            set
            {
                _internalQuery = value;
                RaisePropertyChanged(() => InternalQuery);
            }
        }

        #endregion
    }
}
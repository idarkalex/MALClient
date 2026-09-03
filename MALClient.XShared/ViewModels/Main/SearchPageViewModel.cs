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
                        // both tabs already loaded for this query -> instant tab switch, no refetch
                        IsFirstVisitGridVisible = false;
                        EmptyNoticeVisibility = false;
                        ViewModelLocator.GeneralMain.CurrentSearchQuery = args.Query;
                        InternalQuery = args.Query;
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
                return;

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
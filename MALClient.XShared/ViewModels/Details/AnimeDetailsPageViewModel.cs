using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MALClient.Adapters;
using MALClient.Models.Enums;
using MALClient.Models.Interfaces;
using MALClient.Models.Models.Anime;
using MALClient.Models.Models.AnimeScrapped;
using MALClient.Models.Models.Favourites;
using MALClient.Models.Models.Library;
using MALClient.XShared.Comm;
using MALClient.XShared.Comm.Anime;
using MALClient.XShared.Comm.MagicalRawQueries;
using MALClient.XShared.Comm.Manga;
using MALClient.XShared.Delegates;
using MALClient.XShared.Interfaces;
using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;
using MALClient.XShared.Utils.Managers;
using VideoLibrary;

namespace MALClient.XShared.ViewModels.Details
{
    public partial class AnimeDetailsPageViewModel : ViewModelBase
    {
        private readonly IClipboardProvider _clipboardProvider;
        private readonly ISystemControlsLauncherService _systemControlsLauncherService;
        private readonly IAnimeLibraryDataStorage _animeLibraryDataStorage;

        private readonly IAiringNotificationsAdapter _airingNotificationsAdapter;

        //additional fields
        private int _allEpisodes;
        private int _allVolumes;
        private string _alternateImgUrl;
        private IAnimeData _animeItemReference; //our connection with everything
        public IAnimeData AnimeItemReference => _animeItemReference;

        public bool AnimeMode
        {
            get { return _animeMode; }
            set
            {
                _animeMode = value;
                RaisePropertyChanged(() => RewatchedLabel);
                RaisePropertyChanged(() => RewatchingLabel);
                RaisePropertyChanged(() => AnimeMode);
            }
        }

        private AnimeStaffDataViewModels _animeStaffData;
        private float _globalScore;

        private int _id;
        //crucial fields
        private string _imgUrl;
        public bool _initialized;


        //loaded fields
        private bool _loadedDetails;
        private bool _loadedEpisodes;
        private bool _loadedRecomm;
        private bool _loadedRelated;
        private bool _loadedReviews;
        private bool _loadedVideos;

        private bool _loadingAlternate;

        private List<FavouriteViewModel> _mangaCharacterData;

        public AnimeDetailsPageNavigationArgs PrevArgs { get; private set; }
        private List<string> _synonyms = new List<string>(); //used to increase ann's search reliability
        private bool _animeMode;
        private bool _loadedCharacters;
        private string _broadcast = "";

        public AnimeDetailsPageViewModel(IClipboardProvider clipboardProvider,
            ISystemControlsLauncherService systemControlsLauncherService, IAnimeLibraryDataStorage animeLibraryDataStorage, IAiringNotificationsAdapter airingNotificationsAdapter)
        {
            _clipboardProvider = clipboardProvider;
            _systemControlsLauncherService = systemControlsLauncherService;
            _animeLibraryDataStorage = animeLibraryDataStorage;
            _airingNotificationsAdapter = airingNotificationsAdapter;
            UpdateScoreFlyoutChoices();
        }

        public bool Initialized
        {
            get { return _initialized; }
            private set
            {
                _initialized = value;
                //OnInitialized?.Invoke(null, null);
            }
        }

        public string Title { get; set; }
        public string Type { get; private set; }
        public string Status { get; private set; }

        public string TimeTillNextAir
        {
            get
            {
                var now = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(_broadcast))
                {
                    var nextAir = ComputeNextAirDate(_broadcast, now);
                    if (nextAir.HasValue)
                        return FormatAirCountdown(nextAir.Value, now);
                }

                var nextFromEpisodes = ComputeNextAirFromEpisodes(Episodes, now);
                if (nextFromEpisodes.HasValue)
                    return FormatAirCountdown(nextFromEpisodes.Value, now);

                if (_animeItemReference is AnimeItemViewModel vm)
                {
                    if (ResourceLocator.AiringInfoProvider.TryGetNextAirDate(vm.ParentAbstraction.MalId, now, out DateTime airDate) &&
                        now < airDate)
                    {
                        return FormatAirCountdown(airDate, now);
                    }
                }
                return "";
            }
        }

        private static string FormatAirCountdown(DateTime airDate, DateTime now)
        {
            var diff = airDate - now;
            if (diff.TotalSeconds <= 0)
                return "";
            if (diff.TotalDays >= 1)
                return $"{(int)diff.TotalDays}D";
            if (diff.TotalHours >= 1)
                return $"{(int)diff.TotalHours}H";
            return $"{(int)diff.TotalMinutes}M";
        }

        public string LastAired { get; private set; } = "";

        private void UpdateLastAired()
        {
            var last = Episodes
                .Where(ep => ep.AiredDate.HasValue)
                .OrderBy(ep => ep.AiredDate.Value)
                .LastOrDefault();
            if (last == null)
            {
                LastAired = "";
            }
            else
            {
                var ep = last.EpisodeId > 0
                    ? last.EpisodeId
                    : Episodes.IndexOf(last) + 1;
                LastAired = $"EP {ep} - {last.AiredDate.Value.ToString("d MMM", CultureInfo.InvariantCulture)}";
            }
            RaisePropertyChanged(() => LastAired);
        }

        //Dates when show starts or ends airing
        public string StartDate { get; private set; }
        public string EndDate { get; private set; }

        public string GeneralRank { get; private set; }
        public string GeneralPopularity { get; private set; }
        public string GeneralStudios { get; private set; }
        public string GeneralFavorites { get; private set; }
        public string GeneralMembers { get; private set; }
        public string GeneralSeason { get; private set; }
        public string TrailerUrl { get; private set; }

        public string StartYear
        {
            get
            {
                if (StartDate == AnimeItemViewModel.InvalidStartEndDate || StartDate == "N/A" || string.IsNullOrEmpty(StartDate))
                    return null;
                return StartDate.Contains("-00-00")
                    ? StartDate.Substring(0, 4)
                    : StartDate.Substring(0, Math.Min(4, StartDate.Length));
            }
        }
        //Dates set by the user
        public string MyStartDate
            =>
            (_animeItemReference?.StartDate ?? AnimeItemViewModel.InvalidStartEndDate) == AnimeItemViewModel.InvalidStartEndDate
                ? "Not set"
                : _animeItemReference?.StartDate;

        public string MyEndDate
            => (_animeItemReference?.EndDate ?? AnimeItemViewModel.InvalidStartEndDate) == AnimeItemViewModel.InvalidStartEndDate ? "Not set" : _animeItemReference?.EndDate
            ;

        public AnimeStaffDataViewModels AnimeStaffData
        {
            get { return _animeStaffData; }
            set
            {
                _animeStaffData = value;
                RaisePropertyChanged(() => AnimeStaffData);
            }
        }

        /// <summary>
        /// A bit of magic... wrapping magic
        /// </summary>
        public class AnimeStaffDataViewModels
        {
            public List<AnimeCharacterStaffModelViewModel> AnimeCharacterPairs { get; set; }
            public List<FavouriteViewModel> AnimeStaff { get; set; }

            public class AnimeCharacterStaffModelViewModel
            {
                public FavouriteViewModel AnimeCharacter { get; set; }
                public FavouriteViewModel AnimeStaffPerson { get; set; }

                public AnimeCharacterStaffModelViewModel(AnimeCharacterStaffModel data)
                {
                    AnimeCharacter = new FavouriteViewModel(data.AnimeCharacter);
                    AnimeStaffPerson = new FavouriteViewModel(data.AnimeStaffPerson);
                }
            }

            public AnimeStaffDataViewModels(AnimeStaffData data)
            {
                AnimeCharacterPairs =
                    data.AnimeCharacterPairs.Select(pair => new AnimeCharacterStaffModelViewModel(pair)).ToList();
                AnimeStaff = data.AnimeStaff.Select(person => new FavouriteViewModel(person)).ToList();
            }

        }

        public List<FavouriteViewModel> MangaCharacterData
        {
            get { return _mangaCharacterData; }
            set
            {
                _mangaCharacterData = value;
                RaisePropertyChanged(() => MangaCharacterData);
            }
        }

        public ObservableCollection<AnimeReviewData> Reviews { get; } = new ObservableCollection<AnimeReviewData>();

        public SmartObservableCollection<DirectRecommendationData> Recommendations { get; } =
            new SmartObservableCollection<DirectRecommendationData>();

        public ObservableCollection<RelatedAnimeData> RelatedAnime { get; } =
            new ObservableCollection<RelatedAnimeData>();

        public List<Tuple<string, string>> LeftDetailsRow { get; set; } =
            new List<Tuple<string, string>>();

        public List<Tuple<string, string>> RightDetailsRow { get; set; } =
            new List<Tuple<string, string>>();

        public ObservableCollection<string> LeftGenres { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> RightGenres { get; } = new ObservableCollection<string>();

        public ObservableCollection<Tuple<string, string>> Information { get; } =
            new ObservableCollection<Tuple<string, string>>();

        public ObservableCollection<Tuple<string, string>> Stats { get; } =
            new ObservableCollection<Tuple<string, string>>();

        public ObservableCollection<string> OPs { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> EDs { get; } = new ObservableCollection<string>();
        public SmartObservableCollection<AnimeEpisode> Episodes { get; } = new SmartObservableCollection<AnimeEpisode>();

        public ObservableCollection<AnimeVideoData> AvailableVideos { get;  } = new ObservableCollection<AnimeVideoData>();

        public static List<string> ScoreFlyoutChoices { get; set; }

        public int Id
        {
            get { return _id; }
            set
            {
                _id = value;
                if (value <= 0)
                    PrevArgs = null;
            }
        }

        public int MalId { get; set; }

        public int AllEpisodes
        {
            get { return _animeItemReference?.AllEpisodes ?? _allEpisodes; }
            set { _allEpisodes = value; }
        }

        private int AllVolumes
        {
            get { return _animeItemReference?.AllVolumes ?? _allVolumes; }
            set { _allVolumes = value; }
        }

        public DirectRecommendationData CurrentRecommendationsSelectedItem { get; set; }

        public async void Init(AnimeDetailsPageNavigationArgs param,bool fakeDelay = true)
        {
            Initialized = false;
            LoadingGlobal = false;
            //wait for UI
            if(fakeDelay)
                await Task.Delay(5);
            ViewModelLocator.GeneralMain.IsCurrentStatusSelectable = true;

            _loadingAlternate = false;

            //details reset - only for a DIFFERENT entry; back-nav to the same entry keeps loaded data
            var sameEntry = _animeItemReference != null && param.AnimeItem != null &&
                            _animeItemReference.Id == param.AnimeItem.Id && AnimeMode == param.AnimeMode;
            if (!sameEntry)
            {
                _loadedDetails = _loadedEpisodes = _loadedReviews = _loadedRecomm = _loadedRelated = _loadedVideos = _loadedCharacters = false;
                LastAired = "";
                RaisePropertyChanged(() => LastAired);
            }

            //basic init assignment
            _animeItemReference = param.AnimeItem;
            RaisePropertyChanged(() => TimeTillNextAir);
            AnimeMode = param.AnimeMode;
            Id = param.Id;
            Title = param.Title;
            if (Settings.SelectedApiType == ApiType.Mal)
                MalId = Id;
            else
                MalId = -1; //we will find this thing later

            //is manga stuff visibile
            if (AnimeMode)
            {
                MyVolumesVisibility = false;
                HiddenPivotItemIndex = -1;
            }
            else
            {
                MyVolumesVisibility = true;
                HiddenPivotItemIndex = 1;
            }
            //Add/Rem
            IsRemoveAnimeButtonEnabled = false;
            IsAddAnimeButtonEnabled = false;
            //favs
            IsFavourite = FavouritesManager.IsFavourite(AnimeMode ? FavouriteType.Anime : FavouriteType.Manga,
                Id.ToString());
            //staff - only wipe for a DIFFERENT entry; back-nav to the same entry keeps
            //the already-loaded data so the Characters/Staff tabs don't go blank.
            if (!sameEntry)
            {
                CharactersGridVisibility = MangaCharacterGridVisibility = false;
                LoadCharactersButtonVisibility = true;
                AnimeStaffData = null;
                MangaCharacterData = null;
            }
            //so there will be no floting start/end dates
            MyDetailsVisibility = false;
            StartDateValid = false;
            EndDateValid = false;
            _alternateImgUrl = null;

            if (AnimeMode)
            {
                Status1Label = "Watching";
                Status5Label = "Plan to watch";
                WatchedEpsLabel = "EPISODES";
                UpdateEpsUpperLabel = "EPISODES";
                if (_animeItemReference is AnimeItemViewModel vm)
                {
                    if (!vm.Auth || !vm.Airing || vm.AllEpisodes <= 0)
                    {
                        AiringNotificationsButtonVisibility = AreAirNotificationsEnabled = false;
                    }
                    else
                    {
                        AiringNotificationsButtonVisibility = true;
                        AreAirNotificationsEnabled = _airingNotificationsAdapter.AreNotificationRegistered(Id.ToString());
                    }
                }
                else
                    AiringNotificationsButtonVisibility = AreAirNotificationsEnabled = false;
            }
            else
            {
                Status1Label = "Reading";
                Status5Label = "Plan to read";
                WatchedEpsLabel = "Read\nchapters";
                UpdateEpsUpperLabel = "Read\nchapters";
                LoadCharactersButtonVisibility = false;
                AiringNotificationsButtonVisibility = false;
            }

            if (_animeItemReference == null || _animeItemReference is AnimeSearchItemViewModel ||
                (_animeItemReference is AnimeItemViewModel && !(_animeItemReference as AnimeItemViewModel).Auth))
                //if we are from search or from unauthenticated item let's look for proper abstraction
            {
                var possibleRef =
                    await ViewModelLocator.AnimeList.TryRetrieveAuthenticatedAnimeItem(param.Id, AnimeMode);
                if (possibleRef == null) // else we don't have this item
                {
                    //we may only prepare for its creation
                    RefreshData();
                    AddAnimeVisibility = true;
                    MyDetailsVisibility = false;
                }
                else
                    _animeItemReference = possibleRef;
            } // else we already have it

            if ((_animeItemReference as AnimeItemViewModel)?.Auth ?? false)
            {
                //we have item on the list , so there's valid data here
                MyDetailsVisibility = true;
                AddAnimeVisibility = false;
                IsRemoveAnimeButtonEnabled = true;
                IsAddAnimeButtonEnabled = false;
                PopulateStartEndDates();
                //tags
                if (Settings.SelectedApiType == ApiType.Mal)
                {
                    var tags = string.IsNullOrEmpty(_animeItemReference.Notes)
                        ? new List<string>()
                        : _animeItemReference.Notes.Contains(",")
                            ? _animeItemReference.Notes.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                                .ToList()
                            : new List<string> {_animeItemReference.Notes};
                    var collection = new ObservableCollection<string>(tags);
                    MyTags = collection;
                }
            }
            else
            {
                IsRemoveAnimeButtonEnabled = false;
                IsAddAnimeButtonEnabled = true;
                MyTags = new ObservableCollection<string>();
            }

            switch (param.Source)
            {
                case PageIndex.PageSearch:
                case PageIndex.PageMangaSearch:
                    await FetchData(false, param.Source, !sameEntry);
                    if (PrevArgs != null)
                        ViewModelLocator.NavMgr.RegisterBackNav(PrevArgs);
                    ViewModelLocator.NavMgr.RegisterBackNav(param.Source, param.PrevPageSetup);
                    break;
                case PageIndex.PageAnimeList:
                case PageIndex.PageMangaList:
                case PageIndex.PageProfile:
                case PageIndex.PageHistory:
                case PageIndex.PageArticles:
                case PageIndex.PageForumIndex:
                case PageIndex.PageStaffDetails:
                case PageIndex.PageCharacterDetails:
                case PageIndex.PageCalendar:
                case PageIndex.PagePopularVideos:
                case PageIndex.PageListComparison:
                case PageIndex.PageClubDetails:
                case PageIndex.PageSearchEverywhere:
                case PageIndex.PageDiscover:
                    await FetchData(false, param.Source, !sameEntry);
                    if (PrevArgs != null)
                        ViewModelLocator.NavMgr.RegisterBackNav(PrevArgs);
                    if (ViewModelLocator.Mobile || (!ViewModelLocator.Mobile && param.Source != PageIndex.PageProfile && param.Source != PageIndex.PageClubDetails))
                        ViewModelLocator.NavMgr.RegisterBackNav(param.Source, param.PrevPageSetup);
                    break;
                case PageIndex.PageAnimeDetails:
                    await FetchData(false, param.Source, clearEnrichment: !sameEntry);
                    if (param.RegisterBackNav) //we are already going back
                    {
                        ViewModelLocator.NavMgr.RegisterBackNav(param.PrevPageSetup as AnimeDetailsPageNavigationArgs);
                    }
                    break;
                case PageIndex.PageRecomendations:
                    if (param.AnimeElement != null)
                    {
                        ExtractData(param.AnimeElement, !sameEntry);
                    }
                    else
                    {
                        await FetchData(false, param.Source, clearEnrichment: !sameEntry);
                    }

                    if (PrevArgs != null)
                        ViewModelLocator.NavMgr.RegisterBackNav(PrevArgs);
                    ViewModelLocator.NavMgr.RegisterBackNav(param.Source, param.PrevPageSetup);
                    break;
                case PageIndex.PageNotificationHub:
                case PageIndex.PageFeeds:
                    if (PrevArgs != null)
                        ViewModelLocator.NavMgr.RegisterBackNav(PrevArgs);
                    await FetchData(false, param.Source, !sameEntry);
                    break;
            }

            PrevArgs = param;
            PrevArgs.RegisterBackNav = false;
            PrevArgs.Source = PageIndex.PageAnimeDetails;
            Initialized = true;
            DetailsPivotSelectedIndex = param.SourceTabIndex;
        }

        private void OpenMalPage()
        {
            if (Settings.SelectedApiType == ApiType.Mal)
            {
                _systemControlsLauncherService.LaunchUri(
                    new Uri($"https://myanimelist.net/{(AnimeMode ? "anime" : "manga")}/{Id}"));
            }
            else
            {
                _systemControlsLauncherService.LaunchUri(
                    new Uri($"https://hummingbird.me/{(AnimeMode ? "anime" : "manga")}/{Id}"));
            }
        }

        private async void NavigateDetails(IDetailsPageArgs args)
        {
            if (Settings.SelectedApiType == ApiType.Hummingbird)
                //recoms and review have mal id so we have to walk around thid
            {

            }
            ViewModelLocator.GeneralMain
                .Navigate(PageIndex.PageAnimeDetails,
                    new AnimeDetailsPageNavigationArgs(args.Id, args.Title, null, null,
                            new AnimeDetailsPageNavigationArgs(Id, Title, null, _animeItemReference)
                            {
                                Source = PageIndex.PageAnimeDetails,
                                RegisterBackNav = false,
                                AnimeMode = AnimeMode,
                                SourceTabIndex = DetailsPivotSelectedIndex
                            })
                        {Source = PageIndex.PageAnimeDetails, AnimeMode = args.Type == RelatedItemType.Anime});
        }

        /// <summary>
        ///     Launches update of all UI bound variables.
        /// </summary>
        /// <param name="callerId">Anime item id that calls this thing.</param>
        public void UpdateAnimeReferenceUiBindings(int callerId)
        {
            if (callerId != Id)
                return;

            RaisePropertyChanged(() => StartDateTimeOffset);
            RaisePropertyChanged(() => EndDateTimeOffset);
            RaisePropertyChanged(() => MyEpisodesBind);
            RaisePropertyChanged(() => MyVolumesBind);
            RaisePropertyChanged(() => MyStatusBind);
            RaisePropertyChanged(() => MyScoreBind);
            RaisePropertyChanged(() => MyStartDate);
            RaisePropertyChanged(() => MyEndDate);
            RaisePropertyChanged(() => IncrementEpsCommand);
            RaisePropertyChanged(() => DecrementEpsCommand);
            RaisePropertyChanged(() => IsIncrementButtonEnabled);
            RaisePropertyChanged(() => IsDecrementButtonEnabled);
            RaisePropertyChanged(() => IsRewatching);
            RaisePropertyChanged(() => IsRewatchingButtonVisibility);
        }


        public void UpdateScoreFlyoutChoices()
        {
            ScoreFlyoutChoices = Settings.SelectedApiType == ApiType.Mal
                ? new List<string>
                {
                    "10 - Masterpiece",
                    "9 - Great",
                    "8 - Very Good",
                    "7 - Good",
                    "6 - Fine",
                    "5 - Average",
                    "4 - Bad",
                    "3 - Very Bad",
                    "2 - Horrible",
                    "1 - Appalling"
                }
                : new List<string>
                {
                    "5 - Masterpiece",
                    "4.5 - Great",
                    "4 - Very Good",
                    "3.5 - Good",
                    "3 - Fine",
                    "2.5 - Average",
                    "2 - Bad",
                    "1.5 - Very Bad",
                    "1 - Horrible",
                    "0.5 - Appalling"
                };
        }



        #region ChangeStuff

        #region IncrementDecrementRelay

        public bool IsIncrementButtonEnabled
            => (_animeItemReference as AnimeItemViewModel)?.IncrementEpsVisibility == true;

        public bool IsDecrementButtonEnabled
            => (_animeItemReference as AnimeItemViewModel)?.DecrementEpsVisibility == true;

        public ICommand IncrementEpsCommand => new RelayCommand(() =>
        {
            (_animeItemReference as AnimeItemViewModel)?.IncrementWatchedCommand.Execute(null);
            RaisePropertyChanged(() => IsIncrementButtonEnabled);
            RaisePropertyChanged(() => IsDecrementButtonEnabled);
        });

        public ICommand DecrementEpsCommand => new RelayCommand(() =>
        {
            (_animeItemReference as AnimeItemViewModel)?.DecrementWatchedCommand.Execute(null);
            RaisePropertyChanged(() => IsIncrementButtonEnabled);
            RaisePropertyChanged(() => IsDecrementButtonEnabled);
        });

        #endregion

        private Query GetAppropriateUpdateQuery(int? rewatchCount = null)
        {
            try
            {
                if (AnimeItemReference is AnimeItemViewModel vm)
                    vm.ParentAbstraction.LastWatched = DateTime.Now;

                if (rewatchCount == null)
                {
                    if (AnimeMode)
                        return new AnimeUpdateQuery(_animeItemReference);
                    return new MangaUpdateQuery(_animeItemReference);
                }
                else
                {
                    if (AnimeMode)
                        return new AnimeUpdateQuery(_animeItemReference, rewatchCount.Value);
                    return new MangaUpdateQuery(_animeItemReference, rewatchCount.Value);
                }
            }
            catch (Exception e)
            {
                ResourceLocator.DispatcherAdapter.Run(() => ResourceLocator.SnackbarProvider.ShowText("Failed to update the entry."));
                return null;
            }
        }

        private async void LaunchUpdate()
        {
            LoadingUpdate = true;
            await GetAppropriateUpdateQuery().GetRequestResponse();
            LoadingUpdate = false;
        }

        public async void ChangeStatus(AnimeStatus status)
        {
            LoadingUpdate = true;
            var prevStatus = MyStatus;
            MyStatus = status;

            if (Settings.SetStartDateOnWatching && MyStatus == AnimeStatus.Watching &&
                (Settings.OverrideValidStartEndDate || !StartDateValid))
            {
                _startDateTimeOffset = DateTimeOffset.Now;
                _animeItemReference.StartDate = DateTimeOffset.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                StartDateValid = true;
                RaisePropertyChanged(() => StartDateTimeOffset);
                RaisePropertyChanged(() => MyStartDate);
            }
            else if (Settings.SetEndDateOnDropped && MyStatus == AnimeStatus.Dropped &&
                     (Settings.OverrideValidStartEndDate || !EndDateValid))
            {
                _endDateTimeOffset = DateTimeOffset.Now;
                _animeItemReference.EndDate = DateTimeOffset.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                EndDateValid = true;
                RaisePropertyChanged(() => EndDateTimeOffset);
                RaisePropertyChanged(() => MyEndDate);
            }
            else if (Settings.SetEndDateOnCompleted && MyStatus == AnimeStatus.Completed &&
                     (Settings.OverrideValidStartEndDate || !EndDateValid))
            {
                if (prevStatus == AnimeStatus.PlanToWatch) //we have just insta completed the series
                {
                    _startDateTimeOffset = DateTimeOffset.Now;
                    _animeItemReference.StartDate =
                        DateTimeOffset.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    StartDateValid = true;
                    RaisePropertyChanged(() => StartDateTimeOffset);
                    RaisePropertyChanged(() => MyStartDate);
                }
                _endDateTimeOffset = DateTimeOffset.Now;
                _animeItemReference.EndDate = DateTimeOffset.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                EndDateValid = true;
                RaisePropertyChanged(() => EndDateTimeOffset);
                RaisePropertyChanged(() => MyEndDate);
            }

            //in case of series having one episode
            if (AllEpisodes == 1 && prevStatus == AnimeStatus.PlanToWatch && MyStatus == AnimeStatus.Completed)
                if (Settings.SetStartDateOnWatching &&
                    (Settings.OverrideValidStartEndDate || _animeItemReference.StartDate == AnimeItemViewModel.InvalidStartEndDate))
                {
                    _startDateTimeOffset = DateTimeOffset.Now;
                    _animeItemReference.StartDate =
                        DateTimeOffset.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    StartDateValid = true;
                    RaisePropertyChanged(() => StartDateTimeOffset);
                    RaisePropertyChanged(() => MyStartDate);
                }

            if (_animeItemReference.IsRewatching)
            {
                if (_animeItemReference.AllEpisodes != 0)
                    MyEpisodes = _animeItemReference.AllEpisodes;
                _animeItemReference.IsRewatching = false;
                RaisePropertyChanged(() => IsRewatching);
                RaisePropertyChanged(() => MyStatusBind);
            }

            if (_animeItemReference.IsRewatching)
            {
                if (_animeItemReference.MyStatus == AnimeStatus.Completed && _animeItemReference.AllEpisodes != 0)
                    _animeItemReference.MyEpisodes = _animeItemReference.AllEpisodes;
            }

            var response = await GetAppropriateUpdateQuery().GetRequestResponse();
            if (response != "Updated" && Settings.SelectedApiType == ApiType.Mal)
                MyStatus = prevStatus;
            else
            {
                ResourceLocator.ShareManager.EnqueueEvent(ShareEvent.AnimeStatusChanged, new AnimeShareDiff
                {
                    Title = Title,
                    NewStatus = MyStatus,
                    Id = Id,
                    IsAnime = AnimeMode
                });
            }
            
            if (_animeItemReference is AnimeItemViewModel vm)
            {
                if (MyStatus == AnimeStatus.Completed && MyEpisodes != AllEpisodes && AllEpisodes != 0)
                {
                    vm.PromptForWatchedEpsChange(AnimeMode ? AllEpisodes : (Settings.MangaFocusVolumes ? AllVolumes : AllEpisodes));
                    RaisePropertyChanged(() => MyEpisodesBind);
                }

                if (MyStatus == AnimeStatus.Completed && Math.Abs(MyScore) < .0001 && Settings.DisplayScoreDialogAfterCompletion)
                {
                    vm.PromptForScoreChange();
                }
            }
            LoadingUpdate = false;
        }

        private async void ChangeScore(float score)
        {
            LoadingUpdate = true;
            var prevScore = MyScore;
            if (Settings.SelectedApiType == ApiType.Hummingbird)
            {
                MyScore = score/2;
                if (MyScore == prevScore)
                    MyScore = 0;
            }
            else
            {
                MyScore = score;
            }

            var response = await GetAppropriateUpdateQuery().GetRequestResponse();
            if (response != "Updated" && Settings.SelectedApiType == ApiType.Mal)
                MyScore = prevScore;
            else
            {
                ResourceLocator.ShareManager.EnqueueEvent(ShareEvent.AnimeScoreChanged, new AnimeShareDiff
                {
                    Title = Title,
                    NewScore = (int) MyScore,
                    Id = Id,
                    IsAnime = AnimeMode
                });
            }
            LoadingUpdate = false;
        }

        private async void ChangeNotes()
        {
            LoadingUpdate = true;
            await GetAppropriateUpdateQuery().GetRequestResponse();
            LoadingUpdate = false;
        }

        private async void ChangeRewatching(bool state)
        {
            LoadingUpdate = true;
            IsRewatchingButtonEnabled = false;

            if (state)
            {
                _animeItemReference.MyEpisodes = 0;
            }
            else if (_animeItemReference.AllEpisodes != 0)
            {
                _animeItemReference.MyEpisodes = _animeItemReference.AllEpisodes;
            }
            await GetAppropriateUpdateQuery().GetRequestResponse();
            (_animeItemReference as AnimeItemViewModel)?.AdjustIncrementButtonsVisibility();
            RaisePropertyChanged(() => IsIncrementButtonEnabled);
            RaisePropertyChanged(() => IsDecrementButtonEnabled);

            if (IsRewatching)
            {
                ResourceLocator.ShareManager.EnqueueEvent(ShareEvent.StartedRewatching, new AnimeShareDiff
                {
                    Id = Id,
                    IsAnime = AnimeMode,
                    Title = Title
                });
            }

            IsRewatchingButtonEnabled = true;
            LoadingUpdate = false;
        }

        private async void ChangeRewatchingCount(int count)
        {
            var query = GetAppropriateUpdateQuery(count);

            if(query == null)
                return;
            LoadingUpdate = true;
            var response = await query.GetRequestResponse();
            LoadingUpdate = false;

            if (response == "Updated")
            {
                ResourceLocator.ShareManager.EnqueueEvent(ShareEvent.ChangedRewatchingCount, new AnimeShareDiff
                {
                    Id = Id,
                    IsAnime = AnimeMode,
                    Title = Title,
                    RewatchCount = count,
                });
            }
        }

        private async void ChangeWatchedEps() //change from input
        {
            LoadingUpdate = true;
            int eps;
            if (!int.TryParse(WatchedEpsInput, out eps))
            {
                WatchedEpsInputNoticeVisibility = true;
                LoadingUpdate = false;
                return;
            }
            if (eps >= 0 && (AllEpisodes == 0 || eps <= AllEpisodes))
            {
                WatchedEpsInputNoticeVisibility = false;
                var prevEps = MyEpisodes;
                MyEpisodes = eps;
                var response = await GetAppropriateUpdateQuery().GetRequestResponse();
                if (response != "Updated" && Settings.SelectedApiType == ApiType.Mal)
                    MyEpisodes = prevEps;
                else
                {
                    ResourceLocator.ShareManager.EnqueueEvent(ShareEvent.AnimeEpisodesChanged, new AnimeShareDiff
                    {
                        Title = Title,
                        NewEpisodes = MyEpisodes,
                        TotalEpisodes = AllEpisodes,
                        Id = Id,
                        IsAnime = AnimeMode
                    });
                }

                if (_animeItemReference is AnimeItemViewModel reference)
                {
                    if (prevEps == 0 && AllEpisodes > 1 && MyEpisodes != AllEpisodes &&
                        (MyStatus == AnimeStatus.PlanToWatch || MyStatus == AnimeStatus.Dropped ||
                         (!Settings.DontAskToMoveOnHoldEntries && MyStatus == AnimeStatus.OnHold)))
                    {
                        reference.PromptForStatusChange(AnimeStatus.Watching);
                        RaisePropertyChanged(() => MyStatusBind);
                    }
                    else if (MyEpisodes == AllEpisodes && AllEpisodes != 0)
                    {

                        reference.PromptForStatusChange(AnimeStatus.Completed);
                        RaisePropertyChanged(() => MyStatusBind);
                    }
                    if (Settings.SelectedApiType == ApiType.Hummingbird)
                        reference.ParentAbstraction.LastWatched = DateTime.Now;
                }
                WatchedEpsInput = "";
            }
            else
            {
                WatchedEpsInputNoticeVisibility = true;
            }

            RaisePropertyChanged(() => IsIncrementButtonEnabled);
            RaisePropertyChanged(() => IsDecrementButtonEnabled);

            LoadingUpdate = false;
        }

        private async void ChangeReadVolumes()
        {
            LoadingUpdate = true;
            int vol;
            if (!int.TryParse(ReadVolumesInput, out vol))
            {
                WatchedEpsInputNoticeVisibility = true;
                LoadingUpdate = false;
                return;
            }
            if (vol >= 0 && (AllVolumes == 0 || vol <= AllVolumes))
            {
                WatchedEpsInputNoticeVisibility = false;
                var prevVol = MyVolumes;
                MyVolumes = vol;
                var response = await GetAppropriateUpdateQuery().GetRequestResponse();
                if (response != "Updated" && Settings.SelectedApiType == ApiType.Mal)
                    MyVolumes = prevVol;
                else
                {
                    ResourceLocator.ShareManager.EnqueueEvent(ShareEvent.AnimeEpisodesChanged, new AnimeShareDiff
                    {
                        Title = Title,
                        NewEpisodes = MyVolumes,
                        TotalEpisodes = AllVolumes,
                        Id = Id,
                        IsAnime = false,
                        IsVolumes = true
                    });
                }

                WatchedEpsInput = "";
            }
            else
            {
                WatchedEpsInputNoticeVisibility = true;
            }
            LoadingUpdate = false;
        }

        #endregion

        #region Add/Remove

        private async void AddAnime()
        {
            LoadingUpdate = true;
            IsAddAnimeButtonEnabled = false;
            var response = AnimeMode
                ? await new AnimeAddQuery(Id.ToString()).GetRequestResponse()
                : await new MangaAddQuery(Id.ToString()).GetRequestResponse();
            LoadingUpdate = false;
            IsAddAnimeButtonEnabled = true;
            if (Settings.SelectedApiType == ApiType.Mal && !response.Contains("Created") && AnimeMode)
                return;
            AddAnimeVisibility = false;
            AnimeType typeA;
            MangaType typeM;
            var type = 0;
            try
            {
                if (AnimeMode)
                {
                    Enum.TryParse(Type, out typeA);
                    type = (int) typeA;
                }
                else
                {
                    Enum.TryParse(Type.Replace("-", ""), out typeM);
                    type = (int) typeM;
                }
            }
            catch (Exception)
            {
                //who knows what MAL has thrown at us...
            }


            var startDate = AnimeItemViewModel.InvalidStartEndDate;
            if (Settings.SetStartDateOnListAdd)
            {
                startDate = DateTimeOffset.Now.ToString("yyyy-MM-dd");
                _startDateTimeOffset = DateTimeOffset.Now; //update without mal-update
                RaisePropertyChanged(() => StartDateTimeOffset);
            }
            var animeItem = AnimeMode
                ? new AnimeItemAbstraction(true, new AnimeLibraryItemData
                {
                    Title = Title,
                    ImgUrl = _imgUrl,
                    Type = type,
                    Id = Id,
                    AllEpisodes = AllEpisodes,
                    MalId = MalId,
                    MyStatus = Settings.DefaultStatusAfterAdding,
                    MyEpisodes = 0,
                    MyScore = 0,
                    MyStartDate = startDate,
                    MyEndDate = AnimeItemViewModel.InvalidStartEndDate
                })
                : new AnimeItemAbstraction(true, new MangaLibraryItemData
                {
                    Title = Title,
                    ImgUrl = _imgUrl,
                    Type = type,
                    Id = Id,
                    AllEpisodes = AllEpisodes,
                    MalId = MalId,
                    MyStatus = Settings.DefaultStatusAfterAdding,
                    MyEpisodes = 0,
                    MyScore = 0,
                    MyStartDate = startDate,
                    MyEndDate = AnimeItemViewModel.InvalidStartEndDate,
                    AllVolumes = AllVolumes,
                    MyVolumes = MyVolumes
                });
            _animeItemReference = animeItem.ViewModel;

            MyScore = 0;
            MyStatus = Settings.DefaultStatusAfterAdding;
            MyEpisodes = 0;
            RaisePropertyChanged(() => GlobalScore); //trigger setter of anime item
            if (string.Equals(Status, "Currently Airing", StringComparison.CurrentCultureIgnoreCase))
                (_animeItemReference as AnimeItemViewModel).Airing = true;
            ResourceLocator.AnimeLibraryDataStorage.AddAnimeEntry(animeItem);
            MyDetailsVisibility = true;
            PopulateStartEndDates();
            RaisePropertyChanged(() => StartDateTimeOffset);
            RaisePropertyChanged(() => EndDateTimeOffset);
            RaisePropertyChanged(() => IsIncrementButtonEnabled);
            RaisePropertyChanged(() => IncrementEpsCommand);
            RaisePropertyChanged(() => DecrementEpsCommand);

            if(AnimeMode)
                await new AnimeUpdateQuery(_animeItemReference).GetRequestResponse();
            else
                await new MangaUpdateQuery(_animeItemReference).GetRequestResponse();
        }

        public void CurrentAnimeHasBeenAddedToList(IAnimeData reference)
        {
            _animeItemReference = reference;
            MyDetailsVisibility = true;
            AddAnimeVisibility = false;
            RaisePropertyChanged(() => IsIncrementButtonEnabled);
            RaisePropertyChanged(() => IncrementEpsCommand);
            RaisePropertyChanged(() => DecrementEpsCommand);
        }

        private void RemoveAnime()
        {
            if (_animeItemReference == null)
                return;
            var uSure = false;
            ResourceLocator.MessageDialogProvider.ShowMessageDialogWithInput(
                "Are you sure about deleting this entry from your list?", "You are about to remove this entry!",
                "I'm sure", "Cancel",
                async () =>
                {
                    LoadingUpdate = true;
                    IsRemoveAnimeButtonEnabled = false;

                    var response = AnimeMode
                        ? await new AnimeRemoveQuery(Id.ToString()).GetRequestResponse()
                        : await new MangaRemoveQuery(Id.ToString()).GetRequestResponse();

                    LoadingUpdate = false;
                    IsRemoveAnimeButtonEnabled = true;

                   _animeLibraryDataStorage.RemoveAnimeEntry(
                        (_animeItemReference as AnimeItemViewModel).ParentAbstraction);

                    (_animeItemReference as AnimeItemViewModel).SetAuthStatus(false, true);
                    AddAnimeVisibility = true;
                    IsAddAnimeButtonEnabled = true;
                    MyDetailsVisibility = false;
                });
        }

        #endregion

        #region FetchAndPopulate

        private void PopulateData(bool clearEnrichment = true)
        {
            //purge scraped data possibly left over from the previously viewed entry
            LeftGenres.Clear();
            RightGenres.Clear();
            Information.Clear();
            Stats.Clear();
            OPs.Clear();
            EDs.Clear();

            if (clearEnrichment)
            {
                Reviews.Clear();
                Recommendations.Clear();
                RelatedAnime.Clear();
            }

            var model = _animeItemReference as AnimeItemViewModel;
            if (model != null && AnimeMode)
            {
                var day = -1;
                try
                {
                    day = StartDate != AnimeItemViewModel.InvalidStartEndDate &&
                          (string.Equals(Status, "Currently Airing", StringComparison.CurrentCultureIgnoreCase) ||
                           string.Equals(Status, "Not yet aired", StringComparison.CurrentCultureIgnoreCase))
                        ? (int) DateTime.Parse(StartDate).DayOfWeek + 1
                        : -1;
                }
                catch (Exception)
                {
                    day = -1;
                }

                DataCache.RegisterVolatileData(Id, new VolatileDataCache
                {
                    DayOfAiring = day,
                    GlobalScore = GlobalScore,
                    AirStartDate = StartDate == AnimeItemViewModel.InvalidStartEndDate ? null : StartDate
                });
                model.Airing = day != -1;
                if (model.ParentAbstraction.TryRetrieveVolatileData())
                    model.UpdateVolatileDataBindings();
            }

            LeftDetailsRow = new List<Tuple<string, string>>();
            RightDetailsRow = new List<Tuple<string, string>>();
            var item = _animeItemReference as AnimeItemViewModel;
            if (AnimeMode || item == null)
            {
                LeftDetailsRow.Add(new Tuple<string, string>(AnimeMode ? "Episodes" : "Chapters",
                    AllEpisodes == 0 ? "?" : AllEpisodes.ToString()));
            }
            else
            {
                LeftDetailsRow.Add(new Tuple<string, string>(Settings.MangaFocusVolumes ? "Volumes" : "Chapters",
                    item.AllEpisodesFocused == 0 ? "?" : item.AllEpisodesFocused.ToString()));
            }

            LeftDetailsRow.Add(new Tuple<string, string>("Score", GlobalScore == 0 ? "N/A" : GlobalScore.ToString("N2")));
            LeftDetailsRow.Add(new Tuple<string, string>("Start",
                StartDate == AnimeItemViewModel.InvalidStartEndDate || string.IsNullOrEmpty(StartDate)
                    ? "?"
                    : StartDate.Contains("-00-00")
                        ? StartDate.Substring(0, 4)
                        : StartDate));
            RightDetailsRow.Add(new Tuple<string, string>("Type", Type));
            if (string.Equals(Status, "Currently Airing", StringComparison.CurrentCultureIgnoreCase) && ResourceLocator.AiringInfoProvider.TryGetCurrentEpisode(Id,out int ep))
            {
                RightDetailsRow.Add(new Tuple<string, string>("Status", $"{Status}\nCurrent ep. {ep}"));
            }
            else
                RightDetailsRow.Add(new Tuple<string, string>("Status", Status));
            RightDetailsRow.Add(new Tuple<string, string>("End",
                EndDate == AnimeItemViewModel.InvalidStartEndDate || string.IsNullOrEmpty(EndDate)
                    ? "?"
                    : EndDate.Contains("-00-00")
                        ? EndDate.Substring(0, 4)
                        : EndDate));

            RaisePropertyChanged(() => LeftDetailsRow);
            RaisePropertyChanged(() => RightDetailsRow);
            RaisePropertyChanged(() => StartYear);
            ViewModelLocator.GeneralMain.CurrentOffStatus = Title;

            // Pre-cache AnimeThemes themes for this entry (fire-and-forget)
            if (!string.IsNullOrEmpty(Title))
            {
                var atTitle = Title;
                var atId = Id;
                var atAnime = AnimeMode;
                Task.Run(async () =>
                {
                    ResourceLocator.EnglishTitlesProvider.TryGetEnglishTitleForSeries(atId, atAnime, out var english);
                    await AnimeThemesHelper.SearchAsync(atTitle, english);
                });
            }

            DetailImage = _imgUrl;
            LoadingGlobal = false;

            if (Settings.DetailsAutoLoadDetails)
                LoadDetails();
            if (Settings.DetailsAutoLoadReviews)
                LoadReviews();
            if (Settings.DetailsAutoLoadRecomms)
                LoadRecommendations();
            if (Settings.DetailsAutoLoadRelated)
                LoadRelatedAnime();

            //Launch UI updates without triggering inner update logic -> nothng to update
            UpdateAnimeReferenceUiBindings(Id);
        }

        private void PopulateStartEndDates()
        {
            try
            {
                _startDateTimeOffset = DateTimeOffset.Parse(_animeItemReference.StartDate);
                StartDateValid = true;
            }
            catch (Exception)
            {
                _startDateTimeOffset = DateTimeOffset.Now;
                StartDateValid = false;
            }
            try
            {
                _endDateTimeOffset = DateTimeOffset.Parse(_animeItemReference.EndDate);
                EndDateValid = true;
            }
            catch (Exception)
            {
                _endDateTimeOffset = DateTimeOffset.Now;
                EndDateValid = false;
            }
        }

        private void ExtractData(AnimeGeneralDetailsData data, bool clearEnrichment = true)
        {
            Title = _animeItemReference?.Title ?? data.Title;
            Type = NormalizeMediaType(data.Type);
            Status = data.Status;
            Synopsis = StripSynopsisCredit(data.Synopsis);
            StartDate = data.StartDate;
            EndDate = data.EndDate;
            GlobalScore = data.GlobalScore;
            GeneralRank = data.Rank > 0 ? $"#{data.Rank:N0}" : "";
            GeneralPopularity = data.Popularity > 0 ? $"#{data.Popularity:N0}" : "";
            GeneralStudios = data.Studios != null && data.Studios.Any() ? string.Join(", ", data.Studios) : "";
            GeneralFavorites = data.FavoritesCount > 0 ? data.FavoritesCount.ToString("N0") : "";
            GeneralMembers = data.MembersCount > 0 ? data.MembersCount.ToString("N0") : "";
            GeneralSeason = string.IsNullOrWhiteSpace(data.Season) ? "" : data.Season;
            TrailerUrl = data.TrailerUrl;
            _imgUrl = NormalizeImageUrl((_animeItemReference as AnimeItemViewModel)?.ImgUrl ?? data.ImgUrl);
            if (Settings.SelectedApiType == ApiType.Hummingbird)
                MalId = data.MalId;

            _broadcast = data.Broadcast ?? "";

            RaisePropertyChanged(() => Type);
            RaisePropertyChanged(() => StartYear);
            RaisePropertyChanged(() => Status);

            _synonyms = data.Synonyms;
            _synonyms = _synonyms.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            for (var i = 0; i < _synonyms.Count; i++)
                _synonyms[i] = Regex.Replace(_synonyms[i], @" ?\(.*?\)", string.Empty);
            //removes string from brackets (sthsth) lol ->  lol
            AllEpisodes = data.AllEpisodes;
            if (!AnimeMode)
            {
                AllVolumes = data.AllVolumes;
                var vm = _animeItemReference as AnimeItemViewModel;
                if (vm != null)
                {
                    vm.UpdateChapterData(data.AllEpisodes);
                }
            }



            PopulateData(clearEnrichment);
        }

        private async Task FetchData(bool force = false, PageIndex? sourcePage = null, bool clearEnrichment = true)
        {

            LoadingGlobal = true;
            try
            {
                var data =
                    await
                        new AnimeGeneralDetailsQuery().GetAnimeDetails(force, Id.ToString(), Title, AnimeMode,
                            sourcePage != null
                                ? sourcePage == PageIndex.PageCharacterDetails ||
                                  sourcePage == PageIndex.PageStaffDetails
                                    ? (ApiType?) ApiType.Mal
                                    : null
                                : null);
                ExtractData(data, clearEnrichment);
            }
            catch (Exception e)
            {

                LoadingGlobal = false;
                // no internet?              
            }

        }

        public async void RefreshData()
        {
            await FetchData(true);
            LoadDetails(true);
            // Reload the decoupled tabs serialized so the global Tenrai rate limiter
            // (1 request at a time) is not saturated.
            try { await LoadEpisodes(true); } catch { }
            try { await LoadRecommendations(true); } catch { }
            try { await LoadRelatedAnime(true); } catch { }
            try { await LoadReviews(true); } catch { }
            if (_loadedCharacters)
                try { await LoadCharacters(true); } catch { }
        }

        public event EmptyEventHander OnDetailsLoaded;
        public event Action<string> RequestVideoPlayback;

        public void PlayVideoInApp(string url)
        {
            if (!string.IsNullOrEmpty(url))
                RequestVideoPlayback?.Invoke(url);
        }

        public event Action<string> RequestWebNavigation;

        public void OpenWebPageInApp(string url)
        {
            if (!string.IsNullOrEmpty(url))
                RequestWebNavigation?.Invoke(url);
        }

        public async void LoadDetails(bool force = false)
        {
            if (LoadingDetails || (_loadedDetails && !force))
                return;
            LoadingDetails = true;
            try
            {
                await LoadDetailsCoreAsync(force);
            }
            finally
            {
                LoadingDetails = false;
            }

            // Prefetch the remaining tabs in the background, serialized so the
            // global Tenrai rate limiter (1 request at a time) is not saturated.
            // Priority order: Episodes > Recomms > Related > Reviews > Characters(+Staff).
            // Selecting a tab forces its own load (TabSelected) independently.
            // NOTE: runs on the UI thread (LoadDetails is invoked from the UI thread),
            // NOT inside Task.Run. The tab fragments re-bind via MvvmLight
            // WhenSourceChanges(LoadingX) / CollectionChanged, which only work when the
            // LoadingX/collection notifications fire on the UI thread. A background
            // prefetch left every post-Details tab blank because those handlers then
            // touched the UI from a pool thread.
            _ = PrefetchRemainingTabs();
        }

        private async Task PrefetchRemainingTabs()
        {
            // Each load is wrapped in its own try/catch so one failure (e.g. a Tenrai
            // 429/timeout throwing out of the query) does not abort the remaining
            // serialized loads. Serialization keeps the global rate limiter from being
            // saturated; the awaits yield the UI thread between each tab.
            try { await LoadEpisodes(); } catch { }
            try { await LoadRecommendations(); } catch { }
            try { await LoadRelatedAnime(); } catch { }
            try { await LoadReviews(); } catch { }
            try { await LoadCharacters(); } catch { }
        }

        private async Task LoadDetailsCoreAsync(bool force)
        {
            LeftGenres.Clear();
            RightGenres.Clear();
            Information.Clear();
            Stats.Clear();
            OPs.Clear();
            EDs.Clear();
            var data = await new AnimeDetailsMalQuery(MalId, AnimeMode).GetDetails(force);
            if (data == null)
            {
                DetailedDataVisibility = false;
                return;
            }
            _loadedDetails = true;
            DetailedDataVisibility = true;
            //Now we can build elements here

            try
            {
                var i = 1;
                foreach (var genre in data.Information.FirstOrDefault(s => s.StartsWith("Genres:"))?.Substring(7).Split(',') ?? Enumerable.Empty<string>())
                {
                    if (i % 2 == 0)
                        LeftGenres.Add(Utils.Utilities.FirstCharToUpper(genre));
                    else
                        RightGenres.Add(Utils.Utilities.FirstCharToUpper(genre));
                    i++;
                }
            }
            catch
            {
                
            }

            try
            {
                //Umm... K-ON is NOT music anime
                if (Id == 5680 || Id == 7791 || Id == 9617)
                {
                    bool truthHadBeenTold = false;
                    for (int j = 0; j < LeftGenres.Count; j++)
                    {
                        if (LeftGenres[j].Trim() == "Music")
                        {
                            LeftGenres[j] = "Certainly NOT Music Anime...";
                            truthHadBeenTold = true;
                            break;
                        }
                    }
                    if (!truthHadBeenTold)
                    {
                        for (int j = 0; j < RightGenres.Count; j++)
                        {
                            if (RightGenres[j].Trim() == "Music")
                            {
                                RightGenres[j] = "Certainly NOT Music Anime...";
                                break;
                            }
                        }
                    }
                }
            }
            catch
            {
                
            }
            
            foreach (var info in data.Information)
            {
                try
                {
                    var infoString = info;
                    if (info.StartsWith("Genres:"))
                        continue;
                    infoString = infoString.Replace(", add some", "");
                    var parts = infoString.Split(':');

                    if (parts[0] == "Broadcast" && parts.Length > 1 && parts[1] != "Unknown")
                    {
                        if (_animeItemReference is AnimeItemViewModel vm)
                        {
                            if (vm.ParentAbstraction.LoadedVolatile)
                            {
                                var time = data.ExtractAiringTime();
                                if (time != null)
                                {
                                    DataCache.UpdateVolatileDataWithExactDate(Id, time);
                                    vm.ParentAbstraction.ExactAiringTime = time;
                                }
                                else
                                    DataCache.RegisterVolatileDataAiringTimeFetchFailure(Id);
                            }
                        }
                    }

                    // fields duplicated by General tab cards / hero stay out of Details
                    var duplicated = parts[0] == "Type" || parts[0] == "Episodes" || parts[0] == "Status"
                                     || parts[0] == "Aired" || parts[0] == "Premiered" || parts[0] == "Studios";
                    // backfill General cards when official API/Tenrai /full failed for this entry
                    if (string.IsNullOrEmpty(GeneralStudios) && parts[0] == "Studios")
                        GeneralStudios = string.Join(":", parts.Skip(1)).Trim();
                    if (string.IsNullOrEmpty(GeneralSeason) && parts[0] == "Premiered")
                        GeneralSeason = string.Join(":", parts.Skip(1)).Trim();
                    if (!duplicated)
                        Information.Add(new Tuple<string, string>(parts[0], string.Join(":", parts.Skip(1))));
                }
                catch (Exception e)
                {
                    
                }
                
            }
            if(_synonyms?.Any() ?? false)
                Information.Add(new Tuple<string, string>("Alt. Titles", string.Join("\n", _synonyms)));

            foreach (var statistic in data.Statistics)
            {
                try
                {
                    var infoString = statistic;
                    var pos = infoString.IndexOf("1 indicates", StringComparison.Ordinal);
                    if (pos != -1)
                        continue;
                    pos = infoString.IndexOf("2 based", StringComparison.Ordinal);
                    if (pos != -1)
                        infoString = infoString.Substring(0, pos - 2);
                    pos = infoString.IndexOf("(scored", StringComparison.Ordinal);
                    if (pos != -1)
                        infoString = infoString.Substring(0, pos - 2);

                    var parts = infoString.Split(':');
                    if (parts.Length > 1)
                    {
                        var value = parts[1].Trim();
                        if (string.IsNullOrEmpty(GeneralRank) && parts[0] == "Rank")
                            GeneralRank = value.StartsWith("#") ? value : $"#{value}";
                        if (string.IsNullOrEmpty(GeneralPopularity) && parts[0] == "Popularity")
                            GeneralPopularity = value.StartsWith("#") ? value : $"#{value}";
                        if (string.IsNullOrEmpty(GeneralMembers) && parts[0] == "Members")
                            GeneralMembers = value;
                        if (string.IsNullOrEmpty(GeneralFavorites) && parts[0] == "Favorites")
                            GeneralFavorites = value;
                    }
                    if (parts[0] == "Rank" || parts[0] == "Popularity" || parts[0] == "Members" || parts[0] == "Favorites" || parts[0] == "Score")
                        continue;
                    Stats.Add(new Tuple<string, string>(parts[0], parts[1]));
                }
                catch
                {
                    
                }

            }


            try
            {
                foreach (var op in data.Openings)
                    OPs.Add(op);
                foreach (var ed in data.Endings)
                    EDs.Add(ed);
            }
            catch
            {
                
            }


            RaisePropertyChanged(() => AnimeMode);
            OnDetailsLoaded?.Invoke();
        }

        public async Task LoadEpisodes(bool force = false)
        {
            if (!AnimeMode) return;
            if (LoadingEpisodes || (_loadedEpisodes && !force && Episodes.Count > 0)) return;
            LoadingEpisodes = true;
            try
            {
                var episodes = await new AnimeEpisodesQuery().GetEpisodes(MalId, force);
                if (episodes != null)
                {
                    Episodes.Clear();
                    Episodes.AddRange(episodes);
                    _loadedEpisodes = true;
                    UpdateLastAired();
                    RaisePropertyChanged(() => TimeTillNextAir);
                }
            }
            catch (Exception ex)
            {
                DiagnosticsReporter.Error("Details", $"LoadEpisodes failed for anime {MalId} (animeMode={AnimeMode})", ex);
            }
            finally
            {
                LoadingEpisodes = false;
            }
        }

        public async Task LoadReviews(bool force = false)
        {
            if (LoadingReviews == true || (_loadedReviews && !force && Reviews.Any()))
                return;
            LoadingReviews = true;
            try
            {
                Reviews.Clear();
                var revs = new List<AnimeReviewData>();
                await Task.Run(async () => revs = await new AnimeReviewsQuery(MalId, AnimeMode).GetAnimeReviews(force));
                if (revs == null)
                {
                    DiagnosticsReporter.Warn("Details", $"reviews: null result for anime {MalId}");
                    NoReviewsDataNoticeVisibility = true;
                    return;
                }
                _loadedReviews = true;
                foreach (var rev in revs)
                    Reviews.Add(rev);
                DiagnosticsReporter.Info("Details", $"reviews: loaded {revs.Count} items for anime {MalId}");
                NoReviewsDataNoticeVisibility = Reviews.Count <= 0;
            }
            catch (Exception ex)
            {
                DiagnosticsReporter.Error("Details", $"LoadReviews failed for anime {MalId} (animeMode={AnimeMode})", ex);
            }
            finally
            {
                LoadingReviews = false;
            }
        }

        public async Task LoadRecommendations(bool force = false)
        {
            if (LoadingRecommendations || (_loadedRecomm && !force && Recommendations.Any()))
                return;
            LoadingRecommendations = true;
            try
            {
                Recommendations.Clear();
                var recomm = new List<DirectRecommendationData>();
                await
                    Task.Run(
                        async () =>
                            recomm =
                                await new AnimeDirectRecommendationsQuery(MalId, AnimeMode).GetDirectRecommendations(force));
                if (recomm == null)
                {
                    DiagnosticsReporter.Warn("Details", $"recommendations: null result for anime {MalId}");
                    NoRecommDataNoticeVisibility = true;
                    return;
                }
                _loadedRecomm = true;
                Recommendations.AddRange(recomm);
                DiagnosticsReporter.Info("Details", $"recommendations: loaded {recomm.Count} items for anime {MalId}");
                NoRecommDataNoticeVisibility = Recommendations.Count <= 0;
            }
            catch (Exception ex)
            {
                DiagnosticsReporter.Error("Details", $"LoadRecommendations failed for anime {MalId} (animeMode={AnimeMode})", ex);
            }
            finally
            {
                LoadingRecommendations = false;
            }
        }

        public async Task LoadRelatedAnime(bool force = false)
        {
            if (LoadingRelated || (_loadedRelated && !force && RelatedAnime.Any()))
                return;
            LoadingRelated = true;
            try
            {
                RelatedAnime.Clear();
                var related = new List<RelatedAnimeData>();
                await Task.Run(async () => related = await new AnimeRelatedQuery(MalId, AnimeMode).GetRelatedAnime(force));
                if (related == null)
                {
                    DiagnosticsReporter.Warn("Details", $"related: null result for anime {MalId} (animeMode={AnimeMode})");
                    NoRelatedDataNoticeVisibility = true;
                    return;
                }
                _loadedRelated = true;
                foreach (var item in related)
                    RelatedAnime.Add(item);
                DiagnosticsReporter.Info("Details", $"related: loaded {related.Count} items for anime {MalId}");
                NoRelatedDataNoticeVisibility = RelatedAnime.Count <= 0;
            }
            catch (Exception ex)
            {
                DiagnosticsReporter.Error("Details", $"LoadRelatedAnime failed for anime {MalId} (animeMode={AnimeMode})", ex);
            }
            finally
            {
                LoadingRelated = false;
            }
        }
   

        public async Task LoadCharacters(bool force = false)
        {
            if (_loadedCharacters && !force && MalId > 0 && AnimeStaffData != null)
                return;
            LoadingCharactersVisibility = true;
            LoadCharactersButtonVisibility = false;
            try
            {
                if (AnimeMode)
                {
                    AnimeStaffData =
                        new AnimeStaffDataViewModels(
                            await new AnimeCharactersStaffQuery(MalId, AnimeMode).GetCharStaffData(force));
                    _loadedCharacters = true;
                    var pairCount = AnimeStaffData?.AnimeCharacterPairs?.Count ?? 0;
                    var staffCount = AnimeStaffData?.AnimeStaff?.Count ?? 0;
                    DiagnosticsReporter.Info("Details", $"characters loaded: {pairCount} pairs, {staffCount} staff for anime {MalId}");
                    CharactersGridVisibility = true;
                }
                else
                {
                    AnimeStaffData =
                        new AnimeStaffDataViewModels(
                            await new AnimeCharactersStaffQuery(MalId, AnimeMode).GetMangaCharStaffData(force));
                    _loadedCharacters = true;
                    CharactersGridVisibility = true;
                }
                LoadingCharactersVisibility = false;
            }
            catch (Exception ex)
            {
                DiagnosticsReporter.Error("Details", $"LoadCharacters failed for anime {MalId} (animeMode={AnimeMode})", ex);
                LoadingCharactersVisibility = false;
            }
        }


        private async void LoadVideos(bool force = false)
        {
            if (LoadingVideosVisibility || (_loadedVideos && !force))
                return;
            AvailableVideos.Clear();
            LoadingVideosVisibility = true;
            try
            {
                foreach (var animeVideoData in await new AnimeVideosQuery(Id).GetVideos(force))
                {
                    AvailableVideos.Add(animeVideoData);
                }

                _loadedVideos = true;
                NoVideosNoticeVisibility = !AvailableVideos.Any();
            }
            finally
            {
                LoadingVideosVisibility = false;
            }
        }

        #endregion

        public async Task OpenVideo(AnimeVideoData data)
        {
            try
            {
                var handler = RequestVideoPlayback;
                if (handler != null)
                    handler(data.YtLink);
                else
                    ResourceLocator.SystemControlsLauncherService.LaunchUri(new Uri(data.YtLink));
            }
            catch (Exception e)
            {
                ResourceLocator.MessageDialogProvider.ShowMessageDialog("Something went wrong with loading this video, probably google has messed again with their api again... yay!","Unable to load youtube video!");
            }
        }

        private static string NormalizeImageUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;

            url = Regex.Replace(url, @"\/r\/\d+x\d+\/", "/");

            var qPos = url.IndexOf('?');
            if (qPos > 0) url = url.Substring(0, qPos);

            var dotPos = url.LastIndexOf('.');
            if (dotPos > 0)
            {
                var beforeDot = url.Substring(0, dotPos);
                var lastChar = beforeDot[beforeDot.Length - 1];
                if (lastChar != 'l' && lastChar != 'm' && lastChar != 's')
                    url = beforeDot + "l" + url.Substring(dotPos);
            }

            return url;
        }

        private static string NormalizeMediaType(string type)
        {
            if (string.IsNullOrWhiteSpace(type)) return type;

            var parts = type.Replace('_', ' ').Trim().Split(' ');
            for (var i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0 && char.IsLower(parts[i][0]))
                    parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
            return string.Join(" ", parts);
        }

        private static string StripSynopsisCredit(string synopsis)
        {
            if (string.IsNullOrEmpty(synopsis)) return synopsis;
            return Regex.Replace(synopsis, @"[\[\(]?\s*Written by MAL[ _]Rewrite\s*[\]\)]?",
                string.Empty, RegexOptions.IgnoreCase).TrimEnd();
        }

        private static readonly TimeSpan JstOffset = TimeSpan.FromHours(9);

        private static readonly string[] DayNames = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

        private static DateTime? ComputeNextAirFromEpisodes(IEnumerable<AnimeEpisode> episodes, DateTime nowUtc)
        {
            var aired = episodes
                .Where(ep => ep.AiredDate.HasValue)
                .Select(ep => ep.AiredDate.Value)
                .Where(d => d <= nowUtc)
                .OrderBy(d => d)
                .ToList();
            if (aired.Count == 0) return null;

            double gap = 7;
            if (aired.Count >= 2)
            {
                var gaps = new List<double>();
                for (int i = 1; i < aired.Count; i++)
                    gaps.Add((aired[i] - aired[i - 1]).TotalDays);
                gaps.Sort();
                gap = gaps[gaps.Count / 2];
                if (gap < 1 || gap > 30)
                    gap = 7;
            }

            var next = aired[aired.Count - 1].AddDays(gap);
            while (next <= nowUtc)
                next = next.AddDays(gap);
            return next;
        }

        private static DateTime? ComputeNextAirDate(string broadcast, DateTime nowUtc)
        {
            if (string.IsNullOrEmpty(broadcast)) return null;

            var match = Regex.Match(broadcast, @"(?<day>[A-Za-z]+?day)[^0-9]*(?<time>\d{1,2}:\d{2})");
            if (!match.Success) return null;

            var dayStr = match.Groups["day"].Value;
            var timeStr = match.Groups["time"].Value;

            var timeParts = timeStr.Split(':');
            if (timeParts.Length != 2 ||
                !int.TryParse(timeParts[0], out var hours) ||
                !int.TryParse(timeParts[1], out var minutes))
                return null;

            var timeOfDay = TimeSpan.FromMinutes(hours * 60 + minutes);

            var dayIndex = -1;
            for (int i = 0; i < DayNames.Length; i++)
            {
                if (dayStr.StartsWith(DayNames[i], StringComparison.OrdinalIgnoreCase))
                {
                    dayIndex = i;
                    break;
                }
            }
            if (dayIndex < 0) return null;

            var nowJst = nowUtc + JstOffset;
            var targetJst = new DateTime(nowJst.Year, nowJst.Month, nowJst.Day, 0, 0, 0).Add(timeOfDay);
            var dayNet = (dayIndex + 1) % 7;
            var daysToAdd = ((dayNet - (int)nowJst.DayOfWeek) + 7) % 7;
            targetJst = targetJst.AddDays(daysToAdd);
            if (targetJst <= nowJst)
                targetJst = targetJst.AddDays(7);

            return targetJst - JstOffset;
        }
    }
}



using System;
using System.Collections.Generic;
using System.Windows.Input;
using Android.Support.V4.App;
using Android.Views;
using MALClient.Android.Fragments;
using MALClient.Android.Fragments.ArticlesPageFragments;
using MALClient.Android.Fragments.CalendarFragments;
using MALClient.Android.Fragments.Clubs;
using MALClient.Android.Fragments.DetailsFragments;
using MALClient.Android.Fragments.ForumFragments;
using MALClient.Android.Fragments.HistoryFragments;
using MALClient.Android.Fragments.MessagingFragments;
using MALClient.Android.Fragments.ProfilePageFragments;
using MALClient.Android.Fragments.RecommendationsFragments;
using MALClient.Android.Fragments.SearchFragments;
using MALClient.Android.Fragments.SettingsFragments;
using MALClient.Models.Enums;
using MALClient.Models.Models;
using MALClient.Models.Models.MalSpecific;
using MALClient.XShared.Delegates;
using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALClient.Android.ViewModels
{
    public delegate void AndroidNavigationRequest(Fragment fragment);

    public class MainViewModel : MainViewModelBase
    {
        public new event AndroidNavigationRequest MainNavigationRequested;

        protected override void CurrentStatusStoryboardBegin()
        {
            //throw new NotImplementedException();
        }

        protected override void CurrentOffSubStatusStoryboardBegin()
        {
           // throw new NotImplementedException();
        }

        protected override void CurrentOffStatusStoryboardBegin()
        {
          //  throw new NotImplementedException();
        }

        public override void Navigate(PageIndex index, object args = null)
        {
            PageIndex originalIndex = index;
            var wasOnSearchPage = SearchToggleLock;
            SearchToggleLock = false;
            if (!Credentials.Authenticated && PageUtils.PageRequiresAuth(index))
            {
                ResourceLocator.MessageDialogProvider.ShowMessageDialog("Log in first in order to access this page.","Log in required");               
                return;
            }
            if(index == PageIndex.PageForumIndex && args is ForumsNavigationArgs arg)
                ResourceLocator.TelemetryProvider.TelemetryTrackNavigation(arg.Page);
            else
                ResourceLocator.TelemetryProvider.TelemetryTrackNavigation(index);

            ScrollToTopButtonVisibility = false;
            ViewModelLocator.AnimeDetails.Id = -1;

            if (index == PageIndex.PageMangaList && args == null)
                args = AnimeListPageNavigationArgs.Manga;

            if (index == PageIndex.PageSeasonal ||
                index == PageIndex.PageMangaList ||
                index == PageIndex.PageTopManga ||
                index == PageIndex.PageMangaAdapted ||
                index == PageIndex.PageTopAnime)
                index = PageIndex.PageAnimeList;

            var destinationIsRoot = index == PageIndex.PageAnimeList
                                    || index == PageIndex.PageDiscover
                                    || index == PageIndex.PageMore;
            if (!IsNavigatingBack && !destinationIsRoot && CurrentMainPage.HasValue && CurrentMainPage != index)
            {
                var candidate = new Tuple<PageIndex, object>(CurrentMainPage.Value, _currentPageNavArgs);
                var top = ViewModelLocator.NavMgr.PeekMainBackNav();
                if (top == null || top.Item1 != candidate.Item1 || !Equals(top.Item2, candidate.Item2))
                    ViewModelLocator.NavMgr.RegisterBackNav(candidate.Item1, candidate.Item2);
            }

            switch (index)
            {
                case PageIndex.PageAnimeList:
                    if (ViewModelLocator.AnimeList.Initializing)
                    {
                        if (!_subscribed)
                        {
                            ViewModelLocator.AnimeList.Initialized += AnimeListOnInitialized;
                            _subscribed = true;
                        }
                        _postponedNavigationArgs = new Tuple<PageIndex, object>(originalIndex, args);
                        return;
                    }
                    switch ((args as AnimeListPageNavigationArgs)?.WorkMode ?? AnimeListWorkModes.Anime)
                    {
                        case AnimeListWorkModes.Anime:
                            ViewModelLocator.GeneralHamburger.SetActiveButton(HamburgerButtons.AnimeList);
                            break;
                        case AnimeListWorkModes.SeasonalAnime:
                            ViewModelLocator.GeneralHamburger.SetActiveButton(HamburgerButtons.Seasonal);
                            break;
                        case AnimeListWorkModes.Manga:
                            ViewModelLocator.GeneralHamburger.SetActiveButton(HamburgerButtons.MangaList);
                            break;
                        case AnimeListWorkModes.TopAnime:
                            ViewModelLocator.GeneralHamburger.SetActiveButton(HamburgerButtons.TopAnime);
                            break;
                        case AnimeListWorkModes.TopManga:
                            ViewModelLocator.GeneralHamburger.SetActiveButton(HamburgerButtons.TopManga);
                            break;
                        case AnimeListWorkModes.MangaAdapted:
                            ViewModelLocator.GeneralHamburger.SetActiveButton(HamburgerButtons.MangaAdapted);
                            break;
                        case AnimeListWorkModes.AnimeByGenre:
                            ViewModelLocator.GeneralHamburger.SetActiveButton(HamburgerButtons.AnimeList);
                            break;
                        case AnimeListWorkModes.AnimeByStudio:
                            ViewModelLocator.GeneralHamburger.SetActiveButton(HamburgerButtons.AnimeList);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    if (CurrentMainPage == PageIndex.PageAnimeList)
                        ViewModelLocator.AnimeList.Init(args as AnimeListPageNavigationArgs);
                    else
                        MainNavigationRequested?.Invoke(new AnimeListPageFragment(args as AnimeListPageNavigationArgs));

                    var alargs = args as AnimeListPageNavigationArgs;
                    if (alargs != null && (alargs.WorkMode == AnimeListWorkModes.Manga && alargs.ResetBackNav))
                    {
                        ViewModelLocator.NavMgr.DeregisterBackNav();
                        ViewModelLocator.NavMgr.RegisterBackNav(PageIndex.PageAnimeList, null);
                    }
                    break;
                case PageIndex.PageAnimeDetails:
                    var detail = ViewModelLocator.AnimeDetails;
                    detail.DetailImage = null;
                    detail.LeftDetailsRow.Clear();
                    detail.RightDetailsRow.Clear();
                    _wasOnDetailsFromSearch = (args as AnimeDetailsPageNavigationArgs).Source == PageIndex.PageSearch;
                    MainNavigationRequested?.Invoke(
                        new AnimeDetailsPageFragment(args as AnimeDetailsPageNavigationArgs));
                    break;
                case PageIndex.PageSettings:
                    MainNavigationRequested?.Invoke(new SettingsPageFragment(args as SettingsPageIndex?));
                    break;
                case PageIndex.PageSearch:
                case PageIndex.PageMangaSearch:
                case PageIndex.PageCharacterSearch:
                case PageIndex.PageSearchEverywhere:
                    if (CurrentMainPage != PageIndex.PageSearch && CurrentMainPage != PageIndex.PageMangaSearch &&
                        CurrentMainPage != PageIndex.PageCharacterSearch)
                        _searchStateBeforeNavigatingToSearch = SearchToggleStatus;

                    if (args != null)
                    {
                        var searchArg = args as SearchPageNavigationArgs;
                        if (string.IsNullOrWhiteSpace(searchArg.Query))
                        {
                            searchArg.Query = CurrentSearchQuery;
                        }
                        if (!searchArg.ByGenre && !searchArg.ByStudio)
                        {
                            SearchToggleLock = true;
                        }
                    }
                    MainNavigationRequested?.Invoke(SearchPageFragment.BuildInstance(args as SearchPageNavigationArgs));
                    break;
                case PageIndex.PageLogIn:
                    MainNavigationRequested?.Invoke(LogInPageFragment.Instance);
                    break;
                case PageIndex.PageProfile:
                    if (Settings.SelectedApiType == ApiType.Mal)
                    {
                        if (CurrentMainPage == PageIndex.PageProfile)
                            ViewModelLocator.ProfilePage.LoadProfileData(args as ProfilePageNavigationArgs);
                        else
                            MainNavigationRequested?.Invoke(new ProfilePageFragment(args as ProfilePageNavigationArgs));
                    }
                    break;
                case PageIndex.PageRecomendations:
                    MainNavigationRequested?.Invoke(
                        new RecommendationsPageFragment(args as RecommendationPageNavigationArgs));
                    break;
                case PageIndex.PageCalendar:
                    MainNavigationRequested?.Invoke(CalendarPageFragment.Instance);
                    break;
                case PageIndex.PageArticles:
                case PageIndex.PageNews:
                    MainNavigationRequested?.Invoke(new ArticlesPageFragment(args as MalArticlesPageNavigationArgs));
                    break;
                case PageIndex.PageDiscover:
                    if (CurrentMainPage == PageIndex.PageDiscover)
                        return;
                    ViewModelLocator.GeneralHamburger.SetActiveButton(HamburgerButtons.Discover);
                    MainNavigationRequested?.Invoke(new DiscoverPageFragment());
                    break;
                case PageIndex.PageMore:
                    if (CurrentMainPage == PageIndex.PageMore)
                        return;
                    MainNavigationRequested?.Invoke(new MorePageFragment());
                    break;
                case PageIndex.PageMessanging:
                    MainNavigationRequested?.Invoke(new MessagingPageFragment());
                    break;
                case PageIndex.PageMessageDetails:
                    MainNavigationRequested?.Invoke(new MessagingDetailsPageFragment(args as MalMessageDetailsNavArgs));
                    break;
                case PageIndex.PageForumIndex:
                    if (CurrentMainPage != null && CurrentMainPage == PageIndex.PageForumIndex)
                        ViewModelLocator.ForumsMain.Init(args as ForumsNavigationArgs);
                    else
                        MainNavigationRequested?.Invoke(new ForumMainPageFragment(args as ForumsNavigationArgs));
                    break;
                case PageIndex.PageHistory:
                    MainNavigationRequested?.Invoke(new HistoryPageFragment(args as HistoryNavigationArgs));
                    break;
                case PageIndex.PageCharacterDetails:
                    OffContentVisibility = true;
                    if (CurrentOffPage == PageIndex.PageCharacterDetails)
                        ViewModelLocator.CharacterDetails.Init(args as CharacterDetailsNavigationArgs);
                    else
                        MainNavigationRequested?.Invoke(
                            new CharacterDetailsPageFragment(args as CharacterDetailsNavigationArgs));
                    break;
                case PageIndex.PageStaffDetails:
                    OffContentVisibility = true;
                    if (CurrentOffPage == PageIndex.PageStaffDetails)
                        ViewModelLocator.StaffDetails.Init(args as StaffDetailsNaviagtionArgs);
                    else
                        MainNavigationRequested?.Invoke(
                            new PersonDetailsPageFragment(args as StaffDetailsNaviagtionArgs));
                    break;
                case PageIndex.PageWallpapers:
                    MainNavigationRequested?.Invoke(new WallpapersPageFragment());
                    break;
                case PageIndex.PagePopularVideos:
                    MainNavigationRequested?.Invoke(new PromoVideosPageFragment());
                    break;
                case PageIndex.PageFeeds:
                    MainNavigationRequested?.Invoke(new FriendsFeedsPageFragment());
                    break;
                case PageIndex.PageNotificationHub:
                    MainNavigationRequested?.Invoke(new NotificationHubPageFragment());
                    break;
                case PageIndex.PageListComparison:
                    MainNavigationRequested?.Invoke(
                        new ListComparisonPageFragment(args as ListComparisonPageNavigationArgs));
                    break;
                case PageIndex.PageFriends:
                    MainNavigationRequested?.Invoke(new FriendsPageFragment(args as FriendsPageNavArgs));
                    break;
                case PageIndex.PageClubIndex:
                    MainNavigationRequested?.Invoke(new ClubsIndexPageFragment());
                    break;
                case PageIndex.PageClubDetails:
                    MainNavigationRequested?.Invoke(new ClubDetailsPageFragment(args as ClubDetailsPageNavArgs));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index), index, null);
            }
            CurrentMainPage = index;
            CurrentMainPageKind = index;
            _currentPageNavArgs = args;
            RaisePropertyChanged(() => SearchToggleLock);
        }

        public override string CurrentOffStatus
        {
            get { return CurrentStatus; }
            set { CurrentStatus = value; }
        }

        public override ICommand RefreshOffDataCommand
        {
            get { return RefreshDataCommand; }
            set { RefreshDataCommand = value; }
        }

        public void PerformFirstNavigation()
        {
            //var previousVersion = Settings.AppVersion;
            //var currentVersion = ResourceLocator.ChangelogProvider.CurrentVersion;
            //var isNewVersion = false;
            //if (previousVersion != null)
            //{
            //    if (previousVersion.Substring(0, previousVersion.LastIndexOf('.')) !=
            //        currentVersion.Substring(0, currentVersion.LastIndexOf('.')))
            //    {
            //        Credentials.Reset();
            //        ResourceLocator.AnimeLibraryDataStorage.Reset();
            //        ResourceLocator.MalHttpContextProvider.Invalidate();
            //        ResourceLocator.DataCacheService.ClearAnimeListData();
            //    }
            //}

            bool hasArgumentsWithSync =
                    InitDetailsFull?.Item1.GetAttribute<EnumUtilities.PageIndexEnumMember>().RequiresSyncBlock ?? true;
            if (Credentials.Authenticated)
            {
                if (hasArgumentsWithSync)
                    Navigate(Settings.DefaultMenuTab == "anime"
                        ? PageIndex.PageAnimeList
                        : Settings.DefaultMenuTab == "manga" ? PageIndex.PageMangaList : PageIndex.PageDiscover);
                //entry point whatnot
                else if (InitDetailsFull != null)
                {
                    ViewModelLocator.AnimeList.Init(null);
                    Navigate(InitDetailsFull.Item1, InitDetailsFull.Item2);
                }
            }
            else
            {
                Navigate(PageIndex.PageLogIn);
            }
            if (InitDetails != null || hasArgumentsWithSync)
            {
                ViewModelLocator.AnimeList.Initialized += AnimeListOnInitializedLoadArgs;
            }
        }
    }
}
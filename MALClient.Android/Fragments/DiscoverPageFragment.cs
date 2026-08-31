using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Android.OS;
using Android.Views;
using Android.Widget;
using FFImageLoading.Views;
using MALClient.Android.Activities;
using MALClient.Android.Listeners;
using MALClient.Android.UserControls;
using MALClient.Models.Enums;
using MALClient.Models.Models.Anime;
using MALClient.Models.Models.AnimeScrapped;
using MALClient.Models.Models.Library;
using MALClient.Models.Models.MalSpecific;
using MALClient.XShared.Comm;
using MALClient.XShared.Comm.Anime;
using MALClient.XShared.Comm.Articles;
using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;
using Newtonsoft.Json;

namespace MALClient.Android.Fragments
{
    public class DiscoverPageFragment : MalFragmentBase
    {
        private static readonly TimeZoneInfo JstTimeZone = TimeZoneInfo.CreateCustomTimeZone("JST", TimeSpan.FromHours(9), "JST", "JST");
        private bool _dataLoaded;

        public DiscoverPageFragment()
        {
        }

        public override int LayoutResourceId => Resource.Layout.DiscoverPage;

        protected override void Init(Bundle savedInstanceState)
        {
        }

        protected override void InitBindings()
        {
            if (_dataLoaded)
                return;
            _dataLoaded = true;
            DiscoverWatchingSeeAll.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageAnimeList, new AnimeListPageNavigationArgs(0, AnimeListWorkModes.Anime))));
            DiscoverReadingSeeAll.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageMangaList, new AnimeListPageNavigationArgs(0, AnimeListWorkModes.Manga))));
            DiscoverCompletedSeeAll.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageAnimeList, new AnimeListPageNavigationArgs(1, AnimeListWorkModes.Anime))));
            DiscoverSeasonalSeeAll.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageSeasonal, AnimeListPageNavigationArgs.Seasonal)));
            DiscoverTopAnimeSeeAll.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageTopAnime, AnimeListPageNavigationArgs.TopAnime(TopAnimeType.General))));
            DiscoverTopMangaSeeAll.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageTopManga, AnimeListPageNavigationArgs.TopManga)));
            DiscoverAdaptedSeeAll.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageMangaAdapted, AnimeListPageNavigationArgs.MangaAdapted(MangaAdaptedType.AiringNow))));
            DiscoverNewsSeeAll.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageNews, new MalArticlesPageNavigationArgs { WorkMode = ArticlePageWorkMode.News, Source = PageIndex.PageDiscover })));
            DiscoverSuggestionsSeeAll.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageRecomendations, null)));
            DiscoverUpcomingSeeAll.SetOnClickListener(new OnClickListener(v =>
            {
                var args = AnimeListPageNavigationArgs.Seasonal;
                args.CurrSeason = GetNextSeason();
                NavigateTo(PageIndex.PageSeasonal, args);
            }));
            DiscoverFeaturedSeeAll.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageNews, new MalArticlesPageNavigationArgs { WorkMode = ArticlePageWorkMode.Articles, Source = PageIndex.PageDiscover })));
            DiscoverLoginButton.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageLogIn, null)));
            DiscoverSeasonalHeader.Text = $"Seasonal - {GetCurrentSeason().Name}";
            DiscoverUpcomingHeader.Text = $"Upcoming - {GetNextSeason().Name}";
            DiscoverPageRefresh.ScrollingView = DiscoverPageScroll;
            DiscoverPageRefresh.Refresh += DiscoverPageRefreshOnRefresh;
            if (!Credentials.Authenticated)
            {
                DiscoverLoginPrompt.Visibility = ViewStates.Visible;
                DiscoverLoginButton.Visibility = ViewStates.Visible;
            }
            LoadSections();
        }

        private void DiscoverPageRefreshOnRefresh(object sender, EventArgs e)
        {
            ClearSections();
            ReloadSectionsAsync();
        }

        private async void ReloadSectionsAsync()
        {
            await LoadSections(force: true);
            DiscoverPageRefresh.Refreshing = false;
        }

        private async Task LoadSections(bool force = false)
        {
            DiscoverPageLoadingSpinner.Visibility = ViewStates.Visible;
            try
            {
                await Task.WhenAll(
                    LoadWatchingAsync(force),
                    LoadReadingAsync(force),
                    LoadCompletedAsync(force),
                    LoadSuggestionsAsync(),
                    LoadSeasonalAsync(force),
                    LoadUpcomingAsync(force),
                    LoadTopAnimeAsync(force),
                    LoadTopMangaAsync(force),
                    LoadAdaptedAsync(force),
                    LoadFeaturedAsync(force),
                    LoadNewsAsync(force));
            }
            catch (Exception)
            {
                // individual sections handle their own failures
            }
            DiscoverPageLoadingSpinner.Visibility = ViewStates.Gone;
        }

        private async Task LoadSectionAsync<T>(TextView header, LinearLayout row, Func<Task<List<T>>> fetch, Func<List<T>, Task> populate)
        {
            try
            {
                var data = await fetch();
                if (data == null || data.Count == 0)
                {
                    SetSectionVisibility(header, row, false);
                    return;
                }
                await populate(data);
            }
            catch (Exception)
            {
                SetSectionVisibility(header, row, false);
            }
        }

        private void ClearSections()
        {
            DiscoverWatchingRow.RemoveAllViews();
            DiscoverReadingRow.RemoveAllViews();
            DiscoverCompletedRow.RemoveAllViews();
            DiscoverSuggestionsRow.RemoveAllViews();
            DiscoverSeasonalRow.RemoveAllViews();
            DiscoverUpcomingRow.RemoveAllViews();
            DiscoverTopAnimeRow.RemoveAllViews();
            DiscoverTopMangaRow.RemoveAllViews();
            DiscoverAdaptedRow.RemoveAllViews();
            DiscoverFeaturedRow.RemoveAllViews();
            DiscoverNewsRow.RemoveAllViews();
        }

        private Task LoadWatchingAsync(bool force = false)
        {
            return LoadSectionAsync(DiscoverWatchingHeader, DiscoverWatchingRow, async () =>
            {
                if (!Credentials.Authenticated)
                    return new List<AnimeItemAbstraction>();
                return await GetWatchingAnimeAsync();
            }, data => PopulateWatchingRow(data.Take(12).ToList()));
        }

        private static async Task<List<AnimeItemAbstraction>> GetWatchingAnimeAsync()
        {
            return await GetPersonalAnimeAsync(1);
        }

        private static async Task<List<AnimeItemAbstraction>> GetCompletedAnimeAsync()
        {
            return await GetPersonalAnimeAsync(2);
        }

        private static async Task<List<AnimeItemAbstraction>> GetPersonalAnimeAsync(int status)
        {
            var client = await ResourceLocator.MalHttpContextProvider.GetApiHttpContextAsync();
            var raw = await client.GetStringAsync(
                $"https://myanimelist.net/animelist/{Credentials.UserName}/load.json?offset=0&status={status}&order=5");
            if (string.IsNullOrEmpty(raw))
                return new List<AnimeItemAbstraction>();
            var items = JsonConvert.DeserializeObject<List<LibraryListQuery.RootObject>>(raw);
            return items.Select(item =>
            {
                var image = item.anime_image_path;
                if (!string.IsNullOrEmpty(image))
                {
                    image = Regex.Replace(image, @"\/r\/\d+x\d+", "");
                    var queryIndex = image.IndexOf('?');
                    if (queryIndex >= 0)
                        image = image.Substring(0, queryIndex);
                }
                var libraryItem = new AnimeLibraryItemData
                {
                    Title = item.anime_title,
                    ImgUrl = image,
                    Type = (int)MalTypeParser.ParseAnimeType(item.anime_media_type_string),
                    MalId = item.anime_id,
                    MyStatus = (AnimeStatus)status,
                    MyEpisodes = item.num_watched_episodes,
                    AllEpisodes = item.anime_num_episodes,
                    MyScore = item.score,
                    IsRewatching = (item.is_rewatching ?? 0) > 0
                };
                return new AnimeItemAbstraction(true, libraryItem);
            }).ToList();
        }

        private static AnimeType GetAnimeType(string type)
        {
            return MalTypeParser.ParseAnimeType(type);
        }

        private Task LoadReadingAsync(bool force = false)
        {
            return LoadSectionAsync(DiscoverReadingHeader, DiscoverReadingRow, async () =>
            {
                if (!Credentials.Authenticated)
                    return new List<AnimeItemAbstraction>();
                return await GetReadingMangaAsync();
            }, async data => { await PopulateReadingRow(data.Take(12).ToList()); });
        }

        private Task LoadCompletedAsync(bool force = false)
        {
            return LoadSectionAsync(DiscoverCompletedHeader, DiscoverCompletedRow, async () =>
            {
                if (!Credentials.Authenticated)
                    return new List<AnimeItemAbstraction>();
                return await GetCompletedAnimeAsync();
            }, async data => { await PopulateCompletedRow(data.Take(12).ToList()); });
        }

        private Task LoadSuggestionsAsync()
        {
            return LoadSectionAsync(DiscoverSuggestionsHeader, DiscoverSuggestionsRow, async () =>
            {
                if (!Credentials.Authenticated)
                    return new List<AnimeLibraryItemData>();
                var recs = await new AnimePersonalizedRecommendationsQuery(true).GetPersonalizedRecommendations();
                if (recs == null)
                    return new List<AnimeLibraryItemData>();
                return recs.Take(12).Select(rec => new AnimeLibraryItemData
                {
                    Title = rec.Title,
                    ImgUrl = rec.ImgUrl,
                    MalId = rec.Id,
                    MyStatus = AnimeStatus.PlanToWatch,
                }).Cast<AnimeLibraryItemData>().ToList();
            }, data => { PopulateLibraryCardRow(DiscoverSuggestionsRow, data); return Task.CompletedTask; });
        }

        private static async Task<List<AnimeItemAbstraction>> GetReadingMangaAsync()
        {
            var client = await ResourceLocator.MalHttpContextProvider.GetApiHttpContextAsync();
            var raw = await client.GetStringAsync(
                $"https://myanimelist.net/mangalist/{Credentials.UserName}/load.json?offset=0&status=1&order=5");
            if (string.IsNullOrEmpty(raw))
                return new List<AnimeItemAbstraction>();
            var items = JsonConvert.DeserializeObject<List<LibraryListQuery.MangaRootObject>>(raw);
            return items.Select(item =>
            {
                var image = item.manga_image_path;
                if (!string.IsNullOrEmpty(image))
                {
                    image = Regex.Replace(image, @"\/r\/\d+x\d+", "");
                    var queryIndex = image.IndexOf('?');
                    if (queryIndex >= 0)
                        image = image.Substring(0, queryIndex);
                }
                var libraryItem = new MangaLibraryItemData
                {
                    Title = item.manga_title,
                    ImgUrl = image,
                    Type = (int)MalTypeParser.ParseMangaType(item.manga_media_type_string),
                    MalId = item.manga_id,
                    MyStatus = AnimeStatus.Watching,
                    MyEpisodes = item.num_read_chapters,
                    AllEpisodes = item.manga_num_chapters,
                    MyVolumes = item.num_read_volumes,
                    AllVolumes = item.manga_num_volumes,
                    MyScore = item.score,
                    IsRewatching = (item.is_rereading ?? 0) > 0
                };
                return new AnimeItemAbstraction(true, libraryItem);
            }).ToList();
        }

        private static MangaType GetMangaType(string type)
        {
            return MalTypeParser.ParseMangaType(type);
        }

        private Task LoadSeasonalAsync(bool force = false)
        {
            return LoadSectionAsync(DiscoverSeasonalHeader, DiscoverSeasonalRow,
                async () => await new AnimeSeasonalQuery(GetCurrentSeason()).GetSeasonalAnime(force),
                data => { PopulateAnimeRow(DiscoverSeasonalRow, data.Take(12).ToList(), true); return Task.CompletedTask; });
        }

        private Task LoadUpcomingAsync(bool force = false)
        {
            return LoadSectionAsync(DiscoverUpcomingHeader, DiscoverUpcomingRow,
                async () => await new AnimeSeasonalQuery(GetNextSeason()).GetSeasonalAnime(force),
                data => { PopulateAnimeRow(DiscoverUpcomingRow, data.Take(12).ToList(), true); return Task.CompletedTask; });
        }

        private Task LoadTopAnimeAsync(bool force = false)
        {
            return LoadSectionAsync(DiscoverTopAnimeHeader, DiscoverTopAnimeRow,
                async () => await new AnimeTopQuery(TopAnimeType.General).GetTopAnimeData(force),
                data => { PopulateAnimeRow(DiscoverTopAnimeRow, data.Take(12).ToList(), true); return Task.CompletedTask; });
        }

        private Task LoadTopMangaAsync(bool force = false)
        {
            return LoadSectionAsync(DiscoverTopMangaHeader, DiscoverTopMangaRow,
                async () => await new AnimeTopQuery(MangaTopType.All).GetTopAnimeData(force),
                data => { PopulateAnimeRow(DiscoverTopMangaRow, data.Take(12).ToList(), false); return Task.CompletedTask; });
        }

        private Task LoadAdaptedAsync(bool force = false)
        {
            return LoadSectionAsync(DiscoverAdaptedHeader, DiscoverAdaptedRow,
                async () => await new AnimeAdaptedToAnimeQuery(MangaAdaptedType.AiringNow).GetAdaptedToAnimeData(force),
                data => { PopulateAnimeRow(DiscoverAdaptedRow, data.Take(12).ToList(), false); return Task.CompletedTask; });
        }

        private Task LoadFeaturedAsync(bool force = false)
        {
            return LoadSectionAsync(DiscoverFeaturedHeader, DiscoverFeaturedRow,
                async () => await new MalArticlesIndexQuery(ArticlePageWorkMode.Articles).GetArticlesIndex(force),
                data => { PopulateNewsRow(DiscoverFeaturedRow, data.Take(10).ToList()); return Task.CompletedTask; });
        }

        private Task LoadNewsAsync(bool force = false)
        {
            return LoadSectionAsync(DiscoverNewsHeader, DiscoverNewsRow,
                async () => await new MalArticlesIndexQuery(ArticlePageWorkMode.News).GetArticlesIndex(force),
                data => { PopulateNewsRow(DiscoverNewsRow, data.Take(10).ToList()); return Task.CompletedTask; });
        }

        private void PopulateLibraryCardRow(LinearLayout row, List<AnimeLibraryItemData> data)
        {
            foreach (var item in data)
            {
                item.Type = (int)AnimeType.TV;
                AddGridCardToRow(row, new AnimeItemAbstraction(true, item).ViewModel);
            }
        }

        private void AddGridCardToRow(LinearLayout row, AnimeItemViewModel viewModel)
        {
            var view = new AnimeGridItem(Activity, vm => vm.NavigateDetails(PageIndex.PageDiscover));
            view.LayoutParameters = new LinearLayout.LayoutParams(
                (int)Resources.GetDimension(Resource.Dimension.GridCardWidth),
                (int)Resources.GetDimension(Resource.Dimension.GridCardHeight))
            {
                TopMargin = DimensionsHelper.DpToPx(2),
                BottomMargin = DimensionsHelper.DpToPx(2),
                LeftMargin = DimensionsHelper.DpToPx(4),
                RightMargin = DimensionsHelper.DpToPx(4)
            };
            view.BindModel(viewModel, false);
            row.AddView(view);
        }

        private void PopulateAnimeRow(LinearLayout row, IEnumerable<SeasonalAnimeData> data, bool isAnime)
        {
            foreach (var item in data)
                AddGridCardToRow(row, new AnimeItemAbstraction(item, isAnime).ViewModel);
        }

        private async Task PopulateWatchingRow(IList<AnimeItemAbstraction> data)
        {
            foreach (var item in data)
            {
                var viewModel = item.ViewModel;
                viewModel.SetNextAirCache(await viewModel.GetTimeTillNextAirAsync(JstTimeZone));
                AddGridCardToRow(DiscoverWatchingRow, viewModel);
            }
        }

        private Task PopulateReadingRow(IList<AnimeItemAbstraction> data)
        {
            foreach (var item in data)
                AddGridCardToRow(DiscoverReadingRow, item.ViewModel);
            return Task.CompletedTask;
        }

        private Task PopulateCompletedRow(IList<AnimeItemAbstraction> data)
        {
            foreach (var item in data)
                AddGridCardToRow(DiscoverCompletedRow, item.ViewModel);
            return Task.CompletedTask;
        }

        private void PopulateNewsRow(LinearLayout row, IList<MalNewsUnitModel> data)
        {
            var cardWidth = DimensionsHelper.DpToPx(320);
            var verticalMargin = DimensionsHelper.DpToPx(2);
            var horizontalMargin = DimensionsHelper.DpToPx(4);
            foreach (var item in data)
            {
                var view = Activity.LayoutInflater.Inflate(Resource.Layout.ArtclesPageItem, null);
                view.LayoutParameters = new LinearLayout.LayoutParams(cardWidth, ViewGroup.LayoutParams.WrapContent)
                {
                    TopMargin = verticalMargin,
                    BottomMargin = verticalMargin,
                    RightMargin = horizontalMargin
                };
                view.FindViewById<TextView>(Resource.Id.ArticlesPageItemAuthor).Text = item.Author;
                view.FindViewById<TextView>(Resource.Id.ArticlesPageItemViews).Text = item.Views;
                view.FindViewById<TextView>(Resource.Id.ArticlesPageItemTags).Text = item.Tags;
                view.FindViewById<TextView>(Resource.Id.ArticlesPageItemHeader).Text = item.Title;
                view.FindViewById<TextView>(Resource.Id.ArticlesPageItemHighlight).Text = item.Highlight;
                try
                {
                    view.FindViewById<ImageViewAsync>(Resource.Id.ArticlesPageItemImage).Into(item.ImgUrl, null, null, 200);
                }
                catch (Exception)
                {
                    // network on main thread
                }
                view.SetOnClickListener(new OnClickListener(v =>
                {
                    ViewModelLocator.MalArticles.PendingArticle = item;
                    ViewModelLocator.MalArticles.PendingArticleAt = DateTime.UtcNow;
                    NavigateTo(PageIndex.PageNews, new MalArticlesPageNavigationArgs { WorkMode = ArticlePageWorkMode.News, Source = PageIndex.PageDiscover });
                }));
                row.AddView(view);
            }
        }

        private void SetSectionVisibility(TextView header, LinearLayout row, bool visible)
        {
            var visibility = visible ? ViewStates.Visible : ViewStates.Gone;
            header.Visibility = visibility;
            row.Visibility = visibility;
        }

        private void NavigateTo(PageIndex page, object args)
        {
            ViewModelLocator.GeneralMain.Navigate(page, args);
        }

        public void ScrollToTop()
        {
            DiscoverPageScroll.FullScroll(global::Android.Views.FocusSearchDirection.Up);
        }

        public void ScrollToSection(int index)
        {
            var headers = new[]
            {
                DiscoverWatchingHeader,
                DiscoverReadingHeader,
                DiscoverCompletedHeader,
                DiscoverSeasonalHeader,
                DiscoverTopAnimeHeader,
                DiscoverTopMangaHeader,
                DiscoverAdaptedHeader,
                DiscoverNewsHeader
            };
            if (index < 0 || index >= headers.Length)
                return;
            var target = headers[index];
            if (target.Visibility != ViewStates.Visible)
                return;
            var targetRect = new global::Android.Graphics.Rect();
            target.GetGlobalVisibleRect(targetRect);
            var scrollRect = new global::Android.Graphics.Rect();
            DiscoverPageScroll.GetGlobalVisibleRect(scrollRect);
            DiscoverPageScroll.SmoothScrollTo(0, targetRect.Top - scrollRect.Top + DiscoverPageScroll.ScrollY);
        }

        private static AnimeSeason GetCurrentSeason()
        {
            var now = DateTime.UtcNow;
            var month = now.Month;
            Season season;
            if (month >= 3 && month <= 5)
                season = Season.Spring;
            else if (month >= 6 && month <= 8)
                season = Season.Summer;
            else if (month >= 9 && month <= 11)
                season = Season.Fall;
            else
                season = Season.Winter;
            var year = season == Season.Winter && month == 12 ? now.Year + 1 : now.Year;
            return new AnimeSeason
            {
                Name = $"{season} {year}",
                Year = year,
                Season = season,
                IsCurrentSeason = true
            };
        }

        private static AnimeSeason GetNextSeason()
        {
            var current = GetCurrentSeason();
            Season next;
            int year = current.Year;
            switch (current.Season)
            {
                case Season.Winter:
                    next = Season.Spring;
                    break;
                case Season.Spring:
                    next = Season.Summer;
                    break;
                case Season.Summer:
                    next = Season.Fall;
                    break;
                default:
                    next = Season.Winter;
                    year++;
                    break;
            }
            return new AnimeSeason
            {
                Name = $"{next} {year}",
                Year = year,
                Season = next,
                IsCurrentSeason = false
            };
        }

        #region Views

        private ScrollView _discoverPageScroll;
        private ScrollableSwipeToRefreshLayout _discoverPageRefresh;
        private ProgressBar _discoverPageLoadingSpinner;
        private TextView _discoverLoginPrompt;
        private TextView _discoverLoginButton;
        private TextView _discoverWatchingSeeAll;
        private TextView _discoverReadingSeeAll;
        private TextView _discoverCompletedSeeAll;
        private TextView _discoverSeasonalSeeAll;
        private TextView _discoverTopAnimeSeeAll;
        private TextView _discoverTopMangaSeeAll;
        private TextView _discoverAdaptedSeeAll;
        private TextView _discoverNewsSeeAll;
        private TextView _discoverSuggestionsSeeAll;
        private TextView _discoverUpcomingSeeAll;
        private TextView _discoverFeaturedSeeAll;
        private TextView _discoverSeasonalHeader;
        private TextView _discoverTopAnimeHeader;
        private TextView _discoverTopMangaHeader;
        private TextView _discoverAdaptedHeader;
        private TextView _discoverNewsHeader;
        private TextView _discoverWatchingHeader;
        private TextView _discoverReadingHeader;
        private TextView _discoverCompletedHeader;
        private TextView _discoverSuggestionsHeader;
        private TextView _discoverUpcomingHeader;
        private TextView _discoverFeaturedHeader;
        private LinearLayout _discoverSeasonalRow;
        private LinearLayout _discoverTopAnimeRow;
        private LinearLayout _discoverTopMangaRow;
        private LinearLayout _discoverAdaptedRow;
        private LinearLayout _discoverNewsRow;
        private LinearLayout _discoverWatchingRow;
        private LinearLayout _discoverReadingRow;
        private LinearLayout _discoverCompletedRow;
        private LinearLayout _discoverSuggestionsRow;
        private LinearLayout _discoverUpcomingRow;
        private LinearLayout _discoverFeaturedRow;

        public ScrollView DiscoverPageScroll => GetView(ref _discoverPageScroll, Resource.Id.DiscoverPageScroll);
        public ScrollableSwipeToRefreshLayout DiscoverPageRefresh => GetView(ref _discoverPageRefresh, Resource.Id.DiscoverPageRefresh);
        public ProgressBar DiscoverPageLoadingSpinner => GetView(ref _discoverPageLoadingSpinner, Resource.Id.DiscoverPageLoadingSpinner);
        public TextView DiscoverLoginPrompt => GetView(ref _discoverLoginPrompt, Resource.Id.DiscoverLoginPrompt);
        public TextView DiscoverLoginButton => GetView(ref _discoverLoginButton, Resource.Id.DiscoverLoginButton);
        public TextView DiscoverWatchingSeeAll => GetView(ref _discoverWatchingSeeAll, Resource.Id.DiscoverWatchingSeeAll);
        public TextView DiscoverReadingSeeAll => GetView(ref _discoverReadingSeeAll, Resource.Id.DiscoverReadingSeeAll);
        public TextView DiscoverCompletedSeeAll => GetView(ref _discoverCompletedSeeAll, Resource.Id.DiscoverCompletedSeeAll);
        public TextView DiscoverSeasonalSeeAll => GetView(ref _discoverSeasonalSeeAll, Resource.Id.DiscoverSeasonalSeeAll);
        public TextView DiscoverTopAnimeSeeAll => GetView(ref _discoverTopAnimeSeeAll, Resource.Id.DiscoverTopAnimeSeeAll);
        public TextView DiscoverTopMangaSeeAll => GetView(ref _discoverTopMangaSeeAll, Resource.Id.DiscoverTopMangaSeeAll);
        public TextView DiscoverAdaptedSeeAll => GetView(ref _discoverAdaptedSeeAll, Resource.Id.DiscoverAdaptedSeeAll);
        public TextView DiscoverNewsSeeAll => GetView(ref _discoverNewsSeeAll, Resource.Id.DiscoverNewsSeeAll);
        public TextView DiscoverSuggestionsSeeAll => GetView(ref _discoverSuggestionsSeeAll, Resource.Id.DiscoverSuggestionsSeeAll);
        public TextView DiscoverUpcomingSeeAll => GetView(ref _discoverUpcomingSeeAll, Resource.Id.DiscoverUpcomingSeeAll);
        public TextView DiscoverFeaturedSeeAll => GetView(ref _discoverFeaturedSeeAll, Resource.Id.DiscoverFeaturedSeeAll);
        public TextView DiscoverSeasonalHeader => GetView(ref _discoverSeasonalHeader, Resource.Id.DiscoverSeasonalHeader);
        public TextView DiscoverTopAnimeHeader => GetView(ref _discoverTopAnimeHeader, Resource.Id.DiscoverTopAnimeHeader);
        public TextView DiscoverTopMangaHeader => GetView(ref _discoverTopMangaHeader, Resource.Id.DiscoverTopMangaHeader);
        public TextView DiscoverAdaptedHeader => GetView(ref _discoverAdaptedHeader, Resource.Id.DiscoverAdaptedHeader);
        public TextView DiscoverNewsHeader => GetView(ref _discoverNewsHeader, Resource.Id.DiscoverNewsHeader);
        public TextView DiscoverWatchingHeader => GetView(ref _discoverWatchingHeader, Resource.Id.DiscoverWatchingHeader);
        public TextView DiscoverReadingHeader => GetView(ref _discoverReadingHeader, Resource.Id.DiscoverReadingHeader);
        public TextView DiscoverCompletedHeader => GetView(ref _discoverCompletedHeader, Resource.Id.DiscoverCompletedHeader);
        public TextView DiscoverSuggestionsHeader => GetView(ref _discoverSuggestionsHeader, Resource.Id.DiscoverSuggestionsHeader);
        public TextView DiscoverUpcomingHeader => GetView(ref _discoverUpcomingHeader, Resource.Id.DiscoverUpcomingHeader);
        public TextView DiscoverFeaturedHeader => GetView(ref _discoverFeaturedHeader, Resource.Id.DiscoverFeaturedHeader);
        public LinearLayout DiscoverSeasonalRow => GetView(ref _discoverSeasonalRow, Resource.Id.DiscoverSeasonalRow);
        public LinearLayout DiscoverTopAnimeRow => GetView(ref _discoverTopAnimeRow, Resource.Id.DiscoverTopAnimeRow);
        public LinearLayout DiscoverTopMangaRow => GetView(ref _discoverTopMangaRow, Resource.Id.DiscoverTopMangaRow);
        public LinearLayout DiscoverAdaptedRow => GetView(ref _discoverAdaptedRow, Resource.Id.DiscoverAdaptedRow);
        public LinearLayout DiscoverNewsRow => GetView(ref _discoverNewsRow, Resource.Id.DiscoverNewsRow);
        public LinearLayout DiscoverSuggestionsRow => GetView(ref _discoverSuggestionsRow, Resource.Id.DiscoverSuggestionsRow);
        public LinearLayout DiscoverUpcomingRow => GetView(ref _discoverUpcomingRow, Resource.Id.DiscoverUpcomingRow);
        public LinearLayout DiscoverFeaturedRow => GetView(ref _discoverFeaturedRow, Resource.Id.DiscoverFeaturedRow);
        public LinearLayout DiscoverWatchingRow => GetView(ref _discoverWatchingRow, Resource.Id.DiscoverWatchingRow);
        public LinearLayout DiscoverReadingRow => GetView(ref _discoverReadingRow, Resource.Id.DiscoverReadingRow);
        public LinearLayout DiscoverCompletedRow => GetView(ref _discoverCompletedRow, Resource.Id.DiscoverCompletedRow);

        #endregion
    }
}

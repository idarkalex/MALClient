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
                NavigateTo(PageIndex.PageNews, MalArticlesPageNavigationArgs.News)));
            DiscoverLoginButton.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageLogIn, null)));
            DiscoverSeasonalHeader.Text = $"Seasonal - {GetCurrentSeason().Name}";
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
            await LoadSections();
            DiscoverPageRefresh.Refreshing = false;
        }

        private async Task LoadSections()
        {
            DiscoverPageLoadingSpinner.Visibility = ViewStates.Visible;
            try
            {
                await Task.WhenAll(
                    LoadWatchingAsync(),
                    LoadReadingAsync(),
                    LoadCompletedAsync(),
                    LoadSeasonalAsync(),
                    LoadTopAnimeAsync(),
                    LoadTopMangaAsync(),
                    LoadAdaptedAsync(),
                    LoadNewsAsync());
            }
            catch (Exception)
            {
                // individual sections handle their own failures
            }
            DiscoverPageLoadingSpinner.Visibility = ViewStates.Gone;
        }

        private void ClearSections()
        {
            DiscoverWatchingRow.RemoveAllViews();
            DiscoverReadingRow.RemoveAllViews();
            DiscoverCompletedRow.RemoveAllViews();
            DiscoverSeasonalRow.RemoveAllViews();
            DiscoverTopAnimeRow.RemoveAllViews();
            DiscoverTopMangaRow.RemoveAllViews();
            DiscoverAdaptedRow.RemoveAllViews();
            DiscoverNewsRow.RemoveAllViews();
        }

        private async Task LoadWatchingAsync()
        {
            if (!Credentials.Authenticated)
            {
                SetSectionVisibility(DiscoverWatchingHeader, DiscoverWatchingRow, false);
                return;
            }
            try
            {
                var data = await GetWatchingAnimeAsync();
                if (data == null || data.Count == 0)
                {
                    SetSectionVisibility(DiscoverWatchingHeader, DiscoverWatchingRow, false);
                    return;
                }
                PopulateWatchingRow(data.Take(12).ToList());
            }
            catch (Exception)
            {
                SetSectionVisibility(DiscoverWatchingHeader, DiscoverWatchingRow, false);
            }
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
                    Type = (int)GetAnimeType(item.anime_media_type_string),
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
            switch (type)
            {
                case "TV":
                    return AnimeType.TV;
                case "Movie":
                    return AnimeType.Movie;
                case "Special":
                    return AnimeType.Special;
                case "OVA":
                    return AnimeType.OVA;
                case "ONA":
                    return AnimeType.ONA;
                case "Music":
                    return AnimeType.Music;
            }
            return AnimeType.TV;
        }

        private async Task LoadReadingAsync()
        {
            if (!Credentials.Authenticated)
            {
                SetSectionVisibility(DiscoverReadingHeader, DiscoverReadingRow, false);
                return;
            }
            try
            {
                var data = await GetReadingMangaAsync();
                if (data == null || data.Count == 0)
                {
                    SetSectionVisibility(DiscoverReadingHeader, DiscoverReadingRow, false);
                    return;
                }
                PopulateReadingRow(data.Take(12).ToList());
            }
            catch (Exception)
            {
                SetSectionVisibility(DiscoverReadingHeader, DiscoverReadingRow, false);
            }
        }

        private async Task LoadCompletedAsync()
        {
            if (!Credentials.Authenticated)
            {
                SetSectionVisibility(DiscoverCompletedHeader, DiscoverCompletedRow, false);
                return;
            }
            try
            {
                var data = await GetCompletedAnimeAsync();
                if (data == null || data.Count == 0)
                {
                    SetSectionVisibility(DiscoverCompletedHeader, DiscoverCompletedRow, false);
                    return;
                }
                PopulateCompletedRow(data.Take(12).ToList());
            }
            catch (Exception)
            {
                SetSectionVisibility(DiscoverCompletedHeader, DiscoverCompletedRow, false);
            }
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
                    Type = (int)GetMangaType(item.manga_media_type_string),
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
            switch (type)
            {
                case "Manga":
                    return MangaType.Manga;
                case "Novel":
                    return MangaType.Novel;
                case "Doujinshi":
                    return MangaType.Doujinshi;
                case "OneShot":
                    return MangaType.OneShot;
                case "Manhwa":
                    return MangaType.Manhwa;
                case "Manhua":
                    return MangaType.Manhua;
            }
            return MangaType.Manga;
        }

        private async Task LoadSeasonalAsync()
        {
            try
            {
                var data = await new AnimeSeasonalQuery(GetCurrentSeason()).GetSeasonalAnime();
                if (data == null || data.Count == 0)
                {
                    SetSectionVisibility(DiscoverSeasonalHeader, DiscoverSeasonalRow, false);
                    return;
                }
                PopulateAnimeRow(DiscoverSeasonalRow, data.Take(12).ToList(), true);
            }
            catch (Exception)
            {
                SetSectionVisibility(DiscoverSeasonalHeader, DiscoverSeasonalRow, false);
            }
        }

        private async Task LoadTopAnimeAsync()
        {
            try
            {
                var data = await new AnimeTopQuery(TopAnimeType.General).GetTopAnimeData();
                if (data == null || data.Count == 0)
                {
                    SetSectionVisibility(DiscoverTopAnimeHeader, DiscoverTopAnimeRow, false);
                    return;
                }
                PopulateAnimeRow(DiscoverTopAnimeRow, data.Take(12).ToList(), true);
            }
            catch (Exception)
            {
                SetSectionVisibility(DiscoverTopAnimeHeader, DiscoverTopAnimeRow, false);
            }
        }

        private async Task LoadTopMangaAsync()
        {
            try
            {
                var data = await new AnimeTopQuery(MangaTopType.All).GetTopAnimeData();
                if (data == null || data.Count == 0)
                {
                    SetSectionVisibility(DiscoverTopMangaHeader, DiscoverTopMangaRow, false);
                    return;
                }
                PopulateAnimeRow(DiscoverTopMangaRow, data.Take(12).ToList(), false);
            }
            catch (Exception)
            {
                SetSectionVisibility(DiscoverTopMangaHeader, DiscoverTopMangaRow, false);
            }
        }

        private async Task LoadAdaptedAsync()
        {
            try
            {
                var data = await new AnimeAdaptedToAnimeQuery(MangaAdaptedType.AiringNow).GetAdaptedToAnimeData();
                if (data == null || data.Count == 0)
                {
                    SetSectionVisibility(DiscoverAdaptedHeader, DiscoverAdaptedRow, false);
                    return;
                }
                PopulateAnimeRow(DiscoverAdaptedRow, data.Take(12).ToList(), false);
            }
            catch (Exception)
            {
                SetSectionVisibility(DiscoverAdaptedHeader, DiscoverAdaptedRow, false);
            }
        }

        private async Task LoadNewsAsync()
        {
            try
            {
                var data = await new MalArticlesIndexQuery(ArticlePageWorkMode.News).GetArticlesIndex();
                if (data == null || data.Count == 0)
                {
                    SetSectionVisibility(DiscoverNewsHeader, DiscoverNewsRow, false);
                    return;
                }
                PopulateNewsRow(data.Take(10).ToList());
            }
            catch (Exception)
            {
                SetSectionVisibility(DiscoverNewsHeader, DiscoverNewsRow, false);
            }
        }

        private void PopulateAnimeRow(LinearLayout row, IEnumerable<SeasonalAnimeData> data, bool isAnime)
        {
            var cardWidth = (int)(MainActivity.CurrentContext.Resources.DisplayMetrics.Density >= 2 ? 160 : 170);
            var verticalMargin = DimensionsHelper.DpToPx(2);
            var horizontalMargin = DimensionsHelper.DpToPx(4);
            foreach (var item in data)
            {
                var model = new AnimeItemAbstraction(item, isAnime).ViewModel;
                var view = new AnimeGridItem(Activity);
                view.LayoutParameters = new LinearLayout.LayoutParams(cardWidth, ViewGroup.LayoutParams.WrapContent)
                {
                    TopMargin = verticalMargin,
                    BottomMargin = verticalMargin,
                    RightMargin = horizontalMargin
                };
                view.BindModel(model, false);
                row.AddView(view);
            }
        }

        private void PopulateWatchingRow(IList<AnimeItemAbstraction> data)
        {
            var cardWidth = (int)(MainActivity.CurrentContext.Resources.DisplayMetrics.Density >= 2 ? 160 : 170);
            var verticalMargin = DimensionsHelper.DpToPx(2);
            var horizontalMargin = DimensionsHelper.DpToPx(4);
            foreach (var item in data)
            {
                var view = new AnimeGridItem(Activity);
                view.LayoutParameters = new LinearLayout.LayoutParams(cardWidth, ViewGroup.LayoutParams.WrapContent)
                {
                    TopMargin = verticalMargin,
                    BottomMargin = verticalMargin,
                    RightMargin = horizontalMargin
                };
                var viewModel = item.ViewModel;
                viewModel.TimeTillNextAirCache = viewModel.GetTimeTillNextAir(JstTimeZone);
                view.BindModel(viewModel, false);
                DiscoverWatchingRow.AddView(view);
            }
        }

        private void PopulateReadingRow(IList<AnimeItemAbstraction> data)
        {
            var cardWidth = (int)(MainActivity.CurrentContext.Resources.DisplayMetrics.Density >= 2 ? 160 : 170);
            var verticalMargin = DimensionsHelper.DpToPx(2);
            var horizontalMargin = DimensionsHelper.DpToPx(4);
            foreach (var item in data)
            {
                var view = new AnimeGridItem(Activity);
                view.LayoutParameters = new LinearLayout.LayoutParams(cardWidth, ViewGroup.LayoutParams.WrapContent)
                {
                    TopMargin = verticalMargin,
                    BottomMargin = verticalMargin,
                    RightMargin = horizontalMargin
                };
                view.BindModel(item.ViewModel, false);
                DiscoverReadingRow.AddView(view);
            }
        }

        private void PopulateCompletedRow(IList<AnimeItemAbstraction> data)
        {
            var cardWidth = (int)(MainActivity.CurrentContext.Resources.DisplayMetrics.Density >= 2 ? 160 : 170);
            var verticalMargin = DimensionsHelper.DpToPx(2);
            var horizontalMargin = DimensionsHelper.DpToPx(4);
            foreach (var item in data)
            {
                var view = new AnimeGridItem(Activity);
                view.LayoutParameters = new LinearLayout.LayoutParams(cardWidth, ViewGroup.LayoutParams.WrapContent)
                {
                    TopMargin = verticalMargin,
                    BottomMargin = verticalMargin,
                    RightMargin = horizontalMargin
                };
                view.BindModel(item.ViewModel, false);
                DiscoverCompletedRow.AddView(view);
            }
        }

        private void PopulateNewsRow(IList<MalNewsUnitModel> data)
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
                    view.FindViewById<ImageViewAsync>(Resource.Id.ArticlesPageItemImage).Into(item.ImgUrl);
                }
                catch (Exception)
                {
                    // network on main thread
                }
                view.SetOnClickListener(new OnClickListener(v =>
                {
                    ViewModelLocator.MalArticles.PendingArticle = item;
                    NavigateTo(PageIndex.PageNews, MalArticlesPageNavigationArgs.News);
                }));
                DiscoverNewsRow.AddView(view);
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
        private TextView _discoverSeasonalHeader;
        private TextView _discoverTopAnimeHeader;
        private TextView _discoverTopMangaHeader;
        private TextView _discoverAdaptedHeader;
        private TextView _discoverNewsHeader;
        private TextView _discoverWatchingHeader;
        private TextView _discoverReadingHeader;
        private TextView _discoverCompletedHeader;
        private LinearLayout _discoverSeasonalRow;
        private LinearLayout _discoverTopAnimeRow;
        private LinearLayout _discoverTopMangaRow;
        private LinearLayout _discoverAdaptedRow;
        private LinearLayout _discoverNewsRow;
        private LinearLayout _discoverWatchingRow;
        private LinearLayout _discoverReadingRow;
        private LinearLayout _discoverCompletedRow;

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
        public TextView DiscoverSeasonalHeader => GetView(ref _discoverSeasonalHeader, Resource.Id.DiscoverSeasonalHeader);
        public TextView DiscoverTopAnimeHeader => GetView(ref _discoverTopAnimeHeader, Resource.Id.DiscoverTopAnimeHeader);
        public TextView DiscoverTopMangaHeader => GetView(ref _discoverTopMangaHeader, Resource.Id.DiscoverTopMangaHeader);
        public TextView DiscoverAdaptedHeader => GetView(ref _discoverAdaptedHeader, Resource.Id.DiscoverAdaptedHeader);
        public TextView DiscoverNewsHeader => GetView(ref _discoverNewsHeader, Resource.Id.DiscoverNewsHeader);
        public TextView DiscoverWatchingHeader => GetView(ref _discoverWatchingHeader, Resource.Id.DiscoverWatchingHeader);
        public TextView DiscoverReadingHeader => GetView(ref _discoverReadingHeader, Resource.Id.DiscoverReadingHeader);
        public TextView DiscoverCompletedHeader => GetView(ref _discoverCompletedHeader, Resource.Id.DiscoverCompletedHeader);
        public LinearLayout DiscoverSeasonalRow => GetView(ref _discoverSeasonalRow, Resource.Id.DiscoverSeasonalRow);
        public LinearLayout DiscoverTopAnimeRow => GetView(ref _discoverTopAnimeRow, Resource.Id.DiscoverTopAnimeRow);
        public LinearLayout DiscoverTopMangaRow => GetView(ref _discoverTopMangaRow, Resource.Id.DiscoverTopMangaRow);
        public LinearLayout DiscoverAdaptedRow => GetView(ref _discoverAdaptedRow, Resource.Id.DiscoverAdaptedRow);
        public LinearLayout DiscoverNewsRow => GetView(ref _discoverNewsRow, Resource.Id.DiscoverNewsRow);
        public LinearLayout DiscoverWatchingRow => GetView(ref _discoverWatchingRow, Resource.Id.DiscoverWatchingRow);
        public LinearLayout DiscoverReadingRow => GetView(ref _discoverReadingRow, Resource.Id.DiscoverReadingRow);
        public LinearLayout DiscoverCompletedRow => GetView(ref _discoverCompletedRow, Resource.Id.DiscoverCompletedRow);

        #endregion
    }
}

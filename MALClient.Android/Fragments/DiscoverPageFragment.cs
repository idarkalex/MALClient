using System;
using System.Collections.Generic;
using System.Linq;
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
using MALClient.Models.Models.MalSpecific;
using MALClient.XShared.Comm.Anime;
using MALClient.XShared.Comm.Articles;
using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALClient.Android.Fragments
{
    public class DiscoverPageFragment : MalFragmentBase
    {
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
            LoadSections();
        }

        private async void LoadSections()
        {
            try
            {
                await Task.WhenAll(
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
            var cardWidth = (int)(MainActivity.CurrentContext.Resources.DisplayMetrics.Density >= 2 ? 190 : 200);
            var verticalMargin = DimensionsHelper.DpToPx(2);
            var horizontalMargin = DimensionsHelper.DpToPx(4);
            foreach (var item in data)
            {
                var model = new AnimeItemAbstraction(item, isAnime).ViewModel;
                var view = new AnimeGridItem(Activity, false, null);
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
                    ViewModelLocator.GeneralMain.Navigate(PageIndex.PageNews, MalArticlesPageNavigationArgs.News)));
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
        private LinearLayout _discoverSeasonalRow;
        private LinearLayout _discoverTopAnimeRow;
        private LinearLayout _discoverTopMangaRow;
        private LinearLayout _discoverAdaptedRow;
        private LinearLayout _discoverNewsRow;

        public TextView DiscoverSeasonalSeeAll => _discoverSeasonalSeeAll ?? (_discoverSeasonalSeeAll = FindViewById<TextView>(Resource.Id.DiscoverSeasonalSeeAll));
        public TextView DiscoverTopAnimeSeeAll => _discoverTopAnimeSeeAll ?? (_discoverTopAnimeSeeAll = FindViewById<TextView>(Resource.Id.DiscoverTopAnimeSeeAll));
        public TextView DiscoverTopMangaSeeAll => _discoverTopMangaSeeAll ?? (_discoverTopMangaSeeAll = FindViewById<TextView>(Resource.Id.DiscoverTopMangaSeeAll));
        public TextView DiscoverAdaptedSeeAll => _discoverAdaptedSeeAll ?? (_discoverAdaptedSeeAll = FindViewById<TextView>(Resource.Id.DiscoverAdaptedSeeAll));
        public TextView DiscoverNewsSeeAll => _discoverNewsSeeAll ?? (_discoverNewsSeeAll = FindViewById<TextView>(Resource.Id.DiscoverNewsSeeAll));
        public TextView DiscoverSeasonalHeader => _discoverSeasonalHeader ?? (_discoverSeasonalHeader = FindViewById<TextView>(Resource.Id.DiscoverSeasonalHeader));
        public TextView DiscoverTopAnimeHeader => _discoverTopAnimeHeader ?? (_discoverTopAnimeHeader = FindViewById<TextView>(Resource.Id.DiscoverTopAnimeHeader));
        public TextView DiscoverTopMangaHeader => _discoverTopMangaHeader ?? (_discoverTopMangaHeader = FindViewById<TextView>(Resource.Id.DiscoverTopMangaHeader));
        public TextView DiscoverAdaptedHeader => _discoverAdaptedHeader ?? (_discoverAdaptedHeader = FindViewById<TextView>(Resource.Id.DiscoverAdaptedHeader));
        public TextView DiscoverNewsHeader => _discoverNewsHeader ?? (_discoverNewsHeader = FindViewById<TextView>(Resource.Id.DiscoverNewsHeader));
        public LinearLayout DiscoverSeasonalRow => _discoverSeasonalRow ?? (_discoverSeasonalRow = FindViewById<LinearLayout>(Resource.Id.DiscoverSeasonalRow));
        public LinearLayout DiscoverTopAnimeRow => _discoverTopAnimeRow ?? (_discoverTopAnimeRow = FindViewById<LinearLayout>(Resource.Id.DiscoverTopAnimeRow));
        public LinearLayout DiscoverTopMangaRow => _discoverTopMangaRow ?? (_discoverTopMangaRow = FindViewById<LinearLayout>(Resource.Id.DiscoverTopMangaRow));
        public LinearLayout DiscoverAdaptedRow => _discoverAdaptedRow ?? (_discoverAdaptedRow = FindViewById<LinearLayout>(Resource.Id.DiscoverAdaptedRow));
        public LinearLayout DiscoverNewsRow => _discoverNewsRow ?? (_discoverNewsRow = FindViewById<LinearLayout>(Resource.Id.DiscoverNewsRow));

        #endregion
    }
}

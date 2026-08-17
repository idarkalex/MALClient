using System;
using System.Threading.Tasks;
using Android.OS;
using Android.Views;
using Android.Widget;
using FFImageLoading.Views;
using MALClient.Android.Activities;
using MALClient.Android.Listeners;
using MALClient.Models.Enums;
using MALClient.XShared.Comm.Anime;
using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALClient.Android.Fragments
{
    public class MorePageFragment : MalFragmentBase
    {
        public override int LayoutResourceId => Resource.Layout.MorePage;

        protected override void Init(Bundle savedInstanceState) { }

        protected override void InitBindings()
        {
            MorePageProfileHeader.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageProfile,
                    new ProfilePageNavigationArgs { TargetUser = Credentials.UserName })));

            MorePageAnimeListItem.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageAnimeList,
                    new AnimeListPageNavigationArgs(0, AnimeListWorkModes.Anime))));

            MorePageSeasonalItem.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageSeasonal, AnimeListPageNavigationArgs.Seasonal)));

            MorePageTopAnimeItem.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageTopAnime,
                    AnimeListPageNavigationArgs.TopAnime(TopAnimeType.General))));

            MorePageSearchItem.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageSearch, new SearchPageNavigationArgs())));

            MorePageRecommendationsItem.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageRecomendations, null)));

            MorePageCalendarItem.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageCalendar, null)));

            MorePageMangaListItem.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageMangaList,
                    new AnimeListPageNavigationArgs(0, AnimeListWorkModes.Manga))));

            MorePageTopMangaItem.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageTopManga, AnimeListPageNavigationArgs.TopManga)));

            MorePageAdaptedItem.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageMangaAdapted,
                    AnimeListPageNavigationArgs.MangaAdapted(MangaAdaptedType.AiringNow))));

            MorePageArticlesItem.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageArticles, MalArticlesPageNavigationArgs.Articles)));

            MorePageVideosItem.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PagePopularVideos, null)));

            MorePageForumsItem.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageForumIndex, null)));

            MorePageSettingsItem.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageSettings, null)));

            if (Credentials.Authenticated)
            {
                MorePageProfileUsername.Text = Credentials.UserName;
                MorePageProfileCompleted.Visibility = ViewStates.Gone;
                LoadProfileDataAsync();
            }
            else
            {
                MorePageProfileUsername.Text = "Log in";
                MorePageProfileCompleted.Visibility = ViewStates.Gone;
            }
        }

        private async void LoadProfileDataAsync()
        {
            try
            {
                var data = await DataCache.RetrieveProfileData(Credentials.UserName);
                if (data?.User?.ImgUrl != null && IsAdded)
                {
                    MorePageProfileImage.Into(data.User.ImgUrl);
                    if (data.AnimeCompleted > 0 || data.MangaCompleted > 0)
                    {
                        MorePageProfileCompleted.Text = $"{data.AnimeCompleted} Completed";
                        MorePageProfileCompleted.Visibility = ViewStates.Visible;
                    }
                }
            }
            catch (Exception)
            {
                // Profile not cached yet, that's ok
            }
        }

        private void NavigateTo(PageIndex page, object args)
        {
            ViewModelLocator.GeneralMain.Navigate(page, args);
        }

        #region Views

        private LinearLayout _morePageProfileHeader;
        private ImageViewAsync _morePageProfileImage;
        private TextView _morePageProfileUsername;
        private TextView _morePageProfileCompleted;
        private LinearLayout _morePageAnimeListItem;
        private LinearLayout _morePageSeasonalItem;
        private LinearLayout _morePageTopAnimeItem;
        private LinearLayout _morePageSearchItem;
        private LinearLayout _morePageRecommendationsItem;
        private LinearLayout _morePageCalendarItem;
        private LinearLayout _morePageMangaListItem;
        private LinearLayout _morePageTopMangaItem;
        private LinearLayout _morePageAdaptedItem;
        private LinearLayout _morePageArticlesItem;
        private LinearLayout _morePageVideosItem;
        private LinearLayout _morePageForumsItem;
        private LinearLayout _morePageSettingsItem;

        public LinearLayout MorePageProfileHeader => GetView(ref _morePageProfileHeader, Resource.Id.MorePageProfileHeader);
        public ImageViewAsync MorePageProfileImage => GetView(ref _morePageProfileImage, Resource.Id.MorePageProfileImage);
        public TextView MorePageProfileUsername => GetView(ref _morePageProfileUsername, Resource.Id.MorePageProfileUsername);
        public TextView MorePageProfileCompleted => GetView(ref _morePageProfileCompleted, Resource.Id.MorePageProfileCompleted);
        public LinearLayout MorePageAnimeListItem => GetView(ref _morePageAnimeListItem, Resource.Id.MorePageAnimeListItem);
        public LinearLayout MorePageSeasonalItem => GetView(ref _morePageSeasonalItem, Resource.Id.MorePageSeasonalItem);
        public LinearLayout MorePageTopAnimeItem => GetView(ref _morePageTopAnimeItem, Resource.Id.MorePageTopAnimeItem);
        public LinearLayout MorePageSearchItem => GetView(ref _morePageSearchItem, Resource.Id.MorePageSearchItem);
        public LinearLayout MorePageRecommendationsItem => GetView(ref _morePageRecommendationsItem, Resource.Id.MorePageRecommendationsItem);
        public LinearLayout MorePageCalendarItem => GetView(ref _morePageCalendarItem, Resource.Id.MorePageCalendarItem);
        public LinearLayout MorePageMangaListItem => GetView(ref _morePageMangaListItem, Resource.Id.MorePageMangaListItem);
        public LinearLayout MorePageTopMangaItem => GetView(ref _morePageTopMangaItem, Resource.Id.MorePageTopMangaItem);
        public LinearLayout MorePageAdaptedItem => GetView(ref _morePageAdaptedItem, Resource.Id.MorePageAdaptedItem);
        public LinearLayout MorePageArticlesItem => GetView(ref _morePageArticlesItem, Resource.Id.MorePageArticlesItem);
        public LinearLayout MorePageVideosItem => GetView(ref _morePageVideosItem, Resource.Id.MorePageVideosItem);
        public LinearLayout MorePageForumsItem => GetView(ref _morePageForumsItem, Resource.Id.MorePageForumsItem);
        public LinearLayout MorePageSettingsItem => GetView(ref _morePageSettingsItem, Resource.Id.MorePageSettingsItem);

        #endregion
    }
}

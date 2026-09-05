using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using GalaSoft.MvvmLight.Helpers;
using MALClient.Android.BindingConverters;
using MALClient.Android.CollectionAdapters;
using MALClient.Android.Utilities;
using MALClient.Models.Enums;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALClient.Android.Fragments.ProfilePageFragments
{
    public class ProfilePageRecentUpdatesFragment : MalFragmentBase
    {
        private readonly ProfilePageViewModel ViewModel = ViewModelLocator.ProfilePage;

        protected override void Init(Bundle savedInstanceState)
        {
            
        }

        protected override void InitBindings()
        {

            Bindings.Add(this.SetBinding(() => ViewModel.RecentAnime)
                .WhenSourceChanges(() =>
                {
                    if (ViewModel.RecentAnime?.Any() ?? false)
                        ProfilePageRecentUpdatesTabAnimeList.SetAnimeListAdapter(Context, ViewModel.RecentAnime,
                            AnimeListDisplayModes.IndefiniteList, OnItemClickAction);
                    else
                        ProfilePageRecentUpdatesTabAnimeList.RemoveAllViews();
                }));

            Bindings.Add(this.SetBinding(() => ViewModel.RecentManga)
                .WhenSourceChanges(() =>
                {
                    if (ViewModel.RecentManga?.Any() ?? false)
                        ProfilePageRecentUpdatesTabMangaList.SetAnimeListAdapter(Context, ViewModel.RecentManga,
                            AnimeListDisplayModes.IndefiniteList, OnItemClickAction);
                    else
                        ProfilePageRecentUpdatesTabMangaList.RemoveAllViews();
                }));

            Bindings.Add(
                this.SetBinding(() => ViewModel.EmptyRecentAnimeNoticeVisibility,
                        () => ProfilePageRecentUpdatesTabAnimeListEmptyNotice.Visibility)
                    .ConvertSourceToTarget(Converters.BoolToVisibility));


            Bindings.Add(
                this.SetBinding(() => ViewModel.EmptyRecentMangaNoticeVisibility,
                        () => ProfilePageRecentUpdatesTabMangaListEmptyNotice.Visibility)
                    .ConvertSourceToTarget(Converters.BoolToVisibility));
        }

        private void OnItemClickAction(AnimeItemViewModel animeItemViewModel)
        {
            ViewModel.TemporarilySelectedAnimeItem = animeItemViewModel;
        }

        public override int LayoutResourceId => Resource.Layout.ProfilePageRecentUpdatesTab;

        public override void OnPause()
        {
            try
            {
                ScrollStateHelper.SaveScrollY(ProfilePageRecentUpdatesTabScroll?.ScrollY ?? 0, FragmentUiState.Profile, "Recent");
            }
            catch { }
            base.OnPause();
        }

        public override void OnResume()
        {
            base.OnResume();
            try
            {
                var y = ScrollStateHelper.RestoreScrollY(FragmentUiState.Profile, "Recent");
                var scroll = ProfilePageRecentUpdatesTabScroll;
                if (y > 0 && scroll != null)
                    scroll.Post(() =>
                    {
                        try { scroll.ScrollTo(0, y); } catch { }
                    });
            }
            catch { }
        }

        #region Views

        private ScrollView _profilePageRecentUpdatesTabScroll;
        private LinearLayout _profilePageRecentUpdatesTabAnimeList;
        private RelativeLayout _profilePageRecentUpdatesTabAnimeListEmptyNotice;
        private RelativeLayout _profilePageRecentUpdatesTabMangaListEmptyNotice;
        private LinearLayout _profilePageRecentUpdatesTabMangaList;

        public ScrollView ProfilePageRecentUpdatesTabScroll => GetView(ref _profilePageRecentUpdatesTabScroll, Resource.Id.ProfilePageRecentUpdatesTabScroll);
        public LinearLayout ProfilePageRecentUpdatesTabAnimeList => GetView(ref _profilePageRecentUpdatesTabAnimeList, Resource.Id.ProfilePageRecentUpdatesTabAnimeList);

        public RelativeLayout ProfilePageRecentUpdatesTabAnimeListEmptyNotice => GetView(ref _profilePageRecentUpdatesTabAnimeListEmptyNotice, Resource.Id.ProfilePageRecentUpdatesTabAnimeListEmptyNotice);

        public RelativeLayout ProfilePageRecentUpdatesTabMangaListEmptyNotice => GetView(ref _profilePageRecentUpdatesTabMangaListEmptyNotice, Resource.Id.ProfilePageRecentUpdatesTabMangaListEmptyNotice);

        public LinearLayout ProfilePageRecentUpdatesTabMangaList => GetView(ref _profilePageRecentUpdatesTabMangaList, Resource.Id.ProfilePageRecentUpdatesTabMangaList);


        #endregion
    }
}
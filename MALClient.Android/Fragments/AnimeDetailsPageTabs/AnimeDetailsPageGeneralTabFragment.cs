using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.Widget;
using Android.Views;
using Android.Widget;
using GalaSoft.MvvmLight.Helpers;
using MALClient.Android.BindingConverters;
using MALClient.Android.Resources;
using MALClient.Android.Utilities;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Details;
using MALClient.XShared.ViewModels.Main;


namespace MALClient.Android.Fragments.AnimeDetailsPageTabs
{
    public class AnimeDetailsPageGeneralTabFragment : MalFragmentBase
    {
        private AnimeDetailsPageViewModel ViewModel;

        private AnimeDetailsPageGeneralTabFragment()
        {
            ViewModel = ViewModelLocator.AnimeDetails;
        }

        public override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            RetainInstance = true;
        }

        protected override void Init(Bundle savedInstanceState)
        {
        }

        protected override void InitBindings()
        {
            Bindings.Add(this.SetBinding(() => ViewModel.LoadingGlobal).WhenSourceChanges(() =>
            {
                if (!ViewModel.LoadingGlobal)
                    UpdateCards();
            }));

            Bindings.Add(this.SetBinding(() => ViewModel.LoadingDetails).WhenSourceChanges(() =>
            {
                if (!ViewModel.LoadingDetails)
                    UpdateCards();
            }));

            Bindings.Add(this.SetBinding(() => ViewModel.AddAnimeVisibility)
                .WhenSourceChanges(() =>
                {
                }));

            Bindings.Add(this.SetBinding(() => ViewModel.EndDateTimeOffset).WhenSourceChanges(() =>
            {
            }));
        }

        private void UpdateCards()
        {
            try
            {
                (RootView?.Parent as UserControls.HeightAdjustingViewPager)?.SetTabHeightForCurrentView(RootView);
                AnimeDetailsPageGeneralTabFragmentEpisodesLabel.Text =
                    string.IsNullOrEmpty(ViewModel.GeneralRank) ? "N/A" : ViewModel.GeneralRank;

                AnimeDetailsPageGeneralTabFragmentScore.Text =
                    string.IsNullOrEmpty(ViewModel.GeneralPopularity) ? "N/A" : ViewModel.GeneralPopularity;

                AnimeDetailsPageGeneralTabFragmentType.Text =
                    string.IsNullOrEmpty(ViewModel.GeneralStudios) ? "Unknown" : ViewModel.GeneralStudios;

                // Favorites / Members / Premiered / Duration / Rating: official API + Tenrai enrichment
                AnimeDetailsPageGeneralTabFragmentFavorites.Text =
                    string.IsNullOrEmpty(ViewModel.GeneralFavorites) ? "N/A" : ViewModel.GeneralFavorites;
                AnimeDetailsPageGeneralTabFragmentMembers.Text =
                    string.IsNullOrEmpty(ViewModel.GeneralMembers) ? "N/A" : ViewModel.GeneralMembers;
                AnimeDetailsPageGeneralTabFragmentPremiered.Text =
                    string.IsNullOrEmpty(ViewModel.GeneralSeason) ? "N/A" : ViewModel.GeneralSeason;

                // Synopsis
                if (!string.IsNullOrEmpty(ViewModel.Synopsis))
                {
                    AnimeDetailsPageGeneralTabFragmentSynopsis.Text = ViewModel.Synopsis;
                    AnimeDetailsPageGeneralTabFragmentSynopsis.Gravity = GravityFlags.Left;
                }
                else
                {
                    AnimeDetailsPageGeneralTabFragmentSynopsis.Text = "Synopsis unavailable...";
                    AnimeDetailsPageGeneralTabFragmentSynopsis.Gravity = GravityFlags.CenterHorizontal;
                }
            }
            catch (Exception)
            {
                //data loading has failed
            }
        }


        public override int LayoutResourceId => Resource.Layout.AnimeDetailsPageGeneralTab;

        public static AnimeDetailsPageGeneralTabFragment Instance => new AnimeDetailsPageGeneralTabFragment();

        public override void OnPause()
        {
            try
            {
                ScrollStateHelper.SaveScrollY(AnimeDetailsPageGeneralTabScroll?.ScrollY ?? 0, FragmentUiState.AnimeDetails, "General");
            }
            catch { }
            base.OnPause();
        }

        public override void OnResume()
        {
            base.OnResume();
            try
            {
                var y = ScrollStateHelper.RestoreScrollY(FragmentUiState.AnimeDetails, "General");
                var scroll = AnimeDetailsPageGeneralTabScroll;
                if (y > 0 && scroll != null)
                    scroll.Post(() =>
                    {
                        try { scroll.ScrollTo(0, y); } catch { }
                    });
            }
            catch { }
        }

        #region Views

        private NestedScrollView _animeDetailsPageGeneralTabScroll;
        private TextView _animeDetailsPageGeneralTabFragmentEpisodesLabel;
        private TextView _animeDetailsPageGeneralTabFragmentEpisodes;
        private TextView _animeDetailsPageGeneralTabFragmentScore;
        private LinearLayout _animeDetailsPageGeneralTabFragmentScoreHiddenLabel;
        private TextView _animeDetailsPageGeneralTabFragmentStart;
        private TextView _animeDetailsPageGeneralTabFragmentMyStart;
        private FrameLayout _animeDetailsPageGeneralTabFragmentMyStartButton;
        private TextView _animeDetailsPageGeneralTabFragmentType;
        private TextView _animeDetailsPageGeneralTabFragmentStatus;
        private TextView _animeDetailsPageGeneralTabFragmentEnd;
        private TextView _animeDetailsPageGeneralTabFragmentMyEnd;
        private FrameLayout _animeDetailsPageGeneralTabFragmentMyEndButton;
        private TextView _animeDetailsPageGeneralTabFragmentSynopsis;
        private TextView _animeDetailsPageGeneralTabFragmentFavorites;
        private TextView _animeDetailsPageGeneralTabFragmentMembers;
        private TextView _animeDetailsPageGeneralTabFragmentPremiered;

        public NestedScrollView AnimeDetailsPageGeneralTabScroll => GetView(ref _animeDetailsPageGeneralTabScroll, Resource.Id.AnimeDetailsPageGeneralTabScroll);
        public TextView AnimeDetailsPageGeneralTabFragmentEpisodesLabel => GetView(ref _animeDetailsPageGeneralTabFragmentEpisodesLabel, Resource.Id.AnimeDetailsPageGeneralTabFragmentEpisodesLabel);
        public TextView AnimeDetailsPageGeneralTabFragmentEpisodes => GetView(ref _animeDetailsPageGeneralTabFragmentEpisodes, Resource.Id.AnimeDetailsPageGeneralTabFragmentEpisodes);
        public TextView AnimeDetailsPageGeneralTabFragmentScore => GetView(ref _animeDetailsPageGeneralTabFragmentScore, Resource.Id.AnimeDetailsPageGeneralTabFragmentScore);
        public LinearLayout AnimeDetailsPageGeneralTabFragmentScoreHiddenLabel => GetView(ref _animeDetailsPageGeneralTabFragmentScoreHiddenLabel, Resource.Id.AnimeDetailsPageGeneralTabFragmentScoreHiddenLabel);
        public TextView AnimeDetailsPageGeneralTabFragmentStart => GetView(ref _animeDetailsPageGeneralTabFragmentStart, Resource.Id.AnimeDetailsPageGeneralTabFragmentStart);
        public TextView AnimeDetailsPageGeneralTabFragmentMyStart => GetView(ref _animeDetailsPageGeneralTabFragmentMyStart, Resource.Id.AnimeDetailsPageGeneralTabFragmentMyStart);
        public FrameLayout AnimeDetailsPageGeneralTabFragmentMyStartButton => GetView(ref _animeDetailsPageGeneralTabFragmentMyStartButton, Resource.Id.AnimeDetailsPageGeneralTabFragmentMyStartButton);
        public TextView AnimeDetailsPageGeneralTabFragmentType => GetView(ref _animeDetailsPageGeneralTabFragmentType, Resource.Id.AnimeDetailsPageGeneralTabFragmentType);
        public TextView AnimeDetailsPageGeneralTabFragmentStatus => GetView(ref _animeDetailsPageGeneralTabFragmentStatus, Resource.Id.AnimeDetailsPageGeneralTabFragmentStatus);
        public TextView AnimeDetailsPageGeneralTabFragmentEnd => GetView(ref _animeDetailsPageGeneralTabFragmentEnd, Resource.Id.AnimeDetailsPageGeneralTabFragmentEnd);
        public TextView AnimeDetailsPageGeneralTabFragmentMyEnd => GetView(ref _animeDetailsPageGeneralTabFragmentMyEnd, Resource.Id.AnimeDetailsPageGeneralTabFragmentMyEnd);
        public FrameLayout AnimeDetailsPageGeneralTabFragmentMyEndButton => GetView(ref _animeDetailsPageGeneralTabFragmentMyEndButton, Resource.Id.AnimeDetailsPageGeneralTabFragmentMyEndButton);
        public TextView AnimeDetailsPageGeneralTabFragmentSynopsis => GetView(ref _animeDetailsPageGeneralTabFragmentSynopsis, Resource.Id.AnimeDetailsPageGeneralTabFragmentSynopsis);
        public TextView AnimeDetailsPageGeneralTabFragmentFavorites => GetView(ref _animeDetailsPageGeneralTabFragmentFavorites, Resource.Id.AnimeDetailsPageGeneralTabFragmentFavorites);
        public TextView AnimeDetailsPageGeneralTabFragmentMembers => GetView(ref _animeDetailsPageGeneralTabFragmentMembers, Resource.Id.AnimeDetailsPageGeneralTabFragmentMembers);
        public TextView AnimeDetailsPageGeneralTabFragmentPremiered => GetView(ref _animeDetailsPageGeneralTabFragmentPremiered, Resource.Id.AnimeDetailsPageGeneralTabFragmentPremiered);

        #endregion
    }
}

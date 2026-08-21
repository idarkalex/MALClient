using System;
using System.Collections.Generic;
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
using MALClient.Android.Resources;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Details;


namespace MALClient.Android.Fragments.AnimeDetailsPageTabs
{
    public class AnimeDetailsPageGeneralTabFragment : MalFragmentBase
    {
        private AnimeDetailsPageViewModel ViewModel;

        private AnimeDetailsPageGeneralTabFragment()
        {
            ViewModel = ViewModelLocator.AnimeDetails;
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
                // Rank: official API value first, scraped stats as fallback
                var rankText = !string.IsNullOrEmpty(ViewModel.GeneralRank) ? ViewModel.GeneralRank : null;
                if (string.IsNullOrEmpty(rankText))
                {
                    var rankEntry = ViewModel.Stats.FirstOrDefault(s => s.Item1 == "Rank");
                    rankText = rankEntry?.Item2?.Trim();
                }
                AnimeDetailsPageGeneralTabFragmentEpisodesLabel.Text = string.IsNullOrEmpty(rankText) ? "N/A" : rankText;

                // Popularity: official API value first, scraped stats as fallback
                var popText = !string.IsNullOrEmpty(ViewModel.GeneralPopularity) ? ViewModel.GeneralPopularity : null;
                if (string.IsNullOrEmpty(popText))
                {
                    var popEntry = ViewModel.Stats.FirstOrDefault(s => s.Item1 == "Popularity");
                    popText = popEntry?.Item2?.Trim();
                }
                AnimeDetailsPageGeneralTabFragmentScore.Text = string.IsNullOrEmpty(popText) ? "N/A" : popText;

                // Studios: official API value first, scraped info as fallback
                var studioText = !string.IsNullOrEmpty(ViewModel.GeneralStudios) ? ViewModel.GeneralStudios : null;
                if (string.IsNullOrEmpty(studioText))
                {
                    var studioEntry = ViewModel.Information.FirstOrDefault(s => s.Item1 == "Studios");
                    studioText = studioEntry?.Item2?.Trim();
                }
                AnimeDetailsPageGeneralTabFragmentType.Text = string.IsNullOrEmpty(studioText) ? "Unknown" : studioText;

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

        #region Views

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

        #endregion
    }
}
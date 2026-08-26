using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Support.V7.Widget;
using Android.Text;
using Android.Util;
using Android.Views;
using Android.Widget;
using FFImageLoading;
using FFImageLoading.Views;
using GalaSoft.MvvmLight.Helpers;
using MALClient.Android.Activities;
using MALClient.Android.AoLibsCompat;
using MALClient.Android.BindingConverters;
using MALClient.Android.Listeners;
using MALClient.Android.Resources;
using MALClient.Android.Utilities.ImageLoading;
using MALClient.Models.Models.AnimeScrapped;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Details;
using Debug = System.Diagnostics.Debug;
using Orientation = Android.Content.Res.Orientation;

namespace MALClient.Android.Fragments.AnimeDetailsPageTabs
{
    public class AnimeDetailsPageRecomsTabFragment : MalFragmentBase
    {
        private AnimeDetailsPageViewModel ViewModel;

        private AnimeDetailsPageRecomsTabFragment()
        {
            ViewModel = ViewModelLocator.AnimeDetails;
        }

        protected override void Init(Bundle savedInstanceState)
        {

        }

        protected override void InitBindings()
        {
            Bindings.Add(
                this.SetBinding(() => ViewModel.LoadingRecommendations,
                    () => AnimeDetailsPageRecomTabLoadingOverlay.Visibility).ConvertSourceToTarget(Converters.BoolToVisibility));

            Bindings.Add(
                this.SetBinding(() => ViewModel.LoadingRecommendations).WhenSourceChanges(() =>
                {
                    if (ViewModel.LoadingRecommendations)
                    {
                        AnimeDetailsPageRecomTabsList.SetAdapter(null);
                    }
                    else
                    {
                        if (ViewModel.Recommendations == null || !ViewModel.Recommendations.Any())
                        {
                            AnimeDetailsPageRecomTabsList.SetAdapter(null);
                            return;
                        }
                        AnimeDetailsPageRecomTabsList.SetAdapter(
                            new ObservableRecyclerAdapter<DirectRecommendationData, RecomHolder>(
                                ViewModel.Recommendations, BindRecom, LayoutInflater, Resource.Layout.AnimeRecomItem));
                    }
                }));

            Bindings.Add(
                this.SetBinding(() => ViewModel.NoRecommDataNoticeVisibility,
                    () => AnimeDetailsPageReviewsTabEmptyNotice.Visibility)
                    .ConvertSourceToTarget(Converters.BoolToVisibility));

            AnimeDetailsPageRecomTabsList.SetLayoutManager(new LinearLayoutManager(Activity));
            AnimeDetailsPageRecomTabsList.AddOnScrollListener(new CustomScrollListener());

            SetUpForOrientation(Activity.Resources.Configuration.Orientation);
        }

        private void BindRecom(DirectRecommendationData data, RecomHolder holder, int position)
        {
            holder.ShowTitle.Text = data.Title;
            holder.ShowType.Text = data.Type.ToString();

            var txt = holder.RecomContent;
            var txtOverflow = holder.RecomContentOverflow;
            txtOverflow.Text = string.Empty;
            txtOverflow.Visibility = ViewStates.Gone;
            txt.Text = data.Description;
            txt.Post(() =>
            {
                try
                {
                    if (txt.LineCount < 11)
                        return;

                    var ellipsis = txt.Layout.GetEllipsisStart(10);
                    if (ellipsis != -1)
                    {
                        var chars = txt.Layout.GetLineEnd(9) + ellipsis;
                        int lastSpaceIndex = 0;
                        for (int j = chars - 5; j > 0; j--)
                        {
                            if (data.Description[j] == ' ')
                            {
                                lastSpaceIndex = j;
                                break;
                            }
                        }
                        if (lastSpaceIndex == 0)
                            return;
                        txt.Text = data.Description.Substring(0, lastSpaceIndex);
                        txtOverflow.Text = data.Description.Substring(lastSpaceIndex + 1);
                        txtOverflow.Visibility = ViewStates.Visible;
                    }
                }
                catch (Exception)
                {
                }
            });

            var img = holder.Image;
            if (!img.IntoIfLoaded(data.ImageUrl))
                img.Visibility = ViewStates.Invisible;

            holder.ItemView.Click -= OnRecomClick;
            holder.ItemView.Tag = data.Wrap();
            holder.ItemView.Click += OnRecomClick;
        }

        private void OnRecomClick(object sender, EventArgs e)
        {
            var view = sender as View;
            var tag = view?.Tag?.Unwrap<DirectRecommendationData>();
            if (tag != null)
                ViewModel.NavigateDetailsCommand.Execute(tag);
        }

        private void SetUpForOrientation(Orientation orientation)
        {
            ViewGroup.LayoutParams param;
            switch (orientation)
            {
                case Orientation.Landscape:
                    param = RootView.LayoutParameters;
                    param.Height = ViewGroup.LayoutParams.WrapContent;
                    RootView.LayoutParameters = param;
                    break;
                case Orientation.Portrait:
                case Orientation.Square:
                case Orientation.Undefined:
                    param = RootView.LayoutParameters;
                    param.Height = ViewGroup.LayoutParams.MatchParent;
                    RootView.LayoutParameters = param;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(orientation), orientation, null);
            }
        }

        public override void OnConfigurationChanged(Configuration newConfig)
        {
            base.OnConfigurationChanged(newConfig);
        }

        public static AnimeDetailsPageRecomsTabFragment Instance => new AnimeDetailsPageRecomsTabFragment();

        public override int LayoutResourceId => Resource.Layout.AnimeDetailsPageRecomsTab;

        #region Views

        private RecyclerView _animeDetailsPageRecomTabsList;
        private TextView _animeDetailsPageReviewsTabEmptyNotice;
        private RelativeLayout _animeDetailsPageRecomTabLoadingOverlay;

        public RecyclerView AnimeDetailsPageRecomTabsList => GetView(ref _animeDetailsPageRecomTabsList, Resource.Id.AnimeDetailsPageRecomTabsList);

        public TextView AnimeDetailsPageReviewsTabEmptyNotice => GetView(ref _animeDetailsPageReviewsTabEmptyNotice, Resource.Id.AnimeDetailsPageReviewsTabEmptyNotice);

        public RelativeLayout AnimeDetailsPageRecomTabLoadingOverlay => GetView(ref _animeDetailsPageRecomTabLoadingOverlay, Resource.Id.AnimeDetailsPageRecomTabLoadingOverlay);

        #endregion

        class RecomHolder : RecyclerView.ViewHolder
        {
            private readonly View _view;

            public RecomHolder(View view) : base(view)
            {
                _view = view;
            }

            private TextView _showTitle;
            private TextView _showType;
            private TextView _recomContent;
            private TextView _recomContentOverflow;
            private ImageViewAsync _image;

            public TextView ShowTitle => _showTitle ?? (_showTitle = _view.FindViewById<TextView>(Resource.Id.AnimeRecomItemShowTitle));
            public TextView ShowType => _showType ?? (_showType = _view.FindViewById<TextView>(Resource.Id.AnimeRecomItemShowType));
            public TextView RecomContent => _recomContent ?? (_recomContent = _view.FindViewById<TextView>(Resource.Id.AnimeRecomItemRecomContent));
            public TextView RecomContentOverflow => _recomContentOverflow ?? (_recomContentOverflow = _view.FindViewById<TextView>(Resource.Id.AnimeRecomItemRecomContentOverflow));
            public ImageViewAsync Image => _image ?? (_image = _view.FindViewById<ImageViewAsync>(Resource.Id.AnimeRecomItemImage));
        }
    }
}

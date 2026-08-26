using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using FFImageLoading;
using FFImageLoading.Transformations;
using FFImageLoading.Views;
using GalaSoft.MvvmLight.Helpers;
using MALClient.Android.Activities;
using MALClient.Android.AoLibsCompat;
using MALClient.Android.BindingConverters;
using MALClient.Android.Listeners;
using MALClient.Android.Utilities;
using MALClient.Android.Utilities.ImageLoading;
using MALClient.Android.Resources;
using MALClient.Models.Models.AnimeScrapped;
using MALClient.XShared.Comm.MagicalRawQueries;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Details;
using MoreLinq;

namespace MALClient.Android.Fragments.AnimeDetailsPageTabs
{
    public class AnimeDetailsPageReviewsTabFragment : MalFragmentBase
    {
        private AnimeDetailsPageViewModel ViewModel;

        private AnimeDetailsPageReviewsTabFragment()
        {
            ViewModel = ViewModelLocator.AnimeDetails;
        }

        protected override void Init(Bundle savedInstanceState)
        {

        }

        private readonly ObservableCollection<AnimeReviewData> _localReviews = new ObservableCollection<AnimeReviewData>();
        private CancellationTokenSource _drainCts;

        protected override void InitBindings()
        {
            Bindings.Add(
                this.SetBinding(() => ViewModel.LoadingReviews,
                    () => AnimeDetailsPageReviewsTabLoadingOverlay.Visibility).ConvertSourceToTarget(Converters.BoolToVisibility));

            Bindings.Add(
                this.SetBinding(() => ViewModel.LoadingReviews).WhenSourceChanges(() =>
                {
                    if (ViewModel.LoadingReviews)
                    {
                        _drainCts?.Cancel();
                        _localReviews.Clear();
                        AnimeDetailsPageReviewsTabsList.SetAdapter(null);
                    }
                    else
                    {
                        AnimeDetailsPageReviewsTabsList.SetAdapter(
                            new ObservableRecyclerAdapter<AnimeReviewData, ReviewHolder>(
                                _localReviews, BindReview, LayoutInflater, Resource.Layout.AnimeReviewItemLayout));
                        _drainCts = IncrementalListHelper.Drain(ViewModel.Reviews, _localReviews);
                    }
                }));

            Bindings.Add(
                this.SetBinding(() => ViewModel.NoReviewsDataNoticeVisibility,
                    () => AnimeDetailsPageReviewsTabEmptyNotice.Visibility)
                    .ConvertSourceToTarget(Converters.BoolToVisibility));

            AnimeDetailsPageReviewsTabsList.SetLayoutManager(new LinearLayoutManager(Activity));
            AnimeDetailsPageReviewsTabsList.AddOnScrollListener(new CustomScrollListener());
        }

        private void BindReview(AnimeReviewData review, ReviewHolder holder, int position)
        {
            if (!_reviewStates.ContainsKey(review))
                _reviewStates.Add(review, false);

            if (_reviewStates[review])
            {
                LoadScores(holder.ScoresList, review);
                holder.ReviewContent.Visibility = ViewStates.Visible;
            }
            else
            {
                holder.ScoresList.RemoveAllViews();
                holder.ReviewContent.Visibility = ViewStates.Gone;
            }

            holder.Author.Text = review.Author;
            holder.Date.Text = review.Date;
            holder.OverallScore.Text = $"Overall Rating: {review.OverallRating}";
            if (review.EpisodesSeen == "N/A")
            {
                holder.EpsSeen.Visibility = ViewStates.Gone;
            }
            else
            {
                holder.EpsSeen.Visibility = ViewStates.Visible;
                holder.EpsSeen.Text = $"{review.EpisodesSeen} episodes seen";
            }

            var text = review.HasSpoilers ? "Has Spoilers!" : string.Empty;
            text += review.IsPreliminary ? " Preliminary Review" : string.Empty;

            if (string.IsNullOrEmpty(text))
            {
                holder.MarkAsHelpful.Visibility = ViewStates.Gone;
            }
            else
            {
                holder.MarkAsHelpful.Visibility = ViewStates.Visible;
                holder.MarkAsHelpful.Text = text.Trim();
            }

            var img = holder.AvatarImage;
            if (img.Tag == null || (string)img.Tag != review.AuthorAvatar)
            {
                img.Into(review.AuthorAvatar, new CircleTransformation(), null, 200);
                img.Tag = review.AuthorAvatar;
            }
            else
            {
                img.Visibility = ViewStates.Visible;
            }
            holder.ImgPlaceholder.Visibility = ViewStates.Gone;

            holder.ItemView.Click -= OnReviewClick;
            holder.ItemView.Tag = review.Wrap();
            holder.ItemView.Click += OnReviewClick;
        }

        private void LoadScores(LinearLayout scores, AnimeReviewData review)
        {
            scores.RemoveAllViews();
            foreach (var score in review.Score)
            {
                var txt = new TextView(scores.Context)
                {
                    Text = $"{score.Field} {score.Score}",
                    Typeface = Typeface.Create("sans-serif-light", TypefaceStyle.Normal)
                };
                txt.SetTextColor(new Color(ResourceExtension.BrushText));
                scores.AddView(txt);
            }
        }

        private readonly Dictionary<AnimeReviewData, bool> _reviewStates = new Dictionary<AnimeReviewData, bool>();
        private readonly HashSet<AnimeReviewData> _revealedSpoilers = new HashSet<AnimeReviewData>();

        private void OnReviewClick(object sender, EventArgs eventArgs)
        {
            var view = sender as View;
            var model = view.Tag.Unwrap<AnimeReviewData>();
            if (_reviewStates[model])
            {
                _reviewStates[model] = false;
                _revealedSpoilers.Remove(model);
                view.FindViewById<LinearLayout>(Resource.Id.AnimeReviewItemLayoutMarksList).RemoveAllViews();
                view.FindViewById(Resource.Id.AnimeReviewItemLayoutReviewContent).Visibility = ViewStates.Gone;
                view.FindViewById<TextView>(Resource.Id.AnimeReviewItemLayoutReviewContent).Text = "";
            }
            else
            {
                _reviewStates[model] = true;
                LoadScores(view.FindViewById<LinearLayout>(Resource.Id.AnimeReviewItemLayoutMarksList), model);
                var content = view.FindViewById<TextView>(Resource.Id.AnimeReviewItemLayoutReviewContent);
                if (model.HasSpoilers && !_revealedSpoilers.Contains(model))
                {
                    _revealedSpoilers.Add(model);
                    content.Text = "This review contains spoilers. Tap again to reveal.";
                    content.Visibility = ViewStates.Visible;
                    return;
                }
                content.Text = model.Review;
                content.Visibility = ViewStates.Visible;
            }
        }

        public override int LayoutResourceId => Resource.Layout.AnimeDetailsPageReviewsTab;

        public static AnimeDetailsPageReviewsTabFragment Instance => new AnimeDetailsPageReviewsTabFragment();

        #region Views

        private RecyclerView _animeDetailsPageReviewsTabsList;
        private TextView _animeDetailsPageReviewsTabEmptyNotice;
        private FrameLayout _animeDetailsPageReviewsTabLoadingOverlay;

        public RecyclerView AnimeDetailsPageReviewsTabsList => GetView(ref _animeDetailsPageReviewsTabsList, Resource.Id.AnimeDetailsPageReviewsTabsList);

        public TextView AnimeDetailsPageReviewsTabEmptyNotice => GetView(ref _animeDetailsPageReviewsTabEmptyNotice, Resource.Id.AnimeDetailsPageReviewsTabEmptyNotice);

        public FrameLayout AnimeDetailsPageReviewsTabLoadingOverlay => GetView(ref _animeDetailsPageReviewsTabLoadingOverlay, Resource.Id.AnimeDetailsPageReviewsTabLoadingOverlay);

        #endregion

        class ReviewHolder : RecyclerView.ViewHolder
        {
            private readonly View _view;

            public ReviewHolder(View view) : base(view)
            {
                _view = view;
            }

            private TextView _author;
            private TextView _date;
            private TextView _overallScore;
            private TextView _epsSeen;
            private TextView _markAsHelpful;
            private LinearLayout _scoresList;
            private TextView _reviewContent;
            private ImageViewAsync _avatarImage;
            private View _imgPlaceholder;

            public TextView Author => _author ?? (_author = _view.FindViewById<TextView>(Resource.Id.AnimeReviewItemLayoutAuthor));
            public TextView Date => _date ?? (_date = _view.FindViewById<TextView>(Resource.Id.AnimeReviewItemLayoutDate));
            public TextView OverallScore => _overallScore ?? (_overallScore = _view.FindViewById<TextView>(Resource.Id.AnimeReviewItemLayoutOverallScore));
            public TextView EpsSeen => _epsSeen ?? (_epsSeen = _view.FindViewById<TextView>(Resource.Id.AnimeReviewItemLayoutEpsSeen));
            public TextView MarkAsHelpful => _markAsHelpful ?? (_markAsHelpful = _view.FindViewById<TextView>(Resource.Id.MarkAsHelpfulButton));
            public LinearLayout ScoresList => _scoresList ?? (_scoresList = _view.FindViewById<LinearLayout>(Resource.Id.AnimeReviewItemLayoutMarksList));
            public TextView ReviewContent => _reviewContent ?? (_reviewContent = _view.FindViewById<TextView>(Resource.Id.AnimeReviewItemLayoutReviewContent));
            public ImageViewAsync AvatarImage => _avatarImage ?? (_avatarImage = _view.FindViewById<ImageViewAsync>(Resource.Id.AnimeReviewItemLayoutAvatarImage));
            public View ImgPlaceholder => _imgPlaceholder ?? (_imgPlaceholder = _view.FindViewById(Resource.Id.AnimeReviewItemImgPlaceholder));
        }
    }
}

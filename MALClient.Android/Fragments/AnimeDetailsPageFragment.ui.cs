using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.View;
using Android.Views;
using Android.Webkit;
using Android.Widget;

using FFImageLoading.Views;
using MALClient.Android.Resources;
using MALClient.Android.UserControls;


namespace MALClient.Android.Fragments
{
    public partial class AnimeDetailsPageFragment
    {
        #region Views

        private ImageViewAsync _animeDetailsPageShowCoverImage;
        private ImageViewAsync _animeDetailsPageBlurredBackground;
        private FrameLayout _animeDetailsPagePosterContainer;
        private TextView _animeDetailsPageWatchedLabel;
        private TextView _animeDetailsPageReadVolumesLabel;
        private Button _animeDetailsPageScoreButton;
        private Button _animeDetailsPageStatusButton;
        private Button _animeDetailsPageWatchedButton;
        private Button _animeDetailsPageReadVolumesButton;
        private LinearLayout _animeDetailsPageUpdateSection;
        private FrameLayout _animeDetailsPageIncrementButton;
        private FrameLayout _animeDetailsPageDecrementButton;
        private RelativeLayout _animeDetailsPageIncDecSection;
        private FrameLayout _animeDetailsPageAddButton;
        private ProgressBar _animeDetailsPageLoadingUpdateSpinner;
        private ImageButton _animeDetailsPageFavouriteButton;
        private ImageButton _animeDetailsPageMoreButton;
        private ImageButton _animeDetailsPageTrailerButton;
        private RelativeLayout _animeDetailsPageVideoOverlay;
        private WebView _animeDetailsPageVideoWebView;
        private ImageButton _animeDetailsPageVideoCloseButton;
        private PagerSlidingTabStrip _animeDetailsPageTabStrip;
        private HeightAdjustingViewPager _animeDetailsPagePivot;
        private RelativeLayout _animeDetailsPageLoadingOverlay;
        private ScrollableSwipeToRefreshLayout _animeDetailsPageSwipeRefresh;
        private ScrollView _animeDetailsPageScrollView;
        private TextView _animeDetailsPageTitle;
        private TextView _animeDetailsPageSubtitle;
        private TextView _animeDetailsPageScoreValue;
        private TextView _animeDetailsPageTypeBadge;
        private TextView _animeDetailsPageYearLabel;
        private Button _animeDetailsPageQuickAddToListButton;
        private Button _animeDetailsPageQuickFavoriteButton;

        public ImageViewAsync AnimeDetailsPageShowCoverImage => GetView(ref _animeDetailsPageShowCoverImage, Resource.Id.AnimeDetailsPageShowCoverImage);

        public ImageViewAsync AnimeDetailsPageBlurredBackground => GetView(ref _animeDetailsPageBlurredBackground, Resource.Id.AnimeDetailsPageBlurredBackground);

        public FrameLayout AnimeDetailsPagePosterContainer => GetView(ref _animeDetailsPagePosterContainer, Resource.Id.AnimeDetailsPagePosterContainer);

        public TextView AnimeDetailsPageWatchedLabel => GetView(ref _animeDetailsPageWatchedLabel, Resource.Id.AnimeDetailsPageWatchedLabel);

        public TextView AnimeDetailsPageReadVolumesLabel => GetView(ref _animeDetailsPageReadVolumesLabel, Resource.Id.AnimeDetailsPageReadVolumesLabel);

        public Button AnimeDetailsPageScoreButton => GetView(ref _animeDetailsPageScoreButton, Resource.Id.AnimeDetailsPageScoreButton);

        public Button AnimeDetailsPageStatusButton => GetView(ref _animeDetailsPageStatusButton, Resource.Id.AnimeDetailsPageStatusButton);

        public Button AnimeDetailsPageWatchedButton => GetView(ref _animeDetailsPageWatchedButton, Resource.Id.AnimeDetailsPageWatchedButton);

        public Button AnimeDetailsPageReadVolumesButton => GetView(ref _animeDetailsPageReadVolumesButton, Resource.Id.AnimeDetailsPageReadVolumesButton);

        public LinearLayout AnimeDetailsPageUpdateSection => GetView(ref _animeDetailsPageUpdateSection, Resource.Id.AnimeDetailsPageUpdateSection);

        public FrameLayout AnimeDetailsPageIncrementButton => GetView(ref _animeDetailsPageIncrementButton, Resource.Id.AnimeDetailsPageIncrementButton);

        public FrameLayout AnimeDetailsPageDecrementButton => GetView(ref _animeDetailsPageDecrementButton, Resource.Id.AnimeDetailsPageDecrementButton);

        public RelativeLayout AnimeDetailsPageIncDecSection => GetView(ref _animeDetailsPageIncDecSection, Resource.Id.AnimeDetailsPageIncDecSection);

        public FrameLayout AnimeDetailsPageAddButton => GetView(ref _animeDetailsPageAddButton, Resource.Id.AnimeDetailsPageAddButton);

        public ProgressBar AnimeDetailsPageLoadingUpdateSpinner => GetView(ref _animeDetailsPageLoadingUpdateSpinner, Resource.Id.AnimeDetailsPageLoadingUpdateSpinner);

        public ImageButton AnimeDetailsPageFavouriteButton => GetView(ref _animeDetailsPageFavouriteButton, Resource.Id.AnimeDetailsPageFavouriteButton);

        public ImageButton AnimeDetailsPageMoreButton => GetView(ref _animeDetailsPageMoreButton, Resource.Id.AnimeDetailsPageMoreButton);
        public ImageButton AnimeDetailsPageTrailerButton => GetView(ref _animeDetailsPageTrailerButton, Resource.Id.AnimeDetailsPageTrailerButton);
        public RelativeLayout AnimeDetailsPageVideoOverlay => GetView(ref _animeDetailsPageVideoOverlay, Resource.Id.AnimeDetailsPageVideoOverlay);
        public WebView AnimeDetailsPageVideoWebView => GetView(ref _animeDetailsPageVideoWebView, Resource.Id.AnimeDetailsPageVideoWebView);
        public ImageButton AnimeDetailsPageVideoCloseButton => GetView(ref _animeDetailsPageVideoCloseButton, Resource.Id.AnimeDetailsPageVideoCloseButton);

        public UserControls.PagerSlidingTabStrip AnimeDetailsPageTabStrip => GetView(ref _animeDetailsPageTabStrip, Resource.Id.AnimeDetailsPageTabStrip);

        public HeightAdjustingViewPager AnimeDetailsPagePivot => GetView(ref _animeDetailsPagePivot, Resource.Id.AnimeDetailsPagePivot);

        public RelativeLayout AnimeDetailsPageLoadingOverlay => GetView(ref _animeDetailsPageLoadingOverlay, Resource.Id.AnimeDetailsPageLoadingOverlay);

        public ScrollView AnimeDetailsPageScrollView => GetView(ref _animeDetailsPageScrollView, Resource.Id.AnimeDetailsPageScrollView);

        public ScrollableSwipeToRefreshLayout AnimeDetailsPageSwipeRefresh => GetView(ref _animeDetailsPageSwipeRefresh, Resource.Id.AnimeDetailsPageSwipeRefresh);

        public TextView AnimeDetailsPageTitle => GetView(ref _animeDetailsPageTitle, Resource.Id.AnimeDetailsPageTitle);

        public TextView AnimeDetailsPageSubtitle => GetView(ref _animeDetailsPageSubtitle, Resource.Id.AnimeDetailsPageSubtitle);

        public TextView AnimeDetailsPageScoreValue => GetView(ref _animeDetailsPageScoreValue, Resource.Id.AnimeDetailsPageScoreValue);

        public TextView AnimeDetailsPageTypeBadge => GetView(ref _animeDetailsPageTypeBadge, Resource.Id.AnimeDetailsPageTypeBadge);

        public TextView AnimeDetailsPageYearLabel => GetView(ref _animeDetailsPageYearLabel, Resource.Id.AnimeDetailsPageYearLabel);

        public Button AnimeDetailsPageQuickAddToListButton => GetView(ref _animeDetailsPageQuickAddToListButton, Resource.Id.AnimeDetailsPageQuickAddToListButton);

        public Button AnimeDetailsPageQuickFavoriteButton => GetView(ref _animeDetailsPageQuickFavoriteButton, Resource.Id.AnimeDetailsPageQuickFavoriteButton);

        #endregion

    }
}

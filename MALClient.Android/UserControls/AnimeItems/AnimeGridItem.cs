using System;
using System.ComponentModel;
using Android.Content;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using FFImageLoading.Transformations;
using FFImageLoading.Views;
using MALClient.Android.Listeners;
using MALClient.Models.Enums;
using MALClient.XShared.ViewModels;

namespace MALClient.Android.UserControls
{
    public class AnimeGridItem : UserControlBase<AnimeItemViewModel, FrameLayout>
    {
        private readonly Action<AnimeItemViewModel> _onItemClickAction;
        private bool _propertyHandlerAttached;

        #region Constructors

        public AnimeGridItem(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        public AnimeGridItem(Context context, Action<AnimeItemViewModel> onItemClickAction = null) : base(context)
        {
            _onItemClickAction = onItemClickAction;
        }

        public AnimeGridItem(Context context, IAttributeSet attrs) : base(context, attrs)
        {
        }

        public AnimeGridItem(Context context, IAttributeSet attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
        }

        public AnimeGridItem(Context context, IAttributeSet attrs, int defStyleAttr, int defStyleRes) : base(context, attrs, defStyleAttr, defStyleRes)
        {
        }

        #endregion

        protected override int ResourceId => Resource.Layout.AnimeGridItem;

        protected override void BindModelFling()
        {
            if (!AnimeGridItemImage.AnimeIntoIfLoaded(ViewModel.ImgUrl, new RoundedCornersTransformation(8, 0)))
            {
                AnimeGridItemImage.Visibility = ViewStates.Invisible;
                AnimeGridItemImgPlaceholder.Visibility = ViewStates.Visible;
            }
            else
            {
                AnimeGridItemImgPlaceholder.Visibility = ViewStates.Gone;
            }
        }

        protected override void BindModelFull()
        {
            if ((string)AnimeGridItemImage.Tag != ViewModel.ImgUrl)
            {
                AnimeGridItemImage.AnimeInto(ViewModel.ImgUrl, AnimeGridItemImgPlaceholder, new RoundedCornersTransformation(8, 0));
            }
            else
            {
                AnimeGridItemImage.Visibility = ViewStates.Visible;
            }

            AnimeGridItemTitle.Text = ViewModel.Title;
            UpdateBadge();

            if (!_propertyHandlerAttached)
            {
                ViewModel.PropertyChanged += ViewModelOnPropertyChanged;
                _propertyHandlerAttached = true;
            }
        }

        private void UpdateBadge()
        {
            if (AnimeGridItemBadgeContainer == null) return;
            if (ViewModel.Auth)
            {
                AnimeGridItemBadgeContainer.Visibility = ViewStates.Visible;
                AnimeGridItemStatus.Text = ViewModel.MyStatusBindShort;
                AnimeGridItemEpisodes.Text = ViewModel.MyEpisodesBindShort;
                AnimeGridItemScore.Text = ViewModel.MyScoreBindShort;

                int statusColor;
                switch (ViewModel.MyStatus)
                {
                    case AnimeStatus.Watching:
                        statusColor = global::Android.Graphics.Color.ParseColor("#228b22");
                        break;
                    case AnimeStatus.Completed:
                        statusColor = global::Android.Graphics.Color.ParseColor("#1e90ff");
                        break;
                    case AnimeStatus.OnHold:
                        statusColor = global::Android.Graphics.Color.ParseColor("#ffd700");
                        break;
                    case AnimeStatus.Dropped:
                        statusColor = global::Android.Graphics.Color.ParseColor("#dc143c");
                        break;
                    default:
                        statusColor = global::Android.Graphics.Color.ParseColor("#808080");
                        break;
                }
                AnimeGridItemStatus.SetTextColor(new global::Android.Graphics.Color(statusColor));
            }
            else
            {
                AnimeGridItemBadgeContainer.Visibility = ViewStates.Gone;
            }
        }

        private void ViewModelOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            switch (propertyChangedEventArgs.PropertyName)
            {
                case nameof(ViewModel.ImgUrl):
                    AnimeGridItemImage.AnimeInto(ViewModel.ImgUrl, AnimeGridItemImgPlaceholder, new RoundedCornersTransformation(8, 0));
                    break;
                case nameof(ViewModel.Title):
                    AnimeGridItemTitle.Text = ViewModel.Title;
                    break;
                case nameof(ViewModel.MyStatusBindShort):
                case nameof(ViewModel.MyStatus):
                case nameof(ViewModel.MyEpisodesBindShort):
                case nameof(ViewModel.MyScoreBindShort):
                    UpdateBadge();
                    break;
            }
        }

        protected override void RootContainerInit()
        {
            RootContainer.SetOnClickListener(new OnClickListener(view => ContainerOnClick()));
        }

        private void ContainerOnClick()
        {
            if (_onItemClickAction != null)
                _onItemClickAction.Invoke(ViewModel);
            else
                ViewModel.NavigateDetailsCommand.Execute(null);
        }

        protected override void BindModelBasic()
        {
            AnimeGridItemTitle.Text = ViewModel.Title;
            AnimeGridItemImage.Tag = null;
        }

        public void BindModel()
        {
            if (ViewModel.ImgUrl != null)
                RootContainer.Post(() => BindModelFull());
        }

        #region Views

        private ImageViewAsync _animeGridItemImage;
        private ProgressBar _animeGridItemImgPlaceholder;
        private TextView _animeGridItemTitle;
        private View _animeGridItemBadgeContainer;
        private TextView _animeGridItemStatus;
        private TextView _animeGridItemEpisodes;
        private TextView _animeGridItemScore;

        public ImageViewAsync AnimeGridItemImage => _animeGridItemImage ?? (_animeGridItemImage = FindViewById<ImageViewAsync>(Resource.Id.AnimeGridItemImage));
        public ProgressBar AnimeGridItemImgPlaceholder => _animeGridItemImgPlaceholder ?? (_animeGridItemImgPlaceholder = FindViewById<ProgressBar>(Resource.Id.AnimeGridItemImgPlaceholder));
        public TextView AnimeGridItemTitle => _animeGridItemTitle ?? (_animeGridItemTitle = FindViewById<TextView>(Resource.Id.AnimeGridItemTitle));
        public View AnimeGridItemBadgeContainer => _animeGridItemBadgeContainer ?? (_animeGridItemBadgeContainer = FindViewById<View>(Resource.Id.AnimeGridItemBadgeContainer));
        public TextView AnimeGridItemStatus => _animeGridItemStatus ?? (_animeGridItemStatus = FindViewById<TextView>(Resource.Id.AnimeGridItemStatus));
        public TextView AnimeGridItemEpisodes => _animeGridItemEpisodes ?? (_animeGridItemEpisodes = FindViewById<TextView>(Resource.Id.AnimeGridItemEpisodes));
        public TextView AnimeGridItemScore => _animeGridItemScore ?? (_animeGridItemScore = FindViewById<TextView>(Resource.Id.AnimeGridItemScore));

        #endregion
    }
}

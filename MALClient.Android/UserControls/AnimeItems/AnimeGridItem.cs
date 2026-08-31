using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Android.Content;
using Android.OS;
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
            if (!AnimeGridItemImage.AnimeIntoIfLoaded(ViewModel.ImgUrl))
            {
                AnimeGridItemImage.Visibility = ViewStates.Invisible;
                AnimeGridItemImgPlaceholder.Visibility = ViewStates.Visible;
            }
            else
            {
                AnimeGridItemImgPlaceholder.Visibility = ViewStates.Gone;
            }
        }

        protected override async void BindModelFull()
        {
            if ((string)AnimeGridItemImage.Tag != ViewModel.ImgUrl)
            {
                AnimeGridItemImage.AnimeInto(ViewModel.ImgUrl, AnimeGridItemImgPlaceholder);
            }
            else
            {
                AnimeGridItemImage.Visibility = ViewStates.Visible;
            }

            AnimeGridItemTitle.Text = ViewModel.Title;
            await ViewModel.UpdateAirDateDisplay();
            UpdateBadge();

            var airTime = ViewModel.TimeTillNextAirCache;
            if (!string.IsNullOrEmpty(airTime))
            {
                AnimeGridItemAirTime.Text = airTime;
                AnimeGridItemAirTime.Visibility = ViewStates.Visible;
            }
            else
            {
                AnimeGridItemAirTime.Visibility = ViewStates.Gone;
            }

            if (!_propertyHandlerAttached)
            {
                ViewModel.PropertyChanged += ViewModelOnPropertyChanged;
                _propertyHandlerAttached = true;
            }
        }

        private void UpdateBadge()
        {
            if (AnimeGridItemBadgeContainer == null) return;

            var countdown = ViewModel.AirDayTillBind;
            if (string.IsNullOrEmpty(countdown))
                countdown = ViewModel.TimeTillNextAirCache;
            if (countdown == "Aired!") countdown = "";

            var hasCountdown = !string.IsNullOrEmpty(countdown);

            if (ViewModel.Auth)
            {
                AnimeGridItemBadgeContainer.Visibility = ViewStates.Visible;
                AnimeGridItemStatus.Text = ViewModel.MyStatusBindShort;
                AnimeGridItemEpisodes.Text = ViewModel.MyEpisodesBindShort;
                AnimeGridItemScore.Text = ViewModel.MyScoreBindShort;
                AnimeGridItemScore.Visibility = ViewModel.MyScore <= 0 ? ViewStates.Gone : ViewStates.Visible;

                var statusVisible = true;
                var episodesVisible = true;
                var scoreVisible = ViewModel.MyScore > 0;

                AnimeGridItemStatus.Visibility = statusVisible ? ViewStates.Visible : ViewStates.Gone;

                var rightSectionVisible = episodesVisible || scoreVisible;
                var rightSection = AnimeGridItemEpisodes.Parent as LinearLayout;
                if (rightSection != null)
                    rightSection.Visibility = rightSectionVisible ? ViewStates.Visible : ViewStates.Gone;

                if (hasCountdown)
                {
                    AnimeGridItemCountdown.Text = countdown;
                    AnimeGridItemCountdown.Visibility = ViewStates.Visible;
                    AnimeGridItemCountdownDivider.Visibility = rightSectionVisible ? ViewStates.Visible : ViewStates.Gone;
                }
                else
                {
                    AnimeGridItemCountdown.Visibility = ViewStates.Gone;
                    AnimeGridItemCountdownDivider.Visibility = ViewStates.Gone;
                }

                AnimeGridItemDivider1.Visibility = statusVisible && rightSectionVisible ? ViewStates.Visible : ViewStates.Gone;

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
                if (hasCountdown)
                {
                    AnimeGridItemBadgeContainer.Visibility = ViewStates.Visible;
                    AnimeGridItemCountdown.Text = countdown;
                    AnimeGridItemCountdown.Visibility = ViewStates.Visible;
                    AnimeGridItemCountdownDivider.Visibility = ViewStates.Gone;
                    AnimeGridItemDivider1.Visibility = ViewStates.Gone;
                    var rightSection = AnimeGridItemEpisodes.Parent as LinearLayout;
                    if (rightSection != null)
                        rightSection.Visibility = ViewStates.Gone;
                    AnimeGridItemStatus.Visibility = ViewStates.Gone;
                }
                else
                {
                    AnimeGridItemBadgeContainer.Visibility = ViewStates.Gone;
                }
            }
        }

        private void ViewModelOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            switch (propertyChangedEventArgs.PropertyName)
            {
                case nameof(ViewModel.ImgUrl):
                    AnimeGridItemImage.AnimeInto(ViewModel.ImgUrl, AnimeGridItemImgPlaceholder);
                    break;
                case nameof(ViewModel.Title):
                    AnimeGridItemTitle.Text = ViewModel.Title;
                    break;
                case nameof(ViewModel.MyStatusBindShort):
                case nameof(ViewModel.MyStatus):
                case nameof(ViewModel.MyEpisodesBindShort):
                case nameof(ViewModel.MyScoreBindShort):
                case nameof(ViewModel.AirDayTillBind):
                case nameof(ViewModel.Airing):
                case nameof(ViewModel.TimeTillNextAirCache):
                    UpdateBadge();
                    break;
            }
        }

        protected override void CleanupPreviousModel()
        {
            ViewModel.PropertyChanged -= ViewModelOnPropertyChanged;
            _propertyHandlerAttached = false;
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
        private TextView _animeGridItemAirTime;
        private TextView _animeGridItemCountdown;
        private View _animeGridItemCountdownDivider;
        private View _animeGridItemDivider1;

        public ImageViewAsync AnimeGridItemImage => _animeGridItemImage ?? (_animeGridItemImage = FindViewById<ImageViewAsync>(Resource.Id.AnimeGridItemImage));
        public ProgressBar AnimeGridItemImgPlaceholder => _animeGridItemImgPlaceholder ?? (_animeGridItemImgPlaceholder = FindViewById<ProgressBar>(Resource.Id.AnimeGridItemImgPlaceholder));
        public TextView AnimeGridItemTitle => _animeGridItemTitle ?? (_animeGridItemTitle = FindViewById<TextView>(Resource.Id.AnimeGridItemTitle));
        public View AnimeGridItemBadgeContainer => _animeGridItemBadgeContainer ?? (_animeGridItemBadgeContainer = FindViewById<View>(Resource.Id.AnimeGridItemBadgeContainer));
        public TextView AnimeGridItemStatus => _animeGridItemStatus ?? (_animeGridItemStatus = FindViewById<TextView>(Resource.Id.AnimeGridItemStatus));
        public TextView AnimeGridItemEpisodes => _animeGridItemEpisodes ?? (_animeGridItemEpisodes = FindViewById<TextView>(Resource.Id.AnimeGridItemEpisodes));
        public TextView AnimeGridItemScore => _animeGridItemScore ?? (_animeGridItemScore = FindViewById<TextView>(Resource.Id.AnimeGridItemScore));
        public TextView AnimeGridItemAirTime => _animeGridItemAirTime ?? (_animeGridItemAirTime = FindViewById<TextView>(Resource.Id.AnimeGridItemAirTime));
        public TextView AnimeGridItemCountdown => _animeGridItemCountdown ?? (_animeGridItemCountdown = FindViewById<TextView>(Resource.Id.AnimeGridItemCountdown));
        public View AnimeGridItemCountdownDivider => _animeGridItemCountdownDivider ?? (_animeGridItemCountdownDivider = FindViewById<View>(Resource.Id.AnimeGridItemCountdownDivider));
        public View AnimeGridItemDivider1 => _animeGridItemDivider1 ?? (_animeGridItemDivider1 = FindViewById<View>(Resource.Id.AnimeGridItemDivider1));

        #endregion
    }
}

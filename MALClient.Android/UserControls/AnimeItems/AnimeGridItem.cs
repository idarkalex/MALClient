using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Com.Daimajia.Swipe;
using FFImageLoading.Views;
using MALClient.Android.Listeners;
using MALClient.Models.Enums;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;

namespace MALClient.Android.UserControls
{
    public class AnimeGridItem : UserControlBase<AnimeItemViewModel, SwipeLayout>
    {
        private readonly bool _allowSwipeInGivenContext;
        private readonly Action<AnimeItemViewModel> _onItemClickAction;
        private bool _propertyHandlerAttached;

        #region Constructors

        public AnimeGridItem(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        public AnimeGridItem(Context context, bool allowSwipeInGivenContext, Action<AnimeItemViewModel> onItemClickAction, bool displayTimeTillAir = false) : base(context)
        {
            if (Settings.EnableSwipeToIncDec)
                _allowSwipeInGivenContext = allowSwipeInGivenContext;
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

        protected override void BindModelFull()
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

            if (!_propertyHandlerAttached)
            {
                ViewModel.PropertyChanged += ViewModelOnPropertyChanged;
                _propertyHandlerAttached = true;
            }

            if (_allowSwipeInGivenContext && ViewModel.Auth)
            {
                RootContainer.SwipeEnabled = true;
            }
            else
            {
                RootContainer.SwipeEnabled = false;
                return;
            }

            if (_swipeListener == null)
            {
                _swipeListener = new SwipeLayoutListener();
                _swipeListener.OnOpenEvent += SwipeOnOpenEvent;
                RootContainer.AddSwipeListener(_swipeListener);
            }

            if (Settings.ReverseSwipingDirection)
            {
                AnimeGridItemBackSurfaceAdd.SetBackgroundColor(new Color(ResourceExtension.BrushFlyoutBackground));
                SurfaceAddIcon.SetImageResource(Resource.Drawable.icon_minus);
                SurfaceAddIcon.ImageTintList = ColorStateList.ValueOf(new Color(ResourceExtension.BrushText));

                AnimeGridItemBackSurfaceSubtract.SetBackgroundColor(new Color(ResourceExtension.AccentColour));
                SurfaceSubtractIcon.SetImageResource(Resource.Drawable.icon_add);
                SurfaceSubtractIcon.ImageTintList = ColorStateList.ValueOf(Color.White);
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
            }
        }

        protected override void RootContainerInit()
        {
            RootContainer.SetOnClickListener(new OnClickListener(view => ContainerOnClick()));

            if (Settings.MakeGridItemsSmaller)
            {
                AnimeGridItemUpperSection.LayoutParameters.Height = DimensionsHelper.DpToPx(260);
                AnimeGridItemTitle.SetTextSize(ComplexUnitType.Sp, 11);
            }
        }

        private void ContainerOnClick()
        {
            if (_swipeListener?.IsSwiping ?? false)
                return;
            if (_onItemClickAction != null)
                _onItemClickAction.Invoke(ViewModel);
            else
                ViewModel.NavigateDetailsCommand.Execute(null);
        }

        #region Swipe

        private SwipeLayoutListener _swipeListener;
        private bool _swipeCooldown;

        private async void SwipeOnOpenEvent(SwipeLayout sender)
        {
            if (_swipeCooldown)
                return;
            _swipeCooldown = true;

            var edge = sender.GetDragEdge();
            if (edge == SwipeLayout.DragEdge.Right)
            {
                if (Settings.ReverseSwipingDirection)
                    ViewModel.DecrementWatchedCommand.Execute(null);
                else
                    ViewModel.IncrementWatchedCommand.Execute(null);
            }
            else if (edge == SwipeLayout.DragEdge.Left)
            {
                if (Settings.ReverseSwipingDirection)
                    ViewModel.IncrementWatchedCommand.Execute(null);
                else
                    ViewModel.DecrementWatchedCommand.Execute(null);
            }
            await Task.Delay(350);
            sender.Close();
            _swipeCooldown = false;
        }

        #endregion

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

        private RelativeLayout _animeGridItemUpperSection;
        private ImageViewAsync _animeGridItemImage;
        private ProgressBar _animeGridItemImgPlaceholder;
        private TextView _animeGridItemTitle;
        private LinearLayout _animeGridItemTitleOverlay;
        private FrameLayout _animeGridItemBackSurfaceAdd;
        private FrameLayout _animeGridItemBackSurfaceSubtract;
        private ImageView _surfaceAddIcon;
        private ImageView _surfaceSubtractIcon;

        public RelativeLayout AnimeGridItemUpperSection => _animeGridItemUpperSection ?? (_animeGridItemUpperSection = FindViewById<RelativeLayout>(Resource.Id.AnimeGridItemUpperSection));
        public ImageViewAsync AnimeGridItemImage => _animeGridItemImage ?? (_animeGridItemImage = FindViewById<ImageViewAsync>(Resource.Id.AnimeGridItemImage));
        public ProgressBar AnimeGridItemImgPlaceholder => _animeGridItemImgPlaceholder ?? (_animeGridItemImgPlaceholder = FindViewById<ProgressBar>(Resource.Id.AnimeGridItemImgPlaceholder));
        public TextView AnimeGridItemTitle => _animeGridItemTitle ?? (_animeGridItemTitle = FindViewById<TextView>(Resource.Id.AnimeGridItemTitle));
        public LinearLayout AnimeGridItemTitleOverlay => _animeGridItemTitleOverlay ?? (_animeGridItemTitleOverlay = FindViewById<LinearLayout>(Resource.Id.AnimeGridItemTitleOverlay));
        public FrameLayout AnimeGridItemBackSurfaceAdd => _animeGridItemBackSurfaceAdd ?? (_animeGridItemBackSurfaceAdd = FindViewById<FrameLayout>(Resource.Id.AnimeGridItemBackSurfaceAdd));
        public FrameLayout AnimeGridItemBackSurfaceSubtract => _animeGridItemBackSurfaceSubtract ?? (_animeGridItemBackSurfaceSubtract = FindViewById<FrameLayout>(Resource.Id.AnimeGridItemBackSurfaceSubtract));
        public ImageView SurfaceAddIcon => _surfaceAddIcon ?? (_surfaceAddIcon = FindViewById<ImageView>(Resource.Id.SurfaceAddIcon));
        public ImageView SurfaceSubtractIcon => _surfaceSubtractIcon ?? (_surfaceSubtractIcon = FindViewById<ImageView>(Resource.Id.SurfaceSubtractIcon));

        #endregion
    }
}
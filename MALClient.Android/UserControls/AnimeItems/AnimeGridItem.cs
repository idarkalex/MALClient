using System;
using System.ComponentModel;
using Android.Content;
using Android.Graphics;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using FFImageLoading.Views;
using MALClient.Android.Listeners;
using MALClient.XShared.Utils;
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

        public AnimeGridItem(Context context, bool allowSwipeInGivenContext, Action<AnimeItemViewModel> onItemClickAction, bool displayTimeTillAir = false) : base(context)
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

        public ImageViewAsync AnimeGridItemImage => _animeGridItemImage ?? (_animeGridItemImage = FindViewById<ImageViewAsync>(Resource.Id.AnimeGridItemImage));
        public ProgressBar AnimeGridItemImgPlaceholder => _animeGridItemImgPlaceholder ?? (_animeGridItemImgPlaceholder = FindViewById<ProgressBar>(Resource.Id.AnimeGridItemImgPlaceholder));
        public TextView AnimeGridItemTitle => _animeGridItemTitle ?? (_animeGridItemTitle = FindViewById<TextView>(Resource.Id.AnimeGridItemTitle));

        #endregion
    }
}

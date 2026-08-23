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
using Android.Text;
using Android.Text.Style;
using Android.Util;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using MALClient.Android.Listeners;
using FFImageLoading.Views;
using GalaSoft.MvvmLight.Helpers;
using MALClient.Android.BindingConverters;
using MALClient.Android.Resources;
using MALClient.Models.Enums;
using MALClient.Models.Models.AnimeScrapped;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Details;
using MALClient.XShared.ViewModels.Main;

namespace MALClient.Android.Fragments
{
    public class PromoVideosPageFragment : MalFragmentBase
    {
        private PopularVideosViewModel ViewModel;

        private static readonly StyleSpan PrefixStyle;
        private static readonly ForegroundColorSpan PrefixColorStyle;

        static PromoVideosPageFragment()
        {
            PrefixStyle = new StyleSpan(TypefaceStyle.Bold);
            PrefixColorStyle = new ForegroundColorSpan(new Color(ResourceExtension.AccentColour));
        }

        private GridViewColumnHelper _helper;

        protected override void Init(Bundle savedInstanceState)
        {
            ViewModel = ViewModelLocator.PopularVideos;
            ViewModel.Init();
        }

        protected override void InitBindings()
        {
            PromoVideosPageVideoCloseButton.SetOnClickListener(new OnClickListener(view => HideVideoOverlay()));
            Bindings.Add(
                this.SetBinding(() => ViewModel.Loading,
                    () => PromoVideosPageLoadingSpinner.Visibility).ConvertSourceToTarget(Converters.BoolToVisibility));

            _helper = new GridViewColumnHelper(PromoVideosPageGridView,null,2,3,true);

            Bindings.Add(this.SetBinding(() => ViewModel.Videos).WhenSourceChanges(() =>
            {
                if (ViewModel.Videos != null)
                    PromoVideosPageGridView.InjectFlingAdapter(ViewModel.Videos, SetItemBindingsFull,
                        SetItemBindingsFling, GetItemContainer,DataTemplateBasic);
                else
                    PromoVideosPageGridView.Adapter = null;
            }));          
        }

        private void DataTemplateBasic(View view, int i, AnimeVideoData animeVideoData)
        {
            var str = new SpannableString($"{animeVideoData.Name} - {animeVideoData.AnimeTitle}");
            str.SetSpan(PrefixStyle, 0, animeVideoData.Name.Length, SpanTypes.InclusiveInclusive);
            str.SetSpan(PrefixColorStyle, 0, animeVideoData.Name.Length, SpanTypes.InclusiveInclusive);
            view.FindViewById<TextView>(Resource.Id.PromoVideosPageItemSubtitle)
                .SetText(str.SubSequenceFormatted(0, str.Length()), TextView.BufferType.Spannable);
        }

        private View GetItemContainer(int i)
        {
            var view = Activity.LayoutInflater.Inflate(Resource.Layout.PromoVideosPageItem, null);

            view.FindViewById(Resource.Id.PromoVideosPageItemImageSection).Click += VideoItemOnClickOpenVideo;
            view.FindViewById(Resource.Id.PromoVideosPageItemSubtitleSection).Click += VideoItemOnClickOpenAnime;


            return view;
        }

        private void SetItemBindingsFull(View view, int i, AnimeVideoData animeVideoData)
        {
            var img = view.FindViewById<ImageViewAsync>(Resource.Id.PromoVideosPageItemImage);
            if (img.Tag == null || (string)img.Tag != animeVideoData.Thumb)
            {
                img.Into(animeVideoData.Thumb);
            }
            view.FindViewById(Resource.Id.PromoVideosPageItemImgPlaceholder).Visibility = ViewStates.Gone;
        }

        private void SetItemBindingsFling(View view, int i, AnimeVideoData animeVideoData)
        {
            var img = view.FindViewById<ImageViewAsync>(Resource.Id.PromoVideosPageItemImage);
            if (img.IntoIfLoaded(animeVideoData.Thumb))
            {
                img.Visibility = ViewStates.Visible;
                view.FindViewById(Resource.Id.PromoVideosPageItemImgPlaceholder).Visibility = ViewStates.Visible;
            }
            else
            {
                img.Visibility = ViewStates.Invisible;
                view.FindViewById(Resource.Id.PromoVideosPageItemImgPlaceholder).Visibility = ViewStates.Gone;
            }
            
             
        }

        private async void VideoItemOnClickOpenVideo(object sender, EventArgs eventArgs)
        {
            var data = ((sender as View).Parent as View).Tag.Unwrap<AnimeVideoData>();
            if (string.IsNullOrEmpty(data?.YtLink))
                return;
            ShowVideoOverlay(data.YtLink);
        }

        private void ShowVideoOverlay(string url)
        {
            if (string.IsNullOrEmpty(url) || !IsAdded)
                return;
            ResourceLocator.SystemControlsLauncherService.LaunchUri(new Uri(url));
        }

        private void HideVideoOverlay()
        {
            PromoVideosPageVideoWebView.LoadUrl("about:blank");
            PromoVideosPageVideoOverlay.Visibility = ViewStates.Gone;
        }

        private void VideoItemOnClickOpenAnime(object sender, EventArgs eventArgs)
        {
            ViewModel.NavDetailsCommand.Execute(((sender as View).Parent as View).Tag.Unwrap<AnimeVideoData>());
        }

        public override int LayoutResourceId => Resource.Layout.PromoVideosPage;


        public override void OnConfigurationChanged(Configuration newConfig)
        {
            _helper.OnConfigurationChanged(newConfig);
        }

        #region Views

        private GridView _promoVideosPageGridView;
        private ProgressBar _promoVideosPageLoadingSpinner;
        private RelativeLayout _promoVideosPageVideoOverlay;
        private WebView _promoVideosPageVideoWebView;
        private ImageButton _promoVideosPageVideoCloseButton;

        public RelativeLayout PromoVideosPageVideoOverlay => GetView(ref _promoVideosPageVideoOverlay, Resource.Id.PromoVideosPageVideoOverlay);
        public WebView PromoVideosPageVideoWebView => GetView(ref _promoVideosPageVideoWebView, Resource.Id.PromoVideosPageVideoWebView);
        public ImageButton PromoVideosPageVideoCloseButton => GetView(ref _promoVideosPageVideoCloseButton, Resource.Id.PromoVideosPageVideoCloseButton);

        public GridView PromoVideosPageGridView => GetView(ref _promoVideosPageGridView, Resource.Id.PromoVideosPageGridView);

        public ProgressBar PromoVideosPageLoadingSpinner => GetView(ref _promoVideosPageLoadingSpinner, Resource.Id.PromoVideosPageLoadingSpinner);

        #endregion
    }
}




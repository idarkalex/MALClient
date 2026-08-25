using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Webkit;
using Android.Widget;

using Com.Shehabic.Droppy;
using FFImageLoading;
using FFImageLoading.Transformations;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Helpers;
using MALClient.Android.Activities;
using MALClient.Android.BindingConverters;
using MALClient.Android.Dialogs;
using MALClient.Android.DIalogs;
using MALClient.Android.Flyouts;
using MALClient.Android.Listeners;
using MALClient.Android.PagerAdapters;
using MALClient.Android.Resources;
using MALClient.Android.UserControls;
using MALClient.Models.Enums;
using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Details;

namespace MALClient.Android.Fragments
{
    public partial class AnimeDetailsPageFragment : MalFragmentBase
    {
        private AnimeDetailsPageNavigationArgs _navArgs;
        private AnimeDetailsPageViewModel ViewModel;
        private DroppyMenuPopup _menu;
        private ScrollListener _scrollListener;

        public AnimeDetailsPageFragment(AnimeDetailsPageNavigationArgs navArgs)
        {
            _navArgs = navArgs;
        }

        protected override void Init(Bundle savedInstanceState)
        {
            ViewModel = ViewModelLocator.AnimeDetails;
            ViewModel.Init(_navArgs, false);
        }

        protected override void InitBindings()
        {
            AnimeDetailsPagePivot.Adapter = new AnimeDetailsPagerAdapter(ChildFragmentManager);
            AnimeDetailsPageTabStrip.IndicatorColor = Color.ParseColor("#0066FF");
            AnimeDetailsPageTabStrip.IndicatorHeight = 3;
            AnimeDetailsPageTabStrip.SetViewPager(AnimeDetailsPagePivot);
            AnimeDetailsPageTabStrip.CenterTabs();
            AnimeDetailsPagePivot.OffscreenPageLimit = 7;
            var maxTabs = ViewModelLocator.AnimeDetails.AnimeMode ? 7 : 6;
            var initialTab = Math.Max(0, Math.Min(_navArgs.SourceTabIndex, maxTabs - 1));
            AnimeDetailsPagePivot.SetCurrentItem(initialTab, false);

            AnimeDetailsPageTabStrip.OnPageChangeListener =
                new OnPageChangedListener(i =>
                {
                    ViewModel.DetailsPivotSelectedIndex = i;
                    AnimeDetailsPagePivot.RequestLayout();
                });

            Bindings.Add(
                this.SetBinding(() => ViewModel.MyScoreBind,
                    () => AnimeDetailsPageScoreButton.Text));
            Bindings.Add(
                this.SetBinding(() => ViewModel.MyStatusBind,
                    () => AnimeDetailsPageStatusButton.Text));
            Bindings.Add(
                this.SetBinding(() => ViewModel.MyEpisodesBind,
                    () => AnimeDetailsPageWatchedButton.Text));
            Bindings.Add(
                this.SetBinding(() => ViewModel.MyVolumesBind,
                    () => AnimeDetailsPageReadVolumesButton.Text));
            Bindings.Add(
                this.SetBinding(() => ViewModel.LoadingGlobal,
                        () => AnimeDetailsPageLoadingOverlay.Visibility)
                    .ConvertSourceToTarget(Converters.BoolToVisibility));

            Bindings.Add(this.SetBinding(() => ViewModel.IsIncrementButtonEnabled).WhenSourceChanges(() =>
            {
                AnimeDetailsPageIncrementButton.Alpha = ViewModel.IsIncrementButtonEnabled ? 1 : .35f;
            }));
            Bindings.Add(this.SetBinding(() => ViewModel.IsDecrementButtonEnabled).WhenSourceChanges(() =>
            {
                AnimeDetailsPageDecrementButton.Alpha = ViewModel.IsDecrementButtonEnabled ? 1 : .35f;
            }));

            Bindings.Add(this.SetBinding(() => ViewModel.AnimeMode).WhenSourceChanges(() =>
            {
                // pager Count changes 7<->6; a stale CurrentItem (e.g. Staff=6 on manga)
                // explodes inside ViewPager's Java internals with no managed trace
                var maxIndex = ViewModelLocator.AnimeDetails.AnimeMode ? 6 : 5;
                if (AnimeDetailsPagePivot.CurrentItem > maxIndex)
                    AnimeDetailsPagePivot.SetCurrentItem(0, false);
                AnimeDetailsPagePivot.ClearTabHeights();
                AnimeDetailsPagePivot.Adapter.NotifyDataSetChanged();
            }));

            Bindings.Add(this.SetBinding(() => ViewModel.AddAnimeVisibility)
                .WhenSourceChanges(() =>
                {
                    AnimeDetailsPageQuickAddToListButton.Visibility = ViewModel.AddAnimeVisibility
                        ? ViewStates.Visible
                        : ViewStates.Gone;

                if (ViewModel.AddAnimeVisibility)
                {
                    AnimeDetailsPageIncDecSection.Visibility = ViewStates.Gone;
                    AnimeDetailsPageUpdateSection.Visibility = ViewStates.Gone;
                    AnimeDetailsPageFavouriteButton.Visibility = ViewStates.Gone;
                }
                else
                {
                    AnimeDetailsPageIncDecSection.Visibility = ViewStates.Visible;
                    AnimeDetailsPageUpdateSection.Visibility = ViewStates.Visible;
                    AnimeDetailsPageFavouriteButton.Visibility = ViewStates.Visible;
                }
                }));

            Bindings.Add(
                this.SetBinding(() => ViewModel.DetailsPivotSelectedIndex)
                    .WhenSourceChanges(
                        () =>
                        {
                            AnimeDetailsPagePivot.SetCurrentItem(ViewModel.DetailsPivotSelectedIndex, true);
                        }));

            Bindings.Add(
                this.SetBinding(() => ViewModel.IsFavourite)
                    .WhenSourceChanges(() =>
                    {
                        if (ViewModel.IsFavourite)
                        {
                            AnimeDetailsPageFavouriteButton.ImageTintList = ColorStateList.ValueOf(Color.White);
                            AnimeDetailsPageFavouriteButton.SetImageResource(Resource.Drawable.icon_favourite);
                            AnimeDetailsPageFavouriteButton.SetBackgroundResource(ResourceExtension.AccentColourRes);
                        }
                        else
                        {
                            AnimeDetailsPageFavouriteButton.ImageTintList = ColorStateList.ValueOf(new Color(ResourceExtension.BrushText));
                            AnimeDetailsPageFavouriteButton.SetImageResource(Resource.Drawable.icon_unfavourite);
                            AnimeDetailsPageFavouriteButton.SetBackgroundColor(Color.Transparent);
                        }
                    }));

            Bindings.Add(this.SetBinding(() => ViewModel.AnimeMode)
                .WhenSourceChanges(() =>
                {
                    if (ViewModel.AnimeMode)
                    {
                        AnimeDetailsPageReadVolumesButton.Visibility =
                            AnimeDetailsPageReadVolumesLabel.Visibility = ViewStates.Gone;
                    }
                    else
                    {
                        AnimeDetailsPageReadVolumesButton.Visibility =
                            AnimeDetailsPageReadVolumesLabel.Visibility = ViewStates.Visible;
                    }
                }));

            Bindings.Add(this.SetBinding(() => ViewModel.DetailImage)
                .WhenSourceChanges(() =>
                {
                    AnimeDetailsPageBlurredBackground.Into(ViewModel.DetailImage, new BlurredTransformation(25));
                    AnimeDetailsPageShowCoverImage.Into(ViewModel.DetailImage);
                }));

            Bindings.Add(this.SetBinding(() => ViewModel.Title)
                .WhenSourceChanges(() =>
                {
                    AnimeDetailsPageTitle.Text = ViewModel.Title;
                }));

            Bindings.Add(this.SetBinding(() => ViewModel.AnimeMode)
                .WhenSourceChanges(() =>
                {
                    // Initial fallback — will be replaced when Type loads from API
                    if (string.IsNullOrEmpty(ViewModel.Type))
                        AnimeDetailsPageTypeBadge.Text = ViewModel.AnimeMode ? "Anime" : "Manga";
                }));

            Bindings.Add(this.SetBinding(() => ViewModel.Type)
                .WhenSourceChanges(() =>
                {
                    if (!string.IsNullOrEmpty(ViewModel.Type))
                        AnimeDetailsPageTypeBadge.Text = ViewModel.Type;
                }));

            Bindings.Add(this.SetBinding(() => ViewModel.StartYear)
                .WhenSourceChanges(() =>
                {
                    AnimeDetailsPageYearLabel.Text = ViewModel.StartYear ?? "";
                }));

            Bindings.Add(this.SetBinding(() => ViewModel.AllEpisodes)
                .WhenSourceChanges(() =>
                {
                    var eps = ViewModel.AnimeItemReference?.AllEpisodes ?? ViewModel.AllEpisodes;
                    var unit = ViewModel.AnimeMode ? "Episodes" : "Chapters";
                    AnimeDetailsPageSubtitle.Text = $"{(eps == 0 ? "?" : eps.ToString())} {unit}";
                }));

            Bindings.Add(this.SetBinding(() => ViewModel.LoadingGlobal)
                .WhenSourceChanges(() =>
                {
                    if (!ViewModel.LoadingGlobal && ViewModel.AnimeItemReference != null)
                    {
                        var score = ViewModel.AnimeItemReference.GlobalScore;
                        AnimeDetailsPageScoreValue.Text = score == 0 ? "N/A" : score.ToString("N2");
                    }
                }));

            Bindings.Add(
                this.SetBinding(() => ViewModel.LoadingUpdate,
                    () => AnimeDetailsPageLoadingUpdateSpinner.Visibility)
                    .ConvertSourceToTarget(Converters.BoolToVisibility));

            Bindings.Add(this.SetBinding(() => ViewModel.TrailerUrl).WhenSourceChanges(() =>
            {
                AnimeDetailsPageTrailerButton.Visibility =
                    string.IsNullOrEmpty(ViewModel.TrailerUrl) ? ViewStates.Gone : ViewStates.Visible;
            }));

            AnimeDetailsPageTrailerButton.SetOnClickListener(new OnClickListener(view =>
            {
                ViewModel.PlayVideoInApp(ViewModel.TrailerUrl);
            }));

            ViewModel.RequestVideoPlayback += ShowVideoOverlay;

            AnimeDetailsPageVideoCloseButton.SetOnClickListener(new OnClickListener(view => HideVideoOverlay()));

            Bindings.Add(
                this.SetBinding(() => ViewModel.IsAddAnimeButtonEnabled,
                    () => AnimeDetailsPageAddButton.Enabled));

            AnimeDetailsPageFavouriteButton.SetOnClickListener(
                new OnClickListener(view => ViewModel.ToggleFavouriteCommand.Execute(null)));
            AnimeDetailsPageIncrementButton.SetOnClickListener(
                new OnClickListener(view => ViewModel.IncrementEpsCommand.Execute(null)));
            AnimeDetailsPageDecrementButton.SetOnClickListener(
                new OnClickListener(view => ViewModel.DecrementEpsCommand.Execute(null)));
            AnimeDetailsPageAddButton.SetOnClickListener(
                new OnClickListener(view => ViewModel.AddAnimeCommand.Execute(null)));
            AnimeDetailsPageMoreButton.SetOnClickListener(new OnClickListener(view =>
            {
                _menu = AnimeDetailsPageMoreFlyoutBuilder.BuildForAnimeDetailsPage(Activity, ViewModel,
                    AnimeDetailsPageMoreButton,
                    OnMoreFlyoutClick);
                _menu.Show();
            }));
            AnimeDetailsPageQuickAddToListButton.SetOnClickListener(
                new OnClickListener(view =>
                {
                    if (ViewModel.AddAnimeVisibility)
                        ViewModel.AddAnimeCommand.Execute(null);
                }));
            AnimeDetailsPageQuickFavoriteButton.SetOnClickListener(
                new OnClickListener(view => ViewModel.ToggleFavouriteCommand.Execute(null)));

            //OneTime

            AnimeDetailsPageQuickAddToListButton.Visibility = ViewModel.AddAnimeVisibility
                ? ViewStates.Visible
                : ViewStates.Gone;

            AnimeDetailsPageWatchedLabel.Text = ViewModel.WatchedEpsLabel;

            AnimeDetailsPageTitle.Text = ViewModel.Title;
            AnimeDetailsPageTypeBadge.Text = ViewModel.Type ?? (ViewModel.AnimeMode ? "Anime" : "Manga");
            var eps = ViewModel.AnimeItemReference?.AllEpisodes ?? ViewModel.AllEpisodes;
            var unit = ViewModel.AnimeMode ? "Episodes" : "Chapters";
            AnimeDetailsPageSubtitle.Text = $"{(eps == 0 ? "?" : eps.ToString())} {unit}";
            if (ViewModel.AnimeItemReference != null)
            {
                var score = ViewModel.AnimeItemReference.GlobalScore;
                AnimeDetailsPageScoreValue.Text = score == 0 ? "N/A" : score.ToString("N2");
            }

            if (Settings.HideDecrementButtons)
            {
                AnimeDetailsPageDecrementButton.Visibility = ViewStates.Gone;
                AnimeDetailsPageIncrementButton.LayoutParameters.Width =
                    DimensionsHelper.DpToPx(45);
                AnimeDetailsPageIncrementButton.LayoutParameters.Height =
                    DimensionsHelper.DpToPx(45);
            }

            //Events
            AnimeDetailsPageStatusButton.SetOnClickListener(
                new OnClickListener(view => AnimeDetailsPageStatusButtonOnClick()));
            AnimeDetailsPageScoreButton.SetOnClickListener(
                new OnClickListener(view => AnimeDetailsPageScoreButtonOnClick()));
            AnimeDetailsPageWatchedButton.SetOnClickListener(
                new OnClickListener(view => AnimeDetailsPageWatchedButtonOnClick()));
            AnimeDetailsPageReadVolumesButton.SetOnClickListener(
                new OnClickListener(view => AnimeDetailsPageVolumesButtonOnClick()));

            // Poster scroll effect: zoom-in on scroll
            var heroHeight = DimensionsHelper.DpToPx(520);
            _scrollListener = new ScrollListener(AnimeDetailsPageScrollView, AnimeDetailsPagePosterContainer, heroHeight);
            AnimeDetailsPageScrollView.ViewTreeObserver.AddOnScrollChangedListener(_scrollListener);

            // Pull-to-refresh
            AnimeDetailsPageSwipeRefresh.ScrollingView = AnimeDetailsPageScrollView;
            AnimeDetailsPageSwipeRefresh.Refresh += (s, e) =>
            {
                ViewModel.RefreshData();
                AnimeDetailsPageSwipeRefresh.Refreshing = false;
            };
        }

        private void ShowVideoOverlay(string url)
        {
            if (string.IsNullOrEmpty(url) || !IsAdded)
                return;
            AnimeDetailsPageVideoWebView.Settings.JavaScriptEnabled = true;
            AnimeDetailsPageVideoWebView.Settings.MediaPlaybackRequiresUserGesture = false;
            AnimeDetailsPageVideoWebView.SetWebChromeClient(new WebChromeClient());

            var videoId = Web.InlineVideoWebViewClient.ExtractYouTubeId(url);
            string html;
            if (!string.IsNullOrEmpty(videoId))
            {
                // YouTube embed (trailer)
                html = "<html><head><meta name='viewport' content='width=device-width,initial-scale=1'/>" +
                       "<style>body{margin:0;padding:0;background:#000;overflow:hidden}" +
                       "iframe{position:absolute;top:0;left:0;width:100%;height:100%;border:none}</style></head>" +
                       "<body><iframe src='https://www.youtube.com/embed/" + videoId + "?autoplay=1' " +
                       "allow='autoplay;encrypted-media;fullscreen' allowfullscreen></iframe></body></html>";
            }
            else
            {
                // Direct video (AnimeThemes WebM) with EM-styled controls
                html = BuildVideoPlayerHtml(url);
            }
            AnimeDetailsPageVideoOverlay.Visibility = ViewStates.Visible;
            AnimeDetailsPageVideoWebView.LoadDataWithBaseURL("https://myanimelist.net", html, "text/html", "utf-8", null);

            // Back button closes the video overlay instead of navigating away
            ViewModelLocator.NavMgr.RegisterOneTimeOverride(new RelayCommand(() =>
            {
                HideVideoOverlay();
            }));
        }

        public void ShowVideoLoading()
        {
            if (!IsAdded) return;
            AnimeDetailsPageVideoWebView.LoadUrl("about:blank");
            AnimeDetailsPageVideoOverlay.Visibility = ViewStates.Visible;
        }

        public void HideVideoLoading()
        {
            if (!IsAdded) return;
            AnimeDetailsPageVideoOverlay.Visibility = ViewStates.Gone;
        }

        private static string BuildVideoPlayerHtml(string videoUrl)
        {
            return "<html><head><meta name='viewport' content='width=device-width,initial-scale=1'/>" +
                "<style>" +
                "*{box-sizing:border-box}" +
                "body{margin:0;padding:0;background:#000;overflow:hidden;font-family:sans-serif;user-select:none}" +
                "#video{position:absolute;top:0;left:0;width:100%;height:calc(100% - 48px);object-fit:contain;background:#000}" +
                "#controls{position:absolute;bottom:0;left:0;right:0;height:48px;background:rgba(5,21,34,0.92);display:flex;align-items:center;padding:0 12px;z-index:10}" +
                "#playBtn{background:none;border:none;color:#fff;font-size:20px;cursor:pointer;margin-right:10px;padding:4px}" +
                "#progressContainer{flex:1;height:4px;background:rgba(255,255,255,0.15);border-radius:2px;cursor:pointer;position:relative}" +
                "#progressFill{height:100%;background:#0066FF;border-radius:2px;width:0%;pointer-events:none}" +
                "#timeDisplay{color:#d4e4f7;font-size:11px;margin-left:10px;white-space:nowrap}" +
                "</style></head><body>" +
                "<video id='video' src='" + videoUrl + "' autoplay playsinline webkit-playsinline></video>" +
                "<div id='controls'>" +
                "<button id='playBtn'>&#9208;</button>" +
                "<div id='progressContainer'><div id='progressFill'></div></div>" +
                "<span id='timeDisplay'>0:00 / 0:00</span>" +
                "</div>" +
                "<script>" +
                "var v=document.getElementById('video');" +
                "var btn=document.getElementById('playBtn');" +
                "var fill=document.getElementById('progressFill');" +
                "var container=document.getElementById('progressContainer');" +
                "var time=document.getElementById('timeDisplay');" +
                "btn.onclick=function(e){e.stopPropagation();v.paused?v.play():v.pause()};" +
                "v.onplay=function(){btn.innerHTML='&#9208;'};" +
                "v.onpause=function(){btn.innerHTML='&#9654;'};" +
                "v.ontimeupdate=function(){if(v.duration){fill.style.width=((v.currentTime/v.duration)*100)+'%';time.textContent=fmt(v.currentTime)+' / '+fmt(v.duration)}};" +
                "container.onclick=function(e){var r=container.getBoundingClientRect();v.currentTime=((e.clientX-r.left)/r.width)*v.duration};" +
                "function fmt(s){s=Math.floor(s);return Math.floor(s/60)+':'+('0'+s%60).slice(-2)}" +
                "</script></body></html>";
        }

        private void HideVideoOverlay()
        {
            AnimeDetailsPageVideoOverlay.Visibility = ViewStates.Gone;
        }

        private async void OnMoreFlyoutClick(int i)
        {
            switch (i)
            {
                case 0:
                    ViewModel.NavigateForumBoardCommand.Execute(null);
                    break;

                case 1:
                    AnimeDetailsPageDialogBuilder.BuildPromotionalVideoDialog(ViewModel);
                    break;

                case 2:
                    AnimeUpdateDialogBuilder.BuildTagDialog(ViewModel);
                    break;

                case 3:
                    ViewModel.CopyToClipboardCommand.Execute(null);
                    break;

                case 4:
                    ViewModel.OpenInMalCommand.Execute(null);
                    break;

                case 5:
                    ViewModel.RemoveAnimeCommand.Execute(null);
                    break;

                case 6:
                    ViewModel.IsRewatching = !ViewModel.IsRewatching;
                    break;

                case 7:
                    ViewModel.CopyTitleToClipboardCommand.Execute(null);
                    break;

                case 8:
                    var counts = Enumerable.Range(0, 10);
                    var pickerResult = await ShowItemsPicker(counts.Select(i1 => i1.ToString()), 0,
                        "Times rewatched", "Cancel", "Ok");
                    if(pickerResult.HasValue)
                        ViewModel.SetRewatchingCountCommand.Execute(pickerResult.Value);
                    break;
            }
            _menu?.Dismiss(true);
            _menu = null;
        }

        public async Task<int?> ShowItemsPicker(
            IEnumerable<string> items,
            int selectedIndex,
            string title,
            string cancelText,
            string okText)
        {
            var semaphore = new SemaphoreSlim(0);
            var builder = new AlertDialog.Builder(Context);

            int? selectedItem = selectedIndex;
            builder.SetTitle(title);
            builder.SetSingleChoiceItems(items.ToArray(), selectedIndex, (sender, args) =>
            {
                selectedItem = args.Which;
            });
            builder.SetNegativeButton(cancelText, (sender, args) =>
            {
                selectedItem = null;
                semaphore.Release();
            });
            builder.SetPositiveButton(okText, (sender, args) => semaphore.Release());

            var dialog = builder.Create();
            dialog.SetCanceledOnTouchOutside(false);
            dialog.SetCancelable(false);
            dialog.Show();

            await semaphore.WaitAsync();
            dialog.Dismiss();

            return selectedItem;
        }

        private void AnimeDetailsPageWatchedButtonOnClick()
        {
            AnimeUpdateDialogBuilder.BuildWatchedDialog(ViewModel.AnimeItemReference as AnimeItemViewModel,
                (model, s) =>
                {
                    if (ViewModel != null)
                    {
                        ViewModel.WatchedEpsInput = s;
                        ViewModel.ChangeWatchedCommand.Execute(null);
                    }
                    else
                    {
                        ResourceLocator.SnackbarProvider.ShowText("Failed to update.");
                    }

                });
        }

        private void AnimeDetailsPageScoreButtonOnClick()
        {
            AnimeUpdateDialogBuilder.BuildScoreDialog(ViewModel.AnimeItemReference, i =>
            {
                ViewModel.ChangeScoreCommand.Execute(i.ToString());
            });
        }

        private void AnimeDetailsPageStatusButtonOnClick()
        {
            AnimeUpdateDialogBuilder.BuildStatusDialog(ViewModel.AnimeItemReference, ViewModel.AnimeMode, status =>
             {
                 ViewModel.ChangeStatus(status);
             });
        }

        private void AnimeDetailsPageVolumesButtonOnClick()
        {
            AnimeUpdateDialogBuilder.BuildWatchedDialog(ViewModel.AnimeItemReference as AnimeItemViewModel,
                (model, s) =>
                {
                    if (ViewModel != null)
                    {
                        ViewModel.ReadVolumesInput = s;
                        ViewModel.ChangeVolumesCommand.Execute(null);
                    }
                    else
                    {
                        ResourceLocator.SnackbarProvider.ShowText("Failed to update.");
                    }
                }, true);
        }

        public override int LayoutResourceId => Resource.Layout.AnimeDetailsPage;

        public override void DetachBindings()
        {
            ViewModel.RequestVideoPlayback -= ShowVideoOverlay;
            if (_scrollListener != null)
            {
                AnimeDetailsPageScrollView?.ViewTreeObserver?.RemoveOnScrollChangedListener(_scrollListener);
                _scrollListener = null;
            }
            base.DetachBindings();
        }

        private class ScrollListener : Java.Lang.Object, ViewTreeObserver.IOnScrollChangedListener
        {
            private readonly ScrollView _scrollView;
            private readonly View _poster;
            private readonly int _heroHeight;

            public ScrollListener(ScrollView scrollView, View poster, int heroHeight)
            {
                _scrollView = scrollView;
                _poster = poster;
                _heroHeight = heroHeight;
            }

            public void OnScrollChanged()
            {
                var scrollY = _scrollView.ScrollY;
                var ratio = Math.Max(0f, Math.Min(1f, (float)scrollY / _heroHeight));
                var scale = 1f + ratio * 0.3f;
                _poster.ScaleX = scale;
                _poster.ScaleY = scale;
            }
        }
    }
}


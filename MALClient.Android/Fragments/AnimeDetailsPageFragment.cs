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
using Android.Support.Design.Widget;
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
        private class AppBarOffsetListener : Java.Lang.Object, AppBarLayout.IOnOffsetChangedListener
        {
            private readonly Action<AppBarLayout, int> _onOffset;

            public AppBarOffsetListener(Action<AppBarLayout, int> onOffset)
            {
                _onOffset = onOffset;
            }

            public void OnOffsetChanged(AppBarLayout appBarLayout, int verticalOffset)
                => _onOffset?.Invoke(appBarLayout, verticalOffset);
        }

        private AnimeDetailsPageNavigationArgs _navArgs;
        private AnimeDetailsPageViewModel ViewModel;
        private DroppyMenuPopup _menu;

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
            var maxTabs = ViewModelLocator.AnimeDetails.AnimeMode ? 8 : 5;
            var initialTab = Math.Max(0, Math.Min(_navArgs.SourceTabIndex, maxTabs - 1));
            AnimeDetailsPagePivot.SetCurrentItem(initialTab, false);

            AnimeDetailsPageTabStrip.OnPageChangeListener =
                new OnPageChangedListener(i =>
                {
                    ViewModel.DetailsPivotSelectedIndex = i;
                    AnimeDetailsPagePivot.RequestLayout();
                });

            AnimeDetailsPageAppBar.AddOnOffsetChangedListener(new AppBarOffsetListener((bar, offset) =>
            {
                var totalRange = bar.TotalScrollRange;
                if (totalRange == 0)
                {
                    return;
                }
                var ratio = (float)Math.Abs(offset) / totalRange;

                // Zoom the poster in as the hero collapses (fills more of the
                // frame's width). fitCenter keeps the poster complete; the parent
                // clipChildren="false" lets it grow past the poster frame.
                var scale = 1f + 0.35f * ratio;
                AnimeDetailsPagePosterContainer.ScaleX = scale;
                AnimeDetailsPagePosterContainer.ScaleY = scale;

                // Show a translucent dark scrim over the collapsed header area, so
                // the hero poster stays visible through it. The scrim is a vertical
                // gradient anchored at the bottom (transparent at top, darker at the
                // bottom) so the darkening "rises from the bottom up" as it fades in.
                // At ratio==0 (expanded) it is fully transparent; at ratio==1
                // (fully collapsed) the gradient is shown at full strength.
                AnimeDetailsPageHeroScrim.Alpha = ratio;
            }));

            Bindings.Add(
                this.SetBinding(() => ViewModel.MyScoreBind,
                    () => AnimeDetailsPageScoreButton.Text));
            Bindings.Add(
                this.SetBinding(() => ViewModel.MyScoreBind,
                    () => AnimeDetailsPageQuickScoreButton.Text));
            Bindings.Add(
                this.SetBinding(() => ViewModel.MyStatusBind)
                    .WhenSourceChanges(() =>
                    {
                        AnimeDetailsPageStatusButton.Text = ViewModel.MyStatusBind;
                    }));
            Bindings.Add(
                this.SetBinding(() => ViewModel.MyEpisodesBind)
                    .WhenSourceChanges(() =>
                    {
                        AnimeDetailsPageWatchedButton.Text = ViewModel.MyEpisodesBind;
                    }));
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

            ViewModel.PropertyChanged += OnAnimeDetailsPivotPropertyChanged;

            Bindings.Add(this.SetBinding(() => ViewModel.AddAnimeVisibility)
                .WhenSourceChanges(() =>
                {
                    AnimeDetailsPageQuickAddToListButton.Visibility = ViewModel.AddAnimeVisibility
                        ? ViewStates.Visible
                        : ViewStates.Gone;
                    AnimeDetailsPageQuickScoreButton.Visibility = ViewModel.AddAnimeVisibility
                        ? ViewStates.Gone
                        : ViewStates.Visible;

                if (ViewModel.AddAnimeVisibility)
                {
                    AnimeDetailsPageIncDecSection.Visibility = ViewStates.Gone;
                    AnimeDetailsPageUpdateSection.Visibility = ViewStates.Gone;
                }
                else
                {
                    AnimeDetailsPageIncDecSection.Visibility = ViewStates.Visible;
                    AnimeDetailsPageUpdateSection.Visibility = ViewStates.Visible;
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
                            AnimeDetailsPageQuickFavoriteButton.SetCompoundDrawablesRelativeWithIntrinsicBounds(
                                Resource.Drawable.icon_heart_white, 0, 0, 0);
                        }
                        else
                        {
                            AnimeDetailsPageQuickFavoriteButton.SetCompoundDrawablesRelativeWithIntrinsicBounds(
                                Resource.Drawable.icon_heart_outline, 0, 0, 0);
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
                    if (string.IsNullOrEmpty(ViewModel.Type))
                    {
                        var fallbackType = ViewModel.AnimeMode ? "Anime" : "Manga";
                        AnimeDetailsPageTypeBadge.Text = fallbackType;
                    }
                }));

            Bindings.Add(this.SetBinding(() => ViewModel.Type)
                .WhenSourceChanges(() =>
                {
                    if (!string.IsNullOrEmpty(ViewModel.Type))
                    {
                        AnimeDetailsPageTypeBadge.Text = ViewModel.Type;
                    }
                }));

            Bindings.Add(this.SetBinding(() => ViewModel.StartYear)
                .WhenSourceChanges(() =>
                {
                    var year = ViewModel.StartYear ?? "";
                    AnimeDetailsPageYearLabel.Text = year;
                }));

            Bindings.Add(this.SetBinding(() => ViewModel.AllEpisodes)
                .WhenSourceChanges(() =>
                {
                    var eps = ViewModel.AnimeItemReference?.AllEpisodes ?? ViewModel.AllEpisodes;
                    var unit = ViewModel.AnimeMode ? "Episodes" : "Chapters";
                    var subtitle = $"{(eps == 0 ? "?" : eps.ToString())} {unit}";
                    AnimeDetailsPageSubtitle.Text = subtitle;
                }));

            Bindings.Add(this.SetBinding(() => ViewModel.LoadingGlobal)
                .WhenSourceChanges(() =>
                {
                    if (!ViewModel.LoadingGlobal && ViewModel.AnimeItemReference != null)
                    {
                        var score = ViewModel.AnimeItemReference.GlobalScore;
                        var scoreText = score == 0 ? "N/A" : score.ToString("N2");
                        AnimeDetailsPageScoreValue.Text = scoreText;
                    }
                }));

            Bindings.Add(
                this.SetBinding(() => ViewModel.LoadingUpdate,
                    () => AnimeDetailsPageLoadingUpdateSpinner.Visibility)
                    .ConvertSourceToTarget(Converters.BoolToVisibility));

            Bindings.Add(this.SetBinding(() => ViewModel.TrailerUrl).WhenSourceChanges(UpdateTrailerButtonVisibility));
            Bindings.Add(this.SetBinding(() => ViewModel.AddAnimeVisibility).WhenSourceChanges(UpdateTrailerButtonVisibility));

            Bindings.Add(this.SetBinding(() => ViewModel.Status).WhenSourceChanges(() =>
            {
                var isAiring = string.Equals(ViewModel.Status, "Currently Airing",
                    StringComparison.CurrentCultureIgnoreCase);
                AnimeDetailsPageAiringBadge.Visibility = isAiring ? ViewStates.Visible : ViewStates.Gone;
                if (isAiring)
                    _ = ViewModel.LoadEpisodes();
                UpdateAiringCountdown();
            }));

            Bindings.Add(this.SetBinding(() => ViewModel.TimeTillNextAir).WhenSourceChanges(UpdateAiringCountdown));

            AnimeDetailsPageTrailerButton.SetOnClickListener(new OnClickListener(view =>
            {
                if (ViewModel.AddAnimeVisibility)
                    AnimeDetailsPageDialogBuilder.BuildPromotionalVideoDialog(ViewModel);
                else
                    ViewModel.PlayVideoInApp(ViewModel.TrailerUrl);
            }));

            ViewModel.RequestVideoPlayback += ShowVideoOverlay;
            ViewModel.RequestWebNavigation += ShowWebOverlay;

            AnimeDetailsPageVideoCloseButton.SetOnClickListener(new OnClickListener(view => HideVideoOverlay()));

            Bindings.Add(
                this.SetBinding(() => ViewModel.IsAddAnimeButtonEnabled,
                    () => AnimeDetailsPageAddButton.Enabled));

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
            AnimeDetailsPageQuickScoreButton.SetOnClickListener(
                new OnClickListener(view => AnimeDetailsPageScoreButtonOnClick()));
            AnimeDetailsPageQuickFavoriteButton.SetOnClickListener(
                new OnClickListener(view =>
                {
                    ViewModel.ToggleFavouriteCommand.Execute(null);
                    Toast.MakeText(Activity, ViewModel.IsFavourite ? "Añadido a favoritos" : "Eliminado de favoritos", ToastLength.Short).Show();
                }));
            AnimeDetailsPageRefreshButton.SetOnClickListener(
                new OnClickListener(view => ViewModel.RefreshData()));

            //OneTime

            AnimeDetailsPageQuickAddToListButton.Visibility = ViewModel.AddAnimeVisibility
                ? ViewStates.Visible
                : ViewStates.Gone;
            AnimeDetailsPageQuickScoreButton.Visibility = ViewModel.AddAnimeVisibility
                ? ViewStates.Gone
                : ViewStates.Visible;

            AnimeDetailsPageWatchedLabel.Text = "EPISODES";

             AnimeDetailsPageTitle.Text = ViewModel.Title;
            AnimeDetailsPageTypeBadge.Text = ViewModel.Type ?? (ViewModel.AnimeMode ? "Anime" : "Manga");
            var eps = ViewModel.AnimeItemReference?.AllEpisodes ?? ViewModel.AllEpisodes;
            var unit = ViewModel.AnimeMode ? "Episodes" : "Chapters";
            var epsSubtitle = $"{(eps == 0 ? "?" : eps.ToString())} {unit}";
            AnimeDetailsPageSubtitle.Text = epsSubtitle;
            if (ViewModel.AnimeItemReference != null)
            {
                var score = ViewModel.AnimeItemReference.GlobalScore;
                var scoreText = score == 0 ? "N/A" : score.ToString("N2");
                AnimeDetailsPageScoreValue.Text = scoreText;
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
        }

        private void UpdateAiringCountdown()
        {
            var isAiring = string.Equals(ViewModel.Status, "Currently Airing",
                StringComparison.CurrentCultureIgnoreCase);
            if (isAiring && !string.IsNullOrEmpty(ViewModel.TimeTillNextAir))
            {
                AnimeDetailsPageAiringCountdown.Text = ViewModel.TimeTillNextAir;
                AnimeDetailsPageAiringCountdown.Visibility = ViewStates.Visible;
            }
            else
            {
                AnimeDetailsPageAiringCountdown.Visibility = ViewStates.Gone;
            }

            var showLastAired = isAiring && !string.IsNullOrEmpty(ViewModel.LastAired);
            if (showLastAired)
            {
                AnimeDetailsPageLastAiredValue.Text = ViewModel.LastAired;
                AnimeDetailsPageLastAiredSection.Visibility = ViewStates.Visible;
            }
            else
            {
                AnimeDetailsPageLastAiredSection.Visibility = ViewStates.Gone;
            }
        }

        private void ShowVideoOverlay(string url)
        {
            if (string.IsNullOrEmpty(url) || !IsAdded)
                return;
            AnimeDetailsPageVideoWebView.Settings.JavaScriptEnabled = true;
            AnimeDetailsPageVideoWebView.Settings.MediaPlaybackRequiresUserGesture = false;
            AnimeDetailsPageVideoWebView.SetWebChromeClient(new VideoOverlayWebChromeClient());
            try { AnimeDetailsPageVideoWebView.OnResume(); } catch { }

            var videoId = Web.InlineVideoWebViewClient.ExtractYouTubeId(url);
            string html;
            if (!string.IsNullOrEmpty(videoId))
            {
                html = "<html><head><meta name='viewport' content='width=device-width,initial-scale=1'/>" +
                       "<style>body{margin:0;padding:0;background:#000;overflow:hidden}" +
                       "iframe{position:absolute;top:0;left:0;width:100%;height:100%;border:none}</style></head>" +
                       "<body><iframe src='https://www.youtube.com/embed/" + videoId + "?autoplay=1' " +
                       "allow='autoplay;encrypted-media;fullscreen' allowfullscreen></iframe></body></html>";
            }
            else
            {
                html = BuildVideoPlayerHtml(url);
            }
            AnimeDetailsPageVideoOverlay.Visibility = ViewStates.Visible;
            global::Android.Util.Log.Info("MALPlus VideoOverlay", "Loading video HTML into WebView");
            AnimeDetailsPageVideoWebView.LoadDataWithBaseURL("https://myanimelist.net", html, "text/html", "utf-8", null);

            ViewModelLocator.NavMgr.RegisterOneTimeOverride(new RelayCommand(() =>
            {
                HideVideoOverlay();
            }));
        }

        private void ShowWebOverlay(string url)
        {
            if (string.IsNullOrEmpty(url) || !IsAdded)
                return;
            AnimeDetailsPageVideoWebView.Settings.JavaScriptEnabled = true;
            AnimeDetailsPageVideoWebView.SetWebChromeClient(new WebChromeClient());
            AnimeDetailsPageVideoOverlay.Visibility = ViewStates.Visible;
            AnimeDetailsPageVideoWebView.LoadUrl(url);
        }

        public void ShowVideoLoading()
        {
            if (!IsAdded) return;
            const string spinnerHtml =
                "<html><head><meta name='viewport' content='width=device-width,initial-scale=1'/><style>" +
                "@keyframes spin{to{transform:rotate(360deg)}}" +
                "body{margin:0;padding:0;background:#000;height:100%}" +
                "#s{position:absolute;top:50%;left:50%;width:44px;height:44px;margin:-22px 0 0 -22px;" +
                "border:4px solid rgba(255,255,255,0.2);border-top-color:#0066FF;border-radius:50%;animation:spin 1s linear infinite}" +
                "</style></head><body><div id='s'></div></body></html>";
            AnimeDetailsPageVideoWebView.Settings.JavaScriptEnabled = true;
            AnimeDetailsPageVideoWebView.SetWebChromeClient(new WebChromeClient());
            AnimeDetailsPageVideoOverlay.Visibility = ViewStates.Visible;
            AnimeDetailsPageVideoWebView.LoadDataWithBaseURL("https://myanimelist.net", spinnerHtml, "text/html", "utf-8", null);
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
                "#splash{position:absolute;top:0;left:0;width:100%;height:100%;background:#000;z-index:20;transition:opacity 0.3s;pointer-events:none}" +
                "#video{position:absolute;top:0;left:0;width:100%;height:calc(100% - 48px);object-fit:contain;background:#000}" +
                "#controls{position:absolute;bottom:0;left:0;right:0;height:48px;background:rgba(5,21,34,0.92);display:flex;align-items:center;padding:0 12px;z-index:10}" +
                "#playBtn{background:none;border:none;color:#fff;font-size:20px;cursor:pointer;margin-right:10px;padding:4px}" +
                "#progressContainer{flex:1;height:4px;background:rgba(255,255,255,0.15);border-radius:2px;cursor:pointer;position:relative}" +
                "#progressFill{height:100%;background:#0066FF;border-radius:2px;width:0%;pointer-events:none}" +
                "#timeDisplay{color:#d4e4f7;font-size:11px;margin-left:10px;white-space:nowrap}" +
                "</style></head><body>" +
                "<div id='splash'></div>" +
                "<video id='video' src='" + videoUrl + "' autoplay playsinline webkit-playsinline></video>" +
                "<div id='controls'>" +
                "<button id='playBtn' style='color:#ffffff;font-size:20px'>&#9208;</button>" +
                "<div id='progressContainer'><div id='progressFill'></div></div>" +
                "<span id='timeDisplay'>0:00 / 0:00</span>" +
                "</div>" +
                "<script>" +
                "var v=document.getElementById('video');" +
                "var splash=document.getElementById('splash');" +
                "var btn=document.getElementById('playBtn');" +
                "var fill=document.getElementById('progressFill');" +
                "var container=document.getElementById('progressContainer');" +
                "var time=document.getElementById('timeDisplay');" +
                "btn.onclick=function(e){e.stopPropagation();v.paused?v.play():v.pause()};" +
                "v.onplaying=function(){splash.style.opacity='0';};" +
                "v.onplay=function(){btn.innerHTML='<span style=\"color:#ffffff;font-size:20px\">&#9208;</span>';};" +
                "v.onpause=function(){btn.innerHTML='<span style=\"color:#ffffff;font-size:20px\">&#9654;</span>';};" +
                "v.onerror=function(){splash.style.opacity='0';console.error('video-error code='+(v.error?v.error.code:-1)+' net='+v.networkState);};" +
                "v.ontimeupdate=function(){if(v.duration){fill.style.width=((v.currentTime/v.duration)*100)+'%';time.textContent=fmt(v.currentTime)+' / '+fmt(v.duration)}};" +
                "container.onclick=function(e){e.stopPropagation();var r=container.getBoundingClientRect();v.currentTime=((e.clientX-r.left)/r.width)*v.duration};" +
                "function fmt(s){s=Math.floor(s||0);return Math.floor(s/60)+':'+('0'+s%60).slice(-2)}" +
                "setTimeout(function(){if(splash.style.opacity!=='0'){splash.style.opacity='0';console.log('DBG: timeout');}},5000);" +
                "</script></body></html>";
        }

        private void HideVideoOverlay()
        {
            try
            {
                if (AnimeDetailsPageVideoWebView != null)
                {
                    AnimeDetailsPageVideoWebView.LoadDataWithBaseURL(null, "<html><body></body></html>", "text/html", "utf-8", null);
                    AnimeDetailsPageVideoWebView.OnPause();
                }
            }
            catch { }
            AnimeDetailsPageVideoOverlay.Visibility = ViewStates.Gone;
        }

        private sealed class VideoOverlayWebChromeClient : WebChromeClient
        {
            public override bool OnConsoleMessage(ConsoleMessage consoleMessage)
            {
                var msg = consoleMessage.Message();
                if (string.IsNullOrEmpty(msg))
                    return base.OnConsoleMessage(consoleMessage);
                if (msg.StartsWith("video-error") || msg.StartsWith("video-stalled") || msg.Contains(" error ") || msg.Contains("failed"))
                    DiagnosticsReporter.Error("VideoOverlay", msg);
                else
                    global::Android.Util.Log.Info("MALPlus VideoOverlay", msg);
                return base.OnConsoleMessage(consoleMessage);
            }
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

        private void UpdateTrailerButtonVisibility()
        {
            var hasTrailer = !string.IsNullOrEmpty(ViewModel.TrailerUrl);
            AnimeDetailsPageTrailerButton.Visibility =
                ViewModel.AnimeMode && (ViewModel.AddAnimeVisibility || hasTrailer)
                    ? ViewStates.Visible
                    : ViewStates.Gone;
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

        public override void OnPause()
        {
            try { AnimeDetailsPageVideoWebView?.OnPause(); } catch { }
            HideVideoOverlay();
            base.OnPause();
        }

        private void OnAnimeDetailsPivotPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.PivotVersion)
                || e.PropertyName == nameof(ViewModel.AnimeMode)
                || e.PropertyName == nameof(ViewModel.Type))
            {
                var vm = ViewModelLocator.AnimeDetails;
                var episodesIncluded = vm.AnimeMode && vm.Type != "Movie";
                var maxIndex = vm.AnimeMode ? (episodesIncluded ? 7 : 6) : 4;
                var targetItem = AnimeDetailsPagePivot.CurrentItem <= maxIndex ? AnimeDetailsPagePivot.CurrentItem : 0;

                AnimeDetailsPagePivot.ClearTabHeights();
                var newAdapter = new AnimeDetailsPagerAdapter(ChildFragmentManager);
                newAdapter.ResetForAnimeChange();
                AnimeDetailsPagePivot.Adapter = newAdapter;
                AnimeDetailsPagePivot.SetCurrentItem(0, false);
                newAdapter.NotifyDataSetChanged();
                AnimeDetailsPagePivot.SetCurrentItem(targetItem, false);
                AnimeDetailsPageTabStrip.SetViewPager(AnimeDetailsPagePivot);
            }
        }

        public override void DetachBindings()
        {
            ViewModel.RequestVideoPlayback -= ShowVideoOverlay;
            ViewModel.RequestWebNavigation -= ShowWebOverlay;
            ViewModel.PropertyChanged -= OnAnimeDetailsPivotPropertyChanged;
            base.DetachBindings();
        }
    }
}


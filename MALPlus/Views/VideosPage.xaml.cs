using MALClient.Models.Models.AnimeScrapped;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;
using Microsoft.Maui.ApplicationModel;

namespace MALPlus.Views;

public partial class VideosPage : ContentPage
{
    private bool _initialized;
    private bool _overlayVisible;
    private PopularVideosViewModel Vm => (PopularVideosViewModel)BindingContext;

    public bool VideoOverlayVisibility
    {
        get => _overlayVisible;
        set { _overlayVisible = value; OnPropertyChanged(); }
    }

    public VideosPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.PopularVideos;
#if ANDROID
        VideoWebView.HandlerChanged += OnVideoWebViewHandlerChanged;
#endif
    }

#if ANDROID
    private void OnVideoWebViewHandlerChanged(object sender, EventArgs e)
    {
        try
        {
            var pv = VideoWebView?.Handler?.PlatformView as global::Android.Views.View;
            if (pv != null)
            {
                MALPlus.Services.VideoWebViewHelper.ConfigurePlatformView(pv);
                MALPlus.Services.VideoWebViewHelper.Resume(pv);
            }
        }
        catch { }
    }

    private void ResumeVideoWebView()
    {
        try
        {
            var pv = VideoWebView?.Handler?.PlatformView as global::Android.Views.View;
            if (pv != null) MALPlus.Services.VideoWebViewHelper.Resume(pv);
        }
        catch { }
    }

    private static void SetSystemBars(bool video)
    {
        try
        {
            var window = Platform.CurrentActivity?.Window;
            if (window == null) return;
            window.SetStatusBarColor(video
                ? global::Android.Graphics.Color.Black
                : global::Android.Graphics.Color.ParseColor("#051522"));
            window.SetNavigationBarColor(global::Android.Graphics.Color.Black);
        }
        catch { }
    }
#else
    private void ResumeVideoWebView() { }
    private static void SetSystemBars(bool video) { }
#endif

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;
        try
        {
            Vm.Init();
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(200);
                if (!Vm.Loading) break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS VideosPage Init failed: " + ex.Message);
        }
    }

    private void OnRefreshing(object sender, EventArgs e)
    {
        try { Vm.Init(); } catch { }
    }

    private void OnVideoTapped(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is AnimeVideoData video && !string.IsNullOrEmpty(video.YtLink))
            {
                ((CollectionView)sender).SelectedItem = null;
                ResumeVideoWebView();
                var embed = MALPlus.Services.VideoWebViewHelper.ToEmbedUrl(video.YtLink, autoplay: true);
                VideoWebView.Source = new HtmlWebViewSource
                {
                    Html = MALPlus.Services.VideoWebViewHelper.BuildEmbedHtml(embed),
                    BaseUrl = "https://myanimelist.net"
                };
                SetSystemBars(true);
                VideoOverlayVisibility = true;
                VideoOverlay.IsVisible = true;
                VideoListRoot.IsVisible = false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS video play failed: " + ex.Message);
        }
    }

    private void OnCloseVideo(object sender, EventArgs e)
    {
        try
        {
            VideoWebView.Source = null;
            VideoOverlay.IsVisible = false;
            VideoOverlayVisibility = false;
            VideoListRoot.IsVisible = true;
            SetSystemBars(false);
        }
        catch { }
    }
}

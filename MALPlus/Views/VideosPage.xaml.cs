using MALClient.Models.Models.AnimeScrapped;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

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
    }

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
                var embed = ToYouTubeEmbed(video.YtLink);
                var html = $"<!DOCTYPE html><html><head><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"/>" +
                           $"<style>html,body{{margin:0;background:#000;height:100%}}iframe{{width:100%;height:100%;border:0}}</style>" +
                           $"</head><body><iframe src=\"{embed}\" allowfullscreen></iframe></body></html>";
                VideoWebView.Source = new HtmlWebViewSource { Html = html, BaseUrl = "https://www.youtube.com" };
                VideoOverlayVisibility = true;
                VideoOverlay.IsVisible = true;
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
        }
        catch { }
    }

    private static string ToYouTubeEmbed(string url)
    {
        try
        {
            var m = System.Text.RegularExpressions.Regex.Match(url, @"youtube\.com/watch\?v=([\w\-]+)");
            if (m.Success) return $"https://www.youtube.com/embed/{m.Groups[1].Value}";
            m = System.Text.RegularExpressions.Regex.Match(url, @"youtu\.be/([\w\-]+)");
            if (m.Success) return $"https://www.youtube.com/embed/{m.Groups[1].Value}";
            return url;
        }
        catch { return url; }
    }
}

using System.Collections.ObjectModel;
using MALClient.XShared.Comm.Discovery;
using MALPlus.Services;

namespace MALPlus.Views;

[QueryProperty(nameof(ItemsJson), "items")]
[QueryProperty(nameof(HeaderText), "header")]
public partial class DiscoveryResultsPage : ContentPage
{
    private string _itemsJson;
    private string _headerText;
    private WebView _videoWebView;

    public ObservableCollection<DiscoveryItem> Results { get; } = new();

    public DiscoveryResultsPage()
    {
        InitializeComponent();
        ResultsList.ItemsSource = Results;
    }

    public string ItemsJson
    {
        get => _itemsJson;
        set
        {
            _itemsJson = value;
            Load();
        }
    }

    public string HeaderText
    {
        get => _headerText;
        set
        {
            _headerText = value;
            if (!string.IsNullOrEmpty(value))
                Title = value;
            HeadingLabel.Text = value;
        }
    }

    private void Load()
    {
        Results.Clear();
        if (string.IsNullOrWhiteSpace(_itemsJson))
            return;
        try
        {
            var items = System.Text.Json.JsonSerializer.Deserialize<List<DiscoveryItem>>(_itemsJson);
            if (items == null)
                return;
            foreach (var item in items)
                Results.Add(item);
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("MALPlus Discover", "results decode: " + ex.GetType().Name);
        }
        EmptyLabel.IsVisible = Results.Count == 0;
    }

    private async void OnItemSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not DiscoveryItem item)
            return;
        ((CollectionView)sender).SelectedItem = null;
        await OpenAsync(item);
    }

    private async Task OpenAsync(DiscoveryItem item)
    {
        try
        {
            if (!string.IsNullOrEmpty(item.ThemeUrl))
            {
                PlayTheme(item.ThemeUrl);
                return;
            }
            var qs = item.IsManga ? "&manga=true" : "";
            await Shell.Current.GoToAsync(
                $"animedetails?id={item.Id}&title={Uri.EscapeDataString(item.Title ?? "")}{qs}");
        }
        catch
        {
        }
    }

    private void PlayTheme(string url)
    {
        try
        {
            var webView = EnsureVideoWebView();
            VideoOverlay.IsVisible = true;
            VideoWebViewHelper.Resume(webView.Handler?.PlatformView as global::Android.Views.View);
            webView.Source = VideoWebViewHelper.BuildSource(url);
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("MALPlus Discover", "theme play: " + ex.GetType().Name);
        }
    }

    private WebView EnsureVideoWebView()
    {
        if (_videoWebView != null)
            return _videoWebView;
        _videoWebView = new WebView();
        Grid.SetRow(_videoWebView, 1);
        VideoOverlay.Add(_videoWebView);
#if ANDROID
        _videoWebView.HandlerChanged += (s, e) =>
        {
            var pv = _videoWebView?.Handler?.PlatformView as global::Android.Views.View;
            if (pv != null)
            {
                VideoWebViewHelper.ConfigurePlatformView(pv);
                VideoWebViewHelper.Resume(pv);
            }
        };
#endif
        return _videoWebView;
    }

    private void OnCloseVideo(object sender, EventArgs e)
    {
        try
        {
            if (_videoWebView != null)
                _videoWebView.Source = null;
            VideoOverlay.IsVisible = false;
        }
        catch
        {
        }
    }

    private async void OnClose(object sender, EventArgs e)
    {
        try
        {
            await Navigation.PopModalAsync();
        }
        catch
        {
        }
    }
}

using System;
using System.Globalization;
using System.Threading.Tasks;
using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Forums;
using Microsoft.Maui.ApplicationModel;

namespace MALPlus.Views;

[QueryProperty(nameof(TopicId), "id")]
public partial class ForumsTopicPage : ContentPage, ForumTopicViewModel.IScrollInfoProvider
{
    private bool _initialized;
    private bool _handlerAttached;
    public string TopicId { get; set; }

    private ForumTopicViewModel Vm => BindingContext as ForumTopicViewModel;

    public ForumsTopicPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.ForumsTopic;
        if (Vm != null)
            Vm.ScrollInfoProvider = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (Vm == null)
            return;
        Vm.PropertyChanged -= OnVmPropertyChanged;
        Vm.PropertyChanged += OnVmPropertyChanged;
#if ANDROID
        if (!_handlerAttached)
        {
            TopicWebView.HandlerChanged += OnTopicWebViewHandlerChanged;
            _handlerAttached = true;
        }
        ConfigureTopicWebView(TopicWebView?.Handler?.PlatformView as global::Android.Views.View);
#endif
        if (_initialized || string.IsNullOrWhiteSpace(TopicId))
            return;
        _initialized = true;
        try
        {
            await Vm.Init(new ForumsTopicNavigationArgs(TopicId, null, 1));
            await UpdateTopicWebViewHeightAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS ForumsTopicPage Init failed: " + ex.Message);
        }
    }

    protected override void OnDisappearing()
    {
        if (Vm != null)
            Vm.PropertyChanged -= OnVmPropertyChanged;
#if ANDROID
        if (_handlerAttached)
        {
            TopicWebView.HandlerChanged -= OnTopicWebViewHandlerChanged;
            _handlerAttached = false;
        }
#endif
        base.OnDisappearing();
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        if (Vm == null)
            return;
        try
        {
            await Vm.ReloadAsync();
            await UpdateTopicWebViewHeightAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS ForumsTopicPage refresh failed: " + ex.Message);
        }
    }

    private void OnVmPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ForumTopicViewModel.PageHtml))
            return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (TopicWebView == null)
                return;
            TopicWebView.HeightRequest = 1;
            _ = UpdateTopicWebViewHeightAsync();
        });
    }

    private async void OnTopicWebViewNavigated(object sender, WebNavigatedEventArgs e)
    {
        await UpdateTopicWebViewHeightAsync();
    }

    private async Task UpdateTopicWebViewHeightAsync()
    {
        try
        {
            var result = await MainThread.InvokeOnMainThreadAsync(() =>
                TopicWebView.EvaluateJavaScriptAsync(
                    "Math.max(1, Math.ceil(Math.max(document.documentElement.scrollHeight, document.body ? document.body.scrollHeight : 0, document.body ? document.body.getBoundingClientRect().height : 0)));"));
            if (!double.TryParse(result, NumberStyles.Float, CultureInfo.InvariantCulture, out var height))
                return;
            var targetHeight = Math.Max(1, Math.Ceiling(height) + 4);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                TopicWebView.HeightRequest = targetHeight;
            });
            // Images inside posts finish loading after the first pass and grow the
            // document, so measure again once they have settled.
            await Task.Delay(350);
            var second = await MainThread.InvokeOnMainThreadAsync(() =>
                TopicWebView.EvaluateJavaScriptAsync(
                    "Math.max(1, Math.ceil(Math.max(document.documentElement.scrollHeight, document.body ? document.body.scrollHeight : 0)));"));
            if (double.TryParse(second, NumberStyles.Float, CultureInfo.InvariantCulture, out var secondHeight) &&
                secondHeight > targetHeight - 4)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    TopicWebView.HeightRequest = Math.Max(1, Math.Ceiling(secondHeight) + 4);
                });
            }
        }
        catch { }
    }

#if ANDROID
    private void OnTopicWebViewHandlerChanged(object sender, EventArgs e)
    {
        ConfigureTopicWebView(TopicWebView?.Handler?.PlatformView as global::Android.Views.View);
    }

    private static void ConfigureTopicWebView(global::Android.Views.View platformView)
    {
        if (platformView is not global::Android.Webkit.WebView webView)
            return;
        try
        {
            webView.NestedScrollingEnabled = false;
            webView.VerticalScrollBarEnabled = false;
            webView.HorizontalScrollBarEnabled = false;
            webView.OverScrollMode = global::Android.Views.OverScrollMode.Never;
            webView.Settings.JavaScriptEnabled = true;
            webView.Settings.DomStorageEnabled = true;
            webView.Settings.BuiltInZoomControls = false;
            webView.Settings.DisplayZoomControls = false;
        }
        catch { }
    }
#endif

    public int GetFirstVisibleItemIndex()
    {
        return 0;
    }
}

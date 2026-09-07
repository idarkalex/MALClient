using MALClient.Models.Enums;
using MALClient.Models.Models.MalSpecific;
using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALPlus.Views;

[QueryProperty(nameof(InitialWorkMode), "mode")]
public partial class ArticlesPage : ContentPage
{
    private bool _initialized;
    private ArticlePageWorkMode _currentMode = ArticlePageWorkMode.Articles;
    public string InitialWorkMode { get; set; }

    private MalArticlesViewModel Vm => (MalArticlesViewModel)BindingContext;

    // EM theme CSS (Electric Midnight) — matches v2's "CssManager.GetArticleBody()".
    private const string EmCss = @"
body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,'Helvetica Neue',Arial,sans-serif;
background:#051522;color:#d4e4f7;line-height:1.6;padding:16px;margin:0;font-size:14px;}
h1,h2,h3{color:#FFFFFF;font-weight:600;line-height:1.3;margin:18px 0 10px;}
h1{font-size:20px;} h2{font-size:17px;} h3{font-size:15px;}
p{margin:0 0 12px;}
a{color:#0066FF;text-decoration:none;} a:hover{text-decoration:underline;}
img{max-width:100%;height:auto;border-radius:6px;margin:8px 0;background:#000;}
blockquote{border-left:3px solid #0066FF;margin:12px 0;padding:8px 14px;color:#a8c5e0;background:rgba(0,102,255,0.05);}
cite{font-style:italic;color:#a8c5e0;}
ul,ol{margin:0 0 12px 22px;padding:0;}
li{margin:4px 0;}
table{border-collapse:collapse;width:100%;margin:12px 0;font-size:13px;}
td,th{border:1px solid #1E3A52;padding:6px 10px;text-align:left;}
.intro,.meat{color:#d4e4f7;}
hr{border:none;border-top:1px solid #1E3A52;margin:18px 0;}
pre,code{background:#0a1d2e;padding:6px 10px;border-radius:4px;font-size:12px;color:#a8c5e0;overflow-x:auto;}
";

    public ArticlesPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.MalArticles;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;
        try
        {
            _currentMode = ResolveWorkMode(InitialWorkMode);
            Vm.Init(MakeArgs(_currentMode));
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(200);
                if (!Vm.LoadingVisibility) break;
            }
            HighlightTab(_currentMode);
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS ArticlesPage Init failed: " + ex.Message);
        }
    }

    private static ArticlePageWorkMode ResolveWorkMode(string s)
    {
        if (string.Equals(s, "News", StringComparison.OrdinalIgnoreCase)) return ArticlePageWorkMode.News;
        if (string.Equals(s, "AnnNews", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(s, "ANN", StringComparison.OrdinalIgnoreCase)) return ArticlePageWorkMode.AnnNews;
        return ArticlePageWorkMode.Articles;
    }

    private static MalArticlesPageNavigationArgs MakeArgs(ArticlePageWorkMode mode) => new()
    {
        WorkMode = mode,
        Source = PageIndex.PageMore
    };

    private void OnWorkModeClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Button b && b.CommandParameter is string s)
            {
                _currentMode = ResolveWorkMode(s);
                Vm.Init(MakeArgs(_currentMode), true);
                HighlightTab(_currentMode);
            }
        }
        catch { }
    }

    private void HighlightTab(ArticlePageWorkMode mode)
    {
        ArticlesTab.BackgroundColor = mode == ArticlePageWorkMode.Articles
            ? Color.FromArgb("#0066FF") : Colors.Transparent;
        ArticlesTab.TextColor = mode == ArticlePageWorkMode.Articles
            ? Colors.White : Color.FromArgb("#A0FFFFFF");
        NewsTab.BackgroundColor = mode == ArticlePageWorkMode.News
            ? Color.FromArgb("#0066FF") : Colors.Transparent;
        NewsTab.TextColor = mode == ArticlePageWorkMode.News
            ? Colors.White : Color.FromArgb("#A0FFFFFF");
        AnnTab.BackgroundColor = mode == ArticlePageWorkMode.AnnNews
            ? Color.FromArgb("#0066FF") : Colors.Transparent;
        AnnTab.TextColor = mode == ArticlePageWorkMode.AnnNews
            ? Colors.White : Color.FromArgb("#A0FFFFFF");
    }

    private void OnArticleTapped(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is MalNewsUnitModel item)
            {
                ((CollectionView)sender).SelectedItem = null;
                Vm.LoadArticleCommand.Execute(item);
                _ = LoadArticleHtmlAsync(item);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS article nav failed: " + ex.Message);
        }
    }

    private async Task LoadArticleHtmlAsync(MalNewsUnitModel item)
    {
        try
        {
            // wait for PendingArticle to be set
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(200);
                if (Vm.PendingArticle != null && Vm.PendingArticle.Id == item.Id) break;
            }
            if (Vm.PendingArticle == null) return;

            string? html = null;
            try
            {
                html = await MALClient.XShared.Comm.Articles.AnnNewsQuery.GetAnnArticleHtml(
                    Vm.PendingArticle.Url, Vm.PendingArticle.Id);
            }
            catch { }
            if (string.IsNullOrEmpty(html)) return;

            // MAL or ANN: base URL matters for relative images. For now default to myanimelist
            // (both MAL and ANN content uses absolute URLs mostly).
            var baseUrl = Vm.PendingArticle.Source == "ANN"
                ? "https://www.animenewsnetwork.com"
                : "https://myanimelist.net";

            var wrapped = "<html><head><meta charset='utf-8'/>" +
                          "<meta name='viewport' content='width=device-width,initial-scale=1'/>" +
                          "<style>" + EmCss + "</style></head><body>" + html + "</body></html>";
            ArticleWebView.Source = new HtmlWebViewSource { Html = wrapped, BaseUrl = baseUrl };
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS article html load failed: " + ex.Message);
        }
    }

    private void OnBackToList(object sender, EventArgs e)
    {
        try
        {
            Vm.PendingArticle = null;
            Vm.CurrentNews = -1;
            ArticleWebView.Source = null;
        }
        catch { }
    }

    private async void OnShareClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Button b && b.CommandParameter is MalNewsUnitModel item)
            {
                await Share.RequestAsync(new ShareTextRequest
                {
                    Text = item.Title + "\n" + item.Url,
                    Title = "Share article"
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS share failed: " + ex.Message);
        }
    }

    private void OnRefreshing(object sender, EventArgs e)
    {
        try
        {
            Vm.Init(MakeArgs(_currentMode), true);
        }
        catch { }
    }
}

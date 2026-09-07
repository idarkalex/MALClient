using MALClient.Models.Enums;
using MALClient.Models.Models.MalSpecific;
using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;
using MALClient.XShared.Utils;

namespace MALPlus.Views;

[QueryProperty(nameof(InitialWorkMode), "mode")]
public partial class ArticlesPage : ContentPage
{
    private bool _initialized;
    public string InitialWorkMode { get; set; }

    private MalArticlesViewModel Vm => (MalArticlesViewModel)BindingContext;

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
            var workMode = ResolveWorkMode(InitialWorkMode);
            Vm.Init(MakeArgs(workMode));
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(200);
                if (!Vm.LoadingVisibility) break;
            }
            HighlightTab(workMode);
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
                var mode = ResolveWorkMode(s);
                Vm.Init(MakeArgs(mode), true);
                HighlightTab(mode);
            }
        }
        catch { }
    }

    private void HighlightTab(ArticlePageWorkMode mode)
    {
        ArticlesTab.BackgroundColor = mode == ArticlePageWorkMode.Articles
            ? Color.FromArgb("#0066FF") : Colors.Transparent;
        ArticlesTab.TextColor = mode == ArticlePageWorkMode.Articles
            ? Colors.White : Color.FromArgb("#FFFFFF");
        NewsTab.BackgroundColor = mode == ArticlePageWorkMode.News
            ? Color.FromArgb("#0066FF") : Colors.Transparent;
        NewsTab.TextColor = mode == ArticlePageWorkMode.News
            ? Colors.White : Color.FromArgb("#FFFFFF");
        AnnTab.BackgroundColor = mode == ArticlePageWorkMode.AnnNews
            ? Color.FromArgb("#0066FF") : Colors.Transparent;
        AnnTab.TextColor = mode == ArticlePageWorkMode.AnnNews
            ? Colors.White : Color.FromArgb("#FFFFFF");
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
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(200);
                if (Vm.PendingArticle != null && Vm.PendingArticle.Id == item.Id) break;
            }
            if (Vm.PendingArticle == null) return;
            var html = await MALClient.XShared.Comm.Articles.AnnNewsQuery.GetAnnArticleHtml(
                Vm.PendingArticle.Url, Vm.PendingArticle.Id);
            if (string.IsNullOrEmpty(html)) return;
            var wrapped = "<html><head><meta name='viewport' content='width=device-width,initial-scale=1'/></head><body>"
                + html + "</body></html>";
            ArticleWebView.Source = new HtmlWebViewSource { Html = wrapped, BaseUrl = "https://myanimelist.net" };
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

    private void OnRefreshing(object sender, EventArgs e)
    {
        try
        {
            Vm.Init(MakeArgs(ArticlePageWorkMode.Articles), true);
        }
        catch { }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Android.OS;
using Android.Support.V4.View;
using Android.Webkit;
using Android.Widget;

using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Helpers;
using MALClient.Android.BindingConverters;
using MALClient.Android.PagerAdapters;
using MALClient.Android.Resources;

using MALClient.Android.Web;
using MALClient.Models.Enums;
using MALClient.Models.Models.MalSpecific;
using MALClient.XShared.Comm.Anime;
using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;
using MALClient.Android.Web;

namespace MALClient.Android.Fragments.ArticlesPageFragments
{
    public class ArticlesPageFragment : MalFragmentBase
    {
        private readonly MalArticlesPageNavigationArgs _args;
        private MalArticlesViewModel ViewModel;
        private ListenableWebClient _listenableWebClient;
        private bool _attachedHandler;

        public ArticlesPageFragment(MalArticlesPageNavigationArgs args)
        {
            _args = args;
        }

        protected override void Init(Bundle savedInstanceState)
        {
            ViewModel = ViewModelLocator.MalArticles;
            if (!_attachedHandler)
            {
                ViewModel.OpenWebView += ViewModelOnOpenWebView;
                _attachedHandler = true;
            }
            ViewModel.Init(_args);

            _listenableWebClient = new ListenableWebClient();
            _listenableWebClient.PageReady += PageReady;
            _listenableWebClient.NavigationInterceptOpportunity += NavigationInterceptOpportunity;
        }

        public override void OnDestroy()
        {
            if (ViewModel != null)
                ViewModel.OpenWebView -= ViewModelOnOpenWebView;
            _attachedHandler = false;
            base.OnDestroy();
        }

        protected override void InitBindings()
        {
            ArticlesPagePivot.Adapter = new ArticlesPagePagerAdapter(ChildFragmentManager);
            ArticlesPageTabStrip.SetViewPager(ArticlesPagePivot);
            ArticlesPageTabStrip.CenterTabs();


            ArticlesPageWebView.SetWebViewClient(_listenableWebClient);
            ArticlesPageWebView.SetWebChromeClient(new WebChromeClient());
            ArticlesPageWebView.Settings.JavaScriptEnabled = true;

            if (ViewModel.PendingArticle != null)
            {
                var article = ViewModel.PendingArticle;
                var age = DateTime.UtcNow - ViewModel.PendingArticleAt;
                ViewModel.PendingArticle = null;
                if (age < TimeSpan.FromSeconds(60))
                    ViewModel.LoadArticleCommand.Execute(article);
            }

            Bindings.Add(
                this.SetBinding(() => ViewModel.WebViewVisibility,
                    () => ArticlesPageWebView.Visibility).ConvertSourceToTarget(Converters.BoolToVisibility));

            Bindings.Add(
                this.SetBinding(() => ViewModel.LoadingVisibility,
                    () => ArticlesPageLoadingSpinner.Visibility).ConvertSourceToTarget(Converters.BoolToVisibility));         
        }

        public override int LayoutResourceId => Resource.Layout.ArticlesPage;
        private int _currentId;
        private async void ViewModelOnOpenWebView(string html, MalNewsUnitModel item)
        {
            ViewModelLocator.NavMgr.RegisterOneTimeOverride(new RelayCommand(() =>
            {
                ViewModel.WebViewVisibility = false;
                ViewModel.ArticleIndexVisibility = true;
            }));
            _currentId = ViewModel.Articles.IndexOf(item);
            ArticlesPageWebView.LoadDataWithBaseURL(null,ResourceLocator.CssManager.WrapWithCss(html), "text/html; charset=utf-8", "UTF-8",null);
        }


        private async Task<string> NavigationInterceptOpportunity(string targetUrl)
        {
            if (targetUrl != null)
            {
                // keep youtube videos inside the in-app reader
                if (targetUrl.Contains("youtube.com") || targetUrl.Contains("youtu.be"))
                {
                    var embed = ConvertToYouTubeEmbed(targetUrl);
                    if (embed != null)
                        return embed;
                }

                var navArgs = MalLinkParser.GetNavigationParametersForUrl(targetUrl);
                if (navArgs != null)
                {
                    var artArg = ViewModel.PrevWorkMode == ArticlePageWorkMode.Articles
                        ? MalArticlesPageNavigationArgs.Articles
                        : MalArticlesPageNavigationArgs.News;
                    artArg.NewsId = _currentId;

                    if (navArgs.Item1 != PageIndex.PageAnimeDetails)
                    {
                        ViewModelLocator.NavMgr.RegisterBackNav(PageIndex.PageArticles, artArg);
                    }
                    else
                    {
                        var arg = navArgs.Item2 as AnimeDetailsPageNavigationArgs;
                        arg.Source = PageIndex.PageArticles;                      
                        arg.PrevPageSetup = artArg;
                    }


                    ViewModelLocator.GeneralMain.Navigate(navArgs.Item1, navArgs.Item2);
                }
                else if (Settings.ArticlesLaunchExternalLinks)
                {
                    ResourceLocator.SystemControlsLauncherService.LaunchUri(new Uri(targetUrl));
                }
            }
            return null;
        }

        private static string ConvertToYouTubeEmbed(string url)
        {
            var match = System.Text.RegularExpressions.Regex.Match(url, @"(?:v=|youtu\.be/|/embed/)([A-Za-z0-9_\-]{6,})");
            return match.Success ? $"https://www.youtube.com/embed/{match.Groups[1].Value}" : null;
        }

        private void PageReady()
        {
            ViewModel.LoadingVisibility = false;
            ViewModel.WebViewVisibility = true;
        }

        #region Views

        private UserControls.PagerSlidingTabStrip _articlesPageTabStrip;
        private ViewPager _articlesPagePivot;
        private WebView _articlesPageWebView;
        private ProgressBar _articlesPageLoadingSpinner;

        public UserControls.PagerSlidingTabStrip ArticlesPageTabStrip => GetView(ref _articlesPageTabStrip, Resource.Id.ArticlesPageTabStrip);

        public ViewPager ArticlesPagePivot => GetView(ref _articlesPagePivot, Resource.Id.ArticlesPagePivot);

        public WebView ArticlesPageWebView => GetView(ref _articlesPageWebView, Resource.Id.ArticlesPageWebView);

        public ProgressBar ArticlesPageLoadingSpinner => GetView(ref _articlesPageLoadingSpinner, Resource.Id.ArticlesPageLoadingSpinner);

        #endregion
    }
}
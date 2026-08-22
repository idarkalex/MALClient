using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MALClient.Models.Enums;
using MALClient.Models.Models.MalSpecific;
using MALClient.XShared.Comm.Articles;

namespace MALClient.XShared.ViewModels.Main
{
    public class MalArticlesPageNavigationArgs
    {
        public ArticlePageWorkMode WorkMode { get; set; }
        public int NewsId { get; set; } = -1;
        public PageIndex Source { get; set; } = PageIndex.PageAnimeList;

        public static MalArticlesPageNavigationArgs Articles => new MalArticlesPageNavigationArgs {WorkMode = ArticlePageWorkMode.Articles};
        public static MalArticlesPageNavigationArgs News => new MalArticlesPageNavigationArgs {WorkMode = ArticlePageWorkMode.News};
    }

    public delegate void OpenWebViewRequest(string html,MalNewsUnitModel model);

    public class MalArticlesViewModel : ViewModelBase
    {
        private List<MalNewsUnitModel> _articles = new List<MalNewsUnitModel>();

        public List<MalNewsUnitModel> Articles
        {
            get { return _articles; }
            set
            {
                _articles = value;
                RaisePropertyChanged(() => Articles);
            }
        }

        public event OpenWebViewRequest OpenWebView;

        private ICommand _loadArticleCommand;

        public ICommand LoadArticleCommand
            => _loadArticleCommand ?? (_loadArticleCommand = new RelayCommand<MalNewsUnitModel>(LoadArticle));

        private bool _webViewVisibility = false;

        public bool WebViewVisibility
        {
            get { return _webViewVisibility; }
            set
            {
                _webViewVisibility = value;
                RaisePropertyChanged(() => WebViewVisibility);
            }
        }

        private bool _articleIndexVisibility = true;

        public bool ArticleIndexVisibility
        {
            get { return _articleIndexVisibility; }
            set
            {
                _articleIndexVisibility = value;
                RaisePropertyChanged(() => ArticleIndexVisibility);
            }
        }

        private bool _loadingVisibility = false;

        public bool LoadingVisibility
        {
            get { return _loadingVisibility; }
            set
            {
                if(_loadingData)
                    return;
                _loadingVisibility = value;
                
                RaisePropertyChanged(() => LoadingVisibility);
            }
        }

        private double _thumbnailWidth = 150;
        private double _thumbnailHeight = 150;

        public double ThumbnailWidth
        {
            get { return _thumbnailWidth; }
            set
            {
                _thumbnailWidth = value;
                RaisePropertyChanged(() => ThumbnailWidth);
            }
        }

        public double ThumbnailHeight
        {
            get { return _thumbnailHeight; }
            set
            {
                _thumbnailHeight = value;
                RaisePropertyChanged(() => ThumbnailHeight);
            }
        }

        private bool _loadingData;
        public ArticlePageWorkMode? PrevWorkMode;
        public int CurrentNews = -1;
        public MalNewsUnitModel PendingArticle;
        public DateTime PendingArticleAt { get; set; } = DateTime.MinValue;
        public async void Init(MalArticlesPageNavigationArgs args,bool force = false)
        {
            if (args == null) //refresh
            {
                switch (PrevWorkMode)
                {
                    case ArticlePageWorkMode.Articles:
                        args = MalArticlesPageNavigationArgs.Articles;
                        break;
                    case ArticlePageWorkMode.AnnNews:
                        args = new MalArticlesPageNavigationArgs { WorkMode = ArticlePageWorkMode.AnnNews };
                        break;
                    default:
                        args = MalArticlesPageNavigationArgs.News;
                        break;
                }
                force = true;
            }
            ArticleIndexVisibility = true;
            WebViewVisibility = false;
            ViewModelLocator.GeneralMain.CurrentStatus =
                args.WorkMode == ArticlePageWorkMode.Articles ? "Articles" :
                args.WorkMode == ArticlePageWorkMode.AnnNews ? "ANN News" : "News";

            if (PrevWorkMode == args.WorkMode && !force)
            {
                try
                {
                    if (args.NewsId != -1)
                        LoadArticle(Articles[args.NewsId]);
                }
                catch (Exception)
                {
                    //
                }
                return;
            }          
            LoadingVisibility = true;
            _loadingData = true;

            switch (args.WorkMode)
            {
                case ArticlePageWorkMode.Articles:
                    ThumbnailWidth = ThumbnailHeight = 150;
                    break;
                case ArticlePageWorkMode.News:
                    ThumbnailWidth = 100;
                    ThumbnailHeight = 150;
                    break;
                case ArticlePageWorkMode.AnnNews:
                    ThumbnailWidth = 100;
                    ThumbnailHeight = 150;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            PrevWorkMode = args.WorkMode;

            var data = new List<MalNewsUnitModel>();
            Articles = new List<MalNewsUnitModel>();

            await Task.Run(async () =>
            {
                data = args.WorkMode == ArticlePageWorkMode.AnnNews
                    ? await new Comm.Articles.AnnNewsQuery().GetAnnNewsIndex(force)
                    : await new MalArticlesIndexQuery(args.WorkMode).GetArticlesIndex(force);
            });
            Articles = data;
            _loadingData = false;
            LoadingVisibility = false;


        }

        private async void LoadArticle(MalNewsUnitModel data)
        {
            LoadingVisibility = true;
            ArticleIndexVisibility = false;
            ViewModelLocator.GeneralMain.CurrentStatus = data.Title;
            CurrentNews = Articles.IndexOf(data);
            string html;
            if (data.Source == "ANN")
                html = await Comm.Articles.AnnNewsQuery.GetAnnArticleHtml(data.Url, data.Id);
            else
                html = await new MalArticleQuery(data.Url, data.Title, data.Type).GetArticleHtml();
            OpenWebView?.Invoke(html, data);
        }
    }
}

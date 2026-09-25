using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MALClient.Models.Enums;
using MALClient.Models.Interfaces;
using MALClient.Models.Models;
using MALClient.Models.Models.Forums;
using MALClient.XShared.Comm.Forums;
using MALClient.XShared.Comm.MagicalRawQueries.Forums;
using MALClient.XShared.Delegates;
using MALClient.XShared.Interfaces;
using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels.Forums.Items;
using IHandyDataStorage = MALClient.XShared.ViewModels.Interfaces.IHandyDataStorage;

namespace MALClient.XShared.ViewModels.Forums
{
    public class ForumTopicViewModel : ViewModelBase , ISelfBackNavAware
    {
        private readonly IHandyDataStorage _handyDataStorage;

        public interface IScrollInfoProvider
        {
            int GetFirstVisibleItemIndex();
        }

        public event EventHandler<int> RequestScroll;

        private bool _loadingTopic;


        private ForumsTopicNavigationArgs _prevArgs;
        private int _currentPage;
        private ObservableCollection<ForumTopicMessageEntryViewModel> _messages;
        private ICommand _loadPageCommand;
        private ICommand _loadGotoPageCommand;
        private ICommand _gotoLastPageCommand;
        private ICommand _gotoWebsiteCommand;
        private ICommand _gotoFirstPageCommand;
        private string _gotoPageTextBind;
        private ForumTopicData _currentTopicData;
        private ICommand _toggleWatchingCommand;
        private string _toggleWatchingButtonText;
        private ICommand _createReplyCommand;
        private string _replyMessage;
        private ICommand _navigateMessagingCommand;
        private bool _isPinned;
        private bool _isWatched;
        private bool _addingToWatchedTopics;
        private string _pageHtml;
        private string _loadError;

        public IScrollInfoProvider ScrollInfoProvider { get; set; }

        public ForumTopicViewModel(IHandyDataStorage handyDataStorage)
        {
            _handyDataStorage = handyDataStorage;
        }

        public async Task Init(ForumsTopicNavigationArgs args)
        {
            var forumsMain = ViewModelLocator.ForumsMain;
            if (forumsMain != null)
                forumsMain.CurrentBackNavRegistrar = this;
            if (forumsMain?.PinnedTopics != null)
                forumsMain.PinnedTopics.CollectionChanged -= PinnedTopicsOnCollectionChanged;
            if (args == null || (string.IsNullOrWhiteSpace(args.TopicId) && args.MessageId == null))
            {
                _prevArgs = null;
                CurrentTopicData = null;
                Messages = new ObservableCollection<ForumTopicMessageEntryViewModel>();
                PageHtml = string.Empty;
                AvailablePages.Clear();
                ReplyMessage = string.Empty;
                SetLoadError("Unable to load this topic.");
                return;
            }
            if (LoadingTopic)
                return;

            LoadingTopic = true;
            _prevArgs = args;
            _prevArgs.TopicPage = Math.Max(1, _prevArgs.TopicPage);
            CurrentTopicData = null;
            Messages = new ObservableCollection<ForumTopicMessageEntryViewModel>();
            PageHtml = string.Empty;
            LoadError = null;
            ReplyMessage = string.Empty;
            AvailablePages.Clear();
            ToggleWatchingButtonText = "Toggle watching";

            try
            {
                CurrentTopicData = await ForumTopicQueries.GetTopicData(
                    _prevArgs.TopicId, _prevArgs.TopicPage, _prevArgs.LastPost, _prevArgs.MessageId);
                if (CurrentTopicData == null)
                {
                    SetLoadError("Unable to load this topic.");
                    return;
                }

                CurrentPage = CurrentTopicData.CurrentPage > 0 ? CurrentTopicData.CurrentPage : _prevArgs.TopicPage;
                _prevArgs.TopicId = CurrentTopicData.Id;
                _prevArgs.TopicPage = CurrentPage;
                if (ViewModelLocator.GeneralMain != null)
                    ViewModelLocator.GeneralMain.CurrentStatus = $"Forums - {(CurrentTopicData.IsLocked ? "Locked: " : "")}{CurrentTopicData.Title}";
                Messages = new ObservableCollection<ForumTopicMessageEntryViewModel>(
                    (CurrentTopicData.Messages ?? new List<ForumMessageEntry>())
                    .Select(entry => new ForumTopicMessageEntryViewModel(entry)));
                PageHtml = ForumTopicQueries.BuildTopicPresentationHtml(CurrentTopicData);

                if (_prevArgs.FirstVisibleItemIndex != null && Messages.Count > 0)
                {
                    RequestScroll?.Invoke(this, _prevArgs.FirstVisibleItemIndex.Value);
                }
                else if (_prevArgs.LastPost && Messages.Count > 0)
                {
                    RequestScroll?.Invoke(this, Messages.Count - 1);
                }
                else if (CurrentTopicData.TargetMessageId != null && Messages.Count > 0)
                {
                    var target = Messages.FirstOrDefault(model => model.Data.Id == CurrentTopicData.TargetMessageId);
                    if (target != null)
                        RequestScroll?.Invoke(this, Messages.IndexOf(target));
                }

                UpdateWatchedState();
                UpdatePinnedState();
                if (forumsMain?.PinnedTopics != null)
                    forumsMain.PinnedTopics.CollectionChanged += PinnedTopicsOnCollectionChanged;
            }
            catch (Exception)
            {
                SetLoadError("Unable to load this topic.");
            }
            finally
            {
                LoadingTopic = false;
            }
        }

        private void PinnedTopicsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
        {
            UpdatePinnedState();
        }

        public void NavigatedFrom()
        {
            if (ViewModelLocator.GeneralMain != null)
                ViewModelLocator.GeneralMain.CurrentStatus = "Forums";
            if (ViewModelLocator.ForumsMain?.PinnedTopics != null)
                ViewModelLocator.ForumsMain.PinnedTopics.CollectionChanged -= PinnedTopicsOnCollectionChanged;
        }

        public ObservableCollection<ForumTopicMessageEntryViewModel> Messages
        {
            get { return _messages; }
            set
            {
                DetachMessageHandlers();
                _messages = value;
                AttachMessageHandlers();
                RaisePropertyChanged(() => Messages);
                RaisePropertyChanged(() => HasMessages);
            }
        }


        public ForumTopicData CurrentTopicData
        {
            get { return _currentTopicData; }
            set
            {
                _currentTopicData = value;
                RaisePropertyChanged(() => CurrentTopicData);
                RaisePropertyChanged(() => PageText);
                RaisePropertyChanged(() => CanPreviousPage);
                RaisePropertyChanged(() => CanNextPage);
            }
        }

        public string PageHtml
        {
            get { return _pageHtml; }
            set
            {
                _pageHtml = value;
                RaisePropertyChanged(() => PageHtml);
            }
        }

        public string LoadError
        {
            get { return _loadError; }
            set
            {
                _loadError = value;
                RaisePropertyChanged(() => LoadError);
                RaisePropertyChanged(() => HasLoadError);
            }
        }

        public bool HasLoadError => !string.IsNullOrWhiteSpace(LoadError);
        public bool HasMessages => Messages != null && Messages.Count > 0;
        public string PageText => CurrentTopicData == null
            ? string.Empty
            : $"Page {CurrentPage} of {Math.Max(1, CurrentTopicData.AllPages)}";
        public bool CanPreviousPage => !LoadingTopic && CurrentTopicData != null && CurrentPage > 1;
        public bool CanNextPage => !LoadingTopic && CurrentTopicData != null && CurrentPage < CurrentTopicData.AllPages;

        public string Header => LoadingTopic ? "..." : CurrentTopicData?.Title ?? "Unable to load topic";
        public bool IsLockedVisibility => !LoadingTopic && (CurrentTopicData?.IsLocked ?? false);
        public List<ForumBreadcrumb> Breadcrumbs => !LoadingTopic ? CurrentTopicData?.Breadcrumbs  : null;

        public int CurrentPage
        {
            get { return _currentPage; }
            set
            {
                _currentPage = Math.Max(1, value);
                var allPages = Math.Max(1, CurrentTopicData?.AllPages ?? _currentPage);
                AvailablePages.Clear();
                var start = _currentPage <= 2 ? 1 : _currentPage - 2;
                for (int i = start; i <= start + 4 && i <= allPages; i++)
                    AvailablePages.Add(new Tuple<int, bool>(i, i == _currentPage));
                RaisePropertyChanged(() => CurrentPage);
                RaisePropertyChanged(() => PageText);
                RaisePropertyChanged(() => CanPreviousPage);
                RaisePropertyChanged(() => CanNextPage);
            }
        }

        public ObservableCollection<Tuple<int, bool>> AvailablePages { get; } = new ObservableCollection<Tuple<int, bool>>();


        public bool LoadingTopic
        {
            get { return _loadingTopic; }
            set
            {
                _loadingTopic = value;
                RaisePropertyChanged(() => LoadingTopic);
                RaisePropertyChanged(() => Header);
                RaisePropertyChanged(() => Breadcrumbs);
                RaisePropertyChanged(() => CanPreviousPage);
                RaisePropertyChanged(() => CanNextPage);
            }
        }

        public string GotoPageTextBind
        {
            get { return _gotoPageTextBind; }
            set
            {
                _gotoPageTextBind = value;
                RaisePropertyChanged(() => GotoPageTextBind);
            }
        }

        public string ToggleWatchingButtonText
        {
            get { return _toggleWatchingButtonText; }
            set
            {
                _toggleWatchingButtonText = value;
                RaisePropertyChanged(() => ToggleWatchingButtonText);
            }
        }

        public string ReplyMessage
        {
            get { return _replyMessage; }
            set
            {
                _replyMessage = value;
                RaisePropertyChanged(() => ReplyMessage);
            }
        }


        public bool IsPinned
        {
            get { return _isPinned; }
            set
            {
                _isPinned = value;
                RaisePropertyChanged(() => IsPinned);
            }
        }

        public bool AddingToWatchedTopics
        {
            get { return _addingToWatchedTopics; }
            set
            {
                _addingToWatchedTopics = value;
                RaisePropertyChanged(() => AddingToWatchedTopics);
            }
        }

        public bool IsWatched
        {
            get { return _isWatched; }
            set
            {

                _isWatched = value;
                RaisePropertyChanged(() => IsWatched);
                if (value)
                {
                    AddToWatchedTopics();
                }
                else if (_handyDataStorage?.WatchedTopics?.StoredItems != null && CurrentTopicData?.Id != null)
                {
                    var index = _handyDataStorage.WatchedTopics.StoredItems.FindIndex(model => model.Id == CurrentTopicData.Id);
                    if (index >= 0)
                        _handyDataStorage.WatchedTopics.StoredItems.RemoveAt(index);
                }
            }
        }

        #region Commands

        public ICommand NavigateBreadcrumbsCommand => new RelayCommand<ForumBreadcrumb>(breadcrumb =>
        {
            var args = MalLinkParser.GetNavigationParametersForUrl(breadcrumb.Link);
            if (args == null)
            {
                ResourceLocator.SystemControlsLauncherService.LaunchUri(new Uri(breadcrumb.Link));
                return;
            }
            RegisterSelfBackNav();
            if (args.Item1 == PageIndex.PageAnimeDetails)
            {
                var detailsArg = args.Item2 as AnimeDetailsPageNavigationArgs;
                ViewModelLocator.GeneralMain.Navigate(PageIndex.PageForumIndex,new ForumsBoardNavigationArgs(detailsArg.Id,detailsArg.Title,detailsArg.AnimeMode));
            }
            else
            {
                ViewModelLocator.GeneralMain.Navigate(args.Item1, args.Item2);
            }

        });

        public ICommand NavigateProfileCommand => new RelayCommand<MalUser>(user =>
        {
            RegisterSelfBackNav();
            ViewModelLocator.GeneralMain.Navigate(PageIndex.PageProfile,
                new ProfilePageNavigationArgs {TargetUser = user.Name,AllowBackNavReset = false});
        });

        public ICommand LoadPageCommand => _loadPageCommand ?? (_loadPageCommand = new RelayCommand<int>(page =>
        {
            if (page < 1 || (CurrentTopicData != null && page > CurrentTopicData.AllPages) || LoadingTopic)
                return;
            _ = LoadCurrentTopicPageAsync(false, false, page);
        }));

        public ICommand PreviousPageCommand => new RelayCommand(() =>
        {
            if (CurrentPage <= 1 || LoadingTopic)
                return;
            _ = LoadCurrentTopicPageAsync(false, false, CurrentPage - 1);
        });

        public ICommand NextPageCommand => new RelayCommand(() =>
        {
            if (CurrentTopicData == null || CurrentPage >= CurrentTopicData.AllPages || LoadingTopic)
                return;
            _ = LoadCurrentTopicPageAsync(false, false, CurrentPage + 1);
        });

        public ICommand LoadGotoPageCommand => _loadGotoPageCommand ?? (_loadGotoPageCommand = new RelayCommand(() =>
        {
            if (!int.TryParse(GotoPageTextBind, out var val) || val < 1 ||
                (CurrentTopicData != null && val > CurrentTopicData.AllPages) || LoadingTopic)
                return;
            GotoPageTextBind = string.Empty;
            _ = LoadCurrentTopicPageAsync(false, false, val);
        }));


        public ICommand GotoLastPageCommand => _gotoLastPageCommand ?? (_gotoLastPageCommand = new RelayCommand(async () =>
            {
                if (CurrentTopicData == null || LoadingTopic)
                    return;
                await LoadCurrentTopicPageAsync(false, true);
                if (Messages?.Count > 0)
                    RequestScroll?.Invoke(this, Messages.Count - 1);
            }));

        public ICommand GotoWebsiteCommand => _gotoWebsiteCommand ?? (_gotoWebsiteCommand = new RelayCommand(
            () =>
            {
                if(CurrentTopicData == null)
                    return;
                ResourceLocator.SystemControlsLauncherService.LaunchUri(new Uri($"https://myanimelist.net/forum/?topicid={CurrentTopicData.Id}"));
            }));



        public ICommand GotoFirstPageCommand => _gotoFirstPageCommand ?? (_gotoFirstPageCommand = new RelayCommand(
            () =>
            {
                if (CurrentPage == 1 || LoadingTopic)
                    return;
                _ = LoadCurrentTopicPageAsync(false, false, 1);
            }));

        public ICommand ToggleWatchingCommand => _toggleWatchingCommand ?? (_toggleWatchingCommand = new RelayCommand(
                                                     async () =>
                                                     {
                                                         if (CurrentTopicData?.Id == null)
                                                             return;
                                                         try
                                                         {
                                                             var res =
                                                                 await ForumTopicQueries.ToggleTopicWatching(
                                                                     CurrentTopicData.Id);
                                                             if (res == null)
                                                                 throw new ArgumentNullException();

                                                             ToggleWatchingButtonText =
                                                                 res == true ? "Watching" : "Stopped watching";
                                                         }
                                                         catch (Exception e)
                                                         {
                                                             ResourceLocator.MessageDialogProvider.ShowMessageDialog(
                                                                 "Unable to toggle watching status.",
                                                                 "Something went wrong");
                                                         }
                                                     }));

        public ICommand CreateReplyCommand => _createReplyCommand ?? (_createReplyCommand = new RelayCommand(
                                                  async () =>
                                                  {
                                                      if (CurrentTopicData?.Id == null || string.IsNullOrWhiteSpace(ReplyMessage))
                                                          return;
                                                      if(await ForumTopicQueries.CreateMessage(CurrentTopicData.Id,ReplyMessage))
                                                      {
                                                          ResourceLocator.TelemetryProvider.TelemetryTrackEvent(TelemetryTrackedEvents.CreatedReply);
                                                          ReplyMessage = string.Empty;
                                                          if (CurrentTopicData.Messages != null && CurrentTopicData.Messages.Count % 50 == 0)
                                                              CurrentPage++;
                                                          await LoadCurrentTopicPageAsync(true, true);
                                                      }
                                                      else
                                                      {
                                                          ResourceLocator.MessageDialogProvider.ShowMessageDialog("Unable to send your reply","Something went wrong");
                                                      }
                                                  }));

        public ICommand NavigateMessagingCommand
            => _navigateMessagingCommand ?? (_navigateMessagingCommand = new RelayCommand<MalUser>(
                   user =>
                   {
                       if(ViewModelLocator.Mobile)
                           RegisterSelfBackNav();
                       ViewModelLocator.GeneralMain.Navigate(PageIndex.PageMessageDetails,new MalMessageDetailsNavArgs{WorkMode = MessageDetailsWorkMode.Message,NewMessageTarget = user.Name,BackNavHandled =  true});
                   }));


        public ICommand PinTopicCommand
            => new RelayCommand<bool>(
                lastpost =>
                {
                    if (CurrentTopicData?.Messages == null || !CurrentTopicData.Messages.Any())
                        return;
                    if (ViewModelLocator.ForumsMain?.PinnedTopics == null)
                        return;

                    var topicEntry = new ForumTopicLightEntry
                    {
                        Created = CurrentTopicData.Messages[0].CreateDate,
                        Id = CurrentTopicData.Id,
                        Lastpost = lastpost,
                        Op = CurrentTopicData.Messages[0].Poster.MalUser.Name,
                        SourceBoard = null,
                        Title = CurrentTopicData.Title
                    };
                    ViewModelLocator.ForumsMain.PinnedTopics.Add(topicEntry);
                    IsPinned = true;
                });

        public ICommand UnpinTopicCommand
            => new RelayCommand(
                () =>
                {
                    if (CurrentTopicData == null)
                        return;
                    var pinnedTopics = ViewModelLocator.ForumsMain?.PinnedTopics;
                    var pinned = pinnedTopics?.FirstOrDefault(entry => entry.Id == CurrentTopicData.Id);
                    if (pinned == null)
                        return;
                    pinnedTopics.Remove(pinned);
                    IsPinned = false;
                });



        public bool IsMangaBoard => _prevArgs?.TopicType == TopicType.Manga;

        #endregion

        private async Task LoadCurrentTopicPageAsync(bool force = false,bool lastpost = false,int? requestedPage = null)
        {
            if (CurrentTopicData?.Id == null || LoadingTopic)
                return;

            var page = Math.Max(1, requestedPage ?? CurrentPage);
            LoadingTopic = true;
            try
            {
                var data = await ForumTopicQueries.GetTopicData(CurrentTopicData.Id, page, lastpost, null, force);
                if (data == null || data.Messages == null || !data.Messages.Any())
                {
                    SetLoadError("Unable to load this page.");
                    return;
                }

                CurrentTopicData = data;
                Messages = new ObservableCollection<ForumTopicMessageEntryViewModel>(
                    data.Messages.Select(entry => new ForumTopicMessageEntryViewModel(entry)));
                PageHtml = ForumTopicQueries.BuildTopicPresentationHtml(data);
                CurrentPage = data.CurrentPage > 0 ? data.CurrentPage : page;
                LoadError = null;
                if (lastpost && Messages.Count > 0)
                    RequestScroll?.Invoke(this, Messages.Count - 1);
            }
            catch (Exception)
            {
                SetLoadError("Unable to load this page.");
            }
            finally
            {
                LoadingTopic = false;
            }
        }

        public void RemoveMessage(ForumTopicMessageEntryViewModel forumTopicMessageEntryViewModel)
        {
            if (forumTopicMessageEntryViewModel == null)
                return;
            forumTopicMessageEntryViewModel.PropertyChanged -= OnMessagePropertyChanged;
            Messages?.Remove(forumTopicMessageEntryViewModel);
            RaisePropertyChanged(() => HasMessages);
            if (CurrentTopicData?.Messages != null && forumTopicMessageEntryViewModel.Data != null)
                CurrentTopicData.Messages.RemoveAll(entry => entry.Id == forumTopicMessageEntryViewModel.Data.Id);
            ForumTopicQueries.NotifyMessageRemoved(forumTopicMessageEntryViewModel.Data);
            if (CurrentTopicData != null)
                PageHtml = ForumTopicQueries.BuildTopicPresentationHtml(CurrentTopicData);
        }

        public async void QuouteMessage(string dataId,string poster)
        {
            if (string.IsNullOrWhiteSpace(dataId))
                return;
            var quote = await ForumTopicQueries.GetQuote(dataId);
            if (string.IsNullOrWhiteSpace(quote))
                return;
            ReplyMessage += $"[quote={poster} message={dataId}]{quote}[/quote]";
        }

        public void RegisterSelfBackNav()
        {
            if (_prevArgs == null)
                return;
            _prevArgs.FirstVisibleItemIndex = ScrollInfoProvider?.GetFirstVisibleItemIndex();
            _prevArgs.TopicPage = Math.Max(1, CurrentPage);
            _prevArgs.MessageId = null;
            ViewModelLocator.NavMgr.RegisterBackNav(PageIndex.PageForumIndex, _prevArgs);
        }

        public ForumsTopicNavigationArgs GetSelfBackNavArgs()
        {
            _prevArgs ??= new ForumsTopicNavigationArgs(null, null, Math.Max(1, CurrentPage));
            _prevArgs.FirstVisibleItemIndex = ScrollInfoProvider?.GetFirstVisibleItemIndex();
            _prevArgs.TopicPage = Math.Max(1, CurrentPage);
            _prevArgs.MessageId = null;
            return _prevArgs;
        }

        public async Task ReloadAsync()
        {
            if (LoadingTopic)
                return;
            if (CurrentTopicData == null)
            {
                if (_prevArgs == null ||
                    (string.IsNullOrWhiteSpace(_prevArgs.TopicId) && _prevArgs.MessageId == null))
                    return;
                await Init(new ForumsTopicNavigationArgs(
                    _prevArgs.TopicId, _prevArgs.MessageId, Math.Max(1, CurrentPage)));
                return;
            }
            await LoadCurrentTopicPageAsync(true, false, CurrentPage);
        }

        public async void Reload()
        {
            await ReloadAsync();
        }

        private void SetLoadError(string message)
        {
            LoadError = string.IsNullOrWhiteSpace(message) ? "Unable to load this topic." : message;
            if (ViewModelLocator.GeneralMain != null)
                ViewModelLocator.GeneralMain.CurrentStatus = "Forums - Unable to load topic";
            if (CurrentTopicData == null)
            {
                Messages ??= new ObservableCollection<ForumTopicMessageEntryViewModel>();
                PageHtml = string.Empty;
            }
        }

        private void AttachMessageHandlers()
        {
            if (Messages == null)
                return;
            foreach (var message in Messages)
            {
                message.PropertyChanged -= OnMessagePropertyChanged;
                message.PropertyChanged += OnMessagePropertyChanged;
            }
        }

        private void DetachMessageHandlers()
        {
            if (Messages == null)
                return;
            foreach (var message in Messages)
                message.PropertyChanged -= OnMessagePropertyChanged;
        }

        private void OnMessagePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ForumTopicMessageEntryViewModel.Data) && CurrentTopicData != null)
                PageHtml = ForumTopicQueries.BuildTopicPresentationHtml(CurrentTopicData);
        }

        private void UpdateWatchedState()
        {
            if (_handyDataStorage?.WatchedTopics?.StoredItems == null || CurrentTopicData == null)
            {
                _isWatched = false;
                RaisePropertyChanged(() => IsWatched);
                return;
            }
            var watched = _handyDataStorage.WatchedTopics.StoredItems.FirstOrDefault(model => model.Id == CurrentTopicData.Id);
            _isWatched = watched != null;
            if (watched != null)
            {
                watched.OnCooldown = false;
                _handyDataStorage.WatchedTopics.SaveData();
            }
            RaisePropertyChanged(() => IsWatched);
        }

        private void UpdatePinnedState()
        {
            var pinnedTopics = ViewModelLocator.ForumsMain?.PinnedTopics;
            IsPinned = pinnedTopics != null && CurrentTopicData != null &&
                       pinnedTopics.Any(entry => entry.Id == CurrentTopicData.Id);
        }

        private async void AddToWatchedTopics()
        {
            if (AddingToWatchedTopics || CurrentTopicData?.Id == null || _handyDataStorage?.WatchedTopics == null)
                return;
            AddingToWatchedTopics = true;
            try
            {
                var count = await new ForumTopicMessageCountQuery(CurrentTopicData.Id).GetMessageCount(false);
                if (count == null)
                {
                    _isWatched = false;
                    RaisePropertyChanged(() => IsWatched);
                    return;
                }
                _handyDataStorage.WatchedTopics.StoredItems.Add(new WatchedTopicModel
                {
                    Id = CurrentTopicData.Id,
                    LastCheckedReplyCount = count.Value,
                    Title = CurrentTopicData.Title
                });
            }
            catch
            {
                _isWatched = false;
                RaisePropertyChanged(() => IsWatched);
            }
            finally
            {
                AddingToWatchedTopics = false;
            }
        }
    }
}

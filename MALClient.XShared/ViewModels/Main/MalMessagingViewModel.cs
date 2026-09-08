using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MALClient.Models.Enums;
using MALClient.Models.Models.MalSpecific;
using MALClient.XShared.Comm.MagicalRawQueries;
using MALClient.XShared.Comm.MagicalRawQueries.Messages;
using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;

namespace MALClient.XShared.ViewModels.Main
{
    public class MalMessagingViewModel : ViewModelBase
    {
        private ICommand _composeNewCommand;

        private bool _displaySentMessages;
        private int _loadedPages = 1;

        private bool _loadingVisibility;

        private ICommand _loadMoreCommand;

        private bool _loadMorePagesVisibility = false;

        private bool _skipLoading;
        private bool _loadedSomething;
        private ICommand _navigateMessageCommand;

        public SmartObservableCollection<MalMessageModel> MessageIndex { get; } =
            new SmartObservableCollection<MalMessageModel>();

        public List<MalMessageModel> Outbox { get; set; } = new List<MalMessageModel>();
        public List<MalMessageModel> Inbox { get; set; } = new List<MalMessageModel>();


        public ICommand NavigateMessageCommand
            => _navigateMessageCommand ?? (_navigateMessageCommand = new RelayCommand<MalMessageModel>(
                   model =>
                   {
                       if(DisplaySentMessages)
                           return;
                       ViewModelLocator.GeneralMain.Navigate(PageIndex.PageMessageDetails,
                           new MalMessageDetailsNavArgs {WorkMode = MessageDetailsWorkMode.Message, Arg = model});
                   }));



        public bool DisplaySentMessages
        {
            get { return _displaySentMessages; }
            set
            {
                _displaySentMessages = value;
                _skipLoading = true;
                LoadMore();
                RaisePropertyChanged(() => DisplaySentMessages);
            }
        }

        public bool LoadingVisibility
        {
            get { return _loadingVisibility; }
            set
            {
                _loadingVisibility = value;
                RaisePropertyChanged(() => LoadingVisibility);
            }
        }

        public bool LoadMorePagesVisibility
        {
            get { return _loadMorePagesVisibility; }
            set
            {
                _loadMorePagesVisibility = value;
                RaisePropertyChanged(() => LoadMorePagesVisibility);
            }
        }

        public ICommand LoadMoreCommand => _loadMoreCommand ?? (_loadMoreCommand = new RelayCommand(() => LoadMore()));

        public ICommand ComposeNewCommand => _composeNewCommand ?? (_composeNewCommand = new RelayCommand(ComposeNew));


        public void Init(bool force = false)
        {
            if(!force && _loadedSomething)
                return;
            LoadMore(force);
        }

        private async void LoadMore(bool force = false)
        {
            LoadingVisibility = true;
            try
            {
                if (force)
                {
                    if (DisplaySentMessages)
                    {
                        Outbox = new List<MalMessageModel>();
                    }
                    else
                    {
                        _loadedPages = 1;
                        Inbox = new List<MalMessageModel>();
                    }
                }
                if (!DisplaySentMessages)
                {
                    if (!_skipLoading)
                    {
                        _loadedSomething = true;
                        try
                        {
                            // Prevent page number from going too high (MAL API typically supports up to ~100 pages)
                            if (_loadedPages > 100)
                            {
                                LoadMorePagesVisibility = false;
                            }
                            else
                            {
                                var messages = await AccountMessagesManager.GetMessagesAsync(_loadedPages);
                                if (messages?.Any() ?? false)
                                {
                                    Inbox.AddRange(messages);
                                    _loadedPages++;
                                }
                                else
                                {
                                    LoadMorePagesVisibility = false;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine("MALPLUS LoadMore inbox failed: " + ex.Message);
                            ResourceLocator.MalHttpContextProvider.ErrorMessage("Messages");
                            LoadMorePagesVisibility = false;
                        }
                    }
                    _skipLoading = false;
                    MessageIndex.Clear();
                    MessageIndex.AddRange(Inbox);
                    LoadMorePagesVisibility = Inbox.Any() && _loadedPages <= 100;
                }
                else
                {
                    try
                    {
                        if (Outbox.Count == 0)
                            Outbox = await AccountMessagesManager.GetSentMessagesAsync();
                        MessageIndex.Clear();
                        MessageIndex.AddRange(Outbox);
                        LoadMorePagesVisibility = false; // Sent messages don't paginate
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("MALPLUS LoadMore outbox failed: " + ex.Message);
                        ResourceLocator.MalHttpContextProvider.ErrorMessage("Messages");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("MALPLUS LoadMore failed: " + ex.Message);
            }
            finally
            {
                LoadingVisibility = false;
            }
        }

        private void ComposeNew()
        {
            ViewModelLocator.GeneralMain.Navigate(PageIndex.PageMessageDetails, new MalMessageDetailsNavArgs {WorkMode = MessageDetailsWorkMode.Message}); // null for new message
        }
    }
}
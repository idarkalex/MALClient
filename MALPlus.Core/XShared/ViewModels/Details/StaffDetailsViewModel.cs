using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MALClient.Models.Enums;
using MALClient.Models.Models.Anime;
using MALClient.Models.Models.Favourites;
using MALClient.Models.Models.ScrappedDetails;
using MALClient.XShared.Comm.Details;
using MALClient.XShared.Delegates;
using MALClient.XShared.NavArgs;

namespace MALClient.XShared.ViewModels.Details
{
    public class StaffDetailsViewModel : ViewModelBase
    {
        private StaffDetailsData _data;
        private StaffDetailsNaviagtionArgs _prevArgs;
        private ICommand _navigateAnimeDetailsCommand;
        private ICommand _navigateCharacterDetailsCommand;
        private bool _loading;
        private ICommand _openInMalCommand;
        private bool _isNoVoiceActingRolesNoticeVisible = true;
        private bool _isNoProductionRolesNoticeVisible = true;

        public event PivotItemSelectionRequest OnPivotItemSelectionRequest;

        public StaffDetailsData Data
        {
            get { return _data; }
            set
            {
                _data = value;
                RaisePropertyChanged(() => Data);
                RaisePropertyChanged(() => FavouriteViewModel);
            }
        }

        public ICommand NavigateAnimeDetailsCommand =>
            _navigateAnimeDetailsCommand ??
            (_navigateAnimeDetailsCommand = new RelayCommand<AnimeLightEntry>(entry =>
            {
                if (entry == null || entry.Id <= 0)
                    return;
                ViewModelLocator.GeneralMain.Navigate(PageIndex.PageAnimeDetails,
                    new AnimeDetailsPageNavigationArgs(entry.Id, entry.Title, null, null, _prevArgs)
                    {
                        AnimeMode = entry.IsAnime,
                        Source = PageIndex.PageStaffDetails
                    });
            }));

        public ICommand NavigateCharacterDetailsCommand =>
            _navigateCharacterDetailsCommand ??
            (_navigateCharacterDetailsCommand = new RelayCommand<AnimeCharacter>(entry =>
            {
                if (entry == null || !int.TryParse(entry.Id, out int id) || id <= 0)
                    return;
                RegisterSelfBackNav();
                ViewModelLocator.GeneralMain.Navigate(PageIndex.PageCharacterDetails,
                    new CharacterDetailsNavigationArgs {Id = id});
            }));

        public ICommand OpenInMalCommand =>
            _openInMalCommand ??
            (_openInMalCommand = new RelayCommand(() =>
            {
                if (Data == null || Data.Id <= 0)
                    return;
                ResourceLocator.SystemControlsLauncherService.LaunchUri(new Uri($"https://myanimelist.net/people/{Data.Id}"));
            }));

        public FavouriteViewModel FavouriteViewModel =>
            Data == null ? null : new FavouriteViewModel(new AnimeStaffPerson {Id = Data.Id.ToString()});

        public bool Loading
        {
            get { return _loading; }
            set
            {
                _loading = value;
                RaisePropertyChanged(() => Loading);
            }
        }

        public bool IsNoVoiceActingRolesNoticeVisible
        {
            get { return _isNoVoiceActingRolesNoticeVisible; }
            set
            {
                _isNoVoiceActingRolesNoticeVisible = value;
                RaisePropertyChanged(() => IsNoVoiceActingRolesNoticeVisible);
            }
        }

        public bool IsNoProductionRolesNoticeVisible
        {
            get { return _isNoProductionRolesNoticeVisible; }
            set
            {
                _isNoProductionRolesNoticeVisible = value;
                RaisePropertyChanged(() => IsNoProductionRolesNoticeVisible);
            }
        }

        public async Task Init(StaffDetailsNaviagtionArgs args, bool force = false)
        {
            if (args == null)
                return;
            if (!force && Data != null && (_prevArgs?.Equals(args) ?? false))
                return;
            if (Data != null)
            {
                ViewModelLocator.GeneralMain.CurrentOffStatus = Data.Name;
                ViewModelLocator.GeneralMain.IsCurrentStatusSelectable = true;
            }
            if (args.ResetNav && !ViewModelLocator.NavMgr.HasSomethingOnStack())
            {
                ViewModelLocator.NavMgr.ResetMainBackNav();
                ViewModelLocator.NavMgr.RegisterBackNav(PageIndex.PageAnimeList, null);
            }

            Loading = true;
            _prevArgs = args;
            try
            {
                ResetState();
                Data = await new StaffDetailsQuery(args.Id).GetStaffDetails(force) ?? new StaffDetailsData();
                Data.Details = Data.Details ?? new List<string>();
                Data.ShowCharacterPairs = Data.ShowCharacterPairs ?? new List<ShowCharacterPair>();
                Data.StaffPositions = Data.StaffPositions ?? new List<AnimeLightEntry>();
                IsNoVoiceActingRolesNoticeVisible = Data.ShowCharacterPairs.Count == 0;
                IsNoProductionRolesNoticeVisible = Data.StaffPositions.Count == 0;
                if (IsNoVoiceActingRolesNoticeVisible)
                    OnPivotItemSelectionRequest?.Invoke(1);
                ViewModelLocator.GeneralMain.CurrentOffStatus = Data.Name ?? string.Empty;
                ViewModelLocator.GeneralMain.IsCurrentStatusSelectable = true;
            }
            finally
            {
                Loading = false;
            }
        }

        public Task RefreshData()
        {
            return Init(_prevArgs, true);
        }

        private void ResetState()
        {
            Data = null;
            IsNoVoiceActingRolesNoticeVisible = true;
            IsNoProductionRolesNoticeVisible = true;
            ViewModelLocator.GeneralMain.CurrentOffStatus = string.Empty;
            ViewModelLocator.GeneralMain.IsCurrentStatusSelectable = false;
        }

        public void RegisterSelfBackNav(int targetId = 0)
        {
            if (Data == null || targetId == Data.Id)
                return;
            ViewModelLocator.NavMgr.RegisterBackNav(PageIndex.PageStaffDetails, _prevArgs);
        }
    }
}

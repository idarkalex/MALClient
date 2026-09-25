using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MALClient.Models.Enums;
using MALClient.Models.Models.Anime;
using MALClient.Models.Models.Favourites;
using MALClient.Models.Models.ScrappedDetails;
using MALClient.XShared.Comm.Details;
using MALClient.XShared.NavArgs;

namespace MALClient.XShared.ViewModels.Details
{
    public class CharacterDetailsViewModel : ViewModelBase
    {
        private CharacterDetailsData _data;
        private bool _animeographyVisibility;
        private bool _spoilerButtonVisibility;
        private ICommand _navigateAnimeDetailsCommand;
        private ICommand _openInMalCommand;
        private CharacterDetailsNavigationArgs _prevArgs;
        private ICommand _navigateMangaDetailsCommand;
        private bool _mangaographyVisibility;
        private List<FavouriteViewModel> _voiceActors = new List<FavouriteViewModel>();
        private ICommand _navigateStaffDetailsCommand;
        private bool _loading;
        private bool _hasAboutContent;
        private bool _isAboutExpanded = true;
        private bool _isSpoilerExpanded;
        private bool _isOverviewEmpty = true;
        private ICommand _toggleAboutCommand;
        private ICommand _toggleSpoilerCommand;

        public CharacterDetailsData Data
        {
            get { return _data; }
            set
            {
                _data = value;
                RaisePropertyChanged(() => Data);
            }
        }

        public List<FavouriteViewModel> VoiceActors
        {
            get { return _voiceActors; }
            set
            {
                _voiceActors = value ?? new List<FavouriteViewModel>();
                RaisePropertyChanged(() => VoiceActors);
            }
        }

        public bool SpoilerButtonVisibility
        {
            get { return _spoilerButtonVisibility; }
            set
            {
                _spoilerButtonVisibility = value;
                RaisePropertyChanged(() => SpoilerButtonVisibility);
            }
        }

        public bool AnimeographyVisibility
        {
            get { return _animeographyVisibility; }
            set
            {
                _animeographyVisibility = value;
                RaisePropertyChanged(() => AnimeographyVisibility);
            }
        }

        public bool MangaographyVisibility
        {
            get { return _mangaographyVisibility; }
            set
            {
                _mangaographyVisibility = value;
                RaisePropertyChanged(() => MangaographyVisibility);
            }
        }

        public bool HasAboutContent
        {
            get { return _hasAboutContent; }
            set
            {
                _hasAboutContent = value;
                RaisePropertyChanged(() => HasAboutContent);
            }
        }

        public bool IsAboutExpanded
        {
            get { return _isAboutExpanded; }
            set
            {
                _isAboutExpanded = value;
                RaisePropertyChanged(() => IsAboutExpanded);
            }
        }

        public string AboutToggleText => IsAboutExpanded ? "ABOUT  -" : "ABOUT  +";

        public bool IsSpoilerExpanded
        {
            get { return _isSpoilerExpanded; }
            set
            {
                _isSpoilerExpanded = value;
                RaisePropertyChanged(() => IsSpoilerExpanded);
            }
        }

        public string SpoilerToggleText => IsSpoilerExpanded ? "SPOILER  -" : "SPOILER  +";

        public bool IsOverviewEmpty
        {
            get { return _isOverviewEmpty; }
            set
            {
                _isOverviewEmpty = value;
                RaisePropertyChanged(() => IsOverviewEmpty);
            }
        }

        public ICommand ToggleAboutCommand =>
            _toggleAboutCommand ??
            (_toggleAboutCommand = new RelayCommand(() =>
            {
                IsAboutExpanded = !IsAboutExpanded;
                RaisePropertyChanged(() => AboutToggleText);
            }));

        public ICommand ToggleSpoilerCommand =>
            _toggleSpoilerCommand ??
            (_toggleSpoilerCommand = new RelayCommand(() =>
            {
                IsSpoilerExpanded = !IsSpoilerExpanded;
                RaisePropertyChanged(() => SpoilerToggleText);
            }));

        public ICommand NavigateStaffDetailsCommand =>
            _navigateStaffDetailsCommand ??
            (_navigateStaffDetailsCommand = new RelayCommand<FavouriteBase>(entry =>
            {
                if (entry == null || !int.TryParse(entry.Id, out int id) || id <= 0)
                    return;
                RegisterSelfBackNav();
                ViewModelLocator.GeneralMain.Navigate(PageIndex.PageStaffDetails,
                    new StaffDetailsNaviagtionArgs {Id = id});
            }));

        public ICommand NavigateAnimeDetailsCommand =>
            _navigateAnimeDetailsCommand ??
            (_navigateAnimeDetailsCommand = new RelayCommand<AnimeLightEntry>(entry =>
            {
                if (entry == null || entry.Id <= 0)
                    return;
                ViewModelLocator.GeneralMain.Navigate(PageIndex.PageAnimeDetails,
                    new AnimeDetailsPageNavigationArgs(entry.Id, entry.Title, null, null, _prevArgs)
                    {
                        AnimeMode = true,
                        Source = PageIndex.PageCharacterDetails
                    });
            }));

        public ICommand NavigateMangaDetailsCommand =>
            _navigateMangaDetailsCommand ??
            (_navigateMangaDetailsCommand = new RelayCommand<AnimeLightEntry>(entry =>
            {
                if (entry == null || entry.Id <= 0)
                    return;
                ViewModelLocator.GeneralMain.Navigate(PageIndex.PageAnimeDetails,
                    new AnimeDetailsPageNavigationArgs(entry.Id, entry.Title, null, null, _prevArgs)
                    {
                        AnimeMode = false,
                        Source = PageIndex.PageCharacterDetails
                    });
            }));

        public ICommand OpenInMalCommand =>
            _openInMalCommand ??
            (_openInMalCommand = new RelayCommand(() =>
            {
                if (Data == null || Data.Id <= 0)
                    return;
                ResourceLocator.SystemControlsLauncherService.LaunchUri(new Uri($"https://myanimelist.net/character/{Data.Id}"));
            }));

        public FavouriteViewModel FavouriteViewModel =>
            Data == null ? null : new FavouriteViewModel(new AnimeCharacter {Id = Data.Id.ToString()});

        public bool Loading
        {
            get { return _loading; }
            set
            {
                _loading = value;
                RaisePropertyChanged(() => Loading);
            }
        }

        public async Task Init(CharacterDetailsNavigationArgs args, bool force = false)
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
                var data = await new CharacterDetailsQuery(args.Id).GetCharacterDetails(force) ?? new CharacterDetailsData();
                Data = data;
                HasAboutContent = !string.IsNullOrWhiteSpace(data.Content);
                SpoilerButtonVisibility = !string.IsNullOrWhiteSpace(data.SpoilerContent);
                IsOverviewEmpty = !HasAboutContent && !SpoilerButtonVisibility;
                AnimeographyVisibility = data.Animeography?.Any() == true;
                MangaographyVisibility = data.Mangaography?.Any() == true;
                VoiceActors = data.VoiceActors?
                    .Where(actor => actor != null)
                    .Select(actor => new FavouriteViewModel(actor))
                    .ToList();
                RaisePropertyChanged(() => FavouriteViewModel);
                ViewModelLocator.GeneralMain.CurrentOffStatus = data.Name ?? string.Empty;
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
            VoiceActors = new List<FavouriteViewModel>();
            HasAboutContent = false;
            SpoilerButtonVisibility = false;
            IsOverviewEmpty = true;
            IsAboutExpanded = true;
            IsSpoilerExpanded = false;
            RaisePropertyChanged(() => AboutToggleText);
            RaisePropertyChanged(() => SpoilerToggleText);
            AnimeographyVisibility = false;
            MangaographyVisibility = false;
            ViewModelLocator.GeneralMain.CurrentOffStatus = string.Empty;
            ViewModelLocator.GeneralMain.IsCurrentStatusSelectable = false;
        }

        public void RegisterSelfBackNav(int targetId = 0)
        {
            if (Data == null || targetId == Data.Id)
                return;
            ViewModelLocator.NavMgr.RegisterBackNav(PageIndex.PageCharacterDetails, _prevArgs);
        }
    }
}

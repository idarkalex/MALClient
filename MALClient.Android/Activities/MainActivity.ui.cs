using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Gms.Ads;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Views;
using Android.Widget;

using Com.Mikepenz.Materialdrawer;
using Com.Mikepenz.Materialdrawer.Holder;
using Com.Mikepenz.Materialdrawer.Model;
using Com.Mikepenz.Materialdrawer.Model.Interfaces;
using Com.Shehabic.Droppy;
using GalaSoft.MvvmLight.Helpers;
using GalaSoft.MvvmLight.Ioc;
using Java.Lang;
using MALClient.Adapters;
using MALClient.Android.BindingConverters;
using MALClient.Android.Flyouts;
using MALClient.Android.Fragments;
using MALClient.Android.Listeners;
using MALClient.Android.Resources;
using MALClient.Models.Enums;
using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;
using Debug = System.Diagnostics.Debug;
using Object = Java.Lang.Object;
using Orientation = Android.Widget.Orientation;
using Settings = MALClient.XShared.Utils.Settings;
using Uri = Android.Net.Uri;

namespace MALClient.Android.Activities
{
    [IntentFilter(new[] { "android.intent.action.VIEW" },
        Categories = new[] { "android.intent.category.DEFAULT", "android.intent.category.BROWSABLE" },
        DataSchemes = new[] { "http", "https" },
        DataHosts = new[] { "www.myanimelist.net", "myanimelist.net" },
        DataPathPatterns = new[]
        {
            "/forum/.*",
            "/news",
            "/featured",
            "/mymessages.php",
            "/forum",
            "/forum/",
            "/anime.php",
            "/anime/.*",
            "/manga/.*",
            "/profile/.*",
            "/character/.*",
            "/people/.*",
        }
    )]
    public partial class MainActivity
    {
        protected T GetView<T>(ref T field, int id) where T : View => field ?? (field = FindViewById<T>(id));

        private Drawer _drawer;
        private readonly List<Binding> Bindings = new List<Binding>();

        private void InitBindings()
        {
            Bindings.Add(this.SetBinding(() => ViewModel.UpdateAvailable).WhenSourceChanges(() =>
            {
                if (ViewModel.UpdateAvailable)
                {
                    var view = _accountHamburgerView?.FindViewById(Resource.Id.HamburgerUpdateNotice);
                    if (view != null)
                    {
                        view.Visibility = ViewStates.Visible;
                    }

                    PromptUpdate();
                }
            }));

            Bindings.Add(this.SetBinding(() => ViewModel.MediaElementVisibility)
                .WhenSourceChanges(() =>
                {
                    if (ViewModel.MediaElementVisibility)
                    {
                        MainPageVideoViewContainer.Visibility = ViewStates.Visible;
                        MainPageVideoView.Visibility = ViewStates.Visible;
                        MainPageVideoView.SetZOrderOnTop(true);
                        _drawer?.DrawerLayout.SetDrawerLockMode(DrawerLayout.LockModeLockedClosed);
                    }
                    else
                    {
                        MainPageVideoViewContainer.Visibility = ViewStates.Gone;
                        MainPageVideoView.Visibility = ViewStates.Gone;
                        MainPageVideoView.SetZOrderOnTop(false);
                        _drawer?.DrawerLayout.SetDrawerLockMode(DrawerLayout.LockModeUnlocked);
                        ViewModelLocator.NavMgr.ResetOneTimeOverride();
                    }
                }));

            Bindings.Add(
                this.SetBinding(() => ViewModel.MediaElementSource).WhenSourceChanges(() =>
                {
                    if (string.IsNullOrEmpty(ViewModel.MediaElementSource))
                        return;

                    var mediaController = new MediaController(this);
                    mediaController.SetAnchorView(MainPageVideoView);
                    MainPageVideoView.SetMediaController(mediaController);
                    MainPageVideoView.SetVideoURI(Uri.Parse(ViewModel.MediaElementSource));
                    MainPageVideoView.RequestFocus();
                }));

            MainPageCloseVideoButton.Click += MainPageCloseVideoButtonOnClick;
            MainPageCopyVideoLinkButton.Click += MainPageCopyVideoLinkButtonOnClick;
            MainPageVideoView.Prepared += MainPageVideoViewOnPrepared;

            ViewModel.PropertyChanged += ViewModelOnPropertyChanged;
            BuildDrawer();
            _drawer.OnDrawerItemClickListener = new HamburgerItemClickListener(OnHamburgerItemClick);

            _lastBottomNavSelectedItemId = MainPageBottomNav.SelectedItemId;
            StartBottomNavPolling();
            SetupBottomNavLongPress();

            MainPageCloseVideoButton.SetZ(0);
            MainPageCopyVideoLinkButton.SetZ(0);
            ShareFloatingActionButton.Hide();
        }

        private void ShareManagerOnTimerStateChanged(object sender, bool e)
        {
            if (e)
            {
                ShareFloatingActionButton.Show();
            }
            else
            {
                RunOnUiThread(() =>
                {
                    ShareFloatingActionButton.Hide();
                });
            }
        }

        private void MainPageCopyVideoLinkButtonOnClick(object o, EventArgs eventArgs)
        {
            ViewModel.CopyMediaElementUrlCommand.Execute(null);
        }

        private void MainPageCloseVideoButtonOnClick(object sender, EventArgs eventArgs)
        {
            ViewModel.MediaElementSource = null;
            ViewModel.MediaElementVisibility = false;
        }

        private void MainPageVideoViewOnPrepared(object sender, EventArgs eventArgs)
        {
            MainPageVideoView.Start();
        }

        private DroppyMenuPopup _bottomNavFilterMenu;

        private void SetupBottomNavLongPress()
        {
            var inner = MainPageBottomNav.GetChildAt(0) as LinearLayout;
            if (inner == null) return;

            for (int i = 0; i < inner.ChildCount; i++)
            {
                var itemView = inner.GetChildAt(i);
                var itemId = MainPageBottomNav.Menu.GetItem(i).ItemId;
                itemView.LongClickable = true;
                itemView.SetOnLongClickListener(new OnLongClickListener(v =>
                {
                    OnBottomNavLongClick(itemId);
                    return true;
                }));
            }
        }

        private void OnBottomNavLongClick(int itemId)
        {
            if (itemId == Resource.Id.bottom_nav_anime)
                ShowStatusFilterFlyout(false);
            else if (itemId == Resource.Id.bottom_nav_manga)
                ShowStatusFilterFlyout(true);
        }

        private void ShowStatusFilterFlyout(bool isManga)
        {
            var anchorView = MainPageBottomNav;
            var currentStatus = isManga
                ? (AnimeStatus)ViewModelLocator.AnimeList.CurrentStatus
                : (AnimeStatus)ViewModelLocator.AnimeList.CurrentStatus;

            _bottomNavFilterMenu = AnimeListPageFlyoutBuilder.BuildForAnimeStatusSelection(
                this, anchorView, status =>
                {
                    if (_bottomNavFilterMenu == null) return;
                    var workMode = isManga ? AnimeListWorkModes.Manga : AnimeListWorkModes.Anime;
                    var statusIndex = Array.IndexOf(Enum.GetValues(typeof(AnimeStatus)), status);
                    ViewModel.Navigate(PageIndex.PageAnimeList,
                        new AnimeListPageNavigationArgs(statusIndex, workMode));
                    _bottomNavFilterMenu.Dismiss(true);
                    _bottomNavFilterMenu = null;
                },
                currentStatus, isManga);
            _bottomNavFilterMenu.Show();
        }

        private void OnUpperFlyoutStatusChanged(AnimeStatus animeStatus)
        {
            if(_bottomNavFilterMenu == null)
                return;
            ViewModelLocator.AnimeList.CurrentStatus = (int)animeStatus;
            ViewModelLocator.AnimeList.RefreshList();
            _bottomNavFilterMenu.Dismiss(true);
            _bottomNavFilterMenu = null;
        }

        private void OnUpperDiscoverSectionSelected(int i)
        {
            (_lastFragment as DiscoverPageFragment)?.ScrollToSection(i);
        }

        private void OnHamburgerItemClick(View view, int i, IDrawerItem arg3)
        {
            if(!_allowHamburgerNavigation)
                return;

            OnHamburgerItemClick((PageIndex)arg3.Identifier);
            _drawer.SetSelection(arg3, false);
        }

        private void OnHamburgerItemClick(PageIndex page)
        {
            if(!_allowHamburgerNavigation)
                return;

            if (page == PageIndex.PageDiscover && ViewModel.CurrentMainPage == PageIndex.PageDiscover)
            {
                (_lastFragment as DiscoverPageFragment)?.ScrollToTop();
                _drawer.CloseDrawer();
                return;
            }

            ViewModelLocator.GeneralMain.Navigate(page, GetAppropriateArgsForPage(page));
            _drawer.CloseDrawer();
        }

        private bool _isBottomNavSyncing;

        private void OnBottomNavigationItemSelected(int itemId)
        {
            if (_isBottomNavSyncing) return;

            PageIndex page;
            switch (itemId)
            {
                case Resource.Id.bottom_nav_discover:
                    page = PageIndex.PageDiscover;
                    break;
                case Resource.Id.bottom_nav_anime:
                    page = PageIndex.PageAnimeList;
                    break;
                case Resource.Id.bottom_nav_manga:
                    page = PageIndex.PageMangaList;
                    break;
                case Resource.Id.bottom_nav_more:
                    page = PageIndex.PageMore;
                    break;
                default:
                    return;
            }

            ViewModelLocator.GeneralMain.Navigate(page, GetAppropriateArgsForPage(page));
        }

        private int _lastBottomNavSelectedItemId = -1;
        private Handler _bottomNavHandler;
        private Runnable _bottomNavRunnable;

        private void StartBottomNavPolling()
        {
            _bottomNavHandler = new Handler();
            _bottomNavRunnable = new Runnable(() =>
            {
                try
                {
                    var currentSelected = MainPageBottomNav.SelectedItemId;
                    if (_lastBottomNavSelectedItemId != -1 && currentSelected != _lastBottomNavSelectedItemId)
                    {
                        _lastBottomNavSelectedItemId = currentSelected;
                        if (!_isBottomNavSyncing)
                            OnBottomNavigationItemSelected(currentSelected);
                    }
                    else
                    {
                        _lastBottomNavSelectedItemId = currentSelected;
                    }
                }
                finally
                {
                    _bottomNavHandler.PostDelayed(_bottomNavRunnable, 200);
                }
            });
            _bottomNavHandler.PostDelayed(_bottomNavRunnable, 200);
        }



        private void SetRightTheme()
        {
            if (Settings.SelectedTheme == 1)
            {
                switch (AndroidColourThemeHelper.CurrentTheme)
                {
                    case AndroidColorThemes.Orange:
                        SetTheme(Resource.Style.Theme_MALPlus_Dark_Orange);
                        break;
                    case AndroidColorThemes.Purple:
                        SetTheme(Resource.Style.Theme_MALPlus_Dark_Purple);
                        break;
                    case AndroidColorThemes.Blue:
                        SetTheme(Resource.Style.Theme_MALPlus_Dark_Blue);
                        break;
                    case AndroidColorThemes.Lime:
                        SetTheme(Resource.Style.Theme_MALPlus_Dark_Lime);
                        break;
                    case AndroidColorThemes.Pink:
                        SetTheme(Resource.Style.Theme_MALPlus_Dark_Pink);
                        break;
                    case AndroidColorThemes.Cyan:
                        SetTheme(Resource.Style.Theme_MALPlus_Dark_Cyan);
                        break;
                    case AndroidColorThemes.SkyBlue:
                        SetTheme(Resource.Style.Theme_MALPlus_Dark_SkyBlue);
                        break;
                    case AndroidColorThemes.Red:
                        SetTheme(Resource.Style.Theme_MALPlus_Dark_Red);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                if (Settings.DarkThemeAmoled)
                {
                    Theme.ApplyStyle(Resource.Style.BlackTheme, true);
                    IsAmoledApplied = true;
                }
                else
                {
                    IsAmoledApplied = false;
                }          
            }
            else
            {
                switch (AndroidColourThemeHelper.CurrentTheme)
                {
                    case AndroidColorThemes.Orange:
                        SetTheme(Resource.Style.Theme_MALPlus_Light_Orange);
                        break;                                  
                    case AndroidColorThemes.Purple:             
                        SetTheme(Resource.Style.Theme_MALPlus_Light_Purple);
                        break;
                    case AndroidColorThemes.Blue:
                        SetTheme(Resource.Style.Theme_MALPlus_Light_Blue);
                        break;
                    case AndroidColorThemes.Lime:
                        SetTheme(Resource.Style.Theme_MALPlus_Light_Lime);
                        break;
                    case AndroidColorThemes.Pink:
                        SetTheme(Resource.Style.Theme_MALPlus_Light_Pink);
                        break;
                    case AndroidColorThemes.Cyan:
                        SetTheme(Resource.Style.Theme_MALPlus_Light_Cyan);
                        break;
                    case AndroidColorThemes.SkyBlue:
                        SetTheme(Resource.Style.Theme_MALPlus_Light_SkyBlue);
                        break;
                    case AndroidColorThemes.Red:
                        SetTheme(Resource.Style.Theme_MALPlus_Light_Red);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }




        public override void OnConfigurationChanged(Configuration newConfig)
        {
            //if (newConfig.Orientation == global::Android.Content.Res.Orientation.Landscape)
            //{

            //}
            //else
            //{
            //    MainPageStatusContainer.Orientation = Orientation.Horizontal;

            //    MainPageCurrentStatus.SetMaxLines(2);
            //    UpdateCurrentStatusWidth();
            //    var margin = DimensionsHelper.DpToPx(5);


            //    var param = MainPageCurrentStatus.LayoutParameters as LinearLayout.LayoutParams;
            //    param.SetMargins(margin, margin, margin, margin);
            //    MainPageCurrentStatus.LayoutParameters = param;

            //    param = MainPageCurrentSatusSubtitle.LayoutParameters as LinearLayout.LayoutParams;
            //    param.SetMargins(margin, margin, margin, margin);
            //    MainPageCurrentSatusSubtitle.LayoutParameters = param;

            //    var cparam = MainPageStatusContainer.LayoutParameters;
            //    cparam.Height = -1;
            //    MainPageStatusContainer.LayoutParameters = cparam;

            //}
            
            base.OnConfigurationChanged(newConfig);
        }

        #region Views

        private FrameLayout _mainContentFrame;
        private AdView _mainPageAdView;
        private FloatingActionButton _shareFloatingActionButton;
        private VideoView _mainPageVideoView;
        private ImageButton _mainPageCopyVideoLinkButton;
        private ImageButton _mainPageCloseVideoButton;
        private RelativeLayout _mainPageVideoViewContainer;
        private LinearLayout _mainPageRoot;
        private BottomNavigationView _mainPageBottomNav;

        public FrameLayout MainContentFrame => GetView(ref _mainContentFrame, Resource.Id.MainContentFrame);
        public AdView MainPageAdView => GetView(ref _mainPageAdView, Resource.Id.MainPageAdView);
        public FloatingActionButton ShareFloatingActionButton => GetView(ref _shareFloatingActionButton, Resource.Id.ShareFloatingActionButton);
        public VideoView MainPageVideoView => GetView(ref _mainPageVideoView, Resource.Id.MainPageVideoView);
        public ImageButton MainPageCopyVideoLinkButton => GetView(ref _mainPageCopyVideoLinkButton, Resource.Id.MainPageCopyVideoLinkButton);
        public ImageButton MainPageCloseVideoButton => GetView(ref _mainPageCloseVideoButton, Resource.Id.MainPageCloseVideoButton);
        public RelativeLayout MainPageVideoViewContainer => GetView(ref _mainPageVideoViewContainer, Resource.Id.MainPageVideoViewContainer);
        public LinearLayout MainPageRoot => GetView(ref _mainPageRoot, Resource.Id.MainPageRoot);

        public BottomNavigationView MainPageBottomNav => GetView(ref _mainPageBottomNav, Resource.Id.MainPageBottomNav);

        #endregion

    }
}
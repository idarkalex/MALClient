using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.Widget;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using Com.Oguzdev.Circularfloatingactionmenu.Library;
using Com.Shehabic.Droppy;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Helpers;
using MALClient.Android.Activities;
using MALClient.Android.BindingConverters;
using MALClient.Android.CollectionAdapters;
using MALClient.Android.DIalogs;
using MALClient.Android.Flyouts;
using MALClient.Android.Listeners;
using MALClient.Android.Resources;
using MALClient.Android.UserControls;
using MALClient.Models.Enums;
using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using static MALClient.Android.Flyouts.AnimeListPageFlyoutBuilder;
using Debug = System.Diagnostics.Debug;
using FloatingActionButton = Android.Support.Design.Widget.FloatingActionButton;

namespace MALClient.Android.Fragments
{
    public partial class AnimeListPageFragment : MalFragmentBase
    {
        private const string FabMenuLoadDetails = "Load all details";
        private const string FabMenuDisplayModes = "Display modes";
        private const string FabMenuSetListSource = "Set list source";

        private DroppyMenuPopup _fabMenu;
        private FloatingActionMenu _actionMenu;

        private bool _autoLoadingMore;
        private LoadMoreScrollListener _loadMoreScrollListener;

        private class LoadMoreScrollListener : Java.Lang.Object, AbsListView.IOnScrollListener
        {
            private readonly Action _onApproachingEnd;

            public LoadMoreScrollListener(Action onApproachingEnd)
            {
                _onApproachingEnd = onApproachingEnd;
            }

            public void OnScroll(AbsListView view, int firstVisibleItem, int visibleItemCount, int totalItemCount)
            {
                if (totalItemCount > 0 && firstVisibleItem + visibleItemCount >= totalItemCount - 4)
                    _onApproachingEnd?.Invoke();
            }

            public void OnScrollStateChanged(AbsListView view, ScrollState scrollState)
            {
            }
        }

        private void LoadMoreOnScrollApproachedEnd()
        {
            if (_autoLoadingMore || ViewModel.Loading || !ViewModel.CanLoadMore)
                return;
            _autoLoadingMore = true;
            ViewModel.LoadMoreCommand.Execute(null);
        }

        protected override void InitBindings()
        {
            var swipeRefresh = RootView as SwipeRefreshLayout;

            RootView.ViewTreeObserver.GlobalLayout += (sender, args) =>
            {
                Rect r = new Rect();
                RootView.GetWindowVisibleDisplayFrame(r);
                int keypadHeight = RootView.RootView.Height - r.Bottom;

                if (keypadHeight > RootView.Height * 0.15)
                {
                    AnimeListPageActionButton.Hide();
                }
                else
                {
                    AnimeListPageActionButton.Show();
                }
            };
            //AnimeListPageGridView.ScrollingCacheEnabled = false;

            Bindings.Add(
                this.SetBinding(() => ViewModel.Loading,
                    () => AnimeListPageLoadingSpinner.Visibility).ConvertSourceToTarget(Converters.BoolToVisibility));

            Bindings.Add(
                this.SetBinding(() => ViewModel.EmptyNoticeVisibility,
                    () => AnimeListPageEmptyNotice.Visibility)
                    .ConvertSourceToTarget(Converters.BoolToVisibility));

            ViewModel.PropertyChanged += AnimeListOnPropertyChanged;
            ViewModel.ScrollIntoViewRequested += ViewModelOnScrollIntoViewRequested;

            _loadMoreScrollListener = new LoadMoreScrollListener(LoadMoreOnScrollApproachedEnd);
            AnimeListPageGridView.SetOnScrollListener(_loadMoreScrollListener);
            AnimeListPageListView.SetOnScrollListener(_loadMoreScrollListener);
            AnimeListPageCompactListView.SetOnScrollListener(_loadMoreScrollListener);

            AnimeListPageActionButton.LongClickable = true;
            AnimeListPageActionButton.SetOnLongClickListener(new OnLongClickListener(view =>
            {
                var items = new List<string>();

                if (ViewModel.AppBtnListSourceVisibility)
                    items.Add(FabMenuSetListSource);
                if (ViewModel.LoadAllDetailsButtonVisiblity)
                    items.Add(FabMenuLoadDetails);
                items.Add(FabMenuDisplayModes);
                _fabMenu = FlyoutMenuBuilder.BuildGenericFlyout(Activity, AnimeListPageActionButton, items,
                    OnFabMenuItemClicked);
                _fabMenu.Tag = items;
                _fabMenu.Show();
            }));

            swipeRefresh.NestedScrollingEnabled = true;
            swipeRefresh.Refresh += (sender, args) =>
            {
                swipeRefresh.Refreshing = false;

                ViewModel.RefreshCommand.Execute(null);
            };

            Bindings.Add(this.SetBinding(() => ViewModel.WorkMode).WhenSourceChanges(InitActionMenu));

            InitDrawer();
        }

        private void InitActionMenu()
        {
            _actionMenu?.Close(true);
            _actionMenu?.Dispose();
            var param = new ViewGroup.LayoutParams(DimensionsHelper.DpToPx(45), DimensionsHelper.DpToPx(45));
            var builder = new FloatingActionMenu.Builder(Activity)
                .AddSubActionView(BuildFabActionButton(param, Resource.Drawable.icon_filter))
                .AddSubActionView(BuildFabActionButton(param, Resource.Drawable.icon_sort))
                .AddSubActionView(BuildFabActionButton(param, Resource.Drawable.icon_shuffle));
            switch (ViewModel.WorkMode)
            {
                case AnimeListWorkModes.SeasonalAnime:
                    builder.AddSubActionView(BuildFabActionButton(param, Resource.Drawable.icon_calendar));
                    builder.SetRadius(DimensionsHelper.DpToPx(95));
                    break;

                case AnimeListWorkModes.TopAnime:
                case AnimeListWorkModes.TopManga:
                    builder.AddSubActionView(BuildFabActionButton(param, Resource.Drawable.icon_arrow_down));
                    builder.SetRadius(DimensionsHelper.DpToPx(95));
                    break;

                default:
                    builder.SetRadius(DimensionsHelper.DpToPx(75));
                    break;
            }
            _actionMenu = builder.AttachTo(AnimeListPageActionButton).Build();
        }

        private void ViewModelOnScrollIntoViewRequested(AnimeItemViewModel item, bool select)
        {
            var list = SwipeRefreshLayout.ScrollingView as AbsListView;
            if (item != ViewModel.AnimeItems.FirstOrDefault() && list.Adapter is IBugFixingGridViewAdapter adapter)
                adapter.HandledGridViewBug = true;
            list?.SetSelection(ViewModel.AnimeItems.IndexOf(item));
        }

        private View BuildFabActionButton(ViewGroup.LayoutParams param, int icon)
        {
            var b1 = new FloatingActionButton(Activity)
            {
                LayoutParameters = param,
                Clickable = true,
                Focusable = true
            };
            b1.Size = FloatingActionButton.SizeMini;
            b1.SetScaleType(ImageView.ScaleType.Center);
            b1.SetImageResource(icon);
            b1.ImageTintList = ColorStateList.ValueOf(new Color(255, 255, 255));
            b1.BackgroundTintList = ColorStateList.ValueOf(new Color(ResourceExtension.AccentColourContrast));
            b1.Tag = icon;
            b1.Click += OnFloatingActionButtonOptionClick;
            return b1;
        }

        private void OnFloatingActionButtonOptionClick(object sender, EventArgs eventArgs)
        {
            _actionMenu.Close(true);
            RightDrawer.OnDrawerItemClickListener = null;
            switch ((int)(sender as View).Tag)
            {
                case Resource.Drawable.icon_filter:
                    OpenFiltersDrawer(true);
                    break;

                case Resource.Drawable.icon_sort:
                    OpenSortingDrawer();
                    break;

                case Resource.Drawable.icon_shuffle:
                    ViewModel.SelectAtRandomCommand.Execute(null);
                    break;

                case Resource.Drawable.icon_calendar:
                    OpenSeasonalSelectionDrawer();
                    break;

                case Resource.Drawable.icon_arrow_down:
                    OpenTopTypeDrawer();
                    break;
            }
        }

        private void OnFabMenuItemClicked(int i)
        {
            switch ((_fabMenu.Tag as List<string>)[i])
            {
                case FabMenuLoadDetails:
                    ViewModel.LoadAllItemsDetailsCommand.Execute(null);
                    ResourceLocator.SnackbarProvider.ShowText("Started pulling data in background.");
                    break;

                case FabMenuSetListSource:
                    SetListSource();
                    break;

                case FabMenuDisplayModes:
                    OpenViewModeDrawer();
                    break;
            }
            _fabMenu.Dismiss(true);
        }

        private async void SetListSource()
        {
            var src = await TextInputDialogBuilder.BuildInputTextDialog(Activity, "List source", "username...", "Go!", true);
            if (string.IsNullOrWhiteSpace(src))
                return;
            if (src.Length > 2)
            {
                ViewModel.ListSource = src;
                await ViewModel.FetchData();
            }
            else
            {
                ResourceLocator.SnackbarProvider.ShowText("Invalid username");
            }
        }

        private void AnimeListOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            MainActivity.CurrentContext.RunOnUiThread(async () =>
            {
                if (propertyChangedEventArgs.PropertyName == nameof(ViewModel.Loading) && !ViewModel.Loading)
                    _autoLoadingMore = false;
                if (propertyChangedEventArgs.PropertyName == nameof(ViewModelLocator.AnimeList.AnimeGridItems))
                {
                    if (ViewModel.AnimeGridItems != null)
                    {

                        AnimeListPageGridView.InjectAnimeListAdapter(Context, ViewModel.AnimeGridItems, AnimeListDisplayModes.IndefiniteGrid, AnimeListPageGridViewOnItemClick);
                        _gridViewColumnHelper = new GridViewColumnHelper(AnimeListPageGridView, null, Settings.SqueezeOneMoreGridItem ? 3 : 2, 3);
                        _gridViewColumnHelper.ForceColumns = 3;

                        SwipeRefreshLayout.ScrollingView = AnimeListPageGridView;

                        AnimeListPageListView.ClearFlingAdapter();
                        AnimeListPageCompactListView.ClearFlingAdapter();

                        await Task.Delay(250);
                        if (ViewModel.AnimeGridItems == null)
                            return;
                        if (_prevArgs != null)
                        {
                            var pos = _prevArgs.SelectedItemIndex;

                            AnimeListPageGridView.RequestFocusFromTouch();
                            AnimeListPageGridView.SetSelection(pos);
                            AnimeListPageGridView.RequestFocus();
                            _prevArgs = null;
                        }

                    }
                }
                else if (propertyChangedEventArgs.PropertyName == nameof(ViewModelLocator.AnimeList.AnimeListItems))
                {
                    if (ViewModel.AnimeListItems != null)
                    {

                        AnimeListPageListView.InjectAnimeListAdapter(Context, ViewModel.AnimeListItems,
                            AnimeListDisplayModes.IndefiniteList, AnimeListPageGridViewOnItemClick);

                        if (_prevArgs != null)
                        {
                            AnimeListPageListView.SmoothScrollToPosition(_prevArgs.SelectedItemIndex);
                            _prevArgs = null;
                        }

                        SwipeRefreshLayout.ScrollingView = AnimeListPageListView;

                        AnimeListPageGridView.ClearFlingAdapter();
                        AnimeListPageCompactListView.ClearFlingAdapter();
                    }
                }
                else if (propertyChangedEventArgs.PropertyName == nameof(ViewModelLocator.AnimeList.AnimeCompactItems))
                {
                    if (ViewModel.AnimeCompactItems != null)
                    {

                        AnimeListPageCompactListView.InjectAnimeListAdapter(Context, ViewModel.AnimeCompactItems, AnimeListDisplayModes.IndefiniteCompactList, AnimeListPageGridViewOnItemClick);

                        if (_prevArgs != null)
                        {
                            AnimeListPageListView.SmoothScrollToPosition(_prevArgs.SelectedItemIndex);
                            _prevArgs = null;
                        }

                        SwipeRefreshLayout.ScrollingView = AnimeListPageCompactListView;

                        AnimeListPageListView.ClearFlingAdapter();
                        AnimeListPageGridView.ClearFlingAdapter();
                    }
                }
                else if (propertyChangedEventArgs.PropertyName == nameof(ViewModel.DisplayMode))
                {
                    switch (ViewModel.DisplayMode)
                    {
                        case AnimeListDisplayModes.IndefiniteList:
                            AnimeListPageListView.Visibility = ViewStates.Visible;

                            AnimeListPageGridView.Visibility = ViewStates.Gone;
                            AnimeListPageCompactListView.Visibility = ViewStates.Gone;
                            break;

                        case AnimeListDisplayModes.IndefiniteGrid:
                            AnimeListPageGridView.Visibility = ViewStates.Visible;

                            AnimeListPageListView.Visibility = ViewStates.Gone;
                            AnimeListPageCompactListView.Visibility = ViewStates.Gone;
                            break;

                        case AnimeListDisplayModes.IndefiniteCompactList:
                            AnimeListPageCompactListView.Visibility = ViewStates.Visible;

                            AnimeListPageListView.Visibility = ViewStates.Gone;
                            AnimeListPageGridView.Visibility = ViewStates.Gone;
                            break;
                    }
                }
            });
           
        }

        public ScrollableSwipeToRefreshLayout SwipeRefreshLayout => RootView as ScrollableSwipeToRefreshLayout;

        #region Views

        private GridView _animeListPageGridView;
        private ListView _animeListPageListView;
        private ListView _animeListPageCompactListView;
        private RelativeLayout _animeListPageLoadingSpinner;
        private TextView _animeListPageEmptyNotice;
        private FloatingActionButton _animeListPageActionButton;

        public GridView AnimeListPageGridView => GetView(ref _animeListPageGridView, Resource.Id.AnimeListPageGridView);

        public ListView AnimeListPageListView => GetView(ref _animeListPageListView, Resource.Id.AnimeListPageListView);

        public ListView AnimeListPageCompactListView => GetView(ref _animeListPageCompactListView, Resource.Id.AnimeListPageCompactListView);

        public RelativeLayout AnimeListPageLoadingSpinner => GetView(ref _animeListPageLoadingSpinner, Resource.Id.AnimeListPageLoadingSpinner);

        public TextView AnimeListPageEmptyNotice => GetView(ref _animeListPageEmptyNotice, Resource.Id.AnimeListPageEmptyNotice);

        public FloatingActionButton AnimeListPageActionButton => GetView(ref _animeListPageActionButton, Resource.Id.AnimeListPageActionButton);

        #endregion Views
    }
}
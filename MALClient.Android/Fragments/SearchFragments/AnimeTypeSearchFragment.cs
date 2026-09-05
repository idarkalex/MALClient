using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Android.OS;
using Android.Views;
using Android.Widget;
using Android.Support.V7.Widget;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Helpers;
using MALClient.Android.Activities;
using MALClient.Android.Resources;
using MALClient.Models.Enums;
using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALClient.Android.Fragments.SearchFragments
{
    public class AnimeTypeSearchFragment : MalFragmentBase
    {
        private readonly bool _isGenreMode;
        private List<Enum> _allChoices;
        private List<Enum> _filteredChoices;
        private bool _catalogueActive;
        private string _catalogueTitle;
        private string _filterToRestore;
        private CatalogueGridAdapter _catalogueAdapter;

        public bool IsCatalogueActive => _catalogueActive;

        public AnimeTypeSearchFragment(bool isGenreMode) : base(false)
        {
            _isGenreMode = isGenreMode;
        }

        protected override void Init(Bundle savedInstanceState)
        {
            _allChoices = _isGenreMode
                ? Enum.GetValues(typeof(AnimeGenreSearch)).Cast<Enum>().OrderBy(val => val.GetDescription()).ToList()
                : Enum.GetValues(typeof(AnimeStudios)).Cast<Enum>().OrderBy(val => val.GetDescription()).ToList();
            var savedFilter = _isGenreMode ? ViewModelLocator.SearchPage.GenreFilterQuery : ViewModelLocator.SearchPage.StudioFilterQuery;
            if (string.IsNullOrWhiteSpace(savedFilter))
                _filteredChoices = new List<Enum>(_allChoices);
            else
            {
                var q = savedFilter.ToLower();
                _filteredChoices = _allChoices.Where(c => c.GetDescription().ToLower().Contains(q)).ToList();
            }
        }

        protected override void InitBindings()
        {
            AnimeTypeSearchResultsList.SetLayoutManager(new GridLayoutManager(Activity, 3));
            _catalogueAdapter = new CatalogueGridAdapter(ViewModelLocator.SearchPage.CatalogueResults, Activity, AnimeTypeSearchResultsList);
            AnimeTypeSearchResultsList.SetAdapter(_catalogueAdapter);
            AttachScrollListener();
            _catalogueActive = ViewModelLocator.SearchPage.CatalogueIsGenre.HasValue && ViewModelLocator.SearchPage.CatalogueIsGenre.Value == _isGenreMode;
            _catalogueTitle = ViewModelLocator.SearchPage.ActiveCatalogueTitle;
            if (_catalogueActive)
            {
                AnimeTypeSearchPageList.Visibility = ViewStates.Gone;
                AnimeTypeSearchResultsList.Visibility = ViewStates.Visible;
                AnimeTypeSearchLoadingSpinner.Visibility = ViewStates.Gone;
                if (!string.IsNullOrEmpty(_catalogueTitle))
                {
                    AnimeTypeSearchCatalogueTitle.Text = _catalogueTitle;
                    AnimeTypeSearchCatalogueTitle.Visibility = ViewStates.Visible;
                }
                var scPos = ViewModelLocator.SearchPage.CatalogueScrollPosition;
                var scOff = ViewModelLocator.SearchPage.CatalogueScrollOffset;
                if (scPos > 0)
                    AnimeTypeSearchResultsList.Post(() =>
                    {
                        try
                        {
                            var lm = AnimeTypeSearchResultsList.GetLayoutManager() as GridLayoutManager;
                            if (lm != null)
                                lm.ScrollToPositionWithOffset(scPos, scOff);
                        } catch { }
                    });
            }
            else
            {
                AnimeTypeSearchResultsList.Visibility = ViewStates.Gone;
                AnimeTypeSearchLoadingSpinner.Visibility = ViewStates.Gone;
                AnimeTypeSearchCatalogueTitle.Visibility = ViewStates.Gone;
            }

            RefreshAdapter();
            AnimeTypeSearchPageList.SetOnScrollListener(new GenreListScrollListener(_isGenreMode));
            var savedListPos = _isGenreMode ? ViewModelLocator.SearchPage.GenreListScrollPosition : ViewModelLocator.SearchPage.StudioListScrollPosition;
            if (savedListPos > 0)
            {
                var list = AnimeTypeSearchPageList;
                list.Post(() =>
                {
                    try { list.SetSelection(savedListPos); } catch { }
                });
            }
        }

        private RecyclerView _scrollListenersAttachedFor;
        private void AttachScrollListener()
        {
            if (_scrollListenersAttachedFor == AnimeTypeSearchResultsList)
                return;
            _scrollListenersAttachedFor = AnimeTypeSearchResultsList;
            AnimeTypeSearchResultsList.AddOnScrollListener(new CatalogueScrollListener(AnimeTypeSearchResultsList));
        }

        public void ShowCatalogue(string title, AnimeStudios? studio, AnimeGenreSearch? genre)
        {
            var wasGenre = _isGenreMode;
            // Capture the current filter from the VM — will be clobbered by QueryTextChange("") when keyboard dismisses
            var preFilter = wasGenre ? ViewModelLocator.SearchPage.GenreFilterQuery : ViewModelLocator.SearchPage.StudioFilterQuery;
            _catalogueActive = true;
            _catalogueTitle = title;
            ViewModelLocator.GeneralMain.CurrentStatus = title;
            var args = new SearchPageNavigationArgs { IsCatalogue = true, CatalogueTitle = title, Studio = studio, Genre = genre };
            SwitchToList(false);
            // Restore filter IMMEDIATELY after SwitchToList — QueryTextChange("") already fired and cleared it
            if (!string.IsNullOrEmpty(_filterToRestore))
            {
                if (wasGenre) ViewModelLocator.SearchPage.GenreFilterQuery = _filterToRestore;
                else ViewModelLocator.SearchPage.StudioFilterQuery = _filterToRestore;
            }
            else if (!string.IsNullOrEmpty(preFilter))
            {
                if (wasGenre) ViewModelLocator.SearchPage.GenreFilterQuery = preFilter;
                else ViewModelLocator.SearchPage.StudioFilterQuery = preFilter;
            }
            _filterToRestore = null; // clear after use
            AnimeTypeSearchCatalogueTitle.Text = title;
            AnimeTypeSearchCatalogueTitle.Visibility = ViewStates.Visible;
            ViewModelLocator.SearchPage.LoadCatalogue(args).ContinueWith(_ =>
            {
                Activity?.RunOnUiThread(() =>
                {
                    if (!_catalogueActive)
                        return;
                    _catalogueAdapter?.NotifyDataSetChanged();
                    AnimeTypeSearchLoadingSpinner.Visibility = ViewStates.Gone;
                    AnimeTypeSearchResultsList.Visibility = ViewStates.Visible;
                });
            });

            ReRegisterBackOverride();
        }

        private void ReRegisterBackOverride()
        {
            ViewModelLocator.NavMgr.RegisterOneTimeMainOverride(new RelayCommand(ExitCatalogue));
        }

        public override void OnResume()
        {
            base.OnResume();
            if (_catalogueActive)
                ReRegisterBackOverride();
        }

        public void ExitCatalogue()
        {
            if (!_catalogueActive)
                return;
            _catalogueActive = false;
            _catalogueTitle = null;
            ViewModelLocator.SearchPage.ClearCatalogueSession();
            AnimateTitleGone();
            SwitchToList(true);
        }

        private void AnimateTitleGone()
        {
            AnimeTypeSearchCatalogueTitle.Visibility = ViewStates.Gone;
        }

        private void SwitchToList(bool showList)
        {
            if (showList)
            {
                AnimeTypeSearchPageList.Visibility = ViewStates.Visible;
                AnimeTypeSearchResultsList.Visibility = ViewStates.Gone;
                AnimeTypeSearchLoadingSpinner.Visibility = ViewStates.Gone;
            }
            else
            {
                AnimeTypeSearchPageList.Visibility = ViewStates.Gone;
                AnimeTypeSearchResultsList.Visibility = ViewStates.Gone;
                AnimeTypeSearchLoadingSpinner.Visibility = ViewStates.Visible;
            }
        }

        public void FilterChoices(string query)
        {
            if (_catalogueActive)
                return;
            // Don't overwrite the saved filter with empty string — keyboard dismiss fires QueryTextChange("")
            // which would clobber the filter the user typed. The list still resets to all for visual feedback.
            if (!string.IsNullOrWhiteSpace(query))
            {
                if (_isGenreMode) ViewModelLocator.SearchPage.GenreFilterQuery = query;
                else ViewModelLocator.SearchPage.StudioFilterQuery = query;
            }
            if (_allChoices == null)
                _allChoices = _isGenreMode ? Enum.GetValues(typeof(AnimeGenreSearch)).Cast<Enum>().OrderBy(val => val.GetDescription()).ToList() : Enum.GetValues(typeof(AnimeStudios)).Cast<Enum>().OrderBy(val => val.GetDescription()).ToList();
            if (string.IsNullOrWhiteSpace(query))
                _filteredChoices = new List<Enum>(_allChoices);
            else
            {
                var q = query.ToLower();
                _filteredChoices = _allChoices.Where(c => c.GetDescription().ToLower().Contains(q)).ToList();
            }
            RefreshAdapter();
        }

        public void RefreshAdapter()
        {
            if (AnimeTypeSearchPageList == null)
                return;
            if (_allChoices == null)
                _allChoices = _isGenreMode ? Enum.GetValues(typeof(AnimeGenreSearch)).Cast<Enum>().OrderBy(val => val.GetDescription()).ToList() : Enum.GetValues(typeof(AnimeStudios)).Cast<Enum>().OrderBy(val => val.GetDescription()).ToList();
            if (_filteredChoices == null) _filteredChoices = new List<Enum>(_allChoices);
            AnimeTypeSearchPageList.NumColumns = 3;
            var ctx = Activity ?? MainActivity.CurrentContext ?? global::Android.App.Application.Context;
            var footer = new View(ctx);
            footer.Visibility = ViewStates.Gone;
            AnimeTypeSearchPageList.Adapter = _filteredChoices.GetAdapter(GetTemplateDelegate, footer, true);
        }

        private View GetTemplateDelegate(int i, Enum parameter, View convertView)
        {
            try
            {
                var view = convertView;
                if (view == null)
                {
                    var ctx = Activity ?? MainActivity.CurrentContext ?? global::Android.App.Application.Context;
                    try
                    {
                        var inflater = ctx != null ? LayoutInflater.From(ctx) : null;
                        var inflatedView = inflater?.Inflate(Resource.Layout.AnimeSearchTypeItem, null);
                        view = inflatedView;
                    }
                    catch { }
                    if (view == null)
                    {
                        var fallbackCtx = Activity ?? MainActivity.CurrentContext ?? global::Android.App.Application.Context;
                        var tvFallback = new TextView(fallbackCtx);
                        tvFallback.SetPadding(20, 20, 20, 20);
                        tvFallback.Text = parameter?.GetDescription() ?? "";
                        tvFallback.Tag = parameter?.Wrap();
                        var fallbackLp = new AbsListView.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
                        tvFallback.LayoutParameters = fallbackLp;
                        return tvFallback;
                    }
                    view.Click += ViewOnClick;
                }
                else
                {
                    var lp = view.LayoutParameters;
                    if (lp == null)
                    {
                        view.LayoutParameters = new AbsListView.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
                    }
                }
                try
                {
                    int targetDp = _isGenreMode ? 56 : 64;
                    var lp = view.LayoutParameters;
                    if (lp != null)
                    {
                        lp.Height = (int)global::Android.Util.TypedValue.ApplyDimension(global::Android.Util.ComplexUnitType.Dip, targetDp, view.Context.Resources.DisplayMetrics);
                        view.LayoutParameters = lp;
                    }
                    else
                    {
                        view.LayoutParameters = new AbsListView.LayoutParams(ViewGroup.LayoutParams.MatchParent, (int)global::Android.Util.TypedValue.ApplyDimension(global::Android.Util.ComplexUnitType.Dip, targetDp, view.Context.Resources.DisplayMetrics));
                    }
                    view.RequestLayout();
                    var tvInner = view.FindViewById<TextView>(Resource.Id.AnimeSearchTypeItemTextView);
                    if (tvInner != null)
                    {
                        tvInner.SetMaxLines(_isGenreMode ? 1 : 2);
                        tvInner.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End;
                    }
                }
                catch { }
                var tv = view.FindViewById<TextView>(Resource.Id.AnimeSearchTypeItemTextView);
                if (tv != null) tv.Text = parameter?.GetDescription() ?? "";
                else
                {
                    if (view is TextView tv2) tv2.Text = parameter?.GetDescription() ?? "";
                }
                view.Tag = parameter?.Wrap();
                return view;
            }
            catch
            {
                try
                {
                    var fallbackCtx = Activity ?? MainActivity.CurrentContext ?? global::Android.App.Application.Context;
                    var tvFallback = new TextView(fallbackCtx);
                    tvFallback.SetPadding(20, 20, 20, 20);
                    tvFallback.Text = parameter?.GetDescription() ?? "item";
                    var fallbackLp = new AbsListView.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
                    tvFallback.LayoutParameters = fallbackLp;
                    return tvFallback;
                }
                catch { return new TextView(global::Android.App.Application.Context) { Text = "item" }; }
            }
        }

        private void ViewOnClick(object sender, EventArgs eventArgs)
        {
            var item = (sender as View).Tag.Unwrap<Enum>();
            if (_isGenreMode)
                OnGenreClick(item);
            else
                OnStudioClick(item);
        }

        private void OnGenreClick(Enum genre)
        {
            _filterToRestore = _isGenreMode ? ViewModelLocator.SearchPage.GenreFilterQuery : ViewModelLocator.SearchPage.StudioFilterQuery;
            var g = (AnimeGenreSearch)genre;
            ShowCatalogue(g.GetDescription(), null, g);
        }

        private void OnStudioClick(Enum studio)
        {
            _filterToRestore = _isGenreMode ? ViewModelLocator.SearchPage.GenreFilterQuery : ViewModelLocator.SearchPage.StudioFilterQuery;
            var s = (AnimeStudios)studio;
            ShowCatalogue(s.GetDescription(), s, null);
        }

        public override int LayoutResourceId => Resource.Layout.AnimeTypeSearchPage;

        #region Views

        private GridView _animeTypeSearchPageList;
        private RecyclerView _animeTypeSearchResultsList;
        private ProgressBar _animeTypeSearchLoadingSpinner;
        private TextView _animeTypeSearchCatalogueTitle;

        public GridView AnimeTypeSearchPageList => GetView(ref _animeTypeSearchPageList, Resource.Id.AnimeTypeSearchPageList);
        public RecyclerView AnimeTypeSearchResultsList => GetView(ref _animeTypeSearchResultsList, Resource.Id.AnimeTypeSearchResultsList);
        public ProgressBar AnimeTypeSearchLoadingSpinner => GetView(ref _animeTypeSearchLoadingSpinner, Resource.Id.AnimeTypeSearchLoadingSpinner);
        public TextView AnimeTypeSearchCatalogueTitle => GetView(ref _animeTypeSearchCatalogueTitle, Resource.Id.AnimeTypeSearchCatalogueTitle);

        #endregion
    }

    class CatalogueGridAdapter : RecyclerView.Adapter
    {
        private readonly System.Collections.Generic.IList<AnimeSearchItemViewModel> _items;
        private readonly global::Android.Content.Context _context;
        private readonly RecyclerView _recyclerView;
        private bool _notifyScheduled;

        public CatalogueGridAdapter(System.Collections.Generic.IList<AnimeSearchItemViewModel> items, global::Android.Content.Context context, RecyclerView recyclerView)
        {
            _items = items;
            _context = context;
            _recyclerView = recyclerView;
            if (items is System.Collections.Specialized.INotifyCollectionChanged notifyCollection)
                notifyCollection.CollectionChanged += (s, e) => RequestNotifyIfNeeded();
        }

        private void RequestNotifyIfNeeded()
        {
            if (_notifyScheduled)
                return;
            _notifyScheduled = true;
            var rv = _recyclerView;
            rv?.Post(() =>
            {
                _notifyScheduled = false;
                try { NotifyDataSetChanged(); } catch { }
            });
        }

        public override int ItemCount => _items?.Count ?? 0;

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var view = LayoutInflater.From(_context).Inflate(Resource.Layout.SearchPosterItem, parent, false);
            return new CatalogueHolder(view);
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            ((CatalogueHolder)holder).Bind(_items[position]);
        }
    }

    class CatalogueHolder : RecyclerView.ViewHolder
    {
        private readonly FFImageLoading.Views.ImageViewAsync _posterImage;
        private readonly TextView _posterTitle;
        private readonly TextView _posterScore;
        private readonly TextView _posterType;
        private AnimeSearchItemViewModel _currentItem;

        public CatalogueHolder(View view) : base(view)
        {
            _posterImage = view.FindViewById<FFImageLoading.Views.ImageViewAsync>(Resource.Id.SearchPosterImage);
            _posterTitle = view.FindViewById<TextView>(Resource.Id.SearchPosterTitle);
            _posterScore = view.FindViewById<TextView>(Resource.Id.SearchPosterScore);
            _posterType = view.FindViewById<TextView>(Resource.Id.SearchPosterType);
            ItemView.Click += (s, e) =>
            {
                ViewModelLocator.NavMgr.ResetOneTimeMainOverride();
                _currentItem?.NavigateDetails();
            };
        }

        public void Bind(AnimeSearchItemViewModel item)
        {
            _currentItem = item;
            _posterImage.AnimeInto(item.ImgUrl);
            _posterTitle.Text = item.Title;
            if (item.GlobalScore > 0)
            {
                _posterScore.Text = item.GlobalScoreBind;
                _posterScore.Visibility = ViewStates.Visible;
            }
            else
                _posterScore.Visibility = ViewStates.Gone;
            if (!string.IsNullOrWhiteSpace(item.Type))
            {
                _posterType.Text = item.Type;
                _posterType.Visibility = ViewStates.Visible;
            }
            else
                _posterType.Visibility = ViewStates.Gone;
        }
    }

    class CatalogueScrollListener : RecyclerView.OnScrollListener
    {
        private readonly RecyclerView _list;

        public CatalogueScrollListener(RecyclerView list)
        {
            _list = list;
        }

        public override void OnScrolled(RecyclerView recyclerView, int dx, int dy)
        {
            base.OnScrolled(recyclerView, dx, dy);
            if (dy <= 0)
                return;
            var layoutManager = _list.GetLayoutManager() as GridLayoutManager;
            if (layoutManager == null)
                return;
            var itemCount = _list.GetAdapter()?.ItemCount ?? 0;
            if (itemCount == 0)
                return;
            var first = layoutManager.FindFirstVisibleItemPosition();
            var firstView = _list.GetChildAt(0);
            ViewModelLocator.SearchPage.CatalogueScrollPosition = first;
            ViewModelLocator.SearchPage.CatalogueScrollOffset = firstView != null ? firstView.Top : 0;
            if (layoutManager.FindLastVisibleItemPosition() >= itemCount - 6)
                ViewModelLocator.SearchPage.LoadMoreCatalogue();
        }
    }

    class GenreListScrollListener : Java.Lang.Object, AbsListView.IOnScrollListener
    {
        private readonly bool _isGenreMode;

        public GenreListScrollListener(bool isGenreMode)
        {
            _isGenreMode = isGenreMode;
        }

        public void OnScroll(AbsListView view, int firstVisibleItem, int visibleItemCount, int totalItemCount)
        {
            if (ViewModelLocator.SearchPage.CatalogueIsGenre.HasValue)
                return;
            if (_isGenreMode)
                ViewModelLocator.SearchPage.GenreListScrollPosition = firstVisibleItem;
            else
                ViewModelLocator.SearchPage.StudioListScrollPosition = firstVisibleItem;
        }

        public void OnScrollStateChanged(AbsListView view, ScrollState scrollState)
        {
        }
    }
}

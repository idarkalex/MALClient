using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.View;
using Android.Views;
using Android.Widget;
using FFImageLoading;
using FFImageLoading.Views;
using MALClient.Android.PagerAdapters;
using MALClient.Android.Resources;

using MALClient.Models.Enums;
using MALClient.XShared.Comm;
using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;

namespace MALClient.Android.Fragments.SearchFragments
{
    public class SuggestionPoster { public string Title; public string ImgUrl; public int MalId; public string Type; }
    public class PosterDropAdapter : ArrayAdapter<SuggestionPoster>
    {
        private readonly global::Android.Views.LayoutInflater _inf;
        public PosterDropAdapter(global::Android.Content.Context ctx) : base(ctx, 0) { _inf = global::Android.Views.LayoutInflater.From(ctx); }
        public override global::Android.Views.View GetView(int pos, global::Android.Views.View cv, global::Android.Views.ViewGroup parent)
        {
            var v = cv ?? _inf.Inflate(Resource.Layout.SuggestionPosterItem, parent, false);
            var it = GetItem(pos);
            v.FindViewById<TextView>(Resource.Id.SuggestionPosterTitle).Text = it.Title;
            var img = v.FindViewById<FFImageLoading.Views.ImageViewAsync>(Resource.Id.SuggestionPosterImage);
            if ((string)img.Tag != it.ImgUrl) { img.Tag = it.ImgUrl; try { img.Into(it.ImgUrl, null, null, 100); } catch { } }
            return v;
        }
    }

    public class SearchPageFragment : MalFragmentBase
    {
        public static SearchPagePagerAdapter CurrentPagerAdapter { get; private set; }

        private readonly SearchPageNavigationArgs _args;
        private PosterDropAdapter _hintAdapter;
        private global::Android.Widget.AutoCompleteTextView _searchAutoComplete;
        private CancellationTokenSource _dropdownCts;
        private int _dropdownGen;
        private SearchPagePagerAdapter _pagerAdapter;

        private SearchPageFragment(SearchPageNavigationArgs args)
        {
            _args = args;

        }

        protected override void InitBindings()
        {
            _pagerAdapter = new SearchPagePagerAdapter(ChildFragmentManager, _args, out int start);
            CurrentPagerAdapter = _pagerAdapter;
            SearchPageViewPager.Adapter = _pagerAdapter;
            SearchPageTabStrip.IndicatorColor = Color.ParseColor("#0066FF");
            SearchPageTabStrip.IndicatorHeight = 3;
            SearchPageTabStrip.SetViewPager(SearchPageViewPager);
            SearchPageTabStrip.CenterTabs();
            SearchPageViewPager.OffscreenPageLimit = 5;

            // Force-create all 6 fragments so their InitBindings run before search starts
            // Tab order: 0=Everywhere, 1=Anime, 2=Manga, 3=Characters, 4=Genres, 5=Studios
            for (int i = 0; i < 6; i++)
            {
                SearchPageViewPager.SetCurrentItem(i, false);
            }
            // Restore the LAST page the user was on (e.g. Genres with an active catalogue) instead of
            // resetting to `start` (2=Manga for manga-mode sessions) on every view recreation.
            int restore = start;
            int last = ViewModelLocator.SearchPage.LastSearchPageIndex;
            if (last >= 0 && last <= 5)
                restore = last;
            SearchPageViewPager.SetCurrentItem(restore, false);
            // Re-apply the saved genre/studio filter text when re-binding a fresh Search page.
            try
            {
                if (last >= 0 && (restore == 4 || restore == 5))
                {
                    var savedFilter = restore == 4 ? ViewModelLocator.SearchPage.GenreFilterQuery : ViewModelLocator.SearchPage.StudioFilterQuery;
                    if (!string.IsNullOrWhiteSpace(savedFilter))
                    {
                        SearchPageSearchView.SetQuery(savedFilter, false);
                        if (restore == 4) _pagerAdapter.FilterCurrentGenreTab(savedFilter);
                        else _pagerAdapter.FilterCurrentStudioTab(savedFilter);
                    }
                }
            } catch { }
            HasOnlyManualBindings = true;
            try
            {
                SearchPageViewPager.AddOnPageChangeListener(new SearchPageChangeListener(SearchPageSearchView, pos => ViewModelLocator.SearchPage.LastSearchPageIndex = pos));
            } catch { }

            SearchPageSearchView.Iconified = false;
            try
            {
                var mag = SearchPageSearchView.FindViewById<ImageView>(Resource.Id.search_mag_icon);
                if (mag != null) mag.SetColorFilter(Color.White);
                var close = SearchPageSearchView.FindViewById<ImageView>(Resource.Id.search_close_btn);
                if (close != null) close.SetColorFilter(Color.White);
            } catch { }
            if (!string.IsNullOrEmpty(_args?.Query))
                SearchPageSearchView.SetQuery(_args.Query, false);
            _hintAdapter = new PosterDropAdapter(Activity);
            _searchAutoComplete = SearchPageSearchView.FindViewById<global::Android.Widget.AutoCompleteTextView>(Resource.Id.search_src_text);
            if (_searchAutoComplete != null)
            {
                _searchAutoComplete.Adapter = _hintAdapter;
                _searchAutoComplete.Threshold = 3;
                _searchAutoComplete.SetTextColor(Color.White);
                _searchAutoComplete.ItemClick += (s2, e2) =>
                {
                    var sel = _hintAdapter.GetItem(e2.Position);
                    SearchPageSearchView.SetQuery(sel.Title, false);
                    _searchAutoComplete.DismissDropDown();
                    _args.Query = sel.Title;
                    _args.ForceQuery = true;
                    ViewModelLocator.GeneralMain.CurrentSearchQuery = sel.Title;
                    _pagerAdapter.TriggerSearch(new SearchPageNavigationArgs { Query = sel.Title, ForceQuery = true, Anime = true });
                    SearchPageViewPager.SetCurrentItem(0, false);
                    SearchPageSearchView.ClearFocus();
                };
            }
            SearchPageSearchView.QueryTextChange += async (s, e) =>
            {
                var q = e.NewText?.Trim() ?? "";
                ViewModelLocator.GeneralMain.CurrentSearchQuery = e.NewText;
                var currentTab = SearchPageViewPager.CurrentItem;
                // Genres(4) y Studios(5) son pestañas independientes: filtrar grid local, no búsqueda global
                if (currentTab == 4)
                {
                    _pagerAdapter.FilterCurrentGenreTab(q);
                    _dropdownCts?.Cancel();
                    Activity?.RunOnUiThread(() => { _hintAdapter.Clear(); _hintAdapter.NotifyDataSetChanged(); _searchAutoComplete?.DismissDropDown(); });
                    return;
                }
                if (currentTab == 5)
                {
                    _pagerAdapter.FilterCurrentStudioTab(q);
                    _dropdownCts?.Cancel();
                    Activity?.RunOnUiThread(() => { _hintAdapter.Clear(); _hintAdapter.NotifyDataSetChanged(); _searchAutoComplete?.DismissDropDown(); });
                    return;
                }

                _dropdownCts?.Cancel();
                _dropdownCts = new CancellationTokenSource();
                var token = _dropdownCts.Token;
                var gen = ++_dropdownGen;
                if (string.IsNullOrEmpty(q) || q.Length < 3)
                {
                    Activity?.RunOnUiThread(() => { _hintAdapter.Clear(); _hintAdapter.NotifyDataSetChanged(); _searchAutoComplete?.DismissDropDown(); });
                    if (string.IsNullOrEmpty(q))
                    {
                        // X pressed - reset search for non-genre/studio tabs
                        if (currentTab != 4 && currentTab != 5)
                        {
                            try { _pagerAdapter.TriggerSearch(new SearchPageNavigationArgs { Query = "", ForceQuery = true }); } catch { }
                        }
                    }
                    return;
                }
                try { await Task.Delay(300, token); } catch { return; }
                if (token.IsCancellationRequested) return;
                try
                {
                    var clean = MALClient.XShared.Utils.Utilities.CleanAnimeTitle(q);
                    var res = await TenraiClient.GetPaginatedAsync($"anime?q={Uri.EscapeDataString(clean)}&sfw&order_by=popularity&sort=asc");
                    var items = res.Items;
                    if (gen != _dropdownGen || token.IsCancellationRequested) return;
                    var posters = items.Select(el =>
                    {
                        var title = el.TryGetProperty("title", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : "";
                        var id = el.TryGetProperty("mal_id", out var ip) && ip.ValueKind == JsonValueKind.Number ? ip.GetInt32() : 0;
                        var img = "";
                        if (el.TryGetProperty("images", out var imgs) && imgs.ValueKind == JsonValueKind.Object)
                            if (imgs.TryGetProperty("jpg", out var jpg) && jpg.ValueKind == JsonValueKind.Object)
                                if (jpg.TryGetProperty("image_url", out var url) && url.ValueKind == JsonValueKind.String) img = url.GetString();
                        var typ = el.TryGetProperty("type", out var tp) && tp.ValueKind == JsonValueKind.String ? tp.GetString()?.ToLower() : "anime";
                        return new SuggestionPoster { Title = title, ImgUrl = img, MalId = id, Type = typ };
                    }).Where(t => !string.IsNullOrEmpty(t.Title) && t.MalId > 0).GroupBy(t => t.MalId).Select(g => g.First()).Take(5).ToList();
                    Activity?.RunOnUiThread(() =>
                    {
                        if (gen != _dropdownGen) return;
                        _hintAdapter.Clear();
                        foreach (var t in posters) _hintAdapter.Add(t);
                        _hintAdapter.NotifyDataSetChanged();
                        if (posters.Count > 0) _searchAutoComplete?.ShowDropDown(); else _searchAutoComplete?.DismissDropDown();
                    });
                }
                catch { }
                // live search in vivo >2 (everywhere default)
                try
                {
                    if (q.Length >= 2)
                    {
                        _args.Query = q;
                        _args.ForceQuery = true;
                        _pagerAdapter.TriggerSearch(new SearchPageNavigationArgs { Query = q, ForceQuery = true, Anime = true });
                    }
                } catch { }
            };
            SearchPageSearchView.QueryTextSubmit += (s, e) =>
            {
                var q = SearchPageSearchView.Query?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(q) && q.Length >= 2)
                {
                    _args.Query = q;
                    _args.ForceQuery = true;
                    ViewModelLocator.GeneralMain.CurrentSearchQuery = q;
                    _pagerAdapter.TriggerSearch(new SearchPageNavigationArgs { Query = q, ForceQuery = true, Anime = true });
                    SearchPageViewPager.SetCurrentItem(0, false);
                }
                e.Handled = true;
                SearchPageSearchView.ClearFocus();
            };
            var se = SearchPageSearchView.FindViewById(Resource.Id.search_src_text) as global::Android.Widget.EditText;
            if (se != null) se.SetTextColor(Color.White);

            // Auto-focus search bar and show keyboard on entering search - post para que ViewPager termine layout
            SearchPageSearchView.PostDelayed(() =>
            {
                try
                {
                    SearchPageSearchView.Iconified = false;
                    var edit = SearchPageSearchView.FindViewById<global::Android.Widget.AutoCompleteTextView>(Resource.Id.search_src_text);
                    if (edit != null)
                    {
                        edit.Focusable = true;
                        edit.FocusableInTouchMode = true;
                        edit.Enabled = true;
                        edit.RequestFocus();
                        edit.SelectAll();
                        var imm = Activity?.GetSystemService(global::Android.Content.Context.InputMethodService) as global::Android.Views.InputMethods.InputMethodManager;
                        imm?.ShowSoftInput(edit, global::Android.Views.InputMethods.ShowFlags.Implicit);
                    }
                    else
                    {
                        SearchPageSearchView.RequestFocus();
                        var imm2 = Activity?.GetSystemService(global::Android.Content.Context.InputMethodService) as global::Android.Views.InputMethods.InputMethodManager;
                        imm2?.ShowSoftInput(SearchPageSearchView, global::Android.Views.InputMethods.ShowFlags.Implicit);
                    }
                } catch { }
            }, 300);

            // Manejar X de la barra global: limpiar búsqueda y filtro de genres/studios
            try
            {
                var closeBtn = SearchPageSearchView.FindViewById<ImageView>(Resource.Id.search_close_btn);
                if (closeBtn != null)
                    closeBtn.Click += (s, e) =>
                    {
                        try
                        {
                            // Clear search query
                            SearchPageSearchView.SetQuery("", false);
                            SearchPageSearchView.ClearFocus();
                            
                            // Clear genre/studio filters
                            if (SearchPageViewPager.CurrentItem == 4) 
                                _pagerAdapter.FilterCurrentGenreTab("");
                            if (SearchPageViewPager.CurrentItem == 5) 
                                _pagerAdapter.FilterCurrentStudioTab("");
                            
                            // Reset search query in ViewModel
                            ViewModelLocator.GeneralMain.CurrentSearchQuery = "";
                            _args.Query = "";
                            _args.ForceQuery = true;
                            
                            // Trigger search with empty query to reset results
                            _pagerAdapter.TriggerSearch(new SearchPageNavigationArgs { Query = "", ForceQuery = true });
                        } 
                        catch { }
                    };
            } 
            catch { }

            // Trigger initial search after fragments are created and their InitBindings run
            _pagerAdapter.TriggerSearch(_args);
            // Restore genre/studio filter after TriggerSearch's SetQuery("") clobbers it.
            SearchPageSearchView.Post(() =>
            {
                try
                {
                    if (SearchPageViewPager?.CurrentItem == 4)
                    {
                        var q = ViewModelLocator.SearchPage.GenreFilterQuery;
                        if (!string.IsNullOrWhiteSpace(q))
                        {
                            SearchPageSearchView.SetQuery(q, false);
                            _pagerAdapter?.FilterCurrentGenreTab(q);
                        }
                    }
                    else if (SearchPageViewPager?.CurrentItem == 5)
                    {
                        var q = ViewModelLocator.SearchPage.StudioFilterQuery;
                        if (!string.IsNullOrWhiteSpace(q))
                        {
                            SearchPageSearchView.SetQuery(q, false);
                            _pagerAdapter?.FilterCurrentStudioTab(q);
                        }
                    }
                } catch { }
            });
        }

        protected override void Init(Bundle savedInstanceState)
        {
        }


        #region Views

        private UserControls.PagerSlidingTabStrip _searchPageTabStrip;
        private ViewPager _searchPageViewPager;
        private global::Android.Support.V7.Widget.SearchView _searchPageSearchView;

        public UserControls.PagerSlidingTabStrip SearchPageTabStrip => GetView(ref _searchPageTabStrip, Resource.Id.SearchPageTabStrip);

        public ViewPager SearchPageViewPager => GetView(ref _searchPageViewPager, Resource.Id.SearchPageViewPager);

        public global::Android.Support.V7.Widget.SearchView SearchPageSearchView => GetView(ref _searchPageSearchView, Resource.Id.SearchPageSearchView);

        #endregion

        public static SearchPageFragment BuildInstance(SearchPageNavigationArgs args)
        {
            return new SearchPageFragment(args);
        }

        public override int LayoutResourceId => Resource.Layout.SearchPage;
        
    }

    class SearchPageChangeListener : ViewPager.SimpleOnPageChangeListener
    {
        private readonly global::Android.Support.V7.Widget.SearchView _searchView;
        private readonly Action<int> _pageChanged;
        public SearchPageChangeListener(global::Android.Support.V7.Widget.SearchView sv, Action<int> pageChanged) { _searchView = sv; _pageChanged = pageChanged; }
        public override void OnPageSelected(int position)
        {
            try
            {
                _pageChanged?.Invoke(position);
                if (position == 4 || position == 5)
                {
                    _searchView.SetQuery("", false);
                    _searchView.ClearFocus();
                }
                if (position == 4) _searchView.QueryHint = "Search genres...";
                else if (position == 5) _searchView.QueryHint = "Search studios...";
                else _searchView.QueryHint = "Search anime, manga, characters...";
            } catch { }
        }
    }
}
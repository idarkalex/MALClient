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

            SearchPageViewPager.SetCurrentItem(start, false);
            HasOnlyManualBindings = true;

            // Force-create all 6 fragments so their InitBindings run before search starts
            // Tab order: 0=Everywhere, 1=Anime, 2=Manga, 3=Characters, 4=Genres, 5=Studios
            for (int i = 0; i < 6; i++)
            {
                SearchPageViewPager.SetCurrentItem(i, false);
            }
            SearchPageViewPager.SetCurrentItem(start, false);

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
                _dropdownCts?.Cancel();
                _dropdownCts = new CancellationTokenSource();
                var token = _dropdownCts.Token;
                var gen = ++_dropdownGen;
                if (string.IsNullOrEmpty(q) || q.Length < 3)
                {
                    Activity?.RunOnUiThread(() => { _hintAdapter.Clear(); _hintAdapter.NotifyDataSetChanged(); _searchAutoComplete?.DismissDropDown(); });
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

            // Trigger initial search after fragments are created and their InitBindings run
            _pagerAdapter.TriggerSearch(_args);
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
}
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
using MALClient.Android.PagerAdapters;
using MALClient.Android.Resources;

using MALClient.Models.Enums;
using MALClient.XShared.Comm;
using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;

namespace MALClient.Android.Fragments.SearchFragments
{
    public class SearchPageFragment : MalFragmentBase
    {
        private readonly SearchPageNavigationArgs _args;
        private ArrayAdapter<string> _hintAdapter;
        private AutoCompleteTextView _searchAutoComplete;
        private CancellationTokenSource _dropdownCts;
        private int _dropdownGen;

        private SearchPageFragment(SearchPageNavigationArgs args)
        {
            _args = args;

        }

        protected override void InitBindings()
        {
            SearchPageViewPager.Adapter = new SearchPagePagerAdapter(ChildFragmentManager, _args, out int start);
            SearchPageTabStrip.SetViewPager(SearchPageViewPager);
            SearchPageTabStrip.CenterTabs();
            SearchPageViewPager.OffscreenPageLimit = 5;

            SearchPageViewPager.SetCurrentItem(start, false);
            HasOnlyManualBindings = true;

            SearchPageSearchView.Iconified = false;
            if (!string.IsNullOrEmpty(_args?.Query))
                SearchPageSearchView.SetQuery(_args.Query, false);
            _hintAdapter = new ArrayAdapter<string>(Activity, global::Android.Resource.Layout.SimpleDropDownItem1Line);
            _searchAutoComplete = SearchPageSearchView.FindViewById<AutoCompleteTextView>(Resource.Id.search_src_text);
            if (_searchAutoComplete != null)
            {
                _searchAutoComplete.Adapter = _hintAdapter;
                _searchAutoComplete.Threshold = 3;
                _searchAutoComplete.SetTextColor(Color.White);
                _searchAutoComplete.ItemClick += (s2, e2) =>
                {
                    var sel = _hintAdapter.GetItem(e2.Position);
                    SearchPageSearchView.SetQuery(sel, false);
                    _searchAutoComplete.DismissDropDown();
                    _args.Query = sel;
                    _args.ForceQuery = true;
                    ViewModelLocator.GeneralMain.CurrentSearchQuery = sel;
                    SearchPageViewPager.Adapter = new SearchPagePagerAdapter(ChildFragmentManager, _args, out int ns2);
                    SearchPageTabStrip.SetViewPager(SearchPageViewPager);
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
                    var res = await TenraiClient.GetPaginatedAsync($"anime?q={Uri.EscapeDataString(clean)}&sfw");
                    var items = res.Items;
                    if (gen != _dropdownGen || token.IsCancellationRequested) return;
                    var titles = items.Select(el => el.TryGetProperty("title", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : "").Where(t => !string.IsNullOrEmpty(t)).Distinct().Take(5).ToList();
                    Activity?.RunOnUiThread(() =>
                    {
                        if (gen != _dropdownGen) return;
                        _hintAdapter.Clear();
                        foreach (var t in titles) _hintAdapter.Add(t);
                        _hintAdapter.NotifyDataSetChanged();
                        if (titles.Count > 0) _searchAutoComplete?.ShowDropDown(); else _searchAutoComplete?.DismissDropDown();
                    });
                }
                catch { }
            };
            SearchPageSearchView.QueryTextSubmit += (s, e) =>
            {
                var q = SearchPageSearchView.Query?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(q) && q.Length >= 2)
                {
                    _args.Query = q;
                    _args.ForceQuery = true;
                    ViewModelLocator.GeneralMain.CurrentSearchQuery = q;
                    SearchPageViewPager.Adapter = new SearchPagePagerAdapter(ChildFragmentManager, _args, out int ns);
                    SearchPageTabStrip.SetViewPager(SearchPageViewPager);
                    SearchPageViewPager.SetCurrentItem(0, false);
                }
                e.Handled = true;
                SearchPageSearchView.ClearFocus();
            };
            var se = SearchPageSearchView.FindViewById(Resource.Id.search_src_text) as EditText;
            if (se != null) se.SetTextColor(Color.White);
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

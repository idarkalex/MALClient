using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;

namespace MALClient.Android.Fragments.SearchFragments
{
    public class SearchPageFragment : MalFragmentBase
    {
        private readonly SearchPageNavigationArgs _args;

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
            var hintAdapter = new ArrayAdapter<string>(Activity, global::Android.Resource.Layout.SimpleDropDownItem1Line);
            var autoComplete = SearchPageSearchView.FindViewById<AutoCompleteTextView>(Resource.Id.search_src_text);
            if (autoComplete != null)
            {
                autoComplete.Adapter = hintAdapter;
                autoComplete.Threshold = 1;
                autoComplete.SetTextColor(Color.White);
            }
            SearchPageSearchView.QueryTextChange += (s, e) =>
            {
                ViewModelLocator.GeneralMain.CurrentSearchQuery = e.NewText;
                hintAdapter.Clear();
                foreach (var h in ViewModelLocator.GeneralMain.CurrentHintSet ?? new System.Collections.Generic.List<string>())
                    hintAdapter.Add(h);
                hintAdapter.NotifyDataSetChanged();
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.View;
using Android.Views;
using Android.Widget;
using MALClient.Android.PagerAdapters;
using MALClient.Android.Resources;

using MALClient.Models.Enums;
using MALClient.XShared.NavArgs;
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
        }

        protected override void Init(Bundle savedInstanceState)
        {
        }


        #region Views

        private UserControls.PagerSlidingTabStrip _searchPageTabStrip;
        private ViewPager _searchPageViewPager;

        public UserControls.PagerSlidingTabStrip SearchPageTabStrip => GetView(ref _searchPageTabStrip, Resource.Id.SearchPageTabStrip);

        public ViewPager SearchPageViewPager => GetView(ref _searchPageViewPager, Resource.Id.SearchPageViewPager);

        #endregion

        public static SearchPageFragment BuildInstance(SearchPageNavigationArgs args)
        {
            return new SearchPageFragment(args);
        }

        public override int LayoutResourceId => Resource.Layout.SearchPage;
        
    }
}

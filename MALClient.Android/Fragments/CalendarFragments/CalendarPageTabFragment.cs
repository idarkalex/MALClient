using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using MALClient.Android.Activities;
using MALClient.Android.CollectionAdapters;
using MALClient.Android.Resources;
using MALClient.Models.Enums;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALClient.Android.Fragments.CalendarFragments
{
    public class CalendarPageTabFragment : MalFragmentBase
    {
        private readonly List<AnimeItemViewModel> _items;
        private readonly int _pageIndex;
        private GridViewColumnHelper _gridViewColumnHelper;

        public CalendarPageTabFragment(List<AnimeItemViewModel> items, int pageIndex)
        {
            _items = items;
            _pageIndex = pageIndex;
        }

        public override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            RetainInstance = true;
        }

        protected override void Init(Bundle savedInstanceState)
        {
            foreach (var animeItemViewModel in _items)
                animeItemViewModel.RefreshTimeTillNextAirInBackground();
        }

        protected override void InitBindings()
        {
            CalendarPageTabContentList.InjectAnimeListAdapter(Context, _items, AnimeListDisplayModes.IndefiniteGrid, OnItemClick);
            _gridViewColumnHelper = new GridViewColumnHelper(CalendarPageTabContentList, null, 2, 3);
        }

        private void OnItemClick(AnimeItemViewModel animeItemViewModel)
        {
            animeItemViewModel.NavigateDetails(PageIndex.PageCalendar);
        }

        public override void OnConfigurationChanged(Configuration newConfig)
        {
            _gridViewColumnHelper.OnConfigurationChanged(newConfig);
            base.OnConfigurationChanged(newConfig);
        }

        public override void OnPause()
        {
            base.OnPause();
            SaveScrollState();
        }

        private void SaveScrollState()
        {
            var viewModel = ViewModelLocator.CalendarPage;
            var pos = CalendarPageTabContentList.FirstVisiblePosition;
            var offset = 0;
            var child = CalendarPageTabContentList.GetChildAt(0);
            if (child != null)
                offset = -child.Top;
            viewModel.UiState["CalendarTab_" + _pageIndex] = (pos, offset);
        }

        public override void OnResume()
        {
            base.OnResume();
            RestoreScrollState();
        }

        private void RestoreScrollState()
        {
            var viewModel = ViewModelLocator.CalendarPage;
            if (viewModel.UiState.TryGetValue("CalendarTab_" + _pageIndex, out var stateObj) && stateObj is (int pos, int offset))
            {
                CalendarPageTabContentList.Post(() => CalendarPageTabContentList.SetSelectionFromTop(pos, offset));
            }
        }

        public override int LayoutResourceId => Resource.Layout.CalenarPageTabContent;

        #region Views

        private GridView _calendarPageTabContentList;

        public GridView CalendarPageTabContentList => GetView(ref _calendarPageTabContentList, Resource.Id.CalendarPageTabContentList);

        #endregion
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Android.Content;
using Android.Content.Res;
using Android.OS;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using MALClient.Android.Activities;
using MALClient.Android.UserControls;
using MALClient.Android.Utilities;
using MALClient.Android.UserControls.AnimeItems;
using MALClient.Models.Enums;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALClient.Android.Fragments.CalendarFragments
{
    public class CalendarPageSummaryTabFragment : MalFragmentBase
    {
        private const int ViewTypeHeader = 0;
        private const int ViewTypeCard = 1;

        private readonly List<Tuple<string, List<AnimeItemViewModel>>> _items;
        private readonly int _pageIndex;
        private readonly GridViewColumnHelper _gridViewColumnHelper = new GridViewColumnHelper((int?)null) { MinColumnsPortrait = 2, MinColumnsLandscape = 3 };
        private CalendarSummaryAdapter _adapter;
        private GridLayoutManager _layoutManager;
        private int _spanCount;

        public CalendarPageSummaryTabFragment(List<Tuple<string, List<AnimeItemViewModel>>> items, int pageIndex)
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
            foreach (var animeItemViewModel in _items.SelectMany(tuple => tuple.Item2))
                animeItemViewModel.RefreshTimeTillNextAirInBackground();
            _gridViewColumnHelper.OnConfigurationChanged(Resources.Configuration);
            _spanCount = _gridViewColumnHelper.LastColmuns;
        }

        protected override void InitBindings()
        {
            _layoutManager = new GridLayoutManager(Activity, _spanCount);
            CalendarPageSummaryTabList.SetLayoutManager(_layoutManager);

            _adapter = new CalendarSummaryAdapter(BuildFlatList(), OnItemClick);
            CalendarPageSummaryTabList.SetAdapter(_adapter);

            _layoutManager.SetSpanSizeLookup(new CalendarSummarySpanLookup(_adapter, _spanCount));

            RestoreScrollState();
        }

        private List<SummaryItem> BuildFlatList()
        {
            var flat = new List<SummaryItem>();
            foreach (var tuple in _items)
            {
                flat.Add(new SummaryHeaderItem { DayName = tuple.Item1 });
                foreach (var vm in tuple.Item2)
                    flat.Add(new SummaryCardItem { ViewModel = vm });
            }
            return flat;
        }

        private void OnItemClick(AnimeItemViewModel animeItemViewModel)
        {
            animeItemViewModel.NavigateDetails(PageIndex.PageCalendar);
        }

        public override void OnConfigurationChanged(Configuration newConfig)
        {
            _gridViewColumnHelper.OnConfigurationChanged(newConfig);
            var newSpan = _gridViewColumnHelper.LastColmuns;
            if (newSpan != _spanCount)
            {
                _spanCount = newSpan;
                _layoutManager.SpanCount = _spanCount;
                _layoutManager.SetSpanSizeLookup(new CalendarSummarySpanLookup(_adapter, _spanCount));
            }
            base.OnConfigurationChanged(newConfig);
        }

        public override void OnPause()
        {
            base.OnPause();
            SaveScrollState();
        }

        private void SaveScrollState()
        {
            if (_layoutManager == null) return;
            var viewModel = ViewModelLocator.CalendarPage;
            var pos = _layoutManager.FindFirstVisibleItemPosition();
            var child = _layoutManager.FindViewByPosition(pos);
            var offset = child != null ? child.Top : 0;
            viewModel.UiState["CalendarTab_" + _pageIndex] = (pos, offset);
        }

        private void RestoreScrollState()
        {
            if (_layoutManager == null) return;
            var viewModel = ViewModelLocator.CalendarPage;
            if (viewModel.UiState.TryGetValue("CalendarTab_" + _pageIndex, out var stateObj) && stateObj is (int pos, int offset))
            {
                CalendarPageSummaryTabList.Post(() => _layoutManager.ScrollToPositionWithOffset(pos, offset));
            }
        }

        public override int LayoutResourceId => Resource.Layout.CalendarPageSummaryTab;

        #region View

        private RecyclerView _calendarPageSummaryTabList;

        public RecyclerView CalendarPageSummaryTabList => GetView(ref _calendarPageSummaryTabList, Resource.Id.CalendarPageSummaryTabList);

        #endregion

        private abstract class SummaryItem
        {
        }

        private class SummaryHeaderItem : SummaryItem
        {
            public string DayName { get; set; }
        }

        private class SummaryCardItem : SummaryItem
        {
            public AnimeItemViewModel ViewModel { get; set; }
        }

        private class CalendarSummaryAdapter : RecyclerView.Adapter
        {
            private readonly List<SummaryItem> _items;
            private readonly Action<AnimeItemViewModel> _onItemClick;
            private readonly global::Android.Views.LayoutInflater _inflater;

            public CalendarSummaryAdapter(List<SummaryItem> items, Action<AnimeItemViewModel> onItemClick)
            {
                _items = items;
                _onItemClick = onItemClick;
                _inflater = MainActivity.CurrentContext.LayoutInflater;
            }

            public override int ItemCount => _items.Count;

            public override int GetItemViewType(int position)
                => _items[position] is SummaryHeaderItem ? ViewTypeHeader : ViewTypeCard;

            public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
            {
                if (viewType == ViewTypeHeader)
                {
                    var view = _inflater.Inflate(Resource.Layout.CalendarSummaryDayHeader, parent, false);
                    return new HeaderHolder(view);
                }
                return new CardHolder(new AnimeGridItem(parent.Context, _onItemClick));
            }

            public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
            {
                if (holder is HeaderHolder headerHolder)
                    headerHolder.Text.Text = ((SummaryHeaderItem) _items[position]).DayName;
                else if (holder is CardHolder cardHolder)
                    cardHolder.GridItem.BindModel(((SummaryCardItem) _items[position]).ViewModel, false);
            }
        }

        private class HeaderHolder : RecyclerView.ViewHolder
        {
            private readonly View _view;
            private TextView _text;

            public TextView Text => _text ?? (_text = _view.FindViewById<TextView>(Resource.Id.CalendarSummaryDayHeaderText));

            public HeaderHolder(View view) : base(view)
            {
                _view = view;
            }
        }

        private class CardHolder : RecyclerView.ViewHolder
        {
            public AnimeGridItem GridItem { get; }

            public CardHolder(AnimeGridItem itemView) : base(itemView)
            {
                GridItem = itemView;
            }
        }

        private class CalendarSummarySpanLookup : GridLayoutManager.SpanSizeLookup
        {
            private readonly CalendarSummaryAdapter _adapter;
            private readonly int _spanCount;

            public CalendarSummarySpanLookup(CalendarSummaryAdapter adapter, int spanCount)
            {
                _adapter = adapter;
                _spanCount = spanCount;
                SpanIndexCacheEnabled = true;
            }

            public override int GetSpanSize(int position)
                => _adapter.GetItemViewType(position) == ViewTypeHeader ? _spanCount : 1;
        }
    }
}

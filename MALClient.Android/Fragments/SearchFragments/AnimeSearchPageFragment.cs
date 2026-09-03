using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Android.OS;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using MALClient.Android.AoLibsCompat;
using GalaSoft.MvvmLight.Helpers;
using MALClient.Android.Activities;
using MALClient.Android.BindingConverters;
using MALClient.Android.Resources;
using MALClient.Android.UserControls;
using MALClient.Models.Enums;
using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALClient.Android.Fragments.SearchFragments
{
    public class AnimeSearchPageFragment : MalFragmentBase
    {
        private bool _waitForRootView;

        public bool IsManga { get; set; }

        public AnimeSearchPageFragment(bool initBindings = true) : base(initBindings)
        {

        }

        private SearchPageViewModel ViewModel;


        protected override void Init(Bundle savedInstanceState)
        {
            ViewModel = ViewModelLocator.SearchPage;
        }

        protected override void InitBindings()
        {
            if (_waitForRootView)
            {
                _waitForRootView = false;
                NavigatedTo();
            }

            SearchRecyclerView.SetAdapter(new SearchGridAdapter(
                IsManga ? ViewModel.MangaSearchItemViewModels : ViewModel.AnimeSearchItemViewModels, Activity));
            SearchRecyclerView.SetLayoutManager(new GridLayoutManager(Activity, 3));

            Bindings.Add(this.SetBinding(() => ViewModel.Loading).WhenSourceChanges(() =>
            {
                if (ViewModel.Loading)
                {
                    AnimeSearchPageLoadingSpinner.Visibility = ViewStates.Visible;
                }
                else
                {
                    AnimeSearchPageLoadingSpinner.Visibility = ViewStates.Gone;
                }
            }));

            Bindings.Add(this.SetBinding(() => ViewModel.EmptyNoticeVisibility).WhenSourceChanges(() =>
            {
                if (ViewModel.EmptyNoticeVisibility)
                {
                    AnimeSearchPageEmptyNotice.Visibility = ViewStates.Visible;
                }
                else
                {
                    AnimeSearchPageEmptyNotice.Visibility = ViewStates.Gone;
                }
            }));

            Bindings.Add(this.SetBinding(() => ViewModel.IsFirstVisitGridVisible).WhenSourceChanges(() =>
            {
                if (ViewModel.IsFirstVisitGridVisible)
                {
                    AnimeSearchPageFirstSearchSection.Visibility = ViewStates.Visible;
                }
                else
                {
                    AnimeSearchPageFirstSearchSection.Visibility = ViewStates.Gone;
                }
            }));
        }

        public override void DetachBindings()
        {

        }

        public void NavigatedTo()
        {
            if (RootView == null)
            {
                _waitForRootView = true;
                return;
            }
        }
        public override int LayoutResourceId => Resource.Layout.AnimeSearchPage;


        class SearchGridAdapter : RecyclerView.Adapter
        {
            private readonly System.Collections.Generic.IList<AnimeSearchItemViewModel> _items;
            private readonly global::Android.Content.Context _context;

            public SearchGridAdapter(System.Collections.Generic.IList<AnimeSearchItemViewModel> items, global::Android.Content.Context context)
            {
                _items = items;
                _context = context;
            }

            public override int ItemCount => _items.Count;

            public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
            {
                GridHolder gridHolder = null;
                var card = new AnimeGridItem(_context, vm => gridHolder?.SearchItem?.NavigateDetails());
                card.LayoutParameters = new RecyclerView.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent,
                    (int)_context.Resources.GetDimension(Resource.Dimension.GridCardHeight));
                gridHolder = new GridHolder(card);
                return gridHolder;
            }

            public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
            {
                var gridHolder = (GridHolder)holder;
                var item = _items[position];
                gridHolder.SearchItem = item;
                gridHolder.Card.BindModel(item.ToGridViewModel(), false);
            }
        }

        class GridHolder : RecyclerView.ViewHolder
        {
            public AnimeGridItem Card { get; }
            public AnimeSearchItemViewModel SearchItem { get; set; }

            public GridHolder(AnimeGridItem card) : base(card)
            {
                Card = card;
            }
        }


        #region Views

        private RecyclerView _searchRecyclerView;
        private TextView _animeSearchPageEmptyNotice;
        private LinearLayout _animeSearchPageFirstSearchSection;
        private ProgressBar _animeSearchPageLoadingSpinner;

        public RecyclerView SearchRecyclerView => GetView(ref _searchRecyclerView, Resource.Id.SearchRecyclerView);
        public TextView AnimeSearchPageEmptyNotice => GetView(ref _animeSearchPageEmptyNotice, Resource.Id.AnimeSearchPageEmptyNotice);
        public LinearLayout AnimeSearchPageFirstSearchSection => GetView(ref _animeSearchPageFirstSearchSection, Resource.Id.AnimeSearchPageFirstSearchSection);
        public ProgressBar AnimeSearchPageLoadingSpinner => GetView(ref _animeSearchPageLoadingSpinner, Resource.Id.AnimeSearchPageLoadingSpinner);

        #endregion
    }
}

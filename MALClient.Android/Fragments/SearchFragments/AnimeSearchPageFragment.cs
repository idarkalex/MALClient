using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using Android.OS;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using FFImageLoading.Views;
using GalaSoft.MvvmLight.Helpers;
using MALClient.Android.Activities;
using MALClient.Android.BindingConverters;
using MALClient.Android.Resources;
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
            private readonly IList<AnimeSearchItemViewModel> _items;
            private readonly global::Android.Content.Context _context;

            public SearchGridAdapter(IList<AnimeSearchItemViewModel> items, global::Android.Content.Context context)
            {
                _items = items;
                _context = context;
                if (items is INotifyCollectionChanged notifyCollection)
                {
                    notifyCollection.CollectionChanged += OnCollectionChanged;
                }
            }

            private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
            {
                NotifyDataSetChanged();
            }

            public override void OnAttachedToRecyclerView(RecyclerView recyclerView)
            {
                base.OnAttachedToRecyclerView(recyclerView);
                if (_items?.Count > 0)
                    NotifyDataSetChanged();
            }

            public override int ItemCount => _items?.Count ?? 0;

            public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
            {
                var view = LayoutInflater.From(_context).Inflate(Resource.Layout.SearchPosterItem, parent, false);
                return new PosterHolder(view);
            }

            public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
            {
                var posterHolder = (PosterHolder)holder;
                var item = _items[position];
                posterHolder.Bind(item);
            }
        }

        class PosterHolder : RecyclerView.ViewHolder
        {
            private readonly ImageViewAsync _posterImage;
            private readonly TextView _posterTitle;
            private readonly TextView _posterScore;
            private readonly TextView _posterType;
            private AnimeSearchItemViewModel _currentItem;

            public PosterHolder(View view) : base(view)
            {
                _posterImage = view.FindViewById<ImageViewAsync>(Resource.Id.SearchPosterImage);
                _posterTitle = view.FindViewById<TextView>(Resource.Id.SearchPosterTitle);
                _posterScore = view.FindViewById<TextView>(Resource.Id.SearchPosterScore);
                _posterType = view.FindViewById<TextView>(Resource.Id.SearchPosterType);
                ItemView.Click += (s, e) => _currentItem?.NavigateDetails();
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
                {
                    _posterScore.Visibility = ViewStates.Gone;
                }
                if (!string.IsNullOrWhiteSpace(item.Type))
                {
                    _posterType.Text = item.Type;
                    _posterType.Visibility = ViewStates.Visible;
                }
                else
                {
                    _posterType.Visibility = ViewStates.Gone;
                }
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

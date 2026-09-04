using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Android.Content;
using Android.Content.Res;
using Android.OS;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using FFImageLoading.Views;
using MALClient.Android.AoLibsCompat;
using GalaSoft.MvvmLight.Helpers;
using MALClient.Android.Activities;
using MALClient.Android.BindingConverters;
using MALClient.Android.Listeners;
using MALClient.Android.Resources;
using MALClient.Android.UserControls;
using MALClient.Models.Enums;
using MALClient.Models.Models.Search;
using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALClient.Android.Fragments.SearchFragments
{
    public class SearchEverywherePageFragment : MalFragmentBase
    {
        private static SearchPageNavArgsBase _prevArgs;

        private SearchEverywhereViewModel ViewModel;
        private CardsEverywhereAdapter _everywhereAdapter;

        private SearchEverywherePageFragment(bool initBindings) : base(initBindings)
        {
            
        }

        protected override void InitBindings()
        {
            _everywhereAdapter = new CardsEverywhereAdapter(this, ViewModel);
            var gridManager = new GridLayoutManager(Activity, 3);
            gridManager.SetSpanSizeLookup(new CardsSpanLookup(_everywhereAdapter));
            SearchRecyclerView.SetLayoutManager(gridManager);
            SearchRecyclerView.SetAdapter(_everywhereAdapter);
            SearchRecyclerView.HasFixedSize = false;
            SearchRecyclerView.SetClipToPadding(false);
            SearchRecyclerView.SetPadding(4, 8, 4, 8);

            // Initial refresh if data already populated
            if (ViewModel.SearchResults?.Count > 0)
            {
                _everywhereAdapter.NotifyDataSetChanged();
                SearchRecyclerView.RequestLayout();
            }

            Bindings.Add(this.SetBinding(() => ViewModel.Loading).WhenSourceChanges(() =>
            {
                Activity?.RunOnUiThread(() =>
                {
                    if (ViewModel.Loading)
                    {
                        LoadingSpinner.Visibility = ViewStates.Visible;
                    }
                    else
                    {
                        LoadingSpinner.Visibility = ViewStates.Gone;
                        SearchRecyclerView?.Post(() =>
                        {
                            _everywhereAdapter?.NotifyDataSetChanged();
                            SearchRecyclerView?.RequestLayout();
                        });
                    }
                });
            }));

            Bindings.Add(this.SetBinding(() => ViewModel.IsEmptyNoticeVisible).WhenSourceChanges(() =>
            {
                if (ViewModel.IsEmptyNoticeVisible)
                {
                    EmptyNotice.Visibility = ViewStates.Visible;
                }
                else
                {
                    EmptyNotice.Visibility = ViewStates.Gone;
                }
            }));

            Bindings.Add(this.SetBinding(() => ViewModel.IsFirstVisitGridVisible).WhenSourceChanges(() =>
            {
                if (ViewModel.IsFirstVisitGridVisible)
                {
                    FirstSearchSection.Visibility = ViewStates.Visible;
                }
                else
                {
                    FirstSearchSection.Visibility = ViewStates.Gone;
                }
            }));
        }

        protected override void Init(Bundle savedInstanceState)
        {
            ViewModel = ViewModelLocator.SearchEverywhereViewModel;
            ViewModel.Init(_prevArgs);
        }

        public override int LayoutResourceId => Resource.Layout.SearchEverywherePage;

        #region Views

        private RecyclerView _searchRecyclerView;
        private TextView _emptyNotice;
        private LinearLayout _firstSearchSection;
        private ProgressBar _loadingSpinner;

        public RecyclerView SearchRecyclerView => GetView(ref _searchRecyclerView, Resource.Id.SearchRecyclerView);
        public TextView EmptyNotice => GetView(ref _emptyNotice, Resource.Id.EmptyNotice);
        public LinearLayout FirstSearchSection => GetView(ref _firstSearchSection, Resource.Id.FirstSearchSection);
        public ProgressBar LoadingSpinner => GetView(ref _loadingSpinner, Resource.Id.LoadingSpinner);

        #endregion

        class CardsSpanLookup : GridLayoutManager.SpanSizeLookup
        {
            private readonly CardsEverywhereAdapter _adapter;
            public CardsSpanLookup(CardsEverywhereAdapter adapter) { _adapter = adapter; }
            public override int GetSpanSize(int position)
            {
                var type = _adapter.GetItemViewType(position);
                // Category (0) and Separator (1) span full width, cards span 1
                return type == 0 || type == 1 ? 3 : 1;
            }
        }

        class CategoryHolder : RecyclerView.ViewHolder
        {
            private readonly View _view;
            public CategoryHolder(View view) : base(view) { _view = view; }
            private TextView _category;
            public TextView Category => _category ?? (_category = _view.FindViewById<TextView>(Resource.Id.Category));
        }

        class SeparatorHolder : RecyclerView.ViewHolder
        {
            public SeparatorHolder(View view) : base(view) { }
        }

        class PosterHolder : RecyclerView.ViewHolder
        {
            private readonly ImageViewAsync _posterImage;
            private readonly TextView _posterTitle;
            private readonly TextView _posterScore;
            private readonly TextView _posterType;
            private ISearchEverywhereItem _currentItem;

            public PosterHolder(View view) : base(view)
            {
                _posterImage = view.FindViewById<ImageViewAsync>(Resource.Id.SearchPosterImage);
                _posterTitle = view.FindViewById<TextView>(Resource.Id.SearchPosterTitle);
                _posterScore = view.FindViewById<TextView>(Resource.Id.SearchPosterScore);
                _posterType = view.FindViewById<TextView>(Resource.Id.SearchPosterType);
                ItemView.Click += (s, e) =>
                {
                    var vm = ViewModelLocator.SearchEverywhereViewModel;
                    if (_currentItem is SearchEverywhereAnimeItem a) vm.NavigateAnimeDetails(a);
                    else if (_currentItem is SearchEverywhereMangaItem m) vm.NavigateMangaDetails(m);
                    else if (_currentItem is SearchEverywhereCharacterItem c) vm.NavigateCharacterDetails(c);
                    else if (_currentItem is SearchEverywherePersonItem p) vm.NavigatePersonDetails(p);
                    else if (_currentItem is SearchEverywhereUserItem u) vm.NavigateUserDetails(u);
                };
            }

            public void Bind(ISearchEverywhereItem item)
            {
                _currentItem = item;
                string title = "", img = "", type = "", score = "";
                if (item is SearchEverywhereGenericItem g)
                {
                    title = g.Item.Name;
                    img = g.Item.ImageUrl;
                    type = g.Item.Payload?.MediaType ?? "";
                    score = g.Item.Payload?.Score ?? "";
                    if (!string.IsNullOrWhiteSpace(type))
                    {
                        type = type.Trim().ToLower();
                        if (type == "novel" || type == "light_novel" || type == "light novel") type = "Novela";
                        else if (type == "oneshot" || type == "one-shot") type = "One-shot";
                        else type = char.ToUpper(type[0]) + type.Substring(1);
                    }
                    if (string.IsNullOrWhiteSpace(type) && item is SearchEverywhereCharacterItem) type = "Character";
                    else if (item is SearchEverywherePersonItem) type = "Person";
                    else if (item is SearchEverywhereUserItem) type = "User";
                    else if (item is SearchEverywhereMangaItem && string.IsNullOrWhiteSpace(type)) type = "Manga";
                    else if (item is SearchEverywhereAnimeItem && string.IsNullOrWhiteSpace(type)) type = "Anime";
                }
                _posterImage.AnimeInto(img);
                _posterTitle.Text = title;
                if (!string.IsNullOrWhiteSpace(type))
                {
                    _posterType.Text = type;
                    _posterType.Visibility = ViewStates.Visible;
                }
                else _posterType.Visibility = ViewStates.Gone;
                if (!string.IsNullOrWhiteSpace(score) && float.TryParse(score, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sc) && sc > 0)
                {
                    _posterScore.Text = sc.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                    _posterScore.Visibility = ViewStates.Visible;
                }
                else _posterScore.Visibility = ViewStates.Gone;
            }
        }

        public static SearchEverywherePageFragment BuildInstance(SearchPageNavArgsBase args,bool initBindings = false)
        {
            _prevArgs = args;
            return new SearchEverywherePageFragment(initBindings);
        }

        class CardsEverywhereAdapter : ObservableRecyclerAdapterWithMultipleViewTypes<ISearchEverywhereItem, RecyclerView.ViewHolder>
    {
        private readonly SearchEverywherePageFragment _fragment;
        private readonly SearchEverywhereViewModel _viewModel;

        public CardsEverywhereAdapter(SearchEverywherePageFragment fragment, SearchEverywhereViewModel viewModel) 
            : base(new Dictionary<Type, IItemEntry>
            {
                {
                    typeof(SearchCategoryItem),
                    new SpecializedItemEntry<SearchCategoryItem, CategoryHolder>
                    {
                        ItemTemplate = viewType => fragment.LayoutInflater.Inflate(Resource.Layout.SearchEverywhereCategoryItem, fragment.SearchRecyclerView, false),
                        SpecializedDataTemplate = (item, holder, position) => holder.Category.Text = item.Name
                    }
                },
                {
                    typeof(SearchEverywhereSeparator),
                    new SpecializedItemEntry<SearchEverywhereSeparator, SeparatorHolder>
                    {
                        ItemTemplate = viewType => fragment.LayoutInflater.Inflate(Resource.Layout.SearchEverywhereSeparatorItem, fragment.SearchRecyclerView, false),
                        SpecializedDataTemplate = (item, holder, position) => {}
                    }
                },
                {
                    typeof(SearchEverywhereAnimeItem),
                    new SpecializedItemEntry<SearchEverywhereAnimeItem, PosterHolder>
                    {
                        ItemTemplate = viewType => fragment.LayoutInflater.Inflate(Resource.Layout.SearchPosterItem, fragment.SearchRecyclerView, false),
                        SpecializedDataTemplate = (item, holder, position) => holder.Bind(item)
                    }
                },
                {
                    typeof(SearchEverywhereMangaItem),
                    new SpecializedItemEntry<SearchEverywhereMangaItem, PosterHolder>
                    {
                        ItemTemplate = viewType => fragment.LayoutInflater.Inflate(Resource.Layout.SearchPosterItem, fragment.SearchRecyclerView, false),
                        SpecializedDataTemplate = (item, holder, position) => holder.Bind(item)
                    }
                },
                {
                    typeof(SearchEverywhereCharacterItem),
                    new SpecializedItemEntry<SearchEverywhereCharacterItem, PosterHolder>
                    {
                        ItemTemplate = viewType => fragment.LayoutInflater.Inflate(Resource.Layout.SearchPosterItem, fragment.SearchRecyclerView, false),
                        SpecializedDataTemplate = (item, holder, position) => holder.Bind(item)
                    }
                },
                {
                    typeof(SearchEverywherePersonItem),
                    new SpecializedItemEntry<SearchEverywherePersonItem, PosterHolder>
                    {
                        ItemTemplate = viewType => fragment.LayoutInflater.Inflate(Resource.Layout.SearchPosterItem, fragment.SearchRecyclerView, false),
                        SpecializedDataTemplate = (item, holder, position) => holder.Bind(item)
                    }
                },
                {
                    typeof(SearchEverywhereUserItem),
                    new SpecializedItemEntry<SearchEverywhereUserItem, PosterHolder>
                    {
                        ItemTemplate = viewType => fragment.LayoutInflater.Inflate(Resource.Layout.SearchPosterItem, fragment.SearchRecyclerView, false),
                        SpecializedDataTemplate = (item, holder, position) => holder.Bind(item)
                    }
                },
            }, 
            viewModel.SearchResults)
        {
            _fragment = fragment;
            _viewModel = viewModel;
            StretchContentHorizonatally = false;
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var holder = base.OnCreateViewHolder(parent, viewType);
            // PosterHolder needs viewModel
            if (holder is PosterHolder ph)
            {
                // already constructed with viewModel via factory, but base factory uses Activator.CreateInstance
                // We need to ensure PosterHolder gets viewModel - override creation
            }
            return holder;
        }
        }
    }
}
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
        private FixedSizeEverywhereAdapter _everywhereAdapter;

        private SearchEverywherePageFragment(bool initBindings) : base(initBindings)
        {
            
        }

        protected override void InitBindings()
        {
            _everywhereAdapter = new FixedSizeEverywhereAdapter(this, ViewModel);
            _everywhereAdapter.StretchContentHorizonatally = true;
            SearchRecyclerView.SetAdapter(_everywhereAdapter);
            SearchRecyclerView.SetLayoutManager(new LinearLayoutManager(Activity));
            SearchRecyclerView.HasFixedSize = true;

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

        class SearchItemHolder : RecyclerView.ViewHolder
        {
            private readonly View _view;

            public SearchItemHolder(View view) : base(view)
            {
                _view = view;
            }
            private ImageView _image;
            private TextView _title;
            private TextView _subtitle;
            private TextView _rightMarker;
            private LinearLayout _clickSurface;

            public ImageView Image => _image ?? (_image = _view.FindViewById<ImageView>(Resource.Id.Image));
            public TextView Title => _title ?? (_title = _view.FindViewById<TextView>(Resource.Id.Title));
            public TextView Subtitle => _subtitle ?? (_subtitle = _view.FindViewById<TextView>(Resource.Id.Subtitle));
            public TextView RightMarker => _rightMarker ?? (_rightMarker = _view.FindViewById<TextView>(Resource.Id.RightMarker));
            public LinearLayout ClickSurface => _clickSurface ?? (_clickSurface = _view.FindViewById<LinearLayout>(Resource.Id.ClickSurface));
        }


        class CategoryHolder : RecyclerView.ViewHolder
        {
            private readonly View _view;

            public CategoryHolder(View view) : base(view)
            {
                _view = view;
            }
            private TextView _category;

            public TextView Category => _category ?? (_category = _view.FindViewById<TextView>(Resource.Id.Category));
        }


        public static SearchEverywherePageFragment BuildInstance(SearchPageNavArgsBase args,bool initBindings = false)
        {
            _prevArgs = args;
            return new SearchEverywherePageFragment(initBindings);
        }

    // Custom adapter with fixed item size and proper view recycling
    class FixedSizeEverywhereAdapter : ObservableRecyclerAdapterWithMultipleViewTypes<ISearchEverywhereItem, RecyclerView.ViewHolder>
    {
        private readonly SearchEverywherePageFragment _fragment;
        private readonly SearchEverywhereViewModel _viewModel;

        public FixedSizeEverywhereAdapter(SearchEverywherePageFragment fragment, SearchEverywhereViewModel viewModel) 
            : base(new Dictionary<Type, ObservableRecyclerAdapterWithMultipleViewTypes<ISearchEverywhereItem, RecyclerView.ViewHolder>.IItemEntry>
            {
                {
                    typeof(SearchCategoryItem),
                    new SpecializedItemEntry<SearchCategoryItem, CategoryHolder>
                    {
                        ItemTemplate = viewType => fragment.LayoutInflater.Inflate(Resource.Layout.SearchEverywhereCategoryItem, null),
                        SpecializedDataTemplate = (item, holder, position) => 
                        {
                            holder.Category.Text = item.Name;
                        }
                    }
                },
                {
                    typeof(SearchEverywhereAnimeItem),
                    new SpecializedItemEntry<SearchEverywhereAnimeItem, SearchItemHolder>
                    {
                        ItemTemplate = viewType => fragment.LayoutInflater.Inflate(Resource.Layout.SearchEverywhereItem, null),
                        SpecializedDataTemplate = (item, holder, position) => 
                        {
                            var subtitleBuilder = new StringBuilder();

                            if (!string.IsNullOrEmpty(item.Item.Payload.Aired))
                                subtitleBuilder.AppendLine($"Aired: {item.Item.Payload.Aired}");

                            if (!string.IsNullOrEmpty(item.Item.Payload.Score))
                                subtitleBuilder.AppendLine($"Score: {item.Item.Payload.Score}");

                            if (!string.IsNullOrEmpty(item.Item.Payload.Status))
                                subtitleBuilder.AppendLine($"Status: {item.Item.Payload.Status}");

                            holder.Image.Into(item.Item.ImageUrl);
                            holder.Title.Text = item.Item.Name;
                            holder.Subtitle.Text = subtitleBuilder.ToString();
                            holder.RightMarker.Text = item.Item.Payload.MediaType;
                            holder.ClickSurface.SetOnClickListener(new OnClickListener(view => { viewModel.NavigateAnimeDetails(item); }));
                            
                            // Force fixed height to prevent cutting
                            ForceFixedHeight(holder.ClickSurface, fragment.Activity);
                        }
                    }
                },
                {
                    typeof(SearchEverywhereMangaItem),
                    new SpecializedItemEntry<SearchEverywhereMangaItem, SearchItemHolder>
                    {
                        ItemTemplate = viewType => fragment.LayoutInflater.Inflate(Resource.Layout.SearchEverywhereItem, null),
                        SpecializedDataTemplate = (item, holder, position) => 
                        {
                            var subtitleBuilder = new StringBuilder();

                            if (!string.IsNullOrEmpty(item.Item.Payload.Aired))
                                subtitleBuilder.AppendLine($"Published: {item.Item.Payload.Published}");

                            if (!string.IsNullOrEmpty(item.Item.Payload.Score))
                                subtitleBuilder.AppendLine($"Score: {item.Item.Payload.Score}");

                            if (!string.IsNullOrEmpty(item.Item.Payload.Status))
                                subtitleBuilder.AppendLine($"Status: {item.Item.Payload.Status}");

                            holder.Image.Into(item.Item.ImageUrl);
                            holder.Title.Text = item.Item.Name;
                            holder.Subtitle.Text = subtitleBuilder.ToString();
                            holder.RightMarker.Text = item.Item.Payload.MediaType;
                            holder.ClickSurface.SetOnClickListener(new OnClickListener(view => { viewModel.NavigateMangaDetails(item); }));
                            
                            // Force fixed height
                            ForceFixedHeight(holder.ClickSurface, fragment.Activity);
                        }
                    }
                },
                {
                    typeof(SearchEverywhereCharacterItem),
                    new SpecializedItemEntry<SearchEverywhereCharacterItem, SearchItemHolder>
                    {
                        ItemTemplate = viewType => fragment.LayoutInflater.Inflate(Resource.Layout.SearchEverywhereItem, null),
                        SpecializedDataTemplate = (item, holder, position) => 
                        {
                            var subtitleBuilder = new StringBuilder();

                            if (item.Item.Payload.RelatedWorks != null)
                            {
                                foreach (var related in item.Item.Payload.RelatedWorks.Take(2))
                                {
                                    subtitleBuilder.AppendLine(related);
                                }
                            }

                            subtitleBuilder.Append("Favs: ").Append(item.Item.Payload.Favorites).AppendLine();

                            holder.Image.Into(item.Item.ImageUrl);
                            holder.Title.Text = item.Item.Name;
                            holder.Subtitle.Text = subtitleBuilder.ToString();
                            holder.RightMarker.Text = string.Empty;
                            holder.ClickSurface.SetOnClickListener(new OnClickListener(view => viewModel.NavigateCharacterDetails(item)));
                            
                            // Force fixed height
                            ForceFixedHeight(holder.ClickSurface, fragment.Activity);
                        }
                    }
                },
                {
                    typeof(SearchEverywherePersonItem),
                    new SpecializedItemEntry<SearchEverywherePersonItem, SearchItemHolder>
                    {
                        ItemTemplate = viewType => fragment.LayoutInflater.Inflate(Resource.Layout.SearchEverywhereItem, null),
                        SpecializedDataTemplate = (item, holder, position) => 
                        {
                            var subtitleBuilder = new StringBuilder();

                            if (!string.IsNullOrEmpty(item.Item.Payload.Birthday))
                                subtitleBuilder.AppendLine($"Birthday: {item.Item.Payload.Birthday}");

                            subtitleBuilder.Append("Favs: ").Append(item.Item.Payload.Favorites).AppendLine();

                            holder.Image.Into(item.Item.ImageUrl);
                            holder.Title.Text = item.Item.Name;
                            holder.Subtitle.Text = subtitleBuilder.ToString();
                            holder.RightMarker.Text = string.Empty;
                            holder.ClickSurface.SetOnClickListener(new OnClickListener(view => viewModel.NavigatePersonDetails(item)));
                            
                            // Force fixed height
                            ForceFixedHeight(holder.ClickSurface, fragment.Activity);
                        }
                    }
                },
                {
                    typeof(SearchEverywhereUserItem),
                    new SpecializedItemEntry<SearchEverywhereUserItem, SearchItemHolder>
                    {
                        ItemTemplate = viewType => fragment.LayoutInflater.Inflate(Resource.Layout.SearchEverywhereItem, null),
                        SpecializedDataTemplate = (item, holder, position) => 
                        {
                            holder.Image.Into(item.Item.ImageUrl);
                            holder.Title.Text = item.Item.Name;
                            holder.Subtitle.Text = string.Empty;
                            holder.RightMarker.Text = string.Empty;
                            holder.ClickSurface.SetOnClickListener(new OnClickListener(view => viewModel.NavigateUserDetails(item)));
                            
                            // Force fixed height
                            ForceFixedHeight(holder.ClickSurface, fragment.Activity);
                        }
                    }
                },
            }, 
            viewModel.SearchResults)
        {
            StretchContentHorizonatally = true;
        }

        private static void ForceFixedHeight(LinearLayout clickSurface, global::Android.Content.Context context)
        {
            var lp = clickSurface.LayoutParameters;
            if (lp != null)
            {
                lp.Height = (int)(150 * global::Android.Util.TypedValue.ApplyDimension(global::Android.Util.ComplexUnitType.Dip, 1, context.Resources.DisplayMetrics));
                clickSurface.LayoutParameters = lp;
            }
        }

        public override void OnViewRecycled(Java.Lang.Object holder)
        {
            base.OnViewRecycled(holder);
            if (holder is SearchItemHolder searchHolder && _fragment.Activity != null)
            {
                ForceFixedHeight(searchHolder.ClickSurface, _fragment.Activity);
            }
        }
    }
}
}
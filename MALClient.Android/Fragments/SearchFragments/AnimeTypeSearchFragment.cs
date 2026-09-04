using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using GalaSoft.MvvmLight.Helpers;
using MALClient.Android.Activities;
using MALClient.Android.Resources;
using MALClient.Models.Enums;
using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALClient.Android.Fragments.SearchFragments
{
    public class AnimeTypeSearchFragment : MalFragmentBase
    {
        private readonly bool _isGenreMode;
        private global::Android.Support.V7.Widget.SearchView _searchView;
        private List<Enum> _allChoices;
        private List<Enum> _filteredChoices;
        private Action<Enum> _onItemClick;

        public AnimeTypeSearchFragment(bool isGenreMode) : base(false)
        {
            _isGenreMode = isGenreMode;
        }

        protected override void Init(Bundle savedInstanceState)
        {
            _allChoices = _isGenreMode
                ? Enum.GetValues(typeof(AnimeGenreSearch)).Cast<Enum>().OrderBy(val => val.ToString()).ToList()
                : Enum.GetValues(typeof(AnimeStudios)).Cast<Enum>().OrderBy(val => val.ToString()).ToList();
            _filteredChoices = new List<Enum>(_allChoices);

            _onItemClick = _isGenreMode ? OnGenreClick : OnStudioClick;
        }

        protected override void InitBindings()
        {
            // Setup SearchView
            _searchView = RootView.FindViewById<global::Android.Support.V7.Widget.SearchView>(Resource.Id.AnimeTypeSearchView);
            if (_searchView != null)
            {
                _searchView.Iconified = false;
                _searchView.QueryHint = _isGenreMode ? "Search genres..." : "Search studios...";
                
                try
                {
                    var mag = _searchView.FindViewById<ImageView>(Resource.Id.search_mag_icon);
                    if (mag != null) mag.SetColorFilter(Color.White);
                    var close = _searchView.FindViewById<ImageView>(Resource.Id.search_close_btn);
                    if (close != null) 
                    {
                        close.SetColorFilter(Color.White);
                        close.Click += (s, e) => ClearFilter();
                    }
                } catch { }

                _searchView.QueryTextChange += (s, e) => FilterChoices(e.NewText);
                _searchView.QueryTextSubmit += (s, e) => { e.Handled = true; _searchView.ClearFocus(); };
            }

            RefreshAdapter();
        }

        private void FilterChoices(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                _filteredChoices = new List<Enum>(_allChoices);
            }
            else
            {
                var q = query.ToLower();
                _filteredChoices = _allChoices.Where(c => c.GetDescription().ToLower().Contains(q)).ToList();
            }
            RefreshAdapter();
        }

        private void ClearFilter()
        {
            if (_searchView != null)
            {
                _searchView.SetQuery("", false);
                _searchView.ClearFocus();
            }
            _filteredChoices = new List<Enum>(_allChoices);
            RefreshAdapter();
        }

        public void RefreshAdapter()
        {
            AnimeTypeSearchPageList.Adapter = _filteredChoices.GetAdapter(GetTemplateDelegate);
        }

        private View GetTemplateDelegate(int i, Enum parameter, View convertView)
        {
            var view = convertView;
            if (view == null)
            {
                view = MainActivity.CurrentContext.LayoutInflater.Inflate(Resource.Layout.AnimeSearchTypeItem, null);
                view.Click += ViewOnClick;
            }

            view.FindViewById<TextView>(Resource.Id.AnimeSearchTypeItemTextView).Text = parameter.GetDescription();
            view.Tag = parameter.Wrap();

            return view;
        }

        private void ViewOnClick(object sender, EventArgs eventArgs)
        {
            var item = (sender as View).Tag.Unwrap<Enum>();
            _onItemClick(item);
        }

        private void OnGenreClick(Enum genre)
        {
            // Apply local filter to the grid - just show this genre
            // In a real implementation, this would filter the results in the current tab
            // For now, navigate to anime list with genre filter
            ViewModelLocator.GeneralMain.Navigate(PageIndex.PageAnimeList, new AnimeListPageNavigationArgs((AnimeGenreSearch)genre));
        }

        private void OnStudioClick(Enum studio)
        {
            // Navigate to Anime tab (index 1) with studio filter
            var pagerAdapter = SearchPageFragment.CurrentPagerAdapter;
            if (pagerAdapter != null)
            {
                pagerAdapter.TriggerSearchWithStudio((AnimeStudios)studio);
            }
        }

        public override int LayoutResourceId => Resource.Layout.AnimeTypeSearchPage;

        #region Views

        private GridView _animeTypeSearchPageList;

        public GridView AnimeTypeSearchPageList => GetView(ref _animeTypeSearchPageList, Resource.Id.AnimeTypeSearchPageList);

        #endregion
    }
}

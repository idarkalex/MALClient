using System;
using System.Collections.Generic;
using System.Linq;

using Android.OS;
using Android.Views;
using Android.Widget;
using MALClient.Android.Activities;
using MALClient.Android.Resources;
using MALClient.Models.Enums;
using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;

namespace MALClient.Android.Fragments.SearchFragments
{
    public class AnimeTypeSearchFragment : MalFragmentBase
    {
        private readonly bool _isGenreMode;
        private List<Enum> _allChoices;
        private List<Enum> _filteredChoices;

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
        }

        protected override void InitBindings()
        {
            RefreshAdapter();
        }

        public void FilterChoices(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                _filteredChoices = new List<Enum>(_allChoices);
            else
            {
                var q = query.ToLower();
                _filteredChoices = _allChoices.Where(c => c.GetDescription().ToLower().Contains(q)).ToList();
            }
            RefreshAdapter();
        }

        public void ClearFilter()
        {
            _filteredChoices = new List<Enum>(_allChoices);
            RefreshAdapter();
        }

        public void RefreshAdapter()
        {
            if (AnimeTypeSearchPageList == null) return;
            if (_allChoices == null)
                _allChoices = _isGenreMode ? Enum.GetValues(typeof(AnimeGenreSearch)).Cast<Enum>().OrderBy(val => val.ToString()).ToList() : Enum.GetValues(typeof(AnimeStudios)).Cast<Enum>().OrderBy(val => val.ToString()).ToList();
            if (_filteredChoices == null) _filteredChoices = new List<Enum>(_allChoices);
            AnimeTypeSearchPageList.Adapter = _filteredChoices.GetAdapter(GetTemplateDelegate, null, true);
        }

        private View GetTemplateDelegate(int i, Enum parameter, View convertView)
        {
            var view = convertView;
            if (view == null)
            {
                var ctx = Activity ?? MainActivity.CurrentContext;
                var inflater = ctx != null ? LayoutInflater.From(ctx) : MainActivity.CurrentContext?.LayoutInflater;
                view = inflater?.Inflate(Resource.Layout.AnimeSearchTypeItem, null);
                if (view == null) return new TextView(ctx) { Text = parameter.GetDescription() };
                view.Click += ViewOnClick;
            }
            var tv = view.FindViewById<TextView>(Resource.Id.AnimeSearchTypeItemTextView);
            if (tv != null) tv.Text = parameter.GetDescription();
            view.Tag = parameter.Wrap();
            return view;
        }

        private void ViewOnClick(object sender, EventArgs eventArgs)
        {
            var item = (sender as View).Tag.Unwrap<Enum>();
            if (_isGenreMode)
                OnGenreClick(item);
            else
                OnStudioClick(item);
        }

        private void OnGenreClick(Enum genre)
        {
            var g = (AnimeGenreSearch)genre;
            ViewModelLocator.GeneralMain.CurrentSearchQuery = g.GetDescription();
            var pager = SearchPageFragment.CurrentPagerAdapter;
            if (pager != null)
                pager.TriggerSearchWithGenre(g);
            else
                ViewModelLocator.GeneralMain.Navigate(PageIndex.PageAnimeList, new AnimeListPageNavigationArgs(g));
        }

        private void OnStudioClick(Enum studio)
        {
            var s = (AnimeStudios)studio;
            ViewModelLocator.GeneralMain.CurrentSearchQuery = s.GetDescription();
            var pager = SearchPageFragment.CurrentPagerAdapter;
            if (pager != null)
                pager.TriggerSearchWithStudio(s);
            else
                ViewModelLocator.GeneralMain.Navigate(PageIndex.PageAnimeList, new AnimeListPageNavigationArgs(s));
        }

        public override int LayoutResourceId => Resource.Layout.AnimeTypeSearchPage;

        #region Views

        private GridView _animeTypeSearchPageList;
        public GridView AnimeTypeSearchPageList => GetView(ref _animeTypeSearchPageList, Resource.Id.AnimeTypeSearchPageList);

        #endregion
    }
}

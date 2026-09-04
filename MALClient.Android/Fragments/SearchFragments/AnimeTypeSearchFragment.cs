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
            if (_allChoices == null)
                _allChoices = _isGenreMode ? Enum.GetValues(typeof(AnimeGenreSearch)).Cast<Enum>().OrderBy(val => val.ToString()).ToList() : Enum.GetValues(typeof(AnimeStudios)).Cast<Enum>().OrderBy(val => val.ToString()).ToList();
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
            var ctx = Activity ?? MainActivity.CurrentContext ?? global::Android.App.Application.Context;
            var footer = new View(ctx);
            footer.Visibility = ViewStates.Gone;
            AnimeTypeSearchPageList.Adapter = _filteredChoices.GetAdapter(GetTemplateDelegate, footer, true);
        }

        private View GetTemplateDelegate(int i, Enum parameter, View convertView)
        {
            try
            {
                var view = convertView;
                if (view == null)
                {
                    var ctx = Activity ?? MainActivity.CurrentContext ?? global::Android.App.Application.Context;
                    View inflated = null;
                    try
                    {
                        var inflater = ctx != null ? LayoutInflater.From(ctx) : null;
                        inflated = inflater?.Inflate(Resource.Layout.AnimeSearchTypeItem, null);
                    } catch { }
                    view = inflated;
                    if (view == null)
                    {
                        var fallbackCtx = Activity ?? MainActivity.CurrentContext ?? global::Android.App.Application.Context;
                        var tvFallback = new TextView(fallbackCtx);
                        tvFallback.SetPadding(20, 20, 20, 20);
                        tvFallback.Text = parameter?.GetDescription() ?? "";
                        tvFallback.Tag = parameter?.Wrap();
                        return tvFallback;
                    }
                    view.Click += ViewOnClick;
                }
                // Horizontal cards: studios 2 filas (80dp), genres 1 fila (56dp) centradas
                try
                {
                    int targetDp = _isGenreMode ? 56 : 80;
                    var lp = view.LayoutParameters;
                    if (lp != null)
                    {
                        lp.Height = (int)global::Android.Util.TypedValue.ApplyDimension(global::Android.Util.ComplexUnitType.Dip, targetDp, view.Context.Resources.DisplayMetrics);
                        view.LayoutParameters = lp;
                    }
                    var tvInner = view.FindViewById<TextView>(Resource.Id.AnimeSearchTypeItemTextView);
                    if (tvInner != null)
                    {
                        tvInner.SetMaxLines(_isGenreMode ? 1 : 2);
                        tvInner.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End;
                    }
                } catch { }
                var tv = view.FindViewById<TextView>(Resource.Id.AnimeSearchTypeItemTextView);
                if (tv != null) tv.Text = parameter?.GetDescription() ?? "";
                else
                {
                    // fallback TextView case
                    if (view is TextView tv2) tv2.Text = parameter?.GetDescription() ?? "";
                }
                view.Tag = parameter?.Wrap();
                return view;
            }
            catch
            {
                try
                {
                    var fallbackCtx = Activity ?? MainActivity.CurrentContext ?? global::Android.App.Application.Context;
                    var tvFallback = new TextView(fallbackCtx);
                    tvFallback.SetPadding(20, 20, 20, 20);
                    tvFallback.Text = parameter?.GetDescription() ?? "item";
                    return tvFallback;
                }
                catch { return new TextView(global::Android.App.Application.Context) { Text = "item" }; }
            }
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

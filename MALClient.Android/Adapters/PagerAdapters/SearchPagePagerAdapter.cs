using System;
using System.Threading.Tasks;
using Android.Graphics;
using Android.Runtime;
using Android.Support.V4.App;
using Android.Views;
using Android.Widget;
using MALClient.Android.Fragments;
using MALClient.Android.Fragments.SearchFragments;
using MALClient.Android.Resources;

using MALClient.Models.Enums;
using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using PagerSlidingTab;

namespace MALClient.Android.PagerAdapters
{
    public class SearchPagePagerAdapter : FragmentPagerAdapter, ICustomTabProvider
    {

        public SearchPagePagerAdapter(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        public SearchPagePagerAdapter(FragmentManager fm, SearchPageNavArgsBase args, out int startPage) : base(fm)
        {
            int targetPage = 0;
            var arg = args as SearchPageNavigationArgs;
            if (arg != null)
            {
                if (string.IsNullOrWhiteSpace(arg.Query))
                    targetPage = 0;
                else if (arg.Anime)
                {
                    _animeSearchPageFragment = new AnimeSearchPageFragment(true) { IsManga = false };
                    _mangaSearchPageFragment = new AnimeSearchPageFragment(true) { IsManga = true };
                    targetPage = 1;
                }
                else
                {
                    _animeSearchPageFragment = new AnimeSearchPageFragment(true) { IsManga = false };
                    _mangaSearchPageFragment = new AnimeSearchPageFragment(true) { IsManga = true };
                    targetPage = 2;
                }

                if (arg.Everywhere)
                    targetPage = 0;

                _characterSearchPageFragment = CharacterSearchPageFragment.BuildInstance(new SearchPageNavArgsBase());
            }
            else
            {
                _animeSearchPageFragment = new AnimeSearchPageFragment(true);
                _mangaSearchPageFragment = new AnimeSearchPageFragment(true);

                ViewModelLocator.CharacterSearch.Init(args);
                _everywhereSearchPageFragment = SearchEverywherePageFragment.BuildInstance(new SearchPageNavArgsBase(), true);
                _characterSearchPageFragment = CharacterSearchPageFragment.BuildInstance(new SearchPageNavArgsBase(),true);
                targetPage = 0;
            }

            if (_everywhereSearchPageFragment == null)
                _everywhereSearchPageFragment = SearchEverywherePageFragment.BuildInstance(new SearchPageNavArgsBase(), true);
            _genresSearchPageFragment = new AnimeTypeSearchFragment(true);
            _studiosSearchPageFragment = new AnimeTypeSearchFragment(false);

            startPage = targetPage;
        }

        public void TriggerSearch(SearchPageNavigationArgs args)
        {
            // Priority 1: Anime/Manga search (highest priority)
            try { ViewModelLocator.SearchPage?.Init(args); } catch { }

            // Priority 2: Everywhere search (runs in parallel)
            try { ViewModelLocator.SearchEverywhereViewModel?.Init(new SearchPageNavigationArgs { Query = args.Query, ForceQuery = true }); } catch { }

            // Priority 3: Characters search (lower priority, small delay to not compete with API)
            Task.Run(async () =>
            {
                await Task.Delay(200);
                try { ViewModelLocator.CharacterSearch?.Init(new SearchPageNavArgsBase()); } catch { }
            });
        }

        public void TriggerSearchWithStudio(AnimeStudios studio)
        {
            var args = new SearchPageNavigationArgs 
            { 
                ByStudio = true, 
                Studio = studio,
                ForceQuery = true 
            };
            ViewModelLocator.GeneralMain.CurrentSearchQuery = studio.GetDescription();
            TriggerSearch(args);
            var activity = MALClient.Android.Activities.MainActivity.CurrentContext;
            activity?.RunOnUiThread(() =>
            {
                try 
                {
                    var viewPager = activity.FindViewById<global::Android.Support.V4.View.ViewPager>(Resource.Id.SearchPageViewPager);
                    viewPager?.SetCurrentItem(1, true);
                } 
                catch { }
            });
        }

        public void TriggerSearchWithGenre(AnimeGenreSearch genre)
        {
            var args = new SearchPageNavigationArgs 
            { 
                ByGenre = true, 
                Genre = genre,
                ForceQuery = true 
            };
            ViewModelLocator.GeneralMain.CurrentSearchQuery = genre.GetDescription();
            TriggerSearch(args);
            var activity = MALClient.Android.Activities.MainActivity.CurrentContext;
            activity?.RunOnUiThread(() =>
            {
                try 
                {
                    var viewPager = activity.FindViewById<global::Android.Support.V4.View.ViewPager>(Resource.Id.SearchPageViewPager);
                    viewPager?.SetCurrentItem(1, true);
                } 
                catch { }
            });
        }

        public void FilterCurrentGenreTab(string query)
        {
            try 
            { 
                _genresSearchPageFragment?.FilterChoices(query); 
            } 
            catch (Exception ex) 
            {
                System.Diagnostics.Debug.WriteLine($"FilterCurrentGenreTab error: {ex.Message}");
            }
        }

        public void FilterCurrentStudioTab(string query)
        {
            try 
            { 
                _studiosSearchPageFragment?.FilterChoices(query); 
            } 
            catch (Exception ex) 
            {
                System.Diagnostics.Debug.WriteLine($"FilterCurrentStudioTab error: {ex.Message}");
            }
        }

        public override int Count => 6;

        private MalFragmentBase _currentFragment;

        private readonly SearchEverywherePageFragment _everywhereSearchPageFragment;
        private readonly AnimeSearchPageFragment _animeSearchPageFragment;
        private readonly AnimeSearchPageFragment _mangaSearchPageFragment;
        private readonly CharacterSearchPageFragment _characterSearchPageFragment;
        private readonly AnimeTypeSearchFragment _studiosSearchPageFragment;
        private readonly AnimeTypeSearchFragment _genresSearchPageFragment;


        public void TabSelected(View p0)
        {
            try
            {
                var txt = p0 as TextView;
                if (txt != null) txt.Alpha = 1f;
                _currentFragment?.DetachBindings();
                var q = ViewModelLocator.GeneralMain?.CurrentSearchQuery ?? "";
                switch ((int)p0.Tag)
                {
                    case 0:
                        _currentFragment = _everywhereSearchPageFragment;
                        ShowSearchStuff();
                        try { ViewModelLocator.SearchEverywhereViewModel?.Init(new SearchPageNavigationArgs { Query = q, ForceQuery = true }); } catch { }
                        break;
                    case 1:
                        try { _animeSearchPageFragment?.NavigatedTo(); } catch { }
                        _currentFragment = _animeSearchPageFragment;
                        ShowSearchStuff();
                        try { ViewModelLocator.SearchPage?.Init(new SearchPageNavigationArgs { Query = q, ForceQuery = true }); } catch { }
                        break;
                    case 2:
                        try { _mangaSearchPageFragment?.NavigatedTo(); } catch { }
                        _currentFragment = _mangaSearchPageFragment;
                        ShowSearchStuff();
                        try { ViewModelLocator.SearchPage?.Init(new SearchPageNavigationArgs {Anime = false , Query = q, ForceQuery = true}); } catch { }
                        break;
                    case 3:
                        _currentFragment = _characterSearchPageFragment;
                        ShowSearchStuff();
                        try { ViewModelLocator.CharacterSearch?.Init(new SearchPageNavArgsBase()); } catch { }
                        break;
                case 4:
                    _currentFragment = _genresSearchPageFragment;
                    ViewModelLocator.GeneralMain.SearchToggleLock = false;
                    ViewModelLocator.GeneralMain.HideSearchStuff();
                    ViewModelLocator.GeneralMain.CurrentStatus = "Anime by Genre";
                    ViewModelLocator.SearchPage.Init(new SearchPageNavigationArgs { ByGenre = true});
                    _currentFragment?.ReattachBindings();
                    _genresSearchPageFragment.RefreshAdapter();
                    break;
                case 5:
                    ViewModelLocator.GeneralMain.HideSearchStuff();
                    ViewModelLocator.GeneralMain.SearchToggleLock = false;
                    ViewModelLocator.GeneralMain.CurrentStatus = "Anime by Studio";
                    _currentFragment = _studiosSearchPageFragment;
                    try { ViewModelLocator.SearchPage.Init(new SearchPageNavigationArgs { ByStudio = true}); } catch { }
                    _currentFragment?.ReattachBindings();
                    _studiosSearchPageFragment.RefreshAdapter();
                    break;
            }
            } catch { }
        }

        private void ShowSearchStuff()
        {
            if(ViewModelLocator.GeneralMain.SearchToggleLock)
                return;
            ViewModelLocator.GeneralMain.SearchToggleLock = true;
            ViewModelLocator.GeneralMain.ShowSearchStuff();
            ViewModelLocator.GeneralMain.ToggleSearchStuff();
        }

        public override Fragment GetItem(int p1)
        {
            switch (p1)
            {
                case 0:
                    return _everywhereSearchPageFragment ?? SearchEverywherePageFragment.BuildInstance(new SearchPageNavArgsBase(), true);
                case 1:
                    return _animeSearchPageFragment ?? new AnimeSearchPageFragment(true) { IsManga = false };
                case 2:
                    return  _mangaSearchPageFragment ?? new AnimeSearchPageFragment(true) { IsManga = true };
                case 3:
                    return _characterSearchPageFragment ?? CharacterSearchPageFragment.BuildInstance(new SearchPageNavArgsBase(), true);
                case 4:
                    return _genresSearchPageFragment;
                case 5:
                    return _studiosSearchPageFragment;
            }
            throw new ArgumentException();
        }

        public void TabUnselected(View p0)
        {
            var txt = p0 as TextView;
            txt.Alpha = .7f;
        }

        public View GetCustomTabView(ViewGroup p0, int p1)
        {
            var txt = new TextView(p0.Context);
            txt.SetTextColor(new Color(ResourceExtension.BrushText));
            txt.Tag = p1;
            switch (p1)
            {
                case 0:
                    txt.Text = "Everywhere";
                    break;
                case 1:
                    txt.Text = "Anime";
                    break;
                case 2:
                    txt.Text = "Manga";
                    break;
                case 3:
                    txt.Text = "Characters";
                    break;
                case 4:
                    txt.Text = "Genres";
                    break;
                case 5:
                    txt.Text = "Studios";
                    break;
            }

            return txt;
        }
    }
}
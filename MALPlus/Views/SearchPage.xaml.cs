using System.Collections.ObjectModel;
using MALClient.Models.Enums;
using MALClient.Models.Models.Favourites;
using MALClient.XShared.Comm.Anime;
using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Items;
using MALClient.XShared.ViewModels.Main;

namespace MALPlus.Views;

[QueryProperty(nameof(InitialQuery), "query")]
[QueryProperty(nameof(InitialMode), "mode")]
public partial class SearchPage : ContentPage
{
    private bool _initialized;
    private int _searchTabIndex;
    private bool _catalogueModeIsGenre = true;

    public string InitialQuery { get; set; }
    public string InitialMode { get; set; }

    private SearchPageViewModel MainVm => (SearchPageViewModel)BindingContext;
    private CharacterSearchViewModel CharVm => ViewModelLocator.CharacterSearch;

    public int SearchTabIndex
    {
        get => _searchTabIndex;
        set
        {
            _searchTabIndex = value;
            OnPropertyChanged();
        }
    }

    private string _charQuery;
    public string CharQuery
    {
        get => _charQuery;
        set { _charQuery = value; OnPropertyChanged(); }
    }

    // Forwarded property for XAML binding (CharacterSearch VM lives separately)
    public System.Collections.IEnumerable CharacterResults => CharVm.FoundCharacters;

    public SearchPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.SearchPage;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized)
            return;
        _initialized = true;
        try
        {
            // If a query was passed in, switch to Main tab and search.
            if (!string.IsNullOrWhiteSpace(InitialQuery))
            {
                var anime = string.IsNullOrEmpty(InitialMode) || InitialMode == "anime";
                SearchTabIndex = 0;
                MainVm.Init(new SearchPageNavigationArgs { Anime = anime, Query = InitialQuery, ForceQuery = true });
            }
            else
            {
                // First visit: show recent searches
                MainVm.Init(new SearchPageNavigationArgs { Anime = true, Query = "" });
            }
            // Load recent searches
            MainVm.LoadRecentSearches();
            // Init the character search VM too (it subscribes to the global search query)
            CharVm.Init(new SearchPageNavArgsBase());
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS SearchPage Init failed: " + ex);
        }
    }

    private void OnSearchTabTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is string s && int.TryParse(s, out int idx))
        {
            SearchTabIndex = idx;
            if (idx == 2)
            {
                _catalogueModeIsGenre = true;
                MainVm.Init(new SearchPageNavigationArgs { ByGenre = true, Anime = true, IsCatalogue = true });
            }
            else if (idx == 3)
            {
                _catalogueModeIsGenre = false;
                MainVm.Init(new SearchPageNavigationArgs { ByStudio = true, Anime = true, IsCatalogue = true });
            }
        }
    }

    private void OnSearchCompleted(object sender, EventArgs e)
    {
        try { MainVm.SearchCommand.Execute(null); } catch { }
    }

    private void OnRecentSearchSelected(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is string q)
            {
                ((CollectionView)sender).SelectedItem = null;
                if (!string.IsNullOrWhiteSpace(q))
                {
                    SearchEntry.Text = q;
                    MainVm.SearchCommand.Execute(null);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS recent search failed: " + ex.Message);
        }
    }

    private async void OnResultSelected(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is AnimeSearchItemViewModel item)
            {
                ((CollectionView)sender).SelectedItem = null;
                await Shell.Current.GoToAsync($"animedetails?id={item.Id}&title={Uri.EscapeDataString(item.Title ?? string.Empty)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS SearchPage result nav failed: " + ex.Message);
        }
    }

    private async void OnGenreStudioSelected(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is Enum choice)
            {
                ((CollectionView)sender).SelectedItem = null;
                var isGenre = SearchTabIndex == 2 || (SearchTabIndex == 0 && _catalogueModeIsGenre);
                var args = new SearchPageNavigationArgs
                {
                    Anime = true,
                    IsCatalogue = true,
                    CatalogueTitle = choice.ToString(),
                    ByGenre = isGenre,
                    ByStudio = !isGenre
                };
                if (isGenre)
                    args.Genre = (AnimeGenreSearch)choice;
                else
                    args.Studio = (AnimeStudios)choice;
                await MainVm.LoadCatalogue(args);
                // Switch to Main tab (index 0) to show results
                SearchTabIndex = 0;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS SearchPage genre/studio failed: " + ex.Message);
        }
    }

    private void OnCharacterSearchCompleted(object sender, EventArgs e) => DoCharacterSearch();
    private void OnCharacterSearchClicked(object sender, EventArgs e) => DoCharacterSearch();

    private void DoCharacterSearch()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(CharQuery)) return;
            CharVm.SearchCharacters(CharQuery.Trim());
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS Character search failed: " + ex.Message);
        }
    }

    private async void OnCharacterSelected(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is FavouriteViewModel item)
            {
                ((CollectionView)sender).SelectedItem = null;
                if (int.TryParse(item.Data.Id, out int id))
                    await Shell.Current.GoToAsync($"character?id={id}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS SearchPage character nav failed: " + ex.Message);
        }
    }
}

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

    public string CharQuery { get; set; }

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
                // Switch to genre catalogue
                MainVm.Init(new SearchPageNavigationArgs { ByGenre = true, Anime = true, IsCatalogue = true });
            }
            else if (idx == 3)
            {
                MainVm.Init(new SearchPageNavigationArgs { ByStudio = true, Anime = true, IsCatalogue = true });
            }
        }
    }

    private void OnSearchCompleted(object sender, EventArgs e)
    {
        try { MainVm.SearchCommand.Execute(null); } catch { }
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

    private void OnGenreStudioSelected(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is Enum choice)
            {
                ((CollectionView)sender).SelectedItem = null;
                MainVm.SubmitFilterCommand.Execute(choice);
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
            // The CharacterSearchViewModel listens to GeneralMain.OnSearchQuerySubmitted.
            // OnSearchInputSubmit sets CurrentSearchQuery + fires the event.
            ViewModelLocator.GeneralMain.CurrentSearchQuery = CharQuery;
            ViewModelLocator.GeneralMain.OnSearchInputSubmit();
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

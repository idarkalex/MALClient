using MALClient.Models.Enums;
using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALPlus.Views;

[QueryProperty(nameof(Query), "query")]
[QueryProperty(nameof(Mode), "mode")]
public partial class SearchPage : ContentPage
{
    private bool _initialized;

    public string Query { get; set; }
    public string Mode { get; set; }

    private SearchPageViewModel Vm => (SearchPageViewModel)BindingContext;

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

        bool anime = Mode != "manga";
        var args = new SearchPageNavigationArgs
        {
            Query = Query ?? string.Empty,
            Anime = anime,
            ForceQuery = !string.IsNullOrWhiteSpace(Query),
            DisplayMode = SearchPageDisplayModes.Main
        };
        Vm.Init(args);
    }

    private void OnSearchCompleted(object sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(Vm.InternalQuery))
        {
            var args = new SearchPageNavigationArgs
            {
                Query = Vm.InternalQuery,
                Anime = Vm._animeSearch,
                ForceQuery = true,
                DisplayMode = SearchPageDisplayModes.Main
            };
            Vm.Init(args);
        }
    }

    private void OnGenreStudioSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Enum selected)
        {
            var args = new SearchPageNavigationArgs
            {
                ByGenre = Vm.CatalogueIsGenre == true,
                ByStudio = Vm.CatalogueIsGenre == false,
                CatalogueTitle = selected.ToString(),
                Genre = Vm.CatalogueIsGenre == true ? (AnimeGenreSearch)selected : null,
                Studio = Vm.CatalogueIsGenre == false ? (AnimeStudios)selected : null
            };
            Vm.LoadCatalogue(args);
        }
    }

    private async void OnResultSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is AnimeSearchItemViewModel item)
        {
            await Shell.Current.GoToAsync($"animedetails?id={item.Id}&title={Uri.EscapeDataString(item.Title ?? string.Empty)}");
        }
    }
}
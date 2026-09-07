using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALPlus.Views;

public partial class RecommendationsPage : ContentPage
{
    private bool _initialized;
    private RecommendationsViewModel Vm => (RecommendationsViewModel)BindingContext;

    public RecommendationsPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.Recommendations;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;
        try
        {
            Vm.CurrentWorkMode = RecommendationsPageWorkMode.Anime;
            await WaitForLoad();
            ItemsList.ItemsSource = Vm.RecommendationAnimeItems;
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS RecommendationsPage Init failed: " + ex.Message);
        }
    }

    private async System.Threading.Tasks.Task WaitForLoad()
    {
        for (int i = 0; i < 60; i++)
        {
            await System.Threading.Tasks.Task.Delay(200);
            if (!Vm.Loading) break;
        }
    }

    private void OnModeClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Button b && b.CommandParameter is string s)
            {
                if (s == "Anime")
                {
                    Vm.CurrentWorkMode = RecommendationsPageWorkMode.Anime;
                    AnimeTab.BackgroundColor = Color.FromArgb("#0066FF");
                    AnimeTab.TextColor = Colors.White;
                    MangaTab.BackgroundColor = Colors.Transparent;
                    MangaTab.TextColor = Color.FromArgb("#FFFFFF");
                    ItemsList.ItemsSource = Vm.RecommendationAnimeItems;
                }
                else
                {
                    Vm.CurrentWorkMode = RecommendationsPageWorkMode.Manga;
                    MangaTab.BackgroundColor = Color.FromArgb("#0066FF");
                    MangaTab.TextColor = Colors.White;
                    AnimeTab.BackgroundColor = Colors.Transparent;
                    AnimeTab.TextColor = Color.FromArgb("#FFFFFF");
                    ItemsList.ItemsSource = Vm.RecommendationMangaItems;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS rec mode switch failed: " + ex.Message);
        }
    }

    private void OnRefreshing(object sender, EventArgs e)
    {
        try
        {
            if (Vm.CurrentWorkMode == RecommendationsPageWorkMode.Anime)
            {
                Vm.RecommendationAnimeItems = null;
                Vm.CurrentWorkMode = RecommendationsPageWorkMode.Anime;
            }
            else
            {
                Vm.RecommendationMangaItems = null;
                Vm.CurrentWorkMode = RecommendationsPageWorkMode.Manga;
            }
        }
        catch { }
    }

    private async void OnOpenClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Button b && b.CommandParameter is RecommendationsViewModel.XPivotItem item)
            {
                // Open the dependent title (first one in the pair)
                var rec = item.Content as RecommendationItemViewModel;
                if (rec != null)
                {
                    await Shell.Current.GoToAsync($"animedetails?id={rec.Data.DependentId}&title={System.Uri.EscapeDataString(rec.Data.DependentTitle ?? "")}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS rec open failed: " + ex.Message);
        }
    }
}

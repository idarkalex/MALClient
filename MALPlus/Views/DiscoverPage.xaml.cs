using System.Collections.ObjectModel;
using MALClient.Models.Models.AnimeScrapped;
using MALClient.XShared.Comm.Anime;
using MALClient.XShared.ViewModels;
using MALClient.Models.Models.Anime;

namespace MALPlus.Views;

public partial class DiscoverPage : ContentPage
{
    private bool _initialized;
    public ObservableCollection<SeasonalAnimeData> Seasonal { get; } = new();
    public ObservableCollection<SeasonalAnimeData> TopAnime { get; } = new();
    public ObservableCollection<SeasonalAnimeData> TopManga { get; } = new();
    public ObservableCollection<SeasonalAnimeData> Adapted { get; } = new();

    public DiscoverPage()
    {
        InitializeComponent();
        SeasonalRow.ItemsSource = Seasonal;
        TopAnimeRow.ItemsSource = TopAnime;
        TopMangaRow.ItemsSource = TopManga;
        AdaptedRow.ItemsSource = Adapted;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;
        await LoadAllAsync();
    }

    private async Task LoadAllAsync()
    {
        try
        {
            // Build a current-season sentinel: "Airing" works in AnimeSeasonalQuery.
            var currentSeason = new AnimeSeason
            {
                Name = "Airing",
                Year = DateTime.UtcNow.Year,
                IsCurrentSeason = true
            };
            var seasonalTask = SafeLoad<SeasonalAnimeData>(
                async () => await new AnimeSeasonalQuery(currentSeason).GetSeasonalAnime(true),
                Seasonal);
            var topAnimeTask = SafeLoad<TopAnimeData>(
                async () => await new AnimeTopQuery(TopAnimeType.General).GetTopAnimeData(),
                TopAnime);
            var topMangaTask = SafeLoad<TopAnimeData>(
                async () => await new AnimeTopQuery(MangaTopType.All).GetTopAnimeData(),
                TopManga);
            var adaptedTask = SafeLoad<TopAnimeData>(
                async () => await new AnimeAdaptedToAnimeQuery(MangaAdaptedType.AiringNow).GetAdaptedToAnimeData(true),
                Adapted);
            await Task.WhenAll(seasonalTask, topAnimeTask, topMangaTask, adaptedTask);
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS Discover load failed: " + ex.Message);
        }
    }

    private async Task SafeLoad<T>(Func<Task<List<T>>> loader,
        ObservableCollection<SeasonalAnimeData> target) where T : SeasonalAnimeData
    {
        try
        {
            var data = await loader();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                target.Clear();
                foreach (var item in data.Take(20))
                    target.Add(item);
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"MALPLUS Discover section failed: {ex.Message}");
        }
    }

    private void OnRefreshing(object sender, EventArgs e)
    {
        try
        {
            Refresh.IsRefreshing = false;
            _ = LoadAllAsync();
        }
        catch { }
    }

    private async void OnItemTapped(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is SeasonalAnimeData item)
            {
                ((CollectionView)sender).SelectedItem = null;
                // TopManga + Adapted rows route to manga details; the rest to anime.
                var isManga = sender is CollectionView cv &&
                              ((cv as VisualElement)?.AutomationId == "TopMangaRow" ||
                               (cv as VisualElement)?.AutomationId == "AdaptedRow");
                var qs = isManga ? "&manga=true" : "";
                await Shell.Current.GoToAsync(
                    $"animedetails?id={item.Id}&title={Uri.EscapeDataString(item.Title ?? string.Empty)}{qs}");
            }
        }
        catch { }
    }
}

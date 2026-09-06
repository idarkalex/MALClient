using System.Net.Http;
using MALClient.XShared.Comm.Anime;
using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALPlus.Views;

public partial class AnimeListPage : ContentPage
{
    private bool _initialized;
    private string _probe = "probe=...";

    private AnimeListViewModel Vm => (AnimeListViewModel)BindingContext;

    public AnimeListPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.AnimeList;
        ListRefresh.Refreshing += OnRefreshing;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Console.WriteLine("MALPLUS AnimeListPage OnAppearing");
        _ = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    await Task.Delay(2000);
                    var vm = Vm;
                    if (vm == null)
                        break;
                    var text = "mode=" + vm.WorkMode + " items=" + vm.AnimeItems.Count
                        + " grid=" + vm.AnimeGridItems.Count + " loading=" + vm.Loading
                        + " empty=" + vm.EmptyNoticeVisibility + " more=" + vm.CanLoadMore
                        + " src=" + vm.ListSource
                        + " " + _probe;
                    Console.WriteLine("MALPLUS " + text);
                    MainThread.BeginInvokeOnMainThread(() => DebugLabel.Text = text);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("MALPLUS poller failed: " + ex.Message);
            }
        });
        _ = Task.Run(async () =>
        {
            try
            {
                using var c = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                var r = await c.GetAsync("https://api.tenrai.org/v1/top/anime");
                var body = await r.Content.ReadAsStringAsync();
                _probe = "http=" + (int)r.StatusCode + " len=" + body.Length;
            }
            catch (Exception ex)
            {
                _probe = "http-FAIL " + ex.GetType().Name;
            }
            try
            {
                var q = await new AnimeTopQuery(TopAnimeType.General, 0).GetTopAnimeData(true);
                _probe += " q=" + (q == null ? "null" : q.Count.ToString());
            }
            catch (Exception ex)
            {
                _probe += " q-FAIL " + ex.GetType().Name;
            }
            Console.WriteLine("MALPLUS probe " + _probe);
        });
        if (_initialized)
            return;
        _initialized = true;
        Console.WriteLine("MALPLUS AnimeListPage Init TopAnime start");
        try
        {
            await Vm.Init(AnimeListPageNavigationArgs.TopAnime(TopAnimeType.General));
            Console.WriteLine("MALPLUS AnimeListPage Init done items=" + Vm.AnimeItems.Count + " grid=" + Vm.AnimeGridItems.Count);
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS AnimeListPage Init failed: " + ex);
        }
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        if (Vm.Loading)
            return;
        try
        {
            await Vm.Init(AnimeListPageNavigationArgs.TopAnime(TopAnimeType.General));
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS refresh failed: " + ex.Message);
        }
    }

    private async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is AnimeItemViewModel item)
            {
                AnimeGrid.SelectedItem = null;
                await Shell.Current.GoToAsync($"animedetails?id={item.Id}&title={Uri.EscapeDataString(item.Title ?? string.Empty)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS selection nav failed: " + ex.Message);
        }
    }
}

using System.Net.Http;
using MALClient.Models.Enums;
using MALClient.XShared.Comm.Anime;
using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALPlus.Views;

public partial class AnimeListPage : ContentPage
{
    private bool _polling;
    private bool _authedAtLoad;
    private string _userAtLoad;
    private string _probe = "probe=...";

    private AnimeListViewModel Vm => (AnimeListViewModel)BindingContext;

    public AnimeListPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.AnimeList;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Console.WriteLine("MALPLUS AnimeListPage OnAppearing");
        if (!_polling)
        {
            _polling = true;
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
                        + " auth=" + Credentials.Authenticated + " user=" + Credentials.UserName
                        + " tok=" + (!string.IsNullOrEmpty(Settings.ApiToken) ? "Y" : "n")
                        + " refr=" + (!string.IsNullOrEmpty(Settings.RefreshToken) ? "Y" : "n")
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
        }
        var authed = Credentials.Authenticated;
        var user = Credentials.UserName;
        if (_authedAtLoad == authed && _userAtLoad == user && Vm.AnimeItems.Count > 0)
            return;
        _authedAtLoad = authed;
        _userAtLoad = user;
        Console.WriteLine("MALPLUS AnimeListPage Init authed=" + authed + " user=" + user);
        try
        {
            if (authed && !string.IsNullOrWhiteSpace(user))
            {
                var args = new AnimeListPageNavigationArgs(0, AnimeListWorkModes.Anime) { ListSource = user };
                await Vm.Init(args);
            }
            else
            {
                await Vm.Init(AnimeListPageNavigationArgs.TopAnime(TopAnimeType.General));
            }
            Console.WriteLine("MALPLUS AnimeListPage Init done items=" + Vm.AnimeItems.Count + " grid=" + Vm.AnimeGridItems.Count);
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS AnimeListPage Init failed: " + ex);
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

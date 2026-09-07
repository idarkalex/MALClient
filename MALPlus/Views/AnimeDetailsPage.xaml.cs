using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Details;

namespace MALPlus.Views;

[QueryProperty(nameof(MalId), "id")]
[QueryProperty(nameof(AnimeTitle), "title")]
public partial class AnimeDetailsPage : ContentPage
{
    private bool _initialized;
    private bool _episodesLoaded;
    private bool _charactersLoaded;
    private bool _staffLoaded;
    private bool _recommendationsLoaded;
    private bool _relatedLoaded;

    public string MalId { get; set; }
    public string AnimeTitle { get; set; }

    private AnimeDetailsPageViewModel Vm => (AnimeDetailsPageViewModel)BindingContext;

    public AnimeDetailsPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.AnimeDetails;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized)
            return;
        _initialized = true;
        Console.WriteLine("MALPLUS Details OnAppearing id=" + MalId);
        _ = Task.Run(async () =>
        {
            try
            {
                using var c = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                var r = await c.GetAsync("https://api.tenrai.org/v1/anime/" + MalId + "/full");
                var body = await r.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine("MALPLUS Details fullhttp=" + (int)r.StatusCode + " len=" + body.Length
                    + " hasrank=" + body.Contains("\"rank\""));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("MALPLUS Details fullhttp-FAIL " + ex.GetType().Name);
            }
        });
        try
        {
            Vm.Init(new AnimeDetailsPageNavigationArgs(int.Parse(MalId), AnimeTitle, null, null, null), fakeDelay: false);
            await Task.Delay(12000);
            System.Diagnostics.Debug.WriteLine("MALPLUS Details Init returned title=" + Vm.Title + " synlen=" + (Vm.Synopsis ?? "").Length
                + " rank=" + Vm.GeneralRank + " pop=" + Vm.GeneralPopularity + " studios=" + Vm.GeneralStudios);
            Console.WriteLine("MALPLUS Details Init returned title=" + Vm.Title);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("MALPLUS Details Init failed: " + ex);
        }
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (BindingContext is AnimeDetailsPageViewModel vm)
        {
            vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private void OnVmPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AnimeDetailsPageViewModel.DetailsPivotSelectedIndex))
        {
            LoadTabData(Vm.DetailsPivotSelectedIndex);
        }
    }

    private async void LoadTabData(int tabIndex)
    {
        try
        {
            switch (tabIndex)
            {
                case 1: // Episodes
                    if (!_episodesLoaded)
                    {
                        _episodesLoaded = true;
                        await Vm.LoadEpisodes(false);
                    }
                    break;
                case 2: // Characters
                    if (!_charactersLoaded)
                    {
                        _charactersLoaded = true;
                        await Vm.LoadCharacters(false);
                    }
                    break;
                case 3: // Staff
                    if (!_staffLoaded)
                    {
                        _staffLoaded = true;
                        await Vm.LoadCharacters(false); // LoadCharacters loads both
                    }
                    break;
                case 4: // Recommendations
                    if (!_recommendationsLoaded)
                    {
                        _recommendationsLoaded = true;
                        await Vm.LoadRecommendations(false);
                    }
                    break;
                case 5: // Related
                    if (!_relatedLoaded)
                    {
                        _relatedLoaded = true;
                        await Vm.LoadRelatedAnime(false);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("MALPLUS LoadTabData failed: " + ex);
        }
    }

    private void OnTabTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is string param && int.TryParse(param, out int tabIndex))
        {
            Vm.DetailsPivotSelectedIndex = tabIndex;
        }
    }

    private void OnHeroScrolled(object sender, ScrolledEventArgs e)
    {
        // Hero is fixed in v2 (no collapse animation), so this handler is a no-op
    }
}
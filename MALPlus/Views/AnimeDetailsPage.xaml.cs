using MALClient.Models.Enums;
using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;
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

    // v2 parity: status/score buttons open pickers (AnimeUpdateDialogBuilder),
    // the VM commands take the picked value as string and crash without one.
    private async void OnStatusClicked(object sender, EventArgs e)
    {
        try
        {
            var options = new[]
            {
                AnimeStatus.Watching, AnimeStatus.Completed, AnimeStatus.OnHold,
                AnimeStatus.Dropped, AnimeStatus.PlanToWatch
            };
            var labels = options.Select(s => Utilities.StatusToString((int)s, !Vm.AnimeMode, false)).ToArray();
            var choice = await DisplayActionSheet("Set status", "Cancel", null, labels);
            if (string.IsNullOrEmpty(choice) || choice == "Cancel")
                return;
            var index = Array.IndexOf(labels, choice);
            if (index >= 0)
                Vm.ChangeStatus(options[index]);
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS OnStatusClicked failed: " + ex.GetType().Name);
        }
    }

    private async void OnScoreClicked(object sender, EventArgs e)
    {
        try
        {
            var choice = await DisplayActionSheet("Set score", "Cancel", null,
                "10", "9", "8", "7", "6", "5", "4", "3", "2", "1", "0 (clear)");
            if (string.IsNullOrEmpty(choice) || choice == "Cancel")
                return;
            var num = choice.Split(' ')[0];
            if (int.TryParse(num, out var score))
                Vm.ChangeScoreCommand.Execute(score.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS OnScoreClicked failed: " + ex.GetType().Name);
        }
    }

    private void OnTabTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is string param && int.TryParse(param, out int tabIndex))
        {
            Vm.DetailsPivotSelectedIndex = tabIndex;
            ResetHero();
            ScrollActiveTabToTop();
        }
    }

    // v2 hero mechanics (AnimeDetailsPageFragment AppBarOffsetListener):
    // ratio = scrollY / (460 - 210); poster scale = 1 + 0.35 * ratio; scrim alpha = ratio.
    private const double HeroExpandedHeight = 460;
    private const double HeroCollapsedHeight = 210;
    private const double HeroCollapseRange = HeroExpandedHeight - HeroCollapsedHeight;
    private double _lastCollapseRatio = -1;

    private void OnTabContentScrolled(object sender, ScrolledEventArgs e)
    {
        ApplyHeroCollapse(e.ScrollY);
    }

    private void OnTabItemsScrolled(object sender, ItemsViewScrolledEventArgs e)
    {
        ApplyHeroCollapse(e.VerticalOffset);
    }

    private void ApplyHeroCollapse(double scrollY)
    {
        var ratio = Math.Clamp(scrollY / HeroCollapseRange, 0, 1);
        if (Math.Abs(ratio - _lastCollapseRatio) < 0.01)
            return;
        _lastCollapseRatio = ratio;
        HeroContainer.HeightRequest = HeroExpandedHeight - HeroCollapseRange * ratio;
        var scale = 1 + 0.35 * ratio;
        PosterContainer.ScaleX = scale;
        PosterContainer.ScaleY = scale;
        HeroScrim.Opacity = ratio;
    }

    private void ResetHero()
    {
        _lastCollapseRatio = -1;
        ApplyHeroCollapse(0);
    }

    private void ScrollActiveTabToTop()
    {
        try
        {
            switch (Vm.DetailsPivotSelectedIndex)
            {
                case 0: _ = GeneralScroll.ScrollToAsync(0, 0, false); break;
                case 1: EpisodesList.ScrollTo(0, animate: false); break;
                case 2: CharactersList.ScrollTo(0, animate: false); break;
                case 3: StaffList.ScrollTo(0, animate: false); break;
                case 4: RecsList.ScrollTo(0, animate: false); break;
                case 5: RelatedList.ScrollTo(0, animate: false); break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("MALPLUS ScrollActiveTabToTop failed: " + ex.GetType().Name);
        }
    }
}
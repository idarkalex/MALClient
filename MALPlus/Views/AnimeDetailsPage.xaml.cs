using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Details;

namespace MALPlus.Views;

[QueryProperty(nameof(MalId), "id")]
[QueryProperty(nameof(AnimeTitle), "title")]
public partial class AnimeDetailsPage : ContentPage
{
    private bool _initialized;

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
                DebugLabel.Text += " fullhttp=" + (int)r.StatusCode + " len=" + body.Length
                    + " hasrank=" + body.Contains("\"rank\"");
            }
            catch (Exception ex)
            {
                DebugLabel.Text += " fullhttp-FAIL " + ex.GetType().Name;
            }
        });
        try
        {
            Vm.Init(new AnimeDetailsPageNavigationArgs(int.Parse(MalId), AnimeTitle, null, null, null), fakeDelay: false);
            await Task.Delay(12000);
            DebugLabel.Text = "loaded title=" + Vm.Title + " synlen=" + (Vm.Synopsis ?? "").Length
                + " rank=" + Vm.GeneralRank + " pop=" + Vm.GeneralPopularity + " studios=" + Vm.GeneralStudios;
            Console.WriteLine("MALPLUS Details Init returned title=" + Vm.Title);
        }
        catch (Exception ex)
        {
            DebugLabel.Text = "details FAILED " + ex.GetType().Name;
            Console.WriteLine("MALPLUS Details Init failed: " + ex);
        }
    }
}

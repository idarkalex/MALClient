using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Clubs;

namespace MALPlus.Views;

[QueryProperty(nameof(ClubId), "id")]
public partial class ClubDetailsPage : ContentPage
{
    private bool _initialized;
    public string ClubId { get; set; }

    private ClubDetailsViewModel Vm => (ClubDetailsViewModel)BindingContext;

    public ClubDetailsPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.ClubDetails;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;
        try
        {
            Vm.NavigatedTo(new ClubDetailsPageNavArgs { Id = ClubId });
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(200);
                if (!Vm.Loading) break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS ClubDetailsPage Init failed: " + ex.Message);
        }
    }
}

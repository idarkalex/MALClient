using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Details;

namespace MALPlus.Views;

[QueryProperty(nameof(StaffId), "id")]
public partial class StaffDetailsPage : ContentPage
{
    private bool _initialized;

    public string StaffId { get; set; }

    private StaffDetailsViewModel Vm => (StaffDetailsViewModel)BindingContext;

    public StaffDetailsPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.StaffDetails;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized)
            return;
        _initialized = true;
        try
        {
            int id = 0;
            int.TryParse(StaffId, out id);
            Vm.Init(new StaffDetailsNaviagtionArgs { Id = id });
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(200);
                if (!Vm.Loading && Vm.Data != null)
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS StaffDetails Init failed: " + ex);
        }
    }
}

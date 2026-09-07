using MALClient.Models.Enums;
using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Details;

namespace MALPlus.Views;

[QueryProperty(nameof(CharId), "id")]
public partial class CharacterDetailsPage : ContentPage
{
    private bool _initialized;

    public string CharId { get; set; }

    private CharacterDetailsViewModel Vm => (CharacterDetailsViewModel)BindingContext;

    public CharacterDetailsPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.CharacterDetails;
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
            int.TryParse(CharId, out id);
            Vm.Init(new CharacterDetailsNavigationArgs { Id = id });
            // wait for data
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(200);
                if (!Vm.Loading && Vm.Data != null)
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS CharacterDetails Init failed: " + ex);
        }
    }
}

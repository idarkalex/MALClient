using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALPlus.Views;

[QueryProperty(nameof(TargetUser), "user")]
public partial class ProfilePage : ContentPage
{
    private bool _initialized;

    public string TargetUser { get; set; }

    private ProfilePageViewModel Vm => (ProfilePageViewModel)BindingContext;

    public ProfilePage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.ProfilePage;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        System.Diagnostics.Debug.WriteLine("MALPLUS ProfilePage OnAppearing");
        if (_initialized)
            return;
        _initialized = true;

        var args = new ProfilePageNavigationArgs
        {
            TargetUser = TargetUser ?? Credentials.UserName
        };
        System.Diagnostics.Debug.WriteLine("MALPLUS ProfilePage calling LoadProfileData");
        try
        {
            await Vm.LoadProfileData(args);
            System.Diagnostics.Debug.WriteLine("MALPLUS ProfilePage LoadProfileData completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("MALPLUS Profile Init failed: " + ex);
        }
    }
}
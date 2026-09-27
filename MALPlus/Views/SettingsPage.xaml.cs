using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels;

namespace MALPlus.Views;

public partial class SettingsPage : ContentPage
{
    private bool _initialized;
    private SettingsViewModelBase Vm => (SettingsViewModelBase)BindingContext;

    /// <summary>Mirrors LogInViewModel.LogOutButtonVisibility so the row only shows when signed in.</summary>
    public bool LogOutButtonVisibility => MALClient.XShared.Utils.Credentials.Authenticated;

    public SettingsPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.Settings;
        UpdateAccountHeader();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateAccountHeader();
        if (_initialized) return;
        _initialized = true;
    }

    private void UpdateAccountHeader()
    {
        if (AccountNameLabel == null)
            return;
        var name = MALClient.XShared.Utils.Credentials.UserName;
        AccountNameLabel.Text = string.IsNullOrWhiteSpace(name)
            ? "Not signed in"
            : $"Signed in as {name}";
        if (LogOutButton != null)
            LogOutButton.IsVisible = MALClient.XShared.Utils.Credentials.Authenticated;
    }

    private async void OnLogOutClicked(object sender, EventArgs e)
    {
        try
        {
            var ok = await DisplayAlert("Sign out",
                "You will need to sign in again, and the forums, clubs and wall will stay unavailable until then.",
                "Sign out", "Cancel");
            if (!ok)
                return;

            // Drops the api tokens, the user id and the MAL website session cookies together, so
            // the next sign in is the only thing that can bring the community sections back.
            MALClient.XShared.Utils.Credentials.Reset();
            MALClient.XShared.Comm.Query.RefreshClientAuthHeader();
            UpdateAccountHeader();
            await Shell.Current.GoToAsync("login");
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS logout failed: " + ex.GetType().Name);
        }
    }

    private async void OnClearCachesClicked(object sender, EventArgs e)
    {
        try
        {
            var ok = await DisplayAlert("Clear caches",
                "This will remove all locally cached data and force a fresh fetch on the next open. Continue?",
                "Clear", "Cancel");
            if (!ok) return;
            // Best-effort: ask the SimpleIoc-resolved data cache to invalidate.
            try
            {
                await ResourceLocator.DataCacheService.ClearApiRelatedCache();
            }
            catch { }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS clear caches failed: " + ex.Message);
        }
    }
}

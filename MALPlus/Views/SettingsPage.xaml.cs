using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels;

namespace MALPlus.Views;

public partial class SettingsPage : ContentPage
{
    private bool _initialized;
    private SettingsViewModelBase Vm => (SettingsViewModelBase)BindingContext;

    public SettingsPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.Settings;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;
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

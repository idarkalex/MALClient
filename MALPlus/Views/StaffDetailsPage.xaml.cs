using MALClient.Models.Enums;
using MALClient.Models.Models.Anime;
using MALClient.Models.Models.ScrappedDetails;
using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Details;
using MALPlus.Services;

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

    private async void OnVoiceRoleTapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (e.Parameter is ShowCharacterPair pair &&
                int.TryParse(pair.AnimeCharacter?.Id, out var characterId) && characterId > 0)
                await Shell.Current.GoToAsync($"character?id={characterId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS OnVoiceRoleTapped failed: " + ex.GetType().Name);
        }
    }

    private async void OnStaffPositionTapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (e.Parameter is not AnimeLightEntry entry || entry.Id <= 0)
                return;
            var args = new AnimeDetailsPageNavigationArgs(entry.Id, entry.Title, null, null, null)
            {
                AnimeMode = entry.IsAnime,
                Source = PageIndex.PageStaffDetails
            };
            MauiDetailsNavigationHandoff.Set(args);
            var title = Uri.EscapeDataString(entry.Title ?? string.Empty);
            var route = entry.IsAnime
                ? $"animedetails?id={entry.Id}&title={title}"
                : $"animedetails?id={entry.Id}&title={title}&manga=true";
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS OnStaffPositionTapped failed: " + ex.GetType().Name);
        }
    }
}

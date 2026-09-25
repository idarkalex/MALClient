using MALClient.Models.Enums;
using MALClient.Models.Models.Anime;
using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Details;
using MALPlus.Services;

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

    private async void OnVoiceActorTapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (e.Parameter is string idStr && int.TryParse(idStr, out int id))
            {
                await Shell.Current.GoToAsync($"staff?id={id}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS OnVoiceActorTapped failed: " + ex.GetType().Name);
        }
    }

    private async void OnAnimeographyTapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (e.Parameter is not AnimeLightEntry entry || entry.Id <= 0)
                return;
            var args = new AnimeDetailsPageNavigationArgs(entry.Id, entry.Title, null, null, null)
            {
                AnimeMode = entry.IsAnime,
                Source = PageIndex.PageCharacterDetails
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
            Console.WriteLine("MALPLUS OnAnimeographyTapped failed: " + ex.GetType().Name);
        }
    }
}

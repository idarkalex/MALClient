using System.Collections.ObjectModel;
using MALClient.Models.Enums;
using MALClient.Models.Models.Anime;
using MALClient.Models.Models.Favourites;
using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Details;
using MALPlus.Services;
using Microsoft.Maui.Graphics;

namespace MALPlus.Views;

[QueryProperty(nameof(CharId), "id")]
public partial class CharacterDetailsPage : ContentPage
{
    private bool _initialized;
    private int _loadedId = -1;
    private int _selectedTab;

    public string CharId { get; set; }

    private CharacterDetailsViewModel Vm => BindingContext as CharacterDetailsViewModel;

    public CharacterDetailsPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.CharacterDetails;
        UpdateTabVisuals();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var vm = Vm;
        if (vm == null)
            return;
        int.TryParse(CharId, out int id);
        if (!_initialized || _loadedId != id)
        {
            try
            {
                await vm.Init(new CharacterDetailsNavigationArgs {Id = id});
                _loadedId = id;
                _initialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("MALPLUS CharacterDetails Init failed: " + ex);
            }
        }
    }

    private async void OnOverviewTabTapped(object sender, TappedEventArgs e)
    {
        await SelectTabAsync(0, true);
    }

    private async void OnVoiceActorsTabTapped(object sender, TappedEventArgs e)
    {
        await SelectTabAsync(1, true);
    }

    private async void OnAnimeographyTabTapped(object sender, TappedEventArgs e)
    {
        await SelectTabAsync(2, true);
    }

    private async void OnMangaographyTabTapped(object sender, TappedEventArgs e)
    {
        await SelectTabAsync(3, true);
    }

    private async Task SelectTabAsync(int index, bool animate)
    {
        index = System.Math.Clamp(index, 0, 3);
        if (index == _selectedTab)
            return;

        OverviewTab.IsVisible = index == 0;
        VoiceActorsTab.IsVisible = index == 1;
        AnimeographyTab.IsVisible = index == 2;
        MangaographyTab.IsVisible = index == 3;
        _selectedTab = index;
        UpdateTabVisuals();
        await Task.CompletedTask;
    }

    private async Task ResetTabTranslationAsync()
    {
        await Task.CompletedTask;
    }

    private void UpdateTabVisuals()
    {
        var labels = new[] { OverviewTabLabel, VoiceActorsTabLabel, AnimeographyTabLabel, MangaographyTabLabel };
        for (var i = 0; i < labels.Length; i++)
        {
            if (labels[i] == null)
                continue;
            labels[i].TextColor = i == _selectedTab ? Color.FromArgb("#0066FF") : Color.FromArgb("#B3FFFFFF");
        }
    }

    private async void OnVoiceActorTapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (e.Parameter is string idStr && int.TryParse(idStr, out int id) && id > 0 && Shell.Current != null)
                await Shell.Current.GoToAsync($"staff?id={id}");
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
            if (Shell.Current != null)
                await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS OnAnimeographyTapped failed: " + ex.GetType().Name);
        }
    }
}

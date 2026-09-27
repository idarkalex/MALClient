using System.Collections.ObjectModel;
using MALClient.Models.Enums;
using MALClient.Models.Models.Anime;
using MALClient.Models.Models.ScrappedDetails;
using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Details;
using MALPlus.Services;
using Microsoft.Maui.Graphics;

namespace MALPlus.Views;

[QueryProperty(nameof(StaffId), "id")]
public partial class StaffDetailsPage : ContentPage
{
    private bool _initialized;
    private int _loadedId = -1;
    private StaffDetailsViewModel _subscribedVm;
    private int _selectedTab;

    public string StaffId { get; set; }

    private StaffDetailsViewModel Vm => BindingContext as StaffDetailsViewModel;

    public StaffDetailsPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.StaffDetails;
        UpdateTabVisuals();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var vm = Vm;
        if (vm == null)
            return;
        SubscribePivot(vm);
        int.TryParse(StaffId, out int id);
        if (!_initialized || _loadedId != id)
        {
            try
            {
                await vm.Init(new StaffDetailsNaviagtionArgs {Id = id});
                _loadedId = id;
                _initialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("MALPLUS StaffDetails Init failed: " + ex);
            }
            ResetRenderedItems();
        }
    }

    protected override void OnDisappearing()
    {
        UnsubscribePivot();
        base.OnDisappearing();
    }

    private void SubscribePivot(StaffDetailsViewModel vm)
    {
        if (_subscribedVm == vm)
            return;
        UnsubscribePivot();
        vm.OnPivotItemSelectionRequest += OnPivotItemSelectionRequest;
        _subscribedVm = vm;
    }

    private void UnsubscribePivot()
    {
        if (_subscribedVm == null)
            return;
        _subscribedVm.OnPivotItemSelectionRequest -= OnPivotItemSelectionRequest;
        _subscribedVm = null;
    }

    private void OnPivotItemSelectionRequest(int index)
    {
        if (index < 0 || index > 2)
            return;
        _ = SelectTabAsync(index, true);
    }

    private void ResetRenderedItems()
    {
        if ((Vm?.Data?.ShowCharacterPairs?.Count ?? 0) == 0 && (Vm?.Data?.StaffPositions?.Count ?? 0) > 0)
            _ = SelectTabAsync(2, false);
        else if ((Vm?.Data?.ShowCharacterPairs?.Count ?? 0) == 0)
            _ = SelectTabAsync(1, false);
    }

    private async void OnInfoTabTapped(object sender, TappedEventArgs e)
    {
        await SelectTabAsync(0, true);
    }

    private async void OnVoiceRolesTabTapped(object sender, TappedEventArgs e)
    {
        await SelectTabAsync(1, true);
    }

    private async void OnProductionRolesTabTapped(object sender, TappedEventArgs e)
    {
        await SelectTabAsync(2, true);
    }

    private async Task SelectTabAsync(int index, bool animate)
    {
        index = System.Math.Clamp(index, 0, 2);
        if (index == _selectedTab)
        {
            await ResetTabTranslationAsync();
            return;
        }

        int direction = index > _selectedTab ? 1 : -1;
        InfoTab.IsVisible = index == 0;
        VoiceRolesTab.IsVisible = index == 1;
        ProductionRolesTab.IsVisible = index == 2;
        _selectedTab = index;
        UpdateTabVisuals();

        if (animate && TabContentHost.Width > 0)
        {
            TabContentHost.TranslationX = direction * TabContentHost.Width;
            await TabContentHost.TranslateTo(0, 0, 160, Easing.CubicOut);
        }
        else
        {
            TabContentHost.TranslationX = 0;
        }
    }

    private async Task ResetTabTranslationAsync()
    {
        if (System.Math.Abs(TabContentHost.TranslationX) <= 0.5)
        {
            TabContentHost.TranslationX = 0;
            return;
        }
        await TabContentHost.TranslateTo(0, 0, 140, Easing.CubicOut);
        TabContentHost.TranslationX = 0;
    }

    private void UpdateTabVisuals()
    {
        var labels = new[] { InfoTabLabel, VoiceRolesTabLabel, ProductionRolesTabLabel };
        for (var i = 0; i < labels.Length; i++)
        {
            if (labels[i] == null)
                continue;
            labels[i].TextColor = i == _selectedTab ? Color.FromArgb("#0066FF") : Color.FromArgb("#B3FFFFFF");
        }
    }

    private async void OnVoiceRoleShowTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is ShowCharacterPair pair)
            await NavigateToMediaAsync(pair.AnimeLightEntry);
    }

    private async void OnVoiceRoleCharacterTapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (e.Parameter is ShowCharacterPair pair &&
                int.TryParse(pair.AnimeCharacter?.Id, out int characterId) && characterId > 0 &&
                Shell.Current != null)
                await Shell.Current.GoToAsync($"character?id={characterId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS OnVoiceRoleCharacterTapped failed: " + ex.GetType().Name);
        }
    }

    private async void OnStaffPositionTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is AnimeLightEntry entry)
            await NavigateToMediaAsync(entry);
    }

    private async Task NavigateToMediaAsync(AnimeLightEntry entry)
    {
        if (entry == null || entry.Id <= 0)
            return;
        try
        {
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
            if (Shell.Current != null)
                await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS StaffDetails media navigation failed: " + ex.GetType().Name);
        }
    }
}

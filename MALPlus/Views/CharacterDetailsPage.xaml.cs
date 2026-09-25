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
    private const int VoiceActorChunkSize = 12;
    private const int MediaChunkSize = 8;
    private const double PanActivationDistance = 14;
    private bool _initialized;
    private int _loadedId = -1;
    private bool _horizontalPanActive;
    private int _selectedTab;
    private int _voiceActorCount;
    private int _animeographyCount;
    private int _mangaographyCount;
    private double _lastProgressiveScrollY;
    private readonly ObservableCollection<FavouriteViewModel> _voiceActorItems = new ObservableCollection<FavouriteViewModel>();
    private readonly ObservableCollection<AnimeLightEntry> _animeographyItems = new ObservableCollection<AnimeLightEntry>();
    private readonly ObservableCollection<AnimeLightEntry> _mangaographyItems = new ObservableCollection<AnimeLightEntry>();

    public string CharId { get; set; }

    public ObservableCollection<FavouriteViewModel> VoiceActorItems => _voiceActorItems;

    public ObservableCollection<AnimeLightEntry> AnimeographyItems => _animeographyItems;

    public ObservableCollection<AnimeLightEntry> MangaographyItems => _mangaographyItems;

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
            ResetRenderedItems();
        }
    }

    private void ResetRenderedItems()
    {
        _voiceActorItems.Clear();
        _animeographyItems.Clear();
        _mangaographyItems.Clear();
        _voiceActorCount = 0;
        _animeographyCount = 0;
        _mangaographyCount = 0;
        _lastProgressiveScrollY = 0;
        AppendVoiceActors();
        AppendAnimeography();
        AppendMangaography();
        UpdateLoadMoreButtons();
    }

    private void AppendVoiceActors()
    {
        var source = Vm?.VoiceActors;
        if (source == null || _voiceActorCount >= source.Count)
            return;
        int target = System.Math.Min(_voiceActorCount + VoiceActorChunkSize, source.Count);
        for (int i = _voiceActorCount; i < target; i++)
        {
            if (source[i] != null)
                _voiceActorItems.Add(source[i]);
        }
        _voiceActorCount = target;
    }

    private void AppendAnimeography()
    {
        var source = Vm?.Data?.Animeography;
        if (source == null || _animeographyCount >= source.Count)
            return;
        int target = System.Math.Min(_animeographyCount + MediaChunkSize, source.Count);
        for (int i = _animeographyCount; i < target; i++)
        {
            if (source[i] != null)
                _animeographyItems.Add(source[i]);
        }
        _animeographyCount = target;
    }

    private void AppendMangaography()
    {
        var source = Vm?.Data?.Mangaography;
        if (source == null || _mangaographyCount >= source.Count)
            return;
        int target = System.Math.Min(_mangaographyCount + MediaChunkSize, source.Count);
        for (int i = _mangaographyCount; i < target; i++)
        {
            if (source[i] != null)
                _mangaographyItems.Add(source[i]);
        }
        _mangaographyCount = target;
    }

    private void UpdateLoadMoreButtons()
    {
        VoiceActorsLoadMoreButton.IsVisible = (Vm?.VoiceActors?.Count ?? 0) > _voiceActorCount;
        AnimeographyLoadMoreButton.IsVisible = (Vm?.Data?.Animeography?.Count ?? 0) > _animeographyCount;
        MangaographyLoadMoreButton.IsVisible = (Vm?.Data?.Mangaography?.Count ?? 0) > _mangaographyCount;
    }

    private void AppendForSelectedTab()
    {
        switch (_selectedTab)
        {
            case 1:
                AppendVoiceActors();
                break;
            case 2:
                AppendAnimeography();
                break;
            case 3:
                AppendMangaography();
                break;
        }
        UpdateLoadMoreButtons();
    }

    private void OnLoadMoreVoiceActorsClicked(object sender, EventArgs e)
    {
        AppendVoiceActors();
        UpdateLoadMoreButtons();
    }

    private void OnLoadMoreAnimeographyClicked(object sender, EventArgs e)
    {
        AppendAnimeography();
        UpdateLoadMoreButtons();
    }

    private void OnLoadMoreMangaographyClicked(object sender, EventArgs e)
    {
        AppendMangaography();
        UpdateLoadMoreButtons();
    }

    private void OnContentScrolled(object sender, ScrolledEventArgs e)
    {
        CompactHeaderHost.TranslationY = System.Math.Max(FullHero.Height - e.ScrollY, 0);
        if (ContentScroll.Height <= 0 || e.ScrollY < _lastProgressiveScrollY + ContentScroll.Height * 0.72)
            return;
        _lastProgressiveScrollY = e.ScrollY;
        AppendForSelectedTab();
    }

    private void OnTabPanUpdated(object sender, PanUpdatedEventArgs e)
    {
        if (e.StatusType == GestureStatus.Started)
        {
            _horizontalPanActive = false;
            TabContentHost.TranslationX = 0;
            return;
        }
        if (e.StatusType == GestureStatus.Completed)
        {
            _ = CompleteTabPanAsync(e.TotalX, true);
            return;
        }
        if (e.StatusType == GestureStatus.Canceled)
        {
            _ = CompleteTabPanAsync(e.TotalX, false);
            return;
        }
        if (e.StatusType != GestureStatus.Running)
            return;

        double horizontal = System.Math.Abs(e.TotalX);
        double vertical = System.Math.Abs(e.TotalY);
        if (!_horizontalPanActive)
        {
            if (horizontal < PanActivationDistance || horizontal <= vertical * 1.2)
            {
                TabContentHost.TranslationX = 0;
                return;
            }
            _horizontalPanActive = true;
        }

        bool canMove = e.TotalX < 0 ? _selectedTab < 3 : _selectedTab > 0;
        double translation = canMove ? e.TotalX : e.TotalX * 0.2;
        double width = System.Math.Max(TabContentHost.Width, 1);
        TabContentHost.TranslationX = System.Math.Clamp(translation, -width, width);
    }

    private async Task CompleteTabPanAsync(double totalX, bool allowSelection)
    {
        double threshold = System.Math.Max(80, TabContentHost.Width * 0.18);
        bool shouldSelect = allowSelection && _horizontalPanActive && System.Math.Abs(totalX) >= threshold;
        _horizontalPanActive = false;
        if (shouldSelect)
        {
            int target = _selectedTab + (totalX < 0 ? 1 : -1);
            if (target >= 0 && target <= 3)
            {
                await SelectTabAsync(target, true);
                return;
            }
        }
        await ResetTabTranslationAsync();
    }

    private async void OnOverviewTabClicked(object sender, EventArgs e)
    {
        await SelectTabAsync(0, true);
    }

    private async void OnVoiceActorsTabClicked(object sender, EventArgs e)
    {
        await SelectTabAsync(1, true);
    }

    private async void OnAnimeographyTabClicked(object sender, EventArgs e)
    {
        await SelectTabAsync(2, true);
    }

    private async void OnMangaographyTabClicked(object sender, EventArgs e)
    {
        await SelectTabAsync(3, true);
    }

    private async Task SelectTabAsync(int index, bool animate)
    {
        index = System.Math.Clamp(index, 0, 3);
        if (index == _selectedTab)
        {
            await ResetTabTranslationAsync();
            return;
        }

        int direction = index > _selectedTab ? 1 : -1;
        OverviewTab.IsVisible = index == 0;
        VoiceActorsTab.IsVisible = index == 1;
        AnimeographyTab.IsVisible = index == 2;
        MangaographyTab.IsVisible = index == 3;
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
        Button[] buttons =
        {
            OverviewTabButton,
            VoiceActorsTabButton,
            AnimeographyTabButton,
            MangaographyTabButton
        };
        for (int i = 0; i < buttons.Length; i++)
        {
            bool selected = i == _selectedTab;
            buttons[i].TextColor = selected ? Color.FromArgb("#FF6B00") : Colors.White;
            buttons[i].BackgroundColor = selected ? Color.FromArgb("#1AFFFFFF") : Colors.Transparent;
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

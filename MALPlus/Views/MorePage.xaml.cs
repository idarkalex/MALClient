using MALClient.Models.Enums;
using MALClient.XShared.Comm.Anime;
using MALClient.XShared.Comm.Profile;
using Microsoft.Maui.Controls.Shapes;

using System.Collections.Generic;
using System.Linq;
using MALClient.Models.Models.Favourites;
using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;
using Microsoft.Maui.Controls;

namespace MALPlus.Views;

public partial class MorePage : ContentPage
{
    private bool _initialized;

    private bool _animeListPanelExpanded;
    private bool _mangaListPanelExpanded;
    private bool _topAnimePanelExpanded;
    private bool _topMangaPanelExpanded;
    private bool _adaptedPanelExpanded;

    public MorePage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized)
        {
            RestorePanelState();
            return;
        }
        _initialized = true;

        LoadProfileDataAsync();

        // Populate collapsible panels
        PopulateStatusPanel(AnimeListPanel, false);
        PopulateTypePanel(TopAnimePanel, 
            Enum.GetValues(typeof(TopAnimeType)).Cast<TopAnimeType>().Select(t => (t.ToString(), (Action)(() => NavigateTo(PageIndex.PageTopAnime, AnimeListPageNavigationArgs.TopAnime(t))))).ToList());
        PopulateTypePanel(TopMangaPanel, 
            Enum.GetValues(typeof(MangaTopType)).Cast<MangaTopType>().Select(t => (t.ToString(), (Action)(() => NavigateTo(PageIndex.PageTopManga, AnimeListPageNavigationArgs.TopMangaCategory(t))))).ToList());
        PopulateTypePanel(AdaptedPanel, 
            Enum.GetValues(typeof(MangaAdaptedType)).Cast<MangaAdaptedType>().Select(t => (AnimeAdaptedToAnimeQuery.ToDisplayName(t), (Action)(() => NavigateTo(PageIndex.PageMangaAdapted, AnimeListPageNavigationArgs.MangaAdapted(t))))).ToList());
        PopulateStatusPanel(MangaListPanel, true);

        // Restore panel state from saved UI state
        RestorePanelState();
    }

    private async void LoadProfileDataAsync()
    {
        try
        {
            if (Credentials.Authenticated)
            {
                ProfileUsername.Text = Credentials.UserName;
                ProfileCompleted.IsVisible = false;

                var cacheDuration = TimeSpan.FromMinutes(10);
                ProfileImage.Source = new UriImageSource
                {
                    Uri = new Uri($"https://cdn.myanimelist.net/images/userimages/{Credentials.Id}.webp"),
                    CachingEnabled = true,
                    CacheValidity = cacheDuration
                };

                // Load profile data from cache
                try
                {
                    var data = await DataCache.RetrieveProfileData(Credentials.UserName);
                    if (data?.User?.ImgUrl != null)
                    {
                        ProfileImage.Source = new UriImageSource { Uri = new Uri(data.User.ImgUrl), CachingEnabled = true };
                        if (data.AnimeCompleted > 0 || data.MangaCompleted > 0)
                        {
                            ProfileCompleted.Text = $"{data.AnimeCompleted} Completed";
                            ProfileCompleted.IsVisible = true;
                        }
                    }
                    else
                    {
                        RefreshProfileCacheInBackground();
                    }
                }
                catch
                {
                    RefreshProfileCacheInBackground();
                }
            }
            else
            {
                ProfileUsername.Text = "Log in";
                ProfileCompleted.IsVisible = false;
                ProfileImage.Source = null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MALPLUS MorePage LoadProfileDataAsync error: {ex.Message}");
        }
    }

    private async void RefreshProfileCacheInBackground()
    {
        try
        {
            var fresh = await new ProfileQuery(Credentials.UserName).GetProfileData(false);
            if (fresh?.User?.ImgUrl != null)
                ProfileImage.Source = new UriImageSource { Uri = new Uri(fresh.User.ImgUrl), CachingEnabled = true };
        }
        catch (Exception) { }
    }

    #region Navigation Handlers

    private void OnAnimeListTapped(object sender, TappedEventArgs e)
    {
        TogglePanel(AnimeListPanel, AnimeListArrow, v => _animeListPanelExpanded = v, _animeListPanelExpanded);
    }

    private void OnMangaListTapped(object sender, TappedEventArgs e)
    {
        TogglePanel(MangaListPanel, MangaListArrow, v => _mangaListPanelExpanded = v, _mangaListPanelExpanded);
    }

    private void OnTopAnimeTapped(object sender, TappedEventArgs e)
    {
        TogglePanel(TopAnimePanel, TopAnimeArrow, v => _topAnimePanelExpanded = v, _topAnimePanelExpanded);
    }

    private void OnTopMangaTapped(object sender, TappedEventArgs e)
    {
        TogglePanel(TopMangaPanel, TopMangaArrow, v => _topMangaPanelExpanded = v, _topMangaPanelExpanded);
    }

    private void OnAdaptedTapped(object sender, TappedEventArgs e)
    {
        TogglePanel(AdaptedPanel, AdaptedArrow, v => _adaptedPanelExpanded = v, _adaptedPanelExpanded);
    }

    private void OnSeasonalTapped(object sender, TappedEventArgs e)
    {
        NavigateTo(PageIndex.PageSeasonal, AnimeListPageNavigationArgs.Seasonal);
    }

    private void OnSearchTapped(object sender, TappedEventArgs e)
    {
        NavigateTo(PageIndex.PageSearch, new SearchPageNavigationArgs());
    }

    private void OnRecommendationsTapped(object sender, TappedEventArgs e)
    {
        NavigateTo(PageIndex.PageRecomendations, null);
    }

    private void OnCalendarTapped(object sender, TappedEventArgs e)
    {
        NavigateTo(PageIndex.PageCalendar, null);
    }

    private void OnArticlesTapped(object sender, TappedEventArgs e)
    {
        NavigateTo(PageIndex.PageArticles, new MalArticlesPageNavigationArgs { WorkMode = ArticlePageWorkMode.Articles, Source = PageIndex.PageMore });
    }

    private void OnVideosTapped(object sender, TappedEventArgs e)
    {
        NavigateTo(PageIndex.PagePopularVideos, null);
    }

    private void OnForumsTapped(object sender, TappedEventArgs e)
    {
        NavigateTo(PageIndex.PageForumIndex, null);
    }

    private void OnMessagingTapped(object sender, TappedEventArgs e)
    {
        if (!Credentials.Authenticated) { NavigateToLogin(); return; }
        NavigateTo(PageIndex.PageMessanging, null);
    }

    private void OnNotificationsTapped(object sender, TappedEventArgs e)
    {
        if (!Credentials.Authenticated) { NavigateToLogin(); return; }
        NavigateTo(PageIndex.PageNotificationHub, null);
    }

    private void OnFriendsTapped(object sender, TappedEventArgs e)
    {
        if (!Credentials.Authenticated) { NavigateToLogin(); return; }
        NavigateTo(PageIndex.PageFriends, null);
    }

    private void OnFeedsTapped(object sender, TappedEventArgs e)
    {
        if (!Credentials.Authenticated) { NavigateToLogin(); return; }
        NavigateTo(PageIndex.PageFeeds, null);
    }

    private void OnHistoryTapped(object sender, TappedEventArgs e)
    {
        if (!Credentials.Authenticated) { NavigateToLogin(); return; }
        NavigateTo(PageIndex.PageHistory, null);
    }

    private void OnSettingsTapped(object sender, TappedEventArgs e)
    {
        NavigateTo(PageIndex.PageSettings, null);
    }

    private void OnProfileHeaderTapped(object sender, TappedEventArgs e)
    {
        if (!Credentials.Authenticated) { NavigateToLogin(); return; }
        NavigateTo(PageIndex.PageProfile, new ProfilePageNavigationArgs { TargetUser = Credentials.UserName });
    }

    private void OnLogOutTapped(object sender, TappedEventArgs e)
    {
        try
        {
            // Fire-and-forget log out (mirrors v2 LogOutCommand flow).
            MALClient.XShared.Utils.Credentials.Reset();
            Shell.Current?.GoToAsync("//login");
        }
        catch (Exception ex) { Console.WriteLine("MALPLUS logout failed: " + ex.Message); }
    }

    private void NavigateToLogin()
    {
        try { Shell.Current?.GoToAsync("//login"); } catch { }
    }

    #endregion

    #region Panel Animation

    private void TogglePanel(Grid panel, Label arrow, System.Action<bool> setExpanded, bool currentExpanded)
    {
        if (currentExpanded)
        {
            AnimateCollapse(panel, arrow);
            setExpanded(false);
        }
        else
        {
            AnimateExpand(panel, arrow);
            setExpanded(true);
        }
    }

    private void AnimateExpand(Grid panel, Label arrow)
    {
        panel.IsVisible = true;
        panel.Opacity = 0;
        panel.TranslationY = -20;
        
        var animation = new Animation(
            d => panel.Opacity = d, 0, 1, Easing.CubicOut);
        animation.Commit(panel, "Expand", 16, 180, Easing.CubicOut);

        var translateAnim = new Animation(
            d => panel.TranslationY = d, -20, 0, Easing.CubicOut);
        translateAnim.Commit(panel, "TranslateExpand", 16, 180, Easing.CubicOut);

        var rotateAnim = new Animation(d => arrow.Rotation = d, 0, 180, Easing.CubicOut);
        rotateAnim.Commit(arrow, "RotateExpand", 16, 180, Easing.CubicOut);
    }

    private void AnimateCollapse(Grid panel, Label arrow)
    {
        var animation = new Animation(
            d => panel.Opacity = d, 1, 0, Easing.CubicIn);
        animation.Commit(panel, "Collapse", 16, 160, Easing.CubicIn, finished: (d, b) => panel.IsVisible = false);

        var translateAnim = new Animation(
            d => panel.TranslationY = d, 0, -20, Easing.CubicIn);
        translateAnim.Commit(panel, "TranslateCollapse", 16, 160, Easing.CubicIn);

        var rotateAnim = new Animation(d => arrow.Rotation = d, 180, 0, Easing.CubicIn);
        rotateAnim.Commit(arrow, "RotateCollapse", 16, 160, Easing.CubicIn);
    }

    #endregion

    #region Panel Population

    private void PopulateStatusPanel(Grid panel, bool manga)
    {
        panel.RowDefinitions.Clear();
        panel.Children.Clear();

        var statusValues = new[] { AnimeStatus.Watching, AnimeStatus.Completed, AnimeStatus.OnHold, AnimeStatus.Dropped, AnimeStatus.PlanToWatch };
        var items = new List<(string Label, Action OnClick)>();

        for (int i = 0; i < statusValues.Length; i++)
        {
            var status = statusValues[i];
            var index = i;
            var workMode = manga ? AnimeListWorkModes.Manga : AnimeListWorkModes.Anime;
            var label = Utilities.StatusToString((int)status, manga);
            items.Add((label, (Action)(() => NavigateTo(PageIndex.PageAnimeList, new AnimeListPageNavigationArgs(index, workMode)))));
        }

        PopulateTypePanel(panel, items);
    }

    private void PopulateTypePanel(Grid panel, List<(string Label, Action OnClick)> items)
    {
        panel.RowDefinitions.Clear();
        panel.Children.Clear();

        // Each item gets its own row; dividers occupy separate rows so they don't paint
        // over the border of the next item.
        for (int i = 0; i < items.Count; i++)
        {
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
        for (int i = 0; i < items.Count - 1; i++)
        {
            panel.RowDefinitions.Add(new RowDefinition { Height = 1 });
        }

        for (int i = 0; i < items.Count; i++)
        {
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var item = items[i];

            var row = new Border
            {
                BackgroundColor = Colors.Transparent,
                Stroke = (Color)Application.Current.Resources["EmBorder"],
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Margin = new Thickness(16, 0, 16, 0),
                Padding = new Thickness(16, 8)
            };

            var rowGrid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = GridLength.Star } } };
            var txt = new Label
            {
                Text = item.Label,
                FontFamily = "Inter",
                FontSize = 14,
                TextColor = (Color)Application.Current.Resources["BrushText"],
                VerticalOptions = LayoutOptions.Center,
                Padding = new Thickness(16, 0, 0, 0)
            };
            rowGrid.Add(txt, 1, 0);
            row.Content = rowGrid;

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (s, e) => item.OnClick();
            row.GestureRecognizers.Add(tapGesture);

            panel.Add(row, 0, i * 2);

            if (i < items.Count - 1)
            {
                var divider = new BoxView
                {
                    HeightRequest = 1,
                    Color = (Color)Application.Current.Resources["EmBorder"],
                    Margin = new Thickness(16, 0, 16, 0)
                };
                panel.Add(divider, 0, i * 2 + 1);
            }
        }
    }

    #endregion

    #region Navigation

    private void NavigateTo(PageIndex page, object args = null)
    {
        if (args is AnimeListPageNavigationArgs animeArgs)
        {
            animeArgs.FromMore = true;
            animeArgs.ResetBackNav = false;
        }
        ViewModelLocator.GeneralMain.Navigate(page, args);
        ViewModelLocator.NavMgr.DeregisterBackNav();
        ViewModelLocator.NavMgr.RegisterBackNav(PageIndex.PageMore, null);
    }

    #endregion

    #region State Persistence

    private void RestorePanelState()
    {
        try
        {
            var ui = MALClient.XShared.ViewModels.Main.FragmentUiState.More;
            if (ui != null)
            {
                if (!_animeListPanelExpanded && ui.TryGetValue("AnimeList", out var a) && a is bool ab && ab)
                {
                    _animeListPanelExpanded = true;
                    AnimeListPanel.IsVisible = true;
                    AnimeListArrow.Rotation = 180;
                }
                if (!_mangaListPanelExpanded && ui.TryGetValue("MangaList", out var ml) && ml is bool mlb && mlb)
                {
                    _mangaListPanelExpanded = true;
                    MangaListPanel.IsVisible = true;
                    MangaListArrow.Rotation = 180;
                }
                if (!_topAnimePanelExpanded && ui.TryGetValue("TopAnime", out var ta) && ta is bool tab && tab)
                {
                    _topAnimePanelExpanded = true;
                    TopAnimePanel.IsVisible = true;
                    TopAnimeArrow.Rotation = 180;
                }
                if (!_topMangaPanelExpanded && ui.TryGetValue("TopManga", out var tm) && tm is bool tmb && tmb)
                {
                    _topMangaPanelExpanded = true;
                    TopMangaPanel.IsVisible = true;
                    TopMangaArrow.Rotation = 180;
                }
                if (!_adaptedPanelExpanded && ui.TryGetValue("Adapted", out var ad) && ad is bool adb && adb)
                {
                    _adaptedPanelExpanded = true;
                    AdaptedPanel.IsVisible = true;
                    AdaptedArrow.Rotation = 180;
                }
            }
        }
        catch { }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        SavePanelState();
    }

    private void SavePanelState()
    {
        try
        {
            var ui = MALClient.XShared.ViewModels.Main.FragmentUiState.More;
            if (ui != null)
            {
                ui["AnimeList"] = _animeListPanelExpanded;
                ui["MangaList"] = _mangaListPanelExpanded;
                ui["TopAnime"] = _topAnimePanelExpanded;
                ui["TopManga"] = _topMangaPanelExpanded;
                ui["Adapted"] = _adaptedPanelExpanded;
            }
        }
        catch { }
    }

    #endregion
}
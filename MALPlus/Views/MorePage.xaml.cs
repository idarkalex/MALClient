using MALClient.Models.Enums;
using MALClient.XShared.Comm.Anime;
using MALClient.XShared.Comm.Profile;

using System.Collections.Generic;
using System.Linq;
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

                // The profile HTML is the only reliable source: an account with no
                // picture has no <img> in user-image, and the CDN id URL 404s.
                SetProfileImage(null);

                // Load profile data from cache
                try
                {
                    var data = await DataCache.RetrieveProfileData(Credentials.UserName);
                    if (data?.User?.ImgUrl != null)
                    {
                        SetProfileImage(data.User.ImgUrl);
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
                SetProfileImage(null);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MALPLUS MorePage LoadProfileDataAsync error: {ex.Message}");
        }
    }

    private void SetProfileImage(string url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            ProfileImage.Source = new UriImageSource
            {
                Uri = new Uri(url),
                CachingEnabled = true,
                CacheValidity = TimeSpan.FromMinutes(10)
            };
            ProfileImage.IsVisible = true;
            ProfileFallbackIcon.IsVisible = false;
        }
        else
        {
            ProfileImage.Source = null;
            ProfileImage.IsVisible = false;
            ProfileFallbackIcon.IsVisible = true;
        }
    }

    private async void RefreshProfileCacheInBackground()
    {
        try
        {
            var fresh = await new ProfileQuery(Credentials.UserName).GetProfileData(false);
            if (!string.IsNullOrEmpty(fresh?.User?.ImgUrl))
                SetProfileImage(fresh.User.ImgUrl);
            if (fresh != null && (fresh.AnimeCompleted > 0 || fresh.MangaCompleted > 0))
            {
                ProfileCompleted.Text = $"{fresh.AnimeCompleted} Completed";
                ProfileCompleted.IsVisible = true;
            }
        }
        catch (Exception) { }
    }

    #region Navigation Handlers

    private void OnAnimeListTapped(object sender, TappedEventArgs e)
    {
        TogglePanel(AnimeListPanel, AnimeListArrow, AnimeListDivider, v => _animeListPanelExpanded = v, _animeListPanelExpanded);
    }

    private void OnMangaListTapped(object sender, TappedEventArgs e)
    {
        TogglePanel(MangaListPanel, MangaListArrow, MangaListDivider, v => _mangaListPanelExpanded = v, _mangaListPanelExpanded);
    }

    private void OnTopAnimeTapped(object sender, TappedEventArgs e)
    {
        TogglePanel(TopAnimePanel, TopAnimeArrow, TopAnimeDivider, v => _topAnimePanelExpanded = v, _topAnimePanelExpanded);
    }

    private void OnTopMangaTapped(object sender, TappedEventArgs e)
    {
        TogglePanel(TopMangaPanel, TopMangaArrow, TopMangaDivider, v => _topMangaPanelExpanded = v, _topMangaPanelExpanded);
    }

    private void OnAdaptedTapped(object sender, TappedEventArgs e)
    {
        TogglePanel(AdaptedPanel, AdaptedArrow, null, v => _adaptedPanelExpanded = v, _adaptedPanelExpanded);
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

    private void OnWallpapersTapped(object sender, TappedEventArgs e)
    {
        NavigateTo(PageIndex.PageWallpapers, null);
    }

    private void OnClubsTapped(object sender, TappedEventArgs e)
    {
        NavigateTo(PageIndex.PageClubIndex, null);
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

    private async void OnLogOutTapped(object sender, TappedEventArgs e)
    {
        try
        {
            // Fire-and-forget log out (mirrors v2 LogOutCommand flow).
            MALClient.XShared.Utils.Credentials.Reset();
            _initialized = false;
            await NavigateToLoginAsync();
        }
        catch (Exception ex) { Console.WriteLine("MALPLUS logout failed: " + ex); }
    }

    private void NavigateToLogin()
    {
        _ = NavigateToLoginAsync();
    }

    // "login" is a global route, so it must be addressed relatively. Absolute
    // routing ("//login") throws inside ShellNavigationManager and the faulted
    // task was being discarded, which left logout and every auth-gated row as
    // a silent no-op.
    private static async Task NavigateToLoginAsync()
    {
        var shell = Shell.Current;
        if (shell == null)
            return;
        try
        {
            await shell.GoToAsync("login");
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS login navigation failed: " + ex);
        }
    }

    #endregion

    #region Panel Animation

    private void TogglePanel(Grid panel, Image arrow, BoxView divider, System.Action<bool> setExpanded, bool currentExpanded)
    {
        if (currentExpanded)
        {
            AnimateCollapse(panel, arrow, divider);
            setExpanded(false);
        }
        else
        {
            AnimateExpand(panel, arrow, divider);
            setExpanded(true);
        }
    }

    private static void ApplyPanelState(Grid panel, Image arrow, BoxView divider, bool expanded)
    {
        panel.IsVisible = expanded;
        panel.Opacity = expanded ? 1 : 0;
        panel.TranslationY = 0;
        arrow.Rotation = expanded ? 180 : 0;
        if (divider != null)
            divider.IsVisible = expanded;
    }

    private void AnimateExpand(Grid panel, Image arrow, BoxView divider)
    {
        panel.IsVisible = true;
        if (divider != null)
            divider.IsVisible = true;
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

    private void AnimateCollapse(Grid panel, Image arrow, BoxView divider)
    {
        var animation = new Animation(
            d => panel.Opacity = d, 1, 0, Easing.CubicIn);
        animation.Commit(panel, "Collapse", 16, 160, Easing.CubicIn, finished: (d, b) =>
        {
            panel.IsVisible = false;
            if (divider != null)
                divider.IsVisible = false;
        });

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
        var statusValues = new[] { AnimeStatus.Watching, AnimeStatus.Completed, AnimeStatus.OnHold, AnimeStatus.Dropped, AnimeStatus.PlanToWatch };
        var workMode = manga ? AnimeListWorkModes.Manga : AnimeListWorkModes.Anime;
        var items = new List<(string Label, Action OnClick)>
        {
            ("All", () => NavigateTo(PageIndex.PageAnimeList, new AnimeListPageNavigationArgs(5, workMode)))
        };

        for (int i = 0; i < statusValues.Length; i++)
        {
            var status = statusValues[i];
            var index = i;
            var label = MALClient.XShared.Utils.Utilities.StatusToString((int)status, manga);
            items.Add((label, (Action)(() => NavigateTo(PageIndex.PageAnimeList, new AnimeListPageNavigationArgs(index, workMode)))));
        }

        PopulateTypePanel(panel, items);
    }

    private void PopulateTypePanel(Grid panel, List<(string Label, Action OnClick)> items)
    {
        panel.RowDefinitions.Clear();
        panel.Children.Clear();

        // The panels are single-column grids; each item is a 44dp row with a
        // divider underneath, indented like the reference layout.
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];

            var row = new Grid
            {
                HeightRequest = 44,
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Star }
                }
            };
            row.Add(new Label
            {
                Text = item.Label,
                FontFamily = "Inter",
                FontSize = 16,
                TextColor = (Color)Application.Current.Resources["BrushText"],
                VerticalOptions = LayoutOptions.Center
            }, 0, 0);

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (s, e) => item.OnClick();
            row.GestureRecognizers.Add(tapGesture);

            panel.Add(row, 0, i * 2);

            if (i < items.Count - 1)
            {
                panel.Add(new BoxView
                {
                    HeightRequest = 1,
                    Color = Color.FromArgb("#1AFFFFFF")
                }, 0, i * 2 + 1);
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
            if (ui == null)
                return;

            if (ui.TryGetValue("AnimeList", out var a) && a is bool ab && ab)
                _animeListPanelExpanded = true;
            if (ui.TryGetValue("MangaList", out var ml) && ml is bool mlb && mlb)
                _mangaListPanelExpanded = true;
            if (ui.TryGetValue("TopAnime", out var ta) && ta is bool tab && tab)
                _topAnimePanelExpanded = true;
            if (ui.TryGetValue("TopManga", out var tm) && tm is bool tmb && tmb)
                _topMangaPanelExpanded = true;
            if (ui.TryGetValue("Adapted", out var ad) && ad is bool adb && adb)
                _adaptedPanelExpanded = true;

            ApplyPanelState(AnimeListPanel, AnimeListArrow, AnimeListDivider, _animeListPanelExpanded);
            ApplyPanelState(MangaListPanel, MangaListArrow, MangaListDivider, _mangaListPanelExpanded);
            ApplyPanelState(TopAnimePanel, TopAnimeArrow, TopAnimeDivider, _topAnimePanelExpanded);
            ApplyPanelState(TopMangaPanel, TopMangaArrow, TopMangaDivider, _topMangaPanelExpanded);
            ApplyPanelState(AdaptedPanel, AdaptedArrow, null, _adaptedPanelExpanded);
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
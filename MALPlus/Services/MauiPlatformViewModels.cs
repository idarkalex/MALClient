using System.Windows.Input;
using MALClient.Models.Enums;
using MALClient.XShared.Comm.Anime;
using MALClient.XShared.Delegates;
using MALClient.XShared.Interfaces;
using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;

namespace MALPlus.Services;

public class MauiMainViewModel : MainViewModelBase
{
    protected override void CurrentStatusStoryboardBegin()
    {
    }

    protected override void CurrentOffSubStatusStoryboardBegin()
    {
    }

    protected override void CurrentOffStatusStoryboardBegin()
    {
    }

    public override void Navigate(PageIndex index, object args = null)
    {
        string route = null;
        string query = null;

        switch (index)
        {
            // Tab roots
            case PageIndex.PageAnimeList:
            case PageIndex.PageSeasonal:
            case PageIndex.PageTopAnime:
            case PageIndex.PageMangaList:
            case PageIndex.PageTopManga:
            case PageIndex.PageMangaAdapted:
                route = "///anime";
                query = BuildAnimeListQuery(index, args);
                break;
            case PageIndex.PageDiscover:
                route = "///discover";
                break;
            case PageIndex.PageMore:
                // The "more" tab now hosts the real MorePage hub directly.
                route = "///more";
                break;

            // Off-page (push) routes
            case PageIndex.PageAnimeDetails:
                if (args is AnimeDetailsPageNavigationArgs ad)
                    query = $"?id={ad.Id}&title={Uri.EscapeDataString(ad.Title ?? "")}";
                else if (args is int idd)
                    query = $"?id={idd}";
                route = "animedetails";
                break;
            case PageIndex.PageSearch:
            case PageIndex.PageMangaSearch:
            case PageIndex.PageCharacterSearch:
            case PageIndex.PageSearchEverywhere:
                route = "search";
                if (args is SearchPageNavigationArgs sa)
                    query = $"?query={Uri.EscapeDataString(sa.Query ?? "")}&mode={(sa.Anime ? "anime" : "manga")}";
                break;
            case PageIndex.PageCalendar:
                route = "calendar";
                break;
            case PageIndex.PageProfile:
                route = "profile";
                if (args is ProfilePageNavigationArgs pa)
                    query = $"?user={Uri.EscapeDataString(pa.TargetUser ?? "")}";
                break;
            case PageIndex.PageLogIn:
                route = "login";
                break;
            case PageIndex.PageSettings:
                route = "settings";
                query = "?name=Settings";
                break;
            case PageIndex.PageRecomendations:
                route = "recommendations";
                query = "?name=Recommendations";
                break;
            case PageIndex.PageArticles:
            case PageIndex.PageNews:
                route = "articles";
                query = "?name=Articles & News";
                break;
            case PageIndex.PagePopularVideos:
                route = "videos";
                query = "?name=Promotional Videos";
                break;
            case PageIndex.PageForumIndex:
                route = "forums";
                query = "?name=Forums";
                break;
            case PageIndex.PageHistory:
                route = "history";
                query = "?name=History";
                break;
            case PageIndex.PageFeeds:
                route = "feeds";
                query = "?name=Friends Feeds";
                break;
            case PageIndex.PageFriends:
                route = "friends";
                query = "?name=Friends";
                break;
            case PageIndex.PageWallpapers:
                route = "wallpapers";
                query = "?name=Wallpapers";
                break;
            case PageIndex.PageMessanging:
                route = "messaging";
                query = "?name=Messaging";
                break;
            case PageIndex.PageClubIndex:
                route = "clubs";
                query = "?name=Clubs";
                break;
            case PageIndex.PageListComparison:
                route = "listcomparison";
                query = "?name=List Comparison";
                break;
            case PageIndex.PageNotificationHub:
                route = "notifications";
                query = "?name=Notifications";
                break;
            case PageIndex.PageCharacterDetails:
                route = "character";
                query = "?name=Character Details";
                break;
            case PageIndex.PageStaffDetails:
                route = "staff";
                query = "?name=Staff Details";
                break;
            // PageAbout and PageMessageDetails: leave as no-op for now (settings/about opens via Settings page)
        }

        if (string.IsNullOrEmpty(route))
            return;

        var final = string.IsNullOrEmpty(query) ? route : route + query;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                if (Shell.Current != null)
                    await Shell.Current.GoToAsync(final);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MALPLUS Navigate failed index={index} route={final}: {ex.GetType().Name} {ex.Message}");
            }
        });
    }

    private static string BuildAnimeListQuery(PageIndex index, object args)
    {
        // Anime list is a parameterised page (work mode, status, type).
        // Args may be AnimeListPageNavigationArgs (rich) or null.
        var sb = new System.Text.StringBuilder("?");
        if (args is AnimeListPageNavigationArgs a)
        {
            sb.Append("mode=").Append((int)a.WorkMode);
            if (a.Status > 0 || (a.StatusIndex.HasValue && a.StatusIndex.Value > 0))
            {
                sb.Append("&status=").Append(a.StatusIndex ?? a.Status);
            }
        }
        else
        {
            int mode = index switch
            {
                PageIndex.PageSeasonal => 1, // SeasonalAnime
                PageIndex.PageTopAnime => 3, // TopAnime
                PageIndex.PageMangaList => 2, // Manga
                PageIndex.PageTopManga => 4, // TopManga
                PageIndex.PageMangaAdapted => 8, // MangaAdapted
                _ => 0,
            };
            sb.Append("mode=").Append(mode);
        }
        return sb.ToString();
    }
}

public class MauiHamburgerViewModel : IHamburgerViewModel
{
    public Task UpdateProfileImg(bool dl = true)
    {
        return Task.CompletedTask;
    }

    public void SetActiveButton(HamburgerButtons val)
    {
    }

    public void UpdateApiDependentButtons()
    {
    }

    public void UpdateAnimeFiltersSelectedIndex()
    {
    }

    public void UpdateLogInLabel()
    {
    }

    public bool MangaSectionVisbility { get; set; }

    public void SetActiveButton(TopAnimeType topType)
    {
    }

    public void UpdatePinnedProfiles()
    {
    }

    public void UpdateBottomMargin()
    {
    }
}

public class MauiCssManager : MALClient.XShared.Utils.CssManagerBase
{
    protected override string AccentColour => "#0066FF";
    protected override string AccentColourLight => "#4D94FF";
    protected override string AccentColourDark => "#0047B3";
    protected override string NotifyFunction => "function notify(msg){}";
    protected override string ShadowsDefinition => "";
}

public class MauiSettingsViewModel : MALClient.XShared.ViewModels.SettingsViewModelBase
{
    public MauiSettingsViewModel() : base() { }

    public override event SettingsNavigationRequest NavigationRequest
    {
        add { }
        remove { }
    }

    public override ICommand ReviewCommand => null;
    public override ICommand RequestNavigationCommand => null;

    public override void LoadCachedEntries()
    {
        // MAUI version: no-op (TotalFilesCached comes from base)
        System.Diagnostics.Debug.WriteLine("MauiSettingsViewModel.LoadCachedEntries (no-op)");
    }
}

using MALClient.Models.Enums;
using MALClient.XShared.Comm.Anime;
using MALClient.XShared.Interfaces;
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
        var route = index switch
        {
            PageIndex.PageAnimeList => "///anime",
            PageIndex.PageMangaList => "///manga",
            PageIndex.PageDiscover => "///discover",
            PageIndex.PageMore => "///more",
            _ => null,
        };
        if (route == null)
            return;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (Shell.Current != null)
                await Shell.Current.GoToAsync(route);
        });
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

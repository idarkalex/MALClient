using System.Windows.Input;
using MALClient.Models.Enums;
using MALClient.XShared.Interfaces;
using MALClient.XShared.NavArgs;

namespace MALPlus.Services;

public class MauiNavMgr : INavMgr
{
    private readonly Stack<Tuple<PageIndex, object>> _mainStack = new();
    private readonly Stack<Tuple<PageIndex, object>> _offStack = new();
    private ICommand _oneTimeOverride;
    private ICommand _oneTimeMainOverride;

    public void RegisterBackNav(PageIndex page, object args, PageIndex source = PageIndex.PageAbout)
    {
        _mainStack.Push(Tuple.Create(page, args));
    }

    public void RegisterOneTimeOverride(ICommand command)
    {
        _oneTimeOverride = command;
    }

    public void DeregisterBackNav()
    {
        _mainStack.Clear();
    }

    public void ResetOffBackNav()
    {
        _offStack.Clear();
    }

    public bool HasSomethingOnStack()
    {
        return _mainStack.Count > 0;
    }

    public Tuple<PageIndex, object> PeekMainBackNav()
    {
        return _mainStack.Count > 0 ? _mainStack.Peek() : null;
    }

    public void RegisterBackNav(ProfilePageNavigationArgs args)
    {
        _mainStack.Push(Tuple.Create(PageIndex.PageProfile, (object)args));
    }

    public void CurrentMainViewOnBackRequested()
    {
        if (_oneTimeMainOverride != null)
        {
            var cmd = _oneTimeMainOverride;
            _oneTimeMainOverride = null;
            if (cmd.CanExecute(null))
                cmd.Execute(null);
            return;
        }
        if (_oneTimeOverride != null)
        {
            var cmd = _oneTimeOverride;
            _oneTimeOverride = null;
            if (cmd.CanExecute(null))
                cmd.Execute(null);
            return;
        }
        if (_mainStack.Count > 0)
            _mainStack.Pop();
    }

    public void CurrentOffViewOnBackRequested()
    {
        if (_offStack.Count > 0)
            _offStack.Pop();
    }

    public void ResetMainBackNav()
    {
        _mainStack.Clear();
        _oneTimeMainOverride = null;
        _oneTimeOverride = null;
    }

    public void RegisterBackNav(AnimeDetailsPageNavigationArgs args)
    {
        _mainStack.Push(Tuple.Create(PageIndex.PageAnimeDetails, (object)args));
    }

    public void RegisterUnmonitoredMainBackNav(PageIndex page, object args)
    {
        _mainStack.Push(Tuple.Create(page, args));
    }

    public void RegisterOneTimeMainOverride(ICommand command)
    {
        _oneTimeMainOverride = command;
    }

    public void ResetOneTimeOverride()
    {
        _oneTimeOverride = null;
    }

    public void ResetOneTimeMainOverride()
    {
        _oneTimeMainOverride = null;
    }
}

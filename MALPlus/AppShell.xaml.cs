using MALPlus.Views;

namespace MALPlus;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("animedetails", typeof(AnimeDetailsPage));
        Routing.RegisterRoute("login", typeof(LogInPage));
        Routing.RegisterRoute("search", typeof(SearchPage));
        Routing.RegisterRoute("calendar", typeof(CalendarPage));
        Routing.RegisterRoute("profile", typeof(ProfilePage));
        Routing.RegisterRoute("more", typeof(MorePage));
    }
}

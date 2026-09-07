using MALPlus.Views;

namespace MALPlus;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Push routes
        Routing.RegisterRoute("animedetails", typeof(AnimeDetailsPage));
        Routing.RegisterRoute("login", typeof(LogInPage));
        Routing.RegisterRoute("search", typeof(SearchPage));
        Routing.RegisterRoute("calendar", typeof(CalendarPage));
        Routing.RegisterRoute("profile", typeof(ProfilePage));
        Routing.RegisterRoute("morehub", typeof(MorePage));

        // All other routes are placeholders for now and will be replaced
        // as the corresponding MAUI pages are built.
        Routing.RegisterRoute("settings", typeof(ComingSoonPage));
        Routing.RegisterRoute("recommendations", typeof(ComingSoonPage));
        Routing.RegisterRoute("articles", typeof(ComingSoonPage));
        Routing.RegisterRoute("videos", typeof(ComingSoonPage));
        Routing.RegisterRoute("forums", typeof(ComingSoonPage));
        Routing.RegisterRoute("history", typeof(ComingSoonPage));
        Routing.RegisterRoute("feeds", typeof(ComingSoonPage));
        Routing.RegisterRoute("friends", typeof(ComingSoonPage));
        Routing.RegisterRoute("wallpapers", typeof(ComingSoonPage));
        Routing.RegisterRoute("messaging", typeof(ComingSoonPage));
        Routing.RegisterRoute("clubs", typeof(ComingSoonPage));
        Routing.RegisterRoute("listcomparison", typeof(ComingSoonPage));
        Routing.RegisterRoute("notifications", typeof(ComingSoonPage));
        Routing.RegisterRoute("character", typeof(CharacterDetailsPage));
        Routing.RegisterRoute("staff", typeof(StaffDetailsPage));
    }
}

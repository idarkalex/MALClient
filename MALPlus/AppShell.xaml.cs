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
        Routing.RegisterRoute("articles", typeof(ArticlesPage));
        Routing.RegisterRoute("videos", typeof(VideosPage));
        Routing.RegisterRoute("forums", typeof(ForumsIndexPage));
        Routing.RegisterRoute("forumboard", typeof(ForumsBoardPage));
        Routing.RegisterRoute("forumtopic", typeof(ForumsTopicPage));
        Routing.RegisterRoute("history", typeof(HistoryPage));
        Routing.RegisterRoute("feeds", typeof(FeedsPage));
        Routing.RegisterRoute("friends", typeof(FriendsPage));
        Routing.RegisterRoute("wallpapers", typeof(WallpapersPage));
        Routing.RegisterRoute("messaging", typeof(MessagingPage));
        Routing.RegisterRoute("messagedetails", typeof(MessageDetailsPage));
        Routing.RegisterRoute("clubs", typeof(ClubsIndexPage));
        Routing.RegisterRoute("clubdetails", typeof(ClubDetailsPage));
        Routing.RegisterRoute("listcomparison", typeof(ListComparisonPage));
        Routing.RegisterRoute("notifications", typeof(NotificationsHubPage));
        Routing.RegisterRoute("character", typeof(CharacterDetailsPage));
        Routing.RegisterRoute("staff", typeof(StaffDetailsPage));
    }
}

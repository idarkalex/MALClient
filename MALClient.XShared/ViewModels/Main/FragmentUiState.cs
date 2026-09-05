using System.Collections.Generic;

namespace MALClient.XShared.ViewModels.Main
{
    /// <summary>
    /// Holds UI state for pages that don't have a dedicated ViewModel.
    /// Keys are page-specific (e.g. "Discover.ScrollY", "More.AnimeListPanel").
    /// </summary>
    public static class FragmentUiState
    {
        public static readonly Dictionary<string, object> Discover = new Dictionary<string, object>();
        public static readonly Dictionary<string, object> More = new Dictionary<string, object>();
        public static readonly Dictionary<string, object> Recommendations = new Dictionary<string, object>();
        public static readonly Dictionary<string, object> Articles = new Dictionary<string, object>();
        public static readonly Dictionary<string, object> Wallpapers = new Dictionary<string, object>();
        public static readonly Dictionary<string, object> PromoVideos = new Dictionary<string, object>();
        public static readonly Dictionary<string, object> Feeds = new Dictionary<string, object>();
        public static readonly Dictionary<string, object> NotifHub = new Dictionary<string, object>();
        public static readonly Dictionary<string, object> History = new Dictionary<string, object>();
        public static readonly Dictionary<string, object> Messaging = new Dictionary<string, object>();
        public static readonly Dictionary<string, object> Friends = new Dictionary<string, object>();
        public static readonly Dictionary<string, object> CharacterDetails = new Dictionary<string, object>();
        public static readonly Dictionary<string, object> StaffDetails = new Dictionary<string, object>();
        public static readonly Dictionary<string, object> FriendsFeeds = new Dictionary<string, object>();
        public static readonly Dictionary<string, object> PopularVideos = new Dictionary<string, object>();
        public static readonly Dictionary<string, object> ClubsIndex = new Dictionary<string, object>();
        public static readonly Dictionary<string, object> ClubDetails = new Dictionary<string, object>();
        public static readonly Dictionary<string, object> ForumIndex = new Dictionary<string, object>();
        public static readonly Dictionary<string, object> ForumBoard = new Dictionary<string, object>();
        public static readonly Dictionary<string, object> ForumTopic = new Dictionary<string, object>();
        public static readonly Dictionary<string, object> AnimeDetails = new Dictionary<string, object>();
        public static readonly Dictionary<string, object> Profile = new Dictionary<string, object>();
        public static readonly Dictionary<string, object> MessagingDetails = new Dictionary<string, object>();
        public static readonly Dictionary<string, object> ListComparison = new Dictionary<string, object>();
        public static readonly Dictionary<string, object> Settings = new Dictionary<string, object>();
        public static readonly Dictionary<string, object> LogIn = new Dictionary<string, object>();
    }
}

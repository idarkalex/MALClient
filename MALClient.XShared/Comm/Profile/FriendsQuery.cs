using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using MALClient.Models.Models;
using MALClient.Models.Models.MalSpecific;
using MALClient.XShared.Utils;

namespace MALClient.XShared.Comm.Profile
{
    public class FriendsQuery : Query
    {
        private readonly string _userName;

        public FriendsQuery(string userName)
        {
            _userName = userName;
        }

        public async Task<List<MalFriend>> GetFriends()
        {
            var output = new List<MalFriend>();

            try
            {
                var (items, _) = await TenraiClient.GetPaginatedAsync(
                    $"users/{Uri.EscapeDataString(_userName)}/friends");

                foreach (var friend in items)
                {
                    var user = friend.GetProperty("user");
                    output.Add(new MalFriend
                    {
                        Id = GetString(user, "url"),
                        User = new MalUser
                        {
                            ImgUrl = GetNestedString(user, "images", "jpg", "image_url"),
                            Name = GetString(user, "username"),
                        },
                        FriendsSince = GetDateString(friend, "friends_since"),
                        LastOnline = GetDateString(user, "last_online"),
                    });
                }
            }
            catch (Exception)
            {
                // fallback to HTML
            }

            return output;
        }

        private static string GetString(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : "";

        private static string GetNestedString(JsonElement el, params string[] props)
        {
            foreach (var prop in props.Take(props.Length - 1))
                if (!el.TryGetProperty(prop, out el)) return "";
            return GetString(el, props.Last());
        }

        private static string GetDateString(JsonElement el, string prop)
        {
            if (!el.TryGetProperty(prop, out var p) || p.ValueKind != JsonValueKind.String)
                return "N/A";
            var val = p.GetString();
            if (DateTime.TryParse(val, out var dt))
                return dt.ToString("yyyy-MM-dd");
            return val;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
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
            Request =
                WebRequest.Create(Uri.EscapeUriString($"https://myanimelist.net/profile/{userName}/friends"));
            Request.ContentType = "application/x-www-form-urlencoded";
            Request.Method = "GET";
        }

        public async Task<List<MalFriend>> GetFriends()
        {
            var output = new List<MalFriend>();

            var raw = await GetRequestResponse();
            if (string.IsNullOrEmpty(raw))
                return output;
            var doc = new HtmlDocument();
            doc.LoadHtml(raw);

            try
            {
                foreach (var entry in doc.WhereOfDescendantsWithPartialClass("div", "boxlist"))
                {
                    var classAttr = entry.Attributes["class"]?.Value ?? "";
                    if (classAttr.Contains("boxlist-container"))
                        continue;

                    try
                    {
                        var current = new MalFriend();

                        var img = entry.Descendants("img").FirstOrDefault();
                        if (img != null)
                            current.User.ImgUrl = StripImageVariant(img.Attributes["data-src"]?.Value ?? img.Attributes["src"]?.Value);

                        var titleAnchor = entry.FirstOrDefaultOfDescendantsWithClass("div", "title")?.Descendants("a").FirstOrDefault();
                        var profileHref = titleAnchor?.Attributes["href"]?.Value ?? entry.Descendants("a").FirstOrDefault()?.Attributes["href"]?.Value;
                        if (!string.IsNullOrEmpty(profileHref))
                        {
                            var segments = profileHref.Split('/');
                            var index = Array.LastIndexOf(segments, "profile");
                            if (index >= 0 && index + 1 < segments.Length)
                                current.Id = segments[index + 1];
                        }

                        current.User.Name = WebUtility.HtmlDecode(
                            titleAnchor?.InnerText.Trim()
                            ?? img?.Attributes["alt"]?.Value
                            ?? "");

                        var metaDivs = entry.Descendants("div")
                            .Where(node => (node.Attributes["class"]?.Value ?? "").Contains("fn-grey2"))
                            .ToList();

                        if (metaDivs.Count > 0)
                            current.LastOnline = WebUtility.HtmlDecode(metaDivs[0].InnerText.Trim());
                        if (metaDivs.Count > 1)
                        {
                            var since = WebUtility.HtmlDecode(metaDivs[1].InnerText.Trim());
                            const string prefix = "Friends since";
                            current.FriendsSince = since.StartsWith(prefix)
                                ? since.Substring(prefix.Length).Trim()
                                : since;
                        }

                        if (!string.IsNullOrEmpty(current.User.Name))
                            output.Add(current);
                    }
                    catch (Exception)
                    {
                        //
                    }
                }
            }
            catch (Exception)
            {
                //
            }

            return output;
        }

        private static string StripImageVariant(string url)
        {
            if (string.IsNullOrEmpty(url))
                return url;
            var queryIndex = url.IndexOf('?');
            return queryIndex > 0 ? url.Substring(0, queryIndex) : url;
        }
    }
}

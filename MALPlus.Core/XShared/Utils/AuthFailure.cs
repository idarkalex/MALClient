using System;
using System.Net.Http;

namespace MALClient.XShared.Utils
{
    /// <summary>
    ///     Tells a genuine credential rejection apart from a transient failure.
    ///     Hummingbird and the MAL website answer 401/403 for a dead token, but the same code
    ///     paths also throw plain timeouts, 429s and deserialisation errors. Telling the user
    ///     to "sign in again" for those makes a perfectly valid session look broken, and the user
    ///     loses the list for no reason.
    /// </summary>
    public static class AuthFailure
    {
        public static bool LooksLikeAuthFailure(Exception error)
        {
            for (var current = error; current != null; current = current.InnerException)
            {
                if (current is HttpRequestException http)
                {
                    var code = http.StatusCode;
                    if (code.HasValue && ((int) code == 401 || (int) code == 403))
                        return true;
                }
                if (current is UnauthorizedAccessException)
                    return true;

                var text = current.Message ?? string.Empty;
                if (Contains(text, "401") ||
                    Contains(text, "403") ||
                    Contains(text, "unauthorized") ||
                    Contains(text, "forbidden") ||
                    Contains(text, "invalid_grant") ||
                    Contains(text, "invalid_token") ||
                    Contains(text, "access denied"))
                    return true;
            }
            return false;
        }

        private static bool Contains(string haystack, string needle) =>
            haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

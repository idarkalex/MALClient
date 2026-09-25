using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MALClient.Models.Enums;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;

namespace MALClient.XShared.Comm
{
    public abstract class Query
    {

        protected Uri Request;
        private bool _retry = true;
        public static ApiType CurrentApiType { get; set; } = Settings.SelectedApiType;

        protected static HttpClient _client;

        /// <summary>
        ///     Cookie-free client for pages MAL server-renders to signed-out
        ///     visitors. Note: the forum board still comes back as the JS-only
        ///     shell for this client (same length with and without cookies, and
        ///     with a browser User-Agent), so the session is NOT the cause there.
        /// </summary>
        protected static readonly HttpClient _anonymousClient = CreateAnonymousClient();

        private static HttpClient CreateAnonymousClient()
        {
            var client = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = true,
                UseCookies = false
            })
            {
                Timeout = TimeSpan.FromSeconds(100)
            };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Linux; Android 13) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Mobile Safari/537.36");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
            return client;
        }

        /// <summary>
        ///     Fetches a public MAL page without the session cookies.
        /// </summary>
        protected static async Task<string> GetAnonymousRequestResponse(Uri request)        {
            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Get, request);
                var res = await _anonymousClient.SendAsync(message);
                var content = await res.Content.ReadAsStringAsync();
                if (res.IsSuccessStatusCode)
                    ResourceLocator.ConnectionInfoProvider.HasInternetConnection = true;
                return content;
            }
            catch (Exception)
            {
                ResourceLocator.ConnectionInfoProvider.HasInternetConnection = false;
            }
            return null;
        }

        static Query()
        {
            _client = new HttpClient(ResourceLocator.MalHttpContextProvider.GetHandler());
            // Several sources reject requests without a browser User-Agent outright.
            _client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Linux; Android 13) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Mobile Safari/537.36");
            RefreshClientAuthHeader();
        }

        public static void RefreshClientAuthHeader()
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Credentials.UserName}:{Credentials.Password}")));
        }


        public virtual async Task<string> GetRequestResponse()
        {
            try
            {
                var res = await _client.GetAsync(Request);
                // Only MAL's own anti-bot page deserves this dialog: other sources
                // (reddit for wallpapers, ANN) 403 on their own and repeating the
                // MAL message there just spams modals over an unreachable host.
                if (res.StatusCode == HttpStatusCode.Forbidden
                    && Request.Host.EndsWith("myanimelist.net", StringComparison.OrdinalIgnoreCase)
                    && !Request.ToString()
                        .Contains("https://myanimelist.net/rss.php?type=rw&u=")) //workaround because I don't want to disturb the spaghetti gods sleeping around
                {
                    HandleMalBuggines();
                }

                await Task.Delay(150);
                var content = await res.Content.ReadAsStringAsync();
                ResourceLocator.ConnectionInfoProvider.HasInternetConnection = true;
                return content;
            }
            catch (Exception)
            {
                ResourceLocator.ConnectionInfoProvider.HasInternetConnection = false;

                if (Credentials.Authenticated)
                    ResourceLocator.SnackbarProvider.ShowText(SnackbarMessageOnFail);
            }
            return null;
        }

        public virtual string SnackbarMessageOnFail => "Operation failed, check your internet connection...";

        private static readonly SemaphoreSlim _buggedMalMessageSemaphore = new SemaphoreSlim(1);
        private async void HandleMalBuggines()
        {
            ResourceLocator.DispatcherAdapter.Run(async () =>
            {
                await _buggedMalMessageSemaphore.WaitAsync();
                try
                {
                    await ResourceLocator.MessageDialogProvider.ShowMessageDialogAsync(
                        "There was an error connecting to MAL Api, it tends to behave in unpredictable ways unfortunately and there's nothing I can do about it. Please try again later.", "Whoops!");
                }
                finally
                {
                    _buggedMalMessageSemaphore.Release();
                }
            });
            //Couldn't handle it :(
        }
    }
}
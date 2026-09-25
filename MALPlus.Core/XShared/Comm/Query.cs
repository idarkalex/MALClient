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

        static Query()
        {
            _client = new HttpClient(ResourceLocator.MalHttpContextProvider.GetHandler());
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
                if (res.StatusCode == HttpStatusCode.Forbidden && !Request.ToString()
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
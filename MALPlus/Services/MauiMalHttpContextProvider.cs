using System.Net;
using System.Net.Http;
using MALClient.XShared.Comm.MagicalRawQueries;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;

namespace MALPlus.Services;

public class MauiMalHttpContextProvider : MALClient.XShared.BL.MalHttpContextProviderBase
{
    public override HttpClientHandler GetHandler()
    {
        return new HttpClientHandler();
    }

    protected override async Task<CsrfHttpClient> ObtainContext()
    {
        var httpHandler = ResourceLocator.MalHttpContextProvider.GetHandler();
        _httpClient = new CsrfHttpClient(httpHandler) { BaseAddress = new Uri(MalBaseUrl) };

        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("authority", "myanimelist.net");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Host", "myanimelist.net");
        _httpClient.DefaultRequestHeaders.Add("X-Requested-With", new[] { "XMLHttpRequest" });
        _httpClient.Handler.CookieContainer.Add(new Cookie("anime_update_advanced", "0", "/", "myanimelist.net"));

        var existingCookies = Credentials.Password;
        var success = false;

        if (!string.IsNullOrEmpty(existingCookies))
        {
            SetCookies(existingCookies);

            try
            {
                var req = await _httpClient.GetAsync("https://myanimelist.net/editprofile.php?go=myoptions");

                req.EnsureSuccessStatusCode();
                success = true;
            }
            catch (Exception e)
            {
                success = false;
            }
        }

        if (!success)
        {
            ResourceLocator.DispatcherAdapter.Run(async () =>
            {
                await ResourceLocator.MessageDialogProvider.ShowMessageDialogAsync(
                    "Failed to sign in. Please sign in again, MAL seems to have invalidated your current session :(",
                    "Error.");
            });

            throw new WebException($"Unable to authorize,");
        }

        await _httpClient.GetToken();

        return _httpClient;
    }
}

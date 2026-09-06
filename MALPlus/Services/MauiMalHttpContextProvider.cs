using System.Net.Http;
using MALClient.XShared.Comm.MagicalRawQueries;
using MALClient.XShared.Interfaces;

namespace MALPlus.Services;

public class MauiMalHttpContextProvider : IMalHttpContextProvider
{
    public void ErrorMessage(string what)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (Shell.Current != null)
                await Shell.Current.DisplayAlert("MAL+", what ?? string.Empty, "OK");
        });
    }

    public Task<CsrfHttpClient> GetHttpContextAsync(bool skipAuthCheck = false)
    {
        return Task.FromResult(new CsrfHttpClient(new HttpClientHandler()));
    }

    public void Invalidate()
    {
    }

    public HttpClientHandler GetHandler()
    {
        return new HttpClientHandler();
    }

    public void SetCookies(string cookies)
    {
    }

    public Task<HttpClient> GetApiHttpContextAsync()
    {
        return Task.FromResult(new HttpClient());
    }
}

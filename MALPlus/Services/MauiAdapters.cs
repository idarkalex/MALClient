using System.Text.Json;
using MALClient.Adapters;
using MALClient.Adapters.Credentials;
using MALClient.Models.AdapterModels;
using MALClient.Models.Enums;
using MALClient.Models.Models.Notifications;

namespace MALPlus.Services;

public class MauiDispatcherAdapter : IDispatcherAdapter
{
    public void Run(Action action)
    {
        MainThread.BeginInvokeOnMainThread(action);
    }
}

public class MauiApplicationDataService : IApplicationDataService
{
    public object this[string key]
    {
        get
        {
            if (!Preferences.ContainsKey(key))
                return null;
            var raw = Preferences.Get(key, (string)null);
            if (raw == null)
                return null;
            var sep = raw.IndexOf(':');
            if (sep > 0 && int.TryParse(raw.Substring(0, sep), out var typeCode))
            {
                var payload = raw.Substring(sep + 1);
                switch ((TypeCode)typeCode)
                {
                    case TypeCode.Boolean when bool.TryParse(payload, out var b):
                        return b;
                    case TypeCode.Int32 when int.TryParse(payload, out var i):
                        return i;
                    case TypeCode.Int64 when long.TryParse(payload, out var l):
                        return l;
                    case TypeCode.String:
                        return payload;
                }
            }
            if (bool.TryParse(raw, out var legacyBool))
                return legacyBool;
            if (int.TryParse(raw, out var legacyInt))
                return legacyInt;
            if (long.TryParse(raw, out var legacyLong))
                return legacyLong;
            return raw;
        }
        set
        {
            if (value == null)
            {
                Preferences.Remove(key);
                return;
            }
            Preferences.Set(key, (int)Type.GetTypeCode(value.GetType()) + ":" + value);
        }
    }

    public object this[RoamingDataTypes key]
    {
        get => this[key.ToString()];
        set => this[key.ToString()] = value;
    }
}

public class MauiClipboardProvider : IClipboardProvider
{
    public void SetText(string text)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Clipboard.Default.SetTextAsync(text ?? string.Empty);
        });
    }
}

public class MauiConnectionInfoProvider : IConnectionInfoProvider
{
    public void Init()
    {
        HasInternetConnection = Connectivity.NetworkAccess != NetworkAccess.None;
    }

    public bool HasInternetConnection { get; set; }
}

public class MauiShareProvider : IShareProvider
{
    public void Share(string message)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Microsoft.Maui.ApplicationModel.DataTransfer.Share.Default.RequestAsync(new ShareTextRequest { Text = message ?? string.Empty });
        });
    }
}

public class MauiMessageDialogProvider : IMessageDialogProvider
{
    public void ShowMessageDialog(string content, string title)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (Shell.Current != null)
                await Shell.Current.DisplayAlert(title ?? string.Empty, content ?? string.Empty, "OK");
        });
    }

    public Task ShowMessageDialogAsync(string content, string title)
    {
        if (Shell.Current == null)
            return Task.CompletedTask;
        return Shell.Current.DisplayAlert(title ?? string.Empty, content ?? string.Empty, "OK");
    }

    public void ShowMessageDialogWithInput(string content, string title, string trueCommand, string falseCommand,
        Action callbackOnTrue, Action callBackOnFalse = null)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (Shell.Current == null)
                return;
            var choice = await Shell.Current.DisplayActionSheet(title ?? string.Empty, "Cancel", null, trueCommand, falseCommand);
            if (choice == trueCommand)
                callbackOnTrue?.Invoke();
            else if (choice == falseCommand)
                callBackOnFalse?.Invoke();
        });
    }

    public void ShowLoadingPopup(string title, string content)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (Shell.Current != null)
                await Shell.Current.Navigation.PushModalAsync(new LoadingPopupPage(title, content), false);
        });
    }

    public void HideLoadingDialog()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (Shell.Current?.Navigation?.ModalStack?.LastOrDefault() is LoadingPopupPage)
                await Shell.Current.Navigation.PopModalAsync(false);
        });
    }

    public void UpdateLoadingPopup(string title, string content)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (Shell.Current?.Navigation?.ModalStack?.LastOrDefault() is LoadingPopupPage page)
                page.Update(title, content);
        });
    }
}

public class MauiSnackbarProvider : ISnackbarProvider
{
    public void ShowText(string text)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (Shell.Current != null)
                await Shell.Current.DisplayAlert("MAL+", text ?? string.Empty, "OK");
        });
    }
}

public class MauiImageDownloaderService : IImageDownloaderService
{
    public void DownloadImage(string url, string suggestedFilename, bool animeConver)
    {
        DownloadImageDefault(url, suggestedFilename, animeConver);
    }

    public void DownloadImageDefault(string url, string suggestedFilename, bool animeCover)
    {
        Task.Run(async () =>
        {
            using var client = new HttpClient();
            var bytes = await client.GetByteArrayAsync(url);
            var path = Path.Combine(FileSystem.CacheDirectory, suggestedFilename ?? "image.jpg");
            await File.WriteAllBytesAsync(path, bytes);
        });
    }
}

public class MauiPasswordVault : IPasswordVault
{
    public void Add(VaultCredential credential)
    {
        Task.Run(async () =>
        {
            await SecureStorage.Default.SetAsync(credential.Domain + "_user", credential.UserName ?? string.Empty);
            await SecureStorage.Default.SetAsync(credential.Domain + "_pass", credential.Password ?? string.Empty);
        }).Wait();
    }

    public VaultCredential Get(string domain)
    {
        var user = Task.Run(() => SecureStorage.Default.GetAsync(domain + "_user")).Result;
        var pass = Task.Run(() => SecureStorage.Default.GetAsync(domain + "_pass")).Result;
        if (user == null || pass == null)
            throw new Exception("Credential not found.");
        return new VaultCredential(domain, user, pass);
    }

    public void Reset()
    {
        SecureStorage.Default.RemoveAll();
    }
}

public class MauiTelemetryProvider : ITelemetryProvider
{
    public void Init()
    {
    }

    public void TelemetryTrackEvent(TelemetryTrackedEvents @event)
    {
    }

    public void TelemetryTrackEvent(TelemetryTrackedEvents @event, params (string Key, string Param)[] args)
    {
    }

    public void TelemetryTrackNavigation(PageIndex page)
    {
    }

    public void TelemetryTrackNavigation(ForumsPageIndex page)
    {
    }

    public void LogEvent(string @event)
    {
    }

    public void TrackExceptionWithMessage(Exception e, string message)
    {
    }

    public void TrackException(Exception e, [System.Runtime.CompilerServices.CallerMemberName] string caller = null)
    {
    }

    public void TrackExceptionWithAttachment(Exception e, string attachment = null, [System.Runtime.CompilerServices.CallerMemberName] string caller = null)
    {
    }
}

public class MauiNotificationsTaskManager : INotificationsTaskManager
{
    public void StartTask(BgTasks task)
    {
    }

    public void StopTask(BgTasks task)
    {
    }

    public void CallTask(BgTasks task)
    {
    }
}

public class MauiScheduledJobsManager : ISchdeuledJobsManger
{
    public void StartJob(ScheduledJob job, int reccurence, Action jobDefinition)
    {
    }

    public void StopJob(ScheduledJob job)
    {
    }
}

public class MauiChangeLogProvider : IChangeLogProvider
{
    public bool NewChangelog { get; set; }
    public string CurrentVersion => AppInfo.VersionString;
    public string DateWithVersion => AppInfo.VersionString;
    public List<string> Changelog => new();
}

public class MauiSystemControlsLauncher : ISystemControlsLauncherService
{
    public void LaunchUri(Uri uri)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Launcher.Default.OpenAsync(uri);
        });
    }
}

public class MauiAiringNotificationsAdapter : MALClient.XShared.Interfaces.IAiringNotificationsAdapter
{
    public void ScheduleToast(AiringShowNotificationEntry entry)
    {
    }

    public void RemoveToasts(string id)
    {
    }

    public bool AreNotificationRegistered(string id)
    {
        return false;
    }
}

public class MauiDialogsProvider : MALClient.XShared.Interfaces.IDialogsProvider
{
    public void ShowScoreDialog(MALClient.XShared.ViewModels.AnimeItemViewModel vm)
    {
    }
}

public class MauiLiveTilesManager : ILiveTilesManager
{
    public void UpdateTile(MALClient.Models.Models.Library.IAnimeData item)
    {
    }
}

public class MauiPinTileService : IPinTileService
{
    public void Load(object data)
    {
    }
}

public class MauiCalendarExportProvider : ICalendarExportProvider
{
    public void ExportToCalendar(object animeItemViewModel)
    {
    }
}

public class MauiDataCache : IDataCache
{
    private static string Root => FileSystem.AppDataDirectory;

    private static string Resolve(string filename, string folder)
    {
        var dir = string.IsNullOrEmpty(folder) ? Root : Path.Combine(Root, folder);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, filename);
    }

    public async Task SaveData<T>(T data, string filename, string targetFolder)
    {
        var json = JsonSerializer.Serialize(data);
        await File.WriteAllTextAsync(Resolve(filename, targetFolder), json);
    }

    public Task SaveDataRoaming<T>(T data, string filename)
    {
        return SaveData(data, filename, "roaming");
    }

    public async Task<T> RetrieveData<T>(string filename, string originFolder, int expiration)
    {
        var path = Resolve(filename, originFolder);
        if (!File.Exists(path))
            return default;
        if (expiration > 0 && (DateTime.UtcNow - File.GetLastWriteTimeUtc(path)).TotalSeconds > expiration)
            return default;
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<T>(json);
    }

    public Task<T> RetrieveDataRoaming<T>(string filename, int expiration)
    {
        return RetrieveData<T>(filename, "roaming", expiration);
    }

    public Task ClearApiRelatedCache()
    {
        return Task.CompletedTask;
    }

    public Task ClearAnimeListData()
    {
        return Task.CompletedTask;
    }
}

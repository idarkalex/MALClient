using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    // Settings.* is read from bound property getters (per cell, per bind), and every
    // read used to cost two JNI SharedPreferences round trips plus a few TryParse.
    // Cache the parsed values and drop the entry on write.
    private static readonly Dictionary<string, object> Cache = new();
    private static readonly Dictionary<string, bool> Missing = new();

    public object this[string key]
    {
        get
        {
            if (Cache.TryGetValue(key, out var cached))
                return cached;
            if (Missing.ContainsKey(key))
                return null;

            if (!Preferences.ContainsKey(key))
            {
                Missing[key] = true;
                return null;
            }

            var raw = Preferences.Get(key, (string)null);
            if (raw == null)
            {
                Missing[key] = true;
                return null;
            }

            var value = Parse(raw);
            Cache[key] = value;
            return value;
        }
        set
        {
            Cache.Remove(key);
            Missing.Remove(key);
            if (value == null)
            {
                Preferences.Remove(key);
                return;
            }
            Preferences.Set(key, (int)Type.GetTypeCode(value.GetType()) + ":" + value);
        }
    }

    private static object Parse(string raw)
    {
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
    private bool _subscribed;

    public void Init()
    {
        UpdateConnectionStatus();
        if (_subscribed)
            return;

        Connectivity.Current.ConnectivityChanged += (_, e) => HasInternetConnection = e.NetworkAccess != NetworkAccess.None;
        _subscribed = true;
    }

    private void UpdateConnectionStatus()
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
    private const int ReadTimeoutMs = 4000;

    private static readonly object Sync = new();
    private static readonly Dictionary<string, VaultCredential> Cache = new();
    private static bool _primed;

    public void Add(VaultCredential credential)
    {
        if (credential == null)
            return;
        lock (Sync)
            Cache[credential.Domain] = credential;
        // Deliberately not awaited with a short cap. The first SecureStorage write has to
        // create the Tink keyset in the AndroidKeyStore, which on this device takes well
        // over a second and a half, and abandoning it here is exactly why the app came back
        // logged out on the next cold start even though the tokens were on disk.
        _ = Task.Run(async () =>
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    await SecureStorage.Default.SetAsync(credential.Domain + "_user", credential.UserName ?? string.Empty);
                    await SecureStorage.Default.SetAsync(credential.Domain + "_pass", credential.Password ?? string.Empty);
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("MALPLUS vault write attempt " + attempt + " failed: " + ex.GetType().Name);
                    await Task.Delay(400);
                }
            }
        });
    }

    public VaultCredential Get(string domain)
    {
        lock (Sync)
        {
            if (Cache.TryGetValue(domain, out var cached))
                return cached;
        }

        var credential = TryReadSecureStorage(domain);
        if (credential == null)
            throw new Exception("Credential not found.");
        lock (Sync)
            Cache[domain] = credential;
        return credential;
    }

    /// <summary>
    /// SecureStorage is EncryptedSharedPreferences + Tink + AndroidKeyStore. Two ways it
    /// takes the whole app down, both seen on device after a reinstall:
    /// (1) the keystore entry is gone, so decrypt throws AEADBadTagException, and because
    ///     this runs under a WebView navigation callback the exception is unhandled and
    ///     kills the process;
    /// (2) the old code did Task.Run(...).Result, so the UI thread waited on a task that
    ///     needs the very looper it was blocking. That is the ANR: main thread parked,
    ///     zero CPU, "Waited 5000ms for MotionEvent".
    /// So: never block longer than ReadTimeoutMs, cache every read, and treat a broken
    /// keystore as "no credential" instead of a crash.
    /// </summary>
    private static VaultCredential TryReadSecureStorage(string domain)
    {
        if (!_primed)
        {
            _primed = true;
            Task.Run(() => TryReadSecureStorage("MALPlus"));
        }
        try
        {
            var read = Task.Run(() =>
            {
                var user = SecureStorage.Default.GetAsync(domain + "_user").GetAwaiter().GetResult();
                var pass = SecureStorage.Default.GetAsync(domain + "_pass").GetAwaiter().GetResult();
                return new VaultCredential(domain, user, pass);
            });
            if (!read.Wait(ReadTimeoutMs))
            {
                Console.WriteLine("MALPLUS vault read timed out for " + domain);
                return null;
            }
            var result = read.Result;
            return string.IsNullOrEmpty(result?.UserName) ? null : result;
        }
        catch (Exception ex)
        {
            // Most likely AEADBadTagException/KeyStoreException after a reinstall: the
            // stored ciphertext can never be decrypted again. Drop it so the next login
            // starts from a clean store instead of failing forever.
            Console.WriteLine("MALPLUS vault read failed for " + domain + ": " + ex.GetType().Name);
            TryResetBrokenStore();
            return null;
        }
    }

    private static void TryResetBrokenStore()
    {
        try
        {
            Task.Run(() => SecureStorage.Default.RemoveAll()).Wait(ReadTimeoutMs);
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS vault reset failed: " + ex.GetType().Name);
        }
    }

    public void Reset()
    {
        lock (Sync)
            Cache.Clear();
        _ = Task.Run(async () =>
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    SecureStorage.Default.RemoveAll();
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("MALPLUS vault reset attempt " + attempt + " failed: " + ex.GetType().Name);
                    await Task.Delay(400);
                }
            }
        });
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
    private static readonly JsonSerializerOptions CacheSerializerOptions = new();

    private static string Root => FileSystem.AppDataDirectory;
    private static readonly HashSet<string> EnsuredDirs = new();

    private static string Resolve(string filename, string folder)
    {
        var dir = string.IsNullOrEmpty(folder) ? Root : Path.Combine(Root, folder);
        // CreateDirectory is a stat+mkdir syscall, and this ran on EVERY cache read and
        // write. The folder set is tiny and fixed, so remember what we already made.
        lock (EnsuredDirs)
        {
            if (EnsuredDirs.Add(dir))
                Directory.CreateDirectory(dir);
        }
        return Path.Combine(dir, filename);
    }

    public async Task SaveData<T>(T data, string filename, string targetFolder)
    {
        var json = JsonSerializer.Serialize(data, CacheSerializerOptions);
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
        if (expiration > 0 && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > TimeSpan.FromDays(expiration))
            return default;
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<T>(json, CacheSerializerOptions);
    }

    public Task<T> RetrieveDataRoaming<T>(string filename, int expiration)
    {
        return RetrieveData<T>(filename, "roaming", expiration);
    }

    public Task ClearApiRelatedCache()
    {
        // Do NOT call MALClient.XShared.Utils.DataCache.ClearApiRelatedCache() from here.
        // That static method dispatches through the IoC, and IDataCache is registered as
        // MauiDataCache, so it re-entered this method forever and killed the process with
        // a StackOverflowException on hwuiTask1 right after a successful sign-in.
        lock (EnsuredDirs)
            EnsuredDirs.Clear();
        foreach (var dir in SafeEnumerateDirectories())
        {
            var name = Path.GetFileName(dir);
            // "roaming" is not API cache: it holds the seed import gate and the settings.
            if (name.Equals("roaming", StringComparison.OrdinalIgnoreCase))
                continue;
            TryDeleteDirectory(dir);
        }
        return Task.CompletedTask;
    }

    public Task ClearAnimeListData()
    {
        return ClearApiRelatedCache();
    }

    private static IEnumerable<string> SafeEnumerateDirectories()
    {
        try
        {
            return Directory.Exists(Root) ? Directory.EnumerateDirectories(Root).ToList() : new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static void TryDeleteDirectory(string dir)
    {
        try
        {
            Directory.Delete(dir, true);
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS cache delete failed for " + dir + ": " + ex.Message);
        }
    }
}

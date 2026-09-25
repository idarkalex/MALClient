namespace MALPlus.Services;

/// <summary>
/// An exception escaping an async void or an un-awaited task tears the process
/// down on Android with only a JavaProxyThrowable in logcat. These hooks mirror
/// the exception to logcat and to a file so the actual managed stack survives.
/// </summary>
public static class MauiCrashDiagnostics
{
    private const string Tag = "MALPLUS";
    private const string FileName = "crash_log.txt";
    private static bool _installed;

    public static void Install()
    {
        if (_installed)
            return;
        _installed = true;
        try
        {
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                Report("UNHANDLED", args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString()));
        }
        catch { }
        try
        {
            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                Report("UNOBSERVED_TASK", args.Exception);
                args.SetObserved();
            };
        }
        catch { }
    }

    public static void Report(string kind, Exception ex)
    {
        if (ex == null)
            return;
        var text = $"=== {kind} {DateTime.UtcNow:o} ==={Environment.NewLine}{ex}{Environment.NewLine}";
        try
        {
            global::Android.Util.Log.Error(Tag, text);
        }
        catch { }
        try
        {
            var path = Path.Combine(FileSystem.CacheDirectory, FileName);
            File.AppendAllText(path, text);
        }
        catch { }
    }
}

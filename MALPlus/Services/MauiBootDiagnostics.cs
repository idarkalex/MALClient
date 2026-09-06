using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Ioc;
using MALClient.XShared.ViewModels;

namespace MALPlus.Services;

public static class MauiBootDiagnostics
{
    public static void Run()
    {
        Task.Run(() =>
        {
            try
            {
                var lines = new List<string> { "MauiBootDiagnostics " + DateTime.UtcNow.ToString("o") };
                var asm = typeof(ViewModelLocator).Assembly;
                var vmTypes = asm.GetTypes()
                    .Where(t => t.IsSubclassOf(typeof(ViewModelBase)) && !t.IsAbstract)
                    .OrderBy(t => t.FullName)
                    .ToList();
                lines.Add("ViewModelBase subclasses: " + vmTypes.Count);
                foreach (var type in vmTypes)
                {
                    try
                    {
                        SimpleIoc.Default.GetInstance(type);
                        lines.Add("OK " + type.FullName);
                    }
                    catch (Exception ex)
                    {
                        lines.Add("FAIL " + type.FullName + " :: " + ex.GetType().Name + ": " + ex.Message);
                    }
                }
                try
                {
                    ViewModelLocator.NavMgr.RegisterBackNav(MALClient.Models.Enums.PageIndex.PageAnimeList, null);
                    var peek = ViewModelLocator.NavMgr.PeekMainBackNav();
                    lines.Add("NavMgr roundtrip: " + (peek != null ? peek.Item1.ToString() : "null"));
                    ViewModelLocator.NavMgr.ResetMainBackNav();
                }
                catch (Exception ex)
                {
                    lines.Add("FAIL NavMgr :: " + ex.GetType().Name + ": " + ex.Message);
                }
                File.WriteAllLines(Path.Combine(FileSystem.CacheDirectory, "maui_boot_diagnostics.txt"), lines);
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(Path.Combine(FileSystem.CacheDirectory, "maui_boot_diagnostics.txt"), "FATAL " + ex);
                }
                catch
                {
                }
            }
        });
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using GalaSoft.MvvmLight.Ioc;
using MALClient.Adapters;

namespace MALClient.Android.Adapters
{
    public class ChangelogProvider : IChangeLogProvider
    {
        static ChangelogProvider()
        {
            var context = SimpleIoc.Default.GetInstance<Activity>();
            var package = context.PackageManager.GetPackageInfo(context.PackageName, 0);
            _currentVersion = package.VersionName;
        }

        private static readonly string _currentVersion;

        public string CurrentVersion => _currentVersion;

        public static string Version => _currentVersion;

        public bool NewChangelog { get; set; }

        public string DateWithVersion => $"MALClient v{_currentVersion}";

        public List<string> Changelog => new List<string>
        {
           "Reviews are now fetched directly from the MyAnimeList website, showing the full review list in the same order as on the site.",
           "Fixed the season label in the anime details screen.",
           "Fixed genre browsing returning empty results for several genres.",
           "Improved API reliability and data freshness.",
        };
    }
}
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Android.Content;
using Android.Net;
using Android.Support.V4.Content;
using MALClient.XShared.Comm;

namespace MALClient.Android.Utilities
{
    public static class UpdateInstaller
    {
        private const string UpdateDir = "updates";
        private const string UpdateFileName = "MALClient-update.apk";

        public static async Task<bool> DownloadAndInstall(AppUpdateInfo info, Context context)
        {
            try
            {
                if (info == null || string.IsNullOrEmpty(info.DownloadUrl))
                    return false;

                var dir = new Java.IO.File(context.FilesDir, UpdateDir);
                if (!dir.Exists())
                    dir.Mkdirs();

                var target = new Java.IO.File(dir, UpdateFileName);
                using (var httpClient = new HttpClient())
                using (var response = await httpClient.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!response.IsSuccessStatusCode)
                        return false;

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(target.Path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                    {
                        await stream.CopyToAsync(fileStream);
                    }
                }

                var uri = FileProvider.GetUriForFile(context,
                    $"{context.PackageName}.fileprovider", target);

                var installIntent = new Intent(Intent.ActionView)
                    .SetDataAndType(uri, "application/vnd.android.package-archive")
                    .AddFlags(ActivityFlags.GrantReadUriPermission)
                    .AddFlags(ActivityFlags.NewTask);

                context.StartActivity(installIntent);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}

using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MALClient.XShared.Utils
{
    public static class DiagnosticsReporter
    {
        private const string WebhookUrl =
            "https://discord.com/api/webhooks/1541497557945950208/WfpkZjc-8CSpUU39iQqr7w7dEExh5BS2Y99DYrcaHzbxxmqQvh0zvyJINdbEvSCEOvSC";

        private static readonly HttpClient Client = new HttpClient();

        public static void Log(string category, string message, Exception ex = null)
        {
            Task.Run(async () =>
            {
                try
                {
                    var color = category.StartsWith("🔴") ? 15158332 :   // red
                                category.StartsWith("🟡") ? 16776960 :   // yellow
                                category.StartsWith("🔵") ? 255 :         // blue
                                category.StartsWith("🟢") ? 3066993 :    // green
                                category.StartsWith("🟣") ? 10181046 :   // purple
                                9807270;                                  // gray

                    var embed = new
                    {
                        title = category,
                        description = message + (ex != null ? $"\n```{ex}```" : ""),
                        color = color,
                        timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                    };
                    var payload = new { embeds = new[] { embed } };
                    var json = JsonConvert.SerializeObject(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    await Client.PostAsync(WebhookUrl, content);
                }
                catch (Exception)
                {
                }
            });
        }

        public static void Error(string category, string message, Exception ex = null)
        {
            Log("🔴 " + category, message, ex);
        }

        public static void Warn(string category, string message)
        {
            Log("🟡 " + category, message);
        }

        public static void Info(string category, string message)
        {
            Log("🔵 " + category, message);
        }

        public static void Success(string category, string message)
        {
            Log("🟢 " + category, message);
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MALClient.XShared.Utils
{
    public static class DiagnosticsReporter
    {
        private const string WebhookUrl =
            "https://discord.com/api/webhooks/1541497557945950208/WfpkZjc-8CSpUU39iQqr7w7dEExh5BS2Y99DYrcaHzbxxmqQvh0zvyJINdbEvSCEOvSC";

        private const int MaxQueue = 40;
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        private static readonly BlockingCollection<string> Queue = new BlockingCollection<string>(MaxQueue);

        static DiagnosticsReporter()
        {
            // ONE drainer for every call site. Previously each Log() spun up its own
            // Task.Run + JSON serialize + HTTPS POST, so a single details open could
            // fire dozens of concurrent webhooks and starve the thread pool.
            var drainer = new Thread( Drain)
            {
                IsBackground = true,
                Name = "MALPlus diagnostics"
            };
            drainer.Start();
        }

        public static void Log(string category, string message, Exception ex = null)
        {
            var color = category.StartsWith("🔴") ? 15158332 :   // red
                        category.StartsWith("🟡") ? 16776960 :   // yellow
                        category.StartsWith("🔵") ? 255 :         // blue
                        category.StartsWith("🟢") ? 3066993 :    // green
                        category.StartsWith("🟣") ? 10181046 :   // purple
                        9807270;                                  // gray

            string payload;
            try
            {
                var embed = new
                {
                    title = category,
                    description = message + (ex != null ? $"\n```{ex}```" : ""),
                    color = color,
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                };
                payload = JsonConvert.SerializeObject(new { embeds = new[] { embed } });
            }
            catch (Exception)
            {
                return;
            }

            // Drop rather than block the caller: diagnostics must never be the reason
            // a page is slow, and a full queue means we are far past the useful rate.
            try
            {
                Queue.TryAdd(payload);
            }
            catch (Exception)
            {
            }
        }

        private static void Drain()
        {
            foreach (var payload in Queue.GetConsumingEnumerable())
            {
                try
                {
                    using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                    using var response = Client.PostAsync(WebhookUrl, content).GetAwaiter().GetResult();
                }
                catch (Exception)
                {
                }
            }
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

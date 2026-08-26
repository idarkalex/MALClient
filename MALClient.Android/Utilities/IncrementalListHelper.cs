using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.OS;

namespace MALClient.Android.Utilities
{
    /// <summary>
    /// Drains a fully loaded source list into a target ObservableCollection in small
    /// batches on the UI thread, so long tabs (characters/staff/reviews) render
    /// progressively instead of binding hundreds of viewholders at once.
    /// </summary>
    public static class IncrementalListHelper
    {
        private static readonly Handler MainHandler = new Handler(Looper.MainLooper);

        public static CancellationTokenSource Drain<T>(IEnumerable<T> source, ICollection<T> target,
            int batch = 32, int delayMs = 120)
        {
            var cts = new CancellationTokenSource();
            var ct = cts.Token;
            var snapshot = source.ToList();
            Task.Run(async () =>
            {
                try
                {
                    var index = 0;
                    while (index < snapshot.Count && !ct.IsCancellationRequested)
                    {
                        var count = Math.Min(batch, snapshot.Count - index);
                        var slice = snapshot.GetRange(index, count);
                        index += count;
                        MainHandler.Post(() =>
                        {
                            if (ct.IsCancellationRequested)
                                return;
                            foreach (var item in slice)
                                target.Add(item);
                        });
                        await Task.Delay(delayMs, ct).ContinueWith(_ => { });
                    }
                }
                catch
                {
                    // cancelled or torn down mid-drain
                }
            });
            return cts;
        }
    }
}

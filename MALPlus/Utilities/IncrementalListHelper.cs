using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MALPlus.Utilities;

public static class IncrementalListHelper
{
    public static CancellationTokenSource Drain<TSource, TTarget>(
        IEnumerable<TSource> source,
        ObservableCollection<TTarget> target,
        Func<TSource, TTarget> selector,
        int batchSize = 20,
        int delayMs = 50)
    {
        var cts = new CancellationTokenSource();
        var sourceList = source.ToList();

        Task.Run(async () =>
        {
            for (int i = 0; i < sourceList.Count; i += batchSize)
            {
                if (cts.Token.IsCancellationRequested)
                    break;

                var batch = sourceList.Skip(i).Take(batchSize);
                foreach (var item in batch)
                {
                    target.Add(selector(item));
                }

                try
                {
                    await Task.Delay(delayMs, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, cts.Token);

        return cts;
    }
}
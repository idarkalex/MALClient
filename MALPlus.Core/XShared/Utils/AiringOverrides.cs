using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MALClient.Adapters;
using MALClient.XShared.ViewModels;

namespace MALClient.XShared.Utils
{
    //Series that the source still advertises with a weekly broadcast slot but that actually
    //moved to a seasonal / batch release model. The source never publishes a next-air date for
    //them ("Oct 20, 1999 to ?"), so the weekly slot estimate would be an invented date.
    //Flagged by hand. Each entry drops itself as soon as a real episode date proves the show
    //is airing again, so nothing has to be undone manually when the next cour starts.
    public static class AiringOverrides
    {
        //v3: v1 was cleared by seeded episode data and v2 by MAL backfilling a row dated weeks in
        //the past. Both were written before the recency rule existed, so the entries they left
        //behind have to be dropped by moving the key again.
        private const string ResumedStorageKey = "AiringOverridesResumedIdsV3";

        //malId -> newest real episode known when the override was created
        private static readonly Dictionary<int, DateTime> Overrides = new Dictionary<int, DateTime>
        {
            { 21, new DateTime(2026, 8, 16) }, //One Piece - last real episode, now on a cour model
        };

        private static readonly object Gate = new object();

        //Roughly two missed weekly slots. Long enough to survive a delayed episode, short enough
        //that a between-cours gap does not count as "airing again".
        private static readonly TimeSpan ResumeWindow = TimeSpan.FromDays(14);

        private static bool _loaded;
        private static HashSet<int> _resumed = new HashSet<int>();

        private static IApplicationDataService DataStore
        {
            get
            {
                try { return ResourceLocator.ApplicationDataService; }
                catch { return null; }
            }
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                var raw = DataStore?[ResumedStorageKey] as string;
                if (string.IsNullOrEmpty(raw)) return;
                foreach (var part in raw.Split(','))
                {
                    var trimmed = part.Trim();
                    if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                        _resumed.Add(id);
                }
            }
            catch { }
        }

        private static void Persist()
        {
            try
            {
                var store = DataStore;
                if (store == null) return;
                store[ResumedStorageKey] = string.Join(",", _resumed.OrderBy(x => x).Select(x => x.ToString(CultureInfo.InvariantCulture)));
            }
            catch { }
        }

        public static bool IsOverriddenAsNotAiring(int malId)
        {
            lock (Gate)
            {
                Load();
                return Overrides.ContainsKey(malId) && !_resumed.Contains(malId);
            }
        }

        //Feed the newest real episode date we managed to read. Anything newer than the one the
        //override was created with means the show went back on air, so the override is dropped.
        public static bool ResumeIfAiring(int malId, DateTime? latestAiredUtc)
        {
            if (!latestAiredUtc.HasValue) return false;
            lock (Gate)
            {
                Load();
                if (!Overrides.TryGetValue(malId, out var lastKnown)) return false;
                if (_resumed.Contains(malId)) return false;
                if (latestAiredUtc.Value.Date <= lastKnown.Date) return false;

                //"Newer than when we flagged it" is not enough on its own: MAL backfills rows, so a
                //seasonal show keeps gaining episodes dated weeks in the past between cours (One
                //Piece picked up a 6 Sep row while it is actually between batches). Treating that
                //as "it resumed" brought the invented countdown straight back. The show only counts
                //as airing again when the newest real episode is actually recent.
                var age = DateTime.UtcNow - DateTime.SpecifyKind(latestAiredUtc.Value, DateTimeKind.Utc);
                if (age > ResumeWindow)
                    return false;

                _resumed.Add(malId);
                Persist();
                return true;
            }
        }
    }
}

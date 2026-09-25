using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MALClient.XShared.Comm.Anime;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;

namespace MALClient.XShared.BL
{
    public static class AiringDatumRepository
    {
        private static readonly Dictionary<int, (DateTime? nextAirUtc, DateTime fetchedAt)> _memCache = new Dictionary<int, (DateTime?, DateTime)>();
        private static readonly Dictionary<int, SemaphoreSlim> _locks = new Dictionary<int, SemaphoreSlim>();
        private static readonly object _lockObj = new object();

        private static SemaphoreSlim GetLock(int malId)
        {
            lock (_lockObj)
            {
                if (!_locks.TryGetValue(malId, out var sem))
                {
                    sem = new SemaphoreSlim(1, 1);
                    _locks[malId] = sem;
                }
                return sem;
            }
        }

        public static async Task<DateTime?> GetNextAirUtcAsync(int malId, bool force = false)
        {
            var now = DateTime.UtcNow;
            if (!force && DataCache.TryRetrieveDataForId(malId, out var vd) && vd.NextAirUtc.HasValue)
            {
                if (!AirTimeUtils.NeedsRefresh(vd.NextAirUtc, vd.NextAirFetchedAtUtc, now))
                    return vd.NextAirUtc;
                if (!string.IsNullOrEmpty(vd.LastKnownStatus) && !AirTimeUtils.IsCurrentlyAiringStatus(vd.LastKnownStatus))
                    return null;
            }
            lock (_lockObj)
            {
                if (!force && _memCache.TryGetValue(malId, out var mem))
                {
                    var ttl = AirTimeUtils.CacheTtl(mem.nextAirUtc, now);
                    if (mem.nextAirUtc.HasValue && (mem.nextAirUtc.Value > now || AirTimeUtils.IsInAiringWindow(mem.nextAirUtc.Value, now)))
                    {
                        if (now - mem.fetchedAt < ttl)
                            return mem.nextAirUtc;
                    }
                    else if (!mem.nextAirUtc.HasValue && now - mem.fetchedAt < TimeSpan.FromMinutes(15))
                        return null;
                }
            }

            var sem = GetLock(malId);
            await sem.WaitAsync();
            try
            {
                now = DateTime.UtcNow;
                if (!force && DataCache.TryRetrieveDataForId(malId, out var vd2) && vd2.NextAirUtc.HasValue)
                {
                    if (!AirTimeUtils.NeedsRefresh(vd2.NextAirUtc, vd2.NextAirFetchedAtUtc, now))
                    {
                        lock (_lockObj) _memCache[malId] = (vd2.NextAirUtc, vd2.NextAirFetchedAtUtc ?? now);
                        return vd2.NextAirUtc;
                    }
                }
                if (ResourceLocator.AiringInfoProvider.TryGetNextAirDate(malId, now, out var airDate) && (airDate > now || AirTimeUtils.IsInAiringWindow(airDate, now)))
                {
                    var res = airDate;
                    lock (_lockObj) _memCache[malId] = (res, now);
                    DataCache.UpdateVolatileDataWithNextAir(malId, res);
                    DiagnosticsReporter.Info("AirRepo", $"malId={malId} hit=provider nextAirUtc={res:O}");
                    return res;
                }

                var fromEpisodes = await GetEpisodesFallbackAsync(malId);
                if (fromEpisodes.HasValue)
                {
                    var isAiring = await IsCurrentlyAiringAsync(malId);
                    if (isAiring)
                    {
                        lock (_lockObj) _memCache[malId] = (fromEpisodes, now);
                        DataCache.UpdateVolatileDataWithNextAir(malId, fromEpisodes);
                        DiagnosticsReporter.Info("AirRepo", $"malId={malId} hit=episodes nextAirUtc={fromEpisodes:O}");
                        return fromEpisodes;
                    }
                }

                var broadcast = await GetBroadcastFallbackAsync(malId);
                if (broadcast.HasValue)
                {
                    lock (_lockObj) _memCache[malId] = (broadcast, now);
                    DataCache.UpdateVolatileDataWithNextAir(malId, broadcast);
                    DiagnosticsReporter.Info("AirRepo", $"malId={malId} hit=broadcast nextAirUtc={broadcast:O}");
                    return broadcast;
                }

                lock (_lockObj) _memCache[malId] = (null, now);
                DataCache.UpdateVolatileDataWithNextAir(malId, null);
                DataCache.RegisterVolatileDataAiringTimeFetchFailure(malId);
                DiagnosticsReporter.Info("AirRepo", $"malId={malId} hit=none -> null");
                return null;
            }
            finally
            {
                sem.Release();
            }
        }

        private static async Task<DateTime?> GetEpisodesFallbackAsync(int malId)
        {
            try
            {
                var episodes = await DataCache.RetrieveAnimeEpisodesStale(malId);
                if (episodes == null || episodes.Count == 0)
                    episodes = await new AnimeEpisodesQuery().GetLastEpisodesAsync(malId);
                if (episodes == null || episodes.Count == 0) return null;
                return AirTimeUtils.ComputeNextAirFromEpisodes(episodes, DateTime.UtcNow);
            }
            catch { return null; }
        }

        private static async Task<bool> IsCurrentlyAiringAsync(int malId)
        {
            try
            {
                var data = await DataCache.RetrieveAnimeSearchResultsDataStale(malId.ToString(), true);
                if (data == null || string.IsNullOrEmpty(data.Status))
                    data = await new AnimeGeneralDetailsQuery().GetAnimeDetails(true, malId.ToString(), "", true);
                if (data == null) return false;
                if (!string.IsNullOrEmpty(data.Status))
                    DataCache.UpdateVolatileStatus(malId, data.Status);
                return AirTimeUtils.IsCurrentlyAiringStatus(data.Status);
            }
            catch { return false; }
        }

        private static async Task<DateTime?> GetBroadcastFallbackAsync(int malId)
        {
            try
            {
                var data = await DataCache.RetrieveAnimeSearchResultsDataStale(malId.ToString(), true);
                if (data == null || string.IsNullOrEmpty(data.Broadcast))
                    data = await new AnimeGeneralDetailsQuery().GetAnimeDetails(true, malId.ToString(), "", true);
                if (data?.Broadcast == null) return null;
                if (!string.IsNullOrEmpty(data.Status))
                    DataCache.UpdateVolatileStatus(malId, data.Status);
                if (!string.Equals(data.Status, "Currently Airing", StringComparison.CurrentCultureIgnoreCase))
                    return null;
                return AirTimeUtils.ComputeNextAirDate(data.Broadcast, DateTime.UtcNow, true);
            }
            catch { return null; }
        }
    }
}

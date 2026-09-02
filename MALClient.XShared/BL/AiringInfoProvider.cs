using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.Runtime;
using MALClient.Adapters;
using MALClient.XShared.Comm.Anime;
using MALClient.XShared.Interfaces;
using MALClient.XShared.Utils;
using Newtonsoft.Json;

namespace MALClient.XShared.BL
{
    public class AiringInfoProvider : IAiringInfoProvider
    {
        private const string UpdateStorakeKey = "AiringInfoProviderLastUpdateDate";
        private const string CacheFileName = "airing_data.json";

        private readonly IDataCache _dataCache;
        private readonly IApplicationDataService _applicationDataService;
        private readonly IMessageDialogProvider _dialogProvider;

        private List<AiringData> _airingData;
        private Task _initTask;
        private List<AiringData> AiringShows
        {
            get { return _airingData; }
            set
            {
                _airingData = value;
                _lookupDictionary =
                    new NullDictionary<int, AiringData>(
                        value.ToDictionary(data => data.MalId, data => data));
            }
        }

        private NullDictionary<int, AiringData> _lookupDictionary;

        public AiringInfoProvider(IDataCache dataCache,
            IApplicationDataService applicationDataService )
        {
            _dataCache = dataCache;
            _applicationDataService = applicationDataService;
        }

        public Task Init(bool cacheOnly)
        {
            if (_airingData != null)
                return Task.CompletedTask;
            //dedupe: let concurrent callers (startup + calendar) share one in-flight fetch
            if (_initTask != null)
                return _initTask;
            _initTask = InitCore(cacheOnly);
            return _initTask;
        }

        private async Task InitCore(bool cacheOnly)
        {
            if(_airingData != null)
                return;

            List<AiringData> data = null;
            try
            {
                data = await _dataCache.RetrieveData<List<AiringData>>(CacheFileName, null, 0);
            }
            catch (Exception)
            {
                data = null;
            }

            var lastUpdate = _applicationDataService[UpdateStorakeKey];
            var fresh = lastUpdate != null &&
                        DateTime.Now - DateTime.FromBinary((long) lastUpdate) < TimeSpan.FromHours(8);

            //self-heal: a cache persisted from the legacy feed only carries mal_id+episodes
            //(no titles/images). Treat it as stale so we try to re-fetch the full schedules data.
            var oldSchema = data != null && data.Any() && data.All(x => string.IsNullOrEmpty(x.Title));

            try
            {
                if (data != null && data.Any() && fresh && !oldSchema)
                {
                    ApplyData(data);
                    return;
                }

                if (!cacheOnly)
                {
                    var schedulesTask = new AnimeSchedulesQuery().GetScheduleAsync();
                    var completed = await Task.WhenAny(schedulesTask, Task.Delay(15000));
                    if (completed == schedulesTask)
                    {
                        var schedulesData = await schedulesTask;
                        if (schedulesData != null && schedulesData.Any())
                        {
                            data = schedulesData;
                            _applicationDataService[UpdateStorakeKey] = DateTime.Now.ToBinary();
                            try { _dataCache.SaveData(data, CacheFileName, null); } catch (Exception) { }
                        }
                    }
                    // timeout or empty/failed refetch -> keep the stale cache we read above
                }
            }
            catch (Exception)
            {
                // network/refetch failure -> keep whatever cache we already read (stale-not-blank)
            }

            if (oldSchema && data != null && data.Any() && !string.IsNullOrEmpty(data.First().Title))
                DiagnosticsReporter.Info("AiringInfoProvider", $"old-schema cache self-healed: {data.Count} entries have titles");

            if (data == null || !data.Any())
            {
                AiringShows = new List<AiringData>();
                InitializationSuccess = false;
                return;
            }

            ApplyData(data);
        }

        private void ApplyData(List<AiringData> data)
        {
            foreach (var airingData in data)
            {
                if (airingData.Episodes != null)
                    airingData.Episodes = airingData.Episodes.OrderBy(episode => episode.Timestamp).ToList();
            }
            AiringShows = data.GroupBy(x => x.MalId).Select(g => g.First()).ToList();
            InitializationSuccess = true;
        }

        public bool TryGetCurrentEpisode(int id, out int episode, DateTime? forDay = null)
        {
            episode = 0;
            var currentTimestamp = Utilities.ConvertToUnixTimestamp(DateTime.UtcNow);
            var data = _lookupDictionary[id];
            if (data == null)
                return false;

            try
            {
                if (forDay == null)
                {
                    episode = GetCurrentEpisode(data,currentTimestamp);
                }
                else
                {
                    var todaysMatch =
                        data.Episodes.FirstOrDefault(ep => Utilities.ConvertFromUnixTimestamp(ep.Timestamp).DayOfYear ==
                                                           forDay.Value.DayOfYear);
                    if (todaysMatch != null)
                        episode = todaysMatch.EpisodeNumber;
                    else
                        episode = GetCurrentEpisode(data, currentTimestamp);

                }

                if (episode <= 0)
                    return false;
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public bool TryGetLastEpisode(int id, out int ep)
        {
            var data = _lookupDictionary[id];
            if (data?.Episodes == null || !data.Episodes.Any())
            {
                ep = 0;
                return false;
            }

            ep = data.Episodes.Max(episode => episode.EpisodeNumber);

            return true;
        }

        private int GetCurrentEpisode(AiringData data, int currentTimestamp)
        {
            var next = data.Episodes.FirstOrDefault(ep => ep.Timestamp >= currentTimestamp);
            if (next != null)
                return next.EpisodeNumber - 1;

            if (data.Episodes.Last().Timestamp < currentTimestamp)
            {
                return data.Episodes.Last().EpisodeNumber;
            }

            return 0;
        }

        public bool TryGetNextAirDate(int id, DateTime forDay, out DateTime date)
        {
            date = DateTime.MinValue;

            var data = _lookupDictionary[id];
            if (data == null)
                return false;

            try
            {
                var todaysMatch =
                    data.Episodes.FirstOrDefault(ep => Utilities.ConvertFromUnixTimestamp(ep.Timestamp).DayOfYear ==
                                                       forDay.DayOfYear);
                if (todaysMatch != null)
                    date = Utilities.ConvertFromUnixTimestamp(todaysMatch.Timestamp);
                else
                {
                    var currentTimestamp = Utilities.ConvertToUnixTimestamp(DateTime.UtcNow);
                    var next = data.Episodes.FirstOrDefault(ep => ep.Timestamp >= currentTimestamp);
                    if (next != null)
                        date = Utilities.ConvertFromUnixTimestamp(next.Timestamp);
                    else if (data.Episodes.Last().Timestamp < currentTimestamp)
                    {
                        date = Utilities.ConvertFromUnixTimestamp(data.Episodes.Last().Timestamp);
                    }
                    
                }
                if (date == DateTime.MinValue)
                    return false;
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        public bool TryGetAiringDay(int id, out DayOfWeek day)
        {
            day = DayOfWeek.Monday;
            var data = _lookupDictionary[id];
            if (data == null || !data.Episodes.Any())
                return false;

            var jst = DateTimeOffset.FromUnixTimeSeconds(data.Episodes[0].Timestamp).ToOffset(TimeSpan.FromHours(9));
            day = jst.DayOfWeek;

            return true;
        }

        public IEnumerable<int> GetAllAiringIds()
        {
            if (_airingData == null)
                yield break;
            foreach (var data in _airingData)
            {
                if (data != null && data.Episodes != null && data.Episodes.Any())
                    yield return data.MalId;
            }
        }

        public bool HasAiringEntry(int id)
        {
            return _lookupDictionary.ContainsKey(id);
        }

        public bool TryGetEntry(int id, out AiringData entry)
        {
            entry = _lookupDictionary[id];
            return entry != null;
        }

        public bool InitializationSuccess { get; set; }

        [Preserve(AllMembers = true)]
        public class Episode
        {
            [JsonProperty("t")]
            public int Timestamp { get; set; }
            [JsonProperty("n")]
            public int EpisodeNumber { get; set; }
        }

        [Preserve(AllMembers = true)]
        public class AiringData
        {
            [JsonProperty("mal_id")]
            public int MalId { get; set; }
            [JsonProperty("airing")]
            public List<Episode> Episodes { get; set; }
            [JsonProperty("title")]
            public string Title { get; set; }
            [JsonProperty("img_url")]
            public string ImgUrl { get; set; }
            [JsonProperty("type")]
            public int Type { get; set; }
            [JsonProperty("all_episodes")]
            public int AllEpisodes { get; set; }
        }

        class NullDictionary<TKey, TVal> : Dictionary<TKey, TVal>
        {
            public NullDictionary()
            {
                
            }

            public NullDictionary(Dictionary<TKey,TVal> source)
            {
                foreach (var val in source)
                {
                    Add(val.Key,val.Value);
                }
            }

            public new TVal this[TKey key]
            {
                get
                {
                    if(ContainsKey(key))
                        return base[key];
                    return default(TVal);              
                }
                set => base[key] = value;
            }
        }
    }
}

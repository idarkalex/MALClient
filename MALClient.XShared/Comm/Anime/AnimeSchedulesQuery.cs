using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MALClient.XShared.BL;
using MALClient.XShared.Utils;

namespace MALClient.XShared.Comm.Anime
{
    public class AnimeSchedulesQuery
    {
        private static readonly string[] DayNames = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

        public async Task<List<AiringInfoProvider.AiringData>> GetScheduleAsync()
        {
            var output = new List<AiringInfoProvider.AiringData>();
            try
            {
                var items = await TenraiClient.GetAllPagesAsync(p => $"schedules?page={p}&sfw", 12);
                var nowUtc = DateTime.UtcNow;

                foreach (var entry in items)
                {
                    try
                    {
                        var malId = GetInt(entry, "mal_id");
                        if (malId <= 0)
                            continue;

                        var dayOfWeek = GetBroadcastDay(entry);
                        var time = GetBroadcastTime(entry);
                        if (dayOfWeek == null || time == null)
                            continue;

                        var firstAir = AirTimeUtils.ComputeNextAirDate(dayOfWeek.Value, time.Value, nowUtc);
                        if (!firstAir.HasValue)
                            continue;

                        var airingData = new AiringInfoProvider.AiringData
                        {
                            MalId = malId,
                            Episodes = new List<AiringInfoProvider.Episode>()
                        };

                        for (int i = 0; i < 52; i++)
                        {
                            var airDate = firstAir.Value.AddDays(i * 7);
                            airingData.Episodes.Add(new AiringInfoProvider.Episode
                            {
                                Timestamp = Utilities.ConvertToUnixTimestamp(airDate),
                                EpisodeNumber = i + 1
                            });
                        }

                        output.Add(airingData);
                    }
                    catch (Exception)
                    {
                        // skip malformed entry
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }

            return output.Count > 0 ? output : null;
        }

        private static DayOfWeek? GetBroadcastDay(JsonElement entry)
        {
            if (!entry.TryGetProperty("broadcast", out var bc) || bc.ValueKind != JsonValueKind.Object)
                return null;
            if (!bc.TryGetProperty("day", out var dayProp) || dayProp.ValueKind != JsonValueKind.String)
                return null;

            var dayStr = dayProp.GetString();
            if (string.IsNullOrEmpty(dayStr))
                return null;

            for (int i = 0; i < DayNames.Length; i++)
            {
                if (dayStr.StartsWith(DayNames[i], StringComparison.OrdinalIgnoreCase))
                    return (DayOfWeek)i;
            }
            return null;
        }

        private static TimeSpan? GetBroadcastTime(JsonElement entry)
        {
            if (!entry.TryGetProperty("broadcast", out var bc) || bc.ValueKind != JsonValueKind.Object)
                return null;
            if (!bc.TryGetProperty("time", out var timeProp) || timeProp.ValueKind != JsonValueKind.String)
                return null;

            var timeStr = timeProp.GetString();
            if (string.IsNullOrEmpty(timeStr))
                return null;

            var parts = timeStr.Split(':');
            if (parts.Length != 2)
                return null;
            if (!int.TryParse(parts[0], out var hours) || !int.TryParse(parts[1], out var minutes))
                return null;

            return TimeSpan.FromMinutes(hours * 60 + minutes);
        }

        private static int GetInt(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : 0;
    }
}
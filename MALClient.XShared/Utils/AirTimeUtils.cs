using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MALClient.XShared.Utils
{
    public static class AirTimeUtils
    {
        private static readonly TimeSpan JstOffset = TimeSpan.FromHours(9);
        private static readonly string[] DayNames = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

        public static bool IsCurrentlyAiringStatus(string status)
        {
            if (string.IsNullOrEmpty(status))
                return false;
            if (string.Equals(status, "Currently Airing", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(status, "Airing", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(status, "currently_airing", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        public static DateTime? ComputeNextAirDate(string broadcast, DateTime nowUtc)
        {
            if (string.IsNullOrEmpty(broadcast)) return null;

            var match = Regex.Match(broadcast, @"(?<day>[A-Za-z]+?day)[^0-9]*(?<time>\d{1,2}:\d{2})");
            if (!match.Success) return null;

            var dayStr = match.Groups["day"].Value;
            var timeStr = match.Groups["time"].Value;

            var timeParts = timeStr.Split(':');
            if (timeParts.Length != 2 ||
                !int.TryParse(timeParts[0], out var hours) ||
                !int.TryParse(timeParts[1], out var minutes))
                return null;

            var timeOfDay = TimeSpan.FromMinutes(hours * 60 + minutes);

            var dayIndex = -1;
            for (int i = 0; i < DayNames.Length; i++)
            {
                if (dayStr.StartsWith(DayNames[i], StringComparison.OrdinalIgnoreCase))
                {
                    dayIndex = i;
                    break;
                }
            }
            if (dayIndex < 0) return null;

            var nowJst = nowUtc + JstOffset;
            var targetJst = new DateTime(nowJst.Year, nowJst.Month, nowJst.Day, 0, 0, 0).Add(timeOfDay);
            var dayNet = (dayIndex + 1) % 7;
            var daysToAdd = ((dayNet - (int)nowJst.DayOfWeek) + 7) % 7;
            var slotUtc = targetJst.AddDays(daysToAdd) - JstOffset;
            if (slotUtc > nowUtc)
                return slotUtc;
            return slotUtc.AddDays(7);
        }

        public static DateTime? ComputeNextAirDate(string broadcast, DateTime nowUtc, bool allowAiringNow)
        {
            if (string.IsNullOrEmpty(broadcast)) return null;

            var match = Regex.Match(broadcast, @"(?<day>[A-Za-z]+?day)[^0-9]*(?<time>\d{1,2}:\d{2})");
            if (!match.Success) return null;

            var dayStr = match.Groups["day"].Value;
            var timeStr = match.Groups["time"].Value;

            var timeParts = timeStr.Split(':');
            if (timeParts.Length != 2 ||
                !int.TryParse(timeParts[0], out var hours) ||
                !int.TryParse(timeParts[1], out var minutes))
                return null;

            var timeOfDay = TimeSpan.FromMinutes(hours * 60 + minutes);

            var dayIndex = -1;
            for (int i = 0; i < DayNames.Length; i++)
            {
                if (dayStr.StartsWith(DayNames[i], StringComparison.OrdinalIgnoreCase))
                {
                    dayIndex = i;
                    break;
                }
            }
            if (dayIndex < 0) return null;

            return ComputeNextAirDate((DayOfWeek)dayIndex, timeOfDay, nowUtc, allowAiringNow);
        }

        public static DateTime? ComputeNextAirFromEpisodes(IEnumerable<Models.Models.Anime.AnimeEpisode> episodes, DateTime nowUtc)
        {
            var aired = episodes
                .Where(ep => ep.AiredDate.HasValue)
                .Select(ep => ep.AiredDate.Value)
                .Where(d => d <= nowUtc)
                .OrderBy(d => d)
                .ToList();
            if (aired.Count == 0) return null;

            double gap = 7;
            if (aired.Count >= 2)
            {
                var gaps = new List<double>();
                for (int i = 1; i < aired.Count; i++)
                    gaps.Add((aired[i] - aired[i - 1]).TotalDays);
                gaps.Sort();
                gap = gaps[gaps.Count / 2];
                if (gap < 1 || gap > 30)
                    gap = 7;
            }

            // Never assume infinite airing: if the latest episode is far older than
            // ~3x the typical cadence, the series likely paused or finished airing.
            if ((nowUtc - aired[aired.Count - 1]).TotalDays > gap * 3)
                return null;

            var latest = aired[aired.Count - 1];
            // currently airing: during the airing hour the latest episode is the airing reference
            if (IsInAiringWindow(latest, nowUtc))
                return latest;

            var next = latest.AddDays(gap);
            while (next <= nowUtc)
                next = next.AddDays(gap);
            return next;
        }

        public static DateTime? ComputeNextAirDate(System.DayOfWeek day, TimeSpan time, DateTime nowUtc)
            => ComputeNextAirDate(day, time, nowUtc, false);

        public static DateTime? ComputeNextAirDate(System.DayOfWeek day, TimeSpan time, DateTime nowUtc, bool allowAiringNow)
        {
            var nowJst = nowUtc + JstOffset;
            var targetJst = new DateTime(nowJst.Year, nowJst.Month, nowJst.Day, 0, 0, 0).Add(time);
            var dayNet = (int)day + 1;
            var daysToAdd = ((dayNet - (int)nowJst.DayOfWeek) + 7) % 7;
            var slotUtc = targetJst.AddDays(daysToAdd) - JstOffset;
            if (slotUtc > nowUtc)
                return slotUtc;
            if (allowAiringNow && IsInAiringWindow(slotUtc, nowUtc))
                return slotUtc;
            return slotUtc.AddDays(7);
        }

        public static DateTime? ComputeNextAirDate(Models.Models.Misc.ExactAiringTimeData exactTime, DateTime nowUtc)
        {
            if (exactTime == null) return null;
            return ComputeNextAirDate(exactTime.DayOfWeek, exactTime.Time, nowUtc);
        }

        public static string FormatAirCountdown(DateTime airDate, DateTime now)
        {
            var diff = airDate - now;
            if (diff.TotalSeconds <= 0)
                return IsInAiringWindow(airDate, now) ? "Airing now" : "";
            if (diff.TotalDays >= 1)
                return $"{(int)diff.TotalDays}D";
            if (diff.TotalHours >= 1)
                return $"{(int)diff.TotalHours}H";
            return $"{(int)diff.TotalMinutes}M";
        }

        //True while the series is on air: the reference date is in the past but
        //the full one-hour airing block hasn't finished yet.
        public static bool IsInAiringWindow(DateTime airDateUtc, DateTime nowUtc)
        {
            var diff = airDateUtc - nowUtc;
            return diff.TotalSeconds <= 0 && diff.TotalSeconds > -3600;
        }

        //How long a cached next-air value stays valid, based on how far away it is
        //(days -> re-fetch daily, hours -> hourly, minutes -> every 15 min).
        //A value inside the airing window stays fresh until the hour passes.
        public static TimeSpan CacheTtl(DateTime? nextAirUtc, DateTime nowUtc)
        {
            if (!nextAirUtc.HasValue)
                return TimeSpan.Zero;
            var remaining = nextAirUtc.Value - nowUtc;
            if (remaining.TotalSeconds > 0 && remaining.TotalDays >= 1)
                return TimeSpan.FromHours(24);
            if (remaining.TotalSeconds > 0 && remaining.TotalHours >= 1)
                return TimeSpan.FromHours(1);
            if (remaining.TotalSeconds > 0)
                return TimeSpan.FromMinutes(15);
            if (IsInAiringWindow(nextAirUtc.Value, nowUtc))
                return nextAirUtc.Value.AddHours(1) - nowUtc;
            return TimeSpan.Zero;
        }

        public static bool NeedsRefresh(DateTime? nextAirUtc, DateTime? fetchedAtUtc, DateTime nowUtc)
        {
            if (!fetchedAtUtc.HasValue)
                return true;
            if (IsInAiringWindow(nextAirUtc, nowUtc))
                return nowUtc - fetchedAtUtc.Value > CacheTtl(nextAirUtc, nowUtc);
            if (!nextAirUtc.HasValue || nextAirUtc.Value <= nowUtc)
                return true;
            return nowUtc - fetchedAtUtc.Value > CacheTtl(nextAirUtc, nowUtc);
        }

        public static bool IsInAiringWindow(DateTime? airDateUtc, DateTime nowUtc)
            => airDateUtc.HasValue && IsInAiringWindow(airDateUtc.Value, nowUtc);
    }
}

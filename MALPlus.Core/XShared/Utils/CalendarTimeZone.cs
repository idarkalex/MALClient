using System;
using System.Collections.Generic;

namespace MALClient.XShared.Utils
{
    //Airing instants are absolute, so a countdown needs no time zone at all: "3H" is the same in
    //Madrid and in Tokyo. What does need one is the weekday column. MAL publishes the broadcast
    //slot in JST, so the provider always reports the Japanese weekday, which puts a 07:30 JST
    //Tuesday show under Tuesday even though anyone west of Tokyo watches it Monday night.
    public static class CalendarTimeZone
    {
        //A real class, not a value tuple: reflection only sees Item1/Item2 on a tuple, so a
        //Picker ItemDisplayBinding of "Label" silently renders nothing.
        public sealed class Option
        {
            public string Id { get; set; }
            public string Label { get; set; }
            public override string ToString() => Label;
        }

        private static readonly List<Option> _options = new List<Option>
        {
            new Option { Id = "", Label = "System default" },
            new Option { Id = "UTC", Label = "UTC" },
            new Option { Id = "Europe/Madrid", Label = "Madrid" },
            new Option { Id = "Europe/Lisbon", Label = "Lisbon" },
            new Option { Id = "Europe/London", Label = "London" },
            new Option { Id = "Europe/Paris", Label = "Paris" },
            new Option { Id = "Europe/Berlin", Label = "Berlin" },
            new Option { Id = "Europe/Athens", Label = "Athens" },
            new Option { Id = "Europe/Kiev", Label = "Kyiv" },
            new Option { Id = "Europe/Moscow", Label = "Moscow" },
            new Option { Id = "Europe/Istanbul", Label = "Istanbul" },
            new Option { Id = "Africa/Lagos", Label = "Lagos" },
            new Option { Id = "Africa/Cairo", Label = "Cairo" },
            new Option { Id = "Africa/Johannesburg", Label = "Johannesburg" },
            new Option { Id = "America/New_York", Label = "New York" },
            new Option { Id = "America/Chicago", Label = "Chicago" },
            new Option { Id = "America/Denver", Label = "Denver" },
            new Option { Id = "America/Los_Angeles", Label = "Los Angeles" },
            new Option { Id = "America/Mexico_City", Label = "Mexico City" },
            new Option { Id = "America/Bogota", Label = "Bogota" },
            new Option { Id = "America/Sao_Paulo", Label = "Sao Paulo" },
            new Option { Id = "America/Argentina/Buenos_Aires", Label = "Buenos Aires" },
            new Option { Id = "Pacific/Honolulu", Label = "Honolulu" },
            new Option { Id = "Asia/Jerusalem", Label = "Jerusalem" },
            new Option { Id = "Asia/Dubai", Label = "Dubai" },
            new Option { Id = "Asia/Karachi", Label = "Karachi" },
            new Option { Id = "Asia/Kolkata", Label = "Kolkata" },
            new Option { Id = "Asia/Dhaka", Label = "Dhaka" },
            new Option { Id = "Asia/Bangkok", Label = "Bangkok" },
            new Option { Id = "Asia/Jakarta", Label = "Jakarta" },
            new Option { Id = "Asia/Shanghai", Label = "Shanghai" },
            new Option { Id = "Asia/Hong_Kong", Label = "Hong Kong" },
            new Option { Id = "Asia/Singapore", Label = "Singapore" },
            new Option { Id = "Asia/Tokyo", Label = "Tokyo" },
            new Option { Id = "Australia/Perth", Label = "Perth" },
            new Option { Id = "Australia/Sydney", Label = "Sydney" },
            new Option { Id = "Pacific/Auckland", Label = "Auckland" },
        };

        public static IReadOnlyList<Option> Options => _options;

        public static string LabelFor(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "System default";
            foreach (var option in _options)
            {
                if (option.Id == id)
                    return option.Label;
            }
            return id;
        }

        public static TimeZoneInfo Resolve()
        {
            var id = Settings.CalendarTimeZoneId;
            if (string.IsNullOrEmpty(id))
                return TimeZoneInfo.Local;
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch
            {
                //Unknown id (missing tzdata, or a zone the OS spells differently). Staying on the
                //device zone is the safe answer: the alternative would be silently bucketing by
                //JST again, which is the bug this setting exists to fix.
                return TimeZoneInfo.Local;
            }
        }

        private static string _cachedId;
        private static TimeZoneInfo _cachedZone;
        private static bool _cachedResolved;

        /// <summary>
        ///     Memoised zone. This is read once per item in hot paths (AnimeItemAbstraction
        ///     constructors), and FindSystemTimeZoneById is far too expensive for that.
        /// </summary>
        public static TimeZoneInfo Resolved
        {
            get
            {
                var id = Settings.CalendarTimeZoneId ?? "";
                if (_cachedResolved && id == _cachedId && _cachedZone != null)
                    return _cachedZone;
                _cachedZone = Resolve();
                _cachedId = id;
                _cachedResolved = true;
                return _cachedZone;
            }
        }

        public static void Invalidate() => _cachedResolved = false;

        //NextAirUtc values come out of ConvertFromUnixTimestamp as Kind=Unspecified, but they are
        //UTC instants, so anything Local has to be normalised before converting.
        public static DateTime ToZone(DateTime utc, TimeZoneInfo zone)
        {
            var target = zone ?? TimeZoneInfo.Local;
            DateTime asUtc;
            if (utc.Kind == DateTimeKind.Local)
                asUtc = utc.ToUniversalTime();
            else
                asUtc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(asUtc, target);
        }

        public static DayOfWeek ToZoneDay(DateTime utc, TimeZoneInfo zone) => ToZone(utc, zone).DayOfWeek;

        //The day-of-year key used to match a day cell. Has to be computed in the same zone as the
        //day column, otherwise an entry can never find its own cell around midnight.
        public static int ToZoneDayOfYear(DateTime utc, TimeZoneInfo zone) => ToZone(utc, zone).DayOfYear;
    }
}

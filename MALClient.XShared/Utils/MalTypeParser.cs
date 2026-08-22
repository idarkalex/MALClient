using System;
using MALClient.Models.Enums;

namespace MALClient.XShared.Utils
{
    public static class MalTypeParser
    {
        public static AnimeType ParseAnimeType(string input)
        {
            switch (Normalize(input))
            {
                case "tv":
                    return AnimeType.TV;
                case "movie":
                    return AnimeType.Movie;
                case "special":
                    return AnimeType.Special;
                case "ova":
                    return AnimeType.OVA;
                case "ona":
                    return AnimeType.ONA;
                case "music":
                    return AnimeType.Music;
                default:
                    return AnimeType.TV;
            }
        }

        public static MangaType ParseMangaType(string input)
        {
            switch (Normalize(input))
            {
                case "manga":
                    return MangaType.Manga;
                case "novel":
                case "lightnovel":
                    return MangaType.Novel;
                case "oneshot":
                    return MangaType.OneShot;
                case "doujinshi":
                    return MangaType.Doujinshi;
                case "manhwa":
                    return MangaType.Manhwa;
                case "manhua":
                    return MangaType.Manhua;
                default:
                    return MangaType.Manga;
            }
        }

        private static string Normalize(string input) =>
            (input ?? "").Trim().ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");
    }
}

// ReSharper disable InconsistentNaming

namespace MALClient.Models.Enums
{
    public enum AnimeType
    {
        TV = 1,
        OVA = 2,
        Movie = 3,
        Special = 4,
        ONA = 5,
        Music = 6
    }

    public enum MangaType
    {
        Manga = 1,
        Novel = 2,
        Manhwa = 5,
        OneShot = 3,
        Manhua = 6,
        Doujinshi = 4,

        /// <summary>
        ///     MAL reports light novels separately ("light_novel"); folding them
        ///     into Novel hid the format everywhere it is shown.
        /// </summary>
        LightNovel = 7
    }
}
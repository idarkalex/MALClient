using System.Collections.Generic;

namespace MALClient.Models.Models.AnimeScrapped
{
    public class SeasonalAnimeData : ISeasonalAnimeBaseData
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public List<string> Genres { get; set; }
        public float Score { get; set; }
        public string ImgUrl { get; set; }
        public string Episodes { get; set; }
        public int Index { get; set; }
        public int AirDay { get; set; }
        public string AirStartDate { get; set; }

        /// <summary>
        ///     MALClient.Models.Enums.AnimeType / MangaType value. 0 means "unknown" and
        ///     hides the format badge; entries that already live in the user's library
        ///     carry their own type and ignore this.
        /// </summary>
        public int Type { get; set; }
    }
}
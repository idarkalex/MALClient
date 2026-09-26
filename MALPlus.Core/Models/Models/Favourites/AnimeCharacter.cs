using MALClient.Models.Enums;

namespace MALClient.Models.Models.Favourites
{
    public class AnimeCharacter : FavouriteBase
    {
        public string ShowId { get; set; }
        public bool FromAnime { get; set; }

        /// <summary>
        /// Tenrai only ever reports "Main" or "Supporting" for a character. It used to be
        /// pasted into Notes together with the favourites count, which made the role
        /// impossible to style and left a broken separator in the middle of the string.
        /// </summary>
        public string Role { get; set; }

        public int Favorites { get; set; }

        public override FavouriteType Type { get; } = FavouriteType.Character;
    }
}
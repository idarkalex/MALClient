using MALClient.Models.Enums;

namespace MALClient.Models.Models.Favourites
{
    public class AnimeStaffPerson : FavouriteBase
    {
        public override FavouriteType Type { get; } = FavouriteType.Person;
        public bool IsUnknown { get; set; }

        /// <summary>
        /// Tenrai sends voice_actors[].person.favorites, so the seiyuu can show how
        /// popular they are. Staff entries carry no favorites at all, so it stays 0 there.
        /// </summary>
        public int Favorites { get; set; }

        /// <summary>Language of a voice actor, e.g. "Japanese". Empty for staff.</summary>
        public string Language { get; set; }

        /// <summary>
        /// A staff member can hold up to six positions and Tenrai embeds the episodes
        /// inside the text, e.g. "Storyboard (eps 376, 379, 381)". Notes used to be every
        /// position joined together, which was an unreadable wall of text.
        /// </summary>
        public string PrimaryPosition { get; set; }

        public int ExtraPositions { get; set; }
    }
}
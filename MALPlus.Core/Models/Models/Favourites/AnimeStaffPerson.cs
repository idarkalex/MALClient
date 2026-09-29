using System.Collections.Generic;
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
        /// position joined together, which was an unreadable wall of text, and then it
        /// collapsed to the first role plus a "+2" that said nothing about what the other two
        /// were. The full list is kept here so the row can show the roles over two lines.
        /// </summary>
        public string PrimaryPosition { get; set; }

        public int ExtraPositions { get; set; }

        /// <summary>Every role this person has, episodes stripped. Empty for a single role.</summary>
        public List<string> Positions { get; set; } = new List<string>();
    }
}
namespace MALClient.Models.Models.Anime
{
    public class AnimeSeason
    {
        public string Name { get; set; }
        public int Year { get; set; }
        public Season Season { get; set; }
        public bool IsCurrentSeason { get; set; }
        public string DisplayName => IsCurrentSeason ? $"{Name} (Current)" : Name;
    }

}

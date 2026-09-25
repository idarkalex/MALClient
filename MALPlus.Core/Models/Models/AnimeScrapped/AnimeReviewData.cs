using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace MALClient.Models.Models.AnimeScrapped
{
    public class ReviewScore
    {
        public string Field { get; set; }
        public string Score { get; set; }
    }

    public class AnimeReviewData : INotifyPropertyChanged
    {
        private bool _isExpanded;

        public string Id { get; set; }
        public string Review { get; set; }
        public string Author { get; set; }
        public string Date { get; set; }
        public string AuthorAvatar { get; set; }
        public string OverallRating { get; set; }
        public string EpisodesSeen { get; set; }
        public string HelpfulCount { get; set; }
        public bool HasSpoilers { get; set; }
        public bool IsPreliminary { get; set; }
        public List<ReviewScore> Score { get; set; } = new List<ReviewScore>();

        [Newtonsoft.Json.JsonIgnore]
        [JsonIgnore]
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value)
                    return;
                _isExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ReviewMaxLines));
                OnPropertyChanged(nameof(ReviewToggleText));
            }
        }

        [Newtonsoft.Json.JsonIgnore]
        [JsonIgnore]
        public int ReviewMaxLines => IsExpanded ? 10000 : 6;

        [Newtonsoft.Json.JsonIgnore]
        [JsonIgnore]
        public string ReviewToggleText => IsExpanded ? "Show less" : "Show more";

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

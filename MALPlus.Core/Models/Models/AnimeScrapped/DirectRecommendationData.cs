using System.ComponentModel;
using System.Runtime.CompilerServices;
using MALClient.Models.Enums;
using MALClient.Models.Interfaces;

namespace MALClient.Models.Models.AnimeScrapped
{
    /// <summary>
    ///     Direct as in recommendation from details page
    /// </summary>
    public class DirectRecommendationData : IDetailsPageArgs, INotifyPropertyChanged
    {
        private string _descriptionBottom;
        private string _descriptionTop;
        private bool _hasDescriptionBottom;
        private bool _hasDescriptionTop;

        public event PropertyChangedEventHandler PropertyChanged;

        private string _description;
        public string Description
        {
            get => _description;
            set
            {
                if (_description == value)
                    return;
                _description = value;
                DescriptionSplitSource = null;
                OnPropertyChanged();
            }
        }

        public string ImageUrl { get; set; }
        public int Id { get; set; }
        public string Title { get; set; }
        public RelatedItemType Type { get; set; }
        public string MediaType { get; set; }
        public string AirDayTillBind { get; set; }

        public string DescriptionTop
        {
            get => _descriptionTop;
            private set => SetFlag(ref _descriptionTop, value, ref _hasDescriptionTop, nameof(DescriptionTop), nameof(HasDescriptionTop));
        }

        public bool HasDescriptionTop
        {
            get => _hasDescriptionTop;
            private set
            {
                if (_hasDescriptionTop == value)
                    return;
                _hasDescriptionTop = value;
                OnPropertyChanged();
            }
        }

        public string DescriptionBottom
        {
            get => _descriptionBottom;
            private set => SetFlag(ref _descriptionBottom, value, ref _hasDescriptionBottom, nameof(DescriptionBottom), nameof(HasDescriptionBottom));
        }

        public bool HasDescriptionBottom
        {
            get => _hasDescriptionBottom;
            private set
            {
                if (_hasDescriptionBottom == value)
                    return;
                _hasDescriptionBottom = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        ///     Memo of the inputs used to produce the current split, so the measuring handler can skip
        ///     recomputing on every layout pass and can notice a late description overwrite.
        /// </summary>
        public string DescriptionSplitSource { get; set; }

        public double SplitWidth { get; set; }
        public double SplitTitleHeight { get; set; }

        public void SetDescriptionSplit(string top, string bottom)
        {
            DescriptionTop = top;
            DescriptionBottom = bottom;
            DescriptionSplitSource = Description;
        }

        private void SetFlag(ref string field, string value, ref bool flag, params string[] names)
        {
            if (field == value)
                return;
            field = value;
            flag = !string.IsNullOrWhiteSpace(value);
            foreach (var name in names)
                OnPropertyChanged(name);
        }

        private void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

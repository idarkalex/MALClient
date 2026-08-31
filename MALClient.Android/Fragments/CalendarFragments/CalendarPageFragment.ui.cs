using Android.Support.V4.View;
using Android.Widget;

using MALClient.Android.Resources;


namespace MALClient.Android.Fragments.CalendarFragments
{
    public partial class CalendarPageFragment
    {
        private UserControls.PagerSlidingTabStrip _calendarPageTabStrip;
        private ViewPager _calendarPageViewPager;
        private LinearLayout _calendarPageContentGrid;
        private ProgressBar _calendarPageProgressBar;
        private LinearLayout _calendarPageProgressBarGrid;
        private LinearLayout _calendarPageModeSegmentedBar;
        private LinearLayout _calendarPageModePersonalSection;
        private LinearLayout _calendarPageModeAiringNowSection;
        private TextView _calendarPageModePersonalButton;
        private TextView _calendarPageModeAiringNowButton;
        private global::Android.Views.View _calendarPageModePersonalIndicator;
        private global::Android.Views.View _calendarPageModeAiringNowIndicator;

        public LinearLayout CalendarPageModeSegmentedBar => GetView(ref _calendarPageModeSegmentedBar, Resource.Id.CalendarPageModeSegmentedBar);

        public LinearLayout CalendarPageModePersonalSection => GetView(ref _calendarPageModePersonalSection, Resource.Id.CalendarPageModePersonalSection);

        public LinearLayout CalendarPageModeAiringNowSection => GetView(ref _calendarPageModeAiringNowSection, Resource.Id.CalendarPageModeAiringNowSection);

        public TextView CalendarPageModePersonalButton => GetView(ref _calendarPageModePersonalButton, Resource.Id.CalendarPageModePersonalButton);

        public TextView CalendarPageModeAiringNowButton => GetView(ref _calendarPageModeAiringNowButton, Resource.Id.CalendarPageModeAiringNowButton);

        public global::Android.Views.View CalendarPageModePersonalIndicator => GetView(ref _calendarPageModePersonalIndicator, Resource.Id.CalendarPageModePersonalIndicator);

        public global::Android.Views.View CalendarPageModeAiringNowIndicator => GetView(ref _calendarPageModeAiringNowIndicator, Resource.Id.CalendarPageModeAiringNowIndicator);

        public UserControls.PagerSlidingTabStrip CalendarPageTabStrip => GetView(ref _calendarPageTabStrip, Resource.Id.CalendarPageTabStrip);

        public ViewPager CalendarPageViewPager => GetView(ref _calendarPageViewPager, Resource.Id.CalendarPageViewPager);

        public LinearLayout CalendarPageContentGrid => GetView(ref _calendarPageContentGrid, Resource.Id.CalendarPageContentGrid);

        public ProgressBar CalendarPageProgressBar => GetView(ref _calendarPageProgressBar, Resource.Id.CalendarPageProgressBar);

        public LinearLayout CalendarPageProgressBarGrid => GetView(ref _calendarPageProgressBarGrid, Resource.Id.CalendarPageProgressBarGrid);



    }
}
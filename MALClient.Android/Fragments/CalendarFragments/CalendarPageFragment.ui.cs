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

        public UserControls.PagerSlidingTabStrip CalendarPageTabStrip => GetView(ref _calendarPageTabStrip, Resource.Id.CalendarPageTabStrip);

        public ViewPager CalendarPageViewPager => GetView(ref _calendarPageViewPager, Resource.Id.CalendarPageViewPager);

        public LinearLayout CalendarPageContentGrid => GetView(ref _calendarPageContentGrid, Resource.Id.CalendarPageContentGrid);

        public ProgressBar CalendarPageProgressBar => GetView(ref _calendarPageProgressBar, Resource.Id.CalendarPageProgressBar);

        public LinearLayout CalendarPageProgressBarGrid => GetView(ref _calendarPageProgressBarGrid, Resource.Id.CalendarPageProgressBarGrid);



    }
}
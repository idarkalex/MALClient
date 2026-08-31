using System.Collections.Generic;
using System.Threading.Tasks;
using Android.Graphics;
using Android.OS;
using Android.Widget;
using GalaSoft.MvvmLight.Helpers;
using MALClient.Android.BindingConverters;
using MALClient.Android.Listeners;
using MALClient.Android.PagerAdapters;
using MALClient.Android.Resources;
using MALClient.Models.Enums;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALClient.Android.Fragments.CalendarFragments
{
    public partial class CalendarPageFragment : MalFragmentBase
    {

        private CalendarPageViewModel ViewModel;

        private CalendarPageFragment()
        {
            
        }

        protected override void Init(Bundle savedInstanceState)
        {
            ViewModelLocator.AnimeList.AnimeItemsDisplayContext = AnimeItemDisplayContext.AirDay;
            ViewModel = ViewModelLocator.CalendarPage;
            ViewModel.Init();
        }

        protected override void InitBindings()
        {                   
            Bindings.Add(
                this.SetBinding(() => ViewModel.ProgressValue,
                    () => CalendarPageProgressBar.Progress));
            Bindings.Add(
                this.SetBinding(() => ViewModel.MaxProgressValue,
                    () => CalendarPageProgressBar.Max));

            
            Bindings.Add(
                this.SetBinding(() => ViewModel.CalendarBuildingVisibility,
                    () => CalendarPageProgressBarGrid.Visibility).ConvertSourceToTarget(Converters.BoolToVisibility));

            
            Bindings.Add(this.SetBinding(() => ViewModel.CalendarData).WhenSourceChanges( async () =>
            {
                CalendarPageViewPager.Adapter = new CalendarPagerAdapter(ChildFragmentManager, ViewModel.CalendarData);
                CalendarPageTabStrip.SetViewPager(CalendarPageViewPager);
                CalendarPageTabStrip.CenterTabs();

                await Task.Delay(30);
                CalendarPageViewPager.SetCurrentItem(ViewModel.CalendarPivotIndex,false);
            }));

            CalendarPageViewPager.OffscreenPageLimit = 1;

            Bindings.Add(
                this.SetBinding(() => ViewModel.CalendarVisibility,
                    () => CalendarPageContentGrid.Visibility).ConvertSourceToTarget(Converters.BoolToVisibility));

            UpdateModeToggleButton();
            CalendarPageModePersonalButton.SetOnClickListener(new OnClickListener(v => SetCalendarMode(false)));
            CalendarPageModeAiringNowButton.SetOnClickListener(new OnClickListener(v => SetCalendarMode(true)));
        }

        public override int LayoutResourceId => Resource.Layout.CalendarPage;

        private void UpdateModeToggleButton()
        {
            bool allAiring = Settings.CalendarShowAllAiring;
            SetSectionActive(CalendarPageModePersonalButton, CalendarPageModePersonalIndicator, !allAiring);
            SetSectionActive(CalendarPageModeAiringNowButton, CalendarPageModeAiringNowIndicator, allAiring);
        }

        private void SetSectionActive(TextView label, global::Android.Views.View indicator, bool active)
        {
            var accent = new Color(ResourceExtension.AccentColour);
            if (active)
            {
                label.SetTextColor(accent);
                indicator.SetBackgroundColor(accent);
            }
            else
            {
                label.SetTextColor(new Color(ResourceExtension.BrushText));
                indicator.SetBackgroundColor(new Color(global::Android.Graphics.Color.Transparent));
            }
        }

        private void SetCalendarMode(bool allAiring)
        {
            if (Settings.CalendarShowAllAiring == allAiring)
                return;
            Settings.CalendarShowAllAiring = allAiring;
            UpdateModeToggleButton();
            ViewModel.Init(true);
        }

        public static CalendarPageFragment Instance => new CalendarPageFragment();
    }
}

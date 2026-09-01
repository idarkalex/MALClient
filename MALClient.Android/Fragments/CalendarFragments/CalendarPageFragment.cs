using System;
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
        private Task _initTask;

        private CalendarPageFragment()
        {
            
        }

        protected override void Init(Bundle savedInstanceState)
        {
            ViewModelLocator.AnimeList.AnimeItemsDisplayContext = AnimeItemDisplayContext.AirDay;
            ViewModel = ViewModelLocator.CalendarPage;
            _initTask = ViewModel.Init();
        }

        public override void OnResume()
        {
            base.OnResume();
            //Heal any undelivered visibility change whenever the page comes back to foreground
            SyncCalendarOverlay();
        }

        /// <summary>
        /// Binding-independent resolution of the build overlay: whenever the VM has finished
        /// building, the overlay is forced off and the content shown. Reports a warning when
        /// the overlay was visible although the VM was already done (i.e. a lost PropertyChanged).
        /// </summary>
        private void SyncCalendarOverlay()
        {
            if (Activity == null || RootView == null) return;
            try
            {
                if (CalendarPageProgressBarGrid.Visibility != global::Android.Views.ViewStates.Visible)
                    return;
                if (ViewModel.CalendarBuildingVisibility)
                    return;

                //Overlay visible while the VM is done - the binding never delivered the change
                MALClient.XShared.Utils.DiagnosticsReporter.Warn("Calendar", "overlay was visible while VM was done - forcibly cleared");
                CalendarPageProgressBarGrid.Visibility = global::Android.Views.ViewStates.Gone;
                CalendarPageContentGrid.Visibility = global::Android.Views.ViewStates.Visible;
            }
            catch (Exception ex)
            {
                MALClient.XShared.Utils.DiagnosticsReporter.Error("Calendar", "SyncCalendarOverlay exception", ex);
            }
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

            //Explicit initial-state sync - do not rely on binding delivery alone
            CalendarPageProgressBarGrid.Visibility = ViewModel.CalendarBuildingVisibility
                ? global::Android.Views.ViewStates.Visible
                : global::Android.Views.ViewStates.Gone;
            CalendarPageContentGrid.Visibility = ViewModel.CalendarVisibility
                ? global::Android.Views.ViewStates.Visible
                : global::Android.Views.ViewStates.Gone;

            //Once a view exists, complete the Init-driven resolution even if Init finished before inflation
            if (_initTask != null)
                _initTask.ContinueWith(_ => RootView?.Post(SyncCalendarOverlay));

            //Self-healing watchdog: unconditionally clear a staled overlay after 10s
            CalendarPageProgressBarGrid.PostDelayed(() =>
            {
                try
                {
                    if (Activity == null || IsDetached) return;
                    if (CalendarPageProgressBarGrid.Visibility != global::Android.Views.ViewStates.Visible)
                        return;
                    if (ViewModel.CalendarBuildingVisibility)
                        MALClient.XShared.Utils.DiagnosticsReporter.Error("Calendar", "watchdog fired: still building after 10s", null);
                    else
                        MALClient.XShared.Utils.DiagnosticsReporter.Warn("Calendar", "watchdog fired: overlay visible although VM done");
                    CalendarPageProgressBarGrid.Visibility = global::Android.Views.ViewStates.Gone;
                    CalendarPageContentGrid.Visibility = global::Android.Views.ViewStates.Visible;
                }
                catch (Exception ex)
                {
                    MALClient.XShared.Utils.DiagnosticsReporter.Error("Calendar", "watchdog exception", ex);
                }
            }, 10000);

            
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using GalaSoft.MvvmLight.Helpers;
using MALClient.Android.Listeners;
using MALClient.Android.ViewModels;

namespace MALClient.Android.Fragments.SettingsFragments
{
    public class SettingsMiscFragment : SettingsFragmentBase
    {
        protected override void InitBindings()
        {
            Bindings.Add(
                this.SetBinding(() => ViewModel.RatePopUpEnable,
                    () => SettingsPageMiscEnableReviewReminder.Checked,BindingMode.TwoWay));

            Bindings.Add(
                this.SetBinding(() => ViewModel.AskBeforeSendingCrashReports,
                    () => SettingsPageMiscAskBeforeCrashReports.Checked,BindingMode.TwoWay));

            SettingsPageMiscPageRateNowButton.SetOnClickListener(new OnClickListener(view => ViewModel.ReviewCommand.Execute(null)));
        }

        public override int LayoutResourceId => Resource.Layout.SettingsPageMisc;

        #region Views

        private Switch _settingsPageMiscEnableReviewReminder;
        private Button _settingsPageMiscPageRateNowButton;
        private Switch _settingsPageMiscAskBeforeCrashReports;

        public Switch SettingsPageMiscEnableReviewReminder => GetView(ref _settingsPageMiscEnableReviewReminder, Resource.Id.SettingsPageMiscEnableReviewReminder);

        public Button SettingsPageMiscPageRateNowButton => GetView(ref _settingsPageMiscPageRateNowButton, Resource.Id.SettingsPageMiscPageRateNowButton);

        public Switch SettingsPageMiscAskBeforeCrashReports => GetView(ref _settingsPageMiscAskBeforeCrashReports, Resource.Id.SettingsPageMiscAskBeforeCrashReports);



        #endregion
    }
}
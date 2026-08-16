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

namespace MALClient.Android.Fragments.SettingsFragments
{
    public partial class SettingsCalendarFragment
    {
        private CheckBox _settingsPageCalendarBuildOptionsWatchingCheckBox;
        private CheckBox _settingsPageCalendarBuildOptionsPlanToWatchCheckBox;
        private RadioButton _settingsPageCalendarStartPageRadioSummary;
        private RadioButton _settingsPageCalendarStartPageRadioToday;
        private RadioGroup _settingsPageCalendarStartPageRadioGroup;
        private Switch _settingsPageCalendarMiscFirstDaySwitch;
        private Switch _settingsPageCalendarMiscRemoveEmptyDaysSwitch;
        private EditText _settingsPageCalendarMiscMaxItemsEditText;
        //private Switch _settingsPageCalendarMiscExactAiringTimeSwitch;

        public CheckBox SettingsPageCalendarBuildOptionsWatchingCheckBox => GetView(ref _settingsPageCalendarBuildOptionsWatchingCheckBox, Resource.Id.SettingsPageCalendarBuildOptionsWatchingCheckBox);

        public CheckBox SettingsPageCalendarBuildOptionsPlanToWatchCheckBox => GetView(ref _settingsPageCalendarBuildOptionsPlanToWatchCheckBox, Resource.Id.SettingsPageCalendarBuildOptionsPlanToWatchCheckBox);

        public RadioButton SettingsPageCalendarStartPageRadioSummary => GetView(ref _settingsPageCalendarStartPageRadioSummary, Resource.Id.SettingsPageCalendarStartPageRadioSummary);

        public RadioButton SettingsPageCalendarStartPageRadioToday => GetView(ref _settingsPageCalendarStartPageRadioToday, Resource.Id.SettingsPageCalendarStartPageRadioToday);

        public RadioGroup SettingsPageCalendarStartPageRadioGroup => GetView(ref _settingsPageCalendarStartPageRadioGroup, Resource.Id.SettingsPageCalendarStartPageRadioGroup);

        public Switch SettingsPageCalendarMiscFirstDaySwitch => GetView(ref _settingsPageCalendarMiscFirstDaySwitch, Resource.Id.SettingsPageCalendarMiscFirstDaySwitch);

        public Switch SettingsPageCalendarMiscRemoveEmptyDaysSwitch => GetView(ref _settingsPageCalendarMiscRemoveEmptyDaysSwitch, Resource.Id.SettingsPageCalendarMiscRemoveEmptyDaysSwitch);

        public EditText SettingsPageCalendarMiscMaxItemsEditText => GetView(ref _settingsPageCalendarMiscMaxItemsEditText, Resource.Id.SettingsPageCalendarMiscMaxItemsEditText);

        //public Switch SettingsPageCalendarMiscExactAiringTimeSwitch => GetView(ref _settingsPageCalendarMiscExactAiringTimeSwitch, Resource.Id.SettingsPageCalendarMiscExactAiringTimeSwitch);
    }
}
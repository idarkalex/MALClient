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
    public partial class SettingsFeedsFragment
    {
        private Switch _settingsPageFeedsAddPinnedProfilesSwitch;
        private TextView _settingsPageFeedsMaximumEntriesTextView;
        private SeekBar _settingsPageFeedsMaximumEntriesSlider;
        private TextView _settingsPageFeedsElderEntriesTextView;
        private SeekBar _settingsPageFeedsElderEntriesSlider;

        public Switch SettingsPageFeedsAddPinnedProfilesSwitch => GetView(ref _settingsPageFeedsAddPinnedProfilesSwitch, Resource.Id.SettingsPageFeedsAddPinnedProfilesSwitch);

        public TextView SettingsPageFeedsMaximumEntriesTextView => GetView(ref _settingsPageFeedsMaximumEntriesTextView, Resource.Id.SettingsPageFeedsMaximumEntriesTextView);

        public SeekBar SettingsPageFeedsMaximumEntriesSlider => GetView(ref _settingsPageFeedsMaximumEntriesSlider, Resource.Id.SettingsPageFeedsMaximumEntriesSlider);

        public TextView SettingsPageFeedsElderEntriesTextView => GetView(ref _settingsPageFeedsElderEntriesTextView, Resource.Id.SettingsPageFeedsElderEntriesTextView);

        public SeekBar SettingsPageFeedsElderEntriesSlider => GetView(ref _settingsPageFeedsElderEntriesSlider, Resource.Id.SettingsPageFeedsElderEntriesSlider);
    }
}
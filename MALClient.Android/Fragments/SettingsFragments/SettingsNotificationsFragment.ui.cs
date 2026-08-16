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
    public partial class SettingsNotificationsFragment
    {
        private Switch _settingsPageNotificationsEnable;
        private Switch _settingsPageNotificationsCheckInRuntime;
        private LinearLayout _notificationsTypesCheckBoxGroup;
        private Spinner _settingsPageNotificationsFrequencySpinner;
        
        public Switch SettingsPageNotificationsEnable => GetView(ref _settingsPageNotificationsEnable, Resource.Id.SettingsPageNotificationsEnable);

        public Switch SettingsPageNotificationsCheckInRuntime => GetView(ref _settingsPageNotificationsCheckInRuntime, Resource.Id.SettingsPageNotificationsCheckInRuntime);

        public LinearLayout NotificationsTypesCheckBoxGroup => GetView(ref _notificationsTypesCheckBoxGroup, Resource.Id.NotificationsTypesCheckBoxGroup);

        public Spinner SettingsPageNotificationsFrequencySpinner => GetView(ref _settingsPageNotificationsFrequencySpinner, Resource.Id.SettingsPageNotificationsFrequencySpinner);
    }
}
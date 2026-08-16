using Android.OS;
using MALClient.Android.ViewModels;
using MALClient.XShared.ViewModels;

namespace MALClient.Android.Fragments.SettingsFragments
{
    public abstract class SettingsFragmentBase : MalFragmentBase
    {
        protected SettingsViewModel ViewModel;

        protected override void Init(Bundle savedInstanceState)
        {
            ViewModel = AndroidViewModelLocator.Settings;
        }
    }
}

using System;
using Android.Views;
using Com.Google.Android.Material.BottomNavigation;

namespace MALClient.Android.Listeners
{
    public class BottomNavigationItemSelectedListener : Java.Lang.Object, BottomNavigationView.IOnNavigationItemSelectedListener
    {
        private readonly Func<IMenuItem, bool> _action;

        public BottomNavigationItemSelectedListener(Func<IMenuItem, bool> action)
        {
            _action = action;
        }

        public bool OnNavigationItemSelected(IMenuItem item)
        {
            return _action?.Invoke(item) ?? true;
        }
    }
}

using System;
using Android.Views;

namespace MALClient.Android.Listeners
{
    public class BottomNavigationItemSelectedListener : Java.Lang.Object, Android.Support.Design.Widget.BottomNavigationView.IOnItemSelectedListener
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

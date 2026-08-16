using System;
using Android.Support.Design.Widget;
using Android.Views;

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

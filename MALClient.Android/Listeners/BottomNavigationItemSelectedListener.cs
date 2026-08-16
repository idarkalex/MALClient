using System;
using Android.Support.Design.Widget;
using Android.Views;

namespace MALClient.Android.Listeners
{
    public class BottomNavigationItemSelectedListener : Java.Lang.Object, BottomNavigationView.IOnNavigationItemSelectedListener
    {
        private readonly Action<IMenuItem> _action;

        public BottomNavigationItemSelectedListener(Action<IMenuItem> action)
        {
            _action = action;
        }

        public bool OnNavigationItemSelected(IMenuItem item)
        {
            _action?.Invoke(item);
            return true;
        }
    }
}

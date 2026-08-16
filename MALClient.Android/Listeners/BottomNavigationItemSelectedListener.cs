using System;
using Android.Runtime;
using Android.Views;

namespace MALClient.Android.Listeners
{
    public class BottomNavigationItemSelectedListener : Java.Lang.Object, global::Android.Support.Design.Widget.BottomNavigationView.IOnNavigationItemSelectedListener
    {
        private readonly Func<IMenuItem, bool> _action;

        public BottomNavigationItemSelectedListener(Func<IMenuItem, bool> action)
        {
            _action = action;
        }

        [Register("onNavigationItemSelected", "(Landroid/view/MenuItem;)Z", "GetOnNavigationItemSelected_Landroid_view_MenuItem_Handler")]
        public bool OnNavigationItemSelected(IMenuItem item)
        {
            return _action?.Invoke(item) ?? true;
        }
    }
}

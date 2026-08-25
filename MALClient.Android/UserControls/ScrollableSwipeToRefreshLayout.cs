using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.Widget;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace MALClient.Android.UserControls
{
    public class ScrollableSwipeToRefreshLayout : SwipeRefreshLayout
    {
        public ScrollableSwipeToRefreshLayout(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        public ScrollableSwipeToRefreshLayout(Context context) : base(context)
        {
        }

        public ScrollableSwipeToRefreshLayout(Context context, IAttributeSet attrs) : base(context, attrs)
        {
        }

        public View ScrollingView { get; set; }

        /// <summary>
        /// Returns the currently visible page's root view when ScrollingView is a
        /// ViewPager; lets CanChildScrollUp ask the real scrollable instead of the
        /// pager (which never reports vertical scroll itself).
        /// </summary>
        public Func<View> CurrentPageViewProvider { get; set; }

        public override bool CanChildScrollUp()
        {
            return CanScrollUpRecursive(CurrentPageViewProvider?.Invoke() ?? ScrollingView);
        }

        private static bool CanScrollUpRecursive(View view)
        {
            if (view == null)
                return true;
            if (view.CanScrollVertically(-1))
                return true;
            var group = view as ViewGroup;
            if (group == null)
                return false;
            for (var i = 0; i < group.ChildCount; i++)
                if (CanScrollUpRecursive(group.GetChildAt(i)))
                    return true;
            return false;
        }
    }
}
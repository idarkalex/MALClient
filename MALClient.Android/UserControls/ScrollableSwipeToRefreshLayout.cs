using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.View;
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
            var view = CurrentPageViewProvider?.Invoke();
            if (view == null && CurrentPageViewProvider != null)
                return true;
            return CanScrollUpRecursive(view ?? ScrollingView);
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

        private bool _forwardingNestedScroll;

        // Forward pre-scroll to parent (CoordinatorLayout → AppBarLayout.Behavior).
        // Lazy-start nested scrolling with CoordinatorLayout on first scroll event.
        public override void OnNestedPreScroll(View target, int dx, int dy, int[] consumed)
        {
            EnsureNestedScrollingStarted();
            int[] parentConsumed = new int[2];
            ViewCompat.DispatchNestedPreScroll(this, dx, dy, parentConsumed, null);
            if (consumed != null)
            {
                consumed[0] += parentConsumed[0];
                consumed[1] += parentConsumed[1];
            }
        }

        // Forward unconsumed scroll to parent
        public override void OnNestedScroll(View target, int dxConsumed, int dyConsumed, int dxUnconsumed, int dyUnconsumed)
        {
            EnsureNestedScrollingStarted();
            ViewCompat.DispatchNestedScroll(this, 0, 0, dxUnconsumed, dyUnconsumed, null);
        }

        // Stop nested scrolling with parent
        public override void OnStopNestedScroll(View target)
        {
            if (_forwardingNestedScroll)
            {
                ViewCompat.StopNestedScroll(this);
                _forwardingNestedScroll = false;
            }
            base.OnStopNestedScroll(target);
        }

        private void EnsureNestedScrollingStarted()
        {
            if (!_forwardingNestedScroll)
                _forwardingNestedScroll = ViewCompat.StartNestedScroll(this, (int)ScrollAxis.Vertical);
        }
    }
}

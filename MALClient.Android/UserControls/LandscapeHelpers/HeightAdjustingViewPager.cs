using System;
using System.Collections.Generic;
using Android.Content;
using Android.Content.Res;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.View;
using Android.Util;
using Android.Views;

namespace MALClient.Android.UserControls
{
    public class HeightAdjustingViewPager : ViewPager
    {
        private readonly Dictionary<int, int> _tabHeights = new Dictionary<int, int>();

        public HeightAdjustingViewPager(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        public HeightAdjustingViewPager(Context context) : base(context)
        {
        }

        public HeightAdjustingViewPager(Context context, IAttributeSet attrs) : base(context, attrs)
        {
        }

        /// <summary>
        /// Called by tab fragments after their content is fully loaded.
        /// Measures the view and stores the height for the current tab.
        /// </summary>
        public void SetTabHeightForCurrentView(View view)
        {
            Post(() =>
            {
                view.Measure(
                    MeasureSpec.MakeMeasureSpec(Width, MeasureSpecMode.Exactly),
                    MeasureSpec.MakeMeasureSpec(Resources.DisplayMetrics.HeightPixels * 3, MeasureSpecMode.AtMost));
                _tabHeights[CurrentItem] = view.MeasuredHeight;
                RequestLayout();
            });
        }

        /// <summary>
        /// Called by tab fragments after their content is fully loaded.
        /// Sets the height for the given tab index and triggers re-layout if active.
        /// </summary>
        public void SetTabHeight(int tabIndex, int height)
        {
            _tabHeights[tabIndex] = height;
            if (tabIndex == CurrentItem)
            {
                RequestLayout();
            }
        }

        /// <summary>
        /// Clears all cached heights (call when navigating to a different entry).
        /// </summary>
        public void ClearTabHeights()
        {
            _tabHeights.Clear();
            RequestLayout();
        }

        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            // CoordinatorLayout gives Exactly mode via scrolling_view_behavior:
            // the pager fills ALL remaining space below the AppBarLayout. Pass through.
            if (MeasureSpec.GetMode(heightMeasureSpec) == MeasureSpecMode.Exactly)
            {
                base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
                return;
            }

            // Fallback for non-CoordinatorLayout usage (wrap_content in ScrollView)
            if (ChildCount > 0)
            {
                var index = Math.Max(0, Math.Min(CurrentItem, ChildCount - 1));
                int height = 0;
                if (_tabHeights.TryGetValue(index, out var stored))
                    height = stored;
                else
                    height = (int)(Context.Resources.DisplayMetrics.HeightPixels * 0.6);
                heightMeasureSpec = MeasureSpec.MakeMeasureSpec(height, MeasureSpecMode.Exactly);
            }
            base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
        }
    }
}

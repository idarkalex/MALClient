using System;
using Android.Content;
using Android.Runtime;
using Android.Support.V4.View;
using Android.Util;
using Android.Views;

namespace MALClient.Android.UserControls
{
    public class HeightAdjustingViewPager : ViewPager
    {
        public HeightAdjustingViewPager(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        public HeightAdjustingViewPager(Context context) : base(context)
        {
        }

        public HeightAdjustingViewPager(Context context, IAttributeSet attrs) : base(context, attrs)
        {
        }

        public void SetTabHeightForCurrentView(View view) { }
        public void SetTabHeight(int tabIndex, int height) { }
        public void ClearTabHeights() { }
    }
}

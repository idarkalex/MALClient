using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
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
        private bool EnableAdjustments { get; set; }

        public HeightAdjustingViewPager(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        public HeightAdjustingViewPager(Context context) : base(context)
        {
            EnableAdjustments = true;
        }

        public HeightAdjustingViewPager(Context context, IAttributeSet attrs) : base(context, attrs)
        {
            EnableAdjustments = true;
        }

        protected override void OnConfigurationChanged(Configuration newConfig)
        {
            //EnableAdjustments = newConfig.Orientation == Orientation.Landscape;
            base.OnConfigurationChanged(newConfig);
        }

        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            // FIXED height: 1.5x screen. No probing, no child measurement, no stale heights.
            // Every tab gets the same height and scrolls internally via its own
            // ScrollView/ListView/RecyclerView. This eliminates ALL measurement issues.
            var height = (int)(Context.Resources.DisplayMetrics.HeightPixels * 1.5);
            heightMeasureSpec = MeasureSpec.MakeMeasureSpec(height, MeasureSpecMode.Exactly);
            base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
        }

        public void RefreshHeight()
        {
            Post(() => RequestLayout());
        }
    }
}

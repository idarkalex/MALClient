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
            if (EnableAdjustments && ChildCount > 0)
            {
                var index = Math.Max(0, Math.Min(CurrentItem, ChildCount - 1));
                var child = GetChildAt(index);
                int height = 0;
                if (child != null)
                {
                    // Cap at 55% of screen: tabs taller than this scroll internally (virtualized = fast).
                    // Prevents infinite bars, giant gaps, and cut-off tabs regardless of content timing.
                    var maxH = (int)(Context.Resources.DisplayMetrics.HeightPixels * 0.55);
                    child.Measure(widthMeasureSpec, MeasureSpec.MakeMeasureSpec(maxH, MeasureSpecMode.AtMost));
                    height = child.MeasuredHeight;
                }
                heightMeasureSpec = MeasureSpec.MakeMeasureSpec(height, MeasureSpecMode.Exactly);
            }
            base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
        }

        public void RefreshHeight()
        {
            Post(() => RequestLayout());
        }
    }
}

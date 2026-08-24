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
                int height = 0;
                for (int i = 0; i < ChildCount; i++)
                {
                    var child = GetChildAt(i);
                    if (child == null) continue;
                    // AtMost(2x screen): ScrollViews report full content,
                    // ListViews measure ALL rows (not just 1 like Unspecified)
                    child.Measure(widthMeasureSpec, MeasureSpec.MakeMeasureSpec(
                        Context.Resources.DisplayMetrics.HeightPixels * 2, MeasureSpecMode.AtMost));
                    int h = child.MeasuredHeight;
                    if (h > height) height = h;
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

using System;
using System.Collections.Generic;
using Android.Widget;
using global::Android.Support.V7.Widget;

namespace MALClient.Android.Utilities
{
    /// <summary>
    /// Helpers for saving and restoring scroll state of lists/grids/recyclers into a
    /// VM's UiState dictionary. Use in OnPause/OnResume pairs (or OnDestroyView/OnCreateView).
    /// </summary>
    public static class ScrollStateHelper
    {
        public static void SaveAbsListView(AbsListView view, Dictionary<string, object> uiState, string key)
        {
            if (view == null || uiState == null || string.IsNullOrEmpty(key))
                return;
            try
            {
                var pos = view.FirstVisiblePosition;
                var offset = 0;
                var child = view.GetChildAt(0);
                if (child != null)
                    offset = -child.Top;
                uiState[key] = (pos, offset);
            } catch { }
        }

        public static void RestoreAbsListView(AbsListView view, Dictionary<string, object> uiState, string key)
        {
            if (view == null || uiState == null || string.IsNullOrEmpty(key))
                return;
            try
            {
                if (uiState.TryGetValue(key, out var stateObj) && stateObj is ValueTuple<int, int> v)
                {
                    view.Post(() =>
                    {
                        try { view.SetSelectionFromTop(v.Item1, v.Item2); } catch { }
                    });
                }
            } catch { }
        }

        public static void SaveRecyclerView(RecyclerView view, Dictionary<string, object> uiState, string key)
        {
            if (view == null || uiState == null || string.IsNullOrEmpty(key))
                return;
            try
            {
                var lm = view.GetLayoutManager();
                if (lm == null)
                    return;
                int pos = -1;
                int offset = 0;
                if (lm is GridLayoutManager grid)
                {
                    pos = grid.FindFirstVisibleItemPosition();
                    var firstView = view.GetChildAt(0);
                    if (firstView != null)
                        offset = firstView.Top;
                }
                else if (lm is LinearLayoutManager linear)
                {
                    pos = linear.FindFirstVisibleItemPosition();
                    var firstView = view.GetChildAt(0);
                    if (firstView != null)
                        offset = firstView.Top;
                }
                if (pos >= 0)
                    uiState[key] = (pos, offset);
            } catch { }
        }

        public static void RestoreRecyclerView(RecyclerView view, Dictionary<string, object> uiState, string key)
        {
            if (view == null || uiState == null || string.IsNullOrEmpty(key))
                return;
            try
            {
                if (uiState.TryGetValue(key, out var stateObj) && stateObj is ValueTuple<int, int> v && v.Item1 >= 0)
                {
                    view.Post(() =>
                    {
                        try
                        {
                            var lm = view.GetLayoutManager();
                            if (lm is GridLayoutManager grid)
                                grid.ScrollToPositionWithOffset(v.Item1, v.Item2);
                            else if (lm is LinearLayoutManager linear)
                                linear.ScrollToPositionWithOffset(v.Item1, v.Item2);
                        } catch { }
                    });
                }
            } catch { }
        }

        public static void SaveScrollY(int scrollY, Dictionary<string, object> uiState, string key)
        {
            if (uiState == null || string.IsNullOrEmpty(key))
                return;
            uiState[key] = scrollY;
        }

        public static int RestoreScrollY(Dictionary<string, object> uiState, string key)
        {
            if (uiState == null || string.IsNullOrEmpty(key))
                return 0;
            if (uiState.TryGetValue(key, out var v) && v is int i)
                return i;
            return 0;
        }
    }
}

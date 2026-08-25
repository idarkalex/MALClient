using System;
using System.Collections.Generic;
using System.ComponentModel;
using Android.Views;
using Android.Widget;
using FFImageLoading.Extensions;
using GalaSoft.MvvmLight.Helpers;
using MALClient.Android.UserControls;

namespace MALClient.Android
{
    public static class FlingCollectionsHelper
    {
        private static readonly Dictionary<AbsListView, bool> FlingStates = new Dictionary<AbsListView, bool>();
        private static readonly Dictionary<AbsListView, Dictionary<View, object>> ViewHolders = new Dictionary<AbsListView, Dictionary<View, object>>();

        public static void InjectFlingAdapter<T>(this AbsListView container, IList<T> items,
            Action<View,int, T> dataTemplateFull, Action<View,int,T> dataTemplateFling,
            Func<int,View> containerTemplate,Action<View,int,T> dataTemplateBasic = null,View footer = null,bool skipBugFix = false) where T : class
        {
            if(!FlingStates.ContainsKey(container))
                FlingStates.Add(container,false);
            HookCollectionChanged(container, items);
            container.MakeFlingAware(b =>
            {
                if(FlingStates[container] == b)
                    return;
                FlingStates[container] = b;
                if (!b)
                {
                    for (int i = 0; i < container.ChildCount; i++)
                    {
                        var view = container.GetChildAt(i);
                        var item = view.Tag.Unwrap<T>();
                        if (view.Tag?.ToString() == "Footer")
                            continue;  
                        dataTemplateFull(view,items.IndexOf(item),item);
                    }
                }
            });
            if (footer == null)
            {
                container.Adapter = new LiveFlingAdapter<T>(container, items,
                    (root, i, arg2) =>
                    {
                        dataTemplateBasic?.Invoke(root, i, arg2);
                        if (FlingStates[container])
                            dataTemplateFling(root, i, arg2);
                        else
                            dataTemplateFull(root, i, arg2);
                    },
                    containerTemplate);
            }
            else
            {
                container.Adapter = new LiveFlingAdapter<T>(container, items,
                    (root, i, arg2) =>
                    {
                        if (FlingStates[container])
                            dataTemplateFling(root, i, arg2);
                        else
                            dataTemplateFull(root, i, arg2);
                    },
                    containerTemplate,
                    footer);

            }

        }

        public static void InjectFlingAdapter<T, TViewHolder>(this AbsListView container, IList<T> items, Func<View, TViewHolder> holderFactory,
            Action<View, int, T, TViewHolder> dataTemplateFull, Action<View, int, T, TViewHolder> dataTemplateFling,
            Action<View, int, T, TViewHolder> dataTemplateBasic, Func<int, View> containerTemplate, View footer = null, bool skipBugFix = false,Action onScrolled = null) where T : class
        {
            if (!FlingStates.ContainsKey(container))
                FlingStates.Add(container, false);
            if (!ViewHolders.ContainsKey(container))
                ViewHolders.Add(container, new Dictionary<View, object>());
            HookCollectionChanged(container, items);
            if (onScrolled == null)
            {
                container.MakeFlingAware(b =>
                {
                    if (FlingStates[container] == b)
                        return;
                    FlingStates[container] = b;
                    if (!b)
                    {
                        for (int i = 0; i < container.ChildCount; i++)
                        {
                            var view = container.GetChildAt(i);
                            var item = view.Tag.Unwrap<T>();
                            if (view.Tag?.ToString() == "Footer")
                                continue;
                            dataTemplateFull(view, items.IndexOf(item), item, (TViewHolder)ViewHolders[container][view]);
                        }
                    }
                });
            }
            else
            {
                container.MakeFlingAware(b =>
                {
                    onScrolled.Invoke();
                    if (FlingStates[container] == b)
                        return;
                    FlingStates[container] = b;
                    if (!b)
                    {
                        for (int i = 0; i < container.ChildCount; i++)
                        {
                            var view = container.GetChildAt(i);
                            var item = view.Tag.Unwrap<T>();
                            if (view.Tag?.ToString() == "Footer")
                                continue;
                            dataTemplateFull(view, items.IndexOf(item), item, (TViewHolder)ViewHolders[container][view]);
                        }
                    }
                });
            }

            if (footer == null)
            {
                container.Adapter = new LiveFlingAdapter<T>(container, items,
                    (root, i, arg2) =>
                    {
                        TViewHolder holder;
                        if (!ViewHolders[container].ContainsKey(root))
                            ViewHolders[container][root] = holderFactory(root);
                        holder = (TViewHolder)ViewHolders[container][root];
                        dataTemplateBasic.Invoke(root, i, arg2, holder);
                        if (FlingStates[container])
                            dataTemplateFling(root, i, arg2, holder);
                        else
                            dataTemplateFull(root, i, arg2, holder);
                    },
                    containerTemplate);
            }
            else
            {
                container.Adapter = new LiveFlingAdapter<T>(container, items,
                    (root, i, arg2) =>
                    {
                        TViewHolder holder;
                        if (!ViewHolders[container].ContainsKey(root))
                            ViewHolders[container][root] = holderFactory(root);
                        holder = (TViewHolder)ViewHolders[container][root];
                        if (FlingStates[container])
                            dataTemplateFling(root, i, arg2, holder);
                        else
                            dataTemplateFull(root, i, arg2, holder);
                    },
                    containerTemplate,
                    footer);
            }

        }

        public static void ClearFlingAdapter(this AbsListView container)
        {
            if (FlingStates.ContainsKey(container))
                FlingStates.Remove(container);
            if (ViewHolders.ContainsKey(container))
                ViewHolders.Remove(container);
            
            container.SetOnScrollListener(null);
            container.Adapter = null;
        }

        private static UserControls.HeightAdjustingViewPager FindViewPager(View view)
        {
            var parent = view.Parent;
            while (parent != null)
            {
                if (parent is UserControls.HeightAdjustingViewPager vp) return vp;
                parent = (parent as View)?.Parent;
            }
            return null;
        }

        private static void HookCollectionChanged<T>(AbsListView container, IList<T> items) where T : class
        {
            var notifying = items as System.Collections.Specialized.INotifyCollectionChanged;
            if (notifying == null)
                return;
            System.Collections.Specialized.NotifyCollectionChangedEventHandler handler = (s, e) =>
            {
                (container.Adapter as BaseAdapter)?.NotifyDataSetChanged();
                var pager = FindViewPager(container);
                pager?.SetTabHeightForCurrentView(container);
            };
            notifying.CollectionChanged += handler;
        }

        private class LiveFlingAdapter<T> : BaseAdapter<T> where T : class
        {
            private readonly AbsListView _container;
            private readonly IList<T> _items;
            private readonly Action<View, int, T> _bind;
            private readonly Func<int, View> _containerTemplate;
            private readonly View _footer;

            public LiveFlingAdapter(AbsListView container, IList<T> items, Action<View, int, T> bind,
                Func<int, View> containerTemplate, View footer = null)
            {
                _container = container;
                _items = items;
                _bind = bind;
                _containerTemplate = containerTemplate;
                _footer = footer;
            }

            public override int Count => _items.Count + (_footer != null ? 1 : 0);

            public override T this[int position] => position < _items.Count ? _items[position] : null;

            public override long GetItemId(int position) => position;

            public override View GetView(int position, View convertView, ViewGroup parent)
            {
                if (_footer != null && position >= _items.Count)
                    return _footer;

                var item = _items[position];
                var root = convertView ?? _containerTemplate(position);
                root.Tag = item.Wrap();
                _bind(root, position, item);
                return root;
            }
        }
    }
}



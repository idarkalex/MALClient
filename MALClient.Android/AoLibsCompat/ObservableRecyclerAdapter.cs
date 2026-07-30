using System;
using System.Collections.Generic;
using Android.Content;
using Android.Support.V7.Widget;
using Android.Views;

namespace MALClient.Android.AoLibsCompat
{
    public class ObservableRecyclerAdapter<T, THolder> : RecyclerView.Adapter
        where THolder : RecyclerView.ViewHolder
    {
        private readonly IList<T> _source;
        private readonly Action<T, THolder, int> _dataTemplate;
        private readonly LayoutInflater _inflater;
        private readonly int? _layoutResource;
        private readonly Func<int, View> _itemTemplate;
        private readonly Func<ViewGroup, int, View, THolder> _holderFactory;

        public bool StretchContentHorizonatally { get; set; }

        public ObservableRecyclerAdapter(
            IList<T> source,
            Action<T, THolder, int> dataTemplate,
            LayoutInflater inflater,
            int layoutResource)
        {
            _source = source;
            _dataTemplate = dataTemplate;
            _inflater = inflater;
            _layoutResource = layoutResource;
            _itemTemplate = null;
            _holderFactory = null;
        }

        public ObservableRecyclerAdapter(
            IList<T> source,
            Action<T, THolder, int> dataTemplate,
            Func<int, View> itemTemplate)
        {
            _source = source;
            _dataTemplate = dataTemplate;
            _inflater = null;
            _layoutResource = null;
            _itemTemplate = itemTemplate;
            _holderFactory = null;
        }

        public ObservableRecyclerAdapter(
            IList<T> source,
            Action<T, THolder, int> dataTemplate,
            Func<int, View> itemTemplate,
            Func<ViewGroup, int, View, THolder> holderFactory)
        {
            _source = source;
            _dataTemplate = dataTemplate;
            _inflater = null;
            _layoutResource = null;
            _itemTemplate = itemTemplate;
            _holderFactory = holderFactory;
        }

        public override int ItemCount => _source?.Count ?? 0;

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var typedHolder = (THolder)holder;
            _dataTemplate(_source[position], typedHolder, position);

            if (StretchContentHorizonatally)
            {
                var lp = typedHolder.ItemView.LayoutParameters;
                if (lp != null)
                {
                    lp.Width = ViewGroup.LayoutParams.MatchParent;
                    typedHolder.ItemView.LayoutParameters = lp;
                }
            }
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            View view;
            if (_itemTemplate != null)
            {
                view = _itemTemplate(viewType);
            }
            else if (_inflater != null && _layoutResource.HasValue)
            {
                view = _inflater.Inflate(_layoutResource.Value, parent, false);
            }
            else
            {
                throw new InvalidOperationException("No item template or layout resource provided.");
            }

            if (StretchContentHorizonatally)
            {
                var lp = view.LayoutParameters;
                if (lp == null)
                    view.LayoutParameters = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
                else
                {
                    lp.Width = ViewGroup.LayoutParams.MatchParent;
                    view.LayoutParameters = lp;
                }
            }

            if (_holderFactory != null)
                return _holderFactory(parent, viewType, view);

            return (THolder)Activator.CreateInstance(typeof(THolder), view);
        }
    }

    public class ObservableRecyclerAdapterWithMultipleViewTypes<T, THolder> : RecyclerView.Adapter
        where THolder : RecyclerView.ViewHolder
    {
        private readonly Dictionary<Type, int> _typeToIndex;
        private readonly IItemEntry[] _entries;
        private readonly IList<T> _source;

        public bool StretchContentHorizonatally { get; set; }

        public ObservableRecyclerAdapterWithMultipleViewTypes(
            IDictionary<Type, IItemEntry> entries,
            IList<T> source)
        {
            _entries = new IItemEntry[entries.Count];
            _typeToIndex = new Dictionary<Type, int>();
            int i = 0;
            foreach (var kvp in entries)
            {
                _typeToIndex[kvp.Key] = i;
                _entries[i] = kvp.Value;
                i++;
            }
            _source = source;
        }

        public override int ItemCount => _source?.Count ?? 0;

        public override int GetItemViewType(int position)
        {
            var item = _source[position];
            var t = item.GetType();
            if (_typeToIndex.TryGetValue(t, out var idx))
                return idx;
            return 0;
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var entry = _entries[viewType];
            var view = entry.ItemTemplate(viewType);
            return entry.CreateHolder(parent, viewType, view);
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var item = _source[position];
            var t = item.GetType();
            if (_typeToIndex.TryGetValue(t, out var idx))
            {
                _entries[idx].Bind(item, (THolder)holder, position);
            }

            if (StretchContentHorizonatally)
            {
                var lp = holder.ItemView.LayoutParameters;
                if (lp != null)
                {
                    lp.Width = ViewGroup.LayoutParams.MatchParent;
                    holder.ItemView.LayoutParameters = lp;
                }
            }
        }

        public interface IItemEntry
        {
            Func<int, View> ItemTemplate { get; }
            RecyclerView.ViewHolder CreateHolder(ViewGroup parent, int viewType, View view);
            void Bind(T item, THolder holder, int position);
        }

        public class SpecializedItemEntry<TItem, TItemHolder> : IItemEntry
            where TItem : T
            where TItemHolder : THolder
        {
            public Func<int, View> ItemTemplate { get; set; }
            public Action<TItem, TItemHolder, int> SpecializedDataTemplate { get; set; }

            public RecyclerView.ViewHolder CreateHolder(ViewGroup parent, int viewType, View view)
            {
                return (TItemHolder)Activator.CreateInstance(typeof(TItemHolder), view);
            }
            public void Bind(T item, THolder holder, int position)
            {
                SpecializedDataTemplate((TItem)item, (TItemHolder)holder, position);
            }
        }
    }
}

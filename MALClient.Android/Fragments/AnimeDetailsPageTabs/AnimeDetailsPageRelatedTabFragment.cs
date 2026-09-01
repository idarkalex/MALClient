using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using FFImageLoading.Views;
using GalaSoft.MvvmLight.Helpers;
using MALClient.Android.Activities;
using MALClient.Android.AoLibsCompat;
using MALClient.Android.BindingConverters;
using MALClient.Android.Listeners;
using MALClient.Android.Resources;
using MALClient.Android.Utilities.ImageLoading;
using MALClient.Models.Enums;
using MALClient.Models.Models.AnimeScrapped;
using MALClient.XShared.Comm.Anime;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Details;

namespace MALClient.Android.Fragments.AnimeDetailsPageTabs
{
    class AnimeDetailsPageRelatedTabFragment : MalFragmentBase
    {
        private AnimeDetailsPageViewModel ViewModel;

        private AnimeDetailsPageRelatedTabFragment()
        {
            ViewModel = ViewModelLocator.AnimeDetails;
        }

        public override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            RetainInstance = true;
        }

        protected override void Init(Bundle savedInstanceState)
        {

        }

        protected override void InitBindings()
        {
            Bindings.Add(
                this.SetBinding(() => ViewModel.LoadingRelated,
                    () => AnimeDetailsPageRelatedTabLoadingOverlay.Visibility).ConvertSourceToTarget(Converters.BoolToVisibility));

            Bindings.Add(
                this.SetBinding(() => ViewModel.LoadingRelated).WhenSourceChanges(() =>
                {
                    if (ViewModel.LoadingRelated)
                    {
                        AnimeDetailsPageRelatedTabsList.SetAdapter(null);
                    }
                    else
                    {
                        if (ViewModel.RelatedAnime == null || !ViewModel.RelatedAnime.Any())
                        {
                            AnimeDetailsPageRelatedTabsList.SetAdapter(null);
                            return;
                        }
                        AnimeDetailsPageRelatedTabsList.SetAdapter(
                            new ObservableRecyclerAdapter<RelatedAnimeData, RelatedHolder>(
                                ViewModel.RelatedAnime, BindRelated, Activity.LayoutInflater, Resource.Layout.AnimeRelatedItem));
                    }
                }));

            Bindings.Add(
                this.SetBinding(() => ViewModel.NoRelatedDataNoticeVisibility,
                    () => AnimeDetailsPageRelatedTabEmptyNotice.Visibility)
                    .ConvertSourceToTarget(Converters.BoolToVisibility));

            AnimeDetailsPageRelatedTabsList.SetLayoutManager(new LinearLayoutManager(Activity));
            AnimeDetailsPageRelatedTabsList.AddOnScrollListener(new CustomScrollListener());

            ViewModel.LoadRelatedAnime();
        }

        private void BindRelated(RelatedAnimeData data, RelatedHolder holder, int position)
        {
            holder.Content.Text =
                string.IsNullOrEmpty(data.WholeRelation)
                    ? data.Title
                    : $"{data.WholeRelation.TrimEnd()} · {data.Title}";

            var img = holder.Image;
            if (!string.IsNullOrEmpty(data.ImgUrl))
            {
                img.Visibility = ViewStates.Visible;
                img.Into(data.ImgUrl);
            }
            else
            {
                string link = null;
                if (AnimeImageQuery.IsCached(data.Id, data.Type == RelatedItemType.Anime, ref link))
                {
                    img.Visibility = ViewStates.Visible;
                    img.Into(link);
                }
                else
                {
                    img.IntoWithTask(AnimeImageQuery.GetImageUrl(data.Id, data.Type == RelatedItemType.Anime));
                }
            }

            holder.RootContainer.Click -= OnItemClick;
            holder.RootContainer.Tag = data.Wrap();
            holder.RootContainer.Click += OnItemClick;
        }

        private void OnItemClick(object sender, EventArgs eventArgs)
        {
            var view = sender as View;
            var tag = view?.Tag?.Unwrap<RelatedAnimeData>();
            if (tag != null)
                ViewModel.NavigateDetailsCommand.Execute(tag);
        }

        public static AnimeDetailsPageRelatedTabFragment Instance => new AnimeDetailsPageRelatedTabFragment();

        public override int LayoutResourceId => Resource.Layout.AnimeDetailsPageRelatedTab;

        #region Views
        private RecyclerView _animeDetailsPageRelatedTabsList;
        private TextView _animeDetailsPageRelatedTabEmptyNotice;
        private RelativeLayout _animeDetailsPageRelatedTabLoadingOverlay;

        public RecyclerView AnimeDetailsPageRelatedTabsList => GetView(ref _animeDetailsPageRelatedTabsList, Resource.Id.AnimeDetailsPageRelatedTabsList);

        public TextView AnimeDetailsPageRelatedTabEmptyNotice => GetView(ref _animeDetailsPageRelatedTabEmptyNotice, Resource.Id.AnimeDetailsPageRelatedTabEmptyNotice);

        public RelativeLayout AnimeDetailsPageRelatedTabLoadingOverlay => GetView(ref _animeDetailsPageRelatedTabLoadingOverlay, Resource.Id.AnimeDetailsPageRelatedTabLoadingOverlay);

        #endregion

        class RelatedHolder : RecyclerView.ViewHolder
        {
            private readonly View _view;

            public RelatedHolder(View view) : base(view)
            {
                _view = view;
            }

            private TextView _content;
            private ImageViewAsync _image;
            private View _rootContainer;

            public TextView Content => _content ?? (_content = _view.FindViewById<TextView>(Resource.Id.AnimeRelatedItemContent));
            public ImageViewAsync Image => _image ?? (_image = _view.FindViewById<ImageViewAsync>(Resource.Id.Image));
            public View RootContainer => _rootContainer ?? (_rootContainer = _view.FindViewById(Resource.Id.RootContainer));
        }
    }
}

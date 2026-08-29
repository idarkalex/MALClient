using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using Android.App;
using Android.OS;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using GalaSoft.MvvmLight.Helpers;
using MALClient.Android.Activities;
using MALClient.Android.AoLibsCompat;
using MALClient.Android.BindingConverters;
using MALClient.Android.Flyouts;
using MALClient.Android.Listeners;
using MALClient.Android.Resources;
using MALClient.Android.Utilities.ImageLoading;
using MALClient.Models.Models.Anime;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Details;

namespace MALClient.Android.Fragments.AnimeDetailsPageTabs
{
    internal class AnimeDetailsPageEpisodesTabFragment : MalFragmentBase
    {
        private readonly AnimeDetailsPageViewModel ViewModel;
        private global::Android.Widget.PopupMenu _epPopupMenu;
        private readonly NotifyCollectionChangedEventHandler _episodesChangedHandler;

        private RecyclerView _animeDetailsPageEpisodesTabList;
        private RelativeLayout _animeDetailsPageEpisodesTabLoadingOverlay;

        public RecyclerView AnimeDetailsPageEpisodesTabList => GetView(ref _animeDetailsPageEpisodesTabList, Resource.Id.AnimeDetailsPageEpisodesTabList);

        public RelativeLayout AnimeDetailsPageEpisodesTabLoadingOverlay => GetView(ref _animeDetailsPageEpisodesTabLoadingOverlay, Resource.Id.AnimeDetailsPageEpisodesTabLoadingOverlay);

        private AnimeDetailsPageEpisodesTabFragment()
        {
            ViewModel = ViewModelLocator.AnimeDetails;
            _episodesChangedHandler = OnEpisodesChanged;
        }

        public override int LayoutResourceId => Resource.Layout.AnimeDetailsPageEpisodesTab;

        public override void OnDestroy()
        {
            ViewModel.Episodes.CollectionChanged -= _episodesChangedHandler;
            base.OnDestroy();
        }

        private void OnEpisodesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (ViewModel.LoadingDetails)
                return;
            try
            {
                BindEpisodes();
            }
            catch (Exception ex)
            {
                MainActivity.WriteCrashLog("EpisodesTab bind (Episodes changed)", ex);
            }
        }

        public static AnimeDetailsPageEpisodesTabFragment Instance => new AnimeDetailsPageEpisodesTabFragment();

        protected override void Init(Bundle savedInstanceState)
        {

        }

        protected override void InitBindings()
        {
            Bindings.Add(
                this.SetBinding(() => ViewModel.LoadingDetails,
                    () => AnimeDetailsPageEpisodesTabLoadingOverlay.Visibility).ConvertSourceToTarget(Converters.BoolToVisibility));

            Bindings.Add(this.SetBinding(() => ViewModel.LoadingDetails).WhenSourceChanges(() =>
            {
                if (ViewModel.LoadingDetails)
                    return;

                try
                {
                    BindEpisodes();
                }
                catch (Exception ex)
                {
                    MainActivity.WriteCrashLog("EpisodesTab bind", ex);
                }
            }));

            ViewModel.Episodes.CollectionChanged += _episodesChangedHandler;

            ViewModel.LoadEpisodes();

            Bindings.Add(this.SetBinding(() => ViewModel.MyEpisodes).WhenSourceChanges(() =>
            {
                if (!ViewModel.LoadingDetails)
                    BindEpisodes();
            }));

            AnimeDetailsPageEpisodesTabList.SetLayoutManager(new LinearLayoutManager(Activity));
            AnimeDetailsPageEpisodesTabList.AddOnScrollListener(new CustomScrollListener());

            BindEpisodes();
        }

        private void BindEpisodes()
        {
            MainActivity.WriteCrashLog($"EpisodesTab bind: EP={ViewModel.Episodes.Count}", null);

            if (!ViewModel.Episodes.Any())
            {
                AnimeDetailsPageEpisodesTabList.SetAdapter(null);
                return;
            }

            AnimeDetailsPageEpisodesTabList.SetAdapter(
                new ObservableRecyclerAdapter<AnimeEpisode, EpHolder>(
                    ViewModel.Episodes, BindEpisode, LayoutInflater, Resource.Layout.DetailAnimeEpisodeView));
        }

        private void BindEpisode(AnimeEpisode ep, EpHolder holder, int position)
        {
            holder.EpisodeCount.Text = $"Ep. {ep.EpisodeId}";
            holder.EpisodeName.Text = ep.Title;

            holder.TickIcon.Visibility = ep.EpisodeId <= ViewModel.MyEpisodes ? ViewStates.Visible : ViewStates.Gone;

            if (string.IsNullOrEmpty(ep.TitleJapanese) && string.IsNullOrEmpty(ep.TitleRomanji) &&
                string.IsNullOrEmpty(ep.ForumUrl) && string.IsNullOrEmpty(ep.VideoUrl))
            {
                holder.MoreButton.Visibility = ViewStates.Gone;
            }
            else
            {
                holder.MoreButton.Visibility = ViewStates.Visible;
                holder.MoreButton.SetOnClickListener(new OnClickListener(v =>
                {
                    _epPopupMenu = new global::Android.Widget.PopupMenu(Activity, holder.MoreButton);

                    if (!string.IsNullOrEmpty(ep.VideoUrl))
                        _epPopupMenu.Menu.Add(0, 0, 0, "Open website");
                    if (!string.IsNullOrEmpty(ep.ForumUrl))
                        _epPopupMenu.Menu.Add(0, 1, 0, "Forum discussion");
                    if (!string.IsNullOrEmpty(ep.TitleJapanese) || !string.IsNullOrEmpty(ep.TitleRomanji))
                        _epPopupMenu.Menu.Add(0, 2, 0, "Alternate titles");
                    _epPopupMenu.SetOnMenuItemClickListener(new AnimeItemFlyoutBuilder.MenuListener(item =>
                    {
                        if (item.ItemId == 0)
                        {
                            ViewModelLocator.AnimeDetails.OpenWebPageInApp(ep.VideoUrl);
                        }
                        else if (item.ItemId == 1)
                        {
                            ViewModel.NavigateEpDiscussionCommand.Execute(ep);
                        }
                        else if (item.ItemId == 2)
                        {
                            var content = "";
                            if (!string.IsNullOrEmpty(ep.TitleJapanese))
                                content += $"Japanese: {ep.TitleJapanese}\n\n";
                            if (!string.IsNullOrEmpty(ep.TitleRomanji))
                                content += $"Romaji: {ep.TitleRomanji}";

                            ResourceLocator.MessageDialogProvider.ShowMessageDialog(content, "Alternate titles");
                        }
                    }));
                    _epPopupMenu.Show();
                }));
            }

            holder.ItemView.SetOnClickListener(new OnClickListener(v =>
            {
                if (!string.IsNullOrEmpty(ep.ForumUrl))
                    ViewModel.NavigateEpDiscussionCommand.Execute(ep);
                else if (!string.IsNullOrEmpty(ep.VideoUrl))
                    ViewModelLocator.AnimeDetails.OpenWebPageInApp(ep.VideoUrl);
            }));

            if (ep.Filler || ep.Recap)
            {
                holder.EpisodeNote.Visibility = ViewStates.Visible;
                holder.EpisodeNote.Text = $"{(ep.Filler ? "Filler " : "")} {(ep.Recap ? "Recap" : "")}".Trim();
            }
            else
            {
                holder.EpisodeNote.Visibility = ViewStates.Gone;
            }

            if (ep.AiredDate.HasValue)
            {
                holder.EpisodeDate.Visibility = ViewStates.Visible;
                holder.EpisodeDate.Text = ep.AiredDate.Value.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
            }
            else
            {
                holder.EpisodeDate.Visibility = ViewStates.Gone;
            }
        }

        class EpHolder : RecyclerView.ViewHolder
        {
            private readonly View _view;

            public EpHolder(View view) : base(view)
            {
                _view = view;
            }

            private TextView _episodeCount;
            private TextView _episodeName;
            private View _tickIcon;
            private View _moreButton;
            private TextView _episodeNote;
            private TextView _episodeDate;

            public TextView EpisodeCount => _episodeCount ?? (_episodeCount = _view.FindViewById<TextView>(Resource.Id.EpisodeCount));
            public TextView EpisodeName => _episodeName ?? (_episodeName = _view.FindViewById<TextView>(Resource.Id.EpisodeName));
            public View TickIcon => _tickIcon ?? (_tickIcon = _view.FindViewById(Resource.Id.TickIcon));
            public View MoreButton => _moreButton ?? (_moreButton = _view.FindViewById(Resource.Id.MoreButton));
            public TextView EpisodeNote => _episodeNote ?? (_episodeNote = _view.FindViewById<TextView>(Resource.Id.EpisodeNote));
            public TextView EpisodeDate => _episodeDate ?? (_episodeDate = _view.FindViewById<TextView>(Resource.Id.EpisodeDate));
        }
    }
}

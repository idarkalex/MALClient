using System;
using System.Linq;
using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using GalaSoft.MvvmLight.Helpers;
using MALClient.Android.Activities;
using MALClient.Android.BindingConverters;
using MALClient.Android.CollectionAdapters;
using MALClient.Android.Flyouts;
using MALClient.Android.Listeners;
using MALClient.Android.Resources;
using MALClient.Models.Models.Anime;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Details;

namespace MALClient.Android.Fragments.AnimeDetailsPageTabs
{
    internal class AnimeDetailsPageEpisodesTabFragment : MalFragmentBase
    {
        private readonly AnimeDetailsPageViewModel ViewModel;
        private PopupMenu _epPopupMenu;

        private ListView _animeDetailsPageEpisodesTabList;
        private RelativeLayout _animeDetailsPageEpisodesTabLoadingOverlay;

        public ListView AnimeDetailsPageEpisodesTabList => GetView(ref _animeDetailsPageEpisodesTabList, Resource.Id.AnimeDetailsPageEpisodesTabList);

        public RelativeLayout AnimeDetailsPageEpisodesTabLoadingOverlay => GetView(ref _animeDetailsPageEpisodesTabLoadingOverlay, Resource.Id.AnimeDetailsPageEpisodesTabLoadingOverlay);

        private AnimeDetailsPageEpisodesTabFragment()
        {
            ViewModel = ViewModelLocator.AnimeDetails;
        }

        public override int LayoutResourceId => Resource.Layout.AnimeDetailsPageEpisodesTab;

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

            BindEpisodes();
        }

        private void BindEpisodes()
        {
            MainActivity.WriteCrashLog($"EpisodesTab bind: EP={ViewModel.Episodes.Count}", null);

            if (!ViewModel.Episodes.Any())
            {
                AnimeDetailsPageEpisodesTabList.Adapter = null;
                return;
            }

            AnimeDetailsPageEpisodesTabList.Adapter =
                ViewModel.Episodes.GetAdapter(EpisodeItemTemplate);
        }

        private View EpisodeItemTemplate(int i, AnimeEpisode ep, View arg3)
        {
            var view = arg3 ?? Activity.LayoutInflater.Inflate(Resource.Layout.DetailAnimeEpisodeView, null);

            view.FindViewById<TextView>(Resource.Id.EpisodeCount).Text = $"Ep. {ep.EpisodeId}";
            view.FindViewById<TextView>(Resource.Id.EpisodeName).Text = ep.Title;

            if (ep.EpisodeId <= ViewModel.MyEpisodes)
                view.FindViewById(Resource.Id.TickIcon).Visibility = ViewStates.Visible;
            else
                view.FindViewById(Resource.Id.TickIcon).Visibility = ViewStates.Gone;

            if (string.IsNullOrEmpty(ep.TitleJapanese) && string.IsNullOrEmpty(ep.TitleRomanji) &&
                string.IsNullOrEmpty(ep.ForumUrl) && string.IsNullOrEmpty(ep.VideoUrl))
            {
                view.FindViewById(Resource.Id.MoreButton).Visibility = ViewStates.Gone;
            }
            else
            {
                var moreBtn = view.FindViewById(Resource.Id.MoreButton);
                moreBtn.Visibility = ViewStates.Visible;
                moreBtn.SetOnClickListener(new OnClickListener(v =>
                {
                    _epPopupMenu = new PopupMenu(Activity, view.FindViewById(Resource.Id.MoreButton));

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

            // Row itself: forum discussion first, episode page as fallback
            view.SetOnClickListener(new OnClickListener(v =>
            {
                if (!string.IsNullOrEmpty(ep.ForumUrl))
                    ViewModel.NavigateEpDiscussionCommand.Execute(ep);
                else if (!string.IsNullOrEmpty(ep.VideoUrl))
                    ViewModelLocator.AnimeDetails.OpenWebPageInApp(ep.VideoUrl);
            }));

            if (ep.Filler || ep.Recap)
            {
                var note = view.FindViewById<TextView>(Resource.Id.EpisodeNote);
                note.Visibility = ViewStates.Visible;
                note.Text = $"{(ep.Filler ? "Filler " : "")} {(ep.Recap ? "Recap" : "")}".Trim();
            }
            else
            {
                view.FindViewById(Resource.Id.EpisodeNote).Visibility = ViewStates.Gone;
            }

            return view;
        }
    }
}

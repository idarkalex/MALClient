using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Android.App;
using Android.Graphics;
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
    internal class AnimeDetailsPageDetailsTabFragment : MalFragmentBase
    {    
        private readonly AnimeDetailsPageViewModel ViewModel;
        private PopupMenu _epPopupMenu;
        private PopupMenu _opEdPopup;

        private AnimeDetailsPageDetailsTabFragment()
        {
            ViewModel = ViewModelLocator.AnimeDetails;
        }

        public override int LayoutResourceId => Resource.Layout.AnimeDetailsPageDetailsTab;

        public static AnimeDetailsPageDetailsTabFragment Instance => new AnimeDetailsPageDetailsTabFragment();

        protected override void Init(Bundle savedInstanceState)
        {

        }

        protected override void InitBindings()
        {
            Bindings.Add(
                this.SetBinding(() => ViewModel.LoadingDetails,
                    () => AnimeDetailsPageDetailsTabLoadingOverlay.Visibility).ConvertSourceToTarget(Converters.BoolToVisibility));

            Bindings.Add(this.SetBinding(() => ViewModel.LoadingDetails).WhenSourceChanges(() =>
            {
                if (ViewModel.LoadingDetails)
                    return;

                try
                {
                    BindDetailsTab();
                }
                catch (Exception ex)
                {
                    MainActivity.WriteCrashLog("DetailsTab bind", ex);
                }
            }));
        }

        private void BindDetailsTab()
        {
            MainActivity.WriteCrashLog(
                $"DetailsTab bind: Info={ViewModel.Information.Count} Stats={ViewModel.Stats.Count} " +
                $"OP={ViewModel.OPs.Count} ED={ViewModel.EDs.Count} EP={ViewModel.Episodes.Count} " +
                $"Genres={ViewModel.LeftGenres.Count + ViewModel.RightGenres.Count}", null);

            // Pre-cache AnimeThemes data so OP/ED clicks are instant
            if (ViewModel.OPs.Count > 0 || ViewModel.EDs.Count > 0)
            {
                var animeTitle = ViewModelLocator.AnimeDetails.Title;
                if (!string.IsNullOrEmpty(animeTitle))
                    Task.Run(async () => await AnimeThemesHelper.SearchAsync(animeTitle));
            }

            AnimeDetailsPageDetailsTabLeftGenresList.SetAdapter(
                ViewModel.LeftGenres.GetAdapter(GetSingleDetailTemplateDelegate));
            AnimeDetailsPageDetailsTabRightGenresList.SetAdapter(
                ViewModel.RightGenres.GetAdapter(GetSingleDetailTemplateDelegate));
            AnimeDetailsPageDetailsTabInformationList.SetAdapter(
                ViewModel.Information.GetAdapter(GetDetailsTemplateDelegate));
            AnimeDetailsPageDetailsTabStatsList.SetAdapter(ViewModel.Stats.GetAdapter(GetDetailsTemplateDelegate));

            if (ViewModel.AnimeMode)
            {
                AnimeDetailsPageDetailsTabOPsList.Visibility =
                    AnimeDetailsPageDetailsTabEDsList.Visibility =
                        AnimeDetailsPageDetailsTabEDsListLabel.Visibility =
                            AnimeDetailsPageDetailsTabOPsListLabel.Visibility = ViewStates.Visible;

                AnimeDetailsPageDetailsTabOPsList.SetAdapter(
                    ViewModel.OPs.GetAdapter(GetOpEdDetailTemplateDelegate));
                AnimeDetailsPageDetailsTabEDsList.SetAdapter(
                    ViewModel.EDs.GetAdapter(GetOpEdDetailTemplateDelegate));

                if (ViewModel.Episodes.Any())
                {
                    EpisodesLabel.Visibility =
                        EpisodesList.Visibility = ViewStates.Visible;

                    EpisodesList.Adapter = ViewModel.Episodes.GetAdapter(EpisodeItemTemplate);
                    EpisodesList.LayoutParameters.Height =
                        (Math.Min(5, ViewModel.Episodes.Count) * DimensionsHelper.DpToPx(54));
                }
                else
                {
                    EpisodesLabel.Visibility =
                        EpisodesList.Visibility = ViewStates.Gone;
                }
            }
            else
            {
                AnimeDetailsPageDetailsTabOPsList.Visibility =
                    AnimeDetailsPageDetailsTabEDsList.Visibility =
                        AnimeDetailsPageDetailsTabEDsListLabel.Visibility =
                            AnimeDetailsPageDetailsTabOPsListLabel.Visibility = EpisodesLabel.Visibility =
                                EpisodesList.Visibility = ViewStates.Gone;
            }
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
                            ResourceLocator.SystemControlsLauncherService.LaunchUri(new Uri(ep.VideoUrl));
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

        private View GetSingleDetailTemplateDelegate(int i, string s, View arg3)
        {
            var view = Activity.LayoutInflater.Inflate(Resource.Layout.GenreItemView, null);
            view.FindViewById<TextView>(Resource.Id.GenreItemTextView).Text = s;

            return view;
        }

        private bool _currentOpEdIsOp;

        private void PlayOpEdTheme(string s)
        {
            var seq = AnimeThemesHelper.ParseSequence(s);
            var title = ViewModelLocator.AnimeDetails.Title;

            // Show overlay immediately with loading spinner
            var activity = Activity as Activities.MainActivity;
            activity?.ShowVideoLoading();

            Task.Run(async () =>
            {
                var videos = await AnimeThemesHelper.SearchAsync(title);
                var match = AnimeThemesHelper.FindMatch(videos, _currentOpEdIsOp, seq);
                if (Activity == null || Activity.IsFinishing) return;
                Activity.RunOnUiThread(() =>
                {
                    if (match != null && !string.IsNullOrEmpty(match.Url))
                        ViewModelLocator.AnimeDetails.PlayVideoInApp(match.Url);
                    else
                    {
                        activity?.HideVideoLoading();
                        var query = WebUtility.UrlEncode(s);
                        ResourceLocator.SystemControlsLauncherService.LaunchUri(
                            new Uri($"https://www.youtube.com/results?search_query={query}"));
                    }
                });
            });
        }

        private View GetOpEdDetailTemplateDelegate(int i, string s, View arg3)
        {
            var view = Activity.LayoutInflater.Inflate(Resource.Layout.OpEdItemView, null);
            view.FindViewById<TextView>(Resource.Id.GenreItemTextView).Text = s;

            _currentOpEdIsOp = ViewModel.OPs.Contains(s);
            view.SetOnClickListener(new OnClickListener(v => PlayOpEdTheme(s)));

            view.FindViewById(Resource.Id.MoreButton).SetOnClickListener(new OnClickListener(v =>
            {
                _currentOpEdIsOp = ViewModel.OPs.Contains(s);
                _opEdPopup = new PopupMenu(Activity, view.FindViewById(Resource.Id.MoreButton));


                _opEdPopup.Menu.Add(0, 0, 0, "Search YouTube");
                _opEdPopup.Menu.Add(0, 1, 0, "Copy to clipboard");


                _opEdPopup.SetOnMenuItemClickListener(new AnimeItemFlyoutBuilder.MenuListener(item =>
                {
                    if (item.ItemId == 0)
                    {
                        PlayOpEdTheme(s);
                    }
                    else if(item.ItemId == 1)
                    {
                        ResourceLocator.ClipboardProvider.SetText(s);
                    }
                }));
                _opEdPopup.Show();
            }));

            return view;
        }

        private View GetDetailsTemplateDelegate(int i, Tuple<string, string> tuple, View arg3)
        {
            var view = Activity.LayoutInflater.Inflate(Resource.Layout.DetailItemView, null);
            view.FindViewById<TextView>(Resource.Id.DetailItemCategoryTextView).Text = tuple.Item1;
            var contentTextView = view.FindViewById<TextView>(Resource.Id.DetailItemContentTextView);
            contentTextView.Text = tuple.Item2;
            if (tuple.Item1 == "Alt. Titles")
                contentTextView.SetMaxLines(int.MaxValue);

            return view;
        }

        #region Views

        private LinearLayout _animeDetailsPageDetailsTabLeftGenresList;
        private LinearLayout _animeDetailsPageDetailsTabRightGenresList;
        private LinearLayout _animeDetailsPageDetailsTabInformationList;
        private LinearLayout _animeDetailsPageDetailsTabStatsList;
        private TextView _animeDetailsPageDetailsTabOPsListLabel;
        private LinearLayout _animeDetailsPageDetailsTabOPsList;
        private TextView _animeDetailsPageDetailsTabEDsListLabel;
        private LinearLayout _animeDetailsPageDetailsTabEDsList;
        private TextView _episodesLabel;
        private ListView _episodesList;
        private RelativeLayout _animeDetailsPageDetailsTabLoadingOverlay;


        public LinearLayout AnimeDetailsPageDetailsTabLeftGenresList => GetView(ref _animeDetailsPageDetailsTabLeftGenresList, Resource.Id.AnimeDetailsPageDetailsTabLeftGenresList);
        public LinearLayout AnimeDetailsPageDetailsTabRightGenresList => GetView(ref _animeDetailsPageDetailsTabRightGenresList, Resource.Id.AnimeDetailsPageDetailsTabRightGenresList);
        public LinearLayout AnimeDetailsPageDetailsTabInformationList => GetView(ref _animeDetailsPageDetailsTabInformationList, Resource.Id.AnimeDetailsPageDetailsTabInformationList);
        public LinearLayout AnimeDetailsPageDetailsTabStatsList => GetView(ref _animeDetailsPageDetailsTabStatsList, Resource.Id.AnimeDetailsPageDetailsTabStatsList);
        public TextView AnimeDetailsPageDetailsTabOPsListLabel => GetView(ref _animeDetailsPageDetailsTabOPsListLabel, Resource.Id.AnimeDetailsPageDetailsTabOPsListLabel);
        public LinearLayout AnimeDetailsPageDetailsTabOPsList => GetView(ref _animeDetailsPageDetailsTabOPsList, Resource.Id.AnimeDetailsPageDetailsTabOPsList);
        public TextView AnimeDetailsPageDetailsTabEDsListLabel => GetView(ref _animeDetailsPageDetailsTabEDsListLabel, Resource.Id.AnimeDetailsPageDetailsTabEDsListLabel);
        public LinearLayout AnimeDetailsPageDetailsTabEDsList => GetView(ref _animeDetailsPageDetailsTabEDsList, Resource.Id.AnimeDetailsPageDetailsTabEDsList);
        public TextView EpisodesLabel => GetView(ref _episodesLabel, Resource.Id.EpisodesLabel);
        public ListView EpisodesList => GetView(ref _episodesList, Resource.Id.EpisodesList);
        public RelativeLayout AnimeDetailsPageDetailsTabLoadingOverlay => GetView(ref _animeDetailsPageDetailsTabLoadingOverlay, Resource.Id.AnimeDetailsPageDetailsTabLoadingOverlay);

        #endregion
    }
}




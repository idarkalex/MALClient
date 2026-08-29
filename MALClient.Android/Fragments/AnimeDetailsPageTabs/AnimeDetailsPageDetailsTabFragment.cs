using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
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
using MALClient.Android.Fragments;
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

                (RootView?.Parent as UserControls.HeightAdjustingViewPager)?.SetTabHeightForCurrentView(RootView);
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
                    Task.Run(async () =>
                    {
                        ResourceLocator.EnglishTitlesProvider.TryGetEnglishTitleForSeries(
                            ViewModelLocator.AnimeDetails.Id, ViewModelLocator.AnimeDetails.AnimeMode, out var english);
                        await AnimeThemesHelper.SearchAsync(animeTitle, english);
                    });
            }

            AnimeDetailsPageDetailsTabLeftGenresList.SetAdapter(
                ViewModel.LeftGenres.GetAdapter(GetSingleDetailTemplateDelegate));
            AnimeDetailsPageDetailsTabRightGenresList.SetAdapter(
                ViewModel.RightGenres.GetAdapter(GetSingleDetailTemplateDelegate));
            AnimeDetailsPageDetailsTabInformationList.SetAdapter(
                ViewModel.Information.GetAdapter(GetDetailsTemplateDelegate));

            // Hide Statistics section when empty (all data moved to General cards)
            var statsHeader = RootView.FindViewById<TextView>(Resource.Id.AnimeDetailsPageDetailsTabStatsLabel);
            if (ViewModel.Stats.Count == 0)
            {
                statsHeader.Visibility = ViewStates.Gone;
                AnimeDetailsPageDetailsTabStatsList.Visibility = ViewStates.Gone;
            }
            else
            {
                statsHeader.Visibility = ViewStates.Visible;
                AnimeDetailsPageDetailsTabStatsList.Visibility = ViewStates.Visible;
                AnimeDetailsPageDetailsTabStatsList.SetAdapter(ViewModel.Stats.GetAdapter(GetDetailsTemplateDelegate));
            }

            if (ViewModel.AnimeMode)
            {
                AnimeDetailsPageDetailsTabOPsList.Visibility =
                    AnimeDetailsPageDetailsTabEDsList.Visibility =
                        AnimeDetailsPageDetailsTabEDsListLabel.Visibility =
                            AnimeDetailsPageDetailsTabOPsListLabel.Visibility = ViewStates.Visible;

                AnimeDetailsPageDetailsTabOPsList.SetAdapter(
                    ViewModel.OPs.GetAdapter((i, s, v) => GetOpEdDetailTemplateDelegate(i, s, v, true)));
                AnimeDetailsPageDetailsTabEDsList.SetAdapter(
                    ViewModel.EDs.GetAdapter((i, s, v) => GetOpEdDetailTemplateDelegate(i, s, v, false)));
            }
            else
            {
                AnimeDetailsPageDetailsTabOPsList.Visibility =
                    AnimeDetailsPageDetailsTabEDsList.Visibility =
                        AnimeDetailsPageDetailsTabEDsListLabel.Visibility =
                            AnimeDetailsPageDetailsTabOPsListLabel.Visibility = ViewStates.Gone;
            }
        }


        private View GetSingleDetailTemplateDelegate(int i, string s, View arg3)
        {
            var view = Activity.LayoutInflater.Inflate(Resource.Layout.GenreItemView, null);
            view.FindViewById<TextView>(Resource.Id.GenreItemTextView).Text = s;

            return view;
        }

        private void PlayOpEdTheme(string s, bool isOp)
        {
            var seq = AnimeThemesHelper.ParseSequence(s);
            var title = ViewModelLocator.AnimeDetails.Title;
            var searchQuery = BuildOpEdSearchQuery(s);
            var songName = ExtractOpEdSong(s);
            global::Android.Util.Log.Info("MALPlus", $"PlayOpEdTheme: seq={seq} isOp={isOp} query={searchQuery} song={songName} title={title}");
            DiagnosticsReporter.Info("OP/ED", $"click: seq={seq} isOp={isOp} query=\"{searchQuery}\" title=\"{title}\"");

            // Show the video overlay immediately so the tap feels instant while resolving the source
            Activity?.RunOnUiThread(() =>
            {
                if (Activity == null || Activity.IsFinishing) return;
                (ParentFragment as AnimeDetailsPageFragment)?.ShowVideoLoading();
            });

            Task.Run(async () =>
            {
                try
                {
                    // PRIMARY: AnimeThemes (direct WebM, EM controls, exact match)
                    ResourceLocator.EnglishTitlesProvider.TryGetEnglishTitleForSeries(
                        ViewModelLocator.AnimeDetails.Id, ViewModelLocator.AnimeDetails.AnimeMode, out var englishTitle);
                    var videos = await AnimeThemesHelper.SearchAsync(title, englishTitle);
                    global::Android.Util.Log.Info("MALPlus", $"AnimeThemes search: {videos.Count} videos for '{title}'");
                    foreach (var v in videos)
                        global::Android.Util.Log.Info("MALPlus", $"  AT: {v.Type}{v.Sequence} -> {v.Url}");
                    var match = AnimeThemesHelper.FindMatch(videos, isOp, seq, searchQuery, songName);
                    DiagnosticsReporter.Info("OP/ED", $"AnimeThemes: {videos.Count} videos, match={(match?.Url ?? "null")}");

                    if (!string.IsNullOrEmpty(match?.Url))
                    {
                        if (Activity == null || Activity.IsFinishing) return;
                        Activity.RunOnUiThread(() =>
                            ViewModelLocator.AnimeDetails.PlayVideoInApp(match.Url));
                        return;
                    }

                    // SECONDARY: YouTube search scraping
                    global::Android.Util.Log.Info("MALPlus", "AnimeThemes empty, trying YouTube scraping...");
                    var videoId = await Web.InlineVideoWebViewClient.SearchYouTubeVideoId(
                        WebUtility.UrlEncode(searchQuery));
                    global::Android.Util.Log.Info("MALPlus", $"YouTube scrape: videoId={videoId ?? "null"}");
                    DiagnosticsReporter.Info("OP/ED", $"YouTube: videoId={videoId ?? "null"}");

                    if (!string.IsNullOrEmpty(videoId))
                    {
                        if (Activity == null || Activity.IsFinishing) return;
                        Activity.RunOnUiThread(() =>
                            ViewModelLocator.AnimeDetails.PlayVideoInApp(
                                $"https://www.youtube.com/watch?v={videoId}"));
                        return;
                    }

                    // FALLBACK: external YouTube
                    global::Android.Util.Log.Info("MALPlus", "Both failed, opening external");
                    DiagnosticsReporter.Warn("OP/ED", "no match, opening external");
                    if (Activity == null || Activity.IsFinishing) return;
                    Activity.RunOnUiThread(() =>
                    {
                        (ParentFragment as AnimeDetailsPageFragment)?.HideVideoLoading();
                        ResourceLocator.SystemControlsLauncherService.LaunchUri(
                            new Uri($"https://www.youtube.com/results?search_query={WebUtility.UrlEncode(searchQuery)}"));
                    });
                }
                catch (Exception ex)
                {
                    global::Android.Util.Log.Error("MALPlus", $"PlayOpEdTheme ERROR: {ex}");
                    DiagnosticsReporter.Error("OP/ED", "exception", ex);
                    MainActivity.WriteCrashLog("PlayOpEdTheme error", ex);
                }
            });
        }

        private static string BuildOpEdSearchQuery(string opEdText)
        {
            if (string.IsNullOrEmpty(opEdText)) return "";
            // Input: 1: "Song Name (Japanese Name)" by Artist Name (eps 1)
            // Extract: Song Name Artist Name
            var songMatch = Regex.Match(opEdText, @"""\s*([^""]+?)\s*(?:\([^)]*\))?\s*""");
            var song = songMatch.Success ? songMatch.Groups[1].Value.Trim() : "";
            var artistMatch = Regex.Match(opEdText, @"by\s+(.+?)(?:\s*\(eps|\s*$)");
            var artist = artistMatch.Success ? artistMatch.Groups[1].Value.Trim() : "";
            var query = $"{song} {artist}".Trim();
            // Strip Japanese characters and extra spaces
            query = Regex.Replace(query, @"[\u3000-\u9FFF\uFF00-\uFFEF]+", " ").Trim();
            return Regex.Replace(query, @"\s+", " ");
        }

        private static string ExtractOpEdSong(string opEdText)
        {
            if (string.IsNullOrEmpty(opEdText)) return "";
            var songMatch = Regex.Match(opEdText, @"""\s*([^""]+?)\s*(?:\([^)]*\))?\s*""");
            return songMatch.Success ? songMatch.Groups[1].Value.Trim() : "";
        }

        private View GetOpEdDetailTemplateDelegate(int i, string s, View arg3, bool isOp)
        {
            var view = Activity.LayoutInflater.Inflate(Resource.Layout.OpEdItemView, null);
            view.FindViewById<TextView>(Resource.Id.GenreItemTextView).Text = s;

            view.SetOnClickListener(new OnClickListener(v => PlayOpEdTheme(s, isOp)));

            view.FindViewById(Resource.Id.MoreButton).SetOnClickListener(new OnClickListener(v =>
            {
                _opEdPopup = new PopupMenu(Activity, view.FindViewById(Resource.Id.MoreButton));


                _opEdPopup.Menu.Add(0, 0, 0, "Search YouTube");
                _opEdPopup.Menu.Add(0, 1, 0, "Copy to clipboard");


                _opEdPopup.SetOnMenuItemClickListener(new AnimeItemFlyoutBuilder.MenuListener(item =>
                {
                    if (item.ItemId == 0)
                    {
                        PlayOpEdTheme(s, isOp);
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


        public LinearLayout AnimeDetailsPageDetailsTabLeftGenresList => GetView(ref _animeDetailsPageDetailsTabLeftGenresList, Resource.Id.AnimeDetailsPageDetailsTabLeftGenresList);
        public LinearLayout AnimeDetailsPageDetailsTabRightGenresList => GetView(ref _animeDetailsPageDetailsTabRightGenresList, Resource.Id.AnimeDetailsPageDetailsTabRightGenresList);
        public LinearLayout AnimeDetailsPageDetailsTabInformationList => GetView(ref _animeDetailsPageDetailsTabInformationList, Resource.Id.AnimeDetailsPageDetailsTabInformationList);
        public LinearLayout AnimeDetailsPageDetailsTabStatsList => GetView(ref _animeDetailsPageDetailsTabStatsList, Resource.Id.AnimeDetailsPageDetailsTabStatsList);
        public TextView AnimeDetailsPageDetailsTabOPsListLabel => GetView(ref _animeDetailsPageDetailsTabOPsListLabel, Resource.Id.AnimeDetailsPageDetailsTabOPsListLabel);
        public LinearLayout AnimeDetailsPageDetailsTabOPsList => GetView(ref _animeDetailsPageDetailsTabOPsList, Resource.Id.AnimeDetailsPageDetailsTabOPsList);
        public TextView AnimeDetailsPageDetailsTabEDsListLabel => GetView(ref _animeDetailsPageDetailsTabEDsListLabel, Resource.Id.AnimeDetailsPageDetailsTabEDsListLabel);
        public LinearLayout AnimeDetailsPageDetailsTabEDsList => GetView(ref _animeDetailsPageDetailsTabEDsList, Resource.Id.AnimeDetailsPageDetailsTabEDsList);

        #endregion
    }
}






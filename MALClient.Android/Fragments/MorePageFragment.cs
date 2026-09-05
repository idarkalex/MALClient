using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android.Animation;
using Android.Graphics;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using FFImageLoading;
using FFImageLoading.Transformations;
using FFImageLoading.Views;
using MALClient.Android.Activities;
using MALClient.Android.Listeners;
using MALClient.Android.Resources;
using MALClient.Models.Enums;
using MALClient.XShared.Comm.Anime;
using MALClient.XShared.Comm.Profile;
using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALClient.Android.Fragments
{
    public class MorePageFragment : MalFragmentBase
    {
        public override int LayoutResourceId => Resource.Layout.MorePage;

        protected override void Init(Bundle savedInstanceState) { }

        protected override void InitBindings()
        {
            MorePageProfileHeader.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageProfile,
                    new ProfilePageNavigationArgs { TargetUser = Credentials.UserName })));

            MorePageAnimeListItem.SetOnClickListener(new OnClickListener(v =>
                TogglePanel(MorePageAnimeListPanel, MorePageAnimeListMoreButton, v2 => _animeListPanelExpanded = v2, _animeListPanelExpanded)));

            MorePageSeasonalItem.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageSeasonal, AnimeListPageNavigationArgs.Seasonal)));

            MorePageTopAnimeItem.SetOnClickListener(new OnClickListener(v =>
                TogglePanel(MorePageTopAnimeTypesPanel, MorePageTopAnimeMoreButton, v2 => _topAnimePanelExpanded = v2, _topAnimePanelExpanded)));

            MorePageSearchItem.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageSearch, new SearchPageNavigationArgs())));

            MorePageRecommendationsItem.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageRecomendations, null)));

            MorePageCalendarItem.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageCalendar, null)));

            MorePageMangaListItem.SetOnClickListener(new OnClickListener(v =>
                TogglePanel(MorePageMangaListPanel, MorePageMangaListMoreButton, v2 => _mangaListPanelExpanded = v2, _mangaListPanelExpanded)));

            MorePageTopMangaItem.SetOnClickListener(new OnClickListener(v =>
                TogglePanel(MorePageTopMangaTypesPanel, MorePageTopMangaMoreButton, v2 => _topMangaPanelExpanded = v2, _topMangaPanelExpanded)));

            MorePageAdaptedItem.SetOnClickListener(new OnClickListener(v =>
                TogglePanel(MorePageAdaptedTypesPanel, MorePageAdaptedMoreButton, v2 => _adaptedPanelExpanded = v2, _adaptedPanelExpanded)));

            MorePageAnimeListMoreButton.SetOnClickListener(new OnClickListener(v =>
                TogglePanel(MorePageAnimeListPanel, MorePageAnimeListMoreButton, v2 => _animeListPanelExpanded = v2, _animeListPanelExpanded)));

            MorePageMangaListMoreButton.SetOnClickListener(new OnClickListener(v =>
                TogglePanel(MorePageMangaListPanel, MorePageMangaListMoreButton, v2 => _mangaListPanelExpanded = v2, _mangaListPanelExpanded)));

            MorePageTopAnimeMoreButton.SetOnClickListener(new OnClickListener(v =>
                TogglePanel(MorePageTopAnimeTypesPanel, MorePageTopAnimeMoreButton, v2 => _topAnimePanelExpanded = v2, _topAnimePanelExpanded)));

            MorePageTopMangaMoreButton.SetOnClickListener(new OnClickListener(v =>
                TogglePanel(MorePageTopMangaTypesPanel, MorePageTopMangaMoreButton, v2 => _topMangaPanelExpanded = v2, _topMangaPanelExpanded)));

            MorePageAdaptedMoreButton.SetOnClickListener(new OnClickListener(v =>
                TogglePanel(MorePageAdaptedTypesPanel, MorePageAdaptedMoreButton, v2 => _adaptedPanelExpanded = v2, _adaptedPanelExpanded)));

            PopulateTypePanel(MorePageTopAnimeTypesPanel,
                Enum.GetValues(typeof(TopAnimeType)).Cast<TopAnimeType>().Select(t => (t.ToString(), (Action)(() => NavigateTo(PageIndex.PageTopAnime, AnimeListPageNavigationArgs.TopAnime(t))))).ToList());
            PopulateTypePanel(MorePageTopMangaTypesPanel,
                Enum.GetValues(typeof(MangaTopType)).Cast<MangaTopType>().Select(t => (t.ToString(), (Action)(() => NavigateTo(PageIndex.PageTopManga, AnimeListPageNavigationArgs.TopMangaCategory(t))))).ToList());
            PopulateTypePanel(MorePageAdaptedTypesPanel,
                Enum.GetValues(typeof(MangaAdaptedType)).Cast<MangaAdaptedType>().Select(t => (AnimeAdaptedToAnimeQuery.ToDisplayName(t), (Action)(() => NavigateTo(PageIndex.PageMangaAdapted, AnimeListPageNavigationArgs.MangaAdapted(t))))).ToList());
            PopulateStatusPanel(MorePageAnimeListPanel, false);
            PopulateStatusPanel(MorePageMangaListPanel, true);

            MorePageArticlesItem.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageArticles, new MalArticlesPageNavigationArgs { WorkMode = ArticlePageWorkMode.Articles, Source = PageIndex.PageMore })));

            MorePageVideosItem.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PagePopularVideos, null)));

            MorePageForumsItem.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageForumIndex, null)));

            MorePageSettingsItem.SetOnClickListener(new OnClickListener(v =>
                NavigateTo(PageIndex.PageSettings, null)));

            if (Credentials.Authenticated)
            {
                MorePageProfileUsername.Text = Credentials.UserName;
                MorePageProfileCompleted.Visibility = ViewStates.Gone;
                LoadProfileDataAsync();
            }
            else
            {
                MorePageProfileUsername.Text = "Log in";
                MorePageProfileCompleted.Visibility = ViewStates.Gone;
            }
        }

        private async void LoadProfileDataAsync()
        {
            try
            {
                var data = await DataCache.RetrieveProfileData(Credentials.UserName);
                if (data?.User?.ImgUrl != null && IsAdded)
                {
                    MorePageProfileImage.Into(data.User.ImgUrl);
                    if (data.AnimeCompleted > 0 || data.MangaCompleted > 0)
                    {
                        MorePageProfileCompleted.Text = $"{data.AnimeCompleted} Completed";
                        MorePageProfileCompleted.Visibility = ViewStates.Visible;
                    }
                }
                else
                {
                    LoadAvatarFromCdn();
                    RefreshProfileCacheInBackground();
                }
            }
            catch (Exception)
            {
                // Profile not cached yet, that's ok
                LoadAvatarFromCdn();
            }
        }

        private void LoadAvatarFromCdn()
        {
            if (!IsAdded || Credentials.Id == 0)
                return;
            var cacheDuration = TimeSpan.FromMinutes(10);
            ImageService.Instance
                .LoadUrl($"https://cdn.myanimelist.net/images/userimages/{Credentials.Id}.webp", cacheDuration)
                .FadeAnimation(false).Transform(new CircleTransformation())
                .Error(e =>
                {
                    if (!IsAdded) return;
                    ImageService.Instance
                        .LoadUrl($"https://cdn.myanimelist.net/images/userimages/{Credentials.Id}.jpg", cacheDuration)
                        .FadeAnimation(false).Transform(new CircleTransformation())
                        .Into(MorePageProfileImage);
                })
                .Into(MorePageProfileImage);
        }

        private async void RefreshProfileCacheInBackground()
        {
            try
            {
                var fresh = await new ProfileQuery(Credentials.UserName).GetProfileData(false);
                if (fresh?.User?.ImgUrl != null && IsAdded)
                    MorePageProfileImage.Into(fresh.User.ImgUrl);
            }
            catch (Exception)
            {
            }
        }

        private void NavigateTo(PageIndex page, object args)
        {
            if (args is AnimeListPageNavigationArgs animeArgs)
            {
                animeArgs.FromMore = true;
                animeArgs.ResetBackNav = false;
            }
            ViewModelLocator.GeneralMain.Navigate(page, args);
            ViewModelLocator.NavMgr.DeregisterBackNav();
            ViewModelLocator.NavMgr.RegisterBackNav(PageIndex.PageMore, null);
        }

        private void TogglePanel(LinearLayout panel, ImageView arrow, Action<bool> setExpanded, bool currentExpanded)
        {
            if (currentExpanded)
            {
                AnimateCollapse(panel, arrow);
                setExpanded(false);
            }
            else
            {
                AnimateExpand(panel, arrow);
                setExpanded(true);
            }
        }

        public override void OnResume()
        {
            base.OnResume();
            // Restore from FragmentUiState if instance fields are default (fresh fragment instance after nav)
            try
            {
                var ui = XShared.ViewModels.Main.FragmentUiState.More;
                if (ui != null)
                {
                    if (!_animeListPanelExpanded && ui.TryGetValue("AnimeList", out var a) && a is bool ab && ab) _animeListPanelExpanded = true;
                    if (!_mangaListPanelExpanded && ui.TryGetValue("MangaList", out var ml) && ml is bool mlb && mlb) _mangaListPanelExpanded = true;
                    if (!_topAnimePanelExpanded && ui.TryGetValue("TopAnime", out var ta) && ta is bool tab && tab) _topAnimePanelExpanded = true;
                    if (!_topMangaPanelExpanded && ui.TryGetValue("TopManga", out var tm) && tm is bool tmb && tmb) _topMangaPanelExpanded = true;
                    if (!_adaptedPanelExpanded && ui.TryGetValue("Adapted", out var ad) && ad is bool adb && adb) _adaptedPanelExpanded = true;
                }
            } catch { }
            RootView.PostDelayed(() =>
            {
                if (_animeListPanelExpanded && MorePageAnimeListPanel != null)
                {
                    MorePageAnimeListPanel.Visibility = ViewStates.Visible;
                    MorePageAnimeListMoreButton.Rotation = 180f;
                }
                if (_mangaListPanelExpanded && MorePageMangaListPanel != null)
                {
                    MorePageMangaListPanel.Visibility = ViewStates.Visible;
                    MorePageMangaListMoreButton.Rotation = 180f;
                }
                if (_topAnimePanelExpanded && MorePageTopAnimeTypesPanel != null)
                {
                    MorePageTopAnimeTypesPanel.Visibility = ViewStates.Visible;
                    MorePageTopAnimeMoreButton.Rotation = 180f;
                }
                if (_topMangaPanelExpanded && MorePageTopMangaTypesPanel != null)
                {
                    MorePageTopMangaTypesPanel.Visibility = ViewStates.Visible;
                    MorePageTopMangaMoreButton.Rotation = 180f;
                }
                if (_adaptedPanelExpanded && MorePageAdaptedTypesPanel != null)
                {
                    MorePageAdaptedTypesPanel.Visibility = ViewStates.Visible;
                    MorePageAdaptedMoreButton.Rotation = 180f;
                }
            }, 50);
        }

        public override void OnPause()
        {
            base.OnPause();
            try
            {
                var ui = XShared.ViewModels.Main.FragmentUiState.More;
                if (ui == null) return;
                ui["AnimeList"] = _animeListPanelExpanded;
                ui["MangaList"] = _mangaListPanelExpanded;
                ui["TopAnime"] = _topAnimePanelExpanded;
                ui["TopManga"] = _topMangaPanelExpanded;
                ui["Adapted"] = _adaptedPanelExpanded;
            } catch { }
        }

        private static void AnimateExpand(LinearLayout panel, ImageView arrow)
        {
            panel.Visibility = ViewStates.Visible;
            var parent = panel.Parent as View;
            panel.Measure(
                View.MeasureSpec.MakeMeasureSpec(parent != null ? parent.Width : 0, MeasureSpecMode.Exactly),
                View.MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified));
            var target = panel.MeasuredHeight;
            var animator = ValueAnimator.OfInt(0, target);
            animator.SetDuration(180);
            animator.Update += (s, e) =>
            {
                var lp = panel.LayoutParameters;
                lp.Height = (int)(target * animator.AnimatedFraction);
                panel.LayoutParameters = lp;
            };
            animator.AnimationEnd += (s, e) =>
            {
                var lp = panel.LayoutParameters;
                lp.Height = ViewGroup.LayoutParams.WrapContent;
                panel.LayoutParameters = lp;
            };
            animator.Start();
            arrow.Rotation = 180f;
        }

        private static void AnimateCollapse(LinearLayout panel, ImageView arrow)
        {
            var startH = panel.Height;
            var animator = ValueAnimator.OfInt(startH, 0);
            animator.SetDuration(160);
            animator.Update += (s, e) =>
            {
                var lp = panel.LayoutParameters;
                lp.Height = (int)(startH * (1f - animator.AnimatedFraction));
                panel.LayoutParameters = lp;
            };
            animator.AnimationEnd += (s, e) => panel.Visibility = ViewStates.Gone;
            animator.Start();
            arrow.Rotation = 0f;
        }

        private void PopulateTypePanel(LinearLayout panel, List<(string Label, Action OnClick)> items)
        {
            panel.RemoveAllViews();
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var row = new LinearLayout(Context)
                {
                    LayoutParameters = new ViewGroup.LayoutParams(
                        ViewGroup.LayoutParams.MatchParent, DimensionsHelper.DpToPx(40))
                };
                row.SetBackgroundResource(ResourceExtension.SelectableItemBackground);
                row.SetGravity(GravityFlags.CenterVertical);
                row.Orientation = Orientation.Horizontal;
                row.SetPadding(DimensionsHelper.DpToPx(16), 0, DimensionsHelper.DpToPx(16), 0);

                var txt = new TextView(Context)
                {
                    LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
                };
                txt.SetPadding(DimensionsHelper.DpToPx(12), 0, 0, 0);
                txt.Typeface = Typeface.Create("Inter", TypefaceStyle.Normal);
                txt.SetTextColor(new Color(ResourceExtension.BrushText));
                txt.SetTextSize(ComplexUnitType.Sp, 14f);
                txt.Text = item.Label;

                row.AddView(txt);
                row.Click += (s, e) => item.OnClick();
                panel.AddView(row);

                if (i < items.Count - 1)
                {
                    panel.AddView(new View(Context)
                    {
                        LayoutParameters = new ViewGroup.LayoutParams(
                            ViewGroup.LayoutParams.MatchParent, DimensionsHelper.DpToPx(1))
                    });
                }
            }
        }

        private void PopulateStatusPanel(LinearLayout panel, bool manga)
        {
            panel.RemoveAllViews();
            var items = new List<(string Label, Action OnClick)>();
            var statusValues = new[] { AnimeStatus.Watching, AnimeStatus.Completed, AnimeStatus.OnHold, AnimeStatus.Dropped, AnimeStatus.PlanToWatch };
            for (int i = 0; i < statusValues.Length; i++)
            {
                var status = statusValues[i];
                var index = i;
                var workMode = manga ? AnimeListWorkModes.Manga : AnimeListWorkModes.Anime;
                items.Add((
                    XShared.Utils.Utilities.StatusToString((int)status, manga),
                    (Action)(() => NavigateTo(PageIndex.PageAnimeList, new AnimeListPageNavigationArgs(index, workMode)))));
            }
            PopulateTypePanel(panel, items);
        }

        private bool _animeListPanelExpanded;
        private bool _mangaListPanelExpanded;
        private bool _topAnimePanelExpanded;
        private bool _topMangaPanelExpanded;
        private bool _adaptedPanelExpanded;

        #region Views

        private LinearLayout _morePageProfileHeader;
        private ImageViewAsync _morePageProfileImage;
        private TextView _morePageProfileUsername;
        private TextView _morePageProfileCompleted;
        private LinearLayout _morePageAnimeListItem;
        private LinearLayout _morePageSeasonalItem;
        private LinearLayout _morePageTopAnimeItem;
        private LinearLayout _morePageSearchItem;
        private LinearLayout _morePageRecommendationsItem;
        private LinearLayout _morePageCalendarItem;
        private LinearLayout _morePageMangaListItem;
        private LinearLayout _morePageTopMangaItem;
        private LinearLayout _morePageAdaptedItem;
        private LinearLayout _morePageArticlesItem;
        private LinearLayout _morePageVideosItem;
        private LinearLayout _morePageForumsItem;
        private LinearLayout _morePageSettingsItem;
        private ImageView _morePageTopAnimeMoreButton;
        private ImageView _morePageTopMangaMoreButton;
        private ImageView _morePageAdaptedMoreButton;
        private ImageView _morePageAnimeListMoreButton;
        private ImageView _morePageMangaListMoreButton;
        private LinearLayout _morePageAnimeListPanel;
        private LinearLayout _morePageMangaListPanel;
        private LinearLayout _morePageTopAnimeTypesPanel;
        private LinearLayout _morePageTopMangaTypesPanel;
        private LinearLayout _morePageAdaptedTypesPanel;

        public LinearLayout MorePageProfileHeader => GetView(ref _morePageProfileHeader, Resource.Id.MorePageProfileHeader);
        public ImageViewAsync MorePageProfileImage => GetView(ref _morePageProfileImage, Resource.Id.MorePageProfileImage);
        public TextView MorePageProfileUsername => GetView(ref _morePageProfileUsername, Resource.Id.MorePageProfileUsername);
        public TextView MorePageProfileCompleted => GetView(ref _morePageProfileCompleted, Resource.Id.MorePageProfileCompleted);
        public LinearLayout MorePageAnimeListItem => GetView(ref _morePageAnimeListItem, Resource.Id.MorePageAnimeListItem);
        public LinearLayout MorePageSeasonalItem => GetView(ref _morePageSeasonalItem, Resource.Id.MorePageSeasonalItem);
        public LinearLayout MorePageTopAnimeItem => GetView(ref _morePageTopAnimeItem, Resource.Id.MorePageTopAnimeItem);
        public LinearLayout MorePageSearchItem => GetView(ref _morePageSearchItem, Resource.Id.MorePageSearchItem);
        public LinearLayout MorePageRecommendationsItem => GetView(ref _morePageRecommendationsItem, Resource.Id.MorePageRecommendationsItem);
        public LinearLayout MorePageCalendarItem => GetView(ref _morePageCalendarItem, Resource.Id.MorePageCalendarItem);
        public LinearLayout MorePageMangaListItem => GetView(ref _morePageMangaListItem, Resource.Id.MorePageMangaListItem);
        public LinearLayout MorePageTopMangaItem => GetView(ref _morePageTopMangaItem, Resource.Id.MorePageTopMangaItem);
        public LinearLayout MorePageAdaptedItem => GetView(ref _morePageAdaptedItem, Resource.Id.MorePageAdaptedItem);
        public LinearLayout MorePageArticlesItem => GetView(ref _morePageArticlesItem, Resource.Id.MorePageArticlesItem);
        public LinearLayout MorePageVideosItem => GetView(ref _morePageVideosItem, Resource.Id.MorePageVideosItem);
        public LinearLayout MorePageForumsItem => GetView(ref _morePageForumsItem, Resource.Id.MorePageForumsItem);
        public LinearLayout MorePageSettingsItem => GetView(ref _morePageSettingsItem, Resource.Id.MorePageSettingsItem);
        public ImageView MorePageTopAnimeMoreButton => GetView(ref _morePageTopAnimeMoreButton, Resource.Id.MorePageTopAnimeMoreButton);
        public ImageView MorePageTopMangaMoreButton => GetView(ref _morePageTopMangaMoreButton, Resource.Id.MorePageTopMangaMoreButton);
        public ImageView MorePageAdaptedMoreButton => GetView(ref _morePageAdaptedMoreButton, Resource.Id.MorePageAdaptedMoreButton);
        public ImageView MorePageAnimeListMoreButton => GetView(ref _morePageAnimeListMoreButton, Resource.Id.MorePageAnimeListMoreButton);
        public ImageView MorePageMangaListMoreButton => GetView(ref _morePageMangaListMoreButton, Resource.Id.MorePageMangaListMoreButton);
        public LinearLayout MorePageAnimeListPanel => GetView(ref _morePageAnimeListPanel, Resource.Id.MorePageAnimeListPanel);
        public LinearLayout MorePageMangaListPanel => GetView(ref _morePageMangaListPanel, Resource.Id.MorePageMangaListPanel);
        public LinearLayout MorePageTopAnimeTypesPanel => GetView(ref _morePageTopAnimeTypesPanel, Resource.Id.MorePageTopAnimeTypesPanel);
        public LinearLayout MorePageTopMangaTypesPanel => GetView(ref _morePageTopMangaTypesPanel, Resource.Id.MorePageTopMangaTypesPanel);
        public LinearLayout MorePageAdaptedTypesPanel => GetView(ref _morePageAdaptedTypesPanel, Resource.Id.MorePageAdaptedTypesPanel);

        #endregion
    }
}

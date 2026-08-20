using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

using Com.Shehabic.Droppy;
using FFImageLoading;
using FFImageLoading.Transformations;
using GalaSoft.MvvmLight.Helpers;
using MALClient.Android.Activities;
using MALClient.Android.BindingConverters;
using MALClient.Android.Dialogs;
using MALClient.Android.DIalogs;
using MALClient.Android.Flyouts;
using MALClient.Android.Listeners;
using MALClient.Android.PagerAdapters;
using MALClient.Android.Resources;
using MALClient.Android.UserControls;
using MALClient.Models.Enums;
using MALClient.XShared.NavArgs;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Details;

namespace MALClient.Android.Fragments
{
    public partial class AnimeDetailsPageFragment : MalFragmentBase
    {
        private AnimeDetailsPageNavigationArgs _navArgs;
        private AnimeDetailsPageViewModel ViewModel;
        private DroppyMenuPopup _menu;

        public AnimeDetailsPageFragment(AnimeDetailsPageNavigationArgs navArgs)
        {
            _navArgs = navArgs;
        }

        protected override void Init(Bundle savedInstanceState)
        {
            ViewModel = ViewModelLocator.AnimeDetails;
            ViewModel.Init(_navArgs, false);
        }

        protected override void InitBindings()
        {
            AnimeDetailsPagePivot.Adapter = new AnimeDetailsPagerAdapter(ChildFragmentManager);
            AnimeDetailsPageTabStrip.SetViewPager(AnimeDetailsPagePivot);
            AnimeDetailsPageTabStrip.CenterTabs();
            AnimeDetailsPagePivot.SetCurrentItem(_navArgs.SourceTabIndex, false);
            AnimeDetailsPagePivot.OffscreenPageLimit = 7;

            AnimeDetailsPageTabStrip.OnPageChangeListener =
                new OnPageChangedListener(i => ViewModel.DetailsPivotSelectedIndex = i);

            Bindings.Add(
                this.SetBinding(() => ViewModel.MyScoreBind,
                    () => AnimeDetailsPageScoreButton.Text));
            Bindings.Add(
                this.SetBinding(() => ViewModel.MyStatusBind,
                    () => AnimeDetailsPageStatusButton.Text));
            Bindings.Add(
                this.SetBinding(() => ViewModel.MyEpisodesBind,
                    () => AnimeDetailsPageWatchedButton.Text));
            Bindings.Add(
                this.SetBinding(() => ViewModel.MyVolumesBind,
                    () => AnimeDetailsPageReadVolumesButton.Text));
            Bindings.Add(
                this.SetBinding(() => ViewModel.LoadingGlobal,
                        () => AnimeDetailsPageLoadingOverlay.Visibility)
                    .ConvertSourceToTarget(Converters.BoolToVisibility));

            Bindings.Add(this.SetBinding(() => ViewModel.IsIncrementButtonEnabled).WhenSourceChanges(() =>
            {
                AnimeDetailsPageIncrementButton.Alpha = ViewModel.IsIncrementButtonEnabled ? 1 : .35f;
            }));
            Bindings.Add(this.SetBinding(() => ViewModel.IsDecrementButtonEnabled).WhenSourceChanges(() =>
            {
                AnimeDetailsPageDecrementButton.Alpha = ViewModel.IsDecrementButtonEnabled ? 1 : .35f;
            }));

            Bindings.Add(this.SetBinding(() => ViewModel.AnimeMode).WhenSourceChanges(() =>
            {
                AnimeDetailsPagePivot.Adapter.NotifyDataSetChanged();
            }));

            Bindings.Add(this.SetBinding(() => ViewModel.AddAnimeVisibility)
                .WhenSourceChanges(() =>
                {
                    if (ViewModel.AddAnimeVisibility)
                    {
                        AnimeDetailsPageIncDecSection.Visibility = ViewStates.Gone;
                        AnimeDetailsPageUpdateSection.Visibility = ViewStates.Gone;
                        AnimeDetailsPageAddSection.Visibility = ViewStates.Visible;
                        AnimeDetailsPageFavouriteButton.Visibility = ViewStates.Gone;
                    }
                    else
                    {
                        AnimeDetailsPageIncDecSection.Visibility = ViewStates.Visible;
                        AnimeDetailsPageUpdateSection.Visibility = ViewStates.Visible;
                        AnimeDetailsPageAddSection.Visibility = ViewStates.Gone;
                        AnimeDetailsPageFavouriteButton.Visibility = ViewStates.Visible;
                    }
                }));

            Bindings.Add(
                this.SetBinding(() => ViewModel.DetailsPivotSelectedIndex)
                    .WhenSourceChanges(
                        () =>
                        {
                            AnimeDetailsPagePivot.SetCurrentItem(ViewModel.DetailsPivotSelectedIndex, true);
                        }));

            Bindings.Add(
                this.SetBinding(() => ViewModel.IsFavourite)
                    .WhenSourceChanges(() =>
                    {
                        if (ViewModel.IsFavourite)
                        {
                            AnimeDetailsPageFavouriteButton.ImageTintList = ColorStateList.ValueOf(Color.White);
                            AnimeDetailsPageFavouriteButton.SetImageResource(Resource.Drawable.icon_favourite);
                            AnimeDetailsPageFavouriteButton.SetBackgroundResource(ResourceExtension.AccentColourRes);
                        }
                        else
                        {
                            AnimeDetailsPageFavouriteButton.ImageTintList = ColorStateList.ValueOf(new Color(ResourceExtension.BrushText));
                            AnimeDetailsPageFavouriteButton.SetImageResource(Resource.Drawable.icon_unfavourite);
                            AnimeDetailsPageFavouriteButton.SetBackgroundColor(Color.Transparent);
                        }
                    }));

            Bindings.Add(this.SetBinding(() => ViewModel.AnimeMode)
                .WhenSourceChanges(() =>
                {
                    if (ViewModel.AnimeMode)
                    {
                        AnimeDetailsPageReadVolumesButton.Visibility =
                            AnimeDetailsPageReadVolumesLabel.Visibility = ViewStates.Gone;
                    }
                    else
                    {
                        AnimeDetailsPageReadVolumesButton.Visibility =
                            AnimeDetailsPageReadVolumesLabel.Visibility = ViewStates.Visible;
                    }
                }));

            Bindings.Add(this.SetBinding(() => ViewModel.DetailImage)
                .WhenSourceChanges(() =>
                {
                    AnimeDetailsPageBlurredBackground.Into(ViewModel.DetailImage, new BlurredTransformation(25));
                    AnimeDetailsPageShowCoverImage.Into(ViewModel.DetailImage);
                }));

            Bindings.Add(this.SetBinding(() => ViewModel.Title)
                .WhenSourceChanges(() =>
                {
                    AnimeDetailsPageTitle.Text = ViewModel.Title;
                }));

            Bindings.Add(this.SetBinding(() => ViewModel.AnimeMode)
                .WhenSourceChanges(() =>
                {
                    // Initial fallback — will be replaced when Type loads from API
                    if (string.IsNullOrEmpty(ViewModel.Type))
                        AnimeDetailsPageTypeBadge.Text = ViewModel.AnimeMode ? "Anime" : "Manga";
                }));

            Bindings.Add(this.SetBinding(() => ViewModel.Type)
                .WhenSourceChanges(() =>
                {
                    if (!string.IsNullOrEmpty(ViewModel.Type))
                        AnimeDetailsPageTypeBadge.Text = ViewModel.Type;
                }));

            Bindings.Add(this.SetBinding(() => ViewModel.StartYear)
                .WhenSourceChanges(() =>
                {
                    AnimeDetailsPageYearLabel.Text = ViewModel.StartYear ?? "";
                }));

            Bindings.Add(this.SetBinding(() => ViewModel.AllEpisodes)
                .WhenSourceChanges(() =>
                {
                    var eps = ViewModel.AnimeItemReference?.AllEpisodes ?? ViewModel.AllEpisodes;
                    var unit = ViewModel.AnimeMode ? "Episodes" : "Chapters";
                    AnimeDetailsPageSubtitle.Text = $"{(eps == 0 ? "?" : eps.ToString())} {unit}";
                }));

            Bindings.Add(this.SetBinding(() => ViewModel.LoadingGlobal)
                .WhenSourceChanges(() =>
                {
                    if (!ViewModel.LoadingGlobal && ViewModel.AnimeItemReference != null)
                    {
                        var score = ViewModel.AnimeItemReference.GlobalScore;
                        AnimeDetailsPageScoreValue.Text = score == 0 ? "N/A" : score.ToString("N2");
                    }
                }));

            Bindings.Add(
                this.SetBinding(() => ViewModel.LoadingUpdate,
                        () => AnimeDetailsPageLoadingUpdateSpinner.Visibility)
                    .ConvertSourceToTarget(Converters.BoolToVisibility));

            Bindings.Add(
                this.SetBinding(() => ViewModel.IsAddAnimeButtonEnabled,
                    () => AnimeDetailsPageAddButton.Enabled));

            AnimeDetailsPageFavouriteButton.SetOnClickListener(
                new OnClickListener(view => ViewModel.ToggleFavouriteCommand.Execute(null)));
            AnimeDetailsPageIncrementButton.SetOnClickListener(
                new OnClickListener(view => ViewModel.IncrementEpsCommand.Execute(null)));
            AnimeDetailsPageDecrementButton.SetOnClickListener(
                new OnClickListener(view => ViewModel.DecrementEpsCommand.Execute(null)));
            AnimeDetailsPageAddButton.SetOnClickListener(
                new OnClickListener(view => ViewModel.AddAnimeCommand.Execute(null)));
            AnimeDetailsPageMoreButton.SetOnClickListener(new OnClickListener(view =>
            {
                _menu = AnimeDetailsPageMoreFlyoutBuilder.BuildForAnimeDetailsPage(Activity, ViewModel,
                    AnimeDetailsPageMoreButton,
                    OnMoreFlyoutClick);
                _menu.Show();
            }));
            AnimeDetailsPageQuickAddToListButton.SetOnClickListener(
                new OnClickListener(view => ViewModel.AddAnimeCommand.Execute(null)));
            AnimeDetailsPageQuickFavoriteButton.SetOnClickListener(
                new OnClickListener(view => ViewModel.ToggleFavouriteCommand.Execute(null)));

            //OneTime

            AnimeDetailsPageWatchedLabel.Text = ViewModel.WatchedEpsLabel;

            AnimeDetailsPageTitle.Text = ViewModel.Title;
            AnimeDetailsPageTypeBadge.Text = ViewModel.Type ?? (ViewModel.AnimeMode ? "Anime" : "Manga");
            var eps = ViewModel.AnimeItemReference?.AllEpisodes ?? ViewModel.AllEpisodes;
            var unit = ViewModel.AnimeMode ? "Episodes" : "Chapters";
            AnimeDetailsPageSubtitle.Text = $"{(eps == 0 ? "?" : eps.ToString())} {unit}";
            if (ViewModel.AnimeItemReference != null)
            {
                var score = ViewModel.AnimeItemReference.GlobalScore;
                AnimeDetailsPageScoreValue.Text = score == 0 ? "N/A" : score.ToString("N2");
            }

            if (Settings.HideDecrementButtons)
            {
                AnimeDetailsPageDecrementButton.Visibility = ViewStates.Gone;
                AnimeDetailsPageIncrementButton.LayoutParameters.Width =
                    DimensionsHelper.DpToPx(45);
                AnimeDetailsPageIncrementButton.LayoutParameters.Height =
                    DimensionsHelper.DpToPx(45);
            }

            //Events
            AnimeDetailsPageStatusButton.SetOnClickListener(
                new OnClickListener(view => AnimeDetailsPageStatusButtonOnClick()));
            AnimeDetailsPageScoreButton.SetOnClickListener(
                new OnClickListener(view => AnimeDetailsPageScoreButtonOnClick()));
            AnimeDetailsPageWatchedButton.SetOnClickListener(
                new OnClickListener(view => AnimeDetailsPageWatchedButtonOnClick()));
            AnimeDetailsPageReadVolumesButton.SetOnClickListener(
                new OnClickListener(view => AnimeDetailsPageVolumesButtonOnClick()));

            // Pull-to-refresh
            AnimeDetailsPageSwipeRefresh.ScrollingView = AnimeDetailsPageScrollView;
            AnimeDetailsPageSwipeRefresh.Refresh += (s, e) =>
            {
                ViewModel.RefreshData();
                AnimeDetailsPageSwipeRefresh.Refreshing = false;
            };
        }

        private async void OnMoreFlyoutClick(int i)
        {
            switch (i)
            {
                case 0:
                    ViewModel.NavigateForumBoardCommand.Execute(null);
                    break;

                case 1:
                    AnimeDetailsPageDialogBuilder.BuildPromotionalVideoDialog(ViewModel);
                    break;

                case 2:
                    AnimeUpdateDialogBuilder.BuildTagDialog(ViewModel);
                    break;

                case 3:
                    ViewModel.CopyToClipboardCommand.Execute(null);
                    break;

                case 4:
                    ViewModel.OpenInMalCommand.Execute(null);
                    break;

                case 5:
                    ViewModel.RemoveAnimeCommand.Execute(null);
                    break;

                case 6:
                    ViewModel.IsRewatching = !ViewModel.IsRewatching;
                    break;

                case 7:
                    ViewModel.CopyTitleToClipboardCommand.Execute(null);
                    break;

                case 8:
                    var counts = Enumerable.Range(0, 10);
                    var pickerResult = await ShowItemsPicker(counts.Select(i1 => i1.ToString()), 0,
                        "Times rewatched", "Cancel", "Ok");
                    if(pickerResult.HasValue)
                        ViewModel.SetRewatchingCountCommand.Execute(pickerResult.Value);
                    break;
            }
            _menu?.Dismiss(true);
            _menu = null;
        }

        public async Task<int?> ShowItemsPicker(
            IEnumerable<string> items,
            int selectedIndex,
            string title,
            string cancelText,
            string okText)
        {
            var semaphore = new SemaphoreSlim(0);
            var builder = new AlertDialog.Builder(Context);

            int? selectedItem = selectedIndex;
            builder.SetTitle(title);
            builder.SetSingleChoiceItems(items.ToArray(), selectedIndex, (sender, args) =>
            {
                selectedItem = args.Which;
            });
            builder.SetNegativeButton(cancelText, (sender, args) =>
            {
                selectedItem = null;
                semaphore.Release();
            });
            builder.SetPositiveButton(okText, (sender, args) => semaphore.Release());

            var dialog = builder.Create();
            dialog.SetCanceledOnTouchOutside(false);
            dialog.SetCancelable(false);
            dialog.Show();

            await semaphore.WaitAsync();
            dialog.Dismiss();

            return selectedItem;
        }

        private void AnimeDetailsPageWatchedButtonOnClick()
        {
            AnimeUpdateDialogBuilder.BuildWatchedDialog(ViewModel.AnimeItemReference as AnimeItemViewModel,
                (model, s) =>
                {
                    if (ViewModel != null)
                    {
                        ViewModel.WatchedEpsInput = s;
                        ViewModel.ChangeWatchedCommand.Execute(null);
                    }
                    else
                    {
                        ResourceLocator.SnackbarProvider.ShowText("Failed to update.");
                    }

                });
        }

        private void AnimeDetailsPageScoreButtonOnClick()
        {
            AnimeUpdateDialogBuilder.BuildScoreDialog(ViewModel.AnimeItemReference, i =>
            {
                ViewModel.ChangeScoreCommand.Execute(i.ToString());
            });
        }

        private void AnimeDetailsPageStatusButtonOnClick()
        {
            AnimeUpdateDialogBuilder.BuildStatusDialog(ViewModel.AnimeItemReference, ViewModel.AnimeMode, status =>
             {
                 ViewModel.ChangeStatus(status);
             });
        }

        private void AnimeDetailsPageVolumesButtonOnClick()
        {
            AnimeUpdateDialogBuilder.BuildWatchedDialog(ViewModel.AnimeItemReference as AnimeItemViewModel,
                (model, s) =>
                {
                    if (ViewModel != null)
                    {
                        ViewModel.ReadVolumesInput = s;
                        ViewModel.ChangeVolumesCommand.Execute(null);
                    }
                    else
                    {
                        ResourceLocator.SnackbarProvider.ShowText("Failed to update.");
                    }
                }, true);
        }

        public override int LayoutResourceId => Resource.Layout.AnimeDetailsPage;

        public override void DetachBindings()
        {
            base.DetachBindings();
        }
    }
}
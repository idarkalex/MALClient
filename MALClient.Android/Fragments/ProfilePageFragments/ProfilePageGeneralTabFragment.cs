using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Net.Wifi.Hotspot2.Pps;
using Android.OS;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using FFImageLoading;
using FFImageLoading.Views;
using GalaSoft.MvvmLight.Helpers;
using MALClient.Android.BindingConverters;
using MALClient.Android.Dialogs;
using MALClient.Android.DIalogs;
using MALClient.Android.Listeners;
using MALClient.Android.Resources;
using MALClient.Android.UserControls;
using MALClient.Android.ViewHolders;
using MALClient.Models.Models;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALClient.Android.Fragments.ProfilePageFragments
{
    public class ProfilePageGeneralTabFragment : MalFragmentBase
    {
        private ProfilePageViewModel ViewModel = ViewModelLocator.ProfilePage;

        protected override void Init(Bundle savedInstanceState)
        {
        }

        protected override void InitBindings()
        {
            Bindings.Add(this.SetBinding(() => ViewModel.CurrentData)
                .WhenSourceChanges( async () =>
                {
                    ProfilePageGeneralTabScrollingContainer.ScrollY = 0;

                    if (string.IsNullOrEmpty(ViewModel.CurrentData.User.ImgUrl))
                    {
                        ProfilePageGeneralTabAnimeUserImg.Visibility = ViewStates.Invisible;
                        ProfilePageGeneralTabImagePlaceholder.Visibility = ViewStates.Visible;
                    }
                    else
                    {
                        ProfilePageGeneralTabAnimeUserImg.Into(ViewModel.CurrentData.User.ImgUrl);
                        ProfilePageGeneralTabImagePlaceholder.Visibility = ViewStates.Gone;
                    }


                    ProfilePageGeneralTabDetailsList.SetAdapter(
                        ViewModel.CurrentData.Details.GetAdapter(GetDetailTemplateDelegate));

                    ProfilePageGeneralTabFriendsGrid.ItemHeight =
                        ProfilePageGeneralTabFriendsGrid.ItemWidth = DimensionsHelper.DpToPx(65);
                    ProfilePageGeneralTabFriendsGrid.SetColumnWidth((int)ProfilePageGeneralTabFriendsGrid.ItemWidth);
                    if (ViewModel.CurrentData.Friends.Any())
                    {
                        ProfilePageGeneralTabFriendsGrid.Adapter =
                            ViewModel.CurrentData.Friends.GetAdapter(GetFriendTemplateDelegate);
                        ProfilePageGeneralTabFriendsEmptyNotice.Visibility = ViewStates.Gone;
                        ProfilePageGeneralTabMoreFriendsButton.Visibility = ViewStates.Visible;
                    }
                    else
                    {
                        ProfilePageGeneralTabFriendsGrid.Adapter = null;
                        ProfilePageGeneralTabFriendsEmptyNotice.Visibility = ViewStates.Visible;
                        ProfilePageGeneralTabMoreFriendsButton.Visibility = ViewStates.Gone;
                    }

                    await Task.Delay(500);
                    PopulateComments();
                }));

            Bindings.Add(
                this.SetBinding(() => ViewModel.EmptyCommentsNoticeVisibility,
                    () => ProfilePageGeneralTabCommentsEmptyNotice.Visibility)
                    .ConvertSourceToTarget(Converters.BoolToVisibility));

            Bindings.Add(this.SetBinding(() => ViewModel.LoadingOhersLibrariesProgressVisiblity).WhenSourceChanges(() =>
            {
                if (ViewModel.LoadingOhersLibrariesProgressVisiblity)
                {
                    ProfilePageGeneralTabCompareList.Enabled = false;
                }
                else
                {
                    ProfilePageGeneralTabCompareList.Enabled = true;
                }
            }));

            Bindings.Add(this.SetBinding(() => ViewModel.CommentInputBoxVisibility).WhenSourceChanges(() =>
            {
                if (ViewModel.CommentInputBoxVisibility)
                {
                    ProfilePageGeneralTabCommentInput.Visibility = ViewStates.Visible;
                    ProfilePageGeneralTabSendCommentButton.Visibility = ViewStates.Visible;

                    if(ViewModel.CurrentData.IsFriend)
                        ProfilePageGeneralTabRemoveFriendButton.Visibility = ViewStates.Visible;
                    else
                        ProfilePageGeneralTabRemoveFriendButton.Visibility = ViewStates.Gone;

                    if(ViewModel.CurrentData.CanAddFriend)
                        ProfilePageGeneralTabSendRequestButton.Visibility = ViewStates.Visible;
                    else
                        ProfilePageGeneralTabSendRequestButton.Visibility = ViewStates.Gone;
                }
                else
                {
                    ProfilePageGeneralTabCommentInput.Visibility = ViewStates.Gone;
                    ProfilePageGeneralTabSendCommentButton.Visibility = ViewStates.Gone;
                    ProfilePageGeneralTabSendRequestButton.Visibility = ViewStates.Gone;
                    ProfilePageGeneralTabRemoveFriendButton.Visibility = ViewStates.Gone;
                }
            }));

            Bindings.Add(
                this.SetBinding(() => ViewModel.IsMyProfile,
                    () => ProfilePageGeneralTabCompareList.Visibility)
                    .ConvertSourceToTarget(Converters.BoolToVisibilityInverted));
          
            Bindings.Add(
                this.SetBinding(() => ViewModel.CommentText,
                    () => ProfilePageGeneralTabCommentInput.Text,BindingMode.TwoWay));

            Bindings.Add(
                this.SetBinding(() => ViewModel.PinProfileVisibility,
                    () => PinButton.Visibility).ConvertSourceToTarget(Converters.BoolToVisibility));

            Bindings.Add(
                this.SetBinding(() => ViewModel.LoadingCommentsVisiblity,
                    () => AnimeDetailsPageLoadingUpdateSpinner.Visibility)
                    .ConvertSourceToTarget(Converters.BoolToVisibility));


            Bindings.Add(this.SetBinding(() => ViewModel.IsPinned).WhenSourceChanges(() =>
            {
                if(!ViewModel.PinProfileVisibility)
                    return;

                if (ViewModel.IsPinned)
                {
                    PinButton.SetBackgroundColor(new Color(ResourceExtension.AccentColour));
                    PinButtonIcon.Rotation = 0;
                    PinButtonIcon.SetImageResource(Resource.Drawable.icon_unpin);
                }
                else
                {
                    PinButton.SetBackgroundColor(new Color(ResourceExtension.OpaqueAccentColour));
                    PinButtonIcon.Rotation = 45;
                    PinButtonIcon.SetImageResource(Resource.Drawable.icon_pin);
                }
            }));


            PinButton.SetOnClickListener(new OnClickListener(view =>
            {
                ViewModel.IsPinned = !ViewModel.IsPinned;
                PinButtonIcon.Rotation = ViewModel.IsPinned ? 0 : 45;
            }));

            ProfilePageGeneralTabAnimeListButton.SetOnClickListener(new OnClickListener(v => ViewModel.NavigateAnimeListCommand.Execute(null)));
            ProfilePageGeneralTabMangaListButton.SetOnClickListener(new OnClickListener(v => ViewModel.NavigateMangaListCommand.Execute(null)));
            ProfilePageGeneralTabHistoryButton.SetOnClickListener(new OnClickListener(v => ViewModel.NavigateHistoryCommand.Execute(null)));
            ProfilePageGeneralTabSendCommentButton.SetOnClickListener(new OnClickListener(v => ViewModel.SendCommentCommand.Execute(null)));
            ProfilePageGeneralTabActionButton.SetOnClickListener(new OnClickListener(v => ProfilePageGeneralTabActionButtonOnClick()));
            ProfilePageGeneralTabCompareList.SetOnClickListener(new OnClickListener(v => ViewModel.NavigateComparisonCommand.Execute(null)));
            ProfilePageGeneralTabMoreFriendsButton.SetOnClickListener(new OnClickListener(v => ViewModel.NavigateFriendsCommand.Execute(null)));
            ReloadButton.SetOnClickListener(new OnClickListener(view => ViewModel.RefreshCommentsCommand.Execute(null)));
            ProfilePageGeneralTabSendRequestButton.SetOnClickListener(new OnClickListener(view => AddFriendDialog.Instance.ShowDialog(Activity,ViewModel.CurrentData,this)));
            ProfilePageGeneralTabRemoveFriendButton.SetOnClickListener(new OnClickListener(view => ViewModel.RemoveFriendCommand.Execute(null)));
            AboutButton.SetOnClickListener(new OnClickListener(view => ProfileDescriptionDialog.Instance.ShowDialog(Activity,RootView,ViewModel.CurrentData.HtmlContent)));
            ShareButton.SetOnClickListener(new OnClickListener(view => ShareProfile()));
            
            FindViewById<FloatingActionButton>(Resource.Id.ReportUserButton).SetOnClickListener(new OnClickListener(view =>
            {
                ResourceLocator.MessageDialogProvider.ShowMessageDialogWithInput(
                    "If you wish to report or block this user you will be redirected to the web form on the MyAnimeList website. Please sign-in in the browser in order to access it, being a 3rd party app I cannot provide direct in-app form.",
                    "Report", "Report", "Block",
                    () =>
                    {
                        ResourceLocator.SystemControlsLauncherService.LaunchUri(
                            new Uri("https://myanimelist.net/modules.php?go=report"));
                    },
                    () =>
                    {
                        ResourceLocator.SystemControlsLauncherService.LaunchUri(
                            new Uri("https://myanimelist.net/editprofile.php?go=privacy"));
                    });
            }));


            PopulateComments();

            ProfilePageGeneralTabScrollingContainer.ViewTreeObserver.ScrollChanged -= ViewTreeObserverOnScrollChanged;
            ProfilePageGeneralTabScrollingContainer.ViewTreeObserver.ScrollChanged += ViewTreeObserverOnScrollChanged;
            ProfilePageGeneralTabScrollingContainer.Touch -= RootViewOnTouch;
            ProfilePageGeneralTabScrollingContainer.Touch += RootViewOnTouch;
            ProfilePageGeneralTabCommentsList.Touch -= RootViewOnTouch;
            ProfilePageGeneralTabCommentsList.Touch += RootViewOnTouch;
        }

        private void ShareProfile()
        {
            try
            {
                var shareIntent = new Intent(Intent.ActionSend);
                shareIntent.SetType("text/plain");
                shareIntent.PutExtra(Intent.ExtraSubject, "MAL profile");
                if(ViewModel.IsMyProfile)
                    shareIntent.PutExtra(Intent.ExtraText, $"My MAL profile: https://myanimelist.net/profile/{Credentials.UserName}");
                else
                    shareIntent.PutExtra(Intent.ExtraText, $"{ViewModel.CurrentData.User.Name}'s MAL profile: https://myanimelist.net/profile/{ViewModel.CurrentData.User.Name}");
                Activity.StartActivity(Intent.CreateChooser(shareIntent, "How to share this profile"));
            }
            catch (Exception e)
            {

            }
        }

        #region ScrollHandling

        private async void RootViewOnTouch(object sender, View.TouchEventArgs touchEventArgs)
        {
            touchEventArgs.Handled = false;
            if (touchEventArgs.Event.Action == MotionEventActions.Up ||
                touchEventArgs.Event.Action == MotionEventActions.Cancel)
            {
                await Task.Delay(200); //why? because fling... I should listen for when it ends but 200ms seem to do the job just fine /shrug
                if (_disableNestedScrollingOnTouchUp)
                {

                    if (ProfilePageGeneralTabScrollingContainer.GetChildAt(0).Bottom >
                        ProfilePageGeneralTabScrollingContainer.Height + ProfilePageGeneralTabScrollingContainer.ScrollY)
                        ProfilePageGeneralTabCommentsList.NestedScrollingEnabled = false;

                    _disableNestedScrollingOnTouchUp = false;
                }
            }
        }

        private bool _disableNestedScrollingOnTouchUp;
        private void ViewTreeObserverOnScrollChanged(object sender, EventArgs eventArgs)
        {
            if (ProfilePageGeneralTabScrollingContainer.GetChildAt(0).Bottom <=
                (ProfilePageGeneralTabScrollingContainer.Height + ProfilePageGeneralTabScrollingContainer.ScrollY))
            {
                ProfilePageGeneralTabCommentsList.NestedScrollingEnabled = true;
            }
            else
            {
                _disableNestedScrollingOnTouchUp = true;
            }
        }

        #endregion

        #region Comments
        private static int _unbounded = View.MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified);

        private void PopulateComments()
        {
            Bindings.Add(this.SetBinding(() => ViewModel.MalComments).WhenSourceChanges(() =>
            {
                ProfilePageGeneralTabCommentsList.InjectFlingAdapter(ViewModel.MalComments,
                    view => new CommentViewHolder(view), DataTemplateFull, DataTemplateFling, DataTemplateBasic,
                    ContainerTemplate);
                ProfilePageGeneralTabCommentsList.Post(() =>
                {

                    int grossElementHeight = 0;
                    if (ProfilePageGeneralTabCommentsList.Adapter.Count > 0)
                    {
                        for (int i = 0;
                            i < (ProfilePageGeneralTabCommentsList.Adapter.Count > 4
                                ? 4
                                : ProfilePageGeneralTabCommentsList.Adapter.Count);
                            i++)
                        {
                            View childView =
                                ProfilePageGeneralTabCommentsList.Adapter.GetView(i, null, ProfilePageGeneralTabCommentsList);
                            childView.Measure(_unbounded, _unbounded);
                            grossElementHeight += childView.MeasuredHeight;
                        }
                    }
                    else
                    {
                        grossElementHeight = DimensionsHelper.DpToPx(100);
                    }


                    ProfilePageGeneralTabCommentsList.LayoutParameters.Height = grossElementHeight;
                    ProfilePageGeneralTabCommentsList.NestedScrollingEnabled = false;
                });
            }));
            
        }

        private View ContainerTemplate(int i1)
        {
            var view = Activity.LayoutInflater.Inflate(Resource.Layout.ProfilePageGeneralTabCommentItem, null);

            view.FindViewById(Resource.Id.ProfilePageGeneralTabCommentItemDeleteButton)
                .SetOnClickListener(new OnClickListener(OnCommentDeleteClick));
            view.FindViewById(Resource.Id.ProfilePageGeneralTabCommentItemConvButton)
                .SetOnClickListener(new OnClickListener(OnCommentConversationClick));
            view.FindViewById(Resource.Id.ProfilePageGeneralTabCommentItemImgButton)
                .SetOnClickListener(new OnClickListener(OnCommentAuthorClick));

            view.FindViewById(Resource.Id.ReportCommentButton).SetOnClickListener(new OnClickListener(view =>
            {
                ResourceLocator.MessageDialogProvider.ShowMessageDialogWithInput(
                    "If you wish to report this comment or block its author you will be redirected to the web form on the MyAnimeList website. Please sign-in in the browser in order to access it, being a 3rd party app I cannot provide direct in-app form.",
                    "Report or Block", "Report", "Block Author",
                    () =>
                    {
                        ResourceLocator.SystemControlsLauncherService.LaunchUri(
                            new Uri("https://myanimelist.net/modules.php?go=report"));
                    },
                    () =>
                    {
                        ResourceLocator.SystemControlsLauncherService.LaunchUri(
                            new Uri("https://myanimelist.net/editprofile.php?go=privacy"));
                    });
            }));

            return view;
        }

        private void DataTemplateBasic(View view1, int i1, MalComment malComment, CommentViewHolder arg4)
        {
            arg4.ProfilePageGeneralTabCommentItemUsername.Text = malComment.User.Name;
            arg4.ProfilePageGeneralTabCommentItemDate.Text = malComment.Date;
            arg4.ProfilePageGeneralTabCommentItemContent.Text = malComment.Content;
            arg4.ProfilePageGeneralTabCommentItemDeleteButton.Visibility =
                malComment.CanDelete ? ViewStates.Visible : ViewStates.Gone;
            arg4.ProfilePageGeneralTabCommentItemConvButton.Visibility = string.IsNullOrEmpty(malComment.ComToCom)
                ? ViewStates.Gone
                : ViewStates.Visible;


            arg4.ProfilePageGeneralTabCommentItemDeleteButton.Tag =
                arg4.ProfilePageGeneralTabCommentItemConvButton.Tag =
                    arg4.ProfilePageGeneralTabCommentItemImgButton.Tag = view1.Tag;
        }

        private void DataTemplateFling(View view1, int i1, MalComment arg3, CommentViewHolder arg4)
        {
            if(!arg4.ProfilePageGeneralTabCommentItemUserImg.IntoIfLoaded(arg3.User.ImgUrl))
                arg4.ProfilePageGeneralTabCommentItemUserImg.Visibility = ViewStates.Invisible;
        }

        private void DataTemplateFull(View view1, int i1, MalComment arg3, CommentViewHolder arg4)
        {
            arg4.ProfilePageGeneralTabCommentItemUserImg.Into(arg3.User.ImgUrl);
        }

        private void OnCommentConversationClick(View sender)
        {
            ViewModel.NavigateConversationCommand.Execute(sender.Tag.Unwrap<MalComment>());
        }

        private void OnCommentDeleteClick(View sender)
        {
            ViewModel.DeleteCommentCommand.Execute(sender.Tag.Unwrap<MalComment>());
        }

        private void OnCommentAuthorClick(View sender)
        {
            ViewModel.NavigateProfileCommand.Execute((sender as View).Tag.Unwrap<MalComment>().User);
        }

        #endregion

        private async void ProfilePageGeneralTabActionButtonOnClick()
        {
            var str = await TextInputDialogBuilder.BuildInputTextDialog(Context, "Find user", "username...","Go");
            if(!string.IsNullOrEmpty(str))
                ViewModel.NavigateProfileCommand.Execute(new MalUser{Name = str});
        }

        private View GetFriendTemplateDelegate(int i, MalUser malUser, View convertView)
        {
            var view = convertView;
            if (view == null)
            {
                view = Activity.LayoutInflater.Inflate(Resource.Layout.ProfilePageGeneralTabFriendItem, null);
                view.SetOnClickListener(new OnClickListener(FriendButtonOnClick));
            }

            var img = (view as FrameLayout).FindViewById<ImageViewAsync>(Resource.Id.ProfilePageGeneralTabFriendItemImage);

            img.Into(malUser.ImgUrl);

            view.Tag = malUser.Wrap();

            return view;
        }


        private View GetDetailTemplateDelegate(int i, Tuple<string, string> tuple, View convertView)
        {
            var view = convertView ??
                       Activity.LayoutInflater.Inflate(Resource.Layout.ProfilePageGeneralTabDetailsItem, null);

            try
            {
                view.FindViewById<TextView>(Resource.Id.ProfilePageGeneralTabDetailsItemLeft).Text = tuple?.Item1;
                view.FindViewById<TextView>(Resource.Id.ProfilePageGeneralTabDetailsItemRight).Text = tuple?.Item2;
            }
            catch (Exception e)
            {
                ResourceLocator.TelemetryProvider.TrackException(e);
                //random crash
            }


            return view;
        }



        private void FriendButtonOnClick(View sender)
        {
            ViewModel.NavigateProfileCommand.Execute((sender as View).Tag.Unwrap<MalUser>());
        }


        public override int LayoutResourceId => Resource.Layout.ProfilePageGeneralTab;

        #region Views

        private ImageView _profilePageGeneralTabImagePlaceholder;
        private ImageViewAsync _profilePageGeneralTabAnimeUserImg;
        private FloatingActionButton _aboutButton;
        private LinearLayout _profilePageGeneralTabDetailsList;
        private Button _profilePageGeneralTabAnimeListButton;
        private ImageButton _profilePageGeneralTabCompareList;
        private Button _profilePageGeneralTabMangaListButton;
        private Button _profilePageGeneralTabHistoryButton;
        private ImageView _pinButtonIcon;
        private FrameLayout _pinButton;
        private FrameLayout _shareButton;
        private ImageButton _profilePageGeneralTabSendRequestButton;
        private ImageButton _profilePageGeneralTabRemoveFriendButton;
        private ImageButton _profilePageGeneralTabMoreFriendsButton;
        private ExpandableGridView _profilePageGeneralTabFriendsGrid;
        private TextView _profilePageGeneralTabFriendsEmptyNotice;
        private ProgressBar _animeDetailsPageLoadingUpdateSpinner;
        private ImageButton _reloadButton;
        private EditText _profilePageGeneralTabCommentInput;
        private Button _profilePageGeneralTabSendCommentButton;
        private LinearLayout _profilePageGeneralTabCommentSection;
        private TextView _profilePageGeneralTabCommentsEmptyNotice;
        private ListView _profilePageGeneralTabCommentsList;
        private ScrollView _profilePageGeneralTabScrollingContainer;
        private FloatingActionButton _profilePageGeneralTabActionButton;

        public ImageView ProfilePageGeneralTabImagePlaceholder => GetView(ref _profilePageGeneralTabImagePlaceholder, Resource.Id.ProfilePageGeneralTabImagePlaceholder);
        public ImageViewAsync ProfilePageGeneralTabAnimeUserImg => GetView(ref _profilePageGeneralTabAnimeUserImg, Resource.Id.ProfilePageGeneralTabAnimeUserImg);
        public FloatingActionButton AboutButton => GetView(ref _aboutButton, Resource.Id.AboutButton);
        public LinearLayout ProfilePageGeneralTabDetailsList => GetView(ref _profilePageGeneralTabDetailsList, Resource.Id.ProfilePageGeneralTabDetailsList);
        public Button ProfilePageGeneralTabAnimeListButton => GetView(ref _profilePageGeneralTabAnimeListButton, Resource.Id.ProfilePageGeneralTabAnimeListButton);
        public ImageButton ProfilePageGeneralTabCompareList => GetView(ref _profilePageGeneralTabCompareList, Resource.Id.ProfilePageGeneralTabCompareList);
        public Button ProfilePageGeneralTabMangaListButton => GetView(ref _profilePageGeneralTabMangaListButton, Resource.Id.ProfilePageGeneralTabMangaListButton);
        public Button ProfilePageGeneralTabHistoryButton => GetView(ref _profilePageGeneralTabHistoryButton, Resource.Id.ProfilePageGeneralTabHistoryButton);
        public ImageView PinButtonIcon => GetView(ref _pinButtonIcon, Resource.Id.PinButtonIcon);
        public FrameLayout PinButton => GetView(ref _pinButton, Resource.Id.PinButton);
        public FrameLayout ShareButton => GetView(ref _shareButton, Resource.Id.ShareButton);
        public ImageButton ProfilePageGeneralTabSendRequestButton => GetView(ref _profilePageGeneralTabSendRequestButton, Resource.Id.ProfilePageGeneralTabSendRequestButton);
        public ImageButton ProfilePageGeneralTabRemoveFriendButton => GetView(ref _profilePageGeneralTabRemoveFriendButton, Resource.Id.ProfilePageGeneralTabRemoveFriendButton);
        public ImageButton ProfilePageGeneralTabMoreFriendsButton => GetView(ref _profilePageGeneralTabMoreFriendsButton, Resource.Id.ProfilePageGeneralTabMoreFriendsButton);
        public ExpandableGridView ProfilePageGeneralTabFriendsGrid => GetView(ref _profilePageGeneralTabFriendsGrid, Resource.Id.ProfilePageGeneralTabFriendsGrid);
        public TextView ProfilePageGeneralTabFriendsEmptyNotice => GetView(ref _profilePageGeneralTabFriendsEmptyNotice, Resource.Id.ProfilePageGeneralTabFriendsEmptyNotice);
        public ProgressBar AnimeDetailsPageLoadingUpdateSpinner => GetView(ref _animeDetailsPageLoadingUpdateSpinner, Resource.Id.AnimeDetailsPageLoadingUpdateSpinner);
        public ImageButton ReloadButton => GetView(ref _reloadButton, Resource.Id.ReloadButton);
        public EditText ProfilePageGeneralTabCommentInput => GetView(ref _profilePageGeneralTabCommentInput, Resource.Id.ProfilePageGeneralTabCommentInput);
        public Button ProfilePageGeneralTabSendCommentButton => GetView(ref _profilePageGeneralTabSendCommentButton, Resource.Id.ProfilePageGeneralTabSendCommentButton);
        public LinearLayout ProfilePageGeneralTabCommentSection => GetView(ref _profilePageGeneralTabCommentSection, Resource.Id.ProfilePageGeneralTabCommentSection);
        public TextView ProfilePageGeneralTabCommentsEmptyNotice => GetView(ref _profilePageGeneralTabCommentsEmptyNotice, Resource.Id.ProfilePageGeneralTabCommentsEmptyNotice);
        public ListView ProfilePageGeneralTabCommentsList => GetView(ref _profilePageGeneralTabCommentsList, Resource.Id.ProfilePageGeneralTabCommentsList);
        public ScrollView ProfilePageGeneralTabScrollingContainer => GetView(ref _profilePageGeneralTabScrollingContainer, Resource.Id.ProfilePageGeneralTabScrollingContainer);
        public FloatingActionButton ProfilePageGeneralTabActionButton => GetView(ref _profilePageGeneralTabActionButton, Resource.Id.ProfilePageGeneralTabActionButton);

        #endregion

    }
}
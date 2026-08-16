using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Java.Lang;
using MALClient.Android.Activities;
using MALClient.Android.Adapters;
using MALClient.Android.DIalogs;
using MALClient.Android.Listeners;
using MALClient.Android.ViewModels;
using MALClient.XShared.ViewModels;
using Org.Json;
using Exception = System.Exception;

namespace MALClient.Android.Fragments.SettingsFragments
{
    public class SettingsAboutFragment : SettingsFragmentBase
    {
        protected override void InitBindings()
        {
            AboutPageViewSourceButton.SetOnClickListener(
                new OnClickListener(view => ResourceLocator.SystemControlsLauncherService.LaunchUri(
                    new Uri("https://github.com/idarkalex/MALClient"))));

            AboutPageIssuesBoard.SetOnClickListener(
                new OnClickListener(view => ResourceLocator.SystemControlsLauncherService.LaunchUri(
                    new Uri("https://github.com/idarkalex/MALClient/issues"))));

            AboutPageChangelogButton.SetOnClickListener(
                new OnClickListener(view => ChangelogDialog.BuildChangelogDialog(ResourceLocator.ChangelogProvider)));

            AboutPageCheckUpdatesButton.SetOnClickListener(new OnClickListener(view =>
            {
                AboutPageCheckUpdatesButton.Enabled = false;
                CheckForUpdates();
            }));
        }

        private async void CheckForUpdates()
        {
            var info = await ViewModelLocator.GeneralMain.CheckForUpdatesAsync();
            AboutPageCheckUpdatesButton.Enabled = true;

            if (info == null)
            {
                ResourceLocator.MessageDialogProvider.ShowMessageDialog(
                    $"You are running the latest version of MAL+ ({ResourceLocator.ChangelogProvider.CurrentVersion}).",
                    "Check for updates");
            }
            else
            {
                MainActivity.PromptUpdate();
            }
        }

        public override int LayoutResourceId => Resource.Layout.SettingsPageAbout;

        #region Views

        private Button _aboutPageViewSourceButton;
        private Button _aboutPageIssuesBoard;
        private Button _aboutPageChangelogButton;
        private Button _aboutPageCheckUpdatesButton;

        public Button AboutPageViewSourceButton => GetView(ref _aboutPageViewSourceButton, Resource.Id.AboutPageViewSourceButton);

        public Button AboutPageIssuesBoard => GetView(ref _aboutPageIssuesBoard, Resource.Id.AboutPageIssuesBoard);

        public Button AboutPageChangelogButton => GetView(ref _aboutPageChangelogButton, Resource.Id.AboutPageChangelogButton);

        public Button AboutPageCheckUpdatesButton => GetView(ref _aboutPageCheckUpdatesButton, Resource.Id.AboutPageCheckUpdatesButton);

        #endregion
    }
}
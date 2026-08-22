using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using GalaSoft.MvvmLight.Helpers;
using MALClient.Android.Listeners;
using MALClient.Android.Resources;
using MALClient.Android.UserControls;
using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Forums;

namespace MALClient.Android.Fragments.ForumFragments
{
    public class ForumNewTopicPageFragment : MalFragmentBase
    {
        private readonly ForumsNewTopicNavigationArgs _args;
        private ForumNewTopicViewModel ViewModel;

        public ForumNewTopicPageFragment(ForumsNewTopicNavigationArgs args)
        {
            _args = args;
        }

        protected override void Init(Bundle savedInstanceState)
        {
            ViewModel = ViewModelLocator.ForumsNewTopic;
            ViewModel.Init(_args);
        }

        public override void OnResume()
        {
            ViewModel.UpdatePreview += ViewModelOnUpdatePreview;
            base.OnResume();
        }

        private void ViewModelOnUpdatePreview(object sender, string s)
        {
            ForumNewTopicPagePreview
                .LoadDataWithBaseURL(null, ResourceLocator.CssManager.WrapWithCss(s.Replace("\n", "<br>")),
                    "text/html; charset=utf-8", "UTF-8", null);
        }

        protected override void InitBindings()
        {

            ForumNewTopicPageEditor.Text = ViewModel.Message;
            if(!string.IsNullOrEmpty(ViewModel.Message))
                ViewModel.PreviewCommand.Execute(null);

            ForumNewTopicPageEditor.TextChanged += ForumNewTopicPageEditorOnTextChanged;

            Bindings.Add(
                this.SetBinding(() => ViewModel.Header,
                    () => ForumNewTopicPageTitle.Text));

            Bindings.Add(
                this.SetBinding(() => ViewModel.Title,
                    () => ForumNewTopicPageTitleTextBox.Text,BindingMode.TwoWay));

            Bindings.Add(this.SetBinding(() => ViewModel.IsSendButtonEnabled).WhenSourceChanges(() =>
            {
                ForumNewTopicPageAcceptButton.Enabled = ViewModel.IsSendButtonEnabled;
            }));

            ForumNewTopicPagePreview.SetBackgroundColor(new Color(ResourceExtension.BrushAnimeItemBackground));

            ForumNewTopicPageAcceptButton.SetOnClickListener(new OnClickListener(view =>
            {
                if (ViewModel.IsSendButtonEnabled)
                    ViewModel.CreateTopicCommand.Execute(null);
            }));

        }

        private void ForumNewTopicPageEditorOnTextChanged(object sender, string s)
        {
            ViewModel.Message = s;
        }


        protected override void Cleanup()
        {
            ViewModel.UpdatePreview -= ViewModelOnUpdatePreview;
            base.Cleanup();
        }

        public override int LayoutResourceId => Resource.Layout.ForumNewTopicPage;

        #region Views

        private TextView _forumNewTopicPageTitle;
        private EditText _forumNewTopicPageTitleTextBox;
        private BBCodeEditor _forumNewTopicPageEditor;
        private Button _forumNewTopicPageAcceptButton;
        private WebView _forumNewTopicPagePreview;
        private LinearLayout _forumNewTopicPageInputSection;

        public TextView ForumNewTopicPageTitle => GetView(ref _forumNewTopicPageTitle, Resource.Id.ForumNewTopicPageTitle);

        public EditText ForumNewTopicPageTitleTextBox => GetView(ref _forumNewTopicPageTitleTextBox, Resource.Id.ForumNewTopicPageTitleTextBox);

        public BBCodeEditor ForumNewTopicPageEditor => GetView(ref _forumNewTopicPageEditor, Resource.Id.ForumNewTopicPageEditor);

        public Button ForumNewTopicPageAcceptButton => GetView(ref _forumNewTopicPageAcceptButton, Resource.Id.ForumNewTopicPageAcceptButton);

        public WebView ForumNewTopicPagePreview => GetView(ref _forumNewTopicPagePreview, Resource.Id.ForumNewTopicPagePreview);

        public LinearLayout ForumNewTopicPageInputSection => GetView(ref _forumNewTopicPageInputSection, Resource.Id.ForumNewTopicPageInputSection);




        #endregion
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.App;
using Android.Support.V7.App;
using Android.Util;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using GalaSoft.MvvmLight.Helpers;
using Java.Lang;
using MALClient.Android.BindingConverters;
using MALClient.Android.Resources;
using MALClient.Models.Enums;

namespace MALClient.Android.Fragments
{
    public partial class LogInPageFragment : MalFragmentBase
    {
        #region Views

        private WebView _authWebView;
        private Button _signInButton;
        private ProgressBar _loginPageLoadingSpinner;
        private Button _loginPageLogOutButton;
        private Button _loginPageProblemsButton;
        private Button _loginPageRegisterButton;
        private FrameLayout _bottomButtonsSection;

        public WebView AuthWebView => GetView(ref _authWebView, Resource.Id.AuthWebView);
        public Button SignInButton => GetView(ref _signInButton, Resource.Id.SignInButton);
        public ProgressBar LoginPageLoadingSpinner => GetView(ref _loginPageLoadingSpinner, Resource.Id.LoginPageLoadingSpinner);
        public Button LoginPageLogOutButton => GetView(ref _loginPageLogOutButton, Resource.Id.LoginPageLogOutButton);
        public Button LoginPageProblemsButton => GetView(ref _loginPageProblemsButton, Resource.Id.LoginPageProblemsButton);
        public Button LoginPageRegisterButton => GetView(ref _loginPageRegisterButton, Resource.Id.LoginPageRegisterButton);
        public FrameLayout BottomButtonsSection => GetView(ref _bottomButtonsSection, Resource.Id.BottomButtonsSection);

        #endregion
    }
}
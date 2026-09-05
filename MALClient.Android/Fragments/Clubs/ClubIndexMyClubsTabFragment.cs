using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Com.Mikepenz.Materialdrawer;
using GalaSoft.MvvmLight.Helpers;
using MALClient.Android.BindingConverters;
using MALClient.Android.Utilities;
using MALClient.Models.Models.MalSpecific;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Clubs;
using MALClient.XShared.ViewModels.Main;

namespace MALClient.Android.Fragments.Clubs
{
    public class ClubIndexMyClubsTabFragment : ClubIndexTabFragmentBase
    {
        protected override void Init(Bundle savedInstanceState)
        {

        }

        protected override void InitBindings()
        {
            Bindings.Add(this.SetBinding(() => ViewModel.MyClubs).WhenSourceChanges(() =>
            {
                if (ViewModel.MyClubs == null)
                    List.Adapter = null;
                else
                    List.InjectFlingAdapter(ViewModel.MyClubs, ViewHolderFactory, DataTemplateFull, DataTemplateFling, DataTemplateBasic, ContainerTemplate);
            }));

            Bindings.Add(
                this.SetBinding(() => ViewModel.EmptyNoticeVisibility,
                    () => EmptyNotice.Visibility).ConvertSourceToTarget(Converters.BoolToVisibility));

            
        }

        public override int LayoutResourceId => Resource.Layout.ClubsIndexMyClubsTab;

        public override void OnPause()
        {
            try
            {
                ScrollStateHelper.SaveAbsListView(List, FragmentUiState.ClubsIndex, "MyClubs");
            }
            catch { }
            base.OnPause();
        }

        public override void OnResume()
        {
            base.OnResume();
            try
            {
                ScrollStateHelper.RestoreAbsListView(List, FragmentUiState.ClubsIndex, "MyClubs");
            }
            catch { }
        }

        #region Views

        private ListView _list;
        private TextView _emptyNotice;

        public ListView List => GetView(ref _list, Resource.Id.List);

        public TextView EmptyNotice => GetView(ref _emptyNotice, Resource.Id.EmptyNotice);

        #endregion
    }
}
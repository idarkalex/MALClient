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
using FFImageLoading;
using FFImageLoading.Views;
using GalaSoft.MvvmLight.Helpers;
using MALClient.Android.CollectionAdapters;
using MALClient.Android.Listeners;
using MALClient.Android.Resources;
using MALClient.Models.Enums;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;

namespace MALClient.Android.Fragments.RecommendationsFragments
{
    public class RecommendationItemFragment : MalFragmentBase
    {
        private bool _delayForInit;

        private RecommendationItemViewModel ViewModel;

        public RecommendationItemFragment()
        {

        }

        public void BindModel(RecommendationItemViewModel viewModel)
        {
            ViewModel = viewModel;

            if (RootView == null)
            {
                _delayForInit = true;
                return;
            }

            Bindings.ForEach(binding => binding.Detach());
            Bindings.Clear();

            Bindings.Add(
                this.SetBinding(() => ViewModel.LoadingSpinnerVisibility).WhenSourceChanges(() =>
                {
                    if (ViewModel.LoadingSpinnerVisibility)
                    {
                        RecommendationItemLoading.Visibility = ViewStates.Visible;
                        return;
                    }
                    else
                    {
                        RecommendationItemLoading.Visibility = ViewStates.Gone;
                    }

                   

                    RecommendationItemDescription.Text = ViewModel.Data.Description;
                    RecommendationItemDepTitle.Text = ViewModel.Data.DependentTitle;
                    RecommendationItemRecTitle.Text = ViewModel.Data.RecommendationTitle;
                    if (ViewModel.Data.AnimeDependentData?.ImgUrl != null)
                        RecommendationItemDepImage.Into(ViewModel.Data.AnimeDependentData.ImgUrl);
                    if (ViewModel.Data.AnimeRecommendationData?.ImgUrl != null)
                        RecommendationItemRecImage.Into(ViewModel.Data.AnimeRecommendationData.ImgUrl);

                    if (ViewModel.DetailItems.Count == 0)
                        return;

                    //Because adapter is slow here
                    //
                    RecommendationItemDetailItemType1.Text = ViewModel.DetailItems[0].Item1;
                    RecommendationItemDetailItemDepValue1.Text = ViewModel.DetailItems[0].Item2;                
                    if (string.IsNullOrEmpty(ViewModel.DetailItems[0].Item3))
                    {
                        RecommendationItemDetailItemMyDepValue1.Visibility = ViewStates.Gone;
                    }
                    else
                    {
                        RecommendationItemDetailItemMyDepValue1.Visibility = ViewStates.Visible;
                        RecommendationItemDetailItemMyDepValue1.Text = ViewModel.DetailItems[0].Item3;
                    }
                    RecommendationItemDetailItemRecValue1.Text = ViewModel.DetailItems[0].Item4;
                    if (string.IsNullOrEmpty(ViewModel.DetailItems[0].Item5))
                    {
                        RecommendationItemDetailItemMyRecValue1.Visibility = ViewStates.Gone;
                    }
                    else
                    {
                        RecommendationItemDetailItemMyRecValue1.Visibility = ViewStates.Visible;
                        RecommendationItemDetailItemMyRecValue1.Text = ViewModel.DetailItems[0].Item5;
                    }

                    //
                    RecommendationItemDetailItemType2.Text = ViewModel.DetailItems[1].Item1;
                    RecommendationItemDetailItemDepValue2.Text = ViewModel.DetailItems[1].Item2;    
                    if (string.IsNullOrEmpty(ViewModel.DetailItems[1].Item3))
                    {
                        RecommendationItemDetailItemMyDepValue2.Visibility = ViewStates.Gone;
                    }
                    else
                    {
                        RecommendationItemDetailItemMyDepValue2.Visibility = ViewStates.Visible;
                        RecommendationItemDetailItemMyDepValue2.Text = ViewModel.DetailItems[1].Item3;
                    }
                    RecommendationItemDetailItemRecValue2.Text = ViewModel.DetailItems[1].Item4;
                    if (string.IsNullOrEmpty(ViewModel.DetailItems[1].Item5))
                    {
                        RecommendationItemDetailItemMyRecValue2.Visibility = ViewStates.Gone;
                    }
                    else
                    {
                        RecommendationItemDetailItemMyRecValue2.Visibility = ViewStates.Visible;
                        RecommendationItemDetailItemMyRecValue2.Text = ViewModel.DetailItems[1].Item5;
                    }
                    //
                    RecommendationItemDetailItemType3.Text = ViewModel.DetailItems[2].Item1;
                    RecommendationItemDetailItemDepValue3.Text = ViewModel.DetailItems[2].Item2;
                    if (string.IsNullOrEmpty(ViewModel.DetailItems[2].Item3))
                    {
                        RecommendationItemDetailItemMyDepValue3.Visibility = ViewStates.Gone;
                    }
                    else
                    {
                        RecommendationItemDetailItemMyDepValue3.Visibility = ViewStates.Visible;
                        RecommendationItemDetailItemMyDepValue3.Text = ViewModel.DetailItems[2].Item3;
                    }
                    RecommendationItemDetailItemRecValue3.Text = ViewModel.DetailItems[2].Item4;
                    if (string.IsNullOrEmpty(ViewModel.DetailItems[2].Item5))
                    {
                        RecommendationItemDetailItemMyRecValue3.Visibility = ViewStates.Gone;
                    }
                    else
                    {
                        RecommendationItemDetailItemMyRecValue3.Visibility = ViewStates.Visible;
                        RecommendationItemDetailItemMyRecValue3.Text = ViewModel.DetailItems[2].Item5;
                    }
                    //
                    RecommendationItemDetailItemType4.Text = ViewModel.DetailItems[3].Item1;
                    RecommendationItemDetailItemDepValue4.Text = ViewModel.DetailItems[3].Item2;
                    if (string.IsNullOrEmpty(ViewModel.DetailItems[3].Item3))
                    {
                        RecommendationItemDetailItemMyDepValue4.Visibility = ViewStates.Gone;
                    }
                    else
                    {
                        RecommendationItemDetailItemMyDepValue4.Visibility = ViewStates.Visible;
                        RecommendationItemDetailItemMyDepValue4.Text = ViewModel.DetailItems[3].Item3;
                    }
                    RecommendationItemDetailItemRecValue4.Text = ViewModel.DetailItems[3].Item4;
                    if (string.IsNullOrEmpty(ViewModel.DetailItems[3].Item5))
                    {
                        RecommendationItemDetailItemMyRecValue4.Visibility = ViewStates.Gone;
                    }
                    else
                    {
                        RecommendationItemDetailItemMyRecValue4.Visibility = ViewStates.Visible;
                        RecommendationItemDetailItemMyRecValue4.Text = ViewModel.DetailItems[3].Item5;
                    }
                    //
                    RecommendationItemDetailItemType5.Text = ViewModel.DetailItems[4].Item1;
                    RecommendationItemDetailItemDepValue5.Text = ViewModel.DetailItems[4].Item2;                   
                    if (string.IsNullOrEmpty(ViewModel.DetailItems[4].Item3))
                    {
                        RecommendationItemDetailItemMyDepValue5.Visibility = ViewStates.Gone;
                    }
                    else
                    {
                        RecommendationItemDetailItemMyDepValue5.Visibility = ViewStates.Visible;
                        RecommendationItemDetailItemMyDepValue5.Text = ViewModel.DetailItems[4].Item3;
                    }
                    RecommendationItemDetailItemRecValue5.Text = ViewModel.DetailItems[4].Item4;
                    if (string.IsNullOrEmpty(ViewModel.DetailItems[4].Item5))
                    {
                        RecommendationItemDetailItemMyRecValue5.Visibility = ViewStates.Gone;
                    }
                    else
                    {
                        RecommendationItemDetailItemMyRecValue5.Visibility = ViewStates.Visible;
                        RecommendationItemDetailItemMyRecValue5.Text = ViewModel.DetailItems[4].Item5;
                    }                    
                    //
                    RecommendationItemDetailItemType6.Text = ViewModel.DetailItems[5].Item1;
                    RecommendationItemDetailItemDepValue6.Text = ViewModel.DetailItems[5].Item2;                   
                    if (string.IsNullOrEmpty(ViewModel.DetailItems[5].Item3))
                    {
                        RecommendationItemDetailItemMyDepValue6.Visibility = ViewStates.Gone;
                    }
                    else
                    {
                        RecommendationItemDetailItemMyDepValue6.Visibility = ViewStates.Visible;
                        RecommendationItemDetailItemMyDepValue6.Text = ViewModel.DetailItems[5].Item3;
                    }
                    RecommendationItemDetailItemRecValue6.Text = ViewModel.DetailItems[5].Item4;
                    if (string.IsNullOrEmpty(ViewModel.DetailItems[5].Item5))
                    {
                        RecommendationItemDetailItemMyRecValue6.Visibility = ViewStates.Gone;
                    }
                    else
                    {
                        RecommendationItemDetailItemMyRecValue6.Visibility = ViewStates.Visible;
                        RecommendationItemDetailItemMyRecValue6.Text = ViewModel.DetailItems[5].Item5;
                    }
                }));

            RecommendationItemRecImageButton.SetOnClickListener(new OnClickListener(view => ViewModel.NavigateRecDetails.Execute(null)));
            RecommendationItemDepImageButton.SetOnClickListener(new OnClickListener(view => ViewModel.NavigateDepDetails.Execute(null)));
        }


        protected override void Init(Bundle savedInstanceState)
        {

        }

        protected override void InitBindings()
        {
            if (_delayForInit)
            {
                _delayForInit = false;
                BindModel(ViewModel);
            }

        }

        public override int LayoutResourceId => Resource.Layout.RecommendationItem;



        #region Views

        private ProgressBar _recommendationItemDepImagePlaceholder;
        private ImageViewAsync _recommendationItemDepImage;
        private FrameLayout _recommendationItemDepImageButton;
        private TextView _recommendationItemDepTitle;
        private ProgressBar _recommendationItemRecImagePlaceholder;
        private ImageViewAsync _recommendationItemRecImage;
        private FrameLayout _recommendationItemRecImageButton;
        private TextView _recommendationItemRecTitle;
        private TextView _recommendationItemDescription;
        private TextView _recommendationItemDetailItemType1;
        private TextView _recommendationItemDetailItemDepValue1;
        private TextView _recommendationItemDetailItemMyDepValue1;
        private TextView _recommendationItemDetailItemRecValue1;
        private TextView _recommendationItemDetailItemMyRecValue1;
        private TextView _recommendationItemDetailItemType2;
        private TextView _recommendationItemDetailItemDepValue2;
        private TextView _recommendationItemDetailItemMyDepValue2;
        private TextView _recommendationItemDetailItemRecValue2;
        private TextView _recommendationItemDetailItemMyRecValue2;
        private TextView _recommendationItemDetailItemType3;
        private TextView _recommendationItemDetailItemDepValue3;
        private TextView _recommendationItemDetailItemMyDepValue3;
        private TextView _recommendationItemDetailItemRecValue3;
        private TextView _recommendationItemDetailItemMyRecValue3;
        private TextView _recommendationItemDetailItemType4;
        private TextView _recommendationItemDetailItemDepValue4;
        private TextView _recommendationItemDetailItemMyDepValue4;
        private TextView _recommendationItemDetailItemRecValue4;
        private TextView _recommendationItemDetailItemMyRecValue4;
        private TextView _recommendationItemDetailItemType5;
        private TextView _recommendationItemDetailItemDepValue5;
        private TextView _recommendationItemDetailItemMyDepValue5;
        private TextView _recommendationItemDetailItemRecValue5;
        private TextView _recommendationItemDetailItemMyRecValue5;
        private TextView _recommendationItemDetailItemType6;
        private TextView _recommendationItemDetailItemDepValue6;
        private TextView _recommendationItemDetailItemMyDepValue6;
        private TextView _recommendationItemDetailItemRecValue6;
        private TextView _recommendationItemDetailItemMyRecValue6;
        private LinearLayout _recommendationItemDetailsContainer;
        private RelativeLayout _recommendationItemLoading;

        public ProgressBar RecommendationItemDepImagePlaceholder => GetView(ref _recommendationItemDepImagePlaceholder, Resource.Id.RecommendationItemDepImagePlaceholder);

        public ImageViewAsync RecommendationItemDepImage => GetView(ref _recommendationItemDepImage, Resource.Id.RecommendationItemDepImage);

        public FrameLayout RecommendationItemDepImageButton => GetView(ref _recommendationItemDepImageButton, Resource.Id.RecommendationItemDepImageButton);

        public TextView RecommendationItemDepTitle => GetView(ref _recommendationItemDepTitle, Resource.Id.RecommendationItemDepTitle);

        public ProgressBar RecommendationItemRecImagePlaceholder => GetView(ref _recommendationItemRecImagePlaceholder, Resource.Id.RecommendationItemRecImagePlaceholder);

        public ImageViewAsync RecommendationItemRecImage => GetView(ref _recommendationItemRecImage, Resource.Id.RecommendationItemRecImage);

        public FrameLayout RecommendationItemRecImageButton => GetView(ref _recommendationItemRecImageButton, Resource.Id.RecommendationItemRecImageButton);

        public TextView RecommendationItemRecTitle => GetView(ref _recommendationItemRecTitle, Resource.Id.RecommendationItemRecTitle);

        public TextView RecommendationItemDescription => GetView(ref _recommendationItemDescription, Resource.Id.RecommendationItemDescription);

        public TextView RecommendationItemDetailItemType1 => GetView(ref _recommendationItemDetailItemType1, Resource.Id.RecommendationItemDetailItemType1);

        public TextView RecommendationItemDetailItemDepValue1 => GetView(ref _recommendationItemDetailItemDepValue1, Resource.Id.RecommendationItemDetailItemDepValue1);

        public TextView RecommendationItemDetailItemMyDepValue1 => GetView(ref _recommendationItemDetailItemMyDepValue1, Resource.Id.RecommendationItemDetailItemMyDepValue1);

        public TextView RecommendationItemDetailItemRecValue1 => GetView(ref _recommendationItemDetailItemRecValue1, Resource.Id.RecommendationItemDetailItemRecValue1);

        public TextView RecommendationItemDetailItemMyRecValue1 => GetView(ref _recommendationItemDetailItemMyRecValue1, Resource.Id.RecommendationItemDetailItemMyRecValue1);

        public TextView RecommendationItemDetailItemType2 => GetView(ref _recommendationItemDetailItemType2, Resource.Id.RecommendationItemDetailItemType2);

        public TextView RecommendationItemDetailItemDepValue2 => GetView(ref _recommendationItemDetailItemDepValue2, Resource.Id.RecommendationItemDetailItemDepValue2);

        public TextView RecommendationItemDetailItemMyDepValue2 => GetView(ref _recommendationItemDetailItemMyDepValue2, Resource.Id.RecommendationItemDetailItemMyDepValue2);

        public TextView RecommendationItemDetailItemRecValue2 => GetView(ref _recommendationItemDetailItemRecValue2, Resource.Id.RecommendationItemDetailItemRecValue2);

        public TextView RecommendationItemDetailItemMyRecValue2 => GetView(ref _recommendationItemDetailItemMyRecValue2, Resource.Id.RecommendationItemDetailItemMyRecValue2);

        public TextView RecommendationItemDetailItemType3 => GetView(ref _recommendationItemDetailItemType3, Resource.Id.RecommendationItemDetailItemType3);

        public TextView RecommendationItemDetailItemDepValue3 => GetView(ref _recommendationItemDetailItemDepValue3, Resource.Id.RecommendationItemDetailItemDepValue3);

        public TextView RecommendationItemDetailItemMyDepValue3 => GetView(ref _recommendationItemDetailItemMyDepValue3, Resource.Id.RecommendationItemDetailItemMyDepValue3);

        public TextView RecommendationItemDetailItemRecValue3 => GetView(ref _recommendationItemDetailItemRecValue3, Resource.Id.RecommendationItemDetailItemRecValue3);

        public TextView RecommendationItemDetailItemMyRecValue3 => GetView(ref _recommendationItemDetailItemMyRecValue3, Resource.Id.RecommendationItemDetailItemMyRecValue3);

        public TextView RecommendationItemDetailItemType4 => GetView(ref _recommendationItemDetailItemType4, Resource.Id.RecommendationItemDetailItemType4);

        public TextView RecommendationItemDetailItemDepValue4 => GetView(ref _recommendationItemDetailItemDepValue4, Resource.Id.RecommendationItemDetailItemDepValue4);

        public TextView RecommendationItemDetailItemMyDepValue4 => GetView(ref _recommendationItemDetailItemMyDepValue4, Resource.Id.RecommendationItemDetailItemMyDepValue4);

        public TextView RecommendationItemDetailItemRecValue4 => GetView(ref _recommendationItemDetailItemRecValue4, Resource.Id.RecommendationItemDetailItemRecValue4);

        public TextView RecommendationItemDetailItemMyRecValue4 => GetView(ref _recommendationItemDetailItemMyRecValue4, Resource.Id.RecommendationItemDetailItemMyRecValue4);

        public TextView RecommendationItemDetailItemType5 => GetView(ref _recommendationItemDetailItemType5, Resource.Id.RecommendationItemDetailItemType5);

        public TextView RecommendationItemDetailItemDepValue5 => GetView(ref _recommendationItemDetailItemDepValue5, Resource.Id.RecommendationItemDetailItemDepValue5);

        public TextView RecommendationItemDetailItemMyDepValue5 => GetView(ref _recommendationItemDetailItemMyDepValue5, Resource.Id.RecommendationItemDetailItemMyDepValue5);

        public TextView RecommendationItemDetailItemRecValue5 => GetView(ref _recommendationItemDetailItemRecValue5, Resource.Id.RecommendationItemDetailItemRecValue5);

        public TextView RecommendationItemDetailItemMyRecValue5 => GetView(ref _recommendationItemDetailItemMyRecValue5, Resource.Id.RecommendationItemDetailItemMyRecValue5);

        public TextView RecommendationItemDetailItemType6 => GetView(ref _recommendationItemDetailItemType6, Resource.Id.RecommendationItemDetailItemType6);

        public TextView RecommendationItemDetailItemDepValue6 => GetView(ref _recommendationItemDetailItemDepValue6, Resource.Id.RecommendationItemDetailItemDepValue6);

        public TextView RecommendationItemDetailItemMyDepValue6 => GetView(ref _recommendationItemDetailItemMyDepValue6, Resource.Id.RecommendationItemDetailItemMyDepValue6);

        public TextView RecommendationItemDetailItemRecValue6 => GetView(ref _recommendationItemDetailItemRecValue6, Resource.Id.RecommendationItemDetailItemRecValue6);

        public TextView RecommendationItemDetailItemMyRecValue6 => GetView(ref _recommendationItemDetailItemMyRecValue6, Resource.Id.RecommendationItemDetailItemMyRecValue6);

        public LinearLayout RecommendationItemDetailsContainer => GetView(ref _recommendationItemDetailsContainer, Resource.Id.RecommendationItemDetailsContainer);

        public RelativeLayout RecommendationItemLoading => GetView(ref _recommendationItemLoading, Resource.Id.RecommendationItemLoading);

        #endregion
    }
}
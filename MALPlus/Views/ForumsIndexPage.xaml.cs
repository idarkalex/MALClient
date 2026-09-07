using MALClient.Models.Enums;
using MALClient.Models.Models.Forums;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Forums;
using MALClient.XShared.ViewModels.Forums.Items;
using MALClient.XShared.NavArgs;

namespace MALPlus.Views;

public partial class ForumsIndexPage : ContentPage
{
    private bool _initialized;
    private ForumIndexViewModel Vm => (ForumIndexViewModel)BindingContext;

    public ForumsIndexPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.ForumsIndex;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;
        try
        {
            Vm.Init(true);
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(200);
                if (!Vm.LoadingSideContentVisibility) break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS ForumsIndexPage Init failed: " + ex.Message);
        }
    }

    private async void OnBoardTapped(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is ForumBoardEntryViewModel item)
            {
                ((CollectionView)sender).SelectedItem = null;
                await Shell.Current.GoToAsync(
                    $"forumboard?board={(int)item.Board}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS board nav failed: " + ex.Message);
        }
    }
}

using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALPlus.Views;

public partial class FeedsPage : ContentPage
{
    private bool _initialized;
    private FriendsFeedsViewModel Vm => (FriendsFeedsViewModel)BindingContext;

    public FeedsPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.FriendsFeeds;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;
        try
        {
            Vm.Init(false);
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(200);
                if (!Vm.Loading) break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS FeedsPage Init failed: " + ex.Message);
        }
    }

    private void OnRefreshing(object sender, EventArgs e)
    {
        try { Vm.Init(true); } catch { }
    }
}

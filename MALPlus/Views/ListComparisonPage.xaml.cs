using MALClient.Models.Models;
using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;
using MALClient.XShared.Utils;

namespace MALPlus.Views;

[QueryProperty(nameof(OtherUser), "user")]
public partial class ListComparisonPage : ContentPage
{
    private bool _initialized;
    public string OtherUser { get; set; }

    private ListComparisonViewModel Vm => (ListComparisonViewModel)BindingContext;

    public ListComparisonPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.Comparison;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;
        try
        {
            if (string.IsNullOrWhiteSpace(OtherUser))
            {
                Console.WriteLine("MALPLUS ListComparison needs ?user= param");
                return;
            }
            Vm.NavigatedTo(new ListComparisonPageNavigationArgs
            {
                CompareWith = new MalUser { Name = OtherUser }
            });
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(200);
                if (!Vm.Loading) break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS ListComparisonPage Init failed: " + ex.Message);
        }
    }

    private void OnRefreshing(object sender, EventArgs e)
    {
        try { Vm.RefreshList(); } catch { }
    }
}

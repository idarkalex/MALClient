using MALClient.Models.Models.MalSpecific;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Clubs;

namespace MALPlus.Views;

public partial class ClubsIndexPage : ContentPage
{
    private int _clubsTabIndex;
    private bool _initialized;
    private ClubIndexViewModel Vm => (ClubIndexViewModel)BindingContext;

    public int ClubsTabIndex
    {
        get => _clubsTabIndex;
        set { _clubsTabIndex = value; OnPropertyChanged(); }
    }

    public ClubsIndexPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.ClubIndex;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;
        try
        {
            Vm.ReloadMyClubs();
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(200);
                if (!Vm.Loading) break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS ClubsIndexPage Init failed: " + ex.Message);
        }
    }

    private void OnTabClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Button b && b.CommandParameter is string s)
            {
                ClubsTabIndex = s == "my" ? 0 : 1;
                if (s == "my")
                {
                    MyClubsTab.BackgroundColor = Color.FromArgb("#0066FF");
                    MyClubsTab.TextColor = Colors.White;
                    SearchTab.BackgroundColor = Colors.Transparent;
                    SearchTab.TextColor = Color.FromArgb("#FFFFFF");
                }
                else
                {
                    SearchTab.BackgroundColor = Color.FromArgb("#0066FF");
                    SearchTab.TextColor = Colors.White;
                    MyClubsTab.BackgroundColor = Colors.Transparent;
                    MyClubsTab.TextColor = Color.FromArgb("#FFFFFF");
                }
            }
        }
        catch { }
    }

    private void OnRefreshMy(object sender, EventArgs e)
    {
        try { Vm.ReloadMyClubs(); } catch { }
    }

    private void OnSearchCompleted(object sender, EventArgs e)
    {
        try { Vm.SearchCommand?.Execute(null); } catch { }
    }

    private async void OnClubTapped(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is MalClubEntry entry)
            {
                ((CollectionView)sender).SelectedItem = null;
                await Shell.Current.GoToAsync($"clubdetails?id={entry.Id}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS club nav failed: " + ex.Message);
        }
    }
}

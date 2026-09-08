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
        BuildTabs();
        try
        {
            Vm.ReloadMyClubs();
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(200);
                if (!Vm.Loading) break;
            }
            HighlightTab(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS ClubsIndexPage Init failed: " + ex.Message);
        }
    }

    private void BuildTabs()
    {
        TabStrip.Children.Clear();
        var tabs = new[] { ("My Clubs", 0), ("Search", 1), ("Activity", 2) };
        for (int i = 0; i < tabs.Length; i++)
        {
            var (label, index) = tabs[i];
            var button = new Button
            {
                Text = label,
                FontFamily = "InterSemiBold",
                FontSize = 12,
                Padding = new Thickness(12, 6),
                HeightRequest = 32,
                CornerRadius = 16,
                BorderColor = Color.FromArgb("#29FFFFFF"),
                BorderWidth = 1,
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#FFFFFF")
            };
            int captured = index;
            button.Clicked += (s, e) => OnTabClicked(captured);
            TabStrip.Children.Add(button);
        }
    }

    private void HighlightTab(int activeIndex)
    {
        for (int i = 0; i < TabStrip.Children.Count; i++)
        {
            if (TabStrip.Children[i] is Button b)
            {
                b.BackgroundColor = i == activeIndex ? Color.FromArgb("#0066FF") : Colors.Transparent;
                b.TextColor = i == activeIndex ? Colors.White : Color.FromArgb("#FFFFFF");
            }
        }
    }

    private void OnTabClicked(int index)
    {
        _clubsTabIndex = index;
        OnPropertyChanged(nameof(ClubsTabIndex));
        HighlightTab(index);
    }

    private void OnRefreshMy(object sender, EventArgs e)
    {
        try { Vm.ReloadMyClubs(); } catch { }
    }

    private void OnSearchRefresh(object sender, EventArgs e)
    {
        try { Vm.SearchCommand?.Execute(null); } catch { }
    }

    private void OnActivityRefresh(object sender, EventArgs e)
    {
        try { Vm.LoadClubActivityCommand?.Execute(null); } catch { }
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

    private async void OnActivityTapped(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is ClubActivityEntry entry)
            {
                ((CollectionView)sender).SelectedItem = null;
                await Shell.Current.GoToAsync($"forumtopic?id={entry.TopicId}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS activity nav failed: " + ex.Message);
        }
    }
}

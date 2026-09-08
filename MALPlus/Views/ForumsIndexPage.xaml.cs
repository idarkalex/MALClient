using MALClient.Models.Enums;
using MALClient.Models.Models.Forums;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Forums;
using MALClient.XShared.ViewModels.Forums.Items;
using MALClient.XShared.NavArgs;
using ForumBoardEntryPeekPost = MALClient.Models.Models.Forums.ForumBoardEntryPeekPost;

namespace MALPlus.Views;

public partial class ForumsIndexPage : ContentPage
{
    private bool _initialized;
    private int _forumsTabIndex = 0;
    private ForumIndexViewModel Vm => (ForumIndexViewModel)BindingContext;

    public int ForumsTabIndex
    {
        get => _forumsTabIndex;
        set { _forumsTabIndex = value; OnPropertyChanged(); }
    }

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
        BuildTabs();
        try
        {
            Vm.Init(true);
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(200);
                if (!Vm.LoadingSideContentVisibility) break;
            }
            HighlightTab(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS ForumsIndexPage Init failed: " + ex.Message);
        }
    }

    private void BuildTabs()
    {
        TabStrip.Children.Clear();
        var tabs = new[] { ("Boards", 0), ("Recent Posts", 1) };
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
        _forumsTabIndex = index;
        OnPropertyChanged(nameof(ForumsTabIndex));
        HighlightTab(index);
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

    private async void OnRecentPostTapped(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is MALClient.Models.Models.Forums.ForumBoardEntryPeekPost post)
            {
                ((CollectionView)sender).SelectedItem = null;
                await Shell.Current.GoToAsync(
                    $"forumtopic?id={post.Id}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS recent post nav failed: " + ex.Message);
        }
    }

    private void OnRefreshing(object sender, EventArgs e)
    {
        try { Vm.Init(true); } catch { }
    }
}

using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Forums;
using MALClient.XShared.ViewModels.Forums.Items;

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
        Console.WriteLine("MALPLUS ForumsIndexPage.OnAppearing init=true calling Vm.Init");
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

    private async void OnBoardTapped2(object sender, TappedEventArgs e)
    {
        Console.WriteLine("MALPLUS OnBoardTapped2 FIRED sender=" + (sender?.GetType().Name ?? "null") +
                          " bindingContext=" + (sender is BindableObject bo ? bo.BindingContext?.GetType().Name ?? "null" : "null"));
        try
        {
            if (sender is Microsoft.Maui.Controls.Border border &&
                border.BindingContext is ForumBoardEntryViewModel item2)
            {
                Console.WriteLine("MALPLUS navigating to forumboard board=" + (int)item2.Board);
                await Shell.Current.GoToAsync(
                    $"forumboard?board={(int)item2.Board}");
                Console.WriteLine("MALPLUS board nav success");
            }
            else if (sender is Microsoft.Maui.Controls.BindableObject b &&
                     b.BindingContext is ForumBoardEntryViewModel item)
            {
                Console.WriteLine("MALPLUS navigating to forumboard board=" + (int)item.Board);
                await Shell.Current.GoToAsync(
                    $"forumboard?board={(int)item.Board}");
                Console.WriteLine("MALPLUS board nav success");
            }
            else
            {
                Console.WriteLine("MALPLUS OnBoardTapped2: sender not Border or bad bindingContext");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS board nav failed: " + ex.Message + " | " + ex.StackTrace);
        }
    }

    private async void OnBoardButtonClicked(object sender, EventArgs e)
    {
        Console.WriteLine("MALPLUS OnBoardButtonClicked FIRED sender=" + (sender?.GetType().Name ?? "null"));
        try
        {
            if (sender is Button b && b.BindingContext is ForumBoardEntryViewModel item)
            {
                Console.WriteLine("MALPLUS navigating to forumboard board=" + (int)item.Board);
                await Shell.Current.GoToAsync($"forumboard?board={(int)item.Board}");
                Console.WriteLine("MALPLUS board nav success");
            }
            else
            {
                Console.WriteLine("MALPLUS OnBoardButtonClicked: sender not Button with correct bindingContext. bc=" +
                                  (sender is BindableObject bib ? bib.BindingContext?.GetType().Name ?? "null" : "null"));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS board nav failed: " + ex.Message + " | " + ex.StackTrace);
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

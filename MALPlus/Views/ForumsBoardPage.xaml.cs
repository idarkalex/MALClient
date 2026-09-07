using MALClient.Models.Enums;
using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Forums;
using MALClient.XShared.ViewModels.Forums.Items;

namespace MALPlus.Views;

[QueryProperty(nameof(BoardId), "board")]
public partial class ForumsBoardPage : ContentPage
{
    private bool _initialized;
    public string BoardId { get; set; }

    private ForumBoardViewModel Vm => (ForumBoardViewModel)BindingContext;

    public ForumsBoardPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.ForumsBoard;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;
        try
        {
            int board = 0;
            int.TryParse(BoardId, out board);
            Vm.Init(new ForumsBoardNavigationArgs((ForumBoards)board));
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(200);
                if (!Vm.LoadingTopics) break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS ForumsBoardPage Init failed: " + ex.Message);
        }
    }

    private void OnRefreshing(object sender, EventArgs e)
    {
        try
        {
            int board = 0;
            int.TryParse(BoardId, out board);
            Vm.Init(new ForumsBoardNavigationArgs((ForumBoards)board), true);
        }
        catch { }
    }

    private async void OnTopicTapped(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is ForumTopicEntryViewModel item)
            {
                ((CollectionView)sender).SelectedItem = null;
                await Shell.Current.GoToAsync(
                    $"forumtopic?id={item.Data.Id}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS topic nav failed: " + ex.Message);
        }
    }
}

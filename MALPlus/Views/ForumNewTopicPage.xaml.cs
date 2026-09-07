using MALClient.Models.Enums;
using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Forums;

namespace MALPlus.Views;

[QueryProperty(nameof(BoardId), "board")]
public partial class ForumNewTopicPage : ContentPage
{
    private bool _initialized;
    public string BoardId { get; set; }

    private ForumNewTopicViewModel Vm => (ForumNewTopicViewModel)BindingContext;

    public ForumNewTopicPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.ForumsNewTopic;
        Vm.UpdatePreview += OnUpdatePreview;
    }

    private void OnUpdatePreview(object sender, string html)
    {
        MainThread.BeginInvokeOnMainThread(() => PreviewLabel.Text = html);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;
        try
        {
            int board = 0;
            int.TryParse(BoardId, out board);
            Vm.Init(new ForumsNewTopicNavigationArgs((ForumBoards)board));
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS ForumNewTopicPage Init failed: " + ex.Message);
        }
    }
}

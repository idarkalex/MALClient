using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Forums;

namespace MALPlus.Views;

[QueryProperty(nameof(TopicId), "id")]
public partial class ForumsTopicPage : ContentPage
{
    private bool _initialized;
    public string TopicId { get; set; }

    private ForumTopicViewModel Vm => (ForumTopicViewModel)BindingContext;

    public ForumsTopicPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.ForumsTopic;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;
        try
        {
            int page = 1;
            int postId = -1;
            Vm.Init(new ForumsTopicNavigationArgs(TopicId, postId, page));
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(200);
                if (!Vm.LoadingTopic) break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS ForumsTopicPage Init failed: " + ex.Message);
        }
    }

    private void OnRefreshing(object sender, EventArgs e)
    {
        try
        {
            Vm.Init(new ForumsTopicNavigationArgs(TopicId, -1, 1));
        }
        catch { }
    }
}

using MALClient.Models.Models.MalSpecific;
using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALPlus.Views;

[QueryProperty(nameof(ThreadId), "id")]
[QueryProperty(nameof(Subject), "subject")]
[QueryProperty(nameof(ToUser), "to")]
public partial class MessageDetailsPage : ContentPage
{
    private bool _initialized;
    public string ThreadId { get; set; }
    public string Subject { get; set; }
    public string ToUser { get; set; }

    private MalMessageDetailsViewModel Vm => (MalMessageDetailsViewModel)BindingContext;

    public MessageDetailsPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.MalMessageDetails;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;
        try
        {
            var subject = Uri.UnescapeDataString(Subject ?? "");
            if (string.IsNullOrEmpty(ThreadId) || ThreadId == "0")
            {
                Vm.NewMessageFieldsVisibility = true;
                Vm.MessageSubject = subject;
                if (!string.IsNullOrEmpty(ToUser))
                    Vm.MessageTarget = Uri.UnescapeDataString(ToUser);
                Vm.Init(new MalMessageDetailsNavArgs
                {
                    WorkMode = MessageDetailsWorkMode.Message,
                    Arg = (MalMessageModel)null,
                    NewMessageTarget = Vm.MessageTarget
                });
            }
            else
            {
                var msg = new MalMessageModel
                {
                    Id = ThreadId,
                    ThreadId = ThreadId,
                    Subject = subject
                };
                Vm.MessageSubject = subject;
                Vm.Init(new MalMessageDetailsNavArgs
                {
                    WorkMode = MessageDetailsWorkMode.Message,
                    Arg = msg
                });
                for (int i = 0; i < 60; i++)
                {
                    await Task.Delay(200);
                    if (!Vm.LoadingVisibility) break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS MessageDetailsPage Init failed: " + ex.Message);
        }
    }

    private void OnRefreshing(object sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(ThreadId) || ThreadId == "0") return;
            var msg = new MalMessageModel { Id = ThreadId, ThreadId = ThreadId, Subject = Uri.UnescapeDataString(Subject ?? "") };
            Vm.Init(new MalMessageDetailsNavArgs
            {
                WorkMode = MessageDetailsWorkMode.Message,
                Arg = msg
            }, true);
        }
        catch { }
    }
}

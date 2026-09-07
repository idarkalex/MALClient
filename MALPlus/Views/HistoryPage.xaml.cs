using System.Collections.ObjectModel;
using MALClient.Models.Models.MalSpecific;
using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;
using MALClient.XShared.Utils;
using AnimeItemViewModel = MALClient.XShared.ViewModels.AnimeItemViewModel;

namespace MALPlus.Views;

[QueryProperty(nameof(SourceUser), "user")]
public partial class HistoryPage : ContentPage
{
    private bool _initialized;
    public string SourceUser { get; set; }

    private HistoryViewModel Vm => (HistoryViewModel)BindingContext;

    public ObservableCollection<object> FlatHistory { get; } = new();

    public HistoryPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.History;
        HistoryList.ItemsSource = FlatHistory;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;
        try
        {
            Vm.Init(new HistoryNavigationArgs { Source = SourceUser ?? Credentials.UserName });
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(200);
                if (!Vm.LoadingVisibility) break;
            }
            // Flatten the dictionary into a list of (date, (item, entries)) pairs
            FlatHistory.Clear();
            if (Vm.History != null)
            {
                foreach (var kv in Vm.History)
                    foreach (var tuple in kv.Value)
                        FlatHistory.Add(new KeyValuePair<string,
                            System.Tuple<AnimeItemViewModel,
                                         System.Collections.Generic.List<MalProfileHistoryEntry>>>(
                            kv.Key, tuple));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS HistoryPage Init failed: " + ex.Message);
        }
    }

    private void OnRefreshing(object sender, EventArgs e)
    {
        try
        {
            Vm.Init(new HistoryNavigationArgs { Source = SourceUser ?? Credentials.UserName }, true);
        }
        catch { }
    }
}

using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Main;

namespace MALPlus.Views;

public partial class CalendarPage : ContentPage
{
    private bool _initialized;

    private CalendarPageViewModel Vm => (CalendarPageViewModel)BindingContext;

    public CalendarPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.CalendarPage;
    }

    private bool _subscribed;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized)
            return;
        _initialized = true;
        System.Diagnostics.Debug.WriteLine("MALPLUS CalendarPage OnAppearing");
        BuildTabStrip();
        try
        {
            System.Diagnostics.Debug.WriteLine("MALPLUS CalendarPage calling Vm.Init");
            await Vm.Init(false);
            System.Diagnostics.Debug.WriteLine("MALPLUS CalendarPage Vm.Init completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("MALPLUS CalendarPage Init failed: " + ex);
        }
    }

    private void BuildTabStrip()
    {
        if (Vm == null) return;
        if (!_subscribed)
        {
            Vm.PropertyChanged += OnVmPropertyChanged;
            _subscribed = true;
        }
        // If data is already there (back-nav re-entry), redraw immediately
        if (Vm.CalendarData != null && Vm.CalendarData.Count > 0)
        {
            OnVmPropertyChanged(Vm, new System.ComponentModel.PropertyChangedEventArgs(nameof(CalendarPageViewModel.CalendarData)));
        }
    }

    private void OnVmPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CalendarPageViewModel.CalendarData)) return;
        if (Vm?.CalendarData == null || Vm.CalendarData.Count == 0) return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            TabStrip.Children.Clear();
            for (int i = 0; i < Vm.CalendarData.Count; i++)
            {
                var page = Vm.CalendarData[i];
                var index = i;
                var label = new Label
                {
                    Text = page.Header,
                    FontFamily = "InterSemiBold",
                    FontSize = 13,
                    TextColor = Color.FromArgb("#FFFFFF"),
                    VerticalOptions = LayoutOptions.Center,
                    Padding = new Thickness(0, 6)
                };

                var border = new Border
                {
                    Content = label,
                    Padding = new Thickness(16, 6),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                    BackgroundColor = Colors.Transparent
                };

                var tap = new TapGestureRecognizer();
                tap.Tapped += (s, e) => Vm.CalendarPivotIndex = index;
                border.GestureRecognizers.Add(tap);

                TabStrip.Children.Add(border);
            }
        });
    }

    private async void OnItemTapped(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is MALClient.XShared.ViewModels.AnimeItemViewModel item)
            {
                ((CollectionView)sender).SelectedItem = null;
                await Shell.Current.GoToAsync(
                    $"animedetails?id={item.Id}&title={Uri.EscapeDataString(item.Title ?? string.Empty)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS calendar item nav failed: " + ex.Message);
        }
    }
}
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
        if (Vm?.CalendarData == null)
            return;

        // Build tab strip after data is loaded - we'll do this in a callback
        Vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CalendarPageViewModel.CalendarData) && Vm.CalendarData != null)
        {
            Vm.PropertyChanged -= OnVmPropertyChanged;
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
                        TextColor = (Color)Application.Current.Resources["BrushText"],
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
    }
}
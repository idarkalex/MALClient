using MALClient.XShared.ViewModels;

namespace MALPlus.Views;

public partial class PlaceholderPage : ContentPage
{
    public PlaceholderPage()
    {
        InitializeComponent();
        CoreLabel.Text = "Core: " + typeof(ViewModelLocator).Assembly.GetName().Version;
    }
}

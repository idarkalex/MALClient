using MALClient.XShared.NavArgs;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Items;
using MALClient.XShared.ViewModels.Main;

namespace MALPlus.Views;

[QueryProperty(nameof(SourceUrl), "url")]
[QueryProperty(nameof(SourceTitle), "title")]
public partial class WallpapersPage : ContentPage
{
    private bool _initialized;
    private bool _overlayVisible;
    public string SourceUrl { get; set; }
    public string SourceTitle { get; set; }

    private WallpapersViewModel Vm => (WallpapersViewModel)BindingContext;

    public bool WallpaperOverlayVisibility
    {
        get => _overlayVisible;
        set { _overlayVisible = value; OnPropertyChanged(); }
    }

    public WallpapersPage()
    {
        InitializeComponent();
        BindingContext = ViewModelLocator.Wallpapers;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Console.WriteLine("MALPLUS WallpapersPage OnAppearing");
        if (_initialized) return;
        _initialized = true;
        try
        {
            Console.WriteLine("MALPLUS WallpapersPage calling Vm.Init");
            Vm.Init(new WallpaperPageNavigationArgs { Query = SourceUrl ?? SourceTitle });
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(200);
                if (!Vm.LoadingWallpapersVisibility) break;
            }
            Console.WriteLine("MALPLUS WallpapersPage Init completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS WallpapersPage Init failed: " + ex);
        }
    }

    private void OnRefreshing(object sender, EventArgs e)
    {
        try
        {
            Vm.Init(new WallpaperPageNavigationArgs { Query = SourceUrl ?? SourceTitle });
        }
        catch { }
    }

    private void OnWallpaperTapped(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is WallpaperItemViewModel item)
            {
                ((CollectionView)sender).SelectedItem = null;
                FullImage.Source = new UriImageSource { Uri = new System.Uri(item.Data.FileUrl) };
                WallpaperOverlayVisibility = true;
                WallpaperOverlay.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MALPLUS wallpaper tap failed: " + ex.Message);
        }
    }

    private void OnClose(object sender, EventArgs e)
    {
        try
        {
            FullImage.Source = null;
            WallpaperOverlay.IsVisible = false;
            WallpaperOverlayVisibility = false;
        }
        catch { }
    }
}

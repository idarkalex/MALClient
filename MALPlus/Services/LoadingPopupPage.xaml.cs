namespace MALPlus.Services;

public partial class LoadingPopupPage : ContentPage
{
    public LoadingPopupPage(string title, string content)
    {
        InitializeComponent();
        Update(title, content);
    }

    public void Update(string title, string content)
    {
        TitleLabel.Text = title ?? string.Empty;
        ContentLabel.Text = content ?? string.Empty;
    }
}

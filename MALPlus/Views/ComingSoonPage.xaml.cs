using System.Text;

namespace MALPlus.Views;

[QueryProperty(nameof(FeatureName), "name")]
public partial class ComingSoonPage : ContentPage
{
    public string FeatureName { get; set; }

    public ComingSoonPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var title = Humanize(FeatureName);
        TitleLabel.Text = title;
        SubtitleLabel.Text = $"\"{title}\" will arrive in a later phase of the MAUI migration.";
    }

    private static string Humanize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Coming soon";
        var sb = new StringBuilder();
        for (int i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (i == 0) sb.Append(char.ToUpperInvariant(c));
            else if (c == '_' || c == '-') sb.Append(' ');
            else sb.Append(c);
        }
        return sb.ToString();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("..");
        }
        catch { }
    }
}

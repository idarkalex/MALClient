using MALPlus.Services;

namespace MALPlus;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("inter_regular.ttf", "Inter");
                fonts.AddFont("inter_medium.ttf", "InterMedium");
                fonts.AddFont("inter_semibold.ttf", "InterSemiBold");
                fonts.AddFont("inter_bold.ttf", "InterBold");
                fonts.AddFont("fontawesomewebfont.ttf", "FontAwesome");
            });

        MauiViewModelLocator.RegisterDependencies();

        return builder.Build();
    }
}

using System.Collections;
using System.Windows.Input;

namespace MALPlus.Controls;

public partial class AnimePosterCard : ContentView
{
    public static readonly BindableProperty PosterUrlProperty =
        BindableProperty.Create(nameof(PosterUrl), typeof(string), typeof(AnimePosterCard), default(string));

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(AnimePosterCard), default(string));

    public static readonly BindableProperty TypeTagProperty =
        BindableProperty.Create(nameof(TypeTag), typeof(string), typeof(AnimePosterCard), default(string));

    public static readonly BindableProperty BadgeVisibleProperty =
        BindableProperty.Create(nameof(BadgeVisible), typeof(bool), typeof(AnimePosterCard), false);

    public static readonly BindableProperty BadgeAccentProperty =
        BindableProperty.Create(nameof(BadgeAccent), typeof(string), typeof(AnimePosterCard), default(string));

    public static readonly BindableProperty BadgeMainProperty =
        BindableProperty.Create(nameof(BadgeMain), typeof(string), typeof(AnimePosterCard), default(string));

    public static readonly BindableProperty BadgeExtraProperty =
        BindableProperty.Create(nameof(BadgeExtra), typeof(string), typeof(AnimePosterCard), default(string));

    public static readonly BindableProperty TitleOverlayProperty =
        BindableProperty.Create(nameof(TitleOverlay), typeof(bool), typeof(AnimePosterCard), true);

    public static readonly BindableProperty ChipSourceProperty =
        BindableProperty.Create(nameof(ChipSource), typeof(IEnumerable), typeof(AnimePosterCard), default(IEnumerable));

    public static readonly BindableProperty ChipTemplateProperty =
        BindableProperty.Create(nameof(ChipTemplate), typeof(DataTemplate), typeof(AnimePosterCard),
            default(DataTemplate));

    public AnimePosterCard()
    {
        InitializeComponent();
    }

    public string PosterUrl
    {
        get => (string)GetValue(PosterUrlProperty);
        set => SetValue(PosterUrlProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string TypeTag
    {
        get => (string)GetValue(TypeTagProperty);
        set => SetValue(TypeTagProperty, value);
    }

    public bool BadgeVisible
    {
        get => (bool)GetValue(BadgeVisibleProperty);
        set => SetValue(BadgeVisibleProperty, value);
    }

    public string BadgeAccent
    {
        get => (string)GetValue(BadgeAccentProperty);
        set => SetValue(BadgeAccentProperty, value);
    }

    public string BadgeMain
    {
        get => (string)GetValue(BadgeMainProperty);
        set => SetValue(BadgeMainProperty, value);
    }

    public string BadgeExtra
    {
        get => (string)GetValue(BadgeExtraProperty);
        set => SetValue(BadgeExtraProperty, value);
    }

    public bool TitleOverlay
    {
        get => (bool)GetValue(TitleOverlayProperty);
        set => SetValue(TitleOverlayProperty, value);
    }

    public IEnumerable ChipSource
    {
        get => (IEnumerable)GetValue(ChipSourceProperty);
        set => SetValue(ChipSourceProperty, value);
    }

    public DataTemplate ChipTemplate
    {
        get => (DataTemplate)GetValue(ChipTemplateProperty);
        set => SetValue(ChipTemplateProperty, value);
    }
}

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

    /// <summary>
    /// Mean score, drawn as a second blue tag under the type tag. Tops and search results used
    /// to spell it out in the counter pill as "Score 8.72", which put a word where a number
    /// belongs and left the type tag alone at the top.
    /// </summary>
    public static readonly BindableProperty ScoreTagProperty =
        BindableProperty.Create(nameof(ScoreTag), typeof(string), typeof(AnimePosterCard), default(string));

    public static readonly BindableProperty BadgeVisibleProperty =
        BindableProperty.Create(nameof(BadgeVisible), typeof(bool), typeof(AnimePosterCard), false);

    public static readonly BindableProperty BadgeAccentProperty =
        BindableProperty.Create(nameof(BadgeAccent), typeof(string), typeof(AnimePosterCard), default(string),
            propertyChanged: OnBadgeChanged);

    public static readonly BindableProperty BadgeMainProperty =
        BindableProperty.Create(nameof(BadgeMain), typeof(string), typeof(AnimePosterCard), default(string),
            propertyChanged: OnBadgeChanged);

    public static readonly BindableProperty BadgeExtraProperty =
        BindableProperty.Create(nameof(BadgeExtra), typeof(string), typeof(AnimePosterCard), default(string),
            propertyChanged: OnBadgeChanged);

    /// <summary>
    /// True when at least one badge carries text. The accent/main pill had no visibility of
    /// its own, so a card with no badges still drew an empty dark blob in the corner.
    /// </summary>
    public static readonly BindableProperty HasBadgeProperty =
        BindableProperty.Create(nameof(HasBadge), typeof(bool), typeof(AnimePosterCard), false);

    public bool HasBadge
    {
        get => (bool)GetValue(HasBadgeProperty);
        private set => SetValue(HasBadgeProperty, value);
    }

    /// <summary>
    ///     True when the countdown is worth a separator inside the single merged counter pill.
    /// </summary>
    public static readonly BindableProperty HasBadgeExtraProperty =
        BindableProperty.Create(nameof(HasBadgeExtra), typeof(bool), typeof(AnimePosterCard), false);

    public bool HasBadgeExtra
    {
        get => (bool)GetValue(HasBadgeExtraProperty);
        private set => SetValue(HasBadgeExtraProperty, value);
    }

    private static void OnBadgeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var card = (AnimePosterCard)bindable;
        card.HasBadge = !string.IsNullOrWhiteSpace(card.BadgeAccent)
                        || !string.IsNullOrWhiteSpace(card.BadgeMain)
                        || !string.IsNullOrWhiteSpace(card.BadgeExtra);
        // The dot only earns its place when there is something on both sides of it.
        card.HasBadgeExtra = !string.IsNullOrWhiteSpace(card.BadgeExtra)
                             && (!string.IsNullOrWhiteSpace(card.BadgeAccent)
                                 || !string.IsNullOrWhiteSpace(card.BadgeMain));
    }

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

    public string ScoreTag
    {
        get => (string)GetValue(ScoreTagProperty);
        set => SetValue(ScoreTagProperty, value);
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

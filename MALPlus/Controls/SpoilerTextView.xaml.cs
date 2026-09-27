using System.Text;
using Microsoft.Maui.Controls.Shapes;
using System.Text.RegularExpressions;

namespace MALPlus.Controls;

/// <summary>
/// Renders text that may carry MAL's [Spoiler]...[/Spoiler] markup. The text is split in place so
/// each spoiler keeps its own toggle right after the words that introduce it, and every spoiler
/// starts collapsed. The markers themselves never reach the screen.
/// </summary>
public partial class SpoilerTextView : ContentView
{
    private static readonly Regex SpoilerPattern =
        new(@"\[Spoiler\](?<body>.*?)\[/Spoiler\]", RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private sealed class Segment
    {
        public string Text;
        public bool IsSpoiler;
        public bool Expanded;
    }

    private readonly List<Segment> _segments = new();

    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(SpoilerTextView), default(string),
            propertyChanged: (bindable, _, value) => ((SpoilerTextView)bindable).Render(value as string));

    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(SpoilerTextView), Colors.White,
            propertyChanged: (bindable, _, _) => ((SpoilerTextView)bindable).Render(((SpoilerTextView)bindable).Text));

    public static readonly BindableProperty TextFontProperty =
        BindableProperty.Create(nameof(TextFont), typeof(string), typeof(SpoilerTextView), "Inter",
            propertyChanged: (bindable, _, _) => ((SpoilerTextView)bindable).Render(((SpoilerTextView)bindable).Text));

    public static readonly BindableProperty TextSizeProperty =
        BindableProperty.Create(nameof(TextSize), typeof(double), typeof(SpoilerTextView), 13d,
            propertyChanged: (bindable, _, _) => ((SpoilerTextView)bindable).Render(((SpoilerTextView)bindable).Text));

    public static readonly BindableProperty LineHeightProperty =
        BindableProperty.Create(nameof(LineHeight), typeof(double), typeof(SpoilerTextView), 1d,
            propertyChanged: (bindable, _, _) => ((SpoilerTextView)bindable).Render(((SpoilerTextView)bindable).Text));

    public SpoilerTextView()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public string TextFont
    {
        get => (string)GetValue(TextFontProperty);
        set => SetValue(TextFontProperty, value);
    }

    public double TextSize
    {
        get => (double)GetValue(TextSizeProperty);
        set => SetValue(TextSizeProperty, value);
    }

    public double LineHeight
    {
        get => (double)GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    /// <summary>
    /// True when the value carries spoiler markup, so callers can skip the control entirely on
    /// the overwhelming majority of rows that have none.
    /// </summary>
    public static bool ContainsSpoiler(string value)
    {
        return !string.IsNullOrEmpty(value) && SpoilerPattern.IsMatch(value);
    }

    private void Render(string value)
    {
        _segments.Clear();

        if (!string.IsNullOrWhiteSpace(value))
        {
            var position = 0;
            foreach (Match match in SpoilerPattern.Matches(value))
            {
                AddPlain(value.Substring(position, match.Index - position));
                var body = match.Groups["body"].Value.Trim();
                if (!string.IsNullOrEmpty(body))
                    _segments.Add(new Segment {Text = body, IsSpoiler = true});

                position = match.Index + match.Length;
            }

            AddPlain(value.Substring(position));
        }

        Build();
    }

    private void AddPlain(string chunk)
    {
        if (string.IsNullOrWhiteSpace(chunk))
            return;
        _segments.Add(new Segment {Text = chunk, IsSpoiler = false});
    }

    private void Build()
    {
        SegmentsHost.Children.Clear();
        foreach (var segment in _segments)
            SegmentsHost.Children.Add(CreateView(segment));
    }

    private View CreateView(Segment segment)
    {
        if (!segment.IsSpoiler)
        {
            var label = new Label
            {
                Text = segment.Text,
                TextColor = TextColor,
                FontFamily = TextFont,
                FontSize = TextSize,
                LineHeight = LineHeight
            };
            return label;
        }

        var host = new VerticalStackLayout {Spacing = 4};

        var toggle = new Button
        {
            Text = segment.Expanded ? "Hide spoiler" : "Show spoiler",
            FontFamily = "InterSemiBold",
            FontSize = 12,
            TextColor = Color.FromArgb("#80B4FF"),
            BackgroundColor = Color.FromArgb("#1A0066FF"),
            BorderWidth = 0,
            CornerRadius = 8,
            Padding = new Thickness(10, 4),
            HeightRequest = 30,
            HorizontalOptions = LayoutOptions.Start
        };
        toggle.Clicked += (_, _) => Toggle(segment);

        host.Children.Add(toggle);

        if (segment.Expanded)
        {
            var panel = new Border
            {
                Padding = new Thickness(10),
                BackgroundColor = Color.FromArgb("#14FF8A3D"),
                Stroke = Color.FromArgb("#33FF8A3D"),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle {CornerRadius = new CornerRadius(8)}
            };
            panel.Content = new Label
            {
                Text = segment.Text,
                TextColor = TextColor,
                FontFamily = TextFont,
                FontSize = TextSize,
                LineHeight = LineHeight
            };
            host.Children.Add(panel);
        }

        return host;
    }

    private void Toggle(Segment segment)
    {
        if (segment == null || !segment.IsSpoiler)
            return;
        segment.Expanded = !segment.Expanded;
        Build();
    }
}

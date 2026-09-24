using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace MALPlus.Controls;

public class OutlinedLabel : SKCanvasView
{
    private static readonly Dictionary<string, SKTypeface> TypefaceCache = new();

    public OutlinedLabel()
    {
        SizeChanged += (s, e) => InvalidateSurface();
    }
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(OutlinedLabel), string.Empty,
            propertyChanged: (b, o, n) => ((OutlinedLabel)b).InvalidateSurface());

    public static readonly BindableProperty FontFamilyProperty =
        BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(OutlinedLabel), "Inter",
            propertyChanged: (b, o, n) => ((OutlinedLabel)b).InvalidateSurface());

    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(nameof(FontSize), typeof(double), typeof(OutlinedLabel), 12.0,
            propertyChanged: (b, o, n) => ((OutlinedLabel)b).InvalidateSurface());

    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(OutlinedLabel), Colors.White,
            propertyChanged: (b, o, n) => ((OutlinedLabel)b).InvalidateSurface());

    public static readonly BindableProperty OutlineColorProperty =
        BindableProperty.Create(nameof(OutlineColor), typeof(Color), typeof(OutlinedLabel), Colors.Black,
            propertyChanged: (b, o, n) => ((OutlinedLabel)b).InvalidateSurface());

    public static readonly BindableProperty OutlineWidthProperty =
        BindableProperty.Create(nameof(OutlineWidth), typeof(double), typeof(OutlinedLabel), 3.0,
            propertyChanged: (b, o, n) => ((OutlinedLabel)b).InvalidateSurface());

    public static readonly BindableProperty MaxLinesProperty =
        BindableProperty.Create(nameof(MaxLines), typeof(int), typeof(OutlinedLabel), 2,
            propertyChanged: (b, o, n) => ((OutlinedLabel)b).InvalidateSurface());

    public static readonly BindableProperty HorizontalTextAlignmentProperty =
        BindableProperty.Create(nameof(HorizontalTextAlignment), typeof(TextAlignment), typeof(OutlinedLabel), TextAlignment.Start,
            propertyChanged: (b, o, n) => ((OutlinedLabel)b).InvalidateSurface());

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string FontFamily
    {
        get => (string)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public Color OutlineColor
    {
        get => (Color)GetValue(OutlineColorProperty);
        set => SetValue(OutlineColorProperty, value);
    }

    public double OutlineWidth
    {
        get => (double)GetValue(OutlineWidthProperty);
        set => SetValue(OutlineWidthProperty, value);
    }

    public int MaxLines
    {
        get => (int)GetValue(MaxLinesProperty);
        set => SetValue(MaxLinesProperty, value);
    }

    public TextAlignment HorizontalTextAlignment
    {
        get => (TextAlignment)GetValue(HorizontalTextAlignmentProperty);
        set => SetValue(HorizontalTextAlignmentProperty, value);
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var text = (Text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        if (text.Length == 0)
            return;
        var width = (float)Width;
        var height = (float)Height;
        if (width <= 0 || height <= 0 || e.Info.Width <= 0 || e.Info.Height <= 0)
            return;
        var scale = e.Info.Width / width;
        if (!float.IsFinite(scale) || scale <= 0)
            return;

        var typeface = GetTypeface(FontFamily);
        using var fill = new SKPaint
            {
                Typeface = typeface,
                TextSize = (float)(FontSize * scale),
                Color = TextColor.ToSKColor(),
                IsAntialias = true,
                TextAlign = HorizontalTextAlignment switch
                {
                    TextAlignment.Center => SKTextAlign.Center,
                    TextAlignment.End => SKTextAlign.Right,
                    _ => SKTextAlign.Left,
                },
            };
            using var stroke = fill.Clone();
            stroke.Style = SKPaintStyle.Stroke;
            stroke.Color = OutlineColor.ToSKColor();
            stroke.StrokeWidth = (float)(OutlineWidth * scale);
            stroke.StrokeJoin = SKStrokeJoin.Round;

            var maxLines = Math.Max(1, MaxLines);
            var lines = WrapText(fill, text, e.Info.Width, maxLines);
            if (lines.Count == 0)
                return;
            var metrics = fill.FontMetrics;
            var lineHeight = metrics.Descent - metrics.Ascent + metrics.Leading;
            if (lineHeight <= 0)
                lineHeight = fill.TextSize * 1.25f;
            var x = HorizontalTextAlignment switch
            {
                TextAlignment.Center => e.Info.Width / 2f,
                TextAlignment.End => e.Info.Width,
                _ => 0f,
            };
            for (var i = 0; i < lines.Count; i++)
            {
                var y = e.Info.Height - metrics.Descent - ((lines.Count - 1 - i) * lineHeight);
                canvas.DrawText(lines[i], x, y, stroke);
                canvas.DrawText(lines[i], x, y, fill);
            }
    }

    private static SKTypeface GetTypeface(string family)
    {
        var key = string.IsNullOrWhiteSpace(family) ? "default" : family;
        lock (TypefaceCache)
        {
            if (TypefaceCache.TryGetValue(key, out var cached))
                return cached;
            var created = key == "default"
                ? SKTypeface.Default
                : SKTypeface.FromFamilyName(key) ?? SKTypeface.Default;
            TypefaceCache[key] = created;
            return created;
        }
    }

    private static List<string> WrapText(SKPaint paint, string text, float maxWidth, int maxLines)
    {
        var lines = new List<string>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return lines;
        var current = string.Empty;
        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (paint.MeasureText(candidate) <= maxWidth || current.Length == 0)
            {
                current = candidate.Length <= 0 ? word : candidate;
                if (paint.MeasureText(current) > maxWidth)
                    current = TruncateToWidth(paint, current, maxWidth);
                continue;
            }
            lines.Add(current);
            if (lines.Count == maxLines)
                return lines;
            current = TruncateToWidth(paint, word, maxWidth);
        }
        if (current.Length > 0)
            lines.Add(current);
        if (lines.Count > maxLines)
            lines.RemoveRange(maxLines, lines.Count - maxLines);
        if (lines.Count == maxLines && words.Length > 0)
        {
            var joined = string.Join(" ", words);
            if (joined.Length > string.Join(" ", lines).Length)
                lines[maxLines - 1] = Ellipsize(paint, lines[maxLines - 1], maxWidth);
        }
        return lines;
    }

    private static string TruncateToWidth(SKPaint paint, string value, float maxWidth)
    {
        if (paint.MeasureText(value) <= maxWidth)
            return value;
        var result = value;
        while (result.Length > 1 && paint.MeasureText(result) > maxWidth)
            result = result.Substring(0, result.Length - 1);
        return result;
    }

    private static string Ellipsize(SKPaint paint, string value, float maxWidth)
    {
        const string ellipsis = "…";
        var result = value.TrimEnd();
        while (result.Length > 0 && paint.MeasureText(result + ellipsis) > maxWidth)
            result = result.Substring(0, result.Length - 1).TrimEnd();
        return result + ellipsis;
    }
}

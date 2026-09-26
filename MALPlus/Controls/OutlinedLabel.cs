using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace MALPlus.Controls;

public class OutlinedLabel : SKCanvasView
{
    private static readonly Dictionary<string, SKTypeface> TypefaceCache = new();

    // One SKCanvasView is a TextureView, and the grid keeps ~30 of these alive. Binding
    // a cell sets 6 properties back to back, so OnPaintSurface used to redo the
    // MeasureText-heavy wrap and allocate 3 SKPaints on every one of those repaints.
    // The paints and the wrapped lines are now cached; invalidation itself is unchanged
    // (posting through Dispatcher instead made it strictly worse: 4x the frames).
    private bool _paintsDirty = true;
    private float _paintScale = -1f;
    private SKPaint _fill;
    private SKPaint _stroke;
    private SKPaint _shadow;

    private bool _wrapValid;
    private string _wrapText;
    private float _wrapWidth = -1f;
    private int _wrapMaxLines = -1;
    private string _wrapFamily;
    private double _wrapFontSize = -1d;
    private List<string> _wrapLines;

    public OutlinedLabel()
    {
        SizeChanged += (s, e) => RequestRepaint();
    }

    private void RequestRepaint()
    {
        _wrapValid = false;
        _paintsDirty = true;
        InvalidateSurface();
    }

    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(OutlinedLabel), string.Empty,
            propertyChanged: (b, o, n) => ((OutlinedLabel)b).RequestRepaint());

    public static readonly BindableProperty FontFamilyProperty =
        BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(OutlinedLabel), "Inter",
            propertyChanged: (b, o, n) => ((OutlinedLabel)b).RequestRepaint());

    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(nameof(FontSize), typeof(double), typeof(OutlinedLabel), 12.0,
            propertyChanged: (b, o, n) => ((OutlinedLabel)b).RequestRepaint());

    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(OutlinedLabel), Colors.White,
            propertyChanged: (b, o, n) => ((OutlinedLabel)b).RequestRepaint());

    public static readonly BindableProperty OutlineColorProperty =
        BindableProperty.Create(nameof(OutlineColor), typeof(Color), typeof(OutlinedLabel), Colors.Black,
            propertyChanged: (b, o, n) => ((OutlinedLabel)b).RequestRepaint());

    public static readonly BindableProperty OutlineWidthProperty =
        BindableProperty.Create(nameof(OutlineWidth), typeof(double), typeof(OutlinedLabel), 3.0,
            propertyChanged: (b, o, n) => ((OutlinedLabel)b).RequestRepaint());

    public static readonly BindableProperty MaxLinesProperty =
        BindableProperty.Create(nameof(MaxLines), typeof(int), typeof(OutlinedLabel), 2,
            propertyChanged: (b, o, n) => ((OutlinedLabel)b).RequestRepaint());

    public static readonly BindableProperty HorizontalTextAlignmentProperty =
        BindableProperty.Create(nameof(HorizontalTextAlignment), typeof(TextAlignment), typeof(OutlinedLabel), TextAlignment.Start,
            propertyChanged: (b, o, n) => ((OutlinedLabel)b).RequestRepaint());

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

        var fill = EnsurePaints(scale);

        var maxLines = Math.Max(1, MaxLines);
        var lines = GetWrappedLines(fill, text, e.Info.Width, maxLines);
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
            canvas.DrawText(lines[i], x + 1.5f * scale, y + 1.5f * scale, _shadow);
            canvas.DrawText(lines[i], x, y, _stroke);
            canvas.DrawText(lines[i], x, y, fill);
        }
    }

    private SKPaint EnsurePaints(float scale)
    {
        if (!_paintsDirty && Math.Abs(_paintScale - scale) < 0.001f)
            return _fill;

        var typeface = GetTypeface(FontFamily);
        _fill?.Dispose();
        _stroke?.Dispose();
        _shadow?.Dispose();

        _fill = new SKPaint
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
        _stroke = _fill.Clone();
        _stroke.Style = SKPaintStyle.Stroke;
        _stroke.Color = OutlineColor.ToSKColor();
        _stroke.StrokeWidth = (float)(OutlineWidth * scale);
        _stroke.StrokeJoin = SKStrokeJoin.Round;
        _shadow = _fill.Clone();
        _shadow.Color = new SKColor(0, 0, 0, 190);

        _paintScale = scale;
        _paintsDirty = false;
        return _fill;
    }

    private List<string> GetWrappedLines(SKPaint paint, string text, float maxWidth, int maxLines)
    {
        if (_wrapValid &&
            _wrapWidth == maxWidth &&
            _wrapMaxLines == maxLines &&
            _wrapFontSize == FontSize &&
            string.Equals(_wrapFamily, FontFamily, StringComparison.Ordinal) &&
            string.Equals(_wrapText, text, StringComparison.Ordinal))
            return _wrapLines;

        _wrapLines = WrapText(paint, text, maxWidth, maxLines);
        _wrapText = text;
        _wrapWidth = maxWidth;
        _wrapMaxLines = maxLines;
        _wrapFontSize = FontSize;
        _wrapFamily = FontFamily;
        _wrapValid = true;
        return _wrapLines;
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

    // Was a char-at-a-time Substring loop: 60 iterations, each allocating a new string
    // AND calling native MeasureText. MeasureText is monotonic in length, so a binary
    // search lands on the same string with ~6 probes.
    private static string TruncateToWidth(SKPaint paint, string value, float maxWidth)
    {
        if (paint.MeasureText(value) <= maxWidth)
            return value;
        var lo = 1;
        var hi = value.Length - 1;
        var best = value.Substring(0, 1);
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (paint.MeasureText(value.Substring(0, mid)) <= maxWidth)
            {
                best = value.Substring(0, mid);
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return best;
    }

    private static string Ellipsize(SKPaint paint, string value, float maxWidth)
    {
        const string ellipsis = "…";
        var trimmed = value.TrimEnd();
        if (paint.MeasureText(trimmed + ellipsis) <= maxWidth)
            return trimmed + ellipsis;
        var lo = 0;
        var hi = trimmed.Length;
        var best = string.Empty;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            var candidate = trimmed.Substring(0, mid).TrimEnd();
            if (paint.MeasureText(candidate + ellipsis) <= maxWidth)
            {
                best = candidate;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return best + ellipsis;
    }
}

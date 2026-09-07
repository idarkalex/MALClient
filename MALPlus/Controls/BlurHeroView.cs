using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace MALPlus.Controls;

// Blurred hero background (v2 BlurredTransformation(25) parity).
// Downloads the poster once, bakes a blurred snapshot off-thread,
// then paints it cover-fit. Never throws: failures keep a dark placeholder.
public class BlurHeroView : SKCanvasView
{
    public static readonly BindableProperty ImageUrlProperty =
        BindableProperty.Create(nameof(ImageUrl), typeof(string), typeof(BlurHeroView), null,
            propertyChanged: (b, o, n) => ((BlurHeroView)b).OnUrlChanged((string)n));

    public string ImageUrl
    {
        get => (string)GetValue(ImageUrlProperty);
        set => SetValue(ImageUrlProperty, value);
    }

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private CancellationTokenSource _cts;
    private SKBitmap _blurred;

    private async void OnUrlChanged(string url)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var old = _blurred;
        _blurred = null;
        old?.Dispose();
        InvalidateSurface();
        if (string.IsNullOrWhiteSpace(url))
            return;
        try
        {
            var bytes = await _http.GetByteArrayAsync(url, token);
            if (token.IsCancellationRequested)
                return;
            SKBitmap src = null;
            try
            {
                src = SKBitmap.Decode(bytes);
                if (src == null)
                    return;
                var baked = BakeBlurred(src);
                if (token.IsCancellationRequested)
                {
                    baked?.Dispose();
                    return;
                }
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (token.IsCancellationRequested)
                    {
                        baked?.Dispose();
                        return;
                    }
                    var prev = _blurred;
                    _blurred = baked;
                    prev?.Dispose();
                    InvalidateSurface();
                });
            }
            finally
            {
                src?.Dispose();
            }
        }
        catch
        {
            // Keep dark placeholder.
        }
    }

    private static SKBitmap BakeBlurred(SKBitmap src)
    {
        const int bakedWidth = 400;
        var bakedHeight = Math.Max(1, (int)(bakedWidth * (float)src.Height / src.Width));
        var dst = new SKBitmap(bakedWidth, bakedHeight);
        using var canvas = new SKCanvas(dst);
        using var paint = new SKPaint { ImageFilter = SKImageFilter.CreateBlur(25, 25) };
        canvas.DrawBitmap(src, new SKRect(0, 0, bakedWidth, bakedHeight), paint);
        return dst;
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(new SKColor(0x05, 0x15, 0x22));
        var bmp = _blurred;
        if (bmp == null)
            return;
        var dst = e.Info.Rect;
        if (dst.Width <= 0 || dst.Height <= 0)
            return;
        float scale = Math.Max(dst.Width / bmp.Width, dst.Height / bmp.Height);
        float w = bmp.Width * scale;
        float h = bmp.Height * scale;
        canvas.DrawBitmap(bmp, new SKRect((dst.Width - w) / 2, (dst.Height - h) / 2,
            (dst.Width + w) / 2, (dst.Height + h) / 2));
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ControllerMouse.App;

/// <summary>Shared decoded artwork and native coordinates; the input controls remain live WPF Buttons.</summary>
internal sealed class ControllerArtwork
{
    private static readonly Dictionary<int, ControllerArtwork> Cache = [];
    internal Uri ResourceUri { get; }
    internal BitmapSource? Source { get; }
    internal BitmapSource? Original { get; }
    internal Int32Rect NativeBounds { get; }
    internal BitmapSource? LeftCap { get; }
    internal BitmapSource? RightCap { get; }
    internal BitmapSource? ShellPatch { get; }

    private ControllerArtwork(int mode)
    {
        string name = mode == 3 ? "standard-front-dark" : mode == 2 ? "adaptive-rear" : mode == 1 ? "adaptive-front" : "standard-front";
        ResourceUri = new Uri("pack://application:,,,/Assets/Controllers/" + name + ".png", UriKind.Absolute);
        try
        {
            using var stream = Application.GetResourceStream(ResourceUri)?.Stream;
            if (stream == null) return;
            var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            Original = frame; Original.Freeze();
            var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            int width = converted.PixelWidth, height = converted.PixelHeight, stride = width * 4;
            var pixels = new byte[stride * height]; converted.CopyPixels(pixels, stride, 0);
            int left = width, top = height, right = -1, bottom = -1;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (pixels[y * stride + x * 4 + 3] > 32)
                    { left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y); }
            NativeBounds = right < left ? new Int32Rect(0, 0, width, height) :
                new Int32Rect(Math.Max(0, left - 4), Math.Max(0, top - 4), Math.Min(width - Math.Max(0, left - 4), right - left + 9), Math.Min(height - Math.Max(0, top - 4), bottom - top + 9));
            Source = new CroppedBitmap(frame, NativeBounds); Source.Freeze();
            if (mode is 0 or 3)
            {
                LeftCap = Crop(new(310, 276, 178, 178));
                RightCap = Crop(new(854, 496, 178, 178));
                // A plain area of the same shell covers the generated, unsupported Share glyph.
                ShellPatch = Crop(new(710, 474, 110, 68));
            }
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or InvalidOperationException or ArgumentException)
        { Source = null; }
    }
    private BitmapSource? Crop(Int32Rect rect)
    {
        if (Original == null || rect.X + rect.Width > Original.PixelWidth || rect.Y + rect.Height > Original.PixelHeight) return null;
        var crop = new CroppedBitmap(Original, rect); crop.Freeze(); return crop;
    }
    internal static ControllerArtwork For(int mode)
    {
        int key = mode == 0 && Application.Current is App { LightTheme: false } ? 3 : mode;
        if (!Cache.TryGetValue(key, out var art)) Cache[key] = art = new(key);
        return art;
    }
    internal Rect Fit(Rect available)
    {
        if (Source == null) return available;
        double scale = Math.Min(available.Width / Source.PixelWidth, available.Height / Source.PixelHeight);
        double width = Source.PixelWidth * scale, height = Source.PixelHeight * scale;
        return new(available.X + (available.Width - width) / 2, available.Y + (available.Height - height) / 2, width, height);
    }
    internal Point Map(Point native, Rect rendered)
    {
        if (Source == null) return native;
        return new(rendered.X + (native.X - NativeBounds.X) * rendered.Width / NativeBounds.Width,
            rendered.Y + (native.Y - NativeBounds.Y) * rendered.Height / NativeBounds.Height);
    }
    internal double Length(double native, Rect rendered) => Source == null ? native : native * rendered.Width / NativeBounds.Width;
}

internal readonly record struct ControllerPartLayout(Rect Target, Point Center, double Radius, Point? DpadCenter = null, double DpadRadius = 0);

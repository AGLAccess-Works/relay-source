using System.Globalization;
using System.Text.Json.Serialization;

namespace ControllerMouse.Core;

public enum DisplayScheme { RelayDefault, SoftLight, DeepDark, MaximumContrastLight, MaximumContrastDark, CviSimplified, Custom }
public enum VisualSpacing { Standard, Comfortable, Wide }
public enum VisualComplexity { Standard, Reduced, Minimal }
public enum VisualMotion { Standard, Reduced, Off }
public enum VisualFocus { Standard, Strong, VeryStrong }
public enum VisualHighlight { AglYellow, Blue, Green, Red, Orange, Purple, Custom }

/// <summary>Independent interface preferences. Text size remains Settings.InterfaceScale.</summary>
public sealed record VisualAccessPreferences
{
    public DisplayScheme Scheme { get; init; }
    public VisualSpacing Spacing { get; init; }
    public VisualComplexity Complexity { get; init; }
    public VisualMotion Motion { get; init; }
    public VisualFocus Focus { get; init; } = VisualFocus.Strong;
    public VisualHighlight Highlight { get; init; }
    public string CustomHighlight { get; init; } = "#FFC629";
    public bool OneControlAtATime { get; init; }
    public string BackgroundColour { get; init; } = "#F6F1FF";
    public string SurfaceColour { get; init; } = "#FDFBFF";
    public string TextColour { get; init; } = "#1F1B2E";
    public string SecondaryTextColour { get; init; } = "#514760";

    [JsonIgnore]
    public bool IsDark => Scheme is DisplayScheme.DeepDark or DisplayScheme.MaximumContrastDark ||
        Scheme == DisplayScheme.Custom && Luminance(Colour(BackgroundColour, "#F6F1FF")) < .179;

    public static VisualAccessPreferences ForScheme(DisplayScheme scheme)
    {
        if (!Enum.IsDefined(scheme)) scheme = DisplayScheme.RelayDefault;
        return scheme == DisplayScheme.CviSimplified
            ? new()
            {
                Scheme = scheme,
                Spacing = VisualSpacing.Wide,
                Complexity = VisualComplexity.Minimal,
                Motion = VisualMotion.Off,
                Focus = VisualFocus.VeryStrong,
                OneControlAtATime = true
            }
            : new() { Scheme = scheme };
    }

    // Validation does not reapply a preset: every preference may be changed independently.
    public VisualAccessPreferences Validate() => this with
    {
        Scheme = Enum.IsDefined(Scheme) ? Scheme : DisplayScheme.RelayDefault,
        Spacing = Enum.IsDefined(Spacing) ? Spacing : VisualSpacing.Standard,
        Complexity = Enum.IsDefined(Complexity) ? Complexity : VisualComplexity.Standard,
        Motion = Enum.IsDefined(Motion) ? Motion : VisualMotion.Standard,
        Focus = Enum.IsDefined(Focus) ? Focus : VisualFocus.Strong,
        Highlight = Enum.IsDefined(Highlight) ? Highlight : VisualHighlight.AglYellow,
        CustomHighlight = Colour(CustomHighlight, "#FFC629"),
        BackgroundColour = Colour(BackgroundColour, "#F6F1FF"),
        SurfaceColour = Colour(SurfaceColour, "#FDFBFF"),
        TextColour = Colour(TextColour, "#1F1B2E"),
        SecondaryTextColour = Colour(SecondaryTextColour, "#514760")
    };

    private static string Colour(string? value, string fallback)
    {
        string colour = value?.Trim() ?? "";
        if (colour.Length is not (4 or 7) || colour[0] != '#' || !colour.Skip(1).All(Uri.IsHexDigit)) return fallback;
        if (colour.Length == 4) colour = $"#{colour[1]}{colour[1]}{colour[2]}{colour[2]}{colour[3]}{colour[3]}";
        return colour.ToUpperInvariant();
    }

    private static double Luminance(string colour)
    {
        double Channel(int offset)
        {
            double value = byte.Parse(colour.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
            return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
        }
        return .2126 * Channel(1) + .7152 * Channel(3) + .0722 * Channel(5);
    }
}

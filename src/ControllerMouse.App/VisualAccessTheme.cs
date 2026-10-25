using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ControllerMouse.Core;

namespace ControllerMouse.App;

/// <summary>One semantic palette for the shell, dialogs, keyboard and live controller.</summary>
internal static class VisualAccessTheme
{
    internal static readonly FontFamily InterfaceFont = new("Segoe UI Variable, Segoe UI");

    internal static Dictionary<string, string> Create(VisualAccessPreferences preferences, out string[] adjustments)
    {
        var p = preferences.Validate();
        bool dark = p.IsDark;
        bool maximum = p.Scheme is DisplayScheme.MaximumContrastLight or DisplayScheme.MaximumContrastDark;
        bool neutral = p.Scheme is DisplayScheme.RelayDefault or DisplayScheme.DeepDark;
        string background = dark ? "#130E1B" : "#F6F1FF";
        string surface = dark ? "#1D1428" : "#FDFBFF";
        string subtle = dark ? "#22172F" : "#F2ECFA";
        string strong = dark ? "#271937" : "#EEE5FA";
        string primary = dark ? "#F9F4FF" : "#1F1B2E";
        string secondary = dark ? "#D8CBEA" : "#514760";
        string border = dark ? "#5F4B73" : "#D8D0E3";
        string boundary = dark ? "#B6A3CA" : "#79658E";
        var changes = new List<string>();
        if (neutral)
        {
            background = dark ? "#111B20" : "#F3F6F7";
            surface = dark ? "#18262D" : "#FFFFFF";
            subtle = dark ? "#1D2D35" : "#F0F5F6";
            strong = dark ? "#293E47" : "#E2ECEF";
            primary = dark ? "#F0F6F7" : "#182D35";
            secondary = dark ? "#BDCFD5" : "#4C626C";
            border = dark ? "#3B535F" : "#CEDCE1";
            boundary = dark ? "#90ABB6" : "#697F89";
        }
        else if (p.Scheme == DisplayScheme.SoftLight)
        { background = "#F8F4FB"; surface = "#FCFAFD"; subtle = "#F3EDF7"; strong = "#EDE4F5"; }
        else if (p.Scheme == DisplayScheme.CviSimplified)
        { background = "#FDFBFF"; surface = "#FDFBFF"; subtle = "#F6F1FF"; strong = "#F0E9F8"; boundary = "#514760"; }
        else if (maximum)
        {
            background = dark ? "#110C18" : "#FFFFFF"; surface = background;
            subtle = dark ? "#1D1428" : "#FDFBFF"; strong = dark ? "#271937" : "#F6F1FF";
            primary = dark ? "#FFFFFF" : "#1F1B2E"; secondary = primary;
            border = boundary = dark ? "#E6DCEF" : "#514760";
        }
        else if (p.Scheme == DisplayScheme.Custom)
        {
            background = p.BackgroundColour; surface = p.SurfaceColour;
            subtle = surface; strong = surface;
            primary = p.TextColour; secondary = p.SecondaryTextColour;
            // Every existing control shares these foreground roles. Opposite-luminance
            // surfaces can make a shared foreground impossible; keep the user background
            // and use it for surfaces rather than render inaccessible custom text.
            if (BestInk([background, surface]).ratio < 4.5)
            { surface = subtle = strong = background; changes.Add("Surface colour adjusted so text stays readable."); }
            string readable = Readable(primary, [background, surface, subtle, strong], 4.5);
            if (readable != primary) changes.Add("Text colour adjusted so text stays readable.");
            primary = readable;
            readable = Readable(secondary, [background, surface, subtle, strong], 4.5);
            if (readable != secondary) changes.Add("Supporting text colour adjusted so text stays readable.");
            secondary = readable;
            boundary = Readable(boundary, [surface], 3);
            border = maximum ? boundary : border;
        }
        string highlight = p.Highlight switch
        {
            VisualHighlight.Blue => "#2563EB",
            VisualHighlight.Green => "#16A34A",
            VisualHighlight.Red => "#DC2626",
            VisualHighlight.Orange => "#F97316",
            VisualHighlight.Purple => "#7C3AED",
            VisualHighlight.Custom => p.CustomHighlight,
            _ => "#FFC629"
        };
        // One restrained accent identifies the primary action. Focus and inspected
        // inputs retain the independent highlight chosen by the user.
        string action = neutral ? dark ? "#9BDCE5" : "#144E59" : "#FFC629";
        string actionText = neutral ? dark ? "#122B33" : "#FFFFFF" : "#3B1E6D";
        string actionBoundary = neutral ? actionText : "#3B1E6D";
        string selection = neutral ? surface : dark ? "#3C2B21" : "#FFF0B3";
        // Custom schemes may use light text on a light user surface or vice versa.
        // The selected-state surface must remain readable with the same control text.
        if (Contrast(primary, selection) < 4.5 || Contrast(secondary, selection) < 4.5) selection = surface;
        string accent = neutral ? action : dark ? "#BEA0FF" : "#7C3AED";
        string onAccent = neutral ? actionText : dark ? "#1F1B2E" : "#FFFFFF";
        // The inner edge must also remain visible on surfaces where the chosen
        // highlight is too close in luminance (for example purple on dark teal).
        string[] focusBackgrounds = [background, surface, subtle, strong, selection];
        string focusInner = Readable(neutral ? "#171717" : "#3B1E6D",
            new[] { highlight }.Concat(focusBackgrounds.Where(colour => Contrast(highlight, colour) < 3)).ToArray(), 3);
        string actionFocusInner = Readable(actionText, Contrast(highlight, action) < 3 ? [highlight, action] : [highlight], 3);
        var palette = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SurfaceBackgroundBrush"] = background,
            ["SurfaceRaisedBrush"] = surface,
            ["SurfaceSubtleBrush"] = subtle,
            ["SurfaceStrongBrush"] = strong,
            ["BorderDefaultBrush"] = border,
            ["BorderStrongBrush"] = boundary,
            ["TextPrimaryBrush"] = primary,
            ["TextSecondaryBrush"] = secondary,
            ["TextDisabledBrush"] = secondary,
            ["BrandBrush"] = neutral ? accent : dark ? "#D8CBEA" : "#3B1E6D",
            ["AccentPrimaryBrush"] = accent,
            ["AccentSecondaryBrush"] = accent,
            ["OnAccentBrush"] = onAccent,
            ["ActionFillBrush"] = action,
            ["ActionTextBrush"] = actionText,
            ["ActionBoundaryBrush"] = actionBoundary,
            ["SelectionSurfaceBrush"] = selection,
            ["SelectionTextBrush"] = primary,
            ["NavigationSelectionBrush"] = neutral ? strong : action,
            ["NavigationSelectedTextBrush"] = neutral ? primary : "#3B1E6D",
            ["NavigationAccentBrush"] = neutral ? primary : "#3B1E6D",
            ["FocusRingBrush"] = highlight,
            ["FocusInnerBrush"] = focusInner,
            ["ActionFocusInnerBrush"] = actionFocusInner,
            ["SelectedRingBrush"] = highlight,
            ["SelectedInnerBrush"] = focusInner,
            ["InputActiveRingBrush"] = dark ? "#4ADE80" : "#16A34A",
            ["SuccessBrush"] = dark ? "#A7F3C0" : "#166534",
            ["SuccessSurfaceBrush"] = dark ? "#153221" : "#E7F8EE",
            ["WarningBrush"] = dark ? "#FFD181" : "#92400E",
            ["ErrorBrush"] = dark ? "#FFAAA0" : "#B91C1C",
            ["ControllerBodyBrush"] = maximum ? surface : neutral ? dark ? "#141516" : "#F6F6F6" : "#F6F1FF",
            ["ControllerSocketBrush"] = maximum ? surface : neutral ? "#171717" : "#1F1B2E",
            ["ControllerCapBrush"] = maximum ? surface : neutral ? "#242526" : "#282230",
            ["ControllerCapTextBrush"] = maximum ? primary : neutral ? "#F5F5F5" : "#FDFBFF",
            // The adaptive rear plastic remains light in both normal appearances.
            ["ArtworkSurfaceBrush"] = maximum ? surface : "#F6F6F6",
            ["ArtworkInkBrush"] = maximum ? primary : "#171717"
        };
        foreach (var (alias, token) in new[] {
            ("BackgroundBrush","SurfaceBackgroundBrush"), ("PanelBrush","SurfaceRaisedBrush"),
            ("ControlBrush","SurfaceSubtleBrush"), ("LineBrush","BorderStrongBrush"),
            ("MutedBrush","TextSecondaryBrush"), ("TextBrush","TextPrimaryBrush"),
            ("AccentBrush","AccentSecondaryBrush"), ("InputBrush","InputActiveRingBrush"),
            ("StatusSurfaceBrush","SurfaceSubtleBrush") }) palette[alias] = palette[token];
        adjustments = changes.ToArray();
        return palette;
    }

    internal static void Apply(ResourceDictionary resources, VisualAccessPreferences preferences, bool systemContrast, out string[] adjustments)
    {
        var palette = Create(preferences, out adjustments);
        foreach (var (token, colour) in palette)
        {
            Brush brush = systemContrast ? SystemBrush(token) : new SolidColorBrush(Parse(colour));
            if (!systemContrast) brush.Freeze();
            resources[token] = brush;
        }
        double outer = preferences.Focus switch { VisualFocus.VeryStrong => 5, VisualFocus.Strong => 4, _ => 3 };
        resources["FocusOuterThickness"] = new Thickness(outer);
        resources["FocusInnerThickness"] = new Thickness(preferences.Focus == VisualFocus.Standard ? 1 : 2);
        resources["FocusStrokeWidth"] = outer;
        resources["InterfaceFontFamily"] = InterfaceFont;
        resources["UseControllerArtwork"] = !systemContrast && preferences.Scheme is not (DisplayScheme.MaximumContrastLight or DisplayScheme.MaximumContrastDark)
            && preferences.Complexity != VisualComplexity.Minimal;
    }

    private static Brush SystemBrush(string token) => token switch
    {
        "ActionFillBrush" or "AccentBrush" or "AccentPrimaryBrush" or "AccentSecondaryBrush" or "NavigationSelectionBrush" or
            "FocusRingBrush" or "SelectedRingBrush" or "InputActiveRingBrush" or "InputBrush" => SystemColors.HighlightBrush,
        "OnAccentBrush" or "ActionTextBrush" or "ActionFocusInnerBrush" or "NavigationSelectedTextBrush" or "NavigationAccentBrush" => SystemColors.HighlightTextBrush,
        "FocusInnerBrush" or "SelectedInnerBrush" => SystemColors.WindowTextBrush,
        "TextDisabledBrush" => SystemColors.GrayTextBrush,
        "SurfaceBackgroundBrush" or "SurfaceRaisedBrush" or "SurfaceSubtleBrush" or "BackgroundBrush" or "PanelBrush" or
            "ControlBrush" or "ControllerSocketBrush" => SystemColors.WindowBrush,
        "SurfaceStrongBrush" or "SelectionSurfaceBrush" or "SuccessSurfaceBrush" or "StatusSurfaceBrush" or "ArtworkSurfaceBrush" or
            "ControllerBodyBrush" or "ControllerCapBrush" => SystemColors.ControlBrush,
        "ControllerCapTextBrush" => SystemColors.ControlTextBrush,
        _ => SystemColors.WindowTextBrush
    };

    internal static double Contrast(string first, string second) => Contrast(Parse(first), Parse(second));
    internal static double Contrast(Color first, Color second)
    {
        static double Luminance(Color colour)
        {
            static double Linear(byte component) { double c = component / 255d; return c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4); }
            return .2126 * Linear(colour.R) + .7152 * Linear(colour.G) + .0722 * Linear(colour.B);
        }
        double a = Luminance(first), b = Luminance(second);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }
    private static Color Parse(string value) => (Color)ColorConverter.ConvertFromString(value);
    private static (string ink, double ratio) BestInk(string[] backgrounds) => new[] { "#1F1B2E", "#FDFBFF", "#000000", "#FFFFFF", "#767676" }
        .Select(ink => (ink, ratio: backgrounds.Min(background => Contrast(ink, background)))).OrderByDescending(pair => pair.ratio).First();
    private static string Readable(string preferred, string[] backgrounds, double minimum) => backgrounds.All(background => Contrast(preferred, background) >= minimum)
        ? preferred : BestInk(backgrounds).ink;

    /// <summary>Exercises the actual resolved roles, including customised unsafe colours.</summary>
    internal static string VerifyPaletteContrast()
    {
        var lines = new List<string>();
        foreach (var scheme in Enum.GetValues<DisplayScheme>())
        {
            Check(VisualAccessPreferences.ForScheme(scheme));
            foreach (var highlight in Enum.GetValues<VisualHighlight>())
                Check(VisualAccessPreferences.ForScheme(scheme) with { Highlight = highlight, CustomHighlight = "#FFFFFF" });
        }
        Check(VisualAccessPreferences.ForScheme(DisplayScheme.Custom) with
        {
            BackgroundColour = "#FFFFFF",
            SurfaceColour = "#000000",
            TextColour = "#FFFFFF",
            SecondaryTextColour = "#FFFFFF"
        });
        foreach (var scheme in new[] { DisplayScheme.RelayDefault, DisplayScheme.DeepDark })
            foreach (int red in new[] { 0, 51, 102, 153, 204, 255 })
                foreach (int green in new[] { 0, 51, 102, 153, 204, 255 })
                    foreach (int blue in new[] { 0, 51, 102, 153, 204, 255 })
                        Check(VisualAccessPreferences.ForScheme(scheme) with
                        {
                            Highlight = VisualHighlight.Custom,
                            CustomHighlight = $"#{red:X2}{green:X2}{blue:X2}"
                        });
        return string.Join(Environment.NewLine, lines);

        void Check(VisualAccessPreferences preferences)
        {
            var palette = Create(preferences, out _);
            void Pair(string foreground, string background, double minimum)
            {
                double ratio = Contrast(palette[foreground], palette[background]);
                if (ratio < minimum) throw new InvalidOperationException($"{preferences.Scheme}/{preferences.Highlight}: {foreground} against {background} is {ratio:F4}:1.");
            }
            foreach (string background in new[] { "SurfaceBackgroundBrush", "SurfaceRaisedBrush", "SurfaceSubtleBrush", "SurfaceStrongBrush", "SelectionSurfaceBrush" })
            { Pair("TextPrimaryBrush", background, 4.5); Pair("TextSecondaryBrush", background, 4.5); }
            Pair("ActionTextBrush", "ActionFillBrush", 4.5); Pair("ActionBoundaryBrush", "ActionFillBrush", 3);
            Pair("NavigationSelectedTextBrush", "NavigationSelectionBrush", 4.5);
            Pair("OnAccentBrush", "AccentBrush", 4.5); Pair("SuccessBrush", "SuccessSurfaceBrush", 4.5);
            Pair("BorderStrongBrush", "ControlBrush", 3); Pair("FocusInnerBrush", "FocusRingBrush", 3);
            Pair("ActionFocusInnerBrush", "FocusRingBrush", 3);
            if (Math.Max(Contrast(palette["FocusRingBrush"], palette["ActionFillBrush"]),
                Contrast(palette["ActionFocusInnerBrush"], palette["ActionFillBrush"])) < 3)
                throw new InvalidOperationException($"{preferences.Scheme}/{preferences.Highlight}: neither action focus layer contrasts with its fill.");
            foreach (string background in new[] { "SurfaceBackgroundBrush", "SurfaceRaisedBrush", "SurfaceSubtleBrush", "SurfaceStrongBrush", "SelectionSurfaceBrush", "ControllerBodyBrush" })
                if (Math.Max(Contrast(palette["FocusRingBrush"], palette[background]), Contrast(palette["FocusInnerBrush"], palette[background])) < 3)
                    throw new InvalidOperationException($"{preferences.Scheme}/{preferences.Highlight}: neither focus layer contrasts with {background}.");
            Pair("ArtworkInkBrush", "ArtworkSurfaceBrush", 4.5);
            lines.Add($"{preferences.Scheme}/{preferences.Highlight}" + (preferences.Highlight == VisualHighlight.Custom ? "/" + preferences.CustomHighlight : "") +
                ": semantic text, actions, selection, combined focus and artwork contrast PASS.");
        }
    }
}

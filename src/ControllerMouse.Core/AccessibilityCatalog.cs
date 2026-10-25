namespace ControllerMouse.Core;

public sealed record AccessibilityFeature(string Category, string Name, string Description, string Uri, int MinimumBuild = 17763)
{
    public Shortcut PageAction => Uri switch
    {
        "ms-settings:easeofaccess-display" => Shortcut.SettingsTextSize,
        "ms-settings:easeofaccess-visualeffects" => Shortcut.SettingsVisualEffects,
        "ms-settings:easeofaccess-mousepointer" => Shortcut.SettingsPointer,
        "ms-settings:easeofaccess-cursor" => Shortcut.SettingsCursor,
        "ms-settings:easeofaccess-magnifier" => Shortcut.SettingsMagnifier,
        "ms-settings:easeofaccess-colorfilter" => Shortcut.SettingsColourFilters,
        "ms-settings:easeofaccess-screentint" => Shortcut.SettingsScreenTint,
        "ms-settings:easeofaccess-highcontrast" => Shortcut.SettingsContrast,
        "ms-settings:easeofaccess-narrator" => Shortcut.SettingsNarrator,
        "ms-settings:easeofaccess-audio" => Shortcut.SettingsAudio,
        "ms-settings:easeofaccess-closedcaptioning" => Shortcut.SettingsCaptions,
        "ms-settings:easeofaccess-hearingaids" => Shortcut.SettingsHearing,
        "ms-settings:easeofaccess-speechrecognition" => Shortcut.SettingsSpeech,
        "ms-settings:easeofaccess-keyboard" => Shortcut.SettingsKeyboard,
        "ms-settings:easeofaccess-mouse" => Shortcut.SettingsMouse,
        "ms-settings:easeofaccess-eyecontrol" => Shortcut.SettingsEyeControl,
        "ms-settings:typing" => Shortcut.SettingsTyping,
        _ => Shortcut.None
    };
}
public sealed record ActionOption(Shortcut Action, string Name);

public static class AccessibilityCatalog
{
    public static IReadOnlyList<AccessibilityFeature> Features { get; } = new AccessibilityFeature[]
    {
        new("Vision", "Text size", "Make text easier to read across Windows.", "ms-settings:easeofaccess-display"),
        new("Vision", "Visual effects", "Animation, transparency, and notification timing.", "ms-settings:easeofaccess-visualeffects", 22000),
        new("Vision", "Pointer & touch", "Pointer size, colour, and touch feedback.", "ms-settings:easeofaccess-mousepointer"),
        new("Vision", "Text cursor", "A more visible typing indicator and cursor.", "ms-settings:easeofaccess-cursor", 19041),
        new("Vision", "Magnifier", "Zoom, tracking, reading, and lens options.", "ms-settings:easeofaccess-magnifier"),
        new("Vision", "Colour filters", "Adjust the colours displayed on screen.", "ms-settings:easeofaccess-colorfilter"),
        new("Vision", "Screen tint", "Choose a screen tint and its strength. Requires a Windows build with Screen tint.", "ms-settings:easeofaccess-screentint", 22000),
        new("Vision", "Contrast themes", "High-contrast colours for text and controls.", "ms-settings:easeofaccess-highcontrast"),
        new("Vision", "Narrator & braille", "Screen reading, voices, navigation, and braille setup.", "ms-settings:easeofaccess-narrator"),
        new("Hearing", "Audio", "Mono sound and visual alerts for notifications.", "ms-settings:easeofaccess-audio"),
        new("Hearing", "Captions", "Caption appearance and Live captions on Windows 11.", "ms-settings:easeofaccess-closedcaptioning"),
        new("Hearing", "Hearing devices", "Manage supported hearing aids. Requires compatible hardware.", "ms-settings:easeofaccess-hearingaids", 26100),
        new("Interaction", "Speech & voice access", "Voice control and speech options available on your Windows version.", "ms-settings:easeofaccess-speechrecognition"),
        new("Interaction", "Keyboard", "On-screen keyboard, Sticky Keys, Filter Keys, and Toggle Keys.", "ms-settings:easeofaccess-keyboard"),
        new("Interaction", "Mouse Keys", "Move the pointer with the numeric keypad.", "ms-settings:easeofaccess-mouse"),
        new("Interaction", "Eye control", "Use a supported eye tracker to interact with Windows.", "ms-settings:easeofaccess-eyecontrol"),
        new("Interaction", "Typing assistance", "Text suggestions, spelling, and typing preferences.", "ms-settings:typing")
    };
    public static IReadOnlyList<ActionOption> Actions { get; } = new ActionOption[]
    {
        new(Shortcut.None, "No action"), new(Shortcut.AccessibilityHub, "Open accessibility hub"),
        new(Shortcut.Accessibility, "Windows accessibility settings"), new(Shortcut.ZoomIn, "Magnifier: zoom in"),
        new(Shortcut.ZoomOut, "Magnifier: zoom out"), new(Shortcut.CloseMagnifier, "Close Magnifier"),
        new(Shortcut.MagnifierStart, "Start Magnifier (keep current zoom if running)"),
        new(Shortcut.NarratorStart, "Start Narrator"), new(Shortcut.NarratorStop, "Stop Narrator"),
        new(Shortcut.MagnifierRead, "Magnifier: read / pause"), new(Shortcut.MagnifierView, "Magnifier: change view"),
        new(Shortcut.MagnifierFullScreen, "Magnifier: full screen"), new(Shortcut.MagnifierLens, "Magnifier: lens"),
        new(Shortcut.MagnifierDocked, "Magnifier: docked"), new(Shortcut.MagnifierInvert, "Magnifier: invert colours"),
        new(Shortcut.ScreenSnipping, "Screen snipping"),
        new(Shortcut.Narrator, "Toggle Narrator"), new(Shortcut.OnScreenKeyboard, "Relay keyboard"),
        new(Shortcut.WindowsOnScreenKeyboard, "Windows on-screen keyboard (protected)"),
        new(Shortcut.LiveCaptions, "Toggle Live captions (Windows 11)"), new(Shortcut.ColorFilters, "Toggle colour filters"),
        new(Shortcut.VoiceTyping, "Voice typing / speech to text (Windows + H)"), new(Shortcut.StartMenu, "Windows Start")
    }.Concat(Features.Select(f => new ActionOption(f.PageAction, "Open Windows settings: " + f.Name))).ToArray();
    public static string? SettingsUri(Shortcut action) => action == Shortcut.Accessibility ? "ms-settings:easeofaccess" : Features.FirstOrDefault(f => f.PageAction == action)?.Uri;
    public static bool IsQuickAction(Shortcut value) => Actions.Any(a => a.Action == value);
    public static ushort[] Keys(Shortcut shortcut) => shortcut switch
    {
        Shortcut.StartMenu => [0x5B],
        Shortcut.ZoomIn => [0x5B, 0x6B],
        Shortcut.ZoomOut => [0x5B, 0x6D],
        Shortcut.CloseMagnifier => [0x5B, 0x1B],
        Shortcut.Up => [0x26],
        Shortcut.Down => [0x28],
        Shortcut.Left => [0x25],
        Shortcut.Right => [0x27],
        Shortcut.Narrator => [0x5B, 0x11, 0x0D],
        Shortcut.WindowsOnScreenKeyboard => [0x5B, 0x11, 0x4F],
        Shortcut.LiveCaptions => [0x5B, 0x11, 0x4C],
        Shortcut.ColorFilters => [0x5B, 0x11, 0x43],
        Shortcut.VoiceTyping => [0x5B, 0x48],
        Shortcut.MagnifierRead => [0x11, 0x12, 0x0D],
        Shortcut.MagnifierView => [0x11, 0x12, 0x4D],
        Shortcut.MagnifierFullScreen => [0x11, 0x12, 0x46],
        Shortcut.MagnifierLens => [0x11, 0x12, 0x4C],
        Shortcut.MagnifierDocked => [0x11, 0x12, 0x44],
        Shortcut.MagnifierInvert => [0x11, 0x12, 0x49],
        Shortcut.ScreenSnipping => [0x5B, 0x10, 0x53],
        _ => []
    };
}

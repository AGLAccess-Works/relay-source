using System.Text.Json;

namespace ControllerMouse.Core;

public sealed record Profile(string Id, string Name, Settings Settings);
public sealed record ProfileLibrary(int Version, string ActiveId, Profile[] Profiles)
{
    public int AccessibilityRevision { get; init; }
    public bool SetupCompleted { get; init; }
    public ProfileLibrary WithAccessibilityDefaults()
    {
        if (AccessibilityRevision >= 1) return this;
        var profiles = Profiles.ToList();
        foreach (var preset in AccessibilityProfiles.All)
        {
            if (profiles.Count >= 64) break;
            if (profiles.Any(p => p.Id == preset.Id || p.Name.Equals(preset.Name, StringComparison.OrdinalIgnoreCase))) continue;
            profiles.Add(new(preset.Id, preset.Name, preset.Settings.Validate()));
        }
        return this with { Profiles = profiles.ToArray(), AccessibilityRevision = 1 };
    }
    public Profile Active => Profiles.First(p => p.Id == ActiveId);
    public ProfileLibrary Update(Settings settings) => this with
    {
        Profiles = Profiles.Select(p => p.Id == ActiveId ? p with { Settings = settings.Validate() } : p).ToArray()
    };
    public static ProfileLibrary Create(Settings existing)
    {
        Profile[] profiles =
        [
            new("personal", "My desktop", existing.Validate()),
            new("precise", "Precise", new Settings { PointerSpeed = 650, ScrollSpeed = 5, ResponseCurve = 2, Smoothing = .035 }),
            new("steady", "Steady hands", new Settings { PointerSpeed = 850, ScrollSpeed = 5, ResponseCurve = 1.9,
                Smoothing = .035, TremorStabilization = true, StabilizationStrength = .7, ClickHoldMilliseconds = 100,
                ScrollStabilization = true, ScrollAxisLock = true, ScrollSmoothing = .035 }),
            new("quick", "Quick", new Settings { PointerSpeed = 1800, ScrollSpeed = 12, ResponseCurve = 1.3, Smoothing = .015 }),
            new("reading", "Reading", new Settings { PointerSpeed = 750, ScrollSpeed = 4, ScrollMode = ScrollMode.Step, ScrollAxisLock = true,
                ButtonMappings = new() { [Control.Y] = new(BindingKind.Shortcut, Shortcut: Shortcut.MagnifierRead) } })
        ];
        return new ProfileLibrary(1, profiles[0].Id, profiles).WithAccessibilityDefaults();
    }
    public ProfileLibrary Validate()
    {
        if (Version != 1 || Profiles is not { Length: > 0 and <= 64 } ||
            Profiles.Any(p => p == null || string.IsNullOrWhiteSpace(p.Id) || p.Id.Length > 100 ||
                !ValidName(p.Name) || p.Settings == null) ||
            Profiles.Select(p => p.Id).Distinct().Count() != Profiles.Length ||
            Profiles.Select(p => p.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Profiles.Length ||
            !Profiles.Any(p => p.Id == ActiveId)) throw new JsonException("Invalid or unsupported profile library.");
        return this with { Profiles = Profiles.Select(p => p with { Name = p.Name.Trim(), Settings = p.Settings.Validate() }).ToArray() };
    }
    public static bool ValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 60 && !name.Any(char.IsControl);
}

public sealed record ProfileExport(int Version, string Name, Settings Settings);

public sealed class ProfileStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string? Warning { get; private set; }
    private bool _preserveUnreadable;
    public ProfileLibrary Load(Settings existing)
    {
        if (!File.Exists(path)) return ProfileLibrary.Create(existing);
        try
        {
            if (new FileInfo(path).Length > 2_000_000) throw new JsonException("Profile library is too large.");
            return (JsonSerializer.Deserialize<ProfileLibrary>(File.ReadAllText(path)) ?? throw new JsonException()).Validate().WithAccessibilityDefaults();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _preserveUnreadable = true;
            Warning = "Saved profiles could not be read. A recovery copy will be kept before saving new profiles.";
            return ProfileLibrary.Create(existing);
        }
    }
    public void Save(ProfileLibrary library)
    {
        var json = JsonSerializer.Serialize(library.Validate(), Options);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        if (_preserveUnreadable && File.Exists(path))
        {
            File.Copy(path, path + ".recovery-" + Guid.NewGuid().ToString("N"));
            _preserveUnreadable = false;
        }
        var temporary = path + ".tmp";
        try { File.WriteAllText(temporary, json); File.Move(temporary, path, true); Warning = null; }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static void Export(string path, Profile profile) => File.WriteAllText(path,
        JsonSerializer.Serialize(new ProfileExport(1, profile.Name, profile.Settings.Validate()), Options));
    public static Profile Import(string path)
    {
        if (new FileInfo(path).Length > 100_000) throw new JsonException("The profile exceeds 100 KB.");
        var data = JsonSerializer.Deserialize<ProfileExport>(File.ReadAllText(path)) ?? throw new JsonException("Empty profile.");
        if (data.Version != 1 || !ProfileLibrary.ValidName(data.Name) || data.Settings == null || !ButtonChord.ValidList(data.Settings.ButtonChords) ||
            data.Settings.ButtonMappings?.Any(p => !Enum.IsDefined(p.Key) || p.Value is not { IsValid: true } ||
                (p.Key is Control.Back or Control.Start && (p.Value.Kind is BindingKind.Precision or BindingKind.Boost or BindingKind.Pointer || p.Value.Repeat))) == true)
            throw new JsonException("Invalid or unsupported profile. Use a Relay version 1 profile export.");
        return new(Guid.NewGuid().ToString("N"), data.Name.Trim(), data.Settings.Validate());
    }
}

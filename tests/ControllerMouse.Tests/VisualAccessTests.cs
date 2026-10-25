using System.Text.Json;
using ControllerMouse.Core;

internal static class VisualAccessTests
{
    private static void Check(bool condition, string message = "Assertion failed")
    {
        if (!condition) throw new Exception(message);
    }

    private static bool SameAccessories(AdaptiveAccessory[]? first, AdaptiveAccessory[]? second) =>
        first == null || second == null ? first == second : first.Length == second.Length && first.Zip(second).All(pair =>
            pair.First.Port == pair.Second.Port && pair.First.Name == pair.Second.Name && pair.First.Kind == pair.Second.Kind &&
            pair.First.Signals.SequenceEqual(pair.Second.Signals));

    private static VisualAccessPreferences IndependentChoices => new()
    {
        Scheme = DisplayScheme.Custom,
        Spacing = VisualSpacing.Wide,
        Complexity = VisualComplexity.Reduced,
        Motion = VisualMotion.Off,
        Focus = VisualFocus.VeryStrong,
        Highlight = VisualHighlight.Custom,
        CustomHighlight = "#FFAA33",
        OneControlAtATime = true,
        BackgroundColour = "#130E1B",
        SurfaceColour = "#271937",
        TextColour = "#F9F4FF",
        SecondaryTextColour = "#D8CBEA"
    };

    public static void Run(Action<string, Action> test)
    {
        test("Windows text size is a minimum without multiplying or overwriting Relay preferences", () =>
        {
            Check(VisualTextScale.Resolve(1, 1.5) == 1.5);
            Check(VisualTextScale.Resolve(1.8, 1.5) == 1.8);
            Check(VisualTextScale.Resolve(2, 2.25) == 2.25);
            Check(VisualTextScale.Resolve(2, 1) == 2);
            Check(VisualTextScale.Resolve(double.NaN, double.PositiveInfinity) == 1);
            Check(VisualTextScale.Resolve(-1, 99) == 2.25);
            var settings = new Settings { InterfaceScale = 1.8 };
            _ = VisualTextScale.Resolve(settings.InterfaceScale, 2.25);
            Check(settings.InterfaceScale == 1.8);
        });
        test("new installations use Relay Default without duplicating saved text size", () =>
        {
            var settings = new Settings();
            Check(settings.EffectiveVisualAccess.Scheme == DisplayScheme.RelayDefault && settings.InterfaceScale == 1);
            Check(settings.EffectiveVisualAccess.Focus == VisualFocus.Strong && !settings.EffectiveVisualAccess.OneControlAtATime);
            string json = JsonSerializer.Serialize(settings);
            Check(!json.Contains("EffectiveVisualAccess") && !json.Contains("TextSize"));
        });

        foreach (bool light in new[] { true, false })
            foreach (bool reduced in new[] { false, true })
                test($"legacy appearance migrates light={light} reduced-motion={reduced} without altering access preferences", () =>
                {
                    string json = JsonSerializer.Serialize(new
                    {
                        LightTheme = light,
                        ReduceInterfaceMotion = reduced,
                        InterfaceScale = 1.75,
                        ReadHoveredText = false,
                        PointerSpeed = 650,
                        ButtonMappings = new Dictionary<Control, ControlBinding> { [Control.A] = new(BindingKind.Keyboard, Key: 13) }
                    });
                    var legacy = JsonSerializer.Deserialize<Settings>(json)!.Validate();
                    Check(legacy.VisualAccess == null && legacy.InterfaceScale == 1.75 && !legacy.ReadHoveredText && legacy.PointerSpeed == 650);
                    Check(legacy.EffectiveVisualAccess.Scheme == (light ? DisplayScheme.RelayDefault : DisplayScheme.DeepDark));
                    Check(legacy.EffectiveVisualAccess.Motion == (reduced ? VisualMotion.Reduced : VisualMotion.Standard));
                    Check(ControlMapping.Resolve(legacy, Control.A).Kind == BindingKind.Keyboard && ControlMapping.Resolve(legacy, Control.A).Key == 13);
                    var again = legacy.Validate();
                    Check(again.VisualAccess == null && again.InterfaceScale == legacy.InterfaceScale &&
                        again.LightTheme == legacy.LightTheme && again.ReduceInterfaceMotion == legacy.ReduceInterfaceMotion &&
                        ControlMapping.Resolve(again, Control.A) == ControlMapping.Resolve(legacy, Control.A),
                        "Validation rewrote the legacy marker, appearance, text size or mapping.");
                });

        test("invalid saved visual enum values recover to supported accessible defaults", () =>
        {
            var restored = JsonSerializer.Deserialize<Settings>("""
                {"InterfaceScale":2,"VisualAccess":{"Scheme":99,"Spacing":99,"Complexity":99,"Motion":99,"Focus":99,"Highlight":99}}
                """)!.Validate();
            Check(restored.EffectiveVisualAccess == new VisualAccessPreferences() && restored.InterfaceScale == 2);
        });

        test("malformed custom colours cannot introduce transparent or unsupported colour values", () =>
        {
            var invalid = new VisualAccessPreferences
            {
                CustomHighlight = "red",
                BackgroundColour = "#11223344",
                SurfaceColour = "#GGFFFF",
                TextColour = null!,
                SecondaryTextColour = ""
            }.Validate();
            Check(invalid == new VisualAccessPreferences());
        });

        test("valid custom colours normalize without changing the user's chosen palette", () =>
        {
            var choice = IndependentChoices with { CustomHighlight = " #fa3 ", BackgroundColour = "#130e1b", SurfaceColour = "#271937" };
            var validated = choice.Validate();
            Check(validated.CustomHighlight == "#FFAA33" && validated.BackgroundColour == "#130E1B");
            Check(validated.SurfaceColour == choice.SurfaceColour && validated.TextColour == choice.TextColour &&
                validated.SecondaryTextColour == choice.SecondaryTextColour && validated.Highlight == VisualHighlight.Custom);
        });

        test("custom background darkness keeps legacy controller-art appearance truthful", () =>
        {
            Check(IndependentChoices.IsDark);
            Check(!(IndependentChoices with { BackgroundColour = "#FDFBFF" }).IsDark);
            Check(!(IndependentChoices with { BackgroundColour = "invalid" }).IsDark);
            Check(!JsonSerializer.Serialize(IndependentChoices).Contains("IsDark"));
        });

        test("explicit visual choices take priority over legacy appearance and motion flags", () =>
        {
            var settings = new Settings { VisualAccess = IndependentChoices, LightTheme = true, ReduceInterfaceMotion = false };
            Check(settings.EffectiveVisualAccess == IndependentChoices);
        });

        test("CVI starter reduces complexity without changing controller mappings or motor-access settings", () =>
        {
            var settings = AdaptiveAccessibilityProfiles.All[0].Settings with
            {
                PointerSpeed = 420,
                InterfaceScale = 1,
                AdaptiveAccessories = [new("A", "Green switch", AccessoryKind.Switch, [AccessorySignal.A])]
            };
            var result = settings.WithVisualAccessScheme(DisplayScheme.CviSimplified);
            var visual = result.EffectiveVisualAccess;
            Check(visual.Scheme == DisplayScheme.CviSimplified && visual.Spacing == VisualSpacing.Wide &&
                visual.Complexity == VisualComplexity.Minimal && visual.Motion == VisualMotion.Off &&
                visual.Focus == VisualFocus.VeryStrong && visual.OneControlAtATime && result.InterfaceScale == 1.5);
            Check(result.PointerSpeed == settings.PointerSpeed && result.ControllerType == settings.ControllerType &&
                result.NarratorNavigation == settings.NarratorNavigation && result.Haptics == settings.Haptics &&
                SameAccessories(result.AdaptiveAccessories, settings.AdaptiveAccessories) &&
                result.ButtonMappings!.All(pair => pair.Value == settings.ButtonMappings![pair.Key]));
        });

        foreach (DisplayScheme scheme in Enum.GetValues<DisplayScheme>())
            test($"visual preset {scheme} preserves larger text and custom palette for later use", () =>
            {
                var settings = new Settings
                {
                    InterfaceScale = 2,
                    VisualAccess = IndependentChoices,
                    ReadHoveredText = false,
                    InputAnnouncements = true
                };
                var applied = settings.WithVisualAccessScheme(scheme);
                Check(applied.EffectiveVisualAccess.Scheme == scheme && applied.InterfaceScale == 2 &&
                    !applied.ReadHoveredText && applied.InputAnnouncements);
                Check(applied.EffectiveVisualAccess.CustomHighlight == IndependentChoices.CustomHighlight &&
                    applied.EffectiveVisualAccess.BackgroundColour == IndependentChoices.BackgroundColour &&
                    applied.EffectiveVisualAccess.SurfaceColour == IndependentChoices.SurfaceColour &&
                    applied.EffectiveVisualAccess.TextColour == IndependentChoices.TextColour &&
                    applied.EffectiveVisualAccess.SecondaryTextColour == IndependentChoices.SecondaryTextColour);
                Check(applied.LightTheme == !applied.EffectiveVisualAccess.IsDark &&
                    applied.ReduceInterfaceMotion == (applied.EffectiveVisualAccess.Motion != VisualMotion.Standard));
            });

        test("CVI individual overrides survive validation and persistence without reapplying its defaults", () =>
        {
            var settings = new Settings().WithVisualAccessScheme(DisplayScheme.CviSimplified);
            settings = settings with
            {
                InterfaceScale = 1.75,
                VisualAccess = settings.EffectiveVisualAccess with
                {
                    OneControlAtATime = false,
                    Spacing = VisualSpacing.Comfortable,
                    Complexity = VisualComplexity.Standard,
                    Motion = VisualMotion.Reduced,
                    Focus = VisualFocus.Standard,
                    Highlight = VisualHighlight.Blue
                }
            };
            var restored = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings))!.Validate();
            Check(restored == settings && !restored.EffectiveVisualAccess.OneControlAtATime && restored.InterfaceScale == 1.75);
        });

        test("one-control mode and custom highlights work independently of the CVI scheme", () =>
        {
            var settings = new Settings { VisualAccess = IndependentChoices with { Scheme = DisplayScheme.DeepDark } };
            var restored = settings.Validate();
            Check(restored.EffectiveVisualAccess.Scheme == DisplayScheme.DeepDark && restored.EffectiveVisualAccess.OneControlAtATime &&
                restored.EffectiveVisualAccess.Highlight == VisualHighlight.Custom && restored.EffectiveVisualAccess.Motion == VisualMotion.Off);
        });

        test("profile switches retain visual preferences without replacing input mappings or saved accessories", () =>
        {
            var preferences = new Settings
            {
                VisualAccess = IndependentChoices,
                InterfaceScale = 2,
                LightTheme = false,
                ReduceInterfaceMotion = true,
                ShowControllerLabels = true,
                InputAnnouncements = true,
                ReadHoveredText = false,
                MinimizeToTray = false
            };
            var inputProfile = AdaptiveAccessibilityProfiles.All[0].Settings with
            { AdaptiveAccessories = [new("A", "Chin switch", AccessoryKind.Switch, [AccessorySignal.A])] };
            var applied = inputProfile.WithInterfacePreferences(preferences);
            Check(applied.EffectiveVisualAccess == IndependentChoices && applied.InterfaceScale == 2 &&
                !applied.ReadHoveredText && applied.ShowControllerLabels && applied.InputAnnouncements && !applied.MinimizeToTray);
            Check(applied.ControllerType == inputProfile.ControllerType && applied.AdaptiveAccessories == inputProfile.AdaptiveAccessories &&
                applied.ButtonMappings == inputProfile.ButtonMappings && applied.PointerSpeed == inputProfile.PointerSpeed);
        });

        test("guided setup and preview preserve disabled hover reading and all independent visual choices", () =>
        {
            var preferences = new Settings
            {
                VisualAccess = IndependentChoices,
                InterfaceScale = 2,
                ReadHoveredText = false,
                MinimizeToTray = false,
                ShowControllerLabels = true,
                ReduceInterfaceMotion = true,
                InputAnnouncements = true,
                ControllerType = ControllerPreference.Adaptive,
                AdaptiveAccessories = [new("A", "Green switch", AccessoryKind.Switch, [AccessorySignal.A])]
            };
            foreach (var built in new[] { SetupPlan.Build(new SetupChoices { LargerText = true }, preferences), SetupPlan.Navigation(preferences) })
                Check(built.EffectiveVisualAccess == IndependentChoices && built.InterfaceScale == 2 && !built.ReadHoveredText &&
                    !built.MinimizeToTray && built.ShowControllerLabels && built.ReduceInterfaceMotion && built.InputAnnouncements &&
                    built.ControllerType == ControllerPreference.Adaptive && SameAccessories(built.AdaptiveAccessories, preferences.AdaptiveAccessories));
        });

        test("guided larger-text choice raises small text while keeping an existing extra-large choice", () =>
        {
            Check(SetupPlan.Build(new SetupChoices { LargerText = true }, new Settings()).InterfaceScale == 1.5);
            Check(SetupPlan.Build(new SetupChoices { LargerText = true }, new Settings { InterfaceScale = 1.75 }).InterfaceScale == 1.75);
            Check(SetupPlan.Build(new SetupChoices { LargerText = false }, new Settings { InterfaceScale = 2 }).InterfaceScale == 2);
        });

        test("visual access settings survive the actual settings store and omit computed migration fields", () => InTemporaryFolder(folder =>
        {
            var settings = new Settings { VisualAccess = IndependentChoices, InterfaceScale = 1.75, ReadHoveredText = false };
            var store = new SettingsStore(Path.Combine(folder, "settings.json"));
            store.Save(settings);
            Check(store.Load() == settings && !File.ReadAllText(store.Path).Contains("EffectiveVisualAccess"));
            File.WriteAllText(store.Path, "{\"LightTheme\":false,\"InterfaceScale\":2,\"ReadHoveredText\":false}");
            var legacy = store.Load();
            Check(legacy.VisualAccess == null && legacy.EffectiveVisualAccess.Scheme == DisplayScheme.DeepDark &&
                legacy.InterfaceScale == 2 && !legacy.ReadHoveredText);
        }));

        test("profile export and import retain visual choices alongside deliberate switch mappings", () => InTemporaryFolder(folder =>
        {
            var settings = AdaptiveAccessibilityProfiles.All[0].Settings with { VisualAccess = IndependentChoices, InterfaceScale = 2 };
            string path = Path.Combine(folder, "switches.relay.json");
            ProfileStore.Export(path, new Profile("switches", "My switches", settings));
            var imported = ProfileStore.Import(path);
            Check(imported.Settings.EffectiveVisualAccess == IndependentChoices && imported.Settings.InterfaceScale == 2 &&
                imported.Settings.ButtonMappings!.All(pair => pair.Value == settings.ButtonMappings![pair.Key]));
        }));
    }

    private static void InTemporaryFolder(Action<string> test)
    {
        string folder = Path.Combine(Path.GetTempPath(), "Relay.VisualAccess.Tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try { test(folder); }
        finally { Directory.Delete(folder, true); }
    }
}

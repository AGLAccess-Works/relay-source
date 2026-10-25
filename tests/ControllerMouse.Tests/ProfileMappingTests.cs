using System.Text.Json;
using ControllerMouse.Core;
using ControllerMouse.App;

internal static class ProfileMappingTests
{
    private static void Check(bool value) { if (!value) throw new Exception("Assertion failed"); }
    private static Gamepad Pad(Control c) => c switch
    {
        Control.LeftTrigger => new(0, 100, 0, 0, 0, 0, 0),
        Control.RightTrigger => new(0, 0, 100, 0, 0, 0, 0),
        _ => new(Enum.Parse<Buttons>(c.ToString()), 0, 0, 0, 0, 0, 0)
    };
    private static (InputProcessor, Recorder) Ready(Dictionary<Control, ControlBinding> bindings)
    {
        var recorder = new Recorder(); var engine = new InputProcessor(recorder) { Settings = new Settings { ButtonMappings = bindings } };
        engine.Process(default(Gamepad), .008); return (engine, recorder);
    }
    public static void Run(Action<string, Action> test)
    {
        foreach (var control in ControlMapping.Controls.Select(c => c.Control))
        {
            test($"{control} can map to a custom keyboard chord exactly once", () =>
            {
                var (engine, sink) = Ready(new() { [control] = new(BindingKind.Keyboard, Key: 0x43, Modifiers: 1) });
                for (int i = 0; i < 80; i++) engine.Process(Pad(control), .008);
                if (control is Control.Back or Control.Start) Check(sink.Events.Count == 0);
                engine.Process(default(Gamepad), .008);
                Check(sink.Events.SequenceEqual(new[] { "Keys:17,67" }));
            });
            test($"{control} can be disabled", () =>
            {
                var (engine, sink) = Ready(new() { [control] = new() });
                engine.Process(Pad(control), .008); engine.Process(default(Gamepad), .008); Check(sink.Events.Count == 0);
            });
        }
        test("shared mouse bindings keep drag held until the last owner releases", () =>
        {
            var (engine, sink) = Ready(new() { [Control.B] = new(BindingKind.Mouse) });
            engine.Process(Pad(Control.A), .008);
            engine.Process(Pad(Control.A) with { Buttons = Buttons.A | Buttons.B }, .008);
            engine.Process(Pad(Control.B), .008); Check(sink.Events.SequenceEqual(new[] { "Left:True" }));
            engine.Process(default(Gamepad), .008); Check(sink.Events.Last() == "Left:False");
        });
        test("trigger mouse binding retains hysteresis and releases on disconnect", () =>
        {
            var (engine, sink) = Ready(new() { [Control.LeftTrigger] = new(BindingKind.Mouse) });
            foreach (byte value in new byte[] { 51, 49, 36, 50 }) engine.Process(Pad(Control.LeftTrigger) with { LeftTrigger = value }, .008);
            Check(sink.Events.SequenceEqual(new[] { "Left:True" }));
            engine.Process(null, .008); Check(sink.Events.Last() == "Left:False");
        });
        test("mapping change releases held inputs and requires neutral before new action", () =>
        {
            var (engine, sink) = Ready(new()); engine.Process(Pad(Control.A), .008);
            engine.Settings = new Settings { ButtonMappings = new() { [Control.A] = new(BindingKind.Keyboard, Key: 13) } };
            Check(!engine.Armed && sink.Events.Last() == "Left:False"); sink.Events.Clear();
            engine.Process(Pad(Control.A), .008); Check(sink.Events.Count == 0);
            engine.Process(default(Gamepad), .008); engine.Process(Pad(Control.A), .008); Check(sink.Events.Single() == "Keys:13");
        });
        test("remapped View and Menu never fire when the pause chord is used", () =>
        {
            var (engine, sink) = Ready(new() { [Control.Back] = new(BindingKind.Mouse), [Control.Start] = new(BindingKind.Keyboard, Key: 13) });
            engine.Process(Pad(Control.Back), .008);
            engine.Process(Pad(Control.Back) with { Buttons = Buttons.Back | Buttons.Start }, .008);
            engine.Process(default(Gamepad), .008); Check(!engine.Enabled && sink.Events.Count == 0);
        });
        test("remapping a bumper removes its old pointer and scroll modifier", () =>
        {
            var settings = new Settings { Smoothing = 0, ScrollSmoothing = 0, ButtonMappings = new() { [Control.LeftShoulder] = new() } };
            var sink = new Recorder(); var engine = new InputProcessor(sink) { Settings = settings }; engine.Process(default(Gamepad), .008);
            for (int i = 0; i < 50; i++) engine.Process(Pad(Control.LeftShoulder) with { LeftX = 32767, RightY = 32767 }, .02);
            Check(Math.Abs(sink.X - 1100) <= 1 && Math.Abs(sink.Vertical - 960) <= 1);
            Check(!ControlMapping.Modifier(Pad(Control.LeftShoulder), settings, BindingKind.Precision));
        });
        test("precision can move to another button and affects both sticks", () =>
        {
            var settings = new Settings { Smoothing = 0, ScrollSmoothing = 0, ButtonMappings = new() { [Control.A] = new(BindingKind.Precision) } };
            var sink = new Recorder(); var engine = new InputProcessor(sink) { Settings = settings }; engine.Process(default(Gamepad), .008);
            for (int i = 0; i < 50; i++) engine.Process(Pad(Control.A) with { LeftX = 32767, RightY = 32767 }, .02);
            Check(Math.Abs(sink.X - 308) <= 1 && Math.Abs(sink.Vertical - 336) <= 1 && !sink.Events.Contains("Left:True"));
        });
        test("trigger precision remains stable in the hysteresis band", () =>
        {
            var (engine, sink) = Ready(new() { [Control.LeftTrigger] = new(BindingKind.Precision) });
            engine.Settings = engine.Settings with { Smoothing = 0 }; engine.Process(default(Gamepad), .008);
            engine.Process(Pad(Control.LeftTrigger) with { LeftX = 32767 }, .05);
            engine.Process(Pad(Control.LeftTrigger) with { LeftTrigger = 40, LeftX = 32767 }, .05);
            Check(sink.X is >= 30 and <= 31);
        });
        test("navigation keyboard taps use extended-key flags on down and up", () =>
        {
            var sent = new List<NativeMethods.Input>();
            var sink = new WindowsInputSink(inputs => { sent.AddRange(inputs); return (uint)inputs.Length; });
            sink.InvokeKeys([0x24]);
            Check(sent.Count == 2 && sent[0].Data.Keyboard.Flags == 1 && sent[1].Data.Keyboard.Flags == 3);
        });
        test("invalid mappings are sanitised while legacy tool choices survive", () =>
        {
            var settings = new Settings
            {
                YAction = Shortcut.Narrator,
                ButtonMappings = new()
                {
                    [Control.Y] = new((BindingKind)999),
                    [(Control)999] = new(),
                    [Control.Start] = new(BindingKind.Precision),
                    [Control.A] = new(BindingKind.Keyboard, Key: 0)
                }
            }.Validate();
            Check(settings.ButtonMappings!.Count == 0 && ControlMapping.Resolve(settings, Control.Y).Shortcut == Shortcut.Narrator);
        });
        test("custom shortcut cleans up every possible partial accepted prefix", () =>
        {
            for (uint prefix = 0; prefix < 8; prefix++)
            {
                bool first = true; var releases = new List<NativeMethods.Input>();
                var sink = new WindowsInputSink(inputs => { if (first) { first = false; return prefix; } releases.AddRange(inputs); return (uint)inputs.Length; });
                sink.InvokeKeys([0x11, 0x12, 0x10, 0x41]);
                Check(!sink.HasHeldInputs && releases.All(i => (i.Data.Keyboard.Flags & 2) != 0));
            }
        });
        test("profiles preserve existing settings, isolate edits, and persist selection", () => WithFolder(folder =>
        {
            var store = new ProfileStore(Path.Combine(folder, "profiles.json"));
            var library = store.Load(new Settings { PointerSpeed = 777, YAction = Shortcut.Narrator });
            Check(library.Profiles.Length == 5 + AccessibilityProfiles.All.Count && library.Active.Settings.PointerSpeed == 777);
            library = library.Update(library.Active.Settings with { ButtonMappings = new() { [Control.A] = new(BindingKind.Keyboard, Key: 13) } });
            library = library with { ActiveId = "reading" }; store.Save(library);
            var loaded = store.Load(new Settings()); Check(loaded.ActiveId == "reading");
            Check(loaded.Profiles[0].Settings.ButtonMappings![Control.A].Key == 13 && ControlMapping.Resolve(loaded.Active.Settings, Control.A).Kind == BindingKind.Mouse);
        }));
        test("accessibility defaults migrate once without changing active settings or restoring deleted profiles", () =>
        {
            var original = new Profile("mine", "My setup", new Settings { PointerSpeed = 789 });
            var migrated = new ProfileLibrary(1, original.Id, [original]).WithAccessibilityDefaults();
            Check(migrated.Active == original && migrated.Profiles.Length == 1 + AccessibilityProfiles.All.Count);
            var deleted = migrated with { Profiles = [original] };
            Check(deleted.WithAccessibilityDefaults().Profiles.Length == 1);
            Check(AccessibilityProfiles.All.All(p => p.Settings.Validate() == p.Settings || p.Settings.ButtonMappings != null));
        });
        test("accessibility defaults respect collisions and the library limit", () =>
        {
            var profiles = Enumerable.Range(0, 64).Select(i => new Profile(i.ToString(), "Profile " + i, new Settings())).ToArray();
            Check(new ProfileLibrary(1, "0", profiles).WithAccessibilityDefaults().Profiles.Length == 64);
            var collision = new Profile("mine", "Magnifier", new Settings { PointerSpeed = 999 });
            var result = new ProfileLibrary(1, "mine", [collision]).WithAccessibilityDefaults().Validate();
            Check(result.Profiles.Count(p => p.Name == "Magnifier") == 1 && result.Active.Settings.PointerSpeed == 999);
            Check(new Settings { InterfaceScale = double.NaN }.Validate().InterfaceScale == 1);
        });
        test("profile export import round trip preserves mappings with a new identity", () => WithFolder(folder =>
        {
            var profile = new Profile("id", "My profile", new Settings { ButtonMappings = new() { [Control.RightTrigger] = new(BindingKind.Keyboard, Key: 67, Modifiers: 5) } });
            string path = Path.Combine(folder, "export.json"); ProfileStore.Export(path, profile); var imported = ProfileStore.Import(path);
            Check(imported.Id != profile.Id && imported.Name == profile.Name && imported.Settings.ButtonMappings![Control.RightTrigger] == profile.Settings.ButtonMappings![Control.RightTrigger]);
        }));
        test("corrupt profile library is backed up before recovery saves", () => WithFolder(folder =>
        {
            string path = Path.Combine(folder, "profiles.json"); File.WriteAllText(path, "{broken");
            var store = new ProfileStore(path); var recovered = store.Load(new Settings { PointerSpeed = 888 });
            Check(store.Warning != null && recovered.Active.Settings.PointerSpeed == 888);
            store.Save(recovered); Check(store.Warning == null && !File.Exists(path + ".tmp"));
            Check(File.ReadAllText(Directory.GetFiles(folder, "*.recovery-*").Single()) == "{broken");
        }));
        test("profile imports reject invalid versions, invalid bindings and oversized files", () => WithFolder(folder =>
        {
            string path = Path.Combine(folder, "import.json");
            foreach (string json in new[] { "{}", "{broken", "{\"Version\":99,\"Name\":\"Test\",\"Settings\":{}}",
                "{\"Version\":1,\"Name\":\"Test\",\"Settings\":{\"ButtonMappings\":{\"A\":{\"Kind\":3,\"Key\":0}}}}", new string(' ', 100001) })
            {
                File.WriteAllText(path, json); bool rejected = false;
                try { ProfileStore.Import(path); } catch (JsonException) { rejected = true; }
                Check(rejected);
            }
        }));
        test("profile names and duplicate identities are validated", () =>
        {
            Check(!ProfileLibrary.ValidName(" ") && !ProfileLibrary.ValidName("a\nb") && !ProfileLibrary.ValidName(new string('a', 61)));
            var library = ProfileLibrary.Create(new Settings());
            bool rejected = false;
            try { (library with { Profiles = [library.Active, library.Active] }).Validate(); } catch (JsonException) { rejected = true; }
            Check(rejected);
        });
    }
    private static void WithFolder(Action<string> test)
    {
        string folder = Path.Combine(Path.GetTempPath(), "relay-profile-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try { test(folder); } finally { Directory.Delete(folder, true); }
    }
}
